using System.Diagnostics;
using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Web.Services;

/// <summary>
/// B34b（R03 文首「2026-11-13 B34主要Q570—Q572」§Q571／§Q572，S14 同日节，Q296／Q297／Q321）：战斗特写播放层。
/// 内核先按原事务结算并抄下出手快照（<see cref="GameEngine.DrainBattleFx"/>），这里只把快照排队播放：每段基准 1200ms × 动画速度（0.5／1／1.5）；
/// 点击／Enter／Space 两级跳过（第一次跳到当前段结果，仍有队列时再一次跳过余下表现）；菜单与系统说明打开时停在当前进度，关闭后原处续播；
/// 读档、新局、回标题直接清掉旧队列与跳过意图。播放、跳过都不调用任何结算；快照不进存档。
/// </summary>
public sealed partial class GameSession
{
    /// <summary>特写每段基准时长（Q297：默认 1200 毫秒；Q321 乘动画速度倍率）。</summary>
    public const int FxBaseMs = 1200;

    /// <summary>第一次跳过后结果帧的停留（界面节奏，乘动画速度；不是玩法数值）。</summary>
    private const int FxResultHoldBaseMs = 320;

    /// <summary>同一次物理输入（双击、按下抬起与原生 click 合流）不得跨两个跳过阶段：两次受理之间的最短间隔。</summary>
    private const int FxSkipDebounceMs = 220;

    /// <summary>短反馈在锚点上停留的时长（界面节奏，乘动画速度）。</summary>
    private const int FxPulseBaseMs = 600;

    private readonly Queue<BattleFxEvent> _fxQueue = new();
    private int _fxGen;
    private bool _fxSkipAll;
    private long _fxLastSkipMs;
    private long _fxEndedMs;

    /// <summary>正在播放的出手（null 为没有播放）。</summary>
    public BattleFxEvent? FxNow { get; private set; }

    /// <summary>当前段已跳到结果帧（第一次跳过之后，或减少动态效果时的静态结果）。</summary>
    public bool FxAtResult { get; private set; }

    /// <summary>每开始新的一段加一，界面用它重建特写节点、从头播 CSS 动画。</summary>
    public int FxSegment { get; private set; }

    /// <summary>播放层是否在最高层承接输入（S14 Q572：画面点击、Enter／Space 由它独占，只有 ESC 能开菜单）。</summary>
    public bool PlaybackActive => FxNow != null;

    /// <summary>队列里还没开始的段数。</summary>
    public int FxQueued => _fxQueue.Count;

    /// <summary>菜单、系统说明打开时沿原阅读暂停：特写停在当前帧（Q572）。</summary>
    public bool FxPaused => MenuOpen || BattleMenuOpen || BattleDrawer == "help";

    /// <summary>只读子页（全记录、预测详情等）盖在上面时：特写照常播放但退到子页下面，点击归子页，不穿透成跳过（R03 §Q572）。</summary>
    public bool FxUnderReading => InBattleUi && BattleDrawer != null && BattleDrawer != "help";

    /// <summary>
    /// 跳过控件此刻的业务名（S14 键表「跳到结果 Enter / Space／跳过余下 Enter / Space」，仅在该跳过阶段实际可用时显示）：
    /// 当前段还没到结果为「跳到结果」；已在结果帧且队列里还有段为「跳过余下」；其余不显示。
    /// </summary>
    public string? FxSkipLabel => FxNow == null ? null : !FxAtResult ? ShellKeys.SkipToResult : _fxQueue.Count > 0 ? ShellKeys.SkipRest : null;

    /// <summary>当前一段的总时长（毫秒）。</summary>
    public int FxDurationMs => MotionMs(FxBaseMs);

    /// <summary>短反馈：锚点键（阵营:编号）→ 序号；界面按它在锚点上播一次短亮（不挡输入、不进队列）。</summary>
    public Dictionary<string, int> FxPulses { get; } = new();

    /// <summary>短反馈此刻显示的真实行动名（S14 Q573：短反馈只显示行动名）。</summary>
    private readonly Dictionary<string, string> _fxPulseNames = new();

