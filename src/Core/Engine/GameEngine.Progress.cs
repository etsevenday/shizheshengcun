using System;
using System.Collections.Generic;
using System.Linq;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>点“退出”会怎样（S01 §9）。</summary>
    public enum ExitKind
    {
        /// <summary>不能退出（没有行动、战斗中、对话中、已死亡）。</summary>
        None,
        /// <summary>在当前 5 分钟段边界停止，不再调度下一段；已发生的保留、未发生的不结算。</summary>
        StopAtBoundary,
        /// <summary>单段事务（点火、自用、融合）开始时已提交，只能如实等它完成，不退还、不撤销。</summary>
        CompletesCurrentTransaction,
    }

    /// <summary>
    /// 中央推进条的数据（S14 §11、S01 §9）：行动名、进度、剩余游戏时间（“预计”标记）、暂停原因、对话等待、退出语义。
    /// 全部由玩法层给出，界面不另建现实秒表；剩余时间在副本上只读模拟（开发假设 A9），不推进真实时间、不抽真实随机。
    /// </summary>
    public sealed class ActionProgressInfo
    {
        public int ActionId { get; set; }
        public ActionType Type { get; set; }
        public string Label { get; set; } = "";
        public bool IsSub { get; set; }
        /// <summary>主行动暂停中（危险阈值、熄火等）：中央条显示“继续/退出”与真实原因。</summary>
        public bool Paused { get; set; }
        public string? PauseText { get; set; }
        /// <summary>有未读对话：中央条标“对话中，时间未推进”。</summary>
        public bool DialogueWaiting { get; set; }
        public int ElapsedMinutes { get; set; }
        /// <summary>剩余游戏分钟（按 5 分钟段）；无法给出可信倒计时（如燃料不足会停止）为 null，此时看 StopNote。</summary>
        public int? RemainingMinutes { get; set; }
        /// <summary>工作与等待烹饪的剩余是按当前及可预知条件重算的“预计”；固定时长为 false。</summary>
        public bool Estimated { get; set; }
        /// <summary>进度 0–1。</summary>
        public double Fraction { get; set; }
        /// <summary>“尚需至少45有效分钟，当前燃料不足，耗尽时停止”等；没有为 null。</summary>
        public string? StopNote { get; set; }
        public ExitKind Exit { get; set; }
        /// <summary>退出入口旁的说明（单段事务“已开始结算，完成后结束”、对话中/战斗中不能退出的原因）。</summary>
        public string ExitNote { get; set; } = "";
        /// <summary>退出会作废本次行动的成果（探索、清理、采集、制作、处理尸体、移动）：中央条在“退出”旁常驻写明后果。</summary>
        public bool ExitForfeits { get; set; }
        /// <summary>等待烹饪的目标与各自预计（其他行动为空）。</summary>
        public List<CookTargetInfo> CookTargets { get; set; } = new List<CookTargetInfo>();
    }

    public sealed partial class GameEngine
    {
        /// <summary>只有一个 5 分钟原子事务、开始时就提交效果的行动（S01 §9）。</summary>
        public static bool IsSingleTransaction(ActionType t) => t == ActionType.Use || t == ActionType.Ignite || t == ActionType.Fuse;

        private static bool IsFixedDuration(ActionType t) =>
            t == ActionType.Wait || t == ActionType.Rest || t == ActionType.Sleep || t == ActionType.Bypass || IsSingleTransaction(t);

        /// <summary>
        /// 当前行动（子行动优先）的中央推进数据；没有行动为 null。固定时长按未发生时长；工作按剩余工作量与当前/已知未来倍率逐段模拟（预计）；
        /// 等待烹饪按绑定目标依赖与并行取最晚（预计），燃料不足时 RemainingMinutes 为 null 并写 StopNote。只读。
        /// </summary>
        public ActionProgressInfo? ActionProgress(GameState s)
        {
            var a = s.Sub ?? s.Main;
            if (a == null) return null;
            var info = new ActionProgressInfo
            {
                ActionId = a.ActionId, Type = a.Type, Label = a.Label, IsSub = a.IsSub,
                Paused = !a.IsSub && a.Status == ActionStatus.Paused,
                PauseText = !a.IsSub && a.Status == ActionStatus.Paused ? PauseText(s, a) : null,
                DialogueWaiting = DialogueLocked(s),
                ElapsedMinutes = a.ElapsedMinutes,
            };
            if (a.Type == ActionType.WaitCook)
            {
                var plan = new CookWaitPlan { LocationId = a.TargetId };
                var open = a.CookTargets.Where(p => !a.CookDone.Contains(p)).ToList();
                AnalyzeCook(s, a.TargetId, open, plan);
                info.CookTargets = a.CookTargets.Select((pid, k) => plan.Targets.FirstOrDefault(t => t.ProcessId == pid)
                    ?? new CookTargetInfo { ProcessId = pid, TargetInstanceId = k < a.CookTargetItems.Count ? a.CookTargetItems[k] : 0, Done = true,
                                            Label = k < a.CookTargetItems.Count && FindItem(s, a.CookTargetItems[k]) is ItemInstance t ? $"{ItemName(t.ItemId)}#{t.InstanceId}" : pid })
                    .Select(t => { if (a.CookDone.Contains(t.ProcessId)) { t.Done = true; t.EtaMinutes = 0; } return t; }).ToList();
                info.Estimated = true;
                info.RemainingMinutes = open.Count == 0 ? 0 : plan.EstimatedMinutes;
                info.StopNote = plan.FuelNote;
                var total = a.ElapsedMinutes + (info.RemainingMinutes ?? plan.NeededEffectiveMinutes);
                info.Fraction = total <= 0 ? 0 : Math.Min(1, a.ElapsedMinutes / (double)total);
            }
            else if (IsFixedDuration(a.Type))
            {
                info.RemainingMinutes = Math.Max(0, a.PlannedMinutes - a.ElapsedMinutes);
                info.Fraction = a.PlannedMinutes <= 0 ? 0 : Math.Min(1, a.ElapsedMinutes / (double)a.PlannedMinutes);
            }
            else
            {
                var end = EstimateEndMinute(s);
                info.Estimated = true;
                info.RemainingMinutes = end.HasValue ? end.Value - s.GameMinute : (int?)null;
                info.Fraction = a.BaseWorkMinutes <= 0 ? 0 : Math.Min(1, (a.CarryReserved + a.WorkDone) / a.BaseWorkMinutes);
            }
            var why = ExitBlocked(s);
            info.Exit = why != null ? ExitKind.None : IsSingleTransaction(a.Type) ? ExitKind.CompletesCurrentTransaction : ExitKind.StopAtBoundary;
            var forfeit = info.Exit == ExitKind.StopAtBoundary ? ExitForfeitText(a) : null;
            info.ExitForfeits = forfeit != null;
            info.ExitNote = why ?? (info.Exit == ExitKind.CompletesCurrentTransaction ? $"{a.Label}已开始结算，本段完成后结束（已提交的消耗与效果不撤销）" : forfeit ?? "在当前5分钟段结束处停止，已发生的消耗与收获保留");
            return info;
        }

        /// <summary>中途退出会作废成果的行动：退出后果短提示（与 Cancel 的实际结算一致，已耗时间不退）。其余行动返回 null。</summary>
        private string? ExitForfeitText(ActionState a) => a.Type switch
        {
            ActionType.Explore => "退出将放弃本次探索：探索度不变、不获得物资（已耗时间不退）",
            ActionType.Clear => "退出将放弃本次清理：不获得物资，已扣的1次清理机会不返还（已耗时间不退）",
            ActionType.Gather => "退出将放弃本次采集：不获得产物（已耗时间不退）",
            ActionType.Survey => "退出将放弃本次遗迹勘整：不获得经验，不占今日次数（已耗时间不退）",
            ActionType.Craft or ActionType.Build => "退出将放弃本次制作：材料返还当前地面，不得半成品（已耗时间不退）",
            ActionType.Butcher => "退出将放弃本次处理：尸体返还当前地面，没有产物（已耗时间不退）",
            ActionType.Travel => $"退出将留在{LocName(a.SourceLocation)}（已走的时间与消耗不退）",
            _ => null,
        };

        private string PauseText(GameState s, ActionState a) => a.PauseReason switch
        {
            PauseReason.FireOut => "火堆熄灭",
            PauseReason.StatZero when a.PauseStat.HasValue => AlertText(a.PauseStat.Value, true),
            PauseReason.StatAlert when a.PauseStat.HasValue => AlertText(a.PauseStat.Value, false),
            _ => "已暂停",
        };

        private string? ExitBlocked(GameState s)
        {
            if (s.Progress.Dead) return "角色已死亡";
            if (DialogueLocked(s)) return "对话中，时间未推进；读完对话后才能退出";
            if (s.Battle != null) return "战斗中不能退出：请用撤退或战斗指令";
            if (s.Sub == null && s.Main == null) return "没有进行中的行动";
            return null;
        }

        /// <summary>
        /// 中央条“退出”（S01 §9）：内核每次推进都是完整的 5 分钟原子段，所以收到请求时总在段边界——立即停止调度后续段，
        /// 未发生的时长/工作不结算、按各玩法释放预留；已提交的时间、消耗、收获不回滚。子行动退出后回原主行动暂停态（父行动不解除占用）。
        /// 点火、自用、融合开始时已提交：返回 Ok 且 Deferred=true，本段照常完成，不退还。对话中、战斗中、死亡后拒绝。
        /// </summary>
        public ActionResult RequestExit(GameState s)
        {
            var why = ExitBlocked(s);
            if (why != null) return ActionResult.Fail(why);
            var a = (s.Sub ?? s.Main)!;
            if (IsSingleTransaction(a.Type))
                return new ActionResult { Ok = true, Deferred = true, ActionId = a.ActionId, Reason = $"{a.Label}已开始结算，本段完成后结束（已提交的消耗与效果不撤销）" };
            return Cancel(s);
        }
    }
}
