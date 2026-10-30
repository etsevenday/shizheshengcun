using System.Collections.Generic;

namespace Survival.Core.Config
{
    /// <summary>data/config.v0.6.json 的强类型映射。数值来源：基础系统初始配置 v0.2.json、交互与地图扩展 v0.3 整合配置、卡片统一与设施烹饪 v0.4 增量配置，
    /// 以及人物与战斗修订参考 v0.6（基线 v0.5）参考配置。</summary>
    public sealed class GameConfig
    {
        public string Version { get; set; } = "";
        public string SourceVersion { get; set; } = "";
        public TimeConfig Time { get; set; } = new TimeConfig();
        public StatBlock InitialStats { get; set; } = new StatBlock();
        public CarryConfig Carry { get; set; } = new CarryConfig();
        public WorkFactorConfig WorkFactors { get; set; } = new WorkFactorConfig();
        public ActionsConfig Actions { get; set; } = new ActionsConfig();
        public Dictionary<string, int> StartingCarried { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, int> StartingCampGround { get; set; } = new Dictionary<string, int>();
        public FireConfig Fire { get; set; } = new FireConfig();
        public List<ItemConfig> Items { get; set; } = new List<ItemConfig>();
        public List<LocationConfig> Locations { get; set; } = new List<LocationConfig>();
        public List<EdgeConfig> Edges { get; set; } = new List<EdgeConfig>();
        public List<ResourceConfig> Resources { get; set; } = new List<ResourceConfig>();
        public List<RecipeConfig> Recipes { get; set; } = new List<RecipeConfig>();
        public Dictionary<string, RateBlock> HourlyRates { get; set; } = new Dictionary<string, RateBlock>();
        public HealthRulesConfig HealthRules { get; set; } = new HealthRulesConfig();
        public Dictionary<string, ThresholdConfig> Thresholds { get; set; } = new Dictionary<string, ThresholdConfig>();
        public ItemActionsConfig ItemActions { get; set; } = new ItemActionsConfig();
        public FacilityEffectsConfig FacilityEffects { get; set; } = new FacilityEffectsConfig();
        public ToolEffectsConfig ToolEffects { get; set; } = new ToolEffectsConfig();
        public ProgressConfig Progress { get; set; } = new ProgressConfig();
        /// <summary>清理与探索共用的地块掉落表（地图清理案 5.1），键为 lootTableId。</summary>
        public Dictionary<string, LootTableConfig> LootTables { get; set; } = new Dictionary<string, LootTableConfig>();
        /// <summary>容器定义（拖拽与容器案 6.1），键为 containerId。</summary>
        public Dictionary<string, ContainerConfig> Containers { get; set; } = new Dictionary<string, ContainerConfig>();
        /// <summary>设施自动加工进程（设施与烹饪案第 5 节）：烧水、烤制、水煮。</summary>
        public List<ProcessConfig> Processes { get; set; } = new List<ProcessConfig>();
        public UiConfig Ui { get; set; } = new UiConfig();

        // ───── 人物与战斗 v0.6（基线 v0.5）；用户 2026-10-26 拍板直接开启 ─────
        /// <summary>战斗、技能、免疫与世界敌人是否启用。策划参考配置为 false，按用户拍板改为 true（不做开关界面）。</summary>
        public bool CombatEnabled { get; set; }
        public StateScaleConfig StateScale { get; set; } = new StateScaleConfig();
        public CombatScopeConfig Scope { get; set; } = new CombatScopeConfig();
        public List<SkillConfig> Skills { get; set; } = new List<SkillConfig>();
        public List<WeaponConfig> Weapons { get; set; } = new List<WeaponConfig>();
        public List<CombatActionConfig> CombatActions { get; set; } = new List<CombatActionConfig>();
        public List<EnemyConfig> Enemies { get; set; } = new List<EnemyConfig>();
        public XpConfig Xp { get; set; } = new XpConfig();
        public CraftingSkillConfig Crafting { get; set; } = new CraftingSkillConfig();
        public CombatMathConfig CombatMath { get; set; } = new CombatMathConfig();
        public PanicConfig Panic { get; set; } = new PanicConfig();
        public RetreatConfig Retreat { get; set; } = new RetreatConfig();
        public AmmoRulesConfig Ammo { get; set; } = new AmmoRulesConfig();
        public WearConfig Wear { get; set; } = new WearConfig();
        public NoiseConfig Noise { get; set; } = new NoiseConfig();
        public StatusesConfig Statuses { get; set; } = new StatusesConfig();
        public BattleTimeConfig BattleTime { get; set; } = new BattleTimeConfig();
        public EncountersConfig Encounters { get; set; } = new EncountersConfig();
        public ZombieLootConfig ZombieLoot { get; set; } = new ZombieLootConfig();
        public List<CarcassConfig> AnimalCarcasses { get; set; } = new List<CarcassConfig>();
        public SkillGrowthConfig SkillGrowth { get; set; } = new SkillGrowthConfig();
        public DurabilityConfig Durability { get; set; } = new DurabilityConfig();
        public ImmunityConfig Immunity { get; set; } = new ImmunityConfig();
        public InfectionConfig Infection { get; set; } = new InfectionConfig();
        public WorldEnemiesConfig WorldEnemies { get; set; } = new WorldEnemiesConfig();
        public DevKitConfig DevKit { get; set; } = new DevKitConfig();

        // ───── 系统策划总册 v1.0 答复（N01/R07/S15） ─────
        public ActiveSubstanceConfig ActiveSubstance { get; set; } = new ActiveSubstanceConfig();
        public StoryConfig Story { get; set; } = new StoryConfig();
    }

