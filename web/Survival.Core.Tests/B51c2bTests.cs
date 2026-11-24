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
// B59a 改期望（S11 §Q1029「同步撤下常规晾制入位、取出和全部取出重复播报」）：原「把野果从临时营地／晾制架工位1取出到随身。」（B51c2b 沿火堆单件取出原句）——晾制位取出不写日志
        Assert.That(s.Log.Last().Text, Does.Not.StartWith("把野果"));
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
// B59a 改期望（S11 §Q1029「同步撤下常规晾制入位、取出和全部取出重复播报」）：原「把野果×2从临时营地／晾制架工位1取出到临时营地地面。」
        Assert.That(s.Log.Last().Text, Does.Not.StartWith("把野果"));
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
// B59a 改期望（S11 §Q1029「同步撤下常规晾制入位、取出和全部取出重复播报」）：原断投料日志含「投入余料野果×1、随身野果×1」——来源顺序已由预览 Sources 断言
        Assert.That(s.Log.Last().Text, Does.Not.Contain("投入余料"));
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

    // B59a 删除 Q926_DragToDryingSlot_EmptyPreview_OccupiedFull、Q926_DragToResidualSlot_RefillOrFull：
    // S11／S07／S14 §Q1029「合法普通拖入直接提交这次投入，取代 Q926 A 的“只预览、再确认投料”……非法物品、无容量或无操作资格的目标不高亮，松手不投放、不扣物、不弹提示」，
    // 原「拖到空位只定位食谱」「占用位拒已满」「余料位进补投预览」三条前提作废；新规则见 B59aTests（Q1033_01_*、Q1029_01_IllegalTargets_Silent_IdleEmptyOk、Q1034_02_ResidualAfterTake_DragRefill）。

    // ───────────── 变异测试补测（基线 2612268d..8dd031e0，见开发记录 B51c2b.8） ─────────────

    /// <summary>直接造一个晾制余料位（起点构造）：位号 slot、配方 recipe，位内物品另造。</summary>
    private DryingSlot ResidualSlot(GameState s, int slot, string recipe)
    {
        var b = new DryingSlot
        {
            Slot = slot, BatchId = 8800 + slot, RecipeId = recipe, Status = DryingSlot.Residual, StartMinute = s.GameMinute - 30,
            RequiredMinutes = 120, EndMinute = s.GameMinute - 10, InitiatorActorId = GameState.PlayerId,
        };
        DrySlots(s).Add(b);
        return b;
    }

    /// <summary>直接造一条陶窑余料条目（起点构造），批内物品另造。</summary>
    private StationBatch ResidualKiln(GameState s, int id)
    {
        var b = new StationBatch
        {
            BatchId = id, StationId = "kiln", RecipeId = Kiln, RequiredMinutes = 60, StartMinute = s.GameMinute - 10,
            InitiatorActorId = GameState.PlayerId, Residual = true,
        };
        E.FacOf(s, s.Ground)!.StationBatches.Add(b);
        return b;
    }

    /// <summary>在 owner 造一张卡（起点构造）；expires 给定时改写它的截止（keep 为真时连 null 也写）。</summary>
    private ItemInstance MakeIn(GameState s, string itemId, int count, string owner, int? expires = null, bool keep = false)
    {
        var made = E.CreateItems(s, itemId, count, owner);
        Assert.That(made, Has.Count.EqualTo(1));
        if (keep || expires.HasValue) made[0].ExpiresAt = expires;
        return made[0];
    }

    /// <summary>
    /// IsPreview（DropIntent 原定义）：采集工具、配方两种预览只打开面板，不提交。
    /// B59a 改期望（S11 §Q1029 取代 Q926 A）：原第三种「晾制位预览」PreviewDrying 撤下，拖到晾制位改为即时投料 LoadDrying（不是预览）。
    /// </summary>
    [Test]
    public void MT_IsPreview_AllThreeKinds()
    {
        Assert.That(new[] { DropVerb.PreviewGatherTool, DropVerb.PreviewRecipe, DropVerb.LoadDrying, DropVerb.TakeOut }
            .Select(v => new DropIntent { Verb = v }.IsPreview), Is.EqualTo(new[] { true, true, false, false }));
    }

    /// <summary>Q924 B「余料先用，再从来源补足所缺」：余料野果×1、随身野果×3，补投按原来源顺序（截止先到先用，Q606）只补 1 份：取地面那份先到期的野果，随身 3 份不动。</summary>
    [Test]
    public void MT_DryingRefill_TakesOnlyShortfall()
    {
        var s = Rack();
        var card = BerriesInSlot0(s);
        Ok(Take(s, card, 1, s.Ground));
        MakeIn(s, "berry", 3, ItemInstance.Carried, s.GameMinute + 600);
        var o = E.DryingPreview(s, "dry_berries", 0);
        Assert.That(o.Sources, Has.Count.EqualTo(2));
        Assert.That(o.Sources[1], Does.StartWith("地面野果×1（"));
        Ok(E.StartDrying(s, "dry_berries", 0, null, o.SourceKey));
        Assert.That(E.DryingContents(s, s.Ground, 0).Sum(i => i.Count), Is.EqualTo(2), "配方野果×2");
        Assert.That((Sum(s, "berry", ItemInstance.Carried), Sum(s, "berry", s.Ground)), Is.EqualTo((3, 0)));
    }

    /// <summary>
    /// Q214（预览来源变了要求再确认）＋Q924 B：预览看到的是「余料野果×1」，确认前这份余料被「取出」到地面——同一实例改从地面来，
    /// 确认按原预览拒「原料或晾制位有变化……」，不悄悄换来源。
    /// </summary>
    [Test]
    public void MT_DryingRefill_ResidualMovedToGround_PreviewChanged()
    {
        var s = Rack();
        var card = BerriesInSlot0(s);
        Ok(Take(s, card, 1, ItemInstance.Carried));
        foreach (var i in s.Items.Where(i => i.ItemId == "berry" && i.Owner == ItemInstance.Carried)) i.ExpiresAt = s.GameMinute + 600;   // 随身那份晚到期，地面余料排在前
        var o = E.DryingPreview(s, "dry_berries", 0);
        Assert.That(o.Sources[0], Does.StartWith("余料野果×1"));
        Ok(E.TakeDryingSlot(s, 0, E.DryingTakeStamp(s, s.Ground, 0)));
        var r = E.StartDrying(s, "dry_berries", 0, null, o.SourceKey);
        Assert.That((r.Ok, r.Reason), Is.EqualTo((false, GameEngine.DryingPreviewChanged)));
    }

    /// <summary>Q924 B 只有余料位才把位内物品当来源：晾制中的位预览（拒「已满」）不把位内原料列成「余料」。</summary>
    [Test]
    public void MT_Preview_InProgressSlot_NoResidualSources()
    {
        var s = Rack();
        BerriesInSlot0(s);
        MakeIn(s, "berry", 2, ItemInstance.Carried, s.GameMinute + 600);
        var o = E.DryingPreview(s, "dry_berries", 0);
        Assert.That(o.Reason, Is.EqualTo(Full));
        Assert.That(o.Sources.Count(x => x.StartsWith("余料")), Is.EqualTo(0));
    }

    /// <summary>Q924 B：余料与配方所需一样多（肉干取走盐，余生肉×1＝所需1）仍可补投，只补盐。</summary>
    [Test]
    public void MT_DryingRefillable_ResidualEqualsNeed()
    {
        var s = Rack();
        MakeIn(s, "raw_meat", 1, ItemInstance.Carried);
        MakeIn(s, "salt", 1, ItemInstance.Carried);
        Ok(E.StartDrying(s, "dry_meat"));
        var salt = E.DryingContents(s, s.Ground, 0).Single(i => i.ItemId == "salt");
        Ok(Take(s, salt, 1, ItemInstance.Carried));
        var o = E.DryingPreview(s, "dry_meat", 0);
        Assert.That(o.Ok, Is.True, o.Reason);
        Assert.That(o.Sources.Select(x => x.Substring(0, x.IndexOf('（'))), Is.EqualTo(new[] { "余料生肉×1", "随身盐×1" }));
    }

    /// <summary>
    /// Q924 B「本位对应配方合法余料」：位内有一份不是本配方原料（盐之于果干）、已到期未转换的野果、
    /// 同名合计超过所需（两份野果 1＋2＞2）、或一种超量一种合法（生肉×1＋生肉×1＋盐×1），都不能补投，按占用拒「已满」（不吞物）；对照合法余料可补投。
    /// </summary>
    [Test]
    public void MT_DryingRefillable_IllegalResidual_Full()
    {
        var s = Rack();
        MakeIn(s, "berry", 2, ItemInstance.Carried, s.GameMinute + 600);
        MakeIn(s, "raw_meat", 1, ItemInstance.Carried);
        MakeIn(s, "salt", 1, ItemInstance.Carried);

        ResidualSlot(s, 0, "dry_berries");
        MakeIn(s, "berry", 1, ItemInstance.DryingOwner(s.Ground, 0), s.GameMinute + 300);
        MakeIn(s, "salt", 1, ItemInstance.DryingOwner(s.Ground, 0));
        ResidualSlot(s, 1, "dry_berries");
        MakeIn(s, "berry", 1, ItemInstance.DryingOwner(s.Ground, 1), s.GameMinute);   // 已到期、尚未转换
        ResidualSlot(s, 2, "dry_berries");
        MakeIn(s, "berry", 1, ItemInstance.DryingOwner(s.Ground, 2), s.GameMinute + 300);
        MakeIn(s, "berry", 2, ItemInstance.DryingOwner(s.Ground, 2), s.GameMinute + 400);
        ResidualSlot(s, 3, "dry_meat");
        MakeIn(s, "raw_meat", 1, ItemInstance.DryingOwner(s.Ground, 3), s.GameMinute + 300);
        MakeIn(s, "raw_meat", 1, ItemInstance.DryingOwner(s.Ground, 3), s.GameMinute + 400);
        MakeIn(s, "salt", 1, ItemInstance.DryingOwner(s.Ground, 3));
        ResidualSlot(s, 4, "dry_berries");
        MakeIn(s, "berry", 1, ItemInstance.DryingOwner(s.Ground, 4), s.GameMinute + 300);   // 对照：合法余料

        Assert.That(E.DryingContents(s, s.Ground, 2), Has.Count.EqualTo(2), "起点：两份分开");
        Assert.That(new[] { (0, "dry_berries"), (1, "dry_berries"), (2, "dry_berries"), (3, "dry_meat"), (4, "dry_berries") }
            .Select(x => E.DryingPreview(s, x.Item2, x.Item1).Reason),
            Is.EqualTo(new string?[] { Full, Full, Full, Full, null }));
        // Q924「列清……真实数量」：超量余料位的预览来源只按配方所需列，不出「×0」
        Assert.That(E.DryingPreview(s, "dry_meat", 3).Sources.Count(x => x.Contains("×0")), Is.EqualTo(0));
    }

    /// <summary>Q879 A：余料位里的物品都没了（如被丢弃），「取出」置灰「没有可取出的物品。」。</summary>
    [Test]
    public void MT_ResidualSlot_Empty_TakeEmpty()
    {
        var s = Rack();
        ResidualSlot(s, 0, "dry_berries");
        Assert.That(E.DryingSlotTakeBlocked(s, 0), Is.EqualTo("没有可取出的物品。"));
    }

    /// <summary>
    /// 晾制架摘要：余料位占一位、计入「空{n}」的扣减。
    /// B59a 改期望（S11「2026-11-24」§Q1034「不另列余料位数或缺料提示」、S14 Q1034-02「没有……余料位数摘要」）：原「余料1 · 空7」（B51c2b 暂用余料计数）。
    /// </summary>
    [Test]
    public void MT_DryingSummary_ResidualCount()
    {
        var s = Rack();
        var card = BerriesInSlot0(s);
        Ok(Take(s, card, 1, s.Ground));
        Assert.That(E.DryingSummary(s), Is.EqualTo("空7"));
    }

    /// <summary>Q925 B：陶窑余料条目被补投换成新批后，余料卡打开时的旧身份戳不再生效，拒「物品已变，请重选。」。</summary>
    [Test]
    public void MT_KilnStamp_OldBatch_Changed()
    {
        var s = Workshop();
        KilnInputs(s, 1);
        Ok(E.StartStationBatch(s, Kiln, "k"));
        Ok(Take(s, Inputs(s, KilnBatches(s).Single()).Single(), 1, s.Ground));
        var residual = Inputs(s, KilnBatches(s).Single()).Single();
        var oldStamp = E.FacilityItemStamp(s, residual);
        E.CreateItems(s, "charcoal", 1, s.Ground);
        Ok(E.StartStationBatch(s, Kiln, "k2"));
        Assert.That(residual.Owner, Is.EqualTo(ItemInstance.StationOwner(s.Ground, KilnBatches(s).Single().BatchId)), "余料进了新批");
        Assert.That(Take(s, residual, 1, s.Ground, oldStamp).Reason, Is.EqualTo(Changed));
        Assert.That(Sum(s, "clay", s.Ground), Is.EqualTo(0), "什么都不动");
    }

    /// <summary>Q925 B：所属晾制位／设施批记录已不在的物品（异常残留）不能按设施物品卡取出，拒「物品已变，请重选。」；不存在的卡同样拒。</summary>
    [Test]
    public void MT_FacilityItem_NoRecord_Changed()
    {
        var s = Rack();
        var dry = MakeIn(s, "berry", 1, ItemInstance.DryingOwner(s.Ground, 3), s.GameMinute + 300);
        var w = Workshop();
        var st = MakeIn(w, "clay", 1, ItemInstance.StationOwner(w.Ground, 99999));
        Assert.That((E.FacilityItemGate(s, dry), E.FacilityItemGate(w, st)), Is.EqualTo((Changed, Changed)));
        Assert.That(Take(s, dry, 1, s.Ground).Reason, Is.EqualTo(Changed));
        Assert.That(E.DoInstant(s, InstantRequest.FacilityTake(987654, 1, s.Ground, "x")).Reason, Is.EqualTo(Changed), "卡已不在");
        Assert.That(dry.Owner, Is.EqualTo(ItemInstance.DryingOwner(s.Ground, 3)));
    }

    /// <summary>
    /// S07 数量规则（FAC-Q870-03）：取到随身的上限按随身容量（已满五张卡为 0），取到当前地面的上限是这张卡全部份数；
    /// 随身容量失败带回可取上限 0，门槛失败（行动进行中）不带上限。
    /// </summary>
    [Test]
    public void MT_FacilityItemMax_ByZone_AndFailMax()
    {
        var s = Rack();
        var card = BerriesInSlot0(s);
        foreach (var id in new[] { "wood", "stone", "rope", "plank", "cloth" }) E.CreateItems(s, id, 1, ItemInstance.Carried);
        Assert.That((E.FacilityItemMax(s, card, ItemInstance.Carried), E.FacilityItemMax(s, card, s.Ground)), Is.EqualTo((0, 2)));
        Assert.That(Take(s, card, 1, ItemInstance.Carried).MaxCount, Is.EqualTo(0));
        Start(s, ActionRequest.Wait(30));
        var busy = Take(s, card, 1, s.Ground);
        Assert.That((busy.Reason, busy.MaxCount), Is.EqualTo((Busy, (int?)null)));
    }

    /// <summary>去向只认随身或当前地面（原句「只能取到随身或当前地面」）；份数小于 1 拒原句「数量无效」；拖到别处拒「只能放到随身或当前地面」。</summary>
    [Test]
    public void MT_FacilityTake_BadZoneOrCount()
    {
        var s = Rack();
        var card = BerriesInSlot0(s);
        Assert.That(Take(s, card, 1, "elsewhere").Reason, Is.EqualTo("只能取到随身或当前地面"));
        var zero = Take(s, card, 0, s.Ground);
        Assert.That((zero.Reason, zero.MaxCount), Is.EqualTo(("数量无效", (int?)null)), "取到地面不受容量限制，失败不带可取上限");
        Assert.That(Drop(s, card, "zone:elsewhere").Reason, Is.EqualTo("只能放到随身或当前地面"));
        Assert.That(card.Count, Is.EqualTo(2));
    }

    /// <summary>
    /// Q880「原料已腐败。」只挡晾制中的位：在制原料已到期（旧档未暂停）拒取；余料位里到期未转换的野果可以取出（取出后同一边界转为腐败物，
    /// 结果沿原转换句；S06 Q610 每个变更边界做一次到期转换）。
    /// </summary>
    [Test]
    public void MT_SpoiledInput_OnlyBlocksDrying_ResidualTakeConverts()
    {
        var s = Rack();
        var card = BerriesInSlot0(s);
        card.ShelfPausedMinutes = null;
        card.ExpiresAt = s.GameMinute;
        Assert.That(E.FacilityItemTakeBlocked(s, card, 1, s.Ground), Is.EqualTo("原料已腐败。"));

        ResidualSlot(s, 1, "dry_berries");
        var old = MakeIn(s, "berry", 1, ItemInstance.DryingOwner(s.Ground, 1), s.GameMinute);
        Assert.That(E.FacilityItemTakeBlocked(s, old, 1, s.Ground), Is.Null);
        Ok(Take(s, old, 1, s.Ground));
        var ground = s.Items.Where(i => i.Owner == s.Ground).ToList();
        Assert.That(ground.Select(i => (i.ItemId, i.Count)), Is.EqualTo(new[] { (GameEngine.SpoiledMatterId, 1) }), "落地即转为腐败物");
        Assert.That(s.Log.Select(l => l.Text), Does.Contain("野果×1已腐败，变为腐败物×1。"));
        Assert.That(DrySlots(s).Any(x => x.Slot == 1), Is.False, "位取空即空出");
    }

    /// <summary>落地合堆（同单件取出）：同一位先后两次各取 1 份到地面，同期限的两份在地面合成一堆野果×2。</summary>
    [Test]
    public void MT_FacilityTake_MergesOnGround()
    {
        var s = Rack();
        var card = BerriesInSlot0(s);
        Ok(Take(s, card, 1, s.Ground));
        var rest = E.DryingContents(s, s.Ground, 0).Single();
        Ok(Take(s, rest, 1, s.Ground));
        var ground = s.Items.Where(i => i.ItemId == "berry" && i.Owner == s.Ground).ToList();
        Assert.That(ground.Select(i => i.Count), Is.EqualTo(new[] { 2 }));
    }

    /// <summary>FacilityItems 文档「已晾好／已停止的位只是少了物品」（S11 §Q870 B 只结束在制批）：停止位（缺料）取走一份，仍是停止位、不改成余料。</summary>
    [Test]
    public void MT_StoppedSlot_PartialTake_StaysStopped()
    {
        var s = Rack();
        DrySlots(s).Add(new DryingSlot
        {
            Slot = 0, BatchId = 7300, RecipeId = "dry_berries", Status = DryingSlot.Spoiled, StartMinute = s.GameMinute - 60, DoneMinutes = 50,
            RequiredMinutes = 120, EndMinute = s.GameMinute - 5, InitiatorActorId = GameState.PlayerId, StopReason = DryingSlot.StopMissing,
        });
        var berry = MakeIn(s, "berry", 2, ItemInstance.DryingOwner(s.Ground, 0), s.GameMinute + 300);
        Ok(Take(s, berry, 1, s.Ground));
        Assert.That(DrySlots(s).Select(x => x.Status), Is.EqualTo(new[] { DryingSlot.Spoiled }));
    }

    /// <summary>FAC-Q870-04（陶窑）：批内原料带暂停保质（R＝100）时，取走 1 份后余料同样恢复 C＋R；整批取空则这条记录删掉。</summary>
    [Test]
    public void MT_KilnResidual_ResumesCR_EmptyRemoved()
    {
        var s = Workshop();
        MakeIn(s, "clay", 2, s.Ground, s.GameMinute + 100);
        E.CreateItems(s, "charcoal", 1, s.Ground);
        Ok(E.StartStationBatch(s, Kiln, "k"));
        var b = KilnBatches(s).Single();
        var inBatch = Inputs(s, b).Single();
        Assert.That(inBatch.ShelfPausedMinutes, Is.EqualTo(100), "起点：开工暂停保质");
        AdvanceTo(s, s.GameMinute + 30);
        var c = s.GameMinute;
        Ok(Take(s, inBatch, 1, s.Ground));
        var rest = Inputs(s, b).Single();
        Assert.That((rest.Count, rest.ExpiresAt, rest.ShelfPausedMinutes), Is.EqualTo((1, (int?)(c + 100), (int?)null)), "余料恢复 C＋R");
        Ok(Take(s, rest, 1, s.Ground));
        Assert.That(KilnBatches(s), Is.Empty, "取空即删掉这条");
    }

    /// <summary>
    /// 晾制位落点：位号只认 0—7；位号非法、当前区没有晾制架、火堆上的食物、已到期的材料都不接收。
    /// B59a 改期望（S11 §Q1029「非法物品、无容量或无操作资格的目标不高亮，松手不投放、不扣物、不弹提示」）：原各给拒绝句（「暂不支持此组合」「这批材料不可用」
    /// 「临时营地没有晾制架（只能操作当前区的晾制架）」）——现在一律静默（Silent、原因为空）。
    /// </summary>
    [Test]
    public void MT_DropOnDryingSlot_Guards()
    {
        var s = Rack();
        var berry = MakeIn(s, "berry", 1, ItemInstance.Carried, s.GameMinute + 600);
        Assert.That(new[] { "dryslot:8", "dryslot:-1", "dryslot:x" }.Select(t => Drop(s, berry, t)).Select(d => (d.Ok, d.Silent, d.Reason)),
            Is.All.EqualTo((false, true, "")));
        Assert.That(Drop(s, berry, "dryslot:7").Ok, Is.True, "对照：第 8 位");

        var old = MakeIn(s, "berry", 1, s.Ground, s.GameMinute);   // 已到期未转换
        Assert.That((Drop(s, old, "dryslot:1").Silent, Drop(s, old, "dryslot:1").Reason), Is.EqualTo((true, "")));

        var meat = MakeIn(s, "raw_meat", 1, s.Ground);
        Ok(E.DoInstant(s, InstantRequest.PutInFireSlot(meat.InstanceId, 0)));
        var onFire = s.Items.Single(i => i.ItemId == "raw_meat" && i.InFacility);
        Assert.That((Drop(s, onFire, "dryslot:1").Silent, Drop(s, onFire, "dryslot:1").Reason), Is.EqualTo((true, "")));

        var w = Workshop();
        var b2 = MakeIn(w, "berry", 1, ItemInstance.Carried, w.GameMinute + 600);
        Assert.That((Drop(w, b2, "dryslot:0").Silent, Drop(w, b2, "dryslot:0").Reason), Is.EqualTo((true, "")));
    }

    /// <summary>
    /// 到期转换的身边日志（S06 Q610；B50a「在办工作的输入不写普通句」）：余料位里的野果、已晾好位里的果干到期都写普通转换句；
    /// 对照：晾制中位里未暂停的旧档原料到期由停止句接分句、缺料停止位里的原料（WorkOwned 原注释「含已腐败停止待清理」）都属在办工作，不写普通句。
    /// </summary>
    [Test]
    public void MT_SpoilLog_ResidualAndDone_Normal_DryingWork()
    {
        var s = Rack();
        ResidualSlot(s, 0, "dry_berries");
        MakeIn(s, "berry", 1, ItemInstance.DryingOwner(s.Ground, 0), s.GameMinute + 10);
        DrySlots(s).Add(new DryingSlot
        {
            Slot = 1, BatchId = 7401, RecipeId = "dry_berries", Status = DryingSlot.Done, StartMinute = s.GameMinute - 200, DoneMinutes = 120,
            RequiredMinutes = 120, EndMinute = s.GameMinute - 80, InitiatorActorId = GameState.PlayerId,
        });
        MakeIn(s, "dried_berries", 1, ItemInstance.DryingOwner(s.Ground, 1), s.GameMinute + 10);
        MakeIn(s, "raw_meat", 1, ItemInstance.Carried);
        MakeIn(s, "salt", 1, ItemInstance.Carried);
        Ok(E.StartDrying(s, "dry_meat", 2));
        var meat = E.DryingContents(s, s.Ground, 2).Single(i => i.ItemId == "raw_meat");
        meat.ShelfPausedMinutes = null;   // 旧档：未暂停
        meat.ExpiresAt = s.GameMinute + 10;
        DrySlots(s).Single(x => x.Slot == 2).ShelfPaused = false;
        DrySlots(s).Add(new DryingSlot
        {
            Slot = 3, BatchId = 7403, RecipeId = "dry_meat", Status = DryingSlot.Spoiled, StartMinute = s.GameMinute - 60, DoneMinutes = 50,
            RequiredMinutes = 180, EndMinute = s.GameMinute - 5, InitiatorActorId = GameState.PlayerId, StopReason = DryingSlot.StopMissing,
        });
        MakeIn(s, "raw_meat", 1, ItemInstance.DryingOwner(s.Ground, 3), s.GameMinute + 10);   // 缺料停止位里的生肉

        AdvanceTo(s, s.GameMinute + 30);

        var texts = s.Log.Select(l => l.Text).ToList();
        Assert.That(texts, Does.Contain("野果×1已腐败，变为腐败物×1。"), "余料位");
        Assert.That(texts, Does.Contain("果干×1已腐败，变为腐败物×1。"), "已晾好位");
        Assert.That(texts, Does.Not.Contain("生肉×1已腐败，变为腐败物×1。"), "晾制中在办原料（由停止句接分句）、停止待取出位里的原料都不写普通句");
        // B59a 改期望（S11 §Q1029 撤自动停止日志「回到当地取出」尾句）：原「…不给经验；原料留在这一位，回到当地取出。腐败部分已变为腐败物。」
        Assert.That(texts, Does.Contain("临时营地晾制架工位3：生肉已腐败，晾制肉干停止，没有成品、不给经验。腐败部分已变为腐败物。"), "对照：在办原料由停止句说明");
    }

    /// <summary>
    /// Q924 B 补投预览：余料已够配方所需时补足来源只列燃料（不列「黏土×0」）；两条余料条目同名合量（S11 Q876 B 清单写法）。
    /// </summary>
    [Test]
    public void MT_StationRefillText_FullResidual_AndMerged()
    {
        var s = Workshop();
        var rc = Cfg.Recipes.Single(r => r.Id == Kiln);
        var b = ResidualKiln(s, 9001);
        MakeIn(s, "clay", 2, ItemInstance.StationOwner(s.Ground, b.BatchId));
        E.CreateItems(s, "charcoal", 1, s.Ground);
        Assert.That(E.StationRefillText(s, rc, 1), Is.EqualTo("先用余料黏土×2，再从随身与当前地面取木炭×1"), "黏土不写×0");

        var w = Workshop();
        KilnInputs(w, 2);
        Ok(E.StartStationBatch(w, Kiln, "k", 2));
        foreach (var x in KilnBatches(w)) Ok(Take(w, Inputs(w, x).Single(), 1, w.Ground));
        Assert.That(E.StationRefillText(w, rc, 2), Is.EqualTo("先用余料黏土×2，再从随身与当前地面取黏土×2、木炭×2"));
    }

    /// <summary>净水台（无燃料）余料已齐：预览只写「先用余料脏水×20、滤材×1」，不接补足来源。</summary>
    [Test]
    public void MT_StationRefillText_NothingMore()
    {
        var s = Workshop();
        E.DebugBuildAllWorkshops(s);
        var rc = Cfg.Recipes.Single(r => r.Id == "ex_station_water");
        var b = new StationBatch
        {
            BatchId = 9101, StationId = rc.Station, RecipeId = rc.Id, RequiredMinutes = 30, StartMinute = s.GameMinute - 10,
            InitiatorActorId = GameState.PlayerId, Residual = true,
        };
        E.FacOf(s, s.Ground)!.StationBatches.Add(b);
        MakeIn(s, "dirty_water", 20, ItemInstance.StationOwner(s.Ground, b.BatchId));
        MakeIn(s, "filter_media", 1, ItemInstance.StationOwner(s.Ground, b.BatchId));
        Assert.That(E.StationRefillText(s, rc, 1), Is.EqualTo("先用余料脏水×20、滤材×1"));
    }

    /// <summary>
    /// Q924 B（陶窑）「本位对应配方合法余料」：余料条目里有非原料（石头）、已到期的黏土、同名合计超量（1＋2＞2）都不能复用；对照合法余料可复用。
    /// </summary>
    [Test]
    public void MT_StationRefillable_IllegalResidual()
    {
        var s = Workshop();
        var rc = Cfg.Recipes.Single(r => r.Id == Kiln);
        var a = ResidualKiln(s, 9201);
        MakeIn(s, "clay", 1, ItemInstance.StationOwner(s.Ground, a.BatchId));
        MakeIn(s, "stone", 1, ItemInstance.StationOwner(s.Ground, a.BatchId));
        var bb = ResidualKiln(s, 9202);
        MakeIn(s, "clay", 1, ItemInstance.StationOwner(s.Ground, bb.BatchId), s.GameMinute);   // 已到期
        var c = ResidualKiln(s, 9203);
        MakeIn(s, "clay", 1, ItemInstance.StationOwner(s.Ground, c.BatchId), s.GameMinute + 500);
        MakeIn(s, "clay", 2, ItemInstance.StationOwner(s.Ground, c.BatchId));
        Assert.That(Inputs(s, c), Has.Count.EqualTo(2), "起点：两份分开");
        Assert.That(E.StationReusable(s, rc), Is.Empty);
        var ok = ResidualKiln(s, 9204);
        MakeIn(s, "clay", 1, ItemInstance.StationOwner(s.Ground, ok.BatchId));
        Assert.That(E.StationReusable(s, rc).Select(x => x.BatchId), Is.EqualTo(new[] { 9204 }), "对照");
    }

    /// <summary>Q924 B（净水台）：一种超量（脏水×21＞20）、一种合法（滤材×1）的余料条目不能复用；对照恰好够（脏水×20）可复用。</summary>
    [Test]
    public void MT_StationRefillable_WaterOneOver()
    {
        var s = Workshop();
        E.DebugBuildAllWorkshops(s);
        var rc = Cfg.Recipes.Single(r => r.Id == "ex_station_water");
        foreach (var (id, water) in new[] { (9301, 21), (9302, 20) })
        {
            E.FacOf(s, s.Ground)!.StationBatches.Add(new StationBatch
            {
                BatchId = id, StationId = rc.Station, RecipeId = rc.Id, RequiredMinutes = 30, StartMinute = s.GameMinute - 10,
                InitiatorActorId = GameState.PlayerId, Residual = true,
            });
            MakeIn(s, "dirty_water", water, ItemInstance.StationOwner(s.Ground, id));
            MakeIn(s, "filter_media", 1, ItemInstance.StationOwner(s.Ground, id));
        }
        Assert.That(E.StationReusable(s, rc).Select(x => x.BatchId), Is.EqualTo(new[] { 9302 }));
    }

    /// <summary>Q924 B（陶窑）余料先用：补投只从地面取所缺的 1 份黏土（取出的 1 份放回地面后又添 3 份，地面 4 份，补投后剩 3）。</summary>
    [Test]
    public void MT_KilnRefill_TakesOnlyShortfall()
    {
        var s = Workshop();
        KilnInputs(s, 1);
        Ok(E.StartStationBatch(s, Kiln, "k"));
        Ok(Take(s, Inputs(s, KilnBatches(s).Single()).Single(), 1, s.Ground));
        E.CreateItems(s, "clay", 3, s.Ground);
        E.CreateItems(s, "charcoal", 1, s.Ground);
        Ok(E.StartStationBatch(s, Kiln, "k2"));
        Assert.That(Sum(s, "clay", s.Ground), Is.EqualTo(3));
        Assert.That(Inputs(s, KilnBatches(s).Single()).Sum(i => i.Count), Is.EqualTo(2));
    }

    /// <summary>Q870 B「只数真实正在加工的批」：两批在加工、一条余料，加工中批数为 2。</summary>
    [Test]
    public void MT_StationProcessingCount_ExcludesResidual()
    {
        var s = Workshop();
        KilnInputs(s, 3);
        Ok(E.StartStationBatch(s, Kiln, "k", 3));
        Ok(Take(s, Inputs(s, KilnBatches(s)[0]).Single(), 1, s.Ground));
        Assert.That(E.StationProcessingCount(s, "kiln"), Is.EqualTo(2));
    }

    // ───────────── B51c2b.10 试玩遗留修复（2026-11-24） ─────────────

    /// <summary>
    /// 验收 B51C2A1-Q1007-01（S11／S14 §Q1007 A：位置字段有展示的详情和悬停处，陶窑与固定净水台的批内原料用“{区域}／{设施名}”，例“临时营地／陶窑”，省去批次序号）：
    /// 右栏来源行对第二批的黏土整行写「临时营地／陶窑」，不再只写区名「临时营地」，也不带批次序号。
    /// </summary>
    [Test]
    public void Q1007_RailSourceLine_KilnInput_ZoneSlashFacility()
    {
        var s = Workshop();
        KilnInputs(s, 2);
        Ok(E.StartStationBatch(s, Kiln, "k", 2));
        var second = Inputs(s, KilnBatches(s)[1]).Single();
        Assert.That(E.RailSourceLine(s, second), Is.EqualTo("临时营地／陶窑"));
    }

    /// <summary>
    /// 配对（Q1007 只管陶窑与固定净水台；其余沿 S14 Q589「{真实地点}[ · {区域}] · {真实持有人或来源}」）：晾制位里的野果来源行仍是「临时营地 · 晾制架工位1」。
    /// </summary>
    [Test]
    public void Q1007_RailSourceLine_DryingInput_Unchanged()
    {
        var s = Rack();
        var berry = BerriesInSlot0(s);
        Assert.That(E.RailSourceLine(s, berry), Is.EqualTo("临时营地 · 晾制架工位1"));
    }
}
