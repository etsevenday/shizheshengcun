using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// B41b 里程碑 6：继续前的完整只读重验与分来源返还（R05「B41b Q463—Q471」Q465、Q466、Q471，「Q472—Q476」Q475、Q476；
/// 逐字稿「B41b Q463—Q471 续接与返还逐字稿」；验收 Q440-A01 返还部分、Q440-A02、Q465-A01—A03、Q466-A01—A02、Q471-A01、Q475-A01、Q476-A01）。
/// </summary>
// B51c1 改期望（出处 S08 文首 2026-11-16 B51 §Q771 拼接顺序与短稿、02 Q874 开发方建议 A 暂用）：撤退保留／普通退出预告引句「退出将放弃本次{动作}。」→「退出将取消{动作}。」（维护→维修，现场修补用原名）、
// 结果尾句「已耗时间保留。」→「已耗时间不退。」、删「未生成半成品。」「不会生成半成品。」；处理尸体沿 Q722 引句，只改尾句。锁定自动中止仍「已耗时间保留。」。
public class B41bResumeReturnTests : B41bTestBase
{
    private string Field => E.LocName("field");

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

    // B60a 改用例（出处 S01「2026-11-24 B60 暂停继续与撤退后重新开始」Q1100，取代 R05 Q440 撤退离地保留）：原「撤退保留→异地退出」的 Q466／Q471 返还，
    // 改为撤退离地当刻按原中止结算返还（Q466 开工来源返还仍用于结算）；结果句由「已取消制作。…已耗时间不退。」改为系统中止格式「{行动名}已中止。原因：…已耗时间保留。」。
    // 原「撤退保留→走回原地→续接复核」的 Q465／Q475 用例改用同地暂停起点（PausedAtField），复核规则本身不变。

    [Test]
    public void Q1100_Q466_RetreatAway_CarriedStaysCarried_GroundBackToOrigin_Once()
    {
        var s = Field(3);
        var campFiber0 = -1;
        var allFiber0 = -1;
        var id = RetreatEndsAtField(s, ActionRequest.Craft("knife"), g =>
        {
            E.DebugGive(g, "stone", 1, ItemInstance.Carried);
            E.DebugGive(g, "fiber", 1, "field");
            campFiber0 = g.Items.Where(i => i.ItemId == "fiber" && i.Owner == "camp").Sum(i => i.Count);
            allFiber0 = g.Items.Where(i => i.ItemId == "fiber").Sum(i => i.Count);
        });
        Assert.That(s.Log.Last().Text,
            Is.EqualTo($"{LastLabel}已中止。原因：已撤退离开{Field}。随身预留材料已解除预留，仍在随身；地面预留材料已返还{Field}地面。已耗时间保留。"));
        Assert.That(s.Items.Any(i => i.ReservedBy == id), Is.False);
        Assert.That(s.Items.Count(i => i.ItemId == "stone" && i.Owner == ItemInstance.Carried), Is.EqualTo(1), "随身部分不被抛回远处");
        Assert.That(s.Items.Where(i => i.ItemId == "fiber" && i.Owner == "field").Sum(i => i.Count), Is.EqualTo(1), "原地面材料回原行动地点，不落到当前营地");
        Assert.That(s.Items.Where(i => i.ItemId == "fiber" && i.Owner == "camp").Sum(i => i.Count), Is.EqualTo(campFiber0));
        Assert.That(s.Items.Any(i => i.ItemId == "stone_knife"), Is.False, "没有半成品");
        Assert.That(E.Cancel(s).Ok, Is.False, "已结束，不能再退出");
        Assert.That(s.Items.Where(i => i.ItemId == "fiber").Sum(i => i.Count), Is.EqualTo(allFiber0), "不重复返还");
    }

    [Test]
    public void Q1100_Q471_GroundOnly_And_CarriedOnly_Clauses()
    {
        var s = Field(3);
        RetreatEndsCraft(s, stoneCarried: false, fiberOnGround: true);
        Assert.That(s.Log.Last().Text, Is.EqualTo($"{LastLabel}已中止。原因：已撤退离开{Field}。地面预留材料已返还{Field}地面。已耗时间保留。"));

        var t = Field(3);
        RetreatEndsCraft(t, stoneCarried: true, fiberOnGround: false);
        Assert.That(t.Log.Last().Text, Is.EqualTo($"{LastLabel}已中止。原因：已撤退离开{Field}。随身预留材料已解除预留，仍在随身。已耗时间保留。"));
        var mine = t.Items.Where(i => (i.ItemId == "stone" || i.ItemId == "fiber") && i.Owner == ItemInstance.Carried).ToList();
        Assert.That(mine, Has.Count.EqualTo(2));
        Assert.That(mine.All(i => i.ReservedBy == null), Is.True);
    }

