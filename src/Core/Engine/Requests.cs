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
        /// <summary>幂等请求号：同一请求重复提交只生效一次。</summary>
        public string? RequestId { get; set; }

        public static ActionRequest Travel(string locationId) => new ActionRequest { Type = ActionType.Travel, TargetId = locationId };
        public static ActionRequest Explore() => new ActionRequest { Type = ActionType.Explore };
        public static ActionRequest Clear() => new ActionRequest { Type = ActionType.Clear };
        public static ActionRequest Gather(string nodeId, int? toolInstanceId = null) =>
            new ActionRequest { Type = ActionType.Gather, TargetId = nodeId, ToolInstanceId = toolInstanceId };
        public static ActionRequest Craft(string recipeId, int? potInstanceId = null, IEnumerable<int>? preferredInstanceIds = null) =>
            new ActionRequest
            {
                Type = ActionType.Craft, TargetId = recipeId, PotInstanceId = potInstanceId,
                PreferredInstanceIds = preferredInstanceIds == null ? new List<int>() : new List<int>(preferredInstanceIds),
            };
        public static ActionRequest Wait(int minutes) => new ActionRequest { Type = ActionType.Wait, Minutes = minutes };
        public static ActionRequest Rest(int minutes) => new ActionRequest { Type = ActionType.Rest, Minutes = minutes };
        public static ActionRequest Sleep(int minutes) => new ActionRequest { Type = ActionType.Sleep, Minutes = minutes };
        public static ActionRequest UseInstance(int instanceId) => new ActionRequest { Type = ActionType.Use, InstanceId = instanceId };
        public static ActionRequest UseItem(string itemId) => new ActionRequest { Type = ActionType.Use, TargetId = itemId };

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

    public enum InstantKind { Pickup, Drop, AddFuel, Extinguish, FillPot, Decant, AttachPot, DetachPot, Reorder }

    public sealed class InstantRequest
    {
        public InstantKind Kind { get; set; }
        /// <summary>来源实例：拾取/放下的物品、投料的木材（0 表示自动选）、装水的水卡、倒出的锅内水、架上/取下的锅。</summary>
        public int InstanceId { get; set; }
        public int Count { get; set; } = 1;
        /// <summary>目标锅（装水）。</summary>
        public int? TargetInstanceId { get; set; }
        /// <summary>目标区域：carried 或当前地点 ID（倒出、排序）。</summary>
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
        public static InstantRequest AttachPot(int potInstanceId) => new InstantRequest { Kind = InstantKind.AttachPot, InstanceId = potInstanceId };
        public static InstantRequest DetachPot() => new InstantRequest { Kind = InstantKind.DetachPot };
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

        public static ActionResult Success(int? actionId = null) => new ActionResult { Ok = true, ActionId = actionId };
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
        public int? ToolDurabilityBefore { get; set; }
        public int? ToolDurabilityAfter { get; set; }
        public int? ToolMaxDurability { get; set; }
        public double HazardProbability { get; set; }
        public double HazardHealthLoss { get; set; }
        public string HazardLabel { get; set; } = "";

        public Stats StatsBefore { get; set; } = new Stats();
        public Stats StatsAfter { get; set; } = new Stats();
        public bool WouldDie { get; set; }
        public bool WillPauseFireOut { get; set; }
        public List<StatId> AlertsExpected { get; set; } = new List<StatId>();
        public List<string> Notes { get; set; } = new List<string>();
        public List<string> Warnings { get; set; } = new List<string>();

        /// <summary>清理与探索：本地掉落表（含未获得物资），只列可能结果，不泄露本次抽签。</summary>
        public List<LootOddsLine> LootOdds { get; set; } = new List<LootOddsLine>();
        /// <summary>清理：开始前的次数、上限与距下一次恢复的世界分钟（已满为 null）。</summary>
        public int? ClearCharges { get; set; }
        public int? ClearCap { get; set; }
        public int? NextRefillMinutes { get; set; }
    }
}
