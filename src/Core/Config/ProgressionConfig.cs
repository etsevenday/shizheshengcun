using System.Collections.Generic;

namespace Survival.Core.Config
{
    /// <summary>
    /// 策划答复 Q53—Q61 的成长与行动合同（A06/S01/S05/S08/S11 本轮章「2026-10-31 Q53—Q61成长与行动裁定（当前优先）」，
    /// 数据同 assets/20261031/progression/progression-adjudications.json；开发计划 v0.9 批次 B7b）。由 tools/import-b7b-progression.py 写入。
    /// </summary>
    public sealed class ProgressionConfig
    {
        public string Source { get; set; } = "";
        public DismantleRulesConfig Dismantle { get; set; } = new DismantleRulesConfig();
        public FieldXpConfig FieldXp { get; set; } = new FieldXpConfig();
        public SurveyConfig Survey { get; set; } = new SurveyConfig();
        public CookingXpConfig Cooking { get; set; } = new CookingXpConfig();
        public WorkCarryConfig WorkCarry { get; set; } = new WorkCarryConfig();
    }

    /// <summary>Q54：拆解归制造。制造等级达到 CraftingLevel 可拆阶 ≤MaxTier 的装备；每件完成 +Xp 制造经验；制造 ExpertCraftingLevel 级回收专家（同材料追加 ExpertBonus 份，受 80% 预算）。</summary>
    public sealed class DismantleRulesConfig
    {
        public List<DismantleStageConfig> Stages { get; set; } = new List<DismantleStageConfig>();
        public int Xp { get; set; }
        public int ExpertCraftingLevel { get; set; }
        public int ExpertBonus { get; set; }
    }

    public sealed class DismantleStageConfig
    {
        public int CraftingLevel { get; set; }
        public int MaxTier { get; set; }
    }

    /// <summary>Q55：采集/搜集完成经验 clamp(StepXp×ceil(原基础分钟/StepMinutes),Min,Max)；每角色每资源点每日前 PerResourceDaily 次、每角色每门每日 DailyPerActorSkill 上限。</summary>
    public sealed class FieldXpConfig
    {
        public string FormulaText { get; set; } = "";
        public int StepXp { get; set; }
        public int StepMinutes { get; set; }
        public int Min { get; set; }
        public int Max { get; set; }
        public int PerResourceDaily { get; set; }
        public int DailyPerActorSkill { get; set; }
    }

    /// <summary>Q55：四个旧遗迹的“遗迹勘整”训练动作（不产物、不消耗库存、不恢复箱）。</summary>
    public sealed class SurveyConfig
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public List<string> NodeIds { get; set; } = new List<string>();
        public int BaseMinutes { get; set; }
        public int Xp { get; set; }
        public string ProficiencyId { get; set; } = "";
        public int DailyPerActorNode { get; set; }
        public int RequiresExplorePercent { get; set; }
        public Dictionary<string, HazardConfig> HazardByNode { get; set; } = new Dictionary<string, HazardConfig>();
    }

    /// <summary>Q60：现有四种食品进程与净化的烹饪经验。</summary>
    public sealed class CookingXpConfig
    {
        public List<string> FoodProcesses { get; set; } = new List<string>();
        public int FoodHeatStepMinutes { get; set; }
        public int FoodMin { get; set; }
        public int FoodMax { get; set; }
        public int FoodDailyCap { get; set; }
        public string WaterProcess { get; set; } = "";
        public int WaterMlPerXp { get; set; }
        public int WaterDailyCap { get; set; }
    }

    /// <summary>Q61：同角色同动作族的基础工作分钟余额（移动按同行角色集合）。</summary>
    public sealed class WorkCarryConfig
    {
        public double MaxCredit { get; set; }
        public int RoundingDecimals { get; set; }
    }
}
