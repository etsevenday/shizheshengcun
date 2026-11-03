using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 B22 R68：封存战利品账、远征回收箱与原实例遗留物清单。出处：R03 D7「已得物资、沿途奖励与owner」（D7.1 三类账目、D7.2）、D10；
/// R08「地牢封存与遗物归属」；A10 §7（终止确认窗列原实例遗留物）；开发假设 B22-3—B22-5。验收 DR10、DR11、DR12。
/// </summary>
public class B22SealedTests : BattleTestBase
{
    private void Go(GameState s, string to, bool confirm = false)
    {
        Start(s, ActionRequest.Travel(to, confirm));
        Assert.That(Run(s), Is.EqualTo(SegmentStatus.Completed));
        Assert.That(s.CurrentLocation, Is.EqualTo(to));
    }

    /// <summary>01→02（打赢）→04（安全点，探索 100% 揭示两个普通箱）的进行中远征。</summary>
    internal GameState At04(uint seed)
    {
        var s = B21EntryTests.AtBuffer(E, seed);
        UnlockCapacity(s, 2);
        Go(s, "d01_02", true);
        E.DebugWinFixedHere(s);
        E.DebugSetProgress(s, "d01_02", 100);
        Go(s, "d01_04");
        E.DebugSetProgress(s, "d01_04", 100);
        return s;
    }

    private void Gather(GameState s, string id)
    {
        Start(s, ActionRequest.Gather(id));
        Run(s);
    }

    /// <summary>随身塞满五张卡（各一份不同的轻材料）。</summary>
    private void FillCards(GameState s)
    {
        foreach (var it in Cfg.Items.Where(i => i.Kind == "material" && i.WeightGrams <= 100 && i.IsStackable && i.Id != "cloth" && i.Id != "leather"))
        {
            if (E.RootCardCount(s) >= E.MaxRootCards) break;
            if (s.Items.Any(x => x.IsCarried && x.ItemId == it.Id)) continue;
            E.CreateItems(s, it.Id, 1, ItemInstance.Carried);
        }
        Assert.That(E.RootCardCount(s), Is.EqualTo(5));
    }

