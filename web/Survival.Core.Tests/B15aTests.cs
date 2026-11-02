using Newtonsoft.Json.Linq;
using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 批次 B15a：策划答复 Q152—Q159（策划提交 d1da493，需求 R151—R158）。
/// 规则出处：A11「Q152—Q159 主线衔接与同行邀约」、更新说明 2026-11-02「Q152-Q159」（Q155 显示算法、Q156 邀约交互、六人逐字稿）、
/// assets/20261102/city/quest-adjudications.json。验收 QUEST-Q152-01 … QUEST-Q159-01。
/// </summary>
public class B15aTests : TestBase
{
    private void Ok(ActionResult r) => Assert.That(r.Ok, Is.True, r.Reason);

    private static string Q(string id, string kind) => GameEngine.QuestFact(id, kind);

    private static readonly string[] Registered = { "registration_authorized", "hub_registered" };

    private static JObject Design(string name) => JObject.Parse(File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, name)));

    private GameState At(string loc, params string[] facts)
    {
        var s = New();
        s.Progress.Flags.UnionWith(facts);
        E.DebugMapTeleport(s, loc);
        ReadDialogues(s);
        return s;
    }

    private static void Explore(GameState s, string loc, int pct) => s.Locations[loc].Progress = pct;

    private void Go(GameState s, string loc)
    {
        E.DebugMapTeleport(s, loc);
        ReadDialogues(s);
    }

    private NpcCardView? Card(GameState s, string actorId) => E.NpcsHere(s).FirstOrDefault(c => c.NpcId == actorId && c.Kind == "companion");

    /// <summary>交谈 → 邀约对白末句 → 选择（登记/暂不登记）。返回邀约对白那一句。</summary>
    private DialogueLine TalkAndAnswer(GameState s, string actorId, string questId, bool register)
    {
        Ok(E.TalkNpc(s, actorId));
        Assert.That(E.RecruitOfferPending(s), Is.EqualTo(questId));
        var line = E.CurrentDialogueLine(s)!;
        Ok(E.AnswerRecruit(s, questId, line.LineId, register));
        Assert.That(E.DialogueLocked(s), Is.False);
        return line;
    }

    // ───────────── Q152 RN02—RN06 前置与地点 ─────────────

    /// <summary>
    /// QUEST-Q152-01：RN02 已接或已完成 FA02 且 a01 探索 100%、h03；RN03 已接或已完成 FA03 且 b01 100%、h04（营区医务室）；RN04 完成 FA03 且 w02 100%、h02；
    /// RN05 已领警备仓备用电源或 FA04 完成、h03；RN06 同 RN05 且 b03 100%、h02。前置不满足不显示、不出卡；只在本人地点出卡。
    /// </summary>
    [TestCase("RN02", "npc_zhou_yan", "h03_workshop")]
    [TestCase("RN03", "npc_shen_he", "h04_infirmary")]
    [TestCase("RN04", "npc_tang_li", "h02_square")]
    [TestCase("RN05", "npc_gu_cheng", "h03_workshop")]
    [TestCase("RN06", "npc_ye_xun", "h02_square")]
    public void QUEST_Q152_01_Prerequisites(string quest, string actor, string loc)
    {
        var s = At(loc, Registered);
        Assert.That(E.QuestViewOf(s, quest), Is.Null, "前置不满足不显示");
        Assert.That(Card(s, actor), Is.Null);
        switch (quest)
        {
            case "RN02":
                s.Progress.Flags.Add(Q("FA02", "accepted"));
                Assert.That(E.AcceptQuest(s, quest).Reason, Is.EqualTo("需要：把铁路货场探索到100%（现在0%）"));
                Explore(s, "a01_yard", 100);
                break;
            case "RN03":
                Explore(s, "b01_school", 100);
                Assert.That(E.AcceptQuest(s, quest).Reason, Is.EqualTo("需要：接取或完成“样本之外”"));
                s.Progress.Flags.Add(Q("FA03", "completed"));   // 已完成也能补接（不因主线超前漏人）
                break;
            case "RN04":
                s.Progress.Flags.Add(Q("FA03", "accepted"));
                Explore(s, "w02_forest", 100);
                Assert.That(E.AcceptQuest(s, quest).Reason, Is.EqualTo("需要：完成“样本之外”"), "RN04 要 FA03 完成，只接取不够");
                s.Progress.Flags.Add(Q("FA03", "completed"));
                break;
            case "RN05":
                Assert.That(E.AcceptQuest(s, quest).Reason, Is.EqualTo("需要：在警备仓封存仓核心领取警备仓备用电源，或完成“同一条归路”"));
                s.Progress.Flags.Add(Q("FA04", "completed"));
                break;
            case "RN06":
                s.Progress.Flags.Add("communication_power_claimed");
                Assert.That(E.AcceptQuest(s, quest).Reason, Is.EqualTo("需要：把医院外院探索到100%（现在0%）"));
                Explore(s, "b03_hospital", 100);
                break;
        }
        Go(s, "h01_gate");
        Assert.That(Card(s, actor), Is.Null, "只在本人地点出卡");
        Assert.That(E.AcceptQuest(s, quest).Reason, Does.StartWith("须实际到达"));
        Go(s, loc);
        var c = Card(s, actor)!;
        Assert.That((c.KindLabel, c.ActionLabel), Is.EqualTo(("固定队友", "交谈")));
        var v = E.QuestViewOf(s, quest)!;
        Assert.That((v.Status, v.KindLabel), Is.EqualTo(("available", "同行邀约")));
        // 0 分钟 0 物资；首次两件装备；进名册候补
        var minute = s.GameMinute;
        var items = s.Items.Count;
        TalkAndAnswer(s, actor, quest, true);
        Assert.That(s.GameMinute, Is.EqualTo(minute));
        Assert.That(s.Items.Count - items, Is.EqualTo(2));
        var named = Cfg.Party.Named(actor)!;
        Assert.That(s.Items.Where(i => i.Owner != null && i.Owner.Contains(actor)).Select(i => i.ItemId), Is.EquivalentTo(new[] { named.Weapon1, named.Weapon2 }));
        Assert.That(s.Progress.Flags, Does.Contain(Q(quest, "completed")));
        Assert.That(Card(s, actor), Is.Null);
    }

    /// <summary>QUEST-Q152-01：RN02/RN05 两种“任一”前置各自都够（FA02 只完成未单独记接取；RN05 只领电源）。</summary>
    [Test]
    public void QUEST_Q152_01_AnyOfEitherSuffices()
    {
        var s = At("h03_workshop", Registered.Concat(new[] { Q("FA02", "completed"), "communication_power_claimed" }).ToArray());
        Explore(s, "a01_yard", 100);
        Go(s, "h03_workshop");
        Assert.That(E.QuestViewOf(s, "RN02")!.Status, Is.EqualTo("available"));
        Assert.That(E.QuestViewOf(s, "RN05")!.Status, Is.EqualTo("available"));
        Assert.That(E.NpcsHere(s).Count(c => c.Kind == "companion"), Is.EqualTo(2), "同一地点两张邀约卡，不替代罗铮岗位卡");
        Assert.That(E.NpcsHere(s).Any(c => c.Kind == "post" && c.NpcId == "npc_luo_zheng"), Is.True);
    }

    // ───────────── Q153 / Q158 战术训练 ─────────────

    /// <summary>
    /// QUEST-Q153-01 / QUEST-Q158-01：阶位 2 在已领警备仓备用电源（或 FA04 完成）后开放，没有 10 分钟接口检测；精密零件2+钢材2，全队固定 90 分钟，
    /// 不吃技能/制造加速；完成到 5AP。入口在 h03 罗铮事项、不在 19 项城市服务表里；旧“QMAP04”说明已去掉。
    /// </summary>
    [TestCase("communication_power_claimed")]
    [TestCase("quest:FA04:completed")]
    public void QUEST_Q153_Q158_TrainingTier2(string unlock)
    {
        var s = At("h03_workshop", Registered.Concat(new[] { "church_route_record" }).ToArray());
        Assert.That(Cfg.City.Services.Any(x => x.Id == GameEngine.TrainingServiceId), Is.False, "不计入 19 项城市服务");
        Assert.That(Cfg.City.Services, Has.Count.EqualTo(19));
        Assert.That(E.CityServicesOf(s, "npc_luo_zheng").Any(x => x.Id == GameEngine.TrainingServiceId), Is.True, "入口在罗铮事项");
        Assert.That(Cfg.Map.FactLabels.ContainsKey(Q("QMAP04", "completed")), Is.False);
        Assert.That(Cfg.Scope.Training.Tiers.Single(t => t.Tier == 2).RequiresAll, Is.Empty);
        s.Player.TacticalTier = 1;
        E.DebugSetSkillLevel(s, "crafting", 10);   // 制造技能不加速训练
        E.DebugGiveCarriedOrGround(s, new[] { ("precision_parts", 2), ("steel", 2) });
        var v = E.CityServicePreview(s, GameEngine.TrainingServiceId)!;
        Assert.That(v.Targets.Single(t => t.ActorId == GameState.PlayerId).Blocked, Does.StartWith("阶位2需要："));
        s.Progress.Flags.Add(unlock);
        v = E.CityServicePreview(s, GameEngine.TrainingServiceId)!;
        Assert.That(v.Targets.Single(t => t.ActorId == GameState.PlayerId).Blocked, Is.Null, "无单设接口检测");
        var t0 = s.GameMinute;
        Ok(E.StartCityService(s, GameEngine.TrainingServiceId, GameState.PlayerId));
        Run(s);
        ReadDialogues(s);
        Assert.That(s.GameMinute, Is.EqualTo(t0 + 90), "全队固定等待 90 分钟");
        Assert.That(s.Player.TacticalTier, Is.EqualTo(2));
        Assert.That(E.NormalActivationAp(s.Player), Is.EqualTo(5));
        Assert.That(s.Items.Where(i => i.ItemId is "precision_parts" or "steel").Sum(i => i.Count), Is.EqualTo(0));
    }

    /// <summary>QUEST-Q158-01：阶位 1 也是固定 60 分钟，不吃制造技能加速。</summary>
    [Test]
    public void QUEST_Q158_01_Tier1Fixed60()
    {
        var s = At("h03_workshop", Registered.Concat(new[] { "church_route_record" }).ToArray());
        E.DebugSetSkillLevel(s, "crafting", 10);
        s.Items.RemoveAll(i => i.ItemId is "parts" or "cloth");
        E.DebugGiveCarriedOrGround(s, new[] { ("parts", 2), ("cloth", 2) });
        var t0 = s.GameMinute;
        Ok(E.StartCityService(s, GameEngine.TrainingServiceId, GameState.PlayerId));
        Run(s);
        Assert.That(s.GameMinute, Is.EqualTo(t0 + 60));
        Assert.That(s.Player.TacticalTier, Is.EqualTo(1));
    }

    // ───────────── Q154 ─────────────

    /// <summary>QUEST-Q154-01：旧 QMAP02 修泵、QMAP03 救护不作为新局委托：没有任务条目、没有服务；可信旧档 QMAP02 只在锻炉/军械台资格里认（Q141/Q148，不转成奖励）。</summary>
    [Test]
    public void QUEST_Q154_01_NoLegacyQmapQuests()
    {
        Assert.That(Cfg.Quests.List.Select(q => q.Id), Has.None.StartsWith("QMAP"));
        Assert.That(Cfg.City.Services.Where(x => x.QuestId != null).Select(x => x.QuestId), Has.None.StartsWith("QMAP"));
        Assert.That(Cfg.City.PublicStations.SelectMany(p => p.AnyFacts), Does.Contain("trusted_legacy_QMAP02_completed"));
        var s = At("h03_workshop", Registered.Concat(new[] { "trusted_legacy_QMAP02_completed" }).ToArray());
        Assert.That(E.QuestBook(s).Select(v => v.Id), Has.None.StartsWith("QMAP"));
    }

    // ───────────── Q155 登记后主目标 ─────────────

    private PrologueObjectiveView Objective(GameState s)
    {
        E.PrologueBoundary(s);
        return E.CurrentObjective(s)!;
    }

    /// <summary>
    /// QUEST-Q155-01：登记前仍是序章 route_onward；登记后按 FA01→FA05 固定顺序：先进行中、再可接；平行 FA02/FA03 都已接时 FA02 优先、完成后切 FA03；
    /// FA04 完成而 FA05 未满足条件时“查看主线与委托”；全部完成“保持联络，继续求生”。标题正式名、下一步任务册同状态短句；显示不自动接取。
    /// </summary>
    [Test]
    public void QUEST_Q155_01_MainQuestObjective()
    {
        var s = At("h01_gate", "camp_route_known");
        Assert.That(Objective(s).Title, Is.Not.EqualTo("营区之外"), "登记前不接续");
        s.Progress.Flags.UnionWith(Registered);
        var o = Objective(s);
        Assert.That((o.Id, o.Title, o.Step), Is.EqualTo(("main_quest", "营区之外", E.QuestViewOf(s, "FA01")!.Step)));
        Assert.That(s.Prologue.ObjectiveId, Is.EqualTo("main_quest"));
        Go(s, "h02_square");   // FA01 到达即完成
        var flags = s.Progress.Flags.Count;
        o = Objective(s);
        Assert.That((o.Title, o.Step), Is.EqualTo(("被称作新生的人", "到教会接待庭向祁闻询问公开路线（可拒绝宣誓）。")), "都可接时按固定顺序取 FA02");
        Assert.That(s.Progress.Flags.Count, Is.EqualTo(flags), "只读，不自动接取");
        s.Progress.Flags.Add(Q("FA03", "accepted"));
        o = Objective(s);
        Assert.That((o.Title, o.Step), Is.EqualTo(("样本之外", E.QuestViewOf(s, "FA03")!.Step)), "已接的优先于可接的");
        Assert.That(o.Step, Does.StartWith("从封存实验楼进入灰塔封存区"));
        s.Progress.Flags.Add(Q("FA02", "accepted"));
        Assert.That(Objective(s).Title, Is.EqualTo("被称作新生的人"), "都已接时 FA02 优先");
        s.Progress.Flags.UnionWith(new[] { Q("FA02", "completed"), "FA02_record_credit" });
        Assert.That(Objective(s).Title, Is.EqualTo("样本之外"), "FA02 完成后切到另一条");
        s.Progress.Flags.UnionWith(new[] { Q("FA03", "completed"), "FA03_record_credit" });
        o = Objective(s);
        Assert.That((o.Title, o.Step), Is.EqualTo(("同一条归路", E.QuestViewOf(s, "FA04")!.Step)));
        s.Progress.Flags.UnionWith(new[] { Q("FA04", "accepted"), Q("FA04", "completed") });
        Assert.That(E.QuestViewOf(s, "FA05")!.Status, Is.EqualTo("locked"));
        o = Objective(s);
        Assert.That((o.Title, o.Step), Is.EqualTo(("查看主线与委托", "打开任务册，查看当前主线进度与接取条件。")));
        s.Progress.Flags.Add("communication_power_claimed");
        Assert.That(Objective(s).Title, Is.EqualTo("联络仍在继续"));
        s.Progress.Flags.UnionWith(new[] { Q("FA05", "accepted"), Q("FA05", "completed") });
        o = Objective(s);
        Assert.That((o.Title, o.Step), Is.EqualTo(("保持联络，继续求生", "本轮主线已完成，可在任务册回看记录，继续探索与求生。")));
        Assert.That(s.Progress.Dead, Is.False, "不强制终局");
        Assert.That(E.ObjectiveFocus(s), Is.Null, "不点亮/伪造路线");
    }

    // ───────────── Q156 邀约对白与登记 ─────────────

    /// <summary>QUEST-Q156-01：六人逐字邀约（narrative-texts recruitInvitations）导入为各自一句、由本人说；对白组名不带内部编号。</summary>
    [Category("DesignSync")]
    [Test]
    public void QUEST_Q156_00_InvitationsVerbatim()
    {
        var inv = (JArray)Design("design.narrative-texts.json")["recruitInvitations"]!;
        Assert.That(inv, Has.Count.EqualTo(6));
        foreach (var x in inv)
        {
            var q = Cfg.Quests.List.Single(q => q.Id == (string)x["questId"]!);
            Assert.That(q.Implemented, Is.True);
            Assert.That(q.OfferDialogue, Is.EqualTo((string)x["dialogueId"]!));
            var lines = Cfg.Story.Dialogues.Where(d => d.Event == q.OfferDialogue).ToList();
            Assert.That(lines, Has.Count.EqualTo(1));
            Assert.That((lines[0].Id, lines[0].Speaker, lines[0].Text), Is.EqualTo(((string)x["lineId"]!, (string)x["displayName"]!, (string)x["text"]!)));
            Assert.That(Cfg.Party.Named(q.NpcId)!.Name, Is.EqualTo((string)x["displayName"]!));
            Assert.That(Cfg.Quests.DialogueSets.Single(d => d.Id == q.OfferDialogue).Title, Is.EqualTo(q.Title + "：邀约"));
        }
    }

    /// <summary>
    /// QUEST-Q156-01：“交谈”打开逐字邀约，读完末句是“登记同行”“暂不登记”两选项；暂不登记：不完成、不永久拒招、卡还在、可重读（每次新对话记录），
    /// 都 0 分钟 0 物资不计经验；重读不改变资格/任务/时间；本人邀约不由机构代班人代说（岗位离岗也是本人说）。
    /// </summary>
    [Test]
    public void QUEST_Q156_01_DeclineThenReread()
    {
        var s = At("h02_square", Registered);
        E.DebugToggleCityPostAbsent(s);   // 许安离岗：广场的机构代班不影响林昭本人
        ReadDialogues(s);
        var before = (s.GameMinute, s.Items.Count, s.Skills.Values.Sum(k => k.Xp), s.Progress.Flags.Count);
        Ok(E.TalkNpc(s, "npc_lin_zhao"));
        var line = E.CurrentDialogueLine(s)!;
        Assert.That((line.Speaker, line.Text), Is.EqualTo(("林昭", Cfg.Story.Dialogues.Single(d => d.Event == "RN01_recruit_offer").Text)));
        Assert.That(E.DialogueHasMore(s), Is.False);
        Assert.That(E.RecruitOfferPending(s), Is.EqualTo("RN01"));
        Ok(E.AnswerRecruit(s, "RN01", line.LineId, false));
        Assert.That(E.AnswerRecruit(s, "RN01", line.LineId, true).Duplicate, Is.True, "双击/旧句不再登记");
        Assert.That(E.RosterOf(s, "npc_lin_zhao"), Is.Null);
        Assert.That(s.Progress.Flags, Does.Not.Contain(Q("RN01", "completed")).And.Not.Contain(Q("RN01", "accepted")));
        Assert.That((s.GameMinute, s.Items.Count, s.Skills.Values.Sum(k => k.Xp), s.Progress.Flags.Count), Is.EqualTo(before));
        Assert.That(Card(s, "npc_lin_zhao"), Is.Not.Null, "暂不登记不是永久拒招");
        Assert.That(s.Log.Any(l => l.Text.Contains("暂不登记林昭")), Is.True);
        // 重读：新的一段对话记录，再选登记
        TalkAndAnswer(s, "npc_lin_zhao", "RN01", true);
        Assert.That(s.Story.DialogueLog.Count(d => d.EventId.StartsWith("RN01_recruit_offer")), Is.EqualTo(2));
        Assert.That(s.Story.DialogueLog.Where(d => d.EventId.StartsWith("RN01_recruit_offer")).SelectMany(d => d.Lines).All(l => l.Speaker == "林昭"), Is.True);
        Assert.That(E.RosterOf(s, "npc_lin_zhao"), Is.Not.Null);
        Assert.That(s.GameMinute, Is.EqualTo(before.GameMinute));
    }

    /// <summary>QUEST-Q156-01：读对白本身不登记；关闭（不选）不完成；没读完（不在邀约末句）不能登记。</summary>
    [Test]
    public void QUEST_Q156_01_OnlyAfterReading()
    {
        var s = At("h02_square", Registered);
        Assert.That(E.AnswerRecruit(s, "RN01", "NTL_RN_01", true).Duplicate, Is.True, "没有打开邀约不能登记");
        Ok(E.TalkNpc(s, "npc_lin_zhao"));
        Assert.That(E.RosterOf(s, "npc_lin_zhao"), Is.Null, "交谈本身不登记");
        Assert.That(E.AnswerRecruit(s, "RN02", E.CurrentDialogueLine(s)!.LineId, true).Duplicate, Is.True);
        Assert.That(E.RosterOf(s, "npc_zhou_yan"), Is.Null, "别人的邀约不能拿来登记");
    }

    /// <summary>
    /// QUEST-Q156-01：登记原子完成 RN、入名册候补、首次两件装备只生成一次、不自动编队；当前队伍满员也能登记（候补）；
    /// 已登记后不再出卡、再交谈/回看不出选项、不重发装备。
    /// </summary>
    [Test]
    public void QUEST_Q156_01_RegisterWhenPartyFull_NoAutoFormation_NoRegrant()
    {
        var s = At("h03_workshop", Registered.Concat(new[] { Q("FA02", "accepted") }).ToArray());
        Explore(s, "a01_yard", 100);
        E.DebugRecruit(s, "npc_lin_zhao");   // C2：你 + 林昭，同行已满
        ReadDialogues(s);
        Assert.That(s.Party.Count, Is.EqualTo(E.PartyCapacity(s)));
        Go(s, "h03_workshop");
        var items = s.Items.Count;
        TalkAndAnswer(s, "npc_zhou_yan", "RN02", true);
        Assert.That(E.RosterOf(s, "npc_zhou_yan"), Is.Not.Null);
        Assert.That(s.Party, Does.Not.Contain("npc_zhou_yan"), "不自动编队");
        Assert.That(s.Items.Count - items, Is.EqualTo(2));
        Assert.That(s.Log.Any(l => l.Text.Contains("同行邀约“检修交接”完成")), Is.True);
        Assert.That(Card(s, "npc_zhou_yan"), Is.Null);
        Assert.That(E.TalkNpc(s, "npc_zhou_yan").Ok, Is.False);
        Ok(E.ReviewQuest(s, "RN02"));
        Assert.That(E.RecruitOfferPending(s), Is.Null, "回看只回放，不出登记选项");
        ReadDialogues(s);
        Assert.That(s.Items.Count - items, Is.EqualTo(2), "不重发装备");
    }

    /// <summary>QUEST-Q156-01：名册已有（开发入口登记）的无声追认 RN 完成、不播邀约、不补装备；死亡的命名人物不复活、不出卡。</summary>
    [Test]
    public void QUEST_Q156_01_RosterRetro_DeadNotRevived()
    {
        var s = At("h02_square", Registered.Concat(new[] { "communication_power_claimed" }).ToArray());
        Explore(s, "b03_hospital", 100);
        E.DebugRecruit(s, "npc_ye_xun");
        ReadDialogues(s);
        var items = s.Items.Count;
        s.Roster.Single(r => r.ActorId == "npc_ye_xun").Dead = true;
        Go(s, "h03_workshop");
        Go(s, "h02_square");
        Assert.That(s.Progress.Flags, Does.Contain(Q("RN06", "completed")));
        Assert.That(s.Quests.Records["RN06"].Retro, Is.True);
        Assert.That(s.Story.DialogueLog.Any(d => d.EventId.StartsWith("RN06_recruit_offer")), Is.False, "不强制重播");
        Assert.That(Card(s, "npc_ye_xun"), Is.Null);
        Assert.That(E.TalkNpc(s, "npc_ye_xun").Ok, Is.False);
        Assert.That(s.Items.Count, Is.EqualTo(items), "不补装备");
        // 开发登记、仍存活的：同样无声追认
        var t = At("h02_square", Registered);
        E.DebugRecruit(t, "npc_lin_zhao");
        Go(t, "h02_square");
        Assert.That(t.Quests.Records["RN01"].Retro, Is.True);
        Assert.That(t.Story.DialogueLog.Any(d => d.EventId.StartsWith("RN01_recruit_offer")), Is.False);
    }

    // ───────────── Q157 FA05 ─────────────

    /// <summary>
    /// QUEST-Q157-01：已领警备仓备用电源后在 h03 与罗铮交谈接取 FA05；领到 d04_07 联络日志即完成。先领日志、未接取时不完成；
    /// 接取后同一安全边界追认完成（写完成与日志，不补播联络完成场景、不加容量或物资）。没有额外的“technical stage available”门槛。
    /// </summary>
    [Test]
    public void QUEST_Q157_01_Fa05_LogBeforeAccept()
    {
        var s = At("h03_workshop", Registered);
        E.DebugClaimRecord(s, "d03_08_unique");
        ReadDialogues(s);
        E.DebugClaimRecord(s, "d04_07_unique");
        ReadDialogues(s);
        Assert.That(s.Progress.Flags, Does.Contain("chapter_contact"));
        Assert.That(s.Progress.Flags, Does.Not.Contain(Q("FA05", "completed")), "未接取不完成");
        Assert.That(E.QuestViewOf(s, "FA05")!.Status, Is.EqualTo("available"));
        var cap = E.BattleCapacity(s);
        var items = s.Items.Sum(i => i.Count);
        Ok(E.TalkNpc(s, "npc_luo_zheng"));
        ReadDialogues(s);
        Assert.That(s.Progress.Flags, Is.SupersetOf(new[] { Q("FA05", "accepted"), Q("FA05", "completed") }));
        Assert.That(s.Story.DialogueLog.Any(d => d.EventId == "FA05_power_contact"), Is.True);
        Assert.That(s.Story.DialogueLog.Any(d => d.EventId.StartsWith("FA05_contact_complete")), Is.False, "追认不补播联络完成场景");
        Assert.That(s.Log.Any(l => l.Text.Contains("主线“联络仍在继续”完成")), Is.True);
        Assert.That(s.Quests.Records["FA05"].Retro, Is.False, "新局追认不标旧档");
        Assert.That((E.BattleCapacity(s), s.Items.Sum(i => i.Count)), Is.EqualTo((cap, items)));
    }

    /// <summary>QUEST-Q157-01：正常顺序（领电源→罗铮接取→领日志）完成并播联络完成；没领电源不能接。</summary>
    [Test]
    public void QUEST_Q157_01_Fa05_NormalOrder()
    {
        var s = At("h03_workshop", Registered.Concat(new[] { Q("FA04", "completed") }).ToArray());
        Assert.That(E.QuestViewOf(s, "FA05")!.AcceptBlocked, Is.EqualTo("需要：在警备仓封存仓核心领取警备仓备用电源"));
        E.DebugClaimRecord(s, "d03_08_unique");
        ReadDialogues(s);
        Ok(E.TalkNpc(s, "npc_luo_zheng"));
        ReadDialogues(s);
        Assert.That(s.Progress.Flags, Does.Not.Contain(Q("FA05", "completed")));
        E.DebugClaimRecord(s, "d04_07_unique");
        Assert.That(s.Progress.Flags, Does.Contain(Q("FA05", "completed")));
        Assert.That(s.Story.DialogueLog.Any(d => d.EventId == "FA05_contact_complete"), Is.True);
    }

    // ───────────── Q159 CH02 ─────────────

    /// <summary>
    /// QUEST-Q159-01：CH02 回营区备份的固定 10 分钟在教会训诫堂办完；回营区广场只播一次许安回报，不再扣工作时间、不重复奖励；之后再来不重播。
    /// </summary>
    [Test]
    public void QUEST_Q159_01_Ch02BackupAtAdmonition()
    {
        var s = At("c05_admonition", Registered.Concat(new[] { Q("CH01", "completed"), Q("FA02", "accepted"), "FA02_record_credit" }).ToArray());
        Assert.That(Cfg.City.Services.Single(x => x.Id == "church_public_mediation").LocationId, Is.EqualTo("c05_admonition"));
        Ok(E.TalkNpc(s, "npc_ji_mu"));
        ReadDialogues(s);
        var t0 = s.GameMinute;
        Ok(E.StartCityService(s, "church_public_mediation", null, GameEngine.Ch02BackupChoice));
        Run(s);
        ReadDialogues(s);
        Assert.That(s.GameMinute, Is.EqualTo(t0 + 10));
        Assert.That(s.Progress.Flags, Does.Contain(Q("CH02", "completed")));
        var items = s.Items.Sum(i => i.Count);
        E.DebugMapTeleport(s, "h02_square");
        var t1 = s.GameMinute;
        Assert.That(s.Story.DialogueLog.Count(d => d.EventId == "CH02_hub_backup_turnin"), Is.EqualTo(1));
        Assert.That(s.Story.DialogueLog.Single(d => d.EventId == "CH02_hub_backup_turnin").Counterpart, Is.EqualTo("许安"));
        ReadDialogues(s);
        Assert.That(s.GameMinute, Is.EqualTo(t1), "回报不扣时间");
        Assert.That(s.Items.Sum(i => i.Count), Is.EqualTo(items), "不重复奖励");
        Assert.That(s.Main, Is.Null);
        Go(s, "h01_gate");
        Go(s, "h02_square");
        Assert.That(s.Story.DialogueLog.Count(d => d.EventId.StartsWith("CH02_hub_backup_turnin")), Is.EqualTo(1));
    }
}
