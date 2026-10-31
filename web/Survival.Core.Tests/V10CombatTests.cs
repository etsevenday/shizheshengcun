using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 系统策划总册 v1.0：Q3/T01 受惊模型（R03 §2.1、R05 §6.3）、P01 逐命中目标经验（R01 §3）、
/// T02 教学词条“攻击力+0.1”的伤害结算层（R07 §4.1）、T08 普通丧尸 0.5% 附加活性物质（R07 §3）。
/// </summary>
public class V10CombatTests : BattleTestBase
{
    private void Ready(GameState s)
    {
        PlayerFirst(s);
        E.BattleStep(s);
        Assert.That(E.AwaitingPlayer(s), Is.True);
    }

    /// <summary>在荒地放一只敌人（冻结刷新巡游），不立即入战。</summary>
    private (GameState s, WorldEnemy w) Spot(uint seed, string enemyId, double morale = 60)
    {
        var s = New(seed);
        E.DebugTeleport(s, "field");
        s.GameMinute = At(2, 9, 0);
        s.SpawnNextCheck = int.MaxValue;
        s.PatrolNextCheck = int.MaxValue;
        s.WorldEnemies.Clear();
        s.Stats.Morale = morale;
        return (s, E.DebugSpawnEnemy(s, enemyId, "field"));
    }

    [Test]
    public void Fear_Formula_MatchesR03Examples()
    {
        Assert.That(E.FearChance(0.35, 60, GameEngine.FearEncounter), Is.EqualTo(0.14).Within(1e-12), "M60 游荡尸 随机14%");
        Assert.That(E.FearChance(0.35, 60, GameEngine.FearTutorial), Is.EqualTo(0.14).Within(1e-12), "教学同为 K=1");
        Assert.That(E.FearChance(0.35, 60, GameEngine.FearApproach), Is.EqualTo(0.07).Within(1e-12), "主动接近 7%");
        Assert.That(E.FearChance(0.35, 85, GameEngine.FearEncounter), Is.EqualTo(0.0525).Within(1e-12), "M85 5.25%");
        Assert.That(E.FearChance(0.65, 0, GameEngine.FearEncounter), Is.EqualTo(0.65).Within(1e-12), "M0 嚎叫尸 65%");
        Assert.That(E.FearChance(0.65, 100, GameEngine.FearEncounter), Is.EqualTo(0), "M100 不受惊");
        Assert.That(E.FearChance(0.65, 130, GameEngine.FearEncounter), Is.EqualTo(0), "精神夹紧到0—100");
    }

    [Test]
    public void Fear_Frightened_AddsFearValue_NotFixed10()
    {
        // 受惊：入场 P = 20 + 6 = 26
        var (s, w) = Spot(1, "shambler");
        Force(s, Below(0.14));
        Assert.That(E.Approach(s, w.Id).Ok, Is.True);
        Assert.That(s.Battle!.FearChance, Is.EqualTo(0.07).Within(1e-12), "主动接近 K=0.5");
        // 0.14 以下不一定低于 0.07，按实际结果断言
        var expectPanic = 20 + (s.Battle.Frightened ? 6 : 0);
        Assert.That(s.Battle.Panic, Is.EqualTo(expectPanic).Within(1e-9));

        var (t, w2) = Spot(2, "shambler");
        t.Encounter = new PendingEncounter { LocationId = "field", EnemyIds = { w2.Id }, Source = "explore", Minute = t.GameMinute };
        Force(t, Below(0.13));
        Assert.That(E.Engage(t).Ok, Is.True);
        Assert.That(t.Battle!.Frightened, Is.True);
        Assert.That(t.Battle.Panic, Is.EqualTo(26).Within(1e-9), "未受惊P20，受惊P26（R03 §2.1 算例）");

        var (u, w3) = Spot(3, "shambler");
        u.Encounter = new PendingEncounter { LocationId = "field", EnemyIds = { w3.Id }, Source = "explore", Minute = u.GameMinute };
        Force(u, AtLeast(0.15));
        Assert.That(E.Engage(u).Ok, Is.True);
        Assert.That(u.Battle!.Frightened, Is.False);
        Assert.That(u.Battle.Panic, Is.EqualTo(20).Within(1e-9), "未受惊不再固定+10");
    }

