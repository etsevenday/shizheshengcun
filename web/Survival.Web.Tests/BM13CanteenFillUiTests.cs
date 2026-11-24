using Bunit;
using Survival.Core.Engine;
using Survival.Core.State;
using Survival.Web.Components;
using Survival.Web.Services;

namespace Survival.Web.Tests;

/// <summary>
/// BM13（02 Q1011 A；合同 assets/20261119/b56-q1010-q1011-drink-delta-fill-source.json B56-Q1011-01、02）：水壶「装水」专窗标题下写
/// 「来源：{实际归属}{水名} {该堆可用份数}份」，上限＝min(这堆整份数, 壶内空余)；Q978 A 定稿默认 1 份、旁写毫升、「确认{数量}」沿用。
/// 真实渲染 CardActions（选中水壶），点「装入净水」打开装水窗，断玩家看到的文字与确认后的真实扣量。
/// 本类同时接替 Survival.Core.Tests.B56UiWiringTests.CanteenFillPour_QuantityWindow_Q978A 的读源码断言（2026-11-19 第 8 条：改到组件时顺手换成 bUnit）。
/// </summary>
public class BM13CanteenFillUiTests : WebTestBase
{
    private ItemInstance _can = null!;

    private void ClearWater() => S.Items.RemoveAll(i => i.ItemId is "clean_water" or "dirty_water");

    private IRenderedComponent<CardActions> OpenCanteen(int canteenMl = 0)
    {
        _can = E.CreateItems(S, "ration_canteen", 1, ItemInstance.Carried).Single();
        if (canteenMl > 0) { _can.LiquidId = "clean_water"; _can.LiquidMl = canteenMl; }
        G.Notify();
        var sel = new Selection { Kind = SelectionKind.Tool, Id = _can.ItemId, Owner = _can.Owner, InstanceId = _can.InstanceId };
        return Ctx.RenderComponent<CardActions>(p => p.Add(x => x.Sel, sel));
    }

    private static void OpenFill(IRenderedComponent<CardActions> cut, string waterId = "clean_water") =>
        cut.Find($"[data-pt=canteen-fill][data-water={waterId}]").Click();

    private static string Source(IRenderedComponent<CardActions> cut) => cut.Find("[data-canteen-op=fill] [data-pt=canteen-fill-source]").TextContent;
    private static AngleSharp.Dom.IElement Input(IRenderedComponent<CardActions> cut) => cut.Find("[data-canteen-op=fill] input");
    private static AngleSharp.Dom.IElement Confirm(IRenderedComponent<CardActions> cut) => cut.Find("[data-canteen-op=fill] [data-pt=canteen-op-confirm]");
    private static AngleSharp.Dom.IElement Plus(IRenderedComponent<CardActions> cut) => cut.Find("[data-canteen-op=fill] button[aria-label=增加]");

    [Test]
    public void Q1011_01_CarriedFirst_SourceLine_DefaultOne_StepperStopsAtStackCount()
    {
        // B56-Q1011-01：壶可装 20 份、随身 5 份、地面 50 份 → 「来源：随身净水 5份」、上限 5
        ClearWater();
        E.CreateItems(S, "clean_water", 5, ItemInstance.Carried);
        E.CreateItems(S, "clean_water", 50, S.Ground);
        var cut = OpenCanteen();
        OpenFill(cut);

        Assert.That(cut.Find("[data-canteen-op=fill] .ca-drink-head").TextContent, Is.EqualTo("装水"), "Q978 A 标题");
        Assert.That(Source(cut), Is.EqualTo("来源：随身净水 5份"));
        // Q978 A 定稿：默认 1 份、旁写实际毫升、「确认{数量}」
        Assert.That(Input(cut).GetAttribute("value"), Is.EqualTo("1"));
        Assert.That(cut.Find("[data-canteen-op=fill] .tbar-qlabel").TextContent, Is.EqualTo("100毫升"));
        Assert.That(Confirm(cut).TextContent, Is.EqualTo("确认1"));

        for (var k = 0; k < 8; k++) Plus(cut).Click();
        Assert.That(Input(cut).GetAttribute("value"), Is.EqualTo("5"), "步进到这堆的 5 份为止，不按地面 50 份");
        Assert.That(cut.Find("[data-canteen-op=fill] .tbar-qlabel").TextContent, Is.EqualTo("500毫升"));

        Input(cut).Input("6");
        Assert.That(Confirm(cut).HasAttribute("disabled"), Is.True, "超过上限不能确认");
        Assert.That(cut.Find("[data-canteen-op=fill] .ca-note.bad").TextContent, Is.EqualTo("请输入 1 到 5 之间的整数。"));

        Input(cut).Input("5");
        Assert.That(Confirm(cut).HasAttribute("disabled"), Is.False);
        Confirm(cut).Click();
        Assert.That(_can.LiquidMl, Is.EqualTo(500));
        Assert.That(S.Items.Where(i => i.ItemId == "clean_water" && i.IsCarried).Sum(i => i.Count), Is.EqualTo(0), "随身那堆全装走");
        Assert.That(S.Items.Where(i => i.ItemId == "clean_water" && i.Owner == S.Ground).Sum(i => i.Count), Is.EqualTo(50), "地面不动");
    }

