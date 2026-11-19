using System;
using System.Collections.Generic;
using System.Linq;
using Survival.Core.Config;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>
    /// 鸟瞰定稿文案（批次 B26，Q222/Q223；策划合同 content-feeding-adjudications.json cityCopy，逐字）。
    /// B35c（S14 2026-11-16 B35 节 Q804、B36 节 Q808／Q810，2026-11-17 B35 次要节 Q913／Q914）：鸟瞰并入地图城市层——
    /// 只读句换 Q913；删 confirm 多段句（Q914）与“继续前往”到达句（Q804），到达只留 Arrived；新增远城只读句、图例句与未选提示。
    /// </summary>
    public static class CityCopy
    {
        /// <summary>Q913 A：城市层只读下尝试随身操作的提示（取代要求点“返回当前地点”的旧句）。</summary>
        public const string ReadOnly = "在城市层只能查看；关闭地图后再操作。";
        public const string LegacyReveal = "已按旧存档的到访记录补显城内建筑，仅用于浏览与定位，不补发奖励，也不开通新路线。";
        public static string Outside(string place) => $"{place}是城外独立地点，不在鸟瞰内，经区域地图前往。";
        /// <summary>Q804：城市层点“出发”到达后的提示（Q1005 A 定稿 R315：只在城市层出发且实际到达后显示）。</summary>
        public static string Arrived(string target) => $"已到达{target}。";
        /// <summary>Q810 A：远城只读，替代下一路段与“出发”。</summary>
        public const string RemoteOnly = "仅供查看；前往请用主世界地图。";
        /// <summary>Q804：城市层图例移动句。BM12（Q1003 A 定稿，合同 b35c-q1001-q1006 copy.cityLegend）：引号按定稿写“出发”。</summary>
        public const string LegendMove = "选相邻建筑，再点“出发”。";
        /// <summary>原图例前半句（Q1003 A 定稿 R315：保留，后接 Q804 句）。</summary>
        public const string LegendRoads = "道路只连已发现的城内真实路线。";
        /// <summary>Q808 A：城市层右列未选建筑时的提示（逐字稿 §2 P07b）。</summary>
        public const string NoPick = "选择图上的建筑查看路线。";
        /// <summary>逐字稿 §2 P07b 区名。</summary>
        public const string PickedTitle = "所选建筑";
    }

    /// <summary>
    /// 三城主城鸟瞰的只读视图（开发计划 v0.9 批次 B24，R73；A10「A10-CITY」§2—§7、S14「中枢联动归档」「建筑、NPC与物理移动」）。
    /// 只用玩家已发现的信息：未发现的楼栋只给轮廓（不给名称、人物、服务）；打开、浏览、选建筑都 0 分钟、不改状态。
    /// </summary>
    public sealed class CityOverviewView
    {
        /// <summary>父城节点（h02_square / c02_church / r02_academy）。</summary>
        public string CityId { get; set; } = "";
        /// <summary>城邦名（“幸存者营区”）。</summary>
        public string Title { get; set; } = "";
        /// <summary>当前真实所在地点（本城视图在本城内；远城视图为城外的真实所在地）。</summary>
        public string HereId { get; set; } = "";
        /// <summary>B35c（Q810 A）：远城只读视图——人不在这座城里：不给路线、下一路段和“出发”，只看公开建筑信息。</summary>
        public bool Remote { get; set; }
        public List<CityBuildingView> Buildings { get; set; } = new List<CityBuildingView>();
        /// <summary>本城内两端都已知、已开通或已揭示的内部边（只画已知道路，不画捷径）。</summary>
        public List<MapViewEdge> Roads { get; set; } = new List<MapViewEdge>();
        /// <summary>网格外框（建筑坐标用 v3 gridX/gridY）。</summary>
        public int MinX { get; set; }
        public int MaxX { get; set; }
        public int MinY { get; set; }
        public int MaxY { get; set; }
    }

    /// <summary>鸟瞰里的一栋建筑（或父城区域）。</summary>
    public sealed class CityBuildingView
    {
        public string NodeId { get; set; } = "";
        /// <summary>已发现时的地点名；未发现为空（只画轮廓，写“尚未发现”）。</summary>
        public string Name { get; set; } = "";
        public bool Discovered { get; set; }
        public bool IsHere { get; set; }
        /// <summary>父城区域本身（接待庭/外院/广场）。</summary>
        public bool IsParent { get; set; }
        public int GridX { get; set; }
        public int GridY { get; set; }
        /// <summary>城内已开通路线（从当前地点出发，按基础分钟最短）；当前地点为空表；不可达为 null。</summary>
        public List<MapPathStep>? Path { get; set; }
        public int PathMinutes => Path?.Sum(p => p.Minutes) ?? 0;
        /// <summary>去不了的原因（未发现、道路未发现、门槛原因）；可去或就是当前地点为 null。</summary>
        public string? Blocked { get; set; }
        /// <summary>人物短句（“陆衡 · 值守登记员 · 岗位”“机构当班接续员 · 代班”“韩策 · 固定同行者”）；未发现为空。</summary>
        public List<string> People { get; set; } = new List<string>();
        /// <summary>可公开的服务正式名（只列已实装的）。</summary>
        public List<string> Services { get; set; } = new List<string>();
        /// <summary>本栋有可接的委托/邀约（已显示且前置满足、未接）。</summary>
        public int CanAccept { get; set; }
        /// <summary>本栋有可交的委托（已接且记录已取得，或有可在这里办的工作）。</summary>
        public int CanTurnIn { get; set; }
        /// <summary>任务标记的辅助文字（“可接：鉴定之前”）；没有为空。</summary>
        public string QuestNote { get; set; } = "";
    }

    public sealed partial class GameEngine
    {
        /// <summary>某地点所在的主城（父城节点）：城内建筑为其父城，父城节点本身为自己；不在三城内为 null（h05_homestead、c03、r03 是独立地点）。</summary>
        public string? CityOf(string locationId)
        {
            if (!_locs.TryGetValue(locationId, out var l)) return null;
            if (l.NodeKind == "city_building" && l.ParentCity != null) return l.ParentCity;
            return IsCityParent(locationId) ? locationId : null;
        }

        /// <summary>三城父节点：有城内建筑的世界节点（h02_square、c02_church、r02_academy）。</summary>
        public bool IsCityParent(string locationId) => CityBuildings(locationId).Any();

        /// <summary>全部主城父节点（配置顺序）。</summary>
        public List<string> CityParents() =>
            Config.Locations.Where(l => l.NodeKind == "city_building" && l.ParentCity != null).Select(l => l.ParentCity!).Distinct().ToList();

        /// <summary>某城的父节点与全部城内建筑（配置顺序，父节点在前）。</summary>
        public List<string> CityMembers(string cityId) => new[] { cityId }.Concat(CityBuildings(cityId)).ToList();

        /// <summary>城邦名：取本城岗位人物的组织名（三城各一）；没有岗位时用父城地点名。</summary>
        public string CityTitle(string cityId)
        {
            var members = CityMembers(cityId);
            var npc = Config.City.Npcs.FirstOrDefault(n => members.Contains(n.LocationId));
            return npc != null ? FactionName(npc.FactionId) : LocName(cityId);
        }

        /// <summary>
        /// 鸟瞰能不能打开（A10-CITY §7、开发假设 B24-2）：死亡、对白读完锁、战斗与敌踪中不能开；不在三城内不能开。
        /// 主行动进行中/暂停时可以浏览。B35c 起地点卡“主城鸟瞰”快捷入口据此打开同一地图的本城层（Q595），“出发”走普通移动校验。
        /// </summary>
        public string? CityOverviewBlocked(GameState s)
        {
            if (s.Progress.Dead) return "角色已死亡";
            if (DialogueLocked(s)) return DialogueLockedReason;
            if (s.Battle != null) return "先处理眼前的敌踪";
            if (s.CurrentZone == GameState.SafehouseZone) return "先回到外部区";
            if (CityOf(s.CurrentLocation) == null) return "只在三城城内可以打开主城鸟瞰";
            return null;
        }

        /// <summary>首次到城自动打开的安全空闲判定：先满足能打开，再要求没有主/子行动（B35c 起只用于自动打开，“出发”走普通移动校验）。</summary>
        public string? CityTravelBlocked(GameState s)
        {
            if (CityOverviewBlocked(s) is string why) return why;
            if (s.Main != null || s.Sub != null) return "主行动占用：先结束当前行动";
            return null;
        }

        /// <summary>当前所在主城的鸟瞰视图；不在三城内为 null。只读、0 分钟。</summary>
        public CityOverviewView? CityOverviewOf(GameState s)
        {
            var city = CityOf(s.CurrentLocation);
            return city == null ? null : CityOverviewOf(s, city);
        }

        /// <summary>
        /// 某城的鸟瞰视图（B35c，Q595／Q810：地图城市层）：人在这座城里为本城视图（路线、可出发标记）；人不在这座城里为远城只读视图——
        /// 已发现建筑照常给公开名称、人物、服务与任务标记，不给路线与下一路段，未发现仍只给轮廓。不是三城父节点或父城未发现为 null。只读、0 分钟。
        /// </summary>
        public CityOverviewView? CityOverviewOf(GameState s, string city)
        {
            if (!IsCityParent(city) || !Known(s, city)) return null;
            var here = s.CurrentLocation;
            var remote = CityOf(here) != city;
            var v = new CityOverviewView { CityId = city, Title = CityTitle(city), HereId = here, Remote = remote };
            var members = CityMembers(city);
            foreach (var id in members)
            {
                var lc = _locs[id];
                var b = new CityBuildingView { NodeId = id, IsParent = id == city, IsHere = id == here, GridX = lc.GridX, GridY = lc.GridY, Discovered = Known(s, id) };
                if (b.Discovered)
                {
                    b.Name = lc.Name;
                    FillBuilding(s, b);
                }
                if (remote) { if (!b.Discovered) b.Blocked = MapCopy.Unknown; }
                else if (b.IsHere) b.Path = new List<MapPathStep>();
                else
                {
                    b.Path = b.Discovered ? CityPath(s, city, here, id) : null;
                    if (b.Path == null) b.Blocked = b.Discovered ? CityPathBlocked(s, city, here, id) : "尚未发现";
                }
                v.Buildings.Add(b);
            }
            v.MinX = v.Buildings.Min(b => b.GridX); v.MaxX = v.Buildings.Max(b => b.GridX);
            v.MinY = v.Buildings.Min(b => b.GridY); v.MaxY = v.Buildings.Max(b => b.GridY);
            var set = new HashSet<string>(members);
            foreach (var e in Config.Edges.Where(e => set.Contains(e.A) && set.Contains(e.B)))
            {
                if (!Known(s, e.A) || !Known(s, e.B)) continue;
                var open = EdgeOpen(s, e);
                if (!open && !EdgeRevealed(s, e)) continue;
                v.Roads.Add(new MapViewEdge { Id = e.Id, A = e.A, B = e.B, Open = open, Minutes = e.BaseMinutes, Reason = open ? "" : GateBlockedReason(s, e), Near = !remote && e.Touches(here), Departable = !remote && EdgeDepartable(s, e, open) });
            }
            return v;
        }

        /// <summary>已发现建筑的人物、服务与任务标记（A10-CITY §4/§6，开发假设 B24-5）：只定位，不接任务、不发奖。</summary>
        private void FillBuilding(GameState s, CityBuildingView b)
        {
            var id = b.NodeId;
            foreach (var n in Config.City.Npcs.Where(n => n.LocationId == id))
            {
                b.People.Add(s.City.Absent.Contains(n.NpcId) ? $"{n.DutySpeaker} · 代班" : $"{n.Name} · {n.Role} · 岗位");
                foreach (var sid in n.ServiceIds)
                    if (CityService(sid) is CityServiceConfig svc && svc.Implemented && !b.Services.Contains(svc.Title)) b.Services.Add(svc.Title);
            }
            var accept = new List<string>();
            var turnin = new List<string>();
            foreach (var q in LiveQuests.Where(q => q.LocationId == id || q.WorkServiceIds.Any(x => CityService(x)?.LocationId == id)))
            {
                var home = q.LocationId == id;
                if (home && q.Kind == "recruit" && !QuestDone(s, q.Id) && RosterOf(s, q.NpcId) == null && QuestRequiresBlocked(s, q) == null && Config.Party.Named(q.NpcId) is NamedCompanionConfig c)
                    b.People.Add($"{c.Name} · 固定同行者");
                var qv = QuestViewOf(s, q.Id);
                if (qv == null) continue;
                if (home && qv.Status == "available") accept.Add(qv.Title);
                else if (qv.Status == "active" && ((home && HasQuestRecord(s, q)) || QuestWorkOpenAt(s, q, qv, id))) turnin.Add(qv.Title);
            }
            b.CanAccept = accept.Count;
            b.CanTurnIn = turnin.Count;
            var parts = new List<string>();
            if (accept.Count > 0) parts.Add("可接：" + string.Join("、", accept));
            if (turnin.Count > 0) parts.Add("可交：" + string.Join("、", turnin));
            b.QuestNote = string.Join("；", parts);
        }

        /// <summary>
        /// Q224“该楼栋有可办理的委托工作”：任务条目里的工作（0 分钟交谈或 CH01 这类计时工作）在任务所在楼栋且工作前置已满足，
        /// 或经这栋楼事项办理的工作（CH02/AC01/AC02）现在能办。人不在那里不算阻拦（标记本来就是定位用的）。只读。
        /// </summary>
        private bool QuestWorkOpenAt(GameState s, QuestConfig q, QuestView qv, string id)
        {
            if (q.LocationId == id && qv.WorkLabel != null && q.WorkRequiresAll.All(s.Progress.Flags.Contains)) return true;
            return q.WorkServiceIds.Select(CityService).Any(svc => svc != null && svc.Implemented && svc.LocationId == id && QuestServiceBlocked(s, svc, q) == null);
        }

        /// <summary>
        /// 城内最短路（A10-CITY §5、开发假设 B24-3）：只走本城父节点与城内建筑之间已开通、可从这一端出发、两端已发现的内部边，
        /// 按基础分钟最短，同长按配置地点顺序；不经城外节点。到不了为 null，起终点相同为空表。只读。
        /// </summary>
        public List<MapPathStep>? CityPath(GameState s, string cityId, string fromId, string targetId)
        {
            var set = new HashSet<string>(CityMembers(cityId));
            if (!set.Contains(fromId) || !set.Contains(targetId)) return null;
            if (fromId == targetId) return new List<MapPathStep>();
            if (!Known(s, targetId)) return null;
            return ShortestIn(set, fromId, targetId, e => EdgeOpen(s, e) && Known(s, e.A) && Known(s, e.B));
        }

        /// <summary>城内到不了的原因：沿已知（已开通或已揭示）的内部路能连上时，写第一条未开通边的门槛原因；连不上写“道路未发现”。</summary>
        private string CityPathBlocked(GameState s, string cityId, string fromId, string targetId)
        {
            var set = new HashSet<string>(CityMembers(cityId));
            var known = ShortestIn(set, fromId, targetId, e => Known(s, e.A) && Known(s, e.B) && (EdgeOpen(s, e) || EdgeRevealed(s, e)));
            if (known == null) return "道路未发现";
            foreach (var st in known)
            {
                var e = EdgeBetween(st.From, st.To);
                if (e != null && !EdgeOpen(s, e))
                {
                    var why = GateBlockedReason(s, e);
                    return why.Length > 0 ? why : "目前没有已开通的路线";
                }
            }
            return "目前没有已开通的路线";
        }

        private List<MapPathStep>? ShortestIn(HashSet<string> set, string fromId, string targetId, Func<EdgeConfig, bool> usable)
        {
            var dist = new Dictionary<string, int> { [fromId] = 0 };
            var prev = new Dictionary<string, (string From, EdgeConfig Edge)>();
            var done = new HashSet<string>();
            var order = Config.Locations.Select((l, i) => (l.Id, i)).ToDictionary(x => x.Id, x => x.i);
            while (true)
            {
                string? u = null;
                foreach (var kv in dist)
                    if (!done.Contains(kv.Key) && (u == null || kv.Value < dist[u] || (kv.Value == dist[u] && order[kv.Key] < order[u]))) u = kv.Key;
                if (u == null) return null;
                if (u == targetId) break;
                done.Add(u);
                foreach (var e in EdgesAt(u))
                {
                    if (!e.TraversableFrom(u) || !usable(e)) continue;
                    var w = e.Other(u);
                    if (!set.Contains(w) || done.Contains(w)) continue;
                    var nd = dist[u] + e.BaseMinutes;
                    if (!dist.TryGetValue(w, out var old) || nd < old) { dist[w] = nd; prev[w] = (u, e); }
                }
            }
            var steps = new List<MapPathStep>();
            for (var at = targetId; at != fromId; at = prev[at].From)
                steps.Add(new MapPathStep { From = prev[at].From, To = at, Minutes = prev[at].Edge.BaseMinutes });
            steps.Reverse();
            return steps;
        }

        /// <summary>
        /// 城市层点“出发”走完这一段后的提示（Q804：“已到达{地点}。”；B35c 删“继续前往原远处目标”旧提醒，Q1005 A 定稿只在城市层出发后由界面调用）。
        /// 只在这一段已真实到达（人在目标、没有进行中的主行动）时写；中途退出、仍在路上为 null。只读。
        /// </summary>
        public string? CityArrivalNote(GameState s, string targetId) =>
            s.Main == null && s.CurrentLocation == targetId ? CityCopy.Arrived(LocName(targetId)) : null;

        /// <summary>城外独立地点的提示（A10-CITY §2 特别边界，Q222）：只在玩家已发现该地点时写；这些地点不进城内路线图。</summary>
        public string? CityOutsideNote(GameState s, string cityId)
        {
            var id = cityId switch { "h02_square" => "h05_homestead", "c02_church" => "c03_crypt", "r02_academy" => "r03_lab", _ => null };
            if (id == null || !s.Locations.TryGetValue(id, out var l) || !l.Discovered) return null;
            return CityCopy.Outside(LocName(id));
        }

        // ───────────────────────── 首次到达自动打开（A10-CITY §3，开发假设 B24-1） ─────────────────────────

        /// <summary>实际到达成功后：首次到某城父节点且该城还没展示过鸟瞰，记“待自动打开”（真正打开在对白读完、安全空闲时）。不发任何东西。</summary>
        private void NoteCityOverviewArrival(GameState s, string locationId)
        {
            if (!IsCityParent(locationId)) return;
            var c = s.City;
            if (c.OverviewShown.Contains(locationId) || c.OverviewPending == locationId) return;
            c.OverviewPending = locationId;
            c.OverviewPendingLegacy = false;
        }

        /// <summary>
        /// 现在该不该自动打开鸟瞰：有待打开的城、人就在那座城里，且安全空闲（未死亡、无对白、无主/子行动、无战斗与敌踪）。只读。
        /// </summary>
        public bool CityOverviewAutoOpenDue(GameState s)
        {
            var p = s.City.OverviewPending;
            if (p == null || CityOf(s.CurrentLocation) != p) return false;
            return CityTravelBlocked(s) == null;
        }

        /// <summary>界面已自动打开鸟瞰：记该城已展示（按城一次，刷新/读档不再弹）。返回是否旧档首次说明（界面顶部写一句）。</summary>
        public bool MarkCityOverviewShown(GameState s)
        {
            var c = s.City;
            if (c.OverviewPending == null) return false;
            var legacy = c.OverviewPendingLegacy;
            c.OverviewShown.Add(c.OverviewPending);
            c.OverviewPending = null;
            c.OverviewPendingLegacy = false;
            return legacy;
        }

        /// <summary>开发入口（?dev=1）：清除三城鸟瞰的已展示与待打开标记（下次真实到达父节点再自动打开一次）。</summary>
        public string DebugResetCityOverview(GameState s)
        {
            var n = s.City.OverviewShown.Count;
            s.City.OverviewShown.Clear();
            s.City.OverviewPending = null;
            s.City.OverviewPendingLegacy = false;
            return $"开发：已清除主城鸟瞰展示标记（原有{n}城），再次到达父节点时自动打开一次。";
        }
    }
}
