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
        // B7：A09 §1 新增 20 种另按 A09 §4 新敌受惊参数（B7CombatTests.EN01），旧 10 种不改
        Assert.That(Cfg.Enemies.Select(e => e.Id), Is.SupersetOf(expect.Keys));
        foreach (var e in Cfg.Enemies.Where(e => expect.ContainsKey(e.Id)))
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
        // Q16（S11 §10）取代 T03 继承原期限：配置不再有 keepInputExpiry，成品统一完成起 24 小时（V11CookProtectTests）
        Assert.That(Cfg.Containers["pot"].StoreOnlyFoodIds, Is.EqualTo(new[] { "boiled_meat" }));
        Assert.That(Cfg.Items.Single(i => i.Id == "pot").DurabilityMaxRange, Is.Null);
        Assert.That(Cfg.Durability.Tools.ContainsKey("pot"), Is.False);
    }

    /// <summary>S12 §2/§8 Q7：非战斗日常小时率新表逐格（N/W/E/S/M），战斗 battleTime 率保持原值（R01 §7、R03 §7.2）。</summary>
    [Test]
    public void Q7_HourlyRates_MatchS12_2()
    {
        var expect = new Dictionary<string, double[]>
        {
            ["travel"] = new[] { -1.5, -3.6, -1.28, -4, -0.4 },
            ["explore"] = new[] { -1.5, -3.6, -1.6, -4, -0.4 },
            ["gather"] = new[] { -1.5, -3.6, -1.92, -4, -0.4 },
            ["clear"] = new[] { -1.5, -3.6, -1.92, -4, -0.4 },
            ["craft"] = new[] { -1.2, -3, -0.96, -4, -0.4 },
            ["wait"] = new[] { -1.2, -2.4, 0, -4, -0.4 },
            ["rest"] = new[] { -1.2, -2.4, 6, -4, 0 },
            ["sleep_sheltered"] = new[] { -1.2, -2.4, 4, 12, 1 },
            ["sleep_exposed"] = new[] { -1.2, -2.4, 3, 6, -0.4 },
        };
        Assert.That(Cfg.HourlyRates.Keys, Is.EquivalentTo(expect.Keys));
        foreach (var (k, v) in expect)
        {
            var r = Cfg.HourlyRates[k];
            Assert.That(new[] { r.Satiety, r.Hydration, r.Energy, r.Wakefulness, r.Morale }, Is.EqualTo(v), k);
        }
        var b = Cfg.BattleTime.HourlyRates;
        Assert.That(new[] { b.Satiety, b.Hydration, b.Energy, b.Wakefulness, b.Morale }, Is.EqualTo(new[] { 0, 0, 0, -5, -0.5 }), "战斗小时率不随 Q7 缩放；B6（R03 §6）N/W 冻结为0，S/M 沿原−5/−0.5");
        Assert.That(Cfg.BattleTime.FreezeNegativeSatietyHydration, Is.True);
        // 腹泻额外水耗、归零损血、正向恢复加成不缩放
        Assert.That(Cfg.ItemActions.DiarrheaExtraHydrationPerHour, Is.EqualTo(-3));
        Assert.That((Cfg.HealthRules.ZeroSatietyPerHour, Cfg.HealthRules.ZeroHydrationPerHour, Cfg.HealthRules.ZeroWakefulnessPerHour, Cfg.HealthRules.ZeroEnergyWhileWorkingPerHour),
            Is.EqualTo((-0.4, -1.6, -0.4, -0.2)));
        Assert.That(Cfg.HealthRules.ShelteredRestRecoveryPerHour, Is.EqualTo(0.2));
        Assert.That(Cfg.FacilityEffects.BedExtraSleepEnergyPerHour, Is.EqualTo(1));
        Assert.That(Cfg.FacilityEffects.ShelteredCampRestMoralePerHour, Is.EqualTo(2));
    }

    /// <summary>R04 §2/§3/§8 Q7：免疫正恢复×2、感染自身惩罚取消、进展档与自然治愈门槛 80，其他惩罚不变。</summary>
    [Test]
    public void Q7_ImmunityInfection_MatchR04_8()
    {
        var i = Cfg.Immunity;
        Assert.That((i.HealthyBonusPerHour, i.ShelteredSleepPerHour, i.ExposedSleepPerHour, i.ShelteredRestPerHour, i.ExposedRestPerHour), Is.EqualTo((1.0, 4.0, 2.0, 2.0, 1.0)));
        var p = i.PenaltiesPerHour;
        Assert.That((p.SatietyBelow20, p.HydrationBelow20, p.WakefulnessBelow20, p.HealthBelow5, p.MoraleBelow20, p.Diarrhea, p.VirusInfected),
            Is.EqualTo((-2.0, -3.0, -2.0, -2.0, -1.0, -1.0, 0.0)));
        var bands = Cfg.Infection.ProgressPerHour.Select(x => (x.ImmunityRange[0], x.ImmunityRange[1], x.IncludeUpper, x.Rate)).ToArray();
        Assert.That(bands, Is.EqualTo(new[] { (0.0, 40.0, false, 4.0), (40.0, 70.0, false, 2.0), (70.0, 80.0, false, 0.5), (80.0, 100.0, true, -1.5) }));
        var nc = Cfg.Infection.NaturalCure;
        Assert.That((nc.ConsecutiveImmunityAtLeast, nc.Minutes, nc.LoadAtMost), Is.EqualTo((80.0, 720, 10.0)));
        // 完全压制、负增长、计时三处统一 80
        Assert.That(Cfg.Infection.BaobaoInput.FullSuppressImmunityAtLeast, Is.EqualTo(nc.ConsecutiveImmunityAtLeast));
        Assert.That(Cfg.Infection.ProgressPerHour.Single(x => x.Rate < 0).ImmunityRange[0], Is.EqualTo(nc.ConsecutiveImmunityAtLeast));
        Assert.That(E.ExposureChance(80), Is.EqualTo(0.16).Within(1e-12), "I80 咬伤暴露概率仍为16%");
    }

    [Test]
    public void BaobaoInput_MatchR04_7_2()
    {
        var b = Cfg.Infection.BaobaoInput;
        // R04 §8 Q7 温和初值：Bmax 0.10→0.05、完全压制 85→80、带宽 45→40
        Assert.That(b.MaxPerHour, Is.EqualTo(0.05));
        Assert.That(b.FullSuppressImmunityAtLeast, Is.EqualTo(80));
        Assert.That(b.LinearBandWidth, Is.EqualTo(40));
        Assert.That(b.FirstInfectionMinLoad, Is.EqualTo(1));
        Assert.That(b.NewGamePresent, Is.True);
        Assert.That(b.LegacySavePresent, Is.False);
        // R04 §7.2 算例：I70 时 B=0.0125/h；I40 及以下 0.05；I80 以上 0
        Assert.That(b.Rate(70), Is.EqualTo(0.0125).Within(1e-12));
        Assert.That(b.Rate(40), Is.EqualTo(0.05).Within(1e-12));
        Assert.That(b.Rate(0), Is.EqualTo(0.05).Within(1e-12));
        Assert.That(b.Rate(80), Is.EqualTo(0));
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
