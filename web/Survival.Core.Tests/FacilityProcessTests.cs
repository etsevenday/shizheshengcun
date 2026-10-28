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
        Assert.That(r.Reason, Does.Contain("2.60公斤"));
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

    /// <summary>空闲时等待指定分钟（每段推进世界并结算设施）；n 须为 5 的倍数。</summary>
    private void WaitMinutes(GameState s, int minutes)
    {
        var end = s.GameMinute + minutes;
        while (s.GameMinute < end)
        {
            if (s.Main == null) Start(s, ActionRequest.Wait(30));
            else if (s.Main.Status == ActionStatus.Paused) E.Resume(s);
            E.Advance(s);
        }
        if (s.Main?.Type == ActionType.Wait) E.Cancel(s);
    }

    private ItemInstance RootInFire(GameState s)
    {
        var r = GiveRoots(s, 1);
        Ok(s, InstantRequest.PutInFire(r[0].InstanceId));
        return s.Items.Where(i => i.Owner == Fire && i.ItemId == "raw_root").OrderByDescending(i => i.PlacedOrder).First();
    }

    [Test]
    public void P406_Roast1Root30Minutes_CookedStaysInSlot_NoPotNoWater()
    {
        var s = New();
        LightFire(s);
        var pot = Pot(s);
        var root = RootInFire(s);
        var water = Count(s, "clean_water");
        Assert.That(E.ContentStatus(s, root), Is.EqualTo("烤制中 受热0/30分钟"));
        WaitMinutes(s, 25);
        Assert.That(root.ItemId, Is.EqualTo("raw_root"));
        Assert.That(E.ProcessOf(s, root)!.HeatDoneMinutes, Is.EqualTo(25));
        WaitMinutes(s, 5);
        Assert.That(root.ItemId, Is.EqualTo("cooked_root"));
        Assert.That(root.Owner, Is.EqualTo(Fire), "成品留在原加工位");
        Assert.That(root.ExpiresAt, Is.EqualTo(s.GameMinute + 1440), "保质期从完成时刻算24小时");
        Assert.That(pot.Owner, Is.EqualTo("carried"), "不需要锅");
        Assert.That(Count(s, "clean_water"), Is.EqualTo(water), "不需要水");
        Assert.That(E.ProcessOf(s, root), Is.Null);
        Assert.That(s.Progress.Flags, Does.Contain("cooked"));
        Assert.That(s.Log.Any(l => l.Text.Contains("已烤成烤熟块根")), Is.True);
    }

    [Test]
    public void P407_RootPlusClean500_30Minutes_BoiledRoot_WaterMinus500()
    {
        var s = New();
        LightFire(s);
        var pot = Pot(s);
        FillWater(s, pot, "clean_water", 1);
        Ok(s, InstantRequest.PutInFire(pot.InstanceId));
        PutRootsInPot(s, pot, 1);
        var root = E.PotFood(s, pot.InstanceId).Single();
        Assert.That(E.ProcessOf(s, root)!.ReservedWaterMl, Is.EqualTo(500), "放入即预留该份净水");
        Assert.That(E.PotFreeWaterUnits(s, pot), Is.EqualTo(0));
        WaitMinutes(s, 30);
        Assert.That(root.ItemId, Is.EqualTo("boiled_root"), "不是烤熟块根");
        Assert.That(root.Owner, Is.EqualTo(PotOwner(pot)), "留在原锅");
        Assert.That(E.PotUnits(s, pot.InstanceId), Is.EqualTo(0), "水−500毫升");
        Assert.That(Count(s, "cooked_root"), Is.EqualTo(0));
        Assert.That(E.ItemGrams(s, root), Is.EqualTo(300), "成品重300克，加工水不另产物");
    }

    [Test]
    public void P408_TwoRootsOne500_FirstProcessed_SecondWaits_NoAutoFill_ManualWaterDoesNotResetFirst()
    {
        var s = New();
        LightFire(s, 240);
        var pot = Pot(s);
        FillWater(s, pot, "clean_water", 1);
        Ok(s, InstantRequest.PutInFire(pot.InstanceId));
        PutRootsInPot(s, pot, 2);
        var foods = E.PotFood(s, pot.InstanceId);
        Assert.That(foods, Has.Count.EqualTo(2));
        var (first, second) = (foods[0], foods[1]);
        Assert.That(E.ProcessOf(s, first)!.ReservedWaterMl, Is.EqualTo(500), "先放入的先分到水");
        Assert.That(E.ContentStatus(s, second), Is.EqualTo("待净水500毫升"));
        E.DebugGive(s, "clean_water", 3, "camp");
        WaitMinutes(s, 15);
        Assert.That(E.ProcessOf(s, first)!.HeatDoneMinutes, Is.EqualTo(15));
        Assert.That(E.ProcessOf(s, second)!.HeatDoneMinutes, Is.EqualTo(0), "等水的不受热");
        Assert.That(E.PotUnits(s, pot.InstanceId), Is.EqualTo(1), "不自动补水");
        Assert.That(E.DoInstant(s, InstantRequest.Decant(E.PotLiquids(s, pot.InstanceId).Single().InstanceId, "camp")).Reason,
            Is.EqualTo($"500毫升供生块根#{first.InstanceId}加工，先取出该食材以停止加工"), "预留的水不能倒出");
        Assert.That(E.CanStart(s, ActionRequest.UseInstance(E.PotLiquids(s, pot.InstanceId).Single().InstanceId)), Does.StartWith("500毫升供生块根"), "也不能饮用");
        // 手动加同种净水：第二根从自己的0分钟开始，不重置第一根
        Ok(s, InstantRequest.FillPot(First(s, "clean_water", "camp").InstanceId, pot.InstanceId));
        Assert.That(E.ProcessOf(s, second)!.ReservedWaterMl, Is.EqualTo(500));
        Assert.That(E.ProcessOf(s, first)!.HeatDoneMinutes, Is.EqualTo(15), "净水追加不重置已有进度");
        WaitMinutes(s, 15);
        Assert.That(first.ItemId, Is.EqualTo("boiled_root"));
        Assert.That(second.ItemId, Is.EqualTo("raw_root"));
        Assert.That(E.ProcessOf(s, second)!.HeatDoneMinutes, Is.EqualTo(15));
        WaitMinutes(s, 15);
        Assert.That(second.ItemId, Is.EqualTo("boiled_root"));
        Assert.That(E.PotUnits(s, pot.InstanceId), Is.EqualTo(0));
    }

    [Test]
    public void P409_Dirty1000PlusTwoRoots_15MinClean_45MinBothBoiled_WaterZero()
    {
        var s = New();
        LightFire(s, 240);
        var pot = Pot(s);
        FillWater(s, pot, "dirty_water", 2);
        Ok(s, InstantRequest.PutInFire(pot.InstanceId));
        PutRootsInPot(s, pot, 2);
        var foods = E.PotFood(s, pot.InstanceId);
        Assert.That(foods.Select(f => E.ContentStatus(s, f)), Is.All.EqualTo("等待水净化"));
        WaitMinutes(s, 15);
        Assert.That(E.PotLiquid(s, pot.InstanceId), Is.EqualTo("clean_water"), "15分钟净水");
        Assert.That(E.PotUnits(s, pot.InstanceId), Is.EqualTo(2));
        Assert.That(foods.Select(f => E.ProcessOf(s, f)!.HeatDoneMinutes), Is.All.EqualTo(0), "同一段不能既净化又给食物受热");
        Assert.That(foods.Select(f => E.ProcessOf(s, f)!.ReservedWaterMl), Is.All.EqualTo(500), "1000毫升支持2份");
        WaitMinutes(s, 25);
        Assert.That(foods.Select(f => f.ItemId), Is.All.EqualTo("raw_root"));
        WaitMinutes(s, 5);
        Assert.That(foods.Select(f => f.ItemId), Is.All.EqualTo("boiled_root"), "最早45分钟两根水煮完成");
        Assert.That(E.PotUnits(s, pot.InstanceId), Is.EqualTo(0), "水归0");
        Assert.That(s.Progress.Flags, Is.SupersetOf(new[] { "boiled", "cooked" }));
    }

    [Test]
    public void P410_HalfPotDirty_15Minutes_HalfPotClean_NoTopUp()
    {
        var s = New();
        LightFire(s);
        var pot = Pot(s);
        FillWater(s, pot, "dirty_water", 1);
        E.DebugGive(s, "dirty_water", 3, "camp");
        Ok(s, InstantRequest.PutInFire(pot.InstanceId));
        Assert.That(E.ContentStatus(s, pot), Is.EqualTo("烧水中 受热0/15分钟"), "半锅也能烧，不强迫凑满");
        WaitMinutes(s, 15);
        Assert.That(E.PotLabel(s, pot), Is.EqualTo("净水50%·500/1000毫升"));
        Assert.That(Count(s, "dirty_water", "camp"), Is.EqualTo(3), "不从散卡补水");
    }

    [Test]
    public void P411_TwoRootsParallelWithClear20_Heat20_Fuel20_CharacterOnlyClear()
    {
        var s = New(31);
        LightFire(s, 120);
        var t = s.Clone();
        var a = RootInFire(s);
        var b = RootInFire(s);
        Start(s, ActionRequest.Clear());
        Run(s);
        Start(t, ActionRequest.Clear());
        Run(t);
        Assert.That(E.ProcessOf(s, a)!.HeatDoneMinutes, Is.EqualTo(20));
        Assert.That(E.ProcessOf(s, b)!.HeatDoneMinutes, Is.EqualTo(20));
        Assert.That(s.Locations["camp"].Facility.FuelMinutes, Is.EqualTo(100), "燃料只减20，不按目标数重复扣");
        foreach (var id in Stats.All)
            Assert.That(s.Stats.Get(id), Is.EqualTo(t.Stats.Get(id)).Within(1e-9), $"角色只扣清理消耗：{id}");
        Assert.That(s.GameMinute, Is.EqualTo(t.GameMinute));
    }

    [Test]
    public void P412_BrowsePausedOffline_NoWorldTime_NoProgress()
    {
        var s = New();
        LightFire(s);
        var root = RootInFire(s);
        var minute = s.GameMinute;
        Assert.That(E.Advance(s).Status, Is.EqualTo(SegmentStatus.Idle), "无主行动不推进");
        E.Preview(s, ActionRequest.Wait(60));
        Assert.That(s.GameMinute, Is.EqualTo(minute));
        Assert.That(E.ProcessOf(s, root)!.HeatDoneMinutes, Is.EqualTo(0), "浏览与预览不推进加工");

        SetStats(s, 100, 80, 20.2, 90, 90, 60);
        Start(s, ActionRequest.Clear());
        Assert.That(Run(s, autoResume: false), Is.EqualTo(SegmentStatus.Paused));
        var heat = E.ProcessOf(s, root)!.HeatDoneMinutes;
        var m2 = s.GameMinute;
        for (var k = 0; k < 3; k++) Assert.That(E.Advance(s).Status, Is.EqualTo(SegmentStatus.Paused));
        Assert.That(s.GameMinute, Is.EqualTo(m2), "暂停无子行动时世界时间冻结");
        Assert.That(E.ProcessOf(s, root)!.HeatDoneMinutes, Is.EqualTo(heat));
        // 暂停中的5分钟补给子行动正常推进设施
        Start(s, ActionRequest.UseItem("canned_food"));
        Run(s, autoResume: false);
        Assert.That(E.ProcessOf(s, root)!.HeatDoneMinutes, Is.EqualTo(heat + 5));
        // 现实离线：存档原样读回，不补算
        var t = SaveSerializer.Deserialize(SaveSerializer.Serialize(s));
        Assert.That(t.GameMinute, Is.EqualTo(s.GameMinute));
        Assert.That(E.ProcessOf(t, t.Items.Single(i => i.InstanceId == root.InstanceId))!.HeatDoneMinutes, Is.EqualTo(heat + 5));
    }

    [Test]
    public void P413_FireOutThenRelit_ProgressKept_AutoResume_MainActionUntouched()
    {
        var s = New();
        LightFire(s, 10);
        var root = RootInFire(s);
        Start(s, ActionRequest.Wait(60));
        var id = s.Main!.ActionId;
        Steps(s, 4);
        Assert.That(s.Main!.ActionId, Is.EqualTo(id), "设施熄火不替换角色主行动");
        Assert.That(s.Main.Status, Is.EqualTo(ActionStatus.Running), "也不暂停");
        Assert.That(E.ProcessOf(s, root)!.HeatDoneMinutes, Is.EqualTo(10));
        E.Cancel(s);
        Ok(s, InstantRequest.AddFuel());
        Start(s, ActionRequest.Ignite(First(s, "lighter").InstanceId));
        Run(s);
        WaitMinutes(s, 20);
        Assert.That(root.ItemId, Is.EqualTo("cooked_root"), "复燃后自动续行，不需要“继续”");
    }

    [Test]
    public void P414_TakePotOutBeforeDone_PutBack_HeatFromZero_OtherSlotKept()
    {
        var s = New();
        LightFire(s, 240);
        var pot = Pot(s);
        FillWater(s, pot, "clean_water", 1);
        Ok(s, InstantRequest.PutInFire(pot.InstanceId));
        PutRootsInPot(s, pot, 1);
        var boiled = E.PotFood(s, pot.InstanceId).Single();
        var roast = RootInFire(s);
        WaitMinutes(s, 15);
        Assert.That(E.ProcessOf(s, boiled)!.HeatDoneMinutes, Is.EqualTo(15));
        Ok(s, InstantRequest.TakeOut(pot.InstanceId, "camp"));
        Assert.That(E.ProcessOf(s, boiled), Is.Null, "取锅即清整锅进度");
        Assert.That(E.PotFood(s, pot.InstanceId), Has.Count.EqualTo(1), "内容随锅保留");
        Assert.That(E.PotUnits(s, pot.InstanceId), Is.EqualTo(1));
        Assert.That(E.PotFreeWaterUnits(s, pot), Is.EqualTo(1), "离开火堆后水不再被预留");
        Assert.That(s.Log.Last().Text, Does.Contain("相关加工进度清零"));
        Ok(s, InstantRequest.PutInFire(pot.InstanceId));
        Assert.That(E.ProcessOf(s, boiled)!.HeatDoneMinutes, Is.EqualTo(0), "重新放上从0开始");
        Assert.That(E.ProcessOf(s, roast)!.HeatDoneMinutes, Is.EqualTo(15), "其他加工位不受影响");
        // 取出一份食材只清它自己
        WaitMinutes(s, 5);
        Ok(s, InstantRequest.TakeOut(roast.InstanceId, "carried"));
        Assert.That(roast.Owner, Is.EqualTo("carried"));
        Assert.That(E.ProcessOf(s, roast), Is.Null);
        Assert.That(E.ProcessOf(s, boiled)!.HeatDoneMinutes, Is.EqualTo(5));
    }

    [Test]
    public void P415_ExactlyFiveFuelFinishesLastSegment_LessGivesNoHeat()
    {
        foreach (var (fuel, done) in new[] { (5, true), (4, false) })
        {
            var s = New();
            LightFire(s, 60);
            var root = RootInFire(s);
            E.ProcessOf(s, root)!.HeatDoneMinutes = 25;
            s.Locations["camp"].Facility.FuelMinutes = fuel;
            Start(s, ActionRequest.Wait(30));
            E.Advance(s);
            Assert.That(s.Locations["camp"].Facility.FireLit, Is.False);
            if (done) Assert.That(root.ItemId, Is.EqualTo("cooked_root"), $"燃料{fuel}");
            else
            {
                Assert.That(root.ItemId, Is.EqualTo("raw_root"), $"燃料{fuel}");
                Assert.That(E.ProcessOf(s, root)!.HeatDoneMinutes, Is.EqualTo(25), "不给部分受热");
            }
        }
    }

    [Test]
    public void P416_ExpiresExactlyAtCompletion_NoProduct_DeathSameSegment_NotCommitted()
    {
        var s = New();
        LightFire(s);
        var root = RootInFire(s);
        root.ExpiresAt = s.GameMinute + 30;
        WaitMinutes(s, 30);
        // Q16（S11 §10）：新鲜食材实际受热后受本次加工保护，恰好在完成时刻到期也照常熟成，成品从完成起算保质
        Assert.That(root.ItemId, Is.EqualTo("cooked_root"), "受保护食材跨原期限熟成");
        Assert.That(E.IsSpoiled(s, root), Is.False);
        Assert.That(root.Owner, Is.EqualTo(Fire), "成品留原位");

        // 未受热（火堆没点）到期：照常腐败，之后点火也不再受热恢复
        var u = New();
        var r0 = RootInFire(u);
        r0.ExpiresAt = u.GameMinute + 10;
        WaitMinutes(u, 10);
        Assert.That(E.IsSpoiled(u, r0), Is.True, "未受热的原料照常腐败");
        LightFire(u);
        WaitMinutes(u, 40);
        Assert.That(r0.ItemId, Is.EqualTo("raw_root"), "腐败后不再受热恢复");
        Assert.That(r0.Owner, Is.EqualTo(Fire), "留原位显示腐败");

        var t = New();
        LightFire(t);
        var r2 = RootInFire(t);
        E.ProcessOf(t, r2)!.HeatDoneMinutes = 25;
        Start(t, ActionRequest.Wait(30));
        t.Stats.Health = 0.01;
        t.Stats.Hydration = 0;
        E.SyncLatches(t);
        Assert.That(E.Advance(t).Status, Is.EqualTo(SegmentStatus.Dead));
        Assert.That(r2.ItemId, Is.EqualTo("raw_root"), "死亡前未提交的加工不转化");
        Assert.That(E.ProcessOf(t, r2)!.HeatDoneMinutes, Is.EqualTo(25));
    }

    [Test]
    public void P417_ReloadAndReplay_NoDoubleWaterFuelFoodOrExpiry()
    {
        var s = New();
        LightFire(s, 120);
        var pot = Pot(s);
        FillWater(s, pot, "clean_water", 2);
        Ok(s, InstantRequest.PutInFire(pot.InstanceId));
        PutRootsInPot(s, pot, 1);
        var root = E.PotFood(s, pot.InstanceId).Single();
        WaitMinutes(s, 30);
        Assert.That(root.ItemId, Is.EqualTo("boiled_root"));
        var fac = s.Locations["camp"].Facility;
        Assert.That(fac.Completed, Has.Count.EqualTo(1), "写入完成标记");
        var json = SaveSerializer.Serialize(s);
        var t = SaveSerializer.Deserialize(json);
        Assert.That(SaveSerializer.Serialize(t), Is.EqualTo(json), "读档不重发、不改状态");
        var tRoot = t.Items.Single(i => i.InstanceId == root.InstanceId);
        Assert.That(E.Advance(t).Status, Is.EqualTo(SegmentStatus.Idle));
        Assert.That(E.PotUnits(t, pot.InstanceId), Is.EqualTo(1), "不重复扣水");
        Assert.That(t.Locations["camp"].Facility.FuelMinutes, Is.EqualTo(fac.FuelMinutes), "不重复扣燃料");
        Assert.That(tRoot.ExpiresAt, Is.EqualTo(root.ExpiresAt), "不刷新保质");
        WaitMinutes(t, 30);
        Assert.That(t.Items.Count(i => i.ItemId == "boiled_root"), Is.EqualTo(1), "完成的成品不再处理、不再发放");
        Assert.That(tRoot.ExpiresAt, Is.EqualTo(root.ExpiresAt));
        Assert.That(E.PotUnits(t, pot.InstanceId), Is.EqualTo(1));
        // 同一放入请求重发只生效一次
        var u = New();
        var req = InstantRequest.PutInFire(Pot(u).InstanceId).WithRequestId("put-1");
        Ok(u, req);
        Ok(u, req);
        Assert.That(u.Items.Count(i => i.Owner == Fire), Is.EqualTo(1));
    }

    [Test]
    public void P419_IgniteNeedsChosenLighter_DragBindsInstance_IgnitionSegmentGivesNoHeat()
    {
        var s = New();
        var root = RootInFire(s);
        Assert.That(E.CanStart(s, ActionRequest.Ignite()), Is.EqualTo("请选择要使用的打火器"), "按钮无选中工具禁止开始");
        E.DebugGive(s, "lighter", 1, "camp");
        var ground = s.Items.Single(i => i.ItemId == "lighter" && i.Owner == "camp");
        var drag = E.ResolveDrop(s, DropSource.Parse($"card:camp|lighter|{ground.InstanceId}")!, DropTarget.Parse("fire:camp")!);
        Assert.That(drag.Verb, Is.EqualTo(DropVerb.Ignite), "v0.7：拖入即点火，不再是预览");
        Assert.That(drag.Action!.InstanceId, Is.EqualTo(ground.InstanceId), "拖入即绑定该实例");
        var p = E.Preview(s, drag.Action!);
        Assert.That(p.Notes, Does.Contain("将消耗1木材，提供60分钟燃料"), "点火预览写明消耗木材1");
        Start(s, drag.Action!);
        Assert.That(s.Main!.LockedItemIds, Does.Contain(ground.InstanceId));
        Run(s);
        Assert.That(s.Locations["camp"].Facility.FuelMinutes, Is.EqualTo(55), "0燃料自动投1木材，点火后剩55");
        Assert.That(E.ProcessOf(s, root)!.HeatDoneMinutes, Is.EqualTo(0), "点火这5分钟不算受热");
        WaitMinutes(s, 5);
        Assert.That(E.ProcessOf(s, root)!.HeatDoneMinutes, Is.EqualTo(5));
        // 燃料1—4拒绝点火，与按钮一致
        var t = New();
        t.Locations["camp"].Facility.FuelMinutes = 3;
        Assert.That(E.CanStart(t, ActionRequest.Ignite(First(t, "lighter").InstanceId)), Is.EqualTo("请先投入木材"));
    }

    [Test]
    public void P420_EatBothProducts_ImmediateEffectsThenFiveMinutesWait()
    {
        foreach (var (id, sat, hyd, mor) in new[] { ("cooked_root", 25.0, -2.0, 2.0), ("boiled_root", 23.0, 5.0, 3.0) })
        {
            var s = New();
            SetStats(s, 100, 50, 50, 90, 90, 50);
            E.DebugGive(s, id, 1);
            var preview = E.Preview(s, ActionRequest.UseItem(id));
            Assert.That(preview.Ok, Is.True, preview.Reason);
            Start(s, ActionRequest.UseItem(id));
            Assert.That(s.Log.Last().Text, Does.Contain(GameEngine.UseEffectText(E.Item(id).Use!)));
            Run(s);
            var wait = Cfg.HourlyRates["wait"];
            Assert.That(s.Stats.Satiety, Is.EqualTo(50 + sat + wait.Satiety * 5 / 60).Within(1e-9), id);
            Assert.That(s.Stats.Hydration, Is.EqualTo(50 + hyd + wait.Hydration * 5 / 60).Within(1e-9), id);
            Assert.That(s.Stats.Morale, Is.EqualTo(50 + mor + wait.Morale * 5 / 60).Within(1e-9), id);
            Assert.That(s.Stats.Health, Is.EqualTo(20), "健康不直接变化");
            Assert.That(preview.StatsAfter.Hydration, Is.EqualTo(s.Stats.Hydration).Within(1e-9), "预览与实际一致");
        }
        Assert.That(GameEngine.UseEffectText(Cfg.Items.Single(i => i.Id == "cooked_root").Use!), Is.EqualTo("饱食+25，水分−2，精神+2"));
        Assert.That(GameEngine.UseEffectText(Cfg.Items.Single(i => i.Id == "boiled_root").Use!), Is.EqualTo("饱食+23，水分+5，精神+3"));
    }

    [Test]
    public void P421_GoalCookedTwo_FacilityHasOneOfEach_CountedOnce()
    {
        var s = New();
        LightFire(s, 240);
        var pot = Pot(s);
        FillWater(s, pot, "clean_water", 1);
        E.DebugGive(s, "clean_water", 3, "camp");
        Ok(s, InstantRequest.PutInFire(pot.InstanceId));
        PutRootsInPot(s, pot, 1);
        RootInFire(s);
        Assert.That(s.Progress.Flags, Does.Not.Contain("supplyReady"));
        WaitMinutes(s, 30);
        Assert.That(Count(s, "cooked_root", Fire), Is.EqualTo(1));
        Assert.That(Count(s, "boiled_root", PotOwner(pot)), Is.EqualTo(1));
        Assert.That(s.Progress.Flags, Does.Contain("supplyReady"), "按有效成品合计2，不要求都移入背包");

        var t = New();
        E.DebugGive(t, "clean_water", 3, "camp");
        E.DebugGive(t, "cooked_root", 1, "camp");
        E.UpdateFlags(t);
        Assert.That(t.Progress.Flags, Does.Not.Contain("supplyReady"), "只有1份不达标");
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
