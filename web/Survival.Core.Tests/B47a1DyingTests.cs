using System.Reflection;
using Newtonsoft.Json.Linq;
using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// B47a1 濒死生命态、统一受伤、世界段、战斗与医疗脱离（R09 濒死、意志与致死判定系统；dying-willpower-q616.json；开发记录 B47a1）。
/// 意志经验结账（Q676—Q677）、进入濒死暂停（Q673）与预估截断（Q674—Q675）属 B47a2／B47a3，不在这里。
/// </summary>
public class B47a1DyingTests : BattleTestBase
{
    protected override bool NoEnemyApproach => true;

    /// <summary>找一个种子，使从它开始的随机序列依次满足条件（同 B22hTests.ForceRng）。</summary>
    private static void ForceRng(GameState s, params Func<double, bool>[] next)
    {
        for (uint c = 1; c < 2_000_000; c++)
        {
            var r = new Rng { State = c * 2654435761u + 7 };
            if (next.All(p => p(r.NextDouble()))) { s.Rng.State = c * 2654435761u + 7; return; }
        }
        Assert.Fail("找不到满足条件的随机种子");
    }

    private static string AsSchema51(GameState s)
    {
        var o = JObject.Parse(SaveSerializer.Serialize(s));
        o["SchemaVersion"] = "51";
        foreach (var kv in (JObject)o["Actors"]!)
        {
            var a = (JObject)kv.Value!;
            foreach (var f in new[] { "Dead", "DyingNotified", "WillpowerEnemyAwards", "WillpowerXpDay", "WillpowerXpToday" }) a.Remove(f);
            ((JObject)a["Skills"]!).Remove("willpower");
        }
        return o.ToString();
    }

    // ───────────── M1 生命态、意志熟练度与存档 52 ─────────────

    [Test]
    public void M1_Config_WillpowerSkillAndParams()
    {
        var sk = Cfg.Skills.Single(k => k.Id == GameEngine.Willpower);
        Assert.That((sk.Name, sk.Category, sk.InitialLevel, sk.LevelCap, sk.TrackOnly), Is.EqualTo(("意志", "survival", 1, (int?)10, true)), "R09 §4：第 17 门被动熟练度，成长组“求生与行动”");
        Assert.That(Cfg.Skills.Select(k => k.Id).ToList().IndexOf("willpower"), Is.EqualTo(Cfg.Skills.Select(k => k.Id).ToList().IndexOf("stealth") + 1), "排在潜行之后");
        var d = Cfg.Dying;
        Assert.That((d.ResistanceBasePercent, d.ResistancePerLevelPercent, d.WillpowerXpPerEnemyAction, d.WillpowerMaxAwardsPerActorPerEnemy, d.WillpowerMaxXpPerActorPerDay),
            Is.EqualTo((65, 2, 10, 3, 60)), "Q616 合同 willpower");
        Assert.That(Enumerable.Range(1, 10).Select(E.ResistancePercent), Is.EqualTo(new[] { 65, 67, 69, 71, 73, 75, 77, 79, 81, 83 }));
        Assert.That(E.TotalXpToCap(GameEngine.Willpower), Is.EqualTo(2700));
    }

    [Test]
    public void M1_NewGame_Willpower1_SeventeenSkills_LifeStates()
    {
        var s = New();
        Assert.That((s.Skills[GameEngine.Willpower].Level, s.Skills[GameEngine.Willpower].Xp), Is.EqualTo((1, 0L)));
        Assert.That(E.SkillViews(s), Has.Count.EqualTo(17));
        var p = s.Player;
        Assert.That(E.LifeOf(s, p), Is.EqualTo(LifeState.Normal));
        p.Stats.Health = 0;
        Assert.That((E.LifeOf(s, p), E.IsDying(s, p), E.IsDead(s, p)), Is.EqualTo((LifeState.Dying, true, false)), "H0 未确死即濒死");
        p.Stats.Health = 1e-6;
        Assert.That(E.LifeOf(s, p), Is.EqualTo(LifeState.Normal), "内部>0 即正常，不按显示取整");
        p.Dead = true;
        Assert.That(E.LifeOf(s, p), Is.EqualTo(LifeState.Dead));
    }

