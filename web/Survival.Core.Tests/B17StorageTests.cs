using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 B17（R54）：安全屋固定柜（S09 X2.6、S06 X4）——owner safehouseStorage:&lt;id&gt;、每屋 200000g、不限五卡、0 分钟存取、
/// 只在该屋屋内、屋外只看上次记录、不保鲜、两屋不联网。验收 SH05—SH07、SH13。
/// </summary>
public class B17StorageTests : TestBase
{
    private void Ok(ActionResult r) => Assert.That(r.Ok, Is.True, r.Reason);

    private GameState Built(string loc, GameState? s = null)
    {
        s ??= New();
        if (s.CurrentLocation != loc) E.DebugTeleport(s, loc);
        E.DebugSetProgress(s, loc, 100);
        s.Progress.Flags.Add("hub_registered");
        s.Skills["crafting"].Level = Math.Max(1, s.Skills["crafting"].Level);
        s.WorldEnemies.RemoveAll(w => w.LocationId == loc);
        foreach (var (id, n) in new[] { ("wood", 8), ("fiber", 6), ("cloth", 2), ("stone", 4) }) E.CreateItems(s, id, n, s.Ground);
        Start(s, ActionRequest.Craft("safehouse_base", null, null, 1));
        Run(s);
        Assert.That(E.SafehouseBuilt(s, loc), Is.True);
        return s;
    }

    private static int Sum(GameState s, string itemId, string owner) => s.Items.Where(i => i.ItemId == itemId && i.Owner == owner).Sum(i => i.Count);

    [Test]
    public void SH05_Capacity200kg_NoFiveCardLimit_WholeRefusal()
    {
        var s = Built("camp");
        Ok(E.EnterSafehouse(s));
        var cab = E.CabinetOwnerOf(s, "camp")!;
        Assert.That(cab, Is.EqualTo("safehouseStorage:sh_camp"));
        // 400 份石块 = 200.00kg：放满（远超五卡）
        s.Items.RemoveAll(i => i.Owner == s.Ground);
        E.CreateItems(s, "stone", 400, s.Ground);
        foreach (var batch in s.Items.Where(i => i.Owner == s.Ground && i.ItemId == "stone").ToList())
            Ok(E.DoInstant(s, InstantRequest.StoreCabinet(batch.InstanceId, batch.Count)));
        Assert.That(E.CabinetGrams(s, "camp"), Is.EqualTo(200000));
        Assert.That(E.CabinetItems(s, "camp").Count, Is.GreaterThan(5));
        var extra = E.CreateItems(s, "wood", 1, ItemInstance.Carried).Single();
        var r = E.DoInstant(s, InstantRequest.StoreCabinet(extra.InstanceId, 1));
        Assert.That(r.Ok, Is.False);
        Assert.That(r.Reason, Does.Contain("柜子已满"));
        Assert.That(extra.Owner, Is.EqualTo(ItemInstance.Carried), "失败不改 owner");

        // 角色 29.9kg：取 1 份石块（0.5kg）超 30kg 整次拒绝，不自动塞给队友
        s.Items.RemoveAll(i => i.IsCarried);
        E.CreateItems(s, "scrap", 20, ItemInstance.Carried);
        E.CreateItems(s, "iron_plate", 20, ItemInstance.Carried);
        E.CreateItems(s, "wood", 19, ItemInstance.Carried);
        E.CreateItems(s, "berry", 2, ItemInstance.Carried);   // 10+10+9.5+0.4 = 29.9kg，4 张卡
        Assert.That(E.CarriedGrams(s), Is.EqualTo(29900));
        var st = E.CabinetItems(s, "camp").First(i => i.ItemId == "stone");
        var take = E.DoInstant(s, InstantRequest.TakeCabinet(st.InstanceId, 1));
        Assert.That(take.Ok, Is.False);
        Assert.That(take.Reason, Does.Contain("30公斤"));
        Assert.That(st.Owner, Is.EqualTo(cab));
        // 取到屋内地面不受携重限制
        Ok(E.DoInstant(s, InstantRequest.TakeCabinet(st.InstanceId, 1, s.Ground)));
        Assert.That(Sum(s, "stone", s.Ground), Is.EqualTo(1));
        // 五卡满：取一种新物品整次拒绝
        s.Items.RemoveAll(i => i.IsCarried);
        foreach (var id in new[] { "wood", "fiber", "cloth", "berry", "scrap" }) E.CreateItems(s, id, 1, ItemInstance.Carried);
        var take5 = E.DoInstant(s, InstantRequest.TakeCabinet(E.CabinetItems(s, "camp").First().InstanceId, 1));
        Assert.That(take5.Ok, Is.False);
        Assert.That(take5.Reason, Does.Contain("5"));
    }

