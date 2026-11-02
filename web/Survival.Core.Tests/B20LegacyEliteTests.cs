using System.Reflection;
using Newtonsoft.Json.Linq;
using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 B20 R100：Q81 五处旧精英可选单局挑战。出处：A09「Q81 旧五种的可选投放」、R05「2026-10-31 Q75—Q82」同轮章、
/// legacy-elite-placements.json（= expansion-combat.json optionalLegacyEliteEncounters）；验收 RES-Q81a—c（Q75-Q82 更新说明）。
/// </summary>
public class B20LegacyEliteTests : BattleTestBase
{
    private static JArray Placements() =>
        (JArray)JObject.Parse(File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "design.legacy-elites.json")))["encounters"]!;

    private GameState Ready(string node, bool registered = true, int capacity = 1, uint seed = 5001)
    {
        var s = New(seed);
        UnlockCapacity(s, capacity);
        if (registered) s.Progress.Flags.Add("hub_registered");
        s.GameMinute = At(3, 9, 0);
        s.SpawnNextCheck = int.MaxValue;
        s.PatrolNextCheck = int.MaxValue;
        E.DebugMapTeleport(s, node);
        s.WorldEnemies.RemoveAll(w => w.FixedOf == null);   // 只看挑战本身（首次人口另测）
        return s;
    }

    private void WinBattle(GameState s)
    {
        var kill = typeof(GameEngine).GetMethod("KillEnemy", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var check = typeof(GameEngine).GetMethod("CheckVictory", BindingFlags.NonPublic | BindingFlags.Instance)!;
        foreach (var e in s.Battle!.Enemies.Where(e => e.Alive).ToList()) kill.Invoke(E, new object[] { s, e });
        check.Invoke(E, new object[] { s });
        Assert.That(s.Battle, Is.Null);
    }

    private void Retreat(GameState s)
    {
        PlayerFirst(s);
        E.BattleStep(s);
        Force(s, Below(0.0001));
        Assert.That(Cmd(s, "retreat").Ok, Is.True);
        Assert.That(s.Battle, Is.Null);
    }

    /// <summary>数据：五处地点、敌人、特殊掉落（含 z_guard 钢材 40%）、claimKey 逐条对 JSON；都在非安全主世界野外，不进任何随机池。</summary>
    [Category("DesignSync")]
    [Test]
    public void RES_Q81a_Data_FiveOptionalPlacements()
    {
        var src = Placements();
        Assert.That(src, Has.Count.EqualTo(5));
        foreach (JObject o in src)
        {
            var c = E.EliteConfig((string)o["id"]!)!;
            var item = ((JObject)o["reward"]!["items"]!).Properties().Single();
            Assert.That((c.EnemyId, c.NodeId, c.RewardItemId, c.RewardCount, c.RewardChance, c.ClaimKey, c.RevealAtExplorePercent, c.RequiresFact),
                Is.EqualTo(((string)o["enemyId"]!, (string)o["nodeId"]!, item.Name, (int)item.Value, (double)o["reward"]!["chance"]!, (string)o["reward"]!["claimKey"]!, 100, "hub_registered")));
            var l = E.Location(c.NodeId);
            Assert.That((l.NodeKind, l.IsDungeon, l.RandomEncounterEnabled), Is.EqualTo(("world", false, true)), "非安全主世界野外");
            Assert.That(Cfg.WorldEnemies.Pools.Values.SelectMany(t => t.Values).SelectMany(x => x).Any(x => x.EnemyId == c.EnemyId), Is.False, "不进新手刷新池");
            Assert.That(Cfg.Locations.Any(x => x.PopulationWeights != null && x.PopulationWeights.ContainsKey(c.EnemyId)), Is.False, "不进人口区池");
        }
    }

    /// <summary>
    /// RES-Q81a：登记且当地探索 100% 才显示；告示不是敌踪、不挡清理；明确接战才建唯一敌人；C1 也只这一名敌入场、不被截掉、不附同地敌、禁增援。
    /// </summary>
    [Test]
    public void RES_Q81a_NoticeAfterRegisterAnd100_OptionalAndSolo()
    {
        var u = Ready("w33_waterworks", registered: false);
        E.DebugSetProgress(u, "w33_waterworks", 100);
        Assert.That(E.EliteChallengesHere(u), Is.Empty, "未登记不显示");
        var s = Ready("w33_waterworks");
        Assert.That(E.EliteChallengesHere(s), Is.Empty, "探索不到 100 不显示");
        E.DebugSetProgress(s, "w33_waterworks", 100);
        var c = E.EliteChallengesHere(s).Single();
        Assert.That(c.EnemyId, Is.EqualTo("z_pump_foreman"));
        Assert.That(s.Log.Any(l => l.Text.StartsWith("可选强敌告示：荒废水厂有泵室领班")), Is.True);
        Assert.That(s.WorldEnemies.Any(w => w.EnemyId == "z_pump_foreman"), Is.False, "未接战不建敌人");
        Assert.That(E.VisibleEnemiesAt(s, "w33_waterworks"), Is.Empty, "告示不是活动敌踪");
        Assert.That(E.CanStart(s, ActionRequest.Clear()), Is.Null, "不挡清理");
        E.DebugSpawnEnemy(s, "z_slow", "w33_waterworks");   // 同地另有普通敌人
        Assert.That(E.EngageElite(s, c.Id).Ok, Is.True);
        var b = s.Battle!;
        Assert.That(b.Capacity, Is.EqualTo(2), "营区登记即 C2：同地另有一只普通敌也不跟着入场");
        Assert.That(b.Enemies.Select(e => e.EnemyId), Is.EqualTo(new[] { "z_pump_foreman" }), "只这一名，不附同地敌");
        Assert.That(b.FixedKey, Is.EqualTo("elite:" + c.Id));
        b.ReinforcementRequested = true;
        typeof(GameEngine).GetMethod("TryReinforce", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(E, new object[] { s });
        Assert.That(b.Enemies, Has.Count.EqualTo(1), "禁增援");
        Assert.That(E.LocalCount(s, "w33_waterworks"), Is.EqualTo(1), "不占种群名额（只数那只普通敌）");
    }

    /// <summary>RES-Q81b：z_guard 40% 抽签在首次接战时锁定；撤退保留 HP 与抽签，再接战是同一只；过 72 小时/读档仍在，不重生、不回满。</summary>
    [Test]
    public void RES_Q81b_RetreatKeepsHpAndLockedRoll()
    {
        var s = Ready("w32_railcut");
        E.DebugSetProgress(s, "w32_railcut", 100);
        const string id = "legacy_elite_z_guard";
        Assert.That(E.EngageElite(s, id).Ok, Is.True);
        var st = s.Population.Elites[id];
        var roll = st.RewardWon;
        Assert.That(roll, Is.Not.Null, "首次接战锁定抽签");
        var wid = st.WorldId;
        s.Battle!.Enemies[0].Hp = 4;
        Retreat(s);
        var w = E.EliteEnemy(s, id)!;
        Assert.That((w.Id, w.Hp), Is.EqualTo((wid!.Value, 4.0)), "撤退保留 HP 与唯一 ID");
        Assert.That(E.VisibleEnemiesAt(s, "w32_railcut").Any(x => x.Id == w.Id), Is.False, "不当普通敌踪");
        s.GameMinute += 4320;
        var t = SaveSerializer.Deserialize(SaveSerializer.Serialize(s), Cfg);
        Assert.That(t.Population.Elites[id].RewardWon, Is.EqualTo(roll), "读档不重抽");
        Assert.That(E.EngageElite(t, id).Ok, Is.True);
        Assert.That(t.Battle!.Enemies[0].WorldId, Is.EqualTo(wid));
        Assert.That(t.Battle.Enemies[0].Hp, Is.EqualTo(4.0), "72 小时后也不回满、不重生");
    }

    /// <summary>
    /// RES-Q81c：胜利且主角存活一次提交：泵室领班只发 precision_parts×1（替代原附加掉落，不叠发）；z_guard 抽签落空也记已结算、不发钢材；
    /// 结算后告示不再出现、不能再接战。旧档没有结算凭据：五处从未结算开始（不按背包推测）。
    /// </summary>
    [Test]
    public void RES_Q81c_SpecialDropOnce_MissStillSettled()
    {
        var s = Ready("w33_waterworks", seed: 5003);
        E.DebugSetProgress(s, "w33_waterworks", 100);
        const string id = "legacy_elite_z_pump_foreman";
        var ground = s.Items.Where(i => i.Owner == "w33_waterworks" && i.ItemId == "precision_parts").Sum(i => i.Count);
        Assert.That(E.EngageElite(s, id).Ok, Is.True);
        WinBattle(s);
        Assert.That(s.Items.Where(i => i.Owner == "w33_waterworks" && i.ItemId == "precision_parts").Sum(i => i.Count) - ground, Is.EqualTo(1), "特殊掉落一次，不叠原附加掉落");
        Assert.That(s.Population.Elites[id].Settled, Is.True);
        Assert.That(E.EliteChallengesHere(s), Is.Empty);
        Assert.That(E.CanEngageElite(s, id), Is.EqualTo("本局已挑战成功"));

        var g = Ready("w32_railcut", seed: 5004);
        E.DebugSetProgress(g, "w32_railcut", 100);
        const string gid = "legacy_elite_z_guard";
        Force(g, AtLeast(0.4));   // 首次接战抽签：40% 落空
        Assert.That(E.EngageElite(g, gid).Ok, Is.True);
        Assert.That(g.Population.Elites[gid].RewardWon, Is.False);
        WinBattle(g);
        Assert.That(g.Items.Any(i => i.Owner == "w32_railcut" && i.ItemId == "steel"), Is.False, "落空不发、不重抽");
        Assert.That(g.Population.Elites[gid].Settled, Is.True, "落空也记已结算");
        Assert.That(g.Log.Last(l => l.Kind == GameEventKind.BattleEnd.ToString()).Text, Does.Contain("抽签没有落中"));

        // 旧档（schema 31，没有 Population）：五处从未结算开始
        var old = New(5005);
        var j = JObject.Parse(SaveSerializer.Serialize(old));
        j["SchemaVersion"] = "31";
        j.Remove("Population");
        var t = SaveSerializer.Deserialize(j.ToString(), Cfg);
        Assert.That(t.Population.Elites, Is.Empty);
    }

    /// <summary>开发入口（?dev=1）：可选强敌一键到达（记登记、探索 100%）、人口区计数文字、固定战一键结清。</summary>
    [Test]
    public void DevEntries_EliteGo_PopulationText_WinFixed()
    {
        var s = New(5010);
        var text = E.DebugEliteGo(s, "legacy_elite_z_relay_guard");
        Assert.That(text, Does.Contain("废中继营").And.Contain("天线守卫"));
        Assert.That(E.EliteChallengesHere(s).Single().EnemyId, Is.EqualTo("z_relay_guard"));
        Assert.That(E.DebugPopulationText(s), Does.Contain("ex_pop_deep_wild").And.Contain("（已进入）").And.Contain("新手8点"));
        E.DebugMapTeleport(s, "d07_06");
        Assert.That(E.FixedEnemiesAt(s, "d07_06"), Is.Not.Empty);
        Assert.That(E.DebugWinFixedHere(s), Does.Contain("守卫已击败（本代）"));
        Assert.That(E.InstanceFact(s, "d07", "guardianDefeated"), Is.True);
        Assert.That(E.DebugWinFixedHere(s), Does.Contain("已结清"));
    }
}
