using System.Reflection;
using Newtonsoft.Json.Linq;
using Survival.Core.Config;
using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 批次 B26a（R22 Q235、R26 Q236）。规则来源：策划合同 exq-dialogue-flow-adjudications.json、substitute-talk-adjudications.json、
/// substitute-title-adjudications.json、substitute-followup-adjudications.json、substitute-deferred-adjudications.json；验收 DIALOGUE-Q235-01—04、DIALOGUE-Q236-01—04。
/// DIALOGUE-Q237-01—03、Q238-01—03、Q239-01—03、Q240-01、Q241-01/02、Q242-01/02、Q243-01—05 在文件后部。
/// </summary>
public class B26aSubstituteTalkTests : TestBase
{
    private static readonly string[] Dedicated = { "EXQ05", "EXQ06", "EXQ07", "EXQ08" };

    private static JObject Design(string file) => JObject.Parse(File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, file)));

    private static readonly JObject Q236 = Design("design.substitute-talk.json");

    private static readonly JObject Q239 = Design("design.substitute-title.json");

    private static readonly JObject Q240To242 = Design("design.substitute-followup.json");

    private static string Shared(string state) => (string)Q236["sharedLines"]![state]!;

    /// <summary>B38 N02：Q236 原两条共用句改为各岗位各自一句（按该岗该组的 lineId 取 N02 新稿，合同原文须是旧共用句）。</summary>
    private static string SharedOf(string npc, string state)
    {
        var set = Cfg.City.Npcs.Single(n => n.NpcId == npc).States[state];
        return N02.Line(Cfg.Story.Dialogues.Single(d => d.Event == set).Id, Shared(state));
    }

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
        Assert.That(s.Log.Skip(logs).Any(l => l.Text.Contains("可自行领取")), Is.True, "结束日志照写");
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
        Assert.That(m.SchemaVersion, Is.EqualTo(GameState.CurrentSchema));
        Assert.That(GameEngine.SubstituteIntroShown(m, "npc_xu_an"), Is.True);
        Assert.That(m.Story.DialogueLog.Count, Is.EqualTo(sessions));
        Assert.That(E.DialogueLocked(m), Is.False, "无声");
        Assert.That(m.Progress.Flags.Count, Is.EqualTo(quests + Cfg.City.Registration.LegacyKnowledge.Count), "只多 42→43 的五项永久知识（Q246）");
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
        var text = N02.Line((string)c["lineId"]!, (string)c["text"]!);
        Assert.That((line.Event, line.Text, line.Speaker), Is.EqualTo(((string)c["dialogueSetId"]!, text, n.DutySpeaker)));
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
            Assert.That(d.Lines.Single().Text, Is.EqualTo(text));
            ReadDialogues(s);
        }
        Assert.That(Plays(s, npcId, "institution_substitute"), Is.EqualTo(1), "不重播两句接续说明");
        Assert.That(Ledger(s), Is.EqualTo(snap), "0 分钟 0 资源");
    }

    /// <summary>合同的 16 岗位与配置的 16 岗位一一对应；48 组 lineId 不重复；三十二组待接/进度句：合同原为两条共用句，B38 N02 起按各岗 lineId 逐字等于新稿。</summary>
    [Test]
    public void Q236_01_AllGroupsVerbatim()
    {
        Assert.That(Posts, Is.EquivalentTo(Cfg.City.Npcs.Select(n => n.NpcId)));
        var ids = new List<string>();
        foreach (var n in Cfg.City.Npcs)
            foreach (var st in new[] { "substitute_quest_offer", "substitute_quest_return" })
            {
                var d = Cfg.Story.Dialogues.Single(x => x.Event == n.States[st]);
                Assert.That((d.Text, d.Speaker), Is.EqualTo((N02.Line(d.Id, Shared(st)), n.DutySpeaker)), n.States[st]);
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

    private static string IdleOf(string npc)
    {
        var c = ((JArray)Q236["idleLines"]!).Single(x => (string)x["npcId"]! == npc);
        return N02.Line((string)c["lineId"]!, (string)c["text"]!);
    }

    /// <summary>事项里待接（服务接取 FA02）：播待接句；本次不接取、不写事实。接取后未完成：播进度句。完成后：闲谈。</summary>
    [Test]
    public void Q236_02_OfferThenReturnThenIdle()
    {
        var s = SubstituteReady("c02_church", "npc_qi_wen");
        var flags = s.Progress.Flags.Count;
        Assert.That(TalkLine(s, "npc_qi_wen"), Is.EqualTo(SharedOf("npc_qi_wen", "substitute_quest_offer")));
        Assert.That(s.Progress.Flags.Count, Is.EqualTo(flags), "只是提示，不代接");
        Assert.That(GameEngine.QuestAccepted(s, "FA02"), Is.False);
        s.Progress.Flags.Add("quest:FA02:accepted");
        Assert.That(TalkLine(s, "npc_qi_wen"), Is.EqualTo(SharedOf("npc_qi_wen", "substitute_quest_return")));
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
        Assert.That(TalkLine(s, "npc_xu_an"), Is.EqualTo(SharedOf("npc_xu_an", "substitute_quest_return")));
        s.Progress.Flags.UnionWith(new[] { "quest:FA02:completed", "quest:FA03:completed" });
        Assert.That(TalkLine(s, "npc_xu_an"), Is.EqualTo(SharedOf("npc_xu_an", "substitute_quest_return")), "FA04 已显示但前置不足：无资格不触发");
        s.Progress.Flags.UnionWith(new[] { "FA02_record_credit", "FA03_record_credit" });
        Assert.That(TalkLine(s, "npc_xu_an"), Is.EqualTo(SharedOf("npc_xu_an", "substitute_quest_offer")));
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
        Assert.That(TalkLine(s, npc), Is.Not.EqualTo(SharedOf(npc, "substitute_quest_offer")));
        s.Progress.Flags.Add($"quest:{quest}:accepted");
        Assert.That(TalkLine(s, npc), Is.EqualTo(SharedOf(npc, "substitute_quest_return")));
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
        Assert.That(s.Log.Skip(logs).Count(l => l.Text.Contains("可自行领取")), Is.EqualTo(2), "每项一条结束日志");
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

    // ───────────── DIALOGUE-Q239-01：48 组组名 ─────────────

    private static IEnumerable<JToken> TitleGroups => (JArray)Q239["groups"]!;

    private static string TitleOf(string set) => (string)TitleGroups.Single(g => (string)g["dialogueSetId"]! == set)["title"]!;

    private static GameConfig FreshConfig() => ConfigLoader.FromJson(File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "config.v0.6.json")));

    /// <summary>配置 48 组按组 ID 逐字等于合同：48 个 ID、48 个标题互不相同，岗位名加固定后缀，不带原人物名，发言人是本机构当班接续员。</summary>
    [Test]
    public void Q239_01_AllGroups_TitleFromContract()
    {
        var suffix = new Dictionary<string, string> { ["substitute_idle"] = "代班闲谈", ["substitute_quest_offer"] = "代班事项提示", ["substitute_quest_return"] = "代班进度提醒" };
        Assert.That(TitleGroups.Count(), Is.EqualTo(48));
        Assert.That(TitleGroups.Select(g => (string)g["title"]!).Distinct().Count(), Is.EqualTo(48));
        Assert.That(TitleGroups.Select(g => (string)g["postName"]!).Distinct().Count(), Is.EqualTo(16));
        foreach (var g in TitleGroups)
        {
            var (set, state, title) = ((string)g["dialogueSetId"]!, (string)g["state"]!, (string)g["title"]!);
            var n = Cfg.City.Npcs.Single(x => x.NpcId == (string)g["npcId"]!);
            Assert.That(n.States[state], Is.EqualTo(set));
            Assert.That(Cfg.City.DialogueSets.Single(d => d.Id == set).Title, Is.EqualTo(title));
            Assert.That(E.DialogueSetTitle(set), Is.EqualTo(title));
            Assert.That(title, Is.EqualTo($"{g["postName"]}：{suffix[state]}"));
            Assert.That(title, Does.Not.Contain(n.Name));
            Assert.That(Cfg.Story.Dialogues.Single(d => d.Event == set).Speaker, Is.EqualTo(n.DutySpeaker));
        }
    }

    /// <summary>
    /// 16 岗位各走真实的三类分支。现配置大多数岗位没有经事项办理的未接委托（Q241 下走不到待接分支），
    /// 这里给每个岗位放一条测试委托，把其余任务关掉，让三类分支都由 SubstituteTalkState 选出。
    /// </summary>
    [TestCaseSource(nameof(Posts))]
    public void Q239_01_EachPost_ThreeBranches_HistoryTitle(string npcId)
    {
        var n = Cfg.City.Npcs.Single(x => x.NpcId == npcId);
        var cfg = FreshConfig();
        foreach (var q in cfg.Quests.List) q.Implemented = false;
        cfg.Quests.List.Add(new QuestConfig { Id = "T01", Title = "测试委托", Kind = "side", NpcId = npcId, LocationId = n.LocationId, Accept = "service", Implemented = true });
        E = new GameEngine(cfg);
        var s = SubstituteReady(n.LocationId, npcId);
        var sets = new[] { "substitute_quest_offer", "substitute_quest_return", "substitute_idle" }.Select(st => Set(npcId, st)).ToList();
        var facts = new[] { "", "quest:T01:accepted", "quest:T01:completed" };
        for (var i = 0; i < 3; i++)
        {
            if (facts[i] != "") s.Progress.Flags.Add(facts[i]);
            var mark = s.Story.DialogueLog.Count;
            TalkRead(s, npcId);
            var d = s.Story.DialogueLog.Skip(mark).Single();
            Assert.That(Base(d), Is.EqualTo(sets[i]));
            Assert.That((d.Lines.Single().Speaker, d.Counterpart), Is.EqualTo((n.DutySpeaker, n.DutySpeaker)));
        }
        var history = E.DialogueHistory(s).Where(h => sets.Contains(Base(h)));
        Assert.That(history.Select(h => E.DialogueSetTitle(h.EventId)), Is.EqualTo(sets.Select(TitleOf)));
    }

    // ───────────── DIALOGUE-Q239-02：同组同名、未见不列 ─────────────

    /// <summary>同一组第一次和 #序号重播、读档后回看，都是同一岗位标题；正文不变。</summary>
    [Test]
    public void Q239_02_NewAndReplay_SameTitle()
    {
        var s = SubstituteReady("c07_sanctuary", "npc_fu_heng");
        for (var i = 0; i < 3; i++) TalkRead(s, "npc_fu_heng");
        var set = Set("npc_fu_heng", "substitute_idle");
        foreach (var g in new[] { s, SaveSerializer.Deserialize(SaveSerializer.Serialize(s), Cfg) })
        {
            var h = E.DialogueHistory(g).Where(d => Base(d) == set).ToList();
            Assert.That(h.Select(d => d.EventId).Distinct().Count(), Is.EqualTo(3));
            Assert.That(h.Select(d => E.DialogueSetTitle(d.EventId)), Is.All.EqualTo("圣堂主持岗：代班闲谈"));
            Assert.That(h.Select(d => d.Lines.Single().Text), Is.All.EqualTo(IdleOf("npc_fu_heng")));
        }
    }

    /// <summary>本人在岗各组与代班接续说明的标题不改：仍以人物名开头，不带“代班”。</summary>
    [Test]
    public void Q239_02_NamedTitles_Unchanged()
    {
        foreach (var n in Cfg.City.Npcs)
            foreach (var kv in n.States.Where(kv => !kv.Key.StartsWith("substitute_") && kv.Key != GameEngine.SubstituteIntroState))
            {
                var title = E.DialogueSetTitle(kv.Value);
                Assert.That(title, Does.StartWith(n.Name + "："), kv.Value);
                Assert.That(title, Does.Not.Contain("代班"), kv.Value);
            }
        Assert.That(E.DialogueSetTitle(Set("npc_xu_an", "institution_substitute")), Is.EqualTo("营区公事务岗：岗位接续"), "Q244 接续说明也用岗位名");
    }

    /// <summary>新目录不预先进历史：没产生过的组不列，产生一组只多这一组，同岗位没走到的分支不出现。</summary>
    [Test]
    public void Q239_02_UnseenGroups_NotInHistory()
    {
        var all = TitleGroups.Select(g => (string)g["dialogueSetId"]!).ToHashSet();
        var s = SubstituteReady("c07_sanctuary", "npc_fu_heng");
        Assert.That(s.Story.DialogueLog.Any(d => all.Contains(Base(d))), Is.False);
        TalkRead(s, "npc_fu_heng");
        Assert.That(E.DialogueHistory(s).Where(d => all.Contains(Base(d))).Select(Base), Is.EqualTo(new[] { Set("npc_fu_heng", "substitute_idle") }));
    }

    // ───────────── DIALOGUE-Q239-03：旧记录与零结算 ─────────────

    private static DialogueSession OldSession(GameState s, string eventId, string text, string speaker) => new DialogueSession
    {
        Id = s.Story.NextDialogueSessionId++, EventId = eventId, Counterpart = speaker, Minute = At(1, 8, 30), LocationId = "h02_square", Ended = true,
        Lines = { new DialogueLine { LineId = "old_line", Order = 1, Speaker = speaker, Text = text, Minute = At(1, 8, 30), LocationId = "h02_square" } },
    };

    /// <summary>旧记录有可信组 ID（含 #序号）：组名按 ID 解析成定稿；原文、发言人、时间、已示事实不动，也不补建别的条目。</summary>
    [Test]
    public void Q239_03_TrustedOldId_ResolvesTitle_ContentKept()
    {
        var s = Registered("h02_square");
        var set = Set("npc_xu_an", "substitute_idle");
        s.Story.DialogueLog.Add(OldSession(s, set + "#4", "旧记录原话", "旧记录发言人"));
        var count = s.Story.DialogueLog.Count;
        var m = Upgrade41(s);
        var d = m.Story.DialogueLog.Single(x => Base(x) == set);
        Assert.That(E.DialogueSetTitle(d.EventId), Is.EqualTo("营区公事务岗：代班闲谈"));
        var line = d.Lines.Single();
        Assert.That((line.Text, line.Speaker, line.Minute, d.Minute, d.LocationId), Is.EqualTo(("旧记录原话", "旧记录发言人", (int?)At(1, 8, 30), (int?)At(1, 8, 30), "h02_square")));
        Assert.That(m.Story.DialogueLog.Count, Is.EqualTo(count));
        Assert.That(GameEngine.SubstituteIntroShown(m, "npc_xu_an"), Is.False, "闲谈组不算说明已示");
    }

    /// <summary>无可信组 ID 不按台词猜：哪怕台词和某组一字不差，组名也解析不出来，对话记录走界面原有的通用“对话”。</summary>
    [TestCase("old_talk_9")]
    [TestCase("")]
    public void Q239_03_NoTrustedId_NotGuessedFromText(string eventId)
    {
        var s = Registered("h02_square");
        s.Story.DialogueLog.Add(OldSession(s, eventId, IdleOf("npc_xu_an"), Cfg.City.Npcs.Single(n => n.NpcId == "npc_xu_an").DutySpeaker));
        var m = Upgrade41(s);
        Assert.That(E.DialogueSetTitle(m.Story.DialogueLog.Last().EventId), Is.Null);
    }

    /// <summary>进行中的旧对白沿原队列播完，不重复排队，播完后组名照常。</summary>
    [Test]
    public void Q239_03_OldQueue_PlaysOut_Once()
    {
        var s = Registered("h03_workshop");
        Talk(s, "npc_luo_zheng");   // 接取句正在显示
        var set = Set("npc_luo_zheng", "substitute_idle");
        var ids = Cfg.Story.Dialogues.Where(d => d.Event == set).Select(d => d.Id).ToList();
        s.Story.DialogueLog.Add(new DialogueSession { Id = s.Story.NextDialogueSessionId++, EventId = set, Pending = ids.ToList() });
        s.Story.PendingDialogues.AddRange(ids);
        var m = Upgrade41(s);
        ReadDialogues(m);
        var d = m.Story.DialogueLog.Single(x => Base(x) == set);
        Assert.That(d.Lines.Count, Is.EqualTo(ids.Count));
        Assert.That(E.DialogueSetTitle(d.EventId), Is.EqualTo("营区工务岗：代班闲谈"));
    }

    /// <summary>查看对话记录与解析组名不改存档：0 分钟 0 结算，不设本人首次，不生成任务或奖励。</summary>
    [Test]
    public void Q239_03_History_ZeroEffect()
    {
        var s = SubstituteReady("h03_workshop", "npc_luo_zheng");
        TalkRead(s, "npc_luo_zheng");
        var before = SaveSerializer.Serialize(s);
        foreach (var h in E.DialogueHistory(s)) E.DialogueSetTitle(h.EventId);
        Assert.That(SaveSerializer.Serialize(s), Is.EqualTo(before));
        Assert.That(s.City.Greeted, Does.Not.Contain("npc_luo_zheng"));
    }

    // ───────────── DIALOGUE-Q240-01：版本 42 升级日志 ─────────────

    private static string UpgradeLog => (string)Q240To242["policy"]!["upgradeLog"]!;

    private static int UpgradeLogs(GameState s) => s.Log.Count(l => l.Text == UpgradeLog);

    /// <summary>实际升级写一条逐字日志：不开对话、不走时间、任务配额奖励不动；保存重载不重复；交谈不再写；未升级的旧档副本另读时只在副本里写一次。</summary>
    [Test]
    public void Q240_01_Upgrade_LogVerbatim_Once()
    {
        var s = Registered("h02_square");
        E.DebugToggleCityPostAbsent(s);
        TalkRead(s, "npc_xu_an");
        var snap = (Ledger(s), s.Progress.Flags.Count, s.Story.DialogueLog.Count, s.GameMinute, s.City.Greeted.Count);
        var logs = s.Log.Select(l => l.Text).ToList();

        var m = Upgrade41(s);
        Assert.That(m.Log.Select(l => l.Text), Is.EqualTo(logs.Append(UpgradeLog)), "只多这一条，旧日志不改写");
        Assert.That(m.Log.Last().Kind, Is.EqualTo(GameEventKind.Info.ToString()));
        // 已登记旧档接着走 42→43：Flags 只多五项永久知识（Q246）
        Assert.That((Ledger(m), m.Progress.Flags.Count - Cfg.City.Registration.LegacyKnowledge.Count, m.Story.DialogueLog.Count, m.GameMinute, m.City.Greeted.Count), Is.EqualTo(snap));
        Assert.That(E.DialogueLocked(m), Is.False, "不弹对话");

        var r = SaveSerializer.Deserialize(SaveSerializer.Serialize(m), Cfg);
        r = SaveSerializer.Deserialize(SaveSerializer.Serialize(r), Cfg);
        Assert.That(UpgradeLogs(r), Is.EqualTo(1));
        TalkRead(r, "npc_xu_an");
        Assert.That(UpgradeLogs(r), Is.EqualTo(1));

        Assert.That(UpgradeLogs(Upgrade41(s)), Is.EqualTo(1), "独立旧档副本按原规则再迁移一次");
    }

    /// <summary>新局和已是 42 的档不写。</summary>
    [Test]
    public void Q240_01_NoUpgrade_NoLog()
    {
        var s = Registered("h02_square");
        TalkRead(s, "npc_xu_an");
        Assert.That(UpgradeLogs(s), Is.Zero);
        Assert.That(UpgradeLogs(SaveSerializer.Deserialize(SaveSerializer.Serialize(s), Cfg)), Is.Zero);
    }

    /// <summary>较老的档走原迁移链，各版本日志各写各的。</summary>
    [Test]
    public void Q240_01_OlderSave_Chain_KeepsEachLog()
    {
        var m = Upgrade41(Registered("h02_square"), j => j["SchemaVersion"] = SaveMigrator.Schema40);
        Assert.That(m.Log.Count(l => l.Text.StartsWith($"存档已升级到版本{SaveMigrator.Schema41}（")), Is.EqualTo(1));
        Assert.That(UpgradeLogs(m), Is.EqualTo(1));
        Assert.That(m.Log.Last().Text, Is.EqualTo(UpgradeLog));
    }

    /// <summary>更高版本的档不降级。</summary>
    [Test]
    public void Q240_01_HigherVersion_NotDowngraded() =>
        Assert.Throws<SaveVersionException>(() => Upgrade41(Registered("h02_square"), j => j["SchemaVersion"] = (int.Parse(GameState.CurrentSchema) + 1).ToString()));

    // ───────────── DIALOGUE-Q241-01：待接只算本岗面板 ─────────────

    /// <summary>面板确有合格委托时播待接句；可见条件失效，面板不再列，下一次交谈立即不算。</summary>
    [Test]
    public void Q241_01_PanelOffer_GoneWhenNoLongerListed()
    {
        var s = SubstituteReady("c02_church", "npc_qi_wen");
        Assert.That(E.QuestsOfNpc(s, "npc_qi_wen").Single(q => q.Id == "FA02").Status, Is.EqualTo("available"));
        Assert.That(TalkLine(s, "npc_qi_wen"), Is.EqualTo(SharedOf("npc_qi_wen", "substitute_quest_offer")));
        s.Progress.Flags.Remove("church_contacted");
        Assert.That(E.QuestsOfNpc(s, "npc_qi_wen").Any(q => q.Id == "FA02"), Is.False);
        Assert.That(TalkLine(s, "npc_qi_wen"), Is.EqualTo(IdleOf("npc_qi_wen")));
    }

    /// <summary>合同点名的四项：委托已显示、在岗位关联列表里，但不在本岗面板，未接时播闲谈不播待接；接了之后按原规则播进度。</summary>
    [TestCase("c06_archive", "npc_song_zhu", "FA02", new[] { "church_contacted" })]
    [TestCase("c06_archive", "npc_song_zhu", "CH02", new[] { "quest:CH01:completed" })]
    [TestCase("r05_testing", "npc_ji_wen", "FA03", new[] { "academy_contacted", "quest:AC01:completed" })]
    [TestCase("r08_observation", "npc_yin_ke", "AC01", new[] { "quest:FA03:accepted" })]
    public void Q241_01_RelatedOnly_IdleNotOffer(string loc, string npc, string quest, string[] facts)
    {
        var s = SubstituteReady(loc, npc);
        s.Progress.Flags.UnionWith(facts);
        Assert.That(E.QuestViewOf(s, quest)!.Status, Is.EqualTo("available"));
        Assert.That(Cfg.City.Npcs.Single(n => n.NpcId == npc).QuestIds, Does.Contain(quest));
        Assert.That(E.QuestsOfNpc(s, npc).Any(q => q.Id == quest), Is.False, "本岗面板不列");
        Assert.That(TalkLine(s, npc), Is.EqualTo(IdleOf(npc)));
        s.Progress.Flags.Add($"quest:{quest}:accepted");
        Assert.That(TalkLine(s, npc), Is.EqualTo(SharedOf(npc, "substitute_quest_return")));
    }

    /// <summary>判断跟着面板走，不是写死的 ID：同一任务改由本岗办理后就算待接。</summary>
    [Test]
    public void Q241_01_FollowsPanel_NotFixedIds()
    {
        var cfg = FreshConfig();
        cfg.Quests.List.Single(x => x.Id == "FA02").NpcId = "npc_song_zhu";
        E = new GameEngine(cfg);
        var s = SubstituteReady("c06_archive", "npc_song_zhu");
        s.Progress.Flags.Add("church_contacted");
        Assert.That(TalkLine(s, "npc_song_zhu"), Is.EqualTo(SharedOf("npc_song_zhu", "substitute_quest_offer")));
    }

    // ───────────── DIALOGUE-Q241-02：优先级与不能冒充待接 ─────────────

    /// <summary>实际回报先于兜底；无事务时待接优先于进度；待接没了才进度；都没有才闲谈。许安：FA04 经事项办理，EXQ07 已接未完成。</summary>
    [Test]
    public void Q241_02_Order()
    {
        var s = SubstituteReady("h02_square", "npc_xu_an");
        s.Progress.Flags.UnionWith(new[] { "quest:EXQ07:accepted", "quest:FA02:completed", "quest:FA03:completed", "FA02_record_credit", "FA03_record_credit" });
        Assert.That(TalkLine(s, "npc_xu_an"), Is.EqualTo(SharedOf("npc_xu_an", "substitute_quest_offer")), "待接优先于进度");

        s.Progress.Flags.Add("quest:FA04:accepted");
        Assert.That(TalkLine(s, "npc_xu_an"), Is.EqualTo(SharedOf("npc_xu_an", "substitute_quest_return")), "待接没了才进度");

        s.Progress.Flags.Remove("quest:FA04:accepted");
        GiveClaim(s, "d07_07_unique", "civilian_cache_knowledge");
        var mark = s.Story.DialogueLog.Count;
        TalkRead(s, "npc_xu_an");
        Assert.That(Since(s, mark), Is.EqualTo(new[] { "EXQ07_turnin" }), "实际回报时不追加兜底");

        s.Progress.Flags.UnionWith(new[] { "quest:FA04:accepted", "quest:FA04:completed" });
        Assert.That(TalkLine(s, "npc_xu_an"), Is.EqualTo(IdleOf("npc_xu_an")));
    }

    /// <summary>未显示、前置不足、已接、已完不能冒充待接（教会接待岗 FA02、公事务岗 FA04）。</summary>
    [Test]
    public void Q241_02_NotEligible_NoOffer()
    {
        var s = SubstituteReady("c02_church", "npc_qi_wen");
        s.Progress.Flags.Remove("church_contacted");
        Assert.That(TalkLine(s, "npc_qi_wen"), Is.EqualTo(IdleOf("npc_qi_wen")), "未显示");
        s.Progress.Flags.UnionWith(new[] { "church_contacted", "quest:FA02:accepted" });
        Assert.That(TalkLine(s, "npc_qi_wen"), Is.EqualTo(SharedOf("npc_qi_wen", "substitute_quest_return")), "已接");
        s.Progress.Flags.Add("quest:FA02:completed");
        Assert.That(TalkLine(s, "npc_qi_wen"), Is.EqualTo(IdleOf("npc_qi_wen")), "已完");

        var x = SubstituteReady("h02_square", "npc_xu_an");
        x.Progress.Flags.UnionWith(new[] { "quest:EXQ07:accepted", "quest:EXQ07:completed", "quest:FA02:completed", "quest:FA03:completed" });
        Assert.That(E.QuestsOfNpc(x, "npc_xu_an").Single(q => q.Id == "FA04").Status, Is.EqualTo("locked"));
        Assert.That(TalkLine(x, "npc_xu_an"), Is.EqualTo(IdleOf("npc_xu_an")), "已显示但前置不足");
    }

    /// <summary>未实装的委托不进面板，也不算待接。</summary>
    [Test]
    public void Q241_02_NotImplemented_NoOffer()
    {
        var cfg = FreshConfig();
        cfg.Quests.List.Single(x => x.Id == "FA02").Implemented = false;
        E = new GameEngine(cfg);
        var s = SubstituteReady("c02_church", "npc_qi_wen");
        Assert.That(TalkLine(s, "npc_qi_wen"), Is.EqualTo(IdleOf("npc_qi_wen")));
    }

    /// <summary>本人在岗的通用句仍按岗位关联判断：宋烛本人遇到关联的 FA02 照旧播“委托”组，这一条不跟着 Q241 收紧。</summary>
    [Test]
    public void Q241_02_InPerson_GenericUnchanged()
    {
        var s = Registered("c06_archive");
        s.Progress.Flags.Add("church_contacted");
        TalkRead(s, "npc_song_zhu");
        TalkRead(s, "npc_song_zhu");
        Assert.That(Plays(s, "npc_song_zhu", "quest_offer"), Is.EqualTo(1));
        Assert.That(Plays(s, "npc_song_zhu", "substitute_quest_offer") + Plays(s, "npc_song_zhu", "substitute_idle"), Is.Zero);
    }

    // ───────────── DIALOGUE-Q242-01：事项按钮接 EXQ05—08 ─────────────

    /// <summary>本人从按钮实际接：面板有这个按钮，只播专属接取，记本人首次已处理，下次交谈不补问候和介绍；0 分钟 0 物资。</summary>
    [TestCase("h03_workshop", "npc_luo_zheng", "EXQ05")]
    [TestCase("h03_workshop", "npc_luo_zheng", "EXQ06")]
    [TestCase("h02_square", "npc_xu_an", "EXQ07")]
    [TestCase("h04_infirmary", "npc_chen_zhi", "EXQ08")]
    public void Q242_01_InPerson_Button_OfferOnly_MarksGreeted(string loc, string npc, string quest)
    {
        var s = Registered(loc);
        var v = E.QuestViewOf(s, quest)!;
        Assert.That((v.AcceptHere, v.AcceptBlocked), Is.EqualTo((true, (string?)null)));
        var mark = s.Story.DialogueLog.Count;
        var snap = Ledger(s);
        Assert.That(E.AcceptQuest(s, quest, "r1").Ok, Is.True);
        ReadDialogues(s);
        Assert.That(Since(s, mark), Is.EqualTo(new[] { quest + "_offer" }));
        Assert.That(s.City.Greeted, Does.Contain(npc));
        Assert.That(Ledger(s), Is.EqualTo(snap));
        TalkRead(s, npc);
        Assert.That(Plays(s, npc, "first_greeting") + Plays(s, npc, "role"), Is.Zero, "下次交谈不补");
    }

    /// <summary>代班从按钮实际接，说明未示：先说明后专属接取，不记本人首次。</summary>
    [TestCase("h03_workshop", "npc_luo_zheng", "EXQ05")]
    [TestCase("h03_workshop", "npc_luo_zheng", "EXQ06")]
    [TestCase("h02_square", "npc_xu_an", "EXQ07")]
    [TestCase("h04_infirmary", "npc_chen_zhi", "EXQ08")]
    public void Q242_01_Substitute_Button_IntroThenOffer_NoGreeted(string loc, string npc, string quest)
    {
        var s = Registered(loc);
        E.DebugToggleCityPostAbsent(s);
        var mark = s.Story.DialogueLog.Count;
        Assert.That(E.AcceptQuest(s, quest, "r1").Ok, Is.True);
        Assert.That(GameEngine.SubstituteIntroShown(s, npc), Is.True);
        ReadDialogues(s);
        Assert.That(Since(s, mark), Is.EqualTo(new[] { Set(npc, "institution_substitute"), quest + "_offer" }));
        Assert.That(s.City.Greeted, Does.Not.Contain(npc));
    }

    /// <summary>代班说明已示：按钮只播专属接取，不再介绍，不追加 Q236 兜底，不记本人首次。</summary>
    [Test]
    public void Q242_01_Substitute_Button_Shown_OfferOnly()
    {
        var s = SubstituteReady("h03_workshop", "npc_luo_zheng");
        foreach (var q in new[] { "EXQ05", "EXQ06" })
        {
            var mark = s.Story.DialogueLog.Count;
            Assert.That(E.AcceptQuest(s, q, "r" + q).Ok, Is.True);
            ReadDialogues(s);
            Assert.That(Since(s, mark), Is.EqualTo(new[] { q + "_offer" }));
        }
        Assert.That(Plays(s, "npc_luo_zheng", "institution_substitute"), Is.Zero);
        Assert.That(s.City.Greeted, Does.Not.Contain("npc_luo_zheng"));
    }

    // ───────────── DIALOGUE-Q242-02：不算实际接取的都不吞记录 ─────────────

    private static void AssertUntouched(GameState s, string npc, string quest, int sessions)
    {
        Assert.That(s.City.Greeted, Does.Not.Contain(npc));
        Assert.That(GameEngine.SubstituteIntroShown(s, npc), Is.False);
        Assert.That(GameEngine.QuestAccepted(s, quest), Is.False);
        Assert.That(s.Story.DialogueLog.Count, Is.EqualTo(sessions));
    }

    /// <summary>
    /// 打开事项与预览不改存档；对话中、行动中、敌踪中、不在地点、前置不足的请求都失败且不吞记录；之后第一次合法按钮照常（本人只专属，代班先说明）。
    /// 现版本接取是当场提交，没有“取消接取”入口。
    /// </summary>
    [TestCase(false)]
    [TestCase(true)]
    public void Q242_02_PreviewAndFailed_KeepFirst(bool absent)
    {
        const string npc = "npc_xu_an", quest = "EXQ07";
        var s = Registered("h02_square");
        if (absent) E.DebugToggleCityPostAbsent(s);
        var sessions = s.Story.DialogueLog.Count;

        var before = SaveSerializer.Serialize(s);
        E.QuestsOfNpc(s, npc);
        E.QuestViewOf(s, quest);
        E.QuestBook(s);
        E.CityServicesOf(s, npc);
        E.NpcsHere(s);
        Assert.That(SaveSerializer.Serialize(s), Is.EqualTo(before), "预览 0 写入");

        Start(s, ActionRequest.Rest(60));
        Assert.That(E.AcceptQuest(s, quest).Reason, Is.EqualTo("行动进行中"));
        AssertUntouched(s, npc, quest, sessions);
        Assert.That(E.Cancel(s).Ok, Is.True);
        ReadDialogues(s);
        sessions = s.Story.DialogueLog.Count;

        s.Encounter = new PendingEncounter();
        Assert.That(E.AcceptQuest(s, quest).Reason, Is.EqualTo("先处理眼前的敌踪"));
        s.Encounter = null;
        AssertUntouched(s, npc, quest, sessions);

        var ids = Cfg.Story.Dialogues.Where(d => d.Event == "HU01_offer").Select(d => d.Id).ToList();
        var open = new DialogueSession { Id = s.Story.NextDialogueSessionId++, EventId = "HU01_offer", Pending = ids.ToList(), Lines = { new DialogueLine { LineId = ids[0] } } };
        s.Story.DialogueLog.Add(open);
        Assert.That(E.AcceptQuest(s, quest).Reason, Is.EqualTo(GameEngine.DialogueLockedReason));
        s.Story.DialogueLog.Remove(open);
        AssertUntouched(s, npc, quest, sessions);

        s.Progress.Flags.Remove("hub_registered");
        Assert.That(E.AcceptQuest(s, quest).Ok, Is.False, "前置不足");
        s.Progress.Flags.Add("hub_registered");
        AssertUntouched(s, npc, quest, sessions);

        E.DebugMapTeleport(s, "h01_gate");
        ReadDialogues(s);
        sessions = s.Story.DialogueLog.Count;
        Assert.That(E.AcceptQuest(s, quest).Ok, Is.False, "不在地点");
        AssertUntouched(s, npc, quest, sessions);
        E.DebugMapTeleport(s, "h02_square");
        ReadDialogues(s);

        var mark = s.Story.DialogueLog.Count;
        Assert.That(E.AcceptQuest(s, quest).Ok, Is.True);
        ReadDialogues(s);
        Assert.That(Since(s, mark), Is.EqualTo(absent ? new[] { Set(npc, "institution_substitute"), quest + "_offer" } : new[] { quest + "_offer" }));
    }

    /// <summary>已接后重复点按钮：同一请求按重复处理，新请求写“已经接取”，都不排对白、不写日志、不改账。</summary>
    [Test]
    public void Q242_02_RepeatButton_NoSecondAccept()
    {
        var s = Registered("h02_square");
        Assert.That(E.AcceptQuest(s, "EXQ07", "r1").Ok, Is.True);
        ReadDialogues(s);
        var sessions = s.Story.DialogueLog.Count;
        var logs = s.Log.Count;
        var snap = (Ledger(s), s.Progress.Flags.Count);
        Assert.That(E.AcceptQuest(s, "EXQ07", "r1").Duplicate, Is.True);
        Assert.That(E.AcceptQuest(s, "EXQ07", "r2").Reason, Is.EqualTo("已经接取"));
        Assert.That(E.DialogueLocked(s), Is.False);
        Assert.That((s.Story.DialogueLog.Count, s.Log.Count), Is.EqualTo((sessions, logs)));
        Assert.That((Ledger(s), s.Progress.Flags.Count), Is.EqualTo(snap));
    }

    /// <summary>交谈后再按钮、按钮后再交谈：接取和回报都只结算一次，接取句、回报句各一段。</summary>
    [TestCase(true)]
    [TestCase(false)]
    public void Q242_02_TalkAndButton_SettleOnce(bool buttonFirst)
    {
        var s = Registered("h02_square");
        if (buttonFirst) Assert.That(E.AcceptQuest(s, "EXQ07", "r1").Ok, Is.True);
        else Talk(s, "npc_xu_an");
        ReadDialogues(s);
        if (buttonFirst) TalkRead(s, "npc_xu_an");
        else Assert.That(E.AcceptQuest(s, "EXQ07", "r2").Reason, Is.EqualTo("已经接取"));
        var snap = Ledger(s);
        GiveClaim(s, "d07_07_unique", "civilian_cache_knowledge");
        TalkRead(s, "npc_xu_an");
        Assert.That(GameEngine.QuestDone(s, "EXQ07"), Is.True);
        Assert.That(E.AcceptQuest(s, "EXQ07", "r3").Reason, Is.EqualTo("这项已经完成"));
        TalkRead(s, "npc_xu_an");
        Assert.That((Plays(s, "EXQ07_offer"), Plays(s, "EXQ07_turnin")), Is.EqualTo((1, 1)));
        Assert.That(Ledger(s), Is.EqualTo(snap));
    }

    /// <summary>其他任务的按钮保持：本人只播接取句、不记首次，下次交谈照常问候；代班不播说明。</summary>
    [TestCase(false)]
    [TestCase(true)]
    public void Q242_02_OtherQuestButton_Unchanged(bool absent)
    {
        const string npc = "npc_tao_cen";
        var s = Registered("h05_appraisal");
        if (absent) E.DebugToggleCityPostAbsent(s);
        var mark = s.Story.DialogueLog.Count;
        Assert.That(E.AcceptQuest(s, "HU01").Ok, Is.True);
        ReadDialogues(s);
        Assert.That(Since(s, mark), Is.EqualTo(new[] { "HU01_offer" }));
        Assert.That(s.City.Greeted, Does.Not.Contain(npc));
        Assert.That(GameEngine.SubstituteIntroShown(s, npc), Is.False);
        if (absent) return;
        TalkRead(s, npc);
        Assert.That((Plays(s, npc, "first_greeting"), Plays(s, npc, "role")), Is.EqualTo((1, 1)));
    }

    // ───────────── DIALOGUE-Q243-01—05：服务结束说明被挡住后的自动补播 ─────────────

    private const string Office = "h05_appraisal";

    private static string OfficeNpc => Cfg.City.Npcs.Single(n => n.LocationId == Office).NpcId;

    private static string OfficeIntro => Set(OfficeNpc, "institution_substitute");

    private static int Deferred(GameState s) => s.City.SubstituteIntroDeferred.Count;

    private void EncounterHere(GameState s)
    {
        var id = E.DebugSpawnEnemy(s, "shambler", s.CurrentLocation).Id;
        s.Encounter = new PendingEncounter { LocationId = s.CurrentLocation, EnemyIds = new List<int> { id }, Source = "explore", Minute = s.GameMinute };
    }

    /// <summary>代班未示的鉴定所：开工后碰上敌踪再取消，原服务结束入口被挡住（鉴定取消是走得到结束钩子的入口）。</summary>
    private GameState BlockedEnd(GameState? office = null)
    {
        var s = office ?? AppraisalOffice();
        if (!s.City.Absent.Contains(OfficeNpc)) E.DebugToggleCityPostAbsent(s);
        Assert.That(E.StartAppraise(s, Sample(s).InstanceId).Ok, Is.True);
        Steps(s, 1);
        EncounterHere(s);
        Assert.That(E.Cancel(s).Ok, Is.True);
        return s;
    }

    /// <summary>从广场实际走到鉴定所（留下可信的来处，撤退能退回广场）。</summary>
    private GameState WalkedIntoOffice()
    {
        var s = Registered("h02_square");
        s.Items.RemoveAll(i => i.ItemId == "fiber");
        E.CreateItems(s, "fiber", 2, ItemInstance.Carried);
        E.DebugUnlockEdge(s, "h02_square__h05_appraisal");
        Start(s, ActionRequest.Travel(Office));
        Run(s);
        ReadDialogues(s);
        Assert.That(s.CurrentLocation, Is.EqualTo(Office));
        return s;
    }

    private void Walk(GameState s, string to)
    {
        E.DebugUnlockEdge(s, "h02_square__h05_appraisal");
        Start(s, ActionRequest.Travel(to));
        Run(s);
        ReadDialogues(s);
        Assert.That(s.CurrentLocation, Is.EqualTo(to));
    }

    private void WinBattle(GameState s)
    {
        var kill = typeof(GameEngine).GetMethod("KillEnemy", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var check = typeof(GameEngine).GetMethod("CheckVictory", BindingFlags.NonPublic | BindingFlags.Instance)!;
        foreach (var e in s.Battle!.Enemies.Where(e => e.Alive).ToList()) kill.Invoke(E, new object[] { s, e });
        check.Invoke(E, new object[] { s });
        Assert.That(s.Battle, Is.Null);
    }

    /// <summary>说明已经自动开始显示：记已示、待补播已清、只有一段，读完后边界再跑也不多弹。</summary>
    private void AssertAutoShownOnce(GameState s)
    {
        Assert.That(Base(E.ActiveDialogue(s)!), Is.EqualTo(OfficeIntro), "不用再点入口，说明已经开始显示");
        Assert.That(GameEngine.SubstituteIntroShown(s, OfficeNpc), Is.True, "开始显示时记已示");
        Assert.That(Deferred(s), Is.Zero);
        ReadDialogues(s);
        E.UpdateFlags(s);
        E.SubstituteIntroBoundary(s);
        Assert.That(E.DialogueLocked(s), Is.False, "重复回调不多弹");
        Assert.That(Plays(s, OfficeIntro), Is.EqualTo(1));
    }

    /// <summary>被挡住：不播、不记已示，只记一条待补播，绑定本局、岗位、原地点和身份键；重复回调不记已示。</summary>
    [Test]
    public void Q243_01_BlockedEnd_RecordsOnly_NotShown()
    {
        var s = BlockedEnd();
        Assert.That(E.DialogueLocked(s), Is.False);
        Assert.That(Plays(s, OfficeIntro), Is.Zero);
        Assert.That(GameEngine.SubstituteIntroShown(s, OfficeNpc), Is.False);
        var r = s.City.SubstituteIntroDeferred.Single();
        Assert.That((r.CampaignId, r.NpcId, r.LocationId, r.IdentityKey), Is.EqualTo((GameEngine.CampaignIdOf(s), OfficeNpc, Office, GameEngine.SubstituteIntroKey(s, OfficeNpc))));
        E.UpdateFlags(s);
        E.SubstituteIntroBoundary(s);
        Assert.That(GameEngine.SubstituteIntroShown(s, OfficeNpc), Is.False, "敌踪还在：重复回调不显示、不记已示");
        Assert.That(Deferred(s), Is.EqualTo(1));
    }

    /// <summary>敌踪绕开后：绕行结束那一段自动显示一次；取消日志只有一条，样本不扣；留在当地未接触的敌人不算显示锁。</summary>
    [Test]
    public void Q243_01_Bypass_AutoShowsOnce_ServiceNotResettled()
    {
        var s = BlockedEnd();
        var cancels = s.Log.Count(l => l.Text.Contains("详细鉴定") || l.Text.Contains("鉴定"));
        var fiber = Count(s, "fiber");
        var completed = s.City.Completed;
        Start(s, ActionRequest.Bypass());
        Run(s);
        Assert.That(s.Main, Is.Null);
        Assert.That(E.LocalHostiles(s, Office), Is.Not.Empty, "未接触的游荡敌人不另加为显示锁");
        AssertAutoShownOnce(s);
        Assert.That(s.Log.Count(l => l.Text.Contains("详细鉴定") || l.Text.Contains("鉴定")), Is.EqualTo(cancels), "原服务不再结算、不再写日志");
        Assert.That((Count(s, "fiber"), s.City.Completed), Is.EqualTo((fiber, completed)));
    }

    /// <summary>战斗后：胜利结算的同一步自动显示一次。</summary>
    [Test]
    public void Q243_01_BattleWon_AutoShowsOnce()
    {
        var s = BlockedEnd();
        Assert.That(E.Engage(s).Ok, Is.True);
        Assert.That(s.Battle, Is.Not.Null);
        E.UpdateFlags(s);
        Assert.That(E.DialogueLocked(s), Is.False, "战斗中不显示");
        WinBattle(s);
        AssertAutoShownOnce(s);
    }

    /// <summary>敌踪自行散去（接敌时名单已空）这条路不走 UpdateFlags，也是边界。</summary>
    [Test]
    public void Q243_01_EncounterGone_AutoShowsOnce()
    {
        var s = BlockedEnd();
        s.WorldEnemies.Clear();
        Assert.That(E.Engage(s).Ok, Is.True);
        Assert.That(s.Encounter, Is.Null);
        AssertAutoShownOnce(s);
    }

    /// <summary>敌踪解除后还有动作锁：不打断，动作结束才显示；绕行段内一直等待。</summary>
    [Test]
    public void Q243_02_ActionLock_Waits_ShowsOnRelease()
    {
        var s = BlockedEnd();
        Start(s, ActionRequest.Bypass());
        Assert.That(s.Encounter, Is.Null);
        E.Advance(s);
        Assert.That(s.Main, Is.Not.Null, "绕行还没走完");
        Assert.That((E.DialogueLocked(s), Deferred(s)), Is.EqualTo((false, 1)));
        Assert.That(GameEngine.SubstituteIntroShown(s, OfficeNpc), Is.False);
        Run(s);
        AssertAutoShownOnce(s);
    }

    /// <summary>取消动作放开锁也算：人还在原地就显示。</summary>
    [Test]
    public void Q243_02_ActionCancelled_StillHere_Shows()
    {
        var s = BlockedEnd();
        s.Encounter = null;
        Start(s, ActionRequest.Wait(30));
        E.Advance(s);
        Assert.That(E.DialogueLocked(s), Is.False, "动作中不打断");
        Assert.That(E.Cancel(s).Ok, Is.True);
        Assert.That(s.CurrentLocation, Is.EqualTo(Office));
        AssertAutoShownOnce(s);
    }

    /// <summary>敌踪解除后还有其他对白：不打断、不插队，那段读完才显示。</summary>
    [Test]
    public void Q243_02_OtherDialogue_NotInterrupted_ShowsAfter()
    {
        var s = BlockedEnd();
        s.Encounter = null;
        Assert.That(E.AcceptQuest(s, "HU01").Ok, Is.True);
        Assert.That(Base(E.ActiveDialogue(s)!), Is.EqualTo("HU01_offer"));
        Assert.That(Deferred(s), Is.EqualTo(1));
        Assert.That(GameEngine.SubstituteIntroShown(s, OfficeNpc), Is.False);
        var line = E.CurrentDialogueLine(s)!;
        while (E.DialogueHasMore(s)) { E.AckDialogue(s, line.LineId); line = E.CurrentDialogueLine(s)!; }
        Assert.That(Plays(s, OfficeIntro), Is.Zero, "末句读完之前不显示");
        E.AckDialogue(s, line.LineId);
        AssertAutoShownOnce(s);
    }

    /// <summary>显示前实际离开：资格取消；回到原地、旧回调都不追播；下一次交谈才显示，且只一次。</summary>
    [Test]
    public void Q243_02_Left_Cancelled_ReturnNoReplay_NextTalkShows()
    {
        var s = BlockedEnd();
        s.Encounter = null;
        Walk(s, "h02_square");
        Assert.That(Deferred(s), Is.Zero, "到达别处就取消");
        Assert.That(E.DialogueLocked(s), Is.False, "不跨地点追播");
        Walk(s, Office);
        E.UpdateFlags(s);
        E.SubstituteIntroBoundary(s);
        Assert.That(E.DialogueLocked(s), Is.False, "回来不恢复");
        Assert.That(GameEngine.SubstituteIntroShown(s, OfficeNpc), Is.False, "取消的是自动资格，身份仍未示");
        TalkRead(s, OfficeNpc);
        Assert.That(Plays(s, OfficeIntro), Is.EqualTo(1));
    }

    /// <summary>战斗撤退退回别处也算离开：不在落点显示，回来也不追播。</summary>
    [Test]
    public void Q243_02_RetreatAway_Cancelled()
    {
        var s = BlockedEnd(WalkedIntoOffice());
        Assert.That(E.Engage(s).Ok, Is.True);
        typeof(GameEngine).GetMethod("FinishBattle", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(E, new object[] { s, "retreat", true });
        Assert.That(s.CurrentLocation, Is.EqualTo("h02_square"));
        Assert.That((E.DialogueLocked(s), Deferred(s)), Is.EqualTo((false, 0)));
        Walk(s, Office);
        Assert.That(E.DialogueLocked(s), Is.False);
    }

    /// <summary>本人回岗：取消，不以原人名发言、不写 Greeted；再离岗不恢复；本人首次问候规则不受影响。</summary>
    [Test]
    public void Q243_03_OwnerBack_Cancelled_NoSpeaker_NoGreeted()
    {
        var s = BlockedEnd();
        E.DebugToggleCityPostAbsent(s);
        Assert.That(Deferred(s), Is.Zero);
        E.DebugToggleCityPostAbsent(s);
        s.Encounter = null;
        E.UpdateFlags(s);
        Assert.That(E.DialogueLocked(s), Is.False, "回岗再离岗不恢复");
        Assert.That(Plays(s, OfficeIntro), Is.Zero);
        Assert.That(s.City.Greeted, Does.Not.Contain(OfficeNpc));
        Assert.That(GameEngine.SubstituteIntroShown(s, OfficeNpc), Is.False);
        E.DebugToggleCityPostAbsent(s);
        TalkRead(s, OfficeNpc);
        Assert.That((Plays(s, OfficeNpc, "first_greeting"), Plays(s, OfficeNpc, "role")), Is.EqualTo((1, 1)), "本人首次规则照旧");
    }

    /// <summary>换了代班身份（键不同）：取消原待补播，不把新身份标已示，新身份等下一次合法入口。现版本一岗只有一个身份，用改键模拟。</summary>
    [Test]
    public void Q243_03_IdentityChanged_Cancelled_NewIdentityNotMarked()
    {
        var s = BlockedEnd();
        s.City.SubstituteIntroDeferred.Single().IdentityKey = "other|" + OfficeNpc;
        s.Encounter = null;
        E.UpdateFlags(s);
        Assert.That((E.DialogueLocked(s), Deferred(s)), Is.EqualTo((false, 0)));
        Assert.That(GameEngine.SubstituteIntroShown(s, OfficeNpc), Is.False);
        Assert.That(s.City.SubstituteIntroShown, Is.Empty);
        TalkRead(s, OfficeNpc);
        Assert.That(Plays(s, OfficeIntro), Is.EqualTo(1));
    }

    /// <summary>玩家另一入口抢先触发同身份说明：只显示一份、开始显示时清待补播；EXQ 专属接取句照原顺序保留。</summary>
    [TestCase(true)]
    [TestCase(false)]
    public void Q243_03_OtherEntryFirst_OneCopy_ExqLineKept(bool talk)
    {
        const string npc = "npc_xu_an";
        var s = Registered("h02_square");
        E.DebugToggleCityPostAbsent(s);
        s.City.SubstituteIntroDeferred.Add(new SubstituteIntroDeferral { CampaignId = GameEngine.CampaignIdOf(s), NpcId = npc, LocationId = "h02_square", IdentityKey = GameEngine.SubstituteIntroKey(s, npc) });
        var mark = s.Story.DialogueLog.Count;
        Assert.That((talk ? E.TalkNpc(s, npc) : E.AcceptQuest(s, "EXQ07")).Ok, Is.True);
        Assert.That(Deferred(s), Is.Zero, "实际打开时清待补播");
        ReadDialogues(s);
        E.UpdateFlags(s);
        Assert.That(Since(s, mark), Is.EqualTo(new[] { Set(npc, "institution_substitute"), "EXQ07_offer" }));
        Assert.That(s.City.Greeted, Does.Not.Contain(npc));
    }

    /// <summary>读档：待补播随存档；敌踪还在就等，绕开后才显示，显示时才记已示。</summary>
    [Test]
    public void Q243_04_Reload_RevalidatesLock_ShowsOnStart()
    {
        var s = SaveSerializer.Deserialize(SaveSerializer.Serialize(BlockedEnd()), Cfg, out _);
        Assert.That(Deferred(s), Is.EqualTo(1));
        Assert.That(E.SubstituteIntroBoundary(s), Is.False, "敌踪还在");
        Assert.That(GameEngine.SubstituteIntroShown(s, OfficeNpc), Is.False);
        Start(s, ActionRequest.Bypass());
        var mid = SaveSerializer.Deserialize(SaveSerializer.Serialize(s), Cfg, out _);
        Assert.That(Deferred(mid), Is.EqualTo(1), "行动中读档仍等待");
        Run(mid);
        AssertAutoShownOnce(mid);
    }

    /// <summary>读回时已安全：读档这个边界就显示；不在原地、别的本局、已回岗的记录不算可信，丢弃不显示。</summary>
    [TestCase("ok")]
    [TestCase("left")]
    [TestCase("campaign")]
    [TestCase("owner")]
    public void Q243_04_ReloadIdle_RevalidatesRecord(string kind)
    {
        var s = BlockedEnd();
        s.Encounter = null;
        var j = JObject.Parse(SaveSerializer.Serialize(s));
        if (kind == "left") j["CurrentLocation"] = "h02_square";
        if (kind == "campaign") j["City"]!["SubstituteIntroDeferred"]![0]!["CampaignId"] = "1";
        if (kind == "owner") j["City"]!["Absent"] = new JArray();
        var m = SaveSerializer.Deserialize(j.ToString(), Cfg, out _);
        Assert.That(E.SubstituteIntroBoundary(m), Is.True);
        Assert.That(Deferred(m), Is.Zero);
        Assert.That(E.DialogueLocked(m), Is.EqualTo(kind == "ok"));
        Assert.That(GameEngine.SubstituteIntroShown(m, OfficeNpc), Is.EqualTo(kind == "ok"));
    }

    /// <summary>线上试玩路线的存档写法（开发计划 B26a.5）：代班未示的岗位，手写敌踪和待补播，读档后绕开或接敌都自动显示一次。</summary>
    [TestCase(true)]
    [TestCase(false)]
    public void Q243_04_PlaytestRoute_HandWrittenSave(bool bypass)
    {
        var s = AppraisalOffice();
        E.DebugToggleCityPostAbsent(s);
        var j = JObject.Parse(SaveSerializer.Serialize(s));
        var seed = (string)j["Seed"]!;
        j["Encounter"] = JObject.FromObject(new { LocationId = Office, EnemyIds = new int[0], Source = "explore", Minute = (int)j["GameMinute"]! });
        j["City"]!["SubstituteIntroDeferred"] = JArray.FromObject(new[] { new { CampaignId = seed, NpcId = OfficeNpc, LocationId = Office, IdentityKey = seed + "|" + OfficeNpc } });
        var m = SaveSerializer.Deserialize(j.ToString(), Cfg, out _);
        Assert.That(E.SubstituteIntroBoundary(m), Is.False);
        Assert.That(Deferred(m), Is.EqualTo(1));
        if (bypass)
        {
            Start(m, ActionRequest.Bypass());
            Run(m);
        }
        else Assert.That(E.Engage(m).Ok, Is.True);
        AssertAutoShownOnce(m);
    }

    /// <summary>显示中和已示的档重载不重播；已取消的档重载、回到原地也不恢复。</summary>
    [Test]
    public void Q243_04_ShownOrCancelled_SaveKeepsIt()
    {
        var s = BlockedEnd();
        s.WorldEnemies.Clear();
        E.Engage(s);
        var shown = SaveSerializer.Deserialize(SaveSerializer.Serialize(s), Cfg, out _);
        Assert.That(GameEngine.SubstituteIntroShown(shown, OfficeNpc), Is.True);
        ReadDialogues(shown);
        var again = SaveSerializer.Deserialize(SaveSerializer.Serialize(shown), Cfg, out _);
        E.SubstituteIntroBoundary(again);
        Assert.That(E.DialogueLocked(again), Is.False);
        Assert.That(Plays(again, OfficeIntro), Is.EqualTo(1));

        var c = BlockedEnd();
        c.Encounter = null;
        Walk(c, "h02_square");
        var away = SaveSerializer.Deserialize(SaveSerializer.Serialize(c), Cfg, out _);
        Walk(away, Office);
        Assert.That((E.DialogueLocked(away), Deferred(away)), Is.EqualTo((false, 0)), "已取消的资格随存档保留");
        Assert.That(GameEngine.SubstituteIntroShown(away, OfficeNpc), Is.False);
    }

    /// <summary>旧档没有待补播字段：不按 Talks、服务日志或未示猜造；等下一次合法入口。</summary>
    [TestCase(true)]
    [TestCase(false)]
    public void Q243_04_OldSave_NoGuess(bool from41)
    {
        var s = BlockedEnd();
        s.Encounter = null;
        s.City.Talks[OfficeNpc] = 3;
        Action<JObject> strip = j => ((JObject)j["City"]!).Remove("SubstituteIntroDeferred");
        GameState m;
        if (from41) m = Upgrade41(s, strip);
        else
        {
            var j = JObject.Parse(SaveSerializer.Serialize(s));
            strip(j);
            m = SaveSerializer.Deserialize(j.ToString(), Cfg, out _);
        }
        Assert.That(Deferred(m), Is.Zero);
        Assert.That(E.SubstituteIntroBoundary(m), Is.False);
        E.UpdateFlags(m);
        Assert.That(E.DialogueLocked(m), Is.False);
        Assert.That(GameEngine.SubstituteIntroShown(m, OfficeNpc), Is.False);
        Assert.That(m.SchemaVersion, Is.EqualTo(GameState.CurrentSchema));
        TalkRead(m, OfficeNpc);
        Assert.That(Plays(m, OfficeIntro), Is.EqualTo(1));
    }

    /// <summary>Clone 带上待补播且互不影响（预览用副本读完对白不动本体）。</summary>
    [Test]
    public void Q243_04_Clone_CopiesDeferred()
    {
        var s = BlockedEnd();
        var c = s.Clone();
        Assert.That(Deferred(c), Is.EqualTo(1));
        c.City.SubstituteIntroDeferred.Single().IdentityKey = "x";
        c.City.SubstituteIntroDeferred.Clear();
        Assert.That(s.City.SubstituteIntroDeferred.Single().IdentityKey, Is.EqualTo(GameEngine.SubstituteIntroKey(s, OfficeNpc)));
    }

    /// <summary>同键合并：同一身份被挡两次也只有一条。</summary>
    [Test]
    public void Q243_05_SameKey_Merged()
    {
        var s = BlockedEnd();
        s.Encounter = null;
        var hold = s.City.SubstituteIntroDeferred.ToList();
        s.City.SubstituteIntroDeferred.Clear();
        var again = E.StartAppraise(s, First(s, "active_substance_white_unidentified").InstanceId);
        Assert.That(again.Ok, Is.True, again.Reason);
        s.City.SubstituteIntroDeferred.AddRange(hold);
        Steps(s, 1);
        EncounterHere(s);
        Assert.That(E.Cancel(s).Ok, Is.True);
        Assert.That(Deferred(s), Is.EqualTo(1));
    }

    /// <summary>已示、本人在岗、岗位不存在、已不在原地点：被挡住也不新建待补播；本人的结束句照旧不播。</summary>
    [TestCase("shown")]
    [TestCase("owner")]
    [TestCase("missing")]
    [TestCase("elsewhere")]
    public void Q243_05_NoRecord_WhenEntryWouldNotShow(string kind)
    {
        var s = AppraisalOffice();
        if (kind != "owner") E.DebugToggleCityPostAbsent(s);
        if (kind == "shown") s.City.SubstituteIntroShown.Add(GameEngine.SubstituteIntroKey(s, OfficeNpc));
        Assert.That(E.StartAppraise(s, Sample(s).InstanceId).Ok, Is.True);
        Steps(s, 1);
        if (kind == "missing") s.City.Pending!.NpcId = "npc_none";
        EncounterHere(s);
        if (kind == "elsewhere") E.DebugTeleport(s, "h02_square");
        var mark = s.Story.DialogueLog.Count;
        Assert.That(E.Cancel(s).Ok, Is.True);
        Assert.That(Deferred(s), Is.Zero);
        Assert.That(Since(s, mark), Is.Empty);
    }

    /// <summary>原无结束钩子的鉴定成功：说明未示也不新建待补播（Q177）。</summary>
    [Test]
    public void Q243_05_AppraisalSuccess_NoRecord()
    {
        var s = AppraisalOffice();
        E.DebugToggleCityPostAbsent(s);
        Assert.That(E.StartAppraise(s, Sample(s).InstanceId).Ok, Is.True);
        Run(s);
        Assert.That((E.DialogueLocked(s), Deferred(s)), Is.EqualTo((false, 0)));
    }

    /// <summary>主角死亡：不显示说明，也不记已示。</summary>
    [Test]
    public void Q243_05_Dead_NotShown()
    {
        var s = BlockedEnd();
        s.Encounter = null;
        s.Progress.Dead = true;
        E.UpdateFlags(s);
        E.SubstituteIntroBoundary(s);
        Assert.That(Plays(s, OfficeIntro), Is.Zero);
        Assert.That(GameEngine.SubstituteIntroShown(s, OfficeNpc), Is.False);
    }

    /// <summary>补播 0 分钟 0 结算：物资、经验、容量、时间、身体、配额、任务都不动；原组名和普通闲谈组不受影响，回看不改已示。</summary>
    [Test]
    public void Q243_05_ZeroCost_TitlesAndLedgerKept()
    {
        var s = BlockedEnd();
        s.WorldEnemies.Clear();
        var snap = Ledger(s);
        var flags = s.Progress.Flags.Count;
        var quests = s.City.Completed;
        var greeted = s.City.Greeted.Count;
        var title = E.DialogueSetTitle(OfficeIntro);
        Assert.That(E.Engage(s).Ok, Is.True);
        ReadDialogues(s);
        Assert.That(Ledger(s), Is.EqualTo(snap));
        Assert.That((s.Progress.Flags.Count, s.City.Completed, s.City.Greeted.Count), Is.EqualTo((flags, quests, greeted)));
        Assert.That(E.DialogueSetTitle(OfficeIntro), Is.EqualTo(title));
        var history = E.DialogueHistory(s).Count;
        TalkRead(s, OfficeNpc);
        Assert.That(Plays(s, OfficeIntro), Is.EqualTo(1), "说明已示，之后交谈只走 Q236");
        Assert.That(E.DialogueHistory(s).Count, Is.EqualTo(history + 1));
        Assert.That(E.DialogueSetTitle(Set(OfficeNpc, "substitute_idle")), Does.EndWith("：代班闲谈"), "Q239 组名保持");
    }

    // ───────────── B26b：DIALOGUE-Q244-01—03、DIALOGUE-Q245-01—04（接续说明组名与首句定稿） ─────────────

    private static readonly JObject Q244 = Design("design.substitute-introduction.json");

    private static IEnumerable<JToken> IntroGroups => (JArray)Q244["groups"]!;

    private static JToken IntroOf(string npc) => IntroGroups.Single(g => (string)g["npcId"]! == npc);

    /// <summary>B38 N02：接续说明两句按 lineId/systemLineId 取 N02 新稿（合同原文须是 N02 的 oldText）。</summary>
    private static string IntroText(JToken g) => N02.Line((string)g["lineId"]!, (string)g["text"]!);

    private static string IntroSystem(JToken g) => N02.Line((string)g["systemLineId"]!, (string)g["systemText"]!);

    /// <summary>未示状态下让岗位走一次接续说明（关联任务记完成，只剩说明可播）。</summary>
    private GameState IntroPost(string npcId)
    {
        var n = Cfg.City.Npcs.Single(x => x.NpcId == npcId);
        var s = New();
        s.GameMinute = At(2, 9, 0);
        s.SpawnNextCheck = int.MaxValue;
        s.PatrolNextCheck = int.MaxValue;
        foreach (var q in Cfg.Quests.List.Where(q => q.NpcId == npcId || n.QuestIds.Contains(q.Id))) s.Progress.Flags.Add($"quest:{q.Id}:completed");
        E.DebugMapTeleport(s, n.LocationId);
        ReadDialogues(s);
        s.City.Absent.Add(npcId);
        return s;
    }

    /// <summary>16 组标题逐字等于合同：岗位名加“：岗位接续”，互不相同，不带原人物名；本人与 Q239 的组名不受影响。</summary>
    [Test]
    public void Q244_01_AllGroups_TitleFromContract()
    {
        Assert.That(IntroGroups.Count(), Is.EqualTo(16));
        Assert.That(IntroGroups.Select(g => (string)g["title"]!).Distinct().Count(), Is.EqualTo(16));
        foreach (var g in IntroGroups)
        {
            var n = Cfg.City.Npcs.Single(x => x.NpcId == (string)g["npcId"]!);
            var title = (string)g["title"]!;
            Assert.That(n.States[GameEngine.SubstituteIntroState], Is.EqualTo((string)g["dialogueSetId"]!));
            Assert.That(E.DialogueSetTitle((string)g["dialogueSetId"]!), Is.EqualTo(title));
            Assert.That(title, Is.EqualTo($"{g["postName"]}：岗位接续"));
            Assert.That(title, Does.Not.Contain(n.Name));
            Assert.That(E.DialogueSetTitle(n.States["substitute_idle"]), Is.EqualTo($"{g["postName"]}：代班闲谈"), "Q239 与本组同一岗位名");
        }
    }

    /// <summary>可信组 ID 含 #序号的旧记录按新标题解析，正文快照不动；没有可信组 ID 的不猜组。</summary>
    [Test]
    public void Q244_02_TrustedHistory_NewTitle_TextKept_NoGuess()
    {
        var set = Set("npc_xu_an", GameEngine.SubstituteIntroState);
        var s = Registered("h02_square");
        var old = new DialogueSession { Id = s.Story.NextDialogueSessionId++, EventId = set + "#2", Ended = true, Minute = 77, LocationId = "h02_square" };
        old.Lines.Add(new DialogueLine { LineId = (string)IntroOf("npc_xu_an")["lineId"]!, Order = 1, Speaker = "机构当班接续员", Text = "旧稿原文" });
        s.Story.DialogueLog.Add(old);
        var m = SaveSerializer.Deserialize(SaveSerializer.Serialize(s), Cfg);
        var d = m.Story.DialogueLog.Single(x => x.EventId == set + "#2");
        Assert.That(E.DialogueSetTitle(d.EventId), Is.EqualTo("营区公事务岗：岗位接续"));
        Assert.That((d.Lines.Single().Text, d.Lines.Single().Speaker, d.Minute, d.LocationId), Is.EqualTo(("旧稿原文", "机构当班接续员", (int?)77, "h02_square")));
        Assert.That(E.DialogueSetTitle("legacy_save"), Is.Null, "无可信组 ID 走界面原有兜底");
    }

    /// <summary>读标题不走时间、不动账，不清已示，不重播。</summary>
    [Test]
    public void Q244_03_ReadTitle_NoSideEffect()
    {
        var s = IntroPost("npc_xu_an");
        TalkRead(s, "npc_xu_an");
        var snap = Ledger(s);
        foreach (var d in s.Story.DialogueLog) E.DialogueSetTitle(d.EventId);
        Assert.That(Ledger(s), Is.EqualTo(snap));
        Assert.That(GameEngine.SubstituteIntroShown(s, "npc_xu_an"), Is.True);
        Assert.That(E.DialogueLocked(s), Is.False);
        Assert.That(Plays(s, "npc_xu_an", GameEngine.SubstituteIntroState), Is.EqualTo(1));
    }

    /// <summary>16 岗位实际播放：两句、顺序、lineId 不变，第一句逐字等于合同，第二句是原 system，0 分钟 0 资源，无本人自述。</summary>
    [TestCaseSource(nameof(Posts))]
    public void Q245_01_02_Intro_PlaysFinalText(string npcId)
    {
        var g = IntroOf(npcId);
        var n = Cfg.City.Npcs.Single(x => x.NpcId == npcId);
        var s = IntroPost(npcId);
        var snap = Ledger(s);
        TalkRead(s, npcId);
        var d = s.Story.DialogueLog.Single(x => Base(x) == (string)g["dialogueSetId"]!);
        Assert.That(d.Lines.Select(l => l.LineId), Is.EqualTo(new[] { (string)g["lineId"]!, (string)g["systemLineId"]! }));
        Assert.That(d.Lines[0].Text, Is.EqualTo(IntroText(g)));
        Assert.That(d.Lines[0].Speaker, Is.EqualTo(n.DutySpeaker));
        Assert.That(d.Lines[1].Text, Is.EqualTo(IntroSystem(g)));
        Assert.That(d.Lines[0].Text, Does.Not.StartWith("本岗由我接续"));
        Assert.That(Ledger(s), Is.EqualTo(snap), "说明不新增事务");
    }

    /// <summary>已显示的旧第一句读档后保留并继续，第二句接着播，不重启；已示不清。</summary>
    [Test]
    public void Q245_03_ShownOldLine_Kept_SecondContinues()
    {
        var g = IntroOf("npc_xu_an");
        var s = IntroPost("npc_xu_an");
        Talk(s, "npc_xu_an");
        var cur = E.CurrentDialogueLine(s)!;
        Assert.That(cur.LineId, Is.EqualTo((string)g["lineId"]!));
        cur.Text = "旧稿第一句";
        var m = SaveSerializer.Deserialize(SaveSerializer.Serialize(s), Cfg);
        Assert.That(E.CurrentDialogueLine(m)!.Text, Is.EqualTo("旧稿第一句"), "不倒改、不重启");
        ReadDialogues(m);
        var d = m.Story.DialogueLog.Single(x => Base(x) == (string)g["dialogueSetId"]!);
        Assert.That(d.Lines.Select(l => l.Text), Is.EqualTo(new[] { "旧稿第一句", IntroSystem(g) }));
        Assert.That(GameEngine.SubstituteIntroShown(m, "npc_xu_an"), Is.True);
        TalkRead(m, "npc_xu_an");
        Assert.That(Plays(m, "npc_xu_an", GameEngine.SubstituteIntroState), Is.EqualTo(1), "不补播");
    }

    /// <summary>排在别的对白后面、还没开始显示的接续说明，读档后轮到时用新稿。</summary>
    [Test]
    public void Q245_03_QueuedLine_UsesNewText()
    {
        var s = Registered("h03_workshop");
        Talk(s, "npc_luo_zheng");
        Assert.That(E.DialogueLocked(s), Is.True);
        var set = Set("npc_luo_zheng", GameEngine.SubstituteIntroState);
        var ids = Cfg.Story.Dialogues.Where(d => d.Event == set).Select(d => d.Id).ToList();
        s.Story.DialogueLog.Add(new DialogueSession { Id = s.Story.NextDialogueSessionId++, EventId = set, Pending = ids.ToList() });
        s.Story.PendingDialogues.AddRange(ids);
        var m = SaveSerializer.Deserialize(SaveSerializer.Serialize(s), Cfg);
        ReadDialogues(m);
        var g = IntroOf("npc_luo_zheng");
        Assert.That(m.Story.DialogueLog.Single(x => x.EventId == set).Lines.Select(l => l.Text), Is.EqualTo(new[] { IntroText(g), IntroSystem(g) }));
    }

    /// <summary>Q243 待补播未显示时，读档、安全释放后按新稿自动显示一次。</summary>
    [Test]
    public void Q245_04_Deferred_ShowsNewTextOnce()
    {
        var s = SaveSerializer.Deserialize(SaveSerializer.Serialize(BlockedEnd()), Cfg);
        Assert.That(Deferred(s), Is.EqualTo(1));
        Start(s, ActionRequest.Bypass());
        Run(s);
        AssertAutoShownOnce(s);
        var g = IntroOf(OfficeNpc);
        Assert.That(s.Story.DialogueLog.Single(x => Base(x) == OfficeIntro).Lines.Select(l => l.Text), Is.EqualTo(new[] { IntroText(g), IntroSystem(g) }));
    }
}
