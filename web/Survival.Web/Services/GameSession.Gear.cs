using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Web.Services;

/// <summary>整备页交接盘草稿（P03c，Q432、Q451、Q453-4）：接收人初始留空、数量默认整堆；只是界面状态，不进存档。</summary>
public sealed class GearGiveDraft
{
    public int InstanceId { get; init; }
    public string? To { get; set; }
    public int Count { get; set; }
    /// <summary>从比较底部进入（Q460-1：返回先回比较），否则从非装备来源工作区进入。</summary>
    public bool FromCompare { get; init; }
}

/// <summary>
/// B33 整备页（P03a—P03d）的页内状态：来源卡选择、比较目标槽、旧物去向、交接草稿、右栏临时整备详情。
/// 全部只读预演，提交只有「确认更换」「确认交接」与槽上一键卸下；不改主界面真正选中（Q450）、不进存档。
/// </summary>
public sealed partial class GameSession
{
    /// <summary>右侧来源列表里选中的卡（本人随身任一根卡，或当前地面的可穿装备）。</summary>
    public int? GearSourceId { get; private set; }

    /// <summary>正在比较的目标槽；非装备来源为 null（工作区只显示名称与「交给…」）。</summary>
    public string? GearSlot { get; private set; }

    /// <summary>玩家明确点选的旧物去向（true=当前地面）；null 未选，两处合法时默认本人随身（Q434）。</summary>
    public bool? GearOldToGround { get; private set; }

    /// <summary>交接盘草稿（P03c）；null 不在交接盘。</summary>
    public GearGiveDraft? GivePending { get; set; }

    /// <summary>右栏临时整备详情（Q450）：页内点物品卡或已穿戴装备时只读覆盖右栏，不改 Selected、不计 PRO01。</summary>
    public int? GearPeekId { get; private set; }

    /// <summary>整备页开着且临时详情的物品仍在时返回它，右栏据此覆盖显示。</summary>
    public ItemInstance? GearPeekItem =>
        State != null && Panel == "gear" && GearPeekId is int id ? E.FindItem(S, id) : null;

    /// <summary>当前仍有效的来源卡：所选成员本人随身，或当前地面的可穿装备（失效直接回空态，Q460-1）。</summary>
    public ItemInstance? GearSource
    {
        get
        {
            if (State == null || GearSourceId is not int id || E.FindItem(S, id) is not ItemInstance it) return null;
            if (it.Owner == ItemInstance.CarriedOwner(GearActorId)) return it;
            return it.Owner == S.Ground && E.IsWearable(it.ItemId) ? it : null;
        }
    }

    /// <summary>页内子层（比较、非装备工作区、交接盘）：Esc 逐层退回（Q453-8、Q460）。</summary>
    public bool GearHasSubLayer => Panel == "gear" && (GivePending != null || GearSourceId != null);

    private void ClearGearWork()
    {
        GearSourceId = null;
        GearSlot = null;
        GearOldToGround = null;
        GivePending = null;
    }

    /// <summary>整备页关闭、成员不在同行、来源或临时详情失效时清掉页内意图（Notify 时调用）。</summary>
    private void ReconcileGear()
    {
        if (State == null || Panel != "gear")
        {
            // BM4a（Q590）：整备页被顶栏面板盖在下层时保留来源、比较与交接草稿（恢复后按下面的有效性重验）
            if (State != null && _chain.Contains("gear")) { GearPeekId = null; return; }
            ClearGearWork();
            GearPeekId = null;
            return;
        }
        if (GearSourceId != null && GearSource == null) ClearGearWork();
        if (GivePending is { } g && (GearSource == null || GearSource.InstanceId != g.InstanceId || GearSource.Owner != ItemInstance.CarriedOwner(GearActorId))) GivePending = null;
        if (GearPeekId is int pid && E.FindItem(S, pid) == null) GearPeekId = null;
    }

    /// <summary>
    /// BM1（Q481 选 A）：右栏显示整备临时详情时点「人物」页签——只清点击临时详情（悬停覆盖由页签统一结束），正常进入人物页；
    /// 不切同行者、不清来源选择／比较目标槽／旧物去向／交接草稿、不改真正选中。下一次真正悬停或点击对象才重新显示详情。
    /// </summary>
    private void EndGearPeekForCharacterTab() => GearPeekId = null;

    /// <summary>切成员（Q453-5）：清旧比较、交接与食饮草稿，右栏回打开整备前的上下文；不换世界行动执行者。</summary>
    public void GearPickActor(string actorId)
    {
        ClearGearWork();
        GearPeekId = null;
        DropFeed();
        GearActor = actorId;
        Notify();
    }

    /// <summary>点来源卡（Q431、Q448）：可穿的打开同槽比较（武器默认武器Ⅰ），非装备只显示名称与「交给…」；右栏临时显示只读详情。</summary>
    public void GearPickSource(int instanceId)
    {
        if (State == null || E.FindItem(S, instanceId) is not ItemInstance it) return;
        ClearGearWork();
        GearSourceId = instanceId;
        GearSlot = E.DefaultCompareSlot(it.ItemId);
        GearPeekId = instanceId;
        PeekClear();   // B42：明确点卡回到原点击整备详情（Q450），悬停覆盖结束
        Notify();
    }

