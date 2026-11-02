using Newtonsoft.Json.Linq;
using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 批次 B13（R39—R42）：新手序章内核——18 项 PRO 任务账与主目标、七日退役、来客程望、三条件出区与南口路标、
/// PRO17 远行确认、Legacy 接续（S15「2026-10-30 分类联动本地更新」§3—§9、A10-TUTOR §6—§7；验收 SP01—SP15、TUI08—TUI12、TXT03/TXT04）。
/// </summary>
public class B13PrologueTests : TestBase
{
    private const int D5 = 6240;

    private void Travel(GameState s, string to)
    {
        Start(s, ActionRequest.Travel(to));
        Assert.That(Run(s), Is.EqualTo(SegmentStatus.Completed));
        ReadDialogues(s);
        Assert.That(s.CurrentLocation, Is.EqualTo(to));
    }

    /// <summary>完整新局，读完开场，首战记为胜利（开发入口同款，不发教学物质）。</summary>
    private GameState Won(uint seed = 12345)
    {
        var s = NewStory(seed);
        ReadDialogues(s);
        E.DebugPrologue(s, "won");
        ReadDialogues(s);
        return s;
    }

    /// <summary>在当前地点等待 minutes（读完途中的对白）。</summary>
    private void WaitFor(GameState s, int minutes)
    {
        for (var left = minutes; left > 0; left -= 60)
        {
            SetStats(s, 20, 90, 90, 18, 90, 80);
            ReadDialogues(s);
            Start(s, ActionRequest.Wait(left >= 60 ? 60 : 30));
            Run(s);
        }
    }

    private bool HasFact(GameState s, string fact) => s.Progress.Flags.Contains(fact);

    private List<string> Speakers(GameState s, string eventPrefix) =>
        s.Story.DialogueLog.Where(d => d.EventId.StartsWith(eventPrefix)).SelectMany(d => d.Lines).Select(l => l.Speaker).ToList();

    // ───────────── 配置 ─────────────

    /// <summary>配置导入：18 项任务、程望阈值 6240、出区边 QMAP01 30 分钟、PRO17 四段 70 分钟、Q32B 对白都在。</summary>
    [Test]
    public void Config_ImportedFromPlannerData()
    {
        var p = Cfg.Prologue;
        Assert.That(p.Tasks.Select(t => t.Id), Is.EqualTo(Enumerable.Range(1, 18).Select(i => $"PRO{i:00}")));
        Assert.That(p.Tasks.Where(t => t.ExitGate).Select(t => t.Id), Is.EqualTo(new[] { "PRO04", "PRO16" }), "Q131：PRO18 是到达后的教学完成记录，不再是出区门槛");
        Assert.That(p.Tasks.All(t => t.Hint.Length > 0 && t.Done.Length > 0 && t.ReactionSet.Length > 0), Is.True);
        Assert.That((p.Visitor.NpcId, p.Visitor.Name, p.Visitor.LocationId, p.Visitor.NotBeforeWorldMinute), Is.EqualTo(("npc_cheng_wang", "程望", "camp", 6240)));
        Assert.That(E.Edge(p.Exit.EdgeId).Gate, Is.EqualTo("QMAP01"));
        Assert.That(p.Journey.Legs.Select(l => l.BaseMinutes), Is.EqualTo(new[] { 30, 20, 15, 5 }));
        Assert.That(p.Journey.BaseMinutesTotal, Is.EqualTo(70));
        Assert.That(Cfg.Story.Dialogues.Count(d => d.Event == "visitor_route_first"), Is.EqualTo(12));
        Assert.That(Cfg.Story.Dialogues.Where(d => d.Event == "visitor_route_first").Select(d => d.Speaker).Distinct(), Is.EquivalentTo(new[] { "你", "程望", "" }));
    }

    // ───────────── R39：任务账、主目标、七日退役 ─────────────

    /// <summary>SP01/TUI02：新局只有 1 个主目标 + 最多 2 条建议；任务册 18 项都在；首战前主目标是首战，阶段 D1。</summary>
    [Test]
    public void SP01_OneObjective_AtMostTwoSuggestions_Book18()
    {
        var s = NewStory();
        ReadDialogues(s);
        Assert.That(s.Prologue.StageId, Is.EqualTo("D1"));
        Assert.That(s.Prologue.ObjectiveId, Is.EqualTo("PRO04"));
        Assert.That(E.CurrentObjective(s)!.Title, Is.EqualTo("第一次交手"), "Q129：普通主目标标题用任务标题");
        Assert.That(s.Prologue.Suggestions, Has.Count.EqualTo(2));
        Assert.That(s.Prologue.Suggestions, Does.Not.Contain("PRO04"));
        Assert.That(E.PrologueTasks(s), Has.Count.EqualTo(18));
        Assert.That(E.CurrentHint(s)!.Value.TaskId, Is.EqualTo("PRO01"), "Q128：开场读完后的首次可操作界面给 PRO01 引导（上下文优先于主目标）");
    }

