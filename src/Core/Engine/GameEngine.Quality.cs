using System;
using System.Collections.Generic;
using System.Linq;
using Survival.Core.Config;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>
    /// 装备实例的品质与附加词条（A05 §4、R02 §2/§3；开发计划 v0.9 B4 H4、H5）：
    /// 品质按来源抽一次（只有 A05 写明概率的来源：新手拾荒、主线拾荒、副本首领、普通制造默认绿；其余来源白色）；
    /// 白 0 条、绿 1 条、蓝 2 条，按兼容池等权抽、不重复 ID、同互斥组最多 1 条，池不足时少条；生成后保存在实例里，读档、维修、穿脱都不重抽。
    /// </summary>
    public sealed partial class GameEngine
    {
        /// <summary>来源代号（equipment.qualityOrigins 的键）。</summary>
        public static class Origins
        {
            public const string NoviceScavenge = "novice_scavenge";
            public const string MainlineScavenge = "mainline_scavenge";
            public const string DungeonBoss = "dungeon_boss";
            public const string Craft = "craft";
        }

        public AffixConfig? Affix(string id) => Config.Affixes.FirstOrDefault(a => a.Id == id);

        public GearConfig? Gear(string id) => Config.Gear.FirstOrDefault(g => g.Id == id);

        /// <summary>品质中文名（白/绿/蓝）；非装备为空串，装备品质为空按白。</summary>
        public string QualityName(ItemInstance it) =>
            _items.TryGetValue(it.ItemId, out var c) && c.IsEquipment ? Config.Equipment.QualityName(it.Quality ?? "white") : "";

        /// <summary>该装备能抽到的词条（按配置顺序；兼容池 kind:/slot:/durable）。</summary>
        public List<AffixConfig> AffixPool(string itemId)
        {
            var cfg = _items[itemId];
            var kind = Weapon(itemId)?.WeaponKind;
            return Config.Affixes.Where(a => a.Pools.Any(p =>
                p == "durable" ? cfg.HasDurability
                : p.StartsWith("kind:") ? kind != null && p.Substring(5) == kind
                : p.StartsWith("slot:") && cfg.EquipSlot == p.Substring(5))).ToList();
        }

        /// <summary>按来源概率抽品质；来源没有写概率（或为空）→ 白色，不消耗随机数；只有一种结果时也不消耗随机数。</summary>
        public string RollQuality(GameState s, string? origin)
        {
            var eq = Config.Equipment;
            if (origin == null || !eq.QualityOrigins.TryGetValue(origin, out var table) || table.Count == 0) return "white";
            var ordered = eq.QualityOrder.Where(table.ContainsKey).ToList();
            if (ordered.Count == 1) return ordered[0];
            var r = s.Rng.NextDouble();
            double acc = 0;
            foreach (var q in ordered)
            {
                acc += table[q];
                if (r < acc) return q;
            }
            return ordered[ordered.Count - 1];
        }

        /// <summary>抽 n 条词条：每次从“未抽过、互斥组未占用”的兼容词条里等权抽 1 条，池空即止（少条，不跨池）。</summary>
        public List<string> RollAffixes(GameState s, string itemId, int n)
        {
            var picked = new List<AffixConfig>();
            var pool = AffixPool(itemId);
            for (var k = 0; k < n; k++)
            {
                var cand = pool.Where(a => picked.All(p => p.Id != a.Id && p.Group != a.Group)).ToList();
                if (cand.Count == 0) break;
                var i = (int)Math.Floor(s.Rng.NextDouble() * cand.Count);
                if (i >= cand.Count) i = cand.Count - 1;
                picked.Add(cand[i]);
            }
            return picked.Select(a => a.Id).ToList();
        }

        /// <summary>
        /// 给一件新生成的装备定品质与词条，并按 durable 词条调整最大耐久（开发假设 H5：先抽的最大耐久 × 1.15，按 0.1 四舍五入；
        /// 当前耐久 = 新最大 × 已抽的成色）。forcedQuality 只给开发入口用（直接指定品质，词条照常抽）。
        /// </summary>
        private void ApplyQuality(GameState s, ItemInstance inst, ItemConfig cfg, string? origin, double condition, string? forcedQuality = null)
        {
            if (!cfg.IsEquipment) return;
            var q = forcedQuality ?? RollQuality(s, origin);
            inst.Quality = q;
            var n = Config.Equipment.AffixCounts.TryGetValue(q, out var c) ? c : 0;
            var affixes = n > 0 ? RollAffixes(s, inst.ItemId, n) : new List<string>();
            inst.Affixes = affixes.Count > 0 ? affixes : null;
            if (inst.MaxDurability is double max)
            {
                var mul = affixes.Select(Affix).Where(a => a != null)
                    .SelectMany(a => a!.Effects).Where(e => e.Key == "durability.max" && e.Op == "mul").Aggregate(1.0, (f, e) => f * e.Value);
                if (Math.Abs(mul - 1) > 1e-12)
                {
                    var step = Config.Equipment.DurabilityRounding;
                    max = Math.Round(max * mul / step, MidpointRounding.AwayFromZero) * step;
                    max = Math.Round(max, 6);
                    inst.MaxDurability = max;
                    inst.Durability = max * condition;
                }
            }
        }

        /// <summary>生成提示里的品质与词条：“（绿·校准）”；非装备为空串。</summary>
        public string QualitySuffix(IEnumerable<ItemInstance> made)
        {
            var parts = made.Where(i => _items[i.ItemId].IsEquipment).Select(i =>
                QualityName(i) + (i.Affixes is { Count: > 0 } a ? "·" + string.Join("、", a.Select(id => Affix(id)?.Name ?? id)) : "")).ToList();
            return parts.Count == 0 ? "" : $"（{string.Join("；", parts)}）";
        }

        /// <summary>开发入口：生成一件指定品质的装备（词条按兼容池照常抽；新制品满耐久）。</summary>
        public ItemInstance DebugCreateEquipment(GameState s, string itemId, string quality, string owner) =>
            NewSingle(s, itemId, owner, false, null, null, quality);
    }
}
