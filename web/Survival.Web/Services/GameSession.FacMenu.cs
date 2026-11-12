using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Web.Services;

/// <summary>
/// B45「设施加工移至中央次级菜单、右栏只读」（S14「2026-11-12 用户直接裁定：设施加工改在中央次级菜单操作」、Q542—Q548、Q556—Q562）：
/// 火堆／柴炉卡点击打开中央加工菜单（一级面板 "fire"，盖住中部四轨），菜单自己记住设施地点／区域／身份、工位、锅与内容、来源与数量草稿，
/// 不从右栏当前显示对象取执行目标；右栏只做只读同步（<see cref="SyncRight"/>）。工坊汇总卡多座加工设施时先开「选择设施」盘（Pop "stations"）。
/// 全部只是界面状态，不进存档；关闭、换区、战斗、读档等清空。
/// </summary>
public sealed partial class GameSession
{
    public const string FireMenuPanel = "fire";
    /// <summary>Q542：工坊汇总卡的「选择设施」盘（就近盘种类）。</summary>
    public const string StationsPop = "stations";

    /// <summary>中央火堆加工菜单的上下文（只在会话内）。</summary>
    public sealed class FireMenuContext
    {
        public string Location { get; init; } = "";
        public string Ground { get; init; } = "";
        /// <summary>明确点开的加工位（0 起）；null = 设施主层。</summary>
        public int? Slot { get; set; }
        /// <summary>进入该加工位时位里的实例（空位为 null），用来发现位里内容变了。</summary>
        public int? SlotItem { get; set; }
        /// <summary>锅里明确点开的水（PotLiquid）或食材（Tool）；null = 锅这一层。</summary>
        public Selection? Inner { get; set; }
        /// <summary>明确选中的原料来源实例（随身或当前地面）。</summary>
        public int? SourceId { get; set; }
        /// <summary>Q545：通用来源数量草稿（原文，不悄悄取整）。</summary>
        public string Qty { get; set; } = "1";
        /// <summary>「内容已变化，请重新选择。」等一次性提示；下一次明确选择时清掉。</summary>
        public string? Lost { get; set; }
        /// <summary>设施真实消失（「这处设施已无法操作，请关闭后重新选择。」）。</summary>
        public bool Dead { get; set; }
        /// <summary>上次核对时的世界分钟：不是 0 分钟自己操作造成的变化才提示内容已变化。</summary>
        public int Minute { get; set; }
    }

    public FireMenuContext? FireMenu { get; private set; }

    /// <summary>当前菜单层级：0 设施主层、1 加工位（空位／直烤／煅烧批）或锅、2 锅里的水或食材。</summary>
    public int FireMenuLevel => FireMenu is not { } m ? 0 : m.Inner != null ? 2 : m.Slot != null ? 1 : 0;

    /// <summary>关闭菜单后待恢复焦点的元素 id（GameScreen 渲染后读一次即清）。</summary>
    public string? FocusIdPending { get; set; }

    /// <summary>Q560：本次手工制作是经「选择设施」明确点一座进入的（返回／ESC 回选择层）；换别的面板即结束。</summary>
    public bool CraftFromChooser { get; private set; }

    /// <summary>
    /// 点火堆／柴炉卡主体（或手工制作「查看火堆 ▸」，Q547）：重验当前区真实火源，打开中央加工菜单两加工位主层，不预选锅或空位、不投料。
    /// 右栏同步该设施只读事实；Guide 沿原「选中火堆」的操作前引导上下文。
    /// </summary>
    public void OpenFireMenu()
    {
        if (State == null || S.Progress.Dead || InBattleUi || DialogueLocked || !E.HasFireAt(S, S.Ground)) return;
        HoverPreview = null;
        // 先建上下文再开面板（OpenPanel 里的 Notify 会核对菜单，没有上下文的 fire 面板视为失效）；已开着时重开回主层
        FireMenu = new FireMenuContext { Location = S.CurrentLocation, Ground = S.Ground, Minute = S.GameMinute };
        OpenPanel(FireMenuPanel);
        if (Panel != FireMenuPanel) { FireMenu = null; return; }
        SyncRightSilently(new Selection { Kind = SelectionKind.Facility, Id = GameEngine.FireIdOf(S.Ground) });
        Guide("fire");
        PanelFocusPending = true;
        Notify();
    }

