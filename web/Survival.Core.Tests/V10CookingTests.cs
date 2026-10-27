using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 系统策划总册 v1.0 Q2/T03 与 P02：烤肉可存锅并在合格热源与净水下复煮为水煮肉（15 有效分钟、500ml、继承原到期）；
/// 水煮肉放回锅只存放；金属锅没有耐久（S10 §6、S11 §7–§8、R06 §5、R02 §6）。
/// </summary>
public class V10CookingTests : TestBase
{
    private (GameState s, ItemInstance pot) PotOnFire(int cleanWater)
    {
        var s = New();
        var pot = First(s, "pot");
        if (cleanWater > 0)
        {
            E.DebugGive(s, "clean_water", cleanWater, "camp");
            Assert.That(E.DoInstant(s, InstantRequest.FillPot(First(s, "clean_water", "camp").InstanceId, pot.InstanceId, cleanWater)).Ok, Is.True);
        }
        Assert.That(E.DoInstant(s, InstantRequest.PutInFire(pot.InstanceId)).Ok, Is.True);
        var fac = s.Locations["camp"].Facility;
        fac.FireLit = true;
        fac.FuelMinutes = 180;
        return (s, pot);
    }

    [Test]
    public void Reboil_CookedMeat_15Minutes_500ml_KeepsExpiry()
    {
        var (s, pot) = PotOnFire(2);
        var meat = E.CreateItems(s, "cooked_meat", 1, "camp").Single();
        var expires = s.GameMinute + 180;
        meat.ExpiresAt = expires;
        Assert.That(E.DoInstant(s, InstantRequest.PutFoodInPot(meat.InstanceId, pot.InstanceId, 1)).Ok, Is.True, "烤肉可放进锅");
        var inPot = E.PotFood(s, pot.InstanceId).Single();
        Assert.That(E.ContentStatus(s, inPot), Does.Contain("复煮"));
        var start = s.GameMinute;
        Start(s, ActionRequest.Wait(30));
        Run(s);
        var food = E.PotFood(s, pot.InstanceId).Single();
        Assert.That(food.ItemId, Is.EqualTo("boiled_meat"), "复煮后为水煮肉，留在原锅");
        Assert.That(food.ExpiresAt, Is.EqualTo(expires), "继承原烤肉的绝对到期时刻，不刷新24小时");
        Assert.That(E.PotUnits(s, pot.InstanceId), Is.EqualTo(1), "扣净水500ml（1份）");
        var log = s.Log.Last(l => l.Text.Contains("已煮成水煮肉")).Minute;
        Assert.That(log - start, Is.EqualTo(15), "15有效分钟");
    }

    [Test]
    public void Reboil_SpoilsFirst_WhenCompletingExactlyAtExpiry()
    {
        var (s, pot) = PotOnFire(2);
        var meat = E.CreateItems(s, "cooked_meat", 1, "camp").Single();
        meat.ExpiresAt = s.GameMinute + 15;   // 恰好在完成那一刻腐败：先腐败、不产成品
        E.DoInstant(s, InstantRequest.PutFoodInPot(meat.InstanceId, pot.InstanceId, 1));
        Start(s, ActionRequest.Wait(30));
        Run(s);
        var food = E.PotFood(s, pot.InstanceId).Single();
        Assert.That(food.ItemId, Is.EqualTo("cooked_meat"));
        Assert.That(E.IsSpoiled(s, food), Is.True);
        Assert.That(E.PotUnits(s, pot.InstanceId), Is.EqualTo(2), "不扣水");
    }

