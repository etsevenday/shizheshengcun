using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 B5（策划提交 f172677）R18–R19：15 门十级熟练度、经验来源与防刷、旧档迁移、25 个求生被动。
/// 出处：A06 §1–§4，R01 §1、§2、§5；验收 RG01、SK01–SK06、R01-A05。
/// </summary>
public class B5SkillTests : BattleTestBase
{
    private void Ready(GameState s)
    {
        PlayerFirst(s);
        E.BattleStep(s);
        Assert.That(E.AwaitingPlayer(s), Is.True);
    }

    // ───────────── M121：15 门存储与门槛（H1、H2、H6） ─────────────

    [Test]
    public void RG01_SK01_FifteenSkills_FiveNodesEach()
    {
        var ids = new[] { "unarmed", "blade", "axe", "spear", "blunt", "archery", "firearms", "shield", "command", "medicine",
                          "crafting", "gathering", "scavenging", "cooking", "athletics" };
        Assert.That(Cfg.Skills.Select(x => x.Id), Is.EqualTo(ids), "A06 §4 十五门");
        Assert.That(Cfg.Skills.Single(x => x.Id == "blade").Name, Is.EqualTo("刀剑"));
        Assert.That(Cfg.Skills.Single(x => x.Id == "archery").Name, Is.EqualTo("弓弩"));
        Assert.That(Cfg.SkillNodes, Has.Count.EqualTo(75));
        foreach (var id in ids)
            Assert.That(Cfg.SkillNodes.Where(n => n.SkillId == id).Select(n => n.Level).OrderBy(x => x), Is.EqualTo(new[] { 1, 3, 5, 7, 10 }), id);
        var survival = Cfg.SkillNodes.Where(n => n.IsSurvival).ToList();
        Assert.That(survival, Has.Count.EqualTo(25), "求生被动 25 个");
        Assert.That(survival.Select(n => n.SkillId).Distinct(), Is.EquivalentTo(new[] { "crafting", "gathering", "scavenging", "cooking", "athletics" }));
        Assert.That(Cfg.SkillNodes.Single(n => n.Id == "axe_10").Name, Is.EqualTo("破阵"));
        Assert.That(Cfg.SkillNodes.Single(n => n.Id == "axe_10").BaseEnergy, Is.EqualTo(1.15));
        Assert.That(Cfg.SkillKindBaseEnergy["axe"], Is.EqualTo(0.65));
        var s = New();
        Assert.That(s.Skills.Keys, Is.EquivalentTo(ids), "新局 15 门");
        Assert.That(s.Skills.Values.All(x => x.Level == 1 && x.Xp == 0), Is.True, "全部 1 级 0 经验");
    }

    [Test]
    public void SK02_Threshold_Total2700_StopsAt10()
    {
        Assert.That(Enumerable.Range(1, 9).Select(l => E.NextLevelXp("gathering", l)), Is.EqualTo(new long[] { 100, 150, 200, 250, 300, 350, 400, 450, 500 }), "门槛 50×(L+1)");
        Assert.That(E.TotalXpToCap("gathering"), Is.EqualTo(2700));
        var s = New();
        E.GainXp(s, "gathering", 2699);
        Assert.That(s.Skills["gathering"].Level, Is.EqualTo(9));
        Assert.That(s.Skills["gathering"].Xp, Is.EqualTo(499));
        Assert.That(E.SkillMaxed(s, "gathering"), Is.False);
        Assert.That(E.GainXp(s, "gathering", 50), Is.EqualTo(1));
        Assert.That(s.Skills["gathering"].Level, Is.EqualTo(10));
        Assert.That(s.Skills["gathering"].Xp, Is.EqualTo(0), "溢出不保留、不兑换");
        Assert.That(s.Log.Last().Text, Does.Contain("满级"));
        Assert.That(E.GainXp(s, "gathering", 999), Is.EqualTo(0));
        Assert.That(s.Skills["gathering"].Xp, Is.EqualTo(0), "Lv10 停止累计");
        Assert.That(E.XpText(s, "gathering", 2), Is.EqualTo("采集已满10级，不再获得经验"));
        var v = E.SkillViews(s).Single(x => x.Id == "gathering");
        Assert.That((v.Level, v.Cap, v.Maxed, v.NextXp), Is.EqualTo((10, 10, true, 0L)));
    }

