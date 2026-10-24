using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>卡片统一与设施烹饪 v0.4 · 02 案第 9 节 P401–P421：设施内容物、自动烤制与水煮。</summary>
public class FacilityProcessTests : TestBase
{
    private const string Fire = "facility:camp:fire";

    private ItemInstance Pot(GameState s) => s.Items.First(i => i.ItemId == "pot");
    private static string PotOwner(ItemInstance pot) => "pot:" + pot.InstanceId;

    private void Ok(GameState s, InstantRequest r)
    {
        var res = E.DoInstant(s, r);
        Assert.That(res.Ok, Is.True, res.Reason);
    }

    private void LightFire(GameState s, int fuel = 120)
    {
        var f = s.Locations["camp"].Facility;
        f.FireLit = true;
        f.FuelMinutes = fuel;
    }

    /// <summary>把 n 份指定的水装进锅（水先放在营地地面）。</summary>
    private void FillWater(GameState s, ItemInstance pot, string water, int n)
    {
        foreach (var w in s.Items.Where(i => i.ItemId == water && i.Owner == "camp").ToList()) s.Items.Remove(w);
        E.DebugGive(s, water, n, "camp");
        Ok(s, InstantRequest.FillPot(First(s, water, "camp").InstanceId, pot.InstanceId, n));
    }

    private List<ItemInstance> GiveRoots(GameState s, int n, string owner = "camp")
    {
        E.DebugGive(s, "raw_root", n, owner);
        return s.Items.Where(i => i.ItemId == "raw_root" && i.Owner == owner).ToList();
    }

    private void PutRootsInPot(GameState s, ItemInstance pot, int n)
    {
        var roots = GiveRoots(s, n);
        Ok(s, InstantRequest.PutFoodInPot(roots[0].InstanceId, pot.InstanceId, n));
    }

    [Test]
    public void P401_FullPotIntoFire_OnlyFacilityOwns_Water100()
    {
        var s = New();
        var pot = Pot(s);
        FillWater(s, pot, "clean_water", 2);
        Ok(s, InstantRequest.Drop(pot.InstanceId, 1));
        Ok(s, InstantRequest.PutInFire(pot.InstanceId));
        Assert.That(pot.Owner, Is.EqualTo(Fire));
        Assert.That(s.Items.Count(i => i.InstanceId == pot.InstanceId), Is.EqualTo(1), "同一实例，不复制");
        Assert.That(E.NaturalCards(s, "camp").Any(k => k.EndsWith("|" + pot.InstanceId)), Is.False, "原区域不显示");
        Assert.That(E.FirePot(s, "camp"), Is.SameAs(pot));
        Assert.That(E.PotLabel(s, pot), Is.EqualTo("净水100%·1000/1000毫升"), "水仍100%");
        Assert.That(E.PlaceOf(s, E.PotLiquids(s, pot.InstanceId).Single()), Is.EqualTo("camp"), "锅内水的根位置是营地");
        var t = SaveSerializer.Deserialize(SaveSerializer.Serialize(s));
        Assert.That(t.Items.Single(i => i.InstanceId == pot.InstanceId).Owner, Is.EqualTo(Fire), "读档后仍只属设施");
    }

