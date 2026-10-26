using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>人物战斗与免疫系统修订参考案 v0.6 第 17 节接入验收 R601–R624（同名用例；涉及战斗的部分见 M42 起的补充）。</summary>
public class CombatV06Tests : TestBase
{
    private double Hours(string key, Func<Config.RateBlock, double> f, double hours) => f(Cfg.HourlyRates[key]) * hours;

    /// <summary>
    /// R614 正常日程与低精神重活反例（验证记录 v0.6“24小时预算”）：按配置速率核对日程预算，
    /// 再用引擎实跑休息、遮蔽睡眠与低精神恢复，不只检查睡后最终体力。
    /// </summary>
    [Test]
    public void R614_NormalScheduleBudget_AndLowMoraleCounterExample()
    {
        // 正常日程：移动3h、探索2h、搜集4h、制造3h → 工作体力合计22；休息2h回12；遮蔽睡眠5小时由0回20
        var work = -(Hours("travel", r => r.Energy, 3) + Hours("explore", r => r.Energy, 2) + Hours("gather", r => r.Energy, 4) + Hours("craft", r => r.Energy, 3));
        Assert.That(work, Is.EqualTo(22).Within(1e-9), "全天工作体力合计消耗22.00");
        Assert.That(Hours("rest", r => r.Energy, 2), Is.EqualTo(12).Within(1e-9), "两小时休息提供12");
        var water = -(Hours("travel", r => r.Hydration, 3) + Hours("explore", r => r.Hydration, 2) + Hours("gather", r => r.Hydration, 4)
                      + Hours("craft", r => r.Hydration, 3) + Hours("rest", r => r.Hydration, 2) + Hours("wait", r => r.Hydration, 2)
                      + Hours("sleep_sheltered", r => r.Hydration, 8));
        Assert.That(water, Is.EqualTo(87.75).Within(1e-9), "自然耗水87.75");
        var food = -(Hours("travel", r => r.Satiety, 3) + Hours("explore", r => r.Satiety, 2) + Hours("gather", r => r.Satiety, 4)
                     + Hours("craft", r => r.Satiety, 3) + Hours("rest", r => r.Satiety, 2) + Hours("wait", r => r.Satiety, 2)
                     + Hours("sleep_sheltered", r => r.Satiety, 8));
        Assert.That(food, Is.EqualTo(39.375).Within(1e-9), "饱食自然消耗39.375");

        // 引擎实跑：遮蔽睡眠5小时由0回20；露天睡眠约6小时40分由0回20
        var s = New();
        s.Locations["camp"].Facility.ShelterBuilt = true;
        SetStats(s, 20, 80, 80, 0, 30, 60);
        Start(s, ActionRequest.Sleep(480));
        Steps(s, 60);
        Assert.That(s.Stats.Energy, Is.EqualTo(20).Within(1e-9), "遮蔽睡眠5小时可由0回20");
        var t = New();
        SetStats(t, 20, 80, 80, 0, 30, 60);
        Start(t, ActionRequest.Sleep(480));
        Steps(t, 79);
        Assert.That(t.Stats.Energy, Is.LessThan(20), "露天睡眠6小时35分仍未满");
        Steps(t, 1);
        Assert.That(t.Stats.Energy, Is.EqualTo(20).Within(1e-9), "露天睡眠6小时40分回满");

        // 反例：低精神时休息30分钟只补1.5（正常补3）；白昼轻装搜集30分钟耗1.2
        var u = New();
        SetStats(u, 20, 80, 80, 10, 90, 10);
        Start(u, ActionRequest.Rest(30));
        Run(u);
        Assert.That(u.Stats.Energy, Is.EqualTo(11.5).Within(1e-9), "精神<20时正E恢复×0.5");
        Assert.That(-Hours("gather", r => r.Energy, 0.5), Is.EqualTo(1.2).Within(1e-9));
        // 反例日程：重活10小时、休息1小时、露天睡眠8小时、精神低于20 → 日内体力必然归零（只看睡后体力会误判）
        var heavy = -(Hours("gather", r => r.Energy, 8) + Hours("travel", r => r.Energy, 2));
        var restLow = Hours("rest", r => r.Energy, 1) * Cfg.FacilityEffects.LowMoraleEnergyRecoveryFactor;
        Assert.That(Cfg.InitialStats.Energy + restLow - heavy, Is.LessThan(0), "不满足安全消耗预算：日内归零");
        var v = New();
        SetStats(v, 20, 80, 80, Cfg.InitialStats.Energy, 90, 10);
        E.DebugTeleport(v, "field");
        var minE = v.Stats.Energy;
        var zeroTicks = 0;
        for (var i = 0; i < 16 && !v.Progress.Dead; i++)
        {
            v.Resources["roots"].Available = 6;
            v.Resources["roots"].Discovered = true;
            Start(v, ActionRequest.Gather("roots"));
            while (v.Main != null && !v.Progress.Dead)
            {
                if (v.Main.Status == ActionStatus.Paused) E.Resume(v);
                E.Advance(v);
                minE = Math.Min(minE, v.Stats.Energy);
                if (v.Stats.Energy <= 0) zeroTicks++;
            }
        }
        Assert.That(minE, Is.EqualTo(0), "重活日程日内体力归零");
        Assert.That(zeroTicks, Is.GreaterThan(0));
    }