    [Test]
    public void SK02_CombatXp_StopsAt10()
    {
        var s = Fight(41, "riot");
        Ready(s);
        Arm(s, "hatchet");
        s.Skills["axe"].Level = 10;
        s.Skills["axe"].Xp = 0;
        Force(s, Below(0.01), AtLeast(0.99), Any());
        Enemy(s, 1).Hp = 50;
        Assert.That(Cmd(s, "basic_attack", 1).Ok, Is.True);
        Assert.That(s.Skills["axe"].Xp, Is.EqualTo(0), "满级攻击命中不再加经验");
        Assert.That(s.Battle!.Enemies[0].XpHits["axe"], Is.EqualTo(1), "防刷计数照记");
    }

    [Test]
    public void H2_AxeWeapons_TrainAxe_ShieldCommandHaveOwnSkill()
    {
        foreach (var w in Cfg.Weapons.Where(w => w.WeaponKind == "axe")) Assert.That(w.SkillId, Is.EqualTo("axe"), w.Id);
        foreach (var w in Cfg.Weapons.Where(w => w.WeaponKind == "shield")) Assert.That(w.SkillId, Is.EqualTo("shield"), w.Id);
        foreach (var w in Cfg.Weapons.Where(w => w.WeaponKind == "command")) Assert.That(w.SkillId, Is.EqualTo("command"), w.Id);
        foreach (var w in Cfg.Weapons.Where(w => w.WeaponKind == "medical")) Assert.That(w.SkillId, Is.EqualTo("medicine"), w.Id);
        Assert.That(Cfg.Weapons.Single(w => w.Id == "wooden_shield").CanAttack, Is.False, "盾的技能仍在 B6/B7");
        Assert.That(Cfg.CombatActions.Single(a => a.Id == "cleave").Skills, Is.EquivalentTo(new[] { "blade", "axe" }), "现行劈砍斧仍可用");
        // 简易斧的劈砍按斧等级判定
        var s = Fight(42, "worker");
        Ready(s);
        Arm(s, "hatchet");
        Assert.That(E.CommandBlocked(s, BattleCommand.Of("cleave", 1)), Is.Not.Null);
        s.Skills["axe"].Level = 3;
        Assert.That(E.CommandBlocked(s, BattleCommand.Of("cleave", 1)), Is.Null);
    }

    [Test]
    public void SK06_AntiviralCure_ReachableAt10()
    {
        var rc = Cfg.Recipes.Single(r => r.Id == "make_antiviral_cure");
        Assert.That(rc.MinSkills, Is.EquivalentTo(new Dictionary<string, int> { ["medicine"] = 10, ["crafting"] = 10 }));
        var u = New();
        foreach (var kv in rc.Inputs) E.DebugGive(u, kv.Key, kv.Value, "camp");
        var req = ActionRequest.Craft("make_antiviral_cure");
        Assert.That(E.CanStart(u, req), Does.Contain("需要医疗10级"));
        E.GainXp(u, "medicine", 2700);
        E.GainXp(u, "crafting", 2700);
        Assert.That(E.CanStart(u, req), Does.Not.Contain("需要"), "医疗10/制造10 过了熟练门槛（正常经验可达；剩下的是锅等配方条件）");
        Assert.That(Cfg.Recipes.Count(r => r.Outputs.ContainsKey("antiviral_cure")), Is.EqualTo(1), "没有第三条治愈入口");
    }

    // ───────────── M122：经验来源与防刷（H4） ─────────────

    private GameState AtWoods(uint seed = 7)
    {
        var s = New(seed);
        E.DebugTeleport(s, "woods");
        E.DebugSetProgress(s, "woods", 75);
        return s;
    }

