using System;
using System.Collections.Generic;
using System.Linq;
using Survival.Core.Config;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>
    /// 进入预告饮水块的逐字稿（Q266、Q267，assets/20261105/b29-entry-water-q258-268.json water.copy、treatment.copy）。字段用 FeedCopy.Fill 填。
    /// </summary>
    public static class WaterCopy
    {
        public const string Heading = "饮水与热源（全队随身，只提示）：";
        public const string StockOnly = "全队随身净水{现有}毫升。";
        public const string RouteMissing = "路线时间不足，暂不能估算饮水。";
        public const string PersonUnreliable = "{姓名}：病情失水过快，无法可靠估算饮水。";
        public const string Person = "{姓名}：按当前状态预计需另备净水{低量}—{高量}毫升";
        public const string Summary = "全队随身净水{现有}毫升；保守预计另需携带{低量}—{高量}毫升。";
        public const string Gap = "与现有净水比较，缺口{缺口}毫升";
        public const string Enough = "按当前状态与预计路程，饮水预算可覆盖；仍需逐人手动饮用。";
        public const string Insufficient = "饮水预算不足，建议补充净水或先处理脏水。";
        public const string Disclaimer = "未计战斗拖延、额外停留、未来病情变化与拾取，不保证全程安全。";

        public const string TreatMain = "处理后才可饮用：随身脏水{D}毫升；按现有净水片/滤芯，最多可处理{V}毫升（每批2000毫升，需完整4份脏水）。";
        public const string TreatNoMaterials = "随身没有可用的净水片或滤芯，暂不能按耗材处理脏水。";
        public const string TreatSmallBatch = "随身可用脏水不足2000毫升，未凑足一批。";
        public const string TreatNoFilter = "滤芯{C}份暂不计入：需要可用滤水器";
        public const string TreatBoiling = "可烧水处理，受锅、热源与燃料限制，未计入上述耗材可处理量。";

        /// <summary>合同 water.copy 键 → 逐字稿，测试逐句比对。</summary>
        public static readonly IReadOnlyDictionary<string, string> Water = new Dictionary<string, string>
        {
            ["heading"] = Heading, ["stockOnly"] = StockOnly, ["routeMissing"] = RouteMissing, ["personUnreliable"] = PersonUnreliable,
            ["person"] = Person, ["summary"] = Summary, ["gap"] = Gap, ["enough"] = Enough, ["insufficient"] = Insufficient, ["disclaimer"] = Disclaimer,
        };

        /// <summary>合同 treatment.copy 键 → 逐字稿。</summary>
        public static readonly IReadOnlyDictionary<string, string> Treatment = new Dictionary<string, string>
        {
            ["main"] = TreatMain, ["noMaterials"] = TreatNoMaterials, ["smallBatch"] = TreatSmallBatch, ["noFilter"] = TreatNoFilter, ["boiling"] = TreatBoiling,
        };
    }

    /// <summary>预告里饮水估算的三种状态（Q266 displayPolicy）。</summary>
    public enum WaterBudgetState
    {
        /// <summary>缺可信回撤或探索区间：只写库存与路线不足。</summary>
        RouteMissing,
        /// <summary>有人分母≤0：该人写病情句，全队只写库存。</summary>
        Unreliable,
        Normal,
    }

    /// <summary>一名存活同行（含主角）的估算。</summary>
    public sealed class WaterPersonBudget
    {
        public string ActorId { get; set; } = "";
        /// <summary>主角“你”，其他人实际显示名。</summary>
        public string Name { get; set; } = "";
        /// <summary>r_i：每小时净失水率。</summary>
        public double LossPerHour { get; set; }
        public bool Reliable { get; set; } = true;
        public int LowMl { get; set; }
        public int HighMl { get; set; }
    }

    /// <summary>进入预告的饮水估算（Q218、Q233、Q234、Q261、Q266、Q267）。只读，不写任务、不抽随机。</summary>
    public sealed class WaterBudgetInfo
    {
        public WaterBudgetState State { get; set; }
        /// <summary>配置回撤基础分钟 B；缺路径为 null。不展示给玩家。</summary>
        public int? RouteBaseMinutes { get; set; }
        /// <summary>回撤预算 R（分钟）；缺路径或探索区间不可信时为 null。</summary>
        public int? RouteMinutes { get; set; }
        public List<WaterPersonBudget> Persons { get; set; } = new List<WaterPersonBudget>();
        /// <summary>{现有}：全队随身可喝净水毫升。</summary>
        public int StockMl { get; set; }
        /// <summary>全队{低量}{高量}：逐人相加；只有 Normal 有意义。</summary>
        public int TotalLowMl { get; set; }
        public int TotalHighMl { get; set; }
        public int GapMl { get; set; }
        public bool Enough { get; set; }
        /// <summary>Q234 D：合法随身脏水毫升。</summary>
        public int DirtyMl { get; set; }
        /// <summary>Q234 P：可用净水片整份。</summary>
        public int Tablets { get; set; }
        /// <summary>Q234 C：可用滤芯整份。</summary>
        public int Cartridges { get; set; }
        /// <summary>Q234 F：有可用滤水器。</summary>
        public bool HasFilter { get; set; }
        /// <summary>Q234 V：耗材可处理量毫升，不计入现有净水。</summary>
        public int TreatableMl { get; set; }
        /// <summary>当前同行随身有未锁未预留的锅（Q267 boiling）。</summary>
        public bool HasFreePot { get; set; }
    }

    /// <summary>
    /// 地牢进入预告的饮水预算（开发计划 v0.9 B29，R16、R24、R25、R89、R94、R95）：逐人 r_i 与保守需水、回撤预算 R、净水脏水毫升去重、耗材可处理量。
    /// R 只用配置回撤路线，不走 ComputeExtractPlan 的轨迹和默认段兜底，实际撤出不受影响。
    /// </summary>
    public sealed partial class GameEngine
    {
        // 合同 water.formula 的常量：目标水分余量、主动终止的水分损失、估算单位（250 毫升补 15 水分）
        private const int WaterMarginHydration = 20;
        private const int WaterAbortLoss = 30;
        private const int WaterSipHydration = 15;
        private const int WaterSipMl = 250;
        // 合同 treatment.formula：一批需 4 份 500 毫升脏水
        private const int TreatBatchMl = 2000;

        private static readonly ActionType[] WaterBudgetModes =
        {
            ActionType.Travel, ActionType.Explore, ActionType.Gather, ActionType.Clear, ActionType.Craft, ActionType.Wait, ActionType.Rest,
        };

        /// <summary>
        /// 预算用的回撤边：地牢内部全部双向边加入口缓冲↔父地点。不看本代走过、发现、解锁；不用单向出口，不绕其他地牢或世界地图。
        /// </summary>
        private IEnumerable<EdgeConfig> WaterRouteEdges(DungeonConfig d, string buffer)
        {
            foreach (var e in Config.Edges)
            {
                if (!e.Bidirectional) continue;
                if (DungeonAt(e.A) == d.Id && DungeonAt(e.B) == d.Id) yield return e;
                else if ((e.A == buffer && e.B == d.Parent) || (e.B == buffer && e.A == d.Parent)) yield return e;
            }
        }

        /// <summary>配置 recordNode→parent 的最短基础分钟 B；缺核心、父地点、连接或路径为 null。</summary>
        public int? WaterRouteBaseMinutes(string dungeonId)
        {
            var d = DungeonCfg(dungeonId);
            var buffer = BufferNodeOf(dungeonId);
            if (d == null || buffer == null || !_locs.ContainsKey(d.RecordNode) || !_locs.ContainsKey(d.Parent)) return null;
            return ShortestPath(WaterRouteEdges(d, buffer), d.RecordNode, d.Parent)?.Legs.Sum();
        }

        /// <summary>r_i：七种模式当前净失水率的最大值，含装备修正与腹泻；不含候补照护。</summary>
        public double WaterLossPerHour(GameState s, ActorState actor) =>
            Math.Max(0, WaterBudgetModes.Max(t => -ComputeSegment(s, new ActionState { Type = t, RuleVersion = WorkRuleVersion }, actor).Rates.Hydration));

        /// <summary>V_i(T)：探索 T 分钟、回撤 R 分钟下需另备的净水毫升。调用前已判分母大于 0。</summary>
        private static int WaterNeedMl(double lossPerHour, double hydration, int exploreMinutes, int routeMinutes)
        {
            var lost = Math.Max(lossPerHour * (exploreMinutes + routeMinutes) / 60, lossPerHour * exploreMinutes / 60 + WaterAbortLoss);
            var extra = Math.Max(0, lost + WaterMarginHydration - hydration);
            return WaterSipMl * (int)Math.Ceiling(extra / (WaterSipHydration - lossPerHour / 12) - 1e-9);
        }

        private bool WaterFree(GameState s, ItemInstance i) => i.ReservedBy == null && !IsLocked(s, i);

        /// <summary>全队随身这种水的毫升：水卡、壶液、锅液各算一次，未锁未预留；封存、候补、地面、屋柜不计。</summary>
        private int CarriedWaterMl(GameState s, string waterId)
        {
            var owners = PartyCarriedOwners(s);
            var total = 0;
            foreach (var i in s.Items.Where(x => owners.Contains(x.Owner) && WaterFree(s, x)))
            {
                if (IsCanteen(i))
                {
                    if (i.LiquidId == waterId) total += i.LiquidMl;
                }
                else if (IsPot(i))
                {
                    var ml = PotLiquids(s, i.InstanceId).Where(w => w.ItemId == waterId && WaterFree(s, w)).Sum(w => w.Count) * PotConfig(i).UnitMl;
                    total += Math.Max(0, Math.Min(ml, PotFreeMl(s, i)));
                }
                else if (i.ItemId == waterId) total += i.Count * (_items[i.ItemId].UnitVolumeMl ?? 0);
            }
            return total;
        }

        private int CarriedUsable(GameState s, string itemId)
        {
            var owners = PartyCarriedOwners(s);
            return s.Items.Where(i => i.ItemId == itemId && owners.Contains(i.Owner) && WaterFree(s, i)).Sum(i => i.Count);
        }

        /// <summary>预告的饮水估算。先判路线与探索输入，再判逐人分母；0 分钟是有效值。</summary>
        public WaterBudgetInfo ExpeditionWaterBudget(GameState s, string dungeonId)
        {
            var owners = PartyCarriedOwners(s);
            var b = new WaterBudgetInfo
            {
                RouteBaseMinutes = WaterRouteBaseMinutes(dungeonId),
                StockMl = CarriedWaterMl(s, "clean_water"),
                DirtyMl = CarriedWaterMl(s, "dirty_water"),
                Tablets = CarriedUsable(s, "purification_tablet"),
                Cartridges = CarriedUsable(s, "filter_cartridge"),
                HasFilter = s.Items.Any(i => owners.Contains(i.Owner) && _items[i.ItemId].ToolTag == "water_filter" && WaterFree(s, i) && !(i.Durability <= 0)),
                HasFreePot = s.Items.Any(i => owners.Contains(i.Owner) && IsPot(i) && WaterFree(s, i)),
            };
            b.TreatableMl = TreatBatchMl * Math.Min(b.DirtyMl / TreatBatchMl, b.Tablets + (b.HasFilter ? b.Cartridges : 0));

            if (b.RouteBaseMinutes == null || !ExploreEstimateTrusted(dungeonId))
            {
                b.State = WaterBudgetState.RouteMissing;
                return b;
            }
            var route = ExtractTotalMinutes(b.RouteBaseMinutes.Value, TimeFactor(s, s.Actor(GameState.PlayerId), ActionType.Travel));
            b.RouteMinutes = route;
            var (low, high) = ExploreEstimate(s, dungeonId);
            foreach (var a in s.PartyActors.Where(x => x.Stats.Health > 0))
            {
                var r = WaterLossPerHour(s, a);
                var row = new WaterPersonBudget { ActorId = a.Id, Name = ActorDisplayName(s, a.Id), LossPerHour = r, Reliable = WaterSipHydration - r / 12 > 0 };
                if (row.Reliable)
                {
                    row.LowMl = WaterNeedMl(r, a.Stats.Hydration, low, route);
                    row.HighMl = WaterNeedMl(r, a.Stats.Hydration, high, route);
                }
                b.Persons.Add(row);
            }
            b.State = b.Persons.All(p => p.Reliable) ? WaterBudgetState.Normal : WaterBudgetState.Unreliable;
            if (b.State == WaterBudgetState.Normal)
            {
                b.TotalLowMl = b.Persons.Sum(p => p.LowMl);
                b.TotalHighMl = b.Persons.Sum(p => p.HighMl);
                b.GapMl = Math.Max(0, b.TotalHighMl - b.StockMl);
                b.Enough = b.StockMl >= b.TotalHighMl;
            }
            return b;
        }

        /// <summary>逐人、库存、缺口与结论的行（Q266）。</summary>
        private static IEnumerable<string> WaterBudgetLines(WaterBudgetInfo b)
        {
            var stock = ("现有", b.StockMl.ToString());
            if (b.State == WaterBudgetState.RouteMissing)
            {
                yield return FeedCopy.Fill(WaterCopy.StockOnly, stock);
                yield return WaterCopy.RouteMissing;
                yield break;
            }
            foreach (var p in b.Persons)
            {
                var name = ("姓名", p.Name);
                yield return p.Reliable
                    ? FeedCopy.Fill(WaterCopy.Person, name, ("低量", p.LowMl.ToString()), ("高量", p.HighMl.ToString()))
                    : FeedCopy.Fill(WaterCopy.PersonUnreliable, name);
            }
            if (b.State == WaterBudgetState.Unreliable)
            {
                yield return FeedCopy.Fill(WaterCopy.StockOnly, stock);
                yield break;
            }
            yield return FeedCopy.Fill(WaterCopy.Summary, stock, ("低量", b.TotalLowMl.ToString()), ("高量", b.TotalHighMl.ToString()));
            yield return FeedCopy.Fill(WaterCopy.Gap, ("缺口", b.GapMl.ToString()));
            yield return b.Enough ? WaterCopy.Enough : WaterCopy.Insufficient;
        }

        /// <summary>处理块（Q267），顺序 main→noMaterials→smallBatch→noFilter→boiling；D=0 整块不显示。</summary>
        private static IEnumerable<string> WaterTreatmentLines(WaterBudgetInfo b)
        {
            if (b.DirtyMl <= 0) yield break;
            yield return FeedCopy.Fill(WaterCopy.TreatMain, ("D", b.DirtyMl.ToString()), ("V", b.TreatableMl.ToString()));
            if (b.Tablets + b.Cartridges == 0) yield return WaterCopy.TreatNoMaterials;
            if (b.DirtyMl < TreatBatchMl) yield return WaterCopy.TreatSmallBatch;
            if (b.Cartridges > 0 && !b.HasFilter) yield return FeedCopy.Fill(WaterCopy.TreatNoFilter, ("C", b.Cartridges.ToString()));
            if (b.HasFreePot) yield return WaterCopy.TreatBoiling;
        }
    }
}
