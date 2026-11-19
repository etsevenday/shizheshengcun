namespace Survival.Core.Tests;

/// <summary>
/// B51c2b 界面接线（S11／S07 §Q925 B：点设施物品卡本体打开数量窗与详情、卡本体可拖出；§Q870 B 快捷箭头）。
/// 试玩修复 B1：箭头扩大点击区（::after）原以整张卡为定位基准、盖满卡身——这是浏览器排版结果，bUnit 不算 CSS 布局、照不到，
/// 只能读样式源码锁住「箭头自身作定位基准、扩大区只贴箭头一圈」这条规则；实际点击与起拖仍靠本地试玩确认。
/// </summary>
public class B51c2bWiringTests
{
    private static string Web(string relative) => SourceText.Web(relative);

    [Test]
    public void FacilityCardArrow_HitAreaStaysOnArrow()
    {
        var css = Web("wwwroot/css/app.css");
        Assert.That(css, Does.Contain(".irow.fi-card .card-arrow { position: relative; right: auto; bottom: auto; flex: 0 0 auto; }"),
            "箭头自身作 ::after 的定位基准");
        Assert.That(css, Does.Contain(".irow.fi-card .card-arrow::after { inset: -3px; }"), "扩大点击区只贴着箭头");
        Assert.That(css, Does.Not.Contain(".irow.fi-card .card-arrow { position: static"), "试玩修复 B1：static 会让 ::after 盖满整张卡");
        // 卡本体是起拖源（drag.js 在按钮上按下不起拖，卡身不是按钮）
        var card = Web("Components/FacilityItemCard.razor");
        Assert.That(card, Does.Contain("<div class=\"irow fi-card").And.Contain("data-drag=\"@($\"card:{key}\")\""));
    }
}
