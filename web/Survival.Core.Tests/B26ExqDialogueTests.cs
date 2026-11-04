using Newtonsoft.Json.Linq;
using Survival.Core.Config;
using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 批次 B26（R6 Q217、R1 Q221）。规则来源：策划合同 content-feeding-adjudications.json exqDialogues/dialoguePolicy、
/// narrative-texts.json NTL_Q217_*、expansion-quests.json offerDialogue/turnInDialogue；验收 PLAN-Q217-01、PLAN-Q221-01。
/// </summary>
public class B26ExqDialogueTests : TestBase
{
    private static readonly JArray Lines = (JArray)JObject.Parse(File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "design.content-feeding.json")))["exqDialogues"]!;

    private static JObject Line(string quest) => (JObject)Lines.Single(l => (string)l["questId"]! == quest);

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

    private static DialogueLine? Said(GameState s, string eventId) =>
        s.Story.DialogueLog.Where(d => d.EventId.StartsWith(eventId)).SelectMany(d => d.Lines).LastOrDefault();

    /// <summary>PLAN-Q217-01：四个任务的接取/回报键换成专属组，八句与合同逐字一致，一组一句，发言人是岗位本人。</summary>
    [TestCase("EXQ05")]
    [TestCase("EXQ06")]
    [TestCase("EXQ07")]
    [TestCase("EXQ08")]
    public void Q217_ConfigMatchesContract(string quest)
    {
        var q = Cfg.Quests.List.Single(x => x.Id == quest);
        var l = Line(quest);
        Assert.That((q.OfferDialogue, q.TurninDialogue), Is.EqualTo(((string)l["offerId"]!, (string)l["turninId"]!)));
        Assert.That(q.NpcId, Is.EqualTo((string)l["npcId"]!));
        foreach (var (set, key) in new[] { (q.OfferDialogue!, "offer"), (q.TurninDialogue!, "turnin") })
        {
            var d = Cfg.Story.Dialogues.Where(x => x.Event == set).ToList();
            Assert.That(d.Count, Is.EqualTo(1), set);
            Assert.That((d[0].Speaker, d[0].Text), Is.EqualTo(((string)l["name"]!, (string)l[key]!)));
        }
    }

    /// <summary>通用台词不动：三名岗位的通用组还在、文字未改，其他任务不借用专属句。</summary>
    [Test]
    public void Q217_GenericLinesUntouched()
    {
        foreach (var npc in new[] { "npc_luo_zheng", "npc_xu_an", "npc_chen_zhi" })
            foreach (var st in new[] { "quest_offer", "quest_return" })
            {
                var set = Cfg.City.Npcs.Single(n => n.NpcId == npc).States[st];
                Assert.That(Cfg.Story.Dialogues.Count(d => d.Event == set), Is.EqualTo(1), set);
                Assert.That(Cfg.Quests.List.Where(q => q.Id.StartsWith("EXQ")).Select(q => q.OfferDialogue).Concat(Cfg.Quests.List.Where(q => q.Id.StartsWith("EXQ")).Select(q => q.TurninDialogue)), Does.Not.Contain(set));
            }
        Assert.That(Cfg.Story.Dialogues.Single(d => d.Event == "npc_xu_an_dialogue_quest_offer").Text, Is.EqualTo("你可以分别去教会和科学院问路，不用先选一个效忠。"));
        var others = Cfg.Quests.List.Where(q => !new[] { "EXQ05", "EXQ06", "EXQ07", "EXQ08" }.Contains(q.Id)).SelectMany(q => new[] { q.OfferDialogue, q.TurninDialogue });
        Assert.That(others.Where(x => x != null && x.StartsWith("EXQ")), Is.Empty);
    }

    /// <summary>接取播专属接取句，回报播专属回报句；交谈 0 分钟、不发物资、不给经验、不加容量（台词不是奖励）。</summary>
    [Test]
    public void Q217_AcceptAndTurnin_PlayOwnLines_NoReward()
    {
        var s = Registered("h02_square");
        var t0 = s.GameMinute;
        Assert.That(E.AcceptQuest(s, "EXQ07").Ok, Is.True);
        Assert.That(Said(s, "EXQ07_offer")?.Text, Is.EqualTo((string)Line("EXQ07")["offer"]!));
        Assert.That(Said(s, "npc_xu_an_dialogue_quest_offer"), Is.Null, "不再借通用委托句");
        ReadDialogues(s);
        Assert.That(GameEngine.QuestAccepted(s, "EXQ07"), Is.True);
        s.Progress.Flags.UnionWith(new[] { "claim:d07_07_unique", "campaignId:d07_07_unique", "civilian_cache_knowledge" });
        var snap = (s.Items.Sum(i => i.Count), s.Skills.Sum(kv => kv.Value.Xp), E.BattleCapacity(s));
        Assert.That(E.TalkNpc(s, "npc_xu_an").Ok, Is.True);
        ReadDialogues(s);
        Assert.That(Said(s, "EXQ07_turnin")?.Text, Is.EqualTo((string)Line("EXQ07")["turnin"]!));
        Assert.That(GameEngine.QuestDone(s, "EXQ07"), Is.True);
        Assert.That(s.GameMinute, Is.EqualTo(t0), "0 分钟");
        Assert.That((s.Items.Sum(i => i.Count), s.Skills.Sum(kv => kv.Value.Xp), E.BattleCapacity(s)), Is.EqualTo(snap));
    }

    /// <summary>先取记录后接取：按原任务追认，同一次交谈先接取句再回报句；回看只放对白，不再发东西。</summary>
    [Test]
    public void Q217_RecordFirst_BothLinesQueue_ReviewNoReward()
    {
        var s = Registered("h04_infirmary");
        s.Progress.Flags.UnionWith(new[] { "claim:d08_08_unique", "campaignId:d08_08_unique", "coldchain_knowledge" });
        Assert.That(E.TalkNpc(s, "npc_chen_zhi").Ok, Is.True);
        var order = s.Story.DialogueLog.Select(d => d.EventId).Where(x => x.StartsWith("EXQ08")).ToList();
        Assert.That(order.Count, Is.EqualTo(2));
        Assert.That(order[0], Does.StartWith("EXQ08_offer"));
        Assert.That(order[1], Does.StartWith("EXQ08_turnin"));
        ReadDialogues(s);
        Assert.That(GameEngine.QuestDone(s, "EXQ08"), Is.True);
        var snap = (s.Items.Sum(i => i.Count), s.Skills.Sum(kv => kv.Value.Xp), s.Progress.Flags.Count);
        Assert.That(E.ReviewQuest(s, "EXQ08").Ok, Is.True);
        ReadDialogues(s);
        Assert.That((s.Items.Sum(i => i.Count), s.Skills.Sum(kv => kv.Value.Xp), s.Progress.Flags.Count), Is.EqualTo(snap), "回看不重奖");
    }

    /// <summary>岗位离岗：接取/回报由代班员说同一句（换 speaker，不冒充原人），任务照常完成。</summary>
    [Test]
    public void Q217_Substitute_SpeaksSameLine()
    {
        var s = Registered("h02_square");
        E.DebugToggleCityPostAbsent(s);
        Assert.That(s.City.Absent, Does.Contain("npc_xu_an"));
        var duty = Cfg.City.Npcs.Single(n => n.NpcId == "npc_xu_an").DutySpeaker;
        Assert.That(E.AcceptQuest(s, "EXQ07").Ok, Is.True);
        ReadDialogues(s);   // Q235：代班首次先播一次接续说明，专属句排在后面
        var line = Said(s, "EXQ07_offer")!;
        Assert.That(line.Speaker, Is.EqualTo(duty));
        Assert.That(line.Speaker, Is.Not.EqualTo("许安"));
        Assert.That(line.Text, Is.EqualTo((string)Line("EXQ07")["offer"]!));
        s.Progress.Flags.UnionWith(new[] { "claim:d07_07_unique", "campaignId:d07_07_unique", "civilian_cache_knowledge" });
        Assert.That(E.TalkNpc(s, "npc_xu_an").Ok, Is.True);
        ReadDialogues(s);
        Assert.That(Said(s, "EXQ07_turnin")!.Speaker, Is.EqualTo(duty));
        Assert.That(GameEngine.QuestDone(s, "EXQ07"), Is.True);
    }

    /// <summary>PLAN-Q221-01：只有 d05—d08 四份记录是两段续做；d01—d04 不是，取消不保段。</summary>
    [Test]
    public void Q221_OnlyD05ToD08_Staged()
    {
        var records = Cfg.Resources.Where(r => r.IsRecord && r.LocationId.StartsWith("d0")).ToList();
        Assert.That(records.Where(E.StagedRecord).Select(r => r.NodeId), Is.EquivalentTo(new[] { "d05_07_unique", "d06_08_unique", "d07_07_unique", "d08_08_unique" }));
        Assert.That(records.Where(r => !E.StagedRecord(r)).Select(r => r.NodeId), Is.EquivalentTo(new[] { "d01_08_unique", "d02_08_unique", "d03_08_unique", "d04_07_unique" }));

        var s = Registered("h03_workshop");
        s.GameMinute = At(2, 23, 0);
        E.DebugMapTeleport(s, "d01_08");
        E.DebugSetProgress(s, "d01_08", 100);
        ReadDialogues(s);
        var r0 = E.Preview(s, ActionRequest.Gather("d01_08_unique"));
        Assert.That(r0.Ok, Is.True, r0.Reason);
        Assert.That(r0.Notes, Has.None.Contains("固定等待"));
        Assert.That(r0.EstimatedMinutes, Is.GreaterThan(10), "普通工作吃夜间倍率");
        Start(s, ActionRequest.Gather("d01_08_unique"));
        Assert.That(s.Main!.Label, Does.Not.Contain("固定等待"));
        E.Advance(s);
        Assert.That(E.Cancel(s).Ok, Is.True);
        Assert.That(s.Resources["d01_08_unique"].ClaimMinutesDone, Is.EqualTo(0), "取消不保段");
    }
}
