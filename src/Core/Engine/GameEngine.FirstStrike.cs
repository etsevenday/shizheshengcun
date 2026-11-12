using System;
using System.Collections.Generic;
using System.Linq;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>
    /// B41c 先手加成（R05「Q422 首次出手与受惊」、R03；逐字稿 B41「先手说明与攻击预览」；开发记录 B41c）。
    /// 只在新版敌踪（开关 encounters.enemyCardsV2）下由潜行成功（我方）与敌方主动开战（敌方）给出；旧战斗与开关关着时没有先手。
    /// </summary>
    public sealed partial class GameEngine
    {
        /// <summary>开场时记下先手方与每名开场参战者的首次攻击伤害资格，并写开场说明。中途增援不获得。</summary>
        private void GrantFirstStrike(GameState s, BattleState b, string side)
        {
            b.FirstStrike = side;
            b.FirstStrikeDamage = side == FirstStrikeAlly
                ? b.Members.ToList()
                : b.Enemies.Select(e => FirstStrikeEnemyKey(e.WorldId)).ToList();
            BattleLog(s, (side == FirstStrikeAlly ? "我方" : "敌方") + "获得先手。每名开场参战者首次攻击伤害+50%，第一回合速度+50%。");
        }

        private static string FirstStrikeEnemyKey(int worldId) => "e:" + worldId;
    }
}