    /// <summary>SP07/TUI03：选中预览只记 PRO01；实际吃喝才记 PRO02（满值时也只按真实改善）；读提示、预览不冒充。</summary>
    [Test]
    public void SP07_PreviewDoesNotCount_RealUseDoes()
    {
        var s = NewStory();
        ReadDialogues(s);
        E.Preview(s, ActionRequest.UseItem("canned_food"));
        Assert.That(GameEngine.TaskDone(s, "PRO02"), Is.False, "预览不算");
        Assert.That(s.Story.DialogueLog.Any(d => d.EventId == "PRO01_reaction"), Is.True, "Q128：PRO01 短句在开场读完后、选卡之前");
        E.NoteObjectInspected(s);
        Assert.That(GameEngine.TaskDone(s, "PRO01"), Is.True);
        Assert.That(E.DialogueLocked(s), Is.False, "完成后不再补播短句");
        ReadDialogues(s);
        Assert.That(GameEngine.TaskDone(s, "PRO02"), Is.False);
        SetStats(s, 20, 50, 50, 18, 90, 60);
        var water = s.Items.First(i => i.ItemId == "clean_water");
        var pick = E.DoInstant(s, InstantRequest.Pickup(water.InstanceId, 1));
        Assert.That(pick.Ok, Is.True, pick.Reason);
        Assert.That(GameEngine.TaskDone(s, "PRO11"), Is.True, "实际地面→随身转移");
        ReadDialogues(s);
        Start(s, ActionRequest.UseItem("clean_water"));
        Run(s);
        ReadDialogues(s);
        Assert.That(s.Prologue.Tasks["PRO02"].EventId, Does.StartWith("use:"));
        Assert.That(s.Prologue.LastDoneTask, Is.EqualTo("PRO02"));
    }

    /// <summary>SP07/TUI04：提前做的行为自动追认、之后消耗不回退；完成只写任务账，不改物资与时间。</summary>
    [Test]
    public void SP07_RetroAndLatched_NoRewards()
    {
        var s = NewStory();
        ReadDialogues(s);
        var items = s.Items.Sum(i => i.Count);
        var minute = s.GameMinute;
        s.Progress.Flags.Add("boiled");
        s.Progress.Flags.Add("cooked");
        s.Locations["camp"].Facility.ShelterBuilt = true;
        E.UpdateFlags(s);
        Assert.That(new[] { "PRO08", "PRO09", "PRO13" }.All(id => GameEngine.TaskDone(s, id)), Is.True);
        Assert.That(s.Items.Sum(i => i.Count), Is.EqualTo(items), "不发物资");
        Assert.That(s.GameMinute, Is.EqualTo(minute), "不推进时间");
        s.Locations["camp"].Facility.ShelterBuilt = false;
        E.UpdateFlags(s);
        Assert.That(GameEngine.TaskDone(s, "PRO13"), Is.True, "锁存不回退");
    }

    /// <summary>SP08：同一次制作石刀并装备，同时满足 PRO03 与 PRO10，不要求做两把。</summary>
    [Test]
    public void SP08_OneCraftSatisfiesPro03AndPro10()
    {
        var s = NewStory();
        ReadDialogues(s);
        E.DebugGive(s, "stone", 1, "camp");
        E.DebugGive(s, "fiber", 1, "camp");
        SetStats(s, 20, 90, 90, 18, 90, 80);
        Start(s, ActionRequest.Craft("knife"));
        Run(s);
        ReadDialogues(s);
        Assert.That(GameEngine.TaskDone(s, "PRO10"), Is.True);
        var knife = s.Items.First(i => i.ItemId == "stone_knife");
        Assert.That(E.Equip(s, GameState.PlayerId, knife.InstanceId, EquipSlots.Weapon1).Ok, Is.True);
        Assert.That(GameEngine.TaskDone(s, "PRO03"), Is.True);
        Assert.That(s.Items.Count(i => i.ItemId == "stone_knife"), Is.EqualTo(1));
    }

    /// <summary>PRO12：睡眠按已提交睡眠段累计 120 分钟；等待不算。</summary>
    [Test]
    public void Pro12_SleepAccumulates_WaitDoesNot()
    {
        var s = Won();
        WaitFor(s, 180);
        ReadDialogues(s);
        Assert.That(GameEngine.TaskDone(s, "PRO12"), Is.False, "等待不冒充睡眠");
        SetStats(s, 20, 90, 90, 10, 40, 80);
        Start(s, ActionRequest.Sleep(Cfg.Actions.SleepMinutes.Min()));
        ReadDialogues(s);   // 开始睡眠时饱饱“眼睛快合上了”
        Run(s);
        ReadDialogues(s);
        Assert.That(s.Prologue.SleepMinutes, Is.GreaterThanOrEqualTo(Cfg.Actions.SleepMinutes.Min()));
        Assert.That(GameEngine.TaskDone(s, "PRO12"), Is.EqualTo(s.Prologue.SleepMinutes >= GameEngine.Pro12SleepMinutes));
    }

