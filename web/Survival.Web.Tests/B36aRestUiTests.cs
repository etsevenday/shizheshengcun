using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Survival.Core.Engine;
using Survival.Web.Components;

namespace Survival.Web.Tests;

/// <summary>
/// B36a M4：休息页重排（02 Q815 用户自定、Q887 B、Q896 A、Q897 A；开发方 D1—D3＝02 Q1123—Q1125 A）。
/// 渲染休息页中部与右栏（DetailPanel），真的悬停、聚焦、点时长、点开始，断开始键、短原因、右栏预估与回到本页后的所选。
/// </summary>
public class B36aRestUiTests : WebTestBase
{
    private IRenderedComponent<RestOverlay> Open()
    {
        G.OpenTopPanel("rest");
        return Ctx.RenderComponent<RestOverlay>();
    }

    /// <summary>按钮自身文字（去掉键帽 kbd 里的提示）。</summary>
    private static string Own(AngleSharp.Dom.IElement b) => string.Concat(b.ChildNodes.OfType<AngleSharp.Dom.IText>().Select(t => t.Data)).Trim();

    private static string[] Row(IRenderedComponent<RestOverlay> cut, string kind) =>
        cut.FindAll($"[data-pt=rest-pick][data-kind={kind}]").Select(b => b.TextContent.Trim()).ToArray();

    private static AngleSharp.Dom.IElement Pick(IRenderedComponent<RestOverlay> cut, string kind, int min) =>
        cut.Find($"[data-pt=rest-pick][data-kind={kind}][data-min='{min}']");

    /// <summary>
    /// Q815：同屏两行，只给原档位小时数——休息0.5／1／2小时、睡眠2／4／8小时；时长格里不再铺每小时恢复说明。
    /// Q887 B：真正新开不预选（这里人很困、按原规则会暗选睡8小时），开始键禁用写「开始」（D2，Q1124 A 暂用），旁边短原因「先选时长」；
    /// D3（Q1125 A）：没选也没悬停时右栏不出休息预估。
    /// </summary>
    [Test]
    public void NewOpen_TwoHourRows_NoPreselect_StartDisabledWithReason()
    {
        S.Stats.Wakefulness = 10;   // 起点：困（旧规则会预选睡眠8小时）
        var cut = Open();
        var rail = Ctx.RenderComponent<DetailPanel>();

        Assert.That(Row(cut, "rest"), Is.EqualTo(new[] { "0.5小时", "1小时", "2小时" }));
        Assert.That(Row(cut, "sleep"), Is.EqualTo(new[] { "2小时", "4小时", "8小时" }));
        Assert.That(cut.Find("[data-pt=rest-plan]").TextContent, Does.Not.Contain("每小时").And.Not.Contain("/小时清醒"), "时长格不铺恢复速率");
        Assert.That(cut.FindAll("[data-pt=rest-pick].on"), Is.Empty, "不预选");
        var start = cut.Find("[data-pt=rest-start]");
        Assert.That(start.HasAttribute("disabled"), Is.True);
        Assert.That(Own(start), Is.EqualTo("开始"));
        Assert.That(cut.Find("[data-pt=rest-why]").TextContent, Is.EqualTo("先选时长"));
        Assert.That(start.QuerySelectorAll("kbd"), Is.Empty, "不可开始时没有 Enter 键帽");
        Assert.That(rail.FindAll("[data-pt=pv-minutes]"), Is.Empty, "右栏回常规内容");
    }

    /// <summary>
    /// Q815／Q887 B：悬停或键盘聚焦某档，右栏显示该组合的完整预估（本页右栏计划时长用小时，Q897 A），不选中、不启用开始；离开回原样。
    /// </summary>
    [Test]
    public void HoverOrFocus_RailPreviewOnly_NotSelected()
    {
        var cut = Open();
        var rail = Ctx.RenderComponent<DetailPanel>();

        Pick(cut, "rest", 30).TriggerEvent("onmouseenter", new MouseEventArgs());
        Assert.That(rail.Find("[data-pt=pv-minutes]").TextContent, Is.EqualTo("0.5小时"));
        Assert.That((G.RestKind, G.RestMinutes), Is.EqualTo(((string?)null, (int?)null)), "悬停不选中");
        Assert.That(cut.Find("[data-pt=rest-start]").HasAttribute("disabled"), Is.True, "悬停不启用开始");
        Pick(cut, "rest", 30).TriggerEvent("onmouseleave", new MouseEventArgs());
        Assert.That(rail.FindAll("[data-pt=pv-minutes]"), Is.Empty, "离开后没有所选就回常规内容");

        Pick(cut, "sleep", 240).Focus();
        Assert.That(rail.Find("[data-pt=pv-minutes]").TextContent, Is.EqualTo("4小时"), "键盘聚焦同源预估");
        Pick(cut, "sleep", 240).Blur();
        Assert.That(rail.FindAll("[data-pt=pv-minutes]"), Is.Empty);
    }