    /// <summary>R615 浏览、预览、排序、拾取放下与拖拽解析都不扣体力、不推进时间（战斗 tick 部分见战斗用例）。</summary>
    [Test]
    public void R615_BrowsingAndCardOpsCostNoEnergy()
    {
        var s = New();
        var e0 = s.Stats.Energy;
        var m0 = s.GameMinute;
        var json = SaveSerializer.Serialize(s);
        E.Preview(s, ActionRequest.Explore());
        E.Preview(s, ActionRequest.Rest(60));
        E.ResolveDrop(s, DropSource.Parse("card:camp|wood")!, DropTarget.Parse("zone:carried")!);
        Assert.That(SaveSerializer.Serialize(s), Is.EqualTo(json), "预览与拖拽悬停不改状态");
        var wood = First(s, "wood", "camp");
        Assert.That(E.DoInstant(s, InstantRequest.Pickup(wood.InstanceId, 1)).Ok, Is.True);
        Assert.That(E.DoInstant(s, InstantRequest.Drop(First(s, "wood", "carried").InstanceId, 1)).Ok, Is.True);
        Assert.That(E.DoInstant(s, InstantRequest.Reorder("camp", E.OrderedCards(s, "camp")[0], 3)).Ok, Is.True);
        Assert.That(s.Stats.Energy, Is.EqualTo(e0), "拖卡、拾取、放下、排序不扣隐藏体力");
        Assert.That(s.GameMinute, Is.EqualTo(m0));
        // 点火5分钟按 craft 速率 E−0.1
        E.DebugGive(s, "wood", 1);
        E.DoInstant(s, InstantRequest.AddFuel());
        var lighter = First(s, "lighter");
        Start(s, ActionRequest.Ignite(lighter.InstanceId));
        Run(s);
        Assert.That(e0 - s.Stats.Energy, Is.EqualTo(0.1).Within(1e-9), "点火5分钟按craft速率−0.1");
        // 食用5分钟按 wait：额外E=0
        var e1 = s.Stats.Energy;
        Start(s, ActionRequest.UseItem("canned_food"));
        Run(s);
        Assert.That(s.Stats.Energy, Is.EqualTo(e1), "食用、饮水、用药按wait身体速率，E不变");
    }

