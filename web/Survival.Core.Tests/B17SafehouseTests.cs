using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 B17（R52/R53）：安全屋许可、选址、建造事务、门与屋内区（S09 X2.1—X2.8、S13、A08 白名单 54 地点、Q107/Q118/Q134）。
/// 验收 SH01—SH04、SH08—SH12、SH15、COMP07、WS-Q107。
/// </summary>
public class B17SafehouseTests : TestBase
{
    private static ActionRequest Craft(string id) => ActionRequest.Craft(id, null, null, 1);

    private void Ok(ActionResult r) => Assert.That(r.Ok, Is.True, r.Reason);

    /// <summary>合格的营地选址：已登记、探索 100、制造 1、当地地面放够一间基础屋的材料。</summary>
    private GameState Site(string loc = "camp", bool materials = true)
    {
        var s = New();
        if (loc != s.CurrentLocation) E.DebugTeleport(s, loc);
        E.DebugSetProgress(s, loc, 100);
        s.Progress.Flags.Add("hub_registered");
        s.Skills["crafting"].Level = Math.Max(1, s.Skills["crafting"].Level);
        s.WorldEnemies.RemoveAll(w => w.LocationId == loc);
        if (materials) GiveBase(s);
        return s;
    }

    private void GiveBase(GameState s)
    {
        foreach (var (id, n) in new[] { ("wood", 8), ("fiber", 6), ("cloth", 2), ("stone", 4) }) E.CreateItems(s, id, n, s.Ground);
    }

    private GameState Built(string loc = "camp")
    {
        var s = Site(loc);
        Start(s, Craft("safehouse_base"));
        Run(s);
        Assert.That(E.SafehouseBuilt(s, loc), Is.True);
        return s;
    }

    private static int Total(GameState s, string itemId) => s.Items.Where(i => i.ItemId == itemId).Sum(i => i.Count);

    // ───────────── SH01 许可与选址 ─────────────

    [Test]
    public void SH01_COMP07_PermissionOnlyExplicitWhitelist_ZeroCostRefusals()
    {
        // A08 白名单 54 处：只有 overworld、合法土地且显式 buildAllowed.safehouse
        var allowed = Cfg.Locations.Where(l => E.SafehousePermissionBlocked(l.Id) == null).Select(l => l.Id).ToList();
        Assert.That(allowed, Has.Count.EqualTo(54));
        Assert.That(allowed, Does.Contain("camp").And.Contain("h05_homestead").And.Contain("w13_mill"));
        Assert.That(Cfg.Locations.Where(l => l.BuildAllowedSafehouse).All(l => l.WorldKind == "overworld" && l.PrivateBuildForbidden == null), Is.True);
        Assert.That(E.SafehousePermissionBlocked("d05_01"), Is.EqualTo("地牢内不可建设私人住宅"));
        Assert.That(E.SafehousePermissionBlocked("h02_square"), Is.EqualTo("势力/公共土地不可建设私人住宅"));

        // 未到访 / 探索不足 / 未登记 / 有敌踪：逐条列出、0 扣料 0 时间
        var s = New();
        E.DebugTeleport(s, "w13_mill");
        s.Locations["w13_mill"].Progress = 40;
        E.DebugSpawnEnemy(s, "shambler", "w13_mill");
        GiveBase(s);
        var reasons = E.SafehouseSiteReasons(s, "w13_mill");
        Assert.That(reasons, Has.Some.Contains("探索到100%"));
        Assert.That(reasons, Has.Some.Contains("营区登记"));
        Assert.That(reasons, Has.Some.Contains("敌踪"));
        var t0 = s.GameMinute;
        var n0 = s.Items.Count;
        var r = E.Start(s, Craft("safehouse_base"));
        Assert.That(r.Ok, Is.False);
        Assert.That((s.GameMinute, s.Items.Count, s.Main), Is.EqualTo((t0, n0, (ActionState?)null)));
        Assert.That(E.SafehouseSiteReasons(s, "w14_cemeteryroad"), Has.Some.Contains("实际到访"), "仅发现的远处地点不能远程建");

        // 公共土地/地牢：住宅入口写实际禁建原因（Q134）
        E.DebugTeleport(s, "c02_church");
        Assert.That(E.CanStart(s, Craft("safehouse_base")), Is.EqualTo("势力/公共土地不可建设私人住宅"));
    }

