using Survival.Core.Engine;

namespace Survival.Web.Services;

/// <summary>
/// BM4a 顶栏覆盖与逐层返回（S14「2026-11-13 B45 Q581—Q590」§Q586、§Q590；逐字稿同日节）。
/// 顶栏按钮／快捷键（及加工制作的「查看来源」、详情里定位打开的系统说明）把新面板盖在当前窗口上：原窗口连同层级与未提交选择留在下层，
/// 只有最上层显示、接收输入、显示键帽；ESC／返回／关闭每次只关当前最上层并恢复紧挨着的下一层。同类页已在下层时提到最上层、不复制。
/// 各面板的未提交状态本来就分别存在会话字段里（火堆菜单 <see cref="FireMenu"/>、制作 Craft*、地图 Map*、整备、成长练习盘、编队草稿……），
/// 盖在下层时不清，真正离开整条链才清；滚动与发起焦点由 app.js survivalPanels 按面板记下、恢复时取回。
/// 真实换场（地点／区）、战斗、终局、读档／新局／回标题、原需回中央进度的真实开工等直接收面板时，整条链作废，不远程恢复旧窗口。
/// 只是会话界面状态，不进存档。
/// </summary>
public sealed partial class GameSession
{
    private readonly PanelReturnChain _chain = new();
    /// <summary>返回链建立时的真实地点与区（换场即作废）。</summary>
    private (string Location, string Ground)? _chainAt;
    /// <summary>从没有面板的画面覆盖打开时正显示着的物品操作盘（选中键）；整条链按返回关完后仍是同一来源就原样再显示。</summary>
    private string? _cardPopBase;

    /// <summary>覆盖打开前显示着的物品操作盘仍是当前选中、正被面板盖着：CardPop 只隐藏不卸载（同游戏菜单 Q390），盘内草稿保留。</summary>
    public bool CardPopHeldUnderPanel => _cardPopBase != null && Panel != null && _cardPopBase == Selected.Key;

    /// <summary>被盖住的下层面板（自底向上）。</summary>
    public IReadOnlyList<string> PanelsUnder => _chain.Under;

    /// <summary>当前最上层下面还有窗口。</summary>
    public bool PanelHasUnder => Panel != null && _chain.Count > 0;

    /// <summary>原只有「关闭」的顶栏阅读页的关闭控件业务名：有下层「返回」、无下层「关闭」（逐字稿 Q590；键帽「ESC」另由 EscCap 承接时显示）。</summary>
    public string PanelCloseLabel => PanelHasUnder ? PanelReturnChain.ReturnLabel : PanelReturnChain.CloseLabel;

    /// <summary>刚恢复的那一层（GameScreen 渲染后让 app.js 取回它的滚动与焦点，读一次即清）。</summary>
    public string? PanelRestorePending { get; set; }

    /// <summary>能叠进返回链的面板：顶栏各页与中部一级面板（加工菜单、工坊／安全屋／晾制架、城市事项、鸟瞰）。远征中止／封存等确认窗不叠。</summary>
    private static bool IsChainPanel(string? p) => IsTopPanel(p);

    /// <summary>
    /// 顶栏按钮与快捷键（M C R L G K J T D H；编队见 GameScreen）：当前最上层就是它 → 关闭它并返回下层；
    /// 否则覆盖打开（原窗口留在下层；它已在下层则提到最上层，沿用原状态）。打开失败时原窗口原样保留。
    /// </summary>
    public void OpenTopPanel(string panel)
    {
        if (Panel == panel) { OpenPanel(null); return; }
        SwitchPanel(panel, null, true);
    }

    /// <summary>
    /// 原规则要求直接收起面板的场合（真实开工回中央进度、出发移动、开发传送等）：整条返回链连同各层未提交草稿一起作废，再关当前面板。
    /// </summary>
    public void CloseAllPanels()
    {
        ClearPanelChain();
        _cardPopBase = null;
        OpenPanel(null);
    }

    /// <summary>
    /// 取出关闭当前最上层后要恢复的那一层：逐层重验，已失效的层丢掉（加工设施已无法操作时提示「这处设施已无法操作，请重新选择。」），
    /// 继续看再下一层；都不能恢复为 null（回原画面）。
    /// </summary>
    private string? PopRestorable()
    {
        while (_chain.Pop() is string p)
        {
            var why = PanelRestoreBlock(p);
            if (why == null) return p;
            DropPanelState(p);
            if (why != "") Notice = why;
        }
        return null;
    }

