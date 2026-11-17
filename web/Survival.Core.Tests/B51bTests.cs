using Newtonsoft.Json.Linq;
using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 批次 B51b（需求 R252、R253、R258 的 B51b 部分；S08 文首 Q701.2、B51 Q721／Q725／Q727／Q728，R09 Q702／Q703）：
/// 陶窑与固定净水台全部并行的原料保质、取消本批恢复句、存档 54→55 迁移、未预估字段与预估提醒。
/// </summary>
public class B51bTests : TestBase
{
    private const string Water = "ex_station_water";
    private const string Kiln = "ex_ceramic_shell";

    protected override bool NoEnemyApproach => true;

    private void Ok(ActionResult r) => Assert.That(r.Ok, Is.True, r.Reason);

    private static ActionRequest Craft(string id, int n = 1) => ActionRequest.Craft(id, null, null, n);

    private static int Sum(GameState s, string itemId) => s.Items.Where(i => i.ItemId == itemId).Sum(i => i.Count);

    private List<StationBatch> Batches(GameState s, string station) =>
        E.FacOf(s, s.Ground)!.StationBatches.Where(b => b.StationId == station).ToList();

    private GameState Workshop()
    {
        var s = New();
        s.Skills["crafting"].Level = 3;
        s.Progress.Flags.Add("h03_ceramic_notice_read");
        foreach (var (id, n) in new[] { ("stone", 8), ("clay", 6), ("iron_plate", 2) }) E.CreateItems(s, id, n, s.Ground);
        Start(s, Craft("build_kiln"));
        Run(s);
        foreach (var (id, n) in new[] { ("ceramic_shell", 4), ("plank", 2), ("cloth", 2) }) E.CreateItems(s, id, n, s.Ground);
        Start(s, Craft("build_water_station"));
        Run(s);
        s.Items.RemoveAll(i => i.ItemId is "clay" or "charcoal" or "dirty_water" or "filter_media" or "ceramic_shell" or "clean_water");
        s.Skills["crafting"].Level = 3;
        s.Skills["crafting"].Xp = 0;
        return s;
    }

    /// <summary>给当前地面放 batches 批净水原料；脏水带一个有限截止（现有配置的设施原料都无期限，用测试注入验证通用保质路径）。</summary>
    private void WaterInputs(GameState s, int batches, int? expiresIn = null)
    {
        E.CreateItems(s, "dirty_water", 4 * batches, s.Ground);
        E.CreateItems(s, "filter_media", batches, s.Ground);
        if (expiresIn is int m)
            foreach (var i in s.Items.Where(i => i.ItemId == "dirty_water")) i.ExpiresAt = s.GameMinute + m;
    }

    // ───────────── Q701.1：设施批次输入暂停保质 ─────────────

