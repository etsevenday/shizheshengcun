using Newtonsoft.Json.Linq;
using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 批次 B24（R73 三城鸟瞰与设施栏 NPC 卡）。规则来源：A10「A10-CITY 三城鸟瞰与设施NPC」§2—§7、§9；
/// S14「中枢联动归档」「归属、替代与状态」「建筑、NPC与物理移动」；S07「中枢联动归档：城市与远征交互」UI-X01/UI-X02；A11「城市建筑、岗位NPC与八支线」§1/§6。
/// 验收：CITY-UI01/04/05/06/07/08/09/10/13、UI-X02（内核部分）。
/// </summary>
public class B24CityOverviewTests : TestBase
{
    private GameState Registered(string loc)
    {
        var s = New();
        s.Progress.Flags.UnionWith(new[] { "registration_authorized", "hub_registered" });
        s.GameMinute = At(2, 9, 0);
        s.SpawnNextCheck = int.MaxValue;
        s.PatrolNextCheck = int.MaxValue;
        E.DebugMapTeleport(s, loc);
        ReadDialogues(s);
        return s;
    }

    private GameState At(string loc, params string[] facts)
    {
        var s = New();
        s.Progress.Flags.UnionWith(facts);
        s.GameMinute = At(2, 9, 0);
        s.SpawnNextCheck = int.MaxValue;
        s.PatrolNextCheck = int.MaxValue;
        E.DebugMapTeleport(s, loc);
        ReadDialogues(s);
        return s;
    }

    private static CityBuildingView B(CityOverviewView v, string id) => v.Buildings.Single(b => b.NodeId == id);

    private static string Route(CityBuildingView b) => b.Path == null ? "null" : string.Join("→", new[] { b.Path.FirstOrDefault()?.From }.Concat(b.Path.Select(p => p.To)));

    // ───────────── 范围与城内节点（CITY-UI09） ─────────────

    /// <summary>CITY-UI09：三城父节点与城内建筑按地图数据；h05_homestead、c03_crypt、r03_lab 是独立世界节点，不进鸟瞰、不能在那里打开。</summary>
    [Test]
    public void CITY_UI09_IndependentNodesNotInOverview()
    {
        Assert.That(E.CityParents(), Is.EqualTo(new[] { "h02_square", "c02_church", "r02_academy" }));
        Assert.That(E.CityMembers("h02_square"), Is.EquivalentTo(new[] { "h02_square", "h01_gate", "h03_workshop", "h04_infirmary", "h05_appraisal", "h06_shelter" }));
        Assert.That(E.CityMembers("c02_church"), Is.EquivalentTo(new[] { "c02_church", "c04_alms", "c05_admonition", "c06_archive", "c07_sanctuary" }));
        Assert.That(E.CityMembers("r02_academy"), Is.EquivalentTo(new[] { "r02_academy", "r04_reception", "r05_testing", "r06_archive", "r07_workshop", "r08_observation" }));
        foreach (var x in new[] { "h05_homestead", "c03_crypt", "r03_lab", "camp" })
            Assert.That(E.CityOf(x), Is.Null, x);
        Assert.That(E.CityTitle("h02_square"), Is.EqualTo("幸存者营区"));
        Assert.That(E.CityTitle("c02_church"), Is.EqualTo("新生教会"));
        Assert.That(E.CityTitle("r02_academy"), Is.EqualTo("灰塔科学院"));

        var s = Registered("h05_homestead");
        Assert.That(E.CityOverviewOf(s), Is.Null);
        Assert.That(E.CityOverviewBlocked(s), Is.EqualTo("只在三城城内可以打开主城鸟瞰"));
        var hub = E.CityOverviewOf(Registered("h02_square"))!;
        Assert.That(hub.Buildings.Select(b => b.NodeId), Does.Not.Contain("h05_homestead"));
    }

    // ───────────── 路线（CITY-UI05） ─────────────