    /// <summary>
    /// DR10：五卡已满时拿到多个奖励——箱子产物整包落地面（记本实例来源），拾取被五卡拒绝但可以封存；封存不占卡位、暂不能用；
    /// 终止撤出到父地点后一次迁到“远征回收箱”（不塞爆个人包），回收箱取到随身照常验五卡、可放地面。
    /// </summary>
    [Test]
    public void DR10_FullCards_SealThenRecoveryBox()
    {
        var s = At04(2221);
        FillCards(s);
        Gather(s, "d01_04_cache0");
        Gather(s, "d01_04_cache2");
        var cloth = s.Items.Single(i => i.Owner == "d01_04" && i.ItemId == "cloth");
        var leather = s.Items.Single(i => i.Owner == "d01_04" && i.ItemId == "leather");
        Assert.That(cloth.RewardOrigin, Is.EqualTo(s.Expedition!.InstanceId));
        Assert.That(E.PickupBlockedText(s, cloth, cloth.Count), Does.Contain("卡位已满"));
        Assert.That(E.SealableItems(s).Select(i => i.InstanceId), Is.EquivalentTo(new[] { cloth.InstanceId, leather.InstanceId }));
        Assert.That(E.SealReward(s, cloth.InstanceId, "seal-1").Ok, Is.True);
        Assert.That(E.SealReward(s, cloth.InstanceId, "seal-1").Duplicate, Is.True);
        Assert.That(E.SealReward(s, leather.InstanceId).Ok, Is.True);
        Assert.That(E.RootCardCount(s), Is.EqualTo(5), "封存不是本人五卡");
        Assert.That(E.SealedHere(s).Sum(i => i.Count), Is.EqualTo(4));
        Assert.That(cloth.Owner, Is.EqualTo(GameEngine.SealedOwner(s.Expedition.InstanceId)));
        // 封存物在地牢里可以明确取出（放地面不验卡）
        Assert.That(E.UnsealBlocked(s, cloth.InstanceId, false), Does.Contain("卡位已满"));
        Assert.That(E.UnsealBlocked(s, cloth.InstanceId, true), Is.Null);

        var pv = E.AbortPreview(s);
        Assert.That(pv.SealedText, Does.Contain("布料×2").And.Contain("远征回收箱"));
        var carried = s.Items.Where(i => i.IsCarried).Select(i => (i.ItemId, i.Count)).ToList();
        Assert.That(E.AbortExpedition(s, "abort").Ok, Is.True);
        Run(s);
        Assert.That(s.CurrentLocation, Is.EqualTo("c03_crypt"));
        Assert.That(s.Items.Where(i => i.IsCarried).Select(i => (i.ItemId, i.Count)), Is.EquivalentTo(carried), "个人包不变、不复制");
        var box = s.Remains[GameEngine.RecoveryBoxKey("d01")];
        Assert.That((box.LocationId, box.Kind), Is.EqualTo(("c03_crypt", RemainsState.RecoveryKind)));
        var inBox = E.RemainsItems(s, GameEngine.RecoveryBoxKey("d01"));
        Assert.That(inBox.Select(i => (i.ItemId, i.Count)), Is.EquivalentTo(new[] { ("cloth", 2), ("leather", 2) }));
        Assert.That(inBox.All(i => i.RewardOrigin == null), Is.True, "带出后来源标记清掉");
        Assert.That(E.SealedItems(s, E.InstanceId(s, "d01")), Is.Empty, "已转外部即从旧账移除");
        Assert.That(s.Log.Any(l => l.Text.Contains("远征回收箱")), Is.True);
        Assert.That(E.TakeFromRemainsBlocked(s, GameEngine.RecoveryBoxKey("d01"), inBox[0].InstanceId, false), Does.Contain("卡位已满"));
        Assert.That(E.TakeFromRemains(s, GameEngine.RecoveryBoxKey("d01"), inBox[0].InstanceId, true).Ok, Is.True, "可以放地面");
        // 读档不再搬一次
        var t = SaveSerializer.Deserialize(SaveSerializer.Serialize(s), Cfg);
        Assert.That(E.RemainsItems(t, GameEngine.RecoveryBoxKey("d01")).Sum(i => i.Count), Is.EqualTo(E.RemainsItems(s, GameEngine.RecoveryBoxKey("d01")).Sum(i => i.Count)));
    }

    /// <summary>
    /// DR11：没去的侧室箱、没领的箱子不会因撤出而自动领取或补发；已用掉的东西不补回；沿途已拿到的（随身的与封存的）全部保留。
    /// 留在地牢地面的物品列进终止确认窗的“原实例遗留物”（A10 §7），撤出后仍在旧实例里（可重入回收）。
    /// </summary>
    [Test]
    public void DR11_UnclaimedNotAuto_ConsumedNotRestored_LeftoversListed()
    {
        var s = At04(2222);
        Gather(s, "d01_04_cache0");
        var cloth = s.Items.Single(i => i.Owner == "d01_04" && i.ItemId == "cloth");
        Assert.That(E.DoInstant(s, InstantRequest.Pickup(cloth.InstanceId, 1)).Ok, Is.True);
        var food = E.CreateItems(s, "canned_beans", 2, ItemInstance.Carried)[0];
        E.Start(s, ActionRequest.UseInstance(food.InstanceId));
        Run(s);
        Assert.That(s.Items.Where(i => i.IsCarried && i.ItemId == "canned_beans").Sum(i => i.Count), Is.EqualTo(1));
        var pv = E.AbortPreview(s);
        Assert.That(pv.Leftovers, Has.Some.Contains("布料×1（藏书密室地面）").Or.Some.Contains("布料×1"));
        Assert.That(E.AbortExpedition(s).Ok, Is.True);
        Run(s);
        Assert.That(s.Resources["d01_06_cache1"].Available, Is.EqualTo(2), "没去的侧室箱不自动领取");
        Assert.That(s.Resources["d01_04_cache2"].Available, Is.EqualTo(2), "没领的箱子不补发");
        Assert.That(s.Items.Where(i => i.IsCarried && i.ItemId == "canned_beans").Sum(i => i.Count), Is.EqualTo(1), "已吃的不补回");
        Assert.That(s.Items.Where(i => i.IsCarried && i.ItemId == "cloth").Sum(i => i.Count), Is.EqualTo(1), "随身已得保留");
        Assert.That(s.Items.Single(i => i.IsCarried && i.ItemId == "cloth").RewardOrigin, Is.Null, "带出后不再是本实例奖励");
        Assert.That(s.Items.Where(i => i.Owner == "d01_04" && i.ItemId == "cloth").Sum(i => i.Count), Is.EqualTo(1), "留在地牢地面的仍属旧实例");
        Assert.That(E.LeftoverLines(s, "d01"), Has.Some.Contains("布料×1"));
        Assert.That(s.Remains.ContainsKey(GameEngine.RecoveryBoxKey("d01")), Is.False, "没有封存物就不建回收箱");
    }

