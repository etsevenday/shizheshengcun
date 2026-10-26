using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 基础UI布局与操作修订案 v0.7 第 9 节验收里属于玩法层的部分（U708–U727）。布局、滚轮、页签等纯界面条目在浏览器手测，
/// 记录在 doc/开发计划 v0.4 基础UI布局与操作修订.md 第 6 节。
/// </summary>
public class UiRevisionV07Tests : TestBase
{
    private const string Fire = "facility:camp:fire";

    private ItemInstance Pot(GameState s) => s.Items.First(i => i.ItemId == "pot");

    private void Ok(GameState s, InstantRequest r)
    {
        var res = E.DoInstant(s, r);
        Assert.That(res.Ok, Is.True, res.Reason);
    }

    private static void LightFire(GameState s, int fuel = 120)
    {
        var f = s.Locations["camp"].Facility;
        f.FireLit = true;
        f.FuelMinutes = fuel;
    }

    private void FillWater(GameState s, ItemInstance pot, string water, int n)
    {
        E.DebugGive(s, water, n, "camp");
        var src = s.Items.Where(i => i.ItemId == water && i.Owner == "camp").OrderByDescending(i => i.Count).First();
        Ok(s, InstantRequest.FillPot(src.InstanceId, pot.InstanceId, n));
    }

    private void PutRootsInPot(GameState s, ItemInstance pot, int n)
    {
        E.DebugGive(s, "raw_root", n, "camp");
        Ok(s, InstantRequest.PutFoodInPot(First(s, "raw_root", "camp").InstanceId, pot.InstanceId, n));
    }

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

    private static void RemoveAll(GameState s, string itemId) => s.Items.RemoveAll(i => i.ItemId == itemId);

    /// <summary>燃烧的火堆里放一口锅：2 份净水 + 2 份生块根，水煮 10 分钟。</summary>
    private ItemInstance BoilingPot(GameState s)
    {
        var pot = Pot(s);
        FillWater(s, pot, "clean_water", 2);
        Ok(s, InstantRequest.PutInFire(pot.InstanceId));
        PutRootsInPot(s, pot, 2);
        LightFire(s, 240);
        WaitMinutes(s, 10);
        return pot;
    }

    [Test]
    public void U708_TakeWholePot_DescendantsFollow_OverweightRejectedWithoutResettingProgress()
    {
        var s = New();
        var pot = BoilingPot(s);
        var children = E.PotContents(s, pot.InstanceId).Select(i => i.InstanceId).ToList();
        Assert.That(children, Has.Count.EqualTo(3), "1堆净水与2份生块根");
        var heat = E.AllProcesses(s).Where(p => p.PotInstanceId == pot.InstanceId).ToDictionary(p => p.ProcessId, p => p.HeatDoneMinutes);
        Assert.That(heat.Values.All(h => h == 10), Is.True, "两份水煮各受热10分钟");

        // 事先显示后果：会清零的加工与拾取后重量
        var c = E.TakeOutConsequenceOf(s, pot);
        Assert.That(c.Resets, Has.Count.EqualTo(2));
        Assert.That(c.Text, Does.StartWith("取出后生块根#").And.Contain("水煮受热10分钟").And.EndWith("清零"));
        Assert.That(c.Grams, Is.EqualTo(1000 + 1000 + 600));

        // 随身已接近 30 公斤：整锅拾取整次拒绝，进度不提前清空
        var room = (Cfg.Carry.HardGrams - E.CarriedGrams(s) - c.Grams) / 500 + 1;
        E.DebugGive(s, "stone", room);
        var before = SaveSerializer.Serialize(s);
        var res = E.DoInstant(s, InstantRequest.TakeOut(pot.InstanceId, ItemInstance.Carried));
        Assert.That(res.Ok, Is.False);
        Assert.That(res.Reason, Does.Contain("整次拒绝"));
        Assert.That(SaveSerializer.Serialize(s), Is.EqualTo(before), "失败不改任何状态");
        Assert.That(E.AllProcesses(s).Where(p => p.PotInstanceId == pot.InstanceId).All(p => p.HeatDoneMinutes == 10), Is.True);

        // 取下到地面：全部后代跟随，进度按 v0.4 清零
        Ok(s, InstantRequest.TakeOut(pot.InstanceId, "camp"));
        Assert.That(pot.Owner, Is.EqualTo("camp"));
        Assert.That(E.PotContents(s, pot.InstanceId).Select(i => i.InstanceId), Is.EquivalentTo(children), "水与食材都还在这口锅里");
        Assert.That(E.AllProcesses(s).Any(p => p.PotInstanceId == pot.InstanceId), Is.False);
    }

