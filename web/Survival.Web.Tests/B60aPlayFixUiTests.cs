using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Survival.Core.Engine;
using Survival.Core.State;
using Survival.Web.Components;
using Survival.Web.Services;

namespace Survival.Web.Tests;

/// <summary>
/// B60a 试玩修复（S14「2026-11-24 B60 暂停窗入口与原页返回」Q1103 选 B「结束后……回到该页」「焦点沿 Q1038 回到仍合法的位置」）：
/// <list type="bullet">
/// <item>地图（普通层、城市层）出发走到后地图仍开着：全局右栏回到地图打开时的对象页（显示 BM12 Q1002 默认句「选相邻地点，再点“出发”。远处地点可查看路线。」），
/// 不因换地点清选中切到人物页；城市层「已到达{地点}。」照常给出（Q804、Q1005）。配对：不开地图移动，到达后右栏照旧回人物页（Q281）。</item>
/// <item>进度浮窗退出时，网页把原页仍合法的落点交给 app.js progLockEnd（原焦点已不在时用）：地图——当前位置节点、地图视口、地图窗；火堆菜单——菜单本身（取其中第一个可用控件）；
/// 休息页等其他页不给（沿原焦点所在区域就近找，现状不变）。落点真正取焦点由 app.js 做，bUnit 看不到，靠试玩与无头浏览器核对。</item>
/// </list>
/// </summary>
public class B60aPlayFixUiTests : WebTestBase
{
    private void Ok(ActionResult r) => Assert.That(r.Ok, Is.True, r.Reason);

    private void SetupMapJs()
    {
        Ctx.JSInterop.Setup<double[]>("survivalMap.size", _ => true).SetResult(new double[] { 900, 600 });
        Ctx.JSInterop.Setup<double[]>("survivalMap.getCenter", _ => true).SetResult(new double[] { 450, 300 });
    }

    private void AckAll()
    {
        for (var k = 0; k < 128 && E.DialogueLocked(S); k++) E.AckDialogue(S, E.CurrentDialogueLine(S)!.LineId);
        G.Notify();
    }

    /// <summary>城内建筑 h03_workshop、城市层开着、选相邻 h02_square（同 B60aPageReturnUiTests 起点；CITY-UI05 工坊→广场一段 5 分钟）。</summary>
    private void CityStart()
    {
        S.Progress.Flags.UnionWith(new[] { "registration_authorized", "hub_registered" });
        E.DebugMapTeleport(S, "h03_workshop");
        AckAll();
        G.OpenTopPanel("map");
        G.PickMapNode("h02_square");
        Assert.That(G.CityBrowsing, Is.True, "起点：城市层");
    }

    private object? LastLockEndArg()
    {
        var end = Ctx.JSInterop.Invocations.Where(i => i.Identifier == "survival.progLockEnd").ToList();
        Assert.That(end, Is.Not.Empty, "起点核对：浮窗退出时通知了网页");
        return end[^1].Arguments.Count == 0 ? null : end[^1].Arguments[0];
    }

    /// <summary>
    /// 复现 1（城市层）：营区工坊选营区广场、点「出发」——推进中右栏仍是地图的对象页；走到后地图仍开着，右栏是对象页、显示地图默认句（BM12 Q1002 copy.noSelection，<see cref="MapCopy.DefaultHint"/>），
    /// 不是人物页；「已到达营区广场。」照常给出（Q804 <see cref="CityCopy.Arrived"/>）。
    /// </summary>
    [Test]
    public async Task CityDepart_Arrived_RightRailBackToMapObjectPage()
    {
        CityStart();
        SetupMapJs();
        var map = Ctx.RenderComponent<MapOverlay>();
        var rail = Ctx.RenderComponent<DetailPanel>();
        Assert.That(G.DetailTab, Is.EqualTo(DetailTab.Object), "起点：地图打开时右栏是对象页");

        var run = map.Find("[data-pt=city-depart]").ClickAsync(new MouseEventArgs());
        Assert.That((G.Running, G.Panel, G.DetailTab), Is.EqualTo((true, "map", DetailTab.Object)), "推进中地图连同右栏对象页留在浮窗下（Q1103 保留原页）");
        await run;
        AckAll();
        Assert.That((S.CurrentLocation, S.Main, G.Panel), Is.EqualTo(("h02_square", (ActionState?)null, "map")), "起点核对：已到达、地图仍开着");
        Assert.That(G.DetailTab, Is.EqualTo(DetailTab.Object), "右栏回到地图打开时的对象页");
        rail.Render();
        Assert.That(rail.FindAll(".detail-tabs button.on").Select(b => b.TextContent), Is.EqualTo(new[] { ShellText.ObjectTab }), "页签高亮在「对象」");
        Assert.That(rail.Find(".detail-body").TextContent, Does.Contain(MapCopy.DefaultHint), "显示地图默认句");
        Assert.That(G.Notice, Is.EqualTo(CityCopy.Arrived(E.LocName("h02_square"))), "「已到达」照常");
    }