    /// <summary>DR12：外带钢材、玩家自制物、从回收箱带回地牢的旧奖励都不能封存（不是通用物流）；拒绝不改 owner、不占卡。</summary>
    [Test]
    public void DR12_OutsideOrCrafted_CannotSeal()
    {
        var s = At04(2223);
        var steel = E.CreateItems(s, "steel", 1, ItemInstance.Carried)[0];
        Assert.That(E.SealBlocked(s, steel.InstanceId), Does.Contain("外带物"));
        Assert.That(E.SealReward(s, steel.InstanceId).Ok, Is.False);
        Assert.That(steel.Owner, Is.EqualTo(ItemInstance.Carried));
        var crafted = E.CreateItems(s, "rally_patch", 2, s.Ground)[0];
        Assert.That(E.SealBlocked(s, crafted.InstanceId), Does.Contain("自制物"));
        // 不在远征里（野外）不能封存；别的实例的奖励也不行
        Gather(s, "d01_04_cache0");
        var cloth = s.Items.Single(i => i.Owner == "d01_04" && i.ItemId == "cloth");
        cloth.RewardOrigin = "other:d01:g9";
        Assert.That(E.SealBlocked(s, cloth.InstanceId), Does.Contain("只能封存本次地牢里"));
        var w = New(2224);
        var x = E.CreateItems(w, "cloth", 1, ItemInstance.Carried)[0];
        Assert.That(E.SealBlocked(w, x.InstanceId), Does.Contain("只有在本次远征的地牢里"));
    }

