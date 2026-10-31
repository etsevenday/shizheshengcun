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
        /// <summary>装备位移抵抗：不同来源合计（封顶前）与封顶 40 后（Q63，只防拉扯/击退）。</summary>
        public double DisplaceResistRaw;
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
                // 武器固定特性里的位移抵抗（防暴盾“稳固”+25，Q63 位移装备来源）：穿着就算，不分页
                if (Weapon(it.ItemId) is WeaponConfig tw && tw.Trait("displaceResist") is double dr && dr > 0)
                    list.Add(new ActiveEffect { Effect = new EquipEffectConfig { Key = "combat.displaceResist", Op = "add", Value = dr }, Source = it });
                if (Gear(it.ItemId) is GearConfig g)
                    foreach (var e in g.Effects) list.Add(new ActiveEffect { Effect = e, Source = it });
                foreach (var id in it.Affixes ?? new List<string>())
                {
                    var a = Affix(id);
                    if (a == null) continue;
                    foreach (var e in a.Effects)
                    {
                        if (e.Key == "durability.max") continue;          // 只在生成时作用
                        // 攻击附加不跨武器借用；饰品上的词条作用于佩戴者全部合法攻击（Q51：战斗饰品校准含空槽拳，不绑定某把武器）
                        if (e.AttackScoped && slot != pageSlot && !IsCharmSlot(slot)) continue;
                        list.Add(new ActiveEffect { Effect = e, Source = it, AffixId = id });
                    }
                }
            }
            return list;
        }

        private static bool IsCharmSlot(string slot) => slot == EquipSlots.CombatAccessory || slot == EquipSlots.SurvivalAccessory;

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
                    case "combat.armorIgnore": r.ArmorIgnore += v; break;
                    case "combat.damageMultiplier": r.DamageMultiplier += v; break;
                    case "combat.energyCost": if (op == "mul") r.EnergyCostFactor *= v; break;
                    case "combat.noise": r.Noise += v; break;
                }
            }
            // 位移抵抗（Q63）：固定来源按装备 ID 同名取强、词条按词条 ID 同名取强，不同来源相加后封顶 40（与主动同类取强、稳态另加在控制判定里）
            var disp = active.Where(x => x.Effect.Key == "combat.displaceResist").ToList();
            r.DisplaceResistRaw = disp.Where(x => x.AffixId == null).GroupBy(x => x.Source.ItemId).Sum(gp => gp.Max(x => x.Effect.Value))
                                + disp.Where(x => x.AffixId != null).GroupBy(x => x.AffixId).Sum(gp => gp.Max(x => x.Effect.Value));
            r.DisplaceResist = Math.Max(0, Math.Min(Config.ActorBaseDefense.DisplacementGearAggregateCap, r.DisplaceResistRaw));
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
                case ActionType.Repair:     // 维修、拆解按制造类工作（M120，待确认 Q52 选 A 的现状）
                case ActionType.Salvage:
                    yield return "work.craft";
                    break;
                case ActionType.Travel:
                    yield return "work.move";
                    yield return "work.route";      // 只能沿已发现的路线移动，所以每次移动都是“已发现路线”
                    break;
                case ActionType.Gather:
                    // 搜集背心等“搜集工作量”效果管练搜集的资源点（Q55：箱子与人工/工业资源，含废金属堆）；遗迹勘整不吃工作量折减
                    if (targetId != null && _res.TryGetValue(targetId, out var res) && GatherSkillOf(res) == Scavenging) yield return "work.scavenge";
                    break;
            }
        }

        /// <summary>
        /// 装备的求生工作量倍率（乘在基础工作量上，之后再乘 S01 工作倍率 M）：相关键相乘，下限 ×0.70；reasons 写“装备：耐磨工作衣×0.95”。
        /// </summary>
        public double GearWorkFactor(GameState s, string actorId, ActionType type, string? targetId, List<string>? reasons = null)
        {
            var keys = WorkKeys(type, targetId).ToList();
            var fx = keys.Count == 0 ? null : EquipmentEffects(s, actorId);
            var gear = fx == null ? 1 : keys.Aggregate(1.0, (acc, k) => acc * fx.Factor(k));
            // B5 求生被动与装备相乘后同受 ×0.70 下限（开发假设 H7）
            var (passive, source) = PassiveWorkFactor(s, s.Actor(actorId), type, targetId);
            var f = gear * passive;
            var floor = Config.Equipment.Limits.SurvivalWorkFloor;
            var floored = f < floor;
            if (floored) f = floor;
            if (Math.Abs(passive - 1) < 1e-12)
            {
                if (Math.Abs(f - 1) > 1e-12) reasons?.Add($"装备工作量×{Fmt.T(f)}");
                return f;
            }
            if (Math.Abs(gear - 1) > 1e-12) reasons?.Add($"装备工作量×{Fmt.T(gear)}");
            reasons?.Add($"熟练（{source}）工作量×{Fmt.T(passive)}");
            if (floored) reasons?.Add($"求生工作量合成下限×{Fmt.T(floor)}");
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
                    var cache = targetId != null && _res.TryGetValue(targetId, out var res) && GatherSkillOf(res) == Scavenging;
                    list.Add((StatId.Energy, cache ? "rate.scavengeClear.energy" : "rate.gather.energy"));
                    break;
                case ActionType.Survey: list.Add((StatId.Energy, "rate.scavengeClear.energy")); break;   // 正常搜集身体费率
            }
            return list;
        }

        private static readonly Dictionary<string, string> RateKeyNames = new Dictionary<string, string>
        {
            ["rate.gather.energy"] = "采集负体力", ["rate.move.energy"] = "移动负体力", ["rate.scavengeClear.energy"] = "搜集/清理负体力",
            ["rate.nonCombat.satiety"] = "非战斗负饱食", ["rate.nonCombat.hydration"] = "非战斗负水分",
        };

        /// <summary>预估里的装备速率说明：“装备：采集负体力×0.95，非战斗负水分×0.97”（每项按 0.75 下限合成）；没有为 null。</summary>
        public string? GearRateNote(GameState s, ActionType type, string? targetId)
        {
            var fx = EquipmentEffects(s);
            var floor = Config.Equipment.Limits.NegativeRateFloor;
            var parts = new List<string>();
            bool anyGear = false, anyPassive = false;
            foreach (var g in RateKeys(type, targetId).GroupBy(x => x.stat))
            {
                var gear = g.Aggregate(1.0, (acc, x) => acc * fx.Factor(x.key));
                var passive = g.Aggregate(1.0, (acc, x) => acc * PassiveFactor(s.Player, x.key));
                var f = gear * passive;
                if (f < floor) f = floor;
                if (Math.Abs(f - 1) < 1e-12) continue;
                anyGear |= Math.Abs(gear - 1) > 1e-12;
                anyPassive |= Math.Abs(passive - 1) > 1e-12;
                parts.Add($"{RateKeyNames[g.Last().key]}×{Fmt.T(f)}");
            }
            var who = anyGear && anyPassive ? "装备与熟练" : anyPassive ? "熟练" : "装备";
            return parts.Count == 0 ? null : $"{who}：{string.Join("，", parts)}（只缩减消耗，下限×{Fmt.T(floor)}）";
        }

        /// <summary>
        /// 把装备的求生速率效果作用到本段（只在非战斗的行动段，ComputeSegment 调用）：负小时率按键相乘、每项下限 ×0.75、只缩负值；
        /// 遮蔽休息/睡眠的正体力恢复加值（先加算，之后才受精神低于阈值折半）。
        /// </summary>
        private void ApplyGearRates(GameState s, ActorState actor, ActionState a, SegmentContext ctx)
        {
            var fx = EquipmentEffects(s, actor.Id);
            var anyPassive = SurvivalPassives(actor, true).Count > 0;
            if (fx.Factors.Count == 0 && fx.Adds.Count == 0 && !anyPassive) return;
            var floor = Config.Equipment.Limits.NegativeRateFloor;
            foreach (var g in RateKeys(a.Type, a.TargetId).GroupBy(x => x.stat))
            {
                // B5 求生被动（节奏行进/耐力行进）与装备相乘后同受 ×0.75 下限（开发假设 H7）
                var f = g.Aggregate(1.0, (acc, x) => acc * fx.Factor(x.key) * (anyPassive ? PassiveFactor(actor, x.key) : 1));
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
            return p * PassiveHazardFactor(s.Player, res);   // B5 稳手采集×0.9 / 危险辨识×0.8
        }

        public static bool IsWoodOrScrapGather(ResourceConfig res) =>
            res.Outputs.Count > 0 && res.Outputs.Keys.First() == "wood" || res.Outputs.ContainsKey("scrap");

        /// <summary>搜集划伤的直接健康损失 × 装备 hazard.health（环境划伤，不影响战斗）。</summary>
        public double HazardLoss(GameState s, ResourceConfig res) =>
            res.Hazard == null ? 0 : res.Hazard.HealthLoss * EquipmentEffects(s).Factor("hazard.health");

        /// <summary>工具维护恢复比例：配方 20% 与装备替代比例（维护手册 25%）取较高者，不相加（A05 §2、§5）。</summary>
        public double RepairFraction(GameState s, RecipeConfig rc) =>
            Math.Max(PassiveRepairFraction(s.Player, rc.RestoreOwnDurabilityMaxFraction), EquipmentEffects(s).Set("repair.fraction") ?? 0);

        /// <summary>非战斗食用的正饱食收益 × 装备 eat.satiety（配给袋 ×1.05）；负值与战斗中不变。</summary>
        public double UseSatiety(GameState s, ItemUseConfig u) =>
            u.Satiety > 0 && s.Battle == null ? u.Satiety * EquipmentEffects(s).Factor("eat.satiety") : u.Satiety;

        // ───────────── 界面文字（对象信息栏、人物页装备区；B4 H10） ─────────────

        /// <summary>
        /// 效果是否已接入现行规则：B6 起战斗类的速度、护甲、命中、暴击、忽略护甲、伤害乘数、招式体力、噪声在战斗中生效；
        /// 位移抵抗要等位移类技能（B7）；净水片配方未上线；其余求生效果生效。
        /// </summary>
        public static bool EffectLive(EquipEffectConfig e) => e.Key != "combat.displaceResist" && e.Key != "work.purificationTablet";

        /// <summary>“校准：命中能力+4（生效中）”；位移抵抗写“位移类技能上线后生效”；没穿写“穿戴后生效”（M120、B6）。</summary>
        public string AffixText(string id, bool worn = true, string? slot = null)
        {
            var a = Affix(id);
            if (a == null) return id;
            var text = a.EffectTextFor(slot);
            var live = a.Effects.Count > 0 && a.Effects.All(EffectLive);
            var note = a.Effects.Any(x => x.Key == "durability.max") ? "（生成时已计入最大耐久）"
                : live ? (worn ? "（生效中）" : "（穿戴后生效）") : "（我方多人时才会被拉扯/击退，当前只显示）";
            return $"{a.Name}：{text}{note}";
        }

        /// <summary>对象信息栏的装备行：品质、附加词条、类型与阶、速度修正、使用位→目标位、固定特性/效果、生效情况、拆解产出。非装备为空表。</summary>
        public List<(string Label, string Value)> EquipDetailLines(GameState s, ItemInstance it)
        {
            var list = new List<(string, string)>();
            var cfg = _items[it.ItemId];
            if (!cfg.IsEquipment) return list;
            var n = it.Affixes?.Count ?? 0;
            // Q51：饰品词条池上线前生成的绿/蓝饰品 0 条，保留历史实例、不补抽（新生成的绿/蓝饰品必有 1/2 条）
            var legacyCharm = n == 0 && IsCharmSlot(cfg.EquipSlot ?? "") && (it.Quality ?? "white") != "white";
            list.Add(("品质", legacyCharm ? $"{QualityName(it)}（旧实例，无附加词条：饰品词条池上线前生成，不补抽）" : $"{QualityName(it)}（附加词条{n}条）"));
            var worn = it.IsWorn;
            foreach (var id in it.Affixes ?? new List<string>()) list.Add(("附加词条", AffixText(id, worn, cfg.EquipSlot)));
            if (Weapon(it.ItemId) is WeaponConfig w)
            {
                list.Add(("类型", $"{WeaponKinds.Name(w.WeaponKind)}·T{w.Tier}{(w.Speed != 0 ? $" · 速度修正{Fmt.SignedT(w.Speed)}" : " · 速度修正0")}"));
                if (w.OwnPositions != null) list.Add(("站位", $"使用位{w.OwnPositions} → 目标位{w.TargetPositions}（我方只剩一人时任何武器都按1号位可用）"));
                if (w.TraitText != null) list.Add(("固定特性", $"{w.TraitText}{(w.ArmorIgnore > 0 ? "（生效中）" : "（相关技能或多人战斗上线后生效）")}"));
                if (!w.CanAttack) list.Add(("技能", $"{WeaponKinds.Name(w.WeaponKind)}不能直接攻击：穿进武器槽后，这一页只列{WeaponKinds.Name(w.WeaponKind)}技能（单人时以自己为目标）"));
                // B7a Q49：逐件显示技能归属（weaponSkillBindings：本门五节点，medical 器具→医疗熟练）
                var nodes = Config.SkillNodes.Where(x => x.SkillId == w.SkillId).OrderBy(x => x.Level).ToList();
                if (nodes.Count > 0)
                {
                    var lv = SkillLevel(s, w.SkillId);
                    list.Add(("技能归属", $"{SkillName(w.SkillId)}熟练（当前{lv}级，已解锁{nodes.Count(x => x.Level <= lv)}个）：{string.Join("、", nodes.Select(x => $"{x.Name}{x.Level}级"))}"));
                }
                // B7a Q45：攻击噪声按类型；隐声只算本把武器、最低0
                if (w.CanAttack)
                {
                    var noise = NoiseGain(w);
                    var quiet = (it.Affixes ?? new List<string>()).Select(Affix).Where(a => a != null)
                        .SelectMany(a => a!.Effects).Where(e => e.Key == "combat.noise").Sum(e => e.Value);
                    var kindText = w.WeaponKind == "firearms" || w.SkillId == "firearms" ? "枪械" : w.AmmoId == "bolt" ? "用弩矢的弩" : "弓与近战";
                    list.Add(("噪声", Math.Abs(quiet) > 1e-12
                        ? $"每次攻击+{Fmt.T(Math.Max(0, noise + quiet))}（{kindText}+{Fmt.T(noise)}，隐声{Fmt.SignedT(quiet)}，最低0；只算用这把攻击时）"
                        : $"每次攻击+{Fmt.T(noise)}（{kindText}）"));
                }
            }
            else if (Gear(it.ItemId) is GearConfig g)
            {
                list.Add(("类型", $"{EquipSlots.Name(g.Slot)}·T{g.Tier}"));
                var live = g.Effects.Where(EffectLive).ToList();
                var shown = g.Effects.Count > live.Count || (g.Effects.Count == 0 && g.UnarmedBasicTargets == null);
                if (g.UnarmedBasicTargets != null) live.Add(new EquipEffectConfig { Key = "combat.unarmedBasicTargets" });   // 护拳绑带：拳路扩展已接入
                var on = worn ? "生效中" : "穿戴后生效";
                list.Add(("固定效果", g.EffectText + (live.Count > 0 && !shown ? $"（{on}）" : live.Count > 0 ? $"（部分{on}；其余在相关技能上线后生效）" : "（相关技能上线后生效，当前只显示）")));
            }
            if (it.Durability.HasValue && it.Durability.Value <= 1e-9) list.Add(("状态", "耐久为0：效果暂停（仍占槽、计重），维护后恢复"));
            list.Add(("拆解", SalvageText(it)));
            return list;
        }

        /// <summary>角色自己的基础防御（Q63：属于角色、不是可转移物品；本批只有主角）。</summary>
        public string BaseDefenseLine(string actorId = GameState.PlayerId) =>
            $"基础防御：控制抗性{Fmt.T(Config.Scope.BasePlayerControlResist)}、闪避{Fmt.T(Config.Scope.BasePlayerDodge)}、护甲{Fmt.T(Config.Scope.BasePlayerArmor)}（装备“位移抵抗”只防拉扯/击退）";

        /// <summary>人物页装备区的效果汇总（一行一项）：战斗合计（只显示）、生效中的求生效果、暂停的装备。</summary>
        public List<string> EquipSummaryLines(GameState s, string actorId = GameState.PlayerId)
        {
            var fx = EquipmentEffects(s, actorId);
            var lim = Config.Equipment.Limits;
            var lines = new List<string>();
            var combat = new List<string>();
            if (fx.SpeedPositiveRaw != 0 || fx.SpeedNegative != 0)
                combat.Add($"速度{Fmt.SignedT(fx.Speed)}{(fx.SpeedPositiveRaw > lim.PositiveSpeedCap ? $"（正加成封顶+{Fmt.T(lim.PositiveSpeedCap)}）" : "")}");
            if (fx.ArmorRaw != 0) combat.Add($"护甲{Fmt.T(fx.Armor)}{(fx.ArmorRaw > lim.ArmorCap ? $"（封顶{Fmt.T(lim.ArmorCap)}）" : "")}");
            if (fx.Accuracy != 0) combat.Add($"命中能力{Fmt.SignedT(fx.Accuracy)}");
            if (fx.Crit != 0) combat.Add($"暴击能力{Fmt.SignedT(fx.Crit)}");
            if (fx.ArmorIgnore != 0) combat.Add($"忽略护甲{Fmt.T(fx.ArmorIgnore)}");
            if (fx.DamageMultiplier != 0) combat.Add($"直接伤害乘数{Fmt.SignedT(fx.DamageMultiplier)}");
            if (Math.Abs(fx.EnergyCostFactor - 1) > 1e-12) combat.Add($"招式E×{Fmt.T(fx.EnergyCostFactor)}");
            if (fx.DisplaceResist != 0) combat.Add($"位移抵抗+{Fmt.T(fx.DisplaceResist)}个百分点（只防拉扯/击退{(fx.DisplaceResistRaw > Config.ActorBaseDefense.DisplacementGearAggregateCap ? $"，合计封顶{Fmt.T(Config.ActorBaseDefense.DisplacementGearAggregateCap)}" : "")}）");
            if (fx.Noise != 0) combat.Add($"噪声{Fmt.SignedT(fx.Noise)}");
            if (actorId == GameState.PlayerId && WrapsPunchTargets(s) is string wt) combat.Add($"空槽徒手页的普通拳击可打敌方{PosList(wt.Where(char.IsDigit).Select(ch => ch - '0'))}");
            if (combat.Count > 0) lines.Add($"战斗（全身装备合计；攻击类词条只算当前武器页）：{string.Join("，", combat)}{(fx.DisplaceResist != 0 ? "——位移抵抗在我方多人时才有用（只剩你一人不会被移动），其余战斗中生效" : "——战斗中生效")}");
            var names = new Dictionary<string, string>
            {
                ["work.craft"] = "制造/建造工作量", ["work.scavenge"] = "搜集工作量", ["work.move"] = "移动工作量", ["work.route"] = "已发现路线移动工作量",
                ["rate.gather.energy"] = "采集负体力", ["rate.move.energy"] = "移动负体力", ["rate.scavengeClear.energy"] = "搜集/清理负体力",
                ["rate.nonCombat.satiety"] = "非战斗负饱食", ["rate.nonCombat.hydration"] = "非战斗负水分",
                ["hazard.health"] = "划伤健康损失", ["hazard.woodScrapChance"] = "木材/废料划伤概率", ["eat.satiety"] = "非战斗食用正饱食",
            };
            var survival = new List<string>();
            foreach (var kv in fx.Factors.Where(k => names.ContainsKey(k.Key) && Math.Abs(k.Value - 1) > 1e-12)) survival.Add($"{names[kv.Key]}×{Fmt.T(kv.Value)}");
            if (fx.Add("pos.shelteredRest.energy") != 0) survival.Add($"遮蔽休息体力恢复+{Fmt.T(fx.Add("pos.shelteredRest.energy"))}/小时");
            if (fx.Add("pos.sleep.energy") != 0) survival.Add($"睡眠体力恢复+{Fmt.T(fx.Add("pos.sleep.energy"))}/小时");
            if (fx.Add("bandage.health") != 0) survival.Add($"非战斗包扎回血+{Fmt.T(fx.Add("bandage.health"))}");
            if (fx.Set("repair.fraction") is double rf) survival.Add($"工具维护恢复{Fmt.Pct(rf)}（替代，不相加）");
            if (survival.Count > 0) lines.Add($"求生（生效中，工作量合成下限×{Fmt.T(lim.SurvivalWorkFloor)}、负小时率下限×{Fmt.T(lim.NegativeRateFloor)}）：{string.Join("，", survival)}");
            if (fx.Paused.Count > 0) lines.Add($"耐久为0、效果暂停：{string.Join("、", fx.Paused)}");
            return lines;
        }
    }
}
