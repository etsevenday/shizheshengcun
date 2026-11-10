using System.Net.Http;
using Survival.Core.Config;
using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Web.Services;

/// <summary>
/// Action：拖拽打开的行动预览（指定工具采集）；Clear：清理预览。
/// 基础UI修订 v0.7 第 3、8 节：Facility 为火堆（Id = 设施 ID，如 camp:fire）；PotLiquid 为锅里的一种液体
/// （InstanceId = 锅实例，Id = 液体物品 ID，Owner = pot:&lt;锅ID&gt;）。锅、锅内食材、直烤食材沿用 Tool，按真实实例 ID 选中。
/// </summary>
public enum SelectionKind { None, Location, Entrance, Resource, ItemGroup, Tool, Action, Clear, Facility, PotLiquid, Enemy }

public sealed class Selection
{
    public SelectionKind Kind { get; init; }
    public string Id { get; init; } = "";
    /// <summary>物品所在：carried、地点 ID、facility:&lt;设施ID&gt; 或 pot:&lt;锅ID&gt;（真实 owner，展示路径用内核 WherePath 现算）。</summary>
    public string Owner { get; init; } = "";
    public int? InstanceId { get; init; }

    public string Key => $"{Kind}:{Id}:{Owner}:{InstanceId}";
}

public enum SaveStatus { None, Saving, Saved, Failed }

/// <summary>右栏页签（v0.7 第 6.1 节）：对象信息 / 人物。只在会话内，不进存档、不持久化。</summary>
public enum DetailTab { Object, Character }



/// <summary>
/// 网页壳的会话：持有配置、引擎与当前状态，驱动 5 分钟段循环并在每段之间做进度动画。
/// 所有规则判断都交给内核，这里只负责调度、存档与界面状态。
/// </summary>
public sealed class GameSession
{
    private readonly HttpClient _http;
    private readonly SaveStore _store;

    public GameSession(HttpClient http, SaveStore store)
    {
        _http = http;
        _store = store;
    }

    public GameConfig? Config { get; private set; }
    public GameEngine? Engine { get; private set; }
    public GameState? State { get; private set; }
    public string? LoadError { get; private set; }

    public int Version { get; private set; }
    public event Action? Changed;

    public bool Running { get; private set; }
    private bool _skip;
    // B31a（逐字稿 §1 Q295、§1.1 Q321）：阅读字体与动画速度是当前浏览器的偏好（survival.pref.readFont / animSpeed），
    // 不进存档，换档、新游戏、重载沿用；常态与战斗菜单读同一份。无有效偏好时为默认（细像素、标准），不写回。
    // 本地存储不可用时只在本次会话的内存里保留。速度只改呈现间隔，世界分钟与结算与它无关。
    private UiPresentationPrefs? _presentation;

    public UiPresentationPrefs Presentation
    {
        get
        {
            if (_presentation != null) return _presentation;
            string? font = null, speed = null;
            try
            {
                font = _store.GetPref(UiPresentationPrefs.FontKey);
                speed = _store.GetPref(UiPresentationPrefs.SpeedKey);
            }
            catch { /* 本地存储不可用：从默认开始 */ }
            return _presentation = UiPresentationPrefs.Parse(font, speed);
        }
    }

    /// <summary>世界推进每个 5 游戏分钟段的呈现间隔：快／标准／慢 = 60／120／180 毫秒（Q321，取代 S14 的 40／120／250）。</summary>
    public int SegmentDelayMs => Presentation.SegmentDelayMs;

    /// <summary>当前敌方逐条展示的间隔：快／标准／慢 = 240／480／720 毫秒（Q321，取代原 max(160, 4×段间隔)）。</summary>
    public int EnemyStepDelayMs => Presentation.EnemyStepDelayMs;

    /// <summary>按当前动画速度缩放一个界面动效的基准时长（Q321「其他动效取各自基准时长」）。</summary>
    public int MotionMs(int baseMs) => UiPresentationPrefs.Scale(baseMs, Presentation.Speed);

    public void SetReadingFont(ReadingFont font)
    {
        Presentation.Font = font;
        try { _store.SetPref(UiPresentationPrefs.FontKey, UiPresentationPrefs.FontValue(font)); } catch { /* 只在本次有效 */ }
        ApplyPresentation();
        Notify();
    }

    public void SetAnimationSpeed(AnimationSpeed speed)
    {
        Presentation.Speed = speed;
        try { _store.SetPref(UiPresentationPrefs.SpeedKey, UiPresentationPrefs.SpeedValue(speed)); } catch { /* 只在本次有效 */ }
        ApplyPresentation();
        Notify();
    }

    /// <summary>把当前偏好落到页面（html[data-read-font]、--motion-k）。页面载入时 app.js 已按同一偏好套用一次。</summary>
    public void ApplyPresentation()
    {
        var p = Presentation;
        try { _store.ApplyPresentation(UiPresentationPrefs.FontValue(p.Font), UiPresentationPrefs.Multiplier(p.Speed)); } catch { /* 页面接口不可用时只影响呈现 */ }
    }

    /// <summary>B22f（Q202 ④，交开发方定）：撤出进度条至少可见约 2 秒（纯显示：只放慢每段之间的动画间隔，不推进或延长游戏时间）。</summary>
    public const int ExtractMinVisibleMs = 2000;

    public List<GameEvent> Recent { get; } = new();
    public GameEvent? Toast { get; private set; }
    /// <summary>B31c（R114 toast）：一般提示约 8 秒到时先淡出（140ms×动画速度）再移除；关键提示（Q330）不自动收起。</summary>
    public bool ToastLeaving { get; private set; }
    /// <summary>B31c（Q330）：上次看到的有效提交标记（<see cref="ResultToast.CommitMark"/>），变了就清掉正在显示的结果提示。</summary>
    private string? _commitMark;
    public HashSet<string> Highlights { get; } = new();
    /// <summary>保持的高亮（B18 试玩修复 M372：换物产物落地后描边保持到离开该处地面或下一批新高亮）。</summary>
    public HashSet<string> HeldHighlights { get; } = new();
    /// <summary>保持高亮所属的地面 owner（GameState.Ground）。</summary>
    public string? HeldHighlightAt { get; private set; }

    /// <summary>某张地面物品卡是否该显示保持的落地高亮。</summary>
    public bool HeldHighlight(string itemId) => State != null && HeldHighlightAt == S.Ground && HeldHighlights.Contains(itemId);
    public bool SevenDayPending { get; set; }
    /// <summary>当前反馈位（右栏，暂停层显示时也在暂停层底部）。每次赋值都算一次新的触发（Q318：忙碌句按触发时的占用重新归属）。</summary>
    public string? Notice
    {
        get => _notice;
        set { _notice = value; _busyNotice.Rearm(); }
    }
    private string? _notice;

    /// <summary>Q318（N02 附录 §2）：事项盘忙碌反馈依附触发它的行动，行动都结束、取消或停止后在下次刷新前清掉。</summary>
    private readonly BusyNoticeTracker _busyNotice = new();

    /// <summary>右栏显示的反馈：Q318 暂停层显示时，事项盘忙碌句只在暂停层底部出现一次，右栏省略；其他反馈照常。</summary>
    public string? RightNotice => BusyNoticeTracker.RightColumnNotice(Notice, State, Panel);

    /// <summary>暂停层此刻是否显示（GameScreen 与 Q318 右栏去重共用同一条件）。</summary>
    public bool PauseLayerShown => BusyNoticeTracker.PauseLayerShown(State, Panel);

    /// <summary>Q318：忙碌句依附的行动都已结束、取消或停止时清掉它；其他反馈不动。</summary>
    private void ClearEndedBusyNotice()
    {
        if (_busyNotice.Observe(_notice, State)) _notice = null;
    }

    public SaveStatus SaveState { get; private set; }
    /// <summary>最近一次存档写入失败的浏览器原始错误文本（开发诊断用；玩家看 <see cref="SaveErrorText"/>）。</summary>
    public string? SaveError { get; private set; }
    /// <summary>最近一次失败是否浏览器存储配额用尽（Q282，按异常类别识别）。</summary>
    public bool SaveErrorQuota { get; private set; }
    /// <summary>底栏失败整句（后接“重试”）：配额用尽为「存档失败：浏览器存储空间已满。」，其他为原“保存失败：”+原始信息。</summary>
    public string SaveErrorText => SaveWriteError.PlayerText(SaveErrorQuota, SaveError);

    public Selection Selected { get; private set; } = new();

    /// <summary>右栏当前页签：无选中时默认人物页，选中任何对象自动切到对象信息。</summary>
    public DetailTab DetailTab { get; private set; } = DetailTab.Character;

    public void SetDetailTab(DetailTab tab)
    {
        DetailTab = tab;
        HoverPreview = null;
        // 点“人物”页签回到一级简表（二级说明页只由属性入口打开）
        CharPage = null;
        Notify();
    }

    // ───────────── 右栏人物二级页、左栏状态图标浮层、系统说明（S14 §12、§13） ─────────────

    /// <summary>
    /// 右栏人物页的二级说明页：null 为一级简表；"stat:health"…"stat:morale"、"immunity"、"virus"、"skills"、"traits"。
    /// 在右栏原位置显示，Esc 一次返回一级并保留原选择；只是浏览，不执行玩法、不推进时间。
    /// </summary>
    public string? CharPage { get; private set; }

    public void OpenCharPage(string? page)
    {
        CharPage = page;
        if (page != null)
        {
            DetailTab = DetailTab.Character;
            HoverPreview = null;
        }
        Notify();
    }

    /// <summary>左栏状态图标：悬停中的状态（轻量提示，不拦截输入）与点击固定的说明浮层（"*all" 为“全部状态”）。锚点为图标在视口中的位置。</summary>
    public string? StatusHover { get; private set; }
    public string? StatusPinned { get; private set; }
    public (double Left, double Top) StatusAnchor { get; private set; }

    public void HoverStatus(string? id, double left = 0, double top = 0)
    {
        if (StatusHover == id && (id == null || StatusAnchor == (left, top))) return;
        StatusHover = id;
        if (id != null && StatusPinned == null) StatusAnchor = (left, top);
        Notify();
    }

    public void PinStatus(string? id, double left = 0, double top = 0)
    {
        StatusPinned = id;
        StatusHover = null;
        if (id != null) StatusAnchor = (left, top);
        Notify();
    }

    /// <summary>状态已消失时浮层随之关闭（不保留失效说明）；不通知界面，由下一次渲染体现。</summary>
    public void DropStatusSilently()
    {
        StatusPinned = null;
        StatusHover = null;
    }

    /// <summary>系统说明当前主题与条目（null 为目录 / 主题页）。Esc 每次返回一级：条目 → 主题 → 关闭（S14 §13.3）。</summary>
    public string? HelpTopic { get; private set; }
    public string? HelpEntry { get; private set; }
    /// <summary>从对象/属性/状态详情定位打开时，关闭后要回到的原面板（null 为原主界面）。</summary>
    private string? _helpReturn;
    private bool _helpDirect;
    /// <summary>从固定的状态说明浮层“相关说明”进入时，关闭说明后要恢复的那个浮层与锚点（试玩修复 B8：Esc 逐层回到来源）。</summary>
    private string? _helpReturnStatus;
    private (double Left, double Top) _helpReturnAnchor;

    /// <summary>从固定的状态浮层打开说明：记下浮层，关闭/返回说明时原样恢复它。</summary>
    public void OpenHelpFromStatus(string topic, string entry)
    {
        var pinned = StatusPinned;
        var anchor = StatusAnchor;
        StatusPinned = null;
        StatusHover = null;
        OpenHelp(topic, entry);
        if (Panel == "help") { _helpReturnStatus = pinned; _helpReturnAnchor = anchor; }
    }

    /// <summary>打开系统说明（可直接定位到某主题/条目）。只读浏览，不推进时间；战斗与对话锁期间不可打开。</summary>
    public void OpenHelp(string? topic = null, string? entry = null)
    {
        if (InBattleUi || DialogueLocked) return;
        _helpReturn = Panel is "help" ? _helpReturn : Panel;
        _helpDirect = topic != null;
        _helpReturnStatus = null;
        HelpTopic = topic;
        HelpEntry = entry;
        HoverPreview = null;
        Panel = "help";
        Notify();
    }

    /// <summary>
    /// 系统说明内退一级：条目 → 主题 → 目录 → 关闭。从对象/属性/状态详情直接定位进来的，返回时回到原入口上下文（直接关闭），不强制跳到目录首页。
    /// </summary>
    public void HelpBack()
    {
        if (_helpDirect) { CloseHelp(); return; }
        if (HelpEntry != null) { HelpEntry = null; Notify(); return; }
        if (HelpTopic != null) { HelpTopic = null; Notify(); return; }
        CloseHelp();
    }

    /// <summary>在说明内点选主题/条目（玩家主动翻阅后按目录层级返回）。</summary>
    public void HelpPick(string? topic, string? entry)
    {
        HelpTopic = topic;
        HelpEntry = entry;
        _helpDirect = false;
        Notify();
    }

    public void CloseHelp()
    {
        HelpTopic = null;
        HelpEntry = null;
        _helpDirect = false;
        if (BattleDrawer == "help") BattleDrawer = null;   // B9：战斗内的说明抽屉随之收起
        var back = _helpReturn;
        _helpReturn = null;
        // B29：从食饮盘进来的回到原来的面板（整备也算），食饮盘由 FeedPop 重新校验后显示
        Panel = HelpFromFeed || back is "map" or "craft" or "rest" or "log" or "goals" or "tips" or "dialogs" ? back : null;
        HelpFromFeed = false;
        // 来源是固定的状态浮层：恢复它（状态已消失时 StatusPop 渲染后自行收起）
        if (_helpReturnStatus != null)
        {
            StatusPinned = _helpReturnStatus;
            StatusAnchor = _helpReturnAnchor;
            _helpReturnStatus = null;
        }
        Notify();
    }

    /// <summary>有未读完的对话（Q10）：整局游戏输入锁定，界面只保留对话层的阅读与“继续/结束对话”。</summary>
    public bool DialogueLocked => State != null && Engine != null && E.DialogueLocked(S);
    /// <summary>当前打开的浏览层：map / craft / rest / log / goals / tips / dialogs / help 等（游戏菜单另见 <see cref="MenuOpen"/>）。</summary>
    public string? Panel { get; set; }

    /// <summary>
    /// B31b（逐字稿 §1.1 Q322、PXb）：常态游戏菜单是盖在当前画面上的一层，不替换已开的面板，关闭后回到原场景；
    /// 对白锁、行动推进与暂停中也可打开（锁内只开放关闭、阅读／动画偏好与反馈）。打开或关闭都不提交、取消或恢复世界行动。
    /// 战斗与敌踪用 <see cref="BattleMenuOpen"/>。
    /// </summary>
    public bool MenuOpen { get; private set; }

    /// <summary>实际构建（页面 app-build，网页壳首次渲染时读一次；B31d Q336 游戏菜单版本行用）。</summary>
    public string AppBuild { get; set; } = "";

    /// <summary>死亡结算页的「查看记录」层开着（B31d：结算页 Esc 先收这一层，再开菜单，Q322「先收子层」）。</summary>
    public bool DeathLogOpen { get; set; }

    public void SetMenu(bool open)
    {
        if (open && State == null) return;
        MenuOpen = open;
        if (open)
        {
            // 菜单盖住画面：收起悬停预测、就近浮窗与状态浮层，它们不在菜单下面继续可点（不提交、不取消任何草稿以外的东西）
            HoverPreview = null;
            StatusHover = null;
            StatusPinned = null;
        }
        Notify();
    }
    /// <summary>从设施卡打开加工面板时要预选的配方；面板读取一次后清空。</summary>
    public string? CraftRecipe { get; set; }
    /// <summary>手工制作当前分类标签（B10：工坊/营地建造/自动加工）；null 时按所选配方的工坊。</summary>
    public string? CraftTab { get; set; }
    /// <summary>从材料拖拽打开加工面板时的候选配方（配置顺序）；为空表示列出全部配方。</summary>
    public List<string>? CraftCandidates { get; set; }
    /// <summary>材料槽里手工指定的批次与指定的锅，随确认一起传给 Craft 请求。</summary>
    public List<int> CraftPreferred { get; } = new();
    public int? CraftPot { get; set; }
    /// <summary>批量制作件数（v0.6 §03）；换配方时回到 1。</summary>
    public int CraftBatch { get; set; } = 1;
    /// <summary>工具维护的目标实例（v0.6 §05）。</summary>
    public int? CraftTarget { get; set; }
    /// <summary>现场修补选用的修补包实例（B22b，Q168）；为空时由内核默认选能恢复最多、消耗最少的一包。</summary>
    public int? CraftPatch { get; set; }

    // ───── B23 来源查询（A10-EXUI §4：缺料→“查看来源”进入只读地图定位，关掉地图回制作面板恢复配方/份数/指定批次/目标） ─────
    /// <summary>正在地图上查来源的物品；null 为没有在查。</summary>
    public string? SourceItem { get; private set; }
    /// <summary>地图打开后要居中的地点（MapOverlay 渲染后取用一次）。</summary>
    public string? MapFocus { get; set; }
    private (string? Recipe, string? Tab, int Batch, List<int> Preferred, int? Pot, int? Target, int? Patch, List<string>? Candidates)? _craftReturn;

    /// <summary>从制作面板的缺料行打开来源查询：记下制作面板的选择，打开地图并定位到第一个可获取（或第一个）已知来源地点。只读、0 分钟。</summary>
    public void OpenSources(string itemId)
    {
        if (State == null) return;
        _craftReturn = (CraftRecipe, CraftTab, CraftBatch, CraftPreferred.ToList(), CraftPot, CraftTarget, CraftPatch, CraftCandidates?.ToList());
        SourceItem = null;
        OpenPanel("map");
        if (Panel != "map") { _craftReturn = null; return; }
        SourceItem = itemId;
        var src = E.ItemSources(S, itemId).Where(x => x.LocationId != null).ToList();
        var loc = (src.FirstOrDefault(x => x.Usable) ?? src.FirstOrDefault())?.LocationId;
        if (loc != null) FocusSource(loc);
        Notify();
    }