    [Test]
    public void P402_SlotCapacity_PotPlusRoot_TwoRoots_ThirdRejected_StackCannotBypass()
    {
        var s = New();
        var pot = Pot(s);
        var roots = GiveRoots(s, 5);
        Ok(s, InstantRequest.PutInFire(pot.InstanceId));
        Ok(s, InstantRequest.PutInFire(roots[0].InstanceId));
        Assert.That(E.FireContents(s, "camp"), Has.Count.EqualTo(2), "1锅加1根");
        Assert.That(Count(s, "raw_root", "camp"), Is.EqualTo(4));
        var third = E.DoInstant(s, InstantRequest.PutInFire(First(s, "raw_root", "camp").InstanceId));
        Assert.That(third.Ok, Is.False);
        Assert.That(third.Reason, Does.StartWith("火堆加工位已满"));

        var t = New();
        var r2 = GiveRoots(t, 5);
        var big = E.DoInstant(t, InstantRequest.PutInFire(r2[0].InstanceId, 3));
        Assert.That(big.Ok, Is.False, "一堆3根不能当一个加工位");
        Assert.That(big.MaxCount, Is.EqualTo(2));
        Ok(t, InstantRequest.PutInFire(r2[0].InstanceId, 2));
        var inFire = t.Items.Where(i => i.Owner == Fire).ToList();
        Assert.That(inFire, Has.Count.EqualTo(2), "2根直烤各占一位");
        Assert.That(inFire.All(i => i.Count == 1), Is.True, "每份单独一个实例");
        Assert.That(E.DoInstant(t, InstantRequest.PutInFire(Pot(t).InstanceId)).Reason, Does.StartWith("火堆加工位已满"), "满位后锅也放不进");
        // 设施内的两份不会合堆成一份
        var u = SaveSerializer.Deserialize(SaveSerializer.Serialize(t));
        Assert.That(u.Items.Count(i => i.Owner == Fire), Is.EqualTo(2));
    }

    [Test]
    public void P403_FullPotWithTwoRoots_Into27_5kgBackpack_RejectedWhole()
    {
        var s = New();
        LightFire(s);
        var pot = Pot(s);
        FillWater(s, pot, "clean_water", 2);
        PutRootsInPot(s, pot, 2);
        Ok(s, InstantRequest.PutInFire(pot.InstanceId));
        Assert.That(E.ItemGrams(s, pot), Is.EqualTo(2600), "锅1000+水1000+根600，后代只计一次");
        // 随身凑到 27.5 公斤
        E.DebugGive(s, "stone", 54);
        E.DebugGive(s, "fiber", 4);
        Assert.That(E.CarriedGrams(s), Is.EqualTo(27500));
        var heat = s.Locations["camp"].Facility.Processes.Select(p => (p.ProcessId, p.HeatDoneMinutes, p.ReservedWaterMl)).ToList();
        var r = E.DoInstant(s, InstantRequest.TakeOut(pot.InstanceId, "carried"));
        Assert.That(r.Ok, Is.False);
        Assert.That(r.Reason, Does.Contain("30公斤"));
        Assert.That(r.Reason, Does.Contain("2.6公斤"));
        Assert.That(pot.Owner, Is.EqualTo(Fire), "owner 不变");
        Assert.That(s.Locations["camp"].Facility.Processes.Select(p => (p.ProcessId, p.HeatDoneMinutes, p.ReservedWaterMl)), Is.EqualTo(heat), "进度与预留不变");
        Assert.That(E.CarriedGrams(s), Is.EqualTo(27500));
    }

    [Test]
    public void P404_RootInEmptyPotOnLitFire_StoredButNotProcessed()
    {
        var s = New();
        LightFire(s);
        var pot = Pot(s);
        Ok(s, InstantRequest.PutInFire(pot.InstanceId));
        PutRootsInPot(s, pot, 1);
        var root = E.PotFood(s, pot.InstanceId).Single();
        Assert.That(E.ContentStatus(s, root), Is.EqualTo("缺水，未加工"));
        Start(s, ActionRequest.Wait(60));
        Run(s);
        Assert.That(root.ItemId, Is.EqualTo("raw_root"), "空锅不干煎、不烤制");
        Assert.That(Count(s, "boiled_root") + Count(s, "cooked_root"), Is.EqualTo(0), "无成品");
        Assert.That(E.ContentStatus(s, root), Is.EqualTo("缺水，未加工"));
    }

