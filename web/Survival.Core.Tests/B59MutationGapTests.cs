using Newtonsoft.Json.Linq;
using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// B59a＋B59b 变异测试补测（基线 465a5390..e5919f66，存活／无覆盖逐条处理表见开发记录「B59a 晾制架拖入即投料与槽位静默」「B59b 晾制保鲜冻结与到期即转腐败」的「变异测试」一节）。
/// 期望字面量均注明出处（S11／S06／S10 策划原句、配置 data/config.v0.6.json）；起点只用 CreateItems／直接造位记录，结论都走真实拖放解析、即时提交、取出与世界段推进入口。
/// </summary>
public class B59MutationGapTests : TestBase
{
    protected override bool NoEnemyApproach => true;

    private const string Here = "camp";

    private void Ok(ActionResult r) => Assert.That(r.Ok, Is.True, r.Reason);

    private static List<DryingSlot> DrySlots(GameState s) => s.Locations[s.Ground].Facility.DryingSlots;

    private static string Slot(GameState s, int k) => ItemInstance.DryingOwner(s.Ground, k);

    private static int Sum(GameState s, string itemId, string owner) => s.Items.Where(i => i.ItemId == itemId && i.Owner == owner).Sum(i => i.Count);

    /// <summary>临时营地建好晾制架（同 B59aTests 起点），清掉地面与随身物品，烹饪 5 级（肉干门槛，配置 dry_meat minSkills cooking 5）。</summary>
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

    private ActionResult Commit(GameState s, DropIntent intent, int count)
    {
        var r = intent.Instant!;
        r.Count = count;
        r.RequestId = Guid.NewGuid().ToString("N");
        return E.DoInstant(s, r);
    }

    private ActionResult Submit(GameState s, InstantRequest r)
    {
        r.RequestId = Guid.NewGuid().ToString("N");
        return E.DoInstant(s, r);
    }

    private static void AssertSilent(DropIntent d, string what) =>
        Assert.That((d.Ok, d.Silent, d.Reason), Is.EqualTo((false, true, "")), what + "：静默（不高亮、不转移、不提示）");

    private static void Lit(GameState s, string place = Here, int fuel = 240)
    {
        var f = s.Locations[place].Facility;
        f.FireLit = true;
        f.FuelMinutes = fuel;
    }

    private ItemInstance Pot(GameState s) => s.Items.First(i => i.ItemId == "pot");

    // ───────────── B59a §Q1029：晾制位落点一律静默（Drop.cs ResolveDrop 外包） ─────────────

    /// <summary>
    /// §Q1029「无操作资格的目标……松手不投放、不扣物、不弹提示」：通用前置门槛（这里是「其他地点的物品不能操作」）在晾制位落点上同样静默；
    /// 对照：同一张卡拖到随身仍给原拒绝句（静默只限晾制位落点）。
    /// </summary>
    [Test]
    public void Q1029_GeneralGateOnDryslot_Silent_OtherTargetsKeepReason()
    {
        var s = Rack();
        var other = s.Locations.Keys.First(k => k != s.Ground && k != Here);
        var berry = Make(s, "berry", 2, other, s.GameMinute + 600);

        var zone = Drop(s, berry, "zone:carried");
        Assert.That((zone.Ok, zone.Silent, zone.Reason), Is.EqualTo((false, false, "其他地点的物品不能操作")), "对照：非晾制位落点沿原句");
        AssertSilent(Drop(s, berry, "dryslot:0"), "晾制位落点：通用门槛也静默");
        Assert.That((berry.Owner, DrySlots(s).Count), Is.EqualTo((other, 0)), "不转移");
    }

    // ───────────── B59a §Q1033 A：同组可堆叠卡按组配批（Q606） ─────────────

