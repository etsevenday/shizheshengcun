using System;
using System.Collections.Generic;
using System.Linq;
using Survival.Core.Config;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>
    /// 设施内容物（卡片统一与设施烹饪 v0.4 · 02 案）：营地火堆有 2 个真实加工位，锅与直烤食材放入后归属火堆；
    /// 锅里的水与食材归属该锅。放入、取出不耗时；加工随世界 5 分钟段自动推进，不占角色主行动。
    /// </summary>
    public sealed partial class GameEngine
    {
        public const string FireSuffix = ":fire";

        /// <summary>某地点火堆的设施 ID（营地为 camp:fire）。</summary>
        public static string FireIdOf(string locationId) => locationId + FireSuffix;

        /// <summary>设施 ID 所在的地点；不是已知火堆时为 null。</summary>
        public string? FacilityLocation(string facilityId)
        {
            if (!facilityId.EndsWith(FireSuffix)) return null;
            var loc = facilityId.Substring(0, facilityId.Length - FireSuffix.Length);
            return _locs.TryGetValue(loc, out var l) && l.HasFire ? loc : null;
        }

        /// <summary>火堆内物品的归属字符串：facility:camp:fire。</summary>
        public static string FireOwner(string locationId) => ItemInstance.FacilityOwner(FireIdOf(locationId));

        public ProcessConfig Process(string id) => Config.Processes.First(p => p.Id == id);

        private ProcessConfig? BatchProcess(string liquidId) =>
            Config.Processes.FirstOrDefault(p => p.IsPotBatch && p.InputId == liquidId);

        private ProcessConfig? FireFoodProcess(string itemId) =>
            Config.Processes.FirstOrDefault(p => !p.IsPotBatch && !p.InPot && p.InputId == itemId);

        private ProcessConfig? PotFoodProcess(string itemId) =>
            Config.Processes.FirstOrDefault(p => !p.IsPotBatch && p.InPot && p.InputId == itemId);

        /// <summary>该物品放进火堆加工位能否直烤（白名单内且有对应进程）。</summary>
        public bool CanRoast(string itemId) => Config.Fire.DirectFoodInputIds.Contains(itemId) && FireFoodProcess(itemId) != null;

        /// <summary>该物品放进锅里能否水煮。</summary>
        public bool CanBoilInPot(ItemInstance pot, string itemId) => PotConfig(pot).StoredFoodIds.Contains(itemId) && PotFoodProcess(itemId) != null;

        // ───────────────────────── 查询 ─────────────────────────

        /// <summary>火堆加工位（按位次），空位为 null。</summary>
        public List<ItemInstance?> FireSlots(GameState s, string locationId)
        {
            var list = new List<ItemInstance?>();
            if (!_locs[locationId].HasFire) return list;
            var fac = s.Locations[locationId].Facility;
            var owner = FireOwner(locationId);
            for (var k = 0; k < Config.Fire.ProcessingSlots; k++)
            {
                var id = k < fac.ProcessingSlots.Count ? fac.ProcessingSlots[k] : null;
                var it = id.HasValue ? FindItem(s, id.Value) : null;
                list.Add(it != null && it.Owner == owner ? it : null);
            }
            return list;
        }

        /// <summary>火堆里的真实内容（锅、直烤食材及其成品），按加工位次序。</summary>
        public List<ItemInstance> FireContents(GameState s, string locationId) =>
            FireSlots(s, locationId).Where(x => x != null).Select(x => x!).ToList();

        public int FireFreeSlots(GameState s, string locationId) => FireSlots(s, locationId).Count(x => x == null);

        /// <summary>火堆里的锅（最多一口）。</summary>
        public ItemInstance? FirePot(GameState s, string locationId) =>
            _locs[locationId].HasFire ? FireContents(s, locationId).FirstOrDefault(IsPot) : null;

        /// <summary>锅所在火堆的地点；锅不在火堆里为 null。</summary>
        public string? PotFireLocation(ItemInstance pot) =>
            ItemInstance.TryFacilityId(pot.Owner, out var fid) ? FacilityLocation(fid) : null;

        /// <summary>某物品所属的锅（锅里的水与食材）；不在锅里为 null。</summary>
        public ItemInstance? ContainerOf(GameState s, ItemInstance i) =>
            ItemInstance.TryContainerId(i.Owner, out var potId) ? FindItem(s, potId) : null;

        /// <summary>火堆本段能否加热：燃烧且燃料至少够一段（设施与烹饪案第 7 节）。</summary>
        public bool FireHot(GameState s, string locationId)
        {
            var f = s.Locations[locationId].Facility;
            return _locs[locationId].HasFire && f.FireLit && f.FuelMinutes >= Tick;
        }

        public IEnumerable<FacilityProcess> AllProcesses(GameState s) => s.Locations.Values.SelectMany(l => l.Facility.Processes);

        /// <summary>以这件物品为目标的加工记录。</summary>
        public FacilityProcess? ProcessOf(GameState s, ItemInstance target) =>
            AllProcesses(s).FirstOrDefault(p => p.TargetInstanceId == target.InstanceId);

        /// <summary>为水煮预留的净水（毫升）。</summary>
        public int PotReservedMl(GameState s, int potId) =>
            AllProcesses(s).Where(p => p.PotInstanceId == potId && p.TargetInstanceId != potId).Sum(p => p.ReservedWaterMl);

        /// <summary>锅里没被水煮预留、可以饮用或倒出的水份数。参数可以是锅，也可以是锅里的水。</summary>
        public int PotFreeWaterUnits(GameState s, ItemInstance potOrWater)
        {
            var pot = IsPot(potOrWater) ? potOrWater : ContainerOf(s, potOrWater);
            if (pot == null) return potOrWater.Count;
            var unit = PotConfig(pot).UnitMl;
            return Math.Max(0, PotUnits(s, pot.InstanceId) - (PotReservedMl(s, pot.InstanceId) + unit - 1) / unit);
        }

        /// <summary>“500毫升供生块根#21加工，先取出该食材以停止加工”。</summary>
        public string ReservedWaterText(GameState s, ItemInstance potOrWater)
        {
            var pot = IsPot(potOrWater) ? potOrWater : ContainerOf(s, potOrWater);
            var rec = pot == null ? null : AllProcesses(s).Where(p => p.PotInstanceId == pot.InstanceId && p.ReservedWaterMl > 0)
                .OrderByDescending(p => FindItem(s, p.TargetInstanceId)?.PlacedOrder ?? 0).FirstOrDefault();
            if (rec == null) return "这份水已被加工预留";
            var t = FindItem(s, rec.TargetInstanceId);
            return $"{rec.ReservedWaterMl}毫升供{(t == null ? "食材" : ItemName(t.ItemId))}#{rec.TargetInstanceId}加工，先取出该食材以停止加工";
        }

        /// <summary>“金属锅#7（净水100%·1000/1000毫升，食材1/2）”。</summary>
        public string PotDesc(GameState s, ItemInstance pot) =>
            $"{ItemName(pot.ItemId)}#{pot.InstanceId}（{PotLabel(s, pot)}，食材{PotFood(s, pot.InstanceId).Count}/{PotConfig(pot).FoodSlots}）";

        /// <summary>“临时营地／火堆／金属锅#7”：设施与锅里的物品写清所在，不只写“当前地面”。</summary>
        public string WherePath(GameState s, ItemInstance i)
        {
            if (i.IsCarried) return "随身";
            if (ItemInstance.TryContainerId(i.Owner, out _))
            {
                var pot = ContainerOf(s, i);
                return pot == null ? "锅里" : $"{WherePath(s, pot)}／{ItemName(pot.ItemId)}#{pot.InstanceId}";
            }
            if (ItemInstance.TryFacilityId(i.Owner, out var fid))
            {
                var loc = FacilityLocation(fid);
                return loc == null ? "设施里" : $"{LocName(loc)}／火堆";
            }
            return _locs.ContainsKey(i.Owner) ? $"{LocName(i.Owner)}地面" : i.Owner;
        }

        /// <summary>
        /// 设施与锅里物品的加工状态文字：缺水，未加工 / 等待水净化 / 待净水500毫升 / 水煮中 受热10/30分钟 / 火堆未燃烧 · 暂停……
        /// </summary>
        public string ContentStatus(GameState s, ItemInstance inst)
        {
            var p = ProcessOf(s, inst);
            string Heat(string verb, string loc) => p == null ? "" : FireHot(s, loc)
                ? $"{verb}中 受热{p.HeatDoneMinutes}/{p.RequiredHeatMinutes}分钟"
                : $"火堆未燃烧 · {verb}暂停 {p.HeatDoneMinutes}/{p.RequiredHeatMinutes}分钟";

            if (IsPot(inst))
            {
                var liquid = PotLiquid(s, inst.InstanceId);
                var loc = PotFireLocation(inst);
                if (liquid == null) return loc == null ? "空锅" : "锅里没有水";
                var bp = BatchProcess(liquid);
                if (bp == null) return $"{ItemName(liquid)}{PotUnits(s, inst.InstanceId) * PotConfig(inst).UnitMl}毫升";
                if (loc == null) return $"{ItemName(liquid)}：放进燃烧的火堆后自动{bp.Name}";
                return Heat(bp.Name, loc);
            }
            if (inst.InFacility)
            {
                var loc = PlaceOf(s, inst);
                if (IsSpoiled(s, inst)) return "已腐败，不再加工";
                var rp = FireFoodProcess(inst.ItemId);
                if (rp == null) return "已完成，留在加工位";
                return Heat(rp.Name, loc);
            }
            if (inst.InContainer && !IsLiquid(inst))
            {
                var pot = ContainerOf(s, inst);
                if (pot == null) return "";
                if (IsSpoiled(s, inst)) return "已腐败，不再加工";
                var fp = PotFoodProcess(inst.ItemId);
                if (fp == null) return "已煮熟，留在锅里";
                var liquid = PotLiquid(s, pot.InstanceId);
                if (liquid == null) return "缺水，未加工";
                var loc = PotFireLocation(pot);
                if (loc == null) return "锅不在火堆上，未加工";
                if (fp.RequiredLiquid != null && liquid != fp.RequiredLiquid)
                    return BatchProcess(liquid) != null ? "等待水净化" : "缺水，未加工";
                if (p == null || p.ReservedWaterMl < fp.WaterCostMl) return $"待{ItemName(fp.RequiredLiquid ?? liquid)}{fp.WaterCostMl}毫升";
                return Heat(fp.Name, loc);
            }
            return "";
        }

        // ───────────────────────── 一致性 ─────────────────────────

        /// <summary>全部火堆：加工位对齐真实归属，进程记录按目标增删，按放入顺序为水煮分配净水。</summary>
        public void SyncFacilities(GameState s)
        {
            foreach (var l in Config.Locations.Where(l => l.HasFire))
                if (s.Locations.ContainsKey(l.Id)) SyncFacility(s, l.Id);
        }

        /// <summary>
        /// 加工位与真实归属对齐；进程记录按当前内容增删（删除即清零受热、释放水预留，设施与烹饪案第 6 节）；
        /// 水煮按放入顺序、平手按实例 ID 逐份预留净水，前一份分不到时后面的也等待（5.2）。
        /// </summary>
        private void SyncFacility(GameState s, string loc)
        {
            var fac = s.Locations[loc].Facility;
            if (string.IsNullOrEmpty(fac.FacilityId)) fac.FacilityId = FireIdOf(loc);
            var owner = FireOwner(loc);
            while (fac.ProcessingSlots.Count < Config.Fire.ProcessingSlots) fac.ProcessingSlots.Add(null);

            var contents = s.Items.Where(i => i.Owner == owner)
                .OrderBy(i => i.PlacedOrder ?? int.MaxValue).ThenBy(i => i.InstanceId).ToList();
            var ids = new HashSet<int>(contents.Select(i => i.InstanceId));
            for (var k = 0; k < fac.ProcessingSlots.Count; k++)
                if (fac.ProcessingSlots[k] is int id && (!ids.Contains(id) || fac.ProcessingSlots.IndexOf(id) != k))
                    fac.ProcessingSlots[k] = null;
            foreach (var c in contents)
            {
                if (fac.ProcessingSlots.Contains(c.InstanceId)) continue;
                var free = fac.ProcessingSlots.IndexOf(null);
                if (free < 0) fac.ProcessingSlots.Add(c.InstanceId);
                else fac.ProcessingSlots[free] = c.InstanceId;
            }

            var desired = new List<(ProcessConfig cfg, int target, int? pot)>();
            foreach (var c in contents)
            {
                if (IsPot(c))
                {
                    var liquid = PotLiquid(s, c.InstanceId);
                    var bp = liquid == null ? null : BatchProcess(liquid);
                    if (bp != null) desired.Add((bp, c.InstanceId, c.InstanceId));
                    foreach (var f in PotFood(s, c.InstanceId))
                    {
                        var fp = PotFoodProcess(f.ItemId);
                        if (fp != null) desired.Add((fp, f.InstanceId, c.InstanceId));
                    }
                }
                else
                {
                    var rp = FireFoodProcess(c.ItemId);
                    if (rp != null) desired.Add((rp, c.InstanceId, null));
                }
            }
            fac.Processes.RemoveAll(p => !desired.Any(d => d.cfg.Id == p.RecipeId && d.target == p.TargetInstanceId && d.pot == p.PotInstanceId));
            foreach (var d in desired)
            {
                if (fac.Processes.Any(p => p.RecipeId == d.cfg.Id && p.TargetInstanceId == d.target)) continue;
                fac.Processes.Add(new FacilityProcess
                {
                    ProcessId = $"{d.cfg.Id}#{s.NextProcessSeq++}",
                    RecipeId = d.cfg.Id,
                    TargetInstanceId = d.target,
                    PotInstanceId = d.pot,
                    RequiredHeatMinutes = d.cfg.HeatMinutes,
                    Status = ProcessStatus.Waiting,
                    LastWorldMinute = s.GameMinute,
                });
            }

            foreach (var pot in contents.Where(IsPot))
            {
                var liquid = PotLiquid(s, pot.InstanceId);
                var recs = fac.Processes.Where(p => p.PotInstanceId == pot.InstanceId && p.TargetInstanceId != pot.InstanceId
                                                    && Process(p.RecipeId).WaterCostMl > 0).ToList();
                foreach (var r in recs)
                {
                    var t = FindItem(s, r.TargetInstanceId);
                    if (t == null || IsSpoiled(s, t) || liquid != Process(r.RecipeId).RequiredLiquid) r.ReservedWaterMl = 0;
                }
                var free = PotUnits(s, pot.InstanceId) * PotConfig(pot).UnitMl - recs.Sum(r => r.ReservedWaterMl);
                var order = recs.Where(r => r.ReservedWaterMl == 0)
                    .Select(r => (r, t: FindItem(s, r.TargetInstanceId)))
                    .Where(x => x.t != null && !IsSpoiled(s, x.t))
                    .OrderBy(x => x.t!.PlacedOrder ?? int.MaxValue).ThenBy(x => x.t!.InstanceId);
                foreach (var (r, _) in order)
                {
                    var pc = Process(r.RecipeId);
                    if (liquid != pc.RequiredLiquid || free < pc.WaterCostMl) break;
                    r.ReservedWaterMl = pc.WaterCostMl;
                    free -= pc.WaterCostMl;
                }
            }
        }

        // ───────────────────────── 即时操作：放入与取出 ─────────────────────────

        /// <summary>设施与锅的放入、取出受角色忙碌约束：执行中只能浏览排序，暂停中只允许补给与火堆维护（02 案第 3 节）。</summary>
        private string? HolderBlocked(GameState s)
        {
            if (s.Sub != null || (s.Main != null && s.Main.Status == ActionStatus.Running)) return "行动进行中，不能整理物品";
            if (s.Main != null) return "行动暂停中，放入或取出设施里的物品要先结束或取消行动";
            return null;
        }

        private string FireFullText(GameState s, string loc)
        {
            var n = Config.Fire.ProcessingSlots;
            var names = string.Join("、", FireContents(s, loc).Select(i => IsPot(i) ? $"{ItemName(i.ItemId)}#{i.InstanceId}" : $"{ItemName(i.ItemId)}#{i.InstanceId}"));
            return $"火堆加工位已满（{n}/{n}：{names}），请先取出";
        }

        /// <summary>最多还能放进当前火堆几份（锅只能 1）；不能放时给出原因，不偷偷改投别处。</summary>
        public int MaxPutInFire(GameState s, ItemInstance inst, out string? reason)
        {
            var here = s.CurrentLocation;
            reason = HolderBlocked(s);
            if (reason != null) return 0;
            if (!_locs[here].HasFire) { reason = "这里没有火堆"; return 0; }
            var cfg = _items[inst.ItemId];
            if (inst.InFacility) { reason = PlaceOf(s, inst) == here ? $"{cfg.Name}已经在火堆里" : "异地不能操作火堆"; return 0; }
            if (!(inst.IsCarried || inst.Owner == here))
            {
                reason = inst.InContainer ? $"先把{cfg.Name}从锅里取出来" : "只能放入随身或当前地面的物品";
                return 0;
            }
            if (inst.ReservedBy != null) { reason = "该物品已被预留"; return 0; }
            if (IsLocked(s, inst)) { reason = "行动占用：该物品正被当前行动使用"; return 0; }
            var free = FireFreeSlots(s, here);
            if (IsPot(inst))
            {
                var cur = FirePot(s, here);
                if (cur != null) { reason = $"火堆已有{PotDesc(s, cur)}，最多放{Config.Fire.MaxPots}口锅；请先取下"; return 0; }
                if (free < 1) { reason = FireFullText(s, here); return 0; }
                return 1;
            }
            if (IsLiquid(inst)) { reason = $"{cfg.Name}不能直接倒进火堆（不会产生净水，也不会灭火），请拖进锅里"; return 0; }
            if (!CanRoast(inst.ItemId))
            {
                reason = cfg.Kind == "food" ? $"{cfg.Name}没有可用的处理配方，放进火堆也不会再加工" : $"{cfg.Name}不能放进火堆加工位";
                return 0;
            }
            if (IsSpoiled(s, inst)) { reason = $"{cfg.Name}已腐败，不能再加工"; return 0; }
            if (free < 1) { reason = FireFullText(s, here); return 0; }
            return Math.Min(free, inst.Count);
        }

        /// <summary>锅或直烤食材 → 当前火堆空加工位；一份食材占一位，不能用堆叠绕过（P402）。</summary>
        private ActionResult PutInFire(GameState s, InstantRequest r)
        {
            var inst = FindItem(s, r.InstanceId);
            if (inst == null) return ActionResult.Fail("物品已不在");
            var max = MaxPutInFire(s, inst, out var why);
            if (why != null) return ActionResult.Fail(why);
            var here = s.CurrentLocation;
            var n = IsPot(inst) ? 1 : r.Count;
            if (n < 1) return ActionResult.Fail("数量无效");
            if (n > inst.Count) return ActionResult.Fail("数量无效", max);
            if (n > max) return ActionResult.Fail($"火堆只剩{max}个空加工位", max);
            var owner = FireOwner(here);
            var name = ItemName(inst.ItemId);
            string text;
            if (IsPot(inst))
            {
                inst.Owner = owner;
                inst.PlacedOrder = s.NextProcessSeq++;
                text = $"把{PotDesc(s, inst)}放进{LocName(here)}火堆加工位。";
            }
            else
            {
                for (var k = 0; k < n; k++) MoveOne(s, inst, owner);
                text = $"把{n}份{name}放进{LocName(here)}火堆加工位直接烤制。";
            }
            SyncFacility(s, here);
            Emit(s, GameEventKind.Info, text);
            UpdateFlags(s);
            return ActionResult.Success();
        }

        /// <summary>最多还能放进这口锅几份食材（食材位、白名单与负重共同决定）。</summary>
        public int MaxPutFood(GameState s, ItemInstance food, ItemInstance pot, out string? reason)
        {
            reason = PotBlocked(s, pot);
            if (reason != null) return 0;
            var cfg = PotConfig(pot);
            var fc = _items[food.ItemId];
            if (IsLiquid(food)) { reason = $"{fc.Name}请拖到锅的水量条上装入"; return 0; }
            if (IsPot(food)) { reason = "不能锅套锅"; return 0; }
            if (!(food.IsCarried || food.Owner == s.CurrentLocation))
            {
                reason = food.InFacility || food.InContainer ? $"先把{fc.Name}取出来再放进锅" : "只能放入随身或当前地面的食材";
                return 0;
            }
            if (food.ReservedBy != null) { reason = "该物品已被预留"; return 0; }
            if (IsLocked(s, food)) { reason = "行动占用：该物品正被当前行动使用"; return 0; }
            if (!CanBoilInPot(pot, food.ItemId))
            {
                reason = fc.Kind == "food" ? $"{fc.Name}没有可用的处理配方，不能放进锅里加工" : $"{fc.Name}不能放进锅里";
                return 0;
            }
            if (IsSpoiled(s, food)) { reason = $"{fc.Name}已腐败，不能再加工"; return 0; }
            var foods = PotFood(s, pot.InstanceId);
            var free = cfg.FoodSlots - foods.Count;
            if (free < 1)
            {
                var liquid = PotLiquid(s, pot.InstanceId);
                var water = liquid == null ? "无水" : $"{ItemName(liquid)}{PotUnits(s, pot.InstanceId) * cfg.UnitMl}毫升";
                var list = string.Join("、", foods.GroupBy(f => f.ItemId).Select(g => $"{ItemName(g.Key)}×{g.Count()}"));
                reason = $"锅内{water}、食材{foods.Count}/{cfg.FoodSlots}（{list}），请先取出食物";
                return 0;
            }
            var max = Math.Min(free, food.Count);
            if (PlaceOf(s, pot) == ItemInstance.Carried && !food.IsCarried)
            {
                var room = Math.Max(0, Config.Carry.HardGrams - CarriedGrams(s)) / fc.WeightGrams;
                if (room <= 0) { reason = $"装入后携带将超过{Config.Carry.HardGrams / 1000}公斤"; return 0; }
                max = Math.Min(max, room);
            }
            return max;
        }

        /// <summary>生块根 → 指定锅的食材区（随身、地面或火堆里的锅都可以）；空锅只存放，显示“缺水，未加工”（P404）。</summary>
        private ActionResult PutFoodInPot(GameState s, InstantRequest r)
        {
            var food = FindItem(s, r.InstanceId);
            var pot = r.TargetInstanceId.HasValue ? FindItem(s, r.TargetInstanceId.Value) : null;
            if (food == null) return ActionResult.Fail("物品已不在");
            if (pot == null || !IsPot(pot)) return ActionResult.Fail("没有这口锅");
            var max = MaxPutFood(s, food, pot, out var why);
            if (why != null) return ActionResult.Fail(why);
            if (r.Count < 1 || r.Count > food.Count) return ActionResult.Fail("数量无效", max);
            if (r.Count > max)
            {
                var left = PotConfig(pot).FoodSlots - PotFood(s, pot.InstanceId).Count;
                return r.Count > left
                    ? ActionResult.Fail($"锅里只剩{left}个食材位", max)
                    : ActionResult.Fail($"装入后携带将超过{Config.Carry.HardGrams / 1000}公斤，最多可放{max}份", max);
            }
            var name = ItemName(food.ItemId);
            var owner = ItemInstance.ContainerOwner(pot.InstanceId);
            for (var k = 0; k < r.Count; k++) MoveOne(s, food, owner);
            SyncFacilities(s);
            Emit(s, GameEventKind.Info, $"把{r.Count}份{name}放进{ItemName(pot.ItemId)}#{pot.InstanceId}（{WherePath(s, pot)}）。");
            UpdateFlags(s);
            return ActionResult.Success();
        }

        /// <summary>从火堆或锅里取出到随身或当前地面能否成立；到随身时整锅连同内容一起计重，超 30 公斤整次拒绝（P403）。</summary>
        public string? TakeOutBlocked(GameState s, ItemInstance inst, string zone)
        {
            var busy = HolderBlocked(s);
            if (busy != null) return busy;
            var inPotFood = inst.InContainer && !IsLiquid(inst);
            if (!inst.InFacility && !inPotFood) return inst.InContainer ? "锅里的水请用倒出" : "只能取出火堆或锅里的物品";
            var place = PlaceOf(s, inst);
            if (!(place == ItemInstance.Carried || place == s.CurrentLocation)) return "异地不能取用设施里的物品";
            if (zone != ItemInstance.Carried && zone != s.CurrentLocation) return "只能取到随身或当前地面";
            if (inst.ReservedBy != null || IsLocked(s, inst)) return "行动占用：该物品正被当前行动使用";
            if (zone == ItemInstance.Carried && place != ItemInstance.Carried)
            {
                var grams = ItemGrams(s, inst);
                if (CarriedGrams(s) + grams > Config.Carry.HardGrams)
                {
                    var what = IsPot(inst) ? $"{ItemName(inst.ItemId)}连同内容共{grams / 1000.0:0.0}公斤" : $"{ItemName(inst.ItemId)}{grams / 1000.0:0.0}公斤";
                    return $"携带后将超过{Config.Carry.HardGrams / 1000}公斤（{what}），整次拒绝";
                }
            }
            return null;
        }

        /// <summary>
        /// 从火堆取下锅（内容整体跟随）或取出一份食材到随身/当前地面。取出即明确中断：该对象相关的受热清零、水预留释放，
        /// 其他目标不受影响（P414）。
        /// </summary>
        private ActionResult TakeOut(GameState s, InstantRequest r)
        {
            var inst = FindItem(s, r.InstanceId);
            if (inst == null) return ActionResult.Fail("物品已不在");
            var zone = string.IsNullOrEmpty(r.Zone) ? s.CurrentLocation : r.Zone;
            var why = TakeOutBlocked(s, inst, zone);
            if (why != null) return ActionResult.Fail(why);
            var from = WherePath(s, inst);
            var touched = IsPot(inst)
                ? AllProcesses(s).Where(p => p.PotInstanceId == inst.InstanceId).ToList()
                : AllProcesses(s).Where(p => p.TargetInstanceId == inst.InstanceId).ToList();
            var reset = touched.Any(p => p.HeatDoneMinutes > 0);
            var name = IsPot(inst) ? $"{ItemName(inst.ItemId)}#{inst.InstanceId}" : $"{ItemName(inst.ItemId)}#{inst.InstanceId}";
            inst.Owner = zone;
            inst.PlacedOrder = null;
            Normalize(s, zone);
            SyncFacilities(s);
            var to = zone == ItemInstance.Carried ? "随身" : $"{LocName(zone)}地面";
            Emit(s, GameEventKind.Info, $"把{name}从{from}取出到{to}{(reset ? "，相关加工进度清零" : "")}。");
            UpdateFlags(s);
            return ActionResult.Success();
        }

        // ───────────────────────── 世界时间：自动加工 ─────────────────────────

        /// <summary>段首快照：哪些加工目标本段可以受热（火堆燃烧、段首燃料≥5、不在点火、条件满足）。</summary>
        public sealed class FacilitySnapshot
        {
            public HashSet<string> Eligible { get; } = new HashSet<string>();
        }

        private FacilitySnapshot SnapshotFacilities(GameState s, ActionState running)
        {
            var snap = new FacilitySnapshot();
            foreach (var l in Config.Locations.Where(l => l.HasFire))
            {
                if (!s.Locations.TryGetValue(l.Id, out var ls)) continue;
                // 此前未燃烧的火堆在点火完成时才获得加热资格：点火这一段不算受热（02 案第 6 节）
                var igniting = running.Type == ActionType.Ignite && running.SourceLocation == l.Id;
                if (!FireHot(s, l.Id) || igniting) continue;
                foreach (var p in ls.Facility.Processes)
                    if (HeatEligible(s, l.Id, p)) snap.Eligible.Add(p.ProcessId);
            }
            return snap;
        }

        /// <summary>目标此刻是否满足受热条件（不含火堆燃烧条件）。</summary>
        private bool HeatEligible(GameState s, string loc, FacilityProcess p)
        {
            var cfg = Process(p.RecipeId);
            var t = FindItem(s, p.TargetInstanceId);
            if (t == null) return false;
            var owner = FireOwner(loc);
            if (cfg.IsPotBatch) return t.Owner == owner && PotLiquid(s, t.InstanceId) == cfg.InputId;
            if (t.ItemId != cfg.InputId || IsSpoiled(s, t)) return false;
            if (!cfg.InPot) return t.Owner == owner;
            var pot = p.PotInstanceId.HasValue ? FindItem(s, p.PotInstanceId.Value) : null;
            if (pot == null || pot.Owner != owner || t.Owner != ItemInstance.ContainerOwner(pot.InstanceId)) return false;
            return cfg.RequiredLiquid == null
                   || (PotLiquid(s, pot.InstanceId) == cfg.RequiredLiquid && p.ReservedWaterMl >= cfg.WaterCostMl);
        }

        /// <summary>
        /// 段末结算（02 案第 7 节）：按段首快照、且段末仍有效的目标各加 5 受热分钟；到期优先于完成；
        /// 达标的一次提交转化、耗水、新保质期与完成标记。随后重新分水：本段刚净化的水下一段才供食材受热。
        /// </summary>
        private void AdvanceFacilities(GameState s, FacilitySnapshot snap)
        {
            foreach (var l in Config.Locations.Where(l => l.HasFire))
            {
                if (!s.Locations.TryGetValue(l.Id, out var ls)) continue;
                var fac = ls.Facility;
                foreach (var p in fac.Processes.ToList())
                {
                    p.LastWorldMinute = s.GameMinute;
                    var heated = snap.Eligible.Contains(p.ProcessId) && HeatEligible(s, l.Id, p);
                    p.Status = heated ? ProcessStatus.Heating : ProcessStatus.Waiting;
                    if (!heated) continue;
                    p.HeatDoneMinutes += Tick;
                    if (p.HeatDoneMinutes >= p.RequiredHeatMinutes) CompleteProcess(s, l.Id, fac, p);
                }
            }
            SyncFacilities(s);
        }

        private void CompleteProcess(GameState s, string loc, FacilityState fac, FacilityProcess p)
        {
            var cfg = Process(p.RecipeId);
            var t = FindItem(s, p.TargetInstanceId)!;
            string text;
            if (cfg.IsPotBatch)
            {
                var ml = 0;
                var unit = PotConfig(t).UnitMl;
                foreach (var w in PotLiquids(s, t.InstanceId).Where(w => w.ItemId == cfg.InputId))
                {
                    w.ItemId = cfg.OutputId;
                    ml += w.Count * unit;
                }
                Normalize(s, ItemInstance.ContainerOwner(t.InstanceId));
                s.Progress.Flags.Add("boiled");
                text = $"{LocName(loc)}火堆：{ItemName(t.ItemId)}#{t.InstanceId}里的{ItemName(cfg.InputId)}{ml}毫升已烧成{ItemName(cfg.OutputId)}，留在原锅。";
            }
            else
            {
                var input = ItemName(t.ItemId);
                t.ItemId = cfg.OutputId;
                var outCfg = _items[cfg.OutputId];
                t.ExpiresAt = outCfg.ShelfLifeMinutes.HasValue ? s.GameMinute + outCfg.ShelfLifeMinutes.Value : (int?)null;
                var pot = p.PotInstanceId.HasValue ? FindItem(s, p.PotInstanceId.Value) : null;
                if (cfg.WaterCostMl > 0 && pot != null) ConsumeWater(s, pot, cfg.RequiredLiquid!, cfg.WaterCostMl);
                s.Progress.Flags.Add("cooked");
                text = pot != null
                    ? $"{LocName(loc)}火堆：{input}#{t.InstanceId}已煮成{outCfg.Name}，留在{ItemName(pot.ItemId)}#{pot.InstanceId}里（{ItemName(cfg.RequiredLiquid ?? "")}−{cfg.WaterCostMl}毫升）。"
                    : $"{LocName(loc)}火堆：{input}#{t.InstanceId}已烤成{outCfg.Name}，留在火堆加工位。";
            }
            p.Status = ProcessStatus.Done;
            p.CompletionSerial = s.SegmentCount;
            fac.Processes.Remove(p);
            fac.Completed.Add($"{p.ProcessId}@{p.CompletionSerial}");
            if (fac.Completed.Count > 20) fac.Completed.RemoveRange(0, fac.Completed.Count - 20);
            Emit(s, GameEventKind.FacilityDone, text, null, new[] { cfg.OutputId });
        }

        private void ConsumeWater(GameState s, ItemInstance pot, string liquidId, int ml)
        {
            var units = ml / PotConfig(pot).UnitMl;
            foreach (var w in PotLiquids(s, pot.InstanceId).Where(w => w.ItemId == liquidId).ToList())
            {
                if (units <= 0) break;
                var take = Math.Min(units, w.Count);
                w.Count -= take;
                units -= take;
                if (w.Count <= 0) s.Items.Remove(w);
            }
        }
    }
}
