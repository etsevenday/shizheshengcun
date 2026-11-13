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
        /// <summary>
        /// 已进入位里的内容（锅这一层，或直烤食材／煅烧批这一层）；false = 停在「加工位{序号}」这一层（S14 层级「{设施名} › 加工位{序号} › {锅名} › {内容}」逐级返回）。
        /// </summary>
        public bool Open { get; set; }
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
        /// <summary>
        /// B45b（Q582 选 B）：先选空位与来源、该位随后被别处操作占用——保留这份未提交选择（目标仍是这个加工位、来源与数量原样），
        /// 不跟随新内容、不换到另一空位；下一次真实提交才重验并提示「加工位{序号}已被占用，请重新选择。」。玩家明确改选或进入新内容即结束。
        /// </summary>
        public bool Taken { get; set; }
        /// <summary>B45b：菜单里上一次提交被拒的真实原因（下一次明确选择或提交成功时清掉）。</summary>
        public string? Fail { get; set; }
        /// <summary>
        /// B45b（Q588）：选目标或库存变化使已选来源对当前目标不可放入——来源已清掉，这里留该来源的真实拒绝句（显示在底部说明位），
        /// 须另选可用材料；不自动换料／换批／换工位。下一次明确选择时清掉。
        /// </summary>
        public string? SrcWhy { get; set; }
    }

    /// <summary>B45b（Q582）：菜单自己的放入正在提交（放入加工位／装水／放入食材、煅烧确认），核对时把新内容认作本菜单的放入，不当成“被别处占用”。</summary>
    private bool _fireMenuOwnPut;

    /// <summary>B45b（Q584）：菜单底部数量框的 id（点选来源行后焦点进这里）。</summary>
    public const string FireMenuQtyId = "fm-qty";
    /// <summary>B45b（Q583）：菜单标题（焦点的安全落点）。</summary>
    public const string FireMenuTitleId = "fm-title";
    /// <summary>B45b（Q583）：菜单里加工位卡的 id（0 起）。</summary>
    public static string FireMenuSlotId(int k) => $"fm-slot-{k}";

    public FireMenuContext? FireMenu { get; private set; }

    /// <summary>当前菜单层级：0 设施主层、1 加工位、2 位里的内容（锅，或直烤食材／煅烧批）、3 锅里的水或食材（路径「{设施名} › 加工位{序号} › {锅名} › {内容}」）。</summary>
    public int FireMenuLevel => FireMenu is not { } m ? 0 : m.Inner != null ? 3 : m.Slot == null ? 0 : m.Open && m.SlotItem != null ? 2 : 1;

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
        m.Open = slots[k] != null;   // 点有内容的加工位直接进入位里的内容；Esc／路径「加工位{序号}」再退回这一位
        m.Inner = null;
        m.Lost = null;
        m.Taken = false;   // Q582：明确改选／进入新内容才换目标
        m.Fail = null;
        m.SrcWhy = null;
        SyncRight(slots[k] is ItemInstance it ? ItemSel(E, it) : FireSelection);
    }

    /// <summary>明确点锅里的水或食材：进入内容层（收起来源区），右栏同步该对象。</summary>
    public void FireMenuPickInner(Selection sel)
    {
        if (FireMenu is not { Dead: false } m || m.Slot == null) return;
        m.Open = true;
        m.Inner = sel;
        m.Lost = null;
        m.Taken = false;
        m.Fail = null;
        m.SrcWhy = null;
        SyncRight(sel);
    }

    /// <summary>菜单层级路径：点前面的层级回到那一层（0 主层、1 加工位、2 锅／位里的内容）。</summary>
    public void FireMenuGoLevel(int level)
    {
        if (FireMenu is not { } m || State == null) return;
        var from = FireMenuLevel;
        var left = m.Slot;
        if (level <= 0)
        {
            if (m.Slot != null) m.Qty = "1";
            m.Slot = null;
            m.SlotItem = null;
            m.Open = false;
            m.Inner = null;
        }
        else if (level == 1) { m.Open = false; m.Inner = null; }
        else if (level == 2) m.Inner = null;
        m.Lost = null;
        m.Fail = null;
        m.SrcWhy = null;
        // Q583（选 A）：回父层后键盘焦点回到刚离开的加工位卡（锅里内容回锅这一层时回锅卡）；只聚焦、不执行
        if (level < from && left is int k) FocusIdPending = FireMenuReturnFocus(Math.Max(0, level), k);
        SyncRight(CurrentFireMenuObject());
    }

    /// <summary>
    /// Q583：回到第 level 层后焦点去哪——0／1 层回刚离开的加工位卡，2 层（锅这一层）回该位里的锅卡；对象已不在则回最近合法父层：
    /// 加工位卡（菜单固定两个加工位），连加工位都没有时回菜单标题。不自动选相邻实例。
    /// </summary>
    private string FireMenuReturnFocus(int level, int k)
    {
        if (FireMenu is not { Dead: false } m) return FireMenuTitleId;
        var slots = E.FireSlots(S, m.Ground);
        if (k < 0 || k >= slots.Count) return FireMenuTitleId;
        if (level >= 2 && slots[k] is ItemInstance it && E.IsPot(it)) return $"#{FireMenuSlotId(k)} .pc-head";
        return FireMenuSlotId(k);
    }

    /// <summary>Esc／「返回」：退一层（锅里内容→锅→加工位→主层）。</summary>
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
        // Q588：对当前目标不可放入的材料不能被设为来源（鼠标、触屏、键盘一律在这里同源拒绝），不选、不改已有合法来源
        if (FireMenuBlockWhy(it) != null) return;
        if (m.SourceId != it.InstanceId) m.Qty = "1";
        m.SourceId = it.InstanceId;
        m.Lost = null;
        m.Fail = null;
        m.SrcWhy = null;
        var sel = ItemSel(E, it);
        SyncRightSilently(sel);
        if (sel.Kind == SelectionKind.ItemGroup) SelectedBatchKey = GameEngine.BatchKeyOf(it);
        // Q584（选 A）：明确点选来源行后，焦点进入当前动作的数量框（框可编辑时），Enter 按最新合法数量与该次投放按钮同源提交
        if (FireMenuTarget(out var slot, out var pot) && slot != null && !(E.IsBusy(S) || Running)
            && (m.Taken || FireMenuMaxFor(it, slot, pot, out var why) > 0 && why == null))
            FocusIdPending = FireMenuQtyId;
        Notify();
    }

    /// <summary>
    /// 当前层来源区的放入目标：主层没有目标（slot＝null，仍列来源）；加工位层的空位（或 Q582 被别处占用、保留草稿的那一位）为 (k, null)；
    /// 锅这一层为 (k, 锅)。其余层（停在有内容的加工位、直烤／煅烧内容、锅里的水或食材）没有来源区，返回 false。
    /// </summary>
    public bool FireMenuTarget(out int? slot, out ItemInstance? pot)
    {
        slot = null;
        pot = null;
        if (FireMenu is not { Dead: false } m || State == null || m.Inner != null) return false;
        if (m.Slot is not int k) return true;
        var slots = E.FireSlots(S, m.Ground);
        var occ = k < slots.Count ? slots[k] : null;
        slot = k;
        if (occ == null || m.Taken) return true;
        if (m.Open && E.IsPot(occ)) { pot = occ; return true; }
        return false;
    }

    /// <summary>
    /// B45b（Q588）：这份材料对当前明确目标是否不可放入——不可放入时返回该行原真实拒绝句（同源预演 <see cref="FireMenuMaxFor"/>；
    /// 预演没有原因但可放入量为 0 时用「当前没有可放入数量。」），可放入为 null。没有明确目标（主层，沿 Q544 列料）或
    /// Q582 保留草稿（目标位已被别处占用，提交时才重验）时不判定，返回 null。
    /// </summary>
    public string? FireMenuBlockWhy(ItemInstance it)
    {
        if (FireMenu is not { } m || !FireMenuTarget(out var slot, out var pot) || slot == null || (m.Taken && pot == null)) return null;
        var max = FireMenuMaxFor(it, slot, pot, out var why);
        return why ?? (max <= 0 ? GameEngine.NoQtyText : null);
    }

    /// <summary>明确目标下这份来源的最大可提交量与原因（同源预演：工位 MaxPutInFire(明确位)、锅 MaxFill／MaxPutFood）。</summary>
    public int FireMenuMaxFor(ItemInstance it, int? slot, ItemInstance? pot, out string? why)
    {
        if (pot != null) return E.IsLiquid(it) ? E.MaxFill(S, it, pot, out why) : E.MaxPutFood(S, it, pot, out why);
        if (slot is int k) return E.MaxPutInFire(S, it, out why, k);
        why = null;
        return 0;
    }

    /// <summary>
    /// 菜单的放入提交（放入加工位{序号}／装水／放入食材，Q545／Q557／Q562）：同一事务重验后一次提交，失败原子不扣、记下真实原因。
    /// B45b（Q582 选 B）：成功后停在提交前的菜单层，按真实状态刷新，清空本次来源物品选择（数量文本不动）；不自动进入新物件子层。
    /// </summary>
    public ActionResult FireMenuSubmit(InstantRequest req)
    {
        _fireMenuOwnPut = true;
        ActionResult res;
        try { res = Instant(req); }
        finally { _fireMenuOwnPut = false; }
        if (FireMenu is { } m)
        {
            if (res.Ok)
            {
                m.SourceId = null;
                m.Taken = false;
                m.Fail = null;
                m.SrcWhy = null;
            }
            else m.Fail = res.Reason;
            Notify();
        }
        return res;
    }

    /// <summary>B45b（Q582-A02）：菜单提交前重验得到的拒绝原因（如加工位已被占用），不扣物、不改投。</summary>
    public void FireMenuRefuse(string why)
    {
        if (FireMenu is not { } m) return;
        m.Fail = why;
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
            if (occ == null) m.Taken = false;
            if (occ?.InstanceId != m.SlotItem)
            {
                if (m.SlotItem == null && occ != null && !_fireMenuOwnPut && m.SourceId != null)
                {
                    // Q582（选 B）：先选空位与来源、再由其他操作占用该位——保留未提交选择，不跟随新内容、不换空位；提交时重验出占位句
                    m.Taken = true;
                }
                else
                {
                    // 位里原有的内容没了（取出、吃完、腐败清理）：留在这个加工位（现为空位或新内容）。
                    // Q582（选 B）：新放进来的内容（本菜单放入成功或别处放入）只显示在加工位卡上，不自动进入新物件子层
                    if (m.SlotItem != null && passive) m.Lost = GameEngine.ContentChangedText;
                    m.SlotItem = occ?.InstanceId;
                    m.Open = false;
                    m.Inner = null;
                    m.Taken = false;
                }
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
            else if (!_fireMenuOwnPut && FireMenuBlockWhy(src) is string why)
            {
                // Q588：选目标或库存／容量变化使已选来源对当前目标不可放入——清掉该来源、显示其真实原因、禁止提交；不自动换料／换批／换工位
                m.SourceId = null;
                m.SrcWhy = why;
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
        // FACPROC 第 4 条：焦点进首个操作（第一座设施行）
        if (Pop is { Kind: StationsPop } && E.ProcessingStationChoices(S).FirstOrDefault() is { } first) FocusIdPending = $"st-pick-{first.StationId}";
    }

    /// <summary>选择层明确点一座：提交前重验真实身份／位置／状态，再进入该座原制作面板分类，并记住返回来源（Q560）。</summary>
    public void PickStation(string stationId)
    {
        if (State == null || Pop is not { Kind: StationsPop } p || p.Id != S.Ground) return;
        if (E.ProcessingStationChoices(S).All(c => c.StationId != stationId)) { Notify(); return; }
        Pop = null;
        OpenCraftStation(stationId);
        // Q560：记住这次明确点的那一座（之后在制作面板换分类／配方也不丢），返回时焦点回它的行
        if (Panel == "craft") { CraftFromChooser = true; ChooserReturnStation = stationId; Notify(); }
    }

    /// <summary>经选择层进入时明确点的那一座；回到选择层后焦点要回它的行（Q560）。</summary>
    public string? ChooserReturnStation { get; private set; }

    /// <summary>一级面板的 Esc／关闭按钮：经选择层进入的制作面板回到实时选择列表（不自动跳），其余照原关闭。</summary>
    public void PanelEscape()
    {
        if (Panel == "craft" && CraftFromChooser && State != null)
        {
            var station = ChooserReturnStation ?? CraftTab;
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