    [Test]
    public void P405_WaterAndRootDraggedToPotInFire_SameAsMenuPath()
    {
        var a = New(5);
        var potA = Pot(a);
        Ok(a, InstantRequest.PutInFire(potA.InstanceId));
        var b = a.Clone();
        var potB = Pot(b);

        // 拖拽路径：水卡落在火堆内锅的水量区
        var fill = E.ResolveDrop(a, DropSource.Parse("card:camp|clean_water")!, DropTarget.Parse($"potfill:{potA.InstanceId}")!);
        Assert.That(fill.Ok && fill.Verb == DropVerb.FillPot, Is.True, fill.Reason);
        Ok(a, fill.Instant!);
        // 菜单路径：同一锅
        Ok(b, InstantRequest.FillPot(First(b, "clean_water", "camp").InstanceId, potB.InstanceId));
        Assert.That(SaveSerializer.Serialize(a), Is.EqualTo(SaveSerializer.Serialize(b)), "两条路径结果一致");
        Assert.That(Count(a, "clean_water", PotOwner(potA)), Is.EqualTo(1));
        Assert.That(Count(a, "clean_water", "camp"), Is.EqualTo(1), "地面少1份，无地面副本");

        GiveRoots(a, 1);
        GiveRoots(b, 1);
        Ok(a, InstantRequest.PutFoodInPot(First(a, "raw_root", "camp").InstanceId, potA.InstanceId));
        Ok(b, InstantRequest.PutFoodInPot(First(b, "raw_root", "camp").InstanceId, potB.InstanceId));
        Assert.That(SaveSerializer.Serialize(a), Is.EqualTo(SaveSerializer.Serialize(b)));
        Assert.That(E.PotFood(a, potA.InstanceId), Has.Count.EqualTo(1));
        Assert.That(Count(a, "raw_root", "camp"), Is.EqualTo(0));
        Assert.That(a.Items.Count(i => i.ItemId == "raw_root"), Is.EqualTo(1), "只有锅里那一份");
    }

    [Test]
    public void P418_CookedFoodToHeat_NoRecipe_NoExpiryRefresh()
    {
        var s = New();
        LightFire(s);
        E.DebugGive(s, "cooked_root", 1, "camp");
        E.DebugGive(s, "boiled_root", 1, "camp");
        var cooked = First(s, "cooked_root");
        var expiry = cooked.ExpiresAt;
        var r = E.DoInstant(s, InstantRequest.PutInFire(cooked.InstanceId));
        Assert.That(r.Ok, Is.False);
        Assert.That(r.Reason, Is.EqualTo("烤熟块根没有可用的处理配方，放进火堆也不会再加工"));
        Assert.That(cooked.ExpiresAt, Is.EqualTo(expiry));
        Assert.That(cooked.Owner, Is.EqualTo("camp"));
        var pot = Pot(s);
        var b = E.DoInstant(s, InstantRequest.PutFoodInPot(First(s, "boiled_root").InstanceId, pot.InstanceId));
        Assert.That(b.Reason, Does.StartWith("水煮块根没有可用的处理配方"));
        foreach (var id in new[] { "berry", "herb", "canned_food" })
        {
            E.DebugGive(s, id, 1, "camp");
            Assert.That(E.DoInstant(s, InstantRequest.PutInFire(First(s, id, "camp").InstanceId)).Ok, Is.False, id);
            Assert.That(E.DoInstant(s, InstantRequest.PutFoodInPot(First(s, id, "camp").InstanceId, pot.InstanceId)).Ok, Is.False, id);
        }
        Assert.That(E.DoInstant(s, InstantRequest.PutInFire(First(s, "clean_water", "camp").InstanceId)).Reason, Does.Contain("请拖进锅里"), "水直接丢火堆提示拖入锅");
        Assert.That(s.Locations["camp"].Facility.FireLit, Is.True, "也不灭火");
    }

    [Test]
    public void Busy_And_Remote_Rejected()
    {
        var s = New();
        var pot = Pot(s);
        Start(s, ActionRequest.Wait(30));
        Assert.That(E.DoInstant(s, InstantRequest.PutInFire(pot.InstanceId)).Reason, Is.EqualTo("行动进行中，不能整理物品"));
        E.Cancel(s);
        Ok(s, InstantRequest.PutInFire(pot.InstanceId));
        Start(s, ActionRequest.Travel("field"));
        Run(s);
        Assert.That(E.DoInstant(s, InstantRequest.TakeOut(pot.InstanceId, "carried")).Reason, Is.EqualTo("异地不能取用设施里的物品"));
        GiveRoots(s, 1, "field");
        Assert.That(E.DoInstant(s, InstantRequest.PutFoodInPot(First(s, "raw_root", "field").InstanceId, pot.InstanceId)).Ok, Is.False, "不能远程投放");
    }
}
