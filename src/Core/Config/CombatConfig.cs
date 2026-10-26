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
        public List<string> Slots { get; set; } = new List<string>();
        public int HumanApPerActivation { get; set; } = 2;
        public int EnemyActionsPerActivation { get; set; } = 1;
        public double BasePlayerSpeed { get; set; }
        public double BasePlayerDodge { get; set; }
        public double BasePlayerArmor { get; set; }
        public bool HealthUsesExistingSurvivalHealth { get; set; }
    }

    public sealed class SkillConfig
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public int InitialLevel { get; set; } = 1;
        /// <summary>"50*(L+1)"。</summary>
        public string NextLevelXpFormula { get; set; } = "";
        public int? LevelCap { get; set; }

        [JsonIgnore] public double XpBase => Formula.Num(NextLevelXpFormula, 0);
        [JsonIgnore] public double XpOffset => Formula.Num(NextLevelXpFormula, 1);
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
        public bool GroupAttackXpIsSingleCommand { get; set; }
        public int CancelOrPreviewXp { get; set; }
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

        [JsonIgnore] public double PerLevel => Formula.Num(WorkFactor, 2);
    }

    public sealed class WeaponConfig
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string SkillId { get; set; } = "";
        public double DamageMin { get; set; }
        public double DamageMax { get; set; }
        public double EnergyCost { get; set; }
        public int WeightGrams { get; set; }
        /// <summary>front / all / all_rear_damage_0.75。</summary>
        public string Targets { get; set; } = "front";
        public string? AmmoId { get; set; }
        public int MagazineCapacity { get; set; }
        public int BasicApCost { get; set; } = 1;
        public double AccuracyRating { get; set; }
        public double CritRatingBase { get; set; }
        public double[]? DurabilityMaxRange { get; set; }

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

        [JsonIgnore] public bool IsZombie => Family == "zombie";
        [JsonIgnore] public bool NeutralUntilAttacked => Behavior == "neutral_until_attacked";
    }

    /// <summary>v0.6 combatMath：命中、暴击、伤害、身体修正（§04）。</summary>
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
        public double LowEnergyAccuracyFactor { get; set; } = 1;
        public double LowEnergySpeedPenalty { get; set; }
        public double LowWakefulnessInclusive { get; set; }
        public double LowWakefulnessAccuracyFactor { get; set; } = 1;
        public double LowWakefulnessSpeedPenalty { get; set; }
        public double StaggerAccuracyFactor { get; set; } = 1;
        public bool GuardStacks { get; set; }
        public bool AimRemoved { get; set; }

        [JsonIgnore] public double MinEffectiveSpeed => Formula.Num(Initiative, 0);
        [JsonIgnore] public double SlowSpeedMultiplier => Formula.Num(Initiative, 1);
        [JsonIgnore] public int InitiativeRollMin => (int)Formula.Num(Initiative, 2);
        [JsonIgnore] public int InitiativeRollMax => (int)Formula.Num(Initiative, 3);
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
        public double SurpriseBonus { get; set; }
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

        [JsonIgnore] public double PotWear => Tools.TryGetValue("pot", out var t) ? t.WearPerSuccessfulThermalProcess : 0;
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

        public int CapOf(string locationId) => LocalCaps.TryGetValue(locationId, out var c) ? c : 0;
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
