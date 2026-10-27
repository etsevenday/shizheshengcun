using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>探索随机收获案 v0.3 第 8 节 E01–E13（E14 旧档升级见 MigrationTests）。</summary>
public class ExploreLootTests : TestBase
{
    private GameState At(string loc, int progress = 0)
    {
        var s = New();
        E.DebugTeleport(s, loc);
        if (progress > 0) E.DebugSetProgress(s, loc, progress);
        return s;
    }

    private int GroundCount(GameState s, string loc) => s.Items.Where(i => i.Owner == loc).Sum(i => i.Count);

    [Test]
    public void E01_Field0_EmptyRoll_Progress25_StreamUnlocked_FullCost()
    {
        var s = At("field");
        var e0 = s.Stats.Energy;
        Start(s, ActionRequest.Explore());
        ForceLoot(s, null);
        Assert.That(Run(s), Is.EqualTo(SegmentStatus.Completed));
        Assert.That(s.Locations["field"].Progress, Is.EqualTo(25));
        Assert.That(s.UnlockedEdges, Does.Contain("field__stream"));
        Assert.That(GroundCount(s, "field"), Is.EqualTo(0));
        Assert.That(e0 - s.Stats.Energy, Is.EqualTo(0.8).Within(1e-9), "完整扣耗（探索30分钟−1.6/小时，S12 §8）");
        Assert.That(s.Log.Last(l => l.Kind == "ActionCompleted").Text, Does.Contain("本次未找到可用物资"));
    }

    [Test]
    public void E02_Woods0To25_Berry_WoodNodeFound_BerryBushHidden()
    {
        var s = At("woods");
        Start(s, ActionRequest.Explore());
        ForceLoot(s, "berry");
        Run(s);
        Assert.That(Count(s, "berry", "woods"), Is.EqualTo(1));
        Assert.That(s.Resources["wood"].Discovered, Is.True);
        Assert.That(s.Resources["berries"].Discovered, Is.False, "捡到野果不自动发现野果丛");
        var text = s.Log.Last(l => l.Kind == "ActionCompleted").Text;
        Assert.That(text, Does.Contain("探索度0%→25%").And.Contain("发现可采木材").And.Contain("野果+1，已放在树林边缘地面"));
    }

    [Test]
    public void E03_NoMilestoneStage_ItemWithoutFakeDiscovery()
    {
        var s = At("field", 50);
        var res = s.Resources.Count(r => r.Value.Discovered);
        var edges = s.UnlockedEdges.Count;
        Start(s, ActionRequest.Explore());
        ForceLoot(s, "stone");
        Run(s);
        Assert.That(s.Locations["field"].Progress, Is.EqualTo(75));
        Assert.That(Count(s, "stone", "field"), Is.EqualTo(1));
        Assert.That(s.Resources.Count(r => r.Value.Discovered), Is.EqualTo(res));
        Assert.That(s.UnlockedEdges.Count, Is.EqualTo(edges));
        Assert.That(s.Log.Last(l => l.Kind == "ActionCompleted").Text, Does.Contain("未发现新线索"));
    }

    [Test]
    public void E04_Woods75To100_ScrapyardUnlocked_WoodOnGround_ExploreDisabled()
    {
        var s = At("woods", 75);
        Start(s, ActionRequest.Explore());
        ForceLoot(s, "wood");
        Run(s);
        Assert.That(s.UnlockedEdges, Does.Contain("woods__scrapyard"));
        Assert.That(Count(s, "wood", "woods"), Is.EqualTo(1));
        Assert.That(E.CanStart(s, ActionRequest.Explore()), Is.EqualTo("已充分探索，可清理或搜集已发现资源"));
        Assert.That(s.Main, Is.Null, "不自动再触发一轮");
    }

    [Test]
    public void E05_FullyExplored_RejectedWithoutTimeOrDraw()
    {
        var s = At("woods", 100);
        var minute = s.GameMinute;
        var rng = s.Rng.State;
        var items = s.Items.Sum(i => i.Count);
        for (var k = 0; k < 3; k++)
        {
            Assert.That(E.Start(s, ActionRequest.Explore()).Ok, Is.False);
            Assert.That(E.Preview(s, ActionRequest.Explore()).Ok, Is.False);
        }
        Assert.That(s.GameMinute, Is.EqualTo(minute), "不耗时");
        Assert.That(s.Rng.State, Is.EqualTo(rng), "不抽签");
        Assert.That(s.Items.Sum(i => i.Count), Is.EqualTo(items), "不生成物品");
    }

    [Test]
    public void E06_ClearChargesZero_ExploreStillWorks_NoChargeSpent()
    {
        var s = At("woods");
        s.Locations["woods"].ClearCharges = 0;
        Assert.That(E.CanStart(s, ActionRequest.Clear()), Is.Not.Null);
        Start(s, ActionRequest.Explore());
        Run(s);
        Assert.That(s.Locations["woods"].Progress, Is.EqualTo(25));
        Assert.That(s.Locations["woods"].ClearCharges, Is.EqualTo(0), "不扣、不要求清理次数");
        Assert.That(s.Locations["woods"].ClearRefillProgress, Is.EqualTo(30), "探索耗时也促进恢复");
    }

