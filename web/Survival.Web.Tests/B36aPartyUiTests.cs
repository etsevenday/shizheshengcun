using Bunit;
using Survival.Core.Engine;
using Survival.Core.State;
using Survival.Web.Components;
using Survival.Web.Services;

namespace Survival.Web.Tests;

/// <summary>
/// B36a M7：右栏人物页候补分组与箱区收展（02 Q816 A、Q891 A、Q952 A、Q953 A；合同 b36a-q950-q955-ui-copy.json reserve／boxJump）。
/// 起点用内核 DebugRecruit／SetActive 造候补、DebugDiscardListKit 在当前地点放四类箱；之后点「查看认领箱」「返回」、行末丢弃，以及回收箱卡「取出…」同一入口。
/// </summary>
public class B36aPartyUiTests : WebTestBase
{
    private const string Lin = "npc_lin_zhao";

    private void ReserveLin()
    {
        S.Progress.Flags.UnionWith(new[] { "registration_authorized", "hub_registered", "church_route_record", "academy_safety_record" });
        E.DebugTeleport(S, "h02_square");
        for (var k = 0; k < 128 && E.DialogueLocked(S); k++) E.AckDialogue(S, E.CurrentDialogueLine(S)!.LineId);
        E.DebugRecruit(S, Lin);
        Assert.That(E.SetActive(S, Lin, false).Ok, Is.True, "起点：林昭留作候补");
        G.Notify();
    }

    /// <summary>
    /// B36A-Q952-01：候补单列「留守候补」组（合同 reserve.group），每人行首仍「候补」（reserve.personPrefix），行内直显「所在地点 {地点名}」（reserve.location，取实际驻留处营区广场）；
    /// 同行原号位保留在上面的列表里。B36A-Q952-02：两段原说明保留，名词随 Q950 换「入场站位」。
    /// </summary>
    [Test]
    public void Reserves_OwnGroup_PrefixKept_LocationInline_NotesKept()
    {
        ReserveLin();
        var cut = Ctx.RenderComponent<PartyPanel>();
        var group = cut.Find("[data-pt=reserve-group]");
        Assert.That(group.QuerySelector("[data-pt=reserve-title]")!.TextContent, Is.EqualTo(Contract.B36a("reserve.group")));
        var rows = group.QuerySelectorAll(".party-row");
        Assert.That(rows.Length, Is.EqualTo(1));
        Assert.That(rows[0].GetAttribute("data-actor"), Is.EqualTo(Lin));
        Assert.That(rows[0].QuerySelector(".party-pos")!.TextContent, Is.EqualTo(Contract.B36a("reserve.personPrefix")));
        Assert.That(rows[0].QuerySelector("[data-pt=reserve-where]")!.TextContent.Trim(), Is.EqualTo(Contract.B36a("reserve.location").Replace("{地点名}", "营区广场")));

        var active = cut.Find(".party-list:not(.party-reserves)").QuerySelectorAll(".party-row");
        Assert.That(active.Select(r => r.QuerySelector(".party-pos")!.TextContent).ToArray(), Is.EqualTo(new[] { "1号位" }), "同行原号位");
        Assert.That(cut.Find("[data-pt=party-note]").TextContent, Does.StartWith("入场站位在顶栏"));
        Assert.That(cut.Markup, Does.Contain("候补在所在处（"), "候补自主休息／公共照护原说明保留");
    }

    /// <summary>配对：没有候补时不出「留守候补」组，也不另加空态长句。</summary>
    [Test]
    public void NoReserves_NoGroup()
    {
        S.Progress.Flags.UnionWith(new[] { "hub_registered", "church_route_record", "academy_safety_record" });
        E.DebugRecruit(S, Lin);
        var cut = Ctx.RenderComponent<PartyPanel>();
        Assert.That(cut.FindAll(".party-row"), Has.Count.EqualTo(2), "起点：你和林昭同行");
        Assert.That(cut.FindAll("[data-pt=reserve-group]"), Is.Empty);
        Assert.That(cut.Markup, Does.Not.Contain("留守候补"));
    }

