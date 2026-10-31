using Newtonsoft.Json.Linq;
using Survival.Core.Config;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 B4（策划提交 f172677）R13/R14：物理物品目录与 72 件装备静态表按 A12 全系统策划数据索引导入（tools/import-b4-catalog.py）。
/// 出处：A07「材料唯一表」「二十种食品用品」「X1.2 新增物理目录」、A05 §1/§2/§4、弹药与弹仓表；验收 SUP01、ES01、EQ01–EQ03、EQ07。
/// </summary>
public class B4CatalogTests : TestBase
{
    private static JObject A12() =>
        JObject.Parse(File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "design.a12.json")));

    private static ItemConfig Item(string id) => Cfg.Items.Single(i => i.Id == id);

    private static JObject ConfigJson() =>
        JObject.Parse(File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "config.v0.6.json")));

    // ───────────── M111：物理物品目录（R13） ─────────────

    [Test]
    public void SUP01_PhysicalItems_AllImported_DefinitionsMatchA12()
    {
        var a12 = A12();
        var items = (JArray)a12["physicalItems"]!;
        Assert.That(items, Has.Count.EqualTo(100));
        foreach (var p in items)
        {
            var id = (string)p["id"]!;
            var it = Cfg.Items.SingleOrDefault(i => i.Id == id);
            Assert.That(it, Is.Not.Null, $"{id} 未导入");
            // 旧 ID 的旧数值不动（开发假设 H1）：只核对新导入的物品
            if (it!.Desc.Contains("本版只有物品定义"))
            {
                Assert.That(it.Name, Is.EqualTo((string)p["name"]!), id);
                Assert.That(it.WeightGrams, Is.EqualTo((int)p["weightGrams"]!), id);
                Assert.That(it.StackMax, Is.EqualTo((int)p["stackMax"]!), id);
                var h = (int)p["shelfLifeHours"]!;
                Assert.That(it.ShelfLifeMinutes, Is.EqualTo(h > 0 ? h * 60 : (int?)null), id);
                Assert.That(it.Edible, Is.False, $"{id} 只导定义，不能食用");
                Assert.That(it.Use, Is.Null, $"{id} 不导使用效果");
            }
        }
        Assert.That(Cfg.Items.Count(i => i.Desc.Contains("本版只有物品定义")), Is.EqualTo(70));
    }

    [Test]
    public void SUP01_OldItems_ValuesUnchanged()
    {
        // 抽查：旧物品的名称与数值不被 A12 的写法覆盖（箭在 A12 叫“箭”、旧配置叫“箭矢”）
        Assert.That(Item("arrow").Name, Is.EqualTo("箭矢"));
        Assert.That(Item("canned_food").Use!.Satiety, Is.EqualTo(35));
        Assert.That(Item("herb").ShelfLifeMinutes, Is.EqualTo(4320));
        Assert.That(Item("stone_knife").Kind, Is.EqualTo("tool"));
    }

    [Test]
    public void ES01_MapResourcesAndNewRecipes_ReferenceOnlyExistingItems()
    {
        var a12 = A12();
        var have = Cfg.Items.Select(i => i.Id).ToHashSet();
        var phys = ((JArray)a12["physicalItems"]!).Select(p => (string)p["id"]!).ToHashSet();
        var mapIds = ((JArray)a12["mapResources"]!).SelectMany(r => ((JObject?)r["items"])?.Properties().Select(x => x.Name) ?? Enumerable.Empty<string>()).ToHashSet();
        var missing = mapIds.Where(id => !have.Contains(id)).OrderBy(x => x).ToList();
        // 只剩档案/知识类 ID（不是实体物品，属于后续地图批次）
        Assert.That(missing, Has.Count.EqualTo(8), string.Join("、", missing));
        Assert.That(missing.All(id => !phys.Contains(id)), Is.True);
        foreach (var id in new[] { "steel", "wire", "battery", "rope", "salt", "adhesive", "canned_beans", "charcoal", "clay", "iron_ore", "weapon_parts" })
            Assert.That(have, Does.Contain(id), $"地图资源引用的 {id} 须有定义");

        foreach (var r in (JArray)a12["newRecipes"]!)
            foreach (var key in new[] { "inputs", "outputs" })
                foreach (var prop in ((JObject?)r[key])?.Properties() ?? Enumerable.Empty<JProperty>())
                    Assert.That(have, Does.Contain(prop.Name), $"新配方 {r["id"]} 的 {key} {prop.Name} 须有定义");
        foreach (var id in new[] { "oral_rehydration", "compression_bandage", "canvas", "filter_media", "herb_extract", "metal_tube", "dried_berries" })
            Assert.That(have, Does.Contain(id));
    }

    [Test]
    public void SUP01_NewItemKinds_ByCatalogTable()
    {
        Assert.That(Item("steel").Kind, Is.EqualTo("material"));
        Assert.That(Item("grain").Kind, Is.EqualTo("food"));
        Assert.That(Item("dried_meat").Kind, Is.EqualTo("food"));
        Assert.That(Item("medical_gauze").Kind, Is.EqualTo("medical"));
        Assert.That(Item("sterile_bandage").Kind, Is.EqualTo("medical"));
        Assert.That(Item("purification_tablet").Kind, Is.EqualTo("material"), "不是液体，不能装锅");
        Assert.That(Item("hardtack").ShelfLifeMinutes, Is.EqualTo(336 * 60));
        Assert.That(Cfg.Items.All(i => ItemConfig.Kinds.Contains(i.Kind)), Is.True);
    }

    // ───────────── M112：72 件装备静态表与弹仓表（R14） ─────────────

    private static double[] Range(string text)
    {
        var p = text.Replace("—", "–").Split('–');
        return new[] { double.Parse(p[0], System.Globalization.CultureInfo.InvariantCulture), double.Parse(p[1], System.Globalization.CultureInfo.InvariantCulture) };
    }

    private static readonly Dictionary<string, string> SlotMap = new()
    {
        ["armor"] = "armor", ["combat_charm"] = "combat_acc", ["top"] = "top", ["bottom"] = "bottom", ["survival_charm"] = "survival_acc",
    };

    [Test]
    public void EQ_Weapons38_MatchA05Panel()
    {
        var a12 = (JArray)A12()["equipment"]!["weapons"]!;
        Assert.That(a12, Has.Count.EqualTo(38));
        Assert.That(Cfg.Weapons.Count(w => !w.IsFists), Is.EqualTo(38));
        var oldIds = new[] { "stone_knife", "kitchen_knife", "hatchet", "crowbar", "wooden_spear", "metal_spear", "wooden_bow", "crossbow", "pistol", "shotgun", "rifle" };
        foreach (var j in a12)
        {
            var id = (string)j["id"]!;
            var w = Cfg.Weapons.Single(x => x.Id == id);
            var it = Item(id);
            var dmg = Range((string)j["damage"]!);
            var dur = Range((string)j["durability"]!);
            Assert.That(w.WeaponKind, Is.EqualTo((string)j["kind"]!), id);
            Assert.That(w.Tier, Is.EqualTo((int)j["tier"]!), id);
            Assert.That((w.DamageMin, w.DamageMax), Is.EqualTo((dmg[0], dmg[1])), id);
            Assert.That(w.AccuracyRating, Is.EqualTo((double)j["accuracy"]!), id);
            Assert.That(w.CritRatingBase, Is.EqualTo((double)j["crit"]!), id);
            Assert.That(w.EnergyCost, Is.EqualTo((double)j["energy"]!).Within(1e-9), id);
            Assert.That(w.Speed, Is.EqualTo((double)j["speed"]!), id);
            Assert.That(w.WeightGrams, Is.EqualTo((int)j["weight_g"]!), id);
            Assert.That(w.DurabilityMaxRange, Is.EqualTo(dur), id);
            Assert.That((w.OwnPositions, w.TargetPositions), Is.EqualTo(((string)j["own"]!, (string)j["target"]!)), id);
            Assert.That(w.TraitText, Is.EqualTo((string)j["trait"]!), id);
            // 弹仓表（A05“弹药、弹仓与实际出手实例补全”）
            var ammo = (string)j["ammo"]!;
            Assert.That(w.AmmoId, Is.EqualTo(ammo == "" ? null : ammo), id);
            Assert.That(w.MagazineCapacity, Is.EqualTo((int)j["magazine"]!), id);
            Assert.That(it.EquipSlot, Is.EqualTo("weapon"), id);
            Assert.That(it.WeightGrams, Is.EqualTo(w.WeightGrams), id);
            if (!oldIds.Contains(id))
            {
                Assert.That(it.Kind, Is.EqualTo("weapon"), id);
                Assert.That(it.ToolTag, Is.Null, $"{id} 新武器不凭名字获得工具用途（S05）");
            }
        }
        // 旧武器的现行两行战斗射程不动（开发假设 H3）；新武器按目标位推导
        Assert.That(Cfg.Weapons.Single(w => w.Id == "wooden_spear").Targets, Is.EqualTo("all_rear_damage_0.75"));
        Assert.That(Cfg.Weapons.Single(w => w.Id == "pike").Targets, Is.EqualTo("all_rear_damage_0.75"));
        Assert.That(Cfg.Weapons.Single(w => w.Id == "service_pistol").Targets, Is.EqualTo("all"));
        Assert.That(Cfg.Weapons.Single(w => w.Id == "machete").Targets, Is.EqualTo("front"));
        // B7（A06 §2）：盾有盾击可以攻击；指挥/医疗器具“不可直接攻击”，只用本门技能
        Assert.That(Cfg.Weapons.Where(w => w.WeaponKind is "command" or "medical").All(w => !w.CanAttack), Is.True);
        Assert.That(Cfg.Weapons.Where(w => w.WeaponKind is "shield").All(w => w.CanAttack), Is.True);
        Assert.That(Cfg.Weapons.Single(w => w.Id == "field_medkit").SkillId, Is.EqualTo("medicine"), "医疗器具使用 medicine 熟练（A05 §1 末段）");
    }

    [Test]
    public void EQ_MagazineTable_TenRows()
    {
        var mags = Cfg.Weapons.Where(w => w.AmmoId != null).ToDictionary(w => w.Id, w => (w.AmmoId, w.MagazineCapacity));
        Assert.That(mags, Has.Count.EqualTo(10));
        Assert.That(mags["repeater_crossbow"], Is.EqualTo(("bolt", 2)));
        Assert.That(mags["service_pistol"], Is.EqualTo(("ammo_9mm", 10)));
        Assert.That(mags["marksman_rifle"], Is.EqualTo(("ammo_rifle", 5)));
        Assert.That(mags["hunter_bow"], Is.EqualTo(("arrow", 0)));
    }

    [Test]
    public void EQ_Gear34_MatchA05Table()
    {
        var a12 = (JArray)A12()["equipment"]!["gear"]!;
        // 策划 f58ead8（答复 Q64）新增战斗饰品护拳绑带 knuckle_wraps，B7c 导入（共 35 件）；它的效果原文含内部技能 ID，界面文字另写，由 B7cCombatTests CB-Q64c 核对
        var pending = new[] { "knuckle_wraps" };
        Assert.That(a12.Count(j => !pending.Contains((string)j["id"]!)), Is.EqualTo(34));
        Assert.That(Cfg.Gear, Has.Count.EqualTo(35));
        foreach (var j in a12.Where(j => !pending.Contains((string)j["id"]!)))
        {
            var id = (string)j["id"]!;
            var g = Cfg.Gear.Single(x => x.Id == id);
            var it = Item(id);
            Assert.That(g.Slot, Is.EqualTo(SlotMap[(string)j["slot"]!]), id);
            Assert.That(it.EquipSlot, Is.EqualTo(g.Slot), id);
            Assert.That(it.Kind, Is.EqualTo("gear"), id);
            Assert.That(g.Tier, Is.EqualTo((int)j["tier"]!), id);
            Assert.That(g.WeightGrams, Is.EqualTo((int)j["weight_g"]!), id);
            Assert.That(it.WeightGrams, Is.EqualTo(g.WeightGrams), id);
            Assert.That(g.EffectText, Is.EqualTo((string)j["effect"]!), id);
            var d = (string)j["durability"]!;
            Assert.That(g.DurabilityMaxRange, Is.EqualTo(d == "无" ? null : Range(d)), id);
            // 结构化效果的数值必须出现在原文里（只是把原文抄成机器可读，不自补数值）
            foreach (var ef in g.Effects)
                Assert.That(InText(g.EffectText, ef.Value), Is.True, $"{id} {ef.Key}={ef.Value} 不在原文“{g.EffectText}”里");
        }
        Assert.That(Cfg.Gear.Count(g => g.Slot == "armor"), Is.EqualTo(8));
        Assert.That(Cfg.Gear.Count(g => g.Slot == "combat_acc"), Is.EqualTo(9), "8 件 + B7c 护拳绑带");
        Assert.That(Cfg.Gear.Count(g => g.Slot == "top"), Is.EqualTo(6));
        Assert.That(Cfg.Gear.Count(g => g.Slot == "bottom"), Is.EqualTo(6));
        Assert.That(Cfg.Gear.Count(g => g.Slot == "survival_acc"), Is.EqualTo(6));
    }

    [Test]
    public void EQ_Affixes18_MatchA05Table_PoolsAndGroups()
    {
        var a12 = (JArray)A12()["equipment"]!["affixes"]!;
        Assert.That(Cfg.Affixes, Has.Count.EqualTo(18));
        foreach (var j in a12)
        {
            var id = (string)j["id"]!;
            var a = Cfg.Affixes.Single(x => x.Id == id);
            Assert.That((a.Name, a.PoolText, a.EffectText, a.Group), Is.EqualTo(((string)j["name"]!, (string)j["pool"]!, (string)j["effect"]!, (string)j["group"]!)), id);
            foreach (var ef in a.Effects)
                Assert.That(InText(a.EffectText, ef.Value), Is.True, $"{id} {ef.Key}={ef.Value} 不在原文里");
        }
        Assert.That(Cfg.Affixes.Single(a => a.Id == "accurate").Pools, Does.Not.Contain("kind:shield"), "攻击武器不含盾/指挥/医疗");
        Assert.That(Cfg.Affixes.Single(a => a.Id == "durable").Pools, Is.EqualTo(new[] { "durable" }));
        // B7a（Q51）：求生衣物池之后追加求生饰品池，饰品专用互斥组 charm_consumption，原衣物组 survival 不变
        Assert.That(Cfg.Affixes.Single(a => a.Id == "economical").Pools, Is.EqualTo(new[] { "slot:top", "slot:bottom", "slot:survival_acc" }));
        Assert.That(Cfg.Affixes.Single(a => a.Id == "economical").GroupFor("survival_acc"), Is.EqualTo("charm_consumption"));
        Assert.That(Cfg.Affixes.Single(a => a.Id == "economical").GroupFor("top"), Is.EqualTo("survival"));
    }

    [Test]
    public void EQ_QualityRules_FromA05_4()
    {
        var q = Cfg.Equipment;
        Assert.That(q.AffixCounts, Is.EqualTo(new Dictionary<string, int> { ["white"] = 0, ["green"] = 1, ["blue"] = 2 }));
        Assert.That(q.QualityOrigins["novice_scavenge"], Is.EqualTo(new Dictionary<string, double> { ["white"] = 0.8, ["green"] = 0.2 }));
        Assert.That(q.QualityOrigins["mainline_scavenge"], Is.EqualTo(new Dictionary<string, double> { ["white"] = 0.5, ["green"] = 0.45, ["blue"] = 0.05 }));
        Assert.That(q.QualityOrigins["dungeon_boss"], Is.EqualTo(new Dictionary<string, double> { ["green"] = 0.8, ["blue"] = 0.2 }));
        Assert.That(q.QualityOrigins["craft"], Is.EqualTo(new Dictionary<string, double> { ["green"] = 1 }), "普通制造默认绿色1条");
        Assert.That((q.Limits.PositiveSpeedCap, q.Limits.ArmorCap, q.Limits.SurvivalWorkFloor, q.Limits.NegativeRateFloor), Is.EqualTo((3.0, 0.9, 0.7, 0.75)));
        // B7a 按新规则改写（Q46 取代 A05 §5“武器按品质废金属1/2/3”）：拆解按材料价值预算 5R≤4I，逐件表见 B7aEquipTests
        Assert.That((q.Salvage.MaterialValues["scrap"], q.Salvage.CapNumerator, q.Salvage.CapDenominator), Is.EqualTo((2, 4, 5)));
        var salvage = Cfg.Recipes.Single(r => r.Id == q.Salvage.RecipeId);
        Assert.That((salvage.IsSalvage, salvage.BaseMinutes), Is.EqualTo((true, 15)));
    }

    /// <summary>数值（或其百分数写法，如 0.25 ↔ 25%）出现在原文里。</summary>
    private static bool InText(string text, double v) =>
        Numbers(text).Any(n => Math.Abs(n - Math.Abs(v)) < 1e-9 || Math.Abs(n - Math.Abs(v) * 100) < 1e-6);

    private static List<double> Numbers(string text) =>
        System.Text.RegularExpressions.Regex.Matches(text, @"\d+(\.\d+)?")
            .Select(m => double.Parse(m.Value, System.Globalization.CultureInfo.InvariantCulture)).ToList();

    [Test]
    public void ConfigLoader_RejectsGearSlotMismatch()
    {
        var j = ConfigJson();
        ((JArray)j["items"]!).First(x => (string?)x["id"] == "padded_vest")["equipSlot"] = "top";
        var ex = Assert.Throws<ConfigException>(() => ConfigLoader.FromJson(j.ToString()));
        Assert.That(string.Join("；", ex!.Errors), Does.Contain("padded_vest"));
    }

    [Test]
    public void ConfigLoader_RejectsUnknownItemKind()
    {
        var j = ConfigJson();
        ((JArray)j["items"]!).First(x => (string?)x["id"] == "steel")["kind"] = "alloy";
        var ex = Assert.Throws<ConfigException>(() => ConfigLoader.FromJson(j.ToString()));
        Assert.That(string.Join("；", ex!.Errors), Does.Contain("类别 alloy"));
    }
}