    private void GatherOnce(GameState s, string node)
    {
        Start(s, ActionRequest.Gather(node));
        s.Main!.HazardHit = false;
        Run(s);
    }

    [Test]
    public void H4_GatherXp_Plus2_ThreePerNodePerDay()
    {
        var s = AtWoods();
        s.Resources["berries"].Available = 99;
        Assert.That(E.Preview(s, ActionRequest.Gather("berries")).XpGains, Has.Some.Contains("采集经验+2"));
        for (var k = 0; k < 4; k++) GatherOnce(s, "berries");
        Assert.That(s.Skills["gathering"].Xp, Is.EqualTo(6), "同资源点同日最多3次");
        Assert.That(s.Log.Last(x => x.Text.Contains("野果")).Text, Does.Contain("今日已计3次"));
        Assert.That(E.Preview(s, ActionRequest.Gather("berries")).XpGains.Single(), Does.Contain("不再给经验"));
        s.Resources["woods_fiber"].Available = 99;
        GatherOnce(s, "woods_fiber");
        Assert.That(s.Skills["gathering"].Xp, Is.EqualTo(8), "另一个资源点单独计");
        // 次日（世界日）重新计
        E.DebugSetStats(s, 20, 80, 80, 18, 90, 60);
        AdvanceTo(s, At(2, 8, 0));
        s.Resources["berries"].Available = 99;
        GatherOnce(s, "berries");
        Assert.That(s.Skills["gathering"].Xp, Is.EqualTo(10), "次日重新给经验");
        Assert.That(s.Player.DailyCounts.Keys.All(k => k.StartsWith("2|")), Is.True, "只保留当日计数");
        Assert.That(s.Skills["scavenging"].Xp, Is.EqualTo(0));
    }

    [Test]
    public void H4_CacheTrainsScavenging_CancelGivesNothing()
    {
        var s = New(7);
        E.DebugTeleport(s, "village");
        E.DebugSetProgress(s, "village", 100);
        Start(s, ActionRequest.Gather("supply_box"));
        E.Cancel(s);
        Assert.That(s.Skills["scavenging"].Xp, Is.EqualTo(0), "取消不给经验");
        Assert.That(s.Player.DailyCounts, Is.Empty);
        Start(s, ActionRequest.Gather("misc_box"));
        s.Main!.HazardHit = false;
        Run(s);
        Assert.That(s.Skills["scavenging"].Xp, Is.EqualTo(2), "单次箱子练搜集");
        Assert.That(s.Skills["gathering"].Xp, Is.EqualTo(0));
    }

    [Test]
    public void H4_TravelXp_OncePerDirectedEdgePerDay()
    {
        var s = New(7);
        E.DebugTeleport(s, "field");
        E.DebugUnlockEdge(s, "field__stream");
        Assert.That(E.Preview(s, ActionRequest.Travel("stream")).XpGains.Single(), Is.EqualTo("行旅经验+1"));
        Start(s, ActionRequest.Travel("stream"));
        Run(s);
        Assert.That(s.CurrentLocation, Is.EqualTo("stream"));
        Assert.That(s.Skills["athletics"].Xp, Is.EqualTo(1));
        Start(s, ActionRequest.Travel("field"));
        Run(s);
        Assert.That(s.Skills["athletics"].Xp, Is.EqualTo(2), "反方向是另一条有向边");
        Assert.That(E.Preview(s, ActionRequest.Travel("stream")).XpGains.Single(), Does.Contain("不再给"));
        Start(s, ActionRequest.Travel("stream"));
        Run(s);
        Assert.That(s.Skills["athletics"].Xp, Is.EqualTo(2), "同向边同日只给1次");
        Assert.That(s.Log.Any(x => x.Text.Contains("到达") && x.Text.Contains("行旅经验+1")), Is.True);
    }

    // ───────────── M123：旧档 10→11（H5；验收 SK03、R01-A05） ─────────────

