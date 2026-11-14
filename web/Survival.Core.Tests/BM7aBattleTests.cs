using Survival.Core.Engine;

namespace Survival.Core.Tests;

/// <summary>
/// BM7a（R234：S14 文首「2026-11-14 Q629—Q636」§Q632／Q633／Q634 与验收 Q632-A01／A02、Q633-A01／A02、Q634-A01；R03 文首 Q629—Q636 节；逐字稿同批表；02 Q632—Q634）：
/// 敌人旁战斗信息浮框与右侧敌人属性、敌我出手不可跳过、调整站位不合法号位不可选。网页壳表现按源码核对（同 BM6 壳测试）。
/// </summary>
public class BM7aBattleTests
{
    // ───── Q633：敌我出手不可跳过 ─────

    /// <summary>Q633-A01：无「跳到结果」「跳过余下」按钮、键帽与两级跳过逻辑；画面点击、Enter／Space 被播放层接住、什么也不做。</summary>
    [Test]
    public void Q633_A01_NoSkipControlsOrLogic()
    {
        var layer = Web("Components", "BattleFxLayer.razor");
        Assert.That(layer, Does.Not.Contain("PlaybackSkip").And.Not.Contain("bfx-skip").And.Not.Contain("EnterSpaceCap").And.Not.Contain("@onclick"), "播放层不收跳过点击，也没有跳过按钮");
        var p = Web("Services", "GameSession.Playback.cs");
        Assert.That(p, Does.Not.Contain("FxAtResult").And.Not.Contain("_fxSkipAll").And.Not.Contain("SkipClosingHold"));
        Assert.That(p, Does.Contain("if (!FxPaused) elapsed += now - last;"), "每段按原时长自然播完，只有菜单／说明暂停计时");
        var g = Web("Components", "GameScreen.razor");
        Assert.That(g, Does.Contain("if (key == \"Skip\") return;"), "Enter／Space（'Skip'）对特写什么也不做");
        Assert.That(g, Does.Contain("if (G.BattleClosingHold) { if (key == \"Escape\") G.ClosingHoldEscape(); return; }"), "收束停留也不可跳过");
        Assert.That(g, Does.Contain("<div class=\"bfx-hold\" data-active></div>"), "停留层接住点击但不跳过");
        Assert.That(g, Does.Contain("if (key == \"Skip\") G.SkipEncounterPrompt();"), "非战斗出手的入战短提示（Q605）仍可跳过，不误删");
        var css = Web("wwwroot", "css", "battle.css");
        Assert.That(css, Does.Not.Contain(".bfx-skip {").And.Not.Contain(".at-result"));
        // 键表与帮助：撤下两级跳过行与常量，帮助补正式句
        Assert.That(ShellKeyHelp.Groups().SelectMany(x => x.Rows).Select(r => r.Label), Has.None.Contain("跳到结果").And.None.Contain("跳过余下"));
        Assert.That(ShellKeys.BattleNoSkipHelp, Is.EqualTo("敌我出手表现均不可跳过。播放期间可按ESC打开菜单，关闭菜单后从原处继续。"));
        var help = Web("Services", "HelpCatalog.cs");
        Assert.That(help, Does.Contain("ShellKeys.BattleNoSkipHelp,"));
        Assert.That(help, Does.Contain("跳过动画只是加快显示，不改变任何结果"), "中央行动「跳过动画」帮助不动");
        var cp = Web("Components", "CentralProgress.razor");
        Assert.That(cp, Does.Contain("跳过动画"), "中央行动条跳过按钮保留");
    }

    /// <summary>Q633-A01／A02：播放中及结束帧上的同一次输入不提交指令；ESC 仍开菜单并冻结，关闭原处续；读档清旧表现。</summary>
    [Test]
    public void Q633_A02_NoClickThrough_EscMenuPause_LoadClears()
    {
        var gs = Web("Services", "GameSession.cs");
        var exec = Slice(gs, "public async Task ExecuteBattleCommand(", "var cmd = CurrentBattleCommand()");
        Assert.That(exec, Does.Contain("if (PlaybackActive || FxJustEnded) return;"), "结束帧上的点击不点穿成提交");
        Assert.That(gs, Does.Contain("if (PlaybackActive || FxJustEnded) return;   // B34b（Q572）：播放结束的同一次 Enter 不接着确认指令"));
        Assert.That(gs, Does.Contain("private Task StepPause() => Task.Delay(EnemyStepDelayMs);"));
        var p = Web("Services", "GameSession.Playback.cs");
        Assert.That(p, Does.Contain("public bool FxPaused => MenuOpen || BattleMenuOpen || BattleDrawer == \"help\";"));
        Assert.That(Slice(p, "private void ResetPlayback()", "}"), Does.Contain("_fxGen++;").And.Contain("FxNow = null;"));
        // 收束停留期间的 ESC：停留自然结束后在真实情境开菜单（不缩短停留）
        var hold = Slice(p, "private async Task RunClosingHold(int gen)", "private void EndClosingHold()");
        Assert.That(hold, Does.Contain("if (esc && State != null) { if (InBattleUi) ToggleBattleMenu(true); else if (!MenuOpen) SetMenu(true); }"));
        var keys = Web("Services", "GameSession.Keys.cs");
        Assert.That(keys, Does.Contain("if (PlaybackActive && !FxUnderReading) return EscOwner.OpenMenu;"), "播放中 ESC 开当前情境菜单");
        var js = Web("wwwroot", "js", "app.js");
        Assert.That(js, Does.Contain("if (k === 'Enter' || k === ' ') { ev.preventDefault(); if (!ev.repeat) dotnet.invokeMethodAsync('OnKey', 'Skip'); return; }"), "播放层接住 Enter／Space，不落到背后");
    }

    internal static string Slice(string text, string from, string to)
    {
        var a = text.IndexOf(from, StringComparison.Ordinal);
        Assert.That(a, Is.GreaterThanOrEqualTo(0), from);
        var b = text.IndexOf(to, a + from.Length, StringComparison.Ordinal);
        Assert.That(b, Is.GreaterThan(a), to);
        return text[a..b];
    }

    internal static string Root()
    {
        var d = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (d != null && !Directory.Exists(Path.Combine(d.FullName, "web", "Survival.Web"))) d = d.Parent;
        return d!.FullName;
    }

    internal static string Web(params string[] parts) => File.ReadAllText(Path.Combine(new[] { Root(), "web", "Survival.Web" }.Concat(parts).ToArray())).Replace("\r\n", "\n");
}
