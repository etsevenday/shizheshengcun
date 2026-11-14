using Survival.Core.State;

namespace Survival.Web.Services;

/// <summary>收束后仍开着的本场战斗记录（只读快照：已提交的全部记录原文、旧档是否可能缺早段）。</summary>
public sealed record EndedBattleLogView(IReadOnlyList<string> Lines, bool MayBeIncomplete);

/// <summary>
/// B34 试玩修复 B2（S14 Q528.2 末段；逐字稿「自然战斗结束、已打开记录页的状态」「自然战斗已收束时记录页返回」；05 F06-R）：
/// 自然胜利／撤退收束时，已打开的「战斗记录」子页持有本场最终只读内容直到返回，标「本场战斗已结束。」，返回改「返回当前画面」；
/// 不自动跳看另一场。死亡终局、强制对白、读档／新局、连场（新场次）按原规则优先关闭旧阅读层，不持有。
/// </summary>
public sealed partial class GameSession
{
    /// <summary>当前打开的只读子页属于哪一场（收束后 GameState.Battle 已清空，从这里取本场记录）。</summary>
    private BattleState? _drawerBattle;

    /// <summary>收束后仍开着的本场战斗记录；null＝没有。</summary>
    public EndedBattleLogView? EndedLog { get; private set; }

    /// <summary>战斗收束那一刻（<see cref="OnBattleEnded"/> 收界面之前）：全记录子页开着且是自然胜利／撤退，就持有本场记录。</summary>
    private EndedBattleLogView? TakeEndedLog()
    {
        var b = _drawerBattle;
        _drawerBattle = null;
        if (State == null || BattleDrawer != "log" || b == null) return null;
        if (S.Battle != null || S.Progress.Dead || DialogueLocked) return null;   // 连场新场次、死亡终局、强制对白优先
        return new EndedBattleLogView(b.Log.ToList(), b.LogMayBeIncomplete);
    }

    /// <summary>「返回当前画面」／ESC：关闭收束后的只读记录页。</summary>
    public void CloseEndedLog()
    {
        if (EndedLog == null) return;
        EndedLog = null;
        Notify();
    }
}
