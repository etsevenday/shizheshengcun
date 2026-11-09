using Newtonsoft.Json.Linq;
using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 批次 B14a（R139、R140、R141）：策划答复 Q138 选 A（按这局开局时是否已有序章区分来源，序章后新局升级保留自动抵达）、
/// Q139 选 B（下一真实路段未知/已知可走/已知未开通/未知且不能探索的下一步句与点亮）、Q140 选 B（战外回退主目标时不显示 PRO04 战斗操作提示）。
/// 规则出处：S15/S14/A10「Q138—Q140 来客来源与场景化下一步」、S02/S03/A08/A11「Q138/Q139 序章来源与路线引导」本轮章；
/// 数据 prologue-adjudications.json（legacyPolicy.originClassification、objectivePolicy.travelNextStep、guidancePolicy.nonContextFallback）、
/// prologue-data.json（visitor.runOriginPolicy、objectiveTravelNextStep、guideNonContextFallback）；验收 A10 TUT-Q138-01/02、TUT-Q139-01—03、TUT-Q140-01/02。
/// </summary>
public class B14aSourceSceneTests : BattleTestBase
{
    private const int D5 = 6240;
    private const string ExploreStep = "先探索脚下这个地点，找出往前走的路。";
    private const string NoExploreStep = "先看看地图上已经认出的路和通行条件。";

    private GameState Fresh()
    {
        var s = NewStory();
        ReadDialogues(s);
        return s;
    }

    private GameState Won()
    {
        var s = Fresh();
        E.DebugPrologue(s, "won");
        ReadDialogues(s);
        return s;
    }

    private bool Played(GameState s, string setId) => s.Story.DialogueLog.Any(d => d.EventId == setId);

    /// <summary>把一局存成某个旧版本的存档 JSON（去掉该版本还没有的字段），再走完整读档入口升到当前版本。</summary>
    private GameState Reload(GameState s, int schema, Action<JObject>? edit = null)
    {
        var j = JObject.Parse(SaveSerializer.Serialize(s));
        j["SchemaVersion"] = schema.ToString();
        if (schema < 26) j.Remove("City");
        if (schema < 25 && j["Prologue"] is JObject pj)
            foreach (var k in new[] { "GuideSeen", "GuideActive", "GuideActiveAt", "GuidePending", "GuidePendingAt", "GuideMissed" }) pj.Remove(k);
        if (schema < 22) j.Remove("Prologue");
        edit?.Invoke(j);
        var m = SaveSerializer.Deserialize(j.ToString(), Cfg, out _);
        Assert.That(m.SchemaVersion, Is.EqualTo(GameState.CurrentSchema), $"存档 {schema} 升到当前版本");
        return m;
    }

    /// <summary>首战已胜、交谈得知路线、已在旧公路（route_onward）。</summary>
    private GameState OnRoad()
    {
        var s = Won();
        s.Progress.Flags.Add(GameEngine.FactCampRouteKnown);
        s.Progress.Flags.Add(GameEngine.FactVisitorMet);
        E.DebugMapTeleport(s, "m01_road");
        ReadDialogues(s);
        E.PrologueBoundary(s);
        ReadDialogues(s);
        Assert.That(s.Prologue.ObjectiveId, Is.EqualTo("route_onward"));
        return s;
    }

    // ───────────── TUT-Q138-01 ─────────────

    /// <summary>
    /// TUT-Q138-01：序章后开的新局（存档 22、23、24、25）读档升级后仍是“有序章的新局”：第5日08:00在营地空闲时自动播来客抵达对白，
    /// 接着照常给 PRO16 短句；单看版本升级不改变来源。
    /// </summary>
    [TestCase(22)]
    [TestCase(23)]
    [TestCase(24)]
    [TestCase(25)]
    public void TUT_Q138_01_PrologueEraRun_UpgradeKeepsAutoArrival(int schema)
    {
        var s = Won();
        s.GameMinute = D5 - 60;
        E.PrologueBoundary(s);
        Assert.That(GameEngine.VisitorRegistered(s), Is.False, "第5日08:00之前不登记（原时间条件不变）");
        var m = Reload(s, schema);
        Assert.That(m.Prologue.MigratedAt, Is.Null, "不因升级被记成迁入序章的旧档");
        Assert.That(m.Story.Legacy, Is.False);
        Assert.That(GameEngine.PrePrologueRun(m), Is.False, "序章后开局");
        Assert.That(GameEngine.OptInVisitor(m), Is.False);

        m.GameMinute = D5;
        E.PrologueBoundary(m);
        Assert.That(E.ActiveDialogue(m)?.EventId, Is.EqualTo("visitor_arrival_camp"), "保留原新局自动抵达剧情");
        ReadDialogues(m);
        Assert.That(Played(m, "PRO16_reaction"), Is.True, "保留合法 PRO16 提示资格（饱饱在体内照常播短句）");
        Assert.That(E.CurrentHint(m)!.Value.TaskId, Is.EqualTo("PRO16"));
    }

