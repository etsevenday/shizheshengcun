using Bunit;
using Survival.Web.Components;

namespace Survival.Web.Tests;

/// <summary>
/// B36a M5：编队用词（02 Q825 A、Q950 A；合同 b36a-q950-q955-ui-copy.json formation）。
/// 渲染编队页与右栏队伍块，断副标、空号位名与悬停、底部说明和队伍说明里玩家看到的字。
/// </summary>
public class B36aFormationUiTests : WebTestBase
{
    private const string Lin = "npc_lin_zhao";

    private void RecruitLin()
    {
        S.Progress.Flags.Add("hub_registered");
        S.Progress.Flags.Add("church_route_record");
        S.Progress.Flags.Add("academy_safety_record");
        E.DebugRecruit(S, Lin);
    }

    /// <summary>
    /// B36A-Q950-01／02：编队副标的名词「开战号位」换「入场站位」（合同 formation.name），副标其余部分保留；
    /// 空号位名「未分配」（formation.empty）、悬停「{n}号位未分配：先点一名角色，再点这里」（formation.emptyHover）。
    /// </summary>
    [Test]
    public void Subtitle_StationName_EmptySlotsUnassigned()
    {
        G.OpenTopPanel("formation");
        var cut = Ctx.RenderComponent<FormationOverlay>();
        var sub = cut.Find("[data-pt=fm-sub]").TextContent;
        Assert.That(sub, Does.StartWith(Contract.B36a("formation.name") + " · 前排1号位→后排4号位 · 同行1/C"));
        Assert.That(sub, Does.EndWith("（C只限人数，不限号位）· 保存不花时间、AP和体力"), "副标其余部分保留");
        Assert.That(cut.Markup, Does.Not.Contain("开战号位"));

        var empty = cut.FindAll(".fm-slot.empty");
        Assert.That(empty, Has.Count.EqualTo(3), "起点：单人，三个号位没人");
        foreach (var b in empty)
        {
            var n = b.GetAttribute("data-slot")!;
            Assert.That(b.QuerySelector("[data-pt=fm-slot-name]")!.TextContent, Is.EqualTo(Contract.B36a("formation.empty")));
            Assert.That(b.GetAttribute("title"), Is.EqualTo(Contract.B36a("formation.emptyHover").Replace("{n}", n)));
        }
        Assert.That(cut.Markup, Does.Not.Contain("空位"), "编队页不再写空位");
    }

    /// <summary>
    /// Q825 A／B36A-Q950-02：有同行或候补时底部说明保留办理地点与安全屋说明，旧错误句「队友暂时还不会跟你进战斗，下一场只有你按这里的号位入场。」不再出现。
    /// （这半句原本只在名册有人时出现，所以起点招一名同行。）
    /// </summary>
    [Test]
    public void BottomNote_Q825SentenceGone_RestKept()
    {
        RecruitLin();
        G.OpenTopPanel("formation");
        var cut = Ctx.RenderComponent<FormationOverlay>();
        var note = cut.Find("[data-pt=fm-note]").TextContent;
        Assert.That(note, Does.Contain("编入同行/留作候补在"), "前半句办理地点保留");
        Assert.That(note, Does.Contain("（安全屋候补要进那间屋编入，屋里可留守此屋）。"));
        Assert.That(note, Does.Not.Contain("队友暂时还不会跟你进战斗").And.Not.Contain("下一场只有你按这里的号位入场"));
    }

    /// <summary>B36A-Q952-02（Q950 名词随换）：右栏队伍块说明「开战号位在顶栏“编队”里改……」换成「入场站位在顶栏“编队”里改……」，其余原文保留。</summary>
    [Test]
    public void PartyNote_StationName()
    {
        RecruitLin();
        var cut = Ctx.RenderComponent<PartyPanel>();
        var note = cut.Find("[data-pt=party-note]").TextContent;
        Assert.That(note, Does.StartWith(Contract.B36a("formation.name") + "在顶栏“编队”里改（任何地点空闲时都可以）；编入同行/留作候补在"));
        Assert.That(cut.Markup, Does.Not.Contain("开战号位"));
    }
}
