using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 批次 B35c（地图出发与城市层并入）内核部分。规则来源：S14 2026-11-13 B35 节 Q591／Q595，2026-11-16 B35 节 Q793／Q803／Q804，
/// 2026-11-16 B36 节 Q807／Q808／Q810，2026-11-17 B35 次要节 Q913—Q916；逐字稿 §2 P07a／P07b。
/// 验收：Q591-A01／A02、B35-Q793-01、B36-Q808-01、B36-Q810-01、B35S-Q914-01、B35S-Q915-01（内核部分）。
/// 期望里的地点、路线与分钟按 A10-CITY CITY-UI05（工坊→广场→鉴定所两段各 5 分钟）与 config 地名写字面量。
/// </summary>
public class B35cMapDepartTests : TestBase
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

    private static string Steps(List<MapPathStep>? p) => p == null ? "null" : string.Join("|", p.Select(x => $"{x.From}>{x.To}:{x.Minutes}"));

    // ───────────── 路线区（Q591、Q793 B、Q808、Q915） ─────────────

    /// <summary>没选目的地或选的就是当前位置：路线区没有目的地（显示 Q803 默认句），不给下一路段，也没有出发拦截原因。</summary>
    [Test]
    public void Route_NoTarget()
    {
        var s = Registered("h03_workshop");
        foreach (var t in new[] { null, "h03_workshop" })
        {
            var r = E.MapRouteOf(s, t);
            Assert.That(r.HereId, Is.EqualTo("h03_workshop"));
            Assert.That(r.TargetId, Is.Null);
            Assert.That(r.NextLeg, Is.Null);
            Assert.That(r.Blocked, Is.Null);
        }
    }

    /// <summary>Q591／Q808：相邻目的地——下一路段就是直连这一段（工坊→广场 5 分钟，CITY-UI05），出发不被结构性拦截。</summary>
    [Test]
    public void Route_Adjacent_NextLegIsDirect()
    {
        var s = Registered("h03_workshop");
        var r = E.MapRouteOf(s, "h02_square");
        Assert.That(r.TargetId, Is.EqualTo("h02_square"));
        Assert.That(r.Adjacent, Is.True);
        Assert.That(Steps(r.Path), Is.EqualTo("h03_workshop>h02_square:5"));
        Assert.That((r.NextLeg!.From, r.NextLeg.To, r.NextLeg.Minutes), Is.EqualTo(("h03_workshop", "h02_square", 5)));
        Assert.That(r.SelectNext, Is.Null, "相邻不需要“选中下一段”");
        Assert.That(r.Blocked, Is.Null);
    }

    /// <summary>
    /// B35-Q793-01、B35S-Q915-01：远处目的地只看路线——已知路线两段、下一路段为第一段、“选中下一段”指向真实相邻首站，
    /// 出发拦截只写“先选相邻地点。”（Q915 B 原句）。
    /// </summary>
    [Test]
    public void Route_Remote_OnlyView_PickAdjacent()
    {
        var s = Registered("h03_workshop");
        var r = E.MapRouteOf(s, "h05_appraisal");
        Assert.That(r.Adjacent, Is.False);
        Assert.That(Steps(r.Path), Is.EqualTo("h03_workshop>h02_square:5|h02_square>h05_appraisal:5"));
        Assert.That((r.NextLeg!.From, r.NextLeg.To), Is.EqualTo(("h03_workshop", "h02_square")));
        Assert.That(r.SelectNext, Is.EqualTo("h02_square"));
        Assert.That(r.Blocked, Is.EqualTo("先选相邻地点。"));
        // 改选相邻首站后同一函数给相邻结果（Q793 B：明确改选才可出发）
        var n = E.MapRouteOf(s, r.SelectNext);
        Assert.That((n.Adjacent, n.Blocked), Is.EqualTo((true, (string?)null)));
    }

    /// <summary>B35S-Q915-01：其他真实拒绝原因不被“先选相邻地点。”盖掉——没有路线时写原“目前没有已开通的路线能走到这里”，未发现写“尚未发现”。</summary>
    [Test]
    public void Route_OtherReasonsNotOverridden()
    {
        var s = Registered("h03_workshop");
        s.Locations["c02_church"].Discovered = true;   // 已知但没有已开通的已知路线
        var r = E.MapRouteOf(s, "c02_church");
        Assert.That(r.Blocked, Is.Not.EqualTo("先选相邻地点。"));
        Assert.That(r.Path, Is.Null);
        Assert.That(r.SelectNext, Is.Null);
        Assert.That(r.Blocked, Is.EqualTo("目前没有已开通的路线能走到这里"), "MapOverlay 原句");
        s.Locations["c04_alms"].Discovered = false;
        Assert.That(E.MapRouteOf(s, "c04_alms").Blocked, Is.EqualTo("尚未发现"));
    }

    // ───────────── 出发（Q591-A01／A02、Q793 B） ─────────────

    /// <summary>Q591-A01：点目的地（看路线）不移动、不耗时、不改存档；只有出发才提交一次相邻移动。</summary>
    [Test]
    public void Depart_SelectDoesNotMove_DepartStartsOnce()
    {
        var s = Registered("h03_workshop");
        var before = SaveSerializer.Serialize(s);
        E.MapRouteOf(s, "h02_square");
        E.MapRouteOf(s, "h05_appraisal");
        E.BuildMapView(s, "city:h02_square");
        E.CityOverviewOf(s, "h02_square");
        Assert.That(SaveSerializer.Serialize(s), Is.EqualTo(before), "选中、看路线、切层都不改状态");
        var r = E.StartMapDeparture(s, "h02_square", "go-1");
        Assert.That(r.Ok, Is.True, r.Reason);
        Assert.That((s.Main!.Type, s.Main.TargetId), Is.EqualTo((ActionType.Travel, "h02_square")));
        Assert.That(E.StartMapDeparture(s, "h02_square", "go-1").Duplicate, Is.True, "同一请求号只生效一次");
    }

    /// <summary>B35-Q793-01：远处出发被拒（不替玩家选站、不连走）；到站后也不自动继续。</summary>
    [Test]
    public void Depart_Remote_Refused_NoAutoContinue()
    {
        var s = Registered("h03_workshop");
        var t0 = s.GameMinute;
        var r = E.StartMapDeparture(s, "h05_appraisal", "far");
        Assert.That((r.Ok, r.Reason), Is.EqualTo((false, "先选相邻地点。")));
        Assert.That(s.Main, Is.Null);
        Assert.That(s.GameMinute, Is.EqualTo(t0));
        Assert.That(E.StartMapDeparture(s, "h02_square", "near").Ok, Is.True);
        Run(s);
        ReadDialogues(s);
        Assert.That(s.CurrentLocation, Is.EqualTo("h02_square"));
        Assert.That(s.Main, Is.Null, "到站不自动继续");
    }

    /// <summary>Q591-A02：远征进入仍走原确认——缓冲点走向危险房间，“出发”提交的就是带远征确认的原移动（开始远征）。</summary>
    [Test]
    public void Depart_ExpeditionEntry_SameAsConfirmRequest()
    {
        var s = B21EntryTests.AtBuffer(E, 2112);
        Assert.That(E.NeedsExpeditionConfirm(s, "d01_02"), Is.True);
        Assert.That(s.Expedition, Is.Null);
        var r = E.StartMapDeparture(s, "d01_02", "exp");
        Assert.That(r.Ok, Is.True, r.Reason);
        Assert.That(s.Expedition, Is.Not.Null, "与原“确认开始远征并前往”同一请求：开始远征");
        Assert.That(s.Main!.TargetId, Is.EqualTo("d01_02"));
    }

    /// <summary>Q591-A02：未通关离开仍被原规则拒绝（原因带“未通关离开要先终止本次远征”），出发不绕过后果。</summary>
    [Test]
    public void Depart_LeaveUnfinished_Refused()
    {
        var s = B21EntryTests.AtBuffer(E, 2112);
        Assert.That(E.StartMapDeparture(s, "d01_02", "exp").Ok, Is.True);
        Run(s);
        ReadDialogues(s);
        Assert.That(E.StartMapDeparture(s, "d01_01", "back").Ok, Is.True);
        Run(s);
        ReadDialogues(s);
        var t0 = s.GameMinute;
        var leave = E.StartMapDeparture(s, "c03_crypt", "leave");
        Assert.That(leave.Ok, Is.False);
        Assert.That(leave.Reason, Does.Contain("未通关离开要先终止本次远征"));
        Assert.That(s.GameMinute, Is.EqualTo(t0), "不扣费");
    }

    // ───────────── 当前可出发的相邻连线（Q591-A01） ─────────────

    /// <summary>Q591-A01：城市层与普通层都只把“从当前位置出发、已开通、可开工”的相邻边标为可出发；不相连的边不标。</summary>
    [Test]
    public void Departable_OnlyRealAdjacentLegs()
    {
        var s = Registered("h03_workshop");
        var v = E.CityOverviewOf(s, "h02_square")!;
        var roads = v.Roads.ToDictionary(r => string.CompareOrdinal(r.A, r.B) < 0 ? r.A + "|" + r.B : r.B + "|" + r.A);
        Assert.That(roads["h02_square|h03_workshop"].Departable, Is.True);
        Assert.That(roads["h02_square|h05_appraisal"].Departable, Is.False, "不与当前位置相连");
        var map = E.BuildMapView(s, "city:h02_square");
        Assert.That(map.Edges.Where(e => e.Departable).Select(e => e.A == "h03_workshop" ? e.B : e.A), Is.EquivalentTo(E.AdjacentUnlocked(s).Select(x => x.Other("h03_workshop")).Where(x => E.CityOf(x) == "h02_square")));
        Assert.That(map.Edges.Count(e => e.Departable), Is.GreaterThan(0));
    }

    /// <summary>Q591-A01：被权限阻断（主行动进行中）时相邻边不冒充可出发；与上一条“确实可出发”配对。</summary>
    [Test]
    public void Departable_FalseWhenBusy()
    {
        var s = Registered("h03_workshop");
        Assert.That(E.BuildMapView(s, "city:h02_square").Edges.Count(e => e.Departable), Is.GreaterThan(0), "空闲时有可出发边");
        Start(s, ActionRequest.Rest(60));
        Assert.That(E.BuildMapView(s, "city:h02_square").Edges.Count(e => e.Departable), Is.EqualTo(0), "主行动占用：不高亮");
        Assert.That(E.CityOverviewOf(s, "h02_square")!.Roads.Count(e => e.Departable), Is.EqualTo(0));
    }

    /// <summary>Q591-A01：未通关的远征中，通往地牢外的边不标可出发（原规则拒绝），地牢内相邻边照常可出发。</summary>
    [Test]
    public void Departable_LeaveUnfinishedNotHighlighted()
    {
        var s = B21EntryTests.AtBuffer(E, 2112);
        Assert.That(E.StartMapDeparture(s, "d01_02", "exp").Ok, Is.True);
        Run(s); ReadDialogues(s);
        Assert.That(E.StartMapDeparture(s, "d01_01", "back").Ok, Is.True);
        Run(s); ReadDialogues(s);
        var world = E.BuildMapView(s, GameEngine.WorldLayer);
        Assert.That(world.Edges.Where(e => e.Near).Any(e => e.Departable), Is.False, "去地牢外不冒充可走");
        var dungeon = E.BuildMapView(s, E.MapLayerOf("d01_01"));
        Assert.That(dungeon.Edges.Any(e => e.Departable && (e.A == "d01_02" || e.B == "d01_02")), Is.True, "地牢内相邻照常可走");
    }

    // ───────────── 远城只读（Q810） ─────────────

    /// <summary>
    /// B36-Q810-01：人在营区、查看教会城市层——远城视图只读：建筑可看公开名称、人物与服务，未发现仍遮蔽；不给路线与可出发标记；
    /// 当前位置仍是真实所在地；从远城建筑出发被拒、不移动。与本城视图（Remote=false、有路线）配对。
    /// </summary>
    [Test]
    public void RemoteCity_ReadOnly()
    {
        var s = Registered("h02_square");
        foreach (var id in E.CityMembers("c02_church")) s.Locations[id].Discovered = true;
        s.Locations["c07_sanctuary"].Discovered = false;
        var own = E.CityOverviewOf(s, "h02_square")!;
        Assert.That(own.Remote, Is.False);
        Assert.That(own.Buildings.Single(b => b.NodeId == "h03_workshop").Path, Is.Not.Null, "本城有路线");
        var v = E.CityOverviewOf(s, "c02_church")!;
        Assert.That(v.Remote, Is.True);
        Assert.That(v.HereId, Is.EqualTo("h02_square"), "当前位置为真实所在地");
        Assert.That(v.Buildings.Count, Is.EqualTo(5), "教会父节点＋四栋（CITY-UI09）");
        Assert.That(v.Buildings.All(b => b.Path == null && !b.IsHere), Is.True, "远城不给路线");
        Assert.That(v.Roads.Any(r => r.Departable || r.Near), Is.False);
        var alms = v.Buildings.Single(b => b.NodeId == "c04_alms");
        Assert.That(alms.Name, Is.EqualTo("施济处"));
        var sanct = v.Buildings.Single(b => b.NodeId == "c07_sanctuary");
        Assert.That((sanct.Name, sanct.People.Count, sanct.Services.Count), Is.EqualTo(("", 0, 0)), "未发现仍遮蔽");
        var t0 = s.GameMinute;
        Assert.That(E.StartMapDeparture(s, "c04_alms", "remote").Ok, Is.False);
        Assert.That(s.Main, Is.Null);
        Assert.That((s.CurrentLocation, s.GameMinute), Is.EqualTo(("h02_square", t0)), "所在地不变、不耗时");
    }

    // ───────────── 层（Q595、Q807） ─────────────

    /// <summary>Q595：人在城内打开地图的默认层就是本城城市层；不合法的层退回主世界（与 BuildMapView 同一口径）。</summary>
    [Test]
    public void ResolveMapLayer_DefaultCityLayer()
    {
        var s = Registered("h03_workshop");
        Assert.That(E.ResolveMapLayer(s, null), Is.EqualTo("city:h02_square"));
        Assert.That(E.ResolveMapLayer(s, GameEngine.WorldLayer), Is.EqualTo(GameEngine.WorldLayer));
        Assert.That(E.ResolveMapLayer(s, "city:c02_church"), Is.EqualTo(GameEngine.WorldLayer), "未知城市层退回主世界");
        Assert.That(E.ResolveMapLayer(s, "nonsense"), Is.EqualTo(GameEngine.WorldLayer));
    }

    // ───────────── 变异测试补测（Stryker 9a1c88e4..9b63129b 存活／无覆盖项） ─────────────

    private GameState Unregistered(string loc)
    {
        var s = New();
        s.GameMinute = At(2, 9, 0);
        s.SpawnNextCheck = int.MaxValue;
        s.PatrolNextCheck = int.MaxValue;
        E.DebugMapTeleport(s, loc);
        ReadDialogues(s);
        return s;
    }

    /// <summary>A10-EXUI §1 原右栏按钮“选中下一段：前往{站}”（Q793 B 保留，移进路线区）。</summary>
    [Test]
    public void Mut_SelectNextText()
    {
        Assert.That(MapCopy.SelectNext("营区广场"), Is.EqualTo("选中下一段：前往营区广场"));
    }

    /// <summary>
    /// S14 Q595“势力鸟瞰仅在已知所选／当前城市可看时进入该城”、A10-EXUI 未知不泄露：未发现的主城没有城市层视图，
    /// 不是主城父节点（城内建筑本身）也没有；与已知本城有视图配对。
    /// </summary>
    [Test]
    public void Mut_CityOverviewOf_OnlyKnownCityParent()
    {
        var s = Registered("h03_workshop");
        Assert.That(E.CityOverviewOf(s, "h02_square"), Is.Not.Null, "已知本城有视图");
        Assert.That(s.Locations["c02_church"].Discovered, Is.False, "前提：教会未发现");
        Assert.That(E.CityOverviewOf(s, "c02_church"), Is.Null, "未发现的主城不给城市层");
        Assert.That(s.Locations["h03_workshop"].Discovered, Is.True, "前提：工坊已知");
        Assert.That(E.CityOverviewOf(s, "h03_workshop"), Is.Null, "城内建筑不是主城父节点");
    }

    /// <summary>B36-Q810-01（Q810“未发现仍遮蔽”、A10-CITY CITY-UI08“尚未发现”）：远城里未发现的建筑标“尚未发现”，已发现的不标拦截。</summary>
    [Test]
    public void Mut_RemoteCity_UndiscoveredBlocked()
    {
        var s = Registered("h02_square");
        foreach (var id in E.CityMembers("c02_church")) s.Locations[id].Discovered = true;
        s.Locations["c07_sanctuary"].Discovered = false;
        var v = E.CityOverviewOf(s, "c02_church")!;
        Assert.That(v.Remote, Is.True);
        Assert.That(v.Buildings.Single(b => b.NodeId == "c07_sanctuary").Blocked, Is.EqualTo("尚未发现"));
        Assert.That(v.Buildings.Single(b => b.NodeId == "c04_alms").Blocked, Is.Null);
    }

    /// <summary>
    /// 城市层道路（A10-CITY §4 道路悬停“未开通：{原因}”、CITY-UI08）：已开通的路无原因；未开通但已揭示的路带门槛原因（营门登记边，原因以“需要：”开头，
    /// 同 B24 CITY_UI08 楼栋原因）。Near＝与当前位置相连（MapViewEdge.Near 原义），本城才有，远城没有。
    /// </summary>
    [Test]
    public void Mut_CityRoads_ReasonAndNear()
    {
        var s = Registered("h03_workshop");
        var v = E.CityOverviewOf(s, "h02_square")!;
        string K(MapViewEdge r) => string.CompareOrdinal(r.A, r.B) < 0 ? r.A + "|" + r.B : r.B + "|" + r.A;
        var roads = v.Roads.ToDictionary(K);
        var near = roads["h02_square|h03_workshop"];
        var far = roads["h02_square|h05_appraisal"];
        Assert.That((near.Open, near.Reason, near.Near), Is.EqualTo((true, "", true)));
        Assert.That((far.Open, far.Reason, far.Near), Is.EqualTo((true, "", false)), "不与当前位置相连");
        // 已开通（锁存）的路即使门槛条件此刻不再满足，也仍是已开通、不写未开通原因（S02 Q30B 门槛契约：除每次重验的单向出口外开通后锁存）
        s.Progress.Flags.ExceptWith(new[] { "registration_authorized", "hub_registered" });
        Assert.That(E.GateBlockedReason(s, E.EdgeBetween("h02_square", "h03_workshop")!), Does.StartWith("需要："), "前提：登记门槛此刻不满足");
        var latched = E.CityOverviewOf(s, "h02_square")!.Roads.Single(r => K(r) == "h02_square|h03_workshop");
        Assert.That((latched.Open, latched.Reason), Is.EqualTo((true, "")));
        var g = Unregistered("h01_gate");
        var gv = E.CityOverviewOf(g, "h02_square")!;
        var gate = gv.Roads.Single(r => K(r) == "h01_gate|h02_square");
        Assert.That(gate.Open, Is.False);
        Assert.That(gate.Reason, Does.StartWith("需要："));
        Assert.That(gate.Near, Is.True);
    }

    /// <summary>B35S-Q915-01：别的相邻未开通路线的原因不借给远处目的地——营门旁检查哨路线未开通时，选城内远处工坊仍是“先选相邻地点。”；选检查哨本身才是“路线未开通，需要：…”。</summary>
    [Test]
    public void Mut_Route_OtherLockedEdgeNotBorrowed()
    {
        var s = Registered("h01_gate");
        s.Locations["m02_checkpoint"].Discovered = true;
        Assert.That(E.AdjacentLocked(s).Select(x => x.Target), Is.EqualTo(new[] { "m02_checkpoint" }), "前提：检查哨路线未开通且已揭示");
        Assert.That(E.MapRouteOf(s, "m02_checkpoint").Blocked, Does.StartWith("路线未开通，需要："));
        var r = E.MapRouteOf(s, "h03_workshop");
        Assert.That(r.Blocked, Is.EqualTo("先选相邻地点。"));
        Assert.That(Steps(r.Path), Is.EqualTo("h01_gate>h02_square:5|h02_square>h03_workshop:5"), "A08：营门—广场、广场—工坊各 5 分钟");
    }

    /// <summary>B35-Q793-01 主世界层（不在城内）：远处目的地照样按真实已知路线给第一段与“选中下一段”，出发拦截“先选相邻地点。”（A08：农场、林地各距广场 25 分钟）。</summary>
    [Test]
    public void Mut_Route_WorldRemote()
    {
        var s = Registered("w01_farm");
        s.Locations["w02_forest"].Discovered = true;
        var r = E.MapRouteOf(s, "w02_forest");
        Assert.That(Steps(r.Path), Is.EqualTo("w01_farm>h02_square:25|h02_square>w02_forest:25"));
        Assert.That((r.SelectNext, r.Blocked), Is.EqualTo(("h02_square", "先选相邻地点。")));
    }

    /// <summary>B35S-Q915-01（其他真实原因照原）：城内远处建筑走不到时写城内路线上第一条未开通边的门槛原因（登记前营门→广场→工坊，原因以“需要：”开头，同 B24 CITY_UI08），不写主世界的“目前没有已开通的路线能走到这里”。</summary>
    [Test]
    public void Mut_Route_SameCityBlockedReason()
    {
        var s = Unregistered("h01_gate");
        // 已揭示的城内路（S02 揭示规则：从探索满 100% 的已到过端点可见）：营门、广场都已到过且探索满
        foreach (var id in new[] { "h01_gate", "h02_square" }) { var l = s.Locations[id]; l.Discovered = true; l.Visited = true; l.Progress = 100; }
        s.Locations["h03_workshop"].Discovered = true;
        var r = E.MapRouteOf(s, "h03_workshop");
        Assert.That(r.Path, Is.Null);
        Assert.That(r.Blocked, Does.StartWith("需要："));
        Assert.That(r.Blocked, Is.Not.EqualTo("目前没有已开通的路线能走到这里"));
    }

    /// <summary>原城市出发“已在此地”（B24 StartCityOverviewTravel、原移动校验）：对当前位置出发被拒，不耗时、不开行动。</summary>
    [Test]
    public void Mut_Depart_SameLocationRefused()
    {
        var s = Registered("h03_workshop");
        var t0 = s.GameMinute;
        var r = E.StartMapDeparture(s, "h03_workshop", "here");
        Assert.That((r.Ok, r.Reason), Is.EqualTo((false, "已在此地")));
        Assert.That(s.Main, Is.Null);
        Assert.That(s.GameMinute, Is.EqualTo(t0));
    }

    /// <summary>原鸟瞰“遭遇中拒绝出发”（B24 CITY-UI06）：战斗中出发被拒，原因是原移动校验句“战斗中：请使用战斗指令”，不开行动；战斗结束后同一目的地可出发。</summary>
    [Test]
    public void Mut_Depart_InBattleRefused()
    {
        var s = Registered("h03_workshop");
        s.Battle = new BattleState();
        var r = E.StartMapDeparture(s, "h02_square", "b1");
        Assert.That((r.Ok, r.Reason), Is.EqualTo((false, "战斗中：请使用战斗指令")));
        Assert.That(s.Main, Is.Null);
        s.Battle = null;
        Assert.That(E.StartMapDeparture(s, "h02_square", "b2").Ok, Is.True);
    }
}
