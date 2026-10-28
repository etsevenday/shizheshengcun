using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 策划提交 9fdb44f：等待烹饪（S11 §9、S09 §7、S13 §5）、主动推进的退出与剩余时间（S01 §9、S13 §5）、Q6/Q8/Q9 核验。
/// 验收 S11-A09–A12、S09-A07、S13-A06/A07、S01-A10–A12、R03-A12、R05-A09。
/// </summary>
public class V10CookWaitTests : BattleTestBase
{
    /// <summary>营地火堆上放锅（可选装脏水/净水）、点燃并给燃料。</summary>
    private (GameState s, ItemInstance pot) PotOnFire(string? water, int units, int fuel = 180)
    {
        var s = New();
        var pot = First(s, "pot");
        if (water != null && units > 0)
        {
            E.DebugGive(s, water, units, "camp");
            Assert.That(E.DoInstant(s, InstantRequest.FillPot(First(s, water, "camp").InstanceId, pot.InstanceId, units)).Ok, Is.True);
        }
        Assert.That(E.DoInstant(s, InstantRequest.PutInFire(pot.InstanceId)).Ok, Is.True);
        var fac = s.Locations["camp"].Facility;
        fac.FireLit = true;
        fac.FuelMinutes = fuel;
        return (s, pot);
    }

    private void RootsInPot(GameState s, ItemInstance pot, int n)
    {
        E.DebugGive(s, "raw_root", n, "camp");
        Assert.That(E.DoInstant(s, InstantRequest.PutFoodInPot(First(s, "raw_root", "camp").InstanceId, pot.InstanceId, n)).Ok, Is.True);
    }

    private FacilityProcess Proc(GameState s, ItemInstance target) => E.ProcessOf(s, target)!;

    /// <summary>S11-A09：1L 脏水与两份生根、燃料充足——覆盖净化→水煮链，15 分钟不结束、45 分钟全部完成，只按 wait 消耗。</summary>
    [Test]
    public void S11_A09_DirtyWaterThenTwoRoots_45Minutes_NoExtraBodyCost()
    {
        var (s, pot) = PotOnFire("dirty_water", 2);
        RootsInPot(s, pot, 2);
        var plan = E.PreviewCookWait(s);
        Assert.That(plan.Ok, Is.True, plan.Reason);
        Assert.That(plan.Targets, Has.Count.EqualTo(3), "净水 + 两份水煮");
        Assert.That(plan.EstimatedMinutes, Is.EqualTo(45));
        Assert.That(plan.FuelShort, Is.False);
        Assert.That(plan.BodyRates.Satiety, Is.EqualTo(Cfg.HourlyRates["wait"].Satiety), "复用 wait 速率");

        SetStats(s, 20, 80, 80, 10, 90, 60);
        var m0 = s.GameMinute;
        Start(s, ActionRequest.WaitCook());
        Assert.That(s.Main!.Type, Is.EqualTo(ActionType.WaitCook));
        Steps(s, 3);
        Assert.That(E.PotLiquid(s, pot.InstanceId), Is.EqualTo("clean_water"), "15分钟净水完成");
        Assert.That(s.Main, Is.Not.Null, "净水完成时不结束");
        Assert.That(Run(s), Is.EqualTo(SegmentStatus.Completed));
        Assert.That(s.GameMinute - m0, Is.EqualTo(45));
        Assert.That(E.PotFood(s, pot.InstanceId).Select(f => f.ItemId), Is.EqualTo(new[] { "boiled_root", "boiled_root" }), "成品留在原锅，不自动吃");
        var w = Cfg.HourlyRates["wait"];
        Assert.That(s.Stats.Energy, Is.EqualTo(10).Within(1e-9), "没有额外体力消耗或恢复");
        Assert.That(s.Stats.Satiety, Is.EqualTo(80 + w.Satiety * 45 / 60).Within(1e-9));
        Assert.That(s.Log.Last(l => l.Kind == "ActionCompleted").Text, Does.Contain("全部完成"));
    }

