using Newtonsoft.Json.Linq;
using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 批次 B13d（R132、R134）：策划答复 Q128 选 C（18 项操作前引导、完成后简短反馈）、Q129 选 B（阶段/特殊目标/下一步正式短句）、
/// Q131 选 B（PRO18 实际到达旧公路即完成，路标可选）与存档 25 迁移（S15/A10 Q128—Q133 本轮章、prologue-adjudications.json；
/// 验收 TUT-Q128-01—05、TUT-Q129-01/02、TUT-Q131-01、TUT-Q133-03）。
/// </summary>
public class B13dGuideTests : BattleTestBase
{
    private const int D5 = 6240;

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

    private void Travel(GameState s, string to)
    {
        Start(s, ActionRequest.Travel(to));
        Assert.That(Run(s), Is.EqualTo(SegmentStatus.Completed));
        ReadDialogues(s);
        Assert.That(s.CurrentLocation, Is.EqualTo(to));
    }

    private bool Played(GameState s, string setId) => s.Story.DialogueLog.Any(d => d.EventId == setId);

    // ───────────── TUT-Q128-01 ─────────────

    /// <summary>TUT-Q128-01：18 项都有操作前提示、角色短句与完成短反馈；开场读完后先给 PRO01 引导与短句；任务完成不排、不补播短句。</summary>
    [Test]
    public void TUT_Q128_01_GuideBeforeAction_NoReplayAfterCompletion()
    {
        foreach (var t in Cfg.Prologue.Tasks)
        {
            Assert.That(t.Hint, Is.Not.Empty, t.Id);
            Assert.That(t.Done, Is.Not.Empty, t.Id);
            Assert.That(Cfg.Story.Dialogues.Where(d => d.Event == t.ReactionSet).Select(d => d.Speaker), Is.EqualTo(new[] { "饱饱", "你" }), t.Id);
        }
        Assert.That(Cfg.Prologue.Tasks.Single(t => t.Id == "PRO01").Hint, Is.EqualTo("选中地面或随身的一张物资卡，查看操作和预估；只看不耗时。"));
        Assert.That(Cfg.Prologue.Tasks.Single(t => t.Id == "PRO01").Done, Is.EqualTo("已查看物资操作。"));

        var s = NewStory();
        var m0 = s.GameMinute;
        ReadDialogues(s);
        Assert.That(Played(s, "PRO01_reaction"), Is.True, "开场读完后的首次可操作界面，在首次选卡之前");
        Assert.That(s.Prologue.GuideSeen, Does.Contain("PRO01"));
        Assert.That(E.CurrentHint(s)!.Value.Text, Is.EqualTo("选中地面或随身的一张物资卡，查看操作和预估；只看不耗时。"));
        Assert.That(s.GameMinute, Is.EqualTo(m0), "引导 0 分钟");

        // PRO11 不经任何引导上下文直接做完：只记完成与短反馈，不补播它的短句
        var water = s.Items.First(i => i.ItemId == "clean_water" && i.Owner == "camp");
        Assert.That(E.DoInstant(s, InstantRequest.Pickup(water.InstanceId, 1)).Ok, Is.True);
        Assert.That(GameEngine.TaskDone(s, "PRO11"), Is.True);
        Assert.That(s.Prologue.LastDoneTask, Is.EqualTo("PRO11"));
        Assert.That(E.DialogueLocked(s), Is.False, "完成后不排短句");
        Assert.That(Played(s, "PRO11_reaction"), Is.False);
        Assert.That(s.Prologue.PendingReactions, Is.Empty);
        Assert.That(E.GuideContext(s, "item", "clean_water", ItemInstance.Carried), Is.True, "同卡另有 PRO02 相关");
        Assert.That(s.Prologue.GuideActive, Is.EqualTo("PRO02"), "已完成的 PRO11 不再作为引导");
        ReadDialogues(s);
        Assert.That(Played(s, "PRO11_reaction"), Is.False, "已完成项永不补播");
    }

    // ───────────── TUT-Q128-02 ─────────────

