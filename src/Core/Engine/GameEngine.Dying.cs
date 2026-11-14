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

        /// <summary>
        /// 仅供自动测试注入：非空且有值时，下一次真实致死判定按顺序取这里的 u（不消耗存档随机游标）。正式流程与网页壳恒为 null。
        /// </summary>
        public Queue<double>? TestDeathRolls { get; set; }

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

        // ───────────── 统一受伤入口（R09 §3） ─────────────

        /// <summary>R09 §8「进入濒死」正式句。</summary>
        public static string EnterDyingText(string who) => $"{who}进入濒死状态。必须用药或医疗实际恢复健康才能脱离。";

        /// <summary>R09 §8「濒死后续正伤且抵抗」正式句（Q678：战外每次真实成功抵抗各一条）。</summary>
        public static string ResistedDeathText(string who) => $"{who}抵抗了致死，仍处于濒死状态。";

        /// <summary>R09 §8「用药／医疗回血脱离」正式句。</summary>
        public static string LeaveDyingText(string who) => $"{who}脱离濒死状态。";

        /// <summary>
        /// 一次真实健康伤害事件（R09 §3.1、§4）：D 是已按原命中、护甲、格挡、保护等结算后实际落到此人身上的正伤害（原内部精度，D&gt;0 即有效）。
        /// <list type="bullet">
        /// <item>D≤0 或已确实死亡：不受伤、不判定、不抽随机（<see cref="DamageOutcome.None"/>）。</item>
        /// <item>事件前 H&gt;0：扣到最低 0；仍 &gt;0 为 <see cref="DamageOutcome.Hurt"/>；归 0 进入濒死（本次不抽致死，没有过量穿透），写进入濒死句、置濒死提醒锁。</item>
        /// <item>事件前已濒死：按事件开始前的意志等级抽一次 u，抵抗成功仍 H=0（写抵抗句），失败置确死标记（<see cref="DamageOutcome.Died"/>）。</item>
        /// </list>
        /// 死亡后果（主角终局、队友离队与遗物、战斗收束）由调用方在原时序处理（主角“结算写完再终局”、世界段死亡先于自然治愈等）；
        /// “未能抵抗致死”句待 Q689 答复，本批不写，只写原死亡句。只读预演（SimCopy）不抽随机，按未能抵抗处理（B47a3 预估截断前的过渡口径）。
        /// 状态句写到 notes（调用方在自己的伤害日志之后追加）；notes 为空时立即写：在场我方写战报，其他写普通日志。
        /// </summary>
        public DamageOutcome DamageActor(GameState s, ActorState actor, double d, List<string>? notes = null)
        {
            if (!(d > 0) || IsDead(s, actor)) return DamageOutcome.None;
            var st = actor.Stats;
            if (st.Health > 0)
            {
                st.Add(StatId.Health, -d);
                if (st.Health > 0) return DamageOutcome.Hurt;
                actor.DyingNotified = true;
                LifeNote(s, actor, EnterDyingText(ActorDisplayName(s, actor.Id)), notes);
                return DamageOutcome.EnteredDying;
            }
            var level = SkillLevel(actor, Willpower);
            var resisted = !s.SimCopy && ResistsDeath(level, TestDeathRolls is { Count: > 0 } q ? q.Dequeue() : s.Rng.NextDouble());
            if (resisted)
            {
                LifeNote(s, actor, ResistedDeathText(ActorDisplayName(s, actor.Id)), notes);
                return DamageOutcome.Resisted;
            }
            actor.Dead = true;
            return DamageOutcome.Died;
        }

        // ───────────── 医疗脱离（R09 §6） ─────────────

        /// <summary>
        /// 明确用药／医疗的实际回血（绷带、战斗医疗技能、城内换药同一入口，R09 §6）：已确死者不受理；
        /// 回血前濒死（H=0）且回血后 H&gt;0 即脱离濒死——清濒死提醒锁、写“{人物}脱离濒死状态。”（notes 非空时交调用方追加在自己的治疗日志之后）。
        /// 返回实际增加量。食物等非医疗正健康不走这里（濒死期间不生效）。
        /// </summary>
        public double HealActor(GameState s, ActorState actor, double amount, List<string>? notes = null)
        {
            if (!(amount > 0) || IsDead(s, actor)) return 0;
            var st = actor.Stats;
            var before = st.Health;
            st.Add(StatId.Health, amount);
            if (before <= 0 && st.Health > 0)
            {
                actor.DyingNotified = false;
                LifeNote(s, actor, LeaveDyingText(ActorDisplayName(s, actor.Id)), notes);
            }
            return st.Health - before;
        }

        /// <summary>生命态状态句：notes 非空时交调用方追加；否则在场我方写战报（Q688：战斗中只写战报），其余写普通日志。</summary>
        private void LifeNote(GameState s, ActorState actor, string text, List<string>? notes)
        {
            if (notes != null) { notes.Add(text); return; }
            WriteLifeNotes(s, actor, new[] { text });
        }

        /// <summary>把调用方收集的生命态状态句写出（在场我方写战报，其余写普通日志）。</summary>
        private void WriteLifeNotes(GameState s, ActorState actor, IEnumerable<string> notes)
        {
            var battle = s.Battle != null && AllyInBattle(s.Battle, actor.Id);
            foreach (var n in notes)
            {
                if (battle) BattleLog(s, n);
                else Emit(s, GameEventKind.Info, n);
            }
        }
    }

    /// <summary>一次健康伤害事件的生命结果（R09 §3.1）。</summary>
    public enum DamageOutcome
    {
        /// <summary>没有正伤害或目标已确实死亡：未受伤、未判定。</summary>
        None,
        /// <summary>正常者受伤后仍 H&gt;0。</summary>
        Hurt,
        /// <summary>正常者归 0，进入濒死（本次不判致死）。</summary>
        EnteredDying,
        /// <summary>濒死者再受正伤，抵抗了致死。</summary>
        Resisted,
        /// <summary>濒死者再受正伤，未能抵抗：已确实死亡（死亡后果由调用方处理）。</summary>
        Died,
    }
}
