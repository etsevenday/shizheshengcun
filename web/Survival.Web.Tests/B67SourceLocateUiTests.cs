using Bunit;
using Survival.Core.Engine;
using Survival.Core.State;
using Survival.Web.Components;
using Survival.Web.Services;

namespace Survival.Web.Tests;

/// <summary>
/// B67 R347「来源页定位本地资源卡」网页层行为（开发记录 doc/开发记录/B67 制作检索、工艺链与来源定位.md）：
/// 出处 S05 2026-11-19 D22、S14「D22 从来源页定位本地资源卡」与 D22-01／D22-02（Q911 默认选中、Q592 返回链沿用）。
/// 真正渲染制作窗口的来源侧页、主界面（GameScreen）与地块栏（LocationStrip），点「定位来源」「返回材料来源」，断高亮、返回与“无变化”。
/// </summary>
public class B67SourceLocateUiTests : WebTestBase
{
    /// <summary>
    /// 起点：人在农野荒地（field），石刀缺纤维；荒地纤维丛（本地）与树林边缘的纤维丛都已发现（树林边缘地点已知），并把树林那一处改成同名“荒地纤维丛”（D22-01“同名…分处两个区域”）。
    /// </summary>
    private IRenderedComponent<CraftOverlay> OpenKnifeSourcesAtField()
    {
        E.DebugTeleport(S, "field");
        S.Resources["field_fiber"].Discovered = true;
        S.Resources["woods_fiber"].Discovered = true;
        S.Locations["woods"].Discovered = true;   // 异地地点已知：原“在地图上定位”可用
        E.Config.Resources.Single(r => r.NodeId == "woods_fiber").Name = "荒地纤维丛";
        S.Items.RemoveAll(i => i.ItemId is "stone" or "fiber");
        E.CreateItems(S, "stone", 1, S.Ground);
        G.Notify();
        G.OpenHandCraft("knife");
        var cut = Ctx.RenderComponent<CraftOverlay>();
        cut.Find("[data-source-item=fiber]").Click();
        Assert.That(G.CraftSourcesOpen, Is.True);
        return cut;
    }

    private string PickedTitle(IRenderedComponent<CraftOverlay> cut) => cut.Find("[data-pt=src-detail-title]").TextContent;

    /// <summary>
    /// D22-01：同名纤维丛分处荒地与树林。默认选中的当地那一行详情有「定位来源」（另有原「在地图上定位」）；点它——制作窗口让出①②、
    /// 当前场景里只有荒地那张真实资源卡加来源高亮，定位层有「返回材料来源」并承接 ESC；不选中、不开操作窗、不改地图目的地，
    /// 库存、物品位置与时间都不变。返回后回到来源侧页、原来源行仍选中。
    /// </summary>
    [Test]
    public void D22_01_LocalCard_Highlight_NoChange_BackRestoresRow()
    {
        var cut = OpenKnifeSourcesAtField();
        Assert.That(PickedTitle(cut), Does.Contain("农野荒地 · 荒地纤维丛"));
        Assert.That(cut.Find("[data-pt=src-locate-here]").TextContent, Is.EqualTo("定位来源"));
        Assert.That(cut.FindAll("[data-pt=src-locate]"), Has.Count.EqualTo(1), "原地图定位仍在");
        var key = G.CraftSourceKey;
        var sel = G.Selected.Key;
        var stock = S.Resources["field_fiber"].Available;
        var items = string.Join(";", S.Items.OrderBy(i => i.InstanceId).Select(i => $"{i.InstanceId}:{i.ItemId}:{i.Owner}:{i.Count}"));
        var (minute, here) = (S.GameMinute, S.CurrentLocation);

        cut.Find("[data-pt=src-locate-here]").Click();
        Assert.That((G.Panel, G.CraftLocating, G.CraftLocateNode), Is.EqualTo(("craft", true, "field_fiber")));
        Assert.That(G.LocateFocusPending, Is.EqualTo("res-field_fiber"), "渲染后滚到并聚焦这张卡");

        var screen = Ctx.RenderComponent<GameScreen>();
        Assert.That(screen.FindAll(".panel-host"), Is.Empty, "制作窗口让出①②");
        var spots = screen.FindAll(".ocard.source-spot");
        Assert.That(spots, Has.Count.EqualTo(1));
        Assert.That(spots[0].Id, Is.EqualTo("res-field_fiber"), "只定位当地真实实例");
        var back = screen.Find("[data-pt=src-locate-back]");
        Assert.That(back.TextContent, Does.StartWith("返回材料来源"));
        Assert.That(back.QuerySelector("kbd.kc"), Is.Not.Null, "ESC 键帽在「返回材料来源」上");
        Assert.That(G.EscOwner, Is.EqualTo(EscOwner.Panel));

        Assert.That((G.Selected.Key, G.Pop, G.MapPicked, S.Main), Is.EqualTo((sel, (GameSession.PopContext?)null, (string?)null, (ActionState?)null)), "不选中、不开操作窗、不改目的地、不开始采集");
        Assert.That((S.GameMinute, S.CurrentLocation, S.Resources["field_fiber"].Available), Is.EqualTo((minute, here, stock)), "0 分钟、不移动、库存不变");
        Assert.That(string.Join(";", S.Items.OrderBy(i => i.InstanceId).Select(i => $"{i.InstanceId}:{i.ItemId}:{i.Owner}:{i.Count}")), Is.EqualTo(items));

        back.Click();
        Assert.That((G.Panel, G.CraftLocating, G.CraftSourcesOpen, G.CraftSourceKey), Is.EqualTo(("craft", false, true, key)), "回来源侧页、恢复原来源行");
        Assert.That(Ctx.JSInterop.Invocations["survivalPop.locateId"].Select(i => (string?)i.Arguments.Single()).ToList(), Is.EqualTo(new[] { "res-field_fiber" }), "定位层打开后滚到并聚焦该卡一次");
        Assert.That(Ctx.JSInterop.Invocations["survivalPanels.restore"].Select(i => (string?)i.Arguments.Single()).ToList(), Does.Contain("craft"), "返回时取回制作窗口原滚动与焦点");
        cut.Render();
        Assert.That(PickedTitle(cut), Does.Contain("农野荒地 · 荒地纤维丛"));
        Assert.That(screen.FindAll(".ocard.source-spot"), Is.Empty, "返回后不留高亮");
        Assert.That(screen.FindAll(".panel-host"), Has.Count.EqualTo(1));
    }

