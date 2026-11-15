using System.Collections.Generic;
using System.Linq;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>
    /// 通用「暂停剩余保质 R／恢复 C＋R」（B51a，S08 文首 Q701.1、B51 Q722—Q724；晾制 Q215／Q666 同一口径）：
    /// 生产成功开工时只对本单实际绑定的有期限输入记 R＝原截止 E − 开工分钟 S（R＞0，开工已到期的原拒绝，不在这里救活）；
    /// 保护期间不腐败、不倒计时，所有到期判断（IsSpoiled、到期转换、继续／完成复核）认同一 R；真实释放（取消、中止、失效、终局）在释放分钟 C 恢复截止 C＋R。
    /// 生产输入保留原截止 E 只作合堆／根卡／批次键（不改合堆、卡位），晾制位内原料沿 B22k 起截止暂为空；两者释放都走 <see cref="ResumeShelf"/>。
    /// </summary>
    public sealed partial class GameEngine
    {
        /// <summary>真实保护份额的剩余（逐字稿 B51 表「真实保护份额的剩余」「已保护在办尸体剩余说明」）：「保质暂停，剩余{时长}」。</summary>
        public static string ShelfPausedText(int remaining) => $"保质暂停，剩余{Clock.Duration(remaining)}";

        /// <summary>全保护但 R 不同的卡片短句（逐字稿 B51 表）：「保质暂停 · {批数}批」。</summary>
        public static string ShelfPausedBatchesText(int batches) => $"保质暂停 · {batches}批";

        /// <summary>只有将绑定有期限原料才显示的预估说明（逐字稿 B51 表，Q724）。</summary>
        public const string ProductionShelfNote = "开工后，所选原料暂停保质；退料恢复剩余保质。";

        /// <summary>这份物品正暂停保质（有可信 R）。</summary>
        public static bool ShelfPaused(ItemInstance i) => i.ShelfPausedMinutes != null;

        /// <summary>
        /// 有效截止（只读）：暂停保质的按「现在立即释放」的 C＋R；其余为原截止；无期限为 null。供排序与只读预告使用，不改状态。
        /// </summary>
        public static int? EffectiveExpiry(GameState s, ItemInstance i) =>
            i.ShelfPausedMinutes is int r ? s.GameMinute + r : i.ExpiresAt;

        /// <summary>
        /// 开工暂停一份输入的保质：有截止、尚未暂停、截止晚于现在时记 R＝E−S（原截止保留作合堆键）；无期限不造 R、已暂停不重记。返回是否新记了 R。
        /// </summary>
        internal static bool PauseShelf(GameState s, ItemInstance i)
        {
            if (i.ShelfPausedMinutes != null || i.ExpiresAt is not int e || e <= s.GameMinute || IsSpoiledMatter(i)) return false;
            i.ShelfPausedMinutes = e - s.GameMinute;
            return true;
        }

        /// <summary>释放一份暂停保质的物品：新截止＝现在 C＋R 并清空 R（不恢复完整初始保质、不补回开工前时间）；没有 R 的原样，返回 null。</summary>
        internal static int? ResumeShelf(GameState s, ItemInstance i)
        {
            if (i.ShelfPausedMinutes is not int r) return null;
            i.ExpiresAt = s.GameMinute + r;
            i.ShelfPausedMinutes = null;
            return i.ExpiresAt;
        }

        /// <summary>一份真实恢复（或只读预告）的保质：返还目的地 owner、卡侧、原物 ID、恢复后的精确截止、份数。</summary>
        internal readonly struct ShelfRestore
        {
            public ShelfRestore(string owner, string? side, string itemId, int expiresAt, int count)
            {
                Owner = owner; Side = side; ItemId = itemId; ExpiresAt = expiresAt; Count = count;
            }
            public string Owner { get; }
            public string? Side { get; }
            public string ItemId { get; }
            public int ExpiresAt { get; }
            public int Count { get; }
        }

        /// <summary>Release／ReleaseToSources 本次实际恢复的保质（取消、中止的结果句读它；为 null 时不收集）。</summary>
        private List<ShelfRestore>? _restoreSink;

        /// <summary>释放时恢复这份物品的保质并（需要时）记下恢复分句的素材；owner 为释放后的真实归属。</summary>
        private void ResumeReleased(GameState s, ItemInstance inst)
        {
            if (ResumeShelf(s, inst) is int at) _restoreSink?.Add(new ShelfRestore(inst.Owner, CardSideOf(inst), inst.ItemId, at, inst.Count));
        }

        /// <summary>
        /// 恢复保质分句（逐字稿 B51 表与 Q722／Q723 拼接合同）：同返还目的地／物主／卡侧内同原料名＋同一精确截止合计份数，不同截止分句，
        /// 组间沿返还清单首次出现次序；结果「{原料名}×{数量}恢复剩余保质，保质到{日期时刻}。」，预告「{原料名}×{数量}退回后恢复剩余保质，预计保质到{日期时刻}。」。
        /// 没有恢复为空串。
        /// </summary>
        internal string RestoreClauses(IEnumerable<ShelfRestore> list, bool preview)
        {
            return string.Concat(RestoreGroups(list).Select(g => preview
                ? $"{g.name}×{g.n}退回后恢复剩余保质，预计保质到{Clock.DayTime(g.at)}。"
                : $"{g.name}×{g.n}恢复剩余保质，保质到{Clock.DayTime(g.at)}。"));
        }

        /// <summary>
        /// 只读返料预告（restorePreview）：按现在立即合法退出会返还的目的地（撤退离地保留按开工来源，其余回当前地面）与 C＋R 现值算恢复分句，不改真实状态。
        /// </summary>
        internal string RestorePreview(GameState s, ActionState a, bool bySource)
        {
            var list = new List<ShelfRestore>();
            if (bySource)
            {
                // B54（Q787）：撤退离地保留按来源返还与提交同源（KeptReturnPlan）：随身放不下的份额按原工作地面预告
                foreach (var p in KeptReturnPlan(s, a))
                {
                    var inst = FindItem(s, p.InstanceId);
                    if (inst == null || inst.ReservedBy != a.ActionId || inst.ShelfPausedMinutes is not int r) continue;
                    list.Add(new ShelfRestore(p.Dest, CardSideOf(inst), inst.ItemId, s.GameMinute + r, p.Count));
                }
                return RestoreClauses(list, true);
            }
            foreach (var id in a.ReservedItemIds)
            {
                var inst = FindItem(s, id);
                if (inst == null || inst.ReservedBy != a.ActionId || inst.ShelfPausedMinutes is not int r) continue;
                list.Add(new ShelfRestore(s.Ground, CardSideOf(inst), inst.ItemId, s.GameMinute + r, inst.Count));
            }
            return RestoreClauses(list, true);
        }

        /// <summary>
        /// 生产开工的同一事务里暂停本单实际绑定输入的保质（Q701.1）：只对已成功预留给本行动的份额；工具、维修目标、未投入余料不动。
        /// </summary>
        private static void PauseWorkInputs(GameState s, ActionState a)
        {
            foreach (var id in a.ReservedItemIds)
            {
                var inst = s.Items.FirstOrDefault(i => i.InstanceId == id);
                if (inst != null && inst.ReservedBy == a.ActionId) PauseShelf(s, inst);
            }
        }

        /// <summary>原料暂停保质适用的生产行动（Q701.1：制作含固定等待、建造、维护修补、拆解、处理尸体）。</summary>
        public static bool ShelfPauseWork(ActionType t) =>
            t is ActionType.Craft or ActionType.Build or ActionType.Repair or ActionType.Salvage or ActionType.Butcher;

        /// <summary>
        /// 孤立保护收束（Q701.1「已失去工作归属的孤立保护不得永久保鲜」）：不在晾制位、也不是在办行动（主行动、子行动、暂存原工作）仍绑定的预留，
        /// 却带暂停 R 的物品，按现在 C＋R 恢复后释放保护；不凭空创建工作、不改归属与预留标记。设施批次（陶窑／净水台）与锅受热保护不在此列。
        /// 读档时调用（幂等）；返回收束了几份。
        /// </summary>
        public static int SweepOrphanShelf(GameState s)
        {
            var live = new HashSet<(int action, int item)>();
            foreach (var a in new[] { s.Main, s.Sub, s.StashedWork })
                if (a != null) foreach (var id in a.ReservedItemIds) live.Add((a.ActionId, id));
            var batches = new HashSet<int>();
            foreach (var ls in s.Locations.Values)
            {
                foreach (var b in ls.Facility.StationBatches) batches.Add(b.BatchId);
                if (ls.Safehouse != null) foreach (var b in ls.Safehouse.Interior.StationBatches) batches.Add(b.BatchId);
            }
            var n = 0;
            foreach (var i in s.Items)
            {
                if (i.ShelfPausedMinutes == null || i.Owner.StartsWith(ItemInstance.DryingPrefix)) continue;
                if (i.ReservedBy is int rb && (live.Contains((rb, i.InstanceId)) || batches.Contains(rb))) continue;
                ResumeShelf(s, i);
                n++;
            }
            return n;
        }
    }
}
