using Survival.Core.Engine;

namespace Survival.Core.Tests;

/// <summary>
/// BM6（R233：S14「2026-11-14 BM5c／BM5a Q620—Q628」§Q625、§Q625／Q628、§Q626、§Q627、§Q628，沿 §Q612；02 Q625—Q628）：
/// 对白背景压暗＋模糊与白名单、柔和进出、帮助正式全文、「回到最新」与句首锚定分离、战斗对白同效果。
/// 网页壳表现只能按源码核对（同 BM5a／BM5c 壳测试）；实际清晰度、点穿与动效由无头浏览器自测（见开发记录 BM6）。
/// </summary>
public class BM6DialogueFocusTests
{
    // ───── Q625：参数、白名单与一层遮罩 ─────

    /// <summary>Q625-A01：默认参数沿 S14（遮罩 0.48、模糊 2px、表面提亮约 6%、入场 180／退场 140ms），全在交开发方定的可调范围内。</summary>
    [Test]
    public void Q625_A01_DefaultParameters()
    {
        Assert.That(DialogueFocus.Dim, Is.EqualTo(0.48).And.InRange(0.44, 0.55));
        Assert.That(DialogueFocus.BlurPx, Is.EqualTo(2).And.InRange(1, 3), "正常目标必须有模糊");
        Assert.That(DialogueFocus.Lift, Is.EqualTo(1.06).And.InRange(1.0, 1.08));
        Assert.That(DialogueFocus.InMs, Is.EqualTo(180).And.InRange(160, 220));
        Assert.That(DialogueFocus.ExitMs, Is.EqualTo(140).And.InRange(120, 180));
        var css = Web("wwwroot", "css", "shell.css");
        var rule = Rule(css, ".dlg-focus {");
        Assert.That(rule, Does.Contain("background: rgb(0 0 0 / .48);"));
        Assert.That(rule, Does.Contain("backdrop-filter: blur(2px);"), "只模糊遮罩背后，不模糊对白");
        Assert.That(rule, Does.Contain("opacity calc(140ms * var(--motion-k, 1)) ease-in"), "退场缓入 140ms × 动画速度");
        Assert.That(rule, Does.Contain("visibility 0s linear calc(140ms * var(--motion-k, 1))"), "退场完成前遮罩仍接住点击");
        Assert.That(Rule(css, ".dlg-focus.on {"), Does.Contain("opacity calc(180ms * var(--motion-k, 1)) ease-out"), "入场缓出 180ms × 动画速度");
        Assert.That(Rule(css, ".dlg-layer {"), Does.Contain("filter: brightness(1.06);"), "对白表面微提亮，正文同比提亮、对比度不降");
        // 减少动态效果：全局规则只缩短渐变，不去掉模糊或遮罩
        var app = Web("wwwroot", "css", "app.css");
        var reduce = Slice(app, "@media (prefers-reduced-motion: reduce) {\n  *, *::before, *::after {", "}\n}");
        Assert.That(reduce, Does.Not.Contain("backdrop-filter").And.Not.Contain("filter:"));
        Assert.That(css, Does.Not.Contain("prefers-reduced-motion: reduce) { .dlg-focus"), "不用关模糊冒充减少动态");
    }

    /// <summary>Q625-A01：白名单只有对白层与原本可用的顶栏「菜单」、底栏「在此页面继续保存」「重试」、结果提示 ×；战斗只有对白本身。</summary>
    [Test]
    public void Q625_A01_Whitelist()
    {
        var keep = DialogueFocus.MainKeep.Split(',').Select(x => x.Trim()).ToArray();
        Assert.That(keep, Is.EqualTo(new[] { ".center-scroll.four > .dlg-layer", ".top [data-menu-key]", ".footer .save button", ".toast > button" }));
        Assert.That(DialogueFocus.BattleKeep, Is.EqualTo(".bfs-dlg > .dlg-layer"));

        var g = Web("Components", "GameScreen.razor");
        // 「菜单」本来就在对白锁期间可用（不跟 navOff 禁用），其余顶栏键对白锁时禁用
        var menu = Slice(g, "data-menu-key", "</button>");
        Assert.That(menu, Does.Not.Contain("disabled"));
        Assert.That(g, Does.Contain("var navOff = battleUi || dlgLock;"));
        // 底栏存档处理键：在此页面继续保存只在 CanReclaim 时出现、重试按原 RetryBlockedText 禁用——白名单不改这些条件
        var save = Slice(g, "<div class=\"save @(", "</footer>");
        Assert.That(save, Does.Contain("@if (sv.CanReclaim)").And.Contain("disabled=\"@(sv!.RetryBlockedText != null)\""));
        Assert.That(save.Split("<button").Length - 1, Is.EqualTo(2), "底栏只有这两种按钮，不新开手动保存／读档");
        Assert.That(g, Does.Contain("<button aria-label=\"关闭结果提示\" @onclick=\"G.DismissToast\">×</button>"), "结果提示 × 原样");
        Assert.That(g, Does.Contain("<footer class=\"footer\" inert=\"@fullBattle\">"), "底栏对白锁期间原本不 inert");

        // 战斗：顶条（含战斗菜单键）对白期间原本就 inert，不进白名单
        var b = Web("Components", "BattleScreen.razor");
        Assert.That(b, Does.Contain("var lockedUi = dlg || fxLock || G.EncounterPromptActive;"));
        Assert.That(b, Does.Contain("<header class=\"bfs-top\" inert=\"@lockedUi\">"));
    }

