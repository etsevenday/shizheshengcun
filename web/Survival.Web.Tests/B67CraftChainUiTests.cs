using Bunit;
using Survival.Core.Engine;
using Survival.Core.State;
using Survival.Web.Components;
using Survival.Web.Services;

namespace Survival.Web.Tests;

/// <summary>
/// B67 R353「已知工艺」网页层行为（开发记录 doc/开发记录/B67 制作检索、工艺链与来源定位.md）：
/// 出处 S08 2026-11-20 D28 与 2026-11-24 Q1040／Q1041、S14 D25–D31 节（“实物详情可展开「已知工艺」”）与 D28-01、Q1040-01、Q1041-01，02 Q1040 B、Q1041 A。
/// 真正渲染物品详情（ItemDetail），展开「已知工艺」组，点工序，断工序文字、跳转与“不移动、不开工”。
/// </summary>
public class B67CraftChainUiTests : WebTestBase
{
    private IRenderedComponent<ItemDetail> SelectGround(string itemId, int n = 1)
    {
        E.CreateItems(S, itemId, n, S.Ground);
        G.Select(new Selection { Kind = SelectionKind.ItemGroup, Id = itemId, Owner = S.Ground });
        return Ctx.RenderComponent<ItemDetail>();
    }

    /// <summary>展开「已知工艺」组（默认收起，组头按原折叠组写法）。</summary>
    private static void OpenFold(IRenderedComponent<ItemDetail> cut)
    {
        var head = cut.Find($"[data-fold={RailGroups.CraftChain}] .rail-fold-head");
        Assert.That(head.QuerySelector(".fold-name")!.TextContent, Is.EqualTo("已知工艺"));
        Assert.That(head.GetAttribute("aria-expanded"), Is.EqualTo("false"), "没有明确偏好时默认收起");
        head.Click();
    }

    private static List<string> Steps(IRenderedComponent<ItemDetail> cut) =>
        cut.FindAll("[data-pt=chain-list] > [data-pt=chain-node] > .chain-row [data-pt=chain-step]").Select(b => b.TextContent).ToList();

    /// <summary>
    /// D28 例句与 D28-01：陶壳详情展开「已知工艺」，先列上一步「黏土 → 陶窑 → 陶壳」，再列后续「陶壳、木炭、布料 → 工作台 → 长效滤芯」
    /// 与「陶壳、木板、布料 → 手工制作 → 建固定净水台」；营地已建工作台时点「长效滤芯」进工作台工作区并选中该配方——
    /// 陶壳仍是选中的实物、不移动、不开工、不扣物、不推进时间。
    /// </summary>
    [Test]
    public void D28_01_CeramicShell_PrevNext_ClickNextOpensWorkspace()
    {
        E.DebugLearnAllKnowledge(S);
        E.DebugBuildAllWorkshops(S);
        var cut = SelectGround("ceramic_shell");
        OpenFold(cut);
        Assert.That(Steps(cut), Is.EqualTo(new[] { "黏土 → 陶窑 → 陶壳", "陶壳、木炭、布料 → 工作台 → 长效滤芯", "陶壳、木板、布料 → 手工制作 → 建固定净水台" }));
        var dirs = cut.FindAll("[data-pt=chain-list] > [data-pt=chain-node]").Select(n => n.GetAttribute("data-dir")).ToList();
        Assert.That(dirs, Is.EqualTo(new[] { "prev", "next", "next" }));

        var sel = G.Selected.Key;
        var shell = S.Items.Single(i => i.ItemId == "ceramic_shell");
        var (owner, count, minute, total) = (shell.Owner, shell.Count, S.GameMinute, S.Items.Sum(i => i.Count));
        cut.Find("[data-pt=chain-node][data-recipe=ex_filter_cartridge] [data-pt=chain-step]").Click();
        Assert.That(G.Panel, Is.EqualTo("craft"));
        Assert.That(G.CraftSite?.StationId, Is.EqualTo("workbench"), "实体工位可用时按原入口进工作区");
        Assert.That(G.CraftRecipe, Is.EqualTo("ex_filter_cartridge"));
        Assert.That(G.Selected.Key, Is.EqualTo(sel), "浏览不移动选中实物");
        Assert.That((shell.Owner, shell.Count, S.GameMinute, S.Items.Sum(i => i.Count)), Is.EqualTo((owner, count, minute, total)), "不移动、不扣物、不推进时间");
        Assert.That(S.Main, Is.Null, "不自动开工");
        Assert.That(S.Items.Any(i => i.ReservedBy != null), Is.False, "不预留");
    }

    /// <summary>
    /// Q1041-01 前半：草药绷带的工位显示「手工制作」，整步「布料、草药 → 手工制作 → 草药绷带」；点它走原“手工制作”入口并选中草药绷带（不开工）。
    /// </summary>
    [Test]
    public void Q1041_01_Bandage_HandCraftEntry()
    {
        var cut = SelectGround("bandage");
        OpenFold(cut);
        var step = cut.Find("[data-pt=chain-node][data-recipe=bandage][data-dir=prev] [data-pt=chain-step]");
        Assert.That(step.TextContent, Is.EqualTo("布料、草药 → 手工制作 → 草药绷带"));
        step.Click();
        Assert.That((G.Panel, G.CraftSite, G.CraftRecipe), Is.EqualTo(("craft", (CraftWorkspace?)null, "bandage")));
        Assert.That(S.Main, Is.Null);
    }

