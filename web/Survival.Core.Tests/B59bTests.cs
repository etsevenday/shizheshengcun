using Newtonsoft.Json.Linq;
using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 批次 B59b「晾制保鲜冻结与到期即转腐败」：
/// S11／S14／S06／S08／S10／R06「2026-11-20 Q769、Q770」（验收 Q769-01—03、Q770-01—03）；
/// S11／S14「2026-11-24 Q1033–Q1039」§Q1033、§Q1034（留位冻结）、§Q1036、§Q1037（验收 Q1033-02、Q1036-01／02、Q1037-01、B51C2A2-Q1028-02 取出后恢复保鲜计时）；
/// 02《开发方待确认问题》Q769、Q770（0fef71cc 补正）、Q1036、Q1037 的策划答复。
/// 期望字面量来自起点里设定的原截止与策划规则（冻结 R＝原截止−冻结分钟、取出后截止＝取出分钟＋R），不从引擎取值再断引擎；起点只用 CreateItems／直接改截止，
/// 结论走真实拖放、世界推进、取出与读档入口。
/// </summary>
public class B59bTests : TestBase
{
    protected override bool NoEnemyApproach => true;

    private const string Here = "camp";

    private void Ok(ActionResult r) => Assert.That(r.Ok, Is.True, r.Reason);

    private static List<DryingSlot> DrySlots(GameState s) => s.Locations[s.Ground].Facility.DryingSlots;

    private static string Slot(GameState s, int k) => ItemInstance.DryingOwner(s.Ground, k);

    private static List<string> LogSince(GameState s, int n0) => s.Log.Skip(n0).Select(l => l.Text).ToList();

    /// <summary>临时营地建好晾制架（同 B59a 起点），清掉地面与随身物品，烹饪 5 级（肉干门槛）。</summary>
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

    /// <summary>材料卡拖到晾制位并按 count 份提交（同网页壳 ExecuteDrop）。</summary>
    private void DropIn(GameState s, ItemInstance i, int slot, int count)
    {
        var d = E.ResolveDrop(s, DropSource.Parse($"card:{i.Owner}|{i.ItemId}|{i.InstanceId}")!, DropTarget.Parse($"dryslot:{slot}")!);
        Assert.That(d.Ok, Is.True, d.Reason);
        var r = d.Instant!;
        r.Count = count;
        r.RequestId = Guid.NewGuid().ToString("N");
        Ok(E.DoInstant(s, r));
    }

    private void TakeCard(GameState s, ItemInstance card, int count, string zone) =>
        Ok(E.DoInstant(s, InstantRequest.FacilityTake(card.InstanceId, count, zone, E.FacilityItemStamp(s, card))));

    /// <summary>营地火堆燃烧、锅放进火堆并装 water 份净水（每份 100 毫升）。</summary>
    private (GameState s, ItemInstance pot) PotOnFire(int water, int fuel = 240)
    {
        var s = New();
        E.DebugLearnAllKnowledge(s);
        s.Skills["cooking"].Level = 5;
        s.Items.RemoveAll(i => i.ItemId is "grain" or "salt" or "raw_meat" or "raw_root" or "clean_water" or "dirty_water");
        var pot = First(s, "pot");
        if (water > 0)
        {
            E.DebugGive(s, "clean_water", water, Here);
            Ok(E.DoInstant(s, InstantRequest.FillPot(First(s, "clean_water", Here).InstanceId, pot.InstanceId, water)));
        }
        if (pot.IsCarried) Ok(E.DoInstant(s, InstantRequest.Drop(pot.InstanceId, 1)));
        Ok(E.DoInstant(s, InstantRequest.PutInFire(pot.InstanceId)));
        Lit(s, fuel);
        return (s, pot);
    }

    private static void Lit(GameState s, int fuel = 240)
    {
        var f = s.Locations[Here].Facility;
        f.FireLit = true;
        f.FuelMinutes = fuel;
    }

    private void WaitMinutes(GameState s, int minutes) => AdvanceTo(s, s.GameMinute + minutes);

    // ───────────── R326（Q769）：晾制位原料与余料冻结保鲜 ─────────────

