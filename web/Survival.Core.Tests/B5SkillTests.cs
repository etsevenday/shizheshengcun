using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 B5（策划提交 f172677）R18–R19：15 门十级熟练度、经验来源与防刷、旧档迁移、25 个求生被动。
/// 出处：A06 §1–§4，R01 §1、§2、§5；验收 RG01、SK01–SK06、R01-A05。
/// </summary>
public class B5SkillTests : BattleTestBase
{
    private void Ready(GameState s)
    {
        PlayerFirst(s);
        E.BattleStep(s);
        Assert.That(E.AwaitingPlayer(s), Is.True);
    }

    // ───────────── M121：15 门存储与门槛（H1、H2、H6） ─────────────

    [Test]
    public void RG01_SK01_FifteenSkills_FiveNodesEach()
    {
        var ids = new[] { "unarmed", "blade", "axe", "spear", "blunt", "archery", "firearms", "shield", "command", "medicine",
                          "crafting", "gathering", "scavenging", "cooking", "athletics" };
        Assert.That(Cfg.Skills.Select(x => x.Id), Is.EqualTo(ids), "A06 §4 十五门");
        Assert.That(Cfg.Skills.Single(x => x.Id == "blade").Name, Is.EqualTo("刀剑"));
        Assert.That(Cfg.Skills.Single(x => x.Id == "archery").Name, Is.EqualTo("弓弩"));
        Assert.That(Cfg.SkillNodes, Has.Count.EqualTo(75));
        foreach (var id in ids)
            Assert.That(Cfg.SkillNodes.Where(n => n.SkillId == id).Select(n => n.Level).OrderBy(x => x), Is.EqualTo(new[] { 1, 3, 5, 7, 10 }), id);
        var survival = Cfg.SkillNodes.Where(n => n.IsSurvival).ToList();
        Assert.That(survival, Has.Count.EqualTo(25), "求生被动 25 个");
        Assert.That(survival.Select(n => n.SkillId).Distinct(), Is.EquivalentTo(new[] { "crafting", "gathering", "scavenging", "cooking", "athletics" }));
        Assert.That(Cfg.SkillNodes.Single(n => n.Id == "axe_10").Name, Is.EqualTo("破阵"));
        Assert.That(Cfg.SkillNodes.Single(n => n.Id == "axe_10").BaseEnergy, Is.EqualTo(1.15));
        Assert.That(Cfg.SkillKindBaseEnergy["axe"], Is.EqualTo(0.65));
        var s = New();
        Assert.That(s.Skills.Keys, Is.EquivalentTo(ids), "新局 15 门");
        Assert.That(s.Skills.Values.All(x => x.Level == 1 && x.Xp == 0), Is.True, "全部 1 级 0 经验");
    }

    [Test]
    public void SK02_Threshold_Total2700_StopsAt10()
    {
        Assert.That(Enumerable.Range(1, 9).Select(l => E.NextLevelXp("gathering", l)), Is.EqualTo(new long[] { 100, 150, 200, 250, 300, 350, 400, 450, 500 }), "门槛 50×(L+1)");
        Assert.That(E.TotalXpToCap("gathering"), Is.EqualTo(2700));
        var s = New();
        E.GainXp(s, "gathering", 2699);
        Assert.That(s.Skills["gathering"].Level, Is.EqualTo(9));
        Assert.That(s.Skills["gathering"].Xp, Is.EqualTo(499));
        Assert.That(E.SkillMaxed(s, "gathering"), Is.False);
        Assert.That(E.GainXp(s, "gathering", 50), Is.EqualTo(1));
        Assert.That(s.Skills["gathering"].Level, Is.EqualTo(10));
        Assert.That(s.Skills["gathering"].Xp, Is.EqualTo(0), "溢出不保留、不兑换");
        Assert.That(s.Log.Last().Text, Does.Contain("满级"));
        Assert.That(E.GainXp(s, "gathering", 999), Is.EqualTo(0));
        Assert.That(s.Skills["gathering"].Xp, Is.EqualTo(0), "Lv10 停止累计");
        Assert.That(E.XpText(s, "gathering", 2), Is.EqualTo("采集已满10级，不再获得经验"));
        var v = E.SkillViews(s).Single(x => x.Id == "gathering");
        Assert.That((v.Level, v.Cap, v.Maxed, v.NextXp), Is.EqualTo((10, 10, true, 0L)));
    }

