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

        /// <summary>物品实际所在：锅内物品取锅所在（随身或某地面），其余就是 Owner。</summary>
        public string PlaceOf(GameState s, ItemInstance i)
        {
            if (!ItemInstance.TryContainerId(i.Owner, out var potId)) return i.Owner;
            return FindItem(s, potId)?.Owner ?? "";
        }

        /// <summary>一件物品（锅连同内容）的克重；锅内物品只随锅计一次。</summary>
        public int ItemGrams(GameState s, ItemInstance i)
        {
            var g = _items[i.ItemId].WeightGrams * i.Count;
            if (IsPot(i)) g += PotContents(s, i.InstanceId).Sum(c => _items[c.ItemId].WeightGrams * c.Count);
            return g;
        }

        public int CarriedGrams(GameState s) =>
            s.Items.Where(i => i.IsCarried).Sum(i => ItemGrams(s, i));

        public bool IsOverweight(GameState s) => CarriedGrams(s) > Config.Carry.SoftGrams;

        /// <summary>随身与（可选）当前地面的物品；includeContainers 时也包括这些位置上锅里的水（饮用、引导统计用）。</summary>
        public IEnumerable<ItemInstance> Accessible(GameState s, bool allowGround, bool includeContainers = false) =>
            s.Items.Where(i =>
            {
                var place = includeContainers ? PlaceOf(s, i) : i.Owner;
                return place == ItemInstance.Carried || (allowGround && place == s.CurrentLocation);
            });

        /// <summary>可消耗批次：到期时间升序 → 同批次随身优先（锅内水按锅所在） → 实例 ID 升序。配方与投料不取锅内物品。</summary>
        public List<ItemInstance> UsableBatches(GameState s, string itemId, bool allowGround, bool includeContainers = false) =>
            Accessible(s, allowGround, includeContainers)
                .Where(i => i.ItemId == itemId && i.ReservedBy == null && !IsSpoiled(s, i))
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

        public List<ItemInstance> PotContents(GameState s, int potId)
        {
            var owner = ItemInstance.ContainerOwner(potId);
            return s.Items.Where(i => i.Owner == owner).OrderBy(i => i.InstanceId).ToList();
        }

        public int PotUnits(GameState s, int potId) => PotContents(s, potId).Sum(i => i.Count);

        /// <summary>锅内液体种类；空锅为 null。同一锅只允许一种水。</summary>
        public string? PotLiquid(GameState s, int potId) => PotContents(s, potId).FirstOrDefault()?.ItemId;

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

        /// <summary>架在某地点火堆上的锅（锅仍须在该地点地面）。</summary>
        public ItemInstance? AttachedPot(GameState s, string locationId)
        {
            var id = s.Locations[locationId].Facility.AttachedPotId;
            if (id == null) return null;
            var pot = FindItem(s, id.Value);
            return pot != null && pot.Owner == locationId ? pot : null;
        }

        private static void DetachEverywhere(GameState s, int potId)
        {
            foreach (var l in s.Locations.Values)
                if (l.Facility.AttachedPotId == potId) l.Facility.AttachedPotId = null;
        }

        /// <summary>
        /// 选锅（开发计划 v0.2 B2）：指定的锅优先；否则锅位锅 → 随身锅 → 当前地面锅，同类按实例 ID；被行动锁定的跳过。
        /// </summary>
        public ItemInstance? SelectPot(GameState s, int? requested, out string? reason)
        {
            reason = null;
            var here = s.CurrentLocation;
            bool Reachable(ItemInstance i) => IsPot(i) && i.ReservedBy == null && (i.IsCarried || i.Owner == here);
            if (requested.HasValue)
            {
                var p = FindItem(s, requested.Value);
                if (p == null || !Reachable(p)) { reason = "只能使用随身或当前地面的锅"; return null; }
                if (IsLocked(s, p)) { reason = "行动占用：这口锅正被其他行动使用"; return null; }
                return p;
            }
            var attached = AttachedPot(s, here);
            var pick = s.Items.Where(i => Reachable(i) && !IsLocked(s, i))
                .OrderBy(i => attached != null && i.InstanceId == attached.InstanceId ? 0 : i.IsCarried ? 1 : 2)
                .ThenBy(i => i.InstanceId)
                .FirstOrDefault();
            if (pick == null) reason = "缺少金属锅（需随身或在当前地面）";
            return pick;
        }

        /// <summary>随身、耐久最高（大于0或无耐久）的指定标签工具，平手取实例 ID 小者。</summary>
        public ItemInstance? BestTool(GameState s, string toolTag) =>
            s.Items.Where(i => i.IsCarried && i.ReservedBy == null && _items[i.ItemId].ToolTag == toolTag
                               && (i.Durability == null || i.Durability > 0))
                .OrderByDescending(i => i.Durability ?? int.MaxValue)
                .ThenBy(i => i.InstanceId)
                .FirstOrDefault();

        public ItemInstance? CarriedTool(GameState s, string toolTag) => BestTool(s, toolTag);

        /// <summary>同地全部可用的某标签工具（随身在前，再按实例 ID），供界面让玩家选定。</summary>
        public List<ItemInstance> AccessibleTools(GameState s, string toolTag) =>
            s.Items.Where(i => (i.IsCarried || i.Owner == s.CurrentLocation) && i.ReservedBy == null && !IsLocked(s, i)
                               && _items[i.ItemId].ToolTag == toolTag && (i.Durability == null || i.Durability > 0))
                .OrderBy(i => i.IsCarried ? 0 : 1)
                .ThenBy(i => i.InstanceId)
                .ToList();

        /// <summary>同地可用的催化工具：随身优先，其次当前地面（锅位视为地面）；未预留、未被锁定、有耐久。</summary>
        public ItemInstance? AccessibleTool(GameState s, string toolTag) =>
            s.Items.Where(i => (i.IsCarried || i.Owner == s.CurrentLocation) && i.ReservedBy == null && !IsLocked(s, i)
                               && _items[i.ItemId].ToolTag == toolTag && (i.Durability == null || i.Durability > 0))
                .OrderBy(i => i.IsCarried ? 0 : 1)
                .ThenByDescending(i => i.Durability ?? int.MaxValue)
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
            // 移动锅即解除锅位关联；锅内物品的归属是锅本身，随锅一起走
            if (IsPot(inst)) DetachEverywhere(s, inst.InstanceId);
            if (count >= inst.Count)
            {
                inst.Owner = newOwner;
            }
            else
            {
                inst.Count -= count;
                var n = inst.Clone();
                n.InstanceId = s.NextInstanceId++;
                n.Count = count;
                n.Owner = newOwner;
                s.Items.Add(n);
            }
            Normalize(s, newOwner);
        }

        /// <summary>生成物品到指定位置。工具逐件生成并带满耐久；易腐品按生成时刻记录 expiresAt。</summary>
        public void CreateItems(GameState s, string itemId, int count, string owner)
        {
            if (count <= 0) return;
            var cfg = _items[itemId];
            if (!cfg.IsStackable)
            {
                for (var k = 0; k < count; k++)
                {
                    s.Items.Add(new ItemInstance
                    {
                        InstanceId = s.NextInstanceId++,
                        ItemId = itemId,
                        Count = 1,
                        Durability = cfg.MaxDurability,
                        Owner = owner,
                        ExpiresAt = cfg.ShelfLifeMinutes.HasValue ? s.GameMinute + cfg.ShelfLifeMinutes.Value : (int?)null,
                    });
                }
                return;
            }
            s.Items.Add(new ItemInstance
            {
                InstanceId = s.NextInstanceId++,
                ItemId = itemId,
                Count = count,
                Owner = owner,
                ExpiresAt = cfg.ShelfLifeMinutes.HasValue ? s.GameMinute + cfg.ShelfLifeMinutes.Value : (int?)null,
            });
            Normalize(s, owner);
        }

        /// <summary>同物品且同到期时间、未预留的实例合堆，超过堆叠上限分为新堆。</summary>
        private void Normalize(GameState s, string owner)
        {
            var groups = s.Items
                .Where(i => i.Owner == owner && i.ReservedBy == null && _items[i.ItemId].IsStackable)
                .GroupBy(i => (i.ItemId, i.ExpiresAt))
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
