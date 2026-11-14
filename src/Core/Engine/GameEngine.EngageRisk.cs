using System;
using System.Collections.Generic;
using System.Linq;
using Survival.Core.Config;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>交战风险五档（R05「2026-11-14 Q613主控定稿」）。</summary>
    public enum EngageRiskLevel { VeryLow, Low, Medium, High, VeryHigh }

    /// <summary>一张敌踪的交战风险评估结果（派生值，不存档）。</summary>
    public sealed class EngageRiskResult
    {
        /// <summary>没有有效敌方活成员：不显示等级，详情写 <see cref="GameEngine.EnemyCardInvalidText"/>。</summary>
        public bool Invalid { get; set; }
        /// <summary>数据读取失败或必要定义缺失：显示「交战风险：暂无法评估」，不猜等级。</summary>
        public bool Unknown { get; set; }
        public EngageRiskLevel Level { get; set; }
        /// <summary>首屏文字：「交战风险：{等级}」或「交战风险：暂无法评估」；失效为 <see cref="GameEngine.EnemyCardInvalidText"/>。</summary>
        public string Text { get; set; } = "";
    }

    /// <summary>后台六项（测试与开发查看用；不进玩家界面）。</summary>
    public sealed class EngageRiskFactors
    {
        public int Allies { get; set; }
        public int Enemies { get; set; }
        public double HA { get; set; }
        public double DA { get; set; }
        public double VA { get; set; }
        public double HE { get; set; }
        public double DE { get; set; }
        public double VE { get; set; }
    }

    /// <summary>
    /// BM5b1「敌踪交战风险」（R05「2026-11-14 Q613主控定稿：当前队伍对真实敌踪的即时风险」）：当前已保存编队真正会参战的存活成员，对这张敌踪此刻实际入场名单的
    /// 直接攻防估计。在状态副本上搭一场只读的临时战斗（同 StartBattle 的容量、入场号位、开战武器页，不抽受惊、不抽同速随机、不锁意图、不给先手），
    /// 我方取各成员当前武器页可合法发出的直接伤害攻击（含应急拳击），敌方取原普通攻击；不调用真实入战／出手，不消耗随机序列，不改原状态。
    /// </summary>
    public sealed partial class GameEngine
    {
        // ───────────── 正式文字（R05 Q613） ─────────────

        public const string EngageRiskPrefix = "交战风险：";
        public const string EngageRiskUnknownText = "交战风险：暂无法评估";
        public const string EngageRiskNote = "按当前编队与状态评估，仅供参考。";
        public const string EnemyCardInvalidText = "这处敌踪已失效，请重新选择。";
        /// <summary>「战斗详情」里没有合适攻击者／目标时的命中率值。</summary>
        public const string NoAttackHitText = "当前无可用攻击";

        public static readonly string[] EngageRiskLevelNames = { "很低", "较低", "中等", "较高", "极高" };

        // ───────────── 分档（主控授权的可调初值，改动须同步 R05 本节与验收） ─────────────

        private static readonly double[] EngageRiskThresholds = { 0.35, 0.75, 1.35, 2.20 };
        private const double EngageSpeedClampMin = 0.75;
        private const double EngageSpeedClampMax = 1.5;

        public static string EngageRiskText(EngageRiskLevel level) => EngageRiskPrefix + EngageRiskLevelNames[(int)level];

        /// <summary>R 分档：&lt;0.35 很低、&lt;0.75 较低、&lt;1.35 中等、&lt;2.20 较高、其余极高；双精度直接比较，不先取整。</summary>
        public static EngageRiskLevel EngageRiskLevelOf(double r)
        {
            for (var k = 0; k < EngageRiskThresholds.Length; k++)
                if (r < EngageRiskThresholds[k]) return (EngageRiskLevel)k;
            return EngageRiskLevel.VeryHigh;
        }

        /// <summary>风险指数 R=(H_E×D_E)/(H_A×D_A)×clamp(sqrt(V_E/V_A),0.75,1.5)，速度最低 1。调用方先处理零值。</summary>
        public static double EngageRiskIndex(double hA, double dA, double vA, double hE, double dE, double vE)
        {
            var speed = Math.Max(EngageSpeedClampMin, Math.Min(EngageSpeedClampMax, Math.Sqrt(Math.Max(1, vE) / Math.Max(1, vA))));
            return hE * dE / (hA * dA) * speed;
        }

        /// <summary>
        /// 零值优先的分档：没有有效敌方活成员为失效；无有效我方活成员／H_A=0／D_A=0 一律极高（双方输出均 0 也极高）；我方有有效输出而 D_E=0 为很低；
        /// 其余按 R 分档。非有限值不猜，为 null（暂无法评估）。
        /// </summary>
        public static EngageRiskLevel? EngageRiskClassify(EngageRiskFactors f)
        {
            if (f.Allies <= 0 || f.HA <= 1e-12 || f.DA <= 1e-12) return EngageRiskLevel.VeryHigh;
            if (f.DE <= 1e-12) return EngageRiskLevel.VeryLow;
            var r = EngageRiskIndex(f.HA, f.DA, f.VA, f.HE, f.DE, f.VE);
            return double.IsNaN(r) || double.IsInfinity(r) ? (EngageRiskLevel?)null : EngageRiskLevelOf(r);
        }

        /// <summary>某张敌踪的交战风险（只读、确定性；同一事实快照必得同一结果）。</summary>
        public EngageRiskResult EngageRiskFor(GameState s, EnemyCardView card)
        {
            try
            {
                var f = EngageRiskFactorsFor(s, card);
                if (f == null) return new EngageRiskResult { Invalid = true, Text = EnemyCardInvalidText };
                var level = EngageRiskClassify(f);
                return level is EngageRiskLevel l
                    ? new EngageRiskResult { Level = l, Text = EngageRiskText(l) }
                    : new EngageRiskResult { Unknown = true, Text = EngageRiskUnknownText };
            }
            catch (Exception)
            {
                // 数据读取失败或必要定义缺失（如敌种、武器定义不在配置里）：不猜等级
                return new EngageRiskResult { Unknown = true, Text = EngageRiskUnknownText };
            }
        }

        /// <summary>
        /// 后台六项（只读）：在状态副本上按真实入战口径搭临时战斗后求 H、D、V。没有有效敌方活成员为 null。
        /// 定义缺失时抛出，由 <see cref="EngageRiskFor"/> 归为暂无法评估。
        /// </summary>
        public EngageRiskFactors? EngageRiskFactorsFor(GameState s, EnemyCardView card)
        {
            // 敌方：这张卡此刻实际主动处理会纳入的名单（随机组不计隐藏成员；可选强敌未生成实体时只读其既定配置），按真实容量截取，不生成实体
            var members = EnemyCardPreviewMembers(s, card).Where(m => (m.Enemy?.Hp ?? _enemies[m.EnemyId].Hp) > 1e-9).ToList();
            if (members.Count == 0) return null;

            var c = s.Clone();
            c.SuppressEnemyApproach = true;
            // 战外查看：对白输入锁只拦真实操作，不改队伍的攻防能力（副本里不计未读完的对白）
            c.Story.DialogueLog = c.Story.DialogueLog.Where(d => d.Ended).ToList();
            c.Battle = null;
            var b = new BattleState
            {
                LocationId = c.CurrentLocation, StartMinute = c.GameMinute, Context = FearApproach, CardKey = card.Key, LogComplete = true,
                ActiveWeaponSlot = DefaultWeaponPage(c),
            };
            b.Capacity = BattleCapacity(c);
            b.Allies = EntryAllies(c, b.Capacity);
            b.Members = new List<string> { GameState.PlayerId };
            b.Members.AddRange(b.Allies.Where(a => a != "" && a != GameState.PlayerId));
            c.Battle = b;
            foreach (var id in b.Members.Skip(1))
                using (FocusOn(c, id)) b.ActiveWeaponSlot = DefaultWeaponPage(c);
            var joined = members.Take(Math.Min(b.Capacity, Config.Scope.Slots.Count)).ToList();
            for (var k = 0; k < joined.Count; k++)
            {
                var (enemyId, we) = joined[k];
                b.Enemies.Add(InitBattleEnemy(new BattleEnemy
                {
                    WorldId = we?.Id ?? -(k + 1), Index = k + 1, EnemyId = enemyId, Slot = (k + 1).ToString(),
                    Hp = we?.Hp ?? _enemies[enemyId].Hp, Hostile = we?.Hostile ?? true, JoinRound = 1,
                }));
            }

            var f = new EngageRiskFactors();
            // 我方：按已保存编队与原入战资格真正参战的存活成员（含主角）；当前健康、体力、清醒、技能、装备、武器页、弹药均取现在事实
            var allySpeed = new List<double>();
            foreach (var id in b.Members.ToList())
            {
                if (!c.Actors.TryGetValue(id, out var actor) || actor.Stats.Health <= 1e-9) continue;
                if (id == GameState.PlayerId && c.Progress.Dead) continue;
                using (FocusOn(c, id))
                {
                    f.Allies++;
                    f.HA += Math.Max(0, c.Stats.Health);
                    allySpeed.Add(PlayerSpeedParts(c, b).Speed);
                    var me = b.Me;
                    var ap = NormalActivationAp(c.Player);
                    me.Acting = true;
                    me.Ap = ap;
                    me.ActivationAp = ap;
                    me.RoundStartEnergy = c.Stats.Energy;
                    try { f.DA += BestAllyAttackScore(c, b, ap); }
                    finally { me.Acting = false; me.Ap = 0; }
                }
            }
            f.VA = allySpeed.Count > 0 ? Math.Max(1, allySpeed.Average()) : 1;

            var enemySpeed = new List<double>();
            foreach (var be in b.Enemies)
            {
                f.Enemies++;
                f.HE += Math.Max(0, be.Hp);
                enemySpeed.Add(EnemyEffectiveSpeed(be));
                f.DE += EnemyPlainScore(c, b, be);
            }
            f.VE = enemySpeed.Count > 0 ? Math.Max(1, enemySpeed.Average()) : 1;
            return f;
        }

        /// <summary>
        /// 一名我方成员的 D_i=max(n×d)：当前武器页可合法发出的直接伤害攻击（含原允许的应急拳击）。d 取原预演（命中、暴击、伤害区间已扣护甲），
        /// 单目标对合法目标取平均、多目标对真实可影响集合加总；n=min(AP/AP成本, 体力/体力成本, 弹药/每次耗弹, 耐久可供次数)，AP 成本 0 或有冷却最多 1 次。
        /// </summary>
        private double BestAllyAttackScore(GameState s, BattleState b, int ap)
        {
            var w = CurrentWeapon(s, out var wi);
            var best = 0.0;
            foreach (var opt in SkillOptions(s, true))
            {
                if (!opt.Available) continue;
                var desperate = opt.Action == DesperatePunch;
                SkillNodeConfig? node = null;
                SkillExecConfig? x = null;
                if (!desperate)
                {
                    node = SkillOf(opt.Action, w);
                    if (node == null || !_exec.TryGetValue(node.Id, out x) || !x.IsAttack) continue;
                }
                var sp = SpecFor(s, opt.Action, w);
                if (sp.DamageFactor <= 0 || sp.Weapon.DamageMax <= 0) continue;   // 无伤害的控制／增益不算
                var commands = opt.NeedsTarget ? opt.Targets.Select(t => BattleCommand.Of(opt.Action, t)).ToList() : new List<BattleCommand> { BattleCommand.Of(opt.Action) };
                double sum = 0;
                var count = 0;
                var apCost = 0;
                double energyCost = 0;
                foreach (var cmd in commands)
                {
                    if (CommandBlocked(s, cmd) != null) continue;
                    var p = PreviewCommand(s, cmd);
                    apCost = p.Ap;
                    energyCost = p.EnergyCost;
                    // 多段：每段各算后相加；词条平加只在同一目标第一下（同结算），后续段按无平加的同一区间
                    sum += p.Targets.Sum(t => ExpectedHit(t) + Math.Max(0, sp.Hits - 1) * ExpectedHitNoFlat(t, sp.Weapon));
                    count++;
                }
                if (count == 0) continue;
                var d = sum / count;
                var n = apCost <= 0 ? 1 : ap / apCost;
                if ((node?.CooldownRounds ?? 0) > 0) n = Math.Min(n, 1);
                if (energyCost > 1e-9) n = Math.Min(n, (int)Math.Floor((s.Stats.Energy + 1e-9) / energyCost));
                if (!desperate)
                {
                    var need = AmmoNeed(x!, w);
                    if (need > 0 && w.AmmoId != null)
                        n = Math.Min(n, (w.UsesMagazine ? wi?.LoadedAmmo ?? 0 : CarriedCount(s, w.AmmoId)) / need);
                    var wear = Config.Wear.PerExecutedAttackCommand;
                    if (wi?.Durability is double dur && wear > 1e-12) n = Math.Min(n, (int)Math.Floor((dur + 1e-8) / wear));
                }
                best = Math.Max(best, Math.Max(0, n) * d);
            }
            return best;
        }

        /// <summary>单次期望伤害 d=p_hit×[(1−p_crit)×(d_min+d_max)/2+p_crit×(c_min+c_max)/2]。</summary>
        private static double ExpectedHit(BattleTargetPreview t)
        {
            var crit = Math.Max(0, Math.Min(1, t.CritChance));
            return Math.Max(0, Math.Min(1, t.HitChance)) * ((1 - crit) * (t.DamageMin + t.DamageMax) / 2 + crit * (t.CritDamageMin + t.CritDamageMax) / 2);
        }

        /// <summary>同一目标第二段起的单次期望（无词条平加；区间沿原预演的倍率、护甲、穿透与格挡口径）。</summary>
        private double ExpectedHitNoFlat(BattleTargetPreview t, WeaponConfig w)
        {
            if (t.Flat <= 1e-12) return ExpectedHit(t);
            var m = Config.CombatMath;
            var armor = Math.Max(0, t.Armor - t.Penetration);
            double Dmg(double raw, double critF) => AttackDamage(raw * t.Multiplier * critF, 0, armor, t.Guarded, m.GuardFactor);
            var crit = Math.Max(0, Math.Min(1, t.CritChance));
            return Math.Max(0, Math.Min(1, t.HitChance)) * ((1 - crit) * (Dmg(w.DamageMin, 1) + Dmg(w.DamageMax, 1)) / 2 + crit * (Dmg(w.DamageMin, m.CritFactor) + Dmg(w.DamageMax, m.CritFactor)) / 2);
        }

        /// <summary>
        /// 一名敌人的 D_j：原普通攻击（<see cref="PlainStep"/>）在当前入场号位、本次正常激活 AP 下的原合法路径（同 ExecuteReach：够得到出手、被压前排弱攻击、
        /// 空前排迫近、站太后先前移再出手），每次正常激活一次主攻击；对所有合法我方目标的 d 取平均。不读已锁意图、不改真实位置、不触发迎击／随机；无合法伤害路径为 0。
        /// </summary>
        private double EnemyPlainScore(GameState s, BattleState b, BattleEnemy e)
        {
            var cfg = _enemies[e.EnemyId];
            var st = PlainStep(cfg);
            var ap = EnemyAp(e);
            var ea = Config.CombatMath.ExposureApproach;
            switch (PlanFor(b, e, st, ap))
            {
                case ReachPlan.Execute:
                    return ap >= EnemyMainIntentAp ? EnemyHitScore(s, b, cfg, st.Factor, SlotsOf(cfg, st).target) : 0;
                case ReachPlan.Weak:
                    return ap >= EnemyMainIntentAp ? EnemyHitScore(s, b, cfg, WeakAttackFactor, FrontSlots) : 0;
                case ReachPlan.Approach:
                    return EnemyApproachScore(s, b, cfg, ea.DamageFactor);
                case ReachPlan.AdvanceThenAttack:
                case ReachPlan.AdvanceThenApproach:
                case ReachPlan.Advance:
                {
                    if (ap < 1) return 0;
                    // 前移 1 格（前格空则移入、有同伴则交换）后重验，只在临时战斗里试，结束复原
                    var ahead = b.Alive.FirstOrDefault(x => x.Pos == e.Pos - 1);
                    var (mine, theirs) = (e.Slot, ahead?.Slot);
                    if (ahead != null) (e.Slot, ahead.Slot) = (ahead.Slot, e.Slot);
                    else e.Slot = (e.Pos - 1).ToString();
                    try
                    {
                        var left = ap - 1;
                        var next = PlanFor(b, e, st, left);
                        if (next == ReachPlan.Execute && left >= EnemyMainIntentAp) return EnemyHitScore(s, b, cfg, 1, SlotsOf(cfg, st).target);
                        if (next == ReachPlan.Approach) return EnemyApproachScore(s, b, cfg, ea.DamageFactor);
                        return 0;
                    }
                    finally
                    {
                        e.Slot = mine;
                        if (ahead != null) ahead.Slot = theirs!;
                    }
                }
                default:
                    return 0;
            }
        }

        /// <summary>迫近后的弱近袭只打我方最前的暴露者。</summary>
        private double EnemyApproachScore(GameState s, BattleState b, EnemyConfig cfg, double factor) =>
            FrontmostAlly(b) is string f ? EnemyHitScore(s, b, cfg, factor, new HashSet<int> { b.Allies.IndexOf(f) + 1 }) : 0;

        /// <summary>敌方一击对目标位内全部合法我方的 d 平均：命中同 AttackOneAlly（攻击能力对本人闪避），区间按本人护甲扣减；敌方无暴击。</summary>
        private double EnemyHitScore(GameState s, BattleState b, EnemyConfig cfg, double factor, HashSet<int> slots)
        {
            var cands = EnemyTargetCandidates(b, slots);
            if (cands.Count == 0) return 0;
            var a = Math.Max(Config.CombatMath.MinAccuracy, cfg.AttackRating);
            double sum = 0;
            foreach (var id in cands)
                using (FocusOn(s, id))
                {
                    var p = HitChance(a, MyBaseDodge(s));
                    var armor = Math.Max(0, PlayerArmor(s));
                    var lo = Math.Max(0, cfg.DamageMin * factor - armor);
                    var hi = Math.Max(0, cfg.DamageMax * factor - armor);
                    sum += p * (lo + hi) / 2;
                }
            return sum / cands.Count;
        }

        /// <summary>「战斗详情」命中率行有没有可预估的攻击：开战默认页武器能直接攻击且普通攻击已可用；否则写「当前无可用攻击」。</summary>
        public bool OutOfBattleHasAttack(GameState s)
        {
            WeaponOnPage(s, DefaultWeaponPage(s), out var w);
            return w.CanAttack && SkillOf(BasicAttack, w) is SkillNodeConfig n && SkillUnlocked(s, n);
        }
    }
}
