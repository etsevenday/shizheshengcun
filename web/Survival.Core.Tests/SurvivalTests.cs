using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>基础求生案 08 验收用例 S01–S12（S09/S11/S12 见 AlertDeathSaveTests）。</summary>
public class SurvivalTests : TestBase
{
    [Test]
    public void S01_InitialWait60()
    {
        var s = New();
        Start(s, ActionRequest.Wait(60));
        Assert.That(Run(s), Is.EqualTo(SegmentStatus.Completed));
        Assert.That(s.GameMinute, Is.EqualTo(540));
        // S12-A01（Q7 非战斗负率×0.8）：N−1.2/W−2.4/S−4/M−0.4，E/H不变
        Assert.That(s.Stats.Satiety, Is.EqualTo(80 - 1.2).Within(1e-9));
        Assert.That(s.Stats.Hydration, Is.EqualTo(80 - 2.4).Within(1e-9));
        Assert.That(s.Stats.Wakefulness, Is.EqualTo(90 - 4).Within(1e-9));
        Assert.That(s.Stats.Morale, Is.EqualTo(60 - 0.4).Within(1e-9));
        Assert.That(s.Stats.Energy, Is.EqualTo(18), "v0.6 初始体力18，等待体力变化0");
        Assert.That(s.Stats.Health, Is.EqualTo(20), "v0.6 初始健康20");
    }

    [Test]
    public void S02_ZeroSatietyAndHydration_Health2PerHour()
    {
        // v0.6 §12：零饱食0.4、零水分1.6、零清醒0.4每小时（旧值×0.2）
        var s = New();
        SetStats(s, 20, 0, 0, 18, 90, 60);
        Start(s, ActionRequest.Wait(60));
        Run(s);
        Assert.That(s.Stats.Health, Is.EqualTo(18).Within(1e-6));

        var s2 = New();
        SetStats(s2, 20, 0, 0, 18, 0, 60);
        Start(s2, ActionRequest.Wait(60));
        Run(s2);
        Assert.That(s2.Stats.Health, Is.EqualTo(17.6).Within(1e-6));
    }

    [Test]
    public void S03_DrinkCleanWater_Then5MinWait()
    {
        var s = New();
        SetStats(s, 100, 80, 50, 90, 90, 60);
        Start(s, ActionRequest.UseItem("clean_water"));
        Assert.That(s.Stats.Hydration, Is.EqualTo(80), "即时生效");
        Assert.That(Run(s), Is.EqualTo(SegmentStatus.Completed));
        Assert.That(s.Stats.Hydration, Is.EqualTo(79.80).Within(1e-9), "S12-A03：W50喝净水先+30再按新wait率推进5分钟");
        Assert.That(s.GameMinute, Is.EqualTo(485));
        Assert.That(Count(s, "clean_water"), Is.EqualTo(1));
    }

    [Test]
    public void S04_ShelteredRest1h()
    {
        var s = New();
        s.Locations["camp"].Facility.ShelterBuilt = true;
        SetStats(s, 10, 80, 80, 10, 80, 60);
        Start(s, ActionRequest.Rest(60));
        Run(s);
        Assert.That(s.Stats.Energy, Is.EqualTo(16).Within(1e-9), "休息+6/小时");
        Assert.That(s.Stats.Health, Is.EqualTo(10.2).Within(1e-9), "遮蔽健康恢复+0.2/小时");
        // S13-A01：S76、N78.8/W77.6（Q7 新 rest 负率）
        Assert.That(s.Stats.Wakefulness, Is.EqualTo(76).Within(1e-9));
        Assert.That(s.Stats.Satiety, Is.EqualTo(78.8).Within(1e-9));
        Assert.That(s.Stats.Hydration, Is.EqualTo(77.6).Within(1e-9));
        Assert.That(s.Stats.Morale, Is.EqualTo(62).Within(1e-9), "营地遮蔽棚休息每小时+2精神");
    }

    [Test]
    public void S05_BedShelteredSleep_LowMorale()
    {
        var s = New();
        var f = s.Locations["camp"].Facility;
        f.ShelterBuilt = true;
        f.BedBuilt = true;
        SetStats(s, 16, 80, 80, 10, 40, 10);
        Start(s, ActionRequest.Sleep(120));
        Steps(s, 12);
        Assert.That(s.Main, Is.Not.Null, "睡眠2小时尚未结束");
        Assert.That(s.Stats.Energy, Is.EqualTo(12.5).Within(1e-9), "(4+1)×0.5=2.5");
        Assert.That(s.Stats.Wakefulness, Is.EqualTo(52).Within(1e-9));
        Assert.That(s.Stats.Morale, Is.EqualTo(11).Within(1e-9));
        Assert.That(s.Stats.Health, Is.EqualTo(16).Within(1e-9), "精神低于20不自然恢复");
    }

