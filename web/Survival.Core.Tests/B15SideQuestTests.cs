using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 批次 B15 M326：城市支线 HU01—HU04、CH01—CH02、AC01—AC02（R47；A11「5 支线委托最小表」、narrative-texts 8 支线接交文本）。
/// 验收：S15 TXT05（接交与原工作账同一、复看不奖）、TXT06（CH02 两路同完成账、不入盟；AC01 拒观察仍能接主线）、TXT07（AC02 医疗线索）、
/// TXT08/CITY-N05（岗位代班仍可继续）、CITY-N06（工作中断、读档后奖励幂等）。
/// </summary>
public class B15SideQuestTests : TestBase
{
    private void Ok(ActionResult r) => Assert.That(r.Ok, Is.True, r.Reason);

    private static string Q(string id, string kind) => GameEngine.QuestFact(id, kind);

    private static readonly string[] Registered = { "registration_authorized", "hub_registered" };

    private GameState At(string loc, params string[] facts)
    {
        var s = New();
        s.Progress.Flags.UnionWith(Registered.Concat(facts));
        E.DebugMapTeleport(s, loc);
        ReadDialogues(s);
        return s;
    }

    private (int, int, double, double, double) Body(GameState s) =>
        (s.Items.Sum(i => i.Count), s.Progress.Flags.Count(f => !f.StartsWith("quest:")), s.Stats.Health, s.Body.VirusLoad, s.Body.Immunity);

    /// <summary>TXT05：HU01 交谈接取（播接取对白）→ 阅读说明 0 分钟完成（播交付）→ HU02 随即可接；回看只回放、不扣时不发物；没有样本也能完成。</summary>
    [Test]
    public void TXT05_HU01_HU02_TalkAcceptReadTurnin()
    {
        var s = At("h05_appraisal");
        Assert.That(E.QuestViewOf(s, "HU01")!.Status, Is.EqualTo("available"));
        Assert.That(E.QuestViewOf(s, "HU02"), Is.Null, "HU01 完成前不显示");
        var card = E.NpcsHere(s).Single(c => c.Kind == "post");
        Assert.That(card.QuestText, Is.EqualTo("委托：鉴定之前（可接）"));
        Assert.That((card.QuestCount, card.HasQuests), Is.EqualTo((1, true)), "有委托也出“事项”按钮");
        var body = Body(s);
        var minute = s.GameMinute;
        Ok(E.TalkNpc(s, "npc_tao_cen"));
        Assert.That(s.Story.DialogueLog.Select(d => d.EventId), Does.Contain("HU01_offer"), "首次交谈：初次见面、岗位说明后接取");
        ReadDialogues(s);
        Assert.That(s.Progress.Flags, Does.Contain(Q("HU01", "accepted")));
        var v = E.QuestViewOf(s, "HU01")!;
        Assert.That((v.Status, v.WorkLabel), Is.EqualTo(("active", "阅读说明（0分钟）")));
        Ok(E.StartQuestWork(s, "HU01"));
        Assert.That(s.Story.DialogueLog.Select(d => d.EventId), Does.Contain("HU01_turnin"));
        ReadDialogues(s);
        Assert.That(s.Progress.Flags, Does.Contain(Q("HU01", "completed")));
        Assert.That(E.StartQuestWork(s, "HU01").Ok, Is.False, "完成后不再领奖");
        Ok(E.TalkNpc(s, "npc_tao_cen"));
        ReadDialogues(s);
        Assert.That(s.Progress.Flags, Does.Contain(Q("HU02", "accepted")));
        Ok(E.StartQuestWork(s, "HU02"));
        ReadDialogues(s);
        Assert.That(s.Progress.Flags, Does.Contain(Q("HU02", "completed")));
        Assert.That(s.GameMinute, Is.EqualTo(minute), "对话与阅读都 0 分钟");
        Assert.That(Body(s), Is.EqualTo(body), "不发物、不改身体、不写别的事实");
        Ok(E.ReviewQuest(s, "HU01"));
        ReadDialogues(s);
        Assert.That(s.Story.DialogueLog.Any(d => d.EventId.StartsWith("HU01_repeat")), Is.True);
        Assert.That((s.GameMinute, Body(s)), Is.EqualTo((minute, body)));
        Ok(E.TalkNpc(s, "npc_tao_cen"));
        ReadDialogues(s);
        Assert.That(s.Story.DialogueLog.Last().EventId, Does.StartWith("npc_tao_cen_dialogue_idle"), "都完成后交谈回到闲谈");
    }