    /// <summary>
    /// Q1033-01「默认份数＝min(缺口, 这堆真正可投的份数)」＋S06 Q606「可堆叠卡按同组」：随身野果两堆各 1 份（不同截止），拖第二堆到空位——
    /// 默认与上限都是 2（配置 dry_berries 野果×2，按组合计 1＋1，不是这张卡的 1），请求按组；提交 2 份两堆都进位、齐料开晾。
    /// </summary>
    [Test]
    public void Q1033_TwoStacksSameGroup_DefaultCountsGroup_CommitTakesBoth()
    {
        var s = Rack();
        var a = Make(s, "berry", 1, ItemInstance.Carried, s.GameMinute + 600);
        var b = Make(s, "berry", 1, ItemInstance.Carried, s.GameMinute + 900);

        var d = Drop(s, b, "dryslot:0");
        Assert.That((d.Ok, d.DefaultQuantity, d.MaxQuantity), Is.EqualTo((true, 2, 2)), "按组合计 1＋1，取缺口 2");
        Assert.That((d.Instant!.Group, d.Instant.GroupOwner, d.Instant.GroupItemId), Is.EqualTo((true, ItemInstance.Carried, "berry")), "请求挂同组（Q606）");

        Ok(Commit(s, d, 2));
        Assert.That(E.DryingContents(s, s.Ground, 0).Select(i => i.InstanceId).OrderBy(x => x), Is.EqualTo(new[] { a.InstanceId, b.InstanceId }.OrderBy(x => x)), "两堆都进位");
        Assert.That((DrySlots(s).Single().Status, Sum(s, "berry", ItemInstance.Carried)), Is.EqualTo((DryingSlot.Drying, 0)), "齐料开晾，随身不剩");
    }

    /// <summary>
    /// S06 Q606（自动配批「提交按同组实时实物重新配批」）：拖的是第二堆，松手前这一张已被用掉、同组另一堆仍在——提交按同组实时实物投 1 份，取到的是剩下那堆；
    /// 对照（不按组的请求）：只认那张已不在的实例，整次静默拒绝。
    /// </summary>
    [Test]
    public void Q606_DraggedStackGone_CommitUsesLiveGroup()
    {
        var s = Rack();
        var a = Make(s, "berry", 1, ItemInstance.Carried, s.GameMinute + 600);
        var b = Make(s, "berry", 1, ItemInstance.Carried, s.GameMinute + 900);
        var d = Drop(s, b, "dryslot:0");
        Assert.That(d.Ok, Is.True);
        s.Items.Remove(b);   // 起点：松手前所拖这一张已不在（同组另一堆仍在）

        var single = InstantRequest.DryingDrop(b.InstanceId, 0, "dry_berries", 1);
        Assert.That((Submit(s, single).Ok, DrySlots(s).Count), Is.EqualTo((false, 0)), "对照：只认原实例的请求找不到物品");

        Ok(Commit(s, d, 1));
        Assert.That(E.DryingContents(s, s.Ground, 0).Select(i => i.InstanceId), Is.EqualTo(new[] { a.InstanceId }), "按组实时配批取到同组那一堆");
    }

    // ───────────── B59a §Q1029：无资格、无容量静默 ─────────────

    /// <summary>
    /// §Q1029「无操作资格的目标……静默」：烹饪 4 级时生肉拖到空位静默（配置 dry_meat 要求烹饪 5 级，做法已知）；对照：5 级同一份生肉可投。
    /// </summary>
    [Test]
    public void Q1029_SkillBelowRecipe_Silent_EnoughSkillOk()
    {
        var s = Rack();
        var meat = Make(s, "raw_meat", 1, ItemInstance.Carried);
        s.Skills["cooking"].Level = 4;
        AssertSilent(Drop(s, meat, "dryslot:0"), "烹饪等级不够");
        s.Skills["cooking"].Level = 5;
        Assert.That(Drop(s, meat, "dryslot:0").Ok, Is.True, "对照：等级够即可投");
    }

    /// <summary>
    /// S11 §Q924 B（「腐败物……不能当合法输入，补投不能吞掉它们」）＋§Q1029「无容量……静默」：余料位（肉干）里是腐败物时，盐拖进来静默、不扣盐；
    /// 对照：余料位里是合法生肉时，同一份盐可投。
    /// </summary>
    [Test]
    public void Q1029_ResidualWithSpoiledMatter_NotRefillable_Silent()
    {
        var s = Rack();
        DrySlots(s).Add(new DryingSlot { Slot = 0, BatchId = 9100, RecipeId = "dry_meat", Status = DryingSlot.Residual, StartMinute = s.GameMinute - 60, RequiredMinutes = 180, InitiatorActorId = GameState.PlayerId });
        Make(s, GameEngine.SpoiledMatterId, 1, Slot(s, 0));   // 起点：旧档余料在位里已转腐败物（Q770-03 同类）
        var salt = Make(s, "salt", 1, ItemInstance.Carried);

        AssertSilent(Drop(s, salt, "dryslot:0"), "余料含腐败物，不可补投");
        Assert.That(salt.Owner, Is.EqualTo(ItemInstance.Carried), "不扣盐");

        Ok(Commit(s, Drop(s, Make(s, "raw_meat", 1, ItemInstance.Carried), "dryslot:1"), 1));
        Assert.That(Drop(s, salt, "dryslot:1").Ok, Is.True, "对照：合法余料位可补盐");
    }

