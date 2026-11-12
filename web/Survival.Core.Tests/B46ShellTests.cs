using Survival.Core.Engine;

namespace Survival.Core.Tests;

/// <summary>
/// B46 网页壳（S14／逐字稿「B41c2 Q531—Q534」「B41c2 Q535—Q536」同批节）：单段 5 分钟行动的中央条展示与跳过、暂停条信息顺序、
/// 旧接敌／固定战悬停与真实拒绝同源。源码检查（界面排版另由试玩复验）。
/// </summary>
public class B46ShellTests
{
    private static string Root()
    {
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "doc", "策划案"))) dir = dir.Parent;
        Assert.That(dir, Is.Not.Null, "找不到仓库根目录");
        return dir!.FullName;
    }

    private static string Web(string relative) => File.ReadAllText(Path.Combine(Root(), "web", "Survival.Web", relative)).Replace("\r\n", "\n");

    private static string Between(string text, string from, string to)
    {
        var i = text.IndexOf(from, StringComparison.Ordinal);
        Assert.That(i, Is.GreaterThanOrEqualTo(0), from);
        var j = text.IndexOf(to, i + from.Length, StringComparison.Ordinal);
        Assert.That(j, Is.GreaterThan(i), to);
        return text.Substring(i, j - i);
    }

    /// <summary>Q535-A01／A03：开工即时提交后，第一段之前先让中央条真实可见一段表现等待（600／1200／1800），再进原推进循环。</summary>
    [Test]
    public void Q535_A01_A03_RunLoop_ShowsCentralBarBeforeTheOnlySegment()
    {
        var session = Web("Services/GameSession.cs");
        var loop = Between(session, "private async Task RunLoop()", "finally");
        var show = loop.IndexOf("if (State != null && E.SingleSegmentShowPending(S)) await ShowSingleSegment();", StringComparison.Ordinal);
        var advance = loop.IndexOf("var r = E.Advance(S);", StringComparison.Ordinal);
        Assert.That(show, Is.GreaterThan(0).And.LessThan(advance), "先展示，再提交唯一真实段");
        var body = Between(session, "private async Task ShowSingleSegment()", "/// <summary>");
        Assert.That(body, Does.Contain("Notify();").And.Contain("await Task.Delay(1);"), "先渲染、这一帧画出来以后才计时");
        Assert.That(body, Does.Contain("var total = SingleSegmentShowMs;"));
        Assert.That(body, Does.Not.Contain("E.Advance"), "展示期间不推进世界、不结算");
        Assert.That(session, Does.Contain("public int SingleSegmentShowMs => Presentation.SingleSegmentShowMs;"));
    }

    /// <summary>Q535-A04：点击「跳过动画」或合法空格只缩短表现等待（同一个 _skip），未聚焦 Enter 不新增跳过；运行中中央条有跳过、锁定时无退出。</summary>
    [Test]
    public void Q535_A04_SkipShortensShowOnly_SpaceSameButton_NoEnter()
    {
        var session = Web("Services/GameSession.cs");
        var body = Between(session, "private async Task ShowSingleSegment()", "/// <summary>");
        Assert.That(body, Does.Contain("while (!_skip &&"), "跳过只结束表现等待，随后原段照常提交");
        var cp = Web("Components/CentralProgress.razor");
        Assert.That(cp, Does.Contain("else if (G.Running && !dlg)").And.Contain("data-skip-anim aria-keyshortcuts=\"Space\""));
        Assert.That(cp, Does.Contain("@ShellKeys.SkipAnim <kbd class=\"kc\">@ShellKeys.SpaceCap</kbd>"));
        Assert.That(ShellKeys.SkipAnimTitle, Is.EqualTo("只跳过表现，不改变游戏时间与结果"));
        var js = Web("wwwroot/js/app.js");
        Assert.That(js, Does.Contain("var sk = document.querySelector('.cprog:not(.fading) [data-skip-anim]');"));
        Assert.That(js, Does.Not.Contain("[data-skip-anim]').click(); // Enter"));
        Assert.That(cp, Does.Contain("<span class=\"cp-exit-note cp-noexit\">@GameEngine.NoExitNote</span>"), "锁定：不可退出说明，无退出按钮");
    }

    /// <summary>Q533-A01：原暂停原因 → 下一行复核原因（警示色）→ 永久失效再下一行中止说明；完整弹窗与窄条都逐行，继续按钮原位；文字直接在 DOM 里，不靠悬停。</summary>
    [Test]
    public void Q533_A01_PauseBarOrder_LinesInDockToo_NoHoverOnly()
    {
        var cp = Web("Components/CentralProgress.razor");
        var reason = cp.IndexOf("<div class=\"cp-reason\">@ShellText.PauseReason：@p.PauseText</div>", StringComparison.Ordinal);
        var note = cp.IndexOf("<span class=\"cp-resume-note\">@p.ResumeNote</span>", StringComparison.Ordinal);
        var abort = cp.IndexOf("<span class=\"cp-resume-abort\">@GameEngine.LockedAbortNote</span>", StringComparison.Ordinal);
        var go = cp.IndexOf("@ShellText.ResumeAction</button>", StringComparison.Ordinal);
        Assert.That(new[] { reason, note, abort, go }, Is.Ordered.And.All.GreaterThan(0));
        Assert.That(cp, Does.Contain("<div class=\"cp-note cp-resume warn-text\">"), "警示色");
        var css = Web("wwwroot/css/shell.css");
        Assert.That(css, Does.Contain(".cprog .cp-resume > span { display: block; }"));
        Assert.That(css, Does.Not.Contain(".pause-dock .cprog .cp-resume > span { display: inline; }"), "窄条也逐行，不接排");
        Assert.That(css, Does.Contain(".wasteland .pause-dock:has(.cp-resume) { max-height: none; }"), "不裁字");
        Assert.That(GameEngine.LockedAbortNote, Is.EqualTo("本次行动无法继续，点击「继续行动」后将自动中止。"));
    }

    /// <summary>Q534-A01：锁定主行动暂停时，旧接近／固定战／可选强敌按钮悬停取内核真实拒绝（有子行动为「当前行动还没结束。」）。</summary>
    [Test]
    public void Q534_A01_ApproachAndFixedHover_SameSourceAsRealRejection()
    {
        // B41e3：旧常驻敌人卡的「接近」、固定战卡、可选强敌卡已删，敌踪统一在居中操作盘与右栏敌踪详情里处理，禁用原因直接取内核 CanApproachCard
        // （有未结束子行动时就是「当前行动还没结束。」，内核断言见 B46Tests.Q534_A01_A02）
        var pops = Web("Components/ActionPops.razor");
        Assert.That(pops, Does.Contain("var whyApproach = e.CanApproachCard(s, key);"));
        Assert.That(Web("Components/EnemyDetail.razor"), Does.Contain("var why = e.CanApproachCard(s, card.Key);"));
        var strip = Web("Components/LocationStrip.razor");
        Assert.That(strip, Does.Not.Contain("e.CanEngageFixed(s)").And.Not.Contain("e.CanEngageElite(s, cid)").And.Not.Contain("e.CanApproach(s, wid)"));
        Assert.That(GameEngine.LockedSubRunning, Is.EqualTo("当前行动还没结束。"));
    }

    /// <summary>Q536-A01：继续行动收尾（等待加工全部完成）后不进推进循环、不走单段展示。</summary>
    [Test]
    public void Q536_A01_ShellResume_FinishedWaitDoesNotEnterLoop()
    {
        var session = Web("Services/GameSession.cs");
        var resume = Between(session, "public async Task Resume()", "public void SkipAnimation()");
        var guard = resume.IndexOf("if (res.Aborted || S.Main == null || S.Main.Status != ActionStatus.Running)", StringComparison.Ordinal);
        var loop = resume.IndexOf("await RunLoop();", StringComparison.Ordinal);
        Assert.That(guard, Is.GreaterThan(0).And.LessThan(loop));
    }

    /// <summary>B46 试玩修复 1（Q535-A01／A04）：单段展示开始时收起挡中央条的选卡操作盘与点火盘；展示期间空格按跳过处理，不落到卡上重开操作盘；淡出中的旧盘不挡空格。</summary>
    [Test]
    public void Fix1_SingleShow_CollapsesLocalDisk_SpaceSkipsDuringShow()
    {
        var session = Web("Services/GameSession.cs");
        var body = Between(session, "private async Task ShowSingleSegment()", "/// <summary>");
        Assert.That(body, Does.Contain("if (CardPopShown) CardPopCovered = true;").And.Contain("IgniteMenuOpen = false;").And.Contain("Pop = null;"));
        Assert.That(body, Does.Contain("SingleShowing = true;").And.Contain("finally { SingleShowing = false; }"));
        Assert.That(body.IndexOf("SingleShowing = true;", StringComparison.Ordinal), Is.LessThan(body.IndexOf("Notify();", StringComparison.Ordinal)), "先收盘再渲染");
        Assert.That(Web("Components/CentralProgress.razor"), Does.Contain("data-single-show=\"@(G.SingleShowing && !Fading ? \"1\" : null)\""));
        var js = Web("wwwroot/js/app.js");
        var early = js.IndexOf("var ss = document.querySelector('.cprog[data-single-show]:not(.fading) [data-skip-anim]');", StringComparison.Ordinal);
        var apopGuard = js.IndexOf("if (tgt && tgt.closest && tgt.closest('.apop') && k !== 'Escape' && !tgt.closest('.tbar')) return;", StringComparison.Ordinal);
        Assert.That(early, Is.GreaterThan(0).And.LessThan(apopGuard), "展示期间的空格在就近盘、焦点控件判断之前处理");
        Assert.That(js, Does.Contain("document.activeElement !== ss && !topModal()"));
        Assert.That(js, Does.Contain("return shown(el) && !(el.closest && el.closest('.pop-ghosts'));"));
    }

    /// <summary>B46 试玩修复 2：窄条不限高、压紧间距；休息面板在窄条让位后整块滚动、开始条贴底。</summary>
    [Test]
    public void Fix2_DockStripNotClipped_RestPanelScrolls()
    {
        var css = Web("wwwroot/css/shell.css");
        var fix = css.Substring(css.IndexOf("B46 试玩修复（线上构建 b6a1d4b）", StringComparison.Ordinal));
        Assert.That(fix, Does.Contain(".wasteland :is(.cprog.docked, .pause-dock) { max-height: none; }"));
        Assert.That(fix, Does.Contain(".center-scroll.four.docked-prog .panel-host .rest-panel { overflow-y: auto; overscroll-behavior: contain; }"));
        Assert.That(fix, Does.Contain(".center-scroll.four.docked-prog .panel-host .rest-panel > .rest-go { position: sticky; bottom: 0; }"));
    }

    /// <summary>B46 试玩修复 3：完整暂停弹窗每次打开都滚回顶部（标题与说明可见）。</summary>
    [Test]
    public void Fix3_PauseDialogOpensAtTop()
    {
        var pause = Web("Components/PauseLayer.razor");
        Assert.That(pause, Does.Contain("@inject IJSRuntime JS").And.Contain("await JS.InvokeVoidAsync(\"survival.pauseTop\")"));
        Assert.That(pause, Does.Contain("Docked ? null : $\"{G.S.Main.ActionId}|{G.S.Sub?.ActionId}\""));
        Assert.That(Web("wwwroot/js/app.js"), Does.Contain("var g = document.querySelector('.pause-scrim > .pause-grid'); if (g && g.scrollTop > 0) g.scrollTop = 0;"));
    }
}
