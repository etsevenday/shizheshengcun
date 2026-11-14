using System;
using System.Collections.Generic;
using System.Linq;
using Survival.Core.Config;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>
    /// 持久世界敌人（v0.6 §14、§15）：全局 12 / 各地上限、新局初始分布；第2日00:00起每120分钟一次 25% 刷新检查（先抽稀有层，
    /// 再在有空位、非当前地块的合法地点里等概率选地，再按权重选种类；无合法地点不刷、不改抽更强层、无欠账）；每180分钟对未交战敌
    /// 20% 巡游一条物理相邻边；探索/清理成功完成后按本地敌对密度随机接敌，敌人只从同地现有实例中选。离线不推进。
    /// </summary>
    public sealed partial class GameEngine
    {
        private WorldEnemiesConfig WE => Config.WorldEnemies;

        /// <summary>战斗中：普通行动推进暂停，由战斗流程自己推进世界时间（B41e3 起没有“敌踪待选”状态）。</summary>
        public bool InCombatFlow(GameState s) => s.Battle != null;

        private HashSet<string>? _npcSafe;

        /// <summary>
        /// BM5a（R05「B41e3 Q563—Q569」§Q566 安全地点范围与入口约束）：势力城内 NPC／代班人固定所在地点（配置 city.npcs[].locationId 的岗位绑定，
        /// 不看此刻画面是否渲染 NPC、本人是否暂离／换代班）是安全地图——不生成、首次补充、刷新、组卡，也不是巡游、逃离、躲避重现的目标；
        /// 开发菜单放敌同样拒绝。与原城市／公共地点过滤（randomEncounterEnabled 等）同时成立。检查哨等独立危险节点不受影响。
        /// </summary>
        public bool NpcSafePlace(string loc) => (_npcSafe ??= Config.City.Npcs.Where(n => !string.IsNullOrEmpty(n.LocationId)).Select(n => n.LocationId!).ToHashSet()).Contains(loc);

        /// <summary>首个不早于 minute 的检查点：start + k×interval。</summary>
        private static int NextGrid(int minute, int start, int interval) =>
            minute <= start ? start : start + (int)Math.Ceiling((minute - start) / (double)interval) * interval;

        /// <summary>新局：按配置初始分布生成世界敌人，并设置刷新与巡游游标。</summary>
        public void InitWorldEnemies(GameState s)
        {
            if (!Config.CombatEnabled) return;
            foreach (var kv in WE.Initial)
                foreach (var id in kv.Value)
                    AddWorldEnemy(s, id, kv.Key);
            ResetWorldCursors(s);
        }

        /// <summary>刷新游标：第2日00:00起每120分钟；巡游游标：从第1日00:00起每180分钟的下一个点（都在当前分钟之后）。</summary>
        public void ResetWorldCursors(GameState s)
        {
            s.SpawnNextCheck = NextGrid(s.GameMinute + 1, WE.SpawnChecksBeginAbsoluteMinute, WE.SpawnCheckIntervalMinutes);
            s.PatrolNextCheck = NextGrid(s.GameMinute + 1, 0, WE.PatrolCheckIntervalMinutes);
        }

        private WorldEnemy AddWorldEnemy(GameState s, string enemyId, string locationId)
        {
            var cfg = _enemies[enemyId];
            var w = new WorldEnemy
            {
                Id = s.NextWorldEnemyId++,
                EnemyId = enemyId,
                LocationId = locationId,
                Hp = cfg.Hp,
                Hostile = !cfg.NeutralUntilAttacked,
                SpawnMinute = s.GameMinute,
            };
            s.WorldEnemies.Add(w);
            return w;
        }

        // ───────────── 查询 ─────────────

        /// <summary>暂时不可见：旧版逃走隐藏（到点自动解除），或 B41c 躲避隐藏（到点由重现处理清除，R05 Q420）。</summary>
        public bool Hidden(GameState s, WorldEnemy w) => (w.HiddenUntil.HasValue && w.HiddenUntil.Value > s.GameMinute) || w.EvadeHiddenUntil.HasValue;

        /// <summary>
        /// 旧 8 地点全局种群的存活敌数（含交战中、中立鹿；R05 全局 12）。B20 起只数旧 8 地点的种群：9 个人口区各有自己的上限
        /// （<see cref="RegionLiveCount"/>），固定战与旧精英挑战不计（取代原“全世界只算一个 12”的口径）。
        /// B41c（Q503）：躲避隐藏过的敌人按首次隐藏时锁定的原人口账计（隐藏期间与重现到别处后都不改账），其余按所在地点。
        /// </summary>
        public int LiveCount(GameState s) => s.WorldEnemies.Count(w => w.PopAccount != null ? w.PopAccount == PopAccountLegacy : w.FixedOf == null && InLegacyPopulation(w.LocationId));

        /// <summary>某地的种群敌数（B20：固定战与旧精英挑战的固定实例不计种群上限）。</summary>
        public int LocalCount(GameState s, string loc) => s.WorldEnemies.Count(w => w.LocationId == loc && w.FixedOf == null);

        /// <summary>
        /// 本地上限（接敌概率分母）：旧 8 地点用 worldEnemies.localCaps（R05 原值不变）；v3 新节点用所属人口区的本地上限
        /// （node-expansion-rules worldPopulation.localCap，野外 4、城市/地牢 0）。新人口区的刷新与巡游在 B20。
        /// </summary>
        public int LocalCapOf(string loc) =>
            WE.LocalCaps.ContainsKey(loc) ? WE.CapOf(loc) : _locs.TryGetValue(loc, out var lc) ? lc.PopulationLocalCap : 0;

        /// <summary>旧 8 地点的全局种群（R05 全局 12）覆盖的地点；新人口区不与它共享（node-expansion-rules legacyStarterPopulation）。</summary>
        private bool InLegacyPopulation(string loc) => WE.LocalCaps.ContainsKey(loc);

        /// <summary>某地可见的世界敌人（不含隐藏、不含交战中），按最早出现在前。</summary>
        public List<WorldEnemy> VisibleEnemiesAt(GameState s, string loc) =>
            s.WorldEnemies.Where(w => w.LocationId == loc && !w.Engaged && !Hidden(s, w) && w.FixedOf == null).OrderBy(w => w.Id).ToList();

        /// <summary>本地可参与随机接敌的敌对实例：未交战、未隐藏、敌对，按最早发现在前。</summary>
        public List<WorldEnemy> LocalHostiles(GameState s, string loc) =>
            VisibleEnemiesAt(s, loc).Where(w => w.Hostile).ToList();

        public WorldEnemy? FindWorldEnemy(GameState s, int id) => s.WorldEnemies.FirstOrDefault(w => w.Id == id);

        /// <summary>
        /// 完成探索/清理后形成随机组卡的概率：基础风险×(本地未交战敌对数/本地上限)；第1日、安全地点、无敌对、旧档历史保护期内、同地已有组卡为 0。
        /// v3 新节点（B11，Q31）：基础风险取逐点 encounterBase*Percent、分母取人口区本地上限；城市/公共城内与地牢房间不随机接敌
        /// （地牢只有固定实例遭遇）；危险度只作显示，不乘概率。旧 8 地点参数不变。
        /// </summary>
        public double EncounterChance(GameState s, string loc, ActionType type)
        {
            var en = Config.Encounters;
            if (!Config.CombatEnabled) return 0;
            if (TutorialGateOpen(s)) return 0;   // 首战未胜利：不随机接敌（R05 §4）
            if (Clock.Day(s.GameMinute) < en.NoRandomBeforeDay) return 0;
            if (en.SafeLocations.Contains(loc) || NpcSafePlace(loc)) return 0;
            if (_locs.TryGetValue(loc, out var lc) && (!lc.RandomEncounterEnabled || lc.SafeExterior)) return 0;
            if (s.GameMinute < s.EncounterGraceUntil) return 0;
            // B41a（R05 Q417）：同地已有未隐藏的存续随机组卡时，探索／清理不再形成新组
            if (VisibleGroupCardAt(s, loc) != null) return 0;
            var key = type == ActionType.Explore ? "explore" : type == ActionType.Clear ? "clear" : "";
            double risk;
            var nodePct = lc == null ? null : type == ActionType.Explore ? lc.EncounterBaseExplorePercent : type == ActionType.Clear ? lc.EncounterBaseClearPercent : null;
            if (nodePct.HasValue) risk = nodePct.Value / 100.0;
            else if (!en.FullLocalPopulationActionRisk.TryGetValue(key, out risk)) return 0;
            var cap = LocalCapOf(loc);
            if (cap <= 0) return 0;
            var n = LocalHostiles(s, loc).Count;
            return risk * n / cap;
        }

        /// <summary>探索／清理预览里的随机组卡说明（即 <see cref="GroupCardPreviewNote"/>）。</summary>
        public string EncounterNote(GameState s, string loc, ActionType type) => GroupCardPreviewNote(s, loc, type);

        // ───────────── 世界 tick：刷新与巡游 ─────────────

        private void TickWorldEnemies(GameState s)
        {
            if (!Config.CombatEnabled) return;
            foreach (var w in s.WorldEnemies.Where(w => w.HiddenUntil.HasValue && w.HiddenUntil.Value <= s.GameMinute)) w.HiddenUntil = null;
            ReappearEvaded(s);   // B41c（R05 Q420）：躲避隐藏到点重现（在刷新与巡游之前，重现当刻不查目标人口上限）
            if (s.GameMinute >= s.SpawnNextCheck)
            {
                SpawnCheck(s);
                RegionSpawnChecks(s);   // B20：已进入的人口区各判一次（同一格点，在旧 8 地点之后）
                // 只处理到达的这一个检查点，不补欠账
                s.SpawnNextCheck = NextGrid(s.GameMinute + 1, WE.SpawnChecksBeginAbsoluteMinute, WE.SpawnCheckIntervalMinutes);
            }
            if (s.GameMinute >= s.PatrolNextCheck)
            {
                Patrol(s);
                s.PatrolNextCheck = NextGrid(s.GameMinute + 1, 0, WE.PatrolCheckIntervalMinutes);
            }
        }

        /// <summary>按权重抽稀有层：r∈[0,1)。普通96%、稀有3.5%、强敌0.5%。</summary>
        public string RollRarity(double r)
        {
            var total = WE.RarityWeights.Values.Sum();
            var acc = 0.0;
            foreach (var kv in WE.RarityWeights)
            {
                acc += kv.Value / total;
                if (r < acc) return kv.Key;
            }
            return WE.RarityWeights.Keys.Last();
        }

        /// <summary>该层可刷新的地点：该层有合法敌表、本地有空位、不是玩家当前地块，且全局未满。</summary>
        public List<string> SpawnCandidates(GameState s, string rarity)
        {
            if (LiveCount(s) >= WE.GlobalCap) return new List<string>();
            return WE.Pools.Where(kv => kv.Value.TryGetValue(rarity, out var pool) && pool.Count > 0)
                .Select(kv => kv.Key)
                .Where(loc => WE.CapOf(loc) > 0 && LocalCount(s, loc) < WE.CapOf(loc))
                .Where(loc => !(WE.OnlyOutsidePlayerCurrentLocation && loc == s.CurrentLocation))
                .Where(loc => !Config.Encounters.SafeLocations.Contains(loc) && !NpcSafePlace(loc))
                .ToList();
        }

        private void SpawnCheck(GameState s)
        {
            if (s.Rng.NextDouble() >= WE.SpawnSuccessProbability) return;
            var rarity = RollRarity(s.Rng.NextDouble());
            // P06/T07（R05 §6.1）：未开放的强度层抽中则本次无刷新，不重抽、不积欠
            if (!WE.RarityOpen(rarity, s.GameMinute)) return;
            var cands = SpawnCandidates(s, rarity);
            // 该层无可用地点：本次不刷新，不改抽更强层，也不积攒
            if (cands.Count == 0) return;
            var loc = cands[(int)Math.Min(cands.Count - 1, Math.Floor(s.Rng.NextDouble() * cands.Count))];
            var pool = WE.Pools[loc][rarity];
            var total = pool.Sum(p => p.Weight);
            var r = s.Rng.NextDouble() * total;
            var acc = 0.0;
            var pick = pool[pool.Count - 1];
            foreach (var p in pool) { acc += p.Weight; if (r < acc) { pick = p; break; } }
            for (var k = 0; k < WE.SpawnCountPerSuccessfulCheck && LocalCount(s, loc) < WE.CapOf(loc) && LiveCount(s) < WE.GlobalCap; k++)
                AddWorldEnemy(s, pick.EnemyId, loc);
        }

        /// <summary>物理相邻地点（不依赖玩家是否已发现路线）。</summary>
        public IEnumerable<string> PhysicalNeighbors(string loc) => Config.Edges.Where(e => e.Touches(loc)).Select(e => e.Other(loc));

        /// <summary>巡游：未交战、未隐藏的敌人逐个 20% 判定，走一条物理相邻边；目标不为营地、不超过目标上限；原子提交，全局数不变。</summary>
        private void Patrol(GameState s)
        {
            foreach (var w in s.WorldEnemies.OrderBy(w => w.Id).ToList())
            {
                if (w.Engaged || Hidden(s, w) || w.FixedOf != null) continue;
                // B41a（R05 Q417）：随机组卡成员不自行巡游，也不抽巡游随机数
                if (w.GroupId != null) continue;
                // B20：人口区的敌人只在同区物理相邻、有空位的节点间巡游（不跨区、不进城市/地牢）
                if (PopRegionOf(w.LocationId) != null)
                {
                    if (s.Rng.NextDouble() >= PopRegionOf(w.LocationId)!.PatrolAttempt) continue;
                    var rt = RegionPatrolTargets(s, w.LocationId);
                    if (rt.Count == 0) continue;
                    MovePatrol(s, w, rt[(int)Math.Min(rt.Count - 1, Math.Floor(s.Rng.NextDouble() * rt.Count))]);
                    continue;
                }
                // 不在旧 8 地点种群、也不在人口区的（开发入口放进地牢/城市的）不巡游，也不抽随机数（B11）
                if (!InLegacyPopulation(w.LocationId)) continue;
                // 首战保留游荡尸胜利前不巡游（R05 §5），也不抽巡游随机数
                if (IsTutorialEnemy(s, w.Id)) continue;
                if (s.Rng.NextDouble() >= WE.PatrolMoveProbabilityPerEnemy) continue;
                var targets = PhysicalNeighbors(w.LocationId)
                    .Where(l => WE.PatrolEntersCamp || !Config.Encounters.SafeLocations.Contains(l))
                    .Where(l => !NpcSafePlace(l))
                    .Where(l => WE.CapOf(l) > 0 && LocalCount(s, l) < WE.CapOf(l))
                    .Distinct().OrderBy(l => l).ToList();
                if (targets.Count == 0) continue;
                MovePatrol(s, w, targets[(int)Math.Min(targets.Count - 1, Math.Floor(s.Rng.NextDouble() * targets.Count))]);
            }
        }

        /// <summary>巡游走一条边（原子：原地点扣 1、新地点加 1）；进出玩家当前地点时提示。</summary>
        private void MovePatrol(GameState s, WorldEnemy w, string to)
        {
            var from = w.LocationId;
            w.LocationId = to;
            if (to == s.CurrentLocation)
                Emit(s, GameEventKind.Info, $"新的敌踪出现：{_enemies[w.EnemyId].Name}来到了{LocName(to)}。", null, new[] { $"enemy:{w.Id}" });
            else if (from == s.CurrentLocation)
                Emit(s, GameEventKind.Info, $"{_enemies[w.EnemyId].Name}离开了{LocName(from)}。");
        }

        // ───────────── 接敌 ─────────────

        /// <summary>
        /// 探索/清理完成后（原行动结果已提交）检查随机遭遇：用开始时锁定的判定值与当时的本地敌对密度比较；
        /// 命中时形成一张随机组卡（B41a，R05 Q417；不产生“待处理敌踪”，不阻断其他操作）。
        /// </summary>
        private void CheckRandomEncounter(GameState s, ActionState a)
        {
            if (s.Progress.Dead || InCombatFlow(s)) return;
            if (!Config.Encounters.CheckOnlyAfterCompletedActions.Contains(a.Type == ActionType.Explore ? "explore" : a.Type == ActionType.Clear ? "clear" : "")) return;
            var loc = s.CurrentLocation;
            var p = EncounterChance(s, loc, a.Type);
            if (p <= 0 || a.EncounterRoll >= p) return;
            FormRandomGroupCard(s, a, loc);
        }

        // ───────────── 调试 ─────────────

        /// <summary>调试与测试：立即做一次刷新检查（用存档内随机数），返回新生成的敌人。</summary>
        public WorldEnemy? DebugSpawnCheck(GameState s)
        {
            var before = s.WorldEnemies.Count;
            SpawnCheck(s);
            return s.WorldEnemies.Count > before ? s.WorldEnemies[s.WorldEnemies.Count - 1] : null;
        }

        /// <summary>调试：在指定地点放一只世界敌人（不受上限约束，只供测试与开发菜单）。</summary>
        public WorldEnemy DebugSpawnEnemy(GameState s, string enemyId, string locationId) => AddWorldEnemy(s, enemyId, locationId);

        /// <summary>开发入口放敌人前的校验：安全区、本地上限、全局上限都遵守（试玩修复 L10）；内核测试仍可直接调用 DebugSpawnEnemy 布置场景。</summary>
        public string? DebugSpawnBlocked(GameState s, string locationId)
        {
            if (Config.Encounters.SafeLocations.Contains(locationId)) return $"{LocName(locationId)}是安全区，不放敌人";
            if (NpcSafePlace(locationId) || _locs.TryGetValue(locationId, out var lc) && (!lc.RandomEncounterEnabled || lc.SafeExterior)) return $"{LocName(locationId)}没有随机敌人种群，不放敌人";
            var cap = LocalCapOf(locationId);
            if (LocalCount(s, locationId) >= cap) return $"{LocName(locationId)}敌人已达上限{cap}";
            // B20：新人口区按本区总上限；旧 8 地点按旧全局 12
            if (PopRegionOf(locationId) is PopulationRegionConfig reg) { if (RegionLiveCount(s, reg) >= reg.Cap) return $"人口区敌人已达上限{reg.Cap}"; }
            else if (InLegacyPopulation(locationId) && LiveCount(s) >= WE.GlobalCap) return $"新手区敌人已达上限{WE.GlobalCap}";
            return null;
        }
    }
}