    /// <summary>PRO14：从营地到村庄再实际回营地；PRO15 查看残页 0 分钟，重读不重复记。</summary>
    [Test]
    public void Pro14_RoundTrip_Pro15_ReliefNote()
    {
        var s = Won();
        E.DebugUnlockEdge(s, "field__village");
        Travel(s, "field");
        Travel(s, "village");
        Assert.That(GameEngine.TaskDone(s, "PRO14"), Is.False);
        var m = s.GameMinute;
        Assert.That(E.DocumentsHere(s).Select(d => d.Id), Is.EquivalentTo(new[] { "relief_page_village", "village_south_sign" }));
        Assert.That(E.ReadDocument(s, "relief_page_village").Ok, Is.True);
        ReadDialogues(s);
        Assert.That(s.GameMinute, Is.EqualTo(m), "查看 0 分钟");
        Assert.That(GameEngine.TaskDone(s, "PRO15"), Is.True);
        var at = s.Prologue.Tasks["PRO15"].CompletedAt;
        Assert.That(E.ReadDocument(s, "relief_page_village").Ok, Is.True, "可重读");
        ReadDialogues(s);
        Assert.That(s.Prologue.Tasks["PRO15"].CompletedAt, Is.EqualTo(at));
        Travel(s, "field");
        Travel(s, "camp");
        Assert.That(GameEngine.TaskDone(s, "PRO14"), Is.True);
    }

    /// <summary>SP12：七日不再弹窗、不当门槛；第8日后照常继续。</summary>
    [Test]
    public void SP12_NoSevenDayPopup()
    {
        var s = Won();
        s.GameMinute = At(8, 7, 30);
        WaitFor(s, 60);
        ReadDialogues(s);
        Assert.That(s.Progress.SevenDayShown, Is.False);
        Assert.That(E.DrainEvents(s).Any(e => e.Kind == GameEventKind.SevenDay), Is.False);
        Assert.That(s.Log.Any(l => l.Text.Contains("活过七天")), Is.False, "开场不再写七日目标");
    }

    // ───────────── R40：来客程望 ─────────────

    /// <summary>SP02/TUI08：第2/4日全部教学完成也没有来客；第5日08:00后（首战已胜）在安全边界登记一次，人在营地播抵达对白并出现 NPC 卡。</summary>
    [Test]
    public void SP02_NoEarlyVisitor_D5RegistersOnce()
    {
        var s = Won();
        foreach (var id in E.PrologueTasks(s).Select(t => t.Config.Id).Where(id => id != "PRO16" && id != "PRO17" && id != "PRO18"))
            s.Prologue.Tasks[id] = new PrologueTaskRecord { CompletedAt = s.GameMinute, EventId = "test" };
        s.GameMinute = At(4, 20, 0);
        WaitFor(s, 60);
        Assert.That(HasFact(s, GameEngine.FactVisitorAvailable), Is.False, "第4日不来");
        Assert.That(s.Prologue.ObjectiveId, Is.EqualTo("settle"));
        s.GameMinute = D5 - 10;
        WaitFor(s, 30);
        Assert.That(HasFact(s, GameEngine.FactVisitorAvailable), Is.True);
        Assert.That(s.Prologue.VisitorAvailableAt, Is.GreaterThanOrEqualTo(D5));
        Assert.That(E.DialogueLocked(s), Is.True, "人在营地空闲：抵达对白");
        Assert.That(E.ActiveDialogue(s)!.EventId, Is.EqualTo("visitor_arrival_camp"));
        ReadDialogues(s);
        var npc = E.NpcsHere(s).Single();
        Assert.That((npc.Name, npc.HasNews, npc.TalkBlocked), Is.EqualTo(("程望", true, (string?)null)));
        Assert.That(s.Prologue.ObjectiveId, Is.EqualTo("visitor_talk"));
        var at = s.Prologue.VisitorAvailableAt;
        WaitFor(s, 60);
        Assert.That(s.Prologue.VisitorAvailableAt, Is.EqualTo(at), "只登记一次");
        Assert.That(s.Story.DialogueLog.Count(d => d.EventId == "visitor_arrival_camp"), Is.EqualTo(1));
    }

