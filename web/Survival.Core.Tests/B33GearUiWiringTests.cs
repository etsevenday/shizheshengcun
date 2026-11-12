namespace Survival.Core.Tests;

/// <summary>
/// B33 整备与交接界面接线（P03a—P03d）：测试工程不引用网页壳，这里读组件源码确认入口、层级与轨迹接上了；规则由 B33GearTests 测。
/// 验收 Q429-A01、Q431-A01/A02、Q433-A01、Q434-A01、Q435-A01/A02、Q448-A01、Q449-A01、Q450-A01、Q453-A01、Q460-A01/A02、Q462-A01。
/// </summary>
public class B33GearUiWiringTests
{
    private static string Web(string relative)
    {
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "doc", "策划案"))) dir = dir.Parent;
        Assert.That(dir, Is.Not.Null, "找不到仓库根目录");
        return File.ReadAllText(Path.Combine(dir!.FullName, "web", "Survival.Web", relative));
    }

    /// <summary>Q429-A01、Q433-A01：三栏无底部托盘；本人随身列全部根卡、地面只列可穿装备；关闭键、空态与摘要按定稿，去页头长说明与制作来源小字。</summary>
    [Test]
    public void Q429_Q433_ThreeColumnsAndTexts()
    {
        var g = Web("Components/GearOverlay.razor");
        Assert.That(g, Does.Contain("var carried = s.Items.Where(i => i.Owner == carriedOwner)"));
        Assert.That(g, Does.Contain("i.Owner == s.Ground && e.IsWearable(i.ItemId)"));
        Assert.That(g, Does.Contain("GearPanelCopy.PartyHead(").And.Contain("GearPanelCopy.MemberLine(").And.Contain("GearPanelCopy.SummaryCards(").And.Contain("GearPanelCopy.SummaryWeight("));
        Assert.That(g, Does.Contain("GearPanelCopy.Close").And.Contain("GearPanelCopy.CarriedEmpty").And.Contain("GearPanelCopy.GroundEmpty").And.Contain("GearPanelCopy.EmptyWork"));
        Assert.That(g, Does.Not.Contain("EquipmentSource(").And.Not.Contain("七槽装备 · 随身五卡").And.Not.Contain("只有你一人同行"));
        Assert.That(g, Does.Contain("img/portraits/protagonist.svg").And.Contain("img/portraits/unknown.svg"));
        Assert.That(g, Does.Not.Contain("<AnchorPop"), "整备页内工作区保持原位置");
    }

    /// <summary>Q431、Q453-1/6：槽上一键卸下不开预演（点按钮不冒泡成点槽），点槽交给 GearClickSlot；拖拽键与落点键接到 DragController。</summary>
    [Test]
    public void Q431_SlotClickUnequipAndDrag()
    {
        var g = Web("Components/GearOverlay.razor");
        Assert.That(g, Does.Contain("@onclick=\"() => G.GearClickSlot(sl)\""));
        Assert.That(g, Does.Contain("@onclick=\"() => Unequip(sl, false)\" @onclick:stopPropagation=\"true\""));
        Assert.That(g, Does.Contain("data-drop=\"@($\"gearslot:{sl}\")\"").And.Contain("data-drag=\"@($\"gear:{inst.InstanceId}\")\""));
        var d = Web("Services/DragController.cs");
        Assert.That(d, Does.Contain("G.GearDropToSlot(gid,"));
        var s = Web("Services/GameSession.Gear.cs");
        Assert.That(s, Does.Contain("if (!E.SlotsFor(it.ItemId).Contains(slot))"), "不相容槽不打开比较");
    }

    /// <summary>Q448、Q449、Q460：随身装备比较底部与非装备工作区都有「交给…」；单人隐藏、整体受阻置灰就近写原因；Esc 逐层退回。</summary>
    [Test]
    public void Q448_Q449_Q460_GiveEntryAndEsc()
    {
        Assert.That(Web("Components/GearCompare.razor"), Does.Contain("<GearGiveEntry />"));
        Assert.That(Web("Components/GearOverlay.razor"), Does.Contain("<GearGiveEntry />").And.Contain("<GearGive />"));
        var entry = Web("Components/GearGiveEntry.razor");
        Assert.That(entry, Does.Contain("e.GiveReceivers(s, me).Count > 0"));
        Assert.That(entry, Does.Contain("e.GiveEntryBlocked(s, me, src.InstanceId)").And.Contain("disabled=\"@(why != null)\""));
        Assert.That(entry, Does.Contain("src.Owner == ItemInstance.CarriedOwner(me)"), "地面候选不给交接入口");
        var screen = Web("Components/GameScreen.razor");
        Assert.That(screen, Does.Contain("case EscOwner.GearSub: G.GearBack(); break;"));
        Assert.That(Web("Services/GameSession.Keys.cs"), Does.Contain("if (GearHasSubLayer) return EscOwner.GearSub;"), "B43：顺序在 NormalEscOwner");
        var s = Web("Services/GameSession.Gear.cs");
        Assert.That(s, Does.Contain("if (GearSource == null) ClearGearWork();"), "来源失效直接回空态");
    }

    /// <summary>Q450-A01、Q462-A01：页内点选只记 GearPeekId，不动 Selected；右栏临时只读详情不含自用预估、批次按钮和操作页脚；关整备即清。</summary>
    [Test]
    public void Q450_Q462_PeekDetailReadOnly()
    {
        var s = Web("Services/GameSession.Gear.cs");
        Assert.That(s, Does.Not.Contain("Selected =").And.Not.Contain("Select(").And.Not.Contain("PickBatch("));
        Assert.That(s, Does.Contain("if (State == null || Panel != \"gear\")"));
        var peek = Web("Components/GearPeekDetail.razor");
        Assert.That(peek, Does.Contain("e.GearPlaceText(s, it)"));
        Assert.That(peek, Does.Not.Contain("<span>即时效果").And.Not.Contain("<span>5分钟后").And.Not.Contain("<span>耗时").And.Not.Contain("detail-foot").And.Not.Contain("PickBatch").And.Not.Contain("G.Preview("));
        Assert.That(Web("Components/DetailPanel.razor"), Does.Contain("<GearPeekDetail Item=\"gearPeek\" />"));
    }

    /// <summary>Q435-A01、Q453-7：更换候选卡→槽、交接来源卡→接收人行、卸下槽→实际去向卡、右栏快捷卸下各一次；快捷装备只在槽行可见时亮。</summary>
    [Test]
    public void Q435_Trajectories()
    {
        var s = Web("Services/GameSession.Gear.cs");
        Assert.That(s, Does.Contain("QueueFly($\"[data-gear-slot=\\\"{p.Slot}\\\"]\");"));
        Assert.That(s, Does.Contain("QueueFly($\"[data-gear-member=\\\"{to}\\\"]\");"));
        Assert.That(s, Does.Contain("public ActionResult QuickUnequip(").And.Contain("public ActionResult QuickEquip("));
        Assert.That(Web("Components/CharacterPanel.razor"), Does.Contain("QuickUnequip(slot, false)").And.Contain("QuickUnequip(slot, true)"));
        Assert.That(Web("Components/CardActions.razor"), Does.Contain("G.QuickEquip(wid, slot)"));
        var screen = Web("Components/GameScreen.razor");
        Assert.That(screen, Does.Contain("survivalFx.fly").And.Contain("survivalFx.litIfVisible"));
        var js = Web("wwwroot/js/app.js");
        Assert.That(js, Does.Contain("function mark(sel)").And.Contain("function fly(toSel)").And.Contain("function litIfVisible(sel)"));
        Assert.That(js, Does.Contain("if (!fromOk || reduced() || !document.body.animate) { lit(to); return; }"), "减少动态效果只静态描框");
    }

    /// <summary>试玩修复 P1：比较／交接盘打开时右栏让给工作区（来源列表收起只留所选来源卡），标题与「返回」在滚动区外，结果与确认固定在底部。</summary>
    [Test]
    public void PlayFix_P1_WorkspaceTakesRightColumn()
    {
        var g = Web("Components/GearOverlay.razor");
        Assert.That(g, Does.Contain("var working = src != null && (G.GivePending != null || G.GearSlot != null);"));
        Assert.That(g, Does.Contain("@if (working && src != null)").And.Contain("@SourceCard(src, src.Owner == carriedOwner)"));
        foreach (var file in new[] { "Components/GearCompare.razor", "Components/GearGive.razor" })
        {
            var c = Web(file);
            var head = c.IndexOf("class=\"g3-whead\"", StringComparison.Ordinal);
            var body = c.IndexOf("class=\"g3-wbody\"", StringComparison.Ordinal);
            var foot = c.IndexOf("class=\"g3-wfoot\"", StringComparison.Ordinal);
            Assert.That(head, Is.GreaterThan(0), file);
            Assert.That(body, Is.GreaterThan(head), file + "：标题与返回在滚动区之前");
            Assert.That(foot, Is.GreaterThan(body), file + "：结果与确认在滚动区之后");
        }
        var confirmEquip = Web("Components/GearCompare.razor");
        Assert.That(confirmEquip.IndexOf("GearPanelCopy.ConfirmEquip", StringComparison.Ordinal), Is.GreaterThan(confirmEquip.IndexOf("class=\"g3-wfoot\"", StringComparison.Ordinal)));
        Assert.That(confirmEquip.IndexOf("GearPanelCopy.CardsAfter", StringComparison.Ordinal), Is.GreaterThan(confirmEquip.IndexOf("class=\"g3-wfoot\"", StringComparison.Ordinal)));
        var css = Web("wwwroot/css/app.css");
        Assert.That(css, Does.Contain(".g3-side.working { grid-template-rows: auto minmax(0, 1fr); }"));
        Assert.That(css, Does.Contain(".g3-wbody { flex: 1 1 auto; min-height: 0; overflow-y: auto;"));
    }

    /// <summary>试玩修复 P2：整备开着但看的是别的队员时，右栏快捷卸下的去向卡被整备页盖住，只亮起点；轨迹去向被上层盖住也按不可见处理。</summary>
    [Test]
    public void PlayFix_P2_QuickUnequipHiddenTarget()
    {
        var s = Web("Services/GameSession.Gear.cs");
        Assert.That(s, Does.Contain("QueueFly(QuickUnequipFlyTarget(it, toGround));"));
        Assert.That(s, Does.Contain("if (Panel != \"gear\") return ShelfCardSelector("));
        Assert.That(s, Does.Contain("return GearActorId == GameState.PlayerId ? $\"[data-gear-src=\\\"{it.InstanceId}\\\"]\" : FlyNoTarget;"));
        Assert.That(s, Does.Contain("public const string FlyNoTarget = \"\";"));
        var js = Web("wwwroot/js/app.js");
        Assert.That(js, Does.Contain("if (to && covered(to)) to = null;"));
        Assert.That(js, Does.Contain("function covered(el)").And.Contain("document.elementFromPoint(x, y)"));
        Assert.That(js, Does.Contain("var to = toSel ? document.querySelector(toSel) : null;"), "空选择器即无去向，只亮起点");
    }
}
