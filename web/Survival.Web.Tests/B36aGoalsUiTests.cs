using Bunit;
using Survival.Core.Engine;
using Survival.Web.Components;

namespace Survival.Web.Tests;

/// <summary>
/// B36a M3：生存目标逐条任务（02 Q813 B、Q888 A、Q898 B、Q954①②③，开发方 D4＝02 Q1126 A）。
/// 起点只造进度标记（营区登记、主线完成／接取）与旧档七日标记；之后点组件里的任务项，断左栏顺序、右栏全文、共用主目标区与快捷按钮。
/// </summary>
public class B36aGoalsUiTests : WebTestBase
{
    private IRenderedComponent<GoalsOverlay> Open()
    {
        G.OpenTopPanel("goals");
        return Ctx.RenderComponent<GoalsOverlay>();
    }

    private static string[] Items(IRenderedComponent<GoalsOverlay> cut) =>
        cut.FindAll("[data-pt=goal-item]").Select(x => x.GetAttribute("data-goal")!).ToArray();

    private static string? Picked(IRenderedComponent<GoalsOverlay> cut) => cut.Find("[data-pt=goal-item].on").GetAttribute("data-goal");

    /// <summary>
    /// Q813 B／Q898 B：左栏每条真实任务一项，序章按原六阶段稳定顺序（S15 任务册 PRO01—PRO18），不插阶段组头。
    /// Q888 A：新开默认选主目标对应原任务——新局首战前主目标是「第一次交手」PRO04（S15 主目标优先级：未首战时首战），不是左栏第一条。
    /// 右栏上方共用主目标区（「主目标」＋标题＋下一步＋最多两条求生建议），下方原快捷按钮（暂停操作提示）。
    /// Q954②：序章任务名下写所属阶段原标题与重点，当前阶段带「当前」角标，不加「所属阶段：」前缀。
    /// </summary>
    [Test]
    public void FreshGame_EachTaskOneItem_DefaultMainObjectiveTask_StageUnderTitle()
    {
        var cut = Open();
        var expected = Enumerable.Range(1, 18).Select(n => $"t:PRO{n:00}").ToArray();
        Assert.That(Items(cut), Is.EqualTo(expected), "18条序章任务按原顺序各一项");
        Assert.That(cut.Find("[data-pt=read-nav]").TextContent, Does.Not.Contain("先在附近站稳脚跟"), "左栏不插阶段标题");
        Assert.That(cut.Find("[data-pt=read-nav]").TextContent, Does.Not.Contain("当前目标").And.Not.Contain("已完成"), "Q898 B：不加文字标签");

        Assert.That(Picked(cut), Is.EqualTo("t:PRO04"), "默认主目标对应任务");
        var main = cut.Find("[data-pt=goal-main]");
        Assert.That(main.QuerySelector("header b")!.TextContent, Is.EqualTo("主目标"));
        Assert.That(main.QuerySelector("header span")!.TextContent, Is.EqualTo("第一次交手"));
        Assert.That(main.QuerySelectorAll(".goal-sugg span").Length, Is.EqualTo(2), "最多两条求生建议");

        var detail = cut.Find("[data-pt=goal-detail]");
        Assert.That(detail.QuerySelector("header b")!.TextContent, Is.EqualTo("第一次交手"));
        var stage = cut.Find("[data-pt=goal-stage]");
        Assert.That(stage.QuerySelector("b")!.TextContent, Is.EqualTo("先在附近站稳脚跟"), "S15 D1 阶段原标题");
        Assert.That(stage.QuerySelector("span")!.TextContent, Is.EqualTo("查看物资、补给、防身和首战"), "S15 D1 阶段原重点");
        Assert.That(cut.FindAll("[data-pt=goal-stage-now]"), Has.Count.EqualTo(1), "当前阶段带原角标");
        Assert.That(stage.TextContent, Does.Not.Contain("所属阶段"));
        Assert.That(cut.Find("[data-pt=goal-hints]").TextContent, Is.EqualTo("暂停操作提示"));
        Assert.That(cut.Find("[data-pt=goals-sub]").TextContent, Is.EqualTo("存活第1日 · 序章任务0/18 · 建议日程，不是失败倒计时"),
            "Q954③ 页副标保留（合同 goals.pageSubtitle）");
    }

    /// <summary>
    /// Q888 A：换看别的任务，主目标区与快捷按钮照样在；Q954②：阶段取该任务自己的阶段（D2「建立食水补给」），不是当前阶段就没有「当前」角标。
    /// </summary>
    [Test]
    public void PickOtherTask_SharedMainAreaStays_OwnStage_NoCurrentBadge()
    {
        var cut = Open();
        cut.Find("[data-pt=goal-item][data-goal='t:PRO07']").Click();
        Assert.That(Picked(cut), Is.EqualTo("t:PRO07"));
        Assert.That(cut.Find("[data-pt=goal-detail] header b").TextContent, Is.EqualTo("火要有燃料"));
        Assert.That(cut.Find("[data-pt=goal-stage] b").TextContent, Is.EqualTo("建立食水补给"));
        Assert.That(cut.FindAll("[data-pt=goal-stage-now]"), Is.Empty, "非当前阶段不借写当前");
        Assert.That(cut.Find("[data-pt=goal-main] header span").TextContent, Is.EqualTo("第一次交手"), "主目标区不随选中任务变");
        Assert.That(cut.FindAll("[data-pt=goal-quick]"), Has.Count.EqualTo(1));
        Assert.That(G.Panel, Is.EqualTo("goals"), "换任务只换右栏");
    }