    // ───────────── B59a §Q1029：提交按松手时实时状态重验，不合法整次静默拒绝 ─────────────

    /// <summary>
    /// LoadDryingByDrop 重验（§Q1029「非法……松手不投放、不扣物、不弹提示」；配置晾制架 8 位、dry_berries 野果×2）：
    /// 没有位号、位号 8（越界）、实例已不在、别处地面的物品、配方与位不符、份数超过缺口（3＞2）、份数 0——全部失败、原因为空、什么都不动；
    /// 对照：位号 7、份数 2 的合法提交成功。
    /// </summary>
    [Test]
    public void Q1029_CommitRevalidates_AllIllegalQuiet_NothingMoves()
    {
        var s = Rack();
        var berry = Make(s, "berry", 3, ItemInstance.Carried, s.GameMinute + 600);
        var other = s.Locations.Keys.First(k => k != s.Ground && k != Here);
        var far = Make(s, "berry", 2, other, s.GameMinute + 600);

        void Rejected(InstantRequest r, string what)
        {
            var res = Submit(s, r);
            Assert.That((res.Ok, res.Reason), Is.EqualTo((false, "")), what + "：静默拒绝");
            Assert.That((DrySlots(s).Count, Sum(s, "berry", ItemInstance.Carried), Sum(s, "berry", other)), Is.EqualTo((0, 3, 2)), what + "：什么都不动");
        }

        var noSlot = InstantRequest.DryingDrop(berry.InstanceId, 0, "dry_berries", 1);
        noSlot.Slot = null;
        Rejected(noSlot, "没有位号");
        Rejected(InstantRequest.DryingDrop(berry.InstanceId, 8, "dry_berries", 1), "位号 8（八位为 0—7）");
        Rejected(InstantRequest.DryingDrop(987654, 0, "dry_berries", 1), "实例已不在");
        Rejected(InstantRequest.DryingDrop(far.InstanceId, 0, "dry_berries", 1), "别处地面的物品");
        Rejected(InstantRequest.DryingDrop(berry.InstanceId, 0, "dry_meat", 1), "配方与这一位不符");
        Rejected(InstantRequest.DryingDrop(berry.InstanceId, 0, "dry_berries", 3), "份数超过缺口");
        Rejected(InstantRequest.DryingDrop(berry.InstanceId, 0, "dry_berries", 0), "份数 0");

        Ok(Submit(s, InstantRequest.DryingDrop(berry.InstanceId, 7, "dry_berries", 2)));
        Assert.That((DrySlots(s).Single().Slot, Sum(s, "berry", Slot(s, 7)), Sum(s, "berry", ItemInstance.Carried)), Is.EqualTo((7, 2, 1)), "对照：合法提交进位 2 份");
    }

    /// <summary>
    /// 批 ID（同 StartDrying 开批：取全局行动号 NextActionId 并自增；§Q1029「匹配新一批」）：空位首投未齐建余料记录用当前行动号 n0、计数器到 n0＋1；
    /// 补齐开晾换新批 ID n0＋1、计数器到 n0＋2。
    /// </summary>
    [Test]
    public void Q1029_BatchIds_FromGlobalCounter_NewBatchOnStart()
    {
        var s = Rack();
        var berry = Make(s, "berry", 2, ItemInstance.Carried, s.GameMinute + 600);
        var n0 = s.NextActionId;
        Ok(Commit(s, Drop(s, berry, "dryslot:0"), 1));
        Assert.That((DrySlots(s).Single().Status, DrySlots(s).Single().BatchId, s.NextActionId), Is.EqualTo((DryingSlot.Residual, n0, n0 + 1)));
        Ok(Commit(s, Drop(s, berry, "dryslot:0"), 1));
        Assert.That((DrySlots(s).Single().Status, DrySlots(s).Single().BatchId, s.NextActionId), Is.EqualTo((DryingSlot.Drying, n0 + 1, n0 + 2)));
    }