    /// <summary>
    /// Q769-01：临期野果（原截止＝投入后 30 分钟）拖入开晾，推进越过原截止——位内原料不衰减、不转换（R 仍 30）；
    /// 取出一份到地面：取出的这份从取出时刻＋30 继续计时，留位余料继续冻结（R 30、截止暂空，S11 §Q769「留在同一晾制位的真实余料仍保留各自的冻结余量」）；
    /// 再推进 120 分钟，余料仍是野果 R 30，取出的那份在自己的截止转为腐败物（对照：移出后确实恢复计时）。
    /// </summary>
    [Test]
    public void Q769_01_NearExpiry_NoDecayInSlot_PartialTake_ResidualKeeps_TakenResumes()
    {
        var s = Rack();
        var t0 = s.GameMinute;
        DropIn(s, Make(s, "berry", 2, ItemInstance.Carried, t0 + 30), 0, 2);
        Assert.That(DrySlots(s).Single().Status, Is.EqualTo(DryingSlot.Drying), "起点：齐料开晾");

        WaitMinutes(s, 60);
        var inSlot = E.DryingContents(s, s.Ground, 0);
        Assert.That(inSlot.Select(i => (i.ItemId, i.Count, i.ExpiresAt, i.ShelfPausedMinutes)), Is.EqualTo(new[] { ("berry", 2, (int?)null, (int?)30) }), "越过原截止仍冻结 R＝30");
        Assert.That(DrySlots(s).Single().Status, Is.EqualTo(DryingSlot.Drying), "没有腐败停止");

        var c = s.GameMinute;
        TakeCard(s, inSlot[0], 1, s.Ground);
        var ground = s.Items.Where(i => i.ItemId == "berry" && i.Owner == s.Ground).ToList();
        Assert.That(ground.Select(i => (i.Count, i.ExpiresAt, i.ShelfPausedMinutes)), Is.EqualTo(new[] { (1, (int?)(c + 30), (int?)null) }), "取出的份额从取出时刻恢复计时");
        var rest = E.DryingContents(s, s.Ground, 0);
        Assert.That(rest.Select(i => (i.ItemId, i.Count, i.ExpiresAt, i.ShelfPausedMinutes)), Is.EqualTo(new[] { ("berry", 1, (int?)null, (int?)30) }), "留位余料继续冻结");
        Assert.That(DrySlots(s).Single().Status, Is.EqualTo(DryingSlot.Residual));

        WaitMinutes(s, 120);
        Assert.That(E.DryingContents(s, s.Ground, 0).Select(i => (i.ItemId, i.ShelfPausedMinutes)), Is.EqualTo(new[] { ("berry", (int?)30) }), "余料在位内不衰减");
        Assert.That(s.Items.Where(i => i.Owner == s.Ground).Select(i => (i.ItemId, i.Count)), Is.EqualTo(new[] { (GameEngine.SpoiledMatterId, 1) }), "取出的那份到期转为腐败物");
    }

    /// <summary>
    /// Q1033-02（S11 §Q1033「合法投入后，这次物品实际进入原位并冻结保鲜」「只投入一份生肉时，留原位继续等盐」）：只拖生肉（原截止＝投入后 20 分钟）——
    /// 位内冻结 R＝20、截止暂空；推进越过原截止仍是生肉，没有缺料／腐败日志，摘要不计缺料或余料（§Q769「不计「缺料{n}」」、S14 Q1034-02）；
    /// 再拖盐即开晾（新批、进度 0），生肉冻结量不刷新（§Q769「保鲜量不刷新」）。
    /// </summary>
    [Test]
    public void Q1033_02_MeatOnly_FrozenWhileWaitingSalt_NoMissingFeedback_RNotRefreshed()
    {
        var s = Rack();
        var t0 = s.GameMinute;
        DropIn(s, Make(s, "raw_meat", 1, ItemInstance.Carried, t0 + 20), 0, 1);
        var meat = E.DryingContents(s, s.Ground, 0).Single();
        Assert.That((meat.ItemId, meat.ExpiresAt, meat.ShelfPausedMinutes), Is.EqualTo(("raw_meat", (int?)null, (int?)20)), "新投未齐也冻结");
        Assert.That(DrySlots(s).Single().Status, Is.EqualTo(DryingSlot.Residual));

        var n0 = s.Log.Count;
        WaitMinutes(s, 60);
        Assert.That((meat.ItemId, meat.ShelfPausedMinutes), Is.EqualTo(("raw_meat", (int?)20)), "越过原截止仍是生肉");
        Assert.That(LogSince(s, n0), Has.None.Contains("腐败").And.None.Contains("原料不全").And.None.Contains("缺料"), "无缺料／腐败反馈");
        Assert.That(E.DryingSummaryParts(s), Is.EqualTo(new[] { "空7" }), "不计缺料、不列余料位数");

        DropIn(s, Make(s, "salt", 1, ItemInstance.Carried), 0, 1);
        var b = DrySlots(s).Single();
        Assert.That((b.Status, b.DoneMinutes), Is.EqualTo((DryingSlot.Drying, 0)), "齐料开新批、进度 0");
        Assert.That((meat.ItemId, meat.ExpiresAt, meat.ShelfPausedMinutes), Is.EqualTo(("raw_meat", (int?)null, (int?)20)), "冻结量不刷新");
    }