    /// <summary>
    /// 点七槽（Q453-1、Q460-2）：比较武器时点武器Ⅰ／Ⅱ切换目标槽；点别的已穿戴槽关闭比较、清来源选择，右栏只读显示被点装备；
    /// 点空槽不改右栏，比较的是非武器时点非目标槽退出比较。
    /// </summary>
    public void GearClickSlot(string slot)
    {
        if (State == null) return;
        PeekClear();   // B42：明确点槽结束悬停覆盖，按原规则显示
        var src = GearSource;
        if (src != null && GearSlot != null && GivePending == null && E.SlotsFor(src.ItemId).Contains(slot))
        {
            if (GearSlot != slot) { GearSlot = slot; GearOldToGround = null; }
            Notify();
            return;
        }
        var worn = E.WornItem(S, GearActorId, slot);
        if (worn == null)
        {
            if (src != null && GearSlot != null) ClearGearWork();
            Notify();
            return;
        }
        ClearGearWork();
        GearPeekId = worn.InstanceId;
        Notify();
    }

    /// <summary>旧物去向（Q434）：只记玩家点选，校验与默认值都按预演现算。</summary>
    public void GearSetOldDest(bool toGround)
    {
        GearOldToGround = toGround;
        Notify();
    }

    /// <summary>「返回」与 Esc（Q460）：交接盘先退回进入前的比较或来源工作区，比较／非装备工作区再退回空态；来源失效直接空态。</summary>
    public void GearBack()
    {
        if (GivePending != null)
        {
            GivePending = null;
            if (GearSource == null) ClearGearWork();
        }
        else ClearGearWork();
        Notify();
    }

    /// <summary>预演当前比较（null：没有比较）。</summary>
    public EquipPreviewInfo? GearComparePreview() =>
        State != null && GearSource is ItemInstance src && GearSlot != null ? E.EquipPreview(S, GearActorId, src.InstanceId, GearSlot, GearOldToGround) : null;

    /// <summary>「确认更换」：按预演再校验，成功一次原子提交、回工作空态（Q453-3）；失败两件原位，留在比较里。</summary>
    public ActionResult GearConfirmEquip()
    {
        var p = GearComparePreview();
        if (p == null) return ActionResult.Fail("物品已不在");
        if (p.Blocked != null) { Notice = p.Blocked; Notify(); return ActionResult.Fail(p.Blocked); }
        var res = Commit(E.Equip(S, GearActorId, p.Candidate.InstanceId, p.Slot, Guid.NewGuid().ToString("N"), p.OldToGround == true));
        if (res.Ok)
        {
            QueueFly($"[data-gear-slot=\"{p.Slot}\"]");
            ClearGearWork();
            Notify();
        }
        return res;
    }

    /// <summary>槽上一键「卸下」「放当前地面」（Q431：沿原条件，不另出预演）；成功播槽→实际去向卡一次（Q435）。</summary>
    public ActionResult GearUnequip(string slot, bool toGround)
    {
        var it = State == null ? null : E.WornItem(S, GearActorId, slot);
        var res = Commit(E.Unequip(S, GearActorId, slot, toGround, Guid.NewGuid().ToString("N")));
        if (res.Ok && it != null) QueueFly($"[data-gear-src=\"{it.InstanceId}\"]");
        return res;
    }

    // ───────────── 拖候选到槽（Q431-2、Q453-6） ─────────────

    /// <summary>拖入相容槽等同点选候选并明确选该槽，只打开比较（受阻也打开、写原因）；非装备或不相容槽不打开比较，给原装卸判定原因。</summary>
    public ActionResult GearDropToSlot(int instanceId, string slot)
    {
        if (State == null || Panel != "gear" || E.FindItem(S, instanceId) is not ItemInstance it) return ActionResult.Fail("物品已不在");
        if (!E.SlotsFor(it.ItemId).Contains(slot))
            return ActionResult.Fail(E.EquipBlocked(S, GearActorId, instanceId, slot) ?? GameEngine.Unsupported);
        ClearGearWork();
        GearSourceId = instanceId;
        GearSlot = slot;
        GearPeekId = instanceId;
        Notify();
        return ActionResult.Success();
    }

    // ───────────── 交接（P03c） ─────────────

    /// <summary>「交给…」：接收人留空、数量默认整堆（Q432）。</summary>
    public void GearOpenGive()
    {
        if (GearSource is not ItemInstance src || src.Owner != ItemInstance.CarriedOwner(GearActorId)) return;
        if (E.GiveEntryBlocked(S, GearActorId, src.InstanceId) != null) return;
        GivePending = new GearGiveDraft { InstanceId = src.InstanceId, Count = E.GiveAvailable(S, src), FromCompare = GearSlot != null };
        Notify();
    }

