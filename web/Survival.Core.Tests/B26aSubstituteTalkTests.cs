using Newtonsoft.Json.Linq;
using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 批次 B26a（R22 Q235、R26 Q236）。规则来源：策划合同 exq-dialogue-flow-adjudications.json、substitute-talk-adjudications.json；
/// 验收 DIALOGUE-Q235-01—04、DIALOGUE-Q236-01—04。Q237（说明已示后服务结束）、Q238（代班期间接 EXQ 算不算本人首次）待答，这里只锁定“答复前不动”的现状。
/// </summary>
public class B26aSubstituteTalkTests : TestBase
{
    private static readonly string[] Dedicated = { "EXQ05", "EXQ06", "EXQ07", "EXQ08" };

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

    /// <summary>代班首次与 EXQ 接取同次：先说明、再专属句，不要通用铺垫；和代班员交谈不写 Greeted（Q238 待答，保持现状）。</summary>
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
        Assert.That(s.City.Greeted, Does.Not.Contain("npc_xu_an"), "Q238 待答：代班交谈不算本人首次");
        E.DebugToggleCityPostAbsent(s);
        TalkRead(s, "npc_xu_an");
        Assert.That((Plays(s, "npc_xu_an", "first_greeting"), Plays(s, "npc_xu_an", "role")), Is.EqualTo((1, 1)), "Q238 待答：本人回岗后照现状播首次问候");
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
    public void Q235_02_ServiceEnd_IntroOnce_ThenSilentUntilQ237()
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
        Assert.That(E.DialogueLocked(s), Is.False, "Q237 待答：已示后服务结束不开对话");
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
}
