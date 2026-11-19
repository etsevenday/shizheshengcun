using Bunit;
using Survival.Core.Engine;
using Survival.Core.State;
using Survival.Web.Components;

namespace Survival.Web.Tests;

/// <summary>
/// B56 网页层行为样板（2026-11-19）：这两处逻辑在网页壳里，Survival.Core.Tests 不引用网页壳，以前只能读源码比字符串。
/// 这里真正渲染组件、改输入框、点按钮，断玩家看到的结果。
/// </summary>
public class B56WaterUiTests : WebTestBase
{
    private const string Lin = "npc_lin_zhao";

    /// <summary>
    /// B56 试玩修复 BUG-1（开发假设 5；S12 文首 B53a「水量不足…的数量禁用原因：水分不足。」；W100-Q929-06 超量整次不扣）：
    /// 给林昭饮用、来源林昭的壶 700 毫升（可饮 7 份），在份数框输入 8——输入保留 8、写「不可用：水分不足。」、确认置灰；
    /// 改成 7 后可确认，确认句按 S12 逐字稿「给{目标}饮用：{水壶}内{水种}{所选毫升}毫升 · 5分钟」。
    /// 当时的 bug 是网页壳把 8 静默夹成 7（内核测试和只变异 src/Core 的变异测试都照不到）。
    /// </summary>
    [Test]
    public void FeedPop_CanteenOverMax_KeepsInput_ShowsShortage_ThenExactMaxConfirms()
    {
        S.Progress.Flags.Add("hub_registered");
        S.Progress.Flags.Add("church_route_record");
        S.Progress.Flags.Add("academy_safety_record");
        E.DebugRecruit(S, Lin);
        var c = E.CreateItems(S, "ration_canteen", 1, ItemInstance.CarriedOwner(Lin)).Single();
        c.LiquidId = "clean_water";
        c.LiquidMl = 700;
        G.OpenFeed(E.FeedEntry(S, c.InstanceId)!, "card-sel");

        var pop = Ctx.RenderComponent<FeedPop>();
        pop.Find($"select[aria-label='{FeedPanelCopy.SectionTarget}']").Change(Lin);
        var source = pop.Find($"select[aria-label='{FeedPanelCopy.SectionSource}']");
        var row = source.QuerySelectorAll("option").Single(o => o.TextContent.Contains("分量水壶"));
        source.Change(row.GetAttribute("value"));

        pop.Find("[data-feed-units] input").Change("8");
        Assert.That(pop.Find("[data-feed-units] input").GetAttribute("value"), Is.EqualTo("8"), "超量保留玩家输入，不夹到 7");
        Assert.That(pop.Find("[data-feed-blocked]").TextContent, Is.EqualTo("不可用：水分不足。"));
        Assert.That(ConfirmButton(pop).HasAttribute("disabled"), Is.True, "超量不能确认");
        Assert.That(c.LiquidMl, Is.EqualTo(700), "只看不扣");

        pop.Find("[data-feed-units] input").Change("7");
        Assert.That(pop.FindAll("[data-feed-blocked]"), Is.Empty);
        var ok = ConfirmButton(pop);
        Assert.That(ok.HasAttribute("disabled"), Is.False);
        Assert.That(ok.TextContent, Does.Contain("给林昭饮用：分量水壶内净水700毫升 · 5分钟"));
    }

    private static AngleSharp.Dom.IElement ConfirmButton(IRenderedComponent<FeedPop> pop) => pop.Find("[data-feed-confirm]");

    /// <summary>
    /// 02 Q979 B（S14 2026-11-18 B56 次要原批；W56-Q979-01）：壶里只剩 40 毫升（不足一份 100 毫升）时，丢弃水子区
    /// 不写常驻原因行；「选择数量」置灰但可聚焦（aria-disabled，不用原生 disabled）；点它只在原因行显示「水分不足。」、不进数量层；
    /// 「全部丢弃」仍能进最终确认，按实际 40 毫升列出。
    /// </summary>
    [Test]
    public void DiscardWater_UnderOneUnit_ReasonOnlyWhenViewed_DiscardAllStillWorks()
    {
        var c = E.CreateItems(S, "ration_canteen", 1, ItemInstance.Carried).Single();
        c.LiquidId = "clean_water";
        c.LiquidMl = 40;
        var target = DiscardTarget.CanteenWater(c.InstanceId);
        var panel = Ctx.RenderComponent<DiscardPanel>(p => p.Add(x => x.Target, target));

        panel.Find("[data-pt=discard-water]").Click();
        Assert.That(panel.FindAll(".discard-sub .blocked"), Is.Empty, "没有常驻原因行");
        var qty = panel.Find("[data-pt=discard-qty]");
        Assert.That(qty.GetAttribute("aria-disabled"), Is.EqualTo("true"), "看起来置灰");
        Assert.That(qty.HasAttribute("disabled"), Is.False, "不用原生 disabled，键盘与触屏还能点到它查看原因");
        Assert.That(qty.GetAttribute("title"), Is.EqualTo("水分不足。"), "悬停原因");
        var described = qty.GetAttribute("aria-describedby");
        Assert.That(panel.Find($"#{described}").TextContent, Is.EqualTo("水分不足。"), "读屏原因");

        qty.Click();
        Assert.That(panel.FindAll(".discard-sub .blocked").Select(e => e.TextContent), Is.EqualTo(new[] { "水分不足。" }), "点按后才显示原因");
        Assert.That(panel.FindAll("[data-pt=discard-next]"), Is.Empty, "没有进数量层");

        panel.Find("[data-pt=discard-all]").Click();
        Assert.That(panel.Find(".discard-sub").TextContent, Does.Contain("40毫升"), "全部丢弃按实际尾水列出最终确认");
        Assert.That(panel.FindAll("[data-pt=discard-confirm]"), Has.Count.EqualTo(1));
    }
}
