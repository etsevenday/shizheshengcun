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
}
