using System;
using System.Collections.Generic;
using System.Linq;
using Survival.Core.Config;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>
    /// 自选饮水的逐字稿（B56；S10／S12／S14 文首「2026-11-17 B53a完整原批Q928—Q929」§本批逐字稿，合同 assets/20261117/b53a-q928-q929-water-100ml.json copy）。
    /// 入口按钮与完成日志按 02 Q975 A 定稿，见开发记录 B56 暂用文案。
    /// </summary>
    public static class DrinkCopy
    {
        /// <summary>饮水量窗口标题。</summary>
        public const string Title = "饮水";
        /// <summary>水量选择旁。</summary>
        public const string Amount = "{毫升}毫升";
        /// <summary>原饮水确认／开始动作。</summary>
        public const string Action = "饮用{毫升}毫升（5分钟）";
        /// <summary>饮水右栏所选用量。</summary>
        public const string RightAmount = "饮水：{水种}{毫升}毫升";
        /// <summary>饮水右栏基础补水。</summary>
        public const string RightHydration = "补水：{补水值}";
        /// <summary>饮水右栏耗时。</summary>
        public const string RightDuration = "耗时：5分钟";
        /// <summary>水量不足／不足一新单位的数量禁用原因。</summary>
        public const string Short = "水分不足。";
        /// <summary>食饮确认的单次水量摘要。</summary>
        public const string Summary = "{水种}{所选毫升}毫升";

        /// <summary>Q975 A 定稿：卡下／详情的饮水入口按钮（脏水接「（风险）」）。</summary>
        public const string Entry = "饮用";
        public const string EntryRisky = "饮用（风险）";
        /// <summary>Q975 A 定稿：主角自饮完成日志的动作名部分「饮用{水种}{毫升}毫升」（日志整句＝动作名＋「：{变化}。」）。</summary>
        public const string LogLabel = "饮用{水种}{毫升}毫升";

        public static string AmountText(int ml) => Amount.Replace("{毫升}", ml.ToString());
        public static string ActionText(int ml) => Action.Replace("{毫升}", ml.ToString());
        public static string RightAmountText(string water, int ml) => RightAmount.Replace("{水种}", water).Replace("{毫升}", ml.ToString());
        public static string RightHydrationText(double w) => RightHydration.Replace("{补水值}", Fmt.T(w));
        public static string SummaryText(string water, int ml) => Summary.Replace("{水种}", water).Replace("{所选毫升}", ml.ToString());
        public static string LogLabelText(string water, int ml) => LogLabel.Replace("{水种}", water).Replace("{毫升}", ml.ToString());

        /// <summary>合同 copy 键 → 逐字稿，测试逐句比对（drinkTitle、drinkAction、amount、shortage）。</summary>
        public static readonly IReadOnlyDictionary<string, string> Contract = new Dictionary<string, string>
        {
            ["drinkTitle"] = Title, ["drinkAction"] = Action, ["amount"] = Amount, ["shortage"] = Short,
        };
    }

    /// <summary>一个饮水来源的水量（只读）：水卡一堆、锅里一种水、水壶。</summary>
    public sealed class DrinkSourceInfo
    {
        public string LiquidId { get; set; } = "";
        public string WaterName { get; set; } = "";
        /// <summary>每单位毫升（水种每份体积，现 100）。</summary>
        public int UnitMl { get; set; }
        /// <summary>本来源合法未锁未预留的实际毫升（水壶含不足一单位的尾水）。</summary>
        public int FreeMl { get; set; }
        /// <summary>可选的最大单位数 floor(FreeMl／UnitMl)。</summary>
        public int MaxUnits { get; set; }
    }

    /// <summary>自选饮水的结构化预览（右栏三行与理论补水）。</summary>
    public sealed class DrinkPreviewInfo
    {
        public string WaterName { get; set; } = "";
        public int Units { get; set; }
        public int Ml { get; set; }
        /// <summary>理论基础补水＝实际毫升×每份补水／每份毫升（=6N），未计上限与 5 分钟代谢。</summary>
        public double Hydration { get; set; }
        public int MaxUnits { get; set; }
        /// <summary>「饮水：…」「补水：…」「耗时：5分钟」。</summary>
        public List<string> Lines { get; set; } = new List<string>();
    }

    /// <summary>
    /// 自选饮水（B56；S10／S12 文首 B53a §饮水自选量，一次固定5分钟；验收 W100-Q929-04—06、W56-Q974-03）：
    /// 水卡、锅水、水壶都按整数 N 个 100 毫升单位饮用；一次只饮所选同一来源的一种水；整次一个 5 分钟动作、只扣 N×100 毫升一次；
    /// 基础补水按实际毫升×每份补水／每份毫升（=6N），脏水任意正量腹泻一次 12 小时，不按 N 叠加。
    /// </summary>
    public sealed partial class GameEngine
    {
        /// <summary>这件实例能否作为饮水来源（水卡、锅里的水、装着水的水壶）。</summary>
        public bool IsDrinkSource(ItemInstance i) =>
            CanteenOf(i) != null ? i.LiquidId != null && i.LiquidMl > 0 : IsLiquid(i) && _items[i.ItemId].Use != null;

        /// <summary>来源的水种：水壶为壶内水种，其他为物品本身。</summary>
        public string? DrinkLiquidOf(ItemInstance i) => CanteenOf(i) != null ? (i.LiquidMl > 0 ? i.LiquidId : null) : IsLiquid(i) ? i.ItemId : null;

        /// <summary>
        /// 来源的可饮水量：水卡＝这一堆份数×每份；锅水＝该锅未被水煮预留的整份×每份（同锅只有一种水）；水壶＝壶内实际毫升（含尾水）。
        /// 不是饮水来源为 null。不判位置、占用、战斗等动作锁（那些在 Validate 里按原顺序判）。
        /// </summary>
        public DrinkSourceInfo? DrinkSource(GameState s, ItemInstance i)
        {
            var liquid = DrinkLiquidOf(i);
            if (liquid == null || !_items.TryGetValue(liquid, out var lc) || lc.UnitVolumeMl is not int unit || unit <= 0) return null;
            int free;
            if (CanteenOf(i) != null) free = i.LiquidMl;
            else if (i.InContainer) free = PotFreeWaterUnits(s, i) * unit;
            else free = i.Count * unit;
            return new DrinkSourceInfo { LiquidId = liquid, WaterName = ItemName(liquid), UnitMl = unit, FreeMl = Math.Max(0, free), MaxUnits = Math.Max(0, free) / unit };
        }

        /// <summary>最多可选几单位；不是饮水来源为 0。</summary>
        public int MaxDrinkUnits(GameState s, ItemInstance i) => DrinkSource(s, i)?.MaxUnits ?? 0;

        /// <summary>
        /// 选 units 个单位能否饮用：不足一单位或所选超过可饮量为「水分不足。」，小于 1 为通用整数范围句；能为 null。
        /// </summary>
        public string? DrinkUnitsBlocked(GameState s, ItemInstance i, int units)
        {
            var src = DrinkSource(s, i);
            if (src == null) return "该物品不能直接使用";
            if (src.MaxUnits < 1) return DrinkCopy.Short;
            if (units < 1) return QtyRangeText(src.MaxUnits);
            return units > src.MaxUnits ? DrinkCopy.Short : null;
        }

        /// <summary>N 单位的效果：水种原 use 的各项×N（水只有补水；6N），腹泻沿原「必定腹泻」一次。</summary>
        private ItemUseConfig DrinkUse(string liquidId, int units)
        {
            var u = _items[liquidId].Use!;
            return new ItemUseConfig
            {
                Satiety = u.Satiety * units, Hydration = u.Hydration * units, Morale = u.Morale * units, Health = u.Health * units,
                CausesDiarrhea = u.CausesDiarrhea,
            };
        }

        /// <summary>理论基础补水（预览右栏）：N×每份补水。</summary>
        public double DrinkHydration(string liquidId, int units) => (_items[liquidId].Use?.Hydration ?? 0) * units;

        /// <summary>只扣 N×每份毫升一次：水卡减份数（到 0 移除）、锅里按实例号先后减同种水份数、水壶减实际毫升（喝空回空壶）。</summary>
        private void ConsumeDrink(GameState s, ItemInstance i, string liquidId, int units)
        {
            var unit = _items[liquidId].UnitVolumeMl ?? 0;
            if (CanteenOf(i) != null)
            {
                i.LiquidMl -= units * unit;
                if (i.LiquidMl <= 0) { i.LiquidMl = 0; i.LiquidId = null; }
                return;
            }
            if (i.InContainer && ContainerOf(s, i) is ItemInstance pot)
            {
                // 先扣所选那一堆，不够再按实例号扣同锅同种水（锅水总按整份存放）
                var left = units;
                foreach (var w in new[] { i }.Concat(PotLiquids(s, pot.InstanceId).Where(w => w.ItemId == liquidId && w != i)).ToList())
                {
                    if (left <= 0) break;
                    var take = Math.Min(left, w.Count);
                    w.Count -= take;
                    left -= take;
                    if (w.Count <= 0) s.Items.Remove(w);
                }
                SyncFacilities(s);
                return;
            }
            i.Count -= units;
            if (i.Count <= 0) s.Items.Remove(i);
        }

        /// <summary>
        /// 饮水落到 actor（主角自饮与同行食饮共用）：扣 N 单位一次并按 6N 给补水、脏水腹泻一次。actualDelta 见 ApplyUseEffect
        /// （BM13 起两处都传 true：02 Q1010 A 自饮日志与同行结果句都写实际补水）。
        /// 水壶在{变化}末尾不追加余量（主角自饮的「壶里剩…」由调用方加）。
        /// </summary>
        private void ApplyDrink(GameState s, ItemInstance i, ActorState actor, int units, List<string> parts, bool actualDelta)
        {
            var liquid = DrinkLiquidOf(i)!;
            var use = DrinkUse(liquid, units);
            ConsumeDrink(s, i, liquid, units);
            ApplyUseEffect(s, actor, use, parts, actualDelta);
        }

        /// <summary>预览右栏的饮水块：所选毫升、理论补水与耗时三行。</summary>
        private DrinkPreviewInfo DrinkPreview(GameState s, ItemInstance i, int units)
        {
            var src = DrinkSource(s, i)!;
            var ml = units * src.UnitMl;
            var w = DrinkHydration(src.LiquidId, units);
            return new DrinkPreviewInfo
            {
                WaterName = src.WaterName, Units = units, Ml = ml, Hydration = w, MaxUnits = src.MaxUnits,
                Lines = new List<string> { DrinkCopy.RightAmountText(src.WaterName, ml), DrinkCopy.RightHydrationText(w), DrinkCopy.RightDuration },
            };
        }

        /// <summary>Q975 A 定稿：饮水入口按钮「饮用」／脏水「饮用（风险）」；食物、药品沿原「食用1份」等。</summary>
        public static string DrinkEntryText(ItemConfig liquid) => UseRisky(liquid) ? DrinkCopy.EntryRisky : DrinkCopy.Entry;
    }
}
