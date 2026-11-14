namespace Survival.Core.Tests;

/// <summary>
/// BM7a（R238：S14 文首「2026-11-14 Q644—Q655」§Q644／Q645、§Q655 与验收 Q645-A01、Q655-A01；02 Q645、Q655）：
/// 加工菜单来源行消失后的焦点落点、游戏菜单键盘层级（Tab 首尾循环、关闭回原位置、按住 ESC 不连退）。网页壳接线按源码核对（同 BM5b1 壳测试）。
/// Q644（移入锅说明句取消）的内核与按钮断言在 <see cref="BM5b1FacMenuTests"/>。
/// </summary>
public class BM7aMenuFocusTests
{
    /// <summary>Q645-A01：焦点所在来源行消失——按原显示顺序回下一条、再上一条仍可选择的行，都没有回本层标题；同一稳定键还在（重排／重建）回它本身；只移焦点。</summary>
    [Test]
    public void Q645_A01_SourceRowGone_NextThenPrevThenTitle()
    {
        var menu = BM7aBattleTests.Web("Components", "FireMenu.razor");
        Assert.That(menu, Does.Contain("<div @key=\"it.InstanceId\" class=\"fm-src-row @(on ? \"on\" : \"\")\" role=\"button\" tabindex=\"0\" aria-pressed=\"@on\" data-src-key=\"@it.InstanceId\""), "可选来源行按实例稳定键渲染");
        Assert.That(menu, Does.Contain("<div class=\"fm-src-row no\" @key=\"iid\" data-src-key=\"@iid\" aria-disabled=\"true\""), "灰行同样有稳定键（参与原显示顺序，但不作落点）");
        Assert.That(menu, Does.Contain("data-tab-trap=\"@(G.FireMenuTop ? GameSession.FireMenuTitleId : null)\""), "只在加工菜单为最高层时修复（沿 Q602 标记）");

        var js = BM7aBattleTests.Web("wwwroot", "js", "app.js");
        var rep = BM7aBattleTests.Slice(js, "function repairSrc() {", "var srcTimer = 0;");
        Assert.That(rep, Does.Contain("if (!lost()) {"), "焦点仍在有效位置（别的输入框、更高层）不抢");
        Assert.That(rep, Does.Contain("if (!trap || !shown(trap) || (trap.closest && trap.closest('[inert]'))) return false;"), "菜单被盖住时等它恢复为最上层再修");
        Assert.That(rep, Does.Contain("if (!box) { src = null; return false; }"), "整个来源区随内容页没了：交给 Q604 原规则");
        Assert.That(rep, Does.Contain("if (!r.classList.contains('no') && free(r)) map[srcKey(r)] = r;"), "灰禁用行不作落点");
        Assert.That(rep, Does.Contain("pick = map[src.key] || null;"), "同一稳定键还在：不误当消失");
        Assert.That(rep, Does.Contain("for (var i = at + 1; !pick && i < order.length; i++)").And.Contain("for (var j = at - 1; !pick && j >= 0; j--)"), "先向后、再向前");
        Assert.That(rep, Does.Contain("if (!pick) pick = document.getElementById(trap.getAttribute('data-tab-trap'));"), "都没有回本层标题");
        Assert.That(rep, Does.Not.Contain("click()").And.Not.Contain("invokeMethodAsync"), "只恢复焦点：不选为来源、不改数量、不投料");
        var fin = BM7aBattleTests.Slice(js, "var src = null;", "}, true);");
        Assert.That(fin, Does.Contain("'.fire-menu .fm-src-row[data-src-key]:not(.no)'"), "只记焦点确在可选择来源行上的情形");
        Assert.That(js, Does.Contain("srcTimer = setTimeout(function () { srcTimer = 0; repairSrc(); }, 60);"), "让 Q604 主动操作成功回工位先做");
    }

    /// <summary>Q655-A01：游戏菜单／子页 Tab 与反向首尾只在最高层循环；关闭回原合法位置、原位消失安全回退；按住 ESC 不连退；同键不点穿；权限不放宽。</summary>
    [Test]
    public void Q655_A01_MenuTabTrap_RestoreOnClose()
    {
        var js = BM7aBattleTests.Web("wwwroot", "js", "app.js");
        var menuBranch = BM7aBattleTests.Slice(js, "if (document.querySelector('.menu-modal')) {", "// B34b（S14 Q572）");
        Assert.That(menuBranch, Does.Contain("var mm = topMenu();").And.Contain("if (mm) trapTab(ev, mm, mm.querySelector('h2[tabindex]'));"), "Tab 只在最上层菜单里循环");
        Assert.That(menuBranch, Does.Contain("if (ev.repeat) { ev.preventDefault(); return; }"), "按住 ESC 只收一层");
        Assert.That(js, Does.Contain("if (k === 'Escape' && ev.repeat && window.__survivalMenuEsc) { ev.preventDefault(); return; }"), "菜单收起后同一次按住的 ESC 不落到背后");
        var trap = BM7aBattleTests.Slice(js, "function trapTab(ev, root, safe) {", "function topMenu()");
        Assert.That(trap, Does.Contain("return !el.disabled && el.tabIndex >= 0 && shown(el) && !(el.closest && el.closest('[inert], [hidden]'));"), "跳过禁用、隐藏与 inert；禁用存档不为 Tab 放宽");
        Assert.That(js, Does.Contain("trapTab(ev, trap, document.getElementById(trap.getAttribute('data-tab-trap')));"), "加工菜单 Q602 循环共用同一逻辑");
        foreach (var f in new[] { "MenuOverlay.razor", "ManualSavePage.razor" })
            Assert.That(BM7aBattleTests.Web("Components", f), Does.Contain("<h2 tabindex=\"-1\">"), f + "：本层标题可作安全落点（不进 Tab 序列）");

        var restore = BM7aBattleTests.Slice(js, "function restore(target) {", "new MutationObserver(function () {");
        Assert.That(restore, Does.Contain("if (document.querySelector('.menu-modal') || !lost()) return;"), "别的层或代码已放好焦点时不抢");
        Assert.That(restore, Does.Contain("if (target && free(target) && !covered(target)) { put(target); return; }"), "回原合法控件");
        Assert.That(restore, Does.Contain("if (repairSrc()) return;").And.Contain("var f = menuFallback();"), "原控件失效：来源行按 Q645，其余回现存安全标题／菜单入口");
        Assert.That(restore, Does.Not.Contain("click()").And.Not.Contain("invokeMethodAsync"), "只恢复焦点，不执行原控件");
        var obs = BM7aBattleTests.Slice(js, "new MutationObserver(function () {", ".observe(document.body");
        Assert.That(obs, Does.Contain("Date.now() - preEscAt < 1500 ? preEsc : null"), "ESC 打开前被 blur 的原焦点也记下");
        Assert.That(obs, Does.Contain("if (t) restore(t); else laterSrc();"), "打开前没有键盘焦点时不硬塞焦点");
        // 原规则保持：菜单打开时焦点进菜单底部关闭键；手动存档子页返回仍按 BM2 Q500 回关闭键（与 Q655 的关系另问）
        var mo = BM7aBattleTests.Web("Components", "MenuOverlay.razor");
        Assert.That(mo, Does.Contain("if (firstRender || backFromManual)"));
    }
}
