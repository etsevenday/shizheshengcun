using Newtonsoft.Json.Linq;
using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 批次 B23 M478：可选地牢任务内核（R71；A11「可选地牢任务与知识」§2/§4，开发假设 B23-1—B23-5）。
/// 验收 EXQ-A05（记录 10 分钟两段、与搜集同一事务、中断续剩余）、EXQ-A06（提前拾取/换代/读档不重复首次知识与任务奖励）、
/// EXQ-A07（满物资卡也能取得记录、岗位不在由代班接手）；存档 39→40。
/// </summary>
public class B23ExqQuestTests : TestBase
{
    private void Ok(ActionResult r) => Assert.That(r.Ok, Is.True, r.Reason);

    private static string Q(string id, string kind) => GameEngine.QuestFact(id, kind);

    /// <summary>存档 43 迁移过的已登记旧档：五张蓝图已是永久知识（Q246）。</summary>
    private static void LegacyLearned(GameState s) => s.Progress.Flags.UnionWith(Cfg.City.Registration.LegacyKnowledge.Select(k => "knowledge:" + k));

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

    /// <summary>传送到记录房、探索到 100% 揭示记录卡（守卫事实按需写，不影响记录领取）。</summary>
    private void ToRecord(GameState s, string node)
    {
        E.DebugMapTeleport(s, node);
        E.DebugSetProgress(s, node, 100);
        ReadDialogues(s);
    }

    /// <summary>物资（不含记录房地面——那里是 B22 地牢本身的首通补给包，另一份账）、经验、容量。</summary>
    private (int Items, long Xp, int Cap) Snapshot(GameState s) =>
        (s.Items.Where(i => !i.Owner.StartsWith("d0")).Sum(i => i.Count), s.Skills.Sum(kv => kv.Value.Xp), E.BattleCapacity(s));

