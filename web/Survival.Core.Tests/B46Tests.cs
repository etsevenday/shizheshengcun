using Newtonsoft.Json.Linq;
using Survival.Core.Config;
using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// B46 等待加工即时收尾与锁定行动次要答复（S01「B41c2 Q531—Q534 实施补充定稿」「B41c2 Q535—Q536 试玩批定稿」，S07／S11／S14 同批节，
/// R05 验收 Q531-A01—Q536-A04，逐字稿两张同批表；开发记录 B46）。
/// 本文件是内核用例；网页壳的单段展示、暂停条顺序与悬停同源用源码检查（<see cref="B46ShellTests"/>）。
/// </summary>
public class B46Tests : TestBase
{
    private static readonly GameConfig CfgOff = LoadConfig(false);
    private static readonly GameConfig CfgV2 = LoadConfig(true);

    private static void Pause(GameState s)
    {
        s.Main!.Status = ActionStatus.Paused;
        s.Main.PauseReason = PauseReason.StatAlert;
    }

    private static GameState NewOn(GameEngine e, uint seed = 12345)
    {
        var s = e.NewGame(seed);
        e.DebugLegacyStory(s);
        return s;
    }

    private static GameState Camping(GameEngine e)
    {
        var s = NewOn(e);
        e.DebugTeleport(s, "w19_survey");
        s.WorldEnemies.RemoveAll(x => x.LocationId == "w19_survey");
        return s;
    }

    private static void StartOk(GameEngine e, GameState s, ActionRequest r)
    {
        var res = e.Start(s, r);
        Assert.That(res.Ok, Is.True, res.Reason);
    }

    private static string AsSchema48(GameState s)
    {
        var o = JObject.Parse(SaveSerializer.Serialize(s));
        o["SchemaVersion"] = "48";
        foreach (var key in new[] { "Main", "Sub", "StashedWork" })
            if (o[key] is JObject a) a.Remove("NoExit");
        return o.ToString();
    }

    // ───────────── Q531 野营布／柴炉部署收起因原物丢失自动中止 ─────────────

    [Test]
    public void Q531_A01_StoveDeployAndPack_LostItem_AbortOnlyReasonAndElapsed()
    {
        // 部署：锁定、暂停、原物丢失，真实继续自动中止
        var s = Camping(E);
        var stove = E.CreateItems(s, "folding_stove", 1, ItemInstance.Carried).Single();
        Start(s, GameEngine.Deploy(stove));
        Assert.That(s.Main!.NoExit, Is.True);
        var label = s.Main.Label;
        Pause(s);
        s.Items.Remove(stove);
        var p = E.ActionProgress(s)!;
        Assert.That((p.ResumeNote, p.ResumeAborts), Is.EqualTo((GameEngine.LockedTargetGone, true)));
        var t0 = s.GameMinute;
        var r = E.Resume(s);
        Assert.That((r.Ok, r.Aborted, s.Main, s.GameMinute), Is.EqualTo((true, true, (ActionState?)null, t0)));
        Assert.That(s.Log.Last().Text, Is.EqualTo($"{label}已中止。原因：{GameEngine.LockedTargetGone}{GameEngine.ElapsedKept}"),
            "只原因与「已耗时间保留。」：无返还／后果／取消引句，原因自带句号不重复");
        Assert.That(s.Items.Any(i => i.ItemId == "folding_stove"), Is.False, "不补原物");
        Assert.That(E.FacOf(s, s.Ground)!.StoveInstanceId, Is.Null, "没有未完成成果");
        Assert.That(E.Resume(s).Ok, Is.False, "只收束一次");

        // 收起：同样只两句
        var s2 = Camping(E);
        var st2 = E.CreateItems(s2, "folding_stove", 1, ItemInstance.Carried).Single();
        Start(s2, GameEngine.Deploy(st2));
        Run(s2);
        Assert.That(E.IsDeployed(s2, st2), Is.True);
        Start(s2, GameEngine.Pack(st2));
        Assert.That(s2.Main!.NoExit, Is.True);
        var label2 = s2.Main.Label;
        Pause(s2);
        s2.Items.Remove(st2);
        Assert.That(E.Resume(s2).Aborted, Is.True);
        Assert.That(s2.Log.Last().Text, Is.EqualTo($"{label2}已中止。原因：{GameEngine.LockedTargetGone}{GameEngine.ElapsedKept}"));

        // 原 10 分钟铺开野营布不因此变成 5 分钟或锁定，仍按原取消路径
        var s3 = Camping(E);
        var tarp = E.CreateItems(s3, "field_tarp", 1, ItemInstance.Carried).Single();
        Start(s3, GameEngine.Deploy(tarp));
        Assert.That((s3.Main!.PlannedMinutes, s3.Main.NoExit), Is.EqualTo((10, false)));
    }

