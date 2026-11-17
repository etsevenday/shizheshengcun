using Newtonsoft.Json.Linq;
using Survival.Core.Config;
using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// B51b 变异测试补测试（2026-11-16，Stryker 471c30ab..26768a2b 存活变异；开发记录 B51b「变异测试（2026-11-16 补）」）：
/// Q703 阈值提醒资格与身体共同截止、Q380／Q701.2 可投料批数、设施批完工重验预留、存档 54→55 只保护本批预留、取消本批无 batchId 两句、在办旧批经验说明。
/// 期望值全部按策划案／Q 答复写字面量，不从引擎同一函数取。
/// </summary>
public class B51bMutationGapTests : TestBase
{
    private const string Water = "ex_station_water";
    private const string Kiln = "ex_ceramic_shell";

    protected override bool NoEnemyApproach => true;

    private void Ok(ActionResult r) => Assert.That(r.Ok, Is.True, r.Reason);

    private static int Sum(GameState s, string itemId) => s.Items.Where(i => i.ItemId == itemId).Sum(i => i.Count);

    private List<StationBatch> Batches(GameState s, string station) =>
        E.FacOf(s, s.Ground)!.StationBatches.Where(b => b.StationId == station).ToList();

    /// <summary>当前区（临时营地）建好陶窑与固定净水台，清空设施原料与产物，制造 3 级、经验 0（同 B51bTests）。</summary>
    private GameState Workshop()
    {
        var s = New();
        s.Skills["crafting"].Level = 3;
        s.Progress.Flags.Add("h03_ceramic_notice_read");
        foreach (var (id, n) in new[] { ("stone", 8), ("clay", 6), ("iron_plate", 2) }) E.CreateItems(s, id, n, s.Ground);
        Start(s, ActionRequest.Craft("build_kiln", null, null, 1));
        Run(s);
        foreach (var (id, n) in new[] { ("ceramic_shell", 4), ("plank", 2), ("cloth", 2) }) E.CreateItems(s, id, n, s.Ground);
        Start(s, ActionRequest.Craft("build_water_station", null, null, 1));
        Run(s);
        s.Items.RemoveAll(i => i.ItemId is "clay" or "charcoal" or "dirty_water" or "filter_media" or "ceramic_shell" or "clean_water");
        s.Skills["crafting"].Level = 3;
        s.Skills["crafting"].Xp = 0;
        return s;
    }

    // ───────────── 1. Q703：阈值提醒资格（起点谓词、归零类、恢复后重获资格、身体共同截止） ─────────────

    /// <summary>荒地白天单人探索；六项按 (健康, 饱食, 水分, 体力, 清醒, 精神) 设；clearLatch 时清掉锁存（起点已低但原锁存尚未置上，同 B51bTests.Q703_StartAlreadyLow）。</summary>
    private GameState FieldSolo(double h, double n, double w, double e, double sl, double m, bool clearLatch)
    {
        var s = New();
        E.DebugTeleport(s, "field");
        s.GameMinute = 480;
        SetStats(s, h, n, w, e, sl, m);
        if (clearLatch) { s.Player.AlertLatched.Clear(); s.Player.ZeroLatched.Clear(); }
        return s;
    }

    /// <summary>
    /// Q703（起点已在低值区不假报将越过）＋开发假设 B51b-6（归零类只在起点值 &gt; 0 时可触发）：饱食起点正好 0、锁存未置，
    /// 副本第 1 段会按原锁存收集到饱食归零与饱食提醒两条触发，但起点已在归零／低值一侧，预估不写“将越过”；真实世界照原锁存提醒并暂停。
    /// </summary>
    [Test]
    public void Q703_ZeroStat_StartAtZero_NotListed()
    {
        var s = FieldSolo(100, 0, 90, 90, 90, 60, clearLatch: true);
        var p = E.Preview(s, ActionRequest.Explore());
        Assert.That(p.Ok, Is.True, p.Reason);
        Assert.That(p.BodyAlerts, Is.Empty);
        Assert.That(p.AlertsExpected, Is.Empty);

        Start(s, ActionRequest.Explore());
        Run(s, autoResume: false);
        Assert.That(s.Log.Count(l => l.Text == "饱食归零：健康每小时减0.4，请尽快进食。"), Is.EqualTo(1), "真实世界照原锁存提醒（求生案 02 归零提醒原句，healthRules 饱食归零 −0.4/小时）");
        Assert.That(s.Main!.Status, Is.EqualTo(ActionStatus.Paused));
    }

