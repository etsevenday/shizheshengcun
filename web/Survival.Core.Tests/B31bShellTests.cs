using System.Text.RegularExpressions;
using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v1.0 B31b：外壳几何·顶栏·左右栏·中央行动位·菜单（R51—R55、R124 外壳页，Q319 Q320 Q322 Q323 Q325 Q328 Q329 Q331）。
/// 规则出处：《重墨 V2 开工裁定与界面逐字稿》§1.1、§2 通用位置表与 P01a、P01f、PXa、PXb、PXe、PXk、PXm、PXn、PXo、P05g。
/// 判定逻辑在内核 <see cref="ShellText"/>、<see cref="ShellEsc"/>、<see cref="ShellMenu"/>、<see cref="GameEngine.CurrentPlaceSummary"/>；壳层接线以源码检查。
/// </summary>
public class B31bShellTests : TestBase
{
    // ───────────── 逐字 ─────────────

    /// <summary>R51、Q292、Q322：顶栏 12 键一排、从左到右照 §2，不含「反馈」。</summary>
    [Test]
    public void R51_TopKeys_Verbatim()
    {
        Assert.That(ShellText.TopKeys, Is.EqualTo(new[] { "区域地图", "手工制作", "休息", "行动日志", "编队", "整备", "成长", "生存目标", "生存提示", "对话记录", "系统说明", "菜单" }));
        Assert.That(ShellText.TopKeys, Does.Not.Contain("反馈"));
    }

    [Test]
    public void Verbatim_ShellTexts()
    {
        Assert.That(ShellText.LeftTitle, Is.EqualTo("生存状况"));
        Assert.That(ShellText.ObjectTab, Is.EqualTo("对象"));
        Assert.That(ShellText.CharacterTab, Is.EqualTo("人物"));
        Assert.That(ShellText.ObjectTitle, Is.EqualTo("对象详情"));
        Assert.That(ShellText.ObjectEmptyHint, Is.EqualTo("选择对象查看详情。"));
        Assert.That(ShellText.PlaceTitle, Is.EqualTo("当前位置"));
        Assert.That(ShellText.BodyTitle, Is.EqualTo("身体状态"));
        Assert.That(ShellText.GameMenu, Is.EqualTo("游戏菜单"));
        Assert.That(ShellText.Feedback, Is.EqualTo("反馈与留言"));
        Assert.That(ShellText.ContinueGame, Is.EqualTo("继续游戏"));
        Assert.That(ShellText.SaveToTitle, Is.EqualTo("保存并返回标题"));
        Assert.That(ShellText.SaveNote, Is.EqualTo("存档保存在当前浏览器。"));
        Assert.That(ShellText.BattleMenu, Is.EqualTo("战斗菜单"));
        Assert.That(ShellText.BackToBattle, Is.EqualTo("返回战斗"));
        Assert.That(ShellText.OverwriteNote, Is.EqualTo("已有存档。开始新游戏会覆盖它，无法恢复。"));
        Assert.That(ShellText.OverwriteConfirm, Is.EqualTo("覆盖并开始"));
        Assert.That(ShellText.ActionRunning, Is.EqualTo("行动进行中"));
        Assert.That(ShellText.ActionPaused, Is.EqualTo("行动暂停中"));
        Assert.That(ShellText.ResumeAction, Is.EqualTo("继续行动"));
        Assert.That(ShellText.ExitAction, Is.EqualTo("退出行动"));
        Assert.That(ShellText.LeftStatName(StatId.Wakefulness), Is.EqualTo("清醒"), "§2 左栏写「清醒」");
        Assert.That(ShellText.LeftStatName(StatId.Satiety), Is.EqualTo("饱食"));
    }

    // ───────────── Q322 Esc 分层与菜单入口 ─────────────

    private static ShellEscRoute Esc(bool dlg = false, bool battle = false, bool menu = false, bool bmenu = false, bool layer = false, bool fb = false, bool dead = false) =>
        ShellEsc.Route(new ShellEscInput { DialogueLocked = dlg, BattleUi = battle, MenuOpen = menu, BattleMenuOpen = bmenu, HasClosableLayer = layer, FeedbackOpen = fb, Dead = dead });

