using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 批次 B12（R38）：分层地图视图模型（A10「A10-EXUI」§1、§9 EXUI01—03；S14「地图、来源与四类账」；A08 MAP30-04）
/// 与 B11 地图遗留（同因未开通路线日志合并、开发入口地图事实只能记下）。
/// </summary>
public class B12MapViewTests : TestBase
{
    /// <summary>EXUI01：只画已发现地点与已开通/已揭示路线；构建视图不改存档（序列化前后相同）。</summary>
    [Test]
    public void EXUI01_ReadOnly_NoLeak()
    {
        var s = New();
        var before = SaveSerializer.Serialize(s);
        var v = E.BuildMapView(s);
        Assert.That(SaveSerializer.Serialize(s), Is.EqualTo(before), "浏览地图不改存档");
        Assert.That(v.LayerKey, Is.EqualTo(GameEngine.WorldLayer));
        Assert.That(v.Nodes.Select(n => n.Id), Is.EquivalentTo(s.Locations.Where(kv => kv.Value.Discovered).Select(kv => kv.Key)));
        Assert.That(v.Nodes.All(n => s.Locations[n.Id].Discovered), Is.True);
        Assert.That(v.Edges.All(e => s.Locations[e.A].Discovered && s.Locations[e.B].Discovered), Is.True);
        Assert.That(E.KnownMapLayers(s).Select(x => x.Key), Is.EqualTo(new[] { GameEngine.WorldLayer }), "没有已知城内/地牢就没有这些层");
        Assert.That(v.Breadcrumb, Is.EqualTo(new[] { "主世界", "初醒荒野", E.LocName("camp") }), "B13c（Q124）：中间一级是分区名");
        // 请求一个不认识的层退回主世界，不泄露
        Assert.That(E.BuildMapView(s, "dungeon:d01").LayerKey, Is.EqualTo(GameEngine.WorldLayer));
    }

    /// <summary>MAP30-04 与默认层：h05_homestead 在主世界层、h05_appraisal 在营区城内层；城内建筑默认城内层、站在父城节点默认城内层（H7）、地牢默认该地牢层。</summary>
    [Test]
    public void DefaultLayer_CityAndDungeon_H05Split()
    {
        var s = New();
        E.DebugMapTeleport(s, "h03_workshop");
        Assert.That(s.Locations["h02_square"].Discovered, Is.True, "公开地点 100 揭出边，父城已知");
        Assert.That(E.DefaultMapLayer(s), Is.EqualTo("city:h02_square"));
        var city = E.BuildMapView(s);
        Assert.That(city.Breadcrumb, Is.EqualTo(new[] { "主世界", "幸存者营区", E.LocName("h03_workshop") }), "B13c（Q124）：城内写分区，不写重复的“营区广场城内”");
        Assert.That(city.Nodes.Select(n => n.Id), Does.Contain("h02_square").And.Contain("h03_workshop"));
        Assert.That(E.MapLayerOf("h05_appraisal"), Is.EqualTo("city:h02_square"));
        Assert.That(E.MapLayerOf("h05_homestead"), Is.EqualTo(GameEngine.WorldLayer), "MAP30-04：安全屋地块不与鉴定所混同");

        var world = E.BuildMapView(s, GameEngine.WorldLayer);
        Assert.That(world.Nodes.Select(n => n.Id), Does.Not.Contain("h03_workshop"), "城内建筑在主世界层折叠进父城");
        var hub = world.Nodes.Single(n => n.Id == "h02_square");
        Assert.That(hub.SubLayer, Is.EqualTo("city:h02_square"));
        Assert.That(hub.HereInside, Is.True, "当前位置在城内");
        Assert.That(world.HereNode, Is.EqualTo("h02_square"));

        E.DebugMapTeleport(s, "h02_square");
        Assert.That(E.DefaultMapLayer(s), Is.EqualTo("city:h02_square"), "站在父城节点默认城内层");

        E.DebugMapTeleport(s, "d05_01");
        Assert.That(E.DefaultMapLayer(s), Is.EqualTo("dungeon:d05"));
        Assert.That(E.KnownMapLayers(s).Select(x => x.Key), Does.Contain("dungeon:d05").And.Contain("city:h02_square"));
    }

    /// <summary>城内建筑父城未发现时按自身画在主世界层，不泄露父城名。</summary>
    [Test]
    public void CityBuilding_ParentUnknown_DrawnAsItself()
    {
        var s = New();
        s.Locations["h01_gate"].Discovered = true;
        Assert.That(s.Locations["h02_square"].Discovered, Is.False);
        var v = E.BuildMapView(s);
        Assert.That(v.Nodes.Select(n => n.Id), Does.Contain("h01_gate").And.Not.Contain("h02_square"));
        Assert.That(E.KnownMapLayers(s).Select(x => x.Key), Does.Not.Contain("city:h02_square"));
    }

