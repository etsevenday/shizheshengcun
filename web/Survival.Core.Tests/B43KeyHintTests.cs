using System.Text.RegularExpressions;
using Survival.Core.Engine;

namespace Survival.Core.Tests;

/// <summary>
/// B43 快捷键常驻显示并补齐顶栏绑定（需求 R170；S14 2026-11-12「快捷键在对应按钮常驻显示」，验收 KEYHINT-A01—A08；
/// 重墨 V2 逐字稿同日「对应按钮常驻快捷键」）。键位与键帽文字在内核 <see cref="ShellKeys"/>；网页壳接线（按钮面、按键路由）按源码核对。
/// </summary>
public class B43KeyHintTests : TestBase
{
    private static string Web(string relative)
    {
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "doc", "策划案"))) dir = dir.Parent;
        Assert.That(dir, Is.Not.Null, "找不到仓库根目录");
        return File.ReadAllText(Path.Combine(dir!.FullName, "web", "Survival.Web", relative));
    }

    // ───────────── 顶栏 12 键（KEYHINT-A01、A02） ─────────────

    /// <summary>A01：顶栏 12 个按钮的常驻原文逐字照 S14 表「常驻按钮原文」列，功能原顺序。</summary>
    [Test]
    public void A01_TopLabels_Verbatim()
    {
        var expect = new[] { "区域地图 M", "手工制作 C", "休息 R", "行动日志 L", "编队 P", "整备 G", "成长 K", "生存目标 J", "生存提示 T", "对话记录 D", "系统说明 H", "菜单 ESC" };
        Assert.That(ShellKeys.TopCaps.Count, Is.EqualTo(ShellText.TopKeys.Count));
        for (var i = 0; i < expect.Length; i++) Assert.That(ShellKeys.TopLabel(i), Is.EqualTo(expect[i]), $"第{i + 1}个");
        Assert.That(ShellKeys.Esc, Is.EqualTo("ESC"), "ESC 显示为「ESC」");
    }

    /// <summary>A02：字母不区分大小写、每个键只对应一个顶栏功能；F 不直达反馈；菜单走 Escape 层级、不是字母。</summary>
    [Test]
    public void A02_TopIndex_CaseInsensitiveAndUnique()
    {
        var letters = "MCRLPGKJTDH";
        for (var i = 0; i < letters.Length; i++)
        {
            Assert.That(ShellKeys.TopIndexOf(letters[i].ToString()), Is.EqualTo(i), letters[i].ToString());
            Assert.That(ShellKeys.TopIndexOf(char.ToLowerInvariant(letters[i]).ToString()), Is.EqualTo(i), "小写同效");
        }
        Assert.That(ShellKeys.TopCaps.Distinct().Count(), Is.EqualTo(ShellKeys.TopCaps.Count), "每个键只绑定一个顶栏功能");
        foreach (var k in new[] { "F", "f", "Q", "E", "1", "0", "ESC", "Escape", "", null, "Enter" })
            Assert.That(ShellKeys.TopIndexOf(k), Is.EqualTo(-1), k ?? "null");
    }

    /// <summary>A08：键表（游戏菜单、标题页玩法说明）与按钮同源，三处一致；不再有旧的「M 地图 · C 手工制作」四键表。</summary>
    [Test]
    public void A08_KeyTable_SameSourceAsButtons()
    {
        Assert.That(ShellKeys.TopTable(), Is.EqualTo("区域地图 M · 手工制作 C · 休息 R · 行动日志 L · 编队 P · 整备 G · 成长 K · 生存目标 J · 生存提示 T · 对话记录 D · 系统说明 H · 菜单 ESC"));
        var menu = Web("Components/MenuOverlay.razor");
        var start = Web("Components/StartScreen.razor");
        Assert.That(menu, Does.Contain("快捷键：@ShellKeys.TopTable()"));
        Assert.That(start, Does.Contain("快捷键：@ShellKeys.TopTable()"));
        Assert.That(menu + start, Does.Not.Contain("M 地图"));
    }

    /// <summary>A01、A08：顶栏 12 个按钮的面都是「功能名＋键帽」（TopFace），点击与按键共用 ActivateTop；悬停提示带同一键位；键帽三档都不隐藏。</summary>
    [Test]
    public void A01_TopBar_Wiring()
    {
        var screen = Web("Components/GameScreen.razor");
        var nav = screen.Substring(screen.IndexOf("<nav class=\"nav\""), screen.IndexOf("</nav>") - screen.IndexOf("<nav class=\"nav\""));
        for (var i = 0; i < 12; i++) Assert.That(nav, Does.Contain($">@TopFace({i})</button>"), $"第{i + 1}个按钮面");
        for (var i = 0; i < 11; i++) Assert.That(nav, Does.Contain($"@onclick=\"() => ActivateTop({i})\""), $"第{i + 1}个按钮点击");
        Assert.That(Regex.Matches(nav, "aria-keyshortcuts=").Count, Is.EqualTo(12));
        Assert.That(screen, Does.Contain("<span class=\"nk-name\">@ShellText.TopKeys[i]</span> <kbd class=\"kc\">@ShellKeys.TopCaps[i]</kbd>"));
        Assert.That(screen, Does.Contain("$\"{what}（{ShellKeys.TopCaps[i]}）\""), "悬停提示与键帽同源");
        var onKey = screen.Substring(screen.IndexOf("public void OnKey(string key)"));
        onKey = onKey.Substring(0, onKey.IndexOf("/// <summary>"));
        Assert.That(onKey, Does.Contain("var top = ShellKeys.TopIndexOf(key);").And.Contain("if (top >= 0) ActivateTop(top);"));
        Assert.That(onKey.IndexOf("if (G.DialogueLocked || G.MenuOpen || G.BattleMenuOpen) return;"), Is.LessThan(onKey.IndexOf("ActivateTop(top)")), "对白、菜单时不越权");
        Assert.That(onKey.IndexOf("if (G.InBattleUi) return;"), Is.LessThan(onKey.IndexOf("ActivateTop(top)")), "战斗与敌踪时不越权");
        Assert.That(onKey.IndexOf("if (G.S.Progress.Dead) return;"), Is.LessThan(onKey.IndexOf("ActivateTop(top)")), "终局不越权");
        var act = screen.Substring(screen.IndexOf("private void ActivateTop(int i)"));
        Assert.That(act, Does.Contain("case 4: ToggleFormation(); break;"), "编队沿原编队流程");
        Assert.That(Web("wwwroot/css/app.css"), Does.Not.Contain(".nav kbd { display: none; }"), "1280 档不再藏键位");
        Assert.That(Web("wwwroot/css/shell.css"), Does.Contain(".wasteland .top .nav kbd { display: inline-block; }"));
    }

    // ───────────── 按键路由（KEYHINT-A02、A05） ─────────────

    /// <summary>A02、A05：顶栏字母表含 11 个字母、不含 F；输入法组合、组合键、输入控件不触发；按住重复不重复开关；Enter/Space 重复在捕获阶段挡住。</summary>
    [Test]
    public void A05_KeyRouting()
    {
        var js = Web("wwwroot/js/app.js");
        Assert.That(js, Does.Contain("TOP_LETTERS: ['M', 'C', 'R', 'L', 'P', 'G', 'K', 'J', 'T', 'D', 'H'],"));
        Assert.That(js, Does.Not.Contain("'f', 'F'"));
        Assert.That(js, Does.Contain("if (ev.isComposing || ev.keyCode === 229) return;"), "输入法组合");
        Assert.That(js, Does.Contain("var mod = ev.ctrlKey || ev.altKey || ev.metaKey;"));
        Assert.That(js, Does.Contain("if (!ev.repeat) dotnet.invokeMethodAsync('OnKey', up);"), "按住不重复开关顶栏");
        Assert.That(js, Does.Contain("window.addEventListener('keydown', window.__survivalKeyGuard, true);"), "重复 Enter／Space 捕获阶段挡住");
        Assert.That(js, Does.Contain("if (tg === 'TEXTAREA' || tg === 'SELECT' || el.isContentEditable) return true;"), "下拉与可编辑文字区算输入控件");
        var hook = js.Substring(js.IndexOf("window.__survivalKeys = function (ev)"));
        Assert.That(hook.IndexOf("if (editable(tgt))"), Is.LessThan(hook.IndexOf("TOP.indexOf(up)")), "输入控件先于字母导航");
        Assert.That(hook.IndexOf("if (modal) {"), Is.LessThan(hook.IndexOf("TOP.indexOf(up)")), "模态层先处理自己的键");
        Assert.That(hook.IndexOf("document.querySelector('.menu-modal')"), Is.LessThan(hook.IndexOf("document.querySelector('.dlg-layer')")), "菜单先于对白");
        Assert.That(hook.IndexOf("document.querySelector('.dlg-layer')"), Is.LessThan(hook.IndexOf("document.querySelector('.bfs')")), "对白输入锁先于战斗");
    }
}
