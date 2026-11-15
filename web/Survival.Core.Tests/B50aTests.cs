using Newtonsoft.Json.Linq;
using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 批次 B50a「保质、腐败来源与晾制」（开发记录 B50a）：S06／S11／S14 文首「2026-11-15 BM5b2 Q666—Q671」与「2026-11-15 Q711—Q719」的 B50a 部分
/// （Q666、Q667、Q669、Q671、Q711—Q715；Q668 由 Q712 取代）。正式句见《重墨 V2 开工裁定与界面逐字稿》同两节；网页壳接线以源码检查。
/// 柜子数量操作（Q670、Q716—Q719）与地面／容器牌组（Q717）属 B50b，不在这里。
/// </summary>
public class B50aTests : TestBase
{
    protected override bool NoEnemyApproach => true;

    private const string Rot = GameEngine.SpoiledMatterId;

    private static string Web(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "Survival.Web" }.Concat(parts).ToArray()));

    private List<ItemInstance> Make(GameState s, string itemId, int count, string owner, int? expires = null)
    {
        var made = E.CreateItems(s, itemId, count, owner);
        if (expires.HasValue) foreach (var i in made) i.ExpiresAt = expires;
        return made;
    }

    /// <summary>空闲等待到指定分钟（按 5 分钟段推进）。</summary>
    private void WaitUntil(GameState s, int minute)
    {
        while (s.GameMinute < minute)
        {
            if (s.Main == null) Start(s, ActionRequest.Wait(30));
            E.Advance(s);
        }
        if (s.Main?.Type == ActionType.Wait) E.Cancel(s);
    }

    /// <summary>全局来源账：原物 ID → 全部腐败物上的已知份数合计。</summary>
    private static Dictionary<string, int> Ledger(GameState s) =>
        s.Items.Where(i => i.SpoiledOrigins != null).SelectMany(i => i.SpoiledOrigins!)
            .GroupBy(kv => kv.Key).ToDictionary(g => g.Key, g => g.Sum(kv => kv.Value));

    private static void AssertLedgerValid(GameState s)
    {
        foreach (var i in s.Items.Where(i => i.SpoiledOrigins != null))
        {
            Assert.That(i.ItemId, Is.EqualTo(Rot), "只有腐败物带来源账");
            Assert.That(i.SpoiledOrigins!.Values.All(v => v > 0), Is.True, "账内份数为正");
            Assert.That(i.SpoiledOrigins!.Values.Sum(), Is.LessThanOrEqualTo(i.Count), $"#{i.InstanceId} 已知份数不超过总份数");
        }
    }

    private static string AsSchema(GameState s, string version, Action<JObject>? edit = null)
    {
        var o = JObject.Parse(SaveSerializer.Serialize(s));
        o["SchemaVersion"] = version;
        edit?.Invoke(o);
        return o.ToString();
    }

    // ───────────── M946 存档 53 骨架 ─────────────

    [Test]
    public void M946_Schema53_CloneDeepCopiesLedger_StopReasonField()
    {
        Assert.That(GameState.CurrentSchema, Is.EqualTo("53"));
        var i = new ItemInstance { ItemId = Rot, Count = 3, SpoiledOrigins = new Dictionary<string, int> { ["raw_meat"] = 1, ["cooked_meat"] = 2 } };
        var c = i.Clone();
        c.SpoiledOrigins!["raw_meat"] = 9;
        Assert.That(i.SpoiledOrigins!["raw_meat"], Is.EqualTo(1), "Clone 深拷来源账");
        var b = new DryingSlot { StopReason = DryingSlot.StopMissing };
        Assert.That(b.Clone().StopReason, Is.EqualTo("missing"));
    }

    /// <summary>Q711-A03／Q763（待定，按现状）：52 档的腐败物只有单一 SpoiledFrom，迁移不建来源账、不拆堆、不改重量；SpoiledFrom 原样保留。</summary>
    [Test]
    public void M946_Migrate52_OldSpoiledFromNotTrusted_Kept()
    {
        var s = New();
        Make(s, "raw_meat", 2, "camp", s.GameMinute + 5);
        WaitUntil(s, s.GameMinute + 5);
        var rot = s.Items.Single(i => i.ItemId == Rot && i.Owner == "camp");
        var json = AsSchema(s, "52", o =>
        {
            foreach (var it in o["Items"]!) ((JObject)it).Remove("SpoiledOrigins");
        });
        var t = SaveSerializer.Deserialize(json, Cfg);
        Assert.That(t.SchemaVersion, Is.EqualTo("53"));
        var r = t.Items.Single(i => i.InstanceId == rot.InstanceId);
        Assert.That((r.SpoiledFrom, r.SpoiledOrigins, r.Count, r.SpoiledUnitGrams), Is.EqualTo(("raw_meat", (Dictionary<string, int>?)null, 2, rot.SpoiledUnitGrams)));
        Assert.That(t.Log.Count, Is.EqualTo(s.Log.Count), "静默迁移，不写说明（Q714）");
    }
}