    /// <summary>
    /// Q769-02：部分取出后的余料不足一批——没有缺料提示、摘要计数或停止日志；按 Q924「查看配方」补齐（StartDrying 本位）开新批、进度 0，
    /// 余料沿用原冻结量（R 30），新补的野果从补投时刻冻结（原截止＝补投后 100 分钟 → R 100）。
    /// </summary>
    [Test]
    public void Q769_02_ResidualRefill_ViaRecipe_NewBatchFromZero_ResidualRKept()
    {
        var s = Rack();
        var t0 = s.GameMinute;
        DropIn(s, Make(s, "berry", 2, ItemInstance.Carried, t0 + 30), 0, 2);
        WaitMinutes(s, 40);
        var n0 = s.Log.Count;
        TakeCard(s, E.DryingContents(s, s.Ground, 0).Single(), 1, ItemInstance.Carried);
        Assert.That(DrySlots(s).Single().Status, Is.EqualTo(DryingSlot.Residual));
        var v = E.DryingSlotViews(s)[0];
        Assert.That((v.Line, v.XpText), Is.EqualTo(("", (string?)null)), "余料位不写缺料说明");
        Assert.That(E.DryingSummaryParts(s), Is.EqualTo(new[] { "空7" }));
        Assert.That(LogSince(s, n0), Has.None.Contains("缺料").And.None.Contains("原料不全"));

        s.Items.RemoveAll(i => i.Owner == ItemInstance.Carried);
        var c = s.GameMinute;
        Make(s, "berry", 1, ItemInstance.Carried, c + 100);
        var o = E.DryingPreview(s, "dry_berries", 0);
        Assert.That(o.Ok, Is.True, o.Reason);
        Ok(E.StartDrying(s, "dry_berries", 0, null, o.SourceKey));
        var b = DrySlots(s).Single();
        Assert.That((b.Status, b.DoneMinutes, b.StartMinute), Is.EqualTo((DryingSlot.Drying, 0, c)), "补齐开新批，进度从 0");
        Assert.That(E.DryingContents(s, s.Ground, 0).Select(i => i.ShelfPausedMinutes).OrderBy(r => r), Is.EqualTo(new int?[] { 30, 100 }), "余料 R 不刷新，新料按补投时刻冻结");
    }

