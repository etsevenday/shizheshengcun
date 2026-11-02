using Survival.Core.Config;
using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 批次 B12（R36）：地牢普通箱 instance 账与 8 份核心记录 campaign 账（S04「四账及奖励防重」、S05「唯一与迁移」、A08、gate-contract）。
/// 验收 CLR30-02/03、RES30-04、COMP14、Q109（军械台蓝图随 d03_08_unique 领取）。
/// </summary>
public class B12ClaimTests : TestBase
{
    private void Go(GameState s, string room)
    {
        E.DebugMapTeleport(s, room);
        E.DebugSetProgress(s, room, 100);
    }

    private void Do(GameState s, string id)
    {
        Start(s, ActionRequest.Gather(id));
        Run(s);
    }

    /// <summary>CLR30-02/RES30-04：d01_08 核心记录领取一次写 campaign 事实、记录与 FA02 credit、本代实例事实；不发物资卡、不给经验；换三代仍不补发，只补写本代实例事实。</summary>
    [Test]
    public void CLR30_02_CampaignRecord_OncePerCampaign_ThreeGenerations()
    {
        var s = New();
        Go(s, "d01_08");
        Assert.That(s.Resources["d01_08_unique"].Discovered, Is.True, "reveal 100");
        var res = E.Resource("d01_08_unique");
        Assert.That(GameEngine.GatherVerb(res), Is.EqualTo("领取"));
        Assert.That(E.BattleCapacity(s), Is.EqualTo(1));
        var items = s.Items.Count;
        var xp = s.Skills.Sum(kv => kv.Value.Xp);
        Start(s, ActionRequest.Gather("d01_08_unique"));
        Assert.That(s.Main!.BaseWorkMinutes, Is.EqualTo(10));
        Run(s);
        Assert.That(s.Progress.Flags, Is.SupersetOf(new[] { "campaignId:d01_08_unique", "claim:d01_08_unique", "church_route_record", "FA02_record_credit" }));
        Assert.That(s.DungeonInstances["d01"].Facts, Does.Contain(GameEngine.RecordClaimedFact));
        Assert.That(s.Items.Count, Is.EqualTo(items), "不发物资卡当凭证");
        Assert.That(s.Skills.Sum(kv => kv.Value.Xp), Is.EqualTo(xp), "记录不计经验");
        Assert.That(E.BattleCapacity(s), Is.EqualTo(3), "教会路线记录 → C3");
        Assert.That(E.CanStart(s, ActionRequest.Gather("d01_08_unique")), Does.Contain("已领取"));
        var flags = s.Progress.Flags.Count;

        for (var gen = 2; gen <= 4; gen++)
        {
            E.DebugMapTeleport(s, "c03_crypt");
            Assert.That(E.NewDungeonGeneration(s, "d01"), Is.Null);
            Assert.That(s.DungeonInstances["d01"].Generation, Is.EqualTo(gen));
            Assert.That(s.DungeonInstances["d01"].Facts, Is.Empty, "旧代实例事实丢弃");
            Assert.That(E.CampaignClaimed(s, "d01_08_unique"), Is.True, "campaign 不随换代恢复");
            Go(s, "d01_08");
            Assert.That(E.CanStart(s, ActionRequest.Gather("d01_08_unique")), Is.Null, "新一代可核对本代记录");
            Do(s, "d01_08_unique");
            Assert.That(s.Log.Last(l => l.Kind == GameEventKind.ActionCompleted.ToString()).Text, Does.Contain("不再重复发放"));
            Assert.That(s.Progress.Flags.Count, Is.EqualTo(flags), "不重发 campaign 事实");
            Assert.That(s.DungeonInstances["d01"].Facts, Does.Contain(GameEngine.RecordClaimedFact));
        }
        Assert.That(s.Items.Count, Is.EqualTo(items));
        Assert.That(E.BattleCapacity(s), Is.EqualTo(3), "MAP30-05：容量不重复");
    }

    /// <summary>Q109：d03_08_unique 领取同事务授予军械台蓝图（knowledge grantedByFacts claim:d03_08_unique），并写 communication_power_claimed（FA05 门槛）。</summary>
    [Test]
    public void Q109_ArmoryBlueprint_GrantedByD03Claim()
    {
        var s = New();
        var know = Cfg.Knowledge.First(k => k.Id == "blueprint_armory_station");
        Assert.That(know.GrantedByFacts.Any(s.Progress.Flags.Contains), Is.False);
        Go(s, "d03_08");
        var preview = E.Preview(s, ActionRequest.Gather("d03_08_unique"));
        Assert.That(preview.Notes, Has.Some.Contains(know.Name));
        Do(s, "d03_08_unique");
        Assert.That(s.Progress.Flags, Does.Contain("communication_power_claimed"));
        Assert.That(know.GrantedByFacts.Any(s.Progress.Flags.Contains), Is.True);
        Assert.That(s.Log.Last(l => l.Kind == GameEventKind.ActionCompleted.ToString()).Text, Does.Contain(know.Name));
    }

    /// <summary>出口 instance_complete 每次重验当前代：领取后可走，换代后关闭，再核对又可走；从父地点不能反入。</summary>
    [Test]
    public void ExitRevalidates_PerGeneration()
    {
        var s = New();
        var exit = Cfg.Edges.First(e => e.Id == "d05_08__w33_waterworks");
        Go(s, "d05_07");
        Assert.That(E.EdgeOpen(s, exit), Is.False);
        Do(s, "d05_07_unique");
        Assert.That(E.EdgeOpen(s, exit), Is.True);
        E.DebugMapTeleport(s, "w33_waterworks");
        Assert.That(E.EdgeBetween("w33_waterworks", "d05_08"), Is.Null, "出口不能反入");
        E.NewDungeonGeneration(s, "d05");
        Assert.That(E.EdgeOpen(s, exit), Is.False, "新一代要重新核对");
    }

