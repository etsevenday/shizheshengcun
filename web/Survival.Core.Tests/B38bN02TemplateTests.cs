using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 批次 B38b（R102b、R120）：N02 附录 §2—§4 组件与引擎模板句，按 2026-11-09「Q283–Q291 · B38b 当前实施定稿」逐字落实。
/// 验收：A04 B38b 的 Q283-A01—Q291-A01（引擎可判的部分；界面排列与按钮字见网页壳）。
/// </summary>
public class B38bN02TemplateTests : TestBase
{
    private void Ok(ActionResult r) => Assert.That(r.Ok, Is.True, r.Reason);

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

    // ───────────── §2 城市服务（Q285、Q286、Q291） ─────────────

    /// <summary>Q286-A01：CityCommonBlocked 每种阻断的完整句（地点、对白、行动占用、死亡），原分支顺序不变。</summary>
    [Test]
    public void Q286_A01_CommonBlocked_Sentences()
    {
        var s = Registered("h04_infirmary");
        Assert.That(E.CityServicePreview(s, "church_relief_ration")!.Blocked, Is.EqualTo($"到{E.LocName("c04_alms")}后才能办理。"));
        E.DebugGive(s, "bandage", 1);
        E.DebugGive(s, "cloth", 1);
        s.Stats.Health = 10;
        Ok(E.StartCityService(s, "clinic_dressing", GameState.PlayerId));
        Assert.That(E.CityServicePreview(s, "clinic_consult")!.Blocked, Is.EqualTo("当前行动还没结束。"));
        Assert.That(GameEngine.CityBusyText, Is.EqualTo("当前行动还没结束。"), "CityServiceOverlay 两处、AppraisalPanel 一处同句");
        Ok(E.Cancel(s));
        E.TalkNpc(s, "npc_chen_zhi");
        if (E.DialogueLocked(s))
        {
            Assert.That(E.CityServicePreview(s, "clinic_consult")!.Blocked, Is.EqualTo("请先读完当前对话。"));
            ReadDialogues(s);
        }
        s.Progress.Dead = true;
        Assert.That(E.CityServicePreview(s, "clinic_consult")!.Blocked, Is.EqualTo("本局已结束，无法办理。"));
        Assert.That(GameEngine.CityNotOffered, Is.EqualTo("这里不办理这项业务。"));
    }

