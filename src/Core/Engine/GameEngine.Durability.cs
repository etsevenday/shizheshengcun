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
            s.Items.Where(i => i.Durability.HasValue && i.MaxDurability.HasValue && (i.IsCarried || i.Owner == s.CurrentLocation))
                .OrderBy(i => i.IsCarried ? 0 : 1).ThenBy(i => Condition(i)).ThenBy(i => i.InstanceId).ToList();

        /// <summary>维护目标是否合法；合法时返回 null 并给出实例。</summary>
        public string? RepairBlocked(GameState s, int? instanceId, out ItemInstance? target)
        {
            target = null;
            if (!instanceId.HasValue) return "请先选择要维护的工具";
            target = FindItem(s, instanceId.Value);
            if (target == null) return "这件工具已不在，请重新选择";
            var cfg = _items[target.ItemId];
            if (!target.Durability.HasValue || !target.MaxDurability.HasValue) return $"{cfg.Name}没有耐久，不需要维护";
            if (!(target.IsCarried || target.Owner == s.CurrentLocation))
                return target.InFacility ? $"先把{cfg.Name}从火堆取下" : "只能维护随身或当前地面的工具";
            if (target.ReservedBy != null || IsLocked(s, target)) return "行动占用：这件工具正被使用";
            if (target.Durability.Value >= target.MaxDurability.Value - 1e-9) return $"{cfg.Name}#{target.InstanceId}耐久已满";
            if (IsPot(target) && PotContents(s, target.InstanceId).Count > 0 && RepairRecipe?.RequireEmptyDetachedPotWhenTargetIsPot == true)
                return "锅须先取下并清空才能维护";
            return null;
        }

        /// <summary>维护完成：恢复自身最大耐久 20%，不超过自身最大、不重抽最大值。</summary>
        private string CompleteRepair(GameState s, ActionState a)
        {
            var rc = _recipes[a.TargetId];
            var t = a.ToolInstanceId.HasValue ? FindItem(s, a.ToolInstanceId.Value) : null;
            if (t?.Durability == null || t.MaxDurability == null) return "目标工具已不在，未恢复耐久";
            var before = t.Durability.Value;
            t.Durability = Math.Min(t.MaxDurability.Value, before + t.MaxDurability.Value * rc.RestoreOwnDurabilityMaxFraction);
            return $"{ItemName(t.ItemId)}#{t.InstanceId}耐久{Fmt.N(before)}→{Fmt.N(t.Durability.Value)}/{Fmt.N(t.MaxDurability.Value)}";
        }
    }
}
