using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace Survival.Core.Config
{
    public sealed class ConfigException : Exception
    {
        public IReadOnlyList<string> Errors { get; }

        public ConfigException(IReadOnlyList<string> errors)
            : base("配置校验失败：" + string.Join("；", errors))
        {
            Errors = errors;
        }
    }

    public static class ConfigLoader
    {
        public static GameConfig FromJson(string json, bool validate = true)
        {
            var cfg = JsonConvert.DeserializeObject<GameConfig>(json)
                      ?? throw new ConfigException(new[] { "配置为空" });
            if (validate)
            {
                var errors = Validate(cfg);
                if (errors.Count > 0) throw new ConfigException(errors);
            }
            return cfg;
        }

        public static List<string> Validate(GameConfig c)
        {
            var e = new List<string>();
            void Dup<T>(IEnumerable<T> list, Func<T, string> key, string what)
            {
                foreach (var g in list.GroupBy(key).Where(g => g.Count() > 1))
                    e.Add($"{what} ID 重复：{g.Key}");
            }

            Dup(c.Items, i => i.Id, "物品");
            Dup(c.Locations, l => l.Id, "地点");
            Dup(c.Edges, x => x.Id, "连接");
            Dup(c.Resources, r => r.NodeId, "资源点");
            Dup(c.Recipes, r => r.Id, "配方");

            var items = new HashSet<string>(c.Items.Select(i => i.Id));
            var locs = new HashSet<string>(c.Locations.Select(l => l.Id));
            var tick = c.Time.TickMinutes;
            var milestoneSet = new HashSet<int> { 25, 50, 75, 100 };

            foreach (var it in c.Items)
            {
                if (it.WeightGrams <= 0) e.Add($"物品 {it.Id} 重量必须为正");
                if (it.StackMax < 1) e.Add($"物品 {it.Id} 堆叠上限必须≥1");
                if (it.ShelfLifeMinutes.HasValue && it.ShelfLifeMinutes.Value % tick != 0)
                    e.Add($"物品 {it.Id} 保质期须为{tick}的倍数");
            }

            foreach (var ed in c.Edges)
            {
                if (!locs.Contains(ed.A) || !locs.Contains(ed.B)) e.Add($"连接 {ed.Id} 引用了不存在的地点");
                if (ed.BaseMinutes <= 0 || ed.BaseMinutes % tick != 0) e.Add($"连接 {ed.Id} 时长须为{tick}的正倍数");
                if (!ed.InitiallyUnlocked && ed.UnlockAny.Count == 0) e.Add($"连接 {ed.Id} 既非开局解锁也无解锁条件");
                foreach (var u in ed.UnlockAny)
                {
                    if (!locs.Contains(u.Location)) e.Add($"连接 {ed.Id} 解锁条件引用了不存在的地点 {u.Location}");
                    if (!milestoneSet.Contains(u.Progress)) e.Add($"连接 {ed.Id} 解锁阈值 {u.Progress} 不在 25/50/75/100 中");
                }
            }

            foreach (var r in c.Resources)
            {
                if (!locs.Contains(r.LocationId)) e.Add($"资源点 {r.NodeId} 地点不存在");
                if (r.BaseMinutes <= 0 || r.BaseMinutes % tick != 0) e.Add($"资源点 {r.NodeId} 时长须为{tick}的正倍数");
                if (r.RevealAt.HasValue && !milestoneSet.Contains(r.RevealAt.Value)) e.Add($"资源点 {r.NodeId} revealAt 不在 25/50/75/100 中");
                if (!r.RevealAt.HasValue && !r.RevealOnVisit) e.Add($"资源点 {r.NodeId} 没有发现条件");
                foreach (var o in r.Outputs.Keys)
                    if (!items.Contains(o)) e.Add($"资源点 {r.NodeId} 产出物品 {o} 不存在");
                if (r.IsUnits && r.Outputs.Count != 1) e.Add($"资源点 {r.NodeId} 按件数计库存时只能有一种产物");
                if (!r.IsUnlimited && !r.Cap.HasValue) e.Add($"资源点 {r.NodeId} 缺少上限");
                if (r.ToolBonus != null && !c.Items.Any(i => i.ToolTag == r.ToolBonus.ToolTag))
                    e.Add($"资源点 {r.NodeId} 工具标签 {r.ToolBonus.ToolTag} 无对应工具");
            }

            foreach (var rc in c.Recipes)
            {
                if (rc.BaseMinutes <= 0 || rc.BaseMinutes % tick != 0) e.Add($"配方 {rc.Id} 时长须为{tick}的正倍数");
                foreach (var k in rc.Inputs.Keys.Concat(rc.Outputs.Keys))
                    if (!items.Contains(k)) e.Add($"配方 {rc.Id} 引用了不存在的物品 {k}");
                foreach (var cat in rc.Catalysts)
                    if (!c.Items.Any(i => i.ToolTag == cat)) e.Add($"配方 {rc.Id} 催化工具 {cat} 不存在");
            }

            foreach (var k in c.StartingCarried.Keys.Concat(c.StartingCampGround.Keys))
                if (!items.Contains(k)) e.Add($"开局物品 {k} 不存在");

            foreach (var a in c.Actions.RateAliases.Values.Distinct())
                if (!c.HourlyRates.ContainsKey(a)) e.Add($"速率别名指向不存在的类别 {a}");
            foreach (var k in new[] { "sleep_sheltered", "sleep_outdoors" })
                if (!c.HourlyRates.ContainsKey(k)) e.Add($"缺少速率类别 {k}");

            foreach (var k in new[] { "satiety", "hydration", "wakefulness", "energy", "morale", "health" })
                if (!c.Thresholds.ContainsKey(k)) e.Add($"缺少阈值 {k}");

            foreach (var m in c.Actions.WaitMinutes.Concat(c.Actions.RestMinutes).Concat(c.Actions.SleepMinutes))
                if (m <= 0 || m % tick != 0) e.Add($"行动时长 {m} 须为{tick}的正倍数");

            if (c.Locations.Count(l => l.InitiallyVisited) != 1) e.Add("必须恰好有一个开局所在地点");
            if (c.Actions.ClearMinutes <= 0 || c.Actions.ClearMinutes % tick != 0) e.Add($"清理时长须为{tick}的正倍数");

            // 清理与共享掉落表（地图清理案 5.1、探索收获案 7）
            foreach (var l in c.Locations)
            {
                if (string.IsNullOrEmpty(l.LootTableId)) e.Add($"地点 {l.Id} 缺少 lootTableId");
                else if (!c.LootTables.ContainsKey(l.LootTableId)) e.Add($"地点 {l.Id} 的掉落表 {l.LootTableId} 不存在");
                var cl = l.Clear;
                if (cl.Cap < 1) e.Add($"地点 {l.Id} 清理次数上限须≥1");
                if (cl.RefillIntervalMinutes <= 0 || cl.RefillIntervalMinutes % tick != 0) e.Add($"地点 {l.Id} 清理恢复间隔须为{tick}的正倍数");
                if (cl.InitialCharges < 0 || cl.InitialCharges > cl.Cap) e.Add($"地点 {l.Id} 初始清理次数须在0到上限之间");
            }
            foreach (var kv in c.LootTables)
            {
                var t = kv.Value;
                if (string.IsNullOrEmpty(t.Version)) e.Add($"掉落表 {kv.Key} 缺少版本");
                if (t.Entries.Count == 0) e.Add($"掉落表 {kv.Key} 没有条目");
                foreach (var en in t.Entries)
                {
                    if (en.Weight < 0) e.Add($"掉落表 {kv.Key} 权重不能为负");
                    if (en.ItemId == null)
                    {
                        if (en.Count != 0) e.Add($"掉落表 {kv.Key} 的空结果不能带数量");
                    }
                    else
                    {
                        if (!items.Contains(en.ItemId)) e.Add($"掉落表 {kv.Key} 引用了不存在的物品 {en.ItemId}");
                        if (en.Count != 1) e.Add($"掉落表 {kv.Key} 物品 {en.ItemId} 数量须为1");
                    }
                }
                if (t.Entries.Sum(x => x.Weight) != 100) e.Add($"掉落表 {kv.Key} 权重合计须为100");
            }

            // 容器（拖拽与容器案 6.1）
            foreach (var kv in c.Containers)
            {
                var ct = kv.Value;
                if (ct.UnitMl <= 0 || ct.CapacityMl <= 0 || ct.CapacityMl % ct.UnitMl != 0) e.Add($"容器 {kv.Key} 容量须为每份体积的整数倍");
                foreach (var a in ct.Accepts)
                {
                    var it = c.Items.FirstOrDefault(i => i.Id == a);
                    if (it == null) e.Add($"容器 {kv.Key} 可装物品 {a} 不存在");
                    else if (it.UnitVolumeMl != ct.UnitMl) e.Add($"容器 {kv.Key} 可装物品 {a} 的每份体积须为{ct.UnitMl}毫升");
                }
            }
            foreach (var it in c.Items.Where(i => i.ContainerId != null))
                if (!c.Containers.ContainsKey(it.ContainerId!)) e.Add($"物品 {it.Id} 的容器定义 {it.ContainerId} 不存在");
            foreach (var rc in c.Recipes.Where(r => r.Pot != null))
            {
                var pot = c.Items.FirstOrDefault(i => i.ContainerId != null && rc.Catalysts.Contains(i.ToolTag ?? ""));
                if (pot == null) { e.Add($"配方 {rc.Id} 的锅内转化需要容器类催化工具"); continue; }
                if (!c.Containers.TryGetValue(pot.ContainerId!, out var ct)) continue;
                if (!ct.Accepts.Contains(rc.Pot!.InputItem) || !ct.Accepts.Contains(rc.Pot.OutputItem))
                    e.Add($"配方 {rc.Id} 的锅内输入输出须是容器可装物品");
                if (rc.Pot.Units < 1 || rc.Pot.Units > ct.CapacityUnits) e.Add($"配方 {rc.Id} 的锅内份数超出容器容量");
            }
            foreach (var kv in c.Containers)
            {
                if (kv.Value.FoodSlots < 0) e.Add($"容器 {kv.Key} 食材位不能为负");
                foreach (var f in kv.Value.StoredFoodIds)
                {
                    if (!items.Contains(f)) e.Add($"容器 {kv.Key} 可存放食材 {f} 不存在");
                    else if (kv.Value.Accepts.Contains(f)) e.Add($"容器 {kv.Key} 食材 {f} 不能同时是液体");
                }
            }

            // 火堆加工位与设施进程（设施与烹饪案第 2、5 节）
            var fire = c.Fire;
            if (fire.ProcessingSlots < 1) e.Add("火堆加工位须≥1");
            if (fire.MaxPots < 0 || fire.MaxPots > fire.ProcessingSlots) e.Add("火堆锅数上限须在0到加工位数之间");
            foreach (var f in fire.DirectFoodInputIds)
                if (!items.Contains(f)) e.Add($"火堆直烤食材 {f} 不存在");
            Dup(c.Processes, p => p.Id, "设施进程");
            var potCt = c.Items.Where(i => i.ContainerId != null).Select(i => c.Containers.TryGetValue(i.ContainerId!, out var x) ? x : null)
                .FirstOrDefault(x => x != null);
            foreach (var p in c.Processes)
            {
                if (!items.Contains(p.InputId)) e.Add($"设施进程 {p.Id} 的输入物品 {p.InputId} 不存在");
                if (!items.Contains(p.OutputId)) e.Add($"设施进程 {p.Id} 的输出物品 {p.OutputId} 不存在");
                if (p.HeatMinutes <= 0 || p.HeatMinutes % tick != 0) e.Add($"设施进程 {p.Id} 受热分钟须为{tick}的正倍数");
                if (p.Target != ProcessTargets.PotLiquidBatch && p.Target != ProcessTargets.SingleFood) e.Add($"设施进程 {p.Id} 目标类型未知");
                if (p.RequiredOwner != ProcessOwners.FireSlot && p.RequiredOwner != ProcessOwners.PotInFire) e.Add($"设施进程 {p.Id} 位置要求未知");
                if (p.WaterCostMl < 0) e.Add($"设施进程 {p.Id} 耗水不能为负");
                if (p.IsPotBatch)
                {
                    if (potCt == null || !potCt.Accepts.Contains(p.InputId) || !potCt.Accepts.Contains(p.OutputId))
                        e.Add($"设施进程 {p.Id} 的锅内输入输出须是容器可装物品");
                }
                else if (p.InPot)
                {
                    if (potCt == null || !potCt.StoredFoodIds.Contains(p.InputId) || !potCt.StoredFoodIds.Contains(p.OutputId))
                        e.Add($"设施进程 {p.Id} 的食材须在锅白名单内");
                    if (p.RequiredLiquid != null && (potCt == null || !potCt.Accepts.Contains(p.RequiredLiquid)))
                        e.Add($"设施进程 {p.Id} 所需液体须是容器可装物品");
                    if (p.WaterCostMl > 0 && potCt != null && p.WaterCostMl % potCt.UnitMl != 0)
                        e.Add($"设施进程 {p.Id} 耗水须为每份体积的整数倍");
                }
                else if (!fire.DirectFoodInputIds.Contains(p.InputId))
                    e.Add($"设施进程 {p.Id} 的直烤食材须在火堆直烤白名单内");
            }
            return e;
        }
    }
}