    /// <summary>Q625-A01：主界面只有一层遮罩（在底栏之后、全屏战斗之前），亮的条件是对白锁、非全屏战斗、未死亡；遮罩层级在背景之上、更高层之下。</summary>
    [Test]
    public void Q625_A01_SingleScrimAndLayering()
    {
        Assert.That(DialogueFocus.MainOn(true, false, false), Is.True);
        Assert.That(DialogueFocus.MainOn(false, false, false), Is.False);
        Assert.That(DialogueFocus.MainOn(true, true, false), Is.False, "全屏战斗由战斗内一层负责");
        Assert.That(DialogueFocus.MainOn(true, false, true), Is.False, "死亡结算不亮");

        var g = Web("Components", "GameScreen.razor");
        Assert.That(g.Split("class=\"dlg-focus").Length - 1, Is.EqualTo(1), "只处理一次背景");
        var scrim = g.IndexOf("<div class=\"dlg-focus main @(DialogueFocus.MainOn(dlgLock, fullBattle, s.Progress.Dead) ? \"on\" : \"\")", StringComparison.Ordinal);
        Assert.That(scrim, Is.GreaterThan(g.IndexOf("</footer>", StringComparison.Ordinal)));
        Assert.That(scrim, Is.LessThan(g.IndexOf("@if (fullBattle) { <BattleScreen /> }", StringComparison.Ordinal)));
        Assert.That(g, Does.Contain("data-keep=\"@DialogueFocus.MainKeep\" aria-hidden=\"true\"></div>"));
        Assert.That(Web("Components", "DialogueLayer.razor").Split("dlg-focus").Length - 1, Is.EqualTo(1), "对白层自己不加遮罩（只在注释里提到）");

        var css = Web("wwwroot", "css", "shell.css");
        var z = Z(Rule(css, ".dlg-focus {"));
        Assert.That(z, Is.GreaterThan(60), "在结果提示（fourrows .toast 60）之上，提示文字作背景压暗");
        Assert.That(z, Is.LessThan(65), "在战后全记录（65）、特写（70）、强制整理（80）、游戏菜单（85）之下");
        Assert.That(Web("wwwroot", "css", "fourrows.css"), Does.Contain(".toast { top: auto; bottom: calc(-1 * var(--foot-h)); min-height: var(--foot-h); padding: 0 4px 0 14px; z-index: 60;"));
        Assert.That(Z(Rule(css, ".menu-scrim {")), Is.EqualTo(85));
    }

    /// <summary>Q625-A01：app.js 按 data-keep 的实际位置在遮罩上挖洞（一层 clip-path），不先模糊整页再“恢复”按钮；白名单以外落在遮罩上。</summary>
    [Test]
    public void Q625_A01_HolesFromWhitelist()
    {
        var f = FocusJs();
        Assert.That(f, Does.Contain("var sel = s.getAttribute('data-keep');"));
        Assert.That(f, Does.Contain("return \"path(evenodd, '\" + d + \"')\";"));
        Assert.That(f, Does.Contain("if (s.__p !== p) { s.__p = p; s.style.clipPath = p; }"), "只在洞变化时更新");
        Assert.That(f, Does.Contain("new MutationObserver(schedule)").And.Contain("window.addEventListener('resize', schedule);").And.Contain("document.addEventListener('scroll', schedule, true);"));
        Assert.That(f, Does.Not.Contain("invokeMethodAsync"), "纯视觉，不回调游戏");
        Assert.That(System.Text.RegularExpressions.Regex.IsMatch(Web("wwwroot", "css", "shell.css"), @"(?<![-\w])filter:\s*blur\("), Is.False, "不对整页／对白用 filter 模糊");
    }