    [Test]
    public void Reboil_NeedsHeatAndCleanWater()
    {
        // 没水：只存放
        var (s, pot) = PotOnFire(0);
        var meat = E.CreateItems(s, "cooked_meat", 1, "camp").Single();
        Assert.That(E.DoInstant(s, InstantRequest.PutFoodInPot(meat.InstanceId, pot.InstanceId, 1)).Ok, Is.True, "空锅也可存放烤肉");
        Start(s, ActionRequest.Wait(30));
        Run(s);
        Assert.That(E.PotFood(s, pot.InstanceId).Single().ItemId, Is.EqualTo("cooked_meat"));
        Assert.That(E.ContentStatus(s, E.PotFood(s, pot.InstanceId).Single()), Does.Contain("缺水"));

        // 锅不在火堆：随身锅里有净水也不加工
        var t = New();
        var p2 = First(t, "pot");
        E.DebugGive(t, "clean_water", 1, "camp");
        E.DoInstant(t, InstantRequest.FillPot(First(t, "clean_water", "camp").InstanceId, p2.InstanceId, 1));
        var m2 = E.CreateItems(t, "cooked_meat", 1, "camp").Single();
        E.DoInstant(t, InstantRequest.PutFoodInPot(m2.InstanceId, p2.InstanceId, 1));
        Start(t, ActionRequest.Wait(30));
        Run(t);
        Assert.That(E.PotFood(t, p2.InstanceId).Single().ItemId, Is.EqualTo("cooked_meat"));
        Assert.That(E.PotUnits(t, p2.InstanceId), Is.EqualTo(1));
    }

    [Test]
    public void BoiledMeat_StoreOnly_NoReprocess()
    {
        var (s, pot) = PotOnFire(2);
        var bm = E.CreateItems(s, "boiled_meat", 1, "camp").Single();
        var exp = bm.ExpiresAt;
        Assert.That(E.DoInstant(s, InstantRequest.PutFoodInPot(bm.InstanceId, pot.InstanceId, 1)).Ok, Is.True, "水煮肉可从外部放回锅");
        var inPot = E.PotFood(s, pot.InstanceId).Single();
        Assert.That(E.ContentStatus(s, inPot), Does.Contain("只存放"));
        Start(s, ActionRequest.Wait(60));
        Run(s);
        inPot = E.PotFood(s, pot.InstanceId).Single();
        Assert.That(inPot.ItemId, Is.EqualTo("boiled_meat"));
        Assert.That(inPot.ExpiresAt, Is.EqualTo(exp), "不刷新保质");
        Assert.That(E.PotUnits(s, pot.InstanceId), Is.EqualTo(2), "不耗水");
        Assert.That(E.ProcessOf(s, inPot), Is.Null, "没有水煮肉→水煮肉配方");

        // 外部水煮块根本次不扩展：仍不能放入
        var br = E.CreateItems(s, "boiled_root", 1, "camp").Single();
        Assert.That(E.MaxPutFood(s, br, pot, out var why), Is.EqualTo(0));
        Assert.That(why, Is.Not.Null);
        // 活性物质不能入锅/火堆（R07 §2）
        var sub = E.CreateItems(s, "active_substance_white_unidentified", 1, "camp").Single();
        Assert.That(E.MaxPutFood(s, sub, pot, out _), Is.EqualTo(0));
        Assert.That(E.MaxPutInFire(s, sub, out var fireWhy), Is.EqualTo(0));
        Assert.That(fireWhy, Is.Not.Null);
    }

    [Test]
    public void Pot_HasNoDurability_ManyProcessesNoWear()
    {
        var (s, pot) = PotOnFire(0);
        Assert.That(pot.Durability, Is.Null);
        Assert.That(pot.MaxDurability, Is.Null);
        // 连续多次净水与水煮，锅始终没有耐久、不拒绝受热
        for (var k = 0; k < 4; k++)
        {
            E.DebugGive(s, "dirty_water", 2, "camp");
            Assert.That(E.DoInstant(s, InstantRequest.TakeOut(pot.InstanceId, "camp")).Ok, Is.True);
            foreach (var w in E.PotLiquids(s, pot.InstanceId).ToList())
                E.DoInstant(s, InstantRequest.Decant(w.InstanceId, "camp", w.Count));
            E.DoInstant(s, InstantRequest.FillPot(First(s, "dirty_water", "camp").InstanceId, pot.InstanceId, 2));
            E.DoInstant(s, InstantRequest.PutInFire(pot.InstanceId));
            s.Locations["camp"].Facility.FuelMinutes = 180;
            s.Locations["camp"].Facility.FireLit = true;
            Start(s, ActionRequest.Wait(30));
            Run(s);
            Assert.That(E.PotLiquid(s, pot.InstanceId), Is.EqualTo("clean_water"), $"第{k + 1}次净水");
        }
        Assert.That(pot.Durability, Is.Null);
        Assert.That(s.Log.Any(l => l.Text.Contains("锅耐久")), Is.False);
    }
}