    [Test]
    public void SH06_CampAndTwoHouses_Independent_NoNetwork_RemoteReadOnly()
    {
        var s = Built("camp");
        Ok(E.EnterSafehouse(s));
        var meat = E.CreateItems(s, "stone", 3, ItemInstance.Carried).Single();
        Ok(E.DoInstant(s, InstantRequest.StoreCabinet(meat.InstanceId, 3)));
        Ok(E.ExitSafehouse(s));
        Built("w13_mill", s);
        Assert.That(E.BuiltSafehouses(s), Is.EqualTo(new[] { "camp", "w13_mill" }));
        Ok(E.EnterSafehouse(s));
        Assert.That(E.CabinetItems(s, "w13_mill"), Is.Empty, "不同屋柜不是同一网络仓库");
        Assert.That(E.CabinetItems(s, "camp").Sum(i => i.Count), Is.EqualTo(3));
        // 远处只能看上次记录，不能取放
        var (lines, minute) = E.CabinetRecord(s, "camp");
        Assert.That(lines.Single().Count, Is.EqualTo(3));
        Assert.That(minute, Is.Not.Null);
        var r = E.DoInstant(s, InstantRequest.TakeCabinet(E.CabinetItems(s, "camp")[0].InstanceId, 1));
        Assert.That(r.Ok, Is.False, "别处屋柜不能远程取");
        // 远处柜子不算可用材料
        Assert.That(E.AvailableCount(s, "stone", true), Is.EqualTo(0));
        // 旧营地外部火堆与地面仍在，未搬迁
        Assert.That(s.Locations["camp"].Facility.FacilityId, Is.EqualTo("camp:fire"));
        Ok(E.ExitSafehouse(s));
        Assert.That(E.DoInstant(s, InstantRequest.StoreCabinet(E.CreateItems(s, "wood", 1, ItemInstance.Carried).Single().InstanceId, 1)).Reason,
            Does.Contain("屋内"));
    }

    [Test]
    public void SH07_CabinetDoesNotPreserve_ExpiryUnchanged_SaveLoad()
    {
        var s = Built("camp");
        Ok(E.EnterSafehouse(s));
        var meat = E.CreateItems(s, "raw_meat", 1, s.Ground).Single();
        var exp = meat.ExpiresAt;
        Ok(E.DoInstant(s, InstantRequest.StoreCabinet(meat.InstanceId, 1)));
        var inCab = E.CabinetItems(s, "camp").Single();
        Assert.That(inCab.ExpiresAt, Is.EqualTo(exp), "搬入不刷新");
        // 读档也不刷新；世界时间流逝照样腐败
        var t = SaveSerializer.Deserialize(SaveSerializer.Serialize(s), Cfg);
        AdvanceTo(t, exp!.Value + 5);
        var spoiled = E.CabinetItems(t, "camp").Single();
        Assert.That(E.IsSpoiled(t, spoiled), Is.True);
        Assert.That(spoiled.ExpiresAt, Is.EqualTo(exp));
        Ok(E.DoInstant(t, InstantRequest.TakeCabinet(spoiled.InstanceId, 1)));
        Assert.That(E.IsSpoiled(t, E.FindItem(t, spoiled.InstanceId)!), Is.True, "坏料取出不复活");
    }

    [Test]
    public void SH13_DungeonResetDoesNotTouchHouse()
    {
        var s = Built("camp");
        Ok(E.EnterSafehouse(s));
        var w = E.CreateItems(s, "wood", 2, ItemInstance.Carried).Single();
        Ok(E.DoInstant(s, InstantRequest.StoreCabinet(w.InstanceId, 2)));
        var before = SaveSerializer.Serialize(s);
        foreach (var d in s.DungeonInstances.Keys.ToList()) s.DungeonInstances[d].Generation++;
        Assert.That(E.CabinetItems(s, "camp").Sum(i => i.Count), Is.EqualTo(2));
        Assert.That(s.Locations["camp"].Safehouse!.Built, Is.True);
        Assert.That(before, Does.Contain("safehouseStorage:sh_camp"));
    }
}