    [Test]
    public void M1_Migration51To52_DyingFromH0_DeadFromRoster_WillpowerForLivingOnly()
    {
        var s = New();
        E.DebugQuickParty(s, 3);
        var mates = s.Party.Where(id => id != GameState.PlayerId).ToList();
        var dead = mates[0];
        var alive = mates[1];
        E.DebugActor(s, dead, "kill");
        Assert.That(s.Actors[dead].Dead, Is.True, "CompanionDown 置确死");
        s.Actors[alive].Stats.Health = 0;
        s.Player.Stats.Health = 0;
        s.Actors[alive].Skills[GameEngine.Willpower] = new SkillState { Level = 4, Xp = 30 };
        var rng = s.Rng.State;
        var log = s.Log.Count;
        var json = AsSchema51(s);
        Assert.That(json, Does.Not.Contain("willpower").And.Not.Contain("DyingNotified"));
        // 存活者的意志补上：AsSchema51 删掉了所有人的意志，这里把“已有合法意志不覆盖”单独放回一个
        var o = JObject.Parse(json);
        ((JObject)o["Actors"]![alive]!["Skills"]!)["willpower"] = JObject.FromObject(new { Level = 4, Xp = 30 });
        var t = SaveSerializer.Deserialize(o.ToString(), Cfg);
        Assert.That(t.SchemaVersion, Is.EqualTo("54"));
        Assert.That((t.Actors[dead].Dead, t.Actors[dead].Skills.ContainsKey(GameEngine.Willpower)), Is.EqualTo((true, false)), "旧死者不复活、不补意志");
        Assert.That((E.LifeOf(t, t.Actors[alive]), t.Actors[alive].DyingNotified, t.Actors[alive].Skills[GameEngine.Willpower].Level, t.Actors[alive].Skills[GameEngine.Willpower].Xp),
            Is.EqualTo((LifeState.Dying, true, 4, 30L)), "旧 H0 活人归濒死（提醒锁置已提醒），已有意志不覆盖");
        var p = t.Actors[GameState.PlayerId];
        Assert.That((E.LifeOf(t, p), p.DyingNotified, p.Skills[GameEngine.Willpower].Level, p.Skills[GameEngine.Willpower].Xp), Is.EqualTo((LifeState.Dying, true, 1, 0L)), "缺意志补 1 级 0 经验");
        Assert.That((t.Rng.State, t.Log.Count), Is.EqualTo((rng, log)), "不抽随机、不写日志");
        var once = SaveSerializer.Serialize(t);
        Assert.That(SaveSerializer.Serialize(SaveSerializer.Deserialize(once, Cfg)), Is.EqualTo(once), "幂等");
    }

    [Test]
    public void M1_Migration51To52_OldPlayerDeath_StaysDead()
    {
        var s = New();
        s.Progress.Dead = true;
        s.Progress.Death = new DeathInfo { Cause = "伤势过重", Minute = s.GameMinute, LocationId = s.CurrentLocation };
        s.Player.Stats.Health = 0;
        var t = SaveSerializer.Deserialize(AsSchema51(s), Cfg);
        Assert.That((t.Progress.Dead, t.Player.Dead, E.LifeOf(t, t.Player)), Is.EqualTo((true, true, LifeState.Dead)), "旧终局保持");
        Assert.That(t.Player.Skills.ContainsKey(GameEngine.Willpower), Is.False);
    }

    // ───────────── M2 统一受伤入口 ─────────────

    /// <summary>Q616-A01：正常 H0.3 受 D10 → H0 濒死，本击不抽致死；写进入濒死句；仍可行动。</summary>
    [Test]
    public void M2_A01_NormalToZero_EntersDying_NoRoll()
    {
        var s = New();
        s.Player.Stats.Health = 0.3;
        var rng = s.Rng.State;
        Assert.That(E.DamageActor(s, s.Player, 10), Is.EqualTo(DamageOutcome.EnteredDying));
        Assert.That((s.Player.Stats.Health, s.Rng.State, s.Progress.Dead, s.Player.Dead, s.Player.DyingNotified), Is.EqualTo((0.0, rng, false, false, true)), "没有过量穿透、不抽随机");
        Assert.That(s.Log.Last().Text, Is.EqualTo("你进入濒死状态。必须用药或医疗实际恢复健康才能脱离。"));
        s.Player.Stats.Health = 5;
        Assert.That(E.DamageActor(s, s.Player, 1), Is.EqualTo(DamageOutcome.Hurt));
        Assert.That(s.Rng.State, Is.EqualTo(rng));
    }

