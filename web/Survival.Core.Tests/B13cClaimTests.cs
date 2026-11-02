using Newtonsoft.Json.Linq;
using Survival.Core.Config;
using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 批次 B13c（R130/R131；答复 Q122—Q127）：6 个旧 d03 唯一箱可领取（officer_cache 改在 d03_07、本代守卫胜利后当场出现）、
/// 四核心同授 28 张蓝图、8 份核心记录正式名、schema 24 迁移。
/// 验收 CLAIM-Q122-01/02、KNOW-Q123-01/02、NAME-Q125-01（记录名）。Q126/Q127 的核对见 B13cGenerationExitTests。
/// </summary>
public class B13cClaimTests : TestBase
{
    private static JObject Adj() => JObject.Parse(File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "design.resource-adjudications.json")));

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

    private static readonly string[] Caches = { "shield_cache", "officer_cache", "shotgun_cache", "rifle_cache", "armor_cache", "ammo_reserve_cache" };

    /// <summary>CLAIM-Q122-01（数据）：6 箱逐条对策划表——节点、产出、20 分钟、0 经验、本局唯一实物包、无工具；officer_cache 在 d03_07 读本代守卫胜利。</summary>
    [Category("DesignSync")]
    [Test]
    public void CLAIM_Q122_01_CacheRowsMatchDesign()
    {
        var rows = ((JArray)Adj()["resources"]!["uniqueEquipmentCaches"]!).Cast<JObject>().ToList();
        Assert.That(rows.Select(r => (string)r["id"]!), Is.EquivalentTo(Caches));
        foreach (var j in rows)
        {
            var r = E.Resource((string)j["id"]!);
            Assert.That(r.Name, Is.EqualTo((string)j["displayName"]!), r.NodeId);
            Assert.That(r.LocationId, Is.EqualTo((string)j["nodeId"]!), r.NodeId);
            Assert.That(r.Outputs, Is.EquivalentTo(((JObject)j["items"]!).Properties().ToDictionary(p => p.Name, p => (int)p.Value)), r.NodeId);
            Assert.That(r.BaseMinutes, Is.EqualTo(20), r.NodeId);
            Assert.That(r.IsCampaignPhysical && !r.XpAllowed && r.ToolBonus == null && r.ProficiencyId == null, Is.True, r.NodeId);
            Assert.That(GameEngine.GatherVerb(r), Is.EqualTo("领取"), r.NodeId);
            Assert.That(Cfg.Map.LegacyClaims.Single(c => c.ClaimId == r.NodeId).Node, Is.EqualTo(r.LocationId), "迁移账与领取点一致");
        }
        Assert.That(E.Resource("officer_cache").LocationId, Is.EqualTo("d03_07"), "取代 A09 E6.4 的 d03_04");
        Assert.That(E.Resource("officer_cache").RevealInstanceFact, Is.EqualTo("guardianDefeated"));
        Assert.That(Caches.Where(c => c != "officer_cache").All(c => E.Resource(c).RevealAt == 100), Is.True, "其余 5 箱探索 100% 显示");
    }

    /// <summary>CLAIM-Q122-01：盾箱与 d03_08 四箱探索 100% 出现；领取 20 分钟、0 经验，产物整包落当地地面（装备白色无词条、当前=最大耐久），已领不再发。</summary>
    [Test]
    public void CLAIM_Q122_01_FiveCaches_At100_TwentyMinutes_NoXp()
    {
        var s = New();
        E.DebugMapTeleport(s, "d03_03");
        Assert.That(s.Resources["shield_cache"].Discovered, Is.False);
        E.DebugSetProgress(s, "d03_03", 50);
        Assert.That(s.Resources["shield_cache"].Discovered, Is.False, "50% 还看不到");
        E.DebugSetProgress(s, "d03_03", 100);
        Assert.That(s.Resources["shield_cache"].Discovered, Is.True, "100% 显示");
        var xp = s.Skills.Sum(kv => kv.Value.Xp);
        var minute = s.GameMinute;
        Start(s, ActionRequest.Gather("shield_cache"));
        Assert.That(s.Main!.BaseWorkMinutes, Is.EqualTo(20));
        Run(s);
        Assert.That(s.GameMinute - minute, Is.EqualTo(20));
        var shield = First(s, "riot_shield", "d03_03");
        Assert.That(shield.Quality, Is.EqualTo("white"));
        Assert.That(shield.Affixes, Is.Null.Or.Empty);
        Assert.That(shield.Durability, Is.EqualTo(shield.MaxDurability));
        Assert.That(s.Skills.Sum(kv => kv.Value.Xp), Is.EqualTo(xp), "0 野外经验");
        Assert.That(E.CanStart(s, ActionRequest.Gather("shield_cache")), Does.Contain("已领取"));
        Assert.That(E.ResourceStock(s, E.Resource("shield_cache")), Is.EqualTo(("已领取", true)));

        Go(s, "d03_08");
        foreach (var id in new[] { "shotgun_cache", "rifle_cache", "armor_cache", "ammo_reserve_cache" })
        {
            Assert.That(s.Resources[id].Discovered, Is.True, id);
            Do(s, id);
        }
        Assert.That(Count(s, "shotgun", "d03_08") + Count(s, "rifle", "d03_08") + Count(s, "guard_armor", "d03_08"), Is.EqualTo(3));
        Assert.That(Count(s, "ammo_9mm", "d03_08"), Is.EqualTo(24));
        Assert.That(Count(s, "shell_12g", "d03_08"), Is.EqualTo(12));
        Assert.That(Count(s, "ammo_rifle", "d03_08"), Is.EqualTo(15));
        Assert.That(E.CampaignClaimed(s, "d03_08_unique"), Is.False, "不并入电源领取");
    }

    /// <summary>CLAIM-Q122-01：警官箱在 d03_07，本代守卫胜利后当场出现（不折返 d03_04）；胜利前不显示、不能领；探索 100% 也不显示。</summary>
    [Test]
    public void CLAIM_Q122_01_OfficerCache_AfterGuardVictory_OnTheSpot()
    {
        var s = New();
        Go(s, "d03_07");
        Assert.That(s.Resources["officer_cache"].Discovered, Is.False, "探索 100% 不显示");
        Assert.That(E.CanStart(s, ActionRequest.Gather("officer_cache")), Is.Not.Null);
        var text = E.DebugInstanceFact(s, "d03", "guardianDefeated");
        Assert.That(s.Resources["officer_cache"].Discovered, Is.True, "守卫胜利后当场出现");
        Assert.That(text, Does.Contain("守卫战利品箱已出现"));
        Assert.That(E.ResourceStock(s, E.Resource("officer_cache")).Badge, Is.EqualTo("可领取"));
        var p = E.Preview(s, ActionRequest.Gather("officer_cache"));
        Assert.That(p.Notes, Has.Some.Contains("本代守卫战胜利后出现"));
        Do(s, "officer_cache");
        Assert.That(Count(s, "service_pistol", "d03_07"), Is.EqualTo(1));
        Assert.That(s.Resources.ContainsKey("d03_04_officer"), Is.False);
        Assert.That(E.Resource("officer_cache").LocationId, Is.Not.EqualTo("d03_04"));
    }

    /// <summary>CLAIM-Q122-01：守卫事实先记在别处（未到过 d03_07），实际到达 d03_07 时显示。</summary>
    [Test]
    public void CLAIM_Q122_01_OfficerCache_RevealOnArrivalWhenFactAlreadySet()
    {
        var s = New();
        E.DebugMapTeleport(s, "d03_06");
        E.DebugInstanceFact(s, "d03", "guardianDefeated");
        Assert.That(s.Resources["officer_cache"].Discovered, Is.False, "没到过不显示");
        E.DebugMapTeleport(s, "d03_07");
        Assert.That(s.Resources["officer_cache"].Discovered, Is.True);
    }

    /// <summary>CLAIM-Q122-02：满包仍可领，整包落地；取消无产出不写已领；换代不复活已领箱；未领的警官箱换代后须本代守卫再胜一次。</summary>
    [Test]
    public void CLAIM_Q122_02_FullPack_Cancel_Generation()
    {
        var s = New();
        Go(s, "d03_08");
        // 随身塞满五张卡
        foreach (var id in new[] { "wood", "stone", "fiber", "cloth", "bandage" }) E.DebugGive(s, id, 1);
        var cards = E.RootCardCount(s);
        Assert.That(cards, Is.GreaterThanOrEqualTo(Cfg.Carry.MaxRootCards), "随身已满");
        Start(s, ActionRequest.Gather("rifle_cache"));
        E.Cancel(s);
        Assert.That(Count(s, "rifle"), Is.EqualTo(0), "取消无产出");
        Assert.That(E.CampaignClaimed(s, "rifle_cache"), Is.False, "取消不写已领");
        Do(s, "rifle_cache");
        Assert.That(Count(s, "rifle", "d03_08"), Is.EqualTo(1), "包满照样落地");
        Assert.That(E.RootCardCount(s), Is.EqualTo(cards), "不强塞");

        // 警官箱：本代守卫胜利后出现但先不领
        Go(s, "d03_07");
        E.DebugInstanceFact(s, "d03", "guardianDefeated");
        Assert.That(s.Resources["officer_cache"].Discovered, Is.True);
        E.DebugMapTeleport(s, "o03_police");
        Assert.That(E.NewDungeonGeneration(s, "d03"), Is.Null);
        Assert.That(E.CampaignClaimed(s, "rifle_cache"), Is.True, "已领跨代不复活");
        Assert.That(E.ResourceStock(s, E.Resource("rifle_cache")), Is.EqualTo(("已领取", true)));
        Go(s, "d03_07");
        Assert.That(s.Resources["officer_cache"].Discovered, Is.True, "已发现整局保留");
        Assert.That(E.ResourceStock(s, E.Resource("officer_cache")), Is.EqualTo(("待本代守卫胜利", true)));
        Assert.That(E.CanStart(s, ActionRequest.Gather("officer_cache")), Does.Contain("守卫"), "换代后须本代守卫胜利");
        E.DebugInstanceFact(s, "d03", "guardianDefeated");
        Assert.That(E.CanStart(s, ActionRequest.Gather("officer_cache")), Is.Null);
        Do(s, "officer_cache");
        Assert.That(Count(s, "service_pistol", "d03_07"), Is.EqualTo(1));
        E.DebugMapTeleport(s, "o03_police");
        E.NewDungeonGeneration(s, "d03");
        Go(s, "d03_07");
        E.DebugInstanceFact(s, "d03", "guardianDefeated");
        Assert.That(E.CanStart(s, ActionRequest.Gather("officer_cache")), Does.Contain("已领取"), "重复胜利不重发");
        Assert.That(Count(s, "service_pistol"), Is.EqualTo(1));
    }

    /// <summary>CLAIM-Q122-02：唯一箱不能用工具加速；显式指定工具整次拒绝。</summary>
    [Test]
    public void CLAIM_Q122_02_NoTools()
    {
        var s = New();
        Go(s, "d03_03");
        E.DebugGiveResourceTools(s);
        var p = E.Preview(s, ActionRequest.Gather("shield_cache"));
        Assert.That(p.ToolItemId, Is.Null);
        Start(s, ActionRequest.Gather("shield_cache"));
        Assert.That(s.Main!.BaseWorkMinutes, Is.EqualTo(20), "工具不缩短");
    }

    /// <summary>KNOW-Q123-01：四核心领取同一事务补学 7/7/8/6 共 28 张蓝图（d03 另加军械台），原 10 分钟、无经验、不发纸卡/成品；已知的不重复计。</summary>
    [Test]
    public void KNOW_Q123_01_CorePacks_SameTransaction()
    {
        var packs = ((JArray)Adj()["coreBlueprintPacks"]!).Cast<JObject>()
            .ToDictionary(p => (string)p["resourceId"]!, p => ((JArray)p["knowledgeIds"]!).Select(x => (string)x!).ToList());
        Assert.That(packs.Values.Sum(v => v.Count), Is.EqualTo(28));
        Assert.That(packs.ToDictionary(kv => kv.Key, kv => kv.Value.Count), Is.EquivalentTo(new Dictionary<string, int>
        {
            ["d01_08_unique"] = 7, ["d02_08_unique"] = 7, ["d03_08_unique"] = 8, ["d04_07_unique"] = 6,
        }));
        foreach (var (rid, ids) in packs)
        {
            var s = New();
            var res = E.Resource(rid);
            Assert.That(ids.Any(id => E.KnowsKnowledge(s, id)), Is.False, rid);
            Go(s, res.LocationId);
            var preview = E.Preview(s, ActionRequest.Gather(rid));
            foreach (var id in ids) Assert.That(preview.Notes, Has.Some.Contains(Cfg.Knowledge.First(k => k.Id == id).Name), $"{rid} 预演写 {id}");
            var items = s.Items.Count;
            var xp = s.Skills.Sum(kv => kv.Value.Xp);
            var minute = s.GameMinute;
            Start(s, ActionRequest.Gather(rid));
            Assert.That(s.Main!.BaseWorkMinutes, Is.EqualTo(10), "不加第二次 10 分钟");
            // 进行中（未完成）不半学
            Assert.That(ids.Any(id => E.KnowsKnowledge(s, id)), Is.False, "未完成不半学");
            Run(s);
            Assert.That(s.GameMinute - minute, Is.EqualTo(10));
            Assert.That(ids.All(id => E.KnowsKnowledge(s, id)), Is.True, rid);
            Assert.That(s.Items.Count, Is.EqualTo(items), "不发纸卡或成品");
            Assert.That(s.Skills.Sum(kv => kv.Value.Xp), Is.EqualTo(xp), "不给经验");
            var log = s.Log.Last(l => l.Kind == GameEventKind.ActionCompleted.ToString()).Text;
            Assert.That(log, Does.Contain("获得做法知识"));
            if (rid == "d03_08_unique") Assert.That(E.KnowsKnowledge(s, "blueprint_armory_station"), Is.True, "Q109 军械台另外同授");
            foreach (var id in ids) Assert.That(Cfg.Knowledge.First(k => k.Id == id).Source, Does.Contain(res.Name).And.Not.Contain("待策划确认"));
        }
        // 取消不学
        var c = New();
        Go(c, "d02_08");
        Start(c, ActionRequest.Gather("d02_08_unique"));
        E.Cancel(c);
        Assert.That(packs["d02_08_unique"].Any(id => E.KnowsKnowledge(c, id)), Is.False, "取消不学");
    }

    /// <summary>KNOW-Q123-02：可信旧核心已领（schema 23 存档只有 campaignId/claim 或 communication_power_claimed）读档补缺知识，只记日志，不补物资/经验/本代事实；只探索 100% 不补。</summary>
    [Test]
    public void KNOW_Q123_02_Migration23To24_FillsKnowledgeOnly()
    {
        var s = New();
        Go(s, "d01_08");
        Do(s, "d01_08_unique");
        E.DebugMapTeleport(s, "c03_crypt");
        E.NewDungeonGeneration(s, "d01");   // 本代事实清空：迁移不能伪造
        s.Progress.Flags.Add("communication_power_claimed");   // 旧档 d03 等效
        Go(s, "d02_08");                    // 只探索 100%，没领取
        var items = s.Items.Count;
        var xp = s.Skills.Sum(kv => kv.Value.Xp);
        var j = JObject.Parse(SaveSerializer.Serialize(s));
        j["SchemaVersion"] = "23";
        var res = (JObject)j["Resources"]!;
        foreach (var id in Caches) res.Remove(id);
        var m = SaveSerializer.Deserialize(j.ToString(), Cfg, out _);
        Assert.That(m.SchemaVersion, Is.EqualTo(GameState.CurrentSchema), "23→24→25 链式升级");
        Assert.That(E.KnowsKnowledge(m, "blueprint_breach_axe"), Is.True, "d01 已领补缺");
        Assert.That(E.KnowsKnowledge(m, "blueprint_guard_sword"), Is.True, "d03 认 communication_power_claimed");
        Assert.That(E.KnowsKnowledge(m, "blueprint_rescue_medkit"), Is.False, "只探索 100% 不补");
        Assert.That(m.Log.Any(l => l.Text.Contains("版本24") && l.Text.Contains("补学15张蓝图")), Is.True);
        Assert.That(m.Items, Has.Count.EqualTo(items));
        Assert.That(m.Skills.Sum(kv => kv.Value.Xp), Is.EqualTo(xp));
        Assert.That(m.DungeonInstances["d01"].Facts, Does.Not.Contain(GameEngine.RecordClaimedFact), "不伪造本代核对");
        Assert.That(Caches.All(id => m.Resources.ContainsKey(id) && !E.CampaignClaimed(m, id)), Is.True, "旧唯一箱按未领，不凭背包猜");
        Assert.That(m.Resources["shield_cache"].Discovered, Is.False, "没到过 d03_03");
        var again = SaveSerializer.Deserialize(SaveSerializer.Serialize(m), Cfg, out var notes2);
        Assert.That(notes2, Is.Empty);
        Assert.That(SaveSerializer.Serialize(again), Is.EqualTo(SaveSerializer.Serialize(m)), "幂等");
    }

    /// <summary>NAME-Q125-01（记录名）：8 份核心记录用正式名，记录 ID/容量资格不变；容量说明用新名。</summary>
    [Category("DesignSync")]
    [Test]
    public void NAME_Q125_01_RecordNames()
    {
        var names = ((JObject)Adj()["recordNames"]!).Properties().ToDictionary(p => p.Name, p => (string)p.Value!);
        Assert.That(names, Has.Count.EqualTo(8));
        foreach (var r in Cfg.Resources.Where(r => r.IsRecord)) Assert.That(r.Name, Is.EqualTo(names[r.RecordId!]), r.NodeId);
        Assert.That(E.Resource("d01_08_unique").Name, Is.EqualTo("圣所旧路档册"));
        Assert.That(E.Resource("d01_08_unique").RecordId, Is.EqualTo("church_route_record"), "ID 不变");
        var s = New();
        Go(s, "d02_08");
        Assert.That(E.Preview(s, ActionRequest.Gather("d02_08_unique")).Notes, Has.Some.Contains("圣所旧路档册、封存区安全规程"));
    }
}
