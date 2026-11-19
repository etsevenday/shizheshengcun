using Newtonsoft.Json.Linq;
using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 批次 B51c2a1「被动设施取出 1：统一门槛＋陶窑／净水台（存档 57）」：
/// S11「2026-11-17 B51c2、B47b与B52b三个完整原批」Q870 B／Q871 B／Q872 B，「B51c次要原批Q873—Q881」Q876 B／Q878／Q879 A／Q880，
/// Q779 B，Q921（燃料不入清单）、Q923 B；S14 FAC-Q870-01、FAC-Q871-01／02、FAC-Q872-01／02、B51-Q778-02—06、B51-Q779-01、FACF-Q921-01（部分）。
/// 期望字面量均注明出处；起点只用 Debug／直接改状态构造，不用来造结论。
/// </summary>
public class B51c2a1Tests : TestBase
{
    private const string Water = "ex_station_water";
    private const string Kiln = "ex_ceramic_shell";

    protected override bool NoEnemyApproach => true;

    private void Ok(ActionResult r) => Assert.That(r.Ok, Is.True, r.Reason);

    private static int Sum(GameState s, string itemId) => s.Items.Where(i => i.ItemId == itemId).Sum(i => i.Count);

    private static int GroundSum(GameState s, string itemId) => s.Items.Where(i => i.ItemId == itemId && i.Owner == s.Ground).Sum(i => i.Count);

    private List<StationBatch> Batches(GameState s, string station) =>
        E.FacOf(s, s.Ground)!.StationBatches.Where(b => b.StationId == station).ToList();