    /// <summary>S11-A10：直烤余10分钟、锅煮余25分钟——同次快照两项都纳入，预计25分钟（不相加），先完成项留原位。</summary>
    [Test]
    public void S11_A10_ParallelTargets_TakeLatest_NotSum()
    {
        var (s, pot) = PotOnFire("clean_water", 1);
        RootsInPot(s, pot, 1);
        E.DebugGive(s, "raw_root", 1, "camp");
        Assert.That(E.DoInstant(s, InstantRequest.PutInFire(First(s, "raw_root", "camp").InstanceId, 1)).Ok, Is.True);
        var roast = E.FireContents(s, "camp").Single(i => !E.IsPot(i));
        var boil = E.PotFood(s, pot.InstanceId).Single();
        Proc(s, roast).HeatDoneMinutes = 20;
        Proc(s, boil).HeatDoneMinutes = 5;
        var plan = E.PreviewCookWait(s);
        Assert.That(plan.Targets, Has.Count.EqualTo(2));
        Assert.That(plan.EstimatedMinutes, Is.EqualTo(25), "并行取最晚完成，不是35");
        Assert.That(plan.Targets.Single(t => t.TargetInstanceId == roast.InstanceId).EtaMinutes, Is.EqualTo(10));
        var m0 = s.GameMinute;
        Start(s, ActionRequest.WaitCook());
        Steps(s, 2);
        Assert.That(roast.ItemId, Is.EqualTo("cooked_root"), "直烤先完成，留在加工位");
        Assert.That(roast.InFacility, Is.True);
        Assert.That(s.Main, Is.Not.Null, "任一项先完成不提前结束");
        var prog = E.ActionProgress(s)!;
        Assert.That(prog.RemainingMinutes, Is.EqualTo(15));
        Assert.That(prog.Estimated, Is.True);
        Assert.That(prog.CookTargets.Count(t => t.Done), Is.EqualTo(1));
        Assert.That(Run(s), Is.EqualTo(SegmentStatus.Completed));
        Assert.That(s.GameMinute - m0, Is.EqualTo(25));
        Assert.That(boil.ItemId, Is.EqualTo("boiled_root"));
    }

