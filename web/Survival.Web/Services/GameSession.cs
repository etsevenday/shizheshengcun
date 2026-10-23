using System.Net.Http;
using Survival.Core.Config;
using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Web.Services;

public enum SelectionKind { None, Location, Entrance, Resource, ItemGroup, Tool }

public sealed class Selection
{
    public SelectionKind Kind { get; init; }
    public string Id { get; init; } = "";
    /// <summary>物品所在：carried 或地点 ID。</summary>
    public string Owner { get; init; } = "";
    public int? InstanceId { get; init; }

    public string Key => $"{Kind}:{Id}:{Owner}:{InstanceId}";
}

public enum SaveStatus { None, Saving, Saved, Failed }



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
    /// <summary>当前打开的浏览层：map / craft / rest / log / menu / status:xxx。</summary>
    public string? Panel { get; set; }
    /// <summary>从设施卡打开加工面板时要预选的配方；面板读取一次后清空。</summary>
    public string? CraftRecipe { get; set; }

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
        if (panel == "craft" && State != null)
        {
            var main = S.Main;
            if (main != null && (main.Type == ActionType.Craft || main.Type == ActionType.Build)) CraftRecipe = main.TargetId;
            else if (recipe != null) CraftRecipe = recipe;
            else if (CraftRecipe == null || E.Config.Recipes.All(r => r.Id != CraftRecipe))
                CraftRecipe = E.Location(S.CurrentLocation).HasFire ? "boil" : "knife";
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
            // 配置以内嵌资源随程序集发布，唯一来源仍是 data/config.v0.4.json
            using var stream = typeof(GameSession).Assembly.GetManifestResourceStream("config.v0.4.json")
                               ?? throw new InvalidOperationException("缺少内嵌配置 config.v0.4.json");
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
            State = SaveSerializer.Deserialize(json);
            ResetUi();
            foreach (var l in S.Log.TakeLast(8))
                Recent.Add(new GameEvent { Minute = l.Minute, Text = l.Text, Kind = GameEventKind.Info });
            Notify();
            return null;
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

    /// <summary>检查现有存档能否读取；不能时返回原因（用于禁用“继续”）。</summary>
    public string? SaveProblem()
    {
        try
        {
            var json = _store.Load();
            if (string.IsNullOrEmpty(json)) return "没有存档";
            SaveSerializer.Deserialize(json);
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
        Panel = null;
        _skip = false;
        Running = false;
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
        Notice = null;
        Notify();
    }

    public void ClearSelection()
    {
        Selected = new Selection();
        Notify();
    }

    // ───────────── 行动 ─────────────

    private readonly Dictionary<string, (int ver, ActionPreview p)> _previewCache = new();

    public ActionPreview Preview(ActionRequest r)
    {
        var key = $"{r.Type}|{r.TargetId}|{r.InstanceId}|{r.Minutes}";
        if (_previewCache.TryGetValue(key, out var c) && c.ver == Version) return c.p;
        var p = E.Preview(S, r);
        _previewCache[key] = (Version, p);
        if (_previewCache.Count > 64) _previewCache.Clear();
        return p;
    }

    public async Task<ActionResult> StartAction(ActionRequest r)
    {
        if (State == null) return ActionResult.Fail("没有进行中的游戏");
        var res = E.Start(S, r);
        if (!res.Ok)
        {
            Notice = res.Reason;
            Notify();
            return res;
        }
        Notice = null;
        Drain();
        Save();
        Notify();
        await RunLoop();
        return res;
    }

    public ActionResult Instant(InstantRequest r)
    {
        var res = E.DoInstant(S, r);
        Notice = res.Ok ? null : res.Reason;
        if (res.Ok)
        {
            Drain();
            Save();
        }
        Notify();
        return res;
    }

    /// <summary>同一批次（同物品、同到期时间、同位置）可能分成多堆；按堆依次拾取或放下指定数量。</summary>
    public ActionResult TransferBatch(string owner, string itemId, int? expiresAt, int count, bool toCarried)
    {
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
        Notice = last.Ok ? null : last.Reason;
        Drain();
        ReconcileSelection();
        Save();
        Notify();
        return last;
    }

    /// <summary>选中的物品堆已全部拾走或放下时清空选中，详情区回到默认提示（UI 案 04）。</summary>
    private void ReconcileSelection()
    {
        if (Selected.Kind != SelectionKind.ItemGroup && Selected.Kind != SelectionKind.Tool) return;
        var still = S.Items.Any(i => i.Owner == Selected.Owner && i.ItemId == Selected.Id
                                     && (Selected.InstanceId == null || i.InstanceId == Selected.InstanceId));
        if (!still) Selected = new Selection();
    }

    /// <summary>某批次在 30 公斤上限内最多能拾取多少。</summary>
    public int MaxPickup(string itemId, int available)
    {
        var w = E.Item(itemId).WeightGrams;
        var room = Math.Max(0, E.Config.Carry.HardGrams - E.CarriedGrams(S));
        return Math.Min(available, room / w);
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
                if (r.Status is SegmentStatus.Paused or SegmentStatus.Dead or SegmentStatus.Idle) break;
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
                Selected = new Selection();
            Save();
            Notify();
        }
    }

    private void Drain()
    {
        if (State == null) return;
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
                    Toast = ev;
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
    }

    public void DismissToast()
    {
        Toast = null;
        Notify();
    }

    /// <summary>每次点物品卡上的三角箭头加一；卡片旁的拾取条据此把光标放进数量框。</summary>
    public int TransferFocusTick { get; private set; }

    public void RequestTransferFocus()
    {
        TransferFocusTick++;
        Notify();
    }

    /// <summary>
    /// 物品卡上的三角箭头：把该物品转移 1 件到对面（地面→随身为拾取，随身→地面为放下）。
    /// 工具按实例转移；可堆叠物品取最早到期、未预留的一堆。
    /// </summary>
    public ActionResult TransferOne(string owner, string itemId, int? instanceId)
    {
        if (State == null) return ActionResult.Fail("没有进行中的游戏");
        var inst = instanceId.HasValue
            ? S.Items.FirstOrDefault(i => i.InstanceId == instanceId.Value && i.Owner == owner)
            : S.Items.Where(i => i.Owner == owner && i.ItemId == itemId && i.ReservedBy == null)
                .OrderBy(i => i.ExpiresAt ?? int.MaxValue).ThenBy(i => i.InstanceId).FirstOrDefault();
        if (inst == null) return ActionResult.Fail("没有可转移的物品");
        var toCarried = owner != ItemInstance.Carried;
        return Instant(toCarried ? InstantRequest.Pickup(inst.InstanceId, 1) : InstantRequest.Drop(inst.InstanceId, 1));
    }

    public void DebugFill()
    {
        E.DebugFillGround(S);
        Save();
        Notify();
    }
}