    /// <summary>Q286-A01、Q291-8：施济预览、对象不符、选项不符、成功、预留计数、额度用完与完成重验失败。</summary>
    [Test]
    public void Q286_A01_Relief_Preview_Start_Complete()
    {
        var s = Registered("c04_alms");
        var v = E.CityServicePreview(s, "church_relief_ration")!;
        Assert.That(v.EffectText, Is.EqualTo("符合条件时选一项。办完在这里的地面领取，之后自行食用或饮用。"));
        Assert.That(v.Lines, Is.EqualTo(new[] { "本日全队已领0/1份。" }));
        var t = v.Targets.Single();
        Assert.That(t.Blocked, Is.EqualTo("饱食和水分均不低于40，当前没有可领的配给。"), "对象行已单列姓名，只写饱食和水分……");
        Assert.That(t.BlockedLine, Is.EqualTo("你的饱食和水分均不低于40，当前没有可领的配给。"));
        Assert.That(E.StartCityService(s, "church_relief_ration", GameState.PlayerId, "relief_food").Reason, Is.EqualTo("你的饱食和水分均不低于40，当前没有可领的配给。"));

        s.Player.Stats.Satiety = 15;
        Assert.That(E.StartCityService(s, "church_relief_ration", GameState.PlayerId).Reason, Is.EqualTo("先选一项。"));
        Assert.That(E.StartCityService(s, "church_relief_ration", GameState.PlayerId, "relief_water").Reason, Is.EqualTo("你当前不符合「净水1份（水分低于40）」的领取条件。"));
        Ok(E.StartCityService(s, "church_relief_ration", GameState.PlayerId, "relief_food"));
        Assert.That(E.CityServicePreview(s, "church_relief_ration")!.Lines, Is.EqualTo(new[] { "本日全队已领1/1份（含在办预留）。" }));
        var logs = s.Log.Count;
        Run(s);
        var done = s.Log.Skip(logs).Where(l => l.Kind == nameof(GameEventKind.ActionCompleted)).ToList();
        Assert.That(done.Select(l => l.Text), Is.EqualTo(new[] { $"{E.ItemName("cooked_root")}×1已放在{E.LocName("c04_alms")}地面。可自行领取。" }), "成功一条，不外包服务名");
        ReadDialogues(s);
        v = E.CityServicePreview(s, "church_relief_ration")!;
        Assert.That(v.Lines, Is.EqualTo(new[] { "本日全队已领1/1份。" }));
        Assert.That(v.Blocked, Is.EqualTo("第2日的全队救助额度已用完（每日1份）。"));

        // 完成时重验不合格：材料和额度都不扣
        s.GameMinute = At(3, 9, 0);
        s.Player.Stats.Hydration = 15;
        Ok(E.StartCityService(s, "church_relief_ration", GameState.PlayerId, "relief_water"));
        s.Player.Stats.Hydration = 90;
        logs = s.Log.Count;
        Run(s);
        Assert.That(s.Log.Skip(logs).Single(l => l.Kind == nameof(GameEventKind.ActionCompleted)).Text,
            Is.EqualTo($"{Title("church_relief_ration")}未完成。你已不符合「净水1份（水分低于40）」的领取条件。材料和额度均未扣除。"));
        Assert.That(E.CityQuotaUsed(s, "church_relief_ration", 3), Is.Zero);
    }

    /// <summary>Q286-A01、Q291-4：营门预览、授权日志（不写知识数量）、已授权与已登记两种状态句。</summary>
    [Test]
    public void Q286_A01_Gate_Authorize_Registered()
    {
        var s = New();
        s.SpawnNextCheck = int.MaxValue;
        s.PatrolNextCheck = int.MaxValue;
        var gate = Cfg.City.Services.Single(x => x.Id == "hub_registration_existing").LocationId;
        E.DebugMapTeleport(s, gate);
        ReadDialogues(s);
        var v = E.CityServicePreview(s, "hub_registration_existing")!;
        Assert.That(v.EffectText, Is.EqualTo("在营门确认登记，再沿通道到营区广场完成登记。公共配给到广场另领。"));
        Assert.That(v.EffectText, Does.Not.Contain("项"), "Q291-4：预览不写知识数量");
        var logs = s.Log.Count;
        Ok(E.StartCityService(s, "hub_registration_existing"));
        var square = E.LocName(Cfg.City.Registration.SquareLocation);
        var text = s.Log.Skip(logs).Single(l => l.Kind == nameof(GameEventKind.Info)).Text;
        Assert.That(text, Does.StartWith($"营门已授权。沿通道前往{square}完成登记。公共配给到广场另领。"));
        Assert.That(text.Replace($"营门已授权。沿通道前往{square}完成登记。公共配给到广场另领。", ""), Is.Empty.Or.Match("^新发现路线：.+。$"));
        ReadDialogues(s);
        Assert.That(E.CityServicePreview(s, "hub_registration_existing")!.Blocked, Is.EqualTo($"营门已授权。前往{square}完成登记。"));
        s.Progress.Flags.Add(Cfg.City.Registration.RegisteredFact);
        Assert.That(E.CityServicePreview(s, "hub_registration_existing")!.Blocked, Is.EqualTo("营区登记已完成。"));
    }