    /// <summary>SP03/TUI08：首战没赢不来；第6日07:00才赢也登记（比较绝对分钟，不写成“第5日且8点后”），抵达用晚到对白。</summary>
    [Test]
    public void SP03_AbsoluteMinute_LateVictory()
    {
        var s = NewStory();
        ReadDialogues(s);
        s.GameMinute = At(6, 6, 30);
        WaitFor(s, 30);
        Assert.That(HasFact(s, GameEngine.FactVisitorAvailable), Is.False, "首战未胜不来");
        E.DebugPrologue(s, "won");   // 第6日07:00 赢
        Assert.That(HasFact(s, GameEngine.FactVisitorAvailable), Is.True);
        Assert.That(s.Prologue.VisitorLate, Is.True);
        Assert.That(E.ActiveDialogue(s)!.EventId, Is.EqualTo("visitor_late_or_legacy"));
        ReadDialogues(s);
    }

    /// <summary>SP03/TUI09：人在外只通知一次（非阻断），不传送；之后回到营地才播抵达；来客常驻、跨日不消失。</summary>
    [Test]
    public void SP03_AwayOnlyNotice_NoTeleport_Persistent()
    {
        var s = Won();
        Travel(s, "field");
        s.GameMinute = D5 - 5;
        E.DrainEvents(s);
        WaitFor(s, 30);
        Assert.That(s.CurrentLocation, Is.EqualTo("field"), "不传送");
        Assert.That(E.DialogueLocked(s), Is.False, "人在外不弹对白");
        var ev = E.DrainEvents(s).Where(e => e.Kind == GameEventKind.Notice).ToList();
        Assert.That(ev.Single().Text, Does.Contain("临时营地出现了来客"));
        Assert.That(s.Prologue.ObjectiveId, Is.EqualTo("visitor_return"));
        WaitFor(s, 60 * 24);   // 跨日
        Assert.That(E.DrainEvents(s).Count(e => e.Kind == GameEventKind.Notice), Is.EqualTo(0), "只通知一次");
        Travel(s, "camp");
        Assert.That(s.Story.DialogueLog.Any(d => d.EventId == "visitor_arrival_camp"), Is.True, "回营地后播抵达");
        Assert.That(E.NpcsHere(s), Has.Count.EqualTo(1));
    }

    /// <summary>SP10：营地里长行动（睡眠）中跨过阈值：只在边界登记，行动不丢进度、不重复 tick；行动结束后才播抵达。</summary>
    [Test]
    public void SP10_DuringLongAction_WaitsForSafeBoundary()
    {
        var s = Won();
        s.GameMinute = D5 - 60;
        SetStats(s, 20, 90, 90, 10, 40, 80);
        var minutes = Cfg.Actions.SleepMinutes.Min();
        Start(s, ActionRequest.Sleep(minutes));
        ReadDialogues(s);
        var start = s.GameMinute;
        var segs = 0;
        while (s.Main != null && segs++ < 500)
        {
            var r = E.Advance(s);
            if (r.Status != SegmentStatus.Running) break;
            if (s.GameMinute >= D5) Assert.That(HasFact(s, GameEngine.FactVisitorAvailable), Is.True, "段边界登记");
            Assert.That(E.DialogueLocked(s), Is.False, "行动中不抢输入");
        }
        Assert.That(s.GameMinute - start, Is.EqualTo(minutes), "睡眠时长完整、不重复 tick");
        Assert.That(E.ActiveDialogue(s)?.EventId, Is.EqualTo("visitor_arrival_camp"), "结束后播抵达");
    }

    /// <summary>SP11/TXT03：交谈 0 分钟，首次写 visitor_met、camp_route_known；重复交谈只播“再问路线”，不重发效果、不成队友、不改身体。</summary>
    [Test]
    public void SP11_Talk_ZeroMinutes_RepeatNoEffects()
    {
        var s = Won();
        s.GameMinute = D5;
        E.PrologueBoundary(s);
        ReadDialogues(s);
        var minute = s.GameMinute;
        var stats = (s.Stats.Satiety, s.Stats.Hydration, s.Stats.Health);
        var items = s.Items.Sum(i => i.Count);
        Assert.That(E.TalkNpc(s, "npc_cheng_wang").Ok, Is.True);
        Assert.That(E.ActiveDialogue(s)!.EventId, Is.EqualTo("visitor_route_first"));
        ReadDialogues(s);
        Assert.That(HasFact(s, GameEngine.FactVisitorMet) && HasFact(s, GameEngine.FactCampRouteKnown), Is.True);
        Assert.That(GameEngine.TaskDone(s, "PRO16"), Is.True);
        Assert.That(s.Story.DialogueLog.Any(d => d.EventId == "visitor_after_route_baobao"), Is.True, "有饱饱：交谈之后饱饱发问");
        Assert.That(s.GameMinute, Is.EqualTo(minute), "交谈 0 分钟");
        Assert.That((s.Stats.Satiety, s.Stats.Hydration, s.Stats.Health), Is.EqualTo(stats));
        Assert.That(s.Items.Sum(i => i.Count), Is.EqualTo(items), "不赠物资");
        Assert.That(s.Party, Is.EqualTo(new[] { GameState.PlayerId }), "不成为队友");
        Assert.That(s.Prologue.StageId, Is.EqualTo("mainline"));
        Assert.That(E.CurrentObjective(s)!.Title, Is.EqualTo("前往幸存者营区寻找医疗帮助"));
        var met = s.Prologue.VisitorMetAt;
        Assert.That(E.TalkNpc(s, "npc_cheng_wang").Ok, Is.True);
        Assert.That(E.ActiveDialogue(s)!.EventId, Is.EqualTo("visitor_route_repeat"));
        ReadDialogues(s);
        Assert.That(E.TalkNpc(s, "npc_cheng_wang").Ok, Is.True);
        ReadDialogues(s);
        Assert.That(s.Story.DialogueLog.Count(d => d.EventId.StartsWith("visitor_route_repeat")), Is.EqualTo(2), "每次再问都有记录");
        Assert.That(s.Prologue.VisitorMetAt, Is.EqualTo(met));
        Assert.That(s.Story.DialogueLog.Count(d => d.EventId == "visitor_route_first"), Is.EqualTo(1));
    }

