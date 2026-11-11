using System;
using System.Collections.Generic;
using System.Linq;
using Survival.Core.Config;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>遭遇身份（R05「Q423 警惕逐项定稿」：角色身份以本次遭遇来源判定，同一敌种换遭遇身份时取对应列）。</summary>
    public enum EnemyCardIdentity
    {
        /// <summary>普通常驻（单卡、随机组卡成员）。</summary>
        Resident,
        /// <summary>固定战成员（含守卫战随从、检查哨拦路战）。</summary>
        FixedMember,
        /// <summary>守卫本体（守卫战名单的首名）。</summary>
        GuardianBody,
        /// <summary>可选强敌。</summary>
        OptionalElite,
        /// <summary>教学首战的游荡尸（特例）。</summary>
        Tutorial,
    }

    /// <summary>
    /// B41a 新版敌踪卡模型（R05 文首「Q342 新版敌踪、潜行和躲避定稿」；重墨 V2 逐字稿「B41 Q426–Q428 正式文案」；开发记录 B41a）。
    /// 全部新逻辑挂在 <see cref="CardsV2"/> 后面：开关 encounters.enemyCardsV2 关着时旧版接敌／绕行流程原样。
    /// </summary>
    public sealed partial class GameEngine
    {
        // ───────────── 警惕（R05 Q423） ─────────────

        /// <summary>
        /// 敌种在某遭遇身份下的公开警惕等级（1–10 级）：直接取 R05 Q423 表的对应列（表中已是取高后的值）；教学首战的游荡尸取特例值。
        /// </summary>
        public int VigilanceOf(string enemyId, EnemyCardIdentity identity)
        {
            if (identity == EnemyCardIdentity.Tutorial) return Config.Story.TutorialBattle.Vigilance;
            var v = _enemies[enemyId].Vigilance ?? throw new InvalidOperationException($"敌人 {enemyId} 缺少警惕等级");
            return identity switch
            {
                EnemyCardIdentity.FixedMember => v.FixedMember,
                EnemyCardIdentity.GuardianBody => v.GuardianBody,
                EnemyCardIdentity.OptionalElite => v.OptionalElite,
                _ => v.Resident,
            };
        }
    }
}
