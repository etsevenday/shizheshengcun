using System.Collections.Generic;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>
    /// 重墨 V2 外壳（开发计划 v1.0 B31b；《重墨 V2 开工裁定与界面逐字稿》§1.1 Q322、§2 通用位置表、PXa、PXb、P05g）的正式界面文字。
    /// 网页壳只从这里取外壳固定句，自动测试逐条对照逐字稿。
    /// </summary>
    public static class ShellText
    {
        /// <summary>§2「顶栏从左到右」12 个按钮（Q292；不含反馈，Q322）。</summary>
        public static readonly IReadOnlyList<string> TopKeys = new[]
        {
            "区域地图", "手工制作", "休息", "行动日志", "编队", "整备", "成长", "生存目标", "生存提示", "对话记录", "系统说明", "菜单",
        };

        public const string Brand = "适者生存";
        public const string LeftTitle = "生存状况";
        public const string ObjectTab = "对象";
        public const string CharacterTab = "人物";
        public const string ObjectTitle = "对象详情";
        public const string ObjectEmptyHint = "选择对象查看详情。";
        public const string PlaceTitle = "当前位置";
        public const string BodyTitle = "身体状态";
        public const string Weight = "负重";
        public const string Carried = "随身物品";
        public const string Saved = "已保存";

        // PXb 游戏菜单
        public const string GameMenu = "游戏菜单";
        public const string Feedback = "反馈与留言";
        public const string ContinueGame = "继续游戏";
        public const string SaveToTitle = "保存并返回标题";
        public const string SaveNote = "存档保存在当前浏览器。";

        // P05g 战斗菜单
        public const string BattleMenu = "战斗菜单";
        public const string Help = "系统说明";
        public const string BackToBattle = "返回战斗";

        // PXa 标题页、PXm 覆盖确认
        public const string NewGame = "新游戏";
        public const string HowToPlay = "玩法说明";
        public const string ReadingFont = "阅读字体";
        public const string OverwriteNote = "已有存档。开始新游戏会覆盖它，无法恢复。";
        public const string OverwriteConfirm = "覆盖并开始";
        public const string Back = "返回";

        // PXn 行动进行中、PXe 行动暂停中（Q320）
        public const string ActionRunning = "行动进行中";
        public const string ActionPaused = "行动暂停中";
        public const string PauseReason = "暂停原因";
        public const string ResumeAction = "继续行动";
        public const string ExitAction = "退出行动";

        /// <summary>左栏属性名（§2「左栏」逐字：清醒度在左栏写「清醒」；其余与 <see cref="GameEngine.StatName"/> 相同）。</summary>
        public static string LeftStatName(StatId id) => id == StatId.Wakefulness ? "清醒" : GameEngine.StatName(id);

        /// <summary>
        /// 中央行动位的进度两行（Q320）：固定时长「已推进{已耗时} / {计划时长}」与「剩余{时长}」；工作量型与等待烹饪「已推进{已耗时}」与
        /// 「预计剩余{时长}」，算不出剩余时剩余行让位给实际原因（燃料不足等 StopNote）。时长用现有格式 <see cref="Clock.Duration"/>。
        /// </summary>
        public static (string Advanced, string? Remain) ProgressLines(ActionProgressInfo p)
        {
            if (p.PlannedMinutes is int planned)
                return ($"已推进{Clock.Duration(p.ElapsedMinutes)} / {Clock.Duration(planned)}",
                        p.RemainingMinutes is int r0 ? $"剩余{Clock.Duration(r0)}" : null);
            var advanced = $"已推进{Clock.Duration(p.ElapsedMinutes)}";
            if (p.RemainingMinutes is int r) return (advanced, p.Estimated ? $"预计剩余{Clock.Duration(r)}" : $"剩余{Clock.Duration(r)}");
            return (advanced, string.IsNullOrEmpty(p.StopNote) ? null : p.StopNote);
        }
    }

    /// <summary>Esc 这一下该做什么（Q322「Esc」「菜单内」「返回」）。</summary>
    public enum ShellEscRoute
    {
        /// <summary>不处理（已死亡由结算层接管；反馈窗口自己处理 Esc，关掉后回到游戏菜单）。</summary>
        Ignore,
        /// <summary>收起已开的游戏菜单（常态、对白锁、行动推进、暂停）。</summary>
        CloseMenu,
        /// <summary>收起已开的战斗菜单（战斗、敌踪、战斗中的对白）。</summary>
        CloseBattleMenu,
        /// <summary>按原层级收起最上方可关闭的子层（交给原 Esc 栈逐级处理）。</summary>
        CloseLayer,
        /// <summary>打开游戏菜单。</summary>
        OpenMenu,
        /// <summary>打开战斗菜单（对白与战斗同时存在时按战斗情境，保留对白锁）。</summary>
        OpenBattleMenu,
    }

    /// <summary>Esc 判定所需的界面状态（网页壳每次按键时现填）。</summary>
    public sealed class ShellEscInput
    {
        public bool Dead { get; set; }
        /// <summary>反馈与留言窗口开着。</summary>
        public bool FeedbackOpen { get; set; }
        /// <summary>有未读完的强制对白（Q10 输入锁）：对白本身不可由 Esc 结束，锁住的背后子层也不收。</summary>
        public bool DialogueLocked { get; set; }
        /// <summary>战斗或敌踪选择中。</summary>
        public bool BattleUi { get; set; }
        public bool MenuOpen { get; set; }
        public bool BattleMenuOpen { get; set; }
        /// <summary>原 Esc 栈里还有看得见、可关闭的子层（数量框、浮层、面板、二级页、选中等；不含看不见的残留状态）。</summary>
        public bool HasClosableLayer { get; set; }
    }

    /// <summary>
    /// B31b Esc 分层（逐字稿 §1.1 Q322）：先收当前最上方可关闭的子层（已开的菜单本身也是一层）；没有可关闭子层时打开当前情境的游戏菜单——
    /// 战斗／敌踪、强制对白、行动推进和暂停均可。对白与战斗同时存在时按战斗情境。打开或关闭菜单不提交、确认、取消或恢复世界行动。
    /// </summary>
    public static class ShellEsc
    {
        public static ShellEscRoute Route(ShellEscInput i)
        {
            if (i.Dead || i.FeedbackOpen) return ShellEscRoute.Ignore;
            if (i.MenuOpen) return ShellEscRoute.CloseMenu;
            if (i.BattleMenuOpen) return ShellEscRoute.CloseBattleMenu;
            if (!i.DialogueLocked && i.HasClosableLayer) return ShellEscRoute.CloseLayer;
            return i.BattleUi ? ShellEscRoute.OpenBattleMenu : ShellEscRoute.OpenMenu;
        }

        /// <summary>
        /// 战斗／敌踪里「选中」算不算看得见的子层（修 B31a 线上遗留「Esc 有时要按两次」）：全屏战斗不显示外层选中；
        /// 敌踪时①②被敌踪层替换，只有③④物品卡上的选中仍看得见。看不见的残留选中由后续操作静默清理，不吃掉一次 Esc。
        /// </summary>
        public static bool BattleSelectionVisible(bool fullscreenBattle, bool selectionOnShelves) => !fullscreenBattle && selectionOnShelves;
    }

    /// <summary>游戏菜单里的入口（PXb、P05g）。</summary>
    public enum ShellMenuItem { Continue, ReadingFont, AnimationSpeed, Feedback, SaveToTitle, Restart, AbortExpedition, Help, Dev }

    /// <summary>
    /// 菜单入口在当前情境是否可用（Q322「菜单内」）：关闭菜单、阅读／动画偏好与反馈任何时候可用；强制对白锁期间其余入口
    /// （世界操作、存档退出、重开、开发入口、战斗内说明）仍禁用；其余入口再按各自原有条件判断（由调用方叠加）。
    /// </summary>
    public static class ShellMenu
    {
        public static bool AlwaysOpen(ShellMenuItem item) =>
            item == ShellMenuItem.Continue || item == ShellMenuItem.ReadingFont || item == ShellMenuItem.AnimationSpeed || item == ShellMenuItem.Feedback;

        public static bool Enabled(ShellMenuItem item, bool dialogueLocked) => AlwaysOpen(item) || !dialogueLocked;
    }

    public sealed partial class GameEngine
    {
        /// <summary>
        /// Q328 右栏「对象」页空态的「当前位置」摘要（逐字稿 §1.1 Q328）：按顺序读当前实际地点（不跟随选中的地图节点或未抵达的目的地），
        /// 字段缺失时省略该行。区域行只在安全屋屋内或已建安全屋的外部区出现；遮蔽沿 <see cref="IsSheltered"/>；探索度沿地点卡已公开的数值。
        /// </summary>
        public List<string> CurrentPlaceSummary(GameState s)
        {
            var lines = new List<string>(4);
            var loc = s.CurrentLocation;
            if (string.IsNullOrEmpty(loc) || !_locs.ContainsKey(loc)) return lines;
            lines.Add($"地点：{LocName(loc)}");
            if (s.CurrentZone == GameState.SafehouseZone) lines.Add($"区域：屋内 · {SafehouseName(s, loc)}");
            else if (SafehouseBuilt(s, loc)) lines.Add("区域：屋外");
            lines.Add(IsSheltered(s, s.Ground) ? "遮蔽：有遮蔽" : "遮蔽：露天");
            if (s.Locations.TryGetValue(loc, out var ls)) lines.Add($"探索度：{ls.Progress}%");
            return lines;
        }
    }
}
