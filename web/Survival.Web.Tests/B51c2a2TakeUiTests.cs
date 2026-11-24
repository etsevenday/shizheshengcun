using Bunit;
using Survival.Core.Engine;
using Survival.Core.State;
using Survival.Web.Components;

namespace Survival.Web.Tests;

/// <summary>
/// B51c2a2「被动设施取出 2：晾架＋火堆」网页壳行为（S11 Q870 B、Q871 B、Q876 B、Q877、Q879 A、Q880、Q966）：
/// 真正渲染晾制架面板（DryingOverlay）与火堆中央加工菜单（FireMenu），点「取出」「全部取出」「取到地面」「清空」，断玩家看到的按钮状态、悬停原因与结果。
/// 起点（晾制架、在制位、停止位、火堆里的锅）用内核入口或 Debug 造，之后只经组件按钮操作。
/// </summary>
public class B51c2a2TakeUiTests : WebTestBase
{
    private const string Busy = "行动进行中，暂不能整理物品";   // S11 Q871 B
    private const string Empty = "没有可取出的物品。";          // S11 Q879 A

    private void Ok(ActionResult r) => Assert.That(r.Ok, Is.True, r.Reason);

    private int GroundSum(string itemId) => S.Items.Where(i => i.ItemId == itemId && i.Owner == S.Ground).Sum(i => i.Count);

    private List<DryingSlot> DrySlots => S.Locations[S.Ground].Facility.DryingSlots;

    /// <summary>营地建好晾制架（开发入口不扣料），制造等级够晾制。</summary>
    private void Rack()
    {
        E.DebugBuildAllWorkshops(S);
        S.Skills["cooking"].Level = 5;
        Assert.That(E.DryingRackHere(S), Is.True, "起点：当前区有晾制架");
        S.Items.RemoveAll(i => i.ItemId is "berry" or "raw_meat" or "salt" or "dried_berries");
    }

    private void StartBerries()
    {
        E.CreateItems(S, "berry", 2, ItemInstance.Carried);
        Ok(E.StartDrying(S, "dry_berries"));
    }

    /// <summary>
    /// FAC-Q870-01（网页壳）→ B59a 改写（S11／S07／S14 §Q1029「取代 Q870／Q923 中与此冲突的晾制位专用“取出”、收取与停止位专用按钮」）：
    /// 晾制中的位不再有任何位按钮（原「取出」已撤），取出只走卡片与操作区「全部取出」；
    /// 面板「全部取出」此时可用且无悬停（Q879 A），点后原料回本区地面、该位空出、不写常规取出日志（§Q1029 撤常规取出日志），取完置灰并悬停「没有可取出的物品。」。
    /// </summary>
    [Test]
    public void DryingSlot_NoSlotButtons_TakeAllThenGreyed()
    {
        Rack();
        StartBerries();
        var cut = Ctx.RenderComponent<DryingOverlay>();
        var slot0 = cut.Find("[data-dry-slot='0']");
        Assert.That(slot0.QuerySelectorAll("button").Length, Is.EqualTo(0), "B59a §Q1029：晾制中的位没有专用按钮");
        Assert.That(cut.FindAll("[data-pt=dry-take]"), Is.Empty, "§Q1029：撤晾制位专用「取出」");
        var all = cut.Find("[data-pt=dry-take-all]");
        Assert.That((all.TextContent, all.HasAttribute("disabled"), all.HasAttribute("title")), Is.EqualTo(("全部取出", false, false)), "可用时无悬停");
        var logs = S.Log.Count;

        all.Click();

        Assert.That(DrySlots, Is.Empty, "该位空出");
        Assert.That(GroundSum("berry"), Is.EqualTo(2), "原料回本区地面");
        Assert.That(S.Log.Skip(logs).Select(l => l.Text).Where(x => x.StartsWith("已取出至")), Is.Empty, "§Q1029：不写常规取出日志");
        all = cut.Find("[data-pt=dry-take-all]");
        Assert.That((all.HasAttribute("disabled"), all.GetAttribute("title")), Is.EqualTo((true, Empty)), "空设施置灰、悬停真实原因");
    }