    /// <summary>
    /// §Q1033「这次物品实际进入原位」：整堆投入时这张卡（同一实例）整体进位，不拆出新实例、不在随身留 0 份空卡。
    /// </summary>
    [Test]
    public void Q1033_WholeStack_SameInstanceMoves_NoEmptyLeft()
    {
        var s = Rack();
        var berry = Make(s, "berry", 2, ItemInstance.Carried, s.GameMinute + 600);
        Ok(Commit(s, Drop(s, berry, "dryslot:0"), 2));
        var inSlot = E.DryingContents(s, s.Ground, 0);
        Assert.That(inSlot.Select(i => (i.InstanceId, i.Count)), Is.EqualTo(new[] { (berry.InstanceId, 2) }));
        Assert.That(s.Items.Count(i => i.Count <= 0), Is.EqualTo(0), "不留空卡");
    }

    /// <summary>
    /// 源区合堆（配置野果单堆上限 20；S06 Q606 配批「按最早有效到期、同期限按实例号逐批取」后源区按同批合堆）：
    /// 随身同批野果 25 份是 20＋5 两堆，投 2 份从第一堆取后随身重排为 20＋3；地面同样（拖地面那堆进另一位）。
    /// </summary>
    [Test]
    public void Q606_SourceZonesRestackAfterDrop()
    {
        var s = Rack();
        E.CreateItems(s, "berry", 25, ItemInstance.Carried);   // 同一分钟生成：同一截止（配置保质 1440 分钟）
        E.CreateItems(s, "berry", 25, s.Ground);
        List<int> Stacks(string owner) => s.Items.Where(i => i.ItemId == "berry" && i.Owner == owner).OrderBy(i => i.InstanceId).Select(i => i.Count).ToList();
        Assert.That((Stacks(ItemInstance.Carried), Stacks(s.Ground)), Is.EqualTo((new List<int> { 20, 5 }, new List<int> { 20, 5 })), "起点");

        Ok(Commit(s, Drop(s, s.Items.First(i => i.ItemId == "berry" && i.Owner == ItemInstance.Carried), "dryslot:0"), 2));
        Assert.That(Stacks(ItemInstance.Carried), Is.EqualTo(new List<int> { 20, 3 }), "随身重排合堆");
        Ok(Commit(s, Drop(s, s.Items.First(i => i.ItemId == "berry" && i.Owner == s.Ground), "dryslot:1"), 2));
        Assert.That(Stacks(s.Ground), Is.EqualTo(new List<int> { 20, 3 }), "地面重排合堆");
    }

    /// <summary>
    /// §Q769「重新补足后开的是新批……保鲜量不刷新」＋Q666（R＝E−S）：旧档余料位里还没冻结的野果（截止＝现在＋300），补投野果齐料开晾时按开晾分钟冻结 R＝300、截止暂空；
    /// 这次投入的野果沿用投入时冻结的 R＝600。
    /// </summary>
    [Test]
    public void Q769_LegacyUnfrozenResidual_FrozenAtStart()
    {
        var s = Rack();
        DrySlots(s).Add(new DryingSlot { Slot = 0, BatchId = 9100, RecipeId = "dry_berries", Status = DryingSlot.Residual, StartMinute = s.GameMinute - 60, RequiredMinutes = 120, InitiatorActorId = GameState.PlayerId });
        var old = Make(s, "berry", 1, Slot(s, 0), s.GameMinute + 300);
        var add = Make(s, "berry", 1, ItemInstance.Carried, s.GameMinute + 600);
        Ok(Commit(s, Drop(s, add, "dryslot:0"), 1));
        Assert.That(DrySlots(s).Single().Status, Is.EqualTo(DryingSlot.Drying));
        Assert.That((old.ExpiresAt, old.ShelfPausedMinutes), Is.EqualTo(((int?)null, (int?)300)), "旧余料按开晾分钟冻结");
        Assert.That((add.ExpiresAt, add.ShelfPausedMinutes), Is.EqualTo(((int?)null, (int?)600)), "这次投入的沿用投入时 R");
    }

    // ───────────── B59a §Q1028：火源与锅的拒收静默 ─────────────

