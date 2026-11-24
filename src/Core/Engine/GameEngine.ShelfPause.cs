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

        /// <summary>B51c1（S06／S14 B51 Q773 选 B）：同一显示堆只有部分份数暂停保质时的卡片副标。</summary>
        public const string ShelfPartlyPausedText = "含保质暂停";

        /// <summary>只有将绑定有期限原料才显示的预估说明（逐字稿 B51 表，Q724）；BM11（S08 Q882 用户新裁定）：通用生产说明保留“加工时原料暂停保质。”。</summary>
        public const string ProductionShelfNote = "加工时原料暂停保质。";

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

        /// <summary>释放时恢复这份物品的保质（C＋R）。BM11（S08 Q882 用户新裁定）：不再收集恢复分句素材——返料保质变化不另说明，玩家自行查看详情。</summary>
        private void ResumeReleased(GameState s, ItemInstance inst) => ResumeShelf(s, inst);

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

        /// <summary>
        /// B59b（S11「2026-11-24 Q1033–Q1039」§Q1036、Q1037；S06／S10 文首 Q770 的 2026-11-24 补正）：在某一世界分钟 from 冻结这份物品的真实剩余保鲜量 R＝E−from。
        /// 有截止、尚未冻结、不是腐败物且截止晚于 from 时才记（已有生产／晾制冻结的沿用真实余额，不双重冻结或延长）；clearExpiry 为 true 时原截止暂为空（晾制位口径，Q215／Q666），
        /// 否则保留原截止作合堆／批次键（生产输入与烹饪原料口径，B51a）。返回是否新记了 R。
        /// </summary>
        internal static bool FreezeShelfAt(ItemInstance i, int from, bool clearExpiry)
        {
            if (i.ShelfPausedMinutes != null || i.ExpiresAt is not int e || e <= from || IsSpoiledMatter(i)) return false;
            i.ShelfPausedMinutes = e - from;
            if (clearExpiry) i.ExpiresAt = null;
            return true;
        }

        /// <summary>
        /// B59b（§Q1036）：此刻仍受烹饪保护的原料实例——有效受热保护进程（<see cref="FacilityProcess.HeatProtected"/>）的目标、锅食谱主食材与辅料。
        /// 各区火堆与屋内的加工进程都算（同 <see cref="AllFacilityStates"/> 的范围）。
        /// </summary>
        internal static HashSet<int> CookFrozenIds(GameState s)
        {
            var ids = new HashSet<int>();
            foreach (var ls in s.Locations.Values)
            {
                AddCookFrozen(ids, ls.Facility.Processes);
                if (ls.Safehouse != null) AddCookFrozen(ids, ls.Safehouse.Interior.Processes);
            }
            return ids;
        }

        private static void AddCookFrozen(HashSet<int> ids, IEnumerable<FacilityProcess> processes)
        {
            foreach (var p in processes.Where(p => p.HeatProtected))
            {
                ids.Add(p.TargetInstanceId);
                foreach (var id in p.InputInstanceIds ?? new List<int>()) ids.Add(id);
                foreach (var id in p.AuxInstanceIds ?? new List<int>()) ids.Add(id);
            }
        }

        /// <summary>
        /// B59b（§Q1036「主动取出或原业务作废等导致实际结束保护时，原料按当前时刻加真实冻结余额恢复倒计时」；S10 Q770 补正「退出保护的原料从这次恢复出的实际余量继续计时」）：
        /// 烹饪冻结的原料一旦不再被有效受热保护进程覆盖（取出单份、取下整锅、全部取出、锅食谱作废辅料退回、进程移除等），在这一刻按 C＋R 恢复计时。
        /// 只收未预留、不在晾制位、不在陶窑／净水台批内的冻结物（生产输入由预留绑定、晾制位与设施批各按原规则，不在此列）。
        /// 每次设施同步与每个变更边界调用（幂等）；返回恢复了几份。
        /// </summary>
        internal static int ReleaseCookFreeze(GameState s)
        {
            if (!s.Items.Any(i => i.ShelfPausedMinutes != null && i.ReservedBy == null && !i.InDrying && !i.InStation)) return 0;
            var live = CookFrozenIds(s);
            var n = 0;
            foreach (var i in s.Items)
            {
                if (i.ShelfPausedMinutes == null || i.ReservedBy != null || i.InDrying || i.InStation || live.Contains(i.InstanceId)) continue;
                ResumeShelf(s, i);
                n++;
            }
            return n;
        }

        /// <summary>原料暂停保质适用的生产行动（Q701.1：制作含固定等待、建造、维护修补、拆解、处理尸体）。</summary>
        public static bool ShelfPauseWork(ActionType t) =>
            t is ActionType.Craft or ActionType.Build or ActionType.Repair or ActionType.Salvage or ActionType.Butcher;

        /// <summary>
        /// 孤立保护收束（Q701.1「已失去工作归属的孤立保护不得永久保鲜」）：不在晾制位、不在在办设施批内、不受有效烹饪保护（B59b §Q1036）、也不是在办行动（主行动、子行动、暂存原工作）仍绑定的预留，
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
            var cook = CookFrozenIds(s);   // B59b（§Q1036）：仍受有效烹饪保护的原料随进程冻结，不是孤立保护
            var n = 0;
            foreach (var i in s.Items)
            {
                if (i.ShelfPausedMinutes == null || i.Owner.StartsWith(ItemInstance.DryingPrefix) || cook.Contains(i.InstanceId)) continue;
                // B51c2a1：设施批内原料（无预留标记，归属即绑定）随在办批保护
                if (ItemInstance.TryStation(i.Owner, out _, out var sb) && batches.Contains(sb)) continue;
                if (i.ReservedBy is int rb && (live.Contains((rb, i.InstanceId)) || batches.Contains(rb))) continue;
                ResumeShelf(s, i);
                n++;
            }
            return n;
        }
    }
}
