using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v1.0 B31c：统一操作模型·局部盘居中·公共动效·结果提示（R56、R114，R131 中 Q324、Q330，B31b 外壳遗留）。
/// 规则出处：《重墨 V2 开工裁定与界面逐字稿》§1.1 Q324、Q330 与 B31 验收补充 Q324-A01、Q330-A01；a9b93f0「给开发的动效说明」其他操作的过渡与结果。
/// 结果提示判定在内核 <see cref="ResultToast"/>；局部盘居中、动效与外壳排版在网页壳，以源码检查。
/// </summary>
public class B31cShellTests : TestBase
{
    private static GameEvent Ev(GameEventKind k, string text = "x") => new GameEvent { Kind = k, Text = text };

    // ───────────── Q330 结果提示 ─────────────

    /// <summary>Q330：关键提示只有「危险」与「行动中止」两类。</summary>
    [Test]
    public void Q330_CriticalKinds()
    {
        Assert.That(ResultToast.IsCritical(GameEventKind.Hazard), Is.True);
        Assert.That(ResultToast.IsCritical(GameEventKind.ActionAborted), Is.True);
        foreach (var k in new[] { GameEventKind.ActionCompleted, GameEventKind.ActionCancelled, GameEventKind.Death, GameEventKind.Encounter,
                     GameEventKind.BattleEnd, GameEventKind.Notice, GameEventKind.FacilityDone, GameEventKind.Info })
            Assert.That(ResultToast.IsCritical(k), Is.False, k.ToString());
        Assert.That(ResultToast.IsCritical((GameEvent?)null), Is.False);
    }

    /// <summary>Q330-A01：危险、行动中止不自动收起（超过 8 秒仍在）；一般提示约 8 秒收起。</summary>
    [Test]
    public void Q330_A01_CriticalStaysNormalDismisses()
    {
        Assert.That(ResultToast.NormalDismissMs, Is.EqualTo(8000));
        Assert.That(ResultToast.AutoDismiss(Ev(GameEventKind.Hazard)), Is.False);
        Assert.That(ResultToast.AutoDismiss(Ev(GameEventKind.ActionAborted)), Is.False);
        Assert.That(ResultToast.AutoDismiss(Ev(GameEventKind.ActionCompleted)), Is.True);
        Assert.That(ResultToast.AutoDismiss(Ev(GameEventKind.Notice)), Is.True);
        Assert.That(ResultToast.AutoDismiss(null), Is.False);
    }

    /// <summary>Q330：自动到达的一般提示不顶替尚未处理的关键提示；连发关键提示时显示最新一条。</summary>
    [Test]
    public void Q330_PickKeepsCriticalShowsLatestCritical()
    {
        var hazard = Ev(GameEventKind.Hazard, "危险1");
        Assert.That(ResultToast.Pick(hazard, new[] { Ev(GameEventKind.ActionCompleted), Ev(GameEventKind.FacilityDone), Ev(GameEventKind.Notice) }), Is.SameAs(hazard),
            "一般提示（完成、设施完成、短通知）不顶替关键提示");
        var aborted = Ev(GameEventKind.ActionAborted, "中止");
        Assert.That(ResultToast.Pick(hazard, new[] { aborted }), Is.SameAs(aborted), "新的关键提示顶替旧的");
        var hazard2 = Ev(GameEventKind.Hazard, "危险2");
        Assert.That(ResultToast.Pick(null, new[] { hazard, Ev(GameEventKind.ActionCompleted), hazard2, Ev(GameEventKind.Notice) }), Is.SameAs(hazard2),
            "同一批里也是最新关键结果，后到的一般提示不顶替");
        var done = Ev(GameEventKind.ActionCompleted);
        Assert.That(ResultToast.Pick(done, new[] { hazard }), Is.SameAs(hazard), "关键提示顶替一般提示");
    }