    [Test]
    public void U709_DropIntoFirePot_HitsThatPot_FullPotRejectsWithoutFallingToRoastSlot()
    {
        var s = New();
        var pot = Pot(s);
        Ok(s, InstantRequest.PutInFire(pot.InstanceId));
        Assert.That(E.FireFreeSlots(s, "camp"), Is.EqualTo(1), "还有一个直烤位空着");
        E.DebugGive(s, "clean_water", 3, "camp");
        E.DebugGive(s, "raw_root", 3, "camp");
        var water = DropSource.Parse("card:camp|clean_water")!;
        var root = DropSource.Parse("card:camp|raw_root")!;
        var fill = E.ResolveDrop(s, water, DropTarget.Parse($"potfill:{pot.InstanceId}")!);
        Assert.That(fill.Ok && fill.Verb == DropVerb.FillPot && fill.Instant!.TargetInstanceId == pot.InstanceId, Is.True, fill.Reason);
        var food = E.ResolveDrop(s, root, DropTarget.Parse($"potfood:{pot.InstanceId}")!);
        Assert.That(food.Ok && food.Verb == DropVerb.PutFood, Is.True, food.Reason);
        Assert.That(food.Label, Does.Contain($"金属锅#{pot.InstanceId}"));

        Ok(s, InstantRequest.FillPot(First(s, "clean_water", "camp").InstanceId, pot.InstanceId, 2));
        Ok(s, InstantRequest.PutFoodInPot(First(s, "raw_root", "camp").InstanceId, pot.InstanceId, 2));
        var potKey = $"card:{Fire}|pot|{pot.InstanceId}";
        foreach (var (src, target) in new[] { (water, $"potfill:{pot.InstanceId}"), (water, potKey), (root, $"potfood:{pot.InstanceId}"), (root, potKey) })
        {
            var i = E.ResolveDrop(s, src, DropTarget.Parse(target)!);
            Assert.That(i.Ok, Is.False, target);
            Assert.That(i.FallThrough, Is.False, "锅满不冒泡成火堆直烤");
            Assert.That(i.Verb, Is.Not.EqualTo(DropVerb.PutInFire));
        }
        Assert.That(E.FireFreeSlots(s, "camp"), Is.EqualTo(1), "直烤位仍空");
    }

    [Test]
    public void U710_ReservedWaterDisabled_TakeOutIdlePotAllowedWithConsequence()
    {
        var s = New();
        var pot = Pot(s);
        FillWater(s, pot, "clean_water", 1);
        Ok(s, InstantRequest.PutInFire(pot.InstanceId));
        PutRootsInPot(s, pot, 1);
        LightFire(s);
        var w = E.PotLiquids(s, pot.InstanceId).Single();
        Assert.That(E.PotFreeWaterUnits(s, w), Is.EqualTo(0), "唯一一份净水被水煮预留");
        var why = E.CanStart(s, ActionRequest.UseInstance(w.InstanceId));
        Assert.That(why, Does.StartWith("500毫升供生块根#").And.EndWith("先取出该食材以停止加工"));
        Assert.That(E.DoInstant(s, InstantRequest.Decant(w.InstanceId, ItemInstance.Carried)).Ok, Is.False, "预留的水不能转移");
        WaitMinutes(s, 10);
        Assert.That(E.TakeOutBlocked(s, pot, ItemInstance.Carried), Is.Null, "空闲时可以取下加工中的锅");
        Assert.That(E.TakeOutConsequenceOf(s, pot).Text, Does.Contain("水煮受热10分钟"));
        Start(s, ActionRequest.Wait(30));
        Assert.That(E.TakeOutBlocked(s, pot, ItemInstance.Carried), Is.EqualTo("行动进行中，不能整理物品"), "不因内嵌展示解锁被占用的实体");
    }