    /// <summary>S11-A11/S09-A07：空锅/仅成品/缺水/未点火不能开始并写原因；燃料不足明确“尚需至少X有效分钟”，耗尽时停止且不自动补木；完成前会腐败的不纳入。</summary>
    [Test]
    public void S11_A11_S09_A07_NoTargetOrNoHeat_Reasons_FuelShortStops()
    {
        // 空锅
        var (s, pot) = PotOnFire(null, 0);
        var plan = E.PreviewCookWait(s);
        Assert.That(plan.Ok, Is.False);
        Assert.That(plan.Reason, Does.Contain("没有可加工的目标"));
        Assert.That(plan.Excluded.Single().Reason, Does.Contain("空锅"));
        // 缺水：生根在空锅里
        RootsInPot(s, pot, 1);
        plan = E.PreviewCookWait(s);
        Assert.That(plan.Ok, Is.False);
        Assert.That(plan.Excluded.Any(x => x.Reason.Contains("缺水")), Is.True, string.Join("；", plan.Excluded.Select(x => x.Reason)));
        // 仅成品
        var (t, _) = PotOnFire(null, 0);
        E.DebugGive(t, "raw_root", 1, "camp");
        E.DoInstant(t, InstantRequest.PutInFire(First(t, "raw_root", "camp").InstanceId, 1));
        E.FireContents(t, "camp").Single(i => !E.IsPot(i)).ItemId = "cooked_root";
        E.SyncFacilities(t);
        Assert.That(E.CanStart(t, ActionRequest.WaitCook()), Does.Contain("没有可加工的目标"));

        // 未点火：不自动点火、不加木
        var (u, upot) = PotOnFire("dirty_water", 1);
        u.Locations["camp"].Facility.FireLit = false;
        var wood = Count(u, "wood");
        Assert.That(E.CanStart(u, ActionRequest.WaitCook()), Does.Contain("未燃烧"));
        Assert.That(u.Locations["camp"].Facility.FireLit, Is.False);
        Assert.That(Count(u, "wood"), Is.EqualTo(wood));
        u.Locations["camp"].Facility.FireLit = true;
        u.Locations["camp"].Facility.FuelMinutes = 4;
        Assert.That(E.CanStart(u, ActionRequest.WaitCook()), Does.Contain("燃料"), "下一段燃料不足5分钟不能开始（开发假设 A8）");

        // 燃料不足：1L脏水+两根要45分钟，只有20分钟燃料
        var (v, vpot) = PotOnFire("dirty_water", 2, fuel: 20);
        RootsInPot(v, vpot, 2);
        plan = E.PreviewCookWait(v);
        Assert.That(plan.Ok, Is.True, plan.Reason);
        Assert.That(plan.FuelShort, Is.True);
        Assert.That(plan.EstimatedMinutes, Is.Null, "不假报可准时完成");
        Assert.That(plan.FuelNote, Is.EqualTo("尚需至少45有效分钟，当前燃料不足，耗尽时停止"));
        var woodV = Count(v, "wood");
        Start(v, ActionRequest.WaitCook());
        Assert.That(E.ActionProgress(v)!.RemainingMinutes, Is.Null);
        Assert.That(E.ActionProgress(v)!.StopNote, Does.Contain("燃料不足"));
        Assert.That(Run(v), Is.EqualTo(SegmentStatus.Aborted));
        Assert.That(v.Main, Is.Null);
        Assert.That(v.Log.Last(l => l.Kind == "ActionAborted").Text, Does.Contain("等待烹饪停止").And.Contain("熄灭"));
        Assert.That(Count(v, "wood"), Is.EqualTo(woodV), "不自动补木");
        Assert.That(E.PotLiquid(v, vpot.InstanceId), Is.EqualTo("clean_water"), "已完成的净化保留");
        Assert.That(E.PotFood(v, vpot.InstanceId).All(f => f.ItemId == "raw_root" && Proc(v, f).HeatDoneMinutes == 5), Is.True, "受热进度保留，不清零");

        // Q16：完成前才会过原期限、但首段就能受热的新鲜食材取得保护，照样纳入（S11-A16）
        var (w2, wpot) = PotOnFire("clean_water", 1);
        RootsInPot(w2, wpot, 1);
        E.PotFood(w2, wpot.InstanceId).Single().ExpiresAt = w2.GameMinute + 10;
        plan = E.PreviewCookWait(w2);
        Assert.That(plan.Ok, Is.True, plan.Reason);
        Assert.That(plan.Targets.Count, Is.EqualTo(1));
        Assert.That(Proc(w2, E.PotFood(w2, wpot.InstanceId).Single()).HeatProtected, Is.False, "预览不先设保护");

        // 首次受热前就会腐败（脏水要先净化，食材10分钟后到期）：不纳入并写原因
        var (w3, w3pot) = PotOnFire("dirty_water", 2);
        RootsInPot(w3, w3pot, 1);
        E.PotFood(w3, w3pot.InstanceId).Single().ExpiresAt = w3.GameMinute + 10;
        plan = E.PreviewCookWait(w3);
        Assert.That(plan.Excluded.Single(x => !x.Label.Contains("锅")).Reason, Does.Contain("首次受热前就会腐败"));
    }

    /// <summary>S11-A12/S01-A12：中途退出不清进度/预留、不灭火；重新等待从实际剩余继续；读档后剩余按真实进度重算、不多tick；末段燃料恰好够按完成。</summary>
    [Test]
    public void S11_A12_S01_A12_ExitKeepsProgress_RestartAndReload_LastFuelSegmentCompletes()
    {
        var (s, pot) = PotOnFire("clean_water", 2);
        RootsInPot(s, pot, 1);
        var root = E.PotFood(s, pot.InstanceId).Single();
        Start(s, ActionRequest.WaitCook());
        Steps(s, 2);
        var reserved = Proc(s, root).ReservedWaterMl;
        var exit = E.RequestExit(s);
        Assert.That(exit.Ok && !exit.Deferred, Is.True, exit.Reason);
        Assert.That(s.Main, Is.Null, "退出后空闲");
        Assert.That(Proc(s, root).HeatDoneMinutes, Is.EqualTo(10), "受热进度保留");
        Assert.That(Proc(s, root).ReservedWaterMl, Is.EqualTo(reserved), "水预留保留");
        Assert.That(s.Locations["camp"].Facility.FireLit, Is.True, "不灭火");
        Assert.That(root.Owner, Is.EqualTo(ItemInstance.ContainerOwner(pot.InstanceId)), "不取锅、不领取");
        Assert.That(s.Log.Last(l => l.Kind == "ActionCancelled").Text, Does.Contain("退出等待烹饪"));

        // 重新等待：从实际剩余20分钟继续
        Assert.That(E.PreviewCookWait(s).EstimatedMinutes, Is.EqualTo(20));
        Start(s, ActionRequest.WaitCook());
        Steps(s, 1);
        var json = SaveSerializer.Serialize(s);
        var back = SaveSerializer.Deserialize(json, Cfg, out _);
        var prog = E.ActionProgress(back)!;
        Assert.That(prog.RemainingMinutes, Is.EqualTo(15), "读档按真实进度重算");
        Assert.That(SaveSerializer.Serialize(back), Is.EqualTo(json), "估时只读：不推进时间、不改随机");
        var m1 = back.GameMinute;
        Assert.That(Run(back), Is.EqualTo(SegmentStatus.Completed));
        Assert.That(back.GameMinute - m1, Is.EqualTo(15));
        Assert.That(E.PotFood(back, pot.InstanceId).Single().ItemId, Is.EqualTo("boiled_root"));
        Assert.That(E.PotUnits(back, pot.InstanceId), Is.EqualTo(1), "只扣一次水、不出第二份");

        // 末段燃料恰好够：只有 15 分钟燃料与 15 分钟净化
        var (t, tpot) = PotOnFire("dirty_water", 1, fuel: 15);
        Assert.That(E.PreviewCookWait(t).EstimatedMinutes, Is.EqualTo(15));
        Start(t, ActionRequest.WaitCook());
        Assert.That(Run(t), Is.EqualTo(SegmentStatus.Completed), "最后一段已全完成按完成处理");
        Assert.That(E.PotLiquid(t, tpot.InstanceId), Is.EqualTo("clean_water"));
        Assert.That(t.Locations["camp"].Facility.FireLit, Is.False);
    }

