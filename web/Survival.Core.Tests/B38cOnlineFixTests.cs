using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 批次 B38c（R121—R123）：答复 Q299—Q301，按 N02 附录「2026-11-09 Q299–Q301 · 线上体验修订」逐字落实。
/// 验收：Q299-A01 换物页地面样本按地面卡合并；Q300-A01 换药无收益只列在走的冷却；Q301-A01 施济食物选项用全名“烤熟块根1份”。
/// </summary>
public class B38cOnlineFixTests : TestBase
{
    private void Ok(ActionResult r) => Assert.That(r.Ok, Is.True, r.Reason);

    private const string Sub = "active_substance_white_unidentified";

    private GameState Registered(string loc)
    {
        var s = New();
        s.Progress.Flags.UnionWith(new[] { "registration_authorized", "hub_registered" });
        s.GameMinute = At(2, 9, 0);
        s.SpawnNextCheck = int.MaxValue;
        s.PatrolNextCheck = int.MaxValue;
        E.DebugMapTeleport(s, loc);
        ReadDialogues(s);
        return s;
    }

    private static string Title(string serviceId) => Cfg.City.Services.Single(x => x.Id == serviceId).Title;

    // ───────────── Q299 鉴定所地面样本 ─────────────

    /// <summary>
    /// Q299-A01：同堆两份不同来源的普通样本在换物页显示一行“所选1份（本堆共2份）”，完成一次换物后剩一份；首份预留时只绑定合法的另一份；
    /// 都不可交易时绑定首项并显示其实际原因；教学样本及不同地面卡仍分开；另两页（Samples）仍是原实例行。
    /// </summary>
    [Test]
    public void Q299_A01_Trade_GroundRows_MergedByCard()
    {
        var s = New();
        E.DebugAppraisal(s, "go");
        ReadDialogues(s);
        E.DebugAppraisal(s, "ordinary");
        E.DebugAppraisal(s, "tutorial");
        E.DebugAppraisal(s, "fiber");
        var ords = s.Items.Where(i => i.ItemId == Sub && i.Owner == s.Ground && GameEngine.IsOrdinarySample(i)).OrderBy(i => i.InstanceId).ToList();
        Assert.That(ords, Has.Count.EqualTo(2));
        Assert.That(ords[0].Source!.GrantEventId, Is.Not.EqualTo(ords[1].Source!.GrantEventId), "两份来源不同、是两个实例");
        Assert.That(E.CardKey(ords[0]), Is.EqualTo(E.CardKey(ords[1])), "地面上同一张卡");

        var v = E.AppraisalPreview(s);
        // 另两页：原实例行
        Assert.That(v.Samples, Has.Count.EqualTo(3));
        Assert.That(v.Samples.Where(x => x.Ordinary).Select(x => x.InstanceId), Is.EqualTo(ords.Select(i => i.InstanceId)));
        Assert.That(v.Samples.Select(x => x.PickText), Is.All.EqualTo("所选1份"));
        // 换物页：普通一行、教学一行
        Assert.That(v.TradeSamples, Has.Count.EqualTo(2));
        var row = v.TradeSamples.Single(x => x.Ordinary);
        Assert.That(row.PickText, Is.EqualTo("所选1份（本堆共2份）"));
        Assert.That(row.Count, Is.EqualTo(2));
        Assert.That(row.MemberIds, Is.EqualTo(ords.Select(i => i.InstanceId)));
        Assert.That(row.InstanceId, Is.EqualTo(ords[0].InstanceId), "按样本列表顺序第一份可交易的");
        Assert.That(row.TradeBlocked, Is.Null);
        Assert.That(row.SourceText, Is.EqualTo("普通丧尸回收"));
        var tut = v.TradeSamples.Single(x => x.Tutorial);
        Assert.That(tut.PickText, Is.EqualTo("所选1份"));
        Assert.That(tut.TradeBlocked, Is.EqualTo("教学样本不可交易"));
        Assert.That(tut.RowKey, Is.Not.EqualTo(row.RowKey));

        // 首份被预留（详细鉴定进行中）：只绑定合法的另一份，堆总量仍按地面卡真实总量
        Ok(E.StartAppraise(s, ords[0].InstanceId));
        row = E.AppraisalPreview(s).TradeSamples.Single(x => x.Ordinary);
        Assert.That(row.InstanceId, Is.EqualTo(ords[1].InstanceId));
        Assert.That(row.Reserved, Is.False);
        Assert.That(row.TradeBlocked, Is.Null);
        Assert.That(row.PickText, Is.EqualTo("所选1份（本堆共2份）"));
        Ok(E.Cancel(s));
        ReadDialogues(s);
        Assert.That(E.AppraisalPreview(s).TradeSamples.Single(x => x.Ordinary).InstanceId, Is.EqualTo(ords[0].InstanceId), "取消后回到首份");

        // 堆内都不可交易：绑定原顺序首项，显示其实际阻断原因
        foreach (var i in ords) i.ReservedBy = 9999;
        row = E.AppraisalPreview(s).TradeSamples.Single(x => x.Ordinary);
        Assert.That(row.InstanceId, Is.EqualTo(ords[0].InstanceId));
        Assert.That(row.Reserved, Is.True);
        Assert.That(row.TradeBlocked, Is.EqualTo("这份样本已被预留"));
        foreach (var i in ords) i.ReservedBy = null;

        // 提交时重验绑定实例：预览后绑定实例变了，原请求不换样、给现行阻断原因
        row = E.AppraisalPreview(s).TradeSamples.Single(x => x.Ordinary);
        ords[0].ReservedBy = 9999;
        var r = E.StartAppraisalTrade(s, row.InstanceId, "white_for_cloth");
        Assert.That(r.Ok, Is.False);
        Assert.That(r.Reason, Is.EqualTo("这份样本已被预留"));
        Assert.That(s.Main, Is.Null, "不换样、不开工");
        ords[0].ReservedBy = null;

        // 完成一次换物：只消耗绑定的一份，剩一份
        row = E.AppraisalPreview(s).TradeSamples.Single(x => x.Ordinary);
        Ok(E.StartAppraisalTrade(s, row.InstanceId, "white_for_cloth"));
        Run(s);
        ReadDialogues(s);
        Assert.That(s.Items.Where(i => i.ItemId == Sub && i.Owner == s.Ground && GameEngine.IsOrdinarySample(i)).Sum(i => i.Count), Is.EqualTo(1));
        Assert.That(FindItem(s, ords[0].InstanceId), Is.Null, "交出的是绑定的首份");
        v = E.AppraisalPreview(s);
        row = v.TradeSamples.Single(x => x.Ordinary);
        Assert.That(row.PickText, Is.EqualTo("所选1份"));
        Assert.That(row.InstanceId, Is.EqualTo(ords[1].InstanceId));
        Assert.That(v.TradeSamples.Count(x => x.Tutorial), Is.EqualTo(1), "教学样本仍单独一行");
    }

