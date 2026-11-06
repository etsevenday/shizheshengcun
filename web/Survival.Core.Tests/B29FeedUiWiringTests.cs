namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 B29 界面接线（Q258-268-A07—A09）：测试工程不引用网页壳，这里读组件源码确认入口和清理路径接上了；规则本身由 B29FeedTests 测。
/// </summary>
public class B29FeedUiWiringTests
{
    private static string Web(string relative)
    {
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "doc", "策划案"))) dir = dir.Parent;
        Assert.That(dir, Is.Not.Null, "找不到仓库根目录");
        return File.ReadAllText(Path.Combine(dir!.FullName, "web", "Survival.Web", relative));
    }

    [Test]
    public void Entries_CardCanteenPotLiquid_Party_Gear()
    {
        var card = Web("Components/CardActions.razor");
        Assert.That(card, Does.Contain("m.UseId ?? (m.Instance is ItemInstance fi && e.IsCanteen(fi)"), "普通卡、锅水走 UseId，水壶走实例");
        Assert.That(card, Does.Contain("e.FeedEntry(s, feedId)"));
        Assert.That(Web("Components/PartyPanel.razor"), Does.Contain("G.TogglePartyItems(id)").And.Contain("e.CompanionCarried(s, id)").And.Contain("G.OpenFeed(fe, anchor, id)"));
        Assert.That(Web("Components/GearOverlay.razor"), Does.Contain("e.FeedEntry(s, inst.InstanceId)").And.Contain("G.DropFeed();"));
    }

    [Test]
    public void Pop_HostedOnce_CardPopYields_EscClosesFirst()
    {
        var screen = Web("Components/GameScreen.razor");
        Assert.That(screen, Does.Contain("<FeedPop />"));
        Assert.That(screen.IndexOf("G.CancelFeed()"), Is.LessThan(screen.IndexOf("G.CancelGive()")), "Esc 先收食饮盘");
        Assert.That(Web("Components/CardPop.razor"), Does.Contain("G.FeedPending == null"));
    }

    [Test]
    public void Pop_FourSections_OnlyCoreData_CloseAria()
    {
        var pop = Web("Components/FeedPop.razor");
        Assert.That(pop, Does.Contain("G.E.FeedPanel(G.S, d)"));
        foreach (var part in new[] { "SectionTarget", "SectionSource", "SectionEffect", "SectionConfirm", "CloseAria", "FeedPanelCopy.Cancel", "FeedPanelCopy.Help" })
            Assert.That(pop, Does.Contain(part));
        Assert.That(pop.IndexOf("SectionTarget"), Is.LessThan(pop.IndexOf("SectionSource")));
        Assert.That(pop.IndexOf("SectionSource"), Is.LessThan(pop.IndexOf("SectionEffect")));
        Assert.That(pop.IndexOf("SectionEffect"), Is.LessThan(pop.IndexOf("SectionConfirm")));
    }

    [Test]
    public void Session_DraftClearedOnEveryExit_NotSaved()
    {
        var g = Web("Services/GameSession.cs");
        Assert.That(g, Does.Contain("ReconcileFeed();"));
        Assert.That(g, Does.Contain("if (sel.Key != Selected.Key) { SelectedBatchKey = null; FeedPending = null; }"), "切对象清");
        Assert.That(g, Does.Contain("if (FeedPending?.PartyActor != null) FeedPending = null;"), "切成员、收起清");
        Assert.That(g, Does.Contain("if (InBattleUi || DialogueLocked || targets.Count == 0 || left) FeedPending = null;"), "战斗、对白、离队打断");
    }

    [Test]
    public void Help_EntryInBodyTopic_ReturnsToPop()
    {
        var help = Web("Services/HelpCatalog.cs");
        var topic = help.IndexOf("Id = \"body\"");
        Assert.That(help.IndexOf("Id = GameSession.FeedHelpEntry"), Is.GreaterThan(topic));
        Assert.That(Web("Components/HelpOverlay.razor"), Does.Contain("FeedPanelCopy.HelpBack"));
        Assert.That(Web("Services/GameSession.cs"), Does.Contain("OpenHelp(\"body\", FeedHelpEntry);"));
    }
}
