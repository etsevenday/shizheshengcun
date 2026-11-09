using Newtonsoft.Json.Linq;
using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 批次 B15（R46 主线 FA01—FA05 与容量 C2→C4、h03 战术训练；R47 城市支线与命名队友招募任务）。
/// 规则来源：A11「4 主线任务骨架修订与容量」「5 支线委托最小表」「7 招募与补充任务表」、A08 门槛规则 6/7、S15「gate事实接口」、R03 §3、
/// narrative-texts.json（8 支线与主线场景）。验收：N01 FN02—FN08、R08 PT01/PT07、A11 CITY-N05/N06、S15 TXT05—TXT09 中本批能做的部分。
/// </summary>
public class B15QuestTests : TestBase
{
    private void Ok(ActionResult r) => Assert.That(r.Ok, Is.True, r.Reason);

    private GameState At(string loc, params string[] facts)
    {
        var s = New();
        s.Progress.Flags.UnionWith(facts);
        E.DebugMapTeleport(s, loc);
        ReadDialogues(s);
        return s;
    }

    private void Travel(GameState s, string to)
    {
        Start(s, ActionRequest.Travel(to));
        Run(s);
        ReadDialogues(s);
        Assert.That(s.CurrentLocation, Is.EqualTo(to));
    }

    private static string Q(string id, string kind) => GameEngine.QuestFact(id, kind);

    private static readonly string[] Registered = { "registration_authorized", "hub_registered" };

    // ───────────── 主线 FA01 ─────────────

    /// <summary>FA01：营门登记→真实走到营区广场的同一次到达里播“营区之外”公开说明、写接取与完成；0 分钟、不发物；再来不重播（A11 §4、FN07）。</summary>
    [Test]
    public void FA01_PublicBriefOnRegistrationArrival()
    {
        var s = At("h01_gate");
        Ok(E.StartCityService(s, "hub_registration_existing"));
        ReadDialogues(s);
        var items = s.Items.Sum(i => i.Count);
        Start(s, ActionRequest.Travel("h02_square"));
        Run(s);
        Assert.That(s.Progress.Flags, Does.Contain("hub_registered"));
        Assert.That(s.Story.DialogueLog.Any(d => d.EventId == "FA01_public_brief"), Is.True, "到达即播（at(h02_square) && hub_registered）");
        var minute = s.GameMinute;
        ReadDialogues(s);
        Assert.That(s.GameMinute, Is.EqualTo(minute), "对话 0 分钟");
        Assert.That(s.Progress.Flags, Is.SupersetOf(new[] { Q("FA01", "accepted"), Q("FA01", "completed") }));
        Assert.That(s.Items.Sum(i => i.Count), Is.EqualTo(items), "不发第二份登记物资");
        Assert.That(s.Log.Any(l => l.Text.Contains("营区之外") && l.Text.Contains("两方接触线索")), Is.True);
        Assert.That(s.Progress.Flags.Any(GameEngine.IsMembershipFact), Is.False, "FN02：不写会籍");
        Travel(s, "h01_gate");
        Travel(s, "h02_square");
        Assert.That(s.Story.DialogueLog.Count(d => d.EventId.StartsWith("FA01_public_brief")), Is.EqualTo(1), "不重播");
        var v = E.QuestViewOf(s, "FA01")!;
        Assert.That((v.Status, v.Title, v.KindLabel), Is.EqualTo(("done", "营区之外", "主线")));
        Assert.That(E.QuestViewOf(s, "FA02")!.Status, Is.EqualTo("available"), "两个方向随即可接");
        Assert.That(E.QuestViewOf(s, "FA03")!.Status, Is.EqualTo("available"));
    }

    // ───────────── FA02 / FA03 接待（A08 规则 6、TXT09、FN03） ─────────────