    /// <summary>Q299：随身样本在换物页保留现有实例行（不同来源的两份随身样本仍是两行）。</summary>
    [Test]
    public void Q299_Carried_KeepsInstanceRows()
    {
        var s = New();
        E.DebugAppraisal(s, "go");
        ReadDialogues(s);
        var a = E.CreateItems(s, Sub, 1, ItemInstance.Carried, false, new ItemSource { GrantEventId = "c-a" }).Single();
        var b = E.CreateItems(s, Sub, 1, ItemInstance.Carried, false, new ItemSource { GrantEventId = "c-b" }).Single();
        var v = E.AppraisalPreview(s);
        Assert.That(v.TradeSamples.Select(x => x.InstanceId), Is.EqualTo(new[] { a.InstanceId, b.InstanceId }));
        Assert.That(v.TradeSamples.Select(x => x.PickText), Is.All.EqualTo("所选1份"));
        Assert.That(v.TradeSamples.Select(x => x.RowKey), Is.EqualTo(v.Samples.Select(x => x.RowKey)));
    }

    private static ItemInstance? FindItem(GameState s, int id) => s.Items.FirstOrDefault(i => i.InstanceId == id);

    // ───────────── Q300 换药无收益的冷却提示 ─────────────

    private GameState FullHealthAtInfirmary()
    {
        var s = Registered("h04_infirmary");
        s.Stats.Health = s.Stats.HealthMax;
        s.Body.BleedTriggers = 0;
        s.BandageReadyAt = 0;
        s.Body.BandageVirusReadyAt = 0;
        return s;
    }

    private string? DressingBlocked(GameState s) => E.CityServicePreview(s, "clinic_dressing")!.Targets.Single().Blocked;