    private static string Root()
    {
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "web", "Survival.sln"))) dir = dir.Parent;
        Assert.That(dir, Is.Not.Null, "找到仓库根目录");
        return dir!.FullName;
    }

    /// <summary>临时营地建好陶窑与固定净水台（同 B51b 起点），清掉相关原料与产物，经验清零。</summary>
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

    /// <summary>给当前地面放 batches 批净水原料（配置 ex_station_water：脏水20＋滤材1；B56 水每份100毫升，原脏水4份×500毫升＝20份×100毫升）。</summary>
    private void WaterInputs(GameState s, int batches)
    {
        E.CreateItems(s, "dirty_water", 20 * batches, s.Ground);   // B56：每份100毫升，×5 保持原 2000 毫升／批
        E.CreateItems(s, "filter_media", batches, s.Ground);
    }

    /// <summary>给当前地面放 batches 批陶壳原料（配置 ex_ceramic_shell：黏土2，燃料木炭1）。</summary>
    private void KilnInputs(GameState s, int batches)
    {
        E.CreateItems(s, "clay", 2 * batches, s.Ground);
        E.CreateItems(s, "charcoal", batches, s.Ground);
    }

    private ActionResult TakeBatch(GameState s, StationBatch b, string? req = null) =>
        E.TakeStationBatch(s, s.Ground, b.StationId, b.BatchId, E.StationTakeStamp(s, s.Ground, b.StationId, b.BatchId), req);

    private ActionResult TakeAllNow(GameState s, string station, string? req = null) =>
        E.TakeAll(s, s.Ground, station, E.StationTakeStamp(s, s.Ground, station), req);

    // ───────────── Q872 B：原料真入设施、燃料仍地面预留（Q921 过渡） ─────────────

    /// <summary>
    /// FAC-Q872-01：陶窑投料后黏土真实归属设施该批（不在地面、不作地面来源），燃料木炭仍在地面按本批预留（Q921 燃料库存待 B35a2a）；
    /// 未投入的同名余料仍是地面来源。
    /// </summary>
    [Test]
    public void Q872_KilnInputs_InStation_FuelStaysGroundReserved()
    {
        var s = Workshop();
        KilnInputs(s, 1);
        E.CreateItems(s, "clay", 1, s.Ground);   // 未投入的余料
        Ok(E.StartStationBatch(s, Kiln, "k1"));
        var b = Batches(s, "kiln").Single();
        var owner = ItemInstance.StationOwner(s.Ground, b.BatchId);
        var inStation = s.Items.Where(i => i.Owner == owner).ToList();
        Assert.That(inStation, Has.Count.EqualTo(1));
        Assert.That((inStation[0].ItemId, inStation[0].Count, inStation[0].ReservedBy), Is.EqualTo(("clay", 2, (int?)null)), "配方黏土2移进设施该批，无地面预留标记");
        Assert.That(E.StationBatchInputs(s, s.Ground, b).Select(i => (i.ItemId, i.Count)), Is.EqualTo(new[] { ("clay", 2) }));
        Assert.That(GroundSum(s, "clay"), Is.EqualTo(1), "地面只剩未投入的1份");
        Assert.That(E.AvailableCount(s, "clay", true), Is.EqualTo(1), "设施内黏土不作地面来源");
        var fuel = s.Items.Where(i => i.ItemId == "charcoal").ToList();
        Assert.That(fuel, Has.Count.EqualTo(1));
        Assert.That((fuel[0].Owner, fuel[0].ReservedBy, fuel[0].Count), Is.EqualTo((s.Ground, (int?)b.BatchId, 1)), "燃料木炭1仍在地面、按本批预留（Q921 过渡）");
    }

    /// <summary>Q1007 A（暂用）：设施批内原料的位置文字「{区域}／{设施名}」，不带批次序号；放回地面后回到地面位置文字。</summary>
    [Test]
    public void Q1007_WherePath_StationInput()
    {
        var s = Workshop();
        WaterInputs(s, 1);
        Ok(E.StartStationBatch(s, Water, "w"));
        var b = Batches(s, "water_station").Single();
        var dirty = E.StationBatchInputs(s, s.Ground, b).Single(i => i.ItemId == "dirty_water");
        Assert.That(E.WherePath(s, dirty), Is.EqualTo("临时营地／固定净水台"));   // 02 Q1007 A：{区域}／{设施名}（区名 camp＝临时营地，设施名 water_station＝固定净水台）
        Ok(TakeBatch(s, b));
        Assert.That(E.WherePath(s, dirty), Is.Not.EqualTo("临时营地／固定净水台"), "取出后不再是设施位置");
    }

    // ───────────── Q870 B：每批取出 ─────────────

    /// <summary>
    /// FAC-Q870-01、B51-Q778-02／06：一批「取出」只结束所点的这一批，批内原料回设施所在区地面并解锁；其他批进度不动；
    /// 0 分钟、不开行动、无产物无经验；结果「已取出至{地点}地面：{清单}。」（Q876 B），不接中止／成本尾句（Q878）与保质句（Q882）。
    /// </summary>
    [Test]
    public void Q870_TakeOneBatch_OnlyThatBatch()
    {
        var s = Workshop();
        WaterInputs(s, 3);
        var t0 = s.GameMinute;
        Ok(E.StartStationBatch(s, Water, "w1"));
        AdvanceTo(s, t0 + 10);
        Ok(E.StartStationBatch(s, Water, "w2", 2));
        var q = Batches(s, "water_station");
        Assert.That(q.Select(b => b.DoneMinutes), Is.EqualTo(new[] { 10, 0, 0 }));
        var keep = new[] { q[0].BatchId, q[2].BatchId };
        var now = s.GameMinute;
        Ok(TakeBatch(s, q[1], "t1"));
        Assert.That(s.GameMinute, Is.EqualTo(now), "0 分钟");
        Assert.That(s.Main, Is.Null, "不开行动");
        var left = Batches(s, "water_station");
        Assert.That(left.Select(b => (b.BatchId, b.DoneMinutes)), Is.EqualTo(new[] { (keep[0], 10), (keep[1], 0) }), "其他批不动、进度保留");
        Assert.That(BatchBound(s, q[1].BatchId), Is.Empty, "该批原料已全部离开设施");
        Assert.That((GroundSum(s, "dirty_water"), GroundSum(s, "filter_media")), Is.EqualTo((20, 1)), "配方脏水20＋滤材1回地面");   // B56 改期望（配置 ex_station_water 脏水20＋滤材1→净水20，M1079；b56-q973-q974 契约配方表×5）：原 (4, 1)
        Assert.That(s.Items.Where(i => i.Owner == s.Ground && i.ItemId is "dirty_water" or "filter_media").All(i => i.ReservedBy == null), Is.True, "回地面后解锁");
        Assert.That(Sum(s, "clean_water"), Is.EqualTo(0), "没有产物");
        Assert.That(s.Skills["crafting"].Xp, Is.EqualTo(0), "没有经验");
        // S11 Q876 B：「已取出至{地点}地面：{物品清单}。」名×数、顿号；Q878 不接「已中止…」尾句；Q882 不接保质句
        Assert.That(s.Log.Last().Text, Is.EqualTo("已取出至临时营地地面：脏水×20、滤材×1。"));   // B56 改期望（配置 ex_station_water 脏水20＋滤材1→净水20，M1079；b56-q973-q974 契约配方表×5）：原「脏水×4」

        AdvanceTo(s, t0 + 40);
        Assert.That(Sum(s, "clean_water"), Is.EqualTo(40), "其余两批照常完成（配方净水20×2）");   // B56 改期望（配置 ex_station_water 脏水20＋滤材1→净水20，M1079；b56-q973-q974 契约配方表×5）：原 8
    }

    /// <summary>
    /// Q921（燃料仍地面预留，FACF-Q921-01 只部分满足）：陶窑一批取出时黏土回地面并列进清单；木炭只解锁、原地不动（同一实例）、不进清单。
    /// </summary>
    [Test]
    public void Q921_KilnTake_FuelOnlyUnlocked_NotListed()
    {
        var s = Workshop();
        KilnInputs(s, 1);
        Ok(E.StartStationBatch(s, Kiln, "k"));
        var b = Batches(s, "kiln").Single();
        var fuel = s.Items.Single(i => i.ItemId == "charcoal");
        Assert.That(fuel.ReservedBy, Is.EqualTo(b.BatchId), "起点：燃料按本批预留");
        var fuelId = fuel.InstanceId;
        Ok(TakeBatch(s, b));
        var after = s.Items.Where(i => i.ItemId == "charcoal").ToList();
        Assert.That(after, Has.Count.EqualTo(1));
        Assert.That((after[0].InstanceId, after[0].Owner, after[0].ReservedBy, after[0].Count), Is.EqualTo((fuelId, s.Ground, (int?)null, 1)), "燃料只解锁、原地不动");
        Assert.That(GroundSum(s, "clay"), Is.EqualTo(2));
        Assert.That(s.Log.Last().Text, Is.EqualTo("已取出至临时营地地面：黏土×2。"), "S11 Q876 B 清单；Q921 燃料不入清单");
    }

    /// <summary>B51-Q778-04：批已结束后再用旧戳取这一批，拒「物品已变，请重选。」（S11 Q880 沿原句），什么都不动。</summary>
    [Test]
    public void Q870_TakeBatch_Gone_Changed()
    {
        var s = Workshop();
        WaterInputs(s, 2);
        Ok(E.StartStationBatch(s, Water, "w", 2));
        var q = Batches(s, "water_station");
        var stamp = E.StationTakeStamp(s, s.Ground, "water_station", q[0].BatchId);
        Ok(E.TakeStationBatch(s, s.Ground, "water_station", q[0].BatchId, stamp, "a"));
        var logs = s.Log.Count;
        var r = E.TakeStationBatch(s, s.Ground, "water_station", q[0].BatchId, stamp, "b");
        Assert.That((r.Ok, r.Reason), Is.EqualTo((false, "物品已变，请重选。")));
        Assert.That(E.StationBatchTakeBlocked(s, s.Ground, "water_station", q[0].BatchId), Is.EqualTo("物品已变，请重选。"));
        Assert.That(Batches(s, "water_station").Select(b => b.BatchId), Is.EqualTo(new[] { q[1].BatchId }), "另一批不动");
        Assert.That(s.Log.Count, Is.EqualTo(logs));
        Assert.That(E.StationBatchTakeBlocked(s, s.Ground, "water_station", q[1].BatchId), Is.Null, "另一批仍可取出");
    }

    // ───────────── Q778：全部取出 ─────────────

    /// <summary>
    /// B51-Q778-02／06：「全部取出」把这座设施各批原料一次放回地面，同名合量（Q876 B），各批同一事务结束；别的设施不动；0 分钟、无产物无经验。
    /// </summary>
    [Test]
    public void Q778_TakeAll_AllBatches_MergedList()
    {
        var s = Workshop();
        WaterInputs(s, 2);
        KilnInputs(s, 1);
        var t0 = s.GameMinute;
        Ok(E.StartStationBatch(s, Water, "w", 2));
        Ok(E.StartStationBatch(s, Kiln, "k"));
        AdvanceTo(s, t0 + 20);
        Assert.That(E.TakeAllBlocked(s, s.Ground, "water_station"), Is.Null);
        var now = s.GameMinute;
        Ok(TakeAllNow(s, "water_station", "all"));
        Assert.That(s.GameMinute, Is.EqualTo(now), "0 分钟");
        Assert.That(Batches(s, "water_station"), Is.Empty);
        Assert.That(Batches(s, "kiln").Select(b => b.DoneMinutes), Is.EqualTo(new[] { 20 }), "陶窑那批不动");
        Assert.That((GroundSum(s, "dirty_water"), GroundSum(s, "filter_media")), Is.EqualTo((40, 2)));   // B56 改期望（配置 ex_station_water 脏水20＋滤材1→净水20，M1079；b56-q973-q974 契约配方表×5）：原 (8, 2)
        Assert.That(s.Items.Where(i => i.Owner == s.Ground && i.ItemId == "dirty_water").ToList(), Has.Count.EqualTo(1), "回地面后合堆");
        Assert.That((Sum(s, "clean_water"), s.Skills["crafting"].Xp), Is.EqualTo((0, 0)));
        // S11 Q876 B：同名按真实移出量合为「名×数」，顿号连接
        Assert.That(s.Log.Last().Text, Is.EqualTo("已取出至临时营地地面：脏水×40、滤材×2。"));   // B56 改期望（配置 ex_station_water 脏水20＋滤材1→净水20，M1079；b56-q973-q974 契约配方表×5）：原「脏水×8」
        Assert.That(E.TakeAllBlocked(s, s.Ground, "water_station"), Is.EqualTo("没有可取出的物品。"), "取完后空了");
    }

    /// <summary>逐字稿 B51 表：设施里没有可取出的物品时「全部取出」拒「没有可取出的物品。」，不写日志；有批时可取（配对）。</summary>
    [Test]
    public void Q778_TakeAll_Empty()
    {
        var s = Workshop();
        Assert.That(E.TakeAllBlocked(s, s.Ground, "kiln"), Is.EqualTo("没有可取出的物品。"));
        var logs = s.Log.Count;
        var r = TakeAllNow(s, "kiln");
        Assert.That((r.Ok, r.Reason), Is.EqualTo((false, "没有可取出的物品。")));
        Assert.That(s.Log.Count, Is.EqualTo(logs));
        KilnInputs(s, 1);
        Ok(E.StartStationBatch(s, Kiln, "k"));
        Assert.That(E.TakeAllBlocked(s, s.Ground, "kiln"), Is.Null);
    }

    /// <summary>B51-Q778-04：渲染后设施计划变了（新投一批），旧戳整次拒「物品已变，请重选。」、一样不动；重取戳后成功（配对）。</summary>
    [Test]
    public void Q778_TakeAll_StaleStamp_Rejected()
    {
        var s = Workshop();
        WaterInputs(s, 2);
        Ok(E.StartStationBatch(s, Water, "w1"));
        var stamp = E.StationTakeStamp(s, s.Ground, "water_station");
        Ok(E.StartStationBatch(s, Water, "w2"));
        var r = E.TakeAll(s, s.Ground, "water_station", stamp, "old");
        Assert.That((r.Ok, r.Reason), Is.EqualTo((false, "物品已变，请重选。")));
        Assert.That(Batches(s, "water_station"), Has.Count.EqualTo(2));
        Assert.That(GroundSum(s, "dirty_water"), Is.EqualTo(0), "一样不动");
        Ok(TakeAllNow(s, "water_station", "new"));
        Assert.That(GroundSum(s, "dirty_water"), Is.EqualTo(40));   // B56 改期望（配置 ex_station_water 脏水20＋滤材1→净水20，M1079；b56-q973-q974 契约配方表×5）：原 8
    }

    /// <summary>B51-Q778-04：同一请求号只生效一次——重复提交返回重复、不再动别的批。</summary>
    [Test]
    public void Q778_TakeAll_DuplicateRequest_Once()
    {
        var s = Workshop();
        WaterInputs(s, 2);
        Ok(E.StartStationBatch(s, Water, "w1"));
        var r1 = TakeAllNow(s, "water_station", "req");
        Assert.That((r1.Ok, r1.Duplicate), Is.EqualTo((true, false)));
        Ok(E.StartStationBatch(s, Water, "w2"));
        var logs = s.Log.Count;
        var r2 = TakeAllNow(s, "water_station", "req");
        Assert.That((r2.Ok, r2.Duplicate), Is.EqualTo((true, true)));
        Assert.That(Batches(s, "water_station"), Has.Count.EqualTo(1), "新投的一批不被重复请求取走");
        Assert.That(s.Log.Count, Is.EqualTo(logs));
    }

    // ───────────── Q871 B：统一门槛 ─────────────

    /// <summary>
    /// FAC-Q871-01／02：门槛依次 死亡「本局已结束」→ 对白「请先读完对话」→ 战斗「战斗时不能操作」（BM5a Q563 原句）→
    /// 角色主／子行动进行或暂停「行动进行中，暂不能整理物品」（Q871 B 沿原句）→ 不在当地「异地不能取用设施里的物品」（沿火堆原句）；都没有时可取。
    /// </summary>
    [Test]
    public void Q871_Gate_Order()
    {
        var s = Workshop();
        var here = s.Ground;
        Assert.That(E.StationTakeBlocked(s, here), Is.Null, "都没有时可取");
        Assert.That(E.StationTakeBlocked(s, "h02_square"), Is.EqualTo("异地不能取用设施里的物品"));
        Start(s, ActionRequest.Wait(30));
        Assert.That(E.StationTakeBlocked(s, "h02_square"), Is.EqualTo("行动进行中，暂不能整理物品"), "忙碌先于异地");
        s.Main!.Status = ActionStatus.Paused;
        Assert.That(E.StationTakeBlocked(s, here), Is.EqualTo("行动进行中，暂不能整理物品"), "暂停也算");
        s.Battle = new BattleState();
        Assert.That(E.StationTakeBlocked(s, here), Is.EqualTo("战斗时不能操作"), "战斗先于忙碌");
        s.Story.DialogueLog.Add(new DialogueSession { Id = 9901, EventId = "b51c2a1_test" });
        Assert.That(E.StationTakeBlocked(s, here), Is.EqualTo("请先读完对话"), "对白先于战斗");
        s.Progress.Dead = true;
        Assert.That(E.StationTakeBlocked(s, here), Is.EqualTo("本局已结束"), "死亡最先");
    }

    /// <summary>FAC-Q871-01：只有子行动时也拒；设施自己的被动加工不算角色忙（有在办批、无角色行动时可取，配对）。</summary>
    [Test]
    public void Q871_SubAction_Blocks_PassiveBatchDoesNot()
    {
        var s = Workshop();
        WaterInputs(s, 1);
        Ok(E.StartStationBatch(s, Water, "w"));
        var b = Batches(s, "water_station").Single();
        Assert.That(E.StationBatchTakeBlocked(s, s.Ground, "water_station", b.BatchId), Is.Null, "设施被动加工不算角色忙");
        s.Sub = new ActionState { IsSub = true };
        Assert.That(E.StationBatchTakeBlocked(s, s.Ground, "water_station", b.BatchId), Is.EqualTo("行动进行中，暂不能整理物品"));
        var r = TakeBatch(s, b);
        Assert.That((r.Ok, r.Reason), Is.EqualTo((false, "行动进行中，暂不能整理物品")));
        Assert.That(E.TakeAllBlocked(s, s.Ground, "water_station"), Is.EqualTo("行动进行中，暂不能整理物品"));
        Assert.That(BatchBound(s, b.BatchId).Sum(i => i.Count), Is.EqualTo(21), "被拒时原料留在设施");   // B56 改期望（配置 ex_station_water 脏水20＋滤材1→净水20，M1079；b56-q973-q974 契约配方表×5）：原 5（脏水4＋滤材1）
        s.Sub = null;
        Ok(TakeBatch(s, b));
    }

    // ───────────── Q779 B ─────────────

    /// <summary>B51-Q779-01：显示「各批同时加工，每座最多10批。」时标题只写「加工中{批数}批」；没有说明的分支保留上限。</summary>
    [Test]
    public void Q779_Title()
    {
        Assert.That(GameEngine.StationParallelHelp, Is.EqualTo("各批同时加工，每座最多10批。"));   // S08 Q727 短说明
        Assert.That(GameEngine.StationTitle(3, true), Is.EqualTo("加工中3批"));                       // S11 Q779 B
        Assert.That(GameEngine.StationTitle(3, false), Is.EqualTo("加工中3批（最多10批）"));          // S08 Q701.4 原标题
    }

    // ───────────── 完成与失败 ─────────────

    /// <summary>
    /// 原料不全仍按原失败句（S08 B51「原料不全」沿原句）：缺一样时这一批没有产物，设施里剩下的原料回本区地面并解锁；
    /// 原料齐全时照常核销、设施里不留东西（配对）。
    /// </summary>
    [Test]
    public void Complete_MissingInput_ReturnsRestToGround()
    {
        var s = Workshop();
        WaterInputs(s, 2);
        var t0 = s.GameMinute;
        Ok(E.StartStationBatch(s, Water, "w", 2));
        var q = Batches(s, "water_station");
        s.Items.RemoveAll(i => i.ItemId == "filter_media" && i.Owner == ItemInstance.StationOwner(s.Ground, q[1].BatchId));   // 起点：第二批丢了滤材
        AdvanceTo(s, t0 + 30);
        Assert.That(Sum(s, "clean_water"), Is.EqualTo(20), "第一批照常完成");   // B56 改期望（配置 ex_station_water 脏水20＋滤材1→净水20，M1079；b56-q973-q974 契约配方表×5）：原 4
        Assert.That(BatchBound(s, q[0].BatchId), Is.Empty, "完成批原料已核销");
        Assert.That(s.Items.Any(i => i.InStation), Is.False, "设施里不留原料");
        Assert.That(GroundSum(s, "dirty_water"), Is.EqualTo(20), "失败批剩下的脏水回地面");   // B56 改期望（配置 ex_station_water 脏水20＋滤材1→净水20，M1079；b56-q973-q974 契约配方表×5）：原 4
        Assert.That(s.Items.Where(i => i.ItemId == "dirty_water").All(i => i.ReservedBy == null && i.Owner == s.Ground), Is.True);
        Assert.That(s.Log.Any(l => l.Text == "临时营地固定净水台：原料不全，这一批没有产物，剩下的原料已解锁。"), Is.True);   // B17 原失败句，{区}{设施}
    }

    // ───────────── 保质与丢弃 ─────────────

    /// <summary>读档孤立保护收束（Q701.1）：设施批内原料随在办批保持暂停 R；批已不在的设施归属原料才收束（配对）。</summary>
    [Test]
    public void Sweep_KeepsStationProtection()
    {
        var s = Workshop();
        WaterInputs(s, 2);
        foreach (var i in s.Items.Where(i => i.ItemId == "dirty_water")) i.ExpiresAt = s.GameMinute + 50;
        Ok(E.StartStationBatch(s, Water, "w", 2));
        var q = Batches(s, "water_station");
        Assert.That(s.Items.Where(i => i.InStation && i.ItemId == "dirty_water").All(i => i.ShelfPausedMinutes == 50), Is.True, "起点：R＝E−S");
        E.FacOf(s, s.Ground)!.StationBatches.Remove(q[1]);   // 起点：第二批的批记录丢了（孤立保护）
        var loaded = SaveSerializer.Deserialize(SaveSerializer.Serialize(s), Cfg);
        var live = BatchBound(loaded, q[0].BatchId).Where(i => i.ItemId == "dirty_water").ToList();
        Assert.That(live.Sum(i => i.Count), Is.EqualTo(20));   // B56 改期望（配置 ex_station_water 脏水20＋滤材1→净水20，M1079；b56-q973-q974 契约配方表×5）：原 4
        Assert.That(live.All(i => i.ShelfPausedMinutes == 50), Is.True, "在办批原料保持保护");
        var orphan = loaded.Items.Where(i => i.Owner == ItemInstance.StationOwner(loaded.Ground, q[1].BatchId) && i.ItemId == "dirty_water").ToList();
        Assert.That(orphan.Sum(i => i.Count), Is.EqualTo(20));   // B56 改期望（配置 ex_station_water 脏水20＋滤材1→净水20，M1079；b56-q973-q974 契约配方表×5）：原 4
        Assert.That(orphan.All(i => i.ShelfPausedMinutes == null && i.ExpiresAt == loaded.GameMinute + 50), Is.True, "孤立的才按 C＋R 收束");
    }

    /// <summary>永久丢弃：设施批内原料算「加工中：先停止加工。」（B53b 原句）；取出回地面后不再算加工中（配对）。</summary>
    [Test]
    public void Discard_StationInput_Processing()
    {
        var s = Workshop();
        WaterInputs(s, 1);
        Ok(E.StartStationBatch(s, Water, "w"));
        var b = Batches(s, "water_station").Single();
        var dirty = E.StationBatchInputs(s, s.Ground, b).Single(i => i.ItemId == "dirty_water");
        Assert.That(E.DiscardItemBlocks(s, dirty), Does.Contain("加工中：先停止加工。"));
        Ok(TakeBatch(s, b));
        Assert.That(E.DiscardItemBlocks(s, dirty), Does.Not.Contain("加工中：先停止加工。"));
    }

    // ───────────── 存档 56→57 ─────────────

    /// <summary>
    /// FAC-Q872-02：56 旧档在办批原料（地面按批预留）读档时移进设施该批，R／数量／批次 ID／进度保留，预留标记清掉；
    /// 燃料（Q921）与无关的地面物不动；重复读档不再改。
    /// </summary>
    [Test]
    public void Migrate56To57_InputsIntoStation_FuelUntouched_Idempotent()
    {
        var s = Workshop();
        WaterInputs(s, 1);
        KilnInputs(s, 1);
        foreach (var i in s.Items.Where(i => i.ItemId == "dirty_water")) i.ExpiresAt = s.GameMinute + 100;
        E.CreateItems(s, "clay", 1, s.Ground);   // 无关的地面黏土
        var t0 = s.GameMinute;
        Ok(E.StartStationBatch(s, Water, "w"));
        Ok(E.StartStationBatch(s, Kiln, "k"));
        AdvanceTo(s, t0 + 10);
        var wb = Batches(s, "water_station").Single();
        var kb = Batches(s, "kiln").Single();
        Assert.That(E.DebugLegacyStation56(s), Does.Contain("存档56"));
        Assert.That(s.Items.Any(i => i.InStation), Is.False, "起点：56 旧模型原料都在地面");
        // B56 起点：DebugLegacyStation56 已把水按旧每份500毫升还原（SaveMigrator.DebugToLegacyWaterUnits），读档链 57→58 再×5
        Assert.That(s.Items.Where(i => i.ItemId == "dirty_water").Sum(i => i.Count), Is.EqualTo(4), "起点：旧档脏水4份");
        var obj = JObject.Parse(SaveSerializer.Serialize(s));
        Assert.That((string?)obj["SchemaVersion"], Is.EqualTo("56"));

        var m = SaveSerializer.Deserialize(obj.ToString(), Cfg);
        Assert.That(m.SchemaVersion, Is.EqualTo("58"));   // B56 改期望：读档链升到 58（SaveMigrator.From57To58）：原 "57"
        var wIn = m.Items.Where(i => i.Owner == ItemInstance.StationOwner(m.Ground, wb.BatchId)).OrderBy(i => i.ItemId).ToList();
        Assert.That(wIn.Select(i => (i.ItemId, i.Count)), Is.EqualTo(new[] { ("dirty_water", 20), ("filter_media", 1) }));   // B56 改期望：旧4份经 57→58 水份数×5（SaveMigrator.From57To58）＝20：原 4
        Assert.That(wIn.All(i => i.ReservedBy == null && i.PlacedOrder == null), Is.True, "预留标记与地面排序清掉");
        Assert.That(wIn.Single(i => i.ItemId == "dirty_water").ShelfPausedMinutes, Is.EqualTo(100), "R 保留");
        var kIn = m.Items.Where(i => i.Owner == ItemInstance.StationOwner(m.Ground, kb.BatchId)).ToList();
        Assert.That(kIn.Select(i => (i.ItemId, i.Count)), Is.EqualTo(new[] { ("clay", 2) }));
        var fuel = m.Items.Where(i => i.ItemId == "charcoal").ToList();
        Assert.That(fuel, Has.Count.EqualTo(1));
        Assert.That((fuel[0].Owner, fuel[0].ReservedBy), Is.EqualTo((m.Ground, (int?)kb.BatchId)), "燃料仍地面预留（Q921）");
        Assert.That(m.Items.Where(i => i.ItemId == "clay" && i.Owner == m.Ground).Sum(i => i.Count), Is.EqualTo(1), "无关地面黏土不动");
        Assert.That(Batches(m, "water_station").Select(b => (b.BatchId, b.DoneMinutes)), Is.EqualTo(new[] { (wb.BatchId, 10) }), "批次 ID 与进度保留");

        var again = SaveSerializer.Deserialize(SaveSerializer.Serialize(m), Cfg);
        Assert.That(again.Items.Select(i => (i.InstanceId, i.Owner, i.ReservedBy, i.Count)), Is.EqualTo(m.Items.Select(i => (i.InstanceId, i.Owner, i.ReservedBy, i.Count))), "重复读档不变");
        AdvanceTo(again, again.GameMinute + 60);
        Assert.That((Sum(again, "clean_water"), Sum(again, "ceramic_shell")), Is.EqualTo((20, 2)), "迁移后两批照常完成（配方净水20、陶壳2）");   // B56 改期望（配置 ex_station_water 脏水20＋滤材1→净水20，M1079；b56-q973-q974 契约配方表×5）：原 (4, 2)
    }

    // ───────────── 网页壳接线（src/Core 以外，做改坏验证） ─────────────

    /// <summary>
    /// Q923 B／Q870 B／Q778／Q879 A：设施工作区批次区撤「取消本批」，每批「取出」按本批门槛置灰、悬停真实原因，批内原料只读列出；
    /// 操作区常驻「全部取出」按 TakeAllBlocked 置灰；标题按 Q779 B；开发菜单有 56 旧档入口。
    /// </summary>
    [Test]
    public void Web_StationTakeWiring()
    {
        var craft = File.ReadAllText(Path.Combine(Root(), "web", "Survival.Web", "Components", "CraftOverlay.razor"));
        Assert.That(craft, Does.Contain("var takeWhy = e.StationBatchTakeBlocked(s, zonePlace, rc.Station, bid);"));
        Assert.That(craft, Does.Contain("data-station-take title=\"@takeWhy\" disabled=\"@(takeWhy != null || StationHeld)\""));
        Assert.That(craft, Does.Contain("G.E.TakeStationBatch(G.S, place, stationId, batchId, stamp, $\"station-take:{batchId}\")"));
        Assert.That(craft, Does.Contain("@foreach (var inp in e.StationBatchInputs(s, zonePlace, sbItem))"));
        Assert.That(craft, Does.Contain("var allWhy = e.TakeAllBlocked(s, allPlace, allStation);"));
        Assert.That(craft, Does.Contain("data-station-take-all title=\"@allWhy\" disabled=\"@(allWhy != null || StationHeld)\""));
        Assert.That(craft, Does.Contain("G.E.TakeAll(G.S, place, stationId, stamp, requestKey)"));
        // B51c2b 删断言（S11 §Q870 B／§Q924 B；测试规则第 8 条）：标题只数真实加工批，改由 bUnit B51c2bFacilityItemUiTests 断渲染结果
        Assert.That(craft, Does.Not.Contain("data-station-cancel"));
        var menu = File.ReadAllText(Path.Combine(Root(), "web", "Survival.Web", "Components", "MenuOverlay.razor"));
        Assert.That(menu, Does.Contain("G.E.DebugLegacyStation56"));
    }

    // ───────────── 变异测试补测（基线 9e3db204..d3daa00b） ─────────────

    /// <summary>空批起点：投一批后删掉其批内原料（只旧档异常数据会出现）。</summary>
    private StationBatch EmptyBatch(GameState s)
    {
        WaterInputs(s, 1);
        Ok(E.StartStationBatch(s, Water, "empty"));
        var b = Batches(s, "water_station").Last();
        s.Items.RemoveAll(i => i.Owner == ItemInstance.StationOwner(s.Ground, b.BatchId));
        return b;
    }

    /// <summary>
    /// 物主解析（开发假设 1：`station:{区}#{批ID}`）：格式对才算设施批原料；区为空、批号不是整数、没有批号、不是设施前缀都不算。
    /// </summary>
    [Test]
    public void MT_TryStation_Parse()
    {
        Assert.That(ItemInstance.TryStation(ItemInstance.StationOwner("camp", 15), out var place, out var id), Is.True);
        Assert.That((place, id), Is.EqualTo(("camp", 15)));
        Assert.That(ItemInstance.TryStation("station:#5", out _, out _), Is.False, "区为空");
        Assert.That(ItemInstance.TryStation("station:camp#x", out _, out _), Is.False, "批号不是整数");
        Assert.That(ItemInstance.TryStation("station:camp", out _, out _), Is.False, "没有批号");
        Assert.That(ItemInstance.TryStation("camp", out _, out _), Is.False, "地面物主");
    }

    /// <summary>
    /// 永久丢弃（B53a「加工中：先停止加工。」）：设施批内原料按归属认批（S11 Q872 B 真实归属本设施该批），批记录里的输入清单缺了也照样算加工中；
    /// 批记录已不在的孤立设施原料不算加工中（配对）。
    /// </summary>
    [Test]
    public void MT_Discard_OwnerBound_OrphanNot()
    {
        var s = Workshop();
        WaterInputs(s, 2);
        Ok(E.StartStationBatch(s, Water, "w", 2));
        var q = Batches(s, "water_station");
        q[0].InputInstanceIds.Clear();   // 起点：批记录里的输入清单缺失
        var a = E.StationBatchInputs(s, s.Ground, q[0]).Single(i => i.ItemId == "dirty_water");
        Assert.That(E.DiscardItemBlocks(s, a), Does.Contain("加工中：先停止加工。"), "按归属认批");
        var b = E.StationBatchInputs(s, s.Ground, q[1]).Single(i => i.ItemId == "dirty_water");
        q[1].InputInstanceIds.Clear();
        E.FacOf(s, s.Ground)!.StationBatches.Remove(q[1]);   // 起点：第二批的批记录丢了（孤立）
        Assert.That(E.DiscardItemBlocks(s, b), Does.Not.Contain("加工中：先停止加工。"), "孤立的不算加工中");
    }

    /// <summary>
    /// S06 Q666-A05「无保护的工作输入真腐败……没有同事件普通腐败重复句」：在办批内无保护的原料到期原位变腐败物、不写普通腐败句；
    /// 批记录已不在的孤立设施原料到期照常写「{原物名}×{数量}已腐败，变为腐败物×{数量}。」（S06 B50a 普通腐败句，配对）。
    /// </summary>
    [Test]
    public void MT_Spoil_LiveBatchInput_NoPlainLine_OrphanHasLine()
    {
        var s = Workshop();
        WaterInputs(s, 2);
        Ok(E.StartStationBatch(s, Water, "w", 2));
        var q = Batches(s, "water_station");
        foreach (var i in s.Items.Where(i => i.InStation && i.ItemId == "dirty_water"))
        {
            i.ShelfPausedMinutes = null;   // 起点：无保护（如 54 档升级时已到期的输入）
            i.ExpiresAt = s.GameMinute + 5;
        }
        E.StationBatchInputs(s, s.Ground, q[1]).Single(i => i.ItemId == "dirty_water").Count = 3;   // 起点：孤立那份 3 份，与在办批的 20 份区分
        E.FacOf(s, s.Ground)!.StationBatches.Remove(q[1]);   // 起点：第二批孤立
        var logs = s.Log.Count;
        AdvanceTo(s, s.GameMinute + 10);
        var live = BatchBound(s, q[0].BatchId).Where(i => i.ItemId == GameEngine.SpoiledMatterId).ToList();
        Assert.That(live.Sum(i => i.Count), Is.EqualTo(20), "在办批内的脏水原位变腐败物");   // B56 改期望（配置 ex_station_water 脏水20＋滤材1→净水20，M1079；b56-q973-q974 契约配方表×5）：原 4
        var lines = s.Log.Skip(logs).Select(l => l.Text).Where(t => t.Contains("已腐败，变为腐败物")).ToList();
        Assert.That(lines, Has.Count.EqualTo(1));
        Assert.That(lines[0], Is.EqualTo("脏水×3已腐败，变为腐败物×3。"), "只有孤立那份写普通腐败句，在办批那份不写");
    }

    /// <summary>投料取料（配方原料数）：先到期的一堆正好够数时只拿这一堆，后面的堆不动，设施里不出现 0 份的空堆。</summary>
    [Test]
    public void MT_Start_TakesExactStacks_NoZeroSplit()
    {
        var s = Workshop();
        E.CreateItems(s, "dirty_water", 20, s.Ground);   // B56：每份100毫升，×5 保持原 2000 毫升（正好一批）
        foreach (var i in s.Items.Where(i => i.ItemId == "dirty_water")) i.ExpiresAt = s.GameMinute + 100;   // 起点：先到期的一堆 20
        E.CreateItems(s, "dirty_water", 15, s.Ground);   // 无期限的一堆 15（B56：每份100毫升，×5 保持原 1500 毫升）
        E.CreateItems(s, "filter_media", 1, s.Ground);
        Assert.That(s.Items.Where(i => i.ItemId == "dirty_water").Select(i => i.Count).OrderBy(n => n), Is.EqualTo(new[] { 15, 20 }), "起点：两堆");
        Ok(E.StartStationBatch(s, Water, "w"));
        var b = Batches(s, "water_station").Single();
        var ins = E.StationBatchInputs(s, s.Ground, b);
        Assert.That(ins, Has.Count.EqualTo(2));
        Assert.That(ins.Select(i => (i.ItemId, i.Count)), Is.EquivalentTo(new[] { ("dirty_water", 20), ("filter_media", 1) }));   // B56 改期望（配置 ex_station_water 脏水20＋滤材1→净水20，M1079；b56-q973-q974 契约配方表×5）：原 4
        Assert.That(ins.Single(i => i.ItemId == "dirty_water").ShelfPausedMinutes, Is.EqualTo(100), "拿的是先到期那堆");
        var ground = s.Items.Where(i => i.ItemId == "dirty_water" && i.Owner == s.Ground).ToList();
        Assert.That(ground.Select(i => (i.Count, i.ExpiresAt)), Is.EqualTo(new[] { (15, (int?)null) }), "无期限那堆原样留在地面");   // B56：随造数×5（原 3）
    }

    /// <summary>
    /// B51b Q701-A02「投料即对本批有期限输入记 R＝E−S」，燃料随批分账（Q921 原 Fuel 分账）：有期限的燃料投料时也暂停保质；
    /// 取出只解锁、原地不动，在取出时刻 C 恢复 C＋R。
    /// </summary>
    [Test]
    public void MT_KilnFuel_ShelfPaused_ResumedOnTake()
    {
        var s = Workshop();
        KilnInputs(s, 1);
        var fuel = s.Items.Single(i => i.ItemId == "charcoal");
        fuel.ExpiresAt = s.GameMinute + 20;   // 起点：测试注入有限期限（现有配置木炭无期限）
        var t0 = s.GameMinute;
        Ok(E.StartStationBatch(s, Kiln, "k"));
        Assert.That(fuel.ShelfPausedMinutes, Is.EqualTo(20), "R＝E−S");
        AdvanceTo(s, t0 + 25);
        Assert.That((fuel.ItemId, fuel.Count), Is.EqualTo(("charcoal", 1)), "受保护越过原截止不腐败");
        var c = s.GameMinute;
        Ok(TakeBatch(s, Batches(s, "kiln").Single()));
        Assert.That((fuel.ShelfPausedMinutes, fuel.ExpiresAt, fuel.ReservedBy, fuel.Owner), Is.EqualTo(((int?)null, (int?)(c + 20), (int?)null, s.Ground)));
    }

    /// <summary>配方说明（S11 Q880：原说明只把动词「取消」换成「取出」）：原料、燃料、产物、分钟与落点。</summary>
    [Test]
    public void MT_StationBatchSummary_Text()
    {
        // B56 改期望（配置 ex_station_water 脏水20＋滤材1→净水20，M1079；b56-q973-q974 契约配方表×5）：原「脏水×4、滤材×1 → 净水×4」
        Assert.That(E.StationBatchSummary(Cfg.Recipes.Single(r => r.Id == Water)),
            Is.EqualTo("脏水×20、滤材×1 → 净水×20，被动30世界分钟（不占行动，离开也继续），完成后放在本区地面；取出退回原料，时间不退"));
        Assert.That(E.StationBatchSummary(Cfg.Recipes.Single(r => r.Id == Kiln)),
            Is.EqualTo("黏土×2＋燃料木炭×1 → 陶壳×2，被动60世界分钟（不占行动，离开也继续），完成后放在本区地面；取出退回原料，时间不退"));
    }

    /// <summary>B51-Q778-04（计划绑定设施／批次／物品）：「全部取出」的计划戳不能拿来取单批，哪怕这座只有这一批；本批自己的戳可取（配对）。</summary>
    [Test]
    public void MT_Stamp_ScopeBound()
    {
        var s = Workshop();
        WaterInputs(s, 1);
        Ok(E.StartStationBatch(s, Water, "w"));
        var b = Batches(s, "water_station").Single();
        var all = E.StationTakeStamp(s, s.Ground, "water_station");
        var r = E.TakeStationBatch(s, s.Ground, "water_station", b.BatchId, all, "x");
        Assert.That((r.Ok, r.Reason), Is.EqualTo((false, "物品已变，请重选。")));
        Assert.That(Batches(s, "water_station"), Has.Count.EqualTo(1));
        Ok(TakeBatch(s, b, "y"));
    }

    /// <summary>B51-Q778-04（S11 Q880 快照变化仍用「物品已变，请重选。」）：渲染后批内原料份数变了，旧戳整次拒、一样不动；重取戳后按真实份数取（配对）。</summary>
    [Test]
    public void MT_Stamp_CountChanged_Rejected()
    {
        var s = Workshop();
        WaterInputs(s, 1);
        Ok(E.StartStationBatch(s, Water, "w"));
        var b = Batches(s, "water_station").Single();
        var stamp = E.StationTakeStamp(s, s.Ground, "water_station", b.BatchId);
        E.StationBatchInputs(s, s.Ground, b).Single(i => i.ItemId == "dirty_water").Count = 3;   // 起点：渲染后份数变了
        var r = E.TakeStationBatch(s, s.Ground, "water_station", b.BatchId, stamp, "x");
        Assert.That((r.Ok, r.Reason), Is.EqualTo((false, "物品已变，请重选。")));
        Assert.That(BatchBound(s, b.BatchId).Sum(i => i.Count), Is.EqualTo(4), "一样不动（脏水3＋滤材1）");
        Ok(TakeBatch(s, b, "y"));
        Assert.That(s.Log.Last().Text, Is.EqualTo("已取出至临时营地地面：脏水×3、滤材×1。"));   // S11 Q876 B
    }

    /// <summary>批已结束后即使用此刻重算的戳再取，仍拒「物品已变，请重选。」（批不在），不报「没有可取出的物品。」。</summary>
    [Test]
    public void MT_GoneBatch_FreshStamp_Changed()
    {
        var s = Workshop();
        WaterInputs(s, 1);
        Ok(E.StartStationBatch(s, Water, "w"));
        var b = Batches(s, "water_station").Single();
        Ok(TakeBatch(s, b, "a"));
        var r = TakeBatch(s, b, "b");
        Assert.That((r.Ok, r.Reason), Is.EqualTo((false, "物品已变，请重选。")));
    }

    /// <summary>
    /// 逐字稿 B51 表「没有可取出的物品。」与 B51-Q778-04「未移出批进度保留」：批内已无原料的批，本批「取出」置灰写「没有可取出的物品。」；
    /// 「全部取出」只结束真有原料的批，空批留着、进度不变；全是空批时拒「没有可取出的物品。」、不写日志。
    /// </summary>
    [Test]
    public void MT_EmptyBatch_TakeBlocked_TakeAllSkips()
    {
        var s = Workshop();
        var empty = EmptyBatch(s);
        Assert.That(E.StationBatchTakeBlocked(s, s.Ground, "water_station", empty.BatchId), Is.EqualTo("没有可取出的物品。"));
        Assert.That(E.TakeAllBlocked(s, s.Ground, "water_station"), Is.EqualTo("没有可取出的物品。"));
        var logs = s.Log.Count;
        var r0 = TakeAllNow(s, "water_station", "a0");
        Assert.That((r0.Ok, r0.Reason), Is.EqualTo((false, "没有可取出的物品。")));
        Assert.That((Batches(s, "water_station").Count, s.Log.Count), Is.EqualTo((1, logs)), "空批不结束、不写日志");

        WaterInputs(s, 1);
        Ok(E.StartStationBatch(s, Water, "full"));
        AdvanceTo(s, s.GameMinute + 10);
        var done = Batches(s, "water_station").Single(b => b.BatchId == empty.BatchId).DoneMinutes;
        Ok(TakeAllNow(s, "water_station", "a1"));
        Assert.That(Batches(s, "water_station").Select(b => (b.BatchId, b.DoneMinutes)), Is.EqualTo(new[] { (empty.BatchId, done) }), "空批留着、进度不变");
        Assert.That(s.Log.Last().Text, Is.EqualTo("已取出至临时营地地面：脏水×20、滤材×1。"));   // S11 Q876 B；B56 改期望（配置 ex_station_water 脏水20，M1079）：原「脏水×4」
    }

    /// <summary>存档 56→57：配方已不存在的批不迁（读档不出错），它的地面预留原料原样留在地面；配方在的批照常移进设施（配对）。</summary>
    [Test]
    public void MT_Migrate56To57_MissingRecipe_Untouched()
    {
        var s = Workshop();
        WaterInputs(s, 2);
        Ok(E.StartStationBatch(s, Water, "w", 2));
        var q = Batches(s, "water_station");
        E.DebugLegacyStation56(s);
        // B56 起点：DebugLegacyStation56 已把水按旧每份500毫升还原（SaveMigrator.DebugToLegacyWaterUnits），读档链 57→58 再×5
        q[1].RecipeId = "no_such_recipe";   // 起点：旧档里这一批的配方已从配置删除
        var m = SaveSerializer.Deserialize(SaveSerializer.Serialize(s), Cfg);
        Assert.That(m.SchemaVersion, Is.EqualTo("58"));   // B56 改期望：读档链升到 58（SaveMigrator.From57To58）：原 "57"
        Assert.That(m.Items.Where(i => i.Owner == ItemInstance.StationOwner(m.Ground, q[0].BatchId)).Sum(i => i.Count), Is.EqualTo(21), "配方在的批：脏水20＋滤材1移进设施");   // B56 改期望：旧4份经 57→58 ×5＝20（SaveMigrator.From57To58）：原 5
        var left = m.Items.Where(i => i.ReservedBy == q[1].BatchId).ToList();
        Assert.That(left.Sum(i => i.Count), Is.EqualTo(21));   // B56 改期望：同上，地面预留的旧4份×5＝20＋滤材1：原 5
        Assert.That(left.All(i => i.Owner == m.Ground), Is.True, "配方不在的批原样留在地面预留");
    }
}
