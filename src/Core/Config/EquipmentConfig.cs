using System.Collections.Generic;
using Newtonsoft.Json;

namespace Survival.Core.Config
{
    /// <summary>
    /// 一条结构化装备效果（开发计划 v0.9 B4 H3）：数值取自 A05 §2 固定效果、§4 词条效果原文；原文另存 EffectText/TraitText 全文显示。
    /// Key 前缀：combat.*（B6 四对四战斗内核之前只聚合显示）、work.*（基础工作量倍率）、rate.*（负小时率倍率）、pos.*（正恢复加值）、
    /// hazard.*（划伤）、repair.fraction（维护比例，替代不相加）、bandage.health、eat.satiety、durability.max（生成时最大耐久倍率）。
    /// </summary>
    public sealed class EquipEffectConfig
    {
        public string Key { get; set; } = "";
        /// <summary>add 加值；mul 乘数；set 替代值。</summary>
        public string Op { get; set; } = "add";
        public double Value { get; set; }
        /// <summary>attack：只作用于本次所用武器（A05 §5“攻击附加不跨武器借用”）。</summary>
        public string? Scope { get; set; }

        [JsonIgnore] public bool AttackScoped => Scope == "attack";
    }

    /// <summary>
    /// 拆解表一行（A05「2026-10-31 Q44—Q52装备裁定」§拆解价值与完整产出，Q46；数据 equipment-adjudications.json dismantle.rows）：
    /// 回收材料 ItemId、白/绿/蓝基础数量 Counts；可自制件 RecipeValue 为标准配方材料价值 I（回收价值 R 须 5R≤4I），不可自制件为 null 并按固定表；
    /// 回收专家最多追加 ExpertMax 份 ExpertMaterial（可自制件仍受同一预算）。
    /// </summary>
    public sealed class SalvageOutputConfig
    {
        public string ItemId { get; set; } = "";
        public Dictionary<string, int> Counts { get; set; } = new Dictionary<string, int>();
        public int? RecipeValue { get; set; }
        public bool NonCraftable { get; set; }
        public string ExpertMaterial { get; set; } = "";
        public int ExpertMax { get; set; }
    }

    /// <summary>防具、战斗饰品、上衣、下衣、求生饰品（A05 §2，34 件）。Slot 写代码槽名（combat_acc/survival_acc 对应 A05 combat_charm/survival_charm）。</summary>
    public sealed class GearConfig
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Slot { get; set; } = "";
        public int Tier { get; set; }
        public int WeightGrams { get; set; }
        public double[]? DurabilityMaxRange { get; set; }
        public string EffectText { get; set; } = "";
        public List<EquipEffectConfig> Effects { get; set; } = new List<EquipEffectConfig>();
        /// <summary>拆解表没有这一行的装备为 null：拒绝拆解（Q46 unknownNewItem）。</summary>
        public SalvageOutputConfig? Salvage { get; set; }
    }

    /// <summary>18 条附加词条（A05 §4）。Pools：kind:&lt;武器类型&gt;、slot:&lt;代码槽&gt;（含 Q51 的 slot:combat_acc / slot:survival_acc 饰品池）、durable（有耐久的装备）。</summary>
    public sealed class AffixConfig
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string PoolText { get; set; } = "";
        public List<string> Pools { get; set; } = new List<string>();
        public string EffectText { get; set; } = "";
        public string Group { get; set; } = "";
        /// <summary>饰品专用互斥组（Q51）：代码槽名 → 组；不在表里的槽用 Group（原衣物 survival 组不变）。</summary>
        public Dictionary<string, string>? SlotGroups { get; set; }
        public List<EquipEffectConfig> Effects { get; set; } = new List<EquipEffectConfig>();

        public string GroupFor(string? slot) => slot != null && SlotGroups != null && SlotGroups.TryGetValue(slot, out var g) ? g : Group;
    }

    /// <summary>装备效果合成上限（A05 §4 末段、R01 §4）。</summary>
    public sealed class EquipLimitsConfig
    {
        public double PositiveSpeedCap { get; set; } = 3;
        public double ArmorCap { get; set; } = 0.9;
        public double SurvivalWorkFloor { get; set; } = 0.7;
        public double NegativeRateFloor { get; set; } = 0.75;
    }

    /// <summary>拆解规则（Q46）：材料价值只是拆解预算单位，不是交易报价；总回收价值×CapDenominator ≤ 标准配方材料价值×CapNumerator（5R≤4I）。</summary>
    public sealed class SalvageRulesConfig
    {
        public string RecipeId { get; set; } = "";
        public Dictionary<string, int> MaterialValues { get; set; } = new Dictionary<string, int>();
        public int CapNumerator { get; set; } = 4;
        public int CapDenominator { get; set; } = 5;
    }

    /// <summary>装备品质与生成规则（A05 §4 正文；R02 §2、§3）。</summary>
    public sealed class EquipmentRulesConfig
    {
        public List<string> QualityOrder { get; set; } = new List<string>();
        public Dictionary<string, string> QualityNames { get; set; } = new Dictionary<string, string>();
        public Dictionary<string, int> AffixCounts { get; set; } = new Dictionary<string, int>();
        /// <summary>有写明概率的来源 → 品质概率（A05 §4：新手拾荒、主线拾荒、副本首领、普通制造默认绿）。未列出的来源一律白色（开发假设 H4）。</summary>
        public Dictionary<string, Dictionary<string, double>> QualityOrigins { get; set; } = new Dictionary<string, Dictionary<string, double>>();
        public Dictionary<string, string> OriginNames { get; set; } = new Dictionary<string, string>();
        /// <summary>现有拾荒箱（资源点 scavenged）按哪种来源抽品质（开发假设 H4：新手普通拾荒）。</summary>
        public string ScavengedResourceOrigin { get; set; } = "";
        public string CraftOrigin { get; set; } = "";
        /// <summary>durable 词条乘倍后最大耐久取整精度（开发假设 H5：0.1，四舍五入）。</summary>
        public double DurabilityRounding { get; set; } = 0.1;
        public EquipLimitsConfig Limits { get; set; } = new EquipLimitsConfig();
        public SalvageRulesConfig Salvage { get; set; } = new SalvageRulesConfig();
        /// <summary>防具每次受到正直接伤害磨损（A05 §2）。</summary>
        public double ArmorWearPerHit { get; set; } = 1;

        public string QualityName(string? q) => QualityNames.TryGetValue(q ?? "white", out var n) ? n : q ?? "";
    }
}
