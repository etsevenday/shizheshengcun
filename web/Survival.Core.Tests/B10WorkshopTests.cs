using Survival.Core.Config;
using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 B10：工坊六类设施（A07「工坊设施」）、配方 station/knowledge、装备配方与来源（A05 §3）、材料加工链（A07）、队员交接（A10 §7.2）。
/// </summary>
public class B10WorkshopTests : TestBase
{
    private static List<RecipeConfig> Equip => Cfg.Recipes.Where(r => r.Group == "equipment").ToList();

    private static ActionRequest Craft(string id, int batch = 1) => ActionRequest.Craft(id, null, null, batch);

    private void Give(GameState s, params (string id, int n)[] items)
    {
        foreach (var (id, n) in items) E.CreateItems(s, id, n, s.CurrentLocation);
    }

    /// <summary>A05 §3 数量锁：72 件里 62 件可自制 + Q64 护拳绑带 = 63 个配方、10 件不可自制；默认知识 31+1、蓝图 31；设施与门槛分布。</summary>
    [Test]
    public void B10_Counts_StationAndLevelDistribution()
    {
        var eq = Equip;
        Assert.That(eq, Has.Count.EqualTo(63));
        Assert.That(Cfg.Workshops.EquipmentSources, Has.Count.EqualTo(73));
        Assert.That(Cfg.Workshops.EquipmentSources.Count(x => !x.Craftable), Is.EqualTo(10));
        var a05 = eq.Where(r => r.Id != "make_knuckle_wraps").ToList();
        Assert.That(a05, Has.Count.EqualTo(62));
        var st = a05.GroupBy(r => r.Station).ToDictionary(g => g.Key, g => g.Count());
        Assert.That(st, Is.EquivalentTo(new Dictionary<string, int> { ["hand"] = 16, ["workbench"] = 18, ["sewing"] = 17, ["forge"] = 6, ["medical"] = 4, ["armory"] = 1 }));
        var lv = a05.GroupBy(r => r.MinSkills["crafting"]).ToDictionary(g => g.Key, g => g.Count());
        Assert.That(lv, Is.EquivalentTo(new Dictionary<int, int> { [1] = 15, [3] = 16, [7] = 18, [10] = 13 }));
        var kn = a05.Select(r => Cfg.Knowledge.Single(k => k.Id == r.Knowledge)).ToList();
        Assert.That(kn.Count(k => k.Blueprint), Is.EqualTo(31));
        Assert.That(kn.Count(k => !k.Blueprint), Is.EqualTo(31));
        Assert.That(kn.Count(k => k.Initial), Is.EqualTo(6), "原石刀/斧/木矛/木弓/金属矛/弩开局已会");
        // 原有配方 ID 不变；forge 每批另付木炭1
        foreach (var id in new[] { "knife", "axe", "make_wooden_spear", "make_wooden_bow", "make_metal_spear", "make_crossbow", "make_knuckle_wraps" })
            Assert.That(eq.Any(r => r.Id == id), Is.True, id);
        Assert.That(eq.Where(r => r.Station == "forge").All(r => r.Fuel.Count == 1 && r.Fuel["charcoal"] == 1), Is.True);
        Assert.That(eq.Where(r => r.Station != "forge").All(r => r.Fuel.Count == 0), Is.True);
        // 工坊建造五类 + 加工链
        Assert.That(Cfg.Recipes.Count(r => r.Group == "workshop"), Is.EqualTo(5));
        Assert.That(Cfg.Workshops.Stations.Select(x => x.Id), Is.EqualTo(new[] { "hand", "workbench", "forge", "sewing", "medical", "armory" }));
        Assert.That(Cfg.Recipes.Count(r => r.Group == "material"), Is.EqualTo(26));
        Assert.That(Cfg.Recipes.Single(r => r.Id == "make_saline").MinSkills.Keys.Single(), Is.EqualTo("medicine"), "A07：医疗台加工读医疗");
    }

    /// <summary>CR03：原有配方新局仍可直接做（开局已会、手工、无工坊）；新默认知识要营区登记，蓝图未掌握写“需要蓝图”。</summary>
    [Test]
    public void B10_LegacyRecipesStay_DefaultAndBlueprintKnowledge()
    {
        var s = New();
        Give(s, ("stone", 1), ("fiber", 1));
        Assert.That(E.CanStart(s, Craft("knife")), Is.Null);
        var machete = E.Recipe("make_machete");
        Assert.That(E.RecipeUnknownReason(s, machete), Does.Contain("营区登记"));
        var sword = E.Recipe("make_guard_sword");
        Assert.That(E.RecipeUnknownReason(s, sword), Does.StartWith("未掌握：需要守备短剑蓝图").And.Contain("d03_08"));
        s.Progress.Flags.Add("hub_registered");
        Assert.That(E.RecipeUnknownReason(s, machete), Is.Null, "登记后默认知识已知");
        Assert.That(E.RecipeUnknownReason(s, sword), Is.Not.Null, "蓝图不因登记而得");
        E.DebugLearnAllKnowledge(s);
        Assert.That(E.RecipeUnknownReason(s, sword), Is.Null);
        // 锁一次列全：等级 + 工坊
        var locks = E.RecipeLocks(s, sword);
        Assert.That(locks, Has.Some.Contains("需要制造7级"));
        Assert.That(locks, Has.Some.Contains("需要锻炉"));
    }

