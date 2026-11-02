using Newtonsoft.Json.Linq;
using Survival.Core.Config;
using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 批次 B11（R33/R34）：v3 地图导入（A08「全143节点」「全180边」、map-v3-normalized.json，MAP30-01/02/06/07）与
/// 门槛求值器（S02「Q30B 完整门槛契约」、gate-contract.json，MP01—MP08、GT01—GT08 中本批能落地的部分）。
/// </summary>
public class B11MapTests : TestBase
{
    private static JObject Load(string name) =>
        JObject.Parse(File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, name)));

    private static readonly string[] Starter = { "camp", "field", "stream", "woods", "village", "hardware", "clinic", "scrapyard" };

    private void Travel(GameState s, string to)
    {
        Start(s, ActionRequest.Travel(to));
        Assert.That(Run(s), Is.EqualTo(SegmentStatus.Completed));
        Assert.That(s.CurrentLocation, Is.EqualTo(to));
    }

    private EdgeConfig Edge(string a, string b) =>
        Cfg.Edges.Single(e => (e.A == a && e.B == b) || (e.A == b && e.B == a));

    // ───────────── MAP30-01/02/06/07：数据导入 ─────────────

    /// <summary>MAP30-01：143 节点 180 边、引用都存在、原 8 边 ID/分钟不改；8 条单向出口；无重复地点对（无隐含捷径）。</summary>
    [Category("DesignSync")]
    [Test]
    public void MAP30_01_NodesAndEdgesImported_LegacyEightUnchanged()
    {
        Assert.That(Cfg.Locations.Count(l => l.NodeKind == "world"), Is.EqualTo(62));
        Assert.That(Cfg.Locations.Count(l => l.NodeKind == "city_building"), Is.EqualTo(14));
        Assert.That(Cfg.Locations.Count(l => l.NodeKind == "dungeon"), Is.EqualTo(67));
        Assert.That(Cfg.Edges.Count(e => e.Bidirectional), Is.EqualTo(172));
        Assert.That(Cfg.Edges.Count(e => !e.Bidirectional), Is.EqualTo(8));
        Assert.That(Cfg.Edges.Where(e => !e.Bidirectional).All(e => e.Gate == "instance_complete"), Is.True, "单向的都是地牢出口");
        var ids = Cfg.Locations.Select(l => l.Id).ToHashSet();
        Assert.That(Cfg.Edges.All(e => ids.Contains(e.A) && ids.Contains(e.B)), Is.True);
        Assert.That(ConfigLoader.Validate(Cfg), Is.Empty);

        var legacy = new (string id, string a, string b, int min, string gate)[]
        {
            ("camp__field", "camp", "field", 15, "open"),
            ("field__stream", "field", "stream", 20, "field25"),
            ("field__woods", "field", "woods", 20, "field50"),
            ("field__village", "field", "village", 25, "field100"),
            ("village__hardware", "village", "hardware", 15, "village50"),
            ("village__clinic", "village", "clinic", 20, "village100"),
            ("woods__scrapyard", "woods", "scrapyard", 30, "woods100_or_scrapyard100"),
            ("hardware__scrapyard", "hardware", "scrapyard", 20, "hardware100"),
        };
        foreach (var (id, a, b, min, gate) in legacy)
        {
            var e = E.Edge(id);
            Assert.That((e.A, e.B, e.BaseMinutes, e.Gate, e.Bidirectional), Is.EqualTo((a, b, min, gate, true)), id);
        }
        Assert.That(Cfg.Edges.Count(e => Starter.Contains(e.A) && Starter.Contains(e.B)), Is.EqualTo(8), "旧 8 地点之间仍只有原 8 条边");
        Assert.That(Cfg.Edges.Count(e => Starter.Contains(e.A) != Starter.Contains(e.B)), Is.EqualTo(1), "新手区唯一出口 village↔m01_road");
        Assert.That(Edge("village", "m01_road").BaseMinutes, Is.EqualTo(30));

        // 与策划数据逐条一致（分钟、方向、门槛别名归一）
        var map = Load("design.map-v3.json");
        foreach (var je in map["edges"]!)
        {
            var e = Edge((string)je["from"]!, (string)je["to"]!);
            Assert.That(e.A, Is.EqualTo((string)je["from"]!), "A 是数据原 from（门槛锚点）");
            Assert.That(e.BaseMinutes, Is.EqualTo((int)je["minutes"]!), e.Id);
            Assert.That(e.Bidirectional, Is.EqualTo((bool)je["bidirectional"]!), e.Id);
            Assert.That(e.Gate, Is.EqualTo((string)je["gateContractId"]!), e.Id);
            Assert.That(e.Role, Is.EqualTo((string)je["gateContext"]!["edgeRole"]!), e.Id);
        }
        // 门槛契约 26 个规范门槛全部导入，别名 from_explore_100 → from_explore100
        Assert.That(Cfg.Map.Gates.Select(g => g.Id), Is.EquivalentTo(Load("design.gate-contract.json")["definitions"]!.Select(d => (string)d["id"]!)));
        Assert.That(Cfg.Map.Gates, Has.Count.EqualTo(26));
        Assert.That(Cfg.Edges.Count(e => e.GateAlias == "from_explore_100" && e.Gate == "from_explore100"), Is.EqualTo(27));
        Assert.That(Cfg.Edges.Count(e => e.Gate == "from_explore100"), Is.EqualTo(64));
        Assert.That(Cfg.Edges.Count(e => e.Gate == "instance_from_explore100"), Is.EqualTo(61));
    }

    /// <summary>MAP30-02：八座地牢拓扑不同；禁守卫出门边后无法从入口走到核心（不经守卫门到不了 recordNode）。</summary>
    [Test]
    public void MAP30_02_EightDungeons_RecordUnreachableWithoutGuardEdge()
    {
        Assert.That(Cfg.Map.Dungeons, Has.Count.EqualTo(8));
        var shapes = new HashSet<string>();
        foreach (var d in Cfg.Map.Dungeons)
        {
            var rooms = Cfg.Locations.Where(l => l.DungeonId == d.Id).Select(l => l.Id).ToHashSet();
            var inner = Cfg.Edges.Where(e => rooms.Contains(e.A) && rooms.Contains(e.B)).ToList();
            shapes.Add(string.Join(",", inner.Select(e => e.A[4..] + "-" + e.B[4..]).OrderBy(x => x)) + "|" + rooms.Count);
            var guard = inner.Single(e => e.Gate == "guardian_defeated");
            Assert.That(guard.A, Is.EqualTo(d.GuardNode), d.Id);
            // 从入口房出发、不走守卫门的可达集合
            var start = Cfg.Edges.Single(e => e.A == d.Parent && rooms.Contains(e.B)).B;
            var seen = new HashSet<string> { start };
            var q = new Queue<string>(seen);
            while (q.Count > 0)
            {
                var x = q.Dequeue();
                foreach (var e in inner.Where(e => e != guard && e.TraversableFrom(x)))
                    if (seen.Add(e.Other(x))) q.Enqueue(e.Other(x));
            }
            Assert.That(seen, Does.Not.Contain(d.RecordNode), $"{d.Id} 不经守卫到不了核心");
            var exit = Cfg.Edges.Single(e => !e.Bidirectional && e.DungeonId == d.Id);
            Assert.That((exit.A, exit.B), Is.EqualTo((d.ExitNode, d.Parent)), $"{d.Id} 单向出口");
        }
        Assert.That(shapes, Has.Count.EqualTo(8), "八座拓扑不同");
    }

    /// <summary>MAP30-06/07：逐 143 节点核对 zone/类型/父城/worldKind/landOwner/永久安全屋/临时营地/危险/网格；旧 camp 的棚火与参数不变。</summary>
    [Category("DesignSync")]
    [Test]
    public void MAP30_06_07_NodeFieldsMatchData_CampKept()
    {
        var map = Load("design.map-v3.json");
        foreach (var jn in map["nodes"]!)
        {
            var l = E.Location((string)jn["id"]!);
            Assert.That(l.Name, Is.EqualTo((string)jn["name"]!), l.Id);
            Assert.That(l.Zone, Is.EqualTo((string)jn["zone"]!), l.Id);
            Assert.That(l.NodeKind, Is.EqualTo((string)jn["node_kind"]!), l.Id);
            Assert.That(l.ParentCity ?? "", Is.EqualTo((string)jn["parent_city"]!), l.Id);
            Assert.That(l.WorldKind, Is.EqualTo((string)jn["worldKind"]!), l.Id);
            Assert.That(l.LandOwner, Is.EqualTo((string)jn["landOwner"]!), l.Id);
            Assert.That(l.BuildAllowedSafehouse, Is.EqualTo((bool)jn["buildAllowed"]!["safehouse"]!), l.Id);
            Assert.That(l.TemporaryCamp, Is.EqualTo((bool?)jn["buildAllowed"]!["temporaryCamp"] ?? false), l.Id);
            Assert.That(l.Danger, Is.EqualTo((int?)jn["danger"]), l.Id);
            Assert.That((l.GridX, l.GridY), Is.EqualTo(((int)jn["x"]!, (int)jn["y"]!)), l.Id);
        }
        Assert.That(Cfg.Locations.Count(l => l.BuildAllowedSafehouse), Is.EqualTo(54), "A08 永久安全屋白名单 54 处");
        Assert.That(Cfg.Locations.Count(l => l.TemporaryCamp), Is.EqualTo(8));
        Assert.That(Cfg.Locations.Where(l => l.IsDungeon).All(l => !l.BuildAllowedSafehouse), Is.True, "地牢禁建");
        Assert.That(Cfg.Locations.Where(l => l.NodeKind == "city_building").All(l => !l.BuildAllowedSafehouse), Is.True, "城内建筑禁建");
        Assert.That(new[] { "c01_pilgrim", "w11_orchard", "w16_service" }.All(id => E.Location(id).BuildAllowedSafehouse), Is.True, "c01 与两条 approach 不被泛禁");
        // MAP30-07：camp 原棚/火与清理、掉落参数原样（legacyOverrides 为空）
        var camp = E.Location("camp");
        Assert.That((camp.HasFire, camp.CanBuildShelter, camp.LootTableId, camp.Danger), Is.EqualTo((true, true, "loose_camp", (int?)null)));
        Assert.That(Starter.All(id => E.Location(id).ExploreMinutes == null && E.Location(id).ExploreIncrement == null && E.Location(id).ClearEnabled), Is.True, "旧 8 地点探索/清理参数不变");
    }

    /// <summary>方位按 v3 网格重算（待确认 Q-E）；地牢房间与世界图层不同坐标系，跨图层不给方位。</summary>
    [Test]
    public void Direction_GridAndLayers()
    {
        var s = New();
        E.DebugTeleport(s, "w34_depot");
        s.Locations["d08_01"].Discovered = true;
        Assert.That(E.DirectionFromHere(s, "d08_01"), Is.EqualTo(MapDirection.None), "跨图层不编方位");
        E.DebugTeleport(s, "d08_01");
        s.Locations["d08_02"].Discovered = true;
        Assert.That(E.DirectionFromHere(s, "d08_02"), Is.EqualTo(MapDirection.Right), "地牢内按局部网格");
        E.DebugTeleport(s, "village");
        s.Locations["m01_road"].Discovered = true;
        Assert.That(E.DirectionFromHere(s, "m01_road"), Is.EqualTo(MapDirection.DownRight));
    }

    // ───────────── 旧主路线保护 ─────────────

    /// <summary>七日通关等旧主路线：旧 8 地点之间的连通与原探索阈值一致，任何新门槛都不锁它们（GT01）。</summary>
    [Test]
    public void LegacyEight_ConnectivityUnchanged()
    {
        var s = New();
        Assert.That(E.AdjacentUnlocked(s).Select(e => e.Id), Is.EquivalentTo(new[] { "camp__field" }));
        Travel(s, "field");
        E.DebugSetProgress(s, "field", 25);
        Assert.That(E.AdjacentUnlocked(s).Select(e => e.Id), Is.EquivalentTo(new[] { "camp__field", "field__stream" }));
        E.DebugSetProgress(s, "field", 50);
        Assert.That(s.UnlockedEdges, Does.Contain("field__woods"));
        E.DebugSetProgress(s, "field", 100);
        Travel(s, "village");
        Assert.That(E.AdjacentUnlocked(s).Select(e => e.Other("village")), Is.EquivalentTo(new[] { "field" }), "村庄 0% 只能回荒地");
        E.DebugSetProgress(s, "village", 50);
        Assert.That(s.UnlockedEdges, Does.Contain("village__hardware"));
        Assert.That(s.UnlockedEdges, Does.Not.Contain("village__clinic"));
        E.DebugSetProgress(s, "village", 100);
        Assert.That(s.UnlockedEdges, Does.Contain("village__clinic"));
        Travel(s, "hardware");
        E.DebugSetProgress(s, "hardware", 100);
        Assert.That(s.UnlockedEdges, Does.Contain("hardware__scrapyard"));
        Travel(s, "scrapyard");
        E.DebugSetProgress(s, "scrapyard", 100);
        Assert.That(s.UnlockedEdges, Does.Contain("woods__scrapyard"), "woods 或 scrapyard 任一 100");
        // 走遍旧 8 地点即“走遍新手区”，不要求新地图
        foreach (var id in new[] { "clinic", "stream", "woods" }) E.DebugTeleport(s, id);
        Assert.That(s.Progress.Flags, Does.Contain("allVisited"));
    }

    // ───────────── 门槛求值器（GT01—GT08、MP01—MP08） ─────────────

    /// <summary>MP02/MP03/MP05/QMAP01：(首战胜利或旧档等效) AND 来客路线 AND 实际到访村庄；两种先后顺序都立即开一次；不要村庄 100。</summary>
    [Test]
    public void QMAP01_BothOrders_NoVillage100_TutorialRequired()
    {
        // 顺序一：先到村庄（0%），后得路线
        var s = New();
        E.DebugTeleport(s, "village");
        Assert.That(s.UnlockedEdges, Does.Not.Contain("village__m01_road"));
        Assert.That(E.GateBlockedReason(s, Edge("village", "m01_road")), Does.Contain("来客程望"));
        E.DebugMapFact(s, "camp_route_known");
        Assert.That(s.UnlockedEdges, Does.Contain("village__m01_road"), "先到访后得路线立即开");
        Assert.That(s.Locations["village"].Progress, Is.EqualTo(0), "不授村庄 100");
        Assert.That(s.UnlockedEdges, Does.Not.Contain("village__clinic"), "诊所仍要村庄 100");

        // 顺序二：先得路线，后实际到达村庄
        var t = New();
        E.DebugMapFact(t, "camp_route_known");
        Assert.That(t.UnlockedEdges, Does.Not.Contain("village__m01_road"));
        E.DebugUnlockEdge(t, "field__village");
        E.DebugTeleport(t, "field");
        Travel(t, "village");
        Assert.That(t.UnlockedEdges, Does.Contain("village__m01_road"), "到达成功时重判");
        Travel(t, "m01_road");
        Assert.That(E.Edge("village__m01_road").BaseMinutes, Is.EqualTo(30));
        Travel(t, "village");   // 回程不查 m01 探索

        // MP05/MP01：完整新局首战未胜利，路线与到访齐也锁
        var n = NewStory();
        n.Story.TutorialTriggered = true;
        E.DebugTeleport(n, "village");
        E.DebugMapFact(n, "camp_route_known");
        Assert.That(n.UnlockedEdges, Does.Not.Contain("village__m01_road"));
        Assert.That(E.GateBlockedReason(n, Edge("village", "m01_road")), Does.Contain("第一场战斗"));
        n.Story.TutorialWon = true;
        E.RefreshGates(n);
        Assert.That(n.UnlockedEdges, Does.Contain("village__m01_road"));
    }

    /// <summary>GT02：from_explore100 只查数据原 from；首次满足永久双向锁存；只把 to 探索 100 不开（原锚）。</summary>
    [Test]
    public void GT02_FromExplore100_AnchorIsAuthoredFrom_LatchBothWays()
    {
        var s = New();
        var e = Edge("m01_road", "m02_checkpoint");
        Assert.That(e.Gate, Is.EqualTo("from_explore100"));
        E.DebugTeleport(s, "m02_checkpoint");
        E.DebugSetProgress(s, "m02_checkpoint", 100);
        Assert.That(E.EdgeOpen(s, e), Is.False, "只把 to 端探索 100 不开");
        var locked = E.AdjacentLocked(s).Single(x => x.Target == "m01_road");
        Assert.That(locked.Reason, Does.Contain("旧公路探索到100%"));
        Assert.That(E.Start(s, ActionRequest.Travel("m01_road")).Reason, Does.StartWith("路线未开通"));
        E.DebugTeleport(s, "m01_road");
        E.DebugSetProgress(s, "m01_road", 100);
        Assert.That(E.EdgeOpen(s, e), Is.True);
        s.Locations["m01_road"].Progress = 75;   // 锁存后不倒锁
        Assert.That(E.EdgeOpen(s, e), Is.True);

        // 全部 64 条 from_explore100：只看原 from 的探索度
        foreach (var x in Cfg.Edges.Where(x => x.Gate == "from_explore100"))
        {
            var t = New();
            t.Locations[x.B].Progress = 100;
            Assert.That(E.EvaluateGate(t, x), Is.False, x.Id);
            t.Locations[x.A].Progress = 100;
            Assert.That(E.EvaluateGate(t, x), Is.True, x.Id);
        }
    }

    /// <summary>GT02 实例：instance_from_explore100 查当前实例里原 from 房间；锁在实例上，换代（实例丢弃）不继承。</summary>
    [Test]
    public void GT02_InstanceExplore_LatchedOnInstance_NotInherited()
    {
        var s = New();
        E.DebugTeleport(s, "d05_01");
        Assert.That(s.DungeonInstances, Does.ContainKey("d05"));
        var e = Edge("d05_01", "d05_02");
        Assert.That(e.Gate, Is.EqualTo("instance_from_explore100"));
        E.DebugSetProgress(s, "d05_01", 100);
        Assert.That(E.EdgeOpen(s, e), Is.True);
        Assert.That(s.DungeonInstances["d05"].UnlockedEdges, Does.Contain(e.Id));
        Assert.That(s.UnlockedEdges, Does.Not.Contain(e.Id), "不是世界永久锁存");
        s.DungeonInstances.Remove("d05");   // 模拟换代（B22）
        Assert.That(E.EdgeOpen(s, e), Is.False);
    }

    /// <summary>GT03：h01↔h02 只需登记授权；其余 9 条 register 边必须真正 hub_registered。</summary>
    [Test]
    public void GT03_Register_BoundaryVsOthers()
    {
        var s = New();
        var boundary = Edge("h01_gate", "h02_square");
        var others = Cfg.Edges.Where(e => e.Gate == "register" && e != boundary).ToList();
        Assert.That(others, Has.Count.EqualTo(9));
        Assert.That(E.EvaluateGate(s, boundary), Is.False);
        Assert.That(E.GateBlockedReason(s, boundary), Does.Contain("登记"));
        s.Progress.Flags.Add("registration_authorized");
        Assert.That(E.EvaluateGate(s, boundary), Is.True);
        Assert.That(others.Any(e => E.EvaluateGate(s, e)), Is.False);
        s.Progress.Flags.Add("hub_registered");
        Assert.That(others.All(e => E.EvaluateGate(s, e)), Is.True);
    }

    /// <summary>GT04/GT05/GT08：c02/r02 真实到达写接触事实并开公共城内边；任务 accept 是单一事实；城市到访自动 100、不能探索。</summary>
    [Test]
    public void GT04_GT05_CityContactOnArrival_TaskGatesSingleFact()
    {
        var s = New();
        Assert.That(E.EvaluateGate(s, Edge("c02_church", "c04_alms")), Is.False);
        E.DebugTeleport(s, "c02_church");
        Assert.That(s.Progress.Flags, Does.Contain("church_contacted"));
        Assert.That(s.Locations["c02_church"].Progress, Is.EqualTo(100), "公共城市首次到访自动 100");
        Assert.That(E.Start(s, ActionRequest.Explore()).Reason, Does.Contain("不需要探索"));
        Assert.That(s.UnlockedEdges, Is.SupersetOf(new[] { "c02_church__c04_alms", "c02_church__c05_admonition", "c07_sanctuary__c02_church" }));
        // Q115 选 B（B13a）：取代 B11-3，c06/c07 都没到过也立即开通并记为已发现，但不算到访
        Assert.That(s.UnlockedEdges, Does.Contain("c06_archive__c07_sanctuary"), "条件满足即开通，不要求任一端到访");
        Assert.That((s.Locations["c06_archive"].Discovered, s.Locations["c06_archive"].Visited), Is.EqualTo((true, false)));
        E.DebugTeleport(s, "c05_admonition");
        var fa02 = Edge("c05_admonition", "c06_archive");
        Assert.That(E.EdgeOpen(s, fa02), Is.False);
        Assert.That(E.AdjacentLocked(s).Single(x => x.Target == "c06_archive").Reason, Does.Contain("公开路线"));
        E.DebugMapFact(s, "FA02_route_briefed");
        Assert.That(E.EdgeOpen(s, fa02), Is.True);
        Assert.That(E.EvaluateGate(s, Edge("c03_crypt", "d01_01")), Is.True, "d01 入口复用同一个 FA02 路线事实");

        var a = New();
        E.DebugTeleport(a, "r02_academy");
        Assert.That(a.Progress.Flags, Does.Contain("academy_contacted"));
        E.DebugTeleport(a, "r04_reception");
        Assert.That(E.EdgeOpen(a, Edge("r04_reception", "r05_testing")), Is.True, "r05 入口只查接触，不前置室内同意");
        Assert.That(E.EdgeOpen(a, Edge("r06_archive", "r07_workshop")), Is.False);
        E.DebugMapFact(a, "FA03_environment_briefed");
        Assert.That(E.EvaluateGate(a, Edge("r06_archive", "r07_workshop")), Is.True);
        Assert.That(E.EvaluateGate(a, Edge("r03_lab", "d02_01")), Is.True, "d02 入口复用 r04 环境路线事实");
        Assert.That(E.EvaluateGate(a, Edge("r07_workshop", "r08_observation")), Is.False);
        E.DebugMapFact(a, "academy_observation_pass");
        Assert.That(E.EvaluateGate(a, Edge("r07_workshop", "r08_observation")), Is.True);
    }

    /// <summary>GT06：守卫门看当前实例 guardianDefeated（别的地牢不算）；单向出口每次重验当前实例 recordClaimedAndClear、无反向。</summary>
    [Test]
    public void GT06_GuardAndOneWayExit_CurrentInstanceOnly()
    {
        var s = New();
        var guard = Edge("d01_07", "d01_08");
        var exit = Edge("d01_08", "c03_crypt");
        Assert.That(E.EvaluateGate(s, guard), Is.False);
        E.DebugInstanceFact(s, "d02", "guardianDefeated");
        Assert.That(E.EvaluateGate(s, guard), Is.False, "别的地牢守卫不算");
        E.DebugTeleport(s, "d01_07");
        E.DebugInstanceFact(s, "d01", "guardianDefeated");
        Assert.That(E.EdgeOpen(s, guard), Is.True);
        Assert.That(E.EdgeOpen(s, exit), Is.False, "只杀守卫不算通关");
        E.DebugInstanceFact(s, "d01", "recordClaimedAndClear");
        Assert.That(E.EdgeOpen(s, exit), Is.True);
        Assert.That(s.UnlockedEdges, Does.Not.Contain(exit.Id), "出口不锁存");
        E.DebugTeleport(s, "c03_crypt");
        Assert.That(E.AdjacentUnlocked(s).Any(e => e == exit), Is.False, "单向出口无反向");
        Assert.That(E.EdgeBetween("c03_crypt", "d01_08"), Is.Null);
        s.DungeonInstances.Remove("d01");   // 换代
        Assert.That(E.EdgeOpen(s, exit), Is.False);
        Assert.That(E.EdgeOpen(s, guard), Is.False);
    }

    /// <summary>GT07/GT08：父地点探索 100 才入 d05—d08；FA04 要三项、FA05 读电源领取账；检查哨只认固定战事实。</summary>
    [Test]
    public void GT07_GT08_ParentExplore_FA04_FA05_Checkpoint()
    {
        var s = New();
        var entry = Edge("w33_waterworks", "d05_01");
        E.DebugTeleport(s, "w33_waterworks");
        E.DebugSetProgress(s, "w33_waterworks", 75);
        Assert.That(E.EdgeOpen(s, entry), Is.False);
        E.DebugSetProgress(s, "w33_waterworks", 100);
        Assert.That(E.EdgeOpen(s, entry), Is.True);

        var fa04 = Edge("o03_police", "d03_01");
        s.Progress.Flags.Add("FA02_record_credit");
        s.Progress.Flags.Add("FA03_record_credit");
        Assert.That(E.EvaluateGate(s, fa04), Is.False, "两种记录齐但未合并不通");
        s.Progress.Flags.Add("FA04_merged");
        Assert.That(E.EvaluateGate(s, fa04), Is.True);
        Assert.That(E.EvaluateGate(s, Edge("w10_watchtower", "d04_01")), Is.False);
        s.Progress.Flags.Add("communication_power_claimed");
        Assert.That(E.EvaluateGate(s, Edge("w10_watchtower", "d04_01")), Is.True, "读领取账，不看背包");

        var cp = Edge("m02_checkpoint", "h01_gate");
        E.DebugTeleport(s, "m02_checkpoint");
        E.DebugSetProgress(s, "m02_checkpoint", 100);
        Assert.That(E.EdgeOpen(s, cp), Is.False);
        Assert.That(E.AdjacentLocked(s).Single(x => x.Target == "h01_gate").Reason, Does.Contain("检查哨"));
        Assert.That(s.Locations["h01_gate"].Discovered, Is.True, "100% 揭示出边，远端只显示名称");
        E.DebugMapFact(s, "checkpoint_obstruction_cleared");
        Assert.That(E.EdgeOpen(s, cp), Is.True);
    }

    /// <summary>MP04：field 未 100 去 village、m01 未 100 去 m02 都被原门槛挡住，没有直达/传送。</summary>
    [Test]
    public void MP04_ExploreGatesBlock_NoTeleport()
    {
        var s = New();
        E.DebugTeleport(s, "field");
        Assert.That(E.Start(s, ActionRequest.Travel("village")).Reason, Is.EqualTo("尚未发现路线"));
        E.DebugTeleport(s, "m01_road");
        E.DebugSetProgress(s, "m01_road", 75);
        Assert.That(E.Start(s, ActionRequest.Travel("m02_checkpoint")).Ok, Is.False);
        Assert.That(E.Start(s, ActionRequest.Travel("h02_square")).Ok, Is.False, "非相邻不能直达");
    }

    /// <summary>门槛表达式：未知运算/未绑定变量报错，不默认放行。</summary>
    [Test]
    public void Gate_UnknownOpOrUnboundVariable_Throws()
    {
        var j = JObject.Parse(File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "config.v0.6.json")));
        var gates = (JArray)j["map"]!["gates"]!;
        ((JObject)gates.First(g => (string)g["id"]! == "field25")!)["predicate"] = JObject.Parse("{\"op\":\"teleport\"}");
        Assert.That(ConfigLoader.Validate(ConfigLoader.FromJson(j.ToString(), validate: false)), Has.Some.Contains("未知运算"));
        var e = new GameEngine(ConfigLoader.FromJson(j.ToString(), validate: false));
        Assert.Throws<InvalidOperationException>(() => e.NewGame(1), "新局重判全部边（Q115，不再只看到访过的一端）时就报错，不默认放行");
        var s = E.NewGame(1);
        Assert.Throws<InvalidOperationException>(() => e.EvaluateGate(s, e.Edge("field__stream")));
        var parentless = Cfg.Edges.First(x => x.Gate == "from_explore100");
        var cfg2 = JObject.Parse(File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "config.v0.6.json")));
        ((JObject)((JArray)cfg2["map"]!["gates"]!).First(g => (string)g["id"]! == "from_explore100")!)["predicate"] = JObject.FromObject(new { op = "exploreAtLeast", node = "$dungeonParent", value = 100, scope = "campaign" });
        var e2 = new GameEngine(ConfigLoader.FromJson(cfg2.ToString(), validate: false));
        Assert.Throws<InvalidOperationException>(() => e2.EvaluateGate(e2.NewGame(1), e2.Edge(parentless.Id)), "普通边没有 dungeonParent，不默认放行");
    }

    // ───────────── 存档 19 → 20 ─────────────

    /// <summary>R33/MP07：旧档（只有 8 地点）能读，补 135 个地点状态；已开的边不回退；旧档村庄已 100 时揭示通往旧公路的未开通路线；不补奖励。</summary>
    [Test]
    public void Migration_19To20_AddsLocations_KeepsEdges()
    {
        var s = New();
        E.DebugSetProgress(s, "field", 100);
        E.DebugTeleport(s, "village");
        E.DebugSetProgress(s, "village", 100);
        var edges = s.UnlockedEdges.ToList();
        var j = JObject.Parse(SaveSerializer.Serialize(s));
        j["SchemaVersion"] = "19";
        var locs = (JObject)j["Locations"]!;
        foreach (var p in locs.Properties().Where(p => !Starter.Contains(p.Name)).ToList()) p.Remove();
        j.Remove("DungeonInstances");
        j.Remove("RevealedResourceMarkers");
        var items = ((JArray)j["Items"]!).Count;
        var m = SaveSerializer.Deserialize(j.ToString(), Cfg, out var notes);
        Assert.That(m.SchemaVersion, Is.EqualTo(GameState.CurrentSchema), "19→20→21");
        Assert.That(m.Locations, Has.Count.EqualTo(143));
        Assert.That(m.UnlockedEdges, Is.SupersetOf(edges), "已开边不回退");
        Assert.That(m.Locations["m01_road"].Discovered, Is.True, "村庄 100 揭示出区路线（未开通）");
        Assert.That(m.Locations["m01_road"].Visited, Is.False);
        Assert.That(m.UnlockedEdges, Does.Not.Contain("village__m01_road"), "没有来客路线事实，不伪造");
        Assert.That(m.Locations["h02_square"].Discovered, Is.False);
        Assert.That(((JArray)JObject.Parse(SaveSerializer.Serialize(m))["Items"]!).Count, Is.EqualTo(items), "不补物品");
        Assert.That(notes, Has.Some.Contains("143"));
        // 再读一次不重复改动
        var again = SaveSerializer.Deserialize(SaveSerializer.Serialize(m), Cfg, out var notes2);
        Assert.That(notes2, Is.Empty);
        Assert.That(SaveSerializer.Serialize(again), Is.EqualTo(SaveSerializer.Serialize(m)));
    }
}