    [Test]
    public void SH02_Homestead_RegisteredFirstHouse()
    {
        var hs = Cfg.Locations.Single(l => l.Id == "h05_homestead");
        Assert.That((hs.BuildAllowedSafehouse, hs.SafeExterior, hs.LandOwner, hs.WorldKind), Is.EqualTo((true, true, "unclaimed", "overworld")));
        var v = New();
        E.DebugTeleport(v, "h05_homestead");
        Assert.That(v.Locations["h05_homestead"].Progress, Is.EqualTo(100), "营外地块到访即探索 100（确定的新手建造入口）");
        Assert.That(E.SafehouseSiteReasons(v, "h05_homestead"), Is.EqualTo(new[] { "要先在幸存者营区登记（解锁安全屋建造）" }));
        var s = Site("h05_homestead");
        Assert.That(E.SafehouseSiteReasons(s, "h05_homestead"), Is.Empty);
        Assert.That(E.CanStart(s, Craft("safehouse_base")), Is.Null);
        // 四类材料都是普通物资，没有高级工坊/随机地契前置
        Assert.That(E.SafehouseRecipe.Inputs, Is.EqualTo(new Dictionary<string, int> { ["wood"] = 8, ["fiber"] = 6, ["cloth"] = 2, ["stone"] = 4 }));
        Assert.That((E.SafehouseRecipe.BaseMinutes, E.SafehouseRecipe.MinSkills["crafting"], E.SafehouseRecipe.Station), Is.EqualTo((240, 1, "hand")));
    }

    // ───────────── SH03 建造事务 ─────────────

    [Test]
    public void SH03_BuildOnce_StatusTransitions_RepeatRefused()
    {
        var s = Site();
        Assert.That(E.SafehouseStatus(s, "camp"), Is.EqualTo(GameEngine.SafehouseNotStarted));
        Assert.That(E.CanStart(s, Craft("safehouse_bed")), Does.Contain("屋内"), "未完工不能铺屋内床");
        var xp0 = s.Skills["crafting"].Xp;
        Start(s, Craft("safehouse_base"));
        Assert.That(E.SafehouseStatus(s, "camp"), Is.EqualTo(GameEngine.SafehouseBuildingStatus));
        Assert.That(E.SafehouseDoorBlocked(s, true), Does.Contain("施工"), "施工中没有门/安全/柜");
        Assert.That(E.SafehouseSiteReasons(s, "camp"), Has.Some.Contains("正在施工"));
        Run(s);
        Assert.That(E.SafehouseStatus(s, "camp"), Is.EqualTo(GameEngine.SafehouseBuiltStatus));
        var sh = s.Locations["camp"].Safehouse!;
        Assert.That((sh.Id, sh.Built), Is.EqualTo(("sh_camp", true)));
        Assert.That(Total(s, "wood") + Total(s, "stone"), Is.EqualTo(Total(New(), "wood") + Total(New(), "stone")), "材料按配方真实核销");
        Assert.That(s.Skills["crafting"].Xp, Is.GreaterThan(xp0), "完工记一次经验");
        Assert.That(s.Log.Last().Text, Does.Contain("安全屋建成"));

        // 再建：不扣料、不刷经验
        GiveBase(s);
        var xp1 = s.Skills["crafting"].Xp;
        var n = s.Items.Count;
        Assert.That(E.CanStart(s, Craft("safehouse_base")), Does.Contain("已有安全屋"));
        Assert.That((s.Items.Count, s.Skills["crafting"].Xp), Is.EqualTo((n, xp1)));
        // 旧营地设施不变（camp 外部棚/床/火堆与屋内是不同区）
        Assert.That(s.Locations["camp"].Facility.FacilityId, Is.EqualTo("camp:fire"));
    }