    /// <summary>原 Drain 档次规则不变：行动结果类同批后到顶替先到；短通知、设施完成只在同批没有行动结果时显示，可顶替上一批的一般提示。</summary>
    [Test]
    public void Q330_PickKeepsOriginalRanks()
    {
        var done = Ev(GameEventKind.ActionCompleted, "完成");
        var notice = Ev(GameEventKind.Notice, "来客");
        Assert.That(ResultToast.Pick(null, new[] { done, notice }), Is.SameAs(done), "同批行动结果优先");
        Assert.That(ResultToast.Pick(null, new[] { notice, done }), Is.SameAs(done));
        var fac = Ev(GameEventKind.FacilityDone, "烤好了");
        Assert.That(ResultToast.Pick(done, new[] { fac }), Is.SameAs(fac), "上一批的一般提示可被新一批设施完成顶替");
        var cancelled = Ev(GameEventKind.ActionCancelled, "取消");
        Assert.That(ResultToast.Pick(done, new[] { cancelled }), Is.SameAs(cancelled));
        Assert.That(ResultToast.Pick(done, new[] { Ev(GameEventKind.Info), Ev(GameEventKind.Battle), Ev(GameEventKind.NewDay) }), Is.SameAs(done), "不进提示的事件不改");
        Assert.That(ResultToast.Pick(done, new[] { Ev(GameEventKind.Hazard, "") }), Is.SameAs(done), "空文字事件跳过");
        Assert.That(ResultToast.Pick(null, Array.Empty<GameEvent>()), Is.Null);
    }

    /// <summary>Q330-A01：只读浏览不清关键提示（一般提示照原规则收起）。</summary>
    [Test]
    public void Q330_A01_BrowseKeepsCritical()
    {
        var hazard = Ev(GameEventKind.Hazard);
        Assert.That(ResultToast.OnBrowse(hazard), Is.SameAs(hazard));
        Assert.That(ResultToast.OnBrowse(Ev(GameEventKind.ActionAborted)), Is.Not.Null);
        Assert.That(ResultToast.OnBrowse(Ev(GameEventKind.ActionCompleted)), Is.Null);
        Assert.That(ResultToast.OnBrowse(null), Is.Null);
    }

    /// <summary>Q330-A01：有效提交才换标记；预览、被拒请求、重复请求不换（不清提示）。</summary>
    [Test]
    public void Q330_A01_CommitMark()
    {
        var s = New();
        var m0 = ResultToast.CommitMark(s);
        E.Preview(s, ActionRequest.Rest(30));
        Assert.That(ResultToast.CommitMark(s), Is.EqualTo(m0), "预览不算提交");
        var bad = E.Start(s, ActionRequest.UseInstance(987654).WithRequestId("b31c-bad"));
        Assert.That(bad.Ok, Is.False);
        Assert.That(ResultToast.CommitMark(s), Is.EqualTo(m0), "被拒请求不算提交");
        Assert.That(ResultToast.Committed(m0, ResultToast.CommitMark(s)), Is.False);
        var ok = E.Start(s, ActionRequest.Rest(30).WithRequestId("b31c-ok"));
        Assert.That(ok.Ok, Is.True, ok.Reason);
        var m1 = ResultToast.CommitMark(s);
        Assert.That(ResultToast.Committed(m0, m1), Is.True, "有效提交换标记");
        E.Start(s, ActionRequest.Rest(30).WithRequestId("b31c-ok"));
        Assert.That(ResultToast.CommitMark(s), Is.EqualTo(m1), "同一请求号重发不算新提交");
        Assert.That(ResultToast.Committed(null, m1), Is.False, "首次记标记（新局、读档）不清");
    }