    /// <summary>Q701-A02（设施批）：投料即对本批有期限输入记 R＝E−S，在制越过原截止不腐败；取消在 C 恢复 C＋R 并接恢复句；未投入余料照常计时。</summary>
    [Test]
    public void Q701_StationInputs_PauseShelf_CancelRestores()
    {
        var s = Workshop();
        WaterInputs(s, 1, 20);
        E.CreateItems(s, "dirty_water", 2, s.Ground);
        foreach (var i in s.Items.Where(i => i.ItemId == "dirty_water")) i.ExpiresAt = s.GameMinute + 20;
        var t0 = s.GameMinute;
        Ok(E.StartStationBatch(s, Water, "p1"));
        var bound = s.Items.Where(i => InAnyBatch(i) && i.ItemId == "dirty_water").ToList();   // B51c2a1 改期望（S11 Q872 选 B：原料移进设施该批，无地面预留标记）：原按 ReservedBy 认批
        Assert.That(bound.Sum(i => i.Count), Is.EqualTo(4));
        Assert.That(bound.All(i => i.ShelfPausedMinutes == 20), Is.True, "本批输入 R＝E−S");
        var free = s.Items.Single(i => i.ItemId == "dirty_water" && !InAnyBatch(i));   // B51c2a1 改期望（S11 Q872 选 B：原料移进设施该批，无地面预留标记）：原按 ReservedBy 认批
        Assert.That(free.ShelfPausedMinutes, Is.Null, "未投入余料不保护");
        AdvanceTo(s, t0 + 25);
        Assert.That(s.Items.Where(i => InAnyBatch(i) && i.ItemId == "dirty_water").Sum(i => i.Count), Is.EqualTo(4), "在制越过原截止不转腐败物");   // B51c2a1 改期望（S11 Q872 选 B：原料移进设施该批，无地面预留标记）：原按 ReservedBy 认批
        Assert.That(s.Items.Any(i => i.ItemId == "dirty_water" && !InAnyBatch(i)), Is.False, "余料照常到期");
        var b = Batches(s, "water_station").Single();
        Ok(E.CancelStationBatch(s, "water_station", b.BatchId, "p1c", 1));
        var back = s.Items.Where(i => i.ItemId == "dirty_water").ToList();
        Assert.That(back.Sum(i => i.Count), Is.EqualTo(4));
        Assert.That(back.All(i => i.ShelfPausedMinutes == null && i.ExpiresAt == s.GameMinute + 20 && i.ReservedBy == null && i.Owner == s.Ground), Is.True, "恢复 C＋R，回到本区地面");
        var zone = E.ZoneName(s, s.Ground);
        Assert.That(s.Log.Last(l => l.Kind == nameof(GameEventKind.ActionCancelled)).Text,
            // BM11 改断言出处：S08 文首 2026-11-17 §Q882 用户新裁定（返料不附恢复保质说明）
            Is.EqualTo($"已取消{zone}固定净水台的批次1 · {Cfg.Recipes.Single(r => r.Id == Water).Name}（已进行25分钟）。未消耗原料已退回{zone}地面。" +
                       "没有产物和经验。已耗时间保留。"));
    }

    /// <summary>Q701-A03：受保护输入超过原截止仍按时完成核销；完成产物正常出、无残留 R。</summary>
    [Test]
    public void Q701_StationInputs_ProtectedStillComplete()
    {
        var s = Workshop();
        WaterInputs(s, 2, 10);
        var t0 = s.GameMinute;
        Ok(E.StartStationBatch(s, Water, "p2", 2));
        AdvanceTo(s, t0 + 30);
        Assert.That(Sum(s, "clean_water"), Is.EqualTo(8));
        Assert.That(Sum(s, "dirty_water") + Sum(s, GameEngine.SpoiledMatterId), Is.EqualTo(0), "受保护原料直接核销，不转腐败物");
    }

    /// <summary>Q727：取消无实际可返料的批用无返料版全文。</summary>
    [Test]
    public void Q727_CancelNoReturn_Text()
    {
        var s = Workshop();
        WaterInputs(s, 1);
        Ok(E.StartStationBatch(s, Water, "nr"));
        var b = Batches(s, "water_station").Single();
        var gone = BatchBound(s, b.BatchId);
        s.Items.RemoveAll(gone.Contains);   // B51c2a1 改期望（S11 Q872 选 B：原料移进设施该批，无地面预留标记）：原按 ReservedBy 认批
        Ok(E.CancelStationBatch(s, "water_station", b.BatchId, "nrc"));
        var zone = E.ZoneName(s, s.Ground);
        Assert.That(s.Log.Last(l => l.Kind == nameof(GameEventKind.ActionCancelled)).Text,
            Is.EqualTo($"已取消{zone}固定净水台的批次1 · {Cfg.Recipes.Single(r => r.Id == Water).Name}（已进行0分钟）。没有可返还的剩余原料，没有产物和经验。已耗时间保留。"));
    }

