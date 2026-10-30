using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>人物战斗与免疫系统修订参考案 v0.6 第 17 节接入验收 R601–R624（同名用例；涉及战斗的部分见 M42 起的补充）。</summary>
public class CombatV06Tests : BattleTestBase
{
    private double Hours(string key, Func<Config.RateBlock, double> f, double hours) => f(Cfg.HourlyRates[key]) * hours;

    /// <summary>
    /// R614 正常日程与低精神重活反例（验证记录 v0.6“24小时预算”）：按配置速率核对日程预算，
    /// 再用引擎实跑休息、遮蔽睡眠与低精神恢复，不只检查睡后最终体力。
    /// </summary>
    [Test]
    public void R614_NormalScheduleBudget_AndLowMoraleCounterExample()
    {
        // 正常日程：移动3h、探索2h、搜集4h、制造3h → 工作体力合计17.6（S12 §8 非战斗负率×0.8，原22）；休息2h回12；遮蔽睡眠5小时由0回20
        var work = -(Hours("travel", r => r.Energy, 3) + Hours("explore", r => r.Energy, 2) + Hours("gather", r => r.Energy, 4) + Hours("craft", r => r.Energy, 3));
        Assert.That(work, Is.EqualTo(17.6).Within(1e-9), "全天工作体力合计消耗17.60");
        Assert.That(Hours("rest", r => r.Energy, 2), Is.EqualTo(12).Within(1e-9), "两小时休息提供12");
        var water = -(Hours("travel", r => r.Hydration, 3) + Hours("explore", r => r.Hydration, 2) + Hours("gather", r => r.Hydration, 4)
                      + Hours("craft", r => r.Hydration, 3) + Hours("rest", r => r.Hydration, 2) + Hours("wait", r => r.Hydration, 2)
                      + Hours("sleep_sheltered", r => r.Hydration, 8));
        Assert.That(water, Is.EqualTo(70.2).Within(1e-9), "自然耗水70.2（原87.75×0.8）");
        var food = -(Hours("travel", r => r.Satiety, 3) + Hours("explore", r => r.Satiety, 2) + Hours("gather", r => r.Satiety, 4)
                     + Hours("craft", r => r.Satiety, 3) + Hours("rest", r => r.Satiety, 2) + Hours("wait", r => r.Satiety, 2)
                     + Hours("sleep_sheltered", r => r.Satiety, 8));
        Assert.That(food, Is.EqualTo(31.5).Within(1e-9), "饱食自然消耗31.5（原39.375×0.8）");

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

        // 反例：低精神时休息30分钟只补1.5（正常补3）；白昼轻装搜集30分钟耗0.96
        var u = New();
        SetStats(u, 20, 80, 80, 10, 90, 10);
        Start(u, ActionRequest.Rest(30));
        Run(u);
        Assert.That(u.Stats.Energy, Is.EqualTo(11.5).Within(1e-9), "精神<20时正E恢复×0.5");
        Assert.That(-Hours("gather", r => r.Energy, 0.5), Is.EqualTo(0.96).Within(1e-9));
        // 反例日程：重活12小时、休息1小时、露天睡眠8小时、精神低于20 → 日内体力必然归零（只看睡后体力会误判）。
        // 新费率下原“重活10小时”不再归零（18+3−17.92>0），反例改为搜集10h+移动2h（M57）
        var heavy = -(Hours("gather", r => r.Energy, 10) + Hours("travel", r => r.Energy, 2));
        var restLow = Hours("rest", r => r.Energy, 1) * Cfg.FacilityEffects.LowMoraleEnergyRecoveryFactor;
        Assert.That(Cfg.InitialStats.Energy + restLow - heavy, Is.LessThan(0), "不满足安全消耗预算：日内归零");
        var v = New();
        SetStats(v, 20, 80, 80, Cfg.InitialStats.Energy, 90, 10);
        E.DebugTeleport(v, "field");
        var minE = v.Stats.Energy;
        var zeroTicks = 0;
        for (var i = 0; i < 24 && !v.Progress.Dead; i++)
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
        // 点火5分钟按 craft 速率 E−0.08（R01 §4 新 craft 率）
        E.DebugGive(s, "wood", 1);
        E.DoInstant(s, InstantRequest.AddFuel());
        var lighter = First(s, "lighter");
        Start(s, ActionRequest.Ignite(lighter.InstanceId));
        Run(s);
        Assert.That(e0 - s.Stats.Energy, Is.EqualTo(0.08).Within(1e-9), "点火5分钟按craft速率−0.08");
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

        // B5（A06 §1，取代 v0.6“无10级上限”）：经验逐级扣门槛、可多级提升，到10级停；溢出报错不回零
        var s = New();
        Assert.That(s.Skills.Keys, Is.EquivalentTo(Cfg.Skills.Select(x => x.Id)), "新局15门技能");
        Assert.That(s.Skills.Values.All(x => x.Level == 1 && x.Xp == 0), Is.True);
        E.GainXp(s, "blade", 100 + 150);      // 1→2 需100，2→3 需150
        Assert.That(s.Skills["blade"].Level, Is.EqualTo(3));
        Assert.That(s.Skills["blade"].Xp, Is.EqualTo(0));
        long total = 0;
        for (var l = 3; l < 12; l++) total += E.NextLevelXp("blade", l);
        E.GainXp(s, "blade", total + 7);
        Assert.That(s.Skills["blade"].Level, Is.EqualTo(10), "10级封顶");
        Assert.That(s.Skills["blade"].Xp, Is.EqualTo(0), "满级不留溢出经验");
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
        // 新局开局的打火器有随机耐久；金属锅没有耐久（系统策划总册 v1.0 P02）
        var t = New(7);
        Assert.That(First(t, "pot").MaxDurability, Is.Null);
        Assert.That(First(t, "pot").Durability, Is.Null);
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

        // v1.0 P02：锅没有耐久，放进燃烧火堆即正常净水，不磨损、不显示维护不足
        var pot = First(s, "pot");
        E.DebugGive(s, "dirty_water", 2, "camp");
        E.DoInstant(s, InstantRequest.FillPot(First(s, "dirty_water", "camp").InstanceId, pot.InstanceId, 2));
        E.DoInstant(s, InstantRequest.PutInFire(pot.InstanceId));
        var fac = s.Locations["camp"].Facility;
        fac.FireLit = true;
        fac.FuelMinutes = 120;
        Start(s, ActionRequest.Wait(30));
        Run(s);
        Assert.That(E.PotLiquid(s, pot.InstanceId), Is.EqualTo("clean_water"));
        Assert.That(pot.Durability, Is.Null);
        Assert.That(E.ContentStatus(s, pot), Does.Not.Contain("维护不足"));

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
        // v1.0 P02：锅不是维护候选
        var tp = First(t, "pot");
        var rp = ActionRequest.Craft("repair_selected_tool");
        rp.InstanceId = tp.InstanceId;
        E.DebugGive(t, "scrap", 1);
        E.DebugGive(t, "parts", 1);
        Assert.That(E.CanStart(t, rp), Does.Contain("没有耐久"));
        Assert.That(E.RepairCandidates(t).Any(i => i.InstanceId == tp.InstanceId), Is.False);
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

    /// <summary>免疫每小时变化（R04 §2 Q7 新表）：良好状态+1、遮蔽睡眠+4、低值惩罚可叠加、已感染不再扣免疫；按段首状态、限制0—100。</summary>
    [Test]
    public void Immunity_RatesFromTickStart()
    {
        var s = New();
        s.Body.Immunity = 50;
        Start(s, ActionRequest.Wait(60));
        Run(s);
        Assert.That(s.Body.Immunity, Is.EqualTo(51).Within(1e-9), "饱食水分清醒≥50、健康≥50%、精神≥40：+1/h");
        var t = New();
        t.Locations["camp"].Facility.ShelterBuilt = true;
        t.Body.Immunity = 50;
        Start(t, ActionRequest.Sleep(120));
        Steps(t, 12);
        Assert.That(t.Body.Immunity, Is.EqualTo(55).Within(1e-9), "遮蔽睡眠+4，加良好+1");
        var u = New();
        SetStats(u, 4, 10, 10, 18, 10, 10);
        u.Body.Immunity = 50;
        Infect(u, 20, 50);
        Start(u, ActionRequest.Wait(30));
        Steps(u, 1);
        var expect = 50 + (-2 - 3 - 2 - 2 - 1) * 5 / 60.0;
        Assert.That(u.Body.Immunity, Is.EqualTo(expect).Within(1e-9), "N/W/S/H/M低惩罚叠加，已感染不再另扣（Q7）");
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

    /// <summary>R610/R04-A04 I80 持续12小时：V20 自然治愈（12.0小时），V80 降到62仍感染（Q7 门槛 85→80）。</summary>
    [Test]
    public void R610_HighImmunity12h_CuresLowLoadOnly()
    {
        var s = New();
        Infect(s, 20, 80);
        CareFor(s, 11);
        Assert.That(s.Body.Infected, Is.True, "11小时未满");
        CareFor(s, 1);
        Assert.That(s.Body.Infected, Is.False, "12.0小时自然治愈");
        Assert.That(s.Body.VirusLoad, Is.EqualTo(0));
        Assert.That(s.Log.Any(l => l.Text.Contains("高免疫自然治愈")), Is.True, "日志记为高免疫治愈");
        var t = New();
        Infect(t, 80, 80);
        CareFor(t, 12);
        Assert.That(t.Body.Infected, Is.True);
        Assert.That(t.Body.VirusLoad, Is.EqualTo(62).Within(1e-9), "12小时后仅降至62");
        Assert.That(t.Body.HighImmunityMinutes, Is.EqualTo(720), "达到12小时但V还高则继续维持");
    }

    /// <summary>R611 免疫跌破80或再次暴露：连续计时重置，不能拼凑12小时（Q7 门槛80）。</summary>
    [Test]
    public void R611_TimerResetsOnDipOrReexposure()
    {
        var s = New();
        Infect(s, 5, 80);
        CareFor(s, 6);
        Assert.That(s.Body.HighImmunityMinutes, Is.EqualTo(360));
        s.Body.Immunity = 79; // 健康奖励+1/h，1小时内各段段首都<80
        CareFor(s, 1);
        Assert.That(s.Body.HighImmunityMinutes, Is.EqualTo(0), "跌到80以下归0");
        s.Body.Immunity = 80;
        CareFor(s, 6);
        Assert.That(s.Body.Infected, Is.True, "6+6小时拼不出连续12小时");
        // 刚跨线：段首<80、段末≥80 不白拿一段
        var t = New();
        Infect(t, 5, 79.99);
        t.Locations["camp"].Facility.ShelterBuilt = true;
        SetStats(t, 20, 80, 80, 18, 90, 60);
        Start(t, ActionRequest.Sleep(120));
        Steps(t, 1);
        Assert.That(t.Body.Immunity, Is.GreaterThanOrEqualTo(80));
        Assert.That(t.Body.HighImmunityMinutes, Is.EqualTo(0), "段首未达80不计");
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
        var xp0 = s.Skills["medicine"].Xp;
        Start(s, ActionRequest.UseItem("antiviral_cure"));
        Assert.That(s.Body.Infected, Is.False);
        Assert.That(s.Skills["medicine"].Xp, Is.EqualTo(xp0 + Cfg.Xp.Medical), "清除感染是有效医疗，给医疗经验一次（试玩修复 L2）");
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
        Assert.That(E.CanStart(u, req), Does.Contain("需要医疗10级"), "B5：旧医疗15/制造12门槛改为医疗10/制造10（A06 §4）");
        u.Skills["medicine"].Level = 10;
        u.Skills["crafting"].Level = 10;
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

    // ───────────── 世界敌人（v0.6 §14、§15） ─────────────

    /// <summary>冻结刷新与巡游（测试接敌时避免世界敌人自己走动）。</summary>
    private static void FreezeWorld(GameState s)
    {
        s.SpawnNextCheck = int.MaxValue;
        s.PatrolNextCheck = int.MaxValue;
    }

    /// <summary>空闲推进到指定分钟，期间保持身体良好（只看世界层面的变化）。</summary>
    private void Idle(GameState s, int minute)
    {
        while (s.GameMinute < minute && !s.Progress.Dead)
        {
            if (s.Main == null) Start(s, ActionRequest.Wait(30));
            SetStats(s, 20, 80, 80, 18, 90, 60);
            E.Advance(s);
            if (s.Main?.Status == ActionStatus.Paused) E.Resume(s);
        }
        if (s.Main?.Type == ActionType.Wait) E.Cancel(s);
    }

    /// <summary>新局初始6只，按配置分布；营地0只。</summary>
    [Test]
    public void WorldEnemies_InitialDistribution()
    {
        var s = New();
        Assert.That(E.LiveCount(s), Is.EqualTo(6));
        foreach (var kv in Cfg.WorldEnemies.Initial)
            Assert.That(s.WorldEnemies.Where(w => w.LocationId == kv.Key).Select(w => w.EnemyId), Is.EquivalentTo(kv.Value), kv.Key);
        Assert.That(s.WorldEnemies.Select(w => w.Id).Distinct().Count(), Is.EqualTo(6), "持久ID唯一");
        Assert.That(s.SpawnNextCheck, Is.EqualTo(1440), "第2日00:00起刷新检查");
        Assert.That(s.WorldEnemies.Single(w => w.EnemyId == "feral_dog").Hostile, Is.True);
    }

    /// <summary>R616 全局12与本地上限：新生成及巡游都不越界，交战存活敌计数，营地不进。</summary>
    [Test]
    public void R616_GlobalAndLocalCaps_NeverExceeded()
    {
        var s = New(99);
        AdvanceTo(s, At(2, 0, 0));
        for (var day = 0; day < 30; day++)
        {
            for (var k = 0; k < 24 * 12; k++)
            {
                if (s.Main == null) Start(s, ActionRequest.Wait(60));
                SetStats(s, 20, 80, 80, 18, 90, 60);
                E.Advance(s);
                if (s.Main?.Status == ActionStatus.Paused) E.Resume(s);
                Assert.That(E.LiveCount(s), Is.LessThanOrEqualTo(12), "全局上限12");
                foreach (var loc in Cfg.Locations.Select(l => l.Id))
                    Assert.That(E.LocalCount(s, loc), Is.LessThanOrEqualTo(Cfg.WorldEnemies.CapOf(loc)), loc);
            }
        }
        Assert.That(E.LocalCount(s, "camp"), Is.EqualTo(0), "营地始终0");
        Assert.That(E.LiveCount(s), Is.GreaterThan(6), "缓慢补敌");
        // 交战中的存活敌计数：标记交战不改变计数
        s.WorldEnemies[0].Engaged = true;
        Assert.That(E.LiveCount(s), Is.EqualTo(s.WorldEnemies.Count));
    }

    /// <summary>R617 无本地敌人时完成探索/清理：接敌率0，不凭空造敌。</summary>
    [Test]
    public void R617_NoLocalEnemies_NoEncounter()
    {
        var s = New();
        E.DebugTeleport(s, "hardware");
        s.GameMinute = At(2, 9, 0);
        FreezeWorld(s);
        Assert.That(E.LocalCount(s, "hardware"), Is.EqualTo(0));
        Assert.That(E.EncounterChance(s, "hardware", ActionType.Explore), Is.EqualTo(0));
        var before = E.LiveCount(s);
        Start(s, ActionRequest.Explore());
        s.Main!.EncounterRoll = 0;   // 即使抽到最小值
        Run(s);
        Assert.That(s.Encounter, Is.Null, "不凭空造敌");
        Assert.That(E.LiveCount(s), Is.EqualTo(before));
    }

    /// <summary>R618 低遭遇与绕行保护：概率=基础×本地敌对数/上限；第1日与营地为0；绕行后60分钟内不再随机接敌。</summary>
    [Test]
    public void R618_DensityRisk_BypassGrace()
    {
        var s = New();
        E.DebugTeleport(s, "village");
        Assert.That(E.EncounterChance(s, "village", ActionType.Explore), Is.EqualTo(0), "第1日不随机接敌");
        s.GameMinute = At(2, 9, 0);
        FreezeWorld(s);
        Assert.That(E.EncounterChance(s, "village", ActionType.Explore), Is.EqualTo(0.02 * 2 / 4).Within(1e-12), "探索2%×2/4");
        Assert.That(E.EncounterChance(s, "village", ActionType.Clear), Is.EqualTo(0.01 * 2 / 4).Within(1e-12), "清理1%×2/4");
        Assert.That(E.EncounterChance(s, "camp", ActionType.Clear), Is.EqualTo(0), "营地0");

        // 强制命中：先提交探索结果，再出现敌踪（2只同地游荡尸，按最早出现）
        Start(s, ActionRequest.Explore());
        ForceLoot(s, "cloth");
        s.Main!.EncounterRoll = 0;
        Assert.That(Run(s), Is.EqualTo(SegmentStatus.Completed));
        Assert.That(s.Locations["village"].Progress, Is.EqualTo(25), "原探索已提交");
        Assert.That(Count(s, "cloth", "village"), Is.EqualTo(1), "随机物资已提交、不回滚");
        Assert.That(s.Encounter, Is.Not.Null);
        Assert.That(s.Encounter!.EnemyIds, Is.EqualTo(s.WorldEnemies.Where(w => w.LocationId == "village").OrderBy(w => w.Id).Select(w => w.Id)));
        Assert.That(E.IsBusy(s), Is.True);
        Assert.That(E.CanStart(s, ActionRequest.Explore()), Does.Contain("接敌或绕行"));
        Assert.That(E.DoInstant(s, InstantRequest.Pickup(First(s, "cloth", "village").InstanceId, 1)).Ok, Is.False, "敌踪待选时锁定实体操作");

        // 绕行：10分钟按 travel 消耗，敌人仍在；之后60分钟保护
        var e0 = s.Stats.Energy;
        Start(s, ActionRequest.Bypass());
        Assert.That(s.Encounter, Is.Null);
        Run(s);
        Assert.That(s.GameMinute, Is.EqualTo(At(2, 9, 40)));
        Assert.That(e0 - s.Stats.Energy, Is.EqualTo(1.28 * 10 / 60).Within(1e-9), "按 travel 消耗（非战斗新率−1.28/h）");
        Assert.That(E.LocalCount(s, "village"), Is.EqualTo(2), "绕行不杀死世界敌人");
        Assert.That(E.EncounterChance(s, "village", ActionType.Explore), Is.EqualTo(0), "保护期内");
        Start(s, ActionRequest.Explore());
        s.Main!.EncounterRoll = 0;
        Run(s);
        Assert.That(s.Encounter, Is.Null, "60分钟保护期不重复随机接敌");
        Idle(s, s.EncounterGraceUntil);
        Assert.That(E.EncounterChance(s, "village", ActionType.Explore), Is.GreaterThan(0));
        // 预览不触发接敌、不推进随机
        var json = SaveSerializer.Serialize(s);
        E.Preview(s, ActionRequest.Explore());
        Assert.That(SaveSerializer.Serialize(s), Is.EqualTo(json));
    }

    /// <summary>R619 刷新受阻/长时间离线：无欠账爆刷，离线（不推进）无新敌。</summary>
    [Test]
    public void R619_NoSpawnDebt_NoOffline()
    {
        var s = New(3);
        var json = SaveSerializer.Serialize(s);
        // “离线”即不推进：读档后世界敌人不变
        var t = SaveSerializer.Deserialize(json);
        Assert.That(t.WorldEnemies.Count, Is.EqualTo(s.WorldEnemies.Count));
        // 全局满12时刷新受阻；腾出空位后只按之后的检查点缓慢补，不一次补齐
        while (E.LiveCount(s) < 12)
            foreach (var loc in Cfg.WorldEnemies.LocalCaps.Keys.Where(l => l != "camp"))
                if (E.LiveCount(s) < 12 && E.LocalCount(s, loc) < Cfg.WorldEnemies.CapOf(loc)) E.DebugSpawnEnemy(s, "shambler", loc);
        Idle(s, At(3, 0, 0));
        Assert.That(E.LiveCount(s), Is.EqualTo(12), "满员不刷新");
        s.WorldEnemies.RemoveRange(0, 6);
        var next = s.SpawnNextCheck;
        Assert.That(next, Is.GreaterThan(s.GameMinute));
        Idle(s, next);
        Assert.That(E.LiveCount(s), Is.LessThanOrEqualTo(7), "腾出空位后一次检查至多新增1只，无欠账");
    }

    /// <summary>R620 强敌/未实现层级：层级按96/3.5/0.5抽；该层没有合法地点则本次不刷新，不保底、不改用更强或更弱的怪填空。</summary>
    [Test]
    public void R620_RarityTiers_NoUpgradeNoFill()
    {
        Assert.That(E.RollRarity(0.0), Is.EqualTo("common"));
        Assert.That(E.RollRarity(0.9599), Is.EqualTo("common"));
        Assert.That(E.RollRarity(0.9601), Is.EqualTo("rare"));
        Assert.That(E.RollRarity(0.9949), Is.EqualTo("rare"));
        Assert.That(E.RollRarity(0.9951), Is.EqualTo("elite"));
        var s = New();
        s.CurrentLocation = "camp";
        var elite = E.SpawnCandidates(s, "elite");
        Assert.That(elite, Does.Not.Contain("stream").And.Not.Contain("clinic"), "溪流、诊所无强敌池");
        Assert.That(elite, Does.Not.Contain("camp"));
        Assert.That(E.SpawnCandidates(s, "common"), Does.Contain("stream").And.Contain("clinic"), "普通层可在溪流、诊所刷新");
        // 实际刷新：溪流与诊所从不出现强敌（不用强怪填空，也不把空池换成别的层）；强敌只出自强敌池
        var u = New(2024);
        u.CurrentLocation = "camp";
        var spawned = new List<(string loc, string id)>();
        for (var i = 0; i < 6000; i++)
        {
            u.WorldEnemies.Clear();
            var w = E.DebugSpawnCheck(u);
            if (w != null) spawned.Add((w.LocationId, w.EnemyId));
        }
        Assert.That(spawned.Count / 6000.0, Is.EqualTo(0.25).Within(0.03), "25%尝试新增");
        foreach (var (loc, id) in spawned)
        {
            var tier = Cfg.Enemies.Single(x => x.Id == id).Rarity;
            Assert.That(Cfg.WorldEnemies.Pools[loc][tier].Select(p => p.EnemyId), Does.Contain(id), $"{loc} 的 {id} 来自该地该层池");
            if (loc is "stream" or "clinic") Assert.That(tier, Is.Not.EqualTo("elite"));
            Assert.That(loc, Is.Not.EqualTo("camp"));
        }
        // 统计：2万次检查里强敌约占成功刷新的0.5%，没有保底
        var t = New(12345);
        var counts = new Dictionary<string, int>();
        for (var i = 0; i < 20000; i++)
        {
            var tier = E.RollRarity(t.Rng.NextDouble());
            counts[tier] = counts.GetValueOrDefault(tier) + 1;
        }
        Assert.That(counts["elite"] / 20000.0, Is.EqualTo(0.005).Within(0.002));
        Assert.That(counts["rare"] / 20000.0, Is.EqualTo(0.035).Within(0.006));
    }

    /// <summary>巡游：只走物理相邻边、不进营地、交战敌不动、全局数不变；进入当前地块提示“新的敌踪出现”。</summary>
    [Test]
    public void Patrol_PhysicalEdges_NotCamp_EngagedLocked()
    {
        var s = New(77);
        s.SpawnNextCheck = int.MaxValue;
        var engaged = s.WorldEnemies.First(w => w.LocationId == "village");
        engaged.Engaged = true;
        var positions = new List<string>();
        for (var k = 0; k < 40; k++)
        {
            var before = s.WorldEnemies.ToDictionary(w => w.Id, w => w.LocationId);
            Idle(s, s.PatrolNextCheck);
            Assert.That(E.LiveCount(s), Is.EqualTo(6), "巡游不改变总数");
            foreach (var w in s.WorldEnemies)
            {
                if (before[w.Id] != w.LocationId)
                    Assert.That(E.PhysicalNeighbors(before[w.Id]), Does.Contain(w.LocationId), "一次至多走一条物理相邻边");
                Assert.That(w.LocationId, Is.Not.EqualTo("camp"));
            }
            positions.Add(engaged.LocationId);
        }
        Assert.That(positions.Distinct().Single(), Is.EqualTo("village"), "交战敌锁住不巡游");
    }

    // ───────────── 战斗（v0.6 §04–§07、§15） ─────────────

    private void Ready(GameState s)
    {
        PlayerFirst(s);
        E.BattleStep(s);
        Assert.That(E.AwaitingPlayer(s), Is.True);
    }

    /// <summary>让敌人在本轮先手并执行它锁定的意图（测试敌方攻击用）。</summary>
    private void EnemyActsNow(GameState s, int index, string intent)
    {
        var b = s.Battle!;
        var e = Enemy(s, index);
        e.Intent = intent;
        b.Queue = new List<int> { index, 0 };
        b.Cursor = 0;
        b.PlayerActing = false;
        E.BattleStep(s);
    }

    /// <summary>R602 高能力命中/高闪避：概率用比值 A/(A+D)，不超过1，无95%硬夹紧；1级斧对游荡尸51.92%，游荡尸对玩家35.71%。</summary>
    [Test]
    public void R602_RatioHitChance_NoClamp()
    {
        var s = Fight(31, "shambler");
        Ready(s);
        Arm(s, "hatchet");
        var t = E.PreviewCommand(s, BattleCommand.Of("basic_attack", 1)).Targets.Single();
        Assert.That(t.AccuracyRating, Is.EqualTo(27));
        Assert.That(t.HitChance, Is.EqualTo(27.0 / 52).Within(1e-12), "51.92%");
        Assert.That(GameEngine.HitChance(20, 36), Is.EqualTo(20.0 / 56).Within(1e-12), "普通尸对玩家35.71%");
        // B5（A06 §1）：熟练度最高10级、简易斧练“斧”；“趋近但不超过1”改按公式核对
        s.Skills["axe"].Level = 100000;
        var hi = E.PreviewCommand(s, BattleCommand.Of("basic_attack", 1)).Targets.Single();
        Assert.That(hi.AccuracyRating, Is.EqualTo(24 + 3 * 10), "超过10级按10级");
        Assert.That(GameEngine.HitChance(24 + 3 * 100000, 25), Is.LessThan(1).And.GreaterThan(0.999));
        Assert.That(hi.CritChance, Is.LessThan(1), "暴击概率 C/(C+100) 也不超过1");
        Assert.That(GameEngine.HitChance(1, 1000), Is.LessThan(0.05), "无5%下限");
    }

    /// <summary>R603 小伤害与高甲：不强行补最低1伤，护甲高于伤害时完全挡住；显示不把健康/成本取整成0。</summary>
    [Test]
    public void R603_SmallDamage_ArmorBlocks_NoMinimumOne()
    {
        var s = Fight(32, "riot");
        Ready(s);
        SetStats(s, 20, 80, 80, 0, 90, 60);   // 体力0：只能勉强拳击
        var t = E.PreviewCommand(s, BattleCommand.Of("desperate_punch", 1)).Targets.Single();
        Assert.That(t.DamageMax, Is.EqualTo(0), "0.9×1.035×0.5−0.7<0 → 0，不补1");
        var hp0 = Enemy(s, 1).Hp;
        Force(s, Below(0.001), AtLeast(0.999), Any());
        Cmd(s, "desperate_punch", 1);
        Assert.That(Enemy(s, 1).Hp, Is.EqualTo(hp0), "完全挡住");
        Assert.That(s.Skills["unarmed"].Xp, Is.EqualTo(Cfg.Xp.DamagingAttackCommand), "v1.0 P01：完全挡住的0伤命中也给命中经验");
        Assert.That(s.Battle!.Log.Last(), Does.Contain("护甲完全挡住"));
        // 工尸护甲0.3：勉强拳击最大0.9×1.035×0.5−0.3=0.16575，小数伤害如实生效
        var w = Fight(33, "worker");
        Ready(w);
        SetStats(w, 20, 80, 80, 0, 90, 60);
        var tw = E.PreviewCommand(w, BattleCommand.Of("desperate_punch", 1)).Targets.Single();
        Assert.That(tw.DamageMax, Is.EqualTo(0.9 * 1.035 * 0.5 - 0.3).Within(1e-12));
        Assert.That(Fmt.N(0.004), Is.EqualTo("<0.01"));
        Assert.That(E.PreviewCommand(w, BattleCommand.Of("guard")).EnergyCost, Is.EqualTo(0));
    }

    /// <summary>R606 瞄准入口与增益均已删除；弩/枪装填正常；弓不强制装填、每射消耗随身箭。</summary>
    [Test]
    public void R606_NoAim_ReloadOk_BowLoose()
    {
        Assert.That(Cfg.CombatActions.Any(a => a.Id == "aim"), Is.False);
        var s = Fight(34, "shambler", "shambler");
        Ready(s);
        var bow = Arm(s, "crossbow");
        Assert.That(E.BattleOptions(s).Select(o => o.Action), Does.Not.Contain("aim"));
        Assert.That(E.CommandBlocked(s, BattleCommand.Of("aim")), Is.EqualTo("瞄准已移除"));
        E.DebugGive(s, "bolt", 3);
        Assert.That(Cmd(s, "reload").Ok, Is.True);
        Assert.That(bow.LoadedAmmo, Is.EqualTo(1), "弩弹仓1");
        var wb = Arm(s, "wooden_bow");
        Assert.That(E.BattleOptions(s).Any(o => o.Action == "reload"), Is.False, "弓没有装填按钮");
        Assert.That(E.CommandBlocked(s, BattleCommand.Of("basic_attack", 1)), Does.Contain("随身没有箭矢"));
        E.DebugGive(s, "arrow", 2);
        Assert.That(Cmd(s, "basic_attack", 1).Ok, Is.True);
        Assert.That(Count(s, "arrow"), Is.EqualTo(1), "射击直接消耗随身箭1");
        Assert.That(s.Battle!.Noise, Is.EqualTo(0), "弓与近战噪声0");
    }

    /// <summary>R607 同次丧尸咬击：只有命中且正伤害才判一次暴露；抓击命中不判。</summary>
    [Test]
    public void R607_BiteOnlyOnHitWithDamage()
    {
        // 抓击命中：不暴露（即使暴露判定值很小）
        var s = Fight(35, "shambler");
        Force(s, Below(0.01), Any(), Below(0.0001));
        EnemyActsNow(s, 1, "attack");
        Assert.That(s.Stats.Health, Is.LessThan(20), "抓击命中");
        Assert.That(s.Body.Infected, Is.False, "抓击不判病毒");
        // 咬击未命中：不判
        var t = Fight(36, "shambler");
        Force(t, AtLeast(0.999), Below(0.0001));
        EnemyActsNow(t, 1, "bite");
        Assert.That(t.Body.Infected, Is.False);
        Assert.That(t.Stats.Health, Is.EqualTo(20));
        // 咬击命中有伤害：一次暴露判定（判定值小于概率 → 感染 V=20）
        var u = Fight(37, "shambler");
        u.Body.Immunity = 70;
        Force(u, Below(0.01), Any(), Below(0.215 - 0.001));
        EnemyActsNow(u, 1, "bite");
        Assert.That(u.Body.Infected, Is.True);
        Assert.That(u.Body.VirusLoad, Is.EqualTo(20));
        // 同样的咬击，判定值高于概率 → 不感染；免疫越低概率越高
        var v = Fight(38, "shambler");
        v.Body.Immunity = 70;
        Force(v, Below(0.01), Any(), AtLeast(0.215 + 0.001));
        EnemyActsNow(v, 1, "bite");
        Assert.That(v.Body.Infected, Is.False);
        var w = Fight(39, "shambler");
        w.Body.Immunity = 0;
        Force(w, Below(0.01), Any(), x => x > 0.3 && x < 0.59);
        EnemyActsNow(w, 1, "bite");
        Assert.That(w.Body.Infected, Is.True, "免疫0时60%");
        // 意图20%：多场统计丧尸咬击意图约占20%
        var bites = 0;
        var total = 0;
        for (uint seed = 100; seed < 400; seed++)
        {
            var f = Fight(seed, "shambler");
            total++;
            if (f.Battle!.Enemies[0].Intent == "bite") bites++;
        }
        Assert.That(bites / (double)total, Is.EqualTo(0.2).Within(0.07));
    }

    /// <summary>R608 抓击、动物咬击、完全挡伤：不产生僵尸病毒；动物仍可流血。</summary>
    [Test]
    public void R608_AnimalBite_NoVirus_CanBleed()
    {
        var s = Fight(40, "feral_dog");
        s.Body.Immunity = 0;
        Force(s, Below(0.01), Any(), Below(0.2));
        EnemyActsNow(s, 1, "attack");
        Assert.That(s.Body.Infected, Is.False, "动物撕咬不判僵尸病毒");
        Assert.That(s.Body.BleedTriggers, Is.EqualTo(3), "25%出血 0.4×3");
        Assert.That(s.Body.BleedDamage, Is.EqualTo(0.4));
        Assert.That(Cfg.Enemies.Where(x => x.Family == "animal").All(x => x.BiteIntentWeightPct == 0), Is.True);
    }

    /// <summary>R621 噪声增援：只调用已有同地丧尸，世界活敌总数不增加；没有可用实例则没有增援。</summary>
    [Test]
    public void R621_Reinforcement_ExistingOnly()
    {
        var s = Fight(41, "shambler");
        var dog = E.DebugSpawnEnemy(s, "feral_dog", "field");
        Ready(s);
        var gun = Arm(s, "pistol");
        gun.LoadedAmmo = 8;
        s.Battle!.Noise = 99;
        Force(s, AtLeast(0.999));
        Cmd(s, "basic_attack", 1);
        var live = E.LiveCount(s);
        Cmd(s, "end_turn");
        ToPlayer(s);
        Assert.That(s.Battle!.Enemies.Count, Is.EqualTo(1), "同地只有野犬（非丧尸）：没有增援，也不新造游荡尸");
        Assert.That(s.Battle.ReinforcementUsed, Is.True, "请求已消耗");
        Assert.That(E.LiveCount(s), Is.EqualTo(live));
        Assert.That(s.WorldEnemies.Single(w => w.Id == dog.Id).Engaged, Is.False);
    }

    /// <summary>R622 撤退/巡游/重接同敌：HP、经验次数、掉落记录跟随 worldEnemyId。</summary>
    [Test]
    public void R622_StateFollowsWorldEnemyId()
    {
        var s = Fight(42, "shambler");
        Ready(s);
        Arm(s, "hatchet");
        Force(s, Below(0.01), AtLeast(0.99), Below(0.05));
        Cmd(s, "basic_attack", 1);
        var w = s.WorldEnemies.Single();
        var hp = s.Battle!.Enemies[0].Hp;
        Cmd(s, "end_turn");
        ToPlayer(s);
        Force(s, Below(0.0001));
        Cmd(s, "retreat");
        Assert.That(s.Battle, Is.Null);
        Assert.That(w.Hp, Is.EqualTo(hp).Within(1e-9), "撤退后HP保留");
        Assert.That(w.XpHits["axe"], Is.EqualTo(1));
        // 巡游走到别处再走回来：同一实例
        w.LocationId = "stream";
        w.LocationId = "field";
        s.EncounterGraceUntil = 0;
        s.Encounter = new PendingEncounter { LocationId = "field", EnemyIds = { w.Id } };
        E.Engage(s);
        Assert.That(s.Battle!.Enemies[0].WorldId, Is.EqualTo(w.Id));
        Assert.That(s.Battle.Enemies[0].Hp, Is.EqualTo(hp).Within(1e-9), "换 encounterId 也不刷满血");
        Assert.That(s.Battle.Enemies[0].XpHits["axe"], Is.EqualTo(1), "经验计数不重置");
    }

    /// <summary>R615（战斗部分）：战斗每轮只推进一次 tick，体力自然率0，只付招式成本，不双扣采集消耗。</summary>
    [Test]
    public void R615_BattleTick_NoGatherDoubleCharge()
    {
        var s = Fight(43, "stag");
        Ready(s);
        var e0 = s.Stats.Energy;
        var n0 = s.Stats.Satiety;
        var m0 = s.GameMinute;
        Cmd(s, "guard");
        Cmd(s, "end_turn");
        while (s.Battle!.Round == 1) E.BattleStep(s);
        Assert.That(s.GameMinute, Is.EqualTo(m0 + 5));
        Assert.That(s.Stats.Energy, Is.EqualTo(e0), "防御体力0，tick 体力自然率0");
        Assert.That(n0 - s.Stats.Satiety, Is.EqualTo(1.875 * 5 / 60).Within(1e-9), "战斗速率N−1.875/h");
    }

    /// <summary>战斗中途存档：读档恢复覆盖层与队列游标，敌人不会再行动一次（v0.5 §17）。</summary>
    [Test]
    public void SaveMidBattle_ResumesCursor()
    {
        var s = Fight(44, "shambler", "crawler");
        ToPlayer(s);
        var json = SaveSerializer.Serialize(s);
        var t = SaveSerializer.Deserialize(json);
        Assert.That(t.Battle, Is.Not.Null);
        Assert.That(E.AwaitingPlayer(t), Is.EqualTo(E.AwaitingPlayer(s)));
        Assert.That(t.Battle!.Cursor, Is.EqualTo(s.Battle!.Cursor));
        Assert.That(t.Battle.Log.Count, Is.EqualTo(s.Battle.Log.Count), "敌人不重复行动");
        Cmd(s, "end_turn");
        Cmd(t, "end_turn");
        for (var i = 0; i < 6; i++) { E.BattleStep(s); E.BattleStep(t); }
        Assert.That(SaveSerializer.Serialize(t), Is.EqualTo(SaveSerializer.Serialize(s)));
    }

    /// <summary>显示：两位小数向零截断，正且不足0.01写“&lt;0.01”（v0.6 §02）。</summary>
    [Test]
    public void Display_TwoDecimals_TinyPositive()
    {
        Assert.That(Fmt.N(17.539), Is.EqualTo("17.54"), "四舍五入到两位（试玩修复 D1）");
        Assert.That(Fmt.N(17.534), Is.EqualTo("17.53"));
        Assert.That(Fmt.N(3.6 - 0.8), Is.EqualTo("2.80"), "浮点误差不显示成2.79");
        Assert.That(Fmt.N(3.2 - 1.157), Is.EqualTo("2.04"));
        Assert.That(Fmt.N(1.157), Is.EqualTo("1.16"), "被减数与差同一口径：3.20−1.16=2.04");
        Assert.That(Fmt.N(1.035), Is.EqualTo("1.04"), "二进制略小于1.035的值按吸附后进位");
        Assert.That(Fmt.N(0.006), Is.EqualTo("<0.01"), "正且小于0.01仍写<0.01，不进位成0.01");
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
