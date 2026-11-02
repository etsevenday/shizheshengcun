using Newtonsoft.Json.Linq;
using Survival.Core.Config;
using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 批次 B13b（R129；答复 Q120/Q121/Q125 资源名）：53 条恢复来源里 47 条入资源表（6 个旧唯一箱的领取属 B13c）、
/// h02 营区净水 16/日补 12、逐行合格工具（工时 −5、产量不增、成功磨 1/取消 0、只认石刀/简易斧/撬棍实例）、
/// 搜集 5 级工具运用 ×0.90、135 条资源正式名、schema 23 迁移。验收 RES-Q120-01—04、TOOL-Q121-01—03、NAME-Q125-01（资源名部分）。
/// </summary>
public class B13bResourceTests : TestBase
{
    private static JObject Adj() => JObject.Parse(File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "design.resource-adjudications.json")));

    private static void Lv(GameState s, string id, int lv) => s.Skills[id].Level = lv;

    private GameState AtNode(string node, int progress = 100)
    {
        var s = New();
        E.DebugMapTeleport(s, node);
        if (progress > s.Locations[node].Progress) E.DebugSetProgress(s, node, progress);
        return s;
    }

    private ItemInstance Give(GameState s, string itemId)
    {
        E.DebugGive(s, itemId, 1);
        return s.Items.Where(i => i.ItemId == itemId && i.IsCarried).OrderByDescending(i => i.InstanceId).First();
    }

    /// <summary>RES-Q120-01：原 116 之外 47 条（44 主世界 + 支援包 + 消防斧箱 + 图册）入表，ID/别名不重复，参数与表逐条一致；h02 净水 16/日补 12 每次 2。</summary>
    [Category("DesignSync")]
    [Test]
    public void RES_Q120_01_RestoredSourcesImported()
    {
        var adj = Adj();
        var add = ((JArray)adj["resources"]!["additionalSources"]!).Cast<JObject>().ToList();
        Assert.That(add, Has.Count.EqualTo(53));
        var unique = ((JArray)adj["resources"]!["uniqueEquipmentCaches"]!).Select(u => (string)u["id"]!).ToHashSet();
        Assert.That(Cfg.Resources, Has.Count.EqualTo(116 + 47));
        Assert.That(Cfg.Resources.Select(r => r.NodeId).Distinct().Count(), Is.EqualTo(163), "资源 ID 不重复");
        var aliases = Cfg.Resources.SelectMany(r => r.LegacyAliases).ToList();
        Assert.That(aliases.Distinct().Count(), Is.EqualTo(aliases.Count), "旧别名不重复");
        foreach (var j in add)
        {
            var id = (string)j["id"]!;
            if (unique.Contains(id))
            {
                Assert.That(Cfg.Resources.Any(r => r.NodeId == id), Is.False, $"{id}：旧唯一箱领取在 B13c，本批不建资源点");
                Assert.That(Cfg.Map.LegacyClaims.Single(c => c.ClaimId == id).Name, Is.EqualTo((string)j["displayName"]!), "名称已登记");
                continue;
            }
            var r = E.Resource(id);
            Assert.That(r.LocationId, Is.EqualTo((string)j["nodeId"]!), id);
            Assert.That(r.BaseMinutes, Is.EqualTo((int)j["workMinutes"]!), id);
            Assert.That(r.DailyRefill, Is.EqualTo((int)j["dailyRefill"]!), id);
            var reveal = (int)j["reveal"]!;
            Assert.That(reveal == 0 ? r.RevealOnVisit : r.RevealAt == reveal, Is.True, id);
            Assert.That(r.XpAllowed, Is.EqualTo((bool)j["xpAllowed"]!), id);
            Assert.That(r.ProficiencyId, Is.EqualTo((string?)j["proficiencyId"]), id);
            Assert.That(r.Scope, Is.EqualTo((string)j["scope"]!), id);
            var cap = (int)j["cap"]!;
            if (cap < 0) Assert.That(r.IsUnlimited, Is.True, id);
            else Assert.That(r.Cap, Is.EqualTo(cap), id);
            Assert.That(r.Outputs, Is.EquivalentTo(((JObject)j["items"]!).Properties().ToDictionary(p => p.Name, p => (int)p.Value)), id);
            Assert.That(r.Hazard, Is.Null, "各源不新增划伤");
        }
        var water = E.Resource("h02_square_water");
        Assert.That((water.Cap, water.DailyRefill, water.Outputs["clean_water"], water.RevealOnVisit, water.XpAllowed), Is.EqualTo((16, 12, 2, true, false)));

        // 到营区广场：净水配给处可见 16/16，领取一次净水+2、库存 14、无经验
        var s = AtNode("h02_square");
        Assert.That(s.Resources["h02_square_water"].Discovered, Is.True);
        Assert.That(E.ResourceStock(s, water).Badge, Is.EqualTo("16/16"));
        Start(s, ActionRequest.Gather("h02_square_water"));
        Run(s);
        Assert.That(Count(s, "clean_water", "h02_square"), Is.EqualTo(2));
        Assert.That(s.Resources["h02_square_water"].Available, Is.EqualTo(14));
        Assert.That(s.Skills["gathering"].Xp + s.Skills["scavenging"].Xp, Is.EqualTo(0));
    }

    /// <summary>RES-Q120-02：旧档（schema 22）读入只补可见与初始库存（=cap），不发物品、不记已领；从升级日起算日补；幂等。</summary>
    [Test]
    public void RES_Q120_02_Migration22To23()
    {
        var s = New();
        E.DebugMapTeleport(s, "m01_road");
        E.DebugSetProgress(s, "m01_road", 50);
        var items = s.Items.Count;
        var j = JObject.Parse(SaveSerializer.Serialize(s));
        j["SchemaVersion"] = "22";
        var res = (JObject)j["Resources"]!;
        foreach (var r in Cfg.Resources.Skip(116)) res.Remove(r.NodeId);
        var m = SaveSerializer.Deserialize(j.ToString(), Cfg, out var notes);
        Assert.That(m.SchemaVersion, Is.EqualTo("23"));
        Assert.That(m.Resources, Has.Count.EqualTo(163));
        Assert.That(notes, Has.Some.Contains("163"));
        Assert.That(m.Resources["m01_road_crate"].Discovered, Is.True, "25% 已到：补可见");
        Assert.That(m.Resources["m01_road_crate"].Available, Is.EqualTo(6), "初始=cap");
        Assert.That(m.Resources["firetruck_cache"].Discovered, Is.True, "50% 已到：消防车器材箱可见");
        Assert.That(m.Resources["m01_road_plastic"].Discovered, Is.False, "100% 未到");
        Assert.That(m.Resources["m01_road_crate"].LastRefillDay, Is.EqualTo(m.GameMinute / Clock.MinutesPerDay), "从升级日起算，不追补");
        Assert.That(m.Progress.Flags, Does.Not.Contain(GameEngine.CampaignKey("firetruck_cache")), "不凭背包猜已领");
        Assert.That(m.Items, Has.Count.EqualTo(items), "迁移不发物品");
        var again = SaveSerializer.Deserialize(SaveSerializer.Serialize(m), Cfg, out var notes2);
        Assert.That(notes2, Is.Empty);
        Assert.That(SaveSerializer.Serialize(again), Is.EqualTo(SaveSerializer.Serialize(m)), "幂等");
    }

    /// <summary>RES-Q120-03：消防车器材箱 50%/20 分钟，白色无词条满耐久消防斧×1 落地；采石坡图册 100%/10 分钟授 3 知识，无经验/纸卡/成品；均本局一次。</summary>
    [Test]
    public void RES_Q120_03_FiretruckAndQuarryPack()
    {
        var s = AtNode("m01_road", 25);
        Assert.That(s.Resources["firetruck_cache"].Discovered, Is.False, "25% 还看不到");
        E.DebugSetProgress(s, "m01_road", 50);
        Assert.That(s.Resources["firetruck_cache"].Discovered, Is.True);
        var p = E.Preview(s, ActionRequest.Gather("firetruck_cache"));
        Assert.That(p.Ok, Is.True, p.Reason);
        Assert.That(p.BaseMinutes, Is.EqualTo(20));
        Start(s, ActionRequest.Gather("firetruck_cache"));
        Run(s);
        var axe = s.Items.Single(i => i.ItemId == "fire_axe");
        Assert.That(axe.Owner, Is.EqualTo("m01_road"), "落当地地面");
        Assert.That(axe.Quality, Is.EqualTo("white"));
        Assert.That(axe.Affixes, Is.Null);
        Assert.That(axe.Durability, Is.EqualTo(axe.MaxDurability), "满耐久");
        Assert.That(s.Skills["scavenging"].Xp, Is.EqualTo(0));
        Assert.That(E.CanStart(s, ActionRequest.Gather("firetruck_cache")), Does.Contain("已领取"));
        Assert.That(E.ResourceStock(s, E.Resource("firetruck_cache")).Badge, Is.EqualTo("已领取"));

        var q = AtNode("w03_quarry", 75);
        Assert.That(q.Resources["w03_quarry_blueprints"].Discovered, Is.False);
        E.DebugSetProgress(q, "w03_quarry", 100);
        Assert.That(E.KnowsKnowledge(q, "blueprint_hunter_bow"), Is.False);
        var before = q.Items.Count;
        Start(q, ActionRequest.Gather("w03_quarry_blueprints"));
        Assert.That(q.Main!.BaseWorkMinutes, Is.EqualTo(10));
        Run(q);
        foreach (var k in new[] { "blueprint_barbed_spear", "blueprint_hunter_bow", "blueprint_scout_armor" })
            Assert.That(E.KnowsKnowledge(q, k), Is.True, k);
        Assert.That(q.Items, Has.Count.EqualTo(before), "不发纸卡或成品");
        Assert.That(q.Skills["scavenging"].Xp + q.Skills["gathering"].Xp, Is.EqualTo(0));
        Assert.That(E.CanStart(q, ActionRequest.Gather("w03_quarry_blueprints")), Does.Contain("已领取"));
    }

    /// <summary>RES-Q120-04：工坊首次支援包要 FA02_record_credit（或可信旧 QMAP02）才显示，h03 当场 0 分钟领取一次，紧固件4+精密零件2 落地。</summary>
    [Test]
    public void RES_Q120_04_WorkshopContract()
    {
        var s = AtNode("h03_workshop");
        Assert.That(s.Resources["h03_workshop_contract"].Discovered, Is.False, "没有资格不显示");
        Assert.That(E.CanStart(s, ActionRequest.Gather("h03_workshop_contract")), Is.Not.Null);
        s.Progress.Flags.Add("FA02_record_credit");
        E.DebugMapTeleport(s, "h02_square");
        E.DebugMapTeleport(s, "h03_workshop");
        Assert.That(s.Resources["h03_workshop_contract"].Discovered, Is.True, "有资格后实到 h03 显示");
        var t = s.GameMinute;
        Assert.That(E.Preview(s, ActionRequest.Gather("h03_workshop_contract")).EstimatedMinutes, Is.EqualTo(0));
        Start(s, ActionRequest.Gather("h03_workshop_contract"));
        Assert.That(s.GameMinute, Is.EqualTo(t), "0 分钟当场领取");
        Assert.That(s.Main, Is.Null);
        Assert.That(Count(s, "fastener", "h03_workshop"), Is.EqualTo(4));
        Assert.That(Count(s, "precision_parts", "h03_workshop"), Is.EqualTo(2));
        Assert.That(s.Progress.Flags, Does.Contain(GameEngine.CampaignKey("h03_workshop_contract")));
        Assert.That(E.Start(s, ActionRequest.Gather("h03_workshop_contract")).Ok, Is.False, "本局一次");
        Assert.That(Count(s, "fastener", "h03_workshop"), Is.EqualTo(4));

        var legacy = AtNode("h03_workshop");
        legacy.Progress.Flags.Add("trusted_legacy_QMAP02_completed");
        E.DebugMapTeleport(legacy, "h02_square");
        E.DebugMapTeleport(legacy, "h03_workshop");
        Assert.That(legacy.Resources["h03_workshop_contract"].Discovered, Is.True, "可信旧 QMAP02 同样有资格");
    }

    /// <summary>TOOL-Q121-01：96 行 74 行有合格工具、其余明确无；新表只认列出的物品 ID；旧 3 条工具效果不变；徒手始终可用、地面工具不借。</summary>
    [Category("DesignSync")]
    [Test]
    public void TOOL_Q121_01_ToolTablePerRow()
    {
        var adj = Adj();
        var new96 = ((JArray)adj["tools"]!["new96"]!).Cast<JObject>().ToList();
        Assert.That(new96.Count(t => ((JArray)t["itemIds"]!).Count > 0), Is.EqualTo(74));
        foreach (var t in new96.Concat(((JArray)adj["tools"]!["restored"]!).Cast<JObject>()))
        {
            var id = (string)t["resourceId"]!;
            if (!Cfg.Resources.Any(r => r.NodeId == id)) continue;   // 6 个旧唯一箱（B13c），都无工具
            var r = E.Resource(id);
            var ids = ((JArray)t["itemIds"]!).Select(x => (string)x!).ToList();
            if (ids.Count == 0) { Assert.That(r.ToolBonus, Is.Null, id); continue; }
            Assert.That(r.ToolBonus!.ItemIds, Is.EqualTo(ids), id);
            Assert.That(r.ToolBonus.OutputMultiplier, Is.EqualTo(1), id);
            Assert.That(r.ToolBonus.BaseMinutesOverride, Is.EqualTo(r.BaseMinutes - 5), id);
            Assert.That(r.ToolBonus.ToolUsePassive, Is.EqualTo((bool)t["toolUsePassive"]!), id);
        }
        Assert.That(E.Resource("field_fiber").ToolBonus!.ToolTag, Is.EqualTo("stone_knife"));
        Assert.That(E.Resource("field_fiber").ToolBonus!.BaseMinutesOverride, Is.EqualTo(10));
        Assert.That(E.Resource("wood").ToolBonus!.OutputMultiplier, Is.EqualTo(2), "旧简易斧产量×2 不变");

        var s = AtNode("w12_canal", 50);
        Assert.That(E.Preview(s, ActionRequest.Gather("w12_canal_b")).BaseMinutes, Is.EqualTo(30), "没带撬棍：原工时，徒手可做");
        E.DebugGive(s, "crowbar", 1, "w12_canal");
        Assert.That(E.Preview(s, ActionRequest.Gather("w12_canal_b")).ToolName, Is.Null, "地面撬棍不隔空借用");
        var knife = Give(s, "stone_knife");
        Assert.That(E.Preview(s, ActionRequest.Gather("w12_canal_b")).ToolName, Is.Null, "石刀对撬棍行不合格");
        Assert.That(E.CanStart(s, ActionRequest.Gather("w12_canal_b", knife.InstanceId)), Is.EqualTo("这件工具对该资源点没有加成"));
        var bar = Give(s, "crowbar");
        var p = E.Preview(s, ActionRequest.Gather("w12_canal_b"));
        Assert.That((p.ToolName, p.BaseMinutes, p.ToolChoice), Is.EqualTo(("撬棍", 25, true)));
        var bare = E.Preview(s, ActionRequest.Gather("w12_canal_b", null, true));
        Assert.That((bare.ToolName, bare.BaseMinutes, bare.BareHands), Is.EqualTo(((string?)null, 30, true)), "可明确改徒手");
        // B13b 试玩修复（M279）：网页按 PreviewKey 缓存预估，徒手请求的键须与默认（自动选工具）不同，否则“改用徒手开始”显示带工具的 25 分钟
        Assert.That(ActionRequest.Gather("w12_canal_b", null, true).PreviewKey, Is.Not.EqualTo(ActionRequest.Gather("w12_canal_b").PreviewKey));
        Assert.That(ActionRequest.Gather("w12_canal_b").PreviewKey, Is.EqualTo(ActionRequest.Gather("w12_canal_b").PreviewKey), "同一请求键稳定，缓存照常命中");
        Assert.That(bar.Durability, Is.Not.Null);
    }

    /// <summary>TOOL-Q121-02：工具前 30→合格工具 25、产量不变；搜集 5 级再×0.90（与分类勘整×0.95 相乘）；经验仍按原 30 分钟。</summary>
    [Test]
    public void TOOL_Q121_02_MinusFiveAndToolUse()
    {
        var s = AtNode("w12_canal", 50);
        var bar = Give(s, "crowbar");
        Lv(s, "scavenging", 4);
        Assert.That(E.WorkFactor(s, ActionType.Gather, null, "w12_canal_b", true), Is.EqualTo(0.95).Within(1e-12), "4 级：只有分类勘整");
        Lv(s, "scavenging", 5);
        Assert.That(E.WorkFactor(s, ActionType.Gather, null, "w12_canal_b", true), Is.EqualTo(0.95 * 0.90).Within(1e-12), "5 级：工具运用×0.90 相乘");
        Assert.That(E.WorkFactor(s, ActionType.Gather, null, "w12_canal_b", false), Is.EqualTo(0.95).Within(1e-12), "徒手不吃工具运用");
        Assert.That(E.WorkFactor(s, ActionType.Gather, null, "w11_orchard_a", true), Is.EqualTo(1).Within(1e-12), "采集行不吃工具运用");
        var p = E.Preview(s, ActionRequest.Gather("w12_canal_b"));
        Assert.That(p.Outputs.Single().Count, Is.EqualTo(1), "产量不增");
        Assert.That(p.Warnings, Has.None.Contains("工作倍率变为"), "逐段跟踪与开工倍率同口径，不误报倍率变化");
        Assert.That(p.FactorReasons, Has.Some.Contains("工具运用"));
        Start(s, ActionRequest.Gather("w12_canal_b"));
        Assert.That(s.Main!.BaseWorkMinutes, Is.EqualTo(25 * 0.95 * 0.90).Within(1e-9), "工时表值 25 再走工作倍率");
        Run(s);
        Assert.That(s.Skills["scavenging"].Xp, Is.EqualTo(E.FieldXp(E.Resource("w12_canal_b"))), "经验按原 30 分钟");
        Assert.That(E.Resource("w12_canal_b").BaseMinutes, Is.EqualTo(30));
        Assert.That(Count(s, "rubber", "w12_canal"), Is.EqualTo(1));
        Assert.That(bar.Durability, Is.EqualTo(bar.MaxDurability - 1).Within(1e-9));
    }

    /// <summary>TOOL-Q121-03：指定真实工具并锁定；成功磨 1，取消 0；显式工具失效整次拒绝、不暗换。</summary>
    [Test]
    public void TOOL_Q121_03_WearAndLock()
    {
        var s = AtNode("m01_road", 50);
        var bar = Give(s, "crowbar");
        var max = bar.Durability!.Value;
        Start(s, ActionRequest.Gather("m01_road_barrier", bar.InstanceId));
        Assert.That(s.Main!.LockedItemIds, Does.Contain(bar.InstanceId), "行动中锁住");
        E.Advance(s);
        Assert.That(E.Cancel(s).Ok, Is.True);
        Assert.That(bar.Durability, Is.EqualTo(max), "取消不磨损");
        Start(s, ActionRequest.Gather("m01_road_barrier", bar.InstanceId));
        Run(s);
        Assert.That(bar.Durability, Is.EqualTo(max - 1).Within(1e-9), "成功磨 1");
        Assert.That(Count(s, "scrap", "m01_road"), Is.EqualTo(2), "产量不增");
        bar.Durability = 0.5;
        Assert.That(E.CanStart(s, ActionRequest.Gather("m01_road_barrier", bar.InstanceId)), Does.Contain("耐久不足"), "不暗换");
        Assert.That(E.Preview(s, ActionRequest.Gather("m01_road_barrier")).ToolName, Is.Null, "自动候选也要够 1 耐久");
    }

    /// <summary>NAME-Q125-01（资源名部分）：135 条资源用正式名，旧 20 原名、8 份核心记录（B13c）不变；ID 不变。</summary>
    [Category("DesignSync")]
    [Test]
    public void NAME_Q125_01_ResourceDisplayNames()
    {
        var names = ((JArray)Adj()["resourceDisplayNames"]!).ToDictionary(n => (string)n["resourceId"]!, n => (string)n["name"]!);
        Assert.That(names, Has.Count.EqualTo(149));
        var applied = 0;
        foreach (var r in Cfg.Resources)
        {
            if (!names.TryGetValue(r.NodeId, out var n)) continue;
            if (r.IsRecord) { Assert.That(r.Name, Is.Not.EqualTo(n), $"{r.NodeId}：核心记录正式名在 B13c"); continue; }
            Assert.That(r.Name, Is.EqualTo(n), r.NodeId);
            applied++;
        }
        Assert.That(applied, Is.EqualTo(135));
        Assert.That(E.Resource("roots").Name, Is.EqualTo("块根地"), "旧 20 原名保留");
        Assert.That(E.Resource("w12_canal_b").Name, Is.EqualTo("渠边旧密封件"));
    }
}
