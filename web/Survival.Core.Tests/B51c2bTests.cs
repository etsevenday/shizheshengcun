using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 批次 B51c2b「被动设施卡片箭头、余料与拖入（不升存档）」：
/// S11／S14「2026-11-17 B51c2、B47b与B52b三个完整原批」§Q870 B 快捷箭头（FAC-Q870-02—04、B51-Q778-01 卡片部分），
/// 「2026-11-17 B51c2a／B51c2b完整两原批」§Q924 B（余料可补投）、§Q925 B（设施物品卡数量取出、拖出）、§Q926 A（拖到晾制位只导航）、§Q927 B（火堆食物长按，网页壳另测）；
/// 合同 assets/20261117/facility-two-originals-q921-q927.json 验收 FACF-Q924-01—03、FACF-Q925-01—02、FACF-Q926-01—02。
/// 期望字面量均注明出处；起点只用 Debug／直接造物构造，结论都走真实取出、投料、拖放入口。
/// </summary>
public class B51c2bTests : TestBase
{
    private const string Busy = "行动进行中，暂不能整理物品";   // S11 Q871 B：统一取出门槛沿原句
    private const string Changed = "物品已变，请重选。";        // S11 Q879 A／Q925 B：位／批或卡已变
    private const string Full = "已满";                          // S11 Q880 映射表／Q926 A「占用位不响应并提示已满」
    private const string Kiln = "ex_ceramic_shell";              // 配置：黏土2 → 陶壳2，60 分钟，燃料木炭1

    protected override bool NoEnemyApproach => true;

    private void Ok(ActionResult r) => Assert.That(r.Ok, Is.True, r.Reason);

    private static List<DryingSlot> DrySlots(GameState s) => s.Locations[s.Ground].Facility.DryingSlots;

    private static int Sum(GameState s, string itemId, string owner) => s.Items.Where(i => i.ItemId == itemId && i.Owner == owner).Sum(i => i.Count);

    private ActionResult Take(GameState s, ItemInstance i, int count, string zone, string? stamp = null) =>
        E.DoInstant(s, InstantRequest.FacilityTake(i.InstanceId, count, zone, stamp ?? E.FacilityItemStamp(s, i)));

    /// <summary>临时营地建好晾制架（同 B51c2a2 起点），清掉地面与随身物品。</summary>
    private GameState Rack()
    {
        var s = New();
        s.GameMinute = At(2, 8, 0);
        s.Skills["cooking"].Level = 5;
        s.Items.RemoveAll(i => i.Owner == s.Ground || i.Owner == ItemInstance.Carried);
        E.CreateItems(s, "wood", 4, s.Ground);
        E.CreateItems(s, "rope", 2, s.Ground);
        Start(s, ActionRequest.Craft("build_drying_rack"));
        Run(s);
        Assert.That(E.DryingRackHere(s), Is.True);
        s.Items.RemoveAll(i => i.Owner == s.Ground || i.Owner == ItemInstance.Carried);
        return s;
    }

    /// <summary>位 1 投一批果干（野果×2，原截止＝现在＋30 分钟，开工即暂停保质 R＝30），返回位内那张野果卡。</summary>
    private ItemInstance BerriesInSlot0(GameState s)
    {
        var made = E.CreateItems(s, "berry", 2, ItemInstance.Carried);
        foreach (var i in made) i.ExpiresAt = s.GameMinute + 30;
        Ok(E.StartDrying(s, "dry_berries"));
        var inSlot = s.Items.Where(i => i.Owner == ItemInstance.DryingOwner(s.Ground, 0)).ToList();
        Assert.That(inSlot.Select(i => (i.ItemId, i.Count, i.ShelfPausedMinutes)), Is.EqualTo(new[] { ("berry", 2, (int?)30) }), "起点：位内野果×2、保质暂停剩 30 分钟");
        return inSlot[0];
    }

