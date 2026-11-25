using Bunit;
using Survival.Core.State;
using Survival.Web.Components;

namespace Survival.Web.Tests;

/// <summary>
/// B36a M6：成长页（02 Q823 A、Q824 A、Q892 B、Q951 A；合同 b36a-q950-q955-ui-copy.json growth）。
/// 起点只用内核 DebugSetSkillLevel 设等级、DebugRecruit 招人；点左栏门类、卡与人物页签，断左栏标题与分组、详情头、单张说明与本次运行的选卡记忆。
/// </summary>
public class B36aGrowthUiTests : WebTestBase
{
    private IRenderedComponent<GrowthOverlay> Open()
    {
        G.OpenTopPanel("growth");
        return Ctx.RenderComponent<GrowthOverlay>();
    }

    private static string? Desc(IRenderedComponent<GrowthOverlay> cut) => cut.Find("[data-pt=growth-card-desc]").GetAttribute("data-card");

    /// <summary>
    /// Q951 A：左栏标题「熟练度」（合同 growth.leftTitle），原三组「战斗」「医疗（战内外）」「求生与行动」（growth.groups）与实际17项保留。
    /// Q823 A：首次默认「拳」。详情头「当前等级 {L}/{上限}级」「经验 {xp}/{升级所需}，升到{L+1}级」「下一个技能：{L}级 {技能名}」（growth.detailLevel／detailExperience／next）：
    /// 拳1级、上限10（data 配置 levelCap 10），升级所需 100（配置 nextLevelXpFormula「50*(L+1)」，L=1），下一个技能 3级 推挡（S10 拳节点表）。
    /// </summary>
    [Test]
    public void LeftTitleGroups17_DefaultFists_DetailHeadLabels()
    {
        var cut = Open();
        Assert.That(cut.Find("[data-pt=growth-list-title]").TextContent, Is.EqualTo(Contract.B36a("growth.leftTitle")));
        Assert.That(cut.FindAll(".skill-group").Select(x => x.TextContent).ToArray(), Is.EqualTo(new[] { "战斗", "医疗（战内外）", "求生与行动" }));
        Assert.That(cut.FindAll(".growth-skill"), Has.Count.EqualTo(17), "实际17项（含潜行、意志）");
        Assert.That(cut.Find(".growth-skill.on").GetAttribute("data-skill"), Is.EqualTo("unarmed"), "首次默认拳");

        Assert.That(cut.Find("[data-pt=growth-level]").TextContent, Is.EqualTo("当前等级 1/10级"));
        Assert.That(cut.Find("[data-pt=growth-xp]").TextContent, Is.EqualTo("经验 0/100，升到2级"));
        Assert.That(cut.Find("[data-pt=growth-next]").TextContent, Is.EqualTo("下一个技能：3级 推挡"));
        Assert.That(cut.Markup, Does.Not.Contain("下一节点"));
    }

    /// <summary>
    /// Q824 A：五张卡一排、只显示一张说明，默认下一个未解锁（拳1级 → 3级推挡，状态词「未解锁」、原锁级句「需要拳3级（当前1级）」保留）；
    /// 点别的卡切说明（1级拳击「已解锁」）。点卡只读：不改等级、经验。
    /// </summary>
    [Test]
    public void FiveCards_OneDescription_DefaultNextLocked_ClickSwitches()
    {
        var cut = Open();
        Assert.That(cut.FindAll("[data-pt=growth-card]"), Has.Count.EqualTo(5));
        Assert.That(cut.FindAll("[data-pt=growth-card-desc]"), Has.Count.EqualTo(1), "一次只一条说明");
        Assert.That(Desc(cut), Is.EqualTo("unarmed_03"));
        var d = cut.Find("[data-pt=growth-card-desc]");
        Assert.That(d.QuerySelector("header b")!.TextContent, Is.EqualTo("3级 · 推挡"));
        Assert.That(d.QuerySelector("header em")!.TextContent, Is.EqualTo("未解锁"));
        Assert.That(d.TextContent, Does.Contain("需要拳3级（当前1级）"));

        cut.Find("[data-pt=growth-card][data-card=unarmed_01]").Click();
        Assert.That(Desc(cut), Is.EqualTo("unarmed_01"));
        Assert.That(cut.Find("[data-pt=growth-card-desc] header em").TextContent, Is.EqualTo("已解锁"));
        Assert.That(S.Skills.TryGetValue("unarmed", out var st) ? st.Level : 1, Is.EqualTo(1), "点卡不学习、不升级");
    }

    /// <summary>Q824 A：全部解锁时默认最高一张；满级沿原「满级，不再获得经验」，不编下一等级、不出「下一个技能」（配对：上一组未满级时有）。</summary>
    [Test]
    public void AllUnlocked_DefaultHighest_MaxedOriginalCopy()
    {
        E.DebugSetSkillLevel(S, "blade", 10);
        var cut = Open();
        cut.Find(".growth-skill[data-skill=blade]").Click();
        Assert.That(Desc(cut), Is.EqualTo("blade_10"));
        Assert.That(cut.Find("[data-pt=growth-level]").TextContent, Is.EqualTo("当前等级 10/10级"));
        Assert.That(cut.Find("[data-pt=growth-xp]").TextContent, Is.EqualTo("满级，不再获得经验"));
        Assert.That(cut.FindAll("[data-pt=growth-next]"), Is.Empty);
    }

