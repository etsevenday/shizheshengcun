using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>拖拽与容器案第 4 节白名单：每行一例合法、一例非法；另含 D01、D05、D13、D14 的内核部分与忙碌表。</summary>
public class DropResolverTests : TestBase
{
    private static DropSource Card(string key) => DropSource.Parse("card:" + key)!;
    private static DropTarget T(string key, int index = 0) => DropTarget.Parse(key, index)!;
    private ItemInstance Pot(GameState s) => s.Items.First(i => i.ItemId == "pot");
    private string PotCard(GameState s) => $"{Pot(s).Owner}|pot|{Pot(s).InstanceId}";

    private DropIntent R(GameState s, DropSource src, string target, int index = 0) => E.ResolveDrop(s, src, T(target, index));

    [Test]
    public void Keys_Parse()
    {
        var src = DropSource.Parse("card:carried|hatchet|7")!;
        Assert.That((src.Kind, src.Owner, src.ItemId, src.InstanceId), Is.EqualTo((DropSourceKind.Card, "carried", "hatchet", (int?)7)));
        Assert.That(DropSource.Parse("potchild:12")!.InstanceId, Is.EqualTo(12));
        var slot = DropTarget.Parse("slot:knife:stone")!;
        Assert.That((slot.Kind, slot.Id, slot.ItemId), Is.EqualTo((DropTargetKind.RecipeSlot, "knife", "stone")));
        Assert.That(DropTarget.Parse("self"), Is.Null, "v0.7 第 6.2 节取消左栏自身投放：self 不再是落点");
        Assert.That(DropTarget.Parse("fireslot:camp")!.Kind, Is.EqualTo(DropTargetKind.FireSlot));
        Assert.That(DropTarget.Parse("potfood:7")!.Kind, Is.EqualTo(DropTargetKind.PotFood));
        Assert.That(DropTarget.Parse("facility:camp")!.Kind, Is.EqualTo(DropTargetKind.FireCard));
        var inFire = DropSource.Parse("card:facility:camp:fire|pot|7")!;
        Assert.That((inFire.Owner, inFire.ItemId, inFire.InstanceId), Is.EqualTo(("facility:camp:fire", "pot", (int?)7)));
        Assert.That(DropTarget.Parse("bogus:1"), Is.Null);
    }

    [Test]
    public void Row1_ReorderInSameZone_D01()
    {
        var s = New();
        E.DebugGive(s, "wood", 6, "camp");
        var cards = E.OrderedCards(s, "camp");
        Assert.That(cards.First(), Is.EqualTo("camp|clean_water"), "默认按配置顺序");
        var i = R(s, Card("camp|wood"), "zone:camp", 0);
        Assert.That(i.Ok, Is.True, i.Reason);
        Assert.That(i.Verb, Is.EqualTo(DropVerb.Reorder));
        var minute = s.GameMinute;
        var grams = E.CarriedGrams(s);
        Assert.That(E.DoInstant(s, i.Instant!).Ok, Is.True);
        Assert.That(E.OrderedCards(s, "camp").First(), Is.EqualTo("camp|wood"));
        Assert.That(Count(s, "wood", "camp"), Is.EqualTo(8), "整堆移动，数量不变");
        Assert.That(s.GameMinute, Is.EqualTo(minute));
        Assert.That(E.CarriedGrams(s), Is.EqualTo(grams));
        var t = SaveSerializer.Deserialize(SaveSerializer.Serialize(s));
        Assert.That(E.OrderedCards(t, "camp"), Is.EqualTo(E.OrderedCards(s, "camp")), "读档保留顺序");

        // 新产物追加在尾部，不打乱手工顺序
        E.DebugGive(s, "berry", 1, "camp");
        E.UpdateFlags(s);
        Assert.That(E.OrderedCards(s, "camp").Last(), Is.EqualTo("camp|berry"));
        Assert.That(E.OrderedCards(s, "camp").First(), Is.EqualTo("camp|wood"));

        // 非法：其他地点的区域
        Assert.That(R(s, Card("camp|wood"), "zone:field").Ok, Is.False);
        // 排序在执行中也允许
        Start(s, ActionRequest.Wait(30));
        Assert.That(R(s, Card("camp|wood"), "zone:camp", 2).Ok, Is.True);
        Assert.That(R(s, Card("camp|wood"), "zone:carried").Reason, Is.EqualTo("行动进行中，只能整理卡片顺序"));
    }