    /// <summary>
    /// Q769-03：已晾好的成品留在架上照常计时（不冻结），达到真实截止即在位内转为腐败物（S11 §Q769「留在架上达到真实到期照 Q770 转换」），
    /// 转换句沿原「{物}×{n}已腐败，变为腐败物×{n}。」。成品截止改短只造起点。
    /// </summary>
    [Test]
    public void Q769_03_DoneProductOnRack_ExpiresToSpoiledInPlace()
    {
        var s = Rack();
        var t0 = s.GameMinute;
        DropIn(s, Make(s, "berry", 2, ItemInstance.Carried, t0 + 600), 0, 2);
        WaitMinutes(s, 120);   // 果干晾制 120 分钟（配置 dry_berries baseMinutes，S11 晾制表）
        Assert.That(DrySlots(s).Single().Status, Is.EqualTo(DryingSlot.Done));
        var prod = E.DryingContents(s, s.Ground, 0).Single();
        Assert.That((prod.ItemId, prod.ShelfPausedMinutes), Is.EqualTo(("dried_berries", (int?)null)), "成品不冻结");
        var name = E.ItemName("dried_berries");
        prod.ExpiresAt = s.GameMinute + 10;   // 起点：缩短成品期限
        WaitMinutes(s, 5);
        Assert.That(prod.ItemId, Is.EqualTo("dried_berries"), "到期前仍是成品");
        var n0 = s.Log.Count;
        WaitMinutes(s, 5);
        Assert.That(E.DryingContents(s, s.Ground, 0).Select(i => (i.ItemId, i.Count)), Is.EqualTo(new[] { (GameEngine.SpoiledMatterId, 1) }), "在位内转为腐败物");
        Assert.That(LogSince(s, n0).Where(l => l.Contains("腐败")), Is.EqualTo(new[] { $"{name}×1已腐败，变为腐败物×1。" }), "转换句一次");
    }

    // ───────────── R327（Q770）：实际归零同次转腐败物 ─────────────

    /// <summary>
    /// Q770-01：正余量原物不腐败；真实截止那一次结算即转为腐败物（同一实例、数量与归属不变），转换句一次；读档、再过变更边界都不再转换或补句。
    /// </summary>
    [Test]
    public void Q770_01_ConvertsAtDeadline_Once_ReloadNoRepeat()
    {
        var s = Rack();
        var t0 = s.GameMinute;
        var berry = Make(s, "berry", 3, ItemInstance.Carried, t0 + 5);
        Assert.That(E.IsSpoiled(s, berry), Is.False, "正余量不是腐败");
        var n0 = s.Log.Count;
        WaitMinutes(s, 5);
        Assert.That((berry.ItemId, berry.Count, berry.Owner), Is.EqualTo((GameEngine.SpoiledMatterId, 3, ItemInstance.Carried)), "同次原位转换");
        Assert.That(LogSince(s, n0).Count(l => l == "野果×3已腐败，变为腐败物×3。"), Is.EqualTo(1));

        var n1 = s.Log.Count;
        var back = SaveSerializer.Deserialize(SaveSerializer.Serialize(s), Cfg);
        E.UpdateFlags(back);
        Assert.That(back.Log.Count, Is.EqualTo(n1), "读档不再转换、不补句");
        Assert.That(back.Items.Where(i => i.IsCarried).Select(i => (i.ItemId, i.Count)), Is.EqualTo(new[] { (GameEngine.SpoiledMatterId, 3) }));
    }

    /// <summary>
    /// Q770-02（0fef71cc 补正）：同一口锅里一份已合法受热的生块根（先投、取得 1000 毫升预留）与一份尚未开工的生块根（等水），原截止都设为 10 分钟后——
    /// 推进到旧期限：未开工的那份实际归零原位转为腐败物；已受热的那份冻结 R＝10（首段段首冻结），不转换，按原烹饪规则在 30 分钟煮成水煮块根（成品不带冻结）。
    /// </summary>
    [Test]
    public void Q770_02_SamePot_UnstartedConverts_HeatedFrozenContinues()
    {
        var (s, pot) = PotOnFire(10);
        var t0 = s.GameMinute;
        foreach (var k in new[] { 0, 1 })
        {
            var r = Make(s, "raw_root", 1, Here);
            Ok(E.DoInstant(s, InstantRequest.PutFoodInPot(r.InstanceId, pot.InstanceId, 1)));
        }
        var foods = E.PotFood(s, pot.InstanceId).OrderBy(i => i.InstanceId).ToList();
        foreach (var f in foods) f.ExpiresAt = t0 + 10;
        var (heated, waiting) = (foods[0], foods[1]);

        WaitMinutes(s, 10);
        Assert.That((heated.ItemId, heated.ShelfPausedMinutes, E.ProcessOf(s, heated)!.HeatProtected), Is.EqualTo(("raw_root", (int?)10, true)), "已受热的冻结余额保持");
        Assert.That(waiting.ItemId, Is.EqualTo(GameEngine.SpoiledMatterId), "未开工的实际归零转换");

        WaitMinutes(s, 20);
        Assert.That((heated.ItemId, heated.ShelfPausedMinutes), Is.EqualTo(("boiled_root", (int?)null)), "按原烹饪规则完成（水煮 30 分钟），成品不带冻结");
        Assert.That(heated.ExpiresAt, Is.GreaterThan(s.GameMinute), "成品从完成起自己的保质");
    }

