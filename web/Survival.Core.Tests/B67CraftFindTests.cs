using Survival.Core.Config;
using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// B67「制作检索、工艺链与来源定位」内核部分（开发记录 doc/开发记录/B67 制作检索、工艺链与来源定位.md）：
/// R349 配方检索（S08 D25、S14 D25-01；02 Q1042 A）、R353 已知工艺（S08 D28、S14 D28-01；02 Q1040 B、Q1041 A），
/// R347 当前场景真实资源卡（S05 D22、S14 D22-01／02）。期望值都是策划案原句或按策划案规则对配置数据手算的字面量。
/// </summary>
public class B67CraftFindTests : TestBase
{
    private static RecipeConfig Rc(string id) => Cfg.Recipes.Single(r => r.Id == id);

    private List<string> HandSearch(GameState s, string tab, string q) =>
        E.FilterRecipesByQuery(E.HandCraftRecipes(s, tab), q).Select(r => r.Id).ToList();

    // ───────────── R349 D25／Q1042 ─────────────

    /// <summary>S08 D25、S14 D25-01 原句：搜索框「搜索配方或材料」，无匹配「没有符合条件的配方。」。</summary>
    [Test]
    public void D25_Texts_Verbatim()
    {
        Assert.That(GameEngine.CraftSearchPlaceholder, Is.EqualTo("搜索配方或材料"));
        Assert.That(GameEngine.NoMatchRecipeText, Is.EqualTo("没有符合条件的配方。"));
    }

    /// <summary>
    /// D25-01：在手工制作“徒手制造”分类搜「布料」——结果只含本分类、公开使用布料的配方（按策划规则对配置手算的 11 条）；
    /// 工作台等其它工位的布料配方（砍刀）不进入；未公开（显现条件未满足）的随行医疗包、采集手套不进入。结果顺序是原列表的子序列。
    /// </summary>
    [Test]
    public void D25_01_ClothInHandTab_OnlyThisEntryPublicRecipes()
    {
        var s = New();
        Assert.That(E.SkillLevel(s, "crafting"), Is.LessThan(3), "起点：制造不足3级，随行医疗包、采集手套的显现条件未满足");
        var res = HandSearch(s, GameEngine.TabHand, "布料");
        // 手算：徒手制造（station hand、kind craft）且原料含布料、此刻公开的配方
        var expected = new[]
        {
            "bandage", "make_wooden_bow", "make_crossbow", "make_knuckle_wraps", "make_wooden_shield", "make_padded_vest",
            "make_steady_ring", "make_work_shirt", "make_work_trousers", "make_filter_media", "ex_rally_patch",
        };
        Assert.That(res, Has.Count.EqualTo(11));
        Assert.That(res, Is.EquivalentTo(expected));
        Assert.That(res, Does.Not.Contain("make_machete"), "工作台配方不进入顶栏结果");
        Assert.That(res, Does.Not.Contain("make_field_medkit").And.Not.Contain("make_gather_gloves"), "未公开配方不进入");
        var full = E.HandCraftRecipes(s, GameEngine.TabHand).Select(r => r.Id).ToList();
        Assert.That(res.Select(id => full.IndexOf(id)), Is.Ordered.And.All.GreaterThanOrEqualTo(0), "保持原配方顺序");

        // 配对：同一词在“设施建造”分类得建造配方（与分类取交集）
        Assert.That(HandSearch(s, GameEngine.TabBuild, "布料"), Is.EquivalentTo(new[] { "bed", "safehouse_base", "safehouse_bed", "build_water_station" }));
    }

    /// <summary>D25-01 后半：搜已公开产物「草药绷带」可找到对应配方；空搜索沿该分类原列表。</summary>
    [Test]
    public void D25_01_ProductName_And_EmptyQueryKeepsList()
    {
        var s = New();
        Assert.That(HandSearch(s, GameEngine.TabHand, "草药绷带"), Is.EqualTo(new[] { "bandage" }));
        var full = E.HandCraftRecipes(s, GameEngine.TabHand).Select(r => r.Id).ToList();
        Assert.That(full.Count, Is.GreaterThan(11));
        Assert.That(HandSearch(s, GameEngine.TabHand, ""), Is.EqualTo(full));
        Assert.That(HandSearch(s, GameEngine.TabHand, "   "), Is.EqualTo(full), "只有空白等于空搜索");
    }