    /// <summary>
    /// EXQ05 全流程：h03 与罗铮交谈接取（播专属接取台词）→ d05_07 领取水厂阀控记录（固定 10 分钟，同一事务写记录、日志、知识）→ 任务册“记录已取得”、
    /// 下一步回工坊交谈 → 回 h03 交谈 0 分钟完成（播回报台词）；不发物资、不给经验、不加容量；维护手册蓝图由 claim:d05_07_unique 授予。
    /// </summary>
    [Test]
    public void EXQ05_AcceptClaimReturn_FullFlow()
    {
        var s = Registered("h03_workshop");
        LegacyLearned(s);
        var v = E.QuestViewOf(s, "EXQ05")!;
        Assert.That((v.Status, v.KindLabel, v.Step), Is.EqualTo(("available", "可选任务", "到营区工坊找罗铮，了解水厂调查。")));
        Assert.That(E.QuestBook(s).Select(x => x.Id), Does.Contain("EXQ05").And.Contain("EXQ06"));
        Ok(E.TalkNpc(s, "npc_luo_zheng"));
        Assert.That(s.Story.DialogueLog.Any(d => d.EventId.StartsWith("EXQ05_offer")), Is.True, "专属接取台词（B26 Q217）");
        ReadDialogues(s);
        Assert.That(s.Progress.Flags, Does.Contain(Q("EXQ05", "accepted")));
        v = E.QuestViewOf(s, "EXQ05")!;
        Assert.That((v.Status, v.Step, v.WorkLabel), Is.EqualTo(("active", "从荒废水厂进入废水厂地下管网。到阀控室领取水厂阀控记录（10分钟）。", (string?)null)));
        Assert.That(E.KnowsKnowledge(s, "blueprint_repair_manual"), Is.False);

        var before = Snapshot(s);
        ToRecord(s, "d05_07");
        var t0 = s.GameMinute;
        var p = E.Preview(s, ActionRequest.Gather("d05_07_unique"));
        Assert.That(p.Ok, Is.True, p.Reason);
        Assert.That(p.Notes, Has.Some.Contains("同时获得做法知识：维护手册蓝图"));
        Assert.That(p.Notes, Has.Some.Contains("净水操作套件的做法已掌握，只记来源"));
        Assert.That(p.Notes, Has.Some.EqualTo("可选任务「水从哪里来」：领取后会记为记录已取得，回营区工坊和罗铮交谈即可回报（0分钟）。"));
        Assert.That(p.Notes, Has.Some.Contains("固定等待10分钟，按两段5分钟提交"));
        Start(s, ActionRequest.Gather("d05_07_unique"));
        Run(s);
        Assert.That(s.GameMinute - t0, Is.EqualTo(10), "固定 10 分钟，不另叠任务工时");
        Assert.That(s.Progress.Flags, Does.Contain("claim:d05_07_unique").And.Contain("waterworks_record"));
        Assert.That(E.KnowsKnowledge(s, "blueprint_repair_manual"), Is.True);
        var log = s.Log.Last(l => l.Text.Contains("水厂阀控记录")).Text;
        Assert.That(log, Does.Contain("获得做法知识：维护手册蓝图。净水操作套件的做法已掌握，只记来源").And.Contain("可选任务「水从哪里来」的记录已取得。回营区工坊和罗铮交谈即可回报（0分钟）。"));
        v = E.QuestViewOf(s, "EXQ05")!;
        Assert.That((v.Status, v.StatusText, v.Step), Is.EqualTo(("active", "记录已取得", "回营区工坊和罗铮交谈回报（0分钟，不交材料）。")));
        Assert.That(v.WorkLabel, Is.EqualTo("交谈（0分钟）"));
        Assert.That(v.WorkBlocked, Is.EqualTo("须实际到达营区工坊"));

        E.DebugMapTeleport(s, "h03_workshop");
        ReadDialogues(s);
        var t1 = s.GameMinute;
        Ok(E.TalkNpc(s, "npc_luo_zheng"));
        Assert.That(s.Story.DialogueLog.Any(d => d.EventId.StartsWith("EXQ05_turnin")), Is.True, "专属回报台词（B26 Q217）");
        ReadDialogues(s);
        Assert.That(s.Progress.Flags, Does.Contain(Q("EXQ05", "completed")));
        Assert.That(s.GameMinute, Is.EqualTo(t1), "交谈 0 分钟");
        Assert.That(s.Log.Any(l => l.Text.StartsWith("可选任务“水从哪里来”完成：水厂阀控记录已归档。")), Is.True);
        Assert.That(s.Log.Last(l => l.Text.StartsWith("可选任务“水从哪里来”完成：")).Text, Does.Not.Contain("。。").And.Not.Contain("。；"), "B38：N02 结果句自带句号，拼接不重复标点");
        var after = Snapshot(s);
        Assert.That((after.Items, after.Xp, after.Cap), Is.EqualTo((before.Items, before.Xp, before.Cap)), "不发物资、不给经验、不加容量");
        Assert.That(E.QuestViewOf(s, "EXQ05")!.Status, Is.EqualTo("done"));
    }

    /// <summary>任务条目里的“交谈（0分钟）”与岗位交谈同效（在工坊时）。</summary>
    [Test]
    public void EXQ06_TurninFromQuestEntry()
    {
        var s = Registered("h03_workshop");
        Ok(E.AcceptQuest(s, "EXQ06"));
        ReadDialogues(s);
        Assert.That(E.StartQuestWork(s, "EXQ06").Ok, Is.False, "没有记录不能回报");
        s.Progress.Flags.UnionWith(new[] { "claim:d06_08_unique", "campaignId:d06_08_unique", "mining_blueprint_pack" });
        var v = E.QuestViewOf(s, "EXQ06")!;
        Assert.That((v.StatusText, v.WorkLabel, v.WorkBlocked), Is.EqualTo(("记录已取得", "交谈（0分钟）", (string?)null)));
        Ok(E.StartQuestWork(s, "EXQ06"));
        ReadDialogues(s);
        Assert.That(GameEngine.QuestDone(s, "EXQ06"), Is.True);
    }

