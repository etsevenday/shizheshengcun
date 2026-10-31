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
        /// Q111 投料（S11/S01 本轮章）：0 世界分钟；从随身与当前地面未预留的木材原子取足 4 份，合成一批放进当前火堆一个空加工位，
        /// 立即建立 fire_charcoal 进程（受热 0/60）。缺料、无空位、来源失效整次拒绝，什么都不变。不加燃料、不点火。
        /// </summary>
        private ActionResult LoadCharcoal(GameState s)
        {
            var info = CharcoalLoadPreview(s);
            if (!info.Ok) return ActionResult.Fail(info.Reason ?? "不能投料");
            var cp = CharcoalProcess!;
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