    [Test]
    public void Q466_A02_SaveLoadKeepsSources_LegacyActionBackfilledFromRealOwner()
    {
        var s = Field(3);
        PausedCraft(s);
        var t = SaveSerializer.Deserialize(SaveSerializer.Serialize(s), CfgV2);
        Assert.That(t.Main!.ReserveSources, Is.EquivalentTo(s.Main!.ReserveSources!));

        // 旧行动没有来源记录：撤退离地结算时按仍在原处的真实归属补齐后返还，不按队伍当前地点
        var u = Field(3);
        TravelTo(u, "camp");
        TravelTo(u, "field");
        E.DebugSpawnEnemy(u, "shambler", "field");
        E.DebugGive(u, "stone", 1);
        E.DebugGive(u, "fiber", 1, "field");
        Start(u, ActionRequest.Craft("knife"));
        var uf = Reserved(u, "fiber", u.Main!.ActionId);
        var us = Reserved(u, "stone", u.Main.ActionId);
        u.Main!.ReserveSources = null;
        u.Main.StartZone = null;
        SetLeft(u, GameEngine.SingleCardKey(u.WorldEnemies.Single().Id), 0);
        Step(u);
        RetreatOk(u);
        Assert.That(u.Main, Is.Null);
        Assert.That((uf.Owner, uf.ReservedBy), Is.EqualTo(("field", (int?)null)));
        Assert.That((us.Owner, us.ReservedBy), Is.EqualTo((ItemInstance.Carried, (int?)null)));
    }

    [Test]
    public void Q465_A01_SpoiledReservationChangedTargetGone_BlockWithoutSideEffects()
    {
        var s = Field(3);
        var id = PausedCraft(s);
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
        PausedCraft(s);
        var work = s.Main!.WorkDone;
        Assert.That(work, Is.GreaterThan(0), "起点：已提交一段进度");
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
        PausedAtField(s, ActionRequest.Clear(), g => g.Locations["field"].ClearCharges = Math.Max(1, g.Locations["field"].ClearCharges));
        s.Locations["field"].ClearCharges = 0;
        Assert.That(E.ResumeBlockReason(s), Is.Null, "已扣的清理次数视为已付");
    }

    [Test]
    public void Q465_Q491_SubRunning_GeneralReason_TravelSameText()
    {
        var s = Field(3);
        PausedCraft(s);
        Start(s, ActionRequest.Rest(30));
        Assert.That(E.ResumeBlockReason(s), Is.EqualTo("当前行动还没结束。"));
        Assert.That(Run(s, autoResume: false), Is.EqualTo(SegmentStatus.Completed));
        Assert.That(E.ResumeBlockReason(s), Is.Null, "子行动结束后同地继续可用");

        var t = Field(3);
        Start(t, ActionRequest.Travel("camp"));
        Step(t);
        t.Main!.Status = ActionStatus.Paused;
        t.Main.PauseReason = PauseReason.StatAlert;
        Start(t, ActionRequest.Rest(30));
        Assert.That(E.ResumeBlockReason(t), Is.EqualTo("当前行动还没结束。"), "Q491：移动有未结束子行动也统一用此句");
    }

