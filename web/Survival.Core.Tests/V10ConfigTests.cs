using Survival.Core.Config;

namespace Survival.Core.Tests;

/// <summary>
/// 系统策划总册 v1.0 答复（策划提交 664fbda、1f2786d）新增与替换的配置：数值逐项对照策划案原文
/// （R05 §6.1/§6.3、R03 §2.1、S08 §8、S11 §8、R04 §7.2、R07 §2–§4、N01 §3）。
/// </summary>
public class V10ConfigTests : TestBase
{
    [Test]
    public void FearParameters_MatchR05_6_3()
    {
        var expect = new Dictionary<string, (double q, double v)>
        {
            ["shambler"] = (0.35, 6), ["crawler"] = (0.40, 7), ["runner"] = (0.55, 10), ["worker"] = (0.40, 8), ["riot"] = (0.60, 14),
            ["howler"] = (0.65, 16), ["feral_dog"] = (0.20, 4), ["wolf"] = (0.35, 7), ["boar"] = (0.35, 8), ["stag"] = (0.10, 3),
        };
        Assert.That(Cfg.Enemies.Select(e => e.Id), Is.EquivalentTo(expect.Keys));
        foreach (var e in Cfg.Enemies)
        {
            Assert.That(e.FearChance, Is.EqualTo(expect[e.Id].q), e.Id);
            Assert.That(e.FearValue, Is.EqualTo(expect[e.Id].v), e.Id);
        }
        var f = Cfg.Panic.Fear;
        Assert.That(f.Factor("approach"), Is.EqualTo(0.5));
        Assert.That(f.Factor("encounter"), Is.EqualTo(1.0));
        Assert.That(f.Factor("tutorial"), Is.EqualTo(1.0));
        Assert.That(f.ReuseRollMinutes, Is.EqualTo(60));
    }

    [Test]
    public void RarityOpening_MatchR05_6_1()
    {
        var o = Cfg.WorldEnemies.RarityOpenAbsoluteMinute;
        Assert.That(o["common"], Is.EqualTo(0));
        Assert.That(o["rare"], Is.EqualTo(2880), "第3日00:00");
        Assert.That(o["elite"], Is.EqualTo(8640), "第7日00:00");
    }

    [Test]
    public void Recipes_MatchS08_8()
    {
        void Check(string id, int minutes, Dictionary<string, int> inputs, string output, int count)
        {
            var r = Cfg.Recipes.Single(x => x.Id == id);
            Assert.That(r.BaseMinutes, Is.EqualTo(minutes), id);
            Assert.That(r.Inputs, Is.EquivalentTo(inputs), id);
            Assert.That(r.Outputs, Is.EquivalentTo(new Dictionary<string, int> { [output] = count }), id);
            Assert.That(r.MinSkills["crafting"], Is.EqualTo(1), id);
            Assert.That(r.Requirement, Is.Null, id + " 无地点限制");
        }
        Check("make_metal_spear", 90, new() { ["wood"] = 2, ["scrap"] = 2, ["fiber"] = 2 }, "metal_spear", 1);
        Check("make_crossbow", 120, new() { ["wood"] = 3, ["scrap"] = 2, ["parts"] = 2, ["fiber"] = 4, ["cloth"] = 1 }, "crossbow", 1);
        Check("make_bolts", 30, new() { ["wood"] = 1, ["scrap"] = 1, ["fiber"] = 1 }, "bolt", 4);
    }

    [Test]
    public void Reboil_MatchS11_8_PotNoDurability()
    {
        var p = Cfg.Processes.Single(x => x.Id == "reboil_cooked_meat");
        Assert.That(p.InputId, Is.EqualTo("cooked_meat"));
        Assert.That(p.OutputId, Is.EqualTo("boiled_meat"));
        Assert.That(p.HeatMinutes, Is.EqualTo(15));
        Assert.That(p.WaterCostMl, Is.EqualTo(500));
        Assert.That(p.RequiredLiquid, Is.EqualTo("clean_water"));
        Assert.That(p.InPot, Is.True);
        Assert.That(p.KeepInputExpiry, Is.True, "继承原烤肉 expiresAt");
        Assert.That(Cfg.Containers["pot"].StoreOnlyFoodIds, Is.EqualTo(new[] { "boiled_meat" }));
        Assert.That(Cfg.Items.Single(i => i.Id == "pot").DurabilityMaxRange, Is.Null);
        Assert.That(Cfg.Durability.Tools.ContainsKey("pot"), Is.False);
    }

