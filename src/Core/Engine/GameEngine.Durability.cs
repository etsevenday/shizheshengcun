using System;
using System.Collections.Generic;
using System.Linq;
using Survival.Core.Config;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>
    /// 实例随机耐久、磨损与工具维护（v0.6 §05）：武器、石刀、斧、打火器的最大耐久（v1.0 P02：金属锅没有耐久）在实例生成时按 0.1 精度均匀抽一次并保存；
    /// 新制品当前=最大，拾荒箱子产出另乘 0.6—1.0 成色；动作开始前须有足够耐久支付本次磨损；维护恢复自身最大耐久的 20%，不重抽。
    /// </summary>
    public sealed partial class GameEngine
    {
        public double LighterWear => Config.Durability.LighterWear;
        public double GatherWear => Config.ToolEffects.SuccessfulActionWear;

        /// <summary>按配置区间、0.1 精度均匀抽一次最大耐久（两端都可能抽到）。</summary>
        public double RollMaxDurability(GameState s, ItemConfig cfg)
        {
            var r = cfg.DurabilityMaxRange!;
            var lo = (long)Math.Round(r[0] * 10);
            var hi = (long)Math.Round(r[1] * 10);
            var k = (long)Math.Floor(s.Rng.NextDouble() * (hi - lo + 1));
            if (k > hi - lo) k = hi - lo;
            return (lo + k) / 10.0;
        }

        /// <summary>拾荒成色（v0.6 scavengedConditionOptional 0.6—1.0），连续均匀抽取，生成时一次。</summary>
        public double RollCondition(GameState s)
        {
            var c = Config.Durability.ScavengedConditionOptional;
            if (c.Length != 2) return 1;
            return c[0] + s.Rng.NextDouble() * (c[1] - c[0]);
        }

        /// <summary>有没有足够耐久支付一次磨损（无耐久物品恒为真）。比较容差 1e-8。</summary>
        public static bool HasWear(ItemInstance i, double wear) => i.Durability == null || i.Durability.Value >= wear - 1e-8;

        /// <summary>成色比例 当前/最大；无耐久为 1。</summary>
        public static double Condition(ItemInstance i) =>
            i.Durability.HasValue && i.MaxDurability is double m && m > 0 ? Math.Max(0, i.Durability.Value / m) : 1;

        /// <summary>扣耐久（不低于 0），返回“石刀耐久12.30→11.30”。</summary>
        private string Wear(ItemInstance i, double wear)
        {
            if (i.Durability == null || wear <= 0) return "";
            var before = i.Durability.Value;
            i.Durability = Math.Max(0, before - wear);
            return $"{ItemName(i.ItemId)}耐久{Fmt.N(before)}→{Fmt.N(i.Durability.Value)}";
        }

        /// <summary>“12.30/24.00”；无耐久为空串。</summary>
        public static string DurabilityText(ItemInstance i) =>
            i.Durability.HasValue && i.MaxDurability.HasValue ? $"{Fmt.N(i.Durability.Value)}/{Fmt.N(i.MaxDurability.Value)}" : "";

        /// <summary>病毒特效药的锅：须明确指定、随身或当前地面（不在火堆）、已清空、未被占用。</summary>
        public string? ExplicitPotBlocked(GameState s, int? potId, out ItemInstance? pot)
        {
            pot = potId.HasValue ? FindItem(s, potId.Value) : null;
            if (!potId.HasValue) return "请指定一口取下并清空的锅";
            if (pot == null || !IsPot(pot)) return "指定的锅已不在，请重新选择";
            if (!(pot.IsCarried || pot.Owner == s.CurrentLocation)) return pot.InFacility ? "先把锅从火堆取下" : "只能用随身或当前地面的锅";
            if (PotContents(s, pot.InstanceId).Count > 0) return "锅须先清空（倒出水、取出食材）";
            if (pot.ReservedBy != null || IsLocked(s, pot)) return "行动占用：这口锅正被使用";
            return null;
        }

        // ───────────── 工具维护（v0.6 §05 repair_selected_tool） ─────────────

        public RecipeConfig? RepairRecipe => Config.Recipes.FirstOrDefault(r => r.IsRepair);

        /// <summary>可维护的目标：随身或当前地面（不在火堆、不在锅里）、有耐久且未满、未被预留或锁定。</summary>
        public List<ItemInstance> RepairCandidates(GameState s) =>
            s.Items.Where(i => i.Durability.HasValue && i.MaxDurability.HasValue && (OnPlayer(i) || i.Owner == s.CurrentLocation))
                .OrderBy(i => OnPlayer(i) ? 0 : 1).ThenBy(i => Condition(i)).ThenBy(i => i.InstanceId).ToList();

        /// <summary>维护目标是否合法；合法时返回 null 并给出实例。</summary>
        public string? RepairBlocked(GameState s, int? instanceId, out ItemInstance? target)
        {
            target = null;
            if (!instanceId.HasValue) return "请先选择要维护的工具";
            target = FindItem(s, instanceId.Value);
            if (target == null) return "这件工具已不在，请重新选择";
            var cfg = _items[target.ItemId];
            if (!target.Durability.HasValue || !target.MaxDurability.HasValue) return $"{cfg.Name}没有耐久，不需要维护";
            if (!(OnPlayer(target) || target.Owner == s.CurrentLocation))
                return target.InFacility ? $"先把{cfg.Name}从火堆取下" : "只能维护随身或当前地面的工具";
            if (target.ReservedBy != null || IsLocked(s, target)) return "行动占用：这件工具正被使用";
            if (target.Durability.Value >= target.MaxDurability.Value - 1e-9) return $"{cfg.Name}#{target.InstanceId}耐久已满";
            if (IsPot(target) && PotContents(s, target.InstanceId).Count > 0 && RepairRecipe?.RequireEmptyDetachedPotWhenTargetIsPot == true)
                return "锅须先取下并清空才能维护";
            return null;
        }

        // ───────────── 拆解（A05 §5，B4 H9；B7a Q46 72 件拆解表与 80% 材料价值预算） ─────────────

        public RecipeConfig? SalvageRecipe => Config.Recipes.FirstOrDefault(r => r.IsSalvage);

        /// <summary>这件装备的拆解表行（Q46）；表里没有的装备为 null（拒绝拆解）。</summary>
        public SalvageOutputConfig? SalvageDef(string itemId) => Weapon(itemId)?.Salvage ?? Gear(itemId)?.Salvage;

        /// <summary>基础产出：拆解表回收材料 × 本实例品质的白/绿/蓝基础数量（Q46；品质不是价值倍增器，可能同产出）。表里没有为 null。</summary>
        public (string itemId, int count)? SalvageOutput(ItemInstance it)
        {
            var def = SalvageDef(it.ItemId);
            if (def == null) return null;
            return def.Counts.TryGetValue(it.Quality ?? "white", out var n) ? (def.ItemId, n) : ((string, int)?)null;
        }

        /// <summary>材料的拆解预算价值（Q46 materialValues；只是预算单位，不是交易报价）。</summary>
        public int SalvageValue(string itemId) => Config.Equipment.Salvage.MaterialValues.TryGetValue(itemId, out var v) ? v : 0;

        /// <summary>
        /// 回收专家追加（Q46 取代 B5“固定多返废金属1”）：基础产出后尝试追加同表回收材料，最多 ExpertMax 份；
        /// 可自制件只在总回收价值 R 仍满足 5R≤4I 时追加（否则 0，不向下拆分原料）；不可自制件按固定表追加。
        /// 返回追加份数与说明（没有回收专家时为 (0, null)）。回收专家在制造10级（Q54，取代搜集10级）。
        /// </summary>
        public (int extra, string? note) SalvageExpertBonus(GameState s, ItemInstance it)
        {
            var lv = SalvageExpertLevel(s);
            var def = SalvageDef(it.ItemId);
            if (lv <= 0 || def == null || SalvageOutput(it) is not (string oid, int n)) return (0, null);
            var max = Math.Min(lv, def.ExpertMax);
            var mat = ItemName(def.ExpertMaterial);
            if (def.NonCraftable || def.RecipeValue is not int i)
                return (max, $"{SalvageExpertSource}：追加{mat}×{max}（不可自制装备按固定表）");
            var rules = Config.Equipment.Salvage;
            var baseValue = n * SalvageValue(oid);
            var v = SalvageValue(def.ExpertMaterial);
            var extra = 0;
            while (extra < max && (baseValue + (extra + 1) * v) * rules.CapDenominator <= i * rules.CapNumerator) extra++;
            var cap = $"标准配方材料价值{i}的{Fmt.Pct((double)rules.CapNumerator / rules.CapDenominator)}";
            return extra > 0
                ? (extra, $"{SalvageExpertSource}：追加{mat}×{extra}（回收价值{baseValue + extra * v}，不超过{cap}）")
                : (0, $"{SalvageExpertSource}：再追加{mat}会使回收价值{baseValue + v}超过{cap}，不追加");
        }

        /// <summary>拆解总产出（基础 + 回收专家追加，同材料合并），预估与完成同源。</summary>
        public List<(string itemId, int count)> SalvageYield(GameState s, ItemInstance it)
        {
            var list = new List<(string itemId, int count)>();
            if (SalvageOutput(it) is not (string oid, int n)) return list;
            list.Add((oid, n));
            var (extra, _) = SalvageExpertBonus(s, it);
            if (extra > 0)
            {
                var mat = SalvageDef(it.ItemId)!.ExpertMaterial;
                var k = list.FindIndex(x => x.itemId == mat);
                if (k >= 0) list[k] = (mat, list[k].count + extra);
                else list.Add((mat, extra));
            }
            return list;
        }

        /// <summary>对象信息栏/卡下的拆解说明：“废金属×3（15基础分钟，产物落当前地面；不可自制：按固定表）”。</summary>
        public string SalvageText(ItemInstance it)
        {
            var def = SalvageDef(it.ItemId);
            if (SalvageOutput(it) is not (string oid, int cnt) || def == null) return $"{ItemName(it.ItemId)}没有拆解表，暂不能拆解";
            var rules = Config.Equipment.Salvage;
            var budget = def.RecipeValue is int i
                ? $"标准配方材料价值{i}，回收价值不超过{Fmt.Pct((double)rules.CapNumerator / rules.CapDenominator)}"
                : "不可自制：按固定表";
            return $"{ItemName(oid)}×{cnt}（{SalvageRecipe?.BaseMinutes ?? 0}基础分钟，产物落当前地面；{budget}）";
        }

        /// <summary>
        /// 能否拆解（只读）：须是本人随身或当前地面的装备、未穿戴、未被预留或行动占用、在拆解表里；弹仓里还有弹药的先打空（不吞弹药）。
        /// 耐久 0 仍可拆。战斗、敌踪待选、其他行动进行中由通用开始校验拦截。
        /// Q54：拆解归制造，制造 1/3/7/10 级开放 T0/T1/T2/T3（按装备表的阶，不按名称、品质；没有阶的拒绝），不看搜集等级。
        /// </summary>
        public string? SalvageBlocked(GameState s, int? instanceId, out ItemInstance? target)
        {
            target = null;
            if (!instanceId.HasValue) return "请先选择要拆解的装备";
            target = FindItem(s, instanceId.Value);
            if (target == null) return "这件装备已不在，请重新选择";
            var cfg = _items[target.ItemId];
            if (!cfg.IsEquipment) return $"{cfg.Name}不是装备，不能拆解";
            if (target.LoanSlot != null) return $"借用的{cfg.Name}不能拆解（RB01 借用物，不产出材料）";
            if (target.IsWorn) return target.IsWornBy(GameState.PlayerId) ? "穿戴中：先在装备区卸下再拆解" : "穿在别人身上，不能拆解";
            if (!(target.IsCarried || target.Owner == s.CurrentLocation)) return "只能拆解随身或当前地面的装备";
            if (target.ReservedBy != null || IsLocked(s, target)) return "行动占用：这件装备正被使用";
            if (target.LoadedAmmo > 0) return $"弹仓里还有{target.LoadedAmmo}发{ItemName(target.LoadedAmmoId ?? "")}，打空后再拆解";
            if (SalvageOutput(target) == null) return $"{cfg.Name}没有拆解表，暂不能拆解";
            var tier = EquipTier(target.ItemId);
            if (tier == null) return $"{cfg.Name}没有装备阶级，不能拆解";
            var need = SalvageCraftingLevel(tier.Value);
            if (need == null) return $"{cfg.Name}（{tier}阶）暂无可拆解的制造等级";
            var lv = SkillLevel(s, Crafting);
            if (lv < need) return $"拆解{tier}阶装备需要{SkillName(Crafting)}{need}级（当前{lv}级）";
            return null;
        }

        /// <summary>装备的阶 T0—T3（武器表 tier、防具/饰品/衣物表 tier）；不是装备或没写为 null。</summary>
        public int? EquipTier(string itemId) => Weapon(itemId)?.Tier ?? Gear(itemId)?.Tier;

        /// <summary>拆解该阶装备所需的制造等级（Q54：制造1/3/7/10 开放 T0/T1/T2/T3）；没有对应档为 null。</summary>
        public int? SalvageCraftingLevel(int tier) =>
            Config.Progression.Dismantle.Stages.Where(x => x.MaxTier >= tier).OrderBy(x => x.CraftingLevel).Select(x => (int?)x.CraftingLevel).FirstOrDefault();

        /// <summary>某制造等级可拆的最高阶（Q54 常驻被动，不是第六个节点）；一档都没开为 null。</summary>
        public int? SalvageMaxTier(int craftingLevel) =>
            Config.Progression.Dismantle.Stages.Where(x => craftingLevel >= x.CraftingLevel).Select(x => (int?)x.MaxTier).DefaultIfEmpty(null).Max();

        /// <summary>
        /// 拆解完成：原装备原子移除，产物（基础+回收专家）落当前地面，同一事务给制造经验 +2（Q54，取代 Q46 不给经验）；
        /// 目标已不在则不产出、不给经验、不改其他状态。取消与重复请求本来就不走这里。
        /// </summary>
        private bool CompleteSalvage(GameState s, ActionState a)
        {
            var here = s.CurrentLocation;
            var t = a.ToolInstanceId.HasValue ? FindItem(s, a.ToolInstanceId.Value) : null;
            var yield = t != null ? SalvageYield(s, t) : new List<(string itemId, int count)>();
            var note = t != null ? SalvageExpertBonus(s, t).note : null;
            ClearAction(s, a);
            if (t == null || yield.Count == 0)
            {
                Emit(s, GameEventKind.ActionCompleted, "要拆解的装备已不在，没有产出。");
                return true;
            }
            var name = $"{ItemName(t.ItemId)}#{t.InstanceId}（{QualityName(t)}）";
            s.Items.Remove(t);
            foreach (var (oid, n) in yield) CreateItems(s, oid, n, here);
            // 语序（B7b 试玩修复）：产物 → 放置地点 → 回收专家说明 → 经验
            var got = string.Join("，", yield.Select(x => $"{ItemName(x.itemId)}+{x.count}")) + $"，已放在{LocName(here)}地面";
            if (note != null) got += $"（{note}）";
            var xp = Config.Progression.Dismantle.Xp;
            if (xp > 0)
            {
                got += "；" + XpText(s, Crafting, xp);
                GainXp(s, Crafting, xp);
            }
            Emit(s, GameEventKind.ActionCompleted, $"拆解{name}：{got}。", null, yield.Select(x => x.itemId).ToArray());
            UpdateFlags(s);
            return true;
        }

        /// <summary>维护完成：恢复自身最大耐久 20%，不超过自身最大、不重抽最大值。</summary>
        private string CompleteRepair(GameState s, ActionState a)
        {
            var rc = _recipes[a.TargetId];
            var t = a.ToolInstanceId.HasValue ? FindItem(s, a.ToolInstanceId.Value) : null;
            if (t?.Durability == null || t.MaxDurability == null) return "目标工具已不在，未恢复耐久";
            var before = t.Durability.Value;
            t.Durability = Math.Min(t.MaxDurability.Value, before + t.MaxDurability.Value * RepairFraction(s, rc));
            return $"{ItemName(t.ItemId)}#{t.InstanceId}耐久{Fmt.N(before)}→{Fmt.N(t.Durability.Value)}/{Fmt.N(t.MaxDurability.Value)}";
        }
    }
}
