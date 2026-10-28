using Newtonsoft.Json.Linq;
using Survival.Core.Config;
using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 策划提交 9fdb44f Q10/Q12：读完对话才能操作、对话在安全边界等待、按次累计展示与对话记录持久化、schema 6→7。
/// 验收 S01-A08/A09、R03-A08、S14-A37（内核拒绝部分）、S14-A39（内核部分）。
/// </summary>
public class V10DialogueTests : BattleTestBase
{
    private const string Sub = "active_substance_white_unidentified";

    [Test]
    public void Opening_AccumulatesLines_EndRequired_NoTimeNoSkip()
    {
        var s = NewStory();
        var m0 = s.GameMinute;
        var d = E.ActiveDialogue(s)!;
        Assert.That(E.DialogueLocked(s), Is.True);
        Assert.That(d.EventId, Is.EqualTo("opening"));
        Assert.That(d.Counterpart, Is.EqualTo("饱饱"));
        Assert.That((d.Minute, d.LocationId), Is.EqualTo(((int?)m0, "camp")), "首句展示时登记游戏内时间与地点");
        Assert.That(d.Lines.Select(l => l.LineId), Is.EqualTo(new[] { "awakening_1" }), "只展示首句，不暴露后续");
        Assert.That(d.Lines[0].Side, Is.EqualTo(DialogueSide.Right), "饱饱恒在右侧");
        Assert.That(d.Lines[0].Note, Does.Contain("临时营地"));
        Assert.That(E.DialogueHasMore(s), Is.True);
        Assert.That(E.DialogueHistory(s), Is.Empty, "未结束的对话不进记录列表");

        // 错句/双击：不跳过未读句
        Assert.That(E.AckDialogue(s, "food_water").Duplicate, Is.True);
        Assert.That(E.AckDialogue(s, "awakening_1").Ok, Is.True);
        Assert.That(E.AckDialogue(s, "awakening_1").Duplicate, Is.True, "重复点击只推进一次");
        Assert.That(d.Lines.Select(l => l.LineId), Is.EqualTo(new[] { "awakening_1", "awakening_2" }), "追加到同一对话流，已展示的不被替换");
        Assert.That(E.AckDialogue(s, "awakening_2").Ok, Is.True);
        Assert.That(E.DialogueHasMore(s), Is.False, "末句：按钮为“结束对话”");
        Assert.That(E.DialogueLocked(s), Is.True, "末句展示后仍锁定，明确结束才解除");
        Assert.That(E.AckDialogue(s, "food_water").Ok, Is.True);
        Assert.That(E.DialogueLocked(s), Is.False);
        Assert.That(d.Ended, Is.True);
        Assert.That(E.DialogueHistory(s).Single().Lines.Select(l => (l.Order, l.Speaker)), Is.EqualTo(new[] { (1, "饱饱"), (2, "饱饱"), (3, "饱饱") }));
        Assert.That(s.GameMinute, Is.EqualTo(m0), "对话不推进时间");
        Assert.That(s.Story.PendingDialogues, Is.Empty);
        Assert.That(s.Story.SeenDialogues, Is.EqualTo(new[] { "awakening_1", "awakening_2", "food_water" }));
    }

    /// <summary>S14-A37 内核部分：对话期间开始/取消/继续行动、即时操作、接敌、战斗指令、换武器都被拒绝且状态不变。</summary>
    [Test]
    public void S14_A37_InputLockRejectsGameplay_StateUnchanged()
    {
        var s = NewStory();
        var json = SaveSerializer.Serialize(s);
        var r = E.Start(s, ActionRequest.Wait(30));
        Assert.That(r.Ok, Is.False);
        Assert.That(r.Reason, Is.EqualTo(GameEngine.DialogueLockedReason));
        Assert.That(E.CanStart(s, ActionRequest.Explore()), Is.EqualTo(GameEngine.DialogueLockedReason));
        Assert.That(E.DoInstant(s, InstantRequest.Pickup(First(s, "wood", "camp").InstanceId, 1)).Ok, Is.False);
        Assert.That(E.Cancel(s).Ok, Is.False);
        Assert.That(E.Resume(s).Ok, Is.False);
        Assert.That(E.Engage(s).Ok, Is.False);
        Assert.That(E.EquipWeapon(s, null).Ok, Is.False);
        Assert.That(E.Advance(s).Status, Is.EqualTo(SegmentStatus.Dialogue));
        Assert.That(SaveSerializer.Serialize(s), Is.EqualTo(json), "被拒绝的请求不改任何状态、不 tick");
    }

