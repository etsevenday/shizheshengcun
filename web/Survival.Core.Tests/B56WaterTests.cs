using Newtonsoft.Json.Linq;
using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// B56（R294 + R306）：水量 100 毫升单位、自选饮水与烧水水煮新量。规则来源 S10／S11／S12／S14 文首 2026-11-17 B53a Q929、
/// assets/20261117/b53a-q928-q929-water-100ml.json（W100-Q928-01、W100-Q929-01—14）、assets/20261118/b56-q973-q974-water-yield-budget.json
/// （W56-Q973-01—02、W56-Q974-01—04）。期望值都写字面量，来源见各断言旁注释。
/// </summary>
public class B56WaterTests : TestBase
{
    private const string Lin = "npc_lin_zhao";
    private const string Fire = "facility:camp:fire";

    protected override bool NoEnemyApproach => true;

    /// <summary>营地新局，清掉开局水，主角水分 20（留足 +60 的余量不溢出）。</summary>
    private GameState Fresh()
    {
        var s = New();
        s.SpawnNextCheck = int.MaxValue;
        s.PatrolNextCheck = int.MaxValue;
        s.Items.RemoveAll(i => i.ItemId is "clean_water" or "dirty_water");
        s.Stats.Hydration = 20;
        return s;
    }

    private void Ok(ActionResult r) => Assert.That(r.Ok, Is.True, r.Reason);