    /// <summary>NPC 卡交谈门控：行动中、对话中拒绝；人不在营地时没有卡。</summary>
    [Test]
    public void Talk_Blocked_WhenBusyOrAway()
    {
        var s = Won();
        s.GameMinute = D5;
        E.PrologueBoundary(s);
        Assert.That(E.TalkNpc(s, "npc_cheng_wang").Reason, Is.EqualTo(GameEngine.DialogueLockedReason));
        ReadDialogues(s);
        Start(s, ActionRequest.Wait(30));
        Assert.That(E.NpcsHere(s).Single().TalkBlocked, Is.EqualTo("行动进行中"));
        Run(s);
        Travel(s, "field");
        Assert.That(E.NpcsHere(s), Is.Empty);
        Assert.That(E.TalkNpc(s, "npc_cheng_wang").Ok, Is.False);
    }

    // ───────────── R41：三条件出区与南口路标 ─────────────

    /// <summary>SP04/TUI10：先访村庄后交谈 → 交谈时立即开 village↔m01_road（不用重访）；不要村庄 100、不提前诊所。</summary>
    [Test]
    public void SP04_VillageFirst_TalkOpensExitImmediately()
    {
        var s = Won();
        E.DebugUnlockEdge(s, "field__village");
        Travel(s, "field");
        Travel(s, "village");
        Assert.That(s.UnlockedEdges, Does.Not.Contain("village__m01_road"));
        Travel(s, "field");
        Travel(s, "camp");
        s.GameMinute = D5;
        E.PrologueBoundary(s);
        ReadDialogues(s);
        E.TalkNpc(s, "npc_cheng_wang");
        ReadDialogues(s);
        Assert.That(s.UnlockedEdges, Does.Contain("village__m01_road"));
        Assert.That(s.Locations["village"].Progress, Is.LessThan(100));
        Assert.That(s.UnlockedEdges, Does.Not.Contain("village__clinic"));
        Assert.That(s.Prologue.ObjectiveId, Is.EqualTo("exit_go"));
    }

    /// <summary>SP04/SP06/TUI11：先交谈后到村庄 → 到达时开边；未融合、没建棚、没睡、任务未全清也能出发；南口路标识别写 south_sign_known，PRO18 = 路标 + 实际抵达旧公路。</summary>
    [Test]
    public void SP04_SP06_TalkFirst_ArriveOpens_NoTutorialChecklist_Pro18()
    {
        var s = Won();
        s.GameMinute = D5;
        E.PrologueBoundary(s);
        ReadDialogues(s);
        E.TalkNpc(s, "npc_cheng_wang");
        ReadDialogues(s);
        Assert.That(s.Prologue.ObjectiveId, Is.EqualTo("exit_need"));
        Assert.That(E.CurrentObjective(s)!.Step, Is.EqualTo("先沿已知道路到访废弃村庄。"), "Q129 exit_need 按实际缺项");
        Assert.That(GameEngine.TaskDone(s, "PRO05") || GameEngine.TaskDone(s, "PRO12") || GameEngine.TaskDone(s, "PRO13"), Is.False);
        E.DebugUnlockEdge(s, "field__village");
        Travel(s, "field");
        Travel(s, "village");
        Assert.That(s.UnlockedEdges, Does.Contain("village__m01_road"), "到达复核");
        Assert.That(s.Prologue.ObjectiveId, Is.EqualTo("exit_go"));
        Assert.That(E.ObjectiveFocus(s), Is.EqualTo("loc:m01_road"), "Q131：指向真实下一路段，不指路标");
        Assert.That(E.ReadDocument(s, "village_south_sign").Ok, Is.True);
        Assert.That(E.ActiveDialogue(s)!.EventId, Is.EqualTo("village_south_sign"));
        ReadDialogues(s);
        Assert.That(HasFact(s, GameEngine.FactSouthSignKnown), Is.True);
        Assert.That(GameEngine.TaskDone(s, "PRO18"), Is.False, "只读路标不完成，还没实际抵达");
        Travel(s, "m01_road");
        Assert.That(GameEngine.TaskDone(s, "PRO18"), Is.True);
        Assert.That(s.Prologue.ObjectiveId, Is.EqualTo("route_onward"));
    }

