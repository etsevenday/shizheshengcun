using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 B3a R87：Q40 选 B——当前地面上的合法装备直接穿到本人已开放的相容槽，一次最终状态事务（不先临时进五卡）。
/// 出处：S06/R02/A05「2026-10-31 开发B3答复：Q38—Q42装备与工具」§Q40地面直接穿戴、时机表；验收 EQ-B3-40a/b。
/// </summary>
public class B3aGroundEquipTests : BattleTestBase
{
    private const string P0 = GameState.PlayerId;
    private static readonly string W1 = EquipSlots.Weapon1;
    private static readonly string W2 = EquipSlots.Weapon2;

    private ItemInstance Give(GameState s, string itemId)
    {
        E.DebugGive(s, itemId, 1);
        return s.Items.Where(i => i.IsCarried && i.ItemId == itemId).OrderByDescending(i => i.InstanceId).First();
    }

    private ItemInstance Ground(GameState s, string itemId) => E.CreateItems(s, itemId, 1, s.CurrentLocation).Single();

    /// <summary>随身补满五张卡（木头、纤维、石头各一张起）。</summary>
    private void FillCards(GameState s)
    {
        foreach (var id in new[] { "wood", "fiber", "stone", "cloth" })
        {
            if (E.RootCardCount(s) >= E.MaxRootCards) break;
            E.DebugGive(s, id, 1);
        }
        Assert.That(E.RootCardCount(s), Is.EqualTo(E.MaxRootCards));
    }

    private static string Snap(GameState s) => SaveSerializer.Serialize(s);

    // ───────────── EQ-B3-40a ─────────────

    [Test]
    public void EQB340a_FullCardsEmptySlot_GroundKnifeWorn_NoTemporarySixthCard()
    {
        // S06 算例：本人五格已满、武器槽空且总重 18kg，地面 0.5kg 石刀直接穿 → 五格仍 5、总重 18.5kg
        var s = New();
        E.DebugGive(s, "wood", 20);
        E.DebugGive(s, "stone", (18000 - E.CarriedGrams(s) - 1000) / 500);
        E.DebugGive(s, "fiber", (18000 - E.CarriedGrams(s)) / 100);
        Assert.That(E.RootCardCount(s), Is.EqualTo(5));
        Assert.That(E.CarriedGrams(s), Is.EqualTo(18000));
        var knife = Ground(s, "stone_knife");
        Assert.That(E.Item("stone_knife").WeightGrams, Is.EqualTo(500));
        Assert.That(E.PickupBlockedText(s, knife, 1), Does.Contain("卡位已满"), "先拾取会成第六张——地面直穿不走这条");
        var (grams, cards) = E.EquipOutcome(s, P0, knife, W1, false);
        Assert.That((grams, cards), Is.EqualTo((18500, 5)), "预览给最终重量与实际 N/5");
        var minute = s.GameMinute;
        var dur = knife.Durability;
        Assert.That(E.Equip(s, P0, knife.InstanceId, W1, "g-1").Ok, Is.True);
        Assert.That(knife.Owner, Is.EqualTo(ItemInstance.EquipOwner(P0, W1)));
        Assert.That(E.RootCardCount(s), Is.EqualTo(5));
        Assert.That(E.CarriedGrams(s), Is.EqualTo(18500));
        Assert.That(knife.Durability, Is.EqualTo(dur), "不重抽");
        Assert.That(s.GameMinute, Is.EqualTo(minute), "0 分钟");
        Assert.That(s.Log.Last().Text, Does.Contain("地面装备石刀#"));
    }

