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
        /// <summary>卡片键：堆叠物品 owner|itemId，工具 owner|itemId|instanceId（与网页 data-card 一致）。</summary>
        public string CardKey(ItemInstance i) =>
            _items[i.ItemId].IsStackable ? $"{i.Owner}|{i.ItemId}" : $"{i.Owner}|{i.ItemId}|{i.InstanceId}";

        /// <summary>按配置顺序的卡片：先堆叠物品组，再工具（同种按实例 ID）。</summary>
        public List<string> NaturalCards(GameState s, string zone)
        {
            var items = s.Items.Where(i => i.Owner == zone).ToList();
            int Order(string id) => Config.Items.FindIndex(c => c.Id == id);
            var stacks = items.Where(i => _items[i.ItemId].IsStackable).Select(i => i.ItemId).Distinct()
                .OrderBy(Order).Select(id => $"{zone}|{id}");
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
            if (r.Zone != ItemInstance.Carried && r.Zone != s.CurrentLocation) return ActionResult.Fail("只能整理随身或当前地面的卡片");
            var list = OrderedCards(s, r.Zone);
            if (!list.Remove(r.CardKey)) return ActionResult.Fail("这张卡已不在该区域");
            var index = r.Index < 0 ? 0 : r.Index > list.Count ? list.Count : r.Index;
            list.Insert(index, r.CardKey);
            s.Layout[r.Zone] = list;
            return ActionResult.Success();
        }
    }
}
