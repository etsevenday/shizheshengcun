using System.Linq;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>BM5b1（S14 Q601）：中央加工菜单主层上一份加工位食材的入口（「移入锅（加工位{目标序号}）」）。</summary>
    public sealed class MoveToPotOption
    {
        public int FromSlot { get; set; }
        public int ToSlot { get; set; }
        public ItemInstance Food { get; set; } = null!;
        public ItemInstance Pot { get; set; } = null!;
        /// <summary>按钮文字「移入锅（加工位{目标序号}）」。</summary>
        public string Label { get; set; } = "";
        /// <summary>按钮说明「将加工位{源序号}的{食材名}移入加工位{目标序号}的{锅名}。原直烤受热进度不继承。」。</summary>
        public string Hint { get; set; } = "";
        /// <summary>不能移入时的原具体原因（按钮禁用并显示）；可移入为 null。</summary>
        public string? Reason { get; set; }
        public InstantRequest Request { get; set; } = null!;
    }

    /// <summary>
    /// BM5b1「同火堆食材明确移入锅」（S14「2026-11-14 B45b Q601—Q604：独立加工目标、明确入锅与键盘收尾」Q601；
    /// 正式句见《重墨 V2 开工裁定与界面逐字稿》「2026-11-14 B45b Q601—Q604：明确入锅与焦点逐字」）。
    /// 原子等价“取到当前地面，再放入指定锅”：两步的最终合法状态先完整预验，任一失效全部原位；成功 0 世界分钟、只移这一份。
    /// </summary>
    public sealed partial class GameEngine
    {
        /// <summary>逐字稿 Q601：非同一火源的请求。</summary>
        public const string MoveToPotSameFireText = "只能在同一火堆的加工位之间移入锅。";
        /// <summary>逐字稿 Q601：旧直烤已有大于 0 的受热时，成功日志后接这句。</summary>
        public const string MoveToPotResetText = "原直烤受热进度已清零。";

        /// <summary>逐字稿 Q601：按钮「移入锅（加工位{目标序号}）」（序号从 1 起）。</summary>
        public static string MoveToPotLabel(int toNo) => $"移入锅（加工位{toNo}）";

        /// <summary>逐字稿 Q601：按钮说明（序号从 1 起）。</summary>
        public static string MoveToPotHint(int fromNo, string food, int toNo, string pot) =>
            $"将加工位{fromNo}的{food}移入加工位{toNo}的{pot}。原直烤受热进度不继承。";

        /// <summary>逐字稿 Q601：成功日志（序号从 1 起）。</summary>
        public static string MoveToPotDoneText(int fromNo, string food, int toNo, string pot) =>
            $"已将加工位{fromNo}的{food}移入加工位{toNo}的{pot}。";

        /// <summary>逐字稿 Q601：受热保护食材离开原工位就会按原 ExpiresAt 腐败。</summary>
        public static string MoveToPotSpoiledText(string food) => $"{food}取出后已腐败，不能移入锅。";

        /// <summary>两件都在当前区火堆加工位里时，按它们此刻的真实加工位组成移入请求；否则 null（不是本入口的组合）。</summary>
        private InstantRequest? MoveToPotRequest(GameState s, ItemInstance food, ItemInstance pot)
        {
            if (!food.InFacility || !pot.InFacility || IsPot(food) || !IsPot(pot)) return null;
            if (food.Owner != pot.Owner) return new InstantRequest { Kind = InstantKind.MoveToPot, InstanceId = food.InstanceId, TargetInstanceId = pot.InstanceId, ExpectItemId = food.ItemId };
            var from = FireSlotOf(s, food);
            var to = FireSlotOf(s, pot);
            if (from < 0 || to < 0 || from == to) return null;
            return InstantRequest.MoveToPot(food.InstanceId, from, food.ItemId, pot.InstanceId, to);
        }

        /// <summary>
        /// 主层加工位卡上的「移入锅（加工位{目标序号}）」入口：只在这一位是真实食材、同一火源另一位确有锅时给出；
        /// 不能移入时带原具体原因（按钮禁用，不造可点的伪成功）。其余（空位、锅、煅烧批、另一位没有锅）为 null。只读。
        /// </summary>
        public MoveToPotOption? MoveToPotOptionFor(GameState s, int fromSlot)
        {
            var here = s.Ground;
            if (!HasFireAt(s, here)) return null;
            var slots = FireSlots(s, here);
            if (fromSlot < 0 || fromSlot >= slots.Count || slots[fromSlot] is not ItemInstance food || IsPot(food)) return null;
            if (_items[food.ItemId].Kind != "food") return null;
            for (var k = 0; k < slots.Count; k++)
            {
                if (k == fromSlot || slots[k] is not ItemInstance pot || !IsPot(pot)) continue;
                var req = InstantRequest.MoveToPot(food.InstanceId, fromSlot, food.ItemId, pot.InstanceId, k);
                var why = DialogueLocked(s) ? DialogueLockedReason : s.Battle != null ? "战斗中不能整理物品；换武器、装填与用药请用战斗指令" : MoveToPotBlocked(s, req);
                return new MoveToPotOption
                {
                    FromSlot = fromSlot, ToSlot = k, Food = food, Pot = pot, Request = req, Reason = why,
                    Label = MoveToPotLabel(k + 1),
                    Hint = MoveToPotHint(fromSlot + 1, ItemName(food.ItemId), k + 1, ItemName(pot.ItemId)),
                };
            }
            return null;
        }

        /// <summary>
        /// 移入锅能否成立（只读）：忙碌等原阻断在前；源实例／源位／预期种类、目标锅／目标位任一变化为「内容已变化，请重新选择。」；
        /// 不是同一火源为「只能在同一火堆的加工位之间移入锅。」；取出资格按原 TakeOutBlocked；受热保护且已过原保质期为「{食材名}取出后已腐败，不能移入锅。」；
        /// 再按“已取到当前地面”的真实资格走原 MaxPutFood（锅满、白名单、腐败等原句）。
        /// </summary>
        public string? MoveToPotBlocked(GameState s, InstantRequest r)
        {
            var busy = HolderBlocked(s);
            if (busy != null) return busy;
            var here = s.Ground;
            var food = FindItem(s, r.InstanceId);
            var pot = r.TargetInstanceId is int pid ? FindItem(s, pid) : null;
            if (food == null || pot == null || !IsPot(pot) || IsPot(food) || !food.InFacility || !pot.InFacility) return ContentChangedText;
            if (!HasFireAt(s, here) || food.Owner != pot.Owner || food.Owner != FireOwner(here)) return MoveToPotSameFireText;
            var slots = FireSlots(s, here);
            if (r.Slot is not int from || r.TargetSlot is not int to || from == to || from < 0 || to < 0 || from >= slots.Count || to >= slots.Count
                || slots[from]?.InstanceId != food.InstanceId || slots[to]?.InstanceId != pot.InstanceId
                || (r.ExpectItemId != null && food.ItemId != r.ExpectItemId))
                return ContentChangedText;
            var take = TakeOutBlocked(s, food, here);
            if (take != null) return take;
            if (food.ExpiresAt is int ex && ex <= s.GameMinute && AllProcesses(s).Any(p => p.HeatProtected && TouchesItem(p, food.InstanceId)))
                return MoveToPotSpoiledText(ItemName(food.ItemId));
            // 第二步按“已取到当前地面”的真实资格预验（只在本方法内临时改归属，返回前复原，不改任何状态）
            var keep = food.Owner;
            food.Owner = here;
            try
            {
                var max = MaxPutFood(s, food, pot, out var why);
                if (why != null) return why;
                if (max < 1) return NoQtyText;
            }
            finally { food.Owner = keep; }
            return null;
        }

        /// <summary>
        /// 明确移入锅：只移所指这一份、释放原工位、移除原直烤工序（受热与保护按原取出语义清除，原期限不刷新）；
        /// 锅内原有水、其他食材与工序不重置，新食材按原入锅工序从起点同步。只记一条真实结果日志。
        /// </summary>
        private ActionResult MoveToPot(GameState s, InstantRequest r)
        {
            var why = MoveToPotBlocked(s, r);
            if (why != null) return ActionResult.Fail(why);
            var food = FindItem(s, r.InstanceId)!;
            var pot = FindItem(s, r.TargetInstanceId!.Value)!;
            var reset = AllProcesses(s).Any(p => TouchesItem(p, food.InstanceId) && p.HeatDoneMinutes > 0);
            var name = ItemName(food.ItemId);
            MoveOne(s, food, ItemInstance.ContainerOwner(pot.InstanceId));
            SyncFacilities(s);
            Emit(s, GameEventKind.Info, MoveToPotDoneText(r.Slot!.Value + 1, name, r.TargetSlot!.Value + 1, ItemName(pot.ItemId)) + (reset ? MoveToPotResetText : ""));
            UpdateFlags(s);
            return ActionResult.Success();
        }
    }
}
