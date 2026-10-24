using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>卡片拖拽与容器案 v0.3 第 10 节 D03–D16 的内核部分与金属锅规则；v0.4 起烧水、烤制、水煮改为设施自动加工，见 FacilityProcessTests。</summary>
public class ContainerTests : TestBase
{
    private ItemInstance Pot(GameState s) => s.Items.First(i => i.ItemId == "pot");
    private static string PotOwner(ItemInstance pot) => "pot:" + pot.InstanceId;

    private void LightFire(GameState s, int fuel = 120)
    {
        var f = s.Locations["camp"].Facility;
        f.FireLit = true;
        f.FuelMinutes = fuel;
    }

    /// <summary>v0.3 取消自动补水后，烧水前要先把脏水装进锅。</summary>
    private void FillDirty(GameState s, ItemInstance pot, int n)
    {
        E.DebugGive(s, "dirty_water", n, "camp");
        Ok(s, InstantRequest.FillPot(First(s, "dirty_water", "camp").InstanceId, pot.InstanceId, n));
    }

    private void Ok(GameState s, InstantRequest r)
    {
        var res = E.DoInstant(s, r);
        Assert.That(res.Ok, Is.True, res.Reason);
    }

    [Test]
    public void D03_GroundWoodInto29_8kgBackpack_RejectedWhole()
    {
        var s = New();
        E.DebugGive(s, "stone", 57);
        E.DebugGive(s, "fiber", 2);
        Assert.That(E.CarriedGrams(s), Is.EqualTo(29800));
        E.DebugGive(s, "wood", 1, "camp");
        var wood = s.Items.First(i => i.ItemId == "wood" && i.Owner == "camp");
        var r = E.DoInstant(s, InstantRequest.Pickup(wood.InstanceId, 1));
        Assert.That(r.Ok, Is.False);
        Assert.That(E.CarriedGrams(s), Is.EqualTo(29800));
        Assert.That(Count(s, "wood", "camp"), Is.EqualTo(3), "开局2加放入的1都还在地面");
    }

    [Test]
    public void D04_Fuel180PlusOneIs240_190Rejected()
    {
        var s = New();
        var f = s.Locations["camp"].Facility;
        f.FuelMinutes = 180;
        Ok(s, InstantRequest.AddFuel());
        Assert.That(f.FuelMinutes, Is.EqualTo(240));
        f.FuelMinutes = 190;
        var wood = Count(s, "wood");
        var r = E.DoInstant(s, InstantRequest.AddFuel());
        Assert.That(r.Ok, Is.False);
        Assert.That(r.Reason, Does.Contain("至少需要60分钟空余容量"));
        Assert.That(Count(s, "wood"), Is.EqualTo(wood));
        Assert.That(E.MaxWoodForFire(f), Is.EqualTo(0));
        f.FuelMinutes = 0;
        Assert.That(E.MaxWoodForFire(f), Is.EqualTo(4));
        Assert.That(E.DoInstant(s, InstantRequest.AddFuel(5)).Ok, Is.False, "超过空位整次拒绝");
        Assert.That(Count(s, "wood"), Is.EqualTo(wood));
    }

    [Test]
    public void D06_DirtyWaterIntoEmptyPotTwice_50Then100_TwoKg()
    {
        var s = New();
        E.DebugGive(s, "dirty_water", 2, "camp");
        var pot = Pot(s);
        var water = First(s, "dirty_water", "camp");
        Ok(s, InstantRequest.FillPot(water.InstanceId, pot.InstanceId));
        Assert.That(E.PotUnits(s, pot.InstanceId), Is.EqualTo(1));
        Assert.That(E.PotLabel(s, pot), Is.EqualTo("脏水50%·500/1000毫升"));
        Ok(s, InstantRequest.FillPot(First(s, "dirty_water", "camp").InstanceId, pot.InstanceId));
        Assert.That(E.PotLabel(s, pot), Is.EqualTo("脏水100%·1000/1000毫升"));
        Assert.That(Count(s, "dirty_water", "camp"), Is.EqualTo(0), "散卡扣2");
        Assert.That(E.ItemGrams(s, pot), Is.EqualTo(2000), "满锅总重2公斤");
        Assert.That(E.CarriedGrams(s), Is.EqualTo(2100), "锅2公斤加打火器0.1公斤");
        E.DebugGive(s, "dirty_water", 1, "camp");
        var r = E.DoInstant(s, InstantRequest.FillPot(First(s, "dirty_water", "camp").InstanceId, pot.InstanceId));
        Assert.That(r.Reason, Does.StartWith("满锅"));
    }

