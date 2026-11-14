using System.Collections.Generic;
using System.Linq;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>
    /// B48（R05 文首「2026-11-14 Q636：直接清除安全地点残留敌人」；逐字稿文首「Q629—Q636」表安全区两句；开发记录 B48）：
    /// 岗位安全地点（<see cref="NpcSafePlace"/>，配置 city.npcs[].locationId 的岗位绑定）上已残留的真实敌人实例与对应敌人卡直接清除。
    /// 时点：读档／旧档迁移链尾（<see cref="Save.SaveMigrator"/> 采纳新版敌踪之后），以及运行中每个敌踪接近边界（<see cref="EnemyApproachBoundary"/>
    /// 先于修剪、抽时与到期检查）；过期的主动接近／潜行／躲避／固定入口请求命中安全地点时先清理再拒绝。
    /// 不算击杀：不发经验、掉落、尸体、奖励，不触发死亡恐慌与战斗结算；已在交战中的实例（已真实提交的在办战斗）不动，战斗收束后的边界再清。
    /// </summary>
    public sealed partial class GameEngine
    {
        /// <summary>逐字稿「安全区旧接敌请求／异常固定入口拒绝」原句（R05 Q636）。</summary>
        public const string SafeZoneNoEngageText = "这里是安全区域，不能接敌。";

        /// <summary>逐字稿「确有残留被清除时的世界日志」原句（R05 Q636；地点取原公开名称，按地点聚合一条）。</summary>
        public static string SafeZonePurgedText(string placeName) => $"已清除安全区域“{placeName}”中的残留敌人卡。";

        /// <summary>
        /// 这次接敌请求命中安全地点（R05 Q636）：队伍此刻在岗位安全地点，或所指卡（仍存在时）位于岗位安全地点。只读。
        /// </summary>
        private bool SafePlaceContact(GameState s, string cardKey) =>
            NpcSafePlace(s.CurrentLocation) || (FindEnemyCard(s, cardKey, out _) is EnemyCardView card && NpcSafePlace(card.LocationId));

        /// <summary>
        /// 清除岗位安全地点上残留的真实敌人（R05 Q636）：未交战的世界敌人实例（含旧版逃走隐藏、躲避隐藏待重现、组卡成员、固定战／可选强敌实例）按实例 ID 去重一次移除——
        /// 人口总账、人口区账与节点数都由存续实例现算，移除即释放一次；组卡删去已清成员，成员清空的组卡移除；位于安全地点、但剩余成员都在合法地点的组卡
        /// 解散为各自原地的单卡（不迁移实例）；随后已不存在的卡的接近时限／撤退保护与现身计数一并移除。不发经验、掉落、尸体、任务或首次处理奖励，
        /// 不写战报、不触发恐慌；不动物品、遗物、设施与 NPC。有实际清理时按地点各写一条世界日志，无残留什么都不做（幂等）。返回是否清了东西。
        /// </summary>
        public bool PurgeSafePlaceEnemies(GameState s)
        {
            var doomed = s.WorldEnemies.Where(w => !w.Engaged && NpcSafePlace(w.LocationId)).ToList();
            var doomedIds = doomed.Select(w => w.Id).ToHashSet();
            // 位于安全地点、仍有存续成员的组卡：没有交战中的成员才清（在办战斗的组按原场次收束，战后边界再清）
            var safeGroups = s.EnemyCards.Groups
                .Where(g => NpcSafePlace(g.LocationId) && g.MemberIds.Any(id => FindWorldEnemy(s, id) != null)
                            && !g.MemberIds.Any(id => FindWorldEnemy(s, id) is WorldEnemy m && m.Engaged))
                .ToList();
            if (doomed.Count == 0 && safeGroups.Count == 0) return false;

            // 受影响的卡键（清理前取，清理后按“卡已不存在”移除其时限、保护与现身计数）
            // （组员的旧单卡键也算：同一实例可能同时被组卡与单卡记录引用）
            var keys = doomed.SelectMany(w => new[] { CardKeyOfEnemy(w), SingleCardKey(w.Id) })
                .Concat(safeGroups.Select(g => GroupCardKey(g.Id))).Distinct().ToList();
            // 日志地点：按最早实例／组卡编号出现的先后，每地一条
            var places = doomed.OrderBy(w => w.Id).Select(w => (order: w.Id, loc: w.LocationId))
                .Concat(safeGroups.Select(g => (order: g.MemberIds.Count > 0 ? g.MemberIds.Min() : int.MaxValue, loc: g.LocationId)))
                .OrderBy(x => x.order).Select(x => x.loc).Distinct().ToList();

            s.WorldEnemies.RemoveAll(w => doomedIds.Contains(w.Id));
            foreach (var g in safeGroups)
            {
                // 仍存续的成员（真实位于合法地点）保留在原地，只是不再属于这张已清的组卡
                foreach (var id in g.MemberIds)
                    if (FindWorldEnemy(s, id) is WorldEnemy w && w.GroupId == g.Id) w.GroupId = null;
                g.MemberIds.Clear();
            }
            PruneEnemyCards(s);
            foreach (var key in keys)
            {
                if (FindEnemyCard(s, key, out _) != null) continue;
                s.EnemyCards.Timers.Remove(key);
                s.EnemyCards.Appearances.Remove(key);
            }
            foreach (var loc in places) Emit(s, GameEventKind.Info, SafeZonePurgedText(LocName(loc)));
            return true;
        }
    }
}
