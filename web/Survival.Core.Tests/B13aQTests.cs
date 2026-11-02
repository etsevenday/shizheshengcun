using Newtonsoft.Json.Linq;
using Survival.Core.Config;
using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 批次 B13a（R124—R128）：策划提交 7154269 答复待确认 Q114—Q119。出处 S02/S03/S09/S13/S14/R08/A08/A11 本轮章、
/// assets/20261102/map/map-adjudications.json、gate-contract.json stateRules、party-adjudications.json serviceLocations.v3Activation；
/// 验收 A10「Q114—Q119 可判定验收」MAP-Q114-01、MAP-Q115-01/02、MAP-Q116-01、MAP-Q117-01、MAP-Q118-01/02、HOUSE-Q118-01/02、PARTY-Q119-01—03。
/// </summary>
public class B13aQTests : TestBase
{
    private static JObject Adj() =>
        JObject.Parse(File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "design.map-adjudications.json")));

    private static readonly string[] Starter = { "camp", "field", "stream", "woods", "village", "hardware", "clinic", "scrapyard" };

    private string LastDone(GameState s) => s.Log.Last(l => l.Kind == GameEventKind.ActionCompleted.ToString()).Text;

    private string Explore(GameState s)
    {
        Start(s, ActionRequest.Explore());
        Assert.That(Run(s), Is.EqualTo(SegmentStatus.Completed));
        return LastDone(s);
    }

    private GameState Reload(GameState s) => SaveSerializer.Deserialize(SaveSerializer.Serialize(s), Cfg, out _);

    // ───────────── R124 / Q114 ─────────────

    /// <summary>MAP-Q114-01：村庄↔诊所 ↙/↗、五金↔废料场 →/←（与 map-adjudications 同源），其余 6 组不变；跨图层（父地点↔地牢房间）不画方位。</summary>
    [Category("DesignSync")]
    [Test]
    public void MAP_Q114_01_LegacyDirectionsFromGrid_CrossLayerNone()
    {
        foreach (var p in (JArray)Adj()["direction"]!["changedLegacyPairs"]!)
        {
            var a = E.Location((string)p["from"]!);
            var b = E.Location((string)p["to"]!);
            Assert.That(GameEngine.DirectionArrow(GameEngine.DirectionBetween(a, b)), Is.EqualTo((string)p["forward"]!), a.Id);
            Assert.That(GameEngine.DirectionArrow(GameEngine.DirectionBetween(b, a)), Is.EqualTo((string)p["reverse"]!), b.Id);
        }
        var six = new (string a, string b, string ab)[]
        {
            ("camp", "field", "→"), ("field", "stream", "↙"), ("field", "woods", "→"),
            ("field", "village", "↓"), ("village", "hardware", "→"), ("woods", "scrapyard", "↘"),
        };
        foreach (var (a, b, ab) in six)
            Assert.That(GameEngine.DirectionArrow(GameEngine.DirectionBetween(E.Location(a), E.Location(b))), Is.EqualTo(ab), $"{a}→{b}");
        // 坐标同一换算：mapX = 130 + 280·x、mapY = 100 + 200·y
        foreach (var l in Cfg.Locations.Where(l => !l.IsDungeon))
            Assert.That((l.MapX, l.MapY), Is.EqualTo((130 + 280.0 * l.GridX, 100 + 200.0 * l.GridY)), l.Id);

        var s = New();
        E.DebugTeleport(s, "w33_waterworks");
        E.DebugSetProgress(s, "w33_waterworks", 100);
        Assert.That(s.Locations["d05_01"].Discovered, Is.True);
        Assert.That(E.DirectionFromHere(s, "d05_01"), Is.EqualTo(MapDirection.None), "跨图层不画方位");
    }

    // ───────────── R125 / Q115 ─────────────

    /// <summary>
    /// MAP-Q115-01：c06/c07 都没到访时提交 church_contacted（到达 c02），c06↔c07 立即锁存并揭示两端；两端 visited/探索度仍原值、不自动 100、不发奖。
    /// 远方地牢入口（FA02 路线事实开 c03→d01_01）只发现，不凭发现创建地牢实例。
    /// </summary>
    [Test]
    public void MAP_Q115_01_FactOpensRemoteEdge_DiscoverOnly()
    {
        var s = New();
        var items = s.Items.Count;
        E.DebugMapTeleport(s, "c02_church");
        Assert.That(s.UnlockedEdges, Does.Contain("c06_archive__c07_sanctuary"), "不要求任一端已到访");
        foreach (var id in new[] { "c06_archive", "c07_sanctuary" })
        {
            var ls = s.Locations[id];
            Assert.That((ls.Discovered, ls.Visited, ls.Progress), Is.EqualTo((true, false, 0)), id);
            Assert.That(ls.TriggeredMilestones, Is.Empty, id);
        }
        Assert.That(s.Items.Count, Is.EqualTo(items), "不发物资");
        Assert.That(E.EdgeOpen(s, E.Edge("c06_archive__c07_sanctuary")), Is.True);
        Assert.That(E.MapPathTo(s, "c06_archive"), Is.Not.Null.And.Not.Empty, "远方已知边照实算连续路线（要真实走过去）");
        Assert.That(s.CurrentLocation, Is.EqualTo("c02_church"), "不传送");

        E.DebugMapFact(s, "FA02_route_briefed");
        var crypt = Cfg.Edges.Single(e => e.Touches("c03_crypt") && e.Other("c03_crypt") == "d01_01");
        Assert.That(E.EdgeOpen(s, crypt), Is.True);
        Assert.That(s.Locations["d01_01"].Discovered, Is.True);
        Assert.That(s.Locations["d01_01"].Visited, Is.False);
        Assert.That(s.DungeonInstances.ContainsKey("d01"), Is.False, "远方地牢不凭发现创建实例");
        Assert.That(s.DungeonInstances.Values.SelectMany(i => i.UnlockedEdges).Any(), Is.False, "实例边仍按该代谓词，不跟着开");
        Assert.That(s.Progress.Flags, Does.Not.Contain("hub_registered").And.Not.Contain("registration_authorized"), "不凭发现写登记事实");
    }

    /// <summary>
    /// MAP-Q115-02：旧档已满足事实但远边未锁（B11 口径），读档补锁存与发现、不写到访/探索度/日志/奖励；缺事实的不开；
    /// 换代后的旧实例边与单向出口不跨代开放。重复读档幂等。
    /// </summary>
    [Test]
    public void MAP_Q115_02_LoadRejudges_NoRewards_InstanceNotCrossGeneration()
    {
        var s = New();
        s.Progress.Flags.Add("church_contacted");   // 旧档：事实在，但两端都没到过，B11 口径没锁
        E.DebugTeleport(s, "d05_01");
        E.DebugSetProgress(s, "d05_01", 100);
        Assert.That(s.DungeonInstances["d05"].UnlockedEdges, Does.Contain("d05_01__d05_02"));
        E.DebugTeleport(s, "w33_waterworks");
        Assert.That(E.NewDungeonGeneration(s, "d05"), Is.Null);
        s.UnlockedEdges.Remove("c06_archive__c07_sanctuary");
        foreach (var e in Cfg.Edges.Where(e => e.Gate == "church_public_access")) { s.UnlockedEdges.Remove(e.Id); }
        foreach (var id in new[] { "c02_church", "c04_alms", "c05_admonition", "c06_archive", "c07_sanctuary" }) s.Locations[id].Discovered = false;
        var logs = s.Log.Count;
        var items = s.Items.Count;

        var m = Reload(s);
        Assert.That(m.UnlockedEdges, Is.SupersetOf(Cfg.Edges.Where(e => e.Gate == "church_public_access").Select(e => e.Id)), "加载补锁");
        Assert.That(m.Locations["c06_archive"].Discovered, Is.True);
        Assert.That(m.Locations["c02_church"].Visited, Is.False);
        Assert.That(m.Locations["c02_church"].Progress, Is.EqualTo(0), "不提前把公开地点置 100");
        Assert.That(m.Log.Count, Is.EqualTo(logs), "不补日志");
        Assert.That(m.Items.Count, Is.EqualTo(items), "不补物资");
        Assert.That(Cfg.Edges.Where(e => e.Gate == "register").Any(e => m.UnlockedEdges.Contains(e.Id)), Is.False, "缺登记事实的不开");
        Assert.That(m.DungeonInstances["d05"].UnlockedEdges, Does.Not.Contain("d05_01__d05_02"), "换代后旧实例边不跨代");
        Assert.That(E.EdgeOpen(m, E.Edge("d05_08__w33_waterworks")), Is.False, "出口每次重验当前代");
        Assert.That(SaveSerializer.Serialize(Reload(m)), Is.EqualTo(SaveSerializer.Serialize(m)), "再读一次不变（幂等）");
    }

    // ───────────── R126 / Q116 ─────────────

    /// <summary>
    /// MAP-Q116-01：本点处于双向边 to 端时 100% 仍揭示该边（门槛未达只显示锁与具体原因、远端只记已发现）；单向出口的 to 端不揭示逆向；旧 8 地点同样适用；揭示≠开通。
    /// </summary>
    [Test]
    public void MAP_Q116_01_Reveal100_AllIncidentEdges_OneWayOutgoingOnly()
    {
        var s = New();
        E.DebugTeleport(s, "w12_canal");
        Assert.That(s.Locations["w11_orchard"].Discovered, Is.False);
        E.DebugSetProgress(s, "w12_canal", 100);
        var lk = E.AdjacentLocked(s).Single(x => x.Target == "w11_orchard");
        Assert.That(lk.Edge.B, Is.EqualTo("w12_canal"), "本点是数据 to 端");
        Assert.That(lk.Reason, Does.Contain("荒弃果园").And.Contain("100%"), "写具体原因");
        Assert.That((s.Locations["w11_orchard"].Discovered, s.Locations["w11_orchard"].Visited, s.Locations["w11_orchard"].Progress), Is.EqualTo((true, false, 0)));
        Assert.That(E.EdgeOpen(s, lk.Edge), Is.False, "揭示不等于开通");

        // 单向出口：w33 是 d05_08→w33 的 to 端，w33 100% 不揭示逆向
        E.DebugTeleport(s, "w33_waterworks");
        E.DebugSetProgress(s, "w33_waterworks", 100);
        Assert.That(E.AdjacentLocked(s).Any(x => x.Target == "d05_08"), Is.False);
        Assert.That(s.Locations["d05_08"].Discovered, Is.False, "单向只算 from");
        Assert.That(E.EdgeRevealed(s, E.Edge("d05_08__w33_waterworks")), Is.False);

        // 旧 8 地点：诊所是 village__clinic 的 to 端；直接到诊所探索 100 时村庄那条（village100 未满足）显示为未开通
        var t = New();
        E.DebugTeleport(t, "clinic");
        E.DebugSetProgress(t, "clinic", 100);
        var back = E.AdjacentLocked(t).Single(x => x.Target == "village");
        Assert.That(back.Reason, Does.Contain("废弃村庄"));
    }

    // ───────────── R127 / Q117 ─────────────

    /// <summary>MAP-Q117-01：45 点恰各一条逐点 75% 文案（同源 map-adjudications），只记一次；不加物资/揭路/揭资源；旧点/城市/地牢没有；读档、取消不重播；旧档已过 75% 不补。</summary>
    [Category("DesignSync")]
    [Test]
    public void MAP_Q117_01_PerNodeClue_OnceOnly_NoExtras()
    {
        var clues = ((JArray)Adj()["clues"]!).ToDictionary(c => (string)c["nodeId"]!, c => (string)c["text"]!);
        Assert.That(clues, Has.Count.EqualTo(45));
        Assert.That(Cfg.Locations.Where(l => l.Clue != null).ToDictionary(l => l.Id, l => l.Clue!), Is.EquivalentTo(clues));
        Assert.That(Cfg.Locations.Where(l => l.Clue != null).All(l => !l.IsStarter && !l.IsDungeon && !l.ExploreAuto100OnVisit && l.ExploreEnabled), Is.True);

        var s = New();
        E.DebugTeleport(s, "w11_orchard");
        Explore(s);
        Explore(s);
        var res = s.Resources.Count(kv => kv.Value.Discovered);
        var edges = s.UnlockedEdges.Count;
        var t75 = Explore(s);
        Assert.That(t75, Does.Contain("发现一条安全线索：" + clues["w11_orchard"].TrimEnd('。')));
        Assert.That(s.Resources.Count(kv => kv.Value.Discovered), Is.EqualTo(res), "不揭资源");
        Assert.That(s.UnlockedEdges.Count, Is.EqualTo(edges), "不揭路");
        int Count(GameState x) => x.Log.Count(l => l.Text.Contains(clues["w11_orchard"].TrimEnd('。')));
        var m = Reload(s);
        Assert.That(Count(m), Is.EqualTo(1), "读档不重播");
        Start(m, ActionRequest.Explore());
        E.Cancel(m);
        Assert.That(Count(m), Is.EqualTo(1), "取消不重播");
        Explore(m);
        Assert.That(Count(m), Is.EqualTo(1), "100% 不再记线索");

        // 旧档已过 75%：视为已过，不补
        var o = New();
        E.DebugTeleport(o, "m01_road");
        E.DebugSetProgress(o, "m01_road", 75);
        Explore(o);
        Assert.That(o.Log.Any(l => l.Text.Contains(clues["m01_road"].TrimEnd('。'))), Is.False);

        // 地牢房间 / 公开城市 / 旧 8 地点没有线索
        Assert.That(Cfg.Locations.Where(l => l.IsDungeon || l.ExploreAuto100OnVisit || l.IsStarter).All(l => l.Clue == null), Is.True);
    }

    // ───────────── R127 / Q118 ─────────────

    /// <summary>MAP-Q118-01：135 新节点 76 室内 / 59 露天，与策划表逐点一致；旧 8 点原标签不变（五金店、诊所室内，其余露天）。</summary>
    [Category("DesignSync")]
    [Test]
    public void MAP_Q118_01_ExposureTable_Matches()
    {
        var table = ((JArray)Adj()["exposure"]!).ToDictionary(x => (string)x["nodeId"]!, x => (string)x["exposure"]!);
        Assert.That(table, Has.Count.EqualTo(135));
        foreach (var (id, ex) in table) Assert.That(E.Location(id).Exposure, Is.EqualTo(ex), id);
        Assert.That(table.Values.Count(v => v == "indoor"), Is.EqualTo(76));
        Assert.That(Starter.Where(id => E.Location(id).IsIndoor), Is.EquivalentTo(new[] { "hardware", "clinic" }));
        Assert.That(E.Location("h02_square").IsIndoor, Is.False, "营区广场仍露天");
        Assert.That(E.Location("h06_shelter").IsIndoor, Is.True);
    }

    /// <summary>
    /// MAP-Q118-02：天然室内只给 S13 基础遮蔽——遮蔽睡眠速率与遮蔽休息的健康自然恢复；不给棚休息 M+2、床垫 E+1；露天节点不遮蔽；
    /// 换到新标签后按当前位置每段实时判定（不回溯旧恢复）。
    /// </summary>
    [Test]
    public void MAP_Q118_02_NaturalIndoor_BasicShelterOnly()
    {
        var s = New();
        E.DebugTeleport(s, "w13_mill");   // 天然室内野外
        Assert.That(E.IsSheltered(s, "w13_mill"), Is.True);
        var sleep = E.ComputeSegment(s, new ActionState { Type = ActionType.Sleep, TargetId = "" });
        Assert.That(sleep.Sheltered, Is.True);
        Assert.That(sleep.Rates.Energy, Is.EqualTo(Cfg.HourlyRates["sleep_sheltered"].Energy).Within(1e-9), "无床垫 E+1");
        var rest = E.ComputeSegment(s, new ActionState { Type = ActionType.Rest, TargetId = "" });
        Assert.That(rest.Rates.Morale, Is.EqualTo(Cfg.HourlyRates["rest"].Morale).Within(1e-9), "没有棚休息 M+2");
        Assert.That(rest.HealthPerHour, Is.EqualTo(Cfg.HealthRules.ShelteredRestRecoveryPerHour).Within(1e-9), "遮蔽休息按 S13 自然恢复（条件满足时）");

        E.DebugTeleport(s, "w11_orchard");   // 露天
        Assert.That(E.IsSheltered(s, "w11_orchard"), Is.False);
        Assert.That(E.ComputeSegment(s, new ActionState { Type = ActionType.Rest, TargetId = "" }).HealthPerHour, Is.EqualTo(0).Within(1e-9));

        // 营地已建棚的休息 M+2 只认营地棚
        E.DebugTeleport(s, "camp");
        s.Locations["camp"].Facility.ShelterBuilt = true;
        var campRest = E.ComputeSegment(s, new ActionState { Type = ActionType.Rest, TargetId = "" });
        Assert.That(campRest.Rates.Morale, Is.EqualTo(Cfg.HourlyRates["rest"].Morale + Cfg.FacilityEffects.ShelteredCampRestMoralePerHour).Within(1e-9));
        Assert.That(E.Location("w13_mill").CanBuildShelter, Is.False, "室内不是建设许可");
    }

    /// <summary>
    /// HOUSE-Q118-01：地牢 67 点及势力/公共/任务保留地都不许私人住宅（与策划表 safehouseAllowed 一致，禁建原因留给 B17 住宅入口）；
    /// UI-Q134-01：现有营地建造入口（棚/床垫/工坊）在这些地点与普通野外预览即拒绝，原因只写“只能在临时营地建造”、不挂“不可建设私人住宅”，不扣材料、不花时间。
    /// </summary>
    [Category("DesignSync")]
    [Test]
    public void HOUSE_Q118_01_ForbiddenLand_BuildRefusedWithReason()
    {
        foreach (var x in (JArray)Adj()["exposure"]!)
        {
            var l = E.Location((string)x["nodeId"]!);
            Assert.That(l.BuildAllowedSafehouse, Is.EqualTo((bool)x["safehouseAllowed"]!), l.Id);
            if (l.IsDungeon || l.LandOwner is "faction" or "public" or "quest_reserved")
            {
                Assert.That(l.BuildAllowedSafehouse, Is.False, l.Id);
                Assert.That(l.PrivateBuildForbidden, Is.Not.Null, l.Id);
            }
            Assert.That(l.CanBuildShelter, Is.False, $"{l.Id}：新节点没有营地建造入口");
        }
        Assert.That(Cfg.Locations.Count(l => l.IsDungeon), Is.EqualTo(67));

        var s = New();
        foreach (var (loc, why) in new[] { ("d05_01", "地牢内不可建设私人住宅"), ("c02_church", "势力/公共土地不可建设私人住宅"), ("h02_square", "势力/公共土地不可建设私人住宅") })
        {
            E.DebugTeleport(s, loc);
            var minute = s.GameMinute;
            var items = s.Items.Count;
            foreach (var rid in new[] { "shelter", "bed", "build_workbench" })
            {
                var r = E.Start(s, ActionRequest.Craft(rid));
                Assert.That(r.Ok, Is.False, $"{loc}/{rid}");
                Assert.That(r.Reason, Is.EqualTo("只能在临时营地建造"), $"{loc}/{rid}");
                Assert.That(r.Reason, Does.Not.Contain("私人住宅"), $"{loc}/{rid}");
                Assert.That(Cfg.Locations.First(l => l.Id == loc).PrivateBuildForbidden, Is.EqualTo(why), "住宅禁建原因仍在，留给 B17 住宅入口");
            }
            Assert.That((s.GameMinute, s.Items.Count, s.Main), Is.EqualTo((minute, items, (ActionState?)null)), "不扣费、不花时间");
        }
        E.DebugTeleport(s, "w13_mill");
        var ok = E.Start(s, ActionRequest.Craft("shelter"));
        Assert.That(ok.Reason, Is.EqualTo("只能在临时营地建造"), "允许地块也没有住宅入口（B17）");
    }

    /// <summary>HOUSE-Q118-02（本批可判定部分）：给新节点加室内标签不会产生住宅/棚/床/柜；营地棚不会自动升级或赠屋。住宅本身在 B17。</summary>
    [Test]
    public void HOUSE_Q118_02_IndoorTagIsNotHousing()
    {
        var s = New();
        foreach (var l in Cfg.Locations.Where(l => l.IsIndoor && !l.IsStarter))
        {
            var f = s.Locations[l.Id].Facility;
            Assert.That((f.ShelterBuilt, f.BedBuilt, l.CanBuildShelter, l.HasFire), Is.EqualTo((false, false, false, false)), l.Id);
        }
        Assert.That(Cfg.Recipes.Any(r => r.Id.Contains("safehouse") || r.Name.Contains("住宅") || r.Name.Contains("安全屋")), Is.False, "本批没有住宅配方（B17）");
    }

    // ───────────── R128 / Q119 ─────────────

    private static void Prep(GameState s, int c = 4)
    {
        if (c >= 2) s.Progress.Flags.Add("hub_registered");
        if (c >= 3) s.Progress.Flags.Add("church_route_record");
        if (c >= 4) s.Progress.Flags.Add("academy_safety_record");
        s.SpawnNextCheck = int.MaxValue;
        s.PatrolNextCheck = int.MaxValue;
        s.GameMinute = At(1, 10, 0);
    }

    /// <summary>
    /// PARTY-Q119-01：游戏数据启用正式地址（servicesAtV3Nodes=true）；新局直接 h02/h06；camp 没有新请求代办、原因写去哪办；
    /// 未登记不会因迁址开路线、不传送；当前同行号位编辑任意空闲地点照常。
    /// </summary>
    [Category("DesignSync")]
    [Test]
    public void PARTY_Q119_01_ServicesAtV3Now_CampHintOnly_NoFreeRoute()
    {
        var sm = (JObject)Adj()["serviceMigration"]!;
        Assert.That(Cfg.Map.ServicesAtV3Nodes, Is.EqualTo((bool)sm["servicesAtV3Nodes"]!));
        Assert.That((Cfg.Party.HubV3, Cfg.Party.ShelterV3), Is.EqualTo(((string)sm["hub"]!, (string)sm["shelter"]!)));

        var s = New();
        Assert.That(s.ServiceMapMigrated, Is.True);
        Assert.That((E.HubLocation(s), E.ShelterLocation(s)), Is.EqualTo(("h02_square", "h06_shelter")));
        Assert.That(s.CurrentLocation, Is.EqualTo("camp"));
        Assert.That(E.EscortRegistrationBlocked(s), Is.EqualTo("临时营地已不再代办；要到收容所安全空闲时登记补员"));
        E.DebugRecruit(s, "npc_lin_zhao");
        Assert.That(E.SetActiveBlocked(s, "npc_lin_zhao", false), Is.EqualTo("临时营地已不再代办；要到营区广场（营区）安全空闲时留作候补"));
        Assert.That(E.FormationEditBlocked(s), Is.Null, "号位编辑仍任意空闲地点（Q86）");
        Assert.That(s.UnlockedEdges.Any(id => E.Edge(id).Touches("h02_square") || E.Edge(id).Touches("h06_shelter")), Is.False, "不免费开营区路线");
        Assert.That(s.Locations["h02_square"].Visited || s.Locations["h06_shelter"].Visited, Is.False);
        E.DebugTeleport(s, "field");
        Assert.That(E.EscortRegistrationBlocked(s), Does.StartWith("只能在收容所安全空闲时登记补员"), "别处照常写正式地址");
    }

    /// <summary>
    /// PARTY-Q119-02：迁址前的旧档读入后只迁一次——明确公共候补遗物迁 h02（实例/耐久不变），来源不明（null）与同行者 camp 遗物、camp 地面物不迁；
    /// 在办 camp RB01 按原快照在 camp 收尾、只建一人，之后新请求只在 h06。
    /// </summary>
    [Test]
    public void PARTY_Q119_02_OldSave_MigratesOnce_Rb01Snapshot()
    {
        var s = New();
        s.ServiceMapMigrated = false;   // 迁址前旧档（B11 口径：服务在 camp）
        Prep(s, 2);
        E.DebugRecruit(s, "npc_gu_cheng");
        E.SetActive(s, "npc_gu_cheng", false);
        E.DebugActor(s, "npc_gu_cheng", "kill");
        E.DebugRecruit(s, "npc_lin_zhao");
        E.DebugActor(s, "npc_lin_zhao", "kill");
        s.Remains["npc_lin_zhao"].PublicReserve = null;   // 旧档分不清来源
        var guItems = E.RemainsItems(s, "npc_gu_cheng").Select(i => (i.InstanceId, i.Durability)).OrderBy(x => x).ToList();
        E.CreateItems(s, "wood", 1, "camp");
        int CampWood(GameState x) => x.Items.Where(i => i.Owner == "camp" && i.ItemId == "wood").Sum(i => i.Count);
        var wood = CampWood(s);
        Assert.That(E.StartEscortRegistration(s, "rb-old").Ok, Is.True, "迁址前在 camp 开始的 RB01");

        var m = Reload(s);
        Assert.That(E.MigrateServiceLocations(m), Is.True);
        Assert.That(E.MigrateServiceLocations(m), Is.False, "只迁一次");
        Assert.That(m.Remains["npc_gu_cheng"].LocationId, Is.EqualTo("h02_square"));
        Assert.That(E.RemainsItems(m, "npc_gu_cheng").Select(i => (i.InstanceId, i.Durability)).OrderBy(x => x), Is.EqualTo(guItems));
        Assert.That(m.Remains["npc_lin_zhao"].LocationId, Is.EqualTo("camp"), "来源不明不迁、原地可取");
        Assert.That(CampWood(m), Is.EqualTo(wood), "camp 普通地面物不迁");
        for (var i = 0; i < 200 && m.Main != null; i++) E.Advance(m);
        Assert.That(E.AliveEscorts(m), Has.Count.EqualTo(1), "按原快照收尾，只建一人");
        Assert.That(E.StartEscortRegistration(m, "rb-new").Ok, Is.False);
        Assert.That(E.EscortRegistrationBlocked(m), Does.Contain("收容所"), "新请求只在 h06");
        Assert.That(m.UnlockedEdges.Count, Is.EqualTo(Reload(s).UnlockedEdges.Count), "迁址不开路线");
    }

    /// <summary>PARTY-Q119-03：候补在 h02 按 Q90 持续自主恢复（读 h02 实际设施：露天无棚床，camp 的棚床不远程生效）、不瞬回；h06 报失新矛只落 h06 地面。</summary>
    [Test]
    public void PARTY_Q119_03_ReserveCareAtNewSite_LoanReplacementAtShelter()
    {
        var s = New();
        Prep(s, 2);
        s.Locations["camp"].Facility.ShelterBuilt = true;
        s.Locations["camp"].Facility.BedBuilt = true;
        E.DebugTeleport(s, "h02_square");
        E.DebugRecruit(s, "npc_lin_zhao");
        Assert.That(E.SetActive(s, "npc_lin_zhao", false).Ok, Is.True);
        Assert.That(E.ReserveFacility(s), Is.EqualTo("none"), "h02 广场露天，camp 的棚床不远程生效");
        var a = s.Actors["npc_lin_zhao"];
        a.Stats.Satiety = 50;
        Assert.That(a.Stats.Satiety, Is.EqualTo(50), "切换本身不瞬回");
        E.DebugTeleport(s, "field");
        Start(s, ActionRequest.Rest(60));
        Run(s);
        Assert.That(a.Stats.Satiety, Is.GreaterThan(50).And.LessThanOrEqualTo(Cfg.Party.ReserveCare.PublicNutritionTarget), "玩家不在 h02 也持续公共补给");

        E.DebugTeleport(s, "h06_shelter");
        E.DebugAddEscort(s);
        var esc = E.AliveEscorts(s).Single();
        s.Items.Single(i => i.LoanSlot == "loan_1").Durability = 0;
        E.DebugActor(s, esc.ActorId, "kill");
        Assert.That(E.ReportLostLoan(s, "loan_1", "lost-q119").Ok, Is.True);
        Assert.That(s.Items.Single(i => i.LoanSlot == "loan_1").Owner, Is.EqualTo("h06_shelter"));
    }
}
