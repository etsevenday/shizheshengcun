using Survival.Core.Config;
using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 B7（策划提交 f172677）R25–R27：50 个战斗/医疗主动技能执行、A09 §1 二十种新敌人与敌人字段、旧战斗规则回归。
/// 出处：A06 §2；R03 §1、§3、§4、敌方资源接口补充；A09 §1、§3、§4、最终敌方资源/人口接口；验收 SK01、SK04、SK05、SK07、SK08、EN01—EN08。
/// </summary>
public class B7CombatTests : BattleTestBase
{
    // ───────────── M140 数据 ─────────────

    [Test]
    public void SK01_EveryCombatNodeHasExec()
    {
        var combat = Cfg.SkillNodes.Where(n => !n.IsSurvival).Select(n => n.Id).OrderBy(x => x).ToList();
        Assert.That(combat, Has.Count.EqualTo(50), "九门战斗 + 医疗 = 50 个主动技能");
        Assert.That(Cfg.SkillExec.Select(x => x.Id).OrderBy(x => x), Is.EqualTo(combat));
        // A06 §2 抽样：连拳两击×0.85；割伤×0.65 正伤后流血；猎标无伤标记+6两轮、弹药1；连射两击、弹药2、单发弩不可用；贯穿相邻双位×0.65
        var ex = Cfg.SkillExec.ToDictionary(x => x.Id);
        Assert.That((ex["unarmed_10"].Hits, ex["unarmed_10"].DamageFactor), Is.EqualTo((2, 0.85)));
        Assert.That(ex["blade_03"].Effects.Single().Status, Is.EqualTo("bleed"));
        Assert.That(ex["blade_03"].Effects.Single().Positive, Is.True);
        Assert.That((ex["archery_05"].DamageFactor, ex["archery_05"].Ammo, ex["archery_05"].Effects.Single().Value, ex["archery_05"].Effects.Single().Rounds), Is.EqualTo((0.0, 1, 6.0, 2)));
        Assert.That((ex["archery_10"].Ammo, ex["archery_10"].MinAmmo, ex["archery_10"].NoSingleShot), Is.EqualTo((2, 2, true)));
        Assert.That((ex["spear_07"].TargetMode, ex["spear_07"].DamageFactor), Is.EqualTo(("pair", 0.65)));
        Assert.That(ex["medicine_10"].Items, Is.EquivalentTo(new Dictionary<string, int> { ["saline"] = 1, ["medical_gauze"] = 1 }));
        // 节点位置拆分：推挡 12→1、按武器为空
        var push = Cfg.SkillNodes.Single(n => n.Id == "unarmed_03");
        Assert.That((push.OwnPart, push.TargetPart), Is.EqualTo(("12", "1")));
        Assert.That(Cfg.SkillNodes.Single(n => n.Id == "firearms_01").TargetPart, Is.EqualTo(""));
    }

    [Test]
    public void WeaponTraits_SkillLinkedOnly()
    {
        var w = Cfg.Weapons.ToDictionary(x => x.Id);
        Assert.That(w["kitchen_knife"].Trait("bleedTriggers"), Is.EqualTo(1), "锋口：流血持续+1");
        Assert.That(w["machete"].Trait("bleedAccuracy"), Is.EqualTo(4), "扫切：流血命中能力+4");
        Assert.That(w["repeater_crossbow"].Trait("reloadMax"), Is.EqualTo(2), "快装：每次装填最多2发");
        Assert.That(w["stone_knife"].TraitEffects, Is.Empty, "轻巧挂在换位上（多人，B8）");
    }

