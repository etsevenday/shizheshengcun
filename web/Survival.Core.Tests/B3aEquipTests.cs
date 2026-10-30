using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 B3a（策划提交 6b3b3e9、bbc8c4c 答复 Q37–Q42）：按 S06「2026-10-31 开发B3答复：Q38—Q42装备与工具」时机表与验收
/// EQ-B3-38/39/42 核对现状（Q38/Q39/Q41/Q42 选 A），以及 B3 线上试玩遗留 R90e（开战停在第一个有武器的页）。
/// </summary>
public class B3aEquipTests : BattleTestBase
{
    private const string P0 = GameState.PlayerId;

    private ItemInstance Give(GameState s, string itemId)
    {
        E.DebugGive(s, itemId, 1);
        return s.Items.Where(i => i.IsCarried && i.ItemId == itemId).OrderByDescending(i => i.InstanceId).First();
    }

    private static void Quiet(GameState s)
    {
        s.SpawnNextCheck = int.MaxValue;
        s.PatrolNextCheck = int.MaxValue;
        s.WorldEnemies.Clear();
    }

    // ───────────── EQ-B3-38：穿戴中的刀斧仍是本人工具，开工锁定实例 ─────────────

    private GameState Woods(bool wearAxe, out ItemInstance axe, out ItemInstance spear)
    {
        var s = New(381);
        E.DebugTeleport(s, "woods");
        E.DebugSetProgress(s, "woods", 75);
        Quiet(s);
        axe = Give(s, "hatchet");
        spear = Give(s, "wooden_spear");
        Assert.That(E.Equip(s, P0, spear.InstanceId, EquipSlots.Weapon2).Ok, Is.True);
        if (wearAxe) Assert.That(E.Equip(s, P0, axe.InstanceId, EquipSlots.Weapon1).Ok, Is.True);
        return s;
    }

    [Test]
    public void EQB338_WornAxe_SameEfficiencyAndWearAsCarried_NotAnyWeapon()
    {
        var worn = Woods(true, out var wAxe, out var wSpear);
        var carried = Woods(false, out var cAxe, out _);
        Assert.That(wAxe.Durability, Is.EqualTo(cAxe.Durability), "同种子同一把斧");
        Assert.That(E.BestTool(worn, "hatchet")!.InstanceId, Is.EqualTo(wAxe.InstanceId), "穿在武器Ⅰ的斧自动选作采集工具");
        Assert.That(E.Item("wooden_spear").ToolTag, Is.Null, "矛不是工具：不把任意武器都算工具");
        Assert.That(new[] { "hatchet", "stone_knife", "pot", "lighter" }.Select(t => E.BestTool(worn, t)?.InstanceId), Has.No.Member(wSpear.InstanceId));

        var pw = E.Preview(worn, ActionRequest.Gather("wood"));
        var pc = E.Preview(carried, ActionRequest.Gather("wood"));
        Assert.That(pw.Ok && pc.Ok, Is.True, pw.Reason + pc.Reason);
        Assert.That((pw.EstimatedMinutes, pw.Outputs.Sum(o => o.Count)), Is.EqualTo((pc.EstimatedMinutes, pc.Outputs.Sum(o => o.Count))), "与随身斧同效率");
        Assert.That(pw.ToolDurabilityAfter, Is.EqualTo(pc.ToolDurabilityAfter));
        Start(worn, ActionRequest.Gather("wood"));
        Start(carried, ActionRequest.Gather("wood"));
        Assert.That(worn.Main!.ToolInstanceId, Is.EqualTo(wAxe.InstanceId));
        Assert.That(worn.Main.LockedItemIds, Does.Contain(wAxe.InstanceId), "开工锁定实际实例");
        Run(worn);
        Run(carried);
        Assert.That(wAxe.Durability, Is.EqualTo(cAxe.Durability).Within(1e-9), "磨损与随身斧相同");
        Assert.That(wAxe.Durability, Is.LessThan(wAxe.MaxDurability!.Value));
        Assert.That(worn.Items.Where(i => i.ItemId == "wood").Sum(i => i.Count), Is.EqualTo(carried.Items.Where(i => i.ItemId == "wood").Sum(i => i.Count)), "产出相同");
        Assert.That(wAxe.IsWornBy(P0), Is.True, "用完仍在武器Ⅰ");
    }

