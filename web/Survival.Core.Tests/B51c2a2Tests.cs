using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 批次 B51c2a2「被动设施取出 2：晾架＋火堆全部取出（不升存档）」：
/// S11「2026-11-17 B51c2、B47b与B52b三个完整原批」Q870 B（晾架每在制位「取出」、原料退本区地面）、Q871 B（四类统一取出门槛、放入仍原门槛），
/// 「B51c次要原批Q873—Q881」Q876 B（结果句）、Q877（火堆整锅连内容＋直接烹饪食材全部取出）、Q878（不报中止批数）、Q879 A（全部取出无悬停、快照戳）、
/// Q880（映射表：已满／晾制中／待取出／原料已腐败。），Q1009 A（取出即腐败列「腐败物×{数量}」并接一次原转换句），Q966（晾架暂停说明）；
/// S14 FAC-Q870-01、FAC-Q871-01／02、B51-Q778-01—06、B51C-Q879-01、B51C-Q880-01、B51C2A1-Q1009-01（火堆部分）。
/// 期望字面量均注明出处；起点只用 Debug／直接改状态构造，结论都走真实取出入口。
/// </summary>
public class B51c2a2Tests : TestBase
{
    private const string Busy = "行动进行中，暂不能整理物品";   // S11 Q871 B：四类取出统一沿原句
    private const string Changed = "物品已变，请重选。";        // S11 Q879 A：计划快照变化
    private const string Empty = "没有可取出的物品。";          // S11 Q879 A：空设施／空范围

    protected override bool NoEnemyApproach => true;

    private void Ok(ActionResult r) => Assert.That(r.Ok, Is.True, r.Reason);

    private List<ItemInstance> Make(GameState s, string itemId, int count, string owner, int? expires = null)
    {
        var made = E.CreateItems(s, itemId, count, owner);
        if (expires.HasValue) foreach (var i in made) i.ExpiresAt = expires;
        return made;
    }

    /// <summary>临时营地建好晾制架（同 B50a／B53b 起点），清掉地面与随身物品。</summary>
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

    private static List<DryingSlot> DrySlots(GameState s) => s.Locations[s.Ground].Facility.DryingSlots;

    private long Xp(GameState s) => s.Skills["cooking"].Xp;

    private static int GroundSum(GameState s, string itemId) => s.Items.Where(i => i.ItemId == itemId && i.Owner == s.Ground).Sum(i => i.Count);

    private ActionResult TakeAllNow(GameState s, string facility, string? req = null) =>
        E.TakeAll(s, s.Ground, facility, E.TakeAllStamp(s, s.Ground, facility), req);

    // ───────────── 晾制架：在制位「取出」（Q870 B） ─────────────

    /// <summary>
    /// FAC-Q870-01：晾制中的这一位「取出」——原料回本区地面（不进随身），恢复 C＋R（原剩 30 → 新截止＝取出时刻＋30），该位结束，
    /// 无成品、不给经验、0 分钟；同座另一位照常晾。结果「已取出至{地点}地面：{物品清单}。」（Q876 B），无中止／成本尾句（Q878）。
    /// </summary>
    [Test]
    public void Q870_SlotTake_ToGround_RestoresCR_NoXp_OtherSlotUntouched()
    {
        var s = Rack();
        var t0 = s.GameMinute;
        Make(s, "berry", 2, ItemInstance.Carried, t0 + 30);
        Ok(E.StartDrying(s, "dry_berries"));
        Make(s, "raw_meat", 1, ItemInstance.Carried);
        Make(s, "salt", 1, ItemInstance.Carried);
        Ok(E.StartDrying(s, "dry_meat"));
        Assert.That(DrySlots(s).Select(b => (b.Slot, b.RecipeId, b.Status)),
            Is.EqualTo(new[] { (0, "dry_berries", DryingSlot.Drying), (1, "dry_meat", DryingSlot.Drying) }));
        AdvanceTo(s, t0 + 60);
        var x0 = Xp(s);
        var c = s.GameMinute;

        Ok(TakeDryingNow(s, 0));

        Assert.That(s.GameMinute, Is.EqualTo(c), "0 分钟");
        var berries = s.Items.Where(i => i.ItemId == "berry").ToList();
        Assert.That(berries, Has.Count.EqualTo(1));
        Assert.That((berries[0].Owner, berries[0].Count, berries[0].ExpiresAt, berries[0].ShelfPausedMinutes),
            Is.EqualTo((s.Ground, 2, (int?)(c + 30), (int?)null)), "回本区地面，新截止＝C＋R（R＝30）");
        Assert.That(Xp(s), Is.EqualTo(x0), "不给经验");
        Assert.That(s.Items.Count(i => i.ItemId == "dried_berries"), Is.EqualTo(0), "没有成品");
        Assert.That(DrySlots(s).Select(b => (b.Slot, b.Status)), Is.EqualTo(new[] { (1, DryingSlot.Drying) }), "该位结束，另一位不动");
        Assert.That(s.Items.Where(i => i.ItemId is "raw_meat" or "salt").All(i => i.InDrying), Is.True, "另一位原料仍在晾制位");
        Assert.That(s.Log.Last().Text, Is.EqualTo("已取出至临时营地地面：野果×2。"), "Q876 B 结果句；临时营地＝本局起始区名");
    }

