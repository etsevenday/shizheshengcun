using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>
    /// B46 单段 5 分钟行动先展示中央条再结算（S01「2026-11-12 B41c2 Q535—Q536 试玩批定稿」Q535，S14／逐字稿同批节，R05 Q535-A01—A06；开发记录 B46）。
    /// 内核只给只读判定；展示间隔是网页壳的现实动画时间，不推进世界分钟、不结算任何东西，之后由原推进循环提交唯一的原 5 分钟段。
    /// </summary>
    public sealed partial class GameEngine
    {
        /// <summary>
        /// 当前要推进的行动（子行动优先）是否是「单段 5 分钟、还没提交过任何段」的行动——网页壳在第一段之前先让中央条真实可见一段表现时间。只读。
        /// 适用：开工锁定（同源真实总预计恰为 5 分钟，Q506／Q516）、原单段事务（自用、点火、融合）、潜行接近／躲避，以及读档后仍可退出的旧在办行动
        /// 中同源预计恰为一段的（展示依其真实退出状态，不重新认定锁定）。已推进过的长行动剩 5 分钟、10 分钟铺布等不适用。
        /// 终局、对白、战斗、敌踪待选或暂停时不展示：先沿原边界处理，不为走完动画越过它们。
        /// </summary>
        public bool SingleSegmentShowPending(GameState s)
        {
            if (s.Progress.Dead || DialogueLocked(s) || s.Battle != null) return false;
            var a = s.Sub ?? s.Main;
            if (a == null || a.Status != ActionStatus.Running || a.ElapsedMinutes > 0) return false;
            if (ExitLocked(a)) return true;
            var end = EstimateEndMinute(s, 2);
            return end.HasValue && end.Value - s.GameMinute == ExitLockMinutes;
        }
    }
}
