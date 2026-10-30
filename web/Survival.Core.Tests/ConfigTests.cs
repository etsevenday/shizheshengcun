using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using Survival.Core.Config;

namespace Survival.Core.Tests;

/// <summary>
/// v0.6 结构化配置必须与策划交付的 v0.2 数值、交互与地图扩展 v0.3 整合配置、卡片统一与设施烹饪 v0.4 增量配置，
/// 以及人物与战斗修订参考 v0.6（基线 v0.5）参考配置逐项一致；v0.6 替换的旧数值改为对照 v0.6。
/// </summary>
public class ConfigTests : TestBase
{
    private static JObject Load(string name) =>
        JObject.Parse(File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, name)));

    private static JObject V02() => Load("config.v0.2.json");
    private static JObject V03() => Load("design.v0.3.json");
    private static JObject V04() => Load("design.v0.4.json");
    private static JObject V05() => Load("design.v0.5.json");
    private static JObject V06() => Load("design.v0.6.json");

    /// <summary>v0.4 增量里改过的食物（如熟块根改名“烤熟块根”）不再按 v0.2 原值核对。</summary>
    private static HashSet<string> ChangedByV04() =>
        new(V04()["foodChanges"]!.Where(f => (string)f["operation"]! == "update_existing").Select(f => (string)f["id"]!));

    private static GameConfig Fresh() =>
        ConfigLoader.FromJson(File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "config.v0.6.json")), validate: false);

    [Test]
    public void LoadsAndValidates()
    {
        Assert.That(ConfigLoader.Validate(Cfg), Is.Empty);
        Assert.That(Cfg.CombatEnabled, Is.True, "用户 2026-10-26 拍板：直接开启，不做开关");
        Assert.That(Cfg.Items, Has.Count.EqualTo(19 + 26 + 1 + 70), "v0.4 的19种加 v0.6 新增武器、弹药、尸体、肉、药与原料，加 v1.0 活性物质，加 B4 物理目录 70 件（A07/A12 physicalItems）");
        Assert.That(Cfg.Locations, Has.Count.EqualTo(8));
        Assert.That(Cfg.Edges, Has.Count.EqualTo(8));
        Assert.That(Cfg.Resources, Has.Count.EqualTo(16 + 4), "新增武器柜、旧储物柜、医药补给箱、废弃冷藏箱");
        Assert.That(Cfg.Recipes, Has.Count.EqualTo(5 + 5 + 3), "新增木矛、木弓、箭矢、工具维护、病毒特效药；v1.0 新增金属矛、弩、弩矢");
    }

    // ───────────── v0.2–v0.4 未被 v0.6 替换的部分 ─────────────

    [Test]
    public void Items_MatchV02()
    {
        var changed = ChangedByV04();
        foreach (var j in V02()["items"]!)
        {
            var it = Cfg.Items.Single(i => i.Id == (string)j["id"]!);
            if (changed.Contains(it.Id)) continue;
            Assert.That(it.Name, Is.EqualTo((string)j["name"]!), it.Id);
            Assert.That(it.WeightGrams, Is.EqualTo((int)j["weightGrams"]!), it.Id);
            Assert.That(it.StackMax, Is.EqualTo((int)j["stackMax"]!), it.Id);
            Assert.That(it.ShelfLifeMinutes, Is.EqualTo((int?)j["shelfLifeMinutes"]), it.Id);
        }
    }

    [Test]
    public void ItemEffects_MatchV02_WithV06Scaling()
    {
        var a = V02()["itemActions"]!;
        double Use(string id, Func<ItemUseConfig, double> f) => f(Cfg.Items.Single(i => i.Id == id).Use!);
        Assert.That(Use("clean_water", u => u.Hydration), Is.EqualTo((double)a["cleanWaterHydration"]!));
        Assert.That(Use("dirty_water", u => u.Hydration), Is.EqualTo((double)a["dirtyWaterHydration"]!));
        Assert.That(Use("canned_food", u => u.Satiety), Is.EqualTo((double)a["cannedFoodSatiety"]!));
        Assert.That(Use("berry", u => u.Satiety), Is.EqualTo((double)a["berrySatiety"]!));
        Assert.That(Use("berry", u => u.Hydration), Is.EqualTo((double)a["berryHydration"]!));
        Assert.That(Use("cooked_root", u => u.Satiety), Is.EqualTo((double)a["cookedRootSatiety"]!));
        Assert.That(Use("cooked_root", u => u.Morale), Is.EqualTo((double)a["cookedRootMorale"]!));
        // v0.6 §02：绷带基础恢复 5→1（×0.2），再乘医疗公式 1×(1+0.02L)
        var bandageAct = V06()["actions"]!.Single(x => (string)x["id"]! == "bandage");
        Assert.That(Use("bandage", u => u.Health), Is.EqualTo((double)a["bandageHealth"]! * (double)V06()["stateScale"]!["oldHealthAndEnergyConversionFactor"]!).Within(1e-9));
        Assert.That(Use("bandage", u => u.Health), Is.EqualTo(Formula.Num((string)bandageAct["healthFormula"]!, 0)));
        Assert.That(Cfg.ItemActions.BandageCooldownMinutes, Is.EqualTo((int)a["bandageCooldownMinutes"]!));
        Assert.That(Cfg.ItemActions.BandageCooldownMinutes, Is.EqualTo((int)bandageAct["healthCooldownMinutes"]!));
        Assert.That(Cfg.ItemActions.DiarrheaMinutes, Is.EqualTo((int)a["diarrheaMinutes"]!));
        Assert.That(Cfg.ItemActions.DiarrheaExtraHydrationPerHour, Is.EqualTo((double)a["diarrheaExtraHydrationPerHour"]!));
        var tools = V02()["toolEffects"]!;
        Assert.That(Cfg.Items.Single(i => i.Id == "stone_knife").LegacyMaxDurability, Is.EqualTo((int)tools["stone_knife"]!["maxDurability"]!), "旧档迁移用旧满耐久保持成色");
        Assert.That(Cfg.Items.Single(i => i.Id == "hatchet").LegacyMaxDurability, Is.EqualTo((int)tools["hatchet"]!["maxDurability"]!));
        Assert.That(Cfg.ToolEffects.SuccessfulActionWear, Is.EqualTo((int)tools["successfulActionWear"]!));
        Assert.That(Cfg.Resources.Single(r => r.NodeId == "wood").ToolBonus!.OutputMultiplier, Is.EqualTo((int)tools["hatchet"]!["woodOutput"]!));
        Assert.That(Cfg.Resources.Single(r => r.NodeId == "field_fiber").ToolBonus!.BaseMinutesOverride, Is.EqualTo((int)tools["stone_knife"]!["fiberBaseMinutes"]!));
        Assert.That(Cfg.Resources.Single(r => r.NodeId == "woods_fiber").ToolBonus!.BaseMinutesOverride, Is.EqualTo((int)tools["stone_knife"]!["fiberBaseMinutes"]!));
    }

    [Test]
    public void Resources_MatchV02_HazardsScaledByV06()
    {
        var env = V06()["survivalRecovery"]!["environmentalGatherDamageOverrides"]!;
        foreach (var j in V02()["resources"]!)
        {
            var r = Cfg.Resources.Single(x => x.NodeId == (string)j["nodeId"]!);
            Assert.That(r.LocationId, Is.EqualTo((string)j["locationId"]!), r.NodeId);
            Assert.That(r.BaseMinutes, Is.EqualTo((int)j["baseMinutes"]!), r.NodeId);
            Assert.That(r.Cap, Is.EqualTo((int?)j["cap"]), r.NodeId);
            Assert.That(r.DailyRefill, Is.EqualTo((int)j["dailyRefill"]!), r.NodeId);
            Assert.That(r.PoolMode, Is.EqualTo((string)j["poolMode"]!), r.NodeId);
            var reveal = (int)j["revealAt"]!;
            if (reveal < 0) Assert.That(r.RevealOnVisit, Is.True, r.NodeId);
            else Assert.That(r.RevealAt, Is.EqualTo(reveal), r.NodeId);
            var outs = ((JObject)j["outputs"]!).Properties().ToDictionary(p => p.Name, p => (int)p.Value);
            Assert.That(r.Outputs, Is.EquivalentTo(outs), r.NodeId);
            Assert.That(r.Hazard?.Probability ?? 0, Is.EqualTo((double)j["hazardProbability"]!), r.NodeId + " 概率不变");
            var old = (double)j["hazardHealthLoss"]!;
            var expect = old == 0 ? 0 : r.NodeId == "wood" ? (double)env["wood"]! : r.NodeId == "misc_box" ? (double)env["misc_box"]! : (double)env["scrapyard"]!;
            Assert.That(r.Hazard?.HealthLoss ?? 0, Is.EqualTo(expect), r.NodeId + " 环境伤害按 v0.6 改值");
            if (old > 0) Assert.That(r.Hazard!.HealthLoss, Is.EqualTo(old * 0.2).Within(1e-9), r.NodeId + " 即旧值×0.2");
        }
    }

    [Test]
    public void Recipes_MatchV02()
    {
        var retired = new[] { "boil", "cook" };
        foreach (var id in retired) Assert.That(Cfg.Recipes.Any(r => r.Id == id), Is.False, $"{id} 已改为设施自动加工");
        foreach (var j in V02()["recipes"]!)
        {
            if (retired.Contains((string)j["id"]!)) continue;
            var r = Cfg.Recipes.Single(x => x.Id == (string)j["id"]!);
            Assert.That(r.BaseMinutes, Is.EqualTo((int)j["baseMinutes"]!), r.Id);
            Assert.That(r.Inputs, Is.EquivalentTo(((JObject)j["inputs"]!).Properties().ToDictionary(p => p.Name, p => (int)p.Value)), r.Id);
            Assert.That(r.Outputs, Is.EquivalentTo(((JObject)j["outputs"]!).Properties().ToDictionary(p => p.Name, p => (int)p.Value)), r.Id);
            Assert.That(r.FacilityResult, Is.EqualTo((string?)j["facilityResult"]), r.Id);
            var cats = j["catalysts"]!.Select(x => (string)x!).ToList();
            Assert.That(r.Catalysts, Is.EquivalentTo(cats), r.Id);
        }
    }

    [Test]
    public void EdgesAndLocations_MatchV02()
    {
        var v = V02();
        foreach (var j in v["edges"]!)
        {
            var e = Cfg.Edges.Single(x => x.Id == (string)j["id"]!);
            Assert.That(e.BaseMinutes, Is.EqualTo((int)j["baseMinutes"]!), e.Id);
            Assert.That(new[] { e.A, e.B }, Is.EquivalentTo(new[] { (string)j["source"]!, (string)j["target"]! }), e.Id);
        }
        foreach (var j in v["locations"]!)
        {
            var l = Cfg.Locations.Single(x => x.Id == (string)j["id"]!);
            Assert.That(l.Name, Is.EqualTo((string)j["name"]!));
            Assert.That(l.InitialProgress, Is.EqualTo((int)j["initialProgress"]!));
            Assert.That(l.IsIndoor, Is.EqualTo((string)j["exposure"]! == "室内"));
        }
    }

    /// <summary>v0.6 §12：全部行为速率、恢复与归零损血改为 v0.6 survivalRates / survivalRecovery。</summary>
    [Test]
    public void RatesAndRules_MatchV06()
    {
        var v = V06();
        // 系统策划总册 v1.0 Q7（S12 §8）：非战斗负小时率=v0.6×0.8，0 与正向恢复不变；具体新表另由 V10ConfigTests 逐格核对
        static double Q7(double v06) => v06 < 0 ? v06 * 0.8 : v06;
        foreach (var p in ((JObject)v["survivalRates"]!).Properties())
        {
            var r = Cfg.HourlyRates[p.Name];
            Assert.That(r.Satiety, Is.EqualTo(Q7((double)p.Value["satiety"]!)).Within(1e-12), p.Name);
            Assert.That(r.Hydration, Is.EqualTo(Q7((double)p.Value["hydration"]!)).Within(1e-12), p.Name);
            Assert.That(r.Energy, Is.EqualTo(Q7((double)p.Value["energy"]!)).Within(1e-12), p.Name);
            Assert.That(r.Wakefulness, Is.EqualTo(Q7((double)p.Value["wakefulness"]!)).Within(1e-12), p.Name);
            Assert.That(r.Morale, Is.EqualTo(Q7((double)p.Value["morale"]!)).Within(1e-12), p.Name);
        }
        Assert.That(Cfg.HourlyRates.Keys, Is.EquivalentTo(((JObject)v["survivalRates"]!).Properties().Select(p => p.Name)));
        var rec = v["survivalRecovery"]!;
        Assert.That(Cfg.HealthRules.ZeroSatietyPerHour, Is.EqualTo((double)rec["zeroSatietyHealthPerHour"]!));
        Assert.That(Cfg.HealthRules.ZeroHydrationPerHour, Is.EqualTo((double)rec["zeroHydrationHealthPerHour"]!));
        Assert.That(Cfg.HealthRules.ZeroWakefulnessPerHour, Is.EqualTo((double)rec["zeroWakefulnessHealthPerHour"]!));
        Assert.That(Cfg.HealthRules.ZeroEnergyWhileWorkingPerHour, Is.EqualTo((double)v["worldTime"]!["zeroEnergyWorkingHealthPerHour"]!));
        Assert.That(Cfg.HealthRules.ShelteredRestRecoveryPerHour, Is.EqualTo((double)rec["shelteredHealthyHealthRecoveryPerHour"]!));
        Assert.That(Cfg.FacilityEffects.BedExtraSleepEnergyPerHour, Is.EqualTo((double)rec["bedSleepEnergyBonusPerHour"]!));
        Assert.That(Cfg.FacilityEffects.ShelteredCampRestMoralePerHour, Is.EqualTo((double)rec["shelteredRestMoralePerHour"]!));
        Assert.That(Cfg.FacilityEffects.LowMoraleEnergyRecoveryFactor, Is.EqualTo((double)rec["lowMoralePositiveEnergyFactor"]!));
        Assert.That(Cfg.ItemActions.DiarrheaExtraHydrationPerHour, Is.EqualTo((double)rec["diarrheaExtraHydrationPerHour"]!));
        Assert.That(Cfg.Carry.SoftGrams, Is.EqualTo((int)rec["carrySoftKg"]! * 1000));
        Assert.That(Cfg.Carry.HardGrams, Is.EqualTo((int)rec["carryHardKg"]! * 1000));
        // v0.2 求生规则中 v0.6 没改的部分
        var hr = V02()["healthRules"]!;
        Assert.That(Cfg.HealthRules.RecoveryMinSatiety, Is.EqualTo((double)hr["recoveryMinSatiety"]!));
        Assert.That(Cfg.HealthRules.RecoveryMinHydration, Is.EqualTo((double)hr["recoveryMinHydration"]!));
        Assert.That(Cfg.HealthRules.RecoveryMinMorale, Is.EqualTo((double)hr["recoveryMinMorale"]!));
        Assert.That(Cfg.WorkFactors.Night, Is.EqualTo((double)V02()["night_time_factor"]!));
        Assert.That(Cfg.WorkFactors.Cap, Is.EqualTo((double)V02()["time_factor_cap"]!));
        Assert.That(Cfg.Fire.MaxFuelMinutes, Is.EqualTo((int)V02()["facilityEffects"]!["fireMaxFuelMinutes"]!));
    }

    /// <summary>v0.6 §02 stateScale：健康/体力上限20、初值20/18，提醒线 25→5、20→4；其余初值不变。</summary>
    [Test]
    public void StateScale_MatchV06()
    {
        var sc = V06()["stateScale"]!;
        Assert.That(Cfg.InitialStats.Health, Is.EqualTo((double)sc["initialHealth"]!));
        Assert.That(Cfg.InitialStats.Energy, Is.EqualTo((double)sc["initialEnergy"]!));
        Assert.That(Cfg.InitialStats.Satiety, Is.EqualTo((double)sc["initialSatiety"]!));
        Assert.That(Cfg.InitialStats.Hydration, Is.EqualTo((double)sc["initialHydration"]!));
        Assert.That(Cfg.InitialStats.Wakefulness, Is.EqualTo((double)sc["initialWakefulness"]!));
        Assert.That(Cfg.InitialStats.Morale, Is.EqualTo((double)sc["initialMorale"]!));
        Assert.That(Cfg.StateScale.HealthMax, Is.EqualTo((double)sc["healthMax"]!));
        Assert.That(Cfg.StateScale.EnergyMax, Is.EqualTo((double)sc["energyMax"]!));
        Assert.That(Cfg.Thresholds["health"].Value, Is.EqualTo((double)sc["healthCritical"]!));
        Assert.That(Cfg.Thresholds["health"].Inclusive, Is.True, "健康高危仍为“降至”（含）");
        Assert.That(Cfg.Thresholds["energy"].Value, Is.EqualTo((double)sc["lowEnergyExclusive"]!));
        Assert.That(Cfg.Thresholds["energy"].Inclusive, Is.False, "低体力线仍为“低于”（不含）");
        var old = V02()["thresholds"]!;
        Assert.That(Cfg.Thresholds["health"].Value, Is.EqualTo((double)old["healthCritical"]! * 0.2).Within(1e-9));
        Assert.That(Cfg.Thresholds["energy"].Value, Is.EqualTo((double)old["lowEnergyExclusive"]! * 0.2).Within(1e-9));
        Assert.That(Cfg.Thresholds["satiety"].Value, Is.EqualTo((double)old["satietyWarning"]!), "不缩放");
        Assert.That(Cfg.Thresholds["hydration"].Value, Is.EqualTo((double)old["hydrationWarning"]!), "不缩放");
        Assert.That(Cfg.Thresholds["wakefulness"].Value, Is.EqualTo((double)old["wakefulnessWarning"]!), "不缩放");
        Assert.That(Cfg.Thresholds["morale"].Value, Is.EqualTo((double)old["lowMoraleExclusive"]!), "不缩放");
    }

    [Test]
    public void StartingConditions_MatchV02()
    {
        var v = V02();
        Assert.That(Cfg.StartingCarried, Is.EquivalentTo(((JObject)v["starting_inventory"]!).Properties().ToDictionary(p => p.Name, p => (int)p.Value)));
        Assert.That(Cfg.StartingCampGround, Is.EquivalentTo(((JObject)v["starting_camp_ground"]!).Properties().ToDictionary(p => p.Name, p => (int)p.Value)));
        var s = New();
        Assert.That(s.GameMinute, Is.EqualTo(480));
        Assert.That(s.CurrentLocation, Is.EqualTo("camp"));
        Assert.That(Count(s, "pot", "carried"), Is.EqualTo(1));
        Assert.That(Count(s, "canned_food", "camp"), Is.EqualTo(2));
        Assert.That(s.Locations["camp"].Facility.FireLit, Is.False);
    }

    [Test]
    public void LootTables_MatchV03Design()
    {
        var d = (JObject)V03()["lootTables"]!;
        Assert.That(Cfg.LootTables.Keys, Is.EquivalentTo(d.Properties().Select(p => p.Name)));
        foreach (var p in d.Properties())
        {
            var t = Cfg.LootTables[p.Name];
            Assert.That(t.Version, Is.EqualTo((string)p.Value["version"]!), p.Name);
            var entries = p.Value["entries"]!.ToList();
            Assert.That(t.Entries, Has.Count.EqualTo(entries.Count), p.Name);
            for (var i = 0; i < entries.Count; i++)
            {
                Assert.That(t.Entries[i].ItemId, Is.EqualTo((string?)entries[i]["itemId"]), $"{p.Name}[{i}] 顺序与物品");
                Assert.That(t.Entries[i].Count, Is.EqualTo((int)entries[i]["count"]!), $"{p.Name}[{i}]");
                Assert.That(t.Entries[i].Weight, Is.EqualTo((int)entries[i]["weight"]!), $"{p.Name}[{i}]");
            }
            Assert.That(t.Entries.Sum(x => x.Weight), Is.EqualTo(100), p.Name);
        }
    }

    [Test]
    public void LocationsClear_MatchV03Design()
    {
        foreach (var j in V03()["locations"]!)
        {
            var l = Cfg.Locations.Single(x => x.Id == (string)j["id"]!);
            Assert.That(l.LootTableId, Is.EqualTo((string)j["lootTableId"]!), l.Id);
            Assert.That(l.Clear.Cap, Is.EqualTo((int)j["clear"]!["cap"]!), l.Id);
            Assert.That(l.Clear.InitialCharges, Is.EqualTo((int)j["clear"]!["initialCharges"]!), l.Id);
            Assert.That(l.Clear.RefillIntervalMinutes, Is.EqualTo((int)j["clear"]!["refillIntervalMinutes"]!), l.Id);
        }
        var v = V03();
        Assert.That(Cfg.Actions.ClearMinutes, Is.EqualTo((int)v["clearAction"]!["baseMinutes"]!));
        Assert.That(Cfg.Actions.ExploreMinutes, Is.EqualTo((int)v["exploreAction"]!["baseMinutes"]!));
        Assert.That(Cfg.Actions.ExploreProgress, Is.EqualTo((int)v["exploreAction"]!["progressDelta"]!));
        Assert.That(Cfg.Actions.WorkActionTypes, Does.Contain("clear"));
    }

    [Test]
    public void Container_MatchV03Design()
    {
        var v = V03();
        var d = v["containerDefinitions"]!["pot"]!;
        var pot = Cfg.Containers["pot"];
        Assert.That(pot.CapacityMl, Is.EqualTo((int)d["capacityMl"]!));
        Assert.That(pot.UnitMl, Is.EqualTo((int)d["transferQuantumMl"]!));
        Assert.That(pot.Accepts, Is.EquivalentTo(d["accepts"]!.Select(x => (string)x!)));
        Assert.That(Cfg.Items.Single(i => i.Id == "pot").ContainerId, Is.EqualTo("pot"));
        Assert.That(Cfg.Items.Single(i => i.Id == "pot").WeightGrams, Is.EqualTo(1000));
        foreach (var id in new[] { "dirty_water", "clean_water" })
            Assert.That(Cfg.Items.Single(i => i.Id == id).UnitVolumeMl, Is.EqualTo(500), id);
        var boil = Cfg.Processes.Single(x => x.Id == "boil_water");
        Assert.That(boil.InputId, Is.EqualTo((string)d["boilInputType"]!));
        Assert.That(boil.OutputId, Is.EqualTo((string)d["boilResultType"]!));
        Assert.That(boil.AllowedBatchMl, Does.Contain(pot.UnitMl));
        Assert.That(Cfg.Fire.MaxPots, Is.EqualTo((int)v["facilityEffects"]!["firePotSlots"]!));
        Assert.That(Cfg.Ui.DragThresholdPx, Is.EqualTo((int)v["cardInteraction"]!["dragThresholdPx"]!));
    }

    [Test]
    public void FoodChanges_MatchV04Delta()
    {
        foreach (var f in V04()["foodChanges"]!)
        {
            var id = (string)f["id"]!;
            var it = Cfg.Items.Single(i => i.Id == id);
            Assert.That(it.Name, Is.EqualTo((string)f["name"]!), id);
            Assert.That(it.WeightGrams, Is.EqualTo((int)f["weightGrams"]!), id);
            Assert.That(it.StackMax, Is.EqualTo((int)f["stackMax"]!), id);
            Assert.That(it.ShelfLifeMinutes, Is.EqualTo((int)f["shelfLifeMinutes"]!), id);
            var fx = f["immediateEffects"]!;
            Assert.That(it.Use!.Satiety, Is.EqualTo((double)fx["satiety"]!), id);
            Assert.That(it.Use.Hydration, Is.EqualTo((double)fx["hydration"]!), id);
            Assert.That(it.Use.Morale, Is.EqualTo((double)fx["morale"]!), id);
            Assert.That(it.Use.Health, Is.EqualTo(0), id + " 健康不直接变化");
            Assert.That(Cfg.ItemActions.UseMinutes, Is.EqualTo((int)f["useMinutes"]!), id);
            Assert.That(Cfg.Actions.RateAliases["use"], Is.EqualTo((string)f["useRateCategory"]!), id);
        }
    }

    /// <summary>火堆与锅（v0.4）；锅食材白名单按 v0.5 §14 新增生肉及两种成品。</summary>
    [Test]
    public void FireAndPot_MatchV04Delta_PlusMeat()
    {
        var v = V04();
        var fire = v["fire"]!;
        Assert.That(Cfg.Fire.ProcessingSlots, Is.EqualTo((int)fire["processingSlotCount"]!));
        Assert.That(Cfg.Fire.MaxPots, Is.EqualTo((int)fire["maxPots"]!));
        Assert.That(Cfg.Fire.DirectFoodInputIds, Is.EqualTo(fire["directFoodInputIds"]!.Select(x => (string)x!).Append("raw_meat")));
        var keep = v["retain"]!;
        Assert.That(Cfg.Fire.BurnMinutesPerWood, Is.EqualTo((int)keep["fuelMinutesPerWood"]!));
        Assert.That(Cfg.Fire.MaxFuelMinutes, Is.EqualTo((int)keep["fuelCapMinutes"]!));
        Assert.That(Cfg.Fire.IgniteMinutes, Is.EqualTo((int)keep["igniteMinutes"]!));
        var pot = v["pot"]!;
        var ct = Cfg.Containers[Cfg.Items.Single(i => i.Id == (string)pot["itemId"]!).ContainerId!];
        Assert.That(ct.CapacityMl, Is.EqualTo((int)pot["liquidCapacityMl"]!));
        Assert.That(ct.UnitMl, Is.EqualTo((int)pot["transferQuantumMl"]!));
        Assert.That(ct.FoodSlots, Is.EqualTo((int)pot["foodSlots"]!), "新增肉类不改变2个食材位");
        Assert.That(ct.Accepts, Is.EquivalentTo(pot["acceptedLiquids"]!.Select(x => (string)x!)));
        Assert.That(ct.StoredFoodIds, Is.EquivalentTo(pot["storedFoodIds"]!.Select(x => (string)x!).Concat(new[] { "raw_meat", "cooked_meat", "boiled_meat" })));
    }

    [Test]
    public void Processes_MatchV04Delta_PlusV06Meat()
    {
        var d = V04()["processes"]!.ToList();
        var meat = V06()["passiveCookingExtensions"]!.ToList();
        // v1.0 T03 追加烤肉复煮
        Assert.That(Cfg.Processes.Select(p => p.Id), Is.EqualTo(d.Select(x => (string)x["id"]!).Concat(meat.Select(x => (string)x["id"]!)).Concat(new[] { "reboil_cooked_meat" })));
        foreach (var j in d)
        {
            var p = Cfg.Processes.Single(x => x.Id == (string)j["id"]!);
            Assert.That(p.Target, Is.EqualTo((string)j["target"]!), p.Id);
            Assert.That(p.InputId, Is.EqualTo((string)j["inputId"]!), p.Id);
            Assert.That(p.OutputId, Is.EqualTo((string)j["outputId"]!), p.Id);
            Assert.That(p.HeatMinutes, Is.EqualTo((int)j["heatMinutes"]!), p.Id);
            Assert.That(p.WaterCostMl, Is.EqualTo((int?)j["waterCostMl"] ?? 0), p.Id);
            Assert.That(p.RequiredLiquid, Is.EqualTo((string?)j["requiredLiquid"]), p.Id);
        }
        foreach (var j in meat)
        {
            var p = Cfg.Processes.Single(x => x.Id == (string)j["id"]!);
            Assert.That(p.InputId, Is.EqualTo((string)j["inputId"]!), p.Id);
            Assert.That(p.OutputId, Is.EqualTo((string)j["outputId"]!), p.Id);
            Assert.That(p.HeatMinutes, Is.EqualTo((int)j["heatMinutes"]!), p.Id);
            Assert.That(p.WaterCostMl, Is.EqualTo((int)j["waterCostMl"]!), p.Id);
            Assert.That(p.RequiredLiquid, Is.EqualTo((string?)j["requiredLiquid"]), p.Id);
            Assert.That(p.InPot, Is.EqualTo((string)j["outputOwner"]! == "same_pot"), p.Id);
        }
    }

    // ───────────── v0.6 / v0.5 新增段落 ─────────────

    private static readonly JsonSerializer Camel = JsonSerializer.Create(new JsonSerializerSettings
    {
        ContractResolver = new CamelCasePropertyNamesContractResolver(),
        NullValueHandling = NullValueHandling.Include,
    });

    /// <summary>
    /// 策划段落里的每个字段都要在加载后的配置里原样存在（Newtonsoft 会静默丢弃未映射字段，这里逐字段兜底）。
    /// 数字按数值比较，数字与字符串按文本比较（如 ap 1 与 "remaining"）。
    /// </summary>
    private static void AssertSubset(JToken design, JToken mapped, string path)
    {
        switch (design)
        {
            case JObject o:
                Assert.That(mapped, Is.InstanceOf<JObject>(), path);
                foreach (var p in o.Properties())
                {
                    var m = ((JObject)mapped)[p.Name] ?? ((JObject)mapped).Properties().FirstOrDefault(x => string.Equals(x.Name, p.Name, StringComparison.OrdinalIgnoreCase))?.Value;
                    Assert.That(m, Is.Not.Null, $"{path}.{p.Name} 没有映射到 GameConfig（会被静默丢弃）");
                    AssertSubset(p.Value, m!, $"{path}.{p.Name}");
                }
                break;
            case JArray a:
                Assert.That(mapped, Is.InstanceOf<JArray>(), path);
                Assert.That(((JArray)mapped).Count, Is.EqualTo(a.Count), path);
                for (var i = 0; i < a.Count; i++) AssertSubset(a[i], ((JArray)mapped)[i], $"{path}[{i}]");
                break;
            default:
                var dv = (JValue)design;
                var mv = (JValue)mapped;
                if (dv.Type is JTokenType.Integer or JTokenType.Float && mv.Type is JTokenType.Integer or JTokenType.Float)
                    Assert.That((double)mv, Is.EqualTo((double)dv), path);
                else if (dv.Type == JTokenType.Null)
                    Assert.That(mv.Type, Is.EqualTo(JTokenType.Null), path);
                else
                    Assert.That(Convert.ToString(mv.Value, System.Globalization.CultureInfo.InvariantCulture),
                        Is.EqualTo(Convert.ToString(dv.Value, System.Globalization.CultureInfo.InvariantCulture)), path);
                break;
        }
    }

    private static readonly (string design, string config)[] CopiedSections =
    {
        ("scope", "scope"), ("skills", "skills"), ("weapons", "weapons"), ("actions", "combatActions"), ("enemies", "enemies"),
        ("xp", "xp"), ("crafting", "crafting"), ("combatMath", "combatMath"), ("panic", "panic"), ("retreat", "retreat"),
        ("ammo", "ammo"), ("wear", "wear"), ("noise", "noise"), ("statuses", "statuses"), ("worldTime", "battleTime"),
        ("encounters", "encounters"), ("animalCarcasses", "animalCarcasses"), ("skillGrowth", "skillGrowth"),
        ("durability", "durability"), ("immunity", "immunity"), ("infection", "infection"), ("worldEnemies", "worldEnemies"),
        ("stateScale", "stateScale"),
    };

    [Test]
    public void V06Sections_AllFieldsMapped_ValuesEqual()
    {
        var mapped = JObject.FromObject(Cfg, Camel);
        var d = V06();
        // 系统策划总册 v1.0 替换的 v0.6 字段（P02 锅无耐久、Q3 取消固定受惊+10、P01 逐目标经验），新值另由 V10_ 用例核对
        ((JObject)d["durability"]!["tools"]!).Remove("pot");
        ((JObject)d["panic"]!).Remove("surpriseBonus");
        ((JObject)d["xp"]!).Remove("groupAttackXpIsSingleCommand");
        // 策划提交 f172677 B3（A10 §4.2、A05 §5、R02 §5）：旧“换武器1AP、从随身取”改为两个武器页 0AP 切换（穿戴槽）；新值由 B3EquipTests 核对
        var sw = (JObject)((JArray)d["actions"]!).First(x => (string?)x["id"] == "switch_weapon");
        sw.Remove("ap");
        sw.Remove("sourceOwner");

        // Q7（R04 §8）：免疫正恢复×2、取消已感染免疫惩罚、高免疫门槛 85→80；新值由 V10ConfigTests.Q7_ImmunityInfection_MatchR04_8 核对
        foreach (var k in new[] { "healthyBonusPerHour", "shelteredSleepPerHour", "exposedSleepPerHour", "shelteredRestPerHour", "exposedRestPerHour" })
            ((JObject)d["immunity"]!).Remove(k);
        ((JObject)d["immunity"]!["penaltiesPerHour"]!).Remove("virusInfected");
        ((JObject)d["infection"]!).Remove("progressPerHour");
        ((JObject)d["infection"]!["naturalCure"]!).Remove("consecutiveImmunityAtLeast");
        foreach (var (ds, cs) in CopiedSections)
            AssertSubset(d[ds]!, mapped[cs]!, $"v0.6.{ds}");
        // 丧尸掉落表（v0.6 沿用 v0.5）
        AssertSubset(d["zombieLoot"]!, mapped["zombieLoot"]!, "v0.6.zombieLoot");
        AssertSubset(V05()["zombieLoot"]!, mapped["zombieLoot"]!, "v0.5.zombieLoot");
    }

    [Test]
    public void V06NewItems_MatchDesign()
    {
        foreach (var j in V06()["newItems"]!)
        {
            var id = (string)j["id"]!;
            var it = Cfg.Items.Single(i => i.Id == id);
            Assert.That(it.Name, Is.EqualTo((string)j["name"]!), id);
            Assert.That(it.WeightGrams, Is.EqualTo((int)j["weightGrams"]!), id);
            Assert.That(it.StackMax, Is.EqualTo((int)j["stackMax"]!), id);
            Assert.That(it.ShelfLifeMinutes, Is.EqualTo((int?)j["shelfLifeMinutes"]), id);
            if (j["durabilityMaxRange"] is JArray r) Assert.That(it.DurabilityMaxRange, Is.EqualTo(r.Select(x => (double)x)), id);
            if (j["immediateEffects"] is JObject fx)
            {
                Assert.That(it.Use!.Satiety, Is.EqualTo((double)fx["satiety"]!), id);
                Assert.That(it.Use.Hydration, Is.EqualTo((double)fx["hydration"]!), id);
                Assert.That(it.Use.Morale, Is.EqualTo((double)fx["morale"]!), id);
            }
            if (j["usable"] is JValue u && !(bool)u) Assert.That(it.Use, Is.Null, id + " 不可直接食用");
        }
        // v0.5 新物品（v0.6 未覆盖的）也一致
        foreach (var j in V05()["newItems"]!)
        {
            var it = Cfg.Items.Single(i => i.Id == (string)j["id"]!);
            Assert.That(it.WeightGrams, Is.EqualTo((int)j["weightGrams"]!), it.Id);
            Assert.That(it.ShelfLifeMinutes, Is.EqualTo((int?)j["shelfLifeMinutes"]), it.Id);
        }
        // 工具随机耐久（v0.6 §05）
        var tools = V06()["durability"]!["tools"]!;
        // v1.0 P02：金属锅取消耐久
        Assert.That(Cfg.Items.Single(i => i.Id == "pot").DurabilityMaxRange, Is.Null);
        Assert.That(Cfg.Items.Single(i => i.Id == "lighter").DurabilityMaxRange, Is.EqualTo(tools["lighter"]!["maxRange"]!.Select(x => (double)x)));
        foreach (var w in V06()["weapons"]!.Where(w => w["durabilityMaxRange"]!.Type == JTokenType.Array))
            Assert.That(Cfg.Items.Single(i => i.Id == (string)w["id"]!).DurabilityMaxRange, Is.EqualTo(w["durabilityMaxRange"]!.Select(x => (double)x)), (string)w["id"]!);
    }

    [Test]
    public void V06Recipes_Caches_MatchDesign()
    {
        foreach (var j in V06()["newRecipes"]!)
        {
            var r = Cfg.Recipes.Single(x => x.Id == (string)j["id"]!);
            Assert.That(r.BaseMinutes, Is.EqualTo((int)j["baseMinutes"]!), r.Id);
            Assert.That(r.Inputs, Is.EquivalentTo(((JObject)j["inputs"]!).Properties().ToDictionary(p => p.Name, p => (int)p.Value)), r.Id);
            Assert.That(r.Outputs, Is.EquivalentTo(((JObject)j["outputs"]!).Properties().ToDictionary(p => p.Name, p => (int)p.Value)), r.Id);
            if (j["minSkills"] is JObject ms) Assert.That(r.MinSkills, Is.EquivalentTo(ms.Properties().ToDictionary(p => p.Name, p => (int)p.Value)), r.Id);
            if (j["requirement"] != null) Assert.That(r.Requirement?.Facility, Is.EqualTo((string)j["requirement"]!), r.Id);
            if (j["potCatalyst"] != null) Assert.That(r.PotCatalyst, Is.EqualTo((string)j["potCatalyst"]!), r.Id);
            if (j["restoreOwnDurabilityMaxFraction"] != null) Assert.That(r.RestoreOwnDurabilityMaxFraction, Is.EqualTo((double)j["restoreOwnDurabilityMaxFraction"]!), r.Id);
        }
        var rep = V06()["durability"]!["repair"]!;
        var repair = Cfg.Recipes.Single(r => r.IsRepair);
        Assert.That(repair.BaseMinutes, Is.EqualTo((int)rep["baseMinutes"]!));
        Assert.That(repair.Inputs, Is.EquivalentTo(((JObject)rep["inputs"]!).Properties().ToDictionary(p => p.Name, p => (int)p.Value)));
        Assert.That(repair.RestoreOwnDurabilityMaxFraction, Is.EqualTo((double)rep["restoreFractionOfOwnMax"]!));
        // v0.5 配方（木矛、木弓、箭矢）一致
        foreach (var j in V05()["newRecipes"]!)
        {
            var r = Cfg.Recipes.Single(x => x.Id == (string)j["id"]!);
            Assert.That(r.BaseMinutes, Is.EqualTo((int)j["baseMinutes"]!), r.Id);
        }
        foreach (var j in V06()["newCaches"]!)
        {
            var r = Cfg.Resources.Single(x => x.NodeId == (string)j["id"]!);
            Assert.That(r.LocationId, Is.EqualTo((string)j["locationId"]!), r.NodeId);
            Assert.That(r.RevealAt, Is.EqualTo((int)j["revealAt"]!), r.NodeId);
            Assert.That(r.BaseMinutes, Is.EqualTo((int)j["baseMinutes"]!), r.NodeId);
            Assert.That(r.Cap, Is.EqualTo((int)j["initialUses"]!), r.NodeId);
            Assert.That(r.IsCache, Is.True, r.NodeId + " 一次性，不补充");
            Assert.That(r.Outputs, Is.EquivalentTo(((JObject)j["outputs"]!).Properties().ToDictionary(p => p.Name, p => (int)p.Value)), r.NodeId);
        }
        Assert.That(Cfg.Resources.Single(r => r.NodeId == "village_weapon_locker").Name, Is.EqualTo("旧储物柜"));
        foreach (var j in V06()["medicineCaches"]!)
        {
            var r = Cfg.Resources.Single(x => x.NodeId == (string)j["id"]!);
            Assert.That(r.LocationId, Is.EqualTo((string)j["locationId"]!), r.NodeId);
            Assert.That(r.RevealAt, Is.EqualTo((int)j["revealAt"]!), r.NodeId);
            Assert.That(r.Cap, Is.EqualTo((int)j["uses"]!), r.NodeId);
            Assert.That(r.BaseMinutes, Is.EqualTo((int)j["baseMinutes"]!), r.NodeId);
            if (j["outputs"] is JObject o) Assert.That(r.Outputs, Is.EquivalentTo(o.Properties().ToDictionary(p => p.Name, p => (int)p.Value)), r.NodeId);
            if (j["independentRareCureChance"] != null) Assert.That(r.IndependentRare!.Chance, Is.EqualTo((double)j["independentRareCureChance"]!), r.NodeId);
            if (j["exclusiveLoot"] is JArray ex)
            {
                Assert.That(r.ExclusiveLoot!.Select(x => (x.ItemId, x.Count, x.Weight)),
                    Is.EqualTo(ex.Select(x => ((string)x["itemId"]!, (int)x["count"]!, (int)x["weight"]!))), r.NodeId);
                Assert.That(r.Outputs, Is.Empty, r.NodeId);
            }
        }
    }

    /// <summary>公式字符串里的系数被正确读出（验证记录 v0.6“技能成长样例”）。</summary>
    [Test]
    public void V06Formulas_Coefficients()
    {
        Assert.That(Cfg.SkillGrowth.DamagePerLevel, Is.EqualTo(0.035));
        Assert.That(Cfg.SkillGrowth.AccuracyPerLevel, Is.EqualTo(3));
        Assert.That(Cfg.SkillGrowth.EnergyPerLevel, Is.EqualTo(0.025));
        Assert.That(Cfg.SkillGrowth.MedicalHealPerLevel, Is.EqualTo(0.02));
        Assert.That(Cfg.Crafting.PerLevel, Is.EqualTo(0.025));
        Assert.That(Cfg.CombatMath.CritAccuracyPerLevel, Is.EqualTo(0.3));
        Assert.That(Cfg.CombatMath.CritDenominator, Is.EqualTo(100));
        Assert.That(Cfg.CombatMath.MinEffectiveSpeed, Is.EqualTo(0.1));
        Assert.That((Cfg.CombatMath.InitiativeRollMin, Cfg.CombatMath.InitiativeRollMax), Is.EqualTo((1, 4)));
        Assert.That(Cfg.CombatMath.GuardFactor, Is.EqualTo(0.5));
        Assert.That(Cfg.Skills.All(s => s.XpBase == 50 && s.XpOffset == 1 && s.LevelCap == null), Is.True, "50×(L+1)，无等级上限");
        Assert.That((Cfg.Xp.CraftDivisor, Cfg.Xp.CraftMin, Cfg.Xp.CraftMax), Is.EqualTo((10.0, 1.0, 12.0)));
        Assert.That((Cfg.Panic.InitialBase, Cfg.Panic.InitialDivisor, Cfg.Panic.DamageGainFactor), Is.EqualTo((100.0, 2.0, 1.5)));
        Assert.That((Cfg.Panic.MoraleLossMax, Cfg.Panic.MoraleLossDivisor), Is.EqualTo((5.0, 20.0)));
        Assert.That((Cfg.Retreat.SpeedDivisor, Cfg.Retreat.Offset), Is.EqualTo((3.0, 0.7)));
        Assert.That((Cfg.Infection.ExposureBase, Cfg.Infection.ExposureScale, Cfg.Infection.ExposureImmunityDivisor), Is.EqualTo((0.05, 0.55, 100.0)));
        Assert.That((Cfg.Infection.Bandage.Base, Cfg.Infection.Bandage.PerLevel), Is.EqualTo((2.0, 0.01)));
        Assert.That(Cfg.Weapons.Single(w => w.Id == "wooden_spear").RearDamageFactor, Is.EqualTo(0.75));
        Assert.That(Cfg.Weapons.Single(w => w.Id == "hatchet").RearDamageFactor, Is.EqualTo(1));
        Assert.That(Cfg.CombatActions.Single(a => a.Id == "end_turn").ApCost, Is.EqualTo(-1), "结束行动 ap=remaining");
        Assert.That(Cfg.CombatActions.Any(a => a.Id == "aim"), Is.False, "v0.6 彻底移除瞄准");
    }

    [Test]
    public void Validate_RejectsBrokenProcesses()
    {
        AssertInvalid(c => c.Processes[0].InputId = "gold", "输入物品 gold 不存在");
        AssertInvalid(c => c.Processes[1].OutputId = "gold", "输出物品 gold 不存在");
        AssertInvalid(c => c.Processes[1].HeatMinutes = 0, "受热分钟");
        AssertInvalid(c => c.Processes[1].HeatMinutes = 7, "受热分钟");
        AssertInvalid(c => c.Processes[2].OutputId = "cooked_root", "锅白名单");
        AssertInvalid(c => c.Processes[1].InputId = "berry", "直烤白名单");
        AssertInvalid(c => c.Processes[0].OutputId = "berry", "容器可装物品");
        AssertInvalid(c => c.Fire.MaxPots = 3, "锅数上限");
        AssertInvalid(c => c.Containers["pot"].StoredFoodIds.Add("gold"), "可存放食材 gold 不存在");
    }

    private static void AssertInvalid(Action<GameConfig> breakIt, string expect)
    {
        var c = Fresh();
        breakIt(c);
        var errors = ConfigLoader.Validate(c);
        Assert.That(errors.Any(x => x.Contains(expect)), Is.True, $"应报“{expect}”，实际：{string.Join("；", errors)}");
        Assert.Throws<ConfigException>(() => ConfigLoader.FromJson(Newtonsoft.Json.JsonConvert.SerializeObject(c)));
    }

    [Test]
    public void Validate_RejectsBrokenLootAndClear()
    {
        AssertInvalid(c => c.Locations[0].LootTableId = "", "缺少 lootTableId");
        AssertInvalid(c => c.Locations[0].LootTableId = "nope", "不存在");
        AssertInvalid(c => c.LootTables["loose_woods"].Entries[1].Weight = 31, "权重合计须为100");
        AssertInvalid(c => { var t = c.LootTables["loose_woods"].Entries; t[1].Weight = -5; t[2].Weight = 60; }, "权重不能为负");
        AssertInvalid(c => c.LootTables["loose_woods"].Entries[1].ItemId = "gold", "不存在的物品 gold");
        AssertInvalid(c => c.LootTables["loose_woods"].Entries[1].Count = 2, "数量须为1");
        AssertInvalid(c => c.LootTables["loose_woods"].Entries[0].Count = 1, "空结果不能带数量");
        AssertInvalid(c => c.Locations[0].Clear.Cap = 0, "上限须≥1");
        AssertInvalid(c => c.Locations[0].Clear.RefillIntervalMinutes = 7, "恢复间隔");
        AssertInvalid(c => c.Locations[0].Clear.InitialCharges = 9, "初始清理次数");
    }

    [Test]
    public void Validate_RejectsBrokenContainer()
    {
        AssertInvalid(c => c.Containers["pot"].CapacityMl = 1200, "整数倍");
        AssertInvalid(c => c.Containers["pot"].Accepts.Add("wood"), "每份体积");
        AssertInvalid(c => c.Items.Single(i => i.Id == "pot").ContainerId = "kettle", "容器定义 kettle 不存在");
    }

    [Test]
    public void Validate_RejectsBrokenCombat()
    {
        AssertInvalid(c => c.Weapons.Single(w => w.Id == "pistol").AmmoId = "gold", "弹药 gold 不存在");
        AssertInvalid(c => c.Weapons.Single(w => w.Id == "hatchet").SkillId = "magic", "技能 magic 不存在");
        AssertInvalid(c => c.Items.Single(i => i.Id == "hatchet").DurabilityMaxRange = new double[] { 30, 20 }, "最大耐久区间无效");
        AssertInvalid(c => c.WorldEnemies.Initial["field"].Add("dragon"), "不存在的敌人 dragon");
        AssertInvalid(c => c.WorldEnemies.Pools["woods"]["common"].Add(new WeightedEnemy { EnemyId = "boar", Weight = 1 }), "不属于 common 层");
        AssertInvalid(c => c.WorldEnemies.Initial["village"].AddRange(new[] { "shambler", "shambler", "shambler" }), "超过本地上限");
        AssertInvalid(c => c.SkillGrowth.DamageFactor = "1+0.035*L*L", "skillGrowth.damageFactor 的结构变了");
        AssertInvalid(c => c.Infection.ExposureProbability = "0.05+0.55*immunity", "infection.exposureProbability 的结构变了");
        AssertInvalid(c => c.Recipes.Single(r => r.IsRepair).TargetInstanceRequired = false, "须指定目标实例");
        AssertInvalid(c => c.Resources.Single(r => r.NodeId == "village_cold_box").ExclusiveLoot![0].Weight = 30, "互斥抽取权重合计须为100");
    }
}
