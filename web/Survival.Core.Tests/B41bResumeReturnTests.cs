using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// B41b 里程碑 6：继续前的完整只读重验与分来源返还（开关开；R05「B41b Q463—Q471」Q465、Q466、Q471，「Q472—Q476」Q475、Q476；
/// 逐字稿「B41b Q463—Q471 续接与返还逐字稿」；验收 Q440-A01 返还部分、Q440-A02、Q465-A01—A03、Q466-A01—A02、Q471-A01、Q475-A01、Q476-A01）。
/// </summary>
public class B41bResumeReturnTests : B41bTestBase
{
    private string Field => E.LocName("field");

    /// <summary>撤退保留后走回荒地（原工作恢复为暂停）。</summary>
    private void BackToField(GameState s, int id)
    {
        TravelTo(s, "field");
        Assert.That(s.Main!.ActionId, Is.EqualTo(id));
        Assert.That(s.Main.Status, Is.EqualTo(ActionStatus.Paused));
    }

    /// <summary>重验失败：给定原因，且状态、预留、原暂停原因都不变。</summary>
    private void AssertBlocked(GameState s, string why)
    {
        var before = SaveSerializer.Serialize(s);
        Assert.That(E.ResumeBlockReason(s), Is.EqualTo(why));
        var r = E.Resume(s);
        Assert.That(r.Ok, Is.False);
        Assert.That(r.Reason, Is.EqualTo(why));
        Assert.That(SaveSerializer.Serialize(s), Is.EqualTo(before), "失败不推进时间、不扣物、不改预留与暂停原因");
    }

    [Test]
    public void Q466_Q471_ExitOffSite_CarriedStaysCarried_GroundBackToOrigin_Once()
    {
        var s = Field(3);
        var id = KeptCraft(s);
        var stone = Reserved(s, "stone", id);
        var fiber = Reserved(s, "fiber", id);
        var campFiber = s.Items.Where(i => i.ItemId == "fiber" && i.Owner == "camp").Sum(i => i.Count);
        var allFiber = s.Items.Where(i => i.ItemId == "fiber").Sum(i => i.Count);
        Assert.That(E.ActionProgress(s)!.ExitNote,
            Is.EqualTo($"退出将放弃本次制作。随身预留材料解除预留后留在随身，地面预留材料会返还{Field}地面。不会生成半成品。已耗时间不退。"));
        Assert.That(E.ActionProgress(s)!.WorkPlace, Is.EqualTo(Field));
        Assert.That(E.Cancel(s).Ok, Is.True);
        Assert.That(s.Log.Last().Text,
            Is.EqualTo($"已取消制作。随身预留材料已解除预留，仍在随身；地面预留材料已返还{Field}地面。未生成半成品。已耗时间保留。"));
        Assert.That(stone.ReservedBy, Is.Null);
        Assert.That(stone.Owner, Is.EqualTo(ItemInstance.Carried), "随身部分不被抛回远处");
        Assert.That(fiber.ReservedBy, Is.Null);
        Assert.That(fiber.Owner, Is.EqualTo("field"), "原地面材料回原行动地点，不落到当前营地");
        Assert.That(s.Items.Where(i => i.ItemId == "fiber" && i.Owner == "camp").Sum(i => i.Count), Is.EqualTo(campFiber));
        Assert.That(s.Items.Any(i => i.ItemId == "stone_knife"), Is.False, "没有半成品");
        Assert.That(E.Cancel(s).Ok, Is.False, "不能重复退出");
        Assert.That(s.Items.Where(i => i.ItemId == "fiber").Sum(i => i.Count), Is.EqualTo(allFiber), "不重复返还");
    }

