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
            "区域地图", "手工制作", "休息", "行动日志", "编队", "整备", "成长", "生存目标", "生存提示", "对话记录", "系统说明", Menu,
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
        // 底栏「已保存 {时:分}」改由 SaveSlots.SavedPrefix（B40，S01 Q406）

        // PXb 游戏菜单
        public const string GameMenu = "游戏菜单";
        /// <summary>顶栏与死亡结算页的「菜单」键（§2 顶栏第 12 键；B31d Q333 结算页同字）。</summary>
        public const string Menu = "菜单";
        public const string Feedback = "反馈与留言";
        public const string ContinueGame = "继续游戏";
        public const string SaveToTitle = "保存并返回标题";
        public const string SaveNote = "存档保存在当前浏览器。";

        /// <summary>
        /// B31d（逐字稿 §1.2 Q336 选 B）：原标题页底部版本行移到普通游戏菜单种子行下，写法沿原「原型 · … · 存档版本N」，
        /// 中间的版本值取实际构建（发布时写进页面 app-build 的「日期 · 提交短号」，本地为「本地开发版」），N 取当前存档格式。取不到构建时省略中段。
        /// </summary>
        public static string VersionLine(string? build, string schema) =>
            string.IsNullOrWhiteSpace(build) ? $"原型 · 存档版本{schema}" : $"原型 · {build!.Trim()} · 存档版本{schema}";

        // P05g 战斗菜单
        public const string BattleMenu = "战斗菜单";
        public const string Help = "系统说明";
        public const string BackToBattle = "返回战斗";
        /// <summary>B31e（逐字稿 §1.3 Q353 选 A）：死亡结算页打开的游戏菜单底部关闭键；只收起菜单、回到同一结算。</summary>
        public const string BackToSettlement = "返回结算";

        /// <summary>
        /// B31d（逐字稿 §1.2 Q334 选 B）：战斗层菜单的标题与可访问名称——实际战斗中为「战斗菜单」，敌踪尚未接战时为「游戏菜单」。
        /// </summary>
        public static string BattleUiMenuTitle(bool inBattle) => inBattle ? BattleMenu : GameMenu;

        // PXa 标题页（PXm 覆盖确认由 S01 Q335 新游戏确认取代，文字在 SaveSlots）
        public const string NewGame = "新游戏";
        public const string HowToPlay = "玩法说明";
        public const string ReadingFont = "阅读字体";
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

    /// <summary>
    /// B43（S14 2026-11-12「快捷键在对应按钮常驻显示」，需求 R170）：按钮与控件上常驻显示的键帽和顶栏键位。
    /// 顶栏 12 键与 <see cref="ShellText.TopKeys"/> 一一对应、同一顺序；字母不区分大小写，F 不绑定（反馈只从游戏菜单进入）。
    /// 键帽文字、局部控件名都照 S14 同日两张表逐字；网页壳只从这里取，按钮、悬停提示与键表同源。
    /// </summary>
    public static class ShellKeys
    {
        /// <summary>顶栏 12 键的键帽（S14「顶栏12个功能的固定键位与按钮原文」「按键显示」列）。最后一个是菜单的「ESC」，实际绑定 Escape。</summary>
        public static readonly IReadOnlyList<string> TopCaps = new[] { "M", "C", "R", "L", "P", "G", "K", "J", "T", "D", "H", Esc };

        /// <summary>ESC 键帽（S14：ESC 显示为「ESC」，实际绑定仍为 Escape）。</summary>
        public const string Esc = "ESC";
        public const string Enter = "Enter";
        public const string Shift = "Shift";
        /// <summary>数量输入旁的键帽（S14「数量 0–9」）。</summary>
        public const string Digits = "0–9";
        public const string Qty = "数量";
        public const string DragQty = "选数量";

        // 地图（S14「地图放大／缩小」「地图定位」）
        public const string ZoomIn = "放大";
        public const string ZoomOut = "缩小";
        public const string ZoomInCap = "+";
        public const string ZoomOutCap = "-";
        public const string BackToHere = "回到当前位置";
        public const string BackToHereCap = "0";

        // 战斗（S14「战斗武器页」「战斗五个技能位」「战斗目标预测切换」）
        public const string WeaponPage1Cap = "Q";
        public const string WeaponPage2Cap = "E";
        public const string PrevTarget = "上一目标";
        public const string NextTarget = "下一目标";
        public const string PrevTargetCap = "←";
        public const string NextTargetCap = "→";

        /// <summary>顶栏第 i 个按钮的常驻原文「功能名＋键帽」（如「区域地图 M」「菜单 ESC」），也是读屏名称。</summary>
        public static string TopLabel(int i) => $"{ShellText.TopKeys[i]} {TopCaps[i]}";

        /// <summary>键表（游戏菜单与标题页「玩法说明」里的快捷键行）：顶栏 12 个按钮原文按顺序以「 · 」相连，与按钮上的写法同源。</summary>
        public static string TopTable()
        {
            var parts = new string[TopCaps.Count];
            for (var i = 0; i < parts.Length; i++) parts[i] = TopLabel(i);
            return string.Join(" · ", parts);
        }

        /// <summary>
        /// 一个单字母按键对应顶栏第几个按钮（0—10；不区分大小写）；不是顶栏字母时为 -1。菜单（第 12 个）走 Escape 层级，不在这里。
        /// </summary>
        public static int TopIndexOf(string? key)
        {
            if (string.IsNullOrEmpty(key) || key!.Length != 1) return -1;
            var k = key.ToUpperInvariant();
            for (var i = 0; i < TopCaps.Count - 1; i++) if (TopCaps[i] == k) return i;
            return -1;
        }

        /// <summary>战斗技能卡位的固定键帽：本页第 n 个卡位（1—5，对应 1/3/5/7/10 级）。不是该门等级。</summary>
        public static string SkillSlotCap(int level)
        {
            var i = System.Array.IndexOf(SkillSlotLevels, level);
            return i < 0 ? "" : (i + 1).ToString();
        }

        /// <summary>本页五个技能位对应的等级（A10 §6.4，与网页壳 BattleHotkeySkill 同一张表）。</summary>
        public static readonly int[] SkillSlotLevels = { 1, 3, 5, 7, 10 };
    }

    /// <summary>Esc 这一下该做什么（Q322「Esc」「菜单内」「返回」）。</summary>
    public enum ShellEscRoute
    {
        /// <summary>不处理（反馈窗口自己处理 Esc，关掉后回到游戏菜单）。</summary>
        Ignore,
        /// <summary>收起已开的游戏菜单（常态、对白锁、行动推进、暂停、死亡结算）。</summary>
        CloseMenu,
        /// <summary>收起已开的战斗菜单（战斗、敌踪、战斗中的对白）。</summary>
        CloseBattleMenu,
        /// <summary>按原层级收起最上方可关闭的子层（交给原 Esc 栈逐级处理；死亡结算时为结算页的「查看记录」层）。</summary>
        CloseLayer,
        /// <summary>打开游戏菜单（死亡结算时为只开放阅读字体、动画速度与反馈的结算菜单，B31d Q333）。</summary>
        OpenMenu,
        /// <summary>打开战斗菜单（对白与战斗同时存在时按战斗情境，保留对白锁）。</summary>
        OpenBattleMenu,
    }

    /// <summary>Esc 判定所需的界面状态（网页壳每次按键时现填）。</summary>
    public sealed class ShellEscInput
    {
        /// <summary>角色已死亡、结算页接管（B31d Q333：Esc 同结算页「菜单」键）。</summary>
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
    /// B31d（逐字稿 §1.2 Q333 选 A）：死亡结算页上 Esc 与「菜单」键同效，打开游戏菜单（只开放阅读字体、动画速度与反馈），已开时收起、回到同一结算；
    /// 结算页自己的「查看记录」层开着时先收它（同上面“先收子层”）。
    /// </summary>
    public static class ShellEsc
    {
        public static ShellEscRoute Route(ShellEscInput i)
        {
            if (i.FeedbackOpen) return ShellEscRoute.Ignore;
            if (i.Dead) return i.MenuOpen ? ShellEscRoute.CloseMenu : i.HasClosableLayer ? ShellEscRoute.CloseLayer : ShellEscRoute.OpenMenu;
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
    /// B31d（Q333 选 A）：死亡结算页打开的菜单同样只开放这几项（关闭回到同一结算）：调用方把 dialogueLocked 传「对白锁或已死亡」。
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