    /// <summary>S13-A06/A07：普通等待选项不变；已有主行动/暂停主行动时拒绝等待烹饪；休息/睡眠/等待在段边界退出，保留实际增减。</summary>
    [Test]
    public void S13_A06_A07_ExitRestSleepWait_RejectCookWaitWhenOccupied()
    {
        Assert.That(Cfg.Actions.WaitMinutes, Is.EqualTo(new[] { 30, 60 }), "普通等待选项不变");
        var (s, pot) = PotOnFire("dirty_water", 1);
        Start(s, ActionRequest.Wait(30));
        Assert.That(E.CanStart(s, ActionRequest.WaitCook()), Does.Contain("已有进行中的行动"));
        s.Main!.Status = ActionStatus.Paused;
        Assert.That(E.CanStart(s, ActionRequest.WaitCook()), Does.Contain("先退出原行动"));
        Assert.That(E.RequestExit(s).Ok, Is.True, "暂停时也能退出");
        Assert.That(s.Main, Is.Null);

        foreach (var req in new[] { ActionRequest.Rest(120), ActionRequest.Sleep(480), ActionRequest.Wait(60) })
        {
            var t = New();
            SetStats(t, 20, 80, 80, 5, 50, 60);
            Start(t, req);
            Steps(t, 3);
            var e = t.Stats.Energy;
            var m = t.GameMinute;
            var p = E.ActionProgress(t)!;
            Assert.That(p.Exit, Is.EqualTo(ExitKind.StopAtBoundary));
            Assert.That(p.RemainingMinutes, Is.EqualTo(req.Minutes - 15), "固定时长按未发生时长");
            Assert.That(p.Estimated, Is.False);
            Assert.That(E.RequestExit(t).Ok, Is.True);
            Assert.That(t.Main, Is.Null, "未来时长取消");
            Assert.That((t.Stats.Energy, t.GameMinute), Is.EqualTo((e, m)), "已恢复/消耗保留，不多结算一段");
        }
    }

    /// <summary>S01-A10：段前退出不启动本段；推进一段后退出只结算该段；跳过动画不影响内核段数。</summary>
    [Test]
    public void S01_A10_ExitBeforeOrAfterSegment()
    {
        var s = New();
        E.DebugTeleport(s, "field");
        var m0 = s.GameMinute;
        var rng = s.Rng.State;
        Start(s, ActionRequest.Explore());
        var rngAfterStart = s.Rng.State;
        Assert.That(E.RequestExit(s).Ok, Is.True);
        Assert.That(s.GameMinute, Is.EqualTo(m0), "段前退出：本段不开始");
        Assert.That(s.Locations["field"].Progress, Is.EqualTo(0));

        Start(s, ActionRequest.Explore());
        E.Advance(s);
        Assert.That(E.RequestExit(s).Ok, Is.True);
        Assert.That(s.GameMinute, Is.EqualTo(m0 + 5), "段内退出：只完成当前原子段");
        Assert.That(E.Advance(s).Status, Is.EqualTo(SegmentStatus.Idle), "不启动下一段");
        Assert.That(rngAfterStart, Is.Not.EqualTo(rng), "探索开始锁定随机（原规则）");
    }

