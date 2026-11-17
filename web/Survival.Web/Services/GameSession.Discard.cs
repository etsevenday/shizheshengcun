using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Web.Services;

/// <summary>B53a 永久丢弃子区所在的层：主菜单（选择数量／全部丢弃）→ 数量 → 最终确认。</summary>
public enum DiscardStage { Menu, Quantity, Confirm }

/// <summary>
/// B53a（S14 2026-11-15 B50a、2026-11-17 B53）：右栏「丢弃」／「丢弃水」的会话草稿。只在会话内；换选中对象、清选中、读档即作废。
/// 打开子区、选数量、看确认都不改游戏状态；只有最终「确认丢弃」带确认时的快照和请求号提交一次。
/// 数量 → 确认这一段与目标无关，B53b 无右栏清单行末「丢弃」可直接用 <see cref="OpenDiscard"/> 复用。
/// </summary>
public sealed class DiscardDraft
{
    public DiscardTarget Target { get; init; } = null!;
    /// <summary>发起时的选中对象键（换卡即作废）。</summary>
    public string SelectionKey { get; init; } = "";
    public DiscardStage Stage { get; set; } = DiscardStage.Menu;
    /// <summary>数量路径的整数 N（输入框原文另存，便于提示非法）。</summary>
    public string QuantityText { get; set; } = "1";
    /// <summary>本次确认走的是「全部丢弃」。</summary>
    public bool All { get; set; }
    /// <summary>确认层固定的真实清单与快照。</summary>
    public DiscardPlanView? Plan { get; set; }
    /// <summary>当前层的拒绝原因（数量非法、确认后变化等）。</summary>
    public string? Error { get; set; }
    /// <summary>本次确认的请求号：连点、重发只生效一次。</summary>
    public string RequestId { get; set; } = Guid.NewGuid().ToString("N");
}

public sealed partial class GameSession
{
    private DiscardDraft? _discard;

    /// <summary>仍属于当前选中对象的丢弃草稿（换卡后旧草稿不再出现）。</summary>
    public DiscardDraft? Discarding => _discard != null && _discard.SelectionKey == Selected.Key ? _discard : null;

    /// <summary>这一目标的丢弃子区开着。</summary>
    public bool DiscardOpenFor(DiscardTarget t) => Discarding is DiscardDraft d && d.Target.Key == t.Key;

    /// <summary>点「丢弃」／「丢弃水」：只开子区，不删除。</summary>
    public void OpenDiscard(DiscardTarget t)
    {
        if (State == null) return;
        _discard = new DiscardDraft { Target = t, SelectionKey = Selected.Key };
        Notify();
    }

    /// <summary>子区当前数据（可丢弃数、上限、拒绝原因）。</summary>
    public DiscardView DiscardViewOf(DiscardTarget t) => E.DiscardViewOf(S, t);

    /// <summary>「选择数量」：进入数量层，默认 1。</summary>
    public void DiscardChooseQuantity()
    {
        if (Discarding is not DiscardDraft d) return;
        d.Stage = DiscardStage.Quantity;
        d.QuantityText = "1";
        d.Error = null;
        Notify();
    }

    public void DiscardSetQuantity(string text)
    {
        if (Discarding is not DiscardDraft d) return;
        d.QuantityText = new string(text.Where(char.IsDigit).ToArray());
        d.Error = null;
        Notify();
    }

    public void DiscardStep(int k)
    {
        if (Discarding is not DiscardDraft d) return;
        var max = Math.Max(1, E.DiscardViewOf(S, d.Target).MaxUnits);
        var n = int.TryParse(d.QuantityText, out var v) ? v : 0;
        d.QuantityText = Math.Clamp(n + k, 1, max).ToString();
        d.Error = null;
        Notify();
    }

    /// <summary>数量层「下一步」：按 N 取本次真实清单进确认层；数量非法给原句，不进确认。</summary>
    public void DiscardNext()
    {
        if (Discarding is not DiscardDraft d) return;
        var n = int.TryParse(d.QuantityText, out var v) ? v : 0;
        ToConfirm(d, n, false);
    }

    /// <summary>「全部丢弃」：直接取本次范围全部合法份额进确认层。</summary>
    public void DiscardAll()
    {
        if (Discarding is not DiscardDraft d) return;
        ToConfirm(d, 0, true);
    }

    private void ToConfirm(DiscardDraft d, int n, bool all)
    {
        var plan = E.DiscardPreview(S, d.Target, n, all, out var why);
        if (plan == null) { d.Error = why; Notify(); return; }
        d.All = all;
        d.Plan = plan;
        d.Stage = DiscardStage.Confirm;
        d.Error = null;
        d.RequestId = Guid.NewGuid().ToString("N");
        Notify();
    }

    /// <summary>「返回」／ESC：每次退一层（确认 → 数量或主菜单 → 收起子区），都不删除。</summary>
    public void DiscardBack()
    {
        if (Discarding is not DiscardDraft d) return;
        d.Error = null;
        switch (d.Stage)
        {
            case DiscardStage.Confirm:
                d.Stage = d.All ? DiscardStage.Menu : DiscardStage.Quantity;
                d.Plan = null;
                break;
            case DiscardStage.Quantity:
                d.Stage = DiscardStage.Menu;
                break;
            default:
                _discard = null;
                break;
        }
        Notify();
    }

    /// <summary>「取消」：收起整个子区，不删除。</summary>
    public void CancelDiscard()
    {
        _discard = null;
        Notify();
    }

    /// <summary>
    /// 「确认丢弃」：带确认时的快照与请求号提交一次。成功收起子区（整卡删光时选中随之清空，部分剩余刷新原详情）；
    /// 失败（确认后有变化等）退回子区主层并写原因，不部分删除。
    /// </summary>
    public ActionResult ConfirmDiscard()
    {
        if (Discarding is not DiscardDraft d || d.Plan == null) return ActionResult.Fail(DiscardCopy.Changed);
        if (CityBrowsing) return CityReadOnlyFail();
        if (FormationOpen) return Commit(ActionResult.Fail(FormationOpenText));
        var res = E.Discard(S, new DiscardRequest { Target = d.Target, Count = d.All ? 0 : d.Plan.Count, All = d.All, Stamp = d.Plan.Stamp, RequestId = d.RequestId });
        if (res.Ok)
        {
            _discard = null;
            Notice = null;
            Drain();
            ReconcileSelection();
            Commit();
        }
        else
        {
            d.Stage = DiscardStage.Menu;
            d.Plan = null;
            d.Error = res.Reason;
        }
        Notify();
        return res;
    }

    /// <summary>选中对象变了、清空选中或读档：丢弃草稿作废。</summary>
    private void DropDiscard() => _discard = null;
}
