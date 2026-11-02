using Newtonsoft.Json.Linq;
using Survival.Core.Config;
using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 批次 B13a（R124—R128；本文件先含 R124—R126）：策划提交 7154269 答复待确认 Q114—Q119。出处 S02/S03/S09/S13/S14/R08/A08/A11 本轮章、
/// assets/20261102/map/map-adjudications.json、gate-contract.json stateRules、party-adjudications.json serviceLocations.v3Activation；
/// 验收 A10「Q114—Q119 可判定验收」MAP-Q114-01、MAP-Q115-01/02、MAP-Q116-01、MAP-Q117-01、MAP-Q118-01/02、HOUSE-Q118-01/02、PARTY-Q119-01—03。
/// </summary>
public class B13aQTests : TestBase
{
    private static JObject Adj() =>
        JObject.Parse(File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "design.map-adjudications.json")));

    private static readonly string[] Starter = { "camp", "field", "stream", "woods", "village", "hardware", "clinic", "scrapyard" };

    private string LastDone(GameState s) => s.Log.Last(l => l.Kind == GameEventKind.ActionCompleted.ToString()).Text;

    private string Explore(GameState s)
    {
        Start(s, ActionRequest.Explore());
        Assert.That(Run(s), Is.EqualTo(SegmentStatus.Completed));
        return LastDone(s);
    }

    private GameState Reload(GameState s) => SaveSerializer.Deserialize(SaveSerializer.Serialize(s), Cfg, out _);

    // ───────────── R124 / Q114 ─────────────

    /// <summary>MAP-Q114-01：村庄↔诊所 ↙/↗、五金↔废料场 →/←（与 map-adjudications 同源），其余 6 组不变；跨图层（父地点↔地牢房间）不画方位。</summary>
    [Test]
    public void MAP_Q114_01_LegacyDirectionsFromGrid_CrossLayerNone()
    {
        foreach (var p in (JArray)Adj()["direction"]!["changedLegacyPairs"]!)
        {
            var a = E.Location((string)p["from"]!);
            var b = E.Location((string)p["to"]!);
            Assert.That(GameEngine.DirectionArrow(GameEngine.DirectionBetween(a, b)), Is.EqualTo((string)p["forward"]!), a.Id);
            Assert.That(GameEngine.DirectionArrow(GameEngine.DirectionBetween(b, a)), Is.EqualTo((string)p["reverse"]!), b.Id);
        }
        var six = new (string a, string b, string ab)[]
        {
            ("camp", "field", "→"), ("field", "stream", "↙"), ("field", "woods", "→"),
            ("field", "village", "↓"), ("village", "hardware", "→"), ("woods", "scrapyard", "↘"),
        };
        foreach (var (a, b, ab) in six)
            Assert.That(GameEngine.DirectionArrow(GameEngine.DirectionBetween(E.Location(a), E.Location(b))), Is.EqualTo(ab), $"{a}→{b}");
        // 坐标同一换算：mapX = 130 + 280·x、mapY = 100 + 200·y
        foreach (var l in Cfg.Locations.Where(l => !l.IsDungeon))
            Assert.That((l.MapX, l.MapY), Is.EqualTo((130 + 280.0 * l.GridX, 100 + 200.0 * l.GridY)), l.Id);

        var s = New();
        E.DebugTeleport(s, "w33_waterworks");
        E.DebugSetProgress(s, "w33_waterworks", 100);
        Assert.That(s.Locations["d05_01"].Discovered, Is.True);
        Assert.That(E.DirectionFromHere(s, "d05_01"), Is.EqualTo(MapDirection.None), "跨图层不画方位");
    }

    // ───────────── R125 / Q115 ─────────────

    /// <summary>
    /// MAP-Q115-01：c06/c07 都没到访时提交 church_contacted（到达 c02），c06↔c07 立即锁存并揭示两端；两端 visited/探索度仍原值、不自动 100、不发奖。
    /// 远方地牢入口（FA02 路线事实开 c03→d01_01）只发现，不凭发现创建地牢实例。
    /// </summary>
    [Test]
    public void MAP_Q115_01_FactOpensRemoteEdge_DiscoverOnly()
    {
        var s = New();
        var items = s.Items.Count;
        E.DebugMapTeleport(s, "c02_church");
        Assert.That(s.UnlockedEdges, Does.Contain("c06_archive__c07_sanctuary"), "不要求任一端已到访");
        foreach (var id in new[] { "c06_archive", "c07_sanctuary" })
        {
            var ls = s.Locations[id];
            Assert.That((ls.Discovered, ls.Visited, ls.Progress), Is.EqualTo((true, false, 0)), id);
            Assert.That(ls.TriggeredMilestones, Is.Empty, id);
        }
        Assert.That(s.Items.Count, Is.EqualTo(items), "不发物资");
        Assert.That(E.EdgeOpen(s, E.Edge("c06_archive__c07_sanctuary")), Is.True);
        Assert.That(E.MapPathTo(s, "c06_archive"), Is.Not.Null.And.Not.Empty, "远方已知边照实算连续路线（要真实走过去）");
        Assert.That(s.CurrentLocation, Is.EqualTo("c02_church"), "不传送");

        E.DebugMapFact(s, "FA02_route_briefed");
        var crypt = Cfg.Edges.Single(e => e.Touches("c03_crypt") && e.Other("c03_crypt") == "d01_01");
        Assert.That(E.EdgeOpen(s, crypt), Is.True);
        Assert.That(s.Locations["d01_01"].Discovered, Is.True);
        Assert.That(s.Locations["d01_01"].Visited, Is.False);
        Assert.That(s.DungeonInstances.ContainsKey("d01"), Is.False, "远方地牢不凭发现创建实例");
        Assert.That(s.DungeonInstances.Values.SelectMany(i => i.UnlockedEdges).Any(), Is.False, "实例边仍按该代谓词，不跟着开");
        Assert.That(s.Progress.Flags, Does.Not.Contain("hub_registered").And.Not.Contain("registration_authorized"), "不凭发现写登记事实");
    }

    /// <summary>
    /// MAP-Q115-02：旧档已满足事实但远边未锁（B11 口径），读档补锁存与发现、不写到访/探索度/日志/奖励；缺事实的不开；
    /// 换代后的旧实例边与单向出口不跨代开放。重复读档幂等。
    /// </summary>
    [Test]
    public void MAP_Q115_02_LoadRejudges_NoRewards_InstanceNotCrossGeneration()
    {
        var s = New();
        s.Progress.Flags.Add("church_contacted");   // 旧档：事实在，但两端都没到过，B11 口径没锁
        E.DebugTeleport(s, "d05_01");
        E.DebugSetProgress(s, "d05_01", 100);
        Assert.That(s.DungeonInstances["d05"].UnlockedEdges, Does.Contain("d05_01__d05_02"));
        E.DebugTeleport(s, "w33_waterworks");
        Assert.That(E.NewDungeonGeneration(s, "d05"), Is.Null);
        s.UnlockedEdges.Remove("c06_archive__c07_sanctuary");
        foreach (var e in Cfg.Edges.Where(e => e.Gate == "church_public_access")) { s.UnlockedEdges.Remove(e.Id); }
        foreach (var id in new[] { "c02_church", "c04_alms", "c05_admonition", "c06_archive", "c07_sanctuary" }) s.Locations[id].Discovered = false;
        var logs = s.Log.Count;
        var items = s.Items.Count;

        var m = Reload(s);
        Assert.That(m.UnlockedEdges, Is.SupersetOf(Cfg.Edges.Where(e => e.Gate == "church_public_access").Select(e => e.Id)), "加载补锁");
        Assert.That(m.Locations["c06_archive"].Discovered, Is.True);
        Assert.That(m.Locations["c02_church"].Visited, Is.False);
        Assert.That(m.Locations["c02_church"].Progress, Is.EqualTo(0), "不提前把公开地点置 100");
        Assert.That(m.Log.Count, Is.EqualTo(logs), "不补日志");
        Assert.That(m.Items.Count, Is.EqualTo(items), "不补物资");
        Assert.That(Cfg.Edges.Where(e => e.Gate == "register").Any(e => m.UnlockedEdges.Contains(e.Id)), Is.False, "缺登记事实的不开");
        Assert.That(m.DungeonInstances["d05"].UnlockedEdges, Does.Not.Contain("d05_01__d05_02"), "换代后旧实例边不跨代");
        Assert.That(E.EdgeOpen(m, E.Edge("d05_08__w33_waterworks")), Is.False, "出口每次重验当前代");
        Assert.That(SaveSerializer.Serialize(Reload(m)), Is.EqualTo(SaveSerializer.Serialize(m)), "再读一次不变（幂等）");
    }

    // ───────────── R126 / Q116 ─────────────

    /// <summary>
    /// MAP-Q116-01：本点处于双向边 to 端时 100% 仍揭示该边（门槛未达只显示锁与具体原因、远端只记已发现）；单向出口的 to 端不揭示逆向；旧 8 地点同样适用；揭示≠开通。
    /// </summary>
    [Test]
    public void MAP_Q116_01_Reveal100_AllIncidentEdges_OneWayOutgoingOnly()
    {
        var s = New();
        E.DebugTeleport(s, "w12_canal");
        Assert.That(s.Locations["w11_orchard"].Discovered, Is.False);
        E.DebugSetProgress(s, "w12_canal", 100);
        var lk = E.AdjacentLocked(s).Single(x => x.Target == "w11_orchard");
        Assert.That(lk.Edge.B, Is.EqualTo("w12_canal"), "本点是数据 to 端");
        Assert.That(lk.Reason, Does.Contain("荒弃果园").And.Contain("100%"), "写具体原因");
        Assert.That((s.Locations["w11_orchard"].Discovered, s.Locations["w11_orchard"].Visited, s.Locations["w11_orchard"].Progress), Is.EqualTo((true, false, 0)));
        Assert.That(E.EdgeOpen(s, lk.Edge), Is.False, "揭示不等于开通");

        // 单向出口：w33 是 d05_08→w33 的 to 端，w33 100% 不揭示逆向
        E.DebugTeleport(s, "w33_waterworks");
        E.DebugSetProgress(s, "w33_waterworks", 100);
        Assert.That(E.AdjacentLocked(s).Any(x => x.Target == "d05_08"), Is.False);
        Assert.That(s.Locations["d05_08"].Discovered, Is.False, "单向只算 from");
        Assert.That(E.EdgeRevealed(s, E.Edge("d05_08__w33_waterworks")), Is.False);

        // 旧 8 地点：诊所是 village__clinic 的 to 端；直接到诊所探索 100 时村庄那条（village100 未满足）显示为未开通
        var t = New();
        E.DebugTeleport(t, "clinic");
        E.DebugSetProgress(t, "clinic", 100);
        var back = E.AdjacentLocked(t).Single(x => x.Target == "village");
        Assert.That(back.Reason, Does.Contain("废弃村庄"));
    }
}
