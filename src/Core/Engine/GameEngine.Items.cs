using System;
using System.Collections.Generic;
using System.Linq;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    public sealed partial class GameEngine
    {
        // ───────────────────────── 物品（搜集案 05） ─────────────────────────

        public bool IsSpoiled(GameState s, ItemInstance i) => i.ExpiresAt.HasValue && i.ExpiresAt.Value <= s.GameMinute;

        public int CarriedGrams(GameState s) =>
            s.Items.Where(i => i.IsCarried).Sum(i => _items[i.ItemId].WeightGrams * i.Count);

        public bool IsOverweight(GameState s) => CarriedGrams(s) > Config.Carry.SoftGrams;

        public IEnumerable<ItemInstance> Accessible(GameState s, bool allowGround) =>
            s.Items.Where(i => i.IsCarried || (allowGround && i.Owner == s.CurrentLocation));

        /// <summary>可消耗批次：到期时间升序 → 同批次随身优先 → 实例 ID 升序。</summary>
        public List<ItemInstance> UsableBatches(GameState s, string itemId, bool allowGround) =>
            Accessible(s, allowGround)
                .Where(i => i.ItemId == itemId && i.ReservedBy == null && !IsSpoiled(s, i))
                .OrderBy(i => i.ExpiresAt ?? int.MaxValue)
                .ThenBy(i => i.IsCarried ? 0 : 1)
                .ThenBy(i => i.InstanceId)
                .ToList();

        public int AvailableCount(GameState s, string itemId, bool allowGround) =>
            UsableBatches(s, itemId, allowGround).Sum(i => i.Count);

        /// <summary>随身、耐久最高（大于0或无耐久）的指定标签工具，平手取实例 ID 小者。</summary>
        public ItemInstance? BestTool(GameState s, string toolTag) =>
            s.Items.Where(i => i.IsCarried && i.ReservedBy == null && _items[i.ItemId].ToolTag == toolTag
                               && (i.Durability == null || i.Durability > 0))
                .OrderByDescending(i => i.Durability ?? int.MaxValue)
                .ThenBy(i => i.InstanceId)
                .FirstOrDefault();

        public ItemInstance? CarriedTool(GameState s, string toolTag) => BestTool(s, toolTag);

        /// <summary>按消耗顺序预留指定数量，必要时拆堆，返回预留实例 ID。</summary>
        private List<int> Reserve(GameState s, string itemId, int count, int actionId)
        {
            var ids = new List<int>();
            var left = count;
            foreach (var b in UsableBatches(s, itemId, true))
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
        private (string text, List<string> ids) ApplyExplore(GameState s, string locationId)
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

            var text = $"探索度{old}%→{now}%";
            text += found.Count > 0 ? $"，发现{string.Join("与", found)}。" : "。";
            if (now >= 100) text += "此地已充分探索。";
            return (text, ids);
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
