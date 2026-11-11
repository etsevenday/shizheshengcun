using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// B33 整备与交接（P03a—P03d）：内核只读预演。出处：《重墨 V2 开工裁定与界面逐字稿》B33 Q429—Q435、第二轮 Q448—Q453、Q458—Q462；
/// 05 页面与操作流程 P03；07 UX2-A05—A07；03 INK2-09。验收 Q430-A01、Q432-A01、Q434-A01、Q449-A01、Q451-A01、Q452-A01—A03、Q459-A01、Q461-A01。
/// </summary>
public class B33GearTests : BattleTestBase
{
    private const string P0 = GameState.PlayerId;
    private static readonly string W1 = EquipSlots.Weapon1;
    private static readonly string W2 = EquipSlots.Weapon2;

    private ItemInstance Carry(GameState s, string itemId, string? actor = null) =>
        E.CreateItems(s, itemId, 1, ItemInstance.CarriedOwner(actor ?? P0)).Single();

    private ItemInstance Ground(GameState s, string itemId) => E.CreateItems(s, itemId, 1, s.CurrentLocation).Single();

    private void FillCards(GameState s)
    {
        foreach (var id in new[] { "wood", "fiber", "stone", "cloth", "scrap" })
        {
            if (E.RootCardCount(s) >= E.MaxRootCards) break;
            E.DebugGive(s, id, 1);
        }
        Assert.That(E.RootCardCount(s), Is.EqualTo(E.MaxRootCards));
    }

    private static string Snap(GameState s) => SaveSerializer.Serialize(s);

    /// <summary>Q430-A01、Q452-A01/A02：单件两列沿详情原句与注记，缺项「—」；全身只列变化项，组名一次，真实 0／×1 照写；预演不改状态。</summary>
    [Test]
    public void Q430_Q452_CompareRowsAndTotals_ReadOnly()
    {
        var s = New();
        var vest = Carry(s, "padded_vest");
        Assert.That(E.Equip(s, P0, vest.InstanceId, EquipSlots.Armor).Ok, Is.True);
        var scout = Carry(s, "scout_armor");
        var before = Snap(s);
        var p = E.EquipPreview(s, P0, scout.InstanceId, EquipSlots.Armor, null)!;
        Assert.That(Snap(s), Is.EqualTo(before), "只读预演不留任何改动");
        Assert.That(p.Old!.InstanceId, Is.EqualTo(vest.InstanceId));
        Assert.That(p.Source, Is.EqualTo("本人随身"));
        var fixedRow = p.Rows.Single(r => r.Label == "固定效果");
        Assert.That(fixedRow.Current, Does.Contain("（生效中）"));
        Assert.That(fixedRow.Candidate, Does.Contain("（穿戴后生效）"));
        // 护甲 0.1 → 0.2、速度 0 → +1：只列变化项，未变的命中等不列
        Assert.That(p.CombatChanges, Is.EquivalentTo(new[] { "速度 0 → +1", "护甲 0.1 → 0.2" }));
        Assert.That(p.CombatNotes, Is.EqualTo(new[] { "战斗中生效" }));
        Assert.That(p.SurvivalChanges, Is.Empty);
        Assert.That(p.SurvivalNotes, Is.Empty, "空组不显示，说明也不出");
        Assert.That(p.PausedChange, Is.Null);
        // 两处都合法默认本人随身
        Assert.That((p.CanToCarried, p.CanToGround, p.OldToGround, p.Blocked), Is.EqualTo((true, true, (bool?)false, (string?)null)));

        // 求生：×1 是真实值，不写「—」；工具维护缺失写「—」并带（替代，不相加）
        var shirt = Carry(s, "work_shirt");
        var q = E.EquipPreview(s, P0, shirt.InstanceId, EquipSlots.Top, null)!;
        Assert.That(q.Old, Is.Null);
        Assert.That(q.Rows.Single(r => r.Label == "固定效果").Current, Is.EqualTo("—"), "空槽一侧缺项写「—」");
        Assert.That(q.SurvivalChanges, Is.EqualTo(new[] { "制造/建造工作量 ×1 → ×0.95" }));
        Assert.That(q.SurvivalNotes.Single(), Does.Contain("工作量合成下限×0.7").And.Contain("负小时率下限×0.75"));
        var manual = Carry(s, "repair_manual");
        var r = E.EquipPreview(s, P0, manual.InstanceId, EquipSlots.SurvivalAccessory, null)!;
        Assert.That(r.SurvivalChanges, Is.EqualTo(new[] { "工具维护恢复 — → 25%（替代，不相加）" }));
    }