    /// <summary>CITY-UI05：工坊→鉴定所 h03→h02→h05_appraisal 两段各 5、共 10；外院→检测室 r02→r04→r05；接待庭→典藏室前置满足时 c02→c05→c06。</summary>
    [Test]
    public void CITY_UI05_RealTwoEdgeRoutes()
    {
        var hub = E.CityOverviewOf(Registered("h03_workshop"))!;
        var ap = B(hub, "h05_appraisal");
        Assert.That(Route(ap), Is.EqualTo("h03_workshop→h02_square→h05_appraisal"));
        Assert.That(ap.Path!.Select(p => p.Minutes), Is.EqualTo(new[] { 5, 5 }));
        Assert.That(ap.PathMinutes, Is.EqualTo(10));
        Assert.That(ap.Blocked, Is.Null);

        var ac = E.CityOverviewOf(At("r02_academy"))!;
        Assert.That(Route(B(ac, "r05_testing")), Is.EqualTo("r02_academy→r04_reception→r05_testing"));
        Assert.That(B(ac, "r05_testing").PathMinutes, Is.EqualTo(10));

        var ch = E.CityOverviewOf(At("c02_church", "FA02_route_briefed"))!;
        Assert.That(Route(B(ch, "c06_archive")), Is.EqualTo("c02_church→c05_admonition→c06_archive"), "同长 10 分钟按配置顺序取训诫堂");
        Assert.That(B(ch, "c06_archive").PathMinutes, Is.EqualTo(10));
        // 前置未满足：不规划穿锁路线，改走已开通的圣堂一侧（同样两条真实边）
        var ch2 = E.CityOverviewOf(At("c02_church"))!;
        Assert.That(Route(B(ch2, "c06_archive")), Is.EqualTo("c02_church→c07_sanctuary→c06_archive"));
    }

    // ───────────── 锁与原因（CITY-UI08） ─────────────

    /// <summary>CITY-UI08：营门登记前广场写门槛原因、其余楼栋未发现只给轮廓；科学院试制工坊前置缺失写原因，不自解锁门；拒绝检测不锁主线（r05 公开可达）。</summary>
    [Test]
    public void CITY_UI08_LockReasons()
    {
        var gate = At("h01_gate");
        var v = E.CityOverviewOf(gate)!;
        Assert.That(v.CityId, Is.EqualTo("h02_square"));
        Assert.That(B(v, "h01_gate").IsHere, Is.True);
        var sq = B(v, "h02_square");
        Assert.That(sq.Path, Is.Null);
        Assert.That(sq.Blocked, Does.StartWith("需要："));
        foreach (var b in v.Buildings.Where(b => !b.Discovered))
        {
            Assert.That(b.Name, Is.Empty, "未发现的楼栋不露名字");
            Assert.That(b.People, Is.Empty);
            Assert.That(b.Services, Is.Empty);
            Assert.That(b.Blocked, Is.EqualTo("尚未发现"));
        }
        Assert.That(v.Buildings.Count(b => !b.Discovered), Is.GreaterThan(0));

        var ac = At("r02_academy");
        var av = E.CityOverviewOf(ac)!;
        var ws = B(av, "r07_workshop");
        if (ws.Discovered)
        {
            Assert.That(ws.Path, Is.Null);
            Assert.That(ws.Blocked, Is.Not.Null.And.Not.Empty);
        }
        Assert.That(B(av, "r05_testing").Path, Is.Not.Null, "检测室公开可达：拒绝检测不锁主线");
        Assert.That(ac.UnlockedEdges, Does.Not.Contain("r06_archive__r07_workshop"), "鸟瞰不自解锁门");
    }

    // ───────────── 当前建筑与只读（CITY-UI04） ─────────────

    /// <summary>CITY-UI04：当前建筑路径为空（进入当前地点 0 时间）；打开/浏览鸟瞰不改任何状态、不推进时间、不发服务。</summary>
    [Test]
    public void CITY_UI04_BrowseIsReadOnly()
    {
        var s = Registered("h04_infirmary");
        var before = SaveSerializer.Serialize(s);
        var v = E.CityOverviewOf(s)!;
        Assert.That(v.HereId, Is.EqualTo("h04_infirmary"));
        Assert.That(B(v, "h04_infirmary").IsHere, Is.True);
        Assert.That(B(v, "h04_infirmary").Path, Is.Empty);
        Assert.That(B(v, "h02_square").IsParent, Is.True);
        Assert.That(B(v, "h04_infirmary").People, Has.Some.Contains("陈芷"));
        Assert.That(SaveSerializer.Serialize(s), Is.EqualTo(before));
        Assert.That(s.CurrentLocation, Is.EqualTo("h04_infirmary"), "不移回广场");
    }

