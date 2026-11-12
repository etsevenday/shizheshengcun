using System;
using System.Collections.Generic;
using System.Linq;
using Survival.Core.Config;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>
    /// 分量水壶（开发计划 v0.9 B16，R49；S10「X4 分量水壶、水处理与当前zone容器」、A07 X1.5）：容量 2000 毫升、空重 400 克、一根物资卡、无嵌套子物；
    /// 只装同一种脏/净水，不装食材、不受热、不放火堆。从 500 毫升水卡装入须整份；倒出成水卡也按 500 毫升整份；壶内每次可直接喝 250 毫升
    /// （5 分钟、水分+15；脏水同样必定腹泻 12 小时；战斗中不能喝）。水量按真实毫升等量转移，重量按 1 克/毫升计入，不凭空增减。
    /// 水量存在实例 LiquidId/LiquidMl 上（存档 28）。
    /// </summary>
    public sealed partial class GameEngine
    {
        public CanteenConfig? CanteenOf(ItemInstance i) => _items.TryGetValue(i.ItemId, out var c) ? c.Canteen : null;

        public bool IsCanteen(ItemInstance i) => CanteenOf(i) != null;

        /// <summary>“分量水壶#12（净水 750/2000毫升）”。</summary>
        public string CanteenLabel(ItemInstance c)
        {
            var cfg = CanteenOf(c)!;
            return c.LiquidId == null || c.LiquidMl <= 0
                ? $"空 0/{cfg.CapacityMl}毫升"
                : $"{ItemName(c.LiquidId)} {c.LiquidMl}/{cfg.CapacityMl}毫升";
        }

        /// <summary>水壶只能在随身或当前地面操作（不在火堆、锅里，未被预留或占用）。</summary>
        private string? CanteenHereBlocked(GameState s, ItemInstance c)
        {
            if (s.Battle != null) return "战斗中不能整理物品或饮水";
            if (DialogueLocked(s)) return DialogueLockedReason;
            if (IsBusy(s)) return "行动进行中，不能整理物品";
            if (!(c.IsCarried || c.Owner == s.Ground)) return "只能操作随身或当前地面的水壶";
            if (c.ReservedBy != null || IsLocked(s, c)) return "行动占用：这个水壶正被使用";
            return null;
        }

        /// <summary>这份水卡最多能装几份（500 毫升）进这个水壶；不能装时给原因。</summary>
        public int MaxFillCanteen(GameState s, ItemInstance water, ItemInstance canteen, out string? reason)
        {
            var cfg = CanteenOf(canteen);
            reason = cfg == null ? "这不是水壶" : CanteenHereBlocked(s, canteen);
            if (reason != null) return 0;
            if (!IsLiquid(water) || !cfg!.Accepts.Contains(water.ItemId)) { reason = $"{ItemName(water.ItemId)}不能装进水壶（只装脏水或净水）"; return 0; }
            if (water.InContainer || water.InFacility) { reason = "锅里的水请先倒出成水卡再装进水壶"; return 0; }
            if (!(water.IsCarried || water.Owner == s.Ground)) { reason = "只能用随身或当前地面的水卡"; return 0; }
            if (water.ReservedBy != null || IsLocked(s, water)) { reason = "该物品已被预留"; return 0; }
            if (canteen.LiquidId != null && canteen.LiquidMl > 0 && canteen.LiquidId != water.ItemId)
            { reason = $"水壶里是{ItemName(canteen.LiquidId)}，不能再装{ItemName(water.ItemId)}（不混装）"; return 0; }
            var unit = _items[water.ItemId].UnitVolumeMl ?? cfg.TransferMl;
            var room = (cfg.CapacityMl - canteen.LiquidMl) / unit;
            if (room < 1) { reason = $"水壶已满（{CanteenLabel(canteen)}），装不下{unit}毫升"; return 0; }
            var max = Math.Min(room, water.Count);
            // 地面的水装进随身的水壶：重量增加（水卡离开地面、进入随身的壶），按 30 公斤硬上限截断
            if (canteen.IsCarried && !water.IsCarried)
            {
                var g = _items[water.ItemId].WeightGrams;
                var byWeight = g <= 0 ? max : Math.Max(0, Config.Carry.HardGrams - CarriedGrams(s)) / g;
                if (byWeight < 1) { reason = $"装入后携带将超过{Config.Carry.HardGrams / 1000}公斤"; return 0; }
                max = Math.Min(max, byWeight);
            }
            return max;
        }

        /// <summary>装水：count 份 500 毫升水卡 → 水壶（0 分钟）。整份等量转移，不改水种、不改重量总和。</summary>
        private ActionResult FillCanteen(GameState s, InstantRequest r)
        {
            var water = FindItem(s, r.InstanceId);
            var canteen = r.TargetInstanceId.HasValue ? FindItem(s, r.TargetInstanceId.Value) : null;
            if (water == null) return ActionResult.Fail("水卡已不在");
            if (canteen == null || !IsCanteen(canteen)) return ActionResult.Fail("没有这个水壶");
            var max = MaxFillCanteen(s, water, canteen, out var why);
            if (why != null) return ActionResult.Fail(why);
            if (r.Count < 1 || r.Count > water.Count) return ActionResult.Fail("数量无效", max);
            if (r.Count > max) return ActionResult.Fail($"最多可装{max}份", max);
            var unit = _items[water.ItemId].UnitVolumeMl ?? CanteenOf(canteen)!.TransferMl;
            var id = water.ItemId;
            var from = water.IsCarried ? "随身" : "地面";
            water.Count -= r.Count;
            if (water.Count <= 0) s.Items.Remove(water);
            canteen.LiquidId = id;
            canteen.LiquidMl += r.Count * unit;
            Emit(s, GameEventKind.Info, $"把{from}{ItemName(id)}{r.Count * unit}毫升装进{ItemName(canteen.ItemId)}（{CanteenLabel(canteen)}）。");
            UpdateFlags(s);
            return ActionResult.Success();
        }

        /// <summary>最多能倒出几份 500 毫升水卡到 zone（随身/当前地面）；不能倒时给原因。不足 500 毫升的剩余只能直接喝。</summary>
        public int MaxPourCanteen(GameState s, ItemInstance canteen, string zone, out string? reason)
        {
            var cfg = CanteenOf(canteen);
            reason = cfg == null ? "这不是水壶" : CanteenHereBlocked(s, canteen);
            if (reason != null) return 0;
            if (zone != ItemInstance.Carried && zone != s.Ground) { reason = "只能倒到随身或当前地面"; return 0; }
            if (canteen.LiquidId == null || canteen.LiquidMl <= 0) { reason = "水壶是空的"; return 0; }
            var unit = _items[canteen.LiquidId].UnitVolumeMl ?? cfg!.TransferMl;
            var n = canteen.LiquidMl / unit;
            if (n < 1) { reason = $"壶里只剩{canteen.LiquidMl}毫升，不够倒成一份{unit}毫升水卡（可以直接喝）"; return 0; }
            if (zone == ItemInstance.Carried)
            {
                // 进随身成为水卡：重量不变时（壶在随身）只看卡位；壶在地面时还要看重量
                if (!canteen.IsCarried)
                {
                    var g = _items[canteen.LiquidId].WeightGrams;
                    n = Math.Min(n, g <= 0 ? n : Math.Max(0, Config.Carry.HardGrams - CarriedGrams(s)) / g);
                    if (n < 1) { reason = $"携带将超过{Config.Carry.HardGrams / 1000}公斤"; return 0; }
                }
                var probe = new ItemInstance { ItemId = canteen.LiquidId, Count = 1, Owner = "", ExpiresAt = null };
                var k = n;
                while (k > 0 && !RootCardsAllow(s, RootCardsAfter(s, probe, k, null, 0))) k--;
                if (k < 1) { reason = $"{RootCardsFullText(s)}，整次拒绝"; return 0; }
                n = k;
            }
            return n;
        }

        /// <summary>倒出：水壶 → count 份 500 毫升水卡到随身/当前地面（0 分钟）。</summary>
        private ActionResult PourCanteen(GameState s, InstantRequest r)
        {
            var canteen = FindItem(s, r.InstanceId);
            if (canteen == null || !IsCanteen(canteen)) return ActionResult.Fail("没有这个水壶");
            var zone = string.IsNullOrEmpty(r.Zone) ? s.Ground : r.Zone;
            var max = MaxPourCanteen(s, canteen, zone, out var why);
            if (why != null) return ActionResult.Fail(why);
            if (r.Count < 1) return ActionResult.Fail("数量无效", max);
            if (r.Count > max) return ActionResult.Fail($"最多可倒出{max}份", max);
            var id = canteen.LiquidId!;
            var unit = _items[id].UnitVolumeMl ?? CanteenOf(canteen)!.TransferMl;
            if (zone == ItemInstance.Carried)
            {
                var probe = new ItemInstance { ItemId = id, Count = 1, Owner = "", ExpiresAt = null };
                if (!RootCardsAllow(s, RootCardsAfter(s, probe, r.Count, null, 0))) return ActionResult.Fail($"{RootCardsFullText(s)}，整次拒绝");
            }
            canteen.LiquidMl -= r.Count * unit;
            if (canteen.LiquidMl <= 0) { canteen.LiquidMl = 0; canteen.LiquidId = null; }
            CreateItems(s, id, r.Count, zone);
            var to = zone == ItemInstance.Carried ? "随身" : $"{LocName(zone)}地面";
            Emit(s, GameEventKind.Info, $"从{ItemName(canteen.ItemId)}倒出{ItemName(id)}{r.Count * unit}毫升到{to}（剩{CanteenLabel(canteen)}）。");
            UpdateFlags(s);
            return ActionResult.Success();
        }

        /// <summary>直接从水壶喝一口能否开始；能为 null（Use 校验里调用）。</summary>
        private string? CanteenSipBlocked(ItemInstance c)
        {
            var cfg = CanteenOf(c)!;
            if (c.LiquidId == null || c.LiquidMl <= 0) return "水壶是空的";
            if (c.LiquidMl < cfg.SipMl) return $"壶里不足{cfg.SipMl}毫升";
            return null;
        }

        /// <summary>水壶一口的效果（Use 开始时一次结清）：扣 250 毫升，水分+15；脏水必定腹泻 12 小时（不按半份免风险）。</summary>
        private void ApplyCanteenSip(GameState s, ItemInstance c, List<string> parts)
        {
            ApplyCanteenEffect(s, c, s.Player, parts, false);
            parts.Add($"壶里剩{CanteenLabel(c)}");
        }

        /// <summary>水壶一口落到指定角色（B28 R12）：扣 250 毫升，水分与腹泻写到 actor。actualDelta 见 ApplyUseEffect。</summary>
        private void ApplyCanteenEffect(GameState s, ItemInstance c, ActorState actor, List<string> parts, bool actualDelta)
        {
            var cfg = CanteenOf(c)!;
            var liquid = c.LiquidId!;
            c.LiquidMl -= cfg.SipMl;
            if (c.LiquidMl <= 0) { c.LiquidMl = 0; c.LiquidId = null; }
            AddUseStat(actor, StatId.Hydration, "水分", cfg.SipHydration, parts, actualDelta);
            if (_items[liquid].Use?.CausesDiarrhea == true)
            {
                actor.DiarrheaUntil = s.GameMinute + Config.ItemActions.DiarrheaMinutes;
                parts.Add($"出现腹泻（{Clock.Duration(Config.ItemActions.DiarrheaMinutes)}）");
            }
        }

        /// <summary>界面用：从水壶喝一口的按钮文字“喝一口（净水250毫升）”。</summary>
        public string CanteenSipText(ItemInstance c) =>
            c.LiquidId == null ? "喝一口（壶空）" : $"喝一口（{ItemName(c.LiquidId)}{CanteenOf(c)!.SipMl}毫升）";
    }
}
