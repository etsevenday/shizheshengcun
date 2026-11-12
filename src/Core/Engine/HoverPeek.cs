using System;
using System.Collections.Generic;
using System.Linq;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>
    /// B42（S14「2026-11-12 悬停卡片／条目立即显示右栏详情」，HOVER-A01—A08）：悬停查看的右栏覆盖状态。
    /// 鼠标进入对象热区即记下该对象的身份键（不是列表序号）与所在界面上下文；移到空白处不清（跨空白保留最后一项），
    /// 进入下一个对象立即替换；上下文变了（一级面板、成员、场景、读档等）或对象失效时由 <see cref="Reconcile"/> 清掉。
    /// 只是界面显示状态：不改真正选中、批次、草稿、任务、时间或随机，不进存档。
    /// </summary>
    public sealed class HoverPeekState
    {
        /// <summary>当前悬停覆盖的对象键；null 为没有覆盖（右栏按原页签与点击选择显示）。</summary>
        public string? Key { get; private set; }

        /// <summary>记下该对象时的界面上下文签名（<see cref="HoverPeekContext.Of"/>）。</summary>
        public string? Context { get; private set; }

        /// <summary>
        /// 指针或键盘焦点进入一个真实对象热区。空键（空白处、右栏正文、普通按钮）不清，保留最后一项（HOVER-A03）。
        /// 返回 true 表示显示的对象变了。
        /// </summary>
        public bool Enter(string? key, string context)
        {
            if (string.IsNullOrEmpty(key)) return false;
            if (Key == key && Context == context) return false;
            Key = key;
            Context = context;
            return true;
        }

        /// <summary>结束悬停覆盖（点右栏页签／返回控件、明确点选、上下文退出）。返回 true 表示原来有覆盖。</summary>
        public bool Clear()
        {
            if (Key == null) return false;
            Key = null;
            Context = null;
            return true;
        }

        /// <summary>
        /// 每次刷新界面前校验：上下文签名不同（打开／关闭一级面板、切换成员、切换场景、读档、新局、回标题），
        /// 或对象已被移除、隐藏、失效时立即清掉，不按相同位置改指另一件（HOVER-A05）。返回 true 表示清掉了。
        /// </summary>
        public bool Reconcile(string context, Func<string, bool> stillValid)
        {
            if (Key == null) return false;
            if (Context == context && stillValid(Key)) return false;
            return Clear();
        }
    }

    /// <summary>B42：悬停查看的界面上下文签名。任一段不同即视为换了界面上下文，旧悬停覆盖作废。</summary>
    public static class HoverPeekContext
    {
        /// <param name="epoch">读档、新局、回标题时递增的会话序号。</param>
        /// <param name="panel">当前一级面板（null 为主桌面）。</param>
        /// <param name="member">面板内当前成员（整备、成长）；其他面板传 null。</param>
        /// <param name="location">真实当前地点。</param>
        /// <param name="zone">当前区（外部／安全屋屋内）。</param>
        /// <param name="scene">战斗、敌踪等场景标记（""为常态）。</param>
        /// <param name="page">面板内的页（地图层、制作分类等）；没有时传 null。</param>
        public static string Of(int epoch, string? panel, string? member, string location, string zone, string scene, string? page) =>
            string.Join("\u001f", new[] { epoch.ToString(), panel ?? "", member ?? "", location, zone, scene, page ?? "" });
    }

    /// <summary>
    /// B42：悬停热区的对象键（页面元素 data-peek 的值）。形如「种类|段1|段2…」，每段按 URL 编码，避免批次键等自带的「|」。
    /// 键只用对象的真实身份（实例号、物品与归属、地点、配方、节点等），不用列表序号。
    /// </summary>
    public static class PeekKey
    {
        /// <summary>沿用主界面点击选择的对象（种类、ID、归属、实例、可选批次）。</summary>
        public const string Sel = "sel";
        /// <summary>整备页来源卡或已穿戴装备（实例号）。</summary>
        public const string Gear = "gear";
        /// <summary>区域地图已公开节点（地点 ID）。</summary>
        public const string Node = "node";
        /// <summary>手工制作配方或自动加工说明（配方 ID，或带 proc: 前缀的加工 ID）。</summary>
        public const string Recipe = "recipe";
        /// <summary>成长页熟练度门类（技能 ID）。</summary>
        public const string Skill = "skill";
        /// <summary>左栏已有解释的生存状态项（状态 ID）。</summary>
        public const string Status = "status";
        /// <summary>没有独立详情组件的公开条目：由卡片或条目自己登记原公开标题、说明与状态。</summary>
        public const string Card = "card";
        /// <summary>全屏战斗敌方锚点（本场序号）。</summary>
        public const string Foe = "foe";
        /// <summary>全屏战斗我方锚点（成员 ID）。</summary>
        public const string Ally = "ally";
        /// <summary>BM1（Q483）：右栏「人物」页已穿戴装备槽行（实例号）；显示与整备同一只读事实（Q462）。</summary>
        public const string Worn = "worn";
        /// <summary>BM1（Q483）：右栏「人物」页队伍里的队员行（成员 ID）；显示该队员自己的公开事实。</summary>
        public const string Member = "member";

        public static string Make(string kind, params string?[] parts) =>
            parts.Length == 0 ? kind : kind + "|" + string.Join("|", parts.Select(p => Uri.EscapeDataString(p ?? "")));

        /// <summary>拆出种类与各段（已解码）；空键返回 false。</summary>
        public static bool TryParse(string? key, out string kind, out string[] parts)
        {
            kind = "";
            parts = Array.Empty<string>();
            if (string.IsNullOrEmpty(key)) return false;
            var all = key!.Split('|');
            kind = all[0];
            parts = all.Skip(1).Select(Uri.UnescapeDataString).ToArray();
            return kind.Length > 0;
        }

        /// <summary>键的种类（解析失败为空串）。</summary>
        public static string KindOf(string? key) => TryParse(key, out var k, out _) ? k : "";

        /// <summary>第 i 段（不存在或为空串时为 null）。</summary>
        public static string? Part(string? key, int i) =>
            TryParse(key, out _, out var p) && i < p.Length && p[i].Length > 0 ? p[i] : null;
    }

    /// <summary>B42 悬停的物品对象（与点击选择同一身份：种类 ItemGroup／Tool、物品、归属、实例、可选批次）。</summary>
    public readonly struct PeekItemRef
    {
        public PeekItemRef(string kind, string id, string owner, int? inst, string? batch)
        {
            Kind = kind; Id = id; Owner = owner; Inst = inst; Batch = batch;
        }

        public string Kind { get; }
        public string Id { get; }
        public string Owner { get; }
        public int? Inst { get; }
        public string? Batch { get; }
    }

    /// <summary>
    /// B42 试玩修复（BUG-1，S14「数量、归属变化时以同一真实引用更新」「对象被移除、隐藏、失效时立即清」）：
    /// 悬停的物品被挪走（放下、拾起、放进火堆加工位、取出）后，按同一批真实实例跟到新归属；实例不在了或不在随身／当前地点就清。
    /// 跟随后的身份与点击选中随状态校正（GameSession.ReconcileSelection）同一口径：可堆叠且不在设施／锅里按「物品+归属」，否则按实例。
    /// </summary>
    public static class PeekItems
    {
        public const string Group = "ItemGroup";
        public const string Tool = "Tool";

        private static bool Visible(GameEngine e, GameState s, ItemInstance i)
        {
            var place = e.PlaceOf(s, i);
            return place == ItemInstance.Carried || place == s.Ground;
        }

        private static IEnumerable<ItemInstance> At(GameState s, PeekItemRef r) =>
            s.Items.Where(i => i.Owner == r.Owner && i.ItemId == r.Id && (r.Inst == null || i.InstanceId == r.Inst)
                               && (r.Kind != Group || r.Batch == null || GameEngine.BatchKeyOf(i) == r.Batch));

        /// <summary>右栏物品详情此刻能按这个身份显示（与物品详情的判断同源：原归属里还有、根位置在随身或当前地点、所指批次还在）。</summary>
        public static bool Shown(GameEngine e, GameState s, PeekItemRef r)
        {
            var first = At(s, r).FirstOrDefault();
            return first != null && Visible(e, s, first);
        }

        /// <summary>这个身份此刻代表的真实实例号（悬停有效时记下，挪走后据此跟随）。</summary>
        public static List<int> Members(GameState s, PeekItemRef r) => At(s, r).Select(i => i.InstanceId).OrderBy(x => x).ToList();

        /// <summary>原身份不能再显示时，按记下的实例找新归属；都不在或都不可见返回 null（清悬停，不改指别的物品）。</summary>
        public static PeekItemRef? Follow(GameEngine e, GameState s, PeekItemRef r, IEnumerable<int> members)
        {
            foreach (var id in members.OrderBy(x => x))
            {
                var it = e.FindItem(s, id);
                if (it == null || !Visible(e, s, it)) continue;
                var group = e.Item(it.ItemId).IsStackable && !it.InFacility && !it.InContainer;
                var next = group
                    ? new PeekItemRef(Group, it.ItemId, it.Owner, null, r.Batch != null ? GameEngine.BatchKeyOf(it) : null)
                    : new PeekItemRef(Tool, it.ItemId, it.Owner, it.InstanceId, null);
                if (Shown(e, s, next)) return next;
            }
            return null;
        }
    }
}
