using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 批次 B23 M479：公开抄录（R71；A11「可选地牢任务与知识」§3 确定替代来源，开发假设 B23-4）。
/// 验收 EXQ-A04（公开抄录不需同副本记录、知识确实可达、未调查者不伪造通关/任务/容量）与 Q33 通用事务（中途取消无效、时间不退）。
/// </summary>
public class B23ExqCopyTests : TestBase
{
    private void Ok(ActionResult r) => Assert.That(r.Ok, Is.True, r.Reason);

    private GameState At(string loc, bool registered = true)
    {
        var s = New();
        if (registered) s.Progress.Flags.UnionWith(new[] { "registration_authorized", "hub_registered" });
        s.GameMinute = At(2, 9, 0);
        s.SpawnNextCheck = int.MaxValue;
        s.PatrolNextCheck = int.MaxValue;
        E.DebugMapTeleport(s, loc);
        ReadDialogues(s);
        return s;
    }

    /// <summary>
    /// EXQ-A04：h03 罗铮事项“查抄公开资料”两项——公开资料（净水操作套件已掌握、维护手册未学）可抄；公开作业资料两条都是登记默认知识，禁用写“已全部掌握”。
    /// 抄录 30 分钟 0 材料后学会维护手册；不写核心记录/任务/通关/容量事实，不发物、不给经验。
    /// </summary>
    [Test]
    public void EXQ_A04_H03Copy_LearnsUnknownOnly_NoFakeClear()
    {
        var s = At("h03_workshop");
        var v = E.CityServicesOf(s, "npc_luo_zheng").Single(x => x.Id == "copy_public_knowledge");
        Assert.That((v.Title, v.TimeText, v.CostText, v.Blocked), Is.EqualTo(("查抄公开资料", "30分钟", "无额外耗材/费用", (string?)null)));
        Assert.That(v.Choices.Select(c => c.Label), Is.EqualTo(new[]
        {
            "公开资料：净水操作套件的做法（已掌握）、维护手册蓝图",
            "公开作业资料：采集手套的做法（已掌握）、作业长裤的做法（已掌握）",
        }));
        Assert.That(v.Choices[0].Blocked, Is.Null);
        Assert.That(v.Choices[1].Blocked, Does.StartWith("已全部掌握"));
        Assert.That(E.StartCityService(s, "copy_public_knowledge", null, "kp_mining").Ok, Is.False, "完全已知的包禁用");
        Assert.That(E.StartCityService(s, "copy_public_knowledge").Reason, Is.EqualTo("先选一项"));

        var flags = s.Progress.Flags.ToHashSet();
        var items = s.Items.Sum(i => i.Count);
        var xp = s.Skills.Sum(kv => kv.Value.Xp);
        var cap = E.BattleCapacity(s);
        var t0 = s.GameMinute;
        Ok(E.StartCityService(s, "copy_public_knowledge", null, "kp_waterworks"));
        Run(s);
        ReadDialogues(s);
        Assert.That(s.GameMinute - t0, Is.EqualTo(30));
        Assert.That(E.KnowsKnowledge(s, "blueprint_repair_manual"), Is.True);
        Assert.That(s.Log.Any(l => l.Text.Contains("抄录公开资料，学会做法知识：维护手册蓝图")), Is.True);
        Assert.That(s.Progress.Flags.Except(flags), Is.EquivalentTo(new[] { "kpcopy:kp_waterworks" }), "只写抄录事实");
        Assert.That(E.CampaignClaimed(s, "d05_07_unique") || GameEngine.QuestDone(s, "EXQ05") || s.DungeonInstances.ContainsKey("d05"), Is.False, "不伪造记录/任务/通关");
        Assert.That((s.Items.Sum(i => i.Count), s.Skills.Sum(kv => kv.Value.Xp), E.BattleCapacity(s)), Is.EqualTo((items, xp, cap)));
        v = E.CityServicePreview(s, "copy_public_knowledge")!;
        Assert.That(v.Blocked, Does.StartWith("已全部掌握"), "两包都已知后整项禁用");
    }

