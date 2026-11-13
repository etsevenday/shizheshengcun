using Survival.Core.Engine;

namespace Survival.Core.Tests;

/// <summary>
/// B34a（S14 Q570、Q528、Q529、Q530，P05 逐字稿）：下方「战斗信息／战报」阅读区的文字规则与接线。
/// </summary>
public class B34aBattleInfoUiTests
{
    private static string Root()
    {
        var d = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (d != null && !File.Exists(Path.Combine(d.FullName, "web", "Survival.sln"))) d = d.Parent;
        return d!.FullName;
    }

    private static string Web(string relative) => File.ReadAllText(Path.Combine(Root(), "web", "Survival.Web", relative)).Replace("\r\n", "\n");

    [Test]
    public void Recent_LastFive_InOrder_NoPadding()
    {
        var log = Enumerable.Range(0, 7).Select(i => $"行{i}").ToList();
        var r = BattleInfoCopy.Recent(log);
        Assert.That(r.Select(x => x.Index), Is.EqualTo(new[] { 2, 3, 4, 5, 6 }), "最新在最后，带本场下标");
        Assert.That(r[^1].Text, Is.EqualTo("行6"));
        Assert.That(BattleInfoCopy.Recent(log.Take(2).ToList()).Count, Is.EqualTo(2), "不足 5 条不补假条");
        Assert.That(BattleInfoCopy.Recent(new List<string>()), Is.Empty);
    }

    [Test]
    public void Counts_FollowTranscript()
    {
        Assert.That(BattleInfoCopy.RecentCount(5, 12, false), Is.EqualTo("最近5条 · 共12条"));
        Assert.That(BattleInfoCopy.RecentCount(5, 80, true), Is.EqualTo("最近5条 · 已保存80条"));
        Assert.That(BattleInfoCopy.LogSubtitle(12, false), Is.EqualTo("本场战斗 · 共12条"));
        Assert.That(BattleInfoCopy.LogSubtitle(80, true), Is.EqualTo("本场战斗 · 已保存80条"));
        Assert.That(BattleInfoCopy.NewRecords(3), Is.EqualTo("有3条新记录"));
        Assert.That(BattleInfoCopy.Affected(2), Is.EqualTo("受影响2个目标"));
    }

    [Test]
    public void Q530_NoTargetHeader_OmitsZeroEnergy()
    {
        Assert.That(BattleInfoCopy.AimNoTargetHeader("2AP", 0.29, 1, 0), Is.EqualTo("2AP · 体力−0.29 · 可选目标1个"));
        Assert.That(BattleInfoCopy.AimNoTargetHeader("1AP", 0, 3, 0), Is.EqualTo("1AP · 可选目标3个"), "消耗为 0 省略体力项");
        Assert.That(BattleInfoCopy.AimNoTargetHeader("1AP", 0.0001, 3, 0), Does.Not.Contain("−0"), "按原精度显示为 0 时也省略，不写负零");
        Assert.That(BattleInfoCopy.AimNoTargetHeader("2AP", 0.5, 2, 2), Does.EndWith(" · 冷却2轮"));
    }

    [Test]
    public void Q570_InfoArea_BelowConsole_CollapsedByDefault_TwoEntries()
    {
        var bs = Web("Components/BattleScreen.razor");
        Assert.That(bs, Does.Contain("id=\"bfs-infoarea\""), "下方阅读区");
        Assert.That(bs, Does.Contain("id=\"@(\"bi-entry-\" + page)\""), "主画面两个入口（战斗信息／战报）");
        Assert.That(bs, Does.Contain("aria-expanded=\"@(G.BattleInfoPage == page)\""), "默认收起：未选页时不展开");
        Assert.That(bs, Does.Not.Contain("class=\"bfs-log\""), "右侧战报列撤销");
        Assert.That(bs.IndexOf("bfs-infoarea", StringComparison.Ordinal), Is.GreaterThan(bs.IndexOf("bfs-feed", StringComparison.Ordinal)), "阅读区在底部反馈位之下");
        Assert.That(Web("Components/BattlePanel.razor"), Does.Not.Contain("data-aim-confirm"), "执行与目标控制不进阅读区");
    }

    [Test]
    public void Q529_NoTargetCommand_FocusesConfirm()
    {
        var gs = Web("Services/GameSession.cs");
        Assert.That(gs, Does.Contain("BattleConfirmFocusPending = !BattleAiming && !BattleAllyAiming;"));
    }

    [Test]
    public void Esc_InfoAreaCollapsesBeforeAimCancel()
    {
        var gs = Web("Services/GameSession.cs");
        var drawer = gs.IndexOf("if (BattleInfoPage != null) { CollapseBattleInfo(); return; }", StringComparison.Ordinal);
        Assert.That(drawer, Is.GreaterThan(0));
        Assert.That(Web("Services/GameSession.Keys.cs"), Does.Contain("EscOwner.BattleInfo"));
    }
}
