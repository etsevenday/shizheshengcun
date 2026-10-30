using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 B4（策划提交 f172677）R14–R17：七槽开放、实例品质与词条、效果合成上限、磨损/维修/拆解新口径。
/// 出处：A05 §1、§2、§4、§5，R02 §2/§3/§5；验收 EQ01–EQ08。
/// </summary>
public class B4EquipTests : BattleTestBase
{
    private const string P0 = GameState.PlayerId;

    private ItemInstance Give(GameState s, string itemId)
    {
        E.DebugGive(s, itemId, 1);
        return s.Items.Where(i => i.IsCarried && i.ItemId == itemId).OrderByDescending(i => i.InstanceId).First();
    }

    private ItemInstance Ground(GameState s, string itemId) => E.CreateItems(s, itemId, 1, s.CurrentLocation).Single();

    // ───────────── M113：七槽开放（H2） ─────────────

    [Test]
    public void H2_FiveNonWeaponSlots_OpenByEquipSlot()
    {
        var s = New();
        var cases = new (string id, string slot)[]
        {
            ("padded_vest", EquipSlots.Armor), ("steady_ring", EquipSlots.CombatAccessory), ("work_shirt", EquipSlots.Top),
            ("work_trousers", EquipSlots.Bottom), ("gather_gloves", EquipSlots.SurvivalAccessory),
        };
        foreach (var (id, slot) in cases)
        {
            var it = Ground(s, id);
            var choices = E.EquipChoices(s, P0, it.InstanceId);
            Assert.That(choices.Select(c => c.slot), Is.EqualTo(new[] { slot }), id);
            Assert.That(choices[0].reason, Is.Null, id);
            Assert.That(E.Equip(s, P0, it.InstanceId, slot).Ok, Is.True, id);
            Assert.That(it.Owner, Is.EqualTo(ItemInstance.EquipOwner(P0, slot)));
        }
        Assert.That(E.WornItems(s), Has.Count.EqualTo(5));
        var shirt2 = Ground(s, "craft_coat");
        Assert.That(E.EquipBlocked(s, P0, shirt2.InstanceId, EquipSlots.Bottom), Does.Contain("只能装备到上衣"));
        Assert.That(E.EquipBlocked(s, P0, shirt2.InstanceId, EquipSlots.Weapon1), Does.Contain("只能装备到上衣"));
        // 交换：旧上衣回随身
        Assert.That(E.Equip(s, P0, shirt2.InstanceId, EquipSlots.Top).Ok, Is.True);
        Assert.That(s.Items.Single(i => i.ItemId == "work_shirt").IsCarried, Is.True);
        var wood = s.Items.First(i => i.ItemId == "wood");
        Assert.That(E.EquipChoices(s, P0, wood.InstanceId), Is.Empty);
        Assert.That(E.EquipBlocked(s, P0, wood.InstanceId, EquipSlots.Armor), Does.Contain("不是装备"));
    }

    [Test]
    public void EQ02_ShieldOccupiesWeaponSlot_NoThirdWeapon_SameKindRejected()
    {
        var s = New();
        var knife = Give(s, "stone_knife");
        var shield = Give(s, "wooden_shield");
        Assert.That(E.Equip(s, P0, knife.InstanceId, EquipSlots.Weapon1).Ok, Is.True);
        Assert.That(E.Equip(s, P0, shield.InstanceId, EquipSlots.Weapon2).Ok, Is.True, "刀+盾合法");
        var shield2 = Give(s, "scrap_shield");
        Assert.That(E.EquipBlocked(s, P0, shield2.InstanceId, EquipSlots.Weapon1), Does.Contain("已装备同类型武器"), "盾占 shield kind");
        var medkit = Give(s, "field_medkit");
        var whistle = Give(s, "signal_whistle");
        Assert.That(E.EquipChoices(s, P0, medkit.InstanceId).Select(c => c.slot), Is.EqualTo(EquipSlots.Weapons));
        // 两槽已满：再穿只能交换，不会出现第三件
        Assert.That(E.Equip(s, P0, medkit.InstanceId, EquipSlots.Weapon1).Ok, Is.True);
        Assert.That(E.WornItems(s).Count(i => E.Weapon(i.ItemId) != null), Is.EqualTo(2));
        Assert.That(knife.IsCarried, Is.True);
        Assert.That(E.EquipBlocked(s, P0, whistle.InstanceId, EquipSlots.Weapon2), Is.Null, "指挥与医疗不同 kind");
    }

    [Test]
    public void H2_SkillLockedTools_CannotAttack_GeneralCommandsStay()
    {
        var s = Fight(7, "shambler");
        var shield = Arm(s, "wooden_shield");
        ToPlayer(s);
        PlayerFirst(s);
        ToPlayer(s);
        Assert.That(E.CurrentWeapon(s, out var wi).Id, Is.EqualTo("wooden_shield"));
        Assert.That(wi!.InstanceId, Is.EqualTo(shield.InstanceId));
        var why = E.CommandBlocked(s, BattleCommand.Of("basic_attack", s.Battle!.Alive.First().Index));
        Assert.That(why, Does.Contain("技能尚未开放"));
        var opts = E.BattleOptions(s);
        Assert.That(opts.Single(o => o.Action == "basic_attack").Reason, Does.Contain("技能尚未开放"));
        Assert.That(opts.Single(o => o.Action == "guard").Available, Is.True, "通用指令照旧");
        Assert.That(opts.Single(o => o.Action == "retreat").Available, Is.True);
    }

    [Test]
    public void EQ03_GearWeightCountsOnPerson_NotRootCards()
    {
        var s = New();
        var cards = E.RootCardCount(s);
        var grams = E.CarriedGrams(s);
        var armor = Give(s, "heavy_armor");
        Assert.That(E.RootCardCount(s), Is.EqualTo(cards + 1));
        Assert.That(E.Equip(s, P0, armor.InstanceId, EquipSlots.Armor).Ok, Is.True);
        Assert.That(E.RootCardCount(s), Is.EqualTo(cards), "穿戴不占五卡");
        Assert.That(E.CarriedGrams(s), Is.EqualTo(grams + 6000), "重量计本人");
    }
}