    /// <summary>
    /// Q892 B：本次运行按门类记住上次点的卡——拳点 1级卡后换到刀剑（刀剑回自己的默认），再切回拳恢复 1级卡；关页重开同样恢复（Q823 A 门类也记住）。
    /// </summary>
    [Test]
    public void PerCategoryCardMemory_AcrossSwitchAndReopen()
    {
        var cut = Open();
        cut.Find("[data-pt=growth-card][data-card=unarmed_01]").Click();
        cut.Find(".growth-skill[data-skill=blade]").Click();
        Assert.That(Desc(cut), Is.EqualTo("blade_03"), "刀剑没点过：默认下一个未解锁");
        cut.Find(".growth-skill[data-skill=unarmed]").Click();
        Assert.That(Desc(cut), Is.EqualTo("unarmed_01"), "切回拳恢复上次点的卡");

        G.OpenPanel(null);
        G.OpenTopPanel("growth");
        cut.Render();
        Assert.That(cut.Find(".growth-skill.on").GetAttribute("data-skill"), Is.EqualTo("unarmed"));
        Assert.That(Desc(cut), Is.EqualTo("unarmed_01"), "关页重开仍恢复");
    }

    /// <summary>Q892 B「切人沿Q823门类记忆」：换人物保留所选门类和卡，等级经验换成这人的（不借上一人的状态）。</summary>
    [Test]
    public void SwitchActor_KeepsCategoryAndCard()
    {
        S.Progress.Flags.Add("hub_registered");
        S.Progress.Flags.Add("church_route_record");
        S.Progress.Flags.Add("academy_safety_record");
        E.DebugRecruit(S, "npc_lin_zhao");
        E.DebugSetSkillLevel(S, "unarmed", 4);   // 主角拳4级
        var cut = Open();
        cut.Find("[data-pt=growth-card][data-card=unarmed_10]").Click();
        Assert.That(cut.Find("[data-pt=growth-level]").TextContent, Is.EqualTo("当前等级 4/10级"), "起点：主角");

        cut.Find(".gear-member[data-actor=npc_lin_zhao]").Click();
        Assert.That(cut.Find(".growth-skill.on").GetAttribute("data-skill"), Is.EqualTo("unarmed"));
        Assert.That(Desc(cut), Is.EqualTo("unarmed_10"));
        Assert.That(cut.Find("[data-pt=growth-level]").TextContent, Does.Not.Contain("4/10级"), "等级换成林昭本人的");
    }

    /// <summary>
    /// Q892 B／Q736：意志五个参考格只写「{L}级 · 致死抵抗{P}%」并按达到点亮，不写已解锁／未解锁、不出「下一个技能」；
    /// 默认下一个尚未达到的格（意志1级 → 3级格），全达到选10级；点格只切说明。
    /// </summary>
    [Test]
    public void Willpower_RefCards_TextAndLitOnly_DefaultNextUnattained()
    {
        var cut = Open();
        cut.Find(".growth-skill[data-skill=willpower]").Click();
        var cards = cut.FindAll("[data-pt=growth-card]");
        Assert.That(cards.Select(c => c.GetAttribute("data-will-ref")).ToArray(), Is.EqualTo(new[] { "1", "3", "5", "7", "10" }), "R09 参考格 1／3／5／7／10级");
        var lv = new[] { 1, 3, 5, 7, 10 };
        for (var k = 0; k < lv.Length; k++)
            Assert.That(cards[k].TextContent, Does.Match($@"^{lv[k]}级 · 致死抵抗\d+(\.\d+)?%$"), "Q690 格式「{L}级 · 致死抵抗{P}%」");
        Assert.That(cards.Count(c => c.ClassList.Contains("attained")), Is.EqualTo(1), "1级只点亮1级格");
        Assert.That(Desc(cut), Is.EqualTo("will:3"));
        var will = cut.Find(".growth-detail").TextContent;
        Assert.That(will, Does.Not.Contain("已解锁").And.Not.Contain("未解锁").And.Not.Contain("下一个技能"));

        cut.Find("[data-pt=growth-card][data-card='will:7']").Click();
        Assert.That(Desc(cut), Is.EqualTo("will:7"));
        Assert.That(cut.Find("[data-pt=growth-card-desc]").TextContent, Is.EqualTo(cut.Find("[data-pt=growth-card][data-card='will:7']").TextContent), "单张说明只该格原句");
        Assert.That(cut.Find("[data-pt=growth-card-desc]").TextContent, Does.StartWith("7级 · 致死抵抗"));

        E.DebugSetSkillLevel(S, "willpower", 10);
        G.GrowthCards.Clear();
        cut.Render();
        Assert.That(Desc(cut), Is.EqualTo("will:10"), "全达到选10级");
    }
}