    [Test]
    public void Q475_GatherToolGone_UsesGatherText_BrokenAndWornUseStartTexts()
    {
        var s = Field(3);
        ItemInstance? knife = null;
        PausedAtField(s, ActionRequest.Gather("field_fiber"), g =>
        {
            var rs = g.Resources["field_fiber"];
            rs.Discovered = true;
            rs.Available = Math.Max(3, rs.Available ?? 0);
            knife = E.DebugCreateEquipment(g, "stone_knife", "white", ItemInstance.Carried);
        });
        Assert.That(s.Main!.ToolInstanceId, Is.EqualTo(knife!.InstanceId));

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
        PausedAtField(s, g =>
        {
            knife = E.DebugCreateEquipment(g, "stone_knife", "white", ItemInstance.Carried);
            carcass = E.CreateItems(g, "carcass_feral_dog", 1, "field").Single();
            return ActionRequest.Butcher(carcass.InstanceId);
        });
        var r0 = carcass!.ShelfPausedMinutes!.Value;   // 保护仍在（只撤说明）
        s.Items.Remove(knife!);
        AssertBlocked(s, GameEngine.ResumeToolGone);
        s.Items.Add(knife!);
        var exp = carcass!.ExpiresAt;
        // 旧档无保护的尸体到期（B51a 起新开工受 R 保护，不会按原截止腐败）
        carcass.ShelfPausedMinutes = null;
        carcass.ExpiresAt = s.GameMinute;
        AssertBlocked(s, "尸体已腐败，不能处理");
        carcass.ExpiresAt = exp;
        carcass.ShelfPausedMinutes = r0;
        Assert.That(E.ResumeBlockReason(s), Is.Null);
        Assert.That(E.Cancel(s).Ok, Is.True);
        // BM11 改断言出处：S08 文首 2026-11-17 §Q882 用户新裁定（返料不附恢复保质说明）
        Assert.That(s.Log.Last().Text, Is.EqualTo($"已取消处理尸体。尸体已返还{Field}地面。没有产物。已耗时间不退。"));
        Assert.That(carcass.Owner, Is.EqualTo("field"));
        Assert.That((carcass.ExpiresAt, carcass.ShelfPausedMinutes), Is.EqualTo(((int?)(s.GameMinute + r0), (int?)null)), "C＋R");

        // 随身尸体、撤退离地结束：Q466 解除预留留在随身（系统中止格式，Q1100）
        var t = Field(3);
        RetreatEndsAtField(t, g =>
        {
            E.DebugCreateEquipment(g, "stone_knife", "white", ItemInstance.Carried);
            return ActionRequest.Butcher(E.CreateItems(g, "carcass_feral_dog", 1, ItemInstance.Carried).Single().InstanceId);
        });
        Assert.That(t.Log.Last().Text, Is.EqualTo($"{LastLabel}已中止。原因：已撤退离开{Field}。尸体已解除预留，仍在随身。没有产物。已耗时间保留。"));
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
        // BM11 改断言出处：S08 文首 2026-11-17 §Q882（后果词同步“维修”；Q874 A）
        Assert.That(E.ActionProgress(s)!.ExitNote, Is.EqualTo("退出将取消现场修补。不会完成本次维修。已耗时间不退。"));
        Assert.That(E.Cancel(s).Ok, Is.True);
        Assert.That(s.Log.Last().Text, Is.EqualTo("已取消现场修补。未完成本次维修。已耗时间不退。"));
        Assert.That(E.IsLocked(s, pack) || E.IsLocked(s, mach), Is.False);
        Assert.That(mach.Durability, Is.EqualTo(50));

        // 固定等待净水：「制作」行，返还按实际来源
        var w = New();
        E.DebugLearnAllKnowledge(w);
        E.DebugBuildAllWorkshops(w);
        foreach (var k in new[] { "crafting", "medicine", "cooking" }) w.Skills[k].Level = 5;
        w.Items.RemoveAll(i => i.ItemId is "dirty_water" or "clean_water" or "purification_tablet");
        E.CreateItems(w, "dirty_water", 20, w.CurrentLocation);   // B56：每份100毫升，×5 保持原 2000 毫升（一批）
        E.CreateItems(w, "purification_tablet", 2, w.CurrentLocation);
        Start(w, ActionRequest.Craft("ex_tablet_water"));
        Assert.That(w.Main!.FixedWait, Is.True);
        MarkKept(w);
        Assert.That(E.Cancel(w).Ok, Is.True);
        Assert.That(w.Log.Last().Text, Is.EqualTo($"已取消制作。地面预留材料已返还{E.ZoneName(w, w.Ground)}地面。已耗时间不退。"));
        Assert.That(w.Items.Where(i => i.ItemId == "dirty_water").Sum(i => i.Count), Is.EqualTo(20));   // B56 改期望：取消全数返还造数 20 份（×5 保持原 2000 毫升）
        Assert.That(w.Items.Any(i => i.ReservedBy != null), Is.False);

        // 野营布／柴炉部署：保留「已结束{行动名}。」，只解锁原物（B41c2：柴炉部署开工 5 分钟不可退出，改用 10 分钟的铺开野营布）
        var c = New();
        E.DebugTeleport(c, "w19_survey");
        c.WorldEnemies.RemoveAll(x => x.LocationId == "w19_survey");
        var stove = E.CreateItems(c, "field_tarp", 1, ItemInstance.Carried).Single();
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
        // B46（Q536）：原绑定目标全部有完成回执时可以继续，继续即正常完成；后放进去的新工序不加进目标
        Assert.That(E.ResumeBlockReason(s), Is.Null, "后放进去的新工序不加进目标");
        Assert.That(E.CookWaitAllDone(s, s.Main), Is.True);
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
}
