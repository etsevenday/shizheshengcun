using Newtonsoft.Json.Linq;
using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 批次 B22e：策划答复 Q173 按 B（需求 R172）——普通野外外部区显式清单可铺野营布、可建野外固定火堆；
/// 柴炉仍限 temporaryCamp 8 处，区域火源唯一。规则出处：assets/20261103/camping-appraisal-adjudications.json 的 camping
/// （allowedExteriorNodeIds、tarp、fixedFire、fireOperation、fireUse、stove、migration）、rules.Q173；验收 PLAN-Q173-01、PLAN-Q173-02。
/// </summary>
public class B22eTests : TestBase
{
    private void Ok(ActionResult r) => Assert.That(r.Ok, Is.True, r.Reason);

    private static ActionRequest Fire() => ActionRequest.Craft("wild_fixed_fire", null, null, 1);

    private static JObject Camping() =>
        (JObject)JObject.Parse(File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "design.camping-appraisal.json")))["camping"]!;

    private static int Sum(GameState s, string itemId, string? owner = null) =>
        s.Items.Where(i => i.ItemId == itemId && (owner == null || i.Owner == owner)).Sum(i => i.Count);

    /// <summary>到某地外部区（清掉当地敌人），可选在当前地面放石4木2。</summary>
    private GameState At(string loc, bool materials = true)
    {
        var s = New();
        if (loc != s.CurrentLocation) E.DebugTeleport(s, loc);
        s.WorldEnemies.RemoveAll(w => w.LocationId == loc);
        if (materials)
        {
            E.CreateItems(s, "stone", 4, s.Ground);
            E.CreateItems(s, "wood", 2, s.Ground);
        }
        return s;
    }

    private GameState WithFire(string loc)
    {
        var s = At(loc);
        Start(s, Fire());
        Run(s);
        Assert.That(s.Locations[loc].Facility.FireBuilt, Is.True);
        return s;
    }

    // ───────────── 数据：清单与火堆参数与策划 JSON 一致（PLAN-Q173-01） ─────────────

    [Test]
    public void PLAN_Q173_01_Config_MatchesDesign()
    {
        var c = Camping();
        var ids = c["allowedExteriorNodeIds"]!.Select(x => (string)x!).ToList();
        Assert.That(Cfg.Safehouse.Camping.AllowedExteriorNodeIds, Is.EqualTo(ids), "清单逐项照抄策划 JSON");
        Assert.That(ids.Count, Is.EqualTo(54));
        foreach (var id in ids) Assert.That(Cfg.Locations.Any(l => l.Id == id && l.WorldKind == "overworld" && !l.IsDungeon), id);
        // 柴炉 8 处都在清单里，但清单不扩大柴炉许可
        var tempCamps = Cfg.Locations.Where(l => l.TemporaryCamp).Select(l => l.Id).ToList();
        Assert.That(tempCamps.Count, Is.EqualTo(8));
        Assert.That(tempCamps.All(ids.Contains), Is.True);

        var f = c["fixedFire"]!;
        var rc = E.WildFireRecipe;
        Assert.That(rc.Id, Is.EqualTo((string)f["recipeId"]!));
        Assert.That(rc.Inputs, Is.EqualTo(new Dictionary<string, int> { ["stone"] = (int)f["inputs"]!["stone"]!, ["wood"] = (int)f["inputs"]!["wood"]! }));
        Assert.That((rc.BaseMinutes, rc.MinSkills["crafting"], rc.Knowledge, rc.Station, rc.IsBuild, rc.FacilityResult),
            Is.EqualTo(((int)f["baseMinutes"]!, (int)f["level"]!, (string?)null, (string)f["station"]!, true, "fireBuilt")));
        Assert.That(rc.MinSkills.Keys, Is.EqualTo(new[] { (string)f["skill"]! }));
        Assert.That(E.CraftXp(rc.BaseMinutes), Is.EqualTo((long)(int)f["constructionXp"]!), "原建造经验口径 ceil(30/10)=3");
        var fire = Cfg.Fire;
        Assert.That((fire.ProcessingSlots, fire.MaxPots, fire.MaxFuelMinutes, fire.BurnMinutesPerWood, fire.IgniteMinutes),
            Is.EqualTo(((int)f["fireSlots"]!, (int)f["maxPots"]!, (int)f["maxFuelMinutes"]!, (int)f["woodAddsMinutes"]!, (int)f["lightMinutes"]!)));
    }

    // ───────────── 野营布：白名单外部区可铺，地牢/城市公共/势力地/屋内拒绝 ─────────────

    [Test]
    public void PLAN_Q173_02_Tarp_WhitelistOnly()
    {
        // 普通野外（非 8 处露营点）现在可铺：不要求登记、建屋或探索 100%
        var s = At("field", false);
        Assert.That(s.Progress.Flags.Contains("hub_registered"), Is.False);
        var tarp = E.CreateItems(s, "field_tarp", 1, ItemInstance.Carried).Single();
        Assert.That(E.CanStart(s, GameEngine.Deploy(tarp)), Is.Null);
        var t0 = s.GameMinute;
        Start(s, GameEngine.Deploy(tarp));
        Run(s);
        Assert.That(s.GameMinute - t0, Is.EqualTo(10), "铺开固定等待 10 分钟");
        Assert.That((E.TarpDeployedAt(s, "field"), E.IsDeployed(s, tarp)), Is.EqualTo((true, true)));
        Assert.That(E.CanStart(s, GameEngine.Deploy(E.CreateItems(s, "field_tarp", 1, ItemInstance.Carried).Single())), Does.Contain("已经铺开"));

        // camp 在清单内：可铺（棚的遮蔽不叠加）
        var c = At("camp", false);
        var ct = E.CreateItems(c, "field_tarp", 1, ItemInstance.Carried).Single();
        Assert.That(E.CanStart(c, GameEngine.Deploy(ct)), Is.Null);

        // 清单外：城市公共地、势力地、地牢（含 safe 房）一律拒绝
        foreach (var loc in new[] { "h02_square", "b01_school", "c02_church", "d06_06", "d01_01" })
        {
            var x = At(loc, false);
            var xt = E.CreateItems(x, "field_tarp", 1, ItemInstance.Carried).Single();
            Assert.That(E.CanStart(x, GameEngine.Deploy(xt)), Does.Contain("普通野外"), loc);
        }
    }

    [Test]
    public void PLAN_Q173_02_Stove_StillOnlyEightCamps()
    {
        var s = At("field", false);
        var stove = E.CreateItems(s, "folding_stove", 1, ItemInstance.Carried).Single();
        Assert.That(E.CanStart(s, GameEngine.Deploy(stove)), Does.Contain("临时露营许可点"), "Q173 不放宽柴炉许可");
        var w = At("w19_survey", false);
        var ws = E.CreateItems(w, "folding_stove", 1, ItemInstance.Carried).Single();
        Assert.That(E.CanStart(w, GameEngine.Deploy(ws)), Is.Null);
    }

    // ───────────── 野外固定火堆：建造、初始状态、经验与使用 ─────────────

    [Test]
    public void PLAN_Q173_02_WildFire_BuildsInitialOut_Xp3_UsesFireRules()
    {
        var s = At("field");
        Assert.That(E.HasFireAt(s, "field"), Is.False);
        Assert.That(E.WildFireReasons(s), Is.Empty);
        var xp0 = s.Skills["crafting"].Xp;
        var lv0 = s.Skills["crafting"].Level;
        var (stone0, wood0) = (Sum(s, "stone"), Sum(s, "wood"));
        var t0 = s.GameMinute;
        Start(s, Fire());
        Assert.That(s.Main!.Type, Is.EqualTo(ActionType.Build));
        Assert.That(s.Main.ReservedItemIds, Is.Not.Empty, "开工预留材料");
        Run(s);
        Assert.That(s.GameMinute - t0, Is.GreaterThanOrEqualTo(30));
        var fac = s.Locations["field"].Facility;
        Assert.That(E.HasFireAt(s, "field"), Is.True);
        Assert.That((fac.FireBuilt, fac.FireLit, fac.FuelMinutes, fac.FacilityId, E.FuelCapOf(fac)), Is.EqualTo((true, false, 0, "field:fire", 240)));
        Assert.That(E.FireContents(s, "field"), Is.Empty, "初始无锅");
        Assert.That((stone0 - Sum(s, "stone"), wood0 - Sum(s, "wood")), Is.EqualTo((4, 2)), "石4木2核销");
        if (s.Skills["crafting"].Level == lv0) Assert.That(s.Skills["crafting"].Xp - xp0, Is.EqualTo(3), "执行者制造经验 3 一次");
        Assert.That(s.Log.Count(l => l.Text.Contains("野外火堆建成")), Is.EqualTo(1));
        Assert.That(E.FacilityBuiltHere(s, E.WildFireRecipe), Is.True);
        Assert.That(E.FirePlaces(s), Does.Contain("field"), "随世界时钟推进");

        // 沿用原火堆规则：投木 +60，上限 240，5 分钟点火
        E.CreateItems(s, "wood", 5, ItemInstance.Carried);
        Ok(E.DoInstant(s, InstantRequest.AddFuel(4)));
        Assert.That(fac.FuelMinutes, Is.EqualTo(240));
        Assert.That(E.DoInstant(s, InstantRequest.AddFuel(1)).Ok, Is.False, "燃料上限 240");
        var lighter = E.IgniteToolOptions(s).FirstOrDefault(o => o.Usable);
        if (lighter == null) { E.CreateItems(s, "lighter", 1, ItemInstance.Carried); lighter = E.IgniteToolOptions(s).First(o => o.Usable); }
        Start(s, ActionRequest.Ignite(lighter.InstanceId));
        Run(s);
        Assert.That((fac.FireLit, fac.FuelMinutes), Is.EqualTo((true, 235)));
        var pot = E.CreateItems(s, "pot", 1, ItemInstance.Carried).Single();
        Ok(E.DoInstant(s, InstantRequest.PutInFire(pot.InstanceId)));
        var pot2 = E.CreateItems(s, "pot", 1, ItemInstance.Carried).Single();
        Assert.That(E.DoInstant(s, InstantRequest.PutInFire(pot2.InstanceId)).Ok, Is.False, "最多一口锅");

        // 同区不能再建第二处
        E.CreateItems(s, "stone", 4, s.Ground);
        E.CreateItems(s, "wood", 2, s.Ground);
        Assert.That(E.CanStart(s, Fire()), Does.Contain("已有火堆"));
    }

    [Test]
    public void PLAN_Q173_02_WildFire_RejectedOutsideWhitelist_AndInside()
    {
        foreach (var loc in new[] { "h02_square", "b01_school", "c02_church", "r02_academy", "d06_06", "d01_01" })
        {
            var x = At(loc);
            Assert.That(E.CanStart(x, Fire()), Does.Contain("普通野外"), loc);
            Assert.That(E.WildFireReasons(x), Has.Some.Contains("普通野外"), loc);
        }
        // camp 已有原火堆：区域火源唯一
        var c = At("camp");
        Assert.That(E.CanStart(c, Fire()), Does.Contain("已有火堆"));
        // 安全屋屋内不能用这个配方（屋内用“建屋内火堆”）
        var h = At("h05_homestead");
        E.DebugBuildSafehouse(h);
        Ok(E.EnterSafehouse(h));
        E.CreateItems(h, "stone", 4, h.Ground);
        E.CreateItems(h, "wood", 2, h.Ground);
        Assert.That(E.CanStart(h, Fire()), Does.Contain("屋外"));
        // 建野外火堆不扩大屋外工坊许可
        Ok(E.ExitSafehouse(h));
        Assert.That(E.CanStart(h, ActionRequest.Craft("build_workbench", null, null, 1)), Is.Not.Null.And.Not.Contain("普通野外"));
    }

    [Test]
    public void PLAN_Q173_02_WildFire_ReasonsListedItemByItem()
    {
        var s = At("field", false);
        E.CreateItems(s, "stone", 1, s.Ground);
        var why = E.CanStart(s, Fire());
        Assert.That(why, Does.Contain("缺少石块（现有1/需4）").And.Contain("缺少木材（现有0/需2）"), "材料逐项写全");
        Assert.That(E.WildFireReasons(s), Has.Count.EqualTo(2));
        // 敌踪：不能靠施工躲开
        s.Encounter = new PendingEncounter { LocationId = "field", EnemyIds = new List<int>(), Source = "explore", Minute = s.GameMinute };
        Assert.That(E.CanStart(s, Fire()), Does.Contain("敌踪"));
        Assert.That(E.WildFireReasons(s), Has.Some.Contains("敌踪").And.Some.Contains("缺少石块"));
    }

    [Test]
    public void PLAN_Q173_02_StoveAndFire_RegionUnique()
    {
        // 有部署柴炉：不能建火堆；收起后可以
        var s = At("w19_survey");
        var stove = E.CreateItems(s, "folding_stove", 1, ItemInstance.Carried).Single();
        Start(s, GameEngine.Deploy(stove));
        Run(s);
        Assert.That(E.CanStart(s, Fire()), Does.Contain("折叠柴炉"));
        Start(s, GameEngine.Pack(stove));
        Run(s);
        Assert.That(E.CanStart(s, Fire()), Is.Null);
        Start(s, Fire());
        Run(s);
        // 有固定火堆：不能再部署柴炉
        Assert.That(E.CanStart(s, GameEngine.Deploy(stove)), Does.Contain("已有固定火堆"));
        Assert.That(s.Locations["w19_survey"].Facility.StoveInstanceId, Is.Null);
    }

    [Test]
    public void PLAN_Q173_02_WildFire_CancelReturnsMaterials_NoRefund_NoXp()
    {
        var s = At("w03_quarry");
        var xp0 = s.Skills["crafting"].Xp;
        var t0 = s.GameMinute;
        Start(s, Fire());
        Steps(s, 2);
        Assert.That(s.Main, Is.Not.Null);
        var elapsed = s.GameMinute - t0;
        Assert.That(elapsed, Is.GreaterThan(0));
        Ok(E.Cancel(s));
        Assert.That(s.GameMinute - t0, Is.EqualTo(elapsed), "已过时间不退");
        Assert.That((Sum(s, "stone", "w03_quarry"), Sum(s, "wood", "w03_quarry")), Is.EqualTo((4, 2)), "预留材料返还当前地面");
        Assert.That(s.Items.Any(i => i.ReservedBy != null), Is.False);
        Assert.That((s.Locations["w03_quarry"].Facility.FireBuilt, E.HasFireAt(s, "w03_quarry")), Is.EqualTo((false, false)));
        Assert.That(s.Skills["crafting"].Xp, Is.EqualTo(xp0), "取消经验 0");
        Assert.That(s.Log.Last(l => l.Kind == GameEventKind.ActionCancelled.ToString()).Text, Does.Contain("预留材料已返还当前地面"));
        // 重新开工从头计算
        Start(s, Fire());
        Assert.That(s.Main!.WorkDone, Is.EqualTo(0));
    }

    [Test]
    public void PLAN_Q173_02_WildFire_DoesNotGrantEncounterSafety()
    {
        var s = At("field");
        s.GameMinute = At(3, 9, 0);
        var before = E.EncounterChance(s, "field", ActionType.Explore);
        s.Locations["field"].Facility.FireBuilt = true;
        Assert.That(E.EncounterChance(s, "field", ActionType.Explore), Is.EqualTo(before), "造火不免敌");
        Assert.That(E.IsSheltered(s, "field"), Is.False, "不赠遮蔽");
    }

    // ───────────── 旧档：不赠、不删、读档保留 ─────────────

    [Test]
    public void PLAN_Q173_02_OldSave_NoGift_FirePersists()
    {
        var s = New();
        var campFuel = s.Locations["camp"].Facility.FuelMinutes;
        var t = SaveSerializer.Deserialize(SaveSerializer.Serialize(s), Cfg);
        foreach (var id in Cfg.Safehouse.Camping.AllowedExteriorNodeIds.Where(i => i != "camp"))
        {
            Assert.That(E.HasFireAt(t, id), Is.False, $"{id} 新开放地点不赠火堆");
            Assert.That(t.Locations[id].Facility.TarpInstanceId, Is.Null, id);
        }
        Assert.That((E.HasFireAt(t, "camp"), t.Locations["camp"].Facility.FuelMinutes), Is.EqualTo((true, campFuel)), "原营地火堆保留");
        Assert.That(t.Items.Count(i => i.ItemId is "field_tarp" or "stone" or "wood"), Is.EqualTo(s.Items.Count(i => i.ItemId is "field_tarp" or "stone" or "wood")), "不赠布或材料");

        // 建成的野外火堆读档保留（只一处），燃料随世界时钟
        var w = WithFire("field");
        var fac = w.Locations["field"].Facility;
        fac.FireLit = true;
        fac.FuelMinutes = 60;
        var u = SaveSerializer.Deserialize(SaveSerializer.Serialize(w), Cfg);
        Assert.That((E.HasFireAt(u, "field"), u.Locations["field"].Facility.FireBuilt), Is.EqualTo((true, true)));
        Assert.That(u.Log.Count(l => l.Text.Contains("野外火堆建成")), Is.EqualTo(1), "读档不重复建成");
        AdvanceTo(u, u.GameMinute + 60);
        Assert.That((u.Locations["field"].Facility.FireLit, u.Locations["field"].Facility.FuelMinutes), Is.EqualTo((false, 0)));
    }
}
