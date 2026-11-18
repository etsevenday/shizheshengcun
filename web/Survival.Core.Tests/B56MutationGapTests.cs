using Survival.Core.Config;
using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// B56 变异测试补测（基线 16015261..19db03a6 的存活与无覆盖）：配置校验、鉴定净水档说明、水壶空壶、锅水多堆扣量与设施同步、
/// 同行食饮自选份数、预估缓存键、存档 57→58 在办预留与事务输出。不补的变异体逐条理由见开发记录 B56.8。
/// 规则来源：S10／S12／S14 文首 2026-11-17 B53a Q929、assets/20261117/b53a-q928-q929-water-100ml.json；期望值写字面量，出处见断言旁注释。
/// </summary>
public class B56MutationGapTests : TestBase
{
    private const string Lin = "npc_lin_zhao";
    private const string Fire = "facility:camp:fire";

    protected override bool NoEnemyApproach => true;

    private GameState Fresh()
    {
        var s = New();
        s.SpawnNextCheck = int.MaxValue;
        s.PatrolNextCheck = int.MaxValue;
        s.Items.RemoveAll(i => i.ItemId is "clean_water" or "dirty_water");
        s.Stats.Hydration = 20;
        return s;
    }

    private GameState WithLin()
    {
        var s = Fresh();
        s.Progress.Flags.Add("hub_registered");
        s.Progress.Flags.Add("church_route_record");
        s.Progress.Flags.Add("academy_safety_record");
        E.DebugRecruit(s, Lin);
        s.Actors[Lin].Stats.Hydration = 30;
        return s;
    }

    private void Ok(ActionResult r) => Assert.That(r.Ok, Is.True, r.Reason);

    private ItemInstance Canteen(GameState s, string? liquid, int ml)
    {
        var c = E.CreateItems(s, "ration_canteen", 1, ItemInstance.Carried).Single();
        c.LiquidId = liquid;
        c.LiquidMl = ml;
        return c;
    }

    // ───────────── 配置校验（ConfigLoader B56 段） ─────────────

