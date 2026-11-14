namespace Survival.Web.Services;

/// <summary>
/// BM7a（S14 文首「2026-11-14 Q629—Q636」§Q632、R03 同批节、逐字稿同批表）：选好攻击技能后，悬停亮起的合法敌人（或键盘把它指定为明确目标），
/// 在该敌人立绘旁浮出本次「战斗信息」只读浮框，右侧信息栏（操作台右块之上的只读叠层）显示该敌人的公开详细属性。
/// 两者与下方「战斗信息」「预测详情」共用同一次 PreviewCommand（同一行动者、技能、明确目标与实际承伤）；只读：不选人、不提交、不扣费、不抽随机。
/// 浮框不接焦点、不成 ESC 层；鼠标离开合法目标即撤，右栏沿明确目标保留（BM2 Q497：移出锚点不丢目标）。
/// 播放、对白、遭遇提示、菜单、只读子页接管，或目标失效／换技能／取消瞄准时都不显示，重返后按当前合法上下文重建。
/// </summary>
public sealed partial class GameSession
{
    /// <summary>最近一次由键盘（Tab 聚焦敌人、←／→、初入瞄准时的合法键盘焦点）明确指定的目标与当时的瞄准上下文。</summary>
    private int? _aimKbd;
    private string? _aimKbdCtx;

    private void MarkAimKeyboard(int index)
    {
        _aimKbd = index;
        _aimKbdCtx = AimCtx();
        _aimRailHidden = null;   // 试玩修复（M891）：键盘重新指定目标时右栏叠层回来
    }

    /// <summary>试玩修复（M891，S14 Q632「悬停亮起的合法敌人……同时右侧信息栏显示该敌人的公开详细属性」）：右栏叠层为让出通用行动区而隐去后，再悬停敌人即重新显示。返回是否有变化。</summary>
    private bool ShowAimRailAgain()
    {
        if (_aimRailHidden == null) return false;
        _aimRailHidden = null;
        return true;
    }

    private void ClearAimKeyboard() => _aimKbd = null;

    /// <summary>战斗画面上没有更高的层盖着，且正等我方下令（浮框与右栏只在这时出现）。</summary>
    private bool AimPopOpenable =>
        State != null && S.Battle != null && BattleAiming && !DialogueLocked && !PlaybackActive && !EncounterPromptActive
        && !BattleMenuOpen && BattleDrawer == null && !BattleBusy && E.AwaitingPlayer(S);

    /// <summary>
    /// 此刻要在其立绘旁浮出「战斗信息」的敌人（本场序号）；null 不显示。
    /// 单体／成对／贯穿：本次明确目标合法，且鼠标正停在它上面或它是键盘指定的；群体：鼠标或键盘所在的那名合法受影响者（浮框列出全部真实受影响成员）。
    /// </summary>
    public int? AimPopTarget
    {
        get
        {
            if (!AimPopOpenable) return null;
            var set = AimTargets();
            var kbd = _aimKbd != null && _aimKbdCtx == AimCtx() ? _aimKbd : null;
            if (AreaAction)
            {
                var at = BattleHover ?? kbd;
                return at is int a && set.Contains(a) ? a : null;
            }
            if (BattleAimTarget is not int t || !set.Contains(t)) return null;
            return BattleHover == t || kbd == t ? t : null;
        }
    }

    /// <summary>
    /// 右侧信息栏此刻显示其公开属性的敌人：本次瞄准的明确目标（合法才显示；鼠标移开后沿原只读跟随／保留，不随悬停漂移）；群体按最近指定的那名受影响者。
    /// 鼠标移进右栏时它让开（<see cref="HideAimRail"/>），直到明确目标换人。
    /// </summary>
    public int? AimRailTarget
    {
        get
        {
            if (!AimPopOpenable || BattleAimTarget is not int t || !AimTargets().Contains(t)) return null;
            return _aimRailHidden == $"{AimCtx()}:{t}" ? null : t;
        }
    }

    private string? _aimRailHidden;

    /// <summary>鼠标移进右栏叠层：叠层让开，露出下面的通用行动（同 B34 灰卡说明的让位做法），直到再悬停敌人、键盘重新指定目标或重新瞄准。</summary>
    public void HideAimRail()
    {
        if (BattleAimTarget is not int t) return;
        _aimRailHidden = $"{AimCtx()}:{t}";
        Notify();
    }
}