    /// <summary>Q452 特殊行：徒手拳击可打位置按真实范围（默认 1号位，护拳绑带 1、2号位）；耐久 0 暂停名单空为「无」。</summary>
    [Test]
    public void Q452_SpecialRows_PunchTargetsAndPausedList()
    {
        var s = New();
        var wraps = Carry(s, "knuckle_wraps");
        var p = E.EquipPreview(s, P0, wraps.InstanceId, EquipSlots.CombatAccessory, null)!;
        Assert.That(p.CombatChanges, Does.Contain("徒手拳击可打位置 1号位 → 1、2号位"));

        var vest = Carry(s, "padded_vest");
        Assert.That(E.Equip(s, P0, vest.InstanceId, EquipSlots.Armor).Ok, Is.True);
        vest.Durability = 0;
        var other = Carry(s, "leather_armor");
        var q = E.EquipPreview(s, P0, other.InstanceId, EquipSlots.Armor, null)!;
        Assert.That(q.PausedChange, Is.EqualTo("效果暂停 软垫背心（防具） → 无").Or.Match("效果暂停 .+（防具） → 无"));
    }

    /// <summary>Q431-A02：攻击类按目标武器槽预演，武器Ⅰ／Ⅱ分别重算；默认比较武器Ⅰ。</summary>
    [Test]
    public void Q431_WeaponTargetSlot_DefaultW1AndPerSlotPreview()
    {
        var s = New();
        var knife = Carry(s, "stone_knife");
        Assert.That(E.DefaultCompareSlot(knife.ItemId), Is.EqualTo(W1));
        Assert.That(E.DefaultCompareSlot("wood"), Is.Null, "非装备不进比较");
        var p1 = E.EquipPreview(s, P0, knife.InstanceId, W1, null)!;
        var p2 = E.EquipPreview(s, P0, knife.InstanceId, W2, null)!;
        Assert.That(p1.Slot, Is.EqualTo(W1));
        Assert.That(p2.Slot, Is.EqualTo(W2));
        Assert.That(p1.Blocked, Is.Null);
        Assert.That(p2.Blocked, Is.Null);
    }

    /// <summary>Q434-A01、Q452-A03、Q461-A01：本人随身放不下且地面合法时只列地面、不自动选中，写正式句并按回随身预演（可超限）；点地面后重算并可确认。</summary>
    [Test]
    public void Q452_Q461_OldGoesToGroundOnlyWhenCarriedFull()
    {
        var s = New();
        var vest = Carry(s, "padded_vest");
        Assert.That(E.Equip(s, P0, vest.InstanceId, EquipSlots.Armor).Ok, Is.True);
        var scout = Ground(s, "scout_armor");
        FillCards(s);
        var p = E.EquipPreview(s, P0, scout.InstanceId, EquipSlots.Armor, null)!;
        Assert.That(p.Source, Is.EqualTo("当前地面"));
        Assert.That((p.CanToCarried, p.CanToGround, p.OldToGround), Is.EqualTo((false, true, (bool?)null)));
        Assert.That(p.CardsAfter, Is.EqualTo(6), "未选时按旧件回随身预演，可超限");
        Assert.That(p.Blocked, Is.EqualTo($"随身卡位不足，无法收下换下的{E.ItemName("padded_vest")}（更换后6/5）。请选择「当前地面」，或先腾出随身卡位。"));
        var before = Snap(s);
        var g = E.EquipPreview(s, P0, scout.InstanceId, EquipSlots.Armor, true)!;
        Assert.That(Snap(s), Is.EqualTo(before));
        Assert.That((g.OldToGround, g.CardsAfter, g.Blocked), Is.EqualTo(((bool?)true, 5, (string?)null)));
        // 选了不合法的随身：按未选处理，仍不能确认
        var c = E.EquipPreview(s, P0, scout.InstanceId, EquipSlots.Armor, false)!;
        Assert.That(c.OldToGround, Is.Null);
        Assert.That(c.Blocked, Does.StartWith("随身卡位不足"));
        // 地面也不合法（行动中）：沿真实原因，不给地面建议
        Start(s, ActionRequest.Wait(30));
        var w = E.EquipPreview(s, P0, scout.InstanceId, EquipSlots.Armor, null)!;
        Assert.That(w.CanToGround, Is.False);
        Assert.That(w.Blocked, Does.Not.StartWith("随身卡位不足"));
    }