    /// <summary>R601 技能 L1/10/30/100/1000 持续成长：能力不封顶、成本保持正值且递减（验证记录“技能成长样例”）。</summary>
    [Test]
    public void R601_SkillGrowth_Uncapped()
    {
        var axe = Cfg.Weapons.Single(w => w.Id == "hatchet");
        var shambler = Cfg.Enemies.Single(x => x.Id == "shambler");
        double Hit(int l) { var a = axe.AccuracyRating + E.SkillAccuracyBonus(l); return a / (a + shambler.Dodge); }
        var table = new (int l, double hit, double dmg, double energy, double craft)[]
        {
            (1, 0.5192, 1.035, 0.6341, 0.9756), (10, 0.6835, 1.350, 0.5200, 0.8000),
            (30, 0.8201, 2.050, 0.3714, 0.5714), (100, 0.9284, 4.500, 0.1857, 0.2857),
        };
        foreach (var (l, hit, dmg, energy, craft) in table)
        {
            Assert.That(Hit(l), Is.EqualTo(hit).Within(5e-5), $"L{l} 斧命中游荡尸");
            Assert.That(E.SkillDamageFactor(l), Is.EqualTo(dmg).Within(1e-9), $"L{l} 伤害倍率");
            Assert.That(E.SkillEnergyCost(axe.EnergyCost, l), Is.EqualTo(energy).Within(5e-5), $"L{l} 单击体力");
            Assert.That(E.CraftWorkFactor(l), Is.EqualTo(craft).Within(5e-5), $"L{l} 制造工作量倍率");
        }
        // L1000：伤害继续增长、命中概率<1、成本仍为正且更小
        Assert.That(E.SkillDamageFactor(1000), Is.GreaterThan(E.SkillDamageFactor(100)));
        Assert.That(Hit(1000), Is.LessThan(1).And.GreaterThan(Hit(100)), "概率趋近但不超过1，无95%夹紧");
        Assert.That(E.SkillEnergyCost(axe.EnergyCost, 1000), Is.GreaterThan(0).And.LessThan(E.SkillEnergyCost(axe.EnergyCost, 100)));
        Assert.That(E.CraftWorkFactor(1000), Is.GreaterThan(0).And.LessThan(E.CraftWorkFactor(100)));

        // 无10级上限：经验持续累计、逐级扣门槛，可多级提升；溢出报错不回零
        var s = New();
        Assert.That(s.Skills.Keys, Is.EquivalentTo(Cfg.Skills.Select(x => x.Id)), "新局8项技能");
        Assert.That(s.Skills.Values.All(x => x.Level == 1 && x.Xp == 0), Is.True);
        E.GainXp(s, "blade", 100 + 150);      // 1→2 需100，2→3 需150
        Assert.That(s.Skills["blade"].Level, Is.EqualTo(3));
        Assert.That(s.Skills["blade"].Xp, Is.EqualTo(0));
        long total = 0;
        for (var l = 3; l < 12; l++) total += E.NextLevelXp("blade", l);
        E.GainXp(s, "blade", total + 7);
        Assert.That(s.Skills["blade"].Level, Is.EqualTo(12), "10级后继续升级");
        Assert.That(s.Skills["blade"].Xp, Is.EqualTo(7));
        s.Skills["crafting"].Xp = long.MaxValue - 1;
        Assert.Throws<OverflowException>(() => E.GainXp(s, "crafting", 5), "经验溢出报错而非静默回零");
    }

    /// <summary>R604 所有装备生成、读档、转移：最大耐久在区间内按0.1抽一次，实例保存，不重抽、成色不刷新。</summary>
    [Test]
    public void R604_RandomMaxDurability_RolledOnce()
    {
        var s = New(4242);
        foreach (var it in Cfg.Items.Where(i => i.HasDurability))
        {
            var seen = new HashSet<double>();
            for (var k = 0; k < 60; k++)
            {
                var inst = E.CreateItems(s, it.Id, 1, "camp").Single();
                var max = inst.MaxDurability!.Value;
                Assert.That(max, Is.InRange(it.DurabilityMaxRange![0], it.DurabilityMaxRange[1]), it.Id);
                Assert.That(Math.Abs(max * 10 - Math.Round(max * 10)), Is.LessThan(1e-9), it.Id + " 按0.1精度");
                Assert.That(inst.Durability, Is.EqualTo(max), it.Id + " 新制品当前=最大");
                seen.Add(max);
            }
            Assert.That(seen.Count, Is.GreaterThan(1), it.Id + " 每件随机，不是统一满耐久");
        }
        // 新局开局的锅与打火器也有随机耐久
        var t = New(7);
        Assert.That(First(t, "pot").MaxDurability, Is.InRange(60.0, 100.0));
        Assert.That(First(t, "lighter").MaxDurability, Is.InRange(40.0, 80.0));

        // 读档、转移、拾取不重抽
        var knife = E.CreateItems(t, "stone_knife", 1, "camp").Single();
        var (m0, d0) = (knife.MaxDurability, knife.Durability);
        var json = SaveSerializer.Serialize(t);
        var u = SaveSerializer.Deserialize(json);
        var k2 = u.Items.Single(i => i.InstanceId == knife.InstanceId);
        Assert.That((k2.MaxDurability, k2.Durability), Is.EqualTo((m0, d0)), "读档不重抽");
        E.DoInstant(u, InstantRequest.Pickup(k2.InstanceId, 1));
        E.DoInstant(u, InstantRequest.Drop(k2.InstanceId, 1));
        Assert.That((k2.MaxDurability, k2.Durability), Is.EqualTo((m0, d0)), "转移不重抽");
        Assert.That(SaveSerializer.Serialize(SaveSerializer.Deserialize(SaveSerializer.Serialize(u))), Is.EqualTo(SaveSerializer.Serialize(u)));

        // 拾荒箱子（五金店工具箱）产出：当前=最大×0.6—1.0成色，搜集时生成一次
        var v = New(11);
        E.DebugTeleport(v, "hardware");
        E.DebugSetProgress(v, "hardware", 75);
        Start(v, ActionRequest.Gather("tool_box"));
        Run(v);
        var axe = First(v, "hatchet", "hardware");
        Assert.That(GameEngine.Condition(axe), Is.InRange(0.6, 1.0), "拾荒成色");
        var save = SaveSerializer.Serialize(v);
        Assert.That(SaveSerializer.Deserialize(save).Items.Single(i => i.InstanceId == axe.InstanceId).Durability, Is.EqualTo(axe.Durability), "成色不刷新");
    }

