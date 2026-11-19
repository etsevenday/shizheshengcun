using System.Collections.Generic;
using System.Linq;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>
    /// 区域地图路线区与“出发”的定稿文案（批次 B35c；S14 2026-11-13 B35 节 Q591、2026-11-16 B35 节 Q793／Q803、2026-11-17 B35 次要节 Q915／Q916；逐字稿 §2 P07a）。
    /// </summary>
    public static class MapCopy
    {
        /// <summary>Q591：唯一普通移动按钮业务名（不写“前往下一站”，不把时长拼进按钮名）。</summary>
        public const string Depart = "出发";
        /// <summary>Q803：没选目的地时的默认提示（路线区与右栏当前位置页）。BM12（Q1002 A 定稿，合同 b35c-q1001-q1006 copy.noSelection）：引号按定稿写“出发”。</summary>
        public const string DefaultHint = "选相邻地点，再点“出发”。远处地点可查看路线。";
        /// <summary>Q803：右栏只读预估脚注。</summary>
        public const string FootNote = "在路线区点‘出发’；耗时为预计值。";
        /// <summary>Q915 B：选中远处地点、出发因不相邻而禁用时，按钮旁只写这一句。</summary>
        public const string PickAdjacent = "先选相邻地点。";
        /// <summary>Q916 A：系统说明三处移动帮助同句（Q1001 A 定稿 R315：就近执行只换移动分句、移动段换第二句、分层地图按“路线区会列出…”＋本句＋“浏览、缩放、切层都不推进游戏时间。”）。</summary>
        public const string Help = "点相邻地图卡在浮窗里确认移动；在区域地图上，先选相邻地点，在路线区点“出发”。";
        /// <summary>逐字稿 §2 P07a 区名。</summary>
        public const string Here = "当前位置";
        public const string Target = "所选目的地";
        public const string NextLegTitle = "下一路段";
        public const string RoutesTitle = "已知路线";
        /// <summary>Q595：地图底部业务按钮。</summary>
        public const string World = "主世界";
        public const string FactionView = "势力鸟瞰";
        /// <summary>Q808：下一路段原格式（Q1002 A 定稿：普通地图路线区同用）。</summary>
        public static string NextLeg(string from, string to, int minutes) => $"{from}→{to} · 基础{minutes}分钟";
        /// <summary>原右栏“选中下一段”按钮（A10-EXUI §1；Q793 B 保留，移进路线区）。</summary>
        public static string SelectNext(string to) => $"选中下一段：前往{to}";
        /// <summary>原右栏两句真实拒绝原因（相邻未开通、远处无已开通路线），B35c 起由路线区与出发拦截共用。</summary>
        public static string EdgeLocked(string reason) => $"路线未开通，{reason}";
        public const string NoRoute = "目前没有已开通的路线能走到这里";
        public const string Unknown = "尚未发现";
    }

    /// <summary>区域地图路线区（地图普通层与城市层共用）：当前位置、所选目的地、下一路段、已知路线与“出发”的结构性拦截。只读。</summary>
    public sealed class MapRouteView
    {
        public string HereId { get; set; } = "";
        /// <summary>所选目的地；没选或选的就是当前位置为 null（路线区显示 Q803 默认句）。</summary>
        public string? TargetId { get; set; }
        /// <summary>目的地与当前位置之间有已开通、可从这里出发的直连路线（只有这时“出发”可能可用）。</summary>
        public bool Adjacent { get; set; }
        /// <summary>下一路段：相邻时为直连这一段；远处为已知路线的第一段；没有可走路线为 null。</summary>
        public MapPathStep? NextLeg { get; set; }
        /// <summary>已知路线（逐段，基础分钟）；没有为 null。</summary>
        public List<MapPathStep>? Path { get; set; }
        /// <summary>远处目的地：“选中下一段”要改选的真实相邻首站（Q793 B）；相邻或无路线为 null。</summary>
        public string? SelectNext { get; set; }
        /// <summary>“出发”的结构性拦截（不相邻＝“先选相邻地点。”、路线未开通、无路线、尚未发现）；相邻为 null，具体能否走再看移动预览的真实原因。</summary>
        public string? Blocked { get; set; }
    }

    public sealed partial class GameEngine
    {
        /// <summary>
        /// 路线区视图（B35c；Q591、Q793 B、Q808、Q915）：选中只看、不移动。相邻优先用直连边；直连边未开通时写原“路线未开通”原因；
        /// 远处按真实已知路线（同城内用城内最短路，其余用地图最短路）给第一段与“选中下一段”，出发拦截写“先选相邻地点。”。
        /// </summary>
        public MapRouteView MapRouteOf(GameState s, string? targetId)
        {
            var here = s.CurrentLocation;
            var r = new MapRouteView { HereId = here };
            if (targetId == null || targetId == here || !_locs.ContainsKey(targetId)) return r;
            r.TargetId = targetId;
            if (!Known(s, targetId)) { r.Blocked = MapCopy.Unknown; return r; }
            var direct = AdjacentUnlocked(s).Where(e => e.Other(here) == targetId).OrderBy(e => e.BaseMinutes).FirstOrDefault();
            if (direct != null)
            {
                r.Adjacent = true;
                r.NextLeg = new MapPathStep { From = here, To = targetId, Minutes = direct.BaseMinutes };
                r.Path = new List<MapPathStep> { r.NextLeg };
                return r;
            }
            var locked = AdjacentLocked(s).FirstOrDefault(x => x.Target == targetId);
            if (locked.Edge != null) { r.Blocked = MapCopy.EdgeLocked(locked.Reason); return r; }
            var city = CityOf(here);
            var sameCity = city != null && CityOf(targetId) == city;
            var path = sameCity ? CityPath(s, city!, here, targetId) : MapPathTo(s, targetId);
            if (path != null && path.Count > 0)
            {
                r.Path = path;
                r.NextLeg = path[0];
                r.SelectNext = path[0].To;
                r.Blocked = MapCopy.PickAdjacent;
                return r;
            }
            r.Blocked = sameCity ? CityPathBlocked(s, city!, here, targetId) : MapCopy.NoRoute;
            return r;
        }

        /// <summary>
        /// 这一段现在能不能真实开工（与移动开工同一校验，含远征进入确认标记）；能为 null。用于“当前可出发”的相邻连线高亮（Q591：
        /// 未知、锁定、单向不通及被原权限阻断的边不冒充可走）。只读。
        /// </summary>
        public string? TravelLegBlocked(GameState s, string targetId) =>
            Validate(s, ActionRequest.Travel(targetId, NeedsExpeditionConfirm(s, targetId)), out _);

        /// <summary>这条已知边现在是不是“当前可出发”的相邻路线：从当前位置出发、已开通、此端可走，且移动开工校验通过。</summary>
        private bool EdgeDepartable(GameState s, Config.EdgeConfig e, bool open)
        {
            var here = s.CurrentLocation;
            return open && e.Touches(here) && e.TraversableFrom(here) && TravelLegBlocked(s, e.Other(here)) == null;
        }

        /// <summary>
        /// 路线区“出发”（B35c；Q591、Q793 B、Q595“移动相关实际执行与 Q591 普通出发同源”）：地图普通层与城市层共用。
        /// 重新核对目的地与相邻关系：远处一律拒绝“先选相邻地点。”（不替玩家选站、不连走）；相邻才提交这一段普通移动（远征进入自动带确认标记，
        /// 与原“确认开始远征并前往”同一请求）。同一请求号重复提交只生效一次。
        /// </summary>
        public ActionResult StartMapDeparture(GameState s, string targetId, string? requestId = null)
        {
            if (SeenRequest(s, requestId)) return ActionResult.Repeated();
            if (targetId == s.CurrentLocation) return ActionResult.Fail("已在此地");
            var route = MapRouteOf(s, targetId);
            if (route.Blocked != null) return ActionResult.Fail(route.Blocked);
            var r = ActionRequest.Travel(targetId, NeedsExpeditionConfirm(s, targetId));
            r.RequestId = requestId;
            return Start(s, r);
        }
    }
}