    /// <summary>Q432-A01、Q451-A01：接收人未选确认禁用并写「请选择接收人。」、只给出物人一侧；满包者可选看双方真实预演和原因；单份无数量行。</summary>
    [Test]
    public void Q432_Q451_GivePreview()
    {
        var s = New();
        E.DebugQuickParty(s, 2);
        var mate = s.Party.First(id => id != P0);
        Assert.That(E.GiveReceivers(s, P0), Is.EqualTo(new[] { mate }));
        var cloth = E.CreateItems(s, "cloth", 3, ItemInstance.Carried)[0];
        var p = E.GivePreview(s, P0, null, cloth.InstanceId, 3);
        Assert.That(p.Blocked, Is.EqualTo("请选择接收人。"));
        Assert.That((p.ShowQuantity, p.Max, p.HasOutcome), Is.EqualTo((true, 3, true)));
        Assert.That(p.FromGrams, Is.EqualTo(E.CarriedGramsOf(s, P0) - E.ItemGrams(s, cloth)));
        var q = E.GivePreview(s, P0, mate, cloth.InstanceId, 2);
        Assert.That(q.Blocked, Is.Null);
        Assert.That(q.ToCards, Is.EqualTo(E.RootCardCountOf(s, mate) + 1));
        Assert.That(E.GivePreview(s, P0, mate, cloth.InstanceId, 4).Blocked, Is.EqualTo("数量无效"));
        var knife = Carry(s, "stone_knife");
        Assert.That(E.GivePreview(s, P0, mate, knife.InstanceId, 1).ShowQuantity, Is.False, "只有1份省略数量行");
        foreach (var id in new[] { "hatchet", "wooden_spear", "wooden_bow", "wooden_shield", "stone" })
            E.CreateItems(s, id, 1, ItemInstance.CarriedOwner(mate));
        var full = E.GivePreview(s, P0, mate, knife.InstanceId, 1);
        Assert.That(full.Blocked, Does.Contain("随身卡位不够"));
        Assert.That(full.ToCards, Is.EqualTo(6), "满包接收人照样给真实最终卡位");
        Assert.That(E.GiveEntryBlocked(s, P0, knife.InstanceId), Is.Null, "接收人满包不算整体受阻");
    }

    /// <summary>Q449-A01：行动、预留、占用时入口整体受阻，取与接收人无关的首条原句。</summary>
    [Test]
    public void Q449_GiveEntryBlocked_OriginalReasons()
    {
        var s = New();
        E.DebugQuickParty(s, 2);
        var cloth = E.CreateItems(s, "cloth", 2, ItemInstance.Carried)[0];
        Assert.That(E.GiveEntryBlocked(s, P0, cloth.InstanceId), Is.Null);
        cloth.ReservedBy = 999;
        Assert.That(E.GiveEntryBlocked(s, P0, cloth.InstanceId), Is.EqualTo("该物品已被预留"));
        cloth.ReservedBy = null;
        Start(s, ActionRequest.Wait(30));
        Assert.That(E.GiveEntryBlocked(s, P0, cloth.InstanceId), Is.EqualTo("行动进行中，不能交接物品"));
    }

    /// <summary>Q451：容器追加句只在真实持有子物品时显示；没有子物品的锅不触发。</summary>
    [Test]
    public void Q451_ContainerNoteOnlyWithChildren()
    {
        var s = New();
        E.DebugQuickParty(s, 2);
        var mate = s.Party.First(id => id != P0);
        var pot = s.Items.FirstOrDefault(i => E.IsPot(i) && i.IsCarried) ?? E.CreateItems(s, "pot", 1, ItemInstance.Carried)[0];
        foreach (var c in s.Items.Where(i => i.Owner == ItemInstance.ContainerOwner(pot.InstanceId)).ToList()) s.Items.Remove(c);
        Assert.That(E.GivePreview(s, P0, mate, pot.InstanceId, 1).ContainerNote, Is.False);
        E.CreateItems(s, "clean_water", 1, ItemInstance.ContainerOwner(pot.InstanceId));
        Assert.That(E.GivePreview(s, P0, mate, pot.InstanceId, 1).ContainerNote, Is.True);
    }

    /// <summary>Q459-A01：位置行主角「随身物品」「穿戴·{槽名}」，同行者「{姓名}随身」「{姓名} · 穿戴·{槽名}」，地面沿真实位置。</summary>
    [Test]
    public void Q459_PlaceText()
    {
        var s = New();
        E.DebugQuickParty(s, 2);
        var mate = s.Party.First(id => id != P0);
        var name = E.ActorDisplayName(s, mate);
        var a = Carry(s, "stone_knife");
        Assert.That(E.GearPlaceText(s, a), Is.EqualTo("随身物品"));
        Assert.That(E.Equip(s, P0, a.InstanceId, W1).Ok, Is.True);
        Assert.That(E.GearPlaceText(s, a), Is.EqualTo("穿戴·武器Ⅰ"));
        var b = Carry(s, "stone_knife", mate);
        Assert.That(E.GearPlaceText(s, b), Is.EqualTo($"{name}随身"));
        Assert.That(E.Equip(s, mate, b.InstanceId, W2).Ok, Is.True);
        Assert.That(E.GearPlaceText(s, b), Is.EqualTo($"{name} · 穿戴·武器Ⅱ"));
        var g = Ground(s, "stone_knife");
        Assert.That(E.GearPlaceText(s, g), Is.EqualTo(E.WherePath(s, g)));
    }
}
