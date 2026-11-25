using Bunit;
using Survival.Core.Engine;
using Survival.Core.State;
using Survival.Web.Components;
using Survival.Web.Services;

namespace Survival.Web.Tests;

/// <summary>
/// B36a M2：系统说明改「目录左、内容右」与返回链（02 Q812 A、Q889 A、Q890 B、Q899 A、Q955 B、Q821 A）。
/// 点组件里的主题／条目／两个退出控件，Esc 走 GameScreen.OnKey（与玩家按键同一入口），断玩家看到的目录、正文、按钮字与回到哪一层。
/// </summary>
public class B36aHelpUiTests : WebTestBase
{
    private static string Btn(IRenderedFragment cut, string pt) => cut.Find($"[data-pt={pt}]").TextContent.Replace("ESC", "").Trim();

    /// <summary>
    /// Q889 A：顶栏正常新开不选主题，右栏列9个主题的原摘要；左栏「主题」9项、都不展开条目。
    /// Q899 A：没显示条目正文时不出正文内「‹ 返回」；Q821 A：顶栏直接开、退出回主界面写「关闭」。
    /// </summary>
    [Test]
    public void TopOpen_NineSummaries_NoTopicPicked_ExitClose()
    {
        G.OpenTopPanel("help");
        var cut = Ctx.RenderComponent<HelpOverlay>();

        Assert.That(cut.Find("[data-pt=read-nav-title]").TextContent, Is.EqualTo("主题"), "逐字稿 P08g「主题」");
        Assert.That(cut.FindAll("[data-pt=help-topic]"), Has.Count.EqualTo(9), "Q812：左栏原9主题");
        Assert.That(cut.FindAll("[data-pt=help-summary]"), Has.Count.EqualTo(9), "Q889 A：右栏9主题摘要");
        Assert.That(cut.FindAll("[data-pt=help-topic].on"), Is.Empty, "不预选主题");
        Assert.That(cut.FindAll("[data-pt=help-entry]"), Is.Empty, "不展开条目");
        Assert.That(cut.FindAll("[data-pt=help-back]"), Is.Empty, "总览时没有正文内返回");
        Assert.That(Btn(cut, "help-exit"), Is.EqualTo("关闭"));
    }

    /// <summary>
    /// Q812 A：点主题在左栏展开该主题原条目，右栏显示主题摘要（不出正文返回）；点条目右栏显示正文，出「‹ 返回」；
    /// 点「‹ 返回」只回所属主题摘要，不退出说明（Q899 A 两个控件各管各的）。
    /// </summary>
    [Test]
    public void PickTopic_ExpandsEntries_ThenEntry_InternalBackToTopic()
    {
        G.OpenTopPanel("help");
        var cut = Ctx.RenderComponent<HelpOverlay>();
        cut.Find("[data-pt=help-topic][data-topic=body]").Click();

        var body = HelpCatalog.Topic("body")!;
        Assert.That(cut.FindAll("[data-pt=help-entry]").Select(x => x.TextContent.Trim()).ToArray(), Is.EqualTo(body.Entries.Select(x => x.Title).ToArray()), "只展开所选主题的原条目");
        Assert.That(cut.FindAll("[data-pt=help-summary]"), Is.Empty, "不再是9摘要总览");
        Assert.That(cut.Find("[data-pt=help-topic-summary]").TextContent, Does.Contain("基础属性与熟练度、饮食、恢复、异常状态、免疫与感染、医疗"), "原主题摘要（HelpCatalog body.Summary）");
        Assert.That(cut.FindAll("[data-pt=help-back]"), Is.Empty);

        var first = body.Entries[0];
        cut.Find($"[data-pt=help-entry][data-entry={first.Id}]").Click();
        Assert.That(cut.Find(".help-article h3").TextContent, Is.EqualTo(first.Title));
        Assert.That(Btn(cut, "help-back"), Is.EqualTo("‹ 返回"));
        Assert.That(Btn(cut, "help-exit"), Is.EqualTo("关闭"), "独立退出照旧");

        cut.Find("[data-pt=help-back]").Click();
        Assert.That((G.Panel, G.HelpTopic, G.HelpEntry), Is.EqualTo(("help", "body", (string?)null)), "只回所属主题");
        Assert.That(cut.FindAll(".help-article"), Is.Empty);
        Assert.That(cut.FindAll("[data-pt=help-entry]"), Has.Count.EqualTo(body.Entries.Count), "主题仍展开");
    }

    /// <summary>
    /// Q890 B：从属性详情直接定位到条目，仍直接显示那条正文；Esc 第一次回所属主题摘要（撤掉原一步直关），第二次才退出说明回原入口，
    /// 不额外插9摘要总览一层。
    /// </summary>
    [Test]
    public void DirectEntry_Esc_TopicThenExit()
    {
        var cut = Ctx.RenderComponent<GameScreen>();
        var entry = HelpCatalog.Topic("body")!.Entries[0];
        cut.InvokeAsync(() => G.OpenHelp("body", entry.Id));
        Assert.That(cut.Find(".help-article h3").TextContent, Is.EqualTo(entry.Title), "直接显示所指条目");
        // ESC 键帽跟着 Esc 实际承接的控件走（B43 A03／A06）：正文时在「‹ 返回」上，独立退出上没有
        Assert.That(cut.FindAll("[data-pt=help-back] kbd"), Has.Count.EqualTo(1));
        Assert.That(cut.FindAll("[data-pt=help-exit] kbd"), Is.Empty);

        cut.InvokeAsync(() => cut.Instance.OnKey("Escape"));
        Assert.That((G.Panel, G.HelpTopic, G.HelpEntry), Is.EqualTo(("help", "body", (string?)null)), "第一次 Esc 回所属主题");
        Assert.That(cut.FindAll("[data-pt=help-topic-summary]"), Has.Count.EqualTo(1));
        Assert.That(cut.FindAll("[data-pt=help-exit] kbd"), Has.Count.EqualTo(1), "主题摘要时 ESC 键帽到独立退出上");

        cut.InvokeAsync(() => cut.Instance.OnKey("Escape"));
        Assert.That(G.Panel, Is.Null, "第二次 Esc 回原入口（主界面），没有9摘要总览这一层");
    }