    /// <summary>B51C-Q879-01：计划快照变化整次拒「物品已变，请重选。」、不动物品；同一请求号只生效一次（配对：首次确实取出）。</summary>
    [Test]
    public void Q879_SlotTake_StaleStampRejected_RequestIdOnce()
    {
        var s = Rack();
        Make(s, "berry", 2, ItemInstance.Carried);
        Ok(E.StartDrying(s, "dry_berries"));
        var stale = E.DryingTakeStamp(s, s.Ground, 0);
        Make(s, "berry", 1, ItemInstance.DryingOwner(s.Ground, 0));   // 起点：位内物品在渲染后变了
        var r = E.TakeDryingSlot(s, 0, stale, "rq-a");
        Assert.That((r.Ok, r.Reason), Is.EqualTo((false, Changed)));
        Assert.That(DrySlots(s).Single().Status, Is.EqualTo(DryingSlot.Drying), "拒绝不动该位");
        Assert.That(s.Items.Where(i => i.ItemId == "berry").All(i => i.InDrying), Is.True);

        Ok(TakeDryingNow(s, 0, "rq-b"));
        Assert.That(DrySlots(s), Is.Empty, "首次确实取出");
        Assert.That(GroundSum(s, "berry"), Is.EqualTo(3));
        Ok(E.StartDrying(s, "dry_berries"));
        var again = TakeDryingNow(s, 0, "rq-b");
        Assert.That((again.Ok, again.Duplicate), Is.EqualTo((true, true)), "同一请求号不再生效");
        Assert.That(DrySlots(s).Single().Status, Is.EqualTo(DryingSlot.Drying), "重放不取第二次");
    }

    /// <summary>
    /// FAC-Q871-01／02：晾制架取出门槛——主行动进行／暂停、只有子行动都拒「行动进行中，暂不能整理物品」，战斗沿晾制架原句「战斗中不能操作晾制架」；
    /// 都没有时可取。放入仍原门槛（StartDrying 忙碌时沿「行动进行中，先完成或取消再操作晾制架」）。
    /// </summary>
    [Test]
    public void Q871_DryingTakeGate_BusyPausedSubBattle_PutInKeepsOldGate()
    {
        var s = Rack();
        Make(s, "berry", 2, ItemInstance.Carried);
        Ok(E.StartDrying(s, "dry_berries"));
        Make(s, "berry", 2, ItemInstance.Carried);
        Assert.That(E.DryingSlotTakeBlocked(s, 0), Is.Null, "空闲可取");
        Assert.That(E.TakeAllBlocked(s, s.Ground, GameEngine.DryingStationId), Is.Null);

        Start(s, ActionRequest.Wait(30));
        Assert.That(E.DryingSlotTakeBlocked(s, 0), Is.EqualTo(Busy));
        Assert.That(E.TakeAllBlocked(s, s.Ground, GameEngine.DryingStationId), Is.EqualTo(Busy));
        var r = TakeDryingNow(s, 0);
        Assert.That((r.Ok, r.Reason), Is.EqualTo((false, Busy)));
        Assert.That(DrySlots(s).Single().Status, Is.EqualTo(DryingSlot.Drying), "被拒不动");
        // 放入仍原门槛（Q871 B：放入不改）——晾制架原句
        Assert.That(E.StartDrying(s, "dry_berries").Reason, Is.EqualTo("行动进行中，先完成或取消再操作晾制架"));
        s.Main!.Status = ActionStatus.Paused;
        Assert.That(E.DryingSlotTakeBlocked(s, 0), Is.EqualTo(Busy), "暂停也算");
        E.Cancel(s);
        Assert.That(s.Main, Is.Null);
        s.Sub = new ActionState { IsSub = true };
        Assert.That(E.DryingSlotTakeBlocked(s, 0), Is.EqualTo(Busy), "只有子行动也拒");
        s.Sub = null;
        s.Battle = new BattleState();
        Assert.That(E.DryingSlotTakeBlocked(s, 0), Is.EqualTo("战斗中不能操作晾制架"), "沿该设施原战斗句");
        s.Battle = null;
        Assert.That(E.DryingSlotTakeBlocked(s, 0), Is.Null, "恢复空闲可取");
        Ok(TakeDryingNow(s, 0));
    }

    /// <summary>
    /// B51-Q778-02—06（晾制架）：「全部取出」把已晾好的成品与晾制中的原料一次放到本区地面——位全部空出、不重发经验、原料恢复 C＋R；
    /// 结果按位次列「{物名}×{数量}」顿号分隔（Q876 B）；之后再按为空「没有可取出的物品。」（Q879 A），提交也拒同句。
    /// </summary>
    [Test]
    public void Q778_DryingTakeAll_DoneAndInProgress_ThenEmpty()
    {
        var s = Rack();
        var t0 = s.GameMinute;
        Make(s, "berry", 2, ItemInstance.Carried);
        Ok(E.StartDrying(s, "dry_berries"));
        Make(s, "raw_meat", 1, ItemInstance.Carried, t0 + 600);
        Make(s, "salt", 1, ItemInstance.Carried);
        Ok(E.StartDrying(s, "dry_meat"));
        AdvanceTo(s, t0 + 120);   // 配置 dry_berries 120 分钟完成、dry_meat 180 分钟（B50a Q666-A02）
        Assert.That(DrySlots(s).Select(b => (b.Slot, b.Status)), Is.EqualTo(new[] { (0, DryingSlot.Done), (1, DryingSlot.Drying) }));
        var x0 = Xp(s);
        var c = s.GameMinute;
        Assert.That(E.TakeAllBlocked(s, s.Ground, GameEngine.DryingStationId), Is.Null);

        Ok(TakeAllNow(s, GameEngine.DryingStationId, "ta-1"));

        Assert.That(DrySlots(s), Is.Empty, "全部位空出");
        Assert.That((GroundSum(s, "dried_berries"), GroundSum(s, "raw_meat"), GroundSum(s, "salt")), Is.EqualTo((1, 1, 1)), "配置 dry_berries 出果干×1");
        Assert.That(s.Items.Any(i => i.InDrying), Is.False);
        Assert.That(Xp(s), Is.EqualTo(x0), "不重发经验、取出在制不给经验");
        var meat = s.Items.Single(i => i.ItemId == "raw_meat");
        Assert.That((meat.ExpiresAt, meat.ShelfPausedMinutes), Is.EqualTo(((int?)(c + 600), (int?)null)), "C＋R：投料时剩 600");
        Assert.That(s.Log.Last().Text, Is.EqualTo("已取出至临时营地地面：果干×1、生肉×1、盐×1。"));

        Assert.That(E.TakeAllBlocked(s, s.Ground, GameEngine.DryingStationId), Is.EqualTo(Empty));
        var again = TakeAllNow(s, GameEngine.DryingStationId);
        Assert.That((again.Ok, again.Reason), Is.EqualTo((false, Empty)));
    }

