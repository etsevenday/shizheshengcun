using Bunit;
using Survival.Core.Engine;
using Survival.Core.State;
using Survival.Web.Components;
using Survival.Web.Services;

namespace Survival.Web.Tests;

/// <summary>
/// B51c2b.10 试玩遗留修复（2026-11-24，开发记录 B51c2b.9 的 N1—N3 与体验观察两条）网页壳行为：
/// 真正渲染晾制架面板、陶窑工作区、右栏与主画面，经组件点卡、取消、关面板、点配方行、点箭头、数量窗「最多」确认，断玩家看到的右栏、操作窗与面板覆盖范围。
/// 起点用内核 CreateItems／Debug* 造，之后只经组件或会话入口操作。
/// </summary>
public class B51c2bPlayFixUiTests : WebTestBase
{
    private const string KilnRecipe = "ex_ceramic_shell";   // 配置：黏土2 → 陶壳2，燃料木炭1
    private const string KilnOutput = "陶壳";                 // 陶窑工作区右栏配方预估的标题（配方名）

    private void Ok(ActionResult r) => Assert.That(r.Ok, Is.True, r.Reason);

    private int Sum(string itemId, string owner) => S.Items.Where(i => i.ItemId == itemId && i.Owner == owner).Sum(i => i.Count);

    /// <summary>营地建好全部工坊（含晾制架、陶窑）；位 1 投果干（野果×2），陶窑投一批陶壳（黏土×2＋木炭×1）。</summary>
    private void DryingAndKiln()
    {
        E.DebugBuildAllWorkshops(S);
        S.Skills["cooking"].Level = 5;
        S.Skills["crafting"].Level = 3;
        S.Progress.Flags.Add("h03_ceramic_notice_read");
        S.Items.RemoveAll(i => i.ItemId is "berry" or "dried_berries" or "clay" or "charcoal" or "ceramic_shell");
        E.CreateItems(S, "berry", 2, ItemInstance.Carried);
        Ok(E.StartDrying(S, "dry_berries"));
        E.CreateItems(S, "clay", 2, S.Ground);
        E.CreateItems(S, "charcoal", 1, S.Ground);
        Ok(E.StartStationBatch(S, KilnRecipe, "k"));
    }

    private void OpenKiln() => G.OpenWorkspace(E.WorkspaceForStationHere(S, "kiln")!, KilnRecipe);

    private static string RailTitle(IRenderedComponent<DetailPanel> rail) => rail.Find(".rail-page .rail-head h2").TextContent.Trim();

    // ───────────── N1：选过设施物品卡后，陶窑工作区右栏回到配方预估 ─────────────

    /// <summary>
    /// N1（S14 Q487／B51c2b.7「工作区详细预估在右栏」；S11 §Q925 B 只在点卡本体时看本卡详情）：晾制架面板点位内野果卡 → 数量窗「取消」→ 关面板 → 开陶窑工作区，
    /// 右栏是陶壳的配方预估（标题「陶壳」），不再停在野果卡的详情；对照：点卡后右栏确是野果。
    /// </summary>
    [Test]
    public void N1_DryingCardThenKiln_RailShowsRecipe()
    {
        DryingAndKiln();
        G.OpenPanel("drying");
        var dry = Ctx.RenderComponent<DryingOverlay>();
        dry.Find("[data-pt=dry-toggle][data-slot='0']").Click();   // B59a（S11「2026-11-24」§Q1034）：占用位默认收起，先展开才见逐份原料卡
        var rail = Ctx.RenderComponent<DetailPanel>();
        dry.Find("[data-pt=fac-item]").Click();
        rail.Render();
        Assert.That(RailTitle(rail), Is.EqualTo("野果"), "对照：点卡本体右栏看这张卡");
        dry.Find("[data-pt=fac-take-cancel]").Click();
        dry.Find(".sub-head > button.secondary").Click();   // 晾制架面板「关闭」
        Assert.That(G.Panel, Is.Null);

        OpenKiln();
        var craft = Ctx.RenderComponent<CraftOverlay>();
        rail.Render();

        Assert.That(RailTitle(rail), Is.EqualTo(KilnOutput), "工作区右栏是所选配方的预估");
        Assert.That(G.IsFacilityItemSel(G.Selected), Is.False);
        Assert.That(craft.FindAll("[data-pt=fac-take]"), Is.Empty);
    }