    /// <summary>R605 耐久不足一次成本/锅损坏：拒绝使用但保留物与内容；维护恢复自身最大20%、不重抽最大值。</summary>
    [Test]
    public void R605_InsufficientDurability_RepairKeepsMax()
    {
        var s = New();
        var lighter = First(s, "lighter");
        lighter.Durability = 0.8;
        E.DebugGive(s, "wood", 1);
        E.DoInstant(s, InstantRequest.AddFuel());
        var why = E.CanStart(s, ActionRequest.Ignite(lighter.InstanceId));
        Assert.That(why, Does.Contain("耐久不足"), "0.8耐久不能执行成本1的点火");
        Assert.That(E.DoInstant(s, InstantRequest.Drop(lighter.InstanceId, 1)).Ok, Is.True, "仍可转移");
        Assert.That(E.DoInstant(s, InstantRequest.Pickup(lighter.InstanceId, 1)).Ok, Is.True);

        // 锅耐久不足：放进火堆不受热、显示维护不足，水与内容保留
        var pot = First(s, "pot");
        E.DebugGive(s, "dirty_water", 2, "camp");
        E.DoInstant(s, InstantRequest.FillPot(First(s, "dirty_water", "camp").InstanceId, pot.InstanceId, 2));
        pot.Durability = 0.1;
        E.DoInstant(s, InstantRequest.PutInFire(pot.InstanceId));
        var fac = s.Locations["camp"].Facility;
        fac.FireLit = true;
        fac.FuelMinutes = 120;
        Start(s, ActionRequest.Wait(30));
        Run(s);
        Assert.That(E.PotLiquid(s, pot.InstanceId), Is.EqualTo("dirty_water"), "不加工、不销毁水");
        Assert.That(E.PotUnits(s, pot.InstanceId), Is.EqualTo(2));
        Assert.That(E.ContentStatus(s, pot), Does.Contain("维护不足"));

        // 正常耐久：每次成功净水磨损0.2
        pot.Durability = 10;
        Start(s, ActionRequest.Wait(30));
        Run(s);
        Assert.That(E.PotLiquid(s, pot.InstanceId), Is.EqualTo("clean_water"));
        Assert.That(pot.Durability, Is.EqualTo(10 - Cfg.Durability.PotWear).Within(1e-9));

        // 维护：废金属1+零件1，恢复自身最大20%，不超过最大、不重抽
        var t = New();
        var lt = First(t, "lighter");
        var max = lt.MaxDurability!.Value;
        lt.Durability = 1;
        E.DebugGive(t, "scrap", 1);
        E.DebugGive(t, "parts", 1);
        Assert.That(E.CanStart(t, ActionRequest.Craft("repair_selected_tool")), Is.EqualTo("请先选择要维护的工具"));
        var req = ActionRequest.Craft("repair_selected_tool");
        req.InstanceId = lt.InstanceId;
        Start(t, req);
        Assert.That(t.Main!.BaseWorkMinutes, Is.EqualTo(30 * E.CraftWorkFactor(1)).Within(1e-9), "制造熟练影响维护工作量");
        Run(t);
        Assert.That(lt.Durability, Is.EqualTo(1 + max * 0.2).Within(1e-9));
        Assert.That(lt.MaxDurability, Is.EqualTo(max), "不重抽最大值");
        Assert.That(Count(t, "scrap") + Count(t, "parts"), Is.EqualTo(0));
        lt.Durability = max;
        Assert.That(E.CanStart(t, req), Does.Contain("耐久已满"), "满耐久禁用");
        lt.Durability = max - 0.5;
        Assert.That(E.CanStart(t, req), Does.Contain("缺少废金属"), "缺料不扣物");
        E.DebugGive(t, "scrap", 1);
        E.DebugGive(t, "parts", 1);
        Start(t, req);
        Run(t);
        Assert.That(lt.Durability, Is.EqualTo(max).Within(1e-9), "不超过自身最大");
        // 有内容的锅须先清空
        var tp = First(t, "pot");
        tp.Durability = 5;
        E.DebugGive(t, "clean_water", 1);
        E.DoInstant(t, InstantRequest.FillPot(First(t, "clean_water", "carried").InstanceId, tp.InstanceId, 1));
        var rp = ActionRequest.Craft("repair_selected_tool");
        rp.InstanceId = tp.InstanceId;
        E.DebugGive(t, "scrap", 1);
        E.DebugGive(t, "parts", 1);
        Assert.That(E.CanStart(t, rp), Does.Contain("清空"));
    }

