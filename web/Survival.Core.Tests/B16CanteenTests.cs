using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 B16（R49）：分量水壶（S10 X4、A07 X1.5）——容量 2000、空重 400、只装同种水、装入/倒出 500 整份、壶内 250 直接喝
/// （5 分钟 W+15，脏水同样腹泻 12 小时，战内禁饮）、不增水不混水、重量按真实毫升；存档 28 迁移。验收 ES04、SUP04（水壶部分）。
/// </summary>
public class B16CanteenTests : TestBase
{
    private (GameState s, ItemInstance canteen) Setup(int clean = 4, int dirty = 0)
    {
        var s = New();
        s.Items.RemoveAll(i => i.ItemId is "clean_water" or "dirty_water");
        var c = E.CreateItems(s, "ration_canteen", 1, ItemInstance.Carried).Single();
        if (clean > 0) E.CreateItems(s, "clean_water", clean, ItemInstance.Carried);
        if (dirty > 0) E.CreateItems(s, "dirty_water", dirty, s.CurrentLocation);
        return (s, c);
    }

    private void Ok(ActionResult r) => Assert.That(r.Ok, Is.True, r.Reason);

    [Test]
    public void ES04_FillPourSip_NoWaterCreated_WeightExact()
    {
        var (s, c) = Setup(clean: 4);
        var g0 = E.CarriedGrams(s);
        var water = First(s, "clean_water", ItemInstance.Carried);
        Ok(E.DoInstant(s, InstantRequest.FillCanteen(water.InstanceId, c.InstanceId, 3)));
        Assert.That((c.LiquidId, c.LiquidMl), Is.EqualTo(("clean_water", 1500)));
        Assert.That(Count(s, "clean_water"), Is.EqualTo(1));
        Assert.That(E.CarriedGrams(s), Is.EqualTo(g0), "随身水卡进随身水壶：总重不变");
        Assert.That(E.ItemGrams(s, c), Is.EqualTo(400 + 1500));

        // 壶内喝一口：5 分钟、W+15、−250ml；不能凭空倒成 500 卡
        s.Stats.Hydration = 50;
        var t0 = s.GameMinute;
        Start(s, ActionRequest.UseInstance(c.InstanceId));
        Run(s);
        Assert.That(s.GameMinute - t0, Is.EqualTo(5));
        Assert.That(c.LiquidMl, Is.EqualTo(1250));
        Assert.That(s.Stats.Hydration, Is.GreaterThan(64).And.LessThan(65.1), "W+15，再扣 5 分钟等待消耗");

        // 倒出两份 500 到地面，剩 250 只能喝
        Ok(E.DoInstant(s, InstantRequest.PourCanteen(c.InstanceId, s.CurrentLocation, 2)));
        Assert.That(Count(s, "clean_water", s.CurrentLocation), Is.EqualTo(2));
        Assert.That(c.LiquidMl, Is.EqualTo(250));
        Assert.That(E.MaxPourCanteen(s, c, s.CurrentLocation, out var why), Is.EqualTo(0));
        Assert.That(why, Does.Contain("不够倒成一份"));
        Start(s, ActionRequest.UseInstance(c.InstanceId));
        Run(s);
        Assert.That((c.LiquidId, c.LiquidMl), Is.EqualTo(((string?)null, 0)));
        Assert.That(E.CanStart(s, ActionRequest.UseInstance(c.InstanceId)), Does.Contain("空"));
    }

    [Test]
    public void ES04_NoMixing_Capacity_DirtySipDiarrhea()
    {
        var (s, c) = Setup(clean: 1, dirty: 5);
        var dirty = First(s, "dirty_water", s.CurrentLocation);
        Ok(E.DoInstant(s, InstantRequest.FillCanteen(dirty.InstanceId, c.InstanceId, 4)));
        Assert.That(c.LiquidMl, Is.EqualTo(2000));
        Assert.That(E.MaxFillCanteen(s, First(s, "dirty_water"), c, out var full), Is.EqualTo(0));
        Assert.That(full, Does.Contain("已满"));
        Assert.That(E.MaxFillCanteen(s, First(s, "clean_water"), c, out var mix), Is.EqualTo(0));
        Assert.That(mix, Does.Contain("不混装"));
        Assert.That(E.CarriedGrams(s), Is.GreaterThanOrEqualTo(2400), "地面脏水装进随身壶，重量计入");

        s.DiarrheaUntil = null;
        Start(s, ActionRequest.UseInstance(c.InstanceId));
        Run(s);
        Assert.That(s.DiarrheaUntil, Is.Not.Null, "脏水 250 毫升也腹泻 12 小时");
        Assert.That(s.DiarrheaUntil!.Value - s.GameMinute, Is.EqualTo(720 - 5));
    }

    [Test]
    public void ES04_NotInFireOrPot_NotInBattle()
    {
        var (s, c) = Setup();
        E.DebugBuildAllWorkshops(s);
        Assert.That(E.MaxPutInFire(s, c, out var why), Is.EqualTo(0));
        Assert.That(why, Is.Not.Null);
        var pot = s.Items.FirstOrDefault(i => E.IsPot(i));
        if (pot != null)
        {
            Assert.That(E.MaxPutFood(s, c, pot, out var why2), Is.EqualTo(0));
            Assert.That(why2, Is.Not.Null);
        }
    }

    [Test]
    public void Save28_RoundTripAndMigration()
    {
        var (s, c) = Setup(clean: 2);
        Ok(E.DoInstant(s, InstantRequest.FillCanteen(First(s, "clean_water").InstanceId, c.InstanceId, 2)));
        var t = SaveSerializer.Deserialize(SaveSerializer.Serialize(s), Cfg);
        var c2 = t.Items.Single(i => i.ItemId == "ration_canteen");
        Assert.That((c2.LiquidId, c2.LiquidMl), Is.EqualTo(("clean_water", 1000)));

        // 27 → 28：没有水壶内容与锅食谱锁定的旧档原样保留，进程字段为空，日志一条
        var old = New();
        old.SchemaVersion = "27";
        var procs = old.Locations.Values.Sum(l => l.Facility.Processes.Count);
        var items = old.Items.Sum(i => i.Count);
        var m = SaveSerializer.Deserialize(SaveSerializer.Serialize(old), Cfg);
        Assert.That(m.SchemaVersion, Is.EqualTo(GameState.CurrentSchema));
        Assert.That(m.Items.Sum(i => i.Count), Is.EqualTo(items));
        Assert.That(m.Locations.Values.Sum(l => l.Facility.Processes.Count), Is.EqualTo(procs));
        Assert.That(m.Locations.Values.SelectMany(l => l.Facility.Processes).All(p => p.InputInstanceIds == null), Is.True);
        Assert.That(m.Log.Count(l => l.Text.Contains("版本28")), Is.EqualTo(1));
        var json = SaveSerializer.Serialize(m);
        SaveMigrator.Migrate(m, Cfg);
        Assert.That(SaveSerializer.Serialize(m), Is.EqualTo(json), "幂等");
    }
}