    /// <summary>Q727-A01：工坊汇总取全部在办批最早完成，无批空闲；工坊卡用同一汇总、不再按单批写「加工中：陶窑25/60分」。</summary>
    [Test]
    public void Q727_WorkshopSummary()
    {
        var s = Workshop();
        Assert.That(E.StationSummary(s, s.Ground, "water_station"), Is.EqualTo($"{E.StationName("water_station")} · 空闲"));
        WaterInputs(s, 3);
        var t0 = s.GameMinute;
        Ok(E.StartStationBatch(s, Water, "w1"));
        AdvanceTo(s, t0 + 10);
        Ok(E.StartStationBatch(s, Water, "w2", 2));
        Assert.That(E.StationSummary(s, s.Ground, "water_station"),
            Is.EqualTo($"{E.StationName("water_station")} · 加工中3批 · 最近{Clock.DayTime(t0 + 30)}完成"));
        var q = Batches(s, "water_station");
        Assert.That(E.StationBatchRow(s, q[1], 2), Is.EqualTo($"批次2 · {Cfg.Recipes.Single(r => r.Id == Water).Name} · 0/30分 · 预计{Clock.DayTime(t0 + 40)}完成"));
        Assert.That(E.StationOutputGround(s, s.Ground), Is.EqualTo($"完成后放在{E.ZoneName(s, s.Ground)}地面。"));
        // B35a1（Q795 B）：工坊汇总卡拆成每座一张，陶窑／固定净水台的独立卡状态行沿用同一汇总（原断言为汇总卡源码含 StationSummary）
        var cards = E.FacilityCardsHere(s).Where(c => c.StationId == "water_station").ToList();
        Assert.That(cards, Has.Count.EqualTo(1));
        Assert.That(cards[0].State, Is.EqualTo($"{E.StationName("water_station")} · 加工中3批 · 最近{Clock.DayTime(t0 + 30)}完成"));
        Assert.That(cards[0].State, Does.Not.Contain("加工中："));
    }

    // ───────────── 存档 54→55（Q701-A09） ─────────────

