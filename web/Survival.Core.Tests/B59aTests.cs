using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 批次 B59a「晾制架拖入即投料、取出收敛、火源和锅槽位静默」：
/// S11／S07／S09／S10／S14「2026-11-19 B51c2a2原批Q1028—Q1030」§Q1028、§Q1029（验收 B51C2A2-Q1028-01／02、B51C2A2-Q1029-01／02，
/// 合同 assets/20261119/b51c2a2-q1028-q1030-facility-interaction.json fireAndPot／dryingRack）；
/// S11／S14「2026-11-24 Q1033–Q1039」§Q1033–Q1035（验收 Q1033-01／02、Q1034-01／02、Q1035-01）。
/// 期望字面量均注明出处；起点只用 CreateItems／直接造位记录，结论都走真实拖放解析、即时提交与取出入口。
/// </summary>
public class B59aTests : TestBase
{
    protected override bool NoEnemyApproach => true;

    private void Ok(ActionResult r) => Assert.That(r.Ok, Is.True, r.Reason);

    private static List<DryingSlot> DrySlots(GameState s) => s.Locations[s.Ground].Facility.DryingSlots;

    private static int Sum(GameState s, string itemId, string owner) => s.Items.Where(i => i.ItemId == itemId && i.Owner == owner).Sum(i => i.Count);

    private static string Slot(GameState s, int k) => ItemInstance.DryingOwner(s.Ground, k);

    /// <summary>临时营地建好晾制架（同 B51c2b 起点），清掉地面与随身物品，烹饪 5 级（肉干门槛）。</summary>
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

    private ItemInstance Make(GameState s, string itemId, int count, string owner, int? expires = null)
    {
        var made = E.CreateItems(s, itemId, count, owner);
        Assert.That(made, Has.Count.EqualTo(1));
        if (expires.HasValue) made[0].ExpiresAt = expires;
        return made[0];
    }

    private DropIntent Drop(GameState s, ItemInstance i, string target) =>
        E.ResolveDrop(s, DropSource.Parse($"card:{i.Owner}|{i.ItemId}|{i.InstanceId}")!, DropTarget.Parse(target)!);

    /// <summary>按落点解析给出的即时请求提交 count 份（同网页壳 ExecuteDrop：count 写进请求，带请求号）。</summary>
    private ActionResult Commit(GameState s, DropIntent intent, int count)
    {
        var r = intent.Instant!;
        r.Count = count;
        r.RequestId = Guid.NewGuid().ToString("N");
        return E.DoInstant(s, r);
    }

    private static void AssertSilent(DropIntent d, string what) =>
        Assert.That((d.Ok, d.Silent, d.Reason), Is.EqualTo((false, true, "")), what + "：静默（不高亮、不转移、不提示）");

    // ───────────── Q1033 A：只用手中这一堆，默认投缺口与可投的较小值 ─────────────

    /// <summary>
    /// Q1033-01：果干缺野果两份（配置 dry_berries 野果×2），拖野果三份到空位——落点即投（取代 Q926 A 的预览），默认 2 份、Shift 上限 2；
    /// 松手实际进位 2 份、源堆余 1 份，齐料即开始晾制（进度 0、暂停保质 R＝原截止−现在，S11 Q666），不写常规入位日志（§Q1029）。
    /// </summary>
    [Test]
    public void Q1033_01_ThreeBerries_DefaultTwo_StartsDrying()
    {
        var s = Rack();
        var berry = Make(s, "berry", 3, ItemInstance.Carried, s.GameMinute + 600);
        var logs = s.Log.Count;

        var d = Drop(s, berry, "dryslot:0");
        Assert.That((d.Ok, d.Verb, d.IsPreview, d.DefaultQuantity, d.MaxQuantity, d.Quantifiable), Is.EqualTo((true, DropVerb.LoadDrying, false, 2, 2, true)));
        Assert.That(d.Label, Is.EqualTo("投入野果×2"), "暂用落点短句（开发记录 B59a.6）");

        Ok(Commit(s, d, d.DefaultQuantity));

        var b = DrySlots(s).Single();
        Assert.That((b.Slot, b.Status, b.RecipeId, b.DoneMinutes, b.StartMinute), Is.EqualTo((0, DryingSlot.Drying, "dry_berries", 0, s.GameMinute)));
        var inSlot = E.DryingContents(s, s.Ground, 0);
        Assert.That(inSlot.Sum(i => i.Count), Is.EqualTo(2), "实际进位两份");
        Assert.That(inSlot.All(i => i.ShelfPausedMinutes == 600 && i.ExpiresAt == null), Is.True, "齐料开工即暂停保质（Q666，R＝600）");
        Assert.That(Sum(s, "berry", ItemInstance.Carried), Is.EqualTo(1), "源堆余一份");
        Assert.That(s.Log.Count, Is.EqualTo(logs), "§Q1029 撤常规晾制入位播报");
    }