    /// <summary>
    /// N1（同上）：陶窑工作区里点批内黏土卡，右栏看黏土；再点左列陶壳配方行，右栏回到陶壳配方预估，数量窗收起。
    /// </summary>
    [Test]
    public void N1_KilnCardThenRecipeRow_RailBackToRecipe()
    {
        DryingAndKiln();
        OpenKiln();
        var craft = Ctx.RenderComponent<CraftOverlay>();
        var rail = Ctx.RenderComponent<DetailPanel>();
        craft.Find("[data-station-inputs] [data-pt=fac-item]").Click();
        rail.Render();
        Assert.That(RailTitle(rail), Is.EqualTo("黏土"), "对照：点卡本体右栏看这张卡");
        Assert.That(craft.FindAll("[data-pt=fac-take]"), Has.Count.EqualTo(1));

        craft.Find($"button.recipe[data-recipe='{KilnRecipe}']").Click();
        rail.Render();

        Assert.That(RailTitle(rail), Is.EqualTo(KilnOutput));
        Assert.That(craft.FindAll("[data-pt=fac-take]"), Is.Empty, "数量窗收起");
    }

    // ───────────── N2：选中的设施物品卡整张取走后不弹随身卡操作窗 ─────────────

    /// <summary>
    /// N2（S14 §9.2 Q5-2「设施整锅／直烤位／锅内子物箭头→随身1份或1实例；不能弹操作条」；S11 §Q925 B）：先点卡身选中晾制位野果×2，
    /// 箭头取 1 份时这张卡仍在位里、仍选中（对照）；再点箭头整张取到随身后，不弹随身卡的操作窗、选中清空、右栏页签仍是「对象」。
    /// 配对：直接选中随身的野果卡确会弹操作窗（CardPopShown 判据有效）。
    /// </summary>
    [Test]
    public void N2_SelectedDryingCard_ArrowTakesAll_NoCardPop()
    {
        DryingAndKiln();
        G.OpenPanel("drying");
        var dry = Ctx.RenderComponent<DryingOverlay>();
        dry.Find("[data-pt=dry-toggle][data-slot='0']").Click();   // B59a（S11「2026-11-24」§Q1034）：占用位默认收起，先展开才见逐份原料卡
        var card = S.Items.Single(i => i.ItemId == "berry");
        dry.Find("[data-pt=fac-item]").Click();
        Assert.That(G.Selected.InstanceId, Is.EqualTo(card.InstanceId));

        dry.Find("[data-pt=fac-item] button.card-arrow").Click(detail: 0);
        Assert.That(Sum("berry", ItemInstance.Carried), Is.EqualTo(1));
        Assert.That(G.Selected.InstanceId, Is.EqualTo(card.InstanceId), "对照：只取了 1 份，卡仍在位里、仍选中");

        dry.Find("[data-pt=fac-item] button.card-arrow").Click(detail: 0);

        Assert.That(Sum("berry", ItemInstance.Carried), Is.EqualTo(2), "整张取到随身");
        Assert.That(G.CardPopShown, Is.False, "不弹随身卡操作窗");
        Assert.That(G.Selected.Kind, Is.EqualTo(SelectionKind.None));
        Assert.That(G.DetailTab, Is.EqualTo(DetailTab.Object), "面板开着，右栏页签不跳到人物页");

        G.Select(new Selection { Kind = SelectionKind.ItemGroup, Id = "berry", Owner = ItemInstance.Carried });
        Assert.That(G.CardPopShown, Is.True, "配对：直接选中随身卡确会弹操作窗");
    }

    /// <summary>
    /// N2（同上，数量窗）：陶窑工作区点批内黏土×2 卡、数量窗「最多」后确认，整张到随身——不弹操作窗、选中清空，右栏回到陶壳配方预估。
    /// </summary>
    [Test]
    public void N2_KilnCard_QuantityMax_NoCardPop_RailRecipe()
    {
        DryingAndKiln();
        OpenKiln();
        var craft = Ctx.RenderComponent<CraftOverlay>();
        var rail = Ctx.RenderComponent<DetailPanel>();
        craft.Find("[data-station-inputs] [data-pt=fac-item]").Click();
        craft.Find("[data-pt=fac-take] button.tbar-max").Click();
        Assert.That(craft.Find("[data-pt=fac-take-ok]").TextContent, Is.EqualTo("确认 2"), "S11 §Q925 B「确认{数量}」");

        craft.Find("[data-pt=fac-take-ok]").Click();
        rail.Render();

        Assert.That(Sum("clay", ItemInstance.Carried), Is.EqualTo(2));
        Assert.That(G.CardPopShown, Is.False, "不弹随身卡操作窗");
        Assert.That(G.Selected.Kind, Is.EqualTo(SelectionKind.None));
        Assert.That(RailTitle(rail), Is.EqualTo(KilnOutput));
    }

