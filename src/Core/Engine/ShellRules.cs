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
        public const string WeaponPage1 = "武器Ⅰ";
        public const string WeaponPage2 = "武器Ⅱ";
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

        // BM2（Q502）：中央行动进度条「跳过动画 空格」——只绑空格，不加中央 Enter；悬停沿原句
        public const string SkipAnim = "跳过动画";
        public const string SpaceCap = "空格";
        public const string SkipAnimTitle = "只跳过表现，不改变游戏时间与结果";
        /// <summary>B34b（S14 键表「战斗已有／已批准动画跳过控件」、逐字稿 B34 Q571／Q572 节）：战斗特写两级跳过的业务名与键帽。</summary>
        public const string SkipToResult = "跳到结果";
        public const string SkipRest = "跳过余下";
        public const string EnterSpaceCap = "Enter / Space";

        /// <summary>BM2（Q496）：顶栏、战斗区、结算页「菜单 ESC」按钮的悬停正式句。</summary>
        public const string MenuHover = "点击打开或关闭菜单。按 ESC 时先处理当前可关闭层；没有可关闭层时打开菜单。";
    }

    /// <summary>系统说明「键盘快捷键」里的一行：操作标签／键位与配套说明（顶栏行没有说明）。</summary>
    public sealed class KeyHelpRow
    {
        public KeyHelpRow(string label, string? note) { Label = label; Note = note; }
        public string Label { get; }
        public string? Note { get; }
    }

    /// <summary>系统说明「键盘快捷键」的一个区（顶栏、局部操作、战斗、动画跳过）。</summary>
    public sealed class KeyHelpGroup
    {
        public KeyHelpGroup(string title, IReadOnlyList<KeyHelpRow> rows) { Title = title; Rows = rows; }
        public string Title { get; }
        public IReadOnlyList<KeyHelpRow> Rows { get; }
    }

    /// <summary>
    /// BM2（Q495、Q501；S14「B43 Q495—Q502」、重墨 V2 逐字稿同日节）：系统说明「入门与操作」里的「键盘快捷键」条目与「ESC 与界面层级」六段。
    /// 键表标签由 <see cref="ShellKeys"/> 的按钮原文与键帽拼出，与按钮、真实绑定同源；未交付的战斗两级跳过不列。
    /// </summary>
    public static class ShellKeyHelp
    {
        public const string Title = "键盘快捷键";
        public const string Lead = "这里列出已可用的键盘操作。按键与对应按钮执行相同操作；按钮不可用时，快捷键也不可用。输入文字或打开菜单、对白、确认窗口时，按键优先作用于当前界面。";

        /// <summary>表旁三句逐字补充。</summary>
        public static readonly IReadOnlyList<string> Notes = new[]
        {
            "字母键不区分大小写。",
            "多个并列选项不会由 Enter 自动代选；可用 Tab 选择控件后再激活。",
            "仅显示“×”的关闭键和手动存档页的“返回”按钮不标 ESC，仍可按 ESC 按界面层级返回。",
        };

        /// <summary>四个区：顶栏 12 项（与 <see cref="ShellKeys.TopLabel"/> 同源）、局部操作、战斗、动画跳过（当前只有中央「跳过动画 空格」）。</summary>
        public static IReadOnlyList<KeyHelpGroup> Groups()
        {
            var top = new List<KeyHelpRow>();
            for (var i = 0; i < ShellKeys.TopCaps.Count; i++) top.Add(new KeyHelpRow(ShellKeys.TopLabel(i), null));
            return new[]
            {
                new KeyHelpGroup("顶栏", top),
                new KeyHelpGroup("局部操作", new[]
                {
                    new KeyHelpRow($"返回／取消／菜单 {ShellKeys.Esc}", "按 ESC 先处理当前可关闭层；没有可关闭层时打开菜单。"),
                    new KeyHelpRow($"当前确认 {ShellKeys.Enter}", "仅当前界面有唯一可用主确认时执行；多个并列选项不代选。"),
                    new KeyHelpRow($"继续／结束对话 {ShellKeys.Enter}", "只推进当前一句对白；有分支时先明确选择。"),
                    new KeyHelpRow($"{ShellKeys.ZoomIn} {ShellKeys.ZoomInCap}／{ShellKeys.ZoomOut} {ShellKeys.ZoomOutCap}／{ShellKeys.BackToHere} {ShellKeys.BackToHereCap}", "只操作当前区域地图，不移动队伍。"),
                    new KeyHelpRow($"{ShellKeys.Qty} {ShellKeys.Digits}", "进入或输入物品数量；数量仍须符合实际范围。"),
                    new KeyHelpRow($"{ShellKeys.DragQty} {ShellKeys.Shift}", "按住 Shift 松手可选数量。"),
                }),
                new KeyHelpGroup("战斗", new[]
                {
                    new KeyHelpRow($"{ShellKeys.WeaponPage1} {ShellKeys.WeaponPage1Cap}／{ShellKeys.WeaponPage2} {ShellKeys.WeaponPage2Cap}", "切换武器页，不提交攻击。"),
                    new KeyHelpRow($"技能卡位 1–{ShellKeys.SkillSlotLevels.Length}", "数字对应当前武器页的五个技能卡位，不是所需等级。"),
                    new KeyHelpRow($"{ShellKeys.PrevTarget} {ShellKeys.PrevTargetCap}／{ShellKeys.NextTarget} {ShellKeys.NextTargetCap}", "只切换合法目标的预测，不执行指令。"),
                    new KeyHelpRow($"{ShellAim.ConfirmLabel(ShellAim.HelpCommand)} {ShellKeys.Enter}", "提交当前指令与已指定的合法目标。"),   // BM3（Q509）：帮助表固定泛称，不显示花括号
                }),
                new KeyHelpGroup("动画跳过", new[]
                {
                    new KeyHelpRow($"{ShellKeys.SkipAnim} {ShellKeys.SpaceCap}", "跳过中央行动的表现等待，实际行动仍按原规则逐段结算。"),
                }),
            };
        }

        /// <summary>Q501：条目标题由「Esc 与界面层级」改为「ESC 与界面层级」。</summary>
        public const string EscTitle = "ESC 与界面层级";

        /// <summary>Q501：「ESC 与界面层级」六段定稿（逐字替换原两段）；BM4a（Q590）在第一段后加入顶栏覆盖与逐层返回段落，共七段。</summary>
        public static readonly IReadOnlyList<string> EscParagraphs = new[]
        {
            "按 ESC 时，先返回或关闭当前最上层可关闭的界面；没有可关闭层时，打开当前场景的菜单。一次只处理一层。",
            // BM4a（Q590；逐字稿同日节）：加入顶栏覆盖与逐层返回段落，其余原段保持
            PanelReturnChain.EscHelpParagraph,
            "游戏菜单的手动存档页按 ESC 返回菜单首页；在菜单首页再按 ESC，回到原画面。覆盖确认打开时，先取消覆盖确认，不会保存或覆盖存档。",
            "ESC 不会退出正在进行或暂停的行动，也不会替你继续行动。允许退出的行动，请使用中央行动栏的“退出行动”；开工为5分钟的行动不可中途退出。强制整理等必须完成的步骤不能用 ESC 跳过。",
            "战斗中，ESC 先收起当前可关闭的说明、物资页、医疗盘或取消尚未提交的指令；没有这些界面时打开战斗菜单。选择“返回战斗”或再次按 ESC 只返回战场，不能脱战。菜单内的存档、系统说明和反馈按各自可用条件开放。",
            "对白期间，ESC 可以打开菜单，但不会结束对白或确认选项；关闭菜单后回到原对白。结算页按 ESC 打开或关闭结算菜单，查看记录等子页先按自身层级返回。",
            "反馈与留言窗口打开时，ESC 由窗口处理；关闭后回到菜单。",
        };
    }

    /// <summary>
    /// BM2（Q497；S14「Q497 瞄准预测区的明确提交」、R03 同日节、逐字稿 Q497）：战斗目标预测区「执行{指令名} Enter」按钮的文字与禁用原因顺序。
    /// 按钮、Enter 与原「点亮起的合法目标」共用同一当前预览与正式提交校验；这里只定原因顺序与原句，校验本身沿内核 CommandBlocked／PreviewCommand。
    /// </summary>
    public static class ShellAim
    {
        /// <summary>按钮业务名「执行{指令名}」（指令名取本次所选指令的公开业务名，与预测标题同一指令）；键帽 Enter 在同一按钮里。</summary>
        public static string ConfirmLabel(string command) => $"执行{command}";

        /// <summary>BM3（Q509；S14「Q509 帮助行与真实按钮」、逐字稿同日表）：系统说明键表「战斗」区第四行的固定泛称——「执行（当前指令） Enter」；真实战斗按钮仍取所选指令名。</summary>
        public const string HelpCommand = "（当前指令）";

        public const string NeedTarget = "请先指定目标。";
        public const string NoTargets = "没有可用目标。";
        public const string TargetChanged = "目标已变化，请重新选择。";
        /// <summary>指向从未合法的候选且内核没有更具体句时，沿该敌人锚点上原有的说明（战斗区不可选目标的原句）。</summary>
        public const string OutOfScope = "不在本指令范围内";

        /// <summary>目标预测区的指引（取代「点击亮起的目标即执行；右键/Esc 取消」等旧句）。</summary>
        public static string Guide(string command) => $"指定目标后，可点击亮起的目标或“执行{command}”按钮，也可按 Enter 执行；右键/ESC 取消。";

        /// <summary>目标预测区下方只读提示。</summary>
        public const string ReadOnly = "查看预测不扣费、不抽随机。";

        /// <summary>
        /// BM3（Q512；S14「Q512 无目标指引引用实际按钮」、逐字稿同日表）：操作台「确认」按钮的业务文字（不含 Enter 键帽）。
        /// 结束行动「确认结束行动 · 剩余{剩余AP}AP清零」，其他无目标指令「确认 · {指令名} · {AP}AP」；按钮与指引都从这里取，不另拼第二份。
        /// </summary>
        public static string ConsoleConfirmText(bool endTurn, string command, int ap) => endTurn ? $"确认结束行动 · 剩余{ap}AP清零" : $"确认 · {command} · {ap}AP";

        /// <summary>BM3（Q512）：无目标类指令的指引模板「在操作台点“{确认按钮业务文字}”才提交；ESC 取消。」，变量取同一按钮的 <see cref="ConsoleConfirmText"/>。</summary>
        public static string ConsoleGuide(string confirmText) => $"在操作台点“{confirmText}”才提交；ESC 取消。";

        /// <summary>
        /// 「执行{指令名}」此刻为什么不可用（null 为可用）。顺序：战斗通用锁（未轮到、对白等，<paramref name="lockWhy"/>，空串表示锁定但无原句）→
        /// 群体指令按真实受影响集合（集合为空「没有可用目标。」，否则看预览）→ 尚未指定目标（候选为 0「没有可用目标。」，否则「请先指定目标。」）→
        /// 已指定但不合法：内核更具体句优先；没有时，原先合法、现在失效的写「目标已变化，请重新选择。」，从未合法的候选沿锚点原句 → 合法目标再看预览（成本等）。
        /// 不把失效目标默认成另一只。
        /// </summary>
        public static string? ConfirmBlocked(string? lockWhy, bool area, int legalCount, bool hasTarget, bool targetLegal, bool targetWasLegal, string? targetWhy, string? previewWhy)
        {
            if (lockWhy != null) return lockWhy;
            if (area) return legalCount == 0 ? NoTargets : previewWhy;
            if (!hasTarget) return legalCount == 0 ? NoTargets : NeedTarget;
            if (!targetLegal) return targetWhy ?? (targetWasLegal ? TargetChanged : OutOfScope);
            return previewWhy;
        }
    }

    /// <summary>Esc 这一下该做什么（Q322「Esc」「菜单内」「返回」）。</summary>
    public enum ShellEscRoute
    {
        /// <summary>不处理（反馈窗口自己处理 Esc，关掉后回到游戏菜单）。</summary>
        Ignore,
        /// <summary>收起已开的游戏菜单（常态、对白锁、行动推进、暂停、死亡结算）。</summary>
        CloseMenu,
        /// <summary>BM2（Q500）：游戏菜单开在子页（手动存档）上——先收子页里的覆盖确认，没有就回菜单首页；不关整个菜单、不保存。</summary>
        CloseMenuSubPage,
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
        /// <summary>BM2（Q500）：游戏菜单当前开在子页（手动存档）上；菜单首页为 false。</summary>
        public bool MenuSubPage { get; set; }
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
            if (i.MenuOpen) return i.MenuSubPage ? ShellEscRoute.CloseMenuSubPage : ShellEscRoute.CloseMenu;
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
