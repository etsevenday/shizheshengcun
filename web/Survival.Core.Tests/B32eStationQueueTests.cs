using Newtonsoft.Json.Linq;
using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 批次 B32e（R142）：策划答复 Q380 选 B——陶窑与固定净水台连续投料、多批排队（S08「2026-11-10 Q380」，A07 同名节取代 X1.4 单批限制）。
/// 验收 Q380-A01—A04。
/// </summary>
public class B32eStationQueueTests : TestBase
{
    private const string Water = "ex_station_water";
    private const string Kiln = "ex_ceramic_shell";

    private void Ok(ActionResult r) => Assert.That(r.Ok, Is.True, r.Reason);

    private static ActionRequest Craft(string id, int n = 1) => ActionRequest.Craft(id, null, null, n);

    private static int Sum(GameState s, string itemId, string? owner = null) =>
        s.Items.Where(i => i.ItemId == itemId && (owner == null || i.Owner == owner)).Sum(i => i.Count);

    private static int Reserved(GameState s, string itemId) => s.Items.Where(i => i.ItemId == itemId && i.ReservedBy != null).Sum(i => i.Count);

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
        E.CreateItems(s, "dirty_water", 4 * batches, s.Ground);
        E.CreateItems(s, "filter_media", batches, ItemInstance.Carried);
    }

    private void KilnInputs(GameState s, int batches)
    {
        E.CreateItems(s, "clay", 2 * batches, s.Ground);
        E.CreateItems(s, "charcoal", batches, s.Ground);
    }

    // ───────────── Q380-A01 ─────────────

    /// <summary>Q380-A01：净水台选 3 批，预留脏水×12、滤材×3，预估净水×12；每 30 分钟完成一批、共 90 分钟，落地 3 次各净水×4。</summary>
    [Test]
    public void Q380_A01_WaterStation_ThreeBatches_ThirtyMinutesEach()
    {
        var s = Workshop();
        WaterInputs(s, 3);
        var rc = Cfg.Recipes.Single(r => r.Id == Water);
        Assert.That(E.StationBatchMaxNew(s, rc), Is.EqualTo(3), "上限 = min(10−0, 材料够的 3 批)");
        var p = E.Preview(s, Craft(Water, 3));
        Assert.That(p.Outputs.Single(o => o.ItemId == "clean_water").Count, Is.EqualTo(12), "显示预估净水×12");
        Assert.That(p.Reason, Does.Contain("投料（3批）"));
        var t0 = s.GameMinute;
        Assert.That(E.StationNewFinishes(s, rc, 3), Is.EqualTo(new[] { t0 + 30, t0 + 60, t0 + 90 }), "各批预计完成首尾相接");

        Ok(E.StartStationBatch(s, Water, "a01", 3));
        Assert.That(s.GameMinute, Is.EqualTo(t0), "0 分钟");
        Assert.That(s.Main, Is.Null, "不占主行动");
        Assert.That((Reserved(s, "dirty_water"), Reserved(s, "filter_media")), Is.EqualTo((12, 3)));
        Assert.That(s.Items.Where(i => i.ReservedBy != null).All(i => i.Owner == s.Ground), Is.True, "预留移到设施所在区地面");
        var q = Queue(s, "water_station");
        Assert.That(q.Select(b => b.Queued), Is.EqualTo(new[] { false, true, true }), "首批加工中，其余排队");
        foreach (var b in q)
            Assert.That((s.Items.Where(i => i.ReservedBy == b.BatchId && i.ItemId == "dirty_water").Sum(i => i.Count),
                         s.Items.Where(i => i.ReservedBy == b.BatchId && i.ItemId == "filter_media").Sum(i => i.Count)), Is.EqualTo((4, 1)), "每份只归一批");
        Assert.That(E.StationWaitingHere(s, "water_station").Count, Is.EqualTo(2));
        Assert.That(s.Log.Last().Text, Does.Contain("投料（3批）"));

        AdvanceTo(s, t0 + 30);
        Assert.That(Sum(s, "clean_water", "camp"), Is.EqualTo(4));
        AdvanceTo(s, t0 + 60);
        Assert.That(Sum(s, "clean_water", "camp"), Is.EqualTo(8));
        AdvanceTo(s, t0 + 85);
        Assert.That(Sum(s, "clean_water", "camp"), Is.EqualTo(8), "第三批 90 分钟才完成");
        AdvanceTo(s, t0 + 90);
        Assert.That(Sum(s, "clean_water", "camp"), Is.EqualTo(12));
        Assert.That(Sum(s, "dirty_water") + Sum(s, "filter_media"), Is.EqualTo(0), "逐批核销");
        Assert.That(Queue(s, "water_station"), Is.Empty);
        var done = s.Log.Where(l => l.Kind == nameof(GameEventKind.FacilityDone) && l.Text.Contains("净水")).ToList();
        Assert.That(done.Count, Is.EqualTo(3), "落地 3 次");
        Assert.That(done.All(l => l.Text.Contains("净水×4")), Is.True, "每次净水×4");
    }

    /// <summary>Q380-A01：陶窑按每批实际原料、木炭与 60 分钟同法验证。</summary>
    [Test]
    public void Q380_A01_Kiln_ThreeBatches_SixtyMinutesEach_CharcoalPerBatch()
    {
        var s = Workshop();
        KilnInputs(s, 3);
        var t0 = s.GameMinute;
        Ok(E.StartStationBatch(s, Kiln, "a01k", 3));
        Assert.That((Reserved(s, "clay"), Reserved(s, "charcoal")), Is.EqualTo((6, 3)), "原料黏土 2×3、燃料木炭 1×3");
        AdvanceTo(s, t0 + 60);
        Assert.That((Sum(s, "ceramic_shell"), Sum(s, "clay"), Sum(s, "charcoal")), Is.EqualTo((2, 4, 2)));
        AdvanceTo(s, t0 + 120);
        Assert.That((Sum(s, "ceramic_shell"), Sum(s, "clay"), Sum(s, "charcoal")), Is.EqualTo((4, 2, 1)));
        AdvanceTo(s, t0 + 180);
        Assert.That((Sum(s, "ceramic_shell"), Sum(s, "clay"), Sum(s, "charcoal")), Is.EqualTo((6, 0, 0)));
    }

    // ───────────── Q380-A02 ─────────────

    /// <summary>Q380-A02：工位忙时投料加入队尾；当前批完成的同一段，下一批进度仍为 0。</summary>
    [Test]
    public void Q380_A02_BusyAppendsToTail_NextStartsNextSegment()
    {
        var s = Workshop();
        WaterInputs(s, 3);
        var t0 = s.GameMinute;
        Ok(E.StartStationBatch(s, Water, "x1"));
        AdvanceTo(s, t0 + 10);
        var head = E.StationBatchHere(s, "water_station")!;
        Assert.That(head.DoneMinutes, Is.EqualTo(10));
        Ok(E.StartStationBatch(s, Water, "x2", 2));
        var q = Queue(s, "water_station");
        Assert.That(q[0], Is.SameAs(head), "已有加工批不变");
        Assert.That(q.Skip(1).All(b => b.Queued && b.DoneMinutes == 0), Is.True, "新批排在队尾");
        Assert.That(q.Select(b => b.BatchId), Is.Ordered, "按提交顺序");

        AdvanceTo(s, t0 + 30);
        var next = E.StationBatchHere(s, "water_station")!;
        Assert.That(next.BatchId, Is.EqualTo(q[1].BatchId), "最早等候批进入加工中");
        Assert.That(next.DoneMinutes, Is.EqualTo(0), "同一段完成时下一批进度仍为 0");
        Assert.That(next.StartMinute, Is.EqualTo(s.GameMinute));
        AdvanceTo(s, t0 + 35);
        Assert.That(next.DoneMinutes, Is.EqualTo(5), "下一世界段开始推进");
        Assert.That(E.StationWaitingHere(s, "water_station").Single().DoneMinutes, Is.EqualTo(0), "每段只推进加工中的一批");
    }

    /// <summary>Q380-A02：余位不足或总料不足时整次拒绝（不预留、不改队列）；满 10 批显示「队列已满（10批）。」。</summary>
    [Test]
    public void Q380_A02_RoomOrMaterialShort_RejectsWholeRequest()
    {
        var s = Workshop();
        var rc = Cfg.Recipes.Single(r => r.Id == Water);
        WaterInputs(s, 3);
        E.CreateItems(s, "dirty_water", 4, s.Ground);   // 脏水够 4 批（16），滤材只够 3 批
        var before = SaveSerializer.Serialize(s);
        var r4 = E.StartStationBatch(s, Water, "m4", 4);
        Assert.That(r4.Ok, Is.False);
        Assert.That(r4.Reason, Does.Contain("缺少滤材（3/4"), "按实际原料缺口提示");
        Assert.That(SaveSerializer.Serialize(s), Is.EqualTo(before), "整次拒绝：不预留、不入队");
        Assert.That(E.StationBatchMaxNew(s, rc), Is.EqualTo(3));

        // 填到 8 批，余位 2：要 3 批整次拒绝
        WaterInputs(s, 10);
        Ok(E.StartStationBatch(s, Water, "f8", 8));
        Assert.That(E.StationQueueRoom(s, "water_station"), Is.EqualTo(2));
        Assert.That(E.StationBatchMaxNew(s, rc), Is.EqualTo(2), "上限 = min(10−8, 材料够的批数)");
        var reserved = Reserved(s, "dirty_water");
        var r3 = E.StartStationBatch(s, Water, "f3", 3);
        Assert.That(r3.Ok, Is.False);
        Assert.That((Reserved(s, "dirty_water"), Queue(s, "water_station").Count), Is.EqualTo((reserved, 8)), "余位不足整次拒绝");
        Ok(E.StartStationBatch(s, Water, "f2", 2));
        Assert.That(Queue(s, "water_station").Count, Is.EqualTo(10), "含加工中最多 10 批");
        Assert.That(E.StationBatchMaxNew(s, rc), Is.EqualTo(0));
        Assert.That(E.StationBatchBlocked(s, rc), Is.EqualTo("队列已满（10批）。"));
        Assert.That(E.StartStationBatch(s, Water, "f1").Reason, Is.EqualTo("队列已满（10批）。"));
    }

    // ───────────── Q380-A03 ─────────────

    /// <summary>Q380-A03：取消当前批只返该批预留且队首转为加工中；取消排队只返等待批预留；已完成批照原记录；重复请求只生效一次；所见批次变了提示重新选择。</summary>
    [Test]
    public void Q380_A03_CancelCurrent_CancelQueue_Idempotent_Bound()
    {
        var s = Workshop();
        WaterInputs(s, 4);
        var t0 = s.GameMinute;
        Ok(E.StartStationBatch(s, Water, "c-load", 4));
        Assert.That(E.StartStationBatch(s, Water, "c-load", 4).Duplicate, Is.True, "同一投料请求只生效一次");
        Assert.That(Queue(s, "water_station").Count, Is.EqualTo(4));

        AdvanceTo(s, t0 + 30);   // 第一批完成：净水×4、制造经验+3
        var xpAfterFirst = s.Skills["crafting"].Xp;
        Assert.That((Sum(s, "clean_water"), xpAfterFirst), Is.EqualTo((4, 3L)));
        AdvanceTo(s, t0 + 45);
        var cur = E.StationBatchHere(s, "water_station")!;
        Assert.That(cur.DoneMinutes, Is.EqualTo(15));
        var waiting = E.StationWaitingHere(s, "water_station").Select(b => b.BatchId).ToList();
        Assert.That(waiting.Count, Is.EqualTo(2));

        // 取消当前批：只返该批（脏水4、滤材1），队首转为加工中
        var reservedBefore = Reserved(s, "dirty_water");
        Ok(E.CancelStationBatch(s, "water_station", cur.BatchId, "c-cur"));
        Assert.That(Reserved(s, "dirty_water"), Is.EqualTo(reservedBefore - 4), "只释放该批预留");
        Assert.That(s.Items.Where(i => i.ItemId == "dirty_water" && i.ReservedBy == null).All(i => i.Owner == "camp"), Is.True, "退到本区地面");
        var head = E.StationBatchHere(s, "water_station")!;
        Assert.That((head.BatchId, head.DoneMinutes, head.Queued), Is.EqualTo((waiting[0], 0, false)), "队首立即转为加工中、不退时间");
        Assert.That(E.CancelStationBatch(s, "water_station", cur.BatchId, "c-cur").Duplicate, Is.True, "重复请求只生效一次");
        Assert.That(E.StationBatchHere(s, "water_station")!.BatchId, Is.EqualTo(waiting[0]));
        var stale = E.CancelStationBatch(s, "water_station", cur.BatchId, "c-cur-2");
        Assert.That((stale.Ok, stale.Reason), Is.EqualTo((false, GameEngine.StationQueueChanged)), "所见的那批已不是加工中批");

        // 取消排队：只返等待批，当前批继续
        var stillWaiting = E.StationWaitingHere(s, "water_station").Select(b => b.BatchId).ToList();
        Assert.That(stillWaiting, Is.EqualTo(new[] { waiting[1] }));
        var wrong = E.CancelStationQueue(s, "water_station", waiting, "q-wrong");
        Assert.That((wrong.Ok, wrong.Reason), Is.EqualTo((false, GameEngine.StationQueueChanged)), "等待名单已变");
        Ok(E.CancelStationQueue(s, "water_station", stillWaiting, "q-ok"));
        Assert.That(E.StationWaitingHere(s, "water_station"), Is.Empty);
        Assert.That(Reserved(s, "dirty_water"), Is.EqualTo(4), "只剩当前批的预留");
        Assert.That(E.CancelStationQueue(s, "water_station", stillWaiting, "q-ok").Duplicate, Is.True);
        Assert.That(s.Log.Last(l => l.Kind == nameof(GameEventKind.ActionCancelled)).Text, Does.Contain("排队中1批"));

        // 已完成批的产物与经验照原记录；当前批照常完成
        Assert.That((Sum(s, "clean_water"), s.Skills["crafting"].Xp), Is.EqualTo((4, xpAfterFirst)), "取消不给产物与经验，已完成的不动");
        AdvanceTo(s, s.GameMinute + 30);
        Assert.That(Sum(s, "clean_water"), Is.EqualTo(8));
        Assert.That(Queue(s, "water_station"), Is.Empty);
    }

    /// <summary>Q380-A03：完成时预留缺失只终止该批（释放剩余预留、无产物无经验），后续批照原顺序、只用自己绑定的预留。</summary>
    [Test]
    public void Q380_A03_ReservationLost_OnlyThatBatchEnds()
    {
        var s = Workshop();
        WaterInputs(s, 2);
        E.CreateItems(s, "dirty_water", 4, s.Ground);   // 未预留的余料，不被补取
        var t0 = s.GameMinute;
        Ok(E.StartStationBatch(s, Water, "lost", 2));
        var first = E.StationBatchHere(s, "water_station")!;
        s.Items.Remove(s.Items.First(i => i.ReservedBy == first.BatchId && i.ItemId == "filter_media"));
        AdvanceTo(s, t0 + 30);
        Assert.That(Sum(s, "clean_water"), Is.EqualTo(0), "失效批没有产物");
        Assert.That(s.Skills["crafting"].Xp, Is.EqualTo(0), "没有经验");
        Assert.That(s.Items.Any(i => i.ReservedBy == first.BatchId), Is.False, "剩余预留已释放");
        Assert.That(Sum(s, "dirty_water"), Is.EqualTo(12), "失效批的脏水退回地面");
        var second = E.StationBatchHere(s, "water_station")!;
        Assert.That(second.DoneMinutes, Is.EqualTo(0));
        AdvanceTo(s, t0 + 60);
        Assert.That(Sum(s, "clean_water"), Is.EqualTo(4), "后续批照原顺序完成");
        Assert.That(Sum(s, "dirty_water"), Is.EqualTo(8), "只核销自己绑定的预留，不补取余料");
    }

    // ───────────── Q380-A04 ─────────────

    /// <summary>Q380-A04：旧档（存档 44）的单批作为加工中批续原进度、等待队列为空；新档读回队列顺序与预留；读档不补进度。</summary>
    [Test]
    public void Q380_A04_LegacySingleBatch_And_QueueRoundTrip()
    {
        var s = Workshop();
        KilnInputs(s, 1);
        var t0 = s.GameMinute;
        Ok(E.StartStationBatch(s, Kiln));
        AdvanceTo(s, t0 + 25);
        var obj = JObject.Parse(SaveSerializer.Serialize(s));
        obj["SchemaVersion"] = "44";
        foreach (var b in obj.SelectTokens("$..StationBatches[*]").OfType<JObject>()) b.Remove("Queued");
        var old = SaveSerializer.Deserialize(obj.ToString(), Cfg);
        Assert.That(old.SchemaVersion, Is.EqualTo(GameState.CurrentSchema));
        var legacy = E.StationBatchHere(old, "kiln")!;
        Assert.That((legacy.Queued, legacy.DoneMinutes, legacy.XpSnapshot), Is.EqualTo((false, 25, 6)), "原进度、经验快照原样接入");
        Assert.That(E.StationWaitingHere(old, "kiln"), Is.Empty, "等待队列为空");
        Assert.That(old.Items.Where(i => i.ReservedBy == legacy.BatchId).Sum(i => i.Count), Is.EqualTo(3), "原料与燃料预留原样");
        AdvanceTo(old, t0 + 55);
        Assert.That(Sum(old, "ceramic_shell"), Is.EqualTo(0));
        AdvanceTo(old, t0 + 60);
        Assert.That(Sum(old, "ceramic_shell"), Is.EqualTo(2), "续原进度在 60 分钟完成");

        // 新档：队列顺序、排队标记、预留归属读回；读档本身不补进度
        var n = Workshop();
        WaterInputs(n, 3);
        Ok(E.StartStationBatch(n, Water, "rt", 3));
        AdvanceTo(n, n.GameMinute + 10);
        var ids = Queue(n, "water_station").Select(b => (b.BatchId, b.Queued, b.DoneMinutes)).ToList();
        var owners = n.Items.Where(i => i.ReservedBy != null).Select(i => (i.InstanceId, i.ReservedBy, i.Count)).OrderBy(x => x.InstanceId).ToList();
        var back = SaveSerializer.Deserialize(SaveSerializer.Serialize(n), Cfg);
        Assert.That(Queue(back, "water_station").Select(b => (b.BatchId, b.Queued, b.DoneMinutes)), Is.EqualTo(ids));
        Assert.That(back.Items.Where(i => i.ReservedBy != null).Select(i => (i.InstanceId, i.ReservedBy, i.Count)).OrderBy(x => x.InstanceId), Is.EqualTo(owners));
        Assert.That(back.GameMinute, Is.EqualTo(n.GameMinute), "读档不补时间与进度");
        AdvanceTo(back, back.GameMinute + 80);
        Assert.That(Sum(back, "clean_water"), Is.EqualTo(12));
    }

    /// <summary>Q380-A04：跨世界日按实际完成日核算经验；预览按队列和预计完成日分开显示。</summary>
    [Test]
    public void Q380_A04_CrossDay_XpByActualFinishDay()
    {
        var s = Workshop();
        var day = Clock.Day(s.GameMinute);
        AdvanceTo(s, day * Clock.MinutesPerDay - 90);   // 当日 22:30
        KilnInputs(s, 2);
        var rc = Cfg.Recipes.Single(r => r.Id == Kiln);
        var preview = E.StationBatchXpPreview(s, rc, 2);
        var parts = preview.Split('；');
        Assert.That(parts.Length, Is.EqualTo(2), preview);
        Assert.That(parts[0], Does.Contain("制造经验+6").And.Not.Contain("额度"), "今日完成一批");
        Assert.That(parts[1], Does.Contain("制造经验+6").And.Contain($"按完成日第{day + 1}日额度"), "次日完成一批");
        Assert.That(E.StationBatchXpPreview(s, rc, 1), Does.Not.Contain("；"));

        Ok(E.StartStationBatch(s, Kiln, "xd", 2));
        AdvanceTo(s, day * Clock.MinutesPerDay - 30);
        Assert.That((StationXpOn(s, day), StationXpOn(s, day + 1)), Is.EqualTo((6, 0)), "第一批当日完成，扣当日额度");
        AdvanceTo(s, day * Clock.MinutesPerDay + 30);
        Assert.That(StationXpOn(s, day + 1), Is.EqualTo(6), "第二批次日完成，扣次日额度（旧日计数按原规则清理）");
        Assert.That(s.Skills["crafting"].Xp, Is.EqualTo(12));
        Assert.That(Sum(s, "ceramic_shell"), Is.EqualTo(4));
    }

    /// <summary>预览扣掉同一投料者先完成的在途批次：已有排队批时新批按队列排到的完成日计算余额。</summary>
    [Test]
    public void Q380_XpPreview_CountsQueuedBatchesAhead()
    {
        var s = Workshop();
        KilnInputs(s, 3);
        var rc = Cfg.Recipes.Single(r => r.Id == Kiln);
        Assert.That(E.StationBatchXpPreview(s, rc, 2), Does.Contain("制造经验+12"), "同日两批合计");
        Ok(E.StartStationBatch(s, Kiln, "pa", 2));
        Assert.That(E.StationBatchXpPreview(s, rc, 1), Does.Contain("已满12"), "前面两批已占满当日 12");
        Assert.That(E.StationBatchXpNote(s, E.StationWaitingHere(s, "kiln").Single()), Does.Contain("+6"));
    }

    /// <summary>网页壳接线（源码检查）：「投料批数」「投料（{批数}批）」「加工中」「排队中{批数}批」「取消当前批」「取消排队（{批数}批）」，取消绑定所见批次。</summary>
    [Test]
    public void Q380_WebWiring()
    {
        var dir = TestContext.CurrentContext.TestDirectory;
        while (dir != null && !File.Exists(Path.Combine(dir, "Survival.sln"))) dir = Path.GetDirectoryName(dir);
        var razor = File.ReadAllText(Path.Combine(dir!, "Survival.Web", "Components", "CraftOverlay.razor"));
        Assert.That(razor, Does.Contain(">投料批数<"));
        Assert.That(razor, Does.Contain("投料（@(batch)批）"));
        Assert.That(razor, Does.Contain("<b>加工中</b>"));
        Assert.That(razor, Does.Contain("<b>排队中@(sbWait.Count)批</b>"));
        Assert.That(razor, Does.Contain(">取消当前批<"));
        Assert.That(razor, Does.Contain("取消排队（@(sbWait.Count)批）"));
        Assert.That(razor, Does.Contain("CancelStationBatch(G.S, stationId, batchId,"));
        Assert.That(razor, Does.Contain("CancelStationQueue(G.S, stationId, waitIds,"));
        Assert.That(razor, Does.Not.Contain("投料（被动）"));
    }

    // ───────────── Q397—Q399（S08「Q397–Q399 · B32e 文案定稿」） ─────────────

    /// <summary>Q397-A01：还有 2 个空位提交 3 批、提交 0 批、无等待批请求取消排队，分别显示定稿句；无空位与名单改变仍走 Q380 原提示。</summary>
    [Test]
    public void Q397_A01_FallbackTexts()
    {
        var s = Workshop();
        var rc = Cfg.Recipes.Single(r => r.Id == Water);
        WaterInputs(s, 11);
        Ok(E.StartStationBatch(s, Water, "q397-8", 8));
        Assert.That(E.StartStationBatch(s, Water, "q397-3", 3).Reason, Is.EqualTo("队列还可加入2批，请减少投料批数。"));
        Assert.That(E.StartStationBatch(s, Water, "q397-0", 0).Reason, Is.EqualTo("投料批数必须是1或更大的整数。"));
        Assert.That(E.StationBatchBlocked(s, rc, -1), Is.EqualTo("投料批数必须是1或更大的整数。"));
        Ok(E.StartStationBatch(s, Water, "q397-2", 2));
        Assert.That(E.StartStationBatch(s, Water, "q397-full", 0).Reason, Is.EqualTo("队列已满（10批）。"), "10 批满时沿用 Q380 句（校验顺序不变）");

        // 只有加工中的一批（无等待批）时请求取消排队
        var k = Workshop();
        KilnInputs(k, 1);
        Ok(E.StartStationBatch(k, Kiln, "k1"));
        var none = E.CancelStationQueue(k, "kiln", null, "k-q");
        Assert.That((none.Ok, none.Reason), Is.EqualTo((false, "当前区的陶窑没有可取消的排队批次。")));
        var w = E.CancelStationQueue(k, "water_station", null, "w-q");
        Assert.That(w.Reason, Is.EqualTo("当前区的固定净水台没有可取消的排队批次。"), "设施用实际设施名");
        var changed = E.CancelStationQueue(k, "kiln", new[] { 999 }, "k-q2");
        Assert.That(changed.Reason, Is.EqualTo(GameEngine.StationQueueChanged), "等待名单改变仍走 Q380 原提示");
    }

    /// <summary>Q398-A01：陶窑燃料分账块写「批数」（值为投料批数），固定净水台同字段同样；锻炉仍写「份数」。</summary>
    [Test]
    public void Q398_A01_FuelBlockBatchWord()
    {
        var dir = TestContext.CurrentContext.TestDirectory;
        while (dir != null && !File.Exists(Path.Combine(dir, "Survival.sln"))) dir = Path.GetDirectoryName(dir);
        var razor = File.ReadAllText(Path.Combine(dir!, "Survival.Web", "Components", "CraftOverlay.razor"));
        Assert.That(razor, Does.Contain("<span>@(stationRecipe ? \"批数\" : \"份数\")<b>@batch</b></span>"));
        Assert.That(razor, Does.Not.Contain("<span>份数<b>@batch</b></span>"));
        Assert.That(E.IsStationBatchRecipe(Cfg.Recipes.Single(r => r.Id == Kiln)), Is.True, "陶窑配方走「批数」");
        var forge = Cfg.Recipes.Where(r => r.Fuel.Count > 0 && r.Station == "forge").ToList();
        Assert.That(forge, Is.Not.Empty);
        Assert.That(forge.All(r => !E.IsStationBatchRecipe(r)), Is.True, "锻炉配方仍写「份数」");
    }

    /// <summary>Q399-A01：跨日多批的完成时刻按队列先后以「、」连成「预计…完成」一句；经验按完成日分别估算、同日合计、各日「；」分隔。</summary>
    [Test]
    public void Q399_A01_EtaAndXpFormat()
    {
        var s = Workshop();
        var day = Clock.Day(s.GameMinute);
        AdvanceTo(s, day * Clock.MinutesPerDay - 70);   // 当日 22:50
        var rc = Cfg.Recipes.Single(r => r.Id == Kiln);
        KilnInputs(s, 3);
        var t = s.GameMinute;
        Assert.That(E.StationEtaText(s, rc, 3),
            Is.EqualTo($"预计{Clock.DayTime(t + 60)}、{Clock.DayTime(t + 120)}、{Clock.DayTime(t + 180)}完成"));
        Assert.That(E.StationEtaText(s, rc, 2), Is.EqualTo($"预计第{day}日 23:50、第{day + 1}日 00:50完成"));
        var xp = E.StationBatchXpPreview(s, rc, 3).Split('；');
        Assert.That(xp.Length, Is.EqualTo(2), "两个完成日各一段");
        Assert.That(xp[0], Does.StartWith("完成后制造经验+6（"), "当日一批");
        Assert.That(xp[1], Does.StartWith("完成后制造经验+12（").And.EndWith($"按完成日第{day + 1}日额度"), "次日两批合计");
    }

    /// <summary>
    /// 试玩修复 M680（Q380-A03 重复请求只生效一次）：连点「取消当前批」不再连取消两批——连击第二下起（Detail&gt;1）不提交，
    /// 提交成功后三个队列按钮停用一段再随重渲恢复；请求号与所绑批次来自渲染时。另：队列区放进原料滚动区顶部、投料条留在卡底，矮屏整卡兜底可滚。
    /// </summary>
    [Test]
    public void Q380_M680_ClickGuard_And_QueueInsideScroll()
    {
        var dir = TestContext.CurrentContext.TestDirectory;
        while (dir != null && !File.Exists(Path.Combine(dir, "Survival.sln"))) dir = Path.GetDirectoryName(dir);
        var razor = File.ReadAllText(Path.Combine(dir!, "Survival.Web", "Components", "CraftOverlay.razor"));
        var css = File.ReadAllText(Path.Combine(dir!, "Survival.Web", "wwwroot", "css", "app.css"));
        Assert.That(razor, Does.Contain("ev.Detail <= 1 && !StationHeld"), "连击第二下起不提交");
        Assert.That(razor, Does.Contain("(MouseEventArgs ev) => CancelStation(rc.Station, curId, ev)"), "批号在渲染时绑定");
        Assert.That(razor, Does.Contain("(MouseEventArgs ev) => CancelStationQueue(rc.Station, waitIds, ev)"));
        Assert.That(razor, Does.Contain("(MouseEventArgs ev) => LoadStation(rc.Id, batch, loadKey, ev)"));
        Assert.That(razor.Split("disabled=\"@StationHeld\"").Length - 1, Is.EqualTo(2), "两个取消键提交后停用");
        Assert.That(razor, Does.Contain("disabled=\"@(sbWhy != null || StationHeld)\""));
        Assert.That(razor, Does.Contain("HoldStation(G.EngineDo(() => G.E.CancelStationBatch(G.S, stationId, batchId, $\"station-cancel:{batchId}\")))"));
        var scroll = razor.IndexOf("<div class=\"mat-scroll", StringComparison.Ordinal);
        var queue = razor.IndexOf("data-station-queue", StringComparison.Ordinal);
        var mats = razor.IndexOf("原料@(rc.Fuel.Count > 0 ? \"与燃料\" : \"\")", StringComparison.Ordinal);
        Assert.That(scroll < queue && queue < mats, Is.True, "队列区在原料滚动区顶部");
        Assert.That(razor.IndexOf("data-station-go", StringComparison.Ordinal), Is.GreaterThan(mats), "投料条在滚动区外、卡底");
        Assert.That(css, Does.Contain(".craft-side > .craft-mats.station-mats { overflow-y: auto; }"));
        Assert.That(css, Does.Contain(".sq-row { display: flex; align-items: flex-start;"));
    }
}