    /// <summary>Q322-A01：战斗、战斗对白、普通对白、行动推进和暂停中 Esc 都打开相应菜单；对白与战斗同在按战斗情境。</summary>
    [Test]
    public void Q322_A01_EscOpensMenuEverywhere()
    {
        Assert.That(Esc(), Is.EqualTo(ShellEscRoute.OpenMenu), "常态、行动推进、暂停（中央行动位不是可关闭子层）");
        Assert.That(Esc(dlg: true), Is.EqualTo(ShellEscRoute.OpenMenu), "普通对白：对白本身不由 Esc 结束，直接开菜单");
        Assert.That(Esc(dlg: true, layer: true), Is.EqualTo(ShellEscRoute.OpenMenu), "对白锁住的背后子层不收");
        Assert.That(Esc(battle: true), Is.EqualTo(ShellEscRoute.OpenBattleMenu), "战斗／敌踪");
        Assert.That(Esc(battle: true, dlg: true), Is.EqualTo(ShellEscRoute.OpenBattleMenu), "战斗对白按战斗情境");
    }

    /// <summary>Q322：先收最上方可关闭子层；菜单开着时 Esc 收菜单（回原场景）；反馈窗开着由窗口自己处理，关掉后回到菜单。</summary>
    [Test]
    public void Q322_EscLayering()
    {
        Assert.That(Esc(layer: true), Is.EqualTo(ShellEscRoute.CloseLayer));
        Assert.That(Esc(battle: true, layer: true), Is.EqualTo(ShellEscRoute.CloseLayer));
        Assert.That(Esc(menu: true, layer: true), Is.EqualTo(ShellEscRoute.CloseMenu), "菜单在最上方");
        Assert.That(Esc(menu: true, dlg: true), Is.EqualTo(ShellEscRoute.CloseMenu));
        Assert.That(Esc(bmenu: true, battle: true, dlg: true), Is.EqualTo(ShellEscRoute.CloseBattleMenu));
        Assert.That(Esc(menu: true, fb: true), Is.EqualTo(ShellEscRoute.Ignore), "反馈关闭后回到游戏菜单，这一下不收菜单");
        Assert.That(Esc(dead: true), Is.EqualTo(ShellEscRoute.Ignore));
    }

    /// <summary>B31a 遗留「战斗／敌踪 Esc 有时要按两次」：看不见的残留选中不算子层。</summary>
    [Test]
    public void BattleEsc_HiddenSelectionIsNotALayer()
    {
        Assert.That(ShellEsc.BattleSelectionVisible(fullscreenBattle: true, selectionOnShelves: true), Is.False, "全屏战斗看不到外层选中");
        Assert.That(ShellEsc.BattleSelectionVisible(false, false), Is.False, "敌踪时①②被替换，地点、资源、设施选中看不见");
        Assert.That(ShellEsc.BattleSelectionVisible(false, true), Is.True, "敌踪时③④物品卡选中看得见");
    }

    /// <summary>Q322「菜单内」：关闭、阅读／动画偏好与反馈任何时候可用；对白锁期间其余入口禁用。</summary>
    [Test]
    public void Q322_MenuItemsUnderDialogueLock()
    {
        foreach (var it in new[] { ShellMenuItem.Continue, ShellMenuItem.ReadingFont, ShellMenuItem.AnimationSpeed, ShellMenuItem.Feedback })
            Assert.That(ShellMenu.Enabled(it, dialogueLocked: true), Is.True, it.ToString());
        foreach (var it in new[] { ShellMenuItem.SaveToTitle, ShellMenuItem.Restart, ShellMenuItem.AbortExpedition, ShellMenuItem.Help, ShellMenuItem.Dev })
        {
            Assert.That(ShellMenu.Enabled(it, dialogueLocked: true), Is.False, it.ToString());
            Assert.That(ShellMenu.Enabled(it, dialogueLocked: false), Is.True, it.ToString());
        }
    }

    // ───────────── Q328 当前位置摘要 ─────────────

    /// <summary>Q328-A01：正常地块——地点、遮蔽、探索度；无安全屋时省略区域行。</summary>
    [Test]
    public void Q328_A01_PlainLocation()
    {
        var s = New();
        var lines = E.CurrentPlaceSummary(s);
        Assert.That(lines[0], Is.EqualTo($"地点：{E.LocName(s.CurrentLocation)}"));
        Assert.That(lines.Any(l => l.StartsWith("区域：")), Is.False);
        Assert.That(lines, Does.Contain(E.IsSheltered(s, s.Ground) ? "遮蔽：有遮蔽" : "遮蔽：露天"));
        Assert.That(lines.Last(), Is.EqualTo($"探索度：{s.Locations[s.CurrentLocation].Progress}%"));
        Assert.That(lines.Count, Is.EqualTo(3));
    }

