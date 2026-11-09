using Newtonsoft.Json.Linq;
using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 批次 B27（R8 Q216、R36 Q246、R37 Q247、R38 Q248、R39 Q249）。规则来源：策划合同 knowledge-source-adjudications.json；
/// 验收 KNOW-Q246-01/02、KNOW-Q247-01/02、SAVE-Q248-01、TEXT-Q249-01/02。来源线索（Q250）另见 B27SourceHintTests。
/// </summary>
public class B27KnowledgeSourceTests : TestBase
{
    private static readonly JObject Contract = JObject.Parse(File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "design.knowledge-source.json")));

    private static readonly List<JToken> Sources = ((JArray)Contract["blueprintSources"]!).ToList();

    private static readonly string[] Five = Sources.Select(x => (string)x["knowledgeId"]!).ToArray();

    private static readonly IEnumerable<string> Permanent = Five.Select(k => "knowledge:" + k);

    private static readonly string Q240Log = (string)JObject.Parse(File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "design.substitute-followup.json")))["policy"]!["upgradeLog"]!;

    private void Ok(ActionResult r) => Assert.That(r.Ok, Is.True, r.Reason);

    private GameState At(string loc)
    {
        var s = New();
        s.GameMinute = At(2, 9, 0);
        s.SpawnNextCheck = int.MaxValue;
        s.PatrolNextCheck = int.MaxValue;
        E.DebugMapTeleport(s, loc);
        ReadDialogues(s);
        return s;
    }

    /// <summary>已登记的档（事实齐全，五项没有任何逐项记录）。</summary>
    private GameState Registered(string loc = "h03_workshop")
    {
        var s = At(loc);
        s.Progress.Flags.UnionWith(new[] { "registration_authorized", "hub_registered" });
        return s;
    }

    /// <summary>营门办过授权、人还在营门的档。</summary>
    private GameState Authorized()
    {
        var s = At("h01_gate");
        Ok(E.StartCityService(s, "hub_registration_existing"));
        ReadDialogues(s);
        return s;
    }

    /// <summary>把档写成旧版本再走完整读档入口（43 字段全去掉，等同旧版存出来的档）。</summary>
    private GameState Upgrade(GameState s, string schema = SaveMigrator.Schema42, Action<JObject>? edit = null)
    {
        var j = JObject.Parse(SaveSerializer.Serialize(s));
        j["SchemaVersion"] = schema;
        ((JObject)j["City"]!).Remove("LegacyRegistrationPending");
        edit?.Invoke(j);
        return SaveSerializer.Deserialize(j.ToString(), Cfg, out _);
    }

    private static GameState Reload(GameState s) => SaveSerializer.Deserialize(SaveSerializer.Serialize(s), Cfg);

    private void Travel(GameState s, string to)
    {
        Start(s, ActionRequest.Travel(to));
        Run(s);
        ReadDialogues(s);
        Assert.That(s.CurrentLocation, Is.EqualTo(to));
    }

    private static List<string> RegistrationLogs(GameState s) => s.Log.Where(l => l.Text.Contains("营区登记完成")).Select(l => l.Text).ToList();

    private bool KnowsAny(GameState s) => Five.Any(k => E.KnowsKnowledge(s, k));

    private bool KnowsAll(GameState s) => Five.All(k => E.KnowsKnowledge(s, k));

    private static (int Items, long Xp) Ledger(GameState s) => (s.Items.Sum(i => i.Count), s.Skills.Sum(kv => kv.Value.Xp));

    // ───────────── Q216 配置 ─────────────

    /// <summary>登记默认 26→21；五张只留核心记录与抄录事实，知识包成员和 30 分钟 0 材料不动。</summary>
    [Test]
    public void Q216_Registration21_FiveKeepClaimAndCopy()
    {
        Assert.That(Cfg.Knowledge.Count(k => k.GrantedByFacts.Contains("hub_registered")), Is.EqualTo(21));
        Assert.That(Cfg.City.Registration.LegacyKnowledge, Is.EquivalentTo(Five));
        foreach (var x in Sources)
        {
            var k = Cfg.Knowledge.Single(c => c.Id == (string)x["knowledgeId"]!);
            var p = Cfg.ExpansionQuests.Packs.Single(c => c.Id == (string)x["packageId"]!);
            Assert.That(k.GrantedByFacts, Is.EquivalentTo(new[] { "claim:" + (string)x["recordNode"]! + "_unique", p.CopyFact }), k.Id);
            Assert.That(p.Knowledge, Does.Contain(k.Id));
            Assert.That(p.CopyLocationId, Is.EqualTo((string)x["copyLocation"]!));
            Assert.That(p.CopyMinutes, Is.EqualTo((int)x["copyMinutes"]!));
        }
    }

    // ───────────── KNOW-Q246-01：已登记旧档 ─────────────

    /// <summary>42 或更早的已登记档：五项全写永久知识（无逐项记录也算），重载仍已知，配方不反锁，不发物资经验；再存再读不变。</summary>
    [TestCase("42")]
    [TestCase("41")]
    [TestCase("30")]
    public void KNOW_Q246_01_RegisteredOldSave_FivePermanent(string schema)
    {
        var s = Registered();
        var ledger = Ledger(s);
        var m = Upgrade(s, schema);
        Assert.That(m.SchemaVersion, Is.EqualTo(GameState.CurrentSchema), "B27c 起链尾为 44");
        Assert.That(m.Progress.Flags, Is.SupersetOf(Permanent));
        Assert.That(KnowsAll(m), Is.True);
        Assert.That(m.City.LegacyRegistrationPending, Is.False, "已登记不是在办");
        foreach (var x in Sources)
            Assert.That(E.RecipeKnown(m, E.Recipe("make_" + (string)x["itemId"]!)), Is.True, "已知配方不反锁");
        Assert.That(Ledger(m), Is.EqualTo(ledger), "不发物资经验");
        var r = Reload(Reload(m));
        Assert.That(KnowsAll(r), Is.True);
        Assert.That(r.Progress.Flags, Is.EquivalentTo(m.Progress.Flags), "幂等");
        Assert.That(new[] { "claim:d06_08_unique", "claim:d07_07_unique", "claim:d05_07_unique" }.Any(r.Progress.Flags.Contains), Is.False, "不伪造地牢记录");
    }

    /// <summary>登记后已显式学过一部分的旧档：集合并入，已有的不动，不双授。</summary>
    [Test]
    public void KNOW_Q246_01_AlreadyLearnedPart_SetMerge()
    {
        var s = Registered();
        s.Progress.Flags.UnionWith(new[] { "claim:d06_08_unique", "kpcopy:kp_waterworks", "knowledge:" + Five[0] });
        var before = s.Progress.Flags.ToHashSet();
        var m = Upgrade(s);
        Assert.That(m.Progress.Flags, Is.EquivalentTo(before.Union(Permanent)));
    }

    // ───────────── KNOW-Q246-02：新规则 ─────────────

    /// <summary>新局真实登记只给 21 项，日志写 21；保存重载、再到广场都不补五项。</summary>
    [Test]
    public void KNOW_Q246_02_NewRegistration_21_NoBackfill()
    {
        var s = Authorized();
        Assert.That(s.City.LegacyRegistrationPending, Is.False);
        Travel(s, "h02_square");
        Assert.That(s.Progress.Flags, Does.Contain("hub_registered"));
        Assert.That(RegistrationLogs(s).Single(), Does.Contain("学会营区登记默认做法知识 21 项"));
        Assert.That(KnowsAny(s), Is.False);
        Assert.That(Cfg.Knowledge.Where(k => k.GrantedByFacts.Contains("hub_registered")).All(k => E.KnowsKnowledge(s, k.Id)), Is.True);
        var r = Reload(s);
        Assert.That(KnowsAny(r), Is.False);
        Travel(r, "h01_gate");
        Travel(r, "h02_square");
        Assert.That(KnowsAny(r), Is.False, "再到广场不补");
        Assert.That(RegistrationLogs(r), Has.Count.EqualTo(1));
    }

    /// <summary>43 档看见 hub_registered 不当旧知识补发（读档入口不再走迁移）。</summary>
    [Test]
    public void KNOW_Q246_02_Schema43_RegisteredFact_NoBackfill()
    {
        var s = Registered();
        Assert.That(s.SchemaVersion, Is.EqualTo(GameState.CurrentSchema), "43 起的当前档（B27c 起 44）");
        var flags = s.Progress.Flags.ToHashSet();
        var r = Reload(Reload(s));
        Assert.That(r.Progress.Flags, Is.EquivalentTo(flags));
        Assert.That(KnowsAny(r), Is.False);
    }

    /// <summary>未登记但原已显式学会（领过记录/抄过）的知识，迁移和新登记都保留。</summary>
    [Test]
    public void KNOW_Q246_02_UnregisteredExplicitKnowledge_Kept()
    {
        var s = At("h01_gate");
        s.Progress.Flags.UnionWith(new[] { "claim:d06_08_unique", "knowledge:blueprint_field_medkit" });
        var m = Upgrade(s);
        Assert.That(E.KnowsKnowledge(m, "blueprint_gather_gloves"), Is.True);
        Assert.That(E.KnowsKnowledge(m, "blueprint_utility_trousers"), Is.True);
        Assert.That(E.KnowsKnowledge(m, "blueprint_field_medkit"), Is.True);
        Assert.That(E.KnowsKnowledge(m, "blueprint_filter_token"), Is.False);
        Ok(E.StartCityService(m, "hub_registration_existing"));
        ReadDialogues(m);
        Travel(m, "h02_square");
        Assert.That(E.KnowsKnowledge(m, "blueprint_field_medkit"), Is.True);
        Assert.That(E.KnowsKnowledge(m, "blueprint_filter_token"), Is.False);
    }

    // ───────────── KNOW-Q247-01：旧在办 ─────────────

    /// <summary>
    /// 只有授权的旧档：迁移不提前完成/授予/增容，资格随存档；重载后真实到广场一次按 26 项完成，五项永久入账、容量与 QMAP01 照常；
    /// 之后重载、再到广场不双发（日志一条、物资不变）。
    /// </summary>
    [Test]
    public void KNOW_Q247_01_PendingOldSave_Reload_Square26_NoDouble()
    {
        var s = Authorized();
        var ledger = Ledger(s);
        var m = Upgrade(s);
        Assert.That(m.City.LegacyRegistrationPending, Is.True);
        Assert.That(m.Progress.Flags, Does.Not.Contain("hub_registered"));
        Assert.That(m.Progress.Flags, Does.Contain("registration_authorized"));
        Assert.That(KnowsAny(m), Is.False, "迁移时不提前发");
        Assert.That(E.BattleCapacity(m), Is.EqualTo(1), "不提前增容");
        Assert.That(m.CurrentLocation, Is.EqualTo("h01_gate"), "不补行走");

        var r = Reload(m);
        Assert.That(r.City.LegacyRegistrationPending, Is.True, "资格随存档");
        Travel(r, "h02_square");
        Assert.That(RegistrationLogs(r).Single(), Does.Contain("学会营区登记默认做法知识 26 项"));
        Assert.That(r.Progress.Flags, Is.SupersetOf(Permanent));
        Assert.That(r.Progress.Flags, Does.Contain("hub_registered").And.Contain(GameEngine.QuestFact("QMAP01", "completed")));
        Assert.That(E.BattleCapacity(r), Is.EqualTo(2));
        Assert.That(r.City.LegacyRegistrationPending, Is.False);
        var items = r.Items.Sum(i => i.Count);

        var again = Reload(r);
        Assert.That(KnowsAll(again), Is.True, "知识持久");
        Travel(again, "h01_gate");
        Travel(again, "h02_square");
        Assert.That(RegistrationLogs(again), Has.Count.EqualTo(1), "事务不双发");
        Assert.That(again.Items.Sum(i => i.Count), Is.EqualTo(items));
        Assert.That(Ledger(again).Xp, Is.GreaterThanOrEqualTo(ledger.Xp));
    }

    /// <summary>营门到广场途中存的旧档：行动快照不动，走完按 26 项完成。</summary>
    [Test]
    public void KNOW_Q247_01_MidTravelOldSave_ContinuesTo26()
    {
        var s = Authorized();
        Start(s, ActionRequest.Travel("h02_square"));   // 营门到广场只有一段，开工后就存档
        Assert.That(s.Main, Is.Not.Null, "存档时人还在路上");
        var m = Upgrade(s);
        Assert.That(m.Main, Is.Not.Null, "行动快照不动");
        Assert.That(m.Main!.TargetId, Is.EqualTo(s.Main!.TargetId));
        Run(m);
        ReadDialogues(m);
        Assert.That(m.CurrentLocation, Is.EqualTo("h02_square"));
        Assert.That(RegistrationLogs(m).Single(), Does.Contain("26 项"));
    }

    // ───────────── KNOW-Q247-02：无资格 ─────────────

    /// <summary>无授权的旧档迁移不写资格；之后才办的营门授权到广场只有 21 项。</summary>
    [Test]
    public void KNOW_Q247_02_NoAuthOldSave_NewAuth_21()
    {
        var m = Upgrade(At("h01_gate"));
        Assert.That(m.City.LegacyRegistrationPending, Is.False);
        Ok(E.StartCityService(m, "hub_registration_existing"));
        ReadDialogues(m);
        Assert.That(m.City.LegacyRegistrationPending, Is.False, "新授权不造资格");
        var r = Reload(m);
        Assert.That(r.City.LegacyRegistrationPending, Is.False, "43 档不重判旧授权");
        Travel(r, "h02_square");
        Assert.That(RegistrationLogs(r).Single(), Does.Contain("21 项"));
        Assert.That(KnowsAny(r), Is.False);
    }

    /// <summary>当前地点不造资格：人在广场、无授权的旧档迁移后什么都不写。</summary>
    [Test]
    public void KNOW_Q247_02_AtSquareWithoutAuth_NoGuess()
    {
        var m = Upgrade(At("h02_square"));
        Assert.That(m.City.LegacyRegistrationPending, Is.False);
        Assert.That(m.Progress.Flags, Does.Not.Contain("hub_registered"));
        Assert.That(KnowsAny(m), Is.False);
    }

    /// <summary>进行中的抄录随旧档继续，不清零；完成后只写抄录事实（已登记迁移的五项已知时走“完全已知”原规则）。</summary>
    [Test]
    public void KNOW_Q247_02_OldCopyInProgress_KeepsProgress()
    {
        var s = Registered();
        Ok(E.StartCityService(s, "copy_public_knowledge", null, "kp_waterworks"));
        Steps(s, 1);
        var done = s.Main!.ElapsedMinutes;
        var m = Upgrade(s);
        Assert.That(m.Main!.ElapsedMinutes, Is.EqualTo(done));
        Assert.That(m.City.Pending, Is.Not.Null);
        Run(m);
        ReadDialogues(m);
        Assert.That(E.KnowsKnowledge(m, "blueprint_repair_manual"), Is.True, "维护手册照常抄到");
        Assert.That(m.City.Pending, Is.Null);
    }

    /// <summary>开发入口只记容量事实：说明不再列五张，也不授五项（只认 knowledge.grantedByFacts，不看旧资格）。</summary>
    [Test]
    public void KNOW_Q246_02_DevCapacityFact_NoFive()
    {
        var s = At("camp");
        var text = E.DebugCapacityFact(s, "hub_registered");
        Assert.That(KnowsAny(s), Is.False);
        foreach (var k in Five) Assert.That(text, Does.Not.Contain(Cfg.Knowledge.Single(c => c.Id == k).Name));
    }

    // ───────────── SAVE-Q248-01：静默 ─────────────

    /// <summary>已登记 / 旧在办 / 未登记三种旧档的 43 步骤：日志、对话、时间一字不增；已 43 重载也不补。</summary>
    [Test]
    public void SAVE_Q248_01_Step43_Silent()
    {
        foreach (var s in new[] { Registered(), Authorized(), At("h01_gate") })
        {
            var logs = s.Log.Select(l => l.Text).ToList();
            var talks = s.Story.DialogueLog.Count;
            var m = Upgrade(s);
            Assert.That(m.Log.Select(l => l.Text), Is.EqualTo(logs), "43 不写日志");
            Assert.That((m.Story.DialogueLog.Count, m.GameMinute, E.DialogueLocked(m)), Is.EqualTo((talks, s.GameMinute, false)));
            var j = JObject.Parse(SaveSerializer.Serialize(s));
            j["SchemaVersion"] = SaveMigrator.Schema42;
            SaveSerializer.Deserialize(j.ToString(), Cfg, out var notes);
            Assert.That(notes, Is.Empty, "没有读档提示");
            Assert.That(Reload(m).Log.Select(l => l.Text), Is.EqualTo(logs));
        }
    }

    /// <summary>41 档跨 42：Q240 原句逐字一次、版本号仍 42，是最后一条；没有版本 43 的句子，旧日志原样。</summary>
    [Test]
    public void SAVE_Q248_01_CrossSchema42_Q240Verbatim()
    {
        var s = Registered();
        var logs = s.Log.Select(l => l.Text).ToList();
        var m = Upgrade(s, SaveMigrator.Schema41);
        Assert.That(Q240Log, Does.StartWith("存档已升级到版本42（"));
        Assert.That(m.Log.Select(l => l.Text), Is.EqualTo(logs.Append(Q240Log)));
        Assert.That(m.Log.Any(l => l.Text.Contains("版本43")), Is.False);
    }

    /// <summary>
    /// 20 档走完整条链：20→21 那句原来拼当前版本号、线上一直写“版本42”，升 43 后原样；中途每步各写一句，版本号都不是 43；末尾仍是 Q240 原句。
    /// </summary>
    [Test]
    public void SAVE_Q248_01_OldChain_HistoricalLogsUnchanged()
    {
        var s = At("h01_gate");
        var before = s.Log.Count;
        var m = Upgrade(s, SaveMigrator.Schema20, j => ((JObject)j["City"]!).Remove("SubstituteIntroShown"));
        var added = m.Log.Skip(before).Select(l => l.Text).ToList();
        Assert.That(added.Count(t => t.StartsWith("存档已升级到版本42（资源四账：新增")), Is.EqualTo(1));
        Assert.That(added.Count(t => t.StartsWith("存档已升级到版本41（")), Is.EqualTo(1));
        Assert.That(added.Last(), Is.EqualTo(Q240Log));
        Assert.That(added.Count(t => t == Q240Log), Is.EqualTo(1));
        Assert.That(added.Any(t => t.Contains("版本43")), Is.False);
        Assert.That(added.Count(t => t.StartsWith("存档已升级到版本")), Is.EqualTo(22), "21—42 每步一句，43 没有");
    }

    // ───────────── TEXT-Q249-01：五句来源 ─────────────

    /// <summary>五项 source 逐字等于合同，名称、记录、地点、抄录资料对得上，不再写登记默认；配方查询和未知原因跟着走；其他知识 source 没动。</summary>
    [Test]
    public void TEXT_Q249_01_FiveSources_Verbatim_OthersUntouched()
    {
        foreach (var x in Sources)
        {
            var k = Cfg.Knowledge.Single(c => c.Id == (string)x["knowledgeId"]!);
            var src = (string)x["source"]!;
            Assert.That(k.Source, Is.EqualTo(src));
            Assert.That(k.Name, Does.StartWith((string)x["name"]!));
            foreach (var part in new[] { "dungeonName", "roomName", "recordName", "copyLocationName", "copyAction", "copyMaterial" })
                Assert.That(src, Does.Contain((string)x[part]!), part);
            Assert.That(src, Does.Contain("固定等待30分钟，0材料"));
            Assert.That(src, Does.Not.Contain("默认"));
            var res = Cfg.Resources.Single(r => r.NodeId == (string)x["recordNode"]! + "_unique");
            Assert.That(res.Name, Is.EqualTo((string)x["recordName"]!));
            var rc = E.Recipe("make_" + (string)x["itemId"]!);
            Assert.That(E.RecipeUnknownReason(New(), rc), Does.Contain(src));
        }
        var rest = Cfg.Knowledge.Where(k => k.GrantedByFacts.Contains("hub_registered")).ToList();
        Assert.That(rest.All(k => k.Source == "营区登记默认知识（h02 广场）"), Is.True, "其余 21 项 source 不动");
    }

    /// <summary>装备来源（“来源：…”）前段工位/制造/分钟保留，末段换成同一条知识的定稿句；只有这五件改了。</summary>
    [Test]
    public void TEXT_Q249_01_EquipmentSourceText()
    {
        foreach (var x in Sources)
        {
            var e = E.EquipmentSource((string)x["itemId"]!)!;
            Assert.That(e.Knowledge, Is.EqualTo((string)x["knowledgeId"]!));
            var cut = e.Text.IndexOf('；');
            Assert.That(e.Text.Substring(0, cut), Does.Match("^[^／]+／制造[0-9]+／[0-9]+分钟$"));
            Assert.That(e.Text.Substring(cut + 1), Is.EqualTo((string)x["source"]!));
        }
        Assert.That(Cfg.Workshops.EquipmentSources.Where(e => !Five.Contains(e.Knowledge)).Count(e => e.Text.EndsWith("营区登记默认知识（h02 广场）")), Is.EqualTo(21));
    }

    // ───────────── TEXT-Q249-02：改稿不改规则 ─────────────

    /// <summary>新登记的档：抄录 30 分钟 0 材料学到包内知识（不要求先通关），只写抄录事实；部分已知只补缺项，完全已知禁用；不发物不给经验。</summary>
    [Test]
    public void TEXT_Q249_02_Copy_Rules_Unchanged()
    {
        var s = Registered();
        var v = E.CityServicesOf(s, "npc_luo_zheng").Single(x => x.Id == "copy_public_knowledge");
        Assert.That(v.Choices.Select(c => c.Label), Is.EqualTo(new[]
        {
            "公开资料：净水操作套件的做法、维护手册蓝图",
            "公开作业资料：采集手套的做法、作业长裤的做法",
        }), "登记不再默认给，入口不再是灰的");
        Assert.That(v.Choices.All(c => c.Blocked == null), Is.True);

        var ledger = Ledger(s);
        var flags = s.Progress.Flags.ToHashSet();
        var t0 = s.GameMinute;
        Ok(E.StartCityService(s, "copy_public_knowledge", null, "kp_mining"));
        Run(s);
        ReadDialogues(s);
        Assert.That(s.GameMinute - t0, Is.EqualTo(30));
        Assert.That(E.KnowsKnowledge(s, "blueprint_gather_gloves") && E.KnowsKnowledge(s, "blueprint_utility_trousers"), Is.True);
        Assert.That(s.Progress.Flags.Except(flags), Is.EquivalentTo(new[] { "kpcopy:kp_mining" }));
        Assert.That(Ledger(s).Items, Is.EqualTo(ledger.Items));
        Assert.That(E.CampaignClaimed(s, "d06_08_unique"), Is.False);

        v = E.CityServicePreview(s, "copy_public_knowledge")!;
        Assert.That(v.Choices[1].Blocked, Does.StartWith("已全部掌握"), "完全已知禁用");
        Assert.That(E.StartCityService(s, "copy_public_knowledge", null, "kp_mining").Ok, Is.False);

        s.Progress.Flags.Add("knowledge:blueprint_filter_token");
        v = E.CityServicePreview(s, "copy_public_knowledge")!;
        Assert.That(v.Choices[0].Label, Is.EqualTo("公开资料：净水操作套件的做法（已掌握）、维护手册蓝图"));
        Assert.That(v.Choices[0].Blocked, Is.Null, "部分已知只补缺项");
    }

    /// <summary>未登记时抄录仍禁用（要先登记）；来源改稿本身不授知识：新局五项都不知道。</summary>
    [Test]
    public void TEXT_Q249_02_SourceTextGrantsNothing()
    {
        var s = At("h03_workshop");
        Assert.That(KnowsAny(s), Is.False);
        Assert.That(E.CityServicesOf(s, "npc_luo_zheng").Single(x => x.Id == "copy_public_knowledge").Blocked, Does.Contain("营区登记"));
        foreach (var k in Five)
            Assert.That(Cfg.Knowledge.Single(c => c.Id == k).Initial, Is.False);
    }

    /// <summary>新登记未学的玩家领核心记录：同事务学到包内蓝图，不再写“知识已知”。</summary>
    [Test]
    public void TEXT_Q249_02_RecordClaim_TeachesFive()
    {
        var s = Registered();
        E.DebugExq(s, "record", "EXQ05");
        ReadDialogues(s);
        var p = E.Preview(s, ActionRequest.Gather("d05_07_unique"));
        Assert.That(p.Notes, Has.Some.Contains("同时获得做法知识：净水操作套件的做法、维护手册蓝图"));
        Assert.That(p.Notes, Has.None.Contains("知识已知"));
    }

    // ───────────── B27 试玩修复 ─────────────

    /// <summary>配方列表短句：五张蓝图登记前后都是“需要蓝图”，21 项登记默认知识不变；悬停的完整原因不动。</summary>
    [Test]
    public void B27_Playtest_LockShort_FiveBlueprints_NotRegistration()
    {
        var s = Registered();
        foreach (var k in Five)
        {
            var r = Cfg.Recipes.First(x => x.Knowledge == k);
            var why = E.RecipeUnknownReason(s, r);
            Assert.That(why, Does.StartWith("还不知道").And.Contain((string)Sources.Single(x => (string)x["knowledgeId"]! == k)["source"]!), k);
            Assert.That(E.RecipeLockShort(r, why!), Is.EqualTo("未掌握：需要蓝图"), k);
        }
        var fresh = At("h03_workshop");
        var regs = Cfg.Recipes.Where(r => r.Knowledge != null && Cfg.Knowledge.Single(x => x.Id == r.Knowledge).GrantedByFacts.Contains("hub_registered")).ToList();
        Assert.That(regs, Is.Not.Empty);
        foreach (var r in regs) Assert.That(E.RecipeLockShort(r, E.RecipeUnknownReason(fresh, r)!), Is.EqualTo("未掌握：营区登记后学会"), r.Id);
    }

    /// <summary>其他锁定原因的短句和原来一样。</summary>
    [Test]
    public void B27_Playtest_LockShort_OtherReasonsKept()
    {
        var r = Cfg.Recipes.First(x => x.Knowledge == Five[0]);
        Assert.That(E.RecipeLockShort(r, "未掌握：需要某图（来源）"), Is.EqualTo("未掌握：需要蓝图"));
        Assert.That(E.RecipeLockShort(r, "需要火堆：先建"), Is.EqualTo("需要火堆"));
        Assert.That(E.RecipeLockShort(r, "材料不足"), Is.EqualTo("材料不足"));
    }

    /// <summary>Q248 migration.logs：只走 43 这一步的 42 档不预告、不在右栏说明；更老的档维持通用提示，当前版本不算升级。备份另走 NeedsMigration，仍然要做。</summary>
    [TestCase("42", true, false)]
    [TestCase("41", true, true)]
    [TestCase("3", true, true)]
    [TestCase("0.2-proto-1", true, true)]
    [TestCase(GameState.CurrentSchema, false, false)]
    public void SAVE_Q248_01_UpgradeNotice_42Silent_OlderKept(string schema, bool backup, bool announce)
    {
        var j = JObject.Parse(SaveSerializer.Serialize(New()));
        j["SchemaVersion"] = schema;
        var json = j.ToString();
        Assert.That(SaveSerializer.NeedsMigration(json), Is.EqualTo(backup));
        Assert.That(SaveSerializer.AnnouncesUpgrade(json), Is.EqualTo(announce));
    }
}