    /// <summary>TUT-Q128-02：主目标仍是首战时，打开恢复/制作、选中食物等也在提交前得到对应提示；同屏只一条，同上下文按编号先给未示的一项。</summary>
    [Test]
    public void TUT_Q128_02_ContextHintBeforeSubmit_EvenWhenObjectiveIsBattle()
    {
        var s = Fresh();
        var m0 = s.GameMinute;
        var items = s.Items.Sum(i => i.Count);
        Assert.That(s.Prologue.ObjectiveId, Is.EqualTo("PRO04"));

        Assert.That(E.GuideContext(s, "rest"), Is.True);
        Assert.That(E.CurrentHint(s)!.Value.TaskId, Is.EqualTo("PRO12"), "上下文优先于主目标");
        Assert.That(E.ActiveDialogue(s)!.EventId, Is.EqualTo("PRO12_reaction"));
        ReadDialogues(s);
        Assert.That(E.GuideContext(s, "rest"), Is.False, "同一项不重复弹");

        Assert.That(E.GuideContext(s, "recipe", "knife"), Is.True);
        Assert.That(E.CurrentHint(s)!.Value.TaskId, Is.EqualTo("PRO03"), "石刀：PRO03 与 PRO10 都相关，先编号小的");
        ReadDialogues(s);
        Assert.That(E.GuideContext(s, "recipe", "knife"), Is.True);
        Assert.That(E.CurrentHint(s)!.Value.TaskId, Is.EqualTo("PRO10"), "其余在之后新的实际上下文给");
        ReadDialogues(s);

        Assert.That(E.GuideContext(s, "item", "canned_food", s.CurrentLocation), Is.True);
        Assert.That(E.CurrentHint(s)!.Value.TaskId, Is.EqualTo("PRO02"));
        ReadDialogues(s);
        Assert.That(E.GuideContext(s, "fire"), Is.True);
        Assert.That(E.CurrentHint(s)!.Value.TaskId, Is.EqualTo("PRO07"));
        ReadDialogues(s);
        Assert.That(E.GuideContext(s, "process", "boil_water"), Is.True);
        Assert.That(E.CurrentHint(s)!.Value.TaskId, Is.EqualTo("PRO08"));
        ReadDialogues(s);
        Assert.That(E.GuideContext(s, "recipe", "shelter"), Is.True, "在营地打开遮蔽棚预览");
        Assert.That(E.CurrentHint(s)!.Value.TaskId, Is.EqualTo("PRO13"));
        ReadDialogues(s);

        Assert.That(s.GameMinute, Is.EqualTo(m0), "提示与短句 0 分钟");
        Assert.That(s.Items.Sum(i => i.Count), Is.EqualTo(items), "不动物资");
        Assert.That(new[] { "PRO02", "PRO03", "PRO07", "PRO08", "PRO10", "PRO12", "PRO13" }.Any(id => GameEngine.TaskDone(s, id)), Is.False, "看提示不算完成");
        Assert.That(s.Prologue.ObjectiveId, Is.EqualTo("PRO04"), "主目标不跟着跳");
    }

    // ───────────── TUT-Q128-03 ─────────────

    /// <summary>TUT-Q128-03：首战原警告之后、首次行动结算之前给选技能/目标预测指引（战内提示）；不给 AP、不推进时间、不替你行动。</summary>
    [Test]
    public void TUT_Q128_03_FirstBattle_AfterWarning_BeforeFirstAction()
    {
        var s = Fresh();
        Start(s, ActionRequest.Travel("field"));
        Run(s);
        Assert.That(s.Battle?.Tutorial, Is.True);
        var events = new List<string>();
        var ap = s.Battle!.PlayerAp;
        var round = s.Battle.Round;
        var minute = s.GameMinute;
        for (var k = 0; k < 10 && E.DialogueLocked(s); k++)
        {
            if (!events.Contains(E.ActiveDialogue(s)!.EventId)) events.Add(E.ActiveDialogue(s)!.EventId);
            E.AckDialogue(s, E.CurrentDialogueLine(s)!.LineId);
        }
        Assert.That(events, Is.EqualTo(new[] { "tutorial_arrive", "PRO04_reaction" }), "原警告在前");
        Assert.That(E.BattleHint(s)!.Value.Text, Is.EqualTo("先选技能，悬停合法目标看预测，再点目标提交；也可查看防御和结束行动。"));
        Assert.That((s.Battle.PlayerAp, s.Battle.Round, s.GameMinute), Is.EqualTo((ap, round, minute)), "不给 AP、不推进");
        Assert.That(s.Battle.Log.Count(l => l.Contains("伤害")), Is.EqualTo(0), "不替你攻击");
    }