    /// <summary>Q330 壳层接线：Drain 走 ResultToast.Pick；有效提交在 Drain 与 Notify 前比较标记；选中只按 OnBrowse；被拒接敌不清；关键提示不自动收起。</summary>
    [Test]
    public void Q330_ShellWiring()
    {
        var g = Web("Services", "GameSession.cs");
        Assert.That(g, Does.Contain("SetToast(ResultToast.Pick(Toast, batch));"));
        Assert.That(g, Does.Contain("if (sel.Kind != SelectionKind.None) SetToast(ResultToast.OnBrowse(Toast));"));
        Assert.That(g, Does.Contain("if (res.Ok) Toast = null;"), "接敌被拒不清提示");
        Assert.That(g, Does.Contain("if (ResultToast.AutoDismiss(t)) _ = AutoDismissToast(_toastSeq);"));
        var notify = g.Substring(g.IndexOf("public void Notify()"));
        Assert.That(notify.Substring(0, notify.IndexOf("Version++")), Does.Contain("SyncCommitMark();"));
        var drain = g.Substring(g.IndexOf("private void Drain()"));
        drain = drain.Substring(0, drain.IndexOf("E.DrainEvents(S)"));
        Assert.That(drain, Does.Contain("SyncCommitMark();"), "先清旧提示再放本次结果");
        Assert.That(g, Does.Not.Contain("if (sel.Kind != SelectionKind.None) Toast = null;"));
        Assert.That(g, Does.Contain("requestId ??= Guid.NewGuid().ToString(\"N\");"), "操作条整批拾放不带请求号时补一个，成功也算有效提交");
        var screen = Web("Components", "GameScreen.razor");
        Assert.That(screen, Does.Contain("var bad = ResultToast.IsCritical(G.Toast);"));
        Assert.That(screen, Does.Contain("@(G.ToastLeaving ? \"leaving\" : \"\")"));
        Assert.That(screen, Does.Contain("<button aria-label=\"关闭结果提示\" @onclick=\"G.DismissToast\">×</button>"), "关闭键沿原文字");
    }

    // ───────────── Q324 局部操作盘居中 ─────────────

    /// <summary>Q324-A01：所有 AnchorPop 局部盘（选卡操作、移动、接近、投炭、采集、锁定路线、点火工具、食饮）默认居中，位置由 survivalPop 按 data-center 计算，距屏边 ≥8。</summary>
    [Test]
    public void Q324_A01_AnchorPopsCentered()
    {
        var pop = Web("Components", "AnchorPop.razor");
        Assert.That(pop, Does.Contain("[Parameter] public bool Centered { get; set; } = true;"));
        Assert.That(pop, Does.Contain("data-center=\"@(Centered ? \"1\" : null)\""));
        Assert.That(pop, Does.Contain("@(Centered ? \"apop-c\" : \"\")"));
        foreach (var f in new[] { "ActionPops.razor", "CardPop.razor", "FeedPop.razor", "IgniteMenu.razor" })
            Assert.That(Web("Components", f), Does.Not.Contain("Centered=\"false\""), f);
        // M621（试玩 B1）：原断言“居中前先做来源失效判定、按量到的尺寸算 left/top”改为：先由 CSS 居中（清掉 left/top），再做来源失效判定
        var js = Web("wwwroot", "js", "app.js");
        var center = js.IndexOf("var centered = !!el.getAttribute('data-center');");
        Assert.That(center, Is.GreaterThan(0));
        var branch = js.Substring(center, js.IndexOf("var a = cur.anchor", center) - center);
        Assert.That(branch, Does.Contain("el.style.left = ''; el.style.top = '';").And.Contain("el.style.visibility = 'visible';"));
        Assert.That(center, Is.LessThan(js.IndexOf("if (!a || A.vis < 0.25)")), "先居中，再做来源失效判定（收起前不偏位）");
        Assert.That(js.IndexOf("if (!a || A.vis < 0.25)"), Is.LessThan(js.IndexOf("if (centered) return;")), "滚出或消失仍照原规则收起");
        Assert.That(Block(Web("wwwroot", "css", "shell.css"), ".wasteland .apop.apop-c {"), Does.Contain("inset: 0; margin: auto;"));
    }

