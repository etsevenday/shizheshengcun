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

    // ───────────── M99：战外换装 API 与 8→9 迁移 ─────────────

    private ItemInstance Give(GameState s, string itemId)
    {
        E.DebugGive(s, itemId, 1);
        return s.Items.Where(i => i.IsCarried && i.ItemId == itemId).OrderByDescending(i => i.InstanceId).First();
    }

    private const string P0 = GameState.PlayerId;

    [Test]
    public void EQ_SameKindSecondWeaponRejected_StateUnchanged()
    {
        var s = New();
        var stone = Give(s, "stone_knife");
        var kitchen = Give(s, "kitchen_knife");
        var spear = Give(s, "wooden_spear");
        Assert.That(E.Equip(s, P0, stone.InstanceId, EquipSlots.Weapon1).Ok, Is.True);
        var before = SaveSerializer.Serialize(s);
        var r = E.Equip(s, P0, kitchen.InstanceId, EquipSlots.Weapon2);
        Assert.That(r.Ok, Is.False);
        Assert.That(r.Reason, Does.StartWith("已装备同类型武器：石刀"));
        Assert.That(SaveSerializer.Serialize(s), Is.EqualTo(before), "失败不卸原武器、不改任何状态");
        Assert.That(E.Equip(s, P0, spear.InstanceId, EquipSlots.Weapon2).Ok, Is.True, "刀+矛合法");
        // 同槽替换同类：石刀换厨房刀（另一槽是矛）合法，石刀回随身
        Assert.That(E.Equip(s, P0, kitchen.InstanceId, EquipSlots.Weapon1).Ok, Is.True);
        Assert.That(stone.Owner, Is.EqualTo(ItemInstance.Carried));
        Assert.That(E.WornItem(s, P0, EquipSlots.Weapon1)!.InstanceId, Is.EqualTo(kitchen.InstanceId));
    }

    [Test]
    public void EQ_SameKindPairsAcrossNames_BowCrossbow_PistolRifle()
    {
        var s = New();
        var bow = Give(s, "wooden_bow");
        var xbow = Give(s, "crossbow");
        Assert.That(E.Equip(s, P0, bow.InstanceId, EquipSlots.Weapon2).Ok, Is.True);
        Assert.That(E.EquipBlocked(s, P0, xbow.InstanceId, EquipSlots.Weapon1), Does.Contain("已装备同类型武器：木弓"));
        Assert.That(E.Unequip(s, P0, EquipSlots.Weapon2, false).Ok, Is.True);
        var pistol = Give(s, "pistol");
        var rifle = Give(s, "rifle");
        Assert.That(E.Equip(s, P0, pistol.InstanceId, EquipSlots.Weapon1).Ok, Is.True);
        Assert.That(E.EquipBlocked(s, P0, rifle.InstanceId, EquipSlots.Weapon2), Does.Contain("已装备同类型武器：手枪"));
        Assert.That(E.EquipBlocked(s, P0, bow.InstanceId, EquipSlots.Weapon2), Is.Null, "弓+枪合法");
    }

    [Test]
    public void INV01_EQ03_TwoWornPlusFiveCards_WeightSumsOnPerson()
    {
        var s = New();
        var knife = Give(s, "stone_knife");
        var spear = Give(s, "wooden_spear");
        Assert.That(E.Equip(s, P0, knife.InstanceId, EquipSlots.Weapon1).Ok, Is.True);
        Assert.That(E.Equip(s, P0, spear.InstanceId, EquipSlots.Weapon2).Ok, Is.True);
        foreach (var id in new[] { "wood", "fiber", "stone" }) E.DebugGive(s, id, 1);
        Assert.That(E.RootCardCount(s), Is.EqualTo(5), "锅、打火器、木头、纤维、石头 = 5 张；两件穿戴不占卡");
        var expect = s.Items.Where(i => i.IsCarried || i.IsWorn).Sum(i => E.ItemGrams(s, i));
        Assert.That(E.CarriedGrams(s), Is.EqualTo(expect), "七槽与五卡的重量都计本人");
        Assert.That(E.WornGrams(s), Is.EqualTo(E.Item("stone_knife").WeightGrams + E.Item("wooden_spear").WeightGrams));
    }

    [Test]
    public void INV02_UnequipWithFullCards_RejectedUnlessGround()
    {
        var s = New();
        var knife = Give(s, "stone_knife");
        Assert.That(E.Equip(s, P0, knife.InstanceId, EquipSlots.Weapon1).Ok, Is.True);
        foreach (var id in new[] { "wood", "fiber", "stone" }) E.DebugGive(s, id, 1);
        Assert.That(E.RootCardCount(s), Is.EqualTo(5));
        var before = SaveSerializer.Serialize(s);
        var r = E.Unequip(s, P0, EquipSlots.Weapon1, false);
        Assert.That(r.Ok, Is.False);
        Assert.That(r.Reason, Does.Contain("放当前地面"));
        Assert.That(SaveSerializer.Serialize(s), Is.EqualTo(before), "整次拒绝，不自动丢地");
        Assert.That(E.Unequip(s, P0, EquipSlots.Weapon1, true).Ok, Is.True);
        Assert.That(knife.Owner, Is.EqualTo(s.CurrentLocation), "玩家明确选择后放当前地面");
        Assert.That(E.RootCardCount(s), Is.EqualTo(5));
    }

    [Test]
    public void Equip_SwapWithFullCards_JudgedByFinalState()
    {
        var s = New();
        var knife = Give(s, "stone_knife");
        Assert.That(E.Equip(s, P0, knife.InstanceId, EquipSlots.Weapon1).Ok, Is.True);
        var axe = Give(s, "hatchet");
        foreach (var id in new[] { "wood", "fiber" }) E.DebugGive(s, id, 1);
        Assert.That(E.RootCardCount(s), Is.EqualTo(5));
        // 斧离开五卡、石刀回五卡：最终仍 5 张，不因中间瞬态第六张误拒
        Assert.That(E.Equip(s, P0, axe.InstanceId, EquipSlots.Weapon1).Ok, Is.True);
        Assert.That(knife.Owner, Is.EqualTo(ItemInstance.Carried));
        Assert.That(axe.Owner, Is.EqualTo(ItemInstance.EquipOwner(P0, EquipSlots.Weapon1)));
        Assert.That(E.RootCardCount(s), Is.EqualTo(5));
    }

    [Test]
    public void Equip_SourceAndSlotRules()
    {
        var s = New();
        var ground = E.CreateItems(s, "stone_knife", 1, s.CurrentLocation).Single();
        // Q40 选 B（B3a）：当前地面的合法装备可直接穿；其他地点的仍拒绝
        Assert.That(E.EquipBlocked(s, P0, ground.InstanceId, EquipSlots.Weapon1), Is.Null, "当前地面可直接穿（Q40 选 B）");
        var away = E.CreateItems(s, "stone_knife", 1, "field").Single();
        Assert.That(E.EquipBlocked(s, P0, away.InstanceId, EquipSlots.Weapon1), Does.Contain("只能穿上本人随身或当前地面"));
        var lighter = s.Items.First(i => i.IsCarried && i.ItemId == "lighter");
        Assert.That(E.EquipBlocked(s, P0, lighter.InstanceId, EquipSlots.Weapon1), Does.Contain("不是武器"));
        var knife = Give(s, "stone_knife");
        Assert.That(E.EquipBlocked(s, P0, knife.InstanceId, EquipSlots.Armor), Does.Contain("只能装备到武器Ⅰ或武器Ⅱ"), "B4 起七槽开放，按 equipSlot 匹配");
        knife.ReservedBy = 99;
        Assert.That(E.EquipBlocked(s, P0, knife.InstanceId, EquipSlots.Weapon1), Does.Contain("预留"));
        knife.ReservedBy = null;
        Assert.That(E.Unequip(s, P0, EquipSlots.Weapon2, false).Reason, Does.Contain("空的"));
        Assert.That(E.Equip(s, "p9", knife.InstanceId, EquipSlots.Weapon1).Ok, Is.False, "不在同行的角色不能换装");
    }

    [Test]
    public void Equip_IdempotentAndBlockedInBattle()
    {
        var s = New();
        var knife = Give(s, "stone_knife");
        Assert.That(E.Equip(s, P0, knife.InstanceId, EquipSlots.Weapon1, "eq-1").Ok, Is.True);
        Assert.That(E.Unequip(s, P0, EquipSlots.Weapon1, false, "uq-1").Ok, Is.True);
        Assert.That(E.Equip(s, P0, knife.InstanceId, EquipSlots.Weapon1, "eq-1").Duplicate, Is.True, "同一 requestId 不重复执行");
        Assert.That(knife.Owner, Is.EqualTo(ItemInstance.Carried));

        var b = Fight(5, "shambler");
        var k2 = Give(b, "kitchen_knife");
        Assert.That(E.EquipBlocked(b, P0, k2.InstanceId, EquipSlots.Weapon1), Does.Contain("战斗中"));
    }

    [Test]
    public void Equip_ArmedWeaponUsedOutOfBattle_Weapon1IsCurrent()
    {
        var s = New();
        var spear = Give(s, "wooden_spear");
        Assert.That(E.CurrentWeapon(s, out _).IsFists, Is.True);
        Assert.That(E.Equip(s, P0, spear.InstanceId, EquipSlots.Weapon1).Ok, Is.True);
        Assert.That(E.CurrentWeapon(s, out var wi).Id, Is.EqualTo("wooden_spear"));
        Assert.That(wi!.InstanceId, Is.EqualTo(spear.InstanceId));
    }

    // ───────────── M100：战内两个武器页 0AP ─────────────

    private GameState ArmedFight(uint seed, out ItemInstance knife, out ItemInstance spear)
    {
        var s = New(seed);
        knife = Give(s, "stone_knife");
        spear = Give(s, "wooden_spear");
        Assert.That(E.Equip(s, P0, knife.InstanceId, EquipSlots.Weapon1).Ok, Is.True);
        Assert.That(E.Equip(s, P0, spear.InstanceId, EquipSlots.Weapon2).Ok, Is.True);
        E.DebugTeleport(s, "field");
        s.GameMinute = At(2, 9, 0);
        s.SpawnNextCheck = int.MaxValue;
        s.PatrolNextCheck = int.MaxValue;
        s.WorldEnemies.Clear();
        var w = E.DebugSpawnEnemy(s, "shambler", "field");
        Assert.That(E.Approach(s, w.Id).Ok, Is.True);
        return s;
    }

    [Test]
    public void H6_BattleStartsOnPage1_SwitchZeroAp_AnyTime_SavedInBattle()
    {
        var s = ArmedFight(31, out var knife, out var spear);
        Assert.That(s.Battle!.ActiveWeaponSlot, Is.EqualTo(1));
        Assert.That(E.CurrentWeapon(s, out _).Id, Is.EqualTo("stone_knife"));
        // 非本人激活也可切页查看（A10 §4.2）
        Assert.That(E.AwaitingPlayer(s), Is.False, "开战后先推进队列，尚未轮到玩家");
        Assert.That(E.SelectWeaponPage(s, 2).Ok, Is.True);

        Assert.That(E.CurrentWeapon(s, out _).Id, Is.EqualTo("wooden_spear"));
        Assert.That(E.SelectWeaponPage(s, 2).Reason, Does.Contain("已在武器Ⅱ页"));
        Assert.That(E.SelectWeaponPage(s, 3).Ok, Is.False);
        var json = SaveSerializer.Serialize(s);
        var back = SaveSerializer.Deserialize(json, Cfg);
        Assert.That(back.Battle!.ActiveWeaponSlot, Is.EqualTo(2), "战斗中存读档后当前页保留");
        Assert.That(E.CurrentWeapon(back, out _).Id, Is.EqualTo("wooden_spear"));
        ToPlayer(s);
        var ap = s.Battle!.PlayerAp;
        var energy = s.Stats.Energy;
        Assert.That(Cmd(s, "switch_weapon", null, 1).Ok, Is.True);
        Assert.That(s.Battle!.PlayerAp, Is.EqualTo(ap), "0AP");
        Assert.That(s.Stats.Energy, Is.EqualTo(energy), "不耗体力");
        Assert.That(knife.IsWornBy(P0) && spear.IsWornBy(P0), Is.True, "切页不改变装备归属");
    }

    [Test]
    public void EQ01_AttackWearsOnlyUsedWeapon_PostBattleNoWriteBack()
    {
        var s = ArmedFight(32, out var knife, out var spear);
        ToPlayer(s);
        PlayerFirst(s);
        ToPlayer(s);
        Assert.That(E.SelectWeaponPage(s, 2).Ok, Is.True);
        var kd = knife.Durability;
        var sd = spear.Durability;
        Force(s, AtLeast(0.999));
        Assert.That(Cmd(s, "basic_attack", 1).Ok, Is.True);
        Assert.That(spear.Durability, Is.EqualTo(sd - Cfg.Wear.PerExecutedAttackCommand).Within(1e-9), "只扣本次所用武器");
        Assert.That(knife.Durability, Is.EqualTo(kd), "另一把不扣");
        // 撤退结束战斗（停在武器Ⅱ页）：两把仍在原槽，页号不回写装备
        Assert.That(Cmd(s, "end_turn").Ok, Is.True);
        ToPlayer(s);
        Assert.That(s.Battle!.ActiveWeaponSlot, Is.EqualTo(2));
        Force(s, Below(0.0001));

        Assert.That(Cmd(s, "retreat").Ok, Is.True);
        Assert.That(s.Battle, Is.Null);
        Assert.That(E.WornItem(s, P0, EquipSlots.Weapon1)!.InstanceId, Is.EqualTo(knife.InstanceId));
        Assert.That(E.WornItem(s, P0, EquipSlots.Weapon2)!.InstanceId, Is.EqualTo(spear.InstanceId));
        Assert.That(E.CurrentWeapon(s, out _).Id, Is.EqualTo("stone_knife"), "战外当前武器回到武器Ⅰ页");
    }

    [Test]
    public void H6_EmptyPageIsFists_FullPagesHaveNoSeparateFists()
    {
        var s = New(33);
        var knife = Give(s, "stone_knife");
        Assert.That(E.Equip(s, P0, knife.InstanceId, EquipSlots.Weapon1).Ok, Is.True);
        E.DebugTeleport(s, "field");
        s.GameMinute = At(2, 9, 0);
        s.WorldEnemies.Clear();
        s.SpawnNextCheck = int.MaxValue;
        var w = E.DebugSpawnEnemy(s, "shambler", "field");
        Assert.That(E.Approach(s, w.Id).Ok, Is.True);
        Assert.That(E.WeaponOnPage(s, 2, out var wp2), Is.Null);
        Assert.That(wp2.IsFists, Is.True, "空槽页即拳");
        var full = ArmedFight(34, out _, out _);
        Assert.That(E.WeaponOnPage(full, 1, out var a1)!.ItemId, Is.EqualTo("stone_knife"));
        Assert.That(E.WeaponOnPage(full, 2, out var a2)!.ItemId, Is.EqualTo("wooden_spear"));
        Assert.That(a1.IsFists || a2.IsFists, Is.False, "两槽都有武器时不再提供单独徒手页");
        Assert.That(E.BattleOptions(full).Any(o => o.Action == "switch_weapon"), Is.False);
    }

    // ───────────── M101：非法同类双武器整理（COMP08） ─────────────

    private (GameState s, ItemInstance a, ItemInstance b) IllegalPair()
    {
        var s = New();
        var a = E.CreateItems(s, "stone_knife", 1, ItemInstance.EquipOwner(P0, EquipSlots.Weapon1)).Single();
        var b = E.CreateItems(s, "kitchen_knife", 1, ItemInstance.EquipOwner(P0, EquipSlots.Weapon2)).Single();
        return (s, a, b);
    }

    [Test]
    public void COMP08_DetectIllegalSameKind()
    {
        var (s, a, b) = IllegalPair();
        Assert.That(E.DuplicateWeaponsIllegal(s), Is.True);
        Assert.That(E.DuplicateKindWeapons(s).Select(i => i.InstanceId), Is.EqualTo(new[] { a.InstanceId, b.InstanceId }));
        b.Owner = ItemInstance.Carried;
        Assert.That(E.DuplicateWeaponsIllegal(s), Is.False);
    }

    [Test]
    public void COMP08_ResolveNeedsExplicitChoice_FailLeavesStateUnchanged()
    {
        var (s, a, b) = IllegalPair();
        var before = SaveSerializer.Serialize(s);
        var r = E.ResolveDuplicateWeapons(s, P0, a.InstanceId, new Dictionary<int, bool>());
        Assert.That(r.Ok, Is.False);
        Assert.That(r.Reason, Does.Contain("选择去向"), "默认不勾，必须明确选择");
        Assert.That(E.ResolveDuplicateWeapons(s, P0, 9999, new Dictionary<int, bool> { [b.InstanceId] = false }).Ok, Is.False);
        foreach (var id in new[] { "wood", "fiber", "stone" }) E.DebugGive(s, id, 1);
        before = SaveSerializer.Serialize(s);
        r = E.ResolveDuplicateWeapons(s, P0, a.InstanceId, new Dictionary<int, bool> { [b.InstanceId] = false });
        Assert.That(r.Ok, Is.False);
        Assert.That(r.Reason, Does.Contain("卡位不够"));
        Assert.That(SaveSerializer.Serialize(s), Is.EqualTo(before), "失败不改状态");
    }

    [Test]
    public void COMP08_ResolveKeepOne_OthersToCardsOrGround_NoDeleteNoReroll()
    {
        var (s, a, b) = IllegalPair();
        var dur = b.Durability;
        var count = s.Items.Count;
        Assert.That(E.ResolveDuplicateWeapons(s, P0, b.InstanceId, new Dictionary<int, bool> { [a.InstanceId] = false }, "dup-1").Ok, Is.True);
        Assert.That(a.Owner, Is.EqualTo(ItemInstance.Carried));
        Assert.That(b.Owner, Is.EqualTo("equip:p0:weapon2"), "保留的那把留在原槽");
        Assert.That(b.Durability, Is.EqualTo(dur));
        Assert.That(s.Items.Count, Is.EqualTo(count), "不删除");
        Assert.That(E.DuplicateWeaponsIllegal(s), Is.False);

        var (s2, a2, b2) = IllegalPair();
        Assert.That(E.ResolveDuplicateWeapons(s2, P0, a2.InstanceId, new Dictionary<int, bool> { [b2.InstanceId] = true }).Ok, Is.True);
        Assert.That(b2.Owner, Is.EqualTo(s2.CurrentLocation), "明确选择放当前地面");
    }

    [Test]
    public void COMP08_ResolveOnlyAtSafeIdle()
    {
        var (s, a, b) = IllegalPair();
        Start(s, ActionRequest.Wait(30));
        Assert.That(E.ResolveDuplicateWeaponsBlocked(s, P0, a.InstanceId, new Dictionary<int, bool> { [b.InstanceId] = true }), Does.Contain("现在不能整理"));
    }

    // ───────────── 8→9 迁移：旧当前武器进武器Ⅰ ─────────────

    [Test]
    public void Migration8To9_EquippedWeaponMovesToWeapon1_FreesCard()
    {
        var s = New();
        var knife = Give(s, "stone_knife");
        var cards = E.RootCardCount(s);
        var j = B3ActorTests.ToSchema8(s);
        j["EquippedWeaponId"] = knife.InstanceId;
        var m = SaveSerializer.Deserialize(j.ToString(), Cfg, out var notes);
        var mk = m.Items.Single(i => i.InstanceId == knife.InstanceId);
        Assert.That(mk.Owner, Is.EqualTo("equip:p0:weapon1"));
        Assert.That(E.RootCardCount(m), Is.EqualTo(cards - 1), "从五卡根列表移除同一实例，不复制");
        Assert.That(m.Items.Count(i => i.ItemId == "stone_knife"), Is.EqualTo(1));
        Assert.That(mk.Durability, Is.EqualTo(knife.Durability), "不重抽耐久");
        Assert.That(notes.Any(n => n.Contains("武器Ⅰ")), Is.True);
        var json = SaveSerializer.Serialize(m);
        Assert.That(json, Does.Not.Contain("EquippedWeaponId"));
        var back = SaveSerializer.Deserialize(json, Cfg);
        Assert.That(SaveSerializer.Serialize(back), Is.EqualTo(json), "往返不变、不再迁移");
    }

    [Test]
    public void Migration8To9_InvalidReferenceIgnored()
    {
        var s = New();
        var ground = E.CreateItems(s, "stone_knife", 1, s.CurrentLocation).Single();
        foreach (var bad in new[] { 9999, ground.InstanceId, s.Items.First(i => i.ItemId == "lighter").InstanceId })
        {
            var j = B3ActorTests.ToSchema8(s);
            j["EquippedWeaponId"] = bad;
            var m = SaveSerializer.Deserialize(j.ToString(), Cfg);
            Assert.That(m.Items.Any(i => i.IsWorn), Is.False, $"无效引用 {bad} 忽略");
            Assert.That(m.Items.Count, Is.EqualTo(s.Items.Count));
        }
    }

    [Test]
    public void Migration8To9_InBattle_UsesBattleCurrentWeapon()
    {
        var s = Fight(9, "shambler");
        var knife = Give(s, "stone_knife");
        var pistol = Give(s, "pistol");
        s.Battle!.WeaponInstanceId = pistol.InstanceId;
        var j = B3ActorTests.ToSchema8(s);
        j["EquippedWeaponId"] = knife.InstanceId;
        var m = SaveSerializer.Deserialize(j.ToString(), Cfg);
        Assert.That(m.Items.Single(i => i.InstanceId == pistol.InstanceId).Owner, Is.EqualTo("equip:p0:weapon1"), "战斗中的旧档按战内当前武器");
        Assert.That(m.Items.Single(i => i.InstanceId == knife.InstanceId).Owner, Is.EqualTo(ItemInstance.Carried));
        Assert.That(m.Battle!.ActiveWeaponSlot, Is.EqualTo(1));
        Assert.That(E.CurrentWeapon(m, out _).Id, Is.EqualTo("pistol"));
    }
}