    [Test]
    public void Fear_MainSource_HighestChanceThenValueThenId()
    {
        var (s, a) = Spot(4, "shambler");
        var b = E.DebugSpawnEnemy(s, "howler", "field");
        var c = E.DebugSpawnEnemy(s, "worker", "field");
        Assert.That(E.FearSource(new[] { a, b, c })!.Id, Is.EqualTo(b.Id), "嚎叫尸65%最高");
        var d = E.DebugSpawnEnemy(s, "crawler", "field");
        Assert.That(E.FearSource(new[] { c, d })!.Id, Is.EqualTo(c.Id), "同为40%取惊吓值较高（工尸8>爬行尸7）");
        var e2 = E.DebugSpawnEnemy(s, "shambler", "field");
        Assert.That(E.FearSource(new[] { e2, a })!.Id, Is.EqualTo(Math.Min(a.Id, e2.Id)), "再平手按 worldEnemyId");
        // 多敌只判一次，不对每敌叠加
        s.Encounter = new PendingEncounter { LocationId = "field", EnemyIds = { a.Id, b.Id, c.Id }, Source = "explore", Minute = s.GameMinute };
        UnlockCapacity(s, 3);   // Q62：三只都入场需要 C3（新局 C1 只有排第一的入场、受惊只看入场者）
        s.Stats.Morale = 0;
        Force(s, Below(0.5));
        Assert.That(E.Engage(s).Ok, Is.True);
        Assert.That(s.Battle!.FearSourceWorldId, Is.EqualTo(b.Id));
        Assert.That(s.Battle.Panic, Is.EqualTo(50 + 16).Within(1e-9), "只加主惊吓源的16");
    }

    [Test]
    public void Fear_RollReusedWithin60Minutes_ThenRerolled()
    {
        var (s, w) = Spot(5, "shambler", morale: 0);
        Force(s, AtLeast(0.2), Any());
        Assert.That(E.Approach(s, w.Id).Ok, Is.True);
        var u0 = s.Battle!.FearRoll;
        Assert.That(s.Battle.Frightened, Is.False, "u≥0.175");
        var json = SaveSerializer.Serialize(s);
        var back = SaveSerializer.Deserialize(json, Cfg, out _);
        Assert.That(back.Battle!.FearRoll, Is.EqualTo(u0), "读档不重抽");
        Assert.That(back.Battle.Frightened, Is.False);
        // 撤退回世界后 60 分钟内再入战：复用同一个 u（即使存档随机数换了）
        s.Battle = null;
        w.Engaged = false;
        s.GameMinute += 60;
        Force(s, Below(0.01), Any());
        Assert.That(E.Approach(s, w.Id).Ok, Is.True);
        Assert.That(s.Battle!.FearRoll, Is.EqualTo(u0), "60 分钟内复用 u");
        Assert.That(s.Battle.Frightened, Is.False);
        // 超过 60 分钟：重新生成
        s.Battle = null;
        w.Engaged = false;
        s.GameMinute += 65;
        Force(s, Below(0.01), Any());
        Assert.That(E.Approach(s, w.Id).Ok, Is.True);
        Assert.That(s.Battle!.FearRoll, Is.Not.EqualTo(u0));
        Assert.That(s.Battle.Frightened, Is.True, "新 u<0.01<p");
    }

    [Test]
    public void Xp_PerHitTarget_CapPerTarget_ZeroDamageAndPushCount()
    {
        // 两目标都命中且未满：+4；其中一个已满：+2
        var f = Fight(41, "shambler", "shambler");
        Ready(f);
        var sg = Arm(f, "shotgun");
        StandAt(f, 2);   // Q80：霰弹枪使用位23
        sg.LoadedAmmo = 4;
        f.Skills["firearms"].Level = 7;   // B7：霰弹专长射击（取代散射）
        Enemy(f, 1).Hp = 50;
        Enemy(f, 2).Hp = 50;
        Enemy(f, 2).XpHits["firearms"] = 3;
        var xp0 = f.Skills["firearms"].Xp;
        Force(f, Below(0.4), AtLeast(0.1), Any(), Below(0.4), AtLeast(0.1), Any());
        Assert.That(Cmd(f, "firearms_07").Ok, Is.True);
        Assert.That(f.Skills["firearms"].Xp - xp0, Is.EqualTo(Cfg.Xp.DamagingAttackCommand), "满额目标不给，另一目标+2");
        Assert.That(Enemy(f, 1).XpHits["firearms"], Is.EqualTo(1));
        Assert.That(Enemy(f, 2).XpHits["firearms"], Is.EqualTo(3));

        // 未命中不计
        var m = Fight(42, "shambler");
        Ready(m);
        Force(m, AtLeast(0.99), Any());
        Assert.That(Cmd(m, "basic_attack", 1).Ok, Is.True);
        Assert.That(m.Skills["unarmed"].Xp, Is.EqualTo(0));

        // 推挡命中也计（有命中判定的技能动作）
        var p = Fight(43, "shambler");
        Ready(p);
        Enemy(p, 1).Hp = 50;   // 推挡：B7 起为拳3级 unarmed_03（12→1）
        p.Skills["unarmed"].Level = 3;
        Force(p, Below(0.01), AtLeast(0.99), Any());
        var r = Cmd(p, "unarmed_03", 1);
        Assert.That(r.Ok, Is.True, r.Reason);
        Assert.That(p.Skills["unarmed"].Xp, Is.EqualTo(Cfg.Xp.DamagingAttackCommand), "推挡命中+2");
    }

