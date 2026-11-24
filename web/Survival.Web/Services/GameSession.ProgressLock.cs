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

    /// <summary>
    /// B60：进度遮罩期间背景整块 inert（不能点、拖、取得键盘焦点，读屏跳过）。B60a（S14 B60 Q1100）：撤 Q523 原工具拾取、Q569 撤退返程两种例外开放行，
    /// 暂停时同样整块 inert。
    /// </summary>
    public bool ProgressBgInert => ProgressLock;

    /// <summary>
    /// B60：进度遮罩上挖洞保留原权限的位置（app.js survivalFocus 按实际位置算 clip-path）——顶栏「菜单」（与 Esc 同一游戏菜单，S14 Q322 行动推进和暂停均可打开）、
    /// 底栏存档处理按钮（「在此页面继续保存」「重试」，系统级，不向背景派发业务），沿 BM6 Q625 对白聚焦的白名单（02 Q1101 按 A 保持）。
    /// 进度浮窗、暂停浮窗、结果提示、数量框与游戏菜单本身层级在遮罩之上，不靠挖洞。
    /// </summary>
    public string ProgressKeep => ProgressKeepBase;

    /// <summary>
    /// B60a 试玩修复（S14 B60 Q1103「焦点沿 Q1038 回到仍合法的位置」）：进度浮窗退出时原焦点已不在（点火盘确认键、随所选目的地清除而消失的「出发」键），
    /// 按当前回到的原页给出仍合法的落点（app.js progLockEnd 依次取第一个存在、可用、不在 inert 里的；本身不可聚焦就取其中第一个可用控件）：
    /// 地图——当前位置节点（普通层地点节点、城市层建筑），再地图视口、地图窗；火堆菜单——菜单内第一个可用控件。其他页为空，按原焦点所在区域就近找（app.js）。
    /// </summary>
    public string[] ProgReturnFocus
    {
        get
        {
            if (State == null) return [];
            if (Panel == "map")
                return [$"[id=\"mn-{S.CurrentLocation}\"]", $".city-bldg[data-node=\"{S.CurrentLocation}\"]", ".map-viewport", ".map-panel"];
            if (Panel == FireMenuPanel) return [".fire-menu"];
            return [];
        }
    }

    /// <summary>进度遮罩的基础白名单（顶栏「菜单」、底栏存档处理按钮）。</summary>
    public const string ProgressKeepBase = ".top [data-menu-key], .footer .save button";

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
