using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>物资搜集案 07 验收用例 G01–G08。</summary>
public class GatherCraftTests : TestBase
{
    private GameState AtWoods(uint seed = 7)
    {
        var s = New(seed);
        E.DebugTeleport(s, "woods");
        E.DebugSetProgress(s, "woods", 75);
        return s;
    }

    [Test]
    public void G01_HatchetWithOneWoodLeft()
    {
        var s = AtWoods();
        E.DebugGive(s, "hatchet", 1);
        s.Resources["wood"].Available = 1;
        var p = E.Preview(s, ActionRequest.Gather("wood"));
        Assert.That(p.Outputs.Single().Count, Is.EqualTo(1), "预览与实际都显示1");
        Start(s, ActionRequest.Gather("wood"));
        s.Main!.HazardHit = false;
        Run(s);
        Assert.That(s.GameMinute, Is.EqualTo(480 + 30), "耗时不变");
        Assert.That(Count(s, "wood", "woods"), Is.EqualTo(1));
        Assert.That(s.Resources["wood"].Available, Is.EqualTo(0));
        Assert.That(First(s, "hatchet").Durability, Is.EqualTo(29));
    }

    [Test]
    public void G01b_HatchetDoublesWood_KnifeHalvesFiberTime()
    {
        var s = AtWoods();
        E.DebugGive(s, "hatchet", 1);
        E.DebugGive(s, "stone_knife", 1);
        Start(s, ActionRequest.Gather("wood"));
        s.Main!.HazardHit = false;
        Run(s);
        Assert.That(Count(s, "wood", "woods"), Is.EqualTo(2));
        Assert.That(s.Resources["wood"].Available, Is.EqualTo(8), "工具加产也须多扣库存");
        var t0 = s.GameMinute;
        Start(s, ActionRequest.Gather("woods_fiber"));
        Run(s);
        Assert.That(s.GameMinute - t0, Is.EqualTo(10));
        Assert.That(First(s, "stone_knife").Durability, Is.EqualTo(19));
    }

    [Test]
    public void G02_CacheSearchedOnce_NoResetAcrossDays()
    {
        var s = New();
        E.DebugTeleport(s, "village");
        E.DebugSetProgress(s, "village", 25);
        Start(s, ActionRequest.Gather("supply_box"));
        Run(s);
        Assert.That(Count(s, "canned_food", "village"), Is.EqualTo(2));
        Assert.That(Count(s, "clean_water", "village"), Is.EqualTo(1));
        var r = E.Start(s, ActionRequest.Gather("supply_box"));
        Assert.That(r.Ok, Is.False);
        Assert.That(r.Reason, Is.EqualTo("该容器已搜空，不再补充"));
        s.GameMinute = At(3, 23, 55);
        Start(s, ActionRequest.Wait(30));
        Run(s);
        Assert.That(s.Resources["supply_box"].Available, Is.EqualTo(0));
    }

    [Test]
    public void G03_PickupBeyond30kgRejected()
    {
        var s = New();
        E.DebugGive(s, "stone", 56);  // 28kg
        E.DebugGive(s, "fiber", 7);   // 0.7kg
        E.DebugGive(s, "wood", 2, "camp");
        // 随身：锅1kg + 打火器0.1kg + 28 + 0.7 = 29.8kg
        Assert.That(E.CarriedGrams(s), Is.EqualTo(29800));
        var wood = First(s, "wood", "camp");
        var r = E.DoInstant(s, InstantRequest.Pickup(wood.InstanceId, 1));
        Assert.That(r.Ok, Is.False);
        Assert.That(r.MaxCount, Is.EqualTo(0));
        Assert.That(E.CarriedGrams(s), Is.EqualTo(29800));
        Assert.That(s.GameMinute, Is.EqualTo(480));

        E.DoInstant(s, InstantRequest.Drop(First(s, "fiber", "carried").InstanceId, 2));
        Assert.That(E.CarriedGrams(s), Is.EqualTo(29600));
        Assert.That(E.DoInstant(s, InstantRequest.Pickup(First(s, "fiber", "camp").InstanceId, 2)).Ok, Is.True, "恰好不超过30公斤可拾取");
    }

    [Test]
    public void G04_FactorsAffectTimeNotYield()
    {
        var s = New();
        E.DebugTeleport(s, "field");
        s.GameMinute = At(1, 21, 0);
        SetStats(s, 100, 80, 80, 15, 90, 60);
        Start(s, ActionRequest.Gather("roots"));
        Run(s);
        Assert.That(s.GameMinute - At(1, 21, 0), Is.EqualTo(60), "30÷(5÷1.875)=11.25→12段");
        Assert.That(Count(s, "raw_root", "field"), Is.EqualTo(2));
    }

