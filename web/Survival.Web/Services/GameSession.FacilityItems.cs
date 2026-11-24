using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Web.Services;

/// <summary>
/// B51c2b「被动设施卡片箭头、余料与拖入」的网页壳状态（S11／S07／S14「2026-11-17 B51c2a／B51c2b完整两原批」§Q925 B、§Q926 A）：
/// 晾制位、陶窑／净水台批内物品卡点本体——右栏看本卡详情（只读），面板里就地打开数量取出窗（标题「取出」、去向「取到随身／取到地面」、确认「确认 {数量}」）；
/// 材料卡拖到晾制位——选中该位并定位到用这件物品的食谱预览（只导航，仍要「投料…」「确认投料」）。
/// </summary>
public sealed partial class GameSession
{
    /// <summary>设施物品卡的数量取出草稿：打开时绑定的卡实例与身份戳、所选去向（默认随身）、输入的数量文字。</summary>
    public sealed class FacilityTakeDraft
    {
        public int InstanceId { get; init; }
        public string Stamp { get; init; } = "";
        public bool ToCarried { get; set; } = true;
        public string Qty { get; set; } = "1";
    }

    /// <summary>当前打开的设施物品卡数量取出窗；没有为 null。</summary>
    public FacilityTakeDraft? FacTake { get; private set; }

    /// <summary>所选对象是晾制位或陶窑／净水台批内的物品卡（右栏只读、不弹选卡操作盘，操作在面板里的数量窗）。</summary>
    public bool IsFacilityItemSel(Selection sel) =>
        State != null && sel.Kind == SelectionKind.Tool && sel.InstanceId is int id
        && E.FindItem(S, id) is { } it && GameEngine.IsFacilityItem(it);

    /// <summary>
    /// 点设施物品卡本体（Q925 B）：选中并在右栏看这张卡的真实详情，同时在面板里打开这张卡的数量取出窗（绑定此刻的卡实例与位／批身份）。
    /// 再点同一张卡收起数量窗。
    /// </summary>
    public void PickFacilityItem(ItemInstance it)
    {
        if (State == null) return;
        if (FacTake?.InstanceId == it.InstanceId)
        {
            FacTake = null;
            Notify();
            return;
        }
        SyncRightSilently(new Selection { Kind = SelectionKind.Tool, Id = it.ItemId, Owner = it.Owner, InstanceId = it.InstanceId });
        Notice = null;
        FacTake = new FacilityTakeDraft { InstanceId = it.InstanceId, Stamp = E.FacilityItemStamp(S, it) };
        Notify();
    }

    /// <summary>
    /// B51c2b 试玩遗留 N1（S14 Q487／B51c2b.7「工作区详细预估在右栏」；S11 §Q925 B 只在点卡本体时看本卡详情）：
    /// 设施物品卡只在打开它的面板里可见——离开这一层面板（关闭、换面板）或在工作区点配方行时，收起数量窗并清掉这张卡的选中，
    /// 右栏回到工作区的配方预估，不再停在已看不见的卡上。
    /// </summary>
    private void DropFacilityItemSel()
    {
        FacTake = null;
        if (IsFacilityItemSel(Selected))
        {
            Selected = new Selection();
            CardPopCovered = false;
        }
    }

    public void SetFacilityTakeDest(bool toCarried)
    {
        if (FacTake == null) return;
        FacTake.ToCarried = toCarried;
        Notify();
    }

    public void CancelFacilityTake()
    {
        FacTake = null;
        Notify();
    }

    /// <summary>
    /// 数量窗「确认 {数量}」：按打开时绑定的卡与身份戳提交（同一事务 <see cref="GameEngine.TakeFacilityItem"/>）；失败原因写进窗内（Notice），不改去向、不缩量。
    /// 成功收起数量窗。
    /// </summary>
    public ActionResult ConfirmFacilityTake(int count)
    {
        if (State == null || FacTake is not { } d) return ActionResult.Fail("没有进行中的游戏");
        var zone = d.ToCarried ? ItemInstance.Carried : S.Ground;
        var res = Instant(InstantRequest.FacilityTake(d.InstanceId, count, zone, d.Stamp));
        if (res.Ok)
        {
            FacTake = null;
            Notify();
        }
        return res;
    }

    // ───────────── Q926 A：拖到晾制位只做导航 ─────────────

    /// <summary>晾制架面板选中的位（「查看配方」或拖入定位）；面板重新打开时清空。</summary>
    public int? DryingPickSlot { get; set; }
    /// <summary>拖入定位到的食谱（用这件物品的原合法晾制食谱；多条都列）；没有为 null。</summary>
    public List<string>? DryingFocusRecipes { get; set; }
    /// <summary>面板下次渲染后把食谱区滚到可见（只影响定位）。</summary>
    public bool DryingScrollPending { get; set; }

    /// <summary>
    /// 材料卡拖到晾制位松手（Q926 A）：选中这一位并定位到对应合法食谱预览；不开工、不预留、不搬物。面板没开就打开晾制架面板。
    /// </summary>
    public void FocusDrying(int slot, List<string> recipes)
    {
        DryingPickSlot = slot;
        DryingFocusRecipes = recipes;
        DryingScrollPending = true;
        if (Panel != "drying") OpenPanel("drying");
        Notify();
    }
}