    /// <summary>做一份 schema 10 的旧档 JSON：只有旧 8 门技能，按给定等级/经验。</summary>
    private string OldSave(GameState s, Dictionary<string, (int lv, long xp)> skills)
    {
        var j = Newtonsoft.Json.Linq.JObject.Parse(Survival.Core.Save.SaveSerializer.Serialize(s));
        var sk = new Newtonsoft.Json.Linq.JObject();
        foreach (var id in new[] { "crafting", "medicine", "firearms", "blade", "spear", "unarmed", "archery", "blunt" })
        {
            var (lv, xp) = skills.TryGetValue(id, out var v) ? v : (1, 0L);
            sk[id] = new Newtonsoft.Json.Linq.JObject { ["Level"] = lv, ["Xp"] = xp };
        }
        j["Actors"]![GameState.PlayerId]!["Skills"] = sk;
        ((Newtonsoft.Json.Linq.JObject)j["Actors"]![GameState.PlayerId]!).Remove("DailyCounts");
        j["SchemaVersion"] = "10";
        return j.ToString();
    }

    [Test]
    public void SK03_R01A05_Migration10To11_BladeCopiedOnce_CappedAt10_Idempotent()
    {
        var s = New();
        var json = OldSave(s, new Dictionary<string, (int, long)> { ["blade"] = (7, 123), ["crafting"] = (13, 40), ["medicine"] = (10, 5) });
        var m = Survival.Core.Save.SaveSerializer.Deserialize(json, Cfg, out var notes);
        Assert.That(m.SchemaVersion, Is.EqualTo("11"));
        var p = m.Player.Skills;
        Assert.That(p.Keys, Is.EquivalentTo(Cfg.Skills.Select(x => x.Id)), "十五门");
        Assert.That((p["blade"].Level, p["blade"].Xp), Is.EqualTo((7, 123L)), "刀剑沿用旧刀斧");
        Assert.That((p["axe"].Level, p["axe"].Xp), Is.EqualTo((7, 123L)), "斧复制旧刀斧");
        Assert.That((p["crafting"].Level, p["crafting"].Xp, p["crafting"].LegacyLevel, p["crafting"].LegacyXp), Is.EqualTo((10, 0L, (int?)13, (long?)40)), "超过10截到10，原值只作历史");
        Assert.That((p["medicine"].Level, p["medicine"].Xp), Is.EqualTo((10, 0L)), "10级的余经验清零");
        foreach (var id in new[] { "shield", "command", "gathering", "scavenging", "cooking", "athletics" })
            Assert.That((p[id].Level, p[id].Xp), Is.EqualTo((1, 0L)), id);
        Assert.That(notes.Single(), Does.Contain("刀斧7级（经验123）复制为刀剑与斧").And.Contain("制造13级截到10级"));
        Assert.That(m.Items.Count, Is.EqualTo(s.Items.Count), "不发物资");
        // 再存再读不再迁移、不再复制
        m.Player.Skills["axe"].Level = 8;
        var again = Survival.Core.Save.SaveSerializer.Deserialize(Survival.Core.Save.SaveSerializer.Serialize(m), Cfg, out var notes2);
        Assert.That(notes2, Is.Empty);
        Assert.That(again.Player.Skills["axe"].Level, Is.EqualTo(8), "不再从刀剑复制");
    }

    [Test]
    public void SK03_Migration_WorldEnemyCounters_NotReset()
    {
        var s = Fight(43, "riot");
        var w = s.WorldEnemies.Single();
        w.XpHits["blade"] = 3;
        s.Battle!.Enemies.Single().XpHits["blade"] = 3;
        var json = OldSave(s, new Dictionary<string, (int, long)>());
        var j = Newtonsoft.Json.Linq.JObject.Parse(json);
        foreach (var we in (Newtonsoft.Json.Linq.JArray)j["WorldEnemies"]!) ((Newtonsoft.Json.Linq.JObject)we["XpHits"]!).Remove("axe");
        var m = Survival.Core.Save.SaveSerializer.Deserialize(j.ToString(), Cfg, out _);
        Assert.That(m.WorldEnemies.Single().XpHits["blade"], Is.EqualTo(3));
        Assert.That(m.WorldEnemies.Single().XpHits["axe"], Is.EqualTo(3), "旧刀斧防刷计数同样算给斧，不重置");
        Assert.That(m.Battle!.Enemies.Single().XpHits["axe"], Is.EqualTo(3));
    }

