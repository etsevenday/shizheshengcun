using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace Survival.Core.State
{
    public enum StatId { Health, Satiety, Hydration, Energy, Wakefulness, Morale }

    public enum ActionType { Travel, Explore, Gather, Craft, Build, Wait, Rest, Sleep, Use, Ignite }

    public enum ActionStatus { Running, Paused }

    public enum PauseReason { None, StatAlert, StatZero, FireOut }

    /// <summary>整局唯一的可序列化状态。内核不持有其他可变数据。</summary>
    public sealed class GameState
    {
        /// <summary>存档结构版本。"0.2-proto-1" 是首版（设计文档里的 schemaVersion 2），读档时迁移。</summary>
        public const string CurrentSchema = "3";

        public string SchemaVersion { get; set; } = CurrentSchema;
        public string ConfigVersion { get; set; } = "";
        public int GameMinute { get; set; }
        public uint Seed { get; set; }
        public Rng Rng { get; set; } = new Rng();
        public int NextInstanceId { get; set; } = 1;
        public int NextActionId { get; set; } = 1;
        public long SegmentCount { get; set; }

        public Stats Stats { get; set; } = new Stats();
        public int? DiarrheaUntil { get; set; }
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

        [JsonIgnore] public List<GameEvent> PendingEvents { get; set; } = new List<GameEvent>();

        public GameState Clone()
        {
            var c = (GameState)MemberwiseClone();
            c.Rng = new Rng { State = Rng.State };
            c.Stats = Stats.Clone();
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
            c.PendingEvents = new List<GameEvent>();
            return c;
        }
    }

    public sealed class Stats
    {
        public double Health { get; set; }
        public double Satiety { get; set; }
        public double Hydration { get; set; }
        public double Energy { get; set; }
        public double Wakefulness { get; set; }
        public double Morale { get; set; }

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
            if (v > 100) v = 100;
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

    public sealed class FacilityState
    {
        public bool ShelterBuilt { get; set; }
        public bool BedBuilt { get; set; }
        public bool FireLit { get; set; }
        public int FuelMinutes { get; set; }
        /// <summary>架在火堆锅位上的锅实例。只是关联，锅本身仍归属营地地面。</summary>
        public int? AttachedPotId { get; set; }

        public FacilityState Clone() => (FacilityState)MemberwiseClone();
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

        public int InstanceId { get; set; }
        public string ItemId { get; set; } = "";
        public int Count { get; set; }
        public int? ExpiresAt { get; set; }
        public int? Durability { get; set; }
        /// <summary>"carried" 表示随身，"pot:&lt;实例ID&gt;" 表示在该锅内，否则为地点 ID（该地点地面）。</summary>
        public string Owner { get; set; } = Carried;
        public int? ReservedBy { get; set; }

        public bool IsCarried => Owner == Carried;
        public bool InContainer => Owner.StartsWith(ContainerPrefix);

        public static string ContainerOwner(int containerInstanceId) => ContainerPrefix + containerInstanceId;

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

        public bool IsWorkType => Type == ActionType.Travel || Type == ActionType.Explore || Type == ActionType.Gather
                                  || Type == ActionType.Craft || Type == ActionType.Build;

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
        Discovery, Hazard, Alert, Death, SevenDay, NewDay, FireOut, Info
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
