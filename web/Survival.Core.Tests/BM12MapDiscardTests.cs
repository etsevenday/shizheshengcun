using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发记录 BM12「次要问题合并」：丢弃最后确认层（02 Q999，R313，合同 assets/20261118/b53b-q999-q1000-discard-and-freshness.json discardConfirmation，验收 B53B-Q999-01；Q1000 不在本批）
/// 与地图路线文案（02 Q1001—Q1006，R315，合同 assets/20261118/b35c-q1001-q1006-map-route-copy.json，验收 B35C-Q1001-01—Q1006-01）。
/// 期望句读合同 copy 段逐字比对；内核规则直接测，网页壳接线按源码核对（同 B35cShellTests）。
/// </summary>
public class BM12MapDiscardTests : TestBase
{
    protected override bool NoEnemyApproach => true;

    private static string Root()
    {
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "web", "Survival.sln"))) dir = dir.Parent;
        Assert.That(dir, Is.Not.Null, "找到仓库根目录");
        return dir!.FullName;
    }

    private static JObject Asset(string file) =>
        JObject.Parse(File.ReadAllText(Path.Combine(Root(), "doc", "策划案", "系统策划总册 v1.0", "assets", "20261118", file)));

    private static JObject MapContract() => Asset("b35c-q1001-q1006-map-route-copy.json");
    private static string Copy(string key) => (string)MapContract()["copy"]![key]!;

    private static string Web(params string[] parts) => File.ReadAllText(Path.Combine(new[] { Root(), "web", "Survival.Web" }.Concat(parts).ToArray()));

    /// <summary>去掉 Razor 注释（@* … *@）与 // 行注释，只看真正渲染／执行的源码。</summary>
    private static string Code(string src) => Regex.Replace(Regex.Replace(src, @"@\*.*?\*@", "", RegexOptions.Singleline), @"(?m)^\s*//.*$", "");

    private static string Slice(string text, string from, string to)
    {
        var i = text.IndexOf(from, StringComparison.Ordinal);
        Assert.That(i, Is.GreaterThanOrEqualTo(0), from);
        var j = text.IndexOf(to, i + from.Length, StringComparison.Ordinal);
        Assert.That(j, Is.GreaterThan(i), to);
        return text.Substring(i, j - i);
    }

    private GameState Registered(string loc)
    {
        var s = New();
        s.Progress.Flags.UnionWith(new[] { "registration_authorized", "hub_registered" });
        s.GameMinute = At(2, 9, 0);
        s.SpawnNextCheck = int.MaxValue;
        s.PatrolNextCheck = int.MaxValue;
        E.DebugMapTeleport(s, loc);
        ReadDialogues(s);
        return s;
    }

    // ═════════════ Q999 丢弃最后确认层 ═════════════

    /// <summary>
    /// B53B-Q999-01：野果共 5 份、其中 2 份被预留（可丢 3），本次选 2 份——主层与数量层的总数行“总数5，可丢弃3”；
    /// 最后确认只列本次真实清单与原警示两行，不写总数行（合同 discardConfirmation.confirmationShowsOverallAndAvailable=false）。
    /// </summary>
    [Test]
    public void Q999_ConfirmListsOnlyRealPlan_NoTotals()
    {
        var dc = (JObject)Asset("b53b-q999-q1000-discard-and-freshness.json")["discardConfirmation"]!;
        Assert.That(((bool)dc["selectionAndQuantityViewsShowOverallAndAvailable"]!, (bool)dc["confirmationShowsOverallAndAvailable"]!), Is.EqualTo((true, false)));
        var s = New();
        s.Items.RemoveAll(i => i.ItemId == "berry");
        var free = E.CreateItems(s, "berry", 5, s.Ground).Single();
        var held = E.SplitOff(s, free, 2);   // 起点：同组拆出 2 份
        held.ReservedBy = 9991;               // 起点：被行动预留
        var t = DiscardTarget.Group(s.Ground, "berry", null);
        var v = E.DiscardViewOf(s, t);
        Assert.That((v.Total, v.Available), Is.EqualTo((5, 3)));
        Assert.That(DiscardCopy.Totals(v.Total, v.Available), Is.EqualTo("总数5，可丢弃3"), "主层、数量层的总数行（§Q863 A）");
        var plan = E.DiscardPreview(s, t, 2, false, out var why);
        Assert.That(plan, Is.Not.Null, why);
        Assert.That(plan!.Count, Is.EqualTo(2));
        Assert.That(plan.Lines, Has.Count.EqualTo(2), "确认层：本次清单＋不可找回警示");
        Assert.That(plan.Lines[0], Does.StartWith("永久丢弃：野果×2"));
        Assert.That(plan.Lines[1], Is.EqualTo("丢弃后无法找回。"));
        Assert.That(plan.Lines.Any(l => l.Contains("总数") || l.Contains("可丢弃")), Is.False, "Q999 A：确认层不写总数行");
    }

    /// <summary>B53B-Q999-01（网页）：总数行只在主层、数量层两处渲染（对照），确认层段落里没有。</summary>
    [Test]
    public void Q999_Web_TotalsOnlyMenuAndQuantity()
    {
        var p = Code(Web("Components", "DiscardPanel.razor"));
        Assert.That(Slice(p, "case DiscardStage.Menu:", "case DiscardStage.Quantity:"), Does.Contain("<div class=\"discard-avail\">@totals</div>"), "主层（对照）");
        Assert.That(Slice(p, "case DiscardStage.Quantity:", "case DiscardStage.Confirm"), Does.Contain("<div class=\"discard-avail\">@totals</div>"), "数量层（对照）");
        var confirm = Slice(p, "case DiscardStage.Confirm", "@code {");
        Assert.That(confirm, Does.Not.Contain("@totals").And.Not.Contain("Totals("), "Q999 A：确认层不显示总数行");
        Assert.That(confirm, Does.Contain("@foreach (var line in open.Plan.Lines)"), "只列本次真实清单与警示");
    }

    // ═════════════ Q1001 系统说明三处移动句 ═════════════

    /// <summary>B35C-Q1001-01：就近执行只换移动分句（句末随串列为分号）；移动段第二句整句用定稿；分层地图依次 layerLead、move、layerEnd。</summary>
    [Test]
    public void Q1001_HelpThreePlaces_Contract()
    {
        var move = Copy("move");
        Assert.That(MapCopy.Help, Is.EqualTo(move));
        var help = Web("Services", "HelpCatalog.cs");
        string Entry(string id) => Slice(help, $"Id = \"{id}\"", "new HelpEntry");
        Assert.That(Entry("nearby"), Does.Contain("：" + move.TrimEnd('。') + "；"), "就近执行：移动分句末尾随原并列项用分号");
        Assert.That(Entry("travel"), Does.Contain("没有自动寻路。" + move + "\""), "移动：第二句整句");
        Assert.That(Entry("maplayers"), Does.Contain("\"" + Copy("layerLead") + move + Copy("layerEnd") + "\""), "分层地图：三句顺序");
    }

    // ═════════════ Q1002 普通地图路线区 ═════════════

    /// <summary>B35C-Q1002-01：未选目的地时写“选相邻地点，再点“出发”。远处地点可查看路线。”（copy.noSelection）；下一路段“{起}→{止} · 基础{N}分钟”；已知路线标题与逐段行格式。</summary>
    [Test]
    public void Q1002_RouteAreaCopy()
    {
        Assert.That(MapCopy.DefaultHint, Is.EqualTo(Copy("noSelection")));
        Assert.That(MapCopy.NextLeg("营区工坊", "营区广场", 5), Is.EqualTo(Copy("nextLeg").Replace("{起}", "营区工坊").Replace("{止}", "营区广场").Replace("{N}", "5")));
        Assert.That(MapCopy.RoutesTitle, Is.EqualTo(Copy("routeTitle")));
        Assert.That((Copy("routeSegment"), Copy("routeMinutes")), Is.EqualTo(("{起} → {止}", "{N}分钟")));
        var area = Code(Web("Components", "MapRouteArea.razor"));
        Assert.That(area, Does.Contain("<div class=\"mr-hint\">@MapCopy.DefaultHint</div>"));
        Assert.That(area, Does.Contain("@MapCopy.NextLeg(e.LocName(leg.From), e.LocName(leg.To), leg.Minutes)"));
        Assert.That(area, Does.Contain("<span>@e.LocName(st.From) → @e.LocName(st.To)</span><b>@(st.Minutes)分钟</b>"));
        // 真实路段与基础分钟来自路线数据（对照：工坊→广场→鉴定所两段各 5 分钟，CITY-UI05）
        var s = Registered("h03_workshop");
        var r = E.MapRouteOf(s, "h05_appraisal");
        Assert.That(r.Path!.Select(x => x.Minutes), Is.EqualTo(new[] { 5, 5 }));
    }

    // ═════════════ Q1003 图例 ═════════════

    /// <summary>B35C-Q1003-01：城市图例“道路只连已发现的城内真实路线。选相邻建筑，再点“出发”。”；普通地图首条图例原句。</summary>
    [Test]
    public void Q1003_Legends()
    {
        Assert.That(CityCopy.LegendRoads + CityCopy.LegendMove, Is.EqualTo(Copy("cityLegend")));
        Assert.That(Code(Web("Components", "CityOverview.razor")), Does.Contain("@CityCopy.LegendRoads@CityCopy.LegendMove"));
        var legend = Slice(Code(Web("Components", "MapOverlay.razor")), "<div class=\"map-legend-pop\" role=\"note\">", "</div>");
        Assert.That(legend, Does.Contain("<i class=\"lg-line open\"></i>" + Copy("worldLegend")), "首条图例");
    }

    // ═════════════ Q1004 势力鸟瞰 ═════════════

    /// <summary>B35C-Q1004-01：先看所选且可查看的已知主城父节点，所选城不可看则回退到可看的所在城；都不可用隐藏；城市层里隐藏。</summary>
    [Test]
    public void Q1004_FactionViewTarget()
    {
        var fc = (JObject)MapContract()["factionOverview"]!;
        Assert.That(((JArray)fc["candidatesInOrder"]!).Select(x => (string)x!), Is.EqualTo(new[] { "selectedKnownViewableCityParent", "currentViewableCity" }));
        var map = Code(Web("Components", "MapOverlay.razor"));
        Assert.That(map, Does.Contain("var factionLayer = cityLayer == null ? FactionLayer(layers) : null;"), "城市层里隐藏");
        Assert.That(map, Does.Contain("@if (factionLayer != null)"), "都不可用时隐藏");
        var fl = Slice(map, "private string? FactionLayer(", "private void ToggleLegend()");
        var picked = fl.IndexOf("if (G.MapPicked is string p && G.E.IsCityParent(p) && Known(\"city:\" + p)) return \"city:\" + p;", StringComparison.Ordinal);
        var here = fl.IndexOf("G.E.CityOf(G.S.CurrentLocation) is string c && Known(\"city:\" + c)", StringComparison.Ordinal);
        Assert.That(picked, Is.GreaterThanOrEqualTo(0), "所选城须可查看才进");
        Assert.That(here, Is.GreaterThan(picked), "所选城在前，所在城回退在后，所在城也须可查看");
    }

    // ═════════════ Q1005 到达提示 ═════════════

    /// <summary>B35C-Q1005-01：城市层出发且实际到达后“已到达{地点}。”；未到达不写；普通地图出发沿原反馈（会话只在 fromCity 时写）。</summary>
    [Test]
    public void Q1005_CityArrivalOnly()
    {
        Assert.That(CityCopy.Arrived("营区广场"), Is.EqualTo(Copy("cityArrival").Replace("{地点}", "营区广场")));
        var s = Registered("h03_workshop");
        Assert.That(E.CityArrivalNote(s, "h02_square"), Is.Null, "还没出发不提示");
        Assert.That(E.StartMapDeparture(s, "h02_square", "q1005").Ok, Is.True);
        Run(s);
        Assert.That(E.CityArrivalNote(s, "h02_square"), Is.EqualTo("已到达" + E.LocName("h02_square") + "。"), "实际到达后");
        var depart = Code(Web("Services", "GameSession.cs"));
        var md = Slice(depart, "public async Task MapDepart(", "private void CityOverviewTick()");
        Assert.That(md, Does.Contain("if (State == null || !fromCity) return;"), "普通地图出发不加到达句");
    }

    // ═════════════ Q1006 城市层远处建筑 ═════════════

    /// <summary>B35C-Q1006-01：城市层选不相邻建筑时出发禁用，旁写“先选相邻地点。”，下方“选中下一段：前往{站}”只改选真实相邻首站（不出发）。</summary>
    [Test]
    public void Q1006_CityRemoteBuilding()
    {
        var s = Registered("h03_workshop");
        var r = E.MapRouteOf(s, "h05_appraisal");
        Assert.That(r.Adjacent, Is.False);
        Assert.That(r.Blocked, Is.EqualTo(Copy("nonAdjacentCity")));
        Assert.That(r.SelectNext, Is.EqualTo("h02_square"));
        Assert.That(MapCopy.SelectNext(E.LocName("h02_square")), Is.EqualTo(Copy("selectNextLeg").Replace("{站}", E.LocName("h02_square"))));
        Assert.That(s.Main, Is.Null, "看路线不出发");
        var city = Code(Web("Components", "CityOverview.razor"));
        Assert.That(city, Does.Contain("<button type=\"button\" class=\"primary\" disabled>@MapCopy.Depart</button>"));
        Assert.That(city, Does.Contain("@if (route.Blocked is string why) { <div class=\"ci-line warn\">@why</div> }"));
        Assert.That(city, Does.Contain("@onclick=\"() => Pick(nx)\">@MapCopy.SelectNext(e.LocName(nx))</button>"), "点击只改选");
    }
}
