using System;
using System.Collections.Generic;
using System.Linq;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>
    /// B41c 先手加成（R05「Q422 首次出手与受惊」、R03；逐字稿 B41「先手说明与攻击预览」；开发记录 B41c）。
    /// 由潜行成功（我方）与敌方主动开战（敌方）给出；主动接近与上线前开始的旧战斗没有先手。
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

        /// <summary>第一完整回合（Round 1）先手方的速度倍率；其余为 1。</summary>
        private double FirstStrikeSpeedFactor(BattleState b, string side) =>
            b.FirstStrike == side && b.Round == 1 ? Config.Encounters.FirstStrike.SpeedFactor : 1;

        /// <summary>某名参战者（我方人员 ID／敌方 "e:世界ID"）此刻还有没有首次攻击伤害资格，有则返回伤害倍率。只读（预览与结算同口径）。</summary>
        public double FirstStrikeDamageFactor(BattleState b, string key) =>
            b.FirstStrikeDamage != null && b.FirstStrikeDamage.Contains(key) ? Config.Encounters.FirstStrike.DamageFactor : 1;

        /// <summary>这名参战者是否还有先手伤害加成（界面状态「先手伤害加成可用。」用）。</summary>
        public bool FirstStrikeReady(BattleState b, string key) => FirstStrikeDamageFactor(b, key) > 1;

        /// <summary>实际出手：消耗这名参战者的先手伤害资格（没有就什么都不做）。</summary>
        private static void ConsumeFirstStrike(BattleState b, string key)
        {
            if (b.FirstStrikeDamage == null) return;
            b.FirstStrikeDamage.Remove(key);
        }

        /// <summary>取用并消耗：返回本次攻击的伤害倍率（有资格 1.5，否则 1）。</summary>
        private double TakeFirstStrike(BattleState b, string key)
        {
            var f = FirstStrikeDamageFactor(b, key);
            ConsumeFirstStrike(b, key);
            return f;
        }
    }
}
