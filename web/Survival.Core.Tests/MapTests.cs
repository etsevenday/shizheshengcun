using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>地图系统案 09 验收用例 M01–M09。</summary>
public class MapTests : TestBase
{
    private void Explore(GameState s)
    {
        Start(s, ActionRequest.Explore());
        Assert.That(Run(s), Is.EqualTo(SegmentStatus.Completed));
    }

    [Test]
    public void M01_NewGameMap()
    {
        var s = New();
        Assert.That(s.Locations["camp"].Visited, Is.True);
        Assert.That(s.Locations["field"].Discovered, Is.True);
        Assert.That(s.Locations["field"].Visited, Is.False);
        foreach (var id in new[] { "stream", "woods", "village", "hardware", "clinic", "scrapyard" })
            Assert.That(s.Locations[id].Discovered, Is.False, id);
        Assert.That(s.UnlockedEdges, Is.EquivalentTo(new[] { "camp__field" }));
    }

    [Test]
    public void M02_FieldExploredFourTimes()
    {
        var s = New();
        Start(s, ActionRequest.Travel("field"));
        Run(s);
        Assert.That(s.Resources["roots"].Discovered && s.Resources["field_fiber"].Discovered, Is.True, "进入荒地即显示块根与纤维");

        Explore(s);
        Assert.That(s.Locations["field"].Progress, Is.EqualTo(25));
        Assert.That(s.UnlockedEdges, Does.Contain("field__stream"));
        Assert.That(s.Locations["stream"].Discovered, Is.True);
        // v0.3 起探索完成抽取一次本地物资：至多 1 件，且来自荒地掉落表
        var loot = s.Items.Where(i => i.Owner == "field").ToList();
        Assert.That(loot.Sum(i => i.Count), Is.LessThanOrEqualTo(1), "每次探索至多1份随机物资");
        Assert.That(loot.All(i => Cfg.LootTables["loose_field"].Entries.Any(x => x.ItemId == i.ItemId)), Is.True);

        Explore(s);
        Assert.That(s.UnlockedEdges, Does.Contain("field__woods"));
        var edges = s.UnlockedEdges.Count;

        Explore(s);
        Assert.That(s.Locations["field"].Progress, Is.EqualTo(75));
        Assert.That(s.UnlockedEdges.Count, Is.EqualTo(edges), "75无新增");

        Explore(s);
        Assert.That(s.UnlockedEdges, Does.Contain("field__village"));
        Assert.That(E.Start(s, ActionRequest.Explore()).Reason, Is.EqualTo("已充分探索，可清理或搜集已发现资源"));
    }

    [Test]
    public void M03_MilestoneOnce_CacheUntouchedAfterLeaving()
    {
        var s = New();
        E.DebugTeleport(s, "village");
        E.DebugSetProgress(s, "village", 50);
        Explore(s);
        Assert.That(s.Resources["misc_box"].Discovered, Is.True);
        Assert.That(s.Locations["village"].TriggeredMilestones, Does.Contain(75));

        E.DebugUnlockEdge(s, "field__village");
        Start(s, ActionRequest.Travel("field"));
        Run(s);
        Start(s, ActionRequest.Travel("village"));
        Run(s);
        Assert.That(s.Resources["misc_box"].Available, Is.EqualTo(1), "未搜集前内容保持不变");
        Assert.That(s.Locations["village"].Progress, Is.EqualTo(75));
        Assert.That(s.Locations["village"].TriggeredMilestones.Count(t => t == 75), Is.EqualTo(1));
    }

    [Test]
    public void M04_CannotJumpToNonAdjacent()
    {
        var s = New();
        var r = E.Start(s, ActionRequest.Travel("hardware"));
        Assert.That(r.Ok, Is.False);
        Assert.That(r.Reason, Is.EqualTo("尚未发现路线"));
        s.Locations["hardware"].Discovered = true;
        r = E.Start(s, ActionRequest.Travel("hardware"));
        Assert.That(r.Reason, Is.EqualTo("请先前往相邻地点"));
        Assert.That(s.GameMinute, Is.EqualTo(480));
        Assert.That(s.Main, Is.Null);
    }