    [Test]
    public void Q471_GroundOnly_And_CarriedOnly_Clauses()
    {
        var s = Field(3);
        KeptCraft(s, stoneCarried: false, fiberOnGround: true);
        Assert.That(E.Cancel(s).Ok, Is.True);
        Assert.That(s.Log.Last().Text, Is.EqualTo($"已取消制作。地面预留材料已返还{Field}地面。未生成半成品。已耗时间保留。"));

        var t = Field(3);
        var tid = KeptCraft(t, stoneCarried: true, fiberOnGround: false);
        var mine = new[] { Reserved(t, "stone", tid), Reserved(t, "fiber", tid) };
        Assert.That(E.ActionProgress(t)!.ExitNote, Is.EqualTo("退出将放弃本次制作。随身预留材料解除预留后留在随身。不会生成半成品。已耗时间不退。"));
        Assert.That(E.Cancel(t).Ok, Is.True);
        Assert.That(t.Log.Last().Text, Is.EqualTo("已取消制作。随身预留材料已解除预留，仍在随身。未生成半成品。已耗时间保留。"));
        Assert.That(mine.All(i => i.Owner == ItemInstance.Carried && i.ReservedBy == null), Is.True);
    }

    [Test]
    public void Q466_A02_SaveLoadKeepsSources_LegacyActionBackfilledFromRealOwner()
    {
        var s = Field(3);
        var id = KeptAtField(s, ActionRequest.Craft("knife"), g =>
        {
            E.DebugGive(g, "stone", 1);
            E.DebugGive(g, "fiber", 1, "field");
        });
        var t = SaveSerializer.Deserialize(SaveSerializer.Serialize(s), CfgV2);
        Assert.That(t.Main!.ReserveSources, Is.EquivalentTo(s.Main!.ReserveSources!));

        // 旧行动没有来源记录：采纳 Q440 时按仍在原处的真实归属补齐，不按队伍当前地点
        var u = Field(3);
        TravelTo(u, "camp");
        TravelTo(u, "field");
        E.DebugSpawnEnemy(u, "shambler", "field");
        E.DebugGive(u, "stone", 1);
        E.DebugGive(u, "fiber", 1, "field");
        Start(u, ActionRequest.Craft("knife"));
        var uf = Reserved(u, "fiber", u.Main!.ActionId);
        u.Main!.ReserveSources = null;
        u.Main.StartZone = null;
        SetLeft(u, GameEngine.SingleCardKey(u.WorldEnemies.Single().Id), 0);
        Step(u);
        RetreatOk(u);
        Assert.That(u.Main.RetreatKept, Is.True);
        Assert.That(u.Main.StartZone, Is.EqualTo(GameState.ExteriorZone));
        Assert.That(u.Main.ReserveSources!.Values, Is.EquivalentTo(new[] { ItemInstance.Carried, "field" }));
        Assert.That(E.Cancel(u).Ok, Is.True);
        Assert.That(uf.Owner, Is.EqualTo("field"));
        Assert.That(uf.ReservedBy, Is.Null);
    }

    [Test]
    public void Q465_A01_SpoiledReservationChangedTargetGone_BlockWithoutSideEffects()
    {
        var s = Field(3);
        var id = KeptCraft(s);
        BackToField(s, id);
        var fiber = Reserved(s, "fiber", id);

        fiber.ExpiresAt = s.GameMinute;
        AssertBlocked(s, GameEngine.ResumeSpoiled);
        fiber.ExpiresAt = null;

        fiber.ReservedBy = null;
        AssertBlocked(s, GameEngine.ResumeReserveChanged);
        fiber.ReservedBy = id;

        var recipe = s.Main!.TargetId;
        s.Main.TargetId = "no_such_recipe";
        AssertBlocked(s, GameEngine.ResumeTargetGone);
        s.Main.TargetId = recipe;

        Assert.That(E.ResumeBlockReason(s), Is.Null);
    }