    [Test]
    public void BaobaoInput_MatchR04_7_2()
    {
        var b = Cfg.Infection.BaobaoInput;
        Assert.That(b.MaxPerHour, Is.EqualTo(0.10));
        Assert.That(b.FullSuppressImmunityAtLeast, Is.EqualTo(85));
        Assert.That(b.LinearBandWidth, Is.EqualTo(45));
        Assert.That(b.FirstInfectionMinLoad, Is.EqualTo(1));
        Assert.That(b.NewGamePresent, Is.True);
        Assert.That(b.LegacySavePresent, Is.False);
        // R04 §7.2 算例：I70 时 B≈0.033333/h；I40 及以下 0.10；I85 以上 0
        Assert.That(b.Rate(70), Is.EqualTo(0.1 / 3).Within(1e-9));
        Assert.That(b.Rate(40), Is.EqualTo(0.10).Within(1e-12));
        Assert.That(b.Rate(0), Is.EqualTo(0.10).Within(1e-12));
        Assert.That(b.Rate(85), Is.EqualTo(0));
        Assert.That(b.Rate(100), Is.EqualTo(0));
    }

    [Test]
    public void ActiveSubstance_MatchR07()
    {
        var a = Cfg.ActiveSubstance;
        var it = Cfg.Items.Single(i => i.Id == a.ItemId);
        Assert.That(it.Id, Is.EqualTo("active_substance_white_unidentified"));
        Assert.That(it.Name, Is.EqualTo("未鉴定的活性物质"));
        Assert.That(it.Quality, Is.EqualTo("white"));
        Assert.That(it.WeightGrams, Is.EqualTo(50));
        Assert.That(it.StackMax, Is.EqualTo(20));
        Assert.That(it.ShelfLifeMinutes, Is.Null, "本期无保质");
        Assert.That(it.Use, Is.Null, "不是食物或药品");
        Assert.That(a.NormalDrop.Families, Is.EqualTo(new[] { "zombie" }), "仅丧尸，动物不掉");
        Assert.That(a.NormalDrop.ChancePerEnemy, Is.EqualTo(0.005));
        Assert.That(a.NormalDrop.Count, Is.EqualTo(1));
        Assert.That(a.TutorialDrop.Count, Is.EqualTo(1));
        Assert.That(a.Fusion.Minutes, Is.EqualTo(5));
        Assert.That(a.Fusion.RateKey, Is.EqualTo("wait"));
        Assert.That(a.Fusion.ExtraEnergy, Is.EqualTo(0));
        Assert.That(a.TutorialEffect.EffectId, Is.EqualTo("tutorial_attack_bonus_001"));
        Assert.That(a.TutorialEffect.Quality, Is.EqualTo("green"));
        Assert.That(a.TutorialEffect.Label, Is.EqualTo("攻击力+0.1"));
        Assert.That(a.TutorialEffect.FlatAttack, Is.EqualTo(0.1));
    }

    [Test]
    public void Story_MatchN01_S15()
    {
        var st = Cfg.Story;
        Assert.That(st.TutorialBattle.FromLocationId, Is.EqualTo("camp"));
        Assert.That(st.TutorialBattle.LocationId, Is.EqualTo("field"));
        Assert.That(st.TutorialBattle.EnemyId, Is.EqualTo("shambler"));
        Assert.That(st.TutorialBattle.NoReinforcement, Is.True);
        string T(string id) => st.Dialogues.Single(d => d.Id == id).Text;
        Assert.That(T("awakening_1"), Is.EqualTo("醒醒……醒醒，我饿了。"));
        Assert.That(T("awakening_2"), Is.EqualTo("我不知道我叫什么。我只知道，我饿了。"));
        Assert.That(T("food_water"), Is.EqualTo("先找点能吃的。还有水。"));
        Assert.That(T("tutorial_arrive"), Is.EqualTo("前面那个……不像还活着。小心。"));
        Assert.That(T("tutorial_won"), Is.EqualTo("那东西身上有一团还在动的物质……让我试试。"));
        Assert.That(T("fusion_done_1"), Is.EqualTo("有变化了。攻击力增加了0.1。"));
        Assert.That(T("fusion_done_2"), Is.EqualTo("也可能有不好的变化……这次运气不错。"));
        // 初始 6 只世界敌人里荒地游荡尸作为首战保留实例（R05 §5）
        Assert.That(Cfg.WorldEnemies.Initial["field"], Does.Contain("shambler"));
    }

    [Test]
    public void Xp_P01_PerTarget()
    {
        Assert.That(Cfg.Xp.PerHitTarget, Is.True);
        Assert.That(Cfg.Xp.ZeroDamageHitCounts, Is.True);
        Assert.That(Cfg.Xp.GroupAttackXpIsSingleCommand, Is.False);
        Assert.That(Cfg.Xp.DamagingAttackCommand, Is.EqualTo(2));
        Assert.That(Cfg.Xp.MaxHitsPerEncounterEnemySkill, Is.EqualTo(3));
    }
}