    /// <summary>A10-CITY §7：死亡、对白、战斗/敌踪不能打开；主行动中可以浏览但“前往”写“主行动占用”。</summary>
    [Test]
    public void Blocked_DialogueAndBusy()
    {
        var s = Registered("h02_square");
        Assert.That(E.CityOverviewBlocked(s), Is.Null);
        Assert.That(E.CityTravelBlocked(s), Is.Null);
        Start(s, ActionRequest.Travel("h03_workshop"));
        Assert.That(E.CityOverviewBlocked(s), Is.Null, "主行动中可以浏览");
        Assert.That(E.CityTravelBlocked(s), Does.StartWith("主行动占用"));
        Assert.That(E.CityOverviewOf(s)!.TravelBlocked, Does.StartWith("主行动占用"));
        E.Cancel(s);
        s.Progress.Dead = true;
        Assert.That(E.CityOverviewBlocked(s), Is.EqualTo("角色已死亡"));
    }

    // ───────────── 首次到达自动打开（CITY-UI01） ─────────────

    /// <summary>CITY-UI01：首次真实到达父节点记待打开；对白未读完、行动中不到时机；记已展示后再到达、读档不重复；不补奖励。</summary>
    [Test]
    public void CITY_UI01_FirstArrivalOncePerCity()
    {
        var s = Registered("h03_workshop");
        Assert.That(s.City.OverviewPending, Is.Null, "到城内建筑不自动打开");
        Start(s, ActionRequest.Travel("h02_square"));
        Assert.That(s.City.OverviewPending, Is.Null, "移动中不提前打开");
        Run(s);
        Assert.That(s.CurrentLocation, Is.EqualTo("h02_square"));
        Assert.That(s.City.OverviewPending, Is.EqualTo("h02_square"));
        if (E.DialogueLocked(s)) Assert.That(E.CityOverviewAutoOpenDue(s), Is.False, "对白读完后才打开");
        ReadDialogues(s);
        Assert.That(E.CityOverviewAutoOpenDue(s), Is.True);
        var items = s.Items.Count;
        var flags = s.Progress.Flags.Count;
        Assert.That(E.MarkCityOverviewShown(s), Is.False, "不是旧档补显");
        Assert.That(s.City.OverviewShown, Does.Contain("h02_square"));
        Assert.That(E.CityOverviewAutoOpenDue(s), Is.False);
        Assert.That(s.Items.Count, Is.EqualTo(items));
        Assert.That(s.Progress.Flags.Count, Is.EqualTo(flags), "不写事实、不补奖励");

        var m = SaveSerializer.Deserialize(SaveSerializer.Serialize(s), Cfg);
        Assert.That(E.CityOverviewAutoOpenDue(m), Is.False, "读档不重复");
        Start(m, ActionRequest.Travel("h04_infirmary")); Run(m); ReadDialogues(m);
        Start(m, ActionRequest.Travel("h02_square")); Run(m); ReadDialogues(m);
        Assert.That(m.City.OverviewPending, Is.Null, "再次到达不重复");

        // 另一座城单独计
        var c = At("c01_pilgrim");
        E.DebugMapTeleport(c, "c02_church");
        Assert.That(c.City.OverviewPending, Is.EqualTo("c02_church"));
        E.DebugResetCityOverview(m);
        Assert.That(m.City.OverviewShown, Is.Empty);
    }