    /// <summary>TUT-Q128-01（PRO05）：首战奖励落地且胜利剧情读完、战斗结束后才给融合引导（在融合之前），不插在胜利剧情里；物质不在则不给。</summary>
    [Test]
    public void TUT_Q128_01_Pro05_AfterVictoryStory_BeforeFusion()
    {
        var s = Fresh();
        Start(s, ActionRequest.Travel("field"));
        Run(s);
        ReadDialogues(s);
        for (var k = 0; k < 20 && s.Battle != null; k++)
        {
            if (E.DialogueLocked(s)) { ReadDialogues(s); continue; }
            if (!E.AwaitingPlayer(s)) { E.BattleStep(s); continue; }
            s.Battle.Enemies[0].Hp = 0.01;
            Force(s, Below(0.2), AtLeast(0.6), Any());
            Cmd(s, "basic_attack", s.Battle.Enemies[0].Index);
        }
        Assert.That(s.Story.TutorialWon, Is.True);
        var order = new List<string>();
        for (var k = 0; k < 20 && E.DialogueLocked(s); k++)
        {
            if (!order.Contains(E.ActiveDialogue(s)!.EventId)) order.Add(E.ActiveDialogue(s)!.EventId);
            E.AckDialogue(s, E.CurrentDialogueLine(s)!.LineId);
        }
        Assert.That(order.IndexOf("PRO05_reaction"), Is.GreaterThan(order.IndexOf("tutorial_won")), "胜利剧情读完之后");
        Assert.That(E.CurrentHint(s)!.Value.TaskId, Is.EqualTo("PRO05"));
        Assert.That(GameEngine.TaskDone(s, "PRO05"), Is.False, "引导不代你融合");
        Assert.That(s.Battle, Is.Null);
    }

    // ───────────── TUT-Q128-04 ─────────────

    /// <summary>TUT-Q128-04：关闭/暂停/回看不完成任务；暂停只停 18 组教学短句与提示、不积压，不关主线剧情（首战警告照播）；Legacy 无饱饱仍有中性提示。</summary>
    [Test]
    public void TUT_Q128_04_DismissPause_LegacyNeutral()
    {
        var s = Fresh();
        E.SetHintsPaused(s, true);
        Assert.That(E.GuideContext(s, "rest"), Is.False);
        Assert.That(E.CurrentHint(s), Is.Null);
        Assert.That(s.Prologue.GuideSeen, Does.Not.Contain("PRO12"), "暂停期间不记、不积压");
        Start(s, ActionRequest.Travel("field"));
        Run(s);
        var events = new List<string>();
        for (var k = 0; k < 10 && E.DialogueLocked(s); k++)
        {
            if (!events.Contains(E.ActiveDialogue(s)!.EventId)) events.Add(E.ActiveDialogue(s)!.EventId);
            E.AckDialogue(s, E.CurrentDialogueLine(s)!.LineId);
        }
        Assert.That(events, Is.EqualTo(new[] { "tutorial_arrive" }), "主线首战警告照播，教学短句暂停");
        Assert.That(E.BattleHint(s), Is.Null);
        E.SetHintsPaused(s, false);
        Assert.That(E.BattleHint(s), Is.Not.Null, "恢复后只看当前未完成且相关的一项");

        var t = Fresh();
        Assert.That(E.GuideContext(t, "rest"), Is.True);
        ReadDialogues(t);
        E.DismissHint(t, "PRO12");
        Assert.That(E.CurrentHint(t)?.TaskId, Is.Not.EqualTo("PRO12"));
        Assert.That(E.GuideContext(t, "rest"), Is.False, "关闭后不再自动出");
        Assert.That(GameEngine.TaskDone(t, "PRO12"), Is.False, "关闭不等于完成");
        Assert.That(E.PrologueTasks(t).Single(x => x.Config.Id == "PRO12").HintDismissed, Is.True, "任务册可回看");

        var legacy = New();
        legacy.Prologue.GuideSeen.Clear();
        Assert.That(E.GuideContext(legacy, "rest"), Is.True);
        Assert.That(E.DialogueLocked(legacy), Is.False, "Legacy 不播饱饱短句");
        Assert.That(E.CurrentHint(legacy)!.Value.TaskId, Is.EqualTo("PRO12"), "仍有中性系统提示");
        Assert.That(legacy.Story.DialogueLog.SelectMany(d => d.Lines).Any(l => l.Speaker == "饱饱"), Is.False);
    }

