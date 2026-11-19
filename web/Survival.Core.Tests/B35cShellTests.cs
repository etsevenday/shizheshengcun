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
        // BM12：02 Q1002 A 定稿（合同 b35c-q1001-q1006 copy.noSelection）引号写“出发”；原期望为 Q803 暂用“‘出发’”
        Assert.That(MapCopy.DefaultHint, Is.EqualTo("选相邻地点，再点“出发”。远处地点可查看路线。"), "Q803 默认（Q1002 A 定稿）");
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
        // B35c 试玩修复改写法（动作条移到路线区底部，预估在开头算一次）：仍是与原节点旁浮窗同一移动请求的预估
        Assert.That(route, Does.Contain("var p = to != null && route.Adjacent ? G.Preview(ActionRequest.Travel(to, needExp)) : null;"), "与原节点旁浮窗同一预估请求");
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
        // B35c 试玩修复改写法：远处的灰“出发”、原因与“选中下一段”同在底部动作条的非相邻分支（期望内容不变，只换所在位置）
        var remote = Slice(route, "<button type=\"button\" class=\"primary mr-depart\" disabled>@MapCopy.Depart</button>", "</aside>");
        Assert.That(remote, Does.Contain("@if (route.Blocked is string why) { <span class=\"mr-why\">@why</span> }"));
        Assert.That(remote, Does.Contain("@MapCopy.SelectNext(e.LocName(nx))"));
        Assert.That(Slice(route, "<div class=\"mr-actions\">", "</aside>"), Does.Contain("@MapCopy.SelectNext(e.LocName(nx))"));
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
        Assert.That(Web("Components/MapRouteArea.razor"), Does.Contain("<div class=\"mr-hint\">@MapCopy.DefaultHint</div>"), "路线区没选目的地也显示默认句（Q1002 A 定稿）");
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

    // ───────────── 城市层并入地图（Q595-A01、B35-Q804-01、B36-Q807／Q808／Q810-01、B35S-Q913／Q914-01） ─────────────

    /// <summary>Q595-A01：城市层就在区域地图里画建筑鸟瞰（不再另开 citymap 面板）；地点卡“主城鸟瞰”与首次到城都打开同一张地图的本城层。</summary>
    [Test]
    public void Q595_A01_CityLayerInsideMap()
    {
        var map = Web("Components/MapOverlay.razor");
        Assert.That(map, Does.Contain("var cityLayer = v.LayerKey.StartsWith(\"city:\") ? v.LayerKey.Substring(5) : null;"));
        Assert.That(map, Does.Contain("<CityOverview CityId=\"@cityLayer\" />"));
        var stage = Slice(map, "<CityOverview CityId=\"@cityLayer\" />", "<div class=\"map-foot\"");
        Assert.That(stage, Does.Contain("<div class=\"map-viewport\" @ref=\"_vp\""), "普通层才画可拖动画布");
        Assert.That(stage, Does.Contain("@if (cityLayer == null)").And.Contain("<MapRouteArea OnPickNext=\"PickNext\" />"), "城市层出发在层内右列，普通层用路线区");
        var screen = Web("Components/GameScreen.razor");
        Assert.That(Code(screen), Does.Not.Contain("<CityOverview").And.Not.Contain("\"citymap\""), "独立城市面板撤掉");
        Assert.That(Web("Services/GameSession.PanelStack.cs") + Web("Services/GameSession.Keys.cs"), Does.Not.Contain("citymap").And.Not.Contain("CityMap"));
        var session = Web("Services/GameSession.cs");
        Assert.That(Slice(session, "public static bool IsTopPanel(", ";"), Does.Not.Contain("citymap"));
        var open = Slice(session, "private void OpenMapCityLayer(string city)", "public void HoverCity(");
        Assert.That(open, Does.Contain("if (Panel != \"map\") OpenPanel(\"map\");").And.Contain("MapLayer = \"city:\" + city;").And.Contain("MapPicked = null;"));
        Assert.That(Slice(session, "public void ToggleCityOverview()", "private void OpenMapCityLayer("), Does.Contain("OpenMapCityLayer(city!);").And.Contain("if (city != null && MapCityShown == city) { OpenPanel(null); return; }"));
        var tick = Slice(session, "private void CityOverviewTick()", "public static bool IsTopPanel(");
        Assert.That(tick, Does.Contain("var legacy = E.MarkCityOverviewShown(S);").And.Contain("OpenMapCityLayer(city);"), "首次到城一次触发开同一地图本城层（Q804／Q807 A）");
        var card = Web("Components/LocationCard.razor");
        Assert.That(card, Does.Contain("var cityOn = G.MapCityShown == e.CityOf(LocationId);").And.Contain("@onclick=\"() => G.ToggleCityOverview()\""));
    }

    /// <summary>
    /// B35-Q804-01：城市层标题用地图层名；撤“返回当前地点”“进入当前地点”与旧会话状态；只有从城市层出发并真实到达才提示“已到达{地点}。”。
    /// 底部三键「主世界」「势力鸟瞰」「回到当前位置」（Q595 底部按钮，生成界面.cjs 原词），城市层里不显示「势力鸟瞰」，缩放／总览／图例只属普通层。
    /// </summary>
    [Test]
    public void Q804_CityLayerChrome_FootButtons()
    {
        Assert.That((MapCopy.World, MapCopy.FactionView), Is.EqualTo(("主世界", "势力鸟瞰")), "Q595 底部按钮原词");
        var city = Web("Components/CityOverview.razor");
        Assert.That(city, Does.Contain("<h2 class=\"city-title\">@e.MapLayerName(\"city:\" + v.CityId)</h2>"));
        var cityCode = Code(city);
        foreach (var old in new[] { "返回当前地点", "进入当前地点", "EnterCurrentFromCity", "CityGo(", "主城鸟瞰</" })
            Assert.That(cityCode, Does.Not.Contain(old), old);
        var session = Web("Services/GameSession.cs");
        foreach (var old in new[] { "CityTarget", "EnterCurrentFromCity", "public async Task CityGo(", "PickCity(" })
            Assert.That(session, Does.Not.Contain(old), old);
        var depart = Slice(session, "public async Task MapDepart(", "private void CityOverviewTick()");
        Assert.That(depart, Does.Contain("var fromCity = CityBrowsing;"));
        Assert.That(depart.IndexOf("if (State == null || !fromCity) return;", StringComparison.Ordinal), Is.GreaterThan(depart.IndexOf("await RunLoop();", StringComparison.Ordinal)), "跑完这一段再看是否真实到达");
        Assert.That(depart, Does.Contain("if (E.CityArrivalNote(S, targetId) is string note)"));
        var map = Web("Components/MapOverlay.razor");
        Assert.That(map, Does.Contain("var factionLayer = cityLayer == null ? FactionLayer(layers) : null;"), "城市层里不显示势力鸟瞰");
        var foot = Slice(map, "<div class=\"map-foot\"", "</div>");
        var world = foot.IndexOf("@MapCopy.World", StringComparison.Ordinal);
        var faction = foot.IndexOf("@MapCopy.FactionView", StringComparison.Ordinal);
        Assert.That(world, Is.GreaterThan(0));
        Assert.That(faction, Is.GreaterThan(world));
        Assert.That(foot.IndexOf("data-map-cmd=\"here\"", StringComparison.Ordinal), Is.GreaterThan(faction), "回到当前位置在底部第三键（0 键同一按钮）");
        Assert.That(Regex.Matches(map, "data-map-cmd=\"here\"").Count, Is.EqualTo(1), "工具条上不再重复");
        var tools = Slice(map, "@if (cityLayer == null)", "<button type=\"button\" class=\"secondary\" @onclick=\"Close\">");
        Assert.That(tools, Does.Contain("data-map-cmd=\"out\"").And.Contain("data-map-cmd=\"in\"").And.Contain(">总览</button>").And.Contain(">图例</button>"));
        var pickFirst = Slice(map, "private string? FactionLayer(", "private void ToggleLegend()");
        Assert.That(pickFirst.IndexOf("G.MapPicked is string p && G.E.IsCityParent(p)", StringComparison.Ordinal), Is.LessThan(pickFirst.IndexOf("G.E.CityOf(G.S.CurrentLocation)", StringComparison.Ordinal)), "Q1004 A 定稿：先所选城，再所在城");
    }

    /// <summary>B36-Q807-01：城市层盖①②③（tall-panel、进度卡沿原鸟瞰位置），④随身只读判据 CityBrowsing 只看“地图开着且显示城市层”；普通主世界／地牢层不误锁。</summary>
    [Test]
    public void Q807_CityLayerCoversRows_ReadOnlyOnlyOnCityLayer()
    {
        var session = Web("Services/GameSession.cs");
        Assert.That(session, Does.Contain("public bool CityBrowsing => MapCityShown != null;"));
        var shown = Slice(session, "public string? MapCityShown", "public const string CityReadOnlyText");
        Assert.That(shown, Does.Contain("if (State == null || Panel != \"map\") return null;"));
        Assert.That(shown, Does.Contain("var layer = E.ResolveMapLayer(S, MapLayer);").And.Contain("var city = layer.StartsWith(\"city:\") ? layer.Substring(5) : null;"));
        var screen = Web("Components/GameScreen.razor");
        Assert.That(screen, Does.Contain("<section class=\"center @(G.CityBrowsing ? \"city-open\" : \"\")\""));
        Assert.That(screen, Does.Contain("G.Panel is \"craft\" or \"safehouse\" or \"drying\" || G.CityBrowsing ||"));
        Assert.That(screen, Does.Contain("case EscOwner.MapPick: G.PickMapNode(null); break;"));
        Assert.That(Web("Services/GameSession.Keys.cs"), Does.Contain("if (Panel == \"map\" && MapPicked != null && SourceItem == null) return EscOwner.MapPick;"));
    }

    /// <summary>B36-Q808-01：城市层右列顺序——所选建筑（原状态词）／当前位置／下一路段，后接完整路线与人物、服务、任务；未选写 P07b 原句；不加右列标题。</summary>
    [Test]
    public void Q808_CitySideOrder()
    {
        Assert.That((CityCopy.PickedTitle, CityCopy.NoPick), Is.EqualTo(("所选建筑", "选择图上的建筑查看路线。")), "逐字稿 P07b");
        var side = Slice(Web("Components/CityOverview.razor"), "<aside class=\"city-side\"", "</aside>");
        int At(string x) { var i = side.IndexOf(x, StringComparison.Ordinal); Assert.That(i, Is.GreaterThanOrEqualTo(0), x); return i; }
        Assert.That(At("@CityCopy.PickedTitle"), Is.LessThan(At("@MapCopy.Here")));
        Assert.That(At("@MapCopy.Here"), Is.LessThan(At("@MapCopy.NextLegTitle")));
        Assert.That(At("@MapCopy.NextLegTitle"), Is.LessThan(At(">@MapCopy.Depart</button>")));
        Assert.That(At(">@MapCopy.Depart</button>"), Is.LessThan(At("路线：@string.Join")));
        Assert.That(At("路线：@string.Join"), Is.LessThan(At("人物：")));
        Assert.That(At("人物："), Is.LessThan(At("服务：")));
        Assert.That(side, Does.Contain("<div class=\"ci-hint\">@CityCopy.NoPick</div>"));
        Assert.That(side, Does.Contain("@MapCopy.NextLeg(e.LocName(leg.From), e.LocName(leg.To), leg.Minutes)"));
        Assert.That(side, Does.Not.Contain("<h2").And.Not.Contain("<h3"), "不加右列标题");
    }

    /// <summary>B36-Q810-01：远城只读——右列只写 Q810 原句，不给下一路段与出发（远城分支在出发分支之前且互斥）。</summary>
    [Test]
    public void Q810_RemoteCityReadOnly()
    {
        Assert.That(CityCopy.RemoteOnly, Is.EqualTo("仅供查看；前往请用主世界地图。"), "Q810 答复原句");
        var side = Slice(Web("Components/CityOverview.razor"), "<aside class=\"city-side\"", "</aside>");
        var remote = Slice(side, "@if (v.Remote)", "else if (route?.TargetId is string to)");
        Assert.That(remote, Does.Contain("@CityCopy.RemoteOnly"));
        Assert.That(remote, Does.Not.Contain("MapCopy.Depart").And.Not.Contain("NextLeg"));
    }

    /// <summary>B35S-Q913-01：城市层只读拒绝用 Q913 定稿句（拖拽、卡片浮窗、会话拦截同一常量）；只在城市层生效（判据见 Q807 用例）。</summary>
    [Test]
    public void Q913_ReadOnlyText()
    {
        Assert.That(CityCopy.ReadOnly, Is.EqualTo("在城市层只能查看；关闭地图后再操作。"), "Q913 A 定稿句");
        Assert.That(Web("Services/GameSession.cs"), Does.Contain("public const string CityReadOnlyText = CityCopy.ReadOnly;"));
        Assert.That(Web("Components/CardPop.razor"), Does.Contain("@GameSession.CityReadOnlyText"));
        Assert.That(Web("Services/DragController.cs"), Does.Contain("if (G.CityBrowsing) return Result(false, GameSession.CityReadOnlyText);"));
    }

    /// <summary>B35S-Q914-01：城市层删多段“本次先走…到达后请再次选择下一段”整句且不加替句；远处只看路线，仍要明确改选相邻站（选中下一段）。</summary>
    [Test]
    public void Q914_NoMultiLegConfirmSentence()
    {
        var city = Code(Web("Components/CityOverview.razor"));
        foreach (var old in new[] { "本次先走", "到达后请再次选择下一段", "远处地点仅供查看。", "CityCopy.Confirm" })
            Assert.That(city, Does.Not.Contain(old), old);
        var side = Slice(city, "<aside class=\"city-side\"", "</aside>");
        var far = Slice(side, "<button type=\"button\" class=\"primary\" disabled>@MapCopy.Depart</button>", "@if (G.Notice != null && _tried)");
        Assert.That(far, Does.Contain("@if (route.Blocked is string why)").And.Contain("@onclick=\"() => Pick(nx)\">@MapCopy.SelectNext(e.LocName(nx))</button>"));
    }

    // ───────────── 系统说明（B35S-Q916-01） ─────────────

    /// <summary>
    /// B35S-Q916-01：系统说明三处移动帮助（就近执行、移动、分层地图）同用 Q916 批准句，分别指地点栏卡浮窗与区域地图路线区“出发”；
    /// 旧“在旁边的浮窗里确认移动”等句删除；句子只出现在这三处（不到处追加）。就近执行段是分号串列，句末句号随列表改分号；分层地图“右栏会列出”改“路线区会列出”（Q1001 A 定稿）。
    /// </summary>
    [Test]
    public void Q916_HelpThreePlaces()
    {
        const string q916 = "点相邻地图卡在浮窗里确认移动；在区域地图上，先选相邻地点，在路线区点“出发”。";   // S14 2026-11-17 B35 次要节 Q916 A 原句
        Assert.That(MapCopy.Help, Is.EqualTo(q916));
        var body = q916.TrimEnd('。');
        var help = Web("Services/HelpCatalog.cs");
        string Entry(string id) => Slice(help, $"Id = \"{id}\"", "new HelpEntry");
        Assert.That(Entry("nearby"), Does.Contain("按钮就在对象卡上或卡旁的小浮窗里：" + body + "；点击敌踪卡打开操作盘"));
        Assert.That(Entry("travel"), Does.Contain("\"一次只能沿一条已知路线走到相邻地点，没有自动寻路。" + q916 + "\","));
        Assert.That(Entry("maplayers"), Does.Contain("\"点远处的已知地点，路线区会列出实际能走的路线和每段时间。" + q916 + "浏览、缩放、切层都不推进游戏时间。\","));
        Assert.That(Regex.Matches(help, Regex.Escape(body)).Count, Is.EqualTo(3), "只在原三处");
        foreach (var old in new[] { "在旁边的浮窗里确认移动", "区域地图上的相邻地点或“前往”", "但一次只能确认下一段相邻路线" })
            Assert.That(help, Does.Not.Contain(old), old);
        Assert.That(Web("Components/ActionPops.razor"), Does.Contain("case \"travel\":"), "地点卡浮窗入口照旧（句子前半所指）");
    }

    // ───────────── 试玩修复（B35c 本地 Release 包试玩） ─────────────

    /// <summary>试玩修复 1（1280×720 路线区约 200px）：信息区单独滚动，“出发”、原因、“选中下一段”在底部固定动作条，不用滚动即可见。</summary>
    [Test]
    public void PlayFix1_RouteActionsPinned()
    {
        var route = Code(Web("Components/MapRouteArea.razor"));
        var info = route.IndexOf("<div class=\"mr-info\">", StringComparison.Ordinal);
        var actions = route.IndexOf("<div class=\"mr-actions\">", StringComparison.Ordinal);
        Assert.That(info, Is.GreaterThan(0));
        Assert.That(actions, Is.GreaterThan(info));
        var bar = Slice(route, "<div class=\"mr-actions\">", "</aside>");
        Assert.That(bar, Does.Contain("@(needExp ? \"确认开始远征并前往\" : MapCopy.Depart)").And.Contain(">终止本次远征…</button>").And.Contain("@MapCopy.SelectNext(e.LocName(nx))"));
        Assert.That(route[info..actions], Does.Not.Contain("mr-depart").And.Not.Contain("SelectNext"), "按钮不在可滚动的信息区里");
        var css = Web("wwwroot/css/fourrows.css");
        Assert.That(css, Does.Contain(".map-route .mr-info { flex: 1 1 auto; min-height: 0; overflow-y: auto;"));
        Assert.That(css, Does.Contain(".map-route .mr-actions { flex: 0 0 auto;"));
        Assert.That(Regex.Match(css, @"(?m)^\.map-route \{[^}]*\}").Value, Does.Contain("overflow: hidden;").And.Not.Contain("overflow-y: auto"));
    }

    /// <summary>试玩修复 2：切层（主世界、势力鸟瞰、回到当前位置、面包屑、下拉、路口标签、分区定位）后，所选目的地在新层看不见就取消，右栏与 Esc 跟可见层一致。</summary>
    [Test]
    public void PlayFix2_LayerSwitchDropsHiddenPick()
    {
        var session = Web("Services/GameSession.cs");
        var drop = Slice(session, "public void DropHiddenMapPick()", "/// <summary>地图上点选／取消所选目的地");
        Assert.That(drop, Does.Contain("? E.CityMembers(layer.Substring(5)).Contains(id)").And.Contain(": E.BuildMapView(S, MapLayer).Nodes.Any(n => n.Id == id);"));
        Assert.That(drop, Does.Contain("if (visible) return;").And.Contain("MapPicked = null;"));
        var map = Web("Components/MapOverlay.razor");
        foreach (var m in new[] { ("private void SwitchLayer(", "private async Task Zoom("), ("private void GoRegion(", "/// <summary>切换查看的层"), ("private void BackToHere()", "private void ToggleLegend()") })
            Assert.That(Slice(map, m.Item1, m.Item2), Does.Contain("G.DropHiddenMapPick();"), m.Item1);
    }

    /// <summary>试玩修复 3（Q807／Q913）：城市层只读卡片浮窗按内容高度显示；右栏物品详情不显示“转移目标与数量…”操作提示（不另加文字）。</summary>
    [Test]
    public void PlayFix3_ReadOnlyCardPopFits()
    {
        Assert.That(Web("Components/CardPop.razor"), Does.Contain("+ (G.CityBrowsing ? \" city-ro\" : \"\") +"));
        Assert.That(Web("wwwroot/css/cityoverview.css"), Does.Contain(".wasteland .apop.card-pop.apop-c.city-ro { height: fit-content;"));
        Assert.That(Web("Components/ItemDetail.razor"), Does.Contain("@if (G.Panel != GameSession.FireMenuPanel && G.InFireCrumbs(sel) == null && !G.CityBrowsing)"));
    }

    /// <summary>试玩修复 4：城市层楼栋、道路与路标排在标题下方的绘图区里，左上楼栋选中框不压在标题下。</summary>
    [Test]
    public void PlayFix4_CityPlotBelowTitle()
    {
        var city = Web("Components/CityOverview.razor");
        var plot = Slice(city, "<div class=\"city-plot\">", "<aside class=\"city-side\"");
        Assert.That(plot, Does.Contain("<svg class=\"city-roads\"").And.Contain("class=\"road-tag").And.Contain("class=\"city-bldg"));
        Assert.That(city.IndexOf("<h2 class=\"city-title\">", StringComparison.Ordinal), Is.LessThan(city.IndexOf("<div class=\"city-plot\">", StringComparison.Ordinal)));
        Assert.That(Web("wwwroot/css/cityoverview.css"), Does.Contain(".city-plot { position: absolute; left: 0; right: 0; top: 34px; bottom: 0; }"));
    }
}