    /// <summary>
    /// 播一批刚取走的出手快照（调用方已先 Drain／Commit／Notify 真实状态）。短反馈只在锚点上亮一下；特写逐段排队播放。
    /// 返回是否真的播了特写（播了就不再叠旧的 480 毫秒敌方逐条停顿，R03 §Q571）。上下文切换（读档、新局）时立即返回。
    /// </summary>
    private async Task<bool> PlayFx(List<BattleFxEvent> evs)
    {
        if (evs.Count == 0) return false;
        var gen = _fxGen;
        // B34 试玩修复 E1（R03 §Q572 按真实顺序）：短反馈与特写按发生先后排——短反馈先亮，特写要等锚点上的短反馈播完再盖上来
        var played = false;
        var run = new List<BattleFxEvent>();
        foreach (var ev in evs.OrderBy(x => x.Seq))
        {
            if (gen != _fxGen) return played;
            if (ev.Short)
            {
                if (run.Count > 0) { played |= await PlayRun(run, gen); run.Clear(); }
                if (gen != _fxGen) return played;
                Pulse(ev, gen);
                Notify();
            }
            else if (ev.Results.Count > 0) run.Add(ev);
        }
        if (run.Count > 0) played |= await PlayRun(run, gen);
        return played;
    }

    /// <summary>一串相邻的特写：先等此前的短反馈播完（不叠在它上面），再逐段排队播放。</summary>
    private async Task<bool> PlayRun(List<BattleFxEvent> main, int gen)
    {
        if (_fxSkipAll) return true;   // 本批已选择跳过余下：结果照常（已结算），只略动画
        while (gen == _fxGen && !_fxSkipAll && NowMs < _fxPulseUntilMs) await Task.Delay(30);
        if (gen != _fxGen) return false;
        if (_fxSkipAll) return true;
        foreach (var ev in main) _fxQueue.Enqueue(ev);
        var sw = Stopwatch.StartNew();
        try
        {
            while (_fxQueue.Count > 0)
            {
                if (gen != _fxGen || _fxSkipAll) break;
                FxNow = _fxQueue.Dequeue();
                FxAtResult = false;
                FxSegment++;
                Notify();
                long elapsed = 0, last = sw.ElapsedMilliseconds;
                long total = FxDurationMs;
                while (elapsed < total)
                {
                    await Task.Delay(30);
                    if (gen != _fxGen || _fxSkipAll) break;
                    var now = sw.ElapsedMilliseconds;
                    // 菜单／系统说明打开：不计时，停在当前帧；关上后从原处接着播（不补播期间时长）
                    if (!FxPaused) elapsed += now - last;
                    last = now;
                    if (FxAtResult && total > elapsed + MotionMs(FxResultHoldBaseMs)) total = elapsed + MotionMs(FxResultHoldBaseMs);
                }
            }
        }
        finally
        {
            if (gen == _fxGen)
            {
                _fxQueue.Clear();
                FxNow = null;
                FxAtResult = false;
                _fxEndedMs = NowMs;
                Notify();
            }
        }
        return true;
    }

    /// <summary>
    /// 一次跳过输入（画面点击、Enter、Space、跳过按钮同一入口）。第一次：当前段直接到结果帧；当前段已在结果帧：跳过余下表现（本批随后的出手只略动画）。
    /// 菜单或只读子页在上面时不受理（它们消费自己的输入）；同一次物理输入不跨两级。
    /// </summary>
    public void PlaybackSkip()
    {
        if (FxNow == null || FxPaused || FxUnderReading) return;
        var now = NowMs;
        if (now - _fxLastSkipMs < FxSkipDebounceMs) return;
        _fxLastSkipMs = now;
        if (!FxAtResult) FxAtResult = true;
        else _fxSkipAll = true;
        Notify();
    }

    /// <summary>播放刚结束的同一次输入不得接着确认背后的指令（Q572 输入合流）：结束后很短时间内的 Enter 确认不受理。</summary>
    public bool FxJustEnded => NowMs - _fxEndedMs < FxSkipDebounceMs;