    [Test]
    public void D07_CleanWaterIntoHalfDirtyPot_MixForbidden()
    {
        var s = New();
        E.DebugGive(s, "dirty_water", 1, "camp");
        var pot = Pot(s);
        Ok(s, InstantRequest.FillPot(First(s, "dirty_water", "camp").InstanceId, pot.InstanceId));
        var clean = First(s, "clean_water", "camp");
        var r = E.DoInstant(s, InstantRequest.FillPot(clean.InstanceId, pot.InstanceId));
        Assert.That(r.Ok, Is.False);
        Assert.That(r.Reason, Does.Contain("混装禁止"));
        Assert.That(Count(s, "clean_water", "camp"), Is.EqualTo(2));
        Assert.That(E.PotUnits(s, pot.InstanceId), Is.EqualTo(1));
    }

    [Test]
    public void D08_CarriedWaterIntoCarriedPot_WeightUnchanged()
    {
        var s = New();
        E.DebugGive(s, "clean_water", 2);
        var before = E.CarriedGrams(s);
        Ok(s, InstantRequest.FillPot(First(s, "clean_water", "carried").InstanceId, Pot(s).InstanceId, 2));
        Assert.That(E.CarriedGrams(s), Is.EqualTo(before), "不双算内容");
        Assert.That(Count(s, "clean_water", "carried"), Is.EqualTo(0));
        Assert.That(Count(s, "clean_water", PotOwner(Pot(s))), Is.EqualTo(2));
    }

    [Test]
    public void D08b_GroundWaterIntoCarriedPot_AddsWeight_DecantBack()
    {
        var s = New();
        var before = E.CarriedGrams(s);
        var pot = Pot(s);
        Ok(s, InstantRequest.FillPot(First(s, "clean_water", "camp").InstanceId, pot.InstanceId));
        Assert.That(E.CarriedGrams(s), Is.EqualTo(before + 500));
        var child = E.PotContents(s, pot.InstanceId).Single();
        Ok(s, InstantRequest.Decant(child.InstanceId, "camp"));
        Assert.That(E.PotUnits(s, pot.InstanceId), Is.EqualTo(0));
        Assert.That(Count(s, "clean_water", "camp"), Is.EqualTo(2));
        Assert.That(s.Items.Count(i => i.ItemId == "clean_water" && i.Owner == "camp"), Is.EqualTo(1), "倒出后与地面同类合堆");
    }

    [Test]
    public void Ignite_RequiresChosenLighter_GroundAllowed()
    {
        var s = New();
        var lighter = First(s, "lighter");
        Ok(s, InstantRequest.Drop(lighter.InstanceId, 1));
        Start(s, ActionRequest.Ignite(lighter.InstanceId));
        Assert.That(s.Sub ?? s.Main, Is.Not.Null);
        Assert.That((s.Sub ?? s.Main)!.LockedItemIds, Does.Contain(lighter.InstanceId));
        Run(s);
        Assert.That(s.Locations["camp"].Facility.FireLit, Is.True);
        var t = New();
        Ok(t, InstantRequest.Drop(First(t, "lighter").InstanceId, 1));
        // 策划答复 2026-10-24 第 1 条：按钮点火也必须选定打火器，不自动挑
        Assert.That(E.CanStart(t, ActionRequest.Ignite()), Is.EqualTo("请选择要使用的打火器"));
        Assert.That(E.CanStart(t, ActionRequest.Ignite(First(t, "lighter").InstanceId)), Is.Null, "选定的地面打火器可用");
        foreach (var l in t.Items.Where(i => i.ItemId == "lighter").ToList()) t.Items.Remove(l);
        Assert.That(E.CanStart(t, ActionRequest.Ignite()), Is.EqualTo("缺少打火器（需随身或在当前地面）"));
    }

    [Test]
    public void PutInFire_CarriedPotOwnedByFacility_OnePotOnly()
    {
        var s = New();
        var pot = Pot(s);
        var before = E.CarriedGrams(s);
        Ok(s, InstantRequest.PutInFire(pot.InstanceId));
        Assert.That(pot.Owner, Is.EqualTo("facility:camp:fire"), "随身锅直接归属火堆，不经过地面");
        Assert.That(s.Locations["camp"].Facility.ProcessingSlots, Does.Contain((int?)pot.InstanceId));
        Assert.That(E.CarriedGrams(s), Is.EqualTo(before - 1000), "火堆内物资不计角色携重");
        Assert.That(E.OrderedCards(s, "camp").Any(k => k.Contains("|pot|")), Is.False, "地面栏没有这口锅");
        Assert.That(E.OrderedCards(s, "carried").Any(k => k.Contains("|pot|")), Is.False, "随身栏也没有");
        E.DebugGive(s, "pot", 1, "camp");
        var other = s.Items.Last(i => i.ItemId == "pot");
        var r = E.DoInstant(s, InstantRequest.PutInFire(other.InstanceId));
        Assert.That(r.Reason, Does.Contain($"火堆已有金属锅#{pot.InstanceId}"), "已有锅时提示当前锅实例与内容，不替换");
        Assert.That(r.Reason, Does.Contain("请先取下"));
        Ok(s, InstantRequest.TakeOut(pot.InstanceId, "camp"));
        Assert.That(pot.Owner, Is.EqualTo("camp"), "取下到当前地面");
        Assert.That(s.Locations["camp"].Facility.ProcessingSlots.All(x => x == null), Is.True);
    }