    [Test]
    public void EN01_ThirtyEnemiesWithA09Fields()
    {
        var ids = Cfg.Enemies.Select(e => e.Id).ToList();
        Assert.That(ids, Has.Count.EqualTo(30));
        Assert.That(ids.Distinct().Count(), Is.EqualTo(30));
        var added = new[] { "z_slow", "z_helmet", "z_spitter", "z_hauler", "z_darter", "z_screamer", "z_guard", "z_clinger", "z_bloater", "z_nurse",
                            "z_engineer", "z_signal", "z_pump_foreman", "z_isolation_guard", "z_power_guard", "z_relay_guard",
                            "raider_melee", "raider_gunner", "wild_dog_leader", "feral_boar" };
        Assert.That(ids, Is.SupersetOf(added));
        var en = Cfg.Enemies.ToDictionary(e => e.Id);
        // 普通 3AP、精英 4AP（R03 敌方资源接口补充）
        Assert.That(Cfg.Enemies.Where(e => e.Elite).Select(e => e.Id), Is.EquivalentTo(new[] { "z_pump_foreman", "z_isolation_guard", "z_power_guard", "z_relay_guard" }));
        Assert.That(Cfg.Enemies.All(e => e.Ap == (e.Elite ? 4 : 3)), Is.True);
        // A09 §1 抽样：面板、己位→目标、控制抗性
        Assert.That((en["z_spitter"].Hp, en["z_spitter"].Dodge, en["z_spitter"].AttackRating, en["z_spitter"].OwnSlots, en["z_spitter"].TargetSlots, en["z_spitter"].ControlResist),
            Is.EqualTo((4.2, 28.0, 24.0, "34", "1234", 20.0)));
        Assert.That((en["z_relay_guard"].Hp, en["z_relay_guard"].Armor, en["z_relay_guard"].Speed, en["z_relay_guard"].TargetSlots), Is.EqualTo((14.0, 0.45, 5.0, "123")));
        Assert.That(en["shambler"].ControlResist, Is.EqualTo(10));
        Assert.That(en["riot"].ControlResist, Is.EqualTo(30));
        Assert.That((en["howler"].OwnSlots, en["howler"].TargetSlots), Is.EqualTo(("34", "12")));
        // A09 §4 受惊参数：默认35%/6，迅捷55%/10，尖啸65%/16，精英60%/14；旧10敌不改
        Assert.That((en["z_slow"].FearChance, en["z_slow"].FearValue), Is.EqualTo((0.35, 6.0)));
        Assert.That((en["z_darter"].FearChance, en["z_darter"].FearValue), Is.EqualTo((0.55, 10.0)));
        Assert.That((en["z_screamer"].FearChance, en["z_screamer"].FearValue), Is.EqualTo((0.65, 16.0)));
        Assert.That((en["z_power_guard"].FearChance, en["z_power_guard"].FearValue), Is.EqualTo((0.60, 14.0)));
        // 族类与附加掉落
        Assert.That(en["raider_gunner"].Family, Is.EqualTo("human"));
        Assert.That(en["raider_gunner"].ImmuneBleed, Is.False);
        Assert.That((en["z_slow"].ExtraDrop!.ItemId, en["z_slow"].ExtraDrop!.Chance), Is.EqualTo(("scrap", 0.20)));
        Assert.That((en["z_relay_guard"].ExtraDrop!.ItemId, en["z_relay_guard"].ExtraDrop!.Count, en["z_relay_guard"].ExtraDrop!.Chance), Is.EqualTo(("precision_parts", 2, 1.0)));
        Assert.That(Cfg.AnimalCarcasses.Single(c => c.EnemyId == "wild_dog_leader").CarcassItemId, Is.EqualTo("carcass_wolf"));
        Assert.That(Cfg.AnimalCarcasses.Single(c => c.EnemyId == "feral_boar").CarcassItemId, Is.EqualTo("carcass_boar"));
    }

    // ───────────── M141 攻击技能 ─────────────

    private void Ready(GameState s)
    {
        PlayerFirst(s);
        E.BattleStep(s);
        Assert.That(E.AwaitingPlayer(s), Is.True);
    }

    private static double Lv1(double e) => e / 1.025;

    [Test]
    public void SK07_EnergyByWeaponRatio_PositionsAndTargets()
    {
        var s = Fight(701, "feral_dog", "feral_dog", "feral_dog");
        Ready(s);
        Arm(s, "machete");                               // 刀剑类，武器E 0.5，基准 0.4
        s.Skills["blade"].Level = 3;
        var opts = E.BattleOptions(s);
        var basic = opts.Single(o => o.Action == "basic_attack");
        Assert.That((basic.Label, basic.Ap), Is.EqualTo(("斩击", 2)));
        Assert.That(basic.Energy, Is.EqualTo(E.SkillEnergyCost(0.5, 3)).Within(1e-9), "1级攻击技能=武器基础E");
        var cut = opts.Single(o => o.Action == "blade_03");
        Assert.That(cut.Energy, Is.EqualTo(E.SkillEnergyCost(0.5 * 0.5 / 0.4, 3)).Within(1e-9), "割伤 0.5×(0.5÷0.4)÷(1+0.025L)");
        Assert.That(cut.Targets, Is.EquivalentTo(new[] { 1, 2 }), "割伤 12→12");
        Assert.That(E.CommandBlocked(s, BattleCommand.Of("blade_03", 3)), Does.StartWith("射程不足").And.Contain("1、2号位"));
        // 拳：推挡 12→1（只打 1 号位）
        Arm(s, "machete").Owner = ItemInstance.Carried;
        s.Skills["unarmed"].Level = 3;
        Assert.That(E.CurrentWeapon(s, out _).IsFists, Is.True);
        Assert.That(E.BattleOptions(s).Single(o => o.Action == "unarmed_03").Targets, Is.EquivalentTo(new[] { 1 }));
    }