    [Test]
    public void U711_LighterDragged_StartsFiveMinuteIgniteDirectly()
    {
        foreach (var owner in new[] { ItemInstance.Carried, "camp" })
        {
            var s = New();
            var lighter = First(s, "lighter");
            lighter.Owner = owner;
            var drag = E.ResolveDrop(s, DropSource.Parse($"card:{owner}|lighter|{lighter.InstanceId}")!, DropTarget.Parse("fire:camp")!);
            Assert.That(drag.Ok && drag.StartsAction && !drag.IsPreview, Is.True, drag.Reason);
            var res = E.Start(s, drag.Action!.WithRequestId("drag-1"));
            Assert.That(res.Ok, Is.True, res.Reason);
            Assert.That(s.Main!.Type, Is.EqualTo(ActionType.Ignite));
            Assert.That(s.Main.PlannedMinutes, Is.EqualTo(5));
            Assert.That(s.Main.LockedItemIds, Does.Contain(lighter.InstanceId), "绑定拖入的那一把");
            Run(s);
            Assert.That(s.Locations["camp"].Facility.FireLit, Is.True, owner);
        }
    }

    [Test]
    public void U713_SingleToolStillPicked_NoToolConsumesNothing()
    {
        var s = New();
        var opts = E.IgniteToolOptions(s);
        Assert.That(opts, Has.Count.EqualTo(1));
        Assert.That((opts[0].Name, opts[0].Source, opts[0].Usable), Is.EqualTo(($"打火器#{opts[0].InstanceId}", "随身", true)));
        Assert.That(E.CanStart(s, ActionRequest.Ignite()), Is.EqualTo("请选择要使用的打火器"), "只有一把也不自动选择");
        Assert.That(E.IgniteNoToolReason(s), Is.Null);

        RemoveAll(s, "lighter");
        var wood = Count(s, "wood");
        var before = SaveSerializer.Serialize(s);
        Assert.That(E.IgniteToolOptions(s), Is.Empty);
        Assert.That(E.IgniteNoToolReason(s), Is.EqualTo("缺少打火器（需随身或在当前地面）"));
        Assert.That(E.Start(s, ActionRequest.Ignite()).Ok, Is.False);
        Assert.That(Count(s, "wood"), Is.EqualTo(wood), "无工具不消耗");
        Assert.That(SaveSerializer.Serialize(s), Is.EqualTo(before));

        // 其他地块的打火器不出现在列表里
        E.DebugGive(s, "lighter", 1, "field");
        Assert.That(E.IgniteToolOptions(s), Is.Empty);
    }

    [Test]
    public void U714_FuelRules_AutoWoodVisibleBeforehand_FailureDeductsNothing()
    {
        // 0 燃料有木材：事先可见自动投木，完成后剩 55
        var s = New();
        var lighter = First(s, "lighter").InstanceId;
        var p = E.Preview(s, ActionRequest.Ignite(lighter));
        Assert.That(p.Ok, Is.True, p.Reason);
        Assert.That((p.FuelBefore, p.FuelAfterStart, p.FuelAtEnd, p.AutoWood, p.DiesOutAtEnd), Is.EqualTo(((int?)0, (int?)60, (int?)55, true, false)));
        Assert.That(p.AutoWoodInstanceId, Is.EqualTo(First(s, "wood", "camp").InstanceId));
        Assert.That(p.Notes, Does.Contain("将消耗1木材，提供60分钟燃料"));
        var wood = Count(s, "wood");
        Start(s, ActionRequest.Ignite(lighter));
        Assert.That(Count(s, "wood"), Is.EqualTo(wood - 1), "自动投木与开始在同一次提交");
        Run(s);
        Assert.That(s.Locations["camp"].Facility.FuelMinutes, Is.EqualTo(55));

        // 0 燃料无木材：拒绝，不部分扣资源
        var t = New();
        RemoveAll(t, "wood");
        var before = SaveSerializer.Serialize(t);
        var res = E.Start(t, ActionRequest.Ignite(First(t, "lighter").InstanceId));
        Assert.That(res.Reason, Is.EqualTo("请先投入木材"));
        Assert.That(SaveSerializer.Serialize(t), Is.EqualTo(before));

        // 燃料 1—4 分钟拒绝；5 分钟可点但完成即熄灭，预测写明
        foreach (var fuel in new[] { 1, 4 })
        {
            var u = New();
            u.Locations["camp"].Facility.FuelMinutes = fuel;
            Assert.That(E.CanStart(u, ActionRequest.Ignite(First(u, "lighter").InstanceId)), Is.EqualTo("请先投入木材"), $"燃料{fuel}");
            Assert.That(Count(u, "wood"), Is.EqualTo(2));
        }
        var v = New();
        v.Locations["camp"].Facility.FuelMinutes = 5;
        var pv = E.Preview(v, ActionRequest.Ignite(First(v, "lighter").InstanceId));
        Assert.That((pv.AutoWood, pv.FuelAtEnd, pv.DiesOutAtEnd), Is.EqualTo((false, (int?)0, true)));
        Assert.That(pv.Warnings, Has.Some.Contains("点火完成时燃尽熄灭"));
        Start(v, ActionRequest.Ignite(First(v, "lighter").InstanceId));
        Assert.That(Count(v, "wood"), Is.EqualTo(2), "燃料够时不强制另加木材");
    }

