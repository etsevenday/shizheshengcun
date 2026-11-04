using Newtonsoft.Json.Linq;
using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 批次 B26a（R22 Q235、R26 Q236）。规则来源：策划合同 exq-dialogue-flow-adjudications.json、substitute-talk-adjudications.json；
/// 验收 DIALOGUE-Q235-01—04、DIALOGUE-Q236-01—04。Q237、Q238 验收 DIALOGUE-Q237-01—03、DIALOGUE-Q238-01—03 在文件末尾。
/// </summary>
public class B26aSubstituteTalkTests : TestBase
{
    private static readonly string[] Dedicated = { "EXQ05", "EXQ06", "EXQ07", "EXQ08" };

    private static readonly JObject Q236 = JObject.Parse(File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "design.substitute-talk.json")));

    private static string Shared(string state) => (string)Q236["sharedLines"]![state]!;

    public static IEnumerable<string> Posts => ((JArray)Q236["idleLines"]!).Select(x => (string)x["npcId"]!);

    private GameState Registered(string loc)
    {
        var s = New();
        s.Progress.Flags.UnionWith(new[] { "registration_authorized", "hub_registered" });
        s.GameMinute = At(2, 9, 0);
        s.SpawnNextCheck = int.MaxValue;
        s.PatrolNextCheck = int.MaxValue;
        E.DebugMapTeleport(s, loc);
        ReadDialogues(s);
        return s;
    }

    private static string Set(string npc, string state) => Cfg.City.Npcs.Single(n => n.NpcId == npc).States[state];

    private static string Base(DialogueSession d) => d.EventId.Split('#')[0];

    /// <summary>某对白组在对话记录里出现的次数（含 #序号的重播）。</summary>
    private static int Plays(GameState s, string set) => s.Story.DialogueLog.Count(d => Base(d) == set);

    private static int Plays(GameState s, string npc, string state) => Plays(s, Set(npc, state));

    /// <summary>这次之后新增的对话段（按发生顺序的组 ID）。</summary>
    private static List<string> Since(GameState s, int mark) => s.Story.DialogueLog.Skip(mark).Select(Base).ToList();

    private static void GiveClaim(GameState s, string node, string fact) => s.Progress.Flags.UnionWith(new[] { "claim:" + node, "campaignId:" + node, fact });

    private void Talk(GameState s, string npc)
    {
        var r = E.TalkNpc(s, npc);
        Assert.That(r.Ok, Is.True, r.Reason);
    }

    private void TalkRead(GameState s, string npc)
    {
        Talk(s, npc);
        ReadDialogues(s);
    }

    /// <summary>可比的账：物资、经验、容量、时间、身体、配额。</summary>
    private (int, int, int, int, double, int) Ledger(GameState s) =>
        (s.Items.Sum(i => i.Count), (int)s.Skills.Sum(kv => kv.Value.Xp), E.BattleCapacity(s), s.GameMinute, s.PartyActors.Sum(a => a.Stats.Health + a.Stats.Satiety + a.Stats.Hydration + a.Stats.Energy), s.City.QuotaUsed.Values.Sum());

    // ───────────── DIALOGUE-Q235-01：EXQ 接取跳过通用铺垫 ─────────────

    /// <summary>配置里走专属接取的正好是 EXQ05—08（引擎按 kind=optional 判断，多出新任务要在这里报）。</summary>
    [Test]
    public void Q235_01_DedicatedOffersAreExactlyExq05To08() =>
        Assert.That(Cfg.Quests.List.Where(q => q.Kind == "optional").Select(q => q.Id), Is.EquivalentTo(Dedicated));

    /// <summary>首次接 EXQ：只播本项专属接取，不播 first_greeting/role；首次铺垫记为已处理，下一次非 EXQ 交谈不补播，也不误接别的任务。</summary>
    [TestCase("h02_square", "npc_xu_an", "EXQ07")]
    [TestCase("h04_infirmary", "npc_chen_zhi", "EXQ08")]
    public void Q235_01_FirstTalkExq_SkipsGenericLead_NoBackfill(string loc, string npc, string quest)
    {
        var s = Registered(loc);
        var mark = s.Story.DialogueLog.Count;
        TalkRead(s, npc);
        Assert.That(Since(s, mark), Is.EqualTo(new[] { quest + "_offer" }));
        Assert.That(s.City.Greeted, Does.Contain(npc));
        Assert.That(GameEngine.QuestAccepted(s, quest), Is.True);

        var flags = s.Progress.Flags.Count;
        TalkRead(s, npc);
        Assert.That(Plays(s, npc, "first_greeting") + Plays(s, npc, "role"), Is.Zero, "跳过的铺垫不补播");
        Assert.That(Plays(s, npc, "quest_return"), Is.EqualTo(1), "已接未完成照原规则播委托回报");
        Assert.That(s.Progress.Flags.Count, Is.EqualTo(flags), "不误接其他任务");
    }

    /// <summary>事项里的“接取（0分钟，与交谈同效）”走同一规则：首次铺垫记为已处理，之后交谈不补播。</summary>
    [Test]
    public void Q235_01_AcceptButton_SameAsTalk()
    {
        var s = Registered("h02_square");
        Assert.That(E.AcceptQuest(s, "EXQ07").Ok, Is.True);
        ReadDialogues(s);
        Assert.That(s.City.Greeted, Does.Contain("npc_xu_an"));
        TalkRead(s, "npc_xu_an");
        Assert.That(Plays(s, "npc_xu_an", "first_greeting") + Plays(s, "npc_xu_an", "role"), Is.Zero);
    }

    /// <summary>按本次实际将接取的任务判断，不按“该 NPC 有 EXQ”：罗铮这次实际接 FA05（配置靠前）、或没登记接不了 EXQ 时，通用铺垫照旧。</summary>
    [Test]
    public void Q235_01_JudgedByActualOffer_NotByNpc()
    {
        var s = Registered("h03_workshop");
        s.Progress.Flags.UnionWith(new[] { "quest:FA04:completed", "communication_power_claimed" });
        TalkRead(s, "npc_luo_zheng");
        Assert.That(GameEngine.QuestAccepted(s, "FA05"), Is.True);
        Assert.That(GameEngine.QuestAccepted(s, "EXQ05"), Is.False);
        Assert.That((Plays(s, "npc_luo_zheng", "first_greeting"), Plays(s, "npc_luo_zheng", "role")), Is.EqualTo((1, 1)));

        var u = New();
        u.GameMinute = At(2, 9, 0);
        u.SpawnNextCheck = int.MaxValue;
        u.PatrolNextCheck = int.MaxValue;
        E.DebugMapTeleport(u, "h03_workshop");
        ReadDialogues(u);
        TalkRead(u, "npc_luo_zheng");
        Assert.That(GameEngine.QuestAccepted(u, "EXQ05"), Is.False);
        Assert.That((Plays(u, "npc_luo_zheng", "first_greeting"), Plays(u, "npc_luo_zheng", "role")), Is.EqualTo((1, 1)));
    }

    /// <summary>非 EXQ 首次交谈与非 EXQ 任务继续原规则：铺垫 → 接取句。</summary>
    [Test]
    public void Q235_01_OtherQuests_KeepGenericLead()
    {
        var s = Registered("h05_appraisal");
        var mark = s.Story.DialogueLog.Count;
        TalkRead(s, "npc_tao_cen");
        Assert.That(Since(s, mark), Is.EqualTo(new[] { Set("npc_tao_cen", "first_greeting"), Set("npc_tao_cen", "role"), "HU01_offer" }));
    }

    /// <summary>代班首次与 EXQ 接取同次：先说明、再专属句，不要通用铺垫；和代班员交谈不写 Greeted（Q238）。</summary>
    [TestCase(false)]
    [TestCase(true)]
    public void Q235_01_SubstituteFirst_IntroThenDedicated_NoGreeted(bool viaButton)
    {
        var s = Registered("h02_square");
        E.DebugToggleCityPostAbsent(s);
        var mark = s.Story.DialogueLog.Count;
        if (viaButton) Assert.That(E.AcceptQuest(s, "EXQ07").Ok, Is.True);
        else Talk(s, "npc_xu_an");
        ReadDialogues(s);
        Assert.That(Since(s, mark), Is.EqualTo(new[] { Set("npc_xu_an", "institution_substitute"), "EXQ07_offer" }));
        Assert.That(s.City.Greeted, Does.Not.Contain("npc_xu_an"), "Q238：代班交谈不算本人首次");
        E.DebugToggleCityPostAbsent(s);
        TalkRead(s, "npc_xu_an");
        Assert.That((Plays(s, "npc_xu_an", "first_greeting"), Plays(s, "npc_xu_an", "role")), Is.EqualTo((1, 1)), "Q238：本人回岗后播首次问候");
    }

    // ───────────── DIALOGUE-Q235-02：每名代班员只介绍一次 ─────────────

    /// <summary>交谈 → 回报 → 服务结束 → 回岗再离岗 → 读档，同一岗位只播一次；键是岗位不是显示名，另一岗位同叫“机构当班接续员”仍首次介绍。</summary>
    [Test]
    public void Q235_02_OncePerPost_AcrossEntries()
    {
        var s = Registered("h02_square");
        E.DebugToggleCityPostAbsent(s);
        TalkRead(s, "npc_xu_an");
        Assert.That(Plays(s, "npc_xu_an", "institution_substitute"), Is.EqualTo(1));
        GiveClaim(s, "d07_07_unique", "civilian_cache_knowledge");
        TalkRead(s, "npc_xu_an");
        Assert.That(GameEngine.QuestDone(s, "EXQ07"), Is.True);
        Assert.That(Plays(s, "npc_xu_an", "institution_substitute"), Is.EqualTo(1), "回报入口不重播");

        E.DebugToggleCityPostAbsent(s);
        E.DebugToggleCityPostAbsent(s);
        TalkRead(s, "npc_xu_an");
        Assert.That(Plays(s, "npc_xu_an", "institution_substitute"), Is.EqualTo(1), "回岗再离岗不重播");

        var m = SaveSerializer.Deserialize(SaveSerializer.Serialize(s), Cfg);
        TalkRead(m, "npc_xu_an");
        Assert.That(Plays(m, "npc_xu_an", "institution_substitute"), Is.EqualTo(1), "读档不重播");

        E.DebugMapTeleport(s, "h03_workshop");
        ReadDialogues(s);
        E.DebugToggleCityPostAbsent(s);
        Assert.That(E.NpcsHere(s).Single(n => n.NpcId == "npc_luo_zheng").Name, Is.EqualTo(Cfg.City.Npcs.Single(n => n.NpcId == "npc_xu_an").DutySpeaker));
        TalkRead(s, "npc_luo_zheng");
        Assert.That(Plays(s, "npc_luo_zheng", "institution_substitute"), Is.EqualTo(1), "另一岗位同显示名仍首次介绍");
    }

    /// <summary>服务结束也是自动入口：未示时播一次并记已示，之后交谈不再介绍；已示后服务结束不重播、不换别的对白（Q237 待答），结束日志与事务效果不变。</summary>
    [Test]
    public void Q235_02_ServiceEnd_IntroOnce_ThenSilent()
    {
        var s = Registered("c04_alms");
        SetLow(s);
        E.DebugToggleCityPostAbsent(s);
        Assert.That(E.StartCityService(s, "church_relief_ration", GameState.PlayerId, "relief_food").Ok, Is.True);
        Run(s);
        Assert.That(Plays(s, "npc_bai_ling", "institution_substitute"), Is.EqualTo(1));
        Assert.That(GameEngine.SubstituteIntroShown(s, "npc_bai_ling"), Is.True);
        ReadDialogues(s);
        TalkRead(s, "npc_bai_ling");
        Assert.That(Plays(s, "npc_bai_ling", "institution_substitute"), Is.EqualTo(1));

        var logs = s.Log.Count;
        var mark = s.Story.DialogueLog.Count;
        s.GameMinute = At(3, 9, 0);   // 全队每日一份额度，隔日再领
        Assert.That(E.StartCityService(s, "church_relief_ration", GameState.PlayerId, "relief_water").Ok, Is.True);
        Run(s);
        Assert.That(E.DialogueLocked(s), Is.False, "Q237：已示后服务结束不开对话");
        Assert.That(Since(s, mark), Is.Empty);
        Assert.That(Plays(s, "npc_bai_ling", "service_end"), Is.Zero, "不改播“办完事项”");
        Assert.That(s.Log.Skip(logs).Any(l => l.Text.Contains("完成")), Is.True, "结束日志照写");
        Assert.That(s.City.QuotaUsed.Values.Sum(), Is.EqualTo(2), "事务效果不因省对白跳过");
    }

    /// <summary>本人在岗的服务结束不变，仍播“办完事项”。</summary>
    [Test]
    public void Q235_02_ServiceEnd_InPerson_Unchanged()
    {
        var s = Registered("c04_alms");
        SetLow(s);
        Assert.That(E.StartCityService(s, "church_relief_ration", GameState.PlayerId, "relief_food").Ok, Is.True);
        Run(s);
        ReadDialogues(s);
        Assert.That(Plays(s, "npc_bai_ling", "service_end"), Is.EqualTo(1));
        Assert.That(Plays(s, "npc_bai_ling", "institution_substitute"), Is.Zero);
    }

    private static void SetLow(GameState s)
    {
        s.Player.Stats.Satiety = 15;
        s.Player.Stats.Hydration = 15;
    }

    /// <summary>手动回看只回放：不重置自动已示标记，不授奖励。</summary>
    [Test]
    public void Q235_02_Review_ReadOnly()
    {
        var s = Registered("h02_square");
        E.DebugToggleCityPostAbsent(s);
        TalkRead(s, "npc_xu_an");
        GiveClaim(s, "d07_07_unique", "civilian_cache_knowledge");
        TalkRead(s, "npc_xu_an");
        var snap = (Ledger(s), s.Progress.Flags.Count);
        Assert.That(E.ReviewQuest(s, "EXQ07").Ok, Is.True);
        ReadDialogues(s);
        Assert.That((Ledger(s), s.Progress.Flags.Count), Is.EqualTo(snap));
        Assert.That(GameEngine.SubstituteIntroShown(s, "npc_xu_an"), Is.True);
    }

    // ───────────── DIALOGUE-Q235-03：一次至多接一项，回报连播 ─────────────

    /// <summary>EXQ05/06 分两次接；记录齐后一次回报按原顺序连播；离岗由代班员说，不多授奖。</summary>
    [TestCase(false)]
    [TestCase(true)]
    public void Q235_03_TwoAccepts_OneChainedReturn(bool absent)
    {
        var s = Registered("h03_workshop");
        if (absent) E.DebugToggleCityPostAbsent(s);
        TalkRead(s, "npc_luo_zheng");
        Assert.That((GameEngine.QuestAccepted(s, "EXQ05"), GameEngine.QuestAccepted(s, "EXQ06")), Is.EqualTo((true, false)), "一次至多接一项");
        TalkRead(s, "npc_luo_zheng");
        Assert.That(GameEngine.QuestAccepted(s, "EXQ06"), Is.True);
        GiveClaim(s, "d05_07_unique", "waterworks_record");
        GiveClaim(s, "d06_08_unique", "mining_record");
        var mark = s.Story.DialogueLog.Count;
        var snap = Ledger(s);
        TalkRead(s, "npc_luo_zheng");
        Assert.That(Since(s, mark), Is.EqualTo(new[] { "EXQ05_turnin", "EXQ06_turnin" }));
        Assert.That(Dedicated.Take(2).All(q => GameEngine.QuestDone(s, q)), Is.True);
        Assert.That(Ledger(s), Is.EqualTo(snap), "0 分钟、不发物资经验容量");
        if (absent)
        {
            var lines = s.Story.DialogueLog.SelectMany(d => d.Lines).ToList();
            Assert.That(lines.Any(l => l.Speaker == "罗铮"), Is.False, "离岗人物不发言");
            Assert.That(Plays(s, "npc_luo_zheng", "institution_substitute"), Is.EqualTo(1));
        }
    }

    /// <summary>先取记录后接取：按原追认接取句 + 回报句；两项仍分两次；不重复完成。</summary>
    [Test]
    public void Q235_03_RecordFirst_RetroOnly()
    {
        var s = Registered("h03_workshop");
        GiveClaim(s, "d05_07_unique", "waterworks_record");
        GiveClaim(s, "d06_08_unique", "mining_record");
        var mark = s.Story.DialogueLog.Count;
        var snap = Ledger(s);
        TalkRead(s, "npc_luo_zheng");
        Assert.That(Since(s, mark), Is.EqualTo(new[] { "EXQ05_offer", "EXQ05_turnin" }));
        Assert.That((GameEngine.QuestDone(s, "EXQ05"), GameEngine.QuestAccepted(s, "EXQ06")), Is.EqualTo((true, false)));
        mark = s.Story.DialogueLog.Count;
        TalkRead(s, "npc_luo_zheng");
        Assert.That(Since(s, mark), Is.EqualTo(new[] { "EXQ06_offer", "EXQ06_turnin" }));
        Assert.That(GameEngine.QuestDone(s, "EXQ06"), Is.True);
        Assert.That(Ledger(s), Is.EqualTo(snap));
    }

    // ───────────── DIALOGUE-Q235-04：旧档、阻止打开、队列 ─────────────

    private GameState Upgrade41(GameState s, Action<JObject>? edit = null)
    {
        var j = JObject.Parse(SaveSerializer.Serialize(s));
        j["SchemaVersion"] = SaveMigrator.Schema41;
        ((JObject)j["City"]!).Remove("SubstituteIntroShown");
        edit?.Invoke(j);
        return SaveSerializer.Deserialize(j.ToString(), Cfg, out _);
    }

    /// <summary>旧档有本岗实际显示过的说明（含 #序号的重播）：无声标已示，不播、不改队列、Greeted、任务。</summary>
    [TestCase(1)]
    [TestCase(3)]
    public void Q235_04_TrustedOldRecord_SilentlyMarked(int plays)
    {
        var s = Registered("h02_square");
        E.DebugToggleCityPostAbsent(s);
        TalkRead(s, "npc_xu_an");   // 接 EXQ07
        var set = Set("npc_xu_an", "institution_substitute");
        for (var i = 1; i < plays; i++)
        {
            var d = s.Story.DialogueLog.First(x => x.EventId == set).Clone();
            d.EventId = $"{set}#{i}";
            d.Id = s.Story.NextDialogueSessionId++;
            s.Story.DialogueLog.Add(d);
        }
        var sessions = s.Story.DialogueLog.Count;
        var quests = s.Progress.Flags.Count;
        var m = Upgrade41(s);
        Assert.That(m.SchemaVersion, Is.EqualTo("42"));
        Assert.That(GameEngine.SubstituteIntroShown(m, "npc_xu_an"), Is.True);
        Assert.That(m.Story.DialogueLog.Count, Is.EqualTo(sessions));
        Assert.That(E.DialogueLocked(m), Is.False, "无声");
        Assert.That(m.Progress.Flags.Count, Is.EqualTo(quests));
        Assert.That(m.City.Greeted, Is.EquivalentTo(s.City.Greeted));
        Assert.That(GameEngine.SubstituteIntroShown(m, "npc_luo_zheng"), Is.False, "别的岗位不受影响");
        TalkRead(m, "npc_xu_an");
        Assert.That(Plays(m, set), Is.EqualTo(plays), "不再重播");
    }

    /// <summary>无可信记录不猜：Talks 次数、Greeted、原人物问候都不算；下一次合法展示一次。空台词的旧会话也不算。</summary>
    [Test]
    public void Q235_04_NoTrustedRecord_NoGuess()
    {
        var s = Registered("h03_workshop");
        TalkRead(s, "npc_luo_zheng");
        TalkRead(s, "npc_luo_zheng");
        s.City.Talks["npc_luo_zheng"] = 9;
        Assert.That(s.City.Greeted, Does.Contain("npc_luo_zheng"));
        E.DebugToggleCityPostAbsent(s);
        var set = Set("npc_luo_zheng", "institution_substitute");
        s.Story.DialogueLog.Add(new DialogueSession { Id = s.Story.NextDialogueSessionId++, EventId = set + "#7", Ended = true });
        var m = Upgrade41(s);
        Assert.That(GameEngine.SubstituteIntroShown(m, "npc_luo_zheng"), Is.False);
        Assert.That(m.City.Talks["npc_luo_zheng"], Is.EqualTo(9));
        var before = m.Story.DialogueLog.Count(d => Base(d) == set && d.Lines.Count > 0);
        TalkRead(m, "npc_luo_zheng");
        Assert.That(m.Story.DialogueLog.Count(d => Base(d) == set && d.Lines.Count > 0), Is.EqualTo(before + 1));
        TalkRead(m, "npc_luo_zheng");
        Assert.That(m.Story.DialogueLog.Count(d => Base(d) == set && d.Lines.Count > 0), Is.EqualTo(before + 1));
    }

    /// <summary>升级时已开的旧队列能播完不截断；排在后面的旧说明开始显示才记已示；同键不再排一段。</summary>
    [Test]
    public void Q235_04_OldQueue_PlaysOut_MarkedAtDisplay()
    {
        var s = Registered("h03_workshop");
        Talk(s, "npc_luo_zheng");   // 接取句正在显示
        Assert.That(E.DialogueLocked(s), Is.True);
        var set = Set("npc_luo_zheng", "institution_substitute");
        var ids = Cfg.Story.Dialogues.Where(d => d.Event == set).Select(d => d.Id).ToList();
        s.Story.DialogueLog.Add(new DialogueSession { Id = s.Story.NextDialogueSessionId++, EventId = set, Pending = ids.ToList() });
        s.Story.PendingDialogues.AddRange(ids);
        E.DebugToggleCityPostAbsent(s);
        var m = Upgrade41(s);
        Assert.That(GameEngine.SubstituteIntroShown(m, "npc_luo_zheng"), Is.False, "还没开始显示");
        Assert.That(E.DialogueLocked(m), Is.True, "旧对话不截断");
        Assert.That(E.TalkNpc(m, "npc_luo_zheng").Ok, Is.False);
        ReadDialogues(m);
        Assert.That(GameEngine.SubstituteIntroShown(m, "npc_luo_zheng"), Is.True);
        Assert.That(m.Story.DialogueLog.Single(d => Base(d) == set).Lines.Count, Is.EqualTo(ids.Count), "旧队列整段播完");
    }

    /// <summary>被阻止的交谈不吞首次：对话中、行动中、战斗中点交谈失败且不记已示，下一次合法交谈才显示；开始显示即记录，关闭后不重复。</summary>
    [Test]
    public void Q235_04_Blocked_KeepsFirst_ShownOnDisplay()
    {
        var s = Registered("h02_square");
        E.DebugToggleCityPostAbsent(s);

        Start(s, ActionRequest.Rest(60));
        var r = E.TalkNpc(s, "npc_xu_an");
        Assert.That((r.Ok, r.Reason), Is.EqualTo((false, "行动进行中")));
        Assert.That(GameEngine.SubstituteIntroShown(s, "npc_xu_an"), Is.False);
        Assert.That(E.Cancel(s).Ok, Is.True);
        ReadDialogues(s);

        s.Encounter = new PendingEncounter();
        r = E.TalkNpc(s, "npc_xu_an");
        Assert.That((r.Ok, r.Reason), Is.EqualTo((false, "先处理眼前的敌踪")));
        s.Encounter = null;
        Assert.That(GameEngine.SubstituteIntroShown(s, "npc_xu_an"), Is.False);
        Assert.That(s.City.Talks.ContainsKey("npc_xu_an"), Is.False);

        Talk(s, "npc_xu_an");
        Assert.That(GameEngine.SubstituteIntroShown(s, "npc_xu_an"), Is.True, "开始显示时提交，不等关闭");
        var saved = SaveSerializer.Deserialize(SaveSerializer.Serialize(s), Cfg);
        Assert.That(GameEngine.SubstituteIntroShown(saved, "npc_xu_an"), Is.True, "已示随存档");
        ReadDialogues(s);
        Assert.That(Plays(s, "npc_xu_an", "institution_substitute"), Is.EqualTo(1));
    }

    /// <summary>进行中的计时事务与旧队列：升级不重置配额、服务事务，升级后走完按新规则。</summary>
    [Test]
    public void Q235_04_RunningService_NotReset()
    {
        var s = Registered("c04_alms");
        SetLow(s);
        Assert.That(E.StartCityService(s, "church_relief_ration", GameState.PlayerId, "relief_food").Ok, Is.True);
        E.DebugToggleCityPostAbsent(s);
        var m = Upgrade41(s);
        Assert.That(m.City.Pending, Is.Not.Null, "已在办的事务不截断");
        Run(m);
        ReadDialogues(m);
        Assert.That(m.City.QuotaUsed.Values.Sum(), Is.EqualTo(1));
        Assert.That(Plays(m, "npc_bai_ling", "institution_substitute"), Is.EqualTo(1));
    }

    /// <summary>预览走副本：不写已示。</summary>
    [Test]
    public void Q235_04_CloneDoesNotLeak()
    {
        var s = Registered("h02_square");
        E.DebugToggleCityPostAbsent(s);
        var c = s.Clone();
        Talk(c, "npc_xu_an");
        Assert.That(GameEngine.SubstituteIntroShown(c, "npc_xu_an"), Is.True);
        Assert.That(GameEngine.SubstituteIntroShown(s, "npc_xu_an"), Is.False);
    }
    // ───────────── DIALOGUE-Q236-01：16 岗位闲谈 ─────────────

    /// <summary>说明已示、没有相关委托时：播本岗一句，可反复，发言人是机构当班接续员，文字与合同逐字一致，不重播两句接续说明。</summary>
    [TestCaseSource(nameof(Posts))]
    public void Q236_01_Idle_EachPost_Verbatim_Repeatable(string npcId)
    {
        var c = (JObject)((JArray)Q236["idleLines"]!).Single(x => (string)x["npcId"]! == npcId);
        var n = Cfg.City.Npcs.Single(x => x.NpcId == npcId);
        Assert.That(n.LocationId, Is.EqualTo((string)c["locationId"]!));
        var line = Cfg.Story.Dialogues.Single(d => d.Id == (string)c["lineId"]!);
        Assert.That((line.Event, line.Text, line.Speaker), Is.EqualTo(((string)c["dialogueSetId"]!, (string)c["text"]!, n.DutySpeaker)));
        Assert.That(n.States["substitute_idle"], Is.EqualTo((string)c["dialogueSetId"]!));

        // 关联任务都记为已完成：没有待接、没有已接未完成
        var s = New();
        s.GameMinute = At(2, 9, 0);
        s.SpawnNextCheck = int.MaxValue;
        s.PatrolNextCheck = int.MaxValue;
        foreach (var q in Cfg.Quests.List.Where(q => q.NpcId == npcId || n.QuestIds.Contains(q.Id))) s.Progress.Flags.Add($"quest:{q.Id}:completed");
        E.DebugMapTeleport(s, n.LocationId);
        ReadDialogues(s);
        s.City.Absent.Add(npcId);
        TalkRead(s, npcId);
        Assert.That(Plays(s, npcId, "institution_substitute"), Is.EqualTo(1));
        Assert.That(Plays(s, npcId, "substitute_idle"), Is.Zero, "首次说明这一次只播说明，不尾随兜底句");
        var snap = Ledger(s);
        for (var i = 1; i <= 3; i++)
        {
            var mark = s.Story.DialogueLog.Count;
            Talk(s, npcId);
            var d = s.Story.DialogueLog.Skip(mark).Single();
            Assert.That(Base(d), Is.EqualTo((string)c["dialogueSetId"]!));
            Assert.That(d.Lines.Single().Speaker, Is.EqualTo(n.DutySpeaker));
            Assert.That(d.Lines.Single().Speaker, Is.Not.EqualTo(n.Name));
            Assert.That(d.Lines.Single().Text, Is.EqualTo((string)c["text"]!));
            ReadDialogues(s);
        }
        Assert.That(Plays(s, npcId, "institution_substitute"), Is.EqualTo(1), "不重播两句接续说明");
        Assert.That(Ledger(s), Is.EqualTo(snap), "0 分钟 0 资源");
    }

    /// <summary>合同的 16 岗位与配置的 16 岗位一一对应；48 组 lineId 不重复；两条共用句三十二组逐字一致。</summary>
    [Test]
    public void Q236_01_AllGroupsVerbatim()
    {
        Assert.That(Posts, Is.EquivalentTo(Cfg.City.Npcs.Select(n => n.NpcId)));
        var ids = new List<string>();
        foreach (var n in Cfg.City.Npcs)
            foreach (var st in new[] { "substitute_quest_offer", "substitute_quest_return" })
            {
                var d = Cfg.Story.Dialogues.Single(x => x.Event == n.States[st]);
                Assert.That((d.Text, d.Speaker), Is.EqualTo((Shared(st), n.DutySpeaker)), n.States[st]);
                ids.Add(d.Id);
            }
        ids.AddRange(((JArray)Q236["idleLines"]!).Select(x => (string)x["lineId"]!));
        Assert.That(ids.Count, Is.EqualTo(48));
        Assert.That(ids.Distinct().Count(), Is.EqualTo(48));
    }

    // ───────────── DIALOGUE-Q236-02：分支优先级 ─────────────

    private GameState SubstituteReady(string loc, string npc)
    {
        var s = Registered(loc);
        s.City.Absent.Add(npc);
        s.City.SubstituteIntroShown.Add(GameEngine.SubstituteIntroKey(s, npc));
        return s;
    }

    private string TalkLine(GameState s, string npc)
    {
        var mark = s.Story.DialogueLog.Count;
        Talk(s, npc);
        var d = s.Story.DialogueLog.Skip(mark).Single();
        ReadDialogues(s);
        return d.Lines.Single().Text;
    }

    private static string IdleOf(string npc) => (string)((JArray)Q236["idleLines"]!).Single(x => (string)x["npcId"]! == npc)["text"]!;

    /// <summary>事项里待接（服务接取 FA02）：播待接句；本次不接取、不写事实。接取后未完成：播进度句。完成后：闲谈。</summary>
    [Test]
    public void Q236_02_OfferThenReturnThenIdle()
    {
        var s = SubstituteReady("c02_church", "npc_qi_wen");
        var flags = s.Progress.Flags.Count;
        Assert.That(TalkLine(s, "npc_qi_wen"), Is.EqualTo(Shared("substitute_quest_offer")));
        Assert.That(s.Progress.Flags.Count, Is.EqualTo(flags), "只是提示，不代接");
        Assert.That(GameEngine.QuestAccepted(s, "FA02"), Is.False);
        s.Progress.Flags.Add("quest:FA02:accepted");
        Assert.That(TalkLine(s, "npc_qi_wen"), Is.EqualTo(Shared("substitute_quest_return")));
        s.Progress.Flags.Add("quest:FA02:completed");
        Assert.That(TalkLine(s, "npc_qi_wen"), Is.EqualTo(IdleOf("npc_qi_wen")), "完成不触发进度提醒");
    }

    /// <summary>待接与进度都有：待接优先。许安：FA04 可在事项办理、EXQ07 已接未完成。</summary>
    [Test]
    public void Q236_02_OfferBeatsReturn()
    {
        var s = SubstituteReady("h02_square", "npc_xu_an");
        s.Progress.Flags.UnionWith(new[] { "quest:EXQ07:accepted", "quest:EXQ07:completed" });
        Assert.That(TalkLine(s, "npc_xu_an"), Is.EqualTo(IdleOf("npc_xu_an")), "FA04 未显示：隐藏的委托不触发，完成的 EXQ07 也不提醒");
        s.Progress.Flags.Remove("quest:EXQ07:completed");
        Assert.That(TalkLine(s, "npc_xu_an"), Is.EqualTo(Shared("substitute_quest_return")));
        s.Progress.Flags.UnionWith(new[] { "quest:FA02:completed", "quest:FA03:completed" });
        Assert.That(TalkLine(s, "npc_xu_an"), Is.EqualTo(Shared("substitute_quest_return")), "FA04 已显示但前置不足：无资格不触发");
        s.Progress.Flags.UnionWith(new[] { "FA02_record_credit", "FA03_record_credit" });
        Assert.That(TalkLine(s, "npc_xu_an"), Is.EqualTo(Shared("substitute_quest_offer")));
    }

    /// <summary>待接提示说的是“这里”的事项：宋烛关联 FA02、尹珂关联 AC01，它们不在本岗事项里办，不触发待接；已接未完成仍按岗位关联提醒。</summary>
    [TestCase("c06_archive", "npc_song_zhu", "FA02", new[] { "quest:FA01:completed", "church_contacted" })]
    [TestCase("r08_observation", "npc_yin_ke", "AC01", new[] { "quest:FA03:accepted" })]
    public void Q236_02_RelatedElsewhere_NoOfferHint(string loc, string npc, string quest, string[] facts)
    {
        var s = SubstituteReady(loc, npc);
        s.Progress.Flags.UnionWith(facts);
        Assert.That(E.QuestViewOf(s, quest), Is.Not.Null);
        var related = Cfg.City.Npcs.Single(n => n.NpcId == npc).QuestIds;
        Assert.That(related, Does.Contain(quest));
        Assert.That(TalkLine(s, npc), Is.Not.EqualTo(Shared("substitute_quest_offer")));
        s.Progress.Flags.Add($"quest:{quest}:accepted");
        Assert.That(TalkLine(s, npc), Is.EqualTo(Shared("substitute_quest_return")));
    }

    /// <summary>六人招募不由代班代说：林昭可邀约时，许安的代班员播闲谈；邀约卡还在、由本人说。</summary>
    [Test]
    public void Q236_02_RecruitNotSpokenBySubstitute()
    {
        var s = SubstituteReady("h02_square", "npc_xu_an");
        s.Progress.Flags.UnionWith(new[] { "quest:EXQ07:accepted", "quest:EXQ07:completed" });
        var card = E.NpcsHere(s).SingleOrDefault(c => c.NpcId == "npc_lin_zhao");
        Assert.That(card, Is.Not.Null, "前置满足的邀约卡");
        Assert.That(TalkLine(s, "npc_xu_an"), Is.EqualTo(IdleOf("npc_xu_an")));
        Talk(s, "npc_lin_zhao");
        Assert.That(s.Story.DialogueLog.Last().Lines.First().Speaker, Is.EqualTo(card!.Name));
    }

    // ───────────── DIALOGUE-Q236-03：有事务对白时不尾随 ─────────────

    /// <summary>实际接取或回报（一项、多项）时只播专属句，不尾随兜底；不多接、不多授奖。</summary>
    [Test]
    public void Q236_03_NoFallbackAfterDedicated()
    {
        var s = SubstituteReady("h03_workshop", "npc_luo_zheng");
        var mark = s.Story.DialogueLog.Count;
        TalkRead(s, "npc_luo_zheng");
        Assert.That(Since(s, mark), Is.EqualTo(new[] { "EXQ05_offer" }));
        mark = s.Story.DialogueLog.Count;
        TalkRead(s, "npc_luo_zheng");
        Assert.That(Since(s, mark), Is.EqualTo(new[] { "EXQ06_offer" }));
        mark = s.Story.DialogueLog.Count;
        TalkRead(s, "npc_luo_zheng");
        Assert.That(Since(s, mark), Is.EqualTo(new[] { Set("npc_luo_zheng", "substitute_quest_return") }), "这次没有事务才兜底");
        GiveClaim(s, "d05_07_unique", "waterworks_record");
        mark = s.Story.DialogueLog.Count;
        TalkRead(s, "npc_luo_zheng");
        Assert.That(Since(s, mark), Is.EqualTo(new[] { "EXQ05_turnin" }));
        GiveClaim(s, "d06_08_unique", "mining_record");
        mark = s.Story.DialogueLog.Count;
        var snap = Ledger(s);
        TalkRead(s, "npc_luo_zheng");
        Assert.That(Since(s, mark), Is.EqualTo(new[] { "EXQ06_turnin" }));
        Assert.That(Ledger(s), Is.EqualTo(snap));
    }

    /// <summary>本人在岗不切换成代班句：闲谈与委托句照旧。</summary>
    [Test]
    public void Q236_03_InPerson_Unchanged()
    {
        var s = Registered("h01_gate");
        TalkRead(s, "npc_lu_heng");
        TalkRead(s, "npc_lu_heng");
        Assert.That(Plays(s, "npc_lu_heng", "idle"), Is.EqualTo(1));
        Assert.That(Plays(s, "npc_lu_heng", "substitute_idle"), Is.Zero);

        var c = Registered("c02_church");
        c.Progress.Flags.UnionWith(new[] { "quest:FA01:completed", "church_contacted" });
        TalkRead(c, "npc_qi_wen");
        TalkRead(c, "npc_qi_wen");
        Assert.That(Plays(c, "npc_qi_wen", "quest_offer"), Is.EqualTo(1));
        Assert.That(Plays(c, "npc_qi_wen", "substitute_quest_offer"), Is.Zero);
    }

    // ───────────── DIALOGUE-Q236-04：零结算、读档、非法状态、升级 ─────────────

    /// <summary>反复交谈、回看、读档后再交谈：0 分钟 0 资源，身体、任务、知识、配额、奖励都不动；“歇歇”不启动休息。</summary>
    [Test]
    public void Q236_04_ZeroEffect_Repeat_Reload()
    {
        var s = SubstituteReady("c07_sanctuary", "npc_fu_heng");
        var snap = (Ledger(s), s.Progress.Flags.Count);   // 知识按事实判断，事实数含知识
        Assert.That(TalkLine(s, "npc_fu_heng"), Is.EqualTo(IdleOf("npc_fu_heng")));
        Assert.That(TalkLine(s, "npc_fu_heng"), Is.EqualTo(IdleOf("npc_fu_heng")));
        Assert.That(s.Main, Is.Null, "不启动休息行动");
        var m = SaveSerializer.Deserialize(SaveSerializer.Serialize(s), Cfg);
        Assert.That(TalkLine(m, "npc_fu_heng"), Is.EqualTo(IdleOf("npc_fu_heng")));
        Assert.That((Ledger(m), m.Progress.Flags.Count), Is.EqualTo(snap));
        Assert.That(Plays(m, "npc_fu_heng", "substitute_idle"), Is.EqualTo(3));
    }

    /// <summary>非法状态维持原禁用理由，卡上写同一句，点了不播不记。</summary>
    [Test]
    public void Q236_04_Illegal_KeepsReason()
    {
        var s = SubstituteReady("h01_gate", "npc_lu_heng");
        Start(s, ActionRequest.Rest(60));
        Assert.That(E.NpcsHere(s).Single(n => n.NpcId == "npc_lu_heng").TalkBlocked, Is.EqualTo("行动进行中"));
        var count = s.Story.DialogueLog.Count;
        Assert.That(E.TalkNpc(s, "npc_lu_heng").Reason, Is.EqualTo("行动进行中"));
        Assert.That(s.Story.DialogueLog.Count, Is.EqualTo(count));
    }

    /// <summary>升级后下一次合法交谈选新句，不补播历史，不重置 Greeted；已示的不再介绍。</summary>
    [Test]
    public void Q236_04_Upgrade_NextTalkUsesNewLine()
    {
        var s = Registered("h01_gate");
        TalkRead(s, "npc_lu_heng");
        E.DebugToggleCityPostAbsent(s);
        TalkRead(s, "npc_lu_heng");
        s.City.SubstituteIntroShown.Clear();
        var m = Upgrade41(s);
        Assert.That(m.City.Greeted, Does.Contain("npc_lu_heng"));
        Assert.That(TalkLine(m, "npc_lu_heng"), Is.EqualTo(IdleOf("npc_lu_heng")));
        Assert.That(Plays(m, "npc_lu_heng", "institution_substitute"), Is.EqualTo(1));
    }

    // ───────────── DIALOGUE-Q237：说明已示后的服务结束 ─────────────

    private GameState AppraisalOffice()
    {
        var s = Registered("h05_appraisal");
        s.Items.RemoveAll(i => i.ItemId == "fiber");
        E.CreateItems(s, "fiber", 2, ItemInstance.Carried);
        return s;
    }

    private ItemInstance Sample(GameState s) =>
        E.CreateItems(s, "active_substance_white_unidentified", 1, ItemInstance.Carried, false, new ItemSource { WorldEnemyId = 1, BattleId = 9, GrantEventId = "q237" }).Single();

    /// <summary>已示后连着完成两项服务：各写一次结束日志、各结算一次，不开对话，不播“办完事项”和代班闲谈。</summary>
    [Test]
    public void Q237_01_TwoServices_LogOnly_NoDialogue()
    {
        var s = Registered("c04_alms");
        SetLow(s);
        E.DebugToggleCityPostAbsent(s);
        TalkRead(s, "npc_bai_ling");
        var logs = s.Log.Count;
        var mark = s.Story.DialogueLog.Count;
        foreach (var (day, choice) in new[] { (2, "relief_food"), (3, "relief_water") })
        {
            s.GameMinute = At(day, 10, 0);
            Assert.That(E.StartCityService(s, "church_relief_ration", GameState.PlayerId, choice).Ok, Is.True);
            Run(s);
            Assert.That(E.DialogueLocked(s), Is.False);
        }
        Assert.That(Since(s, mark), Is.Empty, "不播 service_end，也不追加 Q236 兜底");
        Assert.That(s.Log.Skip(logs).Count(l => l.Text.Contains("完成")), Is.EqualTo(2), "每项一条结束日志");
        Assert.That(s.City.QuotaUsed.Values.Sum(), Is.EqualTo(2), "每项结算一次");
    }

    /// <summary>鉴定成功原来就不走结束钩子（Q177）：代班未示时也不为补介绍新增对白，说明留到下一次合法入口。</summary>
    [Test]
    public void Q237_02_AppraisalSuccess_NoNewHook()
    {
        var s = AppraisalOffice();
        E.DebugToggleCityPostAbsent(s);
        var npc = Cfg.City.Npcs.Single(n => n.LocationId == "h05_appraisal").NpcId;
        Assert.That(E.StartAppraise(s, Sample(s).InstanceId).Ok, Is.True);
        Run(s);
        Assert.That(E.DialogueLocked(s), Is.False);
        Assert.That(GameEngine.SubstituteIntroShown(s, npc), Is.False, "没显示就不记已示");
    }

    /// <summary>原会进结束钩子的取消：未示时只播一次说明；已示后不开对话，取消日志照写，样本不扣。</summary>
    [Test]
    public void Q237_03_Cancel_IntroOnce_ThenLogOnly()
    {
        var s = AppraisalOffice();
        E.DebugToggleCityPostAbsent(s);
        var npc = Cfg.City.Npcs.Single(n => n.LocationId == "h05_appraisal").NpcId;
        var sample = Sample(s);
        Assert.That(E.StartAppraise(s, sample.InstanceId).Ok, Is.True);
        Steps(s, 1);
        Assert.That(E.Cancel(s).Ok, Is.True);
        ReadDialogues(s);
        Assert.That(Plays(s, npc, "institution_substitute"), Is.EqualTo(1));
        Assert.That(Plays(s, npc, "service_end"), Is.Zero);

        var mark = s.Story.DialogueLog.Count;
        var logs = s.Log.Count;
        Assert.That(E.StartAppraise(s, sample.InstanceId).Ok, Is.True);
        Steps(s, 1);
        Assert.That(E.Cancel(s).Ok, Is.True);
        Assert.That(E.DialogueLocked(s), Is.False);
        Assert.That(Since(s, mark), Is.Empty);
        Assert.That(s.Log.Count, Is.GreaterThan(logs), "取消日志照写");
        Assert.That(s.Items.Any(i => i.InstanceId == sample.InstanceId), Is.True, "取消不交样");
    }

    // ───────────── DIALOGUE-Q238：代班不代记本人 ─────────────

    /// <summary>代班接了 EXQ，本人回岗当次没接 EXQ：问候和岗位介绍各一次，下一次不再播，代班已示记录不动。</summary>
    [Test]
    public void Q238_01_ReturnAfterSubstituteExq_GreetsOnce()
    {
        var s = Registered("h02_square");
        E.DebugToggleCityPostAbsent(s);
        TalkRead(s, "npc_xu_an");
        Assert.That(GameEngine.QuestAccepted(s, "EXQ07"), Is.True);
        Assert.That(s.City.Greeted, Does.Not.Contain("npc_xu_an"));
        E.DebugToggleCityPostAbsent(s);
        TalkRead(s, "npc_xu_an");
        TalkRead(s, "npc_xu_an");
        Assert.That((Plays(s, "npc_xu_an", "first_greeting"), Plays(s, "npc_xu_an", "role")), Is.EqualTo((1, 1)));
        Assert.That(GameEngine.SubstituteIntroShown(s, "npc_xu_an"), Is.True);
    }

    /// <summary>本人回岗当次实际接 EXQ：跳过两组铺垫并记已处理，之后不补播（罗铮 EXQ05、EXQ06 分两次接）。</summary>
    [Test]
    public void Q238_02_ReturnActualExq_SkipsAndMarks()
    {
        var npc = Cfg.City.Npcs.Single(n => n.NpcId == "npc_luo_zheng");
        var s = Registered(npc.LocationId);
        E.DebugToggleCityPostAbsent(s);
        TalkRead(s, npc.NpcId);
        Assert.That(GameEngine.QuestAccepted(s, "EXQ05"), Is.True);
        Assert.That(s.City.Greeted, Does.Not.Contain(npc.NpcId), "代班期间不设本人标记");
        E.DebugToggleCityPostAbsent(s);
        TalkRead(s, npc.NpcId);
        Assert.That(GameEngine.QuestAccepted(s, "EXQ06"), Is.True);
        Assert.That(s.City.Greeted, Does.Contain(npc.NpcId));
        TalkRead(s, npc.NpcId);
        Assert.That(Plays(s, npc.NpcId, "first_greeting") + Plays(s, npc.NpcId, "role"), Is.Zero, "不补播");
    }

    /// <summary>旧记录：已见过本人的回岗不重播；没见过的不按交谈次数和任务事实猜。</summary>
    [TestCase(true, 0)]
    [TestCase(false, 1)]
    public void Q238_03_OldRecord_NoGuess(bool greeted, int plays)
    {
        var s = Registered("h02_square");
        E.DebugToggleCityPostAbsent(s);
        TalkRead(s, "npc_xu_an");
        E.DebugToggleCityPostAbsent(s);
        s.City.Talks["npc_xu_an"] = 5;
        if (greeted) s.City.Greeted.Add("npc_xu_an");
        var m = SaveSerializer.Deserialize(SaveSerializer.Serialize(s), Cfg);
        TalkRead(m, "npc_xu_an");
        Assert.That((Plays(m, "npc_xu_an", "first_greeting"), Plays(m, "npc_xu_an", "role")), Is.EqualTo((plays, plays)));
    }
}