    /// <summary>B51C-Q879-01（晾制架全部取出）：快照戳过期整次拒、不动任何位（配对：用新戳确实取出）。</summary>
    [Test]
    public void Q879_DryingTakeAll_StaleStampRejected()
    {
        var s = Rack();
        Make(s, "berry", 2, ItemInstance.Carried);
        Ok(E.StartDrying(s, "dry_berries"));
        var stale = E.TakeAllStamp(s, s.Ground, GameEngine.DryingStationId);
        Make(s, "berry", 2, ItemInstance.Carried);
        Ok(E.StartDrying(s, "dry_berries"));   // 另一位新投料，整座计划变了
        var r = E.TakeAll(s, s.Ground, GameEngine.DryingStationId, stale);
        Assert.That((r.Ok, r.Reason), Is.EqualTo((false, Changed)));
        Assert.That(DrySlots(s), Has.Count.EqualTo(2), "两位都不动");
        Ok(TakeAllNow(s, GameEngine.DryingStationId));
        Assert.That(DrySlots(s), Is.Empty);
        Assert.That(s.Log.Last().Text, Is.EqualTo("已取出至临时营地地面：野果×4。"), "Q876 B 同名合量");
    }

    /// <summary>
    /// B51C-Q880-01（晾制架）：晾制中位不能走收取／余物路径「晾制中」；已晾好位不能走在制取出「已经晾好了：请取出成品」（原句）；
    /// 腐败停止位「原料已腐败。」，其他停止位「请取出剩余物品。」；面板视图晾制中位可取（TakeBlocked 为空）、带本位快照戳。
    /// </summary>
    [Test]
    public void Q880_DryingTakeTexts_ByStatus()
    {
        var s = Rack();
        var t0 = s.GameMinute;
        Make(s, "berry", 2, ItemInstance.Carried);
        Ok(E.StartDrying(s, "dry_berries"));
        Assert.That(E.DryingTakeBlocked(s, 0, false), Is.EqualTo("晾制中"), "Q880：可以取消（退回原料）→ 晾制中");
        var v = E.DryingSlotViews(s).Single(x => x.Slot == 0);
        Assert.That((v.TakeBlocked, v.TakeStamp), Is.EqualTo(((string?)null, E.DryingTakeStamp(s, s.Ground, 0))));
        AdvanceTo(s, t0 + 120);
        Assert.That(DrySlots(s).Single().Status, Is.EqualTo(DryingSlot.Done));
        Assert.That(E.DryingSlotTakeBlocked(s, 0), Is.EqualTo("已经晾好了：请取出成品"));

        DrySlots(s).Add(new DryingSlot
        {
            Slot = 1, BatchId = 5201, RecipeId = "dry_berries", Status = DryingSlot.Spoiled, StartMinute = s.GameMinute - 60, DoneMinutes = 55,
            RequiredMinutes = 120, EndMinute = s.GameMinute - 5, InitiatorActorId = GameState.PlayerId, StopReason = DryingSlot.StopSpoiled,
        });
        DrySlots(s).Add(new DryingSlot
        {
            Slot = 2, BatchId = 5202, RecipeId = "dry_berries", Status = DryingSlot.Spoiled, StartMinute = s.GameMinute - 60, DoneMinutes = 55,
            RequiredMinutes = 120, EndMinute = s.GameMinute - 5, InitiatorActorId = GameState.PlayerId, StopReason = DryingSlot.StopMissing,
        });
        Assert.That(E.DryingSlotTakeBlocked(s, 1), Is.EqualTo("原料已腐败。"), "Q880：原料已腐败：只能清理… → 原料已腐败。");
        Assert.That(E.DryingSlotTakeBlocked(s, 2), Is.EqualTo("请取出剩余物品。"), "Q880：请清理剩余物品 → 请取出剩余物品。");
    }

    // ───────────── 火堆：全部取出（Q877／Q1009） ─────────────

    private ItemInstance Pot(GameState s) => s.Items.First(i => i.ItemId == "pot");

    private void Do(GameState s, InstantRequest r) => Ok(E.DoInstant(s, r));

    private static void LightFire(GameState s, int fuel = 240)
    {
        var f = s.Locations["camp"].Facility;
        f.FireLit = true;
        f.FuelMinutes = fuel;
    }

    private void WaitMinutes(GameState s, int minutes)
    {
        var end = s.GameMinute + minutes;
        while (s.GameMinute < end)
        {
            if (s.Main == null) Start(s, ActionRequest.Wait(30));
            else if (s.Main.Status == ActionStatus.Paused) E.Resume(s);
            E.Advance(s);
        }
        if (s.Main?.Type == ActionType.Wait) E.Cancel(s);
    }

    /// <summary>营地火堆：加工位1 放满锅净水（10 份×100）＋1 份生块根的锅，加工位2 直接放 1 份生肉；点燃受热 10 分钟（同 UiRevisionV07 起点）。</summary>
    private (ItemInstance Pot, ItemInstance Meat) FireWithPotAndMeat(GameState s, int? meatExpires = null, int? rootExpires = null)
    {
        var pot = Pot(s);
        E.DebugGive(s, "clean_water", 10, "camp");
        Do(s, InstantRequest.FillPot(s.Items.Where(i => i.ItemId == "clean_water" && i.Owner == "camp").OrderByDescending(i => i.Count).First().InstanceId, pot.InstanceId, 10));
        Do(s, InstantRequest.PutInFire(pot.InstanceId));
        E.DebugGive(s, "raw_root", 1, "camp");
        Do(s, InstantRequest.PutFoodInPot(First(s, "raw_root", "camp").InstanceId, pot.InstanceId, 1));
        if (rootExpires is int rt) E.PotContents(s, pot.InstanceId).Single(i => i.ItemId == "raw_root").ExpiresAt = s.GameMinute + rt;
        s.Items.RemoveAll(i => i.ItemId == "raw_meat");
        E.DebugGive(s, "raw_meat", 1, "camp");
        var meat = First(s, "raw_meat", "camp");
        if (meatExpires is int at) meat.ExpiresAt = s.GameMinute + at;
        Do(s, InstantRequest.PutInFireSlot(meat.InstanceId, 1, 1));
        LightFire(s);
        WaitMinutes(s, 10);
        return (pot, s.Items.Single(i => i.ItemId == "raw_meat"));
    }