    [Test]
    public void E07_CancelOrDeathBeforeCommit_NothingCommitted()
    {
        var s = At("woods");
        Start(s, ActionRequest.Explore());
        ForceLoot(s, "wood");
        Steps(s, 3);
        E.Cancel(s);
        Assert.That(s.Locations["woods"].Progress, Is.EqualTo(0));
        Assert.That(s.Resources["wood"].Discovered, Is.False);
        Assert.That(GroundCount(s, "woods"), Is.EqualTo(0));
        Assert.That(s.GameMinute, Is.EqualTo(480 + 15), "时间消耗保留");

        var d = At("woods");
        Start(d, ActionRequest.Explore());
        ForceLoot(d, "wood");
        d.Stats.Health = 0.1;
        d.Stats.Hydration = 0;
        E.SyncLatches(d);
        Run(d);
        Assert.That(d.Progress.Dead, Is.True);
        Assert.That(d.Locations["woods"].Progress, Is.EqualTo(0));
        Assert.That(GroundCount(d, "woods"), Is.EqualTo(0));
    }

    [Test]
    public void E08_PauseSupplyReload_PlanAndWorkKept()
    {
        var s = At("woods");
        E.DebugGive(s, "clean_water", 1);
        SetStats(s, 100, 80, 20.2, 90, 90, 60);
        Start(s, ActionRequest.Explore());
        var roll = s.Main!.LootRoll;
        var item = s.Main.LootItemId;
        Assert.That(Run(s, autoResume: false), Is.EqualTo(SegmentStatus.Paused));
        Start(s, ActionRequest.UseItem("clean_water"));
        Run(s);
        Assert.That(s.Main!.LootRoll, Is.EqualTo(roll), "补给子行动不覆盖原计划");
        var t = SaveSerializer.Deserialize(SaveSerializer.Serialize(s));
        Assert.That(t.Main!.LootItemId, Is.EqualTo(item));
        Assert.That(t.Main.StartProgress, Is.EqualTo(0));
        Assert.That(t.Main.WorkDone, Is.EqualTo(s.Main.WorkDone));
        E.Resume(t);
        Run(t);
        Assert.That(t.Locations["woods"].Progress, Is.EqualTo(25));
        Assert.That(GroundCount(t, "woods"), Is.EqualTo(item == null ? 0 : 1));
    }

    [Test]
    public void E09_CompletionCommittedOnce()
    {
        var s = At("woods");
        Start(s, ActionRequest.Explore());
        ForceLoot(s, "stone");
        Run(s);
        for (var k = 0; k < 3; k++) Assert.That(E.Advance(s).Status, Is.EqualTo(SegmentStatus.Idle));
        Assert.That(s.Locations["woods"].Progress, Is.EqualTo(25));
        Assert.That(Count(s, "stone", "woods"), Is.EqualTo(1));
        Assert.That(s.Log.Count(l => l.Kind == "ActionCompleted"), Is.EqualTo(1));
    }

    [Test]
    public void E10_FullyExploredStaysFull_ClearRefillsIndependently()
    {
        var s = At("field", 100);
        s.Locations["field"].ClearCharges = 2;
        Start(s, ActionRequest.Travel("camp"));
        Run(s);
        AdvanceTo(s, At(2, 9, 0));
        Start(s, ActionRequest.Travel("field"));
        Run(s);
        Assert.That(s.Locations["field"].Progress, Is.EqualTo(100));
        Assert.That(E.CanStart(s, ActionRequest.Explore()), Is.EqualTo("已充分探索，可清理或搜集已发现资源"));
        Assert.That(s.Locations["field"].ClearCharges, Is.EqualTo(4), "清理独立恢复");
    }

    [Test]
    public void E11_HeavyBackpack_HerbOnGround_ShelfLifeFromCompletion()
    {
        var s = At("stream");
        E.DebugGive(s, "stone", 57);
        E.DebugGive(s, "fiber", 3);
        var carried = E.CarriedGrams(s);
        Start(s, ActionRequest.Explore());
        ForceLoot(s, "herb");
        Run(s);
        var herb = First(s, "herb", "stream");
        Assert.That(E.CarriedGrams(s), Is.EqualTo(carried));
        Assert.That(herb.ExpiresAt, Is.EqualTo(s.GameMinute + 4320), "保质期从完成时刻算");
    }

    [Test]
    public void E13_LastSegmentAlertAndComplete_OneResultNoPausedAction()
    {
        var s = At("woods");
        // 30分钟探索每段失水0.3（S12 §8），第6段（最后一段）恰好越过20
        SetStats(s, 100, 80, 21.7, 90, 90, 60);
        Start(s, ActionRequest.Explore());
        ForceLoot(s, "fiber");
        var st = Run(s, autoResume: false);
        Assert.That(st, Is.EqualTo(SegmentStatus.Completed));
        Assert.That(s.Main, Is.Null, "不留可重领奖的原行动");
        Assert.That(s.Locations["woods"].Progress, Is.EqualTo(25));
        Assert.That(Count(s, "fiber", "woods"), Is.EqualTo(1));
        Assert.That(s.Log.Any(l => l.Kind == "Alert"), Is.True, "危险提示照常");
    }

    [Test]
    public void Explore_Preview_NewTextAndOdds()
    {
        var s = At("woods");
        var p = E.Preview(s, ActionRequest.Explore());
        Assert.That(p.Notes, Does.Contain("探索推进地块调查，并有机会取得零散物资"));
        Assert.That(p.Notes.Any(n => n.Contains("不获得") || n.Contains("不直接产出")), Is.False, "旧文案已替换");
        Assert.That(p.LootOdds, Has.Count.EqualTo(5));
        Assert.That(p.LootOdds[0].Name, Is.EqualTo("未获得物资"));
    }
}