    /// <summary>
    /// TUT-Q138-01：Legacy、从存档 21 迁入序章的旧档，以及“21 迁入后又在 25 存过”的档（可信来源标记优先于当前 schema 编号）：
    /// 来客登记可见，但不自动弹抵达对白和 PRO16 短句，主动点交谈才接续。
    /// </summary>
    [Test]
    public void TUT_Q138_01_PrePrologueRuns_StayTalkToContinue()
    {
        // 从 schema 21 迁入
        var s = Won();
        s.GameMinute = D5 - 60;
        s.Story.TutorialWonMinute = null;   // schema 21 没有这个字段
        var m21 = Reload(s, 21);
        Assert.That(m21.Prologue.MigratedAt, Is.Not.Null);
        Assert.That(GameEngine.PrePrologueRun(m21), Is.True);

        // 同一份 21 迁入档在存档 25 时存过：MigratedAt 随档保留，升级到 26 仍是旧来源
        var m25 = Reload(m21, 25);
        Assert.That(m25.Prologue.MigratedAt, Is.EqualTo(m21.Prologue.MigratedAt), "可信旧来源标记不随版本升级丢失");
        Assert.That(GameEngine.PrePrologueRun(m25), Is.True, "不能仅因现在版本大于 22 误认作新局");

        var legacy = New();
        Assert.That(GameEngine.PrePrologueRun(legacy), Is.True, "Legacy");

        foreach (var x in new[] { m21, m25, legacy })
        {
            if (x.CurrentLocation != "camp") E.DebugTeleport(x, "camp");
            x.GameMinute = D5 + 30;
            E.PrologueBoundary(x);
            Assert.That(GameEngine.VisitorRegistered(x), Is.True, "原条件满足即登记可见");
            Assert.That(E.DialogueLocked(x), Is.False, "不自动弹抵达对白或 PRO16 短句");
            Assert.That(Played(x, "visitor_arrival_camp") || Played(x, "PRO16_reaction"), Is.False);
            Assert.That(E.NpcsHere(x).Any(n => n.NpcId == "npc_cheng_wang"), Is.True, "人物卡可见");
            Assert.That(E.TalkNpc(x, "npc_cheng_wang").Ok, Is.True, "主动交谈才接续");
            ReadDialogues(x);
            Assert.That(x.Progress.Flags.Contains(GameEngine.FactCampRouteKnown), Is.True);
        }
    }

    // ───────────── TUT-Q138-02 ─────────────