    /// <summary>
    /// Q1042-01：去首尾空白后「 草药 」命中草药绷带（材料名“草药”）；「草药 绷带」中间空格按字面，不拆词，不命中无空格的“草药绷带”。
    /// 未公开的病毒特效药（原料也有草药）不进入。
    /// </summary>
    [Test]
    public void Q1042_01_ContinuousLiteral_NoWordSplit()
    {
        var s = New();
        Assert.That(HandSearch(s, GameEngine.TabHand, " 草药 "), Is.EqualTo(new[] { "bandage" }));
        Assert.That(HandSearch(s, GameEngine.TabHand, "草药 绷带"), Is.Empty);
        Assert.That(E.RecipeMatchesQuery(Rc("bandage"), "草药 绷带"), Is.False);
        Assert.That(E.RecipeMatchesQuery(Rc("bandage"), "药绷"), Is.True, "配方名内的连续片段命中");
        Assert.That(E.RecipeMatchesQuery(Rc("bandage"), "绷带草药"), Is.False, "不跨字段拼接");
    }

    // ───────────── R353 D28／Q1040／Q1041 ─────────────

    /// <summary>
    /// D28 例句：陶壳的上一步「黏土 → 陶窑 → 陶壳」；后续「陶壳、木炭、布料 → 工作台 → 长效滤芯」，以及同样以陶壳为原料的设施建造
    /// 「陶壳、木板、布料 → 手工制作 → 建固定净水台」（Q1040 B 含设施建造；Q1041 A 无实体工位写「手工制作」）。燃料（陶窑的木炭）不算用料。
    /// </summary>
    [Test]
    public void D28_CeramicShell_PrevAndNext_Verbatim()
    {
        var s = New();
        E.DebugLearnAllKnowledge(s);
        var prev = E.CraftChainPrev(s, "ceramic_shell");
        Assert.That(prev.Select(r => r.Id), Is.EqualTo(new[] { "ex_ceramic_shell" }));
        Assert.That(E.CraftChainText(prev[0]), Is.EqualTo("黏土 → 陶窑 → 陶壳"));
        var next = E.CraftChainNext(s, "ceramic_shell");
        Assert.That(next.Select(r => r.Id), Is.EqualTo(new[] { "ex_filter_cartridge", "build_water_station" }));
        Assert.That(next.Select(E.CraftChainText), Is.EqualTo(new[] { "陶壳、木炭、布料 → 工作台 → 长效滤芯", "陶壳、木板、布料 → 手工制作 → 建固定净水台" }));
        Assert.That(E.HasCraftChain(s, "ceramic_shell"), Is.True);
    }

    /// <summary>Q1041-01：草药绷带的工位显示「手工制作」，整句「布料、草药 → 手工制作 → 草药绷带」。</summary>
    [Test]
    public void Q1041_01_Bandage_HandCraftStation()
    {
        var s = New();
        var prev = E.CraftChainPrev(s, "bandage");
        Assert.That(prev.Select(r => r.Id), Is.EqualTo(new[] { "bandage" }));
        Assert.That(E.CraftChainStation(prev[0]), Is.EqualTo("手工制作"));
        Assert.That(E.CraftChainText(prev[0]), Is.EqualTo("布料、草药 → 手工制作 → 草药绷带"));
        Assert.That(E.CraftChainStation(Rc("ex_filter_cartridge")), Is.EqualTo("工作台"), "配对：实体工位写原设施名");
    }

