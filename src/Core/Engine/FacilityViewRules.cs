using System.Collections.Generic;
using System.Linq;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>B32b 火堆右栏层级路径的一级（逐字稿 §1.2 Q339：「{火堆名} › 加工位{序号} › {锅名} › {内容}」逐级返回）。</summary>
    public sealed class FireCrumb
    {
        /// <summary>fire、slot、pot、content。</summary>
        public string Kind { get; set; } = "";
        public string Label { get; set; } = "";
        /// <summary>pot、content 层的实例号（锅里的水为锅号）。</summary>
        public int? InstanceId { get; set; }
    }

    /// <summary>
    /// 开发计划 v1.0 B32b：火堆、晾制架、安全屋卡与右栏的显示口径（逐字稿 §1.2 Q339、Q343、Q350，§1.5 Q362—Q372，§2 P02a—P02f）。
    /// 只读状态与配置，不改玩法；网页壳按这里取副标、角标、加工中样式与固定字样。
    /// </summary>
    public static partial class CardStates
    {
        // ───────────── Q372 三类卡摘要 ─────────────

        /// <summary>Q350：火堆卡名——折叠柴炉为「柴炉」，其余「火堆」。</summary>
        public static string FireName(FacilityState fac) => fac.StoveInstanceId.HasValue ? "柴炉" : "火堆";

        /// <summary>Q372：火堆／柴炉燃烧状态角标「燃烧中」「熄灭」「未点燃」。</summary>
        public static string FireStateText(FacilityState fac) => fac.FireLit ? "燃烧中" : fac.WentOut ? "熄灭" : "未点燃";

        /// <summary>Q372：火堆／柴炉副标「燃料{当前}/{上限}分钟」。</summary>
        public static string FireFuelSub(GameEngine e, FacilityState fac) => $"燃料{fac.FuelMinutes}/{e.FuelCapOf(fac)}分钟";

        /// <summary>Q372：两个加工位均占用时另显示的角标。</summary>
        public const string FireSlotsFull = "加工位满";

        /// <summary>P02a：右栏工位名「加工位{序号}」（序号从 1 起）。</summary>
        public static string FireSlotName(int slotIndex) => $"加工位{slotIndex + 1}";

        /// <summary>P02a、Q343：右栏煅烧规则行标题。</summary>
        public const string CharcoalRuleTitle = "木炭工位";

        // ───────────── Q365 煅烧投料盘 ─────────────

        /// <summary>Q365：煅烧盘「耗时」行「投料0分钟 · 需{有效受热分钟}有效受热分钟」。</summary>
        public static string CharcoalTimeText(CharcoalLoadInfo info) => $"投料0分钟 · 需{info.HeatMinutes}有效受热分钟";

        /// <summary>Q365：煅烧盘「工位」行——这一批实际投进的加工位（第一个空位）；没有空位为 null（省略该行）。</summary>
        public static string? CharcoalSlotText(GameEngine e, GameState s)
        {
            var k = e.FireSlots(s, s.Ground).FindIndex(x => x == null);
            return k < 0 ? null : FireSlotName(k);
        }

        // ───────────── Q366 点火盘 ─────────────

        /// <summary>P02e 未选工具时的提示。</summary>
        public const string IgnitePickHint = "请选择打火器。";

        /// <summary>Q366：点火盘「火堆」行「{火堆或柴炉} · 剩余燃料{分钟数}分钟」。</summary>
        public static string IgniteFireLine(FacilityState fac) => $"{FireName(fac)} · 剩余燃料{fac.FuelMinutes}分钟";

        /// <summary>P02e：提交按钮「点火（{时长}）」。</summary>
        public static string IgniteButton(int minutes) => $"点火（{Clock.Duration(minutes)}）";

        // ───────────── P02c 晾制位 ─────────────

        /// <summary>P02c：晾制位名「工位{序号}」。</summary>
        public static string DryingSlotName(int slotIndex) => $"工位{slotIndex + 1}";

        /// <summary>P02c：晾制位状态「空闲」「晾制中」「可收取」，原料腐败沿现行阻断原因「原料已腐败」。</summary>
        public static string DryingStatusText(string status) => status switch
        {
            "empty" => "空闲",
            DryingSlot.Drying => "晾制中",
            DryingSlot.Done => "可收取",
            _ => "原料已腐败",
        };

        /// <summary>Q364：晾制收取居中盘标题与两键。</summary>
        public const string HarvestTitle = "收取";
        public const string HarvestToCarried = "取到随身";
        public const string HarvestToGround = "取到地面";

        // ───────────── P02d、Q369 安全屋休息条件 ─────────────

        /// <summary>
        /// Q347、Q369：安全屋面板「休息条件」各行。屋内沿现行「屋内遮蔽，休息时精神每小时额外+2」；屋外先写当前区实际休息遮蔽「遮蔽」或「露天」，
        /// 再写「进入屋内休息精神每小时额外+2」（只说明屋内加成，屋外休息与野营布睡眠判定按原规则）。
        /// </summary>
        public static List<string> SafehouseRestLines(GameEngine e, GameState s)
        {
            var bonus = Fmt.SignedT(e.Config.FacilityEffects.ShelteredCampRestMoralePerHour);
            if (s.CurrentZone == GameState.SafehouseZone) return new List<string> { $"屋内遮蔽，休息时精神每小时额外{bonus}" };
            return new List<string> { e.IsSheltered(s, s.Ground) ? "遮蔽" : "露天", $"进入屋内休息精神每小时额外{bonus}" };
        }

        // ───────────── Q371 主角食饮描边 ─────────────

        /// <summary>Q371：两次左栏数值对比，只列实际变化的项（封顶等无变化的不列）。</summary>
        public static List<StatId> ChangedStats(Stats before, Stats after) =>
            Stats.All.Where(id => System.Math.Abs(before.Get(id) - after.Get(id)) > 1e-9).ToList();

        /// <summary>Q371：这个行动请求是不是食饮（吃、喝；含给同行者食饮）——只有食饮才描边。</summary>
        public static bool IsEatOrDrink(GameEngine e, GameState s, ActionRequest r)
        {
            if (r.Type != ActionType.Use) return false;
            if (r.Feeding != null) return true;
            var it = r.InstanceId.HasValue ? e.FindItem(s, r.InstanceId.Value) : null;
            return it != null && (e.IsLiquid(it) || e.Item(it.ItemId).Kind is "food" or "water");
        }
    }

    public sealed partial class GameEngine
    {
        // ───────────── Q362 加工中 ─────────────

        /// <summary>
        /// Q362：火堆／柴炉某加工位此刻是否实际受热——火堆本段能加热，且该位的物品（锅连同锅里的水与食材）有满足受热条件的加工记录。
        /// 已完成、空位、受热暂停（火灭、燃料不足一段）或没有有效热量（缺水、腐败等）为 false。
        /// </summary>
        public bool FireSlotHeating(GameState s, string place, int slotIndex)
        {
            if (!FireHot(s, place)) return false;
            var slots = FireSlots(s, place);
            if (slotIndex < 0 || slotIndex >= slots.Count) return false;
            var it = slots[slotIndex];
            if (it == null) return false;
            var ids = new HashSet<int> { it.InstanceId };
            if (IsPot(it)) foreach (var c in PotContents(s, it.InstanceId)) ids.Add(c.InstanceId);
            var fac = FacOf(s, place)!;
            return fac.Processes.Any(p => p.Status != ProcessStatus.Done
                && (ids.Contains(p.TargetInstanceId) || p.PotInstanceId == it.InstanceId || (p.InputInstanceIds?.Any(ids.Contains) ?? false))
                && HeatEligible(s, place, p));
        }

        /// <summary>Q362：火堆／柴炉卡加工中——任一加工位实际受热。</summary>
        public bool FireHeating(GameState s, string place)
        {
            var n = FireSlots(s, place).Count;
            for (var k = 0; k < n; k++) if (FireSlotHeating(s, place, k)) return true;
            return false;
        }

        /// <summary>Q362：当前区晾制架某位正在晾制。</summary>
        public bool DryingSlotBusy(GameState s, int slotIndex) => DryingSlotAt(s, s.Ground, slotIndex)?.Status == DryingSlot.Drying;

        /// <summary>Q362：晾制架卡加工中——任一位晾制中。</summary>
        public bool DryingBusy(GameState s) => (FacOf(s, s.Ground)?.DryingSlots ?? new List<DryingSlot>()).Any(b => b.Status == DryingSlot.Drying);

        // ───────────── Q339 右栏层级路径 ─────────────

        /// <summary>某物品（含锅里的水与食材）在当前区火堆的加工位序号（0 起）；不在当前区火堆里为 -1。</summary>
        public int FireSlotOf(GameState s, ItemInstance item)
        {
            if (!HasFireAt(s, s.Ground)) return -1;
            var top = item.InContainer ? ContainerOf(s, item) : item;
            if (top == null) return -1;
            return FireSlots(s, s.Ground).FindIndex(x => x != null && x.InstanceId == top.InstanceId);
        }

        /// <summary>
        /// Q339：右栏层级路径「{火堆名} › 加工位{序号} › {锅名} › {内容}」。target 为 null 时只有火堆一级；
        /// liquidId 给锅里的水（选中 PotLiquid 时 target 是锅）。target 不在当前区火堆里为 null。
        /// </summary>
        public List<FireCrumb>? FireCrumbs(GameState s, ItemInstance? target, string? liquidId = null)
        {
            if (!HasFireAt(s, s.Ground)) return null;
            var fac = FacOf(s, s.Ground)!;
            var list = new List<FireCrumb> { new FireCrumb { Kind = "fire", Label = CardStates.FireName(fac) } };
            if (target == null) return list;
            var k = FireSlotOf(s, target);
            if (k < 0) return null;
            list.Add(new FireCrumb { Kind = "slot", Label = CardStates.FireSlotName(k) });
            var pot = target.InContainer ? ContainerOf(s, target) : IsPot(target) ? target : null;
            if (pot != null) list.Add(new FireCrumb { Kind = "pot", Label = ItemName(pot.ItemId), InstanceId = pot.InstanceId });
            if (liquidId != null) list.Add(new FireCrumb { Kind = "content", Label = ItemName(liquidId), InstanceId = pot?.InstanceId });
            else if (pot == null || target.InstanceId != pot.InstanceId)
                list.Add(new FireCrumb { Kind = "content", Label = target.Count > 1 ? $"{ItemName(target.ItemId)}×{target.Count}" : ItemName(target.ItemId), InstanceId = target.InstanceId });
            return list;
        }

        // ───────────── Q368 结果高亮的工位键 ─────────────

        /// <summary>Q368：火堆加工位的高亮键「slot:fire:{区}:{序号}」（序号 0 起）。</summary>
        public static string FireSlotHighlight(string place, int slotIndex) => $"slot:fire:{place}:{slotIndex}";

        /// <summary>Q368：晾制位的高亮键「slot:dry:{区}:{序号}」（序号 0 起）。</summary>
        public static string DryingSlotHighlight(string place, int slotIndex) => $"slot:dry:{place}:{slotIndex}";
    }
}
