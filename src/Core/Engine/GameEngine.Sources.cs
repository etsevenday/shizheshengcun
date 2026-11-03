using System;
using System.Collections.Generic;
using System.Linq;
using Survival.Core.Config;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>某种物品的一条已知获取来源（A10-EXUI §4 来源查询；只读、0 分钟）。</summary>
    public sealed class ItemSourceView
    {
        /// <summary>resource（已发现资源点）/ recipe（已知配方）/ process（火堆加工）/ service（城市服务）/ drop（战斗、探索、处理尸体等概率来源）。</summary>
        public string Kind { get; set; } = "";
        /// <summary>A10 §3 四类账短标：有限 / 每日补充 / 本实例 / 本局一次；持续水源写“持续”；配方/服务/掉落为空。</summary>
        public string Account { get; set; } = "";
        public string Title { get; set; } = "";
        /// <summary>每次产量、工作时间、库存/日补、合格工具。</summary>
        public string Detail { get; set; } = "";
        /// <summary>现在的状态：可获取 / 已耗尽 / 已领取 / 本代已搜空… / 还没有领取资格。</summary>
        public string Status { get; set; } = "";
        /// <summary>现在真能拿到（库存或领取资格还在，配方可做）才为 true——耗尽/已领/本代搜空不亮“可获取”。</summary>
        public bool Usable { get; set; }
        /// <summary>来源所在地点（地图定位用）；配方与掉落为 null。</summary>
        public string? LocationId { get; set; }
        /// <summary>从当前位置的已开通路线（“当前位置”/“A→B→C · 约N基础分钟”/“现在没有已开通的路线”）。</summary>
        public string Route { get; set; } = "";
    }

    /// <summary>
    /// 来源查询（开发计划 v0.9 B23，R72；A10-EXUI §4“配方可达与缺料查询”、§3 四类账，开发假设 B23-6）：
    /// 只列已发现的资源点（确定产量、工作时间、现库存/补量、合格工具、到达路线与门槛），状态区分已领取/已耗尽/本代已搜空，
    /// 不把耗尽或已领的来源亮成“可获取”；再列已掌握的配方、火堆加工与城市服务；概率来源（战斗/探索/尸体）只写一行，不冒充保底。
    /// 未发现的资源点不列（不泄露未知坐标或深层房间）。只读，不推进时间、不改存档。
    /// </summary>
    public sealed partial class GameEngine
    {
        public List<ItemSourceView> ItemSources(GameState s, string itemId)
        {
            var list = new List<ItemSourceView>();
            foreach (var r in Config.Resources)
            {
                if (!ResourceYields(r, itemId) || !s.Resources.TryGetValue(r.NodeId, out var rs) || !rs.Discovered) continue;
                list.Add(ResourceSource(s, r, itemId));
            }
            // 能拿到的排前面，其余按配置顺序
            list = list.OrderBy(v => v.Usable ? 0 : 1).ToList();
            foreach (var rc in Config.Recipes.Where(rc => !rc.IsSalvage && rc.Outputs.ContainsKey(itemId) && RecipeKnown(s, rc)))
            {
                var locks = RecipeLocks(s, rc);
                list.Add(new ItemSourceView
                {
                    Kind = "recipe", Title = $"配方：{rc.Name}（{StationName(rc.Station)}）",
                    Detail = $"每份×{rc.Outputs[itemId]} · 原料{string.Join("、", rc.AllInputs.Select(kv => $"{ItemName(kv.Key)}×{kv.Value}"))}" +
                             (rc.BaseMinutes > 0 ? $" · 基础{rc.BaseMinutes}分钟" : ""),
                    Status = locks.Count > 0 ? locks[0] : "已掌握，按原料与工坊条件制作", Usable = locks.Count == 0,
                });
            }
            foreach (var p in Config.Processes.Where(p => p.OutputId == itemId))
                list.Add(new ItemSourceView
                {
                    Kind = "process", Title = $"火堆加工：{p.Name}",
                    Detail = $"{ItemName(p.InputId)}放进燃烧的火堆，受热{p.HeatMinutes}分钟得到{ItemName(p.OutputId)}（燃料另计，不是一键处理）",
                    Status = "要火堆、燃料与原料", Usable = true,
                });
            foreach (var svc in Config.City.Services.Where(x => x.Implemented && x.Choices.Any(c => c.Outputs.ContainsKey(itemId))))
                list.Add(new ItemSourceView
                {
                    Kind = "service", Title = $"城市服务：{svc.Title}（{LocName(svc.LocationId)}）", LocationId = svc.LocationId,
                    Detail = svc.Summary + (svc.DailyPartyQuota is int q ? $"（全队每世界日{q}份，有条件）" : ""),
                    Status = Known(s, svc.LocationId) ? "须实际到达并符合条件" : "地点未知", Usable = false, Route = RouteText(s, svc.LocationId),
                });
            // 概率来源：一行说明，不冒充保底
            var drops = new List<string>();
            var z = Config.ZombieLoot.Entries.FirstOrDefault(x => x.ItemId == itemId);
            if (z != null) drops.Add($"击败感染者后按概率掉落（{z.Weight}%）");
            foreach (var a in Config.AnimalCarcasses.Where(a => a.Outputs.ContainsKey(itemId)))
                drops.Add($"处理{ItemName(a.CarcassItemId)}得到×{a.Outputs[itemId]}");
            if (Config.LootTables.Values.Any(t => t.Entries.Any(x => x.ItemId == itemId))) drops.Add("探索/清理时按概率随机掉落");
            if (drops.Count > 0)
                list.Add(new ItemSourceView { Kind = "drop", Title = "概率来源", Detail = string.Join("；", drops), Status = "概率，不是保底", Usable = false });
            return list;
        }

        private static bool ResourceYields(ResourceConfig r, string itemId) =>
            r.Outputs.ContainsKey(itemId) || (r.ExclusiveLoot?.Any(x => x.ItemId == itemId) ?? false) || r.IndependentRare?.ItemId == itemId;

        /// <summary>A10 §3 四类账短标。</summary>
        public static string AccountLabel(ResourceConfig r) =>
            r.IsCampaign ? "本局一次" : r.IsInstance ? "本实例" : r.IsUnlimited ? "持续" : r.Refills ? "每日补充" : "有限";

        private ItemSourceView ResourceSource(GameState s, ResourceConfig r, string itemId)
        {
            var rs = s.Resources[r.NodeId];
            var parts = new List<string>();
            if (r.ExclusiveLoot?.FirstOrDefault(x => x.ItemId == itemId) is CacheLootEntry ex) parts.Add($"随机其一：×{ex.Count}（{ex.Weight}%，不是保底）");
            else if (r.IndependentRare?.ItemId == itemId) parts.Add($"独立{Fmt.Pct(r.IndependentRare.Chance)}概率附带×{r.IndependentRare.Count}（不是保底）");
            else parts.Add($"每次固定×{r.Outputs[itemId]}");
            parts.Add(r.BaseMinutes > 0 ? $"工作{r.BaseMinutes}分钟" : "0分钟领取");
            var (badge, empty) = ResourceStock(s, r);
            if (r.Refills) parts.Add($"库存{badge}，每日00:00补{r.DailyRefill}");
            else if (r.IsUnlimited) parts.Add("源头无库存上限（采集仍要时间）");
            else parts.Add(badge);
            if (r.ToolBonus != null)
            {
                var tools = r.ToolBonus.ItemIds.Count > 0 ? r.ToolBonus.ItemIds.Select(ItemName)
                    : Config.Items.Where(i => i.ToolTag == r.ToolBonus.ToolTag && !string.IsNullOrEmpty(i.ToolTag)).Select(i => i.Name);
                var names = tools.Distinct().ToList();
                if (names.Count > 0) parts.Add("合格工具：" + string.Join("、", names) + (r.ToolBonus.OutputMultiplier > 1 ? $"（产量×{r.ToolBonus.OutputMultiplier}）" : ""));
            }
            string status;
            var usable = false;
            if (r.IsCampaign && CampaignClaimed(s, r.NodeId)) status = "已领取（本局一次，不再发放）";
            else if (!ResourceUnlocked(s, r)) status = "还没有领取资格";
            else if (!InstanceConditionMet(s, r)) status = "需要本代守卫战胜利";
            else if (r.IsInstance && empty) status = "本代已搜空（地牢通关满72世界小时、在父地点确认重置后的新一代恢复）";
            else if (r.IsCache && empty) status = "已搜空（不再补充）";
            else if (empty) status = r.Refills ? $"今天已采空（下次00:00补{r.DailyRefill}）" : "已耗尽（不会补充）";
            else { status = "可获取"; usable = true; }
            if (usable && r.IsInstance) status = $"可获取（本代剩余{rs.Available ?? 0}/{r.Cap}，换代才恢复）";
            var route = RouteText(s, r.LocationId);
            // B23 试玩修复：没有已开通路线时不写“可获取”，原因只写一次（状态即原因，路线栏留空）
            if (usable && route == NoRouteText) { status = NoRouteText; usable = false; route = ""; }
            return new ItemSourceView
            {
                Kind = "resource", Account = AccountLabel(r), Title = $"{LocName(r.LocationId)} · {r.Name}", Detail = string.Join(" · ", parts),
                Status = status, Usable = usable, LocationId = r.LocationId, Route = route,
            };
        }

        private const string NoRouteText = "现在没有已开通的路线";

        /// <summary>从当前位置到某地点的已开通路线说明（只读，不规划穿锁路线）。</summary>
        private string RouteText(GameState s, string locationId)
        {
            if (locationId == s.CurrentLocation) return "当前位置";
            var path = MapPathTo(s, locationId);
            if (path == null) return NoRouteText;
            var names = new List<string> { LocName(s.CurrentLocation) };
            names.AddRange(path.Select(p => LocName(p.To)));
            var text = string.Join("→", names.Count > 5 ? names.Take(2).Concat(new[] { "…" }).Concat(names.Skip(names.Count - 2)) : names);
            return $"{text} · 约{path.Sum(p => p.Minutes)}基础分钟（{path.Count}段，不含倍率）";
        }

        /// <summary>
        /// 配方状态逐类（A10 §4“知识未得、熟练不足、设施未到、工具/燃料不足、材料缺口、来源当前不可达/实例已耗尽”）：只读，按当前随身与当地地面。
        /// 材料缺口一项一行，并写这种材料有没有现在可获取的已知来源。
        /// </summary>
        public List<(string Category, string Text, string? ItemId)> RecipeStatus(GameState s, RecipeConfig rc, int batch = 1)
        {
            var list = new List<(string, string, string?)>();
            if (RecipeUnknownReason(s, rc) is string k) list.Add(("知识未得", k, null));
            foreach (var kv in rc.MinSkills)
            {
                var lv = SkillLevel(s, kv.Key);
                if (lv < kv.Value) list.Add(("熟练不足", $"需要{SkillName(kv.Key)}{kv.Value}级（当前{lv}级）", null));
            }
            if (StationBlocked(s, rc) is string st) list.Add(("设施未到", st, null));
            foreach (var tag in rc.Catalysts)
                if (AccessibleTool(s, tag) == null) list.Add(("工具/燃料不足", $"缺少{Config.Items.FirstOrDefault(i => i.ToolTag == tag)?.Name ?? tag}（随身或当前地面，不消耗）", null));
            foreach (var kv in rc.AllInputs)
            {
                var need = kv.Value * Math.Max(1, batch);
                var have = AvailableCount(s, kv.Key, true);
                if (have >= need) continue;
                var cat = rc.Fuel.ContainsKey(kv.Key) && !rc.Inputs.ContainsKey(kv.Key) ? "工具/燃料不足" : "材料缺口";
                list.Add((cat, $"{ItemName(kv.Key)}缺{need - have}（现有{have}/需{need}）", kv.Key));
                var src = ItemSources(s, kv.Key);
                if (!src.Any(x => x.Usable))
                    list.Add(("来源当前不可达", src.Any(x => x.Kind == "resource")
                        ? $"{ItemName(kv.Key)}：已知资源点现在都拿不到（已耗尽/已领取/本代已搜空或没有已开通的路线）"
                        : $"{ItemName(kv.Key)}：还没有已发现的资源点来源", kv.Key));
            }
            return list;
        }
    }
}