    /// <summary>存档 40→41：人在城内且到过父节点的记旧档补显一次（不补奖励）；到过但不在城里的记已展示；当前版本不重复迁移。</summary>
    [Test]
    public void Migration40To41_LegacyInCityOnce()
    {
        var s = Registered("h02_square");
        E.DebugMapTeleport(s, "c02_church");
        E.DebugMapTeleport(s, "c04_alms");
        ReadDialogues(s);
        var j = JObject.Parse(SaveSerializer.Serialize(s));
        j["SchemaVersion"] = "40";
        var city = (JObject)j["City"]!;
        city.Remove("OverviewShown"); city.Remove("OverviewPending"); city.Remove("OverviewPendingLegacy");
        var m = SaveSerializer.Deserialize(j.ToString(), Cfg, out _);
        Assert.That(m.SchemaVersion, Is.EqualTo("41"));
        Assert.That(m.Log.Count(l => l.Text.Contains("存档已升级到版本41")), Is.EqualTo(1));
        Assert.That(m.City.OverviewPending, Is.EqualTo("c02_church"));
        Assert.That(m.City.OverviewPendingLegacy, Is.True);
        Assert.That(m.City.OverviewShown, Does.Contain("h02_square"));
        Assert.That(m.Items.Count, Is.EqualTo(s.Items.Count));
        Assert.That(m.Progress.Flags, Is.EquivalentTo(s.Progress.Flags));
        Assert.That(E.CityOverviewAutoOpenDue(m), Is.True);
        Assert.That(E.MarkCityOverviewShown(m), Is.True, "旧档补显：界面写说明");
        var again = SaveSerializer.Deserialize(SaveSerializer.Serialize(m), Cfg);
        Assert.That(again.Log.Count(l => l.Text.Contains("存档已升级到版本41")), Is.EqualTo(1), "迁移只一次");
        Assert.That(again.City.OverviewPending, Is.Null);
    }

    // ───────────── 前往（CITY-UI05/06/07、UI-X02） ─────────────

    /// <summary>CITY-UI05/07：工坊点鉴定所“前往”只提交第一段 h03→h02（真实移动、按行动规则计时）；同一请求号双击只走一次；到达后再选下一段。</summary>
    [Test]
    public void CITY_UI07_FirstLegOnly_RequestOnce()
    {
        var s = Registered("h03_workshop");
        var t0 = s.GameMinute;
        var r = E.StartCityOverviewTravel(s, "h05_appraisal", "city-req-1");
        Assert.That(r.Ok, Is.True, r.Reason);
        Assert.That(s.Main!.Type, Is.EqualTo(ActionType.Travel));
        Assert.That(s.Main.TargetId, Is.EqualTo("h02_square"), "只走第一条相邻边");
        var id = s.Main.ActionId;
        var again = E.StartCityOverviewTravel(s, "h05_appraisal", "city-req-1");
        Assert.That(again.Duplicate, Is.True, "重复点击不重复走路");
        Assert.That(s.Main.ActionId, Is.EqualTo(id));
        Assert.That(E.StartCityOverviewTravel(s, "h05_appraisal", "city-req-2").Ok, Is.False, "主行动占用");
        Run(s);
        ReadDialogues(s);
        Assert.That(s.CurrentLocation, Is.EqualTo("h02_square"));
        Assert.That(s.GameMinute - t0, Is.GreaterThanOrEqualTo(5), "第一段按真实移动扣时");
        Assert.That(E.StartCityOverviewTravel(s, "h05_appraisal", "city-req-1").Duplicate, Is.True, "旧请求号到达后也不重发");
        Assert.That(s.Main, Is.Null);
        Ok(E.StartCityOverviewTravel(s, "h05_appraisal", "city-req-3"));
        Run(s);
        ReadDialogues(s);
        Assert.That(s.CurrentLocation, Is.EqualTo("h05_appraisal"));
        Assert.That(s.GameMinute - t0, Is.GreaterThanOrEqualTo(10), "两段都真实扣时，不免费跨两边");
        Assert.That(E.StartCityOverviewTravel(s, "h05_appraisal", "city-req-4").Reason, Is.EqualTo("已在此地"));
    }

    /// <summary>UI-X02/CITY-UI06：提交后未抵达前设施栏仍是出发地人物、目标人物不能交谈；中途退出留在出发地；读档后仍在途、不算已到达。</summary>
    [Test]
    public void UI_X02_NoServiceBeforeArrival()
    {
        var s = Registered("h03_workshop");
        Ok(E.StartCityOverviewTravel(s, "h05_appraisal", "x02"));
        Steps(s, 0);
        Assert.That(E.NpcsHere(s).Select(n => n.NpcId), Does.Contain("npc_luo_zheng"));
        Assert.That(E.NpcsHere(s).Select(n => n.NpcId), Does.Not.Contain("npc_tao_cen"));
        Assert.That(E.TalkNpc(s, "npc_tao_cen").Ok, Is.False, "未抵达不服务");
        var m = SaveSerializer.Deserialize(SaveSerializer.Serialize(s), Cfg);
        Assert.That(m.CurrentLocation, Is.EqualTo("h03_workshop"));
        Assert.That(m.Main!.TargetId, Is.EqualTo("h02_square"), "读档不把目标当已到达");
        Ok(E.Cancel(s));
        Assert.That(s.CurrentLocation, Is.EqualTo("h03_workshop"), "退出留在本段起点");
        Assert.That(E.NpcsHere(s).Select(n => n.NpcId), Does.Contain("npc_luo_zheng"));
    }