    [Test]
    public void Row2_TransferGroundToCarried_AndBack()
    {
        var s = New();
        var i = R(s, Card("camp|wood"), "zone:carried");
        Assert.That(i.Ok && i.Verb == DropVerb.Transfer, Is.True, i.Reason);
        Assert.That(i.Quantifiable, Is.True);
        Assert.That(i.MaxQuantity, Is.EqualTo(2));
        Assert.That(E.DoInstant(s, i.Instant!).Ok, Is.True);
        Assert.That(Count(s, "wood", "carried"), Is.EqualTo(1), "默认1份");
        var back = R(s, Card("carried|wood"), "zone:camp");
        Assert.That(back.Ok, Is.True);
        Assert.That(back.Label, Does.StartWith("放下1木材"));
        E.DoInstant(s, back.Instant!);

        // 非法：29.8公斤时拾取木材
        E.DebugGive(s, "stone", 57);
        E.DebugGive(s, "fiber", 2);
        Assert.That(E.CarriedGrams(s), Is.EqualTo(29800));
        var no = R(s, Card("camp|wood"), "zone:carried");
        Assert.That(no.Ok, Is.False);
        Assert.That(no.Reason, Does.Contain("30公斤"));
    }

    [Test]
    public void Row3_SameItemCardAcrossZones_Merges()
    {
        var s = New();
        E.DebugGive(s, "wood", 1);
        var i = R(s, Card("camp|wood"), "card:carried|wood");
        Assert.That(i.Ok && i.Verb == DropVerb.Transfer, Is.True, i.Reason);
        Assert.That(i.Label, Does.Contain("合堆"));
        E.DoInstant(s, i.Instant!);
        Assert.That(s.Items.Count(x => x.ItemId == "wood" && x.IsCarried), Is.EqualTo(1));
        // 非法：不同物品的普通卡面没有组合，界面改落到插入线
        var no = R(s, Card("camp|canned_food"), "card:camp|clean_water");
        Assert.That(no.Ok, Is.False);
        Assert.That(no.Reason, Is.EqualTo(GameEngine.Unsupported));
        Assert.That(no.FallThrough, Is.True);
    }

    [Test]
    public void Row4_WoodToFuel_AndRejects()
    {
        var s = New();
        var i = R(s, Card("camp|wood"), "fuel:camp");
        Assert.That(i.Ok && i.Verb == DropVerb.AddFuel, Is.True, i.Reason);
        Assert.That(i.Label, Is.EqualTo("投放1木材，燃料+60分钟"));
        Assert.That(i.MaxQuantity, Is.EqualTo(2));
        E.DoInstant(s, i.Instant!);
        Assert.That(s.Locations["camp"].Facility.FuelMinutes, Is.EqualTo(60));
        Assert.That(s.Locations["camp"].Facility.FireLit, Is.False, "不自动点火");
        Assert.That(R(s, Card("camp|clean_water"), "fuel:camp").Reason, Does.Contain("请拖进锅里"), "水放火堆不灭火，提示拖入锅");
        s.Locations["camp"].Facility.FuelMinutes = 190;
        Assert.That(R(s, Card("camp|wood"), "fuel:camp").Reason, Does.Contain("至少需要60分钟空余容量"));
    }

    [Test]
    public void D05_SameRequestIdTwice_OneWoodOnly()
    {
        var s = New();
        var i = R(s, Card("camp|wood"), "fuel:camp");
        var req = i.Instant!.WithRequestId("drop-1");
        Assert.That(E.DoInstant(s, req).Ok, Is.True);
        Assert.That(E.DoInstant(s, req).Ok, Is.True, "重发返回成功");
        Assert.That(s.Locations["camp"].Facility.FuelMinutes, Is.EqualTo(60));
        Assert.That(Count(s, "wood"), Is.EqualTo(1), "只扣1木材");
        var t = SaveSerializer.Deserialize(SaveSerializer.Serialize(s));
        Assert.That(E.DoInstant(t, req).Ok, Is.True);
        Assert.That(t.Locations["camp"].Facility.FuelMinutes, Is.EqualTo(60), "读档后重发仍不生效");
    }

