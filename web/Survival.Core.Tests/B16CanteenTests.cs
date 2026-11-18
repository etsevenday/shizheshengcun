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
        var (s, c) = Setup(clean: 20);   // B56：每份100毫升，×5 保持原 2000 毫升
        var g0 = E.CarriedGrams(s);
        var water = First(s, "clean_water", ItemInstance.Carried);
        Ok(E.DoInstant(s, InstantRequest.FillCanteen(water.InstanceId, c.InstanceId, 15)));   // B56：装 15 份×100（原 3 份×500），同为 1500 毫升
        Assert.That((c.LiquidId, c.LiquidMl), Is.EqualTo(("clean_water", 1500)));
        // B56（S10 文首 B53a Q929；b53a-q928-q929-water-100ml.json containers.ration_canteen、drink、W100-Q929-04）：剩 5 份×100（原 1 份×500）
        Assert.That(Count(s, "clean_water"), Is.EqualTo(5));
        Assert.That(E.CarriedGrams(s), Is.EqualTo(g0), "随身水卡进随身水壶：总重不变");
        Assert.That(E.ItemGrams(s, c), Is.EqualTo(400 + 1500));

        // B56（S10 文首 B53a Q929；b53a-q928-q929-water-100ml.json containers.ration_canteen、drink、W100-Q929-04）：壶内饮用不再固定一口 250，选 2 份＝200 毫升、一次 5 分钟、W+12（原 −250／W+15）；不能凭空倒成水卡
        s.Stats.Hydration = 50;
        var t0 = s.GameMinute;
        Start(s, ActionRequest.UseInstance(c.InstanceId, 2));
        Run(s);
        Assert.That(s.GameMinute - t0, Is.EqualTo(5));
        Assert.That(c.LiquidMl, Is.EqualTo(1300));
        Assert.That(s.Stats.Hydration, Is.GreaterThan(61).And.LessThan(62.1), "W+12，再扣 5 分钟等待消耗");

        // B56：倒出 12 份×100 到地面（原 2 份×500），剩 100 仍够倒一份
        Ok(E.DoInstant(s, InstantRequest.PourCanteen(c.InstanceId, s.CurrentLocation, 12)));
        Assert.That(Count(s, "clean_water", s.CurrentLocation), Is.EqualTo(12));
        Assert.That(c.LiquidMl, Is.EqualTo(100));
        Assert.That(E.MaxPourCanteen(s, c, s.CurrentLocation, out var why), Is.EqualTo(1));
        Assert.That(why, Is.Null);
        Start(s, ActionRequest.UseInstance(c.InstanceId));
        Run(s);
        Assert.That((c.LiquidId, c.LiquidMl), Is.EqualTo(((string?)null, 0)));
        Assert.That(E.CanStart(s, ActionRequest.UseInstance(c.InstanceId)), Does.Contain("空"));

        // B56（Q929；b53a json copy.shortage）：旧档壶余 250 毫升（水壶按毫升不迁移）喝 2 份后剩 50，不足一份——倒出与饮用都「水分不足。」（原「不够倒成一份」）
        c.LiquidId = "clean_water";
        c.LiquidMl = 250;
        Start(s, ActionRequest.UseInstance(c.InstanceId, 2));
        Run(s);
        Assert.That(c.LiquidMl, Is.EqualTo(50));
        Assert.That(E.MaxPourCanteen(s, c, s.CurrentLocation, out var tail), Is.EqualTo(0));
        Assert.That(tail, Is.EqualTo("水分不足。"));
        Assert.That(E.CanStart(s, ActionRequest.UseInstance(c.InstanceId)), Is.EqualTo("水分不足。"));
    }

    [Test]
    public void ES04_NoMixing_Capacity_DirtySipDiarrhea()
    {
        var (s, c) = Setup(clean: 5, dirty: 25);   // B56：每份100毫升，×5 保持原 500／2500 毫升
        var dirty = First(s, "dirty_water", s.CurrentLocation);
        Ok(E.DoInstant(s, InstantRequest.FillCanteen(dirty.InstanceId, c.InstanceId, 20)));   // B56：20 份×100（原 4 份×500）装满 2000
        Assert.That(c.LiquidMl, Is.EqualTo(2000));
        Assert.That(E.MaxFillCanteen(s, First(s, "dirty_water"), c, out var full), Is.EqualTo(0));
        Assert.That(full, Does.Contain("已满"));
        Assert.That(E.MaxFillCanteen(s, First(s, "clean_water"), c, out var mix), Is.EqualTo(0));
        Assert.That(mix, Does.Contain("不混装"));
        Assert.That(E.CarriedGrams(s), Is.GreaterThanOrEqualTo(2400), "地面脏水装进随身壶，重量计入");

        s.DiarrheaUntil = null;
        Start(s, ActionRequest.UseInstance(c.InstanceId));
        Run(s);
        Assert.That(s.DiarrheaUntil, Is.Not.Null, "B56：脏水默认 1 份 100 毫升也腹泻 12 小时（原一口 250）");
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
        var (s, c) = Setup(clean: 10);   // B56：每份100毫升，×5 保持原 1000 毫升
        Ok(E.DoInstant(s, InstantRequest.FillCanteen(First(s, "clean_water").InstanceId, c.InstanceId, 10)));
        var t = SaveSerializer.Deserialize(SaveSerializer.Serialize(s), Cfg);
        var c2 = t.Items.Single(i => i.ItemId == "ration_canteen");
        Assert.That((c2.LiquidId, c2.LiquidMl), Is.EqualTo(("clean_water", 1000)));

        // 27 → 28：没有水壶内容与锅食谱锁定的旧档原样保留，进程字段为空，日志一条
        var old = New();
        old.SchemaVersion = "27";
        var procs = old.Locations.Values.Sum(l => l.Facility.Processes.Count);
        var items = old.Items.Where(i => i.ItemId is not ("clean_water" or "dirty_water")).Sum(i => i.Count);
        var oldWater = old.Items.Where(i => i.ItemId is "clean_water" or "dirty_water").Sum(i => i.Count);
        Assert.That(oldWater, Is.EqualTo(10), "新局营地地面净水 10（B56 配置 startingCampGround）");
        var m = SaveSerializer.Deserialize(SaveSerializer.Serialize(old), Cfg);
        Assert.That(m.SchemaVersion, Is.EqualTo(GameState.CurrentSchema));
        // B56（M1083 存档 57→58：水物品份数×5，真实毫升不变）：旧档迁移链末尾会把水份数×5；非水物品不补不减（原断言全部物品总数不变）
        Assert.That(m.Items.Where(i => i.ItemId is not ("clean_water" or "dirty_water")).Sum(i => i.Count), Is.EqualTo(items));
        Assert.That(m.Items.Where(i => i.ItemId is "clean_water" or "dirty_water").Sum(i => i.Count), Is.EqualTo(50));
        Assert.That(m.Locations.Values.Sum(l => l.Facility.Processes.Count), Is.EqualTo(procs));
        Assert.That(m.Locations.Values.SelectMany(l => l.Facility.Processes).All(p => p.InputInstanceIds == null), Is.True);
        Assert.That(m.Log.Count(l => l.Text.Contains("版本28")), Is.EqualTo(1));
        var json = SaveSerializer.Serialize(m);
        SaveMigrator.Migrate(m, Cfg);
        Assert.That(SaveSerializer.Serialize(m), Is.EqualTo(json), "幂等");
    }
}
