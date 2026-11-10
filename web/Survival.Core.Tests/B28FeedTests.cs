using Newtonsoft.Json.Linq;
using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 B28 同行食饮内核（R12、R13、R23、R45—R47）。合同 assets/20261104/content-feeding-adjudications.json feeding、
/// carried-pot-water-adjudications.json pot、assets/20261105/ui-card-feeding-q252-257.json feeding；验收 PLAN-Q232-01—03、FEED-Q233-01、Q252-257-A05—A08 的内核部分。
/// 界面入口与“同类”筛选属 B29，COMP20 走通属 B30。
/// </summary>
public class B28FeedTests : TestBase
{
    private const string Me = GameState.PlayerId;
    private const string Lin = "npc_lin_zhao";
    private const string Tang = "npc_tang_li";

    /// <summary>营地、C=4、主角与两名同行，全员状态留有余量，敌踪与巡逻关掉。</summary>
    private GameState Party(params string[] ids)
    {
        var s = New();
        s.Progress.Flags.Add("hub_registered");
        s.Progress.Flags.Add("church_route_record");
        s.Progress.Flags.Add("academy_safety_record");
        s.SpawnNextCheck = int.MaxValue;
        s.PatrolNextCheck = int.MaxValue;
        s.GameMinute = At(1, 10, 0);
        foreach (var id in ids.Length == 0 ? new[] { Lin, Tang } : ids) E.DebugRecruit(s, id);
        foreach (var a in s.Actors.Values)
        {
            a.Stats.Satiety = 50;
            a.Stats.Hydration = 50;
            a.Stats.Morale = 50;
        }
        return s;
    }

    private static string Bag(string actor) => ItemInstance.CarriedOwner(actor);

    private ItemInstance Give(GameState s, string item, string owner, int count = 1) => E.CreateItems(s, item, count, owner).Single();

    private ItemInstance Canteen(GameState s, string owner, string? liquid, int ml)
    {
        var c = Give(s, "ration_canteen", owner);
        c.LiquidId = liquid;
        c.LiquidMl = ml;
        return c;
    }

    private (ItemInstance pot, ItemInstance water) PotWater(GameState s, string owner, string liquid, int units)
    {
        var pot = Give(s, "pot", owner);
        return (pot, Give(s, liquid, ItemInstance.ContainerOwner(pot.InstanceId), units));
    }

    private static FeedRequest Req(string target, string source, ItemInstance item, string? id = null) =>
        new FeedRequest { TargetActorId = target, SourceActorId = source, InstanceId = item.InstanceId, RequestId = id };

    private void Rejected(GameState s, FeedRequest f, string reason)
    {
        var before = SaveSerializer.Serialize(s);
        Assert.That(E.FeedBlocked(s, f), Is.EqualTo(reason));
        var res = E.Feed(s, f);
        Assert.That((res.Ok, res.Reason), Is.EqualTo((false, reason)));
        Assert.That(SaveSerializer.Serialize(s), Is.EqualTo(before), "失败不扣物、不改人、不耗时");
    }

    private void Fed(GameState s, FeedRequest f)
    {
        var res = E.Feed(s, f);
        Assert.That(res.Ok, Is.True, res.Reason);
    }

