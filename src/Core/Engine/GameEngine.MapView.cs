using System;
using System.Collections.Generic;
using System.Linq;
using Survival.Core.Config;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>分层地图的一层（只读视图，不改存档）。</summary>
    public sealed class MapView
    {
        /// <summary>层键：world / city:&lt;父城节点&gt; / dungeon:&lt;地牢ID&gt;。</summary>
        public string LayerKey { get; set; } = "world";
        public string LayerName { get; set; } = "";
        /// <summary>面包屑：“主世界 / 营区广场城内 / 营区工坊”（当前位置在本层时末段是当前地点）。</summary>
        public List<string> Breadcrumb { get; set; } = new List<string>();
        public List<MapViewNode> Nodes { get; set; } = new List<MapViewNode>();
        public List<MapViewEdge> Edges { get; set; } = new List<MapViewEdge>();
        /// <summary>跨层入口（城内建筑通往世界、地牢入口与出口）：画徽标，点了只切层，不移动。</summary>
        public List<MapPortal> Portals { get; set; } = new List<MapPortal>();
        /// <summary>本层代表当前位置的节点（城市折叠时为父城节点）；当前位置不在本层为 null。</summary>
        public string? HereNode { get; set; }
        public double MinX { get; set; }
        public double MinY { get; set; }
        public double MaxX { get; set; }
        public double MaxY { get; set; }
    }

    public sealed class MapViewNode
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public double X { get; set; }
        public double Y { get; set; }
        public bool Visited { get; set; }
        public int Progress { get; set; }
        public bool IsHere { get; set; }
        /// <summary>当前位置在这个折叠城市的城内建筑里。</summary>
        public bool HereInside { get; set; }
        public int? Danger { get; set; }
        /// <summary>可进入的下一层（城内/地牢）：徽标文字与层键。</summary>
        public string? SubLayer { get; set; }
        public string? SubLabel { get; set; }
        /// <summary>地牢房间角色（只给已到访房间）：守卫、核心记录、出口。</summary>
        public List<string> Roles { get; set; } = new List<string>();
    }

    public sealed class MapViewEdge
    {
        public string Id { get; set; } = "";
        public string A { get; set; } = "";
        public string B { get; set; } = "";
        public bool Open { get; set; }
        /// <summary>单向出口：只能从这一端走（双向为 null）。</summary>
        public string? OneWayFrom { get; set; }
        public int Minutes { get; set; }
        /// <summary>未开通原因（“需要：……”）；已开通为空。</summary>
        public string Reason { get; set; } = "";
        /// <summary>与当前位置相连。</summary>
        public bool Near { get; set; }
    }

    public sealed class MapPortal
    {
        public string FromNode { get; set; } = "";
        public string TargetId { get; set; } = "";
        public string TargetName { get; set; } = "";
        public string TargetLayer { get; set; } = "";
        public string TargetLayerName { get; set; } = "";
        public int Minutes { get; set; }
        public bool Open { get; set; }
        public bool OneWayOut { get; set; }
        public string Reason { get; set; } = "";
    }

    public sealed class MapPathStep
    {
        public string From { get; set; } = "";
        public string To { get; set; } = "";
        public int Minutes { get; set; }
    }

    /// <summary>
    /// 分层地图视图模型（开发计划 v0.9 批次 B12，R38；A10「A10-EXUI」§1「分层导航与发现权限」、S14「地图、来源与四类账」、S02「最新地图与道路」）。
    /// 三层：主世界 → 城内（parentCity）/地牢（dungeonId）；层只按 nodeKind/parentCity/dungeonId 判，不按 ID 前缀猜。
    /// 只画已发现地点、两端已知且已开通或已揭示的路线；城内建筑在主世界层折叠进父城节点（父城未发现时按自身画，不泄露父城名）；
    /// 地牢角色标记只给已到访房间（H7）。全部只读：不改存档、不推进时间、不发现任何东西（EXUI01）。
    /// </summary>
    public sealed partial class GameEngine
    {
        public const string WorldLayer = "world";

        /// <summary>地点所在图层：地牢房间 dungeon:dXX；城内建筑 city:父城；其余 world。</summary>
        public string MapLayerOf(string locationId)
        {
            var l = _locs[locationId];
            if (l.NodeKind == "dungeon" && l.DungeonId != null) return "dungeon:" + l.DungeonId;
            if (l.NodeKind == "city_building" && l.ParentCity != null) return "city:" + l.ParentCity;
            return WorldLayer;
        }

        public string MapLayerName(string layer)
        {
            if (layer.StartsWith("city:")) return $"{LocName(layer.Substring(5))}城内";
            if (layer.StartsWith("dungeon:"))
            {
                var d = layer.Substring(8);
                return Config.Map.Dungeons.FirstOrDefault(x => x.Id == d)?.Name ?? d;
            }
            return "主世界";
        }

        private bool Known(GameState s, string id) => s.Locations.TryGetValue(id, out var l) && l.Discovered;

        /// <summary>主世界层里代表该地点的节点：父城已发现的城内建筑折叠进父城节点。</summary>
        private string WorldRep(GameState s, string id)
        {
            var l = _locs[id];
            return l.NodeKind == "city_building" && l.ParentCity != null && Known(s, l.ParentCity) ? l.ParentCity : id;
        }

        private IEnumerable<string> CityBuildings(string parent) => Config.Locations.Where(l => l.NodeKind == "city_building" && l.ParentCity == parent).Select(l => l.Id);

        /// <summary>
        /// 打开地图的默认层（A10-EXUI §1“默认聚焦当前地区”）：在地牢里为该地牢；在城内建筑且父城已知为该城；站在有已知建筑的父城节点为城内层（H7）；其余主世界。
        /// </summary>
        public string DefaultMapLayer(GameState s)
        {
            var here = s.CurrentLocation;
            var layer = MapLayerOf(here);
            if (layer.StartsWith("dungeon:")) return layer;
            if (layer.StartsWith("city:")) return Known(s, _locs[here].ParentCity!) ? layer : WorldLayer;
            if (CityBuildings(here).Any(b => Known(s, b))) return "city:" + here;
            return WorldLayer;
        }

        /// <summary>可切换的层（至少一个已知节点）：主世界、各已知城市（父城已知且有已知建筑，或人在城内）、各已知地牢。</summary>
        public List<(string Key, string Name)> KnownMapLayers(GameState s)
        {
            var list = new List<(string, string)> { (WorldLayer, MapLayerName(WorldLayer)) };
            var here = DefaultMapLayer(s);
            foreach (var parent in Config.Locations.Where(l => l.NodeKind == "city_building" && l.ParentCity != null).Select(l => l.ParentCity!).Distinct())
            {
                var key = "city:" + parent;
                if (Known(s, parent) && (CityBuildings(parent).Any(b => Known(s, b)) || here == key)) list.Add((key, MapLayerName(key)));
            }
            foreach (var d in Config.Map.Dungeons)
                if (DungeonRooms(d.Id).Any(r => Known(s, r.Id))) list.Add(("dungeon:" + d.Id, d.Name));
            return list;
        }

        /// <summary>某层的只读视图。层不合法或没有已知节点时退回主世界。</summary>
        public MapView BuildMapView(GameState s, string? layerKey = null)
        {
            var layer = layerKey ?? DefaultMapLayer(s);
            if (!KnownMapLayers(s).Any(x => x.Key == layer)) layer = WorldLayer;
            var here = s.CurrentLocation;
            var view = new MapView { LayerKey = layer, LayerName = MapLayerName(layer) };

            // 1) 本层节点集合与“代表”映射
            Func<string, string?> rep;
            var members = new List<string>();
            if (layer == WorldLayer)
            {
                foreach (var l in Config.Locations)
                {
                    if (!Known(s, l.Id) || l.NodeKind == "dungeon") continue;
                    if (l.NodeKind == "city_building" && WorldRep(s, l.Id) != l.Id) continue;
                    members.Add(l.Id);
                }
                rep = id => _locs[id].NodeKind == "dungeon" ? null : WorldRep(s, id);
            }
            else if (layer.StartsWith("city:"))
            {
                var parent = layer.Substring(5);
                if (Known(s, parent)) members.Add(parent);
                members.AddRange(CityBuildings(parent).Where(b => Known(s, b)));
                var set = new HashSet<string>(members);
                rep = id => set.Contains(id) ? id : null;
            }
            else
            {
                var d = layer.Substring(8);
                members.AddRange(DungeonRooms(d).Where(r => Known(s, r.Id)).Select(r => r.Id));
                var set = new HashSet<string>(members);
                rep = id => set.Contains(id) ? id : null;
            }
            var memberSet = new HashSet<string>(members);

            // 2) 节点
            var hereRep = rep(here);
            view.HereNode = hereRep != null && memberSet.Contains(hereRep) ? hereRep : null;
            var dungeonCfg = layer.StartsWith("dungeon:") ? Config.Map.Dungeons.First(x => x.Id == layer.Substring(8)) : null;
            foreach (var id in members)
            {
                var l = _locs[id];
                var ls = s.Locations[id];
                var n = new MapViewNode
                {
                    Id = id, Name = l.Name, X = l.MapX, Y = l.MapY, Visited = ls.Visited, Progress = ls.Progress,
                    IsHere = id == here, HereInside = id == view.HereNode && id != here, Danger = l.Danger,
                };
                if (layer == WorldLayer)
                {
                    if (CityBuildings(id).Any(b => Known(s, b))) { n.SubLayer = "city:" + id; n.SubLabel = "城内 ▸"; }
                    var dg = Config.Map.Dungeons.FirstOrDefault(x => x.Parent == id && DungeonRooms(x.Id).Any(r => Known(s, r.Id)));
                    if (dg != null && n.SubLayer == null) { n.SubLayer = "dungeon:" + dg.Id; n.SubLabel = "地牢 ▸"; }
                }
                if (dungeonCfg != null && ls.Visited)
                {
                    if (dungeonCfg.GuardNode == id) n.Roles.Add("守卫");
                    if (dungeonCfg.RecordNode == id) n.Roles.Add("核心记录");
                    if (dungeonCfg.ExitNode == id) n.Roles.Add("出口");
                }
                view.Nodes.Add(n);
            }

            // 3) 连线与跨层入口：两端都已知、且已开通或已揭示
            var pairs = new Dictionary<string, MapViewEdge>();
            foreach (var e in Config.Edges)
            {
                if (!Known(s, e.A) || !Known(s, e.B)) continue;
                var open = EdgeOpen(s, e);
                if (!open && !EdgeRevealed(s, e)) continue;
                var ra = rep(e.A);
                var rb = rep(e.B);
                var inA = ra != null && memberSet.Contains(ra);
                var inB = rb != null && memberSet.Contains(rb);
                if (inA && inB)
                {
                    if (ra == rb) continue;   // 折叠城市的内部路
                    var key = string.CompareOrdinal(ra, rb) < 0 ? ra + "|" + rb : rb + "|" + ra;
                    var me = new MapViewEdge
                    {
                        Id = e.Id, A = ra!, B = rb!, Open = open, Minutes = e.BaseMinutes,
                        OneWayFrom = e.Bidirectional ? null : ra,
                        Reason = open ? "" : GateBlockedReason(s, e),
                        Near = e.Touches(here),
                    };
                    if (!pairs.TryGetValue(key, out var old) || (me.Open && !old.Open) || (me.Open == old.Open && me.Minutes < old.Minutes))
                        pairs[key] = me;
                }
                else if (inA || inB)
                {
                    var from = inA ? e.A : e.B;
                    var to = e.Other(from);
                    var tl = MapLayerOf(to);
                    if (tl == WorldLayer && layer == WorldLayer) continue;
                    view.Portals.Add(new MapPortal
                    {
                        FromNode = inA ? ra! : rb!, TargetId = to, TargetName = LocName(to),
                        TargetLayer = tl.StartsWith("city:") && !Known(s, tl.Substring(5)) ? WorldLayer : tl,
                        TargetLayerName = MapLayerName(tl.StartsWith("city:") && !Known(s, tl.Substring(5)) ? WorldLayer : tl),
                        Minutes = e.BaseMinutes, Open = open,
                        OneWayOut = !e.Bidirectional && e.A == from,
                        Reason = open ? "" : GateBlockedReason(s, e),
                    });
                }
            }
            view.Edges = pairs.Values.ToList();
            // 主世界层不把地牢入口画成跨层线（节点上已有“地牢 ▸”）
            if (layer == WorldLayer) view.Portals.RemoveAll(p => p.TargetLayer.StartsWith("dungeon:"));

            // 4) 外框与面包屑（只按已知节点）
            if (view.Nodes.Count > 0)
            {
                view.MinX = view.Nodes.Min(n => n.X); view.MaxX = view.Nodes.Max(n => n.X);
                view.MinY = view.Nodes.Min(n => n.Y); view.MaxY = view.Nodes.Max(n => n.Y);
            }
            view.Breadcrumb.Add(MapLayerName(WorldLayer));
            if (layer != WorldLayer) view.Breadcrumb.Add(view.LayerName);
            if (view.HereNode != null) view.Breadcrumb.Add(LocName(here));
            return view;
        }

        /// <summary>
        /// 到远处已知地点的实际可达路径（A10-EXUI §1“远处节点显示实际可达路径与分段时间，只能确认下一段”）：只走已开通、可从这一端出发、两端已知的边，
        /// 按基础分钟最短（同价按配置顺序）；到不了为 null，目标就是当前位置为空表。只读。
        /// </summary>
        public List<MapPathStep>? MapPathTo(GameState s, string targetId)
        {
            var here = s.CurrentLocation;
            if (targetId == here) return new List<MapPathStep>();
            if (!Known(s, targetId)) return null;
            var dist = new Dictionary<string, int> { [here] = 0 };
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
                    if (!e.TraversableFrom(u) || !EdgeOpen(s, e)) continue;
                    var v = e.Other(u);
                    if (!Known(s, v) || done.Contains(v)) continue;
                    var nd = dist[u] + e.BaseMinutes;
                    if (!dist.TryGetValue(v, out var old) || nd < old) { dist[v] = nd; prev[v] = (u, e); }
                }
            }
            var steps = new List<MapPathStep>();
            for (var at = targetId; at != here; at = prev[at].From)
                steps.Add(new MapPathStep { From = prev[at].From, To = at, Minutes = prev[at].Edge.BaseMinutes });
            steps.Reverse();
            return steps;
        }
    }
}
