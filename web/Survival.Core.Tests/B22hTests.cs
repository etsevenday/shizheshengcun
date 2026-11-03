using Newtonsoft.Json.Linq;
using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 B22h：战斗撤退与人口分层（R195 Q192 选 C、R198 Q195、R199 Q196 选 C）。
/// 出处：party-combat-expedition-adjudications.json layering / protection / retreat / acceptance；02 待确认 Q192/Q195/Q196 策划答复。
/// </summary>
public class B22hTests : BattleTestBase
{
    private static JObject Pce() =>
        JObject.Parse(File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "design.pce.json")));

    private GameState Fresh(uint seed = 4101)
    {
        var s = New(seed);
        s.GameMinute = At(2, 9, 0);
        s.SpawnNextCheck = int.MaxValue;
        s.PatrolNextCheck = int.MaxValue;
        return s;
    }

    /// <summary>找一段满足条件的随机序列（每个谓词对应依次抽出的一个随机数）并写进存档随机源。</summary>
    private static void ForceRng(GameState s, params Func<double, bool>[] next)
    {
        for (uint c = 1; c < 50_000_000; c++)
        {
            var r = new Rng { State = c * 2654435761u + 7 };
            if (next.All(p => p(r.NextDouble()))) { s.Rng.State = c * 2654435761u + 7; return; }
        }
        Assert.Fail("找不到满足条件的随机序列");
    }

    // ───────────── R195 人口分层（Q192） ─────────────

    /// <summary>PLAN-Q192-02（数据）：21 节点逐敌层表对合同原样；每种原池敌恰归一层、层内权重保留；b01/b02 与 ex_pop 五区不入表。</summary>
    [Test]
    public void PLAN_Q192_02_LayerTable_MatchesContract()
    {
        var lay = (JObject)Pce()["layering"]!;
        var rows = ((JArray)lay["rows"]!).Cast<JObject>().ToList();
        Assert.That(rows, Has.Count.EqualTo(21));
        foreach (var r in rows)
        {
            var l = E.Location((string)r["nodeId"]!);
            Assert.That(l.PopulationRegionId, Is.EqualTo((string)r["regionId"]!));
            var union = new Dictionary<string, int>();
            foreach (var layer in new[] { "ordinary", "rare", "strong" })
            {
                var want = ((JObject)r["layers"]![layer]!).Properties().ToDictionary(x => x.Name, x => (int)x.Value);
                Assert.That(l.PopulationLayers![layer], Is.EquivalentTo(want), $"{l.Id} {layer}");
                foreach (var kv in want) { Assert.That(union.ContainsKey(kv.Key), Is.False, $"{l.Id} {kv.Key} 只归一层"); union[kv.Key] = kv.Value; }
            }
            Assert.That(union, Is.EquivalentTo(l.PopulationWeights!), $"{l.Id} 层表并集=原池");
        }
        Assert.That(Cfg.Locations.Count(l => l.PopulationLayers != null), Is.EqualTo(21));
        Assert.That(E.Location("b01_school").PopulationLayers, Is.Null);
        Assert.That(E.Location("b02_station").PopulationLayers, Is.Null);
        var ex = Cfg.WorldEnemies.Regions.Where(x => x.Id.StartsWith("ex_pop")).SelectMany(x => x.Nodes).ToHashSet();
        Assert.That(Cfg.Locations.Where(l => ex.Contains(l.Id)).All(l => l.PopulationLayers == null), Is.True, "ex_pop 五区不加层");
        var main = Cfg.WorldEnemies.Regions.Single(x => x.Id == "mainline_wild");
        Assert.That((main.LayerWeights!["ordinary"], main.LayerWeights["rare"], main.LayerWeights["strong"]), Is.EqualTo((96.0, 3.5, 0.5)));
        Assert.That((main.LayerOpenWorldDay!["ordinary"], main.LayerOpenWorldDay["rare"], main.LayerOpenWorldDay["strong"]), Is.EqualTo((1, 3, 7)));
    }

    /// <summary>
    /// PLAN-Q192-01：第 3 日起抽到稀有层，选中的节点稀有层非空（w02_forest：狼 20、缠绕者 20）就在层内按原权重抽一种、只生一名；
    /// 选中节点稀有层空（其余 mainline_wild 节点）即抽空，不重抽、不改到普通层。
    /// </summary>
    [Test]
    public void PLAN_Q192_01_RareLayer_SpawnsFromNodeLayer_EmptyLayerNoReroll()
    {
        var s = Fresh(4102);
        s.GameMinute = At(3, 9, 0);
        E.DebugMapTeleport(s, "m01_road");
        var r = Cfg.WorldEnemies.Regions.Single(x => x.Id == "mainline_wild");
        var cands = E.RegionSpawnCandidates(s, r);
        var forest = cands.IndexOf("w02_forest");
        var other = cands.IndexOf("w01_farm");
        Assert.That(forest, Is.GreaterThanOrEqualTo(0));
        Assert.That(other, Is.GreaterThanOrEqualTo(0));
        double Mid(int i) => (i + 0.5) / cands.Count;
        bool Hit(double x, int i) => Math.Abs(x - Mid(i)) < 0.5 / cands.Count;

        // 稀有层（96%≤x<99.5%）→ 选到 w02_forest → 层内抽种
        var before = s.WorldEnemies.Select(w => w.Id).ToHashSet();
        ForceRng(s, x => x < 0.25, x => x >= 0.965 && x < 0.99, x => Hit(x, forest), x => true);
        var added = E.DebugRegionSpawnCheck(s);
        Assert.That(added, Has.Count.EqualTo(1), "非空层只生一名");
        Assert.That(added[0].LocationId, Is.EqualTo("w02_forest"));
        Assert.That(new[] { "wolf", "z_clinger" }, Does.Contain(added[0].EnemyId), "稀有层内抽种");

        // 稀有层 → 选到 w01_farm（稀有层空）→ 抽空，不重抽
        var count = E.RegionLiveCount(s, r);
        ForceRng(s, x => x < 0.25, x => x >= 0.965 && x < 0.99, x => Hit(x, E.RegionSpawnCandidates(s, r).IndexOf("w01_farm")), x => true);
        Assert.That(E.DebugRegionSpawnCheck(s), Is.Empty, "该节点该层空：抽空");
        Assert.That(E.RegionLiveCount(s, r), Is.EqualTo(count));
    }

    /// <summary>PLAN-Q192-01/02：未开放层抽空不重抽——第 3 日前稀有层、第 7 日前强敌层都不刷，即使节点该层有敌；普通层照常按层内权重。</summary>
    [Test]
    public void PLAN_Q192_02_UnopenedLayers_NoSpawn_OrdinaryUsesLayer()
    {
        var s = Fresh(4103);
        s.GameMinute = At(6, 9, 0);
        E.DebugMapTeleport(s, "m01_road");
        var r = Cfg.WorldEnemies.Regions.Single(x => x.Id == "mainline_wild");
        var forest = E.RegionSpawnCandidates(s, r).IndexOf("w02_forest");
        var n = E.RegionSpawnCandidates(s, r).Count;
        bool AtF(double x) => Math.Abs(x - (forest + 0.5) / n) < 0.5 / n;
        var count = E.RegionLiveCount(s, r);
        ForceRng(s, x => x < 0.25, x => x >= 0.996, x => AtF(x), x => true);   // 强敌层（≥99.5%），第 6 日未开放
        Assert.That(E.DebugRegionSpawnCheck(s), Is.Empty, "强敌层第 7 日才开放");
        s.GameMinute = At(2, 9, 0);
        ForceRng(s, x => x < 0.25, x => x >= 0.965 && x < 0.99, x => AtF(x), x => true);   // 稀有层，第 2 日未开放
        Assert.That(E.DebugRegionSpawnCheck(s), Is.Empty, "稀有层第 3 日才开放");
        Assert.That(E.RegionLiveCount(s, r), Is.EqualTo(count));
        // 第 7 日强敌层：w02_forest 强敌层只有头狼
        s.GameMinute = At(7, 9, 0);
        ForceRng(s, x => x < 0.25, x => x >= 0.996, x => AtF(x), x => true);
        var strong = E.DebugRegionSpawnCheck(s);
        Assert.That(strong.Select(w => (w.LocationId, w.EnemyId)), Is.EqualTo(new[] { ("w02_forest", "wild_dog_leader") }));
        // 普通层：w02_forest 普通层只有野狗
        ForceRng(s, x => x < 0.25, x => x < 0.9, x => AtF(x), x => true);
        var ord = E.DebugRegionSpawnCheck(s);
        Assert.That(ord.Select(w => (w.LocationId, w.EnemyId)), Is.EqualTo(new[] { ("w02_forest", "feral_dog") }), "普通层内按原权重相对归一");
    }

    /// <summary>PLAN-Q192-02：初次人口只按普通层（不借初次人口绕过日层），原初始 2 只；ex_pop 五区与 b01/b02 不变。</summary>
    [Test]
    public void PLAN_Q192_02_FirstPopulation_OrdinaryLayerOnly()
    {
        for (uint seed = 4110; seed < 4140; seed++)
        {
            E = new GameEngine(Cfg);
            var s = Fresh(seed);
            s.GameMinute = At(9, 9, 0);   // 稀有/强敌层都已开放也不影响初次人口
            foreach (var node in new[] { "w02_forest", "a02_pump", "a03_foundry", "b03_hospital" })
            {
                E.DebugMapTeleport(s, node);
                var here = s.WorldEnemies.Where(w => w.LocationId == node && w.FixedOf == null).ToList();
                Assert.That(here, Has.Count.EqualTo(E.Location(node).FirstPopulation), node);
                Assert.That(here.All(w => E.Location(node).PopulationLayers!["ordinary"].ContainsKey(w.EnemyId)), Is.True, $"{node}：{string.Join(",", here.Select(w => w.EnemyId))}");
            }
            E.DebugMapTeleport(s, "b01_school");
            Assert.That(E.LocalCount(s, "b01_school"), Is.EqualTo(0), "b01 不刷新");
        }
    }

    /// <summary>PLAN-Q192-01：ex_pop 五区不分层，整池照旧（刷新抽种只看原池）。</summary>
    [Test]
    public void PLAN_Q192_01_ExPop_Unchanged()
    {
        var s = Fresh(4104);
        E.DebugMapTeleport(s, "w21_pine");
        var deep = Cfg.WorldEnemies.Regions.Single(x => x.Id == "ex_pop_deep_wild");
        Assert.That(deep.LayerWeights, Is.Null);
        for (var i = 0; i < 200; i++)
            foreach (var w in E.DebugRegionSpawnCheck(s))
                Assert.That(E.Location(w.LocationId).PopulationWeights!.ContainsKey(w.EnemyId), Is.True);
        Assert.That(E.RegionLiveCount(s, deep), Is.EqualTo(16));
    }
}
