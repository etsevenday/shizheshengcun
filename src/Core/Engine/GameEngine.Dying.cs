using System;
using System.Collections.Generic;
using System.Linq;
using Survival.Core.Config;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>我方角色的三态（R09 §2）：正常存活 H&gt;0；濒死存活 H=0 且未确死；确实死亡。</summary>
    public enum LifeState { Normal, Dying, Dead }

    /// <summary>
    /// 濒死、意志与致死判定（R09；开发记录 B47a1）。我方健康归零先进入濒死，濒死后每个后续真实正伤事件按意志抽一次致死抵抗；
    /// 敌人没有濒死（敌方 H 归 0 直接死亡，沿原 KillEnemy）。生命态由“已确死标记 + 当前健康”派生：
    /// 确死 = ActorState.Dead／名册 Dead／主角 Progress.Dead；否则 H≤0 为濒死、H&gt;0 为正常（健康内部精度、不按显示取整）。
    /// </summary>
    public sealed partial class GameEngine
    {
        /// <summary>意志熟练度 ID（R09 §4，第 17 门被动熟练度）。</summary>
        public const string Willpower = "willpower";

        private DyingConfig DyingCfg => Config.Dying;

        /// <summary>该角色已确实死亡（R09 §2）：本人确死标记、名册死亡记录或主角终局。</summary>
        public bool IsDead(GameState s, ActorState actor) =>
            actor.Dead || (actor.IsPlayer ? s.Progress.Dead : RosterOf(s, actor.Id)?.Dead == true);

        /// <summary>该角色处于濒死存活（H=0、未确死）。</summary>
        public bool IsDying(GameState s, ActorState actor) => !IsDead(s, actor) && actor.Stats.Health <= 0;

        public LifeState LifeOf(GameState s, ActorState actor) =>
            IsDead(s, actor) ? LifeState.Dead : actor.Stats.Health <= 0 ? LifeState.Dying : LifeState.Normal;

        /// <summary>
        /// 意志 L 级的致死抵抗百分点 P = 65 + 2×(L−1)（R09 §4：整数百分点，最后一次除以 100，不逐级浮点累加）。L 按 1—10 夹紧。
        /// </summary>
        public int ResistancePercent(int level)
        {
            var cap = SkillCap(Willpower);
            var l = Math.Max(1, Math.Min(cap == int.MaxValue ? level : cap, level));
            return DyingCfg.ResistanceBasePercent + DyingCfg.ResistancePerLevelPercent * (l - 1);
        }

        /// <summary>该角色当前有效意志等级的致死抵抗百分点（事件开始前的等级）。</summary>
        public int ResistancePercent(ActorState actor) => ResistancePercent(SkillLevel(actor, Willpower));

        /// <summary>
        /// 一次致死判定（R09 §4）：均匀 u∈[0,1)，u &lt; P/100 抵抗成功，相等判失败。纯函数（不抽随机），真实判定由 DamageActor 抽 u 后调用。
        /// </summary>
        public bool ResistsDeath(int level, double u) => u < ResistancePercent(level) / 100.0;
    }
}