    public void GearGivePick(string? to)
    {
        if (GivePending == null) return;
        GivePending.To = string.IsNullOrEmpty(to) ? null : to;
        Notify();
    }

    public void GearGiveCount(int n)
    {
        if (GivePending == null) return;
        GivePending.Count = n;
        Notify();
    }

    public GivePreviewInfo? GearGivePreview() =>
        State != null && GivePending is { } g ? E.GivePreview(S, GearActorId, g.To, g.InstanceId, g.Count) : null;

    /// <summary>
    /// 「确认交接」：确认前重验人、物、数量与双方容量；成功播来源卡→接收人行一次，仍看原成员（Q432）。
    /// 部分交接后原来源仍有效留在盘里、数量重置为剩余整堆、接收人仍合法则保留；来源耗尽或失效回空态，不自动换批（Q453-4）。
    /// </summary>
    public ActionResult GearConfirmGive()
    {
        if (GivePending is not { } g || g.To is not string to) return ActionResult.Fail(GearPanelCopy.PickReceiver);
        var p = GearGivePreview();
        if (p?.Blocked != null) { Notify(); return ActionResult.Fail(p.Blocked); }
        var res = Commit(E.Give(S, GearActorId, to, g.InstanceId, g.Count, Guid.NewGuid().ToString("N")));
        if (!res.Ok) return res;
        QueueFly($"[data-gear-member=\"{to}\"]");
        if (E.FindItem(S, g.InstanceId) is ItemInstance left && left.Owner == ItemInstance.CarriedOwner(GearActorId))
        {
            g.Count = E.GiveAvailable(S, left);
            if (!E.GiveReceivers(S, GearActorId).Contains(to)) g.To = null;
        }
        else ClearGearWork();
        Notify();
        return res;
    }

    // ───────────── 成功轨迹（Q435、Q453-7） ─────────────

    /// <summary>待播的成功轨迹去向（CSS 选择器）；起点由页面在提交前用 survivalFx.mark 记下。界面绘制后由 GameScreen 交给 survivalFx.fly。</summary>
    public string? PendingFly { get; private set; }

    public void QueueFly(string toSelector) => PendingFly = toSelector;

    public string? TakeFly()
    {
        var f = PendingFly;
        PendingFly = null;
        return f;
    }

    /// <summary>
    /// 右栏人物页快捷卸下（Q453-7）：沿原一键入口与条件；成功播槽→实际去向卡一次——整备开着时落在整备右侧真实卡，
    /// 否则落在主界面随身或当前地面那一行的真实卡。
    /// </summary>
    public ActionResult QuickUnequip(string slot, bool toGround)
    {
        var it = State == null ? null : E.WornItem(S, GameState.PlayerId, slot);
        var res = Unequip(slot, toGround);
        if (res.Ok && it != null) QueueFly(QuickUnequipFlyTarget(it, toGround));
        return res;
    }

    /// <summary>
    /// 右栏快捷卸下的去向卡（试玩修复 P2）：整备没开时是主界面随身／地面那张卡；整备开着且正看主角时是整备右侧那张真实来源卡；
    /// 整备开着但正看别的队员时，去向卡被整备页盖住、页内也没有它，按 Q367 只亮可见端（起点槽行），不飞向被遮住的卡。
    /// </summary>
    private string QuickUnequipFlyTarget(ItemInstance it, bool toGround)
    {
        if (Panel != "gear") return ShelfCardSelector(toGround ? S.Ground : ItemInstance.Carried, it.ItemId);
        return GearActorId == GameState.PlayerId ? $"[data-gear-src=\"{it.InstanceId}\"]" : FlyNoTarget;
    }

    /// <summary>去向不可见：survivalFx.fly 收到空选择器只亮起点。</summary>
    public const string FlyNoTarget = "";

    /// <summary>主界面卡架里某物品的真实卡（堆叠卡「owner|itemId」，实例卡「owner|itemId|实例号」）。</summary>
    private static string ShelfCardSelector(string owner, string itemId) =>
        $"[data-drop=\"zone:{owner}\"] [data-card=\"{owner}|{itemId}\"], [data-drop=\"zone:{owner}\"] [data-card^=\"{owner}|{itemId}|\"]";

    /// <summary>主界面卡片快捷装备（Q435）：沿原入口；成功时右栏装备槽可见就高亮对应槽行一次，否则只留原结果提示。</summary>
    public ActionResult QuickEquip(int instanceId, string slot, bool oldToGround = false)
    {
        var res = Equip(instanceId, slot, oldToGround);
        if (res.Ok) PendingLit = $".detail .equip-row[data-slot=\"{slot}\"]";
        return res;
    }

    /// <summary>待高亮一次的目标（只在可见时亮，GameScreen 绘制后交给 survivalFx.litIfVisible）。</summary>
    public string? PendingLit { get; private set; }

    public string? TakeLit()
    {
        var l = PendingLit;
        PendingLit = null;
        return l;
    }
}
