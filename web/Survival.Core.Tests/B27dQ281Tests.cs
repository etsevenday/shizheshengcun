using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 批次 B27d（R108 Q281）。规则来源：S14「2026-11-09 Q281–Q282 右栏切换与存档提示」Q281：
/// 以真实地点 ID 前后不同为条件清右栏旧选中（判定由 LocationContextTracker 给，网页壳在每次刷新界面前调用）。
/// 测试工程不引用网页壳，接线处读组件源码确认。
/// </summary>
public class B27dQ281Tests : TestBase
{
    private static string Web(string relative)
    {
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "doc", "策划案"))) dir = dir.Parent;
        Assert.That(dir, Is.Not.Null, "找不到仓库根目录");
        return File.ReadAllText(Path.Combine(dir!.FullName, "web", "Survival.Web", relative));
    }

    // ───────────── Q281 地点切换与右栏 ─────────────

    [Test]
    public void Q281_Tracker_OnlyRealLocationIdChangeCounts()
    {
        var t = new LocationContextTracker();
        Assert.That(t.Observe("camp"), Is.False, "第一次只记基准");
        Assert.That(t.Observe("camp"), Is.False, "同地点不算");
        Assert.That(t.Observe("field"), Is.True, "地点 ID 变了");
        Assert.That(t.Observe("field"), Is.False, "清过一次后同地点不再清");
        Assert.That(t.Observe("camp"), Is.True, "回到原地也是一次真实改变");
        t.Reset();
        Assert.That(t.Observe("village"), Is.False, "新开局/读档后只记基准");
        Assert.That(t.Observe(null), Is.False, "没有进行中的游戏不算");
    }

    /// <summary>Q281-A01：正常抵达与现有传送都改变真实地点 ID → 判定清右栏。</summary>
    [Test]
    public void Q281_A01_ArrivalAndTeleport_TriggerReset()
    {
        var s = New();
        var t = new LocationContextTracker();
        t.Observe(s.CurrentLocation);
        Assert.That(s.CurrentLocation, Is.EqualTo("camp"));

        Start(s, ActionRequest.Travel("field"));
        Assert.That(Run(s), Is.EqualTo(SegmentStatus.Completed));
        Assert.That(t.Observe(s.CurrentLocation), Is.True, "正常抵达");

        Assert.That(E.DebugMapTeleport(s, "village"), Does.StartWith("开发：已传送到"));
        Assert.That(t.Observe(s.CurrentLocation), Is.True, "现有传送（按实际到达处理）");
    }

    /// <summary>Q281-A02：移动在途、取消后仍在原地、移动失败（被拒）都不改真实地点 → 不清，沿同地点有效性校验。</summary>
    [Test]
    public void Q281_A02_InTransitCancelAndRefusal_DoNotReset()
    {
        var s = New();
        var t = new LocationContextTracker();
        t.Observe(s.CurrentLocation);

        Start(s, ActionRequest.Travel("field"));
        Assert.That(E.Advance(s).Status, Is.EqualTo(SegmentStatus.Running), "camp→field 15 分钟，推一段仍在途");
        Assert.That(t.Observe(s.CurrentLocation), Is.False, "在途位置不变");

        Assert.That(E.Cancel(s).Ok, Is.True);
        Assert.That(s.CurrentLocation, Is.EqualTo("camp"));
        Assert.That(t.Observe(s.CurrentLocation), Is.False, "取消后仍在原地");

        Assert.That(E.Start(s, ActionRequest.Travel("village")).Ok, Is.False, "非相邻被拒");
        Assert.That(t.Observe(s.CurrentLocation), Is.False, "移动失败仍在原地");
    }

    /// <summary>接线：每次刷新界面前（Notify）判定；清选中（含随身卡）、批次、拖拽预览、卡旁浮窗，右栏回人物页；不动 Notice（当前反馈位）。</summary>
    [Test]
    public void Q281_Wiring_NotifyResetsBeforeRender()
    {
        var g = Web("Services/GameSession.cs");
        var notify = g.IndexOf("public void Notify()");
        Assert.That(g.IndexOf("ResetOnLocationChange();", notify), Is.LessThan(g.IndexOf("Version++;", notify)), "刷新前先判定");
        var body = g.Substring(g.IndexOf("private void ResetOnLocationChange()"), 600);
        Assert.That(body, Does.Contain("_uiLocation.Observe(S.CurrentLocation)").And.Contain("ClearSelectionSilently();")
            .And.Contain("SelectedBatchKey = null;").And.Contain("PendingAction = null;").And.Contain("Pop = null;"));
        Assert.That(body, Does.Not.Contain("Notice = null"), "拒绝原因走当前反馈位，不清");
        Assert.That(g, Does.Not.Contain("Selected.Owner == ItemInstance.Carried))"), "随身卡选中也清（旧的到达保留随身选中已去掉）");
        var clear = g.Substring(g.IndexOf("private void ClearSelectionSilently()"), 200);
        Assert.That(clear, Does.Contain("DetailTab = DetailTab.Character;"), "回默认人物页");
    }
}