    /// <summary>Q286-A01：咨询完成一条日志（感染、出血、两种冷却），换药主句、成功句与预留材料不足的失败外层。</summary>
    [Test]
    public void Q286_A01_Consult_And_Dressing()
    {
        var s = Registered("h04_infirmary");
        Assert.That(E.CityServicePreview(s, "clinic_consult")!.EffectText, Is.EqualTo("查看所选对象当前的健康、免疫、病毒、出血和用药冷却。此项只读。"));
        s.Body.Infected = true;
        s.Body.VirusLoad = 30;
        s.Body.BleedTriggers = 2;
        s.BandageReadyAt = s.GameMinute + 90;
        var logs = s.Log.Count;
        Ok(E.StartCityService(s, "clinic_consult", GameState.PlayerId));
        var a = s.Player;
        Assert.That(s.Log.Skip(logs).Single().Text, Is.EqualTo(
            $"你的现有记录：健康{Fmt.N(a.Stats.Health)}/{Fmt.N(a.Stats.HealthMax)}，免疫{Fmt.N(a.Body.Immunity)}，已感染（病毒30.00），正在出血。回血冷却还需{Clock.Duration(90)}。病毒减量冷却已就绪。本次只查看记录。"));

        var d = E.CityServicePreview(s, "clinic_dressing")!;
        Assert.That(d.EffectText, Is.EqualTo("医务室为所选对象换药。可处理的出血、回血与病毒变化见所选对象旁的预估。"));
        E.DebugGive(s, "bandage", 2);
        E.DebugGive(s, "cloth", 2);
        logs = s.Log.Count;
        Ok(E.StartCityService(s, "clinic_dressing", GameState.PlayerId));
        Run(s);
        var ok = s.Log.Skip(logs).Single(l => l.Kind == nameof(GameEventKind.ActionCompleted)).Text;
        Assert.That(ok, Does.StartWith("已为你换药。").And.EndWith($"。消耗{E.ItemName("bandage")}×1、{E.ItemName("cloth")}×1。"));
        Assert.That(ok, Does.Match("病毒[0-9.]+→[0-9.]+（仍感染）"), "病毒前后值");
        Assert.That(ok, Does.Contain("健康恢复冷却中（还需").And.Contain("），本次不恢复健康"), "Q289 冷却行定稿");
        ReadDialogues(s);

        // 预留材料完成前被挪走：失败外层，材料和额度不扣
        s.BandageReadyAt = 0;
        s.Stats.Health = 5;
        Ok(E.StartCityService(s, "clinic_dressing", GameState.PlayerId));
        foreach (var i in s.Items.Where(i => i.ReservedBy == s.Main!.ActionId && i.ItemId == "cloth").ToList()) i.Count = 0;
        logs = s.Log.Count;
        Run(s);
        Assert.That(s.Log.Skip(logs).Single(l => l.Kind == nameof(GameEventKind.ActionCompleted)).Text,
            Is.EqualTo($"{Title("clinic_dressing")}未完成。预留的换药材料已不足。材料和额度均未扣除。"));
    }

    /// <summary>Q283-A01、Q291-3：工坊办理日志直接用定稿句与工位分行，不外包服务名；科学院另加告示行（「」）。</summary>
    [Test]
    public void Q283_A01_Workshop_Log_And_Lines()
    {
        var s = Registered("h03_workshop");
        var v = E.CityServicePreview(s, "hub_workshop_access")!;
        Assert.That(v.EffectText, Is.EqualTo("打开当地制作面板。随身手工配方也可以在这里查看。"));
        Assert.That(v.Lines, Has.Some.StartWith("已开放 "));
        Assert.That(v.Lines, Has.None.Contain("木炭"), "Q291-3：锻炉木炭数量从配方条件显示");
        var logs = s.Log.Count;
        Ok(E.StartCityService(s, "hub_workshop_access"));
        Assert.That(s.Log.Skip(logs).Single().Text, Is.EqualTo(string.Join("\n", new[] { v.EffectText }.Concat(v.Lines))));
        Assert.That(s.Log.Last().Text, Does.Not.StartWith(Title("hub_workshop_access")));

        var r = Registered(Cfg.City.Services.Single(x => x.Id == "academy_workshop_access").LocationId);
        Assert.That(E.CityServicePreview(r, "academy_workshop_access")!.Lines.Last(), Is.EqualTo("观察区通行请读门旁的「观察区公开通道告示」。"));
    }