    /// <summary>EXUI03：8 座地牢全部已知时，地牢层连线与 v3 数据一致；入口为双向跨层入口、出口为单向跨层出口，主世界层不画进地牢的线，出口不能反入。</summary>
    [Test]
    public void EXUI03_DungeonLayers_MatchV3_OneWayExit()
    {
        var s = New();
        foreach (var d in Cfg.Map.Dungeons)
        {
            foreach (var r in E.DungeonRooms(d.Id))
            {
                var ls = s.Locations[r.Id];
                ls.Discovered = true; ls.Visited = true; ls.Progress = 100;
            }
            s.Locations[d.Parent].Discovered = true; s.Locations[d.Parent].Visited = true; s.Locations[d.Parent].Progress = 100;
        }
        foreach (var d in Cfg.Map.Dungeons)
        {
            var v = E.BuildMapView(s, "dungeon:" + d.Id);
            var inner = Cfg.Edges.Where(e => E.Location(e.A).DungeonId == d.Id && E.Location(e.B).DungeonId == d.Id).Select(e => e.Id);
            Assert.That(v.Edges.Select(e => e.Id), Is.EquivalentTo(inner), d.Id);
            Assert.That(v.Nodes, Has.Count.EqualTo(E.DungeonRooms(d.Id).Count()), d.Id);
            var exit = v.Portals.Single(p => p.FromNode == d.ExitNode && p.TargetId == d.Parent);
            Assert.That(exit.OneWayOut, Is.True, d.Id);
            Assert.That(v.Portals.Any(p => p.TargetId == d.Parent && !p.OneWayOut), Is.True, $"{d.Id} 入口");
            Assert.That(v.Nodes.Single(n => n.Id == d.GuardNode).Roles, Does.Contain("守卫"));
            Assert.That(v.Nodes.Single(n => n.Id == d.RecordNode).Roles, Does.Contain("核心记录"));
            Assert.That(E.EdgeBetween(d.Parent, d.ExitNode), Is.Null, $"{d.Id} 出口不能反入");
        }
        var world = E.BuildMapView(s, GameEngine.WorldLayer);
        Assert.That(world.Portals.Any(p => p.TargetLayer.StartsWith("dungeon:")), Is.False);
        Assert.That(world.Nodes.Single(n => n.Id == "w33_waterworks").SubLayer, Is.EqualTo("dungeon:d05"));
    }

    /// <summary>地牢角色标记只给已到访房间（H7）。</summary>
    [Test]
    public void DungeonRoles_OnlyVisitedRooms()
    {
        var s = New();
        E.DebugMapTeleport(s, "d05_01");
        s.Locations["d05_06"].Discovered = true;
        var v = E.BuildMapView(s);
        Assert.That(v.Nodes.Single(n => n.Id == "d05_06").Roles, Is.Empty);
    }

    /// <summary>远处节点的路径预览：只走已开通边，按基础分钟最短；到不了为 null；不改存档。</summary>
    [Test]
    public void PathPreview_ShortestOpenPath()
    {
        var s = New();
        E.DebugTeleport(s, "field");
        E.DebugSetProgress(s, "field", 100);
        E.DebugTeleport(s, "camp");
        var before = SaveSerializer.Serialize(s);
        var path = E.MapPathTo(s, "village")!;
        Assert.That(path.Select(p => (p.From, p.To)), Is.EqualTo(new[] { ("camp", "field"), ("field", "village") }));
        Assert.That(path.Sum(p => p.Minutes), Is.EqualTo(Cfg.Edges.First(e => e.Id == "camp__field").BaseMinutes + Cfg.Edges.First(e => e.Id == "field__village").BaseMinutes));
        Assert.That(E.MapPathTo(s, "h02_square"), Is.Null, "未知/不可达");
        Assert.That(E.MapPathTo(s, "camp"), Is.Empty);
        Assert.That(SaveSerializer.Serialize(s), Is.EqualTo(before));
    }

    /// <summary>B11 遗留：同一原因的未开通路线合成一句，原因只写一次（营区广场 10 条 register 边）。单条格式不变。</summary>
    [Test]
    public void RouteNotes_GroupedByReason()
    {
        var s = New();
        var text = E.DebugMapTeleport(s, "h02_square");
        var count = text.Split("未开通").Length - 1;
        Assert.That(count, Is.EqualTo(2), "营门（登记授权或已登记）与其余 9 条（已登记）两种原因各写一次：" + text);
        Assert.That(text, Does.Contain($"通往{E.LocName("h03_workshop")}、{E.LocName("h04_infirmary")}、"));
    }

    /// <summary>B11 遗留：开发入口地图事实只能记下，清除被拒（campaign 锁存，已开路线不回退）；重复记下不再改动。</summary>
    [Test]
    public void DebugMapFact_RecordOnly()
    {
        var s = New();
        Assert.That(E.DebugMapFact(s, "camp_route_known", false), Does.Contain("不能清除"));
        Assert.That(s.Progress.Flags, Does.Not.Contain("camp_route_known"));
        E.DebugMapFact(s, "camp_route_known");
        Assert.That(E.DebugMapFact(s, "camp_route_known"), Does.Contain("已记下"));
        Assert.That(E.DebugMapFact(s, "camp_route_known", false), Does.Contain("不能清除"));
        Assert.That(s.Progress.Flags, Does.Contain("camp_route_known"));
    }
}
