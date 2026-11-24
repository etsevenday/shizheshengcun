using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Survival.Core.Engine;
using Survival.Core.State;
using Survival.Web.Components;
using Survival.Web.Services;

namespace Survival.Web.Tests;

/// <summary>
/// B60a R389（S14「2026-11-24 B60 暂停窗入口与原页返回」Q1103 选 B，取代 Q581／FACPROC-A09、相关休息页规则及 Q590 中这三页因确认开工而收起的部分）与
/// R390（S08 末尾「B35b Q1104 选 A」及「Q1104 验收」）：从火堆菜单、休息页、地图成功开始行动时保留原页，居中进度浮窗覆盖其上；
/// 结束后原页仍合法可查看则回到该页；人物已移动时地图按当前位置刷新。从材料来源「在地图上定位」打开的地图同样能出发。
/// 点真实按钮开工；不等待时看推进中的状态，等待按钮任务结束后看结束后的页面。
/// </summary>
public class B60aPageReturnUiTests : WebTestBase
{
    private void Ok(ActionResult r) => Assert.That(r.Ok, Is.True, r.Reason);

    private IRenderedComponent<MapOverlay> RenderMap()
    {
        Ctx.JSInterop.Setup<double[]>("survivalMap.size", _ => true).SetResult(new double[] { 900, 600 });
        Ctx.JSInterop.Setup<double[]>("survivalMap.getCenter", _ => true).SetResult(new double[] { 450, 300 });
        return Ctx.RenderComponent<MapOverlay>();
    }

    /// <summary>
    /// Q1103 B60 验收「从火堆菜单点『等待加工』，进度窗覆盖火堆菜单，结束后尚能使用该火堆时回原菜单」：营地火堆锅里有一份脏水、火燃着——
    /// 点菜单主层「等待加工」后推进中火堆菜单仍是当前面板（进度遮罩在上），做完后仍是同一火堆菜单。
    /// </summary>
    [Test]
    public async Task FireMenu_WaitCook_MenuStaysUnderProgress_ReturnsAfter()
    {
        var pot = S.Items.First(i => i.ItemId == "pot");
        var water = E.CreateItems(S, "dirty_water", 1, S.Ground).Single();
        Ok(E.DoInstant(S, InstantRequest.FillPot(water.InstanceId, pot.InstanceId, 1)));
        Ok(E.DoInstant(S, InstantRequest.PutInFire(pot.InstanceId)));
        var fac = E.FacOf(S, S.Ground)!;
        fac.FireLit = true;
        fac.FuelMinutes = 180;
        G.OpenFireMenu();
        Assert.That(G.Panel, Is.EqualTo(GameSession.FireMenuPanel), "起点：火堆加工菜单开着");
        var cut = Ctx.RenderComponent<FireMenu>();

        var run = cut.Find(".fm-quick button.cook").ClickAsync(new MouseEventArgs());
        Assert.That((G.Running, G.ProgressLock), Is.EqualTo((true, true)), "起点核对：已开工、进度浮窗在最上层");
        Assert.That(G.Panel, Is.EqualTo(GameSession.FireMenuPanel), "推进中火堆菜单保留在浮窗下");
        await run;
        Assert.That(S.Main, Is.Null, "等待加工已结束");
        Assert.That(G.Panel, Is.EqualTo(GameSession.FireMenuPanel), "结束后回到原火堆菜单");
        Assert.That(G.FireMenu, Is.Not.Null);
    }

    /// <summary>
    /// 配对（Q1103「原页失效时按现有安全返回」）：火堆菜单开着时开工，推进中人物换了地点（开发传送造起点）——菜单按原 ReconcileFireMenu 规则关闭，不远程恢复。
    /// </summary>
    [Test]
    public void FireMenu_InvalidAfterMove_SafeReturn()
    {
        G.OpenFireMenu();
        Ok(E.Start(S, ActionRequest.Wait(30)));
        G.Notify();
        Assert.That(G.Panel, Is.EqualTo(GameSession.FireMenuPanel), "起点：开工后菜单仍在");
        E.DebugTeleport(S, "field");
        G.Notify();
        Assert.That(G.Panel, Is.Null, "换地点后菜单失效即关");
        Assert.That(G.FireMenu, Is.Null);
    }

    /// <summary>
    /// Q1103 B60 验收「休息页开始睡眠……也分别保留原页于遮罩之下，结束后恢复仍合法的原页」：休息页选 30 分钟休息、点开始——推进中休息页仍是当前面板，结束后仍在休息页。
    /// </summary>
    [Test]
    public async Task RestPage_Start_StaysUnderProgress_ReturnsAfter()
    {
        G.OpenTopPanel("rest");
        G.RestKind = "rest";
        G.RestMinutes = 30;
        var cut = Ctx.RenderComponent<RestOverlay>();

        var run = cut.Find(".rg-start").ClickAsync(new MouseEventArgs());
        Assert.That((G.Running, G.ProgressLock), Is.EqualTo((true, true)), "起点核对：已开工");
        Assert.That(G.Panel, Is.EqualTo("rest"), "推进中休息页保留在浮窗下");
        await run;
        Assert.That(S.Main, Is.Null, "休息已结束");
        Assert.That(G.Panel, Is.EqualTo("rest"), "结束后回到休息页");
    }