    /// <summary>Q1033-01「Shift 可只投一份」：先投 1 份——位里留野果×1（余料，未开工），再拖同堆默认只补所缺 1 份，齐料即开工。</summary>
    [Test]
    public void Q1033_01_ShiftOne_ThenTopUp()
    {
        var s = Rack();
        var berry = Make(s, "berry", 3, ItemInstance.Carried, s.GameMinute + 600);
        Ok(Commit(s, Drop(s, berry, "dryslot:0"), 1));

        var b = DrySlots(s).Single();
        Assert.That((b.Status, b.RecipeId), Is.EqualTo((DryingSlot.Residual, "dry_berries")), "未齐：留在位里，与余料同一种（§Q1034）");
        Assert.That(Sum(s, "berry", Slot(s, 0)), Is.EqualTo(1));

        var again = Drop(s, berry, "dryslot:0");
        Assert.That((again.Ok, again.DefaultQuantity, again.MaxQuantity), Is.EqualTo((true, 1, 1)), "只补本项还缺的 1 份");
        Ok(Commit(s, again, 1));
        Assert.That(DrySlots(s).Single().Status, Is.EqualTo(DryingSlot.Drying));
        Assert.That(Sum(s, "berry", ItemInstance.Carried), Is.EqualTo(1));
    }

    /// <summary>
    /// Q1033-02：只拖生肉（配置 dry_meat 生肉×1＋盐×1）——生肉实际留在架内等盐，地面的盐不被自动扣走；推过原晾制时长也不开工、不出肉干；
    /// 再合法拖入盐即开始晾制，按原时长（180 分钟）完成出肉干。
    /// </summary>
    [Test]
    public void Q1033_02_MeatOnly_Waits_SaltNotAutoTaken()
    {
        var s = Rack();
        var meat = Make(s, "raw_meat", 1, ItemInstance.Carried);
        var salt = Make(s, "salt", 1, s.Ground);

        Ok(Commit(s, Drop(s, meat, "dryslot:2"), 1));
        Assert.That(DrySlots(s).Single().Status, Is.EqualTo(DryingSlot.Residual));
        Assert.That(Sum(s, "raw_meat", Slot(s, 2)), Is.EqualTo(1));
        Assert.That(Sum(s, "salt", s.Ground), Is.EqualTo(1), "不从别处自动配齐");

        AdvanceTo(s, s.GameMinute + 200);
        Assert.That(DrySlots(s).Single().Status, Is.EqualTo(DryingSlot.Residual), "缺盐不开工");
        Assert.That(s.Items.Count(i => i.ItemId == "dried_meat"), Is.EqualTo(0));

        Ok(Commit(s, Drop(s, salt, "dryslot:2"), 1));
        Assert.That(DrySlots(s).Single().Status, Is.EqualTo(DryingSlot.Drying), "齐料即开工");
        AdvanceTo(s, s.GameMinute + 180);
        Assert.That(DrySlots(s).Single().Status, Is.EqualTo(DryingSlot.Done));
        Assert.That(Sum(s, "dried_meat", Slot(s, 2)), Is.EqualTo(1));
    }

    // ───────────── Q1029：非法静默 ─────────────

