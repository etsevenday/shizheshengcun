using System;
using System.Collections.Generic;
using System.Linq;
using Survival.Core.Config;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>某角色穿戴装备的效果合成结果（A05 §4 末段、R16；开发计划 v0.9 B4 H3）。</summary>
    public sealed class GearEffects
    {
        /// <summary>装备速度修正：正加成合计（封顶前）、负修正合计、封顶后的总修正（正加成最多 +3，负修正照算）。</summary>
        public double SpeedPositiveRaw;
        public double SpeedNegative;
        public double Speed;
        /// <summary>护甲合计（封顶前）与封顶后（0—0.9）。</summary>
        public double ArmorRaw;
        public double Armor;
        public double Accuracy;
        public double Crit;
        public double DisplaceResist;
        /// <summary>本页武器的攻击类词条：忽略护甲、直接伤害乘数加值、E 成本倍率、噪声修正（只取本次所用武器，A05 §5）。</summary>
        public double ArmorIgnore;
        public double DamageMultiplier;
        public double EnergyCostFactor = 1;
        public double Noise;
        /// <summary>求生乘数（work.*、rate.*、hazard.*、eat.*）：同名固定效果取强、同名词条取强，二者相乘。</summary>
        public Dictionary<string, double> Factors = new Dictionary<string, double>();
        /// <summary>求生加值（pos.*、bandage.*）：同名固定效果取强 + 同名词条取强。</summary>
        public Dictionary<string, double> Adds = new Dictionary<string, double>();
        /// <summary>替代值（repair.fraction）：取最高。</summary>
        public Dictionary<string, double> Sets = new Dictionary<string, double>();
        /// <summary>耐久为 0、效果暂停的装备名（仍占槽、计重，A05 §2）。</summary>
        public List<string> Paused = new List<string>();

        public double Factor(string key) => Factors.TryGetValue(key, out var v) ? v : 1;
        public double Add(string key) => Adds.TryGetValue(key, out var v) ? v : 0;
        public double? Set(string key) => Sets.TryGetValue(key, out var v) ? v : (double?)null;
    }

    /// <summary>
    /// 装备效果合成（R16）：基础特性先结算，再附加词条；战斗速度正加成合计最多 +3（负修正照算），总护甲不超过 0.9；
    /// 求生同类工作量合成下限 ×0.70，负 E/N/W 小时率合成下限 ×0.75，之后再乘现行 S01 工作倍率；同名效果取强不叠；
    /// 攻击类词条只取本次所用武器（不借给另一把）；耐久 0 的装备效果暂停。
    /// 开发假设 H3：combat.* 在 B6 四对四战斗内核之前只聚合显示，不改现行先手、命中与伤害；求生侧能接上的效果直接生效。
    /// </summary>
    public sealed partial class GameEngine
    {
        /// <summary>一条生效中的效果及来源（界面逐条列出用）。</summary>
        public sealed class ActiveEffect
        {
            public EquipEffectConfig Effect = new EquipEffectConfig();
            public ItemInstance Source = new ItemInstance();
            /// <summary>来自附加词条（否则为装备固定效果/武器速度修正）。</summary>
            public string? AffixId;
        }

        /// <summary>
        /// 某角色身上生效的全部效果（不含已暂停的）：穿戴物的固定效果、武器速度修正、附加词条；攻击类词条只取 attackPage（1/2）那一页的武器，
        /// attackPage 为空时按战斗当前页或开战默认页。paused 收集耐久 0 而暂停的装备。
        /// </summary>
        public List<ActiveEffect> ActiveEffects(GameState s, string actorId, int? attackPage, List<string>? paused = null)
        {
            var list = new List<ActiveEffect>();
            var page = attackPage ?? s.Battle?.ActiveWeaponSlot ?? DefaultWeaponPage(s);
            var pageSlot = EquipSlots.WeaponSlotOfPage(page);
            foreach (var slot in EquipSlots.All)
            {
                var it = WornItem(s, actorId, slot);
                if (it == null) continue;
                if (it.Durability.HasValue && it.Durability.Value <= 1e-9)
                {
                    paused?.Add($"{ItemName(it.ItemId)}（{EquipSlots.Name(slot)}）");
                    continue;
                }
                if (Weapon(it.ItemId) is WeaponConfig w && Math.Abs(w.Speed) > 1e-12)
                    list.Add(new ActiveEffect { Effect = new EquipEffectConfig { Key = "combat.speed", Op = "add", Value = w.Speed }, Source = it });
                if (Gear(it.ItemId) is GearConfig g)
                    foreach (var e in g.Effects) list.Add(new ActiveEffect { Effect = e, Source = it });
                foreach (var id in it.Affixes ?? new List<string>())
                {
                    var a = Affix(id);
                    if (a == null) continue;
                    foreach (var e in a.Effects)
                    {
                        if (e.Key == "durability.max") continue;          // 只在生成时作用
                        if (e.AttackScoped && slot != pageSlot) continue;   // 攻击附加不跨武器借用
                        list.Add(new ActiveEffect { Effect = e, Source = it, AffixId = id });
                    }
                }
            }
            return list;
        }

        /// <summary>合成某角色的装备效果（见类注释的上限与取强规则）。</summary>
        public GearEffects EquipmentEffects(GameState s, string actorId = GameState.PlayerId, int? attackPage = null)
        {
            var r = new GearEffects();
            var active = ActiveEffects(s, actorId, attackPage, r.Paused);
            var lim = Config.Equipment.Limits;
            // 战斗加值：不同装备的不同特性相加；同 ID 的词条出现在多件装备上只取强（同名不叠）
            var combat = active.Where(x => x.Effect.Key.StartsWith("combat.")).ToList();
            var combatAdds = combat.Where(x => x.AffixId == null).Select(x => (x.Effect.Key, x.Effect.Op, x.Effect.Value))
                .Concat(combat.Where(x => x.AffixId != null).GroupBy(x => (x.AffixId, x.Effect.Key))
                    .Select(gp => gp.OrderByDescending(x => Math.Abs(x.Effect.Value - (x.Effect.Op == "mul" ? 1 : 0))).First())
                    .Select(x => (x.Effect.Key, x.Effect.Op, x.Effect.Value)))
                .ToList();
            foreach (var (key, op, v) in combatAdds)
            {
                switch (key)
                {
                    case "combat.speed": if (v > 0) r.SpeedPositiveRaw += v; else r.SpeedNegative += v; break;
                    case "combat.armor": r.ArmorRaw += v; break;
                    case "combat.accuracy": r.Accuracy += v; break;
                    case "combat.crit": r.Crit += v; break;
                    case "combat.displaceResist": r.DisplaceResist += v; break;
                    case "combat.armorIgnore": r.ArmorIgnore += v; break;
                    case "combat.damageMultiplier": r.DamageMultiplier += v; break;
                    case "combat.energyCost": if (op == "mul") r.EnergyCostFactor *= v; break;
                    case "combat.noise": r.Noise += v; break;
                }
            }
            r.Speed = Math.Min(lim.PositiveSpeedCap, r.SpeedPositiveRaw) + r.SpeedNegative;
            r.Armor = Math.Max(0, Math.Min(lim.ArmorCap, r.ArmorRaw));

            // 求生：同名固定效果取强、同名词条取强，二者合成
            foreach (var key in active.Select(x => x.Effect.Key).Where(k => !k.StartsWith("combat.")).Distinct())
            {
                var rows = active.Where(x => x.Effect.Key == key).ToList();
                var op = rows[0].Effect.Op;
                double Strongest(IEnumerable<ActiveEffect> xs, double neutral) =>
                    xs.Select(x => x.Effect.Value).DefaultIfEmpty(neutral).OrderByDescending(v => Math.Abs(v - neutral)).First();
                var baseRows = rows.Where(x => x.AffixId == null);
                var affixRows = rows.Where(x => x.AffixId != null);
                switch (op)
                {
                    case "mul": r.Factors[key] = Strongest(baseRows, 1) * Strongest(affixRows, 1); break;
                    case "add": r.Adds[key] = Strongest(baseRows, 0) + Strongest(affixRows, 0); break;
                    case "set": r.Sets[key] = rows.Max(x => x.Effect.Value); break;
                }
            }
            return r;
        }

        // ───────────── 求生侧接入（R16；开发假设 H3） ─────────────

        /// <summary>该行动对应的求生工作量效果键（制造/建造、移动、搜集）。</summary>
        private IEnumerable<string> WorkKeys(ActionType type, string? targetId)
        {
            switch (type)
            {
                case ActionType.Craft:
                case ActionType.Build:
                    yield return "work.craft";
                    break;
                case ActionType.Travel:
                    yield return "work.move";
                    yield return "work.route";      // 只能沿已发现的路线移动，所以每次移动都是“已发现路线”
                    break;
                case ActionType.Gather:
                    if (targetId != null && _res.TryGetValue(targetId, out var res) && res.IsCache) yield return "work.scavenge";
                    break;
            }
        }

        /// <summary>
        /// 装备的求生工作量倍率（乘在基础工作量上，之后再乘 S01 工作倍率 M）：相关键相乘，下限 ×0.70；reasons 写“装备：耐磨工作衣×0.95”。
        /// </summary>
        public double GearWorkFactor(GameState s, string actorId, ActionType type, string? targetId, List<string>? reasons = null)
        {
            var keys = WorkKeys(type, targetId).ToList();
            if (keys.Count == 0) return 1;
            var fx = EquipmentEffects(s, actorId);
            var f = keys.Aggregate(1.0, (acc, k) => acc * fx.Factor(k));
            var floor = Config.Equipment.Limits.SurvivalWorkFloor;
            if (f < floor) f = floor;
            if (Math.Abs(f - 1) > 1e-12) reasons?.Add($"装备工作量×{Fmt.T(f)}");
            return f;
        }

        /// <summary>该行动在各状态上的负小时率效果键：非战斗 N/W 全行动适用；E 按采集/搜集与清理/移动。</summary>
        private List<(StatId stat, string key)> RateKeys(ActionType type, string? targetId)
        {
            var list = new List<(StatId, string)>
            {
                (StatId.Satiety, "rate.nonCombat.satiety"),
                (StatId.Hydration, "rate.nonCombat.hydration"),
            };
            switch (type)
            {
                case ActionType.Travel: list.Add((StatId.Energy, "rate.move.energy")); break;
                case ActionType.Clear: list.Add((StatId.Energy, "rate.scavengeClear.energy")); break;
                case ActionType.Gather:
                    var cache = targetId != null && _res.TryGetValue(targetId, out var res) && res.IsCache;
                    list.Add((StatId.Energy, cache ? "rate.scavengeClear.energy" : "rate.gather.energy"));
                    break;
            }
            return list;
        }

        /// <summary>
        /// 把装备的求生速率效果作用到本段（只在非战斗的行动段，ComputeSegment 调用）：负小时率按键相乘、每项下限 ×0.75、只缩负值；
        /// 遮蔽休息/睡眠的正体力恢复加值（先加算，之后才受精神低于阈值折半）。
        /// </summary>
        private void ApplyGearRates(GameState s, ActorState actor, ActionState a, SegmentContext ctx)
        {
            var fx = EquipmentEffects(s, actor.Id);
            if (fx.Factors.Count == 0 && fx.Adds.Count == 0) return;
            var floor = Config.Equipment.Limits.NegativeRateFloor;
            foreach (var g in RateKeys(a.Type, a.TargetId).GroupBy(x => x.stat))
            {
                var f = g.Aggregate(1.0, (acc, x) => acc * fx.Factor(x.key));
                if (f < floor) f = floor;
                if (Math.Abs(f - 1) < 1e-12) continue;
                var v = Get(ctx.Rates, g.Key);
                if (v < 0) Set(ctx.Rates, g.Key, v * f);
            }
            if (ctx.Rates.Energy > 0)
            {
                if (a.Type == ActionType.Rest && ctx.Sheltered) ctx.Rates.Energy += fx.Add("pos.shelteredRest.energy");
                if (a.Type == ActionType.Sleep) ctx.Rates.Energy += fx.Add("pos.sleep.energy");
            }
        }

        private static double Get(RateBlock r, StatId id) => id switch
        {
            StatId.Satiety => r.Satiety,
            StatId.Hydration => r.Hydration,
            StatId.Energy => r.Energy,
            StatId.Wakefulness => r.Wakefulness,
            StatId.Morale => r.Morale,
            _ => 0,
        };

        private static void Set(RateBlock r, StatId id, double v)
        {
            switch (id)
            {
                case StatId.Satiety: r.Satiety = v; break;
                case StatId.Hydration: r.Hydration = v; break;
                case StatId.Energy: r.Energy = v; break;
                case StatId.Wakefulness: r.Wakefulness = v; break;
                case StatId.Morale: r.Morale = v; break;
            }
        }

        /// <summary>
        /// 搜集划伤概率 × 装备 hazard.woodScrapChance（采集手套“木材/废料采集划伤概率×0.80”）：只算木材点（首项产物木材）与废料（产物含废金属）的资源点；
        /// 村庄杂物箱（布料为主、附带木材）不算。
        /// </summary>
        public double HazardChance(GameState s, ResourceConfig res)
        {
            if (res.Hazard == null) return 0;
            var p = res.Hazard.Probability;
            if (IsWoodOrScrapGather(res)) p *= EquipmentEffects(s).Factor("hazard.woodScrapChance");
            return p;
        }

        public static bool IsWoodOrScrapGather(ResourceConfig res) =>
            res.Outputs.Count > 0 && res.Outputs.Keys.First() == "wood" || res.Outputs.ContainsKey("scrap");

        /// <summary>搜集划伤的直接健康损失 × 装备 hazard.health（环境划伤，不影响战斗）。</summary>
        public double HazardLoss(GameState s, ResourceConfig res) =>
            res.Hazard == null ? 0 : res.Hazard.HealthLoss * EquipmentEffects(s).Factor("hazard.health");

        /// <summary>工具维护恢复比例：配方 20% 与装备替代比例（维护手册 25%）取较高者，不相加（A05 §2、§5）。</summary>
        public double RepairFraction(GameState s, RecipeConfig rc) =>
            Math.Max(rc.RestoreOwnDurabilityMaxFraction, EquipmentEffects(s).Set("repair.fraction") ?? 0);

        /// <summary>非战斗食用的正饱食收益 × 装备 eat.satiety（配给袋 ×1.05）；负值与战斗中不变。</summary>
        public double UseSatiety(GameState s, ItemUseConfig u) =>
            u.Satiety > 0 && s.Battle == null ? u.Satiety * EquipmentEffects(s).Factor("eat.satiety") : u.Satiety;
    }
}
