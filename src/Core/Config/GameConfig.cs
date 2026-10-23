using System.Collections.Generic;

namespace Survival.Core.Config
{
    /// <summary>data/config.v0.3.json 的强类型映射。数值来源：基础系统初始配置 v0.2.json。</summary>
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
    }

    public sealed class ItemUseConfig
    {
        public double Satiety { get; set; }
        public double Hydration { get; set; }
        public double Morale { get; set; }
        public double Health { get; set; }
        public bool CausesDiarrhea { get; set; }
        public bool IsBandage { get; set; }
    }

    public sealed class ItemConfig
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public int WeightGrams { get; set; }
        public int StackMax { get; set; } = 1;
        public int? ShelfLifeMinutes { get; set; }
        public string Kind { get; set; } = "material";
        public string? ToolTag { get; set; }
        public int? MaxDurability { get; set; }
        public bool Edible { get; set; }
        public bool RequiresCooking { get; set; }
        public ItemUseConfig? Use { get; set; }
        public string Desc { get; set; } = "";

        public bool IsTool => Kind == "tool";
        public bool IsStackable => StackMax > 1 && !IsTool;
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

        public bool IsIndoor => Exposure == "indoor";
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

        public bool IsBuild => Kind == "build";
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