    /// <summary>FA02：c02 两个选择（接受调查/拒绝宣誓）都写同一个 FA02_route_briefed 与接取，重判门槛开 FA02 两条边；再问只回看不再写；不写会籍。</summary>
    [TestCase("FA02_accept_investigation")]
    [TestCase("FA02_accept_no_oath")]
    public void FA02_ReceptionChoices_SameRouteFact(string choice)
    {
        var s = At("c02_church", Registered);
        var v = E.CityServicePreview(s, "church_reception_existing")!;
        Assert.That(v.Implemented, Is.True);
        Assert.That(v.Blocked, Is.Null);
        Assert.That(v.Choices.Select(c => c.Label), Is.EqualTo(new[] { "接受公开路线调查，不作身份承诺", "拒绝宣誓，仍领取公开路线" }));
        Assert.That(E.StartCityService(s, "church_reception_existing").Ok, Is.False, "先选一项");
        var minute = s.GameMinute;
        Ok(E.StartCityService(s, "church_reception_existing", null, choice));
        Assert.That(s.GameMinute, Is.EqualTo(minute), "0 分钟");
        Assert.That(s.Story.DialogueLog.Any(d => d.EventId == "FA02_route_choice"), Is.True);
        ReadDialogues(s);
        Assert.That(s.Progress.Flags, Is.SupersetOf(new[] { "FA02_route_briefed", Q("FA02", "accepted") }));
        Assert.That(s.Progress.Flags, Does.Not.Contain(Q("FA02", "completed")), "领路线不等于完成主线");
        Assert.That(s.Progress.Flags.Any(GameEngine.IsMembershipFact), Is.False);
        Assert.That(s.Quests.Records["FA02"].Choice, Is.EqualTo(choice));
        foreach (var edge in new[] { "c05_admonition__c06_archive", "c03_crypt__d01_01" })
            Assert.That(s.UnlockedEdges, Does.Contain(edge), $"{edge}：FA02_accept_no_oath / FA02_public_route");
        Assert.That(E.CityServicePreview(s, "church_reception_existing")!.Blocked, Does.Contain("已领取公开路线"));
        Assert.That(E.QuestViewOf(s, "FA02")!.Status, Is.EqualTo("active"));
    }

    /// <summary>FA03：r04 只调查环境/拒绝检测同写 FA03_environment_briefed（不要求身体检测），开 r06→r07 与 r03→d02。</summary>
    [Test]
    public void FA03_ReceptionDeclineTest_StillGetsRoute()
    {
        var s = At("r04_reception", Registered);
        var v = E.CityServicePreview(s, "academy_reception_existing")!;
        Assert.That(v.Choices.Select(c => c.Label), Is.EqualTo(new[] { "仅调查环境记录", "拒绝检测，仍领取公开路线" }));
        Ok(E.StartCityService(s, "academy_reception_existing", null, v.Choices[1].Id));
        ReadDialogues(s);
        Assert.That(s.Progress.Flags, Is.SupersetOf(new[] { "FA03_environment_briefed", Q("FA03", "accepted") }));
        foreach (var edge in new[] { "r06_archive__r07_workshop", "r03_lab__d02_01" })
            Assert.That(s.UnlockedEdges, Does.Contain(edge));
        Assert.That((s.Stats.Health, s.Body.VirusLoad, s.Body.Immunity), Is.EqualTo((s.Stats.Health, s.Body.VirusLoad, s.Body.Immunity)));
    }

    // ───────────── 记录 → 主线完成与容量（PT01、FN04） ─────────────

    /// <summary>
    /// 首次核心记录：圣所旧路档册 → FA02 完成、C2→C3；封存区安全规程 → FA03 完成、C4；同一地牢换代后再核对不再加容量、不重复完成（PT01/FN04）。
    /// FA04 随两份记录到齐出现在任务册。
    /// </summary>
    [Test]
    public void FA02_FA03_RecordsCompleteMainline_CapacityC3C4Once()
    {
        var s = At("h02_square", Registered);
        Assert.That(E.BattleCapacity(s), Is.EqualTo(2));
        Assert.That(E.QuestViewOf(s, "FA04"), Is.Null, "两条线都没完成前任务册不出现整理");
        Assert.That(E.DebugClaimRecord(s, "d01_08_unique"), Does.Contain("C2→C3"));
        Assert.That(s.Progress.Flags, Does.Contain(Q("FA02", "completed")));
        Assert.That(E.BattleCapacity(s), Is.EqualTo(3));
        Assert.That(s.Log.Any(l => l.Text.Contains("被称作新生的人") && l.Text.Contains("完成")), Is.True);
        Assert.That(E.QuestViewOf(s, "FA04")!.Status, Is.EqualTo("locked"));
        Assert.That(E.QuestViewOf(s, "FA04")!.AcceptBlocked, Is.EqualTo("需要：在灰塔封存区领取封存区安全规程"));
        E.DebugClaimRecord(s, "d02_08_unique");
        Assert.That(E.BattleCapacity(s), Is.EqualTo(4));
        Assert.That(s.Progress.Flags, Does.Contain(Q("FA03", "completed")));
        Assert.That(E.QuestViewOf(s, "FA04")!.Status, Is.EqualTo("available"));
        Assert.That(E.NewDungeonGeneration(s, "d01"), Is.Null);
        Assert.That(E.DebugClaimRecord(s, "d01_08_unique"), Does.Contain("已领取"), "同一记录本局唯一");
        Assert.That(E.BattleCapacity(s), Is.EqualTo(4));
        Assert.That(s.Log.Count(l => l.Text.Contains("被称作新生的人") && l.Text.Contains("完成")), Is.EqualTo(1));
    }