    /// <summary>
    /// Q770-03：刚到期的实物在火堆加工位上已原位转为腐败物——取出后果不列「已过原保质期」或「取出后失去烹饪保护」，取出只记真实移动（不再报一次转换）。
    /// 对照：同一份在到期前取出时仍是原物。
    /// </summary>
    [Test]
    public void Q770_03_AlreadySpoiledInSlot_TakeOutOnlyRealMove()
    {
        var s = New();
        s.Items.RemoveAll(i => i.ItemId == "raw_meat");
        var t0 = s.GameMinute;
        var meat = Make(s, "raw_meat", 1, Here, t0 + 5);
        Ok(E.DoInstant(s, InstantRequest.PutInFireSlot(meat.InstanceId, 0, 1)));   // 火未点燃：只存放，不受热
        WaitMinutes(s, 5);
        Assert.That((meat.ItemId, meat.InFacility), Is.EqualTo((GameEngine.SpoiledMatterId, true)), "到期即在加工位原位转换");
        var c = E.TakeOutConsequenceOf(s, meat);
        Assert.That(c.Spoils, Is.Empty);
        Assert.That(c.Text, Does.Not.Contain("已过原保质期").And.Not.Contain(GameEngine.TakeOutSpoilRiskText));
        var n0 = s.Log.Count;
        Ok(E.DoInstant(s, InstantRequest.TakeOut(meat.InstanceId, Here)));
        var lines = LogSince(s, n0);
        Assert.That(lines, Has.Count.EqualTo(1));
        Assert.That(lines[0], Does.StartWith("把腐败物从").And.Not.Contain("已腐败，变为腐败物"), "只记真实移动");

        // 对照：到期前取出仍是原物
        var t = New();
        t.Items.RemoveAll(i => i.ItemId == "raw_meat");
        var m2 = Make(t, "raw_meat", 1, Here, t.GameMinute + 30);
        Ok(E.DoInstant(t, InstantRequest.PutInFireSlot(m2.InstanceId, 0, 1)));
        Ok(E.DoInstant(t, InstantRequest.TakeOut(m2.InstanceId, Here)));
        Assert.That(m2.ItemId, Is.EqualTo("raw_meat"));
    }

    // ───────────── R375／R376（Q1036、Q1037）：实际开始烹饪即冻结全部原料 ─────────────

    /// <summary>
    /// Q1036-01／Q1037-01：谷肉炖食（谷物×1＋生肉×1、净水 1000 毫升、受热 45 分钟），生肉原截止＝投料后 10 分钟（临期）——
    /// 首次有效受热在段首冻结全部已投入原料（生肉 R＝10、谷物 R＝原截止−段首）；熄火暂停 60 分钟不扣余额，越过旧截止仍是生肉、食谱不作废；
    /// 复燃后照常完成两份炖食；全程没有腐败、进度清零或作废日志。
    /// </summary>
    [Test]
    public void Q1036_01_Stew_FirstHeatFreezesAll_PauseNoDecay_CompletesPastOldDeadline_NoFakeLogs()
    {
        var (s, pot) = PotOnFire(10);
        E.CreateItems(s, "grain", 1, Here);
        E.CreateItems(s, "raw_meat", 1, Here);
        var t0 = s.GameMinute;
        var meat = First(s, "raw_meat", Here);
        var grain = First(s, "grain", Here);
        meat.ExpiresAt = t0 + 10;
        var eg = grain.ExpiresAt!.Value;
        var n0 = s.Log.Count;
        Ok(E.DoInstant(s, InstantRequest.LoadPotRecipe("pot_stew", pot.InstanceId)));
        Assert.That((meat.ShelfPausedMinutes, grain.ShelfPausedMinutes), Is.EqualTo(((int?)null, (int?)null)), "投料未受热不冻结");

        WaitMinutes(s, 5);
        Assert.That((meat.ShelfPausedMinutes, grain.ShelfPausedMinutes), Is.EqualTo(((int?)10, (int?)(eg - t0))), "首段段首冻结全部原料");

        Ok(E.DoInstant(s, InstantRequest.Extinguish()));
        WaitMinutes(s, 60);
        Assert.That((meat.ItemId, meat.ShelfPausedMinutes, grain.ShelfPausedMinutes), Is.EqualTo(("raw_meat", (int?)10, (int?)(eg - t0))), "熄火暂停不衰减，越过旧截止仍是原物");
        Assert.That(E.AllProcesses(s).Single(p => p.InputInstanceIds != null).HeatDoneMinutes, Is.EqualTo(5), "食谱不作废、热进度保留");

        Lit(s);
        WaitMinutes(s, 40);
        Assert.That(E.PotFood(s, pot.InstanceId).Select(i => (i.ItemId, i.ShelfPausedMinutes)), Is.EqualTo(new[] { ("stew_portion", (int?)null), ("stew_portion", (int?)null) }), "照常完成两份");
        Assert.That(LogSince(s, n0), Has.None.Contains("腐败").And.None.Contains("清零").And.None.Contains("作废"), "没有中途腐败、清零或作废的伪日志");
    }