    /// <summary>
    /// Q1005 A 定稿（R315）、Q804：从城市层点「出发」且实际到达后显示「已到达{地点}。」——地图仍开着、右栏回对象页时，这句在右栏可见（地图默认句同在）。
    /// </summary>
    [Test]
    public async Task CityDepart_Arrived_ArrivalNoteVisibleOnObjectPage()
    {
        CityStart();
        SetupMapJs();
        var map = Ctx.RenderComponent<MapOverlay>();
        var rail = Ctx.RenderComponent<DetailPanel>();
        await map.Find("[data-pt=city-depart]").ClickAsync(new MouseEventArgs());
        AckAll();
        rail.Render();
        Assert.That((S.CurrentLocation, G.Panel, G.DetailTab), Is.EqualTo(("h02_square", "map", DetailTab.Object)), "起点核对：已到达、地图开着、右栏对象页");
        Assert.That(rail.Find("[data-pt=map-arrived]").TextContent, Is.EqualTo("已到达营区广场。"), "Q1005：到达句可见");
        Assert.That(rail.Find(".detail-body").TextContent, Does.Contain(MapCopy.DefaultHint));
    }

    /// <summary>配对（现状不变：出发失败的原因只在路线区，Q915／B35c）：城市层对不相邻的鉴定所出发被拒——当前反馈是拒绝原因，右栏不出到达位。</summary>
    [Test]
    public async Task CityDepart_Rejected_ReasonNotInArrivalSlot()
    {
        CityStart();
        var rail = Ctx.RenderComponent<DetailPanel>();
        await G.MapDepart("h05_appraisal", "rq-b60a-far");
        rail.Render();
        Assert.That((S.CurrentLocation, S.Main), Is.EqualTo(("h03_workshop", (ActionState?)null)), "起点核对：没走");
        Assert.That(G.Notice, Is.Not.Null, "起点核对：有拒绝原因");
        Assert.That(rail.FindAll("[data-pt=map-arrived]"), Is.Empty);
    }

    /// <summary>配对（Q1005 A「普通地图沿原反馈不加」）：普通地图出发走到荒地——右栏没有「已到达…」。</summary>
    [Test]
    public async Task MapDepart_Arrived_NoArrivalNote()
    {
        S.Locations["field"].Visited = true;
        G.OpenTopPanel("map");
        G.PickMapNode("field");
        SetupMapJs();
        var map = Ctx.RenderComponent<MapOverlay>();
        var rail = Ctx.RenderComponent<DetailPanel>();
        await map.Find("[data-pt=map-depart]").ClickAsync(new MouseEventArgs());
        AckAll();
        rail.Render();
        Assert.That((S.CurrentLocation, G.Panel, G.DetailTab), Is.EqualTo(("field", "map", DetailTab.Object)), "起点核对");
        Assert.That(rail.FindAll("[data-pt=map-arrived]"), Is.Empty);
        Assert.That(rail.Find(".detail-body").TextContent, Does.Not.Contain("已到达"));
    }

    /// <summary>复现 1（主世界）：营地开普通地图、选相邻荒地、点「出发」走到——地图仍开着，右栏对象页显示地图默认句（BM12 Q1002）。</summary>
    [Test]
    public async Task MapDepart_Arrived_RightRailBackToMapObjectPage()
    {
        S.Locations["field"].Visited = true;
        G.OpenTopPanel("map");
        G.PickMapNode("field");
        SetupMapJs();
        var map = Ctx.RenderComponent<MapOverlay>();
        var rail = Ctx.RenderComponent<DetailPanel>();
        Assert.That(G.CityBrowsing, Is.False, "起点：普通地图");

        await map.Find("[data-pt=map-depart]").ClickAsync(new MouseEventArgs());
        AckAll();
        Assert.That((S.CurrentLocation, G.Panel), Is.EqualTo(("field", "map")), "起点核对：已到达、地图仍开着");
        Assert.That(G.DetailTab, Is.EqualTo(DetailTab.Object));
        rail.Render();
        Assert.That(rail.Find(".detail-body").TextContent, Does.Contain(MapCopy.DefaultHint));
    }