    /// <summary>
    /// B51C2A2-Q1029-01「非法物或满位松手无转移、无额外提示」：拖到晾制中的位、不是晾制原料（木材）、本项已齐（生肉位再拖生肉）、
    /// 别的配方原料（野果拖到生肉位）、行动进行中——全部静默（Ok＝false、Silent、原因为空）；对照：空闲时同一份野果拖到空位可投。
    /// </summary>
    [Test]
    public void Q1029_01_IllegalTargets_Silent_IdleEmptyOk()
    {
        var s = Rack();
        Ok(Commit(s, Drop(s, Make(s, "berry", 2, ItemInstance.Carried, s.GameMinute + 600), "dryslot:0"), 2));
        Ok(Commit(s, Drop(s, Make(s, "raw_meat", 1, ItemInstance.Carried), "dryslot:1"), 1));
        var berry = Make(s, "berry", 1, s.Ground, s.GameMinute + 900);
        var wood = Make(s, "wood", 1, s.Ground);
        var meat2 = Make(s, "raw_meat", 1, s.Ground);

        AssertSilent(Drop(s, berry, "dryslot:0"), "晾制中的位");
        AssertSilent(Drop(s, wood, "dryslot:3"), "不是晾制原料");
        AssertSilent(Drop(s, meat2, "dryslot:1"), "本项已齐");
        AssertSilent(Drop(s, berry, "dryslot:1"), "别的配方原料");
        Assert.That(Drop(s, berry, "dryslot:3").Ok, Is.True, "对照：空闲、空位可投");

        Start(s, ActionRequest.Wait(30));
        AssertSilent(Drop(s, berry, "dryslot:3"), "行动进行中（无操作资格）");
        var r = E.DoInstant(s, InstantRequest.DryingDrop(berry.InstanceId, 3, "dry_berries", 1));
        Assert.That((r.Ok, r.Reason), Is.EqualTo((false, "")), "提交同样静默拒绝");
        Assert.That(berry.Owner, Is.EqualTo(s.Ground), "不扣物");
    }

    /// <summary>
    /// B51C2A2-Q1029-02「空异常位恢复为空槽」：没有物品的停止位记录在下一个变更边界自动删掉、成普通空位；对照：有物品的停止位照实保留（卡片取出）。
    /// </summary>
    [Test]
    public void Q1029_02_EmptyStoppedSlot_AutoRestored()
    {
        var s = Rack();
        foreach (var k in new[] { 4, 5 })
            DrySlots(s).Add(new DryingSlot
            {
                Slot = k, BatchId = 9100 + k, RecipeId = "dry_berries", Status = DryingSlot.Spoiled, StartMinute = s.GameMinute - 60,
                RequiredMinutes = 120, EndMinute = s.GameMinute - 5, StopReason = DryingSlot.StopMissing,
            });
        Make(s, "berry", 1, Slot(s, 5), s.GameMinute + 300);

        E.UpdateFlags(s);

        Assert.That(DrySlots(s).Select(b => b.Slot), Is.EqualTo(new[] { 5 }), "空停止位恢复成空位；有物的停止位保留");
        Assert.That(E.DryingSlotViews(s)[4].Status, Is.EqualTo("empty"));
    }

    /// <summary>
    /// §Q1029「同步撤下常规晾制入位、取出和全部取出重复播报」：「查看配方」确认投料、点卡取出、全部取出都不写日志，结果由卡片与槽位表达。
    /// （对照：陶窑批内卡取出仍写原句，见 B51c2bTests.Q870_KilnCard_PartialTake_ResidualEntry_OtherBatchUntouched。）
    /// </summary>
    [Test]
    public void Q1029_NoRoutineDryingLogs()
    {
        var s = Rack();
        Make(s, "berry", 4, ItemInstance.Carried, s.GameMinute + 600);
        var logs = s.Log.Count;
        Ok(E.StartDrying(s, "dry_berries"));
        Ok(E.StartDrying(s, "dry_berries"));
        Assert.That(DrySlots(s), Has.Count.EqualTo(2), "起点：两位在晾");
        var card = E.DryingContents(s, s.Ground, 0).First();
        Ok(E.DoInstant(s, InstantRequest.FacilityTake(card.InstanceId, 1, s.Ground, E.FacilityItemStamp(s, card))));
        Assert.That(Sum(s, "berry", s.Ground), Is.EqualTo(1), "取出确实发生");
        Ok(E.TakeAll(s, s.Ground, GameEngine.DryingStationId, E.TakeAllStamp(s, s.Ground, GameEngine.DryingStationId)));
        Assert.That((DrySlots(s).Count, Sum(s, "berry", s.Ground)), Is.EqualTo((0, 4)), "全部取出确实发生");
        Assert.That(s.Log.Count, Is.EqualTo(logs), "三种常规晾制转移都不写日志");
    }

