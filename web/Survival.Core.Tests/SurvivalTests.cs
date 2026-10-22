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
        Assert.That(s.Stats.Satiety, Is.EqualTo(80 - 1.5).Within(1e-9));
        Assert.That(s.Stats.Hydration, Is.EqualTo(80 - 3).Within(1e-9));
        Assert.That(s.Stats.Wakefulness, Is.EqualTo(90 - 5).Within(1e-9));
        Assert.That(s.Stats.Morale, Is.EqualTo(60 - 0.5).Within(1e-9));
        Assert.That(s.Stats.Energy, Is.EqualTo(90));
        Assert.That(s.Stats.Health, Is.EqualTo(100));
    }

    [Test]
    public void S02_ZeroSatietyAndHydration_Health10PerHour()
    {
        var s = New();
        SetStats(s, 100, 0, 0, 90, 90, 60);
        Start(s, ActionRequest.Wait(60));
        Run(s);
        Assert.That(s.Stats.Health, Is.EqualTo(90).Within(1e-6));

        var s2 = New();
        SetStats(s2, 100, 0, 0, 90, 0, 60);
        Start(s2, ActionRequest.Wait(60));
        Run(s2);
        Assert.That(s2.Stats.Health, Is.EqualTo(88).Within(1e-6));
    }

    [Test]
    public void S03_DrinkCleanWater_Then5MinWait()
    {
        var s = New();
        SetStats(s, 100, 80, 50, 90, 90, 60);
        Start(s, ActionRequest.UseItem("clean_water"));
        Assert.That(s.Stats.Hydration, Is.EqualTo(80), "即时生效");
        Assert.That(Run(s), Is.EqualTo(SegmentStatus.Completed));
        Assert.That(s.Stats.Hydration, Is.EqualTo(79.75).Within(1e-9));
        Assert.That(s.GameMinute, Is.EqualTo(485));
        Assert.That(Count(s, "clean_water"), Is.EqualTo(1));
    }

    [Test]
    public void S04_ShelteredRest1h()
    {
        var s = New();
        s.Locations["camp"].Facility.ShelterBuilt = true;
        SetStats(s, 50, 80, 80, 50, 80, 60);
        Start(s, ActionRequest.Rest(60));
        Run(s);
        Assert.That(s.Stats.Energy, Is.EqualTo(70).Within(1e-9));
        Assert.That(s.Stats.Health, Is.EqualTo(51).Within(1e-9));
        Assert.That(s.Stats.Wakefulness, Is.EqualTo(75).Within(1e-9));
        Assert.That(s.Stats.Satiety, Is.EqualTo(78.5).Within(1e-9));
        Assert.That(s.Stats.Hydration, Is.EqualTo(77).Within(1e-9));
        Assert.That(s.Stats.Morale, Is.EqualTo(62).Within(1e-9), "营地遮蔽棚休息每小时+2精神");
    }

    [Test]
    public void S05_BedShelteredSleep_LowMorale()
    {
        var s = New();
        var f = s.Locations["camp"].Facility;
        f.ShelterBuilt = true;
        f.BedBuilt = true;
        SetStats(s, 80, 80, 80, 50, 40, 10);
        Start(s, ActionRequest.Sleep(120));
        Steps(s, 12);
        Assert.That(s.Main, Is.Not.Null, "睡眠2小时尚未结束");
        Assert.That(s.Stats.Energy, Is.EqualTo(60).Within(1e-9), "(15+5)×0.5=10");
        Assert.That(s.Stats.Wakefulness, Is.EqualTo(52).Within(1e-9));
        Assert.That(s.Stats.Morale, Is.EqualTo(11).Within(1e-9));
        Assert.That(s.Stats.Health, Is.EqualTo(80).Within(1e-9), "精神低于20不自然恢复");
    }

    [Test]
    public void S06_FactorsCappedAt2()
    {
        var s = New();
        E.DebugTeleport(s, "field");
        E.DebugUnlockEdge(s, "field__stream");
        s.GameMinute = At(1, 22, 0);
        E.DebugGive(s, "stone", 50); // 25 公斤
        SetStats(s, 100, 80, 80, 10, 90, 60);
        Assert.That(E.WorkFactor(s, ActionType.Travel), Is.EqualTo(2.0));
        Start(s, ActionRequest.Travel("stream"));
        Run(s);
        Assert.That(s.GameMinute, Is.EqualTo(At(1, 22, 40)), "20分钟移动在倍率2.0下需40分钟");
        Assert.That(s.CurrentLocation, Is.EqualTo("stream"));
        Assert.That(s.Stats.Energy, Is.EqualTo(10 - 8 * 40 / 60.0).Within(1e-9), "按实际40分钟计算消耗");
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
        Assert.That(w0 - s.Stats.Hydration, Is.EqualTo(6).Within(1e-9), "普通3加腹泻3");

        s.GameMinute = 480 + 720;
        var w1 = s.Stats.Hydration;
        Start(s, ActionRequest.Wait(60));
        Run(s);
        Assert.That(w1 - s.Stats.Hydration, Is.EqualTo(3).Within(1e-9), "12小时后消退");
    }

    [Test]
    public void S10_FireExactly5Minutes_CompletesBoil_4Minutes_Pauses()
    {
        foreach (var (fuel, expectDone) in new[] { (5, true), (4, false) })
        {
            var s = New();
            var f = s.Locations["camp"].Facility;
            f.FireLit = true;
            f.FuelMinutes = 60;
            E.DebugGive(s, "dirty_water", 2, "camp");
            Start(s, ActionRequest.Craft("boil"));
            s.Main!.WorkDone = 10;
            f.FuelMinutes = fuel;
            var r = E.Advance(s);
            if (expectDone)
            {
                Assert.That(r.Status, Is.EqualTo(SegmentStatus.Completed));
                Assert.That(Count(s, "clean_water", "camp"), Is.EqualTo(4));
                Assert.That(f.FireLit, Is.False, "本段末燃尽");
            }
            else
            {
                Assert.That(r.Status, Is.EqualTo(SegmentStatus.Paused));
                Assert.That(s.Main!.PauseReason, Is.EqualTo(PauseReason.FireOut));
                Assert.That(s.GameMinute, Is.EqualTo(480), "不推进该段");
                Assert.That(Count(s, "clean_water", "camp"), Is.EqualTo(2));
            }
        }
    }
}