    /// <summary>h04 陈芷“查抄公开资料”只有物资登记资料一包：不选也直接用它；学会配给袋蓝图。h02 许安的公共告示资料两条都是登记默认知识：整项禁用。</summary>
    [Test]
    public void H04Copy_SinglePack_H02AllKnownDisabled()
    {
        var s = At("h04_infirmary");
        var v = E.CityServicesOf(s, "npc_chen_zhi").Single(x => x.Id == "copy_public_knowledge_h04");
        Assert.That((v.Title, v.Choices.Single().Label), Is.EqualTo(("查抄公开资料", "物资登记资料：配给袋蓝图")));
        Ok(E.StartCityService(s, "copy_public_knowledge_h04"));
        Run(s);
        ReadDialogues(s);
        Assert.That(E.KnowsKnowledge(s, "blueprint_ration_pouch"), Is.True);
        Assert.That(GameEngine.QuestDone(s, "EXQ08"), Is.False);

        E.DebugMapTeleport(s, "h02_square");
        ReadDialogues(s);
        var h02 = E.CityServicesOf(s, "npc_xu_an").Single(x => x.Id == "copy_public_knowledge_h02");
        Assert.That(h02.Blocked, Does.StartWith("已全部掌握"));
        Assert.That(E.StartCityService(s, "copy_public_knowledge_h02").Ok, Is.False);
    }

    /// <summary>未登记：抄录写“需要：完成营区登记…”不能办（A11“登记后”）。</summary>
    [Test]
    public void Copy_RequiresRegistration()
    {
        var s = At("h04_infirmary", registered: false);
        var v = E.CityServicesOf(s, "npc_chen_zhi").Single(x => x.Id == "copy_public_knowledge_h04");
        Assert.That(v.Blocked, Does.StartWith("需要：完成营区登记"));
        Assert.That(E.StartCityService(s, "copy_public_knowledge_h04").Ok, Is.False);
    }

    /// <summary>Q33 通用事务：抄录中途停下不授知识、已过时间不退；途中从别处学会（领了冷链调度档案），完成时重验失败、不重复写。</summary>
    [Test]
    public void Copy_CancelNoEffect_TimeKept_RecheckOnComplete()
    {
        var s = At("h04_infirmary");
        var t0 = s.GameMinute;
        Ok(E.StartCityService(s, "copy_public_knowledge_h04"));
        Steps(s, 2);
        Ok(E.Cancel(s));
        Assert.That(s.GameMinute - t0, Is.EqualTo(10), "时间不退");
        Assert.That(E.KnowsKnowledge(s, "blueprint_ration_pouch"), Is.False);
        Assert.That(s.Progress.Flags, Does.Not.Contain("kpcopy:kp_coldchain"));

        Ok(E.StartCityService(s, "copy_public_knowledge_h04"));
        s.Progress.Flags.Add("claim:d08_08_unique");   // 模拟途中从别处学会
        Run(s);
        ReadDialogues(s);
        Assert.That(s.Log.Any(l => l.Text.Contains("查抄公开资料没有完成：这些做法已全部掌握")), Is.True);
        Assert.That(s.Progress.Flags, Does.Not.Contain("kpcopy:kp_coldchain"));
    }

    /// <summary>抄录后再领地牢记录：知识已知，本次用于保存调查记录（任务照常“记录已取得”，不兑换别的）。</summary>
    [Test]
    public void CopyThenClaim_RecordOnly()
    {
        var s = At("h04_infirmary");
        Ok(E.StartCityService(s, "copy_public_knowledge_h04"));
        Run(s);
        ReadDialogues(s);
        E.DebugMapTeleport(s, "d08_08");
        E.DebugSetProgress(s, "d08_08", 100);
        ReadDialogues(s);
        Assert.That(E.Preview(s, ActionRequest.Gather("d08_08_unique")).Notes, Has.Some.EqualTo("知识已知，本次用于保存调查记录"));
    }
}
