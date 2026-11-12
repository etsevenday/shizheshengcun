using System;
using System.Collections.Generic;
using System.Linq;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>
    /// B41c 新版敌踪的小队队长、潜行接近／躲避、练习潜行（R05「队长、潜行与 Q424 练习」「潜行与躲避判定」「B41c Q503—Q508」，
    /// R08「Q342 小队队长」；逐字稿 B41「结果、取消与队长回退」「新操作的禁用与拒绝」「队长与练习潜行」；开发记录 B41c）。
    /// 全部挂在 <see cref="CardsV2"/> 后面：开关 encounters.enemyCardsV2 关着时没有队长、潜行行动与潜行经验，旧版流程原样。
    /// </summary>
    public sealed partial class GameEngine
    {
        // ───────────── 小队队长（R08 Q342；R05「队长、潜行与 Q424 练习」） ─────────────

        /// <summary>
        /// 某人此刻能不能当队长（或当练习者）：主角存活；同行者存活、仍在同行队伍里（同行者与主角同地同区）。
        /// </summary>
        public bool SquadMemberValid(GameState s, string actorId)
        {
            if (s.Progress.Dead) return false;
            if (actorId == GameState.PlayerId) return true;
            if (!s.Party.Contains(actorId) || !s.Actors.TryGetValue(actorId, out var a)) return false;
            if (a.Stats.Health <= 0 || RosterOf(s, actorId)?.Dead == true) return false;
            return true;
        }

        /// <summary>当前小队队长（缺省与失效都为主角）。</summary>
        public string SquadLeaderId(GameState s) =>
            s.SquadLeader != null && SquadMemberValid(s, s.SquadLeader) ? s.SquadLeader : GameState.PlayerId;

        /// <summary>
        /// 有人死亡或离开同行队伍后（原死亡／离队结果已写）：他是队长时立即改回主角，主角仍存活时追加回退句；
        /// 他是进行中练习潜行的练习者时收束那次练习（Q508）。重复调用不重复写。
        /// </summary>
        private void AfterSquadLeave(GameState s, string actorId)
        {
            if (s.SquadLeader == actorId && !SquadMemberValid(s, actorId))
            {
                s.SquadLeader = null;
                if (!s.Progress.Dead && CardsV2(s)) Emit(s, GameEventKind.Info, "原队长已无法带队，小队队长已改为你。");
            }
        }
    }
}
