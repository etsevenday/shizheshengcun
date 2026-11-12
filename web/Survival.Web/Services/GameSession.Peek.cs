using Microsoft.AspNetCore.Components;
using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Web.Services;

/// <summary>
/// B42：没有独立详情组件的公开条目登记给右栏的原公开内容（S14「悬停卡片／条目立即显示右栏详情」：至少放原公开标题、说明和当前状态）。
/// 全部是卡片或条目自己已有的文字与片段，不另写句子；片段每次随卡片重绘重新登记，右栏读的是当前状态，不保留过期快照。
/// </summary>
public sealed class PeekFacts
{
    public string Title { get; init; } = "";
    /// <summary>卡面角标等当前状态（没有时为 null）。</summary>
    public string? State { get; init; }
    /// <summary>原悬停说明或公开说明（没有时为 null）。</summary>
    public string? Desc { get; init; }
    /// <summary>卡面主图。</summary>
    public RenderFragment? Art { get; init; }
    /// <summary>卡面副标、第二行或条目正文（不含执行按钮）。</summary>
    public RenderFragment? Body { get; init; }
}

/// <summary>B42：页面元素 data-peek 的键（内核 <see cref="PeekKey"/> 的壳层写法）。</summary>
public static class Peek
{
    /// <summary>主界面可点选对象：与点击选中同一身份（种类、ID、归属、实例），可带卡片所代表的批次。</summary>
    public static string Sel(SelectionKind kind, string id, string owner = "", int? instanceId = null, string? batch = null) =>
        PeekKey.Make(PeekKey.Sel, kind.ToString(), id, owner, instanceId?.ToString(), batch);

    public static string Sel(Selection s, string? batch = null) => Sel(s.Kind, s.Id, s.Owner, s.InstanceId, batch);

    public static string Card(string id) => PeekKey.Make(PeekKey.Card, id);
    public static string Gear(int instanceId) => PeekKey.Make(PeekKey.Gear, instanceId.ToString());
    public static string Node(string locationId) => PeekKey.Make(PeekKey.Node, locationId);
    public static string Recipe(string recipeId) => PeekKey.Make(PeekKey.Recipe, recipeId);
    public static string Skill(string skillId) => PeekKey.Make(PeekKey.Skill, skillId);
    public static string Status(string statusId) => PeekKey.Make(PeekKey.Status, statusId);
    public static string Foe(int index) => PeekKey.Make(PeekKey.Foe, index.ToString());
    public static string Ally(string actorId) => PeekKey.Make(PeekKey.Ally, actorId);

    /// <summary>从键还原主界面对象身份；不是 sel 键或种类不认识时为 null。</summary>
    public static Selection? ParseSel(string? key)
    {
        if (!PeekKey.TryParse(key, out var kind, out var p) || kind != PeekKey.Sel || p.Length < 4) return null;
        if (!Enum.TryParse<SelectionKind>(p[0], out var k) || k == SelectionKind.None) return null;
        return new Selection { Kind = k, Id = p[1], Owner = p[2], InstanceId = int.TryParse(p[3], out var n) ? n : null };
    }
}

/// <summary>
/// B42（S14「2026-11-12 悬停卡片／条目立即显示右栏详情」，HOVER-A01—A08）：悬停／键盘焦点进入对象热区，右栏立即只读显示该对象。
/// 只改显示：不碰 <see cref="Selected"/>、批次、卡片选中态、当前角色、配方、材料槽、比较槽、接收人、数量、战斗目标／瞄准与草稿，
/// 不记任务进度、不给引导与对白、不扣费、不推进时间、不抽随机，不开操作盘。状态生命周期在内核 <see cref="HoverPeekState"/>。
/// </summary>
public sealed partial class GameSession
{
    private readonly HoverPeekState _peek = new();
    /// <summary>读档、新局、回标题时递增，旧悬停上下文随之作废。</summary>
    private int _peekEpoch;
    private readonly Dictionary<string, (object Owner, PeekFacts Facts)> _peekFacts = new();

    /// <summary>当前悬停覆盖的对象键；null 为没有覆盖。</summary>
    public string? PeekKeyNow => State == null ? null : _peek.Key;

    /// <summary>悬停覆盖的对象登记的公开内容变了、或登记它的卡片已移除（右栏据此立即重绘）。</summary>
    public event Action? PeekFactsChanged;

    /// <summary>悬停覆盖的主界面对象（与点击选中同一身份）；不是主界面对象时为 null。</summary>
    public Selection? PeekSelection => Peek.ParseSel(PeekKeyNow);

    /// <summary>悬停的卡片代表的批次（叠卡后层、分卡的一侧）；null 按右栏原规则取默认或已点选的批次。</summary>
    public string? PeekBatch => PeekKey.KindOf(PeekKeyNow) == PeekKey.Sel ? PeekKey.Part(PeekKeyNow, 4) : null;