    [Test]
    public void FlatAttack_LayerMatchesR07_4_1()
    {
        var g = Cfg.CombatMath.GuardFactor;
        Assert.That(GameEngine.AttackDamage(1.20, 0, 0.30, false, g), Is.EqualTo(0.90).Within(1e-12));
        Assert.That(GameEngine.AttackDamage(1.20, 0.1, 0.30, false, g), Is.EqualTo(1.00).Within(1e-12));
        Assert.That(GameEngine.AttackDamage(1.20, 0, 0.30, true, g), Is.EqualTo(0.45).Within(1e-12));
        Assert.That(GameEngine.AttackDamage(1.20, 0.1, 0.30, true, g), Is.EqualTo(0.50).Within(1e-12));
        Assert.That(GameEngine.AttackDamage(1.80, 0.1, 0.30, false, g), Is.EqualTo(1.60).Within(1e-12), "暴击后1.80+0.10−0.30，不是(1.20+0.10)×1.5−0.30");
        Assert.That(GameEngine.AttackDamage(0.10, 0.1, 0.30, false, g), Is.EqualTo(0), "仍不补最低伤害");
    }

    [Test]
    public void FlatAttack_AppliedInBattle_NotToPush()
    {
        var s = Fight(44, "worker");
        Ready(s);
        var before = E.PreviewCommand(s, BattleCommand.Of("basic_attack", 1)).Targets.Single();
        s.Traits.Add(new TraitInstance { EffectId = "tutorial_attack_bonus_001", Quality = "green", Label = "攻击力+0.1", FlatAttack = 0.1 });
        var after = E.PreviewCommand(s, BattleCommand.Of("basic_attack", 1)).Targets.Single();
        // 工尸护甲0.3：拳 0.9—1.1 ×1.035 −0.3（低端可能被夹到0）
        Assert.That(after.DamageMax - before.DamageMax, Is.EqualTo(0.1).Within(1e-12));
        Assert.That(after.CritDamageMax - before.CritDamageMax, Is.EqualTo(0.1).Within(1e-12), "不被暴击再放大");
        // 实际结算：强制命中、不暴击、最大伤害
        var hp0 = Enemy(s, 1).Hp;
        Force(s, Below(0.2), AtLeast(0.6), AtLeast(0.999));
        Cmd(s, "basic_attack", 1);
        Assert.That(hp0 - Enemy(s, 1).Hp, Is.EqualTo(after.DamageMax).Within(0.01));

        var p = Fight(45, "shambler");
        Ready(p);
        p.Traits.Add(new TraitInstance { EffectId = "tutorial_attack_bonus_001", FlatAttack = 0.1 });
        p.Skills["unarmed"].Level = 3;   // B7：推挡为拳3级
        var push = E.PreviewCommand(p, BattleCommand.Of("unarmed_03", 1)).Targets.Single();
        Assert.That(push.DamageMax, Is.EqualTo(0), "推挡基础0伤不加0.1");
    }

