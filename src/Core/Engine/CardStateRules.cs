using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Survival.Core.Config;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>
    /// 开发计划 v1.0 B32a：主界面卡片的状态挂载与卡面口径（逐字稿 §1.2 Q340、Q345、Q349、Q351；v0.9 06 §1—§4）。
    /// 只读状态与配置，不改任何玩法；网页壳按这里的判定给卡挂「危险」「加工中」「不可用」叠层、写副标与来源行。
    /// </summary>
    public static partial class CardStates
    {
        // ───────────── Q345 加工中：当前行动作用中的对象 ─────────────

        /// <summary>主行动正在采集/搜集这个资源点。</summary>
        public static bool BusyResource(GameState s, string nodeId) =>
            s.MainWorks().Any(a => a.Type == ActionType.Gather && a.TargetId == nodeId);   // B41b Q472：暂存的原采集仍是在办

        /// <summary>主行动正在当前地点探索、清理或遗迹勘整（作用于地点大图卡）。</summary>
        public static bool BusyPlace(GameState s, string locationId) =>
            s.Main is ActionState a && a.Type is ActionType.Explore or ActionType.Clear or ActionType.Survey
            && (a.TargetId == locationId || (a.Type == ActionType.Survey && a.SourceLocation == locationId));

        /// <summary>主行动正在前往这个相邻地点（路线卡）。</summary>
        public static bool BusyRoute(GameState s, string locationId) =>
            s.Main is ActionState a && a.Type == ActionType.Travel && a.TargetId == locationId;

        /// <summary>主行动正在建造这个设施配方（遮蔽棚、床垫等设施卡）。</summary>
        public static bool BusyBuild(GameState s, string recipeId) =>
            s.MainWorks().Any(a => a.Type is ActionType.Build or ActionType.Craft && a.TargetId == recipeId);

        // ───────────── Q345 危险 ─────────────

        /// <summary>耐久为 0 的工具（有耐久上限且当前耐久 ≤0）。</summary>
        public static bool BrokenTool(ItemInstance i) => i.MaxDurability.HasValue && i.Durability.HasValue && i.Durability.Value <= 0;

        // ───────────── B53a（S06／S14 2026-11-15 B50a §Q766）腐败物普通卡 ─────────────

        /// <summary>腐败物卡第二行（逐字稿 B50a rotWeight「{重量}公斤」）：这张卡全部腐败物的真实总重；不再写「已腐败」，也不挂危险叠层。</summary>
        public static string RotWeightLine(GameEngine e, GameState s, IEnumerable<ItemInstance> rot) =>
            Fmt.Kg(rot.Sum(i => (long)e.ItemGrams(s, i)));

        // ───────────── Q356 物品卡副标与第二行 ─────────────

        /// <summary>Q356：常规物品卡与独立工具卡副标固定为本卡实际数量；Q361：本卡实际预留数大于 0（部分或全部预留）写「×{总数}（预留{数量}）」。</summary>
        public static string ItemQtyText(int total, int reserved) => reserved > 0 ? $"×{total}（预留{reserved}）" : $"×{total}";

        /// <summary>
        /// B54（S08／S06／S14 Q790，逐字稿 cardActionTotal／cardActionReservedTotal）：物品操作窗标题副行。总数为该窗当前选中卡片／组同一真实归属与卡侧的全部实物（含预留），
        /// 真实行动预留数大于 0 时「{来源} · ×{总数}（预留{预留数}）」，否则「{来源} · ×{总数}」。只是显示，数量上限与按钮仍按实际可用数。
        /// </summary>
        public static string CardActionTitleText(string from, int total, int reserved) => $"{from} · {ItemQtyText(total, reserved)}";

        /// <summary>Q790：这一组实物的（总数, 真实行动预留数）——预留只数 ReservedBy 标记的份额，部署、锁定等其他占用不算预留。</summary>
        public static (int total, int reserved) TotalAndReserved(IEnumerable<ItemInstance> items)
        {
            var total = 0;
            var reserved = 0;
            foreach (var i in items)
            {
                total += i.Count;
                if (i.ReservedBy != null) reserved += i.Count;
            }
            return (total, reserved);
        }

        /// <summary>Q356：物品卡第二行取哪一项——明确状态优先（已腐败、已损坏），其次实际保质/到期，再耐久；都没有不出第二行。</summary>
        public static ItemLine2 ItemLine2Of(GameEngine e, GameState s, ItemInstance i) =>
            e.IsSpoiled(s, i) ? ItemLine2.Spoiled
            : BrokenTool(i) ? ItemLine2.Broken
            : i.ExpiresAt.HasValue ? ItemLine2.Expiry
            : i.MaxDurability.HasValue && i.Durability.HasValue ? ItemLine2.Durability
            : ItemLine2.None;

        /// <summary>
        /// B51c1（S08／R02 B51 Q772）：被本单锁定、按本单用量确定完成后损坏的同一工具实例，卡片第二行与详情状态写「本次生产结束后损坏」；否则 null（沿原第二行）。
        /// </summary>
        public static string? ToolOrderState(GameEngine e, GameState s, ItemInstance i) =>
            e.ToolBreaksAfterOrder(s, i) ? GameEngine.ToolBreaksAfterOrderText : null;

        /// <summary>
        /// B51c1（S06／S14 B51 Q773 选 B）：卡内有期限、未腐败的份里只有部分受生产／晾制保护时，副标只写「含保质暂停」（不接到期、数量）；
        /// 全部受保护时同 R 写「保质暂停，剩余{时长}」、R 不同写「保质暂停 · {批数}批」（B51a Q724）；没有受保护份返回 null，沿原副标。
        /// </summary>
        public static string? PausedCardLine(GameEngine e, GameState s, IEnumerable<ItemInstance> list)
        {
            var live = list.Where(i => (i.ExpiresAt.HasValue || i.ShelfPausedMinutes.HasValue) && !e.IsSpoiled(s, i)).ToList();
            if (live.Count == 0 || live.All(i => i.ShelfPausedMinutes == null)) return null;
            if (live.Any(i => i.ShelfPausedMinutes == null)) return GameEngine.ShelfPartlyPausedText;
            var rs = live.Select(i => i.ShelfPausedMinutes!.Value).Distinct().ToList();
            return rs.Count == 1 ? GameEngine.ShelfPausedText(rs[0]) : GameEngine.ShelfPausedBatchesText(live.Select(i => (i.ExpiresAt, i.ShelfPausedMinutes)).Distinct().Count());
        }

        // ───────────── Q351 资源来源 ─────────────

        /// <summary>Q351：右栏资源详情「来源」行的值「{资源所在地点名} · {资源账短标}」。</summary>
        public static string ResourceSource(GameEngine e, ResourceConfig r) => $"{e.LocName(r.LocationId)} · {GameEngine.AccountLabel(r)}";
    }

    /// <summary>Q356 物品卡第二行的内容类别。</summary>
    public enum ItemLine2 { None, Spoiled, Broken, Expiry, Durability }

    public sealed partial class GameEngine
    {
        private Regex? _instanceTag;

        /// <summary>
        /// B32a（逐字稿 §1.2 Q349）：记录回看遇到带物品内部号的旧显示时隐藏该字段，保存的原记录不改。
        /// 只去掉紧跟在配置物品名（可带「（…）」限定与「×份数」）后面的「#数字」，例「打火器#12」→「打火器」、「木材×3#7」→「木材×3」；
        /// 人物代号等不是物品名后面的「#」不动。
        /// </summary>
        public string HideInstanceTags(string? text)
        {
            if (string.IsNullOrEmpty(text) || text!.IndexOf('#') < 0) return text ?? "";
            // 旧投喂结果句「取自你的随身锅#5」的锅号跟在「随身锅」后面（不出现锅的物品名），一并当作前缀
            _instanceTag ??= new Regex("(?<=(?:" + string.Join("|", Config.Items.Select(i => i.Name).Where(n => !string.IsNullOrEmpty(n)).Append("随身锅").Distinct()
                .OrderByDescending(n => n.Length).Select(Regex.Escape)) + @")(?:（[^）#]*）)?(?:×\d+)?)#\d+");
            return _instanceTag.Replace(text, "");
        }
    }
}