    private void Infect(GameState s, double load, double immunity)
    {
        s.Body.Infected = true;
        s.Body.VirusLoad = load;
        s.Body.Immunity = immunity;
        s.Body.HighImmunityMinutes = 0;
    }

    /// <summary>R607（暴露部分）暴露概率随免疫降低而升高：I=0/40/70/85/100 → 60%/38%/21.5%/13.25%/5%；首次 V=20，再次 +10 并重置高免疫计时。咬击条件见战斗用例。</summary>
    [Test]
    public void R607_ExposureChance_FirstAndRepeat()
    {
        foreach (var (i, p) in new[] { (0.0, 0.60), (40.0, 0.38), (70.0, 0.215), (85.0, 0.1325), (100.0, 0.05) })
            Assert.That(E.ExposureChance(i), Is.EqualTo(p).Within(1e-12), $"I={i}");
        var s = New(3);
        var tries = 0;
        while (!s.Body.Infected && tries++ < 200) E.ExposeToVirus(s, out _);
        Assert.That(s.Body.Infected, Is.True);
        Assert.That(s.Body.VirusLoad, Is.EqualTo(20), "初次暴露 V=20");
        s.Body.HighImmunityMinutes = 300;
        var v0 = s.Body.VirusLoad;
        while (s.Body.VirusLoad == v0 && tries++ < 400) E.ExposeToVirus(s, out _);
        Assert.That(s.Body.VirusLoad, Is.EqualTo(30), "再次成功暴露 V+10");
        Assert.That(s.Body.HighImmunityMinutes, Is.EqualTo(0), "计时重置");
        var t = New();
        Assert.That(t.Body.VirusLoad, Is.EqualTo(0), "无感染时 V 恒为 0");
        Start(t, ActionRequest.Wait(60));
        Run(t);
        Assert.That(t.Body.VirusLoad, Is.EqualTo(0));
    }

    /// <summary>免疫每小时变化（v0.6 §08）：良好状态+0.5、遮蔽睡眠+2、低值惩罚可叠加、已感染−0.5；按段首状态、限制0—100。</summary>
    [Test]
    public void Immunity_RatesFromTickStart()
    {
        var s = New();
        s.Body.Immunity = 50;
        Start(s, ActionRequest.Wait(60));
        Run(s);
        Assert.That(s.Body.Immunity, Is.EqualTo(50.5).Within(1e-9), "饱食水分清醒≥50、健康≥50%、精神≥40：+0.5/h");
        var t = New();
        t.Locations["camp"].Facility.ShelterBuilt = true;
        t.Body.Immunity = 50;
        Start(t, ActionRequest.Sleep(120));
        Steps(t, 12);
        Assert.That(t.Body.Immunity, Is.EqualTo(52.5).Within(1e-9), "遮蔽睡眠+2，加良好+0.5");
        var u = New();
        SetStats(u, 4, 10, 10, 18, 10, 10);
        u.Body.Immunity = 50;
        Infect(u, 20, 50);
        Start(u, ActionRequest.Wait(30));
        Steps(u, 1);
        var expect = 50 + (-2 - 3 - 2 - 2 - 1 - 0.5) * 5 / 60.0;
        Assert.That(u.Body.Immunity, Is.EqualTo(expect).Within(1e-9), "N/W/S/H/M低与已感染惩罚叠加");
        u.Body.Immunity = 0.01;
        Steps(u, 1);
        Assert.That(u.Body.Immunity, Is.EqualTo(0), "下限0");
    }