    /// <summary>
    /// Q1103 B60 验收「地图确认出发时，也保留原页……人物已移动时地图按当前位置刷新」：城内建筑 h03_workshop 在城市层选相邻 h02_square、点「出发」——
    /// 推进中地图仍是当前面板；到达后地图仍开着，所选目的地清掉（已走到）、阅读锚点改为当前位置。
    /// </summary>
    [Test]
    public async Task Map_Depart_MapStays_RefreshedToCurrentLocation()
    {
        S.Progress.Flags.UnionWith(new[] { "registration_authorized", "hub_registered" });
        E.DebugMapTeleport(S, "h03_workshop");
        for (var k = 0; k < 128 && E.DialogueLocked(S); k++) E.AckDialogue(S, E.CurrentDialogueLine(S)!.LineId);
        G.OpenTopPanel("map");
        G.PickMapNode("h02_square");
        var map = RenderMap();

        Assert.That(G.CityBrowsing, Is.True, "起点：城市层（出发与普通地图同源 MapDepart）");
        var run = map.Find("[data-pt=city-depart]").ClickAsync(new MouseEventArgs());
        Assert.That(G.Running, Is.True, "起点核对：已出发");
        Assert.That(G.Panel, Is.EqualTo("map"), "推进中地图保留在浮窗下");
        await run;
        Assert.That(S.CurrentLocation, Is.EqualTo("h02_square"), "起点核对：已到达");
        Assert.That(G.Panel, Is.EqualTo("map"), "结束后回到地图");
        Assert.That((G.MapPicked, G.MapAnchor), Is.EqualTo(((string?)null, "h02_square")), "按当前位置刷新：清所选目的地、锚到当前位置");
    }

    /// <summary>
    /// R390（S08 B35b Q1104 选 A 与「Q1104 验收」）：石刀缺纤维、从材料来源「在地图上定位」荒地——人物仍在营地；这张地图也能选相邻地点荒地并「出发」（按钮可用）。
    /// 走完后地图仍开着、按当前位置刷新；下层制作页随换场作废（Q586／Q590 不远程恢复），来源定位链结束，关闭键不再写「返回材料来源」，关闭即回原画面。
    /// 配对「直接点『返回材料来源』则回来源侧页」见 B35bCraftPageUiTests.Sources_LocateOnMap_BackToSourcesKeepsPick。
    /// </summary>
    [Test]
    public async Task LocatedMap_CanDepart_ThenPlainMapAtNewPlace()
    {
        S.Items.RemoveAll(i => i.ItemId is "stone" or "fiber");
        E.CreateItems(S, "stone", 1, S.Ground);
        S.Resources["field_fiber"].Discovered = true;
        S.Locations["field"].Visited = true;
        G.OpenHandCraft("knife");
        var craft = Ctx.RenderComponent<CraftOverlay>();
        craft.Find("[data-source-item=fiber]").Click();
        craft.Find("[data-pt=src-locate]").Click();
        var here = S.CurrentLocation;
        Assert.That((G.Panel, G.SourceItem, G.MapSourceSpot, G.MapPicked), Is.EqualTo(("map", "fiber", "field", (string?)null)), "起点：来源定位地图，只标来源");

        G.PickMapNode("field");
        var map = RenderMap();
        Assert.That(map.Find("[data-pt=map-close]").TextContent, Does.StartWith(MapOverlay.BackToSources), "定位地图关闭键仍是「返回材料来源」");
        var go = map.Find("[data-pt=map-depart]");
        Assert.That(go.HasAttribute("disabled"), Is.False, "Q1104 A：定位地图也能出发");
        Assert.That(S.CurrentLocation, Is.EqualTo(here), "选点不移动");

        await go.ClickAsync(new MouseEventArgs());
        for (var k = 0; k < 128 && E.DialogueLocked(S); k++) E.AckDialogue(S, E.CurrentDialogueLine(S)!.LineId);
        G.Notify();
        Assert.That(S.CurrentLocation, Is.EqualTo("field"), "起点核对：已到达");
        Assert.That(G.Panel, Is.EqualTo("map"), "走完回到地图");
        Assert.That((G.MapPicked, G.MapAnchor), Is.EqualTo(((string?)null, "field")), "按当前位置刷新");
        Assert.That((G.SourceItem, G.MapSourceSpot), Is.EqualTo(((string?)null, (string?)null)), "来源定位链随换场结束");
        map.Render();
        Assert.That(map.Find("[data-pt=map-close]").TextContent, Does.Not.Contain(MapOverlay.BackToSources));
        map.Find("[data-pt=map-close]").Click();
        Assert.That(G.Panel, Is.Null, "不远程恢复原制作页");
    }
}
