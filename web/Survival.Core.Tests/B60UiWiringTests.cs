namespace Survival.Core.Tests;

/// <summary>
/// B60 界面接线（S01／S14 文首「2026-11-24 Q1038 主动行动统一居中显示进度」选 C；S11／S14 §Q1030）：只测 bUnit 渲染不到的样式与浏览器脚本——
/// 整屏进度遮罩的层级与模糊、挖洞白名单接进 survivalFocus、键盘路由在进度浮窗最上层时只认浮窗、开始时取消拖拽、城市层右下角进度规则已撤。
/// 组件里的显示、inert、Esc／快捷键行为见 Survival.Web.Tests 的 B60ProgressLockUiTests。
/// </summary>
public class B60UiWiringTests
{
    private static string Web(string relative) => SourceText.Web(relative);

    private static string Block(string css, string head)
    {
        var at = css.IndexOf(head, StringComparison.Ordinal);
        Assert.That(at, Is.GreaterThanOrEqualTo(0), head);
        var end = css.IndexOf('}', at);
        return css.Substring(at, end - at);
    }

    /// <summary>
    /// Q1038「遮罩和模糊作用于整个框外背景」：.prog-focus 固定铺满视口、压暗＋模糊（开发方取 BM6 对白聚焦同值 0.48／2px），亮着时接住指针；
    /// 层级在一级面板（30／31）、局部盘遮罩（42）之上，居中浮窗（45）与暂停弹窗（46）之下——浮窗与弹窗清晰可点。
    /// </summary>
    [Test]
    public void Mask_FullViewport_DimBlur_BetweenPanelsAndProgress()
    {
        var css = Web("wwwroot/css/shell.css");
        var mask = Block(css, ".prog-focus {");
        Assert.That(mask, Does.Contain("position: fixed; inset: 0; z-index: 44;"));
        Assert.That(mask, Does.Contain("backdrop-filter: blur(2px)").And.Contain("background: rgb(0 0 0 / .48)"));
        Assert.That(mask, Does.Contain("pointer-events: none"), "不亮时不拦截");
        Assert.That(Block(css, ".prog-focus.on {"), Does.Contain("pointer-events: auto"), "亮着时点击、触摸、拖放、悬停都落在遮罩上");
        Assert.That(Block(css, ".pause-scrim {"), Does.Contain("z-index: 46"), "暂停弹窗在遮罩之上、也在一级面板（30／31）之上");
        Assert.That(Block(Web("wwwroot/css/app.css"), ".cprog.float {"), Does.Contain("z-index: 45"), "居中浮窗在遮罩之上");
        Assert.That(Block(Web("wwwroot/css/fourrows.css"), ".center-scroll.four .panel-host {"), Does.Contain("z-index: 30"), "一级面板在遮罩之下");
        Assert.That(Block(css, ".wasteland .center > .disk-scrim {"), Does.Contain("z-index: 42"));
    }

    /// <summary>Q1038 C：撤掉城市层右下角进度——cityoverview.css 不再给城市层里的 .cprog 另定位置；GameScreen 挂着进度遮罩、白名单来自 G.ProgressKeep。</summary>
    [Test]
    public void CityCornerRuleRemoved_MaskMountedWithKeepList()
    {
        Assert.That(Web("wwwroot/css/cityoverview.css"), Does.Not.Contain(".cprog"));
        var screen = Web("Components/GameScreen.razor");
        Assert.That(screen, Does.Contain("<div class=\"prog-focus @(progLock ? \"on\" : \"\")\" data-keep=\"@(progLock ? G.ProgressKeep : null)\""));
    }

    /// <summary>白名单挖洞：survivalFocus 同时处理对白聚焦与进度遮罩（按各自 data-keep 算 clip-path）。</summary>
    [Test]
    public void FocusHoles_CoverProgressMask()
    {
        var js = Web("wwwroot/js/app.js");
        Assert.That(js, Does.Contain("function scrims() { return document.querySelectorAll('.dlg-focus, .prog-focus'); }"));
    }

