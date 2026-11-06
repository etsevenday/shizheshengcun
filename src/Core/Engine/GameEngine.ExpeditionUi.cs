using System;
using System.Collections.Generic;
using System.Linq;
using Survival.Core.Config;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>远征物资面板的一行。</summary>
    public sealed class ExpeditionStoreRow
    {
        public int InstanceId { get; set; }
        public string Text { get; set; } = "";
        /// <summary>本次地牢实例的战利品（可封存）。</summary>
        public bool Reward { get; set; }
        public bool Spoiled { get; set; }
        /// <summary>现在能封存（来源与地点都合格）。</summary>
        public bool CanSeal { get; set; }
    }

    /// <summary>远征物资分栏（A10-EXUI §6）：本人可用物资 / 当前节点地面 / 本次已封存战利品，另列仍在旧实例别处的东西。</summary>
    public sealed class ExpeditionStoresView
    {
        public string DungeonName { get; set; } = "";
        public string ParentName { get; set; } = "";
        public string NodeName { get; set; } = "";
        public List<ExpeditionStoreRow> Usable { get; set; } = new List<ExpeditionStoreRow>();
        public List<ExpeditionStoreRow> Ground { get; set; } = new List<ExpeditionStoreRow>();
        public List<ExpeditionStoreRow> Sealed { get; set; } = new List<ExpeditionStoreRow>();
        /// <summary>同一实例别的房间地面/队友遗物里的东西（“仍在旧实例”，不随队）。</summary>
        public List<string> Elsewhere { get; set; } = new List<string>();
    }

    /// <summary>
    /// 远征界面完整版的只读数据（开发计划 v0.9 B23，R72；A10-EXUI §5 饮水处理与远征携带、§6 已得/封存/可用分栏，开发假设 B23-7、B23-9）。
    /// 只读，不推进时间、不改存档；按钮的可用与原因仍走 SealBlocked / UnsealBlocked。
    /// </summary>
    public sealed partial class GameEngine
    {
        /// <summary>
        /// 进入预告饮水块的行（A10 §5、Q266）：估算与库存、Q267 处理块、处理用品、热源、封存附注、统一附注。标题 WaterCopy.Heading 由界面写。
        /// 只数全队随身；封存物与地面不计。只提示、不拦。
        /// </summary>
        public List<string> ExpeditionWaterLines(GameState s, string dungeonId) => ExpeditionWaterLines(s, ExpeditionWaterBudget(s, dungeonId));

        private List<string> ExpeditionWaterLines(GameState s, WaterBudgetInfo budget)
        {
            var carried = PartyCarriedOwners(s);
            var mine = s.Items.Where(i => carried.Contains(i.Owner) || (i.InContainer && FindItem(s, ContainerIdOf(i)) is ItemInstance pot && carried.Contains(pot.Owner))).ToList();
            int Cards(string id) => mine.Where(i => i.ItemId == id && !i.InContainer).Sum(i => i.Count);
            var lines = WaterBudgetLines(budget).Concat(WaterTreatmentLines(budget)).ToList();
            // 只列供给主案定义过的处理路径用到的随身物品（净水片、滤芯与滤水器、烧水用的锅）
            var treat = new List<string>();
            foreach (var rc in Config.Recipes.Where(r => r.Inputs.ContainsKey("dirty_water") && r.Outputs.ContainsKey("clean_water")))
                foreach (var id in rc.Inputs.Keys.Where(k => k != "dirty_water").Concat(rc.Catalysts.SelectMany(t => Config.Items.Where(i => i.ToolTag == t).Select(i => i.Id))))
                {
                    var n = mine.Where(i => i.ItemId == id).Sum(i => i.Count);
                    if (n > 0 && !treat.Any(t => t.StartsWith(ItemName(id) + "×"))) treat.Add($"{ItemName(id)}×{n}");
                }
            var pots = mine.Count(i => IsPot(i) && !i.InContainer);
            if (pots > 0 && Config.Processes.Any(p => p.InputId == "dirty_water")) treat.Add($"{ItemName("pot")}×{pots}（火堆烧水）");
            lines.Add("处理用品：" + (treat.Count == 0 ? "没有" : string.Join("、", treat)));
            var heat = new List<string>();
            foreach (var it in mine.Where(i => !i.InContainer && _items.TryGetValue(i.ItemId, out var c) && c.ToolTag == "lighter"))
                heat.Add(ItemName(it.ItemId) + (it.Durability.HasValue && it.MaxDurability.HasValue ? $"（耐久{Fmt.N(it.Durability.Value)}/{Fmt.N(it.MaxDurability.Value)}）" : ""));
            var wood = Cards("wood");
            if (wood > 0) heat.Add($"{ItemName("wood")}×{wood}（每份燃烧{Config.Fire.BurnMinutesPerWood}分钟）");
            lines.Add("热源：" + (heat.Count == 0 ? "没有点火工具与燃料" : string.Join("、", heat)));
            if (s.Expedition != null && SealedItems(s, s.Expedition.InstanceId).Any(i => _items.TryGetValue(i.ItemId, out var c) && c.Kind == "water"))
                lines.Add("封存中的水不计入（本次不可直接使用）");
            lines.Add(WaterCopy.Disclaimer);
            return lines;
        }

        private static int ContainerIdOf(ItemInstance i) =>
            int.TryParse(i.Owner.Substring(ItemInstance.ContainerPrefix.Length), out var id) ? id : -1;

        /// <summary>远征物资三栏（只在本次远征的地牢里有内容；不在远征里为 null）。</summary>
        public ExpeditionStoresView? ExpeditionStores(GameState s)
        {
            var e = s.Expedition;
            if (e == null || DungeonAt(s.CurrentLocation) != e.DungeonId) return null;
            var d = DungeonCfg(e.DungeonId)!;
            var v = new ExpeditionStoresView { DungeonName = d.Name, ParentName = LocName(d.Parent), NodeName = LocName(s.CurrentLocation) };
            ExpeditionStoreRow Row(ItemInstance it, string where)
            {
                var reward = it.RewardOrigin == e.InstanceId;
                var spoiled = IsSpoiled(s, it);
                return new ExpeditionStoreRow
                {
                    InstanceId = it.InstanceId, Reward = reward, Spoiled = spoiled, CanSeal = reward && SealBlocked(s, it.InstanceId) == null,
                    Text = $"{ItemName(it.ItemId)}×{it.Count}" + (where.Length > 0 ? $"（{where}）" : "") + (reward ? " · 战利品" : "") + (spoiled ? " · 已腐败" : ""),
                };
            }
            foreach (var a in s.PartyActors)
            {
                var owner = ItemInstance.CarriedOwner(a.Id);
                foreach (var it in s.Items.Where(i => i.Owner == owner).OrderBy(i => i.InstanceId))
                    v.Usable.Add(Row(it, a.IsPlayer ? "你" : ActorDisplayName(s, a.Id)));
            }
            foreach (var it in s.Items.Where(i => i.Owner == s.Ground).OrderBy(i => i.InstanceId)) v.Ground.Add(Row(it, ""));
            foreach (var it in SealedItems(s, e.InstanceId))
                v.Sealed.Add(new ExpeditionStoreRow
                {
                    InstanceId = it.InstanceId, Reward = true, Spoiled = IsSpoiled(s, it),
                    Text = $"{ItemName(it.ItemId)}×{it.Count} · 封存中·本次不可直接使用" + (IsSpoiled(s, it) ? " · 已腐败" : ""),
                });
            v.Elsewhere = DungeonLeftovers(s, e.DungeonId)
                .Where(x => x.Item.Owner != s.Ground && !x.Item.Owner.StartsWith(SealedPrefix))
                .Select(x => $"{ItemName(x.Item.ItemId)}×{x.Item.Count}（{x.From}{(IsSpoiled(s, x.Item) ? "，已腐败" : "")}）").ToList();
            return v;
        }
    }
}
