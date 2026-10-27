using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>地图清理案 v0.3 第 9 节 C01–C08、C10–C13、C15。</summary>
public class ClearTests : TestBase
{
    private GameState AtWoods(int minute = 480)
    {
        var s = New();
        E.DebugTeleport(s, "woods");
        s.GameMinute = minute;
        foreach (var l in s.Locations.Values) l.ClearUpdatedMinute = minute;
        return s;
    }

    private LocationState Woods(GameState s) => s.Locations["woods"];

    [Test]
    public void NewGame_AllLocationsStartFull()
    {
        var s = New();
        foreach (var l in Cfg.Locations)
        {
            Assert.That(s.Locations[l.Id].ClearCharges, Is.EqualTo(l.Clear.InitialCharges), l.Id);
            Assert.That(s.Locations[l.Id].ClearRefillProgress, Is.EqualTo(0), l.Id);
            Assert.That(s.Locations[l.Id].ClearUpdatedMinute, Is.EqualTo(480), l.Id);
        }
    }

    [Test]
    public void C01_UnexploredOrFullyExplored_BothClearable_ProgressUnchanged()
    {
        var s = AtWoods();
        Assert.That(E.CanStart(s, ActionRequest.Clear()), Is.Null, "探索度0也可清理");
        Start(s, ActionRequest.Clear());
        Run(s);
        Assert.That(Woods(s).Progress, Is.EqualTo(0), "不改探索度");

        var c = New();
        Assert.That(c.Locations["camp"].Progress, Is.EqualTo(100));
        Assert.That(E.CanStart(c, ActionRequest.Clear()), Is.Null, "营地初始100%也能清理");
        var edges = c.UnlockedEdges.Count;
        Start(c, ActionRequest.Clear());
        Run(c);
        Assert.That(c.Locations["camp"].Progress, Is.EqualTo(100));
        Assert.That(c.UnlockedEdges.Count, Is.EqualTo(edges), "不发现任何东西");
    }

    [Test]
    public void C02_Woods4of4_Immediately3_Then20of240()
    {
        var s = AtWoods();
        Start(s, ActionRequest.Clear());
        Assert.That(Woods(s).ClearCharges, Is.EqualTo(3), "开始即扣");
        Assert.That(Woods(s).ClearRefillProgress, Is.EqualTo(0));
        Assert.That(Run(s), Is.EqualTo(SegmentStatus.Completed));
        Assert.That(s.GameMinute, Is.EqualTo(At(1, 8, 20)));
        Assert.That(Woods(s).ClearRefillProgress, Is.EqualTo(20));
        Assert.That(E.NextClearRefillMinutes(s, "woods"), Is.EqualTo(220));
        Assert.That(E.ClearChargesText(s, "woods"), Is.EqualTo("次数3/4 · 距恢复1次3小时40分"));
        AdvanceTo(s, At(1, 12, 0));
        Assert.That(Woods(s).ClearCharges, Is.EqualTo(4), "12:00回到4/4");
        Assert.That(Woods(s).ClearRefillProgress, Is.EqualTo(0), "满后进度清0");
        Assert.That(E.ClearChargesText(s, "woods"), Is.EqualTo("4/4·已满"));
    }

    [Test]
    public void C03_TwoSpends20MinutesApart_SerialRefill()
    {
        var s = AtWoods();
        Start(s, ActionRequest.Clear());
        Run(s);
        Start(s, ActionRequest.Clear());
        Assert.That(Woods(s).ClearCharges, Is.EqualTo(2));
        Assert.That(Woods(s).ClearRefillProgress, Is.EqualTo(20), "原20分钟进度不重置");
        Run(s);
        AdvanceTo(s, At(1, 11, 55));
        Assert.That(Woods(s).ClearCharges, Is.EqualTo(2));
        AdvanceTo(s, At(1, 12, 0));
        Assert.That(Woods(s).ClearCharges, Is.EqualTo(3), "12:00恢复到3/4，不是两次一起恢复");
        AdvanceTo(s, At(1, 15, 55));
        Assert.That(Woods(s).ClearCharges, Is.EqualTo(3));
        AdvanceTo(s, At(1, 16, 0));
        Assert.That(Woods(s).ClearCharges, Is.EqualTo(4));
    }

    [Test]
    public void C04_CancelAfter5Minutes_NoLoot_ChargeNotRefunded()
    {
        var s = AtWoods();
        var before = s.Stats.Energy;
        var items = s.Items.Count;
        Start(s, ActionRequest.Clear());
        ForceLoot(s, "wood");
        Steps(s, 1);
        Assert.That(E.Cancel(s).Ok, Is.True);
        Assert.That(s.Items.Count, Is.EqualTo(items), "无物资");
        Assert.That(Woods(s).ClearCharges, Is.EqualTo(3), "次数不退");
        Assert.That(Woods(s).ClearRefillProgress, Is.EqualTo(5), "恢复累计5分钟");
        Assert.That(s.GameMinute, Is.EqualTo(485));
        Assert.That(s.Stats.Energy, Is.EqualTo(before - 1.92 * 5 / 60).Within(1e-9), "身体消耗保留（S12 §8 清理−1.92/小时）");
        Assert.That(Woods(s).Progress, Is.EqualTo(0), "没有探索进度");
    }

