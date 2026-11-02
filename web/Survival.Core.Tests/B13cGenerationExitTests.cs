using Newtonsoft.Json.Linq;
using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 批次 B13c（R131；答复 Q126、Q127 均选 A，与现状一致）：核对换代重置范围与“领取/核对即开出口”，不改行为。
/// 验收 GEN-Q126-01、EXIT-Q127-01（resource-adjudications.json generation/exit）。
/// </summary>
public class B13cGenerationExitTests : TestBase
{
    /// <summary>策划数据 generation/exit 的取值就是现状的口径（探索回 0、里程碑清空、已发现保留、实例开通/守卫/完成重置；领取或核对即开出口、不另清场、每次重验、记录仍 10 分钟）。</summary>
    [Category("DesignSync")]
    [Test]
    public void Q126_Q127_DesignFlags()
    {
        var adj = JObject.Parse(File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "design.resource-adjudications.json")));
        var g = (JObject)adj["generation"]!;
        Assert.That((int)g["exploreResetTo"]!, Is.EqualTo(0));
        Assert.That((bool)g["exploreMilestonesReset"]! && (bool)g["knownRoomsResourcesAndRoutesPersist"]! && (bool)g["unlockedInstanceEdgesAndGuardAndCompletionReset"]!
                    && (bool)g["normalCachesRefillOnlyOnLegalGeneration"]! && (bool)g["campaignClaimsAndKnowledgePersist"]!, Is.True);
        var x = (JObject)adj["exit"]!;
        Assert.That((bool)x["recordOrCheckCommitsCurrentGenerationCompletion"]! && !(bool)x["extraFullClearRequirement"]! && (bool)x["currentGenerationRevalidateEveryTraversal"]!
                    && (bool)x["guardDoorStillRequired"]!, Is.True);
        Assert.That((int)x["recordTimeUnchanged"]!, Is.EqualTo(10));
        Assert.That(Cfg.Resources.Where(r => r.IsRecord).All(r => r.BaseMinutes == 10), Is.True, "核心记录仍 10 基础分钟");
    }

    private void Go(GameState s, string room)
    {
        E.DebugMapTeleport(s, room);
        E.DebugSetProgress(s, room, 100);
    }

    private void Do(GameState s, string id)
    {
        Start(s, ActionRequest.Gather(id));
        Run(s);
    }

    /// <summary>
    /// GEN-Q126-01（Q126 选 A，与现状一致）：换代房间探索度/里程碑回 0、普通房间仍每次 15 分钟 +50；已发现房间/资源/路线整局保留；
    /// 实例开通/守卫/核对清空，普通箱补满、清理次数回到初值；核心唯一/知识/世界边不退、不重发。
    /// </summary>
    [Test]
    public void GEN_Q126_01_GenerationReset()
    {
        var s = New();
        E.DebugMapTeleport(s, "w33_waterworks");
        E.DebugExploreHere100(s);
        Go(s, "d05_04");
        Do(s, "d05_04_cache0");
        Start(s, ActionRequest.Clear());
        Run(s);
        Go(s, "d05_07");
        E.DebugInstanceFact(s, "d05", "guardianDefeated");
        Do(s, "d05_07_unique");
        var worldEdges = Cfg.Edges.Where(e => e.DungeonId == null && E.EdgeOpen(s, e)).Select(e => e.Id).ToList();
        Assert.That(worldEdges, Is.Not.Empty);
        var knownFlags = s.Progress.Flags.Where(f => f.StartsWith("campaignId:") || f.StartsWith("claim:")).ToList();
        E.DebugMapTeleport(s, "w33_waterworks");
        Assert.That(E.NewDungeonGeneration(s, "d05"), Is.Null);
        var inst = s.DungeonInstances["d05"];
        Assert.That(inst.Facts, Is.Empty, "守卫与核对清空");
        Assert.That(inst.UnlockedEdges, Is.Empty, "实例开通清空");
        foreach (var room in E.DungeonRooms("d05"))
        {
            var ls = s.Locations[room.Id];
            Assert.That(ls.Progress, Is.EqualTo(room.InitialProgress), room.Id);
            Assert.That(ls.TriggeredMilestones, Is.Empty, room.Id);
            Assert.That(ls.ClearCharges, Is.EqualTo(room.Clear.InitialCharges), room.Id);
        }
        Assert.That(s.Locations["d05_04"].Discovered && s.Locations["d05_04"].Visited, Is.True, "已发现保留");
        Assert.That(s.Resources["d05_04_cache0"].Discovered, Is.True, "资源已发现保留");
        Assert.That(s.Resources["d05_04_cache0"].Available, Is.EqualTo(2), "普通箱补满");
        Assert.That(worldEdges.All(id => E.EdgeOpen(s, Cfg.Edges.First(e => e.Id == id))), Is.True, "世界边不退");
        Assert.That(knownFlags.All(s.Progress.Flags.Contains), Is.True, "唯一 claim 不退");
        Assert.That(E.ExploreMinutesAt("d05_04"), Is.EqualTo(15));
        Assert.That(E.ExploreIncrementAt("d05_04"), Is.EqualTo(50));
        E.DebugMapTeleport(s, "d05_04");
        Start(s, ActionRequest.Explore());
        Run(s);
        Assert.That(s.Locations["d05_04"].Progress, Is.EqualTo(50), "重新探索 +50");
    }

    /// <summary>
    /// EXIT-Q127-01（Q127 选 A，与现状一致）：本代领取或核对 10 分钟后出口立即可走、不另要求清场；只胜守卫不能冒充；
    /// 旧代或别的地牢的完成不算；出口每次按当前代重验。
    /// </summary>
    [Test]
    public void EXIT_Q127_01_ExitAfterClaimOrCheck()
    {
        var s = New();
        var exit = Cfg.Edges.First(e => e.Id == "d05_08__w33_waterworks");
        var guardDoor = Cfg.Edges.First(e => e.Id == "d05_06__d05_07");
        Go(s, "d05_06");
        Assert.That(E.EdgeOpen(s, guardDoor), Is.False, "原守卫门仍须通过");
        Assert.That(E.GateBlockedReason(s, guardDoor), Does.Contain("守卫"));
        Go(s, "d05_07");
        E.DebugInstanceFact(s, "d05", "guardianDefeated");
        Assert.That(E.EdgeOpen(s, guardDoor), Is.True, "本代守卫胜利后门开");
        Assert.That(E.EdgeOpen(s, exit), Is.False, "只胜守卫不算");
        // 别的地牢完成不算
        E.DebugInstanceFact(s, "d01", GameEngine.RecordClaimedFact);
        Assert.That(E.EdgeOpen(s, exit), Is.False, "别的地牢完成不算");
        Start(s, ActionRequest.Gather("d05_07_unique"));
        Assert.That(s.Main!.BaseWorkMinutes, Is.EqualTo(10));
        Run(s);
        Assert.That(E.EdgeOpen(s, exit), Is.True, "领取即开，不查清场");
        E.DebugMapTeleport(s, "d05_08");
        Start(s, ActionRequest.Travel("w33_waterworks"));
        Run(s);
        Assert.That(s.CurrentLocation, Is.EqualTo("w33_waterworks"), "出口可走");
        Assert.That(E.NewDungeonGeneration(s, "d05"), Is.Null);
        Assert.That(E.EdgeOpen(s, exit), Is.False, "旧代完成不算");
        Go(s, "d05_07");
        var items = s.Items.Count;
        Do(s, "d05_07_unique");   // 核对：原 10 分钟
        Assert.That(E.EdgeOpen(s, exit), Is.True, "本代核对后再开");
        Assert.That(s.Items.Count, Is.EqualTo(items), "不重发");
    }
}
