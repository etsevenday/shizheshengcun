using AngleSharp.Dom;
using Bunit;
using Newtonsoft.Json.Linq;
using Survival.Core.Engine;
using Survival.Core.State;
using Survival.Web.Components;
using Survival.Web.Services;

namespace Survival.Web.Tests;

/// <summary>
/// B59a「晾制架拖入即投料、取出收敛、火源和锅槽位静默」网页壳行为（S11／S07／S14「2026-11-19 B51c2a2原批Q1028—Q1030」§Q1028／§Q1029，
/// 「2026-11-24 Q1033–Q1039」§Q1033 A／§Q1034 用户另定／§Q1035 A＋手写）：
/// 真正渲染晾制架面板（DryingOverlay）、Shift 数量框（DropQuantity）、火堆加工菜单（FireMenu）、锅卡（PotCard），并直接调拖放控制器（DragController，drag.js 调的同一组 JSInvokable），
/// 断玩家看到的标题、卡片、数量与拖放反馈种类。起点用内核入口或 Debug 造，之后只经组件或控制器操作。
/// </summary>
public class B59aDryingUiTests : WebTestBase
{
    private void Ok(ActionResult r) => Assert.That(r.Ok, Is.True, r.Reason);

    private int Sum(string itemId, string owner) => S.Items.Where(i => i.ItemId == itemId && i.Owner == owner).Sum(i => i.Count);

    private List<DryingSlot> DrySlots => S.Locations[S.Ground].Facility.DryingSlots;

    /// <summary>营地建好晾制架（开发入口不扣料），烹饪等级够晾制（晾制肉干 5 级），清掉随身与地面的相关物品。</summary>
    private void Rack()
    {
        E.DebugBuildAllWorkshops(S);
        S.Skills["cooking"].Level = 5;
        S.Items.RemoveAll(i => i.ItemId is "berry" or "raw_meat" or "salt" or "dried_berries");
    }

    private void StartBerries()
    {
        E.CreateItems(S, "berry", 2, ItemInstance.Carried);
        Ok(E.StartDrying(S, "dry_berries"));
    }

    private void StartMeat()
    {
        E.CreateItems(S, "raw_meat", 1, ItemInstance.Carried);
        E.CreateItems(S, "salt", 1, ItemInstance.Carried);
        Ok(E.StartDrying(S, "dry_meat"));
    }

    private static IElement Head(IRenderedComponent<DryingOverlay> cut, int k) => cut.Find($"[data-pt=dry-toggle][data-slot='{k}']");

    private static string Title(IRenderedComponent<DryingOverlay> cut, int k) => Head(cut, k).QuerySelector(".dry-title")!.TextContent;

    // ───────────── §Q1034 收起标题与展开；§Q1035 逐份卡 ─────────────

    /// <summary>
    /// B51C2A2-Q1034-01（网页壳）：占用位默认收起，标题逐字「{首件物品名}等」（野果×2 的位→「野果等」）、aria-expanded=false、不列原料卡；
    /// 点标题展开：标题回「工位1」、列出逐份原料卡两张「野果」（§Q1035「晾制位每份一张卡」）；再点收起。
    /// 配对：空位（工位2）不是可展开按钮，点了也不列卡、标题仍「工位2」（沿原空槽）。
    /// </summary>
    [Test]
    public void Q1034_CollapsedTitle_ToggleExpand_PerUnitCards()
    {
        Rack();
        StartBerries();
        var cut = Ctx.RenderComponent<DryingOverlay>();

        Assert.That(Title(cut, 0), Is.EqualTo("野果等"), "§Q1034 收起标题");
        Assert.That(Head(cut, 0).GetAttribute("aria-expanded"), Is.EqualTo("false"));
        Assert.That(cut.FindAll("[data-dry-items='0']"), Is.Empty, "收起不列卡");

        Head(cut, 0).Click();

        Assert.That(Head(cut, 0).GetAttribute("aria-expanded"), Is.EqualTo("true"));
        Assert.That(Title(cut, 0), Is.EqualTo("工位1"), "展开后标题回工位名（B32b P02c「工位{序号}」）");
        var cards = cut.FindAll("[data-dry-items='0'] [data-pt=fac-item]");
        Assert.That(cards, Has.Count.EqualTo(2), "§Q1035：野果×2 逐份两张卡，不叠卡");
        Assert.That(cards.Select(c => c.QuerySelector(".ir-name")!.TextContent), Is.EqualTo(new[] { "野果", "野果" }));
        Assert.That(cards.Select(c => c.GetAttribute("data-unit")), Is.EqualTo(new[] { "0", "1" }));

        Head(cut, 0).Click();
        Assert.That(cut.FindAll("[data-dry-items='0']"), Is.Empty, "再点收起");
        Assert.That(Title(cut, 0), Is.EqualTo("野果等"));

        // 配对：空位不展开
        Assert.That(Head(cut, 1).HasAttribute("aria-expanded"), Is.False, "空位不是展开按钮");
        Head(cut, 1).Click();
        Assert.That(cut.FindAll("[data-dry-items='1']"), Is.Empty);
        Assert.That(Title(cut, 1), Is.EqualTo("工位2"));
    }