    [Test]
    public void G05_ReservedAcrossMidnightThenCancel()
    {
        var s = New();
        E.DebugTeleport(s, "field");
        s.Resources["roots"].Available = 3;
        s.GameMinute = At(1, 23, 50);
        Start(s, ActionRequest.Gather("roots"));
        Assert.That(s.Resources["roots"].Available, Is.EqualTo(1));
        Assert.That(s.Resources["roots"].Reserved, Is.EqualTo(2));
        Steps(s, 3);
        Assert.That(s.Resources["roots"].Available, Is.EqualTo(4), "min(6−2, 1+3)");
        E.Cancel(s);
        Assert.That(s.Resources["roots"].Available, Is.EqualTo(6));
        Assert.That(s.Resources["roots"].Reserved, Is.EqualTo(0));
        Assert.That(Count(s, "raw_root"), Is.EqualTo(0));
        Start(s, ActionRequest.Wait(60));
        Run(s);
        Assert.That(s.Resources["roots"].Available, Is.EqualTo(6), "无重复补充");
    }

    [Test]
    public void G05b_ReservedAcrossMidnightThenComplete()
    {
        var s = New();
        E.DebugTeleport(s, "field");
        s.Resources["roots"].Available = 3;
        s.GameMinute = At(1, 23, 50);
        Start(s, ActionRequest.Gather("roots"));
        Run(s);
        Assert.That(s.Resources["roots"].Available, Is.EqualTo(4), "完成不再次扣减");
        Assert.That(Count(s, "raw_root", "field"), Is.EqualTo(2));
    }

    [Test]
    public void G06a_CookPausesWhenFireOut_ThenRefuelIgniteContinue()
    {
        var s = New();
        var f = s.Locations["camp"].Facility;
        E.DebugGive(s, "raw_root", 2, "camp");
        f.FireLit = true;
        f.FuelMinutes = 10;
        Start(s, ActionRequest.Craft("cook"));
        Assert.That(Run(s), Is.EqualTo(SegmentStatus.Paused));
        Assert.That(s.Main!.PauseReason, Is.EqualTo(PauseReason.FireOut));
        Assert.That(s.Main.WorkDone, Is.EqualTo(10).Within(1e-9));
        Assert.That(Count(s, "cooked_root"), Is.EqualTo(0));

        Assert.That(E.DoInstant(s, InstantRequest.AddFuel()).Ok, Is.True);
        Assert.That(f.FuelMinutes, Is.EqualTo(60));
        Start(s, ActionRequest.Ignite(First(s, "lighter").InstanceId));
        Assert.That(s.Sub, Is.Not.Null);
        Run(s);
        Assert.That(s.Sub, Is.Null);
        Assert.That(s.Main!.Status, Is.EqualTo(ActionStatus.Paused), "点火后不自动继续");
        Assert.That(f.FuelMinutes, Is.EqualTo(55));
        E.Resume(s);
        Assert.That(Run(s), Is.EqualTo(SegmentStatus.Completed));
        Assert.That(Count(s, "cooked_root", "camp"), Is.EqualTo(2));
        Assert.That(Count(s, "raw_root"), Is.EqualTo(0));
    }

    [Test]
    public void G06b_IngredientSpoilsDuringCook_Aborts()
    {
        var s = New();
        var f = s.Locations["camp"].Facility;
        f.FireLit = true;
        f.FuelMinutes = 120;
        E.DebugGive(s, "raw_root", 2, "camp");
        First(s, "raw_root").ExpiresAt = 480 + 30; // 恰好在完成时刻到期
        Start(s, ActionRequest.Craft("cook"));
        Assert.That(Run(s), Is.EqualTo(SegmentStatus.Aborted));
        Assert.That(Count(s, "cooked_root"), Is.EqualTo(0));
        var raw = First(s, "raw_root");
        Assert.That(raw.Owner, Is.EqualTo("camp"));
        Assert.That(raw.ReservedBy, Is.Null);
        Assert.That(E.IsSpoiled(s, raw), Is.True);
    }

    [Test]
    public void G07_DoubleSubmitAndReload_SingleGrant()
    {
        var s = AtWoods(seed: 99);
        E.DebugGive(s, "hatchet", 1);
        Start(s, ActionRequest.Gather("wood"));
        Assert.That(E.Start(s, ActionRequest.Gather("wood")).Ok, Is.False, "重复点击被拒绝");
        Steps(s, 2);
        var hazard = s.Main!.HazardHit;
        var t = SaveSerializer.Deserialize(SaveSerializer.Serialize(s));
        Assert.That(t.Main!.HazardHit, Is.EqualTo(hazard), "读档不重掷");
        Run(s);
        Run(t);
        Assert.That(SaveSerializer.Serialize(t), Is.EqualTo(SaveSerializer.Serialize(s)));
        Assert.That(First(t, "hatchet").Durability, Is.EqualTo(29), "只磨损一次");
        Assert.That(Count(t, "wood", "woods"), Is.EqualTo(2));
    }

