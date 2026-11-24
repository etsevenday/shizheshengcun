using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Survival.Core.Engine;
using Survival.Core.State;
using Survival.Web.Components;
using Survival.Web.Services;

namespace Survival.Web.Tests;

/// <summary>
/// B60a R388 网页壳（S01「2026-11-24 B60 暂停继续与撤退后重新开始」、S14「2026-11-24 B60 暂停窗入口与原页返回」Q1100 手写裁定）：
/// 普通行动暂停由居中暂停窗接管输入，取消 Q523 地面／随身拾取让位及 Q569 撤退返程的地图行让位，也不在暂停窗新设这两个入口；
/// 原工具在当地地面时直接「继续行动」。起点用内核入口造（开工、暂停、把工具放到地面），之后点真实按钮、断玩家看到的属性与状态。
/// </summary>
public class B60aPauseResumeUiTests : WebTestBase
{
    private void Ok(ActionResult r) => Assert.That(r.Ok, Is.True, r.Reason);

    private static bool Inert(AngleSharp.Dom.IElement el) => el.HasAttribute("inert");

    /// <summary>地图组件渲染后量视口；bUnit 宽松模式下数组返回 null，这里给固定视口（同 B35bCraftPageUiTests）。</summary>
    private IRenderedComponent<MapOverlay> RenderMap()
    {
        Ctx.JSInterop.Setup<double[]>("survivalMap.size", _ => true).SetResult(new double[] { 900, 600 });
        Ctx.JSInterop.Setup<double[]>("survivalMap.getCenter", _ => true).SetResult(new double[] { 450, 300 });
        return Ctx.RenderComponent<MapOverlay>();
    }

    /// <summary>营地处理野狗尸体（刀随身开工），推进前暂停，再把本行动锁着的刀放到当前地面——原 Q523 例外拾取的局面。</summary>
    private ItemInstance PausedButcherToolOnGround()
    {
        var knife = E.CreateItems(S, "stone_knife", 1, ItemInstance.Carried).Single();
        var carcass = E.CreateItems(S, "carcass_feral_dog", 1, S.Ground).Single();
        Ok(E.Start(S, ActionRequest.Butcher(carcass.InstanceId)));
        S.Main!.Status = ActionStatus.Paused;
        S.Main.PauseReason = PauseReason.StatAlert;
        knife.Owner = S.Ground;
        G.Notify();
        return knife;
    }

    /// <summary>
    /// Q1100（S14 B60）：原工具在当前地面时暂停——暂停遮罩不再给③④开口（scrim 不带让位类、③④ inert、遮罩白名单只剩顶栏「菜单」与底栏存档按钮，02 Q1101 按 A），
    /// 地面那把刀的拾取箭头不可用（不新设拾取入口）；暂停弹窗「继续行动」可用，点了直接继续、做完得肉（B60 验收「原工具在脚边也可直接点『继续行动』」）。
    /// </summary>
    [Test]
    public async Task ToolOnGround_NoPickupYield_ResumeButtonContinues()
    {
        G.OpenFireMenu();   // 起点：背后开着火堆菜单（Esc 不该落到被遮住的面板）
        var knife = PausedButcherToolOnGround();
        Assert.That(G.Panel, Is.EqualTo(GameSession.FireMenuPanel));
        var cut = Ctx.RenderComponent<GameScreen>();

        var scrim = cut.Find(".pause-scrim");
        Assert.That(scrim.ClassName, Is.EqualTo("pause-scrim"), "不挂原拾取／返程让位类");
        Assert.That(cut.Find("[data-pt=prog-focus]").GetAttribute("data-keep"), Is.EqualTo(GameSession.ProgressKeepBase));
        Assert.That(Inert(cut.Find("[data-pt=shelf-ground]")) && Inert(cut.Find("[data-pt=shelf-carried]")), Is.True, "③④随背景一起盖住");
        Assert.That(Inert(cut.Find(".rows2-inner")), Is.True, "①②同样盖住");
        var arrow = cut.Find("[data-pt=shelf-ground] [data-pt=card-stone_knife] .card-arrow");
        Assert.That(arrow.HasAttribute("disabled"), Is.True, "地面原工具不开放拾取");
        Assert.That(G.EscOwner, Is.EqualTo(EscOwner.OpenMenu), "Esc 只开游戏菜单，不收被遮住的火堆菜单（不再有开放行时的原顺序）");

        var go = cut.Find("[data-pt=pause-dialog] .cp-go");
        Assert.That(go.HasAttribute("disabled"), Is.False, "继续行动可用");
        await go.ClickAsync(new MouseEventArgs());
        Assert.That(S.Main, Is.Null, "继续后做完");
        Assert.That(S.Items.Count(i => i.ItemId == "raw_meat"), Is.GreaterThan(0));
        Assert.That(knife.Owner, Is.EqualTo(S.Ground), "工具没被搬动");
    }

    /// <summary>
    /// Q1100 取消 Q569 撤退返程地图行让位：主行动暂停时，地图路线区的「出发」不可用（不再按 PausedTravelOpen 放行）。
    /// 配对：退出行动后同一相邻地点「出发」可用。
    /// </summary>
    [Test]
    public void Paused_MapDepartDisabled_ThenEnabledWhenIdle()
    {
        G.OpenTopPanel("map");
        Ok(E.Start(S, ActionRequest.Wait(60)));
        S.Main!.Status = ActionStatus.Paused;
        S.Main.PauseReason = PauseReason.StatAlert;
        G.PickMapNode("field");
        var map = RenderMap();
        Assert.That(map.Find("[data-pt=map-depart]").HasAttribute("disabled"), Is.True, "暂停中不能出发");

        Ok(E.Cancel(S));
        G.Notify();
        map.Render();
        Assert.That(map.Find("[data-pt=map-depart]").HasAttribute("disabled"), Is.False, "配对：空闲时可出发");
    }
}