    [Test]
    public void Row5_LighterToFire_IgniteStartsOnDrop()
    {
        var s = New();
        var lighter = First(s, "lighter");
        var i = R(s, Card($"carried|lighter|{lighter.InstanceId}"), "fire:camp");
        Assert.That(i.Ok && i.Verb == DropVerb.Ignite, Is.True, i.Reason);
        Assert.That(i.IsPreview, Is.False, "v0.7 第 4.1 节：不再打开需要再按开始的预览");
        Assert.That(i.StartsAction, Is.True);
        Assert.That(i.Action!.InstanceId, Is.EqualTo(lighter.InstanceId));
        Assert.That(i.Label, Is.EqualTo("松开后开始点火·5分钟"));
        Assert.That(E.Preview(s, i.Action).Notes, Does.Contain($"使用打火器#{lighter.InstanceId}（随身），成功点火磨损1（耐久{GameEngine.DurabilityText(lighter)}）"), "v0.6：打火器开始磨损");
        Assert.That(s.Sub ?? s.Main, Is.Null, "解析本身不开始，松手才提交");
        Assert.That(R(s, Card($"carried|lighter|{lighter.InstanceId}"), "fuel:camp").Verb, Is.EqualTo(DropVerb.Ignite), "燃料区也属于火堆");
        Assert.That(R(s, Card($"carried|lighter|{lighter.InstanceId}"), "potslot:camp").Verb, Is.EqualTo(DropVerb.Ignite), "锅位也属于火堆");
        // 非法：没有燃料也没有木材
        foreach (var w in s.Items.Where(x => x.ItemId == "wood").ToList()) s.Items.Remove(w);
        Assert.That(R(s, Card($"carried|lighter|{lighter.InstanceId}"), "fire:camp").Reason, Is.EqualTo("请先投入木材"));
    }

    [Test]
    public void Row6_WaterToPotFill()
    {
        var s = New();
        E.DebugGive(s, "dirty_water", 2, "camp");
        var i = R(s, Card("camp|dirty_water"), $"potfill:{Pot(s).InstanceId}");
        Assert.That(i.Ok && i.Verb == DropVerb.FillPot, Is.True, i.Reason);
        Assert.That(i.Label, Is.EqualTo("装入1份脏水，水量0%→50%"));
        Assert.That(i.MaxQuantity, Is.EqualTo(2));
        E.DoInstant(s, i.Instant!);
        Assert.That(R(s, Card("camp|clean_water"), $"potfill:{Pot(s).InstanceId}").Reason, Does.Contain("混装禁止"));
        Assert.That(R(s, Card("camp|dirty_water"), "card:" + PotCard(s)).Verb, Is.EqualTo(DropVerb.FillPot), "落到锅卡面等同装水区");
    }

    [Test]
    public void Row7_PotChildDecant()
    {
        var s = New();
        var pot = Pot(s);
        E.DoInstant(s, InstantRequest.FillPot(First(s, "clean_water", "camp").InstanceId, pot.InstanceId, 2));
        var child = E.PotContents(s, pot.InstanceId).Single();
        var i = R(s, DropSource.Parse($"potchild:{child.InstanceId}")!, "zone:camp");
        Assert.That(i.Ok && i.Verb == DropVerb.Decant, Is.True, i.Reason);
        Assert.That(i.MaxQuantity, Is.EqualTo(2));
        E.DoInstant(s, i.Instant!);
        Assert.That(E.PotUnits(s, pot.InstanceId), Is.EqualTo(1));
        Assert.That(R(s, DropSource.Parse($"potchild:{child.InstanceId}")!, "zone:field").Ok, Is.False, "不能倒到其他地点");
        Assert.That(E.CanStart(s, ActionRequest.UseInstance(child.InstanceId)), Is.Null, "锅内水改由行下“饮用1份”按钮使用");
    }

    [Test]
    public void Row8_PotToSlot_PutInFire()
    {
        var s = New();
        var i = R(s, Card(PotCard(s)), "potslot:camp");
        Assert.That(i.Ok && i.Verb == DropVerb.PutInFire, Is.True, i.Reason);
        E.DoInstant(s, i.Instant!);
        Assert.That(Pot(s).Owner, Is.EqualTo("facility:camp:fire"), "锅的真实归属变为火堆");
        Assert.That(R(s, Card(PotCard(s)), "potslot:camp").Reason, Does.Contain("已经在火堆里"));
        Assert.That(R(s, Card("camp|stone"), "potslot:camp").Reason, Is.EqualTo("石块不能放进火堆加工位"));
    }