    /// <summary>
    /// §Q1028「不接受该物的槽……不高亮、不转移、不追加提示」：腐败物拖到火堆、锅拖到已被占用的明确加工位（原句「加工位1已被占用，请重新选择。」撤为静默）；
    /// 对照：锅拖到空的加工位 2 可放；行动进行中拖到火堆仍给原忙碌句。
    /// </summary>
    [Test]
    public void Q1028_Fire_SpoiledMatterAndTakenSlot_Silent()
    {
        var s = New();
        var pot = Pot(s);
        E.DebugGive(s, "raw_root", 1, Here);
        Ok(E.DoInstant(s, InstantRequest.PutInFireSlot(First(s, "raw_root", Here).InstanceId, 0, 1)));
        var rot = Make(s, GameEngine.SpoiledMatterId, 1, Here);

        AssertSilent(Drop(s, rot, "fire:camp"), "腐败物拖到火堆");
        AssertSilent(Drop(s, pot, "fslot:0:camp"), "锅拖到已被占用的加工位 1");
        Assert.That(Drop(s, pot, "fslot:1:camp").Ok, Is.True, "对照：空的加工位 2 可放锅");

        Start(s, ActionRequest.Wait(30));
        var busy = Drop(s, rot, "fire:camp");
        Assert.That((busy.Ok, busy.Silent, busy.Reason), Is.EqualTo((false, false, "行动进行中，不能整理物品")), "对照：忙碌沿原句");
    }

    /// <summary>
    /// §Q1028「不接受该物的槽……静默」（锅的食材槽）：另一口锅、腐败物、木材拖到锅的食材区静默；按钮入口仍给各自原句
    /// （「不能锅套锅」「腐败物不能用于此操作。」「木材不能放进锅里」，净水「净水请拖到锅的水量条上装入」）。
    /// 对照：行动进行中拖生块根到食材区给原忙碌句（不静默）。
    /// </summary>
    [Test]
    public void Q1028_PotFood_NotAccepted_Silent_ButtonReasonsKept_BusyKeepsReason()
    {
        var s = New();
        var pot = Pot(s);
        s.Items.RemoveAll(i => i.ItemId == "wood" && i.Owner == Here);   // 起点：地面不留同名木材（新造的一份不被合堆并掉）
        var pot2 = Make(s, "pot", 1, Here);
        var rot = Make(s, GameEngine.SpoiledMatterId, 1, Here);
        var wood = Make(s, "wood", 1, Here);
        E.DebugGive(s, "clean_water", 1, Here);
        var water = First(s, "clean_water", Here);
        var target = $"potfood:{pot.InstanceId}";

        AssertSilent(Drop(s, pot2, target), "锅套锅");
        AssertSilent(Drop(s, rot, target), "腐败物");
        AssertSilent(Drop(s, wood, target), "木材");
        Assert.That(new[] { pot2, rot, wood, water }.Select(i => { E.MaxPutFood(s, i, pot, out var why); return why; }),
            Is.EqualTo(new[] { "不能锅套锅", GameEngine.SpoiledMatterRejectText, "木材不能放进锅里", "净水请拖到锅的水量条上装入" }), "按钮入口原句保留");

        E.DebugGive(s, "raw_root", 1, Here);
        var root = First(s, "raw_root", Here);
        Assert.That(Drop(s, root, target).Ok, Is.True, "对照：空闲时生块根可放");
        Start(s, ActionRequest.Wait(30));
        var busy = Drop(s, root, target);
        Assert.That((busy.Ok, busy.Silent, busy.Reason), Is.EqualTo((false, false, "行动进行中，不能整理物品")), "忙碌沿原句");
    }

    /// <summary>
    /// S06 Q606／S14 Q642（拖放与按钮同一配批，按组算上限）：随身生块根两堆各 1 份（不同截止），拖到空锅食材区——上限 2（锅食材 2 位，按组合计 1＋1），请求按组。
    /// </summary>
    [Test]
    public void Q642_PotFood_GroupCount_MaxTwo()
    {
        var s = New();
        var pot = Pot(s);
        s.Items.RemoveAll(i => i.ItemId == "raw_root");
        Make(s, "raw_root", 1, ItemInstance.Carried, s.GameMinute + 600);
        var b = Make(s, "raw_root", 1, ItemInstance.Carried, s.GameMinute + 900);
        var d = Drop(s, b, $"potfood:{pot.InstanceId}");
        Assert.That((d.Ok, d.MaxQuantity, d.Instant!.Group), Is.EqualTo((true, 2, true)));
    }

    // ───────────── B59a §Q1035：本位同物品取到随身的容量句 ─────────────