    /// <summary>
    /// B51-Q778-02—06（火堆，S11 Q877 用户新裁定）：「全部取出」把整口锅（连锅水、食材）与直接烹饪的生肉一次放到本区地面——
    /// 锅内物随锅不另列，相关加工进度一次解除；已烧燃料不退、火照常燃烧，0 分钟。结果「已取出至临时营地地面：金属锅×1、生肉×1。」（Q876 B），
    /// 不报中止批数（Q878）；生肉未过原保质不接腐败句（Q1009 配对）。
    /// </summary>
    [Test]
    public void Q877_FireTakeAll_WholePotWithContents_AndDirectFood()
    {
        var s = New();
        var (pot, meat) = FireWithPotAndMeat(s);
        var fac = s.Locations["camp"].Facility;
        Assert.That(fac.Processes, Has.Count.EqualTo(2), "起点：锅里生块根水煮、直烤生肉各一个加工");
        Assert.That(E.PotContents(s, pot.InstanceId).Select(i => i.ItemId).OrderBy(x => x), Is.EqualTo(new[] { "clean_water", "raw_root" }));
        var fuel = fac.FuelMinutes;
        var c = s.GameMinute;
        Assert.That(E.TakeAllBlocked(s, "camp", GameEngine.FireTakeAllId), Is.Null, "Q879 A：可用无悬停");

        Ok(TakeAllNow(s, GameEngine.FireTakeAllId, "fire-1"));

        Assert.That(s.GameMinute, Is.EqualTo(c), "0 分钟");
        Assert.That((pot.Owner, pot.InFacility), Is.EqualTo(("camp", false)), "整锅到地面");
        Assert.That(E.PotContents(s, pot.InstanceId).Select(i => i.ItemId).OrderBy(x => x), Is.EqualTo(new[] { "clean_water", "raw_root" }), "锅内物随锅");
        Assert.That((meat.Owner, meat.InFacility), Is.EqualTo(("camp", false)), "直接烹饪食材到地面");
        Assert.That(fac.Processes, Is.Empty, "相关加工一次解除");
        Assert.That(E.FireContents(s, "camp"), Is.Empty, "加工位全空");
        Assert.That((fac.FireLit, fac.FuelMinutes), Is.EqualTo((true, fuel)), "已烧燃料不退、火照常");
        Assert.That(s.Log.Last().Text, Is.EqualTo("已取出至临时营地地面：金属锅×1、生肉×1。"));
        Assert.That(E.TakeAllBlocked(s, "camp", GameEngine.FireTakeAllId), Is.EqualTo(Empty), "空设施置灰原因");
    }

    /// <summary>
    /// B51C2A1-Q1009-01（火堆全部取出部分，S11 Q1009 A）：受热保护中原保质已过的独立食材在本次取出中转为腐败物——
    /// 清单按转换后列「腐败物×1」，清单后接一次原转换结果句「生肉×1已腐败，变为腐败物×1。」（合同 conversionInPlace 模板）。
    /// </summary>
    [Test]
    public void Q1009_FireTakeAll_ExpiredProtectedFood_ListedAsSpoiled_OneConversionLine()
    {
        var s = New();
        var (pot, meat) = FireWithPotAndMeat(s, meatExpires: 5);
        Assert.That(E.ProcessOf(s, meat)!.HeatProtected, Is.True, "起点：受热保护中");
        Assert.That(E.IsSpoiled(s, meat), Is.False, "保护期间不腐败");
        Assert.That(meat.ExpiresAt, Is.LessThan(s.GameMinute), "原保质已过");

        Ok(TakeAllNow(s, GameEngine.FireTakeAllId));

        Assert.That(s.Log.Last().Text, Is.EqualTo("已取出至临时营地地面：金属锅×1、腐败物×1。生肉×1已腐败，变为腐败物×1。"));
        Assert.That((GroundSum(s, "spoiled_matter"), GroundSum(s, "raw_meat")), Is.EqualTo((1, 0)), "地面确实是腐败物×1");
        Assert.That(pot.Owner, Is.EqualTo("camp"));
    }

    /// <summary>
    /// Q1009（锅内）：「整锅仍按Q876作为一件移出，内容随原归属，锅内物未独立落地就不重复列为散件」——锅内受热保护且原保质已过的生块根随锅在锅里转为腐败物，
    /// 清单只写锅名，清单后接一次原转换句（与单件取出同一受热保护判定）。
    /// </summary>
    [Test]
    public void Q1009_FireTakeAll_ExpiredRootInPot_StaysInPot_ConversionLineOnce()
    {
        var s = New();
        var (pot, _) = FireWithPotAndMeat(s, rootExpires: 5);
        var root = E.PotContents(s, pot.InstanceId).Single(i => i.ItemId == "raw_root");
        Assert.That(E.ProcessOf(s, root)!.HeatProtected, Is.True, "起点：受热保护中");

        Ok(TakeAllNow(s, GameEngine.FireTakeAllId));

        Assert.That(s.Log.Last().Text, Is.EqualTo("已取出至临时营地地面：金属锅×1、生肉×1。生块根×1已腐败，变为腐败物×1。"));
        Assert.That(E.PotContents(s, pot.InstanceId).Select(i => i.ItemId).OrderBy(x => x), Is.EqualTo(new[] { "clean_water", "spoiled_matter" }), "在锅里转换、随锅");
        Assert.That(GroundSum(s, "spoiled_matter"), Is.EqualTo(0), "未独立落地");
    }

