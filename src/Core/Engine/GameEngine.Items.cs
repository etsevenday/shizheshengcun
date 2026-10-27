using System;
using System.Collections.Generic;
using System.Linq;
using Survival.Core.Config;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    public sealed partial class GameEngine
    {
        // ───────────────────────── 物品（搜集案 05） ─────────────────────────

        public bool IsSpoiled(GameState s, ItemInstance i) => i.ExpiresAt.HasValue && i.ExpiresAt.Value <= s.GameMinute;

        public ItemInstance? FindItem(GameState s, int instanceId) => s.Items.FirstOrDefault(i => i.InstanceId == instanceId);

        /// <summary>
        /// 物品实际所在的根位置：随身或某地点。锅内物品取锅所在，设施内物品取设施所在地点（火堆 → 锅 → 水/食材，最多两层）。
        /// </summary>
        public string PlaceOf(GameState s, ItemInstance i)
        {
            var owner = i.Owner;
            for (var depth = 0; depth < 4; depth++)
            {
                if (ItemInstance.TryContainerId(owner, out var potId))
                {
                    var pot = FindItem(s, potId);
                    if (pot == null) return "";
                    owner = pot.Owner;
                    continue;
                }
                if (ItemInstance.TryFacilityId(owner, out var fid)) return FacilityLocation(fid) ?? "";
                return owner;
            }
            return "";
        }

        /// <summary>一件物品连同全部后代（锅里的水与食材）的克重，每件只计一次。</summary>
        public int ItemGrams(GameState s, ItemInstance i)
        {
            var g = _items[i.ItemId].WeightGrams * i.Count;
            if (IsPot(i)) g += PotContents(s, i.InstanceId).Sum(c => ItemGrams(s, c));
            // 弹仓内弹药属于武器内容，计重一次（v0.5 §05）
            if (i.LoadedAmmo > 0 && i.LoadedAmmoId != null && _items.TryGetValue(i.LoadedAmmoId, out var ammo)) g += ammo.WeightGrams * i.LoadedAmmo;
            return g;
        }

        public int CarriedGrams(GameState s) =>
            s.Items.Where(i => i.IsCarried).Sum(i => ItemGrams(s, i));

        public bool IsOverweight(GameState s) => CarriedGrams(s) > Config.Carry.SoftGrams;

        /// <summary>
        /// 随身与（可选）当前地面的物品；includeContainers 时也包括这些位置（含当前火堆里）锅中的水（饮用、引导统计用）。
        /// 设施与锅里的食材是独立来源，不在这里自动取用（统一卡片案第 6 节）。
        /// </summary>
        public IEnumerable<ItemInstance> Accessible(GameState s, bool allowGround, bool includeContainers = false) =>
            s.Items.Where(i =>
            {
                if (i.InFacility) return false;
                if (i.InContainer)
                {
                    if (!includeContainers || !IsLiquid(i)) return false;
                    var place = PlaceOf(s, i);
                    return place == ItemInstance.Carried || (allowGround && place == s.CurrentLocation);
                }
                return i.IsCarried || (allowGround && i.Owner == s.CurrentLocation);
            });

        /// <summary>
        /// 可消耗批次：到期时间升序 → 同批次随身优先（锅内水按锅所在） → 实例 ID 升序。配方与投料不取锅内物品；
        /// 锅里的水已全部被水煮预留时不可取。
        /// </summary>
        public List<ItemInstance> UsableBatches(GameState s, string itemId, bool allowGround, bool includeContainers = false) =>
            Accessible(s, allowGround, includeContainers)
                .Where(i => i.ItemId == itemId && i.ReservedBy == null && !IsSpoiled(s, i))
                .Where(i => !i.InContainer || PotFreeWaterUnits(s, i) > 0)
                .OrderBy(i => i.ExpiresAt ?? int.MaxValue)
                .ThenBy(i => PlaceOf(s, i) == ItemInstance.Carried ? 0 : 1)
                .ThenBy(i => i.InstanceId)
                .ToList();

        public int AvailableCount(GameState s, string itemId, bool allowGround, bool includeContainers = false) =>
            UsableBatches(s, itemId, allowGround, includeContainers).Sum(i => i.Count);

        public bool IsLocked(GameState s, ItemInstance i) =>
            (s.Main != null && s.Main.LockedItemIds.Contains(i.InstanceId)) || (s.Sub != null && s.Sub.LockedItemIds.Contains(i.InstanceId));

        // ───────────────────────── 金属锅（拖拽与容器案 6） ─────────────────────────

        public bool IsPot(ItemInstance i) => _items[i.ItemId].ContainerId != null;

        public ContainerConfig PotConfig(ItemInstance pot) => Config.Containers[_items[pot.ItemId].ContainerId!];

        /// <summary>锅里的全部直接内容（水与食材）。</summary>
        public List<ItemInstance> PotContents(GameState s, int potId)
        {
            var owner = ItemInstance.ContainerOwner(potId);
            return s.Items.Where(i => i.Owner == owner).OrderBy(i => i.InstanceId).ToList();
        }

        public bool IsLiquidItem(string itemId) => _items.TryGetValue(itemId, out var c) && c.UnitVolumeMl.HasValue;
        public bool IsLiquid(ItemInstance i) => IsLiquidItem(i.ItemId);

        /// <summary>锅里的水（液体与食材是两个容量维度）。</summary>
        public List<ItemInstance> PotLiquids(GameState s, int potId) => PotContents(s, potId).Where(IsLiquid).ToList();

        /// <summary>锅里的食材：按放入顺序、再按实例 ID；每份单独一个实例，各占一个食材位。</summary>
        public List<ItemInstance> PotFood(GameState s, int potId) =>
            PotContents(s, potId).Where(i => !IsLiquid(i)).OrderBy(i => i.PlacedOrder ?? int.MaxValue).ThenBy(i => i.InstanceId).ToList();

        /// <summary>锅里水的份数（每份 500 毫升）。</summary>
        public int PotUnits(GameState s, int potId) => PotLiquids(s, potId).Sum(i => i.Count);

        /// <summary>锅内液体种类；无水为 null。同一锅只允许一种水。</summary>
        public string? PotLiquid(GameState s, int potId) => PotLiquids(s, potId).FirstOrDefault()?.ItemId;

        /// <summary>水量标签，如“脏水50%·500/1000毫升”；空锅为“空锅0%·0/1000毫升”。</summary>
        public string PotLabel(GameState s, ItemInstance pot)
        {
            var c = PotConfig(pot);
            var units = PotUnits(s, pot.InstanceId);
            var liquid = PotLiquid(s, pot.InstanceId);
            var ml = units * c.UnitMl;
            var pct = c.CapacityMl > 0 ? ml * 100 / c.CapacityMl : 0;
            return $"{(liquid == null ? "空锅" : ItemName(liquid))}{pct}%·{ml}/{c.CapacityMl}毫升";
        }

        /// <summary>
        /// 随身、成色（当前/最大）最高且耐久足够一次磨损的指定标签工具，平手取实例 ID 小者（v0.6 起各实例最大耐久不同，按比例比较）。
        /// </summary>
        public ItemInstance? BestTool(GameState s, string toolTag) =>
            s.Items.Where(i => i.IsCarried && i.ReservedBy == null && _items[i.ItemId].ToolTag == toolTag && HasWear(i, ToolWear(toolTag)))
                .OrderByDescending(Condition)
                .ThenBy(i => i.InstanceId)
                .FirstOrDefault();

        /// <summary>该标签工具一次成功使用的磨损：打火器按点火磨损，锅不按次磨损（按加工），其余按采集磨损。</summary>
        public double ToolWear(string toolTag) => toolTag == "lighter" ? LighterWear : toolTag == "pot" ? 0 : GatherWear;

        public ItemInstance? CarriedTool(GameState s, string toolTag) => BestTool(s, toolTag);

        /// <summary>同地全部可用的某标签工具（随身在前，再按实例 ID），供界面让玩家选定。</summary>
        public List<ItemInstance> AccessibleTools(GameState s, string toolTag) =>
            s.Items.Where(i => (i.IsCarried || i.Owner == s.CurrentLocation) && i.ReservedBy == null && !IsLocked(s, i)
                               && _items[i.ItemId].ToolTag == toolTag && HasWear(i, ToolWear(toolTag)))
                .OrderBy(i => i.IsCarried ? 0 : 1)
                .ThenBy(i => i.InstanceId)
                .ToList();

        /// <summary>同地可用的催化工具：随身优先，其次当前地面（锅位视为地面）；未预留、未被锁定、耐久足够。</summary>
        public ItemInstance? AccessibleTool(GameState s, string toolTag) =>
            s.Items.Where(i => (i.IsCarried || i.Owner == s.CurrentLocation) && i.ReservedBy == null && !IsLocked(s, i)
                               && _items[i.ItemId].ToolTag == toolTag && HasWear(i, ToolWear(toolTag)))
                .OrderBy(i => i.IsCarried ? 0 : 1)
                .ThenByDescending(Condition)
                .ThenBy(i => i.InstanceId)
                .FirstOrDefault();

        /// <summary>按消耗顺序预留指定数量，必要时拆堆，返回预留实例 ID。手工指定的批次排在最前（拖拽与容器案 5）。</summary>
        private List<int> Reserve(GameState s, string itemId, int count, int actionId, IList<int>? preferred = null)
        {
            var ids = new List<int>();
            var left = count;
            var batches = UsableBatches(s, itemId, true);
            if (preferred != null && preferred.Count > 0)
                batches = batches.OrderBy(b => preferred.Contains(b.InstanceId) ? preferred.IndexOf(b.InstanceId) : int.MaxValue).ToList();
            foreach (var b in batches)
            {
                if (left <= 0) break;
                var take = Math.Min(left, b.Count);
                if (take == b.Count)
                {
                    b.ReservedBy = actionId;
                    ids.Add(b.InstanceId);
                }
                else
                {
                    b.Count -= take;
                    var n = b.Clone();
                    n.InstanceId = s.NextInstanceId++;
                    n.Count = take;
                    n.ReservedBy = actionId;
                    s.Items.Add(n);
                    ids.Add(n.InstanceId);
                }
                left -= take;
            }
            if (left > 0) throw new InvalidOperationException($"预留 {itemId} 数量不足");
            return ids;
        }

        private void ConsumeOne(GameState s, ItemInstance inst)
        {
            inst.Count -= 1;
            if (inst.Count <= 0) s.Items.Remove(inst);
        }

        private void MoveItems(GameState s, ItemInstance inst, int count, string newOwner)
        {
            // 锅内物品的归属是锅本身，随锅一起走；离开设施与锅的物品清掉放入序号
            var intoHolder = newOwner.StartsWith(ItemInstance.ContainerPrefix) || newOwner.StartsWith(ItemInstance.FacilityPrefix);
            if (count >= inst.Count)
            {
                inst.Owner = newOwner;
                if (!intoHolder) inst.PlacedOrder = null;
            }
            else
            {
                inst.Count -= count;
                var n = inst.Clone();
                n.InstanceId = s.NextInstanceId++;
                n.Count = count;
                n.Owner = newOwner;
                if (!intoHolder) n.PlacedOrder = null;
                s.Items.Add(n);
            }
            Normalize(s, newOwner);
        }

        /// <summary>把一份（数量 1）移入设施或锅：整堆只剩 1 份时直接改归属，否则拆出新实例。返回移入的实例。</summary>
        private ItemInstance MoveOne(GameState s, ItemInstance inst, string newOwner)
        {
            ItemInstance moved;
            if (inst.Count <= 1)
            {
                inst.Owner = newOwner;
                moved = inst;
            }
            else
            {
                inst.Count -= 1;
                moved = inst.Clone();
                moved.InstanceId = s.NextInstanceId++;
                moved.Count = 1;
                moved.Owner = newOwner;
                s.Items.Add(moved);
            }
            moved.PlacedOrder = s.NextProcessSeq++;
            return moved;
        }

        /// <summary>
        /// 生成物品到指定位置。工具与武器逐件生成：最大耐久按区间以 0.1 精度抽一次（v0.6 §05），新制品当前=最大，
        /// scavenged（拾荒箱子产出）再乘 0.6—1.0 成色；易腐品按生成时刻记录 expiresAt。返回新生成的实例。
        /// </summary>
        public List<ItemInstance> CreateItems(GameState s, string itemId, int count, string owner, bool scavenged = false, ItemSource? source = null)
        {
            var made = new List<ItemInstance>();
            if (count <= 0) return made;
            var cfg = _items[itemId];
            // 设施与锅里的食材每份各占一位，逐份生成，不合堆
            var single = !cfg.IsStackable || ItemInstance.TryFacilityId(owner, out _) || (owner.StartsWith(ItemInstance.ContainerPrefix) && !cfg.UnitVolumeMl.HasValue);
            if (single)
            {
                for (var k = 0; k < count; k++)
                {
                    var inst = new ItemInstance
                    {
                        InstanceId = s.NextInstanceId++,
                        ItemId = itemId,
                        Count = 1,
                        Owner = owner,
                        ExpiresAt = cfg.ShelfLifeMinutes.HasValue ? s.GameMinute + cfg.ShelfLifeMinutes.Value : (int?)null,
                        PlacedOrder = cfg.IsStackable ? s.NextProcessSeq++ : (int?)null,
                        Source = source?.Clone(),
                    };
                    if (cfg.HasDurability)
                    {
                        inst.MaxDurability = RollMaxDurability(s, cfg);
                        inst.Durability = scavenged ? inst.MaxDurability * RollCondition(s) : inst.MaxDurability;
                    }
                    var w = Weapon(itemId);
                    if (w != null && w.UsesMagazine) inst.LoadedAmmoId = w.AmmoId;
                    s.Items.Add(inst);
                    made.Add(inst);
                }
                return made;
            }
            var stack = new ItemInstance
            {
                InstanceId = s.NextInstanceId++,
                ItemId = itemId,
                Count = count,
                Owner = owner,
                ExpiresAt = cfg.ShelfLifeMinutes.HasValue ? s.GameMinute + cfg.ShelfLifeMinutes.Value : (int?)null,
                Source = source?.Clone(),
            };
            s.Items.Add(stack);
            made.Add(stack);
            Normalize(s, owner);
            return made;
        }

        /// <summary>
        /// 同物品、同到期时间且同来源（R07 活性物质的发放事件）、未预留的实例合堆，超过堆叠上限分为新堆。
        /// 设施里的物品与锅里的食材每份占一个位，不合堆（否则两根会合成“一堆占一位”）；锅里的水照常合堆。
        /// </summary>
        private void Normalize(GameState s, string owner)
        {
            if (ItemInstance.TryFacilityId(owner, out _)) return;
            var inPot = owner.StartsWith(ItemInstance.ContainerPrefix);
            var groups = s.Items
                .Where(i => i.Owner == owner && i.ReservedBy == null && _items[i.ItemId].IsStackable && (!inPot || IsLiquid(i)))
                .GroupBy(i => (i.ItemId, i.ExpiresAt, i.Source?.GrantEventId))
                .ToList();
            foreach (var g in groups)
            {
                var list = g.OrderBy(i => i.InstanceId).ToList();
                var max = _items[g.Key.ItemId].StackMax;
                var total = list.Sum(i => i.Count);
                var needed = (total + max - 1) / max;
                if (list.Count == needed && list.Take(needed - 1).All(i => i.Count == max)) continue;
                foreach (var inst in list)
                {
                    var take = Math.Min(max, total);
                    inst.Count = take;
                    total -= take;
                }
                foreach (var inst in list.Where(i => i.Count <= 0)) s.Items.Remove(inst);
                while (total > 0)
                {
                    var take = Math.Min(max, total);
                    var tmpl = list[0];
                    s.Items.Add(new ItemInstance
                    {
                        InstanceId = s.NextInstanceId++,
                        ItemId = tmpl.ItemId,
                        Count = take,
                        Owner = owner,
                        ExpiresAt = tmpl.ExpiresAt,
                        Source = tmpl.Source?.Clone(),
                    });
                    total -= take;
                }
            }
        }

        // ───────────────────────── 世界（地图案 04–07） ─────────────────────────

        /// <summary>到达地点：标记已访问，揭示进入即显示的资源点。返回新发现的资源点 ID。</summary>
        private List<string> Visit(GameState s, string locationId)
        {
            var ls = s.Locations[locationId];
            ls.Visited = true;
            ls.Discovered = true;
            var revealed = new List<string>();
            foreach (var r in Config.Resources.Where(r => r.LocationId == locationId))
            {
                var rs = s.Resources[r.NodeId];
                if (rs.Discovered) continue;
                if (r.RevealOnVisit || (r.RevealAt.HasValue && r.RevealAt.Value <= ls.Progress))
                {
                    rs.Discovered = true;
                    revealed.Add(r.NodeId);
                }
            }
            return revealed;
        }

        /// <summary>探索完成：+25 探索度，按 25→50→75→100 顺序处理新跨过的里程碑，每个只触发一次。</summary>
        private (int old, int now, List<string> found, List<string> ids) ApplyExplore(GameState s, string locationId)
        {
            var ls = s.Locations[locationId];
            var old = ls.Progress;
            var now = Math.Min(100, old + Config.Actions.ExploreProgress);
            ls.Progress = now;

            var found = new List<string>();
            var ids = new List<string>();
            foreach (var t in new[] { 25, 50, 75, 100 })
            {
                if (t <= old || t > now) continue;
                if (!ls.TriggeredMilestones.Add(t)) continue;
                foreach (var r in Config.Resources.Where(r => r.LocationId == locationId && r.RevealAt.HasValue && r.RevealAt.Value <= t))
                {
                    var rs = s.Resources[r.NodeId];
                    if (rs.Discovered) continue;
                    rs.Discovered = true;
                    found.Add(r.Name);
                    ids.Add(r.NodeId);
                }
            }
            foreach (var e in UnlockEdges(s))
            {
                found.Add($"通往{LocName(e.Touches(locationId) ? e.Other(locationId) : e.B)}的路线");
                ids.Add(e.Id);
            }

            return (old, now, found, ids);
        }

        /// <summary>检查所有未解锁连接，任一条件满足即解锁并发现两端地点。</summary>
        private List<Config.EdgeConfig> UnlockEdges(GameState s)
        {
            var list = new List<Config.EdgeConfig>();
            foreach (var e in Config.Edges)
            {
                if (s.UnlockedEdges.Contains(e.Id)) continue;
                if (!e.UnlockAny.Any(u => s.Locations[u.Location].Progress >= u.Progress)) continue;
                s.UnlockedEdges.Add(e.Id);
                s.Locations[e.A].Discovered = true;
                s.Locations[e.B].Discovered = true;
                list.Add(e);
            }
            return list;
        }

        /// <summary>某地点下一次探索将跨过的里程碑是否有新内容（不泄露具体内容）。</summary>
        public bool NextMilestoneHasContent(GameState s, string locationId)
        {
            var ls = s.Locations[locationId];
            var next = Math.Min(100, ls.Progress + Config.Actions.ExploreProgress);
            var t = next;
            if (ls.TriggeredMilestones.Contains(t)) return false;
            if (Config.Resources.Any(r => r.LocationId == locationId && r.RevealAt == t && !s.Resources[r.NodeId].Discovered)) return true;
            return Config.Edges.Any(e => !s.UnlockedEdges.Contains(e.Id) && e.UnlockAny.Any(u => u.Location == locationId && u.Progress == t));
        }
    }
}