    /// <summary>
    /// 配对：饱食起点 0.05（已低于提醒线 20，提醒类无资格；仍 &gt; 0，归零类有资格），探索饱食 −1.5/小时第 1 段归零——只凭归零类资格列一条“途中饱食将越过提醒阈值，行动会暂停”（单人原句，Q703）。
    /// </summary>
    [Test]
    public void Q703_ZeroStat_StartAboveZero_Listed()
    {
        var s = FieldSolo(100, 0.05, 90, 90, 90, 60, clearLatch: true);
        var p = E.Preview(s, ActionRequest.Explore());
        Assert.That(p.Ok, Is.True, p.Reason);
        Assert.That(p.BodyAlerts, Is.EqualTo(new[] { "途中饱食将越过提醒阈值，行动会暂停" }));
        Assert.That(p.AlertsExpected, Is.EqualTo(new[] { StatId.Satiety }));
    }

    /// <summary>
    /// Q703「真正恢复到原解除侧后再跨线仍按原资格」：营地棚内休息，健康起点 4.99（≤5 已在高危低值区，起点无资格）；
    /// 休息遮蔽自然恢复 +0.2/小时（healthRules，饱食水分≥40、精神≥20），第 1 段回到 5 以上（原解除侧）重获资格；
    /// 清醒 2 按休息 −4/小时第 6 段归零（暂停、列清醒度），此后归零扣健康 −0.4/小时、恢复 +0.2/小时，净 −0.2/小时，第 12 段再降到 5 以下——再次跨线照列。
    /// 真实世界同样在恢复后再跨线时提醒一次健康高危。
    /// </summary>
    [Test]
    public void Q703_RecoverAboveThenCrossAgain_Listed()
    {
        var s = New();
        s.Locations["camp"].Facility.ShelterBuilt = true;
        SetStats(s, 4.99, 80, 80, 18, 2, 60);
        var p = E.Preview(s, ActionRequest.Rest(120));
        Assert.That(p.Ok, Is.True, p.Reason);
        Assert.That(p.BodyCut, Is.False, "健康不会到 0，没有身体截断");
        Assert.That(p.BodyAlerts, Is.EqualTo(new[]
        {
            "途中清醒度将越过提醒阈值，行动会暂停",
            "途中健康将越过提醒阈值，行动会暂停",
        }));
        Assert.That(p.AlertsExpected, Is.EqualTo(new[] { StatId.Wakefulness, StatId.Health }));

        Start(s, ActionRequest.Rest(120));
        Run(s);
        Assert.That(s.Log.Count(l => l.Kind == nameof(GameEventKind.Alert) && l.Text == "健康降至5，处于高危状态。"), Is.EqualTo(1), "真实：恢复后再跨线提醒一次（阈值 health ≤5 含等号）");
    }

    /// <summary>
    /// Q703「只列可靠身体范围」／Q683 身体共同截止（含截止段）：荒地探索，饱食水分为 0、健康 0.3（−2/小时，第 2 段归零进入濒死＝截止 10 分钟）；
    /// 清醒 20.2（−4/小时）第 1 段跨线、体力 4.2（−1.6/小时，&lt;4 提醒）第 2 段即截止段跨线，都列；精神 20.09（−0.4/小时，&lt;20 提醒）第 3 段才跨线，在截止之后，不列。
    /// </summary>
    [Test]
    public void Q703_OnlyWithinBodyCut_IncludesCutSegment()
    {
        var s = FieldSolo(0.3, 0, 0, 4.2, 20.2, 20.09, clearLatch: false);
        var p = E.Preview(s, ActionRequest.Explore());
        Assert.That(p.Ok, Is.True, p.Reason);
        Assert.That(p.BodyCutoffMinutes, Is.EqualTo(10), "0.3 每段扣 1/6：第 2 段归零");
        Assert.That(p.BodyAlerts, Is.EqualTo(new[]
        {
            "途中清醒度将越过提醒阈值，行动会暂停",
            "途中体力将越过提醒阈值，行动会暂停",
        }));
        Assert.That(p.AlertsExpected, Is.EqualTo(new[] { StatId.Wakefulness, StatId.Energy }));
    }