    /// <summary>B51-Q778-02（网页壳）：点面板「全部取出」把两位在制原料一次放到地面，地面同名合量 4 份（B59a：§Q1029 撤常规取出日志，原「已取出至…野果×4。」断言改为不写该日志）。</summary>
    [Test]
    public void DryingTakeAll_Button_TakesEverySlot()
    {
        Rack();
        StartBerries();
        StartBerries();
        Assert.That(DrySlots, Has.Count.EqualTo(2));
        var cut = Ctx.RenderComponent<DryingOverlay>();
        var logs = S.Log.Count;
        cut.Find("[data-pt=dry-take-all]").Click();
        Assert.That(DrySlots, Is.Empty);
        Assert.That(GroundSum("berry"), Is.EqualTo(4));
        Assert.That(S.Log.Skip(logs).Select(l => l.Text).Where(x => x.StartsWith("已取出至")), Is.Empty, "§Q1029");
    }

    /// <summary>
    /// FAC-Q871-01（网页壳）：主行动进行中，「全部取出」与位内原料卡箭头都置灰、悬停「行动进行中，暂不能整理物品」；不动（配对：空闲时可点）。
    /// B59a：原位「取出」按钮已撤（§Q1029），改断展开后的原料卡箭头。
    /// </summary>
    [Test]
    public void DryingTake_BusyMain_GreyedWithReason()
    {
        Rack();
        StartBerries();
        Ok(E.Start(S, ActionRequest.Wait(30)));
        var cut = Ctx.RenderComponent<DryingOverlay>();
        cut.Find("[data-pt=dry-toggle][data-slot='0']").Click();
        var arrow = cut.Find("[data-dry-items='0'] [data-pt=fac-item] button.card-arrow");
        Assert.That((arrow.HasAttribute("disabled"), arrow.GetAttribute("title")), Is.EqualTo((true, Busy)));
        var all = cut.Find("[data-pt=dry-take-all]");
        Assert.That((all.HasAttribute("disabled"), all.GetAttribute("title")), Is.EqualTo((true, Busy)));
        Assert.That(DrySlots.Single().Status, Is.EqualTo(DryingSlot.Drying));

        E.Cancel(S);
        cut.Render();
        Assert.That(cut.Find("[data-dry-items='0'] [data-pt=fac-item] button.card-arrow").HasAttribute("disabled"), Is.False, "空闲恢复可点");
        Assert.That(cut.Find("[data-pt=dry-take-all]").HasAttribute("disabled"), Is.False, "空闲恢复可点");
    }

    /// <summary>
    /// B51C-Q880-01（网页壳）→ B59a 改写（S11／S07／S14 §Q1029「取代……停止位专用按钮」「空的异常停止位自动恢复成普通空位，无须玩家操作」；
    /// S11「2026-11-24」§Q1034 已停止位和普通余料一样直接展示剩余物品卡，不再显示按钮、提示、说明或专用状态）：
    /// 留有余物的停止位没有「取到随身／取到地面／清空」按钮，也没有说明行，展开即见余物卡，走卡片箭头取走；最后一件离开后这一位自动回到空位（不写「已清空。」）。
    /// 无余物的异常位在下一次变更边界自动恢复成空位（不再出「清空」）。
    /// </summary>
    [Test]
    public void StoppedSlot_NoButtons_CardsOnly_AutoRestored()
    {
        Rack();
        foreach (var k in new[] { 0, 1 })
            DrySlots.Add(new DryingSlot
            {
                Slot = k, BatchId = 7100 + k, RecipeId = "dry_meat", Status = DryingSlot.Spoiled, StartMinute = S.GameMinute - 60, DoneMinutes = 55,
                RequiredMinutes = 180, EndMinute = S.GameMinute - 5, InitiatorActorId = GameState.PlayerId, StopReason = DryingSlot.StopMissing,
            });
        E.CreateItems(S, "salt", 1, ItemInstance.DryingOwner(S.Ground, 0));   // 起点：位 1 留有余物盐×1，位 2 已空（尚未经过变更边界）
        var cut = Ctx.RenderComponent<DryingOverlay>();

        Assert.That(cut.FindAll("[data-pt^=dry-rest]"), Is.Empty, "§Q1029：撤停止位「取到随身／取到地面」");
        Assert.That(cut.FindAll("[data-pt=dry-clear]"), Is.Empty, "§Q1029：撤「清空」");
        Assert.That(cut.Find("[data-dry-slot='0']").QuerySelectorAll("button").Length, Is.EqualTo(0), "停止位没有任何按钮");
        cut.Find("[data-pt=dry-toggle][data-slot='0']").Click();
        var cards = cut.FindAll("[data-dry-items='0'] [data-pt=fac-item]");
        Assert.That(cards, Has.Count.EqualTo(1));
        Assert.That(cards[0].QuerySelector(".ir-name")!.TextContent, Is.EqualTo("盐"));
        var logs = S.Log.Count;

        cards[0].QuerySelector("button.card-arrow")!.Click(detail: 0);

        Assert.That(S.Items.Where(i => i.ItemId == "salt" && i.IsCarried).Sum(i => i.Count), Is.EqualTo(1), "余物经卡片箭头到随身");
        Assert.That(DrySlots, Is.Empty, "§Q1029：位 1 最后一件离开后回到空位；位 2 空停止位同一变更边界自动恢复");
        Assert.That(S.Log.Skip(logs).Select(l => l.Text), Has.None.EqualTo("已清空。"), "§Q1029 撤清空日志");
        cut.Render();
        Assert.That(cut.FindAll("[data-pt=dry-view]"), Has.Count.EqualTo(8), "全部回到普通空位（「查看配方」；晾制架 8 位，B50a Q713 汇总「空8」口径）");
    }