    [Test]
    public void SK08_CooldownSharedAcrossPages_AndPursuit()
    {
        var s = Fight(702, "feral_dog");
        Ready(s);
        Arm(s, "kitchen_knife");
        s.Skills["blade"].Level = 3;
        s.Battle!.PlayerAp = 5;
        Enemy(s, 1).Hp = 50;
        var r = s.Battle.Round;
        Force(s, AtLeast(0.99));
        Assert.That(Cmd(s, "blade_03", 1).Ok, Is.True);
        Assert.That(E.CommandBlocked(s, BattleCommand.Of("blade_03", 1)), Is.EqualTo($"冷却中（第{r + 2}轮起可用）"));
        E.SelectWeaponPage(s, 2);
        E.SelectWeaponPage(s, 1);
        Assert.That(E.CommandBlocked(s, BattleCommand.Of("blade_03", 1)), Does.StartWith("冷却中"), "切页不刷新冷却");
        s.Battle.Round = r + 1;
        Assert.That(E.CommandBlocked(s, BattleCommand.Of("blade_03", 1)), Does.StartWith("冷却中"), "CD1：r+1 仍不可用");
        s.Battle.Round = r + 2;
        Assert.That(E.CommandBlocked(s, BattleCommand.Of("blade_03", 1)), Is.Null, "r+2 可用");
    }

    [Test]
    public void Cut_BleedWithEdgeTrait_ZombieImmune()
    {
        var s = Fight(703, "feral_dog");
        Ready(s);
        Arm(s, "kitchen_knife");
        s.Skills["blade"].Level = 3;
        Enemy(s, 1).Hp = 50;
        Force(s, Below(0.01), AtLeast(0.99), Any());
        Assert.That(Cmd(s, "blade_03", 1).Ok, Is.True);
        Assert.That(Enemy(s, 1).BleedTriggers, Is.EqualTo(4), "锋口：流血持续+1（3→4，仍不叠强度）");
        Assert.That(s.Battle!.Log.Last(), Does.Contain("出血"));

        var z = Fight(704, "shambler");
        Ready(z);
        Arm(z, "stone_knife");
        z.Skills["blade"].Level = 3;
        Enemy(z, 1).Hp = 50;
        Force(z, Below(0.01), AtLeast(0.99), Any());
        Assert.That(Cmd(z, "blade_03", 1).Ok, Is.True);
        Assert.That(Enemy(z, 1).BleedTriggers, Is.EqualTo(0));
        Assert.That(z.Battle!.Log.Last(), Does.Contain("不会流血"));
    }