    /// <summary>
    /// TUT-Q138-02：已播抵达、已交谈、已示 PRO16 的新局存档（24/25）升级后状态不清零、不重播，也不补样本/奖励；
    /// 首战未胜的新局到第5日也不登记（原首战条件不变）；Legacy 不因分类补饱饱。
    /// </summary>
    [TestCase(24)]
    [TestCase(25)]
    public void TUT_Q138_02_ShownStatesKept_NoReplay_ConditionsUnchanged(int schema)
    {
        var s = Won();
        s.GameMinute = D5;
        E.PrologueBoundary(s);
        ReadDialogues(s);
        Assert.That(E.TalkNpc(s, "npc_cheng_wang").Ok, Is.True);
        ReadDialogues(s);
        Assert.That(s.Prologue.VisitorArrivalShown, Is.True);
        var talks = s.Prologue.VisitorTalks;
        var metAt = s.Prologue.VisitorMetAt;
        var dialogues = s.Story.DialogueLog.Count;
        var items = s.Items.Sum(i => i.Count);
        var m = Reload(s, schema);
        E.PrologueBoundary(m);
        Assert.That(E.DialogueLocked(m), Is.False, "读档与边界不重播抵达、交谈或 PRO16");
        Assert.That((m.Prologue.VisitorArrivalShown, m.Prologue.VisitorTalks, m.Prologue.VisitorMetAt), Is.EqualTo((true, talks, metAt)), "已播/已交谈不清零");
        Assert.That(m.Prologue.GuideSeen, Does.Contain("PRO16"), "已示的教学不清零");
        Assert.That(m.Progress.Flags.Contains(GameEngine.FactVisitorMet) && m.Progress.Flags.Contains(GameEngine.FactCampRouteKnown), Is.True);
        Assert.That(m.Story.DialogueLog.Count, Is.EqualTo(dialogues), "不补播");
        Assert.That(m.Items.Sum(i => i.Count), Is.EqualTo(items), "不补样本或奖励");

        // 首战未胜的序章新局：到第5日08:00也不登记来客（原条件不变）
        var t = Fresh();
        var tm = Reload(t, schema);
        tm.GameMinute = D5 + 60;
        E.PrologueBoundary(tm);
        Assert.That(GameEngine.VisitorRegistered(tm), Is.False, "首战未胜不登记");

        // Legacy 分类不补饱饱/历史剧情
        var legacy = New();
        var lm = Reload(legacy, schema);
        Assert.That((lm.Story.Legacy, lm.Story.BaobaoPresent), Is.EqualTo((true, false)), "不因分类创建饱饱");
    }

    // ───────────── TUT-Q139-01 ─────────────

    /// <summary>
    /// TUT-Q139-01：前进类主目标的下一真实路段未知、当前地点可探索且未满 100%：保留主目标标题，下一步换成定稿探索句并点亮当前地点（探索入口）；
    /// 不写未知地点名、方向或门槛，也不因展示揭示地图。
    /// </summary>
    [Test]
    public void TUT_Q139_01_UnknownNextSegment_ExploreStep_FocusCurrent()
    {
        var s = OnRoad();
        Assert.That(s.Locations["m02_checkpoint"].Discovered, Is.False);
        Assert.That(s.Locations["m01_road"].Progress, Is.LessThan(100));
        var o = E.CurrentObjective(s)!;
        Assert.That((o.Title, o.Step), Is.EqualTo(("前往幸存者营区寻找医疗帮助", ExploreStep)), "保留标题，替换下一步");
        Assert.That(E.ObjectiveFocus(s), Is.EqualTo("loc:m01_road"), "点亮当前地点探索入口");
        Assert.That(o.Step, Does.Not.Contain("检查哨").And.Not.Contain("营门"), "不泄露未知地点名");
        Assert.That(E.NextTravelSegment(s)!.NodeId, Is.Null, "未知段不给出地点");
        Assert.That(s.Locations["m02_checkpoint"].Discovered, Is.False, "展示不揭示");

        // 出区前的前进类主目标同样适用：路线已知、村庄还没发现、人在荒地且荒地未满 100%
        var w = Won();
        w.Progress.Flags.Add(GameEngine.FactCampRouteKnown);
        w.Progress.Flags.Add(GameEngine.FactVisitorMet);
        E.DebugMapTeleport(w, "field");
        ReadDialogues(w);
        E.DebugSetProgress(w, "field", 50);
        E.PrologueBoundary(w);
        ReadDialogues(w);
        Assert.That(w.Locations["village"].Discovered, Is.False);
        Assert.That(w.Prologue.ObjectiveId, Is.EqualTo("exit_need"));
        o = E.CurrentObjective(w)!;
        Assert.That((o.Title, o.Step), Is.EqualTo(("前往幸存者营区寻找医疗帮助", ExploreStep)));
        Assert.That(E.ObjectiveFocus(w), Is.EqualTo("loc:field"));
        Assert.That(o.Step, Does.Not.Contain("村庄"));
    }

    // ───────────── TUT-Q139-02 ─────────────