    /// <summary>离开火堆菜单（关闭或换到别的面板）：丢未提交草稿；右栏若停在火堆内容或来源上，回到该火堆（仍在）或清空；关闭时焦点回原火堆卡。</summary>
    private void LeaveFireMenu(bool closing)
    {
        FireMenu = null;
        if (State == null) return;
        var fireSel = new Selection { Kind = SelectionKind.Facility, Id = GameEngine.FireIdOf(S.Ground) };
        if (Selected.Kind != SelectionKind.None && Selected.Key != fireSel.Key)
        {
            if (E.HasFireAt(S, S.Ground)) { Selected = fireSel; SelectedBatchKey = null; }
            else ClearSelectionSilently();
        }
        if (closing) FocusIdPending = E.HasFireAt(S, S.Ground) ? "fire-card" : null;
    }

    /// <summary>
    /// Q548：右栏同步菜单里明确点击的真实对象——只读，不清就近盘／食饮草稿、不给操作前引导、不记任务、不改中央菜单。
    /// </summary>
    public void SyncRight(Selection sel)
    {
        SyncRightSilently(sel);
        Notify();
    }

    private void SyncRightSilently(Selection sel)
    {
        if (sel.Key != Selected.Key) SelectedBatchKey = null;
        Selected = sel;
        CardPopCovered = false;
        PeekClear();
        if (sel.Kind != SelectionKind.None) DetailTab = DetailTab.Object;
    }

    /// <summary>右栏只读层级路径（FireCrumbs）点前面的层级：只换右栏显示，不改中央菜单、不开操作盘（Q548）。</summary>
    public void ReadOnlyCrumb(Selection sel) => SyncRight(sel);

    private Selection FireSelection => new() { Kind = SelectionKind.Facility, Id = GameEngine.FireIdOf(S.Ground) };

    private static Selection ItemSel(GameEngine e, ItemInstance it) =>
        e.Item(it.ItemId).IsStackable && !it.InFacility && !it.InContainer
            ? new Selection { Kind = SelectionKind.ItemGroup, Id = it.ItemId, Owner = it.Owner }
            : new Selection { Kind = SelectionKind.Tool, Id = it.ItemId, Owner = it.Owner, InstanceId = it.InstanceId };

    /// <summary>明确点菜单里的加工位（0 起）：进入该位（空位为放入目标，有内容为内容层），右栏同步位里的真实对象（空位不伪造详情，仍是设施）。</summary>
    public void FireMenuPickSlot(int k)
    {
        if (FireMenu is not { Dead: false } m || State == null) return;
        var slots = E.FireSlots(S, m.Ground);
        if (k < 0 || k >= slots.Count) return;
        if (m.Slot != k) m.Qty = "1";
        m.Slot = k;
        m.SlotItem = slots[k]?.InstanceId;
        m.Inner = null;
        m.Lost = null;
        SyncRight(slots[k] is ItemInstance it ? ItemSel(E, it) : FireSelection);
    }

    /// <summary>明确点锅里的水或食材：进入内容层（收起来源区），右栏同步该对象。</summary>
    public void FireMenuPickInner(Selection sel)
    {
        if (FireMenu is not { Dead: false } m || m.Slot == null) return;
        m.Inner = sel;
        m.Lost = null;
        SyncRight(sel);
    }

    /// <summary>菜单层级路径：点前面的层级回到那一层（0 主层、1 加工位／锅）。</summary>
    public void FireMenuGoLevel(int level)
    {
        if (FireMenu is not { } m || State == null) return;
        if (level <= 0)
        {
            if (m.Slot != null) m.Qty = "1";
            m.Slot = null;
            m.SlotItem = null;
            m.Inner = null;
        }
        else if (level == 1) m.Inner = null;
        m.Lost = null;
        SyncRight(CurrentFireMenuObject());
    }

    /// <summary>Esc／「返回」：退一层（内容→锅／加工位→主层）。</summary>
    public void FireMenuBack() => FireMenuGoLevel(FireMenuLevel - 1);