    [Test]
    public void Row9_10_ToolOnResource_D13ExplicitTool()
    {
        var s = New();
        E.DebugTeleport(s, "woods");
        E.DebugSetProgress(s, "woods", 75);
        E.DebugGive(s, "hatchet", 2);
        var axes = s.Items.Where(x => x.ItemId == "hatchet").ToList();
        Assert.That(axes, Has.Count.EqualTo(2), "两把斧不合并");
        axes[1].Durability = 12;
        var worn = R(s, Card($"carried|hatchet|{axes[1].InstanceId}"), "res:wood");
        Assert.That(worn.Ok && worn.Verb == DropVerb.PreviewGatherTool, Is.True, worn.Reason);
        var p = E.Preview(s, worn.Action!);
        Assert.That(p.ToolDurabilityBefore, Is.EqualTo(12), "拖入哪把就预览哪把");
        Assert.That(p.ToolExplicit, Is.True);
        Assert.That(E.Preview(s, ActionRequest.Gather("wood")).ToolDurabilityBefore, Is.EqualTo(axes[0].Durability), "按钮仍自动选最高耐久");
        Start(s, worn.Action!);
        Assert.That(s.Main!.ToolInstanceId, Is.EqualTo(axes[1].InstanceId));
        E.Cancel(s);

        // 石刀 → 纤维资源
        E.DebugGive(s, "stone_knife", 1);
        var knife = First(s, "stone_knife");
        Assert.That(R(s, Card($"carried|stone_knife|{knife.InstanceId}"), "res:woods_fiber").Verb, Is.EqualTo(DropVerb.PreviewGatherTool));
        // 非法：斧头拖到纤维；地面的斧需先拾取；损坏的斧不暗中换
        Assert.That(R(s, Card($"carried|hatchet|{axes[0].InstanceId}"), "res:woods_fiber").Reason, Is.EqualTo(GameEngine.Unsupported));
        E.DoInstant(s, InstantRequest.Drop(axes[0].InstanceId, 1));
        Assert.That(R(s, Card($"woods|hatchet|{axes[0].InstanceId}"), "res:wood").Reason, Is.EqualTo("先拾取工具"));
        axes[1].Durability = 0;
        Assert.That(E.CanStart(s, ActionRequest.Gather("wood", axes[1].InstanceId)), Is.EqualTo("这件工具已损坏，请重新选择"));
    }

    [Test]
    public void Row11_MaterialsOpenRecipeCandidates_D12()
    {
        var s = New();
        var i = R(s, Card("camp|stone"), "card:camp|fiber");
        Assert.That(i.Ok && i.Verb == DropVerb.PreviewRecipe, Is.True, i.Reason);
        Assert.That(i.Recipes, Is.EqualTo(new[] { "knife", "axe", "make_wooden_spear", "make_arrows" }), "多候选按配置顺序列出（含 v0.5 木矛、箭矢）");
        var before = SaveSerializer.Serialize(s);
        E.ResolveDrop(s, Card("camp|stone"), T("card:camp|fiber"));
        Assert.That(SaveSerializer.Serialize(s), Is.EqualTo(before), "未确认开始不扣物、不耗时、不预留");

        var slot = R(s, Card("camp|stone"), "slot:knife:stone");
        Assert.That(slot.Ok, Is.True, slot.Reason);
        Assert.That(slot.Action!.PreferredInstanceIds, Has.Count.EqualTo(1));
        Assert.That(R(s, Card("camp|wood"), "slot:knife:stone").Reason, Is.EqualTo("这个材料槽需要石块"));
        var camp = R(s, Card("camp|wood"), "loc:camp");
        Assert.That(camp.Recipes, Is.EqualTo(new[] { "shelter" }), "原料拖至营地地点卡");
        Assert.That(R(s, Card("camp|canned_food"), "card:camp|cloth").Ok, Is.False);
    }

