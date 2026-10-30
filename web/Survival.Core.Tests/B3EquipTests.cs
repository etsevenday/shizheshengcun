using Survival.Core.Config;
using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 B3（策划提交 f172677）R11/R12：七个穿戴槽与五张物资卡分账、武器类型 kind、同类型武器限制。
/// 出处：S06 全系统更新目标 §1、R02 §1/§5、A05 §5、各案“同类型武器限制（用户跨会话补充，2026-10-30）”；验收 INV01–05、EQ01–03、COMP01、COMP08。
/// </summary>
public class B3EquipTests : BattleTestBase
{
    // ───────────── M98：owner、七槽常量与 weaponKind ─────────────

    [Test]
    public void H4_WeaponKinds_FromConfig_NineKindsOnly()
    {
        var expect = new Dictionary<string, string>
        {
            ["stone_knife"] = "blade", ["kitchen_knife"] = "blade", ["hatchet"] = "axe", ["wooden_spear"] = "spear", ["metal_spear"] = "spear",
            ["crowbar"] = "blunt", ["wooden_bow"] = "archery", ["crossbow"] = "archery", ["pistol"] = "firearms", ["shotgun"] = "firearms", ["rifle"] = "firearms",
        };
        foreach (var kv in expect) Assert.That(Cfg.Weapons.Single(w => w.Id == kv.Key).WeaponKind, Is.EqualTo(kv.Value), kv.Key);
        Assert.That(Cfg.Weapons.Single(w => w.IsFists).WeaponKind, Is.Null, "拳不占武器槽");
        Assert.That(Cfg.Weapons.Single(w => w.Id == "hatchet").SkillId, Is.EqualTo("blade"), "熟练度 skillId 不动");
        Assert.That(WeaponKinds.All, Has.Length.EqualTo(9));
    }

    [Test]
    public void H4_ConfigLoader_RejectsUnknownWeaponKind()
    {
        var json = File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "config.v0.6.json"))
            .Replace("\"weaponKind\": \"axe\"", "\"weaponKind\": \"hammer\"");
        var ex = Assert.Throws<ConfigException>(() => ConfigLoader.FromJson(json));
        Assert.That(string.Join("；", ex!.Errors), Does.Contain("weaponKind hammer"));
    }

    [Test]
    public void H2_EquipOwner_ParseAndSlots()
    {
        var o = ItemInstance.EquipOwner("p0", EquipSlots.Weapon2);
        Assert.That(o, Is.EqualTo("equip:p0:weapon2"));
        Assert.That(ItemInstance.TryEquip(o, out var a, out var slot), Is.True);
        Assert.That((a, slot), Is.EqualTo(("p0", "weapon2")));
        Assert.That(ItemInstance.TryEquip("carried", out _, out _), Is.False);
        Assert.That(ItemInstance.CarriedOwner("p0"), Is.EqualTo("carried"));
        Assert.That(ItemInstance.CarriedOwner("p1"), Is.EqualTo("carried:p1"));
        Assert.That(EquipSlots.All, Is.EqualTo(new[] { "weapon1", "weapon2", "armor", "combat_acc", "top", "bottom", "survival_acc" }));
        Assert.That(EquipSlots.All.Select(EquipSlots.Name), Is.EqualTo(new[] { "武器Ⅰ", "武器Ⅱ", "防具", "战斗饰品", "上衣", "下衣", "求生饰品" }));
    }

    [Test]
    public void H3_WornNotRootCard_ButWeightCountsAndStillTool()
    {
        var s = New();
        var cards = E.RootCardCount(s);
        var grams = E.CarriedGrams(s);
        var knife = E.CreateItems(s, "stone_knife", 1, ItemInstance.EquipOwner("p0", EquipSlots.Weapon1)).Single();
        Assert.That(E.RootCardCount(s), Is.EqualTo(cards), "穿戴物不占五张物资卡");
        Assert.That(E.CarriedGrams(s), Is.EqualTo(grams + E.Item("stone_knife").WeightGrams), "重量计本人");
        Assert.That(E.WornGrams(s), Is.EqualTo(E.Item("stone_knife").WeightGrams));
        Assert.That(E.PlaceOf(s, knife), Is.EqualTo(ItemInstance.Carried), "根位置在本人身上");
        var tag = E.Item("stone_knife").ToolTag;
        if (tag != null) Assert.That(E.BestTool(s, tag)?.InstanceId, Is.EqualTo(knife.InstanceId), "穿戴中的刀仍算本人可用工具（Q38 待确认，现状算）");
        Assert.That(E.RepairCandidates(s).Any(i => i.InstanceId == knife.InstanceId), Is.True);
        Assert.That(E.WherePath(s, knife), Is.EqualTo("穿戴·武器Ⅰ"));
    }
}
