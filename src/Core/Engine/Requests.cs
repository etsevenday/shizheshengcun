using System.Collections.Generic;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    public sealed class ActionRequest
    {
        public ActionType Type { get; set; }
        /// <summary>地点 ID / 资源点 ID / 配方 ID / 物品 ID（Use 未指定实例时按物品取最早到期批次）。</summary>
        public string TargetId { get; set; } = "";
        public int? InstanceId { get; set; }
        public int Minutes { get; set; }
        /// <summary>烧水、烹饪指定的锅；为空时按锅位 → 随身 → 地面自动选。</summary>
        public int? PotInstanceId { get; set; }
        /// <summary>采集时玩家拖入的工具；为空时自动用随身耐久最高的一件。</summary>
        public int? ToolInstanceId { get; set; }
        /// <summary>采集/搜集明确徒手：不自动候选随身合格工具（Q121，S05 本轮章“可明确改徒手”）。显式 ToolInstanceId 优先。</summary>
        public bool BareHands { get; set; }
        /// <summary>现场修补（B22b，Q168）选用的野外修补包实例；为空时默认选能恢复最多、消耗最少的一包。</summary>
        public int? PatchInstanceId { get; set; }

        /// <summary>
        /// 预估缓存键（网页 GameSession.Preview 按它缓存）：凡是会改变预估结果的请求字段都要在内。
        /// B13b 试玩修复：原键漏了 BareHands，“改用徒手开始”按钮读到带工具的缓存预估，显示了用工具后的工时。
        /// </summary>
        public string PreviewKey =>
            $"{Type}|{TargetId}|{InstanceId}|{Minutes}|{ToolInstanceId}|{PotInstanceId}|{BatchCount}|{string.Join(",", PreferredInstanceIds)}|{(BareHands ? "bare" : "")}|{(ConfirmExpedition ? "exp" : "")}|{PatchInstanceId}|{Feeding?.Key}";
        /// <summary>
        /// 移动时确认开始地牢远征（R03 D3，B21）：在入口缓冲 01 第一次走向危险房间必须带它，移动开始时原子提交远征；其他移动忽略。
        /// </summary>
        public bool ConfirmExpedition { get; set; }
        /// <summary>配方材料槽里手工指定的批次，先于自动补齐使用。</summary>
        public List<int> PreferredInstanceIds { get; set; } = new List<int>();
        /// <summary>批量制作件数（v0.6 §03 同一预留批次结转余工作量）；建造与维护只能 1。</summary>
        public int BatchCount { get; set; } = 1;
        /// <summary>幂等请求号：同一请求重复提交只生效一次。</summary>
        public string? RequestId { get; set; }
        /// <summary>给同行者吃喝（B28，Q232）：非空时这次 Use 的效果落到目标本人，校验与文字走 FeedRequest 一路。</summary>
        public FeedRequest? Feeding { get; set; }

        public static ActionRequest Travel(string locationId, bool confirmExpedition = false) => new ActionRequest { Type = ActionType.Travel, TargetId = locationId, ConfirmExpedition = confirmExpedition };
        public static ActionRequest Explore() => new ActionRequest { Type = ActionType.Explore };
        public static ActionRequest Clear() => new ActionRequest { Type = ActionType.Clear };
        /// <summary>遗迹勘整（Q55）：在当前地点做搜集训练。</summary>
        public static ActionRequest Survey() => new ActionRequest { Type = ActionType.Survey };
        public static ActionRequest Gather(string nodeId, int? toolInstanceId = null, bool bareHands = false) =>
            new ActionRequest { Type = ActionType.Gather, TargetId = nodeId, ToolInstanceId = toolInstanceId, BareHands = bareHands };
        public static ActionRequest Craft(string recipeId, int? potInstanceId = null, IEnumerable<int>? preferredInstanceIds = null, int batchCount = 1) =>
            new ActionRequest
            {
                Type = ActionType.Craft, TargetId = recipeId, PotInstanceId = potInstanceId,
                PreferredInstanceIds = preferredInstanceIds == null ? new List<int>() : new List<int>(preferredInstanceIds),
                BatchCount = batchCount,
            };
        public static ActionRequest Wait(int minutes) => new ActionRequest { Type = ActionType.Wait, Minutes = minutes };
        public static ActionRequest Rest(int minutes) => new ActionRequest { Type = ActionType.Rest, Minutes = minutes };
        public static ActionRequest Sleep(int minutes) => new ActionRequest { Type = ActionType.Sleep, Minutes = minutes };
        public static ActionRequest UseInstance(int instanceId) => new ActionRequest { Type = ActionType.Use, InstanceId = instanceId };
        public static ActionRequest UseItem(string itemId) => new ActionRequest { Type = ActionType.Use, TargetId = itemId };
        /// <summary>给同行者吃喝一份（B28）：与主角食饮同一 5 分钟事务，开始时原子扣一份并给效果。</summary>
        public static ActionRequest FeedCompanion(FeedRequest f) => new ActionRequest { Type = ActionType.Use, Feeding = f, RequestId = f.RequestId };
        /// <summary>饱饱吞噬融合指定的活性物质实例（R07 §4）。</summary>
        public static ActionRequest Fuse(int instanceId) => new ActionRequest { Type = ActionType.Fuse, InstanceId = instanceId };
        /// <summary>等待当前地点火堆里全部当前可加工内容完成（S11 §9）：时长由目标动态决定，按 wait 消耗。</summary>
        public static ActionRequest WaitCook() => new ActionRequest { Type = ActionType.WaitCook };
        /// <summary>处理一具随身或当前地面的动物尸体（v0.5 §14）。</summary>
        public static ActionRequest Butcher(int carcassInstanceId) => new ActionRequest { Type = ActionType.Butcher, InstanceId = carcassInstanceId };

        public ActionRequest WithRequestId(string? id)
        {
            var c = (ActionRequest)MemberwiseClone();
            c.PreferredInstanceIds = new List<int>(PreferredInstanceIds);
            c.RequestId = id;
            return c;
        }
        /// <summary>点火；指定打火器实例时用它（随身或当前地面），否则自动选。</summary>
        public static ActionRequest Ignite(int? lighterInstanceId = null) => new ActionRequest { Type = ActionType.Ignite, InstanceId = lighterInstanceId };
    }

    /// <summary>
    /// 给同行者吃喝的请求（开发计划 v0.9 B28，合同 feeding、carriedPotQ233）：一次一目标一份。出物人只能是主角或目标本人，不自动换人、不自动换批次。
    /// 选中的物品用 InstanceId（食物、水卡、水壶、锅内食物或锅内水）；锅水也可写 PotInstanceId + WaterItemId。
    /// </summary>
    public sealed class FeedRequest
    {
        /// <summary>吃喝的同行者 actorId。</summary>
        public string TargetActorId { get; set; } = "";
        /// <summary>出物人 actorId：主角 "p0" 或目标本人。</summary>
        public string SourceActorId { get; set; } = "";
        public int? InstanceId { get; set; }
        /// <summary>出物的随身锅；带 InstanceId 时重验内容确实在这口锅里。</summary>
        public int? PotInstanceId { get; set; }
        /// <summary>不带 InstanceId 时，从 PotInstanceId 这口锅里取这种水（dirty_water/clean_water）。</summary>
        public string? WaterItemId { get; set; }
        /// <summary>预览给出的 FeedSourceInfo.Stamp：带上时确认前重验 owner/份数/水量，有变化整条拒绝。null = 不比对。</summary>
        public string? ConfirmStamp { get; set; }
        /// <summary>幂等请求号：连点、读档只结算一次。</summary>
        public string? RequestId { get; set; }

        internal string Key => $"{TargetActorId}|{SourceActorId}|{InstanceId}|{PotInstanceId}|{WaterItemId}|{ConfirmStamp}";
    }

    public enum FeedSourceKind { Food, Water, Canteen, PotFood, PotWater }

    /// <summary>一个合法出物批次（FeedSources 的一行，也是预览的来源字段）。</summary>
    public sealed class FeedSourceInfo
    {
        public string SourceActorId { get; set; } = "";
        /// <summary>“你”或同行显示名。</summary>
        public string SourceName { get; set; } = "";
        public int InstanceId { get; set; }
        public string ItemId { get; set; } = "";
        /// <summary>物品或水种原名；水壶为壶内水种名。</summary>
        public string ItemName { get; set; } = "";
        public FeedSourceKind Kind { get; set; }
        /// <summary>出物的随身锅实例号；不在锅里为 null。</summary>
        public int? PotInstanceId { get; set; }
        /// <summary>可用份数：食物与水卡为堆叠数，锅水为未预留的 500 毫升份数，水壶为可喝的 250 毫升次数。</summary>
        public int Portions { get; set; }
        /// <summary>这一份消耗的毫升（水卡与锅水 500、水壶 250）；食物为 0。</summary>
        public int PortionMl { get; set; }
        /// <summary>现有水量毫升（水卡为这一堆的总量，锅水为未预留水量，水壶为壶内水量）；食物为 0。</summary>
        public int LiquidMl { get; set; }
        /// <summary>保质截止世界分钟；无截止为 null。</summary>
        public int? ExpiresAt { get; set; }
        /// <summary>确认用快照：放进 FeedRequest.ConfirmStamp。</summary>
        public string Stamp { get; set; } = "";
    }

    /// <summary>合法目标（FeedTargets 的一行）。</summary>
    public sealed class FeedTargetInfo
    {
        public string ActorId { get; set; } = "";
        public string Name { get; set; } = "";
        public Stats Stats { get; set; } = new Stats();
    }

    /// <summary>给同行者吃喝的预览字段；耗时、确认文字、溢出在 ActionPreview 的 BaseMinutes/EstimatedMinutes/ConfirmText/Overflow。</summary>
    public sealed class FeedPreviewInfo
    {
        public string TargetActorId { get; set; } = "";
        public string TargetName { get; set; } = "";
        public FeedSourceInfo Source { get; set; } = new FeedSourceInfo();
        /// <summary>结算后的现有水量毫升（规则同 FeedSourceInfo.LiquidMl）；食物为 0。</summary>
        public int LiquidMlAfter { get; set; }
        public Stats TargetBefore { get; set; } = new Stats();
        /// <summary>确认那一刻效果给完（裁上限）后的目标状态。</summary>
        public Stats TargetAfterImmediate { get; set; } = new Stats();
        /// <summary>5 分钟走完后的目标状态（含自然消耗）。</summary>
        public Stats TargetAfter { get; set; } = new Stats();
        /// <summary>结果句里的{变化}：目标实际生效并裁上限后的变化与风险反馈。</summary>
        public string ChangeText { get; set; } = "";
        /// <summary>成功后的完整结果句。</summary>
        public string ResultText { get; set; } = "";
    }

    public enum InstantKind { Pickup, Drop, AddFuel, Extinguish, FillPot, Decant, PutInFire, PutFoodInPot, TakeOut, Reorder, LoadCharcoal, FillCanteen, PourCanteen, LoadPotRecipe, StoreCabinet, TakeCabinet }

    public sealed class InstantRequest
    {
        public InstantKind Kind { get; set; }
        /// <summary>来源实例：拾取/放下的物品、投料的木材（0 表示自动选）、装水的水卡、倒出的锅内水、放进或取出设施的锅与食材。</summary>
        public int InstanceId { get; set; }
        public int Count { get; set; } = 1;
        /// <summary>目标锅（装水、放食材）。</summary>
        public int? TargetInstanceId { get; set; }
        /// <summary>目标区域：carried 或当前地点 ID（倒出、取出、排序）。</summary>
        public string Zone { get; set; } = "";
        /// <summary>排序：被移动的卡片键与插入位置（在去掉该卡后的列表里的下标）。</summary>
        public string CardKey { get; set; } = "";
        public int Index { get; set; }
        /// <summary>幂等请求号：同一请求重复提交只生效一次（拖拽与容器案 9）。</summary>
        public string? RequestId { get; set; }
        /// <summary>Q113 煅烧投料：玩家在浮窗里看到并确认的逐堆明细；带上时确认前重验，来源有任何变化整条拒绝。null = 内部/旧调用，按当时自动分配。</summary>
        public CharcoalPlan? CharcoalPlan { get; set; }

        public static InstantRequest Pickup(int instanceId, int count) => new InstantRequest { Kind = InstantKind.Pickup, InstanceId = instanceId, Count = count };
        public static InstantRequest Drop(int instanceId, int count) => new InstantRequest { Kind = InstantKind.Drop, InstanceId = instanceId, Count = count };
        public static InstantRequest AddFuel(int count = 1, int woodInstanceId = 0) => new InstantRequest { Kind = InstantKind.AddFuel, Count = count, InstanceId = woodInstanceId };
        public static InstantRequest Extinguish() => new InstantRequest { Kind = InstantKind.Extinguish };
        public static InstantRequest FillPot(int waterInstanceId, int potInstanceId, int count = 1) =>
            new InstantRequest { Kind = InstantKind.FillPot, InstanceId = waterInstanceId, TargetInstanceId = potInstanceId, Count = count };
        public static InstantRequest Decant(int potWaterInstanceId, string zone, int count = 1) =>
            new InstantRequest { Kind = InstantKind.Decant, InstanceId = potWaterInstanceId, Zone = zone, Count = count };
        /// <summary>锅或直烤食材放进当前火堆的空加工位（食材每份占一位）。</summary>
        public static InstantRequest PutInFire(int instanceId, int count = 1) => new InstantRequest { Kind = InstantKind.PutInFire, InstanceId = instanceId, Count = count };
        /// <summary>食材放进指定锅的食材区。</summary>
        public static InstantRequest PutFoodInPot(int foodInstanceId, int potInstanceId, int count = 1) =>
            new InstantRequest { Kind = InstantKind.PutFoodInPot, InstanceId = foodInstanceId, TargetInstanceId = potInstanceId, Count = count };
        /// <summary>从火堆取下锅（整锅）或取出火堆/锅里的一份食材，到随身或当前地面。</summary>
        public static InstantRequest TakeOut(int instanceId, string zone) => new InstantRequest { Kind = InstantKind.TakeOut, InstanceId = instanceId, Zone = zone };
        /// <summary>Q111 火堆煅烧木炭：从随身与当前地面未预留的木材凑足一批（4份）原子放进当前火堆一个空加工位；0 世界分钟。</summary>
        /// <summary>Q113：plan 为浮窗展示的明细（<see cref="CharcoalLoadInfo.Plan"/>），确认时内核逐堆重验。</summary>
        public static InstantRequest LoadCharcoal(CharcoalPlan? plan = null) =>
            new InstantRequest { Kind = InstantKind.LoadCharcoal, CharcoalPlan = plan?.Clone() };
        /// <summary>B16 分量水壶：count 份 500 毫升水卡装进水壶。</summary>
        public static InstantRequest FillCanteen(int waterInstanceId, int canteenInstanceId, int count = 1) =>
            new InstantRequest { Kind = InstantKind.FillCanteen, InstanceId = waterInstanceId, TargetInstanceId = canteenInstanceId, Count = count };
        /// <summary>B16 分量水壶：倒出 count 份 500 毫升水卡到随身或当前地面。</summary>
        public static InstantRequest PourCanteen(int canteenInstanceId, string zone, int count = 1) =>
            new InstantRequest { Kind = InstantKind.PourCanteen, InstanceId = canteenInstanceId, Zone = zone, Count = count };
        /// <summary>B16 复合锅食谱：在当前火堆里的锅中投料开做（ProcessId 为锅食谱进程，如 pot_hardtack；TargetInstanceId 为锅）。</summary>
        public static InstantRequest LoadPotRecipe(string processId, int potInstanceId) =>
            new InstantRequest { Kind = InstantKind.LoadPotRecipe, CardKey = processId, TargetInstanceId = potInstanceId };
        /// <summary>B17 安全屋柜子：随身/屋内地面 → 柜子（0 分钟）。</summary>
        public static InstantRequest StoreCabinet(int instanceId, int count = 1) => new InstantRequest { Kind = InstantKind.StoreCabinet, InstanceId = instanceId, Count = count };
        /// <summary>B17 安全屋柜子：柜子 → 随身（zone 为空或 carried）或屋内地面（zone = 屋内地面 owner）。</summary>
        public static InstantRequest TakeCabinet(int instanceId, int count = 1, string? zone = null) =>
            new InstantRequest { Kind = InstantKind.TakeCabinet, InstanceId = instanceId, Count = count, Zone = zone ?? "" };
        public static InstantRequest Reorder(string zone, string cardKey, int index) =>
            new InstantRequest { Kind = InstantKind.Reorder, Zone = zone, CardKey = cardKey, Index = index };

        public InstantRequest WithRequestId(string? id)
        {
            RequestId = id;
            return this;
        }
    }

    public sealed class ActionResult
    {
        public bool Ok { get; set; }
        public string Reason { get; set; } = "";
        /// <summary>超重拾取时可取的最大数量。</summary>
        public int? MaxCount { get; set; }
        public int? ActionId { get; set; }
        /// <summary>同一 requestId 重复到达：返回成功但不再改状态（v0.7 第 4.3 节“重复请求不重复点火”）。</summary>
        public bool Duplicate { get; set; }
        /// <summary>退出请求（RequestExit）：单段事务已开始结算，本段完成后才结束（不退还、不撤销）。</summary>
        public bool Deferred { get; set; }
        /// <summary>B41c2（S01 Q514、Q523）：这次真实「继续行动」或例外拾取复核后，锁定行动因永久失效已按原取消规则自动中止。</summary>
        public bool Aborted { get; set; }

        public static ActionResult Success(int? actionId = null) => new ActionResult { Ok = true, ActionId = actionId };
        public static ActionResult Repeated() => new ActionResult { Ok = true, Duplicate = true };
        public static ActionResult Fail(string reason, int? max = null) => new ActionResult { Ok = false, Reason = reason, MaxCount = max };
    }

    /// <summary>Dialogue：有未读对话，本段没有开始（不 tick、不改行动），读完后从这里继续（S01 §8）。</summary>
    public enum SegmentStatus { Idle, Running, Completed, Paused, Aborted, Dead, Dialogue }

    public sealed class SegmentResult
    {
        public SegmentStatus Status { get; set; }
        public SegmentResult(SegmentStatus status) { Status = status; }
    }

    public sealed class OutputLine
    {
        public string ItemId { get; set; } = "";
        public string Name { get; set; } = "";
        public int Count { get; set; }
    }

    /// <summary>掉落表的一行：物品（null 为未获得）与概率百分比。</summary>
    public sealed class LootOddsLine
    {
        public string? ItemId { get; set; }
        public string Name { get; set; } = "";
        public int Percent { get; set; }
    }

    /// <summary>行动预览：内核的只读估算，供界面在确认前展示。</summary>
    public sealed class ActionPreview
    {
        public bool Ok { get; set; }
        public string Reason { get; set; } = "";
        public string Label { get; set; } = "";
        public string ConfirmText { get; set; } = "";
        public ActionType Type { get; set; }
        public bool AsSubAction { get; set; }
        /// <summary>B41c2（S01 Q516、逐字稿 Q517）：按此刻条件真实开工会被认定为开工 5 分钟、不可中途退出（只读，不建标记）。</summary>
        public bool NoExit { get; set; }

        public int BaseMinutes { get; set; }
        /// <summary>需求工作量：基础分钟×装备与熟练节点倍率（Q61 开工快照，S01 倍率 M 之前）；非工作行动等于 BaseMinutes。</summary>
        public double WorkMinutes { get; set; }
        /// <summary>Q61：开工时可用的同类余量与完成后的新余量（基础工作分钟；非工作行动为 0/null）。</summary>
        public double CarryBefore { get; set; }
        public double? CarryAfter { get; set; }
        /// <summary>完成后获得的经验，如“制造+3”；没有为空。</summary>
        public List<string> XpGains { get; set; } = new List<string>();
        public int EstimatedMinutes { get; set; }
        public int StartMinute { get; set; }
        public int EstimatedEndMinute { get; set; }
        public bool IsWork { get; set; }
        public double FactorAtStart { get; set; } = 1;
        public List<string> FactorReasons { get; set; } = new List<string>();

        public List<OutputLine> Outputs { get; set; } = new List<OutputLine>();
        public string OutputPlace { get; set; } = "";
        public string? ToolName { get; set; }
        /// <summary>B13a2（Q137）：本次实际使用的工具物品 ID（界面据此列出“改用其他合格工具”）。</summary>
        public string? ToolItemId { get; set; }
        /// <summary>工具是玩家拖入指定的那一把（不自动换）。</summary>
        public bool ToolExplicit { get; set; }
        /// <summary>Q121：本行有合格工具可选——界面给“改用徒手/用随身工具”切换（徒手始终可用）。</summary>
        public bool ToolChoice { get; set; }
        /// <summary>本次预估是明确徒手（不自动候选工具）。</summary>
        public bool BareHands { get; set; }
        public double? ToolDurabilityBefore { get; set; }
        public double? ToolDurabilityAfter { get; set; }
        public double? ToolMaxDurability { get; set; }
        public double HazardProbability { get; set; }
        public double HazardHealthLoss { get; set; }
        public string HazardLabel { get; set; } = "";

        public Stats StatsBefore { get; set; } = new Stats();
        public Stats StatsAfter { get; set; } = new Stats();
        public bool WouldDie { get; set; }
        /// <summary>
        /// 期间设施变化（设施与烹饪案第 8 节）：从副本模拟得到，例如“生块根#21烤成烤熟块根”“临时营地火堆燃料55→25分钟”。
        /// 只是对当前状态的预测；角色身体只按本行动结算。
        /// </summary>
        public List<string> FacilityChanges { get; set; } = new List<string>();
        public List<StatId> AlertsExpected { get; set; } = new List<StatId>();
        public List<string> Notes { get; set; } = new List<string>();
        public List<string> Warnings { get; set; } = new List<string>();

        /// <summary>清理与探索：本地掉落表（含未获得物资），只列可能结果，不泄露本次抽签。</summary>
        public List<LootOddsLine> LootOdds { get; set; } = new List<LootOddsLine>();
        /// <summary>清理：开始前的次数、上限与距下一次恢复的世界分钟（已满为 null）。</summary>
        public int? ClearCharges { get; set; }
        public int? ClearCap { get; set; }
        public int? NextRefillMinutes { get; set; }

        // ───── 点火（基础UI修订 v0.7 第 4.1 节：拖拽悬停与工具菜单的结构化预测） ─────
        /// <summary>点火前火堆燃料（分钟）。</summary>
        public int? FuelBefore { get; set; }
        /// <summary>开始点火后（含自动投木）的燃料。</summary>
        public int? FuelAfterStart { get; set; }
        /// <summary>点火完成时的燃料。</summary>
        public int? FuelAtEnd { get; set; }
        /// <summary>燃料为 0 时自动投入 1 份木材。</summary>
        public bool AutoWood { get; set; }
        /// <summary>自动投入的那一堆木材实例。</summary>
        public int? AutoWoodInstanceId { get; set; }
        /// <summary>点火完成即燃尽熄灭（例如只剩 5 分钟燃料）。</summary>
        public bool DiesOutAtEnd { get; set; }
        /// <summary>点火绑定的打火器实例与所在（随身 / 当前地面）。</summary>
        public int? LighterInstanceId { get; set; }
        public string LighterPlace { get; set; } = "";

        // ───── 自用（v0.7 第 7 节：按钮附近写明效果与溢出） ─────
        /// <summary>即时效果结算后、5 分钟自然消耗之前的属性。</summary>
        public Stats? StatsAfterImmediate { get; set; }
        /// <summary>临近上限的溢出，如“饱食溢出12”。</summary>
        public List<string> Overflow { get; set; } = new List<string>();
        /// <summary>给同行者吃喝（B28）的结构化预览；其他行动与不能开始时为 null。Overflow 此时按目标本人算。</summary>
        public FeedPreviewInfo? Feed { get; set; }
        /// <summary>“接近上限：饱食溢出12，溢出部分浪费。”；没有溢出为 null。</summary>
        public string? OverflowText => Overflow.Count > 0 ? $"接近上限：{string.Join("，", Overflow)}，溢出部分浪费。" : null;
    }

    /// <summary>点火工具菜单的一行（v0.7 第 4.2 节）：随身与当前地面的每把打火器，含可用/不可用原因。</summary>
    public sealed class IgniteToolOption
    {
        public int InstanceId { get; set; }
        public string ItemId { get; set; } = "";
        /// <summary>“打火器”（B32a Q349：不带物品内部编号，同名靠来源与耐久区分）。</summary>
        public string Name { get; set; } = "";
        /// <summary>“随身”或“当前地面”。</summary>
        public string Source { get; set; } = "";
        public bool Usable { get; set; }
        /// <summary>不可用原因（内核点火校验原文）；可用为 null。</summary>
        public string? Reason { get; set; }
        public double? Durability { get; set; }
        public double? MaxDurability { get; set; }
    }

    /// <summary>取出设施或锅里的物品会清零的加工（v0.7 第 3.3 节：操作条提前显示后果）。</summary>
    public sealed class TakeOutConsequence
    {
        /// <summary>会清零的加工，如“生块根#21水煮受热10分钟”。</summary>
        public List<string> Resets { get; set; } = new List<string>();
        /// <summary>受烹饪保护且已过原保质期、取出后会立即腐败的食材（Q16）。</summary>
        public List<string> Spoils { get; set; } = new List<string>();
        /// <summary>取到随身后的随身根卡数（S06 §8）。</summary>
        public int RootCardsAfter { get; set; }
        /// <summary>该物品连同内容的克重。</summary>
        public int Grams { get; set; }
        /// <summary>拾取到随身后的随身总重（克）。</summary>
        public int CarriedGramsAfter { get; set; }
        /// <summary>“取出后生块根#21水煮受热10分钟清零”或“当前没有已受热的加工进度”。</summary>
        public string Text { get; set; } = "";
    }

    /// <summary>当前状态异常（v0.7 第 6.1 节人物页与左栏共用一个数据源）。</summary>
    public sealed class ActiveCondition
    {
        /// <summary>diarrhea / bandageCooldown。</summary>
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public int RemainingMinutes { get; set; }
        public string Effect { get; set; } = "";
        /// <summary>是否属于不利状态（界面用警示色）。</summary>
        public bool Harmful { get; set; }
    }

    /// <summary>同一位置同一物品的一个真实批次（Q24，GameEngine.BatchesOf）：到期 + 来源 + 预留状态相同的若干堆。</summary>
    public sealed class ItemBatch
    {
        public string Key { get; set; } = "";
        public string ItemId { get; set; } = "";
        public string Owner { get; set; } = "";
        public int? ExpiresAt { get; set; }
        public string? GrantEventId { get; set; }
        public bool Reserved { get; set; }
        public bool Spoiled { get; set; }
        public int Count { get; set; }
        public List<ItemInstance> Stacks { get; set; } = new List<ItemInstance>();
        /// <summary>未预留、未腐败：可食用与转移的合法批次。</summary>
        public bool Usable => !Reserved && !Spoiled;
    }
}