    // ───────────── M124：求生被动（H7；验收 SK01、SK04） ─────────────

    private static void Lv(GameState s, string id, int lv) => s.Skills[id].Level = lv;

    [Test]
    public void H7_Gathering07_WorkX09_Gathering03_HazardX09_Scavenging07_X08()
    {
        var s = AtWoods();
        var w1 = E.WorkFactor(s, ActionType.Gather, null, "berries");
        Lv(s, "gathering", 7);
        var reasons = new List<string>();
        Assert.That(E.WorkFactor(s, ActionType.Gather, reasons, "berries"), Is.EqualTo(w1 * 0.9).Within(1e-12), "熟练采伐×0.9");
        Assert.That(reasons, Has.Some.Contains("采集7级·熟练采伐"));
        var wood = Cfg.Resources.Single(r => r.NodeId == "wood");
        var p0 = wood.Hazard!.Probability;
        Assert.That(E.HazardChance(s, wood), Is.EqualTo(p0 * 0.9).Within(1e-12), "稳手采集×0.9（3级起）");
        var misc = Cfg.Resources.Single(r => r.NodeId == "misc_box");
        Assert.That(E.HazardChance(s, misc), Is.EqualTo(misc.Hazard!.Probability).Within(1e-12), "箱子不吃采集被动");
        Lv(s, "scavenging", 7);
        Assert.That(E.HazardChance(s, misc), Is.EqualTo(misc.Hazard!.Probability * 0.8).Within(1e-12), "危险辨识×0.8");
        Assert.That(E.WorkFactor(s, ActionType.Gather, null, "misc_box"), Is.EqualTo(w1).Within(1e-12), "箱子不吃熟练采伐");
    }

    [Test]
    public void H7_Gathering05_BasicPlusOne_Gathering10_FirstDailyPlusOne()
    {
        var s = AtWoods();
        s.Resources["woods_fiber"].Available = 2;
        Lv(s, "gathering", 5);
        Assert.That(E.Preview(s, ActionRequest.Gather("woods_fiber")).Outputs.Single().Count, Is.EqualTo(2), "受库存限制");
        s.Resources["woods_fiber"].Available = 10;
        Assert.That(E.Preview(s, ActionRequest.Gather("woods_fiber")).Outputs.Single().Count, Is.EqualTo(3), "纤维 2+1");
        s.Resources["berries"].Available = 10;
        Assert.That(E.Preview(s, ActionRequest.Gather("berries")).Outputs.Single().Count, Is.EqualTo(2), "野果不是木/石/纤维");
        Lv(s, "gathering", 10);
        Assert.That(E.Preview(s, ActionRequest.Gather("berries")).Outputs.Single().Count, Is.EqualTo(3), "精取：今日首次+1");
        GatherOnce(s, "berries");
        Assert.That(Count(s, "berry", "woods"), Is.EqualTo(3));
        Assert.That(s.Resources["berries"].Available, Is.EqualTo(7), "仍扣库存");
        Assert.That(E.Preview(s, ActionRequest.Gather("berries")).Outputs.Single().Count, Is.EqualTo(2), "第二次不再+1");
        Assert.That(E.Preview(s, ActionRequest.Gather("woods_fiber")).Outputs.Single().Count, Is.EqualTo(4), "纤维 2+1+1");
        var t = New(7);
        E.DebugTeleport(t, "scrapyard");
        E.DebugSetProgress(t, "scrapyard", 100);
        t.Resources["metal_heap"].Available = 10;
        Lv(t, "gathering", 10);
        Assert.That(E.Preview(t, ActionRequest.Gather("metal_heap")).Outputs.Single().Count, Is.EqualTo(2), "废金属是工业材料，不吃精取");
    }