    // ───── Q625-A02：进出、入场第一下、菜单冻结、读档 ─────

    /// <summary>Q625-A02：对白层首次淡入，同一层在连续台词与相邻下一段间保留；结束后退场影子淡出、内部 inert、自身接住点击；换局／终局不留影子。</summary>
    [Test]
    public void Q625_A02_EnterExitGhost()
    {
        var css = Web("wwwroot", "css", "shell.css");
        Assert.That(Rule(css, ".dlg-layer {"), Does.Contain("animation: dlg-in calc(180ms * var(--motion-k, 1)) ease-out;"));
        Assert.That(css, Does.Contain("@keyframes dlg-in { from { opacity: 0; } }"));
        Assert.That(Rule(css, ".dlg-layer.dlg-ghost {"), Does.Contain("opacity: 0; pointer-events: auto;"));
        Assert.That(Rule(css, ".dlg-layer.dlg-static {"), Does.Contain("animation-duration: 0s;"), "不换动画名，静态层之后不重播淡入");
        Assert.That(css, Does.Not.Contain(".dlg-layer.dlg-static { animation: none;"));
        Assert.That(Rule(css, ".dlg-focus.static {"), Does.Contain("transition: none; animation-duration: 0s;"));
        Assert.That(Rule(css, ".dlg-focus.on {"), Does.Contain("animation: dlg-in calc(180ms * var(--motion-k, 1)) ease-out;"), "页面新建时（标题进新局、战斗页刚打开）遮罩也淡入");

        var d = Web("Components", "DialogueLayer.razor");
        Assert.That(d, Does.Contain("if (!ReferenceEquals(G.State, _stateSeen)) { _stateSeen = G.State; _last = null; _ghost = null; }"), "读档／新局／回标题不留影子");
        Assert.That(d, Does.Contain("if (live != null) { _last = live; _ghost = null; }"), "影子期间来了下一段对白直接换回正式层");
        Assert.That(d, Does.Contain("else if (_last != null && _ghost == null && G.State != null && !G.S.Progress.Dead) { _ghost = _last; _ = EndGhost(_ghost); }"));
        Assert.That(d, Does.Contain("await Task.Delay(G.MotionMs(DialogueFocus.ExitMs));"));
        Assert.That(d, Does.Contain("<div class=\"dlg-side left @(leftSpeaking ? \"speaking\" : \"idle\")\" inert=\"@ghost\">"));
        Assert.That(d, Does.Contain("<div class=\"dlg-main\" inert=\"@ghost\">"));
        Assert.That(d, Does.Contain("<div class=\"dlg-side right @(rightSpeaking ? \"speaking\" : \"idle\")\" inert=\"@ghost\">"));
        Assert.That(d, Does.Contain("aria-hidden=\"@(ghost ? \"true\" : null)\""));
        Assert.That(d, Does.Contain("inert=\"@(G.MenuOpen || G.BattleMenuOpen)\""), "菜单开着时对白层不可操作（Q322）");

        // 退场淡出期间不补世界段、不推进敌方 AI；对白结束即开新场次时不等（不延迟真实接管）
        var session = Web("Services", "GameSession.cs");
        var ack = Slice(session, "public void AckDialogue(string id)", "/// <summary>\n    /// BM6：对白结束后的原接续");
        Assert.That(ack, Does.Contain("var newBattle = S.Battle != null && S.Battle.BattleId != battleBefore;"));
        Assert.That(ack, Does.Contain("_ = AfterDialogueExit(PromptThenBattle, !newBattle);").And.Contain("_ = AfterDialogueExit(RunLoop, true);"));
        var after = Slice(session, "private async Task AfterDialogueExit(Func<Task> next, bool wait)", "await next();");
        Assert.That(after, Does.Contain("await Task.Delay(MotionMs(DialogueFocus.ExitMs));"));
        Assert.That(after, Does.Contain("if (State == null || !ReferenceEquals(State, st) || E.DialogueLocked(S) || S.Progress.Dead) return;"));
        Assert.That(Slice(session, "public void AnswerRecruit(", "/// <summary>回看已完成委托的对白"), Does.Contain("_ = AfterDialogueExit(RunLoop, true);"));
        // 读档：已在办的对白静态显示，不重播入场；新局／回标题清掉
        Assert.That(Slice(session, "private void ResetUi()", "// ───────────── 选择"), Does.Contain("FocusStaticDialogue = null;"));
        Assert.That(Slice(session, "public string? LoadSlot(string key)", "public void BackToTitle()"), Does.Contain("ResetUi();\n        FocusStaticDialogue = E.ActiveDialogue(S)?.Id;"));
    }

