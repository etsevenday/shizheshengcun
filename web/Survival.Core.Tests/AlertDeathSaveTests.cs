using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>S09 阈值暂停与补给子行动、S11 致死搜集伤害、S12 暂停中存档恢复，以及实施方案 9A 的拍板规则。</summary>
public class AlertDeathSaveTests : TestBase
{
    [Test]
    public void S09_LowWaterPause_DrinkAsSubAction_ThenContinue()
    {
        var s = New();
        E.DebugTeleport(s, "field");
        E.DebugGive(s, "clean_water", 1);
        SetStats(s, 100, 80, 21, 90, 90, 60);
        Start(s, ActionRequest.Explore());
        Assert.That(Run(s, autoResume: false), Is.EqualTo(SegmentStatus.Paused));
        var main = s.Main!;
        Assert.That(main.PauseReason, Is.EqualTo(PauseReason.StatAlert));
        Assert.That(main.PauseStat, Is.EqualTo(StatId.Hydration));
        Assert.That(s.GameMinute, Is.EqualTo(480 + 15), "21→20.625→20.25→19.875 第三段后暂停");
        var progress = main.WorkDone;

        Assert.That(E.Start(s, ActionRequest.Gather("roots")).Ok, Is.False, "暂停中不开放普通行动");
        Start(s, ActionRequest.UseItem("clean_water"));
        Assert.That(s.Sub, Is.Not.Null);
        Run(s);
        Assert.That(s.Sub, Is.Null);
        Assert.That(s.Main!.Status, Is.EqualTo(ActionStatus.Paused), "补给后保持暂停");
        Assert.That(s.Main.WorkDone, Is.EqualTo(progress));
        Assert.That(s.GameMinute, Is.EqualTo(480 + 20), "子行动耗时5分钟");

        E.Resume(s);
        Assert.That(Run(s, autoResume: false), Is.EqualTo(SegmentStatus.Completed));
        Assert.That(s.Locations["field"].Progress, Is.EqualTo(25));
    }

    [Test]
    public void Alert_NotRepeatedInSameBand_ZeroPausesAgain()
    {
        var s = New();
        SetStats(s, 100, 80, 20.5, 90, 90, 60);
        E.DebugTeleport(s, "field");
        Start(s, ActionRequest.Explore());
        Assert.That(Run(s, autoResume: false), Is.EqualTo(SegmentStatus.Paused));
        E.Resume(s);
        Assert.That(Run(s, autoResume: false), Is.EqualTo(SegmentStatus.Completed), "同一区间不再提醒");

        SetStats(s, 100, 80, 0.3, 90, 90, 60);
        s.AlertLatched[StatId.Hydration] = true;
        Start(s, ActionRequest.Explore());
        Assert.That(Run(s, autoResume: false), Is.EqualTo(SegmentStatus.Paused));
        Assert.That(s.Main!.PauseReason, Is.EqualTo(PauseReason.StatZero), "归零再暂停一次");
    }

    [Test]
    public void SubAction_IsNeverPaused()
    {
        var s = New();
        E.DebugTeleport(s, "field");
        SetStats(s, 100, 80, 21, 90, 90, 60);
        Start(s, ActionRequest.Explore());
        Run(s, autoResume: false);
        SetStats(s, 100, 20.5, 80, 90, 90, 60);
        Start(s, ActionRequest.Rest(60));
        var st = Run(s, autoResume: false);
        Assert.That(st, Is.EqualTo(SegmentStatus.Completed), "子行动期间越阈值只记录");
        Assert.That(s.AlertLatched[StatId.Satiety], Is.True);
        Assert.That(s.Main!.Status, Is.EqualTo(ActionStatus.Paused));
    }

    [Test]
    public void TravelPause_SubCannotTouchGround_RestIsOutdoor()
    {
        var s = New();
        s.Locations["camp"].Facility.ShelterBuilt = true;
        SetStats(s, 50, 80, 20.3, 90, 90, 60);
        Start(s, ActionRequest.Travel("field"));
        Run(s, autoResume: false);
        Assert.That(s.Main!.Status, Is.EqualTo(ActionStatus.Paused));
        Assert.That(E.Start(s, ActionRequest.UseItem("clean_water")).Reason, Does.Contain("没有可用"), "地面净水不可用");
        Assert.That(E.Start(s, ActionRequest.Ignite(First(s, "lighter").InstanceId)).Ok, Is.False, "移动途中无法点火");
        var h = s.Stats.Health;
        Start(s, ActionRequest.Rest(60));
        Run(s);
        Assert.That(s.Stats.Health, Is.EqualTo(h), "路上休息按露天，无自然恢复");
    }

    [Test]
    public void S11_LethalHazardOnCompletion_ProductsStayOnGround()
    {
        var s = New();
        E.DebugTeleport(s, "woods");
        E.DebugSetProgress(s, "woods", 25);
        SetStats(s, 0.5, 80, 80, 18, 90, 60);
        Start(s, ActionRequest.Gather("wood"));
        s.Main!.HazardHit = true;
        Assert.That(Run(s), Is.EqualTo(SegmentStatus.Dead));
        Assert.That(s.Progress.Dead, Is.True);
        Assert.That(Count(s, "wood", "woods"), Is.EqualTo(1), "产物留地面");
        Assert.That(s.Progress.Death!.Cause, Does.Contain("划伤"));
        Assert.That(E.Start(s, ActionRequest.Wait(30)).Ok, Is.False, "死亡后锁定");
        Assert.That(E.Advance(s).Status, Is.EqualTo(SegmentStatus.Dead));
    }

