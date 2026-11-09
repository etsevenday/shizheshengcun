using Survival.Core.Config;
using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 批次 B14c（R148）：策划答复 Q147 选 A——“沿已知路线前往…”“查看通往…的路线条件…”两句（B38 N02 起为“下一段路已开通，前往…”“前往…还需要…”）只用于出区后逐段前进（route_onward）；
/// 其他前进类主目标（回营见来客、出区、首战、到访村庄）下一段已知时保留 Q129 原下一步及缺项分支；下一段未知时所有前进类主目标仍按可探索/不可探索两句。
/// 规则出处：S14/S15/A10/A11 等「Q147 已知路线提示的适用范围」本轮章、prologue-adjudications.json；验收 A10 TUT-Q147-01/02。
/// </summary>
public class B14cRouteHintScopeTests : BattleTestBase
{
    private const int D5 = 6240;
    private const string ExploreStep = "先探索脚下这个地点，找出往前走的路。";
    private const string NoExploreStep = "先看看地图上已经认出的路和通行条件。";

    private GameState Won()
    {
        var s = NewStory();
        ReadDialogues(s);
        E.DebugPrologue(s, "won");
        ReadDialogues(s);
        return s;
    }

    private PrologueTaskConfig Task(string id) => Cfg.Prologue.Tasks.First(t => t.Id == id);

    private static bool Known(TravelSegmentView? seg) => seg?.Kind is TravelSegmentKind.KnownOpen or TravelSegmentKind.KnownBlocked;

    /// <summary>回营见来客：首战已胜、第5日来客已登记、人在荒地（营地已知、路已开通）。</summary>
    private GameState VisitorReturn()
    {
        var s = Won();
        s.GameMinute = D5;
        E.PrologueBoundary(s);
        ReadDialogues(s);
        E.DebugMapTeleport(s, "field");
        ReadDialogues(s);
        E.PrologueBoundary(s);
        ReadDialogues(s);
        Assert.That(s.Prologue.ObjectiveId, Is.EqualTo("visitor_return"));
        return s;
    }

    // ───────────── TUT-Q147-01 ─────────────

    /// <summary>
    /// TUT-Q147-01：route_onward 下一段已知可走/已知未开通分别用定稿路线句（B14a 已测，这里复核一次）；
    /// 回营见来客、出区、首战、到访村庄下一段已知时逐字保留 Q129 原句和缺项分支（保留交谈/战斗要求），不换成路线句。
    /// </summary>
    [Test]
    public void TUT_Q147_01_KnownSegment_RouteLinesOnlyForRouteOnward()
    {
        // 首战（PRO04）：开局在营地，荒地已知、路已开通
        var f = NewStory();
        ReadDialogues(f);
        Assert.That(f.Prologue.ObjectiveId, Is.EqualTo("PRO04"));
        Assert.That(Known(E.NextTravelSegment(f)), Is.True);
        Assert.That(E.CurrentObjective(f)!.Step, Is.EqualTo("处理农野荒地的游荡尸，战斗结束时活着。"), "保留战斗要求");

        // 回营见来客
        var v = VisitorReturn();
        Assert.That(Known(E.NextTravelSegment(v)), Is.True);
        Assert.That(E.CurrentObjective(v)!.Step, Is.EqualTo(E.SpecialOf("visitor_return")!.Step), "保留“与程望交谈”");
        Assert.That(E.CurrentObjective(v)!.Step, Does.Not.StartWith("下一段路已开通，前往"));

        // 出区：缺“到访村庄”时保留缺项分支原句（村庄已知、路已开通）
        var x = Won();
        x.Progress.Flags.Add(GameEngine.FactCampRouteKnown);
        x.Progress.Flags.Add(GameEngine.FactVisitorMet);
        E.DebugMapTeleport(x, "village");
        ReadDialogues(x);
        E.DebugMapTeleport(x, "camp");
        ReadDialogues(x);
        E.PrologueBoundary(x);
        ReadDialogues(x);
        Assert.That(x.Prologue.ObjectiveId, Is.AnyOf("exit_need", "exit_go"));
        Assert.That(Known(E.NextTravelSegment(x)), Is.True, "出区下一段已知");
        {
            var o = E.CurrentObjective(x)!;
            var expected = x.Prologue.ObjectiveId == "exit_need" ? E.AdaptiveStep(x, "exit_need") ?? E.SpecialOf("exit_go")!.Step : E.SpecialOf("exit_go")!.Step;
            Assert.That(o.Step, Is.EqualTo(expected), "出区保留 Q129 原句/缺项分支");
            Assert.That(o.Step, Does.Not.StartWith("下一段路已开通，前往").And.Not.Match("^前往.+还需要"));
        }

        // 到访村庄（PRO15）：村庄已知、人在营地
        var p = Won();
        E.DebugMapTeleport(p, "village");
        ReadDialogues(p);
        E.DebugMapTeleport(p, "camp");
        ReadDialogues(p);
        p.Prologue.ObjectiveId = "PRO15";
        Assert.That(Known(E.NextTravelSegment(p)), Is.True, "到访村庄下一段已知");
            Assert.That(E.CurrentObjective(p)!.Step, Is.EqualTo(E.StepText(p, Task("PRO15"))), "到访村庄保留 Q129 原下一步");

        // route_onward：下一段已知可走用定稿路线句
        var r = Won();
        r.Progress.Flags.Add(GameEngine.FactCampRouteKnown);
        r.Progress.Flags.Add(GameEngine.FactVisitorMet);
        E.DebugMapTeleport(r, "m01_road");
        ReadDialogues(r);
        E.PrologueBoundary(r);
        ReadDialogues(r);
        Assert.That(r.Prologue.ObjectiveId, Is.EqualTo("route_onward"));
        E.DebugSetProgress(r, "m01_road", 100);
        E.PrologueBoundary(r);
        Assert.That(E.CurrentObjective(r)!.Step, Is.EqualTo("下一段路已开通，前往检查哨。"));
    }