    [Test]
    public void EQB338_EQB342_LockedWornToolCannotBeUnequippedOrSwapped_UnlockedCanAdjust()
    {
        var s = Woods(true, out var axe, out var spear);
        var knife = Give(s, "stone_knife");
        Start(s, ActionRequest.Gather("wood"));
        var main = s.Main!.ActionId;
        var minute = s.GameMinute;
        var before = SaveSerializer.Serialize(s);
        Assert.That(E.Unequip(s, P0, EquipSlots.Weapon1, false).Reason, Does.Contain("行动占用"));
        Assert.That(E.Unequip(s, P0, EquipSlots.Weapon1, true).Reason, Does.Contain("行动占用"));
        Assert.That(E.Equip(s, P0, knife.InstanceId, EquipSlots.Weapon1).Reason, Does.Contain("正被行动使用"), "锁定工具不能被交换下来");
        Assert.That(E.DoInstant(s, InstantRequest.Drop(axe.InstanceId, 1)).Ok, Is.False);
        Assert.That(SaveSerializer.Serialize(s), Is.EqualTo(before), "全部失败、状态不变");

        // Q42：行动进行中本人未锁定的物品可以随身↔穿戴，0 分钟，不撤销/重启行动
        Assert.That(E.Unequip(s, P0, EquipSlots.Weapon2, false).Ok, Is.True);
        Assert.That(spear.Owner, Is.EqualTo(ItemInstance.Carried));
        Assert.That(E.Equip(s, P0, knife.InstanceId, EquipSlots.Weapon2).Ok, Is.True);
        Assert.That(s.GameMinute, Is.EqualTo(minute), "0 分钟");
        Assert.That(s.Main!.ActionId, Is.EqualTo(main), "行动未被取消或重启");
        Assert.That(s.Main.ToolInstanceId, Is.EqualTo(axe.InstanceId), "不重新选工具");
        // 地面取放仍要求无进行中的行动
        Assert.That(E.Unequip(s, P0, EquipSlots.Weapon2, true).Reason, Does.Contain("行动进行中"));
    }

    // ───────────── EQ-B3-39：空槽拳页、坏/无弹不变拳、勉强拳击条件不放宽 ─────────────

    private GameState ArmedFight(uint seed, string? w1, string? w2)
    {
        var s = New(seed);
        if (w1 != null) Assert.That(E.Equip(s, P0, Give(s, w1).InstanceId, EquipSlots.Weapon1).Ok, Is.True);
        if (w2 != null) Assert.That(E.Equip(s, P0, Give(s, w2).InstanceId, EquipSlots.Weapon2).Ok, Is.True);
        E.DebugTeleport(s, "field");
        s.GameMinute = At(2, 9, 0);
        Quiet(s);
        var w = E.DebugSpawnEnemy(s, "shambler", "field");
        Assert.That(E.Approach(s, w.Id).Ok, Is.True);
        Assert.That(s.Battle, Is.Not.Null);
        return s;
    }

    [Test]
    public void EQB339_BrokenOrEmptyMagazine_IsNotFists_DesperatePunchOnlyWhenTired()
    {
        var s = ArmedFight(391, "stone_knife", "pistol");
        s.Skills["unarmed"] = new SkillState { Level = 1 };
        var knife = E.WornItem(s, P0, EquipSlots.Weapon1)!;
        var pistol = E.WornItem(s, P0, EquipSlots.Weapon2)!;
        knife.Durability = 0;
        pistol.LoadedAmmo = 0;
        ToPlayer(s);
        Assert.That(E.AwaitingPlayer(s), Is.True);

        // 武器Ⅰ：坏刀仍是刀页，不是拳页；不出拳的推挡
        Assert.That(E.WeaponOnPage(s, 1, out var w1)!.InstanceId, Is.EqualTo(knife.InstanceId));
        Assert.That(w1.IsFists, Is.False, "损坏不等于空槽");
        Assert.That(E.BattleOptions(s).Any(o => o.Action == "push"), Is.False, "坏刀页不出现拳/钝器的推挡");
        Assert.That(E.CommandBlocked(s, BattleCommand.Of("basic_attack", 1)), Does.Contain("耐久不足"));
        Assert.That(E.CommandBlocked(s, BattleCommand.Of("desperate_punch", 1)), Does.StartWith("只在体力不足以使用任何已装备攻击技能时可用"), "体力足够时不放开勉强拳击");

        // 武器Ⅱ：没子弹的手枪仍是枪页
        Assert.That(E.SelectWeaponPage(s, 2).Ok, Is.True);
        Assert.That(E.CurrentWeapon(s, out var pi).Id, Is.EqualTo("pistol"));
        Assert.That(pi!.InstanceId, Is.EqualTo(pistol.InstanceId));
        Assert.That(E.CommandBlocked(s, BattleCommand.Of("basic_attack", 1)), Does.Contain("弹仓空"));
        Assert.That(E.CommandBlocked(s, BattleCommand.Of("desperate_punch", 1)), Does.StartWith("只在体力不足以使用任何已装备攻击技能时可用"));
        Assert.That(E.BattleOptions(s).Any(o => o.Action == "push"), Is.False, "两槽满不另给拳");

        // 原条件：体力不足以普通攻击时才可勉强拳击
        s.Stats.Energy = 0;
        Assert.That(E.CommandBlocked(s, BattleCommand.Of("desperate_punch", 1)), Is.Null);
    }