    public sealed class TimeConfig
    {
        public int TickMinutes { get; set; } = 5;
        public int StartAbsoluteMinute { get; set; } = 480;
        public int MinutesPerDay { get; set; } = 1440;
        public int DaylightStartMinute { get; set; } = 360;
        public int DaylightEndMinute { get; set; } = 1080;
        public bool OfflineProgress { get; set; }
    }

    public sealed class StatBlock
    {
        public double Health { get; set; }
        public double Satiety { get; set; }
        public double Hydration { get; set; }
        public double Energy { get; set; }
        public double Wakefulness { get; set; }
        public double Morale { get; set; }
    }

    public sealed class RateBlock
    {
        public double Satiety { get; set; }
        public double Hydration { get; set; }
        public double Energy { get; set; }
        public double Wakefulness { get; set; }
        public double Morale { get; set; }

        public RateBlock Copy() => new RateBlock
        {
            Satiety = Satiety, Hydration = Hydration, Energy = Energy, Wakefulness = Wakefulness, Morale = Morale
        };
    }

    public sealed class CarryConfig
    {
        public int SoftGrams { get; set; } = 20000;
        public int HardGrams { get; set; } = 30000;
        /// <summary>随身最多几张根物品卡（S06 §8：同批合法堆叠算一张，独立工具/武器各一张，锅连同内容一张）。</summary>
        public int MaxRootCards { get; set; } = 5;
    }

    public sealed class WorkFactorConfig
    {
        public double Night { get; set; } = 1.5;
        public double Overweight { get; set; } = 1.25;
        public double LowEnergy { get; set; } = 1.25;
        public double Cap { get; set; } = 2.0;
    }

    public sealed class ActionsConfig
    {
        public List<int> WaitMinutes { get; set; } = new List<int>();
        public List<int> RestMinutes { get; set; } = new List<int>();
        public List<int> SleepMinutes { get; set; } = new List<int>();
        public int ExploreMinutes { get; set; } = 30;
        public int ExploreProgress { get; set; } = 25;
        public int ClearMinutes { get; set; } = 20;
        public int IgniteMinutes { get; set; } = 5;
        public int UseMinutes { get; set; } = 5;
        public Dictionary<string, string> RateAliases { get; set; } = new Dictionary<string, string>();
        public List<string> WorkActionTypes { get; set; } = new List<string>();
    }

    public sealed class FireConfig
    {
        public int BurnMinutesPerWood { get; set; } = 60;
        public int IgniteMinutes { get; set; } = 5;
        public int MaxFuelMinutes { get; set; } = 240;
        public int MinFuelToIgnite { get; set; } = 5;
        /// <summary>火堆加工位数：一口锅占1位，每份直烤食材占1位（设施与烹饪案第 2 节）。</summary>
        public int ProcessingSlots { get; set; } = 2;
        /// <summary>火堆最多容纳的锅数。</summary>
        public int MaxPots { get; set; } = 1;
        /// <summary>可以直接放进火堆加工位烤制的食材。</summary>
        public List<string> DirectFoodInputIds { get; set; } = new List<string>();
    }