    /// <summary>
    /// Q1036-02：已冻结的炖食原料主动取出生肉——原作废返料照旧（日志原作废分句「整份作废」），取出的生肉截止＝取出时刻＋冻结余额（R 10），
    /// 留在锅里的谷物随作废结束保护，同样从此刻＋自己的冻结余额继续计时（旧截止向后顺延已冻结的时长）。
    /// </summary>
    [Test]
    public void Q1036_02_FrozenTakenOut_ResumeFromRemainder_AbandonUnchanged()
    {
        var (s, pot) = PotOnFire(10);
        E.CreateItems(s, "grain", 1, Here);
        E.CreateItems(s, "raw_meat", 1, Here);
        var t0 = s.GameMinute;
        var meat = First(s, "raw_meat", Here);
        var grain = First(s, "grain", Here);
        meat.ExpiresAt = t0 + 10;
        var eg = grain.ExpiresAt!.Value;
        Ok(E.DoInstant(s, InstantRequest.LoadPotRecipe("pot_stew", pot.InstanceId)));
        WaitMinutes(s, 20);
        var c = s.GameMinute;
        var n0 = s.Log.Count;
        Ok(E.DoInstant(s, InstantRequest.TakeOut(meat.InstanceId, Here)));
        Assert.That((meat.ItemId, meat.ExpiresAt, meat.ShelfPausedMinutes), Is.EqualTo(("raw_meat", (int?)(c + 10), (int?)null)), "取出后从冻结余额继续计时");
        Assert.That((grain.ExpiresAt, grain.ShelfPausedMinutes), Is.EqualTo(((int?)(c + eg - t0), (int?)null)), "作废后留锅谷物同样恢复");
        Assert.That(LogSince(s, n0).Single(), Does.Contain("整份作废").And.Not.Contain("已腐败"), "原作废分句照旧，无转换句");
    }

    /// <summary>
    /// Q1036／S10 Q770 补正（Q1009 取出清单按实际移动的物品身份）：直烤中的生肉原截止＝开烤后 5 分钟，受热 10 分钟后全部取出——
    /// 清单写「生肉×1」（不是腐败物），没有转换句，生肉从取出时刻＋R（5）继续计时。
    /// </summary>
    [Test]
    public void Q1036_FireTakeAll_FrozenMeat_ListedAsItself_Resumes()
    {
        var s = New();
        s.Items.RemoveAll(i => i.ItemId == "raw_meat");
        var t0 = s.GameMinute;
        var meat = Make(s, "raw_meat", 1, Here, t0 + 5);
        Ok(E.DoInstant(s, InstantRequest.PutInFireSlot(meat.InstanceId, 0, 1)));
        Lit(s);
        WaitMinutes(s, 10);
        Assert.That((E.ProcessOf(s, meat)!.HeatProtected, meat.ShelfPausedMinutes), Is.EqualTo((true, (int?)5)), "起点：受热保护、冻结 R＝5");
        var c = s.GameMinute;
        var n0 = s.Log.Count;
        Ok(E.TakeAll(s, s.Ground, GameEngine.FireTakeAllId, E.TakeAllStamp(s, s.Ground, GameEngine.FireTakeAllId), null));
        Assert.That(LogSince(s, n0), Is.EqualTo(new[] { "已取出至临时营地地面：生肉×1。" }));
        Assert.That((meat.ItemId, meat.ExpiresAt, meat.ShelfPausedMinutes), Is.EqualTo(("raw_meat", (int?)(c + 5), (int?)null)));
    }