    /// <summary>R609 绷带/抗生素把病毒压低：最低1且仍感染；回血与病毒冷却分开；读档不能再抑制一次。</summary>
    [Test]
    public void R609_Suppressants_MinOne_StillInfected_SeparateCooldowns()
    {
        var s = New();
        Infect(s, 5, 60);
        E.DebugGive(s, "bandage", 3);
        E.DebugGive(s, "antibiotics", 2);
        SetStats(s, 20, 80, 80, 18, 90, 60);   // 满血：绷带只压病毒
        Start(s, ActionRequest.UseItem("bandage"));
        var red = 2 * (1 + 0.01 * 1);
        Assert.That(s.Body.VirusLoad, Is.EqualTo(5 - red).Within(1e-9), "绷带病毒−2×(1+0.01×医疗L)");
        Assert.That(s.BandageReadyAt, Is.EqualTo(0), "未回血不启动回血冷却");
        Assert.That(s.Body.BandageVirusReadyAt, Is.EqualTo(480 + 360), "病毒减量冷却6小时");
        Run(s);
        Assert.That(E.CanStart(s, ActionRequest.UseItem("bandage")), Is.Not.Null, "两种效果都无收益时禁用");
        s.Stats.Health = 15;
        Assert.That(E.CanStart(s, ActionRequest.UseItem("bandage")), Is.Null, "回血就绪即可用（与病毒冷却分开）");
        Start(s, ActionRequest.UseItem("antibiotics"));
        Assert.That(s.Body.VirusLoad, Is.EqualTo(1), "抗生素−8，最低1");
        Assert.That(s.Body.Infected, Is.True, "不能把接近0当治愈");
        Assert.That(s.Body.AntibioticUntil, Is.EqualTo(s.GameMinute + 360));
        Run(s);
        var json = SaveSerializer.Serialize(s);
        var t = SaveSerializer.Deserialize(json);
        Assert.That(E.CanStart(t, ActionRequest.UseItem("antibiotics")), Does.Contain("给药间隔"), "读档后仍在间隔内，不能再抑制");
        // 抗生素有效期内正增长×0.5（I<40 → +4/h → +2/h）
        var u = New();
        Infect(u, 30, 30);
        u.Body.AntibioticUntil = u.GameMinute + 360;
        Start(u, ActionRequest.Wait(60));
        Run(u);
        Assert.That(u.Body.VirusLoad, Is.EqualTo(32).Within(1e-9));
        // 负向自然恢复不被减半
        var w = New();
        Infect(w, 30, 90);
        w.Body.AntibioticUntil = w.GameMinute + 360;
        Start(w, ActionRequest.Wait(60));
        Run(w);
        Assert.That(w.Body.VirusLoad, Is.EqualTo(28.5).Within(1e-9));
    }

    /// <summary>每小时保持良好照护（饱食、水分、清醒、精神与健康满足条件），只推进世界时间。</summary>
    private void CareFor(GameState s, int hours)
    {
        for (var h = 0; h < hours && !s.Progress.Dead; h++)
        {
            SetStats(s, 20, 80, 80, 18, 90, 60);
            Start(s, ActionRequest.Wait(60));
            Run(s);
        }
    }

    /// <summary>R610 I85 持续12小时：V20 自然治愈（12.0小时），V80 降到62仍感染（验证记录“病毒边界推演”）。</summary>
    [Test]
    public void R610_HighImmunity12h_CuresLowLoadOnly()
    {
        var s = New();
        Infect(s, 20, 85);
        CareFor(s, 11);
        Assert.That(s.Body.Infected, Is.True, "11小时未满");
        CareFor(s, 1);
        Assert.That(s.Body.Infected, Is.False, "12.0小时自然治愈");
        Assert.That(s.Body.VirusLoad, Is.EqualTo(0));
        Assert.That(s.Log.Any(l => l.Text.Contains("高免疫自然治愈")), Is.True, "日志记为高免疫治愈");
        var t = New();
        Infect(t, 80, 85);
        CareFor(t, 12);
        Assert.That(t.Body.Infected, Is.True);
        Assert.That(t.Body.VirusLoad, Is.EqualTo(62).Within(1e-9), "12小时后仅降至62");
        Assert.That(t.Body.HighImmunityMinutes, Is.EqualTo(720), "达到12小时但V还高则继续维持");
    }