    /// <summary>配对：同样起点但健康 20（没有危险截断），精神第 3 段跨线确实会暂停并列出——上一条不列精神只因为它在截止之后。</summary>
    [Test]
    public void Q703_NoBodyCut_LaterCrossingListed()
    {
        var s = FieldSolo(20, 0, 0, 4.2, 20.2, 20.09, clearLatch: false);
        var p = E.Preview(s, ActionRequest.Explore());
        Assert.That(p.Ok, Is.True, p.Reason);
        Assert.That(p.BodyCut, Is.False);
        Assert.That(p.BodyAlerts, Is.EqualTo(new[]
        {
            "途中清醒度将越过提醒阈值，行动会暂停",
            "途中体力将越过提醒阈值，行动会暂停",
            "途中精神将越过提醒阈值，行动会暂停",
        }));
    }

    // ───────────── 2. Q380／Q701.2：可投料批数 ─────────────

    /// <summary>
    /// Q380 处理结果（B32e）与 Q701.2：投料批数上限 = min(10 − 在办批数, 材料与燃料够的完整批数)；完整批数对每种原料／燃料取 可用份数 ÷ 每批用量 向下取整，再取各项最小。
    /// 净水台每批脏水×4、滤材×1（A07 ex_station_water）；陶窑每批黏土×2＋燃料木炭×1（A07 ex_ceramic_shell）。
    /// </summary>
    [Test]
    public void Q380_BatchMax_FloorPerInput_MinAcrossInputs()
    {
        var s = Workshop();
        var water = Cfg.Recipes.Single(r => r.Id == Water);
        var kiln = Cfg.Recipes.Single(r => r.Id == Kiln);

        E.CreateItems(s, "dirty_water", 9, s.Ground);
        E.CreateItems(s, "filter_media", 5, s.Ground);
        Assert.That(E.StationBatchMaxNew(s, water), Is.EqualTo(2), "脏水 9÷4 向下取整 2，滤材 5，取 2");

        E.CreateItems(s, "dirty_water", 11, s.Ground);   // 20 份
        s.Items.RemoveAll(i => i.ItemId == "filter_media");
        E.CreateItems(s, "filter_media", 3, s.Ground);
        Assert.That(E.StationBatchMaxNew(s, water), Is.EqualTo(3), "脏水 20÷4＝5，滤材 3，取 3");

        E.CreateItems(s, "clay", 7, s.Ground);
        E.CreateItems(s, "charcoal", 2, s.Ground);
        Assert.That(E.StationBatchMaxNew(s, kiln), Is.EqualTo(2), "黏土 7÷2＝3，燃料木炭 2÷1＝2，取 2");
    }

    /// <summary>Q380 上限式的另一端：没有原料／燃料约束的配方只受并行余位限制（10 − 在办批数）。现有配置无此类配方，用复制的净水台配方去掉原料。</summary>
    [Test]
    public void Q380_BatchMax_NoInputs_OnlyRoomLimits()
    {
        var s = Workshop();
        E.CreateItems(s, "dirty_water", 12, s.Ground);
        E.CreateItems(s, "filter_media", 3, s.Ground);
        Ok(E.StartStationBatch(s, Water, "room", 3));
        var src = Cfg.Recipes.Single(r => r.Id == Water);
        var free = new RecipeConfig { Id = "test_free_water", Name = src.Name, Kind = src.Kind, BaseMinutes = src.BaseMinutes, Station = src.Station, Mode = src.Mode };
        Assert.That(E.StationBatchMaxNew(s, free), Is.EqualTo(7), "10 − 在办 3");
    }

    // ───────────── 3. 完工重验预留：输入必须全部仍在设施所在区 ─────────────

    /// <summary>净水台一批投料（脏水注入 20 分钟截止，现有配置无期限，同 B54 Q791 用例）；返回 (状态, 投料分钟)。</summary>
    private (GameState s, int t0) WaterBatch()
    {
        var s = Workshop();
        foreach (var i in E.CreateItems(s, "dirty_water", 4, s.Ground)) i.ExpiresAt = s.GameMinute + 20;
        E.CreateItems(s, "filter_media", 1, s.Ground);
        var t0 = s.GameMinute;
        Ok(E.StartStationBatch(s, Water, "chk"));
        return (s, t0);
    }

