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

    [Test]
    public void ConfigLoader_RejectsUnknownItemKind()
    {
        var j = ConfigJson();
        ((JArray)j["items"]!).First(x => (string?)x["id"] == "steel")["kind"] = "alloy";
        var ex = Assert.Throws<ConfigException>(() => ConfigLoader.FromJson(j.ToString()));
        Assert.That(string.Join("；", ex!.Errors), Does.Contain("类别 alloy"));
    }
}