    /// <summary>真实领取动作（d01_08 核心记录）同事务写记录与 FA02 完成，日志写容量升档。</summary>
    [Test]
    public void FA02_RealClaimAction_LogsCapacity()
    {
        var s = At("h02_square", Registered);
        E.DebugMapTeleport(s, "d01_08");
        ReadDialogues(s);
        var res = Cfg.Resources.Single(r => r.NodeId == "d01_08_unique");
        s.Resources[res.NodeId].Discovered = true;
        E.DebugInstanceFact(s, "d01", "guardianDefeated");
        var r = E.Start(s, ActionRequest.Gather(res.NodeId));
        Assert.That(r.Ok, Is.True, r.Reason);
        Run(s);
        ReadDialogues(s);
        Assert.That(s.Log.Any(l => l.Text.Contains("已领取圣所旧路档册") && l.Text.Contains("队伍容量升到 C3")), Is.True);
        Assert.That(s.Progress.Flags, Does.Contain(Q("FA02", "completed")));
    }

    // ───────────── FA04（A08 规则 7） ─────────────

    /// <summary>FA04：两份记录到齐，营区广场 wait10 两段；中途停下保留已完成的 5 分钟，续做只补 5 分钟；两段都完成才写 FA04_merged、开警备仓入口；整理完成后不能再办。</summary>
    [Test]
    public void FA04_MergeTwoSegments_KeepOnCancel_OpensD03()
    {
        var s = At("h02_square", Registered);
        var v = E.CityServicePreview(s, "hub_affairs_record")!;
        Assert.That(v.Blocked, Is.EqualTo("需要：在教会地下圣所领取圣所旧路档册"));
        E.DebugClaimRecord(s, "d01_08_unique");
        E.DebugClaimRecord(s, "d02_08_unique");
        ReadDialogues(s);
        v = E.CityServicePreview(s, "hub_affairs_record")!;
        Assert.That((v.Blocked, v.TimeText), Is.EqualTo(((string?)null, "10分钟")));
        var t0 = s.GameMinute;
        Ok(E.StartCityService(s, "hub_affairs_record"));
        Assert.That(s.Progress.Flags, Does.Contain(Q("FA04", "accepted")), "开始整理即接取");
        Assert.That(s.Story.DialogueLog.Any(d => d.EventId == "FA04_merge_accept"), Is.True);
        ReadDialogues(s);
        for (var i = 0; i < 20 && s.GameMinute < t0 + 5; i++) E.Advance(s);
        Assert.That(s.GameMinute, Is.EqualTo(t0 + 5));
        Assert.That(s.Progress.Flags, Does.Not.Contain("FA04_merged"), "第一段不发权限");
        Ok(E.Cancel(s));
        Assert.That(s.Quests.Records["FA04"].WorkMinutes, Is.EqualTo(5), "离开保留已完成段");
        Assert.That(E.CityServicePreview(s, "hub_affairs_record")!.TimeText, Is.EqualTo("5分钟"));
        Assert.That(s.UnlockedEdges, Does.Not.Contain("o03_police__d03_01"));
        var t1 = s.GameMinute;
        Ok(E.StartCityService(s, "hub_affairs_record"));
        Run(s);
        Assert.That(s.GameMinute, Is.EqualTo(t1 + 5), "续做只补剩余 5 分钟");
        Assert.That(s.Progress.Flags, Is.SupersetOf(new[] { "FA04_merged", Q("FA04", "completed") }));
        Assert.That(s.UnlockedEdges, Does.Contain("o03_police__d03_01"), "FA04_merged 门槛开警备仓入口");
        Assert.That(s.Story.DialogueLog.Any(d => d.EventId == "FA04_merge_complete"), Is.True);
        ReadDialogues(s);
        Assert.That(E.CityServicePreview(s, "hub_affairs_record")!.Blocked, Does.Contain("已归档"));
        Assert.That(E.StartCityService(s, "hub_affairs_record").Ok, Is.False);
        Assert.That(E.BattleCapacity(s), Is.EqualTo(4), "整理不加容量");
    }