    /// <summary>来源列表里“定位”：切到该地点所在层、选中并居中（只读，不移动）。</summary>
    public void FocusSource(string locationId)
    {
        MapLayer = E.MapLayerOf(locationId);
        MapAnchor = S.CurrentLocation;
        MapPicked = locationId;
        MapFocus = locationId;
        ClosePop();
        Notify();
    }

    /// <summary>结束来源查询，回到制作面板并恢复原来的配方、分类、份数、指定批次/锅/目标/修补包（再按新库存预览）。</summary>
    public void ReturnFromSources()
    {
        var snap = _craftReturn;
        SourceItem = null;
        _craftReturn = null;
        if (snap is not { } r) { OpenPanel(null); return; }
        OpenPanel("craft", r.Recipe);
        if (Panel != "craft") return;
        CraftTab = r.Tab;
        CraftBatch = r.Batch;
        CraftPreferred.Clear();
        CraftPreferred.AddRange(r.Preferred.Where(id => S.Items.Any(i => i.InstanceId == id)));
        CraftPot = r.Pot;
        CraftTarget = r.Target;
        CraftPatch = r.Patch;
        CraftCandidates = r.Candidates;
        Notify();
    }


    /// <summary>火堆卡“点燃”按钮下的点火工具菜单是否打开（v0.7 第 4.2 节）；只在会话内，Esc 或点菜单外关闭。</summary>
    public bool IgniteMenuOpen { get; private set; }

    /// <summary>选卡操作盘（CardPop）是否显示：选中地面/随身物品、工具或锅内液体，且没有别的局部盘、整理窗、菜单或对白锁让它让位。</summary>
    public bool CardPopShown => State != null && Pop == null && FeedPending == null && !IgniteMenuOpen && !DialogueLocked && !OverflowPending && !DuplicateWeaponsPending
        && !MenuOpen && !BattleMenuOpen && Selected.Kind is SelectionKind.ItemGroup or SelectionKind.Tool or SelectionKind.PotLiquid;

    /// <summary>
    /// B31c（Q324）：屏幕中央有局部操作盘（选卡操作、移动／接近／投炭／采集等就近盘、点火工具、食饮）开着。中央行动位此时与一级面板开着时一样
    /// 收成中部底部窄条，不被居中盘挡住「退出行动」（排版处理，不改规则）。
    /// </summary>
    public bool LocalDiskOpen => State != null && !InBattleUi && (Pop != null || FeedPending != null || IgniteMenuOpen || CardPopShown);

    public void ToggleIgniteMenu()
    {
        IgniteMenuOpen = !IgniteMenuOpen;
        if (IgniteMenuOpen) Pop = null;
        if (!IgniteMenuOpen) HoverPreview = null;
        Notify();
    }

    public void CloseIgniteMenu()
    {
        if (!IgniteMenuOpen) return;
        IgniteMenuOpen = false;
        Notify();
    }

    /// <summary>
    /// 就近操作浮窗（S14 §14.1、S02 §5、R05 §7）：一个时刻只有一个当前操作上下文。Kind：travel（相邻地图卡确认移动）、
    /// mapnode（区域地图节点确认移动）、approach（敌踪卡接近并开战）、gather（拖工具到资源卡后的开始确认）。
    /// （B3 起战斗换武器浮窗取消，改为武器Ⅰ/武器Ⅱ页签。）
    /// 只是界面状态，不进存档；Esc/关闭只收起，不提交、不取消已运行的行动。切对象、开面板、开始行动、进入战斗或对话时收起。
    /// </summary>
    public sealed record PopContext(string Kind, string Id, string AnchorId);
    public PopContext? Pop { get; private set; }

    public void OpenPop(string kind, string id, string anchorId)
    {
        if (State == null || DialogueLocked) return;
        FeedPending = null;
        Pop = new PopContext(kind, id, anchorId);
        IgniteMenuOpen = false;
        Notify();
    }

    /// <summary>收起操作浮窗（Esc、×、取消、锚点滚出或消失）。拖工具打开的采集预览随浮窗一起作废（右栏不留无法开始的预览）。</summary>
    public void ClosePop()
    {
        if (Pop == null) return;
        DropPop();
        Notify();
    }

    /// <summary>不通知界面地收起浮窗（目标失效时由渲染方调用，避免 Esc 栈停在看不见的浮窗上）。</summary>
    public void DropPop()
    {
        if (Pop?.Kind == "gather" && Selected.Kind == SelectionKind.Action)
        {
            ClearSelectionSilently();
            PendingAction = null;
        }
        Pop = null;
    }

    /// <summary>悬停或键盘聚焦某个动作按钮时的临时预测；离开即清空。右栏在“执行中”之后、选中对象之前显示它。</summary>
    public ActionRequest? HoverPreview { get; private set; }
    public string HoverTitle { get; private set; } = "";

    /// <summary>拖拽打开的行动预览（SelectionKind.Action）。</summary>
    public ActionRequest? PendingAction { get; private set; }
    public string PendingActionTitle { get; private set; } = "";

    /// <summary>按住 Shift 松手时等待玩家选数量的落点；取消数量框等于取消本次拖拽。</summary>
    public DropIntent? PendingDrop { get; private set; }
    private string _pendingDropRequest = "";

    /// <summary>地图上当前选中的地点（地图面板的右栏详情据此显示移动预估）。</summary>
    public string? MapPicked { get; set; }

    // ───── 分层地图阅读状态（B12 R38，A10-EXUI §1、EXUI13）：只存在会话里、不进存档；当前位置没变就恢复，变了回默认层居中 ─────
    /// <summary>正在看的层键（world / city:父城 / dungeon:地牢）；null 为按当前位置的默认层。</summary>
    public string? MapLayer { get; set; }
    /// <summary>缩放档序号（见 MapOverlay.Zooms；0 为“总览”只画点）。</summary>
    public int MapZoom { get; set; } = MapDefaultZoom;
    public const int MapDefaultZoom = 2;
    /// <summary>上次设定阅读状态时的当前位置。</summary>
    public string? MapAnchor { get; set; }
    /// <summary>图例浮窗是否展开。</summary>
    public bool MapLegend { get; set; }
    public string RestKind { get; set; } = "rest";
    public int RestMinutes { get; set; } = 60;

    /// <summary>
    /// 打开或关闭二级面板（地图、加工、休息、日志）。二级面板只覆盖中部上方，
    /// 左侧状态、随身物品与右侧详情保持不动（试玩留言 8f9a7c9b 第 2 点）。
    /// </summary>
    public void OpenPanel(string? panel, string? recipe = null)
    {
        // B29：食饮说明开着时点顶栏“系统说明”关掉，同样回到原食饮窗口
        if (panel == null && Panel == "help" && HelpFromFeed) { CloseHelp(); return; }
        // 战斗与敌踪选择中：地图、制作、休息、日志及顶栏浏览入口锁定（v0.5 §16；生存目标/提示、对话记录、系统说明沿用日志限制，S14 §12.3、§13.3）
        if (IsTopPanel(panel) && InBattleUi) return;
        // 对话读完锁（Q10）：不打开任何面板；关闭（null）仍允许，便于对话出现时收起菜单
        if (panel != null && DialogueLocked) return;
        // B23：查来源时关掉地图（关闭按钮、Esc）回制作面板并恢复原选择；切到别的面板就结束这次查询
        if (SourceItem != null && Panel == "map" && panel == null) { ReturnFromSources(); return; }
        if (panel != "map") { SourceItem = null; _craftReturn = null; }
        // B24（A10-CITY §3/§7）：主城鸟瞰打开时记住右栏页签，关闭后原样恢复（不自动展开人物板块、不改折叠偏好）
        var leavingCity = Panel == "citymap" && panel != "citymap";
        if (panel == "citymap" && Panel != "citymap") { _cityTabBefore = DetailTab; CityPick = State != null ? E.CityDefaultPick(S, CityTarget) : null; CityHover = null; NewCityRequest(); }
        HoverPreview = null;
        Pop = null;
        // 离开编队面板（切到别的面板或关闭）就丢弃草稿：关闭不自动保存（Q86）
        if (panel != "formation") { FormationDraft = null; FormationPick = null; }
        GivePending = null;
        FeedPending = null;
        HelpFromFeed = false;
        // 只有地图/制作/休息会在右栏显示面板自己的预估，切到“对象信息”；顶栏浏览层（日志、目标、提示、对话记录、说明）不改右栏页签
        if (panel != null) { IgniteMenuOpen = false; if (panel is "map" or "craft" or "rest") DetailTab = DetailTab.Object; }
        if (panel == "help") { HelpTopic = null; HelpEntry = null; _helpDirect = false; _helpReturn = null; _helpReturnStatus = null; }
        if (panel == "dialogs") DialogLogPick = null;
        // 关闭面板时没有选中对象就回人物页（v0.7 6.1：无选中默认展示人物信息；复验新发现 2）。
        // 试玩修复 M77-B8：原来这句 else 挂在 dialogs 判断上，打开休息/制作/地图也会被切回人物页，右栏看不到“详细预估”
        if (panel == null && Selected.Kind == SelectionKind.None) DetailTab = DetailTab.Character;
        if (leavingCity) { if (Selected.Kind == SelectionKind.None) DetailTab = _cityTabBefore; CityPick = null; CityHover = null; CityLegacyNote = false; }
        // 每次打开或关闭面板都清掉上一次拖拽留下的候选与指定批次；拖拽入口在打开后再写入
        CraftCandidates = null;
        CraftPreferred.Clear();
        CraftPot = null;
        CraftBatch = 1;
        CraftTarget = null;
        CraftPatch = null;
        if (panel == "craft" && State != null)
        {
            CraftTab = null;
            var main = S.Main;
            if (main != null && (main.Type == ActionType.Craft || main.Type == ActionType.Build)) CraftRecipe = main.TargetId;
            else if (recipe != null) CraftRecipe = recipe;
            else if (CraftRecipe == null || E.Config.Recipes.All(r => r.Id != CraftRecipe))
                CraftRecipe = "knife";
        }
        if (panel == "rest" && State != null)
        {
            var sleepy = S.Stats.Wakefulness < 50 || !E.IsDaylight(S.GameMinute);
            RestKind = sleepy ? "sleep" : "rest";
            RestMinutes = sleepy ? 480 : 60;
        }
        if (panel == "map")
        {
            MapPicked = null;
            MapLegend = false;
            // 阅读状态：当前位置没变就恢复上次的层与缩放，变了回默认层（EXUI13）
            if (State != null && MapAnchor != S.CurrentLocation) { MapLayer = null; MapZoom = MapDefaultZoom; MapAnchor = S.CurrentLocation; }
        }
        Panel = panel;
        // B13d（Q128）：打开相关面板即操作前引导的上下文（手工制作看当前配方、整备、恢复、远行准备）
        switch (panel)
        {
            case "craft": Guide("recipe", CraftRecipe); break;
            case "gear": Guide("gear"); break;
            case "rest": Guide("rest"); break;
            case "journey": Guide("journey"); break;
        }
        Notify();
    }

    /// <summary>
    /// 操作前引导的上下文（B13d，Q128 选 C）：界面打开/选中相关对象、动作提交前报给内核，内核决定给哪一项系统提示与饱饱/主角短句（0 分钟）。
    /// 状态有变（给了提示、播了短句或忙时暂存）才存档；不通知界面，由调用方统一 Notify。
    /// </summary>
    public void Guide(string ctx, string? id = null, string? owner = null)
    {
        // 试玩修复 M503：鸟瞰浏览中点第四行的卡只看详情，不给引导——否则饱饱对白会清掉已选建筑（CityOverviewTick 对白期间收起未提交的确认）
        if (State == null || S.Progress.Dead || CityBrowsing) return;
        if (E.GuideContext(S, ctx, id, owner)) { Drain(); Save(); }
    }

    /// <summary>替换中部上方的顶栏面板：地图、制作、休息、日志，以及生存目标、生存提示、对话记录、系统说明。</summary>
    // ───────────── 三城主城鸟瞰（B24，A10-CITY §3—§7、S07 中枢联动归档 1/2） ─────────────

    /// <summary>主城鸟瞰正在打开：上三行被覆盖，第四行随身只看不提交。</summary>
    public bool CityBrowsing => State != null && Panel == "citymap";
    public const string CityReadOnlyText = CityCopy.ReadOnly;
    /// <summary>鸟瞰里固定（点选）的建筑；悬停/聚焦的建筑。只存会话。</summary>
    public string? CityPick { get; private set; }
    public string? CityHover { get; private set; }
    /// <summary>多段路线的最终目标（首期逐段走：到达中途节点后再打开鸟瞰时默认选中它；只存会话，不进存档）。</summary>
    public string? CityTarget { get; private set; }
    /// <summary>本次是旧档补显（顶部写一句说明，不补奖励）。</summary>
    public bool CityLegacyNote { get; private set; }
    /// <summary>当前确认窗的路线请求号：同一确认只发一个，双击/重复回调不重复走路（A10-CITY §5）。</summary>
    public string CityRequest { get; private set; } = "";
    private DetailTab _cityTabBefore = DetailTab.Character;

    private void NewCityRequest() => CityRequest = "city:" + Guid.NewGuid().ToString("N");

    private ActionResult CityReadOnlyFail()
    {
        Notice = CityReadOnlyText;
        Notify();
        return ActionResult.Fail(CityReadOnlyText);
    }

    /// <summary>当前地点卡旁“主城鸟瞰”：开着再按只退浏览层；不能开时提示原因。</summary>
    public void ToggleCityOverview()
    {
        if (State == null) return;
        if (Panel == "citymap") { OpenPanel(null); return; }
        if (E.CityOverviewBlocked(S) is string why) { Notice = why; Notify(); return; }
        OpenPanel("citymap");
    }

    public void HoverCity(string? nodeId)
    {
        if (CityHover == nodeId) return;
        CityHover = nodeId;
        Notify();
    }

    /// <summary>点建筑：只固定说明与确认窗（不移动）；再点同一栋或右键取消未提交选择。</summary>
    public void PickCity(string? nodeId)
    {
        CityPick = nodeId == CityPick ? null : nodeId;
        NewCityRequest();
        Notify();
    }

    /// <summary>“进入当前地点 · 不耗时”：关闭鸟瞰回当前地点（不移动、不触发服务）。</summary>
    public void EnterCurrentFromCity()
    {
        CityTarget = null;
        OpenPanel(null);
    }

    /// <summary>“前往”：内核重验后只提交第一段真实移动，立即关闭鸟瞰回常态主界面，用中央推进条与退出按钮。</summary>
    public async Task CityGo(string targetId)
    {
        if (State == null) return;
        var path = E.CityOverviewOf(S)?.Buildings.FirstOrDefault(b => b.NodeId == targetId)?.Path;
        var res = E.StartCityOverviewTravel(S, targetId, CityRequest);
        if (res.Duplicate) return;
        if (!res.Ok) { Notice = res.Reason; Notify(); return; }
        CityTarget = path != null && path.Count > 1 ? targetId : null;
        var firstTo = path != null && path.Count > 0 ? path[0].To : null;
        Notice = null;
        Toast = null;
        Pop = null;
        ClearHoverNow();
        OpenPanel(null);
        Drain();
        Save();
        Notify();
        await RunLoop();
        if (State == null) return;
        if (CityTarget != null && (S.CurrentLocation == CityTarget || E.CityOf(S.CurrentLocation) != E.CityOf(CityTarget))) CityTarget = null;
        // B26（Q223）：到终点写“已到达”，半路写还剩几段，路线失效写真实原因；单段路线也提示到达
        if (E.CityArrivalNote(S, targetId, firstTo) is string note)
        {
            Notice = note;
            Notify();
        }
    }

    /// <summary>
    /// 每次刷新时：鸟瞰开着但出现敌踪/战斗/死亡就自动关闭（对白期间只收起未提交的确认，读完恢复可读图）；
    /// 首次到达父节点、对白读完且安全空闲时自动打开一次（A10-CITY §3，开发假设 B24-1）。
    /// </summary>
    private void CityOverviewTick()
    {
        if (State == null) return;
        if (Panel == "citymap")
        {
            if (DialogueLocked) { CityPick = null; return; }
            if (E.CityOverviewBlocked(S) != null)
            {
                Panel = null;
                if (Selected.Kind == SelectionKind.None) DetailTab = _cityTabBefore;
                CityPick = null; CityHover = null; CityLegacyNote = false;
            }
            return;
        }
        if (Panel != null || Running || !E.CityOverviewAutoOpenDue(S) || OverflowPending || DuplicateWeaponsPending) return;
        CityLegacyNote = E.MarkCityOverviewShown(S);
        _cityTabBefore = DetailTab;
        CityPick = E.CityDefaultPick(S, CityTarget);   // 试玩修复 M504：多段路线途经父节点首次自动打开时，同手动打开一样预选最终目标
        CityHover = null;
        NewCityRequest();
        Panel = "citymap";
        Save();
    }

    public static bool IsTopPanel(string? panel) =>
        panel is "map" or "craft" or "rest" or "log" or "goals" or "tips" or "dialogs" or "help" or "formation" or "gear" or "growth" or "journey" or "city" or "safehouse" or "drying" or "citymap";

    // ───────────── 新手序章（B13，S15 §3—§8、A10-TUTOR） ─────────────

    /// <summary>任务角已显示过的“最近完成”键（任务ID@分钟），会话内记住，任务角重建不重复闪。</summary>
    public string? TaskDoneSeen { get; set; }

    /// <summary>开发入口等直接改状态后，把内核待发事件（如来客 Notice）送进结果提示与页脚（试玩修复 M269 P8）。</summary>
    public void FlushEvents() => Drain();

    /// <summary>当前主目标要轻量点亮的对象（npc:ID、doc:ID、loc:ID），每次最多一个；没有为 null。</summary>
    public string? TutorialFocus => State == null || S.Progress.Dead ? null : E.ObjectiveFocus(S);

    /// <summary>序章操作（交谈、查看文书、路线日志、远行确认）：0 分钟；成功后收起浏览面板让对白层可见，存档并刷新。</summary>
    private ActionResult PrologueDo(Func<ActionResult> f, bool closePanel = true)
    {
        if (State == null) return ActionResult.Fail("没有进行中的游戏");
        if (FormationOpen) return Commit(ActionResult.Fail(FormationOpenText));
        var res = f();
        Notice = res.Ok ? null : res.Reason;
        if (res.Ok)
        {
            if (closePanel && E.DialogueLocked(S)) { Panel = null; Pop = null; }
            Drain();
            Save();
        }
        Notify();
        return res;
    }