    /// <summary>
    /// Q1037（「已开始烹饪的有效原料保鲜冻结，不为该状态新增……等待腐败停止的日志」）：临期生肉直烤（原截止＝开工后 10 分钟，直烤 30 分钟），
    /// 「等待加工」一直等到烤熟，越过旧截止不停止、不写「等待加工停止」，完成为烤肉。
    /// 对照（「尚未进入保护的实物到期……继续使用对应真实结果和原反馈」）：火堆没点燃、生肉没受热而到期，照常原位转为腐败物。
    /// </summary>
    [Test]
    public void Q1037_WaitCook_PastOldDeadline_NoSpoilStop_UnprotectedStillStops()
    {
        var s = New();
        s.Items.RemoveAll(i => i.ItemId == "raw_meat");
        var meat = Make(s, "raw_meat", 1, Here, s.GameMinute + 10);
        Ok(E.DoInstant(s, InstantRequest.PutInFireSlot(meat.InstanceId, 0, 1)));
        Lit(s);
        var n0 = s.Log.Count;
        Start(s, ActionRequest.WaitCook());
        Run(s);
        Assert.That(meat.ItemId, Is.EqualTo("cooked_meat"), "直烤 30 分钟照常烤熟（配置 roast_meat，S11 直烤表）");
        Assert.That(LogSince(s, n0), Has.None.Contains("等待加工停止").And.None.Contains("已腐败"));

        // 对照：火堆没点燃，生肉没受热（未进入保护），到期在火堆加工位照常转为腐败物
        var t = New();
        t.Items.RemoveAll(i => i.ItemId == "raw_meat");
        var m2 = Make(t, "raw_meat", 1, Here, t.GameMinute + 10);
        Ok(E.DoInstant(t, InstantRequest.PutInFireSlot(m2.InstanceId, 0, 1)));
        WaitMinutes(t, 15);
        Assert.That(m2.ItemId, Is.EqualTo(GameEngine.SpoiledMatterId), "未受保护的实物照常到期转换");
    }

    // ───────────── 存档 58 → 59 ─────────────

    private static string AsSchema(GameState s, string version)
    {
        var o = JObject.Parse(SaveSerializer.Serialize(s));
        o["SchemaVersion"] = version;
        return o.ToString();
    }

