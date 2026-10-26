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

        /// <summary>战斗中或敌踪待选：普通行动推进暂停，由战斗流程自己推进世界时间。</summary>
        public bool InCombatFlow(GameState s) => s.Battle != null || s.Encounter != null;

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

        public bool Hidden(GameState s, WorldEnemy w) => w.HiddenUntil.HasValue && w.HiddenUntil.Value > s.GameMinute;

        /// <summary>全世界存活敌数（含交战中、中立鹿）。</summary>
        public int LiveCount(GameState s) => s.WorldEnemies.Count;

        public int LocalCount(GameState s, string loc) => s.WorldEnemies.Count(w => w.LocationId == loc);

        /// <summary>某地可见的世界敌人（不含隐藏、不含交战中），按最早出现在前。</summary>
        public List<WorldEnemy> VisibleEnemiesAt(GameState s, string loc) =>
            s.WorldEnemies.Where(w => w.LocationId == loc && !w.Engaged && !Hidden(s, w)).OrderBy(w => w.Id).ToList();

        /// <summary>本地可参与随机接敌的敌对实例：未交战、未隐藏、敌对，按最早发现在前。</summary>
        public List<WorldEnemy> LocalHostiles(GameState s, string loc) =>
            VisibleEnemiesAt(s, loc).Where(w => w.Hostile).ToList();

        public WorldEnemy? FindWorldEnemy(GameState s, int id) => s.WorldEnemies.FirstOrDefault(w => w.Id == id);

        /// <summary>
        /// 完成探索/清理后的接敌概率：基础风险×(本地未交战敌对数/本地上限)；第1日、安全地点、无敌对、保护期内为 0。
        /// </summary>
        public double EncounterChance(GameState s, string loc, ActionType type)
        {
            var en = Config.Encounters;
            if (!Config.CombatEnabled) return 0;
            if (Clock.Day(s.GameMinute) < en.NoRandomBeforeDay) return 0;
            if (en.SafeLocations.Contains(loc)) return 0;
            if (s.GameMinute < s.EncounterGraceUntil) return 0;
            var key = type == ActionType.Explore ? "explore" : type == ActionType.Clear ? "clear" : "";
            if (!en.FullLocalPopulationActionRisk.TryGetValue(key, out var risk)) return 0;
            var cap = WE.CapOf(loc);
            if (cap <= 0) return 0;
            var n = LocalHostiles(s, loc).Count;
            return risk * n / cap;
        }

        /// <summary>预览说明：“完成后接敌概率 0.67%（本地敌对1/上限3）”。</summary>
        public string EncounterNote(GameState s, string loc, ActionType type)
        {
            var en = Config.Encounters;
            if (Clock.Day(s.GameMinute) < en.NoRandomBeforeDay) return $"第{en.NoRandomBeforeDay}日起完成后才可能遇到本地敌人（今天不会）";
            if (en.SafeLocations.Contains(loc)) return "营地是安全区，不会遇敌";
            if (s.GameMinute < s.EncounterGraceUntil) return $"随机接敌保护期还剩{Clock.Duration(s.EncounterGraceUntil - s.GameMinute)}";
            var p = EncounterChance(s, loc, type);
            return $"完成后接敌概率{Fmt.Pct(p)}（本地敌对{LocalHostiles(s, loc).Count}/上限{WE.CapOf(loc)}）；只会遇到这里已有的敌人，可选择绕行";
        }

        // ───────────── 世界 tick：刷新与巡游 ─────────────

        private void TickWorldEnemies(GameState s)
        {
            if (!Config.CombatEnabled) return;
            foreach (var w in s.WorldEnemies.Where(w => w.HiddenUntil.HasValue && w.HiddenUntil.Value <= s.GameMinute)) w.HiddenUntil = null;
            if (s.GameMinute >= s.SpawnNextCheck)
            {
                SpawnCheck(s);
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
                .Where(loc => !Config.Encounters.SafeLocations.Contains(loc))
                .ToList();
        }

        private void SpawnCheck(GameState s)
        {
            if (s.Rng.NextDouble() >= WE.SpawnSuccessProbability) return;
            var rarity = RollRarity(s.Rng.NextDouble());
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
                if (w.Engaged || Hidden(s, w)) continue;
                if (s.Rng.NextDouble() >= WE.PatrolMoveProbabilityPerEnemy) continue;
                var targets = PhysicalNeighbors(w.LocationId)
                    .Where(l => WE.PatrolEntersCamp || !Config.Encounters.SafeLocations.Contains(l))
                    .Where(l => WE.CapOf(l) > 0 && LocalCount(s, l) < WE.CapOf(l))
                    .Distinct().OrderBy(l => l).ToList();
                if (targets.Count == 0) continue;
                var to = targets[(int)Math.Min(targets.Count - 1, Math.Floor(s.Rng.NextDouble() * targets.Count))];
                var from = w.LocationId;
                w.LocationId = to;
                if (to == s.CurrentLocation)
                    Emit(s, GameEventKind.Info, $"新的敌踪出现：{_enemies[w.EnemyId].Name}来到了{LocName(to)}。", null, new[] { $"enemy:{w.Id}" });
                else if (from == s.CurrentLocation)
                    Emit(s, GameEventKind.Info, $"{_enemies[w.EnemyId].Name}离开了{LocName(from)}。");
            }
        }

        // ───────────── 接敌 ─────────────

        /// <summary>
        /// 探索/清理完成后（原行动结果已提交）检查随机接敌：用开始时锁定的判定值与当时的本地敌对密度比较；
        /// 触发时从同地已有敌人中按最早出现取至多3只，等待玩家选择接敌或绕行。
        /// </summary>
        private void CheckRandomEncounter(GameState s, ActionState a)
        {
            if (s.Progress.Dead || InCombatFlow(s)) return;
            if (!Config.Encounters.CheckOnlyAfterCompletedActions.Contains(a.Type == ActionType.Explore ? "explore" : a.Type == ActionType.Clear ? "clear" : "")) return;
            var loc = s.CurrentLocation;
            var p = EncounterChance(s, loc, a.Type);
            if (p <= 0 || a.EncounterRoll >= p) return;
            var group = LocalHostiles(s, loc).Take(Config.Encounters.GroupMax).ToList();
            if (group.Count == 0) return;
            s.Encounter = new PendingEncounter
            {
                LocationId = loc,
                EnemyIds = group.Select(w => w.Id).ToList(),
                Source = a.Type == ActionType.Explore ? "explore" : "clear",
                Minute = s.GameMinute,
            };
            var names = string.Join("、", group.GroupBy(w => w.EnemyId).Select(g => $"{_enemies[g.Key].Name}×{g.Count()}"));
            Emit(s, GameEventKind.Encounter, $"发现敌踪：{names}。可以接敌，也可以绕行{Clock.Duration(Config.Encounters.BypassMinutes)}。",
                null, group.Select(w => $"enemy:{w.Id}"));
        }

        /// <summary>敌踪的组成（界面卡片用）。</summary>
        public List<WorldEnemy> EncounterEnemies(GameState s) =>
            s.Encounter == null ? new List<WorldEnemy>() : s.Encounter.EnemyIds.Select(id => FindWorldEnemy(s, id)).Where(w => w != null).Select(w => w!).ToList();

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
            var cap = WE.CapOf(locationId);
            if (LocalCount(s, locationId) >= cap) return $"{LocName(locationId)}敌人已达上限{cap}";
            if (LiveCount(s) >= WE.GlobalCap) return $"全局敌人已达上限{WE.GlobalCap}";
            return null;
        }
    }
}