    public ActionResult TalkNpc(string npcId) => PrologueDo(() => E.TalkNpc(S, npcId));

    /// <summary>B17 安全屋/工坊批次等 0 分钟内核操作：失败写提示，成功存档并刷新（不收起面板）。</summary>
    public ActionResult EngineDo(Func<ActionResult> f) => PrologueDo(f, closePanel: false);
    public ActionResult ReadDocument(string docId, string? choiceId = null) => PrologueDo(() => E.ReadDocument(S, docId, choiceId));

    // ───────────── 城市岗位事项（B14，A11 Q33 服务表） ─────────────

    /// <summary>
    /// 打开制作面板并切到某工坊分类（B14b，Q141：点公共工坊站卡、或在营区工坊/试制工坊办“使用营区工坊/使用试制工坊”）。
    /// 选中该分类里门槛最少的第一条配方；只是浏览，0 分钟。
    /// </summary>
    public void OpenCraftStation(string stationId)
    {
        if (State == null) return;
        var first = E.Config.Recipes.Where(r => !r.IsSalvage && !r.IsBuild && r.Station == stationId)
            .OrderBy(r => E.RecipeLocks(S, r).Count > 0 ? 1 : 0).FirstOrDefault();
        OpenPanel("craft", first?.Id);
        if (Panel != "craft") return;
        CraftTab = stationId;
        Notify();
    }

    /// <summary>城市事项面板当前对应的岗位 npcId。</summary>
    public string? CityNpcId { get; set; }

    /// <summary>B18：鉴定所的事项面板内容多（三页签、样本与报价列表），与制作面板一样占前三行高。</summary>
    public bool CityPanelTall => State != null && CityNpcId != null && E.CityNpc(CityNpcId)?.ServiceIds.Any(x => E.IsAppraisalService(x) || E.IsRecruitService(x)) == true;

    /// <summary>打开某岗位的“事项”面板（预览 0 分钟，只看不提交）。</summary>
    public void OpenCityServices(string npcId)
    {
        CityNpcId = npcId;
        // B18：在鉴定所安全时打开面板 = 当日首次安全访问的兜底（到达时通常已锁定）；锁定后存档，之后开关窗都只读存档里的表
        if (State != null && E.CityNpc(npcId)?.ServiceIds.Any(E.IsAppraisalService) == true && E.QuoteSnapshot(S, Clock.Day(S.GameMinute)) == null
            && E.EnsureAppraisalQuotes(S) != null)
            Save();
        // B19：在收容所打开面板 = 安全访问的兜底（旧档首次实到在这里开放）；同周期只读存档里的候选，换批时生成当前这一批
        if (State != null && E.CityNpc(npcId)?.ServiceIds.Any(E.IsRecruitService) == true)
        {
            var opened = E.TryOpenShelterRecruitment(S);
            if (opened | E.EnsureRecruitCycle(S)) { Drain(); Save(); }
        }
        OpenPanel("city");
    }

    // ───────────── 收容所周期招募（B19，R08 RC1—RC4） ─────────────

    /// <summary>登记一名当前候选（10 分钟 wait；开始锁候选、名额与所选去向，完成才建人；B22g：去向键 party/square/sh:地点）。</summary>
    public Task<ActionResult> StartRecruit(string candidateId, string placementKey) =>
        RunService(() => E.StartRecruit(S, candidateId, GameEngine.PlacementFromKey(placementKey), Guid.NewGuid().ToString("N")));

    /// <summary>待安置确认：改选合法去向（0 分钟建人送达）。</summary>
    public ActionResult ResolveRecruitPlacement(string placementKey)
    {
        if (State == null) return ActionResult.Fail("没有进行中的游戏");
        return Commit(E.ResolveRecruitPlacement(S, GameEngine.PlacementFromKey(placementKey), Guid.NewGuid().ToString("N")));
    }

    /// <summary>待安置确认：取消（不发人不发物，已过时间不退）。</summary>
    public ActionResult CancelRecruitPlacement()
    {
        if (State == null) return ActionResult.Fail("没有进行中的游戏");
        return Commit(E.CancelRecruitPlacement(S, Guid.NewGuid().ToString("N")));
    }

    /// <summary>让一名生成角色退役（0 分钟；个人物品进收容所认领箱）。</summary>
    public ActionResult Retire(string actorId)
    {
        if (State == null) return ActionResult.Fail("没有进行中的游戏");
        return Commit(E.Retire(S, actorId, Guid.NewGuid().ToString("N")));
    }

    // ───────────── 鉴定所（B18，R07 X3） ─────────────

    /// <summary>开始详细鉴定所选样本（10 分钟 wait；完成才扣纤维、写记录）。</summary>
    public Task<ActionResult> StartAppraise(int instanceId) => RunService(() => E.StartAppraise(S, instanceId, Guid.NewGuid().ToString("N")));

    /// <summary>交出所选样本 1 份换当日一档报价（5 分钟 wait；完成才交样、产物落地面）。</summary>
    public Task<ActionResult> StartAppraisalTrade(int instanceId, string offerId) =>
        RunService(() => E.StartAppraisalTrade(S, instanceId, offerId, Guid.NewGuid().ToString("N")));

    /// <summary>教学样本免费说明（0 分钟，不扣纤维）。</summary>
    public ActionResult AppraisalTutorialNote(int instanceId)
    {
        if (State == null) return ActionResult.Fail("没有进行中的游戏");
        return Commit(E.AppraisalTutorialNote(S, instanceId, Guid.NewGuid().ToString("N")));
    }

    private async Task<ActionResult> RunService(Func<ActionResult> start)
    {
        if (State == null) return ActionResult.Fail("没有进行中的游戏");
        if (FormationOpen) return Commit(ActionResult.Fail(FormationOpenText));
        var res = start();
        Notice = res.Ok ? null : res.Reason;
        if (res.Ok)
        {
            if (S.Main != null || E.DialogueLocked(S)) { Panel = null; Pop = null; }
            Drain();
            ReconcileSelection();
            Save();
        }
        Notify();
        if (res.Ok && !res.Duplicate && S.Main != null) await RunLoop();
        return res;
    }

    /// <summary>
    /// 办理一项城市事项：0 分钟的当场提交（登记、咨询、工坊入口等）；有工时的开工为主行动后收起面板照常推进（换药、施济、休息）。
    /// 公共工坊入口成功后打开制作面板。
    /// </summary>
    public async Task<ActionResult> StartCityService(string serviceId, string? targetActorId, string? choiceId)
    {
        if (State == null) return ActionResult.Fail("没有进行中的游戏");
        if (FormationOpen) return Commit(ActionResult.Fail(FormationOpenText));
        var opensCraft = E.CityServicePreview(S, serviceId)?.OpensCraft == true;
        var res = E.StartCityService(S, serviceId, targetActorId, choiceId, Guid.NewGuid().ToString("N"));
        Notice = res.Ok ? null : res.Reason;
        if (res.Ok)
        {
            if (S.Main != null || E.DialogueLocked(S)) { Panel = null; Pop = null; }
            Drain();
            Save();
            if (opensCraft)
            {
                // Q141：打开工坊入口时切到当地第一个已开放的公共站分类；没有已开放的就留在手工
                var st = E.PublicStationsHere(S).FirstOrDefault(p => p.Blocked == null)?.StationId;
                OpenCraftStation(st ?? "hand");
                return res;
            }
        }
        Notify();
        if (res.Ok && !res.Duplicate && S.Main != null) await RunLoop();
        return res;
    }
    // ───────────── 主线与委托（B15） ─────────────

    /// <summary>在委托条目里接取（与岗位交谈同效；同行邀约为选择登记）：0 分钟，播接取对白。</summary>
    /// <remarks>Q317：只由事项盘调用，失败原因为当前行动占用时与按钮旁同句（<see cref="GameEngine.CityPanelReason"/>）。</remarks>
    public ActionResult AcceptQuest(string questId) => PrologueDo(() => CityPanelResult(E.AcceptQuest(S, questId, Guid.NewGuid().ToString("N"))));

    /// <summary>Q317：事项盘内的失败结果，行动占用原因换成事项盘统一句，其他原因原样。</summary>
    private static ActionResult CityPanelResult(ActionResult r)
    {
        if (!r.Ok) r.Reason = GameEngine.CityPanelReason(r.Reason) ?? r.Reason;
        return r;
    }

    /// <summary>
    /// 读完同行邀约后的选择（B15a，Q156 选 B）：“登记同行”入名册候补并结束对话；“暂不登记”只结束对话，以后可再谈。0 分钟 0 物资。
    /// 结束后与“结束对话”一样，让在安全边界等待的行动继续。
    /// </summary>
    public void AnswerRecruit(string questId, string lineId, bool register)
    {
        if (State == null) return;
        var res = E.AnswerRecruit(S, questId, lineId, register);
        Notice = res.Ok || res.Duplicate ? null : res.Reason;
        Drain();
        Save();
        Notify();
        if (E.DialogueLocked(S) || S.Progress.Dead) return;
        if (S.Sub != null || (S.Main != null && S.Main.Status == ActionStatus.Running)) _ = RunLoop();
    }

    /// <summary>回看已完成委托的对白或 AC02 外部医疗线索（0 分钟，只回放）。</summary>
    public ActionResult ReviewQuest(string questId, bool note = false) => PrologueDo(() => E.ReviewQuest(S, questId, note));

    /// <summary>委托条目里的工作：0 分钟阅读当场完成；有工时的（核对物资账）开工为主行动后收起面板照常推进。</summary>
    public async Task<ActionResult> StartQuestWork(string questId)
    {
        if (State == null) return ActionResult.Fail("没有进行中的游戏");
        if (FormationOpen) return Commit(ActionResult.Fail(FormationOpenText));
        var res = CityPanelResult(E.StartQuestWork(S, questId, Guid.NewGuid().ToString("N")));   // Q317：只由事项盘调用
        Notice = res.Ok ? null : res.Reason;
        if (res.Ok)
        {
            if (S.Main != null || E.DialogueLocked(S)) { Panel = null; Pop = null; }
            Drain();
            Save();
        }
        Notify();
        if (res.Ok && !res.Duplicate && S.Main != null) await RunLoop();
        return res;
    }

    public ActionResult OpenRouteLog() => PrologueDo(() => E.OpenRouteLog(S));
    public ActionResult ConfirmJourney() => PrologueDo(() => E.ConfirmJourney(S), closePanel: false);
    public ActionResult SayNotReady() => PrologueDo(() => E.SayNotReady(S));

    /// <summary>系统提示“关闭这条”：只记已关闭，不完成任务，可在任务册回看。</summary>
    public void DismissHint(string taskId)
    {
        if (State == null) return;
        E.DismissHint(S, taskId);
        Save();
        Notify();
    }

    /// <summary>“暂停后续操作提示”/“恢复操作提示”：只开关中性教学提示。</summary>
    public void SetHintsPaused(bool paused)
    {
        if (State == null) return;
        E.SetHintsPaused(S, paused);
        Save();
        Notify();
    }

    /// <summary>对话记录浏览层里选中的那一次对话（DialogueSession.Id）；null 为最近一次。</summary>
    public int? DialogLogPick { get; set; }

    // ───────────── 界面偏好（左栏折叠、提示翻页），存在浏览器本地，不进存档 ─────────────

    private HashSet<string>? _collapsed;

    public bool IsCollapsed(string key)
    {
        if (_collapsed == null)
        {
            try { _collapsed = new HashSet<string>((_store.GetPref("collapsed") ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries)); }
            catch { _collapsed = new HashSet<string>(); }
        }
        return _collapsed.Contains(key);
    }

    public void ToggleCollapsed(string key)
    {
        IsCollapsed(key);
        if (!_collapsed!.Remove(key)) _collapsed.Add(key);
        try { _store.SetPref("collapsed", string.Join(",", _collapsed)); } catch { /* 本地存储不可用时只在本次有效 */ }
        Notify();
    }

    // 右栏人物板块折叠（S14 截图复核 §C，UI-M07—M09；开发计划 v0.9 M87）：按 档案×角色×板块 记玩家最近一次点击，
    // 存浏览器本地 survival.pref.fold（与左栏折叠同一机制），不进存档；组件重建、切次级菜单、刷新、读档、退出战斗都不重置。
    // 本地存储不可用时只在本次会话的内存里保留。

    private UiFoldPrefs? _fold;

    private UiFoldPrefs Fold
    {
        get
        {
            if (_fold != null) return _fold;
            string? raw = null;
            try { raw = _store.GetPref("fold"); } catch { /* 本地存储不可用：从默认值开始，只在内存里记 */ }
            return _fold = UiFoldPrefs.Parse(raw);
        }
    }

    /// <summary>当前角色某板块是否展开（没有记录时为原默认：展开）。只读，不写默认值。</summary>
    public bool IsFoldOpen(string sectionId, string actorId = UiFoldPrefs.MainActor) =>
        Fold.IsOpen(UiFoldPrefs.DefaultProfile, actorId, sectionId);

    /// <summary>对白读完前、战斗/接敌界面期间，人物栏折叠不接受背后点击（S14 截图复核 §C 输入锁）。</summary>
    public bool FoldLocked => DialogueLocked || InBattleUi;

    /// <summary>玩家点击折叠标题：切换并立即写入偏好。输入锁期间不处理。</summary>
    public void ToggleFold(string sectionId, string actorId = UiFoldPrefs.MainActor)
    {
        if (FoldLocked) return;
        Fold.Toggle(UiFoldPrefs.DefaultProfile, actorId, sectionId);
        try { _store.SetPref("fold", Fold.Serialize()); } catch { /* 本地存储不可用时只在本次有效 */ }
        Notify();
    }

    public int TipIndex { get; set; }

    public GameEngine E => Engine!;
    public GameState S => State!;

    public async Task InitAsync()
    {
        if (Config != null) return;
        try
        {
            await Task.Yield();
            // 配置以内嵌资源随程序集发布，唯一来源仍是 data/config.v0.6.json
            using var stream = typeof(GameSession).Assembly.GetManifestResourceStream("config.v0.6.json")
                               ?? throw new InvalidOperationException("缺少内嵌配置 config.v0.6.json");
            using var reader = new StreamReader(stream);
            var json = await reader.ReadToEndAsync();
            Config = ConfigLoader.FromJson(json);
            Engine = new GameEngine(Config);
            Icons.RegisterCategories(Config);   // B4 新物品/装备缺正式图时按分类占位（同一 ID 全局一张）
        }
        catch (Exception ex)
        {
            LoadError = ex.Message;
        }
    }

    public bool HasSave
    {
        get { try { return _store.HasSave; } catch { return false; } }
    }

    public void Notify()
    {
        SyncCommitMark();
        ResetOnLocationChange();
        ReconcileFeed();
        OnDialogueOnset();
        CityOverviewTick();
        CloseStaleFormation();
        ClearStaleFormationNotice();
        ClearEndedBusyNotice();
        Version++;
        Changed?.Invoke();
    }

    /// <summary>Q281：右栏对象上下文所属的真实地点（只在会话内）。</summary>
    private readonly LocationContextTracker _uiLocation = new();

    /// <summary>
    /// Q281（S14「2026-11-09 Q281–Q282」）：真实地点 ID 前后不同（正常抵达、撤退、撤出逐段换点、现有传送）时，在新地点首次可操作前
    /// （每次刷新界面之前）清掉旧右栏对象选中及只依附它的批次、拖拽预览、卡下食饮草稿、卡旁浮窗与点火菜单，右栏回默认人物页。
    /// 打开地图、鸟瞰预览、移动在途、取消或移动失败不改 CurrentLocation，不在这里清，沿 ReconcileSelection 的同地点有效性校验。
    /// 不动 Notice（当前反馈位）、面板与地图选点（各按自己的规则）。
    /// </summary>
    private void ResetOnLocationChange()
    {
        if (State == null) { _uiLocation.Reset(); return; }
        if (!_uiLocation.Observe(S.CurrentLocation)) return;
        ClearSelectionSilently();
        SelectedBatchKey = null;
        PendingAction = null;
        Pop = null;
        IgniteMenuOpen = false;
    }

    private bool _wasLocked;

    /// <summary>
    /// 对话出现的那一刻（S14 §10.1、S07 §7）：取消未提交的数量框、点火工具菜单、悬停/拖拽打开的临时行动预览与状态浮层，
    /// 收起菜单与战斗菜单，不执行其落点或原确认，保证不叠加两个可执行输入层；拖拽本身由 GameScreen 渲染后通知 drag.js 取消。
    /// 选中对象、二级面板、滚动位置保持不动，对话结束后原样恢复。
    /// </summary>
    private void OnDialogueOnset()
    {
        var locked = DialogueLocked;
        if (locked && !_wasLocked)
        {
            PendingDrop = null;
            IgniteMenuOpen = false;
            DropPop();
            ClearHoverNow();
            StatusHover = null;
            StatusPinned = null;
            MenuOpen = false;
            BattleMenuOpen = false;
            // B9 战斗对白锁（A10 §6.2）：取消未提交瞄准、医疗确认、抽屉与临时悬停；已提交的指令不撤销，选中查看的对象保留
            BattleAction = null;
            BattleItem = null;
            BattleHover = null;
            BattleDrawer = null;
            MedTrayOpen = false;
            if (Selected.Kind == SelectionKind.Action)
            {
                ClearSelectionSilently();
                PendingAction = null;
            }
        }
        _wasLocked = locked;
    }

    // ───────────── 开局与存档 ─────────────

    public void NewGame()
    {
        var seed = (uint)(DateTime.UtcNow.Ticks & 0xFFFFFFFF);
        State = E.NewGame(seed);
        ResetUi();
        Recent.Add(new GameEvent { Kind = GameEventKind.Info, Minute = S.GameMinute, Text = S.Log.LastOrDefault()?.Text ?? "" });
        Save();
        Notify();
    }

