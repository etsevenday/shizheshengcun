using Survival.Core.Engine;

namespace Survival.Core.Tests;

/// <summary>
/// B41c2 网页壳与帮助（S14 Q506 锁定行动不放退出入口、Q514 继续收尾、Q523 原工具拾取入口、Q527 帮助两条与 ESC 段逐字）。
/// </summary>
public class B41c2ShellTests
{
    private static string Root()
    {
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "doc", "策划案"))) dir = dir.Parent;
        Assert.That(dir, Is.Not.Null, "找不到仓库根目录");
        return dir!.FullName;
    }

    private static string Web(string relative) => File.ReadAllText(Path.Combine(Root(), "web", "Survival.Web", relative)).Replace("\r\n", "\n");

    private static string S14() => File.ReadAllText(Directory.GetFiles(Path.Combine(Root(), "doc", "策划案"), "S14 主界面与全局UI交互系统.md", SearchOption.AllDirectories).Single()).Replace("\r\n", "\n");

    private static readonly string[] ProgressHelp =
    {
        "行动开始后，屏幕中央出现推进条：行动名、进度、剩余游戏时间；允许退出的行动另有“退出行动”。固定时长写“剩余”，会受倍率或燃料影响的写“预计剩余”。",
        "对允许退出的行动，点“退出行动”会在当前这一段结束时停下：已经过去的时间和已经发生的结果不退回，没做完的部分不结算；制作会把预留材料按原规则返还。按开工为5分钟锁定的行动不可中途退出，点火、食用、吞噬融合等开始就会做完。",
        "跳过动画只是加快显示，不改变任何结果；允许退出的行动在任何速度下都可以退出。",
        "等待期间身体消耗和普通等待一样。它不会自动点火、加木或补水，燃料不够时会写明；允许退出的等待加工可以从中央推进条的“退出行动”退出，已有的加工进度保留。按开工为5分钟锁定的等待加工不可中途退出；这次规则更新前已开始的等待加工仍保留原退出方式。",
    };

    [Test, Category("DesignSync")]
    public void Q527_A01_HelpParagraphsVerbatim()
    {
        var s14 = S14();
        var cat = Web("Services/HelpCatalog.cs");
        foreach (var p in ProgressHelp)
        {
            Assert.That(s14, Does.Contain($"> {p}"), p);
            Assert.That(cat, Does.Contain($"\"{p}\""), p);
        }
        Assert.That(cat, Does.Not.Contain("可以随时从中央推进条退出"));
        Assert.That(cat, Does.Not.Contain("不改变任何结果，任何速度下都可以退出。"), "旧句已换成「允许退出的行动在任何速度下都可以退出」");
        Assert.That(ShellKeyHelp.EscParagraphs[2], Does.Contain("允许退出的行动，请使用中央行动栏的“退出行动”；开工为5分钟的行动不可中途退出。"));
    }

    [Test]
    public void Q506_A02_Shell_NoExitControlWhenLocked_ResumeAbortHandled()
    {
        var cp = Web("Components/CentralProgress.razor");
        var locked = cp.Substring(cp.IndexOf("@if (p.Locked)"));
        locked = locked.Substring(0, locked.IndexOf("else"));
        Assert.That(locked, Does.Contain("GameEngine.NoExitNote").And.Not.Contain("cp-exit\""), "锁定时没有退出按钮，也不留灰色占位");
        Assert.That(cp, Does.Contain("GameEngine.LockedAbortNote").And.Contain("p.ResumeNote"));
        var pause = Web("Components/PauseLayer.razor");
        Assert.That(pause, Does.Contain("@if (!GameEngine.ExitLocked(main))"), "暂停层不写锁定行动的退出后果");
        var session = Web("Services/GameSession.cs");
        Assert.That(session, Does.Contain("if (res.Aborted || S.Main == null || S.Main.Status != ActionStatus.Running)"));
        Assert.That(session, Does.Not.Contain("ExitNoteAction"), "旧「本段完成后结束」延迟退出说明已移除");
        Assert.That(Web("Components/CardActions.razor"), Does.Contain("e.LockedToolPickupOpen(s, it)"));
        Assert.That(Web("Components/ItemShelf.razor"), Does.Contain("e.LockedToolPickupOpen(s, t)"));
        Assert.That(Web("Components/CityOverview.razor"), Does.Contain("est?.NoExit == true ? GameEngine.LockedTravelHover"));
    }

    /// <summary>B41c2 试玩修复 P2／P3／P5／P6：窄条不裁复核原因；原工具可拾取时遮罩让出③④；右栏原因不叠标点；接近悬停用锁定版原因。</summary>
    [Test]
    public void Fix_P2_P3_P5_P6_ShellLayoutAndTexts()
    {
        var css = Web("wwwroot/css/shell.css");
        Assert.That(css, Does.Contain(".wasteland .pause-dock:has(.cp-resume) { max-height: none; }"));
        Assert.That(css, Does.Contain(".pause-scrim.pick-open { bottom: calc(50% + var(--sh-gap) / 2); }"));
        Assert.That(Web("Components/CentralProgress.razor"), Does.Contain("<div class=\"cp-note cp-resume warn-text\">"));
        Assert.That(Web("Components/PauseLayer.razor"), Does.Contain("e.LockedToolPickupOpen(s, pickTool) ? \"pick-open\""));
        Assert.That(Web("Components/EnemyDetail.razor"), Does.Contain("@(lead.EndsWith(\"。\") ? \"\" : \"；\")ESC 不能关掉战斗"));
        Assert.That(Web("Components/LocationStrip.razor"), Does.Contain("GameEngine.LockedMainPaused(s) && s.Sub == null ? e.CanApproach(s, wid)"));
    }
}
