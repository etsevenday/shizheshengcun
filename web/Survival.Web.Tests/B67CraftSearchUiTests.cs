using Bunit;
using Survival.Core.Engine;
using Survival.Web.Components;

namespace Survival.Web.Tests;

/// <summary>
/// B67 R349「搜索配方或材料」网页层行为（开发记录 doc/开发记录/B67 制作检索、工艺链与来源定位.md）：
/// 出处 S08 2026-11-20 D25 与 2026-11-24 Q1042、S14 D25-01／Q1042-01、02 Q1042 A（Q949 A 沿用）。
/// 真正渲染制作窗口，在搜索框里输入，断左列、无匹配句、来源侧页与所选来源。
/// </summary>
public class B67CraftSearchUiTests : WebTestBase
{
    private List<string> Rows(IRenderedComponent<CraftOverlay> cut) =>
        cut.FindAll(".recipe-list [data-recipe]").Select(b => b.GetAttribute("data-recipe")!).ToList();

    private IRenderedComponent<CraftOverlay> OpenHand(string recipe)
    {
        G.OpenHandCraft(recipe);
        Assert.That((G.Panel, G.CraftRecipe), Is.EqualTo(("craft", recipe)));
        return Ctx.RenderComponent<CraftOverlay>();
    }

    /// <summary>
    /// D25-01：手工制作“徒手制造”分类里有「搜索配方或材料」；搜「布料」左列只剩本分类公开使用布料的 11 条（同内核测试手算），
    /// 搜已公开产物「草药绷带」只剩草药绷带；无匹配写「没有符合条件的配方。」；清空回到原列表。
    /// 只改可见列表：所选配方与本次制作区不变（D25“匹配及清除只改变当前可见列表”）。
    /// </summary>
    [Test]
    public void D25_01_SearchBox_FiltersLeftList_NoMatch_Clear()
    {
        var cut = OpenHand("knife");
        Assert.That(G.CraftTab, Is.EqualTo(GameEngine.TabHand));
        var box = cut.Find("[data-pt=craft-search]");
        Assert.That(box.GetAttribute("placeholder"), Is.EqualTo("搜索配方或材料"));
        Assert.That(box.GetAttribute("aria-label"), Is.EqualTo("搜索配方或材料"));
        var before = Rows(cut);
        Assert.That(before, Has.Count.GreaterThan(11));

        box.Input("布料");
        var hit = Rows(cut);
        Assert.That(hit, Has.Count.EqualTo(11));
        Assert.That(hit, Is.EquivalentTo(new[]
        {
            "bandage", "make_wooden_bow", "make_crossbow", "make_knuckle_wraps", "make_wooden_shield", "make_padded_vest",
            "make_steady_ring", "make_work_shirt", "make_work_trousers", "make_filter_media", "ex_rally_patch",
        }));
        Assert.That(hit.Select(id => before.IndexOf(id)), Is.Ordered, "保持原配方顺序");
        Assert.That(G.CraftRecipe, Is.EqualTo("knife"), "只改可见列表，不改所选配方");
        Assert.That(cut.FindAll("[data-pt=craft-run]"), Has.Count.EqualTo(1));
        Assert.That(cut.FindAll("[data-pt=craft-no-match]"), Is.Empty);

        cut.Find("[data-pt=craft-search]").Input("草药绷带");
        Assert.That(Rows(cut), Is.EqualTo(new[] { "bandage" }));

        cut.Find("[data-pt=craft-search]").Input("草药 绷带");
        Assert.That(Rows(cut), Is.Empty);
        Assert.That(cut.Find("[data-pt=craft-no-match]").TextContent, Is.EqualTo("没有符合条件的配方。"));
        Assert.That(cut.FindAll("[data-craft-empty]"), Is.Empty, "不是“当前分类没有可显示的配方。”");

        cut.Find("[data-pt=craft-search]").Input("");
        Assert.That(Rows(cut), Is.EqualTo(before), "清空沿该分类原列表");
        Assert.That(cut.FindAll("[data-pt=craft-no-match]"), Is.Empty);
    }

    /// <summary>D25“结果与当前分类取交集”：搜索词保留时改选“设施建造”分类，左列只剩建造配方里用布料的 4 条，选中其中第一条。</summary>
    [Test]
    public void D25_QueryCombinesWithTab()
    {
        var cut = OpenHand("knife");
        cut.Find("[data-pt=craft-search]").Input("布料");
        cut.Find($"[data-tab={GameEngine.TabBuild}]").Click();
        var rows = Rows(cut);
        Assert.That(rows, Is.EquivalentTo(new[] { "bed", "safehouse_base", "safehouse_bed", "build_water_station" }));
        Assert.That(G.CraftRecipe, Is.EqualTo(rows[0]));
        Assert.That(cut.Find("[data-pt=craft-search]").GetAttribute("value"), Is.EqualTo("布料"), "切分类保留搜索词");
    }