    /// <summary>
    /// 营区登记后：主线 FA01 已完成、FA03 已接取——主目标按 Q155 取进行中的 FA03。
    /// Q898 B：主线与委托在前、序章在后，完成仍✓；Q888 A：默认选 FA03（不是左栏第一条未完成的 FA02）。
    /// Q954①：委托详情进度行写「当前进度：{真实进度}」，不再是「下一步：」；Q954③：每条委托末尾原办理说明。
    /// </summary>
    [Test]
    public void AfterRegistration_QuestsFirst_DefaultMainQuest_ProgressPrefix_Footer()
    {
        S.Progress.Flags.Add("hub_registered");
        S.Progress.Flags.Add(GameEngine.QuestFact("FA01", "completed"));
        S.Progress.Flags.Add(GameEngine.QuestFact("FA03", "accepted"));
        E.PrologueBoundary(S);
        var cut = Open();

        var items = Items(cut);
        Assert.That(items.Take(3).ToArray(), Is.EqualTo(new[] { "q:FA01", "q:FA02", "q:FA03" }), "主线与委托在前");
        Assert.That(items.Last(), Is.EqualTo("t:PRO18"), "序章在后");
        var fa01 = cut.Find("[data-pt=goal-item][data-goal='q:FA01']");
        Assert.That(fa01.ClassList, Does.Contain("done"));
        Assert.That(fa01.QuerySelector(".rn-mark")!.TextContent, Is.EqualTo("✓"));

        Assert.That(Picked(cut), Is.EqualTo("q:FA03"));
        Assert.That(cut.Find("[data-pt=goal-quest-progress]").TextContent, Is.EqualTo("当前进度：从封存实验楼进入灰塔封存区，在核心领取封存区安全规程。"));
        Assert.That(cut.Find("[data-pt=goal-detail]").TextContent, Does.Not.Contain("下一步："));
        Assert.That(cut.Find("[data-pt=goal-quest-foot]").TextContent, Is.EqualTo("到相应地点与岗位交谈或在“事项”里办理；浏览不耗时"), "合同 goals.commissionFooter");
        Assert.That(cut.FindAll("[data-pt=goal-stage]"), Is.Empty, "非序章任务不编阶段");

        cut.Find("[data-pt=goal-item][data-goal='q:FA01']").Click();
        Assert.That(cut.FindAll("[data-pt=goal-quest-progress]"), Is.Empty, "已完成委托不出进度行（原结果保持）");
        Assert.That(cut.FindAll("[data-pt=goal-quest-foot]"), Has.Count.EqualTo(1));
    }

    /// <summary>
    /// D4（02 Q1126 A）：主目标没有对应任务条目（这里用「安顿」settle，S15 特殊主目标）时，默认选左栏第一条未完成任务 PRO01。
    /// 配对：上一组有对应任务时选的是主目标任务而不是第一条。
    /// </summary>
    [Test]
    public void ObjectiveWithoutTask_DefaultFirstUnfinished()
    {
        S.Prologue.ObjectiveId = "settle";   // 起点：主目标是没有任务条目的「安顿」
        var cut = Open();
        Assert.That(E.CurrentObjective(S)!.TaskId, Is.Null, "起点核对");
        Assert.That(Picked(cut), Is.EqualTo("t:PRO01"));
        Assert.That(cut.FindAll("[data-pt=goal-main]"), Has.Count.EqualTo(1), "仍显示真实主目标区");
    }

    /// <summary>Q888 A 默认主目标任务：本窗口里点过别的任务，关页重开回默认。</summary>
    [Test]
    public void Reopen_BackToDefault()
    {
        var cut = Open();
        cut.Find("[data-pt=goal-item][data-goal='t:PRO07']").Click();
        cut.Find("[data-pt=goals-close]").Click();
        G.OpenTopPanel("goals");
        cut.Render();
        Assert.That(Picked(cut), Is.EqualTo("t:PRO04"));
    }

    /// <summary>旧档七日检查历史（原「历史」组，B36a 放左栏最后）：点它右栏显示原说明句；新局没有这一项。</summary>
    [Test]
    public void LegacySevenDay_HistoryLast()
    {
        var fresh = Open();
        Assert.That(Items(fresh), Does.Not.Contain(GoalsOverlay.HistoryKey), "配对：新局没有历史");
        S.Progress.SevenDayShown = true;
        fresh.Render();
        Assert.That(Items(fresh).Last(), Is.EqualTo(GoalsOverlay.HistoryKey));
        fresh.Find($"[data-pt=goal-item][data-goal={GoalsOverlay.HistoryKey}]").Click();
        Assert.That(fresh.Find("[data-pt=goal-detail]").TextContent, Does.Contain("七日基础生存检查（旧版记录，已退役，不再作为目标）"));
    }
}
