using System;
using System.Collections.Generic;
using System.Linq;
using Survival.Core.Config;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>
    /// 敌人意图与行动（A09 §1「敌人面板与行为」、A09「最终敌方资源/人口接口」、R03「敌方资源接口补充」；开发计划 v0.9 B7 R26）。
    /// - 意图循环：按本场第几次正常激活（Round−JoinRound）取 config.enemies[].intents 的 opening 前几项，之后循环 cycle；轮初锁定并公开；
    ///   丧尸“抓击80%/咬击20%”在锁定时按 bitePct 改判咬击（只有明确咬击才判感染）。
    /// - AP：普通 3、精英 4、追击 2；每次正常激活执行一个 2AP 主意图。攻击类意图先满足己位/射程：
    ///   站得太靠后 → 1AP 前移（与前一位同伴换位），余 AP 只做一次普通攻击；后排型（己位 34）被压到 1、2 号位 → 通用弱攻击（伤害×0.7）；
    ///   余下的 AP 不另附攻击、不自动防御（开发假设 B7-9）。追击 2AP 只做一次普通攻击或前移，不放主意图群招。
    /// - 敌方攻击按我方真实号位判断能否够到；拉扯/击退命中后判控制，成功则真实移一格（Q80，单人也移）。
    /// </summary>
    public sealed partial class GameEngine
    {
        private static readonly HashSet<string> ReachKinds = new HashSet<string> { "attack", "bite", "charge", "sweep", "spit", "pull", "shoot" };
        /// <summary>A09：需要后排的敌人被压到前排后改用通用弱攻击，伤害×0.7。</summary>
        public const double WeakAttackFactor = 0.7;

        /// <summary>入场时补敌人的本场状态：加入轮次、枪手弹药计划（弹仓与备弹）。</summary>
        private BattleEnemy InitBattleEnemy(BattleEnemy e)
        {
            InitAmmo(e);
            return e;
        }

        private void InitAmmo(BattleEnemy e)
        {
            var cfg = _enemies[e.EnemyId];
            if (e.Mag >= 0 || cfg.AmmoTotal <= 0) return;
            e.Mag = Math.Min(cfg.Magazine, cfg.AmmoTotal);
            e.Reserve = cfg.AmmoTotal - e.Mag;
        }

        /// <summary>这一轮是它本场第几次正常激活（0 起）。</summary>
        private static int ActivationIndex(BattleState b, BattleEnemy e) => Math.Max(0, b.Round - Math.Max(1, e.JoinRound));

        /// <summary>敌人每次正常激活的 AP（普通 3、精英 4；R03 敌方资源接口补充）。</summary>
        public int EnemyAp(BattleEnemy e) => _enemies[e.EnemyId].Ap;

        // ───────────── 意图步骤 ─────────────

        private static bool KindMatches(string stepKind, string intent) => stepKind == intent || (stepKind == "attack" && intent == "bite");

        private static IEnumerable<EnemyIntentStepConfig> AllSteps(EnemyConfig cfg) => cfg.Intents.Opening.Concat(cfg.Intents.Cycle);

        /// <summary>该敌人的普通攻击：意图表里第一个抓击/撕咬类步骤；没有的（守卫/枪手）用面板伤害的“抓击”，后排远程型打空后为弱近战（×0.7）。</summary>
        private EnemyIntentStepConfig PlainStep(EnemyConfig cfg)
        {
            var first = AllSteps(cfg).FirstOrDefault(x => x.Kind == "attack");
            if (first != null) return new EnemyIntentStepConfig { Kind = "attack", Verb = first.Verb, BitePct = first.BitePct, Control = first.Control, BleedPct = first.BleedPct, OwnSlots = first.OwnSlots, TargetSlots = first.TargetSlots };
            var rear = Digits(cfg.OwnSlots).All(p => p >= 3);
            return new EnemyIntentStepConfig
            {
                Kind = "attack", Verb = rear ? "弱近战" : AttackVerb(cfg), Factor = rear ? WeakAttackFactor : 1,
                OwnSlots = rear ? "12" : null, TargetSlots = rear ? "12" : null,
            };
        }

        /// <summary>本轮锁定的意图对应的步骤（IntentStep 与意图一致时取那一步；否则按意图找同类步骤或普通攻击）。</summary>
        public EnemyIntentStepConfig StepOf(BattleEnemy e)
        {
            var cfg = _enemies[e.EnemyId];
            if (e.IntentStep >= 0 && cfg.Intents.At(e.IntentStep) is EnemyIntentStepConfig st && KindMatches(st.Kind, e.Intent)) return st;
            if (e.Intent is "attack" or "bite") return PlainStep(cfg);
            return AllSteps(cfg).FirstOrDefault(x => KindMatches(x.Kind, e.Intent)) ?? new EnemyIntentStepConfig { Kind = e.Intent, Verb = AttackVerb(cfg) };
        }

        /// <summary>
        /// 追击用的普通攻击：意图表里第一个需要够到目标的步骤，按普通攻击执行（倍率1、无附加、不改判咬击）。
        /// 拉扯不算（试玩修复 B7d-1：拉扯是 0 伤害的位移招，追击按普通攻击结算时改用它的抓击这类普通攻击说法，Q65/Q79）。
        /// </summary>
        private EnemyIntentStepConfig PursuitStep(EnemyConfig cfg) =>
            AllSteps(cfg).FirstOrDefault(x => ReachKinds.Contains(x.Kind) && x.Kind != "pull") ?? PlainStep(cfg);

        private (HashSet<int> own, HashSet<int> target) SlotsOf(EnemyConfig cfg, EnemyIntentStepConfig st) =>
            (Digits(st.OwnSlots ?? cfg.OwnSlots), Digits(st.TargetSlots ?? cfg.TargetSlots));

        /// <summary>
        /// 轮初锁定并公开意图（不公开伤害抽值）：逃离/警戒优先；按意图循环取步骤；保护找不到相邻同伴或已用满次数、枪手没弹药可装时改普通攻击；
        /// 抓击类按 bitePct 改判咬击。
        /// </summary>
        private void LockIntent(GameState s, BattleState b, BattleEnemy e)
        {
            var cfg = _enemies[e.EnemyId];
            InitAmmo(e);
            e.IntentTarget = 0;
            if (e.WillFlee) { e.Intent = "flee"; e.IntentStep = -1; }
            else if (!e.Hostile) { e.Intent = "watch"; e.IntentStep = -1; }
            else
            {
                var idx = cfg.Intents.IndexFor(ActivationIndex(b, e));
                var st = cfg.Intents.At(idx) ?? PlainStep(cfg);
                var kind = st.Kind;
                if (kind == "protect")
                {
                    var target = ProtectTargetFor(b, e);
                    if ((st.Limit > 0 && e.ProtectUses >= st.Limit) || target == null) { kind = "attack"; idx = -2; }
                    else e.IntentTarget = target.Index;
                }
                if (kind == "reload" && (e.Reserve <= 0 || e.Mag >= cfg.Magazine)) { kind = e.Mag > 0 ? "shoot" : "attack"; idx = e.Mag > 0 ? AllStepIndex(cfg, "shoot") : -2; }
                if (kind == "shoot" && e.Mag <= 0) { kind = "attack"; idx = -2; }
                var step = idx >= 0 ? cfg.Intents.At(idx) ?? st : PlainStep(cfg);
                if (kind == "attack" && step.BitePct > 0 && s.Rng.NextDouble() * 100 < step.BitePct) kind = "bite";
                e.Intent = kind;
                e.IntentStep = idx;
            }
            e.IntentLabel = IntentLabel(e, b);
        }

        private static int AllStepIndex(EnemyConfig cfg, string kind)
        {
            var all = AllSteps(cfg).ToList();
            return all.FindIndex(x => x.Kind == kind);
        }

        /// <summary>保护的对象：相邻同伴（先后排再前排），不是已被别人保护的，不形成相互/循环保护（R03 §4、A09 EN06）。</summary>
        private BattleEnemy? ProtectTargetFor(BattleState b, BattleEnemy e)
        {
            var links = b.Alive.Where(x => x.ProtectingIndex > 0 && x.Index != e.Index).ToDictionary(x => $"e{x.Index}", x => $"e{x.ProtectingIndex}");
            foreach (var pos in new[] { e.Pos + 1, e.Pos - 1 })
            {
                var t = b.Alive.FirstOrDefault(x => x.Pos == pos);
                if (t != null && ProtectBlocked(links, $"e{e.Index}", $"e{t.Index}") == null) return t;
            }
            return null;
        }

        private string Range(EnemyConfig cfg, double f) => $"{Fmt.N(cfg.DamageMin * f)}—{Fmt.N(cfg.DamageMax * f)}";

        /// <summary>公开意图文字（轮初锁定；不公开抽值）。</summary>
        public string IntentLabel(BattleEnemy e, BattleState? b = null)
        {
            var cfg = _enemies[e.EnemyId];
            if (e.Intent == "flee") return "逃离";
            if (e.Intent == "watch") return "警戒（不主动攻击）";
            var st = StepOf(e);
            var verb = st.Verb != "" ? st.Verb : AttackVerb(cfg);
            var extra = new List<string>();
            if (st.Control == "slow") extra.Add("命中后扯腿减速");
            if (st.Control == "stagger") extra.Add("命中后可能失衡");
            if (st.BleedPct > 0) extra.Add($"{Fmt.T(st.BleedPct)}%流血");
            if (st.Knockback > 0) extra.Add($"击退{st.Knockback}位");
            if (st.ArmorBreak > 0) extra.Add($"破甲{Fmt.T(st.ArmorBreak)}（{st.ArmorBreakRounds}轮）");
            if (cfg.DeathSplash > 0) extra.Add($"死亡时喷溅1、2号位各{Fmt.T(cfg.DeathSplash)}");
            var tail = extra.Count > 0 ? "，" + string.Join("，", extra) : "";
            switch (e.Intent)
            {
                case "attack": return $"{verb} {Range(cfg, st.Factor)}{tail}";
                case "bite": return $"咬击 {Range(cfg, 1)}（可能感染）{(cfg.DeathSplash > 0 ? $"，死亡时喷溅1、2号位各{Fmt.T(cfg.DeathSplash)}" : "")}";
                case "charge": return $"{verb} {Range(cfg, st.Factor)}{tail}";
                case "charge_prepare":
                {
                    var next = AllSteps(cfg).FirstOrDefault(x => x.Kind == "charge");
                    return $"蓄力（下轮{(next?.Verb is string v && v != "" ? v : "冲撞")}）";
                }
                case "guard": return "防御";
                case "howl": return $"{verb}（恐慌+{Fmt.T(st.Panic)}）";
                case "protect": return b?.Enemies.FirstOrDefault(x => x.Index == e.IntentTarget && x.Index != e.Index) is BattleEnemy pt ? $"保护同伴（替{EnemyName(pt)}挡下一次单体攻击）" : "保护同伴";
                case "signal": return $"{verb}（呼叫附近同类增援1次）";
                case "buff": return $"{verb}（自身下一击×{Fmt.T(st.NextHitFactor)}）";
                case "sweep": return $"{verb}1、2号位 各{Range(cfg, st.Factor)}";
                case "spit": return $"{verb} {Range(cfg, st.Factor)}{tail}（不感染）";
                case "pull": return $"{verb}（命中后把目标往前拉{Math.Max(1, st.Pull)}位）";
                case "shoot": return $"{verb} {Range(cfg, st.Factor)}（弹仓{Math.Max(0, e.Mag)}，备弹{e.Reserve}）";
                case "reload": return $"{verb}（1AP，弹仓{Math.Max(0, e.Mag)}→{Math.Min(cfg.Magazine, Math.Max(0, e.Mag) + e.Reserve)}）";
            }
            return e.Intent;
        }


        // ───────────── 站位与够不到 ─────────────

        public enum ReachPlan { Execute, AdvanceThenAttack, Advance, Weak, Guard, Approach, AdvanceThenApproach }

        /// <summary>
        /// 我方暴露（Q80 空前排迫近的条件之一）：我方最前的存活角色前面全是空位（最前者不在 1 号位）。
        /// 后面有同伴挡住的角色不算暴露——迫近只打最前的那一个。
        /// </summary>
        public static bool AlliesExposed(BattleState b)
        {
            var front = Enumerable.Range(1, 4).FirstOrDefault(p => AllyAt(b, p) != null);
            return front > 1;
        }

        /// <summary>迫近的对象是主角：我方暴露且最前的存活者就是主角（敌人的攻击对象目前只有主角，队友参战在 B8）。</summary>
        public static bool PlayerExposed(BattleState b) =>
            AlliesExposed(b) && Enumerable.Range(1, 4).Select(p => AllyAt(b, p)).First(a => a != null) == GameState.PlayerId;

        /// <summary>
        /// 攻击类意图在当前站位怎么执行（显示与执行同源；ap 为本次可用 AP，缺省按正常激活）：
        /// 在己位且主角在目标位 → 执行；站得太靠后 → 前移 1 格（前格空则移、有同伴则交换；前移后够到就再普通攻击）；
        /// 后排型被压到前排 → 弱攻击×0.7；在合法己位但我方最前者躲在后排、前面全空 → 迫近 1AP + 2AP 弱近袭×0.7（Q80，AP 不够就不做）；
        /// 都不行 → 防御（只在正常激活、无合法攻击且不能合法前移时）。
        /// </summary>
        public ReachPlan PlanFor(BattleState b, BattleEnemy e, EnemyIntentStepConfig st, int? ap = null)
        {
            if (!ReachKinds.Contains(st.Kind)) return ReachPlan.Execute;
            var cfg = _enemies[e.EnemyId];
            var (own, tgt) = SlotsOf(cfg, st);
            var me = PlayerPos(b);
            var budget = ap ?? EnemyAp(e);
            var approach = Config.CombatMath.ExposureApproach.TotalAp;
            if (own.Contains(e.Pos) && tgt.Contains(me)) return ReachPlan.Execute;
            if (own.Count > 0 && e.Pos > own.Max() && e.Pos > 1)
            {
                if (own.Contains(e.Pos - 1) && tgt.Contains(me)) return ReachPlan.AdvanceThenAttack;
                if (own.Contains(e.Pos - 1) && PlayerExposed(b) && budget >= 1 + approach) return ReachPlan.AdvanceThenApproach;
                return ReachPlan.Advance;
            }
            if (own.Count > 0 && e.Pos < own.Min() && e.Pos <= 2 && me <= 2) return ReachPlan.Weak;
            if (own.Contains(e.Pos) && PlayerExposed(b) && budget >= approach) return ReachPlan.Approach;
            return ReachPlan.Guard;
        }

        /// <summary>本轮公开的攻击意图在当前站位不能照原样执行（要前移/改弱攻击/改防御）。</summary>
        public bool IntentOutOfReach(BattleState b, BattleEnemy e) => e.Alive && e.Intent is not ("flee" or "watch") && PlanFor(b, e, StepOf(e)) != ReachPlan.Execute;

        /// <summary>敌卡上显示的意图（与实际执行同源）：够不到时写“前移1位后抓击 …”“前移1位（在4号位够不到）”“弱攻击 …”。</summary>
        public string IntentShown(BattleState b, BattleEnemy e)
        {
            // 文字每次按锁定的意图与当前状态重算（弹仓/备弹实时；旧档存的文字不再直接显示，显示与执行同源，试玩修复 M150 BUG3/BUG4）
            if (!e.Alive || e.Intent is "flee" or "watch") return IntentLabel(e, b);
            var st = StepOf(e);
            var cfg = _enemies[e.EnemyId];
            var verb = e.Intent == "bite" ? "咬击" : st.Verb != "" ? st.Verb : AttackVerb(cfg);
            switch (PlanFor(b, e, st))
            {
                case ReachPlan.AdvanceThenAttack: return $"前移到{e.Pos - 1}号位后{verb} {Range(cfg, 1)}{(e.Intent == "bite" ? "（可能感染）" : "")}";
                case ReachPlan.Advance: return $"前移1位（在{e.Pos}号位够不到）";
                case ReachPlan.Weak: return $"弱攻击 {Range(cfg, WeakAttackFactor)}（在{e.Pos}号位，{verb}要在{PosList(SlotsOf(cfg, st).own)}）";
                case ReachPlan.Guard: return "防御（够不到目标）";
                case ReachPlan.Approach: return $"迫近你（{PosName(PlayerPos(b))}）弱近袭 {Range(cfg, Config.CombatMath.ExposureApproach.DamageFactor)}（{Config.CombatMath.ExposureApproach.ApproachAp}+{Config.CombatMath.ExposureApproach.PlainAttackAp}AP，不咬不控）";
                case ReachPlan.AdvanceThenApproach: return $"前移到{e.Pos - 1}号位再迫近你（{PosName(PlayerPos(b))}）弱近袭 {Range(cfg, Config.CombatMath.ExposureApproach.DamageFactor)}（共{1 + Config.CombatMath.ExposureApproach.TotalAp}AP，不咬不控）";
            }
            return IntentLabel(e, b);
        }

        // ───────────── 执行 ─────────────

        /// <summary>
        /// 敌人一次激活：正常激活清自己的防御与保护，按公开意图执行（攻击类按 PlanFor 先前移/弱攻击）；
        /// 追击激活（2AP）只做一次普通攻击或前移（A09 §1、E2），中立警戒的仍只警戒；不扣出血、不清防御。
        /// </summary>
        private void EnemyActivation(GameState s, BattleEnemy e, bool pursuit = false)
        {
            var b = s.Battle!;
            var cfg = _enemies[e.EnemyId];
            InitAmmo(e);
            e.Acted = true;
            if (pursuit)
            {
                if (!e.Hostile || e.Intent is "watch" or "flee") { BattleLog(s, $"{EnemyName(e)}（追击）警戒地观察，没有攻击。"); return; }
                var ps = PursuitStep(cfg);
                if (ps.Kind == "shoot" && e.Mag <= 0) ps = PlainStep(cfg);
                ExecuteReach(s, b, e, ps, Config.CombatMath.PursuitAp, true, true);
                return;
            }
            // 出血改在大回合开始结算（R03 §4，见 RoundStartBleed）；防御与保护到自己下一次正常激活开始清除
            e.Guard = false;
            e.ProtectingIndex = 0;
            var st = StepOf(e);
            switch (e.Intent)
            {
                case "flee":
                    e.Fled = true;
                    e.Slot = "";
                    BattleLog(s, $"{EnemyName(e)}逃离了战斗（没有尸体，不算击杀）。");
                    CheckVictory(s);
                    return;
                case "watch":
                    BattleLog(s, $"{EnemyName(e)}警戒地观察，没有攻击。");
                    return;
                case "guard":
                    e.Guard = true;
                    BattleLog(s, $"{EnemyName(e)}举盾防御（下一次受到的直接伤害×{Fmt.T(Config.CombatMath.GuardFactor)}）。");
                    return;
                case "howl":
                    b.Panic = Math.Min(Config.Panic.Range[1], b.Panic + st.Panic);
                    b.PanicPeak = Math.Max(b.PanicPeak, b.Panic);
                    BattleLog(s, $"{EnemyName(e)}{(st.Verb != "" ? st.Verb : "嚎叫")}，你的恐慌+{Fmt.T(st.Panic)}（{Fmt.N(b.Panic)}）。");
                    return;
                case "charge_prepare":
                    e.Charged = true;
                    BattleLog(s, $"{EnemyName(e)}蓄力，下轮{(AllSteps(cfg).FirstOrDefault(x => x.Kind == "charge")?.Verb is string v && v != "" ? v : "冲撞")}。");
                    return;
                case "protect":
                {
                    var t = BattleEnemyAt(b, e.IntentTarget);
                    if (t == null || !t.Alive) { BattleLog(s, $"{EnemyName(e)}要保护的同伴已不在。"); return; }
                    var links = b.Alive.Where(x => x.ProtectingIndex > 0 && x.Index != e.Index).ToDictionary(x => $"e{x.Index}", x => $"e{x.ProtectingIndex}");
                    if (ProtectBlocked(links, $"e{e.Index}", $"e{t.Index}") is string why) { BattleLog(s, $"{EnemyName(e)}没能保护{EnemyName(t)}（{why}）。"); return; }
                    e.ProtectingIndex = t.Index;
                    e.ProtectUses++;
                    BattleLog(s, $"{EnemyName(e)}护住{EnemyName(t)}（替它挡下一次单体攻击，到{EnemyName(e)}下次行动前）。");
                    return;
                }
                case "signal":
                    if (!b.ReinforcementUsed && !b.ReinforcementRequested && !b.Tutorial && Config.Noise.MaxReinforcementsPerBattle > 0)
                    {
                        b.ReinforcementRequested = true;
                        BattleLog(s, $"{EnemyName(e)}{st.Verb}：附近的同类可能被吸引过来（下一轮开始时，只调用本地已有的）。");
                    }
                    else BattleLog(s, $"{EnemyName(e)}{st.Verb}，没有回应（本场已请求过增援）。");
                    return;
                case "buff":
                    e.NextHitFactor = st.NextHitFactor > 0 ? st.NextHitFactor : 1;
                    BattleLog(s, $"{EnemyName(e)}{st.Verb}：下一击伤害×{Fmt.T(e.NextHitFactor)}。");
                    return;
                case "reload":
                {
                    var k = Math.Max(0, Math.Min(cfg.Magazine - Math.Max(0, e.Mag), e.Reserve));
                    e.Mag = Math.Max(0, e.Mag) + k;
                    e.Reserve -= k;
                    BattleLog(s, $"{EnemyName(e)}装填{k}发（弹仓{e.Mag}，备弹{e.Reserve}）。");
                    return;
                }
            }
            ExecuteReach(s, b, e, st, EnemyAp(e), false, false);
        }

        /// <summary>
        /// 攻击类意图：按站位先前移 / 弱攻击 / 迫近 / 防御，再执行（每个主意图 2AP，前移 1AP，迫近 1AP；余 AP 直接结束，
        /// 不另附攻击、不自动防御，Q77）。前移后按剩余 AP 重新判断：够到就一次普通攻击，暴露且 AP 够就迫近。
        /// </summary>
        private void ExecuteReach(GameState s, BattleState b, BattleEnemy e, EnemyIntentStepConfig st, int ap, bool plain, bool pursuit)
        {
            switch (PlanFor(b, e, st, ap))
            {
                case ReachPlan.Approach:
                    DoApproach(s, b, e);
                    return;
                case ReachPlan.Execute:
                    if (ap >= 2) DoAttackStep(s, b, e, st, plain, 1, pursuit);
                    return;
                case ReachPlan.Weak:
                    if (ap >= 2) DoAttackStep(s, b, e, st, true, WeakAttackFactor, pursuit);
                    return;
                case ReachPlan.AdvanceThenAttack:
                case ReachPlan.AdvanceThenApproach:
                case ReachPlan.Advance:
                {
                    AdvanceEnemy(s, b, e);
                    ap -= 1;
                    if (s.Battle == null || s.Progress.Dead || !e.Alive) return;
                    var next = PlanFor(b, e, st, ap);
                    if (next == ReachPlan.Execute && ap >= 2) DoAttackStep(s, b, e, st, true, 1, pursuit);
                    else if (next == ReachPlan.Approach && !pursuit) DoApproach(s, b, e);
                    else BattleLog(s, $"{EnemyName(e)}在{PosName(e.Pos)}还够不到，结束行动。");
                    return;
                }
                default:
                    if (ap >= 1 && !pursuit)
                    {
                        e.Guard = true;
                        BattleLog(s, $"{EnemyName(e)}在{PosName(e.Pos)}够不到目标，也无法前压，改为防御。");
                    }
                    else BattleLog(s, $"{EnemyName(e)}在{PosName(e.Pos)}够不到目标。");
                    return;
            }
        }

        /// <summary>
        /// 空前排迫近（Q80）：付 1AP 迫近后再付 2AP，对我方最前那名暴露角色做一次普通近袭×0.70——不咬、不感染、不附控制或特殊意图，
        /// 不移动、不重标任何一方的真实号位（所以不触发架矛迎击）；迫近 AP 已在 PlanFor 里核过。
        /// </summary>
        private void DoApproach(GameState s, BattleState b, BattleEnemy e)
        {
            var ea = Config.CombatMath.ExposureApproach;
            BattleLog(s, $"{EnemyName(e)}迫近{PosName(PlayerPos(b))}的你（{ea.ApproachAp}AP；你前面没有人挡）。");
            var step = new EnemyIntentStepConfig { Kind = "attack", Verb = "弱近袭" };
            DoAttackStep(s, b, e, step, true, ea.DamageFactor, false, approach: true);
        }

        /// <summary>敌人前移 1 位：前格空则移入、有同伴则与它交换（Q78；被换的同伴不多激活、不改锁定队列）；主动前移触发架矛迎击。</summary>
        private void AdvanceEnemy(GameState s, BattleState b, BattleEnemy e)
        {
            var ahead = b.Alive.FirstOrDefault(x => x.Pos == e.Pos - 1);
            if (ahead != null) (e.Slot, ahead.Slot) = (ahead.Slot, e.Slot);
            else e.Slot = (e.Pos - 1).ToString();
            BattleLog(s, $"{EnemyName(e)}前移到{PosName(e.Pos)}{(ahead != null ? $"（{EnemyName(ahead)}退到{PosName(ahead.Pos)}）" : "")}。");
            TryBrace(s, b, e);
        }

        /// <summary>旧接口（测试用反射调用）：按本轮公开意图攻击主角；追击按普通攻击。</summary>
        private void EnemyAttack(GameState s, BattleEnemy e, EnemyConfig cfg, bool pursuit = false) =>
            DoAttackStep(s, s.Battle!, e, pursuit ? PursuitStep(cfg) : StepOf(e), pursuit, 1, pursuit);

        /// <summary>
        /// 敌人的一次攻击（命中→伤害→附加）：命中 P=敌人面板命中/(…+主角闪避36)；伤害=抽值×意图倍率×自身下一击增益×虚弱−主角护甲（被破甲时减）再乘格挡；
        /// 只有丧尸咬击命中且正伤才判感染；扯腿/扑击/敲击失衡是需命中的控制；野犬/狼 25% 出血；喷吐破甲；扫击对我方 1、2 号位逐个判；
        /// 拉扯/击退在我方只剩一人时没有位置可移动。plain=true 为普通攻击（倍率 1、无附加、追击不改判咬击）。
        /// </summary>
        private void DoAttackStep(GameState s, BattleState b, BattleEnemy e, EnemyIntentStepConfig st, bool plain, double factorOverride, bool pursuit, bool approach = false)
        {
            var cfg = _enemies[e.EnemyId];
            var bite = !pursuit && !approach && e.Intent == "bite";   // 迫近弱近袭不咬（Q80）
            var kind = st.Kind;
            var shoot = kind == "shoot" && factorOverride >= 1;   // 弱近战（后排型被压到前排、打空后）不耗子弹
            if (shoot)
            {
                if (e.Mag <= 0) { BattleLog(s, $"{EnemyName(e)}没有子弹了。"); return; }
                e.Mag--;
            }
            var verb = (pursuit ? "追击" : "") + (bite ? "咬击" : approach ? st.Verb : factorOverride < 1 ? "弱攻击" : st.Verb != "" ? st.Verb : AttackVerb(cfg));
            var factor = plain ? factorOverride : st.Factor * factorOverride;
            // 扫击：我方 1、2 号位逐个判（只有主角时只打主角）
            if (!plain && kind == "sweep" && !(new[] { 1, 2 }.Contains(PlayerPos(b)))) { BattleLog(s, $"{EnemyName(e)}{verb}落在空处。"); return; }
            var a = Math.Max(Config.CombatMath.MinAccuracy, cfg.AttackRating);
            if (e.Stagger) { a *= Config.Statuses.Stagger.NextAttackAccuracyFactor; e.Stagger = false; e.StaggerSticky = false; }
            var p = HitChance(a, Config.Scope.BasePlayerDodge);
            if (s.Rng.NextDouble() >= p)
            {
                BattleLog(s, $"{EnemyName(e)}{verb}落空（命中率{Fmt.Pct(p)}）。");
                e.NextHitFactor = 1;
                return;
            }
            var raw = cfg.DamageMin + s.Rng.NextDouble() * (cfg.DamageMax - cfg.DamageMin);
            raw *= factor;
            if (e.NextHitFactor > 1) { raw *= e.NextHitFactor; e.NextHitFactor = 1; }
            if (EnemyWeak(b, e)) raw *= e.WeakFactor;   // 虚弱（重锤/火力压制）：直接伤害×0.8
            if (kind == "charge") e.Charged = false;
            var extra = new List<string>();
            if (!plain && kind == "pull")
            {
                // 拉扯（A09 攀附感染者，Q79/Q80）：0 直接伤害，但合法命中后照判控制，成功则真实前移一格（单人也移；1 号位边界不移、不判控制）
                var px = new List<string>();
                DisplacePlayer(s, b, -Math.Max(1, st.Pull), "pull", px);
                BattleLog(s, $"{EnemyName(e)}{verb}命中（不造成伤害）：{string.Join("，", px)}。");
                TryCounter(s, b, e);
                return;
            }
            // 主角护甲 = 基础0 + 装备护甲（≤0.9）− 被破甲（最低0）；先按磨损前的护甲算，再磨损防具（B6 R23、A05 §2）
            var dmg = Math.Max(0, raw - Math.Max(0, PlayerArmor(s) - (PlayerArmorBroken(b) ? b.PlayerArmorBreak : 0)));
            if (dmg > 0 && b.PlayerNextDamageFactor < 1) { dmg *= b.PlayerNextDamageFactor; b.PlayerNextDamageFactor = 1; }   // 救援医疗箱“稳定”
            var guarded = false;
            if (b.PlayerGuard && dmg > 0)
            {
                dmg *= b.PlayerGuardFactor;
                // 坚守有 2 次格挡，用完才清（B7 shield_10）
                b.PlayerGuardCharges = Math.Max(0, b.PlayerGuardCharges - 1);
                if (b.PlayerGuardCharges <= 0) b.PlayerGuard = false;
                guarded = true;
            }
            // 防具受到正直接伤害（扣甲前）磨损 1（A05 §2，B4 H7）；耐久 0 时防具效果暂停但仍穿着
            var armorWear = raw > 0 ? WearArmor(s, GameState.PlayerId) : "";
            if (!plain && st.ArmorBreak > 0)
            {
                var on = PlayerArmorBroken(b);
                b.PlayerArmorBreak = on ? Math.Max(b.PlayerArmorBreak, st.ArmorBreak) : st.ArmorBreak;
                b.PlayerArmorBreakUntilRound = Math.Max(on ? b.PlayerArmorBreakUntilRound : 0, b.Round + st.ArmorBreakRounds);
                extra.Add($"你被破甲（护甲−{Fmt.T(b.PlayerArmorBreak)}，到第{b.PlayerArmorBreakUntilRound}轮末）");
            }
            // 击退（Q80）：命中（含被护甲完全挡住）后判控制，成功则真实后移一格；4 号位边界不移、不判控制，伤害照算
            if (!plain && st.Knockback > 0) DisplacePlayer(s, b, st.Knockback, "knockback", extra);
            if (dmg <= 0)
            {
                if (armorWear != "") extra.Add(armorWear);
                BattleLog(s, $"{EnemyName(e)}{verb}被完全挡住{(extra.Count > 0 ? "（" + string.Join("，", extra) + "）" : "")}。");
                TryCounter(s, b, e);
                return;
            }
            s.Stats.Add(StatId.Health, -dmg);
            s.Progress.LastDamageCause = $"被{EnemyName(e)}{verb}";
            b.Panic = Math.Min(Config.Panic.Range[1], b.Panic + dmg * Config.Panic.DamageGainFactor);
            b.PanicPeak = Math.Max(b.PanicPeak, b.Panic);
            if (guarded) extra.Insert(0, $"格挡×{Fmt.T(b.PlayerGuardFactor)}");
            if (armorWear != "") extra.Add(armorWear);
            if (s.Stats.Health <= 0)
            {
                BattleLog(s, $"{EnemyName(e)}{verb}命中，健康−{Fmt.N(dmg)}。");
                BattleDeath(s, s.Progress.LastDamageCause);
                return;
            }
            if (bite)
            {
                // 只有丧尸咬击命中且造成正直接伤害才判一次病毒暴露，结果随事务保存（v0.6 §09、A09 §4）
                if (cfg.IsZombie)
                {
                    var infectedBefore = s.Body.Infected;
                    var hit = ExposeToVirus(s, out var chance);
                    extra.Add(hit ? (infectedBefore ? $"病毒暴露成功（{Fmt.Pct(chance)}），病毒+{Fmt.T(Config.Infection.OnRepeatedSuccessfulExposure.LoadDelta)}" : $"病毒暴露成功（{Fmt.Pct(chance)}），你被感染了") : $"病毒暴露判定未感染（{Fmt.Pct(chance)}）");
                }
            }
            else if (!plain)
            {
                if (st.Control is "slow" or "stagger")
                {
                    // 减速/失衡是控制：命中后再判，主角基础控制抗性 5（Q63；位移抵抗不防减速/失衡），稳态期间+30（R03 §4）
                    var steady = b.PlayerSteadyUntilRound;
                    if (RollControl(s, b, PlayerControlResist(s, b, st.Control), ref steady, out var cc))
                    {
                        b.PlayerSteadyUntilRound = steady;
                        if (st.Control == "slow") { b.PlayerSlowPending = true; extra.Add($"你被{st.Verb}，下轮速度−{Fmt.T(Config.Statuses.Slow.NextRoundSpeedPenalty)}（控制{Fmt.Pct(cc)}）"); }
                        else { b.PlayerStagger = true; b.PlayerStaggerRound = b.Round; extra.Add($"你失衡了（控制{Fmt.Pct(cc)}）"); }
                    }
                    else extra.Add($"你稳住了，没有被{(st.Control == "slow" ? "扯慢" : "打得失衡")}（控制{Fmt.Pct(cc)}）");
                }
                if (st.BleedPct > 0 && s.Rng.NextDouble() * 100 < st.BleedPct)
                {
                    ApplyPlayerBleed(s);
                    extra.Add($"你开始出血（{Fmt.T(s.Body.BleedDamage)}×{s.Body.BleedTriggers}）");
                }
            }
            BattleLog(s, $"{EnemyName(e)}{verb}命中，健康−{Fmt.N(dmg)}（恐慌{Fmt.N(b.Panic)}）{(extra.Count > 0 ? "：" + string.Join("，", extra) : "")}。");
            TryCounter(s, b, e);
        }

        /// <summary>
        /// 主角被强制位移（Q80：拉扯 −1、击退 +1）：目的格越界（1 号位再拉、4 号位再退）不移动、不判控制、不给稳态；
        /// 否则按 Q63 控制抗性（含位移抵抗）判一次控制，成功则空格移入或与该格同伴一次原子交换（不再给被动同伴判控制），
        /// 不多激活、不改已锁行动队列与追击进度。说明写进 extra。
        /// </summary>
        private void DisplacePlayer(GameState s, BattleState b, int delta, string status, List<string> extra)
        {
            var from = PlayerPos(b);
            var to = from + delta;
            var verb = delta < 0 ? "拉动" : "击退";
            if (to < 1 || to > Config.Scope.Slots.Count)
            {
                extra.Add($"你已在{PosName(from)}，无法再被{verb}（不判控制）");
                return;
            }
            var steady = b.PlayerSteadyUntilRound;
            if (!RollControl(s, b, PlayerControlResist(s, b, status), ref steady, out var cc))
            {
                extra.Add($"你稳住了，没有被{verb}（控制{Fmt.Pct(cc)}）");
                return;
            }
            b.PlayerSteadyUntilRound = steady;
            var mate = AllyAt(b, to);
            MoveAlly(b, from, to);
            extra.Add($"你被{verb}到{PosName(to)}{(mate != null ? "（与同伴交换）" : "")}（控制{Fmt.Pct(cc)}）");
        }

        /// <summary>膨胀感染者死亡时喷溅我方 1、2 号位：各 H−值，护甲照算，不判感染（A09 §1）。</summary>
        private void DeathSplash(GameState s, BattleState b, BattleEnemy e, EnemyConfig cfg)
        {
            if (cfg.DeathSplash <= 0 || s.Progress.Dead || PlayerPos(b) > 2) return;
            var dmg = Math.Max(0, cfg.DeathSplash - Math.Max(0, PlayerArmor(s) - (PlayerArmorBroken(b) ? b.PlayerArmorBreak : 0)));
            if (dmg <= 0) { BattleLog(s, $"{cfg.Name}死亡喷溅被护甲挡住。"); return; }
            s.Stats.Add(StatId.Health, -dmg);
            s.Progress.LastDamageCause = $"{cfg.Name}死亡喷溅";
            BattleLog(s, $"{cfg.Name}死亡喷溅：你健康−{Fmt.N(dmg)}（不感染）。");
        }
    }
}
