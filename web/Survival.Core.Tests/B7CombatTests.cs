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

    [Test]
    public void NewEnemies_NotInOldSpawnPools()
    {
        var old = new[] { "shambler", "crawler", "runner", "worker", "riot", "howler", "feral_dog", "wolf", "boar", "stag" };
        var pooled = Cfg.WorldEnemies.Pools.Values.SelectMany(p => p.Values).SelectMany(l => l).Select(w => w.EnemyId).Distinct();
        Assert.That(pooled, Is.SubsetOf(old), "新敌只入数据，不改旧 8 地点刷新池（开发假设 B7-14）");
    }
}
