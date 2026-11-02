using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 批次 B11（R35）：新区探索里程碑（S03「新区探索与发现」「首次到访与序章路标」、node-expansion-rules.json，EXP30-01—04）。
/// 普通新增野外每次 30 基础分钟 +25；25% 揭资源 A、50% 揭资源 B（B11 只记标记与日志，采集在 B12）、75% 安全线索不另发物、100% 揭出边且门槛要满足；
/// 地牢房间 15 基础分钟 +50；公共城市/服务首次到访自动 100；visited 只在移动成功时登记。
/// </summary>
public class B11ExploreTests : TestBase
{
    /// <summary>B12 起 96 条增量资源并入资源表：某地点已发现的资源点 ID。</summary>
    private static List<string> DiscAt(GameState s, string loc) =>
        Cfg.Resources.Where(r => r.LocationId == loc && s.Resources[r.NodeId].Discovered).Select(r => r.NodeId).ToList();

    private string Explore(GameState s)
    {
        Start(s, ActionRequest.Explore());
        Assert.That(Run(s), Is.EqualTo(SegmentStatus.Completed));
        return s.Log.Last(l => l.Kind == GameEventKind.ActionCompleted.ToString()).Text;
    }

    /// <summary>EXP30-01：25/50 只揭相应资源标记、75 给线索、100 揭出边；每次 30 基础分钟 +25，四次到 100。</summary>
    [Test]
    public void EXP30_01_NewWildMilestones()
    {
        var s = New();
        E.DebugTeleport(s, "w11_orchard");
        Assert.That(E.ExploreMinutesAt("w11_orchard"), Is.EqualTo(30));
        Start(s, ActionRequest.Explore());
        Assert.That(s.Main!.BaseWorkMinutes, Is.EqualTo(30));
        Run(s);
        Assert.That(s.Locations["w11_orchard"].Progress, Is.EqualTo(25));
        Assert.That(DiscAt(s, "w11_orchard"), Is.EquivalentTo(new[] { "w11_orchard_a" }), "25% 只揭资源 A");
        Assert.That(s.Log.Last(l => l.Kind == GameEventKind.ActionCompleted.ToString()).Text, Does.Contain("发现果园块根地"));

        var t50 = Explore(s);
        Assert.That(DiscAt(s, "w11_orchard"), Is.EquivalentTo(new[] { "w11_orchard_a", "w11_orchard_b" }));
        Assert.That(t50, Does.Contain("果园旧布堆"), "Q125 正式名");
        Assert.That(E.NextMilestoneHasContent(s, "w11_orchard"), Is.True, "75 有线索");

        var t75 = Explore(s);
        Assert.That(t75, Does.Contain(Cfg.Map.ClueText));
        Assert.That(DiscAt(s, "w11_orchard"), Has.Count.EqualTo(2), "75 不再揭资源");

        Assert.That(s.Locations["w12_canal"].Discovered, Is.False);
        var t100 = Explore(s);
        Assert.That(s.Locations["w11_orchard"].Progress, Is.EqualTo(100));
        Assert.That(s.UnlockedEdges, Does.Contain("w11_orchard__w12_canal"), "from_explore100：本地为原 from，100 即开");
        Assert.That(s.Locations["w12_canal"].Discovered, Is.True);
        Assert.That(t100, Does.Contain("灌溉渠岸"));
        Assert.That(E.Start(s, ActionRequest.Explore()).Reason, Is.EqualTo("已充分探索，可清理或搜集已发现资源"));
    }