    [Test]
    public void SH04_PauseCancelTravelSaveLoad()
    {
        var s = Site();
        Start(s, Craft("safehouse_base"));
        Steps(s, 6);
        Assert.That(s.Main!.ReservedItemIds, Is.Not.Empty);
        var reserved = s.Main.ReservedItemIds.ToList();
        // 不能带着预留去别处续工
        Assert.That(E.CanStart(s, ActionRequest.Travel("field")), Is.Not.Null);
        // 读档后照常从剩余工作量继续，只完成一次
        var json = SaveSerializer.Serialize(s);
        var t = SaveSerializer.Deserialize(json, Cfg);
        Assert.That(t.Main!.ReservedItemIds, Is.EqualTo(reserved));
        Run(t);
        Assert.That(E.SafehouseBuilt(t, "camp"), Is.True);
        Assert.That(t.Log.Count(l => l.Text.Contains("安全屋建成")), Is.EqualTo(1));

        // 取消：材料全部回到当前外部地面、进度归零、无半屋、无经验
        var xp = s.Skills["crafting"].Xp;
        Ok(E.Cancel(s));
        Assert.That(E.SafehouseStatus(s, "camp"), Is.EqualTo(GameEngine.SafehouseNotStarted));
        Assert.That(s.Locations["camp"].Safehouse, Is.Null);
        foreach (var (id, n) in new[] { ("wood", 8), ("fiber", 6), ("cloth", 2), ("stone", 4) })
            Assert.That(s.Items.Where(i => i.ItemId == id && i.Owner == "camp" && i.ReservedBy == null).Sum(i => i.Count), Is.GreaterThanOrEqualTo(n), id);
        Assert.That(s.Items.Any(i => i.ReservedBy != null), Is.False);
        Assert.That(s.Skills["crafting"].Xp, Is.EqualTo(xp));
        // 重新开工重新计算
        Assert.That(E.CanStart(s, Craft("safehouse_base")), Is.Null);
    }

    // ───────────── SH08 门 ─────────────

    [Test]
    public void SH08_Door_ZeroMinutes_BlockedInCombatEncounterAction()
    {
        var s = Built();
        var t0 = s.GameMinute;
        Ok(E.EnterSafehouse(s));
        Assert.That((s.CurrentZone, s.GameMinute, s.Ground), Is.EqualTo((GameState.SafehouseZone, t0, "safehouseFloor:sh_camp")));
        Assert.That(E.CanStart(s, ActionRequest.Travel("field")), Is.EqualTo("在安全屋屋内：先出门再离开"));
        Assert.That(E.CanStart(s, ActionRequest.Explore()), Does.Contain("先出门"));
        Ok(E.ExitSafehouse(s));
        Assert.That((s.CurrentZone, s.GameMinute), Is.EqualTo((GameState.ExteriorZone, t0)));

        // 敌踪待处理 / 行动中：不能用门跳过
        s.Encounter = new PendingEncounter { LocationId = "camp", EnemyIds = new List<int>(), Source = "explore", Minute = s.GameMinute };
        Assert.That(E.EnterSafehouse(s).Reason, Does.Contain("敌踪待处理"));
        s.Encounter = null;
        Start(s, ActionRequest.Rest(60));
        Assert.That(E.EnterSafehouse(s).Reason, Does.Contain("行动进行中"));
    }

    // ───────────── SH09/SH10 屋内休息 ─────────────

