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

    /// <summary>打开系统说明（可直接定位到某主题/条目）。只读浏览，不推进时间；战斗与对话锁期间不可打开。</summary>
    public void OpenHelp(string? topic = null, string? entry = null)
    {
        if (InBattleUi || DialogueLocked) return;
        _helpReturn = Panel is "help" ? _helpReturn : Panel;
        _helpDirect = topic != null;
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
        if (!IgniteMenuOpen) HoverPreview = null;
        Notify();
    }

    public void CloseIgniteMenu()
    {
        if (!IgniteMenuOpen) return;
        IgniteMenuOpen = false;
        Notify();
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
        // 只有地图/制作/休息会在右栏显示面板自己的预估，切到“对象信息”；顶栏浏览层（日志、目标、提示、对话记录、说明）不改右栏页签
        if (panel != null) { IgniteMenuOpen = false; if (panel is "map" or "craft" or "rest") DetailTab = DetailTab.Object; }
        if (panel == "help") { HelpTopic = null; HelpEntry = null; _helpDirect = false; _helpReturn = null; }
        if (panel == "dialogs") DialogLogPick = null;
        // 关闭面板时没有选中对象就回人物页（v0.7 6.1：无选中默认展示人物信息；复验新发现 2）
        else if (Selected.Kind == SelectionKind.None) DetailTab = DetailTab.Character;
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
        Version++;
        Changed?.Invoke();
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
        Selected = sel;
        if (sel.Kind != SelectionKind.None) DetailTab = DetailTab.Object;
        Notice = null;
        // 选中卡片后卡下会出现操作条：旧的结果提示收起，不遮挡操作（试玩修复 G5）；内容仍在页脚与行动日志里
        if (sel.Kind != SelectionKind.None) Toast = null;
        Notify();
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

    /// <summary>同一批次（同物品、同到期时间、同位置）可能分成多堆；按堆依次拾取或放下指定数量。</summary>
    public ActionResult TransferBatch(string owner, string itemId, int? expiresAt, int count, bool toCarried, string? requestId = null)
    {
        if (GameEngine.SeenRequest(S, requestId)) return ActionResult.Success();
        var left = count;
        ActionResult last = ActionResult.Success();
        var stacks = S.Items.Where(i => i.Owner == owner && i.ItemId == itemId && i.ExpiresAt == expiresAt && i.ReservedBy == null)
            .OrderBy(i => i.InstanceId).ToList();
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

    /// <summary>某批次在 30 公斤上限内最多能拾取多少（锅按锅加内容计）。</summary>
    public int MaxPickup(string itemId, int available, ItemInstance? inst = null)
    {
        var w = inst != null && inst.Count > 0 ? E.ItemGrams(S, inst) / inst.Count : E.Item(itemId).WeightGrams;
        var room = Math.Max(0, E.Config.Carry.HardGrams - E.CarriedGrams(S));
        return Math.Min(available, room / w);
    }

    // ───────────── 拖拽落点（交互与地图扩展 v0.3 · 01 案） ─────────────

    /// <summary>打开拖拽得到的行动预览：右栏显示与按钮相同的预估，玩家按“开始”才计时。</summary>
    public void ShowAction(ActionRequest r, string title)
    {
        PendingAction = r;
        PendingActionTitle = title;
        Panel = null;
        Selected = new Selection { Kind = SelectionKind.Action, Id = r.TargetId, InstanceId = r.InstanceId };
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

    public void Cancel()
    {
        var res = E.Cancel(S);
        Notice = res.Ok ? null : res.Reason;
        Drain();
        Save();
        Notify();
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
                if (!_skip)
                {
                    Save();
                    Notify();
                }
                if (r.Status is SegmentStatus.Paused or SegmentStatus.Dead or SegmentStatus.Idle or SegmentStatus.Dialogue) break;
                if (!_skip && (S.Main != null || S.Sub != null)) await Task.Delay(SegmentDelayMs);
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

    /// <summary>指令条上选中的指令（点动作 → 右栏预览 → 点执行）。</summary>
    public string? BattleAction { get; private set; }
    /// <summary>选中的目标敌人（本场序号）；点敌卡只选目标，不攻击。</summary>
    public int? BattleTarget { get; private set; }
    /// <summary>换武器/用药指定的实例。</summary>
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

    /// <summary>选择指令：需要目标而目标不合法时自动改选第一个可选目标；只显示预览，不执行。</summary>
    public void PickBattleAction(string action, int? item = null)
    {
        if (State == null || S.Battle == null) return;
        BattleAction = action;
        BattleItem = item;
        var opt = E.BattleOptions(S).FirstOrDefault(o => o.Action == action && (item == null || o.ItemInstanceId == item));
        if (opt != null && opt.NeedsTarget && (BattleTarget == null || !opt.Targets.Contains(BattleTarget.Value)))
            BattleTarget = opt.Targets.Count > 0 ? opt.Targets[0] : BattleTarget;
        DetailTab = DetailTab.Object;
        Notice = null;
        Notify();
    }

    /// <summary>点敌卡：只选目标，不攻击。</summary>
    public void PickBattleTarget(int index)
    {
        BattleTarget = index;
        DetailTab = DetailTab.Object;
        Notify();
    }

    public void ClearBattleAction()
    {
        BattleAction = null;
        BattleItem = null;
        Notify();
    }

    /// <summary>执行右栏预览的指令：新 requestId，执行期间锁定输入；之后逐条展示敌方行动直到轮到玩家或战斗结束。</summary>
    public async Task ExecuteBattleCommand()
    {
        if (State == null || S.Battle == null || BattleBusy || BattleAction == null) return;
        var cmd = CurrentBattleCommand().WithRequestId(Guid.NewGuid().ToString("N"));
        BattleBusy = true;
        try
        {
            var res = E.ExecuteCommand(S, cmd);
            Notice = res.Ok ? null : res.Reason;
            if (res.Ok && !res.Duplicate)
            {
                // 用掉的指令收起；攻击类在还有行动点时保留同一指令，方便连打
                var keep = res.Ok && S.Battle != null && E.AwaitingPlayer(S) && GameEngine.AttackActions.Contains(BattleAction);
                if (!keep) { BattleAction = null; BattleItem = null; }
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

    /// <summary>战外装备当前武器（免费，不耗时）。</summary>
    public void Equip(int? instanceId)
    {
        var res = E.EquipWeapon(S, instanceId);
        Notice = res.Ok ? null : res.Reason;
        Drain();
        Save();
        Notify();
    }
}
