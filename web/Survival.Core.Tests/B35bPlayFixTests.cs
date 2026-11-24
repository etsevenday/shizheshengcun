namespace Survival.Core.Tests;

/// <summary>
/// B35b 试玩修复 M1155（开发记录 doc/开发记录/B35b 制作页三列与来源侧页.md「试玩修复」）：
/// 本地 Release 包场景 b51c2a1-station-take @1280x720／1600x900 里，陶窑／净水台工作区只盖①②（被动面板 passive-panel）时，
/// 内右列“本次制作”整列滚动，投料按钮与「全部取出」滚出面板、落在地面卡行上被盖住。
/// 规则：S14 FACPROC-A11「主要动作与关闭可见，长内容内部滚动，不卡屏外、不遮唯一确认」。
/// 实机可见性由试玩场景 b51c2a1-station-take 的 watch（pt=station-load、[data-station-take-all]）三档核对；这里只断样式接线。
/// </summary>
public class B35bPlayFixTests
{
    private static string Rule(string css, string selector)
    {
        var at = css.IndexOf(selector + " {", StringComparison.Ordinal);
        Assert.That(at, Is.GreaterThanOrEqualTo(0), selector);
        var end = css.IndexOf('}', at);
        return css.Substring(at, end - at);
    }

    [Test]
    public void PassivePanel_StationRunColumn_ActionsPinned_FactsScroll()
    {
        var css = SourceText.Web(Path.Combine("wwwroot", "css", "cards.css"));
        const string P = ".center-scroll.four.passive-panel .craft-run";
        // 列本身不整列滚动（整列滚动会把操作滚出面板）
        Assert.That(Rule(css, P), Does.Contain("overflow: hidden"));
        // 投料栏占列内剩余高度、可收缩
        Assert.That(Rule(css, P + " > .craft-go[data-station-go]"), Does.Contain("flex: 1 1 auto").And.Contain("min-height: 0"));
        // 说明与预计在列内自己滚动
        Assert.That(Rule(css, P + " > .craft-go[data-station-go] .cg-facts"), Does.Contain("overflow-y: auto").And.Contain("min-height: 0"));
        // 投料批数／投料／全部取出不收缩、固定在列底
        Assert.That(Rule(css, P + " > .craft-go[data-station-go] .cg-act"), Does.Contain("flex: none"));
    }
}