    /// <summary>Q625-A02：入场期间第一下点击（对白或背景）／Enter／Space 只完成入场，不推进台词；白名单按钮照常；菜单打开冻结入场，关闭续原进度。</summary>
    [Test]
    public void Q625_A02_FirstInputCompletesEntry()
    {
        var f = FocusJs();
        var down = Slice(f, "window.addEventListener('pointerdown', function (ev) {", "}, true);");
        Assert.That(down, Does.Contain("if (!L || !entering(L).length || document.querySelector('.menu-scrim')) return;"));
        Assert.That(down, Does.Contain("if (!onBackgroundOrDialogue(ev.target, L)) return;"), "白名单按钮不被拦");
        Assert.That(down, Does.Contain("finishEntry(L);").And.Contain("ev.stopImmediatePropagation();"));
        Assert.That(f, Does.Contain("return !!(t && t.closest && (layer.contains(t) || t.closest('.dlg-focus')));"));
        var key = Slice(f, "window.addEventListener('keydown', function (ev) {", "}, true);");
        Assert.That(key, Does.Contain("if (ev.repeat || (ev.key !== 'Enter' && ev.key !== ' ')) return;"), "ESC 不拦，照原路由开菜单");
        Assert.That(key, Does.Contain("onKeep(document.activeElement)"), "焦点在菜单／存档键上时按原生语义");
        Assert.That(f, Does.Contain("var menu = !!document.querySelector('.menu-scrim');"));
        Assert.That(f, Does.Contain("try { list[i].pause(); paused.push(list[i]); } catch (e) { }"));
        Assert.That(f, Does.Contain("for (var j = 0; j < p.length; j++) { try { p[j].play(); } catch (e) { } }"));
        // 退场影子期间：对白路由仍在（.dlg-layer），影子里的继续键在 inert 里，Enter 不会再点一次
        var app = Web("wwwroot", "js", "app.js");
        Assert.That(app, Does.Contain("if (nx && !nx.disabled && !(nx.closest && nx.closest('[inert]'))) { ev.preventDefault(); nx.click(); }"));
    }

    // ───── Q626：帮助正式全文 ─────

    /// <summary>Q626-A01：系统说明「人物对话与对话记录」三段：第1段下两栏、第2段带原按钮例外、第3段原文；旧同义句不再出现。</summary>
    [Test, Category("DesignSync")]
    public void Q626_A01_HelpParagraphs()
    {
        var p1 = "人物对话会盖住当前地面与随身物品两行，左侧是你，右侧是说话的对象。对话一句一句累计显示，点“继续”看下一句，最后一句是“结束对话”。";
        var p2 = "对话没读完时，场景、物品和其他业务操作暂停受理；菜单和原可用的存档异常处理按钮仍可使用。正在进行的行动或战斗停在原处等你读完，不会在你阅读时偷偷推进时间。";
        var p3 = "读过的对话都能在顶栏“对话记录”里按次重看，重看不会重新触发剧情。";
        var h = Web("Services", "HelpCatalog.cs");
        var entry = Slice(h, "Id = \"dialogue\", Title = \"人物对话与对话记录\"", "new HelpEntry");
        var lines = entry.Split('\n').Select(l => l.Trim()).Where(l => l.StartsWith("\"", StringComparison.Ordinal)).ToArray();
        Assert.That(lines, Is.EqualTo(new[] { $"\"{p1}\",", $"\"{p2}\",", $"\"{p3}\"," }));
        Assert.That(h, Does.Not.Contain("人物对话会盖住地图与设施两行").And.Not.Contain("游戏的其他操作都会暂停受理"));
        var s14 = Doc("S14 主界面与全局UI交互系统.md");
        var v2 = Doc("重墨 V2 开工裁定与界面逐字稿.md");
        foreach (var p in new[] { p1, p2, p3 })
        {
            Assert.That(s14, Does.Contain($"> {p}"), "S14 Q626 原句");
            Assert.That(v2, Does.Contain($"> {p}"), "逐字稿原句");
        }
    }