    public sealed class ItemUseConfig
    {
        public double Satiety { get; set; }
        public double Hydration { get; set; }
        public double Morale { get; set; }
        public double Health { get; set; }
        public bool CausesDiarrhea { get; set; }
        public bool IsBandage { get; set; }
        /// <summary>抗生素（v0.6 §11）：已感染时病毒立即减量，之后一段时间正增长减半。</summary>
        public bool IsAntibiotic { get; set; }
        /// <summary>病毒特效药（v0.6 §10）：直接清除感染。</summary>
        public bool IsAntiviral { get; set; }

        public bool IsMedicine => IsBandage || IsAntibiotic || IsAntiviral;
    }

    public sealed class ItemConfig
    {
        /// <summary>
        /// 物品类别：water 液体、food 食物、material 材料、medical 医疗、tool 工具、weapon 武器、ammo 弹药、carcass 尸体、substance 活性物质、
        /// gear 非武器装备（防具/饰品/衣物，B4）。B4 起 A07/A12 物理目录的新物品只导定义，类别按 A07 所在表归类（开发计划 v0.9 B4 H1）。
        /// </summary>
        public static readonly string[] Kinds = { "water", "food", "material", "medical", "tool", "weapon", "ammo", "carcass", "substance", "gear" };

        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public int WeightGrams { get; set; }
        public int StackMax { get; set; } = 1;
        public int? ShelfLifeMinutes { get; set; }
        public string Kind { get; set; } = "material";
        /// <summary>物品品质（R07 活性物质为 white）；普通物品为空。</summary>
        public string? Quality { get; set; }
        public string? ToolTag { get; set; }
        /// <summary>实例最大耐久的随机区间 [下限, 上限]（v0.6 §05）；无耐久为 null。生成时按 0.1 精度抽一次并存进实例。</summary>
        public double[]? DurabilityMaxRange { get; set; }
        /// <summary>旧档（schema 4 及以前）里该物品的固定满耐久，只供一次性迁移保持成色比例。</summary>
        public int? LegacyMaxDurability { get; set; }
        public bool Edible { get; set; }
        public bool RequiresCooking { get; set; }
        public ItemUseConfig? Use { get; set; }
        public string Desc { get; set; } = "";
        /// <summary>该物品本身是容器时指向 containers 的键（金属锅）。</summary>
        public string? ContainerId { get; set; }
        /// <summary>液体每份体积（毫升），可装入容器的物品必须有。</summary>
        public int? UnitVolumeMl { get; set; }

        public bool IsTool => Kind == "tool";
        public bool IsWeaponKind => Kind == "weapon";
        public bool HasDurability => DurabilityMaxRange != null && DurabilityMaxRange.Length == 2;
        public bool IsStackable => StackMax > 1 && !IsTool && !IsWeaponKind;
    }

    public sealed class LocationConfig
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Exposure { get; set; } = "outdoor";
        public int InitialProgress { get; set; }
        public bool InitiallyDiscovered { get; set; }
        public bool InitiallyVisited { get; set; }
        public bool HasFire { get; set; }
        public bool CanBuildShelter { get; set; }
        public double MapX { get; set; }
        public double MapY { get; set; }

        /// <summary>地点行背景场景图（相对网页根目录）。换美术图时改这里即可，图片按 1200×650、居中裁切显示。</summary>
        public string? Scene { get; set; }
        /// <summary>场景图里火堆中心的坐标（1200×650 坐标系），用于叠加火焰与火光；没有火堆则为空。</summary>
        public double? SceneFireX { get; set; }
        public double? SceneFireY { get; set; }

        /// <summary>清理与探索共用的掉落表 ID。</summary>
        public string LootTableId { get; set; } = "";
        public ClearConfig Clear { get; set; } = new ClearConfig();

        public bool IsIndoor => Exposure == "indoor";
    }

    /// <summary>地点清理次数：上限、初始与串行恢复间隔（地图清理案表 2）。</summary>
    public sealed class ClearConfig
    {
        public int Cap { get; set; }
        public int InitialCharges { get; set; }
        public int RefillIntervalMinutes { get; set; }
    }

    public sealed class LootEntryConfig
    {
        /// <summary>null 表示未获得物资。</summary>
        public string? ItemId { get; set; }
        public int Count { get; set; }
        public int Weight { get; set; }
    }

    /// <summary>一张掉落表：按条目顺序构建累积区间，从 [0,100) 抽一个整数命中唯一结果。</summary>
    public sealed class LootTableConfig
    {
        public string Version { get; set; } = "";
        public List<LootEntryConfig> Entries { get; set; } = new List<LootEntryConfig>();
    }