    [Test]
    public void SH09_SH10_InsideRest_ShelterMoraleBedEnergy_LowMoraleHalved()
    {
        var s = Built("w13_mill");
        var rest = new ActionState { Type = ActionType.Rest };
        var sleep = new ActionState { Type = ActionType.Sleep };
        SetStats(s, 15, 80, 80, 10, 50, 60);
        var outRest = E.ComputeSegment(s, rest, s.Player);
        var outSheltered = E.IsSheltered(s, s.Ground);
        Ok(E.EnterSafehouse(s));
        Assert.That(E.IsSheltered(s, s.Ground), Is.True);
        var inRest = E.ComputeSegment(s, rest, s.Player);
        Assert.That(inRest.Rates.Morale - outRest.Rates.Morale, Is.EqualTo(Cfg.FacilityEffects.ShelteredCampRestMoralePerHour).Within(1e-9), "屋内休息 M+2");
        var noBed = E.ComputeSegment(s, sleep, s.Player).Rates.Energy;
        // 屋内床垫
        foreach (var (id, n) in new[] { ("fiber", 4), ("cloth", 2) }) E.CreateItems(s, id, n, s.Ground);
        Start(s, Craft("safehouse_bed"));
        Run(s);
        Assert.That(s.Locations["w13_mill"].Safehouse!.Interior.BedBuilt, Is.True);
        Assert.That(s.Locations["w13_mill"].Facility.BedBuilt, Is.False, "床铺在屋内区，不是外部");
        SetStats(s, 15, 80, 80, 10, 50, 60);
        var bed = E.ComputeSegment(s, sleep, s.Player).Rates.Energy;
        Assert.That(bed - noBed, Is.EqualTo(Cfg.FacilityEffects.BedExtraSleepEnergyPerHour).Within(1e-9), "床垫睡眠 E+1");
        SetStats(s, 15, 80, 80, 10, 50, 10);
        var low = E.ComputeSegment(s, sleep, s.Player).Rates.Energy;
        Assert.That(low, Is.EqualTo(bed * Cfg.FacilityEffects.LowMoraleEnergyRecoveryFactor).Within(1e-9), "低精神正 E 整体折半");
        // 出门后不再有屋内效果
        Ok(E.ExitSafehouse(s));
        Assert.That(E.IsSheltered(s, s.Ground), Is.EqualTo(outSheltered));
        Assert.That(E.ComputeSegment(s, rest, s.Player).Rates.Morale, Is.EqualTo(outRest.Rates.Morale).Within(1e-9));
    }

    [Test]
    public void SH09_InsideTimeStillPasses_NoFreeUpkeep()
    {
        var s = Built("w13_mill");
        Ok(E.EnterSafehouse(s));
        var n0 = s.Stats.Satiety;
        Start(s, ActionRequest.Rest(60));
        Run(s);
        Assert.That(s.Stats.Satiety, Is.LessThan(n0), "屋内休息仍消耗饱食");
        Assert.That(s.Encounter, Is.Null);
    }

    // ───────────── SH11 屋内火堆（zone 内唯一、不同 facilityId） ─────────────

    [Test]
    public void SH11_InteriorFire_SeparateFacility_OnlyCurrentZone()
    {
        var s = Built();
        Ok(E.EnterSafehouse(s));
        Assert.That(E.HasFireAt(s, s.Ground), Is.False);
        Assert.That(E.CanStart(s, ActionRequest.Ignite()), Is.EqualTo("这里没有火堆"), "外部营地火堆在屋内不可操作");
        foreach (var (id, n) in new[] { ("stone", 4), ("wood", 2) }) E.CreateItems(s, id, n, s.Ground);
        Start(s, Craft("safehouse_fire"));
        Run(s);
        Assert.That(E.HasFireAt(s, s.Ground), Is.True);
        var fac = s.Locations["camp"].Safehouse!.Interior;
        Assert.That((fac.FacilityId, fac.FireLit, fac.FuelMinutes), Is.EqualTo(("camp:safehouse:fire", false, 0)));
        Assert.That(GameEngine.FireIdOf("camp", GameState.SafehouseZone), Is.EqualTo("camp:safehouse:fire"));
        Assert.That(E.FacilityLocation("camp:safehouse:fire"), Is.EqualTo("safehouseFloor:sh_camp"));
        Assert.That(E.CanStart(s, Craft("safehouse_fire")), Does.Contain("每区一处"));

        // 屋内放锅：锅 owner 是屋内火堆；外部火堆不受影响
        var pot = E.CreateItems(s, "pot", 1, s.Ground).Single();
        Ok(E.DoInstant(s, InstantRequest.PutInFire(pot.InstanceId)));
        Assert.That(pot.Owner, Is.EqualTo("facility:camp:safehouse:fire"));
        Assert.That(E.FireContents(s, "camp"), Has.None.Matches<ItemInstance>(i => i.InstanceId == pot.InstanceId));
        // 出门后外部区看不到屋内火堆，也不能取锅
        Ok(E.ExitSafehouse(s));
        Assert.That(E.DoInstant(s, InstantRequest.TakeOut(pot.InstanceId, "camp")).Ok, Is.False);
        // 两处火堆同一时钟：屋内火熄灭不影响外部
        Assert.That(E.FirePlaces(s), Is.EquivalentTo(new[] { "camp", "safehouseFloor:sh_camp" }));
    }

