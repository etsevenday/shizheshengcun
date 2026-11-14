using Survival.Core.Engine;

namespace Survival.Core.Tests;

/// <summary>
/// BM7a（R234：S14 文首「2026-11-14 Q629—Q636」§Q632／Q633／Q634 与验收 Q632-A01／A02、Q633-A01／A02、Q634-A01；R03 文首 Q629—Q636 节；逐字稿同批表；02 Q632—Q634）：
/// 敌人旁战斗信息浮框与右侧敌人属性、敌我出手不可跳过、调整站位不合法号位不可选。网页壳表现按源码核对（同 BM6 壳测试）。
/// </summary>
public class BM7aBattleTests
{
    // ───── Q632：攻击预估在敌人旁，属性在右栏 ─────

    /// <summary>Q632-A01：选攻击悬停合法敌人——其旁「战斗信息」浮框（目标／实际承伤、成本、命中与暴击、伤害、目标状态，同一次预演），右栏该实例公开属性；下方入口保留；引导文字按逐字稿。</summary>
    [Test]
    public void Q632_A01_PopAndRail_SameFacts_ReadOnly()
    {
        var bs = Web("Components", "BattleScreen.razor");
        Assert.That(bs, Does.Contain("@if (G.AimPopTarget is int popIdx && e.BattleEnemyAt(b, popIdx) is BattleEnemy)"));
        Assert.That(bs, Does.Contain("<BattlePanel Mode=\"pop\" Index=\"popIdx\" />"), "浮框内容由 BattlePanel 同源渲染");
        Assert.That(bs, Does.Contain("@if (G.AimRailTarget is int railIdx)").And.Contain("<BattlePanel Mode=\"foe\" Index=\"railIdx\" />"));
        Assert.That(bs, Does.Contain("<BattlePanel Mode=\"info\" />").And.Contain("<BattlePanel Mode=\"detail\" />"), "下方「战斗信息」与「预测详情」保留");
        var panel = Web("Components", "BattlePanel.razor");
        var pop = Slice(panel, "private RenderFragment PopView()", "private RenderFragment FoeView()");
        Assert.That(pop, Does.Contain("G.E.PreviewCommand(G.S, BattleCommand.Of(G.BattleAction!, G.AreaAction ? null : Index, G.BattleItem))"), "与下方同一次只读预演（同行动者、技能、明确目标）");
        Assert.That(pop, Does.Contain("@BattleInfoCopy.InfoTab").And.Contain("@KeyRows(p, allMembers: true)").And.Contain("@p.Reason"), "标题「战斗信息」；空值写真实原因");
        var keyRows = Slice(panel, "private RenderFragment KeyRows(", "private RenderFragment MemberRows(");
        Assert.That(keyRows, Does.Contain("@foreach (var mt in p.Targets) { @MemberRows(mt) }"), "范围技能逐真实受影响成员分列");
        var mem = Slice(panel, "private RenderFragment MemberRows(", "private RenderFragment DetailView()");
        Assert.That(mem, Does.Contain("@BattleInfoCopy.RowHit").And.Contain("@BattleInfoCopy.DamageNoCrit").And.Contain("@BattleInfoCopy.RowState").And.Contain("@BattleInfoCopy.TakenBy"));
        var foe = Slice(panel, "private RenderFragment FoeView()", "    };");
        Assert.That(foe, Does.Contain("@ShellText.ObjectTab").And.Contain("<RailHead>").And.Contain("@FoeFacts(t)"), "右栏沿「对象」详情头，内容是敌人公开属性");
        Assert.That(foe, Does.Not.Contain("PreviewCommand").And.Not.Contain("预测详情"), "右栏不冒充攻击预估");
        var facts = Slice(panel, "private RenderFragment FoeFacts(", "private RenderFragment PopView()");
        Assert.That(facts, Does.Contain("<span>健康</span>").And.Contain("护甲 / 闪避 / 速度").And.Contain("B7Status(b, t)"));
        Assert.That(facts, Does.Contain("不显示敌人尚未执行的计划").And.Not.Contain("NextIntent").And.Not.Contain("PlannedTarget"), "沿 Q574：只留公开属性与已生效状态");
        // 逐字稿：瞄准引导、悬停执行说明、预计条纹说明
        Assert.That(bs, Does.Contain("public const string AimDefaultHint = \"把鼠标移到亮起的敌人上，看敌人旁的战斗信息浮框；右侧信息栏可看敌人详细属性。点击亮起的目标或按Enter执行，ESC取消。\";"));
        Assert.That(bs, Does.Contain("public const string FoeAimTip = \"点击即对它执行所选技能；本次预估见敌人旁的战斗信息浮框，详细属性见右侧信息栏。\";"));
        Assert.That(bs, Does.Contain("public const string ForecastTip = \"预计剩余生命（未暴击；暴击预估见战斗信息浮框或预测详情）\";"));
        Assert.That(bs, Does.Contain("悬停看敌人旁的战斗信息浮框逐个预估 · 右键/ESC 取消").And.Contain("点1、2号位打12，点3号位打23；悬停看敌人旁的战斗信息浮框 · 右键/ESC 取消"));
        Assert.That(bs, Does.Contain("$\"点亮起的任一1、2号位目标提交{sel.Label}（1、2号位都打） · 右键/ESC 取消\""), "群体引导原句不变");
        Assert.That(bs, Does.Not.Contain("悬停看逐个预测").And.Not.Contain("悬停看预测\"").And.Not.Contain("暴击见右侧").And.Not.Contain("实际预测见“战斗信息”").And.Not.Contain("把鼠标移到亮起的目标上看预测"));
    }