    // ───────────── TUT-Q128-05 ─────────────

    /// <summary>TUT-Q128-05：行动中触发只暂存、不打断；安全时重验——仍相关才给，已完成或换了地点就丢弃；旧待播队列不爆发。</summary>
    [Test]
    public void TUT_Q128_05_BusyDefers_RevalidateOrDrop()
    {
        // 行动中：暂存，原行动照常走完，结束后再给
        var s = Fresh();
        SetStats(s, 20, 90, 90, 18, 90, 80);
        var m0 = s.GameMinute;
        Start(s, ActionRequest.Wait(30));
        Assert.That(E.GuideContext(s, "rest"), Is.True);
        Assert.That(s.Prologue.GuidePending, Is.EqualTo("PRO12"));
        Assert.That(E.DialogueLocked(s), Is.False, "不打断进行中的行动");
        Run(s);
        Assert.That(s.GameMinute, Is.EqualTo(m0 + 30), "原行动不倒退、不加费");
        Assert.That(E.ActiveDialogue(s)?.EventId, Is.EqualTo("PRO12_reaction"), "安全边界重验后呈现");
        ReadDialogues(s);

        // 暂存后已完成：丢弃，不在完成后补播
        var t = Won();
        SetStats(t, 20, 90, 90, 10, 40, 80);
        Start(t, ActionRequest.Sleep(Cfg.Actions.SleepMinutes.Where(m => m >= GameEngine.Pro12SleepMinutes).Min()));
        Assert.That(E.GuideContext(t, "rest"), Is.True);
        Run(t);
        ReadDialogues(t);
        Assert.That(GameEngine.TaskDone(t, "PRO12"), Is.True);
        Assert.That(Played(t, "PRO12_reaction"), Is.False, "已完成项丢弃");
        Assert.That(t.Prologue.GuidePending, Is.Empty);

        // 暂存后换了地点：上下文失效即丢弃
        var u = Won();
        Start(u, ActionRequest.Travel("field"));
        Assert.That(E.GuideContext(u, "item", "canned_food", "camp"), Is.True);
        Run(u);
        ReadDialogues(u);
        Assert.That(Played(u, "PRO02_reaction"), Is.False);
        Assert.That(u.Prologue.GuideSeen, Does.Not.Contain("PRO02"));

        // 旧版残留的待播队列不播放
        var v = Fresh();
        v.Prologue.PendingReactions.AddRange(new[] { "PRO07_reaction", "PRO13_reaction" });
        E.PrologueBoundary(v);
        Assert.That(E.DialogueLocked(v), Is.False, "无完成后追播、无旧队列爆发");
    }

    // ───────────── TUT-Q129-01/02 ─────────────