    [Test]
    public void U715_ToolGoneRejected_NoSwap_SameRequestIdIgnitesOnce()
    {
        var s = New();
        E.DebugGive(s, "lighter", 1, "camp");
        var chosen = First(s, "lighter", ItemInstance.Carried);
        var other = First(s, "lighter", "camp");
        Assert.That(E.IgniteToolOptions(s).Select(o => o.InstanceId), Is.EqualTo(new[] { chosen.InstanceId, other.InstanceId }), "随身在前");
        s.Items.Remove(chosen);
        var res = E.Start(s, ActionRequest.Ignite(chosen.InstanceId).WithRequestId("pick-1"));
        Assert.That(res.Reason, Is.EqualTo("这把打火器已不在，请重新选择"));
        Assert.That(s.Main, Is.Null, "不暗换另一把");
        other.Owner = "field";
        Assert.That(E.CanStart(s, ActionRequest.Ignite(other.InstanceId)), Is.EqualTo("只能使用随身或当前地面的打火器"));
        Assert.That(E.CanStart(s, ActionRequest.Ignite(First(s, "wood").InstanceId)), Is.EqualTo("这不是打火器"));

        // 双击 / 重复到达：同一 requestId 只点一次
        var t = New();
        var req = ActionRequest.Ignite(First(t, "lighter").InstanceId).WithRequestId("click-7");
        var wood = Count(t, "wood");
        var first = E.Start(t, req);
        Assert.That(first.Ok && !first.Duplicate, Is.True, first.Reason);
        var second = E.Start(t, req);
        Assert.That(second.Ok && second.Duplicate, Is.True, "重复请求返回成功但标记为重复");
        Assert.That(Count(t, "wood"), Is.EqualTo(wood - 1), "不重复扣木材");
        Run(t);
        Assert.That(E.Start(t, req).Duplicate, Is.True, "完成后重发仍不启动第二次");
        Assert.That(t.Main, Is.Null);
    }

    [Test]
    public void U722_NoSelfDropTarget()
    {
        Assert.That(DropTarget.Parse("self"), Is.Null, "左栏自身卡取消，没有隐形投放热区");
        Assert.That(Enum.GetNames(typeof(DropTargetKind)), Has.None.EqualTo("Self"));
        Assert.That(Enum.GetNames(typeof(DropVerb)), Has.None.EqualTo("PreviewUse"));
        Assert.That(GameEngine.NoSelfDropHint, Is.EqualTo("选中物品后在卡片下方使用"));
    }

    [Test]
    public void U723_NoIllegalSelfUse_DisabledShowsActualReason()
    {
        var s = New();
        E.DebugGive(s, "raw_root", 1, "camp");
        E.DebugGive(s, "herb", 1, "camp");
        foreach (var id in new[] { "raw_root", "herb", "wood", "stone", "pot", "lighter" })
            Assert.That(GameEngine.CanSelfUse(E.Item(id)), Is.False, id);
        Assert.That(E.CanStart(s, ActionRequest.UseInstance(First(s, "raw_root", "camp").InstanceId)), Is.EqualTo("生块根需先烹饪，不可直接食用"));

        // 绷带冷却：按钮保留并写原因，人物页与左栏读同一条状态异常（v0.6：满血无出血时包扎无效，先让健康不满）
        E.DebugGive(s, "bandage", 2);
        SetStats(s, 15, 80, 80, 18, 90, 60);
        Start(s, ActionRequest.UseItem("bandage"));
        Run(s);
        var why = E.CanStart(s, ActionRequest.UseItem("bandage"));
        Assert.That(why, Does.StartWith("绷带冷却中，还需"));
        var cond = E.ActiveConditions(s).Single(c => c.Id == "bandageCooldown");
        Assert.That(cond.RemainingMinutes, Is.EqualTo(s.BandageReadyAt - s.GameMinute));
        Assert.That(cond.Effect, Is.EqualTo("冷却结束前草药绷带不回血（仍可止血或压病毒）"), "v0.6：回血冷却只挡回血");

        // 腹泻：剩余时间与效果来自配置
        E.DebugGive(s, "dirty_water", 1);
        Start(s, ActionRequest.UseItem("dirty_water"));
        var d = E.ActiveConditions(s).Single(c => c.Id == "diarrhea");
        Assert.That((d.Name, d.RemainingMinutes, d.Effect, d.Harmful), Is.EqualTo(("腹泻", 720, "每小时额外失水3", true)));
    }