    [Test]
    public void SK05_MultiHit_XpOncePerTarget_PairHitsTwo()
    {
        var s = Fight(705, "worker");
        Ready(s);
        s.Skills["unarmed"].Level = 10;
        s.Skills["unarmed"].Xp = 0;
        s.Battle!.PlayerAp = 3;
        Enemy(s, 1).Hp = 50;
        var xp0 = s.Skills["unarmed"].Xp;
        Force(s, Below(0.5), AtLeast(0.9), Any(), Below(0.5), AtLeast(0.9), Any());
        Assert.That(Cmd(s, "unarmed_10", 1).Ok, Is.True);
        Assert.That(s.Battle.Log.Last(), Does.Contain("第1击").And.Contain("第2击"));
        Assert.That(Enemy(s, 1).XpHits["unarmed"], Is.EqualTo(1), "连拳对同一目标只计1次");

        var p = Fight(706, "feral_dog", "feral_dog", "feral_dog");
        Ready(p);
        Arm(p, "wooden_spear");
        p.Skills["spear"].Level = 7;
        foreach (var e in p.Battle!.Alive) e.Hp = 50;
        var pv = E.PreviewCommand(p, BattleCommand.Of("spear_07", 3));
        Assert.That(pv.Targets.Select(t => t.Slot), Is.EqualTo(new[] { "2", "3" }), "点3号位打23");
        Assert.That(E.PreviewCommand(p, BattleCommand.Of("spear_07", 2)).Targets.Select(t => t.Slot), Is.EqualTo(new[] { "1", "2" }), "点2号位打12");
        Force(p, Below(0.5), AtLeast(0.9), Any(), Below(0.5), AtLeast(0.9), Any());
        Assert.That(Cmd(p, "spear_07", 3).Ok, Is.True);
        Assert.That(Enemy(p, 2).XpHits["spear"] + Enemy(p, 3).XpHits["spear"], Is.EqualTo(2), "群攻不同目标独立计");
    }

    [Test]
    public void MarkArmorBreakWeak_StatusesApplied()
    {
        // 猎标：命中无伤、标记2轮，命中能力+6（稳弓再+5）
        var s = Fight(707, "worker");
        Ready(s);
        Arm(s, "recurve_bow");
        E.DebugGive(s, "arrow", 5);
        s.Skills["archery"].Level = 5;
        var before = E.PreviewCommand(s, BattleCommand.Of("basic_attack", 1)).Targets.Single().AccuracyRating;
        var hp = Enemy(s, 1).Hp;
        Force(s, Below(0.01), AtLeast(0.99), Any());
        Assert.That(Cmd(s, "archery_05", 1).Ok, Is.True);
        Assert.That(Enemy(s, 1).Hp, Is.EqualTo(hp), "猎标无伤");
        Assert.That((Enemy(s, 1).MarkBonus, Enemy(s, 1).MarkUntilRound), Is.EqualTo((6.0, s.Battle!.Round + 2)));
        var after = E.PreviewCommand(s, BattleCommand.Of("basic_attack", 1)).Targets.Single().AccuracyRating;
        Assert.That(after, Is.GreaterThan(before), "标记后命中能力提高（+6，稳弓+5）");

        // 破甲劈：护甲−0.25（破障斧+0.1）2轮
        var a = Fight(708, "worker");
        Ready(a);
        Arm(a, "breach_axe");
        a.Skills["axe"].Level = 3;
        Enemy(a, 1).Hp = 50;
        Force(a, Below(0.01), AtLeast(0.99), Any());
        Assert.That(Cmd(a, "axe_03", 1).Ok, Is.True);
        Assert.That(Enemy(a, 1).ArmorBreak, Is.EqualTo(0.35).Within(1e-9));
        Assert.That(E.PreviewCommand(a, BattleCommand.Of("basic_attack", 1)).Targets.Single().Armor, Is.EqualTo(0).Within(1e-9), "0.3−0.35 最低0");

        // 重锤：控制成功施加虚弱（敌人直接伤害×0.8）
        var w = Fight(709, "worker");
        Ready(w);
        Arm(w, "crowbar");
        w.Skills["blunt"].Level = 10;
        w.Battle!.PlayerAp = 3;
        Enemy(w, 1).Hp = 50;
        Force(w, Below(0.01), AtLeast(0.99), Any(), Below(0.01));
        Assert.That(Cmd(w, "blunt_10", 1).Ok, Is.True);
        Assert.That(GameEngine.EnemyWeak(w.Battle, Enemy(w, 1)), Is.True);
        Assert.That(w.Battle.Log.Last(), Does.Contain("虚弱"));
    }

