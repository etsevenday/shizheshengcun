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
    /// FAC-Q870-01（网页壳）：晾制中的位只有一个「取出」（撤原「取消·到随身／到地面」），点它原料回本区地面、该位空出、日志「已取出至{地点}地面：野果×2。」；
    /// 面板「全部取出」此时可用且无悬停（Q879 A），取完后置灰并悬停「没有可取出的物品。」。
    /// </summary>
    [Test]
    public void DryingSlot_TakeButton_ReturnsToGround_TakeAllThenGreyed()
    {
        Rack();
        StartBerries();
        var cut = Ctx.RenderComponent<DryingOverlay>();
        var slot0 = cut.Find("[data-dry-slot='0']");
        Assert.That(slot0.QuerySelectorAll("button").Select(b => b.TextContent.Trim()), Is.EqualTo(new[] { "取出" }), "晾制中只一个「取出」");
        var all = cut.Find("[data-pt=dry-take-all]");
        Assert.That((all.TextContent, all.HasAttribute("disabled"), all.HasAttribute("title")), Is.EqualTo(("全部取出", false, false)), "可用时无悬停");

        cut.Find("[data-pt=dry-take][data-slot='0']").Click();

        Assert.That(DrySlots, Is.Empty, "该位空出");
        Assert.That(GroundSum("berry"), Is.EqualTo(2), "原料回本区地面");
        Assert.That(S.Log.Last().Text, Is.EqualTo($"已取出至{E.ZoneName(S, S.Ground)}地面：野果×2。"), "Q876 B 结果句");
        all = cut.Find("[data-pt=dry-take-all]");
        Assert.That((all.HasAttribute("disabled"), all.GetAttribute("title")), Is.EqualTo((true, Empty)), "空设施置灰、悬停真实原因");
    }

    /// <summary>B51-Q778-02（网页壳）：点面板「全部取出」把两位在制原料一次放到地面，结果同名合量「野果×4」。</summary>
    [Test]
    public void DryingTakeAll_Button_TakesEverySlot()
    {
        Rack();
        StartBerries();
        StartBerries();
        Assert.That(DrySlots, Has.Count.EqualTo(2));
        var cut = Ctx.RenderComponent<DryingOverlay>();
        cut.Find("[data-pt=dry-take-all]").Click();
        Assert.That(DrySlots, Is.Empty);
        Assert.That(GroundSum("berry"), Is.EqualTo(4));
        Assert.That(S.Log.Last().Text, Is.EqualTo($"已取出至{E.ZoneName(S, S.Ground)}地面：野果×4。"));
    }

    /// <summary>FAC-Q871-01（网页壳）：主行动进行中，晾制中位「取出」与「全部取出」都置灰、悬停「行动进行中，暂不能整理物品」；点了也不动（配对：空闲时可点）。</summary>
    [Test]
    public void DryingTake_BusyMain_GreyedWithReason()
    {
        Rack();
        StartBerries();
        Ok(E.Start(S, ActionRequest.Wait(30)));
        var cut = Ctx.RenderComponent<DryingOverlay>();
        var take = cut.Find("[data-pt=dry-take][data-slot='0']");
        Assert.That((take.HasAttribute("disabled"), take.GetAttribute("title")), Is.EqualTo((true, Busy)));
        var all = cut.Find("[data-pt=dry-take-all]");
        Assert.That((all.HasAttribute("disabled"), all.GetAttribute("title")), Is.EqualTo((true, Busy)));
        Assert.That(DrySlots.Single().Status, Is.EqualTo(DryingSlot.Drying));

        E.Cancel(S);
        cut.Render();
        Assert.That(cut.Find("[data-pt=dry-take][data-slot='0']").HasAttribute("disabled"), Is.False, "空闲恢复可点");
    }

    /// <summary>
    /// B51C-Q880-01（网页壳）：停止位状态「待取出」，按钮「取到随身」「取到地面」，悬停「取出剩余物品。」；点「取到地面」余物到地面。
    /// 无余物的异常位只有「清空」，点后该位空出、日志「已清空。」。（取代 B50a Q713 读源码 v.CleanupHint 的旧断言）
    /// </summary>
    [Test]
    public void StoppedSlot_RestButtons_AndClear()
    {
        Rack();
        foreach (var k in new[] { 0, 1 })
            DrySlots.Add(new DryingSlot
            {
                Slot = k, BatchId = 7100 + k, RecipeId = "dry_meat", Status = DryingSlot.Spoiled, StartMinute = S.GameMinute - 60, DoneMinutes = 55,
                RequiredMinutes = 180, EndMinute = S.GameMinute - 5, InitiatorActorId = GameState.PlayerId, StopReason = DryingSlot.StopMissing,
            });
        E.CreateItems(S, "salt", 1, ItemInstance.DryingOwner(S.Ground, 0));   // 起点：位 1 留有余物盐×1，位 2 已空
        var cut = Ctx.RenderComponent<DryingOverlay>();

        var s0 = cut.Find("[data-dry-slot='0']");
        Assert.That(s0.QuerySelector(".dry-state")!.TextContent, Is.EqualTo("待取出"), "Q880：待清理 → 待取出");
        var carried = cut.Find("[data-pt=dry-rest-carried][data-slot='0']");
        var ground = cut.Find("[data-pt=dry-rest-ground][data-slot='0']");
        Assert.That((carried.TextContent, carried.GetAttribute("title")), Is.EqualTo(("取到随身", "取出剩余物品。")));
        Assert.That((ground.TextContent, ground.GetAttribute("title")), Is.EqualTo(("取到地面", "取出剩余物品。")));
        Assert.That(cut.FindAll("[data-pt=dry-clear][data-slot='0']"), Is.Empty, "有余物不出「清空」");
        var clear = cut.Find("[data-pt=dry-clear][data-slot='1']");
        Assert.That(clear.TextContent, Is.EqualTo("清空"));
        Assert.That(cut.FindAll("[data-slot='1'][data-pt^=dry-rest]"), Is.Empty, "无余物不出取到随身／地面");

        ground.Click();
        Assert.That(GroundSum("salt"), Is.EqualTo(1), "余物到地面");
        Assert.That(DrySlots.Select(b => b.Slot), Is.EqualTo(new[] { 1 }));

        cut.Find("[data-pt=dry-clear][data-slot='1']").Click();
        Assert.That(DrySlots, Is.Empty);
        Assert.That(S.Log.Last().Text, Is.EqualTo("已清空。"), "Q880：异常空位复位 → 已清空。");
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

    // ───────────── 试玩修复：已晾好位「收取」按取出门槛置灰（S11 Q871 B） ─────────────

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

    private IRenderedComponent<ActionPops> OpenHarvestPop(IRenderedComponent<DryingOverlay> cut)
    {
        cut.Find("[data-pt=dry-harvest][data-slot='0']").Click();
        Assert.That(G.Pop?.Kind, Is.EqualTo("harvest"), "起点：收取盘打开");
        return Ctx.RenderComponent<ActionPops>();
    }

    /// <summary>配对（Q346／Q364）：空闲时「收取」可点、悬停原句，盘里「取到地面」可点，点后果干到地面。</summary>
    [Test]
    public void Harvest_Idle_Enabled_TakesToGround()
    {
        DoneSlot();
        var cut = Ctx.RenderComponent<DryingOverlay>();
        var btn = cut.Find("[data-pt=dry-harvest][data-slot='0']");
        Assert.That((btn.HasAttribute("disabled"), btn.GetAttribute("title")), Is.EqualTo((false, "选择去向：取到随身或当前地面（0分钟），这一位空出")));
        var pops = OpenHarvestPop(cut);
        var ground = pops.Find("[data-pt=harvest-ground]");
        Assert.That(ground.HasAttribute("disabled"), Is.False);
        ground.Click();
        Assert.That(GroundSum("dried_berries"), Is.EqualTo(1));
        Assert.That(DrySlots, Is.Empty);
    }

    /// <summary>
    /// 试玩 bug（S11 Q871 B：角色主行动暂停也不能取出）：休息等主行动处于暂停时，「收取」置灰、悬停「行动进行中，暂不能整理物品」；
    /// 已打开的收取盘两键也置灰、悬停同句，果干不动。
    /// </summary>
    [Test]
    public void Harvest_MainPaused_Greyed()
    {
        DoneSlot();
        var cut = Ctx.RenderComponent<DryingOverlay>();
        var pops = OpenHarvestPop(cut);
        Ok(E.Start(S, ActionRequest.Wait(30)));
        S.Main!.Status = ActionStatus.Paused;   // 起点：主行动暂停（如休息被阈值暂停）
        cut.Render();
        pops.Render();
        var btn = cut.Find("[data-pt=dry-harvest][data-slot='0']");
        Assert.That((btn.HasAttribute("disabled"), btn.GetAttribute("title")), Is.EqualTo((true, Busy)));
        foreach (var pt in new[] { "harvest-carried", "harvest-ground" })
        {
            var b = pops.Find($"[data-pt={pt}]");
            Assert.That((b.HasAttribute("disabled"), b.GetAttribute("title")), Is.EqualTo((true, Busy)), pt);
        }
        Assert.That(DrySlots.Single().Status, Is.EqualTo(DryingSlot.Done), "果干不动");
    }

    /// <summary>试玩 bug（S11 Q871 B）：主行动进行中（如睡觉）「收取」置灰，悬停写「行动进行中，暂不能整理物品」，不再是去向说明原句。</summary>
    [Test]
    public void Harvest_MainRunning_GreyedWithReason()
    {
        DoneSlot();
        Ok(E.Start(S, ActionRequest.Wait(30)));
        var cut = Ctx.RenderComponent<DryingOverlay>();
        var btn = cut.Find("[data-pt=dry-harvest][data-slot='0']");
        Assert.That((btn.HasAttribute("disabled"), btn.GetAttribute("title")), Is.EqualTo((true, Busy)));
    }
}
