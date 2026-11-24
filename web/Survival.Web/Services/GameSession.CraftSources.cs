using Survival.Core.Engine;

namespace Survival.Web.Services;

/// <summary>
/// B35b「UI 重做：制作页三列与来源侧页」（05 P06／F08、逐字稿 P06b；S14 2026-11-13 B35 Q592 A、Q593 A，2026-11-14 B35 次要 Q599 B，
/// 2026-11-17 B35 次要 Q911 A，2026-11-18 B35b Q949 A）：缺料行“查看来源”在制作窗口内打开只读的材料来源侧页——
/// 中列来源列表（“可获取”“其他已知来源”两组），内右列所选来源详情、“在地图上定位”与“返回配方”；本次制作区随侧页隐藏。
/// “在地图上定位”把地图盖在制作窗口上，只高亮该地点（不选为移动目的地、不出发）；地图关闭／ESC／“返回材料来源”先回侧页（保留所选来源），
/// 侧页“返回配方”或 ESC 再回配方。点左列配方或分类即一次退出侧页并改选（Q949）。全部只读：不扣物、不预留、不推进时间。
/// </summary>
public sealed partial class GameSession
{
    /// <summary>侧页正在看来源的物品；null 为配方视图（本次制作区可见）。</summary>
    public string? CraftSourceItem { get; private set; }

    /// <summary>侧页所选来源行的键（<see cref="SourceKey"/>）；null 为没有选中（内右列留空）。</summary>
    public string? CraftSourceKey { get; private set; }

    /// <summary>制作窗口正显示材料来源侧页。</summary>
    public bool CraftSourcesOpen => State != null && Panel == "craft" && CraftSourceItem != null;

    /// <summary>来源地图定位时高亮的地点（只高亮，不是所选目的地）；不在来源链里为 null。</summary>
    public string? MapSourceSpot { get; private set; }

    /// <summary>来源行的稳定键：种类、标题与地点（列表行号不当身份）。</summary>
    public static string SourceKey(ItemSourceView v) => $"{v.Kind}|{v.Title}|{v.LocationId}";

    /// <summary>该来源行有合法已知地点（已发现的地点）才可定位、悬停看地点详情；未知地点不泄露。</summary>
    public bool SourceLocatable(ItemSourceView v) =>
        State != null && v.LocationId is string loc && S.Locations.TryGetValue(loc, out var l) && l.Discovered;

    /// <summary>B35b 试玩修复 M1157（Q599 B）：侧页开着且某条列着的来源行在该已知地点——悬停看地点详情（B42 Node）只认这些地点。</summary>
    private bool CraftSourceNodeListed(string locationId) =>
        CraftSourcesOpen && CraftSources().Any(v => v.LocationId == locationId && SourceLocatable(v));

    /// <summary>侧页当前物品的来源（每次按当前真实状态现算）。</summary>
    public List<ItemSourceView> CraftSources() => State == null || CraftSourceItem == null ? new() : E.ItemSources(S, CraftSourceItem);

    /// <summary>侧页所选来源（键已失效为 null）。</summary>
    public ItemSourceView? CraftSourcePicked() =>
        CraftSourceKey == null ? null : CraftSources().FirstOrDefault(v => SourceKey(v) == CraftSourceKey);

    /// <summary>
    /// 缺料行“查看来源”：正常新开侧页，按原来源顺序默认选第一个同时“可获取”且有已知地点的来源；都没有就不选（Q911 A，不退而选不可获取或未知地点行）。
    /// </summary>
    public void OpenCraftSources(string itemId)
    {
        if (State == null || Panel != "craft") return;
        PeekClear();
        CraftSourceItem = itemId;
        CraftSourceKey = E.ItemSources(S, itemId).FirstOrDefault(v => v.Usable && SourceLocatable(v)) is { } first ? SourceKey(first) : null;
        Notify();
    }

    /// <summary>侧页里点选一行来源（只在本页内右列看详情；不开图、不移动）。</summary>
    public void PickCraftSource(string key)
    {
        if (!CraftSourcesOpen) return;
        CraftSourceKey = key;
        Notify();
    }

    /// <summary>侧页“返回配方”（与 ESC 同效）：回配方视图，不连退两层；配方、份数与指定目标原样保留。</summary>
    public void CloseCraftSources()
    {
        if (CraftSourceItem == null) return;
        PeekClear();
        DropCraftSources();
        Notify();
    }

    /// <summary>离开侧页（返回配方、改选配方或分类、面板真正关闭）：不保留来源选择与定位意图（Q949）。</summary>
    private void DropCraftSources()
    {
        CraftSourceItem = null;
        CraftSourceKey = null;
    }

    /// <summary>
    /// 内右列“在地图上定位”：地图盖在制作窗口上（制作窗口连同侧页与所选来源留在下层），切到该地点所在层、居中并高亮；
    /// 不选为移动目的地、不出发、不拿物。地点未知或所选已失效时不动。
    /// </summary>
    public void LocateCraftSource()
    {
        if (!CraftSourcesOpen || CraftSourcePicked() is not { } v || !SourceLocatable(v)) return;
        var item = CraftSourceItem;
        var loc = v.LocationId!;
        SwitchPanel("map", null, true);
        if (Panel != "map") return;
        SourceItem = item;
        MapSourceSpot = loc;
        MapLayer = E.MapLayerOf(loc);
        MapAnchor = S.CurrentLocation;
        MapFocus = loc;
        ClosePop();
        Notify();
    }

    /// <summary>
    /// 地图回到侧页时重验所选来源：仍在当前来源里就保留，失效就清掉选择（内右列留空），不换成同名或别的行（Q592）。
    /// </summary>
    private void RevalidateCraftSources()
    {
        if (CraftSourceItem == null || CraftSourceKey == null) return;
        if (CraftSourcePicked() == null) CraftSourceKey = null;
    }
}
