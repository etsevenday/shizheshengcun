using System;
using System.Collections.Generic;
using System.Linq;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>
    /// B41c 躲避隐藏到期后的重现（R05「Q420 躲避重现与固定关键敌人」「Q445」「Q503 原人口账与实际所在地」；开发记录 B41c）。
    /// 只处理躲避隐藏（WorldEnemy.EvadeHiddenUntil，开关 encounters.enemyCardsV2 下才会出现）；旧版逃走隐藏（HiddenUntil）不变。
    /// </summary>
    public sealed partial class GameEngine
    {
        /// <summary>
        /// 世界 tick 里到点的躲避隐藏逐卡重现（按卡键稳定顺序）：从隐藏时所在地点与物理相邻地点去重后的合法地点里等概率抽一处（只有一处不抽），
        /// 都不合法就留在原地。不看目标人口上限（人口仍记原账），不发消息、不揭示地点；整卡成员一起落地，组卡停在重现地，
        /// 本卡现身号 +1（新一次现身：接近时间重新抽、潜行经验可再领一次）。
        /// </summary>
        private void ReappearEvaded(GameState s)
        {
            var due = s.WorldEnemies.Where(w => w.EvadeHiddenUntil is int t && t <= s.GameMinute).ToList();
            if (due.Count == 0) return;
            var cards = due.GroupBy(CardKeyOfEnemy).OrderBy(g => g.Key, Comparer<string>.Create(CompareStableKey)).ToList();
            foreach (var card in cards)
            {
                var members = card.OrderBy(w => w.Id).ToList();
                var origin = members[0].LocationId;
                var legal = ReappearCandidates(s, members[0]);
                var to = legal.Count == 0 ? origin
                    : legal.Count == 1 ? legal[0]
                    : legal[(int)Math.Min(legal.Count - 1, Math.Floor(s.Rng.NextDouble() * legal.Count))];
                foreach (var w in members)
                {
                    w.LocationId = to;
                    w.EvadeHiddenUntil = null;
                }
                if (members[0].GroupId is int gid && FindGroupCard(s, gid) is EnemyGroupCard g) g.LocationId = to;
                s.EnemyCards.Appearances[card.Key] = AppearanceOf(s, card.Key) + 1;
                s.EnemyCards.Timers.Remove(card.Key);
            }
        }

        /// <summary>
        /// 重现候选（Q420、Q445）：原地点与物理相邻地点去重、按序号排序；排除安全区、城市及公共服务节点（屋外安全、不随机遇敌的非地牢节点）。
        /// 地牢敌只选同一地牢、同一代的危险房间（不出地牢、不进入口缓冲房），旧代敌人留在原地等原实例清理；野外敌不进地牢。不设「自锁地点」过滤。
        /// </summary>
        private List<string> ReappearCandidates(GameState s, WorldEnemy w)
        {
            var origin = w.LocationId;
            var dungeon = DungeonAt(origin);
            if (dungeon != null && w.Generation != (s.DungeonInstances.TryGetValue(dungeon, out var inst) ? inst.Generation : 0))
                return new List<string>();
            return new[] { origin }.Concat(PhysicalNeighbors(origin)).Distinct().OrderBy(l => l, StringComparer.Ordinal)
                .Where(l => ReappearLegal(l, dungeon)).ToList();
        }

        private bool ReappearLegal(string loc, string? dungeon)
        {
            if (!_locs.TryGetValue(loc, out var lc)) return false;
            if (Config.Encounters.SafeLocations.Contains(loc)) return false;
            var d = DungeonAt(loc);
            if (dungeon != null) return d == dungeon && IsDangerRoom(loc);
            if (d != null || lc.IsDungeon) return false;
            return lc.RandomEncounterEnabled && !lc.SafeExterior;
        }
    }
}
