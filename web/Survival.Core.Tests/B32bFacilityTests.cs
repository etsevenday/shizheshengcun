using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v1.0 B32b：火堆与工位右栏、点火盘、煅烧盘、晾制、安全屋与行动反馈（R62、R115、R133、R139）。
/// 规则出处：《重墨 V2 开工裁定与界面逐字稿》§1.2 Q339、Q341、Q343、Q344、Q346、Q347、Q350，§1.5 Q362—Q372 与验收，§2 P02a—P02f。
/// 判定在内核（CardStates、GameEngine.FireSlotHeating 等）的直接测；网页壳的接线与 CSS 以源码检查（同 B31b—B32a）。
/// </summary>
public class B32bFacilityTests : TestBase
{
    private static FacilityState Fac(GameState s) => s.Locations["camp"].Facility;

    private static void Ok(ActionResult r) => Assert.That(r.Ok, Is.True, r.Reason);

    private static ActionRequest Craft(string id) => ActionRequest.Craft(id, null, null, 1);

    // ───────────── Q362 加工中 ─────────────

    /// <summary>Q362-A01：实际受热时火堆与对应工位加工中；熄火、空位、已完成按实际状态不挂。</summary>
    [Test]
    public void Q362_A01_FireSlotHeating_OnlyWhenActuallyHeated()
    {
        var s = New();
        s.Items.RemoveAll(i => i.ItemId == "raw_root");
        var root = E.CreateItems(s, "raw_root", 1, ItemInstance.Carried).Single();
        Ok(E.DoInstant(s, InstantRequest.PutInFire(root.InstanceId)));
        var k = E.FireSlotOf(s, root);
        Assert.That(k, Is.GreaterThanOrEqualTo(0));
        Assert.That(E.FireSlotHeating(s, "camp", k), Is.False, "未点燃不算受热");
        Assert.That(E.FireHeating(s, "camp"), Is.False);

        Fac(s).FireLit = true;
        Fac(s).FuelMinutes = 60;
        Assert.That(E.FireSlotHeating(s, "camp", k), Is.True, "燃烧且燃料够一段：该位受热");
        Assert.That(E.FireHeating(s, "camp"), Is.True, "设施卡加工中");
        Assert.That(E.FireSlotHeating(s, "camp", 1 - k), Is.False, "空位不挂");

        Fac(s).FuelMinutes = 0;
        Assert.That(E.FireSlotHeating(s, "camp", k), Is.False, "燃料不足一段：受热暂停");
        Fac(s).FuelMinutes = 60;
        Fac(s).FireLit = false;
        Assert.That(E.FireHeating(s, "camp"), Is.False, "熄火不挂");
    }

    /// <summary>Q362：晾制中的位与晾制架卡加工中；已晾好不挂。</summary>
    [Test]
    public void Q362_A01_DryingBusy_OnlyWhileDrying()
    {
        var s = New();
        s.GameMinute = At(2, 8, 0);
        s.Skills["cooking"].Level = 5;
        s.Items.RemoveAll(i => i.Owner == s.Ground || i.Owner == ItemInstance.Carried);
        E.CreateItems(s, "wood", 4, s.Ground);
        E.CreateItems(s, "rope", 2, s.Ground);
        Start(s, Craft("build_drying_rack"));
        Run(s);
        Assert.That(E.DryingBusy(s), Is.False);
        E.CreateItems(s, "berry", 2, s.Ground);
        Ok(E.StartDrying(s, "dry_berries"));
        Assert.That(E.DryingSlotBusy(s, 0), Is.True);
        Assert.That(E.DryingSlotBusy(s, 1), Is.False);
        Assert.That(E.DryingBusy(s), Is.True);
        var views = E.DryingSlotViews(s);
        Assert.That(views[0].Title, Is.EqualTo("工位1"), "P02c 工位{序号}");
        Assert.That(views[0].RecipeName, Is.EqualTo(Cfg.Recipes.First(r => r.Id == "dry_berries").Name));
        Assert.That(CardStates.DryingStatusText(views[0].Status), Is.EqualTo("晾制中"));
        Assert.That(CardStates.DryingStatusText(views[1].Status), Is.EqualTo("空闲"));

        AdvanceTo(s, s.GameMinute + 120);
        Assert.That(E.DryingSlotBusy(s, 0), Is.False, "已晾好不挂加工中");
        Assert.That(CardStates.DryingStatusText(E.DryingSlotViews(s)[0].Status), Is.EqualTo("可收取"));
        Assert.That(CardStates.DryingStatusText(DryingSlot.Spoiled), Is.EqualTo("原料已腐败"));
    }

