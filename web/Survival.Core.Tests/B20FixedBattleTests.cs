using System.Reflection;
using Newtonsoft.Json.Linq;
using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 B20 R63：31 组地牢固定战 + m02 检查哨拦路战。出处：A09「E2 战斗通则与差异化环境」「E4 固定战逐节点名单」；
/// R05「2026-10-30 B4 实例敌人、人口区与奖励账」「Q62—Q74」当前优先节；expansion-combat.json enemySets/dungeonInterfaces；
/// gate-contract factProducers.checkpoint_obstruction_cleared；A08 m02“1迟缓尸固定任务战”。验收 EC01、EC03、EC04、EC06、DR21。
/// </summary>
public class B20FixedBattleTests : BattleTestBase
{
    private static JObject Combat() =>
        JObject.Parse(File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "design.expansion-combat.json")));

    private GameState At(string node, int capacity, uint seed = 3001)
    {
        var s = New(seed);
        UnlockCapacity(s, capacity);
        s.GameMinute = At(2, 9, 0);
        s.SpawnNextCheck = int.MaxValue;
        s.PatrolNextCheck = int.MaxValue;
        E.DebugMapTeleport(s, node);
        return s;
    }

    /// <summary>把本场敌人逐个判死并结算胜利（掉落照常锁定）。</summary>
    private void WinBattle(GameState s)
    {
        var kill = typeof(GameEngine).GetMethod("KillEnemy", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var check = typeof(GameEngine).GetMethod("CheckVictory", BindingFlags.NonPublic | BindingFlags.Instance)!;
        foreach (var e in s.Battle!.Enemies.Where(e => e.Alive).ToList()) kill.Invoke(E, new object[] { s, e });
        check.Invoke(E, new object[] { s });
        Assert.That(s.Battle, Is.Null);
    }

    /// <summary>EC01：31 组固定战节点、名单与守卫门逐条对 JSON；8 个守卫节点 = 八座地牢 guardNode；安全点不放固定战；另有 m02 单局拦路战（1 迟缓尸）。</summary>
    [Category("DesignSync")]
    [Test]
    public void EC01_FixedBattles_MatchJson_GuardsAndSafeNodes()
    {
        var j = Combat();
        var sets = ((JArray)j["enemySets"]!).Cast<JObject>().ToList();
        Assert.That(sets, Has.Count.EqualTo(31));
        var cfg = Cfg.WorldEnemies.FixedBattles.Where(f => f.DungeonId != null).ToDictionary(f => f.NodeId);
        Assert.That(cfg, Has.Count.EqualTo(31));
        foreach (var x in sets)
        {
            var f = cfg[(string)x["nodeId"]!];
            Assert.That(f.EnemyOrder, Is.EqualTo(((JArray)x["enemyOrder"]!).Select(v => (string)v!).ToList()), f.NodeId);
            Assert.That(f.Guardian, Is.EqualTo((bool)x["requiredGate"]!), f.NodeId);
            Assert.That(f.Scope, Is.EqualTo("instance"));
        }
        foreach (JObject d in (JArray)j["dungeonInterfaces"]!)
        {
            var guard = (string)d["guardNode"]!;
            Assert.That(cfg[guard].Guardian, Is.True, guard);
            Assert.That(cfg[guard].EnemyOrder[0], Does.StartWith("ec_boss_"), "守卫是首项");
            Assert.That(cfg[guard].Fact, Is.EqualTo("guardianDefeated"));
            foreach (var safe in ((JArray)d["safeNodes"]!).Select(v => (string)v!)) Assert.That(cfg.ContainsKey(safe), Is.False, safe);
        }
        var m02 = Cfg.WorldEnemies.FixedBattles.Single(f => f.DungeonId == null);
        Assert.That((m02.NodeId, string.Join(",", m02.EnemyOrder), m02.Scope, m02.Fact), Is.EqualTo(("m02_checkpoint", "z_slow", "campaign", "checkpoint_obstruction_cleared")));
    }

    /// <summary>EC03 / DR21：首次揭示按当时容量 C1—C4 截前 C 个（守卫首项）；之后扩容不重编、实到人数不足 C 不缩怪。</summary>
    [Test]
    public void EC03_DR21_TruncateByCapacityAtReveal_NoRebuild()
    {
        for (var c = 1; c <= 4; c++)
        {
            var s = At("d01_07", c);
            var st = E.FixedStateAt(s, "d01_07")!;
            Assert.That(st.Capacity, Is.EqualTo(c));
            Assert.That(st.Roster, Is.EqualTo(new[] { "ec_boss_crypt", "ec_cantor", "z_clinger", "z_slow" }.Take(c).ToList()), $"C{c}");
            Assert.That(E.FixedEnemiesAt(s, "d01_07").Select(w => w.EnemyId), Is.EqualTo(st.Roster));
            Assert.That(s.Party, Has.Count.EqualTo(1), "只有主角一人（实到人数不足 C）");
        }
        var t = At("d01_07", 2);
        var ids = E.FixedEnemiesAt(t, "d01_07").Select(w => w.Id).ToList();
        UnlockCapacity(t, 4);
        E.DebugMapTeleport(t, "d01_06");
        E.DebugMapTeleport(t, "d01_07");
        Assert.That(E.FixedEnemiesAt(t, "d01_07").Select(w => w.Id), Is.EqualTo(ids), "扩容后再到不重编");
        Assert.That(E.EngageFixed(t).Ok, Is.True);
        Assert.That(t.Battle!.Enemies.Select(e => e.EnemyId), Is.EqualTo(new[] { "ec_boss_crypt", "ec_cantor" }), "入场仍是揭示时的 2 只");
        Assert.That(t.Battle.Enemies.Select(e => e.Pos), Is.EqualTo(new[] { 1, 2 }), "从 1 号位紧凑排位");
        Assert.That(t.Battle.FixedKey, Is.EqualTo("fixed:d01_07"));
    }

    /// <summary>EC06：撤退后固定敌 HP 与 worldEnemyId 保留、读档不重抽名单；固定敌不计入种群、不进普通敌踪卡与随机接敌。</summary>
    [Test]
    public void EC06_RetreatKeepsHp_ReloadSameRoster()
    {
        var s = At("d02_03", 3);
        var before = E.FixedEnemiesAt(s, "d02_03").Select(w => (w.Id, w.EnemyId)).ToList();
        Assert.That(before.Select(x => x.EnemyId), Is.EqualTo(new[] { "ec_labwalker", "z_spitter", "z_nurse" }));
        Assert.That(E.VisibleEnemiesAt(s, "d02_03"), Is.Empty, "固定敌不在普通敌踪卡里");
        Assert.That(E.LocalCount(s, "d02_03"), Is.EqualTo(0), "不计种群");
        Assert.That(E.EngageFixed(s).Ok, Is.True);
        s.Battle!.Enemies[0].Hp = 1.5;
        PlayerFirst(s);
        E.BattleStep(s);
        Assert.That(E.AwaitingPlayer(s), Is.True);
        Force(s, Below(0.0001));
        Assert.That(Cmd(s, "retreat").Ok, Is.True);
        Assert.That(s.Battle, Is.Null, "撤退成功");
        var after = E.FixedEnemiesAt(s, "d02_03");
        Assert.That(after.Select(w => (w.Id, w.EnemyId)), Is.EqualTo(before));
        Assert.That(after[0].Hp, Is.EqualTo(1.5).Within(1e-9), "HP 保留");
        var t = SaveSerializer.Deserialize(SaveSerializer.Serialize(s), Cfg);
        Assert.That(E.FixedEnemiesAt(t, "d02_03").Select(w => (w.Id, w.EnemyId)), Is.EqualTo(before), "读档不重抽");
        Assert.That(E.FixedStateAt(t, "d02_03")!.Roster, Has.Count.EqualTo(3));
    }

    /// <summary>EC04：固定战禁增援（噪声满/鸣响也不调用同地丧尸）；野外普通战本场累计含已死 ≤ C（初始满 C 不增援）。</summary>
    [Test]
    public void EC04_FixedNoReinforcement_WildCumulativeAtMostC()
    {
        var s = At("d03_05", 4);
        E.DebugSpawnEnemy(s, "shambler", "d03_05");   // 同地另有一只普通丧尸（开发放置）
        Assert.That(E.EngageFixed(s).Ok, Is.True);
        var b = s.Battle!;
        Assert.That(b.Enemies, Has.Count.EqualTo(3), "名单 3 只（riot、机修、鸣响）");
        b.ReinforcementRequested = true;
        typeof(GameEngine).GetMethod("TryReinforce", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(E, new object[] { s });
        Assert.That(b.Enemies, Has.Count.EqualTo(3), "固定战不增援");
        Assert.That(b.Log.Last(), Does.Contain("固定战不会引来增援"));

        // 野外：C2，同地 3 只，入场 2 只，噪声请求时累计已满 C 不再增援
        var w = FightWith(3002, 2, "shambler", "shambler", "shambler");
        Assert.That(w.Battle!.Enemies, Has.Count.EqualTo(2));
        w.Battle.ReinforcementRequested = true;
        typeof(GameEngine).GetMethod("TryReinforce", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(E, new object[] { w });
        Assert.That(w.Battle.Enemies, Has.Count.EqualTo(2), "累计 ≤ C");
        // 打死一只后仍不补（累计含已死）
        typeof(GameEngine).GetMethod("KillEnemy", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(E, new object[] { w, w.Battle.Enemies[0] });
        typeof(GameEngine).GetMethod("TryReinforce", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(E, new object[] { w });
        Assert.That(w.Battle.Enemies, Has.Count.EqualTo(2), "累计含已死 ≤ C");
    }

    /// <summary>开发假设 B20-5：地牢房间固定战敌人还在时不能探索/清理/采集，可以离开；胜利后可以。m02 拦路战不挡探索。</summary>
    [Test]
    public void FixedRoom_BlocksExploreUntilWon_CanLeave_M02DoesNot()
    {
        var s = At("d01_03", 3);
        Assert.That(E.CanStart(s, ActionRequest.Explore()), Does.Contain("固定敌踪还在这里"));
        Assert.That(E.CanStart(s, ActionRequest.Clear()), Does.Contain("固定敌踪还在这里"));
        Assert.That(E.CanStart(s, ActionRequest.Travel("d01_02")), Is.Null.Or.Not.Contains("固定"), "可以离开");
        Assert.That(E.EngageFixed(s).Ok, Is.True);
        WinBattle(s);
        Assert.That(E.CanStart(s, ActionRequest.Explore()), Is.Null);
        Assert.That(E.FixedStateAt(s, "d01_03")!.Won, Is.True);
        Assert.That(E.CanEngageFixed(s), Is.EqualTo("这里没有固定战"));

        var m = At("m02_checkpoint", 1);
        Assert.That(E.FixedEnemiesAt(m, "m02_checkpoint").Select(w => w.EnemyId), Is.EqualTo(new[] { "z_slow" }));
        Assert.That(E.CanStart(m, ActionRequest.Explore()), Is.Null, "m02 拦路战不挡探索");
    }

    /// <summary>
    /// 守卫胜利写本代实例事实 guardianDefeated：守卫门开、守卫战利品箱（d03）当场出现；m02 胜利写 checkpoint_obstruction_cleared，营门路线开通。
    /// B11 因来源未做而暂锁的 clear_checkpoint 与 guardian_defeated 门槛由此可正常解锁。
    /// </summary>
    [Test]
    public void GuardianAndCheckpointVictory_WriteFacts_OpenGates()
    {
        var s = At("d05_06", 3);
        E.DebugSetProgress(s, "d05_06", 100);
        Assert.That(E.AdjacentLocked(s).Any(x => x.Target == "d05_07"), Is.True, "守卫门未开");
        Assert.That(E.EngageFixed(s).Ok, Is.True);
        Assert.That(s.Battle!.Enemies[0].EnemyId, Is.EqualTo("ec_boss_water"));
        WinBattle(s);
        Assert.That(E.InstanceFact(s, "d05", "guardianDefeated"), Is.True);
        Assert.That(E.AdjacentUnlocked(s).Any(e => e.Other("d05_06") == "d05_07"), Is.True, "守卫门开");
        Assert.That(s.Log.Last(l => l.Kind == GameEventKind.BattleEnd.ToString()).Text, Does.Contain("守卫已击败（本代）"));

        var d3 = At("d03_07", 2);
        Assert.That(E.EngageFixed(d3).Ok, Is.True);
        WinBattle(d3);
        Assert.That(d3.Resources["officer_cache"].Discovered, Is.True, "Q122 守卫战利品箱当场出现");

        var m = At("m02_checkpoint", 1);
        E.DebugSetProgress(m, "m02_checkpoint", 100);
        Assert.That(E.AdjacentLocked(m).Any(x => x.Target == "h01_gate"), Is.True);
        Assert.That(E.EngageFixed(m).Ok, Is.True);
        WinBattle(m);
        Assert.That(m.Progress.Flags, Does.Contain("checkpoint_obstruction_cleared"));
        Assert.That(m.UnlockedEdges, Does.Contain("m02_checkpoint__h01_gate"));
        Assert.That(E.FixedEnemiesAt(m, "m02_checkpoint"), Is.Empty);
        E.DebugMapTeleport(m, "m01_road");
        E.DebugMapTeleport(m, "m02_checkpoint");
        Assert.That(E.FixedEnemiesAt(m, "m02_checkpoint"), Is.Empty, "单局一次，不再出现");
    }

    /// <summary>地牢换代：旧代固定战移除；新一代第一次到达重新揭示（新 worldEnemyId，按当时 C）；守卫事实须本代再胜。开发入口“守卫已击败”视为结清。</summary>
    [Test]
    public void NewGeneration_RevealsAgain_NewIds_DevFactSettles()
    {
        var s = At("d04_02", 2);
        var old = E.FixedEnemiesAt(s, "d04_02").Select(w => w.Id).ToList();
        E.DebugMapTeleport(s, "w10_watchtower");
        Assert.That(E.NewDungeonGeneration(s, "d04"), Is.Null);
        Assert.That(s.WorldEnemies.Any(w => old.Contains(w.Id)), Is.False, "旧代固定敌移除");
        UnlockCapacity(s, 3);
        E.DebugMapTeleport(s, "d04_02");
        var now = E.FixedEnemiesAt(s, "d04_02");
        Assert.That(now.Select(w => w.EnemyId), Is.EqualTo(new[] { "ec_cablecrawler", "z_darter" }));
        Assert.That(now.Any(w => old.Contains(w.Id)), Is.False, "新 worldEnemyId");
        Assert.That(now.All(w => w.Generation == 2), Is.True);

        var g = At("d04_06", 4);
        Assert.That(E.FixedEnemiesAt(g, "d04_06"), Has.Count.EqualTo(4));
        E.DebugInstanceFact(g, "d04", "guardianDefeated");
        Assert.That(E.FixedEnemiesAt(g, "d04_06"), Is.Empty, "开发入口记守卫已击败：本代守卫战视为结清");
        Assert.That(E.FixedStateAt(g, "d04_06")!.Won, Is.True);
    }

    /// <summary>存档 31→32：建空的 Population 账；不追补首次人口与固定战（下次实际到达才揭示），不按背包推测旧精英结算。</summary>
    [Test]
    public void Migration_31To32_EmptyLedger_RevealOnNextArrival()
    {
        var s = New(3010);
        E.DebugTeleport(s, "d01_02");   // B20 之前已经到过的地牢房间
        s.Population = new PopulationState();
        s.WorldEnemies.RemoveAll(w => w.FixedOf != null);
        var j = JObject.Parse(SaveSerializer.Serialize(s));
        j["SchemaVersion"] = "31";
        j.Remove("Population");
        foreach (var w in (JArray)j["WorldEnemies"]!) { ((JObject)w).Remove("FixedOf"); ((JObject)w).Remove("Generation"); }
        var t = SaveSerializer.Deserialize(j.ToString(), Cfg);
        Assert.That(t.SchemaVersion, Is.EqualTo("32"));
        Assert.That(t.Population, Is.Not.Null);
        Assert.That(t.Population.Fixed, Is.Empty, "不追补");
        Assert.That(t.Log.Last(l => l.Text.Contains("存档已升级到版本32")).Text, Does.Contain("下一次到达时才揭示固定战"));
        E.DebugMapTeleport(t, "d01_01");
        E.DebugMapTeleport(t, "d01_02");
        Assert.That(E.FixedEnemiesAt(t, "d01_02").Select(w => w.EnemyId), Is.EqualTo(new[] { "z_slow" }), "旧档 C1：下次到达按当时容量揭示");
    }
}