    // ───── Q627：回到最新 ─────

    /// <summary>
    /// Q627-A01：新长句仍句首锚定（跟读），但只要正文实际未到底就显示「回到最新」；点后滚到正文最底并隐藏，不推进台词；
    /// 显隐只看真实底部，与跟读状态分开；程序锚定引起的滚动不算用户回看；用户真回看时新句不抢位置；缩放／字体改变后重验。
    /// </summary>
    [Test]
    public void Q627_A01_LatestButtonSeparateFromFollow()
    {
        var js = Web("wwwroot", "js", "app.js").Replace("\r\n", "\n");
        var dlg = Slice(js, "window.survivalDlg = (function () {", "})();");
        Assert.That(dlg, Does.Contain("function refresh(el) { showLatest(el, !bottom(el)); }"), "显隐只看真实底部（沿原 8px 容差）");
        Assert.That(dlg, Does.Contain("function bottom(el) { return el.scrollTop + el.clientHeight >= el.scrollHeight - 8; }"));
        Assert.That(dlg, Does.Not.Contain("showLatest(el, true)").And.Not.Contain("showLatest(el, false)").And.Not.Contain("showLatest(el, !on)"), "不再按“是否跟读／是否有新句”显隐");
        // 程序滚动不算回看
        Assert.That(dlg, Does.Contain("if (st) st.prog = el.scrollTop !== before ? el.scrollTop : null;"));
        Assert.That(dlg, Does.Contain("if (st.prog !== null && Math.abs(el.scrollTop - st.prog) <= 2) st.prog = null;   // 程序锚定／回到最新引起的滚动"));
        Assert.That(dlg, Does.Contain("else { st.prog = null; st.anchored = false; st.follow = following(el); }"));
        Assert.That(dlg, Does.Contain("function following(el) { return bottom(el) || el.scrollTop >= anchorTop(el) - 8; }"), "真往上翻过句首才退出跟读");
        // 新句：跟读时句首锚定，回看时不动；两种都只按到底与否显示按钮
        var sync = Slice(dlg, "sync: function (el, count) {", "latest: function (el) {");
        Assert.That(sync, Does.Contain("if (st.follow) anchor(el);   // 跟读：新句句首锚定（不算回看）\n      refresh(el);"));
        // 回到最新：滚到已显示正文最底，恢复跟读
        var latest = Slice(dlg, "latest: function (el) {", "\n  };");
        Assert.That(latest, Does.Contain("setTop(el, Math.max(0, el.scrollHeight - el.clientHeight));").And.Contain("st.follow = true;").And.Contain("refresh(el);"));
        Assert.That(latest, Does.Not.Contain("anchor(el)"), "不只锚最后一句句首");
        Assert.That(dlg, Does.Not.Contain("invokeMethodAsync").And.Not.Contain("click()"), "不推进台词、不提交选项");
        // 缩放、字体、头像布局改变后按同一滚动容器重验
        Assert.That(dlg, Does.Contain("st.ro = new ResizeObserver(function () {").And.Contain("if (st.follow && st.anchored) anchor(el);"));
        var layer = Web("Components", "DialogueLayer.razor");
        Assert.That(layer, Does.Contain("<span class=\"dlg-latest-host\"><button type=\"button\" class=\"secondary dlg-latest\" @onclick=\"Latest\">回到最新 ↓</button></span>"));
        Assert.That(layer, Does.Contain("await JS.InvokeVoidAsync(\"survivalDlg.latest\", _stream);"));
    }

    // ───── Q628：战斗对白 ─────