    /// <summary>合堆：本实例奖励与同名外带物分开成两张卡（A07 X1.13“保留批次/来源，不强行压卡”）；带出地牢后合回一张。</summary>
    [Test]
    public void RewardOrigin_SeparateCardInside_MergesAfterLeaving()
    {
        var s = At04(2225);
        E.CreateItems(s, "cloth", 1, ItemInstance.Carried);
        var cards = E.RootCardCount(s);
        Gather(s, "d01_04_cache0");
        var g = s.Items.Single(i => i.Owner == "d01_04" && i.ItemId == "cloth");
        Assert.That(E.DoInstant(s, InstantRequest.Pickup(g.InstanceId, 2)).Ok, Is.True);
        Assert.That(E.RootCardCount(s), Is.EqualTo(cards + 1), "来源不同不合堆");
        E.DebugInstanceFact(s, "d01", GameEngine.RecordClaimedFact);
        Go(s, "d01_02");
        Go(s, "d01_01");
        Go(s, "c03_crypt");
        Assert.That(s.Expedition, Is.Null);
        Assert.That(E.RootCardCount(s), Is.EqualTo(cards), "通关离开后来源清掉、合回一张");
        Assert.That(s.Items.Where(i => i.IsCarried && i.ItemId == "cloth").Sum(i => i.Count), Is.EqualTo(3));
    }
    /// <summary>
    /// 试玩修复 M408（A1）：地面上无来源布料与本实例战利品布料是两张卡（卡片键 owner|cloth 与 owner|cloth|dr，战利品紧跟在后）；
    /// 拖动战利品卡取的是战利品那一堆，批次数量不混算；带出地牢后合回一张。
    /// </summary>
    [Test]
    public void M408_RewardStack_SeparateCardKey_DragHead()
    {
        var s = At04(2226);
        E.CreateItems(s, "cloth", 2, "d01_04");
        Gather(s, "d01_04_cache0");
        Assert.That(E.NaturalCards(s, "d01_04").Where(k => k.Contains("|cloth")), Is.EqualTo(new[] { "d01_04|cloth", "d01_04|cloth|dr" }));
        var drHead = E.CardHead(s, DropSource.Parse("card:d01_04|cloth|dr")!)!;
        var plainHead = E.CardHead(s, DropSource.Parse("card:d01_04|cloth")!)!;
        Assert.That(drHead.RewardOrigin, Is.EqualTo(s.Expedition!.InstanceId));
        Assert.That(plainHead.RewardOrigin, Is.Null);
        Assert.That((E.CardBatchCount(s, drHead), E.CardBatchCount(s, plainHead)), Is.EqualTo((2, 2)));
        Assert.That(E.CardKey(drHead), Is.EqualTo("d01_04|cloth|dr"));
        Assert.That(E.SealableItems(s).Select(i => i.InstanceId), Is.EqualTo(new[] { drHead.InstanceId }), "只有战利品那一堆可封存");
    }
    /// <summary>
    /// 试玩修复 M409（A2）：战利品卡拖到随身空白处、拖到随身别的物品卡（鼓舞提示贴）上、随身战利品卡拖回地面空白处——
    /// 落点给出的实例都是战利品那一堆，网页按它的真实批次键（含来源）转移，同名普通物品不动。
    /// </summary>
    [Test]
    public void M409_DropToZoneOrOtherCard_MovesRewardSideOnly()
    {
        var s = At04(2227);
        E.CreateItems(s, "cloth", 1, "d01_04");
        E.CreateItems(s, "cloth", 4, ItemInstance.Carried);
        E.CreateItems(s, "rally_patch", 1, ItemInstance.Carried);
        Gather(s, "d01_04_cache0");
        var src = DropSource.Parse("card:d01_04|cloth|dr")!;
        foreach (var dst in new[] { DropTarget.Parse("zone:carried")!, DropTarget.Parse("card:carried|rally_patch")! })
        {
            var intent = E.ResolveDrop(s, src, dst);
            if (intent.FallThrough) intent = E.ResolveDrop(s, src, DropTarget.Parse("zone:carried")!);
            Assert.That(intent.Ok, Is.True, intent.Reason);
            var head = E.FindItem(s, intent.Instant!.InstanceId)!;
            Assert.That(head.RewardOrigin, Is.Not.Null, "落点给的是战利品那一堆");
            var key = GameEngine.BatchKeyOf(head);
            Assert.That(s.Items.Where(i => i.Owner == "d01_04" && i.ItemId == "cloth" && GameEngine.BatchKeyOf(i) == key).All(i => i.RewardOrigin != null), Is.True,
                "按真实批次键转移只会选到战利品堆（按到期分组会混进普通布料）");
        }
        // 网页同款：按批次键拾取 1 份
        var g = E.CardHead(s, src)!;
        Assert.That(E.DoInstant(s, InstantRequest.Pickup(g.InstanceId, 1)).Ok, Is.True);
        Assert.That(s.Items.Where(i => i.IsCarried && i.ItemId == "cloth" && i.RewardOrigin == null).Sum(i => i.Count), Is.EqualTo(4), "随身普通布料不变");
        Assert.That(s.Items.Where(i => i.Owner == "d01_04" && i.ItemId == "cloth" && i.RewardOrigin == null).Sum(i => i.Count), Is.EqualTo(1), "地面普通布料不变");
        // 随身战利品卡拖到地面空白处
        var back = E.ResolveDrop(s, DropSource.Parse("card:carried|cloth|dr")!, DropTarget.Parse("zone:d01_04")!);
        Assert.That(E.FindItem(s, back.Instant!.InstanceId)!.RewardOrigin, Is.Not.Null);
    }
}