    /// <summary>Q966（网页壳）：选食谱预览里的暂停说明写用户原句「晾晒时，不会变质」。</summary>
    [Test]
    public void RecipePreview_ShowsQ966PauseText()
    {
        Rack();
        E.CreateItems(S, "berry", 2, ItemInstance.Carried);
        var cut = Ctx.RenderComponent<DryingOverlay>();
        var pause = cut.Find("[data-dry-recipe='dry_berries'] .dry-pause");
        Assert.That(pause.TextContent, Is.EqualTo("晾晒时，不会变质"));
    }

    // ───────────── 火堆中央加工菜单主层「全部取出」 ─────────────

    private ItemInstance PotInFire()
    {
        var pot = S.Items.First(i => i.ItemId == "pot");
        Ok(E.DoInstant(S, InstantRequest.PutInFire(pot.InstanceId)));
        return pot;
    }

    /// <summary>
    /// B51-Q778（火堆，S11 Q877）网页壳：加工菜单主层有「全部取出」，有物时可用无悬停；点它整锅到本区地面、日志「已取出至{地点}地面：金属锅×1。」；
    /// 取完置灰、悬停「没有可取出的物品。」。
    /// </summary>
    [Test]
    public void FireMenu_TakeAll_Button()
    {
        var pot = PotInFire();
        G.OpenFireMenu();
        Assert.That(G.FireMenu, Is.Not.Null, "起点：加工菜单打开");
        var cut = Ctx.RenderComponent<FireMenu>();
        var btn = cut.Find("[data-pt=fire-take-all]");
        Assert.That((btn.TextContent, btn.HasAttribute("disabled"), btn.HasAttribute("title")), Is.EqualTo(("全部取出", false, false)));
        // B51c2a2 试玩修复（开发方排版）：与同排「熄灭」同一类按钮样式，置灰走该类 disabled 样式
        Assert.That(btn.ClassList, Is.SupersetOf(new[] { "loc-btn", "res-btn", "fc-btn", "plain" }));

        btn.Click();

        Assert.That((pot.Owner, pot.InFacility), Is.EqualTo((S.Ground, false)));
        Assert.That(S.Log.Last().Text, Is.EqualTo($"已取出至{E.ZoneName(S, S.Ground)}地面：金属锅×1。"));
        btn = cut.Find("[data-pt=fire-take-all]");
        Assert.That((btn.HasAttribute("disabled"), btn.GetAttribute("title")), Is.EqualTo((true, Empty)));
    }