    // ───────────── Q368 高亮落点 ─────────────

    /// <summary>Q368：火堆成品高亮落到所在加工位，不再按成品物品名去亮随身/地面同名卡；晾好高亮该晾制位。</summary>
    [Test]
    public void Q368_A01_CompletionHighlightsSlot()
    {
        var s = New();
        s.Items.RemoveAll(i => i.ItemId == "raw_root");
        var root = E.CreateItems(s, "raw_root", 1, ItemInstance.Carried).Single();
        Ok(E.DoInstant(s, InstantRequest.PutInFire(root.InstanceId)));
        var k = E.FireSlotOf(s, root);
        Fac(s).FireLit = true;
        Fac(s).FuelMinutes = 240;
        E.DrainEvents(s);
        Start(s, ActionRequest.WaitCook());
        Run(s);
        var done = E.DrainEvents(s).Where(ev => ev.Kind == GameEventKind.FacilityDone).ToList();
        Assert.That(done, Is.Not.Empty);
        Assert.That(done.SelectMany(ev => ev.HighlightIds), Is.EquivalentTo(new[] { GameEngine.FireSlotHighlight("camp", k) }));
        Assert.That(GameEngine.DryingSlotHighlight("camp", 3), Is.EqualTo("slot:dry:camp:3"));
    }

    // ───────────── Q339、Q343、Q350 右栏 ─────────────

    /// <summary>Q339-A01、Q343-A01、Q350-A01：火堆／柴炉名、工位名、规则行标题与层级路径「火堆 › 加工位{n} › {锅名} › {内容}」。</summary>
    [Test]
    public void Q339_A01_FireCrumbsAndNames()
    {
        var s = New();
        var fac = Fac(s);
        Assert.That(CardStates.FireName(fac), Is.EqualTo("火堆"));
        fac.StoveInstanceId = 999;
        Assert.That(CardStates.FireName(fac), Is.EqualTo("柴炉"), "Q350 折叠柴炉卡名");
        fac.StoveInstanceId = null;
        Assert.That(CardStates.FireSlotName(0), Is.EqualTo("加工位1"));
        Assert.That(CardStates.CharcoalRuleTitle, Is.EqualTo("木炭工位"));

        var pot = E.CreateItems(s, "pot", 1, ItemInstance.Carried).Single();
        Ok(E.DoInstant(s, InstantRequest.PutInFire(pot.InstanceId)));
        var k = E.FireSlotOf(s, pot);
        Assert.That(E.FireCrumbs(s, null)!.Select(c => c.Label), Is.EqualTo(new[] { "火堆" }));
        Assert.That(E.FireCrumbs(s, pot)!.Select(c => c.Label), Is.EqualTo(new[] { "火堆", $"加工位{k + 1}", E.ItemName("pot") }));
        var root = E.CreateItems(s, "raw_root", 1, ItemInstance.Carried).Single();
        Ok(E.DoInstant(s, InstantRequest.PutFoodInPot(root.InstanceId, pot.InstanceId)));
        var inPot = s.Items.First(i => i.ItemId == "raw_root" && i.InContainer);
        var path = E.FireCrumbs(s, inPot)!;
        Assert.That(path.Select(c => c.Kind), Is.EqualTo(new[] { "fire", "slot", "pot", "content" }));
        Assert.That(path.Select(c => c.Label), Is.EqualTo(new[] { "火堆", $"加工位{k + 1}", E.ItemName("pot"), E.ItemName("raw_root") }));
        Assert.That(path[2].InstanceId, Is.EqualTo(pot.InstanceId), "锅层可点回锅");
        Assert.That(E.FireCrumbs(s, pot, "clean_water")!.Last().Label, Is.EqualTo(E.ItemName("clean_water")));
        var carried = E.CreateItems(s, "stone_knife", 1, ItemInstance.Carried).Single();
        Assert.That(E.FireCrumbs(s, carried), Is.Null, "不在火堆里的物品没有火堆路径");
    }

    // ───────────── Q372 三类卡摘要 ─────────────