    /// <summary>M621 试玩修复：食饮盘主动作固定盘底；休息时长并排、开始栏不压卡；只开局部盘时窄条停②底；居中确认框底栏统一。</summary>
    [Test]
    public void M621_PlaytestFixes()
    {
        var feed = Web("Components", "FeedPop.razor");
        var actions = feed.Substring(feed.IndexOf("<Actions>"));
        Assert.That(actions, Does.Contain("data-feed-confirm").And.Contain("data-feed-help").And.Contain("data-feed-cancel").And.Contain("FeedPanelCopy.SectionConfirm"));
        Assert.That(feed.Substring(0, feed.IndexOf("<Actions>")), Does.Not.Contain("data-feed-confirm"));
        Assert.That(Web("Components", "RestOverlay.razor"), Does.Contain("class=\"sub-panel rest-panel\""));
        var css = Web("wwwroot", "css", "shell.css");
        Assert.That(css, Does.Contain(".panel-host .rest-panel > .rest-scroll { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr));"));
        Assert.That(css, Does.Contain(".center-scroll.four.docked-prog:not(.locked) ~ :is(.cprog.docked, .pause-dock) { bottom: calc(50% + var(--sh-gap) / 2); }"));
        Assert.That(css, Does.Contain(".overflow-layer > .overflow-box > .ov-foot { order: 1; }"));
        Assert.That(css, Does.Contain(".overflow-layer > .overflow-box > .ov-foot > .secondary { order: -1; flex: 0 1 190px;"));
        Assert.That(css, Does.Contain(".overflow-layer > .overflow-box > .ov-foot > .primary { flex: 0 1 238px;"));
    }

    /// <summary>Q324：居中框尺寸用 B31b 三档居中弹窗令牌（608×438／760×547／912×656），放不下时限制在视口内留 8；标题与动作固定、正文独立滚动。</summary>
    [Test]
    public void Q324_PopSizeAndScroll()
    {
        var css = Web("wwwroot", "css", "shell.css");
        Assert.That(css, Does.Contain("--pop-w: 760px;").And.Contain("--pop-h: 547px;"));
        Assert.That(css, Does.Contain("--pop-w: 608px; --pop-h: 438px;").And.Contain("--pop-w: 912px; --pop-h: 656px;"));
        var rule = Block(css, ".wasteland .apop.apop-c {");
        Assert.That(rule, Does.Contain("width: min(var(--pop-w), calc(100vw - 16px)); height: min(var(--pop-h), calc(100vh - 16px));"));
        Assert.That(Block(css, ".wasteland .apop.apop-c > .apop-body {"), Does.Contain("overflow-y: auto"));
        Assert.That(Block(css, ".wasteland .apop.apop-c > .apop-foot {"), Does.Contain("flex: 0 0 auto"));
        Assert.That(Block(css, ".overflow-layer > .overflow-box {"), Does.Contain("width: min(var(--pop-w), calc(100vw - 16px)); height: min(var(--pop-h), calc(100vh - 16px));"),
            "超额、封存、重置、撤出、同类武器同一居中框");
        Assert.That(Block(css, ".modal-back > .modal {"), Does.Contain("width: min(var(--pop-w), calc(100vw - 16px));"), "转移数量框同一居中框");
    }

    /// <summary>Q324：居中局部盘开着时中央行动位收成底部窄条，不被盖住「退出行动」；整备、制作页内工作区不走 AnchorPop（保持原位）。</summary>
    [Test]
    public void Q324_ProgressDocksUnderLocalDisk()
    {
        var g = Web("Services", "GameSession.cs");
        Assert.That(g, Does.Contain("public bool LocalDiskOpen => State != null && !InBattleUi && (Pop != null || FeedPending != null || IgniteMenuOpen || CardPopShown);"));
        // B32d（Q390）：菜单期间 CardPop 不卸载、只隐藏（menu-hidden），显示条件 CardPopShown 仍排除菜单期间
        Assert.That(Web("Components", "CardPop.razor"), Does.Contain("@if (G.State != null && G.Pop == null && G.FeedPending == null && !G.IgniteMenuOpen && !G.DialogueLocked && !G.OverflowPending && !G.DuplicateWeaponsPending"));
        Assert.That(Web("Components", "CardPop.razor"), Does.Contain("var menuHide = G.MenuOpen || G.BattleMenuOpen ? \" menu-hidden\" : \"\";"));
        Assert.That(g, Does.Contain("public bool CardPopShown => State != null && Pop == null && FeedPending == null && !IgniteMenuOpen && !DialogueLocked && !OverflowPending && !DuplicateWeaponsPending"),
            "CardPop 显示条件与 CardPopShown 一致");
        Assert.That(Web("Components", "GameScreen.razor"), Does.Contain("if (!centerPanel && G.LocalDiskOpen) dockProgress = true;"));
        Assert.That(Web("Components", "GearOverlay.razor"), Does.Not.Contain("<AnchorPop"), "整备页内工作区保持原位置");
        Assert.That(Web("Components", "CraftOverlay.razor"), Does.Not.Contain("<AnchorPop"), "制作页内工作区保持原位置");
    }

