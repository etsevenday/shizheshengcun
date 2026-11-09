using Newtonsoft.Json.Linq;
using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 批次 B15b：策划答复 Q160—Q162（策划提交 9acc2b9，需求 R159—R161）。
/// 规则出处：A11「Q160—Q162 委托显示与接取定稿」、更新说明 2026-11-02「Q160-Q162」、assets/20261102/city/quest-presentation-adjudications.json。
/// 验收 QUEST-Q160-01、QUEST-Q161-01、QUEST-Q162-01。
/// </summary>
public class B15bTests : TestBase
{
    private void Ok(ActionResult r) => Assert.That(r.Ok, Is.True, r.Reason);

    private static string Q(string id, string kind) => GameEngine.QuestFact(id, kind);

    private static readonly string[] Registered = { "registration_authorized", "hub_registered" };

    private static JObject Design() =>
        JObject.Parse(File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "design.quest-presentation.json")));

    private GameState At(string loc, params string[] facts)
    {
        var s = New();
        s.Progress.Flags.UnionWith(Registered.Concat(facts));
        E.DebugMapTeleport(s, loc);
        ReadDialogues(s);
        return s;
    }

    private void Go(GameState s, string loc)
    {
        E.DebugMapTeleport(s, loc);
        ReadDialogues(s);
    }

    private const string OldFa04Line = "将按FA04原工作预览整理记录；对话本身不替代工作提交。";
    private const string OldWorkHint = "请查看本任务领域预览后确认。耗时、可用记录和当前进度按现有规则显示；工作未完成前不会自动结算。";

    // ───────────── Q160 FA04 系统台词 ─────────────

    /// <summary>
    /// QUEST-Q160-01：开始“整理调查记录”播的 FA04_merge_accept 系统句逐字为定稿“将按‘同一条归路’的工作预览整理记录；对话本身不替代工作提交。”（B38 起为 N02 新稿），
    /// 新生成的对话记录里没有“FA04”；工作预览（10 分钟）与结算（FA04_merged、完成）不变；已有的旧对话记录快照不被批量改写（读档后也一样）。
    /// </summary>
    [Test]
    public void QUEST_Q160_01_Fa04SystemLine()
    {
        var contract = (string)Design()["mainWorkText"]!;
        Assert.That(contract, Is.EqualTo("将按‘同一条归路’的工作预览整理记录；对话本身不替代工作提交。"));
        var want = N02.Line("NTL_0325", contract);   // B38 N02 新稿
        Assert.That(want, Is.EqualTo("到「同一条归路」的工作预览核对记录，提交后才算整理完成。"));
        Assert.That(Cfg.Story.Dialogues.Where(d => d.Event == "FA04_merge_accept").Select(d => d.Text), Does.Contain(want));
        Assert.That(Cfg.Story.Dialogues.Any(d => d.Text.Contains("FA04")), Is.False, "玩家可见台词不带内部编号");

        var s = At("h02_square");
        // 旧档里已有的一段旧句记录（快照）：不随新配置倒改
        s.Story.DialogueLog.Add(new DialogueSession
        {
            Id = s.Story.NextDialogueSessionId++, EventId = "FA04_merge_accept#old", Ended = true,
            Lines = { new DialogueLine { LineId = "NTL_0325", Order = 1, Text = OldFa04Line, Side = DialogueSide.Narration } },
        });
        E.DebugClaimRecord(s, "d01_08_unique");
        E.DebugClaimRecord(s, "d02_08_unique");
        ReadDialogues(s);
        Assert.That(E.CityServicePreview(s, "hub_affairs_record")!.TimeText, Is.EqualTo("10分钟"), "工作预览不变");
        var t0 = s.GameMinute;
        Ok(E.StartCityService(s, "hub_affairs_record"));
        var session = s.Story.DialogueLog.Last(d => d.EventId == "FA04_merge_accept");
        ReadDialogues(s);
        var texts = session.Lines.Select(l => l.Text).ToList();
        Assert.That(texts, Does.Contain(want));
        Assert.That(texts.Any(t => t.Contains("FA04")), Is.False);
        Assert.That(session.Lines.Single(l => l.Text == want).Side, Is.EqualTo(DialogueSide.Narration), "系统句仍是中性叙述");
        Run(s);
        Assert.That(s.GameMinute, Is.EqualTo(t0 + 10));
        Assert.That(s.Progress.Flags, Is.SupersetOf(new[] { "FA04_merged", Q("FA04", "completed") }), "内部 FA04 标识与结算不变");
        var back = SaveSerializer.Deserialize(SaveSerializer.Serialize(s), Cfg, out _);
        Assert.That(back.Story.DialogueLog.Single(d => d.EventId == "FA04_merge_accept#old").Lines[0].Text, Is.EqualTo(OldFa04Line), "旧日志不批量改写");
        Assert.That(back.Story.DialogueLog.Last(d => d.EventId == "FA04_merge_accept").Lines.Select(l => l.Text), Does.Contain(want));
    }

    // ───────────── Q161 八条工作提示 ─────────────

    /// <summary>B38 N02：定稿八句按 {quest}_work 组的台词 id 换成 N02 新稿（合同原文须是 N02 的 oldText）。</summary>
    private string WorkHint(string quest) =>
        N02.Line(Cfg.Story.Dialogues.Single(d => d.Event == quest + "_work").Id, (string)Design()["workHints"]![quest]!);

    /// <summary>
    /// QUEST-Q161-01：HU01—HU04、CH01、CH02、AC01、AC02 进行中时的工作提示分别逐字匹配定稿八句，原通用句不再出现；
    /// 任务册与岗位事项（同一视图）一致；真实耗时、工作按钮/条件与进度照旧；反复查看 0 分钟、不提交工作、不发奖励。
    /// </summary>
    [TestCase("HU01", "npc_tao_cen", 0, "阅读说明（0分钟）")]
    [TestCase("HU02", "npc_tao_cen", 0, "了解范围（0分钟）")]
    [TestCase("HU03", "npc_du_sui", 0, "阅读说明（0分钟）")]
    [TestCase("HU04", "npc_du_sui", 0, "查看说明（0分钟）")]
    [TestCase("CH01", "npc_bai_ling", 10, "核对物资账（10分钟）")]
    [TestCase("CH02", "npc_ji_mu", 10, null)]
    [TestCase("AC01", "npc_ji_wen", 10, null)]
    [TestCase("AC02", "npc_shao_ning", 10, null)]
    public void QUEST_Q161_01_WorkHints(string quest, string npc, int minutes, string? workLabel)
    {
        var want = WorkHint(quest);
        Assert.That(Cfg.Story.Dialogues.Any(d => d.Text == OldWorkHint), Is.False, "原通用句不再出现在配置里");
        var s = At("h02_square", Q(quest, "accepted"));
        var v = E.QuestViewOf(s, quest)!;
        Assert.That((v.Status, v.WorkHint), Is.EqualTo(("active", want)));
        Assert.That((v.WorkTotal, v.WorkLabel), Is.EqualTo((minutes, workLabel)), "真实耗时与工作按钮照旧");
        if (quest == "CH01") Assert.That(v.WorkBlocked, Is.EqualTo("需要：在教会地下圣所领取圣所旧路档册"), "条件照旧");
        Assert.That(E.QuestBook(s).Single(x => x.Id == quest).WorkHint, Is.EqualTo(want), "任务册");
        Assert.That(E.QuestsOfNpc(s, npc).Single(x => x.Id == quest).WorkHint, Is.EqualTo(want), "岗位事项");
        var json = SaveSerializer.Serialize(s);
        for (var i = 0; i < 3; i++)
        {
            E.QuestViewOf(s, quest);
            E.QuestBook(s);
            E.QuestsOfNpc(s, npc);
        }
        Assert.That(SaveSerializer.Serialize(s), Is.EqualTo(json), "查看 0 分钟 0 资源，不提交工作、不发奖励");
        Assert.That(s.Progress.Flags, Does.Not.Contain(Q(quest, "completed")));
        // 已完成后不再显示工作提示（回看照旧）
        s.Progress.Flags.Add(Q(quest, "completed"));
        Assert.That(E.QuestViewOf(s, quest)!.WorkHint, Is.Empty);
    }

    /// <summary>QUEST-Q161-01（进度保留）：CH01 已核对 5 分钟时，提示换成定稿句，状态仍写已完成段，按钮只补剩余 5 分钟。</summary>
    [Test]
    public void QUEST_Q161_01_ProgressKept()
    {
        var s = At("c04_alms", Q("FA02", "accepted"), "FA02_record_credit", Q("CH01", "accepted"));
        s.Quests.Of("CH01").WorkMinutes = 5;
        var v = E.QuestViewOf(s, "CH01")!;
        Assert.That(v.WorkHint, Is.EqualTo(WorkHint("CH01")));
        Assert.That((v.StatusText, v.WorkLabel, v.WorkBlocked), Is.EqualTo(("进行中（已完成5/10分钟）", "核对物资账（5分钟）", (string?)null)));
    }

    // ───────────── Q162 CH01 接取须已领档册 ─────────────

    /// <summary>
    /// QUEST-Q162-01（未领）：FA02 已接、未领圣所旧路档册时，任务册与白苓岗位都不提“施济清单”，交谈只是闲谈/岗位对白，不能接取。
    /// </summary>
    [Test]
    public void QUEST_Q162_01_NoRecord_NotOffered()
    {
        var s = At("c04_alms", Q("FA02", "accepted"));
        Assert.That(E.QuestViewOf(s, "CH01"), Is.Null);
        Assert.That(E.QuestBook(s).Select(v => v.Id), Does.Not.Contain("CH01"));
        Assert.That(E.QuestsOfNpc(s, "npc_bai_ling"), Is.Empty);
        var card = E.NpcsHere(s).Single(c => c.NpcId == "npc_bai_ling");
        Assert.That(card.QuestText ?? "", Does.Not.Contain("施济清单"));
        Ok(E.TalkNpc(s, "npc_bai_ling"));
        ReadDialogues(s);
        Assert.That(s.Progress.Flags, Does.Not.Contain(Q("CH01", "accepted")));
        Assert.That(s.Story.DialogueLog.Any(d => d.EventId == "CH01_offer"), Is.False, "不播接取句");
        Assert.That(s.Story.DialogueLog.Any(d => d.EventId == "npc_bai_ling_dialogue_quest_offer"), Is.False, "岗位不播“委托”一组");
        Assert.That(E.AcceptQuest(s, "CH01").Ok, Is.False);
    }

    /// <summary>
    /// QUEST-Q162-01（已领）：领到圣所旧路档册后，不在施济处不自动接取、条目里接取要求实际到达；到施济处与白苓交谈即接取并播原接取句（两句不改）。
    /// </summary>
    [Test]
    public void QUEST_Q162_01_WithRecord_TalkAccept()
    {
        var s = At("h02_square", Q("FA02", "accepted"));
        E.DebugClaimRecord(s, "d01_08_unique");
        ReadDialogues(s);
        Assert.That(s.Progress.Flags, Does.Contain("FA02_record_credit"));
        var v = E.QuestViewOf(s, "CH01")!;
        Assert.That(v.Status, Is.EqualTo("available"));
        Assert.That(s.Progress.Flags, Does.Not.Contain(Q("CH01", "accepted")), "不在原地点不自动接取");
        Assert.That(E.AcceptQuest(s, "CH01").Reason, Does.StartWith("须实际到达"));
        Go(s, "c04_alms");
        Assert.That(s.Progress.Flags, Does.Not.Contain(Q("CH01", "accepted")), "到达也不自动接取，须交谈");
        Assert.That(E.NpcsHere(s).Single(c => c.NpcId == "npc_bai_ling").QuestText, Does.Contain("施济清单（可接）"));
        Ok(E.TalkNpc(s, "npc_bai_ling"));
        var offer = s.Story.DialogueLog.Last(d => d.EventId == "CH01_offer");
        ReadDialogues(s);
        Assert.That(s.Progress.Flags, Does.Contain(Q("CH01", "accepted")));
        Assert.That(offer.Lines.Select(l => l.Text), Is.EqualTo(new[]
        {
            "你带回来的路线里记了物资去了哪儿。跟这张公开清单挨行对一下。有缺口就空着，别替谁填。",
            "这几笔对不上。我能查到哪儿，就写到哪儿。",
        }), "原接取台词保留");
        Assert.That(E.QuestViewOf(s, "CH01")!.WorkBlocked, Is.Null);
    }

    /// <summary>
    /// QUEST-Q162-01（旧档已接无档册）：读档后 CH01 仍是进行中、已提交的 5 分钟保留；工作仍须原档册条件，不能跳过；
    /// 领到档册后只续做剩余 5 分钟并完成一次。
    /// </summary>
    [Test]
    public void QUEST_Q162_01_LegacyAccepted_KeepsProgress()
    {
        var old = At("c04_alms", Q("FA02", "accepted"), Q("CH01", "accepted"));
        old.Quests.Of("CH01").WorkMinutes = 5;
        var s = SaveSerializer.Deserialize(SaveSerializer.Serialize(old), Cfg, out _);
        Assert.That(s.Progress.Flags, Does.Contain(Q("CH01", "accepted")), "不删除旧档已接");
        var v = E.QuestViewOf(s, "CH01")!;
        Assert.That((v.Status, v.WorkDone), Is.EqualTo(("active", 5)));
        Assert.That(v.WorkBlocked, Is.EqualTo("需要：在教会地下圣所领取圣所旧路档册"));
        Assert.That(E.StartQuestWork(s, "CH01").Ok, Is.False, "不能跳过档册条件");
        Assert.That(s.Quests.Records["CH01"].WorkMinutes, Is.EqualTo(5));
        E.DebugClaimRecord(s, "d01_08_unique");
        ReadDialogues(s);
        var t0 = s.GameMinute;
        Ok(E.StartQuestWork(s, "CH01"));
        Run(s);
        ReadDialogues(s);
        Assert.That(s.GameMinute, Is.EqualTo(t0 + 5), "只补剩余段，不倒扣、不重置");
        Assert.That(s.Progress.Flags, Does.Contain(Q("CH01", "completed")));
        Assert.That(s.Log.Count(l => l.Text.Contains("施济清单") && l.Text.Contains("完成")), Is.EqualTo(1));
    }

    /// <summary>QUEST-Q162-01（旧档已完成）：没领档册的已完成 CH01 仍显示已完成、可回看；交谈不重播接取、不补发奖励，CH02 照常可接。</summary>
    [Test]
    public void QUEST_Q162_01_LegacyCompleted_NoReplay()
    {
        var s = At("c04_alms", Q("FA02", "accepted"), Q("CH01", "accepted"), Q("CH01", "completed"));
        var v = E.QuestViewOf(s, "CH01")!;
        Assert.That((v.Status, v.CanReview), Is.EqualTo(("done", true)));
        var items = s.Items.Sum(i => i.Count);
        var minute = s.GameMinute;
        Ok(E.TalkNpc(s, "npc_bai_ling"));
        ReadDialogues(s);
        Assert.That(s.Story.DialogueLog.Any(d => d.EventId == "CH01_offer"), Is.False, "不重播接取");
        Assert.That((s.Items.Sum(i => i.Count), s.GameMinute), Is.EqualTo((items, minute)), "不补发奖励、不扣时");
        Assert.That(E.QuestViewOf(s, "CH02")!.Status, Is.EqualTo("available"));
    }
}
