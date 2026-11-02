using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 批次 B15 M327：RN01 林昭邀约（R47；A11 §6/§7）与 h03 战术训练（R46 接 R22；R03 §3）。
/// 验收：R08 PT07/PT08/PT09、A11 NP-A19（首次装备）、R03 CB04（训练 3→4→5 AP）中本批能做的部分。
/// </summary>
public class B15RecruitTrainingTests : TestBase
{
    private void Ok(ActionResult r) => Assert.That(r.Ok, Is.True, r.Reason);

    private static string Q(string id, string kind) => GameEngine.QuestFact(id, kind);

    private static readonly string[] Registered = { "registration_authorized", "hub_registered" };

    private GameState At(string loc, params string[] facts)
    {
        var s = New();
        s.Progress.Flags.UnionWith(facts);
        E.DebugMapTeleport(s, loc);
        ReadDialogues(s);
        return s;
    }

    // ───────────── RN01 ─────────────

    /// <summary>RN01：登记后营区广场出现林昭同行邀约卡（不替代许安岗位卡）；“交谈”读完邀约后“登记同行”0 分钟入名册（候补）并生成首次两件装备，任务完成、卡消失；再请求不重发。</summary>
    [Test]
    public void RN01_InviteCard_RegisterOnce()
    {
        var s = At("h02_square", Registered);
        var cards = E.NpcsHere(s);
        Assert.That(cards.Select(c => c.Kind), Is.EquivalentTo(new[] { "post", "companion" }));
        var lin = cards.Single(c => c.Kind == "companion");
        Assert.That((lin.Name, lin.Role, lin.KindLabel, lin.ActionLabel), Is.EqualTo(("林昭", "营区巡路", "固定队友", "交谈")));
        Assert.That(lin.Background, Does.StartWith("营区的物资搬运与巡路人员"));
        Assert.That(E.QuestViewOf(s, "RN01")!.KindLabel, Is.EqualTo("同行邀约"));
        var minute = s.GameMinute;
        var items = s.Items.Count;
        Ok(E.TalkNpc(s, "npc_lin_zhao"));
        // B15a（Q156 选 B）：交谈打开邀约对白，读完选“登记同行”才入名册
        Assert.That(E.RosterOf(s, "npc_lin_zhao"), Is.Null);
        Ok(E.AnswerRecruit(s, "RN01", E.CurrentDialogueLine(s)!.LineId, true));
        Assert.That(s.GameMinute, Is.EqualTo(minute), "0 分钟 0 物资");
        Assert.That(E.RosterOf(s, "npc_lin_zhao"), Is.Not.Null);
        Assert.That(s.Party, Does.Not.Contain("npc_lin_zhao"), "进名册候补，不自动编入");
        Assert.That(s.Items.Count - items, Is.EqualTo(2), "首次两件装备（木矛、木盾）");
        Assert.That(s.Progress.Flags, Does.Contain(Q("RN01", "completed")));
        Assert.That(E.NpcsHere(s).Any(c => c.Kind == "companion"), Is.False);
        Assert.That(E.TalkNpc(s, "npc_lin_zhao").Ok, Is.False);
        Assert.That(E.AcceptQuest(s, "RN01").Ok, Is.False, "不重发装备");
        Assert.That(s.Items.Count - items, Is.EqualTo(2));
    }

    /// <summary>RN01 未登记不出现；开发入口已登记过林昭的，安全边界记 RN01 完成（旧档追认），不出邀约卡、不重发；RN02—RN06 前置未满足时不显示。</summary>
    [Test]
    public void RN01_NotRegistered_NoCard_DevRecruitedRetro_OthersHidden()
    {
        var s = At("h02_square");
        Assert.That(E.NpcsHere(s).Any(c => c.Kind == "companion"), Is.False);
        Assert.That(E.QuestViewOf(s, "RN01"), Is.Null);
        var t = At("h03_workshop", Registered);
        E.DebugRecruit(t, "npc_lin_zhao");
        E.DebugMapTeleport(t, "h02_square");
        ReadDialogues(t);
        Assert.That(t.Progress.Flags, Does.Contain(Q("RN01", "completed")));
        Assert.That(t.Quests.Records["RN01"].Retro, Is.True);
        Assert.That(E.NpcsHere(t).Any(c => c.Kind == "companion"), Is.False);
        // B15a（Q152）：RN02—RN06 已开放，但前置未满足时不显示
        foreach (var id in new[] { "RN02", "RN03", "RN04", "RN05", "RN06" })
            Assert.That(E.QuestViewOf(t, id), Is.Null, id);
        Assert.That(E.QuestBook(t).Select(v => v.Id), Does.Not.Contain("RN02"));
    }