    /// <summary>Q328-A01：已建屋外写「区域：屋外」，屋内写「区域：屋内 · {安全屋名}」且有遮蔽。</summary>
    [Test]
    public void Q328_A01_SafehouseOutsideAndInside()
    {
        var s = New();
        E.DebugSafehouseSite(s);
        E.DebugBuildSafehouse(s);
        Assert.That(E.SafehouseBuilt(s, s.CurrentLocation), Is.True);
        var outside = E.CurrentPlaceSummary(s);
        Assert.That(outside[1], Is.EqualTo("区域：屋外"));
        Assert.That(E.EnterSafehouse(s).Ok, Is.True);
        var inside = E.CurrentPlaceSummary(s);
        Assert.That(inside[1], Is.EqualTo($"区域：屋内 · {E.SafehouseName(s, s.CurrentLocation)}"));
        Assert.That(inside, Does.Contain("遮蔽：有遮蔽"));
    }

    /// <summary>Q328：移动途中读当前实际地点（出发地），不跟随目的地。</summary>
    [Test]
    public void Q328_TravelReadsActualLocation()
    {
        var s = New();
        var from = s.CurrentLocation;
        Start(s, ActionRequest.Travel("field"));
        E.Advance(s);
        Assert.That(s.CurrentLocation, Is.EqualTo(from), "途中");
        Assert.That(E.CurrentPlaceSummary(s)[0], Is.EqualTo($"地点：{E.LocName(from)}"));
    }

    // ───────────── Q320 中央行动位 ─────────────

    /// <summary>Q320-A01：固定时长「已推进{已耗时} / {计划时长}」「剩余{时长}」；工作量型「已推进」与「预计剩余」分开，不拼计划总时长。</summary>
    [Test]
    public void Q320_A01_ProgressLines()
    {
        var fixedInfo = new ActionProgressInfo { ElapsedMinutes = 15, PlannedMinutes = 30, RemainingMinutes = 15 };
        Assert.That(ShellText.ProgressLines(fixedInfo), Is.EqualTo(("已推进15分钟 / 30分钟", (string?)"剩余15分钟")));
        var work = new ActionProgressInfo { ElapsedMinutes = 20, RemainingMinutes = 70, Estimated = true };
        Assert.That(ShellText.ProgressLines(work), Is.EqualTo(("已推进20分钟", (string?)"预计剩余1小时10分")));
        var fuel = new ActionProgressInfo { ElapsedMinutes = 5, Estimated = true, StopNote = "尚需至少45有效分钟，当前燃料不足，耗尽时停止" };
        Assert.That(ShellText.ProgressLines(fuel).Remain, Is.EqualTo(fuel.StopNote), "无可用估算时剩余行让位给实际原因");
    }

    /// <summary>Q320：固定时长行动带计划总时长，工作量型没有。</summary>
    [Test]
    public void Q320_PlannedMinutesOnlyForFixed()
    {
        var s = New();
        Start(s, ActionRequest.Rest(60));
        Assert.That(E.ActionProgress(s)!.PlannedMinutes, Is.EqualTo(60));
        var t = New();
        Start(t, ActionRequest.Travel("field"));
        Assert.That(E.ActionProgress(t)!.PlannedMinutes, Is.Null, "移动按工作量预计");
    }

    // ───────────── 壳层接线（源码检查） ─────────────

