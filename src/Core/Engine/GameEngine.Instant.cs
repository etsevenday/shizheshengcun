using System;
using System.Collections.Generic;
using System.Linq;
using Survival.Core.Config;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>即时操作（不耗时）：拾取、放下、投料、熄火，金属锅的装水、倒出，以及设施内容物的放入与取出（拖拽与容器案 4、6、8；设施与烹饪案第 3 节）。</summary>
    public sealed partial class GameEngine
    {
        /// <summary>执行即时操作。带 requestId 时幂等：重发同一请求直接成功、不再改状态。</summary>
        public ActionResult DoInstant(GameState s, InstantRequest r)
        {
            if (SeenRequest(s, r.RequestId)) return ActionResult.Repeated();
            var res = DoInstantOnce(s, r);
            if (res.Ok) RememberRequest(s, r.RequestId);
            return res;
        }

        public static bool SeenRequest(GameState s, string? id) => !string.IsNullOrEmpty(id) && s.RecentRequestIds.Contains(id!);

        public static void RememberRequest(GameState s, string? id)
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
                    fac.WentOut = true;
                    Emit(s, GameEventKind.Info, $"已熄灭火堆，剩余燃料{fac.FuelMinutes}分钟保留。");
                    return ActionResult.Success();
                }
                case InstantKind.FillPot:
                    return FillPot(s, r);
                case InstantKind.Decant:
                    return Decant(s, r);
                case InstantKind.PutInFire:
                    return PutInFire(s, r);
                case InstantKind.PutFoodInPot:
                    return PutFoodInPot(s, r);
                case InstantKind.TakeOut:
                    return TakeOut(s, r);
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

        /// <summary>锅能否被整理：未被行动锁定、角色空闲、锅在随身、当前地面或当前火堆里。</summary>
        private string? PotBlocked(GameState s, ItemInstance? pot)
        {
            if (pot == null || !IsPot(pot)) return "没有这口锅";
            if (IsLocked(s, pot) || PotContents(s, pot.InstanceId).Any(i => i.ReservedBy != null)) return "行动占用：这口锅正被当前行动使用";
            var busy = HolderBlocked(s);
            if (busy != null) return busy;
            var place = PlaceOf(s, pot);
            if (!(place == ItemInstance.Carried || place == s.CurrentLocation)) return "只能操作随身、当前地面或当前火堆里的锅";
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
            if (PlaceOf(s, pot) == ItemInstance.Carried && !water.IsCarried)
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
            var dirtyRestart = BatchProcess(water.ItemId) != null && ProcessOf(s, pot!) is FacilityProcess bp && bp.HeatDoneMinutes > 0;
            MoveItems(s, water, r.Count, ItemInstance.ContainerOwner(pot!.InstanceId));
            // 同种脏水追加：整批净化重新计时（设施与烹饪案 5.2），净水追加不影响已有食物进度
            if (dirtyRestart)
            {
                ProcessOf(s, pot)!.HeatDoneMinutes = 0;
                Emit(s, GameEventKind.Info, $"{ItemName(pot.ItemId)}#{pot.InstanceId}追加{ItemName(water.ItemId)}，整锅净化进度清零。");
            }
            SyncFacilities(s);
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
            var free = PotFreeWaterUnits(s, pot);
            if (free <= 0) { reason = ReservedWaterText(s, pot); return 0; }
            var max = Math.Min(child.Count, free);
            if (zone == ItemInstance.Carried && PlaceOf(s, pot) != ItemInstance.Carried)
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
            if (r.Count > max)
                return r.Count > PotFreeWaterUnits(s, child)
                    ? ActionResult.Fail($"只有{max}份未被预留；{ReservedWaterText(s, child)}", max)
                    : ActionResult.Fail($"携带后将超过{Config.Carry.HardGrams / 1000}公斤，最多可倒出{max}份", max);
            var pot = ContainerOf(s, child)!;
            var batch = ProcessOf(s, pot);
            var interrupted = batch != null && batch.HeatDoneMinutes > 0 && child.ItemId == Process(batch.RecipeId).InputId;
            MoveItems(s, child, r.Count, zone);
            // 倒出正在净化的水属于明确中断：该锅净化进度清零（设施与烹饪案第 6 节）
            if (interrupted && ProcessOf(s, pot) is FacilityProcess still) still.HeatDoneMinutes = 0;
            SyncFacilities(s);
            UpdateFlags(s);
            return ActionResult.Success();
        }
    }
}
