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
        /// <summary>配方材料槽里手工指定的批次，先于自动补齐使用。</summary>
        public List<int> PreferredInstanceIds { get; set; } = new List<int>();
        /// <summary>批量制作件数（v0.6 §03 同一预留批次结转余工作量）；建造与维护只能 1。</summary>
        public int BatchCount { get; set; } = 1;
        /// <summary>幂等请求号：同一请求重复提交只生效一次。</summary>
        public string? RequestId { get; set; }

        public static ActionRequest Travel(string locationId) => new ActionRequest { Type = ActionType.Travel, TargetId = locationId };
        public static ActionRequest Explore() => new ActionRequest { Type = ActionType.Explore };
        public static ActionRequest Clear() => new ActionRequest { Type = ActionType.Clear };
        public static ActionRequest Gather(string nodeId, int? toolInstanceId = null) =>
            new ActionRequest { Type = ActionType.Gather, TargetId = nodeId, ToolInstanceId = toolInstanceId };
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
        /// <summary>绕行当前敌踪（固定10分钟，按 travel 消耗）。</summary>
        public static ActionRequest Bypass() => new ActionRequest { Type = ActionType.Bypass };
        /// <summary>饱饱吞噬融合指定的活性物质实例（R07 §4）。</summary>
        public static ActionRequest Fuse(int instanceId) => new ActionRequest { Type = ActionType.Fuse, InstanceId = instanceId };
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

    public enum InstantKind { Pickup, Drop, AddFuel, Extinguish, FillPot, Decant, PutInFire, PutFoodInPot, TakeOut, Reorder }

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

        public static ActionResult Success(int? actionId = null) => new ActionResult { Ok = true, ActionId = actionId };
        public static ActionResult Repeated() => new ActionResult { Ok = true, Duplicate = true };
        public static ActionResult Fail(string reason, int? max = null) => new ActionResult { Ok = false, Reason = reason, MaxCount = max };
    }

    public enum SegmentStatus { Idle, Running, Completed, Paused, Aborted, Dead }

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

        public int BaseMinutes { get; set; }
        /// <summary>按熟练度折减后的工作量（制造）；其他行动等于 BaseMinutes。</summary>
        public double WorkMinutes { get; set; }
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
        /// <summary>工具是玩家拖入指定的那一把（不自动换）。</summary>
        public bool ToolExplicit { get; set; }
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
        /// <summary>“接近上限：饱食溢出12，溢出部分浪费。”；没有溢出为 null。</summary>
        public string? OverflowText => Overflow.Count > 0 ? $"接近上限：{string.Join("，", Overflow)}，溢出部分浪费。" : null;
    }

    /// <summary>点火工具菜单的一行（v0.7 第 4.2 节）：随身与当前地面的每把打火器，含可用/不可用原因。</summary>
    public sealed class IgniteToolOption
    {
        public int InstanceId { get; set; }
        public string ItemId { get; set; } = "";
        /// <summary>“打火器#3”。</summary>
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
}