    // ───────────── 战术训练（R03 §3） ─────────────

    /// <summary>
    /// CB04：h03 罗铮事项里有“战术训练”；C2 时阶位 1 写容量条件；C3 后缺材料写真实数量；备齐标准零件2+布料2 开始 60 分钟，
    /// 完成阶位 1（4AP）并扣材料；阶位 2 写“为远行供电”条件（暂无来源）；不给经验。
    /// </summary>
    [Test]
    public void CB04_TrainingTier1_AfterC3_Materials()
    {
        var s = At("h03_workshop", Registered);
        var v = E.CityServicesOf(s, "npc_luo_zheng").Single(x => x.Id == GameEngine.TrainingServiceId);
        Assert.That(v.Title, Is.EqualTo("战术训练"));
        Assert.That(v.Targets.Single().Blocked, Is.EqualTo("阶位1需要：队伍容量达到C3（圣所旧路档册或封存区安全规程之一）"));
        E.DebugClaimRecord(s, "d01_08_unique");
        ReadDialogues(s);
        s.Items.RemoveAll(i => i.ItemId is "parts" or "cloth");
        v = E.CityServicePreview(s, GameEngine.TrainingServiceId)!;
        Assert.That(v.Targets.Single().Blocked, Does.StartWith("阶位1材料不足（随身或当地地面）：零件0/2、布料0/2"));
        E.DebugGiveCarriedOrGround(s, new[] { ("parts", 2), ("cloth", 2) });
        v = E.CityServicePreview(s, GameEngine.TrainingServiceId)!;
        Assert.That(v.Targets.Single().Blocked, Is.Null);
        Assert.That(v.Targets.Single().Detail, Does.Contain("阶位0→1（3→4AP）"));
        var xp = s.Skills.Values.Sum(k => k.Xp);
        var t0 = s.GameMinute;
        Ok(E.StartCityService(s, GameEngine.TrainingServiceId, GameState.PlayerId));
        Run(s);
        ReadDialogues(s);
        Assert.That(s.GameMinute, Is.EqualTo(t0 + 60));
        Assert.That(s.Player.TacticalTier, Is.EqualTo(1));
        Assert.That(E.NormalActivationAp(s.Player), Is.EqualTo(4));
        Assert.That(s.Items.Where(i => i.ItemId is "parts" or "cloth").Sum(i => i.Count), Is.EqualTo(0), "完成时扣材料");
        Assert.That(s.Skills.Values.Sum(k => k.Xp), Is.EqualTo(xp), "训练不是熟练度，不给经验");
        Assert.That(E.CityServicePreview(s, GameEngine.TrainingServiceId)!.Targets.Single().Blocked, Is.EqualTo("阶位2需要：在警备仓封存仓核心领取警备仓备用电源，或完成“同一条归路”"), "B15a Q153：阶位2 前置改为已领备用电源或 FA04 完成");
    }

    /// <summary>取消训练：材料原物原处、阶位不变；队友也能训练（各自一次），冷却/阶位记在本人。</summary>
    [Test]
    public void Training_CancelKeepsMaterials_CompanionTrainsSelf()
    {
        var s = At("h03_workshop", Registered.Concat(new[] { "church_route_record" }).ToArray());
        E.DebugRecruit(s, "npc_lin_zhao");
        ReadDialogues(s);
        Assert.That(s.Party, Does.Contain("npc_lin_zhao"));
        s.Items.RemoveAll(i => i.ItemId is "parts" or "cloth");
        E.DebugGiveCarriedOrGround(s, new[] { ("parts", 2), ("cloth", 2) });
        Ok(E.StartCityService(s, GameEngine.TrainingServiceId, "npc_lin_zhao"));
        E.Advance(s);
        Ok(E.Cancel(s));
        Assert.That(s.Actors["npc_lin_zhao"].TacticalTier, Is.EqualTo(0));
        Assert.That(s.Items.Where(i => i.ItemId is "parts" or "cloth").Sum(i => i.Count), Is.EqualTo(4), "取消不扣材料");
        Assert.That(s.Items.Where(i => i.ItemId is "parts" or "cloth").All(i => i.ReservedBy == null), Is.True);
        Ok(E.StartCityService(s, GameEngine.TrainingServiceId, "npc_lin_zhao"));
        Run(s);
        ReadDialogues(s);
        Assert.That(s.Actors["npc_lin_zhao"].TacticalTier, Is.EqualTo(1));
        Assert.That(s.Player.TacticalTier, Is.EqualTo(0));
    }
}