    /// <summary>读取单槽存档。版本不兼容时返回错误文本。</summary>
    public string? Continue()
    {
        try
        {
            var json = _store.Load();
            if (string.IsNullOrEmpty(json)) return "没有存档";
            // 旧版存档（0.2-proto-1、3）先备份原文，再迁移到当前版本并覆盖保存；迁移冲突时不覆盖原档（设施与烹饪 00 案第 6 节）
            var migrate = SaveSerializer.NeedsMigration(json);
            if (migrate) _store.Backup(json);
            State = SaveSerializer.Deserialize(json, Config, out var notes);
            E.SyncFacilities(S);
            // Q89（B8b）：地图导入 v3 营区/收容所节点后一次迁址（没有这两个节点时什么也不做；迁过的不再迁）
            if (E.MigrateServiceLocations(S) && !migrate) Save();
            if (E.SubstituteIntroBoundary(S) && !migrate) Save();   // Q243 save：读回的待补播按当前地点、身份和锁重验
            ResetUi();
            foreach (var l in S.Log.TakeLast(8))
                Recent.Add(new GameEvent { Minute = l.Minute, Text = l.Text, Kind = GameEventKind.Info });
            if (migrate)
            {
                Save();
                if (SaveSerializer.AnnouncesUpgrade(json)) Notice = $"旧存档已升级到版本{GameState.CurrentSchema}，原档已备份" + (notes.Count > 0 ? "：" + string.Join("；", notes) : "") + "。";
            }
            Notify();
            // 行动动画每 5 分钟段都会存档，播放中刷新页面时存档里的主行动仍是“运行中”；
            // 读档后要接着推进，否则界面停在“进行中”却没有跳过/取消按钮，也开不了别的行动
            var active = S.Sub ?? S.Main;
            if (active != null && !S.Progress.Dead && !(S.Sub == null && S.Main!.Status == ActionStatus.Paused))
                _ = RunLoop();
            // 战斗中途读档：恢复覆盖层与队列游标，敌方未行动完的从游标继续（不会重复行动）
            if (S.Battle != null && !S.Progress.Dead && !E.AwaitingPlayer(S)) _ = BattleLoop();
            else if (S.Battle != null) { AutoTarget(); DetailTab = DetailTab.Object; }
            return null;
        }
        catch (SaveMigrationConflictException ex)
        {
            State = null;
            return ConflictText(ex);
        }
        catch (SaveVersionException ex)
        {
            return ex.Message + "，请开始新游戏。";
        }
        catch (Exception ex)
        {
            return "存档损坏：" + ex.Message;
        }
    }

    private static string ConflictText(SaveMigrationConflictException ex) =>
        $"旧存档无法自动升级到版本{GameState.CurrentSchema}：{ex.Message}。原存档没有改动，并已另存一份备份；请开始新游戏。";

    /// <summary>现有存档是旧版、继续时会升级：返回它的版本号（开始界面预告），否则为 null。</summary>
    public string? SaveUpgradeFrom()
    {
        try
        {
            var json = _store.Load();
            if (string.IsNullOrEmpty(json) || !SaveSerializer.AnnouncesUpgrade(json)) return null;
            return SaveSerializer.SchemaVersionOf(json);
        }
        catch { return null; }
    }

    /// <summary>检查现有存档能否读取；不能时返回原因（用于禁用“继续”）。</summary>
    public string? SaveProblem()
    {
        try
        {
            var json = _store.Load();
            if (string.IsNullOrEmpty(json)) return "没有存档";
            try
            {
                SaveSerializer.Deserialize(json, Config);
            }
            catch (SaveMigrationConflictException ex)
            {
                // 冲突档：原档不覆盖，备份照常写一份，开始界面只允许新游戏
                try { _store.Backup(json); } catch { /* 备份失败不影响提示 */ }
                return ConflictText(ex);
            }
            return null;
        }
        catch (SaveVersionException ex) { return ex.Message + "，请开始新游戏。"; }
        catch (Exception ex) { return "存档损坏：" + ex.Message; }
    }

    public void ClearSave()
    {
        _store.Clear();
        Notify();
    }

    public void BackToTitle()
    {
        State = null;
        ResetUi();
        Notify();
    }

    private void ResetUi()
    {
        DeathLogOpen = false;
        Recent.Clear();
        Toast = null;
        Highlights.Clear();
        HeldHighlights.Clear();
        HeldHighlightAt = null;
        SevenDayPending = false;
        TaskDoneSeen = null;
        Notice = null;
        Selected = new Selection();
        DetailTab = DetailTab.Character;
        Panel = null;
        MenuOpen = false;
        HoverPreview = null;
        IgniteMenuOpen = false;
        Pop = null;
        FeedPending = null;
        PartyItemsOpen = null;
        HelpFromFeed = false;
        CharPage = null;
        StatusHover = null;
        StatusPinned = null;
        HelpTopic = null;
        HelpEntry = null;
        DialogLogPick = null;
        _uiLocation.Reset();
        _skip = false;
        Running = false;
        ResetBattleUi();
        BattleBusy = false;
    }

    public void Save()
    {
        if (State == null) return;
        try
        {
            SaveState = SaveStatus.Saving;
            _store.Save(SaveSerializer.Serialize(State));
            SaveState = SaveStatus.Saved;
            SaveError = null;
            SaveErrorQuota = false;
        }
        catch (Exception ex)
        {
            // Q282：按浏览器存储异常类别识别配额用尽（saveSet 回传的类别名；JSInterop 直接抛出时看异常信息里的类别名）
            SaveState = SaveStatus.Failed;
            SaveError = ex.Message;
            SaveErrorQuota = SaveWriteError.IsQuotaExceeded((ex as SaveWriteException)?.Category, ex.Message);
        }
    }

    public void RetrySave()
    {
        Save();
        Notify();
    }

    // ───────────── 选择 ─────────────

    public void Select(Selection sel)
    {
        if (sel.Key != Selected.Key) { SelectedBatchKey = null; FeedPending = null; }
        Selected = sel;
        Pop = null;
        if (sel.Kind != SelectionKind.None) DetailTab = DetailTab.Object;
        Notice = null;
        // 选中卡片后卡下会出现操作条：旧的结果提示收起，不遮挡操作（试玩修复 G5）；内容仍在页脚与行动日志里
        // B31c（Q330）：选中只是浏览，「危险」「行动中止」等关键提示保留，一般提示照原规则收起
        if (sel.Kind != SelectionKind.None) SetToast(ResultToast.OnBrowse(Toast));
        // B13 PRO01：第一次实际选中地面/随身物资卡（卡下展开操作预览）记“看清眼前”；只写任务账，不耗时
        // 试玩修复 M503：鸟瞰浏览中第四行只看，点卡不算任务进度
        if (State != null && !CityBrowsing && (sel.Kind is SelectionKind.ItemGroup or SelectionKind.Tool or SelectionKind.PotLiquid) && !GameEngine.TaskDone(S, "PRO01"))
        {
            E.NoteObjectInspected(S);
            if (GameEngine.TaskDone(S, "PRO01")) { Drain(); Save(); }
        }
        // B13d（Q128）：选中卡片是操作前引导的上下文（物资、火堆、资源点、地点；同次选卡可接着给 PRO01 之后的一项）
        if (State != null)
        {
            switch (sel.Kind)
            {
                case SelectionKind.ItemGroup:
                case SelectionKind.Tool:
                case SelectionKind.PotLiquid:
                    Guide("item", sel.Id, sel.Owner);
                    break;
                case SelectionKind.Facility:
                    if (sel.Id == GameEngine.FireIdOf(S.Ground)) Guide("fire");
                    break;
                case SelectionKind.Resource:
                    Guide("resource", sel.Id);
                    break;
                case SelectionKind.Location:
                case SelectionKind.Entrance:
                    Guide("location", sel.Id);
                    break;
            }
        }
        Notify();
    }

    /// <summary>
    /// 选中物品卡时玩家明确选的真实批次（Q24：可展开选批次，默认指向最前合法可用批次）；null = 默认批次。只在会话内，换选中对象即清空；
    /// 所选批次不存在了（用完、转走、腐败后被丢弃）也回到默认批次。
    /// </summary>
    public string? SelectedBatchKey { get; private set; }

    /// <summary>选中某处某物品的某一批（叠卡后层、操作窗批次条、右栏批次按钮共用）；先选中该物品卡再记批次。</summary>
    public void PickBatch(string owner, string itemId, string? batchKey)
    {
        var sel = new Selection { Kind = SelectionKind.ItemGroup, Id = itemId, Owner = owner };
        if (sel.Key != Selected.Key) Select(sel);
        SelectedBatchKey = batchKey;
        Notify();
    }

    /// <summary>当前对这处这种物品生效的批次：玩家选过且仍存在的那一批，否则默认批次（最前合法可用）。</summary>
    public ItemBatch? CurrentBatch(string owner, string itemId, IList<ItemBatch>? batches = null)
    {
        batches ??= E.BatchesOf(S, owner, itemId);
        var picked = Selected.Kind == SelectionKind.ItemGroup && Selected.Owner == owner && Selected.Id == itemId && SelectedBatchKey != null
            ? batches.FirstOrDefault(b => b.Key == SelectedBatchKey) : null;
        return picked ?? GameEngine.DefaultBatch(batches);
    }

    /// <summary>
    /// 程序清空选中（换地点、物品用尽或移走、拖拽预览确认后）：右栏同样回到人物页（v0.7 计划假设 14、试玩修复 B11）；
    /// “未选中对象”只在玩家主动点“对象信息”页签且没有选中时显示。不通知界面，由调用方统一 Notify。
    /// </summary>
    private void ClearSelectionSilently()
    {
        DropCardFeed();
        Selected = new Selection();
        DetailTab = DetailTab.Character;
    }

    /// <summary>Esc 或收起操作条：清空选中，右栏回到默认的人物页。</summary>
    public void ClearSelection()
    {
        DropCardFeed();
        Selected = new Selection();
        Pop = null;
        DetailTab = DetailTab.Character;
        Notify();
    }

    /// <summary>
    /// 选中当前地点的火堆（v0.7 第 3.1 节：点火堆主图只选中火堆并刷新右栏，不再打开内容物层级菜单）。
    /// 从手工制作的“查看火堆”进入时一并收起二级面板，让第二行的火堆卡露出来。
    /// </summary>
    public void SelectFire()
    {
        if (State == null || !E.HasFireAt(S, S.Ground)) return;
        Panel = null;
        HoverPreview = null;
        Select(new Selection { Kind = SelectionKind.Facility, Id = GameEngine.FireIdOf(S.Ground) });
    }

    private int _hoverSeq;

    /// <summary>
    /// 悬停/聚焦动作按钮：右栏临时显示该动作的预测；传 null 清空。
    /// 清空延后约 150 毫秒（期间又悬停到别的按钮就取消）：指针离开按钮后立刻在右栏点一下时，
    /// 若右栏在这一瞬间整块换掉，浏览器会把这次按下派给已被移除的节点，点击丢失（试玩修复 B3：悬停过工具后第一次点菜单外不关闭）。
    /// </summary>
    public void Hover(ActionRequest? r, string title = "")
    {
        if (r == null)
        {
            if (HoverPreview == null) return;
            var seq = ++_hoverSeq;
            _ = ClearHoverLater(seq);
            return;
        }
        _hoverSeq++;
        if (ReferenceEquals(HoverPreview, r) && HoverTitle == title) return;
        HoverPreview = r;
        HoverTitle = title;
        Notify();
    }

    /// <summary>立即清掉悬停预测（行动开始、进入或离开战斗时）：预测对象已失效，不再等鼠标离开。</summary>
    private void ClearHoverNow()
    {
        _hoverSeq++;
        HoverPreview = null;
        HoverTitle = "";
    }

    private async Task ClearHoverLater(int seq)
    {
        await Task.Delay(150);
        if (seq != _hoverSeq || HoverPreview == null) return;
        HoverPreview = null;
        Notify();
    }

    // ───────────── 行动 ─────────────

    private readonly Dictionary<string, (int ver, ActionPreview p)> _previewCache = new();

    public ActionPreview Preview(ActionRequest r)
    {
        var key = r.PreviewKey;   // B13b 试玩修复：键含 BareHands，徒手预估不再读到带工具的缓存
        if (_previewCache.TryGetValue(key, out var c) && c.ver == Version) return c.p;
        var p = E.Preview(S, r);
        _previewCache[key] = (Version, p);
        if (_previewCache.Count > 64) _previewCache.Clear();
        return p;
    }

    /// <summary>
    /// 开始耗时行动。每次提交都带一个请求号（基础UI修订 v0.7 第 8 节第 5 条：玩法请求沿用幂等机制）：
    /// 拖拽松手用起拖时生成的那一个，按钮每次点击新生成；同一请求号重复到达时内核返回 Duplicate，这里不再推进。
    /// </summary>
    public async Task<ActionResult> StartAction(ActionRequest r, string? requestId = null)
    {
        var res = BeginAction(r, requestId);
        if (res.Ok && !res.Duplicate) await RunLoop();
        return res;
    }

    /// <summary>同步部分：校验、开始、存档；不等待行动推进（拖拽松手后立即返回结果给 drag.js）。</summary>
    private ActionResult BeginAction(ActionRequest r, string? requestId)
    {
        if (State == null) return ActionResult.Fail("没有进行中的游戏");
        if (CityBrowsing) return CityReadOnlyFail();
        if (FormationOpen) { Notice = FormationOpenText; Notify(); return ActionResult.Fail(FormationOpenText); }
        var res = E.Start(S, r.WithRequestId(string.IsNullOrEmpty(requestId) ? Guid.NewGuid().ToString("N") : requestId));
        if (res.Duplicate) return res;
        if (!res.Ok)
        {
            Notice = res.Reason;
            Notify();
            return res;
        }
        Notice = null;
        // 就近确认浮窗已完成使命（S14 §14.1：执行后不留下可再次点击的旧按钮）
        Pop = null;
        // 开始新行动：上一条结果提示与悬停预测作废（试玩修复 G5、L6：按钮开始后消失或禁用，收不到离开事件）
        Toast = null;
        ClearHoverNow();
        // 选中的物品被吃完、用尽时清空选中（试玩修复 B6）
        ReconcileSelection();
        // 无选中时右栏回人物页（例如敌踪“绕行”后不停在“未选中对象”）
        if (Selected.Kind == SelectionKind.None) DetailTab = DetailTab.Character;
        // 拖拽打开的预览确认后收起，避免行动结束后仍停在旧预览上
        if (Selected.Kind == SelectionKind.Action)
        {
            ClearSelectionSilently();
            PendingAction = null;
        }
        Drain();
        Save();
        Notify();
        return res;
    }

    /// <summary>旧档随身超过五张卡且安全空闲：显示超额整理窗（S06 §8、开发假设 H3）。</summary>
    public bool OverflowPending => State != null && E.CarriedOverflow(S) && E.OverflowResolvable(S) && !E.DuplicateWeaponsIllegal(S);

    /// <summary>超额整理：保留所选随身物品（不超过上限），其余放到当前地面，确认后保存。</summary>
    public ActionResult ResolveOverflow(IList<int> keep)
    {
        var res = E.ResolveCarriedOverflow(S, keep, Guid.NewGuid().ToString("N"));
        Notice = res.Ok ? null : res.Reason;
        if (res.Ok)
        {
            Drain();
            ReconcileSelection();
            Save();
        }
        Notify();
        return res;
    }

    public ActionResult Instant(InstantRequest r)
    {
        // 每次界面操作带一个请求号：重复提交（连点、重发）只生效一次
        if (string.IsNullOrEmpty(r.RequestId)) r.RequestId = Guid.NewGuid().ToString("N");
        if (CityBrowsing) return CityReadOnlyFail();
        if (FormationOpen) return Commit(ActionResult.Fail(FormationOpenText));
        var res = E.DoInstant(S, r);
        Notice = res.Ok ? null : res.Reason;
        if (res.Ok)
        {
            Drain();
            ReconcileSelection();
            Save();
        }
        Notify();
        return res;
    }

    /// <summary>
    /// 同一批次（同物品、同到期时间、同位置）可能分成多堆；按堆依次拾取或放下指定数量。batchKey 不为空时只取这一真实批次
    /// （到期 + 来源 + 预留状态，Q24 所选批次作用于转移）。
    /// </summary>
    public ActionResult TransferBatch(string owner, string itemId, int? expiresAt, int count, bool toCarried, string? requestId = null, string? batchKey = null)
    {
        if (GameEngine.SeenRequest(S, requestId)) return ActionResult.Success();
        // B31c（Q330）：操作条按钮不带请求号时补一个，成功的整批转移也记进请求号账本（算一次有效提交，清掉关键结果提示）
        requestId ??= Guid.NewGuid().ToString("N");
        if (CityBrowsing) return CityReadOnlyFail();
        var left = count;
        ActionResult last = ActionResult.Success();
        var stacks = S.Items.Where(i => i.Owner == owner && i.ItemId == itemId && i.ExpiresAt == expiresAt && i.ReservedBy == null
                                        && (batchKey == null || GameEngine.BatchKeyOf(i) == batchKey))
            .OrderBy(i => i.InstanceId).ToList();
        // 拾取整批先按最终根卡数与重量预演：会出现第六张卡或超重就整次拒绝，一份都不先拿（S06 §8、开发假设 H2）
        if (toCarried && stacks.Count > 0 && E.PickupBlockedText(S, stacks[0], count) is string blocked)
        {
            Notice = blocked;
            Notify();
            return ActionResult.Fail(blocked);
        }
        foreach (var st in stacks)
        {
            if (left <= 0) break;
            var n = Math.Min(left, st.Count);
            last = E.DoInstant(S, toCarried ? InstantRequest.Pickup(st.InstanceId, n) : InstantRequest.Drop(st.InstanceId, n));
            if (!last.Ok) break;
            left -= n;
        }
        // 整批转移的请求号记在最后，拆成几堆时也只生效一次
        if (last.Ok) GameEngine.RememberRequest(S, requestId);
        Notice = last.Ok ? null : last.Reason;
        Drain();
        ReconcileSelection();
        Save();
        Notify();
        return last;
    }