    /// <summary>R611 免疫跌破85或再次暴露：连续计时重置，不能拼凑12小时。</summary>
    [Test]
    public void R611_TimerResetsOnDipOrReexposure()
    {
        var s = New();
        Infect(s, 5, 85);
        CareFor(s, 6);
        Assert.That(s.Body.HighImmunityMinutes, Is.EqualTo(360));
        s.Body.Immunity = 84.9;
        CareFor(s, 1);
        Assert.That(s.Body.HighImmunityMinutes, Is.EqualTo(0), "跌到85以下归0");
        s.Body.Immunity = 85;
        CareFor(s, 6);
        Assert.That(s.Body.Infected, Is.True, "6+6小时拼不出连续12小时");
        // 刚跨线：段首<85、段末≥85 不白拿一段
        var t = New();
        Infect(t, 5, 84.99);
        t.Locations["camp"].Facility.ShelterBuilt = true;
        SetStats(t, 20, 80, 80, 18, 90, 60);
        Start(t, ActionRequest.Sleep(120));
        Steps(t, 1);
        Assert.That(t.Body.Immunity, Is.GreaterThanOrEqualTo(85));
        Assert.That(t.Body.HighImmunityMinutes, Is.EqualTo(0), "段首未达85不计");
    }

    /// <summary>R612 病毒100/健康0与同段治愈：终局优先；尸变或死亡后不能用药补救。</summary>
    [Test]
    public void R612_EndgameBeforeCure()
    {
        // 同段：计时将满、病毒≤10，但健康在本段归零 → 死亡优先，不先治愈
        var s = New();
        Infect(s, 5, 90);
        s.Body.HighImmunityMinutes = 715;
        SetStats(s, 0.01, 80, 0, 18, 90, 60);
        Start(s, ActionRequest.Wait(30));
        Run(s);
        Assert.That(s.Progress.Dead, Is.True);
        Assert.That(s.Body.Infected, Is.True, "终局优先于自然治愈");
        // 病毒100尸变：即使健康还高也结束；之后不能用特效药
        var t = New();
        Infect(t, 99.9, 20);
        E.DebugGive(t, "antiviral_cure", 1);
        Start(t, ActionRequest.Wait(30));
        Run(t);
        Assert.That(t.Progress.Dead, Is.True);
        Assert.That(t.Progress.Death!.Zombified, Is.True);
        Assert.That(t.Progress.Death.Cause, Is.EqualTo(GameEngine.ZombifiedCause));
        Assert.That(t.Stats.Health, Is.GreaterThan(0), "健康还高也不能继续");
        Assert.That(E.Start(t, ActionRequest.UseItem("antiviral_cure")).Ok, Is.False, "尸变后不可补救");
    }

    /// <summary>R613 特效药：直接清感染、V归0、计时清0，不额外补健康/体力。</summary>
    [Test]
    public void R613_AntiviralCure_ClearsOnly()
    {
        var s = New();
        Infect(s, 55, 40);
        s.Body.HighImmunityMinutes = 100;
        SetStats(s, 10, 80, 80, 5, 90, 60);
        E.DebugGive(s, "antiviral_cure", 1);
        Start(s, ActionRequest.UseItem("antiviral_cure"));
        Assert.That(s.Body.Infected, Is.False);
        Assert.That(s.Body.VirusLoad, Is.EqualTo(0));
        Assert.That(s.Body.HighImmunityMinutes, Is.EqualTo(0));
        Assert.That(s.Stats.Health, Is.EqualTo(10), "不回健康");
        Assert.That(s.Stats.Energy, Is.EqualTo(5), "不回体力");
        Run(s);
        Assert.That(s.GameMinute, Is.EqualTo(485), "战外5分钟wait");
        var t = New();
        E.DebugGive(t, "antiviral_cure", 1);
        Assert.That(E.CanStart(t, ActionRequest.UseItem("antiviral_cure")), Does.Contain("未感染"));
    }

