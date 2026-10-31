using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 策划提交 5493951：随身最多五张根物品卡（S06 §8、S07、S10 联动）与 Q19 系统说明按当前武器筛选指令（R03 §7.4）。
/// 验收 S06-A08/A09、S07-A07（内核校验部分）、S10-A07、S14-A72（指令筛选部分）。
/// </summary>
public class V11FiveCardTests : TestBase
{
    private ActionResult Pick(GameState s, string itemId, int n = 1) =>
        E.DoInstant(s, InstantRequest.Pickup(First(s, itemId, s.CurrentLocation).InstanceId, n));

    /// <summary>新局随身锅+打火器，再拾取罐头、木材、纤维凑满五张。</summary>
    private GameState FiveCards()
    {
        var s = New();
        Assert.That(Pick(s, "canned_food").Ok, Is.True);
        Assert.That(Pick(s, "wood").Ok, Is.True);
        Assert.That(Pick(s, "fiber").Ok, Is.True);
        Assert.That(E.RootCardCount(s), Is.EqualTo(5));
        return s;
    }

    /// <summary>S06-A08：第六张新卡整次拒绝；能合堆不增根可取；超堆叠拆出新堆也算新卡；重量也须合格。</summary>
    [Test]
    public void S06_A08_SixthCardRejected_MergeAllowed_OverflowSplitCounts()
    {
        var s = FiveCards();
        var grams = E.CarriedGrams(s);
        var cloth = Pick(s, "cloth");
        Assert.That(cloth.Ok, Is.False, "新物品会成第六张卡");
        Assert.That(cloth.Reason, Does.Contain("随身卡位已满（5/5张）"));
        Assert.That(Count(s, "cloth", "camp"), Is.EqualTo(2), "来源不先改变");
        Assert.That(E.CarriedGrams(s), Is.EqualTo(grams));

        Assert.That(Pick(s, "wood").Ok, Is.True, "并进随身已有木材堆，不增根");
        Assert.That(E.RootCardCount(s), Is.EqualTo(5));

        // 木材堆满20后再拾1份：拆出新堆=第六张，整次拒绝
        E.DebugGive(s, "wood", 30, "camp");
        var head = First(s, "wood", "camp");
        Assert.That(E.MaxPickup(s, head), Is.EqualTo(18), "随身2份，再取18份到满堆");
        var over = E.DoInstant(s, InstantRequest.Pickup(head.InstanceId, 19));
        Assert.That(over.Ok, Is.False);
        Assert.That(over.Reason, Does.Contain("随身卡位已满").And.Contain("最多可取18份"));
        Assert.That(Count(s, "wood", ItemInstance.Carried), Is.EqualTo(2), "整次拒绝，不截量");

        // 拖拽落点与按钮同一校验
        var intent = E.ResolveDrop(s, DropSource.Parse("card:camp|cloth")!, DropTarget.Parse("zone:carried", 0)!);
        Assert.That(intent.Ok, Is.False);
        Assert.That(intent.Reason, Does.Contain("随身卡位已满"));
    }

    /// <summary>S06-A09/S10-A07：锅连同内容只算1张；锅里的水/食材取到随身成为新卡需空位，失败不改归属/进度；能合堆可取；用掉最后一份才释放卡位。</summary>
    [Test]
    public void S06_A09_S10_A07_PotChildNeedsSlot_FailKeepsOwnerAndProgress_ConsumeFrees()
    {
        var s = FiveCards();
        var pot = First(s, "pot");
        Assert.That(E.DoInstant(s, InstantRequest.FillPot(First(s, "clean_water", "camp").InstanceId, pot.InstanceId, 2)).Ok, Is.True);
        Assert.That(E.RootCardCount(s), Is.EqualTo(5), "锅与锅里的水合计1张");
        var water = E.PotLiquids(s, pot.InstanceId).Single();
        var dec = E.DoInstant(s, InstantRequest.Decant(water.InstanceId, ItemInstance.Carried));
        Assert.That(dec.Ok, Is.False, "随身锅里的水取到随身会成第六张卡（Q11 箭头）");
        Assert.That(dec.Reason, Does.Contain("随身卡位已满"));
        Assert.That(E.PotUnits(s, pot.InstanceId), Is.EqualTo(2), "失败不改变锅内容");
        Assert.That(E.DoInstant(s, InstantRequest.Decant(water.InstanceId, "camp")).Ok, Is.True, "显式倒到地面不受卡位限制");

        // 用掉最后一份罐头才释放卡位
        Assert.That(E.Start(s, ActionRequest.UseItem("canned_food")).Ok, Is.True);
        while (s.Main != null) E.Advance(s);
        Assert.That(E.RootCardCount(s), Is.EqualTo(4), "最后一份用掉释放卡位");
        Assert.That(E.DoInstant(s, InstantRequest.Decant(E.PotLiquids(s, pot.InstanceId).Single().InstanceId, ItemInstance.Carried)).Ok, Is.True);
        Assert.That(E.RootCardCount(s), Is.EqualTo(5));

        // 火堆里受热中的食材：随身满5张时取到随身整次拒绝，热进度与保护不变
        var t = New();
        var tpot = First(t, "pot");
        E.DebugGive(t, "clean_water", 2, "camp");
        Assert.That(E.DoInstant(t, InstantRequest.FillPot(First(t, "clean_water", "camp").InstanceId, tpot.InstanceId, 2)).Ok, Is.True);
        Assert.That(E.DoInstant(t, InstantRequest.PutInFire(tpot.InstanceId)).Ok, Is.True);
        E.DebugGive(t, "raw_root", 1, "camp");
        Assert.That(E.DoInstant(t, InstantRequest.PutFoodInPot(First(t, "raw_root", "camp").InstanceId, tpot.InstanceId, 1)).Ok, Is.True);
        var fac = t.Locations["camp"].Facility;
        fac.FireLit = true;
        fac.FuelMinutes = 120;
        Start(t, ActionRequest.Wait(30));
        Steps(t, 2);
        E.Cancel(t);
        var root = E.PotFood(t, tpot.InstanceId).Single();
        var heat = E.ProcessOf(t, root)!.HeatDoneMinutes;
        Assert.That(heat, Is.EqualTo(10));
        foreach (var id in new[] { "canned_food", "wood", "fiber", "cloth" }) Assert.That(Pick(t, id).Ok, Is.True, id);
        Assert.That(E.RootCardCount(t), Is.EqualTo(5));
        var take = E.DoInstant(t, InstantRequest.TakeOut(root.InstanceId, ItemInstance.Carried));
        Assert.That(take.Ok, Is.False);
        Assert.That(take.Reason, Does.Contain("随身卡位已满"));
        Assert.That(root.Owner, Is.EqualTo("pot:" + tpot.InstanceId), "失败不改 owner");
        Assert.That(E.ProcessOf(t, root)!.HeatDoneMinutes, Is.EqualTo(heat), "失败不清进度");
        Assert.That(E.ProcessOf(t, root)!.HeatProtected, Is.True, "失败不先解除保护");
        Assert.That(E.DoInstant(t, InstantRequest.TakeOut(tpot.InstanceId, ItemInstance.Carried)).Ok, Is.False, "取整锅到随身同样要空位");
        Assert.That(E.TakeOutConsequenceOf(t, tpot).RootCardsAfter, Is.EqualTo(6));
    }

