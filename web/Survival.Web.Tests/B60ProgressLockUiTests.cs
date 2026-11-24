using Bunit;
using Survival.Core.Engine;
using Survival.Core.State;
using Survival.Web.Components;
using Survival.Web.Services;

namespace Survival.Web.Tests;

/// <summary>
/// B60 网页壳行为：
/// <list type="bullet">
/// <item>R377（S01／S14 文首「2026-11-24 Q1038 主动行动统一居中显示进度」，02 Q1038 选 C；S14 验收 Q1038-01／Q1038-02）：开着火堆、地图等面板时
/// 中央主动行动进度（执行与暂停）一律居中浮窗，撤掉 B31b R55／Q324 的底部窄条；框外整个游戏界面遮罩、阻断输入，只有最上层合法控件可用；关闭后恢复。</item>
/// <item>R325（S11／S14「B51c2a2原批Q1028—Q1030」§Q1030，02 Q1030「按新裁定」；验收 B51C2A2-Q1030-01／02）：被进度弹框覆盖的火堆层不显示
/// 投入木材常驻禁用句和「全部取出」悬停原因；弹框退出后按届时状态恢复。</item>
/// </list>
/// 起点（打开的面板、开始／暂停的行动）用会话与内核入口造；之后断玩家看到的元素、属性与按键结果。遮罩的实际遮挡、模糊与挖洞（CSS／app.js）bUnit 看不到，靠试玩。
/// </summary>
public class B60ProgressLockUiTests : WebTestBase
{
    private void Ok(ActionResult r) => Assert.That(r.Ok, Is.True, r.Reason);

    /// <summary>开着火堆中央加工菜单（一级面板 "fire"）——Q1038-01 点名的「打开火堆…面板后进入原主动行动进度」。</summary>
    private void OpenFire()
    {
        G.OpenFireMenu();
        Assert.That(G.Panel, Is.EqualTo(GameSession.FireMenuPanel), "起点：火堆加工菜单开着");
    }

    private static bool Inert(AngleSharp.Dom.IElement el) => el.HasAttribute("inert");

    /// <summary>
    /// Q1038-01（执行中）：开着火堆面板时开始行动——进度是居中浮窗（cprog float，不带原窄条 docked），浮窗是 Tab 范围（data-prog-lock）；
    /// 整屏进度遮罩亮着（prog-focus on），白名单只留顶栏「菜单」与底栏存档按钮（沿 BM6 Q625）；背景（①②行、面板、左右栏、③④、就近浮窗宿主、顶栏 11 个功能键）inert，
    /// 顶栏「菜单」不 inert（S14 Q322 行动推进中可开菜单）。
    /// 配对（Q1038「退出浮窗或真实结束后恢复当前仍合法的上级面板与焦点」、B51C2A2-Q1030-02）：退出行动后遮罩撤下、背景不再 inert、火堆菜单仍开着。
    /// </summary>
    [Test]
    public void FirePanelOpen_RunningAction_CenteredFloatAndFullMask_ThenRestored()
    {
        OpenFire();
        Ok(E.Start(S, ActionRequest.Wait(30)));
        G.Notify();
        var cut = Ctx.RenderComponent<GameScreen>();

        var floats = cut.FindAll("[data-pt=cprog-float]");
        Assert.That(floats, Has.Count.EqualTo(1), "Q1038 C：只有一个居中浮窗");
        Assert.That(floats[0].ClassList, Does.Contain("float").And.Not.Contain("docked"), "撤掉 R55 底部窄条");
        Assert.That(floats[0].GetAttribute("data-prog-lock"), Is.EqualTo("1"), "浮窗是最上层输入范围");
        Assert.That(cut.FindAll(".pause-dock"), Is.Empty);
        var mask = cut.Find("[data-pt=prog-focus]");
        Assert.That(mask.ClassList, Does.Contain("on"));
        Assert.That(mask.GetAttribute("data-keep"), Is.EqualTo(".top [data-menu-key], .footer .save button"), "白名单：顶栏菜单、底栏存档处理按钮");

        Assert.That(Inert(cut.Find(".rows2-inner")), Is.True, "①②");
        Assert.That(Inert(cut.Find(".panel-host")), Is.True, "盖在四轨上的火堆面板");
        Assert.That(Inert(cut.Find("aside.sidebar")), Is.True, "左栏");
        Assert.That(Inert(cut.Find("aside[data-detail-rail]")), Is.True, "右栏");
        Assert.That(Inert(cut.Find("[data-pt=shelf-ground]")) && Inert(cut.Find("[data-pt=shelf-carried]")), Is.True, "③④");
        Assert.That(Inert(cut.Find(".apop-host")), Is.True, "就近浮窗宿主");
        var nav = cut.FindAll(".top .nav > button");
        Assert.That(nav, Has.Count.EqualTo(12), "顶栏 12 键（Q292）");
        Assert.That(nav.Count(b => Inert(b)), Is.EqualTo(11), "功能键 11 个都 inert");
        Assert.That(Inert(cut.Find(".top [data-menu-key]")), Is.False, "「菜单」保留（Q322）");

        // 配对：真实结束（退出行动）后恢复
        cut.Find("[data-pt=cprog-float] .cp-exit").Click();
        Assert.That(S.Main, Is.Null, "起点核对：退出行动已生效");
        cut.Render();
        Assert.That(cut.Find("[data-pt=prog-focus]").ClassList, Does.Not.Contain("on"), "遮罩撤下");
        Assert.That(cut.Find("[data-pt=prog-focus]").HasAttribute("data-keep"), Is.False);
        Assert.That(Inert(cut.Find(".panel-host")), Is.False, "面板恢复可操作");
        Assert.That(Inert(cut.Find("aside.sidebar")), Is.False);
        Assert.That(cut.FindAll(".top .nav > button").Count(b => Inert(b)), Is.EqualTo(0));
        Assert.That(G.Panel, Is.EqualTo(GameSession.FireMenuPanel), "上级面板仍是火堆菜单");
    }