    private static List<string> Broken(Action<GameConfig> breakIt)
    {
        var c = ConfigLoader.FromJson(File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "config.v0.6.json")), validate: false);
        breakIt(c);
        return ConfigLoader.Validate(c);
    }

    private static CanteenConfig CanteenCfg(GameConfig c) => c.Items.Single(i => i.Id == "ration_canteen").Canteen!;
    private static AppraisalOfferConfig WaterOffer(GameConfig c) => c.Appraisal!.Offers.Single(o => o.Id == "white_for_water");

    [Test]
    public void Config_Canteen_CapacityMustBeWholeTransferUnits()
    {
        // S10 文首 Q929：水壶装／倒／饮按 100 毫升整份，容量须为整数倍（现行 2000／100 合法）
        const string msg = "水壶 ration_canteen 容量须为转移单位的整数倍";
        Assert.That(Broken(_ => { }), Is.Empty, "配对：现行配置合法");
        Assert.That(Broken(c => CanteenCfg(c).CapacityMl = 2050), Does.Contain(msg), "2050 不是 100 的整数倍");
        Assert.That(Broken(c => CanteenCfg(c).CapacityMl = 0), Does.Contain(msg), "容量为 0");
        Assert.That(Broken(c => CanteenCfg(c).TransferMl = 0), Does.Contain(msg), "转移单位为 0（须先判，不能拿 0 取余）");
    }

    [Test]
    public void Config_Canteen_AcceptsMustBeLiquidWithUnitVolume()
    {
        // S10 文首 Q929：可装物品须是有每份体积的液体，且每份体积等于转移单位 100
        Assert.That(Broken(c => CanteenCfg(c).Accepts.Add("wood")), Does.Contain("水壶 ration_canteen 可装物品 wood 须是有每份体积的液体"));
        Assert.That(Broken(c => CanteenCfg(c).Accepts.Add("nope")), Does.Contain("水壶 ration_canteen 可装物品 nope 须是有每份体积的液体"));
        Assert.That(Broken(c => c.Items.Single(i => i.Id == "clean_water").UnitVolumeMl = 250),
            Does.Contain("水壶 ration_canteen 可装物品 clean_water 的每份体积须为100毫升"));
    }

    [Test]
    public void Config_GatherFirstDailyLiquidUnits_AtLeastOne()
    {
        // b56 json waterFirstDaily：水的首采折算份数（现行 5）须≥1；1 合法
        Assert.That(Broken(c => c.SkillPassiveRules.GatherFirstDailyLiquidUnits = 0), Does.Contain("每日首次采集对水的折算份数须≥1"));
        Assert.That(Broken(c => c.SkillPassiveRules.GatherFirstDailyLiquidUnits = 1), Is.Empty, "配对：1 合法");
    }

    [Test]
    public void Config_AppraisalOfferStep_RangeMustBeWholeSteps()
    {
        // W100-Q929-12：净水档 min 5、max 10、step 5；区间须为档距整数倍、max≥min、step≥1
        const string msg = "鉴定报价档 white_for_water 的份数区间须为档距 step 的整数倍";
        Assert.That(Broken(c => WaterOffer(c).Step = 0), Does.Contain(msg), "step 0");
        Assert.That(Broken(c => { WaterOffer(c).Min = 10; WaterOffer(c).Max = 5; }), Does.Contain(msg), "max<min（差 -5 能被 5 整除，只靠 max<min 拦）");
        Assert.That(Broken(c => WaterOffer(c).Max = 12), Does.Contain(msg), "12-5=7 不是 5 的整数倍");
        Assert.That(Broken(c => { WaterOffer(c).Min = 5; WaterOffer(c).Max = 5; }), Is.Empty, "配对：min=max 合法");
        Assert.That(Broken(c => { WaterOffer(c).Min = 2; WaterOffer(c).Max = 12; }), Is.Empty, "配对：12-2=10 是 5 的整数倍");
    }

    [Test]
    public void Config_BoilWaterAllowedBatch_PositiveWholeUnitsWithinPot()
    {
        // S10 Q929 补问 A：单纯烧水可处理量为每份体积（100）的正整数倍、至锅容量 1000
        const string msg = "设施进程 boil_water 可处理水量须为每份体积的正整数倍且不超过容量";
        List<int> Batch(GameConfig c) => c.Processes.Single(p => p.Id == "boil_water").AllowedBatchMl;
        Assert.That(Broken(c => Batch(c).Insert(0, 0)), Does.Contain(msg), "0 毫升");
        Assert.That(Broken(c => Batch(c).Insert(0, 50)), Does.Contain(msg), "50 不是整份");
        Assert.That(Broken(c => Batch(c).Add(1100)), Does.Contain(msg), "超过锅容量 1000");
        Assert.That(Broken(c => { Batch(c).Clear(); Batch(c).AddRange(new[] { 100, 1050 }); }), Does.Contain(msg), "有一项不合法即报");
        Assert.That(Broken(c => { Batch(c).Clear(); Batch(c).AddRange(new[] { 100, 1000 }); }), Is.Empty, "配对：100、1000 合法");
    }

    // ───────────── 鉴定净水档说明（开发记录 B56.5 暂用句） ─────────────

    [Test]
    public void Appraisal_RangeText_StepOfferListsQuantities_PlainOfferKeepsInterval()
    {
        var s = New();
        E.DebugMapTeleport(s, "h05_appraisal");
        ReadDialogues(s);
        var snap = s.City.Quotes.Single();
        Assert.That(snap.Day, Is.EqualTo(1));
        snap.Offers["white_for_water"] = 10;
        snap.Offers["white_for_cloth"] = 2;
        var offers = E.AppraisalPreview(s).Offers;
        Assert.That(offers, Has.Count.EqualTo(3));
        // 开发记录 B56.5：有档距的净水档「本档数量5或10份、每个数量等概率；第{日}日已锁定为{n}份」（W100-Q929-12 只出 5 或 10 份）
        Assert.That(offers.Single(o => o.OfferId == "white_for_water").RangeText, Is.EqualTo("本档数量5或10份、每个数量等概率；第1日已锁定为10份"));
        // 档距 1 的档沿原句「本档区间{min}—{max}份…」（配置 white_for_cloth 1—3）
        Assert.That(offers.Single(o => o.OfferId == "white_for_cloth").RangeText, Is.EqualTo("本档区间1—3份、每个数量等概率；第1日已锁定为2份"));
    }

    // ───────────── 文案函数（合同 copy） ─────────────

    [Test]
    public void DrinkCopy_AmountAndSummary_Verbatim()
    {
        // b53a json copy.amount「{毫升}毫升」；S12 文首 B53a 本批逐字稿 食饮单次水量摘要「{水种}{所选毫升}毫升」
        Assert.That(DrinkCopy.AmountText(300), Is.EqualTo("300毫升"));
        Assert.That(DrinkCopy.SummaryText("净水", 300), Is.EqualTo("净水300毫升"));
    }

    [Test]
    public void UseButtonText_WaterEntry_AndFoodRiskSuffix()
    {
        // Q975 A 暂用：水卡入口「饮用」，脏水「饮用（风险）」
        Assert.That(GameEngine.UseButtonText(Cfg.Items.Single(i => i.Id == "clean_water")), Is.EqualTo("饮用"));
        Assert.That(GameEngine.UseButtonText(Cfg.Items.Single(i => i.Id == "dirty_water")), Is.EqualTo("饮用（风险）"));
        // v0.7 第 7 节：非水自用带既有风险写「食用1份（风险）」；现行配置里带腹泻的只有脏水，食物分支用构造的配置验
        Assert.That(GameEngine.UseButtonText(new ItemConfig { Kind = "food", Use = new ItemUseConfig { Satiety = 5, CausesDiarrhea = true } }), Is.EqualTo("食用1份（风险）"));
        Assert.That(GameEngine.UseButtonText(Cfg.Items.Single(i => i.Id == "berry")), Is.EqualTo("食用1份"), "配对：无风险不带后缀");
    }

    // ───────────── 水壶空壶（S10 文首 Q929；B16 分量水壶「水壶是空的」） ─────────────

    [Test]
    public void Canteen_Empty_NotDrinkSource_EmptyText_DiscardNotQuantityBlocked()
    {
        var s = Fresh();
        var c = Canteen(s, null, 0);
        Assert.That(E.IsDrinkSource(c), Is.False);
        Assert.That(E.DrinkLiquidOf(c), Is.Null);
        Assert.That(E.CanStart(s, ActionRequest.UseInstance(c.InstanceId, 1)), Is.EqualTo("水壶是空的"));
        Assert.That(E.DrinkUnitsBlocked(s, c, 1), Is.EqualTo("该物品不能直接使用"), "不是饮水来源");
        // 空壶丢弃：没有水可丢，不算「不足一份」，数量路径不另报「水分不足。」（配对 B56WaterTests 250→50 毫升尾水报「水分不足。」）
        var v = E.DiscardViewOf(s, DiscardTarget.CanteenWater(c.InstanceId));
        Assert.That((v.Available, v.MaxUnits, v.QuantityBlocked, v.Blocked), Is.EqualTo((0, 0, (string?)null, DiscardCopy.NoWater)));
    }

    [Test]
    public void Canteen_StaleLiquidIdAtZeroMl_TreatedAsEmpty_PairedWithWater()
    {
        // 防御：壶上残留水种但 0 毫升，仍按空壶（来源水种只在毫升>0 时成立）
        var s = WithLin();
        var c = Canteen(s, "clean_water", 0);
        Assert.That(E.DrinkLiquidOf(c), Is.Null);
        Assert.That(E.IsDrinkSource(c), Is.False);
        Assert.That(E.DrinkUnitsBlocked(s, c, 1), Is.EqualTo("该物品不能直接使用"));
        Assert.That(E.CanStart(s, ActionRequest.UseInstance(c.InstanceId, 1)), Is.EqualTo("水壶是空的"));
        var feed = new FeedRequest { TargetActorId = Lin, SourceActorId = GameState.PlayerId, InstanceId = c.InstanceId, WaterUnits = 1 };
        Assert.That(E.FeedBlocked(s, feed), Is.EqualTo("水壶是空的"));
        // 配对：壶里 250 毫升 → 是来源、水种净水，自饮与喂水 2 份都可以
        c.LiquidMl = 250;
        Assert.That(E.DrinkLiquidOf(c), Is.EqualTo("clean_water"));
        Assert.That(E.IsDrinkSource(c), Is.True);
        Assert.That(E.CanStart(s, ActionRequest.UseInstance(c.InstanceId, 2)), Is.Null);
        feed.WaterUnits = 2;
        Assert.That(E.FeedBlocked(s, feed), Is.Null);
    }

    // ───────────── 锅水扣量（Drink.cs ConsumeDrink） ─────────────

    [Test]
    public void PotWater_SelectedHeapShort_TakesRestFromSameLiquidHeap()
    {
        // ConsumeDrink：先扣所选那一堆，不够再扣同锅同种水；只扣 N×100 毫升一次（W100-Q929-02）
        var s = Fresh();
        var pot = E.CreateItems(s, "pot", 1, ItemInstance.Carried).Single();
        var a = E.CreateItems(s, "clean_water", 3, ItemInstance.ContainerOwner(pot.InstanceId)).Single();
        // 第二堆：装锅会合并为一堆，这里直接改归属造出同锅两堆（旧档拆堆的情形）
        var b = E.CreateItems(s, "clean_water", 4, ItemInstance.Carried).Single();
        b.Owner = ItemInstance.ContainerOwner(pot.InstanceId);
        Assert.That(E.PotLiquids(s, pot.InstanceId), Has.Count.EqualTo(2));
        Assert.That(E.PotUnits(s, pot.InstanceId), Is.EqualTo(7));
        Start(s, ActionRequest.UseInstance(a.InstanceId, 5));
        Assert.That(E.FindItem(s, a.InstanceId), Is.Null, "所选 3 份扣完移除");
        Assert.That(E.FindItem(s, b.InstanceId)!.Count, Is.EqualTo(2), "另一堆扣 2 份");
        Assert.That(E.PotUnits(s, pot.InstanceId), Is.EqualTo(2));
        Assert.That(s.Stats.Hydration, Is.EqualTo(50.0).Within(1e-9), "5 份 W+30");
    }

    [Test]
    public void PotWater_DrinkAllDirtyFromBoilingPot_BoilRecordDropsAtOnce()
    {
        // 设施与烹饪案第 6 节：进程记录按当前内容增删（删除即清零受热）；饮水扣锅水后立即同步，不等下一世界段
        var s = Fresh();
        var f = s.Locations["camp"].Facility;
        f.FireLit = true;
        f.FuelMinutes = 600;
        var pot = s.Items.First(i => i.ItemId == "pot");
        Ok(E.DoInstant(s, InstantRequest.PutInFire(pot.InstanceId)));
        Assert.That(pot.Owner, Is.EqualTo(Fire));
        var src = E.CreateItems(s, "dirty_water", 5, "camp").Single();
        Ok(E.DoInstant(s, InstantRequest.FillPot(src.InstanceId, pot.InstanceId, 5)));
        Assert.That(f.Processes.Count(p => p.RecipeId == "boil_water" && p.TargetInstanceId == pot.InstanceId), Is.EqualTo(1), "脏水入锅即有烧水记录");
        var w = E.PotLiquids(s, pot.InstanceId).Single();
        // 配对：只喝 3 份，锅里还剩脏水 2 份，烧水记录还在
        Start(s, ActionRequest.UseInstance(w.InstanceId, 3));
        Assert.That(E.PotUnits(s, pot.InstanceId), Is.EqualTo(2));
        Assert.That(f.Processes.Count(p => p.RecipeId == "boil_water" && p.TargetInstanceId == pot.InstanceId), Is.EqualTo(1));
        Run(s);
        Start(s, ActionRequest.UseInstance(E.PotLiquids(s, pot.InstanceId).Single().InstanceId, 2));
        Assert.That(E.PotUnits(s, pot.InstanceId), Is.EqualTo(0));
        Assert.That(f.Processes.Count(p => p.TargetInstanceId == pot.InstanceId), Is.EqualTo(0), "锅水喝空，烧水记录当场删除");
    }

    // ───────────── 同行食饮自选份数（W100-Q929-05） ─────────────

    [Test]
    public void FeedPanel_WaterUnits_OverMaxBlocked_WithinMaxPreviewMatchesResult()
    {
        var s = WithLin();
        var w = E.CreateItems(s, "clean_water", 4, ItemInstance.Carried).Single();
        var d = FeedDraft.From(E.FeedEntry(s, w.InstanceId)!);
        Assert.That(d.IsWater, Is.True);
        d.TargetActorId = Lin;
        d.SourceKey = E.FeedPanel(s, d).Rows.Single(r => r.InstanceId == w.InstanceId).Key;
        // b53a json copy.shortage「水分不足。」：所选 5 份超过可饮 4 份，确认禁用并写在「不可用：{原因}」行
        d.WaterUnits = 5;
        var over = E.FeedPanel(s, d);
        Assert.That((over.ConfirmWhy, over.BlockedLine, over.Request), Is.EqualTo(("水分不足。", "不可用：水分不足。", (FeedRequest?)null)));
        // 配对：3 份可确认；确认句按所选毫升（S12 文首 B53a 逐字稿「给{目标}饮用：{水种}{所选毫升}毫升 · 5分钟」）
        d.WaterUnits = 3;
        var ok = E.FeedPanel(s, d);
        Assert.That(ok.ConfirmWhy, Is.Null);
        Assert.That(ok.BlockedLine, Is.Null);
        Assert.That(ok.Request!.WaterUnits, Is.EqualTo(3));
        Assert.That(ok.ConfirmText, Is.EqualTo("给林昭饮用：净水300毫升 · 5分钟"));
        // 预估结果句与实际日志同一句（S12 文首 B53a 结果句「{目标}饮用了{水种}{所选毫升}毫升…」）
        var p = E.Preview(s, ActionRequest.FeedCompanion(ok.Request));
        Assert.That(p.Ok, Is.True, p.Reason);
        Assert.That(p.Feed!.ResultText, Does.StartWith("林昭饮用了净水300毫升（取自你的随身物品）"));
        Ok(E.Feed(s, ok.Request));
        Assert.That(s.Log.Count(l => l.Text == p.Feed.ResultText), Is.EqualTo(1));
    }

    // ───────────── 预估缓存键（网页 GameSession.Preview 按 PreviewKey 缓存） ─────────────

    [Test]
    public void PreviewKey_SeparatesEveryPreviewChangingField()
    {
        // Requests.cs PreviewKey 注释：凡是会改变预估结果的请求字段都要在内（B13b 徒手；B56 饮水份数、喂水份数）
        Assert.That(ActionRequest.UseInstance(7, 3).PreviewKey, Is.EqualTo(ActionRequest.UseInstance(7, 3).PreviewKey), "配对：同请求同键");
        Assert.That(ActionRequest.UseInstance(7, 1).PreviewKey, Is.Not.EqualTo(ActionRequest.UseInstance(7, 3).PreviewKey));
        Assert.That(ActionRequest.Gather("wood", bareHands: true).PreviewKey, Is.Not.EqualTo(ActionRequest.Gather("wood").PreviewKey));
        Assert.That(ActionRequest.Travel("camp", true).PreviewKey, Is.Not.EqualTo(ActionRequest.Travel("camp").PreviewKey));
        var p1 = ActionRequest.Gather("wood");
        p1.PreferredInstanceIds.AddRange(new[] { 1, 23 });
        var p2 = ActionRequest.Gather("wood");
        p2.PreferredInstanceIds.AddRange(new[] { 12, 3 });
        Assert.That(p1.PreviewKey, Is.Not.EqualTo(p2.PreviewKey), "首选实例 1,23 与 12,3 不能拼成同一串");
        FeedRequest Feed(int n) => new FeedRequest { TargetActorId = Lin, SourceActorId = GameState.PlayerId, InstanceId = 7, WaterUnits = n };
        Assert.That(ActionRequest.FeedCompanion(Feed(1)).PreviewKey, Is.Not.EqualTo(ActionRequest.FeedCompanion(Feed(3)).PreviewKey));
        Assert.That(ActionRequest.FeedCompanion(Feed(3)).PreviewKey, Is.EqualTo(ActionRequest.FeedCompanion(Feed(3)).PreviewKey));
    }

    // ───────────── 存档 57 → 58（SaveMigrator.From57To58） ─────────────

    [Test]
    public void Migrate57To58_InFlightGatherAndLoot_PendingWaterOutput_TimesFive()
    {
        // From57To58 ②③：在办采集产水点的预留份数、已抽定的水掉落、在办换物的水输出份数×5（真实毫升不变）
        var s = Fresh();
        s.Main = new ActionState { Type = ActionType.Gather, TargetId = "w16_service_cistern", ReservedResourceUnits = 1 };
        s.ExploreCheckPending = new ActionState { Type = ActionType.Explore, TargetId = "w16_service_cistern", ReservedResourceUnits = 1, LootItemId = "dirty_water", LootCount = 1 };
        s.City.Pending = new CityServiceTxn { OutputItemId = "clean_water", ExactOutput = 2 };
        s.Resources["water"].Reserved = 2;
        s.SchemaVersion = "57";
        var m = SaveSerializer.Deserialize(SaveSerializer.Serialize(s), Cfg);
        Assert.That(m.SchemaVersion, Is.EqualTo("58"));
        Assert.That(m.Main!.ReservedResourceUnits, Is.EqualTo(5), "采集储水池预留 1×500 → 5×100");
        Assert.That(m.ExploreCheckPending!.LootCount, Is.EqualTo(5), "水掉落 1 → 5");
        Assert.That(m.ExploreCheckPending.ReservedResourceUnits, Is.EqualTo(1), "非采集行动的预留不换算");
        Assert.That(m.City.Pending!.ExactOutput, Is.EqualTo(10), "净水输出 2 → 10");
        Assert.That(m.Resources["water"].Reserved, Is.EqualTo(2), "不限量溪水不按件计库存，不换算");
    }

    [Test]
    public void Migrate57To58_NonWaterLootAndOutput_Unchanged()
    {
        // 配对：掉落、换物输出不是水时不动
        var s = Fresh();
        s.ExploreCheckPending = new ActionState { Type = ActionType.Explore, TargetId = "camp", LootItemId = "berry", LootCount = 1 };
        s.City.Pending = new CityServiceTxn { OutputItemId = "cloth", ExactOutput = 2 };
        s.SchemaVersion = "57";
        var m = SaveSerializer.Deserialize(SaveSerializer.Serialize(s), Cfg);
        Assert.That(m.ExploreCheckPending!.LootCount, Is.EqualTo(1));
        Assert.That(m.City.Pending!.ExactOutput, Is.EqualTo(2));
    }

    [Test]
    public void Migrate57To58_ReservationAlreadyAtNewCost_Kept()
    {
        // From57To58 ④：只有低于新耗水 1000 的旧预留清 0；已等于 1000 的（如 pot_stew 本就 1000）保留
        var s = Fresh();
        var f = s.Locations["camp"].Facility;
        f.FireLit = true;
        f.FuelMinutes = 600;
        var pot = s.Items.First(i => i.ItemId == "pot");
        Ok(E.DoInstant(s, InstantRequest.PutInFire(pot.InstanceId)));
        var src = E.CreateItems(s, "clean_water", 10, "camp").Single();
        Ok(E.DoInstant(s, InstantRequest.FillPot(src.InstanceId, pot.InstanceId, 10)));
        var root = E.CreateItems(s, "raw_root", 1, "camp").Single();
        Ok(E.DoInstant(s, InstantRequest.PutFoodInPot(root.InstanceId, pot.InstanceId, 1)));
        var food = E.PotFood(s, pot.InstanceId).Single();
        Assert.That(E.ProcessOf(s, food)!.ReservedWaterMl, Is.EqualTo(1000));
        foreach (var w in E.PotLiquids(s, pot.InstanceId)) w.Count = 2;   // 57 旧单位：1000 毫升＝2 份
        s.SchemaVersion = "57";
        var m = SaveSerializer.Deserialize(SaveSerializer.Serialize(s), Cfg);
        Assert.That(E.PotUnits(m, pot.InstanceId), Is.EqualTo(10));
        Assert.That(E.ProcessOf(m, m.Items.Single(i => i.InstanceId == food.InstanceId))!.ReservedWaterMl, Is.EqualTo(1000), "读档当场仍是 1000，不清零");
    }

    // ───────────── 第二轮补测（基线 16015261..cae91955） ─────────────

    [Test]
    public void DrinkLiquidOf_NonLiquidCard_Null_PairedWithWaterCard()
    {
        // Drink.cs DrinkLiquidOf：只有液体（有每份体积）才有水种；食物卡没有
        var s = Fresh();
        var berry = E.CreateItems(s, "berry", 1, ItemInstance.Carried).Single();
        var water = E.CreateItems(s, "clean_water", 2, ItemInstance.Carried).Single();
        Assert.That(E.DrinkLiquidOf(berry), Is.Null);
        Assert.That(E.DrinkLiquidOf(water), Is.EqualTo("clean_water"), "配对：水卡的水种是它自己");
    }

    [Test]
    public void Migrate57To58_ProcessRecipeNoLongerInConfig_Skipped()
    {
        // From57To58 ④：进程配方已不在配置里的旧记录跳过（cfg == null 不处理、不报错），预留原样留给读档后的设施同步
        var s = Fresh();
        s.Locations["camp"].Facility.Processes.Add(new FacilityProcess { ProcessId = "p-gone", RecipeId = "gone_recipe", TargetInstanceId = 999999, ReservedWaterMl = 500 });
        s.SchemaVersion = "57";
        var m = SaveSerializer.Deserialize(SaveSerializer.Serialize(s), Cfg);
        Assert.That(m.SchemaVersion, Is.EqualTo("58"));
        var gone = m.Locations["camp"].Facility.Processes.Where(p => p.RecipeId == "gone_recipe").ToList();
        Assert.That(gone, Has.Count.EqualTo(1));
        Assert.That(gone[0].ReservedWaterMl, Is.EqualTo(500));
    }

    // ───────────── 试玩修复 BUG-1：同行饮水超量保留输入 ─────────────

    [Test]
    public void FeedPanel_Canteen_OverMaxKeepsChoice_ShortageBlocks_PairedWithMax()
    {
        // 试玩场景：给林昭饮用、来源林昭的壶 700 毫升（可用 7 份），输入 8。
        // 开发假设 5、S12 文首 B53a「水量不足…的数量禁用原因：水分不足。」、W100-Q929-06：超量保留所选，禁确认，整次不扣
        var s = WithLin();
        var c = E.CreateItems(s, "ration_canteen", 1, ItemInstance.CarriedOwner(Lin)).Single();
        c.LiquidId = "clean_water";
        c.LiquidMl = 700;
        var d = FeedDraft.From(E.FeedEntry(s, c.InstanceId)!);
        Assert.That(d.IsWater, Is.True);
        d.TargetActorId = Lin;
        d.SourceKey = E.FeedPanel(s, d).Rows.Single(r => r.InstanceId == c.InstanceId).Key;
        d.WaterUnits = 8;
        var before = SaveSerializer.Serialize(s);
        var over = E.FeedPanel(s, d);
        Assert.That(d.WaterUnits, Is.EqualTo(8), "内核不改所选份数");
        Assert.That(over.Selected, Is.Not.Null, "来源仍选中，份数输入行仍显示");
        Assert.That(over.Selected!.MaxUnits, Is.EqualTo(7));
        Assert.That((over.ConfirmWhy, over.BlockedLine, over.Request), Is.EqualTo(("水分不足。", "不可用：水分不足。", (FeedRequest?)null)));
        Assert.That(SaveSerializer.Serialize(s), Is.EqualTo(before), "只看不扣");
        // 配对：恰好 7 份可以确认，确认句 700 毫升（S12 逐字稿「给{目标}饮用：{水壶}内{水种}{所选毫升}毫升 · 5分钟」）
        d.WaterUnits = 7;
        var ok = E.FeedPanel(s, d);
        Assert.That(ok.ConfirmWhy, Is.Null);
        Assert.That(ok.Request!.WaterUnits, Is.EqualTo(7));
        Assert.That(ok.ConfirmText, Is.EqualTo("给林昭饮用：分量水壶内净水700毫升 · 5分钟"));
        Ok(E.Feed(s, ok.Request));
        Assert.That((c.LiquidId, c.LiquidMl), Is.EqualTo(((string?)null, 0)), "喝空回空壶");
        Assert.That(s.Actors[Lin].Stats.Hydration, Is.EqualTo(72.0).Within(1e-9), "30 + 6×7");
    }
}
