using Newtonsoft.Json.Linq;
using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 批次 B22a：策划答复 Q163—Q167、Q169（策划提交 bb6abb2，需求 R162—R167）。
/// 规则出处：更新说明 2026-11-03「Q163-Q170」（固定等待净水经验一节）、assets/20261103/survival-adjudications.json（water、acceptance）、
/// narrative-texts.json questCompletionQ163Q165。验收 PLAN-Q163-01 … PLAN-Q167-01、PLAN-Q169-01、PLAN-Q166-02。
/// </summary>
public class B22aTests : TestBase
{
    private void Ok(ActionResult r) => Assert.That(r.Ok, Is.True, r.Reason);

    private static string Q(string id, string kind) => GameEngine.QuestFact(id, kind);

    private static readonly string[] Registered = { "registration_authorized", "hub_registered" };

    private static JObject Design(string name) => JObject.Parse(File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, name)));

    private GameState At(string loc, params string[] facts)
    {
        var s = New();
        s.Progress.Flags.UnionWith(Registered.Concat(facts));
        E.DebugMapTeleport(s, loc);
        ReadDialogues(s);
        return s;
    }

    private static string WaterKey(GameState s, string kind) => $"{Clock.Day(s.GameMinute)}|{kind}|cooking";

    // ───────────── Q163 / Q164 / Q165 任务接续 ─────────────

    /// <summary>PLAN-Q163-01：先领联络日志、后接 FA05：接取时只追认完成与日志，不补播联络完成四句（正常顺序见 B15aTests.QUEST_Q157_01_Fa05_NormalOrder）。</summary>
    [Test]
    public void PLAN_Q163_01_LogFirst_SilentRetro_NoCompleteScene()
    {
        var s = At("h03_workshop");
        E.DebugClaimRecord(s, "d03_08_unique");
        ReadDialogues(s);
        E.DebugClaimRecord(s, "d04_07_unique");
        ReadDialogues(s);
        Assert.That(s.Progress.Flags, Does.Not.Contain(Q("FA05", "completed")));
        Ok(E.TalkNpc(s, "npc_luo_zheng"));
        ReadDialogues(s);
        Assert.That(s.Progress.Flags, Is.SupersetOf(new[] { Q("FA05", "accepted"), Q("FA05", "completed") }));
        Assert.That(s.Story.DialogueLog.Any(d => d.EventId.StartsWith("FA05_contact_complete")), Is.False, "不补播联络完成四句");
        Assert.That(s.Log.Any(l => l.Text.Contains("主线“联络仍在继续”完成")), Is.True, "日志照写");
    }

    /// <summary>PLAN-Q164-01：CH02 选“回营区备份”完成后，许安离岗时营区回报由机构当班接续员说；仍只一次、0 分钟、不发东西。</summary>
    [Test]
    public void PLAN_Q164_01_HubTurnin_ByDutySpeaker_WhenXuAnAbsent()
    {
        var s = At("h02_square", Q("CH01", "completed"), Q("FA02", "accepted"), "FA02_record_credit");
        E.DebugToggleCityPostAbsent(s);   // 许安离岗
        Assert.That(s.City.Absent, Does.Contain("npc_xu_an"));
        E.DebugMapTeleport(s, "c05_admonition");
        ReadDialogues(s);
        Ok(E.TalkNpc(s, "npc_ji_mu"));
        ReadDialogues(s);
        Ok(E.StartCityService(s, "church_public_mediation", null, "CH02_backup_hub"));
        Run(s);
        ReadDialogues(s);
        Assert.That(s.Progress.Flags, Does.Contain(Q("CH02", "completed")));
        var before = (s.GameMinute, s.Items.Sum(i => i.Count), s.Skills.Values.Sum(k => k.Xp));
        E.DebugMapTeleport(s, "h02_square");
        var sessions = s.Story.DialogueLog.Where(d => d.EventId.StartsWith("CH02_hub_backup_turnin")).ToList();
        Assert.That(sessions, Has.Count.EqualTo(1));
        ReadDialogues(s);
        var lines = sessions[0].Lines;
        Assert.That(lines.Single(l => l.LineId == "NTL_0350").Speaker, Is.EqualTo("机构当班接续员"), "许安的句子由当班接续员说");
        Assert.That(lines.Any(l => l.Speaker == "许安"), Is.False);
        Assert.That((s.GameMinute, s.Items.Sum(i => i.Count), s.Skills.Values.Sum(k => k.Xp)), Is.EqualTo(before), "0 时间 0 奖励");
        E.DebugMapTeleport(s, "h03_workshop");
        ReadDialogues(s);
        E.DebugMapTeleport(s, "h02_square");
        Assert.That(s.Story.DialogueLog.Count(d => d.EventId.StartsWith("CH02_hub_backup_turnin")), Is.EqualTo(1), "只播一次");
    }

    /// <summary>PLAN-Q165-01：CH01 进行中“下一步”逐字为定稿句（narrative-texts ch01ActiveStep），接取与工作条件不变。</summary>
    [Test, Category("DesignSync")]
    public void PLAN_Q165_01_Ch01ActiveStep_Verbatim()
    {
        var want = (string)Design("design.narrative-texts.json")["questCompletionQ163Q165"]!["ch01ActiveStep"]!;
        Assert.That(want, Is.EqualTo("在施济处用圣所旧路档册核对物资账（10分钟）。"));
        Assert.That(Cfg.Quests.List.Single(q => q.Id == "CH01").StepActive, Is.EqualTo(want));
        var s = At("c04_alms", Q("FA02", "accepted"), "FA02_record_credit", Q("CH01", "accepted"));
        var v = E.QuestViewOf(s, "CH01")!;
        Assert.That((v.Status, v.Step), Is.EqualTo(("active", want)));
        Assert.That(E.QuestBook(s).Single(x => x.Id == "CH01").Step, Is.EqualTo(want), "任务册同一句");
        Assert.That((v.WorkTotal, v.WorkLabel, v.WorkBlocked), Is.EqualTo((10, "核对物资账（10分钟）", (string?)null)), "工作照旧");
    }

    // ───────────── Q166 固定等待净水经验 ─────────────

    /// <summary>营地、学会全部知识；清掉水与净水片后放 4 份脏水与 n 片净水片。</summary>
    private GameState WaterCamp(int tablets = 1)
    {
        var s = New();
        E.DebugLearnAllKnowledge(s);
        s.Items.RemoveAll(i => i.ItemId is "dirty_water" or "clean_water" or "purification_tablet");
        E.CreateItems(s, "dirty_water", 4, s.CurrentLocation);
        E.CreateItems(s, "purification_tablet", tablets, s.CurrentLocation);
        return s;
    }

    [Test, Category("DesignSync")]
    public void PLAN_Q166_Contract_TargetsAndMode()
    {
        var w = Design("design.survival-adjudications.json")["water"]!;
        Assert.That((string)w["mode"]!, Is.EqualTo("Q166_B"));
        var targets = w["targets"]!.Select(t => (string)t!).ToList();
        Assert.That(targets, Is.EquivalentTo(new[] { "purification_tablet", "filter_cartridge" }));
        // 两条固定等待净水配方：净水片/滤芯各一，经验门类烹饪、产 4×500ml
        foreach (var id in new[] { "ex_tablet_water", "ex_cartridge_water" })
        {
            var rc = Cfg.Recipes.Single(r => r.Id == id);
            Assert.That((rc.IsFixedWait, rc.XpSkill, rc.BaseMinutes), Is.EqualTo((true, "cooking", 20)), id);
            Assert.That(rc.Inputs.Keys.Intersect(targets).Count(), Is.EqualTo(1), id);
            Assert.That(rc.Outputs, Is.EqualTo(new Dictionary<string, int> { ["clean_water"] = 4 }), id);
        }
        Assert.That(Cfg.Items.Single(i => i.Id == "clean_water").UnitVolumeMl, Is.EqualTo(500));
        Assert.That((Cfg.Progression.Cooking.WaterMlPerXp, Cfg.Progression.Cooking.WaterDailyCap), Is.EqualTo((500, 4)));
    }

    /// <summary>PLAN-Q166-01/02：净水片 2L 正常成功给烹饪 4；20 分钟、不吃加速、全队等待不变；领取/读档不重给。</summary>
    [Test]
    public void PLAN_Q166_01_TwoLitres_Gives4_NoRegrant()
    {
        var s = WaterCamp();
        s.Skills["crafting"].Level = 10;
        var pv = E.Preview(s, ActionRequest.Craft("ex_tablet_water"));
        Assert.That(pv.EstimatedMinutes, Is.EqualTo(20), "固定 20 分钟，不吃制造加速");
        Assert.That(pv.IsWork, Is.False, "全队按等待");
        Assert.That(pv.XpGains, Has.Some.Contains("烹饪经验+4").And.Some.Contains("与锅烧水合计每日最多4"));
        Assert.That(pv.Notes, Has.Some.Contains("成功后每500毫升净水烹饪经验+1"));
        var cook0 = s.Skills["cooking"].Xp;
        var craft0 = s.Skills["crafting"].Xp;
        var t0 = s.GameMinute;
        Start(s, ActionRequest.Craft("ex_tablet_water"));
        Assert.That(s.Main!.XpSkill, Is.EqualTo("cooking"), "开工快照");
        Run(s);
        Assert.That(s.GameMinute - t0, Is.EqualTo(20));
        Assert.That(Count(s, "clean_water", s.CurrentLocation), Is.EqualTo(4));
        Assert.That(s.Skills["cooking"].Xp - cook0, Is.EqualTo(4));
        Assert.That(s.Skills["crafting"].Xp, Is.EqualTo(craft0), "不给制造");
        Assert.That(s.Log.Last(l => l.Kind == "ActionCompleted").Text, Does.Contain("烹饪经验+4"));
        Assert.That(s.Player.DailyCounts[WaterKey(s, GameEngine.DailyWaterXp)], Is.EqualTo(4));
        Assert.That(s.Player.DailyCounts.ContainsKey(WaterKey(s, GameEngine.DailyXp)), Is.False, "不计食品日 30（开发假设 B22a-1）");
        // 读档、拿起净水都不再给
        var t = SaveSerializer.Deserialize(SaveSerializer.Serialize(s), Cfg);
        Ok(E.DoInstant(t, InstantRequest.Pickup(First(t, "clean_water", t.CurrentLocation).InstanceId, 1)));
        Steps(t, 1);
        Assert.That(t.Skills["cooking"].Xp, Is.EqualTo(s.Skills["cooking"].Xp));
    }

    /// <summary>PLAN-Q166-02：当天锅烧水已给 3，本批只给 1；之后锅再烧也不再给（共享 4，不是另加 4）。</summary>
    [Test]
    public void PLAN_Q166_02_SharedCapWithPot_Remaining1()
    {
        var s = WaterCamp(2);
        s.Player.DailyCounts[WaterKey(s, GameEngine.DailyWaterXp)] = 3;
        s.Player.DailyCounts[WaterKey(s, GameEngine.DailyWaterMl)] = 1500;
        var pv = E.Preview(s, ActionRequest.Craft("ex_tablet_water"));
        Assert.That(pv.XpGains, Has.Some.Contains("烹饪经验+1").And.Some.Contains("今日只剩1"));
        var cook0 = s.Skills["cooking"].Xp;
        Start(s, ActionRequest.Craft("ex_tablet_water"));
        Run(s);
        Assert.That(s.Skills["cooking"].Xp - cook0, Is.EqualTo(1));
        Assert.That(s.Player.DailyCounts[WaterKey(s, GameEngine.DailyWaterXp)], Is.EqualTo(4));
        // 日额已满：再做一批净水片净水给 0
        E.CreateItems(s, "dirty_water", 4, s.CurrentLocation);
        Assert.That(E.Preview(s, ActionRequest.Craft("ex_tablet_water")).XpGains, Has.Some.Contains("今日净化经验已满4"));
        Start(s, ActionRequest.Craft("ex_tablet_water"));
        Run(s);
        Assert.That(s.Skills["cooking"].Xp - cook0, Is.EqualTo(1));
        Assert.That(s.Log.Last(l => l.Kind == "ActionCompleted").Text, Does.Contain("今日净化经验已满4"));
    }

    /// <summary>PLAN-Q166-02（反向共享）：固定等待先给 2，锅再净化 1000ml 只给到共享余量 2；锅自身按累计毫升计，不被固定等待的额度吃掉。</summary>
    [Test]
    public void PLAN_Q166_02_FixedFirst_ThenPot_SharesFour()
    {
        var s = New();
        var key = WaterKey(s, GameEngine.DailyWaterXp);
        s.Player.DailyCounts[key] = 2;
        s.Player.DailyCounts[WaterKey(s, GameEngine.DailyWaterFixedXp)] = 2;   // 已由固定等待给 2
        var pot = First(s, "pot");
        E.DebugGive(s, "dirty_water", 2, "camp");
        Ok(E.DoInstant(s, InstantRequest.FillPot(First(s, "dirty_water", "camp").InstanceId, pot.InstanceId, 2)));
        Ok(E.DoInstant(s, InstantRequest.PutInFire(pot.InstanceId)));
        var fac = s.Locations["camp"].Facility;
        fac.FireLit = true; fac.FuelMinutes = 240;
        var cook0 = s.Skills["cooking"].Xp;
        Start(s, ActionRequest.Wait(30));
        Run(s);
        Assert.That(s.Skills["cooking"].Xp - cook0, Is.EqualTo(2), "锅 1000ml=2，共享余量 2");
        Assert.That(s.Player.DailyCounts[key], Is.EqualTo(4));
    }

    /// <summary>PLAN-Q166-02：中途取消给 0（不跨批积余）；完成跨午夜按完成日额度结算。</summary>
    [Test]
    public void PLAN_Q166_02_CancelZero_CompletionDayCap()
    {
        var s = WaterCamp(2);
        var cook0 = s.Skills["cooking"].Xp;
        Start(s, ActionRequest.Craft("ex_tablet_water"));
        Steps(s, 2);
        Ok(E.Cancel(s));
        Assert.That(s.Skills["cooking"].Xp, Is.EqualTo(cook0), "取消 0");
        Assert.That(s.Player.DailyCounts.Keys.Any(k => k.Contains("|waterXp|")), Is.False);

        // 23:50 开工、次日 00:10 完成：开工日已满 4，完成日照常给 4
        var t = WaterCamp();
        t.GameMinute = At(1, 23, 50);
        t.Player.DailyCounts[WaterKey(t, GameEngine.DailyWaterXp)] = 4;
        var c0 = t.Skills["cooking"].Xp;
        Start(t, ActionRequest.Craft("ex_tablet_water"));
        Run(t);
        Assert.That(Clock.Day(t.GameMinute), Is.EqualTo(2));
        Assert.That(t.Skills["cooking"].Xp - c0, Is.EqualTo(4), "按完成日剩余额度");
    }

    /// <summary>PLAN-Q166-02（进行中批次）：新规则上线前已开工的固定等待（开工快照为制造）按原快照不给经验；读档后完成也不给。</summary>
    [Test]
    public void PLAN_Q166_02_InFlightOldSnapshot_NoXp()
    {
        var s = WaterCamp();
        Start(s, ActionRequest.Craft("ex_tablet_water"));
        s.Main!.XpSkill = "crafting";   // 旧版开工快照（rc.XpSkill 为空时记制造）
        var t = SaveSerializer.Deserialize(SaveSerializer.Serialize(s), Cfg);
        var xp0 = t.Skills.Values.Sum(k => k.Xp);
        Run(t);
        Assert.That(Count(t, "clean_water", t.CurrentLocation), Is.EqualTo(4));
        Assert.That(t.Skills.Values.Sum(k => k.Xp), Is.EqualTo(xp0));
        Assert.That(t.Log.Last(l => l.Kind == "ActionCompleted").Text, Does.Contain("固定等待不给经验"));
    }

    /// <summary>滤芯净水同样给（另需滤水器）；烹饪满级不再给。</summary>
    [Test]
    public void PLAN_Q166_01_Cartridge_SameRule_MaxedNone()
    {
        var s = New();
        E.DebugLearnAllKnowledge(s);
        s.Items.RemoveAll(i => i.ItemId is "dirty_water" or "clean_water");
        E.CreateItems(s, "dirty_water", 8, s.CurrentLocation);
        E.CreateItems(s, "filter_cartridge", 2, s.CurrentLocation);
        E.CreateItems(s, "water_filter", 1, s.CurrentLocation);
        var c0 = s.Skills["cooking"].Xp;
        Start(s, ActionRequest.Craft("ex_cartridge_water"));
        Run(s);
        Assert.That(s.Skills["cooking"].Xp - c0, Is.EqualTo(4));
        var t = s;
        t.Player.DailyCounts.Clear();
        t.Skills["cooking"].Level = 10;
        var xp = t.Skills["cooking"].Xp;
        Start(t, ActionRequest.Craft("ex_cartridge_water"));
        Run(t);
        Assert.That(t.Skills["cooking"].Xp, Is.EqualTo(xp));
        Assert.That(t.Log.Last(l => l.Kind == "ActionCompleted").Text, Does.Contain("已满10级"));
    }

    // ───────────── Q167 药粉/补液共用冷却 ─────────────

    /// <summary>
    /// PLAN-Q167-01：止泻药粉与口服补液整件共用 6 世界小时冷却（冷却中两件都不能用，补液的水分 +35 也拿不到）；
    /// 应急恢复剂独立冷却；三件只能战外本人用、不给医疗经验；5 分钟不变。
    /// </summary>
    [Test]
    public void PLAN_Q167_01_ReliefWholeItemCooldown_TonicIndependent_NoMedXp()
    {
        var s = New();
        foreach (var id in new[] { "antidiarrheal_powder", "oral_rehydration", "emergency_tonic" }) E.CreateItems(s, id, 2, ItemInstance.Carried);
        foreach (var id in new[] { "antidiarrheal_powder", "oral_rehydration", "emergency_tonic" })
        {
            var u = Cfg.Items.Single(i => i.Id == id).Use!;
            Assert.That((u.CooldownMinutes, u.OutOfBattleOnly), Is.EqualTo((360, true)), id);
        }
        Assert.That(Cfg.Items.Single(i => i.Id == "antidiarrheal_powder").Use!.CooldownGroup,
            Is.EqualTo(Cfg.Items.Single(i => i.Id == "oral_rehydration").Use!.CooldownGroup));
        Assert.That(Cfg.Items.Single(i => i.Id == "emergency_tonic").Use!.CooldownGroup,
            Is.Not.EqualTo(Cfg.Items.Single(i => i.Id == "oral_rehydration").Use!.CooldownGroup));
        var med0 = s.Skills["medicine"].Xp;
        s.DiarrheaUntil = s.GameMinute + 600;
        s.Stats.Hydration = 40;
        var t0 = s.GameMinute;
        Start(s, ActionRequest.UseInstance(First(s, "antidiarrheal_powder").InstanceId));
        Run(s);
        Assert.That(s.GameMinute - t0, Is.EqualTo(5));
        var w = s.Stats.Hydration;
        Assert.That(E.CanStart(s, ActionRequest.UseInstance(First(s, "oral_rehydration").InstanceId)), Does.Contain("冷却中"), "补液整件冷却，水分 +35 也拿不到");
        Assert.That(E.Start(s, ActionRequest.UseInstance(First(s, "oral_rehydration").InstanceId)).Ok, Is.False);
        Assert.That(s.Stats.Hydration, Is.EqualTo(w));
        // 恢复剂独立：药粉冷却中仍可用
        s.Stats.Energy = 10;
        Assert.That(E.CanStart(s, ActionRequest.UseInstance(First(s, "emergency_tonic").InstanceId)), Is.Null);
        Start(s, ActionRequest.UseInstance(First(s, "emergency_tonic").InstanceId));
        Run(s);
        Assert.That(E.CanStart(s, ActionRequest.UseInstance(First(s, "emergency_tonic").InstanceId)), Does.Contain("给药冷却中"));
        Assert.That(s.Skills["medicine"].Xp, Is.EqualTo(med0), "不给医疗经验");
        // 6 小时后补液恢复可用（从药粉那次成功使用起算）
        AdvanceTo(s, t0 + 360);
        s.DiarrheaUntil = s.GameMinute + 300;
        Assert.That(E.CanStart(s, ActionRequest.UseInstance(First(s, "oral_rehydration").InstanceId)), Is.Null);
    }

    // ───────────── Q169 锅食谱按批经验 ─────────────

    private (GameState s, ItemInstance pot) PotSetup(int water, params (string id, int n)[] give)
    {
        var s = New();
        E.DebugLearnAllKnowledge(s);
        s.Skills["cooking"].Level = 5;
        s.Items.RemoveAll(i => i.ItemId is "grain" or "salt" or "raw_meat" or "clean_water" or "dirty_water");
        var pot = s.Items.First(i => i.ItemId == "pot");
        E.DebugGive(s, "clean_water", water, "camp");
        Ok(E.DoInstant(s, InstantRequest.FillPot(First(s, "clean_water", "camp").InstanceId, pot.InstanceId, water)));
        if (pot.IsCarried) Ok(E.DoInstant(s, InstantRequest.Drop(pot.InstanceId, 1)));
        Ok(E.DoInstant(s, InstantRequest.PutInFire(pot.InstanceId)));
        var f = s.Locations["camp"].Facility;
        f.FireLit = true;
        f.FuelMinutes = 240;
        foreach (var (id, n) in give) E.CreateItems(s, id, n, "camp");
        return (s, pot);
    }

    private void WaitMinutes(GameState s, int minutes)
    {
        var end = s.GameMinute + minutes;
        while (s.GameMinute < end) { Start(s, ActionRequest.Wait(30)); Run(s); }
    }

    /// <summary>PLAN-Q169-01：硬饼一批（出 2 份）给投料人烹饪 6、炖食一批（出 2 份）给 5，每批一次、不按份数；计入每日 30。</summary>
    [TestCase("pot_hardtack", "hardtack", 1, 6)]
    [TestCase("pot_stew", "stew_portion", 2, 5)]
    public void PLAN_Q169_01_PotRecipe_PerBatchXp(string process, string product, int water, int xp)
    {
        var (s, pot) = PotSetup(water, ("grain", 2), ("salt", 1), ("raw_meat", 1));
        Ok(E.DoInstant(s, InstantRequest.LoadPotRecipe(process, pot.InstanceId)));
        var x0 = s.Skills["cooking"].Xp;
        WaitMinutes(s, 60);
        Assert.That(E.PotFood(s, pot.InstanceId).Count(i => i.ItemId == product), Is.EqualTo(2), "一批出两份");
        Assert.That(s.Skills["cooking"].Xp - x0, Is.EqualTo(xp), "每批一次，不按成品份数");
        Assert.That(s.Player.DailyCounts[WaterKey(s, GameEngine.DailyXp)], Is.EqualTo(xp), "计入食品日 30");

        // 日 30：已得 28，只给 2
        var (t, pot2) = PotSetup(water, ("grain", 2), ("salt", 1), ("raw_meat", 1));
        t.Player.DailyCounts[WaterKey(t, GameEngine.DailyXp)] = 28;
        Ok(E.DoInstant(t, InstantRequest.LoadPotRecipe(process, pot2.InstanceId)));
        var y0 = t.Skills["cooking"].Xp;
        WaitMinutes(t, 60);
        Assert.That(t.Skills["cooking"].Xp - y0, Is.EqualTo(2));
    }
}