    [Test]
    public void M05_ScrapyardEntrancesUnlockIndependently()
    {
        var s = New();
        E.DebugTeleport(s, "hardware");
        E.DebugSetProgress(s, "hardware", 75);
        Explore(s);
        Assert.That(s.UnlockedEdges, Does.Contain("hardware__scrapyard"));
        Assert.That(s.Locations["scrapyard"].Discovered, Is.True);
        Assert.That(s.UnlockedEdges, Does.Not.Contain("woods__scrapyard"), "终点已发现不解锁其他边");

        E.DebugTeleport(s, "scrapyard");
        E.DebugSetProgress(s, "scrapyard", 75);
        Explore(s);
        Assert.That(s.UnlockedEdges, Does.Contain("woods__scrapyard"));
    }

    [Test]
    public void M06_CancelTravelAfter10Minutes()
    {
        var s = New();
        E.DebugGive(s, "wood", 3, "field");
        var w0 = s.Stats.Hydration;
        Start(s, ActionRequest.Travel("field"));
        Steps(s, 2);
        var groundWood = s.Items.First(i => i.Owner == "field");
        Assert.That(E.DoInstant(s, InstantRequest.Pickup(groundWood.InstanceId, 1)).Ok, Is.False, "途中不能取物");
        Assert.That(E.Cancel(s).Ok, Is.True);
        Assert.That(s.CurrentLocation, Is.EqualTo("camp"));
        Assert.That(s.GameMinute, Is.EqualTo(490));
        Assert.That(s.Stats.Hydration, Is.LessThan(w0), "已消耗保留");
        Assert.That(s.Locations["field"].Visited, Is.False);
    }

    [Test]
    public void M07_FullyExplored_ThreeDays()
    {
        var s = New();
        E.DebugTeleport(s, "village");
        E.DebugSetProgress(s, "village", 100);
        s.Resources["supply_box"].Available = 0;
        E.DebugTeleport(s, "field");
        E.DebugSetProgress(s, "field", 100);
        s.Resources["roots"].Available = 0;
        for (var i = 0; i < 72; i++)
        {
            SetStats(s, 100, 80, 80, 90, 90, 60);
            Start(s, ActionRequest.Wait(60));
            Run(s);
        }
        Assert.That(s.Locations["field"].Progress, Is.EqualTo(100));
        Assert.That(s.Resources["roots"].Available, Is.EqualTo(6), "3天补到上限");
        Assert.That(s.Resources["supply_box"].Available, Is.EqualTo(0), "遗迹箱不重置");
    }

    [Test]
    public void M08_TravelAcrossDuskAndMidnight()
    {
        var s = New();
        E.DebugTeleport(s, "field");
        E.DebugUnlockEdge(s, "field__stream");
        s.GameMinute = At(1, 17, 55);
        Start(s, ActionRequest.Travel("stream"));
        Run(s);
        Assert.That(s.GameMinute, Is.EqualTo(At(1, 18, 25)), "首段白昼5，其后夜间每段3.33，共6段");

        s.GameMinute = At(1, 23, 50);
        Start(s, ActionRequest.Travel("field"));
        Steps(s, 3);
        Assert.That(s.CurrentLocation, Is.EqualTo("stream"), "午夜后继续，完成才更新地点");
        Assert.That(Clock.Day(s.GameMinute), Is.EqualTo(2));
        Run(s);
        Assert.That(s.GameMinute, Is.EqualTo(At(2, 0, 20)));
        Assert.That(s.CurrentLocation, Is.EqualTo("field"));
    }

    [Test]
    public void M09_SaveLoadMapState()
    {
        var s = New();
        Start(s, ActionRequest.Travel("field"));
        Run(s);
        Start(s, ActionRequest.Explore());
        Run(s);
        Start(s, ActionRequest.Explore());
        Steps(s, 3);
        var json = SaveSerializer.Serialize(s);
        var t = SaveSerializer.Deserialize(json);
        Assert.That(t.CurrentLocation, Is.EqualTo("field"));
        Assert.That(t.UnlockedEdges, Is.EquivalentTo(s.UnlockedEdges));
        Assert.That(t.Locations["field"].TriggeredMilestones, Is.EquivalentTo(new[] { 25 }));
        Assert.That(t.Main!.WorkDone, Is.EqualTo(s.Main!.WorkDone));
        Run(t);
        Run(s);
        Assert.That(t.Locations["field"].Progress, Is.EqualTo(50));
        Assert.That(SaveSerializer.Serialize(t), Is.EqualTo(SaveSerializer.Serialize(s)), "读档续行与不读档结果一致");
    }
}
