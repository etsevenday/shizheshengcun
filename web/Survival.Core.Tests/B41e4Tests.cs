using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// B41e4（S14 Q569 选 A）：战斗打断工作、撤退离开原地且仍保留同一暂停工作时，中央暂停层让开①地图与场景行，
/// 相邻地点卡「前往 ▸」与区域地图同走原移动浮窗、路线条件与确认；其他暂停限制不变。
/// 几何（三档遮罩／弹窗不盖地点卡）由浏览器自测核对，这里核对判定同源与样式规则。
/// </summary>
public class B41e4Tests : B41bTestBase
{
    private static string Web(string rel) =>
        File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "Survival.Web", rel)).Replace("\r\n", "\n");

    /// <summary>Q569-A01／A03：暂停层只在内核移动例外（PausedTravelOpen）成立时让出①行；遮罩从②顶开始，其余三行仍被挡住。</summary>
    [Test]
    public void Q569_A01_Shell_PauseScrimYieldsMapRowOnlyWhenPausedTravelOpen()
    {
        var pause = Web("Components/PauseLayer.razor");
        Assert.That(pause, Does.Contain("@(e.PausedTravelOpen(s) ? \"travel-open\" : \"\")"), "与地图、移动浮窗同一判定");
        Assert.That(pause, Does.Contain("e.LockedToolPickupOpen(s, pickTool) ? \"pick-open\""), "Q523 原工具拾取让位不变");
        var css = Web("wwwroot/css/shell.css");
        Assert.That(css, Does.Contain(".pause-scrim.travel-open { top: calc(25% + var(--sh-gap) / 4); }"), "遮罩与弹窗从②顶开始，①地图与场景行露出");
        // 地点卡「前往 ▸」仍是原移动浮窗入口，确认移动按同一 moveBusy 判定（M809）
        var card = Web("Components/LocationCard.razor");
        Assert.That(card, Does.Contain("G.OpenPop(\"travel\", LocationId, AnchorId);"));
        Assert.That(Web("Components/ActionPops.razor"), Does.Contain("var moveBusy = (e.IsBusy(s) && !e.PausedTravelOpen(s)) || G.Running || G.BattleBusy || _go;"));
    }

    /// <summary>Q569-A02／A03：让位不改权限——地点卡与地图同一移动预览；探索、清理等其他工作仍按暂停拒绝；离地不能继续，回原地仍暂停、明确继续才恢复。</summary>
    [Test]
    public void Q569_A02_A03_SameRouteRules_OtherWorkStillBlocked_ArriveStillPaused()
    {
        var s = Field(3);
        var id = KeptAtField(s, ActionRequest.Gather("field_fiber"), g =>
        {
            var rs = g.Resources["field_fiber"];
            rs.Discovered = true;
            rs.Available = Math.Max(3, rs.Available ?? 0);
        });
        Assert.That(E.PausedTravelOpen(s), Is.True, "撤退离地保留暂停：暂停层让出地点卡");
        Assert.That(E.Preview(s, ActionRequest.Travel("field")).Ok, Is.True, "地点卡与地图同一移动预览");
        Assert.That(E.CanStart(s, ActionRequest.Explore()), Is.Not.Null, "其他工作仍被暂停挡住，不因入口避让获得新权限");
        Assert.That(E.Resume(s).Ok, Is.False, "离地不能继续");

        Start(s, ActionRequest.Travel("field"));
        Assert.That(E.PausedTravelOpen(s), Is.False, "返程移动途中不再让位（原工作暂存）");
        Assert.That(Run(s), Is.EqualTo(SegmentStatus.Completed));
        Assert.That(s.CurrentLocation, Is.EqualTo("field"));
        Assert.That(s.Main!.ActionId, Is.EqualTo(id));
        Assert.That(s.Main.Status, Is.EqualTo(ActionStatus.Paused), "到达不自动续工");
        Assert.That(E.Resume(s).Ok, Is.True, "明确继续才恢复");
        Assert.That(E.PausedTravelOpen(s), Is.False, "继续后暂停层（及让位）收回");
    }
}