    [Test]
    public void H7_Athletics_MoveLight_Visited_RateFactors()
    {
        var s = New(7);
        E.DebugTeleport(s, "field");
        E.DebugUnlockEdge(s, "field__stream");
        var w1 = E.WorkFactor(s, ActionType.Travel, null, "stream");
        Lv(s, "athletics", 5);
        Assert.That(E.WorkFactor(s, ActionType.Travel, null, "stream"), Is.EqualTo(w1 * 0.95).Within(1e-12), "≤20kg 轻装步伐×0.95");
        E.DebugGive(s, "stone", 50);   // 随身超过 20 公斤
        Assert.That(E.CarriedGramsOf(s, GameState.PlayerId), Is.GreaterThan(20000));
        Assert.That(E.WorkFactor(s, ActionType.Travel, null, "stream"), Is.EqualTo(w1 * Cfg.WorkFactors.Overweight).Within(1e-12), "超过20公斤不吃轻装步伐");
        s.Items.RemoveAll(i => i.ItemId == "stone" && i.IsCarried);
        Lv(s, "athletics", 10);
        Assert.That(E.WorkFactor(s, ActionType.Travel, null, "stream"), Is.EqualTo(w1 * 0.95).Within(1e-12), "没走过的路仍按5级");
        Start(s, ActionRequest.Travel("stream"));
        Run(s);
        Assert.That(s.TraveledEdges, Does.Contain("field__stream"));
        var reasons = new List<string>();
        Assert.That(E.WorkFactor(s, ActionType.Travel, reasons, "field"), Is.EqualTo(w1 * 0.90).Within(1e-12), "走过的路远途规划×0.90（替代5级）");
        Assert.That(reasons, Has.Some.Contains("远途规划"));
        // 节奏行进/耐力行进：移动负体力
        Lv(s, "athletics", 1);
        Assert.That(E.GearRateNote(s, ActionType.Travel, "field"), Is.Null);
        Lv(s, "athletics", 3);
        Assert.That(E.GearRateNote(s, ActionType.Travel, "field"), Does.Contain("熟练：移动负体力×0.95"));
        Lv(s, "athletics", 7);
        Assert.That(E.GearRateNote(s, ActionType.Travel, "field"), Does.Contain("移动负体力×0.9（"), "7级替代3级");
        // 实际推进：同条件下 7 级比 1 级少扣体力 10%
        double Drop(int lv)
        {
            var u = New(7);
            E.DebugTeleport(u, "field");
            E.DebugUnlockEdge(u, "field__stream");
            Lv(u, "athletics", lv);
            var e0 = u.Stats.Energy;
            Start(u, ActionRequest.Travel("stream"));
            Run(u);
            return e0 - u.Stats.Energy;
        }
        Assert.That(Drop(7), Is.EqualTo(Drop(1) * 0.9).Within(1e-9));
    }