    /// <summary>诊所医药补给箱与村庄废弃冷藏箱（v0.6 §11）：开始时锁定随机部分，读档不重抽；特效药配方需技能、指定空锅、消耗净水。</summary>
    [Test]
    public void MedicineCaches_AndAntiviralRecipe()
    {
        var s = New(5);
        E.DebugTeleport(s, "clinic");
        Assert.That(s.Resources["clinic_medical_supply"].Discovered, Is.False, "探索100%才揭示");
        E.DebugSetProgress(s, "clinic", 100);
        Start(s, ActionRequest.Gather("clinic_medical_supply"));
        s.Main!.RareHit = true;
        var saved = SaveSerializer.Deserialize(SaveSerializer.Serialize(s));
        Assert.That(saved.Main!.RareHit, Is.True, "锁定结果随存档");
        Run(s);
        Assert.That(Count(s, "antibiotics", "clinic"), Is.EqualTo(2));
        Assert.That(Count(s, "lab_reagent", "clinic"), Is.EqualTo(1));
        Assert.That(Count(s, "antiviral_cure", "clinic"), Is.EqualTo(1));
        Assert.That(E.CanStart(s, ActionRequest.Gather("clinic_medical_supply")), Does.Contain("搜空"), "一次性");

        var t = New(6);
        E.DebugTeleport(t, "village");
        E.DebugSetProgress(t, "village", 100);
        Start(t, ActionRequest.Gather("village_cold_box"));
        var picked = t.Main!.LootItemId;
        Assert.That(picked, Is.AnyOf("antiviral_precursor", "bandage"));
        Run(t);
        Assert.That(Count(t, picked!, "village"), Is.EqualTo(picked == "bandage" ? 2 : 1));

        var u = New();
        foreach (var kv in Cfg.Recipes.Single(r => r.Id == "make_antiviral_cure").Inputs) E.DebugGive(u, kv.Key, kv.Value, "camp");
        var req = ActionRequest.Craft("make_antiviral_cure");
        Assert.That(E.CanStart(u, req), Does.Contain("需要医疗15级"));
        u.Skills["medicine"].Level = 15;
        u.Skills["crafting"].Level = 12;
        Assert.That(E.CanStart(u, req), Is.EqualTo("请指定一口取下并清空的锅"));
        var pot = First(u, "pot");
        E.DebugGive(u, "clean_water", 1);
        E.DoInstant(u, InstantRequest.FillPot(First(u, "clean_water", "carried").InstanceId, pot.InstanceId, 1));
        req.PotInstanceId = pot.InstanceId;
        Assert.That(E.CanStart(u, req), Does.Contain("清空"));
        E.DoInstant(u, InstantRequest.Decant(E.PotLiquids(u, pot.InstanceId)[0].InstanceId, "camp", 1));
        Assert.That(E.CanStart(u, req), Is.Null);
        var water = Count(u, "clean_water");
        Start(u, req);
        Run(u);
        Assert.That(Count(u, "antiviral_cure"), Is.EqualTo(1));
        Assert.That(Count(u, "clean_water"), Is.EqualTo(water - 2), "净水作为原料消耗");
        Assert.That(E.PotContents(u, pot.InstanceId), Is.Empty, "不自动装锅");
        Assert.That(u.Items.Any(i => i.InstanceId == pot.InstanceId), Is.True, "锅不消耗");
    }

    /// <summary>显示：两位小数向零截断，正且不足0.01写“&lt;0.01”（v0.6 §02）。</summary>
    [Test]
    public void Display_TwoDecimals_TinyPositive()
    {
        Assert.That(Fmt.N(17.539), Is.EqualTo("17.53"));
        Assert.That(Fmt.N(0.004), Is.EqualTo("<0.01"));
        Assert.That(Fmt.N(0), Is.EqualTo("0.00"));
        Assert.That(Fmt.N(19.9999999), Is.EqualTo("20.00"), "浮点累计误差不显示成19.99");
        Assert.That(Fmt.N(-0.4), Is.EqualTo("−0.40"));
        Assert.That(Fmt.T(4), Is.EqualTo("4"));
        Assert.That(Fmt.T(0.6), Is.EqualTo("0.6"));
        Assert.That(Fmt.Delta(1.2), Is.EqualTo("+1.20"));
        Assert.That(Fmt.Pct(0.519230769), Is.EqualTo("51.92%"));
    }
}