    /// <summary>HU03 任何时候可读（日志写当前容量与名册）；HU04 名册里有同行者才可接，完成日志列每人伤病风险，不改队伍、不清伤病。</summary>
    [Test]
    public void HU03_HU04_RosterRequirement_NoPartyChange()
    {
        var s = At("h06_shelter");
        Ok(E.TalkNpc(s, "npc_du_sui"));
        ReadDialogues(s);
        Ok(E.StartQuestWork(s, "HU03"));
        ReadDialogues(s);
        Assert.That(s.Log.Any(l => l.Text.Contains("同行不是归属") && l.Text.Contains("当前队伍容量C2")), Is.True);
        var v = E.QuestViewOf(s, "HU04")!;
        Assert.That((v.Status, v.AcceptBlocked), Is.EqualTo(("locked", "需要：名册里有一名同行者")));
        E.DebugRecruit(s, "npc_lin_zhao");
        ReadDialogues(s);
        Assert.That(E.QuestViewOf(s, "HU04")!.Status, Is.EqualTo("available"));
        Ok(E.AcceptQuest(s, "HU04"));
        ReadDialogues(s);
        var party = s.Party.ToList();
        Ok(E.StartQuestWork(s, "HU04"));
        ReadDialogues(s);
        Assert.That(s.Log.Last(l => l.Text.Contains("留守之前")).Text, Does.Contain("林昭：健康"));
        Assert.That(s.Party, Is.EqualTo(party), "不把队员强移候补");
    }

    /// <summary>
    /// CH01：FA02 已接且已领圣所旧路档册才可接（B15b，Q162 选 A；原“FA02 已接即可接”已取代，见 B15bTests.QUEST_Q162_01_*）；
    /// 核对 wait10；完成播交付、不领另一份箱。
    /// </summary>
    [Test]
    public void CH01_NeedsRecord_Wait10()
    {
        var s = At("c04_alms");
        Assert.That(E.QuestViewOf(s, "CH01"), Is.Null, "FA02 未接前不显示");
        s.Progress.Flags.Add(Q("FA02", "accepted"));
        Assert.That(E.QuestViewOf(s, "CH01"), Is.Null, "Q162：未领档册仍不显示");
        E.DebugClaimRecord(s, "d01_08_unique");
        ReadDialogues(s);
        Ok(E.TalkNpc(s, "npc_bai_ling"));
        ReadDialogues(s);
        Assert.That(s.Progress.Flags, Does.Contain(Q("CH01", "accepted")));
        var v = E.QuestViewOf(s, "CH01")!;
        Assert.That((v.WorkLabel, v.WorkBlocked), Is.EqualTo(("核对物资账（10分钟）", (string?)null)));
        var items = s.Items.Sum(i => i.Count);
        var t0 = s.GameMinute;
        Ok(E.StartQuestWork(s, "CH01"));
        ReadDialogues(s);
        Run(s);
        Assert.That(s.GameMinute, Is.EqualTo(t0 + 10));
        Assert.That(s.Progress.Flags, Does.Contain(Q("CH01", "completed")));
        Assert.That(s.Story.DialogueLog.Any(d => d.EventId == "CH01_turnin"), Is.True);
        ReadDialogues(s);
        Assert.That(s.Items.Sum(i => i.Count), Is.EqualTo(items));
        Assert.That(E.StartQuestWork(s, "CH01").Ok, Is.False);
    }

