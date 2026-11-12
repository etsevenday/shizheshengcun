using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Web.Services;

/// <summary>
/// B41e2 新版敌踪界面二（开关 encounters.enemyCardsV2 开时才有入口）：成长页的练习潜行盘、编队页的小队队长草稿。
/// 规则与原句全部来自内核（CanPracticeStealth、PracticeXpPreview、PracticeBodyRateText、SaveFormationBlocked 的 leaderId 分支）。
/// </summary>
public sealed partial class GameSession
{
    // ───────────── 成长页「练习潜行」盘（逐字稿 B41「队长与练习潜行」；R05 Q424、Q540） ─────────────

    /// <summary>成长页里练习盘开着（未提交草稿）。</summary>
    public bool PracticeOpen { get; private set; }

    /// <summary>练习盘里明确选中的练习者（未选为 null，开始按钮写「请选择练习者。」）。</summary>
    public string? PracticeActor { get; private set; }

    /// <summary>点「练习潜行」：打开练习盘，草稿从未选人开始（逐字稿：明确选练习者）。</summary>
    public void OpenPractice()
    {
        if (State == null || !E.CardsV2(S)) return;
        PracticeOpen = true;
        PracticeActor = null;
        Notify();
    }

    /// <summary>盘内「返回」：只放弃未提交草稿。</summary>
    public void ClosePractice()
    {
        PracticeOpen = false;
        PracticeActor = null;
        Notify();
    }

    /// <summary>离开成长页时丢弃练习盘草稿（不通知重绘，由调用方统一重绘）。</summary>
    private void ClosePracticeDraft()
    {
        PracticeOpen = false;
        PracticeActor = null;
    }

    /// <summary>在盘里选练习者（再点同一人不取消，换人即换）。</summary>
    public void PickPracticeActor(string actorId)
    {
        PracticeActor = actorId;
        Notify();
    }

    /// <summary>「开始练习」的禁用原因（内核 CanPracticeStealth，按逐字稿禁用表取第一条）；null 为可开始。</summary>
    public string? PracticeBlocked => State == null ? "没有进行中的游戏" : E.CanPracticeStealth(S, PracticeActor);

    /// <summary>「开始练习」：提交 30 分钟练习（不另加确认）；成功后收盘、关成长页，中央行动位接管。</summary>
    public async Task<ActionResult> StartPractice()
    {
        if (State == null) return ActionResult.Fail("没有进行中的游戏");
        if (FormationOpen) { Notice = FormationOpenText; Notify(); return ActionResult.Fail(FormationOpenText); }
        var res = E.StartPractice(S, PracticeActor, Guid.NewGuid().ToString("N"));
        if (res.Duplicate) return res;
        if (!res.Ok)
        {
            Notice = res.Reason;
            Notify();
            return res;
        }
        Notice = null;
        PracticeOpen = false;
        PracticeActor = null;
        if (Panel == "growth") OpenPanel(null);
        Drain();
        Commit();
        Notify();
        await RunLoop();
        return res;
    }

    // ───────────── 编队页「小队队长」（逐字稿 B41「队长与练习潜行」；R08 Q342） ─────────────

    /// <summary>编队草稿里的小队队长（开关开时打开编队面板按当前队长建草稿；开关关为 null，不提交队长）。</summary>
    public string? FormationLeader { get; private set; }

    /// <summary>草稿里选小队队长（只改草稿，随「保存编队」一起提交）。</summary>
    public void PickFormationLeader(string actorId)
    {
        if (FormationDraft == null || State == null || !E.CardsV2(S) || !FormationDraft.Contains(actorId)) return;
        FormationLeader = actorId;
        Notify();
    }

    /// <summary>队长草稿是否与已保存的队长不同。</summary>
    private bool LeaderDirty => State != null && FormationLeader != null && E.CardsV2(S) && FormationLeader != E.SquadLeaderId(S);
}