    /// <summary>
    /// FAC-Q871-01／02（火堆）：取出门槛——主行动进行／暂停、子行动「行动进行中，暂不能整理物品」，战斗沿火堆原句，异地「异地不能取用设施里的物品」；
    /// 火堆里锅的单件取出走同一取出门槛；放入仍原门槛（锅在地面、主行动进行时 PutInFire 沿「行动进行中，不能整理物品」）。
    /// </summary>
    [Test]
    public void Q871_FireTakeGate_Busy_Battle_Elsewhere_PutInKeepsOldGate()
    {
        var s = New();
        var (pot, _) = FireWithPotAndMeat(s);
        Assert.That(E.TakeOutBlocked(s, pot, "camp"), Is.Null, "空闲可取");
        Start(s, ActionRequest.Wait(30));
        Assert.That(E.TakeAllBlocked(s, "camp", GameEngine.FireTakeAllId), Is.EqualTo(Busy));
        Assert.That(E.TakeOutBlocked(s, pot, "camp"), Is.EqualTo(Busy), "火堆单件取出同门槛");
        var r = TakeAllNow(s, GameEngine.FireTakeAllId);
        Assert.That((r.Ok, r.Reason), Is.EqualTo((false, Busy)));
        Assert.That(pot.InFacility, Is.True, "被拒不动");
        s.Main!.Status = ActionStatus.Paused;
        Assert.That(E.TakeAllBlocked(s, "camp", GameEngine.FireTakeAllId), Is.EqualTo(Busy), "暂停也算");
        E.Cancel(s);
        s.Sub = new ActionState { IsSub = true };
        Assert.That(E.TakeAllBlocked(s, "camp", GameEngine.FireTakeAllId), Is.EqualTo(Busy), "只有子行动也拒");
        s.Sub = null;
        s.Battle = new BattleState();
        Assert.That(E.TakeAllBlocked(s, "camp", GameEngine.FireTakeAllId), Is.EqualTo("战斗中不能整理物品；换武器、装填与用药请用战斗指令"), "火堆即时操作原战斗句");
        s.Battle = null;
        Assert.That(E.TakeAllBlocked(s, "h02_square", GameEngine.FireTakeAllId), Is.EqualTo("异地不能取用设施里的物品"));
        Assert.That(E.TakeAllBlocked(s, "camp", GameEngine.FireTakeAllId), Is.Null, "恢复空闲可取");

        // 放入仍原门槛：锅取到地面后主行动进行中再放回火堆
        Ok(E.DoInstant(s, InstantRequest.TakeOut(pot.InstanceId, "camp")));
        Start(s, ActionRequest.Wait(30));
        Assert.That(E.DoInstant(s, InstantRequest.PutInFire(pot.InstanceId)).Reason, Is.EqualTo("行动进行中，不能整理物品"));
    }

    /// <summary>B51C-Q879-01（火堆）：快照戳过期整次拒「物品已变，请重选。」不动；同一请求号只生效一次（配对：首次确实取出）。</summary>
    [Test]
    public void Q879_FireTakeAll_StaleStamp_RequestIdOnce()
    {
        var s = New();
        var pot = Pot(s);
        Do(s, InstantRequest.PutInFire(pot.InstanceId));
        var stale = E.TakeAllStamp(s, "camp", GameEngine.FireTakeAllId);
        E.DebugGive(s, "raw_meat", 1, "camp");
        Do(s, InstantRequest.PutInFireSlot(First(s, "raw_meat", "camp").InstanceId, 1, 1));
        var r = E.TakeAll(s, "camp", GameEngine.FireTakeAllId, stale, "f-1");
        Assert.That((r.Ok, r.Reason), Is.EqualTo((false, Changed)));
        Assert.That(E.FireContents(s, "camp"), Has.Count.EqualTo(2), "两件都不动");

        Ok(TakeAllNow(s, GameEngine.FireTakeAllId, "f-2"));
        Assert.That(E.FireContents(s, "camp"), Is.Empty, "首次确实取出");
        Do(s, InstantRequest.PutInFire(pot.InstanceId));
        var again = TakeAllNow(s, GameEngine.FireTakeAllId, "f-2");
        Assert.That((again.Ok, again.Duplicate), Is.EqualTo((true, true)));
        Assert.That(pot.InFacility, Is.True, "重放不取第二次");
    }

    /// <summary>Q778 范围：「全部取出」支持四类被动设施（陶窑、固定净水台、晾制架、火堆）；Q966：晾架暂停说明用户原句「晾晒时，不会变质」。</summary>
    [Test]
    public void Q778_Supported_Q966_PauseText()
    {
        Assert.That(new[] { "kiln", "water_station", GameEngine.DryingStationId, GameEngine.FireTakeAllId }.All(GameEngine.TakeAllSupported), Is.True);
        Assert.That(GameEngine.TakeAllSupported("workbench"), Is.False, "范围外设施不支持");
        Assert.That(GameEngine.DryingShelfPauseText, Is.EqualTo("晾晒时，不会变质"));
    }

    // ───────────── 变异测试补测（基线 513fd622..7d1058b3） ─────────────

    /// <summary>在当前区晾制架补一个已停止的位（异常起点，只造起点；同 B53b SpoiledStop）。</summary>
    private static DryingSlot StoppedSlot(GameState s, int slot, string reason, int endMinute) =>
        AddSlot(s, new DryingSlot
        {
            Slot = slot, BatchId = 5400 + slot, RecipeId = "dry_berries", Status = DryingSlot.Spoiled, StartMinute = endMinute - 60, DoneMinutes = 55,
            RequiredMinutes = 120, EndMinute = endMinute, InitiatorActorId = GameState.PlayerId, StopReason = reason,
        });

    private static DryingSlot AddSlot(GameState s, DryingSlot b)
    {
        s.Locations[s.Ground].Facility.DryingSlots.Add(b);
        return b;
    }

