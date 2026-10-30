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
    public int SegmentDelayMs { get; set; } = 120;

    public List<GameEvent> Recent { get; } = new();
    public GameEvent? Toast { get; private set; }
    public HashSet<string> Highlights { get; } = new();
    public bool SevenDayPending { get; set; }
    public string? Notice { get; set; }

    public SaveStatus SaveState { get; private set; }
    public string? SaveError { get; private set; }

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
        var back = _helpReturn;
        _helpReturn = null;
        Panel = back is "map" or "craft" or "rest" or "log" or "goals" or "tips" or "dialogs" ? back : null;
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
    /// <summary>当前打开的浏览层：map / craft / rest / log / goals / tips / dialogs / help / menu。</summary>
    public string? Panel { get; set; }
    /// <summary>从设施卡打开加工面板时要预选的配方；面板读取一次后清空。</summary>
    public string? CraftRecipe { get; set; }
    /// <summary>从材料拖拽打开加工面板时的候选配方（配置顺序）；为空表示列出全部配方。</summary>
    public List<string>? CraftCandidates { get; set; }
    /// <summary>材料槽里手工指定的批次与指定的锅，随确认一起传给 Craft 请求。</summary>
    public List<int> CraftPreferred { get; } = new();
    public int? CraftPot { get; set; }
    /// <summary>批量制作件数（v0.6 §03）；换配方时回到 1。</summary>
    public int CraftBatch { get; set; } = 1;
    /// <summary>工具维护的目标实例（v0.6 §05）。</summary>
    public int? CraftTarget { get; set; }


    /// <summary>火堆卡“点燃”按钮下的点火工具菜单是否打开（v0.7 第 4.2 节）；只在会话内，Esc 或点菜单外关闭。</summary>
    public bool IgniteMenuOpen { get; private set; }

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
    public string RestKind { get; set; } = "rest";
    public int RestMinutes { get; set; } = 60;

    /// <summary>
    /// 打开或关闭二级面板（地图、加工、休息、日志）。二级面板只覆盖中部上方，
    /// 左侧状态、随身物品与右侧详情保持不动（试玩留言 8f9a7c9b 第 2 点）。
    /// </summary>
    public void OpenPanel(string? panel, string? recipe = null)
    {
        // 战斗与敌踪选择中：地图、制作、休息、日志及顶栏浏览入口锁定（v0.5 §16；生存目标/提示、对话记录、系统说明沿用日志限制，S14 §12.3、§13.3）
        if (IsTopPanel(panel) && InBattleUi) return;
        // 对话读完锁（Q10）：不打开任何面板；关闭（null）仍允许，便于对话出现时收起菜单
        if (panel != null && DialogueLocked) return;
        HoverPreview = null;
        Pop = null;
        // 只有地图/制作/休息会在右栏显示面板自己的预估，切到“对象信息”；顶栏浏览层（日志、目标、提示、对话记录、说明）不改右栏页签
        if (panel != null) { IgniteMenuOpen = false; if (panel is "map" or "craft" or "rest") DetailTab = DetailTab.Object; }
        if (panel == "help") { HelpTopic = null; HelpEntry = null; _helpDirect = false; _helpReturn = null; _helpReturnStatus = null; }
        if (panel == "dialogs") DialogLogPick = null;
        // 关闭面板时没有选中对象就回人物页（v0.7 6.1：无选中默认展示人物信息；复验新发现 2）。
        // 试玩修复 M77-B8：原来这句 else 挂在 dialogs 判断上，打开休息/制作/地图也会被切回人物页，右栏看不到“详细预估”
        if (panel == null && Selected.Kind == SelectionKind.None) DetailTab = DetailTab.Character;
        // 每次打开或关闭面板都清掉上一次拖拽留下的候选与指定批次；拖拽入口在打开后再写入
        CraftCandidates = null;
        CraftPreferred.Clear();
        CraftPot = null;
        CraftBatch = 1;
        CraftTarget = null;
        if (panel == "craft" && State != null)
        {
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
        if (panel == "map") MapPicked = null;
        Panel = panel;
        Notify();
    }

    /// <summary>替换中部上方的顶栏面板：地图、制作、休息、日志，以及生存目标、生存提示、对话记录、系统说明。</summary>
    public static bool IsTopPanel(string? panel) =>
        panel is "map" or "craft" or "rest" or "log" or "goals" or "tips" or "dialogs" or "help";

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
        OnDialogueOnset();
        Version++;
        Changed?.Invoke();
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
            if (Panel == "menu") Panel = null;
            BattleMenuOpen = false;
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
            ResetUi();
            foreach (var l in S.Log.TakeLast(8))
                Recent.Add(new GameEvent { Minute = l.Minute, Text = l.Text, Kind = GameEventKind.Info });
            if (migrate)
            {
                Save();
                Notice = $"旧存档已升级到版本{GameState.CurrentSchema}，原档已备份" + (notes.Count > 0 ? "：" + string.Join("；", notes) : "") + "。";
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
            if (string.IsNullOrEmpty(json) || !SaveSerializer.NeedsMigration(json)) return null;
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
        Recent.Clear();
        Toast = null;
        Highlights.Clear();
        SevenDayPending = false;
        Notice = null;
        Selected = new Selection();
        DetailTab = DetailTab.Character;
        Panel = null;
        HoverPreview = null;
        IgniteMenuOpen = false;
        Pop = null;
        CharPage = null;
        StatusHover = null;
        StatusPinned = null;
        HelpTopic = null;
        HelpEntry = null;
        DialogLogPick = null;
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
        }
        catch (Exception ex)
        {
            SaveState = SaveStatus.Failed;
            SaveError = ex.Message;
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
        if (sel.Key != Selected.Key) SelectedBatchKey = null;
        Selected = sel;
        Pop = null;
        if (sel.Kind != SelectionKind.None) DetailTab = DetailTab.Object;
        Notice = null;
        // 选中卡片后卡下会出现操作条：旧的结果提示收起，不遮挡操作（试玩修复 G5）；内容仍在页脚与行动日志里
        if (sel.Kind != SelectionKind.None) Toast = null;
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
        Selected = new Selection();
        DetailTab = DetailTab.Character;
    }

    /// <summary>Esc 或收起操作条：清空选中，右栏回到默认的人物页。</summary>
    public void ClearSelection()
    {
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
        if (State == null || !E.Location(S.CurrentLocation).HasFire) return;
        Panel = null;
        HoverPreview = null;
        Select(new Selection { Kind = SelectionKind.Facility, Id = GameEngine.FireIdOf(S.CurrentLocation) });
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
        var key = $"{r.Type}|{r.TargetId}|{r.InstanceId}|{r.Minutes}|{r.ToolInstanceId}|{r.PotInstanceId}|{r.BatchCount}|{string.Join(",", r.PreferredInstanceIds)}";
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
                if (E.FacilityLocation(Selected.Id) != S.CurrentLocation) ClearSelectionSilently();
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
        S.Items.Where(i => i.Owner == S.CurrentLocation && i.ItemId == itemId && i.ReservedBy == null)
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
            if (E.Item(head.ItemId).IsStackable)
                return TransferBatch(head.Owner, head.ItemId, head.ExpiresAt, count, r.Kind == InstantKind.Pickup, requestId);
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
        Notice = res.Ok ? null : res.Reason;
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

    /// <summary>当前地点火堆“等待烹饪”的只读解析（S11 §9）：悬停预览与按钮禁用原因共用；每次界面版本只算一次。</summary>
    public CookWaitPlan CookWaitPreview()
    {
        if (_cookCache.ver == Version && _cookCache.loc == S.CurrentLocation && _cookCache.plan != null) return _cookCache.plan;
        var plan = E.PreviewCookWait(S);
        _cookCache = (Version, S.CurrentLocation, plan);
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

    /// <summary>连续推进 5 分钟段，直到行动结束、暂停或死亡。浏览与整理时不推进。</summary>
    private async Task RunLoop()
    {
        if (Running) return;
        Running = true;
        _skip = false;
        var segs = 0;
        var startLocation = S.CurrentLocation;
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
                await Task.Delay(_skip ? 1 : SegmentDelayMs);
            }
        }
        finally
        {
            Running = false;
            _skip = false;
            // 到达新地点后，原地点的路线、资源与地面选择都已失效
            if (State != null && S.CurrentLocation != startLocation && Selected.Kind != SelectionKind.None
                && !(Selected.Owner == ItemInstance.Carried))
                ClearSelectionSilently();
            if (State != null && S.CurrentLocation != startLocation) IgniteMenuOpen = false;
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

    /// <summary>结果提示显示约 8 秒后自动收起（界面节奏，非玩法数值）；同一条内容仍留在页脚与行动日志（试玩修复 G5）。</summary>
    private async Task AutoDismissToast(int seq)
    {
        await Task.Delay(8000);
        if (seq != _toastSeq || Toast == null) return;
        Toast = null;
        Notify();
    }

    private void Drain()
    {
        if (State == null) return;
        var toastSet = false;
        var before = Toast;
        foreach (var ev in E.DrainEvents(S))
        {
            if (string.IsNullOrEmpty(ev.Text)) continue;
            Recent.Add(ev);
            switch (ev.Kind)
            {
                case GameEventKind.ActionCompleted:
                case GameEventKind.ActionAborted:
                case GameEventKind.ActionCancelled:
                case GameEventKind.Hazard:
                case GameEventKind.Death:
                case GameEventKind.Encounter:
                case GameEventKind.BattleEnd:
                    Toast = ev;
                    toastSet = true;
                    break;
                case GameEventKind.FacilityDone:
                    // 设施完成只提示，不暂停、不替换角色行动；同一批里角色行动的结果提示优先
                    if (!toastSet) Toast = ev;
                    break;
                case GameEventKind.SevenDay:
                    SevenDayPending = true;
                    break;
            }
            if (ev.HighlightIds.Count > 0)
            {
                Highlights.Clear();
                foreach (var id in ev.HighlightIds) Highlights.Add(id);
            }
        }
        if (Recent.Count > 60) Recent.RemoveRange(0, Recent.Count - 60);
        if (Toast != null && !ReferenceEquals(Toast, before)) _ = AutoDismissToast(++_toastSeq);
    }

    public void DismissToast()
    {
        Toast = null;
        Notify();
    }

    /// <summary>
    /// 物品卡上的三角箭头：把该物品转移 1 件到对面（地面→随身为拾取，随身→地面为放下）。
    /// 工具按实例转移；可堆叠物品取最早到期、未预留的一堆。
    /// </summary>
    public ActionResult TransferOne(string owner, string itemId, int? instanceId, string? toZone = null)
    {
        if (State == null) return ActionResult.Fail("没有进行中的游戏");
        var inst = instanceId.HasValue
            ? S.Items.FirstOrDefault(i => i.InstanceId == instanceId.Value && i.Owner == owner)
            : S.Items.Where(i => i.Owner == owner && i.ItemId == itemId && i.ReservedBy == null)
                .OrderBy(i => i.ExpiresAt ?? int.MaxValue).ThenBy(i => i.InstanceId).FirstOrDefault();
        if (inst == null) return ActionResult.Fail("没有可转移的物品");
        // 火堆或锅里的物品：明确目标“取下到地面”或“拾取到随身”，与拖出走同一校验（统一卡片案第 5 节）
        if (inst.InFacility || (inst.InContainer && !E.IsLiquid(inst)))
        {
            var id = inst.InstanceId;
            var movedItem = inst.ItemId;
            var zone = toZone ?? S.CurrentLocation;
            var wasSelected = Selected.InstanceId == id && Selected.Kind == SelectionKind.Tool;
            var res = Instant(InstantRequest.TakeOut(id, zone));
            if (res.Ok)
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
        Notify();
    }

    private void ResetBattleUi()
    {
        BattleAction = null;
        BattleTarget = null;
        BattleHover = null;
        BattleItem = null;
        BattleMenuOpen = false;
    }

    public BattleCommand CurrentBattleCommand() => BattleCommand.Of(BattleAction ?? "", BattleTarget, BattleItem);

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
    public void PickBattleAction(string action, int? item = null)
    {
        if (State == null || S.Battle == null) return;
        if (BattleAction == action && BattleItem == item) { ClearBattleAction(); return; }
        BattleAction = action;
        BattleItem = item;
        DetailTab = DetailTab.Object;
        Notice = null;
        Notify();
    }

    /// <summary>当前选中的指令是否处于瞄准（需要点击目标提交）：单体攻击类与群攻（散射：点受影响集合中任一目标提交一次）。</summary>
    public bool BattleAiming => State != null && S.Battle != null && BattleAction != null && AimOption() != null;

    private BattleCommandOption? AimOption()
    {
        var o = E.BattleOptions(S).FirstOrDefault(x => x.Action == BattleAction && (BattleItem == null || x.ItemInstanceId == BattleItem));
        return o != null && (o.NeedsTarget || o.Action == "scatter") ? o : null;
    }

    /// <summary>瞄准中可点击提交的目标：单体为合法目标；群攻为预览里真实受影响的集合。</summary>
    public IReadOnlyCollection<int> AimTargets()
    {
        var o = BattleAiming ? AimOption() : null;
        if (o == null) return Array.Empty<int>();
        if (o.Action == "scatter")
            return E.PreviewCommand(S, BattleCommand.Of("scatter")) is { Ok: true } p ? p.Targets.Select(t => t.Index).ToList() : new List<int>();
        return o.Targets;
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
        if (BattleAction != "scatter") BattleTarget = index;
        await ExecuteBattleCommand(token);
    }

    /// <summary>点敌卡（未瞄准）：只选中查看，不攻击。</summary>
    public void PickBattleTarget(int index)
    {
        BattleTarget = index;
        DetailTab = DetailTab.Object;
        Notify();
    }

    /// <summary>右键/Esc：只取消未提交的瞄准或待确认指令，不耗 AP、不脱战。</summary>
    public void ClearBattleAction()
    {
        BattleAction = null;
        BattleItem = null;
        Notify();
    }

    /// <summary>
    /// 本地“执行”按钮的请求号（R03 §7.2“一次执行一个 requestId”）：由按钮渲染时所见的战斗状态与所选指令决定——
    /// 同一画面上的连点/双击带同一个号，内核只受理一次；行动点、游标、轮次或指令/目标/物品一变就是新号。
    /// </summary>
    public string BattleExecToken()
    {
        var b = S.Battle;
        return b == null ? "" : $"bt:{b.BattleId}:{b.Round}:{b.Cursor}:{b.PlayerAp}:{b.Log.Count}:{BattleAction}:{BattleTarget}:{BattleItem}";
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
            if (res.Ok && !res.Duplicate)
            {
                // 提交后瞄准收起（R03 §7.4：连点/双击只提交一次，第二击不排队成另一攻击；新目标不复用旧点击）
                BattleAction = null;
                BattleItem = null;
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
        Toast = null;
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

    /// <summary>逐条推进敌方行动与轮末，直到轮到玩家或战斗结束；每步存档，刷新页面后从游标继续。</summary>
    public async Task BattleLoop()
    {
        if (State == null || S.Battle == null || BattleBusy) return;
        BattleBusy = true;
        try
        {
            while (State != null && S.Battle != null && !S.Progress.Dead && !E.AwaitingPlayer(S))
            {
                var r = E.BattleStep(S);
                Drain();
                Save();
                Notify();
                if (r.Kind is BattleStepKind.None or BattleStepKind.Ended or BattleStepKind.Dialogue) break;
                if (r.Kind is BattleStepKind.EnemyActed or BattleStepKind.RoundEnded)
                    await Task.Delay(Math.Max(160, SegmentDelayMs * 4));
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
            Save();
        }
        Notify();
    }
}