    [Test]
    public void Death_ByDehydration_ReturnsReservations()
    {
        var s = New();
        SetStats(s, 0.5, 80, 0, 18, 90, 60);
        Start(s, ActionRequest.Craft("knife"));
        Run(s);
        Assert.That(s.Progress.Dead, Is.True);
        Assert.That(s.Progress.Death!.Cause, Is.EqualTo("缺水"));
        Assert.That(s.Items.All(i => i.ReservedBy == null));
        Assert.That(Count(s, "stone", "camp"), Is.EqualTo(2));
    }

    [Test]
    public void S12_SaveWhilePausedWithSubAction_RestoresExactly()
    {
        var s = New();
        E.DebugTeleport(s, "woods");
        E.DebugSetProgress(s, "woods", 25);
        E.DebugGive(s, "hatchet", 1);
        E.DebugGive(s, "clean_water", 2);
        SetStats(s, 100, 80, 21, 90, 90, 60);
        Start(s, ActionRequest.Gather("wood"));
        Run(s, autoResume: false);
        Start(s, ActionRequest.Rest(30));
        Steps(s, 2);

        var json = SaveSerializer.Serialize(s);
        var t = SaveSerializer.Deserialize(json);
        Assert.That(SaveSerializer.Serialize(t), Is.EqualTo(json));
        Assert.That(t.SegmentCount, Is.EqualTo(s.SegmentCount));
        Assert.That(t.Main!.HazardHit, Is.EqualTo(s.Main!.HazardHit));
        Assert.That(t.Sub!.ElapsedMinutes, Is.EqualTo(10));
        Assert.That(t.Resources["wood"].Reserved, Is.EqualTo(2));

        foreach (var g in new[] { s, t })
        {
            Run(g);
            E.Resume(g);
            Run(g);
        }
        Assert.That(SaveSerializer.Serialize(t), Is.EqualTo(SaveSerializer.Serialize(s)));
    }

    [Test]
    public void Save_RejectsIncompatibleVersion()
    {
        var s = New();
        var json = SaveSerializer.Serialize(s).Replace(GameState.CurrentSchema, "0.1");
        Assert.Throws<SaveVersionException>(() => SaveSerializer.Deserialize(json));
    }

    [Test]
    public void Bandage_CooldownBlocksWithoutConsuming()
    {
        var s = New();
        E.DebugGive(s, "bandage", 2);
        SetStats(s, 10, 80, 80, 18, 90, 60);
        var heal = E.BandageHeal(s);
        Start(s, ActionRequest.UseItem("bandage"));
        Run(s);
        Assert.That(s.Stats.Health, Is.EqualTo(10 + heal).Within(1e-9), "v0.6：1×(1+0.02×医疗等级)");
        var r = E.Start(s, ActionRequest.UseItem("bandage"));
        Assert.That(r.Ok, Is.False);
        Assert.That(r.Reason, Does.Contain("冷却"));
        Assert.That(Count(s, "bandage"), Is.EqualTo(1));
    }

    [Test]
    public void RawRootAndSpoiledFood_CannotBeEaten()
    {
        var s = New();
        E.DebugGive(s, "raw_root", 1);
        Assert.That(E.Start(s, ActionRequest.UseItem("raw_root")).Reason, Does.Contain("烹饪"));
        E.DebugGive(s, "berry", 1);
        First(s, "berry").ExpiresAt = s.GameMinute;
        Assert.That(E.Start(s, ActionRequest.UseItem("berry")).Reason, Does.Contain("腐败"));
        Assert.That(s.GameMinute, Is.EqualTo(480));
    }

    [Test]
    public void SevenDayCheck_FiresOnceAtDay8_0800()
    {
        var s = New();
        s.GameMinute = At(8, 7, 0);
        SetStats(s, 100, 80, 80, 90, 90, 60);
        Start(s, ActionRequest.Wait(60));
        Run(s);
        Assert.That(s.Progress.SevenDayShown, Is.True);
        var evs = E.DrainEvents(s);
        Assert.That(evs.Count(e => e.Kind == GameEventKind.SevenDay), Is.EqualTo(1));
    }

    [Test]
    public void Preview_DoesNotMutateState()
    {
        var s = New();
        var before = SaveSerializer.Serialize(s);
        var p = E.Preview(s, ActionRequest.Travel("field"));
        Assert.That(p.Ok, Is.True);
        Assert.That(p.EstimatedMinutes, Is.EqualTo(15));
        Assert.That(p.StatsAfter.Hydration, Is.LessThan(s.Stats.Hydration));
        Assert.That(SaveSerializer.Serialize(s), Is.EqualTo(before));
    }

    [Test]
    public void CanStart_ValidatesWithoutMutating()
    {
        var s = New();
        var before = SaveSerializer.Serialize(s);
        Assert.That(E.CanStart(s, ActionRequest.Explore()), Is.EqualTo("已充分探索，可清理或搜集已发现资源"));
        Assert.That(E.CanStart(s, ActionRequest.Travel("field")), Is.Null);
        Assert.That(SaveSerializer.Serialize(s), Is.EqualTo(before));
        Start(s, ActionRequest.Travel("field"));
        Assert.That(E.CanStart(s, ActionRequest.Explore()), Is.EqualTo("已有进行中的行动"));
    }

    [Test]
    public void Tips_Loaded()
    {
        Assert.That(Cfg.Progress.Tips, Has.Count.GreaterThanOrEqualTo(10));
        Assert.That(Cfg.Progress.Tips, Has.All.Not.Empty);
    }
}