    /// <summary>Q628-A01：全屏战斗对白同一压暗＋模糊／清晰对白／柔和进出；仍原专用区域；特写独占播放时不预开聚焦；遮罩在战斗菜单与播放层之下。</summary>
    [Test]
    public void Q628_A01_BattleDialogueFocus()
    {
        Assert.That(DialogueFocus.BattleOn(true, false), Is.True);
        Assert.That(DialogueFocus.BattleOn(true, true), Is.False, "Q572：先播完已提交出手再由对白接管");
        Assert.That(DialogueFocus.BattleOn(false, false), Is.False);

        var b = Web("Components", "BattleScreen.razor");
        Assert.That(b, Does.Contain("var dlgFocusOn = DialogueFocus.BattleOn(dlg, fxLock);"));
        Assert.That(b.Split("class=\"dlg-focus").Length - 1, Is.EqualTo(1));
        var scrim = b.IndexOf("<div class=\"dlg-focus battle @(dlgFocusOn ? \"on\" : \"\") @(dlgStatic ? \"static\" : \"\")\" data-keep=\"@DialogueFocus.BattleKeep\" aria-hidden=\"true\"></div>", StringComparison.Ordinal);
        Assert.That(scrim, Is.GreaterThan(b.IndexOf("<div class=\"bfs-grid", StringComparison.Ordinal)));
        Assert.That(scrim, Is.LessThan(b.IndexOf("<BattleFxLayer />", StringComparison.Ordinal)));
        Assert.That(scrim, Is.LessThan(b.IndexOf("@if (G.BattleMenuOpen) { <MenuOverlay Battle=\"true\" /> }", StringComparison.Ordinal)));
        Assert.That(b, Does.Contain("@if (dlg || _dlgExitSeq > 0)"), "结束后留退场影子");
        Assert.That(b, Does.Contain("await Task.Delay(G.MotionMs(DialogueFocus.ExitMs));"));
        Assert.That(b, Does.Not.Contain("<ItemShelf"), "战斗里不造地面／随身两栏");

        var css = Web("wwwroot", "css", "shell.css");
        Assert.That(css, Does.Contain(".bfs > .dlg-focus { z-index: 50; }"));
        Assert.That(css, Does.Contain(".bfs-dlg.wait > .dlg-layer { visibility: hidden; animation: none; }"));
        var battle = Web("wwwroot", "css", "battle.css");
        Assert.That(battle, Does.Contain(".bfs-dlg, .bfs-drawer { grid-row: 3 / span 2; grid-column: 1 / -1; position: relative; z-index: 40; }"), "对白区位置不变、在遮罩洞里");
        Assert.That(battle, Does.Contain(".bfx-layer {\n  position: fixed; inset: 0; z-index: 70;").Or.Contain(".bfx-layer {\r\n  position: fixed; inset: 0; z-index: 70;"), "特写播放层在遮罩之上");
        Assert.That(battle, Does.Contain(".enc-prompt { position: fixed; inset: 0; z-index: 72;"));
    }

    // ───── 工具 ─────

    private static string FocusJs()
    {
        var js = Web("wwwroot", "js", "app.js").Replace("\r\n", "\n");
        return Slice(js, "window.survivalFocus = (function () {", "  return { refresh: schedule };");
    }

    private static int Z(string rule)
    {
        var m = System.Text.RegularExpressions.Regex.Match(rule, @"z-index:\s*(\d+)");
        Assert.That(m.Success, rule);
        return int.Parse(m.Groups[1].Value);
    }

    private static string Rule(string css, string head)
    {
        var i = css.IndexOf("\n" + head, StringComparison.Ordinal);
        Assert.That(i, Is.GreaterThanOrEqualTo(0), head);
        var r = css.Substring(i + 1);
        return r.Substring(0, r.IndexOf('}'));
    }

    private static string Slice(string text, string from, string to)
    {
        var a = text.IndexOf(from, StringComparison.Ordinal);
        Assert.That(a, Is.GreaterThanOrEqualTo(0), from);
        var b = text.IndexOf(to, a + from.Length, StringComparison.Ordinal);
        Assert.That(b, Is.GreaterThan(a), to);
        return text[a..b];
    }

    private static string Doc(string name) => File.ReadAllText(Directory.GetFiles(Path.Combine(Root(), "doc", "策划案"), name, SearchOption.AllDirectories).Single()).Replace("\r\n", "\n");

    private static string Root()
    {
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "web", "Survival.sln"))) dir = dir.Parent;
        Assert.That(dir, Is.Not.Null, "找到仓库根目录");
        return dir!.FullName;
    }

    private static string Web(params string[] parts) => File.ReadAllText(Path.Combine(new[] { Root(), "web", "Survival.Web" }.Concat(parts).ToArray())).Replace("\r\n", "\n");
}