    [Test]
    public void G08_BuildTwiceRejected()
    {
        var s = New();
        E.DebugGive(s, "wood", 4);
        E.DebugGive(s, "fiber", 4);
        Start(s, ActionRequest.Craft("shelter"));
        Run(s);
        Assert.That(s.Locations["camp"].Facility.ShelterBuilt, Is.True);
        var minute = s.GameMinute;
        var wood = Count(s, "wood");
        var r = E.Start(s, ActionRequest.Craft("shelter"));
        Assert.That(r.Ok, Is.False);
        Assert.That(r.Reason, Is.EqualTo("已建造"));
        Assert.That(s.GameMinute, Is.EqualTo(minute));
        Assert.That(Count(s, "wood"), Is.EqualTo(wood));
    }

    [Test]
    public void Craft_CancelReturnsMaterialsToGround()
    {
        var s = New();
        Start(s, ActionRequest.Craft("knife"));
        Steps(s, 2);
        E.Cancel(s);
        Assert.That(Count(s, "stone", "camp"), Is.EqualTo(2));
        Assert.That(Count(s, "fiber", "camp"), Is.EqualTo(2));
        Assert.That(s.Items.Count(i => i.ItemId == "stone"), Is.EqualTo(1), "返还后重新合堆");
        Assert.That(s.Items.All(i => i.ReservedBy == null));
    }

    [Test]
    public void Fire_IgniteConsumesWood_AddFuelCapRejects()
    {
        var s = New();
        Start(s, ActionRequest.Ignite(First(s, "lighter").InstanceId));
        Run(s);
        var f = s.Locations["camp"].Facility;
        Assert.That(f.FireLit, Is.True);
        Assert.That(f.FuelMinutes, Is.EqualTo(55));
        Assert.That(Count(s, "wood"), Is.EqualTo(1));
        E.DebugGive(s, "wood", 5, "camp");
        for (var i = 0; i < 3; i++) Assert.That(E.DoInstant(s, InstantRequest.AddFuel()).Ok, Is.True);
        Assert.That(f.FuelMinutes, Is.EqualTo(235));
        var woodBefore = Count(s, "wood");
        var r = E.DoInstant(s, InstantRequest.AddFuel());
        Assert.That(r.Ok, Is.False, "235+60>240 拒绝");
        Assert.That(Count(s, "wood"), Is.EqualTo(woodBefore), "拒绝时不扣木材");
        Assert.That(E.DoInstant(s, InstantRequest.Extinguish()).Ok, Is.True);
        Assert.That(f.FuelMinutes, Is.EqualTo(235), "熄火保留燃料");
    }

    [Test]
    public void Preview_BuildAtNight_ListsFireOutAsFactorChange()
    {
        // 夜间 03:40，火堆还剩 55 分钟：建棚开始按 ×1 算，火堆燃尽后转为夜间无照明 ×1.5。
        // UI 案 06 要求列出导致耗时增加的原因，地图案 07 要求预计时间随之更新。
        var s = New();
        s.GameMinute = At(2, 3, 40);
        E.DebugGive(s, "wood", 4, "camp");
        E.DebugGive(s, "fiber", 4, "camp");
        var f = s.Locations["camp"].Facility;
        f.FireLit = true;
        f.FuelMinutes = 55;

        var p = E.Preview(s, ActionRequest.Craft("shelter"));
        Assert.That(p.Ok, Is.True, p.Reason);
        Assert.That(p.FactorAtStart, Is.EqualTo(1).Within(1e-9), "有照明时开工倍率为 1");
        Assert.That(p.EstimatedMinutes, Is.GreaterThan(120), "火堆燃尽后剩余工作按 ×1.5 计时");
        var w = p.Warnings.FirstOrDefault(x => x.Contains("火堆燃尽"));
        Assert.That(w, Is.Not.Null, string.Join(" | ", p.Warnings));
        Assert.That(w, Does.Contain("04:35").And.Contain("×1.5").And.Contain("夜间无照明"));

        // 火堆足够烧完全程时不应出现倍率变化提示
        f.FuelMinutes = 240;
        var p2 = E.Preview(s, ActionRequest.Craft("shelter"));
        Assert.That(p2.EstimatedMinutes, Is.EqualTo(120));
        Assert.That(p2.Warnings.Any(x => x.Contains("倍率变为")), Is.False, string.Join(" | ", p2.Warnings));
    }
}
