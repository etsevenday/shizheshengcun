namespace Survival.Web.Services;

/// <summary>
/// B34a（S14 2026-11-13 B34 主要节 Q570；逐字稿同节）：全屏战斗操作台下方、默认收起的「战斗信息／战报」阅读区，
/// 以及从它进入的只读子页（预测详情、战斗记录、操作提示）的定位与返回焦点；Q529 无目标指令新选中后把焦点交给实际确认按钮一次。
/// 这些都是界面状态：不进存档、不推进时间、不提交指令；新的一场或读档回到默认收起。
/// </summary>
public sealed partial class GameSession
{
    private string? _infoPage;
    private int _infoBattle = -1;

    /// <summary>阅读区当前页签：null 收起，"info" 战斗信息，"log" 战报。只对当前这一场有效（换场自动视为收起）。</summary>
    public string? BattleInfoPage => State != null && S.Battle != null && S.Battle.BattleId == _infoBattle ? _infoPage : null;

    /// <summary>下一次渲染后要聚焦的元素 id（阅读区页签、入口、全记录、摘要行等）；BattleScreen 取走后清空。</summary>
    public string? BattleFocusId { get; set; }

    /// <summary>下一次渲染后要把阅读区滚进视野（展开时）或把战斗内容区滚回顶部（收起时）。BattleScreen 取走后清空。</summary>
    public string? BattleScrollTo { get; set; }

    /// <summary>
    /// 点主画面入口「战斗信息」「战报」或展开区页签：只展开／切到对应页并聚焦该页签，不选人、不确认、不改瞄准（Q570-A02）。
    /// 点同一页签保持本页。对白锁时不开。
    /// </summary>
    public void OpenBattleInfo(string page)
    {
        if (State == null || S.Battle == null || DialogueLocked) return;
        if (page is not ("info" or "log")) return;
        var wasOpen = BattleInfoPage != null;
        _infoPage = page;
        _infoBattle = S.Battle.BattleId;
        BattleFocusId = "bi-tab-" + page;
        if (!wasOpen) BattleScrollTo = "info";
        Notify();
    }

    /// <summary>「收起」或 Esc：只收起阅读区，焦点回原入口、战斗内容滚回原阅读位置；保留仍合法的指令与目标（Q570-A02）。</summary>
    public void CollapseBattleInfo()
    {
        var was = BattleInfoPage;
        if (was == null) return;
        _infoPage = null;
        BattleFocusId = "bi-entry-" + was;
        BattleScrollTo = "top";
        Notify();
    }

    /// <summary>战斗记录子页打开时要定位的记录（本场日志下标）；null 表示默认定位最新一条。</summary>
    public int? BattleLogLocate { get; private set; }

    /// <summary>只读子页关闭后焦点回到的发起控件 id（「全记录」「预测详情」「查看提示」或原摘要行）。</summary>
    private string? _drawerReturnFocus;

    /// <summary>
    /// 从阅读区或主画面打开只读子页（不切换：已开着同一页时保持）：log 战斗记录（可带定位下标）、detail 预测详情、hint 操作提示。
    /// 0 时间 0AP，不执行命令；不新增阅读暂停（只有说明与菜单是阅读暂停）。
    /// </summary>
    public void OpenBattleSub(string drawer, string returnFocus, int? locate = null)
    {
        if (State == null || S.Battle == null || DialogueLocked) return;
        BattleLogLocate = drawer == "log" ? locate : null;
        _drawerReturnFocus = returnFocus;
        if (BattleDrawer != drawer) OpenBattleDrawer(drawer);
        else Notify();
    }

    /// <summary>关闭只读子页（「返回战斗」或 Esc）：焦点回发起控件；发起的摘要行已不在时由界面回到战报标题。</summary>
    private void AfterBattleDrawerClosed()
    {
        BattleLogLocate = null;
        if (_drawerReturnFocus != null) BattleFocusId = _drawerReturnFocus;
        _drawerReturnFocus = null;
    }

    /// <summary>Q529：新选中可确认的无目标指令后，渲染完成把焦点移到实际确认按钮一次（按钮不存在或禁用时不移）。</summary>
    public bool BattleConfirmFocusPending { get; set; }

    private void ResetBattleInfoUi()
    {
        _infoPage = null;
        _infoBattle = -1;
        BattleLogLocate = null;
        _drawerReturnFocus = null;
        BattleConfirmFocusPending = false;
    }
}