    /// <summary>
    /// Q1041-01 后半：营地没有工作台时点「长效滤芯」——不进制作页，留在物品详情，在该步下只读显示原门槛说明（含「需要工作台」）；
    /// 不投料、不开工；再点一次收起说明。配对见 D28_01（有工作台时进工作区）。
    /// </summary>
    [Test]
    public void Q1041_01_StationUnavailable_StaysReadOnlyWithReason()
    {
        E.DebugLearnAllKnowledge(S);
        S.Skills["crafting"].Level = 3;
        var cut = SelectGround("ceramic_shell");
        OpenFold(cut);
        var minute = S.GameMinute;
        var node = "[data-pt=chain-node][data-recipe=ex_filter_cartridge]";
        cut.Find($"{node} [data-pt=chain-step]").Click();
        Assert.That(G.Panel, Is.Null, "找不到合法工位时不进工作区");
        var why = cut.FindAll($"{node} [data-pt=chain-why] .warn-text").Select(x => x.TextContent).ToList();
        Assert.That(why, Has.Count.EqualTo(1));
        Assert.That(why[0], Does.StartWith("需要工作台"));
        Assert.That((S.Main, S.GameMinute), Is.EqualTo(((ActionState?)null, minute)));

        cut.Find($"{node} [data-pt=chain-step]").Click();
        Assert.That(cut.FindAll($"{node} [data-pt=chain-why]"), Is.Empty, "再点收起");
    }

    /// <summary>
    /// Q1040-01：生肉只连着锅食谱与晾制（不作节点），查不到符合条件的工序——详情不出「已知工艺」空入口。配对：陶壳有入口（D28_01）。
    /// 悬停只读详情也不出（点工序会进制作页）。
    /// </summary>
    [Test]
    public void Q1040_01_RawMeat_NoEntry_ReadOnlyHoverNoEntry()
    {
        E.DebugLearnAllKnowledge(S);
        var cut = SelectGround("raw_meat");
        Assert.That(cut.FindAll($"[data-fold={RailGroups.CraftChain}]"), Is.Empty);

        E.CreateItems(S, "ceramic_shell", 1, S.Ground);
        var shellSel = new Selection { Kind = SelectionKind.ItemGroup, Id = "ceramic_shell", Owner = S.Ground };
        var formal = Ctx.RenderComponent<ItemDetail>(p => p.Add(x => x.Sel, shellSel));
        Assert.That(formal.FindAll($"[data-fold={RailGroups.CraftChain}]"), Has.Count.EqualTo(1), "配对：陶壳正式详情有入口");
        var hover = Ctx.RenderComponent<ItemDetail>(p => p.Add(x => x.Sel, shellSel).Add(x => x.ReadOnly, true));
        Assert.That(hover.FindAll($"[data-fold={RailGroups.CraftChain}]"), Is.Empty);
    }

    /// <summary>
    /// D28-01「限制查看某条工艺后对应节点消失」：制造3级时布料的后续里有「净水操作套件」（工作台，显现条件 制造3级）；降回未满足显现条件后该节点消失。
    /// </summary>
    [Test]
    public void D28_01_RestrictedRecipe_NodeDisappears()
    {
        S.Skills["crafting"].Level = 3;
        var cut = SelectGround("cloth");
        OpenFold(cut);
        Assert.That(cut.FindAll("[data-pt=chain-node][data-recipe=make_filter_token]"), Has.Count.EqualTo(1));
        Assert.That(cut.Find("[data-pt=chain-node][data-recipe=make_filter_token] [data-pt=chain-step]").TextContent, Is.EqualTo("布料、塑料 → 工作台 → 净水操作套件"));
        S.Skills["crafting"].Level = 0;
        G.Notify();
        cut.Render();
        Assert.That(cut.FindAll("[data-pt=chain-node][data-recipe=make_filter_token]"), Is.Empty);
        Assert.That(cut.FindAll("[data-pt=chain-node][data-recipe=bandage]"), Has.Count.EqualTo(1), "配对：其余公开节点仍在");
    }

    /// <summary>
    /// D28“玩家可逐步展开”：陶壳的后续「长效滤芯」有下一层，展开后列以长效滤芯为原料的「脏水、长效滤芯 → 手工制作 → 滤芯处理脏水」；
    /// 没有下一层的工序（陶窑的陶壳：没有产黏土的公开配方）不给展开键。展开只是阅读：不改选中、不开面板。
    /// </summary>
    [Test]
    public void D28_StepwiseExpand()
    {
        E.DebugLearnAllKnowledge(S);
        var cut = SelectGround("ceramic_shell");
        OpenFold(cut);
        Assert.That(cut.FindAll("[data-pt=chain-node][data-recipe=ex_ceramic_shell] > .chain-row [data-pt=chain-toggle]"), Is.Empty);
        var node = "[data-pt=chain-list] > [data-pt=chain-node][data-recipe=ex_filter_cartridge]";
        var toggle = cut.Find($"{node} > .chain-row [data-pt=chain-toggle]");
        Assert.That(toggle.GetAttribute("aria-expanded"), Is.EqualTo("false"));
        Assert.That(cut.FindAll($"{node} [data-pt=chain-node]"), Is.Empty);
        toggle.Click();
        var kids = cut.FindAll($"{node} > .chain-list > [data-pt=chain-node]");
        Assert.That(kids, Has.Count.EqualTo(1));
        Assert.That(kids[0].GetAttribute("data-recipe"), Is.EqualTo("ex_cartridge_water"));
        Assert.That(kids[0].QuerySelector("[data-pt=chain-step]")!.TextContent, Is.EqualTo("脏水、长效滤芯 → 手工制作 → 滤芯处理脏水"));
        Assert.That(cut.Find($"{node} > .chain-row [data-pt=chain-toggle]").GetAttribute("aria-expanded"), Is.EqualTo("true"));
        Assert.That(G.Panel, Is.Null);
    }
}