    /// <summary>
    /// EXQ-A05：记录领取固定 10 分钟两段；第一段后停下保留 5 分钟，续做只要 5 分钟（夜间不拉长，不吃工作倍率）；读档后进度仍在；
    /// 一共只花 10 分钟世界时间完成记录（不是 10+10）。
    /// </summary>
    [Test]
    public void EXQ_A05_TwoSegments_ResumeRemaining_NoDoubleTime()
    {
        var s = Registered("h03_workshop");
        s.GameMinute = At(2, 23, 0);   // 夜间：普通工作 ×1.5，固定等待不受影响
        ToRecord(s, "d08_08");
        var t0 = s.GameMinute;
        Start(s, ActionRequest.Gather("d08_08_unique"));
        Assert.That(s.Main!.Label, Does.Contain("固定等待10分钟"));
        Steps(s, 1);
        Ok(E.Cancel(s));
        Assert.That(s.Resources["d08_08_unique"].ClaimMinutesDone, Is.EqualTo(5));
        var cancel = s.Log.Last(l => l.Kind == GameEventKind.ActionCancelled.ToString()).Text;
        Assert.That(cancel, Does.StartWith("已取消领取，未获得产物").And.EndWith("冷链调度档案已保存完成的5分钟，下次只需续做剩余5分钟。"), "试玩修复：保留的段并进取消提示");
        Assert.That(s.Log.Count(l => l.Text.Contains("已保存完成的5分钟")), Is.EqualTo(1), "日志不重复");
        Assert.That(E.StagedLeft(s, Cfg.Resources.Single(r => r.NodeId == "d08_08_unique")), Is.EqualTo(5), "卡面按钮写剩余分钟");
        Assert.That(E.CampaignClaimed(s, "d08_08_unique"), Is.False, "只提交一段，不算领取");
        Assert.That(E.KnowsKnowledge(s, "blueprint_ration_pouch"), Is.False);

        var m = SaveSerializer.Deserialize(SaveSerializer.Serialize(s), Cfg);
        Assert.That(m.Resources["d08_08_unique"].ClaimMinutesDone, Is.EqualTo(5), "读档保留已提交段");
        var p = E.Preview(m, ActionRequest.Gather("d08_08_unique"));
        Assert.That(p.Notes, Has.Some.EqualTo("已完成5分钟，续做剩余5分钟"));
        Start(m, ActionRequest.Gather("d08_08_unique"));
        Assert.That(m.Main!.Label, Does.Contain("固定等待5分钟"));
        Run(m);
        Assert.That(m.GameMinute - t0, Is.EqualTo(10), "两段合计 10 分钟世界时间");
        Assert.That(E.CampaignClaimed(m, "d08_08_unique"), Is.True);
        Assert.That(E.KnowsKnowledge(m, "blueprint_ration_pouch"), Is.True);
        Assert.That(m.Resources["d08_08_unique"].ClaimMinutesDone, Is.EqualTo(0), "领取完成清零");
    }

    /// <summary>
    /// EXQ-A06（提前拾取）：先领记录（还没接任务）——知识当场授予、日志写“接取时即完成”；之后与罗铮交谈接取即追认完成（同一次交谈），
    /// 不要求再进一次、不重复授知识或发物资。
    /// </summary>
    [Test]
    public void EXQ_A06_ClaimBeforeAccept_RetroCompleteOnAccept()
    {
        var s = Registered("h03_workshop");
        LegacyLearned(s);
        Ok(E.TalkNpc(s, "npc_luo_zheng"));   // 首次见面接取 EXQ05
        ReadDialogues(s);
        ToRecord(s, "d06_08");
        var p = E.Preview(s, ActionRequest.Gather("d06_08_unique"));
        Assert.That(p.Notes, Has.Some.EqualTo("知识已知，本次用于保存调查记录"), "已登记旧档两条衣物知识已是永久知识（Q246）");
        Assert.That(p.Notes, Has.Some.EqualTo("同时获得做法知识：基础陶工做法"), "矿道手册原有的同授知识照旧");
        Start(s, ActionRequest.Gather("d06_08_unique"));
        Run(s);
        Assert.That(s.Log.Last(l => l.Text.Contains("矿道作业手册")).Text, Does.Contain("知识已知，本次用于保存调查记录")
            .And.Contain("可选任务「矿道里的旧工具」的记录已保存。到营区工坊和罗铮交谈接取即可完成（0分钟）。"));
        var items = s.Items.Sum(i => i.Count);
        E.DebugMapTeleport(s, "h03_workshop");
        ReadDialogues(s);
        var pre = E.QuestViewOf(s, "EXQ06")!;
        Assert.That((pre.Status, pre.StatusText, pre.Step), Is.EqualTo(("available", "可接 · 记录已取得", "记录已保存，在营区工坊和罗铮交谈接取时即完成，不用再进一次。")), "试玩修复：接取前写明记录已取得");
        Ok(E.TalkNpc(s, "npc_luo_zheng"));   // 接取 EXQ06 → 记录已在手 → 追认完成
        ReadDialogues(s);
        Assert.That(GameEngine.QuestDone(s, "EXQ06"), Is.True);
        Assert.That(GameEngine.QuestDone(s, "EXQ05"), Is.False);
        Assert.That(s.Items.Sum(i => i.Count), Is.EqualTo(items));
        Assert.That(s.Log.Count(l => l.Text.StartsWith("可选任务“矿道里的旧工具”完成")), Is.EqualTo(1));
        Assert.That(s.Log.Any(l => l.Text.StartsWith("可选任务“矿道里的旧工具”已接取")), Is.False, "追认完成时只记完成那条");
    }