    /// <summary>CITY-UI08：前往被锁的楼栋直接拒绝并给原因，不规划穿锁路线、不耗时。</summary>
    [Test]
    public void CITY_UI08_TravelToLockedRefused()
    {
        var s = At("h01_gate");
        var t0 = s.GameMinute;
        var r = E.StartCityOverviewTravel(s, "h02_square", "lock");
        Assert.That(r.Ok, Is.False);
        Assert.That(r.Reason, Does.StartWith("需要："));
        Assert.That(s.Main, Is.Null);
        Assert.That(s.GameMinute, Is.EqualTo(t0));
        Assert.That(E.StartCityOverviewTravel(s, "c02_church", "far").Reason, Is.EqualTo("目标不在这座城内"));
    }

    private void Ok(ActionResult r) => Assert.That(r.Ok, Is.True, r.Reason);

    // ───────────── 设施栏人物卡（CITY-UI10/13） ─────────────

    /// <summary>CITY-UI10/13：岗位卡带组织、“在场”与可接短标；离岗由代班接手标“代班”、不复用原人物；人物与公共工坊站等设施同栏共存。</summary>
    [Test]
    public void CITY_UI10_NpcCardPresenceAndMarks()
    {
        var s = Registered("h05_appraisal");
        var tao = E.NpcsHere(s).Single(n => n.NpcId == "npc_tao_cen");
        Assert.That((tao.FactionName, tao.Presence, tao.KindLabel), Is.EqualTo(("幸存者营区", "在场", "岗位")));
        Assert.That(tao.QuestMarks, Is.EqualTo("可接1"), "HU01 可接；HU02 要先完成 HU01");
        s.Progress.Flags.Add(GameEngine.QuestFact("HU01", "completed"));
        s.Progress.Flags.Add(GameEngine.QuestFact("HU02", "completed"));
        Assert.That(E.NpcsHere(s).Single(n => n.NpcId == "npc_tao_cen").QuestMarks, Is.Empty, "已完成不再标");
        E.DebugToggleCityPostAbsent(s);
        var sub = E.NpcsHere(s).Single(n => n.NpcId == "npc_tao_cen");
        Assert.That((sub.Presence, sub.KindLabel, sub.Substitute), Is.EqualTo(("代班", "代班", true)));
        Assert.That(sub.Name, Is.Not.EqualTo("陶岑"), "不复用退场人物");

        var w = Registered("h03_workshop");
        Assert.That(E.NpcsHere(w).Select(n => n.NpcId), Does.Contain("npc_luo_zheng"));
        Assert.That(E.PublicStationsHere(w), Is.Not.Empty, "人物卡不替换设施");
    }

    // ───────────── 任务标记（CITY-UI13） ─────────────

    /// <summary>CITY-UI13：任务标记绑真实状态——鉴定所 HU01 可接；完成后不再标；打开鸟瞰不接、不推进。</summary>
    [Test]
    public void CITY_UI13_QuestMarksFollowRealState()
    {
        var s = Registered("h02_square");
        var v = E.CityOverviewOf(s)!;
        var ap = B(v, "h05_appraisal");
        Assert.That(ap.CanAccept, Is.GreaterThan(0));
        Assert.That(ap.QuestNote, Does.Contain("可接："));
        Assert.That(GameEngine.QuestAccepted(s, "HU01"), Is.False, "标记不自动接任务");
        s.Progress.Flags.Add(GameEngine.QuestFact("HU01", "completed"));
        s.Progress.Flags.Add(GameEngine.QuestFact("HU02", "completed"));
        var v2 = E.CityOverviewOf(s)!;
        Assert.That(B(v2, "h05_appraisal").CanAccept, Is.EqualTo(0), "已完成的不再标可接");
        Assert.That(B(v2, "h05_appraisal").QuestNote, Does.Not.Contain("鉴定之前"));
    }
}