    /// <summary>
    /// B51C2A2-Q1034-01 后半（网页壳）：首件物品离开后标题按新的首件重算——肉干位（生肉先投、盐后投）收起时「生肉等」，取走生肉后收起标题「盐等」。
    /// </summary>
    [Test]
    public void Q1034_CollapsedTitle_RecomputesAfterFirstTaken()
    {
        Rack();
        StartMeat();
        var cut = Ctx.RenderComponent<DryingOverlay>();
        Assert.That(Title(cut, 0), Is.EqualTo("生肉等"));

        Head(cut, 0).Click();
        var meat = cut.FindAll("[data-dry-items='0'] [data-pt=fac-item]").Single(c => c.QuerySelector(".ir-name")!.TextContent == "生肉");
        meat.QuerySelector("button.card-arrow")!.Click(detail: 0);
        Assert.That(Sum("raw_meat", ItemInstance.Carried), Is.EqualTo(1), "起点：生肉经卡片箭头取到随身");
        Head(cut, 0).Click();

        Assert.That(Title(cut, 0), Is.EqualTo("盐等"), "§Q1034：首件离开后按新的首件重算");
    }

    /// <summary>
    /// B51C2A2-Q1035-01（网页壳）：点野果×2 的第二张卡——只有这张卡标选中（数量窗跟着这张卡开），数量窗上限＝本位这种物品的全部合法份数「最多 2」；
    /// 配对（§Q1035「不从别的晾制位……凑数」）：两个肉干位各有盐×1，点位 1 的盐卡数量窗「最多 1」，不算位 2 的盐。
    /// </summary>
    [Test]
    public void Q1035_CardUnitSelected_MaxIsSameItemInSlot()
    {
        Rack();
        StartBerries();
        StartMeat();
        StartMeat();
        Assert.That(DrySlots.Select(b => (b.Slot, b.RecipeId)), Is.EqualTo(new[] { (0, "dry_berries"), (1, "dry_meat"), (2, "dry_meat") }), "起点");
        var cut = Ctx.RenderComponent<DryingOverlay>();
        Head(cut, 0).Click();
        cut.FindAll("[data-dry-items='0'] [data-pt=fac-item]")[1].Click();

        var cards = cut.FindAll("[data-dry-items='0'] [data-pt=fac-item]");
        Assert.That(cards.Select(c => c.ClassList.Contains("selected")), Is.EqualTo(new[] { false, true }), "只选中点的那张卡");
        Assert.That(cut.FindAll("[data-pt=fac-take]"), Has.Count.EqualTo(1));
        Assert.That(cut.Find("[data-pt=fac-take] .small.muted").TextContent, Is.EqualTo("最多 2"), "§Q1035：上限＝本位野果全部份数");

        cut.Find("[data-pt=fac-take-cancel]").Click();
        Head(cut, 1).Click();
        cut.FindAll("[data-dry-items='1'] [data-pt=fac-item]").Single(c => c.QuerySelector(".ir-name")!.TextContent == "盐").Click();
        Assert.That(cut.Find("[data-pt=fac-take] .small.muted").TextContent, Is.EqualTo("最多 1"), "不从别的晾制位凑数");
    }