    /// <summary>
    /// 选中对象随玩法状态校正（UI 案 04；v0.7 第 7.1 节“物品已被移走或转化后，失效操作条关闭或更新到实际状态”）：
    /// 堆叠物品全部拾走或放下 → 清空；实例被移走 → 跟到新位置；生块根烤熟（物品 ID 变化）→ 仍选同一实例；
    /// 锅里液体净化（脏水→净水）→ 跟到新液体，喝完或倒空 → 清空；火堆不在当前地点 → 清空。
    /// </summary>
    private void ReconcileSelection()
    {
        switch (Selected.Kind)
        {
            case SelectionKind.Tool when Selected.InstanceId is int id:
            {
                var it = S.Items.FirstOrDefault(i => i.InstanceId == id);
                if (it == null) { ClearSelectionSilently(); return; }
                if (it.Owner == Selected.Owner && it.ItemId == Selected.Id) return;
                // 设施里的物品取出后改选它的新位置，卡下操作条与右栏跟着走
                Selected = E.Item(it.ItemId).IsStackable && !it.InFacility && !it.InContainer
                    ? new Selection { Kind = SelectionKind.ItemGroup, Id = it.ItemId, Owner = it.Owner }
                    : new Selection { Kind = SelectionKind.Tool, Id = it.ItemId, Owner = it.Owner, InstanceId = it.InstanceId };
                return;
            }
            case SelectionKind.ItemGroup:
            case SelectionKind.Tool:
            {
                var still = S.Items.Any(i => i.Owner == Selected.Owner && i.ItemId == Selected.Id);
                if (!still) ClearSelectionSilently();
                return;
            }
            case SelectionKind.PotLiquid:
            {
                var pot = Selected.InstanceId is int pid ? S.Items.FirstOrDefault(i => i.InstanceId == pid) : null;
                var liquids = pot == null ? new List<ItemInstance>() : E.PotLiquids(S, pot.InstanceId);
                if (liquids.Count == 0) { ClearSelectionSilently(); return; }
                if (liquids.All(l => l.ItemId != Selected.Id))
                    Selected = new Selection { Kind = SelectionKind.PotLiquid, Id = liquids[0].ItemId, Owner = Selected.Owner, InstanceId = Selected.InstanceId };
                return;
            }
            case SelectionKind.Facility:
                if (E.FacilityLocation(Selected.Id) != S.Ground) ClearSelectionSilently();
                return;
            case SelectionKind.Enemy:
                if (!(Selected.InstanceId is int wid && E.VisibleEnemiesAt(S, S.CurrentLocation).Any(w => w.Id == wid))) ClearSelectionSilently();
                return;
        }
    }

    /// <summary>某批次最多能拾取多少：30 公斤上限与随身五张根卡同时校验（锅按锅加内容计、算 1 张；内核同一函数）。</summary>
    public int MaxPickup(string itemId, int available, ItemInstance? inst = null)
    {
        var head = inst ?? GroundHead(itemId);
        return head == null ? 0 : E.MaxPickupBatch(S, head, available);
    }

    /// <summary>想拾取 want 份但超限时的原因（第六张卡 / 超重）；可以拾取时为 null。</summary>
    public string? PickupLimitText(string itemId, int want, ItemInstance? inst = null)
    {
        var head = inst ?? GroundHead(itemId);
        return head == null ? "物品已不在" : E.PickupBlockedText(S, head, Math.Max(1, want));
    }

    /// <summary>当前地面该物品最早到期、未预留的一堆（与转移同一顺序）。</summary>
    private ItemInstance? GroundHead(string itemId) =>
        S.Items.Where(i => i.Owner == S.Ground && i.ItemId == itemId && i.ReservedBy == null)
            .OrderBy(i => i.ExpiresAt ?? int.MaxValue).ThenBy(i => i.InstanceId).FirstOrDefault();

    // ───────────── 拖拽落点（交互与地图扩展 v0.3 · 01 案） ─────────────

    /// <summary>打开拖拽得到的行动预览：右栏显示与按钮相同的预估，玩家按“开始”才计时。</summary>
    public void ShowAction(ActionRequest r, string title)
    {
        PendingAction = r;
        PendingActionTitle = title;
        Panel = null;
        Selected = new Selection { Kind = SelectionKind.Action, Id = r.TargetId, InstanceId = r.InstanceId };
        // 拖工具到资源卡：开始确认放在该资源卡旁的锚定浮窗（S14 §14.1），右栏只作详细预测
        Pop = r.Type == ActionType.Gather ? new PopContext("gather", r.TargetId, "res-" + r.TargetId) : null;
        DetailTab = DetailTab.Object;
        Notice = null;
        Notify();
    }

    /// <summary>从材料、锅或营地地点卡拖拽打开配方候选：只预选，不扣物、不预留。</summary>
    public void ShowRecipes(List<string> recipes, ActionRequest? request)
    {
        OpenPanel("craft", recipes.FirstOrDefault());
        CraftCandidates = recipes;
        CraftPreferred.Clear();
        if (request != null) CraftPreferred.AddRange(request.PreferredInstanceIds);
        CraftPot = request?.PotInstanceId;
        Notify();
    }

    public void AskDropQuantity(DropIntent intent, string requestId)
    {
        PendingDrop = intent;
        _pendingDropRequest = requestId;
        Notify();
    }

    public void ConfirmDropQuantity(int count)
    {
        var intent = PendingDrop;
        PendingDrop = null;
        if (intent != null) ExecuteDrop(intent, count, _pendingDropRequest);
        else Notify();
    }

    public void CancelDropQuantity()
    {
        PendingDrop = null;
        Notify();
    }

    /// <summary>执行松手结果：即时类直接提交（同一请求号只生效一次），预览类打开对应面板。</summary>
    public ActionResult ExecuteDrop(DropIntent intent, int count, string requestId)
    {
        if (State == null) return ActionResult.Fail("没有进行中的游戏");
        if (CityBrowsing) return CityReadOnlyFail();
        if (intent.StartsAction && intent.Action != null)
        {
            // 打火器拖到火堆：松手即开始点火（v0.7 第 4.1 节），请求号沿用起拖时那一个，重复松手不重复点火
            HoverPreview = null;
            var started = BeginAction(intent.Action, requestId);
            if (started.Ok && !started.Duplicate) _ = RunLoop();
            return started;
        }
        if (intent.IsPreview)
        {
            if (intent.Verb == DropVerb.PreviewRecipe) ShowRecipes(intent.Recipes, intent.Action);
            else if (intent.Action != null) ShowAction(intent.Action, intent.Label.Replace("查看", ""));
            return ActionResult.Success();
        }
        var r = intent.Instant!;
        if (intent.Verb == DropVerb.Transfer && intent.Instant!.Kind is InstantKind.Pickup or InstantKind.Drop)
        {
            var head = S.Items.FirstOrDefault(i => i.InstanceId == r.InstanceId);
            if (head == null) return ActionResult.Fail("物品已不在");
            // B22 试玩修复 M409：按被拖那一堆的真实批次键转移（含本实例战利品来源），不再只按到期分组——否则拖战利品卡到空白处/别的卡会搬走同名普通物品
            if (E.Item(head.ItemId).IsStackable)
                return TransferBatch(head.Owner, head.ItemId, head.ExpiresAt, count, r.Kind == InstantKind.Pickup, requestId, GameEngine.BatchKeyOf(head));
        }
        r.Count = Math.Max(1, count);
        if (r.Kind == InstantKind.Reorder) r.Count = 1;
        r.RequestId = requestId;
        var res = Instant(r);
        if (res.Ok) ReconcileSelection();
        return res;
    }

    /// <summary>
    /// 中央推进条“退出”（S01 §9、S14 §11.2）：走内核 RequestExit——内核每次推进都是完整 5 分钟原子段，收到即在段边界停止，
    /// 不再调度下一段；未发生的时长/工作不结算、按各玩法释放预留，已提交的不回滚；子行动退出回原主行动暂停态。
    /// 点火、自用、融合（单段事务）已开始结算：如实提示“本段完成后结束”，不撤销。对话中、战斗中、死亡后拒绝并说明原因。
    /// </summary>
    public void Exit()
    {
        if (State == null) return;
        var res = E.RequestExit(S);
        ExitNote = res.Ok && res.Deferred ? res.Reason : null;
        ExitNoteAction = res.ActionId ?? 0;
        // Q318：事项盘忙碌句不随“退出”一并清掉——退出尚待段边界或只退了补给子行动时按实际占用保留，行动真正停止后由 Notify 清
        if (!res.Ok) Notice = res.Reason;
        else if (!BusyNoticeTracker.IsBusyNotice(Notice)) Notice = null;
        if (res.Ok && !res.Deferred)
        {
            Drain();
            ReconcileSelection();
            Save();
        }
        Notify();
    }

    /// <summary>单段事务点“退出”后的说明（“已开始结算，本段完成后结束”），只对同一行动显示。</summary>
    public string? ExitNote { get; private set; }
    public int ExitNoteAction { get; private set; }

    private (int ver, ActionProgressInfo? info) _progressCache = (-1, null);

    /// <summary>中央推进条数据（内核 ActionProgress，只读估算）；每次界面版本只算一次。</summary>
    public ActionProgressInfo? ProgressInfo()
    {
        if (State == null) return null;
        if (_progressCache.ver == Version) return _progressCache.info;
        var info = E.ActionProgress(S);
        _progressCache = (Version, info);
        return info;
    }

    private (int ver, string loc, CookWaitPlan? plan) _cookCache = (-1, "", null);

    private (int ver, string loc, CharcoalLoadInfo? info) _charcoalCache = (-1, "", null);

    /// <summary>火堆“煅烧木炭”投料说明（Q111）：按钮提示与卡旁确认浮窗共用；每次界面版本只算一次。</summary>
    public CharcoalLoadInfo CharcoalPreview()
    {
        if (_charcoalCache.ver == Version && _charcoalCache.loc == S.Ground && _charcoalCache.info != null) return _charcoalCache.info;
        var info = E.CharcoalLoadPreview(S);
        _charcoalCache = (Version, S.Ground, info);
        return info;
    }

    private (PopContext? pop, CharcoalLoadInfo? info) _charcoalShown;

    /// <summary>
    /// Q113：煅烧浮窗展示给玩家的那份投料明细——浮窗打开时拍下，之后木材变动也不暗改（确认时由内核按这份重验）；
    /// 拍下时还不能投（缺料等）则每次重拍，确认被拒后也重拍，让玩家看着新明细再确认。
    /// </summary>
    public CharcoalLoadInfo CharcoalShown()
    {
        var live = CharcoalPreview();
        if (Pop is not { Kind: "charcoal" } p) return live;
        if (!ReferenceEquals(_charcoalShown.pop, p) || _charcoalShown.info is not { Ok: true }) _charcoalShown = (p, live);
        return _charcoalShown.info!;
    }

    /// <summary>
    /// Q113 浮窗“确认投料”：带上浮窗展示的明细提交；来源有变化时内核整条拒绝，浮窗保持打开、刷新明细并显示原因，由玩家重新确认。
    /// </summary>
    public ActionResult ConfirmCharcoal()
    {
        var shown = CharcoalShown();
        var res = Instant(InstantRequest.LoadCharcoal(shown.Plan));
        if (!res.Ok && Pop is { Kind: "charcoal" } p)
        {
            _charcoalShown = (p, E.CharcoalLoadPreview(S));
            Notify();
        }
        return res;
    }

    /// <summary>当前地点火堆“等待加工”（原等待烹饪，Q111 起含煅烧）的只读解析（S11 §9）：悬停预览与按钮禁用原因共用；每次界面版本只算一次。</summary>
    public CookWaitPlan CookWaitPreview()
    {
        if (_cookCache.ver == Version && _cookCache.loc == S.Ground && _cookCache.plan != null) return _cookCache.plan;
        var plan = E.PreviewCookWait(S);
        _cookCache = (Version, S.Ground, plan);
        return plan;
    }

    public async Task Resume()
    {
        var res = E.Resume(S);
        if (!res.Ok)
        {
            Notice = res.Reason;
            Notify();
            return;
        }
        Save();
        Notify();
        await RunLoop();
    }

    public void SkipAnimation()
    {
        _skip = true;
    }

    /// <summary>
    /// 两段之间的动画间隔：一般为 SegmentDelayMs；撤出（Q202 ④）按全程段数摊开，保证整条撤出至少约 <see cref="ExtractMinVisibleMs"/> 毫秒可见。
    /// </summary>
    private int SegmentDelay(ActionState? a)
    {
        if (a?.Type != ActionType.Extract || S.Expedition is not { } ex) return SegmentDelayMs;
        var segs = Math.Max(1, (int)Math.Ceiling(ex.TotalMinutes / 5.0));
        return Math.Max(SegmentDelayMs, (int)Math.Ceiling(ExtractMinVisibleMs / (double)segs));
    }

    /// <summary>连续推进 5 分钟段，直到行动结束、暂停或死亡。浏览与整理时不推进。</summary>
    private async Task RunLoop()
    {
        if (Running) return;
        Running = true;
        _skip = false;
        var segs = 0;
        try
        {
            while (State != null && !S.Progress.Dead)
            {
                var a = S.Sub ?? S.Main;
                if (a == null) break;
                if (S.Sub == null && S.Main!.Status == ActionStatus.Paused) break;

                var r = E.Advance(S);
                Drain();
                segs++;
                if (!_skip)
                {
                    Save();
                    Notify();
                }
                // 跳过动画时每 4 段刷新一次中央条（剩余时间是游戏分钟，由内核按真实状态给出）
                else if (segs % 4 == 0) Notify();
                if (r.Status is SegmentStatus.Paused or SegmentStatus.Dead or SegmentStatus.Idle or SegmentStatus.Dialogue) break;
                if (S.Main == null && S.Sub == null) break;
                // 任何速度都给“退出”留出响应机会（S01 §9、S14 §11.2）：跳过动画时每段也让出线程，不一次同步把未来整段时间算完
                await Task.Delay(_skip ? 1 : SegmentDelay(S.Sub ?? S.Main));
            }
        }
        finally
        {
            Running = false;
            _skip = false;
            // 到达新地点后旧右栏选中（含随身卡）与只依附它的临时状态由 Notify 里的 ResetOnLocationChange 统一清（Q281）
            if (State != null) ReconcileSelection();
            Save();
            Notify();
        }
        // 移动完成后由剧情首战直接进入战斗（S15 §5）：清掉地块上的选择与旧提示，接上战斗流程
        if (State != null && S.Battle != null && !S.Progress.Dead && !BattleBusy)
        {
            ResetBattleUi();
            Pop = null;
            Selected = new Selection();
            DetailTab = DetailTab.Object;
            ClearHoverNow();
            Notify();
            await BattleLoop();
        }
    }

    /// <summary>
    /// 对白“继续/结束对话”：只推进对话游标并存档，不推进时间（N01 §6、S01 §8）。对话结束、输入锁解除后，
    /// 原来在安全边界等待的行动或战斗从下一个合法推进点继续（原暂停态仍暂停，不补 tick）。
    /// </summary>
    public void AckDialogue(string id)
    {
        if (State == null) return;
        E.AckDialogue(S, id);
        Drain();
        Save();
        Notify();
        if (E.DialogueLocked(S) || S.Progress.Dead) return;
        if (S.Battle != null)
        {
            if (!E.AwaitingPlayer(S)) _ = BattleLoop();
            else { AutoTarget(); Notify(); }
        }
        else if (S.Sub != null || (S.Main != null && S.Main.Status == ActionStatus.Running)) _ = RunLoop();
    }

    private int _toastSeq;

    /// <summary>
    /// 一般结果提示显示约 8 秒后自动收起（界面节奏，非玩法数值；阅读停留不乘动画速度）；同一条内容仍留在页脚与行动日志（试玩修复 G5）。
    /// B31c：Q330 关键提示（危险、行动中止）不自动收起；收起前先淡出 140ms×动画速度（R114 toast 退场）。
    /// </summary>
    private async Task AutoDismissToast(int seq)
    {
        await Task.Delay(ResultToast.NormalDismissMs);
        if (seq != _toastSeq || Toast == null || !ResultToast.AutoDismiss(Toast)) return;
        ToastLeaving = true;
        Notify();
        await Task.Delay(MotionMs(140));
        if (seq != _toastSeq) return;
        SetToast(null);
        Notify();
    }

    /// <summary>换结果提示：新的一条重新计时（关键提示不计时）、清掉淡出态。</summary>
    private void SetToast(GameEvent? t)
    {
        if (ReferenceEquals(t, Toast)) return;
        Toast = t;
        ToastLeaving = false;
        _toastSeq++;
        if (ResultToast.AutoDismiss(t)) _ = AutoDismissToast(_toastSeq);
    }

    /// <summary>
    /// B31c（Q330）：内核接受了新的玩法提交（请求号进了 RecentRequestIds）就清掉当前结果提示（含关键提示）。
    /// 浏览、开关菜单、预览、草稿与被拒请求不改标记，不清。新局、读档时只记下标记。
    /// </summary>
    private void SyncCommitMark()
    {
        if (State == null) { _commitMark = null; return; }
        var mark = ResultToast.CommitMark(S);
        if (ResultToast.Committed(_commitMark, mark) && Toast != null) SetToast(null);
        _commitMark = mark;
    }

    private void Drain()
    {
        if (State == null) return;
        // B31c（Q330）：先看有没有新的有效提交（清旧提示），再放这次提交及世界推进带来的新结果
        SyncCommitMark();
        // B18 试玩修复二 M374：离开保持高亮所在的地面（换地点或进出安全屋区）即清掉保持描边与这批高亮，回来不再显示
        if (HeldHighlightAt != null && HeldHighlightAt != S.Ground)
        {
            foreach (var id in HeldHighlights) Highlights.Remove(id);
            HeldHighlights.Clear();
            HeldHighlightAt = null;
        }
        var batch = new List<GameEvent>();
        foreach (var ev in E.DrainEvents(S))
        {
            if (string.IsNullOrEmpty(ev.Text)) continue;
            Recent.Add(ev);
            // 结果提示（档次与 Q330 关键提示保留规则在内核 ResultToast.Pick）：角色行动结果类同批后到顶替先到；来客等非阻断短通知（B13）与
            // 设施完成只在同批没有行动结果时提示；一般提示不顶替尚未处理的危险／行动中止提示
            batch.Add(ev);
            if (ev.Kind == GameEventKind.SevenDay) SevenDayPending = true;
            if (ev.HighlightIds.Count > 0)
            {
                Highlights.Clear();
                foreach (var id in ev.HighlightIds) Highlights.Add(id);
                HeldHighlights.Clear();
                HeldHighlightAt = null;
                if (ev.HoldHighlight)
                {
                    foreach (var id in ev.HighlightIds) HeldHighlights.Add(id);
                    HeldHighlightAt = S.Ground;
                }
            }
        }
        if (Recent.Count > 60) Recent.RemoveRange(0, Recent.Count - 60);
        SetToast(ResultToast.Pick(Toast, batch));
    }