    /// <summary>FA04 没齐两份记录时直接请求：拒绝，零扣费零效果（A08 规则 7：不用 C4 单值替代两种记录）。</summary>
    [Test]
    public void FA04_NeedsBothRecords_NotCapacityAlone()
    {
        var s = At("h02_square", Registered.Concat(new[] { "church_route_record", "academy_safety_record" }).ToArray());
        Assert.That(E.BattleCapacity(s), Is.EqualTo(4));
        var json = SaveSerializer.Serialize(s);
        Assert.That(E.StartCityService(s, "hub_affairs_record").Ok, Is.False);
        Assert.That(SaveSerializer.Serialize(s), Is.EqualTo(json));
    }

    // ───────────── FA05（FN08） ─────────────

    /// <summary>
    /// FA05：领到警备仓备用电源后到 h03 与罗铮交谈接取（播电源与联络），联络日志（d04_07 核心）到手即完成并播联络完成；
    /// 饱饱台词只在饱饱在体内时播；不加容量、不发药（FN08）。
    /// </summary>
    [TestCase(true)]
    [TestCase(false)]
    public void FA05_PowerThenContact_ChapterOnly(bool story)
    {
        var s = story ? NewStory() : New();
        ReadDialogues(s);
        s.Progress.Flags.UnionWith(Registered);
        E.DebugMapTeleport(s, "h03_workshop");
        ReadDialogues(s);
        Assert.That(E.QuestViewOf(s, "FA05"), Is.Null);
        E.DebugClaimRecord(s, "d03_08_unique");
        Assert.That(E.QuestViewOf(s, "FA05")!.Status, Is.EqualTo("available"));
        Ok(E.TalkNpc(s, "npc_luo_zheng"));
        Assert.That(s.Story.DialogueLog.Any(d => d.EventId == "FA05_power_contact"), Is.True);
        ReadDialogues(s);
        Assert.That(s.Progress.Flags, Does.Contain(Q("FA05", "accepted")));
        var cap = E.BattleCapacity(s);
        var items = s.Items.Sum(i => i.Count);
        E.DebugClaimRecord(s, "d04_07_unique");
        Assert.That(s.Progress.Flags, Does.Contain(Q("FA05", "completed")));
        var log = s.Story.DialogueLog.Single(d => d.EventId == "FA05_contact_complete");
        ReadDialogues(s);
        var lines = Cfg.Story.Dialogues.Where(d => d.Event == "FA05_contact_complete").ToList();
        Assert.That(s.Story.SeenDialogues.Contains(lines.Single(d => d.Speaker == GameEngine.BaobaoSpeaker).Id), Is.EqualTo(story), "饱饱只在体内时发声");
        Assert.That(E.BattleCapacity(s), Is.EqualTo(cap), "联络不提供新的扩容");
        Assert.That(s.Items.Sum(i => i.Count), Is.EqualTo(items), "不发根治药或物资");
    }

    // ───────────── 交谈状态（FN07） ─────────────

    /// <summary>FN07：祁闻有可领的公开路线时交谈播“委托”一组；领过后播“委托回报”；都 0 分钟、不改身体与事实。</summary>
    [Test]
    public void FN07_PostTalkQuestStates()
    {
        var s = At("c02_church", Registered);
        Ok(E.TalkNpc(s, "npc_qi_wen"));
        ReadDialogues(s);
        var before = (s.GameMinute, s.Progress.Flags.Count, s.Stats.Health);
        Ok(E.TalkNpc(s, "npc_qi_wen"));
        ReadDialogues(s);
        Assert.That(s.Story.DialogueLog.Any(d => d.EventId.StartsWith("npc_qi_wen_dialogue_quest_offer")), Is.True);
        Assert.That((s.GameMinute, s.Progress.Flags.Count, s.Stats.Health), Is.EqualTo(before));
        Ok(E.StartCityService(s, "church_reception_existing", null, "FA02_accept_no_oath"));
        ReadDialogues(s);
        Ok(E.TalkNpc(s, "npc_qi_wen"));
        ReadDialogues(s);
        Assert.That(s.Story.DialogueLog.Any(d => d.EventId.StartsWith("npc_qi_wen_dialogue_quest_return")), Is.True);
        Assert.That(E.NpcsHere(s).Single(c => c.Kind == "post").QuestText, Is.EqualTo("委托：被称作新生的人（进行中）"));
    }