    [Test]
    public void D14_ButtonAndDragStartSameRecipe_Identical()
    {
        var a = New(77);
        E.DebugGive(a, "stone", 1, "carried");
        var b = a.Clone();
        Start(a, ActionRequest.Craft("knife"));
        var drag = E.ResolveDrop(b, Card("camp|stone"), T("slot:knife:stone"));
        // 拖拽指定的是地面那批石块；按钮自动补齐时同到期先用随身，故此处对比自动补齐的拖拽入口
        var viaCard = E.ResolveDrop(b, Card("camp|stone"), T("card:camp|fiber"));
        Assert.That(viaCard.Recipes, Does.Contain("knife"));
        Start(b, ActionRequest.Craft(viaCard.Recipes[0]));
        Assert.That(SaveSerializer.Serialize(b), Is.EqualTo(SaveSerializer.Serialize(a)), "成本、来源、锁、时长一致");
        Run(a);
        Run(b);
        Assert.That(SaveSerializer.Serialize(b), Is.EqualTo(SaveSerializer.Serialize(a)), "产物位置一致");

        // 指定批次：先用拖入的地面石块
        var c = New(77);
        E.DebugGive(c, "stone", 1, "carried");
        var ground = First(c, "stone", "camp");
        Start(c, drag.Action!.WithRequestId(null));
        var stones = c.Main!.ReservedItemIds.Select(id => c.Items.First(x => x.InstanceId == id)).Where(x => x.ItemId == "stone").ToList();
        Assert.That(stones.All(x => x.Owner == "camp"), Is.True, "预留的是拖入的地面批次");
        Assert.That(Count(c, "stone", "carried"), Is.EqualTo(1), "随身石块未被用掉");
    }

    [Test]
    public void Row12_SelfTargetRemoved_UseVerbsFromKernel()
    {
        // v0.7 第 6.2 节：原“拖到自身卡使用”取消，对自身使用统一走卡下按钮；按钮文案与风险来自内核
        Assert.That(DropTarget.Parse("self"), Is.Null);
        var s = New();
        Assert.That(GameEngine.UseButtonText(E.Item("canned_food")), Is.EqualTo("食用1份"));
        Assert.That(GameEngine.UseButtonText(E.Item("clean_water")), Is.EqualTo("饮用1份"));
        Assert.That(GameEngine.UseButtonText(E.Item("dirty_water")), Is.EqualTo("饮用1份（风险）"));
        Assert.That(E.UseRiskText(E.Item("dirty_water")), Is.EqualTo("必定腹泻12小时：期间每小时额外失水3"));
        Assert.That(GameEngine.UseButtonText(E.Item("bandage")), Is.EqualTo("使用1份"));
        E.DebugGive(s, "raw_root", 1, "camp");
        Assert.That(GameEngine.CanSelfUse(E.Item("raw_root")), Is.False, "生块根没有自用按钮");
        Assert.That(E.CanStart(s, ActionRequest.UseInstance(First(s, "raw_root", "camp").InstanceId)), Is.EqualTo("生块根需先烹饪，不可直接食用"));
        Assert.That(GameEngine.CanSelfUse(E.Item("herb")), Is.False);
        Assert.That(GameEngine.CanSelfUse(E.Item("pot")), Is.False, "空锅不出现使用按钮");
    }

    [Test]
    public void RawRootOnPotCard_PutsIntoPotFood_NotRecipe()
    {
        var s = New();
        E.DebugGive(s, "raw_root", 3, "camp");
        var i = R(s, Card("camp|raw_root"), "card:" + PotCard(s));
        Assert.That(i.Ok && i.Verb == DropVerb.PutFood, Is.True, i.Reason);
        Assert.That(i.Label, Does.Contain("缺水，只存放不加工"), "空锅只存放");
        Assert.That(i.MaxQuantity, Is.EqualTo(2), "食材位2");
        Assert.That(E.DoInstant(s, i.Instant!).Ok, Is.True);
        Assert.That(E.PotFood(s, Pot(s).InstanceId), Has.Count.EqualTo(1));
        E.DebugGive(s, "cooked_root", 1, "camp");
        Assert.That(R(s, Card("camp|cooked_root"), "card:" + PotCard(s)).Reason, Does.StartWith("烤熟块根没有可用的处理配方"));
    }