    /// <summary>
    /// S08（B32e 节）「完成时重验该批的真实预留。缺料或预留失效时只终止该批……没有产物或经验」、S08／S11 Q791：本批预留输入有一份已不在设施所在区（其余仍在、份数仍够），
    /// 完工按原料不全失败：原失败句＋有 R 原料的「{原料名}×{数量}保质至{日期时刻}。」（C＋R），没有净水、没有制造经验，预留全部解开。
    /// </summary>
    [Test]
    public void S08_Complete_OneInputNotInPlace_FailsAsMissing()
    {
        var (s, t0) = WaterBatch();
        var filter = s.Items.Single(i => i.ItemId == "filter_media" && i.ReservedBy != null);
        filter.Owner = ItemInstance.Carried;   // 起点：本批一份输入已离开设施所在区，仍挂本批预留
        AdvanceTo(s, t0 + 35);
        var log = s.Log.Where(l => l.Text.Contains("原料不全")).ToList();
        Assert.That(log.Count, Is.EqualTo(1));
        var zone = E.ZoneName(s, s.Ground);
        // BM11 改断言出处：S08 文首 2026-11-17 §Q882 用户新裁定（返料不附恢复保质说明）
        Assert.That(log[0].Text, Is.EqualTo($"{zone}固定净水台：原料不全，这一批没有产物，剩下的原料已解锁。"));
        Assert.That(Sum(s, "clean_water"), Is.EqualTo(0), "无产物");
        Assert.That(s.Skills["crafting"].Xp, Is.EqualTo(0), "无经验");
        Assert.That(s.Items.Where(i => i.ItemId is "dirty_water" or "filter_media").All(i => i.ReservedBy == null), Is.True, "预留解开");
        Assert.That(Batches(s, "water_station"), Is.Empty);
    }

    /// <summary>配对：输入全部仍在原地时同一批按时完成，出净水×4（A07 ex_station_water）、不出原料不全句。</summary>
    [Test]
    public void S08_Complete_AllInputsInPlace_Succeeds()
    {
        var (s, t0) = WaterBatch();
        AdvanceTo(s, t0 + 35);
        Assert.That(Sum(s, "clean_water"), Is.EqualTo(4));
        Assert.That(s.Log.Any(l => l.Text.Contains("原料不全")), Is.False);
    }

    // ───────────── 4. 存档 54→55：只保护本批预留输入 ─────────────

    /// <summary>
    /// Q701-A09（S08「本批生产兼容、时间与验收」、production-and-forecast-q701-q703.json migration）：迁移只对各批真实预留（ReservedBy＝批次 ID）的未到期有期限输入记 R＝旧E−U；
    /// 同区未投入的同种原料与随身有期限物品不受影响（Q701-A02「未投入余料照常计时」）：不记 R、原截止不变。
    /// 注：无关物品若被误记 R，读档末尾的孤立保护收束（SweepOrphanShelf，Q701.1）会按 U＋R＝原截止恢复，所以本条抓“误保护”靠的是本批输入那条断言
    /// （单批时把筛选改成 != 本批，本批输入就不记 R）；无关物品两条断言是迁移＋收束合起来的结果。
    /// </summary>
    [Test]
    public void Migrate54To55_OnlyBatchInputsPaused_UnrelatedUntouched()
    {
        var s = Workshop();
        foreach (var i in E.CreateItems(s, "dirty_water", 4, s.Ground)) i.ExpiresAt = s.GameMinute + 100;
        E.CreateItems(s, "filter_media", 1, s.Ground);
        var t0 = s.GameMinute;
        Ok(E.StartStationBatch(s, Water, "mig"));
        foreach (var i in E.CreateItems(s, "dirty_water", 2, s.Ground)) i.ExpiresAt = t0 + 100;   // 未投入余料
        foreach (var i in E.CreateItems(s, "herb", 1, ItemInstance.Carried)) i.ExpiresAt = t0 + 300;   // 随身无关物
        AdvanceTo(s, t0 + 20);
        foreach (var i in s.Items.Where(i => i.ReservedBy != null)) i.ShelfPausedMinutes = null;   // 54 旧档设施批输入没有暂停保质（原截止 E 保留）
        Assert.That(s.Items.Count(i => i.ShelfPausedMinutes != null), Is.EqualTo(0));
        var obj = JObject.Parse(SaveSerializer.Serialize(s));
        obj["SchemaVersion"] = "54";
        var old = SaveSerializer.Deserialize(obj.ToString(), Cfg);
        Assert.That(old.SchemaVersion, Is.EqualTo("56"));   // B53a 改期望：存档升 56（55→56 晾制旧停止位判因，S11 文首 2026-11-15 §Q764 选 A）
        Assert.That(old.GameMinute, Is.EqualTo(t0 + 20));

        var b = Batches(old, "water_station").Single();
        var bound = old.Items.Where(i => i.ReservedBy == b.BatchId && i.ItemId == "dirty_water").ToList();
        Assert.That(bound.Sum(i => i.Count), Is.EqualTo(4));
        Assert.That(bound.All(i => i.ShelfPausedMinutes == 80), Is.True, "R＝旧E−U＝100−20");
        var spare = old.Items.Where(i => i.ReservedBy == null && i.ItemId == "dirty_water").ToList();
        Assert.That(spare.Sum(i => i.Count), Is.EqualTo(2));
        Assert.That(spare.All(i => i.ShelfPausedMinutes == null && i.ExpiresAt == t0 + 100), Is.True, "未投入余料不保护、原截止不变");
        var herb = old.Items.Where(i => i.ItemId == "herb" && i.Owner == ItemInstance.Carried && i.ExpiresAt == t0 + 300).ToList();
        Assert.That(herb.Count, Is.EqualTo(1));
        Assert.That(herb[0].ShelfPausedMinutes, Is.Null, "随身无关物不保护");
    }