    [Test]
    public void U724_TransferQuantityIgnored_EatsExactlyOneThenFiveMinutes()
    {
        var s = New();
        SetStats(s, 100, 50, 80, 90, 90, 50);
        E.DebugGive(s, "canned_food", 3);
        var head = First(s, "canned_food", ItemInstance.Carried);
        var p = E.Preview(s, ActionRequest.UseInstance(head.InstanceId));
        Assert.That(p.StatsAfterImmediate!.Satiety, Is.EqualTo(85).Within(1e-9), "先扣1份并应用即时效果");
        Assert.That(p.EstimatedMinutes, Is.EqualTo(5));
        var minute = s.GameMinute;
        var total = Count(s, "canned_food");
        Start(s, ActionRequest.UseInstance(head.InstanceId));
        Assert.That(Count(s, "canned_food"), Is.EqualTo(total - 1), "只食用1份");
        Run(s);
        Assert.That(s.GameMinute, Is.EqualTo(minute + 5));
        Assert.That(s.Main, Is.Null, "没有批量串行");
        Assert.That(Count(s, "canned_food"), Is.EqualTo(total - 1));

        // 溢出写进预测
        SetStats(s, 100, 90, 80, 90, 90, 50);
        Assert.That(E.Preview(s, ActionRequest.UseItem("canned_food")).OverflowText, Is.EqualTo("接近上限：饱食溢出25.00，溢出部分浪费。"));
    }

    [Test]
    public void U725_PausedMain_SupplyAndIgniteAsSub_MainStaysPaused()
    {
        var s = New();
        SetStats(s, 100, 80, 20.2, 90, 90, 60);
        Start(s, ActionRequest.Clear());
        Assert.That(Run(s, autoResume: false), Is.EqualTo(SegmentStatus.Paused));
        var mainId = s.Main!.ActionId;
        Start(s, ActionRequest.UseItem("canned_food"));
        Assert.That(s.Sub!.Type, Is.EqualTo(ActionType.Use));
        Run(s, autoResume: false);
        Assert.That(s.Sub, Is.Null);
        Assert.That((s.Main!.ActionId, s.Main.Status), Is.EqualTo((mainId, ActionStatus.Paused)), "补给后主行动仍暂停");

        Start(s, ActionRequest.Ignite(First(s, "lighter").InstanceId));
        Assert.That(s.Sub!.Type, Is.EqualTo(ActionType.Ignite));
        Run(s, autoResume: false);
        Assert.That(s.Locations["camp"].Facility.FireLit, Is.True);
        Assert.That((s.Main!.ActionId, s.Main.Status), Is.EqualTo((mainId, ActionStatus.Paused)), "点火子行动后主行动仍暂停，等待玩家继续");

        // 主行动执行中不能插入点火或补给
        var t = New();
        Start(t, ActionRequest.Wait(30));
        Assert.That(E.CanStart(t, ActionRequest.Ignite(First(t, "lighter").InstanceId)), Is.EqualTo("已有进行中的行动"));
        Assert.That(E.CanStart(t, ActionRequest.UseItem("canned_food")), Is.EqualTo("已有进行中的行动"));
    }