    /// <summary>M77-B10：开发测试箱的说明与实际一致（放到随身几张、放在地面几张），随身不超过5张。</summary>
    [Test]
    public void DevKit_TextMatchesPlacement()
    {
        var s = New();
        var before = s.Items.Count(i => i.Owner == s.CurrentLocation);
        var text = E.DebugGiveDevKit(s);
        var carriedNew = E.RootCardCount(s) - 2;
        var groundNew = s.Items.Count(i => i.Owner == s.CurrentLocation) - before;
        Assert.That(E.RootCardCount(s), Is.LessThanOrEqualTo(Cfg.Carry.MaxRootCards));
        Assert.That(text, Does.Contain($"{carriedNew}张放到随身").And.Contain($"放不下的{groundNew}张放在当前地面"));
        Assert.That(s.Log.Last().Text, Is.EqualTo(text), "日志与界面提示同一句");
    }

    /// <summary>弹仓内容不另占卡；武器连同弹仓1张。</summary>
    [Test]
    public void Magazine_DoesNotTakeExtraCard()
    {
        var s = New();
        E.DebugGive(s, "pistol", 1);
        var gun = First(s, "pistol");
        gun.LoadedAmmo = 8;
        gun.LoadedAmmoId = "ammo_9mm";
        Assert.That(E.RootCardCount(s), Is.EqualTo(3));
    }

    /// <summary>Q19：指令一览只列当前武器兼容、技能条件满足的招式；暂时不足保留并写原因；切换武器立即刷新；战斗内外同源。</summary>
    [Test]
    public void Q19_CompatibleCommands_FilterByWeaponAndSkill()
    {
        var s = New();
        var fists = E.CompatibleCommands(s).Select(o => o.Action).ToList();
        Assert.That(fists, Does.Contain("basic_attack").And.Contain("desperate_punch"));
        // B7（A06 §2）：只列本门已解锁的五节点技能；拳1级只有拳击（普通攻击），推挡拳3级
        Assert.That(fists, Does.Not.Contain("blade_03").And.Not.Contain("unarmed_03").And.Not.Contain("firearms_07"));
        Assert.That(fists, Does.Contain("guard").And.Contain("end_turn"));

        E.DebugGive(s, "stone_knife", 1);
        Assert.That(E.Equip(s, GameState.PlayerId, First(s, "stone_knife").InstanceId, EquipSlots.Weapon1).Ok, Is.True);
        var knife = E.CompatibleCommands(s).Select(o => o.Action).ToList();
        Assert.That(E.CompatibleCommands(s).Single(o => o.Action == "basic_attack").Label, Is.EqualTo("斩击"), "换刀后普通攻击是刀剑1级斩击");
        Assert.That(knife, Does.Not.Contain("blade_03"), "刀剑1级：割伤需3级，未满足不列出");
        s.Skills["blade"].Level = 3;
        knife = E.CompatibleCommands(s).Select(o => o.Action).ToList();
        Assert.That(knife, Does.Contain("blade_03"), "等级满足后列出");

        // 霰弹枪：散射只列给霰弹枪；空弹仓保留并写原因
        E.DebugGive(s, "shotgun", 1);
        Assert.That(E.Equip(s, GameState.PlayerId, First(s, "shotgun").InstanceId, EquipSlots.Weapon1).Ok, Is.True, "同槽替换：石刀退回随身");
        s.Skills["firearms"].Level = 7;
        var gun = E.CompatibleCommands(s);
        var scatter = gun.Single(o => o.Action == "firearms_07");
        Assert.That(scatter.TargetMode, Is.EqualTo("area"), "霰弹枪的专长射击打敌方1、2号位");
        Assert.That(scatter.Available, Is.False);
        Assert.That(scatter.Reason, Does.Contain("弹仓空"));
        Assert.That(gun.Any(o => o.Action == "reload"), Is.True);
    }
}