    // ───────────── N3：被动设施面板只盖①②，③当前地面与④随身照常可见 ─────────────

    /// <summary>
    /// N3（S11／S14 §Q778「火堆主层覆盖①地图②设施……原地面③与随身④的可见及原权限不变。其余被动设施……不覆盖另加的区域」；Q1028 答复只盖设施栏与地图栏）：
    /// 晾制架面板、陶窑工作区、火堆加工菜单开着时，主画面四行网格不加加高类（tall-panel／full-panel），面板只在①②合并区。
    /// 配对：工作台工作区仍盖①②③（tall-panel），整备仍盖四行（full-panel），沿原排版。面板实际几何（遮挡、落点可见）bUnit 看不到，交试玩。
    /// </summary>
    [Test]
    public void N3_PassivePanels_LeaveGroundAndCarriedRows()
    {
        DryingAndKiln();
        var screen = Ctx.RenderComponent<GameScreen>();
        string Rows()
        {
            screen.Render();
            return screen.Find(".center-scroll.four").ClassName ?? "";
        }

        G.OpenPanel("drying");
        Assert.That(Rows(), Does.Not.Contain("tall-panel").And.Not.Contain("full-panel"), "晾制架面板");
        OpenKiln();
        Assert.That(G.Panel, Is.EqualTo("craft"));
        Assert.That(Rows(), Does.Not.Contain("tall-panel").And.Not.Contain("full-panel"), "陶窑工作区");
        G.OpenPanel(null);
        G.OpenFireMenu();
        Assert.That(G.Panel, Is.EqualTo(GameSession.FireMenuPanel));
        Assert.That(Rows(), Does.Not.Contain("tall-panel").And.Not.Contain("full-panel"), "火堆加工菜单");
        G.OpenPanel(null);

        G.OpenWorkspace(E.WorkspaceForStationHere(S, "workbench")!);
        Assert.That(Rows(), Does.Contain("tall-panel"), "配对：工作台工作区沿原排版盖①②③");
        G.OpenPanel(null);
        G.OpenPanel("gear");
        Assert.That(Rows(), Does.Contain("full-panel"), "配对：整备沿原排版盖四行");
    }

    /// <summary>
    /// N3 顺带（1280×720 数量窗「确认／取消」落在面板可视区外）：点卡本体打开数量窗时把整个窗滚到可见（survival.revealNearest，最近边对齐），只调一次；
    /// 配对：没开数量窗时不调。实际滚动效果交试玩。
    /// </summary>
    [Test]
    public void N3_QuantityBox_RevealedOnOpen()
    {
        DryingAndKiln();
        G.OpenPanel("drying");
        var dry = Ctx.RenderComponent<DryingOverlay>();
        dry.Find("[data-pt=dry-toggle][data-slot='0']").Click();   // B59a（S11「2026-11-24」§Q1034）：占用位默认收起，先展开才见逐份原料卡
        Assert.That(Ctx.JSInterop.Invocations.Count(i => i.Identifier == "survival.revealNearest"), Is.EqualTo(0), "配对：没开数量窗不滚");

        dry.Find("[data-pt=fac-item]").Click();
        dry.Render();

        Assert.That(Ctx.JSInterop.Invocations.Count(i => i.Identifier == "survival.revealNearest"), Is.EqualTo(1));
    }

    // ───────────── 体验观察：右栏不显示设施物品卡用不了的操作提示 ─────────────

