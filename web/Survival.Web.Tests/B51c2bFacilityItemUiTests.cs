using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Survival.Core.Engine;
using Survival.Core.State;
using Survival.Web.Components;
using Survival.Web.Services;

namespace Survival.Web.Tests;

/// <summary>
/// B51c2b「被动设施卡片箭头、余料与拖入」网页壳行为（S11／S07／S14「2026-11-17 B51c2、B47b与B52b」§Q870 B 快捷箭头，
/// 「2026-11-17 B51c2a／B51c2b完整两原批」§Q924 B、§Q925 B、§Q926 A、§Q927 B）：
/// 真正渲染晾制架面板（DryingOverlay）、陶窑工作区（CraftOverlay）与火堆食物卡（FoodCard），点卡片箭头、点卡本体开数量窗、改去向与数量、确认，
/// 断玩家看到的按钮、文字与结果。起点用内核入口或 Debug 造，之后只经组件操作。
/// </summary>
public class B51c2bFacilityItemUiTests : WebTestBase
{
    private const string Busy = "行动进行中，暂不能整理物品";   // S11 Q871 B

    private void Ok(ActionResult r) => Assert.That(r.Ok, Is.True, r.Reason);

    private int Sum(string itemId, string owner) => S.Items.Where(i => i.ItemId == itemId && i.Owner == owner).Sum(i => i.Count);

    private List<DryingSlot> DrySlots => S.Locations[S.Ground].Facility.DryingSlots;

    /// <summary>营地建好晾制架，位 1 投果干（野果×2）。</summary>
    private void BerriesDrying()
    {
        E.DebugBuildAllWorkshops(S);
        S.Skills["cooking"].Level = 5;
        S.Items.RemoveAll(i => i.ItemId is "berry" or "dried_berries");
        E.CreateItems(S, "berry", 2, ItemInstance.Carried);
        Ok(E.StartDrying(S, "dry_berries"));
    }

    /// <summary>
    /// FAC-Q870-02（网页壳）：晾制位里每份原料是一张物品卡，卡上有取到随身箭头；点一下（键盘激活，单次）取 1 份到随身，
    /// 这一位成「余料」，位按钮变成「取出」「查看配方」（S11 §Q924 B）。
    /// </summary>
    [Test]
    public void DryingCard_Arrow_TakesOne_SlotShowsResidual()
    {
        BerriesDrying();
        var cut = Ctx.RenderComponent<DryingOverlay>();
        var cards = cut.FindAll("[data-dry-items='0'] [data-pt=fac-item]");
        Assert.That(cards, Has.Count.EqualTo(1));
        Assert.That(cards[0].QuerySelector(".ir-name")!.TextContent, Is.EqualTo("野果×2"));
        var arrow = cards[0].QuerySelector("button.card-arrow")!;
        Assert.That((arrow.TextContent, arrow.HasAttribute("disabled")), Is.EqualTo(("▼", false)));

        arrow.Click(detail: 0);   // Detail 0：键盘式单击，只转 1 份（TransferArrow 鼠标走 pointerdown）

        Assert.That(Sum("berry", ItemInstance.Carried), Is.EqualTo(1));
        Assert.That(DrySlots.Single().Status, Is.EqualTo(DryingSlot.Residual));
        var slot0 = cut.Find("[data-dry-slot='0']");
        Assert.That(slot0.QuerySelector(".dry-state")!.TextContent, Is.EqualTo("余料"), "S11 §Q924 短状态");
        Assert.That(slot0.QuerySelectorAll(".dry-btns button").Select(b => b.TextContent.Trim()), Is.EqualTo(new[] { "取出", "查看配方" }));
        Assert.That(cut.Find("[data-dry-items='0'] .ir-name").TextContent, Is.EqualTo("野果"), "余料卡仍在本位");

        // S11 §Q924 B：余料位「查看配方」选中这一位，食谱区给本位补投预览——来源先列本位余料
        cut.Find("[data-pt=dry-refill][data-slot='0']").Click();
        Assert.That(cut.Find(".dry-recipes h3").TextContent, Is.EqualTo("投到工位1：选食谱"));
        Assert.That(cut.FindAll("[data-dry-recipe='dry_berries'] .dry-src").Select(x => x.TextContent).First(), Does.StartWith("余料野果×1"));
    }