    /// <summary>当前层对应的真实对象（右栏同步用）：主层与空位为设施本身。</summary>
    private Selection CurrentFireMenuObject()
    {
        if (FireMenu is not { } m) return FireSelection;
        if (m.Inner is { } inner) return inner;
        if (m.Slot is int k && E.FireSlots(S, m.Ground) is var slots && k < slots.Count && slots[k] is ItemInstance it) return ItemSel(E, it);
        return FireSelection;
    }

    /// <summary>明确点原料来源行：只选这个来源批次（不转移），数量回 1，右栏同步该来源。</summary>
    public void FireMenuPickSource(ItemInstance it)
    {
        if (FireMenu is not { Dead: false } m) return;
        if (m.SourceId != it.InstanceId) m.Qty = "1";
        m.SourceId = it.InstanceId;
        m.Lost = null;
        var sel = ItemSel(E, it);
        SyncRightSilently(sel);
        if (sel.Kind == SelectionKind.ItemGroup) SelectedBatchKey = GameEngine.BatchKeyOf(it);
        Notify();
    }

    public void FireMenuSetQty(string text)
    {
        if (FireMenu is not { } m) return;
        m.Qty = text;
    }

    /// <summary>
    /// 菜单失效与内容变化的核对（每次刷新界面前）：换地点／区、战斗、死亡即关闭（丢草稿）；设施消失显示定句只留关闭；
    /// 加工位或锅里所点内容消失回最近合法父层（时间推进造成的才提示「内容已变化，请重新选择。」）；来源离开随身／当前地面即清掉。
    /// </summary>
    private void ReconcileFireMenu()
    {
        if (Panel != FireMenuPanel)
        {
            FireMenu = null;
            return;
        }
        if (FireMenu is not { } m || State == null || S.Progress.Dead || InBattleUi || S.CurrentLocation != m.Location || S.Ground != m.Ground)
        {
            Panel = null;
            FireMenu = null;
            return;
        }
        var passive = S.GameMinute != m.Minute;
        m.Minute = S.GameMinute;
        if (!E.HasFireAt(S, m.Ground))
        {
            m.Dead = true;
            m.Slot = null;
            m.SlotItem = null;
            m.Inner = null;
            m.SourceId = null;
            return;
        }
        m.Dead = false;
        if (m.Slot is int k)
        {
            var slots = E.FireSlots(S, m.Ground);
            var occ = k < slots.Count ? slots[k] : null;
            if (occ?.InstanceId != m.SlotItem)
            {
                // 位里原有的内容没了（取出、吃完、腐败清理）：留在这个加工位（现为空位或新内容）；新放进来的内容直接显示
                if (m.SlotItem != null && passive) m.Lost = GameEngine.ContentChangedText;
                m.SlotItem = occ?.InstanceId;
                m.Inner = null;
            }
            if (m.Inner is { } inner && occ != null && E.IsPot(occ))
            {
                if (inner.Kind == SelectionKind.PotLiquid)
                {
                    var liquids = E.PotLiquids(S, occ.InstanceId);
                    if (liquids.Count == 0) { m.Inner = null; if (passive) m.Lost = GameEngine.ContentChangedText; }
                    else if (liquids.All(l => l.ItemId != inner.Id))
                        m.Inner = new Selection { Kind = SelectionKind.PotLiquid, Id = liquids[0].ItemId, Owner = inner.Owner, InstanceId = inner.InstanceId };
                }
                else if (inner.InstanceId is int fid)
                {
                    var f = E.FindItem(S, fid);
                    if (f == null || f.Owner != ItemInstance.ContainerOwner(occ.InstanceId)) { m.Inner = null; if (passive) m.Lost = GameEngine.ContentChangedText; }
                    else if (f.ItemId != inner.Id) m.Inner = new Selection { Kind = SelectionKind.Tool, Id = f.ItemId, Owner = f.Owner, InstanceId = f.InstanceId };
                }
            }
            else if (m.Inner != null) m.Inner = null;
        }
        if (m.SourceId is int sid)
        {
            var src = E.FindItem(S, sid);
            if (src == null || !(src.IsCarried || src.Owner == m.Ground))
            {
                m.SourceId = null;
                if (passive) m.Lost = GameEngine.ContentChangedText;
            }
        }
        // 右栏所看的来源被用完（如两份净水全部装进锅）而清空选中时，右栏回这处设施的只读摘要，不跳回人物页（FACPROC 第 2 条）
        if (Selected.Kind == SelectionKind.None) SyncRightSilently(FireSelection);
    }