    /// <summary>S01-A08：行动在安全边界等待对白——融合事务已提交，5 分钟段读完才开始；读完从下一推进点继续，不补 tick、不重复结算。</summary>
    [Test]
    public void S01_A08_ActionWaitsAtSafeBoundary_ResumesWithoutReplay()
    {
        var s = NewStory(9);
        ReadDialogues(s);
        var inst = E.CreateItems(s, Sub, 1, ItemInstance.Carried, false, new ItemSource { GrantEventId = "g", TutorialReward = true }).Single();
        s.Story.TutorialWon = true;
        var m0 = s.GameMinute;
        Assert.That(E.Start(s, ActionRequest.Fuse(inst.InstanceId)).Ok, Is.True);
        var main = s.Main!;
        var traits = s.Traits.Count;
        Assert.That(E.DialogueLocked(s), Is.True, "融合对白出现");
        for (var k = 0; k < 3; k++) Assert.That(E.Advance(s).Status, Is.EqualTo(SegmentStatus.Dialogue));
        Assert.That(s.GameMinute, Is.EqualTo(m0), "等待不 tick");
        Assert.That(s.Main, Is.SameAs(main), "原行动保留，不取消、不重启");
        Assert.That(main.ElapsedMinutes, Is.EqualTo(0));
        ReadDialogues(s);
        Assert.That(Run(s), Is.EqualTo(SegmentStatus.Completed));
        Assert.That(s.GameMinute - m0, Is.EqualTo(5), "读完后只推进原定的一段");
        Assert.That(s.Traits, Has.Count.EqualTo(traits), "不重复结算融合");
        Assert.That(E.DialogueHistory(s).Last().EventId, Is.EqualTo("fusion_done"));
    }

    /// <summary>R03-A08：首战建立后先展示警告对白；读完前不下指令、不自动行动/tick；读档恢复同一战斗，读完后才按原规则继续。</summary>
    [Test]
    public void R03_A08_TutorialBattleWaitsForWarning_ThenSameBattleContinues()
    {
        var s = NewStory(3);
        ReadDialogues(s);
        Start(s, ActionRequest.Travel("field"));
        for (var i = 0; i < 200 && s.Main != null; i++) { var r = E.Advance(s); if (r.Status == SegmentStatus.Paused) E.Resume(s); }
        Assert.That(s.Battle, Is.Not.Null);
        Assert.That(E.ActiveDialogue(s)!.EventId, Is.EqualTo("tutorial_arrive"));
        var b = s.Battle!;
        var snap = (b.BattleId, b.Round, b.Cursor, b.PlayerAp, s.GameMinute, s.Rng.State, b.Queue.ToList());
        for (var k = 0; k < 5; k++) Assert.That(E.BattleStep(s).Kind, Is.EqualTo(BattleStepKind.Dialogue));
        Assert.That(E.ExecuteCommand(s, BattleCommand.Of("end_turn")).Ok, Is.False);
        Assert.That(E.CommandBlocked(s, BattleCommand.Of("basic_attack", b.Enemies[0].Index)), Is.EqualTo(GameEngine.DialogueLockedReason));
        Assert.That((b.BattleId, b.Round, b.Cursor, b.PlayerAp, s.GameMinute, s.Rng.State, b.Queue.ToList()), Is.EqualTo(snap), "不扣AP、不执行敌行动、不推进轮末、不抽随机");

        // 对话中读档：恢复同一战斗与同一句
        var back = SaveSerializer.Deserialize(SaveSerializer.Serialize(s), Cfg, out _);
        Assert.That(back.Battle!.BattleId, Is.EqualTo(b.BattleId));
        Assert.That(E.CurrentDialogueLine(back)!.LineId, Is.EqualTo("tutorial_arrive"));
        Assert.That(E.BattleStep(back).Kind, Is.EqualTo(BattleStepKind.Dialogue));
        ReadDialogues(back);
        Assert.That(back.Battle, Is.Not.Null, "结束对话不脱战");
        Assert.That(back.Battle!.Round, Is.EqualTo(b.Round));
        var progressed = false;
        for (var i = 0; i < 20 && back.Battle != null && !E.AwaitingPlayer(back); i++) { E.BattleStep(back); progressed = true; }
        Assert.That(progressed || E.AwaitingPlayer(back), Is.True, "读完后按原队列继续");
        if (E.AwaitingPlayer(back)) Assert.That(E.CommandBlocked(back, BattleCommand.Of("end_turn")), Is.Null, "读完才接受战斗指令");
    }