    /// <summary>Q616-A02：意志 1 时 u=0.649999 抵抗、0.65 失败；意志 10 阈值 0.83；0 伤不抽，正但 &lt;0.01 仍抽。</summary>
    [Test]
    public void M2_A02_ResistanceThresholds_ZeroDamageNoRoll_TinyDamageRolls()
    {
        Assert.That((E.ResistsDeath(1, 0.649999), E.ResistsDeath(1, 0.65)), Is.EqualTo((true, false)));
        Assert.That((E.ResistsDeath(5, 0.7299999), E.ResistsDeath(5, 0.73)), Is.EqualTo((true, false)));
        Assert.That((E.ResistsDeath(10, 0.8299999), E.ResistsDeath(10, 0.83)), Is.EqualTo((true, false)), "83% 边界不是 0.8300000000000001");
        var s = New();
        s.Player.Stats.Health = 0;
        var rng = s.Rng.State;
        Assert.That(E.DamageActor(s, s.Player, 0), Is.EqualTo(DamageOutcome.None));
        Assert.That(E.DamageActor(s, s.Player, -1), Is.EqualTo(DamageOutcome.None));
        Assert.That(s.Rng.State, Is.EqualTo(rng), "0 伤不抽");
        ForceRng(s, u => u < 0.65);
        rng = s.Rng.State;
        Assert.That(E.DamageActor(s, s.Player, 0.004), Is.EqualTo(DamageOutcome.Resisted), "正但 <0.01 的伤害仍判");
        Assert.That(s.Rng.State, Is.Not.EqualTo(rng), "恰好抽一次");
        Assert.That((s.Player.Stats.Health, s.Player.Dead), Is.EqualTo((0.0, false)));
        Assert.That(s.Log.Last().Text, Is.EqualTo("你抵抗了致死，仍处于濒死状态。"));
        ForceRng(s, u => u >= 0.65);
        Assert.That(E.DamageActor(s, s.Player, 0.004), Is.EqualTo(DamageOutcome.Died));
        Assert.That(s.Player.Dead, Is.True);
        rng = s.Rng.State;
        Assert.That(E.DamageActor(s, s.Player, 5), Is.EqualTo(DamageOutcome.None), "已确死不再接受伤害或判定");
        Assert.That(s.Rng.State, Is.EqualTo(rng));
    }

    /// <summary>意志 10 用 83%：u∈[0.65,0.83) 在 1 级会死、10 级抵抗（按事件开始前的有效等级）。</summary>
    [Test]
    public void M2_WillpowerLevelUsed()
    {
        var s = New();
        s.Player.Stats.Health = 0;
        s.Skills[GameEngine.Willpower] = new SkillState { Level = 10 };
        ForceRng(s, u => u >= 0.70 && u < 0.83);
        Assert.That(E.DamageActor(s, s.Player, 1), Is.EqualTo(DamageOutcome.Resisted));
    }

    [Test]
    public void M1_DyingCompanion_StillEntersBattleAndLeadsSquad()
    {
        var s = New();
        E.DebugQuickParty(s, 2);
        var mate = s.Party.Single(id => id != GameState.PlayerId);
        s.Actors[mate].Stats.Health = 0;
        Assert.That(E.EntryAllies(s, 2), Does.Contain(mate), "濒死同行者照常入战");
        Assert.That(E.SquadMemberValid(s, mate), Is.True, "濒死同行者仍可当队长");
        s.Actors[mate].Dead = true;
        Assert.That(E.SquadMemberValid(s, mate), Is.False);
    }