    /// <summary>
    /// EXQ-A06（换代/重复回报/读档）：完成后地牢换代再核对——只核对、不再授知识、不再写任务；再次交谈不重复完成；
    /// 读档往返后任务仍是已完成、知识仍在。
    /// </summary>
    [Test]
    public void EXQ_A06_ResetRecheck_NoSecondReward()
    {
        var s = Registered("h04_infirmary");
        Ok(E.AcceptQuest(s, "EXQ08"));
        ReadDialogues(s);
        ToRecord(s, "d08_08");
        Start(s, ActionRequest.Gather("d08_08_unique"));
        Run(s);
        E.DebugMapTeleport(s, "h04_infirmary");
        ReadDialogues(s);
        Ok(E.TalkNpc(s, "npc_chen_zhi"));
        ReadDialogues(s);
        Assert.That(GameEngine.QuestDone(s, "EXQ08"), Is.True);
        var flags = s.Progress.Flags.Count;
        var items = s.Items.Count(i => !i.ItemId.StartsWith("x"));
        // 换代后再进核对
        E.DebugExpedition(s, "end");
        Assert.That(E.NewDungeonGeneration(s, "d08"), Is.Null);
        ToRecord(s, "d08_08");
        Assert.That(E.Preview(s, ActionRequest.Gather("d08_08_unique")).Notes, Has.Some.EqualTo("「冷链调度档案」已在本局记录里。这次只核对本代记录，完成后开放本代出口。"));

        Start(s, ActionRequest.Gather("d08_08_unique"));
        Run(s);
        Assert.That(s.Log.Last(l => l.Text.Contains("冷链调度档案")).Text, Does.StartWith("已核对本代的「冷链调度档案」（本局已领）。"));
        // 试玩修复：本代已核对（按钮灰）时，夜间右栏也不写工作倍率行
        s.GameMinute = At(9, 23, 0);
        var greyed = E.Preview(s, ActionRequest.Gather("d08_08_unique"));
        Assert.That(greyed.Ok, Is.False);
        Assert.That(greyed.Notes.Any(n => n.StartsWith("当前工作倍率")), Is.False);
        Assert.That(s.Log.Count(l => l.Text.Contains("获得做法知识：配给袋蓝图")), Is.EqualTo(1));
        E.DebugMapTeleport(s, "h04_infirmary");
        ReadDialogues(s);
        Ok(E.TalkNpc(s, "npc_chen_zhi"));
        ReadDialogues(s);
        Assert.That(s.Log.Count(l => l.Text.StartsWith("可选任务“封存不是永存”完成")), Is.EqualTo(1), "重复回报不重复完成");
        var m = SaveSerializer.Deserialize(SaveSerializer.Serialize(s), Cfg);
        Assert.That((GameEngine.QuestDone(m, "EXQ08"), E.KnowsKnowledge(m, "blueprint_ration_pouch")), Is.EqualTo((true, true)));
    }

