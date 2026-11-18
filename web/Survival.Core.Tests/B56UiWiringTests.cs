namespace Survival.Core.Tests;

/// <summary>
/// B56 界面接线（S10／S12／S14 文首 2026-11-17 B53a Q929；02 Q975—Q979 A 暂用）：测试工程不引用网页壳，这里读组件源码确认饮水窗、
/// 同行饮水选量、水壶数量窗、丢水不足一份原因与说明句接上了；规则本身由 B56WaterTests 测。
/// </summary>
public class B56UiWiringTests
{
    private static string Web(string relative)
    {
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "doc", "策划案"))) dir = dir.Parent;
        Assert.That(dir, Is.Not.Null, "找不到仓库根目录");
        return File.ReadAllText(Path.Combine(dir!.FullName, "web", "Survival.Web", relative));
    }

    [Test]
    public void DrinkBox_TitleStepperAmountAction_FromDrinkCopy_HoverRightColumn()
    {
        var box = Web("Components/DrinkBox.razor");
        // S14 文首 B53a 逐字稿：标题「饮水」、旁写「{毫升}毫升」、开始「饮用{毫升}毫升（5分钟）」；文字取内核 DrinkCopy，不另写
        Assert.That(box, Does.Contain("@DrinkCopy.Title").And.Contain("DrinkCopy.AmountText(").And.Contain("DrinkCopy.ActionText("));
        Assert.That(box, Does.Contain("ActionRequest.UseInstance(InstanceId, n)"), "所选份数随请求进内核");
        Assert.That(box, Does.Contain("G.Hover(req, DrinkCopy.Title)"), "开始按钮悬停把右栏换成饮水预测");
        Assert.That(box, Does.Contain("await G.StartAction(r)"));
        var pv = Web("Components/PreviewView.razor");
        Assert.That(pv, Does.Contain("p.Drink is DrinkPreviewInfo drink").And.Contain("drink.Lines"), "右栏列饮水三行");
    }

    [Test]
    public void Entries_CardPotCanteenPause_AllUseDrinkBox_TempCopyQ975Q976()
    {
        var card = Web("Components/CardActions.razor");
        Assert.That(card, Does.Contain("e.Item(m.ItemId).Kind == \"water\"").And.Contain("<DrinkBox InstanceId=\"@m.UseId.Value\" />"), "水卡与锅水");
        Assert.That(card, Does.Contain("<DrinkBox InstanceId=\"@cid\" />"), "水壶");
        Assert.That(card, Does.Not.Contain("CanteenSipText").And.Not.Contain("按500毫升装倒"), "旧一口 250／按 500 装倒撤下");
        var pause = Web("Components/PauseLayer.razor");
        Assert.That(pause, Does.Contain("EntryText=\"@($\"饮用{cfg.Name}·5分钟\")\"").And.Contain("EntryText=\"仍然饮用脏水\""), "Q975 A 暂用");
        Assert.That(pause, Does.Contain("脏水每100毫升补水6，但必定腹泻12小时（期间每小时额外失水3）。"), "Q976 A 暂用");
        Assert.That(pause, Does.Not.Contain("仍然饮用1份脏水").And.Not.Contain("应急补水30"));
    }

    [Test]
    public void CanteenFillPour_QuantityWindow_Q978A()
    {
        var card = Web("Components/CardActions.razor");
        Assert.That(card, Does.Contain("CanteenFillTitle = \"装水\"").And.Contain("CanteenPourTitle = \"倒出\""));
        Assert.That(card, Does.Contain(">确认@(on)</button>"), "按钮「确认{数量}」");
        Assert.That(card, Does.Contain("InstantRequest.FillCanteen(sid, canteenId, n)").And.Contain("InstantRequest.PourCanteen(canteenId, zone, n)"), "所选份数一次装／倒");
        Assert.That(card, Does.Not.Contain("InstantRequest.FillCanteen(srcId, cid, 1)"), "不再固定 1 份");
    }

    [Test]
    public void FeedPop_WaterUnitsStepper_ResetOnSourceChange()
    {
        var pop = Web("Components/FeedPop.razor");
        Assert.That(pop, Does.Contain("d.IsWater && v.Selected is { } sel && sel.MaxUnits > 0").And.Contain("G.SetFeedWaterUnits("));
        var session = Web("Services/GameSession.cs");
        Assert.That(session, Does.Contain("f.Draft.WaterUnits = 1;"), "换来源回默认 1 份");
        // B56 试玩修复 BUG-1（开发假设 5、S12 文首 B53a「水量不足…的数量禁用原因：水分不足。」、W100-Q929-06）：原断言要求夹到 1..上限，
        // 改为只保下限 1、超量保留输入交内核 FeedPanel 报「不可用：水分不足。」（规则由 B56MutationGapTests.FeedPanel_Canteen_OverMax… 测）
        Assert.That(session, Does.Contain("f.Draft.WaterUnits = Math.Max(1, units);"));
        Assert.That(session, Does.Not.Contain("Math.Clamp(units, 1, Math.Max(1, max))"), "不再静默夹到可饮上限");
        Assert.That(pop, Does.Contain("G.SetFeedWaterUnits(ParseUnits(ev))"), "输入框原样把所选份数交给会话");
    }

    [Test]
    public void DiscardPanel_QuantityBlockedReason_MlBesideInput_Q979A()
    {
        var panel = Web("Components/DiscardPanel.razor");
        Assert.That(panel, Does.Contain("v.QuantityBlocked is string qb").And.Contain("title=\"@v.QuantityBlocked\""), "子区原因行与悬停都写「水分不足。」");
        Assert.That(panel, Does.Contain("DrinkCopy.AmountText(qn * v.UnitMl)"), "输入旁「{毫升}毫升」");
    }

    [Test]
    public void HelpAndGuide_NoOldHalfLitreSentence()
    {
        Assert.That(Web("Services/HelpCatalog.cs"), Does.Not.Contain("0.5升或1升").And.Contain("烧水可少量进行"));
        var guide = Web("Services/ProcessGuide.cs");
        Assert.That(guide, Does.Not.Contain("0.5升或1升").And.Not.Contain("每份500毫升").And.Contain("每份100毫升"));
    }
}