    /// <summary>
    /// Q1038-01（暂停）：开着火堆面板时主行动暂停——「行动暂停中」是完整居中弹窗（pause-dialog，data-prog-lock），不再收成 pause-dock 窄条；
    /// 遮罩同样亮着、背景 inert（02 Q1038 答复「行动暂停浮窗同样锁背景，仅允许顶层合法控件」）；弹窗里的「继续行动」可用。
    /// </summary>
    [Test]
    public void FirePanelOpen_PausedAction_FullPauseDialogAndMask()
    {
        OpenFire();
        Ok(E.Start(S, ActionRequest.Wait(60)));
        S.Main!.Status = ActionStatus.Paused;   // 起点：主行动暂停（原规则下由属性偏低等触发，此处只造起点）
        G.Notify();
        // B60a：原「起点：不是 Q523／Q569 例外」断言随两种例外（PausePickOpen／PauseTravelOpen）撤掉而删（S14 B60 Q1100）
        var cut = Ctx.RenderComponent<GameScreen>();

        Assert.That(cut.FindAll(".pause-dock"), Is.Empty, "撤掉 R55 暂停窄条");
        var dlg = cut.Find("[data-pt=pause-dialog]");
        Assert.That(dlg.GetAttribute("data-prog-lock"), Is.EqualTo("1"));
        Assert.That(dlg.QuerySelector(".pg-top h2")!.TextContent, Is.EqualTo("等待1小时已暂停"), "完整弹窗的标题区「{行动名}已暂停」（B31b PXe；窄条里没有）");
        Assert.That(dlg.QuerySelector(".cp-go")!.HasAttribute("disabled"), Is.False, "顶层合法控件「继续行动」可用");
        Assert.That(cut.Find("[data-pt=prog-focus]").ClassList, Does.Contain("on"));
        Assert.That(Inert(cut.Find(".panel-host")), Is.True);
        Assert.That(Inert(cut.Find("aside[data-detail-rail]")), Is.True);
    }

    /// <summary>
    /// Q1038-01「Esc／业务快捷键只服从最上层」：火堆面板开着、行动进行中——Esc 不再收被遮住的面板，而是打开游戏菜单（S14 Q322）；顶栏字母 M（地图）不受理。
    /// 配对：行动结束后 Esc 照原逐层收面板（EscOwner.Panel），M 照常打开地图。
    /// </summary>
    [Test]
    public void FirePanelOpen_RunningAction_EscOpensMenu_TopLettersIgnored_ThenNormal()
    {
        OpenFire();
        Ok(E.Start(S, ActionRequest.Wait(30)));
        G.Notify();
        var cut = Ctx.RenderComponent<GameScreen>();
        Assert.That(G.EscOwner, Is.EqualTo(EscOwner.OpenMenu), "遮罩期间面板不承接 Esc");

        cut.InvokeAsync(() => cut.Instance.OnKey("M"));
        Assert.That(G.Panel, Is.EqualTo(GameSession.FireMenuPanel), "M 不打开地图");
        cut.InvokeAsync(() => cut.Instance.OnKey("Escape"));
        Assert.That(G.MenuOpen, Is.True, "Esc 打开游戏菜单");
        Assert.That(G.Panel, Is.EqualTo(GameSession.FireMenuPanel), "Esc 不关被遮住的火堆面板");
        G.SetMenu(false);

        E.Cancel(S);
        G.Notify();
        Assert.That(G.ProgressLock, Is.False);
        Assert.That(G.EscOwner, Is.EqualTo(EscOwner.Panel), "配对：没有进度浮窗时 Esc 收面板");
        cut.InvokeAsync(() => cut.Instance.OnKey("M"));
        Assert.That(G.Panel, Is.EqualTo("map"), "配对：M 打开地图");
    }