    /// <summary>搜集划伤：濒死者再被划伤判致死；失败按原死亡收束（产物留地面、原死亡原因），抵抗则写抵抗句、成果照交。</summary>
    [Test]
    public void M2_GatherHazard_WhileDying_RollsDeath()
    {
        var died = false;
        var resisted = false;
        for (uint seed = 1; seed < 60 && !(died && resisted); seed++)
        {
            var s = New(seed);
            E.DebugTeleport(s, "woods");
            E.DebugSetProgress(s, "woods", 25);
            SetStats(s, 0, 80, 80, 18, 90, 60);
            Start(s, ActionRequest.Gather("wood"));
            s.Main!.HazardHit = true;
            var st = Run(s);
            Assert.That(Count(s, "wood", "woods"), Is.EqualTo(1), "产物先交，留地面");
            if (st == SegmentStatus.Dead)
            {
                died = true;
                Assert.That(s.Progress.Death!.Cause, Does.Contain("划伤"));
                Assert.That(s.Log.Select(l => l.Text), Has.None.Contains("抵抗了致死"));
            }
            else
            {
                resisted = true;
                Assert.That(st, Is.EqualTo(SegmentStatus.Completed));
                Assert.That(s.Log.Select(l => l.Text).Count(x => x == GameEngine.ResistedDeathText("你")), Is.EqualTo(1));
                Assert.That(s.Log.Select(l => l.Text), Has.None.Contains("进入濒死"), "已濒死不再写进入句");
            }
        }
        Assert.That((died, resisted), Is.EqualTo((true, true)));
    }

    // ───────────── M3 世界段 ─────────────

    private void Resists(int n) => E.TestDeathRolls = new Queue<double>(Enumerable.Repeat(0.0, n));

    /// <summary>Q616-A04：濒死者同段饥饿+缺水+缺睡是一次身体损伤（判 1 次），出血是另一独立事件（再判 1 次）；世界只推进 5 分钟；战外每次抵抗各写一条（Q678）。</summary>
    [Test]
    public void M3_A04_BodyDamageOneEvent_BleedSeparate_TwoRolls_FiveMinutes()
    {
        var s = New();
        SetStats(s, 0, 0, 0, 18, 0, 60);
        s.Body.BleedDamage = 2;
        s.Body.BleedTriggers = 3;
        Start(s, ActionRequest.Wait(30));
        Resists(10);
        var m0 = s.GameMinute;
        var log0 = s.Log.Count;
        var rng0 = s.Rng.State;
        E.Advance(s);
        Assert.That(E.TestDeathRolls!.Count, Is.EqualTo(8), "恰好两次致死判定");
        Assert.That(s.GameMinute, Is.EqualTo(m0 + 5));
        Assert.That((s.Player.Stats.Health, s.Player.Dead, s.Progress.Dead), Is.EqualTo((0.0, false, false)));
        Assert.That(s.Log.Skip(log0).Count(l => l.Text == GameEngine.ResistedDeathText("你")), Is.EqualTo(2), "Q678：战外每次真实成功抵抗各一条");
        Assert.That(s.Body.BleedTriggers, Is.EqualTo(2));
        // 没有出血、单一缺水：一次判定
        var t = New();
        SetStats(t, 0, 80, 0, 18, 80, 60);
        Start(t, ActionRequest.Wait(30));
        Resists(10);
        E.Advance(t);
        Assert.That(E.TestDeathRolls!.Count, Is.EqualTo(9));
    }

    /// <summary>Q616-A04：正常 H0.02 遮蔽睡眠、清醒 0 → 本段身体损伤先归 0 进入濒死，自然恢复不与负项相抵、本段不再给。</summary>
    [Test]
    public void M3_A04_NormalToDying_RecoveryNotNetted()
    {
        var s = New();
        E.DebugTeleport(s, "w13_mill");
        SetStats(s, 0.02, 80, 80, 18, 0, 60);
        var ctx = E.ComputeSegment(s, new ActionState { Type = ActionType.Sleep, TargetId = "" });
        Assert.That((ctx.HealthLossPerHour, ctx.HealthRecoveryPerHour), Is.EqualTo((Cfg.HealthRules.ZeroWakefulnessPerHour, Cfg.HealthRules.ShelteredRestRecoveryPerHour)));
        Start(s, ActionRequest.Sleep(Cfg.Actions.SleepMinutes.Min()));
        var rng0 = s.Rng.State;
        E.Advance(s);
        Assert.That(s.Player.Stats.Health, Is.EqualTo(0.0), "−0.0333 先归 0，+0.0167 不相抵也不补");
        Assert.That(s.Player.DyingNotified, Is.True);
        Assert.That(s.Log.Select(l => l.Text), Has.One.EqualTo(GameEngine.EnterDyingText("你")));
    }