    [Test]
    public void S06_FactorsCappedAt2()
    {
        var s = New();
        E.DebugTeleport(s, "field");
        E.DebugUnlockEdge(s, "field__stream");
        s.GameMinute = At(1, 22, 0);
        E.DebugGive(s, "stone", 50); // 25 公斤
        SetStats(s, 20, 80, 80, 3, 90, 60);
        Assert.That(E.WorkFactor(s, ActionType.Travel), Is.EqualTo(2.0), "夜间×负重×低体力（E<4）封顶2");
        Start(s, ActionRequest.Travel("stream"));
        Run(s);
        Assert.That(s.GameMinute, Is.EqualTo(At(1, 22, 40)), "20分钟移动在倍率2.0下需40分钟");
        Assert.That(s.CurrentLocation, Is.EqualTo("stream"));
        Assert.That(s.Stats.Energy, Is.EqualTo(3 - 1.28 * 40 / 60.0).Within(1e-9), "按实际40分钟计算消耗（S12 §8 移动−1.28/h）");
    }

    [Test]
    public void S07_MidnightRefillAndExpiry_OncePerDay_AllLocations()
    {
        var s = New();
        s.Resources["roots"].Available = 1;
        s.Resources["roots"].Discovered = true;
        s.Resources["wood"].Available = 2;
        s.GameMinute = At(1, 23, 30);
        E.DebugGive(s, "berry", 2, "camp");
        var berry = First(s, "berry", "camp");
        berry.ExpiresAt = At(2, 0, 0);

        Start(s, ActionRequest.Wait(30));
        Steps(s, 5);
        Assert.That(E.IsSpoiled(s, berry), Is.False, "23:55 尚未到期");
        Steps(s, 1);
        Assert.That(s.GameMinute, Is.EqualTo(At(2, 0, 0)));
        Assert.That(E.IsSpoiled(s, berry), Is.True, "00:00 到期");
        Assert.That(s.Resources["roots"].Available, Is.EqualTo(4), "农野荒地块根补3");
        Assert.That(s.Resources["wood"].Available, Is.EqualTo(7), "非当前地点同样补充");

        Start(s, ActionRequest.Wait(60));
        Run(s);
        Assert.That(s.Resources["roots"].Available, Is.EqualTo(4), "同一天不重复补充");
        Assert.That(s.Resources["stones"].Available, Is.EqualTo(8), "石块不补充且不超上限");
    }

    [Test]
    public void S08_Diarrhea_Extra3HydrationPerHour_Expires12h()
    {
        var s = New();
        E.DebugGive(s, "dirty_water", 1);
        SetStats(s, 100, 80, 50, 90, 90, 60);
        Start(s, ActionRequest.UseItem("dirty_water"));
        Assert.That(s.DiarrheaUntil, Is.EqualTo(480 + 720));
        Run(s);
        var w0 = s.Stats.Hydration;
        Start(s, ActionRequest.Wait(60));
        Run(s);
        Assert.That(w0 - s.Stats.Hydration, Is.EqualTo(5.4).Within(1e-9), "普通2.4加腹泻3（腹泻额外水耗不缩放）");

        s.GameMinute = 480 + 720;
        var w1 = s.Stats.Hydration;
        Start(s, ActionRequest.Wait(60));
        Run(s);
        Assert.That(w1 - s.Stats.Hydration, Is.EqualTo(2.4).Within(1e-9), "12小时后消退");
    }

    [Test]
    public void S10_FireExactly5Minutes_FinishesBoilWater_4Minutes_NoHeat()
    {
        // v0.4：烧水是火堆上锅的自动进程；段首燃料恰好5可完整加热本段，不足5则本段不加热（02 案第 7 节）
        foreach (var (fuel, expectDone) in new[] { (5, true), (4, false) })
        {
            var s = New();
            var f = s.Locations["camp"].Facility;
            var potItem = First(s, "pot");
            E.DebugGive(s, "dirty_water", 2, "camp");
            E.DoInstant(s, InstantRequest.FillPot(First(s, "dirty_water", "camp").InstanceId, potItem.InstanceId, 2));
            E.DoInstant(s, InstantRequest.PutInFire(potItem.InstanceId));
            E.ProcessOf(s, potItem)!.HeatDoneMinutes = 10;
            f.FireLit = true;
            f.FuelMinutes = fuel;
            Start(s, ActionRequest.Wait(30));
            var r = E.Advance(s);
            var pot = "pot:" + potItem.InstanceId;
            Assert.That(r.Status, Is.EqualTo(SegmentStatus.Running), "设施加工不暂停角色的等待");
            Assert.That(s.GameMinute, Is.EqualTo(485));
            Assert.That(f.FireLit, Is.False, "本段末燃尽");
            Assert.That(f.FuelMinutes, Is.EqualTo(0));
            if (expectDone)
            {
                Assert.That(Count(s, "clean_water", pot), Is.EqualTo(2), "净水留在原锅");
                Assert.That(Count(s, "clean_water", "camp"), Is.EqualTo(2), "地面只有开局的净水");
                Assert.That(s.Progress.Flags, Does.Contain("boiled"));
            }
            else
            {
                Assert.That(Count(s, "dirty_water", pot), Is.EqualTo(2));
                Assert.That(E.ProcessOf(s, potItem)!.HeatDoneMinutes, Is.EqualTo(10), "段首不足5不给部分受热");
            }
        }
    }
}