    /// <summary>配对（Q281 换地点清选中、右栏回默认人物页）：不开地图、直接移动到荒地——到达后右栏是人物页。</summary>
    [Test]
    public async Task TravelWithoutMap_Arrived_RightRailCharacterAsBefore()
    {
        S.Locations["field"].Visited = true;
        Assert.That(G.Panel, Is.Null, "起点：没开地图");
        await G.StartAction(ActionRequest.Travel("field"));
        AckAll();
        Assert.That(S.CurrentLocation, Is.EqualTo("field"), "起点核对：已到达");
        Assert.That(G.DetailTab, Is.EqualTo(DetailTab.Character));
    }

    /// <summary>
    /// 复现 2（地图）：城市层出发走到后，浮窗退出时交给 app.js 的落点依次为当前位置节点（普通层 mn-{id}、城市层建筑 data-node）、地图视口、地图窗——
    /// 原「出发」键已随所选目的地清除而消失时焦点落在这里，不落到 body。
    /// </summary>
    [Test]
    public async Task MapDepart_LockEnd_GivesCurrentNodeThenMapContainer()
    {
        CityStart();
        SetupMapJs();
        var cut = Ctx.RenderComponent<GameScreen>();
        await cut.InvokeAsync(() => G.MapDepart("h02_square", "rq-b60a-fix"));
        AckAll();
        await cut.InvokeAsync(() => cut.Render());
        Assert.That((S.CurrentLocation, G.Panel, G.ProgressLock), Is.EqualTo(("h02_square", "map", false)), "起点核对：已到达、浮窗已退出");
        Assert.That(LastLockEndArg(), Is.EqualTo(new[] { "[id=\"mn-h02_square\"]", ".city-bldg[data-node=\"h02_square\"]", ".map-viewport", ".map-panel" }));
        Assert.That(cut.FindAll(".city-bldg[data-node=\"h02_square\"]"), Has.Count.EqualTo(1), "落点在页面上真实存在：城市层当前位置建筑");
    }

    /// <summary>复现 2（火堆菜单）：火堆菜单开着时开工、结束后回到菜单——落点是菜单本身（app.js 取其中第一个可用控件），原点火盘确认键不在时不落到 body。</summary>
    [Test]
    public async Task FireMenu_LockEnd_GivesMenuAsFallback()
    {
        G.OpenFireMenu();
        var cut = Ctx.RenderComponent<GameScreen>();
        await cut.InvokeAsync(() => G.StartAction(ActionRequest.Wait(30)));
        await cut.InvokeAsync(() => cut.Render());
        Assert.That((S.Main, G.Panel, G.ProgressLock), Is.EqualTo(((ActionState?)null, GameSession.FireMenuPanel, false)), "起点核对：已结束、回到火堆菜单");
        Assert.That(LastLockEndArg(), Is.EqualTo(new[] { ".fire-menu" }));
        Assert.That(cut.FindAll(".fire-menu"), Has.Count.EqualTo(1));
    }

    /// <summary>
    /// 配对：休息页开工、结束回休息页。B36a 改（开发方 D1＝02 Q1123 A「行动结束回到休息页时……焦点按 Q1038，不落在开始键」）：
    /// 原期望「不另给落点（空）」改为给本页落点——所选时长，没有就取本页第一个可用控件（开始键标 data-lock-noreturn 不拿回焦点）。
    /// </summary>
    [Test]
    public async Task RestPage_LockEnd_NoExtraFallback()
    {
        G.OpenTopPanel("rest");
        var cut = Ctx.RenderComponent<GameScreen>();
        await cut.InvokeAsync(() => G.StartAction(ActionRequest.Wait(30)));
        await cut.InvokeAsync(() => cut.Render());
        Assert.That((S.Main, G.Panel), Is.EqualTo(((ActionState?)null, "rest")), "起点核对：已结束、回到休息页");
        Assert.That(LastLockEndArg(), Is.EqualTo(new[] { ".rest-panel [data-pt=rest-pick].on", ".rest-panel" }));
    }
}
