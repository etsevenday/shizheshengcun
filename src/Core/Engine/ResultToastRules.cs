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
    }
}
