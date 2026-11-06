using Newtonsoft.Json.Linq;
using Survival.Core.Config;
using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 B29 饮水预算（R16、R24、R25、R89、R94、R95）。合同 assets/20261105/b29-entry-water-q258-268.json water、treatment；
/// assets/20261104 water.formula、carriedPotQ233、treatmentCapacityQ234。验收 Q258-268-A04、A05、A10—A12，WATER-Q233-02、WATER-Q234-01/02。
/// </summary>
public class B29WaterBudgetTests : TestBase
{
    private const string Lin = "npc_lin_zhao";

    private static JObject Contract()
    {
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "doc", "策划案"))) dir = dir.Parent;
        Assert.That(dir, Is.Not.Null, "找不到仓库根目录");
        return JObject.Parse(File.ReadAllText(Path.Combine(dir!.FullName, "doc", "策划案", "系统策划总册 v1.0", "assets", "20261105", "b29-entry-water-q258-268.json")));
    }

    private static readonly string[] WaterItems = { "clean_water", "dirty_water", "ration_canteen", "pot", "purification_tablet", "filter_cartridge", "water_filter" };

    /// <summary>白天、没有随身水和脏水，全员水分 50。</summary>
    private GameState Clean(GameEngine? eng = null, params string[] companions)
    {
        var e = eng ?? E;
        var s = e.NewGame(11);
        e.DebugLegacyStory(s);
        s.Progress.Flags.Add("hub_registered");
        s.Progress.Flags.Add("church_route_record");
        s.Progress.Flags.Add("academy_safety_record");
        s.GameMinute = At(1, 10, 0);
        foreach (var id in companions) e.DebugRecruit(s, id);
        s.Items.RemoveAll(i => WaterItems.Contains(i.ItemId));
        foreach (var a in s.Actors.Values) a.Stats.Hydration = 50;
        return s;
    }

    private ItemInstance Give(GameState s, string item, string owner, int count = 1) => E.CreateItems(s, item, count, owner).Single();

    private ItemInstance Canteen(GameState s, string owner, string liquid, int ml)
    {
        var c = Give(s, "ration_canteen", owner);
        c.LiquidId = liquid;
        c.LiquidMl = ml;
        return c;
    }

    private (ItemInstance pot, ItemInstance water) PotWater(GameState s, string owner, string liquid, int units)
    {
        var pot = Give(s, "pot", owner);
        return (pot, Give(s, liquid, ItemInstance.ContainerOwner(pot.InstanceId), units));
    }

    private static GameEngine EngineWith(Action<GameConfig> edit)
    {
        var c = ConfigLoader.FromJson(File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "config.v0.6.json")));
        edit(c);
        return new GameEngine(c);
    }

    /// <summary>合同公式的独立写法，用来核对内核。</summary>
    private static int Need(double r, double w, int t, int route)
    {
        var d = Math.Max(r * (t + route) / 60, r * t / 60 + 30);
        var b = Math.Max(0, d + 20 - w);
        return 250 * (int)Math.Ceiling(b / (15 - r / 12) - 1e-9);
    }

    // ───────────── 逐字稿 ─────────────

    [Test]
    public void Copy_VerbatimAgainstContract()
    {
        var c = Contract();
        var w = (JObject)c["water"]!["copy"]!;
        Assert.That(w.Properties().Select(p => p.Name), Is.EquivalentTo(WaterCopy.Water.Keys));
        foreach (var kv in WaterCopy.Water) Assert.That(kv.Value, Is.EqualTo(w[kv.Key]!.Value<string>()), kv.Key);
        var t = (JObject)c["treatment"]!["copy"]!;
        Assert.That(t.Properties().Select(p => p.Name), Is.EquivalentTo(WaterCopy.Treatment.Keys));
        foreach (var kv in WaterCopy.Treatment) Assert.That(kv.Value, Is.EqualTo(t[kv.Key]!.Value<string>()), kv.Key);
    }

    // ───────────── A04 回撤预算 R ─────────────

    [Test]
    public void A04_EightDungeons_RouteMatchesSnapshot_AtFactorOne()
    {
        var snapshot = (JArray)Contract()["water"]!["routeCheckSnapshot"]!;
        Assert.That(snapshot, Has.Count.EqualTo(8));
        var s = Clean();
        foreach (var row in snapshot)
        {
            var id = row["dungeonId"]!.Value<string>()!;
            var d = E.DungeonCfg(id)!;
            Assert.That(d.RecordNode, Is.EqualTo(row["coreNode"]!.Value<string>()), id);
            Assert.That(d.Parent, Is.EqualTo(row["parentNode"]!.Value<string>()), id);
            Assert.That(E.WaterRouteBaseMinutes(id), Is.EqualTo(row["configuredBaseMinutes"]!.Value<int>()), id);
            Assert.That(E.ExpeditionWaterBudget(s, id).RouteMinutes, Is.EqualTo(row["RAtFactor1"]!.Value<int>()), id);
        }
    }

    [Test]
    public void A04_FirstVisit_UsesCurrentFactorAndRounding()
    {
        var s = Clean();
        s.GameMinute = At(1, 23, 0);
        var f = E.TimeFactor(s, s.Player, ActionType.Travel);
        Assert.That(f, Is.GreaterThan(1), "夜间移动倍率");
        var r = E.ExpeditionWaterBudget(s, "d01").RouteMinutes!.Value;
        Assert.That(r, Is.EqualTo(Math.Max(30, 5 * (int)Math.Ceiling(35 * f / 5 - 1e-9))));
    }

    [Test]
    public void A04_RouteIsNotEntryExtract_NoRoomNames()
    {
        var s = Clean();
        var p = E.ExpeditionPreview(s, "d01");
        Assert.That(p.WaterBudget.RouteMinutes, Is.EqualTo(35));
        foreach (var room in Cfg.Locations.Where(l => l.DungeonId == "d01"))
            Assert.That(string.Join("", p.WaterLines), Does.Not.Contain(room.Name), "不公开房名");
    }

    // ───────────── A05 缺路径 ─────────────

    [Test]
    public void A05_MissingRoute_StockAndRouteLinesOnly_ReadOnly()
    {
        var eng = EngineWith(c => c.Edges.RemoveAll(e => e.Bidirectional && (e.A == "d01_08" || e.B == "d01_08")));
        var s = Clean(eng);
        eng.CreateItems(s, "clean_water", 2, ItemInstance.Carried);
        var before = SaveSerializer.Serialize(s);
        var p = eng.ExpeditionPreview(s, "d01");
        Assert.That(SaveSerializer.Serialize(s), Is.EqualTo(before), "读预告 0 时间 0 抽样");
        Assert.That(p.WaterBudget.State, Is.EqualTo(WaterBudgetState.RouteMissing));
        Assert.That(p.WaterBudget.RouteMinutes, Is.Null);
        Assert.That(p.WaterLines[0], Is.EqualTo("全队随身净水1000毫升。"));
        Assert.That(p.WaterLines[1], Is.EqualTo(WaterCopy.RouteMissing));
        Assert.That(string.Join("\n", p.WaterLines), Does.Not.Contain("预计需另备").And.Not.Contain("另需携带").And.Not.Contain("缺口").And.Not.Contain("可覆盖").And.Not.Contain("预算不足"));
        Assert.That(p.WaterLines.Any(l => l.StartsWith("处理用品：")) && p.WaterLines.Any(l => l.StartsWith("热源：")), Is.True);
        Assert.That(p.WaterLines.Last(), Is.EqualTo(WaterCopy.Disclaimer));
    }

    [Test]
    public void A05_MissingEntryLink_CountsAsMissing()
    {
        var eng = EngineWith(c => c.Edges.RemoveAll(e => (e.A == "c03_crypt" && e.B == "d01_01") || (e.B == "c03_crypt" && e.A == "d01_01")));
        Assert.That(eng.WaterRouteBaseMinutes("d01"), Is.Null);
    }

    [Test]
    public void A05_PreviewIsReadOnly_NormalState()
    {
        var s = Clean(null, Lin);
        var before = SaveSerializer.Serialize(s);
        E.ExpeditionPreview(s, "d01");
        Assert.That(SaveSerializer.Serialize(s), Is.EqualTo(before));
    }

    // ───────────── r_i 与逐人区间 ─────────────

    [Test]
    public void Formula_PerPerson_SumsAndGap()
    {
        var s = Clean(null, Lin);
        s.Player.Stats.Hydration = 10;
        s.Actors[Lin].Stats.Hydration = 80;
        E.CreateItems(s, "clean_water", 1, ItemInstance.Carried);
        var (lo, hi) = E.ExploreEstimate(s, "d01");
        var b = E.ExpeditionWaterBudget(s, "d01");
        Assert.That(b.State, Is.EqualTo(WaterBudgetState.Normal));
        Assert.That(b.Persons.Select(p => p.Name), Is.EqualTo(new[] { "你", E.ActorDisplayName(s, Lin) }));
        Assert.That(b.Persons[0].LossPerHour, Is.EqualTo(3.6).Within(1e-9), "取七种模式的最大值");
        Assert.That(b.Persons[0].LowMl, Is.EqualTo(Need(3.6, 10, lo, 35)));
        Assert.That(b.Persons[0].HighMl, Is.EqualTo(Need(3.6, 10, hi, 35)));
        Assert.That(b.Persons[1].HighMl, Is.EqualTo(Need(b.Persons[1].LossPerHour, 80, hi, 35)));
        Assert.That(b.TotalLowMl, Is.EqualTo(b.Persons.Sum(p => p.LowMl)));
        Assert.That(b.TotalHighMl, Is.EqualTo(b.Persons.Sum(p => p.HighMl)));
        Assert.That(b.GapMl, Is.EqualTo(Math.Max(0, b.TotalHighMl - 500)));
        var lines = E.ExpeditionPreview(s, "d01").WaterLines;
        Assert.That(lines[0], Is.EqualTo($"你：按当前状态预计需另备净水{b.Persons[0].LowMl}—{b.Persons[0].HighMl}毫升"));
        Assert.That(lines[2], Is.EqualTo($"全队随身净水500毫升；保守预计另需携带{b.TotalLowMl}—{b.TotalHighMl}毫升。"));
        Assert.That(lines[3], Is.EqualTo($"与现有净水比较，缺口{b.GapMl}毫升"));
        Assert.That(lines[4], Is.EqualTo(WaterCopy.Insufficient));
    }

    [Test]
    public void Formula_DiarrheaRaisesRate()
    {
        var s = Clean();
        var dry = E.WaterLossPerHour(s, s.Player);
        s.Player.DiarrheaUntil = s.GameMinute + 60;
        Assert.That(E.WaterLossPerHour(s, s.Player), Is.EqualTo(dry + 3).Within(1e-9));
    }

    [Test]
    public void Enough_WhenStockCoversHigh_GapZeroStillShown()
    {
        var s = Clean();
        s.Player.Stats.Hydration = 100;
        E.CreateItems(s, "clean_water", 40, ItemInstance.Carried);
        Assert.That(E.ExpeditionWaterBudget(s, "d01").Enough, Is.True);
        var lines = E.ExpeditionPreview(s, "d01").WaterLines;
        Assert.That(lines, Does.Contain("与现有净水比较，缺口0毫升"));
        Assert.That(lines, Does.Contain(WaterCopy.Enough));
        Assert.That(lines, Does.Not.Contain(WaterCopy.Insufficient));
    }

    [Test]
    public void StockZero_WritesZero()
    {
        var lines = E.ExpeditionPreview(Clean(null, Lin), "d01").WaterLines;
        Assert.That(lines.Any(l => l.StartsWith("全队随身净水0毫升；")), Is.True);
        Assert.That(string.Join("", lines), Does.Not.Contain("NaN").And.Not.Contain("∞"));
    }

    // ───────────── A10 显示 ─────────────

    [Test]
    public void A10_OldLinesGone_NamesUseYou()
    {
        var s = Clean();
        E.CreateItems(s, "clean_water", 1, ItemInstance.Carried);
        var lines = E.ExpeditionPreview(s, "d01").WaterLines;
        Assert.That(lines[0], Does.StartWith("你：按当前状态预计需另备净水"));
        Assert.That(lines.Any(l => l.StartsWith("随身净水（") || l.StartsWith("需处理的脏水")), Is.False);
        Assert.That(lines.Count(l => l.StartsWith("处理用品：")), Is.EqualTo(1));
        Assert.That(lines.Count(l => l.StartsWith("热源：")), Is.EqualTo(1));
        Assert.That(lines.Last(), Is.EqualTo(WaterCopy.Disclaimer));
    }

    [Test]
    public void A10_DenominatorNonPositive_PersonSentence_TeamConclusionHidden()
    {
        // 腹泻额外失水调到分母≤0，只在主角身上生效
        var eng = EngineWith(c => c.ItemActions.DiarrheaExtraHydrationPerHour = -200);
        var s = Clean(eng, Lin);
        eng.CreateItems(s, "clean_water", 2, ItemInstance.Carried);
        s.Player.DiarrheaUntil = s.GameMinute + 60;
        Assert.That(eng.ExpeditionWaterBudget(s, "d01").State, Is.EqualTo(WaterBudgetState.Unreliable));
        var lines = eng.ExpeditionPreview(s, "d01").WaterLines;
        Assert.That(lines[0], Is.EqualTo("你：病情失水过快，无法可靠估算饮水。"));
        Assert.That(lines[1], Does.StartWith(eng.ActorDisplayName(s, Lin) + "：按当前状态预计需另备净水"));
        Assert.That(lines[2], Is.EqualTo("全队随身净水1000毫升。"));
        Assert.That(string.Join("\n", lines), Does.Not.Contain("另需携带").And.Not.Contain("缺口").And.Not.Contain("可覆盖").And.Not.Contain("预算不足"));
        Assert.That(lines.Last(), Is.EqualTo(WaterCopy.Disclaimer));
    }

    // ───────────── 库存（WATER-Q233-02） ─────────────

    [Test]
    public void Stock_CardCanteenPot_1750_NoDoubleCount()
    {
        var s = Clean(null, Lin);
        Give(s, "clean_water", ItemInstance.Carried);
        Canteen(s, ItemInstance.CarriedOwner(Lin), "clean_water", 250);
        PotWater(s, ItemInstance.Carried, "clean_water", 2);
        Assert.That(E.ExpeditionWaterBudget(s, "d01").StockMl, Is.EqualTo(1750));
    }

    [Test]
    public void Stock_ReservedAndElsewhere_NotCounted()
    {
        var s = Clean(null, Lin);
        Give(s, "clean_water", ItemInstance.Carried, 2).ReservedBy = 4;
        var (pot, _) = PotWater(s, ItemInstance.Carried, "clean_water", 2);
        s.Locations[s.CurrentLocation].Facility.Processes.Add(new FacilityProcess { RecipeId = "boil_root", TargetInstanceId = 99999, PotInstanceId = pot.InstanceId, ReservedWaterMl = 500 });
        E.CreateItems(s, "clean_water", 5, s.Ground);
        Give(s, "dirty_water", ItemInstance.Carried);
        Assert.That(E.ExpeditionWaterBudget(s, "d01").StockMl, Is.EqualTo(500), "预留水不计，地面不计，脏水不计");

        var t = Clean();
        Canteen(t, ItemInstance.Carried, "clean_water", 500).ReservedBy = 1;
        Assert.That(E.ExpeditionWaterBudget(t, "d01").StockMl, Is.EqualTo(0));
    }

    [Test]
    public void Stock_ReserveCompanion_NotCounted()
    {
        var s = Clean(null, Lin);
        Give(s, "clean_water", ItemInstance.CarriedOwner(Lin), 2);
        Assert.That(E.ExpeditionWaterBudget(s, "d01").StockMl, Is.EqualTo(1000));
        s.Party.Remove(Lin);
        Assert.That(E.ExpeditionWaterBudget(s, "d01").StockMl, Is.EqualTo(0), "候补不计");
    }

    // ───────────── 处理量（WATER-Q234-01/02、A11、A12） ─────────────

    private GameState Treat(int dirtyMl, int tablets, int cartridges, bool filter)
    {
        var s = Clean();
        if (dirtyMl > 0) Give(s, "dirty_water", ItemInstance.Carried, dirtyMl / 500);
        if (tablets > 0) Give(s, "purification_tablet", ItemInstance.Carried, tablets);
        if (cartridges > 0) Give(s, "filter_cartridge", ItemInstance.Carried, cartridges);
        if (filter) Give(s, "water_filter", ItemInstance.Carried);
        return s;
    }

    [TestCase(3500, 1, 1, true, 2000)]
    [TestCase(8000, 1, 2, true, 6000)]
    [TestCase(8000, 1, 2, false, 2000)]
    [TestCase(1500, 5, 5, true, 0)]
    public void Q234_Capacity(int d, int p, int c, bool f, int v)
    {
        var b = E.ExpeditionWaterBudget(Treat(d, p, c, f), "d01");
        Assert.That(b.DirtyMl, Is.EqualTo(d));
        Assert.That(b.TreatableMl, Is.EqualTo(v));
        Assert.That(b.StockMl, Is.EqualTo(0), "V 不进现有净水");
    }

    [Test]
    public void Q234_DirtyCountsCanteenAndPot_Once()
    {
        var s = Clean(null, Lin);
        Give(s, "dirty_water", ItemInstance.Carried);
        Canteen(s, ItemInstance.CarriedOwner(Lin), "dirty_water", 750);
        PotWater(s, ItemInstance.Carried, "dirty_water", 2);
        Assert.That(E.ExpeditionWaterBudget(s, "d01").DirtyMl, Is.EqualTo(2250));
    }

    [Test]
    public void Q234_ReservedMaterials_NotUsable()
    {
        var s = Treat(2000, 1, 0, false);
        Assert.That(E.ExpeditionWaterBudget(s, "d01").TreatableMl, Is.EqualTo(2000));
        s.Items.Single(i => i.ItemId == "purification_tablet").ReservedBy = 9;
        var b = E.ExpeditionWaterBudget(s, "d01");
        Assert.That(b.Tablets, Is.EqualTo(0));
        Assert.That(b.TreatableMl, Is.EqualTo(0));
        Assert.That(E.ExpeditionPreview(s, "d01").WaterLines, Does.Contain(WaterCopy.TreatNoMaterials), "被预留的耗材不当可用");
    }

    [Test]
    public void A11_NoDirty_BlockHidden()
    {
        var all = string.Join("\n", E.ExpeditionPreview(Treat(0, 1, 1, false), "d01").WaterLines);
        Assert.That(all, Does.Not.Contain("处理后才可饮用").And.Not.Contain("暂不计入").And.Not.Contain("可烧水处理").And.Not.Contain("未凑足一批"));
        Assert.That(all, Does.Contain("处理用品："));
    }

    [Test]
    public void A11_1500_NoMaterials_TwoRealGaps_InOrder()
    {
        var lines = E.ExpeditionPreview(Treat(1500, 0, 0, false), "d01").WaterLines;
        var main = lines.IndexOf("处理后才可饮用：随身脏水1500毫升；按现有净水片/滤芯，最多可处理0毫升（每批2000毫升，需完整4份脏水）。");
        Assert.That(main, Is.GreaterThanOrEqualTo(0));
        Assert.That(lines[main + 1], Is.EqualTo(WaterCopy.TreatNoMaterials));
        Assert.That(lines[main + 2], Is.EqualTo(WaterCopy.TreatSmallBatch));
    }

    [Test]
    public void A11_CartridgeOnly_NoFilter_NotNoMaterials()
    {
        var lines = E.ExpeditionPreview(Treat(2000, 0, 2, false), "d01").WaterLines;
        Assert.That(lines, Does.Contain("滤芯2份暂不计入：需要可用滤水器"));
        Assert.That(lines, Does.Not.Contain(WaterCopy.TreatNoMaterials));
        Assert.That(lines, Does.Not.Contain(WaterCopy.TreatSmallBatch));
    }

    [Test]
    public void A11_ReservedFilter_NotAvailable()
    {
        var s = Treat(2000, 0, 1, true);
        s.Items.Single(i => i.ItemId == "water_filter").ReservedBy = 2;
        var b = E.ExpeditionWaterBudget(s, "d01");
        Assert.That(b.HasFilter, Is.False);
        Assert.That(b.TreatableMl, Is.EqualTo(0));
    }

    [Test]
    public void A12_Boiling_NeedsFreeCarriedPot_NoToolCheck()
    {
        var s = Treat(500, 0, 0, false);
        s.Items.RemoveAll(i => E.Item(i.ItemId).ToolTag == "lighter" || i.ItemId == "wood");
        Assert.That(E.ExpeditionPreview(s, "d01").WaterLines, Does.Not.Contain(WaterCopy.TreatBoiling));

        var pot = Give(s, "pot", ItemInstance.Carried);
        var lines = E.ExpeditionPreview(s, "d01").WaterLines;
        Assert.That(lines, Does.Contain(WaterCopy.TreatBoiling), "没有点火工具和燃料也只提示路径");
        Assert.That(lines.IndexOf(WaterCopy.TreatBoiling), Is.GreaterThan(lines.IndexOf(WaterCopy.TreatSmallBatch)));
        Assert.That(E.ExpeditionWaterBudget(s, "d01").StockMl, Is.EqualTo(0), "烧水不进净水");

        pot.ReservedBy = 8;
        Assert.That(E.ExpeditionPreview(s, "d01").WaterLines, Does.Not.Contain(WaterCopy.TreatBoiling), "锁中的锅不算");
        pot.ReservedBy = null;
        pot.Owner = s.Ground;
        Assert.That(E.ExpeditionPreview(s, "d01").WaterLines, Does.Not.Contain(WaterCopy.TreatBoiling), "地面的锅不算");
    }
}
