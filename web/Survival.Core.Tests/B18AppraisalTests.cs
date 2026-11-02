using Newtonsoft.Json.Linq;
using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 批次 B18（R57 鉴定与教学保护、R58 有界随机以物换物、R59 鉴定交易界面、代班与旧档）。
/// 规则来源：R07「X3 营区有限鉴定与活性物质换物」X3.1—X3.7、「X3互操作验收补充」；S01「鉴定交易服务与跨日请求」「世界日报价与服务快照」；
/// S06 X4；A11 Q33 appraisal_white_v1；数据 appraisal-service-contract.json。验收 AT01—AT17、COMP13、CITY-N04。
/// </summary>
public class B18AppraisalTests : TestBase
{
    private static JObject Design(string name) => JObject.Parse(File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, name)));

    private void Ok(ActionResult r) => Assert.That(r.Ok, Is.True, r.Reason);

    private const string Sub = "active_substance_white_unidentified";

    /// <summary>一份普通（有来源字段）白色活性物质。</summary>
    private ItemInstance Ordinary(GameState s, string owner, string grant = "substance-b9-w1", int count = 1) =>
        E.CreateItems(s, Sub, count, owner, false, new ItemSource { WorldEnemyId = 1, BattleId = 9, GrantEventId = grant }).Single();

    // ───────────── 配置（DesignSync） ─────────────

    /// <summary>R07 X3.2/X3.3：config.appraisal 与 appraisal-service-contract.json 逐项一致；服务已实装（Q33：沿原契约，不叠收本表费用）。</summary>
    [Category("DesignSync")]
    [Test]
    public void AT_Config_FromContract()
    {
        var c = Design("design.appraisal-contract.json");
        var a = Cfg.Appraisal;
        Assert.That(a.ServiceId, Is.EqualTo((string)c["serviceId"]!));
        Assert.That(a.LocationId, Is.EqualTo((string)c["locationId"]!));
        Assert.That(a.QuoteVersion, Is.EqualTo("white_barter_v2"));
        Assert.That(a.InfoMinutes, Is.EqualTo(0));
        Assert.That(a.AppraiseMinutes, Is.EqualTo(10));
        Assert.That(a.AppraiseConsume, Is.EquivalentTo(new Dictionary<string, int> { ["fiber"] = 1 }));
        Assert.That(a.TradeMinutes, Is.EqualTo(5));
        Assert.That(a.TradeDailyPartyQuota, Is.EqualTo(2));
        Assert.That(a.SampleItemId, Is.EqualTo(Sub));
        Assert.That(a.ScopeVersion, Is.EqualTo("r07_white_v1"));
        var offers = (JArray)c["offers"]!;
        Assert.That(a.Offers.Select(o => (o.Id, o.ItemId, o.Min, o.Max, o.MinGrams, o.MaxGrams)),
            Is.EqualTo(offers.Select(o => ((string)o["id"]!, (string)o["itemId"]!, (int)o["inclusiveQuantityBounds"]![0]!, (int)o["inclusiveQuantityBounds"]![1]!,
                (int)o["inclusiveWeightGrams"]![0]!, (int)o["inclusiveWeightGrams"]![1]!))));
        // 重量区间 = 份数区间 × 物品单份重量（AT13 “重量闭区间”与物品定义一致）
        foreach (var o in a.Offers)
        {
            var g = Cfg.Items.Single(i => i.Id == o.ItemId).WeightGrams;
            Assert.That((o.Min * g, o.Max * g), Is.EqualTo((o.MinGrams, o.MaxGrams)), o.Id);
        }
        var svc = Cfg.City.Services.Single(x => x.Id == a.ServiceId);
        Assert.That(svc.Implemented, Is.True);
        Assert.That(svc.Inputs, Is.Empty, "Q33：不在城市服务表里重复收费");
    }

    // ───────────── 鉴定记录字段与合堆（R07 X3.4、S06 X4） ─────────────

    /// <summary>AT12：已鉴定份与未鉴定份不合堆、真实批次键不同；来源字段原样。</summary>
    [Test]
    public void AT12_AppraisedPortionDoesNotMergeWithUnappraised()
    {
        var s = New();
        var a = Ordinary(s, s.Ground, "g1");
        var b = Ordinary(s, s.Ground, "g1", 1);
        Assert.That(s.Items.Count(i => i.ItemId == Sub), Is.EqualTo(1), "同来源未鉴定照常合堆");
        var stack = s.Items.Single(i => i.ItemId == Sub);
        Assert.That(stack.Count, Is.EqualTo(2));
        var one = stack.Clone();
        one.InstanceId = s.NextInstanceId++;
        one.Count = 1;
        stack.Count = 1;
        one.Appraisal = new AppraisalRecord { InstanceId = one.InstanceId, ScopeVersion = "r07_white_v1", Result = AppraisalRecord.OrdinaryNoEffect, Minute = s.GameMinute };
        s.Items.Add(one);
        E.CreateItems(s, Sub, 1, s.Ground, false, new ItemSource { GrantEventId = "g1" });
        var subs = s.Items.Where(i => i.ItemId == Sub).ToList();
        Assert.That(subs.Count, Is.EqualTo(2), "已鉴定的一份单独成堆");
        Assert.That(subs.Single(i => i.Appraisal != null).Count, Is.EqualTo(1));
        Assert.That(subs.Single(i => i.Appraisal == null).Count, Is.EqualTo(2));
        Assert.That(GameEngine.BatchKeyOf(subs[0]), Is.Not.EqualTo(GameEngine.BatchKeyOf(subs[1])));
        Assert.That(subs.All(i => i.Source!.GrantEventId == "g1"), Is.True);
        _ = a; _ = b;
    }

    /// <summary>R59 旧档：29→30 只补空报价表，旧活性物质一律未鉴定、来源与教学标记不变；日志一条；幂等。</summary>
    [Test]
    public void AT_Migration29To30_RecordsEmpty_SourceKept()
    {
        var s = New();
        var o = Ordinary(s, s.Ground);
        var t = E.CreateItems(s, Sub, 1, ItemInstance.Carried, false, new ItemSource { GrantEventId = "tutorial-reward-b1", TutorialReward = true }).Single();
        var json = SaveSerializer.Serialize(s).Replace($"\"SchemaVersion\":\"{GameState.CurrentSchema}\"", "\"SchemaVersion\":\"29\"")
            .Replace(",\"Appraisal\":null", "").Replace(",\"Quotes\":[]", "");
        Assert.That(json, Does.Not.Contain("\"Quotes\""));
        var m = SaveSerializer.Deserialize(json, Cfg);
        Assert.That(m.SchemaVersion, Is.EqualTo(GameState.CurrentSchema));
        Assert.That(m.Items.Where(i => i.ItemId == Sub).All(i => i.Appraisal == null), Is.True);
        Assert.That(E.FindItem(m, t.InstanceId)!.Source!.TutorialReward, Is.True);
        Assert.That(E.FindItem(m, o.InstanceId)!.Source!.GrantEventId, Is.EqualTo(o.Source!.GrantEventId));
        Assert.That(m.City.Quotes, Is.Empty, "旧档不补抽报价表，下一次安全访问再生成");
        Assert.That(m.Log.Count(l => l.Text.Contains($"版本{GameState.CurrentSchema}")), Is.EqualTo(1));
        var again = SaveSerializer.Deserialize(SaveSerializer.Serialize(m), Cfg);
        Assert.That(again.Log.Count(l => l.Text.Contains($"版本{GameState.CurrentSchema}")), Is.EqualTo(1));
    }

    // ───────────── R57 详细鉴定与教学保护 ─────────────

    /// <summary>在鉴定所、随身有植物纤维 n 份。</summary>
    private GameState AtOffice(int fiber = 2)
    {
        var s = New();
        E.DebugMapTeleport(s, "h05_appraisal");
        ReadDialogues(s);
        s.Items.RemoveAll(i => i.ItemId == "fiber");   // 开局物资里的纤维不计，只用这里给的
        if (fiber > 0) E.CreateItems(s, "fiber", fiber, ItemInstance.Carried);
        return s;
    }

    private static string SkillsJson(GameState s) => Newtonsoft.Json.JsonConvert.SerializeObject(s.Skills);

    /// <summary>AT01/AT04：普通详细鉴定 10 分钟，完成只扣纤维1、写 ordinary_no_effect 记录；样本不消耗、owner/来源不变，不加经验；读档不重抽、再点禁用。</summary>
    [Test]
    public void AT01_AT04_OrdinaryAppraisal_RecordOnly_FiberOnce_NoXp()
    {
        var s = AtOffice();
        var o = Ordinary(s, ItemInstance.Carried);
        var skills = SkillsJson(s);
        var t0 = s.GameMinute;
        var v = E.AppraisalPreview(s);
        Assert.That(v.Blocked, Is.Null);
        Assert.That(v.FiberBlocked, Is.Null);
        var sv = v.Samples.Single(x => x.InstanceId == o.InstanceId);
        Assert.That((sv.SourceText, sv.QualityText, sv.AppraisalText, sv.AppraiseBlocked), Is.EqualTo(("普通丧尸回收", "白色", "未鉴定", (string?)null)));
        Assert.That(sv.FusionText, Does.Contain("无属性变化"));
        Ok(E.StartAppraise(s, o.InstanceId));
        Assert.That(E.FindItem(s, o.InstanceId)!.ReservedBy, Is.Not.Null, "开工预留所选样本");
        Assert.That(E.AvailableCount(s, "fiber", true), Is.EqualTo(1), "纤维预留 1");
        Run(s);
        ReadDialogues(s);
        Assert.That(s.GameMinute - t0, Is.EqualTo(10));
        var after = E.FindItem(s, o.InstanceId)!;
        Assert.That(after.Count, Is.EqualTo(1), "样本不消耗");
        Assert.That(after.Owner, Is.EqualTo(ItemInstance.Carried), "owner 不变");
        Assert.That(after.ReservedBy, Is.Null);
        Assert.That(after.Source!.GrantEventId, Is.EqualTo("substance-b9-w1"));
        Assert.That(after.Appraisal, Is.Not.Null);
        Assert.That((after.Appraisal!.InstanceId, after.Appraisal.ScopeVersion, after.Appraisal.Result, after.Appraisal.Minute),
            Is.EqualTo((o.InstanceId, "r07_white_v1", AppraisalRecord.OrdinaryNoEffect, t0 + 10)));
        Assert.That(s.Items.Where(i => i.ItemId == "fiber").Sum(i => i.Count), Is.EqualTo(1), "只扣纤维1");
        Assert.That(SkillsJson(s), Is.EqualTo(skills), "不给经验");
        Assert.That(s.Traits, Is.Empty, "不添词条");
        Assert.That(s.Log.Last(l => l.Text.Contains("详细鉴定完成")).Text, Does.Contain("不升品"));
        // 读档：记录与样本原样，再点鉴定禁用、不重复收费
        var m = SaveSerializer.Deserialize(SaveSerializer.Serialize(s), Cfg);
        var mi = E.FindItem(m, o.InstanceId)!;
        Assert.That(mi.Appraisal!.Result, Is.EqualTo(AppraisalRecord.OrdinaryNoEffect));
        var mv = E.AppraisalPreview(m).Samples.Single(x => x.InstanceId == o.InstanceId);
        Assert.That(mv.AppraiseBlocked, Does.Contain("已记录"));
        Assert.That(mv.AppraisalText, Does.StartWith("已鉴定：普通白色，融合无属性"));
        Assert.That(E.StartAppraise(m, o.InstanceId).Ok, Is.False);
        Assert.That(m.Items.Where(i => i.ItemId == "fiber").Sum(i => i.Count), Is.EqualTo(1));
    }

    /// <summary>AT04：中途取消——已过时间保留，样本与纤维都不扣、原处、无记录；不套制作返地。</summary>
    [Test]
    public void AT04_CancelMidway_NothingDeducted()
    {
        var s = AtOffice(1);
        var o = Ordinary(s, ItemInstance.Carried);
        Ok(E.StartAppraise(s, o.InstanceId));
        Steps(s, 1);
        Ok(E.Cancel(s));
        Assert.That(s.Log.Last().Text, Does.Contain("样本与植物纤维都未扣"));
        var i = E.FindItem(s, o.InstanceId)!;
        Assert.That((i.Owner, i.ReservedBy, i.Appraisal), Is.EqualTo((ItemInstance.Carried, (int?)null, (AppraisalRecord?)null)));
        var fiber = s.Items.Single(x => x.ItemId == "fiber");
        Assert.That((fiber.Count, fiber.Owner, fiber.ReservedBy), Is.EqualTo((1, ItemInstance.Carried, (int?)null)), "纤维原物原处");
        Assert.That(s.City.Pending, Is.Null);
    }

    /// <summary>纤维不足不能鉴定（0 扣费 0 时间）；无来源字段的样本不受理；不在鉴定所不能办。</summary>
    [Test]
    public void AT_FiberMissing_NoSource_Remote_Rejected()
    {
        var s = AtOffice(0);
        var o = Ordinary(s, ItemInstance.Carried);
        var t0 = s.GameMinute;
        Assert.That(E.AppraisalPreview(s).FiberBlocked, Does.Contain("植物纤维0/1"));
        var r = E.StartAppraise(s, o.InstanceId);
        Assert.That(r.Ok, Is.False);
        Assert.That(r.Reason, Does.Contain("植物纤维"));
        Assert.That((s.GameMinute, s.Main), Is.EqualTo((t0, (ActionState?)null)));
        E.CreateItems(s, "fiber", 1, s.Ground);
        Assert.That(E.AppraisalPreview(s).FiberBlocked, Is.Null, "当地地面的纤维也可用");
        var bare = E.CreateItems(s, Sub, 1, ItemInstance.Carried).Single();
        Assert.That(E.StartAppraise(s, bare.InstanceId).Reason, Does.Contain("来源不明"));
        E.DebugMapTeleport(s, "h02_square");
        Assert.That(E.StartAppraise(s, o.InstanceId).Reason, Does.Contain("须实际到达"));
        Assert.That(E.AppraisalPreview(s).Blocked, Does.Contain("须实际到达"));
    }

    /// <summary>AT02/AT03：教学样本不能付费鉴定，免费说明 0 分钟写 fixed_tutorial_attack 记录、不扣纤维；原入口直接融合仍得唯一绿色+0.1。</summary>
    [Test]
    public void AT02_AT03_TutorialFreeNote_NoPaidAppraisal_FusionStillWorks()
    {
        var s = NewStory();
        E.DebugMapTeleport(s, "h05_appraisal");
        ReadDialogues(s);
        s.Items.RemoveAll(i => i.ItemId == "fiber");
        E.CreateItems(s, "fiber", 1, ItemInstance.Carried);
        var t = E.CreateItems(s, Sub, 1, ItemInstance.Carried, false, new ItemSource { GrantEventId = "tutorial-reward-b1", TutorialReward = true }).Single();
        var sv = E.AppraisalPreview(s).Samples.Single();
        Assert.That(sv.SourceText, Is.EqualTo("首战教学"));
        Assert.That(sv.FusionText, Does.Contain("固定绿色词条").And.Contain("白色品质"));
        Assert.That(sv.AppraiseBlocked, Does.Contain("教学样本不用付费鉴定"));
        Assert.That(E.StartAppraise(s, t.InstanceId).Ok, Is.False, "服务端重验，不只靠前端禁按钮");
        var t0 = s.GameMinute;
        Ok(E.AppraisalTutorialNote(s, t.InstanceId));
        Assert.That(s.GameMinute, Is.EqualTo(t0), "0 分钟");
        Assert.That(E.FindItem(s, t.InstanceId)!.Appraisal!.Result, Is.EqualTo(AppraisalRecord.FixedTutorialAttack));
        Assert.That(s.Items.Single(i => i.ItemId == "fiber").Count, Is.EqualTo(1), "不扣纤维");
        Ok(E.AppraisalTutorialNote(s, t.InstanceId));
        Assert.That(s.Log.Last().Text, Does.Contain("回看不扣任何东西"));
        // 不经付费鉴定，原入口直接融合得唯一绿色 +0.1
        Start(s, ActionRequest.Fuse(t.InstanceId));
        Run(s);
        ReadDialogues(s);
        Assert.That(s.Traits.Single().Label, Is.EqualTo(Cfg.ActiveSubstance.TutorialEffect.Label));
    }

    /// <summary>AT12：数量大于 1 的堆只鉴定所选 1 份（拆出），记录只跟这 1 份，未鉴定部分不被误标，来源不丢。</summary>
    [Test]
    public void AT12_StackSplit_RecordFollowsPortion()
    {
        var s = AtOffice(1);
        var o = Ordinary(s, s.Ground, "dev-substance-1", 3);
        Ok(E.StartAppraise(s, o.InstanceId));
        Run(s);
        ReadDialogues(s);
        var subs = s.Items.Where(i => i.ItemId == Sub).ToList();
        Assert.That(subs.Sum(i => i.Count), Is.EqualTo(3));
        Assert.That(subs.Single(i => i.Appraisal != null).Count, Is.EqualTo(1));
        Assert.That(subs.Single(i => i.Appraisal == null).Count, Is.EqualTo(2));
        Assert.That(subs.All(i => i.Source!.GrantEventId == "dev-substance-1" && i.Owner == s.Ground), Is.True);
    }
}