    [Test]
    public void SK02_CombatXp_StopsAt10()
    {
        var s = Fight(41, "riot");
        Ready(s);
        Arm(s, "hatchet");
        s.Skills["axe"].Level = 10;
        s.Skills["axe"].Xp = 0;
        Force(s, Below(0.01), AtLeast(0.99), Any());
        Enemy(s, 1).Hp = 50;
        Assert.That(Cmd(s, "basic_attack", 1).Ok, Is.True);
        Assert.That(s.Skills["axe"].Xp, Is.EqualTo(0), "满级攻击命中不再加经验");
        Assert.That(s.Battle!.Enemies[0].XpHits["axe"], Is.EqualTo(1), "防刷计数照记");
    }

    [Test]
    public void H2_AxeWeapons_TrainAxe_ShieldCommandHaveOwnSkill()
    {
        foreach (var w in Cfg.Weapons.Where(w => w.WeaponKind == "axe")) Assert.That(w.SkillId, Is.EqualTo("axe"), w.Id);
        foreach (var w in Cfg.Weapons.Where(w => w.WeaponKind == "shield")) Assert.That(w.SkillId, Is.EqualTo("shield"), w.Id);
        foreach (var w in Cfg.Weapons.Where(w => w.WeaponKind == "command")) Assert.That(w.SkillId, Is.EqualTo("command"), w.Id);
        foreach (var w in Cfg.Weapons.Where(w => w.WeaponKind == "medical")) Assert.That(w.SkillId, Is.EqualTo("medicine"), w.Id);
        Assert.That(Cfg.Weapons.Single(w => w.Id == "wooden_shield").CanAttack, Is.False, "盾的技能仍在 B6/B7");
        Assert.That(Cfg.CombatActions.Single(a => a.Id == "cleave").Skills, Is.EquivalentTo(new[] { "blade", "axe" }), "现行劈砍斧仍可用");
        // 简易斧的劈砍按斧等级判定
        var s = Fight(42, "worker");
        Ready(s);
        Arm(s, "hatchet");
        Assert.That(E.CommandBlocked(s, BattleCommand.Of("cleave", 1)), Is.Not.Null);
        s.Skills["axe"].Level = 3;
        Assert.That(E.CommandBlocked(s, BattleCommand.Of("cleave", 1)), Is.Null);
    }

    [Test]
    public void SK06_AntiviralCure_ReachableAt10()
    {
        var rc = Cfg.Recipes.Single(r => r.Id == "make_antiviral_cure");
        Assert.That(rc.MinSkills, Is.EquivalentTo(new Dictionary<string, int> { ["medicine"] = 10, ["crafting"] = 10 }));
        var u = New();
        foreach (var kv in rc.Inputs) E.DebugGive(u, kv.Key, kv.Value, "camp");
        var req = ActionRequest.Craft("make_antiviral_cure");
        Assert.That(E.CanStart(u, req), Does.Contain("需要医疗10级"));
        E.GainXp(u, "medicine", 2700);
        E.GainXp(u, "crafting", 2700);
        Assert.That(E.CanStart(u, req), Does.Not.Contain("需要"), "医疗10/制造10 过了熟练门槛（正常经验可达；剩下的是锅等配方条件）");
        Assert.That(Cfg.Recipes.Count(r => r.Outputs.ContainsKey("antiviral_cure")), Is.EqualTo(1), "没有第三条治愈入口");
    }
}
