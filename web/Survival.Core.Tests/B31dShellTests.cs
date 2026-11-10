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