    /// <summary>对白锁、游戏／战斗菜单、死亡结算时整局不接受悬停查看，已有覆盖在刷新时清掉（HOVER-A07）。</summary>
    private bool PeekLocked => State == null || DialogueLocked || MenuOpen || BattleMenuOpen || S.Progress.Dead;

    /// <summary>
    /// 页面热区（data-peek）在指针真实移动进入或键盘焦点到达时调用（survivalPeek，app.js）。
    /// 数量框等模态开着时不受理；对象已不在（键失效）时不受理。只在显示对象变化时刷新界面。
    /// </summary>
    public void PeekEnter(string? key)
    {
        if (PeekLocked || PendingDrop != null || string.IsNullOrEmpty(key)) return;
        if (!PeekValid(key)) return;
        if (_peek.Enter(key, PeekContext())) { PeekSerial++; Notify(); }
    }

    /// <summary>结束悬停覆盖（明确点选、点右栏页签或返回控件）。不通知界面，由调用方统一 Notify。</summary>
    public void PeekClear() => _peek.Clear();

    /// <summary>读档、新局、回标题：旧悬停上下文作废。</summary>
    private void ResetPeek()
    {
        _peek.Clear();
        _peekEpoch++;
    }

    /// <summary>
    /// 当前界面上下文：一级面板、面板内成员（整备与成长）、真实地点与区、战斗／敌踪、面板内页（地图层、制作分类与候选）。
    /// 任一变化即清旧悬停（HOVER-A05；实际地点变更另按 Q281 清旧选择）。
    /// </summary>
    private string PeekContext() => HoverPeekContext.Of(
        _peekEpoch,
        Panel,
        Panel is "gear" or "growth" ? GearActorId : null,
        S.CurrentLocation,
        S.CurrentZone ?? "",
        S.Battle != null ? "battle" : S.Encounter != null ? "encounter" : "",
        Panel == "map" ? MapLayer : Panel == "craft" ? $"{CraftTab}/{(CraftCandidates == null ? "" : string.Join(",", CraftCandidates))}" : null);

    /// <summary>Notify 前校验：上下文变了、对象失效或整局输入锁定时立即清掉（不按同一位置改指别物）。</summary>
    private void ReconcilePeek()
    {
        if (State == null || PeekLocked) { _peek.Clear(); _peekItems.Clear(); return; }
        FollowPeekItem();
        _peek.Reconcile(PeekContext(), PeekValid);
        _peekItems = PeekItemOf(_peek.Key) is PeekItemRef r ? PeekItems.Members(S, r) : new List<int>();
    }

    /// <summary>悬停换成另一对象的次数（右栏据此把滚动回到顶部；物品挪走后跟到新归属是同一对象，不加）。</summary>
    public int PeekSerial { get; private set; }

    /// <summary>悬停物品此刻代表的真实实例（上次刷新时记下）；物品被挪走后据此跟到新归属（B42 试玩修复 BUG-1）。</summary>
    private List<int> _peekItems = new();

    /// <summary>键是主界面物品（物品组或单件）时的身份；其他对象为 null。</summary>
    private static PeekItemRef? PeekItemOf(string? key) =>
        Peek.ParseSel(key) is { Kind: SelectionKind.ItemGroup or SelectionKind.Tool } sel
            ? new PeekItemRef(sel.Kind.ToString(), sel.Id, sel.Owner, sel.InstanceId, PeekKey.Part(key!, 4))
            : null;

    /// <summary>
    /// B42 试玩修复（BUG-1；S14「可见对象数量、归属……变化时以同一真实引用更新」「对象被移除、隐藏、失效时立即清」）：
    /// 悬停的物品放下、拾起、放进火堆加工位或取出后，原「物品+归属」已不在：同一批实例仍在随身或当前地点就把悬停跟到新归属
    /// （与点击选中随状态校正同一口径），找不到或不可见留给下面的有效性校验清掉。界面上下文变了不跟随，按原规则清。
    /// </summary>
    private void FollowPeekItem()
    {
        if (PeekItemOf(_peek.Key) is not PeekItemRef r || _peek.Context != PeekContext() || BattleFullscreen) return;
        if (PeekItems.Shown(E, S, r)) return;
        if (PeekItems.Follow(E, S, r, _peekItems) is PeekItemRef n && Enum.TryParse<SelectionKind>(n.Kind, out var k))
            _peek.Enter(Peek.Sel(k, n.Id, n.Owner, n.Inst, n.Batch), PeekContext());
    }

