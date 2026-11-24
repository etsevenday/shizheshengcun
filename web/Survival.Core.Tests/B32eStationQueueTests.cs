using Newtonsoft.Json.Linq;
using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 批次 B32e（R142）：策划答复 Q380 选 B——陶窑与固定净水台连续投料（S08「2026-11-10 Q380」，A07 同名节取代 X1.4 单批限制）。
/// B51b（S08 文首 Q701.2、B51 Q721／Q727）起原串行等待队列改为每座最多 10 批全部并行、「取消本批」只绑定 batchId：本文件按新规则改写
/// （原 Q380-A01—A04 中投料、整次校验、预留归属、读档与跨日经验仍验；串行完成、队首接上、取消排队按新规则改为并行／取消本批）。
/// </summary>
public class B32eStationQueueTests : TestBase
{
    private const string Water = "ex_station_water";
    private const string Kiln = "ex_ceramic_shell";

    private void Ok(ActionResult r) => Assert.That(r.Ok, Is.True, r.Reason);

    private static ActionRequest Craft(string id, int n = 1) => ActionRequest.Craft(id, null, null, n);

    private static int Sum(GameState s, string itemId, string? owner = null) =>
        s.Items.Where(i => i.ItemId == itemId && (owner == null || i.Owner == owner)).Sum(i => i.Count);

    /// <summary>归各批的份数。B51c2a1（S11 Q872 选 B）起原料在设施内、燃料在地面预留，两处合计（原只数地面预留）。</summary>
    private static int Reserved(GameState s, string itemId) => s.Items.Where(i => i.ItemId == itemId && InAnyBatch(i)).Sum(i => i.Count);

    private static int StationXpOn(GameState s, int day) =>
        s.Player.DailyCounts.TryGetValue($"{day}|stationXp|crafting", out var n) ? n : 0;

    private List<StationBatch> Queue(GameState s, string station) =>
        E.FacOf(s, s.Ground)!.StationBatches.Where(b => b.StationId == station).ToList();