    // ───────────── Q532 锁定采集只缺拾取原工具 ─────────────

    private (GameEngine e, GameState s, ItemInstance tool) LockedGather()
    {
        var e = E;
        var s = NewOn(e);
        e.DebugMapTeleport(s, "w19_survey");
        if (s.Locations["w19_survey"].Progress < 100) e.DebugSetProgress(s, "w19_survey", 100);
        s.WorldEnemies.RemoveAll(x => x.LocationId == "w19_survey");
        e.DebugGive(s, "crowbar", 1);
        var bar = s.Items.Where(i => i.ItemId == "crowbar" && i.IsCarried).OrderByDescending(i => i.InstanceId).First();
        StartOk(e, s, ActionRequest.Gather("w19_survey_b", bar.InstanceId));
        s.Main!.NoExit = true;   // 视作开工 5 分钟锁定的采集（采集工作量受时段、负重等倍率影响，这里只验复核分支）
        Pause(s);
        return (e, s, bar);
    }

    [Test]
    public void Q532_A01_LockedGather_OnlyPickupMissing_ToolNameSentence_ReadOnly()
    {
        var (e, s, bar) = LockedGather();
        bar.Owner = s.Ground;
        var why = $"需要先拾取原工具：{e.ItemName(bar.ItemId)}。";
        Assert.That(why, Is.EqualTo("需要先拾取原工具：撬棍。"));
        var before = SaveSerializer.Serialize(s);
        var p = e.ActionProgress(s)!;
        Assert.That((p.ResumeNote, p.ResumeAborts), Is.EqualTo((why, false)), "复核原因用公开名，不是永久失败");
        Assert.That(e.LockedResume(s).Kind, Is.EqualTo(LockedResumeKind.Temporary));
        var r = e.Resume(s);
        Assert.That((r.Ok, r.Reason), Is.EqualTo((false, why)), "真实继续拒绝同句");
        Assert.That(SaveSerializer.Serialize(s), Is.EqualTo(before), "只读、拒绝都不拿工具、不取消、不推进时间");
        Assert.That(why, Does.Not.Contain("#").And.Not.Contain(bar.InstanceId.ToString()));
    }

    [Test]
    public void Q532_A02_OtherFailures_KeepOwnReasons_PickupKeepsLockAndPause_UnlockedOldText()
    {
        var (e, s, bar) = LockedGather();
        // 合法拾取：0 时间、同一实例同一锁，仍暂停须手动继续
        bar.Owner = s.Ground;
        var t0 = s.GameMinute;
        var r = e.DoInstant(s, InstantRequest.Pickup(bar.InstanceId, bar.Count));
        Assert.That((r.Ok, r.Aborted), Is.EqualTo((true, false)), r.Reason);
        Assert.That((bar.Owner, s.GameMinute, s.Main!.Status), Is.EqualTo((ItemInstance.Carried, t0, ActionStatus.Paused)));
        Assert.That(s.Main.LockedItemIds.Contains(bar.InstanceId) || s.Main.ToolInstanceId == bar.InstanceId, Is.True);
        Assert.That(e.LockedResume(s).Kind, Is.EqualTo(LockedResumeKind.Ok));

        // 不在当前地面（别处）：原具体永久原因，不误报可拾取
        bar.Owner = "camp";
        Assert.That((e.ActionProgress(s)!.ResumeNote, e.ActionProgress(s)!.ResumeAborts), Is.EqualTo((GameEngine.LockedGatherToolGone, true)));
        // 已损坏：原句
        bar.Owner = ItemInstance.Carried;
        bar.Durability = 0;
        Assert.That(e.ActionProgress(s)!.ResumeNote, Is.EqualTo(GameEngine.LockedGatherToolBroken));

        // 非锁定采集：原「先拾取工具」不变
        var (e2, s2) = (E, NewOn(E));
        E.DebugMapTeleport(s2, "w19_survey");
        if (s2.Locations["w19_survey"].Progress < 100) E.DebugSetProgress(s2, "w19_survey", 100);
        var ground = E.DebugCreateEquipment(s2, "fire_axe", "white", "w19_survey");
        Assert.That(e2.CanStart(s2, ActionRequest.Gather("w19_survey_b", ground.InstanceId)), Is.EqualTo("先拾取工具"));
    }