    /// <summary>
    /// Q1035 本位同物品取到随身（句式同同组拾取 GroupPickupBlockedText，B10 拾取原句）：位里野果两个实例（冻结余量不同，取出后是两张卡）。
    /// 随身 4 张卡时取 2 份——先报卡位「随身卡位已满（4/5张）：会多出一张新卡，请先放下或用掉一张；最多可取1份」；
    /// 随身 29900 克（硬上限 30 公斤，配置 carry.hardGrams；野果每份 200 克）时取 1 份——「携带后将超过30公斤，拿不动了」；对照：29800 克时 1 份可取。
    /// </summary>
    [Test]
    public void Q1035_CarriedLimit_CardsSomeAndWeightNone()
    {
        var s = Rack();
        Ok(Commit(s, Drop(s, Make(s, "berry", 1, ItemInstance.Carried, s.GameMinute + 600), "dryslot:0"), 1));
        Ok(Commit(s, Drop(s, Make(s, "berry", 1, s.Ground, s.GameMinute + 900), "dryslot:0"), 1));
        var first = E.DryingContents(s, s.Ground, 0)[0];

        s.Items.RemoveAll(i => i.Owner == ItemInstance.Carried);
        foreach (var id in new[] { "wood", "stone", "rope", "plank" }) E.CreateItems(s, id, 1, ItemInstance.Carried);
        Assert.That(E.RootCardCount(s), Is.EqualTo(4), "起点");
        Assert.That(E.FacilityItemTakeBlocked(s, first, 2, ItemInstance.Carried), Is.EqualTo("随身卡位已满（4/5张）：会多出一张新卡，请先放下或用掉一张；最多可取1份"));

        s.Items.RemoveAll(i => i.Owner == ItemInstance.Carried);
        E.CreateItems(s, "plank", 38, ItemInstance.Carried);   // 38×600 克（两张卡）
        E.CreateItems(s, "steel", 14, ItemInstance.Carried);   // 14×500 克（一张卡）
        var salt = E.CreateItems(s, "salt", 1, ItemInstance.Carried).Single();   // 100 克（一张卡）
        Assert.That((E.CarriedGrams(s), E.RootCardCount(s)), Is.EqualTo((29900, 4)), "起点");
        Assert.That(E.FacilityItemTakeBlocked(s, first, 1, ItemInstance.Carried), Is.EqualTo("携带后将超过30公斤，拿不动了"));
        s.Items.Remove(salt);
        Assert.That(E.FacilityItemTakeBlocked(s, first, 1, ItemInstance.Carried), Is.Null, "对照：29800 克时 1 份可取");
    }

    // ───────────── B59b §Q1036：烹饪冻结 ─────────────

    /// <summary>
    /// 旧档受热食材（B59b 遗留：没有冻结余量、沿原 Q16 办法）恰在原截止分钟（S06 Q610「截止不晚于当前分钟即已到期」）移入同火堆的锅——
    /// 拒「生肉取出后已腐败，不能移入锅。」；对照：原截止还差 5 分钟可移入。
    /// </summary>
    [Test]
    public void Q1036_LegacyHeated_AtExactDeadline_MoveToPotRefused()
    {
        foreach (var (offset, blocked) in new[] { (0, (string?)"生肉取出后已腐败，不能移入锅。"), (5, null) })
        {
            var s = New();
            var pot = Pot(s);
            s.Items.RemoveAll(i => i.ItemId is "clean_water" or "raw_meat" or "raw_root");
            E.DebugGive(s, "clean_water", 10, Here);
            Ok(E.DoInstant(s, InstantRequest.FillPot(First(s, "clean_water", Here).InstanceId, pot.InstanceId, 10)));
            Ok(E.DoInstant(s, InstantRequest.PutInFireSlot(pot.InstanceId, 1)));
            var meat = Make(s, "raw_meat", 1, Here, s.GameMinute + 300);
            Ok(E.DoInstant(s, InstantRequest.PutInFireSlot(meat.InstanceId, 0)));
            Lit(s);
            AdvanceTo(s, s.GameMinute + 10);
            Assert.That(E.ProcessOf(s, meat)!.HeatProtected, Is.True, "起点：受热保护");
            meat.ShelfPausedMinutes = null;   // 起点：造旧档受热食材（同 BM5b1 Q601_A04）
            meat.ExpiresAt = s.GameMinute + offset;
            Assert.That(E.MoveToPotBlocked(s, E.MoveToPotOptionFor(s, 0)!.Request), Is.EqualTo(blocked), $"原截止＝现在＋{offset}");
        }
    }

