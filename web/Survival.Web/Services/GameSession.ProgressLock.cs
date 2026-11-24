using Survival.Core.State;

namespace Survival.Web.Services;

public partial class GameSession
{
    /// <summary>
    /// B60（S01／S14 文首「2026-11-24 Q1038 主动行动统一居中显示进度」选 C；S11／S14「B51c2a2原批Q1028—Q1030」§Q1030）：
    /// 中央主动行动进度（执行中的「行动进行中」浮窗、暂停时的「行动暂停中」浮窗、补给子行动）此刻显示在最上层——不论开着地图、日志、火堆、设施、
    /// 城市服务等面板，一律居中浮窗（撤掉 B31b R55／Q324 的底部窄条与城市层右下角进度），框外整个游戏界面加遮罩和模糊，鼠标、触摸、拖放、悬停、
    /// 键盘焦点与业务快捷键只服从最上层。与 GameScreen 渲染 PauseLayer／CentralProgress 的条件一致（淡出中的最后一帧不算，淡出期间不拦截）。
    /// 对白接管（含行动等待对话）时由对白聚焦层负责，不另加；全屏战斗、收束停留与死亡结算各有自己的层。
    /// </summary>
    public bool ProgressLock => State != null && !S.Progress.Dead && !DialogueLocked && !BattleFullscreen && !BattleClosingHold
        && (PauseLayerShown || ProgressInfo() is { DialogueWaiting: false });

    /// <summary>B41c2（S14 Q523）：暂停弹窗显示、原工具可按例外拾取——遮罩与弹窗只占①②，③当前地面、④随身照原规则可操作（PauseLayer「pick-open」同源）。</summary>
    public bool PausePickOpen => State != null && PauseLayerShown && S.Sub == null && E.LockedPickupTool(S) is ItemInstance tool && E.LockedToolPickupOpen(S, tool);

    /// <summary>B41e4（S14 Q569）：暂停弹窗显示、撤退离地保留暂停的地图前往开放——让出①地图与场景行（PauseLayer「travel-open」同源）。</summary>
    public bool PauseTravelOpen => State != null && PauseLayerShown && E.PausedTravelOpen(S);

    /// <summary>
    /// B60：进度遮罩期间仍有原规则开放的背景行（Q523 原工具例外拾取、Q569 地图前往开放）。Q1038 只说“保持现有主动行动原执行条件、子动作与允许的处理流程”，
    /// 没写这两种例外在新遮罩下是否保留，B60 先按原样保留（这两行在遮罩上挖洞、不加 inert、不接管 Tab），已列入待确认。
    /// </summary>
    public bool ProgressLockOpenRows => ProgressLock && (PausePickOpen || PauseTravelOpen);

    /// <summary>B60：进度遮罩期间背景整块 inert（不能点、拖、取得键盘焦点，读屏跳过）；例外开放行时不加，由遮罩挡住其余部分。</summary>
    public bool ProgressBgInert => ProgressLock && !ProgressLockOpenRows;

    /// <summary>
    /// B60：进度遮罩上挖洞保留原权限的位置（app.js survivalFocus 按实际位置算 clip-path）——顶栏「菜单」（与 Esc 同一游戏菜单，S14 Q322 行动推进和暂停均可打开）、
    /// 底栏存档处理按钮（「在此页面继续保存」「重试」，系统级，不向背景派发业务），沿 BM6 Q625 对白聚焦的白名单；暂停例外开放时再加开放的行。
    /// 进度浮窗、暂停浮窗、结果提示、数量框与游戏菜单本身层级在遮罩之上，不靠挖洞。
    /// </summary>
    public string ProgressKeep
    {
        get
        {
            var keep = ProgressKeepBase;
            if (PausePickOpen) keep += ", " + ProgressKeepPickRows;
            if (PauseTravelOpen) keep += ", " + ProgressKeepTravelRow;
            return keep;
        }
    }

    /// <summary>进度遮罩的基础白名单（顶栏「菜单」、底栏存档处理按钮）。</summary>
    public const string ProgressKeepBase = ".top [data-menu-key], .footer .save button";
    /// <summary>Q523 例外：③当前地面、④随身两行。</summary>
    public const string ProgressKeepPickRows = "[data-pt=shelf-ground], [data-pt=shelf-carried]";
    /// <summary>Q569 例外：①地图与场景行。</summary>
    public const string ProgressKeepTravelRow = ".center-scroll.four .map-row";

    /// <summary>
    /// B60：行动推进开始（开工、继续行动、暂停中开始补给子行动）时进度浮窗成为最上层——收起盖在背景上、层级本会高过遮罩的临时局部层：
    /// 就近操作盘、点火工具盘、固定的状态说明浮层，选卡操作盘按 Q378 收起（保留选中，同 B46 单段展示的做法）。一级面板不关，浮窗退出后按届时状态恢复（Q1038）。
    /// </summary>
    private void CollapseForProgressLock()
    {
        if (CardPopShown) CardPopCovered = true;
        IgniteMenuOpen = false;
        Pop = null;
        StatusPinned = null;
        StatusHover = null;
    }
}