    public sealed class ContainerConfig
    {
        public int CapacityMl { get; set; }
        public int UnitMl { get; set; }
        public List<string> Accepts { get; set; } = new List<string>();
        /// <summary>食材位数（与液体容量是两个维度）。</summary>
        public int FoodSlots { get; set; }
        /// <summary>可以存放在锅里的食材（生块根及在本锅完成的水煮块根）。</summary>
        public List<string> StoredFoodIds { get; set; } = new List<string>();
        /// <summary>只存放、不再加工的食物（v1.0 Q2：水煮肉可从外部放回锅）。</summary>
        public List<string> StoreOnlyFoodIds { get; set; } = new List<string>();

        public int CapacityUnits => UnitMl > 0 ? CapacityMl / UnitMl : 0;
    }

    public static class ProcessTargets
    {
        public const string PotLiquidBatch = "pot_liquid_batch";
        public const string SingleFood = "single_food_instance";
    }

    public static class ProcessOwners
    {
        public const string FireSlot = "fire_processing_slot";
        public const string PotInFire = "pot_inside_lit_fire";
    }

    /// <summary>一种设施自动加工：按有效受热分钟推进，不占角色主行动（设施与烹饪案第 4、5 节）。</summary>
    public sealed class ProcessConfig
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Target { get; set; } = "";
        public string InputId { get; set; } = "";
        public string OutputId { get; set; } = "";
        public List<int> AllowedBatchMl { get; set; } = new List<int>();
        public int HeatMinutes { get; set; }
        /// <summary>每份完成时消耗的净水（水煮 500 毫升）。</summary>
        public int WaterCostMl { get; set; }
        public string RequiredOwner { get; set; } = "";
        public string? RequiredLiquid { get; set; }