    /// <summary>EXP30-01：100% 揭出边但任务门槛未满足仍不通，并写明原因（m02→h01 固定路障）。</summary>
    [Test]
    public void EXP30_01_Explore100_RevealsButTaskGateStillLocks()
    {
        var s = New();
        E.DebugTeleport(s, "m02_checkpoint");
        for (var i = 0; i < 4; i++) Explore(s);
        Assert.That(s.Locations["m02_checkpoint"].Progress, Is.EqualTo(100));
        Assert.That(s.Locations["h01_gate"].Discovered, Is.True);
        Assert.That(s.UnlockedEdges, Does.Not.Contain("m02_checkpoint__h01_gate"));
        Assert.That(s.Log.Last(l => l.Kind == GameEventKind.ActionCompleted.ToString()).Text, Does.Contain("营门的路线（未开通"));
        var r = E.Start(s, ActionRequest.Travel("h01_gate"));
        Assert.That(r.Ok, Is.False);
        Assert.That(r.Reason, Does.Contain("检查哨"));
    }

    /// <summary>地牢房间：15 基础分钟 +50、两次到 100；01 缓冲房探索不抽物资；50% 揭本房标记；房间 100 开实例边。</summary>
    [Test]
    public void DungeonRoom_15Min_Plus50_InstanceEdge()
    {
        var s = New();
        E.DebugTeleport(s, "d05_01");
        var ground = s.Items.Count(i => i.Owner == "d05_01");
        Start(s, ActionRequest.Explore());
        Assert.That(s.Main!.BaseWorkMinutes, Is.EqualTo(15));
        Run(s);
        Assert.That(s.Locations["d05_01"].Progress, Is.EqualTo(50));
        Explore(s);
        Assert.That(s.Locations["d05_01"].Progress, Is.EqualTo(100));
        Assert.That(s.Items.Count(i => i.Owner == "d05_01"), Is.EqualTo(ground), "01 缓冲房探索不生成物资");
        Assert.That(s.DungeonInstances["d05"].UnlockedEdges, Does.Contain("d05_01__d05_02"));

        E.DebugTeleport(s, "d05_04");
        Explore(s);
        Assert.That(DiscAt(s, "d05_04"), Is.SupersetOf(new[] { "d05_04_cache0", "d05_04_cache2" }));
        Assert.That(s.Log.Last(l => l.Kind == GameEventKind.ActionCompleted.ToString()).Text, Does.Not.Contain(Cfg.Map.ClueText), "地牢没有 75 线索");
    }

    /// <summary>公共城市/服务首次实际到访自动 100：探索禁用、不发里程碑物资/标记、不随机接敌。</summary>
    [Test]
    public void PublicNode_Auto100OnVisit()
    {
        var s = New();
        var items = s.Items.Count;
        E.DebugTeleport(s, "h01_gate");
        Assert.That(s.Locations["h01_gate"].Progress, Is.EqualTo(100));
        Assert.That(s.Items.Count, Is.EqualTo(items));
        Assert.That(E.Start(s, ActionRequest.Explore()).Ok, Is.False);
        Assert.That(E.EncounterChance(s, "h01_gate", ActionType.Explore), Is.EqualTo(0));
        Assert.That(s.Locations["h02_square"].Discovered, Is.True, "100% 揭示 h01→h02（未开通：登记）");
        Assert.That(E.AdjacentLocked(s).Single(x => x.Target == "h02_square").Reason, Does.Contain("登记"));
    }

    /// <summary>EXP30-02/MP08：取消移动不记 visited，不提交接触事实与门槛；移动成功才记。</summary>
    [Test]
    public void EXP30_02_CancelTravel_NoVisited()
    {
        var s = New();
        E.DebugTeleport(s, "village");
        E.DebugMapFact(s, "camp_route_known");
        Assert.That(s.UnlockedEdges, Does.Contain("village__m01_road"));
        Start(s, ActionRequest.Travel("m01_road"));
        Steps(s, 3);
        Assert.That(E.Cancel(s).Ok, Is.True);
        Assert.That(s.CurrentLocation, Is.EqualTo("village"));
        Assert.That(s.Locations["m01_road"].Visited, Is.False);

        var c = New();
        E.DebugTeleport(c, "c01_pilgrim");
        E.DebugSetProgress(c, "c01_pilgrim", 100);
        Assert.That(c.UnlockedEdges, Does.Contain("c01_pilgrim__c02_church"));
        Start(c, ActionRequest.Travel("c02_church"));
        Steps(c, 1);
        E.Cancel(c);
        Assert.That(c.Progress.Flags, Does.Not.Contain("church_contacted"), "取消不算到达");
        Start(c, ActionRequest.Travel("c02_church"));
        Run(c);
        Assert.That(c.Progress.Flags, Does.Contain("church_contacted"));
    }