    public void DismissToast()
    {
        SetToast(null);
        Notify();
    }

    /// <summary>
    /// 物品卡上的三角箭头：把该物品转移 1 件到对面（地面→随身为拾取，随身→地面为放下）。
    /// 工具按实例转移；可堆叠物品取最早到期、未预留的一堆。
    /// </summary>
    public ActionResult TransferOne(string owner, string itemId, int? instanceId, string? toZone = null)
    {
        if (State == null) return ActionResult.Fail("没有进行中的游戏");
        if (CityBrowsing) return CityReadOnlyFail();
        var inst = instanceId.HasValue
            ? S.Items.FirstOrDefault(i => i.InstanceId == instanceId.Value && i.Owner == owner)
            : S.Items.Where(i => i.Owner == owner && i.ItemId == itemId && i.ReservedBy == null)
                .OrderBy(i => E.CardSideOf(i) != null)   // B22 M408：普通卡的箭头先转普通物品，战利品卡/已鉴定卡（B22d）的箭头带实例号
                .ThenBy(i => i.ExpiresAt ?? int.MaxValue).ThenBy(i => i.InstanceId).FirstOrDefault();
        if (inst == null) return ActionResult.Fail("没有可转移的物品");
        // 火堆或锅里的物品：明确目标“取下到地面”或“拾取到随身”，与拖出走同一校验（统一卡片案第 5 节）
        if (inst.InFacility || (inst.InContainer && !E.IsLiquid(inst)))
        {
            var id = inst.InstanceId;
            var movedItem = inst.ItemId;
            var zone = toZone ?? S.Ground;
            var wasSelected = Selected.InstanceId == id && Selected.Kind == SelectionKind.Tool;
            // B10b 试玩修复：煅烧批次（木材×4 / 成品木炭×3）取出后不跟到合并堆、不再弹操作窗，免得盖住火堆加工位的“煅烧木炭”入口
            var cp = E.CharcoalProcess;
            var charcoalBatch = cp != null && inst.InFacility && (inst.ItemId == cp.InputId || inst.ItemId == cp.OutputId);
            var res = Instant(InstantRequest.TakeOut(id, zone));
            if (res.Ok && charcoalBatch)
            {
                ClearSelectionSilently();
                Pop = null;
                Notify();
            }
            else if (res.Ok)
            {
                ReconcileSelection();
                // 取出的那份并进了目标区已有的同类堆（实例被合并掉）：选中跟到那一堆，卡下操作条与右栏不落空（试玩修复 B1）
                if (wasSelected && S.Items.All(i => i.InstanceId != id))
                    FollowToGroup(zone, movedItem);
            }
            return res;
        }
        var toCarried = owner != ItemInstance.Carried;
        return Instant(toCarried ? InstantRequest.Pickup(inst.InstanceId, 1) : InstantRequest.Drop(inst.InstanceId, 1));
    }

    /// <summary>被选中的对象并进了某区的同类堆或以新卡出现：改选那一堆；找不到就清空并回人物页。</summary>
    public void FollowToGroup(string zone, string itemId)
    {
        var any = S.Items.FirstOrDefault(i => i.Owner == zone && i.ItemId == itemId);
        if (any == null) ClearSelectionSilently();
        else if (E.Item(itemId).IsStackable)
        {
            Selected = new Selection { Kind = SelectionKind.ItemGroup, Id = itemId, Owner = zone };
            DetailTab = DetailTab.Object;
        }
        else
        {
            Selected = new Selection { Kind = SelectionKind.Tool, Id = itemId, Owner = zone, InstanceId = any.InstanceId };
            DetailTab = DetailTab.Object;
        }
        Notify();
    }

    public void DebugFill()
    {
        E.DebugFillGround(S);
        Save();
        Notify();
    }

    // ───────────── 战斗（人物与战斗 v0.5 §16、v0.6 §07） ─────────────

    /// <summary>战斗或敌踪选择中：顶栏导航、M/C/R/L、二级面板与世界交互锁定。</summary>
    public bool InBattleUi => State != null && (S.Battle != null || S.Encounter != null);

    /// <summary>
    /// 操作台上选中的指令（R03 §7.4、S14 §18.4）：需要目标的技能选中即进入瞄准——合法目标高亮，悬停看右侧预测，点击合法目标即提交；
    /// 自用/无目标指令选中后在操作台就近点“确认”提交。
    /// </summary>
    public string? BattleAction { get; private set; }
    /// <summary>选中查看的敌人（本场序号）：未瞄准时点敌卡只选中查看，不攻击；单敌自动选中也只是查看。</summary>
    public int? BattleTarget { get; private set; }
    /// <summary>鼠标正悬停的敌人：右栏对象信息临时显示它（瞄准中显示对它的实际预测），移开恢复。</summary>
    public int? BattleHover { get; private set; }
    /// <summary>用药指定的实例。</summary>
    public int? BattleItem { get; private set; }
    /// <summary>敌方行动逐条展示中（锁定输入）。</summary>
    public bool BattleBusy { get; private set; }
    /// <summary>战斗菜单（Esc）：只含继续、保存并返回标题，不含脱战。</summary>
    public bool BattleMenuOpen { get; private set; }

    public void ToggleBattleMenu(bool? open = null)
    {
        BattleMenuOpen = open ?? !BattleMenuOpen;
        if (BattleMenuOpen) MedTrayOpen = false;
        Notify();
    }

    /// <summary>
    /// 全屏四对四战斗（A10 §1.2/§2.2，批次 B9）：战斗中（不含敌踪选择）用覆盖整个应用内容区的战斗视图，
    /// 外层顶栏、左右栏、地面/随身与底栏留在下面但不可操作（inert），不随战斗改写；战后原样露出。
    /// </summary>
    public bool BattleFullscreen => State != null && S.Battle != null;

    /// <summary>
    /// 战斗内的二级阅读抽屉（A10 §5、§6.3）：supplies 物资查看（只读五卡与容器内容）、log 完整战斗记录、equip 七槽装备查看（只读）、help 系统说明。
    /// 同一时刻只开一个；打开不暂停也不推进世界时间，不提交任何指令。
    /// </summary>
    public string? BattleDrawer { get; private set; }

    /// <summary>快捷医疗锚定小盘（A10 §5）：列出绷带/抗生素/特效药的真实随身数量、成本与不可用原因；选药后照常就近确认。</summary>
    public bool MedTrayOpen { get; private set; }

    /// <summary>全屏战斗里点我方锚点/队列头像：右侧只查看这名我方的状态（A10 §4.1：不跳过队列、不替换当前行动者）。</summary>
    public string? BattleAllyView { get; private set; }

    public void ViewBattleAlly(string? actorId)
    {
        if (State == null || S.Battle == null) return;
        BattleAllyView = BattleAllyView == actorId ? null : actorId;
        Notice = null;   // 试玩修复 B9-3；B9 遗留 3：瞄准中点队列/锚点换查看对象也清掉旧“射程不足”
        if (BattleAllyView != null) BattleTarget = null;
        Notify();
    }

    /// <summary>
    /// 敌方逐条行动展示中“刚刚出手的那一只”（队列编码；B9 遗留 1）：敌人一步结算完游标已指向下一项，顶条/底栏按它点名，
    /// 不再整段只写“敌方行动中”。轮末与轮到我方时清空。
    /// </summary>
    public int? BattleShownActor { get; private set; }

    /// <summary>B9a：以友方为目标的技能（TargetMode "ally"）当前选中的友方目标（点我方锚点即提交）；整队（ally_pair）时为第一目标 A。</summary>
    public string? BattleAllyTarget { get; private set; }
    /// <summary>整队（Q105，TargetMode "ally_pair"）的第二目标 B 与 A/B 选中时的号位快照（提交时内核按快照重验）。</summary>
    public string? BattleAllyTarget2 { get; private set; }
    private int? _allyTargetPos;
    private int? _allyTarget2Pos;

    /// <summary>所选指令要点我方成员当目标（多人时的指挥/医疗/保护，B9a；整队两目标，Q105）。</summary>
    public bool BattleAllyAiming => State != null && S.Battle != null && BattleAction != null && AllyOption() != null;

    /// <summary>所选指令是整队（先点友方 A、再点与 A 相邻的友方 B、再确认；两次点选只是草稿，不提交）。</summary>
    public bool BattlePairAiming => BattleAllyAiming && AllyOption()!.TargetMode == "ally_pair";

    private BattleCommandOption? AllyOption() =>
        E.BattleOptions(S).FirstOrDefault(x => x.Action == BattleAction && x.TargetMode is "ally" or "ally_pair" && (BattleItem == null || x.ItemInstanceId == BattleItem));

    /// <summary>瞄准友方时可点的我方 actorId（整队：第一步为可作 A 的成员，第二步只亮 A 的真实相邻友方，选齐后不再亮）。</summary>
    public IReadOnlyCollection<string> AllyAimTargets()
    {
        if (!BattleAllyAiming) return Array.Empty<string>();
        var o = AllyOption()!;
        if (o.TargetMode != "ally_pair") return o.AllyTargets;
        if (BattleAllyTarget == null) return o.AllyTargets;
        if (BattleAllyTarget2 != null) return Array.Empty<string>();
        return GameEngine.ReorderPartners(S.Battle!, BattleAllyTarget)
            .Where(x => E.CommandBlocked(S, new BattleCommand { Action = BattleAction!, AllyTarget = BattleAllyTarget, AllyTarget2 = x }) == null).ToList();
    }

    private void ClearPairDraft()
    {
        BattleAllyTarget = null;
        BattleAllyTarget2 = null;
        _allyTargetPos = null;
        _allyTarget2Pos = null;
    }

    /// <summary>
    /// 整队两步点选（Q105，S14/A10 本轮章）：第一次点合法的 A，第二次点 A 的相邻友方 B；再点已选的人取消这一步；选齐后由底部“确认”提交。
    /// 只改草稿，不扣费、不抽随机、不改站位。
    /// </summary>
    private void ClickPairAlly(string actorId)
    {
        var b = S.Battle!;
        Notice = null;
        if (BattleAllyTarget2 == actorId) { BattleAllyTarget2 = null; _allyTarget2Pos = null; Notify(); return; }
        if (BattleAllyTarget == actorId) { ClearPairDraft(); Notify(); return; }
        if (BattleAllyTarget == null || BattleAllyTarget2 != null)
        {
            if (!AllyOption()!.AllyTargets.Contains(actorId))
            {
                Notice = E.CommandBlocked(S, new BattleCommand { Action = BattleAction!, AllyTarget = actorId }) ?? "这名成员不能作为整队的第一个目标";
                Notify();
                return;
            }
            ClearPairDraft();
            BattleAllyTarget = actorId;
            _allyTargetPos = GameEngine.AllyPos(b, actorId);
            Notify();
            return;
        }
        if (!AllyAimTargets().Contains(actorId))
        {
            Notice = E.CommandBlocked(S, new BattleCommand { Action = BattleAction!, AllyTarget = BattleAllyTarget, AllyTarget2 = actorId }) ?? "这名成员不能和所选的人交换";
            Notify();
            return;
        }
        BattleAllyTarget2 = actorId;
        _allyTarget2Pos = GameEngine.AllyPos(b, actorId);
        Notify();
    }

    /// <summary>点我方锚点：瞄准友方时点合法目标即提交（一次 requestId）；点非法目标写原因；整队只记两步草稿；未瞄准只查看。</summary>
    public async Task ClickBattleAlly(string actorId, string token)
    {
        if (State == null || S.Battle == null || BattleBusy) return;
        if (!BattleAllyAiming) { ViewBattleAlly(actorId); return; }
        if (BattlePairAiming) { ClickPairAlly(actorId); return; }
        if (!AllyAimTargets().Contains(actorId))
        {
            Notice = E.CommandBlocked(S, new BattleCommand { Action = BattleAction!, ItemInstanceId = BattleItem, AllyTarget = actorId }) ?? "这名成员不是这个技能的目标";
            Notify();
            return;
        }
        BattleAllyTarget = actorId;
        await ExecuteBattleCommand(token);
    }

    public void OpenBattleDrawer(string? drawer)
    {
        if (State == null || S.Battle == null) return;
        if (drawer != null && DialogueLocked) return;
        BattleDrawer = BattleDrawer == drawer ? null : drawer;
        if (BattleDrawer == "help") { HelpTopic = null; HelpEntry = null; _helpDirect = false; _helpReturn = null; _helpReturnStatus = null; }
        MedTrayOpen = false;
        Notify();
    }

    public void ToggleMedTray(bool? open = null)
    {
        if (State == null || S.Battle == null) return;
        MedTrayOpen = open ?? !MedTrayOpen;
        Notify();
    }

    /// <summary>
    /// 全屏战斗的 Esc（A10 §6.3）：每次只关最高可退层——战斗菜单（已开则回战场）→ 数量框 → 医疗小盘/就近浮窗 → 固定的状态说明 →
    /// 物资/战报/装备/说明抽屉（说明内先逐级返回）→ 取消瞄准 → 清查看对象 → 打开战斗菜单。不关闭战斗、不脱战、不提交指令。
    /// </summary>
    /// <summary>
    /// B31b（Q322）：全屏战斗里还有看得见、可关闭的子层。只是悬停的预测（<see cref="BattleHover"/>）与外层残留选中在战场上看不见，
    /// 不算子层，不吃掉一次 Esc（修 B31a 线上遗留「战斗中 Esc 有时要按两次」）。
    /// </summary>
    public bool BattleHasEscLayer =>
        State != null && S.Battle != null
        // B31b 试玩修复（M614）：右栏查看敌人（含执行指令后留下的目标、没选技能直接点敌卡）与查看队友
        // 只是右栏对象详情，不是带关闭入口的子层，不吃 Esc；开关菜单后原样保留
        && (PendingDrop != null || MedTrayOpen || Pop != null || StatusPinned != null || BattleDrawer != null || BattleAction != null);

    public void BattleEscape()
    {
        if (State == null || S.Battle == null) return;
        if (BattleMenuOpen) { ToggleBattleMenu(false); return; }
        if (PendingDrop != null) { CancelDropQuantity(); return; }
        if (MedTrayOpen) { ToggleMedTray(false); return; }
        if (Pop != null) { ClosePop(); return; }
        if (StatusPinned != null) { PinStatus(null); return; }
        if (BattleDrawer == "help") { HelpBack(); return; }
        if (BattleDrawer != null) { OpenBattleDrawer(null); return; }
        if (BattleAction != null) { ClearBattleAction(); return; }
        if (BattleTarget != null || BattleAllyView != null)
        {
            BattleTarget = null;
            BattleAllyView = null;
            BattleHover = null;
            Notice = null;
            ClearSelectionSilently();
            Notify();
            return;
        }
        ToggleBattleMenu(true);
    }

    /// <summary>快捷键落在灰卡上时，下一次渲染要把键盘焦点移到的技能节点；BattleScreen 取走后清空。</summary>
    public string? BattleFocusNode { get; set; }

    /// <summary>
    /// 全屏战斗快捷键 1—5（A10 §6.4）：只选本页对应 1/3/5/7/10 级的技能位（进入瞄准或待确认），不直接攻击；
    /// 该位未解锁、是被动或不可用时只写原因。
    /// </summary>
    public void BattleHotkeySkill(int slot)
    {
        if (State == null || S.Battle == null || DialogueLocked || BattleMenuOpen || slot < 1 || slot > 5) return;
        var lv = new[] { 1, 3, 5, 7, 10 }[slot - 1];
        var w = E.CurrentWeapon(S, out _);
        var node = E.Config.SkillNodes.FirstOrDefault(n => n.SkillId == w.SkillId && n.Level == lv && !n.IsSurvival);
        var o = node == null ? null : E.BattleOptions(S).FirstOrDefault(x => x.NodeId == node.Id);
        if (node != null && !E.SkillUnlocked(S, node)) { BattleFocusNode = node.Id; ExplainBlocked(node.Name, E.SkillLockedText(S, node)); return; }   // Q227：灰卡只写条件，焦点移到这张卡
        if (o == null) { Notice = node != null ? $"{node.Name}是被动，自动生效" : $"本页第{slot}个技能位（{lv}级）还没解锁"; Notify(); return; }
        if (o.Available) PickBattleAction(o.Action, o.ItemInstanceId);
        else ExplainBlocked(o.Label, o.Reason);
    }

    /// <summary>←/→：瞄准中在合法目标间按号位移动预测（只悬停，不提交）。</summary>
    public void BattleCycleTarget(int dir)
    {
        if (State == null || S.Battle == null || !BattleAiming || DialogueLocked) return;
        var list = AimTargets().Select(i => S.Battle.Enemies.First(x => x.Index == i)).OrderBy(x => x.Pos).Select(x => x.Index).ToList();
        if (list.Count == 0) return;
        var at = BattleHover is int h ? list.IndexOf(h) : -1;
        var next = at < 0 ? (dir > 0 ? 0 : list.Count - 1) : (at + dir + list.Count) % list.Count;
        HoverBattleTarget(list[next]);
    }

    /// <summary>
    /// 回车：瞄准中提交当前预测的合法目标；选了自用/无目标指令（防御、架矛、调息、结束行动……）时等同点底部“确认”。
    /// 与点击同一路径、同一请求号去重（试玩修复 B9-7）。
    /// </summary>
    public async Task BattleConfirmHover()
    {
        if (State == null || S.Battle == null || BattleAction == null || BattleBusy || DialogueLocked || BattleMenuOpen || !E.AwaitingPlayer(S)) return;
        if (BattleAiming)
        {
            if (BattleHover is int h && AimTargets().Contains(h)) await ClickBattleTarget(h, BattleExecToken());
            return;
        }
        if (E.PreviewCommand(S, CurrentBattleCommand()).Ok) await ExecuteBattleCommand(BattleExecToken());
    }

    /// <summary>说明抽屉、战斗菜单打开时是阅读暂停（A10 §6.3）：逐条展示的敌方行动等它们关上再继续，不额外推进。</summary>
    private bool BattleReadingPause => BattleMenuOpen || BattleDrawer == "help";