    /// <summary>
    /// TUT-Q139-02：路线揭示后恢复真实已知相邻段（“下一段路已开通，前往检查哨。”并指向检查哨）；已知但未开通显示“前往营门还需要…”与相邻栏同一原因；
    /// 下一段仍未知而当前已 100% 时只写“查看当前已知路线与通行条件。”，不点亮、不要求重复探索。
    /// </summary>
    [Test]
    public void TUT_Q139_02_Revealed_KnownBlocked_UnknownNoExplore()
    {
        var s = OnRoad();
        E.DebugSetProgress(s, "m01_road", 100);
        E.PrologueBoundary(s);
        Assert.That(s.Locations["m02_checkpoint"].Discovered, Is.True, "按原发现规则：探索 100% 揭示");
        Assert.That(E.CurrentObjective(s)!.Step, Is.EqualTo("下一段路已开通，前往检查哨。"), "已知且可走");
        Assert.That(E.ObjectiveFocus(s), Is.EqualTo("loc:m02_checkpoint"));

        // 到检查哨：下一段（营门）未知、检查哨本可探索，但 B22f（Q193）起拦路战未胜时探索被禁 → 写“先战胜检查哨拦路敌人。”、不点亮探索入口
        // （开发假设 B22f-2，待确认 Q208）；探索 100% 揭示营门，但门槛未满足 → 已知未开通
        E.DebugMapTeleport(s, "m02_checkpoint");
        ReadDialogues(s);
        E.PrologueBoundary(s);
        Assert.That(E.CurrentObjective(s)!.Step, Is.EqualTo("先战胜检查哨拦路敌人。"));
        Assert.That(E.ObjectiveFocus(s), Is.Null);
        E.DebugSetProgress(s, "m02_checkpoint", 100);
        E.PrologueBoundary(s);
        Assert.That(s.Locations["h01_gate"].Discovered, Is.True);
        var edge = E.EdgeBetween("m02_checkpoint", "h01_gate")!;
        Assert.That(E.EdgeOpen(s, edge), Is.False);
        var reason = E.GateBlockedReason(s, edge);
        Assert.That(reason, Does.StartWith("需要："));
        var step = E.CurrentObjective(s)!.Step;
        Assert.That(step, Is.EqualTo("前往营门还需要" + reason.Substring(3).TrimEnd('。') + "。"), "用相邻栏同一句领域可公开原因，不编造");
        Assert.That(step, Does.Not.Contain("{").And.Not.Contain("需要："));
        Assert.That(E.ObjectiveFocus(s), Is.EqualTo("loc:h01_gate"), "指向已知但未开通的那一段");
        Assert.That(E.AdjacentLocked(s).Any(x => x.Target == "h01_gate" && x.Reason == reason), Is.True, "与相邻栏一致");

        // 下一段仍未知、当前已 100%：只给已知路线查询，不点亮未知目标、不提示反复探索
        var t = OnRoad();
        t.Locations["m01_road"].Progress = 100;   // 已满 100%，而检查哨尚未写入发现
        Assert.That(t.Locations["m02_checkpoint"].Discovered, Is.False);
        Assert.That(E.CurrentObjective(t)!.Step, Is.EqualTo(NoExploreStep));
        Assert.That(E.ObjectiveFocus(t), Is.Null);
        Assert.That(E.CurrentObjective(t)!.Title, Is.EqualTo("前往幸存者营区寻找医疗帮助"));
    }

    // ───────────── TUT-Q139-03 ─────────────

    /// <summary>
    /// TUT-Q139-03：读下一步与点亮 0 分钟、只读——不写发现/到访/探索/门槛/物品/任务；移动途中不提前换成目的地的下一步，到达后才重算。
    /// </summary>
    [Test]
    public void TUT_Q139_03_ReadOnly_ZeroTime_NoEarlySwitchWhileMoving()
    {
        var s = OnRoad();
        var before = SaveSerializer.Serialize(s);
        for (var k = 0; k < 3; k++)
        {
            _ = E.CurrentObjective(s);
            _ = E.ObjectiveFocus(s);
            _ = E.NextTravelSegment(s);
            _ = E.CurrentHint(s);
        }
        Assert.That(SaveSerializer.Serialize(s), Is.EqualTo(before), "0 时间、无任何状态副作用");

        E.DebugSetProgress(s, "m01_road", 100);
        E.PrologueBoundary(s);
        Assert.That(E.CurrentObjective(s)!.Step, Is.EqualTo("下一段路已开通，前往检查哨。"));
        Start(s, ActionRequest.Travel("m02_checkpoint"));
        Assert.That(s.CurrentLocation, Is.EqualTo("m01_road"));
        Assert.That(E.CurrentObjective(s)!.Step, Is.EqualTo("下一段路已开通，前往检查哨。"), "移动中不提前切到目的地");
        Assert.That(s.Locations["m02_checkpoint"].Visited, Is.False);
        Run(s);
        ReadDialogues(s);
        Assert.That(s.CurrentLocation, Is.EqualTo("m02_checkpoint"));
        Assert.That(E.CurrentObjective(s)!.Step, Is.EqualTo("先战胜检查哨拦路敌人。"), "成功到达后按新位置重算（B22f：m02 拦路战未胜，见开发假设 B22f-2）");
        Assert.That(s.Locations["h01_gate"].Discovered, Is.False, "不自动探索或揭示");
    }