    /// <summary>Q291-7：FA04 整理路线的主句与已有提交进度时另列的进度句。</summary>
    [Test]
    public void Q291_A01_QuestWork_ProgressLine()
    {
        var s = Registered("h02_square");
        E.DebugClaimRecord(s, "d01_08_unique");
        E.DebugClaimRecord(s, "d02_08_unique");
        ReadDialogues(s);
        var v = E.CityServicePreview(s, "hub_affairs_record")!;
        Assert.That(v.EffectText, Is.EqualTo("整理圣所旧路档册与封存区安全规程。两段完成后，旧城后段的通行按这份记录办理。"));
        Assert.That(v.Lines, Is.Empty, "未提交进度不追加");
        var t0 = s.GameMinute;
        Ok(E.StartCityService(s, "hub_affairs_record"));
        ReadDialogues(s);
        for (var i = 0; i < 20 && s.GameMinute < t0 + 5; i++) E.Advance(s);
        Ok(E.Cancel(s));
        v = E.CityServicePreview(s, "hub_affairs_record")!;
        Assert.That((v.TimeText, v.Lines.Single()), Is.EqualTo(("5分钟", "已完成5分钟，还需5分钟。")));
    }

    // ───────────── §3 探索、地牢和任务册（Q283、Q290、Q291） ─────────────

    /// <summary>Q283-A01：首领核心记录一条日志；首句定稿、知识/容量/通关/首通/经验各以一个句号收尾，段间不插分号。</summary>
    [Test]
    public void Q283_A01_Claim_FirstRecord_OneLog_Sentences()
    {
        var s = Registered("h02_square");
        E.DebugMapTeleport(s, "d01_08");
        ReadDialogues(s);
        var res = Cfg.Resources.Single(r => r.NodeId == "d01_08_unique");
        s.Resources[res.NodeId].Discovered = true;
        E.DebugInstanceFact(s, "d01", "guardianDefeated");
        var notes = E.Preview(s, ActionRequest.Gather(res.NodeId)).Notes;
        Assert.That(notes, Has.Some.EqualTo("领取「圣所旧路档册」后会记入本局记录。这份记录只领一次，不占物资卡。"));
        var logs = s.Log.Count;
        Start(s, ActionRequest.Gather(res.NodeId));
        Run(s);
        var done = s.Log.Skip(logs).Where(l => l.Kind == nameof(GameEventKind.ActionCompleted)).ToList();
        Assert.That(done, Has.Count.EqualTo(1), "一个原结果事件");
        var t = done[0].Text;
        Assert.That(t, Does.StartWith("已领取「圣所旧路档册」，记入本局记录（只领一次，不占物资卡）。"));
        Assert.That(t, Does.Contain("队伍容量升到 C3。").And.Contain("本代已通关。经过72世界小时后，可到").And.Contain("确认重置（最早第"));
        Assert.That(t, Does.EndWith("领取记录不计采集/搜集经验。"));
        Assert.That(t, Does.Not.Contain("。；").And.Not.Contain("。。").And.Not.Contain("）；"), "段间不插分号、不重复句号");
    }

    /// <summary>Q290-A01：75% 探索一条日志，探索度与物资在前，“安全线索：{原文}”完整位于最后，不再写“未发现新线索”。</summary>
    [Test]
    public void Q290_A01_Explore_ClueLast()
    {
        var s = New();
        E.DebugTeleport(s, "w11_orchard");
        string Explore()
        {
            var n = s.Log.Count;
            Start(s, ActionRequest.Explore());
            Run(s);
            var done = s.Log.Skip(n).Where(l => l.Kind == nameof(GameEventKind.ActionCompleted)).ToList();
            Assert.That(done, Has.Count.EqualTo(1));
            return done[0].Text;
        }
        Explore();
        Explore();
        var t = Explore();
        var clue = Cfg.Locations.Single(l => l.Id == "w11_orchard").Clue!;
        Assert.That(t, Does.StartWith("探索度50%→75%；").And.EndWith("。安全线索：" + clue));
        Assert.That(t, Does.Not.Contain("未发现新线索").And.Not.Contain("一条安全线索"));
        Assert.That(t.IndexOf("安全线索", StringComparison.Ordinal), Is.EqualTo(t.Length - clue.Length - "安全线索：".Length), "线索正文不夹在路线拼接中");
    }

