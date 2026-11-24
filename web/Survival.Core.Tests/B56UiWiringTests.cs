using Survival.Core.Engine;

namespace Survival.Core.Tests;

/// <summary>
/// B56 界面接线（S10／S12／S14 文首 2026-11-17 B53a Q929；02 Q975—Q979 定稿）：测试工程不引用网页壳，这里读组件源码确认饮水窗、
/// 同行饮水选量、水壶数量窗、丢水不足一份原因与说明句接上了；规则本身由 B56WaterTests 测。
/// </summary>
public class B56UiWiringTests
{
    // 2026-11-19 起：源码断言用 SourceText.Web（去注释），文案期望从策划合同取（SourceText.Contract）
    private static string Web(string relative) => SourceText.Web(relative);

    /// <summary>B56 次要原批 Q975—Q979 定稿合同（S14 2026-11-18 节）。</summary>
    private static Newtonsoft.Json.Linq.JObject Q975Q979() => SourceText.Contract("b56-q975-q979-water-ui-copy.json");

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
    public void Entries_CardPotCanteenPause_AllUseDrinkBox_FinalCopyQ975Q976()
    {
        var card = Web("Components/CardActions.razor");
        Assert.That(card, Does.Contain("e.Item(m.ItemId).Kind == \"water\"").And.Contain("<DrinkBox InstanceId=\"@m.UseId.Value\" />"), "水卡与锅水");
        Assert.That(card, Does.Contain("<DrinkBox InstanceId=\"@cid\" />"), "水壶");
        Assert.That(card, Does.Not.Contain("CanteenSipText").And.Not.Contain("按500毫升装倒"), "旧一口 250／按 500 装倒撤下");
        var pause = Web("Components/PauseLayer.razor");
        var c = Q975Q979();
        Assert.That((string)c["drinkCopy"]!["pausedClean"]!, Is.EqualTo("饮用净水·5分钟"), "合同净水即「饮用{水种}·5分钟」");
        Assert.That(pause, Does.Contain("EntryText=\"@($\"饮用{cfg.Name}·5分钟\")\"").And.Contain($"EntryText=\"{c["drinkCopy"]!["pausedDirty"]}\""), "Q975 A 定稿");
        // 02 Q976 B 定稿（S14 2026-11-18 B56 次要原批）：各处逐处短稿，替换开发方 A 暂用的整段 Q929 新稿
        var ctx = c["contextualCopy"]!;
        Assert.That(pause, Does.Contain((string)ctx["dirtyWaterRisk"]!), "Q976 B 暂停层脏水短稿");
        Assert.That(card, Does.Contain($"CanteenRuleNote = \"{ctx["containerUnitSentence"]}\"").And.Contain("只装同种水；@CanteenRuleNote"), "Q976 B 水壶装倒单位句");
        Assert.That(FeedPanelCopy.HelpText[3], Does.Contain((string)ctx["companionDrinkSentence"]!), "Q976 B 同伴饮水说明");
        var canteen = Survival.Core.Config.ConfigLoader.FromJson(File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "config.v0.6.json"))).Items.Single(i => i.Id == "ration_canteen");
        Assert.That(canteen.Desc, Does.EndWith("只装同一种水；" + ctx["containerUnitSentence"]).And.Contain("容量2000ml"), "Q976 B 水壶物品说明单位句，容量保留");
        Assert.That(pause, Does.Not.Contain("仍然饮用1份脏水").And.Not.Contain("应急补水30"));
    }

    // 原 CanteenFillPour_QuantityWindow_Q978A（读 CardActions 源码比字符串）BM13 起迁到 bUnit：
    // Survival.Web.Tests.BM13CanteenFillUiTests（02 Q978 A 标题、默认 1 份、「确认{数量}」、所选份数一次装／倒；2026-11-19 防假绿第 8 条）

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
    public void DiscardPanel_QuantityBlockedReason_MlBesideInput_Q979B()
    {
        // 02 Q979 B 定稿：删子区常驻原因行；置灰但可聚焦（aria-disabled），悬停 title、读屏 aria-describedby、点按／回车由会话写进原因行
        var panel = Web("Components/DiscardPanel.razor");
        Assert.That(panel, Does.Not.Contain("data-pt=\"discard-qty-why\"").And.Not.Contain("@DiscardCopy.ChooseQuantity：@qb"), "无常驻重复原因行");
        Assert.That(panel, Does.Contain("aria-disabled=\"@(v.QuantityBlocked != null ? \"true\" : null)\"")
            .And.Contain("aria-describedby=\"@(v.QuantityBlocked != null ? \"discard-qty-why\" : null)\"")
            .And.Contain("title=\"@v.QuantityBlocked\""), "键盘、触屏、读屏、悬停都能看到禁用原因");
        Assert.That(panel, Does.Contain("disabled=\"@(v.MaxUnits < 1 && v.QuantityBlocked == null)\""), "水分不足时不用原生 disabled（否则不可聚焦）");
        var session = Web("Services/GameSession.Discard.cs");
        Assert.That(session, Does.Contain("if (DiscardViewOf(d.Target).QuantityBlocked is string why) { d.Error = why; Notify(); return; }"), "点按只看原因、不进数量层");
        Assert.That(panel, Does.Contain("DrinkCopy.AmountText(qn * v.UnitMl)"), "输入旁「{毫升}毫升」");
    }

    [Test]
    public void HelpAndGuide_NoOldHalfLitreSentence()
    {
        // 02 Q976 B 定稿：烧水行「100毫升起，时长不变。」；系统说明完整饮水规则保留（饮食与饮水条）
        var boil = (string)Q975Q979()["contextualCopy"]!["boilSentence"]!;
        var help = Web("Services/HelpCatalog.cs");
        Assert.That(help, Does.Not.Contain("0.5升或1升").And.Contain("变成{output}，" + boil + "\"")
            .And.Contain("\"水量每份100毫升。饮水可选数量，一次5分钟。水煮料理每次耗水1000毫升；烧水可少量进行。\""));
        var guide = Web("Services/ProcessGuide.cs");
        Assert.That(guide, Does.Not.Contain("0.5升或1升").And.Not.Contain("每份500毫升").And.Contain("每份100毫升").And.Contain("100毫升起，时长不变"));
    }
}