    /// <summary>
    /// §Q1036「实际开始烹饪……冻结」（各区火堆与屋内同一规则）：安全屋屋内火堆直烤生肉（截止＝开烤＋100），受热 10 分钟仍冻结 R＝100、原截止保留，
    /// 不被当成「已不受保护」恢复计时；对照：取出后从取出时刻＋100 继续计时。
    /// </summary>
    [Test]
    public void Q1036_SafehouseInteriorFire_FrozenWhileHeated()
    {
        var s = New();
        E.DebugSetProgress(s, Here, 100);
        s.Progress.Flags.Add("hub_registered");
        s.Skills["crafting"].Level = Math.Max(1, s.Skills["crafting"].Level);
        s.WorldEnemies.RemoveAll(w => w.LocationId == Here);
        foreach (var (id, n) in new[] { ("wood", 8), ("fiber", 6), ("cloth", 2), ("stone", 4) }) E.CreateItems(s, id, n, s.Ground);
        Start(s, ActionRequest.Craft("safehouse_base", null, null, 1));
        Run(s);
        Ok(E.EnterSafehouse(s));
        foreach (var (id, n) in new[] { ("stone", 4), ("wood", 2) }) E.CreateItems(s, id, n, s.Ground);
        Start(s, ActionRequest.Craft("safehouse_fire", null, null, 1));
        Run(s);
        Assert.That(E.HasFireAt(s, s.Ground), Is.True, "起点：屋内火堆");

        s.Items.RemoveAll(i => i.ItemId == "raw_meat");
        var t0 = s.GameMinute;
        var meat = Make(s, "raw_meat", 1, s.Ground, t0 + 100);
        Ok(E.DoInstant(s, InstantRequest.PutInFireSlot(meat.InstanceId, 0, 1)));
        var fac = s.Locations[Here].Safehouse!.Interior;
        fac.FireLit = true;
        fac.FuelMinutes = 240;
        AdvanceTo(s, t0 + 10);
        Assert.That(E.ProcessOf(s, meat)!.HeatProtected, Is.True);
        Assert.That((meat.ExpiresAt, meat.ShelfPausedMinutes), Is.EqualTo(((int?)(t0 + 100), (int?)100)), "屋内受热同样冻结");

        var c = s.GameMinute;
        Ok(E.DoInstant(s, InstantRequest.TakeOut(meat.InstanceId, s.Ground)));
        Assert.That((meat.ItemId, meat.ExpiresAt, meat.ShelfPausedMinutes), Is.EqualTo(("raw_meat", (int?)(c + 100), (int?)null)), "对照：取出后 C＋R");
    }

    /// <summary>
    /// §Q1036 释放范围（「只收未预留、不在晾制位……的冻结物；生产输入由预留绑定、晾制位按原规则」，S06 Q701.1 生产输入暂停保质）：
    /// 火堆直烤生肉冻结期间，同时在做草药绷带（草药预留并冻结 R＝120），世界段结束草药仍冻结、原截止不动；
    /// 对照：取出生肉即恢复 C＋R。
    /// </summary>
    [Test]
    public void Q1036_CookFrozenCoexists_ReservedCraftInputStaysPaused()
    {
        var s = New();
        s.Items.RemoveAll(i => i.ItemId is "cloth" or "herb" or "bandage" or "raw_meat");
        E.DebugGive(s, "cloth", 2);
        var t0 = s.GameMinute;
        var herbStack = Make(s, "herb", 1, ItemInstance.Carried, t0 + 120);
        var meat = Make(s, "raw_meat", 1, Here, t0 + 100);
        Ok(E.DoInstant(s, InstantRequest.PutInFireSlot(meat.InstanceId, 0, 1)));
        Lit(s);
        Start(s, ActionRequest.Craft("bandage"));
        var herb = s.Items.Single(i => i.ItemId == "herb" && i.ReservedBy == s.Main!.ActionId);
        Assert.That(herb.ShelfPausedMinutes, Is.EqualTo(120), "起点：生产输入冻结（Q701.1）");

        Steps(s, 1);
        Assert.That((E.ProcessOf(s, meat)!.HeatProtected, meat.ShelfPausedMinutes), Is.EqualTo((true, (int?)100)), "生肉受热冻结");
        Assert.That(s.Main, Is.Not.Null, "绷带仍在做");
        Assert.That((herb.ReservedBy, herb.ShelfPausedMinutes, herb.ExpiresAt), Is.EqualTo(((int?)s.Main!.ActionId, (int?)120, (int?)(t0 + 120))), "预留的生产输入不被烹饪释放");
        Assert.That(herbStack.InstanceId, Is.EqualTo(herb.InstanceId));
    }