    [Test]
    public void EQB340a_SwapWithFullCards_DefaultRejected_ExplicitOldToGroundSucceeds()
    {
        var s = New();
        var spear = Give(s, "wooden_spear");
        Assert.That(E.Equip(s, P0, spear.InstanceId, W1).Ok, Is.True);
        FillCards(s);
        var rifle = Ground(s, "rifle");
        rifle.LoadedAmmo = 3;
        rifle.LoadedAmmoId = E.Weapon("rifle")!.AmmoId;
        var before = Snap(s);
        var grams = E.CarriedGrams(s);

        // 默认旧件回随身 → 第六张：不先换，提示可选“旧件放当前地面”或取消
        var r = E.Equip(s, P0, rifle.InstanceId, W1);
        Assert.That(r.Ok, Is.False);
        Assert.That(r.Reason, Does.Contain("旧件放当前地面"));
        Assert.That(Snap(s), Is.EqualTo(before), "整次拒绝，旧装备不先卸、不自动丢");

        // 玩家明确选择旧件放当前地面
        Assert.That(E.EquipBlocked(s, P0, rifle.InstanceId, W1, oldToGround: true), Is.Null);
        Assert.That(E.Equip(s, P0, rifle.InstanceId, W1, "g-2", oldToGround: true).Ok, Is.True);
        Assert.That(rifle.Owner, Is.EqualTo(ItemInstance.EquipOwner(P0, W1)));
        Assert.That(spear.Owner, Is.EqualTo(s.CurrentLocation), "旧件去玩家选定的当前地面");
        Assert.That(E.RootCardCount(s), Is.EqualTo(5));
        Assert.That(E.CarriedGrams(s), Is.EqualTo(grams + E.ItemGrams(s, rifle) - E.ItemGrams(s, spear)), "按新旧件真实质量净变（含弹仓）");
        Assert.That(rifle.LoadedAmmo, Is.EqualTo(3), "弹仓不变");
        Assert.That(s.Items.Count(i => i.InstanceId == rifle.InstanceId || i.InstanceId == spear.InstanceId), Is.EqualTo(2), "owner 唯一，不复制");
    }

    [Test]
    public void EQB340a_SwapWithRoom_OldBackToCarriedByDefault()
    {
        var s = New();
        var spear = Give(s, "wooden_spear");
        Assert.That(E.Equip(s, P0, spear.InstanceId, W1).Ok, Is.True);
        var cards = E.RootCardCount(s);
        var axe = Ground(s, "hatchet");
        Assert.That(E.EquipOutcome(s, P0, axe, W1, false).Cards, Is.EqualTo(cards + 1));
        Assert.That(E.Equip(s, P0, axe.InstanceId, W1).Ok, Is.True);
        Assert.That(spear.Owner, Is.EqualTo(ItemInstance.Carried), "旧件默认回本人随身");
        Assert.That(E.RootCardCount(s), Is.EqualTo(cards + 1));
        Assert.That(s.Log.Last().Text, Does.Contain("回到随身"));
    }

    // ───────────── EQ-B3-40b ─────────────

    [Test]
    public void EQB340b_Over30kg_WholeTransactionFails()
    {
        var s = New();
        E.DebugGive(s, "stone", (29000 - E.CarriedGrams(s)) / 500);
        var rifle = Ground(s, "rifle");
        var before = Snap(s);
        var r = E.Equip(s, P0, rifle.InstanceId, W1, "g-heavy");
        Assert.That(r.Ok, Is.False);
        Assert.That(r.Reason, Does.Contain("将超过30公斤"));
        Assert.That(Snap(s), Is.EqualTo(before), "失败不改任何状态（含不记 requestId）");
        // 换下更重的旧件放地，最终不超过 30 公斤即可
        var shotgun = Give(s, "shotgun");
        E.DoInstant(s, InstantRequest.Drop(s.Items.First(i => i.IsCarried && i.ItemId == "stone").InstanceId, 7));
        Assert.That(E.Equip(s, P0, shotgun.InstanceId, W1).Ok, Is.True);
        var g = E.CarriedGrams(s);
        Assert.That(g + E.ItemGrams(s, rifle) > 30000, Is.True, "直接加上会超线");
        Assert.That(g + E.ItemGrams(s, rifle) - E.ItemGrams(s, shotgun) <= 30000, Is.True);
        Assert.That(E.EquipBlocked(s, P0, rifle.InstanceId, W1), Is.Not.Null, "旧件回随身时既超卡位也超重");
        Assert.That(E.Equip(s, P0, rifle.InstanceId, W1, null, oldToGround: true).Ok, Is.True, "按最终重量校验");
    }