    /// <summary>TUT-Q129-01：6 阶段与 6 特殊目标用正式短句；普通主目标/建议用任务标题与下一步短句；玩家文字没有 PRO/owner/tick/字段名。</summary>
    [Test]
    public void TUT_Q129_01_FormalTexts_NoInternalFields()
    {
        var p = Cfg.Prologue;
        Assert.That(p.Stages.Select(x => x.Headline), Is.EqualTo(new[] { "先在附近站稳脚跟", "建立食水补给", "安排工具与休息", "探清周围与退路", "寻找求医的方向", "前往幸存者营区寻找医疗帮助" }));
        Assert.That(p.SpecialObjectives.Select(x => x.Id), Is.EqualTo(new[] { "visitor_talk", "visitor_return", "exit_go", "exit_need", "route_onward", "settle" }));
        var bad = new System.Text.RegularExpressions.Regex(@"[A-Za-z_]{2,}|tick|owner|S15|R07");
        foreach (var t in p.Tasks) Assert.That(bad.IsMatch(t.Goal) || bad.IsMatch(t.Title), Is.False, t.Id + "：" + t.Goal);
        foreach (var x in p.SpecialObjectives.Where(x => x.Id != "exit_need")) Assert.That(bad.IsMatch(x.Title + x.Step), Is.False, x.Id);
        foreach (var a in p.AdaptiveSteps) Assert.That(bad.IsMatch(a.Step), Is.False, a.When);

        var s = Fresh();
        var o = E.CurrentObjective(s)!;
        Assert.That((o.Title, o.Step), Is.EqualTo(("第一次交手", "处理农野荒地的游荡尸，战斗结束时活着。")));
        foreach (var x in E.CurrentSuggestions(s))
        {
            var t = E.ProTask(x.TaskId!)!;
            Assert.That((x.Title, x.Step), Is.EqualTo((t.Title, t.Goal)));
        }

        var pro06 = E.ProTask("PRO06")!;
        Assert.That(E.StepText(s, pro06), Is.EqualTo("到访溪流浅滩，并从合法水源实际取一次脏水。"));
        s.Prologue.DrewWater = true;
        Assert.That(E.StepText(s, pro06), Is.EqualTo("沿已知道路到溪流浅滩确认水源。"));
        s.Prologue.DrewWater = false;
        s.Locations["stream"].Visited = true;
        Assert.That(E.StepText(s, pro06), Is.EqualTo("从当前可达的合法水源实际取一次脏水。"));
        s.Prologue.ReachedVillageOrClinic = true;
        Assert.That(E.StepText(s, E.ProTask("PRO14")!), Is.EqualTo("沿已知道路实际返回临时营地。"));

        var w = Won();
        w.GameMinute = D5;
        E.PrologueBoundary(w);
        ReadDialogues(w);
        o = E.CurrentObjective(w)!;
        Assert.That((o.Title, o.Step), Is.EqualTo(("向程望问路", "在设施栏选择程望，交谈了解营区路线。")));
        Assert.That(E.TalkNpc(w, "npc_cheng_wang").Ok, Is.True);
        ReadDialogues(w);
        o = E.CurrentObjective(w)!;
        Assert.That((o.Id, o.Title, o.Step), Is.EqualTo(("exit_need", "前往幸存者营区寻找医疗帮助", "先沿已知道路到访废弃村庄。")), "按实际缺项，不显示“按真实缺项显示”这句说明");
        Assert.That(E.ObjectiveFocus(w), Is.EqualTo("loc:field"), "村庄还没发现：沿真实物理段，下一段营地→荒地已知可走，点亮荒地（Q139 选 B，B14a 取代 M298 的“不相邻不点亮”）；不点亮未知的村庄");
        w.Locations["village"].Discovered = true;
        Assert.That(E.ObjectiveFocus(w), Is.EqualTo("loc:field"), "村庄已知而荒地—村庄未开通：指向真实下一路段");
    }

    /// <summary>TUT-Q129-02：D1—D4 教学提前做完而来客未到：整备下一次出行，不泄露 6240/第5日/D5，不把普通等待加回建议。</summary>
    [Test]
    public void TUT_Q129_02_Settle_NoVisitorTimeSpoiler()
    {
        var s = Won();
        foreach (var t in Cfg.Prologue.Tasks.Where(t => t.Stage is "D1" or "D2" or "D3" or "D4"))
            s.Prologue.Tasks.TryAdd(t.Id, new PrologueTaskRecord { CompletedAt = s.GameMinute, EventId = "test" });
        E.PrologueBoundary(s);
        var o = E.CurrentObjective(s)!;
        Assert.That(s.Prologue.ObjectiveId, Is.EqualTo("settle"));
        Assert.That((o.Title, o.Step), Is.EqualTo(("整备下一次出行", "补充食水、休息或探索已知地点，留意营地的新消息。")));
        Assert.That(o.Title + o.Step, Does.Not.Contain("6240").And.Not.Contain("第5日").And.Not.Contain("D5").And.Not.Contain("08:00"));
        Assert.That(E.CurrentSuggestions(s), Is.Empty, "不把等待或已完成任务重新推荐");
        Assert.That(GameEngine.VisitorRegistered(s), Is.False);
    }