    /// <summary>S01-A11：单段自用/点火退出如实完成、不回滚；制作取消返还预留；维护子行动退出回父行动暂停态。</summary>
    [Test]
    public void S01_A11_SingleTransaction_WorkCancel_SubExitBackToPausedParent()
    {
        var s = New();
        SetStats(s, 20, 50, 50, 18, 90, 60);
        Start(s, ActionRequest.UseItem("clean_water"));
        var w = s.Stats.Hydration;
        var r = E.RequestExit(s);
        Assert.That(r.Ok && r.Deferred, Is.True, "已开始结算：本段完成后结束");
        Assert.That(E.ActionProgress(s) is { Exit: ExitKind.CompletesCurrentTransaction }, Is.True);
        Assert.That(s.Stats.Hydration, Is.EqualTo(w), "即时效果不回滚");
        Assert.That(s.Main, Is.Not.Null);
        Run(s);
        Assert.That(s.Main, Is.Null);

        // 点火：同样完成，不退还木材
        E.DebugGive(s, "wood", 1);
        E.DoInstant(s, InstantRequest.AddFuel());
        Start(s, ActionRequest.Ignite(First(s, "lighter").InstanceId));
        Assert.That(E.RequestExit(s).Deferred, Is.True);
        Run(s);
        Assert.That(s.Locations["camp"].Facility.FireLit, Is.True);

        // 工作：退出返还预留材料
        var t = New();
        E.DebugGive(t, "stone", 1);
        E.DebugGive(t, "fiber", 1);
        Start(t, ActionRequest.Craft("knife"));
        E.Advance(t);
        Assert.That(t.Items.Any(i => i.ReservedBy != null), Is.True);
        Assert.That(E.ActionProgress(t)!.Estimated, Is.True, "工作行动剩余为预计");
        Assert.That(E.RequestExit(t).Ok, Is.True);
        Assert.That(t.Items.Any(i => i.ReservedBy != null), Is.False, "预留返还");

        // 子行动退出：回原主行动暂停态
        var u = New();
        E.DebugTeleport(u, "field");
        Start(u, ActionRequest.Explore());
        u.Main!.Status = ActionStatus.Paused;
        Start(u, ActionRequest.Rest(30));
        Assert.That(u.Sub, Is.Not.Null);
        Assert.That(E.RequestExit(u).Ok, Is.True);
        Assert.That(u.Sub, Is.Null);
        Assert.That(u.Main!.Status, Is.EqualTo(ActionStatus.Paused), "父行动仍暂停，不解除占用");
    }

    /// <summary>剩余时间：工作行动按段模拟，与真实完成一致；对话中标记时间未推进且不能退出；战斗中不能退出。</summary>
    [Test]
    public void ActionProgress_WorkEstimateMatchesActual_DialogueAndBattleBlockExit()
    {
        var s = New();
        s.GameMinute = At(1, 17, 50);   // 跨入夜：倍率变化
        Start(s, ActionRequest.Travel("field"));
        var p = E.ActionProgress(s)!;
        Assert.That(p.Estimated, Is.True);
        var m0 = s.GameMinute;
        Run(s);
        Assert.That(s.GameMinute - m0, Is.EqualTo(p.RemainingMinutes));

        var d = NewStory();
        Assert.That(E.ActionProgress(d), Is.Null);
        Assert.That(E.RequestExit(d).Ok, Is.False);
        var b = Fight(14, "stag");
        Assert.That(E.RequestExit(b).Reason, Does.Contain("战斗"));
    }

