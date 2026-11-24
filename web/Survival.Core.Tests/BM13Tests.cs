using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// BM13（次要攒批 R317）：02 Q1010 A 饮水结果写实际补水、02 Q1011 A 装水窗标来源。
/// 合同 assets/20261119/b56-q1010-q1011-drink-delta-fill-source.json（验收 B56-Q1010-01、B56-Q1011-01、B56-Q1011-02）。
/// 期望值都写字面量，出处见各断言旁注释；网页壳的装水窗显示另由 Survival.Web.Tests.BM13CanteenFillUiTests 测。
/// </summary>
public class BM13Tests : TestBase
{
    private const string Lin = "npc_lin_zhao";

    protected override bool NoEnemyApproach => true;

    private void Ok(ActionResult r) => Assert.That(r.Ok, Is.True, r.Reason);

    /// <summary>营地新局，清掉开局水（避免与新造的水堆合并或抢先被取）。</summary>
    private GameState Fresh()
    {
        var s = New();
        s.SpawnNextCheck = int.MaxValue;
        s.PatrolNextCheck = int.MaxValue;
        s.Items.RemoveAll(i => i.ItemId is "clean_water" or "dirty_water");
        return s;
    }

    private GameState WithLin()
    {
        var s = Fresh();
        s.Progress.Flags.Add("hub_registered");
        s.Progress.Flags.Add("church_route_record");
        s.Progress.Flags.Add("academy_safety_record");
        E.DebugRecruit(s, Lin);
        return s;
    }

    // ───────────── Q1010 A：自饮日志与同伴结果句都写实际补水 ─────────────

    [Test]
    public void Q1010_01_SelfDrink_Hydration95_300ml_LogsActualPlus5_NotNatural5MinLoss()
    {
        // 02 Q1010 A 答复例：结算前水分 95，300 毫升名义补 18、上限 100 → 结果写「水分+5」；不把 5 分钟自然失水算进来
        var s = Fresh();
        s.Stats.Hydration = 95;
        var w = E.CreateItems(s, "clean_water", 3, ItemInstance.Carried).Single();
        Start(s, ActionRequest.UseInstance(w.InstanceId, 3));
        // 日志格式沿 02 Q975 A 定稿「饮用{水种}{毫升}毫升」＋「：{变化}。」
        Assert.That(s.Log.Last().Text, Is.EqualTo("饮用净水300毫升：水分+5。"));
        Assert.That(s.Stats.Hydration, Is.EqualTo(100.0), "补水在开始时一次结清并按上限截断");
        Run(s);
        Assert.That(s.Stats.Hydration, Is.LessThan(100.0), "5 分钟里确有自然失水（辅助：说明下面那条日志没把它算进去）");
        Assert.That(s.Log.Count(l => l.Text == "饮用净水300毫升：水分+5。"), Is.EqualTo(1), "日志仍是补水前后之差 +5");
    }

    [Test]
    public void Q1010_02_SelfDrink_NotCapped_FullGain_And_FractionalFormat()
    {
        // 配对：不受上限截断时实际差＝名义 6N（S10／S12 文首 B53a Q929 每份补水 6）
        var s = Fresh();
        s.Stats.Hydration = 20;
        var w = E.CreateItems(s, "clean_water", 5, ItemInstance.Carried).Single();
        Start(s, ActionRequest.UseInstance(w.InstanceId, 3));
        Assert.That(s.Log.Last().Text, Is.EqualTo("饮用净水300毫升：水分+18。"));
        Run(s);
        // Q1010 A「沿同伴结果句的数值格式」：同伴句按 Fmt 两位小数去尾零，95.5→100 写「+4.5」
        s.Stats.Hydration = 95.5;
        Start(s, ActionRequest.UseInstance(w.InstanceId, 1));
        Assert.That(s.Log.Last().Text, Is.EqualTo("饮用净水100毫升：水分+4.5。"));
    }

    [Test]
    public void Q1010_03_SelfDrink_AtCap_ZeroRule_DirtyKeepsDiarrhea()
    {
        // Q1010 A「沿同伴结果句的零值规则」：Q268 真正为 0 写「{属性名}无变化」
        var s = Fresh();
        s.Stats.Hydration = 100;
        var w = E.CreateItems(s, "clean_water", 2, ItemInstance.Carried).Single();
        Start(s, ActionRequest.UseInstance(w.InstanceId, 1));
        Assert.That(s.Log.Last().Text, Is.EqualTo("饮用净水100毫升：水分无变化。"));
        Run(s);
        // 脏水：只改水分增量，腹泻段沿原「出现腹泻（12小时）」（Q1010 A「受限部分只改结果里的水分增量」）
        s.Stats.Hydration = 100;
        var d = E.CreateItems(s, "dirty_water", 1, ItemInstance.Carried).Single();
        Start(s, ActionRequest.UseInstance(d.InstanceId, 1));
        Assert.That(s.Log.Last().Text, Is.EqualTo("饮用脏水100毫升：水分无变化，出现腹泻（12小时）。"));
    }

    [Test]
    public void Q1010_04_SelfDrink_Canteen_ActualGain_KeepsRemainder()
    {
        // 水壶自饮：增量改实际值，「壶里剩…」与真实毫升照旧（Q975 定稿字段不变）
        var s = Fresh();
        s.Stats.Hydration = 95;
        var c = E.CreateItems(s, "ration_canteen", 1, ItemInstance.Carried).Single();
        c.LiquidId = "clean_water";
        c.LiquidMl = 1000;
        Start(s, ActionRequest.UseInstance(c.InstanceId, 3));
        Assert.That(s.Log.Last().Text, Is.EqualTo("饮用净水300毫升：水分+5，壶里剩净水 700/2000毫升。"));
    }