    /// <summary>
    /// 58→59（B59b，§Q769、§Q1036）：按升级分钟 U 的剩余量冻结——余料位、停止位里的原料 R＝旧E−U（截止暂空）；已晾好位成品不冻结；
    /// U 时已越过原截止的不冻结（交给普通到期转换）；受热保护中的直烤生肉 R＝旧E−U（原截止保留），受热保护中已越过原截止的旧档食材不冻结（沿原 Q16 办法）；
    /// 随身普通物品不动。读档链尾的孤立保护收束不释放仍受烹饪保护的冻结；再存再读不重复加 R。
    /// </summary>
    [Test]
    public void Migrate58To59_DryingAndCookFrozenAtUpgrade_Idempotent()
    {
        var s = Rack();
        // 火堆两位直烤：生肉、生块根，受热 10 分钟取得保护后去掉 R，造 58 旧档（58 时受热保护不记 R）
        s.Items.RemoveAll(i => i.ItemId is "raw_meat" or "raw_root");
        var t0 = s.GameMinute;
        var fireMeat = Make(s, "raw_meat", 1, s.Ground, t0 + 100);
        Ok(E.DoInstant(s, InstantRequest.PutInFireSlot(fireMeat.InstanceId, 0, 1)));
        var fireRoot = Make(s, "raw_root", 1, s.Ground, t0 + 100);
        Ok(E.DoInstant(s, InstantRequest.PutInFireSlot(fireRoot.InstanceId, 1, 1)));
        s.Locations[s.Ground].Facility.FireLit = true;
        s.Locations[s.Ground].Facility.FuelMinutes = 240;
        WaitMinutes(s, 10);
        var u = s.GameMinute;
        Assert.That(E.AllProcesses(s).Count(p => p.HeatProtected), Is.EqualTo(2), "起点：两份都受热保护");
        fireMeat.ShelfPausedMinutes = null;
        fireRoot.ShelfPausedMinutes = null;
        fireRoot.ExpiresAt = u - 1;   // 旧档：受热保护中已越过原截止

        // 晾制架：余料位、停止位、已晾好位、余料位（已到期未转换）；随身普通野果
        DrySlots(s).Add(new DryingSlot { Slot = 0, BatchId = 9100, RecipeId = "dry_berries", Status = DryingSlot.Residual, StartMinute = u - 60, RequiredMinutes = 120, InitiatorActorId = GameState.PlayerId });
        var resBerry = Make(s, "berry", 1, Slot(s, 0), u + 100);
        DrySlots(s).Add(new DryingSlot { Slot = 1, BatchId = 9101, RecipeId = "dry_meat", Status = DryingSlot.Spoiled, StopReason = DryingSlot.StopMissing, StartMinute = u - 60, RequiredMinutes = 180, EndMinute = u - 30, InitiatorActorId = GameState.PlayerId });
        var stopMeat = Make(s, "raw_meat", 1, Slot(s, 1), u + 50);
        DrySlots(s).Add(new DryingSlot { Slot = 2, BatchId = 9102, RecipeId = "dry_berries", Status = DryingSlot.Done, StartMinute = u - 200, RequiredMinutes = 120, EndMinute = u - 80, InitiatorActorId = GameState.PlayerId });
        var product = Make(s, "dried_berries", 1, Slot(s, 2), u + 1000);
        DrySlots(s).Add(new DryingSlot { Slot = 3, BatchId = 9103, RecipeId = "dry_berries", Status = DryingSlot.Residual, StartMinute = u - 60, RequiredMinutes = 120, InitiatorActorId = GameState.PlayerId });
        var dueBerry = Make(s, "berry", 1, Slot(s, 3), u);
        var carried = Make(s, "berry", 1, ItemInstance.Carried, u + 300);

        var m = SaveSerializer.Deserialize(AsSchema(s, "58"), Cfg);
        Assert.That(m.SchemaVersion, Is.EqualTo("59"));
        (int? E, int? R) Of(GameState g, ItemInstance i) { var x = g.Items.Single(y => y.InstanceId == i.InstanceId); return (x.ExpiresAt, x.ShelfPausedMinutes); }
        Assert.That(Of(m, resBerry), Is.EqualTo(((int?)null, (int?)100)), "余料位 R＝旧E−U，截止暂空");
        Assert.That(Of(m, stopMeat), Is.EqualTo(((int?)null, (int?)50)), "停止位原料同样冻结");
        Assert.That(Of(m, product), Is.EqualTo(((int?)(u + 1000), (int?)null)), "已晾好成品照常计时");
        Assert.That(Of(m, dueBerry), Is.EqualTo(((int?)u, (int?)null)), "升级时已到期的不冻结");
        Assert.That(Of(m, carried), Is.EqualTo(((int?)(u + 300), (int?)null)), "随身普通物品不动");
        Assert.That(Of(m, fireMeat), Is.EqualTo(((int?)(t0 + 100), (int?)(t0 + 100 - u))), "受热保护中的原料 R＝旧E−U，原截止保留");
        Assert.That(Of(m, fireRoot), Is.EqualTo(((int?)(u - 1), (int?)null)), "受热保护中已越过原截止的旧档食材不冻结");
        Assert.That(E.PastExpiryButProtected(m, m.Items.Single(i => i.InstanceId == fireRoot.InstanceId)), Is.True, "沿原 Q16 办法");

        var again = SaveSerializer.Deserialize(SaveSerializer.Serialize(m), Cfg);
        Assert.That(new[] { resBerry, stopMeat, fireMeat }.Select(i => Of(again, i).R), Is.EqualTo(new int?[] { 100, 50, t0 + 100 - u }), "再存再读不重复加 R");
    }
}