    /// <summary>临时营地建好陶窑（同 B51c2a1 起点），清掉相关原料与产物，经验清零。</summary>
    private GameState Workshop()
    {
        var s = New();
        s.Skills["crafting"].Level = 3;
        s.Progress.Flags.Add("h03_ceramic_notice_read");
        foreach (var (id, n) in new[] { ("stone", 8), ("clay", 6), ("iron_plate", 2) }) E.CreateItems(s, id, n, s.Ground);
        Start(s, ActionRequest.Craft("build_kiln", null, null, 1));
        Run(s);
        s.Items.RemoveAll(i => i.ItemId is "clay" or "charcoal" or "ceramic_shell");
        s.Skills["crafting"].Xp = 0;
        return s;
    }

    private List<StationBatch> KilnBatches(GameState s) => E.FacOf(s, s.Ground)!.StationBatches.Where(b => b.StationId == "kiln").ToList();

    private void KilnInputs(GameState s, int batches)
    {
        E.CreateItems(s, "clay", 2 * batches, s.Ground);
        E.CreateItems(s, "charcoal", batches, s.Ground);
    }

    private List<ItemInstance> Inputs(GameState s, StationBatch b) => E.StationBatchInputs(s, s.Ground, b);

    // ───────────── 晾制位：卡片箭头／数量取出（Q870 B 快捷箭头、Q925 B） ─────────────

    /// <summary>
    /// FAC-Q870-02／FAC-Q870-04（晾制）：位内野果×2 取 1 份到随身——移出的 1 份恢复 C＋R（新截止＝取出时刻＋30），同一事务结束这一位：
    /// 剩下的野果×1 解锁、恢复 C＋R、留在本位成「余料」（S11 §Q924「短状态可用“余料”」），无成品、无经验、0 分钟；
    /// 结果沿火堆单件取出原句「把{物名}从{位置}取出到{去向}。」（位置沿 WherePath「{区}／晾制架工位{n}」）。
    /// </summary>
    [Test]
    public void Q870_DryingCard_TakeOneToCarried_SlotBecomesResidual_CR_NoXp()
    {
        var s = Rack();
        var card = BerriesInSlot0(s);
        AdvanceTo(s, s.GameMinute + 60);
        var c = s.GameMinute;
        var xp = s.Skills["cooking"].Xp;
        var batch = DrySlots(s).Single().BatchId;

        Ok(Take(s, card, 1, ItemInstance.Carried));

        Assert.That(s.GameMinute, Is.EqualTo(c), "0 分钟");
        var carried = s.Items.Where(i => i.ItemId == "berry" && i.Owner == ItemInstance.Carried).ToList();
        Assert.That(carried, Has.Count.EqualTo(1));
        Assert.That((carried[0].Count, carried[0].ExpiresAt, carried[0].ShelfPausedMinutes), Is.EqualTo((1, (int?)(c + 30), (int?)null)), "移出的 1 份 C＋R（R＝30）");
        var slot = DrySlots(s).Single();
        Assert.That((slot.Slot, slot.BatchId, slot.Status), Is.EqualTo((0, batch, DryingSlot.Residual)), "这一位结束、成余料、仍占位");
        var rest = E.DryingContents(s, s.Ground, 0);
        Assert.That(rest, Has.Count.EqualTo(1));
        Assert.That((rest[0].ItemId, rest[0].Count, rest[0].ExpiresAt, rest[0].ShelfPausedMinutes, rest[0].ReservedBy),
            Is.EqualTo(("berry", 1, (int?)(c + 30), (int?)null, (int?)null)), "余料解锁、恢复 C＋R、留原位");
        Assert.That(s.Skills["cooking"].Xp, Is.EqualTo(xp), "无经验");
        Assert.That(s.Log.Last().Text, Is.EqualTo("把野果从临时营地／晾制架工位1取出到随身。"), "沿火堆单件取出原句");
        Assert.That(E.DryingSlotViews(s)[0].Status, Is.EqualTo(DryingSlot.Residual));
        Assert.That(CardStates.DryingStatusText(DryingSlot.Residual), Is.EqualTo("余料"), "S11 §Q924 短状态");
    }