    // ───────────── Q533 暂停条信息（内核只读部分） ─────────────

    [Test]
    public void Q533_A02_ReadOnlyReview_NoEmptyWarning_TemporaryHasNoAbortNote()
    {
        var (e, s, bar) = LockedGather();
        // 没有复核原因：不造空警告
        var p = e.ActionProgress(s)!;
        Assert.That((p.ResumeNote, p.ResumeAborts, p.PauseText), Is.EqualTo(((string?)null, false, p.PauseText)));
        Assert.That(p.PauseText, Is.Not.Empty, "原暂停原因保留");
        // 暂时失败：不显示永久说明；只读不覆盖原暂停原因
        bar.Owner = s.Ground;
        var p2 = e.ActionProgress(s)!;
        Assert.That((p2.ResumeAborts, p2.PauseText), Is.EqualTo((false, p.PauseText)));
        Assert.That(s.Main!.PauseReason, Is.EqualTo(PauseReason.StatAlert));
    }

    // ───────────── Q534 有未结束子行动时的旧接敌／固定战原因 ─────────────

    [Test]
    public void Q534_A01_A02_LockedPausedWithSub_ApproachAndFixedSay_NotFinished()
    {
        var off = new GameEngine(CfgOff);
        var s = Camping(off);
        Assert.That(off.CardsV2(s), Is.False, "旧接敌／固定战入口");
        StartOk(off, s, GameEngine.Deploy(off.CreateItems(s, "folding_stove", 1, ItemInstance.Carried).Single()));
        Pause(s);
        off.DebugGive(s, "clean_water", 1);
        StartOk(off, s, ActionRequest.UseItem("clean_water"));
        Assert.That(s.Sub, Is.Not.Null, "子行动未结束");
        Assert.That(off.CanApproach(s, 999), Is.EqualTo(GameEngine.LockedSubRunning));
        Assert.That(off.CanApproach(s, 999), Is.EqualTo("当前行动还没结束。"));
        Assert.That(off.Approach(s, 999).Reason, Is.EqualTo("当前行动还没结束。"), "按钮原因与真实拒绝同句");
        Assert.That(off.CanEngageFixed(s), Is.EqualTo("当前行动还没结束。"));
        Assert.That(off.EngageFixed(s).Reason, Is.EqualTo("当前行动还没结束。"));
        Assert.That(s.Battle, Is.Null);

        // 子行动结束后仍落到锁定主行动暂停：原句
        while (s.Sub != null) off.Advance(s);
        Assert.That(s.Main!.Status, Is.EqualTo(ActionStatus.Paused), "子行动完回父暂停");
        Assert.That(off.CanApproach(s, 999), Is.EqualTo("行动暂停中，请先继续行动。"));
        Assert.That(off.CanEngageFixed(s), Is.EqualTo("行动暂停中，请先继续行动。"));

        // 非锁定暂停 + 子行动：原句不变
        var s2 = Camping(off);
        StartOk(off, s2, GameEngine.Deploy(off.CreateItems(s2, "field_tarp", 1, ItemInstance.Carried).Single()));
        Pause(s2);
        off.DebugGive(s2, "clean_water", 1);
        StartOk(off, s2, ActionRequest.UseItem("clean_water"));
        Assert.That(off.CanApproach(s2, 999), Is.EqualTo("行动暂停中，先结束或取消行动"));
        Assert.That(off.CanEngageFixed(s2), Is.EqualTo("行动暂停中，先结束或取消行动"));

        // 读档后同真实状态
        var t = SaveSerializer.Deserialize(SaveSerializer.Serialize(s), CfgOff);
        Assert.That(off.CanApproach(t, 999), Is.EqualTo("行动暂停中，请先继续行动。"));
    }