    /// <summary>工坊建造：只在营地、制造等级门槛、每类每地一座、建成后配方可在本地做；离开营地需要工坊的配方拒绝。</summary>
    [Test]
    public void B10_BuildWorkshop_CampOnly_OnePerType()
    {
        var s = New();
        Assert.That(s.CurrentLocation, Is.EqualTo("camp"));
        Give(s, ("plank", 8), ("fastener", 8), ("parts", 4));
        Start(s, Craft("build_workbench"));
        Run(s);
        Assert.That(s.Locations["camp"].Facility.Workshops, Is.EqualTo(new[] { "workbench" }));
        Assert.That(E.CanStart(s, Craft("build_workbench")), Does.Contain("已有工作台").And.Contain("每类每地一座"));
        // 锻炉要制造3级
        Give(s, ("stone", 8), ("iron_plate", 4), ("parts", 2));
        Assert.That(E.CanStart(s, Craft("build_forge")), Does.Contain("需要制造3级"));
        s.Skills["crafting"].Level = 3;
        Assert.That(E.CanStart(s, Craft("build_forge")), Is.Null);
        // 军械台另需军械蓝图
        s.Skills["crafting"].Level = 7;
        Assert.That(E.CanStart(s, Craft("build_armory")), Does.Contain("未掌握：需要军械台蓝图"));
        // 工作台配方：在营地可做（登记后才会砍刀）
        s.Progress.Flags.Add("hub_registered");
        Give(s, ("iron_plate", 2), ("wood", 1), ("cloth", 1));
        Assert.That(E.CanStart(s, Craft("make_machete")), Is.Null);
        // 旧档缺字段：反序列化后为空表，不影响
        var clone = s.Clone();
        clone.Locations["camp"].Facility.Workshops.Add("sewing");
        Assert.That(s.Locations["camp"].Facility.Workshops, Has.Count.EqualTo(1), "Clone 深拷贝");
    }

    /// <summary>A05 §3：其他工坊须在当前地点；不在营地写“工坊只能建在营地”。</summary>
    [Test]
    public void B10_StationMustBeAtCurrentLocation()
    {
        var s = New();
        E.DebugLearnAllKnowledge(s);
        E.DebugBuildAllWorkshops(s);
        s.Skills["crafting"].Level = 10;
        Give(s, ("iron_plate", 2), ("wood", 1), ("cloth", 1));
        Assert.That(E.CanStart(s, Craft("make_machete")), Is.Null);
        s.CurrentLocation = "field";
        Give(s, ("iron_plate", 2), ("wood", 1), ("cloth", 1));
        Assert.That(E.CanStart(s, Craft("make_machete")), Does.Contain("需要工作台").And.Contain("工坊只能建在临时营地"));
        Assert.That(E.CanStart(s, Craft("knife")), Does.Contain("缺少").Or.Null, "手工配方不看工坊");
    }

    /// <summary>CR02/EQ07：forge 燃料与原料一并预留、完成一并核销；产物一次落当前地面，品质按普通制造。</summary>
    [Test]
    public void B10_ForgeFuelReservedAndConsumed_OutputOnce()
    {
        var s = New();
        E.DebugLearnAllKnowledge(s);
        E.DebugBuildAllWorkshops(s);
        s.Skills["crafting"].Level = 7;
        Give(s, ("steel", 2), ("leather", 1), ("fastener", 1));
        Assert.That(E.CanStart(s, Craft("make_guard_sword")), Does.Contain("缺少燃料木炭").And.Contain("锻炉每批1"));
        Give(s, ("charcoal", 1));
        Start(s, Craft("make_guard_sword"));
        Assert.That(s.Items.Where(i => i.ItemId == "charcoal").All(i => i.ReservedBy != null), Is.True, "木炭已预留");
        Run(s);
        Assert.That(s.Items.Any(i => i.ItemId == "charcoal" || i.ItemId == "steel"), Is.False);
        var swords = s.Items.Where(i => i.ItemId == "guard_sword").ToList();
        Assert.That(swords, Has.Count.EqualTo(1));
        Assert.That(swords[0].Owner, Is.EqualTo("camp"));
    }