    /// <summary>
    /// Q1042-01：草药绷带的“布料”来源侧页开着时搜「 草药 」——左列刷新只剩草药绷带；中列来源列表、所选来源与内右详情保持，本次制作区仍隐藏；
    /// 搜「草药 绷带」左列无匹配，侧页仍在。之后点分类按 Q949 一次退出侧页（搜索词保留）。
    /// </summary>
    [Test]
    public void Q1042_01_TypingInSources_KeepsPaneAndPick_TabExits()
    {
        S.Items.RemoveAll(i => i.ItemId == "cloth");
        E.CreateItems(S, "herb", 1, S.Ground);
        var cut = OpenHand("bandage");
        cut.Find("[data-source-item=cloth]").Click();
        Assert.That(G.CraftSourcesOpen, Is.True);
        cut.Find("[data-pt=src-row]").Click();
        var key = G.CraftSourceKey;
        Assert.That(key, Is.Not.Null);

        cut.Find("[data-pt=craft-search]").Input(" 草药 ");
        Assert.That(Rows(cut), Is.EqualTo(new[] { "bandage" }));
        Assert.That((G.CraftSourcesOpen, G.CraftSourceItem, G.CraftSourceKey, G.CraftRecipe), Is.EqualTo((true, "cloth", key, "bandage")));
        Assert.That(cut.Find("[data-pt=craft-sources]").GetAttribute("data-sources"), Is.EqualTo("cloth"));
        Assert.That(cut.FindAll("[data-pt=src-detail-title]"), Has.Count.EqualTo(1), "选中详情保持");
        Assert.That(cut.FindAll("[data-pt=craft-run]"), Is.Empty, "制作区仍隐藏");

        cut.Find("[data-pt=craft-search]").Input("草药 绷带");
        Assert.That(Rows(cut), Is.Empty);
        Assert.That(cut.Find("[data-pt=craft-no-match]").TextContent, Is.EqualTo("没有符合条件的配方。"));
        Assert.That((G.CraftSourcesOpen, G.CraftSourceKey), Is.EqualTo((true, key)), "清词、无匹配都不退出侧页");

        cut.Find($"[data-tab={GameEngine.TabAll}]").Click();
        Assert.That(G.CraftSourcesOpen, Is.False, "Q949：切分类一次退出来源侧页");
        Assert.That(G.CraftSourceKey, Is.Null);
        Assert.That(G.CraftQuery, Is.EqualTo("草药 绷带"));
    }

    /// <summary>
    /// D25“离开当前制作工作区时清除搜索词，从合法子层返回同一工作区则保留”：来源地图返回侧页、再返回配方，搜索词都在；
    /// 关掉制作窗口再从顶栏打开，搜索框为空、列表回原样。
    /// </summary>
    [Test]
    public void D25_QueryKeptOnSubLayerReturn_ClearedOnLeave()
    {
        S.Items.RemoveAll(i => i.ItemId is "stone" or "fiber");
        E.CreateItems(S, "stone", 1, S.Ground);
        S.Resources["field_fiber"].Discovered = true;
        var cut = OpenHand("knife");
        cut.Find("[data-pt=craft-search]").Input("石刀");
        Assert.That(Rows(cut), Is.EqualTo(new[] { "knife" }));
        cut.Find("[data-source-item=fiber]").Click();
        cut.Find("[data-pt=src-locate]").Click();
        Assert.That(G.Panel, Is.EqualTo("map"));
        G.PanelEscape();   // 地图「返回材料来源」与 ESC 同一入口
        Assert.That((G.Panel, G.CraftSourcesOpen), Is.EqualTo(("craft", true)));
        cut.Render();
        Assert.That(cut.Find("[data-pt=craft-search]").GetAttribute("value"), Is.EqualTo("石刀"), "子层返回保留");
        Assert.That(Rows(cut), Is.EqualTo(new[] { "knife" }));
        cut.Find("[data-pt=src-back]").Click();
        Assert.That(G.CraftQuery, Is.EqualTo("石刀"));

        G.PanelEscape();
        Assert.That(G.Panel, Is.Null);
        Assert.That(G.CraftQuery, Is.EqualTo(""), "离开制作窗口即清除");
        G.OpenHandCraft();
        cut.Render();
        Assert.That(cut.Find("[data-pt=craft-search]").GetAttribute("value"), Is.EqualTo(""));
        Assert.That(Rows(cut), Has.Count.GreaterThan(1));
    }

    /// <summary>D25 配对：拖拽候选列表不是配方入口，不出搜索框（候选按原规则列）。</summary>
    [Test]
    public void Candidates_NoSearchBox()
    {
        G.OpenHandCraft("knife");
        G.CraftCandidates = E.DropRecipeCandidates(S, new[] { "stone", "fiber" }, false);
        Assert.That(G.CraftCandidates, Is.Not.Empty);
        var cut = Ctx.RenderComponent<CraftOverlay>();
        Assert.That(cut.FindAll("[data-pt=craft-search]"), Is.Empty);
        Assert.That(cut.FindAll(".recipe-list [data-recipe]"), Is.Not.Empty);
    }
}