    // ───────────── TUT-Q140-01 ─────────────

    /// <summary>
    /// TUT-Q140-01：战斗外没有有效上下文、主目标是首战（PRO04）：任务框只有标题与下一步，没有战斗操作提示（营地空闲、休息中、战前敌踪都算战外）；
    /// 有效的食饮/恢复/制作上下文仍优先；其他普通主目标（如 PRO03）的战外回退提示不受影响。
    /// </summary>
    [Test]
    public void TUT_Q140_01_OutOfBattle_NoPro04CombatHint_ContextStillFirst()
    {
        var s = Fresh();
        Assert.That(s.Prologue.ObjectiveId, Is.EqualTo("PRO04"));
        Assert.That(E.CurrentHint(s)!.Value.TaskId, Is.EqualTo("PRO01"), "开场后的有效上下文照常");
        E.DismissHint(s, "PRO01");
        var o = E.CurrentObjective(s)!;
        Assert.That((o.Title, o.Step), Is.EqualTo(("第一次交手", "处理农野荒地的游荡尸，战斗结束时活着。")), "标题与下一步照常");
        Assert.That(E.CurrentHint(s), Is.Null, "无上下文、战斗外：不显示“先选技能、悬停合法目标…”");
        Assert.That(E.DialogueLocked(s), Is.False, "不为补提示自动弹角色对白");

        // 有效上下文优先
        Assert.That(E.GuideContext(s, "item", "canned_food", s.CurrentLocation), Is.True);
        Assert.That(E.CurrentHint(s)!.Value.TaskId, Is.EqualTo("PRO02"), "食饮上下文");
        ReadDialogues(s);
        Assert.That(E.GuideContext(s, "rest"), Is.True);
        Assert.That(E.CurrentHint(s)!.Value.TaskId, Is.EqualTo("PRO12"), "恢复上下文");
        ReadDialogues(s);
        Assert.That(E.GuideContext(s, "recipe", "knife"), Is.True);
        Assert.That(E.CurrentHint(s)!.Value.TaskId, Is.EqualTo("PRO03"), "制作上下文");
        ReadDialogues(s);

        // 休息中、战前敌踪：都不算战内
        var r = Fresh();
        E.DismissHint(r, "PRO01");
        Start(r, ActionRequest.Wait(30));
        Assert.That(E.CurrentHint(r)?.TaskId, Is.Not.EqualTo("PRO04"), "休息/等待中");
        Run(r);
        r.Encounter = new PendingEncounter { LocationId = r.CurrentLocation, EnemyIds = new List<int>(), Source = "explore", Minute = r.GameMinute };
        Assert.That(E.CurrentHint(r)?.TaskId, Is.Not.EqualTo("PRO04"), "战前敌踪预览");
        Assert.That(E.BattleHint(r), Is.Null);
        r.Encounter = null;

        // 其他普通主目标的战外回退提示不受影响
        var w = Won();
        foreach (var id in new[] { "PRO01", "PRO02" }) w.Prologue.Tasks.TryAdd(id, new PrologueTaskRecord { CompletedAt = w.GameMinute, EventId = "test" });
        w.Prologue.GuideActive = "";
        E.PrologueBoundary(w);
        ReadDialogues(w);
        w.Prologue.GuideActive = "";
        Assert.That(w.Prologue.ObjectiveId, Is.EqualTo("PRO03"));
        Assert.That(E.CurrentHint(w)!.Value.TaskId, Is.EqualTo("PRO03"), "PRO03 武器装备这类战外教学照常");
    }