    [Test]
    public void H7_Crafting05_Repair25Pct_Scavenging10_ExtraScrap()
    {
        var s = New();
        var rc = Cfg.Recipes.Single(r => r.Id == "repair_selected_tool");
        Assert.That(E.RepairFraction(s, rc), Is.EqualTo(0.20).Within(1e-12));
        Lv(s, "crafting", 5);
        Assert.That(E.RepairFraction(s, rc), Is.EqualTo(0.25).Within(1e-12), "精细加工+5个百分点");
        E.DebugCreateEquipment(s, "repair_manual", "white", ItemInstance.EquipOwner(GameState.PlayerId, EquipSlots.SurvivalAccessory));
        Assert.That(E.RepairFraction(s, rc), Is.EqualTo(0.25).Within(1e-12), "与维护手册25%取较高，不相加");

        var it = E.DebugCreateEquipment(s, "machete", "white", ItemInstance.Carried);
        var req = new ActionRequest { Type = ActionType.Craft, TargetId = "salvage_equipment", InstanceId = it.InstanceId };
        Assert.That(E.Preview(s, req).Outputs.Single().Count, Is.EqualTo(1));
        Lv(s, "scavenging", 10);
        var p = E.Preview(s, req);
        Assert.That(p.Outputs.Single().Count, Is.EqualTo(2), "回收专家多返废金属1");
        var scrap0 = s.Items.Where(i => i.ItemId == "scrap").Sum(i => i.Count);
        Start(s, req);
        Run(s);
        Assert.That(s.Items.Where(i => i.ItemId == "scrap").Sum(i => i.Count) - scrap0, Is.EqualTo(2));
        var shirt = E.DebugCreateEquipment(s, "work_shirt", "white", ItemInstance.Carried);
        var p2 = E.Preview(s, new ActionRequest { Type = ActionType.Craft, TargetId = "salvage_equipment", InstanceId = shirt.InstanceId });
        Assert.That(p2.Outputs.Select(o => (o.ItemId, o.Count)), Is.EquivalentTo(new[] { ("cloth", 1), ("scrap", 1) }), "布料装备另得废金属1");
    }

