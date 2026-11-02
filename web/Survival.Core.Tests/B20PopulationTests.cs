using System.Reflection;
using Newtonsoft.Json.Linq;
using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 B20 R64 + 并入的 Q31：24 个新野外敌池与 5 个新人口区（另含 Q31 的 4 个 mainline 区与 21 个主线野外池）。
/// 出处：A09「E5 二十四新增野外敌池」「最终敌方资源/人口接口」「主线普通敌池具体权重」；R05/A09「Q31 新增135节点…」；
/// node-expansion-rules.json worldPopulation；expansion-combat.json newWildPools/populationRegions。验收 EC02、POP31-01/03/04/05。
/// </summary>
public class B20PopulationTests : TestBase
{
    private static JObject Combat() =>
        JObject.Parse(File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "design.expansion-combat.json")));
    private static JObject Nodes() =>
        JObject.Parse(File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "design.node-expansion.json")));

    private GameState Fresh(uint seed = 4001)
    {
        var s = New(seed);
        s.GameMinute = At(2, 9, 0);
        s.SpawnNextCheck = int.MaxValue;
        s.PatrolNextCheck = int.MaxValue;
        return s;
    }

    private void Patrol(GameState s) =>
        typeof(GameEngine).GetMethod("Patrol", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(E, new object[] { s });

    /// <summary>EC02（数据）：五新区上限 12/12/16/12/8、节点表与刷新/巡游节律原样；24 个新野外池权重、首次 2 只、本地上限 4 对 JSON；Q31 的主线 21 池对 A12 mainlineEnemyPools。</summary>
    [Category("DesignSync")]
    [Test]
    public void EC02_RegionsAndPools_MatchJson()
    {
        var j = Combat();
        var regs = Cfg.WorldEnemies.Regions.ToDictionary(r => r.Id);
        Assert.That(regs, Has.Count.EqualTo(9));
        foreach (JObject r in (JArray)j["populationRegions"]!)
        {
            var c = regs[(string)r["id"]!];
            Assert.That((c.Cap, c.SpawnEveryMinutes, c.SpawnAttempt, c.PatrolEveryMinutes, c.PatrolAttempt),
                Is.EqualTo(((int)r["cap"]!, (int)r["spawnEveryMinutes"]!, (double)r["spawnAttempt"]!, (int)r["patrolEveryMinutes"]!, (double)r["patrolAttempt"]!)));
            Assert.That(c.Nodes, Is.EqualTo(((JArray)r["nodes"]!).Select(x => (string)x!).ToList()));
            Assert.That(c.LayerWeights, Is.Null, "新五区不分层");
        }
        Assert.That(new[] { "ex_pop_church_approach", "ex_pop_academy_approach", "ex_pop_deep_wild", "ex_pop_city_approach", "ex_pop_utility" }.Select(id => regs[id].Cap),
            Is.EqualTo(new[] { 12, 12, 16, 12, 8 }));
        var wild = ((JArray)j["newWildPools"]!).Cast<JObject>().ToList();
        Assert.That(wild, Has.Count.EqualTo(24));
        foreach (var p in wild)
        {
            var l = E.Location((string)p["nodeId"]!);
            Assert.That(l.PopulationWeights, Is.EquivalentTo(((JObject)p["weights"]!).Properties().ToDictionary(x => x.Name, x => (int)x.Value)), l.Id);
            Assert.That((l.FirstPopulation, l.PopulationLocalCap, l.PopulationRegionId), Is.EqualTo(((int)p["firstPopulation"]!, (int)p["localCap"]!, (string)p["populationRegionId"]!)));
        }
        var a12 = (JObject)JObject.Parse(File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "design.a12.json")))["mainlineEnemyPools"]!;
        foreach (var p in a12.Properties())
        {
            // A12 旧表的 b01_school/b02_station 在 Q31（node-expansion-rules，较新）是公共服务点 none_random_world：不随机生成/巡入
            if (p.Name is "b01_school" or "b02_station") { Assert.That(E.Location(p.Name).PopulationWeights, Is.Null, p.Name); continue; }
            Assert.That(E.Location(p.Name).PopulationWeights, Is.EquivalentTo(((JObject)p.Value).Properties().ToDictionary(x => x.Name, x => (int)x.Value)), p.Name);
        }
        Assert.That(Cfg.Locations.Count(l => l.PopulationWeights != null), Is.EqualTo(45), "Q31：45 个普通野外节点");
        Assert.That(E.Location("m02_checkpoint").FirstPopulation, Is.EqualTo(0));
        var main = regs["mainline_wild"];
        Assert.That((main.Cap, main.SpawnStartWorldMinute, main.LayerWeights!["ordinary"], main.LayerOpenWorldDay!["strong"]), Is.EqualTo((12, 1440, 96.0, 7)));
        // POP31-01/05：城市/公共楼与 67 个地牢房间没有随机人口
        Assert.That(Cfg.Locations.Where(l => l.IsDungeon || l.NodeKind == "city_building").All(l => l.PopulationWeights == null), Is.True);
    }

    /// <summary>EC02：首次实际到达新野外节点生成 2 只（按池抽）、记该区已进入；再到不补；新普通变体只出现在新区池里，不倒灌新手 8 点。</summary>
    [Test]
    public void EC02_FirstArrival_TwoFromPool_Once()
    {
        var s = Fresh();
        Assert.That(s.Population.EnteredRegions, Is.Empty);
        E.DebugMapTeleport(s, "w16_service");
        var here = s.WorldEnemies.Where(w => w.LocationId == "w16_service").ToList();
        Assert.That(here, Has.Count.EqualTo(2));
        Assert.That(here.All(w => E.Location("w16_service").PopulationWeights!.ContainsKey(w.EnemyId)), Is.True);
        Assert.That(s.Population.EnteredRegions, Is.EquivalentTo(new[] { "ex_pop_academy_approach" }));
        Assert.That(E.VisibleEnemiesAt(s, "w16_service"), Has.Count.EqualTo(2), "作为普通敌踪显示");
        E.DebugMapTeleport(s, "w17_switchback");
        E.DebugMapTeleport(s, "w16_service");
        Assert.That(E.LocalCount(s, "w16_service"), Is.EqualTo(2), "再到不补");
        Assert.That(Cfg.WorldEnemies.Pools.Values.SelectMany(t => t.Values).SelectMany(x => x).Any(x => x.EnemyId.StartsWith("ec_")), Is.False, "新敌不进新手 8 点刷新池");
    }

    /// <summary>POP31-04：首次 2 只是目标上限——区剩余上限不足时只补到上限；m02 首次 0 只（只有拦路固定战）。</summary>
    [Test]
    public void FirstPopulation_LimitedByRegionCap_M02Zero()
    {
        var s = Fresh();
        var r = Cfg.WorldEnemies.Regions.Single(x => x.Id == "ex_pop_church_approach");
        foreach (var (n, k) in new[] { ("w12_canal", 4), ("w13_mill", 4), ("w14_cemeteryroad", 3) })
            for (var i = 0; i < k; i++) E.DebugSpawnEnemy(s, "z_slow", n);
        Assert.That(E.RegionLiveCount(s, r), Is.EqualTo(11));
        E.DebugMapTeleport(s, "w11_orchard");
        Assert.That(E.RegionLiveCount(s, r), Is.EqualTo(12), "只补到区上限 12");
        Assert.That(E.LocalCount(s, "w11_orchard"), Is.EqualTo(1), "首次 2 只是目标上限，不突破区上限");

        var m = Fresh();
        E.DebugMapTeleport(m, "m02_checkpoint");
        Assert.That(E.LocalCount(m, "m02_checkpoint"), Is.EqualTo(0));
        Assert.That(E.FixedEnemiesAt(m, "m02_checkpoint"), Has.Count.EqualTo(1));
    }

    /// <summary>EC02：已进入的区每 120 分钟 25% 尝试 1 只，避开玩家当前节点；区总数与本地 4 双上限；未进入的区不刷新、不追补。</summary>
    [Test]
    public void EC02_SpawnChecks_RespectCaps_SkipCurrentAndUnentered()
    {
        var s = Fresh(4002);
        E.DebugMapTeleport(s, "w21_pine");
        var deep = Cfg.WorldEnemies.Regions.Single(x => x.Id == "ex_pop_deep_wild");
        for (var i = 0; i < 400; i++)
        {
            var added = E.DebugRegionSpawnCheck(s);
            Assert.That(added.All(w => w.LocationId != "w21_pine"), Is.True, "避开玩家当前节点");
            Assert.That(added.All(w => deep.Nodes.Contains(w.LocationId)), Is.True, "只刷已进入的区");
            Assert.That(E.RegionLiveCount(s, deep), Is.LessThanOrEqualTo(16));
            foreach (var n in deep.Nodes) Assert.That(E.LocalCount(s, n), Is.LessThanOrEqualTo(4), n);
        }
        Assert.That(E.RegionLiveCount(s, deep), Is.EqualTo(16), "400 次检查后刷满区上限");
        Assert.That(E.LocalCount(s, "w21_pine"), Is.EqualTo(2), "当前节点不刷");
        Assert.That(s.WorldEnemies.Count(w => w.LocationId.StartsWith("w1") || w.LocationId.StartsWith("w3")), Is.EqualTo(0), "其他区未进入，不刷");
        Assert.That(E.LiveCount(s), Is.EqualTo(s.WorldEnemies.Count(w => E.Location(w.LocationId).IsStarter)), "旧全局 12 只数旧 8 地点");
    }

    /// <summary>mainline 区按 96/3.5/0.5 抽层，节点池都算普通层：抽到稀有/强敌层本次不刷（开发假设 B20-7）；普通层照常。</summary>
    [Test]
    public void MainlineRegion_LayerRoll_OnlyOrdinarySpawns()
    {
        var s = Fresh(4003);
        E.DebugMapTeleport(s, "m01_road");
        var r = Cfg.WorldEnemies.Regions.Single(x => x.Id == "mainline_wild");
        var before = E.RegionLiveCount(s, r);
        BattleForce(s, x => x < 0.01, x => x >= 0.97);   // 成功 → 抽到稀有层
        E.DebugRegionSpawnCheck(s);
        Assert.That(E.RegionLiveCount(s, r), Is.EqualTo(before), "稀有层无刷新、不重抽");
        BattleForce(s, x => x < 0.01, x => x < 0.5, x => true, x => true);
        E.DebugRegionSpawnCheck(s);
        Assert.That(E.RegionLiveCount(s, r), Is.EqualTo(before + 1), "普通层刷 1 只");
    }

    /// <summary>EC02 / POP31-03：巡游只走同区物理边、有空位的节点，不跨区、不进城市/地牢、不进新手 8 点；新手种群也不进新区。</summary>
    [Test]
    public void Patrol_StaysInRegion()
    {
        var s = Fresh(4004);
        E.DebugMapTeleport(s, "w11_orchard");
        E.DebugMapTeleport(s, "camp");
        var church = Cfg.WorldEnemies.Regions.Single(x => x.Id == "ex_pop_church_approach");
        var ids = s.WorldEnemies.Where(w => church.Nodes.Contains(w.LocationId)).Select(w => w.Id).ToList();
        Assert.That(ids, Has.Count.EqualTo(2));
        var moved = false;
        for (var i = 0; i < 60; i++)
        {
            var at = s.WorldEnemies.Where(w => ids.Contains(w.Id)).Select(w => w.LocationId).ToList();
            Patrol(s);
            var now = s.WorldEnemies.Where(w => ids.Contains(w.Id)).Select(w => w.LocationId).ToList();
            Assert.That(now.All(church.Nodes.Contains), Is.True, "不出区：" + string.Join(",", now));
            if (!at.SequenceEqual(now)) moved = true;
        }
        Assert.That(moved, Is.True, "会巡游");
        Assert.That(s.WorldEnemies.Where(w => !ids.Contains(w.Id)).All(w => E.Location(w.LocationId).IsStarter), Is.True, "新手种群不进新区");
    }

    /// <summary>旧档读入（schema 32 当前版本）：已有敌人照旧；新手区刷新仍按旧全局 12（不因新区敌人挤占）。</summary>
    [Test]
    public void LegacyGlobal12_NotSharedWithRegions()
    {
        var s = Fresh(4005);
        E.DebugMapTeleport(s, "w21_pine");
        var deep = Cfg.WorldEnemies.Regions.Single(x => x.Id == "ex_pop_deep_wild");
        foreach (var n in deep.Nodes) while (E.LocalCount(s, n) < 2) E.DebugSpawnEnemy(s, "wolf", n);
        Assert.That(E.RegionLiveCount(s, deep), Is.EqualTo(16));
        var legacy = E.LiveCount(s);
        Assert.That(legacy, Is.LessThan(12));
        Assert.That(E.SpawnCandidates(s, "common"), Is.Not.Empty, "新区 16 只不占新手 12");
        Assert.That(E.DebugSpawnBlocked(s, "w22_ravine"), Does.Contain("人口区敌人已达上限16"));
    }

    private static void BattleForce(GameState s, params Func<double, bool>[] next)
    {
        for (uint c = 1; c < 50_000_000; c++)
        {
            var r = new Rng { State = c * 2654435761u + 7 };
            if (next.All(p => p(r.NextDouble()))) { s.Rng.State = c * 2654435761u + 7; return; }
        }
        Assert.Fail("找不到满足条件的随机序列");
    }
}