    // ───────────── TUT-Q131-01 ─────────────

    /// <summary>TUT-Q131-01：没读南口路标、沿路实际走到旧公路即完成 PRO18；任务定位指真实下一路段，不要求返村读牌；三条件开路不变、不伪造路标凭证。</summary>
    [Test]
    public void TUT_Q131_01_ArriveRoad_CompletesWithoutSign()
    {
        var s = Won();
        s.GameMinute = D5;
        E.PrologueBoundary(s);
        ReadDialogues(s);
        E.TalkNpc(s, "npc_cheng_wang");
        ReadDialogues(s);
        Assert.That(s.UnlockedEdges, Does.Not.Contain("village__m01_road"), "仍要实际到访村庄（QMAP01 三条件不变）");
        E.DebugUnlockEdge(s, "field__village");
        Travel(s, "field");
        Travel(s, "village");
        Assert.That(s.UnlockedEdges, Does.Contain("village__m01_road"));
        Assert.That(s.Prologue.ObjectiveId, Is.EqualTo("exit_go"));
        Assert.That(E.ObjectiveFocus(s), Is.EqualTo("loc:m01_road"), "不点亮未读路标");
        Assert.That(E.CurrentObjective(s)!.Step, Is.EqualTo("沿已知道路到废弃村庄南口，再走到旧公路。"));
        Assert.That(E.GuideContext(s, "location", "m01_road"), Is.True, "选中出区路线：PRO18 引导");
        Assert.That(E.CurrentHint(s)!.Value.Text, Is.EqualTo("按已知道路前往废弃村庄南口，再实际走到旧公路；南口路标可选读。"));
        ReadDialogues(s);
        Assert.That(GameEngine.TaskDone(s, "PRO18"), Is.False, "看图/选路不算");
        Travel(s, "m01_road");
        Assert.That(GameEngine.TaskDone(s, "PRO18"), Is.True);
        Assert.That(s.Prologue.Tasks["PRO18"].Retro, Is.False);
        Assert.That(s.Progress.Flags.Contains(GameEngine.FactSouthSignKnown), Is.False, "不伪造路标已读");
        Assert.That(s.Prologue.ObjectiveId, Is.EqualTo("route_onward"));
        Assert.That(E.ObjectiveFocus(s), Is.EqualTo("loc:m01_road"), "检查哨还没发现：点亮当前地点（探索入口），不泄露（M298）");
    }

    // ───────────── TUT-Q133-03 / 存档 25 ─────────────