    /// <summary>Q322：F 不直达；顶栏、标题页、结算页没有独立反馈入口；反馈只在游戏菜单（常态与战斗）。</summary>
    [Test]
    public void Q322_FeedbackOnlyInMenu()
    {
        var js = Web("wwwroot", "js", "app.js");
        Assert.That(js, Does.Not.Contain("'f', 'F'"), "快捷键列表不含 F");
        Assert.That(js, Does.Not.Contain("OnKey', 'F'"));
        var screen = Web("Components", "GameScreen.razor");
        Assert.That(screen, Does.Not.Contain("feedback-btn").And.Not.Contain("OpenFeedback").And.Not.Contain("key == \"F\""));
        Assert.That(Web("Components", "StartScreen.razor"), Does.Not.Contain("openFeedback").And.Not.Contain("反馈问题").And.Not.Contain("tagline"));
        Assert.That(Web("Components", "EndScreens.razor"), Does.Not.Contain("openFeedback"));
        var menu = Web("Components", "MenuOverlay.razor");
        Assert.That(Regex.Matches(menu, "ShellText.Feedback").Count, Is.EqualTo(2), "常态与战斗菜单各一个「反馈与留言」");
        Assert.That(menu, Does.Not.Contain("F 反馈").And.Not.Contain("（F）"));
        Assert.That(Web("Components", "StartScreen.razor"), Does.Not.Contain("F 反馈"));
    }

    /// <summary>Q322：对白锁期间 Esc 交给游戏（打开菜单），不再被吞掉；菜单打开时对白层不可操作（菜单不消费对白继续键）。</summary>
    [Test]
    public void Q322_DialogueEscWiring()
    {
        var js = Web("wwwroot", "js", "app.js");
        Assert.That(js, Does.Contain("dotnet.invokeMethodAsync('OnKey', 'Escape')"));
        var screen = Web("Components", "GameScreen.razor");
        Assert.That(screen, Does.Contain("ShellEsc.Route("));
        Assert.That(Web("Components", "DialogueLayer.razor"), Does.Contain("G.MenuOpen || G.BattleMenuOpen"));
    }

    /// <summary>PXa：标题页只留标题、新游戏、继续游戏、玩法说明与阅读字体。</summary>
    [Test]
    public void PXa_TitleScreen()
    {
        var t = Web("Components", "StartScreen.razor");
        Assert.That(t, Does.Contain("ShellText.ContinueGame").And.Contain("ShellText.NewGame").And.Contain("ShellText.HowToPlay"));
        Assert.That(t, Does.Contain("ShellText.OverwriteNote").And.Contain("ShellText.OverwriteConfirm"));
        Assert.That(t, Does.Contain("<PresentationSettings FontOnly=\"true\" />"));
    }

    /// <summary>Q325：外壳三档几何令牌（1600 基准、1280 ×0.8、1920 ×1.2 取整），顶栏双行 12 键一排。</summary>
    [Test]
    public void Q325_ShellGeometryTiers()
    {
        var css = Web("wwwroot", "css", "shell.css");
        foreach (var v in new[] { "--sh-left: 192px", "--sh-right: 262px", "--sh-side: 24px", "--sh-foot: 38px",
                                  "--sh-left: 154px", "--sh-right: 210px", "--sh-side: 19px", "--sh-foot: 30px",
                                  "--sh-left: 230px", "--sh-right: 314px", "--sh-side: 29px", "--sh-foot: 46px",
                                  "--pop-w: 760px", "--pop-w: 608px", "--pop-w: 912px" })
            Assert.That(css, Does.Contain(v), v);
        Assert.That(css, Does.Contain("grid-template-columns: repeat(12, minmax(0, 1fr))"), "顶栏 12 键一排");
        Assert.That(Web("wwwroot", "index.html"), Does.Contain("css/shell.css"));
    }

    /// <summary>R53：左栏一块「生存状况」，左栏属性名、负重、随身物品；第几日在顶栏不重复；状态标记在条前（不和名称抢行）。</summary>
    [Test]
    public void R53_LeftPanel()
    {
        var hud = Web("Components", "Hud.razor");
        Assert.That(hud, Does.Contain("ShellText.LeftTitle").And.Contain("ShellText.LeftStatName(id)").And.Contain("ShellText.Weight").And.Contain("ShellText.Carried"));
        Assert.That(hud, Does.Not.Contain("存活第").And.Not.Contain("携带重量"));
        Assert.That(hud, Does.Contain("<div class=\"vital-bar\">@if (body.Infected) {<em class=\"flag danger-text\">已感染</em>}"));
        Assert.That(Web("wwwroot", "css", "shell.css"), Does.Contain(".sl .sl-vitals { flex: 1 1 auto; min-height: 0; overflow-y: auto;"), "属性区单独滚动，负重块固定在底部");
    }

