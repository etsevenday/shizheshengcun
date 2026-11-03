using Newtonsoft.Json.Linq;
using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 B22 R67：四类奖励分账的数据与“地图箱/旧唯一箱复用同一 resourceId”。出处：A09「E6.1」—「E6.4」「每地牢每代固定重复产出合计」、
/// A05「2026-10-30 B4 蓝图包与旧唯一装备位置迁移」、expansion-combat.json；tools/import-b22-rewards.py。验收 EC07、EC09、COMP14、A05-C01—C03。
/// </summary>
public class B22RewardDataTests : TestBase
{
    private static JObject Combat() =>
        JObject.Parse(File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "design.expansion-combat.json")));

    private static Dictionary<string, int> Items(JToken t) => ((JObject)t).Properties().ToDictionary(p => p.Name, p => (int)p.Value);

    /// <summary>DesignSync：守卫回收 8 条、首通包 8 条、重置 4320 分钟、三种战术品数值与 expansion-combat.json 一致。</summary>
    [Category("DesignSync")]
    [Test]
    public void DesignSync_B22_SalvageFirstClearResetTactical()
    {
        var j = Combat();
        var r = Cfg.DungeonRewards;
        Assert.That(r.ResetCooldownMinutes, Is.EqualTo((int)j["lifecycle"]!["resetCooldownMinutes"]!).And.EqualTo(4320));
        var boss = (JArray)j["bossSalvage"]!;
        Assert.That(r.BossSalvage, Has.Count.EqualTo(8));
        foreach (var b in boss)
        {
            var c = r.BossSalvage.Single(x => x.NodeId == (string)b["nodeId"]!);
            Assert.That(c.Items, Is.EqualTo(Items(b["items"]!)), c.NodeId);
            Assert.That(Cfg.Map.Dungeons.Single(d => d.Id == c.DungeonId).GuardNode, Is.EqualTo(c.NodeId));
        }
        foreach (var f in (JArray)j["firstClearRewards"]!)
        {
            var c = r.FirstClear.Single(x => x.DungeonId == (string)f["dungeonId"]!);
            Assert.That(c.Items, Is.EqualTo(Items(f["items"]!)), c.DungeonId);
            Assert.That(Cfg.Map.Dungeons.Single(d => d.Id == c.DungeonId).RecordNode, Is.EqualTo(c.ClearNode));
        }
        Assert.That(r.FirstClear, Has.Count.EqualTo(8));
        var t = r.Tactical.ToDictionary(x => x.Id);
        Assert.That((t["flash_marker"].Ap, t["flash_marker"].Energy, t["flash_marker"].Noise, t["flash_marker"].MarkBonus, t["flash_marker"].Rounds, t["flash_marker"].Target), Is.EqualTo((2, 0.1, 5d, 6d, 2, "enemy")));
        Assert.That((t["brace_pack"].Ap, t["brace_pack"].Energy, t["brace_pack"].DisplaceResist, t["brace_pack"].Rounds, t["brace_pack"].Target), Is.EqualTo((1, 0d, 25d, 2, "self")));
        Assert.That((t["rally_patch"].Ap, t["rally_patch"].Energy, t["rally_patch"].PanicReduce, t["rally_patch"].PerTargetRoundCap, t["rally_patch"].Target), Is.EqualTo((2, 0d, 6d, 6d, "ally")));
        foreach (var x in r.Tactical) Assert.That(Cfg.Items.Any(i => i.Id == x.Id), x.Id);
    }

    /// <summary>EC07/E6.1：地图箱 32 条全部是资源表同一 resourceId——24 条每代普通箱（instanceId，cap 2、20 分钟）、8 条核心记录（campaignClaimId）；地牢里没有第 33 箱。</summary>
    [Test]
    public void EC07_MapRefs_ReuseSameResourceIds()
    {
        var refs = (JArray)Combat()["mapRewardRefs"]!;
        Assert.That(refs, Has.Count.EqualTo(32));
        foreach (var r in refs)
        {
            var c = E.Resource((string)r["sourceResourceId"]!);
            Assert.That(c.LocationId, Is.EqualTo((string)r["nodeId"]!));
            Assert.That(c.Outputs, Is.EqualTo(new Dictionary<string, int> { [(string)r["itemId"]!] = (int)r["quantity"]! }));
            Assert.That(c.Scope, Is.EqualTo((string)r["scope"]!));
        }
        var inDungeon = Cfg.Resources.Where(r => E.DungeonOf(r) != null).ToList();
        Assert.That(inDungeon.Count(r => r.IsInstance), Is.EqualTo(24));
        Assert.That(inDungeon.Count(r => r.IsRecord), Is.EqualTo(8));
        Assert.That(inDungeon.Where(r => !r.IsInstance && !r.IsRecord).Select(r => r.NodeId),
            Is.EquivalentTo(new[] { "shield_cache", "officer_cache", "shotgun_cache", "rifle_cache", "armor_cache", "ammo_reserve_cache" }), "其余只有 6 个旧唯一箱，没有通用 parts2 通关包");
    }

    /// <summary>EC07：每代地图普通箱只领一次——领完“本代已搜空”、再领被拒；只有合法新一代才补满。</summary>
    [Test]
    public void EC07_MapBox_OncePerGeneration()
    {
        var s = New(2201);
        E.DebugTeleport(s, "d01_04");
        E.DebugSetProgress(s, "d01_04", 100);
        Start(s, ActionRequest.Gather("d01_04_cache0"));
        Run(s);
        Assert.That(s.Items.Where(i => i.Owner == "d01_04" && i.ItemId == "cloth").Sum(i => i.Count), Is.EqualTo(2));
        Assert.That(s.Resources["d01_04_cache0"].Available, Is.EqualTo(0));
        Assert.That(E.CanStart(s, ActionRequest.Gather("d01_04_cache0")), Does.Contain("本代已搜空"));
        Assert.That(E.ResourceStock(s, E.Resource("d01_04_cache0")).Badge, Is.EqualTo("本代已搜空"));
        E.DebugTeleport(s, "c03_crypt");
        Assert.That(E.NewDungeonGeneration(s, "d01"), Is.Null);
        Assert.That(s.Resources["d01_04_cache0"].Available, Is.EqualTo(2), "新一代补满");
    }

    /// <summary>EC09/E6.4：6 个旧军械箱只迁到 d03（03 盾、07 守卫战利品箱、08 四箱），原 claimId 不变、20 分钟；野外没有同 claim 的第二份。</summary>
    [Test]
    public void EC09_LegacyBoxes_MovedToD03_NoWildCopy()
    {
        var want = new Dictionary<string, string>
        {
            ["shield_cache"] = "d03_03", ["officer_cache"] = "d03_07", ["shotgun_cache"] = "d03_08",
            ["rifle_cache"] = "d03_08", ["armor_cache"] = "d03_08", ["ammo_reserve_cache"] = "d03_08",
        };
        foreach (var (id, node) in want)
        {
            Assert.That(Cfg.Resources.Count(r => r.NodeId == id || r.LegacyAliases.Contains(id)), Is.EqualTo(1), id);
            var r = E.Resource(id);
            Assert.That((r.LocationId, r.BaseMinutes, r.IsCampaignPhysical), Is.EqualTo((node, 20, true)), id);
            Assert.That(Cfg.Map.LegacyClaims.Single(c => c.ClaimId == id).Node, Is.EqualTo(node));
        }
    }

    /// <summary>COMP14：每代固定重复产出 = 3 个实例侧箱 + 守卫回收，逐地牢与 A09 表一致；合计 11.95kg 已含侧箱 10.10kg，不另叠 parts2。</summary>
    [Test]
    public void COMP14_RepeatTotals_1195_Includes1010()
    {
        var table = new Dictionary<string, int> { ["d01"] = 1200, ["d02"] = 650, ["d03"] = 2000, ["d04"] = 800, ["d05"] = 1000, ["d06"] = 3400, ["d07"] = 1300, ["d08"] = 1600 };
        int W(string id) => Cfg.Items.Single(i => i.Id == id).WeightGrams;
        var total = 0;
        var side = 0;
        foreach (var (d, kg) in table)
        {
            var sideG = Cfg.Resources.Where(r => r.IsInstance && E.DungeonOf(r) == d).Sum(r => r.Outputs.Sum(kv => W(kv.Key) * kv.Value));
            var guardG = Cfg.DungeonRewards.BossSalvage.Where(b => b.DungeonId == d).Sum(b => b.Items.Sum(kv => W(kv.Key) * kv.Value));
            Assert.That(sideG + guardG, Is.EqualTo(kg), d);
            total += sideG + guardG;
            side += sideG;
        }
        Assert.That((total, side), Is.EqualTo((11950, 10100)));
    }

    /// <summary>
    /// A05-C01—C03：四个旧蓝图包绑定新 recordNode（d01_08/d02_08/d03_08/d04_07），不再绑旧 06；旧 claim 工作量不变（6 箱 20 分钟、记录 10 分钟）；
    /// 知识按集合去重——同一知识由记录领取授予一次，已知的不重复。
    /// </summary>
    [Test]
    public void A05_C01_C03_BlueprintPacksOnRecordNodes_Dedup()
    {
        var facts = Cfg.Knowledge.SelectMany(k => k.GrantedByFacts).Where(f => f.Contains("d0")).Distinct().ToList();
        Assert.That(facts.Any(f => f.Contains("_06")), Is.False, "不再绑旧 06");
        foreach (var rec in new[] { "d01_08_unique", "d02_08_unique", "d03_08_unique", "d04_07_unique" })
            Assert.That(Cfg.Knowledge.Any(k => k.GrantedByFacts.Contains(GameEngine.ClaimKey(rec))), rec);
        Assert.That(Cfg.Resources.Where(r => r.IsRecord).All(r => r.BaseMinutes == 10), Is.True);
        var s = New(2202);
        E.DebugTeleport(s, "d01_08");
        E.DebugSetProgress(s, "d01_08", 100);
        E.DebugInstanceFact(s, "d01", "guardianDefeated");
        Start(s, ActionRequest.Gather("d01_08_unique"));
        Run(s);
        var grantedIds = Cfg.Knowledge.Where(k => k.GrantedByFacts.Contains(GameEngine.ClaimKey("d01_08_unique"))).Select(k => k.Id).ToList();
        Assert.That(grantedIds, Is.Not.Empty);
        Assert.That(grantedIds.All(k => E.KnowsKnowledge(s, k)), Is.True);
        var known = Cfg.Knowledge.Count(k => E.KnowsKnowledge(s, k.Id));
        var flags = s.Progress.Flags.Count;
        // 换代后核对：不重发、知识集合不变
        E.DebugTeleport(s, "c03_crypt");
        Assert.That(E.NewDungeonGeneration(s, "d01"), Is.Null);
        E.DebugTeleport(s, "d01_08");
        E.DebugSetProgress(s, "d01_08", 100);
        E.DebugInstanceFact(s, "d01", "guardianDefeated");
        Start(s, ActionRequest.Gather("d01_08_unique"));
        Assert.That(s.Main!.BaseWorkMinutes, Is.EqualTo(10), "核对仍 10 分钟");
        Run(s);
        Assert.That(Cfg.Knowledge.Count(k => E.KnowsKnowledge(s, k.Id)), Is.EqualTo(known), "已知只记来源，不重复");
        Assert.That(s.Progress.Flags.Count, Is.EqualTo(flags), "核对不再写新的 campaign 事实（首通包只发一次）");
    }
}
