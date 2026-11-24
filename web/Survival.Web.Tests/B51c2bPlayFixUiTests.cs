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
}
