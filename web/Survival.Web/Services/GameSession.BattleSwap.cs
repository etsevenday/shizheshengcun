using Survival.Core.Engine;

namespace Survival.Web.Services;

/// <summary>
/// B34（S14 文首「2026-11-13 B34 Q573—Q580」§Q578）：通用区的「调整站位」合成一张卡。点卡只进入本次站位选择（不移动、不扣费），
/// 我方真实合法的目的号位亮起；明确点（或键盘激活）亮起的号位才按原移动请求提交一次（内核原 swap 指令，目的槽由 ItemInstanceId 带上），
/// 号位、交换、AP 与体力、失败原因全部沿内核原判定。未指定时不代选；提交失败给原具体原因、不自动换别的号位。
/// </summary>
public sealed partial class GameSession
{
    /// <summary>正在为「调整站位」选目的号位（选了卡、还没指定号位）。</summary>
    public bool BattleSwapAiming => State != null && S.Battle != null && BattleAction == "swap" && BattleItem == null;

    /// <summary>当前行动者真实合法的目的号位（与内核每个目的槽一条的 swap 选项同源，只取可用的）。</summary>
    public IReadOnlyCollection<int> SwapAimSlots() =>
        !BattleSwapAiming ? Array.Empty<int>()
        : E.BattleOptions(S).Where(o => o.Action == "swap" && o.Available && o.ItemInstanceId is int).Select(o => o.ItemInstanceId!.Value).ToList();

    /// <summary>鼠标悬停或键盘聚焦中的目的号位（只读预览用，不提交）。</summary>
    public int? SwapHoverSlot { get; private set; }

    public void HoverSwapSlot(int? pos)
    {
        if (SwapHoverSlot == pos) return;
        SwapHoverSlot = pos;
        Notify();
    }

    /// <summary>某个目的号位对应的内核原选项（名称「调整站位→{号位}」、AP、体力与不可用原因同源）。</summary>
    public BattleCommandOption? SwapOptionFor(int pos) =>
        State == null || S.Battle == null ? null : E.BattleOptions(S).FirstOrDefault(o => o.Action == "swap" && o.ItemInstanceId == pos);

    /// <summary>
    /// 明确选了一个目的号位：合法就按原请求提交一次（同一 requestId 防连点）；不代选别的号位。
    /// BM7a（S14 Q634）：不合法号位不可选——点击／键盘激活什么也不做：不移动、不扣费、不退出选位、不清草稿，也不新增提示。
    /// 提交没成功（目的地被占、费用不够等重验失败）时回到选位状态，原因照原文显示。
    /// </summary>
    public async Task ClickSwapSlot(int pos, string token)
    {
        if (!BattleSwapAiming || BattleBusy) return;
        if (!SwapAimSlots().Contains(pos)) return;
        BattleItem = pos;
        SwapHoverSlot = null;
        await ExecuteBattleCommand(token);
        if (State != null && BattleAction == "swap" && BattleItem == pos) { BattleItem = null; Notify(); }
    }
}
