using System;
using System.Collections.Generic;
using System.Linq;
using Survival.Core.Config;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>即时操作（不耗时）：拾取、放下、投料、熄火，以及金属锅的装水、倒出、架锅与取锅（拖拽与容器案 4、6、8）。</summary>
    public sealed partial class GameEngine
    {
        /// <summary>执行即时操作。带 requestId 时幂等：重发同一请求直接成功、不再改状态。</summary>
        public ActionResult DoInstant(GameState s, InstantRequest r)
        {
            if (SeenRequest(s, r.RequestId)) return ActionResult.Success();
            var res = DoInstantOnce(s, r);
            if (res.Ok) RememberRequest(s, r.RequestId);
            return res;
        }

        private static bool SeenRequest(GameState s, string? id) => !string.IsNullOrEmpty(id) && s.RecentRequestIds.Contains(id!);

        private static void RememberRequest(GameState s, string? id)
        {
            if (string.IsNullOrEmpty(id)) return;
            s.RecentRequestIds.Add(id!);
            if (s.RecentRequestIds.Count > 32) s.RecentRequestIds.RemoveRange(0, s.RecentRequestIds.Count - 32);
        }

        private ActionResult DoInstantOnce(GameState s, InstantRequest r)
        {
            if (s.Progress.Dead) return ActionResult.Fail("角色已死亡");
            var here = s.CurrentLocation;
            switch (r.Kind)
            {
                case InstantKind.Pickup:
                {
                    var inst = FindItem(s, r.InstanceId);
                    if (inst != null && IsLocked(s, inst)) return ActionResult.Fail("行动占用：该物品正被当前行动使用");
                    if (IsBusy(s)) return ActionResult.Fail("行动进行中，不能整理物品");
                    if (inst == null || inst.Owner != here) return ActionResult.Fail("只能拾取当前地点地面的物品");
                    if (inst.ReservedBy != null) return ActionResult.Fail("该物品已被预留");
                    if (r.Count < 1 || r.Count > inst.Count) return ActionResult.Fail("数量无效");
                    var max = MaxPickup(s, inst);
                    if (r.Count > max)
                        return ActionResult.Fail($"携带后将超过{Config.Carry.HardGrams / 1000}公斤，最多可取{max}份", max);
                    MoveItems(s, inst, r.Count, ItemInstance.Carried);
                    UpdateFlags(s);
                    return ActionResult.Success();
                }
                case InstantKind.Drop:
                {
                    var inst = FindItem(s, r.InstanceId);
                    if (inst != null && IsLocked(s, inst)) return ActionResult.Fail("行动占用：该物品正被当前行动使用");
                    if (IsBusy(s)) return ActionResult.Fail("行动进行中，不能整理物品");
                    if (inst == null || !inst.IsCarried) return ActionResult.Fail("只能放下随身物品");
                    if (inst.ReservedBy != null) return ActionResult.Fail("该物品已被预留");
                    if (r.Count < 1 || r.Count > inst.Count) return ActionResult.Fail("数量无效");
                    MoveItems(s, inst, r.Count, here);
                    UpdateFlags(s);
                    return ActionResult.Success();
                }
                case InstantKind.AddFuel:
                {
                    var err = FireMaintenanceBlocked(s);
                    if (err != null) return ActionResult.Fail(err);
                    if (!_locs[here].HasFire) return ActionResult.Fail("这里没有火堆");
                    var fac = s.Locations[here].Facility;
                    var room = MaxWoodForFire(fac);
                    if (room <= 0)
                        return ActionResult.Fail($"至少需要{Config.Fire.BurnMinutesPerWood}分钟空余容量（燃料上限{Config.Fire.MaxFuelMinutes}分钟）");
                    if (r.Count < 1) return ActionResult.Fail("数量无效");
                    if (r.Count > room) return ActionResult.Fail($"火堆最多还能放{room}份木材（上限{Config.Fire.MaxFuelMinutes}分钟）", room);
                    List<ItemInstance> source;
                    if (r.InstanceId != 0)
                    {
                        var w = FindItem(s, r.InstanceId);
                        if (w == null || w.ItemId != "wood" || !(w.IsCarried || w.Owner == here) || w.ReservedBy != null)
                            return ActionResult.Fail("只能投入随身或当前地面未预留的木材");
                        source = new List<ItemInstance> { w };
                    }
                    else source = UsableBatches(s, "wood", true);
                    if (source.Sum(i => i.Count) < r.Count)
                        return ActionResult.Fail(source.Count == 0 ? "没有可用的木材" : $"木材不足{r.Count}份");
                    // 每份木材完整提供燃料，先整体校验再一次扣除（拖拽与容器案 7）
                    var left = r.Count;
                    foreach (var w in source)
                    {
                        if (left <= 0) break;
                        var take = Math.Min(left, w.Count);
                        w.Count -= take;
                        if (w.Count <= 0) s.Items.Remove(w);
                        left -= take;
                    }
                    fac.FuelMinutes += r.Count * Config.Fire.BurnMinutesPerWood;
                    Emit(s, GameEventKind.Info, $"投入{r.Count}木材，火堆燃料{fac.FuelMinutes}分钟。");
                    UpdateFlags(s);
                    return ActionResult.Success();
                }
                case InstantKind.Extinguish:
                {
                    var err = FireMaintenanceBlocked(s);
                    if (err != null) return ActionResult.Fail(err);
                    var fac = s.Locations[here].Facility;
                    if (!_locs[here].HasFire || !fac.FireLit) return ActionResult.Fail("火堆没有在燃烧");
                    fac.FireLit = false;
                    Emit(s, GameEventKind.Info, $"已熄灭火堆，剩余燃料{fac.FuelMinutes}分钟保留。");
                    return ActionResult.Success();
                }
                case InstantKind.FillPot:
                    return FillPot(s, r);
                case InstantKind.Decant:
                    return Decant(s, r);
                case InstantKind.AttachPot:
                    return AttachPot(s, r);
                case InstantKind.DetachPot:
                    return DetachPot(s);
                case InstantKind.Reorder:
                    return Reorder(s, r);
            }
            return ActionResult.Fail("未知操作");
        }

        /// <summary>按燃料空位最多还能投入的木材份数：floor((上限 − 当前燃料) / 每份分钟)。</summary>
        public int MaxWoodForFire(FacilityState fac) =>
            Math.Max(0, (Config.Fire.MaxFuelMinutes - fac.FuelMinutes) / Config.Fire.BurnMinutesPerWood);

        private string? FireMaintenanceBlocked(GameState s)
        {
            if (s.Sub != null) return "请等待当前补给或维护完成";
            if (s.Main == null) return null;
            if (s.Main.Status != ActionStatus.Paused) return "行动进行中";
            if (s.Main.Type == ActionType.Travel) return "移动途中无法维护火堆";
            return null;
        }

        private string? PotBlocked(GameState s, ItemInstance? pot)
        {
            if (pot == null || !IsPot(pot)) return "没有这口锅";
            if (IsLocked(s, pot) || PotContents(s, pot.InstanceId).Any(i => i.ReservedBy != null)) return "行动占用：这口锅正被当前行动使用";
            if (IsBusy(s)) return "行动进行中，不能整理物品";
            if (!(pot.IsCarried || pot.Owner == s.CurrentLocation)) return "只能操作随身或当前地面的锅";
            return null;
        }

        /// <summary>向锅里最多还能装多少份某种水（容量、同种、负重共同决定）；不能装时给出原因。</summary>
        public int MaxFill(GameState s, ItemInstance water, ItemInstance pot, out string? reason)
        {
            reason = PotBlocked(s, pot);
            if (reason != null) return 0;
            var cfg = PotConfig(pot);
            if (!cfg.Accepts.Contains(water.ItemId)) { reason = $"{ItemName(water.ItemId)}不能装入锅中"; return 0; }
            if (!(water.IsCarried || water.Owner == s.CurrentLocation) || water.ReservedBy != null)
            {
                reason = "只能装入随身或当前地面未预留的水";
                return 0;
            }
            var liquid = PotLiquid(s, pot.InstanceId);
            if (liquid != null && liquid != water.ItemId)
            {
                reason = $"混装禁止：锅内已有{ItemName(liquid)}，请先倒出或饮用";
                return 0;
            }
            var space = cfg.CapacityUnits - PotUnits(s, pot.InstanceId);
            if (space <= 0) { reason = $"满锅：锅内已满{cfg.CapacityMl}毫升"; return 0; }
            var max = Math.Min(space, water.Count);
            if (pot.IsCarried && !water.IsCarried)
            {
                var w = _items[water.ItemId].WeightGrams;
                var room = Math.Max(0, Config.Carry.HardGrams - CarriedGrams(s)) / w;
                if (room <= 0) { reason = $"装入后携带将超过{Config.Carry.HardGrams / 1000}公斤"; return 0; }
                max = Math.Min(max, room);
            }
            return max;
        }

        /// <summary>水卡 → 锅装水区：转移整份到锅内，不耗时（拖拽与容器案 4、6.1）。</summary>
        private ActionResult FillPot(GameState s, InstantRequest r)
        {
            var water = FindItem(s, r.InstanceId);
            var pot = r.TargetInstanceId.HasValue ? FindItem(s, r.TargetInstanceId.Value) : null;
            if (water == null) return ActionResult.Fail("没有这份水");
            var max = MaxFill(s, water, pot!, out var why);
            if (why != null) return ActionResult.Fail(why);
            if (r.Count < 1) return ActionResult.Fail("数量无效");
            if (r.Count > max)
            {
                var capLeft = PotConfig(pot!).CapacityUnits - PotUnits(s, pot!.InstanceId);
                if (r.Count > water.Count) return ActionResult.Fail("数量无效", max);
                return r.Count > capLeft
                    ? ActionResult.Fail($"锅里只剩{capLeft}份空间", max)
                    : ActionResult.Fail($"装入后携带将超过{Config.Carry.HardGrams / 1000}公斤，最多可装{max}份", max);
            }
            MoveItems(s, water, r.Count, ItemInstance.ContainerOwner(pot!.InstanceId));
            UpdateFlags(s);
            return ActionResult.Success();
        }

        /// <summary>锅内水最多能倒出多少份到目标区域（到随身时受 30 公斤限制）。</summary>
        public int MaxDecant(GameState s, ItemInstance child, string zone, out string? reason)
        {
            reason = null;
            if (!ItemInstance.TryContainerId(child.Owner, out var potId)) { reason = "这份水不在锅里"; return 0; }
            reason = PotBlocked(s, FindItem(s, potId));
            if (reason != null) return 0;
            if (zone != ItemInstance.Carried && zone != s.CurrentLocation) { reason = "只能倒到随身或当前地面"; return 0; }
            if (child.ReservedBy != null) { reason = "行动占用：锅内水已被预留"; return 0; }
            var pot = FindItem(s, potId)!;
            var max = child.Count;
            if (zone == ItemInstance.Carried && !pot.IsCarried)
            {
                var room = Math.Max(0, Config.Carry.HardGrams - CarriedGrams(s)) / _items[child.ItemId].WeightGrams;
                if (room <= 0) { reason = $"携带后将超过{Config.Carry.HardGrams / 1000}公斤"; return 0; }
                max = Math.Min(max, room);
            }
            return max;
        }

        /// <summary>锅内水子卡 → 随身或当前地面：倒出整份成为对应水卡。</summary>
        private ActionResult Decant(GameState s, InstantRequest r)
        {
            var child = FindItem(s, r.InstanceId);
            if (child == null) return ActionResult.Fail("没有这份水");
            var zone = string.IsNullOrEmpty(r.Zone) ? s.CurrentLocation : r.Zone;
            var max = MaxDecant(s, child, zone, out var why);
            if (why != null) return ActionResult.Fail(why);
            if (r.Count < 1 || r.Count > child.Count) return ActionResult.Fail("数量无效", max);
            if (r.Count > max) return ActionResult.Fail($"携带后将超过{Config.Carry.HardGrams / 1000}公斤，最多可倒出{max}份", max);
            MoveItems(s, child, r.Count, zone);
            UpdateFlags(s);
            return ActionResult.Success();
        }

        /// <summary>金属锅 → 营地火堆锅位：随身锅先整件放到当前地面再关联；不耗时，不自动加工（拖拽与容器案 6.2）。</summary>
        private ActionResult AttachPot(GameState s, InstantRequest r)
        {
            var here = s.CurrentLocation;
            var pot = FindItem(s, r.InstanceId);
            var why = PotBlocked(s, pot);
            if (why != null) return ActionResult.Fail(why);
            if (!_locs[here].HasFire) return ActionResult.Fail("这里没有火堆");
            var fac = s.Locations[here].Facility;
            var current = AttachedPot(s, here);
            if (current != null && current.InstanceId == pot!.InstanceId) return ActionResult.Fail("这口锅已架在火堆上");
            if (current != null) return ActionResult.Fail($"锅位已有{ItemName(current.ItemId)}，请先取下");
            if (pot!.IsCarried) MoveItems(s, pot, 1, here);
            fac.AttachedPotId = pot.InstanceId;
            UpdateFlags(s);
            return ActionResult.Success();
        }

        /// <summary>从锅位取下：只解除关联，锅仍在营地地面。</summary>
        private ActionResult DetachPot(GameState s)
        {
            var here = s.CurrentLocation;
            var pot = _locs[here].HasFire ? AttachedPot(s, here) : null;
            if (pot == null) return ActionResult.Fail("锅位上没有锅");
            var why = PotBlocked(s, pot);
            if (why != null) return ActionResult.Fail(why);
            s.Locations[here].Facility.AttachedPotId = null;
            return ActionResult.Success();
        }

        /// <summary>烧水与烹饪对所选锅的校验，并为烧水计算需从散水补入的批次（拖拽与容器案 5、6.2、8）。</summary>
        private string? ValidatePot(GameState s, RecipeConfig rc, Plan plan)
        {
            var pot = plan.Pot!;
            var cfg = PotConfig(pot);
            var potName = ItemName(pot.ItemId);
            var attached = AttachedPot(s, s.CurrentLocation);
            var where = pot.IsCarried ? "随身" : attached != null && attached.InstanceId == pot.InstanceId ? "火堆锅位上" : "地面";
            var units = PotUnits(s, pot.InstanceId);
            var liquid = PotLiquid(s, pot.InstanceId);
            if (PotContents(s, pot.InstanceId).Any(i => i.ReservedBy != null)) return "行动占用：这口锅正被当前行动使用";

            if (rc.PotMustBeEmpty)
            {
                if (units > 0) return "锅内有水，先倒出或饮用";
                plan.Notes.Add($"使用{where}的{potName}（空锅）；原料只被预留，不放进锅里存放");
                return null;
            }
            if (rc.Pot == null) return null;

            var pp = rc.Pot;
            if (liquid != null && liquid != pp.InputItem) return $"锅内有{ItemName(liquid)}，请先倒出或饮用";
            var need = Math.Max(0, pp.Units - units);
            if (need > 0)
            {
                var loose = UsableBatches(s, pp.InputItem, true);
                var have = loose.Sum(i => i.Count);
                if (have < need)
                    return $"再加入{need - have}份{ItemName(pp.InputItem)}，凑满{(pp.Units * cfg.UnitMl / 1000.0):0.#}升";
                var delta = 0;
                var left = need;
                var from = new HashSet<string>();
                foreach (var b in loose)
                {
                    if (left <= 0) break;
                    var take = Math.Min(left, b.Count);
                    plan.PotFill.Add((b, take));
                    left -= take;
                    from.Add(b.IsCarried ? "随身" : "当前地面");
                    var g = _items[b.ItemId].WeightGrams * take;
                    if (pot.IsCarried && !b.IsCarried) delta += g;
                    else if (!pot.IsCarried && b.IsCarried) delta -= g;
                }
                if (delta > 0 && CarriedGrams(s) + delta > Config.Carry.HardGrams)
                    return $"补水后携带将超过{Config.Carry.HardGrams / 1000}公斤，可先把锅放到地面";
                var weight = delta == 0 ? "" : $"，随身重量{(delta > 0 ? "+" : "−")}{Math.Abs(delta) / 1000.0:0.#}公斤";
                plan.Notes.Add($"开始时从{string.Join("与", from)}补入{need}份{ItemName(pp.InputItem)}到锅内{weight}");
            }
            plan.Notes.Add($"使用{where}的{potName}（{PotLabel(s, pot)}）；完成后{ItemName(pp.OutputItem)}{pp.Units}份留在原锅，不在地面另生成");
            return null;
        }
    }
}