    /// <summary>
    /// B67 试玩修复 M1174（排版为开发方取舍，不涉规则）：「返回材料来源」参与①行排版——在 .map-row 里、紧跟当前地点卡、在相邻地点子栏之前，
    /// 不再是 rows2 上浮在卡上的绝对定位层（1280 档曾压住相邻地点卡）。像素位置由试玩场景 b67-src-locate 第 41、42 步核对。
    /// </summary>
    [Test]
    public void M1174_BackBar_InMapRow_BeforeAdjacent()
    {
        var cut = OpenKnifeSourcesAtField();
        cut.Find("[data-pt=src-locate-here]").Click();
        var screen = Ctx.RenderComponent<GameScreen>();
        var bars = screen.FindAll("[data-pt=src-locate-bar]");
        Assert.That(bars, Has.Count.EqualTo(1));
        var bar = bars[0];
        Assert.That(bar.ParentElement!.ClassList, Does.Contain("map-row"), "在①行里参与排版");
        Assert.That(bar.NextElementSibling!.ClassList, Does.Contain("map-adjacent"), "相邻地点子栏在它右侧");
        Assert.That(bar.PreviousElementSibling!.ClassList, Does.Contain("map-card"), "紧跟当前地点大图卡");
        Assert.That(screen.FindAll(".rows2 > .src-locate-bar"), Is.Empty, "不再浮在 rows2 上");
    }

    /// <summary>D22-02 后半：异地的同名来源（树林）不出本地「定位来源」，原可公开的异地地图定位仍可用；没有地点的概率来源两种定位都没有。</summary>
    [Test]
    public void D22_02_RemoteRow_NoLocalLocate_MapLocateKept()
    {
        var cut = OpenKnifeSourcesAtField();
        var rows = cut.FindAll("[data-pt=src-row]");
        Assert.That(rows.Count(r => r.TextContent.Contains("树林边缘 · ")), Is.EqualTo(1), string.Join(" | ", rows.Select(r => r.TextContent)));
        var remote = rows.Single(r => r.TextContent.Contains("树林边缘 · "));
        remote.Click();
        Assert.That(PickedTitle(cut), Does.Contain("荒地纤维丛"));
        Assert.That(cut.FindAll("[data-pt=src-locate-here]"), Is.Empty);
        Assert.That(cut.Find("[data-pt=src-locate]").TextContent, Is.EqualTo("在地图上定位"));

        cut.FindAll("[data-pt=src-row]").Single(r => r.GetAttribute("data-source-kind") == "drop").Click();
        Assert.That(cut.FindAll("[data-pt=src-locate-here]"), Is.Empty);
        Assert.That(cut.FindAll("[data-pt=src-locate]"), Is.Empty);
    }

