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
            s.Main is ActionState a && a.Type == ActionType.Gather && a.TargetId == nodeId;

        /// <summary>主行动正在当前地点探索、清理或遗迹勘整（作用于地点大图卡）。</summary>
        public static bool BusyPlace(GameState s, string locationId) =>
            s.Main is ActionState a && a.Type is ActionType.Explore or ActionType.Clear or ActionType.Survey
            && (a.TargetId == locationId || (a.Type == ActionType.Survey && a.SourceLocation == locationId));

        /// <summary>主行动正在前往这个相邻地点（路线卡）。</summary>
        public static bool BusyRoute(GameState s, string locationId) =>
            s.Main is ActionState a && a.Type == ActionType.Travel && a.TargetId == locationId;

        /// <summary>主行动正在建造这个设施配方（遮蔽棚、床垫等设施卡）。</summary>
        public static bool BusyBuild(GameState s, string recipeId) =>
            s.Main is ActionState a && a.Type is ActionType.Build or ActionType.Craft && a.TargetId == recipeId;

        // ───────────── Q345 危险 ─────────────

        /// <summary>耐久为 0 的工具（有耐久上限且当前耐久 ≤0）。</summary>
        public static bool BrokenTool(ItemInstance i) => i.MaxDurability.HasValue && i.Durability.HasValue && i.Durability.Value <= 0;

        // ───────────── Q356 物品卡副标与第二行 ─────────────

        /// <summary>Q356：常规物品卡与独立工具卡副标固定为本卡实际数量；部分预留沿「×{总数}（预留{数量}）」。</summary>
        public static string ItemQtyText(int total, int reserved) => reserved > 0 && reserved < total ? $"×{total}（预留{reserved}）" : $"×{total}";

        /// <summary>Q356：物品卡第二行取哪一项——明确状态优先（已腐败、已损坏），其次实际保质/到期，再耐久；都没有不出第二行。</summary>
        public static ItemLine2 ItemLine2Of(GameEngine e, GameState s, ItemInstance i) =>
            e.IsSpoiled(s, i) ? ItemLine2.Spoiled
            : BrokenTool(i) ? ItemLine2.Broken
            : i.ExpiresAt.HasValue ? ItemLine2.Expiry
            : i.MaxDurability.HasValue && i.Durability.HasValue ? ItemLine2.Durability
            : ItemLine2.None;

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
