using Newtonsoft.Json.Linq;
using Survival.Core.Config;

namespace Survival.Core.Tests;

/// <summary>v0.4 结构化配置必须与策划交付的 v0.2 数值、交互与地图扩展 v0.3 整合配置逐项一致。</summary>
public class ConfigTests : TestBase
{
    private static JObject V02() =>
        JObject.Parse(File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "config.v0.2.json")));

    private static JObject V03() =>
        JObject.Parse(File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "design.v0.3.json")));

    private static GameConfig Fresh() =>
        ConfigLoader.FromJson(File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "config.v0.4.json")), validate: false);

    [Test]
    public void LoadsAndValidates()
    {
        Assert.That(ConfigLoader.Validate(Cfg), Is.Empty);
        Assert.That(Cfg.Items, Has.Count.EqualTo(18));
        Assert.That(Cfg.Locations, Has.Count.EqualTo(8));
        Assert.That(Cfg.Edges, Has.Count.EqualTo(8));
        Assert.That(Cfg.Resources, Has.Count.EqualTo(16));
        Assert.That(Cfg.Recipes, Has.Count.EqualTo(7));
    }

    [Test]
    public void Items_MatchV02()
    {
        foreach (var j in V02()["items"]!)
        {
            var it = Cfg.Items.Single(i => i.Id == (string)j["id"]!);
            Assert.That(it.Name, Is.EqualTo((string)j["name"]!), it.Id);
            Assert.That(it.WeightGrams, Is.EqualTo((int)j["weightGrams"]!), it.Id);
            Assert.That(it.StackMax, Is.EqualTo((int)j["stackMax"]!), it.Id);
            Assert.That(it.ShelfLifeMinutes, Is.EqualTo((int?)j["shelfLifeMinutes"]), it.Id);
        }
    }

    [Test]
    public void ItemEffects_MatchV02ItemActions()
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
        Assert.That(Use("bandage", u => u.Health), Is.EqualTo((double)a["bandageHealth"]!));
        Assert.That(Cfg.ItemActions.BandageCooldownMinutes, Is.EqualTo((int)a["bandageCooldownMinutes"]!));
        Assert.That(Cfg.ItemActions.DiarrheaMinutes, Is.EqualTo((int)a["diarrheaMinutes"]!));
        Assert.That(Cfg.ItemActions.DiarrheaExtraHydrationPerHour, Is.EqualTo((double)a["diarrheaExtraHydrationPerHour"]!));
        var tools = V02()["toolEffects"]!;
        Assert.That(Cfg.Items.Single(i => i.Id == "stone_knife").MaxDurability, Is.EqualTo((int)tools["stone_knife"]!["maxDurability"]!));
        Assert.That(Cfg.Items.Single(i => i.Id == "hatchet").MaxDurability, Is.EqualTo((int)tools["hatchet"]!["maxDurability"]!));
        Assert.That(Cfg.Resources.Single(r => r.NodeId == "wood").ToolBonus!.OutputMultiplier, Is.EqualTo((int)tools["hatchet"]!["woodOutput"]!));
        Assert.That(Cfg.Resources.Single(r => r.NodeId == "field_fiber").ToolBonus!.BaseMinutesOverride, Is.EqualTo((int)tools["stone_knife"]!["fiberBaseMinutes"]!));
        Assert.That(Cfg.Resources.Single(r => r.NodeId == "woods_fiber").ToolBonus!.BaseMinutesOverride, Is.EqualTo((int)tools["stone_knife"]!["fiberBaseMinutes"]!));
    }

    [Test]
    public void Resources_MatchV02()
    {
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
            Assert.That(r.Hazard?.Probability ?? 0, Is.EqualTo((double)j["hazardProbability"]!), r.NodeId);
            Assert.That(r.Hazard?.HealthLoss ?? 0, Is.EqualTo((double)j["hazardHealthLoss"]!), r.NodeId);
        }
    }

    [Test]
    public void Recipes_MatchV02()
    {
        foreach (var j in V02()["recipes"]!)
        {
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

    [Test]
    public void RatesAndRules_MatchV02()
    {
        var v = V02();
        foreach (var p in ((JObject)v["hourlyRates"]!).Properties())
        {
            var r = Cfg.HourlyRates[p.Name];
            Assert.That(r.Satiety, Is.EqualTo((double)p.Value["satiety"]!), p.Name);
            Assert.That(r.Hydration, Is.EqualTo((double)p.Value["hydration"]!), p.Name);
            Assert.That(r.Energy, Is.EqualTo((double)p.Value["energy"]!), p.Name);
            Assert.That(r.Wakefulness, Is.EqualTo((double)p.Value["wakefulness"]!), p.Name);
            Assert.That(r.Morale, Is.EqualTo((double)p.Value["morale"]!), p.Name);
        }
        var hr = v["healthRules"]!;
        Assert.That(Cfg.HealthRules.ZeroSatietyPerHour, Is.EqualTo((double)hr["zeroSatietyPerHour"]!));
        Assert.That(Cfg.HealthRules.ZeroHydrationPerHour, Is.EqualTo((double)hr["zeroHydrationPerHour"]!));
        Assert.That(Cfg.HealthRules.ZeroWakefulnessPerHour, Is.EqualTo((double)hr["zeroWakefulnessPerHour"]!));
        Assert.That(Cfg.HealthRules.ZeroEnergyWhileWorkingPerHour, Is.EqualTo((double)hr["zeroEnergyWhileWorkingPerHour"]!));
        Assert.That(Cfg.HealthRules.ShelteredRestRecoveryPerHour, Is.EqualTo((double)hr["shelteredRestRecoveryPerHour"]!));
        var init = v["initial_stats"]!;
        Assert.That(Cfg.InitialStats.Health, Is.EqualTo((double)init["health"]!));
        Assert.That(Cfg.InitialStats.Satiety, Is.EqualTo((double)init["satiety"]!));
        Assert.That(Cfg.InitialStats.Hydration, Is.EqualTo((double)init["hydration"]!));
        Assert.That(Cfg.InitialStats.Energy, Is.EqualTo((double)init["energy"]!));
        Assert.That(Cfg.InitialStats.Wakefulness, Is.EqualTo((double)init["wakefulness"]!));
        Assert.That(Cfg.InitialStats.Morale, Is.EqualTo((double)init["morale"]!));
        Assert.That(Cfg.Carry.SoftGrams, Is.EqualTo((int)v["carry_soft_kg"]! * 1000));
        Assert.That(Cfg.Carry.HardGrams, Is.EqualTo((int)v["carry_hard_kg"]! * 1000));
        Assert.That(Cfg.WorkFactors.Night, Is.EqualTo((double)v["night_time_factor"]!));
        Assert.That(Cfg.WorkFactors.Cap, Is.EqualTo((double)v["time_factor_cap"]!));
        var fe = v["facilityEffects"]!;
        Assert.That(Cfg.FacilityEffects.BedExtraSleepEnergyPerHour, Is.EqualTo((double)fe["bedExtraSleepEnergyPerHour"]!));
        Assert.That(Cfg.FacilityEffects.ShelteredCampRestMoralePerHour, Is.EqualTo((double)fe["shelteredCampRestMoralePerHour"]!));
        Assert.That(Cfg.Fire.MaxFuelMinutes, Is.EqualTo((int)fe["fireMaxFuelMinutes"]!));
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
        var clear = v["hourlyRates"]!["clear"]!;
        var r = Cfg.HourlyRates[Cfg.Actions.RateAliases["clear"]];
        Assert.That(r.Satiety, Is.EqualTo((double)clear["satiety"]!));
        Assert.That(r.Hydration, Is.EqualTo((double)clear["hydration"]!));
        Assert.That(r.Energy, Is.EqualTo((double)clear["energy"]!));
        Assert.That(r.Wakefulness, Is.EqualTo((double)clear["wakefulness"]!));
        Assert.That(r.Morale, Is.EqualTo((double)clear["morale"]!));
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
        var boil = Cfg.Recipes.Single(x => x.Id == "boil");
        Assert.That(boil.Pot!.InputItem, Is.EqualTo((string)d["boilInputType"]!));
        Assert.That(boil.Pot.OutputItem, Is.EqualTo((string)d["boilResultType"]!));
        Assert.That(boil.Pot.Units * pot.UnitMl, Is.EqualTo((int)d["boilMinimumMl"]!));
        Assert.That(boil.Pot.TransformInPlace, Is.True);
        Assert.That(Cfg.Recipes.Single(x => x.Id == "cook").PotMustBeEmpty, Is.True);
        Assert.That(Cfg.Fire.PotSlots, Is.EqualTo((int)v["facilityEffects"]!["firePotSlots"]!));
        Assert.That(Cfg.Ui.DragThresholdPx, Is.EqualTo((int)v["cardInteraction"]!["dragThresholdPx"]!));
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
        AssertInvalid(c => c.Recipes.Single(r => r.Id == "boil").Pot!.OutputItem = "berry", "容器可装物品");
    }
}