    /// <summary>
    /// TXT06：CH02 在训诫堂选“回营区备份”，中途停下保留 5 分钟；不能改到典藏室抄录续做；续完 5 分钟完成；典藏室只回看；
    /// 下次到营区广场播一次许安的备份回报；全程不写会籍。
    /// </summary>
    [Test]
    public void TXT06_CH02_BackupBranch_KeepSegment_HubTurninOnce()
    {
        var s = At("c05_admonition", Q("CH01", "completed"), Q("FA02", "accepted"), "FA02_record_credit");
        var v = E.CityServicePreview(s, "church_public_mediation")!;
        Assert.That(v.Blocked, Is.EqualTo("先和纪牧交谈，接取“被扣下的记录”"));
        Ok(E.TalkNpc(s, "npc_ji_mu"));
        ReadDialogues(s);
        v = E.CityServicePreview(s, "church_public_mediation")!;
        Assert.That(v.Choices.Select(c => c.Label), Is.EqualTo(new[] { "协商抄录公开路线", "以已有实地记录回营区备份" }));
        Assert.That(v.Blocked, Is.Null);
        var t0 = s.GameMinute;
        Ok(E.StartCityService(s, "church_public_mediation", null, "CH02_backup_hub"));
        ReadDialogues(s);
        for (var i = 0; i < 20 && s.GameMinute < t0 + 5; i++) E.Advance(s);
        Ok(E.Cancel(s));
        Assert.That(s.Quests.Records["CH02"].WorkMinutes, Is.EqualTo(5));
        E.DebugMapTeleport(s, "c06_archive");
        ReadDialogues(s);
        Assert.That(E.CityServicePreview(s, "church_archive_copy")!.Blocked, Does.Contain("请回训诫堂续做"));
        E.DebugMapTeleport(s, "c05_admonition");
        ReadDialogues(s);
        var t1 = s.GameMinute;
        Ok(E.StartCityService(s, "church_public_mediation", null, "CH02_backup_hub"));
        Run(s);
        Assert.That(s.GameMinute, Is.EqualTo(t1 + 5));
        Assert.That(s.Progress.Flags, Does.Contain(Q("CH02", "completed")));
        ReadDialogues(s);
        E.DebugMapTeleport(s, "c06_archive");
        ReadDialogues(s);
        Assert.That(E.CityServicePreview(s, "church_archive_copy")!.Blocked, Does.Contain("已归档"), "另一处只回看，不再收时间");
        E.DebugMapTeleport(s, "h02_square");
        Assert.That(s.Story.DialogueLog.Count(d => d.EventId == "CH02_hub_backup_turnin"), Is.EqualTo(1));
        ReadDialogues(s);
        E.DebugMapTeleport(s, "h03_workshop");
        E.DebugMapTeleport(s, "h02_square");
        Assert.That(s.Story.DialogueLog.Count(d => d.EventId.StartsWith("CH02_hub_backup_turnin")), Is.EqualTo(1), "只播一次");
        Assert.That(s.Progress.Flags.Any(GameEngine.IsMembershipFact), Is.False);
    }

    /// <summary>CH02 选抄录：典藏室完成也算同一份委托；训诫堂之后只回看。</summary>
    [Test]
    public void CH02_CopyAtArchive_SameLedger()
    {
        var s = At("c05_admonition", Q("CH01", "completed"), Q("FA02", "accepted"), "FA02_record_credit");
        Ok(E.TalkNpc(s, "npc_ji_mu"));
        ReadDialogues(s);
        E.DebugMapTeleport(s, "c06_archive");
        ReadDialogues(s);
        Ok(E.StartCityService(s, "church_archive_copy"));
        Run(s);
        ReadDialogues(s);
        Assert.That(s.Progress.Flags, Does.Contain(Q("CH02", "completed")));
        E.DebugMapTeleport(s, "c05_admonition");
        ReadDialogues(s);
        Assert.That(E.CityServicePreview(s, "church_public_mediation")!.Blocked, Does.Contain("已归档"));
        E.DebugMapTeleport(s, "h02_square");
        Assert.That(s.Story.DialogueLog.Any(d => d.EventId == "CH02_hub_backup_turnin"), Is.False, "抄录分支不播营区备份回报");
    }

    /// <summary>TXT06/FN03：AC01 两个范围同一份委托、都 10 分钟、不改身体数值；拒绝身体观察（只核对环境）主线照常；r08 查阅观察记录写已有记录。</summary>
    [TestCase("AC01_environment")]
    [TestCase("AC01_noninvasive")]
    public void TXT06_AC01_EitherScope_NoBodyChange(string choice)
    {
        var s = At("r05_testing", Q("FA03", "accepted"), "FA03_environment_briefed");
        Ok(E.TalkNpc(s, "npc_ji_wen"));
        ReadDialogues(s);
        var body = (Health: s.Stats.Health, VirusLoad: s.Body.VirusLoad, Immunity: s.Body.Immunity);
        Ok(E.StartCityService(s, "academy_noninvasive_observation", null, choice));
        ReadDialogues(s);
        Run(s);
        ReadDialogues(s);
        Assert.That(s.Progress.Flags, Does.Contain(Q("AC01", "completed")));
        Assert.That(s.Quests.Records["AC01"].Choice, Is.EqualTo(choice));
        Assert.That((s.Stats.Health, s.Body.VirusLoad), Is.EqualTo((body.Health, body.VirusLoad)), "只有等待中的自然变化（免疫照常缓慢恢复），观察不另改数值");
        Assert.That(E.QuestViewOf(s, "FA03")!.Status, Is.EqualTo("active"), "主线不受范围选择影响");
        E.DebugMapTeleport(s, "r08_observation");
        ReadDialogues(s);
        Assert.That(E.CityServicePreview(s, "academy_observation_record")!.EffectText, Does.Contain("已有观察核对记录"));
    }

