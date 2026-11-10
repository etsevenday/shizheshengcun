using System.Text.RegularExpressions;
using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v1.0 B31d：答复 Q332—Q338 外壳收尾（R132）。
/// 规则出处：《重墨 V2 开工裁定与界面逐字稿》§1.2「Q332–Q338 · 外壳收尾」与「B31 收尾与 B32 验收补充」Q332-A01、Q333-A01、Q334-A01、Q336-A01、Q337-A01、Q338-A01；
/// 02 待确认 Q332 选 A、Q333 选 A、Q334 选 B、Q336 选 B、Q337 选 C、Q338 选 B。
/// 判定在内核（ShellText、ShellEsc、ShellMenu、ResultToast）的直接测；网页壳的接线以源码检查（同 B31b、B31c）。
/// </summary>
public class B31dShellTests : TestBase
{
    // ───────────── Q332 缺字 ─────────────

    /// <summary>Q332-A01：战斗装备、物资只读提示以正式文字开头、去掉锁形；出错提示条关闭键写「×」，可访问名称「关闭」；全部界面源码不再有这两个缺字。</summary>
    [Test]
    public void Q332_A01_NoMissingGlyphs()
    {
        Assert.That(Web("Components", "BattleEquip.razor"), Does.Contain("<div class=\"bsup-lock\">战斗中只读："));
        Assert.That(Web("Components", "BattleSupplies.razor"), Does.Contain("<div class=\"bsup-lock\">战斗中只读："));
        var html = Web("wwwroot", "index.html");
        Assert.That(html, Does.Contain("<a class=\"dismiss\" role=\"button\" aria-label=\"关闭\">×</a>"));
        foreach (var f in Directory.EnumerateFiles(Path.Combine(Root(), "web", "Survival.Web"), "*.*", SearchOption.AllDirectories)
                     .Where(p => !p.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar) && !p.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar))
                     .Where(p => p.EndsWith(".razor") || p.EndsWith(".cs") || p.EndsWith(".js") || p.EndsWith(".html") || p.EndsWith(".css")))
        {
            var t = File.ReadAllText(f);
            Assert.That(t, Does.Not.Contain("\U0001F512").And.Not.Contain("\U0001F5D9"), f);
        }
    }

    // ───────────── Q334 敌踪菜单标题 ─────────────

    /// <summary>Q334-A01：敌踪尚未接战时菜单标题（含可访问名称）为「游戏菜单」，实际战斗仍为「战斗菜单」；旧字「菜单 · 敌踪选择中」去掉。</summary>
    [Test]
    public void Q334_A01_EncounterMenuTitle()
    {
        Assert.That(ShellText.BattleUiMenuTitle(false), Is.EqualTo("游戏菜单"));
        Assert.That(ShellText.BattleUiMenuTitle(true), Is.EqualTo("战斗菜单"));
        var menu = Web("Components", "MenuOverlay.razor");
        Assert.That(menu, Does.Not.Contain("菜单 · 敌踪选择中"));
        Assert.That(menu, Does.Contain("var menuTitle = ShellText.BattleUiMenuTitle(inBattle);"));
        Assert.That(menu, Does.Contain("aria-label=\"@menuTitle\"").And.Contain("<h2>@menuTitle</h2>"));
    }

    // ───────────── Q333 死亡结算页菜单 ─────────────

    private static ShellEscRoute Esc(bool dead = true, bool menu = false, bool layer = false, bool fb = false, bool battle = false, bool dlg = false) =>
        ShellEsc.Route(new ShellEscInput { Dead = dead, MenuOpen = menu, HasClosableLayer = layer, FeedbackOpen = fb, BattleUi = battle, DialogueLocked = dlg });

    /// <summary>
    /// Q333-A01：死亡结算页 Esc 与「菜单」键同效——打开游戏菜单，已开时收起回到同一结算；「查看记录」层开着时先收它；反馈窗开着由窗口自己处理。
    /// 战斗中死亡也开同一结算菜单（不开战斗菜单）。菜单只开放关闭、阅读字体、动画速度与「反馈与留言」。
    /// </summary>
    [Test]
    public void Q333_A01_DeathMenu()
    {
        Assert.That(Esc(), Is.EqualTo(ShellEscRoute.OpenMenu));
        Assert.That(Esc(battle: true, dlg: true), Is.EqualTo(ShellEscRoute.OpenMenu), "战斗或对白中死亡也开结算菜单");
        Assert.That(Esc(menu: true), Is.EqualTo(ShellEscRoute.CloseMenu), "关闭回到同一结算");
        Assert.That(Esc(layer: true), Is.EqualTo(ShellEscRoute.CloseLayer), "先收「查看记录」");
        Assert.That(Esc(menu: true, fb: true), Is.EqualTo(ShellEscRoute.Ignore), "反馈窗自己处理 Esc，关掉回到菜单");
        foreach (ShellMenuItem item in Enum.GetValues(typeof(ShellMenuItem)))
        {
            var open = item is ShellMenuItem.Continue or ShellMenuItem.ReadingFont or ShellMenuItem.AnimationSpeed or ShellMenuItem.Feedback;
            Assert.That(ShellMenu.Enabled(item, true), Is.EqualTo(open), item.ToString());
        }
        Assert.That(ShellText.Menu, Is.EqualTo("菜单"));
        Assert.That(ShellText.TopKeys[11], Is.EqualTo(ShellText.Menu));
    }

    /// <summary>Q333 壳层接线：结算页「菜单」键与 Esc 开同一菜单（盖在结算层之上），菜单内其余入口按「对白锁或已死亡」禁用；关闭后焦点回结算页「菜单」键。</summary>
    [Test]
    public void Q333_ShellWiring()
    {
        var end = Web("Components", "EndScreens.razor");
        Assert.That(end, Does.Contain("@onclick=\"() => G.SetMenu(true)\">@ShellText.Menu</button>"));
        Assert.That(end, Does.Contain("inert=\"@(G.MenuOpen || G.DeathLogOpen)\""), "菜单开着时结算层不可操作");
        Assert.That(end, Does.Contain("await _menuBtn.FocusAsync();"));
        Assert.That(end, Does.Not.Contain("openFeedback"), "反馈仍只从菜单进入（Q322）");
        var screen = Web("Components", "GameScreen.razor");
        Assert.That(screen, Does.Contain("@if (G.MenuOpen && G.S.Progress.Dead)"));
        Assert.That(screen, Does.Contain("<div class=\"wasteland end-menu\"><MenuOverlay /></div>"));
        Assert.That(screen, Does.Contain("@if (G.MenuOpen && !battleUi && !s.Progress.Dead) { <MenuOverlay /> }"));
        Assert.That(screen, Does.Contain("HasClosableLayer = dead ? G.DeathLogOpen :"));
        var onKey = screen.Substring(screen.IndexOf("public void OnKey(string key)"));
        Assert.That(onKey.IndexOf("if (key == \"Escape\") { Escape(); return; }"), Is.LessThan(onKey.IndexOf("if (G.S.Progress.Dead) return;")), "死亡时只收 Esc");
        var menu = Web("Components", "MenuOverlay.razor");
        Assert.That(menu, Does.Contain("var locked = G.DialogueLocked || dead;"));
        Assert.That(Block(Web("wwwroot", "css", "shell.css"), ".wasteland.end-menu {"), Does.Contain("z-index: 86"), "高于结算层 z 60");
    }

    // ───────────── Q336 版本行 ─────────────

    /// <summary>Q336-A01：普通游戏菜单种子行下显示「原型 · {实际构建} · 存档版本{当前存档格式}」与浏览器存档说明；战斗层菜单不显示；标题页不再有。</summary>
    [Test]
    public void Q336_A01_VersionLineInMenu()
    {
        Assert.That(ShellText.VersionLine("2026-11-10 · abc1234", GameState.CurrentSchema), Is.EqualTo($"原型 · 2026-11-10 · abc1234 · 存档版本{GameState.CurrentSchema}"));
        Assert.That(ShellText.VersionLine("本地开发版", "44"), Is.EqualTo("原型 · 本地开发版 · 存档版本44"));
        Assert.That(ShellText.VersionLine("", "44"), Is.EqualTo("原型 · 存档版本44"), "取不到构建时省略中段");
        Assert.That(ShellText.VersionLine(null, "44"), Is.EqualTo("原型 · 存档版本44"));
        var menu = Web("Components", "MenuOverlay.razor");
        var seed = menu.IndexOf("<div><span>种子</span><b>@G.S.Seed</b></div>");
        var line = menu.IndexOf("@ShellText.VersionLine(G.AppBuild, GameState.CurrentSchema)");
        var note = menu.IndexOf("@ShellText.SaveNote");
        Assert.That(seed, Is.GreaterThan(menu.IndexOf("return;")), "种子行在普通游戏菜单（战斗层菜单 return 之后）");
        Assert.That(seed, Is.LessThan(line));
        Assert.That(line, Is.LessThan(note), "版本行、存档说明紧接在种子行下");
        Assert.That(Regex.Matches(menu, "VersionLine").Count, Is.EqualTo(1));
        Assert.That(Web("wwwroot", "js", "app.js"), Does.Contain("document.querySelector('meta[name=\"app-build\"]')"));
        Assert.That(Web("Components", "GameScreen.razor"), Does.Contain("G.AppBuild = await JS.InvokeAsync<string>(\"survival.appBuild\");"));
        Assert.That(Web("wwwroot", "index.html"), Does.Contain("<meta name=\"app-build\" content=\"本地开发版\" />"), "发布流程据此替换为实际构建");
        Assert.That(Web("Components", "StartScreen.razor"), Does.Not.Contain("存档版本"), "标题页仍按 PXa 不显示");
    }

    // ───────────── 工具 ─────────────

    private static string Block(string css, string head)
    {
        var i = css.IndexOf(head, StringComparison.Ordinal);
        Assert.That(i, Is.GreaterThanOrEqualTo(0), head);
        var j = css.IndexOf('}', i);
        return css.Substring(i, j - i);
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