    /// <summary>路标在不知道营区路线时只看到模糊字迹，不写凭证；SP05：field100→village、village100→clinic、m01→m02 门槛不变。</summary>
    [Test]
    public void SouthSign_RouteUnknown_NoCredential_SP05_GatesKept()
    {
        var s = Won();
        E.DebugUnlockEdge(s, "field__village");
        Travel(s, "field");
        Travel(s, "village");
        Assert.That(E.ReadDocument(s, "village_south_sign").Ok, Is.True);
        Assert.That(E.ActiveDialogue(s)!.EventId, Is.EqualTo("village_south_sign_route_unknown"));
        ReadDialogues(s);
        Assert.That(HasFact(s, GameEngine.FactSouthSignKnown), Is.False);
        Assert.That(E.Edge("field__village").Gate, Is.EqualTo("field100"));
        Assert.That(E.Edge("village__clinic").Gate, Is.EqualTo("village100"));
        Assert.That(E.Edge("m01_road__m02_checkpoint").Gate, Is.EqualTo("from_explore100"));
    }

    /// <summary>求医路线日志：得知路线后可重读，不移动、不改时间；之前不能打开。</summary>
    [Test]
    public void RouteLog_ReplayOnly()
    {
        var s = Won();
        Assert.That(E.OpenRouteLog(s).Ok, Is.False);
        s.GameMinute = D5;
        E.PrologueBoundary(s);
        ReadDialogues(s);
        E.TalkNpc(s, "npc_cheng_wang");
        ReadDialogues(s);
        var (loc, min) = (s.CurrentLocation, s.GameMinute);
        Assert.That(E.OpenRouteLog(s).Ok, Is.True);
        ReadDialogues(s);
        Assert.That(E.OpenRouteLog(s).Ok, Is.True);
        ReadDialogues(s);
        Assert.That((s.CurrentLocation, s.GameMinute), Is.EqualTo((loc, min)));
        Assert.That(s.Story.DialogueLog.Count(d => d.EventId.StartsWith("camp_route_log")), Is.EqualTo(2));
    }

    // ───────────── R42：远行确认与 Legacy ─────────────

    /// <summary>SP14/TUI12：PRO17 显示四段 70 基础分钟（非全旅程预算）、全员状态与随身；缺建议物只提示，可知情确认；不赠物。</summary>
    [Test]
    public void SP14_JourneyPreview_SoftSuggestions_Confirm()
    {
        var s = Won();
        Assert.That(E.ConfirmJourney(s).Ok, Is.False, "得知路线前不能确认");
        s.GameMinute = D5;
        E.PrologueBoundary(s);
        ReadDialogues(s);
        E.TalkNpc(s, "npc_cheng_wang");
        ReadDialogues(s);
        var pv = E.JourneyPreviewOf(s);
        Assert.That(pv.Legs.Select(l => (l.From, l.To, l.Minutes)), Is.EqualTo(new[] { ("village", "m01_road", 30), ("m01_road", "m02_checkpoint", 20), ("m02_checkpoint", "h01_gate", 15), ("h01_gate", "h02_square", 5) }));
        Assert.That(pv.BaseMinutesTotal, Is.EqualTo(70));
        Assert.That(pv.Missing, Has.Some.Contains("净水").And.Some.Contains("防身武器"));
        Assert.That(pv.PartyLines, Has.Count.EqualTo(1));
        Assert.That(pv.CanSayNotReady, Is.True, "程望在场可说先准备");
        Assert.That(E.SayNotReady(s).Ok, Is.True);
        ReadDialogues(s);
        Assert.That(GameEngine.TaskDone(s, "PRO17"), Is.False, "先准备不算确认");
        var items = s.Items.Sum(i => i.Count);
        Assert.That(E.ConfirmJourney(s).Ok, Is.True, "缺建议物也可知情继续");
        ReadDialogues(s);
        Assert.That(GameEngine.TaskDone(s, "PRO17"), Is.True);
        Assert.That(s.Items.Sum(i => i.Count), Is.EqualTo(items), "不偷赠、不强吃");
    }