    [Test]
    public void D15_FoodMergeAcrossZones_ExpiryKept_ReservedNotReused()
    {
        var s = New();
        E.DebugGive(s, "berry", 2, "camp");
        var expiry = First(s, "berry").ExpiresAt;
        E.DebugGive(s, "berry", 1);
        var carriedBerry = First(s, "berry", "carried");
        Assert.That(carriedBerry.ExpiresAt, Is.EqualTo(expiry), "同一时刻生成，到期相同");
        Ok(s, InstantRequest.Pickup(First(s, "berry", "camp").InstanceId, 2));
        var t = SaveSerializer.Deserialize(SaveSerializer.Serialize(s));
        var stacks = t.Items.Where(i => i.ItemId == "berry").ToList();
        Assert.That(stacks, Has.Count.EqualTo(1), "合堆");
        Assert.That(stacks[0].Count, Is.EqualTo(3));
        Assert.That(stacks[0].ExpiresAt, Is.EqualTo(expiry), "转移和合堆不刷新寿命");

        E.DebugGive(t, "herb", 1, "camp");
        Start(t, ActionRequest.Craft("bandage"));
        Assert.That(E.AvailableCount(t, "herb", true), Is.EqualTo(0), "预留批次不重复可用");
        var u = SaveSerializer.Deserialize(SaveSerializer.Serialize(t));
        Assert.That(E.AvailableCount(u, "herb", true), Is.EqualTo(0));
    }

    [Test]
    public void D16_FullPotInFireTakenToCarried_Weighs2Kg()
    {
        var s = New();
        E.DebugGive(s, "dirty_water", 2, "camp");
        var pot = Pot(s);
        Ok(s, InstantRequest.FillPot(First(s, "dirty_water", "camp").InstanceId, pot.InstanceId, 2));
        Ok(s, InstantRequest.PutInFire(pot.InstanceId));
        var before = E.CarriedGrams(s);
        Assert.That(E.DoInstant(s, InstantRequest.Pickup(pot.InstanceId, 1)).Ok, Is.False, "火堆里的锅不是地面物品，要用取出");
        Ok(s, InstantRequest.TakeOut(pot.InstanceId, "carried"));
        Assert.That(pot.Owner, Is.EqualTo("carried"));
        Assert.That(E.CarriedGrams(s) - before, Is.EqualTo(2000), "满锅按2公斤计");
        Start(s, ActionRequest.Travel("field"));
        Run(s);
        Assert.That(E.DoInstant(s, InstantRequest.PutInFire(pot.InstanceId)).Reason, Is.EqualTo("这里没有火堆"));
        Assert.That(E.DoInstant(s, InstantRequest.AddFuel()).Ok, Is.False, "异地不可操纵原火堆");
        Assert.That(Count(s, "dirty_water", PotOwner(pot)), Is.EqualTo(2), "水随锅走");
    }

    [Test]
    public void PotWater_DrinkableAndCountsForGuide()
    {
        var s = New();
        var pot = Pot(s);
        foreach (var w in s.Items.Where(i => i.ItemId == "clean_water").ToList())
            Ok(s, InstantRequest.FillPot(w.InstanceId, pot.InstanceId, w.Count));
        Assert.That(E.AvailableCount(s, "clean_water", true), Is.EqualTo(0), "配方与投料不取锅内物品");
        Assert.That(E.AvailableCount(s, "clean_water", true, includeContainers: true), Is.EqualTo(2));
        s.Stats.Hydration = 50;
        Start(s, ActionRequest.UseItem("clean_water"));
        Run(s);
        Assert.That(s.Stats.Hydration, Is.GreaterThan(70));
        Assert.That(E.PotUnits(s, pot.InstanceId), Is.EqualTo(1));

        E.DebugGive(s, "clean_water", 2, "camp");
        E.DebugGive(s, "cooked_root", 2, "camp");
        E.UpdateFlags(s);
        Assert.That(s.Progress.Flags, Does.Contain("supplyReady"), "锅内水计入净水3");
    }
}
