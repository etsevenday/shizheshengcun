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

    // ───────────── R58 世界日报价表 ─────────────

    /// <summary>AT13：三档输出只落在闭整数区间，每档各值近似等概率（按世界日统计），种子相同则结果相同。</summary>
    [Test]
    public void AT13_QuoteBoundsAndUniform()
    {
        foreach (var o in Cfg.Appraisal.Offers)
        {
            var counts = new Dictionary<int, int>();
            const int days = 1200;
            for (var d = 1; d <= days; d++)
            {
                var n = GameEngine.QuoteQuantity("12345", Cfg.Appraisal.ServiceId, d, o, Cfg.Appraisal.QuoteVersion);
                Assert.That(n, Is.InRange(o.Min, o.Max), o.Id);
                counts[n] = counts.TryGetValue(n, out var c) ? c + 1 : 1;
                Assert.That(GameEngine.QuoteQuantity("12345", Cfg.Appraisal.ServiceId, d, o, Cfg.Appraisal.QuoteVersion), Is.EqualTo(n), "同种子同结果");
            }
            var k = o.Max - o.Min + 1;
            Assert.That(counts.Keys.OrderBy(x => x), Is.EqualTo(Enumerable.Range(o.Min, k)), o.Id);
            foreach (var kv in counts) Assert.That(kv.Value, Is.InRange(days / k * 0.85, days / k * 1.15), $"{o.Id} 数量{kv.Key}");
        }
    }

    /// <summary>
    /// AT14/COMP13：当日首次安全到达即锁完整三档并存档；之后开窗、读档、离开再来、代班都只读存档里的表（用改过的存档值验证没有重算）；
    /// 午夜后不自动换表，下一次安全访问才生成新一日的表，旧表不被改写。
    /// </summary>
    [Test]
    public void AT14_COMP13_QuoteLockedOnFirstSafeVisit_NoReroll()
    {
        var s = New();
        Assert.That(s.City.Quotes, Is.Empty);
        E.DebugMapTeleport(s, "h05_appraisal");
        ReadDialogues(s);
        var day = Clock.Day(s.GameMinute);
        var snap = s.City.Quotes.Single();
        Assert.That((snap.Day, snap.QuoteVersion, snap.ServiceId, snap.CampaignId), Is.EqualTo((day, "white_barter_v2", "appraisal_white_v1", s.Seed.ToString())));
        Assert.That(snap.Offers.Keys, Is.EquivalentTo(new[] { "white_for_cloth", "white_for_scrap", "white_for_water" }));
        // 把存档里的值改成区间外的哨兵：之后任何途径都只回放它
        snap.Offers["white_for_cloth"] = 99;
        Assert.That(E.AppraisalPreview(s).Offers.Single(o => o.OfferId == "white_for_cloth").Quantity, Is.EqualTo(99), "开窗只读存档");
        E.EnsureAppraisalQuotes(s);
        var m = SaveSerializer.Deserialize(SaveSerializer.Serialize(s), Cfg);
        E.EnsureAppraisalQuotes(m);
        Assert.That(m.City.Quotes.Single().Offers["white_for_cloth"], Is.EqualTo(99), "读档不重抽");
        E.DebugToggleCityPostAbsent(m);
        Assert.That(E.AppraisalPreview(m).Offers.Single(o => o.OfferId == "white_for_cloth").Quantity, Is.EqualTo(99), "代班不重抽");
        E.DebugMapTeleport(m, "h02_square");
        E.DebugMapTeleport(m, "h05_appraisal");
        ReadDialogues(m);
        Assert.That(m.City.Quotes.Single().Offers["white_for_cloth"], Is.EqualTo(99), "同日离开再来不重抽");
        // 跨午夜留在鉴定所：不自动换表；下一次安全访问（打开面板）才生成新日表
        Start(m, ActionRequest.Wait(60));
        m.GameMinute = day * Clock.MinutesPerDay - 30;
        Run(m);
        Assert.That(Clock.Day(m.GameMinute), Is.EqualTo(day + 1));
        Assert.That(m.City.Quotes.Count, Is.EqualTo(1), "午夜不自动生成");
        Assert.That(E.AppraisalPreview(m).QuoteLocked, Is.False);
        var next = E.EnsureAppraisalQuotes(m)!;
        Assert.That(next.Day, Is.EqualTo(day + 1));
        Assert.That(m.City.Quotes.Single(q => q.Day == day).Offers["white_for_cloth"], Is.EqualTo(99), "新日表不改旧表");
        Assert.That(next.Offers.Values.All(n => n is >= 1 and <= 3), Is.True);
        // 不在鉴定所时不生成
        var far = New();
        Assert.That(E.EnsureAppraisalQuotes(far), Is.Null);
        Assert.That(far.City.Quotes, Is.Empty);
    }

    // ───────────── R58 以物换物事务 ─────────────

    private int Ground(GameState s, string id) => s.Items.Where(i => i.ItemId == id && i.Owner == s.Ground).Sum(i => i.Count);

    private void Trade(GameState s, ItemInstance sample, string offer)
    {
        Ok(E.StartAppraisalTrade(s, sample.InstanceId, offer));
        Run(s);
        ReadDialogues(s);
    }

    /// <summary>AT05：布 1 包 + 水 1 包后再点废金属——全队两包共享已满被拒；换角色/代班/离开再来/读档不重置；输出按锁定份数落地面、样本消耗 1。</summary>
    [Test]
    public void AT05_TwoPacksSharedPerDay_NotResetByNpcOrReload()
    {
        var s = AtOffice(0);
        var a = Ordinary(s, ItemInstance.Carried, "g-a");
        var b = Ordinary(s, ItemInstance.Carried, "g-b");
        var c = Ordinary(s, ItemInstance.Carried, "g-c");
        var snap = s.City.Quotes.Single();
        var v = E.AppraisalPreview(s);
        Assert.That((v.QuotaTotal, v.QuotaUsed, v.QuotaReserved, v.QuotaAvailable, v.TradeBlocked), Is.EqualTo((2, 0, 0, 2, (string?)null)));
        var cloth0 = Ground(s, "cloth");
        var t0 = s.GameMinute;
        Trade(s, a, "white_for_cloth");
        Assert.That(s.GameMinute - t0, Is.EqualTo(5));
        Assert.That(E.FindItem(s, a.InstanceId), Is.Null, "样本 1 份交出");
        Assert.That(Ground(s, "cloth") - cloth0, Is.EqualTo(snap.Offers["white_for_cloth"]), "按锁定的确切份数");
        var water0 = Ground(s, "clean_water");
        Trade(s, b, "white_for_water");
        Assert.That(Ground(s, "clean_water") - water0, Is.EqualTo(snap.Offers["white_for_water"]));
        var r = E.StartAppraisalTrade(s, c.InstanceId, "white_for_scrap");
        Assert.That(r.Ok, Is.False);
        Assert.That(r.Reason, Does.Contain("额度已用完"));
        Assert.That(E.AppraisalPreview(s).TradeBlocked, Does.Contain("额度已用完"));
        E.DebugToggleCityPostAbsent(s);
        Assert.That(E.StartAppraisalTrade(s, c.InstanceId, "white_for_scrap").Ok, Is.False, "代班不重置");
        E.DebugMapTeleport(s, "h02_square");
        E.DebugMapTeleport(s, "h05_appraisal");
        ReadDialogues(s);
        var m = SaveSerializer.Deserialize(SaveSerializer.Serialize(s), Cfg);
        Assert.That(E.StartAppraisalTrade(m, c.InstanceId, "white_for_scrap").Ok, Is.False, "读档不重置");
        Assert.That(E.FindItem(m, c.InstanceId)!.ReservedBy, Is.Null);
    }

    /// <summary>
    /// AT06/AT15：23:55 开始的 5 分钟交易在午夜完成——发开始日的 exactOutput、只扣旧日桶；新日照常 2 包；
    /// 下一次安全访问生成新日表，不改旧事务；取消不把旧余额转到新日。
    /// </summary>
    [Test]
    public void AT06_AT15_CrossMidnight_StartDayQuoteAndBucket()
    {
        var s = AtOffice(0);
        var day = Clock.Day(s.GameMinute);
        var a = Ordinary(s, ItemInstance.Carried, "g-a");
        var b = Ordinary(s, ItemInstance.Carried, "g-b");
        var old = s.City.Quotes.Single();
        old.Offers["white_for_scrap"] = 2;   // 固定一个可辨认的数，验证跨午夜仍按开始日的报价
        s.GameMinute = day * Clock.MinutesPerDay - 5;   // 第 day 日 23:55
        Ok(E.StartAppraisalTrade(s, a.InstanceId, "white_for_scrap"));
        var txn = s.City.Pending!;
        Assert.That((txn.StartDay, txn.QuoteDay, txn.QuoteVersion, txn.OfferId, txn.ExactOutput, txn.SampleInstanceId, txn.SampleOwner, txn.QuotaReserved),
            Is.EqualTo((day, day, "white_barter_v2", "white_for_scrap", 2, (int?)a.InstanceId, (string?)ItemInstance.Carried, true)), "事务锁字段");
        Assert.That(txn.TxnId, Is.GreaterThan(0));
        Assert.That(E.AppraisalPreview(s).QuotaReserved, Is.EqualTo(1));
        var scrap0 = Ground(s, "scrap");
        Run(s);
        ReadDialogues(s);
        Assert.That(Clock.Day(s.GameMinute), Is.EqualTo(day + 1));
        Assert.That(Ground(s, "scrap") - scrap0, Is.EqualTo(2), "按开始日 exactOutput");
        Assert.That(s.City.QuotaUsed[$"appraisal_white_v1@{day}"], Is.EqualTo(1), "扣旧日桶");
        Assert.That(s.City.QuotaUsed.ContainsKey($"appraisal_white_v1@{day + 1}"), Is.False, "不扣新日");
        var v = E.AppraisalPreview(s);
        Assert.That(v.QuotaAvailable, Is.EqualTo(Cfg.Appraisal.TradeDailyPartyQuota), "新日 2 包");
        var fresh = E.EnsureAppraisalQuotes(s)!;
        Assert.That(fresh.Day, Is.EqualTo(day + 1));
        Assert.That(s.City.Quotes.Single(q => q.Day == day).Offers["white_for_scrap"], Is.EqualTo(2), "新日表不改旧表");
        // 取消：23:55 开工后马上取消，旧日额度释放，新日仍只有 2 包（不结转）
        s.GameMinute = (day + 1) * Clock.MinutesPerDay - 5;
        E.EnsureAppraisalQuotes(s);
        Ok(E.StartAppraisalTrade(s, b.InstanceId, "white_for_cloth"));
        Ok(E.Cancel(s));
        Assert.That(s.Log.Last().Text, Does.Contain("样本未交出"));
        Assert.That(E.FindItem(s, b.InstanceId)!.ReservedBy, Is.Null);
        Assert.That(s.City.QuotaUsed.ContainsKey($"appraisal_white_v1@{day + 1}"), Is.False);
        s.GameMinute = (day + 1) * Clock.MinutesPerDay;
        Assert.That(E.AppraisalPreview(s).QuotaAvailable, Is.EqualTo(2), "过期日剩余额度不结转");
    }

    /// <summary>AT07：随身五卡满且超重时换物，奖励确切落服务区地面，随身不变。</summary>
    [Test]
    public void AT07_FullPack_OutputOnGround()
    {
        var s = AtOffice(0);
        var a = Ordinary(s, s.Ground, "g-a");
        E.CreateItems(s, "scrap", 60, ItemInstance.Carried);
        var carried = s.Items.Where(i => i.IsCarried).Select(i => (i.InstanceId, i.Count)).OrderBy(x => x.InstanceId).ToList();
        Assert.That(E.IsOverweight(s), Is.True);
        var water0 = Ground(s, "clean_water");
        Trade(s, a, "white_for_water");
        Assert.That(Ground(s, "clean_water") - water0, Is.EqualTo(s.City.Quotes.Single().Offers["white_for_water"]));
        Assert.That(s.Items.Where(i => i.IsCarried).Select(i => (i.InstanceId, i.Count)).OrderBy(x => x.InstanceId), Is.EqualTo(carried), "不自动挤包");
        Assert.That(s.Log.Last(l => l.Text.Contains("换物完成")).Text, Does.Contain("不会自动入包"));
    }

    /// <summary>AT09：同 requestId 重发不重复开工；完成后读档不重复发物；中途死亡不扣样本、不出物、不扣额度。</summary>
    [Test]
    public void AT09_RequestIdempotent_ReloadOnce_DeathNoCommit()
    {
        var s = AtOffice(0);
        var a = Ordinary(s, ItemInstance.Carried, "g-a");
        var txn0 = s.City.NextTxn;
        Ok(E.StartAppraisalTrade(s, a.InstanceId, "white_for_cloth", "req-1"));
        var r = E.StartAppraisalTrade(s, a.InstanceId, "white_for_cloth", "req-1");
        Assert.That(r.Duplicate, Is.True);
        Assert.That(s.City.NextTxn, Is.EqualTo(txn0 + 1), "只建一次事务");
        var mid = SaveSerializer.Deserialize(SaveSerializer.Serialize(s), Cfg);
        Run(mid);
        ReadDialogues(mid);
        var cloth = Ground(mid, "cloth");
        var again = SaveSerializer.Deserialize(SaveSerializer.Serialize(mid), Cfg);
        Assert.That(Ground(again, "cloth"), Is.EqualTo(cloth), "完成读档不重复发物");
        Assert.That(again.City.QuotaUsed.Values.Sum(), Is.EqualTo(1));
        Assert.That(again.City.Pending, Is.Null);

        var d = AtOffice(0);
        var b = Ordinary(d, ItemInstance.Carried, "g-b");
        var cloth0 = Ground(d, "cloth");
        Ok(E.StartAppraisalTrade(d, b.InstanceId, "white_for_cloth"));
        d.Stats.Health = 0;
        Run(d);
        Assert.That(d.Progress.Dead, Is.True);
        Assert.That(E.FindItem(d, b.InstanceId), Is.Not.Null, "未完成死亡不扣样本");
        Assert.That(Ground(d, "cloth"), Is.EqualTo(cloth0));
        Assert.That(d.City.QuotaUsed.Values.Sum(), Is.EqualTo(0));
    }

    /// <summary>AT16：未知旧报价版本安全取消——原样本与 owner 保留、不出物、不扣额度，不用新表替补；确认页份数与实际一致。</summary>
    [Test]
    public void AT16_UnknownQuoteVersion_SafeCancel()
    {
        var s = AtOffice(0);
        var a = Ordinary(s, ItemInstance.Carried, "g-a");
        var offer = E.AppraisalPreview(s).Offers.Single(o => o.OfferId == "white_for_cloth");
        Ok(E.StartAppraisalTrade(s, a.InstanceId, "white_for_cloth"));
        Assert.That(s.City.Pending!.ExactOutput, Is.EqualTo(offer.Quantity), "确认页份数可兑现");
        s.City.Pending!.QuoteVersion = "white_barter_v1";
        var cloth0 = Ground(s, "cloth");
        Run(s);
        ReadDialogues(s);
        var i = E.FindItem(s, a.InstanceId)!;
        Assert.That((i.Owner, i.ReservedBy, i.Count), Is.EqualTo((ItemInstance.Carried, (int?)null, 1)));
        Assert.That(Ground(s, "cloth"), Is.EqualTo(cloth0));
        Assert.That(s.City.QuotaUsed.Values.Sum(), Is.EqualTo(0));
        Assert.That(s.Log.Last(l => l.Text.Contains("换物没有完成")).Text, Does.Contain("white_barter_v1").And.Contain("安全取消"));
    }

    /// <summary>AT03/AT11/AT17：教学样本服务端拒绝交易（不消耗、不复制）；已鉴定普通样本照常可换；换到的物资不能反向换样本，没有买回入口。</summary>
    [Test]
    public void AT03_AT11_AT17_TutorialNeverTraded_NoReverse()
    {
        var s = AtOffice(1);
        var t = E.CreateItems(s, Sub, 1, ItemInstance.Carried, false, new ItemSource { GrantEventId = "tutorial-reward-b1", TutorialReward = true }).Single();
        var o = Ordinary(s, ItemInstance.Carried, "g-a");
        var sv = E.AppraisalPreview(s).Samples.Single(x => x.InstanceId == t.InstanceId);
        Assert.That(sv.TradeBlocked, Is.EqualTo("教学样本不可交易"));
        var r = E.StartAppraisalTrade(s, t.InstanceId, "white_for_cloth");
        Assert.That((r.Ok, r.Reason), Is.EqualTo((false, "教学样本不可交易")));
        Assert.That(s.Items.Count(i => i.ItemId == Sub), Is.EqualTo(2), "不消耗、不复制");
        Ok(E.StartAppraise(s, o.InstanceId));
        Run(s);
        ReadDialogues(s);
        Trade(s, o, "white_for_cloth");
        Assert.That(E.FindItem(s, o.InstanceId), Is.Null, "已鉴定普通样本照常可换（交易不要求先鉴定，鉴定也不影响报价）");
        var cloth = s.Items.First(i => i.ItemId == "cloth" && i.Owner == s.Ground);
        Assert.That(E.StartAppraisalTrade(s, cloth.InstanceId, "white_for_cloth").Reason, Does.Contain("只收普通活性物质"));
        Assert.That(Cfg.Appraisal.Offers.Any(x => x.ItemId == Sub), Is.False, "没有买回样本的报价");
        Assert.That(Cfg.City.Services.SelectMany(x => x.Choices).SelectMany(c => c.Outputs.Keys).Contains(Sub), Is.False, "城市其他服务也不出样本");
    }
}