    [Test]
    public void U726_GroundEatDirect_PotWaterUsesThatPotOnly()
    {
        var s = New();
        var grams = E.CarriedGrams(s);
        var ground = First(s, "canned_food", "camp");
        var groundCount = Count(s, "canned_food", "camp");
        Start(s, ActionRequest.UseInstance(ground.InstanceId));
        Assert.That(E.CarriedGrams(s), Is.EqualTo(grams), "地面直接食用不临时入包");
        Assert.That(Count(s, "canned_food", "camp"), Is.EqualTo(groundCount - 1));
        Assert.That(Count(s, "canned_food", ItemInstance.Carried), Is.EqualTo(0));
        Run(s);

        // 两口锅：只消耗所选那口锅的可用水，不向别的锅或散水补取
        var potA = Pot(s);
        E.DebugGive(s, "pot", 1, "camp");
        var potB = s.Items.First(i => i.ItemId == "pot" && i.InstanceId != potA.InstanceId);
        FillWater(s, potA, "clean_water", 1);
        FillWater(s, potB, "clean_water", 2);
        var loose = Count(s, "clean_water", "camp");
        var bWater = E.PotLiquids(s, potB.InstanceId).Single();
        Start(s, ActionRequest.UseInstance(bWater.InstanceId));
        Assert.That(E.PotUnits(s, potB.InstanceId), Is.EqualTo(1));
        Assert.That(E.PotUnits(s, potA.InstanceId), Is.EqualTo(1));
        Assert.That(Count(s, "clean_water", "camp"), Is.EqualTo(loose), "散装净水不动");
    }

    [Test]
    public void U727_BrowsingQueriesDoNotAdvanceTimeOrProcesses()
    {
        var s = New();
        var pot = BoilingPot(s);
        var before = SaveSerializer.Serialize(s);
        var minute = s.GameMinute;
        E.IgniteToolOptions(s);
        E.IgniteNoToolReason(s);
        E.TakeOutConsequenceOf(s, pot);
        E.ActiveConditions(s);
        E.Preview(s, ActionRequest.Wait(30));
        E.Preview(s, ActionRequest.UseItem("canned_food"));
        E.ResolveDrop(s, DropSource.Parse($"card:{Fire}|pot|{pot.InstanceId}")!, DropTarget.Parse("zone:carried")!);
        E.CanStart(s, ActionRequest.Explore());
        Assert.That(s.GameMinute, Is.EqualTo(minute));
        Assert.That(SaveSerializer.Serialize(s), Is.EqualTo(before), "滚动、查看、切页签只调用只读查询");
    }

    /// <summary>
    /// U728 战斗中操作自用按钮：走战斗校验与 AP（绷带/抗生素/特效药 2AP），不绕到非战斗 5 分钟流程；食物饮水战斗中禁用。
    /// 战斗已按用户 2026-10-26 拍板启用，界面卡下按钮改发这条战斗指令（CardActions → PickBattleAction）。
    /// </summary>
    [Test]
    public void U728_CombatSelfUseGoesThroughCombatValidation()
    {
        var s = New(28);
        E.DebugTeleport(s, "field");
        s.GameMinute = At(2, 9, 0);
        s.SpawnNextCheck = int.MaxValue;
        s.PatrolNextCheck = int.MaxValue;
        s.WorldEnemies.Clear();
        var stag = E.DebugSpawnEnemy(s, "stag", "field");
        Assert.That(E.Approach(s, stag.Id).Ok, Is.True);
        var b = s.Battle!;
        b.Queue.Remove(0);
        b.Queue.Insert(0, 0);
        b.Cursor = 0;
        E.BattleStep(s);
        Assert.That(E.AwaitingPlayer(s), Is.True);
        E.DebugGive(s, "bandage", 1);
        E.DebugGive(s, "canned_food", 1);
        SetStats(s, 12, 80, 80, 18, 90, 60);
        var m0 = s.GameMinute;
        var r = E.Start(s, ActionRequest.UseItem("bandage"));
        Assert.That(r.Ok, Is.False, "旧的5分钟自用路径在战斗中拒绝");
        Assert.That(r.Reason, Does.Contain("战斗"));
        Assert.That(E.Start(s, ActionRequest.UseItem("canned_food")).Ok, Is.False, "战斗中不能进食");
        var bandage = First(s, "bandage", ItemInstance.Carried);
        Assert.That(E.ExecuteCommand(s, BattleCommand.Of("bandage", null, bandage.InstanceId)).Ok, Is.True);
        Assert.That(s.Stats.Health, Is.GreaterThan(12), "效果在确认时应用");
        Assert.That(s.GameMinute, Is.EqualTo(m0), "不另跳5分钟");
        Assert.That(E.AwaitingPlayer(s), Is.False, "用掉2AP");
    }
}