    /// <summary>
    /// B51C-Q879-01（快照戳范围）：本位「取出」的戳与整座「全部取出」的戳不能互用——哪怕只有这一位，拿整座的戳取单位也拒「物品已变，请重选。」（配对：本位戳确实取出）。
    /// </summary>
    [Test]
    public void MT_DryingStamp_ScopeBound()
    {
        var s = Rack();
        Make(s, "berry", 2, ItemInstance.Carried);
        Ok(E.StartDrying(s, "dry_berries"));
        var all = E.DryingTakeStamp(s, s.Ground);
        Assert.That(all, Is.EqualTo(E.TakeAllStamp(s, s.Ground, GameEngine.DryingStationId)), "起点：整座戳");
        var r = E.TakeDryingSlot(s, 0, all);
        Assert.That((r.Ok, r.Reason), Is.EqualTo((false, Changed)));
        Assert.That(DrySlots(s), Has.Count.EqualTo(1), "被拒不动");
        Ok(E.TakeDryingSlot(s, 0, E.DryingTakeStamp(s, s.Ground, 0)));
        Assert.That(DrySlots(s), Is.Empty);
    }

    /// <summary>
    /// B51C-Q879-01（份数变化）：渲染后位内同一份物品的份数变了（实例不变），单位取出与全部取出都整次拒「物品已变，请重选。」；重取戳后按真实份数取出（配对）。
    /// </summary>
    [Test]
    public void MT_DryingStamp_CountChanged_Rejected()
    {
        var s = Rack();
        Make(s, "berry", 2, ItemInstance.Carried);
        Ok(E.StartDrying(s, "dry_berries"));
        var slotStamp = E.DryingTakeStamp(s, s.Ground, 0);
        var allStamp = E.TakeAllStamp(s, s.Ground, GameEngine.DryingStationId);
        var berry = s.Items.Single(i => i.ItemId == "berry");
        berry.Count = 3;   // 起点：同一实例份数变了
        var r = E.TakeDryingSlot(s, 0, slotStamp);
        Assert.That((r.Ok, r.Reason), Is.EqualTo((false, Changed)));
        var ra = E.TakeAll(s, s.Ground, GameEngine.DryingStationId, allStamp);
        Assert.That((ra.Ok, ra.Reason), Is.EqualTo((false, Changed)));
        Assert.That(berry.InDrying, Is.True, "被拒不动");
        Ok(TakeDryingNow(s, 0));
        Assert.That(s.Log.Last().Text, Is.EqualTo("已取出至临时营地地面：野果×3。"));
    }

    /// <summary>
    /// B51C-Q879-01（位状态变化）：渲染「全部取出」后这一位从晾制中停下（位内物品没变），整座计划从「中止在制」变成「取停止位余物」，整次拒「物品已变，请重选。」。
    /// </summary>
    [Test]
    public void MT_DryingStamp_StatusChanged_Rejected()
    {
        var s = Rack();
        Make(s, "berry", 2, ItemInstance.Carried);
        Ok(E.StartDrying(s, "dry_berries"));
        var stamp = E.TakeAllStamp(s, s.Ground, GameEngine.DryingStationId);
        var b = DrySlots(s).Single();
        b.Status = DryingSlot.Spoiled;   // 起点：位停下，物品不变
        b.StopReason = DryingSlot.StopUnknown;
        b.EndMinute = s.GameMinute;
        var r = E.TakeAll(s, s.Ground, GameEngine.DryingStationId, stamp);
        Assert.That((r.Ok, r.Reason), Is.EqualTo((false, Changed)));
        Assert.That(DrySlots(s), Has.Count.EqualTo(1));
    }

    /// <summary>
    /// S11 Q880「原料已腐败：只能清理… → 原料已腐败。」：晾制中的位里只要有一份原料已腐败（旧档未受保护的在制批），本位「取出」就拒「原料已腐败。」；
    /// 位里没有物品的异常晾制位拒「没有可取出的物品。」（Q879 A）。
    /// </summary>
    [Test]
    public void MT_DryingSlotTake_PartlySpoiled_AndEmpty()
    {
        var s = Rack();
        Make(s, "raw_meat", 1, ItemInstance.Carried);
        Make(s, "salt", 1, ItemInstance.Carried);
        Ok(E.StartDrying(s, "dry_meat"));
        var meat = s.Items.Single(i => i.ItemId == "raw_meat");
        meat.ShelfPausedMinutes = null;   // 起点：旧档未受保护、已过期的在制原料
        meat.ExpiresAt = s.GameMinute - 1;
        Assert.That(E.IsSpoiled(s, meat), Is.True);
        Assert.That(E.IsSpoiled(s, s.Items.Single(i => i.ItemId == "salt")), Is.False, "盐没坏：只是一份坏");
        Assert.That(E.DryingSlotTakeBlocked(s, 0), Is.EqualTo("原料已腐败。"));

        AddSlot(s, new DryingSlot
        {
            Slot = 3, BatchId = 5403, RecipeId = "dry_berries", Status = DryingSlot.Drying, StartMinute = s.GameMinute, RequiredMinutes = 120,
            InitiatorActorId = GameState.PlayerId,
        });
        Assert.That(E.DryingSlotTakeBlocked(s, 3), Is.EqualTo(Empty), "位内无物品");
    }

