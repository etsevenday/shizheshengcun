using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace Survival.Core.Config
{
    // 人物与战斗修订参考 v0.6（基线 v0.5）各段的强类型映射。字段名与策划 JSON 一致，数值原样拷贝；
    // 公式字符串的系数用 Formula 按位置读取（形状由 ConfigLoader 校验）。

    /// <summary>v0.6 stateScale：健康/体力上限 20 与显示精度（§02）。</summary>
    public sealed class StateScaleConfig
    {
        public double HealthMax { get; set; } = 20;
        public double InitialHealth { get; set; } = 20;
        public double EnergyMax { get; set; } = 20;
        public double InitialEnergy { get; set; } = 18;
        public double SatietyMax { get; set; } = 100;
        public double HydrationMax { get; set; } = 100;
        public double WakefulnessMax { get; set; } = 100;
        public double MoraleMax { get; set; } = 100;
        public double InitialSatiety { get; set; }
        public double InitialHydration { get; set; }
        public double InitialWakefulness { get; set; }
        public double InitialMorale { get; set; }
        /// <summary>旧档 H/E 一次性缩放系数 0.2。</summary>
        public double OldHealthAndEnergyConversionFactor { get; set; } = 0.2;
        public double HealthCritical { get; set; }
        public double LowEnergyExclusive { get; set; }
        public string InternalPrecision { get; set; } = "";
        public int DisplayDecimals { get; set; } = 2;
        public string DisplayTinyPositive { get; set; } = "<0.01";
        public bool DoNotRoundPerTick { get; set; } = true;
    }

    /// <summary>v0.6 scope：单人、至多 3 敌、玩家速度/闪避/护甲。</summary>
    public sealed class CombatScopeConfig
    {
        public int PlayerCount { get; set; } = 1;
        public int MaxEnemies { get; set; } = 3;
        /// <summary>每侧站位 "1"—"4"（R03 §1 四对四）。</summary>
        public List<string> Slots { get; set; } = new List<string>();
        /// <summary>
        /// 队伍容量 C 的解锁规则（Q62 选 B、R03 2026-10-31 Q62—Q74 章）：新局及没有真实解锁事实时 C1；按存档里的真实事实（Progress.Flags）取已满足档的最大值，只增不减。
        /// 取代 B6 开发假设 B6-1 的临时 defaultCapacity=3。
        /// </summary>
        public CapacityRulesConfig Capacity { get; set; } = new CapacityRulesConfig();
        public int HumanApPerActivation { get; set; } = 2;
        /// <summary>战术训练阶位 0/1/2 的普通激活 AP（R03 §3：3/4/5）。</summary>
        public List<int> ApByTier { get; set; } = new List<int>();
        /// <summary>所有临时 +AP 效果下单次激活 AP 上限（R03 §3：6）。</summary>
        public int ApCap { get; set; } = 6;
        public int EnemyActionsPerActivation { get; set; } = 1;
        public double BasePlayerSpeed { get; set; }
        public double BasePlayerDodge { get; set; }
        public double BasePlayerArmor { get; set; }
        /// <summary>主角基础控制抗性（Q63：5，= actorBaseDefense 主角档 p0）。</summary>
        public double BasePlayerControlResist { get; set; }
        public bool HealthUsesExistingSurvivalHealth { get; set; }
    }

    /// <summary>容量 C 规则（combat-adjudications.json capacity）。</summary>
    public sealed class CapacityRulesConfig
    {
        /// <summary>新局及无已解锁事实者的 C（1）。</summary>
        public int NewGame { get; set; } = 1;
        public int Max { get; set; } = 4;
        public List<CapacityUnlockConfig> Unlocks { get; set; } = new List<CapacityUnlockConfig>();

        /// <summary>按存档里的真实事实算 C：已满足档取最大，至少 NewGame、至多 Max（事实只加不删，所以只增不减）。</summary>
        public int CapacityFor(ICollection<string> facts) =>
            Math.Max(1, Math.Min(Max, Unlocks.Where(u => u.Met(facts)).Select(u => u.Capacity).DefaultIfEmpty(NewGame).Max()));
    }

    /// <summary>一档容量解锁：allOf 全部成立且 anyOf（非空时）至少一条成立，C 至少为 capacity。</summary>
    public sealed class CapacityUnlockConfig
    {
        public int Capacity { get; set; }
        public List<string> AllOf { get; set; } = new List<string>();
        public List<string> AnyOf { get; set; } = new List<string>();
        public string Text { get; set; } = "";

        public bool Met(ICollection<string> facts) => AllOf.All(facts.Contains) && (AnyOf.Count == 0 || AnyOf.Any(facts.Contains));
    }

    /// <summary>Q63 人物基础防御：固定档、随机候选区间、位移装备抵抗规则。</summary>
    public sealed class ActorBaseDefenseConfig
    {
        public List<ActorDefenseProfileConfig> FixedProfiles { get; set; } = new List<ActorDefenseProfileConfig>();
        /// <summary>随机候选（B8 招募）：controlResist/evasion/baseSpeed 各 {min,max,step,distribution}，baseArmor 0。只入配置。</summary>
        public Newtonsoft.Json.Linq.JObject? RandomRecruit { get; set; }
        public ActorDefenseProfileConfig? Rb01Escort { get; set; }
        /// <summary>装备“位移抵抗”只作用的控制：拉扯、击退（不防失衡/减速）。</summary>
        public List<string> DisplacementGearOnlyStatuses { get; set; } = new List<string>();
        /// <summary>不同固定来源的装备位移抵抗相加后的上限 40（再与主动同类取强）。</summary>
        public double DisplacementGearAggregateCap { get; set; } = 40;
        public double SteadyBonus { get; set; } = 30;

        public ActorDefenseProfileConfig? Profile(string actorId) => FixedProfiles.FirstOrDefault(p => p.ActorId == actorId);
    }

    public sealed class ActorDefenseProfileConfig
    {
        public string ActorId { get; set; } = "";
        public double ControlResist { get; set; }
        public double Evasion { get; set; }
        public double BaseArmor { get; set; }
        public double BaseSpeed { get; set; }
        public string Role { get; set; } = "";
        public bool? CanRecruit { get; set; }
    }

    public sealed class SkillConfig
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public int InitialLevel { get; set; } = 1;
        /// <summary>"50*(L+1)"。</summary>
        public string NextLevelXpFormula { get; set; } = "";
        /// <summary>等级上限（A06 §1：全部熟练度最高 10 级，Lv10 停止获取该门经验）。null 为无上限（v0.6 旧口径）。</summary>
        public int? LevelCap { get; set; }
        /// <summary>combat 战斗门类、survival 求生门类、both 医疗（战内外都有用途，A06 §4）。</summary>
        public string Category { get; set; } = "combat";

        [JsonIgnore] public double XpBase => Formula.Num(NextLevelXpFormula, 0);
        [JsonIgnore] public double XpOffset => Formula.Num(NextLevelXpFormula, 1);
    }

    /// <summary>
    /// A06 §2 全技能主表的一个专属节点（15 门 × 1/3/5/7/10 级 = 75 个；开发计划 v0.9 B5 H6）。由 tools/import-b5-skills.py 从 A06 生成。
    /// 战斗节点（主动技能/战斗被动）在 B6/B7 执行，本批只存数据；求生被动按 Status 接入（live）或只存数据。
    /// </summary>
    public sealed class SkillNodeConfig
    {
        public string Id { get; set; } = "";
        public string SkillId { get; set; } = "";
        public int Level { get; set; }
        public string Name { get; set; } = "";
        public int Ap { get; set; }
        /// <summary>主动技能表的基准武器 E（同类不同装备按 武器E÷该类基准E 换算，见 GameConfig.SkillKindBaseEnergy）。</summary>
        public double BaseEnergy { get; set; }
        public string Positions { get; set; } = "";
        public string EffectText { get; set; } = "";
        /// <summary>界面显示用效果文字：A06 原文里的物品 ID、状态缩写、文档编号换成中文（导入脚本生成，数值不改；B5 试玩修复 M127）。</summary>
        public string DisplayText { get; set; } = "";
        [JsonIgnore] public string Text => DisplayText != "" ? DisplayText : EffectText;
        public int CooldownRounds { get; set; }
        public string ExtraCost { get; set; } = "";
        /// <summary>combat 或 survival。</summary>
        public string Kind { get; set; } = "combat";
        /// <summary>live 已接入；baseline 1 级开放原有内容；dormant 现在没有挂点只存数据；pending 待策划确认；combat B6/B7。</summary>
        public string Status { get; set; } = "combat";
        /// <summary>求生被动的程序效果（键/运算/值，数值取自 A06 §2 效果列）。</summary>
        public List<EquipEffectConfig> Effects { get; set; } = new List<EquipEffectConfig>();

        [JsonIgnore] public bool IsSurvival => Kind == "survival";
        [JsonIgnore] public bool IsLive => Status == "live";

        /// <summary>“12→1”左边：己方使用位（数字连写）；“按武器”为空串，按武器使用位。</summary>
        [JsonIgnore] public string OwnPart => PositionsPart(0);
        /// <summary>“12→1”右边：敌方目标位（数字连写）；“按武器”为空串；“自身/友方/全队”为空串。</summary>
        [JsonIgnore] public string TargetPart => PositionsPart(1);

        private string PositionsPart(int i)
        {
            var parts = Positions.Split('→');
            if (parts.Length != 2) return "";
            return new string(parts[i].Where(char.IsDigit).ToArray());
        }
    }

    /// <summary>
    /// 战斗/医疗主动技能的执行口径（A06 §2 效果列；开发计划 v0.9 B7，tools/import-b7-combat.py 导入）。
    /// AP、基准E、使用位→目标、冷却、额外物资仍读 SkillNodeConfig。
    /// </summary>
    public sealed class SkillExecConfig
    {
        public string Id { get; set; } = "";
        /// <summary>attack 攻击 / guard 自身格挡类 / buff 自身增益 / ally 友方增益或调整 / medical 医疗 / reload 装填。</summary>
        public string Type { get; set; } = "attack";
        /// <summary>single 单体 / suffix 起点到4号位（贯穿，Q82）/ pair 旧相邻双位（已不用）/ self 自身 / ally 友方（单人时即自己）。</summary>
        public string TargetMode { get; set; } = "single";
        public double DamageFactor { get; set; } = 1;
        /// <summary>同一目标的独立命中次数（连拳/连刺/连射 2）。</summary>
        public int Hits { get; set; } = 1;
        public double AccuracyBonus { get; set; }
        public double ArmorIgnore { get; set; }
        /// <summary>A06“额外物资”写明的弹药数（猎标1、连射2）；0 表示有弹药的武器按每次命中段数各1。</summary>
        public int Ammo { get; set; }
        public int MinAmmo { get; set; }
        /// <summary>单发弩（弹仓1）不可用（连射）。</summary>
        public bool NoSingleShot { get; set; }
        /// <summary>移除目标格挡次数（破阵）。</summary>
        public int RemoveGuard { get; set; }
        /// <summary>处决：目标 H≤上限×比例时伤害再×ExecuteFactor。</summary>
        public double? ExecuteHpRatio { get; set; }
        public double ExecuteFactor { get; set; } = 1;
        /// <summary>突进斩：攻击前我方前移 1 位，移位失败拒绝整次。</summary>
        public int Advance { get; set; }
        /// <summary>专长射击：这些武器改为对敌方 1、2 号位各一击、伤害×AreaDamageFactor。</summary>
        public List<string> AreaWeapons { get; set; } = new List<string>();
        public double AreaDamageFactor { get; set; } = 1;
        public List<SkillStatusConfig> Effects { get; set; } = new List<SkillStatusConfig>();

        public int GuardCharges { get; set; }
        public double CounterFactor { get; set; }
        public int BraceCharges { get; set; }
        public int BraceMax { get; set; }
        public double BraceFactor { get; set; }
        /// <summary>斧蓄力：下次该斧攻击伤害×，持续至下一大回合末。</summary>
        public double ChargeFactor { get; set; }
        public double DisplaceResist { get; set; }
        /// <summary>增益持续的大回合数（施加轮 r 起到 r+N 轮末）。</summary>
        public int BuffRounds { get; set; }
        public double TeamGuardFactor { get; set; }
        public bool Cleanse { get; set; }
        /// <summary>挣脱可解除的状态（Q80：slow 减速 / binding 束缚 / mark 标记；不解感染、出血、破甲、虚弱、失衡、保护）。</summary>
        public List<string> CleanseStatuses { get; set; } = new List<string>();
        public int MoveBack { get; set; }
        /// <summary>贯穿（Q82，targetMode "suffix"）：从起点 k 起按槽偏移的伤害倍率 0.65/0.50/0.35/0.25，空位不重排。</summary>
        public List<double> SuffixFactors { get; set; } = new List<double>();
        public double InspireAccuracy { get; set; }
        public double PanicReduce { get; set; }
        public double AllOutFactor { get; set; }
        public int ApBonus { get; set; }
        public bool Protect { get; set; }
        /// <summary>需要相邻/其他队友（保护、掩护、整队、协同）；我方只有一人时禁用（队友参战在 B8）。</summary>
        public bool NeedsAllies { get; set; }

        public Dictionary<string, int> Items { get; set; } = new Dictionary<string, int>();
        /// <summary>完全按 R04 绷带效果（战地包扎）。</summary>
        public bool UseBandage { get; set; }
        public bool StopBleed { get; set; }
        /// <summary>回血基础值，×(1+0.02×医疗L)。</summary>
        public double Heal { get; set; }
        /// <summary>危急救护：目标 H≤上限×比例才可用。</summary>
        public double? HealMaxRatio { get; set; }
        public bool RemoveWeakOrStagger { get; set; }
        public bool ReloadFull { get; set; }

        [JsonIgnore] public bool IsAttack => Type == "attack";
    }

    /// <summary>技能附带状态（R03 §4）：stagger 失衡 / slow 减速 / weak 虚弱 / mark 标记 / armor_break 破甲 / knockback 击退 / cancel_charge 打断蓄力 / bleed 流血。</summary>
    public sealed class SkillStatusConfig
    {
        public string Status { get; set; } = "";
        public double Value { get; set; }
        public int Rounds { get; set; }
        /// <summary>需命中后再判控制（R03 §4）。</summary>
        public bool Control { get; set; }
        /// <summary>须造成正直接伤害才施加（割伤、致残射击）。</summary>
        public bool Positive { get; set; }
    }

    /// <summary>求生被动的附加参数（取自 A06 §2 效果原文）。</summary>
    public sealed class SkillPassiveRulesConfig
    {
        /// <summary>gathering_05“木/石/纤维定向采集产量+1”适用的产物。</summary>
        public List<string> GatherBasicItems { get; set; } = new List<string>();
        /// <summary>gathering_10“不生产工业材料”排除的产物（开发假设：废金属、零件，待确认 Q55）。</summary>
        public List<string> IndustrialItems { get; set; } = new List<string>();
        /// <summary>athletics_05“≤20kg 时移动基础工作量×0.95”的重量线（克）。</summary>
        public int MoveLightMaxGrams { get; set; }
    }

    /// <summary>v0.6 skillGrowth：能力随等级持续增长，无硬上限（§03）。</summary>
    public sealed class SkillGrowthConfig
    {
        public string DamageFactor { get; set; } = "";
        public string AccuracyRating { get; set; } = "";
        public string EnergyCost { get; set; } = "";
        public string MedicalHeal { get; set; } = "";
        public int? SkillLevelCap { get; set; }
        public double? DamageCap { get; set; }
        public double? AccuracyRatingCap { get; set; }
        public double? EnergyCostHardFloor { get; set; }
        public double? CraftWorkHardFloor { get; set; }
        public bool PhysicalStateMaxStillApplies { get; set; }

        [JsonIgnore] public double DamageBase => Formula.Num(DamageFactor, 0);
        [JsonIgnore] public double DamagePerLevel => Formula.Num(DamageFactor, 1);
        [JsonIgnore] public double AccuracyPerLevel => Formula.Num(AccuracyRating, 0);
        [JsonIgnore] public double EnergyBase => Formula.Num(EnergyCost, 0);
        [JsonIgnore] public double EnergyPerLevel => Formula.Num(EnergyCost, 1);
        [JsonIgnore] public double MedicalHealBase => Formula.Num(MedicalHeal, 0);
        [JsonIgnore] public double MedicalHealOne => Formula.Num(MedicalHeal, 1);
        [JsonIgnore] public double MedicalHealPerLevel => Formula.Num(MedicalHeal, 2);
    }

    /// <summary>v0.6 xp：经验来源与反刷计数（§03）。</summary>
    public sealed class XpConfig
    {
        /// <summary>"clamp(ceil(originalBaseMinutes/10),1,12)"。</summary>
        public string Craft { get; set; } = "";
        public int Medical { get; set; }
        public int DamagingAttackCommand { get; set; }
        public int MaxHitsPerEncounterEnemySkill { get; set; }
        /// <summary>v1.0 P01 起为 false：群攻逐命中目标计经验。</summary>
        public bool GroupAttackXpIsSingleCommand { get; set; }
        /// <summary>v1.0 P01：每个命中且未满额度的目标各给一次 +2。</summary>
        public bool PerHitTarget { get; set; }
        /// <summary>v1.0 P01：护甲完全抵消（0 伤）的命中也计经验。</summary>
        public bool ZeroDamageHitCounts { get; set; }
        public int CancelOrPreviewXp { get; set; }
        /// <summary>A06 §3：采集/搜集每成功提交一次的经验（+2）与同资源点每个世界日的经验次数上限（3）。</summary>
        public int GatherSubmit { get; set; }
        public int GatherDailyPerNode { get; set; }
        /// <summary>A06 §3：行旅每完整非取消物理边移动 +1，同向边每个世界日最多 1 次。</summary>
        public int TravelEdge { get; set; }
        public int TravelDailyPerDirectedEdge { get; set; }
        public List<string> CounterKey { get; set; } = new List<string>();

        [JsonIgnore] public double CraftDivisor => Formula.Num(Craft, 0);
        [JsonIgnore] public double CraftMin => Formula.Num(Craft, 1);
        [JsonIgnore] public double CraftMax => Formula.Num(Craft, 2);
    }

    /// <summary>v0.6 crafting：制造熟练度折减工作量（§03）。</summary>
    public sealed class CraftingSkillConfig
    {
        /// <summary>"1/(1+0.025*L)"。</summary>
        public string WorkFactor { get; set; } = "";
        public double? MinFactor { get; set; }
        public bool AffectsPassiveCooking { get; set; }
        public bool AffectsButchering { get; set; }
        public bool BatchOverflowWithinSameReservedBatch { get; set; }
        public bool CarryOverflowToOtherAction { get; set; }
        /// <summary>Q57 选 B（B7b）：制造等级工作量倍率已取消，WorkFactor 只作历史记录，程序不再读。</summary>
        public bool LegacyLevelWorkFactorRemoved { get; set; }

        [JsonIgnore] public double PerLevel => Formula.Num(WorkFactor, 2);
    }

    public sealed class WeaponConfig
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string SkillId { get; set; } = "";
        /// <summary>
        /// 武器类型（A05 §1“类”列，九种：blade/axe/spear/blunt/archery/firearms/shield/command/medical）：两武器槽同 kind 最多 1 件
        /// （“同类型武器限制（用户跨会话补充，2026-10-30）”）。熟练度仍按 SkillId。拳没有 kind（不占武器槽）。
        /// </summary>
        public string? WeaponKind { get; set; }
        public double DamageMin { get; set; }
        public double DamageMax { get; set; }
        public double EnergyCost { get; set; }
        public int WeightGrams { get; set; }
        /// <summary>固定特性里的“忽略护甲”（A05 §1：简易斧0.1、弩0.15、步枪0.2），作为穿透计入扣甲（B6 R23）。</summary>
        public double ArmorIgnore { get; set; }
        /// <summary>front / all / all_rear_damage_0.75。</summary>
        public string Targets { get; set; } = "front";
        public string? AmmoId { get; set; }
        public int MagazineCapacity { get; set; }
        public int BasicApCost { get; set; } = 1;
        public double AccuracyRating { get; set; }
        public double CritRatingBase { get; set; }
        public double[]? DurabilityMaxRange { get; set; }
        // ───── A05 §1 新表字段（B4 H3：B6 四对四战斗内核之前只存数据并在详情显示，现行两行战斗仍按 Targets） ─────
        /// <summary>阶 T0—T3。拳为 null。</summary>
        public int? Tier { get; set; }
        /// <summary>装备速度修正（A05 §1“速度修正”）。</summary>
        public double Speed { get; set; }
        /// <summary>使用位（己方站位，数字连写，如“234”）。</summary>
        public string? OwnPositions { get; set; }
        /// <summary>目标位（敌方槽位数字连写，或“友方”）。</summary>
        public string? TargetPositions { get; set; }
        /// <summary>固定基础特性原文。</summary>
        public string? TraitText { get; set; }
        /// <summary>拆解产出（A05 §5）。</summary>
        public SalvageOutputConfig? Salvage { get; set; }
        /// <summary>固定特性里挂在技能上的程序效果（A05 §1；B7 tools/import-b7-combat.py）：锋口 bleedTriggers、扫切 bleedAccuracy……</summary>
        public Dictionary<string, double> TraitEffects { get; set; } = new Dictionary<string, double>();
        public double Trait(string key, double fallback = 0) => TraitEffects.TryGetValue(key, out var v) ? v : fallback;

        /// <summary>指挥、医疗器具不能直接攻击（A05 §1“不可直接攻击”），只用本门的增益/医疗技能（B7）；盾自 B7 起有盾击。</summary>
        public static readonly string[] SkillLockedKinds = { "command", "medical" };
        [JsonIgnore] public bool CanAttack => IsFists || System.Array.IndexOf(SkillLockedKinds, WeaponKind) < 0;

        [JsonIgnore] public bool ReachesRear => Targets.StartsWith("all");
        /// <summary>矛打后排（有前排屏障时）的伤害倍率；没有写倍率的为 1。</summary>
        [JsonIgnore] public double RearDamageFactor => Targets.StartsWith("all_rear_damage_") ? Formula.Num(Targets, 0) : 1;
        [JsonIgnore] public bool UsesMagazine => AmmoId != null && MagazineCapacity > 0;
        [JsonIgnore] public bool ConsumesLooseAmmo => AmmoId != null && MagazineCapacity == 0;
        [JsonIgnore] public bool IsFists => Id == "fists";
    }

    /// <summary>v0.6 actions：招式与通用战斗动作。ap 为数字或 "remaining"（结束行动）。</summary>
    public sealed class CombatActionConfig
    {
        public string Id { get; set; } = "";
        public string Ap { get; set; } = "1";
        public string? EnergySource { get; set; }
        public double? Energy { get; set; }
        public double DamageFactor { get; set; } = 1;
        public double AccuracyRatingFactor { get; set; } = 1;
        public List<string> Skills { get; set; } = new List<string>();
        public int MinLevel { get; set; }
        public bool ApplyCorrespondingSkillEnergyReduction { get; set; }
        public double Bleed { get; set; }
        public int BleedTriggers { get; set; }
        public string? Status { get; set; }
        public string? WeaponId { get; set; }
        public int MaxTargets { get; set; }
        public string? Targets { get; set; }
        public int AmmoCost { get; set; }
        public int Charges { get; set; }
        public double DirectDamageFactor { get; set; } = 1;
        public string? Expires { get; set; }
        public double RestoreEnergy { get; set; }
        public double ReducePanic { get; set; }
        public int MaxPerTurn { get; set; }
        public string? SourceOwner { get; set; }
        public string? ItemId { get; set; }
        public int Count { get; set; }
        public bool RemoveBleed { get; set; }
        public string? HealthFormula { get; set; }
        public int HealthCooldownMinutes { get; set; }
        public bool BleedRemovalIgnoresHealthCooldown { get; set; }
        public int AdditionalWorldMinutes { get; set; }
        public string? InfectionReductionFormula { get; set; }
        public int InfectionEffectCooldownMinutes { get; set; }
        public string? SkillId { get; set; }
        public bool RequiresInsufficientNormalAttackEnergy { get; set; }
        public double LowEnergyChanceFactor { get; set; } = 1;
        public double InstantVirusReduction { get; set; }
        public double MinimumInfectedLoad { get; set; }
        public int CooldownMinutes { get; set; }
        public double PositiveVirusProgressFactor { get; set; } = 1;
        public int EffectMinutes { get; set; }
        public bool ClearInfected { get; set; }
        public double? SetVirusLoad { get; set; }
        public double RestoreHealth { get; set; }

        /// <summary>行动点；“remaining”（结束行动）为 -1。</summary>
        [JsonIgnore] public int ApCost => int.TryParse(Ap, out var v) ? v : -1;
    }

    public sealed class EnemyConfig
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        /// <summary>zombie / animal。</summary>
        public string Family { get; set; } = "";
        public double Hp { get; set; }
        public double Armor { get; set; }
        public double Dodge { get; set; }
        public double Speed { get; set; }
        public double DamageMin { get; set; }
        public double DamageMax { get; set; }
        public string Behavior { get; set; } = "";
        public double? FleeAtHp { get; set; }
        public bool ImmuneBleed { get; set; }
        public bool ArtRequired { get; set; }
        public double AttackRating { get; set; }
        /// <summary>common / rare / elite。</summary>
        public string Rarity { get; set; } = "";
        public double BiteIntentWeightPct { get; set; }
        public bool WorldPersistentId { get; set; }
        /// <summary>基础惊吓概率 q（R05 §6.3，T01 已批准实装初值）。</summary>
        public double FearChance { get; set; }
        /// <summary>惊吓值：受惊时入场恐慌附加（R05 §6.3）。</summary>
        public double FearValue { get; set; }
        /// <summary>控制抗性（百分点；A09 §1，B7 接入）。</summary>
        public double ControlResist { get; set; }
        // ───── A09 §1「敌人面板与行为」、R03「敌方资源接口补充」（B7 R26） ─────
        /// <summary>普通 3AP、精英 4AP（追击 2AP）。</summary>
        public int Ap { get; set; } = 3;
        public bool Elite { get; set; }
        /// <summary>己位（数字连写）：主要意图从这些站位出手。</summary>
        public string OwnSlots { get; set; } = "12";
        /// <summary>目标位（我方站位，数字连写）。</summary>
        public string TargetSlots { get; set; } = "12";
        /// <summary>意图循环：按本场第几次正常激活取 opening 前几项，之后循环 cycle。</summary>
        public EnemyIntentsConfig Intents { get; set; } = new EnemyIntentsConfig();
        /// <summary>A09 附加掉落（独立额外判一次；“固定”为 1）。</summary>
        public EnemyDropConfig? ExtraDrop { get; set; }
        /// <summary>护盔感染者：首次失衡免疫。</summary>
        public bool StaggerImmuneOnce { get; set; }
        /// <summary>膨胀感染者：死亡时对我方 1、2 号位各 H−该值（护甲照算，非咬不感染）。</summary>
        public double DeathSplash { get; set; }
        /// <summary>流窜枪手：本场弹药计划总数与弹仓容量（打空后弱近战或前移）。</summary>
        public int AmmoTotal { get; set; }
        public int Magazine { get; set; }

        [JsonIgnore] public bool IsZombie => Family == "zombie";
        [JsonIgnore] public bool IsHuman => Family == "human";
        [JsonIgnore] public string FamilyName => Family switch { "zombie" => "丧尸", "animal" => "动物", "human" => "敌对人类", _ => Family };
        [JsonIgnore] public bool NeutralUntilAttacked => Behavior == "neutral_until_attacked";
    }

    public sealed class EnemyIntentsConfig
    {
        public List<EnemyIntentStepConfig> Opening { get; set; } = new List<EnemyIntentStepConfig>();
        public List<EnemyIntentStepConfig> Cycle { get; set; } = new List<EnemyIntentStepConfig>();
        [JsonIgnore] public int Count => Opening.Count + Cycle.Count;

        /// <summary>第 n 次正常激活（0 起）用第几项（opening 与 cycle 合并编号）。</summary>
        public int IndexFor(int n) => n < Opening.Count ? n : Cycle.Count == 0 ? 0 : Opening.Count + (n - Opening.Count) % Cycle.Count;

        public EnemyIntentStepConfig? At(int index) =>
            index < 0 ? null : index < Opening.Count ? Opening[index] : index - Opening.Count < Cycle.Count ? Cycle[index - Opening.Count] : null;
    }

    /// <summary>
    /// 敌人一次正常激活的主意图（A09 §1）：attack 攻击（可按 bitePct 改判咬击）/ bite 咬击 / charge 冲撞重击推撞 / charge_prepare 蓄力 /
    /// guard 防御 / howl 嚎叫尖啸 / protect 保护相邻同伴 / signal 鸣响请求增援 / buff 自身下一击增益 / sweep 扫击 / spit 喷吐 /
    /// pull 拉扯 / shoot 射击 / reload 装填。
    /// </summary>
    public sealed class EnemyIntentStepConfig
    {
        public string Kind { get; set; } = "attack";
        public string Verb { get; set; } = "";
        public double Factor { get; set; } = 1;
        public double BitePct { get; set; }
        /// <summary>命中后控制：slow 扯腿减速 / stagger 失衡。</summary>
        public string? Control { get; set; }
        public double BleedPct { get; set; }
        public int Knockback { get; set; }
        public int Pull { get; set; }
        public double ArmorBreak { get; set; }
        public int ArmorBreakRounds { get; set; }
        public double Panic { get; set; }
        public double NextHitFactor { get; set; }
        /// <summary>本场最多几次（隔离守卫保护限1次）；0 不限。</summary>
        public int Limit { get; set; }
        /// <summary>覆盖敌人的己位/目标位（尖啸尸等后排敌人的近战为 12→12）。</summary>
        public string? OwnSlots { get; set; }
        public string? TargetSlots { get; set; }
    }

    public sealed class EnemyDropConfig
    {
        public string ItemId { get; set; } = "";
        public int Count { get; set; } = 1;
        public double Chance { get; set; } = 1;
    }

    /// <summary>v0.6 combatMath：命中、暴击、伤害、身体修正（§04）。</summary>
    /// <summary>敌方空前排迫近（Q80，combat-resolution-adjudications.json enemyActivation.exposureApproach）。</summary>
    public sealed class ExposureApproachConfig
    {
        public int ApproachAp { get; set; } = 1;
        public int PlainAttackAp { get; set; } = 2;
        public double DamageFactor { get; set; } = 0.7;
        public int TotalAp => ApproachAp + PlainAttackAp;
    }

    public sealed class CombatMathConfig
    {
        public string Initiative { get; set; } = "";
        public string TieBreak { get; set; } = "";
        public string PlayerAccuracyRating { get; set; } = "";
        public string HitProbability { get; set; } = "";
        public string CritRating { get; set; } = "";
        public string CritProbability { get; set; } = "";
        public double CritFactor { get; set; } = 1.5;
        public string PlayerDamage { get; set; } = "";
        public string GuardAppliedAfterArmor { get; set; } = "";
        public double? HardHitChanceClamp { get; set; }
        public double? HardCritChanceClamp { get; set; }
        public bool EnemySkillScaling { get; set; }
        public double EnemyCritRating { get; set; }
        public string RandomDamage { get; set; } = "";
        public double LowEnergyExclusive { get; set; }
        public double LowEnergySpeedPenalty { get; set; }
        public double LowWakefulnessInclusive { get; set; }
        /// <summary>我方可控角色攻击能力的基础加值 8（Q67：攻击能力项，不是命中率百分点；不加给敌人）。</summary>
        public double PlayerAccuracyAssistance { get; set; }
        /// <summary>疲劳系数（Q67）：支付本次成本后 E&lt;4 或 S≤20 时统一一次 ×0.95，双低不叠乘；取代旧两条 ×0.85。</summary>
        public double FatigueAccuracyFactor { get; set; } = 1;
        public double LowWakefulnessSpeedPenalty { get; set; }
        public double StaggerAccuracyFactor { get; set; } = 1;
        public bool GuardStacks { get; set; }
        public bool AimRemoved { get; set; }
        // ───── B6 四对四（R03 §2 行动顺序、速度与追击；R01 §4） ─────
        /// <summary>有效速度下限（R03 §2：max(1, 基础+装备+状态)）。</summary>
        public double MinSpeed { get; set; } = 1;
        /// <summary>盾类格挡的直接伤害倍率（R03 §3、A05 木盾“盾类格挡减伤60%”）；普通防御为 guardAppliedAfterArmor 的 0.5。</summary>
        public double ShieldGuardFactor { get; set; } = 0.4;
        // ───── B6 命中、伤害与控制（R03 §4） ─────
        /// <summary>攻击能力与闪避下限（A=max(1,…)，目标闪避最低1）。</summary>
        public double MinAccuracy { get; set; } = 1;
        public double MinEvasion { get; set; } = 1;
        /// <summary>
        /// 正伤害增益递减（Q82，取代旧硬上限×1.5）：同名取强、合法不同来源先乘成 rawB；rawB≤拐点原值，
        /// 以上 B=拐点+尾段×(1−exp(−(rawB−拐点)/尾段))，渐近 拐点+尾段（1.35+0.25=1.6）。成长/技能/处决/暴击/虚弱不进此曲线。
        /// </summary>
        public double DamageBuffKnee { get; set; } = 1.35;
        public double DamageBuffTail { get; set; } = 0.25;
        /// <summary>敌方空前排迫近（Q80）：迫近 1AP + 一次 2AP 弱普通近袭×0.70（无咬击/控制，不改我方真实槽）。</summary>
        public ExposureApproachConfig ExposureApproach { get; set; } = new ExposureApproachConfig();
        /// <summary>控制判定 clamp(controlBase + 控制能力/100 − 目标控制抗性/100, controlMin, controlMax)。</summary>
        public double ControlBase { get; set; } = 0.75;
        public double ControlMin { get; set; } = 0.10;
        public double ControlMax { get; set; } = 0.95;
        /// <summary>控制成功后目标“稳态”至下一大回合末：控制抗性临时+30个百分点，同轮重复成功不加第二层。</summary>
        public double SteadyResistBonus { get; set; } = 30;
        /// <summary>清醒低于此值速度−1（R03 §2“S&lt;20”；命中的低清醒修正仍按 lowWakefulnessInclusive）。</summary>
        public double LowWakefulnessSpeedExclusive { get; set; } = 20;
        /// <summary>追击进度 q=clamp(pursuitScale×(v/vmax−1), 0, pursuitThreshold)；满 pursuitThreshold 得一次 pursuitAp 的追击。</summary>
        public double PursuitScale { get; set; } = 50;
        public double PursuitThreshold { get; set; } = 100;
        public int PursuitAp { get; set; } = 2;


        [JsonIgnore] public double CritAccuracyPerLevel => Formula.Num(CritRating, 0);
        [JsonIgnore] public double CritDenominator => Formula.Num(CritProbability, 0);
        [JsonIgnore] public double GuardFactor => Formula.Num(GuardAppliedAfterArmor, 0);
    }

    public sealed class PanicPenaltyConfig
    {
        public double Minimum { get; set; }
        public double AccuracyRatingFactor { get; set; } = 1;
        public double RetreatFactor { get; set; } = 1;
    }

    /// <summary>v0.6 panic：战斗恐慌（§04）。</summary>
    public sealed class PanicConfig
    {
        public double[] Range { get; set; } = { 0, 100 };
        /// <summary>"(100-morale)/2"。</summary>
        public string Initial { get; set; } = "";
        /// <summary>系统策划总册 v1.0 Q3/T01：受惊改为概率事件（取消固定+10），见 R03 §2.1、R05 §6.3。</summary>
        public FearConfig Fear { get; set; } = new FearConfig();
        /// <summary>"actualDamage*1.5"。</summary>
        public string DirectDamageGain { get; set; } = "";
        public List<PanicPenaltyConfig> Penalties { get; set; } = new List<PanicPenaltyConfig>();
        public bool PenaltiesSelectHighestOnly { get; set; }
        /// <summary>"min(5,panic/20)"。</summary>
        public string PostBattleMoraleLoss { get; set; } = "";
        public bool ClearAfterBattle { get; set; }

        [JsonIgnore] public double InitialBase => Formula.Num(Initial, 0);
        [JsonIgnore] public double InitialDivisor => Formula.Num(Initial, 1);
        [JsonIgnore] public double DamageGainFactor => Formula.Num(DirectDamageGain, 0);
        [JsonIgnore] public double MoraleLossMax => Formula.Num(PostBattleMoraleLoss, 0);
        [JsonIgnore] public double MoraleLossDivisor => Formula.Num(PostBattleMoraleLoss, 1);

        /// <summary>当前恐慌的惩罚档（只取最高一档）；没有为 null。</summary>
        public PanicPenaltyConfig? PenaltyFor(double panic) =>
            Penalties.Where(p => panic >= p.Minimum).OrderByDescending(p => p.Minimum).FirstOrDefault();
    }

    /// <summary>
    /// 受惊（R03 §2.1 T01 已批准实装初值）：p=q×(1−clamp(M,0,100)/100)×K；K 按入战情境（主动接近 0.5、探索/清理接敌 1、剧情首战 1）；
    /// 多敌只取一个主惊吓源（基础概率最高→惊吓值较高→worldEnemyId 小）；每个世界敌人的随机数 u 在 60 游戏分钟内复用。
    /// </summary>
    public sealed class FearConfig
    {
        public string Chance { get; set; } = "";
        public Dictionary<string, double> ContextFactors { get; set; } = new Dictionary<string, double>();
        public int ReuseRollMinutes { get; set; }
        public string SourceSelection { get; set; } = "";
        public bool OncePerBattle { get; set; }

        public double Factor(string context) => ContextFactors.TryGetValue(context, out var k) ? k : 1;
    }

    /// <summary>v0.6 retreat：撤退概率 sigmoid（§07）。</summary>
    public sealed class RetreatConfig
    {
        public string Chance { get; set; } = "";
        public bool SuccessMovesLocation { get; set; }
        public bool FailureConsumesTurn { get; set; }
        public bool PreserveEncounterSurvivorHp { get; set; }
        public bool ResetSurvivorTemporaryStatuses { get; set; }
        public bool ClaimLootOnRetreat { get; set; }

        [JsonIgnore] public double SpeedDivisor => Formula.Num(Chance, 0);
        [JsonIgnore] public double Offset => Formula.Num(Chance, 1);
    }

    public sealed class AmmoRulesConfig
    {
        public bool WeaponLoadedCountsInMass { get; set; }
        public bool LooseAndLoadedExclusiveOwners { get; set; }
        public int ReloadAp { get; set; } = 1;
        public bool AutoReload { get; set; }
        public bool BowConsumesLooseArrowDirectly { get; set; }
        public bool RecoverArrows { get; set; }
    }

    public sealed class WearConfig
    {
        public double PerExecutedAttackCommand { get; set; } = 1;
        public bool OnMiss { get; set; }
        public bool PerGroupTarget { get; set; }
        public bool GatherWearRuleUnchanged { get; set; }
        public bool ZeroDurabilityDisablesAttack { get; set; }
    }

    public sealed class NoiseConfig
    {
        public double Max { get; set; } = 100;
        public double GunGain { get; set; }
        public double CrossbowGain { get; set; }
        public double BowAndMeleeGain { get; set; }
        public int MaxReinforcementsPerBattle { get; set; }
        public bool SpawnAtNextRoundWithSpace { get; set; }
        public bool CancelAfterBattleEnd { get; set; }
        public string ReinforcementSource { get; set; } = "";
        public bool CreateNewEnemy { get; set; }
        public string ExistingEnemyFamilyFilter { get; set; } = "";
    }

    public sealed class BleedStatusConfig
    {
        public double DamagePerOwnActivation { get; set; }
        public int Triggers { get; set; }
        public bool Stacks { get; set; }
        public int RefreshMaxTriggers { get; set; }
        public string ImmuneFamily { get; set; } = "";
        public string PostBattle { get; set; } = "";
        public bool NoExtraExitTrigger { get; set; }
    }

    public sealed class StaggerStatusConfig
    {
        public string ExpiresLatest { get; set; } = "";
        public double NextAttackAccuracyFactor { get; set; } = 1;
    }

    public sealed class SlowStatusConfig
    {
        public double NextRoundSpeedPenalty { get; set; }
        public string Expires { get; set; } = "";
        public bool Stacks { get; set; }
    }

    public sealed class StatusesConfig
    {
        public BleedStatusConfig Bleed { get; set; } = new BleedStatusConfig();
        public StaggerStatusConfig Stagger { get; set; } = new StaggerStatusConfig();
        public SlowStatusConfig Slow { get; set; } = new SlowStatusConfig();
    }

    /// <summary>v0.6 worldTime：每有效轮一次 5 分钟世界 tick（§07）。</summary>
    public sealed class BattleTimeConfig
    {
        public int RoundWorldMinutes { get; set; } = 5;
        public bool TickOnceAtRoundEndOrNonfatalEarlyExit { get; set; }
        public bool PreviewAdvancesTime { get; set; }
        public bool FreezeImmediatelyOnDirectDeathNoFutureTick { get; set; }
        public RateBlock HourlyRates { get; set; } = new RateBlock();
        public bool ActionEnergyPaidSeparately { get; set; }
        public bool CountsAsWorkingForZeroEnergyDamage { get; set; }
        public bool PassiveFacilitiesAndRefillsAdvance { get; set; }
        public bool BattleLowValueWarningsNonBlocking { get; set; }
        public bool NoLegacyInstantUseChildAction { get; set; }
        public bool FoodAndDrinkInCombatEnabled { get; set; }
        public double ZeroEnergyWorkingHealthPerHour { get; set; }
        /// <summary>
        /// 战斗期间每名参战角色 N/W 全部负变化为 0（R03 §6、R01 §3：含腹泻扣水），战后不补扣；进入时已 N/W=0 的健康伤害照旧（B6 R24）。
        /// </summary>
        public bool FreezeNegativeSatietyHydration { get; set; }
    }

    /// <summary>v0.6 encounters：按地块敌密度接敌、绕行与保护期（§15）。</summary>
    public sealed class EncountersConfig
    {
        public string Source { get; set; } = "";
        public bool CreateNewEnemiesOnActionCompletion { get; set; }
        public List<string> SafeLocations { get; set; } = new List<string>();
        public int NoRandomBeforeDay { get; set; } = 2;
        public List<string> CheckOnlyAfterCompletedActions { get; set; } = new List<string>();
        public Dictionary<string, double> FullLocalPopulationActionRisk { get; set; } = new Dictionary<string, double>();
        public string ActualRiskFormula { get; set; } = "";
        public bool NoRiskWhenZeroHostile { get; set; }
        public bool ActiveContactEnabled { get; set; }
        public bool CommitOriginalActionBeforeEncounter { get; set; }
        public int BypassMinutes { get; set; }
        public string BypassRateCategory { get; set; } = "";
        public int PostExitRandomEncounterGraceMinutes { get; set; }
        public bool NoNestedRandomDuringCombatOrBypass { get; set; }
        public int GroupMax { get; set; } = 3;
        public string GroupSelection { get; set; } = "";
        public bool BypassLeavesWorldEnemiesAlive { get; set; }
        public bool DefeatRemovesOnlyKilledWorldIds { get; set; }
        public bool CombatSurvivorsKeepHpAndXpCountersOnWorldId { get; set; }
    }

    public sealed class ZombieLootConfig
    {
        public string Version { get; set; } = "";
        public bool RollAtFirstEnemyDeath { get; set; }
        public bool ClaimOnceAtFinalVictory { get; set; }
        public List<LootEntryConfig> Entries { get; set; } = new List<LootEntryConfig>();

        [JsonIgnore] public LootTableConfig Table => new LootTableConfig { Version = Version, Entries = Entries };
    }

    public sealed class CarcassConfig
    {
        public string EnemyId { get; set; } = "";
        public string CarcassItemId { get; set; } = "";
        public int Count { get; set; } = 1;
        public int ButcherBaseMinutes { get; set; }
        public bool ButcherManufacturingSpeedBonus { get; set; }
        public List<string> RequiredCarriedTools { get; set; } = new List<string>();
        public double ToolWearOnSuccess { get; set; }
        public Dictionary<string, int> Outputs { get; set; } = new Dictionary<string, int>();
    }

    public sealed class ToolDurabilityConfig
    {
        public double[] MaxRange { get; set; } = new double[0];
        public double WearPerSuccessfulThermalProcess { get; set; }
        public double WearPerSuccessfulIgnition { get; set; }
    }

    public sealed class RepairConfig
    {
        public int BaseMinutes { get; set; }
        public Dictionary<string, int> Inputs { get; set; } = new Dictionary<string, int>();
        public double RestoreFractionOfOwnMax { get; set; }
        public bool RequireEmptyDetachedPot { get; set; }
        public bool RerollMaximum { get; set; }
    }

    /// <summary>v0.6 durability：实例随机最大耐久、拾荒成色、锅与打火器磨损（§05）。</summary>
    public sealed class DurabilityConfig
    {
        /// <summary>uniform_inclusive_tenths：按 0.1 精度均匀抽一次。</summary>
        public string Roll { get; set; } = "";
        public string RollAt { get; set; } = "";
        public string NewItemCurrent { get; set; } = "";
        public double[] ScavengedConditionOptional { get; set; } = new double[0];
        public bool AffixesMayModifyRangeWithoutGlobalHardCap { get; set; }
        public Dictionary<string, ToolDurabilityConfig> Tools { get; set; } = new Dictionary<string, ToolDurabilityConfig>();
        public bool NeedEnoughDurabilityForActionCost { get; set; }
        public string MigrateExisting { get; set; } = "";
        public RepairConfig Repair { get; set; } = new RepairConfig();

        [JsonIgnore] public double LighterWear => Tools.TryGetValue("lighter", out var t) ? t.WearPerSuccessfulIgnition : 0;
    }

    public sealed class HealthyConditionsConfig
    {
        public double SatietyAtLeast { get; set; }
        public double HydrationAtLeast { get; set; }
        public double WakefulnessAtLeast { get; set; }
        public double HealthRatioAtLeast { get; set; }
        public double MoraleAtLeast { get; set; }
    }

    public sealed class ImmunityPenaltiesConfig
    {
        public double SatietyBelow20 { get; set; }
        public double HydrationBelow20 { get; set; }
        public double WakefulnessBelow20 { get; set; }
        public double HealthBelow5 { get; set; }
        public double MoraleBelow20 { get; set; }
        public double Diarrhea { get; set; }
        public double VirusInfected { get; set; }
    }

    /// <summary>v0.6 immunity：免疫力每小时变化（§08）。</summary>
    public sealed class ImmunityConfig
    {
        public double Initial { get; set; }
        public double Min { get; set; }
        public double Max { get; set; } = 100;
        public HealthyConditionsConfig HealthyConditions { get; set; } = new HealthyConditionsConfig();
        public double HealthyBonusPerHour { get; set; }
        public double ShelteredSleepPerHour { get; set; }
        public double ExposedSleepPerHour { get; set; }
        public double ShelteredRestPerHour { get; set; }
        public double ExposedRestPerHour { get; set; }
        public ImmunityPenaltiesConfig PenaltiesPerHour { get; set; } = new ImmunityPenaltiesConfig();
        public string UpdateSource { get; set; } = "";
        public bool OfflineProgress { get; set; }
    }

    public sealed class VirusProgressBand
    {
        public double[] ImmunityRange { get; set; } = new double[0];
        public bool IncludeUpper { get; set; }
        public double Rate { get; set; }

        public bool Contains(double immunity) =>
            ImmunityRange.Length == 2 && immunity >= ImmunityRange[0] && (IncludeUpper ? immunity <= ImmunityRange[1] : immunity < ImmunityRange[1]);
    }

    public sealed class FirstExposureConfig
    {
        public double Load { get; set; }
        public int HighImmunityMinutes { get; set; }
    }

    public sealed class RepeatedExposureConfig
    {
        public double LoadDelta { get; set; }
        public bool ResetHighImmunityMinutes { get; set; }
    }

    public sealed class NaturalCureConfig
    {
        public double ConsecutiveImmunityAtLeast { get; set; }
        public int Minutes { get; set; }
        public double LoadAtMost { get; set; }
        public bool ResetWhenBelowThresholdOrExposedAgain { get; set; }
    }

    public sealed class AntibioticsConfig
    {
        public double InstantLoadReduction { get; set; }
        public double PositiveProgressFactor { get; set; } = 1;
        public int DurationMinutes { get; set; }
        public int UseCooldownMinutes { get; set; }
        public bool RefreshNotStack { get; set; }
        public bool CannotClearInfection { get; set; }
    }

    public sealed class BandageInfectionConfig
    {
        /// <summary>"2*(1+0.01*medicineLevel)"。</summary>
        public string LoadReduction { get; set; } = "";
        public int VirusCooldownMinutes { get; set; }
        public bool DoesNotPreventFutureBiteExposure { get; set; }
        public bool CannotClearInfection { get; set; }

        [JsonIgnore] public double Base => Formula.Num(LoadReduction, 0);
        [JsonIgnore] public double PerLevel => Formula.Num(LoadReduction, 2);
    }

    public sealed class AntiviralCureConfig
    {
        public bool ClearFlag { get; set; }
        public double SetLoad { get; set; }
        public int SetHighImmunityMinutes { get; set; }
        public double RestoreHealth { get; set; }
    }

    /// <summary>v0.6 infection：病毒暴露、进展、治愈与药物（§09–§11）。</summary>
    public sealed class InfectionConfig
    {
        public double[] LoadRange { get; set; } = { 0, 100 };
        public bool InitialInfected { get; set; }
        public double InitialLoad { get; set; }
        public string ExposureSource { get; set; } = "";
        /// <summary>"0.05+0.55*(1-immunity/100)"。</summary>
        public string ExposureProbability { get; set; } = "";
        public FirstExposureConfig OnFirstSuccessfulExposure { get; set; } = new FirstExposureConfig();
        public RepeatedExposureConfig OnRepeatedSuccessfulExposure { get; set; } = new RepeatedExposureConfig();
        public List<VirusProgressBand> ProgressPerHour { get; set; } = new List<VirusProgressBand>();
        public double NaturalLoadMinimumWhileInfected { get; set; }
        public double ZombificationAtLoad { get; set; } = 100;
        public NaturalCureConfig NaturalCure { get; set; } = new NaturalCureConfig();
        public List<string> OnlyCureSources { get; set; } = new List<string>();
        public AntibioticsConfig Antibiotics { get; set; } = new AntibioticsConfig();
        public BandageInfectionConfig Bandage { get; set; } = new BandageInfectionConfig();
        public double AllSuppressantsMinLoad { get; set; }
        public AntiviralCureConfig AntiviralCure { get; set; } = new AntiviralCureConfig();
        public List<double> ProgressWarnings { get; set; } = new List<double>();
        public string GameOverReason { get; set; } = "";
        public List<string> EndpointOrder { get; set; } = new List<string>();
        public bool HighImmunityTimerUsesBothTickEndpoints { get; set; }
        /// <summary>饱饱持续体内病毒输入（R04 §7.2，T09 已批准实装初值）。</summary>
        public BaobaoInputConfig BaobaoInput { get; set; } = new BaobaoInputConfig();

        [JsonIgnore] public double ExposureBase => Formula.Num(ExposureProbability, 0);
        [JsonIgnore] public double ExposureScale => Formula.Num(ExposureProbability, 1);
        [JsonIgnore] public double ExposureImmunityDivisor => Formula.Num(ExposureProbability, 3);
    }

    public sealed class WeightedEnemy
    {
        public string EnemyId { get; set; } = "";
        public double Weight { get; set; }
    }

    /// <summary>v0.6 worldEnemies：持久世界敌人、上限、刷新与巡游（§14、§15）。</summary>
    public sealed class WorldEnemiesConfig
    {
        public int GlobalCap { get; set; }
        public Dictionary<string, int> LocalCaps { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, List<string>> Initial { get; set; } = new Dictionary<string, List<string>>();
        public int SpawnChecksBeginAbsoluteMinute { get; set; }
        public int SpawnCheckIntervalMinutes { get; set; }
        public double SpawnSuccessProbability { get; set; }
        public int SpawnCountPerSuccessfulCheck { get; set; }
        public bool OnlyOutsidePlayerCurrentLocation { get; set; }
        public Dictionary<string, double> RarityWeights { get; set; } = new Dictionary<string, double>();
        public bool RarityRollBeforeLocationEligibility { get; set; }
        public string EmptyRarityLocationPool { get; set; } = "";
        public string EligibleLocationSelection { get; set; } = "";
        public Dictionary<string, Dictionary<string, List<WeightedEnemy>>> Pools { get; set; } = new Dictionary<string, Dictionary<string, List<WeightedEnemy>>>();
        public bool DiscardBlockedChecksNoSpawnDebt { get; set; }
        public bool OfflineSpawns { get; set; }
        public int PatrolCheckIntervalMinutes { get; set; }
        public double PatrolMoveProbabilityPerEnemy { get; set; }
        public int PatrolMaxEdgesPerCheck { get; set; }
        public bool EngagedEnemiesDoNotPatrol { get; set; }
        public bool PatrolEntersCamp { get; set; }
        public string PatrolSource { get; set; } = "";
        public bool DeadDoNotCountInCap { get; set; }
        public bool EngagedAliveCountInCap { get; set; }
        public bool IneligibleEnemiesNeverReplacedByStrongerTier { get; set; }
        public int FleeWithoutValidNeighborHiddenMinutes { get; set; }
        /// <summary>各强度层开放的绝对分钟（R05 §6.1，P06/T07：普通第1日、稀有第3日00:00、强敌第7日00:00）；缺省视为一直开放。</summary>
        public Dictionary<string, int> RarityOpenAbsoluteMinute { get; set; } = new Dictionary<string, int>();

        public bool RarityOpen(string rarity, int minute) => !RarityOpenAbsoluteMinute.TryGetValue(rarity, out var m) || minute >= m;

        public int CapOf(string locationId) => LocalCaps.TryGetValue(locationId, out var c) ? c : 0;
    }

    /// <summary>
    /// 饱饱持续病毒输入 B(I)=maxPerHour×clamp((fullSuppress−I)/bandWidth,0,1)/h（R04 §7.2）：与已感染进展率相加；
    /// 未感染且 B&gt;0 时首次转感染，病毒取 max(最低值, 本段实际增加)。旧档不启用。
    /// </summary>
    public sealed class BaobaoInputConfig
    {
        public double MaxPerHour { get; set; }
        public double FullSuppressImmunityAtLeast { get; set; }
        public double LinearBandWidth { get; set; }
        public double FirstInfectionMinLoad { get; set; }
        public bool NewGamePresent { get; set; }
        public bool LegacySavePresent { get; set; }
        public double SafeTargetLoadAtMost { get; set; }

        public double Rate(double immunity)
        {
            if (LinearBandWidth <= 0) return immunity >= FullSuppressImmunityAtLeast ? 0 : MaxPerHour;
            var f = (FullSuppressImmunityAtLeast - immunity) / LinearBandWidth;
            if (f < 0) f = 0;
            if (f > 1) f = 1;
            return MaxPerHour * f;
        }
    }

    /// <summary>R07 活性物质：普通丧尸独立掉落、首战教学掉落、吞噬融合与教学词条（T02/T08 已批准/已确认）。</summary>
    public sealed class ActiveSubstanceConfig
    {
        public string ItemId { get; set; } = "";
        public SubstanceDropConfig NormalDrop { get; set; } = new SubstanceDropConfig();
        public SubstanceDropConfig TutorialDrop { get; set; } = new SubstanceDropConfig();
        public FusionConfig Fusion { get; set; } = new FusionConfig();
        public TraitEffectConfig TutorialEffect { get; set; } = new TraitEffectConfig();
        public string NormalFusionText { get; set; } = "";
    }

    public sealed class SubstanceDropConfig
    {
        public List<string> Families { get; set; } = new List<string>();
        public double ChancePerEnemy { get; set; }
        public int Count { get; set; } = 1;
    }

    public sealed class FusionConfig
    {
        public int Minutes { get; set; }
        public string RateKey { get; set; } = "wait";
        public double ExtraEnergy { get; set; }
        public bool IdleOnly { get; set; }
    }

    public sealed class TraitEffectConfig
    {
        public string EffectId { get; set; } = "";
        public string Quality { get; set; } = "";
        public string Label { get; set; } = "";
        public double FlatAttack { get; set; }
    }

    /// <summary>N01/S15 剧情：首战位置与敌种、饱饱对白（N01 §3 草案原文）。</summary>
    public sealed class StoryConfig
    {
        public TutorialBattleConfig TutorialBattle { get; set; } = new TutorialBattleConfig();
        public List<DialogueConfig> Dialogues { get; set; } = new List<DialogueConfig>();
    }

    public sealed class TutorialBattleConfig
    {
        public string FromLocationId { get; set; } = "";
        public string LocationId { get; set; } = "";
        public string EnemyId { get; set; } = "";
        public bool NoReinforcement { get; set; }
    }

    public sealed class DialogueConfig
    {
        public string Id { get; set; } = "";
        /// <summary>所属“一次对话”的触发事件 ID（S01 §8）：同事件的台词按配置顺序连成一次对话；缺省为自身 ID。</summary>
        public string EventId { get; set; } = "";
        [JsonIgnore] public string Event => string.IsNullOrEmpty(EventId) ? Id : EventId;
        public string Speaker { get; set; } = "";
        public string Text { get; set; } = "";
        public string Note { get; set; } = "";
    }

    /// <summary>开发测试箱（v0.5 §15）：只经调试入口取得，正常流程不可见。</summary>
    public sealed class DevKitConfig
    {
        public Dictionary<string, int> Items { get; set; } = new Dictionary<string, int>();
    }

    /// <summary>箱子的独立稀有附带物（诊所医药补给箱 1% 病毒特效药）。</summary>
    public sealed class IndependentRareConfig
    {
        public string ItemId { get; set; } = "";
        public int Count { get; set; } = 1;
        public double Chance { get; set; }
    }

    /// <summary>箱子的互斥抽取（废弃冷藏箱：20% 抗病毒前体1 / 80% 草药绷带2）。</summary>
    public sealed class CacheLootEntry
    {
        public string ItemId { get; set; } = "";
        public int Count { get; set; }
        public int Weight { get; set; }
    }
}