    /// <summary>该层还能否恢复：null 可以；"" 静默丢掉；其他为要显示的原因。</summary>
    private string? PanelRestoreBlock(string p)
    {
        if (State == null) return "";
        switch (p)
        {
            case FireMenuPanel:
                // Q586：原地点／设施已无法操作不恢复执行页，回当前合法画面
                if (FireMenu is not { } m || m.Location != S.CurrentLocation || m.Ground != S.Ground || !E.HasFireAt(S, m.Ground))
                    return GameEngine.FacReturnGoneText;
                return null;
            case "craft":
                // 经「选择设施」进入的加工站已不在当前区可操作列表里：同上
                if (CraftFromChooser && ChooserReturnStation is string st && E.ProcessingStationChoices(S).All(c => c.StationId != st))
                    return GameEngine.FacReturnGoneText;
                return null;
            case "formation":
                return FormationDraft == null || E.FormationEditBlocked(S) != null ? "" : null;
            case "citymap":
                return E.CityOverviewBlocked(S) != null ? "" : null;
            default:
                return null;
        }
    }

    /// <summary>
    /// 恢复一层（Panel 已设好）：重读当前真实数据并重验该层的未提交选择——不拿旧库存／队列覆盖当前状态；
    /// 所选来源或内容已变化的清掉并提示「内容已变化，请重新选择。」，数量文本原样保留，不自动换批。
    /// </summary>
    private void RestorePanel(string p)
    {
        PanelRestorePending = p;
        if (State == null) return;
        switch (p)
        {
            case FireMenuPanel when FireMenu is { } m:
                // 下一次核对按“非本菜单造成的变化”处理：来源／内容在下层期间被别处用掉、移走，同样提示内容已变化（ReconcileFireMenu）
                m.Minute = int.MinValue;
                SyncRightSilently(FireSelection);
                break;
            case "craft":
            {
                var lost = false;
                if (CraftPot is int pot && E.FindItem(S, pot) == null) { CraftPot = null; lost = true; }
                if (CraftTarget is int t && E.FindItem(S, t) == null) { CraftTarget = null; lost = true; }
                if (CraftPatch is int pk && E.FindItem(S, pk) == null) { CraftPatch = null; lost = true; }
                if (lost) Notice = GameEngine.ContentChangedText;
                break;
            }
        }
    }

    /// <summary>丢掉某种面板留在会话里的未提交状态（链作废或该层已失效时）。当前最上层正是它时不动（由它自己的关闭流程处理）。</summary>
    private void DropPanelState(string p)
    {
        if (Panel == p) return;
        switch (p)
        {
            case FireMenuPanel: FireMenu = null; break;
            case "craft": CraftFromChooser = false; ChooserReturnStation = null; break;
            case "map": SourceItem = null; break;
            case "formation": FormationDraft = null; FormationPick = null; FormationLeader = null; break;
            case "growth": ClosePracticeDraft(); break;
            case "gear": GivePending = null; break;
            case "citymap": CityPick = null; CityHover = null; CityLegacyNote = false; break;
            case "help": HelpTopic = null; HelpEntry = null; _helpDirect = false; break;
        }
    }

    private void ClearPanelChain()
    {
        foreach (var p in _chain.Clear()) DropPanelState(p);
        _chainAt = null;
    }

    /// <summary>
    /// 每次刷新前：没有面板（原规则直接收盘）、终局、进入战斗、真实地点或区变了，返回链作废——回当前合法画面，不远程恢复旧设施（Q586、Q590）。
    /// 仅打开另一个合法顶栏面板不算失效。
    /// </summary>
    private void ReconcilePanelChain()
    {
        if (Panel == null) _cardPopBase = null;
        if (_chain.Count == 0) { _chainAt = null; return; }
        if (State == null || Panel == null || S.Progress.Dead || InBattleUi
            || _chainAt is not { } at || at.Location != S.CurrentLocation || at.Ground != S.Ground)
            ClearPanelChain();
    }
}
