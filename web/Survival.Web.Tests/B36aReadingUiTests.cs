using Bunit;
using Survival.Core.State;
using Survival.Web.Components;

namespace Survival.Web.Tests;

/// <summary>
/// B36a M1：阅读页通用骨架「目录左、内容右」（02 Q812—Q814、Q822）用在行动日志、生存提示。
/// 起点只造日志条目（内核 LogEntry），之后都点组件里真实的目录按钮，断玩家看到的目录、正文与副标。
/// </summary>
public class B36aReadingUiTests : WebTestBase
{
    private const int Day = 24 * 60;

    private void SeedLog()
    {
        S.Log.Clear();
        S.Log.Add(new LogEntry { Minute = 8 * 60 + 30, Kind = "Info", Text = "甲" });            // 第1日 08:30
        S.Log.Add(new LogEntry { Minute = 9 * 60, Kind = "Info", Text = "乙" });                 // 第1日 09:00
        S.Log.Add(new LogEntry { Minute = Day + 7 * 60 + 15, Kind = "Info", Text = "丙" });      // 第2日 07:15
    }

    private static string[] Texts(IRenderedComponent<LogOverlay> cut, string sel) =>
        cut.FindAll(sel).Select(x => x.TextContent.Trim()).ToArray();

    /// <summary>
    /// 02 Q814 A：左栏「第{世界日}日」，最新一天在上并默认选中；右栏该日记录倒序。Q903 C：右栏每条时间只写 HH:mm（去掉同条「第N日」）。
    /// 原副标「最近{n}条 · 最新在上」与真实条数保留（逐字稿 2026-11-16 B36 逐字表）。
    /// </summary>
    [Test]
    public void Log_DayDirectory_NewestDefault_RowsTimeOnly()
    {
        SeedLog();
        G.OpenTopPanel("log");
        var cut = Ctx.RenderComponent<LogOverlay>();

        Assert.That(Texts(cut, "[data-pt=log-day]"), Is.EqualTo(new[] { "第2日", "第1日" }), "最新一天在上");
        Assert.That(cut.Find("[data-pt=log-day].on").TextContent.Trim(), Is.EqualTo("第2日"), "默认选最新一天");
        Assert.That(Texts(cut, "[data-pt=log-row] .t"), Is.EqualTo(new[] { "07:15" }), "只列所选那天；时间只写 HH:mm");
        Assert.That(cut.Find(".sub-head .sub").TextContent, Is.EqualTo("最近3条 · 最新在上"));
        Assert.That(cut.FindAll("[data-pt=log-empty]"), Is.Empty, "有记录不出空态");

        cut.Find("[data-pt=log-day][data-day='1']").Click();
        Assert.That(Texts(cut, "[data-pt=log-row] .t"), Is.EqualTo(new[] { "09:00", "08:30" }), "该日记录最新在上");
        Assert.That(cut.FindAll("[data-pt=log-row]").Select(r => r.QuerySelectorAll("span")[1].TextContent).ToArray(), Is.EqualTo(new[] { "乙", "甲" }));
        Assert.That(cut.Markup, Does.Not.Contain("第1日 09:00"), "同条不再重复写第N日");
    }

    /// <summary>02 Q814 A、逐字稿 §2 P08f：没有记录时显示空态「还没有行动记录。」，不出目录（配对：上一条有记录时不出空态）。</summary>
    [Test]
    public void Log_Empty_ShowsEmptyText()
    {
        S.Log.Clear();
        G.OpenTopPanel("log");
        var cut = Ctx.RenderComponent<LogOverlay>();
        Assert.That(cut.Find("[data-pt=log-empty]").TextContent.Trim(), Is.EqualTo("还没有行动记录。"));
        Assert.That(cut.FindAll("[data-pt=log-split]"), Is.Empty);
    }

    /// <summary>Q814 A「最新日默认」：关页重开回最新一天，不残留上次选的日；同页换日不压返回链（关闭仍是「关闭」，Q821）。</summary>
    [Test]
    public void Log_Reopen_BackToNewest()
    {
        SeedLog();
        G.OpenTopPanel("log");
        var cut = Ctx.RenderComponent<LogOverlay>();
        cut.Find("[data-pt=log-day][data-day='1']").Click();
        Assert.That(cut.Find("[data-pt=log-day].on").TextContent.Trim(), Is.EqualTo("第1日"), "起点：已换到第1日");
        Assert.That(cut.Find("[data-pt=log-close]").TextContent, Does.StartWith("关闭"), "换目录不算一层，直接开的仍写关闭");

        cut.Find("[data-pt=log-close]").Click();
        Assert.That(G.Panel, Is.Null);
        G.OpenTopPanel("log");
        cut.Render();
        Assert.That(cut.Find("[data-pt=log-day].on").TextContent.Trim(), Is.EqualTo("第2日"));
    }

    /// <summary>
    /// 02 Q822 设计A、文案A：左栏两目录「当前状况」「当前建议」，新开默认当前状况；建议仍是全表且当前条高亮。
    /// Q903 C：页头不再有副标「求生建议，不消耗时间」，也不换成「当前建议」副标。
    /// </summary>
    [Test]
    public void Tips_TwoDirectories_DefaultNow_NoSubtitle()
    {
        G.OpenTopPanel("tips");
        var cut = Ctx.RenderComponent<TipsOverlay>();

        Assert.That(cut.FindAll(".read-nav-item").Select(b => b.TextContent.Trim()).ToArray(), Is.EqualTo(new[] { "当前状况", "当前建议" }));
        Assert.That(cut.Find(".read-nav-item.on").TextContent.Trim(), Is.EqualTo("当前状况"), "默认当前状况");
        Assert.That(cut.Find("[data-pt=tips-now] h3").TextContent, Is.EqualTo("当前状况"));
        Assert.That(cut.FindAll("[data-pt=tips-list]"), Is.Empty, "默认不显示建议表");
        Assert.That(cut.FindAll(".sub-head .sub"), Is.Empty, "没有副标");
        Assert.That(cut.Markup, Does.Not.Contain("求生建议，不消耗时间"));

        cut.Find("[data-pt=tips-tab-list]").Click();
        Assert.That(cut.Find("[data-pt=tips-list] h3").TextContent, Is.EqualTo("当前建议"));
        Assert.That(cut.FindAll("[data-pt=tips-now]"), Is.Empty, "右栏只显示所选目录");
        var rows = cut.FindAll("[data-pt=tip-row]");
        Assert.That(rows.Count, Is.GreaterThan(1), "辅助：全表不止一条");
        Assert.That(rows.Count(r => r.ClassList.Contains("now")), Is.EqualTo(1), "当前相关一条高亮");
    }

    /// <summary>Q822 A「默认当前状况」：选了当前建议后关页重开，回当前状况。</summary>
    [Test]
    public void Tips_Reopen_BackToNow()
    {
        G.OpenTopPanel("tips");
        var cut = Ctx.RenderComponent<TipsOverlay>();
        cut.Find("[data-pt=tips-tab-list]").Click();
        Assert.That(cut.Find(".read-nav-item.on").TextContent.Trim(), Is.EqualTo("当前建议"), "起点");
        cut.Find("[data-pt=tips-close]").Click();
        G.OpenTopPanel("tips");
        cut.Render();
        Assert.That(cut.Find(".read-nav-item.on").TextContent.Trim(), Is.EqualTo("当前状况"));
    }
}
