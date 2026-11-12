using System;
using System.Collections.Generic;
using System.Linq;
using Survival.Core.Config;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>
    /// 鸟瞰定稿文案（批次 B26，Q222/Q223；策划合同 content-feeding-adjudications.json cityCopy，逐字）。
    /// 地点名、段数都由调用方按真实已知路线填入；到达终点用 Arrived，不写“还有0段”。
    /// </summary>
    public static class CityCopy
    {
        public const string ReadOnly = "主城鸟瞰浏览中只能查看；使用、转移、穿戴、融合和开始行动，请先点“返回当前地点”再操作。";
        public const string LegacyReveal = "已按旧存档的到访记录补显城内建筑，仅用于浏览与定位，不补发奖励，也不开通新路线。";
        public static string Outside(string place) => $"{place}是城外独立地点，不在鸟瞰内，经区域地图前往。";
        public static string Confirm(int legs, string from, string to) => $"共{legs}段。本次先走{from}→{to}，到达后请再次选择下一段。";
        public static string Arrival(string at, string target, int left) => $"已到达{at}。继续前往{target}：请再打开“主城鸟瞰”选择下一段（还有{left}段）。";
        public static string Arrived(string target) => $"已到达{target}。";
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
        /// <summary>当前真实所在地点（一定在本城内）。</summary>
        public string HereId { get; set; } = "";
        /// <summary>“前往”现在不能提交的原因（主行动占用等）；能提交为 null。浏览照常。</summary>
        public string? TravelBlocked { get; set; }
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
        /// 主行动进行中/暂停时可以浏览（“前往”另看 <see cref="CityOverviewView.TravelBlocked"/>）。
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

        /// <summary>“前往”现在不能提交的原因：先满足能打开，再要求没有主/子行动。</summary>
        public string? CityTravelBlocked(GameState s)
        {
            if (CityOverviewBlocked(s) is string why) return why;
            if (s.Main != null || s.Sub != null) return "主行动占用：先结束当前行动";
            return null;
        }

        /// <summary>当前所在主城的鸟瞰视图；不在三城内为 null。只读、0 分钟。</summary>
        public CityOverviewView? CityOverviewOf(GameState s)
        {
            var here = s.CurrentLocation;
            var city = CityOf(here);
            if (city == null) return null;
            var v = new CityOverviewView { CityId = city, Title = CityTitle(city), HereId = here, TravelBlocked = CityTravelBlocked(s) };
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
                if (b.IsHere) b.Path = new List<MapPathStep>();
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
                v.Roads.Add(new MapViewEdge { Id = e.Id, A = e.A, B = e.B, Open = open, Minutes = e.BaseMinutes, Reason = open ? "" : GateBlockedReason(s, e), Near = e.Touches(here) });
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

        // ───────────────────────── 前往（A10-CITY §5，开发假设 B24-4） ─────────────────────────

        /// <summary>
        /// 鸟瞰里点“前往”：重新核对真实位置、目标、城内路线与门槛，只提交第一条相邻边的普通移动（首期逐段：到达后再次选择下一段，
        /// A10-CITY §5 末段允许）；同一请求号重复提交只生效一次（双击不重复走路）。目标不进存档。路过/到达按真实到达处理。
        /// </summary>
        public ActionResult StartCityOverviewTravel(GameState s, string targetId, string? requestId = null)
        {
            if (SeenRequest(s, requestId)) return ActionResult.Repeated();
            if (CityTravelBlocked(s) is string why) return ActionResult.Fail(why);
            var here = s.CurrentLocation;
            var city = CityOf(here)!;
            if (CityOf(targetId) != city) return ActionResult.Fail("目标不在这座城内");
            if (targetId == here) return ActionResult.Fail("已在此地");
            if (!Known(s, targetId)) return ActionResult.Fail("尚未发现");
            var path = CityPath(s, city, here, targetId);
            if (path == null) return ActionResult.Fail(CityPathBlocked(s, city, here, targetId));
            var r = ActionRequest.Travel(path[0].To);
            r.RequestId = requestId;
            return Start(s, r);
        }

        /// <summary>
        /// 鸟瞰“前往”走完一段后的提示（批次 B26，Q223）：到终点写 Arrived；还在半路按真实已知路线写还剩几段；路线已失效写真实原因，不按旧文案强走。
        /// 没到这一段的终点（中途退出、仍在行动）不写。只读。
        /// </summary>
        public string? CityArrivalNote(GameState s, string targetId, string? firstTo)
        {
            if (firstTo == null || s.Main != null || s.CurrentLocation != firstTo) return null;
            if (s.CurrentLocation == targetId) return CityCopy.Arrived(LocName(targetId));
            var city = CityOf(s.CurrentLocation);
            if (city == null || CityOf(targetId) != city) return null;
            var path = CityPath(s, city, s.CurrentLocation, targetId);
            return path == null ? CityPathBlocked(s, city, s.CurrentLocation, targetId) : CityCopy.Arrival(LocName(s.CurrentLocation), LocName(targetId), path.Count);
        }

        /// <summary>打开鸟瞰时默认选中的建筑：多段路线还没走到的最终目标（手动打开与首次自动打开同一口径）；已到或已离开这座城为 null。只读。</summary>
        public string? CityDefaultPick(GameState s, string? targetId)
        {
            if (targetId == null || targetId == s.CurrentLocation) return null;
            var city = CityOf(s.CurrentLocation);
            return city != null && CityOf(targetId) == city ? targetId : null;
        }

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