    // ───────────── 旧档（FN06） ─────────────

    /// <summary>FN06：存档 26→27 按已有记录无声追认（不播对白、不补奖励、不加容量），幂等；FA01 留到下一次到广场正常播。</summary>
    [Test]
    public void FN06_Migrate26To27_RetroQuests()
    {
        var s = New();
        s.Progress.Flags.UnionWith(Registered.Concat(new[] { "FA02_route_briefed", "FA02_record_credit", "church_route_record", "campaignId:d01_08_unique", "communication_power_claimed" }));
        s.Quests = null!;
        s.SchemaVersion = SaveMigrator.Schema26;
        var cap = E.BattleCapacity(s);
        var items = s.Items.Sum(i => i.Count);
        var dialogues = s.Story.DialogueLog.Count;
        SaveMigrator.Migrate(s, Cfg);
        Assert.That(s.SchemaVersion, Is.EqualTo(GameState.CurrentSchema));
        Assert.That(s.Progress.Flags, Is.SupersetOf(new[] { Q("FA02", "accepted"), Q("FA02", "completed"), Q("FA05", "accepted") }));
        Assert.That(s.Progress.Flags, Does.Not.Contain(Q("FA05", "completed")));
        Assert.That(s.Progress.Flags, Does.Not.Contain(Q("FA01", "completed")), "FA01 不追认，下一次到广场照常说明");
        Assert.That(s.Quests.Records["FA02"].Retro, Is.True);
        Assert.That(E.QuestViewOf(s, "FA05")!.Status, Is.EqualTo("active"));
        Assert.That(E.QuestViewOf(s, "FA05")!.Retro, Is.True, "M333：已接取的追认项同样标旧档追认");
        Assert.That(E.QuestViewOf(s, "FA02")!.Retro, Is.True);
        Assert.That(s.Story.DialogueLog.Count, Is.EqualTo(dialogues), "不播对白");
        Assert.That((E.BattleCapacity(s), s.Items.Sum(i => i.Count)), Is.EqualTo((cap, items)));
        Assert.That(s.Log.Count(l => l.Text.Contains("版本27") && l.Text.Contains("被称作新生的人")), Is.EqualTo(1));
        var json = SaveSerializer.Serialize(s);
        SaveMigrator.Migrate(s, Cfg);
        Assert.That(SaveSerializer.Serialize(s), Is.EqualTo(json), "幂等");
        // 完整读档入口：26 的 JSON（没有 Quests 字段）升到 27
        var j = JObject.Parse(json);
        j["SchemaVersion"] = SaveMigrator.Schema26;
        j.Remove("Quests");
        var m = SaveSerializer.Deserialize(j.ToString(), Cfg, out _);
        Assert.That(m.SchemaVersion, Is.EqualTo(GameState.CurrentSchema));
        Assert.That(m.Quests, Is.Not.Null);
        Assert.That(E.QuestViewOf(m, "FA02")!.Status, Is.EqualTo("done"));
    }

    /// <summary>任务册与岗位卡的玩家文字不出内部任务编号（Q145）。</summary>
    [Test]
    public void QuestText_NoInternalIds()
    {
        var rx = new System.Text.RegularExpressions.Regex(@"(QMAP|FA|CH|AC|EXQ|RN|HU)\d{2}");
        var s = At("h02_square", Registered);
        E.DebugClaimRecord(s, "d01_08_unique");
        E.DebugClaimRecord(s, "d02_08_unique");
        E.DebugClaimRecord(s, "d03_08_unique");
        ReadDialogues(s);
        foreach (var v in E.QuestBook(s))
        {
            var text = string.Join("|", v.Title, v.Step, v.StatusText, v.AcceptBlocked ?? "", v.WorkLabel ?? "", v.WorkBlocked ?? "", v.NpcName, v.LocationName);
            Assert.That(rx.IsMatch(text), Is.False, text);
        }
        Assert.That(s.Log.Where(l => l.Text.Contains("主线")).All(l => !rx.IsMatch(l.Text)), Is.True);
    }
}