    /// <summary>Q300-A01：健康已满、无出血、仍感染且只有病毒减量冷却；仅回血冷却；两者均有；两者均无——四种输出。</summary>
    [Test]
    public void Q300_A01_Dressing_NoGain_RunningCooldownsOnly()
    {
        // 1) 只有病毒减量冷却（仍感染）：无收益 + 病毒减量剩余时间两句
        var s = FullHealthAtInfirmary();
        s.Body.Infected = true;
        s.Body.VirusLoad = 30;
        s.Body.BandageVirusReadyAt = s.GameMinute + 360;
        Assert.That(DressingBlocked(s), Is.EqualTo($"当前换药没有收益。病毒减量冷却还需{Clock.Duration(360)}。"));

        // 2) 仅回血冷却（未感染；病毒减量冷却即使有时间也不适用、不列）
        s = FullHealthAtInfirmary();
        s.BandageReadyAt = s.GameMinute + 60;
        s.Body.BandageVirusReadyAt = s.GameMinute + 300;
        Assert.That(DressingBlocked(s), Is.EqualTo($"当前换药没有收益。回血冷却还需{Clock.Duration(60)}。"));

        // 3) 两者均有（仍感染）：按回血、病毒减量顺序
        s = FullHealthAtInfirmary();
        s.Body.Infected = true;
        s.Body.VirusLoad = 30;
        s.BandageReadyAt = s.GameMinute + 60;
        s.Body.BandageVirusReadyAt = s.GameMinute + 360;
        Assert.That(DressingBlocked(s), Is.EqualTo($"当前换药没有收益。回血冷却还需{Clock.Duration(60)}。病毒减量冷却还需{Clock.Duration(360)}。"));

        // 4) 两者均无：沿用原实际原因（未感染；仍感染但病毒已到最低值）
        s = FullHealthAtInfirmary();
        Assert.That(DressingBlocked(s), Is.EqualTo("健康已满且没有出血，包扎没有效果"));
        s.Body.Infected = true;
        s.Body.VirusLoad = 0;
        Assert.That(DressingBlocked(s), Is.EqualTo("健康已满、没有出血，病毒减量也不可用，包扎没有效果"));
    }

    /// <summary>Q300：事务重验的无收益分支同句——开工时有收益，完成前变成无收益且冷却在走，失败外层里只列在走的冷却。</summary>
    [Test]
    public void Q300_Dressing_Revalidate_SameSentence()
    {
        var s = FullHealthAtInfirmary();
        s.Stats.Health = 5;
        E.DebugGive(s, "bandage", 1);
        E.DebugGive(s, "cloth", 1);
        Ok(E.StartCityService(s, "clinic_dressing", GameState.PlayerId));
        s.Stats.Health = s.Stats.HealthMax;
        s.Body.Infected = true;
        s.Body.VirusLoad = 30;
        s.BandageReadyAt = s.GameMinute + 600;
        s.Body.BandageVirusReadyAt = s.GameMinute + 900;
        var logs = s.Log.Count;
        Run(s);
        var text = s.Log.Skip(logs).Single(l => l.Kind == nameof(GameEventKind.ActionCompleted)).Text;
        Assert.That(text, Does.Match($"^{Title("clinic_dressing")}未完成。当前换药没有收益。回血冷却还需[^。]+。病毒减量冷却还需[^。]+。材料和额度均未扣除。$"));
        Assert.That(text, Does.Not.Contain("已就绪"));
    }

    // ───────────── Q301 施济选项 ─────────────

    /// <summary>Q301-A01：施济处食物选项与实际领取物品都使用全名“烤熟块根”；完成按原模板显示产物与真实落点。</summary>
    [Test]
    public void Q301_A01_Relief_FoodChoice_FullName()
    {
        Assert.That(E.ItemName("cooked_root"), Is.EqualTo("烤熟块根"));
        var s = Registered("c04_alms");
        var v = E.CityServicePreview(s, "church_relief_ration")!;
        Assert.That(v.Choices.Select(c => c.Label), Is.EqualTo(new[] { "烤熟块根1份（饱食低于40）", "净水1份（水分低于40）" }));
        s.Player.Stats.Hydration = 15;
        Assert.That(E.StartCityService(s, "church_relief_ration", GameState.PlayerId, "relief_food").Reason,
            Is.EqualTo("你当前不符合「烤熟块根1份（饱食低于40）」的领取条件。"));
        s.Player.Stats.Hydration = 80;
        s.Player.Stats.Satiety = 15;
        Ok(E.StartCityService(s, "church_relief_ration", GameState.PlayerId, "relief_food"));
        var logs = s.Log.Count;
        Run(s);
        var done = s.Log.Skip(logs).Where(l => l.Kind == nameof(GameEventKind.ActionCompleted)).Select(l => l.Text).ToList();
        Assert.That(done, Is.EqualTo(new[] { $"烤熟块根×1已放在{E.LocName("c04_alms")}地面。可自行领取。" }));
        Assert.That(Count(s, "cooked_root", s.Ground), Is.GreaterThanOrEqualTo(1));
    }
}
