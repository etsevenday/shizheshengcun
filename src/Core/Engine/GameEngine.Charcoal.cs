using System;
using System.Collections.Generic;
using System.Linq;
using Survival.Core.Config;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>
    /// 火堆“煅烧木炭”投料前的只读说明（Q111，S11/S14/A07 本轮章）：原料来源、空加工位、60 有效受热分钟与燃料单独消耗。
    /// 投料按钮的禁用原因与确认浮窗共用；不改状态。
    /// </summary>
    public sealed class CharcoalLoadInfo
    {
        public bool Ok { get; set; }
        /// <summary>现在不能投料的原因；能投为 null。</summary>
        public string? Reason { get; set; }
        public string InputId { get; set; } = "";
        public string OutputId { get; set; } = "";
        public int InputCount { get; set; }
        public int OutputCount { get; set; }
        public int HeatMinutes { get; set; }
        /// <summary>随身与当前地面未预留、可作原料的木材合计。</summary>
        public int WoodAvailable { get; set; }
        /// <summary>这次会取的木材：“随身木材#3×2”“地面木材#9×2”（随身优先，再按实例号）。</summary>
        public List<string> Sources { get; set; } = new List<string>();
        /// <summary>Q113：同一份明细的结构化形式（预览地点 + 逐堆实例号/取用数/随身或地面/预览时该堆数量）；确认投料时原样带回，供内核重验。</summary>
        public CharcoalPlan Plan { get; set; } = new CharcoalPlan();
        public int FreeSlots { get; set; }
        public int Slots { get; set; }
        public bool FireLit { get; set; }
        public int FuelMinutes { get; set; }
        /// <summary>按现有燃料（冷火先扣点火5分钟、0燃料自动投1木）能给这一批的有效受热分钟（整5分钟段）。</summary>
        public int HeatAvailable { get; set; }
        /// <summary>从 0 煅烧完一批还差几份燃料木材（不含4份原料）；够用为 0。</summary>
        public int FuelWoodShort { get; set; }
        /// <summary>燃料说明：已燃足 60 / 冷火点燃后只余 55 不够一批 / 还需另投几份木材作燃料。</summary>
        public string FuelNote { get; set; } = "";
    }

    /// <summary>Q113 煅烧投料明细里的一堆：实例号、这次取几份、随身还是当前地面、预览时这堆共几份（确认时数量变了即视为来源失效）。</summary>
    public sealed class CharcoalPick
    {
        public int InstanceId { get; set; }
        public int Take { get; set; }
        public bool Carried { get; set; }
        public int Had { get; set; }
    }

    /// <summary>Q113 玩家在浮窗里看到并确认的投料明细：预览时主角所在地点 + 逐堆来源（系统自动分配，玩家不能改）。</summary>
    public sealed class CharcoalPlan
    {
        public string Location { get; set; } = "";
        public List<CharcoalPick> Picks { get; set; } = new List<CharcoalPick>();

        /// <summary>地点与逐堆（实例号、取用数、来源、预览时数量）完全一致。</summary>
        public bool SameAs(CharcoalPlan? o) =>
            o != null && o.Location == Location && o.Picks.Count == Picks.Count
            && Picks.Zip(o.Picks, (a, b) => a.InstanceId == b.InstanceId && a.Take == b.Take && a.Carried == b.Carried && a.Had == b.Had).All(x => x);

        public CharcoalPlan Clone() => new CharcoalPlan
        {
            Location = Location,
            Picks = Picks.Select(p => new CharcoalPick { InstanceId = p.InstanceId, Take = p.Take, Carried = p.Carried, Had = p.Had }).ToList(),
        };
    }

    public sealed partial class GameEngine
    {
        /// <summary>煅烧这一批会从哪些木材里取（随身优先，再当前地面，均不含已预留、借用、正被行动占用的）。</summary>
        private List<(ItemInstance inst, int take)> CharcoalSources(GameState s, ProcessConfig cp)
        {
            var list = new List<(ItemInstance, int)>();
            var left = cp.InputCount;
            foreach (var w in UsableBatches(s, cp.InputId, true).Where(w => !IsLocked(s, w)))
            {
                if (left <= 0) break;
                var take = Math.Min(left, w.Count);
                list.Add((w, take));
                left -= take;
            }
            return list;
        }

        /// <summary>
        /// 现有燃料能给“从现在起投入的一批”多少有效受热分钟（S09 原燃料规则）：燃烧中按现有燃料整 5 分钟段；
        /// 熄灭时先扣点火 5 分钟（点火这段不计热）；燃料为 0 时按点火自动投 1 份木材（+60）计，冷火单木点燃后只余 55。
        /// </summary>
        public int CharcoalHeatAvailable(GameState s, string loc)
        {
            var f = s.Locations[loc].Facility;
            var fire = Config.Fire;
            var fuel = f.FuelMinutes;
            if (!f.FireLit)
            {
                if (fuel < fire.MinFuelToIgnite) fuel = fuel == 0 ? fire.BurnMinutesPerWood : 0;
                fuel = Math.Max(0, fuel - fire.IgniteMinutes);
            }
            return fuel / Tick * Tick;
        }

        /// <summary>“煅烧木炭”投料前说明与禁用原因（只读）。</summary>
        public CharcoalLoadInfo CharcoalLoadPreview(GameState s)
        {
            var here = s.CurrentLocation;
            var cp = CharcoalProcess;
            var info = new CharcoalLoadInfo { Slots = Config.Fire.ProcessingSlots };
            if (cp == null) { info.Reason = "煅烧木炭尚未开放"; return info; }
            info.InputId = cp.InputId;
            info.OutputId = cp.OutputId;
            info.InputCount = cp.InputCount;
            info.OutputCount = cp.OutputCount;
            info.HeatMinutes = cp.HeatMinutes;
            if (!_locs.TryGetValue(here, out var lc) || !lc.HasFire) { info.Reason = "这里没有火堆"; return info; }
            var fac = s.Locations[here].Facility;
            info.FireLit = fac.FireLit;
            info.FuelMinutes = fac.FuelMinutes;
            info.FreeSlots = FireFreeSlots(s, here);
            var sources = CharcoalSources(s, cp);
            info.WoodAvailable = UsableBatches(s, cp.InputId, true).Where(w => !IsLocked(s, w)).Sum(w => w.Count);
            info.Sources = sources.Select(x => $"{(x.inst.IsCarried ? "随身" : "地面")}{ItemName(x.inst.ItemId)}#{x.inst.InstanceId}×{x.take}").ToList();
            info.Plan = new CharcoalPlan
            {
                Location = here,
                Picks = sources.Select(x => new CharcoalPick { InstanceId = x.inst.InstanceId, Take = x.take, Carried = x.inst.IsCarried, Had = x.inst.Count }).ToList(),
            };

            info.HeatAvailable = CharcoalHeatAvailable(s, here);
            var unit = Config.Fire.BurnMinutesPerWood;
            var shortMin = Math.Max(0, cp.HeatMinutes - info.HeatAvailable);
            info.FuelWoodShort = (shortMin + unit - 1) / unit;
            var head = fac.FireLit
                ? $"火堆燃烧中，现有燃料{fac.FuelMinutes}分钟可受热{info.HeatAvailable}分钟"
                : fac.FuelMinutes >= Config.Fire.MinFuelToIgnite
                    ? $"火堆未燃：点火{Config.Fire.IgniteMinutes}分钟不计受热，现有燃料{fac.FuelMinutes}分钟点燃后只余{info.HeatAvailable}分钟可受热"
                    : $"火堆未燃且无燃料：点火时自动投1份木材作燃料（+{unit}分钟），点火{Config.Fire.IgniteMinutes}分钟不计受热，点燃后只余{info.HeatAvailable}分钟可受热";
            var tail = info.FuelWoodShort == 0
                ? $"，够这一批{cp.HeatMinutes}分钟"
                : $"，不够一批{cp.HeatMinutes}分钟：还需另投{info.FuelWoodShort}份木材作燃料";
            info.FuelNote = $"{head}{tail}（{cp.InputCount}份原料不算燃料；燃料按火堆整体扣，两批并行不倍扣）";

            info.Reason = CharcoalLoadBlocked(s, cp, info);
            info.Ok = info.Reason == null;
            return info;
        }

        private string? CharcoalLoadBlocked(GameState s, ProcessConfig cp, CharcoalLoadInfo info)
        {
            if (s.Progress.Dead) return "角色已死亡";
            if (s.Battle != null) return "战斗中不能整理物品";
            if (s.Encounter != null) return "发现敌踪：请先选择接敌或绕行";
            if (DialogueLocked(s)) return DialogueLockedReason;
            var busy = HolderBlocked(s);
            if (busy != null) return busy;
            if (info.FreeSlots < 1) return FireFullText(s, s.CurrentLocation);
            if (info.WoodAvailable < cp.InputCount)
                return $"{ItemName(cp.InputId)}不足：随身与当前地面共{info.WoodAvailable}/{cp.InputCount}份（不取队友或别处的）";
            return null;
        }

        /// <summary>
        /// Q113 确认时重验玩家看到的明细（S11/S14/A07 Q113 章）：预览地点、逐堆实例仍是本人随身/当前地面未预留未占用的木材且数量与预览时相同，
        /// 并且按现在的木材重新自动分配仍得到同一份明细（随身优先、同来源按实例号）。任一不符返回拒绝原因（整条拒绝，不取其中几份、不换另一堆）；全符返回 null。
        /// </summary>
        private string? CharcoalPlanStale(GameState s, ProcessConfig cp, CharcoalPlan plan, CharcoalLoadInfo now)
        {
            const string tail = "，请重新查看明细后再确认；本次未投料";
            if (plan.Location != s.CurrentLocation)
                return $"已不在预览时的地点（{(_locs.ContainsKey(plan.Location) ? LocName(plan.Location) : plan.Location)}）{tail}";
            if (plan.Picks.Count == 0 || plan.Picks.Any(p => p.Take < 1) || plan.Picks.Sum(p => p.Take) != cp.InputCount
                || plan.Picks.Select(p => p.InstanceId).Distinct().Count() != plan.Picks.Count)
                return $"投料明细不完整（须合计{ItemName(cp.InputId)}×{cp.InputCount}）{tail}";
            var usable = new HashSet<int>(UsableBatches(s, cp.InputId, true).Where(w => !IsLocked(s, w)).Select(w => w.InstanceId));
            foreach (var p in plan.Picks)
            {
                var label = $"{(p.Carried ? "随身" : "地面")}{ItemName(cp.InputId)}#{p.InstanceId}";
                var w = FindItem(s, p.InstanceId);
                var placeOk = w != null && (p.Carried ? w.IsCarried : w.Owner == plan.Location);
                if (w == null || w.ItemId != cp.InputId || !placeOk || !usable.Contains(w.InstanceId))
                    return $"木材来源已变化（{label}已不可用）{tail}";
                if (w.Count != p.Had)
                    return $"木材来源已变化（{label}由{p.Had}份变为{w.Count}份）{tail}";
            }
            if (!plan.SameAs(now.Plan))
                return $"木材来源已变化（现在会取：{string.Join("、", now.Sources)}）{tail}";
            return null;
        }

        /// <summary>
        /// Q111 投料（S11/S01 本轮章）：0 世界分钟；从随身与当前地面未预留的木材原子取足 4 份，合成一批放进当前火堆一个空加工位，
        /// 立即建立 fire_charcoal 进程（受热 0/60）。缺料、无空位、来源失效整次拒绝，什么都不变。不加燃料、不点火。
        /// Q113：带上玩家确认的明细时先重验（<see cref="CharcoalPlanStale"/>），不符整条拒绝、不静默改取别的堆；
        /// 不带明细的内部/旧调用按当时自动分配取料（同样先整体校验再一次迁移）。
        /// </summary>
        private ActionResult LoadCharcoal(GameState s, CharcoalPlan? plan)
        {
            var info = CharcoalLoadPreview(s);
            if (!info.Ok) return ActionResult.Fail(info.Reason ?? "不能投料");
            var cp = CharcoalProcess!;
            if (plan != null && CharcoalPlanStale(s, cp, plan, info) is string stale) return ActionResult.Fail(stale);
            var here = s.CurrentLocation;
            var owner = FireOwner(here);
            var sources = CharcoalSources(s, cp);
            if (sources.Sum(x => x.take) != cp.InputCount) return ActionResult.Fail($"{ItemName(cp.InputId)}不足{cp.InputCount}份");
            ItemInstance batch;
            if (sources.Count == 1 && sources[0].inst.Count == cp.InputCount)
            {
                // 恰好一整堆 4 份：这堆真实木材原样移入加工位
                batch = sources[0].inst;
                batch.Owner = owner;
            }
            else
            {
                batch = sources[0].inst.Clone();
                batch.InstanceId = s.NextInstanceId++;
                batch.Count = cp.InputCount;
                batch.Owner = owner;
                batch.ReservedBy = null;
                s.Items.Add(batch);
                foreach (var (w, take) in sources)
                {
                    w.Count -= take;
                    if (w.Count <= 0) s.Items.Remove(w);
                }
            }
            batch.PlacedOrder = s.NextProcessSeq++;
            SyncFacility(s, here);
            var slot = s.Locations[here].Facility.ProcessingSlots.IndexOf(batch.InstanceId) + 1;
            Emit(s, GameEventKind.Info,
                $"把{ItemName(cp.InputId)}×{cp.InputCount}（{string.Join("、", info.Sources)}）放进{LocName(here)}火堆加工位{slot}煅烧木炭："
                + $"需{cp.HeatMinutes}有效受热分钟，完成得{ItemName(cp.OutputId)}×{cp.OutputCount}；燃料另计（现有燃料可给这一批受热{info.HeatAvailable}分钟"
                + (info.FuelWoodShort > 0 ? $"，还差{info.FuelWoodShort}份燃料木材）。" : "）。"));
            UpdateFlags(s);
            return ActionResult.Success();
        }

        /// <summary>火堆卡与右栏用的一句话：“煅烧木炭：木材×4→木炭×3，占1个加工位，需60有效受热分钟，燃料另计”。</summary>
        public string CharcoalRuleText()
        {
            var cp = CharcoalProcess;
            return cp == null ? "" : $"{ItemName(cp.InputId)}×{cp.InputCount}→{ItemName(cp.OutputId)}×{cp.OutputCount}，占1个加工位，需{cp.HeatMinutes}有效受热分钟，火堆燃料另计，不给经验";
        }
    }
}