    /// <summary>Q372-A01：火堆副标燃料、角标燃烧状态与「加工位满」。</summary>
    [Test]
    public void Q372_A01_FireCardSummary()
    {
        var s = New();
        var fac = Fac(s);
        fac.FireLit = false;
        fac.WentOut = false;
        fac.FuelMinutes = 18;
        Assert.That(CardStates.FireFuelSub(E, fac), Is.EqualTo($"燃料18/{E.FuelCapOf(fac)}分钟"));
        Assert.That(CardStates.FireStateText(fac), Is.EqualTo("未点燃"));
        fac.FireLit = true;
        Assert.That(CardStates.FireStateText(fac), Is.EqualTo("燃烧中"));
        fac.FireLit = false;
        fac.WentOut = true;
        Assert.That(CardStates.FireStateText(fac), Is.EqualTo("熄灭"));
        Assert.That(CardStates.FireSlotsFull, Is.EqualTo("加工位满"));
    }

    // ───────────── Q365、Q366 盘内字样 ─────────────

    /// <summary>Q365-A01：煅烧盘「耗时」「工位」取实际值；Q366-A01：点火盘「火堆」行与提交按钮。</summary>
    [Test]
    public void Q365_Q366_A01_DiskTexts()
    {
        var s = New();
        var info = E.CharcoalLoadPreview(s);
        Assert.That(CardStates.CharcoalTimeText(info), Is.EqualTo($"投料0分钟 · 需{info.HeatMinutes}有效受热分钟"));
        Assert.That(info.HeatMinutes, Is.EqualTo(60));
        Assert.That(CardStates.CharcoalSlotText(E, s), Is.EqualTo("加工位1"));
        var pot = E.CreateItems(s, "pot", 1, ItemInstance.Carried).Single();
        Ok(E.DoInstant(s, InstantRequest.PutInFire(pot.InstanceId)));
        Assert.That(CardStates.CharcoalSlotText(E, s), Is.EqualTo("加工位2"), "第一个空位");
        var root = E.CreateItems(s, "raw_root", 1, ItemInstance.Carried).Single();
        Ok(E.DoInstant(s, InstantRequest.PutInFire(root.InstanceId)));
        Assert.That(CardStates.CharcoalSlotText(E, s), Is.Null, "没有空位省略该行");

        var fac = Fac(s);
        fac.FuelMinutes = 25;
        Assert.That(CardStates.IgniteFireLine(fac), Is.EqualTo("火堆 · 剩余燃料25分钟"));
        Assert.That(CardStates.IgniteButton(5), Is.EqualTo("点火（5分钟）"));
        Assert.That(CardStates.IgnitePickHint, Is.EqualTo("请选择打火器。"));
    }

    // ───────────── Q369 安全屋休息条件 ─────────────

    /// <summary>Q369-A01：屋外先写当前区遮蔽或露天，再写屋内加成句；屋内沿现行屋内遮蔽与加成说明。</summary>
    [Test]
    public void Q369_A01_SafehouseRestLines()
    {
        var s = New();
        var bonus = Fmt.SignedT(Cfg.FacilityEffects.ShelteredCampRestMoralePerHour);
        Assert.That(bonus, Is.EqualTo("+2"));
        var lines = CardStates.SafehouseRestLines(E, s);
        Assert.That(lines, Has.Count.EqualTo(2));
        Assert.That(lines[0], Is.EqualTo(E.IsSheltered(s, s.Ground) ? "遮蔽" : "露天"));
        Assert.That(lines[1], Is.EqualTo("进入屋内休息精神每小时额外+2"));
        s.CurrentZone = GameState.SafehouseZone;
        Assert.That(CardStates.SafehouseRestLines(E, s), Is.EqualTo(new[] { "屋内遮蔽，休息时精神每小时额外+2" }));
    }

    // ───────────── Q371 食饮描边 ─────────────

    /// <summary>Q371-A01：只列实际变化的数值；封顶无变化的不列；只有食饮算。</summary>
    [Test]
    public void Q371_A01_ChangedStatsOnly()
    {
        var s = New();
        s.Stats.Set(StatId.Satiety, 60);
        var before = s.Stats.Clone();
        s.Stats.Set(StatId.Satiety, 65);
        var changed = CardStates.ChangedStats(before, s.Stats);
        Assert.That(changed, Is.EqualTo(new[] { StatId.Satiety }));

        var berry = E.CreateItems(s, "berry", 1, ItemInstance.Carried).Single();
        var knife = E.CreateItems(s, "stone_knife", 1, ItemInstance.Carried).Single();
        Assert.That(CardStates.IsEatOrDrink(E, s, ActionRequest.UseInstance(berry.InstanceId)), Is.True);
        Assert.That(CardStates.IsEatOrDrink(E, s, ActionRequest.UseInstance(knife.InstanceId)), Is.False);
        Assert.That(CardStates.IsEatOrDrink(E, s, ActionRequest.Explore()), Is.False);
    }
}