    private void ResetBattleUi()
    {
        BattleAction = null;
        BattleTarget = null;
        BattleHover = null;
        BattleItem = null;
        BattleMenuOpen = false;
        BattleDrawer = null;
        MedTrayOpen = false;
        BattleAllyView = null;
        ClearPairDraft();
        BattleShownActor = null;
    }

    /// <summary>当前所选指令（B9a：带下指令的成员 = 正在激活的那一名；友方目标技能带所点的我方成员）。</summary>
    public BattleCommand CurrentBattleCommand()
    {
        var c = BattleCommand.Of(BattleAction ?? "", BattleTarget, BattleItem);
        if (S.Battle != null) c.ActorId = GameEngine.ActingAlly(S.Battle);
        if (BattleAllyAiming) c.AllyTarget = BattleAllyTarget ?? (BattlePairAiming ? null : BattleAllyHover);   // B22 M408：预览跟随悬停的友方
        if (BattlePairAiming)
        {
            c.AllyTarget2 = BattleAllyTarget2;
            c.AllyTargetPos = _allyTargetPos;
            c.AllyTarget2Pos = _allyTarget2Pos;
        }
        return c;
    }

    /// <summary>场上只剩一个敌人时自动选中它（试玩修复 D2）；目标已倒下时清掉。</summary>
    private void AutoTarget()
    {
        if (State == null || S.Battle == null) return;
        var alive = S.Battle.Alive.ToList();
        if (BattleTarget != null && alive.All(x => x.Index != BattleTarget)) BattleTarget = null;
        if (BattleTarget == null && alive.Count == 1) BattleTarget = alive[0].Index;
    }

    /// <summary>
    /// 选择指令（R03 §7.4）：只进入瞄准或待确认，不执行、不自动选第一个目标（取代旧“自动改选第一个可选目标后点执行”）；
    /// 再点同一指令收起。先选敌再点技能也只进入瞄准，仍须一次目标点击。
    /// </summary>
    /// <summary>点了不可用的指令（试玩修复 M150 UX5）：不选中、不进入瞄准，只在右栏写原因。</summary>
    public void ExplainBlocked(string label, string? reason)
    {
        if (State == null || S.Battle == null) return;
        ClearBattleAction();
        DetailTab = DetailTab.Object;   // 试玩修复 B7d-6：右栏切回对象信息，原因显示在最上面
        Notice = $"{label}现在不能用：{reason ?? "条件不满足"}";
        Notify();
    }

    public void PickBattleAction(string action, int? item = null)
    {
        if (State == null || S.Battle == null) return;
        if (BattleAction == action && BattleItem == item) { ClearBattleAction(); return; }
        BattleAction = action;
        BattleItem = item;
        ClearPairDraft();
        DetailTab = DetailTab.Object;
        Notice = null;
        Notify();
    }

    /// <summary>当前选中的指令是否处于瞄准（需要点击目标提交）：单体/双位攻击与群攻（霰弹专长射击：点受影响集合中任一目标提交一次）。</summary>
    public bool BattleAiming => State != null && S.Battle != null && BattleAction != null && AimOption() != null;

    private BattleCommandOption? AimOption()
    {
        var o = E.BattleOptions(S).FirstOrDefault(x => x.Action == BattleAction && (BattleItem == null || x.ItemInstanceId == BattleItem));
        return o != null && (o.NeedsTarget || o.TargetMode == "area") ? o : null;
    }

    /// <summary>所选指令是群攻（不带目标提交；B7 霰弹专长射击）。</summary>
    public bool AreaAction => State != null && S.Battle != null && BattleAction != null && E.BattleOptions(S).FirstOrDefault(x => x.Action == BattleAction)?.TargetMode == "area";

    /// <summary>瞄准中可点击提交的目标：单体为合法目标；群攻为预览里真实受影响的集合。</summary>
    public IReadOnlyCollection<int> AimTargets()
    {
        var o = BattleAiming ? AimOption() : null;
        if (o == null) return Array.Empty<int>();
        if (o.TargetMode == "area")
            return E.PreviewCommand(S, BattleCommand.Of(o.Action)) is { Ok: true } p ? p.Targets.Select(t => t.Index).ToList() : new List<int>();
        return o.Targets;
    }

    /// <summary>
    /// 瞄准友方目标时悬停的我方成员（B22 试玩修复 M408：鼓舞提示贴等以友方为目标的指令，右栏预览跟随悬停的那一名；点下去才提交）。
    /// 只在可选目标上设；离开清空。
    /// </summary>
    public string? BattleAllyHover { get; private set; }

    public void HoverBattleAlly(string? actorId)
    {
        if (BattleAllyHover == actorId) return;
        BattleAllyHover = actorId;
        Notify();
    }

    /// <summary>悬停敌卡：右栏对象信息临时显示它；瞄准中显示对它的实际预测（不扣费、不抽随机）。</summary>
    public void HoverBattleTarget(int? index)
    {
        if (BattleHover == index) return;
        BattleHover = index;
        Notify();
    }

    /// <summary>
    /// 点敌卡（R03 §7.4）：未瞄准时只选中查看；瞄准中点合法目标即这条指令的明确提交（一次 requestId，同一画面连点只受理一次）；
    /// 点非法目标不换敌、不执行，只说明原因。
    /// </summary>
    public async Task ClickBattleTarget(int index, string token)
    {
        if (State == null || S.Battle == null || BattleBusy) return;
        if (!BattleAiming)
        {
            PickBattleTarget(index);
            return;
        }
        if (!AimTargets().Contains(index))
        {
            Notice = E.CommandBlocked(S, BattleCommand.Of(BattleAction!, index, BattleItem)) ?? "这个目标不在本指令范围内";
            Notify();
            return;
        }
        if (!AreaAction) BattleTarget = index;
        await ExecuteBattleCommand(token);
    }

    /// <summary>点敌卡（未瞄准）：只选中查看，不攻击。</summary>
    public void PickBattleTarget(int index)
    {
        BattleTarget = index;
        BattleAllyView = null;
        Notice = null;   // 试玩修复 B9-3：换查看对象时不留旧原因（B9 遗留 3：瞄准中点队列里别的对象也清）
        DetailTab = DetailTab.Object;
        Notify();
    }

    /// <summary>右键/Esc：只取消未提交的瞄准或待确认指令，不耗 AP、不脱战。</summary>
    public void ClearBattleAction()
    {
        BattleAction = null;
        BattleItem = null;
        ClearPairDraft();   // Q105：取消瞄准/Esc/切页丢弃未提交的整队草稿
        Notice = null;   // 试玩修复 B9-3：取消瞄准时清掉上一次点非法目标留下的“射程不足”等原因
        Notify();
    }

    /// <summary>
    /// 本地“执行”按钮的请求号（R03 §7.2“一次执行一个 requestId”）：由按钮渲染时所见的战斗状态与所选指令决定——
    /// 同一画面上的连点/双击带同一个号，内核只受理一次；行动点、游标、轮次或指令/目标/物品一变就是新号。
    /// </summary>
    public string BattleExecToken()
    {
        var b = S.Battle;
        return b == null ? "" : $"bt:{b.BattleId}:{b.Round}:{b.Cursor}:{b.ActiveAlly}:{b.PlayerAp}:{b.Log.Count}:{BattleAction}:{BattleTarget}:{BattleItem}:{BattleAllyTarget}:{BattleAllyTarget2}";
    }

    /// <summary>执行所选指令（战斗区内的本地“执行”按钮）：执行期间锁定输入；之后逐条展示敌方行动直到轮到玩家或战斗结束。</summary>
    public async Task ExecuteBattleCommand(string? requestId = null)
    {
        if (State == null || S.Battle == null || BattleBusy || BattleAction == null) return;
        var cmd = CurrentBattleCommand().WithRequestId(string.IsNullOrEmpty(requestId) ? Guid.NewGuid().ToString("N") : requestId);
        BattleBusy = true;
        try
        {
            var res = E.ExecuteCommand(S, cmd);
            Notice = res.Ok ? null : res.Reason;
            // Q105：整队提交时目标或号位已变化——整条拒绝、清掉草稿让玩家重选，不自动改选别人
            if (!res.Ok && cmd.AllyTarget2 != null) ClearPairDraft();
            if (res.Ok && !res.Duplicate)
            {
                // 提交后瞄准收起（R03 §7.4：连点/双击只提交一次，第二击不排队成另一攻击；新目标不复用旧点击）
                BattleAction = null;
                BattleItem = null;
                ClearPairDraft();
                if (S.Battle != null && BattleTarget != null && S.Battle.Enemies.All(e => e.Index != BattleTarget || !e.Alive)) BattleTarget = null;
                AutoTarget();
            }
            if (State != null && S.Battle == null) OnBattleEnded();
            Drain();
            Save();
            Notify();
        }
        finally
        {
            BattleBusy = false;
        }
        await BattleLoop();
    }

    /// <summary>敌踪：接敌。</summary>
    public async Task EngageEncounter()
    {
        if (State == null || S.Encounter == null || BattleBusy) return;
        var res = E.Engage(S, Guid.NewGuid().ToString("N"));
        Notice = res.Ok ? null : res.Reason;
        ResetBattleUi();
        // B31c（Q330）：被拒的接敌请求不清结果提示
        if (res.Ok) Toast = null;
        ClearHoverNow();
        Pop = null;
        Selected = new Selection();
        DetailTab = DetailTab.Object;
        Drain();
        Save();
        Notify();
        await BattleLoop();
    }

    /// <summary>主动接近地块上的敌人卡。</summary>
    public async Task ApproachEnemy(int worldEnemyId)
    {
        if (State == null || BattleBusy) return;
        if (FormationOpen) { Notice = FormationOpenText; Notify(); return; }
        var res = E.Approach(S, worldEnemyId, Guid.NewGuid().ToString("N"));
        Notice = res.Ok ? null : res.Reason;
        if (res.Ok)
        {
            ResetBattleUi();
            Pop = null;
            Selected = new Selection();
            DetailTab = DetailTab.Object;
            // 进入战斗：上一场的“战斗胜利”等旧提示与地块上的悬停预测作废（试玩修复 G5、L6）
            Toast = null;
            ClearHoverNow();
        }
        Drain();
        Save();
        Notify();
        if (res.Ok) await BattleLoop();
    }

    /// <summary>B20：接战当前地点的固定战（地牢房间/检查哨）或旧精英可选挑战；流程同主动接近。</summary>
    public async Task EngageFixed(string? eliteId = null)
    {
        if (State == null || BattleBusy) return;
        if (FormationOpen) { Notice = FormationOpenText; Notify(); return; }
        var res = eliteId == null ? E.EngageFixed(S, Guid.NewGuid().ToString("N")) : E.EngageElite(S, eliteId, Guid.NewGuid().ToString("N"));
        Notice = res.Ok ? null : res.Reason;
        if (res.Ok)
        {
            ResetBattleUi();
            Pop = null;
            Selected = new Selection();
            DetailTab = DetailTab.Object;
            Toast = null;
            ClearHoverNow();
        }
        Drain();
        Save();
        Notify();
        if (res.Ok) await BattleLoop();
    }

    // ───────────── B21 地牢远征（R03 D3/D5、A10 §7） ─────────────

    /// <summary>终止远征确认窗的请求号：打开窗口时生成一次，双击确认只提交一次（R03 D5.1 去重）。</summary>
    private string? _abortRequestId;

    /// <summary>打开“终止本次远征”确认窗（菜单、安全点卡、离开被拒时的按钮）。战斗中不开（只在战斗菜单里解释）。</summary>
    public void OpenAbort()
    {
        if (State == null || S.Battle != null) return;
        _abortRequestId = Guid.NewGuid().ToString("N");
        Notice = null;
        OpenPanel("abort");
    }

    /// <summary>确认终止：一次扣包并开始不可取消的撤出，存档后逐 5 分钟推进（刷新页面从剩余分钟继续）。</summary>
    public async Task ConfirmAbort()
    {
        if (State == null || Running) return;
        if (FormationOpen) { Notice = FormationOpenText; Notify(); return; }
        var res = E.AbortExpedition(S, _abortRequestId ??= Guid.NewGuid().ToString("N"));
        Notice = res.Ok ? null : res.Reason;
        if (res.Ok)
        {
            Panel = null;
            Pop = null;
            Toast = null;
            ClearHoverNow();
            ClearSelectionSilently();
            DetailTab = DetailTab.Character;
        }
        Drain();
        Save();
        Notify();
        if (res.Ok && !res.Duplicate) await RunLoop();
    }

    /// <summary>开发入口（?dev=1）B21 栏。</summary>
    public void DevExpedition(string kind, string? dungeonId = null)
    {
        if (State == null) return;
        Notice = E.DebugExpedition(S, kind, dungeonId);
        Drain();
        ReconcileSelection();
        Save();
        Panel = null;   // 与其他开发按钮一样执行后收起菜单
        Notify();
    }

    // ───────────── B22 封存战利品、远征回收箱与 72 小时重置（R03 D7/D8/D10、A10 §7） ─────────────

    /// <summary>打开“封存战利品”面板（本次远征地牢里的设施行“封存”卡）。</summary>
    public void OpenSeal()
    {
        if (State == null || S.Battle != null) return;
        Notice = null;
        OpenPanel("seal");
    }

    /// <summary>封存一件（整堆）本实例奖励：0 时间，存档。</summary>
    public void SealItem(int instanceId)
    {
        if (State == null) return;
        var r = E.SealReward(S, instanceId, Guid.NewGuid().ToString("N"));
        Notice = r.Ok ? null : r.Reason;
        Drain();
        ReconcileSelection();
        Save();
        Notify();
    }

    /// <summary>从封存里取出一件到随身或当前地面：0 时间，存档。</summary>
    public void UnsealItem(int instanceId, bool toGround)
    {
        if (State == null) return;
        var r = E.UnsealReward(S, instanceId, toGround, Guid.NewGuid().ToString("N"));
        Notice = r.Ok ? null : r.Reason;
        Drain();
        ReconcileSelection();
        Save();
        Notify();
    }

    /// <summary>重置确认窗对应的地牢与请求号（打开时生成一次，双击只生效一次）。</summary>
    public string? ResetDungeonId { get; private set; }
    private string? _resetRequestId;

    /// <summary>开发入口：下一次重置写盘时模拟失败（验证“写盘失败旧代保持”）。只生效一次。</summary>
    public bool DevFailNextResetSave { get; set; }

    /// <summary>打开“重置地牢”确认窗（父地点设施行“地牢重置”卡）：只读预览条件、刷新与不重发、遗留物清单。</summary>
    public void OpenReset(string dungeonId)
    {
        if (State == null || S.Battle != null) return;
        ResetDungeonId = dungeonId;
        _resetRequestId = Guid.NewGuid().ToString("N");
        Notice = null;
        OpenPanel("reset");
    }

    /// <summary>
    /// 确认重置（R03 D10）：内核在同一事务里迁遗留物、关旧代、建新代，随即写盘；写盘失败时整局回到确认前（旧代与全部物品 owner 不变，可再试）。
    /// </summary>
    public void ConfirmReset()
    {
        if (State == null || Running || ResetDungeonId == null) return;
        var fail = DevFailNextResetSave;
        DevFailNextResetSave = false;
        var r = E.ResetDungeonAtomic(S, ResetDungeonId, _resetRequestId ??= Guid.NewGuid().ToString("N"), st =>
        {
            if (fail) return false;
            _store.Save(SaveSerializer.Serialize(st));
            return true;
        }, out var next);
        State = next;
        if (r.Ok && !r.Duplicate)
        {
            SaveState = SaveStatus.Saved;
            SaveError = null;
            SaveErrorQuota = false;
            Panel = null;
            Notice = null;
        }
        else Notice = r.Ok ? null : r.Reason;
        Drain();
        ReconcileSelection();
        Notify();
    }

    /// <summary>B22 试玩修复 M408：要在人物页滚到并高亮的遗物容器（回收箱卡“取出…”设，PartyPanel 渲染后滚动并清空）。</summary>
    public string? FocusRemainsKey { get; set; }

    /// <summary>父地点“回收箱”卡“取出…”：切到人物页并滚到这座地牢的回收箱/遗物箱块。</summary>
    public void ShowDungeonBoxes(string dungeonId)
    {
        if (State == null) return;
        FocusRemainsKey = E.RemainsItems(S, GameEngine.RecoveryBoxKey(dungeonId)).Count > 0 ? GameEngine.RecoveryBoxKey(dungeonId) : GameEngine.RelicBoxKey(dungeonId);
        SetDetailTab(DetailTab.Character);
        Notify();
    }

    /// <summary>开发入口（?dev=1）B22 栏。</summary>
    public void DevRewards(string kind, string dungeonId)
    {
        if (State == null) return;
        Notice = kind switch
        {
            "clear" => E.DebugInstanceFact(S, dungeonId, GameEngine.RecordClaimedFact),
            "ready" => E.DebugResetReady(S, dungeonId),
            "parent" => E.DebugMapTeleport(S, E.DungeonCfg(dungeonId)!.Parent),
            "tactical" => E.DebugGiveTactical(S),
            "show" => E.DebugRewardLedger(S, dungeonId),
            "failsave" => (DevFailNextResetSave = !DevFailNextResetSave) ? "开发：下一次确认重置时模拟写盘失败（只生效一次）" : "开发：已取消模拟写盘失败",
            _ => "开发：未知操作",
        };
        Drain();
        ReconcileSelection();
        Save();
        Panel = null;
        Notify();
    }

    /// <summary>逐条推进敌方行动与轮末，直到轮到玩家或战斗结束；每步存档，刷新页面后从游标继续。</summary>
    public async Task BattleLoop()
    {
        if (State == null || S.Battle == null || BattleBusy) return;
        BattleBusy = true;
        try
        {
            while (State != null && S.Battle != null && !S.Progress.Dead && !E.AwaitingPlayer(S))
            {
                // B9（A10 §6.3）：战斗菜单、系统说明是阅读暂停——逐条展示的下一步敌方行动等它们关上再走，不额外推进
                while (State != null && S.Battle != null && BattleReadingPause) await Task.Delay(120);
                if (State == null || S.Battle == null) break;
                var at = S.Battle.Cursor < S.Battle.Queue.Count ? S.Battle.Queue[S.Battle.Cursor] : (int?)null;
                var r = E.BattleStep(S);
                // B9 遗留 1：敌人这一步结算完游标已经往后走，展示停顿期间按“刚出手的那一只”点名
                BattleShownActor = r.Kind == BattleStepKind.EnemyActed && at > 0 ? at : null;
                Drain();
                Save();
                Notify();
                if (r.Kind is BattleStepKind.None or BattleStepKind.Ended or BattleStepKind.Dialogue) break;
                if (r.Kind is BattleStepKind.EnemyActed or BattleStepKind.RoundEnded)
                    await Task.Delay(EnemyStepDelayMs);
            }
        }
        finally
        {
            BattleBusy = false;
            if (State != null && S.Battle == null) OnBattleEnded();
            else AutoTarget();
            Save();
            Notify();
        }
    }

