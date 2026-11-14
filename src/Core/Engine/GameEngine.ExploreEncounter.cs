using System;
using System.Linq;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>
    /// BM5c 探索遭遇时序（R05 文首「Q605用户新增：探索随机突遇与在地到期分流」「BM5c Q620—Q624」，S01／S02／S03／R03 同批节，
    /// S14 Q605-A01—A08、R05 Q620-A01—A04／Q621-A01／Q624-A01；逐字稿 Q605、Q620—Q628 节；开发记录 BM5c）。
    /// - 探索自身随机遭遇：新开探索在开工事务里锁原一次命中签与唯一检查段 K，第 K 个实际探索段（或更早的实际完成边界）末按当刻真实资格与密度比较一次；
    ///   命中且有合法名单就把名单锁成组卡并直接被动入战（来源「探索遭遇」），未完的同一次探索暂停保留，完成边界则成果先交。
    /// - 原地普通敌踪到期：同一次未结束的探索（运行、暂停、补给／休息子行动、随机战后暂停、回原地的撤退暂存）在原探索地点／区域延后主动开战，
    ///   正常完成、合法退出或自动中止的结束边界才按原到期顺序检查；成功开工时已到期的也一样（Q620）。清理与其他行动不套这里。
    /// </summary>
    public sealed partial class GameEngine
    {
        /// <summary>逐字稿 Q605／Q620—Q628：随机遭遇短提示，也是完成边界命中时的日志全文。</summary>
        public const string ExploreEncounterText = "探索中遭遇敌人，即将进入战斗。";
        /// <summary>逐字稿 Q605：此类真实入战的日志身份（战报入战方式前缀）。</summary>
        public const string ExploreEncounterSource = "探索遭遇";
        /// <summary>逐字稿 Q621：未完随机暂停原因。</summary>
        public const string ExploreEncounterPauseNote = "探索遭遇";
        /// <summary>逐字稿 Q624：仅旧在办、缺新检查段的探索，追加在中央推进条／暂停层的行动名说明。</summary>
        public const string LegacyExploreNote = "本次探索沿原开工规则，在结束时检查随机遭遇。";

        /// <summary>
        /// 新开探索锁唯一检查段（Q605 §2）：N = max(1, ceil(开工时真实预计分钟 / 5))，K 在 1…max(1, N−1) 间均匀取整数（与命中签独立的一次抽签；
        /// 只有一个可选值时不抽），预计与中央条／正式预览同源（完整倍率与余量）。在 Start 已把行动放进主行动位、锁好余量之后调用。
        /// </summary>
        private void LockExploreCheck(GameState s, ActionState a)
        {
            a.EncounterChecked = false;
            a.EncounterPending = false;
            // 预览／估时副本：命中签已改为不命中，检查段不影响模拟结果；不嵌套估时、不推进副本随机
            if (s.SimCopy) { a.EncounterSegment = 1; return; }
            var minutes = EstimateEndMinute(s) is int end ? end - s.GameMinute : FallbackWorkMinutes(s, a);
            var n = Math.Max(1, (int)Math.Ceiling(minutes / (double)Tick - Eps));
            var top = Math.Max(1, n - 1);
            a.EncounterSegment = top == 1 ? 1 : 1 + (int)Math.Min(top - 1, Math.Floor(s.Rng.NextDouble() * top));
        }

        /// <summary>模拟给不出可信结束（途中终局等）时，按本段倍率与剩余需求工作量折算的预计分钟（只用于选检查段，不改真实进度）。</summary>
        private int FallbackWorkMinutes(GameState s, ActionState a)
        {
            var need = Math.Max(0, a.BaseWorkMinutes - a.CarryReserved - a.WorkDone);
            var factor = ComputeSegment(s, a).Factor;
            return (int)Math.Ceiling(need * factor - Eps);
        }

        /// <summary>这次探索是旧开工规则（缺新检查段、检查还没做）：只在实际完成边界检查一次（Q605 §5、Q624）。</summary>
        public static bool LegacyExploreCheck(ActionState a) =>
            a.Type == ActionType.Explore && a.EncounterSegment == null && !a.EncounterChecked && !a.EncounterPending;

        /// <summary>Q624：中央推进条／暂停层行动名说明里追加的旧在办兼容句；不适用为 null（新工作、已检查、其他行动）。</summary>
        public static string? LegacyExploreNoteFor(ActionState a) => !a.IsSub && LegacyExploreCheck(a) ? LegacyExploreNote : null;

        /// <summary>本段末是不是这次探索的唯一检查点（新规则、还没检查、已实际提交满 K 段）。</summary>
        private bool ExploreCheckDueMidway(ActionState a) =>
            a.Type == ActionType.Explore && !a.IsSub && a.EncounterSegment is int k && !a.EncounterChecked && !a.EncounterPending
            && a.ElapsedMinutes / Tick >= k;

        /// <summary>
        /// 一次探索的唯一随机遭遇检查（Q605 §2—§4、Q621）。completed：探索已在本段实际完成且成果已提交（a 已离开行动位）。
        /// 先记已检查；强制对白先承接时改记待处理，由对白结束的同一安全边界接续（<see cref="ExploreCheckBoundary"/>）。
        /// 按原单次风险公式与检查当刻的真实资格／本地密度比较原命中签；命中且原合法名单非空才入战。
        /// </summary>
        private void ExploreEncounterCheck(GameState s, ActionState a, bool completed)
        {
            if (a.EncounterChecked) return;
            if (s.Progress.Dead) { a.EncounterChecked = true; a.EncounterPending = false; return; }
            if (DialogueLocked(s))
            {
                a.EncounterPending = true;
                if (completed) s.ExploreCheckPending = a.Clone();
                return;
            }
            a.EncounterPending = false;
            a.EncounterChecked = true;
            if (InCombatFlow(s)) return;
            var loc = s.CurrentLocation;
            // 原地点／区域已不是这次探索的地方（不会追到异地）：消耗这次检查
            if (loc != a.SourceLocation || s.CurrentZone != (a.StartZone ?? ExteriorZone)) return;
            var p = EncounterChance(s, loc, ActionType.Explore);
            if (p <= 0 || a.EncounterRoll >= p) return;
            var members = GroupCardCandidates(s, loc);
            if (members.Count == 0) return;
            StartExploreEncounter(s, a, loc, members, completed);
        }

        /// <summary>
        /// 命中后的真实入战（Q605 §2、§4，Q621）：名单按成组时容量锁成组卡（来源 explore；单卡入组后原记录作废），以已到期身份保存，不再抽接近时限；
        /// 未完的同一次探索在本段已完整提交后暂停（原因「探索遭遇」，已暂停的不伪造第二次暂停），日志按三态全文只写一条；
        /// 入战按原被动接敌口径（受惊情境系数 encounter、敌方单方先手），战报身份「探索遭遇」。
        /// </summary>
        private void StartExploreEncounter(GameState s, ActionState a, string loc, System.Collections.Generic.List<WorldEnemy> members, bool completed)
        {
            var g = AddGroupCard(s, loc, members, "explore", s.GameMinute);
            var key = GroupCardKey(g.Id);
            var t = TimerFor(s, key);
            t.Left = 0;
            t.DueMinute = s.GameMinute;
            var m = s.Main;
            var highlight = members.Select(w => $"enemy:{w.Id}");
            if (!completed && m != null && m.ActionId == a.ActionId && m.Status == ActionStatus.Running)
            {
                m.Status = ActionStatus.Paused;
                m.PauseReason = PauseReason.ExploreEncounter;
                m.PauseStat = null;
                m.PauseNote = ExploreEncounterPauseNote;
                EmitPrompt(s, GameEventKind.ActionPaused, ExploreEncounterText + (ExitLocked(m) ? $"{m.Label}已暂停，战后可以继续。" : $"{m.Label}已暂停，战后可以继续或退出。"), highlight);
            }
            else EmitPrompt(s, GameEventKind.Encounter, ExploreEncounterText, highlight);
            StartBattle(s, members, false, ExploreEncounterSource, FearEncounter, cardKey: key, firstStrike: FirstStrikeEnemy);
            if (s.Battle != null) s.Battle.ExploreEncounter = true;
        }

        /// <summary>写入战日志并标记它是要播短提示的那一次真实入战（壳据此在可操作战斗画面之前播一次提示；读档不重播）。</summary>
        private void EmitPrompt(GameState s, GameEventKind kind, string text, System.Collections.Generic.IEnumerable<string> highlight)
        {
            Emit(s, kind, text, null, highlight);
            s.PendingEvents[s.PendingEvents.Count - 1].EncounterPrompt = true;
        }

        /// <summary>
        /// 强制对白结束的安全边界（Q605 §2 末段）：接续一次待处理的探索检查——未完探索在主行动位，完成边界的取快照。不额外推进 5 分钟、不重抽。
        /// </summary>
        private void ExploreCheckBoundary(GameState s)
        {
            if (DialogueLocked(s)) return;
            if (s.ExploreCheckPending is ActionState done)
            {
                s.ExploreCheckPending = null;
                done.EncounterPending = false;
                ExploreEncounterCheck(s, done, true);
            }
            if (s.Main is ActionState m && m.Type == ActionType.Explore && m.EncounterPending)
            {
                m.EncounterPending = false;
                ExploreEncounterCheck(s, m, false);
            }
        }

        /// <summary>
        /// Q605 §1、Q620：队伍此刻在一项未结束探索的原地点／区域（主行动位上的探索，运行、暂停、有补给／休息子行动都算），
        /// 该地普通敌踪到期主动接近延后到这次探索结束。离开原地点（异地、移动途中另有规则）不保护。
        /// </summary>
        private static bool ExploreDefersApproach(GameState s) =>
            s.Main is ActionState m && m.Type == ActionType.Explore && !m.IsSub
            && s.CurrentLocation == m.SourceLocation && s.CurrentZone == (m.StartZone ?? ExteriorZone);
    }
}