    /// <summary>
    /// B51C2A2-Q1030-01（S11 §Q1030「被弹框覆盖的火堆层不显示本题的投入木材常驻禁用句及全部取出悬停原因」）：行动进行中，火堆菜单主层不出现
    /// 「行动进行中，暂不能维护火堆」常驻句，「全部取出」照真实合法性置灰但没有悬停原因；不调换提示位置。
    /// 配对（B51C2A2-Q1030-02 退出后按实际状态恢复）：行动结束后没有木材时，同一位置照原规则常驻「随身和当前地面没有木材」，空火堆「全部取出」悬停「没有可取出的物品。」（Q879 A）。
    /// </summary>
    [Test]
    public void FireMenu_UnderProgressMask_NoWoodLineNoTakeAllHover_ThenRestored()
    {
        S.Items.RemoveAll(i => i.ItemId == "wood");
        OpenFire();
        Ok(E.Start(S, ActionRequest.Wait(30)));
        G.Notify();
        Assert.That(G.FireWoodWhy(out _), Is.EqualTo("行动进行中，暂不能维护火堆"), "起点核对：原规则下投入木材此刻的禁用原因");
        var cut = Ctx.RenderComponent<FireMenu>();

        Assert.That(cut.FindAll("[data-pt=fire-wood-why]"), Is.Empty, "Q1030：不显示投入木材常驻禁用句");
        Assert.That(cut.Find(".fm-quick").TextContent, Does.Not.Contain("行动进行中，暂不能维护火堆"), "按钮行里看不到这句（按钮自身悬停被遮罩挡住）");
        var all = cut.Find("[data-pt=fire-take-all]");
        Assert.That((all.HasAttribute("disabled"), all.HasAttribute("title")), Is.EqualTo((true, false)), "Q1030：置灰、无悬停原因");

        E.Cancel(S);
        G.Notify();
        cut.Render();
        var why = cut.FindAll("[data-pt=fire-wood-why]");
        Assert.That(why, Has.Count.EqualTo(1), "配对：退出后照原规则常驻");
        Assert.That(why[0].TextContent, Is.EqualTo("随身和当前地面没有木材"));
        all = cut.Find("[data-pt=fire-take-all]");
        Assert.That((all.HasAttribute("disabled"), all.GetAttribute("title")), Is.EqualTo((true, "没有可取出的物品。")), "配对：退出后悬停原因照 Q879 A");
    }

    /// <summary>
    /// Q1038 C（撤掉城市层右下角进度）：地图城市层开着时行动进行中，进度同样是唯一的居中浮窗（cprog float，data-prog-lock），遮罩亮着；城市层所在面板 inert。
    /// （右下角定位是 cityoverview.css 的旧规则，已删；位置本身靠试玩看。）
    /// </summary>
    [Test]
    public void CityLayerOpen_RunningAction_SameCenteredFloat()
    {
        // 起点同 B35cMapDepartTests.Registered：登记后到城内建筑，读完对白，地图默认层即本城城市层
        S.Progress.Flags.UnionWith(new[] { "registration_authorized", "hub_registered" });
        E.DebugMapTeleport(S, "h03_workshop");
        for (var k = 0; k < 128 && E.DialogueLocked(S); k++) E.AckDialogue(S, E.CurrentDialogueLine(S)!.LineId);
        G.OpenTopPanel("map");
        Assert.That(G.CityBrowsing, Is.True, "起点：城市层开着");
        Ok(E.Start(S, ActionRequest.Wait(30)));
        G.Notify();
        var cut = Ctx.RenderComponent<GameScreen>();

        Assert.That(cut.Find("section.center").ClassList, Does.Contain("city-open"));
        var floats = cut.FindAll("[data-pt=cprog-float]");
        Assert.That(floats, Has.Count.EqualTo(1));
        Assert.That(floats[0].GetAttribute("data-prog-lock"), Is.EqualTo("1"));
        Assert.That(cut.Find("[data-pt=prog-focus]").ClassList, Does.Contain("on"));
        Assert.That(Inert(cut.Find(".panel-host")), Is.True);
    }

    /// <summary>
    /// Q1038（「从开始到关闭或结束」只服从最上层）＋S14 §Q1030 D1／D2「已开始的连续转移不得继续提交」：经会话真实开始行动时，
    /// 层级本会高过遮罩的临时局部层收起（固定的状态说明浮层、就近操作盘），网页随即收到 progLockOnset（取消未提交拖拽、收起背景焦点）；结束后收到 progLockEnd。
    /// 配对：不开始行动时状态浮层保持固定。
    /// </summary>
    [Test]
    public async Task StartAction_CollapsesFloatingLayers_AndSignalsLockOnsetAndEnd()
    {
        var cut = Ctx.RenderComponent<GameScreen>();
        G.PinStatus("*all", 10, 10);
        Assert.That(G.StatusPinned, Is.EqualTo("*all"), "配对：未开始行动时浮层固定着");

        var run = G.StartAction(ActionRequest.Wait(30));
        Assert.That(G.StatusPinned, Is.Null, "开始推进即收起状态说明浮层");
        await cut.InvokeAsync(() => cut.Render());
        Assert.That(Ctx.JSInterop.Invocations.Select(i => i.Identifier), Does.Contain("survival.progLockOnset"));
        await run;
        await cut.InvokeAsync(() => cut.Render());
        Assert.That(S.Main, Is.Null, "起点核对：行动已真实结束");
        Assert.That(Ctx.JSInterop.Invocations.Select(i => i.Identifier), Does.Contain("survival.progLockEnd"));
    }
}