    [Test]
    public void Q465_A02_OwnReservationNotReRequested_ContinuesSameProgress()
    {
        var s = Field(3);
        var id = KeptCraft(s);
        BackToField(s, id);
        var work = s.Main!.WorkDone;
        Assert.That(E.CanStart(s, ActionRequest.Craft("knife")), Is.Not.Null, "材料都在本行动预留里，新开工会缺料");
        Assert.That(E.Resume(s).Ok, Is.True, "自己的预留计入可用，不再要求同额材料");
        Assert.That(s.Main.WorkDone, Is.EqualTo(work));
        Assert.That(Run(s), Is.EqualTo(SegmentStatus.Completed));
        Assert.That(s.Items.Count(i => i.ItemId == "stone_knife"), Is.EqualTo(1));
    }

    [Test]
    public void Q465_A02_PaidClearChargeNotReRequested()
    {
        var s = Field(3);
        var id = KeptAtField(s, ActionRequest.Clear(), g => g.Locations["field"].ClearCharges = Math.Max(1, g.Locations["field"].ClearCharges));
        BackToField(s, id);
        s.Locations["field"].ClearCharges = 0;
        Assert.That(E.ResumeBlockReason(s), Is.Null, "已扣的清理次数视为已付");
    }

    [Test]
    public void Q465_Q491_SubRunning_GeneralReason_TravelSameText()
    {
        var s = Field(3);
        var id = KeptCraft(s);
        Start(s, ActionRequest.Rest(30));
        Assert.That(E.ResumeBlockReason(s), Is.EqualTo("当前行动还没结束。"));
        Assert.That(Run(s, autoResume: false), Is.EqualTo(SegmentStatus.Completed));
        Assert.That(E.ResumeBlockReason(s), Is.EqualTo($"请返回{Field}后再继续{s.Main!.Label}。"));

        Start(s, ActionRequest.Travel("field"));
        Step(s);
        s.Main!.Status = ActionStatus.Paused;
        s.Main.PauseReason = PauseReason.StatAlert;
        Start(s, ActionRequest.Rest(30));
        Assert.That(E.ResumeBlockReason(s), Is.EqualTo("当前行动还没结束。"), "Q491：移动有未结束子行动也统一用此句");
    }

    [Test]
    public void Q475_GatherToolGone_UsesGatherText_BrokenAndWornUseStartTexts()
    {
        var s = Field(3);
        ItemInstance? knife = null;
        var id = KeptAtField(s, ActionRequest.Gather("field_fiber"), g =>
        {
            var rs = g.Resources["field_fiber"];
            rs.Discovered = true;
            rs.Available = Math.Max(3, rs.Available ?? 0);
            knife = E.DebugCreateEquipment(g, "stone_knife", "white", ItemInstance.Carried);
        });
        Assert.That(s.Main!.ToolInstanceId, Is.EqualTo(knife!.InstanceId));
        BackToField(s, id);

        knife.Durability = 0;
        AssertBlocked(s, "这件工具已损坏，请重新选择");
        knife.Durability = 0.5;
        AssertBlocked(s, $"这件工具耐久不足（{GameEngine.DurabilityText(knife)}），请维护或重新选择");
        s.Items.Remove(knife);
        AssertBlocked(s, "这件工具已不在，请重新选择");
        Assert.That(s.Main!.ToolInstanceId, Is.EqualTo(knife.InstanceId), "不暗换工具");

        // 退出采集后从资源入口重新选择
        Assert.That(E.Cancel(s).Ok, Is.True);
        Assert.That(s.Resources["field_fiber"].Reserved, Is.Zero);
    }