    private static string Doc(params string[] tail)
    {
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "doc", "策划案"))) dir = dir.Parent;
        Assert.That(dir, Is.Not.Null, "找不到仓库根目录");
        return Path.Combine(new[] { dir!.FullName, "doc", "策划案", "系统策划总册 v1.0", "assets" }.Concat(tail).ToArray());
    }

    // ───────────── 17 句与结果句（Q257、Q252-257-A07） ─────────────

    [Test]
    public void A07_Copy17_VerbatimAgainstBothContracts()
    {
        var q = (JObject)JObject.Parse(File.ReadAllText(Doc("20261105", "ui-card-feeding-q252-257.json")))["feeding"]!["copy"]!;
        var c = (JObject)JObject.Parse(File.ReadAllText(Doc("20261104", "content-feeding-adjudications.json")))["feeding"]!["copyQ257"]!;
        Assert.That(FeedCopy.All.Count, Is.EqualTo(17));
        Assert.That(q.Properties().Select(p => p.Name), Is.EquivalentTo(FeedCopy.All.Keys));
        foreach (var kv in FeedCopy.All)
        {
            Assert.That(kv.Value, Is.EqualTo(q[kv.Key]!.Value<string>()), kv.Key);
            Assert.That(kv.Value, Is.EqualTo(c[kv.Key]!.Value<string>()), kv.Key);
        }
    }

    [Test]
    public void A07_ResultFood_PlayerBag_ActualFields()
    {
        var s = Party();
        var can = Give(s, "canned_food", ItemInstance.Carried);
        Fed(s, Req(Lin, Me, can));
        Assert.That(s.Log.Last().Text, Is.EqualTo("林昭食用了罐头×1（取自你的随身物品）：饱食+35。"));
        Assert.That(s.Main!.Label, Is.EqualTo("给林昭食用：罐头×1"));
    }

    [Test]
    public void A07_ResultFood_TargetBag_SourceIsTarget()
    {
        var s = Party();
        Fed(s, Req(Lin, Lin, Give(s, "cooked_root", Bag(Lin))));
        Assert.That(s.Log.Last().Text, Is.EqualTo("林昭食用了烤熟块根×1（取自林昭的随身物品）：饱食+25，水分−2，精神+2。"));
    }

    [Test]
    public void A07_Change_IsClampedAtTarget_NotPlayerBenefit()
    {
        var s = Party();
        var lin = s.Actors[Lin];
        lin.Stats.Satiety = 90;
        lin.Stats.Hydration = 1;
        Fed(s, Req(Lin, Me, Give(s, "cooked_meat", ItemInstance.Carried)));
        Assert.That(s.Log.Last().Text, Is.EqualTo("林昭食用了烤肉×1（取自你的随身物品）：饱食+10，水分−1，精神+2。"));
        Assert.That((lin.Stats.Satiety, lin.Stats.Hydration), Is.EqualTo((100.0, 0.0)));
    }

    [Test]
    public void A07_Change_TargetAtCap_ActualZero_FieldNotEmpty()
    {
        var s = Party();
        s.Actors[Lin].Stats.Satiety = 100;
        Fed(s, Req(Lin, Me, Give(s, "canned_food", ItemInstance.Carried)));
        Assert.That(s.Log.Last().Text, Is.EqualTo("林昭食用了罐头×1（取自你的随身物品）：饱食无变化。"), "Q268：实际变化为 0 写“{属性名}无变化”，不留空字段");
    }

    // ───────────── 水卡与水壶（PLAN-Q232-02） ─────────────

    [Test]
    public void PLAN02_CleanWaterCard_TargetDrinks_OneCard_FiveMinutesOnce()
    {
        var s = Party();
        var w = Give(s, "clean_water", ItemInstance.Carried, 2);
        var (lin, tang, me) = (s.Actors[Lin], s.Actors[Tang], s.Actors[Me]);
        var minute = s.GameMinute;
        Fed(s, Req(Lin, Me, w));
        Assert.That(lin.Stats.Hydration, Is.EqualTo(80), "效果确认时立即给目标");
        Assert.That(me.Stats.Hydration, Is.EqualTo(50), "主角不享效果");
        Assert.That(w.Count, Is.EqualTo(1), "只扣所选来源一份 500 毫升");
        Assert.That(s.GameMinute, Is.EqualTo(minute), "确认那一刻还没走时间");
        Assert.That(s.Log.Last().Text, Is.EqualTo("林昭饮用了净水×1（取自你的随身物品）：水分+30。"));

        Assert.That(Run(s), Is.EqualTo(SegmentStatus.Completed));
        Assert.That(s.GameMinute, Is.EqualTo(minute + 5), "世界只推进一次 5 分钟");
        Assert.That(s.Main, Is.Null);
        var drop = 50 - me.Stats.Hydration;
        Assert.That(drop, Is.GreaterThan(0), "其他人按等待自然消耗");
        Assert.That(50 - tang.Stats.Hydration, Is.EqualTo(drop).Within(1e-9), "旁人与主角同一等待消耗");
        Assert.That(80 - lin.Stats.Hydration, Is.EqualTo(drop).Within(1e-9), "目标同样按等待消耗一次");
    }

    [Test]
    public void PLAN02_Canteen_TargetBag_250Only_KeepsOwner()
    {
        var s = Party();
        var c = Canteen(s, Bag(Lin), "clean_water", 1000);
        var cards = Count(s, "clean_water");
        Fed(s, Req(Lin, Lin, c));
        Assert.That(s.Actors[Lin].Stats.Hydration, Is.EqualTo(65));
        Assert.That((c.LiquidMl, c.Owner), Is.EqualTo((750, Bag(Lin))), "只喝 250，不清空整壶，不转移");
        Assert.That(s.Log.Last().Text, Is.EqualTo("林昭饮用了分量水壶内的净水250毫升（取自林昭的随身物品）：水分+15；壶内剩余净水 750/2000毫升。"));
        Assert.That(Count(s, "clean_water"), Is.EqualTo(cards), "不先造一份水卡");
    }

    [Test]
    public void Canteen_LastSip_EmptyLabel_DirtyDiarrheaOnTarget()
    {
        var s = Party();
        var c = Canteen(s, ItemInstance.Carried, "dirty_water", 250);
        Fed(s, Req(Tang, Me, c));
        Assert.That(s.Log.Last().Text, Is.EqualTo("唐梨饮用了分量水壶内的脏水250毫升（取自你的随身物品）：水分+15，出现腹泻（12小时）；壶内剩余空 0/2000毫升。"));
        Assert.That(s.Actors[Tang].DiarrheaUntil, Is.EqualTo(s.GameMinute + Cfg.ItemActions.DiarrheaMinutes));
        Assert.That(s.Actors[Me].DiarrheaUntil, Is.Null);
    }

    [Test]
    public void Canteen_EmptyOrShort_ExistingSentences()
    {
        var s = Party();
        Rejected(s, Req(Lin, Me, Canteen(s, ItemInstance.Carried, null, 0)), "水壶是空的");
        Rejected(s, Req(Lin, Me, Canteen(s, ItemInstance.Carried, "clean_water", 100)), "壶里不足250毫升");
    }

    [Test]
    public void PLAN03_DirtyWaterCard_DiarrheaOnTarget_ExtraLossOnTargetOnly()
    {
        var s = Party();
        Fed(s, Req(Lin, Me, Give(s, "dirty_water", ItemInstance.Carried)));
        Assert.That(s.Log.Last().Text, Is.EqualTo("林昭饮用了脏水×1（取自你的随身物品）：水分+30，出现腹泻（12小时）。"));
        Assert.That(s.Actors[Lin].DiarrheaUntil, Is.Not.Null);
        Assert.That(s.Actors[Me].DiarrheaUntil, Is.Null);
        Run(s);
        Assert.That(80 - s.Actors[Lin].Stats.Hydration, Is.GreaterThan(50 - s.Actors[Tang].Stats.Hydration), "腹泻额外失水只落目标");
    }

    // ───────────── 随身锅（FEED-Q233-01、Q233 boundaries） ─────────────

    [Test]
    public void Q233_PotWater_PlayerPot_500Once_PotStays_NoCard()
    {
        var s = Party();
        var (pot, water) = PotWater(s, ItemInstance.Carried, "clean_water", 2);
        var minute = s.GameMinute;
        var cards = s.Items.Where(i => i.ItemId == "clean_water" && !i.InContainer).Sum(i => i.Count);
        Fed(s, Req(Lin, Me, water));
        Assert.That(s.Log.Last().Text, Is.EqualTo($"林昭饮用了净水×1（取自你的随身锅）：水分+30。"));
        Assert.That(s.Actors[Lin].Stats.Hydration, Is.EqualTo(80));
        Assert.That(E.PotUnits(s, pot.InstanceId), Is.EqualTo(1), "只扣这口锅 500 毫升");
        Assert.That(s.Items.Where(i => i.ItemId == "clean_water" && !i.InContainer).Sum(i => i.Count), Is.EqualTo(cards), "不生成临时水卡");
        Run(s);
        Assert.That(s.GameMinute, Is.EqualTo(minute + 5));
    }

    [Test]
    public void Q233_PotWater_TargetPot_ByPotAndKind_Empties_PotKeepsOwner()
    {
        var s = Party();
        var (pot, _) = PotWater(s, Bag(Lin), "dirty_water", 1);
        var f = new FeedRequest { TargetActorId = Lin, SourceActorId = Lin, PotInstanceId = pot.InstanceId, WaterItemId = "dirty_water" };
        Fed(s, f);
        Assert.That(s.Log.Last().Text, Is.EqualTo($"林昭饮用了脏水×1（取自林昭的随身锅）：水分+30，出现腹泻（12小时）。"));
        Assert.That((E.FindItem(s, pot.InstanceId)?.Owner, E.PotUnits(s, pot.InstanceId)), Is.EqualTo((Bag(Lin), 0)), "空锅留原 owner");
        Assert.That(s.Actors[Lin].DiarrheaUntil, Is.Not.Null, "净水不因容器变种，脏水仍腹泻");
        Run(s);
        Rejected(s, f, "锅内脏水不足500毫升，无法饮用1份。");
    }

    [Test]
    public void Q233_PotWater_PartlyReserved_BelowOnePortion_Insufficient()
    {
        var s = Party();
        var (pot, water) = PotWater(s, ItemInstance.Carried, "clean_water", 1);
        s.Locations["camp"].Facility.Processes.Add(new FacilityProcess { RecipeId = "boil_root", TargetInstanceId = 99999, PotInstanceId = pot.InstanceId, ReservedWaterMl = 250 });
        Rejected(s, Req(Lin, Me, water), "锅内净水不足500毫升，无法饮用1份。");
    }

    [Test]
    public void Q233_PotWater_FullyReserved_ExistingReservedSentence()
    {
        var s = Party();
        var (pot, water) = PotWater(s, ItemInstance.Carried, "clean_water", 1);
        var root = Give(s, "raw_root", ItemInstance.ContainerOwner(pot.InstanceId));
        s.Locations["camp"].Facility.Processes.Add(new FacilityProcess { RecipeId = "boil_root", TargetInstanceId = root.InstanceId, PotInstanceId = pot.InstanceId, ReservedWaterMl = 500 });
        Rejected(s, Req(Lin, Me, water), E.ReservedWaterText(s, water));
        Assert.That(E.ReservedWaterText(s, water), Does.Contain("毫升供生块根"));
    }

    [Test]
    public void Q233_PotFood_OnlySelectedPortion_PotSentence()
    {
        var s = Party();
        var pot = Give(s, "pot", ItemInstance.Carried);
        var inPot = ItemInstance.ContainerOwner(pot.InstanceId);
        var a = Give(s, "boiled_root", inPot);
        var b = Give(s, "boiled_root", inPot);
        Fed(s, new FeedRequest { TargetActorId = Tang, SourceActorId = Me, InstanceId = b.InstanceId, PotInstanceId = pot.InstanceId });
        Assert.That(s.Log.Last().Text, Is.EqualTo($"唐梨食用了水煮块根×1（取自你的随身锅）：饱食+23，水分+5，精神+3。"));
        Assert.That(E.FindItem(s, b.InstanceId), Is.Null);
        Assert.That(E.FindItem(s, a.InstanceId), Is.Not.Null, "同锅另一份不动");
        Assert.That(E.FindItem(s, pot.InstanceId)!.Owner, Is.EqualTo(ItemInstance.Carried));
    }

    [Test]
    public void Q233_PotFood_LockedByProcess_NotSplitByFeeding()
    {
        var s = Party();
        var pot = Give(s, "pot", ItemInstance.Carried);
        var food = Give(s, "boiled_root", ItemInstance.ContainerOwner(pot.InstanceId));
        s.Locations["camp"].Facility.Processes.Add(new FacilityProcess { RecipeId = "boil_root", TargetInstanceId = food.InstanceId, PotInstanceId = pot.InstanceId });
        Rejected(s, Req(Lin, Me, food), FeedCopy.ProcessingFood);
    }

    [Test]
    public void Q233_PotReservedOrLocked_ContentRejected()
    {
        var s = Party();
        var (pot, water) = PotWater(s, ItemInstance.Carried, "clean_water", 2);
        pot.ReservedBy = 7;
        Rejected(s, Req(Lin, Me, water), "该物品已被预留");
        pot.ReservedBy = null;
        water.ReservedBy = 7;
        Rejected(s, Req(Lin, Me, water), "该物品已被预留");
        water.ReservedBy = null;
        Start(s, ActionRequest.Wait(60));
        s.Main!.Status = ActionStatus.Paused;
        s.Main.LockedItemIds.Add(pot.InstanceId);
        Rejected(s, Req(Lin, Me, water), "该物品已被预留");
    }

    [Test]
    public void Q233_OtherPots_Rejected_ThirdPersonGroundFacilityCabinet()
    {
        var s = Party();
        var why = "只能使用你或林昭随身的食物和饮水。";
        var (_, third) = PotWater(s, Bag(Tang), "clean_water", 2);
        Rejected(s, Req(Lin, Tang, third), why);
        Rejected(s, Req(Lin, Me, third), why);
        Rejected(s, Req(Lin, Lin, third), why);
        var (_, ground) = PotWater(s, s.Ground, "clean_water", 2);
        Rejected(s, Req(Lin, Me, ground), why);
        var (_, fire) = PotWater(s, "facility:camp:fire", "clean_water", 2);
        Rejected(s, Req(Lin, Me, fire), why);
        var (_, cabinet) = PotWater(s, "cabinet:camp", "clean_water", 2);
        Rejected(s, Req(Lin, Me, cabinet), why);
    }

    // ───────────── 配给袋（Q255、Q252-257-A05） ─────────────

    private void Wear(GameState s, string actor) => Give(s, "ration_pouch", ItemInstance.EquipOwner(actor, EquipSlots.SurvivalAccessory));

    [Test]
    public void A05_TargetWears_Times105()
    {
        var s = Party();
        Wear(s, Lin);
        Fed(s, Req(Lin, Me, Give(s, "canned_food", ItemInstance.Carried)));
        Assert.That(s.Actors[Lin].Stats.Satiety, Is.EqualTo(50 + 35 * 1.05).Within(1e-9));
        Assert.That(s.Log.Last().Text, Does.EndWith("：饱食+36.75。"));
    }

    [Test]
    public void A05_PlayerOrSourceWears_TargetNot_NoBonus()
    {
        var s = Party();
        Wear(s, Me);
        Fed(s, Req(Lin, Me, Give(s, "canned_food", ItemInstance.Carried)));
        Assert.That(s.Actors[Lin].Stats.Satiety, Is.EqualTo(85), "主角兼出物人穿着不代算");
        Assert.That(s.Actors[Me].Stats.Satiety, Is.EqualTo(50));
    }

    [Test]
    public void A05_BothWear_NoDoubleStack_NegativeUntouched()
    {
        var s = Party();
        Wear(s, Me);
        Wear(s, Lin);
        Fed(s, Req(Lin, Me, Give(s, "cooked_root", ItemInstance.Carried)));
        Assert.That(s.Actors[Lin].Stats.Satiety, Is.EqualTo(50 + 25 * 1.05).Within(1e-9), "两人都穿也只 ×1.05 一次");
        Assert.That(s.Actors[Lin].Stats.Hydration, Is.EqualTo(48), "只放大正饱食");
    }

    [Test]
    public void A05_TargetFull_ClampedAtCap_FiveMinutesOnce()
    {
        var s = Party();
        Wear(s, Lin);
        var lin = s.Actors[Lin];
        lin.Stats.Satiety = 80;
        var minute = s.GameMinute;
        var f = Req(Lin, Me, Give(s, "canned_food", ItemInstance.Carried));
        var p = E.FeedPreview(s, f);
        Assert.That(p.Overflow, Is.EqualTo(new[] { "饱食溢出16.75" }), "溢出按目标本人含配给袋算");
        Fed(s, f);
        Assert.That(lin.Stats.Satiety, Is.EqualTo(100));
        Assert.That(s.Log.Last().Text, Does.EndWith("：饱食+20。"));
        Run(s);
        Assert.That(s.GameMinute, Is.EqualTo(minute + 5));
    }

    // ───────────── 物品类型（Q256、Q252-257-A06） ─────────────

    [Test]
    public void A06_NoKnowledgeGate_PlayerNotHungry_LegalFoodOk()
    {
        var s = Party();
        s.Actors[Me].Stats.Satiety = 100;
        s.Actors[Me].Stats.Hydration = 100;
        foreach (var id in new[] { "stew_portion", "herbal_drink", "trail_ration" })
        {
            Fed(s, Req(Lin, Me, Give(s, id, ItemInstance.Carried)));
            Assert.That(s.Log.Last().Text, Does.Contain("食用了"), id);
            Run(s);
        }
    }

    [Test]
    public void A06_RawSpoiledMedicineOthers_OriginalLimits()
    {
        var s = Party();
        var bag = ItemInstance.Carried;
        Rejected(s, Req(Lin, Me, Give(s, "raw_root", bag)), "生块根需先烹饪，不可直接食用");
        Rejected(s, Req(Lin, Me, Give(s, "raw_meat", bag)), "生肉需先烹饪，不可直接食用");
        var old = Give(s, "berry", bag);
        old.ExpiresAt = s.GameMinute;
        Rejected(s, Req(Lin, Me, old), "已腐败，不能食用");
        foreach (var id in new[] { "bandage", "antibiotics", "antiviral_cure", "oral_rehydration", "antidiarrheal_powder", "emergency_tonic", "wood" })
            Rejected(s, Req(Lin, Me, Give(s, id, bag)), FeedCopy.InvalidItem);
        var res = Give(s, "pot", bag);
        Rejected(s, Req(Lin, Me, res), FeedCopy.InvalidItem);
    }

    [Test]
    public void A06_ReservedFood_ExistingSentence()
    {
        var s = Party();
        var food = Give(s, "canned_food", ItemInstance.Carried);
        food.ReservedBy = 3;
        Rejected(s, Req(Lin, Me, food), "该物品已被预留");
    }

    // ───────────── 目标与来源（PLAN-Q232-01/03） ─────────────

    [Test]
    public void Targets_NoneOrPlayer_NoTargetSentence()
    {
        var s = Party();
        var food = Give(s, "canned_food", ItemInstance.Carried);
        Rejected(s, Req("", Me, food), "请先选择一名同行者。");
        Rejected(s, Req(Me, Me, food), "请先选择一名同行者。");
    }

    [Test]
    public void Targets_ReserveRetiredOutsider_InvalidTarget()
    {
        var s = Party(Lin);
        E.AddNamedToRoster(s, Tang, false);
        var food = Give(s, "canned_food", ItemInstance.Carried);
        Rejected(s, Req(Tang, Me, food), "当前无法给唐梨吃喝：目标不在同地点、同区域的同行队伍中。");
        E.RosterOf(s, Lin)!.Retired = true;
        Rejected(s, Req(Lin, Me, food), "当前无法给林昭吃喝：目标不在同地点、同区域的同行队伍中。");
    }

    [Test]
    public void Targets_Dead_DeadSentence_NoRevive()
    {
        var s = Party();
        var r = E.RosterOf(s, Lin)!;
        r.Dead = true;
        s.Party.Remove(Lin);
        Rejected(s, Req(Lin, Me, Give(s, "canned_food", ItemInstance.Carried)), "无法给林昭吃喝：目标已死亡。");
    }

    [Test]
    public void Source_ThirdPersonGroundCabinetSealed_Rejected_NoAutoSwap()
    {
        var s = Party();
        var why = "只能使用你或林昭随身的食物和饮水。";
        var third = Give(s, "canned_food", Bag(Tang));
        Rejected(s, Req(Lin, Tang, third), why);
        Rejected(s, Req(Lin, Me, third), why);
        E.CreateItems(s, "canned_food", 1, s.Ground);
        Rejected(s, Req(Lin, Me, First(s, "canned_food", s.Ground)), why);
        Rejected(s, Req(Lin, Me, Give(s, "canned_food", "cabinet:camp")), why);
        Rejected(s, Req(Lin, Me, Give(s, "canned_food", GameEngine.SealedOwner("d01:1"))), why);
        // 主角随身有同物，点的却是目标包里的：出物人写错不自动换
        var mine = Give(s, "canned_food", ItemInstance.Carried);
        Rejected(s, Req(Lin, Lin, mine), why);
    }

    [Test]
    public void Queries_TargetsAndSources_TwoBagsAndPots_OnlyLegal()
    {
        var s = Party();
        E.AddNamedToRoster(s, "npc_zhou_yan", false);
        Assert.That(E.FeedTargets(s).Select(t => t.Name), Is.EquivalentTo(new[] { "林昭", "唐梨" }), "候补不列");

        var mine = Give(s, "canned_food", ItemInstance.Carried, 3);
        Give(s, "raw_root", ItemInstance.Carried);
        Give(s, "bandage", ItemInstance.Carried);
        var (pot, water) = PotWater(s, ItemInstance.Carried, "clean_water", 2);
        var can = Canteen(s, Bag(Lin), "dirty_water", 750);
        Give(s, "clean_water", Bag(Tang));
        Give(s, "canned_food", s.Ground);

        var rows = E.FeedSources(s, Lin);
        Assert.That(rows.Select(r => (r.SourceName, r.InstanceId, r.Kind, r.Portions, r.PortionMl, r.LiquidMl)), Is.EqualTo(new[]
        {
            ("你", mine.InstanceId, FeedSourceKind.Food, 3, 0, 0),
            ("你", water.InstanceId, FeedSourceKind.PotWater, 2, 500, 1000),
            ("林昭", can.InstanceId, FeedSourceKind.Canteen, 3, 250, 750),
        }));
        Assert.That(rows[1].PotInstanceId, Is.EqualTo(pot.InstanceId));
        Assert.That(rows[0].ExpiresAt, Is.EqualTo(mine.ExpiresAt));
        Assert.That(E.FeedSources(s, "npc_zhou_yan"), Is.Empty, "目标不合法没有来源");
    }

    // ───────────── 时机（合同 feeding.timing） ─────────────

    [Test]
    public void Timing_Battle_Extracting_Encounter_Dialogue()
    {
        var s = Party();
        var f = Req(Lin, Me, Give(s, "canned_food", ItemInstance.Carried));
        s.Encounter = new PendingEncounter { LocationId = "camp", EnemyIds = new List<int>(), Source = "explore", Minute = s.GameMinute };
        Rejected(s, f, "发现敌踪：请先选择接敌或绕行");
        s.Encounter = null;

        s.Expedition = new ExpeditionState { Id = "x", DungeonId = "d01", Phase = ExpeditionState.Extracting };
        Assert.That(E.FeedBlocked(s, f), Is.EqualTo("正在撤出，无法给同行者吃喝。"));
        s.Expedition = null;

        s.Battle = new BattleState { BattleId = 9, LocationId = "camp", Allies = new List<string> { Me, Lin } };
        Assert.That(E.FeedBlocked(s, f), Is.EqualTo("战斗中无法给同行者吃喝。"));
        Assert.That(E.Feed(s, f).Ok, Is.False);
        s.Battle = null;

        var d = NewStory();
        Assert.That(E.DialogueLocked(d), Is.True);
        Assert.That(E.FeedBlocked(d, new FeedRequest()), Is.EqualTo(GameEngine.DialogueLockedReason));
    }

    [Test]
    public void Timing_OneSentenceOnly_GlobalBeforeTargetBeforeItem()
    {
        var s = Party();
        s.Encounter = new PendingEncounter { LocationId = "camp", EnemyIds = new List<int>(), Source = "explore", Minute = s.GameMinute };
        Assert.That(E.FeedBlocked(s, new FeedRequest()), Is.EqualTo("发现敌踪：请先选择接敌或绕行"), "敌踪专属句不被未选目标盖住");
        s.Encounter = null;
        var spoiled = Give(s, "berry", ItemInstance.Carried);
        spoiled.ExpiresAt = s.GameMinute;
        spoiled.ReservedBy = 1;
        Assert.That(E.FeedBlocked(s, Req("", Me, spoiled)), Is.EqualTo(FeedCopy.NoTarget));
        Assert.That(E.FeedBlocked(s, Req(Lin, Me, spoiled)), Is.EqualTo("该物品已被预留"), "沿原判定顺序，预留先于腐败");
    }

    [Test]
    public void Timing_MainRunning_Rejected_PausedMain_SubAction()
    {
        var s = Party();
        var food = Give(s, "canned_food", ItemInstance.Carried, 2);
        Start(s, ActionRequest.Wait(60));
        Assert.That(E.FeedBlocked(s, Req(Lin, Me, food)), Is.EqualTo("已有进行中的行动"));
        s.Main!.Status = ActionStatus.Paused;
        Fed(s, Req(Lin, Me, food));
        Assert.That(s.Sub, Is.Not.Null, "走主角原合法补给子行动");
        Assert.That(s.Sub!.Type, Is.EqualTo(ActionType.Use));
        Assert.That(E.FeedBlocked(s, Req(Lin, Me, food)), Is.EqualTo("已有进行中的行动"), "不新增并行零耗时喂人");
    }

    [Test]
    public void Timing_TravelPaused_SubAction_CarriedOnly()
    {
        var s = Party();
        var food = Give(s, "canned_food", ItemInstance.Carried);
        Start(s, ActionRequest.Travel("field"));
        E.Advance(s);
        Assert.That(E.FeedBlocked(s, Req(Lin, Me, food)), Is.EqualTo("已有进行中的行动"), "移动中不可喂人");
        s.Main!.Status = ActionStatus.Paused;
        var sat = s.Actors[Lin].Stats.Satiety;
        Fed(s, Req(Lin, Me, food));
        Assert.That((s.Sub!.IsSub, s.Sub.ForceOutdoor), Is.EqualTo((true, true)), "安全暂停段按原补给子行动，途中在室外");
        Assert.That(s.Actors[Lin].Stats.Satiety, Is.EqualTo(sat + 35).Within(1e-9));
    }

    [Test]
    public void Timing_NotCancellable_ExitDeferred()
    {
        var s = Party();
        Fed(s, Req(Lin, Me, Give(s, "canned_food", ItemInstance.Carried)));
        Assert.That(E.Cancel(s).Reason, Is.EqualTo("该行动不可中途取消"));
        var exit = E.RequestExit(s);
        Assert.That((exit.Ok, exit.Deferred), Is.EqualTo((true, true)), "已提交的消耗与效果不撤销");
        Assert.That(s.Main, Is.Not.Null);
    }

    // ───────────── 幂等（PLAN-Q232-03、Q252-257-A08） ─────────────

    [Test]
    public void Idempotent_SameRequestId_DoubleClickAndAfterFinish()
    {
        var s = Party();
        var food = Give(s, "canned_food", ItemInstance.Carried, 3);
        var f = Req(Lin, Me, food, "feed-1");
        Fed(s, f);
        var again = E.Feed(s, f);
        Assert.That((again.Ok, again.Duplicate), Is.EqualTo((true, true)));
        Assert.That((food.Count, s.Actors[Lin].Stats.Satiety), Is.EqualTo((2, 85.0)), "连点不双扣不双给");
        Run(s);
        var sat = s.Actors[Lin].Stats.Satiety;
        var minute = s.GameMinute;
        Assert.That(E.Feed(s, f).Duplicate, Is.True);
        Assert.That((food.Count, s.Actors[Lin].Stats.Satiety, s.GameMinute, s.Main), Is.EqualTo((2, sat, minute, (ActionState?)null)));
    }

    [Test]
    public void Idempotent_AfterSaveLoad_NoReplay_NoNewFields()
    {
        var s = Party();
        var (pot, water) = PotWater(s, ItemInstance.Carried, "clean_water", 2);
        var f = Req(Lin, Me, water, "feed-2");
        Fed(s, f);
        var json = SaveSerializer.Serialize(s);
        Assert.That(json, Does.Not.Contain("Feeding"), "事务不写新存档字段");
        var back = SaveSerializer.Deserialize(json, Cfg);
        Assert.That(E.Feed(back, f).Duplicate, Is.True);
        Assert.That((E.PotUnits(back, pot.InstanceId), back.Actors[Lin].Stats.Hydration), Is.EqualTo((1, 80.0)));
        Assert.That(Run(back), Is.EqualTo(SegmentStatus.Completed), "读档后继续走完这 5 分钟");
    }

    [Test]
    public void FailedRequest_DoesNotBurnRequestId()
    {
        var s = Party();
        var food = Give(s, "canned_food", ItemInstance.Carried);
        food.ReservedBy = 3;
        var f = Req(Lin, Me, food, "feed-3");
        Rejected(s, f, "该物品已被预留");
        food.ReservedBy = null;
        Fed(s, f);
        Assert.That(s.Actors[Lin].Stats.Satiety, Is.EqualTo(85));
    }

    [Test]
    public void A08_FeedingDoesNotCompletePrologueEatTask_NoQuestOrXp()
    {
        var s = NewStory();
        ReadDialogues(s);
        s.Progress.Flags.Add("hub_registered");
        E.DebugRecruit(s, Lin);
        foreach (var a in s.Actors.Values) a.Stats.Satiety = 50;
        var food = Give(s, "canned_food", ItemInstance.Carried);
        var self = s.Clone();
        Start(self, ActionRequest.UseInstance(food.InstanceId));
        Assert.That(self.Prologue.Tasks.ContainsKey("PRO02"), Is.True, "对照：主角自己吃才算");
        var xp = s.Skills.Select(kv => (kv.Key, kv.Value.Xp)).ToList();
        Fed(s, Req(Lin, Me, food));
        Assert.That(s.Prologue.Tasks.ContainsKey("PRO02"), Is.False, "给同行吃不完成开局任务");
        Run(s);
        Assert.That(s.Skills.Select(kv => (kv.Key, kv.Value.Xp)), Is.EquivalentTo(xp), "不加经验");
    }

    // ───────────── 预览与确认重验（合同 feeding.effect） ─────────────

    [Test]
    public void Preview_ReadOnly_FieldsMatchRealResult()
    {
        var s = Party();
        Wear(s, Lin);
        var food = Give(s, "cooked_meat", ItemInstance.Carried, 2);
        var f = Req(Lin, Me, food);
        var before = SaveSerializer.Serialize(s);
        var p = E.FeedPreview(s, f);
        Assert.That(SaveSerializer.Serialize(s), Is.EqualTo(before), "预览不改状态");
        Assert.That((p.Ok, p.ConfirmText, p.BaseMinutes, p.EstimatedMinutes), Is.EqualTo((true, "给林昭食用：烤肉×1 · 5分钟", 5, 5)));
        var d = p.Feed!;
        Assert.That((d.TargetName, d.Source.SourceName, d.Source.InstanceId, d.Source.Portions, d.Source.ExpiresAt),
            Is.EqualTo(("林昭", "你", food.InstanceId, 2, food.ExpiresAt)));
        Assert.That((d.TargetBefore.Satiety, d.TargetBefore.Hydration), Is.EqualTo((50.0, 50.0)));
        Assert.That(d.TargetAfterImmediate.Satiety, Is.EqualTo(50 + 35 * 1.05).Within(1e-9));
        Assert.That(d.TargetAfterImmediate.Hydration, Is.EqualTo(47));
        Assert.That(d.TargetAfter.Hydration, Is.LessThan(47), "走完 5 分钟含自然消耗");
        Assert.That(d.ChangeText, Is.EqualTo("饱食+36.75，水分−3，精神+2"));
        Assert.That(p.Overflow, Is.Empty);
        Fed(s, f);
        Assert.That(s.Log.Last().Text, Is.EqualTo(d.ResultText));
        Run(s);
        Assert.That(s.Actors[Lin].Stats.Hydration, Is.EqualTo(d.TargetAfter.Hydration).Within(1e-9));
    }

    [Test]
    public void Preview_ConfirmTexts_WaterAndCanteen_RemainingMl()
    {
        var s = Party();
        var (pot, water) = PotWater(s, Bag(Lin), "clean_water", 3);
        var p = E.FeedPreview(s, Req(Lin, Lin, water));
        Assert.That(p.ConfirmText, Is.EqualTo("给林昭饮用：净水×1 · 5分钟"));
        Assert.That((p.Feed!.Source.SourceName, p.Feed.Source.PotInstanceId, p.Feed.Source.ItemName, p.Feed.Source.LiquidMl, p.Feed.LiquidMlAfter),
            Is.EqualTo(("林昭", pot.InstanceId, "净水", 1500, 1000)));

        var c = E.FeedPreview(s, Req(Lin, Me, Canteen(s, ItemInstance.Carried, "dirty_water", 500)));
        Assert.That(c.ConfirmText, Is.EqualTo("给林昭饮用：分量水壶内脏水250毫升 · 5分钟"));
        Assert.That((c.Feed!.Source.Kind, c.Feed.Source.ItemName, c.Feed.LiquidMlAfter), Is.EqualTo((FeedSourceKind.Canteen, "脏水", 250)));
        Assert.That(c.Feed.ChangeText, Is.EqualTo("水分+15，出现腹泻（12小时）"));
        var card = E.FeedPreview(s, Req(Lin, Me, Give(s, "clean_water", ItemInstance.Carried, 2)));
        Assert.That((card.Feed!.Source.LiquidMl, card.Feed.LiquidMlAfter), Is.EqualTo((1000, 500)));
    }

    [Test]
    public void Preview_Blocked_ReasonOnly()
    {
        var s = Party();
        var p = E.FeedPreview(s, Req(Lin, Me, Give(s, "raw_root", ItemInstance.Carried)));
        Assert.That((p.Ok, p.Reason, p.Feed), Is.EqualTo((false, "生块根需先烹饪，不可直接食用", (FeedPreviewInfo?)null)));
    }

    [Test]
    public void Confirm_Stamp_CountOwnerGone_Changed_ExactMatchOk()
    {
        var s = Party();
        var food = Give(s, "canned_food", ItemInstance.Carried, 2);
        var f = Req(Lin, Me, food);
        f.ConfirmStamp = E.FeedPreview(s, f).Feed!.Source.Stamp;
        Assert.That(E.FeedBlocked(s, f), Is.Null);

        food.Count = 1;
        Rejected(s, f, "当前情况已变化，请重新选择后再操作。");
        food.Count = 2;

        food.Owner = s.Ground;
        Rejected(s, f, FeedCopy.Changed);
        food.Owner = ItemInstance.Carried;

        s.Items.Remove(food);
        Rejected(s, f, FeedCopy.Changed);
        s.Items.Add(food);

        Fed(s, f);
        Assert.That(food.Count, Is.EqualTo(1));
    }

    [Test]
    public void Confirm_Stamp_PotWaterAndCanteenChanged()
    {
        var s = Party();
        var (pot, water) = PotWater(s, ItemInstance.Carried, "clean_water", 2);
        var f = Req(Lin, Me, water);
        f.ConfirmStamp = E.FeedPreview(s, f).Feed!.Source.Stamp;
        water.Count = 1;
        Rejected(s, f, FeedCopy.Changed);
        water.Count = 2;
        pot.Owner = s.Ground;
        Rejected(s, f, FeedCopy.Changed);
        pot.Owner = ItemInstance.Carried;

        var c = Canteen(s, Bag(Lin), "clean_water", 750);
        var g = Req(Lin, Lin, c);
        g.ConfirmStamp = E.FeedPreview(s, g).Feed!.Source.Stamp;
        c.LiquidMl = 500;
        Rejected(s, g, FeedCopy.Changed);
    }

    [Test]
    public void Confirm_Stamp_SpecificReasonsNotOverridden()
    {
        var s = Party();
        var food = Give(s, "berry", ItemInstance.Carried);
        var f = Req(Lin, Me, food);
        f.ConfirmStamp = E.FeedPreview(s, f).Feed!.Source.Stamp;
        food.ReservedBy = 4;
        Rejected(s, f, "该物品已被预留");
        food.ReservedBy = null;
        s.GameMinute = food.ExpiresAt!.Value;
        Rejected(s, f, "已腐败，不能食用");
        s.GameMinute = At(1, 10, 0);
        s.Party.Remove(Lin);
        Rejected(s, f, "当前无法给林昭吃喝：目标不在同地点、同区域的同行队伍中。");
    }

    [Test]
    public void StaleSelection_InstanceGone_NoStamp_Changed()
    {
        var s = Party();
        Rejected(s, new FeedRequest { TargetActorId = Lin, SourceActorId = Me, InstanceId = 987654 }, FeedCopy.Changed);
        Rejected(s, new FeedRequest { TargetActorId = Lin, SourceActorId = Me }, FeedCopy.Changed);
        var (pot, water) = PotWater(s, ItemInstance.Carried, "clean_water", 1);
        var other = Give(s, "pot", ItemInstance.Carried);
        Rejected(s, new FeedRequest { TargetActorId = Lin, SourceActorId = Me, InstanceId = water.InstanceId, PotInstanceId = other.InstanceId }, FeedCopy.Changed);
    }

    // ───────────── 主角自用不变（R12） ─────────────

    [Test]
    public void SelfUse_TextAndTarget_Unchanged()
    {
        var s = Party();
        Wear(s, Lin);
        Start(s, ActionRequest.UseInstance(Give(s, "canned_food", ItemInstance.Carried).InstanceId));
        Assert.That(s.Log.Last().Text, Is.EqualTo("食用1份罐头：饱食+35。"), "同行穿配给袋不影响主角");
        Assert.That((s.Actors[Me].Stats.Satiety, s.Actors[Lin].Stats.Satiety), Is.EqualTo((85.0, 50.0)));
        Run(s);
        s.Actors[Me].Stats.Hydration = 90;
        var c = Canteen(s, ItemInstance.Carried, "clean_water", 1000);
        Start(s, ActionRequest.UseInstance(c.InstanceId));
        Assert.That(s.Log.Last().Text, Is.EqualTo("从分量水壶喝250毫升净水：水分+15，壶里剩净水 750/2000毫升。"), "自用仍写配置量，不改裁上限口径");
        Assert.That(s.Actors[Me].Stats.Hydration, Is.EqualTo(100));
    }
}