    /// <summary>Q632-A02：浮框不接点击／焦点、不成 ESC 层；播放／对白／遭遇提示／菜单／只读子页接管、目标失效或取消瞄准即撤；右栏叠层不挤窄站位，鼠标或焦点进入通用行动区时让开。</summary>
    [Test]
    public void Q632_A02_HiddenOnTakeover_NoFocus_NoLayoutShift()
    {
        var a = Web("Services", "GameSession.AimPop.cs");
        Assert.That(a, Does.Contain("BattleAiming && !DialogueLocked && !PlaybackActive && !EncounterPromptActive").And.Contain("&& !BattleMenuOpen && BattleDrawer == null && !BattleBusy && E.AwaitingPlayer(S);"));
        var popT = Slice(a, "public int? AimPopTarget", "public int? AimRailTarget");
        Assert.That(popT, Does.Contain("return BattleHover == t || kbd == t ? t : null;"), "单体：鼠标停在明确目标上或键盘指定它才浮出，离开即撤");
        Assert.That(popT, Does.Contain("_aimKbdCtx == AimCtx()"), "换技能／重新瞄准后旧键盘目标不兜底");
        var gs = Web("Services", "GameSession.cs");
        Assert.That(gs, Does.Contain("MarkAimKeyboard(list[next]);"), "←／→ 换目标即键盘指定");
        Assert.That(Slice(gs, "public void HoverBattleTarget(int? index)", "\n    }"), Does.Contain("ClearAimKeyboard();"), "鼠标真实移出后不被静止的键盘旧焦点抢回");
        var css = Web("wwwroot", "css", "battle.css");
        var popCss = Slice(css, ".bfs-aimpop {", "}");
        Assert.That(popCss, Does.Contain("pointer-events: none").And.Contain("position: fixed"), "浮框不接点击，视口定位不挤位");
        Assert.That(css, Does.Contain(".bfs-console:has(.bc-side:focus-within) .bfs-aimrail { display: none; }"));
        var bs = Web("Components", "BattleScreen.razor");
        var popMarkup = Slice(bs, "<div class=\"bfs-aimpop", "</div>");
        Assert.That(popMarkup, Does.Not.Contain("tabindex").And.Not.Contain("@onclick").And.Not.Contain("@onkeydown"), "浮框不可聚焦、不接指令");
        Assert.That(bs, Does.Contain("@onmouseenter=\"HideAimRail\""));
        var keys = Web("Services", "GameSession.Keys.cs");
        Assert.That(keys, Does.Not.Contain("AimPop"), "浮框不成 ESC 层");
        // 站位几何不变：八个锚点的 left 规则保留
        Assert.That(css, Does.Contain(".bfs-scene .anc.foe[data-slot=\"1\"] { left: calc(50cqw + 96 * var(--u)); }"));
    }