    /// <summary>FAC-Q870-04：余料位不推进、不结算——推过原完成时刻仍是余料、没有果干、无经验。</summary>
    [Test]
    public void Q870_ResidualSlot_NotAdvanced_NoOutput()
    {
        var s = Rack();
        var card = BerriesInSlot0(s);
        Ok(Take(s, card, 1, s.Ground));
        var xp = s.Skills["cooking"].Xp;
        AdvanceTo(s, s.GameMinute + 180);   // 配置 dry_berries 120 分钟
        Assert.That(DrySlots(s).Select(b => b.Status), Is.EqualTo(new[] { DryingSlot.Residual }));
        Assert.That(s.Items.Count(i => i.ItemId == "dried_berries"), Is.EqualTo(0), "无成品");
        Assert.That(s.Skills["cooking"].Xp, Is.EqualTo(xp), "无经验");
    }

    /// <summary>对照：取到当前地面不受随身容量限制，结果去向写「{区}地面」。</summary>
    [Test]
    public void Q925_DryingCard_TakeAllToGround_SlotFreed()
    {
        var s = Rack();
        var card = BerriesInSlot0(s);
        Ok(Take(s, card, 2, s.Ground));
        Assert.That(DrySlots(s), Is.Empty, "位已取空即空出");
        Assert.That(Sum(s, "berry", s.Ground), Is.EqualTo(2));
        Assert.That(s.Log.Last().Text, Is.EqualTo("把野果×2从临时营地／晾制架工位1取出到临时营地地面。"));
    }

    /// <summary>
    /// FAC-Q870-03：随身已满五张卡，取 1 份到随身被拒（沿原拾取拒绝句），什么都不变——这一位仍在晾、原料仍暂停保质、不自动改落地。
    /// 配对：同一张卡取到地面成功。
    /// </summary>
    [Test]
    public void Q870_CapacityFail_NothingChanges_GroundStillWorks()
    {
        var s = Rack();
        var card = BerriesInSlot0(s);
        foreach (var id in new[] { "wood", "stone", "rope", "plank", "cloth" }) E.CreateItems(s, id, 1, ItemInstance.Carried);
        var r = Take(s, card, 1, ItemInstance.Carried);
        Assert.That(r.Ok, Is.False);
        Assert.That(r.Reason, Is.EqualTo("随身卡位已满（5/5张）：会多出一张新卡，请先放下或用掉一张"), "沿原拾取拒绝句");
        Assert.That(DrySlots(s).Single().Status, Is.EqualTo(DryingSlot.Drying), "不中止");
        Assert.That((card.Owner, card.Count, card.ShelfPausedMinutes), Is.EqualTo((ItemInstance.DryingOwner(s.Ground, 0), 2, (int?)30)), "不移、不恢复保质");
        Assert.That(Sum(s, "berry", s.Ground), Is.EqualTo(0), "不自动改落地");

        Ok(Take(s, card, 1, s.Ground));
        Assert.That(Sum(s, "berry", s.Ground), Is.EqualTo(1));
    }

    /// <summary>FAC-Q871（Q925 B「行动进行或暂停都不能取」）：主行动进行中拒统一句，什么都不动；配对：行动结束后可取。</summary>
    [Test]
    public void Q925_BusyGate_ThenIdleOk()
    {
        var s = Rack();
        var card = BerriesInSlot0(s);
        Start(s, ActionRequest.Wait(30));
        Assert.That(E.FacilityItemGate(s, card), Is.EqualTo(Busy));
        var r = Take(s, card, 1, s.Ground);
        Assert.That((r.Ok, r.Reason), Is.EqualTo((false, Busy)));
        Assert.That(DrySlots(s).Single().Status, Is.EqualTo(DryingSlot.Drying));
        E.Cancel(s);
        Assert.That(E.FacilityItemGate(s, card), Is.Null);
        Ok(Take(s, card, 1, s.Ground));
    }