    // ───────────── TUT-Q147-02 ─────────────

    /// <summary>
    /// TUT-Q147-02：前进类主目标下一段未知时按可探索未满 100%/不能探索分别显示探索句/查询句；揭示后恢复各自已知分支（出区回到 Q129 原句，
    /// 不论之前是否出现过探索句）；读下一步 0 时间、无副作用。
    /// </summary>
    [Test]
    public void TUT_Q147_02_UnknownSegment_ExploreOrQuery_ThenRestoreOwnBranch()
    {
        var w = Won();
        w.Progress.Flags.Add(GameEngine.FactCampRouteKnown);
        w.Progress.Flags.Add(GameEngine.FactVisitorMet);
        E.DebugMapTeleport(w, "field");
        ReadDialogues(w);
        E.DebugSetProgress(w, "field", 50);
        E.PrologueBoundary(w);
        ReadDialogues(w);
        Assert.That(w.Prologue.ObjectiveId, Is.EqualTo("exit_need"));
        Assert.That(w.Locations["village"].Discovered, Is.False);
        Assert.That(E.CurrentObjective(w)!.Step, Is.EqualTo(ExploreStep), "未知且可探索");

        var before = SaveSerializer.Serialize(w);
        _ = E.CurrentObjective(w);
        _ = E.ObjectiveFocus(w);
        Assert.That(SaveSerializer.Serialize(w), Is.EqualTo(before), "0 时间、无副作用");

        // 未知且当前已满 100%（村庄尚未写入发现）→ 查询句
        var q = SaveSerializer.Deserialize(before, Cfg, out _);
        q.Locations["field"].Progress = 100;
        Assert.That(q.Locations["village"].Discovered, Is.False);
            Assert.That(E.CurrentObjective(q)!.Step, Is.EqualTo(NoExploreStep), "未知且不能探索");

        // 揭示后：出区恢复自己的已知分支（Q129 原句/缺项），不换成路线句，与之前出现过探索句无关
        E.DebugSetProgress(w, "field", 100);
        E.PrologueBoundary(w);
        ReadDialogues(w);
        Assert.That(Known(E.NextTravelSegment(w)), Is.True, "揭示后下一段已知");
        {
            var step = E.CurrentObjective(w)!.Step;
            var expected = w.Prologue.ObjectiveId == "exit_need" ? E.AdaptiveStep(w, "exit_need") ?? E.SpecialOf("exit_go")!.Step : E.SpecialOf("exit_go")!.Step;
            Assert.That(step, Is.EqualTo(expected));
            Assert.That(step, Does.Not.StartWith("下一段路已开通，前往").And.Not.Match("^前往.+还需要"));
        }
    }
}