    /// <summary>SP13/TUI12/TXT04：旧档（Legacy，无饱饱）过阈值后来客可用但不自动弹窗；交谈用晚到对白、不播饱饱台词；首战等效出区；不补教学物质与+0.1。</summary>
    [Test]
    public void SP13_Legacy_Continues_NoBaobao_NoRetrofit()
    {
        var s = New();
        s.GameMinute = D5 + 600;
        E.PrologueBoundary(s);
        Assert.That(HasFact(s, GameEngine.FactVisitorAvailable), Is.True);
        Assert.That(E.DialogueLocked(s), Is.False, "旧档不自动弹抵达对白");
        Assert.That(GameEngine.TaskDone(s, "PRO04") && s.Prologue.Tasks["PRO04"].Retro, Is.True, "首战按旧档等效");
        Assert.That(E.PrologueTasks(s).Single(t => t.Config.Id == "PRO05").Note, Does.Contain("旧存档"));
        Assert.That(E.TalkNpc(s, "npc_cheng_wang").Ok, Is.True);
        ReadDialogues(s);
        Assert.That(s.Story.DialogueLog.Any(d => d.EventId == "visitor_late_or_legacy"), Is.True);
        Assert.That(s.Story.DialogueLog.Any(d => d.EventId == "visitor_after_route_without_baobao"), Is.True);
        Assert.That(s.Story.DialogueLog.SelectMany(d => d.Lines).Any(l => l.Speaker == "饱饱"), Is.False, "不播饱饱");
        Assert.That(s.Traits, Is.Empty, "不补 +0.1");
        Assert.That(s.Items.Any(i => i.Source?.TutorialReward == true), Is.False, "不补教学物质");
        E.DebugTeleport(s, "village");
        Assert.That(s.UnlockedEdges, Does.Contain("village__m01_road"), "Legacy 等效首战 + 路线 + 到访");
    }

    /// <summary>SP13/COMP16：旧档 21→22 迁移：已开出区路线不反锁、不传回营地；可靠记录追认（标旧档追认）；七日记录只作历史；已过阈值的来客在边界登记。</summary>
    [Test]
    public void SP13_Migration21To22_KeepsRoutes_RetroTasks()
    {
        var s = New();
        s.GameMinute = D5 + 30;
        s.Progress.Flags.Add("boiled");
        s.Progress.SevenDayShown = true;
        E.DebugUnlockEdge(s, "village__m01_road");
        E.DebugTeleport(s, "m01_road");
        var j = JObject.Parse(SaveSerializer.Serialize(s));
        j["SchemaVersion"] = "21";
        j.Remove("Prologue");
        var m = SaveSerializer.Deserialize(j.ToString(), Cfg, out _);
        Assert.That(m.SchemaVersion, Is.EqualTo(GameState.CurrentSchema), "21→22→23（B13b 起 23）");
        Assert.That(m.CurrentLocation, Is.EqualTo("m01_road"), "不传回营地");
        Assert.That(m.UnlockedEdges, Does.Contain("village__m01_road"), "已开路线不反锁");
        Assert.That(m.Prologue.Tasks["PRO08"].Retro, Is.True);
        Assert.That(m.Prologue.Tasks.ContainsKey("PRO06"), Is.False, "没有可靠记录的不猜");
        Assert.That(m.Progress.SevenDayShown, Is.True, "七日记录保留为历史");
        Assert.That(m.Progress.Flags.Contains(GameEngine.FactVisitorAvailable), Is.True, "已过阈值：来客可用");
        Assert.That(m.Log.Any(l => l.Text.Contains("版本22")), Is.True);
    }

    /// <summary>新局首战警告后播饱饱“它过来了”（PRO04 反应），胜利后教学物质落地时播 PRO05 反应；同组只播一次。</summary>
    [Test]
    public void Reactions_PlayOnce_OnlyWithBaobao()
    {
        var s = NewStory();
        ReadDialogues(s);
        Start(s, ActionRequest.Travel("field"));
        Run(s);
        Assert.That(s.Battle, Is.Not.Null);
        var events = new List<string>();
        for (var k = 0; k < 10 && E.DialogueLocked(s); k++)
        {
            events.Add(E.ActiveDialogue(s)!.EventId);
            E.AckDialogue(s, E.CurrentDialogueLine(s)!.LineId);
        }
        Assert.That(events.Distinct(), Is.EqualTo(new[] { "tutorial_arrive", "PRO04_reaction" }));
        Assert.That(Speakers(s, "PRO04_reaction"), Is.EqualTo(new[] { "饱饱", "你" }));
    }