    /// <summary>§Q1029：自动停止日志撤「原料留在这一位，回到当地取出」尾句（B51c2a2 暂用）——晾制中的位缺料停止，日志只到「没有成品、不给经验。」。</summary>
    [Test]
    public void Q1029_StopLog_NoReturnToTakeTail()
    {
        var s = Rack();
        Make(s, "berry", 2, ItemInstance.Carried, s.GameMinute + 600);
        Ok(E.StartDrying(s, "dry_berries"));
        foreach (var i in E.DryingContents(s, s.Ground, 0)) s.Items.Remove(i);   // 起点：位内原料异常缺失（开发护栏，S11 Q769「开发内部缺失输入护栏可保留」）
        AdvanceTo(s, s.GameMinute + 5);
        Assert.That(s.Log.Select(l => l.Text), Does.Contain("临时营地晾制架工位1：原料不全，晾制果干停止，没有成品、不给经验。"));
    }

    // ───────────── Q1034：收起标题与摘要 ─────────────

    /// <summary>
    /// Q1034-01：占用位收起时标题「{首件物品名}等」（展开顺序＝位内实例顺序）；首件拿走后按真实列表重算；空位为 null（沿原空槽）。
    /// </summary>
    [Test]
    public void Q1034_01_CollapsedTitle_FirstItem_Recomputes()
    {
        var s = Rack();
        Ok(Commit(s, Drop(s, Make(s, "raw_meat", 1, ItemInstance.Carried), "dryslot:0"), 1));
        Ok(Commit(s, Drop(s, Make(s, "salt", 1, ItemInstance.Carried), "dryslot:0"), 1));
        var v = E.DryingSlotViews(s);
        Assert.That((v[0].CollapsedTitle, v[1].CollapsedTitle), Is.EqualTo(("生肉等", (string?)null)));

        var meat = E.DryingContents(s, s.Ground, 0).Single(i => i.ItemId == "raw_meat");
        Ok(E.DoInstant(s, InstantRequest.FacilityTake(meat.InstanceId, 1, s.Ground, E.FacilityItemStamp(s, meat))));
        Assert.That(E.DryingSlotViews(s)[0].CollapsedTitle, Is.EqualTo("盐等"), "首件移出后随真实列表更新");
        var salt = E.DryingContents(s, s.Ground, 0).Single();
        Ok(E.DoInstant(s, InstantRequest.FacilityTake(salt.InstanceId, 1, s.Ground, E.FacilityItemStamp(s, salt))));
        Assert.That((E.DryingSlotViews(s)[0].Status, E.DryingSlotViews(s)[0].CollapsedTitle), Is.EqualTo(("empty", (string?)null)), "空栏归原空态");
    }

    /// <summary>Q1034-02「没有额外缺料提示、异常计数或余料位数摘要」：余料位不在晾制架摘要计数（撤 B51c2b 暂用「余料{n}」）；对照：晾制中照常计。</summary>
    [Test]
    public void Q1034_02_Summary_NoResidualCount()
    {
        var s = Rack();
        Ok(Commit(s, Drop(s, Make(s, "raw_meat", 1, ItemInstance.Carried), "dryslot:0"), 1));
        Assert.That(E.DryingSummary(s), Is.EqualTo("空7"), "余料位不另计（八位占一位）");
        Ok(Commit(s, Drop(s, Make(s, "berry", 2, ItemInstance.Carried, s.GameMinute + 600), "dryslot:1"), 2));
        Assert.That(E.DryingSummary(s), Is.EqualTo("晾制中1 · 空6"));
    }

    /// <summary>Q1034-02「部分取出后的余料能继续拖补」：在制位取走 1 份野果成余料，再拖野果进来补齐即重新开工（新批 ID、进度 0）。</summary>
    [Test]
    public void Q1034_02_ResidualAfterTake_DragRefill()
    {
        var s = Rack();
        Ok(Commit(s, Drop(s, Make(s, "berry", 2, ItemInstance.Carried, s.GameMinute + 600), "dryslot:0"), 2));
        AdvanceTo(s, s.GameMinute + 30);
        var old = DrySlots(s).Single().BatchId;
        var card = E.DryingContents(s, s.Ground, 0).Single();
        Ok(E.DoInstant(s, InstantRequest.FacilityTake(card.InstanceId, 1, ItemInstance.Carried, E.FacilityItemStamp(s, card))));
        Assert.That(DrySlots(s).Single().Status, Is.EqualTo(DryingSlot.Residual));

        var back = s.Items.Single(i => i.ItemId == "berry" && i.Owner == ItemInstance.Carried);
        var d = Drop(s, back, "dryslot:0");
        Assert.That((d.Ok, d.DefaultQuantity), Is.EqualTo((true, 1)));
        Ok(Commit(s, d, 1));
        var b = DrySlots(s).Single();
        Assert.That((b.Status, b.DoneMinutes), Is.EqualTo((DryingSlot.Drying, 0)), "进度从 0 开始（§Q1029 不继承已失效进度）");
        Assert.That(b.BatchId, Is.Not.EqualTo(old), "新一批");
    }