    [Test]
    public void BusyTable_PausedAllowsOnlyFuelIgniteAndSupply()
    {
        var s = New();
        SetStats(s, 100, 80, 20.2, 90, 90, 60);
        Start(s, ActionRequest.Clear());
        Assert.That(Run(s, autoResume: false), Is.EqualTo(SegmentStatus.Paused), "水分跌破提醒线暂停");
        Assert.That(R(s, Card("camp|wood"), "fuel:camp").Ok, Is.True, "暂停时可投料");
        Assert.That(E.CanStart(s, ActionRequest.UseItem("canned_food")), Is.Null, "暂停时可补给（卡下按钮）");
        Assert.That(R(s, Card($"carried|lighter|{First(s, "lighter").InstanceId}"), "fire:camp").Ok, Is.True, "暂停时可拖打火器点火维护");
        Assert.That(R(s, Card("camp|wood"), "zone:carried").Reason, Is.EqualTo("行动暂停中，只能补给、休息或维护火堆"));
        Assert.That(R(s, Card("camp|wood"), "zone:camp").Ok, Is.True, "排序始终允许");

        E.DebugTeleport(s, "camp");
        s.Progress.Dead = true;
        Assert.That(R(s, Card("camp|wood"), "zone:camp").Reason, Is.EqualTo("角色已死亡"));
    }

    private string FirePotKey(GameState s) => $"card:facility:camp:fire|pot|{Pot(s).InstanceId}";

    [Test]
    public void V04_FireSlot_RawRootRoasts_CookedRejected()
    {
        var s = New();
        E.DebugGive(s, "raw_root", 3, "camp");
        E.DebugGive(s, "cooked_root", 1, "camp");
        var i = R(s, Card("camp|raw_root"), "fireslot:camp");
        Assert.That(i.Ok && i.Verb == DropVerb.PutInFire, Is.True, i.Reason);
        Assert.That(i.Label, Is.EqualTo("放1份生块根进火堆加工位直接烤制（30分钟）"));
        Assert.That(i.MaxQuantity, Is.EqualTo(2), "两个空位");
        E.DoInstant(s, i.Instant!);
        Assert.That(s.Items.Count(x => x.Owner == "facility:camp:fire"), Is.EqualTo(1));
        Assert.That(R(s, Card("camp|cooked_root"), "fireslot:camp").Reason, Does.StartWith("烤熟块根没有可用的处理配方"));
        Assert.That(R(s, Card("camp|clean_water"), "fire:camp").Reason, Does.Contain("请拖进锅里"));
        Assert.That(R(s, Card("camp|wood"), "fireslot:camp").Verb, Is.EqualTo(DropVerb.AddFuel), "木材落在火堆任何位置都是投料");
        Assert.That(R(s, Card("camp|raw_root"), "fuel:camp").Reason, Is.EqualTo("生块根请拖到火堆的空加工位或锅的食材区；燃料区只收木材"), "试玩 B7：写明原因");
    }

    [Test]
    public void V04_FireCardFace_PotGoesIn_SecondPotRejectedWithCurrentPot()
    {
        var s = New();
        var i = R(s, Card(PotCard(s)), "facility:camp");
        Assert.That(i.Ok && i.Verb == DropVerb.PutInFire, Is.True, i.Reason);
        E.DoInstant(s, i.Instant!);
        E.DebugGive(s, "pot", 1, "camp");
        var other = s.Items.Last(x => x.ItemId == "pot");
        var no = R(s, Card($"camp|pot|{other.InstanceId}"), "fire:camp");
        Assert.That(no.Ok, Is.False);
        Assert.That(no.Reason, Does.Contain($"火堆已有金属锅#{Pot(s).InstanceId}"));
    }

    [Test]
    public void V04_RootOnPotFoodInFire_HitsPot_NotParentFire()
    {
        var s = New();
        var pot = Pot(s);
        E.DoInstant(s, InstantRequest.PutInFire(pot.InstanceId));
        E.DebugGive(s, "raw_root", 1, "camp");
        var i = R(s, Card("camp|raw_root"), $"potfood:{pot.InstanceId}");
        Assert.That(i.Ok && i.Verb == DropVerb.PutFood, Is.True, i.Reason);
        Assert.That(i.Instant!.TargetInstanceId, Is.EqualTo(pot.InstanceId), "落在锅上，不冒泡成父火堆烤制");
        E.DoInstant(s, i.Instant!);
        Assert.That(s.Items.Single(x => x.ItemId == "raw_root").Owner, Is.EqualTo("pot:" + pot.InstanceId));
        Assert.That(s.Items.Count(x => x.Owner == "facility:camp:fire"), Is.EqualTo(1), "火堆里只有锅");
        // 非法：水拖到食材区给出原因，不偷偷改投水量区
        Assert.That(R(s, Card("camp|clean_water"), $"potfood:{pot.InstanceId}").Reason, Does.Contain("水量条"));
        // 水拖到火堆里锅的水量区：直接装水
        Assert.That(R(s, Card("camp|clean_water"), $"potfill:{pot.InstanceId}").Verb, Is.EqualTo(DropVerb.FillPot));
        // 锅卡缩略图本身也是锅的落点
        Assert.That(R(s, Card("camp|clean_water"), "card:" + FirePotKey(s).Substring(5)).Verb, Is.EqualTo(DropVerb.FillPot));
    }

