using System.Text.RegularExpressions;
using Survival.Core.Engine;

namespace Survival.Core.Tests;

/// <summary>
/// BM3 次要问题合并（需求 R188—R194；策划提交 0695e9d、f6834f1 答复 Q509—Q513、Q520、Q522）：
/// S14「2026-11-12 BM2 Q509—Q513 瞄准一致性与预测文案」（验收 Q509-A01、Q510-A01—A02、Q511-A01—A03、Q512-A01—A02、Q513-A01—A02），
/// R03 同日节、重墨 V2 逐字稿同日表；Q520、Q522 维持现状（核对用例在 B41cStealthCheckTests、B41cLeaderTests）。
/// 文字在内核 <see cref="ShellAim"/>、<see cref="ShellKeyHelp"/>；网页壳接线按源码核对（测试工程不引用网页壳）。
/// </summary>
public class BM3AimAnswerTests : TestBase
{
    private static string Root()
    {
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "doc", "策划案"))) dir = dir.Parent;
        Assert.That(dir, Is.Not.Null, "找不到仓库根目录");
        return dir!.FullName;
    }

    private static string Web(string relative) => File.ReadAllText(Path.Combine(Root(), "web", "Survival.Web", relative)).Replace("\r\n", "\n");

    private static string Verbatim() => File.ReadAllText(Path.Combine(Root(), "doc", "策划案", "重墨像素风界面与卡片 v0.9", "重墨 V2 开工裁定与界面逐字稿.md"));

    private static string Body(string src, string head)
    {
        var at = src.IndexOf(head, StringComparison.Ordinal);
        Assert.That(at, Is.GreaterThanOrEqualTo(0), head);
        var body = src.Substring(at);
        return body.Substring(0, body.IndexOf("\n    }", StringComparison.Ordinal));
    }

    // ───────────── Q509 帮助行固定泛称 ─────────────

    /// <summary>Q509-A01：帮助表战斗区第四行「执行（当前指令） Enter」、说明不变、无花括号；真实按钮仍「执行{真实指令名}」。</summary>
    [Test]
    public void Q509_A01_HelpRowGenericLabel()
    {
        var battle = ShellKeyHelp.Groups().Single(g => g.Title == "战斗").Rows;
        Assert.That(battle.Count, Is.EqualTo(4));
        Assert.That(battle[3].Label, Is.EqualTo("执行（当前指令） Enter"));
        Assert.That(battle[3].Note, Is.EqualTo("提交当前指令与已指定的合法目标。"));
        Assert.That(ShellKeyHelp.Groups().SelectMany(g => g.Rows).Select(r => r.Label), Has.None.Contain("{").And.None.Contain("}"));
        Assert.That(ShellAim.ConfirmLabel("拳击"), Is.EqualTo("执行拳击"), "真实按钮取所选指令名");
        var panel = Web("Components/BattlePanel.razor");
        Assert.That(panel, Does.Contain("@ShellAim.ConfirmLabel(st.Name)<EnterCap On=\"@st.Ok\" />"), "真实按钮与 Enter 键帽同一按钮、取真实指令名");
    }

    /// <summary>Q509-A01：标签与说明逐字出自逐字稿 Q509 表。</summary>
    [Test, Category("DesignSync")]
    public void Q509_A01_Verbatim()
    {
        var doc = Verbatim();
        Assert.That(doc, Does.Contain("| 帮助键表“战斗”区第四行标签（Q509） | 执行（当前指令） Enter |"));
        Assert.That(doc, Does.Contain("| 该行说明（保持） | 提交当前指令与已指定的合法目标。 |"));
    }

    // ───────────── Q510、Q511 新瞄准初始化与同一明确目标 ─────────────

    /// <summary>
    /// Q510-A01、A02，Q511-A01：新瞄准只初始化一次——合法焦点敌人 → 合法鼠标所在敌人 → 不指定；不取旧悬停缓存（BattleHover）、不默认首敌；
    /// 读数期间已有明确指定不覆盖；群体不初始化单个目标；只设目标不提交（无执行、无扣费）。
    /// </summary>
    [Test]
    public void Q510_Q511_InitOrder()
    {
        var g = Web("Services/GameSession.cs");
        var pending = g.Substring(g.IndexOf("public bool AimInitPending =>", StringComparison.Ordinal));
        pending = pending.Substring(0, pending.IndexOf('\n'));
        Assert.That(pending, Does.Contain("BattleAiming && !AreaAction && _aimInitCtx != AimCtx()"), "每个瞄准上下文只一次；群体不硬选单个");
        var init = Body(g, "public void InitAimTarget(string ctx, int? focus, int? mouse)");
        Assert.That(init, Does.Contain("ctx != AimCtx() || _aimInitCtx == ctx) return;"), "上下文已变或已初始化不再做");
        Assert.That(init, Does.Contain("_aimInitCtx = ctx;\n        if (BattleAimTarget != null) return;"), "读数期间已有真实指定不覆盖");
        Assert.That(init, Does.Contain("int? pick = focus is int f && set.Contains(f) ? f : mouse is int m && set.Contains(m) ? m : null;"), "焦点优先，再鼠标，都不合法不指定");
        Assert.That(init, Does.Not.Contain("BattleHover").And.Not.Contain("First()").And.Not.Contain("FirstOrDefault"), "不取旧悬停缓存、不默认首敌");
        Assert.That(init, Does.Not.Contain("Execute").And.Not.Contain("ClickBattleTarget").And.Not.Contain("BattleAimSubmit"), "只初始化预测，不提交");
    }

    /// <summary>Q510-A01、A02：页面读数——焦点取 document.activeElement 所在敌方锚点；鼠标取最近真实指针位置的当下最上层元素（被更高层遮住取不到）；触屏不记；inert 不算。</summary>
    [Test]
    public void Q510_A01_A02_PageProbe()
    {
        var js = Web("wwwroot/js/app.js");
        var at = js.IndexOf("window.survivalAim = (function () {", StringComparison.Ordinal);
        Assert.That(at, Is.GreaterThan(0));
        var mod = js.Substring(at, js.IndexOf("})();", at, StringComparison.Ordinal) - at);
        Assert.That(mod, Does.Contain("el.closest('.bfs .anc.foe[id^=\"foe-\"]')").And.Contain("a.closest('[inert]')"));
        Assert.That(mod, Does.Contain("if (ev.pointerType === 'touch') { pt = null; return; }"), "触屏不伪造悬停");
        Assert.That(mod, Does.Contain("if (!ev.relatedTarget) pt = null;"), "指针离开窗口后为空");
        Assert.That(mod, Does.Contain("var focus = ae && ae !== document.body ? foeOf(ae) : null;"));
        Assert.That(mod, Does.Contain("var mouse = pt ? foeOf(document.elementFromPoint(pt.x, pt.y)) : null;"), "按当下最上层元素，遮住时取不到");
        var screen = Web("Components/BattleScreen.razor");
        var probe = Body(screen, "private async Task InitAim()");
        Assert.That(probe, Does.Contain("if (!G.AimInitPending) return;"));
        Assert.That(probe, Does.Contain("await JS.InvokeAsync<int?[]>(\"survivalAim.probe\")"));
        Assert.That(probe, Does.Contain("G.InitAimTarget(ctx, "));
        Assert.That(Body(screen, "protected override async Task OnAfterRenderAsync(bool firstRender)"), Does.Contain("await InitAim();"), "任一入口进入瞄准后渲染完即读一次");
    }

    /// <summary>
    /// Q511-A02、A03：瞄准后焦点主动到另一敌人即更新同一目标（不合法也记下、显示原因）；焦点留在锚点按 Enter／空格走同一瞄准确认，
    /// 不按旧原生激活改打焦点所在的另一只；未瞄准仍只查看；点亮起的目标仍直接提交、无二次确认；请求去重沿原 token。
    /// </summary>
    [Test]
    public void Q511_FocusAndEnterConsistent()
    {
        var g = Web("Services/GameSession.cs");
        var focus = Body(g, "public void FocusBattleTarget(int index)");
        Assert.That(focus, Does.Contain("if (State == null || S.Battle == null || !BattleAiming || AreaAction) return;"));
        Assert.That(focus, Does.Contain("SetAimTarget(index);"));
        Assert.That(focus, Does.Not.Contain("Execute").And.Not.Contain("ClickBattleTarget"), "聚焦只指定，不提交");
        var screen = Web("Components/BattleScreen.razor");
        Assert.That(screen, Does.Contain("@onmouseleave=\"() => G.HoverBattleTarget(null)\" @onfocus=\"() => G.FocusBattleTarget(idx)\">"), "焦点真实到达敌方锚点即明确指定");
        var key = Body(screen, "private async Task FoeKey(KeyboardEventArgs ev, int idx, string token, bool locked)");
        Assert.That(key, Does.Contain("if (G.BattleAiming) { if (!locked && !ev.Repeat) await G.BattleAimSubmit(token); return; }"), "瞄准中 Enter 与按钮、全局回车同一确认");
        Assert.That(key, Does.Contain("await ClickFoe(idx, token, locked);"), "未瞄准仍只选中查看");
        var click = Body(screen, "private async Task ClickFoe(int idx, string token, bool locked)");
        Assert.That(click, Does.Contain("await G.ClickBattleTarget(idx, token);"), "鼠标点亮起目标仍直接提交");
        var submit = Body(g, "public async Task BattleAimSubmit(string token)");
        Assert.That(submit, Does.Contain("var st = BattleAimConfirm();").And.Contain("if (!st.Ok) return;"), "无明确合法目标或不可提交时 Enter 不绕过禁用");
        // 焦点在其他具体控件（取消、菜单、物资等按钮）上：app.js 仍交给其原生语义
        var js = Web("wwwroot/js/app.js");
        Assert.That(js, Does.Contain("var onCtl = document.activeElement && document.activeElement.closest && document.activeElement.closest('button, [role=button]');"));
    }

    // ───────────── Q512 无目标指引引用实际按钮 ─────────────

    /// <summary>Q512-A01、A02：模板逐字；结束行动剩 1AP 的示例；其他无目标指令沿真实按钮原文；不含 Enter 键帽。</summary>
    [Test]
    public void Q512_A01_A02_Texts()
    {
        Assert.That(ShellAim.ConsoleConfirmText(true, "结束行动", 1), Is.EqualTo("确认结束行动 · 剩余1AP清零"));
        Assert.That(ShellAim.ConsoleGuide(ShellAim.ConsoleConfirmText(true, "结束行动", 1)), Is.EqualTo("在操作台点“确认结束行动 · 剩余1AP清零”才提交；ESC 取消。"));
        Assert.That(ShellAim.ConsoleGuide(ShellAim.ConsoleConfirmText(true, "结束行动", 3)), Is.EqualTo("在操作台点“确认结束行动 · 剩余3AP清零”才提交；ESC 取消。"));
        Assert.That(ShellAim.ConsoleGuide(ShellAim.ConsoleConfirmText(false, "防御", 1)), Is.EqualTo("在操作台点“确认 · 防御 · 1AP”才提交；ESC 取消。"));
        Assert.That(ShellAim.ConsoleGuide("x"), Does.Not.Contain("Enter"));
    }

    /// <summary>Q512-A01、A02：按钮与指引同一命名结果（GameSession.BattleConsoleConfirmText）；没有该按钮（敌方瞄准、对白锁、未轮到）时不显示指引；不再由预测标题＋AP 另拼。</summary>
    [Test]
    public void Q512_SameSource()
    {
        var g = Web("Services/GameSession.cs");
        var src = Body(g, "public string? BattleConsoleConfirmText()");
        Assert.That(src, Does.Contain("BattleAction == null || BattleAiming || DialogueLocked || !E.AwaitingPlayer(S)) return null;"), "没有对应按钮时不伪造");
        Assert.That(src, Does.Contain("ShellAim.ConsoleConfirmText(sel.Action == \"end_turn\", BattleShortName(sel), E.PreviewCommand(S, CurrentBattleCommand()).Ap)"));
        var screen = Web("Components/BattleScreen.razor");
        Assert.That(screen, Does.Contain("aria-keyshortcuts=\"Enter\" @onclick=\"() => Exec(token)\">@G.BattleConsoleConfirmText()<EnterCap On=\"@(p.Ok && !locked)\" /></button>"), "按钮业务文字与指引同源，键帽另在按钮里");
        Assert.That(screen, Does.Not.Contain("$\"确认结束行动 · 剩余{p.Ap}AP清零\""), "按钮不再自拼");
        Assert.That(screen, Does.Contain("private static string ShortName(BattleCommandOption o) => GameSession.BattleShortName(o);"));
        var panel = Web("Components/BattlePanel.razor");
        Assert.That(panel, Does.Contain("else if (G.BattleConsoleConfirmText() is string confirmText) { <div class=\"under\">@ShellAim.ConsoleGuide(confirmText)</div> }"));
        Assert.That(panel, Does.Not.Contain("ConsoleGuide(p.Title"), "不由预测标题与 AP 另拼");
    }

    /// <summary>Q512：模板与结束行动示例逐字出自逐字稿 Q512 表。</summary>
    [Test, Category("DesignSync")]
    public void Q512_Verbatim()
    {
        var doc = Verbatim();
        Assert.That(doc, Does.Contain("| 无目标类指令指引（Q512） | 在操作台点“{确认按钮业务文字}”才提交；ESC 取消。 |"));
        Assert.That(doc, Does.Contain("| 结束行动确认按钮业务文字（保持） | 确认结束行动 · 剩余{剩余AP}AP清零 |"));
        Assert.That(doc, Does.Contain($"| 剩余1AP时的指引示例 | {ShellAim.ConsoleGuide(ShellAim.ConsoleConfirmText(true, "结束行动", 1))} |"));
    }

    // ───────────── Q513 预测伤害行前移 ─────────────

    /// <summary>Q513-A01：每目标「伤害（未暴击）」在「命中能力构成」之前；构成为空不显示、无空行；其余行与相对顺序不变。</summary>
    [Test]
    public void Q513_A01_DamageBeforeAccuracyBreakdown()
    {
        var panel = Web("Components/BattlePanel.razor");
        int facts = panel.IndexOf("<span>暴击（命中后）</span>", StringComparison.Ordinal),
            dmg = panel.IndexOf("<span>伤害（未暴击）</span>", StringComparison.Ordinal),
            acc = panel.IndexOf("<span>命中能力构成</span>", StringComparison.Ordinal),
            crit = panel.IndexOf("<span>伤害（暴击×", StringComparison.Ordinal),
            hp = panel.IndexOf("<span>目标健康</span>", StringComparison.Ordinal);
        Assert.That(new[] { facts, dmg, acc, crit, hp }, Is.All.GreaterThan(0));
        Assert.That(facts < dmg && dmg < acc && acc < crit && crit < hp, Is.True, "目标／命中／暴击摘要 → 未暴击伤害 → 命中能力构成 → 暴击伤害 → 目标健康");
        Assert.That(panel, Does.Contain("@if (t.AccuracyText != \"\") { <div><span>命中能力构成</span><b>@t.AccuracyText</b></div> }"), "构成为空不显示，不留空块");
        Assert.That(Regex.Matches(panel, "<span>伤害（未暴击）</span>").Count, Is.EqualTo(1));
    }
}