    /// <summary>
    /// BM7a 试玩修复（M891，S14 Q632「浮框就近避让右栏、人物关键数值和可点击目标，靠屏边可换到另一侧」、Q632-A02「浮框不遮目标点击／主要数值」、
    /// 「1280×720也须读到悬停敌人的命中、伤害和成本」）：浮框放在战场外按视口定位（不被战场区裁切），位置按避让区就近选择、可换到任一侧。
    /// </summary>
    [Test]
    public void M891_Q632_PopAvoidsTargetsAndNumbers_NotClipped()
    {
        var bs = Web("Components", "BattleScreen.razor");
        var scene = Slice(bs, "<section class=\"bfs-scene\"", "</section>");
        Assert.That(scene, Does.Not.Contain("bfs-aimpop"), "浮框不在 overflow:hidden 的战场区里");
        Assert.That(bs, Does.Contain("data-pop-for=\"@($\"foe-{popIdx}\")\""), "JS 按所指敌人锚点定位");
        var css = Web("wwwroot", "css", "battle.css");
        var pop = Slice(css, ".bfs-aimpop {", "}");
        Assert.That(pop, Does.Contain("position: fixed").And.Contain("max-height: calc(100vh - 8px)").And.Contain("visibility: hidden"));
        Assert.That(css, Does.Contain(".bfs-aimpop[data-placed] { visibility: visible; }"), "算好位置才显示，不先闪在旧位置");
        Assert.That(css, Does.Not.Contain(".bfs-aimpop[data-slot="), "不再固定朝中线一侧");
        Assert.That(css, Does.Contain(".bfs-aimpop.multi:has(.bi-mem:nth-child(3)) .bi-members { grid-template-columns: repeat(3, minmax(0, 1fr)); }"),
            "三名及以上受影响成员三列并排，1280 档也放得下、各成员读得到");
        var js = Web("wwwroot", "js", "app.js");
        var g = Slice(js, "function guards(anchor) {", "function overlap(");
        Assert.That(g, Does.Contain(".bfs-scene .anc.targetable, .bfs-aimrail:not(.ghost), .bfs-feed").And.Contain(".bfs-scene .anc .anc-foot").And.Contain(".bfs-scene .anc.ally .anc-head"),
            "避开合法目标锚点、右栏叠层、底部执行条、各人物脚下数值与我方 AP 行");
        Assert.That(g, Does.Contain("var a = rect(anchor); a.w = 3; out.push(a);"), "也不盖悬停的这名敌人");
        var ch = Slice(js, "function choose(a, w, h, gs, vw, vh) {", "function place() {");
        Assert.That(ch, Does.Contain("var xs = [a.l - w - 6, a.r + 6, cx - w / 2]"), "左侧放不下可换到右侧／上下");
        Assert.That(ch, Does.Contain("Math.max(m, Math.min(vw - w - m, xs[i]))").And.Contain("Math.max(m, Math.min(vh - h - m, ys[j]))"), "不出视口");
        Assert.That(ch, Does.Contain("var score = ov * 1000 + Math.sqrt(dx * dx + dy * dy);"), "先少盖、再就近");
        // 右栏叠层：让开后再悬停敌人或键盘重新指定即回来（沿 Q632「悬停……同时右侧信息栏显示」）
        var gs = Web("Services", "GameSession.cs");
        Assert.That(gs, Does.Contain("if (index != null && ShowAimRailAgain()) aimChanged = true;"));
        var ap = Web("Services", "GameSession.AimPop.cs");
        Assert.That(Slice(ap, "private void MarkAimKeyboard(int index)", "}"), Does.Contain("_aimRailHidden = null;"));
        // 底部：执行按钮不再被挤到阅读入口按钮下面
        Assert.That(css, Does.Contain(".bfs-feed .bp-confirm { flex-shrink: 0; }"));
        Assert.That(bs, Does.Contain("<span class=\"bx-hint\" title=\"@AimHint(sel)\">@AimHint(sel)</span>"), "引导句被省略时全文在悬停提示里");
    }

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

    // ───── Q634：不可去的位置不可选 ─────

    /// <summary>Q634-A01：选位中不合法号位不进 Tab 序列、点击／键盘激活不移动不扣费不取消不提示；无“可移过来”错误承诺；合法号位仍原提交与重验。</summary>
    [Test]
    public void Q634_A01_IllegalSlotsNotSelectable()
    {
        var swap = Web("Services", "GameSession.BattleSwap.cs");
        var click = Slice(swap, "public async Task ClickSwapSlot(int pos, string token)", "BattleItem = pos;");
        Assert.That(click, Does.Contain("if (!SwapAimSlots().Contains(pos)) return;"), "不合法：什么也不做");
        Assert.That(click, Does.Not.Contain("Notice").And.Not.Contain("ClearBattleAction"), "不新增提示、不退出选位");
        Assert.That(swap, Does.Contain("await ExecuteBattleCommand(token);"), "合法号位仍走原请求与内核重验");

        var bs = Web("Components", "BattleScreen.razor");
        Assert.That(bs, Does.Contain("var swapOff = G.BattleSwapAiming && !swapSlot;"));
        Assert.That(bs, Does.Contain("tabindex=\"@(swapOff ? -1 : 0)\""), "不合法的有人号位不进键盘序列");
        Assert.That(bs, Does.Contain("aria-disabled=\"@(swapOff ? \"true\" : null)\""));
        // 空号位：选位中的不合法空位没有 role／tabindex（不可激活），说明去掉“可移过来”
        var empty = Slice(bs, "<div class=\"anc ally empty @(G.BattleSwapAiming ? \"unreachable\" : \"\")\"", ">");
        Assert.That(empty, Does.Not.Contain("tabindex").And.Not.Contain("@onclick").And.Not.Contain("role="));
        Assert.That(empty, Does.Contain("G.BattleSwapAiming ? $\"我方{pos}号位：空位（空位不是未解锁）\""));
        Assert.That(bs, Does.Not.Contain("这里不能移过去"), "不新增提示句");
        // 合法号位仍亮起可点、可键盘激活；ESC／×／再点卡取消沿原规则
        Assert.That(bs, Does.Contain("@onclick=\"() => G.ClickSwapSlot(pos, token)\" @onkeydown=\"ev => EmptySlotKey(ev, pos, token, locked)\""));
        Assert.That(bs, Does.Contain("<button type=\"button\" class=\"bx-x\" aria-label=\"取消（ESC）\" title=\"取消（ESC/右键），不执行\" @onclick=\"G.ClearBattleAction\">×</button>"));
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
