using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace Survival.Core.State
{
    public enum StatId { Health, Satiety, Hydration, Energy, Wakefulness, Morale }

    /// <summary>
    /// 角色主行动。Butcher 处理动物尸体（v0.5 §14）；Repair 工具维护（v0.6 §05，走配方）；Bypass 绕行敌踪（v0.6 §15，固定 10 分钟按 travel 消耗）；Fuse 饱饱吞噬融合活性物质（R07 §4，固定 5 分钟按 wait 消耗）；
    /// WaitCook 等待本火堆当前全部可加工内容完成（S11 §9、S13 §5，按 wait 消耗，时长由目标动态决定）。
    /// </summary>
    public enum ActionType { Travel, Explore, Gather, Craft, Build, Wait, Rest, Sleep, Use, Ignite, Clear, Butcher, Repair, Bypass, Fuse, WaitCook }

    public enum ActionStatus { Running, Paused }

    public enum PauseReason { None, StatAlert, StatZero, FireOut }

    /// <summary>整局唯一的可序列化状态。内核不持有其他可变数据。</summary>
    public sealed class GameState
    {
        /// <summary>
        /// 存档结构版本。"0.2-proto-1" 是首版（设计文档里的 schemaVersion 2），"3" 是交互与地图扩展版，读档时按链迁移；
        /// "4" 起锅与直烤食材可以真实归属火堆设施（卡片统一与设施烹饪 v0.4）；
        /// "5" 起健康/体力上限20、随机耐久、技能、免疫病毒、世界敌人与战斗（人物与战斗 v0.6，程序选的未占用版本号，与策划文件名无关）；
        /// "6" 起金属锅无耐久、活性物质来源、人物词条、剧情与饱饱状态（系统策划总册 v1.0 答复）；
        /// "7" 起对话记录（按次分组、逐句快照、游标、待显示队列）与等待烹饪行动状态（策划提交 9fdb44f：S01 §8–9、S11 §9）；
        /// "8" 起设施加工进程带烹饪受热保护标记（策划提交 5493951：Q16、S11 §10），随身最多五张根物品卡（S06 §8）；
        /// "9" 起角色个人状态按 actorId 存在 Actors（主角 "p0"，R08 §2/§5、开发计划 v0.9 B3），装备改为穿戴 owner（七槽，S06 全系统更新目标 §1）。
        /// </summary>
        public const string CurrentSchema = "9";

        /// <summary>主角的 actorId（R08 §5：旧档主角创建 actorId 一次）。</summary>
        public const string PlayerId = "p0";

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

        /// <summary>
        /// 全部角色的个人状态，键为 actorId（schema 9，R08 §2/§5）。本批只有主角 "p0"；队友加入在后续批次。
        /// </summary>
        public Dictionary<string, ActorState> Actors { get; set; } = new Dictionary<string, ActorState> { [PlayerId] = new ActorState { Id = PlayerId } };
        /// <summary>当前同行队伍的 actorId（主角在首位）。世界 tick 对其中每人各结算一次身体，同一个 5 分钟只推进一次世界（R08 §2、COMP04）。</summary>
        public List<string> Party { get; set; } = new List<string> { PlayerId };

        /// <summary>按 actorId 取角色；不存在时建空角色（只在迁移与测试注入时发生）。</summary>
        public ActorState Actor(string actorId)
        {
            if (!Actors.TryGetValue(actorId, out var a))
            {
                a = new ActorState { Id = actorId };
                Actors[actorId] = a;
            }
            return a;
        }

        /// <summary>主角（p0）。</summary>
        [JsonIgnore] public ActorState Player => Actor(PlayerId);

        /// <summary>同行队伍里的全部角色（主角在前），按 Party 顺序；缺失的 actorId 跳过。</summary>
        [JsonIgnore] public IEnumerable<ActorState> PartyActors => Party.Where(Actors.ContainsKey).Select(id => Actors[id]);

        // 以下为主角个人状态的转发（schema 9 起存在 Actors["p0"]；旧档读入时由 SaveSerializer 预处理搬入），不单独序列化。
        [JsonIgnore] public Stats Stats { get => Player.Stats; set => Player.Stats = value; }
        /// <summary>免疫、病毒、药效与出血（v0.6 §08–§11）。不放进六项 Stats，但走同一显示与 ActiveConditions。</summary>
        [JsonIgnore] public BodyState Body { get => Player.Body; set => Player.Body = value; }
        /// <summary>熟练度，键为技能 ID（v0.6 §03）。</summary>
        [JsonIgnore] public Dictionary<string, SkillState> Skills { get => Player.Skills; set => Player.Skills = value; }
        [JsonIgnore] public int? DiarrheaUntil { get => Player.DiarrheaUntil; set => Player.DiarrheaUntil = value; }
        /// <summary>绷带回血冷却结束时刻（v0.6 bandageHealthReadyAt）。</summary>
        [JsonIgnore] public int BandageReadyAt { get => Player.BandageReadyAt; set => Player.BandageReadyAt = value; }
        [JsonIgnore] public Dictionary<StatId, bool> AlertLatched { get => Player.AlertLatched; set => Player.AlertLatched = value; }
        [JsonIgnore] public Dictionary<StatId, bool> ZeroLatched { get => Player.ZeroLatched; set => Player.ZeroLatched = value; }

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
        /// <summary>最近处理过的请求号：重发同一请求直接返回成功且不改状态（v0.6 起战斗指令较多，保留最近 128 条）。</summary>
        public List<string> RecentRequestIds { get; set; } = new List<string>();

        // ───── 世界敌人与接敌（v0.6 §14、§15） ─────
        /// <summary>持久世界敌人实例（只存活的；死亡即移除，尸体与掉落另行结算）。</summary>
        public List<WorldEnemy> WorldEnemies { get; set; } = new List<WorldEnemy>();
        public int NextWorldEnemyId { get; set; } = 1;
        /// <summary>下次全局刷新检查的绝对分钟（第2日00:00起每120分钟）。</summary>
        public int SpawnNextCheck { get; set; }
        /// <summary>下次巡游检查的绝对分钟（每180分钟）。</summary>
        public int PatrolNextCheck { get; set; }
        /// <summary>随机接敌保护期截止（绕行/脱战后60分钟）；主动接敌不受限。</summary>
        public int EncounterGraceUntil { get; set; }
        /// <summary>探索/清理完成后待玩家选择“接敌/绕行”的敌踪；没有为 null。</summary>
        public PendingEncounter? Encounter { get; set; }
        /// <summary>进行中的战斗（v0.5 §17）；没有为 null。</summary>
        public BattleState? Battle { get; set; }
        public int NextBattleId { get; set; } = 1;
        /// <summary>
        /// schema &lt; 9 存档顶层的 EquippedWeaponId（旧“当前武器”随身引用；读档预处理取出，只供 8→9 迁移到武器槽 1，不序列化）。
        /// schema 9 起装备是物品 owner（"equip:p0:weapon1" 等，见 EquipSlots），不再有单独的当前武器引用。
        /// </summary>
        [JsonIgnore] public int? LegacyEquippedWeaponId { get; set; }

        // ───── 系统策划总册 v1.0（R07 吞噬融合、N01/S15 剧情） ─────
        /// <summary>人物词条（R07：首战教学融合的绿色“攻击力+0.1”）。不是物品装备，不随武器丢失。</summary>
        public List<TraitInstance> Traits { get; set; } = new List<TraitInstance>();
        /// <summary>开场、饱饱、首战与教学融合的持久进度（N01 §6、S15 §5、R07 §5）。</summary>
        public StoryState Story { get; set; } = new StoryState();

        [JsonIgnore] public List<GameEvent> PendingEvents { get; set; } = new List<GameEvent>();

        public GameState Clone()
        {
            var c = (GameState)MemberwiseClone();
            c.Rng = new Rng { State = Rng.State };
            c.Actors = Actors.ToDictionary(kv => kv.Key, kv => kv.Value.Clone());
            c.Party = new List<string>(Party);
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
            c.WorldEnemies = WorldEnemies.Select(x => x.Clone()).ToList();
            c.Encounter = Encounter?.Clone();
            c.Battle = Battle?.Clone();
            c.Traits = Traits.Select(x => x.Clone()).ToList();
            c.Story = Story.Clone();
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
        /// <summary>连续高免疫时间（分钟）：段首与段末免疫都≥门槛（config naturalCure.consecutiveImmunityAtLeast）才 +5。</summary>
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
        /// <summary>
        /// 烹饪受热保护（Q16、S11 §10）：食材段首仍新鲜、本段实际完整受热 5 分钟后置 true；此后原 expiresAt 不再判腐败，
        /// 熄火保留，目标被取出（进程随之移除）即解除。不改写原料的 ExpiresAt；净水批次进程不用。
        /// </summary>
        public bool HeatProtected { get; set; }

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
        /// <summary>当前耐久（v0.6 §05：小数，0—自身最大耐久）；无耐久物品为 null。</summary>
        public double? Durability { get; set; }
        /// <summary>实例最大耐久：生成时按区间以 0.1 精度抽一次并保存，读档、转移、拾取都不重抽；维护不改。</summary>
        public double? MaxDurability { get; set; }
        /// <summary>弹仓内已装弹数与弹药类型（v0.5 §05）：属于武器内容并计重一次；散弹药另为独立实例。</summary>
        public int LoadedAmmo { get; set; }
        public string? LoadedAmmoId { get; set; }
        /// <summary>
        /// 唯一归属：“carried” 随身；“equip:&lt;actorId&gt;:&lt;槽&gt;” 穿戴在该角色该槽（schema 9）；“pot:&lt;实例ID&gt;” 在该锅内；
        /// “facility:&lt;设施ID&gt;” 在该设施（火堆加工位）内；否则为地点 ID（该地点地面）。
        /// </summary>
        public string Owner { get; set; } = Carried;
        public int? ReservedBy { get; set; }
        /// <summary>放入设施或锅的先后序号；水煮分水先按它，再按实例 ID。离开设施与锅时清空。</summary>
        public int? PlacedOrder { get; set; }
        /// <summary>来源（R07 活性物质：来源世界敌人、战斗、发放事件、是否教学奖励）；不同来源不合堆。普通物品为 null。</summary>
        public ItemSource? Source { get; set; }

        public bool IsCarried => Owner == Carried;
        /// <summary>穿戴中（任一角色的七槽之一）：不占五张物资卡，重量计入该角色。</summary>
        public bool IsWorn => Owner.StartsWith(EquipPrefix);
        public bool IsWornBy(string actorId) => TryEquip(Owner, out var a, out _) && a == actorId;
        public bool InContainer => Owner.StartsWith(ContainerPrefix);
        public bool InFacility => Owner.StartsWith(FacilityPrefix);

        /// <summary>
        /// 某角色的随身 owner（开发计划 v0.9 B3 H2）：主角仍为 "carried"（不改旧档与现有逻辑）；队友将来为 "carried:&lt;actorId&gt;"（本批只预留）。
        /// </summary>
        public static string CarriedOwner(string actorId) => actorId == GameState.PlayerId ? Carried : Carried + ":" + actorId;

        /// <summary>穿戴 owner 前缀（B3 H2）：完整写法 "equip:&lt;actorId&gt;:&lt;槽ID&gt;"，槽见 EquipSlots。</summary>
        public const string EquipPrefix = "equip:";

        public static string EquipOwner(string actorId, string slot) => EquipPrefix + actorId + ":" + slot;

        /// <summary>拆穿戴 owner；不是穿戴或格式不对返回 false。</summary>
        public static bool TryEquip(string owner, out string actorId, out string slot)
        {
            actorId = "";
            slot = "";
            if (!owner.StartsWith(EquipPrefix)) return false;
            var rest = owner.Substring(EquipPrefix.Length);
            var k = rest.LastIndexOf(':');
            if (k <= 0 || k == rest.Length - 1) return false;
            actorId = rest.Substring(0, k);
            slot = rest.Substring(k + 1);
            return true;
        }

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

        public ItemInstance Clone()
        {
            var c = (ItemInstance)MemberwiseClone();
            c.Source = Source?.Clone();
            return c;
        }
    }

    /// <summary>物品来源（R07 §2）：sourceWorldEnemyId、sourceBattleId、grantEventId、tutorialReward。合堆键含 GrantEventId，教学与普通实例不合并。</summary>
    public sealed class ItemSource
    {
        public int? WorldEnemyId { get; set; }
        public int? BattleId { get; set; }
        public string GrantEventId { get; set; } = "";
        public bool TutorialReward { get; set; }

        public ItemSource Clone() => (ItemSource)MemberwiseClone();
    }

    /// <summary>
    /// 剧情与饱饱的持久状态（S15 §5 阶段表）：awakening → food_water_guidance → tutorial_battle_pending → tutorial_battle_won →
    /// tutorial_fusion_pending → tutorial_fusion_done。各阶段只锁存一次；旧档 Legacy=true，不补开场、首战、教学物质与+0.1，也不启用饱饱输入（T06）。
    /// </summary>
    public sealed class StoryState
    {
        /// <summary>旧档迁移来的局（T06）：不跑本轮新剧情与教学奖励。</summary>
        public bool Legacy { get; set; }
        /// <summary>饱饱在体内（R04 §7.2 baobaoPresent）：新局 true，旧档 false；治愈不移除。</summary>
        public bool BaobaoPresent { get; set; }
        /// <summary>开场唤醒与命名对白已完成（命名标记锁存）。</summary>
        public bool AwakeningDone { get; set; }
        /// <summary>饮食提示已读（不自动吃喝）。</summary>
        public bool FoodWaterRead { get; set; }
        /// <summary>首战保留的游荡尸（新局荒地初始实例）；首战胜利前不巡游、不被其他遭遇征用。</summary>
        public int? TutorialEnemyId { get; set; }
        /// <summary>首次到达荒地已触发首战（重复到达不再触发）。</summary>
        public bool TutorialTriggered { get; set; }
        public int? TutorialBattleId { get; set; }
        /// <summary>首战最终胜利且存活（唯一）。</summary>
        public bool TutorialWon { get; set; }
        /// <summary>教学活性物质的唯一发放事件 ID；已发过即不再发。</summary>
        public string TutorialGrantEventId { get; set; } = "";
        /// <summary>教学融合完成（绿色“攻击力+0.1”已登记）。</summary>
        public bool TutorialFusionDone { get; set; }
        /// <summary>已提交的融合事务 ID（防重复提交）。</summary>
        public List<string> FusionIds { get; set; } = new List<string>();
        /// <summary>待显示的饱饱对白 ID（按顺序）；显示并确认后移除。对白不推进时间、不发奖励。</summary>
        public List<string> PendingDialogues { get; set; } = new List<string>();
        /// <summary>已显示过的对白 ID（回看与防重复加入）。</summary>
        public List<string> SeenDialogues { get; set; } = new List<string>();
        /// <summary>
        /// 本局对话记录（S01 §8、S14 §10.4，schema 7）：按“一次对话”（触发事件）分组，逐句保存实际展示的发言者/正文快照、左右归属、
        /// 游戏内时间与地点；未结束的那次就是当前对话（输入锁）。新游戏重置；旧档按 SeenDialogues 一次性导入为“旧存档记录”。
        /// PendingDialogues/SeenDialogues 与之同步维护（未读/已读台词 ID）。
        /// </summary>
        public List<DialogueSession> DialogueLog { get; set; } = new List<DialogueSession>();
        public int NextDialogueSessionId { get; set; } = 1;

        public StoryState Clone()
        {
            var c = (StoryState)MemberwiseClone();
            c.FusionIds = new List<string>(FusionIds);
            c.PendingDialogues = new List<string>(PendingDialogues);
            c.SeenDialogues = new List<string>(SeenDialogues);
            c.DialogueLog = DialogueLog.Select(d => d.Clone()).ToList();
            return c;
        }
    }

    /// <summary>台词在对话栏的归属：主角恒左、对方恒右、叙述/场景说明为中性样式（S14 §10.2）。</summary>
    public enum DialogueSide { Left, Right, Narration }

    /// <summary>一次对话（S01 §8）：一个触发事件的一组台词。Lines 为已展示（含当前句），Pending 为尚未展示的台词 ID。</summary>
    public sealed class DialogueSession
    {
        /// <summary>本局内递增编号。</summary>
        public int Id { get; set; }
        /// <summary>触发事件 ID（config story.dialogues[].eventId，如 opening、tutorial_arrive）；同一事件只记一次。</summary>
        public string EventId { get; set; } = "";
        /// <summary>对话对象（非主角的发言者，如“饱饱”）。</summary>
        public string Counterpart { get; set; } = "";
        /// <summary>首句展示时的游戏内绝对分钟；旧档导入为 null（显示“未知”）。</summary>
        public int? Minute { get; set; }
        /// <summary>首句展示时所在地点；旧档导入为 null（显示“未知”）。</summary>
        public string? LocationId { get; set; }
        /// <summary>玩家已明确“结束对话”。</summary>
        public bool Ended { get; set; }
        /// <summary>从旧存档已读台词一次性导入的记录。</summary>
        public bool Legacy { get; set; }
        public List<DialogueLine> Lines { get; set; } = new List<DialogueLine>();
        public List<string> Pending { get; set; } = new List<string>();

        public DialogueSession Clone()
        {
            var c = (DialogueSession)MemberwiseClone();
            c.Lines = Lines.Select(l => l.Clone()).ToList();
            c.Pending = new List<string>(Pending);
            return c;
        }
    }

    /// <summary>一句已展示的台词快照：之后改了配置文案也不倒改本局历史（S01 §8）。</summary>
    public sealed class DialogueLine
    {
        public string LineId { get; set; } = "";
        /// <summary>在本次对话内的顺序（从 1 起）。</summary>
        public int Order { get; set; }
        public string Speaker { get; set; } = "";
        public string Text { get; set; } = "";
        /// <summary>随句的叙述/场景说明（中性样式显示）；没有为空串。</summary>
        public string Note { get; set; } = "";
        public DialogueSide Side { get; set; } = DialogueSide.Right;
        public int? Minute { get; set; }
        public string? LocationId { get; set; }

        public DialogueLine Clone() => (DialogueLine)MemberwiseClone();
    }

    /// <summary>人物词条实例（R07 §4）：效果 ID、品质、显示文字、攻击力平加值与发放来源。</summary>
    public sealed class TraitInstance
    {
        public string EffectId { get; set; } = "";
        public string Quality { get; set; } = "";
        public string Label { get; set; } = "";
        public double FlatAttack { get; set; }
        /// <summary>融合事务 ID（同一事务不重复提交）。</summary>
        public string FusionId { get; set; } = "";
        public int Minute { get; set; }

        public TraitInstance Clone() => (TraitInstance)MemberwiseClone();
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
        /// <summary>箱子的独立稀有附带物（医药补给箱 1% 特效药）是否抽中：开始时锁定，读档不重抽。</summary>
        public bool RareHit { get; set; }
        /// <summary>开始时锁定的随机接敌判定值 [0,1)（v0.6 §15）；完成时与按当时敌密度算出的概率比较。预览里置为 1（不接敌）。</summary>
        public double EncounterRoll { get; set; } = 1;

        /// <summary>等待烹饪：开始时绑定的本火堆进程 ID（FacilityProcess.ProcessId，含净水→食材链）；TargetId 为火堆所在地点。</summary>
        public List<string> CookTargets { get; set; } = new List<string>();
        /// <summary>等待烹饪：绑定目标的物品实例 ID（与 CookTargets 同序，界面展示用）。</summary>
        public List<int> CookTargetItems { get; set; } = new List<int>();
        /// <summary>等待烹饪：已完成的绑定进程。</summary>
        public List<string> CookDone { get; set; } = new List<string>();

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
            c.CookTargets = new List<string>(CookTargets);
            c.CookTargetItems = new List<int>(CookTargetItems);
            c.CookDone = new List<string>(CookDone);
            return c;
        }
    }

    public sealed class DeathInfo
    {
        public string Cause { get; set; } = "";
        /// <summary>尸变结束本局（v0.6 §09，病毒≥100），复用死亡结算但文案区分。</summary>
        public bool Zombified { get; set; }
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
        FacilityDone,
        /// <summary>探索/清理后发现敌踪，等待接敌或绕行（v0.6 §15）。</summary>
        Encounter,
        /// <summary>战斗事件：开始、指令结果、敌方行动、轮末与结束（v0.5 §16）。</summary>
        Battle,
        BattleEnd
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
