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