    /// <summary>
    /// FACF-Q925-01：点卡本体——右栏选中这张卡（只读，不弹选卡操作盘），卡下数量窗标题「取出 · 野果」、去向「取到随身／取到地面」（默认随身）、
    /// 确认「确认 1」；改数量 2、选取到地面、确认后两份到本区地面、这一位空出、数量窗收起。
    /// </summary>
    [Test]
    public void DryingCard_Click_QuantityBox_ToGround()
    {
        BerriesDrying();
        var cut = Ctx.RenderComponent<DryingOverlay>();
        var card = S.Items.Single(i => i.ItemId == "berry");
        cut.Find("[data-pt=fac-item]").Click();

        Assert.That((G.Selected.Kind, G.Selected.InstanceId), Is.EqualTo((SelectionKind.Tool, (int?)card.InstanceId)), "右栏看这张卡");
        Assert.That(G.CardPopShown, Is.False, "不弹选卡操作盘");
        var box = cut.Find("[data-pt=fac-take]");
        Assert.That(box.QuerySelector("h3")!.TextContent.Trim(), Is.EqualTo("取出 · 野果"), "Q925 B 标题「取出」");
        Assert.That(cut.Find("[data-pt=fac-take-carried]").TextContent, Is.EqualTo("取到随身"));
        Assert.That(cut.Find("[data-pt=fac-take-carried]").GetAttribute("aria-pressed"), Is.EqualTo("true"), "默认随身");
        Assert.That(cut.Find("[data-pt=fac-take-ground]").TextContent, Is.EqualTo("取到地面"));
        Assert.That(cut.Find("[data-pt=fac-take-ok]").TextContent, Is.EqualTo("确认 1"), "Q925 B「确认{数量}」");

        cut.Find("[data-pt=fac-take-qty]").Input("2");
        cut.Find("[data-pt=fac-take-ground]").Click();
        Assert.That(cut.Find("[data-pt=fac-take-ok]").TextContent, Is.EqualTo("确认 2"));
        cut.Find("[data-pt=fac-take-ok]").Click();

        Assert.That(Sum("berry", S.Ground), Is.EqualTo(2));
        Assert.That(DrySlots, Is.Empty);
        Assert.That(cut.FindAll("[data-pt=fac-take]"), Is.Empty, "成功后收起");
        Assert.That(G.FacTake, Is.Null);
    }

    /// <summary>
    /// FAC-Q870-03（网页壳）：随身已满五张卡——数量窗写真实拒绝句、确认置灰，不改落地；改选「取到地面」后原因消失、可确认。
    /// </summary>
    [Test]
    public void DryingCard_QuantityBox_CarriedFull_ReasonThenGroundOk()
    {
        BerriesDrying();
        S.Items.RemoveAll(i => i.Owner == ItemInstance.Carried);
        foreach (var id in new[] { "wood", "stone", "rope", "plank", "cloth" }) E.CreateItems(S, id, 1, ItemInstance.Carried);
        var cut = Ctx.RenderComponent<DryingOverlay>();
        cut.Find("[data-pt=fac-item]").Click();

        Assert.That(cut.Find("[data-pt=fac-take-why]").TextContent, Is.EqualTo("随身卡位已满（5/5张）：会多出一张新卡，请先放下或用掉一张"));
        Assert.That(cut.Find("[data-pt=fac-take-ok]").HasAttribute("disabled"), Is.True);
        Assert.That(DrySlots.Single().Status, Is.EqualTo(DryingSlot.Drying), "什么都不动");

        cut.Find("[data-pt=fac-take-ground]").Click();
        Assert.That(cut.FindAll("[data-pt=fac-take-why]"), Is.Empty);
        Assert.That(cut.Find("[data-pt=fac-take-ok]").HasAttribute("disabled"), Is.False);
    }

    /// <summary>Q871 B／Q925 B：主行动进行中卡片箭头置灰、悬停统一句；配对：空闲时可用。</summary>
    [Test]
    public void DryingCard_Arrow_BusyGreyed()
    {
        BerriesDrying();
        var cut = Ctx.RenderComponent<DryingOverlay>();
        Assert.That(cut.Find("[data-pt=fac-item] button.card-arrow").HasAttribute("disabled"), Is.False, "空闲可用");
        Ok(E.Start(S, ActionRequest.Wait(30)));
        cut.Render();
        var arrow = cut.Find("[data-pt=fac-item] button.card-arrow");
        Assert.That((arrow.HasAttribute("disabled"), arrow.GetAttribute("title")), Is.EqualTo((true, Busy)));
    }

    /// <summary>
    /// FACF-Q926-01（网页壳）：拖野果到空晾制位松手——面板选中位 2 并高亮果干食谱（只导航，不投料），食谱区标题「投到工位2：选食谱」。
    /// </summary>
    [Test]
    public void DropOnDryingSlot_FocusesSlotAndRecipe()
    {
        BerriesDrying();
        var berry = E.CreateItems(S, "berry", 1, ItemInstance.Carried)[0];
        var intent = E.ResolveDrop(S, DropSource.Parse($"card:{berry.Owner}|berry|{berry.InstanceId}")!, DropTarget.Parse("dryslot:1")!);
        Ok(G.ExecuteDrop(intent, 1, "drop-q926"));

        Assert.That(G.Panel, Is.EqualTo("drying"), "面板没开就打开晾制架面板");
        var cut = Ctx.RenderComponent<DryingOverlay>();
        Assert.That(cut.Find("[data-dry-slot='1']").ClassList, Does.Contain("on"));
        Assert.That(cut.Find("[data-dry-recipe='dry_berries']").ClassList, Does.Contain("on"));
        Assert.That(cut.Find("[data-dry-recipe='dry_meat']").ClassList, Does.Not.Contain("on"));
        Assert.That(cut.Find(".dry-recipes h3").TextContent, Is.EqualTo("投到工位2：选食谱"));
        Assert.That((berry.Owner, DrySlots.Count), Is.EqualTo((ItemInstance.Carried, 1)), "不搬物、不开工");
    }