    /// <summary>
    /// Q887 B：明确点一档才选中，开始键可用，按真实方式与小时数写「休息0.5小时」（Q896／Q897 例）；开始条写「休息 · 0.5小时」。
    /// 选中后悬停别的档右栏临时看那一档，离开回所选。
    /// </summary>
    [Test]
    public void ClickPick_StartEnabledWithHours_HoverOtherThenBackToPicked()
    {
        var cut = Open();
        var rail = Ctx.RenderComponent<DetailPanel>();
        Pick(cut, "rest", 30).Click();

        Assert.That(cut.FindAll("[data-pt=rest-pick].on").Select(b => b.GetAttribute("data-min")).ToArray(), Is.EqualTo(new[] { "30" }));
        var start = cut.Find("[data-pt=rest-start]");
        Assert.That(start.HasAttribute("disabled"), Is.False);
        Assert.That(Own(start), Is.EqualTo("休息0.5小时"));
        Assert.That(start.QuerySelectorAll("kbd"), Has.Length.EqualTo(1), "可开始时有 Enter 键帽");
        Assert.That(cut.Find("[data-pt=rest-picked]").TextContent, Is.EqualTo("休息 · 0.5小时"));
        Assert.That(cut.FindAll("[data-pt=rest-why]"), Is.Empty);
        Assert.That(rail.Find("[data-pt=pv-minutes]").TextContent, Is.EqualTo("0.5小时"));

        Pick(cut, "sleep", 480).TriggerEvent("onmouseenter", new MouseEventArgs());
        Assert.That(rail.Find("[data-pt=pv-minutes]").TextContent, Is.EqualTo("8小时"));
        Pick(cut, "sleep", 480).TriggerEvent("onmouseleave", new MouseEventArgs());
        Assert.That(rail.Find("[data-pt=pv-minutes]").TextContent, Is.EqualTo("0.5小时"), "离开回所选");
    }

    /// <summary>
    /// Q896 A：中部「休息条件」（原「当前条件」）照真实条件；实际精神低才出「风险」（原「体力恢复减半」说明），濒死同样在「风险」下。
    /// 配对：精神正常、没濒死时不出「风险」。
    /// </summary>
    [Test]
    public void ConditionAlways_RiskOnlyWhenReal()
    {
        var cut = Open();
        Assert.That(cut.Find("[data-pt=rest-cond] h3").TextContent, Is.EqualTo("休息条件"));
        Assert.That(cut.Markup, Does.Not.Contain("当前条件"));
        Assert.That(cut.FindAll("[data-pt=rest-risk]"), Is.Empty, "无风险整段不出");

        S.Stats.Morale = 5;
        E.DebugDying(S, "h0");
        G.Notify();
        cut.Render();
        var risk = cut.Find("[data-pt=rest-risk]");
        Assert.That(risk.QuerySelector("h3")!.TextContent, Is.EqualTo("风险"));
        Assert.That(risk.TextContent, Does.Contain("体力恢复减半"));
        Assert.That(risk.TextContent, Does.Contain(GameEngine.DyingIconText));
    }

    /// <summary>Q887 B：离开整页再开恢复未选；从本页进系统说明再返回同一窗口，保留已选（不算新开）。</summary>
    [Test]
    public void SubPageReturnKeepsPick_ReopenClears()
    {
        var cut = Open();
        Pick(cut, "sleep", 240).Click();
        G.OpenTopPanel("help");
        G.CloseHelp();
        Assert.That(G.Panel, Is.EqualTo("rest"), "起点：回到休息页");
        cut.Render();
        Assert.That(cut.FindAll("[data-pt=rest-pick].on").Select(b => b.GetAttribute("data-min")).ToArray(), Is.EqualTo(new[] { "240" }), "子页返回保留草稿");

        cut.Find("[data-pt=rest-close]").Click();
        G.OpenTopPanel("rest");
        cut.Render();
        Assert.That(cut.FindAll("[data-pt=rest-pick].on"), Is.Empty, "关页重开未选");
    }

    /// <summary>
    /// Q1103 B（B60a）＋D1（Q1123 A）：开工后休息页留在进度浮窗下，结束回到本页时原选档保留；
    /// 焦点不落回开始键（开始键标 data-lock-noreturn，原页落点给所选时长）。
    /// </summary>
    [Test]
    public async Task AfterRun_BackToPage_PickKept_FocusToPickedNotStart()
    {
        G.OpenTopPanel("rest");
        var cut = Ctx.RenderComponent<GameScreen>();
        cut.Find("[data-pt=rest-pick][data-kind=rest][data-min='30']").Click();
        await cut.Find("[data-pt=rest-start]").ClickAsync(new MouseEventArgs());
        await cut.InvokeAsync(() => cut.Render());
        Assert.That(S.Main, Is.Null, "休息已结束");
        Assert.That(G.Panel, Is.EqualTo("rest"));
        Assert.That((G.RestKind, G.RestMinutes), Is.EqualTo(("rest", (int?)30)), "原选档保留");
        Assert.That(cut.Find("[data-pt=rest-start]").HasAttribute("data-lock-noreturn"), Is.True);
        var end = Ctx.JSInterop.Invocations.Where(i => i.Identifier == "survival.progLockEnd").ToList();
        Assert.That(end, Is.Not.Empty, "起点核对：浮窗退出时通知了网页");
        Assert.That(end[^1].Arguments[0], Is.EqualTo(new[] { ".rest-panel [data-pt=rest-pick].on", ".rest-panel" }));
    }
}