    [Test]
    public void Q475_OtherUseToolGone_KeepsGenericFallback_CarcassClauses()
    {
        var s = Field(3);
        ItemInstance? knife = null;
        ItemInstance? carcass = null;
        var id = KeptAtField(s, g =>
        {
            knife = E.DebugCreateEquipment(g, "stone_knife", "white", ItemInstance.Carried);
            carcass = E.CreateItems(g, "carcass_feral_dog", 1, "field").Single();
            return ActionRequest.Butcher(carcass.InstanceId);
        });
        Assert.That(E.ActionProgress(s)!.ExitNote, Is.EqualTo($"退出将放弃本次处理。尸体会返还{Field}地面。保质期不刷新，不会获得产物。已耗时间不退。"));
        BackToField(s, id);
        s.Items.Remove(knife!);
        AssertBlocked(s, GameEngine.ResumeToolGone);
        s.Items.Add(knife!);
        var exp = carcass!.ExpiresAt;
        carcass.ExpiresAt = s.GameMinute;
        AssertBlocked(s, "尸体已腐败，不能处理");
        carcass.ExpiresAt = exp;
        Assert.That(E.ResumeBlockReason(s), Is.Null);
        Assert.That(E.Cancel(s).Ok, Is.True);
        Assert.That(s.Log.Last().Text, Is.EqualTo($"已取消处理尸体。尸体已返还{Field}地面。保质期未刷新，没有产物。已耗时间保留。"));
        Assert.That(carcass.Owner, Is.EqualTo("field"));
        Assert.That(carcass.ExpiresAt, Is.EqualTo(exp));

        // 随身尸体：解除预留留在随身
        var t = Field(3);
        KeptAtField(t, g =>
        {
            E.DebugCreateEquipment(g, "stone_knife", "white", ItemInstance.Carried);
            return ActionRequest.Butcher(E.CreateItems(g, "carcass_feral_dog", 1, ItemInstance.Carried).Single().InstanceId);
        });
        Assert.That(E.Cancel(t).Ok, Is.True);
        Assert.That(t.Log.Last().Text, Is.EqualTo("已取消处理尸体。尸体已解除预留，仍在随身。保质期未刷新，没有产物。已耗时间保留。"));
        Assert.That(t.Items.Single(i => i.ItemId == "carcass_feral_dog").Owner, Is.EqualTo(ItemInstance.Carried));
    }

    /// <summary>把当前主行动标成撤退离地保留的暂停（Q440 已接管；用于只核对文案归类的用例）。</summary>
    private static void MarkKept(GameState s)
    {
        s.Main!.Status = ActionStatus.Paused;
        s.Main.PauseReason = PauseReason.StatAlert;
        s.Main.RetreatKept = true;
        s.Main.RetreatMoveOpen = true;
    }