    /// <summary>Q925 B「稳定、幂等」：份数超过现有拒「物品已变，请重选。」；身份戳不符同样拒；同一请求号只生效一次。</summary>
    [Test]
    public void Q925_StampAndCount_Changed_RequestOnce()
    {
        var s = Rack();
        var card = BerriesInSlot0(s);
        Assert.That(Take(s, card, 3, s.Ground).Reason, Is.EqualTo(Changed), "份数超过现有");
        Assert.That(Take(s, card, 1, s.Ground, "x").Reason, Is.EqualTo(Changed), "身份戳不符");
        Assert.That(card.Count, Is.EqualTo(2), "什么都不动");

        var req = InstantRequest.FacilityTake(card.InstanceId, 1, s.Ground, E.FacilityItemStamp(s, card));
        req.RequestId = "q925-once";
        Ok(E.DoInstant(s, req));
        var again = E.DoInstant(s, req);
        Assert.That(again.Ok, Is.True, "重复请求按已处理返回");
        Assert.That(Sum(s, "berry", s.Ground), Is.EqualTo(1), "只取一次");
    }

    // ───────────── 晾制位：余料补投（Q924 B） ─────────────

    /// <summary>
    /// FACF-Q924-01：余料位补投——预览先列本位余料（前缀「余料」）再列补足来源；确认后复用这一位、新批 ID、进度 0、重新暂停保质，不另占一位；
    /// 旧身份戳（旧批）不再能取这张余料卡（Q925 B：位换成新批拒「物品已变，请重选。」）。
    /// </summary>
    [Test]
    public void Q924_DryingRefill_ResidualFirst_NewBatchSameSlot()
    {
        var s = Rack();
        var card = BerriesInSlot0(s);
        Ok(Take(s, card, 1, ItemInstance.Carried));
        var c = s.GameMinute;
        var old = DrySlots(s).Single().BatchId;
        var residual = E.DryingContents(s, s.Ground, 0).Single();
        var oldStamp = E.FacilityItemStamp(s, residual);

        var o = E.DryingPreview(s, "dry_berries", 0);
        Assert.That(o.Ok, Is.True, o.Reason);
        Assert.That(o.Sources, Has.Count.EqualTo(2));
        Assert.That(o.Sources[0], Does.StartWith("余料野果×1"), "余料先用");
        Assert.That(o.Sources[1], Does.StartWith("随身野果×1"), "再从随身补");

        Ok(E.StartDrying(s, "dry_berries", 0, null, o.SourceKey));

        var b = DrySlots(s).Single();
        Assert.That((b.Slot, b.Status, b.DoneMinutes, b.StartMinute), Is.EqualTo((0, DryingSlot.Drying, 0, c)), "复用这一位、进度 0");
        Assert.That(b.BatchId, Is.Not.EqualTo(old), "新批 ID");
        var inSlot = E.DryingContents(s, s.Ground, 0);
        Assert.That(inSlot.Sum(i => i.Count), Is.EqualTo(2));
        Assert.That(inSlot.All(i => i.ShelfPausedMinutes == 30 && i.ExpiresAt == null), Is.True, "重新暂停保质（R＝30）");
        Assert.That(Sum(s, "berry", ItemInstance.Carried), Is.EqualTo(0));
        Assert.That(s.Log.Last().Text, Does.Contain("投入余料野果×1、随身野果×1"));
        Assert.That(Take(s, residual, 1, s.Ground, oldStamp).Reason, Is.EqualTo(Changed), "旧批身份戳不再生效");
    }

    /// <summary>
    /// Q924 B：余料位只能按本位原配方补投——别的配方投到这一位拒「已满」；余料位不算空位，不指定位时投到下一个空位（位 2）。
    /// </summary>
    [Test]
    public void Q924_ResidualSlot_OtherRecipeFull_AutoPickSkipsIt()
    {
        var s = Rack();
        var card = BerriesInSlot0(s);
        Ok(Take(s, card, 1, s.Ground));
        E.CreateItems(s, "raw_meat", 1, ItemInstance.Carried);
        E.CreateItems(s, "salt", 1, ItemInstance.Carried);
        Assert.That(E.DryingPreview(s, "dry_meat", 0).Reason, Is.EqualTo(Full));
        var auto = E.DryingPreview(s, "dry_meat");
        Assert.That((auto.Ok, auto.Slot), Is.EqualTo((true, 1)));
    }