    // ───────────── §4 队伍、战斗和医疗（Q284、Q289） ─────────────

    /// <summary>Q289-A01：病毒警戒主句，速率正、零、负三种值，只有正速率追加预计尸变句；主角称“你”。</summary>
    [Test]
    public void Q289_A01_VirusWarning_RateSigns()
    {
        string Warn(double immunity)
        {
            var s = New();
            s.Body.Infected = true;
            s.Body.Immunity = immunity;
            s.Body.VirusLoad = 20.5;
            var n = s.Log.Count;
            typeof(GameEngine).GetMethod("VirusWarnings", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance, new[] { typeof(GameState) })!.Invoke(E, new object[] { s });
            return s.Log.Skip(n).Single(l => l.Kind == nameof(GameEventKind.Alert)).Text;
        }
        var texts = new[] { 0.0, 50.0, 100.0 }.Select(Warn).ToList();
        foreach (var t in texts) Assert.That(t, Does.Match("^你的病毒程度达到20，当前20.50，每小时[+−-]?[0-9.]+。"));
        var pos = texts.Where(t => t.Contains("每小时+")).ToList();
        var nonPos = texts.Where(t => !t.Contains("每小时+")).ToList();
        Assert.That(pos, Is.Not.Empty);
        foreach (var t in pos) Assert.That(t, Does.Match("。若免疫和用药状态维持现状，按当前速率估计约.+后尸变。$"));
        foreach (var t in nonPos) Assert.That(t, Does.Not.Contain("尸变").And.EndWith("。"), "零或负只写主句");
    }

    /// <summary>Q289-A01：药物预览按效果用分号分段；应用结果只记一条日志；冷却行与特效药成功句照定稿。</summary>
    [Test]
    public void Q289_A01_Medicine_Preview_OneLog()
    {
        var s = New();
        s.Body.Infected = true;
        s.Body.VirusLoad = 30;
        s.BandageReadyAt = s.GameMinute + 30;
        var pre = E.MedicineEffectText(s, Cfg.Items.Single(i => i.Id == "bandage").Use!)!;
        Assert.That(pre, Is.EqualTo($"健康恢复冷却中（还需{Clock.Duration(30)}），本次不恢复健康；病毒30.00→{Fmt.N(Math.Max(1, 30 - E.BandageVirusReduction(s)))}（仍感染）"));
        var anti = Cfg.Items.FirstOrDefault(i => i.Use?.IsAntiviral == true);
        if (anti != null)
        {
            E.DebugGive(s, anti.Id, 1);
            var n = s.Log.Count;
            var r = E.Start(s, ActionRequest.UseItem(anti.Id));
            if (r.Ok) Run(s);
            var logs = s.Log.Skip(n).Where(l => l.Text.Contains("病毒特效药已清除感染")).ToList();
            Assert.That(logs, Has.Count.EqualTo(1), "成功日志一条");
        }
    }

    /// <summary>Q288-A01：换物主句只用实际地面名；第 3 行的下一世界午夜。</summary>
    [Test]
    public void Q288_A01_Appraisal_Trade_Lines()
    {
        var s = Registered("h05_appraisal");
        var v = E.AppraisalPreview(s);
        Assert.That(v.OutputGround, Does.EndWith("地面").And.Not.Contain("（"));
        Assert.That(v.NextMidnightText, Is.EqualTo(Clock.DayTime(Clock.Day(s.GameMinute) * Clock.MinutesPerDay)));
    }
}
