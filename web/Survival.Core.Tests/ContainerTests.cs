using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>卡片拖拽与容器案 v0.3 第 10 节 D03–D16 的内核部分，以及金属锅、烧水、烹饪的新规则。</summary>
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
    public void D09_BoilSuccess_CleanWaterStaysInPot()
    {
        var s = New();
        LightFire(s);
        E.DebugGive(s, "dirty_water", 2, "camp");
        var pot = Pot(s);
        Start(s, ActionRequest.Craft("boil"));
        Assert.That(Count(s, "dirty_water", PotOwner(pot)), Is.EqualTo(2), "按钮入口原子补水入锅");
        Assert.That(E.PotLabel(s, pot), Does.Contain("100%"));
        Assert.That(Run(s), Is.EqualTo(SegmentStatus.Completed));
        Assert.That(Count(s, "clean_water", PotOwner(pot)), Is.EqualTo(2));
        Assert.That(Count(s, "clean_water", "camp"), Is.EqualTo(2), "地面不另生成净水");
        Assert.That(E.PotLabel(s, pot), Is.EqualTo("净水100%·1000/1000毫升"));
        Assert.That(s.Progress.Flags, Does.Contain("boiled"));
        Assert.That(s.Items.All(i => i.ReservedBy == null));
    }

    [Test]
    public void Boil_Rules_CleanInPot_HalfPot_OverweightAutoFill()
    {
        var s = New();
        LightFire(s);
        var pot = Pot(s);
        Ok(s, InstantRequest.FillPot(First(s, "clean_water", "camp").InstanceId, pot.InstanceId));
        Assert.That(E.CanStart(s, ActionRequest.Craft("boil")), Is.EqualTo("锅内有净水，请先倒出或饮用"));

        var t = New();
        LightFire(t);
        E.DebugGive(t, "dirty_water", 1, "camp");
        var tp = Pot(t);
        Ok(t, InstantRequest.FillPot(First(t, "dirty_water", "camp").InstanceId, tp.InstanceId));
        Assert.That(E.CanStart(t, ActionRequest.Craft("boil")), Is.EqualTo("再加入1份脏水，凑满1升"));
        E.DebugGive(t, "dirty_water", 3, "camp");
        Start(t, ActionRequest.Craft("boil"));
        Assert.That(Count(t, "dirty_water", "camp"), Is.EqualTo(2), "半锅只补1份");

        // 开发计划 v0.2 B5：地面水补进随身锅超过30公斤时整次拒绝
        var u = New();
        LightFire(u);
        E.DebugGive(u, "dirty_water", 2, "camp");
        E.DebugGive(u, "stone", 57);
        E.DebugGive(u, "fiber", 3);
        Assert.That(E.CarriedGrams(u), Is.EqualTo(29900));
        var why = E.CanStart(u, ActionRequest.Craft("boil"));
        Assert.That(why, Does.Contain("可先把锅放到地面"));
        Assert.That(E.Start(u, ActionRequest.Craft("boil")).Ok, Is.False);
        Assert.That(Count(u, "dirty_water", "camp"), Is.EqualTo(2));
        Ok(u, InstantRequest.Drop(Pot(u).InstanceId, 1));
        Assert.That(E.CanStart(u, ActionRequest.Craft("boil")), Is.Null, "锅放到地面后可以烧");
    }

    [Test]
    public void D10_BoilPausedByFireOut_PotLocked_FuelAndIgniteAllowed()
    {
        var s = New();
        LightFire(s, 5);
        E.DebugGive(s, "dirty_water", 2, "camp");
        var pot = Pot(s);
        Start(s, ActionRequest.Craft("boil"));
        Assert.That(Run(s), Is.EqualTo(SegmentStatus.Paused));
        Assert.That(s.Main!.PauseReason, Is.EqualTo(PauseReason.FireOut));

        Assert.That(E.DoInstant(s, InstantRequest.Drop(pot.InstanceId, 1)).Reason, Does.StartWith("行动占用"));
        var child = E.PotContents(s, pot.InstanceId).Single();
        Assert.That(E.DoInstant(s, InstantRequest.Decant(child.InstanceId, "camp")).Reason, Does.StartWith("行动占用"));
        Assert.That(E.DoInstant(s, InstantRequest.FillPot(First(s, "clean_water", "camp").InstanceId, pot.InstanceId)).Reason, Does.StartWith("行动占用"));
        Assert.That(E.DoInstant(s, InstantRequest.AttachPot(pot.InstanceId)).Reason, Does.StartWith("行动占用"));
        Assert.That(E.CanStart(s, ActionRequest.UseInstance(child.InstanceId)), Is.EqualTo("该物品已被预留"));

        Ok(s, InstantRequest.AddFuel());
        Start(s, ActionRequest.Ignite());
        Run(s);
        Assert.That(s.Main!.Status, Is.EqualTo(ActionStatus.Paused), "维护后仍等明确继续");
        E.Resume(s);
        Assert.That(Run(s), Is.EqualTo(SegmentStatus.Completed));
        Assert.That(Count(s, "clean_water", PotOwner(pot)), Is.EqualTo(2));
    }

    [Test]
    public void D11_BoilCancelThenReload_DirtyWaterStaysInPot()
    {
        var s = New();
        LightFire(s);
        E.DebugGive(s, "dirty_water", 2, "camp");
        var pot = Pot(s);
        Start(s, ActionRequest.Craft("boil"));
        Steps(s, 1);
        E.Cancel(s);
        var t = SaveSerializer.Deserialize(SaveSerializer.Serialize(s));
        Assert.That(Count(t, "dirty_water", PotOwner(pot)), Is.EqualTo(2), "脏水仍在原锅");
        Assert.That(Count(t, "dirty_water"), Is.EqualTo(2), "无额外散卡");
        Assert.That(t.Main, Is.Null, "不保留加工进度");
        Assert.That(t.Items.All(i => i.ReservedBy == null));
        Start(t, ActionRequest.Craft("boil"));
        Assert.That(t.Main!.WorkDone, Is.EqualTo(0), "重新烧水重新计时");
        Assert.That(t.Main.BaseWorkMinutes, Is.EqualTo(15));
    }

    [Test]
    public void Boil_DeathReleasesWaterInPot()
    {
        var s = New();
        LightFire(s);
        E.DebugGive(s, "dirty_water", 2, "camp");
        var pot = Pot(s);
        Start(s, ActionRequest.Craft("boil"));
        s.Stats.Health = 0.01;
        s.Stats.Hydration = 0;
        E.SyncLatches(s);
        Run(s);
        Assert.That(s.Progress.Dead, Is.True);
        Assert.That(Count(s, "dirty_water", PotOwner(pot)), Is.EqualTo(2));
        Assert.That(s.Items.All(i => i.ReservedBy == null));
    }

    [Test]
    public void Cook_NeedsEmptyPot_GroundPotAllowed()
    {
        var s = New();
        LightFire(s);
        E.DebugGive(s, "raw_root", 2, "camp");
        var pot = Pot(s);
        Ok(s, InstantRequest.FillPot(First(s, "clean_water", "camp").InstanceId, pot.InstanceId));
        Assert.That(E.CanStart(s, ActionRequest.Craft("cook")), Is.EqualTo("锅内有水，先倒出或饮用"));
        Ok(s, InstantRequest.Decant(E.PotContents(s, pot.InstanceId).Single().InstanceId, "camp"));
        Ok(s, InstantRequest.Drop(pot.InstanceId, 1));
        Start(s, ActionRequest.Craft("cook"));
        Assert.That(s.Main!.LockedItemIds, Does.Contain(pot.InstanceId), "地面锅也可催化并被锁定");
        Assert.That(Run(s), Is.EqualTo(SegmentStatus.Completed));
        Assert.That(Count(s, "cooked_root", "camp"), Is.EqualTo(2));
        Assert.That(pot.Owner, Is.EqualTo("camp"));
    }

    [Test]
    public void B1_IgniteWithGroundLighter()
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
        Assert.That(E.CanStart(t, ActionRequest.Ignite()), Is.Null, "按钮点火同样接受地面打火器");
    }

    [Test]
    public void B2_PotSelection_SlotThenCarriedThenGround()
    {
        var s = New();
        LightFire(s);
        E.DebugGive(s, "pot", 1, "camp");
        E.DebugGive(s, "dirty_water", 2, "camp");
        var carried = s.Items.First(i => i.ItemId == "pot" && i.IsCarried);
        var ground = s.Items.First(i => i.ItemId == "pot" && i.Owner == "camp");
        Assert.That(E.SelectPot(s, null, out _), Is.SameAs(carried), "无锅位时随身锅优先");
        Ok(s, InstantRequest.AttachPot(ground.InstanceId));
        Assert.That(E.SelectPot(s, null, out _), Is.SameAs(ground), "锅位锅最优先");
        Start(s, ActionRequest.Craft("boil", carried.InstanceId));
        Assert.That(s.Main!.PotInstanceId, Is.EqualTo(carried.InstanceId), "指定的锅优先");
        Assert.That(E.SelectPot(s, carried.InstanceId, out var why), Is.Null);
        Assert.That(why, Does.StartWith("行动占用"));
    }

    [Test]
    public void AttachPot_CarriedPotMovesToGround_SlotSingle()
    {
        var s = New();
        var pot = Pot(s);
        Ok(s, InstantRequest.AttachPot(pot.InstanceId));
        Assert.That(pot.Owner, Is.EqualTo("camp"), "随身锅原子转到营地地面");
        Assert.That(s.Locations["camp"].Facility.AttachedPotId, Is.EqualTo(pot.InstanceId));
        E.DebugGive(s, "pot", 1, "camp");
        var other = s.Items.Last(i => i.ItemId == "pot");
        Assert.That(E.DoInstant(s, InstantRequest.AttachPot(other.InstanceId)).Reason, Does.Contain("请先取下"));
        Ok(s, InstantRequest.DetachPot());
        Assert.That(s.Locations["camp"].Facility.AttachedPotId, Is.Null);
        Assert.That(pot.Owner, Is.EqualTo("camp"), "取下后仍在地面");
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

        LightFire(t);
        E.DebugGive(t, "raw_root", 2, "camp");
        Start(t, ActionRequest.Craft("cook"));
        Assert.That(E.AvailableCount(t, "raw_root", true), Is.EqualTo(0), "预留批次不重复可用");
        var u = SaveSerializer.Deserialize(SaveSerializer.Serialize(t));
        Assert.That(E.AvailableCount(u, "raw_root", true), Is.EqualTo(0));
    }

    [Test]
    public void D16_AttachedFullPotPickedUp_DetachesAndWeighs2Kg()
    {
        var s = New();
        E.DebugGive(s, "dirty_water", 2, "camp");
        var pot = Pot(s);
        Ok(s, InstantRequest.FillPot(First(s, "dirty_water", "camp").InstanceId, pot.InstanceId, 2));
        Ok(s, InstantRequest.AttachPot(pot.InstanceId));
        var before = E.CarriedGrams(s);
        Ok(s, InstantRequest.Pickup(pot.InstanceId, 1));
        Assert.That(s.Locations["camp"].Facility.AttachedPotId, Is.Null, "先解除关联");
        Assert.That(E.CarriedGrams(s) - before, Is.EqualTo(2000), "满锅按2公斤计");
        Start(s, ActionRequest.Travel("field"));
        Run(s);
        Assert.That(E.DoInstant(s, InstantRequest.AttachPot(pot.InstanceId)).Reason, Is.EqualTo("这里没有火堆"));
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
