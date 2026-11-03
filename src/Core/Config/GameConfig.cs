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
        /// <summary>安全屋与野营部署（S09 X2、A07 X1.4/X1.6，B17）。</summary>
        public SafehouseConfig Safehouse { get; set; } = new SafehouseConfig();
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

        // ───── 系统策划总册 v1.0 B4 物理目录与装备数据（A05 §1、§2、§4、§5；开发计划 v0.9 B4） ─────
        /// <summary>防具、战斗饰品、上衣、下衣、求生饰品（34 件）。武器在 Weapons。</summary>
        public List<GearConfig> Gear { get; set; } = new List<GearConfig>();
        /// <summary>18 条附加词条。</summary>
        public List<AffixConfig> Affixes { get; set; } = new List<AffixConfig>();
        public EquipmentRulesConfig Equipment { get; set; } = new EquipmentRulesConfig();

        // ───── 系统策划总册 v1.0 B5 十级熟练度与求生被动（A06 §1–§4、R01 §1/§2/§5；开发计划 v0.9 B5） ─────
        /// <summary>75 个专属技能节点（15 门 × 1/3/5/7/10）。</summary>
        public List<SkillNodeConfig> SkillNodes { get; set; } = new List<SkillNodeConfig>();
        /// <summary>各武器类基准 E（A06 §1：拳.30/刀剑.40/斧.65/……），B6/B7 主动技能 E 成本换算用。</summary>
        public Dictionary<string, double> SkillKindBaseEnergy { get; set; } = new Dictionary<string, double>();
        /// <summary>50 个战斗/医疗主动技能的执行口径（A06 §2；开发计划 v0.9 B7 R25）。</summary>
        public List<SkillExecConfig> SkillExec { get; set; } = new List<SkillExecConfig>();
        public SkillPassiveRulesConfig SkillPassiveRules { get; set; } = new SkillPassiveRulesConfig();

        // ───── 系统策划总册 v1.0 答复（N01/R07/S15） ─────
        public ActiveSubstanceConfig ActiveSubstance { get; set; } = new ActiveSubstanceConfig();
        public StoryConfig Story { get; set; } = new StoryConfig();
        /// <summary>新手序章（开发计划 v0.9 B13，S15「2026-10-30 分类联动本地更新」§3—§8、prologue-data.json、narrative-texts.json）。</summary>
        public PrologueConfig Prologue { get; set; } = new PrologueConfig();
        /// <summary>策划答复 Q53—Q61 成长与行动合同（开发计划 v0.9 B7b）。</summary>
        public ProgressionConfig Progression { get; set; } = new ProgressionConfig();
        /// <summary>人物基础防御档（Q63，combat-adjudications.json actorBaseDefense）：本批只接主角，队友/随机候选在 B8。</summary>
        public ActorBaseDefenseConfig ActorBaseDefense { get; set; } = new ActorBaseDefenseConfig();
        /// <summary>做法知识（Q64 护拳绑带：营区登记默认知识）。知识是集合、不是实物，由存档里的事实给出。</summary>
        public List<KnowledgeConfig> Knowledge { get; set; } = new List<KnowledgeConfig>();
        /// <summary>队伍与共同生存（R08、A11 §5—§7；开发计划 v0.9 B8，tools/import-b8-party.py）。</summary>
        public PartyConfig Party { get; set; } = new PartyConfig();
        /// <summary>工坊六类与装备来源（A07「工坊设施」、A05 §3；开发计划 v0.9 B10，tools/import-b10-workshops.py）。</summary>
        public WorkshopsConfig Workshops { get; set; } = new WorkshopsConfig();
        /// <summary>v3 地图门槛与探索（开发计划 v0.9 B11，tools/import-b11-map.py）。</summary>
        public MapConfig Map { get; set; } = new MapConfig();
        /// <summary>势力事实、营区登记与城市岗位 NPC/服务（开发计划 v0.9 B14，tools/import-b14-city.py）。</summary>
        public CityConfig City { get; set; } = new CityConfig();
        /// <summary>鉴定所鉴定与以物换物（R07 X3、appraisal-service-contract.json，B18）。</summary>
        public AppraisalConfig Appraisal { get; set; } = new AppraisalConfig();
        /// <summary>主线与支线任务（开发计划 v0.9 B15，tools/import-b15-quests.py）。</summary>
        public QuestsConfig Quests { get; set; } = new QuestsConfig();
        /// <summary>地牢远征：终止惩罚包、惊魂未定、撤出时长、预告警告线与入口缓冲（开发计划 v0.9 B21，tools/import-b21-expedition.py）。</summary>
        public DungeonExpeditionConfig DungeonExpedition { get; set; } = new DungeonExpeditionConfig();
        /// <summary>地牢守卫回收材料、首通补给包、72 小时重置冷却与三种战术品（开发计划 v0.9 B22，tools/import-b22-rewards.py）。</summary>
        public DungeonRewardsConfig DungeonRewards { get; set; } = new DungeonRewardsConfig();
        /// <summary>可选地牢知识支线的四个知识包与公开抄录（A11「可选地牢任务与知识」§2—§4；开发计划 v0.9 B23，tools/import-b23-exq.py）。</summary>
        public ExpansionQuestsConfig ExpansionQuests { get; set; } = new ExpansionQuestsConfig();
    }

    /// <summary>B23（R71）：可选地牢知识包。</summary>
    public sealed class ExpansionQuestsConfig
    {
        public string Source { get; set; } = "";
        public List<KnowledgePackConfig> Packs { get; set; } = new List<KnowledgePackConfig>();
    }

    /// <summary>
    /// 一个知识包（A11 §3）：首次固定来源是地牢核心记录（ClaimResourceId 领取事实 claim:… 授予），确定替代来源是登记后岗位处“查抄公开资料”
    /// 固定等待 CopyMinutes、0 材料（写 CopyFact 授予，只补未掌握的，不写任务/通关/容量事实）。知识集合按并集，已知不兑换任何东西。
    /// </summary>
    public sealed class KnowledgePackConfig
    {
        public string Id { get; set; } = "";
        /// <summary>A11 §2 奖励列的说法（“净水/维护知识包”）。</summary>
        public string Label { get; set; } = "";
        public string QuestId { get; set; } = "";
        public List<string> Knowledge { get; set; } = new List<string>();
        public string ClaimResourceId { get; set; } = "";
        public string DungeonId { get; set; } = "";
        public string CopyLocationId { get; set; } = "";
        public string CopyNpcId { get; set; } = "";
        public string CopyServiceId { get; set; } = "";
        public int CopyMinutes { get; set; }
        /// <summary>A11 §3 替代来源的资料名（“公开资料”“公开作业资料”“公共告示资料”“物资登记资料”）。</summary>
        public string CopyLabel { get; set; } = "";
        public string CopyFact { get; set; } = "";
    }

    /// <summary>工坊（A07「工坊设施」；B10）：hand 不是设施；其余每类每地一座，只在 buildLocations 建（B10-1：只开放 camp，待确认 Q107）。</summary>
    public sealed class WorkshopsConfig
    {
        public string Source { get; set; } = "";
        public List<string> BuildLocations { get; set; } = new List<string>();
        public List<StationConfig> Stations { get; set; } = new List<StationConfig>();
        /// <summary>73 件装备的制作与来源（A05 §3；62 件 + Q64 护拳绑带可自制，10 件不可自制）。</summary>
        public List<EquipmentSourceConfig> EquipmentSources { get; set; } = new List<EquipmentSourceConfig>();
    }

    public sealed class StationConfig
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        /// <summary>建造这类工坊的配方 ID；hand 为 null。</summary>
        public string? BuildRecipe { get; set; }
        public string Note { get; set; } = "";
        /// <summary>独立处理位数（晾制架 8，Q170）；没有处理位的工坊为 null。</summary>
        public int? Slots { get; set; }
    }

    public sealed class EquipmentSourceConfig
    {
        public string ItemId { get; set; } = "";
        public bool Craftable { get; set; }
        public string? RecipeId { get; set; }
        public string? Knowledge { get; set; }
        public string Text { get; set; } = "";
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

        // ── B16（A07「X1.2」「X1.6」「二十种食品用品」、R51）：战外护理类消耗品 ──
        /// <summary>体力即时变化（应急恢复剂 E+2）。</summary>
        public double Energy { get; set; }
        /// <summary>清醒即时变化（应急恢复剂 S−2）。</summary>
        public double Wakefulness { get; set; }
        /// <summary>腹泻剩余减少的分钟（止泻药粉 120、口服补液 60）。</summary>
        public int DiarrheaReduceMinutes { get; set; }
        /// <summary>没有腹泻时拒绝（止泻药粉“无腹泻禁用”）。</summary>
        public bool RequiresDiarrhea { get; set; }
        /// <summary>体力已满时拒绝（应急恢复剂“E满禁用”）。</summary>
        public bool BlockWhenEnergyFull { get; set; }
        /// <summary>水分已满且没有腹泻收益时拒绝（口服补液“W满且无腹泻收益拒绝”）。</summary>
        public bool BlockWhenNoHydrationOrDiarrheaGain { get; set; }
        /// <summary>目标冷却组：同组共用一个冷却（止泻药粉与口服补液共用 relief 6 小时；应急恢复剂 tonic 6 小时）。</summary>
        public string? CooldownGroup { get; set; }
        public int CooldownMinutes { get; set; }
        /// <summary>战内禁用（B16 新护理品只做战外部分；战内 2AP 对友方留后续批次）。</summary>
        public bool OutOfBattleOnly { get; set; }

        /// <summary>B16 战外护理品（止泻、补液、应急恢复剂）：按“使用”结算、走护理冷却，不是绷带/抗生素/特效药。</summary>
        public bool IsCare => DiarrheaReduceMinutes > 0 || CooldownGroup != null;
    }

    /// <summary>
    /// 分量水壶（S10 X4、A07 X1.5，B16 R49）：一根物资卡、无嵌套子物；只装同种脏/净水，装入/倒出按 500 毫升整份水卡，
    /// 壶内按 250 毫升一口直接饮用；不受热、不放火堆、不装食材。水量存在实例 LiquidId/LiquidMl 上。
    /// </summary>
    /// <summary>野外修补包品阶（B22b，Q168）：rank 0 白 / 1 绿 / 2 蓝；maxDurability 每包整数耐久上限。</summary>
    public sealed class FieldPatchConfig
    {
        public int Rank { get; set; }
        public int MaxDurability { get; set; } = 100;
    }

    public sealed class CanteenConfig
    {
        public int CapacityMl { get; set; } = 2000;
        /// <summary>直接饮用一口的分量（250 毫升）。</summary>
        public int SipMl { get; set; } = 250;
        /// <summary>装入/倒出水卡的整份（500 毫升）。</summary>
        public int TransferMl { get; set; } = 500;
        public List<string> Accepts { get; set; } = new List<string>();
        /// <summary>一口的水分（W+15）。</summary>
        public double SipHydration { get; set; } = 15;
        /// <summary>每毫升克重（水 1g/ml，满壶 2.40kg）。</summary>
        public double GramsPerMl { get; set; } = 1;
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
        /// <summary>分量水壶（B16）：不是锅容器，水量记在实例上；不是水壶为 null。</summary>
        public CanteenConfig? Canteen { get; set; }
        /// <summary>野外修补包品阶（B22b，Q168“修补包完整定稿”grades）：白/绿/蓝 rank 0/1/2，每包耐久上限 100；不是修补包为 null。</summary>
        public FieldPatchConfig? FieldPatch { get; set; }
        /// <summary>液体每份体积（毫升），可装入容器的物品必须有。</summary>
        public int? UnitVolumeMl { get; set; }
        /// <summary>
        /// 能穿进哪类槽（B4 H2）：weapon（武器Ⅰ/Ⅱ）、armor、combat_acc、top、bottom、survival_acc（代码槽名；A05 combat_charm/survival_charm 由导入脚本映射）。
        /// 不能穿戴为 null。
        /// </summary>
        public string? EquipSlot { get; set; }

        public static readonly string[] EquipSlotKinds = { "weapon", "armor", "combat_acc", "top", "bottom", "survival_acc" };
        public bool IsEquipment => EquipSlot != null;

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

        /// <summary>
        /// 探索到 75% 时的逐点安全线索文案（Q117 选 B，A08 本轮表、map-adjudications.json clues；只 45 个新普通野外有）。
        /// 只是环境提示：不另发物资/经验，不揭示路线、资源或安全属性。没有为 null。
        /// </summary>
        public string? Clue { get; set; }

        // ───── v3 地图字段（A08「全143节点」、map-v3-normalized.json；B11 R33） ─────
        /// <summary>区域：starter（旧 8 地点）/wild/hub/church/academy/d01…d08 等。</summary>
        public string Zone { get; set; } = "starter";
        /// <summary>world / city_building / dungeon。</summary>
        public string NodeKind { get; set; } = "world";
        public string? ParentCity { get; set; }
        public string WorldKind { get; set; } = "overworld";
        public string LandOwner { get; set; } = "unclaimed";
        /// <summary>永久安全屋许可（A08 白名单 54 处；安全屋本身在 B17）。</summary>
        public bool BuildAllowedSafehouse { get; set; }
        /// <summary>临时营地许可（8 处）。</summary>
        public bool TemporaryCamp { get; set; }
        /// <summary>危险度 0—3：只作显示，不乘接敌率、伤害或敌属性（node-expansion-rules contracts.danger）；旧 8 地点没有。</summary>
        public int? Danger { get; set; }
        /// <summary>v3 网格坐标（地牢节点为地牢内局部坐标）。区域图 mapX/mapY 由它换算。</summary>
        public int GridX { get; set; }
        public int GridY { get; set; }

        // ───── 新区探索（S03「新区探索与发现」、node-expansion-rules.json；B11 R35） ─────
        public bool ExploreEnabled { get; set; } = true;
        /// <summary>公共城市/服务节点：首次实际到访自动探索 100，不发里程碑物资、不随机接敌。</summary>
        public bool ExploreAuto100OnVisit { get; set; }
        /// <summary>每次探索基础分钟；null 用全局 actions.exploreMinutes（旧 8 地点）。</summary>
        public int? ExploreMinutes { get; set; }
        /// <summary>每次探索增量；null 用全局 actions.exploreProgress（旧 8 地点）。</summary>
        public int? ExploreIncrement { get; set; }
        public bool ExplorationLootEnabled { get; set; } = true;
        public bool RandomEncounterEnabled { get; set; } = true;
        /// <summary>新节点完成探索/清理后的满人口接敌基础百分比；null 用全局 encounters（旧 8 地点）。</summary>
        public double? EncounterBaseExplorePercent { get; set; }
        public double? EncounterBaseClearPercent { get; set; }
        /// <summary>新节点所属人口区的本地上限（接敌概率分母）；旧 8 地点用 worldEnemies.localCaps。人口区刷新在 B20。</summary>
        public int PopulationLocalCap { get; set; }
        public string? PopulationRegionId { get; set; }
        /// <summary>B20：人口区节点的敌池内部权重（node-expansion-rules worldPopulation.weights；不是刷新概率）；不刷新的节点为 null。</summary>
        public Dictionary<string, int>? PopulationWeights { get; set; }
        /// <summary>
        /// B22h（Q192 选 C，party-combat-expedition-adjudications.json layering.rows）：mainline 四区 21 节点的逐敌层表
        /// ordinary/rare/strong → 敌人 → 层内原权重（层内相对归一化，不当跨层概率）；三层并集等于 PopulationWeights。其余节点为 null（ex_pop 五区不分层）。
        /// </summary>
        public Dictionary<string, Dictionary<string, int>>? PopulationLayers { get; set; }
        /// <summary>B20：首次实际到达时的初始目标只数（多数 2，m02 检查哨 0），同时受人口区剩余上限与本地上限约束。</summary>
        public int FirstPopulation { get; set; }
        /// <summary>地牢节点所属地牢（d01…d08）；非地牢为 null。</summary>
        public string? DungeonId { get; set; }
        /// <summary>外部明示安全（h05_homestead）。</summary>
        public bool SafeExterior { get; set; }

        /// <summary>清理是否开放：新节点清理参数已导入，但资源点与清理在 B12 接入，本批为 false。</summary>
        public bool ClearEnabled { get; set; } = true;
        public int? ClearMinutes { get; set; }
        public string? ClearRefillMode { get; set; }
        /// <summary>清理按钮文字（Q31 clearLabel：野外/地牢“清理翻找”、城市公共“整理搜集”）；旧 8 地点为空，写“清理”。</summary>
        public string? ClearLabel { get; set; }
        /// <summary>地牢房间清理每代 1 次，不随世界分钟恢复（instance_generation_once，S04 Q31 节）。</summary>
        public bool ClearPerGeneration => ClearRefillMode == "instance_generation_once";

        /// <summary>区域图图层：地牢节点按所属地牢分层（局部坐标），其余同在世界图层。</summary>
        public string Layer => NodeKind == "dungeon" ? (DungeonId ?? Zone) : "world";
        public bool IsDungeon => NodeKind == "dungeon";
        /// <summary>旧 8 地点（新手区）。</summary>
        public bool IsStarter => Zone == "starter";

        /// <summary>
        /// 私人住宅禁建原因（Q118、S09「Q118 正式住宅与环境遮蔽」、S14 本轮章）：地牢内部（worldKind=dungeon_instance）写“地牢内不可建设私人住宅”；
        /// 势力实际占地、公共城市服务地、任务保留地写“势力/公共土地不可建设私人住宅”；其余为 null（可否建仍看原许可与条件，住宅本身在 B17）。
        /// </summary>
        public string? PrivateBuildForbidden =>
            IsDungeon || WorldKind == "dungeon_instance" ? "地牢内不可建设私人住宅"
            : LandOwner is "faction" or "public" or "quest_reserved" ? "势力/公共土地不可建设私人住宅"
            : null;
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
        /// <summary>火堆加工位里的一批整料（Q111 火堆煅烧木炭 fire_charcoal：一位一批 wood×4→charcoal×3，非食物、不受热保护、0 经验）。</summary>
        public const string FireSlotBatch = "fire_slot_batch";
        /// <summary>
        /// 复合锅食谱（B16 R50，A07 X1.5、S10 X4：远行硬饼 grain2+盐辅料+500ml、谷肉炖食 grain1+raw_meat1+1000ml）：
        /// 一个进程锁定锅里的多份主食材实例与辅料实例，按锅水预留，完成一次产出多份成品留在原锅。
        /// </summary>
        public const string PotRecipe = "pot_recipe";
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
        /// <summary>整批加工（fire_slot_batch）每批投入的份数（Q111 煅烧：木材4）；其他进程为 1。</summary>
        public int InputCount { get; set; } = 1;
        /// <summary>整批加工每批产出份数（Q111 煅烧：木炭3）；其他进程为 1。</summary>
        public int OutputCount { get; set; } = 1;

        /// <summary>复合锅食谱对应的配方 ID（recipes 里 kind=pot 的条目，用于知识、熟练门槛与显示）。</summary>
        public string? RecipeId { get; set; }
        /// <summary>复合锅食谱的主食材：每份占一个锅食材位（硬饼 grain×2；炖食 grain×1、raw_meat×1）。</summary>
        public Dictionary<string, int> FoodInputs { get; set; } = new Dictionary<string, int>();
        /// <summary>复合锅食谱的辅料：随进程锁进锅里、不占食材位，完成时与主食材一起核销（硬饼 salt×1）。</summary>
        public Dictionary<string, int> AuxInputs { get; set; } = new Dictionary<string, int>();

        public bool IsPotBatch => Target == ProcessTargets.PotLiquidBatch;
        public bool IsPotRecipe => Target == ProcessTargets.PotRecipe;
        /// <summary>火堆加工位整批被动加工（Q111 火堆煅烧）：不是食物，不受热保护、不给经验。</summary>
        public bool IsSlotBatch => Target == ProcessTargets.FireSlotBatch;
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
        /// <summary>旧式解锁条件（任一地点探索度≥X）。v3 起由 <see cref="Gate"/> 取代；只在 Gate 为空时使用。</summary>
        public List<UnlockCondition> UnlockAny { get; set; } = new List<UnlockCondition>();

        // ───── v3 边字段（A08「全180边」、gate-contract.json；B11 R33/R34） ─────
        /// <summary>false 为单向出口（只能从 A 走到 B，8 条地牢出口）。</summary>
        public bool Bidirectional { get; set; } = true;
        /// <summary>规范门槛 ID（config.map.gates）；A 是数据原 from（门槛锚点），不随当前位置改变。</summary>
        public string? Gate { get; set; }
        /// <summary>原标签（from_explore_100 等别名）。</summary>
        public string? GateAlias { get; set; }
        /// <summary>gateContext.edgeRole：normal / hub_registration_boundary。</summary>
        public string Role { get; set; } = "normal";
        public string? DungeonId { get; set; }
        public string? DungeonParent { get; set; }

        public bool Touches(string loc) => A == loc || B == loc;
        public string Other(string loc) => A == loc ? B : A;
        /// <summary>能否从 loc 出发走这条边：双向边两端都可，单向边只从 A。</summary>
        public bool TraversableFrom(string loc) => A == loc || (Bidirectional && B == loc);
    }

    public sealed class ToolBonusConfig
    {
        /// <summary>旧 20 条的工具标签（stone_knife/hatchet，原规则保留）；新表行为空，改看 <see cref="ItemIds"/>。</summary>
        public string ToolTag { get; set; } = "";
        /// <summary>
        /// Q121（S05/A06 本轮章、resource-adjudications.json tools.new96/restored）：新表逐行列出的合格工具物品 ID（stone_knife 石刀、hatchet 简易斧、crowbar 撬棍），
        /// 只认这些具体物品的实例，不按工具类别或附近地面猜。为空时按旧标签。
        /// </summary>
        public List<string> ItemIds { get; set; } = new List<string>();
        public int OutputMultiplier { get; set; } = 1;
        public int? BaseMinutesOverride { get; set; }
        /// <summary>Q121：本行用合格工具搜集时吃搜集 5 级“工具运用”×0.90（表“可用工具运用”列）。</summary>
        public bool ToolUsePassive { get; set; }
        /// <summary>
        /// Q137 选 C（S05/A08 本轮章、fire-axe-adjudications.json）：本行是 30 个“锁闭储物点”之一（定向搜集里从封闭箱柜/检修腔取物的类型标记，
        /// 不是地图门或永久开门账）。只按 resourceId 名单，不凭名字或撬棍推断。
        /// </summary>
        public bool LockedSearch { get; set; }
        /// <summary>
        /// Q137：实际选用某件工具时本行的额外工作量倍率（锁闭点 fire_axe 破门 ×0.75）；与装备、搜集 3/10 级、5 级工具运用相乘后同受 ×0.70 下限，
        /// 位于工具基础分钟覆盖之后、S01 倍率 M 之前。只认实际选中的那件实例，穿着/包里另一把/队友/地面都不生效。
        /// </summary>
        public Dictionary<string, double> ConditionalToolWorkFactors { get; set; } = new Dictionary<string, double>();

        /// <summary>实际选用该物品作工具时本行的条件工作量倍率；没有为 1。</summary>
        public double ConditionalFactor(string? itemId) =>
            itemId != null && ConditionalToolWorkFactors.TryGetValue(itemId, out var f) ? f : 1;

        /// <summary>这件物品（物品 ID 与其工具标签）对本行是否合格。</summary>
        public bool Qualifies(string itemId, string? toolTag) =>
            ItemIds.Count > 0 ? ItemIds.Contains(itemId) : !string.IsNullOrEmpty(ToolTag) && toolTag == ToolTag;
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
        /// <summary>单局永久唯一领取（campaignClaimId，B12 R36）：8 份地牢核心记录，领取写事实、不发物资卡。</summary>
        public const string CampaignClaim = "campaign_claim";
    }

    /// <summary>资源四账的 scope（S04「四账及奖励防重」、S05「定量采集与四生命周期」；B12 R36）。</summary>
    public static class ResourceScopes
    {
        /// <summary>世界日补库存：午夜补到 cap（含预留），离线不补。旧 20 条与 64 条新增野外资源。</summary>
        public const string WorldStock = "world_stock";
        /// <summary>地牢普通箱：按地牢实例代号记账，只在合法新一代恢复（每箱 2/0）。</summary>
        public const string Instance = "instanceId";
        /// <summary>单局永久一次（campaignId:&lt;resourceId&gt;），重置不恢复。</summary>
        public const string Campaign = "campaignClaimId";
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
        /// <summary>
        /// 练哪门熟练（Q55：116 行逐项归属，自然采出 gathering、人工/工业资源与箱 scavenging）。未写时按旧口径（箱子搜集、其余采集）。
        /// </summary>
        public string? ProficiencyId { get; set; }

        // ───── B12 资源四账（R36；S05「唯一资源表与增量边界」、A08 96 条增量表、progression-adjudications resourceRows） ─────
        /// <summary>world_stock / instanceId / campaignClaimId；旧 20 条为 world_stock。</summary>
        public string Scope { get; set; } = ResourceScopes.WorldStock;
        /// <summary>策划数据 kind：renewable / finite / flowing / cistern / world_addition / instance / campaign（旧 20 条为空，只作说明）。</summary>
        public string? Kind { get; set; }
        /// <summary>完成后是否给采集/搜集经验（Q55 resourceRows.xpAllowed：核心记录与城市配给为 false）。</summary>
        public bool XpAllowed { get; set; } = true;
        /// <summary>campaign 领取的记录 ID（church_route_record 等；R03 容量事实直接用它）。</summary>
        public string? RecordId { get; set; }
        /// <summary>campaign 领取同事务写入的门槛事实（gate-contract factProducers：FA02_record_credit 等）。</summary>
        public List<string> ClaimFacts { get; set; } = new List<string>();

        // ───── B13b（Q120/Q121/Q125；resource-adjudications.json additionalSources） ─────
        /// <summary>旧来源别名（m01/crate 等，旧 A08 折叠主表的后缀写法）；只作对账与来源说明，资源 ID 不变。</summary>
        public List<string> LegacyAliases { get; set; } = new List<string>();
        /// <summary>
        /// 揭示与领取资格：有任一事实才显示/可领（工坊首次支援包：FA02_record_credit 或 trusted_legacy_QMAP02_completed）。空为不要求。
        /// </summary>
        public List<string> UnlockAnyFacts { get; set; } = new List<string>();
        /// <summary>唯一领取的实物装备品质（white_zero_affixes_random_template_max_current_full：白色、无词条，最大耐久按模板区间 0.1 步长正常抽一次、当前=最大，Q136 选 A）。</summary>
        public string? EquipmentQuality { get; set; }
        /// <summary>
        /// 揭示与领取条件是本代地牢实例事实（Q122：officer_cache 守卫战利品箱读 d03 当前代 guardianDefeated，B13c）：本代有此事实且到过该地点才显示；
        /// 已发现后换代仍显示，但须本代再有此事实才可领。null 为不要求。
        /// </summary>
        public string? RevealInstanceFact { get; set; }

        public bool IsCampaign => PoolMode == PoolModes.CampaignClaim;
        /// <summary>8 份核心记录（有记录 ID）：领取写事实、不发物资卡；其余唯一领取（支援包、消防斧箱、知识包）没有记录 ID。</summary>
        public bool IsRecord => IsCampaign && !string.IsNullOrEmpty(RecordId);
        /// <summary>唯一领取的实物包（campaign_physical）：一次领取，产物整包落当地地面。</summary>
        public bool IsCampaignPhysical => IsCampaign && !IsRecord && Outputs.Count > 0;
        public bool IsInstance => Scope == ResourceScopes.Instance;
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
        /// <summary>需要的做法知识（knowledge[].id；护拳绑带 blueprint_knuckle_wraps，Q64）；没有知识的配方不列出、不能开工。null 为不需要。</summary>
        public string? Knowledge { get; set; }
        /// <summary>制作所在工坊（A07/A05 §3；B10）：hand 不需工坊；其余须在当前地点已建成该工坊。</summary>
        public string Station { get; set; } = "hand";
        /// <summary>制作列表分组：equipment 装备、material 材料加工、workshop 工坊建造、camp 营地设施；旧配方为 null（物资）。</summary>
        public string? Group { get; set; }
        /// <summary>设施燃料（A05 §3/A07：forge 每批 charcoal1），与原料一起按批预留、完成核销，界面分开显示。</summary>
        public Dictionary<string, int> Fuel { get; set; } = new Dictionary<string, int>();
        /// <summary>
        /// 完成经验归属的熟练门类（Q110 选 B：医疗台 saline/disinfectant/herb_extract 三条材料加工给 medicine）；null 为制造 crafting。
        /// 数额仍按 clamp(ceil(原基础分钟/10),1,12)×批次；开工时快照到 ActionState.XpSkill。
        /// fixed_wait 净水（ex_tablet_water / ex_cartridge_water）为 cooking：按净化每 500ml 给 1、与锅烧水共享日 4（B22a，Q166 选 B），不按分钟。
        /// </summary>
        public string? XpSkill { get; set; }
        /// <summary>
        /// 制作模式（A07 X1.3、S08 X4，B16 R48）：active_work（默认，主动工作量、吃技能/装备加速）、fixed_wait（固定等待，全队等待、不吃加速；xpSkill=cooking 的净水按净化给烹饪经验，Q166）、
        /// passive_heat / passive（设施被动加工，不吃加速；陶窑、火堆锅食谱、净水台）。null 视为 active_work。
        /// </summary>
        public string? Mode { get; set; }
        /// <summary>成品保质规则（S11 X4、R06 X4）：min_inputs = 不晚于原料里最早的截止时间（口粮棒、应急恢复剂）；null 为从完成起按物品保质。</summary>
        public string? ShelfRule { get; set; }
        /// <summary>维护目标范围：field_patch = 野外修补包现场修补（B22b，Q168：金属依据的武器/工具/防具，按包品阶公式扣包耐久；取代 B16 metal_non_gun 10%）；null 为所有有耐久的武器与工具。</summary>
        public string? RepairFilter { get; set; }

        public const string ModeActive = "active_work";
        public const string ModeFixedWait = "fixed_wait";
        public const string ModePassiveHeat = "passive_heat";
        public const string ModePassive = "passive";
        public string ModeOrDefault => string.IsNullOrEmpty(Mode) ? ModeActive : Mode!;
        public bool IsFixedWait => Mode == ModeFixedWait;
        public bool IsPassive => Mode == ModePassiveHeat || Mode == ModePassive;
        /// <summary>火堆锅食谱（B16 硬饼、炖食）：在火堆上锅卡的“锅食谱”里投料，走设施进程，不在制作面板开工。</summary>
        public bool IsPot => Kind == "pot";
        /// <summary>晾制架晾制（Q170，B22c 果干/肉干）：在晾制架卡上选位投料，被动推进，不在制作面板开工。</summary>
        public bool IsDrying => Kind == "drying";

        /// <summary>原料与燃料合计（每批）：校验与预留都按它。</summary>
        public Dictionary<string, int> AllInputs
        {
            get
            {
                if (Fuel.Count == 0) return Inputs;
                var d = new Dictionary<string, int>(Inputs);
                foreach (var kv in Fuel) d[kv.Key] = (d.TryGetValue(kv.Key, out var n) ? n : 0) + kv.Value;
                return d;
            }
        }

        public bool IsBuild => Kind == "build";
        public bool IsRepair => Kind == "repair";
        /// <summary>拆解装备（A05 §5，B4 H9）：须指定目标实例，产出按目标装备的拆解定义，不出现在手工制作列表。</summary>
        public bool IsSalvage => Kind == "salvage";
        public bool NeedsExplicitPot => PotCatalyst == "explicit_empty_detached_pot";
        public bool NeedsFire => Requirement != null && Requirement.Facility == "fire";
    }

    /// <summary>一条做法知识：存档 Progress.Flags 有 grantedByFacts 里任一事实（或直接记下 knowledge:id）即已知。</summary>
    public sealed class KnowledgeConfig
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public List<string> GrantedByFacts { get; set; } = new List<string>();
        public string Source { get; set; } = "";
        /// <summary>开局已会（原石刀/斧/木矛/木弓/金属矛/弩，S08 2026-10-30）。</summary>
        public bool Initial { get; set; }
        /// <summary>蓝图类知识（地图固定箱/任务授予，B11 起；B10 只做显示与开发入口）。</summary>
        public bool Blueprint { get; set; }
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

    /// <summary>
    /// 安全屋（S09 X2.1—X2.6）：建造配方、开工须有的营区登记事实与探索度、每屋固定柜容量；野营布与折叠柴炉部署参数（A07 X1.6、
    /// expansion-supply.json operationalRules.camping）。
    /// </summary>
    public sealed class SafehouseConfig
    {
        public string Source { get; set; } = "";
        public string BuildRecipe { get; set; } = "safehouse_base";
        public string RegisteredFact { get; set; } = "hub_registered";
        public int RequiredExplore { get; set; } = 100;
        public int StorageCapacityGrams { get; set; } = 200000;
        public CampingConfig Camping { get; set; } = new CampingConfig();
    }

    public sealed class CampingConfig
    {
        public string TarpItemId { get; set; } = "field_tarp";
        public int TarpDeployMinutes { get; set; } = 10;
        public int TarpPackMinutes { get; set; } = 10;
        public int TarpPartyCapacity { get; set; } = 4;
        public string StoveItemId { get; set; } = "folding_stove";
        public int StoveDeployMinutes { get; set; } = 5;
        public int StovePackMinutes { get; set; } = 5;
        public int StoveMaxFuelMinutes { get; set; } = 60;
        /// <summary>
        /// Q173 按 B（camping-appraisal-adjudications.json camping.allowedExteriorNodeIds，B22e）：普通主世界野外外部区显式清单（54 处），
        /// 野营遮雨布可铺、野外固定火堆可建；未列出的地点（地牢含 safe 房、城市/势力公共地、任务保留地）一律不行，不按 ID 前缀或图形推定。
        /// 折叠柴炉不看这张表，仍只限 temporaryCamp 8 处。
        /// </summary>
        public List<string> AllowedExteriorNodeIds { get; set; } = new List<string>();
        /// <summary>野外固定火堆的建造配方（wild_fixed_fire：石4木2、制造1、30 基础分钟、手工）。</summary>
        public string WildFireRecipe { get; set; } = "wild_fixed_fire";
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