    [Test]
    public void KnockbackInterruptExecuteBreakGuard()
    {
        // 逼退：控制成功后与身后同伴换位
        var s = Fight(710, "feral_dog", "wolf");
        Ready(s);
        Arm(s, "wooden_spear");
        s.Skills["spear"].Level = 3;
        Enemy(s, 1).Hp = 50;
        Force(s, Below(0.01), AtLeast(0.99), Any(), Below(0.01));
        Assert.That(Cmd(s, "spear_03", 1).Ok, Is.True);
        Assert.That((Enemy(s, 1).Pos, Enemy(s, 2).Pos), Is.EqualTo((2, 1)));

        // 打断：取消野猪本轮尚未执行的蓄力，改普通攻击
        var b = Fight(711, "boar");
        Ready(b);
        Arm(b, "crowbar");
        b.Skills["blunt"].Level = 7;
        Enemy(b, 1).Hp = 50;
        Assert.That(Enemy(b, 1).Intent, Is.EqualTo("charge_prepare"));
        Force(b, Below(0.01), AtLeast(0.99), Any(), Below(0.01));
        Assert.That(Cmd(b, "blunt_07", 1).Ok, Is.True);
        Assert.That(Enemy(b, 1).Intent, Is.EqualTo("attack"));

        // 处决：目标 H≤30% 时再×1.2；破阵：先移除格挡
        var x = Fight(712, "riot");
        Ready(x);
        Arm(x, "kitchen_knife");
        x.Skills["blade"].Level = 10;
        x.Battle!.PlayerAp = 3;
        var full = E.PreviewCommand(x, BattleCommand.Of("blade_10", 1)).Targets.Single().Multiplier;
        Enemy(x, 1).Hp = 2;
        var low = E.PreviewCommand(x, BattleCommand.Of("blade_10", 1)).Targets.Single().Multiplier;
        Assert.That(low / full, Is.EqualTo(1.2).Within(1e-9));
        var g = Fight(713, "riot");
        Ready(g);
        Arm(g, "hatchet");
        g.Skills["axe"].Level = 10;
        g.Battle!.PlayerAp = 3;
        Enemy(g, 1).Guard = true;
        Assert.That(E.PreviewCommand(g, BattleCommand.Of("axe_10", 1)).Targets.Single().Guarded, Is.False, "破阵无视格挡");
        Assert.That(E.PreviewCommand(g, BattleCommand.Of("basic_attack", 1)).Targets.Single().Guarded, Is.True);
    }

    [Test]
    public void RapidShot_SingleShotCrossbowBlocked_AmmoTwo()
    {
        var s = Fight(714, "feral_dog");
        Ready(s);
        var cb = Arm(s, "crossbow");
        cb.LoadedAmmo = 1;
        s.Skills["archery"].Level = 10;
        s.Battle!.PlayerAp = 3;
        Assert.That(E.CommandBlocked(s, BattleCommand.Of("archery_10", 1)), Does.Contain("单发弩"));
        var bow = Arm(s, "wooden_bow");
        E.DebugGive(s, "arrow", 1);
        Assert.That(E.CommandBlocked(s, BattleCommand.Of("archery_10", 1)), Does.Contain("不足2支"));
        E.DebugGive(s, "arrow", 3);
        Assert.That(E.CommandBlocked(s, BattleCommand.Of("archery_10", 1)), Is.Null);
        Enemy(s, 1).Hp = 50;
        Assert.That(Cmd(s, "archery_10", 1).Ok, Is.True);
        Assert.That(s.Items.Where(i => i.IsCarried && i.ItemId == "arrow").Sum(i => i.Count), Is.EqualTo(2), "连射弹药2");
    }

    [Test]
    public void DesperatePunch_ThresholdIsCheapestUnlockedSkill()
    {
        var s = Fight(715, "shambler");
        Ready(s);
        s.Skills["unarmed"].Level = 3;
        // 拳页：拳击 0.3、推挡 0.25 → 门槛为推挡
        Assert.That(E.MinEquippedAttackEnergy(s), Is.EqualTo(E.SkillEnergyCost(0.25, 3)).Within(1e-9));
        s.Stats.Energy = E.SkillEnergyCost(0.25, 3) - 0.01;
        Assert.That(E.CommandBlocked(s, BattleCommand.Of("desperate_punch", 1)), Is.Null);
    }

    [Test]
    public void NewEnemies_NotInOldSpawnPools()
    {
        var old = new[] { "shambler", "crawler", "runner", "worker", "riot", "howler", "feral_dog", "wolf", "boar", "stag" };
        var pooled = Cfg.WorldEnemies.Pools.Values.SelectMany(p => p.Values).SelectMany(l => l).Select(w => w.EnemyId).Distinct();
        Assert.That(pooled, Is.SubsetOf(old), "新敌只入数据，不改旧 8 地点刷新池（开发假设 B7-14）");
    }
}