    [Test]
    public void H7_PassiveWithGear_FloorsSameAsEquipment()
    {
        // 天然数据：远征长裤×0.9 × 路线罗盘×0.95 × 远途规划×0.90 = 0.7695（未触下限）
        var s = New(7);
        E.DebugTeleport(s, "field");
        E.DebugUnlockEdge(s, "field__stream");
        s.TraveledEdges.Add("field__stream");
        Lv(s, "athletics", 10);
        E.DebugCreateEquipment(s, "expedition_trousers", "white", ItemInstance.EquipOwner(GameState.PlayerId, EquipSlots.Bottom));
        E.DebugCreateEquipment(s, "route_compass", "white", ItemInstance.EquipOwner(GameState.PlayerId, EquipSlots.SurvivalAccessory));
        Assert.That(E.GearWorkFactor(s, GameState.PlayerId, ActionType.Travel, "stream"), Is.EqualTo(0.9 * 0.95 * 0.9).Within(1e-12));
        // 把被动值调到 0.5 的配置：合成后下限 ×0.70；负小时率下限 ×0.75
        var cfg = Survival.Core.Config.ConfigLoader.FromJson(File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "config.v0.6.json")));
        cfg.SkillNodes.Single(n => n.Id == "athletics_10").Effects.Single().Value = 0.5;
        cfg.SkillNodes.Single(n => n.Id == "athletics_07").Effects.Single().Value = 0.5;
        var e2 = new GameEngine(cfg);
        var reasons = new List<string>();
        Assert.That(e2.GearWorkFactor(s, GameState.PlayerId, ActionType.Travel, "stream", reasons), Is.EqualTo(0.70).Within(1e-12));
        Assert.That(reasons, Has.Some.Contains("合成下限×0.7"));
        Assert.That(e2.GearRateNote(s, ActionType.Travel, "stream"), Does.Contain("移动负体力×0.75"), "负小时率合成下限×0.75");
    }

    [Test]
    public void SK01_SurvivalPassiveLines_ByLevel()
    {
        var s = New();
        Assert.That(E.SurvivalPassiveLines(s.Player), Has.Count.EqualTo(5), "1级五门各一个开放类被动");
        Lv(s, "gathering", 10);
        var lines = E.SurvivalPassiveLines(s.Player);
        Assert.That(lines, Has.Count.EqualTo(9));
        Assert.That(lines, Has.Some.EqualTo("采集3级·稳手采集：采集划伤概率×0.9（生效中）"));
        Assert.That(lines.Single(x => x.StartsWith("制造1级")), Does.Contain("待策划确认"));
        Assert.That(Cfg.SkillNodes.Count(n => n.IsSurvival && n.IsLive), Is.EqualTo(11));
    }

    // ───────────── M125：人物页与开发入口（H10） ─────────────

    [Test]
    public void H10_SkillViews_Groups_DevSetLevels()
    {
        var s = New();
        var v = E.SkillViews(s);
        Assert.That((v.Count(x => x.Category == "combat"), v.Count(x => x.Category == "both"), v.Count(x => x.Category == "survival")), Is.EqualTo((9, 1, 5)));
        Assert.That(v.All(x => x.Nodes.Count == 5 && x.Cap == 10 && x.NextXp == 100), Is.True);
        Assert.That(E.DebugSetSkillLevels(s, 10, true), Does.Contain("求生五门"));
        Assert.That(s.Skills["athletics"].Level, Is.EqualTo(10));
        Assert.That(s.Skills["blade"].Level, Is.EqualTo(1), "只改求生五门");
        Assert.That(E.SurvivalPassiveLines(s.Player), Has.Count.EqualTo(25));
        E.DebugSetSkillLevels(s, 1, false);
        Assert.That(s.Skills.Values.All(x => x.Level == 1 && x.Xp == 0), Is.True);
    }

    // ───────────── M127：B5 线上第 1 轮试玩修复 ─────────────

    [Test]
    public void Fix1_NodeDisplayText_NoInternalIds()
    {
        foreach (var n in Cfg.SkillNodes)
        {
            Assert.That(n.Text, Does.Not.Match("[a-z_]{3,}"), n.Id);
            Assert.That(n.Text, Does.Not.Contain("R04").And.Not.Contain("kg").And.Not.Contain("负E"), n.Id);
        }
        Assert.That(Cfg.SkillNodes.Single(x => x.Id == "medicine_10").Text, Does.StartWith("消耗盐水剂×1+医用纱布×1；健康≤25%上限时健康+1.5×(1+0.02×医疗等级)"));
        Assert.That(Cfg.SkillNodes.Single(x => x.Id == "athletics_05").Text, Is.EqualTo("≤20公斤时移动基础工作量×0.95"));
        Assert.That(Cfg.SkillNodes.Single(x => x.Id == "scavenging_10").Text, Does.Contain("废金属×1").And.Contain("80%上限暂未实装"));
        Assert.That(Cfg.SkillNodes.Single(x => x.Id == "cooking_10").Text, Does.Contain("饱食额外+3"));
        Assert.That(Cfg.SkillNodes.Single(x => x.Id == "medicine_01").EffectText, Does.Contain("bandage1"), "原文保留");
    }

    [Test]
    public void Fix3_SupersededPassive_NotLive()
    {
        var s = New();
        Lv(s, "athletics", 7);
        var lines = E.SurvivalPassiveLines(s.Player);
        Assert.That(lines.Single(x => x.StartsWith("行旅3级")), Does.EndWith("（已被7级替代）"));
        Assert.That(lines.Single(x => x.StartsWith("行旅7级")), Does.EndWith("（生效中）"));
        Assert.That(lines.Single(x => x.StartsWith("行旅5级")), Does.EndWith("（生效中）"), "5级轻装步伐没被替代");
        Lv(s, "athletics", 10);
        Assert.That(E.SurvivalPassiveLines(s.Player).Single(x => x.StartsWith("行旅5级")), Does.EndWith("（生效中）"), "远途规划只替代走过的路，5级仍生效");
    }

    [Test]
    public void Fix4_CachePreview_NoDailyFraction_Q61Note()
    {
        var s = New(7);
        E.DebugTeleport(s, "village");
        E.DebugSetProgress(s, "village", 100);
        Assert.That(E.Preview(s, ActionRequest.Gather("supply_box")).XpGains.Single(), Is.EqualTo("搜集经验+2"));
        var t = AtWoods();
        Lv(t, "gathering", 7);
        t.Resources["berries"].Available = 10;
        Assert.That(E.Preview(t, ActionRequest.Gather("berries")).Notes, Has.Some.Contains("省下的时间不足5分钟时，实际耗时不缩短"));
        Lv(t, "gathering", 1);
        Assert.That(E.Preview(t, ActionRequest.Gather("berries")).Notes, Has.None.Contains("不足5分钟"));
    }
}