    /// <summary>
    /// FAC-Q870-04／Q924（陶窑网页壳）：两批里取走第一批的 1 份黏土——该行变「批次1 · 陶壳 · 余料」（不再写进度），
    /// 标题只数真实正在加工的批「加工中1批」（S11 §Q870 B）；投料区列出补投预览句。
    /// </summary>
    [Test]
    public void Station_PartialArrow_ResidualRow_TitleCountsProcessing()
    {
        E.DebugBuildAllWorkshops(S);
        S.Skills["crafting"].Level = 3;
        S.Progress.Flags.Add("h03_ceramic_notice_read");
        S.Items.RemoveAll(i => i.ItemId is "clay" or "charcoal" or "ceramic_shell");
        E.CreateItems(S, "clay", 4, S.Ground);
        E.CreateItems(S, "charcoal", 2, S.Ground);
        Ok(E.StartStationBatch(S, "ex_ceramic_shell", "k", 2));
        G.OpenWorkspace(E.WorkspaceForStationHere(S, "kiln")!, "ex_ceramic_shell");
        var cut = Ctx.RenderComponent<CraftOverlay>();
        Assert.That(cut.Find("[data-station-title]").TextContent, Is.EqualTo("加工中2批"));
        Assert.That(cut.FindAll("[data-station-inputs] [data-pt=fac-item]"), Has.Count.EqualTo(2), "每批一张黏土卡");

        cut.FindAll("[data-station-inputs] [data-pt=fac-item] button.card-arrow")[0].Click(detail: 0);

        Assert.That(Sum("clay", ItemInstance.Carried), Is.EqualTo(1));
        Assert.That(cut.Find("[data-station-title]").TextContent, Is.EqualTo("加工中1批"), "只数真实正在加工的批");
        var rows = cut.FindAll(".sq-row");
        Assert.That(rows, Has.Count.EqualTo(2));
        Assert.That(rows[0].ClassList, Does.Contain("residual"));
        Assert.That(rows[0].QuerySelector(".cg-facts b")!.TextContent, Is.EqualTo("批次1 · 陶壳 · 余料"));
        Assert.That(rows[1].ClassList, Does.Not.Contain("residual"));

        Ok(E.DoInstant(S, InstantRequest.Drop(S.Items.Single(i => i.ItemId == "clay" && i.Owner == ItemInstance.Carried).InstanceId, 1)));
        E.CreateItems(S, "charcoal", 1, S.Ground);
        cut.Render();
        Assert.That(cut.Find("[data-station-refill]").TextContent, Is.EqualTo("先用余料黏土×1，再从随身与当前地面取黏土×1、木炭×1"));
    }

    /// <summary>Q870 B：设施里只剩余料条目时不显示「加工中{批数}批」标题与并行说明。</summary>
    [Test]
    public void Station_OnlyResidual_NoTitle()
    {
        E.DebugBuildAllWorkshops(S);
        S.Skills["crafting"].Level = 3;
        S.Progress.Flags.Add("h03_ceramic_notice_read");
        S.Items.RemoveAll(i => i.ItemId is "clay" or "charcoal" or "ceramic_shell");
        E.CreateItems(S, "clay", 2, S.Ground);
        E.CreateItems(S, "charcoal", 1, S.Ground);
        Ok(E.StartStationBatch(S, "ex_ceramic_shell", "k"));
        G.OpenWorkspace(E.WorkspaceForStationHere(S, "kiln")!, "ex_ceramic_shell");
        var cut = Ctx.RenderComponent<CraftOverlay>();
        cut.Find("[data-station-inputs] [data-pt=fac-item] button.card-arrow").Click(detail: 0);
        Assert.That(cut.FindAll("[data-station-title]"), Is.Empty);
        Assert.That(cut.FindAll(".sq-row.residual"), Has.Count.EqualTo(1), "余料条目仍列出");
    }