    /// <summary>Q812 A：正常翻阅时只选了主题（没选条目），Esc 一次就退出说明（配对：上一条有条目时 Esc 先退主题）。</summary>
    [Test]
    public void TopicOnly_Esc_Exits()
    {
        var cut = Ctx.RenderComponent<GameScreen>();
        cut.InvokeAsync(() => G.OpenTopPanel("help"));
        cut.Find("[data-pt=help-topic][data-topic=map]").Click();
        Assert.That(G.HelpTopic, Is.EqualTo("map"), "起点");
        cut.InvokeAsync(() => cut.Instance.OnKey("Escape"));
        Assert.That(G.Panel, Is.Null);
    }

    /// <summary>Q821 A：先开手工制作再开系统说明，独立退出写「返回」，点了回制作页；左栏换主题／条目不压层（一次就回制作）。</summary>
    [Test]
    public void OverCraft_ExitReturn_BackToCraft_TopicClicksNoExtraLayer()
    {
        G.OpenTopPanel("craft");
        G.OpenTopPanel("help");
        var cut = Ctx.RenderComponent<HelpOverlay>();
        Assert.That(Btn(cut, "help-exit"), Is.EqualTo("返回"));
        cut.Find("[data-pt=help-topic][data-topic=map]").Click();
        cut.Find("[data-pt=help-topic][data-topic=items]").Click();
        cut.FindAll("[data-pt=help-entry]")[0].Click();
        cut.Find("[data-pt=help-exit]").Click();
        Assert.That(G.Panel, Is.EqualTo("craft"), "一次回制作页");
    }

    /// <summary>
    /// Q955 B（合同 b36a-q950-q955-ui-copy.json help.independentExitFromFood「返回」、deprecateOldFoodBackCopy「返回食饮」）：
    /// 从食饮盘进说明，独立退出只写「返回」、不写「返回食饮」；正文内另有「‹ 返回」。
    /// Q890 B／B36A-Q955-02：Esc 正文 → 所属主题 → 真实食饮盘，一次一层；回去后食饮草稿还在。
    /// </summary>
    [Test]
    public void FromFeed_ExitSaysReturn_EscEntryTopicThenFeed()
    {
        S.Progress.Flags.Add("hub_registered");
        S.Progress.Flags.Add("church_route_record");
        S.Progress.Flags.Add("academy_safety_record");
        E.DebugRecruit(S, "npc_lin_zhao");
        var c = E.CreateItems(S, "ration_canteen", 1, ItemInstance.CarriedOwner("npc_lin_zhao")).Single();
        c.LiquidId = "clean_water";
        c.LiquidMl = 700;
        var cut = Ctx.RenderComponent<GameScreen>();
        cut.InvokeAsync(() => G.OpenFeed(E.FeedEntry(S, c.InstanceId)!, "card-sel"));
        cut.InvokeAsync(() => G.OpenFeedHelp());

        Assert.That(G.HelpEntry, Is.EqualTo(GameSession.FeedHelpEntry), "起点：直接定位到食饮说明条目");
        Assert.That(Btn(cut, "help-exit"), Is.EqualTo("返回"));
        Assert.That(cut.Markup, Does.Not.Contain("返回食饮"));
        Assert.That(Btn(cut, "help-back"), Is.EqualTo("‹ 返回"));

        cut.InvokeAsync(() => cut.Instance.OnKey("Escape"));
        Assert.That((G.Panel, G.HelpEntry), Is.EqualTo(("help", (string?)null)), "第一次 Esc 回所属主题");
        Assert.That(G.FeedPending, Is.Not.Null, "辅助：食饮草稿仍在下面");
        cut.InvokeAsync(() => cut.Instance.OnKey("Escape"));
        Assert.That(G.Panel, Is.Null, "第二次 Esc 退出说明");
        Assert.That(G.FeedPending, Is.Not.Null, "回到原食饮盘（草稿未丢）");
        Assert.That(cut.FindAll("[data-feed-help]"), Has.Count.EqualTo(1), "食饮盘重新显示");
    }

    /// <summary>Q955 B：从食饮进来时点独立「返回」一次就回食饮盘（不经主题）。</summary>
    [Test]
    public void FromFeed_ExitClick_BackToFeedAtOnce()
    {
        S.Progress.Flags.Add("hub_registered");
        S.Progress.Flags.Add("church_route_record");
        S.Progress.Flags.Add("academy_safety_record");
        E.DebugRecruit(S, "npc_lin_zhao");
        var c = E.CreateItems(S, "ration_canteen", 1, ItemInstance.CarriedOwner("npc_lin_zhao")).Single();
        c.LiquidId = "clean_water";
        c.LiquidMl = 700;
        var cut = Ctx.RenderComponent<GameScreen>();
        cut.InvokeAsync(() => G.OpenFeed(E.FeedEntry(S, c.InstanceId)!, "card-sel"));
        cut.InvokeAsync(() => G.OpenFeedHelp());
        cut.Find("[data-pt=help-exit]").Click();
        Assert.That(G.Panel, Is.Null);
        Assert.That(G.FeedPending, Is.Not.Null);
    }
}