    /// <summary>余料位「取出」（Q870 B 位按钮沿用）：余料回本区地面、这一位空出。</summary>
    [Test]
    public void Q924_ResidualSlot_TakeButton_ToGround()
    {
        var s = Rack();
        var card = BerriesInSlot0(s);
        Ok(Take(s, card, 1, ItemInstance.Carried));
        var v = E.DryingSlotViews(s)[0];
        Assert.That(v.TakeBlocked, Is.Null);
        Ok(E.TakeDryingSlot(s, 0, v.TakeStamp));
        Assert.That(DrySlots(s), Is.Empty);
        Assert.That(Sum(s, "berry", s.Ground), Is.EqualTo(1));
    }

    // ───────────── 陶窑：批内物品卡、余料条目与补投（Q870 B、Q924 B） ─────────────

    /// <summary>
    /// FAC-Q870-04（陶窑）／FACF-Q924-02：黏土×2 的一批取 1 份到随身——这一批结束成余料条目：燃料木炭只解锁一次、原地不动；
    /// 剩下黏土×1 留批内、计入 10 条上限、不计加工中；同座另一批照常加工并结算陶壳×2。推过完成时刻余料条目无成品、无经验。
    /// </summary>
    [Test]
    public void Q870_KilnCard_PartialTake_ResidualEntry_OtherBatchUntouched()
    {
        var s = Workshop();
        KilnInputs(s, 2);
        Ok(E.StartStationBatch(s, Kiln, "k", 2));
        var list = KilnBatches(s);
        Assert.That(list, Has.Count.EqualTo(2));
        var first = list[0];
        var clay = Inputs(s, first).Single();
        var fuelId = s.Items.Single(i => i.ItemId == "charcoal" && i.ReservedBy == first.BatchId).InstanceId;

        Ok(Take(s, clay, 1, ItemInstance.Carried));

        Assert.That(first.Residual, Is.True, "这一批结束成余料条目");
        Assert.That(Inputs(s, first).Select(i => (i.ItemId, i.Count)), Is.EqualTo(new[] { ("clay", 1) }), "剩下黏土×1 留批内");
        var fuel = s.Items.Single(i => i.InstanceId == fuelId);
        Assert.That((fuel.Owner, fuel.ReservedBy), Is.EqualTo((s.Ground, (int?)null)), "燃料只解锁、原地不动");
        Assert.That(Sum(s, "clay", ItemInstance.Carried), Is.EqualTo(1));
        Assert.That(s.Log.Last().Text, Is.EqualTo("把黏土从临时营地／陶窑取出到随身。"), "位置沿 Q1007 暂用「{区域}／{设施名}」");
        Assert.That(E.StationProcessingCount(s, "kiln"), Is.EqualTo(1), "Q870 B 标题只数真实正在加工的批");
        Assert.That(E.StationBatchRow(s, first, 1), Is.EqualTo("批次1 · 陶壳 · 余料"));

        AdvanceTo(s, s.GameMinute + 61);   // 配置 ex_ceramic_shell 60 分钟
        Assert.That(KilnBatches(s).Select(b => b.Residual), Is.EqualTo(new[] { true }), "另一批结算离开，余料条目仍在");
        Assert.That(Count(s, "ceramic_shell"), Is.EqualTo(2), "只另一批出陶壳×2");
    }