    /// <summary>
    /// §Q1035 手写「先取这张卡，再取本位同物品的其他份」（网页壳）：野果分两次投入位 1（两个实例），点后一张卡确认取 2 份到地面——这一位清空、地面野果 2 份。
    /// 配对：只确认 1 份时取走的是点的那张卡（它的实例离开位），另一张留下。
    /// </summary>
    [Test]
    public void Q1035_TakeFromSelectedCard_ThenOthersInSlot()
    {
        Rack();
        var first = E.CreateItems(S, "berry", 1, ItemInstance.Carried)[0];
        var dc = new DragController(G);
        Assert.That((bool)JObject.Parse(dc.DragDrop($"card:{ItemInstance.Carried}|berry|{first.InstanceId}", "dryslot:0", 0, false))["ok"]!, Is.True, "起点：先投 1 份");
        var second = E.CreateItems(S, "berry", 1, ItemInstance.Carried)[0];
        var ids = new[] { first.InstanceId, second.InstanceId };
        Assert.That((bool)JObject.Parse(dc.DragDrop($"card:{ItemInstance.Carried}|berry|{second.InstanceId}", "dryslot:0", 0, false))["ok"]!, Is.True, "起点：再投 1 份齐料");
        var inSlot = S.Items.Where(i => i.ItemId == "berry" && i.Owner == ItemInstance.DryingOwner(S.Ground, 0)).OrderBy(i => i.InstanceId).ToList();
        Assert.That(inSlot, Has.Count.EqualTo(2), "起点：分两次投入是两个实例（晾制位内不合堆）");
        var cut = Ctx.RenderComponent<DryingOverlay>();
        Head(cut, 0).Click();
        var lastCard = cut.FindAll("[data-dry-items='0'] [data-pt=fac-item]")[1];
        var picked = int.Parse(lastCard.GetAttribute("data-inst")!);
        lastCard.Click();
        cut.Find("[data-pt=fac-take-ground]").Click();
        cut.Find("[data-pt=fac-take-ok]").Click();   // 默认 1 份

        var left = S.Items.Where(i => i.ItemId == "berry" && i.Owner == ItemInstance.DryingOwner(S.Ground, 0)).Select(i => i.InstanceId).ToList();
        Assert.That(left, Has.Count.EqualTo(1));
        Assert.That(left, Does.Not.Contain(picked), "取走的是点的那张卡");

        cut.Find("[data-dry-items='0'] [data-pt=fac-item]").Click();
        cut.Find("[data-pt=fac-take-ground]").Click();
        cut.Find("[data-pt=fac-take-ok]").Click();
        Assert.That(Sum("berry", S.Ground), Is.EqualTo(2));
        Assert.That(DrySlots, Is.Empty, "§Q1029：最后一件离开后回到空位");
    }

    // ───────────── §Q1029／§Q1033 拖放反馈（DragController） ─────────────

    /// <summary>
    /// B51C2A2-Q1029-01（网页壳）：拖随身野果×3 起拖时，在制的位 1 是 quiet（不高亮、无原因、不出禁止标），空位 2 是 instant、可 Shift 选量，
    /// 短动作「投入野果×2」（暂用文案，开发记录 B59a.6；份数＝min(缺 2, 这堆 3)，§Q1033 A）。
    /// 配对：行动进行中拖到随身区仍是 no（有真实原因「行动进行中，只能整理卡片顺序」，B10 拖放表），quiet 只给静默拒绝。
    /// </summary>
    [Test]
    public void Q1029_DragBegin_QuietVsInstant()
    {
        Rack();
        StartBerries();
        var berry = E.CreateItems(S, "berry", 3, ItemInstance.Carried)[0];
        var dc = new DragController(G);
        var map = JObject.Parse(dc.DragBegin($"card:{ItemInstance.Carried}|berry|{berry.InstanceId}", new[] { "dryslot:0", "dryslot:1" }));

        Assert.That(((bool)map["dryslot:0"]!["ok"]!, (string)map["dryslot:0"]!["kind"]!, (string)map["dryslot:0"]!["reason"]!), Is.EqualTo((false, "quiet", "")));
        Assert.That(((bool)map["dryslot:1"]!["ok"]!, (string)map["dryslot:1"]!["kind"]!, (string)map["dryslot:1"]!["label"]!, (bool)map["dryslot:1"]!["qty"]!),
            Is.EqualTo((true, "instant", "投入野果×2", true)));

        Ok(E.Start(S, ActionRequest.Wait(30)));
        E.CreateItems(S, "stone", 1, S.Ground);
        var stone = S.Items.First(i => i.ItemId == "stone" && i.Owner == S.Ground);   // 新建可能并入已有同名堆
        map = JObject.Parse(dc.DragBegin($"card:{S.Ground}|stone|{stone.InstanceId}", new[] { "zone:carried" }));
        Assert.That(((string)map["zone:carried"]!["kind"]!, (string)map["zone:carried"]!["reason"]!), Is.EqualTo(("no", "行动进行中，只能整理卡片顺序")), "配对：有原因的拒绝仍是 no");
    }

