using System.Collections.Generic;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>
    /// B31c 结果提示（《重墨 V2 开工裁定与界面逐字稿》§1.1 Q330；02 待确认 Q330 选 A）：
    /// 「危险」（<see cref="GameEventKind.Hazard"/>）与「行动中止」（<see cref="GameEventKind.ActionAborted"/>）是关键提示，
    /// 保持显示到玩家关闭该条或下一次有效提交的玩法操作；浏览、开关菜单、预览、未提交草稿与被拒请求都不清除。
    /// 连续发生关键提示时显示最新一条（原内容仍在页脚与行动日志）。一般提示沿约 8 秒收起，自动到达的一般提示不顶替尚未处理的关键提示。
    /// 存档失败与「重试」沿 Q282（底栏，不走这里）；死亡等专用层按原优先级（死亡结算接管时不显示结果提示）。
    /// 网页壳只按这里的判定增删提示，自动测试逐条对照。
    /// </summary>
    public static class ResultToast
    {
        /// <summary>一般提示自动收起的停留时长（S14 §8【实现同步】的约 8 秒；阅读停留时长，不乘动画速度倍率）。</summary>
        public const int NormalDismissMs = 8000;

        /// <summary>Q330 关键提示：危险与行动中止两类。</summary>
        public static bool IsCritical(GameEventKind kind) => kind == GameEventKind.Hazard || kind == GameEventKind.ActionAborted;

        public static bool IsCritical(GameEvent? toast) => toast != null && IsCritical(toast.Kind);

        /// <summary>
        /// 事件进结果提示的档次（沿原 Drain 规则）：2 = 角色行动结果类（完成、中止、取消、危险、死亡、敌踪、战斗结束），同一批里后到的顶替先到的；
        /// 1 = 非阻断短通知与设施完成，只在同一批没有 2 档结果时才显示；0 = 不进结果提示。
        /// </summary>
        public static int Rank(GameEventKind kind)
        {
            switch (kind)
            {
                case GameEventKind.ActionCompleted:
                case GameEventKind.ActionAborted:
                case GameEventKind.ActionCancelled:
                case GameEventKind.Hazard:
                case GameEventKind.Death:
                case GameEventKind.Encounter:
                case GameEventKind.BattleEnd:
                    return 2;
                case GameEventKind.Notice:
                case GameEventKind.FacilityDone:
                    return 1;
                default:
                    return 0;
            }
        }

        /// <summary>
        /// 一批新事件到达后结果提示该显示哪一条：沿原档次规则；正在显示（尚未处理）的关键提示不被一般提示顶替，
        /// 新的关键提示顶替旧的（显示最新关键结果）。返回值与 <paramref name="current"/> 引用相同表示不变。
        /// </summary>
        public static GameEvent? Pick(GameEvent? current, IEnumerable<GameEvent> batch)
        {
            var toast = current;
            var setThisBatch = false;
            foreach (var ev in batch)
            {
                if (ev == null || string.IsNullOrEmpty(ev.Text)) continue;
                var rank = Rank(ev.Kind);
                if (rank == 0) continue;
                if (IsCritical(toast) && !IsCritical(ev.Kind)) continue;
                if (rank == 2) { toast = ev; setThisBatch = true; }
                else if (!setThisBatch) toast = ev;
            }
            return toast;
        }

        /// <summary>这条提示是否按约 8 秒自动收起（关键提示不自动收起）。</summary>
        public static bool AutoDismiss(GameEvent? toast) => toast != null && !IsCritical(toast.Kind);

        /// <summary>
        /// 只读浏览（选中卡片查看等）时：一般提示照原规则收起（试玩修复 G5，避免挡住操作），关键提示保留。
        /// </summary>
        public static GameEvent? OnBrowse(GameEvent? current) => IsCritical(current) ? current : null;

        /// <summary>
        /// 有效提交标记：内核每接受一次带请求号的玩法提交就把请求号记进 <see cref="GameState.RecentRequestIds"/>
        /// （被拒、重复、预览、草稿不记）。网页壳比较前后两次标记，不同即“下一次有效提交玩法操作”已发生。
        /// </summary>
        public static string CommitMark(GameState s)
        {
            var ids = s?.RecentRequestIds;
            return ids == null || ids.Count == 0 ? "" : ids[ids.Count - 1] + "#" + ids.Count;
        }

        /// <summary>两次标记之间有没有新的有效提交。</summary>
        public static bool Committed(string? before, string after) => before != null && before != after;

        /// <summary>
        /// B31d（逐字稿 §1.2 Q337 选 C）：不带请求号的 0 分钟操作哪些算“有效提交”。成功完成的交谈、确认远行（「准备好了」）与
        /// 「还没准备好」（远行准备里对来客说「先准备」）算提交，清掉当前提示（含危险、行动中止）；同效入口经同一内核方法受理的一并算
        /// （交谈含序章来客、城市岗位与招募对象，都走 <see cref="GameEngine.TalkNpc"/>）。查看文书、路线日志与任务回顾仍是浏览，不清。
        /// 失败或重复请求不清（沿 Q330）；成功动作自己带来新的关键提示时，先清旧的再显示新结果（同 <see cref="Pick"/>）。
        /// </summary>
        public static bool CommitsOnSuccess(ZeroMinuteOp op) =>
            op == ZeroMinuteOp.Talk || op == ZeroMinuteOp.ConfirmJourney || op == ZeroMinuteOp.NotReady;

        /// <summary>这次 0 分钟操作的结果要不要清掉当前提示：只有成功、非重复、且属于提交的那三类。</summary>
        public static bool ClearsOn(ZeroMinuteOp op, ActionResult? res) =>
            res != null && res.Ok && !res.Duplicate && CommitsOnSuccess(op);
    }

    /// <summary>B31d（Q337）：不带请求号的 0 分钟操作种类（网页壳调用时标明，交给 <see cref="ResultToast.ClearsOn"/> 判断）。</summary>
    public enum ZeroMinuteOp
    {
        /// <summary>交谈（序章来客、城市岗位、招募对象）。</summary>
        Talk,
        /// <summary>远行准备「准备好了」（确认远行）。</summary>
        ConfirmJourney,
        /// <summary>远行准备「先准备」（还没准备好）。</summary>
        NotReady,
        /// <summary>查看文书、告示与路标。</summary>
        ReadDocument,
        /// <summary>路线日志（查看路线）。</summary>
        RouteLog,
        /// <summary>任务回顾（回看委托对白、外部医疗线索）。</summary>
        ReviewQuest,
    }
}