    [Test]
    public void C05_ZeroChargesFiveMinutesShort_StartAfterWorldAdvances()
    {
        var s = AtWoods();
        Woods(s).ClearCharges = 0;
        Woods(s).ClearRefillProgress = 235;
        var why = E.CanStart(s, ActionRequest.Clear());
        Assert.That(why, Is.EqualTo("清理次数0/4，5分钟后恢复1次"));
        Assert.That(E.Start(s, ActionRequest.Clear()).Ok, Is.False);
        Assert.That(s.GameMinute, Is.EqualTo(480), "拒绝不耗时");
        AdvanceTo(s, 485);
        Assert.That(E.CanStart(s, ActionRequest.Clear()), Is.Null);
    }

    [Test]
    public void C06_FullForLongThenSpend_NoStoredProgress()
    {
        var s = AtWoods();
        AdvanceTo(s, At(1, 18, 0));
        Start(s, ActionRequest.Clear());
        Assert.That(Woods(s).ClearCharges, Is.EqualTo(3));
        Assert.That(Woods(s).ClearRefillProgress, Is.EqualTo(0));
        Assert.That(E.NextClearRefillMinutes(s, "woods"), Is.EqualTo(240), "不能立即回满");
    }

    [Test]
    public void C07_TravelAndMidnight_OnlyWorldMinutes_OfflineNoRefill()
    {
        var s = AtWoods(At(1, 22, 0));
        Woods(s).ClearCharges = 2;
        // 切图：离开树林到荒地，树林次数照样按世界分钟恢复
        E.DebugUnlockEdge(s, "field__woods");
        Start(s, ActionRequest.Travel("field"));
        Run(s);
        AdvanceTo(s, At(2, 2, 0));
        Assert.That(Woods(s).ClearCharges, Is.EqualTo(3), "跨午夜只按240分钟恢复1次，没有午夜重置");
        Assert.That(Woods(s).ClearRefillProgress, Is.EqualTo(0));
        Assert.That(E.ClearChargesAt(s, "woods"), Is.EqualTo((3, 0)), "实时更新与重新访问时补算结果相同");

        // 离线：存档后不推进时间，读档次数不变
        var t = SaveSerializer.Deserialize(SaveSerializer.Serialize(s));
        Assert.That(t.Locations["woods"].ClearCharges, Is.EqualTo(3));
        Assert.That(E.ClearChargesAt(t, "woods"), Is.EqualTo((3, 0)));
    }

    [Test]
    public void LazyAndLiveRefill_GiveSameResult()
    {
        // 02 案 3.1：按 5 分钟段实时更新与重新访问时补算，结果必须相同
        var s = AtWoods();
        Woods(s).ClearCharges = 1;
        Woods(s).ClearRefillProgress = 100;
        var lazy = s.Clone();
        AdvanceTo(s, At(1, 18, 25));
        lazy.GameMinute = At(1, 18, 25);
        Assert.That(E.ClearChargesAt(lazy, "woods"), Is.EqualTo((Woods(s).ClearCharges, Woods(s).ClearRefillProgress)));
    }

    [Test]
    public void C08_ForcedEmpty_NothingGained_ChargeSpent_ClearLog()
    {
        var s = AtWoods();
        var items = s.Items.Sum(i => i.Count);
        Start(s, ActionRequest.Clear());
        ForceLoot(s, null);
        Assert.That(Run(s), Is.EqualTo(SegmentStatus.Completed));
        Assert.That(s.Items.Sum(i => i.Count), Is.EqualTo(items));
        Assert.That(Woods(s).ClearCharges, Is.EqualTo(3));
        Assert.That(s.Log.Any(l => l.Text == "清理完成：本次未找到可用物资；已消耗1次清理机会。"), Is.True);
        Assert.That(s.Log.Any(l => l.Text.Contains("失败")), Is.False, "空结果不叫失败");
    }

    [Test]
    public void ClearWithLoot_LogAndGround()
    {
        var s = AtWoods();
        Start(s, ActionRequest.Clear());
        ForceLoot(s, "wood");
        Run(s);
        Assert.That(Count(s, "wood", "woods"), Is.EqualTo(1));
        Assert.That(s.Log.Any(l => l.Text == "清理完成：木材+1，已放在树林边缘地面。"), Is.True);
    }