    /// <summary>到下一个需玩家指令、强制对白、战斗收束或连场边界：清掉本批“跳过余下”的意图，不带进下一场／下一次出手。</summary>
    private void ClearFxSkip() => _fxSkipAll = false;

    /// <summary>读档、新局、回标题等上下文切换：立即清旧播放、跳过意图与短反馈，不等旧队列；旧的播放与战斗推进循环随代号失效退出。</summary>
    private void ResetPlayback()
    {
        _fxGen++;
        _fxQueue.Clear();
        _fxSkipAll = false;
        FxNow = null;
        FxAtResult = false;
        FxPulses.Clear();
        _fxPulseNames.Clear();
        _fxPulseUntilMs = 0;
        EndClosingHold();
        E.DrainBattleFx();
    }

    private void Pulse(BattleFxEvent ev, int gen)
    {
        var u = ev.Actor;
        var key = u.Side + ":" + u.Id;
        var seq = FxPulses.TryGetValue(key, out var v) ? v + 1 : 1;
        FxPulses[key] = seq;
        _fxPulseNames[key] = ev.Name;
        _fxPulseUntilMs = Math.Max(_fxPulseUntilMs, NowMs + MotionMs(FxPulseBaseMs) + 50);   // 与 ClearPulse 撤下名牌同一时刻
        _ = ClearPulse(key, seq, gen);
    }

    /// <summary>B34 试玩修复 E1：锚点上最后一个短反馈播完的时刻（特写要等它）。</summary>
    private long _fxPulseUntilMs;

    // ── B34 试玩修复 E2（R03 §Q572、验收 Q572-A01）：撤退成功等「一条指令就收束战斗」时，行动者短反馈先在原战斗画面上播完（或被跳过）再收束 ──
    private TaskCompletionSource? _holdTcs;

    /// <summary>收束前的短反馈停留中：战斗画面冻结在提交前最后一帧（不重绘、不受理背后输入），只在行动者锚点上亮一次行动名。</summary>
    public bool BattleClosingHold { get; private set; }

    /// <summary>停留中要亮的那条短反馈（界面在渲染后交给脚本点亮冻结画面上的锚点）。</summary>
    public BattleFxEvent? ClosingPulse { get; private set; }

    /// <summary>画面点击、Enter、Space、ESC：结束停留，直接收束（不重结算）。</summary>
    public void SkipClosingHold() => _holdTcs?.TrySetResult();

    private void BeginClosingHold(BattleFxEvent ev)
    {
        BattleClosingHold = true;
        ClosingPulse = ev;
        _holdTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private async Task RunClosingHold(int gen)
    {
        var tcs = _holdTcs;
        if (tcs != null) await Task.WhenAny(Task.Delay(MotionMs(FxPulseBaseMs) + 50), tcs.Task);
        if (gen == _fxGen) EndClosingHold();
    }

    private void EndClosingHold()
    {
        _holdTcs?.TrySetResult();
        _holdTcs = null;
        ClosingPulse = null;
        BattleClosingHold = false;
    }

    private async Task ClearPulse(string key, int seq, int gen)
    {
        await Task.Delay(MotionMs(FxPulseBaseMs) + 50);
        if (gen != _fxGen || !FxPulses.TryGetValue(key, out var v) || v != seq) return;
        FxPulses.Remove(key);
        _fxPulseNames.Remove(key);
        Notify();
    }

    /// <summary>锚点此刻是否在播短反馈（阵营:编号）。</summary>
    public int? PulseOf(string side, string id) => FxPulses.TryGetValue(side + ":" + id, out var v) ? v : null;

    /// <summary>锚点此刻短反馈的行动名（没有在播为 null）。</summary>
    public string? PulseNameOf(string side, string id) => FxPulses.ContainsKey(side + ":" + id) && _fxPulseNames.TryGetValue(side + ":" + id, out var n) && n != "" ? n : null;

    /// <summary>跳过控件此刻的配套说明（S14 Q573 逐字，与键表说明同源）。</summary>
    public string? FxSkipNote => FxSkipLabel == ShellKeys.SkipToResult ? ShellKeys.SkipToResultNote : FxSkipLabel == ShellKeys.SkipRest ? ShellKeys.SkipRestNote : null;
}