    // ───────────── WS-Q107 屋内工坊 ─────────────

    [Test]
    public void WS_Q107_InteriorWorkshop_PerZoneUnique_NotRemote()
    {
        var s = Built("w13_mill");
        s.Skills["crafting"].Level = 3;
        foreach (var (id, n) in new[] { ("plank", 4), ("fastener", 4), ("parts", 2) }) E.CreateItems(s, id, n, s.Ground);
        Assert.That(E.CanStart(s, Craft("build_workbench")), Is.EqualTo(GameEngine.WorkshopBuildOnly), "屋外（非营地）不能建");
        Ok(E.EnterSafehouse(s));
        foreach (var (id, n) in new[] { ("plank", 4), ("fastener", 4), ("parts", 2) }) E.CreateItems(s, id, n, s.Ground);
        Start(s, Craft("build_workbench"));
        Run(s);
        Assert.That(s.Locations["w13_mill"].Safehouse!.Interior.Workshops, Does.Contain("workbench"));
        Assert.That(E.StationUsableHere(s, "workbench"), Is.True);
        Assert.That(E.CanStart(s, Craft("build_workbench")), Does.Contain("每区每类一座"));
        Ok(E.ExitSafehouse(s));
        Assert.That(E.StationUsableHere(s, "workbench"), Is.False, "屋外不能远程用屋内工坊");

        // camp 外部与 camp 屋内各一座
        var c = Built("camp");
        E.DebugBuildAllWorkshops(c);
        Ok(E.EnterSafehouse(c));
        Assert.That(E.StationUsableHere(c, "workbench"), Is.False, "外部工坊不搬进屋");
        Assert.That(E.CanBuildWorkshopsHere(c), Is.True);
    }

    // ───────────── SH12 候补不进安全屋 ─────────────

    [Test]
    public void SH12_ReserveCareStaysAtHub()
    {
        var s = Built("w13_mill");
        Ok(E.EnterSafehouse(s));
        Assert.That(E.ReserveFacility(s), Is.EqualTo(E.ReserveFacility(New())), "候补设施只读营区，安全屋不远程生效");
    }

    // ───────────── SH15 存档迁移 ─────────────

    [Test]
    public void SH15_Migration28To29_CampFireAndProcessesKept_NoFreeBuildings()
    {
        var s = New();
        var fac = s.Locations["camp"].Facility;
        var pot = E.CreateItems(s, "raw_root", 1, "camp").Single();
        Ok(E.DoInstant(s, InstantRequest.PutInFire(pot.InstanceId)));
        fac.FireLit = true;
        fac.FuelMinutes = 120;
        Start(s, ActionRequest.Rest(60));
        Steps(s, 2);
        E.Cancel(s);
        var proc = fac.Processes.Single();
        Assert.That(proc.HeatDoneMinutes, Is.GreaterThan(0));
        s.Progress.Flags.Add("hub_registered");

        var json = SaveSerializer.Serialize(s).Replace($"\"SchemaVersion\":\"{GameState.CurrentSchema}\"", "\"SchemaVersion\":\"28\"")
            .Replace("\"CurrentZone\":\"exterior\",", "").Replace("\"Safehouse\":null,", "");
        var m = SaveSerializer.Deserialize(json, Cfg);
        Assert.That(m.SchemaVersion, Is.EqualTo(GameState.CurrentSchema));
        Assert.That(m.CurrentZone, Is.EqualTo(GameState.ExteriorZone));
        Assert.That(m.Locations.Values.All(l => l.Safehouse == null), Is.True, "旧档不补安全屋/柜");
        var mf = m.Locations["camp"].Facility;
        Assert.That(mf.FacilityId, Is.EqualTo("camp:fire"));
        var mp = mf.Processes.Single();
        Assert.That((mp.ProcessId, mp.HeatDoneMinutes, mp.TargetInstanceId), Is.EqualTo((proc.ProcessId, proc.HeatDoneMinutes, proc.TargetInstanceId)));
        Assert.That(E.FindItem(m, pot.InstanceId)!.Owner, Is.EqualTo("facility:camp:fire"));
        Assert.That(m.Items.Count, Is.EqualTo(s.Items.Count));
        Assert.That(m.Log.Count(l => l.Text.Contains("版本42")), Is.EqualTo(1));
        // 登记只解锁资格，不赠建筑
        Assert.That(E.SafehouseStatus(m, "camp"), Is.EqualTo(GameEngine.SafehouseNotStarted));
        // 幂等：再读一次不再写日志
        var again = SaveSerializer.Deserialize(SaveSerializer.Serialize(m), Cfg);
        Assert.That(again.Log.Count(l => l.Text.Contains("版本42")), Is.EqualTo(1));
    }
    // ───────────── SH14 收藏、改名与返程定位 ─────────────

