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
            return e;
        }
    }
}