    [Test]
    public void NormalSubstance_ZombieOnly_Independent05Pct_GrantedOnVictory()
    {
        // 击杀游荡尸：命中、不暴击、伤害、丧尸互斥掉落、活性物质抽签 < 0.5%
        var s = Fight(46, "shambler");
        Ready(s);
        Enemy(s, 1).Hp = 0.01;
        Force(s, Below(0.2), AtLeast(0.6), Any(), Any(), Below(0.004));
        Assert.That(Cmd(s, "basic_attack", 1).Ok, Is.True);
        Assert.That(s.Battle, Is.Null, "胜利");
        var sub = s.Items.Where(i => i.ItemId == "active_substance_white_unidentified").ToList();
        Assert.That(sub.Sum(i => i.Count), Is.EqualTo(1));
        Assert.That(sub[0].Owner, Is.EqualTo("field"), "落在当前地面");
        Assert.That(sub[0].Source!.TutorialReward, Is.False);
        Assert.That(sub[0].Source.BattleId, Is.Not.Null);
        Assert.That(sub[0].Source.WorldEnemyId, Is.Not.Null);

        // 抽签 ≥ 0.5% 不给
        var n = Fight(47, "shambler");
        Ready(n);
        Enemy(n, 1).Hp = 0.01;
        Force(n, Below(0.2), AtLeast(0.6), Any(), Any(), AtLeast(0.006));
        Cmd(n, "basic_attack", 1);
        Assert.That(n.Items.Any(i => i.ItemId == "active_substance_white_unidentified"), Is.False);

        // 动物：只有尸体，不抽活性物质
        var d = Fight(48, "feral_dog");
        Ready(d);
        Enemy(d, 1).Hp = 0.01;
        Force(d, Below(0.2), AtLeast(0.6), Any(), Below(0.001), Below(0.001));
        Cmd(d, "basic_attack", 1);
        if (d.Battle == null)
        {
            Assert.That(d.Items.Any(i => i.ItemId == "active_substance_white_unidentified"), Is.False, "动物不掉活性物质");
            Assert.That(d.Items.Any(i => i.ItemId == "carcass_feral_dog"), Is.True, "动物尸体照常");
        }
    }

    [Test]
    public void NormalSubstance_StatisticalRate_AndRetreatGetsNothing()
    {
        // 大量独立抽签：比例接近 0.5%，不是保底
        var s = New(99);
        var hits = 0;
        const int n = 40000;
        for (var k = 0; k < n; k++) if (s.Rng.NextDouble() < Cfg.ActiveSubstance.NormalDrop.ChancePerEnemy) hits++;
        Assert.That(hits / (double)n, Is.EqualTo(0.005).Within(0.0015));

        // 两只丧尸：第一只倒下并抽中，第二只存活时撤退 → 放弃全部掉落
        var r = Fight(49, "shambler", "shambler");
        Ready(r);
        Enemy(r, 1).Hp = 0.01;
        Force(r, Below(0.2), AtLeast(0.6), Any(), Any(), Below(0.004));
        Cmd(r, "basic_attack", 1);
        Assert.That(r.Battle!.Loot.Any(l => l.ItemId == "active_substance_white_unidentified"), Is.True, "死亡时锁计划");
        r.Battle.PlayerAp = Math.Max(1, r.Battle.PlayerAp);
        Force(r, Below(0.0001));
        if (Cmd(r, "retreat").Ok && r.Battle == null)
            Assert.That(r.Items.Any(i => i.ItemId == "active_substance_white_unidentified"), Is.False, "撤退不领");
    }

    [Test]
    public void SubstanceStacks_KeepSourceSeparate()
    {
        var s = New();
        var a = E.CreateItems(s, "active_substance_white_unidentified", 1, "camp", false, new ItemSource { GrantEventId = "tutorial-1", TutorialReward = true }).Single();
        var b = E.CreateItems(s, "active_substance_white_unidentified", 1, "camp", false, new ItemSource { GrantEventId = "substance-b2-w3", BattleId = 2, WorldEnemyId = 3 }).Single();
        Assert.That(s.Items.Count(i => i.ItemId == "active_substance_white_unidentified" && i.Owner == "camp"), Is.EqualTo(2), "不同来源不合堆");
        Assert.That(E.DoInstant(s, InstantRequest.Pickup(b.InstanceId, 1)).Ok, Is.True);
        Assert.That(E.DoInstant(s, InstantRequest.Pickup(a.InstanceId, 1)).Ok, Is.True);
        var carried = s.Items.Where(i => i.ItemId == "active_substance_white_unidentified" && i.IsCarried).ToList();
        Assert.That(carried, Has.Count.EqualTo(2), "转移后仍分开，来源不丢失");
        Assert.That(carried.Count(i => i.Source!.TutorialReward), Is.EqualTo(1));
        var json = SaveSerializer.Serialize(s);
        var back = SaveSerializer.Deserialize(json, Cfg, out _);
        Assert.That(back.Items.Count(i => i.Source?.TutorialReward == true), Is.EqualTo(1), "存读档保留来源");
    }
}