    [Test]
    public void Q1010_05_Companion_Hydration95_300ml_ActualPlus5_MlAndSourceUnchanged()
    {
        // 合同 B56-Q1010-01：同伴喝 300 毫升，结果报实际 +5；毫升与同伴来源照实（S12 结果句「{目标}饮用了{水种}{所选毫升}毫升（取自{出物人}的随身物品）：{变化}。」）
        var s = WithLin();
        s.Actors[Lin].Stats.Hydration = 95;
        var w = E.CreateItems(s, "clean_water", 3, ItemInstance.Carried).Single();
        Ok(E.Feed(s, new FeedRequest { TargetActorId = Lin, SourceActorId = GameState.PlayerId, InstanceId = w.InstanceId, WaterUnits = 3 }));
        Assert.That(s.Log.Last().Text, Is.EqualTo("林昭饮用了净水300毫升（取自你的随身物品）：水分+5。"));
        Assert.That(s.Actors[Lin].Stats.Hydration, Is.EqualTo(100.0));
    }

    // ───────────── Q1011 A：装水窗来源行与上限 ─────────────

    [Test]
    public void Q1011_00_FillSourceCopy_FromContract()
    {
        // 来源行模板直接对合同 fillSource.label（02 Q1011 A 答复原句），不另抄
        var c = SourceText.Contract("b56-q1010-q1011-drink-delta-fill-source.json");
        Assert.That(CanteenCopy.FillSource, Is.EqualTo((string)c["fillSource"]!["label"]!));
    }

    [Test]
    public void Q1011_01_CarriedFirst_SourceShowsStackCount_MaxByStackAndRoom()
    {
        // 合同 B56-Q1011-01：壶可装 20 份、随身净水 5 份、地面 50 份 → 来源「随身净水 5份」、上限 5；
        // 壶只剩 2 份空间时来源仍写 5 份、上限 2
        var s = Fresh();
        var can = E.CreateItems(s, "ration_canteen", 1, ItemInstance.Carried).Single();
        var carried = E.CreateItems(s, "clean_water", 5, ItemInstance.Carried).Single();
        var ground = E.CreateItems(s, "clean_water", 50, s.Ground).Single();
        var src = E.CanteenFillSource(s, "clean_water");
        Assert.That(src, Is.SameAs(carried), "沿原取堆：随身优先");
        Assert.That(E.FillSourceText(src!), Is.EqualTo("来源：随身净水 5份"));
        Assert.That(E.MaxFillCanteen(s, src!, can, out var why), Is.EqualTo(5), why);

        can.LiquidId = "clean_water";
        can.LiquidMl = 1800;
        Assert.That(E.FillSourceText(E.CanteenFillSource(s, "clean_water")!), Is.EqualTo("来源：随身净水 5份"), "来源份数是这堆份数，不是上限");
        Assert.That(E.MaxFillCanteen(s, carried, can, out why), Is.EqualTo(2), why);
        Ok(E.DoInstant(s, InstantRequest.FillCanteen(carried.InstanceId, can.InstanceId, 2)));
        Assert.That((Count(s, "clean_water", ItemInstance.Carried), Count(s, "clean_water", s.Ground), can.LiquidMl), Is.EqualTo((3, 50, 2000)),
            "一次只从所选那堆取，地面不动");
        Assert.That(ground.Count, Is.EqualTo(50));
    }

    [Test]
    public void Q1011_02_GroundOnly_SourceShowsGround_ConfirmDeductsOnlyThatStack()
    {
        // 合同 B56-Q1011-02：仅有地面合法水堆时标地面来源，确认只扣所选水堆
        var s = Fresh();
        var can = E.CreateItems(s, "ration_canteen", 1, ItemInstance.Carried).Single();
        var ground = E.CreateItems(s, "clean_water", 50, s.Ground).Single();
        var dirty = E.CreateItems(s, "dirty_water", 4, s.Ground).Single();
        var src = E.CanteenFillSource(s, "clean_water");
        Assert.That(src, Is.SameAs(ground));
        Assert.That(E.FillSourceText(src!), Is.EqualTo("来源：地面净水 50份"));
        Assert.That(E.FillSourceText(E.CanteenFillSource(s, "dirty_water")!), Is.EqualTo("来源：地面脏水 4份"));
        Assert.That(E.MaxFillCanteen(s, ground, can, out var why), Is.EqualTo(20), why + "（壶 2000 毫升＝20 份空余）");
        Ok(E.DoInstant(s, InstantRequest.FillCanteen(ground.InstanceId, can.InstanceId, 3)));
        Assert.That((ground.Count, dirty.Count, can.LiquidId, can.LiquidMl), Is.EqualTo((47, 4, "clean_water", 300)));
        // 原混水处理照旧：壶里已有净水时脏水不能装，原因句不变（B16）
        Assert.That(E.MaxFillCanteen(s, dirty, can, out why), Is.EqualTo(0));
        Assert.That(why, Is.EqualTo("水壶里是净水，不能再装脏水（不混装）"));
    }

    [Test]
    public void Q1011_03_ReservedCarriedStackSkipped_FallsBackToGround()
    {
        // 沿原取堆：被预留的那堆不取，退到当前地面（「一次只从所选真实合法水堆取」）
        var s = Fresh();
        var carried = E.CreateItems(s, "clean_water", 5, ItemInstance.Carried).Single();
        var ground = E.CreateItems(s, "clean_water", 7, s.Ground).Single();
        Assert.That(E.CanteenFillSource(s, "clean_water"), Is.SameAs(carried), "配对：未预留时取随身");
        carried.ReservedBy = 999;
        Assert.That(E.CanteenFillSource(s, "clean_water"), Is.SameAs(ground));
        Assert.That(E.FillSourceText(ground), Is.EqualTo("来源：地面净水 7份"));
    }
}