    [Test]
    public void EQB340b_SameKindAndLocks_Rejected_StateUnchanged()
    {
        var s = New();
        var kitchen = Give(s, "kitchen_knife");
        Assert.That(E.Equip(s, P0, kitchen.InstanceId, W2).Ok, Is.True);
        var stone = Ground(s, "stone_knife");
        var before = Snap(s);
        Assert.That(E.Equip(s, P0, stone.InstanceId, W1).Reason, Does.StartWith("已装备同类型武器：厨房刀"), "不能靠地面入口绕过同类型限制");
        Assert.That(Snap(s), Is.EqualTo(before));
        // 同槽替换同类合法（旧件回随身）
        Assert.That(E.EquipBlocked(s, P0, stone.InstanceId, W2), Is.Null);

        var spear = Ground(s, "wooden_spear");
        spear.ReservedBy = 77;
        Assert.That(E.EquipBlocked(s, P0, spear.InstanceId, W1), Does.Contain("预留"));
        spear.ReservedBy = null;

        // 地面操作条件：行动进行中、敌踪待选都不能从地面穿或把旧件放地（不借 Q42 放开）
        Start(s, ActionRequest.Wait(30));
        Assert.That(E.EquipBlocked(s, P0, spear.InstanceId, W1), Does.Contain("行动进行中，不能从地面穿上"));
        Assert.That(E.EquipBlocked(s, P0, kitchen.InstanceId, W1), Does.Contain("已装备在武器Ⅱ"));
        E.Cancel(s);
        s.Encounter = new PendingEncounter { LocationId = s.CurrentLocation, EnemyIds = { }, Source = "explore", Minute = s.GameMinute };
        Assert.That(E.EquipBlocked(s, P0, spear.InstanceId, W1), Does.Contain("发现敌踪"));
        var carriedSpear = Give(s, "metal_spear");
        Assert.That(E.EquipBlocked(s, P0, carriedSpear.InstanceId, W1), Is.Null, "敌踪待选时随身→穿戴仍可");
        Assert.That(E.EquipBlocked(s, P0, carriedSpear.InstanceId, W2, oldToGround: true), Does.Contain("发现敌踪"), "旧件放地也要地面条件");
        s.Encounter = null;

        // 其他地点、他人身上的不算当前地面
        var away = E.CreateItems(s, "wooden_spear", 1, "field").Single();
        Assert.That(E.EquipBlocked(s, P0, away.InstanceId, W1), Does.Contain("只能穿上本人随身或当前地面"));
        var other = E.CreateItems(s, "wooden_spear", 1, ItemInstance.EquipOwner("p1", W1)).Single();
        Assert.That(E.EquipBlocked(s, P0, other.InstanceId, W1), Does.Contain("别人身上"));
    }

    [Test]
    public void EQB340b_DoubleClickIdempotent_StalePreviewRevalidated()
    {
        var s = New();
        var knife = Ground(s, "stone_knife");
        Assert.That(E.Equip(s, P0, knife.InstanceId, W1, "dbl").Ok, Is.True);
        var after = Snap(s);
        var again = E.Equip(s, P0, knife.InstanceId, W1, "dbl");
        Assert.That(again.Ok && again.Duplicate, Is.True, "同一请求号只执行一次");
        Assert.That(Snap(s), Is.EqualTo(after));
        Assert.That(E.Equip(s, P0, knife.InstanceId, W1, "dbl-2").Reason, Does.Contain("已经装备在武器Ⅰ"), "第二次点击不重复穿");

        // 陈旧预览：预览时合法，确认前卡位变满 → 确认时按当时状态重算，整次失败
        var spear = Ground(s, "wooden_spear");
        Assert.That(E.EquipBlocked(s, P0, spear.InstanceId, W1), Is.Null);
        FillCards(s);
        var before = Snap(s);
        Assert.That(E.Equip(s, P0, spear.InstanceId, W1, "stale").Ok, Is.False);
        Assert.That(Snap(s), Is.EqualTo(before), "无半卸半穿");
        Assert.That(E.Equip(s, P0, spear.InstanceId, W1, "stale", oldToGround: true).Ok, Is.True, "失败的请求号不占用，改选后可提交");
        Assert.That(knife.Owner, Is.EqualTo(s.CurrentLocation));
    }

    [Test]
    public void EQB340b_InBattleOrDialogue_GroundEquipBlocked()
    {
        var b = Fight(401, "shambler");
        var g = Ground(b, "stone_knife");
        Assert.That(E.EquipBlocked(b, P0, g.InstanceId, W1), Does.Contain("战斗中"));
        var story = NewStory();
        var k = Ground(story, "stone_knife");
        Assert.That(E.EquipBlocked(story, P0, k.InstanceId, W1), Is.EqualTo(GameEngine.DialogueLockedReason));
    }
}