    /// <summary>
    /// S11 Q880 映射表（停止位说明）：已停止且仍有余物的位说明末「；取出剩余物品。」、悬停「取出剩余物品。」；余物已空的位说明以「。」收尾、无悬停（暂用拼接，见开发记录 B51c2a2.5）。
    /// 这里用腐败停止与原因未知两类停止位（缺料停止见 B50a Q713）。
    /// </summary>
    [Test]
    public void MT_StoppedSlotLine_RestOrNone()
    {
        var s = Rack();
        var end = s.GameMinute - 5;
        StoppedSlot(s, 0, DryingSlot.StopSpoiled, end);
        StoppedSlot(s, 1, DryingSlot.StopSpoiled, end);
        StoppedSlot(s, 2, DryingSlot.StopUnknown, end);
        Make(s, "salt", 1, ItemInstance.DryingOwner(s.Ground, 0));
        var views = E.DryingSlotViews(s).Where(v => v.Status == DryingSlot.Spoiled).OrderBy(v => v.Slot).ToList();
        Assert.That(views, Has.Count.EqualTo(3));
        var at = Clock.DayTime(end);
        Assert.That((views[0].Line, views[0].CleanupHint), Is.EqualTo(($"晾制果干在{at}停止，没有成品；取出剩余物品。", (string?)"取出剩余物品。")));
        Assert.That((views[1].Line, views[1].CleanupHint), Is.EqualTo(($"晾制果干在{at}停止，没有成品。", (string?)null)));
        Assert.That((views[2].Line, views[2].CleanupHint), Is.EqualTo(("晾制果干已停止，无成品。", (string?)null)));
    }

    /// <summary>
    /// S11 Q778（全部取出只移实物）＋Q880（异常空位用「清空」复位）：晾架「全部取出」不动没有余物的异常停止位，它仍在、仍可「清空」；
    /// 只剩这种位时「全部取出」为「没有可取出的物品。」。
    /// </summary>
    [Test]
    public void MT_DryingTakeAll_SkipsEmptyStoppedSlot()
    {
        var s = Rack();
        Make(s, "berry", 2, ItemInstance.Carried);
        Ok(E.StartDrying(s, "dry_berries"));
        StoppedSlot(s, 1, DryingSlot.StopUnknown, s.GameMinute - 5);
        Ok(TakeAllNow(s, GameEngine.DryingStationId));
        Assert.That(DrySlots(s).Select(b => b.Slot), Is.EqualTo(new[] { 1 }), "空的异常位不被全部取出复位");
        Assert.That(s.Log.Last().Text, Is.EqualTo("已取出至临时营地地面：野果×2。"));
        Assert.That(E.TakeAllBlocked(s, s.Ground, GameEngine.DryingStationId), Is.EqualTo(Empty));
        Ok(E.TakeDrying(s, 1, false));
        Assert.That(s.Log.Last().Text, Is.EqualTo("已清空。"));
    }

    /// <summary>Q879 A：当前区没有火堆／柴炉时火堆「全部取出」为「没有可取出的物品。」（配对：营地有火堆、有锅时可取）。</summary>
    [Test]
    public void MT_FireTakeAll_NoFireHere_Empty()
    {
        var s = New();
        Do(s, InstantRequest.PutInFire(Pot(s).InstanceId));
        Assert.That(E.TakeAllBlocked(s, "camp", GameEngine.FireTakeAllId), Is.Null);
        s.CurrentLocation = "field";   // 起点：人在没有火堆的地点
        Assert.That(E.HasFireAt(s, s.Ground), Is.False, "起点：此地无火堆");
        Assert.That(E.TakeAllBlocked(s, s.Ground, GameEngine.FireTakeAllId), Is.EqualTo(Empty));
        var r = TakeAllNow(s, GameEngine.FireTakeAllId);
        Assert.That((r.Ok, r.Reason), Is.EqualTo((false, Empty)));
    }

    /// <summary>B51C-Q879-01（火堆份数变化）：渲染后加工位上同一份食材的份数变了（实例不变）整次拒「物品已变，请重选。」（配对：新戳确实取出）。</summary>
    [Test]
    public void MT_FireStamp_CountChanged_Rejected()
    {
        var s = New();
        s.Items.RemoveAll(i => i.ItemId == "raw_meat");
        E.DebugGive(s, "raw_meat", 1, "camp");
        var meat = First(s, "raw_meat", "camp");
        Do(s, InstantRequest.PutInFireSlot(meat.InstanceId, 0, 1));
        var stamp = E.TakeAllStamp(s, "camp", GameEngine.FireTakeAllId);
        meat.Count = 2;   // 起点：同一实例份数变了
        var r = E.TakeAll(s, "camp", GameEngine.FireTakeAllId, stamp);
        Assert.That((r.Ok, r.Reason), Is.EqualTo((false, Changed)));
        Assert.That(meat.InFacility, Is.True);
        Ok(TakeAllNow(s, GameEngine.FireTakeAllId));
        Assert.That(s.Log.Last().Text, Is.EqualTo("已取出至临时营地地面：生肉×2。"));
    }

    /// <summary>
    /// Q1009／IsSpoiled 口径（原截止分钟＝当前分钟即算到期，同单件取出 Q16）：受热保护中原截止恰为此刻的生肉，全部取出时也转为腐败物，
    /// 转换句只在结果句里接一次，不另写一条日志。
    /// </summary>
    [Test]
    public void MT_FireTakeAll_ExpiresExactlyNow_Converted_OneLogLine()
    {
        var s = New();
        var (_, meat) = FireWithPotAndMeat(s, meatExpires: 10);   // 受热 10 分钟后原截止＝此刻
        Assert.That(meat.ExpiresAt, Is.EqualTo(s.GameMinute), "起点：原截止恰为此刻");
        Assert.That(E.ProcessOf(s, meat)!.HeatProtected, Is.True);
        var n0 = s.Log.Count;
        Ok(TakeAllNow(s, GameEngine.FireTakeAllId));
        Assert.That(s.Log.Skip(n0).Select(l => l.Text), Is.EqualTo(new[] { "已取出至临时营地地面：金属锅×1、腐败物×1。生肉×1已腐败，变为腐败物×1。" }), "只这一条");
    }