    // ───────────── R114 公共动效 ─────────────

    /// <summary>R114：弹层入场 200、退场 140、遮罩 200、抽屉 190、悬停焦点 140、提示 180 毫秒，全部乘动画速度倍率；减少动态效果时不生成退场影子。</summary>
    [Test]
    public void R114_MotionTimings()
    {
        var css = Web("wwwroot", "css", "shell.css");
        foreach (var t in new[] { "--mo-pop-in: calc(200ms * var(--motion-k));", "--mo-pop-out: calc(140ms * var(--motion-k));", "--mo-scrim: calc(200ms * var(--motion-k));",
                     "--mo-drawer: calc(190ms * var(--motion-k));", "--mo-hover: calc(140ms * var(--motion-k));", "--mo-toast: calc(180ms * var(--motion-k));" })
            Assert.That(css, Does.Contain(t));
        Assert.That(css, Does.Contain(".wasteland .apop.apop-c { animation: b31c-pop-in var(--mo-pop-in)"));
        Assert.That(css, Does.Contain(".center-scroll.four .panel-host > * { animation: b31c-drawer-top var(--mo-drawer)"));
        Assert.That(css, Does.Contain(".wasteland .toast { animation: b31c-toast-in var(--mo-toast)"));
        Assert.That(css, Does.Contain(".wasteland .toast.leaving { animation: b31c-fade-out var(--mo-pop-out)"));
        var js = Web("wwwroot", "js", "app.js");
        Assert.That(js, Does.Contain("if (reduced()) return;"));
        Assert.That(js, Does.Contain("setTimeout(done, Math.round(160 * k()) + 40);"));
        Assert.That(Web("wwwroot", "css", "app.css"), Does.Contain("@media (prefers-reduced-motion: reduce)"), "B31a 全局减少动态效果仍在");
    }

    // ───────────── B31b 外壳遗留 ─────────────

    /// <summary>窄条贴面板覆盖区底边、面板让出窄条高度（不盖③④）；1280 暂停弹窗整窗一个滚动区、行动位吸附；开发传送离开后收起原地点面板。</summary>
    [Test]
    public void B31bLeftovers()
    {
        var css = Web("wwwroot", "css", "shell.css");
        Assert.That(css, Does.Contain(".center-scroll.four.locked.docked-prog .panel-host { bottom: var(--dock-live, var(--dock-h)); }"));
        Assert.That(Web("wwwroot", "js", "app.js"), Does.Contain("c.style.setProperty('--dock-live', v)"), "面板只让出窄条实际高度");
        Assert.That(css, Does.Contain(":is(.cprog.docked, .pause-dock) { bottom: calc(50% + var(--sh-gap) / 2); }"));
        Assert.That(css, Does.Contain(":is(.cprog.docked, .pause-dock) { bottom: calc(25% + var(--sh-gap) / 4); }"));
        Assert.That(css, Does.Contain("display: flex; flex-direction: column; overflow-y: auto; overscroll-behavior: contain; gap: 10px;"), "暂停弹窗整窗一个滚动区");
        Assert.That(Block(css, ".pause-scrim > .center-layer.pause-grid > .pg-mid {"), Does.Contain("position: sticky"));
        var menu = Web("Components", "MenuOverlay.razor");
        Assert.That(menu, Does.Contain("if (_locAtOpen != null && G.State != null && G.S.CurrentLocation != _locAtOpen && G.Panel != null) G.OpenPanel(null);"));
    }

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
