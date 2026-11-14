using System;
using System.Collections.Generic;
using System.Linq;
using Survival.Core.Config;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>
    /// B20 R64 人口区（A09「E5 二十四新增野外敌池」「最终敌方资源/人口接口」「主线普通敌池具体权重」、R05/A09「Q31 新增135节点…」、
    /// node-expansion-rules.json worldPopulation）：9 个人口区（mainline 4 区 12/12/12/12、ex_pop 5 区 12/12/16/12/8），每地本地上限 4。
    /// - 首次实际到达某节点：按该节点 firstPopulation（多数 2，m02 为 0）补到目标只数，同时受人口区剩余上限与本地上限约束；该区记为已进入。
    /// - 已进入的区：与旧 8 地点同一刷新格点（第2日00:00起每120分钟）各判一次 25%，避开玩家当前节点，等概率选有空位的节点、按池内权重选种；
    ///   mainline 区先按 96/3.5/0.5 抽强度层，再按节点逐敌层表在该层内抽种（B22h，Q192 选 C：未开放或该节点该层空本次不刷、不重抽）；失败不积欠，未进入的区不追补。
    /// - 巡游：每180分钟每只未交战敌 20% 沿物理边走到同区、有空位的相邻节点（不进城市/地牢、不跨区），无合法边原地。
    /// - 旧 8 地点的全局 12、本地上限、初始 6 只与刷新池不变，与新区互不共享上限（node-expansion-rules legacyStarterPopulation）。
    /// </summary>
    public sealed partial class GameEngine
    {
        private Dictionary<string, PopulationRegionConfig>? _regions;
        private Dictionary<string, PopulationRegionConfig> Regions => _regions ??= WE.Regions.ToDictionary(r => r.Id);

        /// <summary>某节点所属的人口区（有敌池、在该区节点表里）；不属于任何新人口区为 null（旧 8 地点、城市、地牢）。</summary>
        public PopulationRegionConfig? PopRegionOf(string loc) =>
            !NpcSafePlace(loc) && _locs.TryGetValue(loc, out var lc) && lc.PopulationRegionId != null && lc.PopulationWeights != null && lc.PopulationWeights.Count > 0
            && Regions.TryGetValue(lc.PopulationRegionId, out var r) && r.Nodes.Contains(loc) ? r : null;

        /// <summary>某人口区里的种群敌数（含交战中；固定战与旧精英挑战不计）。B41c（Q503）：有原人口账的按原账计。</summary>
        public int RegionLiveCount(GameState s, PopulationRegionConfig r) =>
            s.WorldEnemies.Count(w => w.PopAccount != null ? w.PopAccount == PopAccountRegion + r.Id : w.FixedOf == null && r.Nodes.Contains(w.LocationId));

        /// <summary>按节点池内部权重抽一种敌人（r∈[0,1)；权重不是刷新概率）。</summary>
        private string PickWeighted(Dictionary<string, int> weights, double r)
        {
            var total = weights.Values.Sum();
            var x = r * total;
            var acc = 0.0;
            foreach (var kv in weights)
            {
                acc += kv.Value;
                if (x < acc) return kv.Key;
            }
            return weights.Keys.Last();
        }

        /// <summary>
        /// 首次实际到达人口区节点：记该区已进入；节点第一次到达时补到 firstPopulation 只（目标上限，受区剩余上限与本地上限约束，
        /// 不把全区各点 2 只预生成到突破总上限）。返回给到达日志的短语。
        /// </summary>
        private void PopulateOnArrival(GameState s, string loc, List<string> notes)
        {
            if (!Config.CombatEnabled) return;
            var r = PopRegionOf(loc);
            if (r == null) return;
            s.Population.EnteredRegions.Add(r.Id);
            if (!s.Population.InitializedNodes.Add(loc)) return;
            var lc = _locs[loc];
            var added = new List<WorldEnemy>();
            // B22h（Q192 选 C）：分层节点的初次人口只按普通层（原初始数量与区/本地上限），不借初次人口绕过日层；普通层为空则 0
            var pool = InitialPool(lc);
            while (pool.Count > 0 && LocalCount(s, loc) < lc.FirstPopulation && LocalCount(s, loc) < lc.PopulationLocalCap && RegionLiveCount(s, r) < r.Cap)
                added.Add(AddWorldEnemy(s, PickWeighted(pool, s.Rng.NextDouble()), loc));
            if (added.Count > 0)
                notes.Add($"这里游荡着{string.Join("、", added.GroupBy(w => w.EnemyId).Select(g => $"{_enemies[g.Key].Name}×{g.Count()}"))}（本地敌踪，可以接近，也可以避开）");
        }

        /// <summary>新人口区的一次刷新检查（与旧 8 地点同一格点、在其后判）：只处理已进入、到了起始分钟的区，每区各判一次。</summary>
        private void RegionSpawnChecks(GameState s)
        {
            foreach (var r in WE.Regions)
            {
                if (!s.Population.EnteredRegions.Contains(r.Id) || s.GameMinute < r.SpawnStartWorldMinute) continue;
                RegionSpawnCheck(s, r);
            }
        }

        /// <summary>候选刷新节点：区内有敌池、本地未满、不是玩家当前节点，且区总数未满（含未知节点，不向玩家泄露）。</summary>
        public List<string> RegionSpawnCandidates(GameState s, PopulationRegionConfig r)
        {
            if (RegionLiveCount(s, r) >= r.Cap) return new List<string>();
            return r.Nodes.Where(n => PopRegionOf(n) == r)
                .Where(n => LocalCount(s, n) < _locs[n].PopulationLocalCap)
                .Where(n => n != s.CurrentLocation)
                .ToList();
        }

        /// <summary>B22h：初次人口用的敌池——有逐敌层表的节点（mainline 21 点）只用普通层，其余（ex_pop）用整池。</summary>
        private static Dictionary<string, int> InitialPool(LocationConfig lc) =>
            lc.PopulationLayers != null
                ? (lc.PopulationLayers.TryGetValue(OrdinaryLayer, out var o) ? o : new Dictionary<string, int>())
                : lc.PopulationWeights!;

        public const string OrdinaryLayer = "ordinary";

        /// <summary>B22h：某节点某层的层内权重（没有逐敌层表的节点只有普通层=整池；该层没有敌人为空表）。</summary>
        public Dictionary<string, int> LayerPool(string loc, string layer)
        {
            var lc = _locs[loc];
            if (lc.PopulationLayers == null) return layer == OrdinaryLayer ? lc.PopulationWeights ?? new Dictionary<string, int>() : new Dictionary<string, int>();
            return lc.PopulationLayers.TryGetValue(layer, out var p) ? p : new Dictionary<string, int>();
        }

        /// <summary>mainline 区的强度层（与旧 8 地点同口径：96/3.5/0.5，第1/3/7日开放）；r∈[0,1)。</summary>
        public static string RollLayer(Dictionary<string, double> weights, double r)
        {
            var total = weights.Values.Sum();
            var acc = 0.0;
            foreach (var kv in weights)
            {
                acc += kv.Value / total;
                if (r < acc) return kv.Key;
            }
            return weights.Keys.Last();
        }

        /// <summary>
        /// 一次人口区刷新检查。B22h（Q192 选 C，layering.policy）：mainline 四区先按原 25% 刷新尝试，再按 96/3.5/0.5 抽层；
        /// 未开放（第 1/3/7 日）或选中节点该层没有敌人即本次抽空——不重抽、不改到普通层、不跨节点补池；非空则在该层按原权重相对归一抽一种，只生一名。
        /// ex_pop 五区不分层，整池照旧。随机序列：尝试 → 抽层 → 选节点 → 层内抽种（开发假设 B22h-1）。
        /// </summary>
        private void RegionSpawnCheck(GameState s, PopulationRegionConfig r)
        {
            if (s.Rng.NextDouble() >= r.SpawnAttempt) return;
            var layer = OrdinaryLayer;
            if (r.LayerWeights != null)
            {
                layer = RollLayer(r.LayerWeights, s.Rng.NextDouble());
                // 未开放层抽中本次无刷新、不重抽（unopenedLayer no_spawn_no_reroll）
                if (r.LayerOpenWorldDay != null && r.LayerOpenWorldDay.TryGetValue(layer, out var day) && Clock.Day(s.GameMinute) < day) return;
            }
            var cands = RegionSpawnCandidates(s, r);
            if (cands.Count == 0) return;
            var loc = cands[(int)Math.Min(cands.Count - 1, Math.Floor(s.Rng.NextDouble() * cands.Count))];
            var pool = LayerPool(loc, layer);
            if (pool.Count == 0 || pool.Values.Sum() <= 0) return;   // 该节点该层空：抽空，不重抽、不重分配
            AddWorldEnemy(s, PickWeighted(pool, s.Rng.NextDouble()), loc);
        }

        /// <summary>人口区敌人的巡游目标：物理相邻、同一人口区、本地未满的节点（不进城市/地牢、不跨区）。</summary>
        public List<string> RegionPatrolTargets(GameState s, string from)
        {
            var r = PopRegionOf(from);
            if (r == null) return new List<string>();
            return PhysicalNeighbors(from).Where(l => PopRegionOf(l) == r && LocalCount(s, l) < _locs[l].PopulationLocalCap)
                .Distinct().OrderBy(l => l, StringComparer.Ordinal).ToList();
        }

        /// <summary>开发入口：人口区计数（已进入的区写“已进入”，其余“未进入（不刷新）”）。</summary>
        public List<(PopulationRegionConfig Region, int Live, bool Entered)> RegionSummary(GameState s) =>
            WE.Regions.Select(r => (r, RegionLiveCount(s, r), s.Population.EnteredRegions.Contains(r.Id))).ToList();

        /// <summary>开发入口：刷新检查没出敌人时的原因（没有已进入的区 / 第2日00:00之前不刷新 / 25%未中或区满）。</summary>
        public string RegionSpawnIdleReason(GameState s)
        {
            var entered = WE.Regions.Where(r => s.Population.EnteredRegions.Contains(r.Id)).ToList();
            if (entered.Count == 0) return "还没有进入任何人口区（到过区内任一节点才开始刷新）";
            if (entered.All(r => s.GameMinute < r.SpawnStartWorldMinute))
                return $"人口区从第{Clock.Day(entered.Min(r => r.SpawnStartWorldMinute))}日00:00起才判刷新（现在{Clock.DayTime(s.GameMinute)}）";
            return "25%未中，或抽到还没开放的层（稀有第3日、强敌第7日起）/选中节点这一层没有敌人（抽空不重抽），或区内节点都满/只剩你所在的节点";
        }

        /// <summary>开发与测试：立即对已进入的人口区做一次刷新检查（用存档内随机数），返回新生成的敌人。</summary>
        public List<WorldEnemy> DebugRegionSpawnCheck(GameState s)
        {
            var before = s.WorldEnemies.Select(w => w.Id).ToHashSet();
            RegionSpawnChecks(s);
            return s.WorldEnemies.Where(w => !before.Contains(w.Id)).ToList();
        }
    }
}
