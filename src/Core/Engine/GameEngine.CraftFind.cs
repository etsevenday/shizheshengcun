using System;
using System.Collections.Generic;
using System.Linq;
using Survival.Core.Config;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>
    /// B67「制作检索、工艺链与来源定位」的内核只读规则（开发记录 doc/开发记录/B67 制作检索、工艺链与来源定位.md）：
    /// <list type="bullet">
    /// <item>R349（S08 D25、S14 D25-01；02 Q1042 A）：制作入口「搜索配方或材料」——去首尾空白后把输入当一个连续片段，
    /// 在已公开的配方名、产物名或单条材料名里按字面查找（中间空格照字面，不拆词、不跨字段拼接、不匹配拼音），结果与当前分类取交集、保持原顺序。</item>
    /// <item>R353（S08 D28、S14 D28-01；02 Q1040 B、Q1041 A）：物品详情「已知工艺」——只取 recipes 目录里已公开的手工制作、设施建造与各工作区配方
    /// （锅食谱、晾制、拆解与独立配置的火堆直烤、烧水、尸体处理都不列）；有产出关联为上一步、有原料关联为后续；工序写“{用料} → {工位} → {配方名}”，
    /// 无实体工位写「手工制作」。</item>
    /// <item>R347（S05 D22、S14 D22-01／02）：来源行能解析到当前地点、当前区里可见的真实资源卡（按资源点实例 NodeId）时才可“定位来源”。</item>
    /// </list>
    /// 全部只读、0 分钟，不预留、不抽签、不写存档。
    /// </summary>
    public sealed partial class GameEngine
    {
        // ───────────── R349 D25／Q1042：配方检索 ─────────────

        /// <summary>S08 D25、S14 D25-01：制作入口搜索框文字。</summary>
        public const string CraftSearchPlaceholder = "搜索配方或材料";

        /// <summary>S08 D25：搜索无匹配。</summary>
        public const string NoMatchRecipeText = "没有符合条件的配方。";

        /// <summary>Q1042 A：只去首尾空白，中间空格保留原样（按字面）。</summary>
        public static string NormalizeCraftQuery(string? query) => (query ?? "").Trim();

        /// <summary>
        /// Q1042 A／S08 D25：去首尾空白后为空恒命中（沿该分类原列表）；否则输入作为一个连续片段，在配方名、任一产物名或任一条材料名里按字面查找。
        /// 材料只取配方原料（不含燃料与催化工具，见开发记录 B67 偏差与解读）；名字取当前公开名（<see cref="ItemName"/>）。
        /// </summary>
        public bool RecipeMatchesQuery(RecipeConfig rc, string? query)
        {
            var q = NormalizeCraftQuery(query);
            if (q.Length == 0) return true;
            if (rc.Name.Contains(q, StringComparison.Ordinal)) return true;
            if (rc.Outputs.Keys.Any(id => ItemName(id).Contains(q, StringComparison.Ordinal))) return true;
            return rc.Inputs.Keys.Any(id => ItemName(id).Contains(q, StringComparison.Ordinal));
        }

        /// <summary>D25：搜索结果与当前分类的可见列表取交集，保持原顺序。</summary>
        public List<RecipeConfig> FilterRecipesByQuery(IEnumerable<RecipeConfig> list, string? query) =>
            list.Where(r => RecipeMatchesQuery(r, query)).ToList();

        // ───────────── R353 D28／Q1040／Q1041：已知工艺 ─────────────

        /// <summary>Q1041 A：无实体工位的配方在工位处写「手工制作」。</summary>
        public const string ChainHandStationText = "手工制作";

        /// <summary>
        /// Q1040 B：能作为「已知工艺」节点的配方——recipes 目录里此刻已公开（Q600 <see cref="RecipeVisible"/>）的手工制作入口配方（徒手制造、设施建造、维护与修补）
        /// 与各设施工作区配方；锅食谱、晾制、拆解不列。
        /// </summary>
        public bool InCraftChainScope(GameState s, RecipeConfig rc) =>
            (HandCraftTabOf(rc) != null || IsWorkspaceRecipe(rc)) && RecipeVisible(s, rc);

        /// <summary>D28：上一步——把该物品作为实际产物的已公开配方（配置顺序）。</summary>
        public List<RecipeConfig> CraftChainPrev(GameState s, string itemId) =>
            Config.Recipes.Where(r => r.Outputs.ContainsKey(itemId) && InCraftChainScope(s, r)).ToList();

        /// <summary>D28：后续——把该物品作为实际原料的已公开配方（配置顺序）。</summary>
        public List<RecipeConfig> CraftChainNext(GameState s, string itemId) =>
            Config.Recipes.Where(r => r.Inputs.ContainsKey(itemId) && InCraftChainScope(s, r)).ToList();

        /// <summary>D28／Q1040-01：筛过目录与公开性后有工序才显示「已知工艺」入口。</summary>
        public bool HasCraftChain(GameState s, string itemId) =>
            CraftChainPrev(s, itemId).Count > 0 || CraftChainNext(s, itemId).Count > 0;

        /// <summary>D28 逐步展开（上一步方向）：产出这条配方任一原料的已公开配方，按配置顺序、不重复。</summary>
        public List<RecipeConfig> CraftChainPrevOf(GameState s, RecipeConfig rc) =>
            Config.Recipes.Where(r => r.Outputs.Keys.Any(rc.Inputs.ContainsKey) && InCraftChainScope(s, r)).ToList();

        /// <summary>D28 逐步展开（后续方向）：以这条配方任一产物为原料的已公开配方，按配置顺序、不重复。</summary>
        public List<RecipeConfig> CraftChainNextOf(GameState s, RecipeConfig rc) =>
            Config.Recipes.Where(r => r.Inputs.Keys.Any(rc.Outputs.ContainsKey) && InCraftChainScope(s, r)).ToList();

        /// <summary>Q1041 A：工序的关联工位——无实体工位（手工制作入口）写「手工制作」，其余写原设施名。</summary>
        public string CraftChainStation(RecipeConfig rc) => rc.Station == "hand" ? ChainHandStationText : StationName(rc.Station);

        /// <summary>
        /// D28 例句「黏土 → 陶窑 → 陶壳」「陶壳、木炭、布料 → 工作台 → 长效滤芯」、Q1041 例句「布料、草药 → 手工制作 → 草药绷带」：
        /// “{原料名（、连接，配置顺序）} → {工位} → {配方名}”。
        /// </summary>
        public string CraftChainText(RecipeConfig rc)
        {
            var parts = new List<string>();
            if (rc.Inputs.Count > 0) parts.Add(string.Join("、", rc.Inputs.Keys.Select(ItemName)));
            parts.Add(CraftChainStation(rc));
            parts.Add(rc.Name);
            return string.Join(" → ", parts);
        }

        // ───────────── R347 D22：当前场景的真实资源卡 ─────────────

        /// <summary>S05 D22、S14 D22：来源行详情里的本地定位按钮。</summary>
        public const string SourceLocateHereText = "定位来源";

        /// <summary>S05 D22、S14 D22-02：按下时资源已失效。</summary>
        public const string SourceLocateFailText = "当前无法定位该来源。";

        /// <summary>
        /// 当前场景里显示的资源卡：当前地点已发现的资源点；安全屋屋内不显示资源（B17）。主界面“采集资源”组与 D22 定位共用这一处。
        /// </summary>
        public List<ResourceConfig> ResourceCardsHere(GameState s) =>
            s.CurrentZone == SafehouseZone ? new List<ResourceConfig>()
                : Config.Resources.Where(r => r.LocationId == s.CurrentLocation && s.Resources.TryGetValue(r.NodeId, out var rs) && rs.Discovered).ToList();

        /// <summary>S05 D22：按资源点实例在当前场景里找真实资源卡；不在当前地点、当前区或未发现为 null（不按同名卡、异地资源代替）。</summary>
        public ResourceConfig? ResourceCardHere(GameState s, string nodeId) => ResourceCardsHere(s).FirstOrDefault(r => r.NodeId == nodeId);
    }
}