    /// <summary>S01-A09/S14-A39：中途读档恢复游标与已展示流；不重复入库；后续改文案不倒改历史；新局清空记录。</summary>
    [Test]
    public void S01_A09_PersistMidDialogue_Dedup_SnapshotText_NewGameResets()
    {
        var s = NewStory();
        E.AckDialogue(s, "awakening_1");
        var json = SaveSerializer.Serialize(s);
        var back = SaveSerializer.Deserialize(json, Cfg, out _);
        var d = E.ActiveDialogue(back)!;
        Assert.That(d.Lines.Select(l => l.LineId), Is.EqualTo(new[] { "awakening_1", "awakening_2" }), "已展示流恢复");
        Assert.That(d.Pending, Is.EqualTo(new[] { "food_water" }), "未读的仍是未读，不标成读完");
        Assert.That(E.CurrentDialogueLine(back)!.LineId, Is.EqualTo("awakening_2"));
        Assert.That(back.Story.SeenDialogues, Is.EqualTo(new[] { "awakening_1" }));
        Assert.That(back.Story.DialogueLog, Has.Count.EqualTo(1), "读档不重复入库");

        // 只改后续台词文案：已展示并归档的历史正文不倒改
        var cfg2 = ConfigLoader.FromJson(File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "config.v0.6.json")));
        cfg2.Story.Dialogues.Single(x => x.Id == "awakening_1").Text = "（改过的文案）";
        var e2 = new GameEngine(cfg2);
        var back2 = SaveSerializer.Deserialize(json, cfg2, out _);
        for (var k = 0; k < 5 && e2.DialogueLocked(back2); k++) e2.AckDialogue(back2, e2.CurrentDialogueLine(back2)!.LineId);
        Assert.That(e2.DialogueHistory(back2).Single().Lines[0].Text, Is.EqualTo("醒醒……醒醒，我饿了。"));

        // 新游戏覆盖：记录重置
        var fresh = NewStory();
        Assert.That(fresh.Story.DialogueLog, Has.Count.EqualTo(1));
        Assert.That(fresh.Story.DialogueLog[0].Lines, Has.Count.EqualTo(1));
        // 旧档（Legacy）：没有对话、不补剧情、不锁
        var legacy = New();
        Assert.That(E.DialogueLocked(legacy), Is.False);
        Assert.That(legacy.Story.DialogueLog, Is.Empty);
    }

    /// <summary>schema 6→7（开发假设 A1）：已读台词按配置顺序导入为一组“旧存档记录”（时间地点未知）；未读的建成待读对话并展示首句；不补剧情。</summary>
    [Test]
    public void Schema6To7_ImportsSeenAsLegacyRecord_PendingStaysUnread()
    {
        var s = NewStory();
        ReadDialogues(s);
        // 构造 schema 6 的形状：没有 DialogueLog；awakening_1/2 已读，food_water 未读
        var j = JObject.Parse(SaveSerializer.Serialize(s));
        j["SchemaVersion"] = "6";
        var story = (JObject)j["Story"]!;
        story.Remove("DialogueLog");
        story.Remove("NextDialogueSessionId");
        story["SeenDialogues"] = new JArray("awakening_2", "awakening_1");
        story["PendingDialogues"] = new JArray("food_water");
        story["FoodWaterRead"] = false;
        var json6 = j.ToString();
        Assert.That(SaveSerializer.NeedsMigration(json6), Is.True);
        var m = SaveSerializer.Deserialize(json6, Cfg, out var notes);
        Assert.That(m.SchemaVersion, Is.EqualTo(GameState.CurrentSchema), "6→7→8 整条链");
        var legacy = m.Story.DialogueLog[0];
        Assert.That(legacy.Legacy && legacy.Ended, Is.True);
        Assert.That(legacy.Lines.Select(l => l.LineId), Is.EqualTo(new[] { "awakening_1", "awakening_2" }), "按配置顺序");
        Assert.That(legacy.Minute, Is.Null, "时间未知");
        Assert.That(legacy.LocationId, Is.Null, "地点未知");
        Assert.That(legacy.Lines.All(l => l.Minute == null), Is.True);
        Assert.That(notes.Any(n => n.Contains("旧存档记录")), Is.True);
        var pend = E.ActiveDialogue(m)!;
        Assert.That(pend.EventId, Is.EqualTo("opening"));
        Assert.That(E.CurrentDialogueLine(m)!.LineId, Is.EqualTo("food_water"), "未读的仍待读，读档后展示");
        Assert.That(m.Story.FoodWaterRead, Is.False);
        ReadDialogues(m);
        Assert.That(m.Story.FoodWaterRead, Is.True);
        Assert.That(E.DialogueHistory(m), Has.Count.EqualTo(2));
        Assert.That(m.Items.Count, Is.EqualTo(s.Items.Count), "不补发物品");
        // 再存再读：幂等
        var again = SaveSerializer.Deserialize(SaveSerializer.Serialize(m), Cfg, out var notes2);
        Assert.That(notes2, Is.Empty);
        Assert.That(again.Story.DialogueLog, Has.Count.EqualTo(2));

        // 旧档 Legacy（schema 6 没有剧情）：空记录、不锁
        var old = JObject.Parse(SaveSerializer.Serialize(New()));
        old["SchemaVersion"] = "6";
        ((JObject)old["Story"]!).Remove("DialogueLog");
        var lm = SaveSerializer.Deserialize(old.ToString(), Cfg, out _);
        Assert.That(lm.Story.DialogueLog, Is.Empty);
        Assert.That(E.DialogueLocked(lm), Is.False);
        Assert.That(lm.Story.Legacy, Is.True, "不解除 Legacy 边界");
    }
}