    /// <summary>
    /// Q701-A09：旧档在办批保留已耗进度，旧等候批在升级 U 转为在办、从未来第一个世界段起推进（不补等候进度）；批次 ID／经验快照／预留不变；
    /// 各批未到期有期限输入从 U 记 R＝旧E−U，U 时已到期的不保护；重复读档不再改进度或 R。
    /// </summary>
    [Test]
    public void Migrate54To55_QueuedBecomeParallel_InputsProtected_Idempotent()
    {
        var s = Workshop();
        WaterInputs(s, 3, 100);
        var t0 = s.GameMinute;
        Ok(E.StartStationBatch(s, Water, "m", 3));
        AdvanceTo(s, t0 + 20);
        // 造 54 旧档：首批加工中 20 分钟，其余两批排队（进度 0），输入当时没有暂停保质；第三批的一份脏水在 U 前已到期
        var q = Batches(s, "water_station");
        q[1].Queued = true; q[1].DoneMinutes = 0;
        q[2].Queued = true; q[2].DoneMinutes = 0;
        E.DebugLegacyStation56(s);   // B51c2a1：造旧档起点——57 前原料在地面按批预留
        foreach (var i in s.Items.Where(i => i.ReservedBy != null)) i.ShelfPausedMinutes = null;   // 原截止 E 一直保留（t0+100）
        var late = s.Items.First(i => i.ReservedBy == q[2].BatchId && i.ItemId == "dirty_water");
        late.ExpiresAt = s.GameMinute;
        var ids = q.Select(b => (b.BatchId, b.XpSnapshot)).ToList();
        var obj = JObject.Parse(SaveSerializer.Serialize(s));
        obj["SchemaVersion"] = "54";
        var old = SaveSerializer.Deserialize(obj.ToString(), Cfg);
        Assert.That(old.SchemaVersion, Is.EqualTo("57"));   // B51c2a1 改期望：存档升 57（56→57 陶窑／净水台原料移进设施，S11 Q872 选 B）
        var u = old.GameMinute;
        var nq = Batches(old, "water_station");
        Assert.That(nq.Select(b => (b.BatchId, b.XpSnapshot)), Is.EqualTo(ids), "批次 ID 与经验快照不变");
        Assert.That(nq.All(b => !b.Queued), Is.True, "没有等候批");
        Assert.That(nq.Select(b => b.DoneMinutes), Is.EqualTo(new[] { 20, 0, 0 }), "在办批保留进度、旧等候批不补进度");
        Assert.That((nq[1].StartMinute, nq[2].StartMinute), Is.EqualTo((u, u)));
        var bound = BatchBound(old, nq[0].BatchId).Where(i => i.ItemId == "dirty_water").ToList();   // B51c2a1 改期望（S11 Q872 选 B：原料移进设施该批，无地面预留标记）：原按 ReservedBy 认批
        Assert.That(bound.Sum(i => i.Count), Is.EqualTo(4));
        Assert.That(bound.All(i => i.ShelfPausedMinutes == 80 && i.Owner == ItemInstance.StationOwner(old.Ground, nq[0].BatchId)), Is.True, "R＝旧E−U，不补偿已耗；56→57 移进设施");
        Assert.That(BatchBound(old, nq[2].BatchId).Where(i => i.ExpiresAt == u).All(i => i.ShelfPausedMinutes == null), Is.True, "U 时已到期的不保护");

        var again = SaveSerializer.Deserialize(SaveSerializer.Serialize(old), Cfg);
        Assert.That(Batches(again, "water_station").Select(b => (b.BatchId, b.DoneMinutes, b.StartMinute)), Is.EqualTo(Batches(old, "water_station").Select(b => (b.BatchId, b.DoneMinutes, b.StartMinute))), "重复读档不变");
        Assert.That(again.Items.Where(i => i.ShelfPausedMinutes != null).Select(i => (i.InstanceId, i.ShelfPausedMinutes)),
            Is.EqualTo(old.Items.Where(i => i.ShelfPausedMinutes != null).Select(i => (i.InstanceId, i.ShelfPausedMinutes))), "不重算 R");

        AdvanceTo(again, u + 10);
        Assert.That(Sum(again, "clean_water"), Is.EqualTo(4), "在办批续原进度完成");
        AdvanceTo(again, u + 30);
        Assert.That(Sum(again, "clean_water"), Is.EqualTo(8), "旧等候批从升级起各自 30 分钟完成；含升级前已到期输入的那批按原路径失败");
        Assert.That(again.Log.Any(l => l.Text.Contains("原料不全")), Is.True);
    }
    /// <summary>开发入口（?dev=1）：在办批改成旧档排队、版本记 54；保存后重新读档按 54→55 迁移为并行。</summary>
    [Test]
    public void Dev_LegacyStationQueue_RoundTripMigrates()
    {
        var s = Workshop();
        Assert.That(E.DebugLegacyStationQueue(s), Does.Contain("没有在办批次"));
        WaterInputs(s, 2);
        Ok(E.StartStationBatch(s, Water, "m", 2));
        AdvanceTo(s, s.GameMinute + 10);
        Assert.That(E.DebugLegacyStationQueue(s), Does.Contain("1批排队"));
        Assert.That(s.SchemaVersion, Is.EqualTo("54"));
        Assert.That(Batches(s, "water_station").Select(b => (b.Queued, b.DoneMinutes)), Is.EqualTo(new[] { (false, 10), (true, 0) }));
        var back = SaveSerializer.Deserialize(SaveSerializer.Serialize(s), Cfg);
        Assert.That(back.SchemaVersion, Is.EqualTo("57"));   // B51c2a1 改期望：存档升 57（56→57 陶窑／净水台原料移进设施，S11 Q872 选 B）
        Assert.That(Batches(back, "water_station").Select(b => (b.Queued, b.DoneMinutes, b.StartMinute)),
            Is.EqualTo(new[] { (false, 10, Batches(s, "water_station")[0].StartMinute), (false, 0, back.GameMinute) }));
    }
    // ───────────── Q725：依赖完成的终值“未预估”、配方产物 ─────────────