    [Test]
    public void C10_ThresholdPause_SupplyReload_SamePlanNoRedraw()
    {
        var s = AtWoods();
        E.DebugGive(s, "clean_water", 1);
        SetStats(s, 100, 80, 20.2, 90, 90, 60);
        Start(s, ActionRequest.Clear());
        var roll = s.Main!.LootRoll;
        var item = s.Main.LootItemId;
        Assert.That(Run(s, autoResume: false), Is.EqualTo(SegmentStatus.Paused));
        var work = s.Main!.WorkDone;
        Start(s, ActionRequest.UseItem("clean_water"));
        Run(s);
        var t = SaveSerializer.Deserialize(SaveSerializer.Serialize(s));
        Assert.That(t.Main!.LootRoll, Is.EqualTo(roll));
        Assert.That(t.Main.LootItemId, Is.EqualTo(item));
        Assert.That(t.Main.WorkDone, Is.EqualTo(work));
        Assert.That(t.Locations["woods"].ClearCharges, Is.EqualTo(3), "已扣次数保留，不重复扣");
        var rng = t.Rng.State;
        E.Resume(t);
        Assert.That(Run(t), Is.EqualTo(SegmentStatus.Completed));
        Assert.That(t.Rng.State, Is.EqualTo(rng), "完成不重抽");
        Assert.That(t.Locations["woods"].ClearCharges, Is.EqualTo(3));
        if (item != null) Assert.That(Count(t, item, "woods"), Is.EqualTo(1));
    }

    [Test]
    public void C11_ResourceStockZero_ClearStillYields_StockUntouched()
    {
        var s = AtWoods();
        E.DebugSetProgress(s, "woods", 25);
        s.Resources["wood"].Available = 0;
        Start(s, ActionRequest.Clear());
        ForceLoot(s, "wood");
        Run(s);
        Assert.That(Count(s, "wood", "woods"), Is.EqualTo(1));
        Assert.That(s.Resources["wood"].Available, Is.EqualTo(0), "原库存仍0");
        Assert.That(s.Resources["wood"].Reserved, Is.EqualTo(0));
    }

    [Test]
    public void C12_Factor1_5_Takes30Minutes_FullBodyCost()
    {
        // 夜间无照明 ×1.5：20基础分钟需30世界分钟，按真实流逝扣体力与水分
        var s = AtWoods(At(1, 22, 0));
        var e0 = s.Stats.Energy;
        var w0 = s.Stats.Hydration;
        Assert.That(E.WorkFactor(s, ActionType.Clear), Is.EqualTo(1.5));
        Start(s, ActionRequest.Clear());
        Run(s);
        Assert.That(s.GameMinute - At(1, 22, 0), Is.EqualTo(30));
        Assert.That(e0 - s.Stats.Energy, Is.EqualTo(0.96).Within(1e-9), "按实际30分钟×1.92/小时（S12 §8）");
        Assert.That(w0 - s.Stats.Hydration, Is.EqualTo(1.8).Within(1e-9));

        var t = AtWoods();
        SetStats(t, 20, 80, 80, 3.9, 90, 60);
        Assert.That(E.WorkFactor(t, ActionType.Clear), Is.EqualTo(1.25));
        Start(t, ActionRequest.Clear());
        Run(t);
        Assert.That(t.GameMinute - 480, Is.EqualTo(25), "×1.25 需25世界分钟");
    }

    [Test]
    public void C13_Backpack30kg_LootStaysOnGround()
    {
        var s = AtWoods();
        E.DebugGive(s, "stone", 57);
        E.DebugGive(s, "fiber", 2);
        var carried = E.CarriedGrams(s);
        Start(s, ActionRequest.Clear());
        ForceLoot(s, "stone");
        Run(s);
        Assert.That(E.CarriedGrams(s), Is.EqualTo(carried));
        Assert.That(Count(s, "stone", "woods"), Is.EqualTo(1));
    }

    [Test]
    public void C15_NoRemoteClear_RepeatStartSpendsOnce()
    {
        var s = New();
        // 只能清理角色所在地点：请求不带地点，结果只作用于当前地点
        Start(s, ActionRequest.Clear());
        Assert.That(s.Main!.TargetId, Is.EqualTo("camp"));
        Assert.That(E.Start(s, ActionRequest.Clear()).Ok, Is.False, "重复提交被拒绝");
        Assert.That(s.Locations["camp"].ClearCharges, Is.EqualTo(2));
        Assert.That(s.Locations["field"].ClearCharges, Is.EqualTo(4), "其他地点不受影响");
    }

    [Test]
    public void Clear_IsWork_ZeroEnergyHurts()
    {
        var s = AtWoods();
        SetStats(s, 20, 80, 80, 0, 90, 60);
        Start(s, ActionRequest.Clear());
        Run(s);
        Assert.That(s.Stats.Health, Is.LessThan(20), "0体力工作伤害适用于清理");
    }

    [Test]
    public void Clear_Preview_ShowsOddsChargesAndGround()
    {
        var s = AtWoods();
        var p = E.Preview(s, ActionRequest.Clear());
        Assert.That(p.Ok, Is.True, p.Reason);
        Assert.That(p.IsWork, Is.True);
        Assert.That(p.BaseMinutes, Is.EqualTo(20));
        Assert.That(p.EstimatedMinutes, Is.EqualTo(20));
        Assert.That(p.ClearCharges, Is.EqualTo(4));
        Assert.That(p.ClearCap, Is.EqualTo(4));
        Assert.That(p.LootOdds.Sum(x => x.Percent), Is.EqualTo(100));
        Assert.That(p.Notes.Any(n => n.Contains("产物留在当前地面")), Is.True);
        Assert.That(s.Locations["woods"].ClearCharges, Is.EqualTo(4), "预览不改状态");
    }
}