    /// <summary>战斗结束（胜利、撤退、玩家指令直接结束）后的界面收尾：清指令与目标、悬停预测；没有选中对象时右栏回人物页（与 v0.7 试玩修复 B11 同一规则）。</summary>
    private void OnBattleEnded()
    {
        ResetBattleUi();
        ReconcileSelection();
        ClearHoverNow();
        if (Selected.Kind == SelectionKind.None) DetailTab = DetailTab.Character;
    }

    // ───────────── 七个穿戴槽（S06 全系统更新目标 §1、R02 §1、A05 §5；开发计划 v0.9 B3 H5/H9） ─────────────

    /// <summary>
    /// 把本人随身或当前地面（Q40 选 B）的这件武器装备到武器Ⅰ/武器Ⅱ（0 时间；不合法时内核拒绝并写原因，状态不变）。
    /// oldToGround：目标槽旧件放当前地面（玩家明确选择；默认旧件回本人随身）。
    /// </summary>
    public ActionResult Equip(int instanceId, string slot, bool oldToGround = false) =>
        Commit(E.Equip(S, GameState.PlayerId, instanceId, slot, Guid.NewGuid().ToString("N"), oldToGround));

    /// <summary>卸下某槽：toGround=false 回本人随身（卡位不够时拒绝），toGround=true 是玩家明确选的“放当前地面”。</summary>
    public ActionResult Unequip(string slot, bool toGround) =>
        Commit(E.Unequip(S, GameState.PlayerId, slot, toGround, Guid.NewGuid().ToString("N")));

    // ───────────── B10 整备面板与成长页（A10 §7/§8） ─────────────

    /// <summary>整备/成长页当前查看的角色（只查看，不转移物品）；null 或已不在同行时为主角。</summary>
    public string? GearActor { get; set; }

    public string GearActorId => GearActor != null && State != null && S.Party.Contains(GearActor) ? GearActor : GameState.PlayerId;

    /// <summary>成长页当前选中的熟练门类。</summary>
    public string? GrowthSkill { get; set; }

    /// <summary>为某名同行角色穿上随身或当前地面的装备（0 时间，内核原子校验）。</summary>
    public ActionResult EquipFor(string actorId, int instanceId, string slot, bool oldToGround = false) =>
        Commit(E.Equip(S, actorId, instanceId, slot, Guid.NewGuid().ToString("N"), oldToGround));

    public ActionResult UnequipFor(string actorId, string slot, bool toGround) =>
        Commit(E.Unequip(S, actorId, slot, toGround, Guid.NewGuid().ToString("N")));

    // ───────────── 同行食饮局部盘（B29，P04、Q262—Q265）─────────────

    /// <summary>系统说明里“给同行者吃喝”条目的 id。</summary>
    public const string FeedHelpEntry = "feed-companion";

    /// <summary>未提交的食饮意图：草稿、浮窗锚点、从谁的展开列表打开的。只是界面状态，不进存档。</summary>
    public sealed record FeedContext(FeedDraft Draft, string AnchorId, string? PartyActor);
    public FeedContext? FeedPending { get; private set; }

    /// <summary>队伍面板里展开随身物品的成员，同一时刻最多一人；只是界面状态。</summary>
    public string? PartyItemsOpen { get; private set; }

    /// <summary>系统说明是从食饮盘打开的：返回按钮写“返回食饮”，关闭后回原盘。</summary>
    public bool HelpFromFeed { get; private set; }

    /// <summary>从入口开盘：目标留空，所点实例只作待重验候选。</summary>
    public void OpenFeed(FeedEntryInfo entry, string anchorId, string? partyActor = null)
    {
        if (State == null || DialogueLocked || InBattleUi || entry.Blocked != null) return;
        Pop = null;
        IgniteMenuOpen = false;
        GivePending = null;
        FeedPending = new FeedContext(FeedDraft.From(entry), anchorId, partyActor);
        Notify();
    }

    public void CancelFeed()
    {
        FeedPending = null;
        Notify();
    }

    /// <summary>清掉未提交的食饮意图，由调用方 Notify。</summary>
    public void DropFeed() => FeedPending = null;

    private void DropCardFeed()
    {
        if (FeedPending?.AnchorId == "card-sel") FeedPending = null;
    }

    public void PickFeedTarget(string? actorId)
    {
        if (FeedPending is not { } f) return;
        f.Draft.TargetActorId = string.IsNullOrEmpty(actorId) ? null : actorId;
        f.Draft.SourceCleared = false;
        Notify();
    }

    public void PickFeedSource(string? key)
    {
        if (FeedPending is not { } f) return;
        f.Draft.SourceKey = string.IsNullOrEmpty(key) ? null : key;
        f.Draft.SourceCleared = false;
        Notify();
    }

    /// <summary>确认：带显示时的快照重验，情况变了留在盘里按新状态刷新；通过后收盘提交，走原 Use 事务。</summary>
    public async Task ConfirmFeed(FeedRequest req)
    {
        if (FeedPending == null) return;
        if (E.FeedBlocked(S, req) != null) { Notify(); return; }
        FeedPending = null;
        await StartAction(ActionRequest.FeedCompanion(req));
    }

    /// <summary>局部“查看食饮说明”：定位同一条目，草稿留着，返回后重验。</summary>
    public void OpenFeedHelp()
    {
        if (FeedPending == null || InBattleUi || DialogueLocked) return;
        OpenHelp("body", FeedHelpEntry);
        HelpFromFeed = true;
    }

    /// <summary>切成员或收起列表都清掉未提交意图。</summary>
    public void TogglePartyItems(string actorId)
    {
        PartyItemsOpen = PartyItemsOpen == actorId ? null : actorId;
        if (FeedPending?.PartyActor != null) FeedPending = null;
        Notify();
    }

    /// <summary>战斗、敌踪、对白、离队打断未提交意图；展开的成员不再是在场同行就收起。</summary>
    private void ReconcileFeed()
    {
        if (State == null || (FeedPending == null && PartyItemsOpen == null)) return;
        var targets = E.FeedTargets(S);
        if (PartyItemsOpen != null && targets.All(t => t.ActorId != PartyItemsOpen))
        {
            if (FeedPending?.PartyActor == PartyItemsOpen) FeedPending = null;
            PartyItemsOpen = null;
        }
        if (FeedPending is not { } f) return;
        var left = f.Draft.TargetActorId is string target && targets.All(t => t.ActorId != target);
        if (InBattleUi || DialogueLocked || targets.Count == 0 || left) FeedPending = null;
    }

    /// <summary>待确认的交接（A10 §7.2：先在短浮窗列双方最终重量/卡数，确认再提交；Esc/取消退出）。</summary>
    public GiveDraft? GivePending { get; set; }

    public void AskGive(string fromId, string toId, int instanceId, int count)
    {
        GivePending = new GiveDraft(fromId, toId, instanceId, count);
        Notify();
    }

    public void CancelGive()
    {
        GivePending = null;
        Notify();
    }

    public ActionResult ConfirmGive()
    {
        var d = GivePending;
        GivePending = null;
        if (d == null) return ActionResult.Fail("没有待确认的交接");
        return GiveTo(d.From, d.To, d.InstanceId, d.Count);
    }

    /// <summary>队员交接（A10 §7.2）：0 时间、原子，双方卡数/重量由内核一次校验。</summary>
    public ActionResult GiveTo(string fromId, string toId, int instanceId, int count) =>
        Commit(E.Give(S, fromId, toId, instanceId, count, Guid.NewGuid().ToString("N")));

    /// <summary>旧档非法同类双武器：已在安全空闲，弹整理窗（先于超五卡整理，B3 H8）。</summary>
    public bool DuplicateWeaponsPending => State != null && E.DuplicateWeaponsIllegal(S) && E.OverflowResolvable(S);

    // R90d（B3 线上试玩遗留）：两个整理窗的勾选存在会话里——Esc 打开菜单时整理窗会卸载，关菜单后恢复原选择；换局或读档后作废。
    private GameState? _sortChoicesFor;
    /// <summary>整理同类型武器窗：选中保留的那把。</summary>
    public int? DupKeep { get; set; }
    /// <summary>整理同类型武器窗：其余每把的去向（true=放当前地面）。</summary>
    public Dictionary<int, bool> DupDest { get; } = new();
    /// <summary>超五卡整理窗：勾选留在身上的实例。</summary>
    public List<int> OverflowKeep { get; } = new();

    /// <summary>整理窗渲染前调用：当前局面换了（新局、读档）就清空上一局的勾选。</summary>
    public void SyncSortChoices()
    {
        if (ReferenceEquals(_sortChoicesFor, State)) return;
        _sortChoicesFor = State;
        DupKeep = null;
        DupDest.Clear();
        OverflowKeep.Clear();
    }

    /// <summary>非法同类双武器整理：保留一把，其余按玩家选择进随身或放当前地面。</summary>
    public ActionResult ResolveDuplicateWeapons(int keep, IDictionary<int, bool> toGround) =>
        Commit(E.ResolveDuplicateWeapons(S, GameState.PlayerId, keep, toGround, Guid.NewGuid().ToString("N")));

    // ───────────── B8 队伍与共同生存（R08、A11 §5—§8）：编队、预设、遗物、RB01、报失 ─────────────

    /// <summary>编入同行或留作候补（营区安全空闲，0 时间）。</summary>
    public ActionResult SetActive(string actorId, bool active) =>
        FormationOpen ? Commit(ActionResult.Fail(FormationOpenText)) : Commit(E.SetActive(S, actorId, active, Guid.NewGuid().ToString("N")));

    /// <summary>B22g（Q183）：在安全屋屋内把同行者留守此屋（0 分钟）。</summary>
    public ActionResult StayInSafehouse(string actorId) =>
        FormationOpen ? Commit(ActionResult.Fail(FormationOpenText)) : Commit(E.StayInSafehouse(S, actorId, Guid.NewGuid().ToString("N")));

    // ───────────── Q86 顶栏“编队”（R08/S14 本轮章）：四格草稿 → 保存编队/取消 ─────────────

    /// <summary>编队草稿（4 格，下标=号位−1，空位 ""）；面板没开为 null。只改草稿，不自动保存。</summary>
    public List<string>? FormationDraft { get; private set; }
    /// <summary>打开草稿时的阵容版本（保存时交给内核比对，队伍已变就拒绝）。</summary>
    public int FormationDraftVersion { get; private set; }
    /// <summary>草稿里点选的角色（再点一个号位：空位移入、有人交换）。</summary>
    public string? FormationPick { get; private set; }

    /// <summary>编队按钮禁用原因（空白操作时期之外）；null 为可用。</summary>
    public string? FormationBlocked => State == null ? "没有进行中的游戏" : E.FormationEditBlocked(S);

    /// <summary>草稿是否与已保存的预设不同。</summary>
    public bool FormationDirty => State != null && FormationDraft != null && !FormationDraft.SequenceEqual(S.FormationPreset);

    /// <summary>“保存编队”禁用原因（无改动、草稿过期、不再空闲等，同内核保存校验）；null 为可保存。</summary>
    public string? FormationSaveBlocked => State == null || FormationDraft == null ? "没有编队草稿" : E.SaveFormationBlocked(S, FormationDraft, FormationDraftVersion);

    /// <summary>打开编队面板：按已保存的合法预设建草稿。不在空白操作时期就不开、提示原因。</summary>
    public void OpenFormation()
    {
        if (State == null) return;
        if (E.FormationEditBlocked(S) is string why) { Notice = why; _formationNotice = why; Notify(); return; }
        // 先建草稿再开面板（OpenPanel 末尾的 Notify 会关掉没有草稿的编队面板）
        FormationDraft = E.RepairedFormation(S, S.FormationPreset);
        FormationDraftVersion = S.FormationVersion;
        FormationPick = null;
        OpenPanel("formation");
    }

    /// <summary>取消（含 Esc）：丢弃草稿、关闭面板，已保存的预设不变。</summary>
    public void CancelFormation()
    {
        FormationDraft = null;
        FormationPick = null;
        if (Panel == "formation") OpenPanel(null);
        else Notify();
    }

    /// <summary>点选/取消点选草稿里的一名角色。</summary>
    public void PickFormationActor(string actorId)
    {
        if (FormationDraft == null || !FormationDraft.Contains(actorId)) return;
        FormationPick = FormationPick == actorId ? null : actorId;
        Notify();
    }

    /// <summary>点一个号位（1—4）：已选角色时移入空位或与该格的人交换；没选角色时点有人的格等于选中他。</summary>
    public void PlaceFormationSlot(int slot)
    {
        if (FormationDraft == null || slot < 1 || slot > FormationDraft.Count) return;
        var here = FormationDraft[slot - 1];
        if (FormationPick == null)
        {
            if (here != "") FormationPick = here;
            Notify();
            return;
        }
        var from = FormationDraft.IndexOf(FormationPick);
        if (from >= 0 && from != slot - 1) (FormationDraft[from], FormationDraft[slot - 1]) = (FormationDraft[slot - 1], FormationDraft[from]);
        FormationPick = null;
        Notify();
    }

    /// <summary>保存编队（0 时间/0AP/0E，原子保存）；成功后关闭面板，失败保留草稿并显示原因。</summary>
    public ActionResult SaveFormation()
    {
        if (State == null || FormationDraft == null) return ActionResult.Fail("没有编队草稿");
        var res = Commit(E.SaveFormation(S, FormationDraft.ToList(), FormationDraftVersion, Guid.NewGuid().ToString("N")));
        if (res.Ok) CancelFormation();
        return res;
    }

    /// <summary>编队面板开着时，其他推进、接敌与换队操作一律先拒绝（Q86：面板与推进操作互斥）。</summary>
    private const string FormationOpenText = "先保存或取消编队";
    private bool FormationOpen => Panel == "formation" && FormationDraft != null;

    /// <summary>点灰的“编队”时写进右栏的锁定原因（如“移动中不能编队”）；被别的提示替换后为 null。</summary>
    private string? _formationNotice;

    /// <summary>
    /// 锁定原因只在原因不变时有效（试玩修复 B8a 第 1 轮 B6）：移动结束、进入敌踪/战斗、行动结束等让编队锁定原因改变或解除时，
    /// 清掉这条右栏提示，不挂到下一场战斗；其他操作照常用自己的提示替换它。
    /// </summary>
    private void ClearStaleFormationNotice()
    {
        if (_formationNotice == null) return;
        if (Notice != _formationNotice) { _formationNotice = null; return; }
        if (State != null && E.FormationEditBlocked(S) == _formationNotice) return;
        Notice = null;
        _formationNotice = null;
    }

    /// <summary>编队面板开着但已不在空白操作时期（例如突发对白/强制事件）：关闭并丢弃草稿，先处理事件，不覆盖已保存的预设。</summary>
    private void CloseStaleFormation()
    {
        if (Panel != "formation" || State == null) return;
        if (FormationDraft == null || E.FormationEditBlocked(S) != null)
        {
            FormationDraft = null;
            FormationPick = null;
            Panel = null;
        }
    }

    /// <summary>从遗物取出一件到你的随身或当前地面。</summary>
    public ActionResult TakeRemains(string deadId, int instanceId, bool toGround) =>
        Commit(E.TakeFromRemains(S, deadId, instanceId, toGround, Guid.NewGuid().ToString("N")));

    /// <summary>报失某借用位的借用矛。</summary>
    public ActionResult ReportLostLoan(string slot) =>
        Commit(E.ReportLostLoan(S, slot, Guid.NewGuid().ToString("N")));

    /// <summary>RB01 登记同行补员：30 分钟 wait 主行动，开始后照常推进。</summary>
    public async Task<ActionResult> StartEscortRegistration()
    {
        if (State == null) return ActionResult.Fail("没有进行中的游戏");
        if (FormationOpen) return Commit(ActionResult.Fail(FormationOpenText));
        var res = E.StartEscortRegistration(S, Guid.NewGuid().ToString("N"));
        Notice = res.Ok ? null : res.Reason;
        if (res.Ok) { Drain(); Save(); }
        Notify();
        if (res.Ok && !res.Duplicate) await RunLoop();
        return res;
    }

    /// <summary>开发入口（?dev=1）：执行一条队伍相关的开发操作并存档。</summary>
    public void DevParty(Func<GameState, string> act)
    {
        if (State == null) return;
        Notice = act(S);
        Drain();
        Save();
        Notify();
    }

    private ActionResult Commit(ActionResult res)
    {
        Notice = res.Ok ? null : res.Reason;
        if (res.Ok)
        {
            Drain();
            ReconcileSelection();
            Save();
        }
        Notify();
        return res;
    }

    /// <summary>
    /// 战斗中切换武器页（A10 §4.2：0AP，只换本次可选技能组）：切页取消尚未提交的瞄准与目标确认；随战斗存档。
    /// </summary>
    public void SelectWeaponPage(int page)
    {
        // 敌方行动逐条展示中（BattleBusy）也可切页查看（B3 H6、A10 §4.2“非本人激活可看但不执行”；R90a）：切页只改当前页，不提交指令
        if (State == null || S.Battle == null) return;
        var res = E.SelectWeaponPage(S, page, Guid.NewGuid().ToString("N"));
        Notice = res.Ok ? null : res.Reason;
        if (res.Ok)
        {
            BattleAction = null;
            BattleItem = null;
            BattleHover = null;
            ClearPairDraft();
            Save();
        }
        Notify();
    }
}

/// <summary>一次待确认的队员交接（B10 试玩修复 2）。</summary>
public sealed record GiveDraft(string From, string To, int InstanceId, int Count);
