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

    // ───────────── 网页壳接线（源码检查） ─────────────

    private static string Web(string relative)
    {
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "doc", "策划案"))) dir = dir.Parent;
        Assert.That(dir, Is.Not.Null, "找不到仓库根目录");
        return File.ReadAllText(Path.Combine(dir!.FullName, "web", "Survival.Web", relative));
    }

    /// <summary>Q339、Q350、Q362、Q372：火堆卡用新模板，副标燃料、角标状态与「加工位满」、受热挂加工中；卡上保留投入木材、点燃／熄灭、等待加工。</summary>
    [Test]
    public void Q372_A01_FireCardUsesInk2Template()
    {
        var c = Web("Components/FireCard.razor");
        Assert.That(c, Does.Contain("Ink2=\"true\""));
        Assert.That(c, Does.Contain("Badge2=\"@(full ? CardStates.FireSlotsFull : null)\""));
        Assert.That(c, Does.Contain("Busy=\"@e.FireHeating(s, here)\""));
        Assert.That(c, Does.Contain("CardStates.FireFuelSub(e, fac)"));
        Assert.That(c, Does.Contain("id=\"fc-ignite\""));
        Assert.That(c, Does.Contain("投入木材"));
        Assert.That(c, Does.Contain("等待加工"));
        Assert.That(c, Does.Not.Contain("<PotCard"), "锅从右栏火堆详情进入，不再嵌在火堆卡里");
        Assert.That(Web("Components/ObjectCard.razor"), Does.Contain("k2-tag2"));
    }

    /// <summary>Q339、Q343：右栏火堆详情有层级路径、剩余燃料、两行加工位（空位是落点、第一处空位有煅烧木炭）；选中锅或食材时路径仍在。</summary>
    [Test]
    public void Q339_A02_FireDetailSlotsAndCrumbs()
    {
        var d = Web("Components/FireDetail.razor");
        Assert.That(d, Does.Contain("<FireCrumbs"));
        Assert.That(d, Does.Contain("剩余燃料"));
        Assert.That(d, Does.Contain("data-drop=\"@($\"fireslot:{here}\")\""));
        Assert.That(d, Does.Contain("CardStates.FireSlotName(k)"));
        Assert.That(d, Does.Contain("e.FireSlotHeating(s, here, k)"));
        Assert.That(d, Does.Contain("id=\"fc-charcoal\""));
        Assert.That(d, Does.Contain("CardStates.CharcoalRuleTitle"));
        Assert.That(Web("Components/DetailPanel.razor"), Does.Contain("G.SelectedInFire is { } crumbs"));
    }

    /// <summary>Q344、Q366：点火盘打开时不预选，未选写提示；按「点火（{时长}）」提交。</summary>
    [Test]
    public void Q366_A01_IgniteMenuTwoStep()
    {
        var m = Web("Components/IgniteMenu.razor");
        Assert.That(m, Does.Contain("private int? _picked;"));
        Assert.That(m, Does.Contain("CardStates.IgnitePickHint"));
        Assert.That(m, Does.Contain("CardStates.IgniteButton(minutes)"));
        Assert.That(m, Does.Contain("disabled=\"@(picked == null || why != null"));
        Assert.That(CardStates.IgniteButton(5), Is.EqualTo("点火（5分钟）"));
    }

    /// <summary>Q365：煅烧盘逐行写投入木材、剩余燃料、工位、耗时，按钮固定「确认投料」，不能投时禁用。</summary>
    [Test]
    public void Q365_A02_CharcoalPopRows()
    {
        var p = Web("Components/ActionPops.razor");
        Assert.That(p, Does.Contain("charcoal-lines"));
        Assert.That(p, Does.Contain("确认投料"));
        Assert.That(p, Does.Not.Contain("无法投料"));
    }

    /// <summary>Q346、Q363、Q364：晾制位「查看配方」只在空闲位；「收取」打开居中盘，取到随身／地面各自提交。</summary>
    [Test]
    public void Q364_A01_DryingViewRecipesAndHarvestPop()
    {
        var d = Web("Components/DryingOverlay.razor");
        Assert.That(d, Does.Contain("查看配方"));
        Assert.That(d, Does.Contain("G.OpenPop(\"harvest\""));
        Assert.That(d, Does.Contain("CardStates.DryingSlotName("));
        var p = Web("Components/ActionPops.razor");
        Assert.That(p, Does.Contain("CardStates.HarvestToCarried"));
        Assert.That(p, Does.Contain("CardStates.HarvestToGround"));
        Assert.That(Web("Components/DryingRackCard.razor"), Does.Contain("Ink2=\"true\""));
    }

    /// <summary>Q347、Q369：安全屋面板关闭按钮「返回」，写当前位置、休息条件，带进入屋内／离开安全屋入口。</summary>
    [Test]
    public void Q369_A02_SafehousePanelHere()
    {
        var o = Web("Components/SafehouseOverlay.razor");
        Assert.That(o, Does.Contain("返回"));
        Assert.That(o, Does.Contain("当前位置"));
        Assert.That(o, Does.Contain("休息条件"));
        Assert.That(o, Does.Contain("进入屋内"));
        Assert.That(o, Does.Contain("离开安全屋"));
        Assert.That(o, Does.Contain("CardStates.SafehouseRestLines"));
        Assert.That(Web("Components/SafehouseCard.razor"), Does.Contain("Ink2=\"true\""));
    }

    /// <summary>Q341、Q367、Q368、Q370、Q371：动效时长挂 --motion-k，新卡与工位只亮一次，中央窗 180ms，轨迹 220ms、换景 140/180ms，减少动效时跳过；食饮只描即时效果改变的行。</summary>
    [Test]
    public void Q341_A01_MotionWiring()
    {
        var css = Web("wwwroot/css/app.css");
        Assert.That(css, Does.Contain(".card.fresh { animation: fresh calc(1.6s * var(--motion-k)) ease-out 1; }"));
        Assert.That(css, Does.Contain("animation: cp-in calc(.18s * var(--motion-k)) ease-out; }"));
        var shell = Web("wwwroot/css/shell.css");
        Assert.That(shell, Does.Contain(".fx-swap"));
        Assert.That(shell, Does.Contain(".fx-scene-old"));
        Assert.That(shell, Does.Contain("@keyframes fx-lit"));
        var js = Web("wwwroot/js/app.js");
        Assert.That(js, Does.Contain("window.survivalFx"));
        Assert.That(js, Does.Contain("duration: 220 * k()"));
        Assert.That(js, Does.Contain("setTimeout(function () { play(owner, itemId, from); }, 30);"), "等卡架重绘落到 DOM 再找去向卡");
        Assert.That(js, Does.Contain("[data-card=\"' + owner + '|' + itemId + '\"]"), "堆叠卡也要能当轨迹去向");
        Assert.That(js, Does.Contain("prefers-reduced-motion: reduce"));
        var g = Web("Services/GameSession.cs");
        Assert.That(g, Does.Contain("var changed = before != null && S.Actors.TryGetValue(feedTarget!, out var now) ? CardStates.ChangedStats(before, now.Stats) : null;"));
        Assert.That(g, Does.Contain("QueueSwap("));
        Assert.That(g, Does.Contain("MergeSwapHighlight();"), "Q368：轨迹与新卡高亮落到同一目标时只亮一次");
        Assert.That(Web("Components/GameScreen.razor"), Does.Contain("survivalFx.sceneSync"));
    }
}
