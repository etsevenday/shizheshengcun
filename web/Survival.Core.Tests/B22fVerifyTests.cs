using System.Reflection;
using Newtonsoft.Json.Linq;
using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// B22f 核对：策划提交 280f8be 答复 Q179—Q207 中“与现状一致”的条目逐条补验收（PLAN-Q182—Q206）。规则原文见
/// 更新说明 2026-11-03「Q179-Q207 更新说明」全部验收节；各 Q 的游戏现状见 00 总则/02 开发方待确认问题。
/// 已有测试覆盖大部分规则，这里每条只写一个小而准的薄测试断言答复里的关键点（已有测试名写在各方法注释里）。
/// </summary>
public class B22fVerifyTests : BattleTestBase
{
    private const string Shelter = "h06_shelter";
    private const string Lin = "npc_lin_zhao";

    private static void Ok(ActionResult r) => Assert.That(r.Ok, Is.True, r.Reason);

    // ───────────── 通用场景 ─────────────

    /// <summary>营区登记后实际到达收容所（同 B19RecruitTests.AtShelter）。</summary>
    private GameState AtShelter(int c = 2, uint seed = 12345)
    {
        var s = New(seed);
        s.SpawnNextCheck = int.MaxValue;
        s.PatrolNextCheck = int.MaxValue;
        s.GameMinute = At(1, 10, 0);
        if (c >= 3) s.Progress.Flags.Add("church_route_record");
        if (c >= 4) s.Progress.Flags.Add("academy_safety_record");
        E.DebugRecruitment(s, "go");
        Assert.That(s.CurrentLocation, Is.EqualTo(Shelter));
        return s;
    }

    /// <summary>登记一名当前候选并走完 10 分钟。</summary>
    private RosterEntry Recruit(GameState s, int index = 0)
    {
        var c = s.Recruit.Candidates[index];
        Ok(E.StartRecruit(s, c.CandidateId, RecruitPlacement.Square, Guid.NewGuid().ToString("N")));
        Run(s);
        return s.Roster.Single(r => r.CandidateId == c.CandidateId && r.Cycle == c.Cycle);
    }

    /// <summary>随身塞满五张卡（同 B22SealedTests.FillCards）。</summary>
    private void FillCards(GameState s)
    {
        foreach (var it in Cfg.Items.Where(i => i.Kind == "material" && i.WeightGrams <= 100 && i.IsStackable && i.Id != "cloth" && i.Id != "leather"))
        {
            if (E.RootCardCount(s) >= E.MaxRootCards) break;
            if (s.Items.Any(x => x.IsCarried && x.ItemId == it.Id)) continue;
            E.CreateItems(s, it.Id, 1, ItemInstance.Carried);
        }
        Assert.That(E.RootCardCount(s), Is.EqualTo(5));
    }

    private void Go(GameState s, string to, bool confirm = false)
    {
        Start(s, ActionRequest.Travel(to, confirm));
        Assert.That(Run(s), Is.EqualTo(SegmentStatus.Completed));
        Assert.That(s.CurrentLocation, Is.EqualTo(to));
    }

    private static void High(GameState s) => s.Player.Stats = new Stats { HealthMax = 20, EnergyMax = 20, Health = 20, Satiety = 95, Hydration = 95, Energy = 20, Wakefulness = 95, Morale = 95 };