    [Test]
    public void V04_FacilityContentsDraggedOut_TakeOut_AndHeavyRejected()
    {
        var s = New();
        var pot = Pot(s);
        E.DoInstant(s, InstantRequest.FillPot(First(s, "clean_water", "camp").InstanceId, pot.InstanceId, 2));
        E.DoInstant(s, InstantRequest.PutInFire(pot.InstanceId));
        var down = R(s, DropSource.Parse(FirePotKey(s))!, "zone:camp");
        Assert.That(down.Ok && down.Verb == DropVerb.TakeOut, Is.True, down.Reason);
        Assert.That(down.Label, Does.StartWith("取下金属锅"));
        var up = R(s, DropSource.Parse(FirePotKey(s))!, "zone:carried");
        Assert.That(up.Ok, Is.True, up.Reason);
        Assert.That(up.Label, Does.Contain("含内容2.0公斤"));
        E.DebugGive(s, "stone", 57);
        Assert.That(R(s, DropSource.Parse(FirePotKey(s))!, "zone:carried").Reason, Does.Contain("整次拒绝"));
        E.DoInstant(s, down.Instant!);
        Assert.That(pot.Owner, Is.EqualTo("camp"));

        // 锅里的食材拖出 = 取出那一份；锅里的水拖出 = 倒出
        E.DebugGive(s, "raw_root", 1, "camp");
        E.DoInstant(s, InstantRequest.PutFoodInPot(First(s, "raw_root").InstanceId, pot.InstanceId));
        var root = E.PotFood(s, pot.InstanceId).Single();
        var outRoot = R(s, DropSource.Parse($"potchild:{root.InstanceId}")!, "zone:camp");
        Assert.That(outRoot.Verb, Is.EqualTo(DropVerb.TakeOut), outRoot.Reason);
        var water = E.PotLiquids(s, pot.InstanceId).Single();
        Assert.That(R(s, DropSource.Parse($"potchild:{water.InstanceId}")!, "zone:camp").Verb, Is.EqualTo(DropVerb.Decant));
        // 执行中不能取
        Start(s, ActionRequest.Wait(30));
        Assert.That(R(s, DropSource.Parse($"potchild:{root.InstanceId}")!, "zone:camp").Ok, Is.False);
    }

    [Test]
    public void V04_PreviewShowsFacilityChanges_WhileCharacterOnlyWaits()
    {
        var s = New();
        var f = s.Locations["camp"].Facility;
        f.FireLit = true;
        f.FuelMinutes = 55;
        E.DebugGive(s, "raw_root", 1, "camp");
        E.DoInstant(s, InstantRequest.PutInFire(First(s, "raw_root").InstanceId));
        var p = E.Preview(s, ActionRequest.Wait(30));
        Assert.That(p.FacilityChanges.Any(x => x.Contains("已烤成烤熟块根")), Is.True, string.Join("|", p.FacilityChanges));
        Assert.That(p.FacilityChanges, Does.Contain("临时营地火堆燃料55→25分钟"));
        var t = s.Clone();
        Start(t, ActionRequest.Wait(30));
        Run(t);
        Assert.That(p.StatsAfter.Hydration, Is.EqualTo(t.Stats.Hydration).Within(1e-9), "角色只按等待结算");
        Assert.That(s.Items.Single(x => x.Owner == "facility:camp:fire").ItemId, Is.EqualTo("raw_root"), "预览不改状态");
        Assert.That(f.FuelMinutes, Is.EqualTo(55));
    }

    [Test]
    public void OtherLocationNotDraggable()
    {
        var s = New();
        E.DebugGive(s, "wood", 1, "field");
        Assert.That(R(s, Card("field|wood"), "zone:carried").Reason, Is.EqualTo("其他地点的物品不能操作"));
    }
}
