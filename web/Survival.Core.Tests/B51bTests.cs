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
        var bound = s.Items.Where(i => i.ReservedBy != null && i.ItemId == "dirty_water").ToList();
        Assert.That(bound.Sum(i => i.Count), Is.EqualTo(4));
        Assert.That(bound.All(i => i.ShelfPausedMinutes == 20), Is.True, "本批输入 R＝E−S");
        var free = s.Items.Single(i => i.ItemId == "dirty_water" && i.ReservedBy == null);
        Assert.That(free.ShelfPausedMinutes, Is.Null, "未投入余料不保护");
        AdvanceTo(s, t0 + 25);
        Assert.That(s.Items.Where(i => i.ReservedBy != null && i.ItemId == "dirty_water").Sum(i => i.Count), Is.EqualTo(4), "在制越过原截止不转腐败物");
        Assert.That(s.Items.Any(i => i.ItemId == "dirty_water" && i.ReservedBy == null), Is.False, "余料照常到期");
        var b = Batches(s, "water_station").Single();
        Ok(E.CancelStationBatch(s, "water_station", b.BatchId, "p1c", 1));
        var back = s.Items.Where(i => i.ItemId == "dirty_water").ToList();
        Assert.That(back.Sum(i => i.Count), Is.EqualTo(4));
        Assert.That(back.All(i => i.ShelfPausedMinutes == null && i.ExpiresAt == s.GameMinute + 20 && i.ReservedBy == null), Is.True, "恢复 C＋R");
        var zone = E.ZoneName(s, s.Ground);
        Assert.That(s.Log.Last(l => l.Kind == nameof(GameEventKind.ActionCancelled)).Text,
            Is.EqualTo($"已取消{zone}固定净水台的批次1 · {Cfg.Recipes.Single(r => r.Id == Water).Name}（已进行25分钟）。未消耗原料已退回{zone}地面。" +
                       $"{E.ItemName("dirty_water")}×4恢复剩余保质，保质到{Clock.DayTime(s.GameMinute + 20)}。没有产物和经验。已耗时间保留。"));
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
        s.Items.RemoveAll(i => i.ReservedBy == b.BatchId);
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
        var dir = TestContext.CurrentContext.TestDirectory;
        while (dir != null && !File.Exists(Path.Combine(dir, "Survival.sln"))) dir = Path.GetDirectoryName(dir);
        var card = File.ReadAllText(Path.Combine(dir!, "Survival.Web", "Components", "FacilityCard.razor"));
        Assert.That(card, Does.Contain("e.StationSummary(s, s.Ground, x.Id)"));
        Assert.That(card, Does.Not.Contain("\"加工中：\""));
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
        foreach (var i in s.Items.Where(i => i.ReservedBy != null)) i.ShelfPausedMinutes = null;   // 原截止 E 一直保留（t0+100）
        var late = s.Items.First(i => i.ReservedBy == q[2].BatchId && i.ItemId == "dirty_water");
        late.ExpiresAt = s.GameMinute;
        var ids = q.Select(b => (b.BatchId, b.XpSnapshot)).ToList();
        var obj = JObject.Parse(SaveSerializer.Serialize(s));
        obj["SchemaVersion"] = "54";
        var old = SaveSerializer.Deserialize(obj.ToString(), Cfg);
        Assert.That(old.SchemaVersion, Is.EqualTo("55"));
        var u = old.GameMinute;
        var nq = Batches(old, "water_station");
        Assert.That(nq.Select(b => (b.BatchId, b.XpSnapshot)), Is.EqualTo(ids), "批次 ID 与经验快照不变");
        Assert.That(nq.All(b => !b.Queued), Is.True, "没有等候批");
        Assert.That(nq.Select(b => b.DoneMinutes), Is.EqualTo(new[] { 20, 0, 0 }), "在办批保留进度、旧等候批不补进度");
        Assert.That((nq[1].StartMinute, nq[2].StartMinute), Is.EqualTo((u, u)));
        var bound = old.Items.Where(i => i.ReservedBy == nq[0].BatchId && i.ItemId == "dirty_water").ToList();
        Assert.That(bound.All(i => i.ShelfPausedMinutes == 80), Is.True, "R＝旧E−U，不补偿已耗");
        Assert.That(old.Items.Where(i => i.ReservedBy == nq[2].BatchId && i.ExpiresAt == u).All(i => i.ShelfPausedMinutes == null), Is.True, "U 时已到期的不保护");

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
        Assert.That(back.SchemaVersion, Is.EqualTo("55"));
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
        Assert.That(GameEngine.OutputStatusText(p, ActionType.Gather), Is.EqualTo("产出 · 留在安全屋地面"));
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
        Assert.That(pv, Does.Contain("p.PlanFailed ? GameEngine.NotEstimated : $\"{Ui.N(p.ToolDurabilityAfter ?? 0)}"));
        var pops = Web("Components/ActionPops.razor");
        Assert.That(pops, Does.Contain("@GameEngine.ToolAfterText(p)"));
        Assert.That(pops, Does.Contain("p.PlanFailed ? GameEngine.NotEstimated : Clock.HHmm(p.EstimatedEndMinute) + \"到达\""));
        Assert.That(pops, Does.Contain("p.PlanFailed ? GameEngine.NotEstimated : Clock.HHmm(p.EstimatedEndMinute) + \"完成\""));
        Assert.That(pops, Does.Contain("p.Ok && !p.PlanFailed && !e.IsDaylight(p.EstimatedEndMinute)"));
    }
}