    /// <summary>Q616-A05：濒死者在完整休息条件下 H 保持 0，其他属性照常恢复；不累存。</summary>
    [Test]
    public void M3_A05_DyingRest_NoNaturalHealthRecovery()
    {
        var s = New();
        E.DebugTeleport(s, "w13_mill");
        SetStats(s, 0, 80, 80, 10, 80, 60);
        var ctx = E.ComputeSegment(s, new ActionState { Type = ActionType.Rest, TargetId = "" });
        Assert.That((ctx.HealthRecoveryPerHour, ctx.HealthPerHour), Is.EqualTo((0.0, 0.0)), "濒死者自然恢复为 0");
        Start(s, ActionRequest.Rest(60));
        Run(s);
        Assert.That(s.Player.Stats.Health, Is.EqualTo(0.0));
        Assert.That(s.Player.Stats.Energy, Is.GreaterThan(10), "体力照常恢复");
        Assert.That(s.Progress.Dead, Is.False);
        // 同条件正常者照旧回血（对照）
        var t = New();
        E.DebugTeleport(t, "w13_mill");
        SetStats(t, 5, 80, 80, 10, 80, 60);
        Start(t, ActionRequest.Rest(60));
        Run(t);
        Assert.That(t.Player.Stats.Health, Is.GreaterThan(5));
    }

    /// <summary>濒死同行者：同一世界段同样不得自然回血；致死失败按原队友死亡收束（原死亡原因）。</summary>
    [Test]
    public void M3_DyingCompanion_NoRecovery_DeathByWorldTick()
    {
        var s = New();
        E.DebugQuickParty(s, 2);
        var mate = s.Actors[s.Party.Single(id => id != GameState.PlayerId)];
        mate.Stats.Health = 0;
        mate.Stats.Satiety = 80; mate.Stats.Hydration = 80; mate.Stats.Morale = 60;
        var ctx = E.ComputeSegment(s, new ActionState { Type = ActionType.Rest, TargetId = "" }, mate);
        Assert.That(ctx.HealthRecoveryPerHour, Is.EqualTo(0.0));
        mate.Stats.Hydration = 0;
        Start(s, ActionRequest.Wait(30));
        FailDeathRolls();
        E.Advance(s);
        Assert.That((mate.Dead, E.RosterOf(s, mate.Id)!.Dead, s.Party.Contains(mate.Id)), Is.EqualTo((true, true, false)), "致死失败 → 原队友死亡收束");
        Assert.That(s.Progress.Dead, Is.False);
    }

    /// <summary>战外出血：濒死者出血致死失败，死亡原因“出血”；正常者出血归 0 只进入濒死。</summary>
    [Test]
    public void M3_BleedWhileDying_DeathCauseBleed()
    {
        var s = New();
        SetStats(s, 1, 80, 80, 18, 80, 60);
        s.Body.BleedDamage = 2;
        s.Body.BleedTriggers = 3;
        Start(s, ActionRequest.Wait(30));
        E.Advance(s);
        Assert.That((s.Player.Stats.Health, s.Progress.Dead), Is.EqualTo((0.0, false)), "正常者出血归 0 → 濒死");
        Assert.That(s.Main!.PauseReason, Is.EqualTo(PauseReason.Dying), "B47a2（Q673）：进入濒死在本段边界暂停一次");
        E.Resume(s);
        FailDeathRolls();
        var st = E.Advance(s);
        Assert.That(st.Status, Is.EqualTo(SegmentStatus.Dead));
        Assert.That(s.Progress.Death!.Cause, Is.EqualTo("出血"));
    }

    // ───────────── M4 战斗 ─────────────

