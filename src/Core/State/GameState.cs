using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace Survival.Core.State
{
    public enum StatId { Health, Satiety, Hydration, Energy, Wakefulness, Morale }

    /// <summary>
    /// 角色主行动。Butcher 处理动物尸体（v0.5 §14）；Repair 工具维护（v0.6 §05，走配方）；Bypass 绕行敌踪（v0.6 §15，固定 10 分钟按 travel 消耗）。
    /// </summary>
    public enum ActionType { Travel, Explore, Gather, Craft, Build, Wait, Rest, Sleep, Use, Ignite, Clear, Butcher, Repair, Bypass }

    public enum ActionStatus { Running, Paused }

    public enum PauseReason { None, StatAlert, StatZero, FireOut }

    /// <summary>整局唯一的可序列化状态。内核不持有其他可变数据。</summary>
    public sealed class GameState
    {
        /// <summary>
        /// 存档结构版本。"0.2-proto-1" 是首版（设计文档里的 schemaVersion 2），"3" 是交互与地图扩展版，读档时按链迁移；
        /// "4" 起锅与直烤食材可以真实归属火堆设施（卡片统一与设施烹饪 v0.4）。
        /// </summary>
        public const string CurrentSchema = "4";

        public string SchemaVersion { get; set; } = CurrentSchema;
        public string ConfigVersion { get; set; } = "";
        public int GameMinute { get; set; }
        public uint Seed { get; set; }
        public Rng Rng { get; set; } = new Rng();
        public int NextInstanceId { get; set; } = 1;
        public int NextActionId { get; set; } = 1;
        /// <summary>设施进程编号与放入设施/锅的先后序号（水煮分水按放入顺序，设施与烹饪案 5.2）。</summary>
        public int NextProcessSeq { get; set; } = 1;
        public long SegmentCount { get; set; }

        public Stats Stats { get; set; } = new Stats();
        /// <summary>免疫、病毒、药效与出血（v0.6 §08–§11）。不放进六项 Stats，但走同一显示与 ActiveConditions。</summary>
        public BodyState Body { get; set; } = new BodyState();
        /// <summary>8 项熟练度（制造、医疗、枪械、刀斧、矛、拳、弓箭、钝器），键为技能 ID（v0.6 §03）。</summary>
        public Dictionary<string, SkillState> Skills { get; set; } = new Dictionary<string, SkillState>();
        public int? DiarrheaUntil { get; set; }
        /// <summary>绷带回血冷却结束时刻（v0.6 bandageHealthReadyAt）。</summary>
        public int BandageReadyAt { get; set; }
        public Dictionary<StatId, bool> AlertLatched { get; set; } = new Dictionary<StatId, bool>();
        public Dictionary<StatId, bool> ZeroLatched { get; set; } = new Dictionary<StatId, bool>();

        public string CurrentLocation { get; set; } = "";
        public Dictionary<string, LocationState> Locations { get; set; } = new Dictionary<string, LocationState>();
        public HashSet<string> UnlockedEdges { get; set; } = new HashSet<string>();
        public Dictionary<string, ResourceState> Resources { get; set; } = new Dictionary<string, ResourceState>();
        public List<ItemInstance> Items { get; set; } = new List<ItemInstance>();

        public ActionState? Main { get; set; }
        public ActionState? Sub { get; set; }

        public ProgressState Progress { get; set; } = new ProgressState();
        public List<LogEntry> Log { get; set; } = new List<LogEntry>();

        /// <summary>各区卡片的手工顺序：区为 carried 或地点 ID，值为卡片键（见 GameEngine.CardKey）。不在表中的卡按配置顺序追加尾部。</summary>
        public Dictionary<string, List<string>> Layout { get; set; } = new Dictionary<string, List<string>>();
        /// <summary>最近处理过的请求号（最多 32 条）：重发同一请求直接返回成功且不改状态。</summary>
        public List<string> RecentRequestIds { get; set; } = new List<string>();

        [JsonIgnore] public List<GameEvent> PendingEvents { get; set; } = new List<GameEvent>();

        public GameState Clone()
        {
            var c = (GameState)MemberwiseClone();
            c.Rng = new Rng { State = Rng.State };
            c.Stats = Stats.Clone();
            c.Body = Body.Clone();
            c.Skills = Skills.ToDictionary(kv => kv.Key, kv => kv.Value.Clone());
            c.AlertLatched = new Dictionary<StatId, bool>(AlertLatched);
            c.ZeroLatched = new Dictionary<StatId, bool>(ZeroLatched);
            c.Locations = Locations.ToDictionary(kv => kv.Key, kv => kv.Value.Clone());
            c.UnlockedEdges = new HashSet<string>(UnlockedEdges);
            c.Resources = Resources.ToDictionary(kv => kv.Key, kv => kv.Value.Clone());
            c.Items = Items.Select(i => i.Clone()).ToList();
            c.Main = Main?.Clone();
            c.Sub = Sub?.Clone();
            c.Progress = Progress.Clone();
            c.Log = new List<LogEntry>(Log);
            c.Layout = Layout.ToDictionary(kv => kv.Key, kv => new List<string>(kv.Value));
            c.RecentRequestIds = new List<string>(RecentRequestIds);
            c.PendingEvents = new List<GameEvent>();
            return c;
        }
    }

    /// <summary>
    /// 六项求生状态。v0.6 §02 起上限按项：健康、体力上限存在实例里（初始 20，未来附加系统可改），饱食、水分、清醒、精神仍 0—100。
    /// 内部保存原始 double，不按显示精度回算、不在每段舍入。
    /// </summary>
    public sealed class Stats
    {
        public const double DefaultMax = 100;

        public double Health { get; set; }
        public double Satiety { get; set; }
        public double Hydration { get; set; }
        public double Energy { get; set; }
        public double Wakefulness { get; set; }
        public double Morale { get; set; }
        /// <summary>健康上限（v0.6：20）。</summary>
        public double HealthMax { get; set; } = 20;
        /// <summary>体力上限（v0.6：20）。</summary>
        public double EnergyMax { get; set; } = 20;

        public double Max(StatId id) => id switch
        {
            StatId.Health => HealthMax,
            StatId.Energy => EnergyMax,
            _ => DefaultMax,
        };

        public double Get(StatId id) => id switch
        {
            StatId.Health => Health,
            StatId.Satiety => Satiety,
            StatId.Hydration => Hydration,
            StatId.Energy => Energy,
            StatId.Wakefulness => Wakefulness,
            _ => Morale,
        };

        public void Set(StatId id, double v)
        {
            if (v < 0) v = 0;
            var max = Max(id);
            if (v > max) v = max;
            switch (id)
            {
                case StatId.Health: Health = v; break;
                case StatId.Satiety: Satiety = v; break;
                case StatId.Hydration: Hydration = v; break;
                case StatId.Energy: Energy = v; break;
                case StatId.Wakefulness: Wakefulness = v; break;
                default: Morale = v; break;
            }
        }

        public void Add(StatId id, double delta) => Set(id, Get(id) + delta);

        public Stats Clone() => (Stats)MemberwiseClone();

        public static readonly StatId[] All =
            { StatId.Health, StatId.Satiety, StatId.Hydration, StatId.Energy, StatId.Wakefulness, StatId.Morale };
    }

    /// <summary>
    /// 免疫与病毒（v0.6 §08–§11）、药物冷却与出血。免疫 0—100；病毒 0—100，无感染时恒为 0，已感染时药物与自然减量最低 1。
    /// </summary>
    public sealed class BodyState
    {
        public double Immunity { get; set; } = 70;
        public bool Infected { get; set; }
        public double VirusLoad { get; set; }
        /// <summary>连续高免疫时间（分钟）：段首与段末免疫都≥85 才 +5。</summary>
        public int HighImmunityMinutes { get; set; }
        /// <summary>绷带病毒减量的冷却结束时刻（独立于回血冷却）。</summary>
        public int BandageVirusReadyAt { get; set; }
        /// <summary>抗生素作用截止（段首在有效期内的段，病毒正增长×0.5）；无为 null。</summary>
        public int? AntibioticUntil { get; set; }
        /// <summary>抗生素下次可用时刻（给药间隔）。</summary>
        public int AntibioticReadyAt { get; set; }
        /// <summary>出血剩余次数与每次损血（v0.6 bleedTriggers/bleedDamage）：战内自己激活时扣，战外每个世界 tick 扣。</summary>
        public int BleedTriggers { get; set; }
        public double BleedDamage { get; set; }
        /// <summary>已发过的病毒阶段警告（20/50/80）；病毒回落到阶段以下时清除，可再次提醒。</summary>
        public List<double> VirusWarned { get; set; } = new List<double>();

        public BodyState Clone()
        {
            var c = (BodyState)MemberwiseClone();
            c.VirusWarned = new List<double>(VirusWarned);
            return c;
        }
    }

    public sealed class FacilityState
    {
        public bool ShelterBuilt { get; set; }
        public bool BedBuilt { get; set; }
        public bool FireLit { get; set; }
        public int FuelMinutes { get; set; }
        /// <summary>火堆点燃后熄灭过（燃尽或手动熄灭）。未燃烧时界面据此写“熄灭”而不是“未点燃”；旧存档缺省为否。</summary>
        public bool WentOut { get; set; }
        /// <summary>schema 3 的“锅位关联”（锅仍归属地面）。只供迁移读取，schema 4 起锅的真实归属是 facility:&lt;火堆ID&gt;。</summary>
        public int? AttachedPotId { get; set; }

        /// <summary>火堆设施 ID（如 camp:fire）；没有火堆的地点为空。物品归属写作 facility:&lt;此ID&gt;。</summary>
        public string FacilityId { get; set; } = "";
        /// <summary>火堆加工位：每格是一件真实物品的实例 ID（锅或一份直烤食材），空位为 null。</summary>
        public List<int?> ProcessingSlots { get; set; } = new List<int?>();
        /// <summary>进行中的设施自动加工（烧水、烤制、水煮），每个目标一条。</summary>
        public List<FacilityProcess> Processes { get; set; } = new List<FacilityProcess>();
        /// <summary>最近完成的加工记录（进程号@完成段号），读档后据此不重复发放。</summary>
        public List<string> Completed { get; set; } = new List<string>();

        public FacilityState Clone()
        {
            var c = (FacilityState)MemberwiseClone();
            c.ProcessingSlots = new List<int?>(ProcessingSlots);
            c.Processes = Processes.Select(p => p.Clone()).ToList();
            c.Completed = new List<string>(Completed);
            return c;
        }
    }

    public enum ProcessStatus { Waiting, Heating, Done }

    /// <summary>
    /// 一个设施自动加工目标（设施与烹饪案第 8 节）：烧水以锅为目标（整锅液体批次），烤制与水煮以单份食材为目标。
    /// 受热分钟随世界 5 分钟段推进；熄火保留，取出对象即删除本记录（进度清零、释放水预留）。
    /// </summary>
    public sealed class FacilityProcess
    {
        public string ProcessId { get; set; } = "";
        /// <summary>processes 配置 ID：boil_water / roast_root / boil_root。</summary>
        public string RecipeId { get; set; } = "";
        public int TargetInstanceId { get; set; }
        /// <summary>目标所在的锅（烧水即目标本身；水煮为装食材的锅；直烤为空）。</summary>
        public int? PotInstanceId { get; set; }
        public int HeatDoneMinutes { get; set; }
        public int RequiredHeatMinutes { get; set; }
        /// <summary>水煮为这一份预留的净水（毫升）；完成时扣除，取出食材即释放。</summary>
        public int ReservedWaterMl { get; set; }
        public ProcessStatus Status { get; set; }
        /// <summary>完成时的世界段号（GameState.SegmentCount），与 ProcessId 一起保证只提交一次。</summary>
        public long? CompletionSerial { get; set; }
        /// <summary>最后一次结算到的世界分钟。</summary>
        public int LastWorldMinute { get; set; }

        public FacilityProcess Clone() => (FacilityProcess)MemberwiseClone();
    }

    public sealed class LocationState
    {
        public bool Discovered { get; set; }
        public bool Visited { get; set; }
        public int Progress { get; set; }
        public HashSet<int> TriggeredMilestones { get; set; } = new HashSet<int>();
        public FacilityState Facility { get; set; } = new FacilityState();

        /// <summary>清理剩余次数、当前恢复累计分钟与上次按世界时间更新的分钟（地图清理案 3.1）。</summary>
        public int ClearCharges { get; set; }
        public int ClearRefillProgress { get; set; }
        public int ClearUpdatedMinute { get; set; }

        public LocationState Clone()
        {
            var c = (LocationState)MemberwiseClone();
            c.TriggeredMilestones = new HashSet<int>(TriggeredMilestones);
            c.Facility = Facility.Clone();
            return c;
        }
    }

    public sealed class ResourceState
    {
        /// <summary>可用库存。不限量资源为 null。</summary>
        public int? Available { get; set; }
        public int Reserved { get; set; }
        public int LastRefillDay { get; set; }
        public bool Discovered { get; set; }

        public ResourceState Clone() => (ResourceState)MemberwiseClone();
    }

    public sealed class ItemInstance
    {
        public const string Carried = "carried";
        public const string ContainerPrefix = "pot:";
        public const string FacilityPrefix = "facility:";

        public int InstanceId { get; set; }
        public string ItemId { get; set; } = "";
        public int Count { get; set; }
        public int? ExpiresAt { get; set; }
        public int? Durability { get; set; }
        /// <summary>
        /// 唯一归属：“carried” 随身；“pot:&lt;实例ID&gt;” 在该锅内；“facility:&lt;设施ID&gt;” 在该设施（火堆加工位）内；否则为地点 ID（该地点地面）。
        /// </summary>
        public string Owner { get; set; } = Carried;
        public int? ReservedBy { get; set; }
        /// <summary>放入设施或锅的先后序号；水煮分水先按它，再按实例 ID。离开设施与锅时清空。</summary>
        public int? PlacedOrder { get; set; }

        public bool IsCarried => Owner == Carried;
        public bool InContainer => Owner.StartsWith(ContainerPrefix);
        public bool InFacility => Owner.StartsWith(FacilityPrefix);

        public static string ContainerOwner(int containerInstanceId) => ContainerPrefix + containerInstanceId;
        public static string FacilityOwner(string facilityId) => FacilityPrefix + facilityId;

        public static bool TryFacilityId(string owner, out string facilityId)
        {
            facilityId = "";
            if (!owner.StartsWith(FacilityPrefix) || owner.Length == FacilityPrefix.Length) return false;
            facilityId = owner.Substring(FacilityPrefix.Length);
            return true;
        }

        public static bool TryContainerId(string owner, out int containerInstanceId)
        {
            containerInstanceId = 0;
            return owner.StartsWith(ContainerPrefix) && int.TryParse(owner.Substring(ContainerPrefix.Length), out containerInstanceId);
        }

        public ItemInstance Clone() => (ItemInstance)MemberwiseClone();
    }

    public sealed class ActionState
    {
        public int ActionId { get; set; }
        public ActionType Type { get; set; }
        public bool IsSub { get; set; }
        public ActionStatus Status { get; set; } = ActionStatus.Running;
        public PauseReason PauseReason { get; set; }
        public StatId? PauseStat { get; set; }

        /// <summary>移动目标地点 / 资源点 ID / 配方 ID / 物品 ID。</summary>
        public string TargetId { get; set; } = "";
        public string SourceLocation { get; set; } = "";
        public int StartMinute { get; set; }

        public double BaseWorkMinutes { get; set; }
        public double WorkDone { get; set; }
        public int ElapsedMinutes { get; set; }
        public int PlannedMinutes { get; set; }

        public int ReservedResourceUnits { get; set; }
        public List<int> ReservedItemIds { get; set; } = new List<int>();
        public List<int> LockedItemIds { get; set; } = new List<int>();
        public int? ToolInstanceId { get; set; }
        public bool HazardHit { get; set; }
        /// <summary>移动途中暂停时的子行动按露天结算。</summary>
        public bool ForceOutdoor { get; set; }
        public string Label { get; set; } = "";

        /// <summary>烧水、烹饪所用的锅（开始时锁定）。</summary>
        public int? PotInstanceId { get; set; }
        /// <summary>从 0.2 存档迁移来的烧水：完成仍按旧案在地面生成净水。</summary>
        public bool LegacyGroundOutput { get; set; }
        /// <summary>从 0.2 存档迁移来的探索：完成不生成随机物品。</summary>
        public bool LegacyNoLoot { get; set; }
        /// <summary>玩家显式指定的工具（拖入），失效时不换另一把。</summary>
        public bool ExplicitTool { get; set; }

        /// <summary>清理与探索开始时锁定的隐藏随机结果（00 案 4.1）；完成才公布，读档不重抽。</summary>
        public bool LootPlanned { get; set; }
        public int LootRoll { get; set; }
        public string? LootItemId { get; set; }
        public int LootCount { get; set; }
        public string LootTableVersion { get; set; } = "";
        /// <summary>探索开始时的探索度。</summary>
        public int StartProgress { get; set; }
        /// <summary>清理已在开始时扣除 1 次数（取消、死亡、空结果都不退）。</summary>
        public bool ChargeSpent { get; set; }
        /// <summary>批量制作件数（同一预留批次一次提交）；普通行动为 1。</summary>
        public int BatchCount { get; set; } = 1;

        public bool IsWorkType => IsWork(Type);

        /// <summary>工作动作吃工作倍率与 0 体力工作伤害；清理也是工作（00 案第 3 节）。</summary>
        public static bool IsWork(ActionType t) => t == ActionType.Travel || t == ActionType.Explore || t == ActionType.Gather
                                                   || t == ActionType.Craft || t == ActionType.Build || t == ActionType.Clear
                                                   || t == ActionType.Butcher || t == ActionType.Repair;

        public ActionState Clone()
        {
            var c = (ActionState)MemberwiseClone();
            c.ReservedItemIds = new List<int>(ReservedItemIds);
            c.LockedItemIds = new List<int>(LockedItemIds);
            return c;
        }
    }

    public sealed class DeathInfo
    {
        public string Cause { get; set; } = "";
        public int Minute { get; set; }
        public string LocationId { get; set; } = "";
    }

    public sealed class ProgressState
    {
        public HashSet<string> Flags { get; set; } = new HashSet<string>();
        public bool SevenDayShown { get; set; }
        public bool Dead { get; set; }
        public DeathInfo? Death { get; set; }
        public string LastDamageCause { get; set; } = "";

        public ProgressState Clone()
        {
            var c = (ProgressState)MemberwiseClone();
            c.Flags = new HashSet<string>(Flags);
            return c;
        }
    }

    public sealed class LogEntry
    {
        public int Minute { get; set; }
        public string Kind { get; set; } = "";
        public string Text { get; set; } = "";
    }

    public enum GameEventKind
    {
        ActionStarted, ActionCompleted, ActionCancelled, ActionAborted, ActionPaused,
        Discovery, Hazard, Alert, Death, SevenDay, NewDay, FireOut, Info,
        /// <summary>设施自动加工完成（不暂停、不替换角色主行动）。</summary>
        FacilityDone
    }

    public sealed class GameEvent
    {
        public GameEventKind Kind { get; set; }
        public int Minute { get; set; }
        public string Text { get; set; } = "";
        public StatId? Stat { get; set; }
        public List<string> HighlightIds { get; set; } = new List<string>();
    }

    /// <summary>可序列化的 xorshift32，读档后继续同一序列。</summary>
    public sealed class Rng
    {
        public uint State { get; set; } = 2463534242u;

        public static Rng FromSeed(uint seed) => new Rng { State = seed == 0 ? 0x9E3779B9u : seed };

        public uint NextUInt()
        {
            var x = State;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            State = x;
            return x;
        }

        public double NextDouble() => NextUInt() / 4294967296.0;
    }
}