    /// <summary>
    /// FACF-Q927-01／02：火堆食物卡箭头按住——先取这张卡（取到随身），之后按原节奏继续只认按下时这张卡；卡取完即停，不拿相邻的另一份。
    /// </summary>
    [Test]
    public async Task FireFood_Hold_OnlyThatCard()
    {
        S.Items.RemoveAll(i => i.ItemId == "raw_meat");
        var a = E.CreateItems(S, "raw_meat", 1, S.Ground)[0];
        var b = E.CreateItems(S, "raw_meat", 1, S.Ground)[0];
        Ok(E.DoInstant(S, InstantRequest.PutInFireSlot(a.InstanceId, 0)));
        var inFire = S.Items.Where(i => i.ItemId == "raw_meat" && i.InFacility).ToList();
        Assert.That(inFire, Has.Count.EqualTo(1), "起点：火堆加工位 1 有一份生肉");
        var food = inFire[0];
        Ok(E.DoInstant(S, InstantRequest.PutInFireSlot(S.Items.Single(i => i.ItemId == "raw_meat" && i.Owner == S.Ground).InstanceId, 1)));
        var neighbor = S.Items.Single(i => i.ItemId == "raw_meat" && i.InFacility && i.InstanceId != food.InstanceId);

        var cut = Ctx.RenderComponent<FoodCard>(p => p.Add(c => c.Item, food).Add(c => c.Slot, 1));
        var arrow = cut.Find("button.row-arrow");
        Assert.That(arrow.GetAttribute("aria-label"), Is.EqualTo("把生肉取到随身"), "沿原可访问名称");
        await cut.InvokeAsync(() => arrow.PointerDownAsync(new PointerEventArgs { Button = 0 }));
        await Task.Delay(800);   // 过 400 毫秒起连发、每 100 毫秒一次

        Assert.That(Sum("raw_meat", ItemInstance.Carried), Is.EqualTo(1), "只取按下的那张");
        Assert.That(neighbor.InFacility, Is.True, "相邻食物不动");
        await cut.InvokeAsync(() => cut.Find("button.row-arrow").PointerUpAsync(new PointerEventArgs()));
    }

    // ───────────── 试玩修复 B2：点（含键盘 Enter）设施物品卡后右栏看这张卡的详情（S11 §Q925 B） ─────────────

    /// <summary>
    /// 试玩修复 B2（S11 §Q925 B「点本体看右栏详情」）：晾制架面板里用键盘 Enter 选中位内野果卡，右栏显示这张卡的只读详情（名称「野果」），
    /// 不再是「这件物品已不在此处。」。
    /// </summary>
    [Test]
    public void DryingCard_KeyboardEnter_RightRailShowsItem()
    {
        BerriesDrying();
        G.OpenPanel("drying");
        var cut = Ctx.RenderComponent<DryingOverlay>();
        var rail = Ctx.RenderComponent<DetailPanel>();
        cut.Find("[data-pt=fac-item]").KeyDown(new KeyboardEventArgs { Key = "Enter" });
        rail.Render();
        Assert.That(rail.Markup, Does.Not.Contain("这件物品已不在此处"));
        Assert.That(rail.Find(".rail-head h2").TextContent.Trim(), Is.EqualTo("野果"));
        Assert.That(cut.FindAll("[data-pt=fac-take]"), Has.Count.EqualTo(1), "数量窗同时打开");
    }

    /// <summary>
    /// 试玩修复 B2（S11 §Q925 B）：陶窑工作区里点批内黏土卡，右栏从配方预估改为这张卡的只读详情（名称「黏土」）；
    /// 对照：点卡前右栏是配方预估（没有物品表头）。
    /// </summary>
    [Test]
    public void StationCard_Click_RightRailShowsItemNotRecipe()
    {
        E.DebugBuildAllWorkshops(S);
        S.Skills["crafting"].Level = 3;
        S.Progress.Flags.Add("h03_ceramic_notice_read");
        S.Items.RemoveAll(i => i.ItemId is "clay" or "charcoal" or "ceramic_shell");
        E.CreateItems(S, "clay", 2, S.Ground);
        E.CreateItems(S, "charcoal", 1, S.Ground);
        Ok(E.StartStationBatch(S, "ex_ceramic_shell", "k"));
        G.OpenWorkspace(E.WorkspaceForStationHere(S, "kiln")!, "ex_ceramic_shell");
        var cut = Ctx.RenderComponent<CraftOverlay>();
        var rail = Ctx.RenderComponent<DetailPanel>();
        Assert.That(rail.FindAll(".rail-page .rail-head h2").Select(h => h.TextContent.Trim()), Does.Not.Contain("黏土"), "起点：右栏是配方预估");

        cut.Find("[data-station-inputs] [data-pt=fac-item]").Click();
        rail.Render();

        Assert.That(rail.Find(".rail-page .rail-head h2").TextContent.Trim(), Is.EqualTo("黏土"));
        Assert.That(rail.Markup, Does.Not.Contain("这件物品已不在此处"));
    }
}
