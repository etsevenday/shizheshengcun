using Newtonsoft.Json.Linq;
using Survival.Core.Engine;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 批次 B23 M477：可选地牢知识支线 EXQ05—08 的配置（R71；A11「可选地牢任务与知识」§1—§4、expansion-quests.json）。
/// 验收 EXQ-A01（入口/记录/出口在 v3 地图且主线记录点不是出口）、EXQ-A02（d05—08 不写主线容量）、EXQ-A03（七知识配方门槛原样）、EXQ-A08（无货币/入盟门槛）静态部分。
/// </summary>
public class B23ExqDataTests : TestBase
{
    private static JObject Source() => JObject.Parse(File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "design.expansion-quests.json")));

    /// <summary>DesignSync：四任务、四知识包与 expansion-quests.json 逐项一致（接取岗位、记录点、10/30 分钟、知识集合、claimId）。</summary>
    [Test]
    public void DesignSync_QuestsAndPacksMatchSource()
    {
        var src = (JArray)Source()["optionalQuests"]!;
        Assert.That(src.Count, Is.EqualTo(4));
        foreach (var q in src)
        {
            var id = (string)q["id"]!;
            var qc = E.Quest(id)!;
            Assert.That(qc, Is.Not.Null, id);
            Assert.That((qc.Title, qc.Kind, qc.LocationId, qc.Accept, qc.WorkMinutes, qc.Implemented),
                Is.EqualTo(((string)q["title"]!, "optional", (string)q["board"]!, "talk", 0, true)), id);
            Assert.That(qc.RequiresAll, Is.EqualTo(new[] { "hub_registered" }), id);
            Assert.That(qc.RecordFacts, Is.EqualTo(new[] { "claim:" + (string)q["claimId"]! }), id);
            var p = Cfg.ExpansionQuests.Packs.Single(x => x.QuestId == id);
            Assert.That((p.Id, p.ClaimResourceId, p.CopyLocationId, p.CopyMinutes),
                Is.EqualTo(((string)q["knowledgePack"]!, (string)q["claimId"]!, (string)q["alternativeKnowledgeNode"]!, (int)q["alternativeWorkMinutes"]!)), id);
            Assert.That(p.Knowledge, Is.EqualTo(q["knowledge"]!.Select(x => (string)x!).ToList()), id);
            var res = Cfg.Resources.Single(r => r.NodeId == p.ClaimResourceId);
            Assert.That((res.LocationId, res.RecordId, res.BaseMinutes, res.IsRecord), Is.EqualTo(((string)q["recordNode"]!, (string)q["record"]!, (int)q["workMinutes"]!, true)), id);
        }
        // 接取岗位：EXQ05/06 罗铮（h03）、EXQ07 许安（h02）、EXQ08 陈芷（h04）
        Assert.That(new[] { "EXQ05", "EXQ06", "EXQ07", "EXQ08" }.Select(x => E.Quest(x)!.NpcId),
            Is.EqualTo(new[] { "npc_luo_zheng", "npc_luo_zheng", "npc_xu_an", "npc_chen_zhi" }));
    }

    /// <summary>EXQ-A01：父入口、记录点、出口点都在 v3 地图（与地牢字段一致），记录点不是出口；d02/d04 主线记录点也不是出口点。</summary>
    [Test]
    public void EXQ_A01_NodesExist_RecordNotExit()
    {
        foreach (var q in (JArray)Source()["optionalQuests"]!)
        {
            var d = Cfg.Map.Dungeons.Single(x => x.Id == (string)q["dungeonId"]!);
            Assert.That((d.Parent, d.RecordNode, d.ExitNode), Is.EqualTo(((string)q["entrance"]!, (string)q["recordNode"]!, (string)q["exitNode"]!)));
            Assert.That(d.RecordNode, Is.Not.EqualTo(d.ExitNode));
            foreach (var n in new[] { d.Parent, d.RecordNode, d.ExitNode }) Assert.That(Cfg.Locations.Any(l => l.Id == n), n);
        }
        Assert.That(Cfg.Map.Dungeons.Single(x => x.Id == "d02").ExitNode, Is.EqualTo("d02_09"));
        Assert.That(Cfg.Map.Dungeons.Single(x => x.Id == "d04").ExitNode, Is.EqualTo("d04_08"));
    }

    /// <summary>EXQ-A02：d05—08 记录不写 FA02/FA03 记录事实、不进容量解锁；主线任务不以四支线为前置。</summary>
    [Test]
    public void EXQ_A02_NoCapacityFacts_MainlineIndependent()
    {
        var capRecords = Cfg.Scope.Capacity.Unlocks.SelectMany(u => u.AllOf.Concat(u.AnyOf)).ToHashSet();
        foreach (var p in Cfg.ExpansionQuests.Packs)
        {
            var res = Cfg.Resources.Single(r => r.NodeId == p.ClaimResourceId);
            Assert.That(res.ClaimFacts, Is.Empty, p.Id);
            Assert.That(capRecords, Does.Not.Contain(res.RecordId), p.Id);
        }
        var exq = new[] { "EXQ05", "EXQ06", "EXQ07", "EXQ08" };
        foreach (var q in Cfg.Quests.List.Where(q => q.Kind == "main"))
            Assert.That(q.RequiresAll.Concat(q.RequiresAny).Concat(q.ShowAny).Any(f => exq.Any(x => f.Contains(x)) || Cfg.ExpansionQuests.Packs.Any(p => f.Contains(p.ClaimResourceId))), Is.False, q.Id);
    }

    /// <summary>EXQ-A03：七种知识的配方存在、产物在物品表，制造等级与工坊按 A11 §3 原样（只授知识，不免除材料/设施/熟练）。</summary>
    [Test]
    public void EXQ_A03_SevenKnowledge_RecipeGatesUnchanged()
    {
        var gates = new Dictionary<string, (int, string)>
        {
            ["blueprint_filter_token"] = (3, "workbench"), ["blueprint_repair_manual"] = (7, "workbench"),
            ["blueprint_gather_gloves"] = (1, "hand"), ["blueprint_utility_trousers"] = (3, "sewing"),
            ["blueprint_forage_jacket"] = (3, "sewing"), ["blueprint_field_medkit"] = (1, "hand"),
            ["blueprint_ration_pouch"] = (10, "sewing"),
        };
        Assert.That(Cfg.ExpansionQuests.Packs.SelectMany(p => p.Knowledge), Is.EquivalentTo(gates.Keys));
        foreach (var (k, (lv, station)) in gates)
        {
            var rc = Cfg.Recipes.Single(r => r.Knowledge == k);
            Assert.That((rc.MinSkills["crafting"], rc.Station), Is.EqualTo((lv, station)), k);
            Assert.That(rc.Inputs, Is.Not.Empty, k);
            foreach (var o in rc.Outputs.Keys) Assert.That(Cfg.Items.Any(i => i.Id == o), o);
            var kc = Cfg.Knowledge.Single(x => x.Id == k);
            var p = Cfg.ExpansionQuests.Packs.Single(x => x.Knowledge.Contains(k));
            Assert.That(kc.GrantedByFacts, Does.Contain("claim:" + p.ClaimResourceId).And.Contain(p.CopyFact), k);
            Assert.That(kc.Source, Does.Contain("查抄公开资料"), k);
        }
    }

    /// <summary>EXQ-A08：任务与抄录没有材料/货币价、入盟/宗教/人体实验/队友存活门槛，记录不是物资卡（campaign 记录，0 重量）。</summary>
    [Test]
    public void EXQ_A08_NoPriceOrMembershipGate()
    {
        foreach (var q in Cfg.Quests.List.Where(q => q.Kind == "optional"))
        {
            Assert.That(q.RequiresAll.Concat(q.RequiresAny).Any(GameEngine.IsMembershipFact), Is.False, q.Id);
            Assert.That(q.RequiresRosterMember, Is.False, q.Id);
            Assert.That(q.WorkServiceIds, Is.Empty, q.Id);
        }
        var copy = Cfg.City.Services.Single(x => x.Id == "copy_public_knowledge");
        Assert.That((copy.Inputs.Count, copy.WorldMinutes), Is.EqualTo((0, 30)));
        Assert.That(Cfg.ExpansionQuests.Packs.All(p => p.CopyMinutes == 30), Is.True);
    }
}