    // ───────────── Q1035：同位同物品逐份、数量窗上限与绑定 ─────────────

    /// <summary>
    /// Q1035-01：同一位两份野果来自两堆（不同截止，位内两个实例）——数量窗上限＝本位这种物品的全部份数 2（不只这张卡的 1 份）；
    /// 从第一张卡取 2 份到地面：先取这张、再取本位另一份，这一位空出；别的位、别的原料不被移动。
    /// </summary>
    [Test]
    public void Q1035_01_QuantityAcrossSameItemInSlot_NotOtherSlots()
    {
        var s = Rack();
        Ok(Commit(s, Drop(s, Make(s, "berry", 1, ItemInstance.Carried, s.GameMinute + 600), "dryslot:0"), 1));
        Ok(Commit(s, Drop(s, Make(s, "berry", 1, s.Ground, s.GameMinute + 900), "dryslot:0"), 1));
        Ok(Commit(s, Drop(s, Make(s, "raw_meat", 1, ItemInstance.Carried), "dryslot:1"), 1));
        var inSlot = E.DryingContents(s, s.Ground, 0);
        Assert.That(inSlot.Select(i => i.Count), Is.EqualTo(new[] { 1, 1 }), "起点：位内两个实例各 1 份");
        var first = inSlot[0];

        Assert.That((E.FacilityItemMax(s, first, s.Ground), E.FacilityItemMax(s, first, ItemInstance.Carried)), Is.EqualTo((2, 2)), "本位该物品合法份数");
        Ok(E.DoInstant(s, InstantRequest.FacilityTake(first.InstanceId, 2, s.Ground, E.FacilityItemStamp(s, first))));

        Assert.That(Sum(s, "berry", s.Ground), Is.EqualTo(2));
        Assert.That(DrySlots(s).Select(b => b.Slot), Is.EqualTo(new[] { 1 }), "位 0 取空空出；位 1 不动");
        Assert.That(Sum(s, "raw_meat", Slot(s, 1)), Is.EqualTo(1));
    }

    /// <summary>Q1035「明确选中实物绑定」：只取 1 份时取的是所点的那张卡（第二个实例），第一个实例仍在位里。</summary>
    [Test]
    public void Q1035_01_TakeOne_BindsSelectedCard()
    {
        var s = Rack();
        Ok(Commit(s, Drop(s, Make(s, "berry", 1, ItemInstance.Carried, s.GameMinute + 600), "dryslot:0"), 1));
        Ok(Commit(s, Drop(s, Make(s, "berry", 1, s.Ground, s.GameMinute + 900), "dryslot:0"), 1));
        var inSlot = E.DryingContents(s, s.Ground, 0);
        var second = inSlot[1];
        Ok(E.DoInstant(s, InstantRequest.FacilityTake(second.InstanceId, 1, s.Ground, E.FacilityItemStamp(s, second))));
        Assert.That(E.DryingContents(s, s.Ground, 0).Select(i => i.InstanceId), Is.EqualTo(new[] { inSlot[0].InstanceId }));
    }

