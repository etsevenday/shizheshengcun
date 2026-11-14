using System.Diagnostics;
using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Web.Services;

/// <summary>
/// BM5c（R220：S14「2026-11-14 Q605：正式入战清窗口，探索随机与到期分流」、R05 文首 Q605 节、逐字稿 Q605）：
/// - 正式入战清窗口：任一真实场次开始（新的 BattleId 出现）时关闭当前最高顶栏页与整条战前返回链（连同各层未提交草稿）、卡旁浮窗、点火菜单与拖拽预览；
///   战后不恢复、不自动续工或执行草稿。敌踪仅到期但因探索延后时没有场次，不在这里清。
/// - 探索随机短提示：内核在真实场次已锁后写下唯一一条带 <see cref="GameEvent.EncounterPrompt"/> 的日志；壳在可操作战斗画面之前把同一句
///   「探索中遭遇敌人，即将进入战斗。」显示 <see cref="SingleSegmentShowMs"/>（标准／快／慢 1200／600／1800）毫秒。提示期间不推进回合、不接受战斗指令；
///   点击／Enter／Space 只跳过提示；ESC 打开战斗菜单并冻结计时，关闭后续播；结束后很短时间内的 Enter 不穿透成战斗确认。
///   提示不进存档：读档时已锁的战斗直接显示，不补播。
/// </summary>
public sealed partial class GameSession
{
    private int? _battleSeen;
    private bool _encounterPromptPending;
    private bool _encounterPromptSkip;

    /// <summary>正在显示的探索遭遇短提示正文（无则 null）。</summary>
    public string? EncounterPrompt { get; private set; }

    /// <summary>短提示正在战斗画面上方显示（战斗画面据此整体 inert）。</summary>
    public bool EncounterPromptActive => EncounterPrompt != null && InBattleUi;

    /// <summary>
    /// 每次刷新前：出现了新的真实场次就按 Q605 清掉战前所有窗口（不取消真实世界工作、不动偏好）。连场换场次也照清（战斗中顶栏页本就打不开）。
    /// </summary>
    private void OnBattleOnset()
    {
        var id = State != null && S.Battle != null ? S.Battle.BattleId : (int?)null;
        if (id == _battleSeen) return;
        _battleSeen = id;
        if (id == null) return;
        if (Panel != null || _chain.Count > 0) CloseAllPanels();
        Pop = null;
        IgniteMenuOpen = false;
        PendingAction = null;
        HoverPreview = null;
        FeedPending = null;
        if (CardPopShown) CardPopCovered = true;
    }

    /// <summary>Drain 里看到本批有探索遭遇入战日志：记下要在战斗画面前播一次短提示。</summary>
    private void NoteEncounterPrompt(GameEvent ev)
    {
        if (ev.EncounterPrompt) _encounterPromptPending = true;
    }

    /// <summary>
    /// 播放一次探索遭遇短提示（有待播且本场确是探索遭遇时）。世界时间、回合与敌方行动都不动；菜单／系统说明打开时停在原处，关上后接着计时。
    /// </summary>
    private async Task ShowEncounterPrompt()
    {
        if (!_encounterPromptPending) return;
        _encounterPromptPending = false;
        if (State == null || S.Battle is not { ExploreEncounter: true } || S.Progress.Dead || E.DialogueLocked(S)) return;
        var gen = _fxGen;
        var battle = S.Battle.BattleId;
        EncounterPrompt = GameEngine.ExploreEncounterText;
        _encounterPromptSkip = false;
        Notify();
        try
        {
            await Task.Delay(1);   // 让这一帧先画出来再计时
            var sw = Stopwatch.StartNew();
            long elapsed = 0, last = 0;
            var total = SingleSegmentShowMs;
            while (gen == _fxGen && !_encounterPromptSkip && State != null && S.Battle?.BattleId == battle && elapsed < total)
            {
                await Task.Delay(30);
                var now = sw.ElapsedMilliseconds;
                if (!FxPaused) elapsed += now - last;
                last = now;
            }
        }
        finally
        {
            if (gen == _fxGen)
            {
                EncounterPrompt = null;
                _encounterPromptSkip = false;
                _fxEndedMs = NowMs;   // 跳过提示的同一次 Enter 不接着确认战斗指令
                Notify();
            }
        }
    }

    /// <summary>点击／Enter／Space：只跳过短提示（菜单或说明盖在上面时不受理）。</summary>
    public void SkipEncounterPrompt()
    {
        if (EncounterPrompt == null || FxPaused) return;
        _encounterPromptSkip = true;
    }

    /// <summary>对白结束接续的探索检查入战（Q605 §2 末段）：先播短提示，再接战斗流程。</summary>
    private async Task PromptThenBattle()
    {
        await ShowEncounterPrompt();
        if (State == null || S.Battle == null || S.Progress.Dead) return;
        if (!E.AwaitingPlayer(S)) await BattleLoop();
        else { AutoTarget(); Notify(); }
    }

    /// <summary>读档、新局、回标题：清掉未播的提示（读档不补播）。</summary>
    private void ResetEncounterPrompt()
    {
        _encounterPromptPending = false;
        _encounterPromptSkip = false;
        EncounterPrompt = null;
        _battleSeen = State?.Battle?.BattleId;
    }
}