    /// <summary>
    /// FACF-Q924-02／03（陶窑）：余料条目补投——预览「先用余料黏土×1，再从随身与当前地面取黏土×1、木炭×1」，确认后新批原位替换余料条目
    /// （同一列表位置、新批 ID、进度 0），余料实例真实进入新批；燃料按新批预留。
    /// </summary>
    [Test]
    public void Q924_KilnRefill_ReplacesResidualEntryInPlace()
    {
        var s = Workshop();
        KilnInputs(s, 2);
        Ok(E.StartStationBatch(s, Kiln, "k", 2));
        var first = KilnBatches(s)[0];
        var second = KilnBatches(s)[1];
        var clay = Inputs(s, first).Single();
        Ok(Take(s, clay, 1, s.Ground));
        var residualId = Inputs(s, first).Single().InstanceId;
        E.CreateItems(s, "charcoal", 1, s.Ground);
        var rc = Cfg.Recipes.Single(r => r.Id == Kiln);

        Assert.That(E.StationBatchBlocked(s, rc, 1), Is.Null);
        Assert.That(E.StationRefillText(s, rc, 1), Is.EqualTo("先用余料黏土×1，再从随身与当前地面取黏土×1、木炭×1"));
        Ok(E.StartStationBatch(s, Kiln, "k2"));

        var list = KilnBatches(s);
        Assert.That(list, Has.Count.EqualTo(2), "不另占一条");
        Assert.That(list[1], Is.SameAs(second), "另一批不动");
        var nb = list[0];
        Assert.That((nb.Residual, nb.DoneMinutes), Is.EqualTo((false, 0)));
        Assert.That(nb.BatchId, Is.Not.EqualTo(first.BatchId), "新批 ID");
        Assert.That(Inputs(s, nb).Sum(i => i.Count), Is.EqualTo(2));
        Assert.That(Inputs(s, nb).Select(i => i.InstanceId), Does.Contain(residualId), "余料先用");
        Assert.That(Sum(s, "clay", s.Ground), Is.EqualTo(0));
        Assert.That(s.Items.Count(i => i.ItemId == "charcoal" && i.ReservedBy == nb.BatchId), Is.EqualTo(1), "燃料按新批预留");
    }

    /// <summary>
    /// Q924 B「8 位／10 批上限都不超过」：10 批里有 1 条余料条目，余料计入上限——只能再投 1 批（即补投这一条），投 2 批拒原句。
    /// </summary>
    [Test]
    public void Q924_KilnCap_ResidualCounted()
    {
        var s = Workshop();
        KilnInputs(s, 10);
        Ok(E.StartStationBatch(s, Kiln, "k", 10));
        Ok(Take(s, Inputs(s, KilnBatches(s)[3]).Single(), 1, s.Ground));
        KilnInputs(s, 3);
        var rc = Cfg.Recipes.Single(r => r.Id == Kiln);
        Assert.That(E.StationBatchMaxNew(s, rc), Is.EqualTo(1));
        Assert.That(E.StationBatchBlocked(s, rc, 2), Is.EqualTo("还可同时加工1批，请减少投料批数。"), "Q701.4 原句");
        Assert.That(E.StationBatchBlocked(s, rc, 1), Is.Null);
    }

    /// <summary>余料条目整批「取出」（B51c2a1 批按钮沿用）：余料回地面、条目删掉；燃料不重复解锁（只解锁一次）。</summary>
    [Test]
    public void Q924_KilnResidual_TakeBatch_RemovesEntry()
    {
        var s = Workshop();
        KilnInputs(s, 1);
        Ok(E.StartStationBatch(s, Kiln, "k"));
        var b = KilnBatches(s).Single();
        Ok(Take(s, Inputs(s, b).Single(), 1, s.Ground));
        Ok(E.TakeStationBatch(s, s.Ground, "kiln", b.BatchId, E.StationTakeStamp(s, s.Ground, "kiln", b.BatchId)));
        Assert.That(KilnBatches(s), Is.Empty);
        Assert.That(Sum(s, "clay", s.Ground), Is.EqualTo(2));
        Assert.That(s.Items.Where(i => i.ItemId == "charcoal").Select(i => (i.Owner, i.ReservedBy, i.Count)), Is.EqualTo(new[] { (s.Ground, (int?)null, 1) }));
    }

    // ───────────── 拖放（Q925 B 拖出、Q926 A 拖到晾制位） ─────────────

    private DropIntent Drop(GameState s, ItemInstance i, string target) =>
        E.ResolveDrop(s, DropSource.Parse($"card:{i.Owner}|{i.ItemId}|{i.InstanceId}")!, DropTarget.Parse(target)!);