    // ───────────── 5. Q721：取消本批不带 batchId 的两句 ─────────────

    /// <summary>S08 Q701.4／逐字稿 Q701 表 + Q721 答复：没有带 batchId 的请求，当前区这座没有在办批时「当前区的{设施名}没有进行中的加工批次。」。</summary>
    [Test]
    public void Q721_CancelWithoutBatchId_NoBatch_EmptyText()
    {
        var s = Workshop();
        var r = E.CancelStationBatch(s, "water_station", null, "none");
        Assert.That(r.Ok, Is.False);
        Assert.That(r.Reason, Is.EqualTo("当前区的固定净水台没有进行中的加工批次。"));
    }

    /// <summary>逐字稿 B51 表 changedBatch ＋ Q721（取消只认所点 batchId）：有在办批但请求没带 batchId 时「加工批次已变化，请重新选择。」，不改指任何一批，批次与预留不动。</summary>
    [Test]
    public void Q721_CancelWithoutBatchId_WithBatch_ChangedText()
    {
        var s = Workshop();
        E.CreateItems(s, "dirty_water", 4, s.Ground);
        E.CreateItems(s, "filter_media", 1, s.Ground);
        Ok(E.StartStationBatch(s, Water, "one"));
        var r = E.CancelStationBatch(s, "water_station", null, "nil");
        Assert.That(r.Ok, Is.False);
        Assert.That(r.Reason, Is.EqualTo("加工批次已变化，请重新选择。"));
        var b = Batches(s, "water_station");
        Assert.That(b.Count, Is.EqualTo(1));
        Assert.That(s.Items.Where(i => i.ReservedBy == b[0].BatchId).Sum(i => i.Count), Is.EqualTo(5), "脏水 4＋滤材 1 仍归本批");
    }

    // ───────────── 附：在办旧批（经验快照 0）的经验说明 ─────────────

    /// <summary>
    /// S08／S11 B22d 实现同步（Q174）：本规则上线前已投料的在办批经验快照读出为 0，按原规则不给经验完成；在办批经验说明写
    /// 「这一批开始时被动加工不给经验（按原规则完成）」（B22d 开发方原句，区别于新投料预览的「被动加工不给经验」）。
    /// 配对：快照 3（Q174 净水台每批 3）的在办批写「完成后制造经验+3（陶窑与净水台被动批次合计每日最多12）」。
    /// </summary>
    [Test]
    public void Q174_InFlightZeroSnapshot_XpNote()
    {
        var s = Workshop();
        E.CreateItems(s, "dirty_water", 8, s.Ground);
        E.CreateItems(s, "filter_media", 2, s.Ground);
        Ok(E.StartStationBatch(s, Water, "xp", 2));
        var q = Batches(s, "water_station");
        Assert.That(q.Count, Is.EqualTo(2));
        q[0].XpSnapshot = 0;   // 起点：旧档在办批
        Assert.That(E.StationBatchXpNote(s, q[0]), Is.EqualTo("这一批开始时被动加工不给经验（按原规则完成）"));
        Assert.That(E.StationBatchXpNote(s, q[1]), Is.EqualTo("完成后制造经验+3（陶窑与净水台被动批次合计每日最多12）"));
    }
}
