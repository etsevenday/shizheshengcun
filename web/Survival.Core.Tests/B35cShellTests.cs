using System.Text.RegularExpressions;
using Survival.Core.Engine;

namespace Survival.Core.Tests;

/// <summary>
/// 批次 B35c（地图出发与城市层并入）网页壳接线。测试工程不引用网页壳，接线按组件源码核对（同 B42HoverPeekTests）。
/// 规则来源：S14 2026-11-13 B35 节 Q591／Q595，2026-11-16 B35 节 Q793／Q803／Q804，B36 节 Q807／Q808／Q810，
/// 2026-11-17 B35 次要节 Q913—Q916、B35a1 主要节 Q931；逐字稿 §2 P07a／P07b。定稿句在本文件写字面量逐字比对。
/// </summary>
public class B35cShellTests
{
    private static string Root()
    {
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "doc", "策划案"))) dir = dir.Parent;
        Assert.That(dir, Is.Not.Null, "找不到仓库根目录");
        return dir!.FullName;
    }

    private static string Web(string relative) => File.ReadAllText(Path.Combine(Root(), "web", "Survival.Web", relative));

    private static string Slice(string text, string from, string to)
    {
        var a = text.IndexOf(from, StringComparison.Ordinal);
        Assert.That(a, Is.GreaterThanOrEqualTo(0), from);
        var b = text.IndexOf(to, a, StringComparison.Ordinal);
        Assert.That(b, Is.GreaterThan(a), to);
        return text[a..b];
    }

    /// <summary>去掉 Razor 注释（@* … *@）与 // 行注释，只看真正渲染／执行的源码。</summary>
    private static string Code(string src) => Regex.Replace(Regex.Replace(src, @"@\*.*?\*@", "", RegexOptions.Singleline), @"(?m)^\s*//.*$", "");

    // ───────────── 定稿句（逐字） ─────────────

    /// <summary>Q591、Q803、Q915、P07a：路线区与右栏用到的定稿句逐字。</summary>
    [Test]
    public void MapCopy_Verbatim()
    {
        Assert.That(MapCopy.Depart, Is.EqualTo("出发"), "Q591");
        Assert.That(MapCopy.DefaultHint, Is.EqualTo("选相邻地点，再点‘出发’。远处地点可查看路线。"), "Q803 默认");
        Assert.That(MapCopy.FootNote, Is.EqualTo("在路线区点‘出发’；耗时为预计值。"), "Q803 脚注");
        Assert.That(MapCopy.PickAdjacent, Is.EqualTo("先选相邻地点。"), "Q915 B");
        Assert.That((MapCopy.Here, MapCopy.Target, MapCopy.NextLegTitle, MapCopy.RoutesTitle), Is.EqualTo(("当前位置", "所选目的地", "下一路段", "已知路线")), "逐字稿 P07a");
        Assert.That(MapCopy.NextLeg("营区工坊", "营区广场", 5), Is.EqualTo("营区工坊→营区广场 · 基础5分钟"), "Q808 下一路段原格式");
    }

    // ───────────── 地图路线区（Q591-A01／A02、B35-Q793-01、B35-Q803-01、B35S-Q915-01） ─────────────

    /// <summary>Q591-A01：点地图节点只选中，不再在节点旁打开确认浮窗；浮窗里的 mapnode 分支撤掉（地点卡“前往 ▸”浮窗保留）。</summary>
    [Test]
    public void Q591_A01_PickOnly_NoNodePop()
    {
        var map = Web("Components/MapOverlay.razor");
        var pick = Slice(map, "private void Pick(string id)", "private void PickNext(");
        Assert.That(pick, Does.Contain("G.MapPicked = id;").And.Contain("G.ClosePop();"));
        Assert.That(Code(map), Does.Not.Contain("OpenPop("), "地图上不开浮窗");
        var pops = Web("Components/ActionPops.razor");
        Assert.That(Code(pops), Does.Not.Contain("\"mapnode\""));
        Assert.That(pops, Does.Contain("case \"travel\":"), "地点卡相邻浮窗照旧（Q916 前半句）");
    }

    /// <summary>Q591-A01：路线区在地图面板内；区名、唯一普通按钮“出发”、同源预估（G.Preview 同一移动请求）与成功收图（MapDepart 清整条链）。</summary>
    [Test]
    public void Q591_A01_RouteAreaInPanel()
    {
        var map = Web("Components/MapOverlay.razor");
        Assert.That(map, Does.Contain("<MapRouteArea OnPickNext=\"PickNext\" />"));
        var route = Web("Components/MapRouteArea.razor");
        Assert.That(route, Does.Contain("@MapCopy.Here").And.Contain("@MapCopy.Target").And.Contain("@MapCopy.NextLegTitle").And.Contain("@MapCopy.RoutesTitle"));
        Assert.That(route, Does.Contain("var p = G.Preview(ActionRequest.Travel(to, needExp));"), "与原节点旁浮窗同一预估请求");
        Assert.That(route, Does.Contain("@(needExp ? \"确认开始远征并前往\" : MapCopy.Depart)"), "Q591-A02：远征进入保留原确认文字");
        Assert.That(route, Does.Contain(">终止本次远征…</button>"), "Q591-A02：未通关离开保留原入口");
        Assert.That(route, Does.Not.Contain("前往下一站"));
        var session = Web("Services/GameSession.cs");
        var depart = Slice(session, "public async Task MapDepart(", "await RunLoop();");
        Assert.That(depart, Does.Contain("E.StartMapDeparture(S, targetId, requestId)").And.Contain("CloseAllPanels();"));
        Assert.That(depart.IndexOf("if (!res.Ok)", StringComparison.Ordinal), Is.LessThan(depart.IndexOf("CloseAllPanels();", StringComparison.Ordinal)), "失败留在原页");
    }

    /// <summary>B35-Q793-01、B35S-Q915-01：远处只看路线——出发灰，旁边写拦截原因（远处为“先选相邻地点。”，其他真实原因照原）；“选中下一段”在路线区。</summary>
    [Test]
    public void Q793_RemoteDisabled_SelectNextInRouteArea()
    {
        var route = Code(Web("Components/MapRouteArea.razor"));
        var remote = Slice(route, "@if (!route.Adjacent && route.Path is { Count: > 0 } path)", "@if (route.Adjacent)");
        Assert.That(remote, Does.Contain("@MapCopy.SelectNext(e.LocName(nx))"));
        var tail = route[route.LastIndexOf("else", StringComparison.Ordinal)..];
        Assert.That(tail, Does.Contain("<button type=\"button\" class=\"primary mr-depart\" disabled>@MapCopy.Depart</button>"));
        Assert.That(tail, Does.Contain("@if (route.Blocked is string why) { <span class=\"mr-why\">@why</span> }"));
    }

    /// <summary>B35-Q803-01、B35S-Q915-01：右栏默认句与脚注换短稿；旧浮窗句、副标与两条“不提供自动寻路”句删除；右栏不再给“选中下一段”（Q591 右栏只读）。</summary>
    [Test]
    public void Q803_Q915_RightPanelTexts()
    {
        var map = Web("Components/MapOverlay.razor");
        Assert.That(map, Does.Contain("<div class=\"tiny muted\">@MapCopy.DefaultHint</div>"));
        Assert.That(map, Does.Contain("FootNote=\"@MapCopy.FootNote\""));
        foreach (var old in new[] { "地点旁出现移动确认", "在地图上该地点旁的浮窗确认移动", "一次只移动一段相邻路线，不提供自动寻路。", "不提供自动寻路：一次只能确认下一段。", "选中下一段：前往@" })
            Assert.That(map, Does.Not.Contain(old), old);
        Assert.That(Web("Components/MapRouteArea.razor"), Does.Contain("<div class=\"mr-hint\">@MapCopy.DefaultHint</div>"), "路线区没选目的地也显示默认句（Q1002 暂用 A）");
    }

    /// <summary>Q591-A01：可出发相邻连线按内核 Departable 描金并带微动态；prefers-reduced-motion 时保留静态描金（不按动画判定可达）；旧“与当前地点相连就描金”撤掉。</summary>
    [Test]
    public void Q591_A01_DepartableEdgeHighlight()
    {
        var map = Web("Components/MapOverlay.razor");
        Assert.That(map, Does.Contain("@(ed.Departable ? \"departable\" : \"\")"));
        Assert.That(map, Does.Not.Contain("@(ed.Near ? \"near\" : \"\")"));
        var css = Web("wwwroot/css/fourrows.css");
        Assert.That(css, Does.Contain(".map-canvas .map-edge.departable line { stroke: #f0cf7e; stroke-width: 5; animation: map-depart-pulse"));
        Assert.That(css, Does.Contain("@media (prefers-reduced-motion: reduce) { .map-canvas .map-edge.departable line { animation: none; } }"));
        Assert.That(css, Does.Not.Contain(".map-edge.near line"));
    }

    /// <summary>B35A1-Q931-06：地图各层都没有“安全屋返程”／改名“安全屋”的远程按钮，地图不打开安全屋面板；普通安全屋标记仍在内核视图（定位用）。</summary>
    [Test]
    public void Q931_06_NoSafehouseReturnButton()
    {
        foreach (var f in new[] { "Components/MapOverlay.razor", "Components/MapRouteArea.razor", "Components/CityOverview.razor" })
        {
            var code = Code(Web(f));
            Assert.That(code, Does.Not.Contain("返程"), f);
            Assert.That(code, Does.Not.Contain("OpenPanel(\"safehouse\")"), f);
            Assert.That(code, Does.Not.Contain(">安全屋<"), f);
        }
        var view = File.ReadAllText(Path.Combine(Root(), "src", "Core", "Engine", "GameEngine.MapView.cs"));
        Assert.That(view, Does.Contain("if (SafehouseBuilt(s, id)) n.Roles.Add(s.FavoriteSafehouse == id ? \"常用安全屋\" : \"安全屋\");"), "地图普通标记照旧");
    }
}