    private static string Doc(params string[] tail)
    {
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "doc", "策划案"))) dir = dir.Parent;
        Assert.That(dir, Is.Not.Null, "找不到仓库根目录");
        return Path.Combine(new[] { dir!.FullName, "doc", "策划案", "系统策划总册 v1.0", "assets" }.Concat(tail).ToArray());
    }

    private static JObject Contract() => JObject.Parse(File.ReadAllText(Doc("20261117", "b53a-q928-q929-water-100ml.json")));

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

    private ItemInstance PotOnLitFire(GameState s)
    {
        var f = s.Locations["camp"].Facility;
        f.FireLit = true;
        f.FuelMinutes = 600;
        var pot = s.Items.First(i => i.ItemId == "pot");
        Ok(E.DoInstant(s, InstantRequest.PutInFire(pot.InstanceId)));
        Assert.That(pot.Owner, Is.EqualTo(Fire));
        return pot;
    }

    private void FillPot(GameState s, ItemInstance pot, string water, int n)
    {
        var src = E.CreateItems(s, water, n, "camp").Single();
        Ok(E.DoInstant(s, InstantRequest.FillPot(src.InstanceId, pot.InstanceId, n)));
    }

    // ───────────── 自选饮水（W100-Q929-04、06） ─────────────

    [TestCase(1, 100, 6)]
    [TestCase(3, 300, 18)]
    [TestCase(10, 1000, 60)]
    public void W100_Q929_04_DrinkChosenUnits_OneFiveMinuteAction_PreviewReadOnly(int units, int ml, int hydration)
    {
        // b53a json drink.examples：100／300／1000 毫升 → 基础 W+6／18／60，一次 5 分钟
        var s = Fresh();
        var w = E.CreateItems(s, "clean_water", 10, ItemInstance.Carried).Single();
        var req = ActionRequest.UseInstance(w.InstanceId, units);
        var before = SaveSerializer.Serialize(s);
        var p = E.Preview(s, req);
        Assert.That(p.Ok, Is.True, p.Reason);
        Assert.That((p.Drink!.Units, p.Drink.Ml, p.Drink.Hydration, p.Drink.MaxUnits), Is.EqualTo((units, ml, (double)hydration, 10)));
        // S14 文首 B53a 本批逐字稿：右栏三行「饮水：{水种}{毫升}毫升」「补水：{补水值}」「耗时：5分钟」
        Assert.That(p.Drink.Lines, Is.EqualTo(new[] { $"饮水：净水{ml}毫升", $"补水：{hydration}", "耗时：5分钟" }));
        Assert.That(p.ConfirmText, Is.EqualTo($"饮用{ml}毫升（5分钟）"));
        Assert.That(SaveSerializer.Serialize(s), Is.EqualTo(before), "未确认无变化：只预览不扣水、不推进时间");

        var t0 = s.GameMinute;
        Start(s, req);
        Assert.That(s.Stats.Hydration, Is.EqualTo(20.0 + hydration).Within(1e-9), "确认时一次给足 6N");
        Assert.That(Count(s, "clean_water"), Is.EqualTo(10 - units), "扣一次实际水量");
        Run(s);
        Assert.That(s.GameMinute - t0, Is.EqualTo(5), "无论所选量都只推进一次 5 分钟");
        Assert.That(s.Log.Count(l => l.Text.StartsWith($"饮用净水{ml}毫升")), Is.EqualTo(1), "日志一条（02 Q975 A 暂用「饮用{水种}{毫升}毫升」）");
    }

    [Test]
    public void W100_Q929_06_ChosenAboveMax_ShortageRejected_NothingChanges_ValidOneStillWorks()
    {
        var s = Fresh();
        var w = E.CreateItems(s, "clean_water", 3, ItemInstance.Carried).Single();
        var before = SaveSerializer.Serialize(s);
        // b53a json copy.shortage「水分不足。」：所选 4 份超过可饮 3 份，整次不扣、不推进
        Assert.That(E.CanStart(s, ActionRequest.UseInstance(w.InstanceId, 4)), Is.EqualTo("水分不足。"));
        var res = E.Start(s, ActionRequest.UseInstance(w.InstanceId, 4));
        Assert.That((res.Ok, res.Reason), Is.EqualTo((false, "水分不足。")));
        Assert.That(E.CanStart(s, ActionRequest.UseInstance(w.InstanceId, 0)), Is.EqualTo("请输入 1 到 3 之间的整数。"));
        Assert.That(SaveSerializer.Serialize(s), Is.EqualTo(before), "失败不扣水、不改水分、不耗时");
        // 配对：恰好等于上限 3 份可以喝
        Assert.That(E.CanStart(s, ActionRequest.UseInstance(w.InstanceId, 3)), Is.Null);
        Start(s, ActionRequest.UseInstance(w.InstanceId, 3));
        Assert.That(Count(s, "clean_water"), Is.EqualTo(0));
        Assert.That(s.Stats.Hydration, Is.EqualTo(38.0).Within(1e-9));
    }

    [Test]
    public void W100_Q929_05_DirtyDiarrheaOnce_NotMultipliedByUnits()
    {
        // b53a json drink.dirtyPositiveVolumeKeepsDiarrheaHours 12：脏水正量原腹泻风险不乘 N
        var s = Fresh();
        var w = E.CreateItems(s, "dirty_water", 5, ItemInstance.Carried).Single();
        Start(s, ActionRequest.UseInstance(w.InstanceId, 5));
        Assert.That(s.Stats.Hydration, Is.EqualTo(50.0).Within(1e-9), "5 份脏水 W+30");
        Assert.That(s.DiarrheaUntil, Is.EqualTo(s.GameMinute + 720), "只腹泻一次 12 小时");
        Assert.That(s.Log.Count(l => l.Text.Contains("腹泻")), Is.EqualTo(1));
    }

    // ───────────── 水壶与锅水（W100-Q929-02、03） ─────────────

    [Test]
    public void W100_Q929_03_Canteen250_MaxTwo_Leaves50_ThenShortage()
    {
        var s = Fresh();
        var c = E.CreateItems(s, "ration_canteen", 1, ItemInstance.Carried).Single();
        c.LiquidId = "clean_water";
        c.LiquidMl = 250;
        // W100-Q929-03：实际 250 毫升数量最大 2，移 200 留 50
        Assert.That(E.MaxDrinkUnits(s, c), Is.EqualTo(2));
        Assert.That(E.MaxPourCanteen(s, c, ItemInstance.Carried, out var pw), Is.EqualTo(2));
        Assert.That(pw, Is.Null);
        Start(s, ActionRequest.UseInstance(c.InstanceId, 2));
        Assert.That((c.LiquidId, c.LiquidMl), Is.EqualTo(("clean_water", 50)));
        Assert.That(s.Stats.Hydration, Is.EqualTo(32.0).Within(1e-9));
        Run(s);
        // 不足 100 毫升：饮用与倒出都「水分不足。」，全部丢弃仍可删真实 50
        Assert.That(E.CanStart(s, ActionRequest.UseInstance(c.InstanceId, 1)), Is.EqualTo("水分不足。"));
        Assert.That(E.MaxPourCanteen(s, c, ItemInstance.Carried, out var why), Is.EqualTo(0));
        Assert.That(why, Is.EqualTo("水分不足。"));
        var t = DiscardTarget.CanteenWater(c.InstanceId);
        var v = E.DiscardViewOf(s, t);
        Assert.That((v.Available, v.MaxUnits, v.UnitMl, v.QuantityBlocked), Is.EqualTo((50, 0, 100, "水分不足。")));
        Assert.That(E.DiscardPreview(s, t, 1, false, out var qwhy), Is.Null);
        Assert.That(qwhy, Is.EqualTo("水分不足。"), "数量路径禁用原因");
        var all = E.DiscardPreview(s, t, 0, true, out var awhy);
        Assert.That(awhy, Is.Null);
        Assert.That(all!.WaterMl, Is.EqualTo(50), "全部丢弃删真实尾水 50");
    }

    [Test]
    public void W100_Q929_03_CanteenOneUnitOrMore_QuantityNotBlocked()
    {
        // 配对：壶里有 150 毫升时数量路径可用，上限 1
        var s = Fresh();
        var c = E.CreateItems(s, "ration_canteen", 1, ItemInstance.Carried).Single();
        c.LiquidId = "dirty_water";
        c.LiquidMl = 150;
        var v = E.DiscardViewOf(s, DiscardTarget.CanteenWater(c.InstanceId));
        Assert.That((v.Available, v.MaxUnits, v.QuantityBlocked), Is.EqualTo((150, 1, (string?)null)));
        var plan = E.DiscardPreview(s, DiscardTarget.CanteenWater(c.InstanceId), 1, false, out var why);
        Assert.That(why, Is.Null);
        Assert.That(plan!.WaterMl, Is.EqualTo(100));
    }

    [Test]
    public void W100_Q929_02_PotTenUnits_DrinkFromCarriedPot_Exact()
    {
        // b53a json containers.pot：锅 1000 毫升＝10 份；从随身锅喝 7 份剩 3 份
        var s = Fresh();
        var pot = E.CreateItems(s, "pot", 1, ItemInstance.Carried).Single();
        var src = E.CreateItems(s, "clean_water", 10, ItemInstance.Carried).Single();
        Ok(E.DoInstant(s, InstantRequest.FillPot(src.InstanceId, pot.InstanceId, 10)));
        Assert.That(E.PotUnits(s, pot.InstanceId), Is.EqualTo(10));
        Assert.That(E.PotLabel(s, pot), Is.EqualTo("净水100%·1000/1000毫升"));
        var water = E.PotLiquids(s, pot.InstanceId).Single();
        Start(s, ActionRequest.UseInstance(water.InstanceId, 7));
        Assert.That(E.PotUnits(s, pot.InstanceId), Is.EqualTo(3));
        Assert.That(s.Stats.Hydration, Is.EqualTo(62.0).Within(1e-9));
    }

    // ───────────── 同行食饮（W100-Q929-05） ─────────────

    [Test]
    public void W100_Q929_05_FeedCompanion_SameChosenUnits_ShortageRejected()
    {
        var s = Fresh();
        s.Progress.Flags.Add("hub_registered");
        s.Progress.Flags.Add("church_route_record");
        s.Progress.Flags.Add("academy_safety_record");
        E.DebugRecruit(s, Lin);
        s.Actors[Lin].Stats.Hydration = 30;
        var w = E.CreateItems(s, "clean_water", 4, ItemInstance.Carried).Single();
        var over = new FeedRequest { TargetActorId = Lin, SourceActorId = GameState.PlayerId, InstanceId = w.InstanceId, WaterUnits = 5 };
        var before = SaveSerializer.Serialize(s);
        Assert.That(E.FeedBlocked(s, over), Is.EqualTo("水分不足。"));
        Assert.That(SaveSerializer.Serialize(s), Is.EqualTo(before));
        var ok = new FeedRequest { TargetActorId = Lin, SourceActorId = GameState.PlayerId, InstanceId = w.InstanceId, WaterUnits = 3 };
        Assert.That(E.FeedBlocked(s, ok), Is.Null);
        var t0 = s.GameMinute;
        Ok(E.Feed(s, ok));
        Assert.That(s.Actors[Lin].Stats.Hydration, Is.EqualTo(48.0).Within(1e-9), "目标 W+18");
        Assert.That(Count(s, "clean_water", ItemInstance.Carried), Is.EqualTo(1));
        Assert.That(s.Log.Count(l => l.Text.StartsWith("林昭饮用了净水300毫升（取自你的随身物品）")), Is.EqualTo(1),
            "S12 文首 B53a 本批逐字稿结果句「{目标}饮用了{水种}{所选毫升}毫升…」");
        Run(s);
        Assert.That(s.GameMinute - t0, Is.EqualTo(5));
    }

    // ───────────── 烧水、水煮与净化经验（W100-Q929-07、08、10） ─────────────

    [Test]
    public void W100_Q929_07_Boil300mlDirty_15Minutes_300mlClean()
    {
        var s = Fresh();
        var pot = PotOnLitFire(s);
        FillPot(s, pot, "dirty_water", 3);
        WaitMinutes(s, 10);
        Assert.That(E.PotLiquid(s, pot.InstanceId), Is.EqualTo("dirty_water"), "10 分钟未满");
        WaitMinutes(s, 5);
        // b53a json waterBoiling.exampleInputMl 300 → exampleOutputMl 300、heatMinutes 15
        Assert.That(E.PotLiquid(s, pot.InstanceId), Is.EqualTo("clean_water"));
        Assert.That(E.PotUnits(s, pot.InstanceId), Is.EqualTo(3));
        Assert.That(E.PotLabel(s, pot), Is.EqualTo("净水30%·300/1000毫升"));
    }

    [Test]
    public void W100_Q929_10_FiveBoilsOf100ml_OneXpAtFifth()
    {
        // W100-Q929-10：连续 5 次各净化 100 毫升，累计到 500 才给 1 经验
        var s = Fresh();
        var pot = PotOnLitFire(s);
        var xp0 = s.Skills["cooking"].Xp;
        var gains = new List<long>();
        for (var k = 0; k < 5; k++)
        {
            FillPot(s, pot, "dirty_water", 1);
            WaitMinutes(s, 15);
            Assert.That(E.PotLiquid(s, pot.InstanceId), Is.EqualTo("clean_water"), $"第{k + 1}次净化完成");
            foreach (var cw in E.PotLiquids(s, pot.InstanceId).ToList()) s.Items.Remove(cw);   // 倒掉净水，下一批重新装脏水
            gains.Add(s.Skills["cooking"].Xp - xp0);
        }
        Assert.That(gains, Has.Count.EqualTo(5));
        Assert.That(gains, Is.EqualTo(new long[] { 0, 0, 0, 0, 1 }));
    }

    [Test]
    public void W100_Q929_08_BoilRoot_Reserves1000_HalfPotWaits()
    {
        // b53a json foodProcesses boil_root waterCostMl 1000：锅里 500 毫升不够，等待；补到 1000 才预留
        var s = Fresh();
        var pot = PotOnLitFire(s);
        FillPot(s, pot, "clean_water", 5);
        var root = E.CreateItems(s, "raw_root", 1, "camp").Single();
        Ok(E.DoInstant(s, InstantRequest.PutFoodInPot(root.InstanceId, pot.InstanceId, 1)));
        var food = E.PotFood(s, pot.InstanceId).Single();
        Assert.That(E.ProcessOf(s, food)!.ReservedWaterMl, Is.EqualTo(0));
        Assert.That(E.ContentStatus(s, food), Is.EqualTo("待净水1000毫升"));
        FillPot(s, pot, "clean_water", 5);
        Assert.That(E.ProcessOf(s, food)!.ReservedWaterMl, Is.EqualTo(1000));
        Assert.That(E.PotFreeWaterUnits(s, pot), Is.EqualTo(0), "1000 毫升全被这一份预留");
    }

    // ───────────── 首采被动（W56-Q973-01、02） ─────────────

    [Test]
    public void W56_Q973_FirstDailyGather_WaterPlusFive_OthersPlusOne_IllegalZero()
    {
        var s = Fresh();
        s.Skills["gathering"].Level = 10;   // gathering_10 精取：yield.gatherFirstDaily +1
        var cistern = Cfg.Resources.Single(r => r.NodeId == "w16_service_cistern");
        var berries = Cfg.Resources.Single(r => r.NodeId == "berries");
        var stream = Cfg.Resources.Single(r => r.NodeId == "water");
        var square = Cfg.Resources.Single(r => r.NodeId == "h02_square_water");
        var notes = new List<string>();
        // b56 json waterFirstDaily：旧 +1 份 500 毫升 → 新 +5 份仍 500 毫升
        Assert.That(E.PassiveGatherBonus(s, s.Player, cistern, "dirty_water", notes), Is.EqualTo(5));
        Assert.That(notes, Has.Some.Contains("此处今日首次采集额外+5（仍扣库存）"));
        // otherItemPlusOneUnchanged：其他物品仍 +1
        Assert.That(E.PassiveGatherBonus(s, s.Player, berries, "berry"), Is.EqualTo(1));
        // W56-Q973-02 仅原合法水资源触发：不限量溪水、城市配给（不吃熟练被动）原本就不触发，仍为 0
        Assert.That(E.PassiveGatherBonus(s, s.Player, stream, "dirty_water"), Is.EqualTo(0));
        Assert.That(E.PassiveGatherBonus(s, s.Player, square, "clean_water"), Is.EqualTo(0));
    }

    // ───────────── 设计数据同步（配方、水煮、单位） ─────────────

    [Test, Category("DesignSync")]
    public void DesignSync_RecipeWaterConversions_FoodProcesses_Unit()
    {
        var c = Contract();
        var rows = (JArray)c["recipeWaterConversions"]!;
        Assert.That(rows, Has.Count.EqualTo(10));
        foreach (var row in rows)
        {
            var rc = Cfg.Recipes.Single(r => r.Id == (string)row["id"]!);
            Assert.That(rc.Inputs[(string)row["waterId"]!], Is.EqualTo((int)row["inputNew"]!), rc.Id);
            Assert.That((int)row["inputNew"]! * 100, Is.EqualTo((int)row["ml"]!), $"{rc.Id} 原毫升守恒");
            if (row["outputId"] is JToken oid)
                Assert.That(rc.Outputs[(string)oid!], Is.EqualTo((int)row["outputNew"]!), rc.Id);
        }
        foreach (var fp in (JArray)c["foodProcesses"]!)
        {
            var p = Cfg.Processes.Single(x => x.Id == (string)fp["id"]!);
            Assert.That((p.WaterCostMl, p.HeatMinutes), Is.EqualTo(((int)fp["waterCostMl"]!, (int)fp["heatMinutes"]!)), p.Id);
        }
        var boil = Cfg.Processes.Single(x => x.Id == "boil_water");
        Assert.That(boil.AllowedBatchMl, Is.EqualTo(c["waterBoiling"]!["allowedBatchMl"]!.Select(x => (int)x).ToList()));
        foreach (var id in new[] { "dirty_water", "clean_water" })
        {
            var it = Cfg.Items.Single(i => i.Id == id);
            Assert.That((it.UnitVolumeMl, it.WeightGrams, it.StackMax, it.Use!.Hydration),
                Is.EqualTo(((int?)(int)c["unit"]!["ml"]!, (int)c["unit"]!["grams"]!, (int)c["unit"]!["waterStackMax"]!, (double)c["unit"]!["baseHydration"]!)), id);
        }
    }

    // ───────────── 存档 57 → 58 ─────────────

    [Test]
    public void Migrate57To58_WaterTimesFive_CanteenMlKept_BoilReservationRereserved_Idempotent()
    {
        var s = Fresh();
        var pot = PotOnLitFire(s);
        FillPot(s, pot, "clean_water", 10);
        var root = E.CreateItems(s, "raw_root", 1, "camp").Single();
        Ok(E.DoInstant(s, InstantRequest.PutFoodInPot(root.InstanceId, pot.InstanceId, 1)));
        var food = E.PotFood(s, pot.InstanceId).Single();
        var canteen = E.CreateItems(s, "ration_canteen", 1, ItemInstance.Carried).Single();
        canteen.LiquidId = "clean_water";
        canteen.LiquidMl = 750;
        // 起点：57 旧档——水按每份 500 毫升记份，水煮预留旧 500
        E.CreateItems(s, "dirty_water", 3, ItemInstance.Carried);
        foreach (var w in E.PotLiquids(s, pot.InstanceId)) w.Count = 2;
        E.ProcessOf(s, food)!.ReservedWaterMl = 500;
        s.Resources["w16_service_cistern"].Available = 3;
        s.Resources["w16_service_cistern"].Reserved = 1;
        var woodStock = s.Resources["wood"].Available;
        s.City.Quotes.Add(new AppraisalQuoteSnapshot { CampaignId = "c", ServiceId = Cfg.Appraisal.ServiceId, Day = 1, QuoteVersion = Cfg.Appraisal.QuoteVersion,
            Offers = new Dictionary<string, int> { ["white_for_cloth"] = 2, ["white_for_scrap"] = 3, ["white_for_water"] = 2 } });
        s.SchemaVersion = "57";

        var m = SaveSerializer.Deserialize(SaveSerializer.Serialize(s), Cfg);
        Assert.That(m.SchemaVersion, Is.EqualTo("58"));
        Assert.That(Count(m, "dirty_water", ItemInstance.Carried), Is.EqualTo(15), "随身脏水 3×500 → 15×100");
        Assert.That(E.PotUnits(m, pot.InstanceId), Is.EqualTo(10), "锅水 2×500 → 10×100");
        var mc = m.Items.Single(i => i.InstanceId == canteen.InstanceId);
        Assert.That((mc.LiquidId, mc.LiquidMl), Is.EqualTo(("clean_water", 750)), "水壶按毫升存，不换算");
        Assert.That((m.Resources["w16_service_cistern"].Available, m.Resources["w16_service_cistern"].Reserved), Is.EqualTo(((int?)15, 5)));
        Assert.That(m.Resources["wood"].Available, Is.EqualTo(woodStock), "非水资源点不动");
        Assert.That(m.City.Quotes.Single().Offers, Is.EquivalentTo(new Dictionary<string, int> { ["white_for_cloth"] = 2, ["white_for_scrap"] = 3, ["white_for_water"] = 10 }));
        var mfood = m.Items.Single(i => i.InstanceId == food.InstanceId);
        Assert.That(E.ProcessOf(m, mfood)!.ReservedWaterMl, Is.EqualTo(0), "旧 500 预留清零");
        WaitMinutes(m, 5);
        Assert.That(E.ProcessOf(m, mfood)!.ReservedWaterMl, Is.EqualTo(1000), "下一世界段首按新耗水 1000 重新预留");

        // 幂等：58 档再读不再×5
        var again = SaveSerializer.Deserialize(SaveSerializer.Serialize(m), Cfg);
        Assert.That(Count(again, "dirty_water", ItemInstance.Carried), Is.EqualTo(15));
        Assert.That(again.City.Quotes.Single().Offers["white_for_water"], Is.EqualTo(10));
    }

    [Test]
    public void Migrate57To58_NonWaterUnchanged_OnlyWaterNodesScaled()
    {
        // 配对：57 档里没有水物品时，物品总数不变；只有产水资源点库存×5
        var s = Fresh();
        var stock = s.Resources["w16_service_cistern"].Available;
        var items = s.Items.Sum(i => i.Count);
        s.SchemaVersion = "57";
        var m = SaveSerializer.Deserialize(SaveSerializer.Serialize(s), Cfg);
        Assert.That(m.SchemaVersion, Is.EqualTo("58"));
        Assert.That(m.Items.Sum(i => i.Count), Is.EqualTo(items));
        Assert.That(m.Resources["w16_service_cistern"].Available, Is.EqualTo(stock is int a ? a * 5 : (int?)null), "产水点库存×5（原未初始化为 null 时仍 null）");
    }
}