    /// <summary>
    /// FACF-Q925-02：设施物品卡拖到随身／当前地面——即时取出（同一事务），提示沿原「拾取1份{物名}到随身」「取出1份{物名}到地面」，
    /// Shift 上限为可取份数；拖到其他地点拒。
    /// </summary>
    [Test]
    public void Q925_DragOut_Intents()
    {
        var s = Workshop();
        KilnInputs(s, 1);
        Ok(E.StartStationBatch(s, Kiln, "k"));
        var clay = Inputs(s, KilnBatches(s).Single()).Single();

        var toCarried = Drop(s, clay, "zone:carried");
        Assert.That((toCarried.Ok, toCarried.Verb, toCarried.Label, toCarried.MaxQuantity), Is.EqualTo((true, DropVerb.TakeOut, "拾取1份黏土到随身", 2)));
        Assert.That(toCarried.Instant!.Kind, Is.EqualTo(InstantKind.FacilityTake));
        var toGround = Drop(s, clay, $"zone:{s.Ground}");
        Assert.That((toGround.Ok, toGround.Label, toGround.MaxQuantity), Is.EqualTo((true, "取出1份黏土到地面", 2)));
        Assert.That(Drop(s, clay, "loc:somewhere").Reason, Is.EqualTo(GameEngine.Unsupported));

        Ok(E.DoInstant(s, toGround.Instant!));
        Assert.That(Sum(s, "clay", s.Ground), Is.EqualTo(1));
        Assert.That(KilnBatches(s).Single().Residual, Is.True);
    }

    /// <summary>拖出同样受统一取出门槛：主行动进行中拒「行动进行中，暂不能整理物品」。</summary>
    [Test]
    public void Q925_DragOut_Busy()
    {
        var s = Workshop();
        KilnInputs(s, 1);
        Ok(E.StartStationBatch(s, Kiln, "k"));
        var clay = Inputs(s, KilnBatches(s).Single()).Single();
        Start(s, ActionRequest.Wait(30));
        var d = Drop(s, clay, "zone:carried");
        Assert.That((d.Ok, d.Reason), Is.EqualTo((false, Busy)));
    }

    /// <summary>
    /// FACF-Q926-01：材料卡拖到空晾制位——只导航：选中该位、定位用这件物品的食谱（野果 → 果干），落点提示沿空位按钮「查看配方」；不搬物、不开工。
    /// FACF-Q926-02：拖到晾制中的位拒「已满」。
    /// </summary>
    [Test]
    public void Q926_DragToDryingSlot_EmptyPreview_OccupiedFull()
    {
        var s = Rack();
        BerriesInSlot0(s);
        var berry = E.CreateItems(s, "berry", 1, ItemInstance.Carried)[0];

        var empty = Drop(s, berry, "dryslot:1");
        Assert.That((empty.Ok, empty.Verb, empty.Label, empty.Slot), Is.EqualTo((true, DropVerb.PreviewDrying, "查看配方", (int?)1)));
        Assert.That(empty.Recipes, Is.EqualTo(new[] { "dry_berries" }));
        Assert.That(empty.IsPreview, Is.True, "预览类：壳只导航不提交");
        Assert.That((berry.Owner, DrySlots(s).Count), Is.EqualTo((ItemInstance.Carried, 1)), "不搬物、不开工");

        var occupied = Drop(s, berry, "dryslot:0");
        Assert.That((occupied.Ok, occupied.Reason), Is.EqualTo((false, Full)));
    }

    /// <summary>Q926 A 例外（Q924 B）：同配方可补投的余料位进本位补投预览；别的材料拖到这一余料位拒「已满」。</summary>
    [Test]
    public void Q926_DragToResidualSlot_RefillOrFull()
    {
        var s = Rack();
        var card = BerriesInSlot0(s);
        Ok(Take(s, card, 1, s.Ground));
        var berry = s.Items.Single(i => i.ItemId == "berry" && i.Owner == s.Ground);
        var meat = E.CreateItems(s, "raw_meat", 1, ItemInstance.Carried)[0];

        var refill = Drop(s, berry, "dryslot:0");
        Assert.That((refill.Ok, refill.Slot), Is.EqualTo((true, (int?)0)));
        Assert.That(refill.Recipes, Is.EqualTo(new[] { "dry_berries" }));
        Assert.That(Drop(s, meat, "dryslot:0").Reason, Is.EqualTo(Full));
    }
}