    // ───────────── Q535 单段 5 分钟行动先展示再结算（内核判定部分） ─────────────

    [Test]
    public void Q535_A01_SingleSegment_PendingAfterStart_ReadOnly_ThenOneRealSegment()
    {
        // 柴炉部署
        var s = Camping(E);
        var stove = E.CreateItems(s, "folding_stove", 1, ItemInstance.Carried).Single();
        Start(s, GameEngine.Deploy(stove));
        var before = SaveSerializer.Serialize(s);
        Assert.That(E.SingleSegmentShowPending(s), Is.True);
        Assert.That(SaveSerializer.Serialize(s), Is.EqualTo(before), "判定只读：不推进世界、不结算");
        var p = E.ActionProgress(s)!;
        Assert.That(ShellText.ProgressLines(p), Is.EqualTo(("已推进0分钟 / 5分钟", (string?)"剩余5分钟")), "新开固定 5 分钟初始真值");
        Assert.That((p.Locked, p.Exit, p.ExitNote), Is.EqualTo((true, ExitKind.None, "本次行动不可中途退出。")));
        var t0 = s.GameMinute;
        Assert.That(E.Advance(s).Status, Is.EqualTo(SegmentStatus.Completed));
        Assert.That((s.GameMinute - t0, s.Main, E.IsDeployed(s, stove)), Is.EqualTo((5, (ActionState?)null, true)), "唯一原 5 分钟段一次");
        Assert.That(E.SingleSegmentShowPending(s), Is.False);

        // 点火（单段事务）
        var s2 = NewOn(E);
        var fac = s2.Locations["camp"].Facility;
        fac.FireLit = false;
        fac.FuelMinutes = 60;
        var lighter = E.IgniteToolOptions(s2).FirstOrDefault(o => o.Usable);
        Assert.That(lighter, Is.Not.Null, "营地可点火");
        StartOk(E, s2, ActionRequest.Ignite(lighter!.InstanceId));
        Assert.That(E.SingleSegmentShowPending(s2), Is.True, "点火");
        Assert.That(E.ActionProgress(s2)!.Locked, Is.True);

        // 救助配给
        var s3 = NewOn(E);
        E.DebugMapTeleport(s3, "c04_alms");
        E.DebugSetStats(s3, 15, 30, 30, 15, 80, 70);
        Assert.That(E.StartCityService(s3, "church_relief_ration", GameState.PlayerId, "relief_water").Ok, Is.True);
        Assert.That(E.SingleSegmentShowPending(s3), Is.True, "救助配给");
        var t3 = s3.GameMinute;
        E.Advance(s3);
        Assert.That((s3.Main, s3.GameMinute - t3, Count(s3, "clean_water", "c04_alms")), Is.EqualTo(((ActionState?)null, 5, 1)));
    }

    [Test]
    public void Q535_A02_SubAction_AndStealthCheck_SameRange_BackToParentPause()
    {
        var s = Camping(E);
        Start(s, GameEngine.Deploy(E.CreateItems(s, "field_tarp", 1, ItemInstance.Carried).Single()));
        Assert.That(E.SingleSegmentShowPending(s), Is.False, "10 分钟铺布不是单段");
        Pause(s);
        Assert.That(E.SingleSegmentShowPending(s), Is.False, "暂停不展示");
        E.DebugGive(s, "clean_water", 1);
        var waterBefore = Count(s, "clean_water");
        Start(s, ActionRequest.UseItem("clean_water"));
        Assert.That(E.SingleSegmentShowPending(s), Is.True, "子行动（自用）同范围");
        var t0 = s.GameMinute;
        while (s.Sub != null) E.Advance(s);
        Assert.That((s.GameMinute - t0, s.Main!.Status), Is.EqualTo((5, ActionStatus.Paused)), "子行动结束回父行动暂停");
        Assert.That(Count(s, "clean_water"), Is.EqualTo(waterBefore - 1), "开工即时扣物一次");

        // 潜行接近／躲避：原单段判定
        var s2 = NewOn(E);
        s2.Main = new ActionState { ActionId = s2.NextActionId++, Type = ActionType.Wait, TargetId = GameEngine.StealthTargetPrefix + GameEngine.SneakKind + ":x", PlannedMinutes = 5, Label = "潜行接近" };
        Assert.That(GameEngine.IsStealthCheck(s2.Main), Is.True);
        Assert.That(E.SingleSegmentShowPending(s2), Is.True);
    }