    /// <summary>
    /// Q1040-01：生肉直接相连的 recipes 只有锅食谱（谷肉炖食，kind pot）与晾制（晾制肉干，kind drying），都不作节点；
    /// 火堆直烤、尸体处理不在 recipes 里——查不到节点，不显示空入口。配对：陶壳有节点（见上）。
    /// </summary>
    [Test]
    public void Q1040_01_RawMeat_PotAndDryingExcluded_NoEntry()
    {
        var s = New();
        E.DebugLearnAllKnowledge(s);
        Assert.That(Cfg.Recipes.Count(r => r.Inputs.ContainsKey("raw_meat") || r.Outputs.ContainsKey("raw_meat")), Is.EqualTo(2), "起点：配置里与生肉相连的 recipes 正是锅食谱与晾制两条");
        Assert.That(E.CraftChainPrev(s, "raw_meat"), Is.Empty);
        Assert.That(E.CraftChainNext(s, "raw_meat"), Is.Empty);
        Assert.That(E.HasCraftChain(s, "raw_meat"), Is.False);
        Assert.That(E.InCraftChainScope(s, Rc("dry_meat")), Is.False);
        Assert.That(E.InCraftChainScope(s, Rc("ex_stew_portion")), Is.False);
        Assert.That(E.InCraftChainScope(s, Rc("salvage_equipment")), Is.False, "拆解不在制作目录");
        Assert.That(E.InCraftChainScope(s, Rc("ex_ceramic_shell")), Is.True, "配对：工作区配方在");
        Assert.That(E.InCraftChainScope(s, Rc("bandage")), Is.True, "配对：手工制作配方在");
    }

    /// <summary>
    /// D28-01「限制查看某条工艺后对应节点消失」：净水操作套件（工作台，显现条件 制造3级）未显现时不作布料的后续节点；满足显现后出现。
    /// </summary>
    [Test]
    public void D28_01_UnrevealedRecipe_NodeHidden_ThenShown()
    {
        var s = New();
        Assert.That(E.SkillLevel(s, "crafting"), Is.LessThan(3));
        Assert.That(E.CraftChainNext(s, "cloth").Select(r => r.Id), Does.Not.Contain("make_filter_token"));
        s.Skills["crafting"].Level = 3;
        Assert.That(E.CraftChainNext(s, "cloth").Select(r => r.Id), Does.Contain("make_filter_token"));
    }

    /// <summary>
    /// D28 逐步展开：陶壳上一步（陶窑配方）再往前——产黏土的公开配方；后续（长效滤芯）再往后——以长效滤芯为原料的公开配方（滤芯处理脏水，手工制作）。
    /// </summary>
    [Test]
    public void D28_StepwiseExpand_PrevOfAndNextOf()
    {
        var s = New();
        E.DebugLearnAllKnowledge(s);
        Assert.That(E.CraftChainPrevOf(s, Rc("ex_ceramic_shell")), Is.Empty, "没有产出黏土的公开配方");
        var after = E.CraftChainNextOf(s, Rc("ex_filter_cartridge"));
        Assert.That(after.Select(r => r.Id), Is.EqualTo(new[] { "ex_cartridge_water" }));
        Assert.That(E.CraftChainText(after[0]), Is.EqualTo("脏水、长效滤芯 → 手工制作 → 滤芯处理脏水"));
        Assert.That(E.CraftChainPrevOf(s, Rc("ex_filter_cartridge")).Select(r => r.Id), Does.Contain("ex_ceramic_shell"), "长效滤芯的原料陶壳由陶窑配方产出");
    }

    // ───────────── R347 D22 ─────────────

    /// <summary>
    /// S05 D22：来源行带资源点实例；当前场景资源卡只算当前地点已发现的资源点，屋内不显示（与主界面“采集资源”组同源）。
    /// </summary>
    [Test]
    public void D22_ResourceCardsHere_ByInstance_AndNodeIdOnSourceRow()
    {
        var s = New();
        E.DebugTeleport(s, "field");
        s.Resources["field_fiber"].Discovered = true;
        s.Resources["woods_fiber"].Discovered = true;
        Assert.That(E.ResourceCardHere(s, "field_fiber")?.NodeId, Is.EqualTo("field_fiber"));
        Assert.That(E.ResourceCardHere(s, "woods_fiber"), Is.Null, "异地资源不是当前场景的卡");
        var rows = E.ItemSources(s, "fiber").Where(v => v.Kind == "resource").ToList();
        Assert.That(rows.Select(v => v.NodeId), Is.EquivalentTo(new[] { "field_fiber", "woods_fiber" }));
        Assert.That(E.ItemSources(s, "fiber").Where(v => v.Kind != "resource").Select(v => v.NodeId), Has.All.Null);
        s.Resources["field_fiber"].Discovered = false;
        Assert.That(E.ResourceCardHere(s, "field_fiber"), Is.Null, "未发现不出卡");
    }
}