    /// <summary>
    /// B36A-Q952-02／Q891 A：本地三类箱默认收起，有箱才给「查看认领箱」；点开展开本地全部箱子，顶上总标题「认领箱」（reserve.expandedBoxHeading），原各箱名保留；
    /// 同一按钮写「返回」，点了只收起箱区（人物页还在）。
    /// </summary>
    [Test]
    public void Boxes_DefaultFolded_OpenShowsAllWithHeading_ReturnFolds()
    {
        E.DebugDiscardListKit(S);
        G.Notify();
        var cut = Ctx.RenderComponent<PartyPanel>();
        Assert.That(cut.Find("[data-pt=boxes-toggle]").TextContent, Is.EqualTo("查看认领箱"));
        Assert.That(cut.FindAll("[data-pt=boxes]"), Is.Empty, "默认收起");
        Assert.That(cut.FindAll(".remains-card"), Is.Empty);

        cut.Find("[data-pt=boxes-toggle]").Click();
        Assert.That(cut.Find("[data-pt=boxes-title]").TextContent, Is.EqualTo(Contract.B36a("reserve.expandedBoxHeading")));
        Assert.That(cut.FindAll(".remains-card"), Has.Count.EqualTo(4), "本地全部四个箱");
        var heads = cut.FindAll(".remains-card > div > b").Select(b => b.TextContent).ToArray();
        Assert.That(heads, Is.EqualTo(new[] { "试用者的遗物", "认领箱：试用者退役寄存", "试用远征回收箱", "试用旧实例遗物认领箱" }), "原各箱名");
        Assert.That(cut.Find("[data-pt=boxes-toggle]").TextContent, Is.EqualTo("返回"));

        cut.Find("[data-pt=boxes-toggle]").Click();
        Assert.That(cut.FindAll("[data-pt=boxes]"), Is.Empty, "返回只收起箱区");
        Assert.That(cut.FindAll(".party-row"), Is.Not.Empty, "辅助：人物页还在");
    }

    /// <summary>配对：当前地点没有任何合法箱就不出「查看认领箱」。</summary>
    [Test]
    public void NoBoxes_NoToggle()
    {
        S.Progress.Flags.UnionWith(new[] { "hub_registered", "church_route_record", "academy_safety_record" });
        E.DebugRecruit(S, Lin);
        var cut = Ctx.RenderComponent<PartyPanel>();
        Assert.That(cut.FindAll("[data-pt=boxes-toggle]"), Is.Empty);
    }

    /// <summary>Q891 A：在展开的箱区里开行末丢弃子区，箱区仍展开（子窗不收起）；换到别处再回来是默认收起。</summary>
    [Test]
    public void Boxes_StayOpenWithDiscardSub_FoldAfterLocationChange()
    {
        E.DebugDiscardListKit(S);
        G.Notify();
        var cut = Ctx.RenderComponent<PartyPanel>();
        cut.Find("[data-pt=boxes-toggle]").Click();
        cut.Find(".remains-card [data-pt=row-discard]").Click();
        Assert.That(G.Discarding, Is.Not.Null, "起点：丢弃子区已开");
        Assert.That(cut.FindAll("[data-pt=boxes]"), Has.Count.EqualTo(1), "子区开着时箱区保持展开");

        var home = S.CurrentLocation;
        E.DebugTeleport(S, "field");
        G.Notify();
        E.DebugTeleport(S, home);
        G.Notify();
        cut.Render();
        Assert.That(cut.FindAll("[data-pt=boxes]"), Is.Empty, "换过地点回默认收起");
        Assert.That(cut.Find("[data-pt=boxes-toggle]").TextContent, Is.EqualTo("查看认领箱"));
    }

    /// <summary>
    /// B36A-Q953-01：回收箱卡「取出…」（GameSession.ShowDungeonBoxes，LocationStrip 按钮同一入口）视同点了「查看认领箱」：自动展开、切到人物页，滚到并高亮对应回收箱一次；
    /// B36A-Q953-02：只是导航，不取物不耗时。「返回」照常收起。
    /// </summary>
    [Test]
    public void BoxJump_AutoExpands_ScrollsToTarget_NoTake()
    {
        var key = GameEngine.RecoveryBoxKey("d01");
        S.Remains[key] = new RemainsState { Name = "远征回收箱", LocationId = S.CurrentLocation, Minute = S.GameMinute, Kind = RemainsState.RecoveryKind };
        var meat = E.CreateItems(S, "raw_meat", 2, ItemInstance.RemainsOwner(key)).Single();
        var minute = S.GameMinute;
        var cut = Ctx.RenderComponent<PartyPanel>();
        Assert.That(cut.FindAll("[data-pt=boxes]"), Is.Empty, "起点：默认收起");

        cut.InvokeAsync(() => G.ShowDungeonBoxes("d01"));
        Assert.That(G.DetailTab, Is.EqualTo(DetailTab.Character));
        Assert.That(cut.FindAll("[data-pt=boxes]"), Has.Count.EqualTo(1), "自动展开");
        Assert.That(cut.FindAll($"#remains-{key}"), Has.Count.EqualTo(1), "目标箱在页面上");
        Assert.That(Ctx.JSInterop.Invocations.Any(i => i.Identifier == "survival.scrollFlash" && (string?)i.Arguments[0] == "remains-" + key), Is.True, "滚到并高亮这一箱");
        Assert.That((meat.Owner, S.GameMinute), Is.EqualTo((ItemInstance.RemainsOwner(key), minute)), "不取物、不耗时");

        cut.Find("[data-pt=boxes-toggle]").Click();
        Assert.That(cut.FindAll("[data-pt=boxes]"), Is.Empty, "返回照常收起");
    }
}