    [Test]
    public void Q1011_01b_RoomOnlyTwo_SourceStillFive_MaxTwo()
    {
        // B56-Q1011-01 后半：壶只剩 2 份空间，来源仍显示 5 份而数量上限为 2
        ClearWater();
        E.CreateItems(S, "clean_water", 5, ItemInstance.Carried);
        var cut = OpenCanteen(1800);
        OpenFill(cut);
        Assert.That(Source(cut), Is.EqualTo("来源：随身净水 5份"));
        for (var k = 0; k < 5; k++) Plus(cut).Click();
        Assert.That(Input(cut).GetAttribute("value"), Is.EqualTo("2"));
        Input(cut).Input("3");
        Assert.That(cut.Find("[data-canteen-op=fill] .ca-note.bad").TextContent, Is.EqualTo("请输入 1 到 2 之间的整数。"));
    }

    [Test]
    public void Q1011_02_GroundOnly_SourceSaysGround_ConfirmDeductsThatStack()
    {
        // B56-Q1011-02：仅有地面合法水堆时标地面来源，确认只扣所选水堆
        ClearWater();
        var ground = E.CreateItems(S, "clean_water", 50, S.Ground).Single();
        var cut = OpenCanteen();
        OpenFill(cut);
        Assert.That(Source(cut), Is.EqualTo("来源：地面净水 50份"));
        Input(cut).Input("3");
        Assert.That(Confirm(cut).TextContent, Is.EqualTo("确认3"));
        Confirm(cut).Click();
        Assert.That((ground.Count, _can.LiquidMl), Is.EqualTo((47, 300)));
        Assert.That(cut.FindAll("[data-canteen-op=fill]"), Is.Empty, "成功后收起数量窗");
    }

    [Test]
    public void Q1011_03_SourceGoneBeforeConfirm_OriginalReason_NoSourceLine()
    {
        // 「确认前复核本次所选来源及水壶，失效时按原原因处理」：所选那堆不在了 → 原「物品已变，请重选。」，确认置灰、不写来源行
        ClearWater();
        var carried = E.CreateItems(S, "clean_water", 5, ItemInstance.Carried).Single();
        var cut = OpenCanteen();
        OpenFill(cut);
        Assert.That(Source(cut), Is.EqualTo("来源：随身净水 5份"), "配对：来源在时写来源行");
        S.Items.Remove(carried);
        G.Notify();
        cut.Render();
        Assert.That(cut.FindAll("[data-pt=canteen-fill-source]"), Is.Empty);
        Assert.That(cut.Find("[data-canteen-op=fill] .ca-note.bad").TextContent, Is.EqualTo(DiscardCopy.Changed));
        Assert.That(Confirm(cut).HasAttribute("disabled"), Is.True);
    }

    [Test]
    public void Q978_Pour_QuantityWindow_TitleDefaultConfirm()
    {
        // 接替 B56UiWiringTests.CanteenFillPour_QuantityWindow_Q978A 的倒出半边：02 Q978 A 标题「倒出」、默认 1 份、「确认{数量}」，所选份数一次倒出
        ClearWater();
        var cut = OpenCanteen(700);
        cut.Find("[data-pt=canteen-pour][data-zone=carried]").Click();
        Assert.That(cut.Find("[data-canteen-op=pour] .ca-drink-head").TextContent, Is.EqualTo("倒出"));
        Assert.That(cut.FindAll("[data-pt=canteen-fill-source]"), Is.Empty, "来源行只在装水窗");
        var input = cut.Find("[data-canteen-op=pour] input");
        Assert.That(input.GetAttribute("value"), Is.EqualTo("1"));
        input.Input("4");
        var ok = cut.Find("[data-canteen-op=pour] [data-pt=canteen-op-confirm]");
        Assert.That(ok.TextContent, Is.EqualTo("确认4"));
        ok.Click();
        Assert.That(_can.LiquidMl, Is.EqualTo(300));
        Assert.That(S.Items.Where(i => i.ItemId == "clean_water" && i.IsCarried).Sum(i => i.Count), Is.EqualTo(4));
    }
}