    /// <summary>
    /// 右栏底部说明（S11／S07 §Q925 B：设施物品卡在右栏是只读详情，取出在面板里的箭头与数量窗）：点晾制位野果卡后右栏不出「食用1份」等指向选卡操作窗的脚注；
    /// 配对：选中随身的野果卡，脚注照常显示「食用1份」。
    /// </summary>
    [Test]
    public void RailFoot_HiddenForFacilityItem_ShownForCarried()
    {
        DryingAndKiln();
        G.OpenPanel("drying");
        var dry = Ctx.RenderComponent<DryingOverlay>();
        dry.Find("[data-pt=dry-toggle][data-slot='0']").Click();   // B59a（S11「2026-11-24」§Q1034）：占用位默认收起，先展开才见逐份原料卡
        var rail = Ctx.RenderComponent<DetailPanel>();
        dry.Find("[data-pt=fac-item]").Click();
        rail.Render();
        Assert.That(RailTitle(rail), Is.EqualTo("野果"));
        Assert.That(rail.FindAll(".detail-foot"), Is.Empty);

        dry.Find("[data-pt=fac-item] button.card-arrow").Click(detail: 0);
        G.OpenPanel(null);
        G.Select(new Selection { Kind = SelectionKind.ItemGroup, Id = "berry", Owner = ItemInstance.Carried });
        rail.Render();
        Assert.That(rail.FindAll(".detail-foot"), Has.Count.EqualTo(1), "配对：随身卡照常有脚注");
        Assert.That(rail.Find(".detail-foot").TextContent, Does.Contain("食用1份"));
    }

    // ───────────── 试玩修复 M1137：被动设施面板只盖①②时的紧凑排版标记、数量窗确认行可见 ─────────────

    /// <summary>
    /// M1137（S14 FACPROC-A11「主要动作与关闭可见，长内容内部滚动」；保留 N3 的 S11／S14 §Q778 只盖①②）：晾制架面板、陶窑工作区、火堆加工菜单开着时，
    /// 主画面四行网格标 passive-panel（紧凑排版的样式钩子，CSS 让投料栏粘底、数量与提交条成一行），且仍不加加高类；
    /// 配对：工作台工作区（tall-panel）、整备（full-panel）不标 passive-panel。实际几何 bUnit 看不到，交试玩。
    /// </summary>
    [Test]
    public void M1137_PassivePanels_MarkedForCompactLayout()
    {
        DryingAndKiln();
        var screen = Ctx.RenderComponent<GameScreen>();
        string[] Rows()
        {
            screen.Render();
            return screen.Find(".center-scroll.four").ClassList.ToArray();
        }

        G.OpenPanel("drying");
        Assert.That(Rows(), Does.Contain("passive-panel").And.Not.Contain("tall-panel"), "晾制架面板");
        OpenKiln();
        Assert.That(Rows(), Does.Contain("passive-panel").And.Not.Contain("tall-panel"), "陶窑工作区");
        G.OpenPanel(null);
        G.OpenFireMenu();
        Assert.That(Rows(), Does.Contain("passive-panel").And.Not.Contain("full-panel"), "火堆加工菜单");
        G.OpenPanel(null);

        G.OpenWorkspace(E.WorkspaceForStationHere(S, "workbench")!);
        Assert.That(Rows(), Does.Contain("tall-panel").And.Not.Contain("passive-panel"), "配对：工作台工作区");
        G.OpenPanel(null);
        G.OpenPanel("gear");
        Assert.That(Rows(), Does.Contain("full-panel").And.Not.Contain("passive-panel"), "配对：整备");
    }

    /// <summary>
    /// M1137（S14 FACPROC-A11「不遮唯一确认」）：陶窑批内黏土卡的数量窗里「确认／取消」所在行标 data-reveal-last，
    /// 打开时 survival.revealNearest 滚完整个窗后再保证这一行可见（窗比滚动区高时也露出确认）；仍只调一次。
    /// </summary>
    [Test]
    public void M1137_KilnQuantityBox_ConfirmRowRevealedLast()
    {
        DryingAndKiln();
        OpenKiln();
        var craft = Ctx.RenderComponent<CraftOverlay>();
        craft.Find("[data-station-inputs] [data-pt=fac-item]").Click();
        craft.Render();

        var last = craft.FindAll("[data-pt=fac-take] [data-reveal-last]");
        Assert.That(last, Has.Count.EqualTo(1));
        Assert.That(last[0].QuerySelectorAll("button").Select(b => b.GetAttribute("data-pt")), Is.EqualTo(new[] { "fac-take-ok", "fac-take-cancel" }));
        Assert.That(Ctx.JSInterop.Invocations.Count(i => i.Identifier == "survival.revealNearest"), Is.EqualTo(1));
    }
}