    [Test]
    public void Q535_A03_ShowDuration_SpeedTiers()
    {
        Assert.That(UiPresentationPrefs.SingleSegmentShowBaseMs, Is.EqualTo(1200));
        Assert.That(new[] { AnimationSpeed.Fast, AnimationSpeed.Standard, AnimationSpeed.Slow }.Select(sp => new UiPresentationPrefs { Speed = sp }.SingleSegmentShowMs),
            Is.EqualTo(new[] { 600, 1200, 1800 }));
    }

    [Test]
    public void Q535_A05_LongActionLastFive_AndHigherLocks_NotShown()
    {
        // 长行动推进过一段后剩 5 分钟：不适用
        var s = Camping(E);
        Start(s, GameEngine.Deploy(E.CreateItems(s, "field_tarp", 1, ItemInstance.Carried).Single()));
        E.Advance(s);
        Assert.That((s.Main!.PlannedMinutes - s.Main.ElapsedMinutes, s.Main.NoExit), Is.EqualTo((5, false)));
        Assert.That(E.SingleSegmentShowPending(s), Is.False);
        // 非法开工：没有行动，也就没有展示
        var s2 = Camping(E);
        Assert.That(E.Start(s2, GameEngine.Deploy(new ItemInstance { InstanceId = 99999, ItemId = "folding_stove", Count = 1 })).Ok, Is.False);
        Assert.That(E.SingleSegmentShowPending(s2), Is.False);
        // 终局、战斗等更高锁先处理
        var s3 = Camping(E);
        Start(s3, GameEngine.Deploy(E.CreateItems(s3, "folding_stove", 1, ItemInstance.Carried).Single()));
        s3.Progress.Dead = true;
        Assert.That(E.SingleSegmentShowPending(s3), Is.False);
    }

    [Test]
    public void Q535_A06_ReloadDuringShow_OnlyUncommittedSegment_NoRepeatCost_OldUnlockedKeepsExit()
    {
        var s = NewOn(E);
        E.DebugMapTeleport(s, "c04_alms");
        E.DebugSetStats(s, 15, 30, 30, 15, 80, 70);
        Assert.That(E.StartCityService(s, "church_relief_ration", GameState.PlayerId, "relief_water").Ok, Is.True);
        // 展示中刷新：读回的仍是未提交段的同一行动，不重新接受或扣额
        var t = SaveSerializer.Deserialize(SaveSerializer.Serialize(s), Cfg);
        Assert.That((t.Main!.ActionId, t.Main.ElapsedMinutes, E.SingleSegmentShowPending(t)), Is.EqualTo((s.Main!.ActionId, 0, true)));
        E.Advance(t);
        Assert.That((t.Main, Count(t, "clean_water", "c04_alms")), Is.EqualTo(((ActionState?)null, 1)), "唯一结果一次");
        var done = SaveSerializer.Deserialize(SaveSerializer.Serialize(t), Cfg);
        Assert.That((done.Main, E.SingleSegmentShowPending(done)), Is.EqualTo(((ActionState?)null, false)), "已完成不复活、不重演");

        // 旧档（存档 48）在办的 5 分钟工作量型：迁移不锁，仍可展示单段，但保留真实退出（不强套不可退出）
        var cfg = LoadConfig();
        cfg.Recipes.Single(r => r.Id == "bandage").BaseMinutes = 5;
        var e = new GameEngine(cfg);
        var o = NewOn(e);
        e.DebugGive(o, "cloth", 1);
        e.DebugGive(o, "herb", 1);
        StartOk(e, o, ActionRequest.Craft("bandage"));
        var old = SaveSerializer.Deserialize(AsSchema48(o), cfg);
        Assert.That(old.Main!.NoExit, Is.False, "Q524：工作量型旧在办不锁");
        Assert.That(e.SingleSegmentShowPending(old), Is.True);
        var p = e.ActionProgress(old)!;
        Assert.That((p.Locked, p.Exit), Is.EqualTo((false, ExitKind.StopAtBoundary)), "保留原退出控件与说明");
    }

    // ───────────── Q536 等待加工全部完成的继续 ─────────────