    /// <summary>主动耗时行动成功开始（食饮、点火、等待加工……）：收起加工菜单，交给中央行动进度层（FACPROC 第 5 条、FACPROC-A09）。</summary>
    private void CloseFireMenuForAction()
    {
        if (Panel != FireMenuPanel) return;
        Panel = null;
        LeaveFireMenu(true);
    }

    /// <summary>火堆维护（投料、熄灭、点火）：空闲或主行动暂停时可做，移动途中与执行中不可（拖拽与容器案 8；原 FireCard 判断，B45 起卡面与菜单同源）。</summary>
    public bool FireMaintBlocked => State != null && (Running || S.Sub != null || (S.Main != null && (S.Main.Status != ActionStatus.Paused || S.Main.Type == ActionType.Travel)));

    /// <summary>「投入木材」快捷（每次1份）的不可用原因（卡面与菜单主层同源，Q556）；可用为 null。</summary>
    public string? FireWoodWhy(out int wood)
    {
        wood = 0;
        if (State == null || E.FacOf(S, S.Ground) is not FacilityState fac) return null;
        wood = E.AvailableCount(S, "wood", true);
        var room = E.MaxWoodForFire(fac);
        return FireMaintBlocked ? "行动进行中，暂不能维护火堆" : room < 1 ? $"至少需要{E.Config.Fire.BurnMinutesPerWood}分钟空余容量（燃料上限{E.FuelCapOf(fac)}分钟）" : wood < 1 ? "随身和当前地面没有木材" : null;
    }

    // ───────────── Q542／Q559／Q560 工坊汇总卡「选择设施」 ─────────────

    /// <summary>
    /// 工坊汇总卡点击：当前区陶窑、固定净水台都已建 → 居中「选择设施」；只建一座 → 直接进入它的原制作面板分类；都没建 → 原「营地建造」工作台入口。
    /// 只读、0 分钟。
    /// </summary>
    public void OpenWorkshops()
    {
        if (State == null) return;
        var choices = E.ProcessingStationChoices(S);
        if (choices.Count >= 2) { OpenStationChooser(); return; }
        if (choices.Count == 1) { OpenCraftStation(choices[0].StationId); return; }
        OpenPanel("craft", "build_workbench");
        if (Panel != "craft") return;
        CraftTab = "camp";
        Notify();
    }

    private void OpenStationChooser()
    {
        if (InBattleUi || DialogueLocked) return;
        CollapseCardPopForPanel(CardPopShown);
        OpenPop(StationsPop, S.Ground, "fac-workshops");
    }

    /// <summary>选择层明确点一座：提交前重验真实身份／位置／状态，再进入该座原制作面板分类，并记住返回来源（Q560）。</summary>
    public void PickStation(string stationId)
    {
        if (State == null || Pop is not { Kind: StationsPop } p || p.Id != S.Ground) return;
        if (E.ProcessingStationChoices(S).All(c => c.StationId != stationId)) { Notify(); return; }
        Pop = null;
        OpenCraftStation(stationId);
        if (Panel == "craft") { CraftFromChooser = true; Notify(); }
    }

    /// <summary>选择层回来后焦点要回的原设施行（Q560）。</summary>
    public string? ChooserReturnStation { get; private set; }

    /// <summary>一级面板的 Esc／关闭按钮：经选择层进入的制作面板回到实时选择列表（不自动跳），其余照原关闭。</summary>
    public void PanelEscape()
    {
        if (Panel == "craft" && CraftFromChooser && State != null)
        {
            var station = CraftTab;
            OpenPanel(null);
            ChooserReturnStation = station;
            OpenPop(StationsPop, S.Ground, "fac-workshops");
            // Q560：焦点回仍有效的原设施行；该行已不在时留在选择层（安全焦点）
            if (station != null && E.ProcessingStationChoices(S).Any(c => c.StationId == station)) FocusIdPending = $"st-pick-{station}";
            return;
        }
        OpenPanel(null);
    }
}