    [Test]
    public void Q476_FieldPatchUsesRepairRow_FixedWaterUsesCraftRow_CampingKeepsEndedText()
    {
        // 现场修补：Q471「维护」行；包与目标只解锁，没有返还材料分句
        var s = New();
        s.Items.RemoveAll(i => i.ItemId.StartsWith("field_patch"));
        var mach = E.DebugCreateEquipment(s, "machete", "white", s.Ground);
        mach.MaxDurability = 100;
        mach.Durability = 50;
        var pack = E.CreateItems(s, "field_patch", 1, s.Ground).Single();
        Start(s, new ActionRequest { Type = ActionType.Craft, TargetId = GameEngine.FieldPatchRepairId, InstanceId = mach.InstanceId, PatchInstanceId = pack.InstanceId });
        Assert.That(s.Main!.Label, Does.StartWith("现场修补"));
        MarkKept(s);
        Assert.That(E.ActionProgress(s)!.ExitNote, Is.EqualTo("退出将放弃本次维护。不会完成本次维护。已耗时间不退。"));
        Assert.That(E.Cancel(s).Ok, Is.True);
        Assert.That(s.Log.Last().Text, Is.EqualTo("已取消维护。未完成本次维护。已耗时间保留。"));
        Assert.That(E.IsLocked(s, pack) || E.IsLocked(s, mach), Is.False);
        Assert.That(mach.Durability, Is.EqualTo(50));

        // 固定等待净水：「制作」行，返还按实际来源
        var w = New();
        E.DebugLearnAllKnowledge(w);
        E.DebugBuildAllWorkshops(w);
        foreach (var k in new[] { "crafting", "medicine", "cooking" }) w.Skills[k].Level = 5;
        w.Items.RemoveAll(i => i.ItemId is "dirty_water" or "clean_water" or "purification_tablet");
        E.CreateItems(w, "dirty_water", 4, w.CurrentLocation);
        E.CreateItems(w, "purification_tablet", 2, w.CurrentLocation);
        Start(w, ActionRequest.Craft("ex_tablet_water"));
        Assert.That(w.Main!.FixedWait, Is.True);
        MarkKept(w);
        Assert.That(E.Cancel(w).Ok, Is.True);
        Assert.That(w.Log.Last().Text, Is.EqualTo($"已取消制作。地面预留材料已返还{E.ZoneName(w, w.Ground)}地面。未生成半成品。已耗时间保留。"));
        Assert.That(w.Items.Where(i => i.ItemId == "dirty_water").Sum(i => i.Count), Is.EqualTo(4));
        Assert.That(w.Items.Any(i => i.ReservedBy != null), Is.False);

        // 野营布／柴炉部署：保留「已结束{行动名}。」，只解锁原物
        var c = New();
        E.DebugTeleport(c, "w19_survey");
        c.WorldEnemies.RemoveAll(x => x.LocationId == "w19_survey");
        var stove = E.CreateItems(c, "folding_stove", 1, ItemInstance.Carried).Single();
        Start(c, GameEngine.Deploy(stove));
        MarkKept(c);
        var label = c.Main!.Label;
        Assert.That(E.Cancel(c).Ok, Is.True);
        Assert.That(c.Log.Last().Text, Is.EqualTo($"已结束{label}。"));
        Assert.That(stove.Owner, Is.EqualTo(ItemInstance.Carried));
        Assert.That(E.IsLocked(c, stove), Is.False);
    }

    [Test]
    public void Q465_A03_WaitCook_OnlyBoundTargets_AllDone_FireTexts()
    {
        var s = New();
        var place = s.Ground;
        var fac = E.FacOf(s, place)!;
        fac.FireBuilt = true;
        fac.FireLit = true;
        fac.FuelMinutes = 60;
        fac.Processes.Add(new FacilityProcess { ProcessId = "p2", RecipeId = "boil_water" });
        fac.Completed.Add("p1@100");
        s.Main = new ActionState
        {
            ActionId = s.NextActionId++, Type = ActionType.WaitCook, TargetId = place, SourceLocation = s.CurrentLocation,
            Status = ActionStatus.Paused, PauseReason = PauseReason.StatAlert, Label = "等待加工",
            CookTargets = new List<string> { "p1" }, CookTargetItems = new List<int> { 0 },
        };
        Assert.That(E.ResumeBlockReason(s), Is.EqualTo(GameEngine.ResumeCookAllDone), "后放进去的新工序不加进目标");
        s.Main.CookTargets.Add("p2");
        s.Main.CookTargetItems.Add(0);
        fac.FireLit = false;
        Assert.That(E.ResumeBlockReason(s), Is.EqualTo("火堆未燃烧：请先点火（等待加工不会自动点火）"));
        fac.FireLit = true;
        fac.FuelMinutes = 0;
        Assert.That(E.ResumeBlockReason(s), Is.EqualTo("火堆燃料不够下一段，请先投入木材（等待加工不会自动加木）"));
        fac.FuelMinutes = 60;
        Assert.That(E.ResumeBlockReason(s), Is.Null);
        Assert.That(s.Main.CookDone, Is.Empty, "检查不改完成记录");
    }

    [Test]
    public void FlagOff_ResumeKeepsOldGeneralChecksOnly()
    {
        var s = New();
        s.Main = new ActionState { ActionId = 99, Type = ActionType.Craft, TargetId = "no_such_recipe", Status = ActionStatus.Paused, Label = "制作" };
        Assert.That(Off.ResumeBlockReason(s), Is.Null, "开关关时不做 Q465 重验");
    }
}
