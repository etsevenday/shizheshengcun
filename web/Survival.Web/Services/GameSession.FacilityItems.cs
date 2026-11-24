using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Web.Services;

/// <summary>
/// B51c2b「被动设施卡片箭头、余料与拖入」的网页壳状态（S11／S07／S14「2026-11-17 B51c2a／B51c2b完整两原批」§Q925 B、§Q926 A）：
/// 晾制位、陶窑／净水台批内物品卡点本体——右栏看本卡详情（只读），面板里就地打开数量取出窗（标题「取出」、去向「取到随身／取到地面」、确认「确认 {数量}」）；
/// B59a（§Q1029）起材料卡拖到晾制位即投料（内核 LoadDrying），不再只导航；晾制架栏位可展开、逐份显示（§Q1034／§Q1035）。
/// </summary>
public sealed partial class GameSession
{
    /// <summary>设施物品卡的数量取出草稿：打开时绑定的卡实例与身份戳、所选去向（默认随身）、输入的数量文字。</summary>
    public sealed class FacilityTakeDraft
    {
        public int InstanceId { get; init; }
        /// <summary>B59a（§Q1035「原料每份独立显示、不叠卡」）：晾制位里同一实例逐份显示时点的是第几份（0 起）；陶窑／净水台批内卡为 null。</summary>
        public int? Unit { get; init; }
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
    public void PickFacilityItem(ItemInstance it, int? unit = null)
    {
        if (State == null) return;
        if (FacTake?.InstanceId == it.InstanceId && FacTake.Unit == unit)
        {
            FacTake = null;
            Notify();
            return;
        }
        SyncRightSilently(new Selection { Kind = SelectionKind.Tool, Id = it.ItemId, Owner = it.Owner, InstanceId = it.InstanceId });
        Notice = null;
        FacTake = new FacilityTakeDraft { InstanceId = it.InstanceId, Unit = unit, Stamp = E.FacilityItemStamp(S, it) };
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

    /// <summary>物主是晾制位或陶窑／净水台的批（设施物品卡原来所在）。</summary>
    private static bool IsFacilityOwner(string owner) =>
        ItemInstance.TryDrying(owner, out _, out _) || ItemInstance.TryStation(owner, out _, out _);

    /// <summary>
    /// B51c2b 试玩遗留 N2（S14 §9.2 Q5-2「设施整锅／直烤位／锅内子物箭头→随身1份或1实例；不能弹操作条」；S11 §Q925 B 数量窗确认只是移动）：
    /// 选中的设施物品卡整张被取走（到随身、到地面，或并进已有的堆）后，选中不跟到新位置的卡，免得自动弹出随身卡的操作窗；
    /// 面板开着时右栏页签不动（陶窑工作区回到配方预估），没开面板时同原程序清空回人物页。
    /// </summary>
    private void DropTakenFacilityItemSel()
    {
        var tab = DetailTab;
        ClearSelectionSilently();
        if (Panel != null) DetailTab = tab;
        FacTake = null;
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

    // ───────────── 试玩遗留 N3：被动设施面板只盖①②两行 ─────────────

    /// <summary>
    /// B51c2b 试玩遗留 N3（S11／S14 §Q778「火堆主层覆盖①地图②设施……原地面③与随身④的可见及原权限不变。其余被动设施……不覆盖另加的区域」；
    /// Q1028 答复同样只盖设施栏与地图栏；S11 §Q925 B 设施物品卡「可拖到…本设施所在地面」）：
    /// 火堆／柴炉加工菜单、晾制架面板、陶窑／固定净水台工作区开着时，③当前地面与④随身照常可见、可作拖放落点。
    /// </summary>
    public bool PassivePanelOpen => State != null
        && (Panel is "drying" or FireMenuPanel || (Panel == "craft" && CraftSite?.StationId is "kiln" or "water_station"));

    /// <summary>
    /// 中部四行网格上标面板覆盖范围的样式类：被动设施面板（<see cref="PassivePanelOpen"/>）不加类，只在①②合并区；
    /// 整备、成长盖四行（full-panel）；其余手工制作、安全屋、城市层盖①②③（tall-panel），沿原排版。
    /// </summary>
    public string PanelRowsClass =>
        PassivePanelOpen ? ""
        : Panel is "gear" or "growth" ? "full-panel"
        : Panel is "craft" or "safehouse" || CityBrowsing || (Panel == "city" && CityPanelTall) ? "tall-panel"
        : "";

    // ───────────── 晾制架面板：选位与栏位展开 ─────────────

    /// <summary>晾制架面板选中的位（空位或余料位的「查看配方」）；面板重新打开时清空。B59a（§Q1029）起拖入即投料，不再由拖入定位。</summary>
    public int? DryingPickSlot { get; set; }
    /// <summary>面板下次渲染后把食谱区滚到可见（只影响定位）。</summary>
    public bool DryingScrollPending { get; set; }

    /// <summary>
    /// B59a（S11「2026-11-24」§Q1034 用户另定「晾晒架的每个栏位都可点开查看内容」、§Q1035「展开晾晒架栏位后原料每份独立显示」）：
    /// 晾制架面板里当前展开的栏位（位号）；面板重新打开时全部收起。
    /// </summary>
    public HashSet<int> DryingOpenSlots { get; } = new();

    /// <summary>点栏位标题：展开／收起这一栏（只看，不改状态、不推进时间）。</summary>
    public void ToggleDryingSlot(int slot)
    {
        if (!DryingOpenSlots.Remove(slot)) DryingOpenSlots.Add(slot);
        Notify();
    }
}
