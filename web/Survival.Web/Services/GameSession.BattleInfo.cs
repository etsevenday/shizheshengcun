using Survival.Core.Engine;
using Survival.Core.State;

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
    /// BM9（S14／R03 Q708）：鼠标点开（<paramref name="byPointer"/>）且原我方待确认指令仍合法、没有更高层时，焦点回战场背景（<see cref="BattleSafeFocusId"/>），
    /// 不留在页签上——保留展开的信息、已选指令、行动者、武器页与明确目标，打开本身不执行；下一次独立空格／Enter 才走原唯一确认。
    /// 键盘（Tab 聚焦后激活）、未选指令时仍按原规则聚焦页签。
    /// </summary>
    public void OpenBattleInfo(string page, bool byPointer = false)
    {
        if (State == null || S.Battle == null || DialogueLocked) return;
        if (page is not ("info" or "log")) return;
        var wasOpen = BattleInfoPage != null;
        _infoPage = page;
        _infoBattle = S.Battle.BattleId;
        BattleFocusId = byPointer && BattlePendingConfirm ? BattleSafeFocusId : "bi-tab-" + page;
        if (!wasOpen) BattleScrollTo = "info";
        Notify();
    }

    /// <summary>BM9（Q708）：战场根元素 id（BattleScreen 的 .bfs，tabindex=-1）——鼠标打开战斗信息后的安全战场背景焦点。</summary>
    public const string BattleSafeFocusId = "bfs-root";

    /// <summary>
    /// BM9（Q708）：原合法待确认情境——轮到我方、已选指令（瞄准或无目标待确认，不含调整站位选号位），无播放、菜单、子页、医疗小盘等更高层。
    /// 只决定鼠标打开信息后焦点落点，不校验也不提交；确认时仍由原 BattleConfirmHover 同源重验。
    /// </summary>
    private bool BattlePendingConfirm =>
        BattleAction != null && !BattleSwapAiming && !BattleBusy && !PlaybackActive && !BattleMenuOpen && !MedTrayOpen
        && BattleDrawer == null && PendingDrop == null && E.AwaitingPlayer(S);

    /// <summary>「收起」或 Esc：只收起阅读区，焦点回原入口、战斗内容滚回原阅读位置；保留仍合法的指令与目标（Q570-A02）。</summary>
    public void CollapseBattleInfo()
    {
        var was = BattleInfoPage;
        if (was == null) return;
        _infoPage = null;
        _infoObject = null;
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

    // ── B47b（R03 文首 B34 §Q618）：没有选指令时明确点击站位人物／队列头像——展开阅读区并切到「战斗信息」显示该真实对象 ──
    // 只是查看：不程序性移焦点、不滚动战场、不选技／攻击／移动；悬停沿原只读更新内容但不展开／切页；已选指令、播放、对白时不由这里展开。

    /// <summary>最近一次点击查看的对象（"foe:{本场序号}" 或 "ally:{人物ID}"）；它确死／离场后阅读区显示「该对象已离场。」。</summary>
    private string? _infoObject;

    private void RevealBattleInfo(string objectKey)
    {
        if (State == null || S.Battle == null || BattleAction != null || PlaybackActive || DialogueLocked) return;
        _infoPage = "info";
        _infoBattle = S.Battle.BattleId;
        _infoObject = objectKey;
    }

    /// <summary>Q618：点击查看的对象在当前显示（Q617 快照）里已确死／离场——阅读区写「该对象已离场。」，不悄悄换看别人。</summary>
    public bool BattleInfoGone(GameState v)
    {
        if (_infoObject == null || v.Battle == null || v.Battle.BattleId != _infoBattle) return false;
        if (_infoObject.StartsWith("foe:")) return !(int.TryParse(_infoObject[4..], out var i) && E.BattleEnemyAt(v.Battle, i) is { Alive: true });
        return !GameEngine.AllyInBattle(v.Battle, _infoObject[5..]);
    }

    private void ResetBattleInfoUi()
    {
        _infoObject = null;
        _infoPage = null;
        _infoBattle = -1;
        BattleLogLocate = null;
        _drawerReturnFocus = null;
        BattleConfirmFocusPending = false;
    }
}