    private static string Root()
    {
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "doc", "策划案"))) dir = dir.Parent;
        return dir!.FullName;
    }

    private static string Web(string relative) => File.ReadAllText(Path.Combine(Root(), "web", "Survival.Web", relative));

    /// <summary>Q725-A01：求不出可信完成时工具完成后耐久、完成后经验写“未预估”；固定配方输出行“配方产物 · 留在{地点}”，采集不在这里换（Q776）。</summary>
    [Test]
    public void Q725_A01_UnreliableFinalValues()
    {
        var p = new ActionPreview { Ok = true, PlanFailed = true, OutputPlace = "安全屋地面", ToolDurabilityBefore = 5, ToolDurabilityAfter = 4 };
        p.XpGains.Add("制造经验+6");
        Assert.That(GameEngine.ToolAfterText(p), Is.EqualTo("未预估"));
        Assert.That(GameEngine.XpAfterText(p), Is.EqualTo("未预估"));
        foreach (var t in new[] { ActionType.Craft, ActionType.Build, ActionType.Butcher, ActionType.Salvage })
            Assert.That(GameEngine.OutputStatusText(p, t), Is.EqualTo("配方产物 · 留在安全屋地面"), t.ToString());
        // B51c1 改期望（出处 S08 B51 §Q776 选 A）：采集完成不可靠时状态「可能产物」
        Assert.That(GameEngine.OutputStatusText(p, ActionType.Gather), Is.EqualTo("可能产物"));
        p.PlanFailed = false;
        Assert.That((GameEngine.ToolAfterText(p), GameEngine.XpAfterText(p), GameEngine.OutputStatusText(p, ActionType.Craft)),
            Is.EqualTo((Fmt.N(4), "制造经验+6", "产出 · 留在安全屋地面")), "可靠完整完成时原终值照旧");
    }

    /// <summary>Q725-A02：可靠预估不受影响（真实预估仍给经验与产出）；界面接线：制作卡静态标题“配方产物”，耗时／完成／工具／经验走未预估。</summary>
    [Test]
    public void Q725_A02_ReliableUnchanged_WebWiring()
    {
        var s = New();
        E.DebugGive(s, "cloth", 1);
        E.DebugGive(s, "herb", 1);
        var p = E.Preview(s, ActionRequest.Craft("bandage"));
        Assert.That((p.Ok, p.PlanFailed), Is.EqualTo((true, false)), p.Reason);
        Assert.That(GameEngine.XpAfterText(p), Is.EqualTo(string.Join("、", p.XpGains)).And.Not.EqualTo("未预估"));
        Assert.That(GameEngine.OutputStatusText(p, ActionType.Craft), Does.StartWith("产出 · 留在"));

        var craft = Web("Components/CraftOverlay.razor");
        Assert.That(craft, Does.Contain("style=\"margin-top:8px\">@GameEngine.RecipeOutputLabel</div>"));
        Assert.That(craft, Does.Not.Contain("style=\"margin-top:8px\">产物</div>"));
        var pv = Web("Components/PreviewView.razor");
        Assert.That(pv, Does.Contain("Status=\"@GameEngine.OutputStatusText(p, Request.Type)\""));
        Assert.That(pv, Does.Contain("@GameEngine.XpAfterText(p)"));
        // B51c1 改期望（出处 S08 B51 §Q775、02 Q873 A 暂用）：各「未预估」改走 UnknownText（带同源实际原因）
        Assert.That(pv, Does.Contain("p.PlanFailed ? GameEngine.UnknownText(p) : $\"{Ui.N(p.ToolDurabilityAfter ?? 0)}"));
        var pops = Web("Components/ActionPops.razor");
        Assert.That(pops, Does.Contain("@GameEngine.ToolAfterText(p)"));
        Assert.That(pops, Does.Contain("p.PlanFailed ? GameEngine.UnknownText(p) : Clock.HHmm(p.EstimatedEndMinute) + \"到达\""));
        Assert.That(pops, Does.Contain("p.PlanFailed ? GameEngine.UnknownText(p) : Clock.HHmm(p.EstimatedEndMinute) + \"完成\""));
        Assert.That(pops, Does.Contain("p.Ok && !p.PlanFailed && !e.IsDaylight(p.EstimatedEndMinute)"));
    }
    // ───────────── Q702／Q703／Q728：各人风险时间、阈值提醒本人身份与真实未来跨线 ─────────────

    private static void Six(ActorState a, double n, double w, double e, double sl, double m, double h)
    {
        a.Stats.Set(StatId.Satiety, n);
        a.Stats.Set(StatId.Hydration, w);
        a.Stats.Set(StatId.Energy, e);
        a.Stats.Set(StatId.Wakefulness, sl);
        a.Stats.Set(StatId.Morale, m);
        a.Stats.Set(StatId.Health, h);
    }

    /// <summary>荒地探索（白天）两人队：主角与同伴六项可设。</summary>
    private (GameState s, ActorState me, ActorState mate) FieldPair()
    {
        var s = New();
        E.DebugTeleport(s, "field");
        s.GameMinute = 480;
        E.DebugQuickParty(s, 2);
        var me = s.Actors[GameState.PlayerId];
        var mate = s.Actors[s.Party.Single(id => id != GameState.PlayerId)];
        foreach (var a in new[] { me, mate }) { a.AlertLatched.Clear(); a.ZeroLatched.Clear(); }
        return (s, me, mate);
    }

    /// <summary>Q702-A01：“各人风险”前缀“约{时长}后：”，时长用原游戏时长格式。</summary>
    [Test]
    public void Q702_LaterRiskPrefix_Format()
    {
        Assert.That(GameEngine.LaterRiskPrefix(30), Is.EqualTo("约30分钟后："));
        Assert.That(GameEngine.LaterRiskPrefix(65), Is.EqualTo("约1小时5分后："));
    }

    /// <summary>
    /// Q703-A01／Q728-A01：同一边界两人同属性、一人另一属性真实跨线，三条各带本人称呼、同属性两人不合并、按原触发顺序；
    /// 预估只读；真实推进只暂停一次。
    /// </summary>
    [Test]
    public void Q728_SameBoundary_AllTriggers_WithNames_PauseOnce()
    {
        var (s, me, mate) = FieldPair();
        Six(me, 20.01, 90, 90, 90, 60, 100);
        Six(mate, 20.01, 20.01, 90, 90, 60, 100);
        var name = E.ActorDisplayName(s, mate.Id);
        var before = SaveSerializer.Serialize(s);
        var p = E.Preview(s, ActionRequest.Explore());
        Assert.That(p.Ok, Is.True, p.Reason);
        Assert.That(SaveSerializer.Serialize(s), Is.EqualTo(before), "预估只读");
        Assert.That(p.BodyAlerts, Is.EqualTo(new[]
        {
            "你：途中饱食将越过提醒阈值，行动会暂停",
            $"{name}：途中饱食将越过提醒阈值，行动会暂停",
            $"{name}：途中水分将越过提醒阈值，行动会暂停",
        }));
        Assert.That(p.AlertsExpected, Is.EqualTo(new[] { StatId.Satiety, StatId.Hydration }));
        Assert.That(p.Warnings, Does.Contain($"{name}：途中水分将越过提醒阈值，行动会暂停"), "紧凑摊平同源");

        Start(s, ActionRequest.Explore());
        Run(s, autoResume: false);
        Assert.That(s.Main, Is.Not.Null);
        Assert.That(s.Main!.Status, Is.EqualTo(ActionStatus.Paused));
        Assert.That(s.Log.Count(l => l.Text.Contains("已暂停")), Is.EqualTo(1), "真实只暂停一次");
    }

    /// <summary>Q703（单人）：只有一名相关人物时仍原句，不带人物前缀。</summary>
    [Test]
    public void Q703_SinglePerson_KeepsOriginalSentence()
    {
        var s = New();
        E.DebugTeleport(s, "field");
        s.GameMinute = 480;
        SetStats(s, 100, 20.01, 90, 90, 90, 60);
        s.Actors[GameState.PlayerId].AlertLatched.Clear();
        var p = E.Preview(s, ActionRequest.Explore());
        Assert.That(p.BodyAlerts, Is.EqualTo(new[] { "途中饱食将越过提醒阈值，行动会暂停" }));
    }

    /// <summary>
    /// Q703-A02／Q728-A02：起点已低于等于原提醒条件（即便副本最初收集到旧低值提醒）不写“将越过”；另一人真实跨线照列。真实提醒与暂停不因筛选改变。
    /// </summary>
    [Test]
    public void Q703_StartAlreadyLow_NotListed()
    {
        var (s, me, mate) = FieldPair();
        Six(me, 20, 90, 90, 90, 60, 100);       // 饱食 20 已满足“≤20”提醒条件，锁存未置
        Six(mate, 90, 20.01, 90, 90, 60, 100);
        var name = E.ActorDisplayName(s, mate.Id);
        var p = E.Preview(s, ActionRequest.Explore());
        Assert.That(p.BodyAlerts, Is.EqualTo(new[] { $"{name}：途中水分将越过提醒阈值，行动会暂停" }));

        Start(s, ActionRequest.Explore());
        Run(s, autoResume: false);
        Assert.That(s.Log.Any(l => l.Text.Contains("饱食")), Is.True, "真实世界照原锁存提醒（起点低值的提醒不被预估筛选去掉）");
    }
}
