using System.Reflection;
using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>开发计划 v0.9 B20.6 试玩修复（M390）：线上试玩（构建 db3083b）报的保护相邻、门槛文案、无咬击敌人感染提示、告示时机、鼓动显示、开发入口提示。</summary>
public class B20PlaytestFixTests : BattleTestBase
{
    private void EnemyAct(GameState s, BattleEnemy e) =>
        typeof(GameEngine).GetMethod("EnemyActivation", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(E, new object[] { s, e, false });

    private void Lock(GameState s, BattleEnemy e) =>
        typeof(GameEngine).GetMethod("LockIntent", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(E, new object[] { s, s.Battle!, e });

    /// <summary>
    /// 1：货架护卫感染者“保护相邻同伴1次”（A09 E3）——锁定时护的同伴被换位挤到不相邻，执行时改护当时相邻的同伴；
    /// 身边没有相邻同伴则这次保护落空，不护不相邻的人。
    /// </summary>
    [Test]
    public void Fix1_Protect_OnlyAdjacentAtExecution()
    {
        var s = Fight(6001, "ec_stockbracer", "ec_labwalker", "z_darter");
        var st = Enemy(s, 1); var lab = Enemy(s, 2); var dart = Enemy(s, 3);
        st.Slot = "2"; lab.Slot = "3"; dart.Slot = "4";
        st.JoinRound = s.Battle!.Round; st.Acted = false; st.RoundApUsed = 0;
        Lock(s, st);
        Assert.That((st.Intent, st.IntentTarget), Is.EqualTo(("protect", lab.Index)), "锁定时护后排相邻的试制区感染者");
        // 迅捷感染者前移与试制区交换：试制区到 4 号位，不再相邻
        (lab.Slot, dart.Slot) = ("4", "3");
        Assert.That(E.IntentShown(s.Battle, st, s), Does.Not.Contain("试制区感染者"));
        EnemyAct(s, st);
        Assert.That(st.ProtectingIndex, Is.EqualTo(dart.Index), "改护当时相邻的迅捷感染者");
        Assert.That(s.Battle.Log.Last(), Does.Contain("护住迅捷感染者"));

        // 身边一个相邻同伴也没有：保护落空
        var t = Fight(6002, "ec_stockbracer", "ec_labwalker");
        var a = Enemy(t, 1); var b = Enemy(t, 2);
        a.Slot = "1"; b.Slot = "2";
        a.JoinRound = t.Battle!.Round; a.Acted = false; a.RoundApUsed = 0;
        Lock(t, a);
        Assert.That(a.Intent, Is.EqualTo("protect"));
        b.Slot = "4";
        EnemyAct(t, a);
        Assert.That(a.ProtectingIndex, Is.EqualTo(0));
        Assert.That(t.Battle.Log.Last(), Does.Contain("保护落空"));
    }

    /// <summary>2：检查哨拦路战与地牢守卫战已上线，未开通原因不再写“后续版本开放”。</summary>
    [Test]
    public void Fix2_GateReasons_NoLongerSayLater()
    {
        var s = New(6003);
        E.DebugMapTeleport(s, "m02_checkpoint");
        E.DebugSetProgress(s, "m02_checkpoint", 100);
        var why = E.AdjacentLocked(s).Single(x => x.Target == "h01_gate").Reason;
        Assert.That(why, Does.Contain("解决检查哨的固定拦路战").And.Not.Contain("后续版本"));
        Assert.That(Cfg.Map.FactLabels["guardianDefeated"], Does.Not.Contain("后续版本"));
        Assert.That(Cfg.Map.FactLabels["checkpoint_obstruction_cleared"], Does.Not.Contain("后续版本"));
    }

    /// <summary>3：没有咬击动作的丧尸（新守卫、低吟、试制区、膨胀等）不算“可能咬伤”；明写咬击的照旧。</summary>
    [Test]
    public void Fix3_CanBite_OnlyExplicitBiters()
    {
        foreach (var id in new[] { "ec_boss_lab", "ec_boss_crypt", "ec_cantor", "ec_labwalker", "z_bloater", "z_pump_foreman" })
            Assert.That(E.CanBite(E.Enemy(id)), Is.False, id);
        foreach (var id in new[] { "shambler", "ec_cablecrawler", "ec_stockbracer", "z_isolation_guard" })
            Assert.That(E.CanBite(E.Enemy(id)), Is.True, id);
        Assert.That(E.CanBite(E.Enemy("wolf")), Is.False, "动物不传播病毒");
    }

    /// <summary>4：人已在探索 100% 的挑战地点时记下营区登记，告示卡出现的同时写日志（只写一次）。</summary>
    [Test]
    public void Fix4_EliteNotice_WhenRegisteredOnTheSpot()
    {
        var s = New(6004);
        E.DebugMapTeleport(s, "w33_waterworks");
        E.DebugSetProgress(s, "w33_waterworks", 100);
        Assert.That(E.EliteChallengesHere(s), Is.Empty);
        E.DebugCapacityFact(s, "hub_registered");
        Assert.That(E.EliteChallengesHere(s), Has.Count.EqualTo(1));
        Assert.That(s.Log.Count(l => l.Text.StartsWith("可选强敌告示：荒废水厂")), Is.EqualTo(1));
        E.UpdateFlags(s);
        Assert.That(s.Log.Count(l => l.Text.StartsWith("可选强敌告示：荒废水厂")), Is.EqualTo(1), "只写一次");
    }

    /// <summary>6/7：固定战撤退后写“已交过手”；被鼓动的敌人意图伤害范围乘上鼓动并标注。</summary>
    [Test]
    public void Fix6_7_HurtNote_RallyShownInIntent()
    {
        var s = New(6005);
        UnlockCapacity(s, 2);
        E.DebugMapTeleport(s, "d01_07");
        Assert.That(E.FixedHurtNote(s, "d01_07"), Is.Null);
        E.FixedEnemiesAt(s, "d01_07")[0].Hp = 7.2;
        Assert.That(E.FixedHurtNote(s, "d01_07"), Is.EqualTo("已交过手：封锁祭室守卫 健康7.20/10.50"));

        var t = Fight(6006, "z_slow");
        var z = Enemy(t, 1);
        z.Slot = "1";
        StandAt(t, 1);
        z.Intent = "attack"; z.IntentStep = 0;
        z.RallyFactor = 1.1;
        var cfg = E.Enemy("z_slow");
        Assert.That(E.IntentShown(t.Battle!, z, t), Does.Contain($"{cfg.DamageMin * 1.1:0.00}—{cfg.DamageMax * 1.1:0.00}").And.Contain("含鼓动×1.1"));
    }

    /// <summary>8：开发入口——人口区刷新没出时写原因（第2日00:00之前不判）；已结算的可选强敌再点写“已结算，本局不再出现”。</summary>
    [Test]
    public void Fix8_DevTexts()
    {
        var s = New(6007);
        Assert.That(E.RegionSpawnIdleReason(s), Does.Contain("还没有进入任何人口区"));
        E.DebugMapTeleport(s, "w11_orchard");
        Assert.That(E.RegionSpawnIdleReason(s), Does.Contain("第2日00:00起"));
        s.Population.Elites["legacy_elite_z_guard"] = new LegacyEliteState { Id = "legacy_elite_z_guard", Settled = true };
        Assert.That(E.DebugEliteGo(s, "legacy_elite_z_guard"), Does.Contain("已结算，本局不再出现"));
    }
}