    /// <summary>R54、Q328、Q329：右栏页签「对象」「人物」、区标题「对象详情」、空态当前位置摘要；属性子页标题「身体状态」。</summary>
    [Test]
    public void R54_RightPanel()
    {
        var d = Web("Components", "DetailPanel.razor");
        Assert.That(d, Does.Contain("ShellText.ObjectTab").And.Contain("ShellText.CharacterTab").And.Contain("ShellText.ObjectTitle"));
        Assert.That(d, Does.Contain("e.CurrentPlaceSummary(s)").And.Contain("ShellText.PlaceTitle").And.Contain("ShellText.ObjectEmptyHint"));
        Assert.That(d, Does.Not.Contain("未选中对象").And.Not.Contain(">对象信息<"));
        var a = Web("Components", "AttrDetail.razor");
        Assert.That(Regex.Matches(a, "ShellText.BodyTitle").Count, Is.EqualTo(3), "基础属性、免疫、病毒三类子页");
        Assert.That(a, Does.Contain("病毒 · 未感染"));
    }

    /// <summary>R55、PXn／PXe（Q320）：中央行动位逐字（「行动进行中」「行动暂停中」「暂停原因」「继续行动」「退出行动」）、进度两行取内核；
    /// 暂停层为中部遮罩上的居中弹窗；开着一级面板时两者收成中部底部窄条（不盖面板，原反馈仍在暂停条下）。</summary>
    [Test]
    public void R55_CentralActionPosition()
    {
        var cp = Web("Components", "CentralProgress.razor");
        Assert.That(cp, Does.Contain("ShellText.ProgressLines(p)").And.Contain("ShellText.ActionRunning").And.Contain("ShellText.ActionPaused")
            .And.Contain("ShellText.PauseReason").And.Contain("ShellText.ResumeAction").And.Contain("ShellText.ExitAction"));
        Assert.That(cp, Does.Not.Contain(">退出</button>").And.Not.Contain(">继续</button>").And.Not.Contain("\"进行中\""));
        var pause = Web("Components", "PauseLayer.razor");
        Assert.That(pause, Does.Contain("<div class=\"pause-scrim\">").And.Contain("<div class=\"pause-dock\""));
        Assert.That(pause, Does.Contain("@if (G.Notice != null) { <div class=\"notice\">@G.Notice</div> }"), "收成窄条时 Q318 忙碌句仍在暂停条下");
        var screen = Web("Components", "GameScreen.razor");
        Assert.That(screen, Does.Contain("var dockProgress = centerPanel && G.Panel != \"citymap\";"));
        Assert.That(screen, Does.Contain("<PauseLayer Docked=\"@dockProgress\" />").And.Contain("<CentralProgress Info=\"progress\" Docked=\"@dockProgress\" />"));
        var css = Web("wwwroot", "css", "shell.css");
        Assert.That(css, Does.Contain(".pause-scrim {").And.Contain(".wasteland .cprog.float.docked, .pause-dock {"));
    }

    /// <summary>P01a 地点角标「当前位置」（各档都显示，不再用样式补“· 当前”）；§2 底栏「已保存」不带圆点字符；Q331 移动中品牌行「起点 → 终点」。</summary>
    [Test]
    public void P01a_CornerFooterAndTravelBrand()
    {
        Assert.That(Web("Components", "LocationCard.razor"), Does.Contain("<em>@ShellText.PlaceTitle</em>").And.Not.Contain("<em>当前地点</em>"));
        Assert.That(Web("wwwroot", "css", "shell.css"), Does.Contain(".mc-chip.name::after { content: none; }"));
        var screen = Web("Components", "GameScreen.razor");
        Assert.That(screen, Does.Contain("<span class=\"saved\">@ShellText.Saved</span>").And.Not.Contain("● 已保存"));
        Assert.That(screen, Does.Contain("main?.Type == ActionType.Travel ? $\" → {e.LocName(main.TargetId)}\""));
    }

    private static string Root()
    {
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "web", "Survival.sln"))) dir = dir.Parent;
        Assert.That(dir, Is.Not.Null, "找到仓库根目录");
        return dir!.FullName;
    }

    private static string Web(params string[] parts) => File.ReadAllText(Path.Combine(new[] { Root(), "web", "Survival.Web" }.Concat(parts).ToArray()));
}