    /// <summary>材料加工链：手工木板、工作台铁片；医疗台盐水剂读医疗等级。</summary>
    [Test]
    public void B10_MaterialChain()
    {
        var s = New();
        Give(s, ("wood", 3));
        Start(s, Craft("make_plank"));
        Run(s);
        Assert.That(s.Items.Where(i => i.ItemId == "plank").Sum(i => i.Count), Is.EqualTo(2));
        Give(s, ("scrap", 3));
        Assert.That(E.CanStart(s, Craft("make_iron_plate")), Does.Contain("需要工作台"));
        E.DebugBuildAllWorkshops(s);
        Assert.That(E.CanStart(s, Craft("make_iron_plate")), Is.Null);
        Give(s, ("clean_water", 1), ("salt", 1));
        Assert.That(E.CanStart(s, Craft("make_saline")), Is.Null, "医疗1级即可");
        Give(s, ("lab_reagent", 1), ("clean_water", 1));
        Assert.That(E.CanStart(s, Craft("make_disinfectant")), Does.Contain("需要医疗5级"));
    }

    /// <summary>A10 §7.2 队员交接：同行、安全空闲、只交随身；接收方五卡与 30 公斤校验；队友可换装（按本人五卡）。</summary>
    [Test]
    public void B10_GiveToTeammate_AndTeammateEquip()
    {
        var s = New();
        E.DebugQuickParty(s, 2);
        var mate = s.Party.First(id => id != GameState.PlayerId);
        var mats = E.CreateItems(s, "cloth", 3, ItemInstance.Carried);
        var cloth = mats[0];
        Assert.That(E.GiveBlocked(s, GameState.PlayerId, mate, cloth.InstanceId, 2), Is.Null);
        Assert.That(E.Give(s, GameState.PlayerId, mate, cloth.InstanceId, 2).Ok, Is.True);
        Assert.That(s.Items.Where(i => i.ItemId == "cloth" && i.Owner == ItemInstance.CarriedOwner(mate)).Sum(i => i.Count), Is.EqualTo(2));
        Assert.That(s.Items.Where(i => i.ItemId == "cloth" && i.IsCarried).Sum(i => i.Count), Is.EqualTo(1));
        // 接收方五卡满：拒绝
        foreach (var id in new[] { "hatchet", "stone_knife", "wooden_spear", "wooden_bow", "wooden_shield" })
            E.CreateItems(s, id, 1, ItemInstance.CarriedOwner(mate));
        var knife = E.CreateItems(s, "stone_knife", 1, ItemInstance.Carried)[0];
        Assert.That(E.GiveBlocked(s, GameState.PlayerId, mate, knife.InstanceId, 1), Does.Contain("随身卡位不够"));
        // 队友换装：按队友本人五卡（穿上让出一张）
        var spear = s.Items.First(i => i.ItemId == "wooden_spear" && i.Owner == ItemInstance.CarriedOwner(mate));
        var slot = E.EquipChoices(s, mate, spear.InstanceId).First(c => c.reason == null).slot;
        Assert.That(E.Equip(s, mate, spear.InstanceId, slot).Ok, Is.True);
        Assert.That(spear.IsWornBy(mate), Is.True);
        // 穿戴中的不能直接交
        Assert.That(E.GiveBlocked(s, mate, GameState.PlayerId, spear.InstanceId, 1), Does.Contain("先卸下"));
        // 行动中不能交接
        Give(s, ("stone", 1), ("fiber", 1));
        Start(s, Craft("knife"));
        Assert.That(E.GiveBlocked(s, GameState.PlayerId, mate, knife.InstanceId, 1), Does.Contain("行动进行中"));
    }

    /// <summary>B10 试玩修复 1/10：制造3/7级随工坊生效、10级部分生效；不可自制装备的来源只写类别与地点。</summary>
    [Test]
    public void B10_CraftingNodesLive_SourceTextsClean()
    {
        string St(string id) => GameEngine.PassiveStatusText(Cfg.SkillNodes.Single(n => n.Id == id));
        Assert.That(St("crafting_03"), Is.EqualTo("生效中"));
        Assert.That(St("crafting_07"), Is.EqualTo("生效中"));
        Assert.That(St("crafting_10"), Does.StartWith("部分生效"));
        Assert.That(E.EquipmentSource("shotgun")!.Text, Is.EqualTo("不可自制：固定箱拾获（d03_08）"));
        Assert.That(E.EquipmentSource("marksman_rifle")!.Text, Is.EqualTo("不可自制：一次性交换（h03_workshop）"));
        Assert.That(Cfg.Knowledge.Single(k => k.Id == "blueprint_shock_baton").Source, Does.Not.Contain("电击"));
    }
}