    [Test]
    public void SH14_FavoriteRenameRoute_NoTeleportNoFreeTime()
    {
        var s = Built("camp");
        Assert.That(E.SetFavoriteSafehouse(s, "field").Ok, Is.False, "未建成不能收藏");
        Ok(E.SetFavoriteSafehouse(s, "camp"));
        Ok(E.RenameSafehouse(s, "camp", "老窝"));
        Assert.That(E.SafehouseName(s, "camp"), Is.EqualTo("老窝"));
        Assert.That(E.SafehouseRouteText(s, "camp"), Does.Contain("就在当前地点"));
        var start = s.CurrentLocation;
        // 去别处后：只给路线与预计时间，不移动、不花时间
        var next = E.AdjacentUnlocked(s).First().Other("camp");
        Start(s, ActionRequest.Travel(next));
        Run(s);
        var t0 = s.GameMinute;
        var edges = s.UnlockedEdges.Count;
        var route = E.SafehouseRoute(s, "camp")!;
        Assert.That(route.Last().To, Is.EqualTo("camp"));
        Assert.That(E.SafehouseRouteText(s, "camp"), Does.Contain("老窝").And.Contain("不会自动寻路或传送"));
        Assert.That((s.GameMinute, s.CurrentLocation, s.UnlockedEdges.Count), Is.EqualTo((t0, next, edges)));
        var node = E.BuildMapView(s).Nodes.Single(n => n.Id == "camp");
        Assert.That(node.Roles, Does.Contain("常用安全屋"));
        // 远处不能存取，也没有门
        Assert.That(E.SafehouseDoorBlocked(s, true), Is.Not.Null);
        Ok(E.SetFavoriteSafehouse(s, null));
        Assert.That(s.FavoriteSafehouse, Is.Null);
        Assert.That(start, Is.EqualTo("camp"));
    }
    // ───────────── 试玩修复 M362 ─────────────

    [Test]
    public void M362_ReasonsListedAll_RegistrationFirst_BuildCancelText_DisplayName()
    {
        var s = New();
        E.DebugTeleport(s, "w13_mill");
        s.Locations["w13_mill"].Progress = 40;
        E.DebugSpawnEnemy(s, "shambler", "w13_mill");
        var reasons = E.SafehouseSiteReasons(s, "w13_mill");
        Assert.That(reasons[0], Does.Contain("营区登记"), "未登记时登记排第一（S09 X2.1）");
        var why = E.CanStart(s, Craft("safehouse_base"))!;
        foreach (var r in reasons) Assert.That(why, Does.Contain(r), "开工原因一次列全");

        var b = Site();
        Start(b, Craft("safehouse_base"));
        Steps(b, 2);
        Ok(E.Cancel(b));
        Assert.That(b.Log.Last().Text, Does.StartWith("已取消建造"));

        var h = Built("camp");
        Ok(E.RenameSafehouse(h, "camp", "老窝"));
        Ok(E.EnterSafehouse(h));
        Assert.That(E.ZoneName(h, h.Ground), Is.EqualTo("老窝"));
        Assert.That(E.LocName(h.Ground), Is.EqualTo("临时营地安全屋"), "无状态的旧名接口不变");
    }
}