    /// <summary>TXT07：AC02 封存区安全规程到手后可接；比对 10 分钟，完成播交付与外部医疗线索（可在任务册回看）；不授知识、容量或药。</summary>
    [Test]
    public void TXT07_AC02_MedicalNote()
    {
        var s = At("r06_archive", Q("FA03", "accepted"));
        Assert.That(E.QuestViewOf(s, "AC02"), Is.Null);
        E.DebugClaimRecord(s, "d02_08_unique");
        ReadDialogues(s);
        Ok(E.TalkNpc(s, "npc_shao_ning"));
        ReadDialogues(s);
        var cap = E.BattleCapacity(s);
        var know = s.Progress.Flags.Count(f => f.StartsWith("knowledge:"));
        var items = s.Items.Sum(i => i.Count);
        Ok(E.StartCityService(s, "academy_report_compare"));
        Run(s);
        Assert.That(s.Story.DialogueLog.Select(d => d.EventId), Is.SupersetOf(new[] { "AC02_turnin", "AC02_external_medical_note" }));
        ReadDialogues(s);
        var v = E.QuestViewOf(s, "AC02")!;
        Assert.That((v.Status, v.HasNote), Is.EqualTo(("done", true)));
        Assert.That((E.BattleCapacity(s), s.Progress.Flags.Count(f => f.StartsWith("knowledge:")), s.Items.Sum(i => i.Count)), Is.EqualTo((cap, know, items)));
        Ok(E.ReviewQuest(s, "AC02", note: true));
        ReadDialogues(s);
        Assert.That(s.Story.DialogueLog.Count(d => d.EventId.StartsWith("AC02_external_medical_note")), Is.EqualTo(2));
    }

    /// <summary>TXT08/CITY-N05：岗位离岗由当班接续员代班，委托照接（不播原人物台词）、工作照办、任务账不重置。</summary>
    [Test]
    public void TXT08_SubstituteStillAcceptsAndWorks()
    {
        var s = At("r06_archive", Q("FA03", "accepted"), "FA03_record_credit");
        E.DebugToggleCityPostAbsent(s);
        Ok(E.TalkNpc(s, "npc_shao_ning"));
        // M333（A11 Q32B“换speaker到代班角色”）：代班交谈播接续两句后照播委托接取台词，原人物的句子由当班接续员说
        Assert.That(s.Story.DialogueLog.Any(d => d.EventId == "AC02_offer"), Is.True);
        ReadDialogues(s);
        var lines = s.Story.DialogueLog.SelectMany(d => d.Lines).ToList();
        Assert.That(lines.Any(l => l.Speaker == "邵宁"), Is.False, "离岗人物不再发言");
        Assert.That(lines.Where(l => l.LineId.StartsWith("NTL_")).Any(l => l.Speaker == "机构当班接续员"), Is.True);
        Assert.That(s.Progress.Flags, Does.Contain(Q("AC02", "accepted")));
        Ok(E.StartCityService(s, "academy_report_compare"));
        Run(s);
        ReadDialogues(s);
        Assert.That(s.Progress.Flags, Does.Contain(Q("AC02", "completed")));
        Ok(E.ReviewQuest(s, "AC02"));
        ReadDialogues(s);
        lines = s.Story.DialogueLog.SelectMany(d => d.Lines).ToList();
        Assert.That(lines.Any(l => l.Speaker == "邵宁"), Is.False, "交付与回看也由当班接续员说");
        E.DebugToggleCityPostAbsent(s);
        Ok(E.ReviewQuest(s, "AC02"));
        ReadDialogues(s);
        Assert.That(s.Story.DialogueLog.Last().Lines.Any(l => l.Speaker == "邵宁"), Is.True, "回岗后回看照常由原人物说");
    }

    /// <summary>
    /// CITY-Q151-02：科学院支线实装后，观察范围委托可接时与尹珂交谈播 NTL_0304（逐字定稿、不含内部编号），0 分钟、不收费不计时；
    /// 已接未完成时播“委托回报”；旧对白不重播奖励。
    /// </summary>
    [Test]
    public void CITY_Q151_02_YinKeQuestLinePlays()
    {
        var s = At("r08_observation", Q("FA03", "accepted"), "academy_observation_pass");
        Ok(E.TalkNpc(s, "npc_yin_ke"));
        ReadDialogues(s);
        var before = (s.GameMinute, s.Items.Sum(i => i.Count), s.Progress.Flags.Count);
        Ok(E.TalkNpc(s, "npc_yin_ke"));
        ReadDialogues(s);
        Assert.That(s.Story.DialogueLog.Any(d => d.EventId.StartsWith("npc_yin_ke_dialogue_quest_offer")), Is.True);
        Assert.That(Cfg.Story.Dialogues.Single(d => d.Id == "NTL_0304").Text, Is.EqualTo("这里只接续你已选的观察范围，不再收一次观察协作工作时间。"));
        Assert.That((s.GameMinute, s.Items.Sum(i => i.Count), s.Progress.Flags.Count), Is.EqualTo(before), "不收费、不计时、不写事实");
        s.Progress.Flags.Add(Q("AC01", "accepted"));
        Ok(E.TalkNpc(s, "npc_yin_ke"));
        ReadDialogues(s);
        Assert.That(s.Story.DialogueLog.Last().EventId, Does.StartWith("npc_yin_ke_dialogue_quest_return"));
    }