    /// <summary>
    /// §Q769（晾制位留位原料冻结）与 §Q1036（烹饪冻结）同时存在：余料位野果冻结 R＝600，火堆生肉受热冻结 R＝100；世界段结束野果仍冻结（截止暂空）；
    /// 对照：取出生肉，生肉从此刻＋100 恢复，野果仍冻结。
    /// </summary>
    [Test]
    public void Q769_DryingFrozenCoexistsWithCookFreeze_NotReleased()
    {
        var s = Rack();
        Ok(Commit(s, Drop(s, Make(s, "berry", 1, ItemInstance.Carried, s.GameMinute + 600), "dryslot:0"), 1));
        var berry = E.DryingContents(s, s.Ground, 0).Single();
        s.Items.RemoveAll(i => i.ItemId == "raw_meat");
        var meat = Make(s, "raw_meat", 1, s.Ground, s.GameMinute + 100);
        Ok(E.DoInstant(s, InstantRequest.PutInFireSlot(meat.InstanceId, 0, 1)));
        Lit(s, s.Ground);
        AdvanceTo(s, s.GameMinute + 10);
        Assert.That((E.ProcessOf(s, meat)!.HeatProtected, meat.ShelfPausedMinutes), Is.EqualTo((true, (int?)100)), "起点：生肉受热冻结");
        Assert.That((berry.ExpiresAt, berry.ShelfPausedMinutes), Is.EqualTo(((int?)null, (int?)600)), "余料位野果仍冻结");

        var c = s.GameMinute;
        Ok(E.DoInstant(s, InstantRequest.TakeOut(meat.InstanceId, s.Ground)));
        Assert.That((meat.ExpiresAt, meat.ShelfPausedMinutes), Is.EqualTo(((int?)(c + 100), (int?)null)), "对照：取出的生肉恢复 C＋R");
        Assert.That((berry.ExpiresAt, berry.ShelfPausedMinutes), Is.EqualTo(((int?)null, (int?)600)), "野果不受影响");
    }

    // ───────────── 存档 58 → 59 ─────────────

    /// <summary>
    /// 58→59（B59b，§Q1036；开发记录 B59b.5）：只冻结受热保护进程真正指向的那份实物——直烤生肉（58 旧档无 R，原截止＝升级时 U＋100）记 R＝100、原截止保留；
    /// 物品表里排在它前面的地面野果（截止 U＋300，不在任何加工里）不冻结。
    /// </summary>
    [Test]
    public void Migrate58To59_FreezesExactProcessTarget_NotOtherItems()
    {
        var s = New();
        s.Items.RemoveAll(i => i.ItemId is "raw_meat" or "berry");
        var meat = Make(s, "raw_meat", 1, Here, s.GameMinute + 500);
        Ok(E.DoInstant(s, InstantRequest.PutInFireSlot(meat.InstanceId, 0, 1)));
        Lit(s);
        AdvanceTo(s, s.GameMinute + 10);
        Assert.That(E.ProcessOf(s, meat)!.HeatProtected, Is.True, "起点：受热保护");
        var u = s.GameMinute;
        meat.ShelfPausedMinutes = null;   // 起点：58 旧档受热保护不记 R
        meat.ExpiresAt = u + 100;
        var berry = Make(s, "berry", 1, Here, u + 300);
        s.Items.Remove(berry);
        s.Items.Insert(0, berry);   // 起点：野果排在物品表最前，生肉排最后
        s.Items.Remove(meat);
        s.Items.Add(meat);

        var o = JObject.Parse(SaveSerializer.Serialize(s));
        o["SchemaVersion"] = "58";
        var m = SaveSerializer.Deserialize(o.ToString(), Cfg);
        (int? E, int? R) Of(ItemInstance i) { var x = m.Items.Single(y => y.InstanceId == i.InstanceId); return (x.ExpiresAt, x.ShelfPausedMinutes); }
        Assert.That(Of(meat), Is.EqualTo(((int?)(u + 100), (int?)100)), "受热生肉 R＝旧E−U，原截止保留");
        Assert.That(Of(berry), Is.EqualTo(((int?)(u + 300), (int?)null)), "地面野果照常计时");
    }
}