    /// <summary>FAC-Q871-01（火堆）网页壳：主行动进行中「全部取出」置灰、悬停「行动进行中，暂不能整理物品」，锅不动。</summary>
    [Test]
    public void FireMenu_TakeAll_BusyGreyed()
    {
        var pot = PotInFire();
        G.OpenFireMenu();
        Ok(E.Start(S, ActionRequest.Wait(30)));
        var cut = Ctx.RenderComponent<FireMenu>();
        var btn = cut.Find("[data-pt=fire-take-all]");
        Assert.That((btn.HasAttribute("disabled"), btn.GetAttribute("title")), Is.EqualTo((true, Busy)));
        Assert.That(pot.InFacility, Is.True);
    }

    // ───────────── 已晾好位：B59a 撤「收取」（S11 §Q1029），成品走卡片取出 ─────────────

    /// <summary>起点：一位果干已晾好（配置 dry_berries 120 分钟，B50a Q666-A02），推进世界时间到完成。</summary>
    private void DoneSlot()
    {
        Rack();
        StartBerries();
        var end = S.GameMinute + 120;
        while (S.GameMinute < end)
        {
            if (S.Main == null) Ok(E.Start(S, ActionRequest.Wait(30)));
            E.Advance(S);
        }
        if (S.Main?.Type == ActionType.Wait) E.Cancel(S);
        Assert.That(DrySlots.Single().Status, Is.EqualTo(DryingSlot.Done), "起点：已晾好");
        G.OpenPanel("drying");
    }

    /// <summary>
    /// 原 Harvest_Idle_Enabled_TakesToGround（Q346／Q364 收取盘）→ B59a 改写（S11／S07／S14 §Q1029「取代 Q870／Q923 中与此冲突的……收取……按钮」）：
    /// 已晾好位没有「收取」，也不再开收取盘；展开后成品果干是一张卡，空闲时箭头可点，点后果干到随身、这一位空出。
    /// </summary>
    [Test]
    public void DoneSlot_NoHarvest_CardArrowTakes()
    {
        DoneSlot();
        var cut = Ctx.RenderComponent<DryingOverlay>();
        Assert.That(cut.FindAll("[data-pt=dry-harvest]"), Is.Empty, "§Q1029：撤收取");
        cut.Find("[data-pt=dry-toggle][data-slot='0']").Click();
        var cards = cut.FindAll("[data-dry-items='0'] [data-pt=fac-item]");
        Assert.That(cards, Has.Count.EqualTo(1));
        Assert.That(cards[0].QuerySelector(".ir-name")!.TextContent, Is.EqualTo("果干"));
        var arrow = cards[0].QuerySelector("button.card-arrow")!;
        Assert.That(arrow.HasAttribute("disabled"), Is.False);
        arrow.Click(detail: 0);
        Assert.That(S.Items.Where(i => i.ItemId == "dried_berries" && i.IsCarried).Sum(i => i.Count), Is.EqualTo(1));
        Assert.That(DrySlots, Is.Empty);
        Assert.That(G.Pop, Is.Null, "不开收取盘");
    }

    /// <summary>
    /// 试玩 bug（S11 Q871 B：角色主行动暂停也不能取出）→ B59a 改写（收取按钮已撤，§Q1029）：主行动暂停时成品卡箭头置灰、悬停「行动进行中，暂不能整理物品」，果干不动。
    /// </summary>
    [Test]
    public void DoneSlot_MainPaused_CardArrowGreyed()
    {
        DoneSlot();
        var cut = Ctx.RenderComponent<DryingOverlay>();
        cut.Find("[data-pt=dry-toggle][data-slot='0']").Click();
        Ok(E.Start(S, ActionRequest.Wait(30)));
        S.Main!.Status = ActionStatus.Paused;   // 起点：主行动暂停（如休息被阈值暂停）
        cut.Render();
        var arrow = cut.Find("[data-dry-items='0'] [data-pt=fac-item] button.card-arrow");
        Assert.That((arrow.HasAttribute("disabled"), arrow.GetAttribute("title")), Is.EqualTo((true, Busy)));
        Assert.That(DrySlots.Single().Status, Is.EqualTo(DryingSlot.Done), "果干不动");
    }
}