    private void Attack(GameState s, BattleEnemy e) =>
        typeof(GameEngine).GetMethod("EnemyAttack", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(E, new object[] { s, e, Cfg.Enemies.Single(x => x.Id == e.EnemyId), false });

    private bool RoundBleed(GameState s) =>
        (bool)typeof(GameEngine).GetMethod("RoundStartBleed", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(E, new object[] { s })!;

    private void Ready(GameState s)
    {
        PlayerFirst(s);
        E.BattleStep(s);
        Assert.That(E.AwaitingPlayer(s), Is.True);
    }

    private static void Rolls(GameEngine e, params double[] u) => e.TestDeathRolls = new Queue<double>(u);

    /// <summary>主角与一名同行者在农野荒地对一只蹒跚者入战（同行者站 1 号位、主角 3 号位，蹒跚者只能打到同行者）。</summary>
    private GameState PartyFight(uint seed, out string mate)
    {
        var s = New(seed);
        UnlockCapacity(s, 3);
        E.DebugQuickParty(s, 2);
        mate = s.Party.Single(id => id != GameState.PlayerId);
        E.DebugTeleport(s, "field");
        s.GameMinute = At(2, 9, 0);
        s.SpawnNextCheck = int.MaxValue;
        s.PatrolNextCheck = int.MaxValue;
        s.WorldEnemies.Clear();
        var id = E.DebugSpawnEnemy(s, "shambler", "field").Id;
        Assert.That(E.DebugStartBattle(s, new[] { id }, GameEngine.FearEncounter), Is.True);
        s.Battle!.Allies = new List<string> { mate, "", GameState.PlayerId };
        return s;
    }

    /// <summary>Q616-A03：同一目标先被打到 0 只入濒死（不抽），之后每次正伤各判一次；抵抗仍在战、失败按原死亡句终局（Q689 不写未能抵抗句）。</summary>
    [Test]
    public void M4_A03_PlayerHitToDying_ThenEachHitRolls()
    {
        var s = Fight(4701, "shambler");
        var e = s.Battle!.Alive.First();
        s.Player.Stats.Health = 0.3;
        Rolls(E, 0.0, 0.999);
        Force(s, Draw(), Below(0.01), Any());
        Attack(s, e);
        Assert.That((s.Player.Stats.Health, s.Progress.Dead, s.Battle != null, E.TestDeathRolls!.Count), Is.EqualTo((0.0, false, true, 2)), "首次归 0 不判致死");
        var log = s.Battle!.Log;
        Assert.That(log[^1], Does.EndWith(GameEngine.EnterDyingText("你")), "状态句接在伤害战报之后");
        Assert.That(log[^2], Does.Contain("命中").And.Contain("健康−"));
        Assert.That(s.Log.Select(l => l.Text), Has.None.Contains("进入濒死"), "Q688：战斗中只写战报");
        Force(s, Draw(), Below(0.01), Any());
        Attack(s, e);
        Assert.That((s.Progress.Dead, s.Battle != null, E.TestDeathRolls!.Count), Is.EqualTo((false, true, 1)));
        Assert.That(s.Battle!.Log[^1], Does.EndWith(GameEngine.ResistedDeathText("你")));
        Force(s, Draw(), Below(0.01), Any());
        Attack(s, e);
        Assert.That((s.Progress.Dead, s.Battle == null, E.TestDeathRolls!.Count), Is.EqualTo((true, true, 0)));
        Assert.That(s.Progress.Death!.Cause, Does.StartWith("被"));
        // B47a2（Q689 B）：失败前缀接在原死亡主结果前，拼成同一条（不另写独立失败战报）
        Assert.That(s.Log.Count(l => l.Text.Contains("未能抵抗")), Is.EqualTo(1));
        Assert.That(s.Log.Single(l => l.Text.Contains("未能抵抗")).Text, Does.StartWith("你未能抵抗致死。你在"));
    }

    /// <summary>Q616-A06：主角与全队都 0H 未确死不自动败北；同行者濒死不发死亡恐慌、不迁遗物；真死才处理一次（恐慌+8、离队）。</summary>
    [Test]
    public void M4_A06_AllZeroNotDefeated_CompanionDyingNoPanic_DeathOnce()
    {
        var s = PartyFight(4702, out var mate);
        var b = s.Battle!;
        var e = b.Alive.First();
        s.Actor(GameState.PlayerId).Stats.Health = 0;
        s.Actors[mate].Stats.Health = 0.2;
        var p0 = b.Fighter(GameState.PlayerId).Panic;
        Rolls(E, 0.0, 0.999);
        Force(s, Draw(), Below(0.01), Any());
        Attack(s, e);
        Assert.That((E.LifeOf(s, s.Actors[mate]), b.Allies.Contains(mate), s.Battle != null, s.Progress.Dead), Is.EqualTo((LifeState.Dying, true, true, false)), "全员 0H 仍在战");
        Assert.That(b.Fighter(GameState.PlayerId).Panic, Is.EqualTo(p0), "濒死不发同伴死亡恐慌");
        Assert.That(b.Log.Any(l => l.Contains("已战死")), Is.False);
        Assert.That(b.Log[^1], Does.EndWith(GameEngine.EnterDyingText(E.ActorDisplayName(s, mate))));
        Force(s, Draw(), Below(0.01), Any());
        Attack(s, e);
        Assert.That(b.Log[^1], Does.EndWith(GameEngine.ResistedDeathText(E.ActorDisplayName(s, mate))));
        Assert.That(E.TestDeathRolls!.Count, Is.EqualTo(1));
        Force(s, Draw(), Below(0.01), Any());
        Attack(s, e);
        Assert.That((s.Actors[mate].Dead, E.RosterOf(s, mate)!.Dead, b.Allies.Contains(mate), s.Party.Contains(mate)), Is.EqualTo((true, true, false, false)));
        Assert.That(b.Log.Count(l => l.Contains("已战死")), Is.EqualTo(1));
        Assert.That(b.Fighter(GameState.PlayerId).Panic, Is.EqualTo(Math.Min(Cfg.Panic.Range[1], p0 + Cfg.Panic.AllyDeathGain)).Within(1e-9));
        Assert.That((s.Battle != null, s.Progress.Dead), Is.EqualTo((true, false)), "濒死主角仍在战");
    }

    /// <summary>大回合出血：濒死者每次出血触发判一次；失败按“出血”收束（队友战死、主角终局）。</summary>
    [Test]
    public void M4_RoundStartBleed_DyingRolls_DeathByBleed()
    {
        var s = PartyFight(4703, out var mate);
        var m = s.Actors[mate];
        m.Stats.Health = 0;
        m.Body.BleedDamage = 0.4;
        m.Body.BleedTriggers = 2;
        Rolls(E, 0.0, 0.999);
        Assert.That(RoundBleed(s), Is.True);
        Assert.That(m.Dead, Is.False);
        Assert.That(s.Battle!.Log[^1], Does.EndWith(GameEngine.ResistedDeathText(E.ActorDisplayName(s, mate))));
        Assert.That(RoundBleed(s), Is.True);
        Assert.That(m.Dead, Is.True);
        Assert.That(s.Battle!.Log.Any(l => l.Contains("已战死（出血）")), Is.True);

        var t = Fight(4704, "shambler");
        t.Player.Stats.Health = 0.3;
        t.Player.Body.BleedDamage = 0.4;
        t.Player.Body.BleedTriggers = 3;
        Rolls(E, 0.999);
        Assert.That(RoundBleed(t), Is.True, "正常者出血归 0 → 濒死，仍在战");
        Assert.That((t.Player.Stats.Health, t.Progress.Dead, E.TestDeathRolls!.Count), Is.EqualTo((0.0, false, 1)));
        Assert.That(RoundBleed(t), Is.False);
        Assert.That((t.Progress.Dead, t.Progress.Death!.Cause, t.Battle), Is.EqualTo((true, "出血", (BattleState?)null)));
    }

    /// <summary>敌人没有濒死：H 归 0 直接死亡、退出在场名单（R09 §7）。</summary>
    [Test]
    public void M4_EnemyAtZero_DiesDirectly()
    {
        var s = Fight(4705, "shambler", "shambler");
        Ready(s);
        var e = Enemy(s, 1);
        e.Hp = 0.01;
        Force(s, Below(0.001), AtLeast(0.999), Any(), Any());
        Cmd(s, "basic_attack", 1);
        Assert.That((e.Alive, s.Battle!.Alive.Contains(e)), Is.EqualTo((false, false)));
        Assert.That(E.TestDeathRolls, Is.Null, "敌人不判致死");
    }

    // ───────────── M5 医疗脱离 ─────────────

    /// <summary>Q616-A05：濒死者用绷带实际回血即脱离（写在用药结果之后）；再次归 0 重新先入濒死、不抽致死（无隐藏累计惩罚）。</summary>
    [Test]
    public void M5_A05_BandageLeavesDying_ThenReentersFresh()
    {
        var s = New();
        SetStats(s, 0, 80, 80, 18, 80, 60);
        s.Player.DyingNotified = true;
        E.DebugGive(s, "bandage", 1);
        var log0 = s.Log.Count;
        Start(s, ActionRequest.UseItem("bandage"));
        Assert.That(s.Player.Stats.Health, Is.GreaterThan(0));
        Assert.That((E.LifeOf(s, s.Player), s.Player.DyingNotified), Is.EqualTo((LifeState.Normal, false)));
        var texts = s.Log.Skip(log0).Select(l => l.Text).ToList();
        var leave = texts.IndexOf(GameEngine.LeaveDyingText("你"));
        Assert.That(leave, Is.GreaterThan(0), "脱离句存在且在用药结果之后");
        Assert.That(texts[leave - 1], Does.Contain("健康+"));
        Assert.That(texts.Count(x => x == GameEngine.LeaveDyingText("你")), Is.EqualTo(1));
        var rng = s.Rng.State;
        Assert.That(E.DamageActor(s, s.Player, 50), Is.EqualTo(DamageOutcome.EnteredDying), "再次归 0 先入濒死");
        Assert.That(s.Rng.State, Is.EqualTo(rng));
    }

    /// <summary>只止血、不回血（回血冷却中）不脱离濒死；食物等非医疗正健康在濒死期间不生效。</summary>
    [Test]
    public void M5_BleedStopOnly_NoLeave_FoodHealthGated()
    {
        var s = New();
        SetStats(s, 0, 80, 80, 18, 80, 60);
        s.BandageReadyAt = s.GameMinute + 30;
        s.Body.BleedTriggers = 2;
        s.Body.BleedDamage = 0.4;
        E.DebugGive(s, "bandage", 1);
        Start(s, ActionRequest.UseItem("bandage"));
        Assert.That((s.Body.BleedTriggers, s.Player.Stats.Health, E.IsDying(s, s.Player)), Is.EqualTo((0, 0.0, true)));
        Assert.That(s.Log.Select(l => l.Text), Has.None.EqualTo(GameEngine.LeaveDyingText("你")));
        var apply = typeof(GameEngine).GetMethod("ApplyUseEffect", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var food = new Survival.Core.Config.ItemUseConfig { Health = 2 };
        var parts = new List<string>();
        apply.Invoke(E, new object[] { s, s.Player, food, parts, false });
        Assert.That(s.Player.Stats.Health, Is.EqualTo(0.0), "食物正健康不让濒死者脱离");
        s.Player.Stats.Health = 5;
        apply.Invoke(E, new object[] { s, s.Player, food, parts, false });
        Assert.That(s.Player.Stats.Health, Is.EqualTo(7.0), "正常者照旧");
    }

    /// <summary>战斗中用绷带脱离：脱离句只进战报（Q688），接在用药战报之后。</summary>
    [Test]
    public void M5_BattleBandage_LeaveLineInBattleLogOnly()
    {
        var s = Fight(4706, "shambler");
        Ready(s);
        E.DebugGive(s, "bandage", 1);
        s.Player.Stats.Health = 0;
        Assert.That(Cmd(s, "bandage").Ok, Is.True);
        Assert.That(E.LifeOf(s, s.Player), Is.EqualTo(LifeState.Normal));
        var log = s.Battle!.Log;
        Assert.That(log[^1], Does.EndWith(GameEngine.LeaveDyingText("你")));
        Assert.That(log[^2], Does.Contain("健康+"));
        Assert.That(s.Log.Select(l => l.Text), Has.None.EqualTo(GameEngine.LeaveDyingText("你")));
    }

    /// <summary>城内换药（clinic_dressing）给濒死同行者回血：同一入口脱离，脱离句写在换药完成句之后（普通日志）。</summary>
    [Test]
    public void M5_ClinicDressing_LeavesDying()
    {
        var s = New();
        s.Progress.Flags.UnionWith(new[] { "registration_authorized", "hub_registered" });
        s.GameMinute = At(2, 9, 0);
        s.SpawnNextCheck = int.MaxValue;
        s.PatrolNextCheck = int.MaxValue;
        E.DebugMapTeleport(s, "h04_infirmary");
        ReadDialogues(s);
        E.DebugGive(s, "bandage", 1);
        E.DebugGive(s, "cloth", 1);
        s.Player.Stats.Health = 0;
        Assert.That(E.StartCityService(s, "clinic_dressing", GameState.PlayerId).Ok, Is.True);
        Run(s);
        Assert.That(E.LifeOf(s, s.Player), Is.EqualTo(LifeState.Normal));
        var texts = s.Log.Select(l => l.Text).ToList();
        var i = texts.LastIndexOf(GameEngine.LeaveDyingText("你"));
        Assert.That(i, Is.GreaterThan(0));
        Assert.That(texts[i - 1], Does.StartWith("已为你换药。"));
    }
}