    /// <summary>系统提示：关闭这条不等于完成，之后不再弹；暂停后续提示只关提示；都不推进时间。</summary>
    [Test]
    public void Hints_DismissAndPause()
    {
        var s = NewStory();
        ReadDialogues(s);
        var h = E.CurrentHint(s);
        Assert.That(h, Is.Not.Null);
        E.DismissHint(s, h!.Value.TaskId);
        Assert.That(E.CurrentHint(s)?.TaskId, Is.Not.EqualTo(h.Value.TaskId), "关闭后不再出这条（退回主目标的提示）");
        E.DismissHint(s, E.CurrentHint(s)!.Value.TaskId);
        Assert.That(E.CurrentHint(s), Is.Null);
        Assert.That(GameEngine.TaskDone(s, h.Value.TaskId), Is.False, "关闭不等于完成");
        Assert.That(E.PrologueTasks(s).Single(t => t.Config.Id == h.Value.TaskId).HintDismissed, Is.True);
        s.Prologue.DismissedHints.Clear();
        E.SetHintsPaused(s, true);
        Assert.That(E.CurrentHint(s), Is.Null);
        E.SetHintsPaused(s, false);
        Assert.That(E.CurrentHint(s), Is.Not.Null);
    }

    // ───────────── 试玩修复 M269 ─────────────

    /// <summary>P5：任务角/任务册给玩家看的文字不含内部 ID 与规则缩写（地点用显示名）。</summary>
    [Test]
    public void M269_P5_PlayerTextsHaveNoInternalIds()
    {
        var bad = new System.Text.RegularExpressions.Regex(@"[A-Za-z_]{3,}|tick|S15|R07|N或W");
        foreach (var t in Cfg.Prologue.Tasks)
            Assert.That(bad.IsMatch(GameEngine.GoalText(t)), Is.False, t.Id + "：" + GameEngine.GoalText(t));
        var s = NewStory();
        ReadDialogues(s);
        Assert.That(E.CurrentObjective(s)!.Step, Does.Contain("农野荒地").And.Not.Contain("S15"));
        Assert.That(E.CurrentSuggestions(s).All(x => !bad.IsMatch(x.Step)), Is.True);
    }

    /// <summary>P4：非 Legacy 的 schema 21 旧档在营地、已过第5日08:00：迁移后不自动弹抵达对白；点交谈才接续，用晚到对白；有饱饱时照常有饱饱台词。</summary>
    [Test]
    public void M269_P4_MigratedSave_NoAutoArrival_TalkContinues()
    {
        var s = Won();
        s.GameMinute = 6300;
        s.Story.TutorialWonMinute = null;   // schema 21 没有这个字段
        var j = JObject.Parse(SaveSerializer.Serialize(s));
        j["SchemaVersion"] = "21";
        j.Remove("Prologue");
        var m = SaveSerializer.Deserialize(j.ToString(), Cfg, out _);
        Assert.That(m.Prologue.MigratedAt, Is.EqualTo(6300));
        Assert.That(m.Progress.Flags.Contains(GameEngine.FactVisitorAvailable), Is.True);
        Assert.That(E.DialogueLocked(m), Is.False, "迁移档不自动弹抵达对白");
        E.PrologueBoundary(m);
        Assert.That(E.DialogueLocked(m), Is.False);
        Assert.That(E.NpcsHere(m), Has.Count.EqualTo(1));
        Assert.That(E.TalkNpc(m, "npc_cheng_wang").Ok, Is.True);
        Assert.That(E.ActiveDialogue(m)!.EventId, Is.EqualTo("visitor_late_or_legacy"));
        ReadDialogues(m);
        Assert.That(m.Progress.Flags.Contains(GameEngine.FactCampRouteKnown), Is.True);
        Assert.That(m.Story.DialogueLog.Any(d => d.EventId == "visitor_after_route_baobao"), Is.True, "原局饱饱在体内：交谈后照常有饱饱台词");
    }

    /// <summary>P7：真实走到村庄与开发入口“按实际到达处理”的传送都触发 PRO15 到达反应（各自一次）。</summary>
    [Test]
    public void M269_P7_VillageArrivalReaction_RealAndDevTeleport()
    {
        var s = Won();
        E.DebugUnlockEdge(s, "field__village");
        Start(s, ActionRequest.Travel("field"));
        Run(s);
        ReadDialogues(s);
        Start(s, ActionRequest.Travel("village"));
        Run(s);
        Assert.That(E.ActiveDialogue(s)?.EventId, Is.EqualTo("PRO15_reaction"), "真实到达");
        ReadDialogues(s);

        var t = Won();
        E.DebugMapTeleport(t, "village");
        Assert.That(E.ActiveDialogue(t)?.EventId, Is.EqualTo("PRO15_reaction"), "按实际到达处理的传送");
        ReadDialogues(t);
        E.DebugMapTeleport(t, "camp");
        ReadDialogues(t);
        Assert.That(GameEngine.TaskDone(t, "PRO14"), Is.True, "传送也走往返钩子");
    }
}