    /// <summary>
    /// D22-02 前半：按下前资源已失效（这里让荒地纤维丛变成未发现）——留在来源页（不进定位层），刷新该行并提示「当前无法定位该来源。」；
    /// 换选别的来源后提示收起。配对见 D22_01（资源有效时进定位层）。
    /// </summary>
    [Test]
    public void D22_02_GoneBeforePress_StaysWithNotice()
    {
        var cut = OpenKnifeSourcesAtField();
        var button = cut.Find("[data-pt=src-locate-here]");
        S.Resources["field_fiber"].Discovered = false;
        button.Click();
        Assert.That((G.CraftLocating, G.CraftSourcesOpen, G.Panel), Is.EqualTo((false, true, "craft")));
        Assert.That(cut.Find("[data-pt=src-locate-miss]").TextContent, Is.EqualTo("当前无法定位该来源。"));
        Assert.That(cut.FindAll("[data-pt=src-row]").Any(r => r.TextContent.Contains("农野荒地 · ")), Is.False, "该行按当前真实状态刷新");

        cut.FindAll("[data-pt=src-row]")[0].Click();
        Assert.That(cut.FindAll("[data-pt=src-locate-miss]"), Is.Empty);
    }

    /// <summary>
    /// D22-02 前半（另一种失效）：来源行还列着，但按下前人已进到屋内、当前场景不再显示这张资源卡——同样留在来源页并提示「当前无法定位该来源。」。
    /// </summary>
    [Test]
    public void D22_02_CardLeftSceneRowStillListed_StaysWithNotice()
    {
        var cut = OpenKnifeSourcesAtField();
        var key = G.CraftSourceKey;
        var button = cut.Find("[data-pt=src-locate-here]");
        S.CurrentZone = GameState.SafehouseZone;
        button.Click();
        Assert.That((G.CraftLocating, G.CraftSourcesOpen, G.CraftSourceKey), Is.EqualTo((false, true, key)), "来源行仍在、选中不变");
        Assert.That(cut.Find("[data-pt=src-locate-miss]").TextContent, Is.EqualTo("当前无法定位该来源。"));
    }

    /// <summary>
    /// S14 D22“返回…重验来源”：定位层开着时资源卡在刷新中失效（如被别处改变）——自动回到来源侧页并重验，原行已消失则不选（不换同名行）。
    /// </summary>
    [Test]
    public void D22_CardGoneOnRefresh_AutoBackToSources()
    {
        var cut = OpenKnifeSourcesAtField();
        cut.Find("[data-pt=src-locate-here]").Click();
        Assert.That(G.CraftLocating, Is.True);
        S.Resources["field_fiber"].Discovered = false;
        G.Notify();
        Assert.That((G.Panel, G.CraftLocating, G.CraftSourcesOpen, G.CraftSourceKey), Is.EqualTo(("craft", false, true, (string?)null)));
    }

    /// <summary>
    /// S14 D22“返回…重验来源；原行已消失则回有效来源列表，不以同名行或其他区域代替”：定位中原资源卡失效——回到来源侧页，
    /// 不再选中任何来源（不换成树林那条同名行）。ESC 与「返回材料来源」同一入口（PanelEscape）。
    /// </summary>
    [Test]
    public void D22_RowGoneWhileLocating_BackToListNoSubstitute()
    {
        var cut = OpenKnifeSourcesAtField();
        cut.Find("[data-pt=src-locate-here]").Click();
        Assert.That(G.CraftLocating, Is.True);
        S.Resources["field_fiber"].Discovered = false;
        G.PanelEscape();
        Assert.That((G.Panel, G.CraftLocating, G.CraftSourcesOpen), Is.EqualTo(("craft", false, true)));
        Assert.That(G.CraftSourceKey, Is.Null, "原行消失：不选别的行");
        cut.Render();
        Assert.That(cut.FindAll("[data-pt=src-detail-title]"), Is.Empty);
        Assert.That(cut.FindAll("[data-pt=src-row]").Any(r => r.TextContent.Contains("树林边缘 · 荒地纤维丛")), Is.True, "有效来源列表仍在");
    }

    /// <summary>D22“当前场景”：在安全屋屋内不显示资源卡，本地定位入口同样不出（配对：屋外有，见 D22_01）。</summary>
    [Test]
    public void D22_InsideSafehouse_NoLocalLocate()
    {
        var cut = OpenKnifeSourcesAtField();
        Assert.That(cut.FindAll("[data-pt=src-locate-here]"), Has.Count.EqualTo(1));
        S.CurrentZone = GameState.SafehouseZone;
        G.Notify();
        Assert.That(cut.FindAll("[data-pt=src-locate-here]"), Is.Empty);
    }
}