    /// <summary>
    /// Q1035 本位同物品取到随身的容量拒绝（句式同同组拾取 GroupPickupBlockedText，B10 拾取原句）：位里野果两个实例各 1 份（每份 200 克，配置），
    /// 随身已 29800 克、3 张卡（硬上限 30 公斤，配置 carry.hardGrams；取 2 份多 2 张卡仍不超 5 张）——取 2 份拒「携带后将超过30公斤，最多可取1份」、上限 1；
    /// 配对：随身五张卡占满且野果会多出一张新卡时先报卡位句「随身卡位已满（5/5张）：…」。
    /// </summary>
    [Test]
    public void Q1035_01_CarriedLimit_WeightThenCards()
    {
        var s = Rack();
        Ok(Commit(s, Drop(s, Make(s, "berry", 1, ItemInstance.Carried, s.GameMinute + 600), "dryslot:0"), 1));
        Ok(Commit(s, Drop(s, Make(s, "berry", 1, s.Ground, s.GameMinute + 900), "dryslot:0"), 1));
        var first = E.DryingContents(s, s.Ground, 0)[0];
        s.Items.RemoveAll(i => i.Owner == ItemInstance.Carried);
        E.CreateItems(s, "plank", 38, ItemInstance.Carried);   // 起点：38×600 克（两张卡）
        E.CreateItems(s, "steel", 14, ItemInstance.Carried);   // 起点：14×500 克（一张卡），共 29800 克、3 张卡
        Assert.That(E.CarriedGrams(s), Is.EqualTo(29800), "起点");

        Assert.That(E.FacilityItemMax(s, first, ItemInstance.Carried), Is.EqualTo(1));
        Assert.That(E.FacilityItemTakeBlocked(s, first, 2, ItemInstance.Carried), Is.EqualTo("携带后将超过30公斤，最多可取1份"));
        Assert.That(E.FacilityItemTakeBlocked(s, first, 1, ItemInstance.Carried), Is.Null, "对照：1 份可取");

        s.Items.RemoveAll(i => i.Owner == ItemInstance.Carried);
        foreach (var id in new[] { "wood", "stone", "rope", "plank", "cloth" }) E.CreateItems(s, id, 1, ItemInstance.Carried);
        Assert.That(E.FacilityItemTakeBlocked(s, first, 2, ItemInstance.Carried), Is.EqualTo("随身卡位已满（5/5张）：会多出一张新卡，请先放下或用掉一张"));
    }

    // ───────────── Q1028：火源和锅的满位、空锅条件与拒收静默 ─────────────

    private const string Fire = "facility:camp:fire";

    private ItemInstance Pot(GameState s) => s.Items.First(i => i.ItemId == "pot");

    /// <summary>
    /// B51C2A2-Q1028-01：火堆两个加工位都占着（配置 2 位），撤「火堆加工位已满（…），请先取出」——按钮入口的拒绝原因为空、拖到火堆静默；
    /// 对照：空出一位后同一份生块根可拖入；行动进行中拖入仍给原忙碌句（不属本题）。
    /// </summary>
    [Test]
    public void Q1028_01_FireFull_NoReason_DropSilent()
    {
        var s = New();
        E.DebugGive(s, "raw_root", 3, "camp");
        Ok(E.DoInstant(s, InstantRequest.PutInFire(First(s, "raw_root", "camp").InstanceId, 2)));
        var root = First(s, "raw_root", "camp");

        Assert.That(E.MaxPutInFire(s, root, out var why), Is.EqualTo(0));
        Assert.That(why, Is.EqualTo(""), "§Q1028 撤满位原因句");
        Assert.That(E.DoInstant(s, InstantRequest.PutInFire(root.InstanceId)).Reason, Is.EqualTo(""));
        AssertSilent(Drop(s, root, "fire:camp"), "满位");
        AssertSilent(Drop(s, root, "fslot:0:camp"), "明确位已占");

        var inFire = s.Items.First(i => i.Owner == Fire);
        Ok(E.DoInstant(s, InstantRequest.TakeOut(inFire.InstanceId, "camp")));
        Assert.That(Drop(s, root, "fire:camp").Ok, Is.True, "对照：空出一位即可投");

        Start(s, ActionRequest.Wait(30));
        var busy = Drop(s, root, "fire:camp");
        Assert.That((busy.Ok, busy.Silent, busy.Reason), Is.EqualTo((false, false, "行动进行中，不能整理物品")), "忙碌沿原句");
    }

    /// <summary>§Q1028「不接受该物的槽……不高亮、不转移、不追加提示」：净水拖到火堆加工位、锅拖进锅的食材区静默（撤原教学句）。</summary>
    [Test]
    public void Q1028_NotAccepted_Silent()
    {
        var s = New();
        E.DebugGive(s, "clean_water", 1, "camp");
        var water = First(s, "clean_water", "camp");
        AssertSilent(Drop(s, water, "fslot:0:camp"), "液体拖到火堆加工位");
        AssertSilent(Drop(s, water, $"potfood:{Pot(s).InstanceId}"), "液体拖到锅的食材区");
    }