    /// <summary>EXP30-03：出区开通不授村庄 100% 奖励；五金 50、诊所 100 仍独立。</summary>
    [Test]
    public void EXP30_03_ExitDoesNotGrantVillage100()
    {
        var s = New();
        E.DebugTeleport(s, "village");
        E.DebugSetProgress(s, "village", 25);
        E.DebugMapFact(s, "camp_route_known");
        Assert.That(s.UnlockedEdges, Does.Contain("village__m01_road"));
        Assert.That(s.Locations["village"].Progress, Is.EqualTo(25));
        Assert.That(s.Resources["village_cold_box"].Discovered, Is.False, "不发村庄 100 的箱子");
        Assert.That(s.UnlockedEdges, Does.Not.Contain("village__hardware"));
        Assert.That(s.UnlockedEdges, Does.Not.Contain("village__clinic"));
    }

    /// <summary>EXP30-04：读档、重复到访不重发里程碑/标记；读档续行与不读档结果一致。</summary>
    [Test]
    public void EXP30_04_ReloadAndRevisit_NoRepeat()
    {
        var s = New();
        E.DebugTeleport(s, "w11_orchard");
        Explore(s);
        Start(s, ActionRequest.Explore());
        Steps(s, 2);
        var t = SaveSerializer.Deserialize(SaveSerializer.Serialize(s), Cfg);
        Run(s);
        Run(t);
        Assert.That(SaveSerializer.Serialize(t), Is.EqualTo(SaveSerializer.Serialize(s)), "读档续行一致");
        var markers = DiscAt(s, "w11_orchard").Count;
        Assert.That(markers, Is.EqualTo(2));
        E.DebugTeleport(s, "w12_canal");
        var log = s.Log.Count;
        E.DebugTeleport(s, "w11_orchard");
        Assert.That(DiscAt(s, "w11_orchard"), Has.Count.EqualTo(markers));
        Assert.That(s.Log.Skip(log).Any(l => l.Text.Contains("生块根点")), Is.False, "重复到访不重发");
        Assert.That(s.Locations["w11_orchard"].TriggeredMilestones, Is.EquivalentTo(new[] { 25, 50 }));
    }

    /// <summary>试玩路线用的开发入口：传送按实际到达处理（忙碌拒绝）；当前地点探索到 100 结算里程碑与揭边并列出未开通原因。</summary>
    [Test]
    public void DevTools_TeleportAndExplore100()
    {
        var s = New();
        Start(s, ActionRequest.Travel("field"));
        Assert.That(E.DebugMapTeleport(s, "c02_church"), Does.Contain("行动进行中"));
        Run(s);
        Assert.That(E.DebugMapTeleport(s, "c02_church"), Does.Contain("建立公开接触"));
        Assert.That(s.Progress.Flags, Does.Contain("church_contacted"));
        E.DebugMapTeleport(s, "w11_orchard");
        E.DebugExploreHere100(s);
        Assert.That(DiscAt(s, "w11_orchard"), Has.Count.EqualTo(2));
        Assert.That(s.UnlockedEdges, Does.Contain("w11_orchard__w12_canal"));
        E.DebugMapTeleport(s, "m02_checkpoint");
        Assert.That(E.DebugExploreHere100(s), Does.Contain("营门（需要：解决检查哨"));
        E.DebugMapTeleport(s, "h02_square");
        Assert.That(E.DebugExploreHere100(s), Does.Contain("不需要探索"));
    }
}