    /// <summary>
    /// TUT-Q133-03 与 24→25 迁移：旧档可信已到旧公路的无声追认 PRO18（不补奖励、不发完成反馈、不伪造路标）；旧待播队列丢弃；
    /// 已播过的短句记为已示；其他完成、进行中的动作、提示偏好保留；读档本身不弹引导。幂等。
    /// </summary>
    [Test]
    public void TUT_Q133_03_Migration24To25()
    {
        var s = Won();
        E.GuideContext(s, "gear");
        ReadDialogues(s);
        Assert.That(Played(s, "PRO03_reaction"), Is.True);
        s.Progress.Flags.Add(GameEngine.FactCampRouteKnown);
        s.Locations["m01_road"].Visited = true;
        s.Prologue.Tasks.Remove("PRO18");
        s.Prologue.DismissedHints.Add("PRO09");
        s.Prologue.HintsPaused = true;
        s.Prologue.PendingReactions.Add("PRO07_reaction");
        var items = s.Items.Sum(i => i.Count);
        var done = s.Prologue.Tasks.Keys.ToList();
        var j = JObject.Parse(SaveSerializer.Serialize(s));
        j["SchemaVersion"] = "24";
        var pj = (JObject)j["Prologue"]!;
        foreach (var k in new[] { "GuideSeen", "GuideActive", "GuidePending", "GuidePendingAt" }) pj.Remove(k);
        var m = SaveSerializer.Deserialize(j.ToString(), Cfg, out _);
        Assert.That(m.SchemaVersion, Is.EqualTo(GameState.CurrentSchema));
        Assert.That(m.Prologue.Tasks["PRO18"].Retro, Is.True, "可信已到旧公路：无声追认");
        Assert.That(m.Prologue.LastDoneTask, Is.Not.EqualTo("PRO18"), "不发完成反馈");
        Assert.That(m.Progress.Flags.Contains(GameEngine.FactSouthSignKnown), Is.False, "不伪造路标已读");
        Assert.That(m.Prologue.PendingReactions, Is.Empty, "旧待播队列丢弃");
        Assert.That(m.Prologue.GuideSeen, Does.Contain("PRO03"), "旧档已播过的短句视为已示");
        Assert.That(E.DialogueLocked(m), Is.False, "读档本身不弹引导");
        Assert.That(m.Items.Sum(i => i.Count), Is.EqualTo(items), "不补物资");
        Assert.That(done.All(m.Prologue.Tasks.ContainsKey), Is.True, "原完成保留");
        Assert.That((m.Prologue.HintsPaused, m.Prologue.DismissedHints.Contains("PRO09")), Is.EqualTo((true, true)), "提示偏好保留");
        Assert.That(m.Log.Count(l => l.Text.Contains("版本25")), Is.EqualTo(1));

        var again = SaveSerializer.Deserialize(SaveSerializer.Serialize(m), Cfg, out var notes);
        Assert.That(notes, Is.Empty);
        Assert.That(again.Log.Count(l => l.Text.Contains("版本25")), Is.EqualTo(1), "幂等");

        // 缺真实证据的不猜：没到过旧公路就不追认
        var t = Won();
        var tj = JObject.Parse(SaveSerializer.Serialize(t));
        tj["SchemaVersion"] = "24";
        var tm = SaveSerializer.Deserialize(tj.ToString(), Cfg, out _);
        Assert.That(GameEngine.TaskDone(tm, "PRO18"), Is.False);
        Assert.That(GameEngine.TaskDone(tm, "PRO06") || GameEngine.TaskDone(tm, "PRO10") || GameEngine.TaskDone(tm, "PRO14"), Is.False, "取水/制作/返营没有证据不追认");
    }

    // ───────────── 试玩修复 M298 ─────────────

    /// <summary>M298-1：到旧公路后检查哨尚未发现：定位落在当前地点卡（探索入口），不点亮未发现的地点；按现有规则探索到 100% 揭示后指向检查哨。</summary>
    [Test]
    public void M298_1_UndiscoveredNextLeg_FocusCurrentCard_ThenNextLeg()
    {
        var s = Won();
        s.Progress.Flags.Add(GameEngine.FactCampRouteKnown);
        s.Progress.Flags.Add(GameEngine.FactVisitorMet);
        E.DebugMapTeleport(s, "m01_road");
        ReadDialogues(s);
        E.PrologueBoundary(s);
        ReadDialogues(s);
        Assert.That(s.Prologue.ObjectiveId, Is.EqualTo("route_onward"));
        Assert.That(s.Locations["m02_checkpoint"].Discovered, Is.False);
        Assert.That(E.ObjectiveFocus(s), Is.EqualTo("loc:m01_road"), "点亮当前地点卡");
        Assert.That(s.Locations["m02_checkpoint"].Discovered, Is.False, "只读，不揭示");
        E.DebugSetProgress(s, "m01_road", 100);
        E.RefreshGates(s);
        E.PrologueBoundary(s);
        Assert.That(s.Locations["m02_checkpoint"].Discovered, Is.True, "按现有发现规则：探索 100% 揭示外出路线");
        Assert.That(E.ObjectiveFocus(s), Is.EqualTo("loc:m02_checkpoint"), "揭示后指向真实下一段");
    }