    /// <summary>
    /// B51C2A2-Q1033-01（网页壳）：随身野果×3 普通松手到空位 2——默认投 2 份（min(缺 2, 这堆 3)），齐料即开始晾制、随身剩 1 份、不弹提示；
    /// 再把剩下的 1 份松手到在制的位 2——静默拒绝：返回原因为空、不扣物、不弹提示（§Q1029）。
    /// </summary>
    [Test]
    public void Q1033_DragDrop_DefaultQuantity_ThenSilentReject()
    {
        Rack();
        var berry = E.CreateItems(S, "berry", 3, ItemInstance.Carried)[0];
        var dc = new DragController(G);
        var key = $"card:{ItemInstance.Carried}|berry|{berry.InstanceId}";
        dc.DragBegin(key, new[] { "dryslot:1" });
        var res = JObject.Parse(dc.DragDrop(key, "dryslot:1", 0, false));

        Assert.That(((bool)res["ok"]!, (string)res["reason"]!), Is.EqualTo((true, "")));
        Assert.That(DrySlots.Single().Slot, Is.EqualTo(1));
        Assert.That(DrySlots.Single().Status, Is.EqualTo(DryingSlot.Drying), "§Q1033：齐料即开始晾制");
        Assert.That(Sum("berry", ItemInstance.Carried), Is.EqualTo(1), "默认投 2 份，随身剩 1");
        Assert.That(G.Notice, Is.Null);

        var rest = S.Items.Single(i => i.ItemId == "berry" && i.IsCarried);
        key = $"card:{ItemInstance.Carried}|berry|{rest.InstanceId}";
        dc.DragBegin(key, new[] { "dryslot:1" });
        res = JObject.Parse(dc.DragDrop(key, "dryslot:1", 0, false));
        Assert.That(((bool)res["ok"]!, (string)res["reason"]!), Is.EqualTo((false, "")), "§Q1029：静默拒绝不给原因");
        Assert.That(Sum("berry", ItemInstance.Carried), Is.EqualTo(1), "不扣物");
        Assert.That(G.Notice, Is.Null, "不弹提示");
    }

    /// <summary>
    /// B51C2A2-Q1033-01 Shift（网页壳）：Shift 松手到空晾制位打开数量框——标题「投料 · 野果」（暂用文案，开发记录 B59a.6）、说明「投入野果 · 最多 2」、输入框默认 2；
    /// 改 1 确认后位里 1 份、成余料（未齐不开工，§Q1033 A）。配对：Shift 拖到地面的普通转移数量框默认仍是 1。
    /// </summary>
    [Test]
    public void Q1033_Shift_QuantityBox_DefaultIsMax()
    {
        Rack();
        var berry = E.CreateItems(S, "berry", 3, ItemInstance.Carried)[0];
        var dc = new DragController(G);
        var key = $"card:{ItemInstance.Carried}|berry|{berry.InstanceId}";
        var box = Ctx.RenderComponent<DropQuantity>();
        dc.DragBegin(key, new[] { "dryslot:0" });
        dc.DragDrop(key, "dryslot:0", 0, true);
        box.Render();

        Assert.That(box.Find("h3.dq-head").TextContent, Is.EqualTo("投料 · 野果"));
        Assert.That(box.Find(".modal .small.muted").TextContent, Is.EqualTo("投入野果 · 最多 2"));
        Assert.That(box.Find("input[aria-label=数量]").GetAttribute("value"), Is.EqualTo("2"), "§Q1033 A：默认份数");

        box.Find("input[aria-label=数量]").Input("1");
        box.Find("button.primary").Click();
        Assert.That(DrySlots.Single().Status, Is.EqualTo(DryingSlot.Residual), "未齐留在位里");
        Assert.That(Sum("berry", ItemInstance.DryingOwner(S.Ground, 0)), Is.EqualTo(1));

        // 配对：普通转移默认 1
        var rest = S.Items.First(i => i.ItemId == "berry" && i.IsCarried);
        key = $"card:{ItemInstance.Carried}|berry|{rest.InstanceId}";
        dc.DragBegin(key, new[] { $"zone:{S.Ground}" });
        dc.DragDrop(key, $"zone:{S.Ground}", 0, true);
        box.Render();
        Assert.That(G.PendingDrop, Is.Not.Null, "起点：数量框打开");
        Assert.That(box.Find("input[aria-label=数量]").GetAttribute("value"), Is.EqualTo("1"), "配对：普通转移默认 1");
    }

    // ───────────── §Q1028 火源与锅槽位：不显示原因 ─────────────