    /// <summary>EXQ-A07：随身五卡已满也能领取记录（记录不占物资卡、0 重量）；岗位人物不在时由机构当班接续员接手回报，任务不卡。</summary>
    [Test]
    public void EXQ_A07_FullCards_AbsentPost_StillCompletes()
    {
        var s = Registered("h02_square");
        Ok(E.AcceptQuest(s, "EXQ07"));
        ReadDialogues(s);
        foreach (var id in new[] { "stone", "wood", "fiber", "cloth", "thread", "leather" }) E.DebugGive(s, id, 1);
        Assert.That(E.RootCardCount(s), Is.GreaterThanOrEqualTo(Cfg.Carry.MaxRootCards));
        ToRecord(s, "d07_07");
        var cards = s.Items.Count(i => i.IsCarried);
        Start(s, ActionRequest.Gather("d07_07_unique"));
        Run(s);
        Assert.That(E.CampaignClaimed(s, "d07_07_unique"), Is.True);
        Assert.That(s.Items.Count(i => i.IsCarried), Is.EqualTo(cards), "记录不是物资卡");
        E.DebugMapTeleport(s, "h02_square");
        ReadDialogues(s);
        E.DebugToggleCityPostAbsent(s);   // 许安离岗，代班员接手
        Ok(E.TalkNpc(s, "npc_xu_an"));
        Assert.That(s.Story.DialogueLog.Any(d => d.EventId.StartsWith("npc_xu_an_dialogue_institution_substitute")), Is.True);
        ReadDialogues(s);
        Assert.That(GameEngine.QuestDone(s, "EXQ07"), Is.True);
    }

    /// <summary>未登记时不显示、不能接；先领的记录在登记后接取即完成（知识先到，读档按授予事实判断）。</summary>
    [Test]
    public void BeforeRegistration_Hidden_ThenAcceptCompletes()
    {
        var s = New();
        s.SpawnNextCheck = int.MaxValue;
        s.PatrolNextCheck = int.MaxValue;
        ToRecord(s, "d05_07");
        Start(s, ActionRequest.Gather("d05_07_unique"));
        Run(s);
        Assert.That(E.QuestViewOf(s, "EXQ05"), Is.Null);
        Assert.That(s.Log.Last(l => l.Text.Contains("水厂阀控记录")).Text, Does.Contain("的记录已保存。营区登记后，到营区工坊和罗铮交谈接取即可完成（0分钟）。"));
        s.Progress.Flags.UnionWith(new[] { "registration_authorized", "hub_registered" });
        E.DebugMapTeleport(s, "h03_workshop");
        ReadDialogues(s);
        Assert.That(E.QuestViewOf(s, "EXQ05")!.Status, Is.EqualTo("available"));
        Ok(E.AcceptQuest(s, "EXQ05"));
        ReadDialogues(s);
        Assert.That(GameEngine.QuestDone(s, "EXQ05"), Is.True);
    }

    /// <summary>存档 39→40：资源点补“已提交分钟”0、写一条升级日志；已领 d05 记录的旧档读档即会对应知识，不补物资、经验或任务完成。</summary>
    [Test]
    public void Migration39To40_KnowledgeByFacts_NoGrants()
    {
        var s = Registered("h03_workshop");
        s.Progress.Flags.UnionWith(new[] { "campaignId:d05_07_unique", "claim:d05_07_unique", "waterworks_record" });
        var j = JObject.Parse(SaveSerializer.Serialize(s));
        j["SchemaVersion"] = "39";
        foreach (var r in ((JObject)j["Resources"]!).Properties()) ((JObject)r.Value).Remove("ClaimMinutesDone");
        var m = SaveSerializer.Deserialize(j.ToString(), Cfg, out _);
        Assert.That(m.SchemaVersion, Is.EqualTo(GameState.CurrentSchema));   // 链式升到当前版本
        Assert.That(m.Log.Count(l => l.Text.Contains("存档已升级到版本40")), Is.EqualTo(1));
        Assert.That(E.KnowsKnowledge(m, "blueprint_repair_manual"), Is.True);
        Assert.That(GameEngine.QuestDone(m, "EXQ05"), Is.False);
        Assert.That(m.Items.Count, Is.EqualTo(s.Items.Count));
        Assert.That(m.Resources.Values.All(r => r.ClaimMinutesDone == 0), Is.True);
        var again = SaveSerializer.Deserialize(SaveSerializer.Serialize(m), Cfg);
        Assert.That(again.Log.Count(l => l.Text.Contains("存档已升级到版本40")), Is.EqualTo(1), "迁移只一次");
    }
}