    /// <summary>对象仍在、仍可见且属于当前界面（键里是真实身份，不是列表序号）。</summary>
    private bool PeekValid(string key)
    {
        if (State == null || !PeekKey.TryParse(key, out var kind, out var p)) return false;
        var first = p.Length > 0 ? p[0] : "";
        switch (kind)
        {
            case PeekKey.Sel:
                // 物品按原归属、实例与所指批次核对（与右栏物品详情同源），不只看实例还在不在
                if (PeekItemOf(key) is PeekItemRef ir) return !BattleFullscreen && PeekItems.Shown(E, S, ir);
                return Peek.ParseSel(key) is Selection sel && !BattleFullscreen && SelectionAlive(sel);
            case PeekKey.Gear:
                return Panel == "gear" && int.TryParse(first, out var gid) && E.FindItem(S, gid) != null;
            case PeekKey.Node:
                return Panel == "map" && S.Locations.TryGetValue(first, out var nl) && nl.Discovered;
            case PeekKey.Recipe:
                return Panel == "craft" && (first.StartsWith(CraftProcPrefix)
                    ? E.Config.Processes.Any(x => x.Id == first.Substring(CraftProcPrefix.Length))
                    : E.Config.Recipes.Any(r => r.Id == first && !r.IsSalvage));
            case PeekKey.Skill:
                return Panel == "growth" && E.SkillViews(S.Actors.TryGetValue(GearActorId, out var ga) ? ga : S.Player).Any(v => v.Id == first);
            case PeekKey.Status:
                return !BattleFullscreen && StatusView.All(E, S).Any(c => c.Id == first);
            case PeekKey.Card:
                return !BattleFullscreen && _peekFacts.ContainsKey(key);
            case PeekKey.Foe:
                return S.Battle != null && int.TryParse(first, out var fi) && E.BattleEnemyAt(S.Battle, fi) is { Alive: true };
            case PeekKey.Ally:
                return S.Battle != null && GameEngine.AlliesOnField(S.Battle).Contains(first);
            default:
                return false;
        }
    }

    /// <summary>手工制作「自动加工说明」条目的配方键前缀（与 CraftOverlay.ProcPrefix 同值）。</summary>
    public const string CraftProcPrefix = "proc:";

    /// <summary>主界面对象仍在当前地点／当前区可见（与右栏点击详情的同地有效性一致）。</summary>
    private bool SelectionAlive(Selection sel)
    {
        var here = S.CurrentLocation;
        switch (sel.Kind)
        {
            case SelectionKind.Location:
                return sel.Id == here;
            case SelectionKind.Entrance:
                return E.AdjacentUnlocked(S).Any(x => x.Other(here) == sel.Id);
            case SelectionKind.Resource:
                return S.CurrentZone != GameState.SafehouseZone && E.Config.Resources.Any(r => r.NodeId == sel.Id && r.LocationId == here)
                       && S.Resources.TryGetValue(sel.Id, out var rs) && rs.Discovered;
            case SelectionKind.ItemGroup:
                return (sel.Owner == ItemInstance.Carried || sel.Owner == S.Ground) && S.Items.Any(i => i.Owner == sel.Owner && i.ItemId == sel.Id);
            case SelectionKind.Tool:
            {
                if (sel.InstanceId is not int id || E.FindItem(S, id) is not ItemInstance it || it.ItemId != sel.Id) return false;
                var place = E.PlaceOf(S, it);
                return place == ItemInstance.Carried || place == S.Ground;
            }
            case SelectionKind.PotLiquid:
            {
                if (sel.InstanceId is not int pid || E.FindItem(S, pid) is not ItemInstance pot) return false;
                var place = E.PlaceOf(S, pot);
                return (place == ItemInstance.Carried || place == S.Ground) && E.PotLiquids(S, pid).Any(l => l.ItemId == sel.Id);
            }
            case SelectionKind.Facility:
                return E.HasFireAt(S, S.Ground) && sel.Id == GameEngine.FireIdOf(S.Ground);
            case SelectionKind.Enemy:
                return S.CurrentZone != GameState.SafehouseZone && sel.InstanceId is int wid && E.VisibleEnemiesAt(S, here).Any(w => w.Id == wid);
            default:
                return false;
        }
    }

    // ───── 公开条目登记（PeekKey.Card） ─────

    /// <summary>卡片／条目每次参数刷新时登记自己的原公开内容；正被悬停时通知右栏重绘。</summary>
    public void SetPeekFacts(string key, object owner, PeekFacts facts)
    {
        _peekFacts[key] = (owner, facts);
        if (_peek.Key == key) PeekFactsChanged?.Invoke();
    }

    /// <summary>卡片／条目移除：撤销登记；正被悬停时立即清掉右栏悬停（HOVER-A05：失效不改指别物）。</summary>
    public void DropPeekFacts(string key, object owner)
    {
        if (!_peekFacts.TryGetValue(key, out var x) || !ReferenceEquals(x.Owner, owner)) return;
        _peekFacts.Remove(key);
        if (_peek.Key == key && _peek.Clear()) PeekFactsChanged?.Invoke();
    }

    public PeekFacts? PeekFactsOf(string? key) => key != null && _peekFacts.TryGetValue(key, out var x) ? x.Facts : null;
}