    /// <summary>营地外部区建好陶窑与固定净水台，清掉相关材料，制造 3 级、经验清零。</summary>
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
        Assert.That(E.WorkshopBuilt(s, s.Ground, "kiln") && E.WorkshopBuilt(s, s.Ground, "water_station"), Is.True);
        s.Items.RemoveAll(i => i.ItemId is "clay" or "charcoal" or "dirty_water" or "filter_media" or "ceramic_shell" or "clean_water");
        s.Skills["crafting"].Level = 3;
        s.Skills["crafting"].Xp = 0;
        return s;
    }

    private void WaterInputs(GameState s, int batches)
    {
        E.CreateItems(s, "dirty_water", 20 * batches, s.Ground);   // B56（W100-Q929-11）：ex_station_water 一批脏水 4→20、净水 4→20（2 升不变）
        E.CreateItems(s, "filter_media", batches, ItemInstance.Carried);
    }

    private void KilnInputs(GameState s, int batches)
    {
        E.CreateItems(s, "clay", 2 * batches, s.Ground);
        E.CreateItems(s, "charcoal", batches, s.Ground);
    }

    private static string RazorOf(string name)
    {
        var dir = TestContext.CurrentContext.TestDirectory;
        while (dir != null && !File.Exists(Path.Combine(dir, "Survival.sln"))) dir = Path.GetDirectoryName(dir);
        return File.ReadAllText(Path.Combine(dir!, "Survival.Web", name));
    }

    // ───────────── Q380-A01／Q701-A03 ─────────────

    /// <summary>Q701-A03（取代 Q380-A01 串行）：净水台同时投 3 批，预留脏水×12、滤材×3，预估净水×12；3 批都在 30 分钟完成，落地 3 次各净水×4。</summary>
    [Test]
    public void Q380_A01_WaterStation_ThreeBatches_AllParallel()
    {
        var s = Workshop();
        WaterInputs(s, 3);
        var rc = Cfg.Recipes.Single(r => r.Id == Water);
        Assert.That(E.StationBatchMaxNew(s, rc), Is.EqualTo(3), "上限 = min(10−0, 材料够的 3 批)");
        var p = E.Preview(s, Craft(Water, 3));
        Assert.That(p.Outputs.Single(o => o.ItemId == "clean_water").Count, Is.EqualTo(60), "显示预估净水×60");   // B56（W100-Q929-11）：ex_station_water 一批脏水 4→20、净水 4→20（2 升不变）（原 ×12）
        // B35b（S14 2026-11-14 B35 次要 Q597 A、B35b Q947 A）：投料按钮名带原固定配方时长“投料（{批数}批 · {时长}）”，原因句随按钮名
        Assert.That(p.Reason, Does.Contain("投料（3批 · 30分钟）"));
        var t0 = s.GameMinute;
        Assert.That(E.StationNewFinishes(s, rc, 3), Is.EqualTo(new[] { t0 + 30, t0 + 30, t0 + 30 }), "各批都从现在开始，30 分钟完成");

        Ok(E.StartStationBatch(s, Water, "a01", 3));
        Assert.That(s.GameMinute, Is.EqualTo(t0), "0 分钟");
        Assert.That(s.Main, Is.Null, "不占主行动");
        Assert.That((Reserved(s, "dirty_water"), Reserved(s, "filter_media")), Is.EqualTo((60, 3)));   // B56（W100-Q929-11）：ex_station_water 一批脏水 4→20、净水 4→20（2 升不变）（原 12）
        // B51c2a1 改期望（S11 Q872 选 B：原料移进设施该批、燃料仍地面预留 Q921）：原断言「预留移到设施所在区地面」
        Assert.That(s.Items.Where(i => i.ItemId is "dirty_water" or "filter_media" && i.InStation && ItemInstance.TryStation(i.Owner, out var pl, out _) && pl == s.Ground).Sum(i => i.Count), Is.EqualTo(63), "原料移进设施所在区的设施内");   // B56（W100-Q929-11）：ex_station_water 一批脏水 4→20、净水 4→20（2 升不变）：60＋3（原 15）
        var q = Queue(s, "water_station");
        Assert.That(q.All(b => !b.Queued && b.StartMinute == t0 && b.DoneMinutes == 0), Is.True, "全部加工中，没有等候批");
        foreach (var b in q)
            Assert.That((BatchBound(s, b.BatchId).Where(i => i.ItemId == "dirty_water").Sum(i => i.Count),
                         BatchBound(s, b.BatchId).Where(i => i.ItemId == "filter_media").Sum(i => i.Count)), Is.EqualTo((20, 1)), "每份只归一批");   // B56（W100-Q929-11）：ex_station_water 一批脏水 4→20、净水 4→20（2 升不变）（原 (4, 1)）   // B51c2a1 改期望（S11 Q872 选 B：原料移进设施该批、燃料仍地面预留 Q921）：原按 ReservedBy 认批
        Assert.That(s.Log.Last().Text, Does.Contain("投料（3批）").And.Contain("；各批同时开始加工；"));

        AdvanceTo(s, t0 + 5);
        Assert.That(q.Select(b => b.DoneMinutes), Is.EqualTo(new[] { 5, 5, 5 }), "每段各 +5，不是总进度分摊");
        AdvanceTo(s, t0 + 25);
        Assert.That(Sum(s, "clean_water", "camp"), Is.EqualTo(0));
        AdvanceTo(s, t0 + 30);
        Assert.That(Sum(s, "clean_water", "camp"), Is.EqualTo(60), "3 批都在 30 分钟完成，而非 30／60／90");   // B56（W100-Q929-11）：ex_station_water 一批脏水 4→20、净水 4→20（2 升不变）（原 12）
        Assert.That(Sum(s, "dirty_water") + Sum(s, "filter_media"), Is.EqualTo(0), "逐批核销");
        Assert.That(Queue(s, "water_station"), Is.Empty);
        var done = s.Log.Where(l => l.Kind == nameof(GameEventKind.FacilityDone) && l.Text.Contains("净水")).ToList();
        Assert.That(done.Count, Is.EqualTo(3), "落地 3 次");
        Assert.That(done.All(l => l.Text.Contains("净水×20")), Is.True, "每次净水×20");   // B56（W100-Q929-11）：一批 20 份（原「净水×4」）
    }

    /// <summary>Q701-A03：陶窑同时 3 批都在 60 分钟完成，各自核销原料与木炭（燃料 N 倍，不共用一份）。</summary>
    [Test]
    public void Q380_A01_Kiln_ThreeBatches_SixtyMinutes_CharcoalPerBatch()
    {
        var s = Workshop();
        KilnInputs(s, 3);
        var t0 = s.GameMinute;
        Ok(E.StartStationBatch(s, Kiln, "a01k", 3));
        Assert.That((Reserved(s, "clay"), Reserved(s, "charcoal")), Is.EqualTo((6, 3)), "原料黏土 2×3、燃料木炭 1×3");
        AdvanceTo(s, t0 + 55);
        Assert.That(Sum(s, "ceramic_shell"), Is.EqualTo(0));
        AdvanceTo(s, t0 + 60);
        Assert.That((Sum(s, "ceramic_shell"), Sum(s, "clay"), Sum(s, "charcoal")), Is.EqualTo((6, 0, 0)));
    }

    // ───────────── Q380-A02／Q701-A04 ─────────────

    /// <summary>Q701-A04（取代 Q380 队尾等待）：已有批时新投批立即从自己的投料时刻开始，不等旧批、不补旧进度；各批按本批剩余完成。</summary>
    [Test]
    public void Q380_A02_BusyNewBatchStartsNow()
    {
        var s = Workshop();
        WaterInputs(s, 3);
        var t0 = s.GameMinute;
        Ok(E.StartStationBatch(s, Water, "x1"));
        AdvanceTo(s, t0 + 10);
        var head = Queue(s, "water_station").Single();
        Assert.That(head.DoneMinutes, Is.EqualTo(10));
        Ok(E.StartStationBatch(s, Water, "x2", 2));
        var q = Queue(s, "water_station");
        Assert.That(q[0], Is.SameAs(head), "已有加工批不变");
        Assert.That(q.Skip(1).All(b => !b.Queued && b.DoneMinutes == 0 && b.StartMinute == t0 + 10), Is.True, "新批从自己的投料时刻开始");
        Assert.That(q.Select(b => b.BatchId), Is.Ordered, "按创建顺序");
        AdvanceTo(s, t0 + 15);
        Assert.That(q.Select(b => b.DoneMinutes), Is.EqualTo(new[] { 15, 5, 5 }), "同段各 +5");
        AdvanceTo(s, t0 + 30);
        Assert.That(Sum(s, "clean_water"), Is.EqualTo(20), "旧批按自己剩余先完成");   // B56（W100-Q929-11）：ex_station_water 一批脏水 4→20、净水 4→20（2 升不变）（原 4）
        AdvanceTo(s, t0 + 40);
        Assert.That(Sum(s, "clean_water"), Is.EqualTo(60), "新批 30 分钟后同时完成");   // B56（W100-Q929-11）：ex_station_water 一批脏水 4→20、净水 4→20（2 升不变）（原 12）
    }

    /// <summary>Q380-A02／Q701.4：余位不足或总料不足时整次拒绝（不预留、不改批次）；满 10 批「加工批次已满（10批）。」。</summary>
    [Test]
    public void Q380_A02_RoomOrMaterialShort_RejectsWholeRequest()
    {
        var s = Workshop();
        var rc = Cfg.Recipes.Single(r => r.Id == Water);
        WaterInputs(s, 3);
        E.CreateItems(s, "dirty_water", 20, s.Ground);   // 脏水够 4 批（80），滤材只够 3 批；B56：每份100毫升，×5 保持原毫升
        var before = SaveSerializer.Serialize(s);
        var r4 = E.StartStationBatch(s, Water, "m4", 4);
        Assert.That(r4.Ok, Is.False);
        Assert.That(r4.Reason, Does.Contain("缺少滤材（3/4"), "按实际原料缺口提示");
        Assert.That(SaveSerializer.Serialize(s), Is.EqualTo(before), "整次拒绝：不预留、不建批");
        Assert.That(E.StationBatchMaxNew(s, rc), Is.EqualTo(3));

        // 填到 8 批，余位 2：要 3 批整次拒绝
        WaterInputs(s, 10);
        Ok(E.StartStationBatch(s, Water, "f8", 8));
        Assert.That(E.StationRoom(s, "water_station"), Is.EqualTo(2));
        Assert.That(E.StationBatchMaxNew(s, rc), Is.EqualTo(2), "上限 = min(10−8, 材料够的批数)");
        var reserved = Reserved(s, "dirty_water");
        var r3 = E.StartStationBatch(s, Water, "f3", 3);
        Assert.That(r3.Ok, Is.False);
        Assert.That((Reserved(s, "dirty_water"), Queue(s, "water_station").Count), Is.EqualTo((reserved, 8)), "余位不足整次拒绝");
        Ok(E.StartStationBatch(s, Water, "f2", 2));
        Assert.That(Queue(s, "water_station").Count, Is.EqualTo(10), "最多 10 批并行");
        Assert.That(E.StationBatchMaxNew(s, rc), Is.EqualTo(0));
        Assert.That(E.StationBatchBlocked(s, rc), Is.EqualTo("加工批次已满（10批）。"));
        Assert.That(E.StartStationBatch(s, Water, "f1").Reason, Is.EqualTo("加工批次已满（10批）。"), "第 11 批拒绝");
    }

    // ───────────── Q380-A03／Q721 ─────────────

    /// <summary>
    /// B51c2a1 改写（S11 Q870 B 每批「取出」取代 Q721「取消本批」；Q876 B 结果清单；Q880 撤取消专用失效句，过时计划「物品已变，请重选。」）：
    /// 取出只结束所点批、原料退本区地面，其他批继续原进度（不重启）；已完成批照原记录；同一请求重复不双返；
    /// 本批已结束的新请求「物品已变，请重选。」，不改指另一批。原断言的取消日志句与「当前区的…没有进行中的加工批次。」随取消入口撤下。
    /// </summary>
    [Test]
    public void Q380_A03_CancelThisBatch_Idempotent_BoundToId()
    {
        var s = Workshop();
        WaterInputs(s, 4);
        var t0 = s.GameMinute;
        Ok(E.StartStationBatch(s, Water, "c-load", 2));
        Assert.That(E.StartStationBatch(s, Water, "c-load", 2).Duplicate, Is.True, "同一投料请求只生效一次");
        AdvanceTo(s, t0 + 15);
        Ok(E.StartStationBatch(s, Water, "c-load2", 2));
        var q = Queue(s, "water_station");
        Assert.That(q.Count, Is.EqualTo(4));
        var (a, b, c, d) = (q[0], q[1], q[2], q[3]);
        string Stamp(StationBatch x) => E.StationTakeStamp(s, s.Ground, "water_station", x.BatchId);

        // 点中乙后，甲先取出、列表换序：仍只取出乙
        var bStamp = Stamp(b);
        Ok(E.TakeStationBatch(s, s.Ground, "water_station", a.BatchId, Stamp(a), "c-a"));
        var reservedBefore = Reserved(s, "dirty_water");
        Ok(E.TakeStationBatch(s, s.Ground, "water_station", b.BatchId, bStamp, "c-b"));
        Assert.That(Reserved(s, "dirty_water"), Is.EqualTo(reservedBefore - 20), "只释放该批");   // B56（W100-Q929-11）：ex_station_water 一批脏水 4→20、净水 4→20（2 升不变）（原 −4）
        Assert.That(s.Items.Where(i => i.ItemId == "dirty_water" && !InAnyBatch(i)).Sum(i => i.Count), Is.EqualTo(40), "甲乙两批脏水退回");   // B56（W100-Q929-11）：ex_station_water 一批脏水 4→20、净水 4→20（2 升不变）（原 8）
        Assert.That(s.Items.Where(i => i.ItemId == "dirty_water" && !InAnyBatch(i)).All(i => i.Owner == "camp"), Is.True, "退到本区地面");
        // Q876 B：「已取出至{地点}地面：{物品清单}。」名×数、顿号（A07 ex_station_water 每批脏水4、滤材1；地点为营地区名）
        Assert.That(s.Log.Last().Text, Is.EqualTo("已取出至临时营地地面：脏水×20、滤材×1。"));   // B56（W100-Q929-11）：ex_station_water 一批脏水 4→20、净水 4→20（2 升不变）（原「脏水×4」）
        Assert.That(Queue(s, "water_station"), Is.EqualTo(new[] { c, d }), "其他批不动");
        Assert.That((c.DoneMinutes, d.DoneMinutes), Is.EqualTo((0, 0)), "不重启、不串行");
        Assert.That(E.TakeStationBatch(s, s.Ground, "water_station", b.BatchId, bStamp, "c-b").Duplicate, Is.True, "同一请求重复只生效一次");
        var stale = E.TakeStationBatch(s, s.Ground, "water_station", b.BatchId, bStamp, "c-b-2");
        Assert.That((stale.Ok, stale.Reason), Is.EqualTo((false, "物品已变，请重选。")), "本批已结束才报变化");
        Assert.That(Queue(s, "water_station").Count, Is.EqualTo(2), "不取出现在同序号的另一批");

        // 本批正常进度更新、别批完成都不使请求失效
        var cStamp = Stamp(c);
        AdvanceTo(s, t0 + 25);
        Assert.That(Stamp(c), Is.EqualTo(cStamp), "进度推进不改计划快照");
        AdvanceTo(s, t0 + 45);
        Assert.That(Sum(s, "clean_water"), Is.EqualTo(40), "丙、丁照原进度在 30 分钟完成");   // B56（W100-Q929-11）：ex_station_water 一批脏水 4→20、净水 4→20（2 升不变）（原 8）
        Assert.That(s.Skills["crafting"].Xp, Is.EqualTo(6L), "取出批没有经验，完成批照原记录");
        var gone = E.TakeStationBatch(s, s.Ground, "water_station", c.BatchId, cStamp, "c-c");
        Assert.That(gone.Reason, Is.EqualTo("物品已变，请重选。"), "已完成批报变化");
    }

    /// <summary>Q701-A04：完成时预留缺失只终止该批（释放剩余预留、无产物无经验），其他批照原进度、只用自己绑定的预留。</summary>
    [Test]
    public void Q380_A03_ReservationLost_OnlyThatBatchEnds()
    {
        var s = Workshop();
        WaterInputs(s, 2);
        E.CreateItems(s, "dirty_water", 20, s.Ground);   // 未预留的余料，不被补取；B56：每份100毫升，×5 保持原毫升
        var t0 = s.GameMinute;
        Ok(E.StartStationBatch(s, Water, "lost", 2));
        var first = Queue(s, "water_station")[0];
        s.Items.Remove(BatchBound(s, first.BatchId).First(i => i.ItemId == "filter_media"));   // B51c2a1 改期望（S11 Q872 选 B：原料移进设施该批、燃料仍地面预留 Q921）：原按 ReservedBy 认批
        AdvanceTo(s, t0 + 30);
        Assert.That(Sum(s, "clean_water"), Is.EqualTo(20), "只有失效批没有产物，另一批照常完成");   // B56（W100-Q929-11）：ex_station_water 一批脏水 4→20、净水 4→20（2 升不变）（原 4）
        Assert.That(s.Skills["crafting"].Xp, Is.EqualTo(3), "失效批没有经验");
        Assert.That(BatchBound(s, first.BatchId), Is.Empty, "剩余预留已释放");   // B51c2a1 改期望（S11 Q872 选 B：原料移进设施该批、燃料仍地面预留 Q921）：原按 ReservedBy 认批
        Assert.That(Sum(s, "dirty_water"), Is.EqualTo(40), "失效批的脏水退回地面，只核销自己绑定的预留，不补取余料");   // B56（W100-Q929-11）：ex_station_water 一批脏水 4→20、净水 4→20（2 升不变）（原 8）
    }

    // ───────────── Q380-A04 ─────────────

    /// <summary>Q380-A04：旧档（存档 44）的单批作为加工中批续原进度；新档读回批次与预留；读档不补进度。</summary>
    [Test]
    public void Q380_A04_LegacySingleBatch_And_RoundTrip()
    {
        var s = Workshop();
        KilnInputs(s, 1);
        var t0 = s.GameMinute;
        Ok(E.StartStationBatch(s, Kiln));
        AdvanceTo(s, t0 + 25);
        E.DebugLegacyStation56(s);   // B51c2a1：造旧档起点——57 前原料在地面按批预留（S11 Q872 选 B 之前的模型）
        var obj = JObject.Parse(SaveSerializer.Serialize(s));
        obj["SchemaVersion"] = "44";
        foreach (var b in obj.SelectTokens("$..StationBatches[*]").OfType<JObject>()) b.Remove("Queued");
        var old = SaveSerializer.Deserialize(obj.ToString(), Cfg);
        Assert.That(old.SchemaVersion, Is.EqualTo(GameState.CurrentSchema));
        var legacy = E.StationBatchesHere(old, "kiln").Single();
        Assert.That((legacy.Queued, legacy.DoneMinutes, legacy.XpSnapshot), Is.EqualTo((false, 25, 6)), "原进度、经验快照原样接入");
        Assert.That(BatchBound(old, legacy.BatchId).Sum(i => i.Count), Is.EqualTo(3), "原料与燃料仍归本批");   // B51c2a1 改期望（S11 Q872 选 B：原料移进设施该批、燃料仍地面预留 Q921）：原按 ReservedBy 认批
        Assert.That(old.Items.Where(i => i.Owner == ItemInstance.StationOwner(old.Ground, legacy.BatchId)).Sum(i => i.Count), Is.EqualTo(2), "56→57：黏土2移进陶窑该批（S11 Q872 选 B）");
        AdvanceTo(old, t0 + 55);
        Assert.That(Sum(old, "ceramic_shell"), Is.EqualTo(0));
        AdvanceTo(old, t0 + 60);
        Assert.That(Sum(old, "ceramic_shell"), Is.EqualTo(2), "续原进度在 60 分钟完成");

        // 新档：批次、进度、预留归属读回；读档本身不补进度
        var n = Workshop();
        WaterInputs(n, 3);
        Ok(E.StartStationBatch(n, Water, "rt", 3));
        AdvanceTo(n, n.GameMinute + 10);
        var ids = Queue(n, "water_station").Select(b => (b.BatchId, b.Queued, b.DoneMinutes)).ToList();
        var owners = n.Items.Where(InAnyBatch).Select(i => (i.InstanceId, i.ReservedBy, i.Owner, i.Count)).OrderBy(x => x.InstanceId).ToList();   // B51c2a1 改期望（S11 Q872 选 B：原料移进设施该批、燃料仍地面预留 Q921）：归属一并比对
        var back = SaveSerializer.Deserialize(SaveSerializer.Serialize(n), Cfg);
        Assert.That(Queue(back, "water_station").Select(b => (b.BatchId, b.Queued, b.DoneMinutes)), Is.EqualTo(ids));
        Assert.That(owners.Count, Is.GreaterThan(0));
        Assert.That(back.Items.Where(InAnyBatch).Select(i => (i.InstanceId, i.ReservedBy, i.Owner, i.Count)).OrderBy(x => x.InstanceId), Is.EqualTo(owners));
        Assert.That(back.GameMinute, Is.EqualTo(n.GameMinute), "读档不补时间与进度");
        AdvanceTo(back, back.GameMinute + 20);
        Assert.That(Sum(back, "clean_water"), Is.EqualTo(60));   // B56（W100-Q929-11）：ex_station_water 一批脏水 4→20、净水 4→20（2 升不变）（原 12）
    }

    /// <summary>Q380-A04：跨世界日按实际完成日核算经验；预览按预计完成日显示（新批同刻完成）。</summary>
    [Test]
    public void Q380_A04_CrossDay_XpByActualFinishDay()
    {
        var s = Workshop();
        var day = Clock.Day(s.GameMinute);
        AdvanceTo(s, day * Clock.MinutesPerDay - 30);   // 当日 23:30
        KilnInputs(s, 2);
        var rc = Cfg.Recipes.Single(r => r.Id == Kiln);
        var preview = E.StationBatchXpPreview(s, rc, 2);
        Assert.That(preview, Does.Contain("制造经验+12").And.Contain($"按完成日第{day + 1}日额度").And.Not.Contain("；"), "两批同刻次日完成、同日合计");

        Ok(E.StartStationBatch(s, Kiln, "xd", 2));
        AdvanceTo(s, day * Clock.MinutesPerDay + 30);
        Assert.That((StationXpOn(s, day), StationXpOn(s, day + 1)), Is.EqualTo((0, 12)), "次日完成，扣次日额度");
        Assert.That(s.Skills["crafting"].Xp, Is.EqualTo(12));
        Assert.That(Sum(s, "ceramic_shell"), Is.EqualTo(4));
    }

    /// <summary>Q701-A05：预览扣掉同一投料者先完成（或同刻排在前面）的在途批次。</summary>
    [Test]
    public void Q380_XpPreview_CountsBatchesAhead()
    {
        var s = Workshop();
        KilnInputs(s, 3);
        var rc = Cfg.Recipes.Single(r => r.Id == Kiln);
        Assert.That(E.StationBatchXpPreview(s, rc, 2), Does.Contain("制造经验+12"), "同日两批合计");
        Ok(E.StartStationBatch(s, Kiln, "pa", 2));
        Assert.That(E.StationBatchXpPreview(s, rc, 1), Does.Contain("已满12"), "同刻完成的两批已占满当日 12");
        var q = Queue(s, "kiln");
        Assert.That(E.StationBatchXpNote(s, q[0]), Does.Contain("+6"));
        Assert.That(E.StationBatchXpNote(s, q[1]), Does.Contain("+6"), "同刻按创建顺序：12−6 仍够 6");
    }

    /// <summary>网页壳接线（源码检查）：「投料批数」「投料（{批数}批）」、并行标题与说明、每批一行「取消本批」绑定所见批次与序号；不再有排队与取消排队。</summary>
    [Test]
    public void Q380_WebWiring()
    {
        var razor = RazorOf(Path.Combine("Components", "CraftOverlay.razor"));
        Assert.That(razor, Does.Contain(">投料批数<"));
        // B35b 改（S14 2026-11-14 B35 次要 Q597 A、B35b Q947 A；测试规则第 8 条）：投料按钮改“投料（{批数}批 · {时长}）”，文字由内核 GameEngine.StationLoadText 给出，
        // 渲染出的按钮文字由 bUnit B35bCraftPageUiTests.Station_LoadButton_BatchAndStaticDuration 断言；这里只留不再写死旧格式
        Assert.That(razor, Does.Not.Contain("投料（@(batch)批）"));
        // B51c2b 删断言（S11 §Q870 B「只数真实正在加工的批」／§Q924 B 余料条目不计加工中；测试规则第 8 条）：标题改按真实加工批数，
        // 由 bUnit B51c2bFacilityItemUiTests.Station_PartialArrow_ResidualRow_TitleCountsProcessing 断渲染出的「加工中{批数}批」
        Assert.That(razor, Does.Contain("@GameEngine.StationParallelHelp"));
        Assert.That(razor, Does.Contain("e.StationBatchRow(s, sbItem, seq)"));
        Assert.That(razor, Does.Contain("@e.StationOutputGround(s, zonePlace)"));
        // B51c2a1 改期望（S11 Q870 B／Q778：撤「取消本批」，每批「取出」、操作区「全部取出」）：原断言取消键与 CancelStationBatch 接线
        Assert.That(razor, Does.Contain(">@GameEngine.TakeLabel<"));
        Assert.That(razor, Does.Contain(">@GameEngine.TakeAllLabel<"));
        Assert.That(razor, Does.Contain("G.E.TakeStationBatch(G.S, place, stationId, batchId, stamp, $\"station-take:{batchId}\")"));
        Assert.That(razor, Does.Not.Contain("CancelStationBatch"));
        Assert.That(razor, Does.Not.Contain("data-station-cancel"));
        Assert.That(razor, Does.Not.Contain("StationCancelLabel"));
        Assert.That(razor, Does.Not.Contain("CancelStationQueue"));
        Assert.That(razor, Does.Not.Contain("排队中"));
        Assert.That(razor, Does.Not.Contain(">取消当前批<"));
        Assert.That(razor, Does.Not.Contain("投料（被动）"));
        // B51c2a1 改期望（S11 Q779 选 B）：说明可见时「加工中{批数}批」，说明不可见的原分支保留「（最多10批）」
        Assert.That(GameEngine.StationTitle(3, true), Is.EqualTo("加工中3批"));
        Assert.That(GameEngine.StationTitle(3, false), Is.EqualTo("加工中3批（最多10批）"));
        Assert.That(GameEngine.StationParallelHelp, Is.EqualTo("各批同时加工，每座最多10批。"));
        Assert.That((GameEngine.TakeLabel, GameEngine.TakeAllLabel), Is.EqualTo(("取出", "全部取出")));   // B51c2a1：原断言取消键「取消本批」（Q778 撤）
    }

    // ───────────── Q397—Q399（S08「Q397–Q399 · B32e 文案定稿」）与 Q701.4 ─────────────

    /// <summary>Q701.4（取代 Q397 队列句）：还有 2 个空位提交 3 批、提交 0 批、满 10 批，分别显示定稿句。</summary>
    [Test]
    public void Q397_A01_FallbackTexts()
    {
        var s = Workshop();
        var rc = Cfg.Recipes.Single(r => r.Id == Water);
        WaterInputs(s, 11);
        Ok(E.StartStationBatch(s, Water, "q397-8", 8));
        Assert.That(E.StartStationBatch(s, Water, "q397-3", 3).Reason, Is.EqualTo("还可同时加工2批，请减少投料批数。"));
        Assert.That(E.StartStationBatch(s, Water, "q397-0", 0).Reason, Is.EqualTo("投料批数必须是1或更大的整数。"));
        Assert.That(E.StationBatchBlocked(s, rc, -1), Is.EqualTo("投料批数必须是1或更大的整数。"));
        Ok(E.StartStationBatch(s, Water, "q397-2", 2));
        Assert.That(E.StartStationBatch(s, Water, "q397-full", 0).Reason, Is.EqualTo("加工批次已满（10批）。"), "10 批满时先报额满（校验顺序不变）");
    }

    /// <summary>Q398-A01：陶窑燃料分账块写「批数」（值为投料批数），固定净水台同字段同样；锻炉仍写「份数」。</summary>
    [Test]
    public void Q398_A01_FuelBlockBatchWord()
    {
        var razor = RazorOf(Path.Combine("Components", "CraftOverlay.razor"));
        Assert.That(razor, Does.Contain("<span>@(stationRecipe ? \"批数\" : \"份数\")<b>@batch</b></span>"));
        Assert.That(razor, Does.Not.Contain("<span>份数<b>@batch</b></span>"));
        Assert.That(E.IsStationBatchRecipe(Cfg.Recipes.Single(r => r.Id == Kiln)), Is.True, "陶窑配方走「批数」");
        var forge = Cfg.Recipes.Where(r => r.Fuel.Count > 0 && r.Station == "forge").ToList();
        Assert.That(forge, Is.Not.Empty);
        Assert.That(forge.All(r => !E.IsStationBatchRecipe(r)), Is.True, "锻炉配方仍写「份数」");
    }

    /// <summary>Q727-A01（取代 Q399 串行时刻清单）：新投多批同刻完成合成一句「预计{日期时刻}完成」；不同时刻「预计分别于…完成」升序去重。</summary>
    [Test]
    public void Q399_A01_EtaFormat()
    {
        var s = Workshop();
        var day = Clock.Day(s.GameMinute);
        AdvanceTo(s, day * Clock.MinutesPerDay - 70);   // 当日 22:50
        var rc = Cfg.Recipes.Single(r => r.Id == Kiln);
        KilnInputs(s, 3);
        var t = s.GameMinute;
        Assert.That(E.StationEtaText(s, rc, 3), Is.EqualTo($"预计{Clock.DayTime(t + 60)}完成"), "同刻合句");
        Assert.That(E.StationNewFinishes(s, rc, 3).Count, Is.EqualTo(3), "批数不因合句减少");
        var m = typeof(GameEngine).GetMethod("StationFinishText", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        Assert.That(m.Invoke(null, new object[] { new[] { t + 60, t + 30, t + 60 } }), Is.EqualTo($"预计分别于{Clock.DayTime(t + 30)}、{Clock.DayTime(t + 60)}完成"));
    }

    /// <summary>
    /// 试玩修复 M680：连点取消不再连取消两批——连击第二下起（Detail&gt;1）不提交，提交成功后按钮停用一段再随重渲恢复；请求号与所绑批次来自渲染时。
    /// 批次区放进原料滚动区顶部、投料条留在卡底，矮屏整卡兜底可滚（B51b 试玩修复 M972 起批次区移出原料滚动区自成一区，见下）。
    /// </summary>
    [Test]
    public void Q380_M680_ClickGuard_And_QueueInsideScroll()
    {
        var razor = RazorOf(Path.Combine("Components", "CraftOverlay.razor"));
        var css = RazorOf(Path.Combine("wwwroot", "css", "app.css"));
        Assert.That(razor, Does.Contain("ev.Detail <= 1 && !StationHeld"), "连击第二下起不提交");
        // B51c2a1 改期望（S11 Q870 B：取消键换成每批「取出」，批号与本批快照戳在渲染时绑定；Q879 被挡时置灰）
        Assert.That(razor, Does.Contain("(MouseEventArgs ev) => TakeStation(zonePlace, stationId, bid, takeStamp, ev)"), "批号与快照戳在渲染时绑定");
        Assert.That(razor, Does.Contain("(MouseEventArgs ev) => LoadStation(rc.Id, batch, loadKey, ev)"));
        Assert.That(razor, Does.Contain("disabled=\"@(takeWhy != null || StationHeld)\""), "取出键提交后停用");
        Assert.That(razor, Does.Contain("disabled=\"@(allWhy != null || StationHeld)\""), "全部取出键提交后停用");
        Assert.That(razor, Does.Contain("disabled=\"@(sbWhy != null || StationHeld)\""));
        // B51b 试玩修复 M972：批次区移出原料滚动区，在配方说明区（.cm-body，陶窑／净水台自成滚动区）与投料栏之间自成一区，逐批列表单独滚动；
        // 「投料批数」步进器移进投料栏、在「投料」按钮上方
        var body = razor.IndexOf("<div class=\"cm-body\"", StringComparison.Ordinal);
        var scroll = razor.IndexOf("<div class=\"mat-scroll", StringComparison.Ordinal);
        // B35b 改锚点（逐字稿 P06a「所需材料」）：材料表标题从「原料／原料与燃料」改「所需材料」，按 data-pt 钩子定位
        var mats = razor.IndexOf("data-pt=\"craft-mats-head\"", StringComparison.Ordinal);
        var queue = razor.IndexOf("data-station-queue", StringComparison.Ordinal);
        var rows = razor.IndexOf("<div class=\"sq-rows\"", StringComparison.Ordinal);
        var go = razor.IndexOf("data-station-go", StringComparison.Ordinal);
        var stepper = razor.IndexOf("data-station-batches", StringComparison.Ordinal);
        var load = razor.IndexOf("(MouseEventArgs ev) => LoadStation(", StringComparison.Ordinal);
        Assert.That(body >= 0 && body < scroll && scroll < mats && mats < queue, Is.True, "批次区在原料滚动区之后、配方说明区外");
        Assert.That(queue < rows && rows < go, Is.True, "逐批列表单独滚动；投料栏在批次区之后（B35b Q593 A 起在内右列“本次制作”）");
        Assert.That(go < stepper && stepper < load, Is.True, "投料批数在投料栏内、投料按钮上方");
        Assert.That(css, Does.Contain(".craft-side > .craft-mats.station-mats { overflow-y: auto; }"));
        Assert.That(css, Does.Contain(".craft-side > .craft-mats > .cm-body { display: contents; }"), "一般配方布局不变");
        Assert.That(css, Does.Contain(".sq-rows { flex: 0 1 auto; min-height: 0; overflow-y: auto;"));
        Assert.That(css, Does.Contain(".sq-row { display: flex; align-items: flex-start;"));
    }
}
