using System.Collections.Generic;
using System.Linq;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>
    /// 卡片桌面的顺序（拖拽与容器案 2）：可堆叠物品按物品成组为一张卡，工具与锅按实例各一张卡。
    /// 顺序存进存档，网页与将来的 UGUI 共用；排序不影响体力、时间或重量。
    /// </summary>
    public sealed partial class GameEngine
    {
        /// <summary>
        /// 卡片键：堆叠物品 owner|itemId，工具 owner|itemId|instanceId（与网页 data-card 一致）。B22 试玩修复 M408：带本实例来源的地牢战利品
        /// （RewardOrigin，可封存）单独成卡 owner|itemId|dr，不与同名外带物叠在一张卡里（A07 X1.13“保留批次/来源，不强行压卡”）。
        /// </summary>
        public string CardKey(ItemInstance i) =>
            !_items[i.ItemId].IsStackable ? $"{i.Owner}|{i.ItemId}|{i.InstanceId}"
            : CardSideOf(i) is string side ? $"{i.Owner}|{i.ItemId}|{side}" : $"{i.Owner}|{i.ItemId}";

        /// <summary>地牢战利品卡的卡片键后缀（B22 M408）。</summary>
        public const string RewardCardSuffix = "dr";

        /// <summary>已鉴定普通白色活性物质卡的卡片键后缀（B22d Q178：鉴定状态不同不混在一个标题下，分卡、逐实例可选）。</summary>
        public const string IdentifiedCardSuffix = "apr";

        /// <summary>堆叠物品属于哪一侧卡：本实例战利品 dr、已鉴定白色 apr，普通为 null。</summary>
        public string? CardSideOf(ItemInstance i) =>
            i.RewardOrigin != null ? RewardCardSuffix : IsIdentifiedWhite(i) ? IdentifiedCardSuffix : null;

        /// <summary>卡片键第三段是不是“侧”（dr/apr）；工具卡第三段是实例号，返回 null。</summary>
        public static string? CardSideOfKey(string[] parts) =>
            parts.Length == 3 && (parts[2] == RewardCardSuffix || parts[2] == IdentifiedCardSuffix) ? parts[2] : null;

        /// <summary>堆叠卡的标题：已鉴定白色卡写“活性物质（白色·已鉴定）”，其余为物品名。</summary>
        public string CardTitle(string itemId, string? side) =>
            side == IdentifiedCardSuffix && !string.IsNullOrEmpty(Ap.IdentifiedName) ? Ap.IdentifiedName : ItemName(itemId);

        /// <summary>按配置顺序的卡片：先堆叠物品组，再工具（同种按实例 ID）。</summary>
        public List<string> NaturalCards(GameState s, string zone)
        {
            var items = s.Items.Where(i => i.Owner == zone).ToList();
            int Order(string id) => Config.Items.FindIndex(c => c.Id == id);
            // 同物品：普通卡在前、本实例战利品卡（B22 M408）紧跟其后
            var stacks = items.Where(i => _items[i.ItemId].IsStackable).OrderBy(i => Order(i.ItemId)).ThenBy(i => CardSideOf(i) ?? "")
                .Select(CardKey).Distinct();
            var tools = items.Where(i => !_items[i.ItemId].IsStackable)
                .OrderBy(i => Order(i.ItemId)).ThenBy(i => i.InstanceId).Select(CardKey);
            return stacks.Concat(tools).ToList();
        }

        /// <summary>某区当前应显示的卡片顺序：手工顺序里仍存在的卡在前，新卡按配置顺序追加在尾部。</summary>
        public List<string> OrderedCards(GameState s, string zone)
        {
            var natural = NaturalCards(s, zone);
            if (!s.Layout.TryGetValue(zone, out var saved) || saved.Count == 0) return natural;
            var set = new HashSet<string>(natural);
            var list = saved.Where(set.Contains).Distinct().ToList();
            list.AddRange(natural.Where(k => !list.Contains(k)));
            return list;
        }

        /// <summary>有手工顺序的区：去掉已不存在的卡，新产物追加尾部，不打乱已排好的顺序。</summary>
        private void SyncLayout(GameState s)
        {
            foreach (var zone in s.Layout.Keys.ToList())
            {
                var now = OrderedCards(s, zone);
                if (!now.SequenceEqual(s.Layout[zone])) s.Layout[zone] = now;
            }
        }

        /// <summary>同区卡片拖到插入线：只改顺序，不改归属；执行中、暂停中都允许，死亡时拒绝。</summary>
        private ActionResult Reorder(GameState s, InstantRequest r)
        {
            if (r.Zone != ItemInstance.Carried && r.Zone != s.Ground) return ActionResult.Fail("只能整理随身或当前地面的卡片");
            var list = OrderedCards(s, r.Zone);
            if (!list.Remove(r.CardKey)) return ActionResult.Fail("这张卡已不在该区域");
            var index = r.Index < 0 ? 0 : r.Index > list.Count ? list.Count : r.Index;
            list.Insert(index, r.CardKey);
            s.Layout[r.Zone] = list;
            return ActionResult.Success();
        }
    }
}