    private static GameState CookWait(GameEngine e, bool locked, out FacilityState fac, out ItemInstance pot)
    {
        var s = NewOn(e);
        pot = s.Items.First(i => i.ItemId == "pot");
        e.DebugGive(s, "dirty_water", 1, "camp");
        var water = s.Items.First(i => i.ItemId == "dirty_water" && i.Owner == "camp");
        Assert.That(e.DoInstant(s, InstantRequest.FillPot(water.InstanceId, pot.InstanceId, 1)).Ok, Is.True);
        Assert.That(e.DoInstant(s, InstantRequest.PutInFire(pot.InstanceId)).Ok, Is.True);
        fac = s.Locations["camp"].Facility;
        fac.FireLit = true;
        fac.FuelMinutes = 600;
        StartOk(e, s, ActionRequest.WaitCook());
        s.Main!.NoExit = locked;
        return s;
    }

    [TestCase(false, false)]
    [TestCase(false, true)]
    [TestCase(true, false)]
    [TestCase(true, true)]
    public void Q536_A01_A02_AllDone_ResumeCompletesNow_NoExtraTimeOrCost(bool locked, bool v2)
    {
        var e = new GameEngine(v2 ? CfgV2 : CfgOff);
        var s = CookWait(e, locked, out var fac, out var pot);
        Assert.That(e.CardsV2(s), Is.EqualTo(v2));
        e.Advance(s);   // 等待自己的一段
        Pause(s);
        var own = s.Main!.ElapsedMinutes;
        Assert.That(own, Is.EqualTo(5));
        // 暂停中休息推进时间直到加工完成（不登记进等待，不并入已耗时）
        StartOk(e, s, ActionRequest.Rest(60));
        for (var k = 0; k < 40 && s.Sub != null; k++) e.Advance(s);
        var pid = s.Main!.CookTargets.Single();
        Assert.That(fac.Processes.Any(x => x.ProcessId == pid), Is.False, "加工已在暂停中完成");
        Assert.That(e.CookWaitAllDone(s, s.Main), Is.True);
        // 后放进火堆的新加工不纳入原目标
        var items = s.Items.Sum(i => i.Count);
        var fuel = fac.FuelMinutes;
        var stats = (s.Stats.Satiety, s.Stats.Hydration, s.Stats.Energy);
        var xp = Newtonsoft.Json.JsonConvert.SerializeObject(s.Player.Skills);
        var t0 = s.GameMinute;
        var label = e.LocName("camp");
        var r = e.Resume(s);
        Assert.That(r.Ok, Is.True, r.Reason);
        Assert.That((s.Main, s.GameMinute), Is.EqualTo(((ActionState?)null, t0)), "09:05 继续仍 09:05 收尾，无额外 5 分钟");
        Assert.That(s.Log.Last().Text, Is.EqualTo($"等待加工结束：{label}火堆1项加工全部完成，成品留在原位（{Clock.Duration(own)}）。"));
        Assert.That((s.Items.Sum(i => i.Count), fac.FuelMinutes, (s.Stats.Satiety, s.Stats.Hydration, s.Stats.Energy), Newtonsoft.Json.JsonConvert.SerializeObject(s.Player.Skills)),
            Is.EqualTo((items, fuel, stats, xp)), "不重发产品／经验、不扣燃料或身体费用");
        Assert.That(e.Resume(s).Ok, Is.False, "重复请求不双完成");
        Assert.That(s.Log.Count(l => l.Text.StartsWith("等待加工结束：")), Is.EqualTo(1));
        _ = pot;
    }

