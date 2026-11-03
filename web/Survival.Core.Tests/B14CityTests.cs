using Newtonsoft.Json.Linq;
using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 批次 B14（R44 势力事实、营区登记与旧势力迁移；R45 城市 16 名岗位 NPC 与服务框架；并入 Q32/Q33 选 B）。
/// 规则来源：N01「3 归属 接触与合作分别存档」「6 旧规则替代与数据迁移」、A11「城市建筑、岗位NPC与八支线」§1—§6、A11「Q33 城市岗位完整服务表」、
/// S02 门槛规则 3/5/6、A07 expedition_basics；数据 city-npc-data.json、city-services.json、narrative-texts.json。
/// 验收：FN02/FN06/FN07、CITY-N02/N03/N05/N06、COMP06、CS01—CS08（本批能做的部分）、TXT01/TXT08/TXT10。
/// </summary>
public class B14CityTests : TestBase
{
    private static JObject Design(string name) => JObject.Parse(File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, name)));

    private GameState At(string loc)
    {
        var s = New();
        E.DebugMapTeleport(s, loc);
        return s;
    }

    private void Travel(GameState s, string to)
    {
        Start(s, ActionRequest.Travel(to));
        Run(s);
        ReadDialogues(s);   // B15：已登记到广场时播一次“营区之外”公开说明
        Assert.That(s.CurrentLocation, Is.EqualTo(to));
    }

    private void Ok(ActionResult r) => Assert.That(r.Ok, Is.True, r.Reason);

    // ───────────── 数据（DesignSync） ─────────────

    /// <summary>CS01/COMP06/TXT01/TXT10：16 岗位与 19 项服务逐条对照策划数据；九种状态对白都有真实文字；服务只引用已定义 serviceId。</summary>
    [Category("DesignSync")]
    [Test]
    public void CS01_NpcAndServiceDataFromDesign()
    {
        var npcs = (JArray)Design("design.city-npc.json")["npcs"]!;
        Assert.That(Cfg.City.Npcs, Has.Count.EqualTo(16));
        foreach (var n in npcs)
        {
            var c = Cfg.City.Npcs.Single(x => x.NpcId == (string)n["npcId"]!);
            Assert.That(c.Name, Is.EqualTo((string)n["displayName"]!));
            Assert.That(c.Role, Is.EqualTo((string)n["role"]!));
            Assert.That(c.FactionId, Is.EqualTo((string)n["factionId"]!));
            Assert.That(c.LocationId, Is.EqualTo((string)n["locationId"]!));
            Assert.That(c.ServiceIds, Is.EqualTo(n["serviceIds"]!.Select(x => (string)x!)));
            Assert.That(c.QuestIds, Is.EqualTo(n["questIds"]!.Select(x => (string)x!)));
            Assert.That(c.CanRecruit, Is.False, "岗位不可招募（COMP06）");
            Assert.That(c.States.Keys, Is.EquivalentTo(new[] { "first_greeting", "role", "quest_offer", "quest_return", "idle", "locked", "service_end", "institution_substitute", "service_offer" }));
            foreach (var set in c.States.Values)
                Assert.That(Cfg.Story.Dialogues.Any(d => d.Event == set && d.Text.Length > 0), Is.True, $"TXT01：{set} 有文字");
        }
        Assert.That(Cfg.City.Npcs.Select(n => n.LocationId).Distinct().Count(), Is.EqualTo(16), "16 个有岗位的建筑（科学院外院 r02 只是父城区域）");

        var svcs = (JArray)Design("design.city-services.json")["services"]!;
        Assert.That(Cfg.City.Services, Has.Count.EqualTo(19));
        foreach (var x in svcs)
        {
            var c = Cfg.City.Services.Single(y => y.Id == (string)x["id"]!);
            Assert.That(c.LocationId, Is.EqualTo((string)x["locationId"]!));
            Assert.That(c.Kind, Is.EqualTo((string)x["kind"]!));
            Assert.That(c.WorldMinutes, Is.EqualTo(x["worldMinutes"]!.Type == JTokenType.Null ? null : (int?)x["worldMinutes"]));
            Assert.That(c.Inputs, Is.EquivalentTo(((JObject)x["inputs"]!).Properties().ToDictionary(p => p.Name, p => (int)p.Value)));
            Assert.That(c.DailyPartyQuota, Is.EqualTo((int?)x["dailyPartyQuota"]));
        }
        var allBound = Cfg.City.Npcs.SelectMany(n => n.ServiceIds).ToList();
        Assert.That(allBound, Is.EquivalentTo(Cfg.City.Services.Select(x => x.Id)), "每项服务恰好绑一名岗位（CS01）");
        var relief = Cfg.City.Services.Single(x => x.Id == "church_relief_ration");
        Assert.That(relief.Choices.Select(c => (c.Stat, c.Below, c.Outputs.Single().Key)), Is.EqualTo(new[] { ("satiety", 40.0, "cooked_root"), ("hydration", 40.0, "clean_water") }));
    }

    // ───────────── R44：事实与迁移 ─────────────

    /// <summary>FN02/S02 规则 3：营门 0 分钟登记只写授权（开 h01↔h02），真实走到广场才 hub_registered/C2/登记知识与 QMAP01 完成；不写 joined_faction，再到广场不重复。</summary>
    [Test]
    public void FN02_Registration_AuthorizeThenArriveSquare()
    {
        var s = At("h01_gate");
        Assert.That(E.BattleCapacity(s), Is.EqualTo(1));
        var card = E.NpcsHere(s).Single();
        Assert.That(card.Name, Is.EqualTo("陆衡"));
        var v = E.CityServicePreview(s, "hub_registration_existing")!;
        Assert.That(v.Blocked, Is.Null);
        var minute = s.GameMinute;
        Ok(E.StartCityService(s, "hub_registration_existing"));
        Assert.That(s.GameMinute, Is.EqualTo(minute), "0 分钟");
        Assert.That(s.Progress.Flags, Does.Contain("registration_authorized"));
        Assert.That(s.Progress.Flags, Does.Not.Contain("hub_registered"), "不在 h01 预发");
        Assert.That(s.Story.DialogueLog.Any(d => d.EventId == "gate_h01_registration"), Is.True);
        ReadDialogues(s);
        Assert.That(E.CityServicePreview(s, "hub_registration_existing")!.Blocked, Does.Contain("已登记授权"));
        var know = Cfg.Knowledge.Where(k => k.GrantedByFacts.Contains("hub_registered")).ToList();
        Assert.That(know.Any(k => E.KnowsKnowledge(s, k.Id)), Is.False);

        Travel(s, "h02_square");
        Assert.That(s.Progress.Flags, Does.Contain("hub_registered"));
        Assert.That(s.Progress.Flags, Does.Contain(GameEngine.QuestFact("QMAP01", "completed")));
        Assert.That(E.BattleCapacity(s), Is.EqualTo(2), "C2");
        Assert.That(know.All(k => E.KnowsKnowledge(s, k.Id)), Is.True, "Q108 登记默认知识");
        Assert.That(s.Progress.Flags.Any(GameEngine.IsMembershipFact), Is.False, "FN02：永不写 joined_faction");
        var items = s.Items.Sum(i => i.Count);
        Travel(s, "h01_gate");
        Travel(s, "h02_square");
        Assert.That(s.Items.Sum(i => i.Count), Is.EqualTo(items), "再到广场不重复登记、不发物");
        Assert.That(E.CityServicePreview(s, "hub_registration_existing")!.Blocked, Does.Contain("须实际到达"));
    }

    /// <summary>不先在营门登记，直接被放到广场（开发传送）不算登记；其余 register 边仍锁。</summary>
    [Test]
    public void FN02_SquareWithoutAuthorization_NotRegistered()
    {
        var s = At("h02_square");
        Assert.That(s.Progress.Flags, Does.Not.Contain("hub_registered"));
        Assert.That(E.BattleCapacity(s), Is.EqualTo(1));
    }

    /// <summary>FN06：旧档 25→26：faction_works/faction_beacon 原样保留并另记 legacy_service；joined_faction 改记 legacy_service；到访过 c02/r02 补接触；已登记补授权；幂等、不发物。</summary>
    [Test]
    public void FN06_MigrateLegacyFactionFacts()
    {
        var s = New();
        s.Progress.Flags.UnionWith(new[] { "faction_works", "faction_beacon", "joined_faction:faction_church", "hub_registered" });
        s.Locations["c02_church"].Visited = true;
        s.City = null!;
        s.SchemaVersion = SaveMigrator.Schema25;
        var items = s.Items.Sum(i => i.Count);
        var cap = E.BattleCapacity(s);
        SaveMigrator.Migrate(s, Cfg);
        Assert.That(s.SchemaVersion, Is.EqualTo(GameState.CurrentSchema));
        Assert.That(s.City, Is.Not.Null);
        Assert.That(s.Progress.Flags, Is.SupersetOf(new[] { "faction_works", "faction_beacon", "legacy_service:faction_works", "legacy_service:faction_beacon",
            "legacy_service:joined_faction:faction_church", "church_contacted", "registration_authorized" }));
        Assert.That(s.Progress.Flags.Any(GameEngine.IsMembershipFact), Is.False);
        Assert.That(s.Progress.Flags, Does.Not.Contain("academy_contacted"), "没到过 r02 不补");
        Assert.That(s.Items.Sum(i => i.Count), Is.EqualTo(items));
        Assert.That(E.BattleCapacity(s), Is.EqualTo(cap));
        Assert.That(s.Story.DialogueLog.Any(d => d.EventId.StartsWith("npc_")), Is.False, "不伪造新组织对白");
        var json = SaveSerializer.Serialize(s);
        SaveMigrator.Migrate(s, Cfg);
        Assert.That(SaveSerializer.Serialize(s), Is.EqualTo(json), "幂等");
    }

    /// <summary>
    /// 试玩修复 M306 A1：真实存档 25 的 JSON 走 SaveSerializer 完整读档路径（不只调 Migrator）升到当前版本；
    /// 原 IsMigratable 只列到 24，存档 25 被判“不兼容”。
    /// </summary>
    [Test]
    public void A1_Save25_FullLoadPath_UpgradesTo26()
    {
        var s = At("c02_church");
        s.Progress.Flags.Remove("church_contacted");
        var j = JObject.Parse(SaveSerializer.Serialize(s));
        j["SchemaVersion"] = SaveMigrator.Schema25;
        j.Remove("City");
        var json = j.ToString();
        Assert.That(SaveSerializer.NeedsMigration(json), Is.True);
        var m = SaveSerializer.Deserialize(json, Cfg, out var notes);
        Assert.That(m.SchemaVersion, Is.EqualTo(GameState.CurrentSchema));
        Assert.That(m.City, Is.Not.Null);
        Assert.That(m.Progress.Flags, Does.Contain("church_contacted"), "到访过 c02 的旧档补接触事实");
        Assert.That(m.Log.Count(l => l.Text.Contains($"版本{GameState.CurrentSchema}")), Is.EqualTo(1));
        Assert.That(SaveSerializer.NeedsMigration(SaveSerializer.Serialize(m)), Is.False);
    }

    /// <summary>
    /// 防回归（M306 A1）：SaveMigrator 里每个 SchemaN 常量与 3..当前−1 的每个数字版本都必须被读档入口接受为“可升级”；
    /// 当前版本不算可升级，未来版本与乱码不接受；SaveMigrator 必须有“当前−1”的常量（升 schema 时漏加迁移步会在这里报）。
    /// </summary>
    [Test]
    public void A1_EveryOlderSchemaIsMigratable()
    {
        var current = int.Parse(GameState.CurrentSchema);
        var consts = typeof(SaveMigrator).GetFields().Where(f => f.IsLiteral && f.Name.StartsWith("Schema")).Select(f => (string)f.GetRawConstantValue()!).ToList();
        Assert.That(consts, Does.Contain((current - 1).ToString()), "SaveMigrator 要有上一个版本的常量与迁移步");
        foreach (var v in consts) Assert.That(SaveSerializer.IsMigratable(v), Is.True, v);
        for (var v = 3; v < current; v++) Assert.That(SaveSerializer.IsMigratable(v.ToString()), Is.True, v.ToString());
        Assert.That(SaveSerializer.IsMigratable(SaveMigrator.LegacySchema), Is.True);
        Assert.That(SaveSerializer.IsMigratable(GameState.CurrentSchema), Is.False);
        Assert.That(SaveSerializer.IsMigratable((current + 1).ToString()), Is.False);
        Assert.That(SaveSerializer.IsMigratable("2"), Is.False);
        Assert.That(SaveSerializer.IsMigratable(null), Is.False);
        Assert.That(SaveSerializer.IsMigratable("x25"), Is.False);
    }

    /// <summary>试玩修复 M306：换药冷却中，目标只写一句原因（不再“预计/冷却”与“（绷带冷却中…（病毒…））”重复嵌套）。</summary>
    [Test]
    public void M306_DressingCooldownReason_OneSentence()
    {
        var s = At("h04_infirmary");
        SetStats(s, 12, 80, 80, 15, 80, 70);
        s.BandageReadyAt = s.GameMinute + 60;
        var t = E.CityServicePreview(s, "clinic_dressing")!.Targets.Single();
        Assert.That(t.Blocked, Does.StartWith("现在换药没有效果：回血冷却还需"));
        Assert.That(t.Detail, Is.Empty);
        Assert.That(t.Blocked, Does.Not.Contain("（"));
    }

    /// <summary>旧档整条链：存档 25 往返序列化后升级，城市状态可用。</summary>
    [Test]
    public void FN06_SaveRoundTrip()
    {
        var s = At("h04_infirmary");
        E.TalkNpc(s, "npc_chen_zhi");
        ReadDialogues(s);
        var back = SaveSerializer.Deserialize(SaveSerializer.Serialize(s));
        Assert.That(back.City.Greeted, Does.Contain("npc_chen_zhi"));
    }

    // ───────────── R45：岗位 NPC ─────────────

    /// <summary>CITY-N02：人物卡只在实际到达的当前地点出现；看地图/不在当地不生成；设施栏原有内容不消失。</summary>
    [Test]
    public void CITY_N02_CardsOnlyWhereActuallyArrived()
    {
        var s = New();
        Assert.That(E.NpcsHere(s), Is.Empty);
        s.CurrentLocation = "h04_infirmary";   // 没有实际到达
        Assert.That(E.NpcsHere(s), Is.Empty, "未到访不生成");
        var t = At("h04_infirmary");
        var c = E.NpcsHere(t).Single();
        Assert.That(c.Name, Is.EqualTo("陈芷"));
        Assert.That(c.FactionName, Is.EqualTo("幸存者营区"));
        Assert.That(c.ServiceCount, Is.EqualTo(4), "B23 起另有查抄公开资料（物资登记资料）");
        E.DebugMapTeleport(t, "c04_alms");
        Assert.That(E.NpcsHere(t).Select(x => x.Name), Is.EqualTo(new[] { "白苓" }));
        E.DebugMapTeleport(t, "r02_academy");
        Assert.That(E.NpcsHere(t), Is.Empty, "科学院外院是父城区域，没有岗位");
        Assert.That(E.CityServicesOf(t, "npc_chen_zhi").All(v => v.Blocked != null && v.Blocked.Contains("须实际到达")), Is.True, "不能远程办理");
    }

    /// <summary>CITY-N03/COMP06：岗位卡标“岗位”、不可招募，交谈不进名册；六名固定队友只加营区职能标签。</summary>
    [Test]
    public void CITY_N03_PostKindAndCompanionLabels()
    {
        var s = At("h06_shelter");
        var c = E.NpcsHere(s).Single();
        Assert.That((c.Kind, c.KindLabel, c.RecruitText), Is.EqualTo(("post", "岗位", "岗位人物，不可招募")));
        Ok(E.TalkNpc(s, c.NpcId));
        ReadDialogues(s);
        Assert.That(s.Roster.Any(r => r.ActorId == c.NpcId), Is.False);
        Assert.That(s.Party, Is.EqualTo(new[] { GameState.PlayerId }));
        var aff = Cfg.Party.NamedCompanions.ToDictionary(n => n.ActorId, n => n.Affiliation);
        Assert.That(aff["npc_shen_he"], Is.EqualTo("营区医疗转运人员"));
        Assert.That(aff["npc_ye_xun"], Is.EqualTo("独立通信记录者"));
        Assert.That(aff.Values.All(v => v.Length > 0), Is.True);
    }

    /// <summary>
    /// FN07/CITY-N06：首次交谈播初次见面+岗位说明，之后播闲谈；交谈 0 分钟、不改身体/物品/事实，回看不重奖。
    /// B14b（Q146 选 B）：名下事项全未实装的岗位不再因此播“暂不可办”，也没有事项。
    /// </summary>
    [Test]
    public void FN07_TalkStates_NoEffects()
    {
        var s = At("h04_infirmary");
        var before = (s.GameMinute, s.Items.Sum(i => i.Count), s.Progress.Flags.Count, s.Stats.Health, s.Body.VirusLoad);
        Ok(E.TalkNpc(s, "npc_chen_zhi"));
        Assert.That(s.Story.DialogueLog.Select(d => d.EventId), Does.Contain("npc_chen_zhi_dialogue_first_greeting").And.Contain("npc_chen_zhi_dialogue_role"));
        ReadDialogues(s);
        Ok(E.TalkNpc(s, "npc_chen_zhi"));
        ReadDialogues(s);
        Ok(E.TalkNpc(s, "npc_chen_zhi"));
        ReadDialogues(s);
        Assert.That(s.Story.DialogueLog.Count(d => d.EventId.StartsWith("npc_chen_zhi_dialogue_idle")), Is.EqualTo(2), "闲谈可重复回放");
        Assert.That((s.GameMinute, s.Items.Sum(i => i.Count), s.Progress.Flags.Count, s.Stats.Health, s.Body.VirusLoad), Is.EqualTo(before));
        Assert.That(s.City.Talks["npc_chen_zhi"], Is.EqualTo(3));

        E.DebugMapTeleport(s, "h02_square");   // 许安：没有可接的委托 → 闲谈，不播“暂不可办”（Q146）
        Ok(E.TalkNpc(s, "npc_xu_an"));
        ReadDialogues(s);
        Ok(E.TalkNpc(s, "npc_xu_an"));
        ReadDialogues(s);
        Assert.That(s.Story.DialogueLog.Any(d => d.EventId.StartsWith("npc_xu_an_dialogue_locked")), Is.False);
        Assert.That(s.Story.DialogueLog.Any(d => d.EventId.StartsWith("npc_xu_an_dialogue_idle")), Is.True);
        // B15：整理调查记录已实装，两份记录没齐时照常列出并写真实原因
        Assert.That(E.CityServicesOf(s, "npc_xu_an").Single(v => v.Id == "hub_affairs_record").Blocked, Does.StartWith("需要："));
    }

    /// <summary>CITY-N05/TXT08：命名岗位退场由机构当班接续员代班，不复活原人物；服务照办、日配额与已领账沿用。</summary>
    [Test]
    public void CITY_N05_InstitutionSubstitute_SameQuota()
    {
        var s = At("c04_alms");
        SetStats(s, 15, 30, 80, 15, 80, 70);
        Ok(E.StartCityService(s, "church_relief_ration", GameState.PlayerId, "relief_food"));
        Run(s);
        ReadDialogues(s);
        E.DebugToggleCityPostAbsent(s);
        var c = E.NpcsHere(s).Single();
        Assert.That((c.Name, c.KindLabel, c.Substitute), Is.EqualTo(("机构当班接续员", "代班", true)));
        Ok(E.TalkNpc(s, "npc_bai_ling"));
        var d = s.Story.DialogueLog.Last(x => x.EventId.StartsWith("npc_bai_ling_dialogue_institution_substitute"));
        ReadDialogues(s);
        Assert.That(d.Lines.First().Speaker, Is.EqualTo("机构当班接续员"));
        Assert.That(E.CityServicePreview(s, "church_relief_ration")!.Blocked, Does.Contain("额度已用完"), "代班不重置当日配额");
    }

    // ───────────── 公开告示 ─────────────

    /// <summary>A07/S08 X4：营区登记后在 h03 看制作告示 0 分钟学会 expedition_basics，一次，不发物、不给经验。</summary>
    [Test]
    public void H03Notice_ExpeditionBasics_Once()
    {
        var s = At("h03_workshop");
        var doc = E.DocumentsHere(s).Single(d => d.Id == "h03_craft_notice");
        Assert.That(doc.ReadBlocked, Does.Contain("营区登记"));
        Assert.That(E.ReadDocument(s, "h03_craft_notice").Ok, Is.False);
        s.Progress.Flags.Add("hub_registered");
        var before = (s.GameMinute, s.Items.Sum(i => i.Count), s.Skills.Sum(k => k.Value.Xp));
        Ok(E.ReadDocument(s, "h03_craft_notice"));
        Assert.That(E.KnowsKnowledge(s, "expedition_basics"), Is.True);
        Ok(E.ReadDocument(s, "h03_craft_notice"));
        Assert.That((s.GameMinute, s.Items.Sum(i => i.Count), s.Skills.Sum(k => k.Value.Xp)), Is.EqualTo(before));
        Assert.That(E.DocumentsHere(s).Single(d => d.Id == "h03_craft_notice").Status, Does.StartWith("已学会"));
    }

    /// <summary>S02 规则 6：r07 门旁告示两个选项（只看记录/拒绝试验）同写 academy_observation_pass 并开通往 r08 的观察通道；不改身体。</summary>
    [TestCase("academy_pass_read_only")]
    [TestCase("academy_pass_decline_experiment")]
    public void R07Notice_EitherChoicePasses(string choice)
    {
        var s = At("r07_workshop");
        var edge = Cfg.Edges.Single(e => e.Gate == "supervised_no_experiment_required");
        Assert.That(E.EvaluateGate(s, edge), Is.False);
        var doc = E.DocumentsHere(s).Single(d => d.Id == "r07_observation_notice");
        Assert.That(doc.Choices.Select(c => c.Id), Is.EquivalentTo(new[] { "academy_pass_read_only", "academy_pass_decline_experiment" }));
        Assert.That(E.ReadDocument(s, "r07_observation_notice").Ok, Is.False, "要先选范围");
        var body = (s.Stats.Health, s.Body.Immunity, s.Body.VirusLoad);
        Ok(E.ReadDocument(s, "r07_observation_notice", choice));
        ReadDialogues(s);
        Assert.That(s.Progress.Flags, Does.Contain("academy_observation_pass"));
        Assert.That(E.EvaluateGate(s, edge), Is.True);
        Assert.That((s.Stats.Health, s.Body.Immunity, s.Body.VirusLoad), Is.EqualTo(body));
        Assert.That(s.Progress.Flags.Any(GameEngine.IsMembershipFact), Is.False);
    }

    // ───────────── 服务（Q33） ─────────────

    /// <summary>CS02/CS03：换药 10 分钟，完成时扣自带绷带1+布1，按原绷带函数（医疗按1级、无装备加值）回血并启动目标冷却，主角不得经验；无收益开始前拒绝；冷却不绕过。</summary>
    [Test]
    public void CS03_Dressing_UsesBandageFunction_Cooldown()
    {
        var s = At("h04_infirmary");
        SetStats(s, 12, 80, 80, 15, 80, 70);
        E.DebugGive(s, "bandage", 2);
        E.DebugGive(s, "cloth", 1);
        var (b0, c0) = (Count(s, "bandage"), Count(s, "cloth"));
        var xp = s.Skills.Sum(k => k.Value.Xp);
        var v = E.CityServicePreview(s, "clinic_dressing")!;
        Assert.That(v.Blocked, Is.Null);
        Assert.That(v.Targets.Single().Blocked, Is.Null);
        var start = s.GameMinute;
        Ok(E.StartCityService(s, "clinic_dressing", GameState.PlayerId));
        Assert.That(s.Main!.PlannedMinutes, Is.EqualTo(10));
        Run(s);
        Assert.That(s.GameMinute - start, Is.EqualTo(10));
        Assert.That(s.Stats.Health, Is.GreaterThan(12));
        Assert.That((Count(s, "bandage"), Count(s, "cloth")), Is.EqualTo((b0 - 1, c0 - 1)), "完成时扣绷带1、布1");
        Assert.That(s.BandageReadyAt, Is.GreaterThan(s.GameMinute), "目标回血冷却启动");
        Assert.That(s.Skills.Sum(k => k.Value.Xp), Is.EqualTo(xp), "主角 0 经验");
        ReadDialogues(s);
        Assert.That(s.Story.DialogueLog.Any(d => d.EventId.StartsWith("npc_chen_zhi_dialogue_service_end")), Is.True);
        // 冷却中、未出血、未感染：没有实际收益 → 开始前拒绝（材料不足也会拦，这里先补布）
        E.DebugGive(s, "cloth", 1);
        var t = E.CityServicePreview(s, "clinic_dressing")!.Targets.Single();
        Assert.That(t.Blocked, Is.Not.Null);
        Assert.That(E.StartCityService(s, "clinic_dressing", GameState.PlayerId).Ok, Is.False);
    }

    /// <summary>CS03：换药目标可以是同行者，冷却记在目标身上；中途取消不扣材料、无效果（CS08）。</summary>
    [Test]
    public void CS03_Dressing_CompanionTarget_CancelRefund()
    {
        var s = At("h04_infirmary");
        s.Progress.Flags.Add("hub_registered");
        E.DebugRecruit(s, "npc_lin_zhao");
        Assume.That(s.Party, Does.Contain("npc_lin_zhao"));
        var lin = s.Actors["npc_lin_zhao"];
        lin.Stats.Health = 10;
        E.DebugGive(s, "bandage", 1);
        E.DebugGive(s, "cloth", 1);
        var mats = Count(s, "bandage") + Count(s, "cloth");
        var carried = Count(s, "bandage", ItemInstance.Carried) + Count(s, "cloth", ItemInstance.Carried);
        Ok(E.StartCityService(s, "clinic_dressing", "npc_lin_zhao"));
        E.Advance(s);
        Ok(E.Cancel(s));
        Assert.That(Count(s, "bandage") + Count(s, "cloth"), Is.EqualTo(mats), "取消不扣材料");
        Assert.That(Count(s, "bandage", ItemInstance.Carried) + Count(s, "cloth", ItemInstance.Carried), Is.EqualTo(carried), "原物原处，不搬到地面");
        Assert.That(s.Items.Any(i => i.ReservedBy != null), Is.False, "预留已解除");
        Assert.That(lin.Stats.Health, Is.EqualTo(10));
        Assert.That(E.ActiveCityTxn(s), Is.Null);
        Assert.That(s.City.Pending, Is.Null);
        Ok(E.StartCityService(s, "clinic_dressing", "npc_lin_zhao"));
        Run(s);
        Assert.That(lin.Stats.Health, Is.GreaterThan(10));
        Assert.That(lin.BandageReadyAt, Is.GreaterThan(s.GameMinute));
        Assert.That(s.Player.BandageReadyAt, Is.LessThanOrEqualTo(s.GameMinute), "主角冷却不动");
    }

    /// <summary>CS04/CS05：施济 5 分钟，只能领低于 40 的一项，产出落当地地面不自动吃；全队每世界日 1 份（跨项共享），取消不扣额，次日再领。</summary>
    [Test]
    public void CS04_Relief_DailyQuota()
    {
        var s = At("c04_alms");
        SetStats(s, 15, 80, 80, 15, 80, 70);
        var v = E.CityServicePreview(s, "church_relief_ration")!;
        Assert.That(v.Targets.Single().Blocked, Does.Contain("不低于40"));
        SetStats(s, 15, 30, 30, 15, 80, 70);
        Assert.That(E.StartCityService(s, "church_relief_ration", GameState.PlayerId, null).Ok, Is.False, "只选一项");
        Ok(E.StartCityService(s, "church_relief_ration", GameState.PlayerId, "relief_water"));
        E.Advance(s);
        Assert.That(s.Main, Is.Null, "5 分钟一段");
        Assert.That(Count(s, "clean_water", "c04_alms"), Is.EqualTo(1));
        Assert.That(s.Stats.Hydration, Is.LessThan(31), "不自动喝");
        ReadDialogues(s);
        Assert.That(E.CityServicePreview(s, "church_relief_ration")!.Blocked, Does.Contain("额度已用完"));
        Assert.That(E.StartCityService(s, "church_relief_ration", GameState.PlayerId, "relief_food").Ok, Is.False, "全队同机构每日合计 1 份");
        SetStats(s, 20, 95, 95, 20, 95, 70);
        AdvanceTo(s, At(Clock.Day(s.GameMinute) + 1, 8, 0));
        SetStats(s, 15, 30, 80, 15, 80, 70);
        Assert.That(E.StartCityService(s, "church_relief_ration", GameState.PlayerId, "relief_water").Ok, Is.False, "水分不低于40不可领水");
        Ok(E.StartCityService(s, "church_relief_ration", GameState.PlayerId, "relief_food"));
        Ok(E.Cancel(s));
        Assert.That(E.CityQuotaUsed(s, "church_relief_ration", Clock.Day(s.GameMinute)), Is.EqualTo(0), "取消不扣额");
    }

    /// <summary>CS07：观察休息/安静休息 30 分钟按普通有遮蔽 rest 逐段推进（全体同行），可停止；无额外效果。</summary>
    [TestCase("h04_infirmary", "clinic_observation_rest")]
    [TestCase("c07_sanctuary", "church_quiet_rest")]
    public void CS07_Rest_Services(string loc, string svc)
    {
        var s = At(loc);
        SetStats(s, 15, 80, 80, 8, 80, 70);
        var start = s.GameMinute;
        Ok(E.StartCityService(s, svc));
        Assert.That(s.Main!.Type, Is.EqualTo(ActionType.Rest));
        Assert.That(s.Main.PlannedMinutes, Is.EqualTo(30));
        Run(s);
        Assert.That(s.GameMinute - start, Is.EqualTo(30));
        Assert.That(s.Stats.Energy, Is.GreaterThan(8));
        Assert.That(s.Items.Count(i => i.Owner == loc), Is.EqualTo(0), "不发物");
    }

    /// <summary>CS06/CS01：咨询 0 分钟只读；未实装的服务只注册（B14b 起从事项里隐藏，直接请求中性拒绝）；收容所沿用原入口。</summary>
    [Test]
    public void CS06_InfoServices_AndPendingEntries()
    {
        var s = At("h04_infirmary");
        var before = (s.GameMinute, s.Stats.Health, s.Body.VirusLoad);
        Ok(E.StartCityService(s, "clinic_consult", GameState.PlayerId));
        Assert.That((s.GameMinute, s.Stats.Health, s.Body.VirusLoad), Is.EqualTo(before));
        Assert.That(s.Log.Last().Text, Does.Contain("伤病咨询"));
        // B15 起主线与城市支线的任务类服务已实装（见 B15QuestTests），B18 起鉴定已实装（见 B18AppraisalTests），B23 起查抄公开资料已实装（见 B23ExqCopyTests）
        Assert.That(Cfg.City.Services.Where(x => !x.Implemented), Is.Empty);
        E.DebugMapTeleport(s, "h06_shelter");
        Assert.That(E.CityServicePreview(s, "shelter_recruitment")!.Blocked, Is.Null);
    }
}
