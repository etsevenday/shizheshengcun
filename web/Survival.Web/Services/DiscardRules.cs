using Survival.Core.Engine;

namespace Survival.Web.Services;

/// <summary>B53b：无右栏清单的发起处（行末「丢弃」所在的原窗口）。</summary>
public static class DiscardHost
{
    /// <summary>右栏人物页「成员」组：同伴随身展开行与当地遗物／认领箱／回收箱行（PartyPanel）。</summary>
    public const string Party = "party";
    /// <summary>安全屋面板的柜内行（SafehouseOverlay）。</summary>
    public const string Cabinet = "cabinet";
    /// <summary>远征物资窗的「本次已封存战利品」行（ExpeditionSeal）。</summary>
    public const string Sealed = "sealed";
}

/// <summary>
/// B53b 丢弃子区的纯界面规则（不依赖会话，测试工程直接编译本文件）：清单发起的子区何时仍有效、主层原因行怎么排。
/// </summary>
public static class DiscardRules
{
    /// <summary>
    /// 清单发起的子区只在发起清单的原窗口仍开着时有效（S14 文首 2026-11-17 §Q857 B「复用原窗口内的子操作／确认承载，关闭返回真实发起清单」）：
    /// 人物页成员组＝右栏在人物页；柜子＝安全屋面板；封存＝远征物资窗。
    /// </summary>
    public static bool HostShown(string host, bool characterTab, string? panel) => host switch
    {
        DiscardHost.Party => characterTab,
        DiscardHost.Cabinet => panel == "safehouse",
        DiscardHost.Sealed => panel == "seal",
        _ => false,
    };

    /// <summary>
    /// 主层的原因行（§Q866 B「命中的真实拒绝原因全部逐条显示，相同句去重、顺序稳定」）：上一步的拒绝（数量非法、确认后有变化等）在前，
    /// 再按内核给的顺序列本次范围命中的全部原因，相同句只写一次。
    /// </summary>
    public static List<string> WhyLines(string? error, IEnumerable<string> reasons)
    {
        var lines = new List<string>();
        if (error != null) lines.Add(error);
        foreach (var r in reasons)
            if (!lines.Contains(r)) lines.Add(r);
        return lines;
    }
}