    private void EnemyAct(GameState s, BattleEnemy e) =>
        typeof(GameEngine).GetMethod("EnemyActivation", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(E, new object[] { s, e, false });

    private void Lock(GameState s, BattleEnemy e) =>
        typeof(GameEngine).GetMethod("LockIntent", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(E, new object[] { s, s.Battle!, e });

    private void Kill(GameState s, BattleEnemy e) =>
        typeof(GameEngine).GetMethod("KillEnemy", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(E, new object[] { s, e });

    private void Patrol(GameState s) =>
        typeof(GameEngine).GetMethod("Patrol", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(E, new object[] { s });

    /// <summary>把敌人意图拨到循环第 k 项并重新锁定（同 B20EnemyTests.AtStep）。</summary>
    private void AtStep(GameState s, BattleEnemy e, int k)
    {
        if (s.Battle!.Round - k < 1) s.Battle.Round = k + 1;
        e.JoinRound = s.Battle.Round - k;
        e.Acted = false;
        e.RoundApUsed = 0;
        Lock(s, e);
    }

    private void ReadyBattle(GameState s)
    {
        PlayerFirst(s);
        E.BattleStep(s);
        Assert.That(E.AwaitingPlayer(s), Is.True);
    }

    /// <summary>入战后把林昭放进我方号位（同 B8bTargetTests.Duo）：slots 为 1—4 号位 actorId，空位写 ""。</summary>
    private GameState Duo(uint seed, string[] slots, params string[] enemies)
    {
        var s = Fight(seed, enemies);
        Ok(E.AddNamedToRoster(s, Lin, false));
        s.Party.Add(Lin);
        var list = slots.ToList();
        while (list.Count > 0 && list[^1] == "") list.RemoveAt(list.Count - 1);
        s.Battle!.Allies = list;
        return s;
    }

    // ───────────── Q182 退役耗时 ─────────────

    /// <summary>
    /// PLAN-Q182-01：退役确认 0 世界分钟立即生效（不推进时间、不开行动、不过段），不计经验（主角与退役者经验都不变）。
    /// 已有：B19RecruitTests.RC06_RetireMovesItemsToClaimBox_FreesSlot / RC06_RetireRules（资格与原子迁物）。
    /// </summary>
    [Test]
    public void PLAN_Q182_01_Retire_ZeroMinutes_NoXp()
    {
        var s = AtShelter(2);
        var r = Recruit(s, 0);
        var t = s.GameMinute;
        var seg = s.SegmentCount;
        var playerXp = s.Skills.ToDictionary(kv => kv.Key, kv => (kv.Value.Level, kv.Value.Xp));
        var mateXp = s.Actors[r.ActorId].Skills.ToDictionary(kv => kv.Key, kv => (kv.Value.Level, kv.Value.Xp));
        Ok(E.Retire(s, r.ActorId));
        Assert.That(s.GameMinute, Is.EqualTo(t), "0 世界分钟");
        Assert.That(s.SegmentCount, Is.EqualTo(seg), "不结算段");
        Assert.That(s.Main, Is.Null, "不开等待行动");
        Assert.That(r.Retired, Is.True, "立即生效");
        Assert.That(r.RetiredMinute, Is.EqualTo(t));
        Assert.That(s.Skills.ToDictionary(kv => kv.Key, kv => (kv.Value.Level, kv.Value.Xp)), Is.EqualTo(playerXp), "主角不计经验");
        Assert.That(s.Actors[r.ActorId].Skills.ToDictionary(kv => kv.Key, kv => (kv.Value.Level, kv.Value.Xp)), Is.EqualTo(mateXp), "退役者经验不变");
    }

    // ───────────── Q184 认领箱 ─────────────

    /// <summary>
    /// PLAN-Q184-01：每名退役者一份独立认领箱（remains:actorId，Kind=claim，都在 h06），各只装本人迁来的物；只有实到收容所能取；
    /// 取到随身验五卡（与 30kg，同 PickupBlockedText），或放当地地面；取空即撤掉，不能当仓库再存（没有存入入口）。
    /// 已有：B19RecruitTests.RC06_RetireMovesItemsToClaimBox_FreesSlot、RC11_RetireEscort_LoanSpearReturnsToSlot_NoNewSpear。
    /// </summary>
    [Test]
    public void PLAN_Q184_01_ClaimBox_PerRetiree_OnlyAtShelter_CardCheckOrGround()
    {
        var s = AtShelter(4);
        var a = Recruit(s, 0);
        var b = Recruit(s, 1);
        var fa = E.CreateItems(s, "cloth", 1, ItemInstance.CarriedOwner(a.ActorId)).Single();
        var fb = E.CreateItems(s, "leather", 1, ItemInstance.CarriedOwner(b.ActorId)).Single();
        Ok(E.Retire(s, a.ActorId));
        Ok(E.Retire(s, b.ActorId));
        Assert.That(s.Remains[a.ActorId].IsClaim && s.Remains[b.ActorId].IsClaim, Is.True);
        Assert.That((s.Remains[a.ActorId].LocationId, s.Remains[b.ActorId].LocationId), Is.EqualTo((Shelter, Shelter)));
        Assert.That(E.RemainsItems(s, a.ActorId).Select(i => i.InstanceId), Is.EqualTo(new[] { fa.InstanceId }), "只收该退役者迁来的物");
        Assert.That(E.RemainsItems(s, b.ActorId).Select(i => i.InstanceId), Is.EqualTo(new[] { fb.InstanceId }));

        E.DebugMapTeleport(s, "camp");
        Assert.That(E.TakeFromRemainsBlocked(s, a.ActorId, fa.InstanceId, true), Does.Contain("认领箱在"), "不在收容所不能取（放地面也不行）");
        E.DebugMapTeleport(s, Shelter);
        FillCards(s);
        Assert.That(E.TakeFromRemainsBlocked(s, a.ActorId, fa.InstanceId, false), Does.Contain("卡位已满"), "取到随身验五卡");
        Assert.That(E.TakeFromRemainsBlocked(s, a.ActorId, fa.InstanceId, false), Is.EqualTo(E.PickupBlockedText(s, fa, fa.Count)), "与拾取同一校验（五卡+负重）");
        Ok(E.TakeFromRemains(s, a.ActorId, fa.InstanceId, true));
        Assert.That(fa.Owner, Is.EqualTo(Shelter), "放当地地面");
        Assert.That(s.Remains.ContainsKey(a.ActorId), Is.False, "取空撤掉，不作可存入的仓库");
        Assert.That(s.Remains[b.ActorId].IsClaim, Is.True, "另一名退役者的箱子独立");
    }

    // ───────────── Q185 随机角色借矛 ─────────────

    /// <summary>
    /// PLAN-Q185-01：随机角色登记空装备/空随身，不绑定借用位、不发借用矛；引擎没有“为随机角色申请借矛”的入口（只有 RB01 首发与报失）。
    /// 已有：B19RecruitTests.RC04_A06C01_RecruitCreatesExactSnapshot_NotReplacedByRefresh（不送装备与物资）、RC11。
    /// </summary>
    [Test]
    public void PLAN_Q185_01_RandomRecruit_EmptyGear_NoLoanEntry()
    {
        var s = AtShelter(2);
        var spears = s.Items.Count(i => i.ItemId == "wooden_spear");
        var loans = s.LoanSlots.Count;
        var r = Recruit(s, 0);
        Assert.That(r.IsRandom, Is.True);
        Assert.That(r.LoanSlot, Is.Null, "不绑定借用位");
        Assert.That(s.Items.Count(i => i.Owner == ItemInstance.CarriedOwner(r.ActorId) || i.IsWornBy(r.ActorId)), Is.EqualTo(0), "空装备、空随身");
        Assert.That(s.Items.Count(i => i.ItemId == "wooden_spear"), Is.EqualTo(spears), "不发借用矛");
        Assert.That(s.LoanSlots.Count, Is.EqualTo(loans));
        var names = typeof(GameEngine).GetMethods(BindingFlags.Public | BindingFlags.Instance).Select(m => m.Name).ToList();
        Assert.That(names.Where(n => n.Contains("Loan")), Has.None.Match("Apply|Request|Grant|Issue"), "没有申请借矛入口");
    }

    // ───────────── Q186 随机候选人物档 ─────────────

    /// <summary>
    /// PLAN-Q186-01：速度 3.0—5.0 步 0.1 共 21 值近似等概率；控制抗性 0—10 步 0.5、闪避 34—38 步 0.5、基础甲 0。
    /// 已有：B19RecruitTests.RC_Config_FromDesign（配置）、RC03_A06C01_TemplatesSkillsBody_InRangeAndDistribution（区间与取值个数）。
    /// </summary>
    [Test]
    public void PLAN_Q186_01_SpeedStep01_21Values_Uniform_ProfileRanges()
    {
        var rr = Cfg.ActorBaseDefense.RandomRecruit!;
        string Spec(string k) => $"{(double)rr[k]!["min"]!}/{(double)rr[k]!["max"]!}/{(double)rr[k]!["step"]!}";
        Assert.That((Spec("baseSpeed"), Spec("controlResist"), Spec("evasion")), Is.EqualTo(("3/5/0.1", "0/10/0.5", "34/38/0.5")));
        var s = AtShelter();
        var start = s.Recruit.ShelterOpenedAt!.Value;
        var all = new List<RecruitCandidate>();
        for (var k = 0; k < 1400; k++)
        {
            s.GameMinute = start + 4320 * k;
            E.EnsureRecruitCycle(s);
            all.AddRange(s.Recruit.Candidates);
        }
        var speeds = all.GroupBy(c => Math.Round(c.BaseSpeed, 4)).ToDictionary(g => g.Key, g => g.Count());
        Assert.That(speeds.Keys.OrderBy(x => x), Is.EqualTo(Enumerable.Range(0, 21).Select(i => Math.Round(3.0 + 0.1 * i, 4))), "3.0、3.1……5.0 共 21 值");
        var expect = all.Count / 21.0;
        foreach (var kv in speeds) Assert.That(kv.Value, Is.InRange(expect * 0.6, expect * 1.4), $"速度{kv.Key}等概率");
        Assert.That(all.Select(c => c.ControlResist).Distinct().OrderBy(x => x), Is.EqualTo(Enumerable.Range(0, 21).Select(i => 0.5 * i)));
        Assert.That(all.Select(c => c.Evasion).Distinct().OrderBy(x => x), Is.EqualTo(Enumerable.Range(0, 9).Select(i => 34 + 0.5 * i)));
        Assert.That(all.All(c => c.BaseArmor == 0), Is.True);
    }

    // ───────────── Q188 固定战首次揭示 ─────────────

    /// <summary>
    /// PLAN-Q188-01：远征入口只作整体预告，不提前锁具体房名单；首次实际到房才按当时容量 C 锁名单与 worldEnemyId，到达不自动开战；
    /// 扩容 + 读档 + 离开再到不重编。已有：B20FixedBattleTests.EC03_DR21_TruncateByCapacityAtReveal_NoRebuild、EC06_RetreatKeepsHp_ReloadSameRoster、
    /// NewGeneration_RevealsAgain_NewIds_DevFactSettles。
    /// </summary>
    [Test]
    public void PLAN_Q188_01_RevealOnActualArrival_NoAutoBattle_NoRebuild()
    {
        var s = B21EntryTests.AtCrypt(E, 2881);
        UnlockCapacity(s, 2);
        E.ExpeditionPreview(s, "d01");
        Assert.That(s.Population.Fixed.Keys.Where(k => k.StartsWith("d01_")), Is.Empty, "入口预告不锁房间名单");
        Go(s, "d01_01");
        E.DebugSetProgress(s, "d01_01", 100);
        Assert.That(E.FixedStateAt(s, "d01_02"), Is.Null, "相邻可见也不揭示");
        Go(s, "d01_02", true);
        Assert.That(s.Battle, Is.Null, "到达不自动开战");
        Assert.That(s.Encounter, Is.Null);
        var st = E.FixedStateAt(s, "d01_02")!;
        Assert.That(st.Capacity, Is.EqualTo(2));
        Assert.That(E.FixedStateAt(s, "d01_03"), Is.Null, "没到的房不锁");
        var ids = E.FixedEnemiesAt(s, "d01_02").Select(w => w.Id).ToList();
        Assert.That(ids, Is.Not.Empty);
        UnlockCapacity(s, 4);
        var t = SaveSerializer.Deserialize(SaveSerializer.Serialize(s), Cfg);
        Go(t, "d01_01");
        Go(t, "d01_02");
        Assert.That(E.FixedEnemiesAt(t, "d01_02").Select(w => w.Id), Is.EqualTo(ids), "扩容、读档、再到不重编");
        Assert.That(E.FixedStateAt(t, "d01_02")!.Capacity, Is.EqualTo(2));
        Assert.That(t.Battle, Is.Null);
    }

    // ───────────── Q190 低吟感染者鼓动 ─────────────

    /// <summary>
    /// PLAN-Q190-01：鼓动最近两名存活同伴（同距前排在前；死者不算）；下一次攻击落空也耗掉；未攻击不按轮过期；同名取强 ×1.10；
    /// 与异名“蓄势”相乘。已有：B20EnemyTests.R62_Cantor_RallyTwoAllies_NoStack_ConsumedOnNextHit（命中耗掉、无同伴改抓击、同名取强）。
    /// </summary>
    [Test]
    public void PLAN_Q190_01_Rally_NearestTwo_MissConsumes_NoRoundExpiry_MultipliesBuff()
    {
        var s = FightWith(2901, 4, "z_slow", "ec_cantor", "shambler", "shambler");
        ReadyBattle(s);
        var b = s.Battle!;
        Assert.That(b.Enemies, Has.Count.EqualTo(4));
        var a1 = Enemy(s, 1); var c = Enemy(s, 2); var a3 = Enemy(s, 3); var a4 = Enemy(s, 4);
        a1.Slot = "1"; c.Slot = "2"; a3.Slot = "3"; a4.Slot = "4";
        Assert.That(E.RallyTargets(b, c).Select(x => x.Pos), Is.EqualTo(new[] { 1, 3 }), "最近两名，同距前排在前");
        AtStep(s, c, 0);
        Assert.That(c.Intent, Is.EqualTo("rally"));
        EnemyAct(s, c);
        Assert.That((a1.RallyFactor, a3.RallyFactor, a4.RallyFactor), Is.EqualTo((1.1, 1.1, 1.0)));

        // 未攻击不按轮过期
        b.Round += 3;
        AtStep(s, a3, 0);
        Assert.That(a3.RallyFactor, Is.EqualTo(1.1), "过几轮、重锁意图都还在");

        // 落空也耗掉
        StandAt(s, 1);
        AtStep(s, a1, 0);
        Force(s, Draw(), AtLeast(0.9999));
        EnemyAct(s, a1);
        Assert.That(a1.RallyFactor, Is.EqualTo(1.0), "落空即耗");

        // 与异名蓄势相乘：×1.3×1.1
        a1.RallyFactor = 1.1;
        a1.NextHitFactor = 1.3;
        s.Stats.Health = 20;
        AtStep(s, a1, 0);
        Force(s, Draw(), Below(0.01), Below(0.0001));
        var h0 = s.Stats.Health;
        EnemyAct(s, a1);
        var cfg = Cfg.Enemies.Single(x => x.Id == "z_slow");
        var lo = cfg.DamageMin * 1.43;
        var hi = (cfg.DamageMin + 0.0001 * (cfg.DamageMax - cfg.DamageMin)) * 1.43;
        Assert.That(h0 - s.Stats.Health, Is.InRange(lo - 1e-6, hi + 1e-6), "伤害×1.3×1.1：" + string.Join(" | ", b.Log.TakeLast(2)));
        Assert.That((a1.RallyFactor, a1.NextHitFactor), Is.EqualTo((1.0, 1.0)));

        // 死者不算：打死 3 号位后最近两名是 1、4 号位的存活者
        Kill(s, a3);
        var ts = E.RallyTargets(b, c);
        Assert.That(ts, Has.Count.EqualTo(2));
        Assert.That(ts.All(x => x.Alive && x.Index != a3.Index), Is.True);
    }

    // ───────────── Q191 守卫标记 ─────────────

    /// <summary>
    /// PLAN-Q191-01：隔离核心守卫的指定普攻只打 1/2 号位：被标记者在 3 号位时从 1/2 存活者随机（这里只有林昭），不换大招；标记本身无命中/伤害加成
    /// （同一随机序列下标记林昭与无标记打出的伤害相同）。仓管守卫标记最靠后活人，拉扯可打 1—4 位被标者、命中后控制成功前拉 1 位。
    /// 已有：B20EnemyTests.R62_ColdGuard_MarkRearmost_PullsMarked、R62_LabGuard_MarkThenAttackMarked_FallbackRandomWhenInvalid。
    /// </summary>
    [Test]
    public void PLAN_Q191_01_LabGuard_MarkedOutOfReach_RandomFrontTwo_NoBonus_ColdGuardPullsRear()
    {
        var s = Duo(2911, new[] { Lin, "", GameState.PlayerId }, "ec_boss_lab");
        ReadyBattle(s);
        s.Battle!.Allies = new List<string> { Lin, "", GameState.PlayerId };
        var e = Enemy(s, 1);
        e.Slot = "1";
        AtStep(s, e, 1);
        Assert.That(e.Intent, Is.EqualTo("attack"));
        e.MarkedActor = GameState.PlayerId;   // 被标记者在 3 号位：普攻够不到
        var note = E.IntentTargetNote(s, s.Battle, e)!;
        Assert.That(note, Does.Not.Contain("打它标记的").And.Contain("随机"));
        var lin = s.Actor(Lin);
        lin.Stats.Set(StatId.Health, 20);
        s.Stats.Health = 20;
        Force(s, Draw(), Below(0.01), Below(0.0001));
        var rng = s.Rng.State;
        EnemyAct(s, e);
        var dealtFallback = 20 - lin.Stats.Health;
        Assert.That(dealtFallback, Is.GreaterThan(0), "打 1 号位的林昭：" + string.Join(" | ", s.Battle.Log.TakeLast(2)));
        Assert.That(s.Stats.Health, Is.EqualTo(20), "不打 3 号位被标记者，也不换大招");

        // 标记林昭（在 1 号位）：同一随机序列伤害相同 → 标记无伤害加成（命中率公式也不读标记）
        lin.Stats.Set(StatId.Health, 20);
        AtStep(s, e, 1);
        e.MarkedActor = Lin;
        s.Rng.State = rng;
        EnemyAct(s, e);
        Assert.That(20 - lin.Stats.Health, Is.EqualTo(dealtFallback).Within(1e-9), "标记无额外伤害");

        // 仓管守卫：最靠后活人（3 号位主角），拉扯打 1—4 位被标者，控制成功前拉 1 位；林昭不受影响
        var t = Duo(2912, new[] { Lin, "", GameState.PlayerId }, "ec_boss_cold");
        ReadyBattle(t);
        t.Battle!.Allies = new List<string> { Lin, "", GameState.PlayerId };
        var g = Enemy(t, 1);
        g.Slot = "1";
        AtStep(t, g, 0);
        Assert.That((g.Intent, g.IntentActor), Is.EqualTo(("mark", GameState.PlayerId)));
        EnemyAct(t, g);
        Assert.That(g.MarkedActor, Is.EqualTo(GameState.PlayerId));
        AtStep(t, g, 1);
        Assert.That(g.Intent, Is.EqualTo("pull"));
        var tl = t.Actor(Lin);
        tl.Stats.Set(StatId.Health, 20);
        t.Stats.Health = 20;
        Force(t, Draw(), Below(0.01), Any(), Below(0.05));
        EnemyAct(t, g);
        Assert.That(t.Stats.Health, Is.LessThan(20), "低伤拉扯打到 3 号位被标者");
        Assert.That(tl.Stats.Health, Is.EqualTo(20));
        Assert.That(GameEngine.PlayerPos(t.Battle), Is.EqualTo(2), "前拉 1 位：" + string.Join(" | ", t.Battle.Log.TakeLast(2)));
    }

    // ───────────── Q194 b01/b02 公共服务点 ─────────────

    /// <summary>
    /// PLAN-Q194-01：b01_school/b02_station 不在任何人口区、无池权重、不随机接敌；首次到达不生成；所在 mainline 区刷新与巡游多次也从不出现在这两点。
    /// 已有：B20PopulationTests.EC02_RegionsAndPools_MatchJson（两点 PopulationWeights 为空）。
    /// </summary>
    [Test]
    public void PLAN_Q194_01_B01B02_NoSpawn_NoPatrol_NoRandomEncounter()
    {
        var pub = new[] { "b01_school", "b02_station" };
        foreach (var id in pub)
        {
            var l = E.Location(id);
            Assert.That((l.PopulationWeights, l.PopulationRegionId, l.RandomEncounterEnabled, l.PopulationLocalCap), Is.EqualTo(((Dictionary<string, int>?)null, (string?)null, false, 0)), id);
            Assert.That(Cfg.WorldEnemies.Regions.Any(r => r.Nodes.Contains(id)), Is.False, id + " 不混入分层人口区");
        }
        var s = New(2941);
        s.GameMinute = At(8, 9, 0);
        s.SpawnNextCheck = int.MaxValue;
        s.PatrolNextCheck = int.MaxValue;
        foreach (var id in pub)
        {
            E.DebugMapTeleport(s, id);
            Assert.That(E.LocalCount(s, id), Is.EqualTo(0), id + " 首次到达不生成");
        }
        E.DebugMapTeleport(s, "b03_hospital");
        E.DebugMapTeleport(s, "h02_square");
        for (var i = 0; i < 300; i++)
        {
            E.DebugRegionSpawnCheck(s);
            Patrol(s);
            Assert.That(s.WorldEnemies.Any(w => pub.Contains(w.LocationId)), Is.False, "不刷新、不巡入");
        }
        Assert.That(s.WorldEnemies.Any(w => w.LocationId == "b03_hospital" || Cfg.WorldEnemies.Regions.Single(r => r.Id == "mainline_aid").Nodes.Contains(w.LocationId)), Is.True, "同区其他节点照常有人口（检查有效）");
    }

    // ───────────── Q197 惊魂未定 ─────────────

    /// <summary>
    /// PLAN-Q197-01：每人独立截止——到点前入战的人恐慌初值 +10、到点后入战的人不加；用后/到期都清掉（不按 6 小时内每场都加）；
    /// 不战到期后状态消失。已有：B21AbortTests.DR08_TwoAborts_ShakenMaxNotStacked_PanicOnce（只下一场、重触发取较晚、不叠层）。
    /// </summary>
    [Test]
    public void PLAN_Q197_01_PerActorDeadline_NextBattleOnly_ExpiresWithoutBattle()
    {
        GameState Setup(int? playerUntil, int? linUntil)
        {
            var s = New(2971);
            UnlockCapacity(s, 2);
            E.DebugRecruit(s, Lin);
            Assert.That(s.Party, Does.Contain(Lin));
            E.DebugTeleport(s, "field");
            s.GameMinute = At(2, 9, 0);
            s.SpawnNextCheck = int.MaxValue;
            s.PatrolNextCheck = int.MaxValue;
            s.WorldEnemies.Clear();
            foreach (var a in s.PartyActors) a.Stats.Set(StatId.Morale, 90);
            s.Player.ShakenUntil = playerUntil == null ? null : s.GameMinute + playerUntil;
            s.Actor(Lin).ShakenUntil = linUntil == null ? null : s.GameMinute + linUntil;
            var id = E.DebugSpawnEnemy(s, "shambler", "field").Id;
            Assert.That(E.DebugStartBattle(s, new List<int> { id }, GameEngine.FearEncounter), Is.True);
            return s;
        }
        var baseline = Setup(null, null);
        var shaken = Setup(100, -1);   // 主角还有 100 分钟；林昭 1 分钟前已到期
        double P(GameState s, string id) => s.Battle!.Fighter(id).Panic;
        Assert.That(P(shaken, GameState.PlayerId) - P(baseline, GameState.PlayerId), Is.EqualTo(10).Within(1e-9), "未到期的人 +10");
        Assert.That(P(shaken, Lin) - P(baseline, Lin), Is.EqualTo(0).Within(1e-9), "已到期的人不加（每人独立截止）");
        Assert.That((shaken.Player.ShakenUntil, shaken.Actor(Lin).ShakenUntil), Is.EqualTo(((int?)null, (int?)null)), "用后消失");

        // 不战到期：6 小时内没入战，到点状态消失
        var w = New(2972);
        w.SpawnNextCheck = int.MaxValue;
        w.PatrolNextCheck = int.MaxValue;
        w.Player.ShakenUntil = w.GameMinute + 60;
        Assert.That(E.ActiveConditions(w).Any(c => c.Id == "shaken"), Is.True);
        SetStats(w, 20, 90, 90, 20, 90, 80);
        AdvanceTo(w, w.GameMinute + 90);
        Assert.That(E.ActiveConditions(w).Any(c => c.Id == "shaken"), Is.False, "不战到期");
    }

    // ───────────── Q198 撤出返程路线 ─────────────

    /// <summary>
    /// PLAN-Q198-01：返程只走本代已开通、两端都到过的双向内部边 + 01↔父地点入口边，按基础分钟最短路：没到过的 03 不走（绕 05→04→02），
    /// 到过之后走 06→03→01；守卫后的 08 撤出不走单向出口 08→父地点。已有：B21ExtractTests.DR06_ShortPath_T30_PositionsAndArrival。
    /// </summary>
    [Test]
    public void PLAN_Q198_01_ExtractRoute_KnownBidirectionalOnly_ShortestBaseMinutes()
    {
        var s = B21EntryTests.AtBuffer(E, 2981);
        UnlockCapacity(s, 2);
        foreach (var n in new[] { "d01_02", "d01_04", "d01_05", "d01_06" })
        {
            E.DebugTeleport(s, n);
            E.DebugSetProgress(s, n, 100);
        }
        // 03—06 边按 03 探索 100% 开通；这里直接记开通（03 仍没到过），验证“两端都到过”这一条单独起作用
        E.DebugUnlockEdge(s, "d01_03__d01_06");
        Assert.That(E.EdgeOpen(s, E.EdgeBetween("d01_03", "d01_06")!), Is.True, "06—03 边已开通");
        var p1 = E.ComputeExtractPlan(s, "d01", "d01_06");
        Assert.That(p1.Route, Is.EqualTo(new[] { "d01_06", "d01_05", "d01_04", "d01_02", "d01_01", "c03_crypt" }), "没到过的 03 不走");
        Assert.That(p1.BaseMinutes, Is.EqualTo(30));
        E.DebugTeleport(s, "d01_03");
        E.DebugTeleport(s, "d01_06");
        var p2 = E.ComputeExtractPlan(s, "d01", "d01_06");
        Assert.That(p2.Route, Is.EqualTo(new[] { "d01_06", "d01_03", "d01_01", "c03_crypt" }), "两端都到过后取基础分钟最短路");
        Assert.That(p2.BaseMinutes, Is.EqualTo(20));

        E.DebugInstanceFact(s, "d01", "guardianDefeated");
        E.DebugTeleport(s, "d01_07");
        E.DebugSetProgress(s, "d01_07", 100);
        E.DebugTeleport(s, "d01_08");
        Assert.That(E.DungeonCleared(s, "d01"), Is.False);
        var p3 = E.ComputeExtractPlan(s, "d01", "d01_08");
        Assert.That(p3.Route[1], Is.EqualTo("d01_07"), "不走单向出口 08→父地点");
        Assert.That(p3.Route.TakeLast(2), Is.EqualTo(new[] { "d01_01", "c03_crypt" }));
    }

    // ───────────── Q199 撤出途中 ─────────────

    /// <summary>
    /// PLAN-Q199-01（限制部分）：撤出途中不因警戒线暂停；不可吃喝/用药/休息/睡眠/接战/探索。
    /// 已有：B21ExtractTests.DR07_NoFiveStatDrain_ZeroDamageContinues_VirusGrows（不暂停、五项不降、归零扣血）、DR05_SCB01_ReloadMidway_ResumesRemaining_NoSecondCharge。
    /// </summary>
    [Test]
    public void PLAN_Q199_01_Extract_NoPause_NoEatMedRestEngage()
    {
        var s = B21EntryTests.AtBuffer(E, 2991);
        UnlockCapacity(s, 2);
        Go(s, "d01_02", true);
        High(s);
        s.Player.Stats.Set(StatId.Satiety, 30);   // 扣 25 → 5，低于警戒线
        E.CreateItems(s, "clean_water", 1, ItemInstance.Carried);
        E.CreateItems(s, "bandage", 1, ItemInstance.Carried);
        Ok(E.AbortExpedition(s));
        foreach (var r in new[] { ActionRequest.UseItem("clean_water"), ActionRequest.UseItem("bandage"), ActionRequest.Rest(60), ActionRequest.Sleep(60), ActionRequest.Explore() })
            Assert.That(E.CanStart(s, r), Does.Contain("正在撤出"), r.Type.ToString());
        Assert.That(E.CanEngageFixed(s), Is.Not.Null, "不可接战");
        while (s.Main != null) Assert.That(E.Advance(s).Status, Is.Not.EqualTo(SegmentStatus.Paused), "警戒线不暂停");
        Assert.That(s.CurrentLocation, Is.EqualTo("c03_crypt"));
    }

    /// <summary>
    /// PLAN-Q199-01（整理部分）：答复“可做拾放等合法即时整理……不因这句禁止原整理入口”，游戏现状（02 文件 Q199）也写“拾取/放下等即时整理可以”。
    /// B22f 核对发现撤出途中 DoInstant 拾取/放下被 IsBusy 拦下，M426 起按答复放开（ExtractTidyAllowed）。
    /// </summary>
    [Test]
    public void PLAN_Q199_01_Extract_PickupDropAllowed()
    {
        var s = B21EntryTests.AtBuffer(E, 2992);
        UnlockCapacity(s, 2);
        Go(s, "d01_02", true);
        High(s);
        var onGround = E.CreateItems(s, "fiber", 1, "d01_02").Single();
        var carried = E.CreateItems(s, "wood", 1, ItemInstance.Carried).Single();
        Ok(E.AbortExpedition(s));
        E.Advance(s);
        Assert.That(s.Main?.Type, Is.EqualTo(ActionType.Extract));
        Assert.That(s.CurrentLocation, Is.EqualTo("d01_02"));
        var pick = E.DoInstant(s, InstantRequest.Pickup(onGround.InstanceId, 1));
        Assert.That(pick.Ok, Is.True, "撤出途中可拾取：" + pick.Reason);
        var drop = E.DoInstant(s, InstantRequest.Drop(carried.InstanceId, 1));
        Assert.That(drop.Ok, Is.True, "撤出途中可放下：" + drop.Reason);
    }

    // ───────────── Q200 预计探索时间 ─────────────

    /// <summary>
    /// PLAN-Q200-01：下限 = 01 到核心记录房最短路移动 + 沿途（不含记录房）剩余探索；上限 = 全房剩余探索 + 每条内部双向边一次；
    /// 按当前探索度重算、预告不自动探索；回撤最低 30 与当前 T。已有：B21EntryTests.DR01_PreviewNoInstance_BufferRoundTripFree。
    /// </summary>
    [Test]
    public void PLAN_Q200_01_ExploreEstimate_LowHigh_Recomputed()
    {
        var s = B21EntryTests.AtBuffer(E, 3001);
        var rooms = Cfg.Locations.Where(l => l.DungeonId == "d01").Select(l => l.Id).ToList();
        foreach (var r in rooms) E.DebugSetProgress(s, r, 100);
        var edges = Cfg.Edges.Where(e => e.Bidirectional && rooms.Contains(e.A) && rooms.Contains(e.B)).Sum(e => e.BaseMinutes);
        Assert.That(edges, Is.EqualTo(40), "d01 八条内部边各 5 分钟");
        Assert.That(E.ExploreEstimate(s, "d01"), Is.EqualTo((25, 40)), "全 100%：下限只剩 01→08 最短路 25，上限只剩走边");
        int Full(string n, int prog) => !E.Location(n).ExploreEnabled ? 0
            : (int)Math.Ceiling((100 - prog) / (double)Math.Max(1, E.ExploreIncrementAt(n))) * E.ExploreMinutesAt(n);
        E.DebugSetProgress(s, "d01_08", 0);
        Assert.That(E.ExploreEstimate(s, "d01"), Is.EqualTo((25, 40 + Full("d01_08", 0))), "记录房只进上限");
        E.DebugSetProgress(s, "d01_07", 40);
        Assert.That(Full("d01_07", 40), Is.GreaterThan(0));
        Assert.That(E.ExploreEstimate(s, "d01"), Is.EqualTo((25 + Full("d01_07", 40), 40 + Full("d01_07", 40) + Full("d01_08", 0))), "必经房按当前探索度计入下限与上限");
        var p = E.ExpeditionPreview(s, "d01");
        Assert.That((p.ExploreLow, p.ExploreHigh), Is.EqualTo(E.ExploreEstimate(s, "d01")));
        Assert.That(s.Locations["d01_07"].Progress, Is.EqualTo(40), "预告不自动探索");
        Assert.That(p.ExtractMinMinutes, Is.EqualTo(30));
        Assert.That(p.ExtractEstimate, Is.EqualTo(E.ComputeExtractPlan(s, "d01", "d01_01").TotalMinutes), "当前 T");
    }

    // ───────────── Q201 远征开始/结束边界 ─────────────

    /// <summary>
    /// PLAN-Q201-01 ④：通关后从 01 原路离开免罚；再进同一代的危险房仍要确认、开始新远征（e2），离开仍免罚。
    /// 已有：B21EntryTests.D3_ConfirmCommitsOnce_SnapshotAndTrail（①确认即开始、取消移动也算）、ClearedLeave_ByBuffer_EndsExpeditionFree（②）、
    /// B21StateTests.Migration_32To33_AtBuffer_NoExpedition / Migration_32To33_InDangerRoom_CommittedExpedition_Idempotent（③旧档）。
    /// </summary>
    [Test]
    public void PLAN_Q201_01_ClearedReenterSameGeneration_ConfirmNewExpedition_LeaveFree()
    {
        var s = B21EntryTests.AtBuffer(E, 3011);
        Go(s, "d01_02", true);
        var first = s.Expedition!.Id;
        Go(s, "d01_01");
        E.DebugInstanceFact(s, "d01", GameEngine.RecordClaimedFact);
        Go(s, "c03_crypt");
        Assert.That(s.ExpeditionLog.Single().EndKind, Is.EqualTo("cleared_exit"));
        Go(s, "d01_01");
        Assert.That(s.Expedition, Is.Null, "01 不开始");
        Assert.That(E.NeedsExpeditionConfirm(s, "d01_02"), Is.True, "通关后再进同代仍要确认");
        Assert.That(E.CanStart(s, ActionRequest.Travel("d01_02")), Is.EqualTo(GameEngine.ExpeditionConfirmReason));
        Go(s, "d01_02", true);
        Assert.That(s.Expedition!.Id, Is.Not.EqualTo(first).And.EndWith(":g1:e2"), "新远征、同一代");
        Go(s, "d01_01");
        var before = s.Stats.Clone();
        Go(s, "c03_crypt");
        Assert.That(s.Expedition, Is.Null);
        Assert.That(s.ExpeditionLog.Last().EndKind, Is.EqualTo("cleared_exit"));
        Assert.That(s.Stats.Satiety, Is.GreaterThan(before.Satiety - 2), "离开免罚");
        Assert.That(s.AppliedEventKeys, Is.Empty);
    }

    // ───────────── Q203 封存 ─────────────

    /// <summary>
    /// PLAN-Q203-01：本实例奖励在本远征非战斗时可从同行者（含主角）随身整堆 0 分钟封存；战斗/敌踪待选时拒绝；取出只到主角随身或当地地面（接口没有选队友）。
    /// 已有：B22SealedTests.DR10_FullCards_SealThenRecoveryBox（地面封存、取出验五卡/放地面、成功离开清标记）、DR12_OutsideOrCrafted_CannotSeal（外带/自制/别实例）、
    /// RewardOrigin_SeparateCardInside_MergesAfterLeaving（分卡与合组）。
    /// </summary>
    [Test]
    public void PLAN_Q203_01_SealFromCompanionCarried_ZeroMinutes_UnsealToPlayerOrGround()
    {
        var s = B21EntryTests.AtBuffer(E, 3031);
        UnlockCapacity(s, 2);
        E.DebugRecruit(s, Lin);
        Assert.That(s.Party, Does.Contain(Lin));
        Go(s, "d01_02", true);
        E.DebugWinFixedHere(s);
        var inst = s.Expedition!.InstanceId;
        var mine = E.CreateItems(s, "cloth", 2, ItemInstance.Carried).Single();
        var his = E.CreateItems(s, "leather", 1, ItemInstance.CarriedOwner(Lin)).Single();
        mine.RewardOrigin = inst;
        his.RewardOrigin = inst;
        Assert.That(E.SealableItems(s).Select(i => i.InstanceId), Is.EquivalentTo(new[] { mine.InstanceId, his.InstanceId }));
        s.Battle = new BattleState();
        Assert.That(E.SealBlocked(s, his.InstanceId), Does.Contain("战斗结束后"), "非战斗才能封存");
        s.Battle = null;
        var t = s.GameMinute;
        Ok(E.SealReward(s, his.InstanceId));
        Ok(E.SealReward(s, mine.InstanceId));
        Assert.That(s.GameMinute, Is.EqualTo(t), "0 分钟");
        Assert.That((his.Owner, his.Count, mine.Count), Is.EqualTo((GameEngine.SealedOwner(inst), 1, 2)), "整堆");
        Ok(E.UnsealReward(s, his.InstanceId, false));
        Assert.That(his.Owner, Is.EqualTo(ItemInstance.Carried), "取出到主角随身，不回林昭");
        Ok(E.UnsealReward(s, mine.InstanceId, true));
        Assert.That(mine.Owner, Is.EqualTo("d01_02"), "或当地地面");
        var unseal = typeof(GameEngine).GetMethod(nameof(GameEngine.UnsealReward))!;
        Assert.That(unseal.GetParameters().Select(p => p.Name), Has.None.Contain("actor"), "取出接口不选队友");
    }

    // ───────────── Q205 旧档 clearedAt ─────────────

    /// <summary>
    /// PLAN-Q205-01：旧档（存档 33）本代已核对记录者升级时以升级时刻记 clearedAt，71h59 不可重置、满 72h 可重置；之后再读档不重记升级时刻。
    /// 已有：B22SalvageFirstClearTests.Migration_33To34_ClearedAtFromUpgrade_FirstClearMarked（记升级时刻、首通补给记已领、幂等）。
    /// </summary>
    [Test]
    public void PLAN_Q205_01_Legacy33_ClearedAtUpgrade_Reset72hLater_ReloadNoRewrite()
    {
        var s = B21EntryTests.AtCrypt(E, 3051);
        UnlockCapacity(s, 3);
        E.DebugTeleport(s, "d01_08");
        E.DebugSetProgress(s, "d01_08", 100);
        E.DebugInstanceFact(s, "d01", "guardianDefeated");
        Start(s, ActionRequest.Gather("d01_08_unique"));
        Run(s);
        E.DebugTeleport(s, "c03_crypt");
        Assert.That(E.DungeonCleared(s, "d01"), Is.True);
        s.GameMinute += 600;   // 通关之后过了一阵才升级：clearedAt 记升级时刻，不倒推
        var j = JObject.Parse(SaveSerializer.Serialize(s));
        j["SchemaVersion"] = "33";
        foreach (var p in ((JObject)j["DungeonInstances"]!).Properties()) { ((JObject)p.Value).Remove("ClearedAt"); ((JObject)p.Value).Remove("ResetEligibleAt"); }
        var t = SaveSerializer.Deserialize(j.ToString(), Cfg);
        var up = t.GameMinute;
        Assert.That(t.DungeonInstances["d01"].ClearedAt, Is.EqualTo(up));
        t.GameMinute = up + 4319;
        Assert.That(E.DungeonResetReady(t, "d01"), Is.False, "不立刻可重置");
        var again = SaveSerializer.Deserialize(SaveSerializer.Serialize(t), Cfg);
        Assert.That(again.DungeonInstances["d01"].ClearedAt, Is.EqualTo(up), "再次读档不重记");
        again.GameMinute = up + 4320;
        Assert.That(E.DungeonResetReady(again, "d01"), Is.True, "升级后满 72 世界小时可重置");
        Assert.That(E.DungeonStatus(again, "d01"), Is.EqualTo(DungeonStatuses.ResetReady));
    }

    // ───────────── Q206 战术品 ─────────────

    /// <summary>
    /// PLAN-Q206-01：闪光标记器持续到使用轮 +2 轮末（第 +2 轮仍生效、第 +3 轮失效）；只加命中能力 6，不加伤害；提示贴同目标同轮多人合计最多减 6。
    /// 已有：B22TacticalTests.EC10_FlashMarker_TakeStrongest_Noise_NoXpNoWear、EC10_BracePack_TakeStrongest、EC10_RallyPatch_PerTargetPerRoundMax6、
    /// R01_C01_MarkNotStacked_Expires_NoPermanentPanel。
    /// </summary>
    [Test]
    public void PLAN_Q206_01_FlashMarkerDuration_AccuracyOnly_RallyPatchCap()
    {
        var s = Fight(3061, "shambler", "shambler");
        ToPlayer(s);
        Assert.That(E.AwaitingPlayer(s), Is.True);
        E.CreateItems(s, "flash_marker", 1, ItemInstance.Carried);
        E.CreateItems(s, "rally_patch", 1, ItemInstance.Carried);
        var b = s.Battle!;
        var t = b.Alive.First();
        var before = E.PreviewCommand(s, BattleCommand.Of("basic_attack", t.Index)).Targets.Single(x => x.Index == t.Index);
        var round = b.Round;
        Ok(E.ExecuteCommand(s, BattleCommand.Of("tactic_flash_marker", t.Index)));
        Assert.That(t.MarkUntilRound, Is.EqualTo(round + 2));
        b.PlayerAp = 3;
        var after = E.PreviewCommand(s, BattleCommand.Of("basic_attack", t.Index)).Targets.Single(x => x.Index == t.Index);
        Assert.That(after.AccuracyRating - before.AccuracyRating, Is.EqualTo(6).Within(1e-9), "命中能力+6");
        Assert.That(after.HitChance, Is.GreaterThan(before.HitChance));
        Assert.That((after.DamageMin, after.DamageMax, after.CritDamageMin, after.CritDamageMax), Is.EqualTo((before.DamageMin, before.DamageMax, before.CritDamageMin, before.CritDamageMax)), "不加伤害");
        b.Round = round + 2;
        Assert.That(GameEngine.EnemyMarked(b, t), Is.True, "使用轮+2 轮仍生效");
        b.Round = round + 3;
        Assert.That(GameEngine.EnemyMarked(b, t), Is.False, "+3 轮失效");

        // 提示贴：同一目标本轮已从别人那里减过 4，再贴只减 2
        b.Me.TacticalRound = 0;
        b.PlayerAp = 3;
        b.Panic = 20;
        var f = b.Fighter(GameState.PlayerId);
        f.RallyRound = b.Round;
        f.RallyAmount = 4;
        Ok(E.ExecuteCommand(s, BattleCommand.Of("tactic_rally_patch")));
        Assert.That(b.Panic, Is.EqualTo(18).Within(1e-9), "同目标同轮合计最多减 6");
        Assert.That(f.RallyAmount, Is.EqualTo(6).Within(1e-9));
    }
}