    /// <summary>
    /// Q1038「焦点和业务快捷键只服从最上层」、§Q1030「键盘焦点（Tab 不能进背景）和能向背景派发业务操作的快捷键」：进度浮窗根（data-prog-lock）在时，
    /// 键盘路由在菜单、对白之后、加工菜单 Tab 循环之前接管——Tab 只在浮窗里循环，Esc 交给游戏，空格只点浮窗自己的「跳过动画」，其余键不落到背景。
    /// </summary>
    [Test]
    public void KeyRouter_ProgressLockOwnsKeys_AfterMenuAndDialogue()
    {
        var js = Web("wwwroot/js/app.js");
        var menu = js.IndexOf("if (document.querySelector('.menu-modal')) {", StringComparison.Ordinal);
        var dlg = js.IndexOf("if (document.querySelector('.dlg-layer')) {", StringComparison.Ordinal);
        var lockAt = js.IndexOf("var plk = document.querySelector('[data-prog-lock]');", StringComparison.Ordinal);
        var trap = js.IndexOf("var trap = document.querySelector('[data-tab-trap]');", StringComparison.Ordinal);
        Assert.That(new[] { menu, dlg, lockAt, trap }, Is.Ordered.And.All.GreaterThan(0));
        var trapIf = js.LastIndexOf("if (k === 'Tab' && !mod) {", trap, StringComparison.Ordinal);
        Assert.That(trapIf, Is.GreaterThan(lockAt));
        var block = js.Substring(lockAt, trapIf - lockAt);
        Assert.That(block, Does.Contain("if (k === 'Tab') { trapTab(ev, plk, plk.matches('[tabindex]') ? plk : null); return; }"));
        Assert.That(block, Does.Contain("if (k === 'Escape') { if (!ev.repeat) send('Escape'); else ev.preventDefault(); return; }"));
        Assert.That(block, Does.Contain("var psk = plk.querySelector('[data-skip-anim]');"), "空格只找浮窗里的跳过");
        Assert.That(block, Does.Contain("if (onCtl) return;"), "浮窗里的按钮按原生语义");
        Assert.That(block.TrimEnd().EndsWith("return;\n      }", StringComparison.Ordinal), Is.True, "其余键到此为止，不往下落到背景路由");
    }

    /// <summary>§Q1030 D1／D2「已开始的连续转移不得继续提交，遮罩退出后需要新的明确输入」：进度浮窗成为最上层时取消未提交拖拽并收起背景焦点。</summary>
    [Test]
    public void Onset_CancelsDrag_BlursBackground_EndRestores()
    {
        var js = Web("wwwroot/js/app.js");
        var onset = js.Substring(js.IndexOf("progLockOnset: function () {", StringComparison.Ordinal));
        onset = onset.Substring(0, onset.IndexOf("\n  },", StringComparison.Ordinal));
        Assert.That(onset, Does.Contain("survivalDrag.cancel();"));
        Assert.That(onset, Does.Contain("a.blur();"));
        var end = js.Substring(js.IndexOf("progLockEnd: function (pref) {", StringComparison.Ordinal));
        end = end.Substring(0, end.IndexOf("\n  },", StringComparison.Ordinal));
        Assert.That(end, Does.Contain("if (usable(prev)) return prev;").And.Contain("t.focus({ preventScroll: true });").And.Not.Contain(".click()"), "原焦点合法先回它；只还焦点、不执行");
        var screen = Web("Components/GameScreen.razor");
        // B60a 试玩修复（S14 B60 Q1103「焦点沿 Q1038 回到仍合法的位置」）：结束时带上原页落点（改期望：原为不带参数的 progLockEnd）
        Assert.That(screen, Does.Contain("if (plock) await JS.InvokeVoidAsync(\"survival.progLockOnset\");"));
        Assert.That(screen, Does.Contain("else await JS.InvokeVoidAsync(\"survival.progLockEnd\", new object[] { G.ProgReturnFocus });"));
    }

    /// <summary>
    /// B60a 试玩修复（S14 B60 Q1103「焦点沿 Q1038 回到仍合法的位置」）：原焦点已不在（点火盘确认键、随所选清除而消失的「出发」键）时，
    /// progLockEnd 先按网页给的原页落点（地图当前位置节点／地图容器、火堆菜单），再按原焦点所在区域（开始时记下的上级）找第一个可用、不在 inert 里的控件；
    /// 都没有就不动——不把焦点放到 body。原焦点还在、只是暂时 inert 时等它解除再回它（沿 B60 原行为）。真实落点已用无头 Chrome 核对（开发记录）。
    /// </summary>
    [Test]
    public void LockEnd_PrevGone_FallsBackToPageThenRegion_NeverBody()
    {
        var js = Web("wwwroot/js/app.js");
        var onset = js.Substring(js.IndexOf("progLockOnset: function () {", StringComparison.Ordinal));
        onset = onset.Substring(0, onset.IndexOf("\n  },", StringComparison.Ordinal));
        Assert.That(onset, Does.Contain("window.__survivalProgChain = chain;"), "开始时记下原焦点所在区域");
        var end = js.Substring(js.IndexOf("progLockEnd: function (pref) {", StringComparison.Ordinal));
        end = end.Substring(0, end.IndexOf("\n  },", StringComparison.Ordinal));
        var iPrev = end.IndexOf("if (usable(prev)) return prev;", StringComparison.Ordinal);
        var iPref = end.IndexOf("var t = firstIn(document.querySelector(pref[i]));", StringComparison.Ordinal);
        var iChain = end.IndexOf("var u = firstIn(chain[j]);", StringComparison.Ordinal);
        Assert.That(new[] { iPrev, iPref, iChain }.All(i => i >= 0) && iPrev < iPref && iPref < iChain, Is.True, "顺序：原焦点 → 原页落点 → 原区域");
        Assert.That(end, Does.Contain("if (c.closest && c.closest('[inert]')) { blocked = true; return false; }"), "不进 inert");
        Assert.That(end, Does.Contain("if (!t) { if (blocked && ++tries < 20) requestAnimationFrame(go); return; }"), "没有合法落点就不动");
        Assert.That(end, Does.Not.Contain("document.body.focus"));
    }
}