    /// <summary>M298-2：暂停期间错过的状态上下文不积压——恢复后在别处不补弹 PRO05，只给当前上下文；地点类错过的离开后再到达才是新上下文。</summary>
    [Test]
    public void M298_2_MissedWhilePaused_NotReplayedElsewhere()
    {
        var s = Fresh();
        E.SetHintsPaused(s, true);
        Start(s, ActionRequest.Travel("field"));
        Run(s);
        ReadDialogues(s);
        for (var k = 0; k < 20 && s.Battle != null; k++)
        {
            if (E.DialogueLocked(s)) { ReadDialogues(s); continue; }
            if (!E.AwaitingPlayer(s)) { E.BattleStep(s); continue; }
            s.Battle.Enemies[0].Hp = 0.01;
            Force(s, Below(0.2), AtLeast(0.6), Any());
            Cmd(s, "basic_attack", s.Battle.Enemies[0].Index);
        }
        ReadDialogues(s);
        Assert.That(s.Story.TutorialWon, Is.True);
        Assert.That(Played(s, "PRO05_reaction"), Is.False, "暂停中不播");
        E.DebugMapTeleport(s, "w12_canal");
        ReadDialogues(s);
        E.SetHintsPaused(s, false);
        E.PrologueBoundary(s);
        Assert.That(E.DialogueLocked(s), Is.False, "恢复后不在别处补弹 PRO05");
        E.DebugGive(s, "dirty_water", 1, "w12_canal");
        Assert.That(E.GuideContext(s, "item", "dirty_water", "w12_canal"), Is.True);
        Assert.That(E.ActiveDialogue(s)?.EventId, Is.EqualTo("PRO11_reaction"), "只给当前上下文");
        ReadDialogues(s);
        Assert.That(Played(s, "PRO05_reaction"), Is.False);

        // 地点类：暂停时到村庄错过 PRO15，原地恢复不补；离开再来是新的上下文
        var t = Won();
        E.SetHintsPaused(t, true);
        E.DebugMapTeleport(t, "village");
        ReadDialogues(t);
        E.SetHintsPaused(t, false);
        E.PrologueBoundary(t);
        Assert.That(E.DialogueLocked(t), Is.False, "原地恢复不补");
        E.DebugMapTeleport(t, "camp");
        ReadDialogues(t);
        E.DebugMapTeleport(t, "village");
        Assert.That(E.ActiveDialogue(t)?.EventId, Is.EqualTo("PRO15_reaction"), "再次到达是新的上下文");
    }

    /// <summary>M298-3：上下文给的提示在离开该地点或主目标切换时让位，任务框回到当前主目标对应项（迁移/Legacy 档到第 5 日换成 PRO16）。</summary>
    [Test]
    public void M298_3_ContextHintYields_OnLeaveOrObjectiveChange()
    {
        var s = Won();
        Assert.That(E.GuideContext(s, "rest"), Is.True);
        ReadDialogues(s);
        Assert.That(E.CurrentHint(s)!.Value.TaskId, Is.EqualTo("PRO12"));
        E.DebugMapTeleport(s, "field");
        ReadDialogues(s);
        Assert.That(E.CurrentHint(s)?.TaskId, Is.Not.EqualTo("PRO12"), "离开给出提示的地点即失效");

        var legacy = New();
        Assert.That(E.GuideContext(legacy, "rest"), Is.True);
        Assert.That(E.CurrentHint(legacy)!.Value.TaskId, Is.EqualTo("PRO12"));
        legacy.GameMinute = D5 + 60;
        E.PrologueBoundary(legacy);
        Assert.That(legacy.Prologue.ObjectiveId, Is.EqualTo("visitor_talk"));
        Assert.That(E.DialogueLocked(legacy), Is.False);
        Assert.That(E.CurrentHint(legacy)!.Value.TaskId, Is.EqualTo("PRO16"), "主目标切换时让位，提示回到 PRO16");
    }

    /// <summary>M298-4：任务框主目标标题允许换行，不再单行截断（“前往幸存者营区寻找医疗帮助”完整显示）。</summary>
    [Test]
    public void M298_4_TaskTitle_Wraps_NotEllipsized()
    {
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "web", "Survival.Web", "wwwroot", "css", "app.css"))) dir = dir.Parent;
        Assert.That(dir, Is.Not.Null, "找到仓库根目录");
        var css = File.ReadAllLines(Path.Combine(dir!.FullName, "web", "Survival.Web", "wwwroot", "css", "app.css"));
        var rule = css.Single(l => l.StartsWith(".task-box .tc-title {"));
        Assert.That(rule, Does.Not.Contain("nowrap").And.Not.Contain("ellipsis"));
        Assert.That(rule, Does.Contain("white-space: normal"));
        Assert.That(Cfg.Prologue.SpecialObjectives.Single(x => x.Id == "route_onward").Title, Is.EqualTo("前往幸存者营区寻找医疗帮助"));
    }
}