    /// <summary>合堆（原取出口径：放到地面后同物同期限合成一堆）：火堆全部取出的生肉落地后与地面上同期限的生肉合成一堆。</summary>
    [Test]
    public void MT_FireTakeAll_MergesWithGroundStack()
    {
        var s = New();
        s.Items.RemoveAll(i => i.ItemId == "raw_meat");
        E.DebugGive(s, "raw_meat", 2, "camp");
        var meat = First(s, "raw_meat", "camp");
        var exp = meat.ExpiresAt;
        Do(s, InstantRequest.PutInFireSlot(meat.InstanceId, 0, 1));
        Assert.That(s.Items.Where(i => i.ItemId == "raw_meat").Select(i => (i.InFacility, i.Count)).OrderBy(x => x.InFacility),
            Is.EqualTo(new[] { (false, 1), (true, 1) }), "起点：地面一份、火堆一份");
        Ok(TakeAllNow(s, GameEngine.FireTakeAllId));
        var ground = s.Items.Where(i => i.ItemId == "raw_meat").ToList();
        Assert.That(ground, Has.Count.EqualTo(1), "合成一堆");
        Assert.That((ground[0].Owner, ground[0].Count, ground[0].ExpiresAt), Is.EqualTo(("camp", 2, exp)));
    }

    /// <summary>
    /// Q877／Q1009（锅食谱作废）：锅里正在烘制远行硬饼（谷物×2＋辅料盐×1）时全部取出——锅连谷物到地面，硬饼整份作废，辅料盐按原规则真实退回地面，
    /// 盐独立落地所以列在锅之后，直接烹饪的生肉随后；锅食谱进度清除。清单「金属锅×1、盐×1、生肉×1」（Q876 B 按真实移出先后）。
    /// </summary>
    [Test]
    public void MT_FireTakeAll_PotRecipeAuxReturned_ListedAfterPot()
    {
        var s = New();
        E.DebugLearnAllKnowledge(s);
        s.Skills["cooking"].Level = 5;
        s.Items.RemoveAll(i => i.ItemId is "grain" or "salt" or "raw_meat" or "clean_water");
        var pot = Pot(s);
        E.DebugGive(s, "clean_water", 10, "camp");
        Do(s, InstantRequest.FillPot(First(s, "clean_water", "camp").InstanceId, pot.InstanceId, 10));
        if (pot.IsCarried) Do(s, InstantRequest.Drop(pot.InstanceId, 1));
        Do(s, InstantRequest.PutInFire(pot.InstanceId));
        LightFire(s);
        E.CreateItems(s, "grain", 2, "camp");
        E.CreateItems(s, "salt", 1, "camp");
        Do(s, InstantRequest.LoadPotRecipe("pot_hardtack", pot.InstanceId));
        E.DebugGive(s, "raw_meat", 1, "camp");
        Do(s, InstantRequest.PutInFireSlot(First(s, "raw_meat", "camp").InstanceId, 1, 1));
        WaitMinutes(s, 10);
        Assert.That(E.PotContents(s, pot.InstanceId).Count(i => i.ItemId == "salt"), Is.EqualTo(1), "起点：辅料盐在锅里");

        Ok(TakeAllNow(s, GameEngine.FireTakeAllId));

        Assert.That(s.Log.Last().Text, Is.EqualTo("已取出至临时营地地面：金属锅×1、盐×1、生肉×1。"));
        Assert.That(s.Items.Where(i => i.ItemId == "salt").Select(i => (i.Owner, i.Count)), Is.EqualTo(new[] { ("camp", 1) }), "盐退回地面一次");
        Assert.That(E.PotContents(s, pot.InstanceId).Where(i => i.ItemId == "grain").Sum(i => i.Count), Is.EqualTo(2), "谷物随锅");
        Assert.That(s.Locations["camp"].Facility.Processes, Is.Empty);
    }

    // ───────────── 变异测试第二轮补测（基线 513fd622..dc99337a） ─────────────

    /// <summary>
    /// S11 Q871 B（只扩四类被动设施取出，「不扩禁普通地面随身／添料原业务」）：不在火堆上的锅里取食材仍按原门槛——主行动进行中拒原句「行动进行中，不能整理物品」；
    /// 配对：同一口锅放进火堆后，取锅里食材改走取出门槛「行动进行中，暂不能整理物品」。
    /// </summary>
    [Test]
    public void MT2_TakeOut_PotNotInFire_KeepsOldGate_InFireUsesTakeGate()
    {
        var s = New();
        var pot = Pot(s);
        Assert.That(pot.InFacility, Is.False, "起点：锅不在火堆上");
        E.DebugGive(s, "raw_root", 1, "camp");
        Do(s, InstantRequest.PutFoodInPot(First(s, "raw_root", "camp").InstanceId, pot.InstanceId, 1));
        var root = E.PotContents(s, pot.InstanceId).Single(i => i.ItemId == "raw_root");
        Start(s, ActionRequest.Wait(30));
        Assert.That(E.TakeOutBlocked(s, root, "camp"), Is.EqualTo("行动进行中，不能整理物品"), "不在火堆：原门槛");
        E.Cancel(s);
        Do(s, InstantRequest.PutInFire(pot.InstanceId));
        Start(s, ActionRequest.Wait(30));
        Assert.That(E.TakeOutBlocked(s, root, "camp"), Is.EqualTo(Busy), "火堆上锅里：取出门槛");
    }

    /// <summary>
    /// 腐败停止位说明（B53a 原句「{配方名}在{时刻}停止，没有成品」）：旧档没有停止时刻的位省去「在{时刻}」，余物已空时以「。」收尾（Q880 暂用拼接）。
    /// </summary>
    [Test]
    public void MT2_SpoiledStopLine_NoEndMinute()
    {
        var s = Rack();
        AddSlot(s, new DryingSlot
        {
            Slot = 0, BatchId = 5500, RecipeId = "dry_berries", Status = DryingSlot.Spoiled, StartMinute = s.GameMinute - 60, DoneMinutes = 55,
            RequiredMinutes = 120, EndMinute = null, InitiatorActorId = GameState.PlayerId, StopReason = DryingSlot.StopSpoiled,
        });
        var v = E.DryingSlotViews(s).Single(x => x.Slot == 0);
        Assert.That(v.Line, Is.EqualTo("晾制果干停止，没有成品。"));
    }
}
