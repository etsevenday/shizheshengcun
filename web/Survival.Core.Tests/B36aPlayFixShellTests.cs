namespace Survival.Core.Tests;

/// <summary>
/// B36a 本地 Release 包试玩修复：只测 bUnit 渲染不到的浏览器脚本与样式（测试规则第 9 条：SourceText 去注释后断言）。
/// 组件侧的接线（容器标记、右栏预估名、左栏定位调用）见 Survival.Web.Tests 的 B36aPlayFixUiTests。
/// </summary>
public class B36aPlayFixShellTests
{
    private static string Web(string relative) => SourceText.Web(relative);

    private static string Block(string css, string head)
    {
        var at = css.IndexOf(head, StringComparison.Ordinal);
        Assert.That(at, Is.GreaterThanOrEqualTo(0), head);
        var end = css.IndexOf('}', at);
        return css.Substring(at, end - at);
    }

    private static string LockEnd()
    {
        var js = Web("wwwroot/js/app.js");
        var end = js.Substring(js.IndexOf("progLockEnd: function (pref) {", StringComparison.Ordinal));
        return end.Substring(0, end.IndexOf("\n  },", StringComparison.Ordinal));
    }

    /// <summary>
    /// B2（02 Q1123 A「还选着刚才那一档，光标停在那个时长按钮上，不停在开始按钮上」）：鼠标点开始键时焦点先归到休息页容器（B43 鼠标归位），
    /// 开工结束时 progLockEnd 不把容器或开始键当作「已有新的明确焦点」，也不把焦点还给它们，改落原页落点（所选时长）。
    /// 场景 tools/playtest/scenarios/b36a-rest.json 第 75 步在真浏览器里断同一件事。
    /// </summary>
    [Test]
    public void LockEnd_NoReturnElements_NeitherKeptNorRestored()
    {
        var end = LockEnd();
        Assert.That(end, Does.Contain("if (c.hasAttribute && c.hasAttribute('data-lock-noreturn')) return false;"), "不还给标了 noreturn 的元素");
        Assert.That(end, Does.Contain("if (ae && ae !== document.body && ae !== prev && !(ae.hasAttribute && ae.hasAttribute('data-lock-noreturn'))) return;"),
            "焦点停在 noreturn 元素上不算新的明确焦点");
    }

    /// <summary>D2：左栏定位脚本只滚到可见（最近边对齐，沿 revealNearest），不动焦点。</summary>
    [Test]
    public void RevealQuery_ScrollsNearest_NoFocus()
    {
        var js = Web("wwwroot/js/app.js");
        var at = js.IndexOf("revealQuery: function (sel) {", StringComparison.Ordinal);
        Assert.That(at, Is.GreaterThanOrEqualTo(0));
        var body = js.Substring(at, js.IndexOf("\n  },", at, StringComparison.Ordinal) - at);
        Assert.That(body, Does.Contain("window.survival.revealNearest(el);"));
        Assert.That(body, Does.Not.Contain(".focus("));
    }

    /// <summary>D1：技能卡卡头「已解锁／未解锁」不折行（1280 档曾折成「已解」「锁」）。</summary>
    [Test]
    public void SkillCard_StateWord_NoWrap()
    {
        var css = Web("wwwroot/css/app.css");
        Assert.That(Block(css, ".growth-node header em {"), Does.Contain("white-space: nowrap;").And.Contain("flex: none;"));
    }

    /// <summary>
    /// D3：意志参考格「达到点亮」是金色描边（B52b .attained：border-color var(--accent)）；所选一张另用浅色外框与底色，
    /// 两种状态样式可区分（不加文字）。
    /// </summary>
    [Test]
    public void PickedCard_DiffersFromAttained()
    {
        var css = Web("wwwroot/css/app.css");
        var attained = Block(css, ".growth-will-ref.attained {");
        Assert.That(attained, Does.Contain("border-color: var(--accent);"));
        var picked = css.Substring(css.IndexOf(".growth-node header em {", StringComparison.Ordinal));
        picked = Block(picked, ".growth-cards .growth-node.picked {");
        Assert.That(picked, Does.Contain("outline: 2px solid var(--ink2-light);").And.Contain("background: var(--ink2-tile-top);"));
        Assert.That(picked, Does.Not.Contain("var(--accent)"), "所选外框不用金色");
    }

    /// <summary>D4：左栏目录项自身作定位容器，按钮里的读屏文字（.sr-only 绝对定位）被左栏裁切，不再撑出页面容器。</summary>
    [Test]
    public void ReadNavItem_ContainsSrOnly()
    {
        var css = Web("wwwroot/css/app.css");
        Assert.That(Block(css, ".read-nav-item {"), Does.Contain("position: relative;"));
    }
}
