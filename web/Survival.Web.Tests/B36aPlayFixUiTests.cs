using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Survival.Core.Engine;
using Survival.Core.State;
using Survival.Web.Components;
using Survival.Web.Services;

namespace Survival.Web.Tests;

/// <summary>
/// B36a 本地 Release 包试玩修复（组件侧）：B1 休息页右栏预估名用小时、B2 休息页容器不收回焦点、D2 系统说明左栏定位到所选项。
/// 浏览器脚本与样式见 Survival.Core.Tests 的 B36aPlayFixShellTests。
/// </summary>
public class B36aPlayFixUiTests : WebTestBase
{
    private IRenderedComponent<RestOverlay> OpenRest()
    {
        G.OpenTopPanel("rest");
        return Ctx.RenderComponent<RestOverlay>();
    }

    /// <summary>
    /// B1（02 Q897 A「仅休息页时长格、开始键／已选时长及本页右栏计划时长用小时，如0.5小时」）：右栏预估名原取内核行动名「休息30分钟」，
    /// 与同栏「预计耗时 0.5小时」不一致；现在右栏名写「休息0.5小时」，悬停睡眠8小时一档写「睡眠8小时」，右栏不再出现「30分钟」。
    /// 场景 tools/playtest/scenarios/b36a-rest-keys.json 第 26 步在真浏览器里断同一件事。
    /// </summary>
    [Test]
    public void RestRail_NameInHours()
    {
        var cut = OpenRest();
        var rail = Ctx.RenderComponent<DetailPanel>();
        cut.Find("[data-pt=rest-pick][data-kind=rest][data-min='30']").Click();
        Assert.That(rail.Find(".rail-head h2").TextContent.Trim(), Is.EqualTo("休息0.5小时"));
        Assert.That(rail.Find("[data-pt=pv-minutes]").TextContent, Is.EqualTo("0.5小时"));
        Assert.That(rail.Markup, Does.Not.Contain("30分钟"));

        cut.Find("[data-pt=rest-pick][data-kind=sleep][data-min='480']").TriggerEvent("onmouseenter", new MouseEventArgs());
        Assert.That(rail.Find(".rail-head h2").TextContent.Trim(), Is.EqualTo("睡眠8小时"));
    }

    /// <summary>配对：别处同一行动的预估名不动（仍是内核行动名，Q897 A 只限休息页）。</summary>
    [Test]
    public void OtherPreview_KeepsEngineLabel()
    {
        var req = ActionRequest.Rest(30);
        var cut = Ctx.RenderComponent<PreviewView>(ps => ps.Add(p => p.Request, req));
        Assert.That(cut.Find(".rail-head h2").TextContent.Trim(), Is.EqualTo(G.Preview(req).Label));
    }

    /// <summary>
    /// B2（02 Q1123 A）：鼠标点开始键时 B43 把焦点归到本页容器 .rest-panel；容器也标 data-lock-noreturn，
    /// 开工结束时 app.js 不把焦点还给容器（否则回车按本页唯一确认又开一轮），改落原页落点「所选时长」。
    /// </summary>
    [Test]
    public void RestPanel_MarkedNoReturn_FallbackStillPickedDuration()
    {
        var cut = OpenRest();
        var panel = cut.Find(".rest-panel");
        Assert.That(panel.HasAttribute("data-lock-noreturn"), Is.True);
        Assert.That(panel.HasAttribute("data-enter-scope"), Is.True, "辅助：仍是本页确认区（B43）");
        Assert.That(G.ProgReturnFocus, Is.EqualTo(new[] { ".rest-panel [data-pt=rest-pick].on", ".rest-panel" }), "辅助：原页落点先所选时长");
    }

    /// <summary>
    /// D2：从食饮盘「查看食饮说明」直接定位到条目时，左栏要滚到所选条目——渲染后调用 survival.revealQuery，选择器指向左栏里真实存在的那个条目按钮。
    /// 配对：顶栏正常新开（没选主题）不调用；同一条目重绘不重复调用。
    /// </summary>
    [Test]
    public void FeedDirectEntry_RevealsPickedNavItem_Once()
    {
        S.Progress.Flags.Add("hub_registered");
        S.Progress.Flags.Add("church_route_record");
        S.Progress.Flags.Add("academy_safety_record");
        E.DebugRecruit(S, "npc_lin_zhao");
        var c = E.CreateItems(S, "ration_canteen", 1, ItemInstance.CarriedOwner("npc_lin_zhao")).Single();
        c.LiquidId = "clean_water";
        c.LiquidMl = 700;
        var cut = Ctx.RenderComponent<GameScreen>();
        cut.InvokeAsync(() => G.OpenHelp());
        Assert.That(Reveals(), Is.Empty, "顶栏新开没选主题，不定位");
        cut.InvokeAsync(() => G.CloseHelp());

        cut.InvokeAsync(() => G.OpenFeed(E.FeedEntry(S, c.InstanceId)!, "card-sel"));
        cut.InvokeAsync(() => G.OpenFeedHelp());
        var sel = $"[data-pt=help-entry][data-entry='{GameSession.FeedHelpEntry}']";
        Assert.That(Reveals(), Is.EqualTo(new[] { sel }));
        Assert.That(cut.FindAll(sel), Has.Count.EqualTo(1), "选择器指向左栏里真实的条目按钮");
        Assert.That(cut.Find(sel).ClassList, Does.Contain("on"));

        G.Notify();
        Assert.That(Reveals(), Has.Count.EqualTo(1), "同一条目重绘不重复定位");
    }

    private List<string?> Reveals() =>
        Ctx.JSInterop.Invocations.Where(i => i.Identifier == "survival.revealQuery").Select(i => (string?)i.Arguments[0]).ToList();
}
