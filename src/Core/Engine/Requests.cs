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

        public static ActionRequest Travel(string locationId) => new ActionRequest { Type = ActionType.Travel, TargetId = locationId };
        public static ActionRequest Explore() => new ActionRequest { Type = ActionType.Explore };
        public static ActionRequest Gather(string nodeId) => new ActionRequest { Type = ActionType.Gather, TargetId = nodeId };
        public static ActionRequest Craft(string recipeId) => new ActionRequest { Type = ActionType.Craft, TargetId = recipeId };
        public static ActionRequest Wait(int minutes) => new ActionRequest { Type = ActionType.Wait, Minutes = minutes };
        public static ActionRequest Rest(int minutes) => new ActionRequest { Type = ActionType.Rest, Minutes = minutes };
        public static ActionRequest Sleep(int minutes) => new ActionRequest { Type = ActionType.Sleep, Minutes = minutes };
        public static ActionRequest UseInstance(int instanceId) => new ActionRequest { Type = ActionType.Use, InstanceId = instanceId };
        public static ActionRequest UseItem(string itemId) => new ActionRequest { Type = ActionType.Use, TargetId = itemId };
        public static ActionRequest Ignite() => new ActionRequest { Type = ActionType.Ignite };
    }

    public enum InstantKind { Pickup, Drop, AddFuel, Extinguish }

    public sealed class InstantRequest
    {
        public InstantKind Kind { get; set; }
        public int InstanceId { get; set; }
        public int Count { get; set; } = 1;

        public static InstantRequest Pickup(int instanceId, int count) => new InstantRequest { Kind = InstantKind.Pickup, InstanceId = instanceId, Count = count };
        public static InstantRequest Drop(int instanceId, int count) => new InstantRequest { Kind = InstantKind.Drop, InstanceId = instanceId, Count = count };
        public static InstantRequest AddFuel() => new InstantRequest { Kind = InstantKind.AddFuel };
        public static InstantRequest Extinguish() => new InstantRequest { Kind = InstantKind.Extinguish };
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
    }
}