    /// <summary>CITY-N06：工作进行中存档读档，续完只完成一次；读档后再请求不重复奖励。</summary>
    [Test]
    public void CITYN06_SaveLoadMidWork_Idempotent()
    {
        var s = At("r06_archive", Q("FA03", "accepted"), "FA03_record_credit");
        Ok(E.TalkNpc(s, "npc_shao_ning"));
        ReadDialogues(s);
        Ok(E.StartCityService(s, "academy_report_compare"));
        E.Advance(s);
        var back = SaveSerializer.Deserialize(SaveSerializer.Serialize(s), Cfg, out _);
        Run(back);
        ReadDialogues(back);
        Assert.That(back.Progress.Flags, Does.Contain(Q("AC02", "completed")));
        Assert.That(back.Log.Count(l => l.Text.Contains("相互矛盾的报告") && l.Text.Contains("完成")), Is.EqualTo(1));
        Assert.That(E.StartCityService(back, "academy_report_compare").Ok, Is.False);
    }

    /// <summary>M333：岗位卡“事项 a/b 项可办”与按钮 N 项同一口径——接待路线算一项（不和委托重复计），领过路线/委托已归档的服务不计。</summary>
    [Test]
    public void M333_CardCountsConsistent()
    {
        var s = At("c02_church");
        var c = E.NpcsHere(s).Single(x => x.Kind == "post");
        Assert.That((c.Status, c.ItemCount), Is.EqualTo(("可交谈 · 事项 1/1 项可办", 1)), "询问公开路线与“被称作新生的人”算一项");
        Ok(E.StartCityService(s, "church_reception_existing", null, "FA02_accept_no_oath"));
        ReadDialogues(s);
        c = E.NpcsHere(s).Single(x => x.Kind == "post");
        Assert.That((c.Status, c.ItemCount), Is.EqualTo(("可交谈 · 事项 0/1 项可办", 1)), "路线已领不计；主线进行中计 1、现在没有可办的");
        var t = At("c06_archive", Q("CH01", "completed"), Q("FA02", "accepted"), "FA02_record_credit", Q("CH02", "accepted"));
        Ok(E.StartCityService(t, "church_archive_copy"));
        Run(t);
        ReadDialogues(t);
        c = E.NpcsHere(t).Single(x => x.Kind == "post");
        Assert.That((c.Status, c.ItemCount, c.ServiceCount), Is.EqualTo(("可交谈", 0, 1)), "典藏室已归档：不计（按钮写“可回看”）");
        var h = At("h05_appraisal");
        Ok(E.TalkNpc(h, "npc_tao_cen"));
        ReadDialogues(h);
        c = E.NpcsHere(h).Single(x => x.Kind == "post");
        Assert.That((c.Status, c.ItemCount), Is.EqualTo(("可交谈 · 事项 2/2 项可办", 2)), "委托阅读说明算一项可办；B18 起鉴定所服务也算一项");
    }

    /// <summary>M333：HU04 前置未满足时“下一步”不再重复条件（条件在未满足条件行），满足后与状态一致；中途停下委托工作的日志写实际时长。</summary>
    [Test]
    public void M333_Hu04Step_CancelLogActualMinutes()
    {
        var s = At("h06_shelter");
        Assert.That(E.QuestViewOf(s, "HU04")!.Step, Is.EqualTo("到收容所和杜穗交谈。"));
        var t = At("r06_archive", Q("FA03", "accepted"), "FA03_record_credit");
        Ok(E.TalkNpc(t, "npc_shao_ning"));
        ReadDialogues(t);
        var t0 = t.GameMinute;
        Ok(E.StartCityService(t, "academy_report_compare"));
        for (var i = 0; i < 20 && t.GameMinute < t0 + 5; i++) E.Advance(t);
        Ok(E.Cancel(t));
        Assert.That(t.Log.Any(l => l.Text == "已停止比对观察报告（实际进行了5分钟）。"), Is.True, string.Join("|", t.Log.TakeLast(3).Select(l => l.Text)));
    }
}