    /// <summary>
    /// §Q1028：锅的食材槽已满（配置锅食材位 2）撤「锅内…食材{n}/{n}（…），请先取出食物」——原因为空、拖入静默；锅页一槽一张食材卡：
    /// 同名同批两份是两个各 1 份的实例（B51C2A2-Q1028-02）。对照：锅里只有 1 份时可再拖入。
    /// </summary>
    [Test]
    public void Q1028_02_PotFoodFull_Silent_OneCardPerSlot()
    {
        var s = New();
        var pot = Pot(s);
        E.DebugGive(s, "raw_root", 3, "camp");
        Ok(E.DoInstant(s, InstantRequest.PutFoodInPot(First(s, "raw_root", "camp").InstanceId, pot.InstanceId)));
        var root = First(s, "raw_root", "camp");
        Assert.That(Drop(s, root, $"potfood:{pot.InstanceId}").Ok, Is.True, "对照：还有一个空食材位");
        Ok(E.DoInstant(s, InstantRequest.PutFoodInPot(root.InstanceId, pot.InstanceId)));

        var foods = E.PotFood(s, pot.InstanceId);
        Assert.That(foods.Select(f => (f.ItemId, f.Count)), Is.EqualTo(new[] { ("raw_root", 1), ("raw_root", 1) }), "同名同批各占一槽");
        root = First(s, "raw_root", "camp");
        Assert.That(E.MaxPutFood(s, root, pot, out var why), Is.EqualTo(0));
        Assert.That(why, Is.EqualTo(""));
        AssertSilent(Drop(s, root, $"potfood:{pot.InstanceId}"), "锅食材槽已满");
    }

    /// <summary>
    /// §Q1028：锅食谱因锅里已有食材不可投料——撤「锅里已有食材{n}/{n}（…），请先取出」，按钮照真实合法性置灰（Ok＝false），原因为空；
    /// 对照：同一口锅清空食材后，原因换成原其他真实条件（没水）的句子。
    /// </summary>
    [Test]
    public void Q1028_PotRecipe_HasFood_NoReason()
    {
        var s = New();
        E.DebugLearnAllKnowledge(s);   // 起点：锅食谱做法已知（同 B16PotRecipeTests）
        s.Skills["cooking"].Level = 5;  // 起点：烹饪等级够（远行硬饼 3 级、谷肉炖食 5 级）
        var pot = Pot(s);
        Ok(E.DoInstant(s, InstantRequest.PutInFire(pot.InstanceId)));
        E.DebugGive(s, "raw_root", 1, "camp");
        Ok(E.DoInstant(s, InstantRequest.PutFoodInPot(First(s, "raw_root", "camp").InstanceId, pot.InstanceId)));
        var opts = E.PotRecipeOptions(s, pot);
        Assert.That(opts, Is.Not.Empty);
        Assert.That(opts.Select(o => (o.Ok, o.Reason)), Is.All.EqualTo((false, (string?)"")));

        Ok(E.DoInstant(s, InstantRequest.TakeOut(E.PotFood(s, pot.InstanceId).Single().InstanceId, "camp")));
        Assert.That(E.PotRecipeOptions(s, pot).Select(o => o.Reason), Is.All.StartsWith("锅里没有水"), "对照：其他真实原因照旧");
    }

    /// <summary>
    /// B51C2A2-Q1029-01／Q1028-01 网页壳接线（bUnit 跑不到 drag.js，只能源码断言，测试规则第 9 条）：静默拒绝 quiet 的落点在悬停时不加高亮类、不写禁止原因
    /// （走「无落点」分支），松手时不弹原因闪字；配对：普通拒绝 no 仍弹原因（flash 原分支保留）。
    /// </summary>
    [Test]
    public void Q1029_DragJs_QuietTargets_NoHighlightNoFlash()
    {
        var js = SourceText.Web("wwwroot/js/drag.js");
        Assert.That(js, Does.Contain("if (!h || !h.r || (!h.r.ok && h.r.kind === 'quiet')) {"), "悬停：quiet 同无落点，不高亮不写原因");
        Assert.That(js, Does.Contain("if (!h.r.ok) { if (h.r.kind !== 'quiet') flash(h.r.reason || '暂不支持此组合', ev.clientX, ev.clientY); cleanup(); return; }"),
            "松手：quiet 不闪原因，也不回落到所在区；no 照旧闪原因");
    }
}