    /// <summary>COMP14/CLR30-03：普通箱按本代库存（2/0），搜空后“本代已搜空”；换代补满并记新代号；已持有物保质期不刷新；房间清理与箱各记各账。</summary>
    [Test]
    public void COMP14_InstanceBox_PerGeneration_ExpiryKept()
    {
        var s = New();
        Go(s, "d08_03");
        Assert.That(s.Resources["d08_03_cache2"].Generation, Is.EqualTo(1));
        Do(s, "d08_03_cache2");
        Assert.That(Count(s, "dried_meat", "d08_03"), Is.EqualTo(2));
        var expires = First(s, "dried_meat", "d08_03").ExpiresAt;
        Assert.That(s.Resources["d08_03_cache2"].Available, Is.EqualTo(0));
        Assert.That(E.CanStart(s, ActionRequest.Gather("d08_03_cache2")), Is.EqualTo("本代已搜空（地牢换代后恢复）"));
        Start(s, ActionRequest.Clear());
        Run(s);
        Assert.That(s.Resources["d08_03_cache0"].Available, Is.EqualTo(2), "清理不领箱");

        Assert.That(E.NewDungeonGeneration(s, "d08"), Does.Contain("先离开"));
        E.DebugMapTeleport(s, "w34_depot");
        Assert.That(E.NewDungeonGeneration(s, "d08"), Is.Null);
        Assert.That(s.Resources["d08_03_cache2"].Available, Is.EqualTo(2));
        Assert.That(s.Resources["d08_03_cache2"].Generation, Is.EqualTo(2));
        Assert.That(s.Resources["d08_03_cache2"].Discovered, Is.True, "已发现按整局记（H3）");
        Assert.That(s.Locations["d08_03"].Progress, Is.EqualTo(0), "房间探索度随代清零");
        Assert.That(E.ClearChargesAt(s, "d08_03").charges, Is.EqualTo(1), "清理次数随代恢复");
        Assert.That(First(s, "dried_meat", "d08_03").ExpiresAt, Is.EqualTo(expires), "重置不刷新已持有物保质");
    }

    /// <summary>旧档迁移账（R37）：旧核心节点映射与 6 个旧唯一箱 claimId→新节点按数据导入；不对 v3 同号侧室全局替换。</summary>
    [Test]
    public void R37_LegacyClaimLedger()
    {
        Assert.That(Cfg.Map.LegacyCoreNodes["d01_06"], Is.EqualTo("d01_08"));
        Assert.That(Cfg.Map.LegacyCoreNodes["d04_06"], Is.EqualTo("d04_07"));
        Assert.That(Cfg.Map.LegacyCoreNodes["d03_07"], Is.EqualTo("d03_08"));
        var claims = Cfg.Map.LegacyClaims.ToDictionary(c => c.ClaimId, c => c.Node);
        Assert.That(claims, Is.EquivalentTo(new Dictionary<string, string>
        {
            ["shield_cache"] = "d03_03", ["officer_cache"] = "d03_04", ["rifle_cache"] = "d03_08",
            ["shotgun_cache"] = "d03_08", ["armor_cache"] = "d03_08", ["ammo_reserve_cache"] = "d03_08",
        }));
        Assert.That(Cfg.Map.LegacyClaims.First(c => c.ClaimId == "ammo_reserve_cache").Grants, Has.Count.EqualTo(3));
        // v3 的 d0x_06 侧室资源保持在原房间（不按旧核心号盲目搬走）
        Assert.That(E.Resource("d01_06_cache1").LocationId, Is.EqualTo("d01_06"));
        Assert.That(E.Resource("d01_08_unique").LocationId, Is.EqualTo(Cfg.Map.RecordNodes["d01"]));
        foreach (var kv in Cfg.Map.RecordNodes)
            Assert.That(Cfg.Resources.Single(r => r.IsCampaign && E.Location(r.LocationId).DungeonId == kv.Key).LocationId, Is.EqualTo(kv.Value), "八记录固定在 recordNode");
    }

    /// <summary>S14「四类账」卡面角标：日补“n/cap”、finite“·不补/搜空不补”、flowing“无限”、普通箱“本代剩余/本代已搜空”、核心记录“可领取/本局已领·可核对/已领取”。</summary>
    [Test]
    public void ResourceStockBadges_PerLedger()
    {
        var s = New();
        Assert.That(E.ResourceStock(s, E.Resource("w11_orchard_a")).Badge, Is.EqualTo("12/12"));
        Assert.That(E.ResourceStock(s, E.Resource("w34_depot_bottles")).Badge, Is.EqualTo("8/8·不补"));
        s.Resources["w34_depot_bottles"].Available = 0;
        Assert.That(E.ResourceStock(s, E.Resource("w34_depot_bottles")), Is.EqualTo(("搜空不补", true)));
        Assert.That(E.ResourceStock(s, E.Resource("w12_canal_water")).Badge, Is.EqualTo("无限"));
        Assert.That(E.ResourceStock(s, E.Resource("d05_04_cache0")).Badge, Is.EqualTo("本代剩余2/2"));
        Go(s, "d05_07");
        Assert.That(E.ResourceStock(s, E.Resource("d05_07_unique")).Badge, Is.EqualTo("可领取"));
        Do(s, "d05_07_unique");
        Assert.That(E.ResourceStock(s, E.Resource("d05_07_unique")), Is.EqualTo(("已领取", true)));
        E.DebugMapTeleport(s, "w33_waterworks");
        E.NewDungeonGeneration(s, "d05");
        Assert.That(E.ResourceStock(s, E.Resource("d05_07_unique")).Badge, Is.EqualTo("本局已领·可核对"));
    }
}