    private ItemInstance PotWithTwoFoods()
    {
        var pot = S.Items.First(i => i.ItemId == "pot");
        Ok(E.DoInstant(S, InstantRequest.PutInFire(pot.InstanceId)));
        E.DebugGive(S, "raw_root", 3, S.Ground);
        for (var j = 0; j < 2; j++)   // 锅食材槽 2 位（A12／策划增量配置 v0.4）
            Ok(E.DoInstant(S, InstantRequest.PutFoodInPot(S.Items.First(i => i.ItemId == "raw_root" && i.Owner == S.Ground).InstanceId, pot.InstanceId)));
        return pot;
    }

    /// <summary>
    /// B51C2A2-Q1028-02（网页壳，火堆加工菜单）：选火堆里的锅为目标、锅食材槽已满——生块根来源行标灰「不可放入」，但不给「原因」按钮、悬停不接原因（§Q1028 撤「已满」类说明）；
    /// 配对：同一目标下木材的行仍有「原因」按钮（「木材不能放进锅里」不属满位说明，按钮入口照旧给真实原因）。
    /// </summary>
    [Test]
    public void Q1028_FireMenu_FullPotRow_NoWhyButton()
    {
        PotWithTwoFoods();
        E.CreateItems(S, "wood", 1, ItemInstance.Carried);
        G.OpenFireMenu();
        G.FireMenuPickSlot(0);
        Assert.That(G.FireMenuTarget(out _, out var target), Is.True);
        Assert.That(target, Is.Not.Null, "起点：目标是火堆里的锅");
        var cut = Ctx.RenderComponent<FireMenu>();

        var root = cut.FindAll(".fm-src-row.no").Single(r => r.QuerySelector(".fm-src-name")!.TextContent.StartsWith("生块根"));
        Assert.That(root.QuerySelector(".fm-src-no")!.TextContent, Is.EqualTo("不可放入"));
        Assert.That(root.QuerySelector(".fm-src-whybtn"), Is.Null, "§Q1028：满位不给原因");
        Assert.That(root.GetAttribute("title"), Does.Not.Contain("："), "悬停不接原因");

        var wood = cut.FindAll(".fm-src-row.no").First(r => r.QuerySelector(".fm-src-name")!.TextContent.StartsWith("木材"));
        Assert.That(wood.QuerySelector(".fm-src-whybtn")!.GetAttribute("title"), Is.EqualTo("木材不能放进锅里"), "配对：非满位的真实原因照旧有「原因」（BM5 锅食材原句）");
    }

    /// <summary>
    /// B59a（S11 §Q1028）锅卡：锅里已有食材时锅食谱按钮照真实合法性置灰，下方不出「{食谱}：……」原因行；
    /// 配对：取空食材后原因行照旧出现（锅里没有水等原真实条件）。
    /// </summary>
    [Test]
    public void Q1028_PotCard_HasFood_NoRecipeWhy()
    {
        E.DebugLearnAllKnowledge(S);   // 起点：锅食谱做法已知（同 B16PotRecipeTests）
        S.Skills["cooking"].Level = 5;  // 起点：烹饪等级够
        var pot = S.Items.First(i => i.ItemId == "pot");
        Ok(E.DoInstant(S, InstantRequest.PutInFire(pot.InstanceId)));
        E.DebugGive(S, "raw_root", 1, S.Ground);
        Ok(E.DoInstant(S, InstantRequest.PutFoodInPot(S.Items.First(i => i.ItemId == "raw_root" && i.Owner == S.Ground).InstanceId, pot.InstanceId)));
        var cut = Ctx.RenderComponent<PotCard>(p => p.Add(x => x.Pot, pot).Add(x => x.Expanded, true));   // 锅食谱在展开内容（选卡浮窗）里

        var btns = cut.FindAll("[data-pot-recipes] button");
        Assert.That(btns, Is.Not.Empty, "起点：有锅食谱");
        Assert.That(btns.Select(b => b.HasAttribute("disabled")), Is.All.True, "照真实合法性置灰");
        Assert.That(btns.Select(b => b.GetAttribute("title")!), Has.None.Contains("现在不能做"), "§Q1028：悬停不接原因");
        Assert.That(cut.FindAll(".pc-recipe-why"), Is.Empty, "§Q1028：不出原因行");

        Ok(E.DoInstant(S, InstantRequest.TakeOut(E.PotFood(S, pot.InstanceId).Single().InstanceId, S.Ground)));
        cut.Render();
        Assert.That(cut.FindAll(".pc-recipe-why").Select(x => x.TextContent), Is.Not.Empty.And.All.Contains("锅里没有水"), "配对：其他真实原因照旧");
    }
}