    [Test]
    public void Q536_A02_ProductTakenAway_ReceiptStillCounts_NewProcessNotAdded()
    {
        var e = new GameEngine(CfgOff);
        var s = CookWait(e, false, out var fac, out var pot);
        Pause(s);
        var pid = s.Main!.CookTargets.Single();
        // 原目标完成回执（成品随后被合法取走：回执仍在）
        fac.Processes.RemoveAll(x => x.ProcessId == pid);
        fac.Completed.Add(pid + "@done");
        pot.Owner = ItemInstance.Carried;
        // 后来的新加工
        fac.Processes.Add(new FacilityProcess { ProcessId = "later", RecipeId = "boil_water" });
        Assert.That(e.CookWaitAllDone(s, s.Main), Is.True);
        var t0 = s.GameMinute;
        Assert.That(e.Resume(s).Ok, Is.True);
        Assert.That((s.Main, s.GameMinute), Is.EqualTo(((ActionState?)null, t0)));
        Assert.That(s.Log.Last().Text, Does.Contain("火堆1项加工全部完成"), "目标数取原绑定");
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Q536_A03_NotAllDone_EmptyOrMissing_NotTreatedAsDone(bool v2)
    {
        var e = new GameEngine(v2 ? CfgV2 : CfgOff);
        // 部分完成（目标仍在加工）：不冒充，继续照原规则运行
        var s = CookWait(e, false, out var fac, out _);
        Pause(s);
        Assert.That(e.CookWaitAllDone(s, s.Main!), Is.False);
        Assert.That(e.Resume(s).Ok, Is.True);
        Assert.That(s.Main!.Status, Is.EqualTo(ActionStatus.Running), "非锁定原继续路径");

        // 目标消失而没有回执：不猜补成功
        var s2 = CookWait(e, false, out var fac2, out _);
        Pause(s2);
        fac2.Processes.Clear();
        Assert.That(e.CookWaitAllDone(s2, s2.Main!), Is.False);
        Assert.That(s2.Log.Any(l => l.Text.StartsWith("等待加工结束：")), Is.False);

        // 空绑定列表：不算全部完成
        var s3 = CookWait(e, false, out _, out _);
        Pause(s3);
        s3.Main!.CookTargets.Clear();
        s3.Main.CookTargetItems.Clear();
        Assert.That(e.CookWaitAllDone(s3, s3.Main), Is.False);
        var r3 = e.Resume(s3);
        Assert.That(s3.Log.Any(l => l.Text.StartsWith("等待加工结束：")), Is.False);
        if (v2) Assert.That(r3.Reason, Is.EqualTo(GameEngine.ResumeCookAllDone), "空绑定沿原拒绝");

        // 更高锁（未结束子行动）先拦
        var s4 = CookWait(e, false, out var fac4, out _);
        Pause(s4);
        var pid = s4.Main!.CookTargets.Single();
        fac4.Processes.RemoveAll(x => x.ProcessId == pid);
        fac4.Completed.Add(pid + "@done");
        e.DebugGive(s4, "clean_water", 1);
        StartOk(e, s4, ActionRequest.UseItem("clean_water"));
        Assert.That(e.Resume(s4).Ok, Is.False);
        Assert.That(s4.Main, Is.Not.Null, "子行动未结束时不收尾");
    }

    [Test]
    public void Q536_A04_ReadOnly_NoClear_OldSaveStillExitable_AndCompletesZeroTime()
    {
        var e = new GameEngine(CfgOff);
        var s = CookWait(e, false, out var fac, out _);
        Pause(s);
        var pid = s.Main!.CookTargets.Single();
        fac.Processes.RemoveAll(x => x.ProcessId == pid);
        fac.Completed.Add(pid + "@done");
        // 只读复核、预览、剩余 0：不清行动、不写日志
        var before = SaveSerializer.Serialize(s);
        var p = e.ActionProgress(s)!;
        Assert.That(p.RemainingMinutes, Is.EqualTo(0));
        _ = e.EstimateEndMinute(s);
        Assert.That(SaveSerializer.Serialize(s), Is.EqualTo(before));
        Assert.That(s.Main, Is.Not.Null);

        // Q524 旧档在办等待：读回不锁、仍可退出；全完成后继续同样 0 新增时间
        var old = SaveSerializer.Deserialize(AsSchema48(s), CfgOff);
        Assert.That(old.Main!.NoExit, Is.False);
        Assert.That(e.ActionProgress(old)!.Exit, Is.EqualTo(ExitKind.StopAtBoundary), "退出能力不改");
        var t0 = old.GameMinute;
        Assert.That(e.Resume(old).Ok, Is.True);
        Assert.That((old.Main, old.GameMinute), Is.EqualTo(((ActionState?)null, t0)));
        // 读回完成后的档不再重做
        var again = SaveSerializer.Deserialize(SaveSerializer.Serialize(old), CfgOff);
        Assert.That((again.Main, e.Resume(again).Ok), Is.EqualTo(((ActionState?)null, false)));
    }
}