        public bool IsPotBatch => Target == ProcessTargets.PotLiquidBatch;
        public bool InPot => RequiredOwner == ProcessOwners.PotInFire;
    }

    public sealed class UiConfig
    {
        public int DragThresholdPx { get; set; } = 6;
    }

    public sealed class UnlockCondition
    {
        public string Location { get; set; } = "";
        public int Progress { get; set; }
    }

    public sealed class EdgeConfig
    {
        public string Id { get; set; } = "";
        public string A { get; set; } = "";
        public string B { get; set; } = "";
        public int BaseMinutes { get; set; }
        public bool InitiallyUnlocked { get; set; }
        public List<UnlockCondition> UnlockAny { get; set; } = new List<UnlockCondition>();

        public bool Touches(string loc) => A == loc || B == loc;
        public string Other(string loc) => A == loc ? B : A;
    }

    public sealed class ToolBonusConfig
    {
        public string ToolTag { get; set; } = "";
        public int OutputMultiplier { get; set; } = 1;
        public int? BaseMinutesOverride { get; set; }
    }

    public sealed class HazardConfig
    {
        public double Probability { get; set; }
        public double HealthLoss { get; set; }
        public string Label { get; set; } = "受伤";
    }

    public static class PoolModes
    {
        public const string Renewable = "renewable_units";
        public const string Finite = "finite_units";
        public const string SingleCache = "single_cache";
        public const string Unlimited = "unlimited";
    }

    public sealed class ResourceConfig
    {
        public string NodeId { get; set; } = "";
        public string Name { get; set; } = "";
        public string LocationId { get; set; } = "";
        public int? RevealAt { get; set; }
        public bool RevealOnVisit { get; set; }
        public int BaseMinutes { get; set; }
        public int? Cap { get; set; }
        public int DailyRefill { get; set; }
        public string PoolMode { get; set; } = PoolModes.Renewable;
        public Dictionary<string, int> Outputs { get; set; } = new Dictionary<string, int>();
        public ToolBonusConfig? ToolBonus { get; set; }
        public HazardConfig? Hazard { get; set; }
        /// <summary>拾荒箱子：产出的工具/武器按拾荒成色生成当前耐久（v0.6 §05 scavengedConditionOptional）。</summary>
        public bool Scavenged { get; set; }
        /// <summary>独立稀有附带物（诊所医药补给箱 1% 病毒特效药），开始搜集时锁定。</summary>
        public IndependentRareConfig? IndependentRare { get; set; }
        /// <summary>互斥抽取（废弃冷藏箱），开始搜集时锁定；与 Outputs 二选一。</summary>
        public List<CacheLootEntry>? ExclusiveLoot { get; set; }

        public bool IsCache => PoolMode == PoolModes.SingleCache;
        public bool IsUnlimited => PoolMode == PoolModes.Unlimited;
        public bool IsUnits => PoolMode == PoolModes.Renewable || PoolMode == PoolModes.Finite;
        public bool Refills => PoolMode == PoolModes.Renewable && DailyRefill > 0;
    }

    public sealed class RecipeRequirement
    {
        /// <summary>fire：当前地点火堆燃烧；camp：位于营地；shelter：营地遮蔽棚已建。</summary>
        public string Facility { get; set; } = "";
    }

    public sealed class RecipeConfig
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Kind { get; set; } = "craft";
        public int BaseMinutes { get; set; }
        public Dictionary<string, int> Inputs { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, int> Outputs { get; set; } = new Dictionary<string, int>();
        public List<string> Catalysts { get; set; } = new List<string>();
        public RecipeRequirement? Requirement { get; set; }
        public string? FacilityResult { get; set; }
        /// <summary>技能门槛（病毒特效药：医疗≥15、制造≥12）。</summary>
        public Dictionary<string, int> MinSkills { get; set; } = new Dictionary<string, int>();
        /// <summary>explicit_empty_detached_pot：须明确指定一口取下并清空的锅作为不消耗催化工具。</summary>
        public string? PotCatalyst { get; set; }
        public bool AutoFillPot { get; set; }
        /// <summary>工具维护：须指定目标实例；恢复其自身最大耐久的比例，不重抽最大值。</summary>
        public bool TargetInstanceRequired { get; set; }
        public double RestoreOwnDurabilityMaxFraction { get; set; }
        public bool RerollDurability { get; set; }
        public bool RequireEmptyDetachedPotWhenTargetIsPot { get; set; }

        public bool IsBuild => Kind == "build";
        public bool IsRepair => Kind == "repair";
        public bool NeedsExplicitPot => PotCatalyst == "explicit_empty_detached_pot";
        public bool NeedsFire => Requirement != null && Requirement.Facility == "fire";
    }

    public sealed class HealthRulesConfig
    {
        public double ZeroSatietyPerHour { get; set; }
        public double ZeroHydrationPerHour { get; set; }
        public double ZeroWakefulnessPerHour { get; set; }
        public double ZeroEnergyWhileWorkingPerHour { get; set; }
        public double ShelteredRestRecoveryPerHour { get; set; }
        public double RecoveryMinSatiety { get; set; }
        public double RecoveryMinHydration { get; set; }
        public double RecoveryMinMorale { get; set; }
    }

    public sealed class ThresholdConfig
    {
        public double Value { get; set; }
        public bool Inclusive { get; set; }

        public bool IsBelow(double v) => Inclusive ? v <= Value : v < Value;
    }

    public sealed class ItemActionsConfig
    {
        public int UseMinutes { get; set; } = 5;
        public int BandageCooldownMinutes { get; set; } = 60;
        public int DiarrheaMinutes { get; set; } = 720;
        public double DiarrheaExtraHydrationPerHour { get; set; } = -3;
    }

    public sealed class FacilityEffectsConfig
    {
        public double BedExtraSleepEnergyPerHour { get; set; }
        public double ShelteredCampRestMoralePerHour { get; set; }
        public double LowMoraleEnergyRecoveryFactor { get; set; } = 0.5;
    }

    public sealed class ToolEffectsConfig
    {
        public int SuccessfulActionWear { get; set; } = 1;
    }

    public sealed class GuideFlagConfig
    {
        public string Id { get; set; } = "";
        public string Label { get; set; } = "";
    }

    public sealed class GuideConfig
    {
        public int Day { get; set; }
        public string Title { get; set; } = "";
        public List<GuideFlagConfig> Flags { get; set; } = new List<GuideFlagConfig>();
    }

    public sealed class ProgressConfig
    {
        public int SevenDayCheckMinute { get; set; } = 10560;
        public List<GuideConfig> Guides { get; set; } = new List<GuideConfig>();
        public List<GuideFlagConfig> SevenDayItems { get; set; } = new List<GuideFlagConfig>();
        /// <summary>左栏“生存提示”的预设文案，只复述已有规则，不引入新数值。</summary>
        public List<string> Tips { get; set; } = new List<string>();
    }
}