    // ───────────── TUT-Q140-02 ─────────────

    /// <summary>
    /// TUT-Q140-02：真正首战建立后按原时机（警告之后、首次行动结算前）在战内给 PRO04 指导；不加 AP/时间/动作；
    /// 离开/结束战斗后战内说明不残留，已示状态不清零、不重播。
    /// </summary>
    [Test]
    public void TUT_Q140_02_InBattleOnly_NoResidueAfterLeaving()
    {
        var s = Fresh();
        E.DismissHint(s, "PRO01");
        Assert.That(E.CurrentHint(s), Is.Null);
        Start(s, ActionRequest.Travel("field"));
        Run(s);
        Assert.That(s.Battle?.Tutorial, Is.True);
        var ap = s.Battle!.PlayerAp;
        var minute = s.GameMinute;
        var events = new List<string>();
        for (var k = 0; k < 10 && E.DialogueLocked(s); k++)
        {
            if (!events.Contains(E.ActiveDialogue(s)!.EventId)) events.Add(E.ActiveDialogue(s)!.EventId);
            E.AckDialogue(s, E.CurrentDialogueLine(s)!.LineId);
        }
        Assert.That(events, Is.EqualTo(new[] { "tutorial_arrive", "PRO04_reaction" }), "原警告在前，首战内指导");
        Assert.That(E.BattleHint(s)!.Value.Text, Is.EqualTo("先选技能，悬停合法目标看预测，再点目标提交；也可查看防御和结束行动。"));
        Assert.That(E.CurrentHint(s)!.Value.TaskId, Is.EqualTo("PRO04"), "实际首战内");
        Assert.That((s.Battle.PlayerAp, s.GameMinute), Is.EqualTo((ap, minute)), "不加 AP、不推进时间");

        for (var k = 0; k < 20 && s.Battle != null; k++)
        {
            if (E.DialogueLocked(s)) { ReadDialogues(s); continue; }
            if (!E.AwaitingPlayer(s)) { E.BattleStep(s); continue; }
            s.Battle.Enemies[0].Hp = 0.01;
            Force(s, Below(0.2), AtLeast(0.6), Any());
            Cmd(s, "basic_attack", s.Battle.Enemies[0].Index);
        }
        ReadDialogues(s);
        Assert.That(s.Battle, Is.Null);
        Assert.That(E.BattleHint(s), Is.Null, "战后不残留");
        Assert.That(E.CurrentHint(s)?.TaskId, Is.Not.EqualTo("PRO04"));

        // 首战未打完就离开（战斗不在了而 PRO04 仍是当前上下文）：战外不残留战斗说明，已示状态保留、不重播
        var t = Fresh();
        Start(t, ActionRequest.Travel("field"));
        Run(t);
        ReadDialogues(t);
        Assert.That(t.Prologue.GuideActive, Is.EqualTo("PRO04"));
        t.Battle = null;   // 模拟已离开战斗（PRO04 未完成）
        Assert.That(GameEngine.TaskDone(t, "PRO04"), Is.False);
        Assert.That(E.CurrentHint(t), Is.Null, "离战后任务框不显示 PRO04 战斗提示");
        Assert.That(E.BattleHint(t), Is.Null);
        Assert.That(t.Prologue.GuideSeen, Does.Contain("PRO04"), "已示状态不清零");
        E.PrologueBoundary(t);
        Assert.That(E.DialogueLocked(t), Is.False, "不重播已示短句");
    }

    /// <summary>B14a 数据同源：Q139 四句与 Q140 战斗类任务表从 prologue-adjudications.json 导入（逐字；四句 B38 起为 N02 新稿）。</summary>
    [Test]
    public void B14a_Config_FromPlannerData()
    {
        var ts = Cfg.Prologue.TravelStep;
        Assert.That((ts.Explore, ts.KnownOpen, ts.KnownBlocked, ts.UnknownNoExplore), Is.EqualTo((
            ExploreStep, "下一段路已开通，前往{实际下一相邻地点名}。", "前往{已发现地点名}还需要{领域可公开原因}。", NoExploreStep)));
        Assert.That(Cfg.Prologue.BattleOnlyHintTaskIds, Is.EqualTo(new[] { "PRO04" }));
    }
}