    /// <summary>R03-A12：首次强制教学首战 K=1；撤退后主动重接同一敌人 K=0.5；同敌、HP 保留，教学奖励唯一不变。</summary>
    [Test]
    public void R03_A12_TutorialK1_ReapproachK05()
    {
        var s = NewStory(7);
        ReadDialogues(s);
        Start(s, ActionRequest.Travel("field"));
        for (var i = 0; i < 200 && s.Main != null; i++) { var r = E.Advance(s); if (r.Status == SegmentStatus.Paused) E.Resume(s); }
        ReadDialogues(s);
        var b = s.Battle!;
        Assert.That(b.Tutorial, Is.True);
        Assert.That(b.Context, Is.EqualTo(GameEngine.FearTutorial));
        Assert.That(Cfg.Panic.Fear.Factor(GameEngine.FearTutorial), Is.EqualTo(1.0));
        Assert.That(b.FearChance, Is.EqualTo(E.FearChance(E.Enemy("shambler").FearChance, b.FearMorale, GameEngine.FearTutorial)).Within(1e-12));
        var tid = s.Story.TutorialEnemyId!.Value;
        for (var i = 0; i < 20 && s.Battle != null && !E.AwaitingPlayer(s); i++) E.BattleStep(s);
        s.Battle!.Enemies[0].Hp = 2.0;
        Force(s, Below(0.0001));
        Assert.That(Cmd(s, "retreat").Ok, Is.True);
        if (s.Battle != null) Assert.Inconclusive("强制撤退未成功");
        ReadDialogues(s);
        Assert.That(E.Approach(s, tid).Ok, Is.True);
        var b2 = s.Battle!;
        Assert.That(b2.Tutorial, Is.True, "仍是同一教学流程");
        Assert.That(b2.Context, Is.EqualTo(GameEngine.FearApproach));
        Assert.That(Cfg.Panic.Fear.Factor(GameEngine.FearApproach), Is.EqualTo(0.5));
        Assert.That(b2.FearChance, Is.EqualTo(E.FearChance(E.Enemy("shambler").FearChance, b2.FearMorale, GameEngine.FearApproach)).Within(1e-12), "主动重接 K=0.5");
        Assert.That(b2.Enemies.Single().WorldId, Is.EqualTo(tid));
        Assert.That(b2.Enemies.Single().Hp, Is.EqualTo(2.0), "HP 保留");
    }

    /// <summary>R05-A09：首战撤退后跨多日、多次进荒地——门控一直保留、重复到达不强制；同敌不巡游、占容量；胜利后才按原条件开放。</summary>
    [Test]
    public void R05_A09_GateHoldsAcrossDays_RepeatedArrivalNotForced()
    {
        var s = NewStory(7);
        ReadDialogues(s);
        Start(s, ActionRequest.Travel("field"));
        for (var i = 0; i < 200 && s.Main != null; i++) { var r = E.Advance(s); if (r.Status == SegmentStatus.Paused) E.Resume(s); }
        ReadDialogues(s);
        var tid = s.Story.TutorialEnemyId!.Value;
        for (var i = 0; i < 20 && s.Battle != null && !E.AwaitingPlayer(s); i++) E.BattleStep(s);
        Force(s, Below(0.0001));
        Assert.That(Cmd(s, "retreat").Ok, Is.True);
        if (s.Battle != null) Assert.Inconclusive("强制撤退未成功");
        ReadDialogues(s);
        // 跨到第4日，多次往返荒地
        for (var day = 0; day < 3; day++)
        {
            SetStats(s, 20, 80, 80, 18, 90, 60);
            s.Body.Immunity = 95;
            s.Body.Infected = false;
            s.Body.VirusLoad = 0;
            Start(s, ActionRequest.Travel("camp"));
            Run(s);
            AdvanceTo(s, s.GameMinute + 1440 - 60);
            SetStats(s, 20, 80, 80, 18, 90, 60);
            Start(s, ActionRequest.Travel("field"));
            Run(s);
            Assert.That(s.Battle, Is.Null, $"第{day + 1}次重复到达不强制入战");
            Assert.That(GameEngine.TutorialGateOpen(s), Is.True, "门控无时限");
            Assert.That(E.FindWorldEnemy(s, tid)!.LocationId, Is.EqualTo("field"), "首战敌人不巡游");
            Assert.That(E.LocalCount(s, "field"), Is.GreaterThanOrEqualTo(1), "仍计入本地容量");
            Assert.That(E.EncounterChance(s, "field", ActionType.Explore), Is.EqualTo(0), "门控期间不随机接敌");
        }
        Assert.That(Clock.Day(s.GameMinute), Is.GreaterThanOrEqualTo(4));
    }
}
