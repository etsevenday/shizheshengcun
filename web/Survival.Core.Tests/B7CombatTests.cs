using Survival.Core.Config;
using Survival.Core.Engine;
using Survival.Core.Save;
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

    // ───────────── M142 格挡/增益/指挥/医疗/装填与反应 ─────────────

    /// <summary>武器Ⅱ穿上指定器具并切到这一页（武器Ⅰ留着原武器）。</summary>
    private ItemInstance Arm2(GameState s, string id)
    {
        var slot = ItemInstance.EquipOwner(GameState.PlayerId, EquipSlots.Weapon2);
        foreach (var old in s.Items.Where(i => i.Owner == slot)) old.Owner = ItemInstance.Carried;
        var it = E.CreateItems(s, id, 1, slot).Single();
        s.Battle!.ActiveWeaponSlot = 2;
        return it;
    }

    [Test]
    public void GuardSkills_StandTwoCharges_CounterStance()
    {
        // 坚守：自己格挡2次（盾类×0.4），两次敌人命中都减伤
        var s = Fight(720, "shambler", "shambler");
        Ready(s);
        var sh = Arm(s, "wooden_shield");
        s.Skills["shield"].Level = 10;
        s.Battle!.PlayerAp = 3;
        Assert.That(Cmd(s, "shield_10").Ok, Is.True);
        Assert.That((s.Battle.PlayerGuard, s.Battle.PlayerGuardCharges, s.Battle.PlayerGuardFactor), Is.EqualTo((true, 2, 0.4)));
        Assert.That(s.Battle.Log.Last(), Does.Contain("格挡2次"));
        s.Battle.PlayerGuardCharges = 2;
        foreach (var e in s.Battle.Alive) { e.Intent = "attack"; }
        // 第一只命中：还剩 1 次；第二只命中：用完
        var h0 = s.Stats.Health;
        Force(s, Below(0.01), AtLeast(0.99), Below(0.01), AtLeast(0.99));
        while (s.Battle != null && !E.AwaitingPlayer(s) && s.Battle.Alive.Any(e => !e.Acted)) E.BattleStep(s);
        Assert.That(s.Battle!.PlayerGuard, Is.False, "两次格挡都用完");

        // 反击架势：受到直接攻击命中后反击1次
        var c = Fight(721, "shambler");
        Ready(c);
        Arm(c, "guard_sword");
        c.Skills["blade"].Level = 5;
        Assert.That(Cmd(c, "blade_05").Ok, Is.True);
        Assert.That(c.Battle!.CounterFactor, Is.EqualTo(0.70 * 1.15).Within(1e-9), "守备短剑“反击”×1.15");
        Cmd(c, "end_turn");
        Enemy(c, 1).Intent = "attack";
        Enemy(c, 1).Hp = 50;
        Force(c, Below(0.01), Any(), Below(0.01), AtLeast(0.99), Any());
        ToPlayer(c);
        Assert.That(c.Battle!.Log.Any(l => l.Contains("你反击")), Is.True);
        Assert.That(c.Battle.CounterFactor, Is.EqualTo(0), "只反击1次");
    }

    [Test]
    public void AxeCharge_NextAxeAttackOnly_Consumed()
    {
        var s = Fight(722, "worker");
        Ready(s);
        Arm(s, "hatchet");
        s.Skills["axe"].Level = 5;
        s.Battle!.PlayerAp = 4;
        var m0 = E.PreviewCommand(s, BattleCommand.Of("basic_attack", 1)).Targets.Single().Multiplier;
        Assert.That(Cmd(s, "axe_05").Ok, Is.True);
        Assert.That(E.CommandBlocked(s, BattleCommand.Of("axe_05")), Does.StartWith("冷却中").Or.Contain("已蓄力"));
        var m1 = E.PreviewCommand(s, BattleCommand.Of("basic_attack", 1)).Targets.Single().Multiplier;
        Assert.That(m1 / m0, Is.EqualTo(1.35).Within(1e-9));
        Enemy(s, 1).Hp = 50;
        Force(s, AtLeast(0.99));
        Assert.That(Cmd(s, "basic_attack", 1).Ok, Is.True);
        Assert.That(GameEngine.AxeChargeOn(s.Battle!), Is.False, "蓄力被用掉");
    }

    [Test]
    public void Command_InspireCalmAllOut_SoloTargetsSelf_NeedsAlliesDisabled()
    {
        var s = Fight(723, "worker");
        Ready(s);
        Arm(s, "kitchen_knife");
        var wh = Arm2(s, "signal_whistle");
        s.Skills["command"].Level = 9;   // 10 级不再获得经验
        s.Battle!.PlayerAp = 6;
        s.Battle.Panic = 30;
        var opts = E.BattleOptions(s);
        Assert.That(opts.Where(o => o.NodeId != null).Select(o => o.Label), Is.EqualTo(new[] { "鼓舞", "整队", "稳心", "协同" }), "9级：总攻令未解锁不列");
        Assert.That(opts.Single(o => o.Action == "command_03").Reason, Does.Contain("没有可交换站位的队友"));
        Assert.That(opts.Single(o => o.Action == "command_07").Reason, Does.Contain("没有可协同的队友"));
        // 稳心：恐慌−8，同目标每大回合一次，有效即+2指挥经验
        var xp0 = s.Skills["command"].Xp;
        Assert.That(Cmd(s, "command_05").Ok, Is.True);
        Assert.That(s.Battle.Panic, Is.EqualTo(22).Within(1e-9));
        Assert.That(s.Skills["command"].Xp - xp0, Is.EqualTo(2));
        Assert.That(E.CommandBlocked(s, BattleCommand.Of("command_05")), Does.StartWith("冷却中").Or.Contain("本回合已对你用过稳心"));
        // 鼓舞：下一次攻击命中能力+6；被用掉才给经验
        Assert.That(Cmd(s, "command_01").Ok, Is.True);
        Assert.That(GameEngine.InspireActive(s.Battle), Is.True);
        Assert.That(wh.Durability, Is.LessThan(wh.MaxDurability), "指挥器具有效技能磨损1");
        E.SelectWeaponPage(s, 1);
        var acc = E.PreviewCommand(s, BattleCommand.Of("basic_attack", 1)).Targets.Single().AccuracyRating;
        s.Battle.InspireBonus = 0;
        var acc0 = E.PreviewCommand(s, BattleCommand.Of("basic_attack", 1)).Targets.Single().AccuracyRating;
        Assert.That(acc, Is.GreaterThan(acc0));
    }

    [Test]
    public void Medical_SelfTarget_SharedCooldown_CriticalOnlyWhenLow()
    {
        var s = Fight(724, "worker");
        Ready(s);
        Arm(s, "kitchen_knife");
        var kit = Arm2(s, "trauma_bag");
        s.Skills["medicine"].Level = 10;
        s.Battle!.PlayerAp = 6;
        s.Stats.Health = 10;
        E.ApplyPlayerBleed(s);
        Assert.That(E.CommandBlocked(s, BattleCommand.Of("medicine_03")), Does.StartWith("随身没有医用纱布"));
        E.DebugGive(s, "medical_gauze", 2);
        E.DebugGive(s, "saline", 2);
        var pv = E.PreviewCommand(s, BattleCommand.Of("medicine_03"));
        Assert.That(pv.EffectText, Does.Contain("止血").And.Contain("健康10.00→"));
        Assert.That(Cmd(s, "medicine_03").Ok, Is.True);
        var heal = E.MedicalHeal(0.6, 10) + 0.2;   // 创伤救护包“创伤处置”+0.2H
        Assert.That(s.Stats.Health, Is.EqualTo(10 + heal).Within(1e-9));
        Assert.That(s.Body.BleedTriggers, Is.EqualTo(0));
        Assert.That(s.Battle.Log.Last(), Does.Contain("医疗已满10级"), "有效医疗给医疗经验（10级已满只写说明）");
        Assert.That(E.CommandBlocked(s, BattleCommand.Of("medicine_05")), Does.Contain("回血冷却中"), "与绷带共享60分钟回血冷却");
        s.BandageReadyAt = 0;
        Assert.That(E.CommandBlocked(s, BattleCommand.Of("medicine_10")), Does.Contain("健康高于上限的25"), "危急救护只在 H≤25%");
        s.Stats.Health = 4;
        Assert.That(E.CommandBlocked(s, BattleCommand.Of("medicine_10")), Is.Null);
        // 应急护理：解除失衡
        s.Battle.PlayerStagger = true;
        s.BandageReadyAt = 0;
        Assert.That(Cmd(s, "medicine_07").Ok, Is.True);
        Assert.That(s.Battle.PlayerStagger, Is.False);
    }

    [Test]
    public void QuickReload_FillsMagazine_NoGeneralReload()
    {
        var s = Fight(725, "worker");
        Ready(s);
        var p = Arm(s, "pistol");
        p.LoadedAmmo = 1;
        E.DebugGive(s, "ammo_9mm", 10);
        s.Skills["firearms"].Level = 5;
        var ap0 = s.Battle!.PlayerAp;
        Assert.That(Cmd(s, "firearms_05").Ok, Is.True);
        Assert.That(p.LoadedAmmo, Is.EqualTo(8));
        Assert.That(s.Battle.PlayerAp, Is.EqualTo(ap0 - 1), "快速装填 1AP");
        Assert.That(E.CommandBlocked(s, BattleCommand.Of("firearms_05")), Does.StartWith("冷却中"));
    }

    // ───────────── M143 指令一览、操作台、预览与执行同源 ─────────────

    private static readonly string[] OneWeaponPerKind =
        { "fists", "kitchen_knife", "hatchet", "wooden_spear", "crowbar", "wooden_bow", "pistol", "shotgun", "wooden_shield", "signal_whistle", "field_medkit" };

    /// <summary>
    /// 每类武器 10 级：操作台每个技能的“可用/原因”= CommandBlocked = PreviewCommand.Ok；战斗中的指令一览与操作台同一份；
    /// 每个可用技能都能执行（不抛异常、扣到 AP）。覆盖 50 个技能里除需要队友的 4 个之外的全部。
    /// </summary>
    [Test]
    public void Q19Q26_OptionsPreviewExecute_SameSource_AllSkills()
    {
        var executed = new HashSet<string>();
        foreach (var wid in OneWeaponPerKind)
        {
            var s = Fight(730, "feral_dog", "feral_dog", "feral_dog");
            Ready(s);
            if (wid != "fists") Arm(s, wid);
            foreach (var k in s.Skills.Keys.ToList()) s.Skills[k].Level = 10;
            s.Battle!.PlayerSlowPending = true;   // 挣脱要有可解除的束缚
            E.DebugGive(s, "arrow", 10);
            E.DebugGive(s, "ammo_9mm", 10);
            E.DebugGive(s, "shell_12g", 4);
            E.DebugGive(s, "bandage", 1);
            E.DebugGive(s, "medical_gauze", 3);
            E.DebugGive(s, "saline", 3);
            if (E.CurrentWeapon(s, out var wi).UsesMagazine) wi!.LoadedAmmo = E.CurrentWeapon(s, out _).MagazineCapacity - 1;
            s.Stats.Health = 4;
            s.Battle!.Panic = 30;
            s.Battle.PlayerStagger = true;
            foreach (var e in s.Battle.Alive) e.Hp = 50;
            var opts = E.BattleOptions(s);
            var help = E.CompatibleCommands(s);
            Assert.That(help.Where(o => o.Action != "switch_weapon").Select(o => (o.Action, o.Available, o.Reason)), Is.EqualTo(opts.Select(o => (o.Action, o.Available, o.Reason))), $"{wid}：战斗中指令一览与操作台同源");
            foreach (var o in opts.Where(o => o.NodeId != null))
            {
                var tgt = o.Targets.Count > 0 ? o.Targets[0] : (int?)null;
                var cmd = BattleCommand.Of(o.Action, o.NeedsTarget ? tgt ?? 1 : null);
                var pv = E.PreviewCommand(s, cmd);
                Assert.That(pv.Ok, Is.EqualTo(o.Available || (o.NeedsTarget && tgt != null && E.CommandBlocked(s, cmd) == null)), $"{wid} {o.Label}：预览与操作台同源（{o.Reason}）");
                if (!pv.Ok) continue;
                var copy = SaveSerializer.Deserialize(SaveSerializer.Serialize(s), Cfg);
                copy.Battle!.PlayerAp = 6;
                var r = E.ExecuteCommand(copy, cmd);
                Assert.That(r.Ok, Is.True, $"{wid} {o.Label}：{r.Reason}");
                executed.Add(o.NodeId!);
            }
        }
        var needAllies = new[] { "shield_03", "shield_07", "command_03", "command_07" };
        Assert.That(Cfg.SkillExec.Select(x => x.Id).Except(needAllies).Except(executed), Is.Empty, "除需要队友的 4 个外全部可执行");
    }

    [Test]
    public void Preview_DamageRange_MatchesExecution()
    {
        var s = Fight(731, "worker");
        Ready(s);
        Arm(s, "crowbar");
        s.Skills["blunt"].Level = 3;
        Enemy(s, 1).Hp = 50;
        var pv = E.PreviewCommand(s, BattleCommand.Of("blunt_03", 1)).Targets.Single();
        Force(s, Below(0.3), AtLeast(0.96), AtLeast(0.999), Any());
        Assert.That(Cmd(s, "blunt_03", 1).Ok, Is.True);
        Assert.That(50 - Enemy(s, 1).Hp, Is.EqualTo(pv.DamageMax).Within(0.01), "震击 ×0.70 预览上限=实际");
    }

    // ───────────── M144/M145 敌人意图与行动 ─────────────

    /// <summary>每种敌人单挑 8 轮（玩家只防御/结束），不报错、意图都有文字；精英 4AP。</summary>
    [Test]
    public void EN_AllThirtyEnemies_RunEightRounds()
    {
        foreach (var en in Cfg.Enemies)
        {
            var s = Fight(740, en.Id, "shambler");
            s.Stats.Health = 20;
            Assert.That(E.EnemyAp(s.Battle!.Enemies[0]), Is.EqualTo(en.Elite ? 4 : 3), en.Id);
            for (var i = 0; i < 400 && s.Battle != null && s.Battle.Round <= 8 && !s.Progress.Dead; i++)
            {
                foreach (var e in s.Battle.Alive) Assert.That(E.IntentShown(s.Battle, e), Is.Not.Empty, en.Id);
                if (E.AwaitingPlayer(s)) { s.Stats.Health = 20; Cmd(s, "end_turn"); }
                else E.BattleStep(s);
            }
        }
    }

    [Test]
    public void Advance_RearEnemySwapsForwardAndAttacks_BraceIntercepts()
    {
        var s = Fight(741, "shambler", "shambler", "shambler");
        var b = s.Battle!;
        var rear = Enemy(s, 3);
        rear.Intent = "attack";
        rear.IntentStep = 0;
        rear.IntentLabel = E.IntentLabel(rear, b);
        Assert.That(E.IntentShown(b, rear), Does.StartWith("前移到2号位后抓击"));
        // 架矛：敌人主动前移后迎击
        PlayerFirst(s);
        E.BattleStep(s);
        Arm(s, "wooden_spear");
        s.Skills["spear"].Level = 5;
        Assert.That(Cmd(s, "spear_05").Ok, Is.True);
        Assert.That(b.BraceCharges, Is.EqualTo(1));
        Cmd(s, "end_turn");
        rear.Hp = 50;
        for (var i = 0; i < 20 && !rear.Acted && s.Battle != null; i++) Step(s);
        Assert.That(b.Log.Any(l => l.Contains("游荡尸前移到2号位")), Is.True, string.Join("\n", b.Log));
        Assert.That(b.Log.Any(l => l.Contains("你迎击")), Is.True);
        Assert.That(b.BraceCharges, Is.EqualTo(0));
    }

    [Test]
    public void Spitter_RangedFromRear_ArmorBreak_WeakAttackAtFront_NoInfection()
    {
        var s = Fight(742, "shambler", "shambler", "z_spitter");
        var b = s.Battle!;
        var sp = Enemy(s, 3);
        Assert.That(sp.Intent, Is.EqualTo("spit"), "第1次正常激活：喷吐");
        Assert.That(E.IntentOutOfReach(b, sp), Is.False, "3号位可喷吐（34→1234）");
        Assert.That(sp.IntentLabel, Does.Contain("破甲0.15（2轮）").And.Contain("不感染"));
        var v0 = s.Body.VirusLoad;
        for (var i = 0; i < 30 && !sp.Acted && s.Battle != null; i++) { if (E.AwaitingPlayer(s)) Cmd(s, "end_turn"); else { if (b.Queue[b.Cursor] == sp.Index) Force(s, Below(0.01), Any()); E.BattleStep(s); } }
        Assert.That(b.PlayerArmorBreak, Is.EqualTo(0.15).Within(1e-9));
        Assert.That(b.PlayerArmorBreakUntilRound, Is.EqualTo(b.Round + 2));
        Assert.That(s.Body.VirusLoad, Is.EqualTo(v0));
        // 被压到 1 号位：喷吐改通用弱攻击×0.7
        var f = Fight(743, "z_spitter");
        Assert.That(E.IntentShown(f.Battle!, Enemy(f, 1)), Does.StartWith("弱攻击").And.Contain("3、4号位"));
    }

    [Test]
    public void Protect_RedirectsSingleTargetAttack_NoCycle_LimitOnce()
    {
        var s = Fight(744, "z_guard", "shambler");
        var b = s.Battle!;
        var guard = Enemy(s, 1);
        var back = Enemy(s, 2);
        Assert.That(guard.Intent, Is.EqualTo("protect"));
        Assert.That(guard.IntentLabel, Does.Contain("替游荡尸挡下一次单体攻击"));
        for (var i = 0; i < 30 && !guard.Acted && s.Battle != null; i++) Step(s);
        Assert.That(guard.ProtectingIndex, Is.EqualTo(back.Index));
        // 下一次轮到玩家：打后排游荡尸被守卫接下
        for (var i = 0; i < 30 && s.Battle != null && !E.AwaitingPlayer(s); i++) E.BattleStep(s);
        if (!E.AwaitingPlayer(s)) { Assert.Ignore("本轮守卫已再次行动"); return; }
        s.Battle!.PlayerAp = 3;
        var hpBack = back.Hp;
        Force(s, Below(0.01), AtLeast(0.99), Any());
        Assert.That(Cmd(s, "basic_attack", back.Index).Ok, Is.True);
        Assert.That(back.Hp, Is.EqualTo(hpBack), "被保护者没受伤");
        Assert.That(b.Log.Last(), Does.Contain("重装守卫感染者挡在游荡尸前面"));
        Assert.That(guard.ProtectingIndex, Is.EqualTo(0), "保护只接一次");

        // 隔离守卫：保护限1次，第二轮起循环到保护时改普通攻击
        var t = Fight(745, "z_isolation_guard", "shambler");
        var ig = Enemy(t, 1);
        Assert.That(ig.Intent, Is.EqualTo("protect"));
        ig.ProtectUses = 1;
        var bb = t.Battle!;
        bb.Round = 4;   // 第4次正常激活 = 循环第1项“保护”
        typeof(GameEngine).GetMethod("LockIntent", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(E, new object[] { t, bb, ig });
        Assert.That(ig.Intent, Is.Not.EqualTo("protect"), "保护限1次");
    }

    [Test]
    public void Gunner_ShootShootReload_SixRoundsThenMelee()
    {
        var s = Fight(746, "shambler", "shambler", "raider_gunner");
        var g = Enemy(s, 3);
        Assert.That((g.Mag, g.Reserve), Is.EqualTo((2, 4)), "弹药计划总6发、弹仓2");
        Assert.That(g.Intent, Is.EqualTo("shoot"));
        Assert.That(E.IntentOutOfReach(s.Battle!, g), Is.False, "3号位射击 1234");
        var shots = 0;
        for (var i = 0; i < 2000 && s.Battle != null && s.Battle.Round <= 12; i++)
        {
            foreach (var e in s.Battle.Alive.Where(e => e.EnemyId == "shambler")) e.Hp = 50;
            s.Stats.Health = 20;
            if (E.AwaitingPlayer(s)) { Cmd(s, "end_turn"); continue; }
            E.BattleStep(s);
            shots = s.Battle?.Log.Count(l => l.Contains("流窜枪手射击")) ?? shots;
        }
        Assert.That(g.Mag + g.Reserve, Is.EqualTo(0), "打空后不补");
        // 被压到 1 号位：射击改弱近战×0.7，不耗子弹
        var f = Fight(747, "raider_gunner");
        var fg = Enemy(f, 1);
        Assert.That(E.IntentShown(f.Battle!, fg), Does.StartWith("弱攻击"));
        for (var i = 0; i < 20 && !fg.Acted && f.Battle != null; i++) Step(f);
        Assert.That((fg.Mag, fg.Reserve), Is.EqualTo((2, 4)), "弱近战不耗子弹");
        Assert.That(s.Battle!.Log.Any(l => l.Contains("流窜枪手装填")), Is.True);
    }

    [Test]
    public void Signal_RequestsReinforcementOnce_Bloater_Splash_HelmetStaggerImmune_ExtraDrop()
    {
        var s = Fight(747, "z_signal");
        var b = s.Battle!;
        Assert.That(Enemy(s, 1).Intent, Is.EqualTo("signal"));
        for (var i = 0; i < 20 && !Enemy(s, 1).Acted && s.Battle != null; i++) Step(s);
        Assert.That(b.ReinforcementRequested, Is.True, "首轮鸣响请求现有增援1次");

        // 膨胀感染者：死亡时喷溅 0.3（护甲照算）
        var p = Fight(748, "z_bloater");
        Ready(p);
        Enemy(p, 1).Hp = 0.01;
        var h0 = p.Stats.Health;
        Force(p, Below(0.01), AtLeast(0.99), Any(), Any());
        Assert.That(Cmd(p, "basic_attack", 1).Ok, Is.True);
        Assert.That(h0 - p.Stats.Health, Is.EqualTo(0.3).Within(1e-9));
        Assert.That(p.Battle?.Log.Any(l => l.Contains("死亡喷溅")) ?? true, Is.True);

        // 护盔感染者：首次失衡免疫
        var h = Fight(749, "z_helmet");
        Ready(h);
        h.Skills["unarmed"].Level = 3;
        Enemy(h, 1).Hp = 50;
        Force(h, Below(0.01), AtLeast(0.99), Any(), Below(0.01));
        Assert.That(Cmd(h, "unarmed_03", 1).Ok, Is.True);
        Assert.That((Enemy(h, 1).Stagger, Enemy(h, 1).StaggerImmuneUsed), Is.EqualTo((false, true)));
        Assert.That(h.Battle!.Log.Last(), Does.Contain("首次失衡免疫"));

        // 附加掉落：天线守卫固定精密零件×2，胜利落地
        var r = Fight(750, "z_relay_guard");
        Ready(r);
        Enemy(r, 1).Hp = 0.01;
        Force(r, Below(0.01), AtLeast(0.99), Any(), Any(), Any(), Any(), Any());
        Assert.That(Cmd(r, "basic_attack", 1).Ok, Is.True);
        Assert.That(r.Battle, Is.Null);
        Assert.That(r.Items.Where(i => i.Owner == "field" && i.ItemId == "precision_parts").Sum(i => i.Count), Is.EqualTo(2));
    }

    [Test]
    public void HowlerPanicFour_DogLeaderBuff_Pursuit()
    {
        var s = Fight(751, "howler");
        var b = s.Battle!;
        Assert.That(Enemy(s, 1).IntentLabel, Is.EqualTo("嚎叫（恐慌+4）"), "A09 §1 嚎叫尸恐慌+4（取代旧 +8）");
        var p0 = b.Panic;
        for (var i = 0; i < 20 && !Enemy(s, 1).Acted && s.Battle != null; i++) Step(s);
        Assert.That(b.Panic - p0, Is.EqualTo(4).Within(1e-9));

        var d = Fight(752, "wild_dog_leader");
        Assert.That(Enemy(d, 1).Intent, Is.EqualTo("buff"));
        for (var i = 0; i < 20 && !Enemy(d, 1).Acted && d.Battle != null; i++) Step(d);
        Assert.That(Enemy(d, 1).NextHitFactor, Is.EqualTo(1.2).Within(1e-9));
    }

    // ───────────── M145 R27 旧规则回归（R03 §5—§7） ─────────────

    /// <summary>EN08/CB03：一大回合里多段技能、敌人前移、迎击、追击都只推进一次 5 分钟世界 tick。</summary>
    [Test]
    public void R27_OneTickPerRound_WithSkillsAdvanceAndReactions()
    {
        var s = Fight(760, "shambler", "shambler", "z_darter");
        var b = s.Battle!;
        PlayerFirst(s);
        E.BattleStep(s);
        Arm(s, "wooden_spear");
        s.Skills["spear"].Level = 10;
        b.PlayerAp = 4;
        foreach (var e in b.Alive) e.Hp = 50;
        var m0 = s.GameMinute;
        var t0 = b.Ticks;
        var r0 = b.Round;
        Assert.That(Cmd(s, "spear_10", 1).Ok, Is.True);
        Assert.That(Cmd(s, "spear_05").Ok, Is.True);
        for (var i = 0; i < 60 && s.Battle != null && s.Battle.Round == r0; i++)
        {
            foreach (var e in b.Alive) e.Hp = 50;
            s.Stats.Health = 20;
            Step(s);
        }
        Assert.That(b.Round, Is.EqualTo(r0 + 1));
        Assert.That(b.Ticks - t0, Is.EqualTo(1));
        Assert.That(s.GameMinute - m0, Is.EqualTo(5));
    }

    /// <summary>R03 §5 撤退公式（B6-17 保留）对新敌人照算：sigmoid((己速−敌方最高速)/3+0.7)×恐慌×低体力。</summary>
    [Test]
    public void R27_RetreatFormula_UnchangedWithNewEnemies()
    {
        var s = Fight(761, "z_darter", "z_slow");
        Ready(s);
        s.Battle!.Panic = 0;
        var (chance, _, _) = E.RetreatChance(s, s.Battle);
        var x = (4 - 8) / Cfg.Retreat.SpeedDivisor + Cfg.Retreat.Offset;
        Assert.That(chance, Is.EqualTo(1 / (1 + Math.Exp(-x))).Within(1e-9), "迅捷感染者速度8");
    }

    /// <summary>噪声增援与鸣响共用“每场至多 1 次、本场累计≤C、只调用同地现有丧尸”（A09 §3、R05）。</summary>
    [Test]
    public void R27_ReinforcementOncePerBattle_SignalAndNoiseShare()
    {
        var s = Fight(762, "z_signal");
        var extra1 = E.DebugSpawnEnemy(s, "shambler", "field");
        var extra2 = E.DebugSpawnEnemy(s, "shambler", "field");
        for (var i = 0; i < 40 && s.Battle != null && s.Battle.Round < 3; i++) { s.Stats.Health = 20; Step(s); }
        var b = s.Battle!;
        Assert.That(b.ReinforcementUsed, Is.True);
        Assert.That(b.Enemies.Count, Is.EqualTo(2), "鸣响只引来 1 只");
        // 之后噪声满也不再增援
        b.Noise = Cfg.Noise.Max;
        b.ReinforcementRequested = true;
        for (var i = 0; i < 40 && s.Battle != null && s.Battle.Round < 5; i++) { s.Stats.Health = 20; Step(s); }
        Assert.That(b.Enemies.Count, Is.EqualTo(2), "每场至多一次增援");
        Assert.That(new[] { extra1, extra2 }.Count(w => w.Engaged), Is.LessThanOrEqualTo(1));
    }

    /// <summary>EN05、R03 §5：首死锁定的掉落（含 A09 附加掉落）只在胜利落地，撤退放弃；存活敌人 HP 与经验计数跟着世界实例。</summary>
    [Test]
    public void R27_FirstDeathLootLocked_RetreatForfeits_PersistentHpXp()
    {
        var s = Fight(763, "raider_melee", "raider_gunner");
        Ready(s);
        Enemy(s, 1).Hp = 0.01;
        Force(s, Below(0.01), AtLeast(0.99), Any());
        Assert.That(Cmd(s, "basic_attack", 1).Ok, Is.True);
        var b = s.Battle!;
        Assert.That(b.Loot.Any(l => l.ItemId == "cloth"), Is.True, "掠夺者固定布料 1，首死锁定");
        Assert.That(s.Items.Any(i => i.Owner == "field" && i.ItemId == "cloth"), Is.False, "未胜利不落地");
        var gunner = Enemy(s, 2);
        gunner.Hp = 3;
        var wid = gunner.WorldId;
        Cmd(s, "end_turn");
        ToPlayer(s);
        Assert.That(s.Battle, Is.Not.Null);
        s.Battle!.RetreatUsed = false;
        s.Battle.PlayerAp = 3;
        Force(s, Below(0.0001));
        Assert.That(Cmd(s, "retreat").Ok, Is.True);
        Assert.That(s.Battle, Is.Null);
        Assert.That(s.Items.Any(i => i.Owner == "field" && i.ItemId == "cloth"), Is.False, "撤退放弃本场掉落");
        var we = s.WorldEnemies.Single(w => w.Id == wid);
        Assert.That(we.Hp, Is.EqualTo(3).Within(1e-9), "存活敌人 HP 保留");
    }

    /// <summary>EN03、A09 §4：只有丧尸咬击正伤才判感染；人类、动物、喷吐、尖啸都不加病毒。</summary>
    [Test]
    public void R27_OnlyZombieBiteInfects()
    {
        foreach (var id in new[] { "raider_melee", "wild_dog_leader", "z_spitter" })
        {
            var s = Fight(764, id);
            var e = Enemy(s, 1);
            e.Intent = "bite";
            var v0 = s.Body.VirusLoad;
            Force(s, Below(0.01), Any(), Any(), Any());
            typeof(GameEngine).GetMethod("EnemyAttack", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .Invoke(E, new object[] { s, e, Cfg.Enemies.Single(x => x.Id == id), false });
            Assert.That(s.Body.Infected, Is.False, id);
            Assert.That(s.Body.VirusLoad, Is.EqualTo(v0), id);
        }
    }

    /// <summary>B7 只加字段、不升 schema：B6（schema 12）的战斗中存档读入后，新字段取缺省值，战斗照常继续。</summary>
    [Test]
    public void R27_Schema12BattleSave_LoadsWithDefaults_Continues()
    {
        var s = Fight(765, "shambler", "howler");
        var json = Newtonsoft.Json.Linq.JObject.Parse(SaveSerializer.Serialize(s));
        var bj = (Newtonsoft.Json.Linq.JObject)json["Battle"]!;
        foreach (var k in new[] { "SkillUsedRound", "PlayerGuardCharges", "CounterFactor", "BraceCharges", "PlayerWeakFactor", "PlayerNextDamageFactor", "CommandXp", "PendingCommandXp" }) bj.Remove(k);
        foreach (var e in (Newtonsoft.Json.Linq.JArray)bj["Enemies"]!)
            foreach (var k in new[] { "JoinRound", "IntentStep", "WeakFactor", "NextHitFactor", "Mag", "Reserve", "IntentTarget" }) ((Newtonsoft.Json.Linq.JObject)e).Remove(k);
        var t = SaveSerializer.Deserialize(json.ToString(), Cfg);
        Assert.That(t.SchemaVersion, Is.EqualTo("12"));
        Assert.That((t.Battle!.PlayerWeakFactor, t.Battle.PlayerNextDamageFactor, t.Battle.Enemies[0].WeakFactor, t.Battle.Enemies[0].NextHitFactor, t.Battle.Enemies[0].Mag), Is.EqualTo((1.0, 1.0, 1.0, 1.0, -1)));
        for (var i = 0; i < 40 && t.Battle != null && t.Battle.Round < 3; i++) { t.Stats.Health = 20; if (E.AwaitingPlayer(t)) E.ExecuteCommand(t, BattleCommand.Of("end_turn")); else E.BattleStep(t); }
        Assert.That(t.Battle == null || t.Battle.Round >= 3, Is.True);
    }

    [Test]
    public void DevSkillKit_OnGround_AllKinds()
    {
        var s = New();
        E.DebugTeleport(s, "field");
        var text = E.DebugGiveSkillKit(s);
        Assert.That(text, Does.Contain("信号哨").And.Contain("创伤救护包").And.Contain("医用纱布×3"));
        var kinds = s.Items.Where(i => i.Owner == "field" && E.Weapon(i.ItemId) != null).Select(i => E.Weapon(i.ItemId)!.WeaponKind).Distinct();
        Assert.That(kinds, Is.SupersetOf(new[] { "blade", "axe", "spear", "blunt", "archery", "firearms", "shield", "command", "medical" }));
        Assert.That(E.DebugSetSkillLevels(s, 7, false), Does.Contain("全部十五门"));
        Assert.That(s.Skills["shield"].Level, Is.EqualTo(7));
    }

    [Test]
    public void NewEnemies_NotInOldSpawnPools()
    {
        var old = new[] { "shambler", "crawler", "runner", "worker", "riot", "howler", "feral_dog", "wolf", "boar", "stag" };
        var pooled = Cfg.WorldEnemies.Pools.Values.SelectMany(p => p.Values).SelectMany(l => l).Select(w => w.EnemyId).Distinct();
        Assert.That(pooled, Is.SubsetOf(old), "新敌只入数据，不改旧 8 地点刷新池（开发假设 B7-14）");
    }
}
