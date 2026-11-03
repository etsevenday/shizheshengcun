using System.Collections.Generic;
using System.Linq;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>
    /// B22h R199（Q196 选 C；party-combat-expedition-adjudications.json retreat.world / fallback / currentBattles）：
    /// 普通主世界（野外、城市外部、m02 检查哨、可选强敌）战斗撤退成功，全队退回“最近一次已完成的地点移动”的来源节点。
    /// - 开战快照锁定来处（<see cref="BattleState.WorldRetreat"/>）：只读 <see cref="GameState.LastMove"/>，它的到达点必须就是开战节点、来处不是开战节点；
    ///   屏幕方位、未完成移动、安全屋进出都不是来处，不走未知边、不猜路线。
    /// - 沿那条实际来路可从开战节点反向通行（已开通、单向出口不能反走）且来处不是地牢节点：撤退结算含在本场唯一 tick 里——0 额外分钟、不收移动费、
    ///   不抽路途遭遇；不是常规移动：不触发到达/探索/开边/领奖、不记移动经验与已走边、不新增移动记录；敌人原 HP 与位置保留。
    /// - 没有可信来处、边不可反向通行、来处是地牢：留在原地并写“没有可返回的来路，本次撤退后仍在原地”；到达处敌踪照常公开、不自动接战。
    /// - 上线前已开始的战斗（没有快照）按旧落点：留在原地。地牢节点里的战斗走地牢撤退（B21，保持现状）。
    /// </summary>
    public sealed partial class GameEngine
    {
        /// <summary>没有可返回来路时战报与预览里的固定说法（retreat.fallback）。</summary>
        public const string NoRetreatPathText = "没有可返回的来路，本次撤退后仍在原地";

        /// <summary>开战时的撤退来处快照：最近一次已完成移动到达的就是这里、且来处不是这里，才记来处；否则为空快照（没有可信来处）。</summary>
        private RetreatOriginSnapshot RetreatOriginAtStart(GameState s)
        {
            var lm = s.LastMove;
            if (lm == null || lm.From == "" || lm.To != s.CurrentLocation || lm.From == s.CurrentLocation) return new RetreatOriginSnapshot();
            return new RetreatOriginSnapshot { From = lm.From, EdgeId = lm.EdgeId };
        }

        /// <summary>
        /// 本场普通主世界撤退的落点：可返回时为来处、原因为 null；不可返回时落点为 null、原因写明（给预览）。
        /// 旧战（没有快照）与地牢节点里的战斗返回 (null, null)，按各自原规则。
        /// </summary>
        public (string? To, string? Why) WorldRetreatTarget(GameState s, BattleState b)
        {
            var snap = b.WorldRetreat;
            if (snap == null || DungeonAt(b.LocationId) != null) return (null, null);
            if (snap.From == null) return (null, "没有可信的已完成移动记录（旧存档或开发传送后）");
            if (!_locs.ContainsKey(snap.From) || DungeonAt(snap.From) != null) return (null, $"来处{LocName(snap.From)}在地牢里");
            var edge = snap.EdgeId != null && _edges.TryGetValue(snap.EdgeId, out var e) ? e : null;
            if (edge == null || !edge.Touches(b.LocationId) || edge.Other(b.LocationId) != snap.From || !edge.TraversableFrom(b.LocationId) || !EdgeOpen(s, edge))
                return (null, $"走来的路现在不能从这边反向回到{LocName(snap.From)}");
            return (snap.From, null);
        }

        /// <summary>撤退预览“成功：本轮推进5分钟后脱战，……”中间这一段。</summary>
        public string WorldRetreatPreview(GameState s, BattleState b)
        {
            if (b.WorldRetreat == null || DungeonAt(b.LocationId) != null) return $"留在{LocName(b.LocationId)}（不传回营地）";
            var (to, why) = WorldRetreatTarget(s, b);
            return to != null
                ? $"全队退回{LocName(to)}（最近一次走来的地点；沿原路退回，不另花时间、不收移动消耗、不算一次移动）"
                : $"{NoRetreatPathText}（{LocName(b.LocationId)}；{why}）";
        }

        /// <summary>撤退成功后的普通主世界落点：移动全队并返回给战报的短语；旧战与地牢节点返回 null（照旧）。</summary>
        private string? WorldRetreatLanding(GameState s, BattleState b)
        {
            if (s.Progress.Dead || b.WorldRetreat == null || DungeonAt(b.LocationId) != null) return null;
            var (to, _) = WorldRetreatTarget(s, b);
            if (to == null) return NoRetreatPathText;
            s.CurrentLocation = to;
            s.CurrentZone = ExteriorZone;
            var text = $"全队退回{LocName(to)}（最近一次走来的地点；不另花时间、不收移动消耗、不算一次移动）";
            var seen = VisibleEnemiesAt(s, to).Where(w => !w.Engaged).ToList();
            if (seen.Count > 0)
                text += $"；{LocName(to)}有敌踪：{string.Join("、", seen.GroupBy(w => w.EnemyId).Select(g => $"{_enemies[g.Key].Name}×{g.Count()}"))}（可以接近，也可以避开；不会自动开战）";
            return text;
        }
    }
}