    [Test]
    public void EQB339_EmptySlotPageIsFistsWithFistSkills()
    {
        var s = ArmedFight(392, "stone_knife", null);
        s.Skills["unarmed"] = new SkillState { Level = 1 };
        ToPlayer(s);
        Assert.That(E.SelectWeaponPage(s, 2).Ok, Is.True);
        Assert.That(E.CurrentWeapon(s, out var wi).IsFists, Is.True);
        Assert.That(wi, Is.Null, "空槽页不生成拳物品");
        Assert.That(E.BattleOptions(s).Any(o => o.Action == "push"), Is.True, "空槽页按原徒手技能解锁");
    }

    // ───────────── EQ-B3-42：换装时机表 ─────────────

    [Test]
    public void EQB342_EncounterPending_CarriedAdjustAllowed_GroundBlocked_EncounterKept()
    {
        var s = New(421);
        var knife = Give(s, "stone_knife");
        var spear = Give(s, "wooden_spear");
        E.DebugTeleport(s, "field");
        Quiet(s);
        var w = E.DebugSpawnEnemy(s, "shambler", "field");
        s.Encounter = new PendingEncounter { LocationId = "field", EnemyIds = { w.Id }, Source = "explore", Minute = s.GameMinute };
        var minute = s.GameMinute;
        Assert.That(E.Equip(s, P0, knife.InstanceId, EquipSlots.Weapon1).Ok, Is.True, "敌踪待选时可战前调整");
        Assert.That(E.Equip(s, P0, spear.InstanceId, EquipSlots.Weapon2).Ok, Is.True);
        Assert.That(E.Unequip(s, P0, EquipSlots.Weapon2, false).Ok, Is.True);
        Assert.That(E.Unequip(s, P0, EquipSlots.Weapon1, true).Reason, Does.Contain("发现敌踪"), "地面取放不越过敌踪锁");
        Assert.That(s.Encounter, Is.Not.Null, "调整不清除敌踪");
        Assert.That(s.Encounter!.EnemyIds, Is.EqualTo(new List<int> { w.Id }));
        Assert.That(s.GameMinute, Is.EqualTo(minute), "0 分钟");
    }

    [Test]
    public void EQB342_DeadDialogueBattle_AllBlocked()
    {
        var story = NewStory();
        Assert.That(E.DialogueLocked(story), Is.True, "新局开场对白未读");
        E.DebugGive(story, "stone_knife", 1);
        var k = story.Items.First(i => i.IsCarried && i.ItemId == "stone_knife");
        Assert.That(E.EquipBlocked(story, P0, k.InstanceId, EquipSlots.Weapon1), Is.EqualTo(GameEngine.DialogueLockedReason));

        var s = New();
        var knife = Give(s, "stone_knife");
        s.Progress.Dead = true;
        Assert.That(E.EquipBlocked(s, P0, knife.InstanceId, EquipSlots.Weapon1), Does.Contain("死亡"));

        var b = ArmedFight(422, "stone_knife", null);
        Assert.That(E.UnequipBlocked(b, P0, EquipSlots.Weapon1, false), Does.Contain("战斗中"));
        var spear = Give(b, "wooden_spear");
        Assert.That(E.EquipBlocked(b, P0, spear.InstanceId, EquipSlots.Weapon2), Does.Contain("战斗中"), "战内不能换入第三件");
    }

    // ───────────── R90e：开战停在第一个有武器的页 ─────────────

    [Test]
    public void R90e_BattleStartsOnFirstPageWithWeapon()
    {
        var only2 = ArmedFight(901, null, "wooden_spear");
        Assert.That(only2.Battle!.ActiveWeaponSlot, Is.EqualTo(2), "只装武器Ⅱ：开战停在Ⅱ页");
        Assert.That(E.CurrentWeapon(only2, out _).Id, Is.EqualTo("wooden_spear"));

        var both = ArmedFight(902, "stone_knife", "wooden_spear");
        Assert.That(both.Battle!.ActiveWeaponSlot, Is.EqualTo(1));

        var none = ArmedFight(903, null, null);
        Assert.That(none.Battle!.ActiveWeaponSlot, Is.EqualTo(1), "两槽都空：Ⅰ页拳");
        Assert.That(E.CurrentWeapon(none, out _).IsFists, Is.True);

        // 战外当前武器（接近浮窗、指令一览）按同一默认页
        var s = New();
        var spear = Give(s, "wooden_spear");
        Assert.That(E.Equip(s, P0, spear.InstanceId, EquipSlots.Weapon2).Ok, Is.True);
        Assert.That(E.DefaultWeaponPage(s), Is.EqualTo(2));
        Assert.That(E.CurrentWeapon(s, out _).Id, Is.EqualTo("wooden_spear"));
        // 坏武器也算“有武器”（Q39）
        var knife = Give(s, "stone_knife");
        Assert.That(E.Equip(s, P0, knife.InstanceId, EquipSlots.Weapon1).Ok, Is.True);
        knife.Durability = 0;
        Assert.That(E.DefaultWeaponPage(s), Is.EqualTo(1));
    }
}
