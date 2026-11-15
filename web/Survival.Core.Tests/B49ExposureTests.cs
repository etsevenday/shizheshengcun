using System.Reflection;
using Survival.Core.Config;
using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 批次 B49 暴露内核（R04 文首 Q652「传播类型、概率和感染量」「命中、承伤、生命态与随机顺序」与 B49 Q739、Q747）：
/// 五类概率与首次／再次量、u=p 失败、封顶与计时；未中／全挡不抽；保护只判承伤者；群体逐人；濒死仍活照判、确死不判；分支选型；喷溅三态拼接。
/// </summary>
public class B49ExposureTests : BattleTestBase
{
    protected override bool NoEnemyApproach => true;

    private const string Lin = "npc_lin_zhao";

    private ExposureProfileConfig P(string id) => E.ExposureProfile(id)!;

    private void Strike(GameState s, BattleEnemy e, EnemyIntentStepConfig st, bool plain = false, double factor = 1, bool pursuit = false, bool approach = false) =>
        typeof(GameEngine).GetMethod("DoAttackStep", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(E, new object[] { s, s.Battle!, e, st, plain, factor, pursuit, approach });

    private void Splash(GameState s, BattleEnemy e) =>
        typeof(GameEngine).GetMethod("DeathSplash", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(E, new object[] { s, s.Battle!, e, Cfg.Enemies.Single(x => x.Id == e.EnemyId) });

    private static uint After(uint state, int draws)
    {
        var r = new Rng { State = state };
        for (var i = 0; i < draws; i++) r.NextDouble();
        return r.State;
    }

    private static EnemyIntentStepConfig CycleStep(string enemyId, string kind) =>
        AnySlot(Cfg.Enemies.Single(x => x.Id == enemyId).Intents.Cycle.First(x => x.Kind == kind));

    private static IEnumerable<string> AllLogs(GameState s) => s.Log.Select(l => l.Text).Concat(s.Battle?.Log ?? new List<string>());

    /// <summary>两人队在农野荒地对给定敌人入战，号位：你 1 号位、林昭 2 号位。</summary>
    private GameState Duo(uint seed, string enemy)
    {
        var s = New(seed);
        E.DebugQuickParty(s, 2);
        E.DebugTeleport(s, "field");
        s.GameMinute = At(2, 9, 0);
        s.SpawnNextCheck = int.MaxValue;
        s.PatrolNextCheck = int.MaxValue;
        s.WorldEnemies.Clear();
        var id = E.DebugSpawnEnemy(s, enemy, "field").Id;
        Assert.That(E.DebugStartBattle(s, new[] { id }, GameEngine.FearEncounter), Is.True);
        s.Battle!.Allies = new List<string> { GameState.PlayerId, Lin };
        return s;
    }

    // ───────────── Q652-A02 ─────────────

    /// <summary>Q652-A02：I0／40／70／100 各类概率＝clamp(p0×倍率)；R04 例 I70：咬伤21.5%、抓伤10.75%、污染器械7.525%、变异体液16.125%、冲撞擦伤5.375%。</summary>
    [Test]
    public void Q652_A02_Probability_PerType_Boundaries()
    {
        var factors = new Dictionary<string, double> { ["bite"] = 1, ["scratch"] = 0.5, ["tainted_weapon"] = 0.35, ["mutant_fluid"] = 0.75, ["impact_abrasion"] = 0.25 };
        foreach (var i in new[] { 0.0, 40, 70, 100 })
            foreach (var (id, f) in factors)
                Assert.That(E.ExposureChance(i, P(id)), Is.EqualTo((0.05 + 0.55 * (1 - i / 100)) * f).Within(1e-12), $"I{i} {id}");
        Assert.That(new[] { "bite", "scratch", "tainted_weapon", "mutant_fluid", "impact_abrasion" }.Select(id => E.ExposureChance(70, P(id))),
            Is.EqualTo(new[] { 0.215, 0.1075, 0.07525, 0.16125, 0.05375 }).Within(1e-12));
        Assert.That(E.ExposureChance(-20), Is.EqualTo(0.6).Within(1e-12), "I 先夹到 0—100");
        Assert.That(E.ExposureChance(130), Is.EqualTo(0.05).Within(1e-12));
        Assert.That(Fmt.Pct(E.ExposureChance(70, P("tainted_weapon"))), Is.EqualTo("7.53%"), "显示沿原精度，判定用未舍入值");
    }

    /// <summary>Q652-A02：u=p 判失败（u&lt;p 才成功）。</summary>
    [Test]
    public void Q652_A02_EqualRoll_Fails()
    {
        var s = New(49001);
        s.Body.Immunity = 0;
        for (uint st = 1; st < 200000; st++)
        {
            var u = new Rng { State = st }.NextDouble();
            if (u <= 0.01 || u >= 0.59) continue;
            var prof = new ExposureProfileConfig { Id = "t", Name = "测试", Factor = u / E.ExposureChance(0), FirstLoad = 10, RepeatDelta = 5 };
            if (E.ExposureChance(0, prof) != u) continue;
            s.Rng.State = st;
            Assert.That(E.ExposeToVirus(s, prof).Success, Is.False, "u 等于 p 失败");
            Assert.That(s.Body.Infected, Is.False);
            return;
        }
        Assert.Fail("没找到 u 恰等于 p 的随机状态");
    }

    /// <summary>Q652-A02：首次量按类型；再次按封顶后实际增量；成功重置连续高免疫计时，失败不加 V、不重置；三态结果句逐字（逐字稿 B49 firstResult／repeatResult／failedResult）。</summary>
    [Test]
    public void Q652_A02_FirstRepeatCap_TimerReset_ResultText()
    {
        foreach (var (id, first) in new[] { ("bite", 20.0), ("scratch", 10.0), ("tainted_weapon", 8.0), ("mutant_fluid", 15.0), ("impact_abrasion", 5.0) })
        {
            var s = New(49002);
            s.Body.Immunity = 0;
            s.Body.HighImmunityMinutes = 300;
            Force(s, Below(0.001));
            var r = E.ExposeToVirus(s, P(id));
            Assert.That((r.Success, s.Body.Infected, s.Body.VirusLoad, s.Body.HighImmunityMinutes), Is.EqualTo((true, true, first, 0)), id);
            Assert.That(E.ExposureResultText(s, r), Is.EqualTo($"你因{P(id).Name}感染，病毒量{Fmt.T(first)}（判定概率{Fmt.Pct(r.Chance)}）"));
        }
        var c = New(49003);
        c.Body.Immunity = 70;
        c.Body.Infected = true;
        c.Body.VirusLoad = 97.5;
        c.Body.HighImmunityMinutes = 100;
        Force(c, Below(0.001));
        var rr = E.ExposeToVirus(c, P("scratch"));
        Assert.That((c.Body.VirusLoad, rr.Gain, c.Body.HighImmunityMinutes), Is.EqualTo((100.0, 2.5, 0)), "封顶 100，实际增量 2.5");
        Assert.That(E.ExposureResultText(c, rr), Is.EqualTo("你因抓伤／刮伤再次受到感染暴露，病毒+2.5（判定概率10.75%）"));
        var f = New(49004);
        f.Body.Immunity = 70;
        f.Body.Infected = true;
        f.Body.VirusLoad = 30;
        f.Body.HighImmunityMinutes = 100;
        Force(f, AtLeast(0.999));
        var fr = E.ExposeToVirus(f, P("impact_abrasion"));
        Assert.That((fr.Success, f.Body.VirusLoad, f.Body.HighImmunityMinutes), Is.EqualTo((false, 30.0, 100)), "失败不加 V、不重置计时");
        Assert.That(E.ExposureResultText(f, fr), Is.EqualTo("你本次冲撞擦伤未增加感染（判定概率5.38%）"));
    }

    // ───────────── Q652-A03／Q739-A02 ─────────────

    /// <summary>Q652-A03／Q739-A02：未中不抽暴露随机；正伤命中才多一次；抓击命中后原扯腿控制仍判且在暴露之后。</summary>
    [Test]
    public void Q652_A03_Miss_NoRoll_ControlKept()
    {
        var s = Fight(49010, "crawler");
        var e = Enemy(s, 1);
        e.Intent = "attack";
        e.IntentStep = 0;
        var st = CycleStep("crawler", "attack");
        Force(s, Draw(), AtLeast(0.999));
        var r0 = s.Rng.State;
        Strike(s, e, st);
        Assert.That(s.Rng.State, Is.EqualTo(After(r0, 2)), "未中：只抽目标与命中");
        Assert.That(s.Battle!.Log.Last(), Does.Contain("落空").And.Not.Contain("判定概率"));

        var t = Fight(49011, "crawler");
        var te = Enemy(t, 1);
        te.Intent = "attack";
        te.IntentStep = 0;
        t.Body.Immunity = 100;
        Force(t, Draw(), Below(0.01), Any(), AtLeast(0.999), Below(0.01));
        var t0 = t.Rng.State;
        Strike(t, te, st);
        Assert.That(t.Rng.State, Is.EqualTo(After(t0, 5)), "目标、命中、抽值、抓伤暴露、控制各一次");
        var log = t.Battle!.Log.Last();
        Assert.That(log, Does.Contain("你本次抓伤／刮伤未增加感染（判定概率2.5%）").And.Contain("你被扯腿"), "暴露在原咬伤位置，控制分支保留");
        Assert.That(log.IndexOf("未增加感染"), Is.LessThan(log.IndexOf("你被扯腿")));
    }

    /// <summary>Q652-A03：完全挡住（D=0）不抽暴露随机、不写感染。</summary>
    [Test]
    public void Q652_A03_FullBlock_NoRoll()
    {
        var s = Fight(49012, "shambler");
        var e = Enemy(s, 1);
        e.Intent = "attack";
        e.IntentStep = 0;
        s.Battle!.PlayerNextDamageFactor = 0.5;
        s.Battle.PlayerGuard = true;
        s.Battle.PlayerGuardFactor = 0;
        s.Battle.PlayerGuardCharges = 1;
        Force(s, Draw(), Below(0.01), Any());
        var r0 = s.Rng.State;
        Strike(s, e, CycleStep("shambler", "attack"));
        Assert.That(s.Battle.Log.Last(), Does.Contain("被完全挡住").And.Not.Contain("判定概率"));
        Assert.That(s.Rng.State, Is.EqualTo(After(r0, 3)), "目标、命中、抽值；无暴露随机");
    }

    /// <summary>Q652-A03：保护转移后只对实际承伤的保护者按其本人 I 判；原被保护者不判。</summary>
    [Test]
    public void Q652_A03_Protection_OnlyBearer()
    {
        var s = Duo(49013, "shambler");
        var b = s.Battle!;
        var e = Enemy(s, 1);
        e.Intent = "attack";
        e.IntentStep = 0;
        b.Protections[Lin] = GameState.PlayerId;   // 林昭护着你
        s.Actors[Lin].Body.Immunity = 0;
        s.Actors[GameState.PlayerId].Body.Immunity = 0;
        Force(s, Below(0.49), Below(0.01), Any(), Below(0.001));
        Strike(s, e, CycleStep("shambler", "attack"));
        Assert.That(s.Actors[Lin].Body.Infected, Is.True, "保护者承伤并按本人判定");
        Assert.That(s.Actors[GameState.PlayerId].Body.Infected, Is.False, "原被保护者不判");
        Assert.That(b.Log.Last(), Does.Contain("林昭因抓伤／刮伤感染，病毒量10（判定概率30%）"));
    }

    /// <summary>Q652-A03：扫击逐真实承伤者各判一次、各用本人 I，不复用第一人的结果。</summary>
    [Test]
    public void Q652_A03_Sweep_EachTargetOwnRoll()
    {
        var s = Duo(49014, "z_relay_guard");
        var b = s.Battle!;
        var e = Enemy(s, 1);
        e.Intent = "sweep";
        e.IntentStep = 0;
        s.Actors[GameState.PlayerId].Body.Immunity = 0;
        s.Actors[Lin].Body.Immunity = 100;
        Force(s, Below(0.01), Any(), Below(0.001), Below(0.01), Any(), Below(0.03));
        Strike(s, e, CycleStep("z_relay_guard", "sweep"));
        Assert.That(s.Actors[GameState.PlayerId].Body.VirusLoad, Is.EqualTo(8), "你：污染器械伤首次 8");
        Assert.That(s.Actors[Lin].Body.Infected, Is.False, "林昭 I100：p=1.75%，u≥p 失败");
        Assert.That(b.Log.Any(l => l.Contains("林昭本次污染器械伤未增加感染（判定概率1.75%）")), Is.True);
    }

    // ───────────── Q652-A04 ─────────────

    /// <summary>Q652-A04：濒死 H0 受正伤、致死抵抗成功仍活 → 照判暴露；确死不判、不取随机。</summary>
    [Test]
    public void Q652_A04_DyingAlive_Exposed_DeadNot()
    {
        var s = Fight(49015, "shambler");
        var e = Enemy(s, 1);
        e.Intent = "attack";
        e.IntentStep = 0;
        s.Stats.Health = 0;
        s.Body.Immunity = 0;
        E.TestDeathRolls = new Queue<double>(new[] { 0.001 });
        Force(s, Draw(), Below(0.01), Any(), Below(0.001));
        Strike(s, e, CycleStep("shambler", "attack"));
        Assert.That((s.Player.Dead, s.Stats.Health, s.Body.Infected, s.Body.VirusLoad), Is.EqualTo((false, 0.0, true, 10.0)), "H0→0 但 D>0 仍活：抓伤首次 10");

        var d = Fight(49016, "shambler");
        var de = Enemy(d, 1);
        de.Intent = "attack";
        de.IntentStep = 0;
        d.Stats.Health = 0;
        E.TestDeathRolls = new Queue<double>(new[] { 0.999 });
        Force(d, Draw(), Below(0.01), Any(), Below(0.001));
        var d0 = d.Rng.State;
        Strike(d, de, CycleStep("shambler", "attack"));
        Assert.That((d.Progress.Dead, d.Body.Infected), Is.EqualTo((true, false)), "确死：不再抽感染");
        Assert.That(d.Rng.State, Is.EqualTo(After(d0, 3)), "目标、命中、抽值；无暴露随机");
    }

    // ───────────── Q739 分支选型 ─────────────

    /// <summary>Q739-A01：同一锁定步骤按执行分支取类型——正常执行／前移后够到（plain）取步骤类型，追击、后排降格弱攻击取默认。</summary>
    [TestCase("z_hauler", "charge", false, 1.0, false, "冲撞擦伤")]
    [TestCase("z_hauler", "charge", true, 1.0, false, "冲撞擦伤")]
    [TestCase("z_hauler", "charge", true, 1.0, true, "抓伤／刮伤")]
    [TestCase("z_power_guard", "charge", true, 1.0, false, "冲撞擦伤")]
    [TestCase("z_power_guard", "charge", true, 0.7, false, "抓伤／刮伤")]
    [TestCase("ec_cablecrawler", "pull", true, 1.0, false, "污染器械伤")]
    [TestCase("z_pump_foreman", "charge", true, 1.0, false, "污染器械伤")]
    [TestCase("ec_boss_water", "charge", true, 1.0, false, "冲撞擦伤")]
    [TestCase("z_spitter", "spit", false, 1.0, false, "变异体液")]
    public void Q739_A01_BranchSelectsProfile(string enemy, string kind, bool plain, double factor, bool pursuit, string expect)
    {
        var s = Fight(49020, enemy);
        var e = Enemy(s, 1);
        var cfg = Cfg.Enemies.Single(x => x.Id == enemy);
        var all = cfg.Intents.Opening.Concat(cfg.Intents.Cycle).ToList();
        e.Intent = kind;
        e.IntentStep = all.FindIndex(x => x.Kind == kind);
        s.Body.Immunity = 0;
        Force(s, Draw(), Below(0.01), Any(), AtLeast(0.9), AtLeast(0.9), AtLeast(0.9));
        Strike(s, e, AnySlot(all[e.IntentStep]), plain, factor, pursuit);
        Assert.That(s.Battle!.Log.Any(l => l.Contains($"你本次{expect}未增加感染")), Is.True, string.Join(" | ", s.Battle.Log.TakeLast(3)));
    }

    /// <summary>Q739-A01：迫近弱近袭取默认（配电守卫默认抓伤／刮伤）；真实咬击取 bite 且只判一次（不叠抓伤）。</summary>
    [Test]
    public void Q739_A01_Approach_Default_Bite_Once()
    {
        var s = Fight(49021, "z_power_guard");
        var e = Enemy(s, 1);
        s.Body.Immunity = 0;
        Force(s, Draw(), Below(0.01), Any(), AtLeast(0.999));
        Strike(s, e, new EnemyIntentStepConfig { Kind = "attack", Verb = "弱近袭", TargetSlots = "1234" }, true, 0.7, false, true);
        Assert.That(s.Battle!.Log.Last(), Does.Contain("你本次抓伤／刮伤未增加感染"));

        var b = Fight(49022, "shambler");
        var z = Enemy(b, 1);
        z.Intent = "bite";
        z.IntentStep = 0;
        b.Body.Immunity = 0;
        Force(b, Draw(), Below(0.01), Any(), AtLeast(0.999));
        var r0 = b.Rng.State;
        Strike(b, z, CycleStep("shambler", "attack"));
        Assert.That(b.Battle!.Log.Last(), Does.Contain("咬击命中").And.Contain("你本次咬伤未增加感染（判定概率60%）"));
        Assert.That(b.Battle.Log.Last(), Does.Not.Contain("抓伤"));
        Assert.That(b.Rng.State, Is.EqualTo(After(r0, 4)), "咬击只判一次");
    }

    /// <summary>Q739-A02：非伤害步骤与 0 伤拉扯不抽感染；攀附感染者拉扯（0 伤）只判控制。</summary>
    [Test]
    public void Q739_A02_ZeroDamagePull_NoExposure()
    {
        var s = Fight(49023, "z_clinger");
        var e = Enemy(s, 1);
        e.Intent = "pull";
        e.IntentStep = 0;
        StandAt(s, 2);
        Force(s, Draw(), Below(0.01), Any(), AtLeast(0.999));
        Strike(s, e, CycleStep("z_clinger", "pull"));
        Assert.That(s.Battle!.Log.Last(), Does.Contain("不造成伤害").And.Not.Contain("判定概率"));
        Assert.That(s.Body.Infected, Is.False);
    }

    // ───────────── Q747 喷溅 ─────────────

    /// <summary>Q747-A01：合法喷溅正伤用逗号接本次真实三态结果，一个句号；Q747-A02：喷溅致死者无子句、不取随机；濒死 H0 仍活照判。</summary>
    [Test]
    public void Q747_SplashSentence_ThreeStates()
    {
        var s = Fight(49030, "z_bloater");
        var e = Enemy(s, 1);
        s.Body.Immunity = 0;
        Force(s, Below(0.001));
        Splash(s, e);
        Assert.That(s.Battle!.Log.Last(), Does.EndWith("膨胀感染者死亡喷溅：你健康−0.30，你因变异体液感染，病毒量15（判定概率45%）。"));
        Force(s, Below(0.001));
        Splash(s, e);
        Assert.That(s.Battle.Log.Last(), Does.EndWith("膨胀感染者死亡喷溅：你健康−0.30，你因变异体液再次受到感染暴露，病毒+8（判定概率45%）。"));
        Force(s, AtLeast(0.999));
        Splash(s, e);
        Assert.That(s.Battle.Log.Last(), Does.EndWith("膨胀感染者死亡喷溅：你健康−0.30，你本次变异体液未增加感染（判定概率45%）。"));

        var d = Fight(49031, "z_bloater");
        d.Stats.Health = 0;
        E.TestDeathRolls = new Queue<double>(new[] { 0.999 });
        var r0 = d.Rng.State;
        Splash(d, Enemy(d, 1));
        Assert.That(AllLogs(d).Any(l => l.EndsWith("膨胀感染者死亡喷溅：你健康−0.30。")), Is.True, "确死：只写伤害句");
        Assert.That((d.Body.Infected, d.Rng.State), Is.EqualTo((false, r0)), "不取感染随机");

        var a = Fight(49032, "z_bloater");
        a.Stats.Health = 0;
        a.Body.Immunity = 0;
        E.TestDeathRolls = new Queue<double>(new[] { 0.001 });
        Force(a, Below(0.001));
        Splash(a, Enemy(a, 1));
        Assert.That((a.Player.Dead, a.Body.Infected, a.Body.VirusLoad), Is.EqualTo((false, true, 15.0)), "濒死 H0 正伤仍活照判");
    }

    /// <summary>Q747-A03：主动攻击直接击杀才喷溅（门槛沿 B47a2 Q679），正伤后接本次真实感染结果。</summary>
    [Test]
    public void Q747_A03_ActiveKill_SplashWithExposure()
    {
        var s = Fight(49033, "z_bloater");
        PlayerFirst(s);
        E.BattleStep(s);
        Assert.That(E.AwaitingPlayer(s), Is.True);
        Enemy(s, 1).Hp = 0.01;
        s.Body.Immunity = 0;
        Force(s, Below(0.01), AtLeast(0.99), Below(0.4), Below(0.4), Below(0.4));
        Assert.That(Cmd(s, "basic_attack", 1).Ok, Is.True);
        Assert.That(AllLogs(s).Any(l => l.Contains("膨胀感染者死亡喷溅：你健康−0.30，你因变异体液感染，病毒量15（判定概率45%）。")), Is.True,
            string.Join(" | ", AllLogs(s).TakeLast(4)));
    }
}
