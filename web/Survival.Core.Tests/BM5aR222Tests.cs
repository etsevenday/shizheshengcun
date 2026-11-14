using System.Reflection;
using Survival.Core.Config;
using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// BM5a R222（02 Q563—Q568 策划答复；R05「2026-11-13 B41e3 Q563—Q569」§Q566、§Q567，验收 Q566-A01—A04）：
/// 工坊取消拒绝句、战斗中鸟瞰／任务原句、旧敌踪两句维持删除、势力 NPC／代班固定地点安全、单人练习预选、A06 危险辨识说明。
/// </summary>
public class BM5aR222Tests : TestBase
{
    private static string Root()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d != null && !File.Exists(Path.Combine(d.FullName, "web", "Survival.sln"))) d = d.Parent;
        return d!.FullName;
    }

    /// <summary>R05 §Q566 当前 16 个岗位地点（验证基线）。</summary>
    private static readonly string[] Posts =
    {
        "h01_gate", "h02_square", "h03_workshop", "h04_infirmary", "h05_appraisal", "h06_shelter",
        "c02_church", "c04_alms", "c05_admonition", "c06_archive", "c07_sanctuary",
        "r04_reception", "r05_testing", "r06_archive", "r07_workshop", "r08_observation",
    };

    /// <summary>岗位地点与野外直接相邻的物理边起点（巡游／逃离／重现的边界）。</summary>
    private static readonly string[] WildNeighbors = { "m02_checkpoint", "w01_farm", "w02_forest", "a01_yard", "c01_pilgrim" };

    private T Call<T>(GameEngine e, string name, params object[] args) =>
        (T)typeof(GameEngine).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(e, args)!;

    private static string PoolEnemy(GameConfig cfg, string loc) =>
        cfg.Locations.Single(l => l.Id == loc).PopulationWeights?.Keys.First() ?? "shambler";

    // ───────────── Q563 ─────────────

    /// <summary>Q563 选 A：工坊「取消当前批／取消排队」战斗中拒绝句统一为「战斗时不能操作」；不再含「或敌踪待处理」。</summary>
    [Test]
    public void Q563_StationCancel_BattleText()
    {
        Assert.That(GameEngine.StationBattleBlocked, Is.EqualTo("战斗时不能操作"));
        var s = New();
        s.Battle = new BattleState();
        Assert.That(E.CancelStationBatch(s, "kiln").Reason, Is.EqualTo("战斗时不能操作"));
        Assert.That(E.CancelStationQueue(s, "kiln").Reason, Is.EqualTo("战斗时不能操作"));
        s.Battle = null;
        Assert.That(E.CancelStationBatch(s, "kiln").Reason, Is.Not.EqualTo("战斗时不能操作"), "不在战斗中不出这句");
        var src = File.ReadAllText(Path.Combine(Root(), "src", "Core", "Engine", "GameEngine.Stations.cs"));
        Assert.That(src, Does.Not.Contain("战斗或敌踪待处理时不能操作"));
    }

    // ───────────── Q564／Q565 ─────────────

    /// <summary>Q564 选 A：实际战斗中鸟瞰仍写「先处理眼前的敌踪」；敌踪卡在场（无战斗）不恢复旧锁。</summary>
    [Test]
    public void Q564_CityOverview_InBattle_KeepsText()
    {
        var s = New();
        s.Progress.Flags.UnionWith(new[] { "registration_authorized", "hub_registered" });
        s.GameMinute = At(2, 9, 0);
        s.SpawnNextCheck = int.MaxValue;
        s.PatrolNextCheck = int.MaxValue;
        E.DebugMapTeleport(s, "h02_square");
        ReadDialogues(s);
        E.MarkCityOverviewShown(s);
        Assert.That(E.CityOverviewBlocked(s), Is.Not.EqualTo("先处理眼前的敌踪"));
        s.Battle = new BattleState();
        Assert.That(E.CityOverviewBlocked(s), Is.EqualTo("先处理眼前的敌踪"));
        // 任务面板（接取／办理）同一原句仍在内核，只在 s.Battle 时给出（B26a Q243 用例覆盖实际战斗中的接取）
        var quests = File.ReadAllText(Path.Combine(Root(), "src", "Core", "Engine", "GameEngine.Quests.cs"));
        Assert.That(quests, Does.Contain("if (s.Battle != null) return \"先处理眼前的敌踪\";"));
    }

    /// <summary>Q565 选 A：「先处理敌踪再存档。」与游戏菜单「请在敌踪界面选择「接敌」或「绕行」……」维持删除，内核与网页壳都不再出现。</summary>
    [Test]
    public void Q565_OldEncounterTexts_StayDeleted()
    {
        var files = Directory.GetFiles(Path.Combine(Root(), "src"), "*.cs", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(Path.Combine(Root(), "web", "Survival.Web"), "*.*", SearchOption.AllDirectories)
                .Where(f => (f.EndsWith(".cs") || f.EndsWith(".razor") || f.EndsWith(".js")) && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")));
        foreach (var f in files)
        {
            var t = File.ReadAllText(f);
            Assert.That(t, Does.Not.Contain("\"先处理敌踪再存档。\""), f);
            Assert.That(t, Does.Not.Contain("请在敌踪界面选择"), f);
        }
    }

    // ───────────── Q566 ─────────────

    /// <summary>Q566-A01：岗位绑定（city.npcs[].locationId）恰为 16 个岗位地点，安全资格只看配置绑定；独立危险检查哨不是安全地点。</summary>
    [Test]
    public void Q566_A01_PostsAreSafeByBinding()
    {
        var bound = Cfg.City.Npcs.Select(n => n.LocationId).Where(l => l != "").Distinct().OrderBy(l => l, StringComparer.Ordinal).ToList();
        Assert.That(bound, Is.EquivalentTo(Posts));
        foreach (var p in Posts) Assert.That(E.NpcSafePlace(p), Is.True, p);
        foreach (var w in WildNeighbors) Assert.That(E.NpcSafePlace(w), Is.False, w);
        Assert.That(E.NpcSafePlace("camp"), Is.False);
    }

    /// <summary>
    /// Q566-A01：在每个岗位地点都不生成、不首次补充、不刷新、不组卡；开发菜单放敌拒绝；即使人为放进敌人，随机组卡概率也为 0。
    /// 刷新／巡游跑满 14 天后这些地点仍没有敌人。
    /// </summary>
    [Test]
    public void Q566_A01_NoSpawnRefreshGroupAtPosts()
    {
        var s = New();
        foreach (var p in Posts)
        {
            Assert.That(E.PopRegionOf(p), Is.Null, p);
            Assert.That(E.DebugSpawnBlocked(s, p), Is.Not.Null, p);
        }
        foreach (var r in Cfg.WorldEnemies.Regions)
            Assert.That(E.RegionSpawnCandidates(s, r).Intersect(Posts), Is.Empty, r.Id);
        foreach (var rarity in new[] { "common", "rare", "strong" })
            Assert.That(E.SpawnCandidates(s, rarity).Intersect(Posts), Is.Empty, rarity);

        // 人为构造的异常状态（内核测试入口）：组卡概率仍为 0
        var t = New();
        t.GameMinute = At(5, 10, 0);
        foreach (var p in Posts)
        {
            E.DebugSpawnEnemy(t, "shambler", p);
            Assert.That(E.EncounterChance(t, p, ActionType.Explore), Is.EqualTo(0), p);
            Assert.That(E.EncounterChance(t, p, ActionType.Clear), Is.EqualTo(0), p);
        }

        // 新人口区全部进入过，刷新与巡游各跑 14 天的检查次数
        foreach (var r in Cfg.WorldEnemies.Regions) s.Population.EnteredRegions.Add(r.Id);
        s.GameMinute = At(30, 8, 0);   // 各区刷新起始分钟与分层开放日都已过
        for (var i = 0; i < 14 * 24; i++)
        {
            E.DebugRegionSpawnCheck(s);
            E.DebugSpawnCheck(s);
            Call<object?>(E, "Patrol", s);
        }
        Assert.That(s.WorldEnemies.Where(w => Posts.Contains(w.LocationId)), Is.Empty);
        Assert.That(s.WorldEnemies.Any(w => WildNeighbors.Contains(w.LocationId)), Is.True, "相邻野外照常有敌人（样本有效）");
    }

    /// <summary>Q566-A02：相邻危险地点的巡游、动物逃离与躲避重现候选都不含岗位地点；无合法目标沿原动作收束（逃离原地隐藏、重现留原地）。</summary>
    [Test]
    public void Q566_A02_PatrolFleeReappear_NeverIntoPosts()
    {
        foreach (var from in WildNeighbors)
        {
            var s = New();
            var postsNear = E.PhysicalNeighbors(from).Intersect(Posts).ToList();
            Assert.That(postsNear, Is.Not.Empty, $"{from} 与岗位地点相邻（样本有效）");
            Assert.That(E.RegionPatrolTargets(s, from).Intersect(Posts), Is.Empty, from);
            var w = E.DebugSpawnEnemy(s, PoolEnemy(Cfg, from), from);
            Assert.That(Call<List<string>>(E, "ReappearCandidates", s, w).Intersect(Posts), Is.Empty, from);
            for (uint k = 1; k <= 60; k++)
            {
                w.LocationId = from;
                w.HiddenUntil = 0;
                s.Rng.State = k * 2654435761u + 3;
                Call<object?>(E, "RelocateFled", s, w);
                Assert.That(Posts, Does.Not.Contain(w.LocationId), $"{from} 逃离");
            }
        }
    }

    /// <summary>
    /// Q566-A01／A02：安全资格按岗位绑定判断，不只靠 randomEncounterEnabled 等城市过滤——把 h02_square 人为改成野外人口区节点，
    /// 仍不刷新、不是巡游／重现目标、开发放敌拒绝、组卡概率 0。
    /// </summary>
    [Test]
    public void Q566_A01_Qualification_NotOnlyCityFlags()
    {
        var cfg = LoadConfig();
        var farm = cfg.Locations.Single(l => l.Id == "w01_farm");
        var sq = cfg.Locations.Single(l => l.Id == "h02_square");
        sq.RandomEncounterEnabled = true;
        sq.SafeExterior = false;
        sq.PopulationRegionId = farm.PopulationRegionId;
        sq.PopulationWeights = new Dictionary<string, int>(farm.PopulationWeights!);
        sq.PopulationLocalCap = 4;
        var region = cfg.WorldEnemies.Regions.Single(r => r.Id == farm.PopulationRegionId);
        region.Nodes.Add("h02_square");
        var e = new GameEngine(cfg);
        var s = e.NewGame(7);
        e.DebugLegacyStory(s);
        Assert.That(e.NpcSafePlace("h02_square"), Is.True);
        Assert.That(e.PopRegionOf("h02_square"), Is.Null);
        Assert.That(e.RegionSpawnCandidates(s, region), Does.Not.Contain("h02_square"));
        Assert.That(e.RegionPatrolTargets(s, "w01_farm"), Does.Not.Contain("h02_square"));
        Assert.That(e.DebugSpawnBlocked(s, "h02_square"), Is.Not.Null);
        var w = e.DebugSpawnEnemy(s, PoolEnemy(cfg, "w01_farm"), "w01_farm");
        Assert.That(Call<List<string>>(e, "ReappearCandidates", s, w), Does.Not.Contain("h02_square"));
        s.GameMinute = At(5, 10, 0);
        e.DebugSpawnEnemy(s, "shambler", "h02_square");
        Assert.That(e.EncounterChance(s, "h02_square", ActionType.Explore), Is.EqualTo(0));
    }

    // Q566-A03／A04（安全岗位服务完成的首次接续说明、可信旧待播兼容）由 B26aSubstituteTalkTests 的 Q235_02_*、Q235_04_*、Q243_01—Q243_05 覆盖：
    // 正常服务完成按原入口展示一次；旧待播按身份／原地／显示锁重验、最多一次（这些用例里的“岗位上开战”只用内核测试入口布场，不是正常玩家场景）。

    // ───────────── Q567 ─────────────

    /// <summary>Q567 选 A：单人队开练习盘预选「你」只是选人，仍须明确开始；内核资格按原规则（选人本身不耗时、不改队长）。</summary>
    [Test]
    public void Q567_SinglePartyPreselect_StillNeedsStart()
    {
        var s = New();
        Assert.That(s.Party.All(id => id == GameState.PlayerId), Is.True, "新局单人队");
        Assert.That(E.PracticeCandidates(s), Is.EqualTo(new[] { GameState.PlayerId }));
        var minute = s.GameMinute;
        var leader = s.SquadLeader;
        Assert.That(s.Main, Is.Null, "预选不开工");
        Assert.That(s.GameMinute, Is.EqualTo(minute));
        Assert.That(s.SquadLeader, Is.EqualTo(leader));
        var sess = File.ReadAllText(Path.Combine(Root(), "web", "Survival.Web", "Services", "GameSession.EnemyCardsV2.cs"));
        var open = sess.Substring(sess.IndexOf("public void OpenPractice()", StringComparison.Ordinal));
        open = open.Substring(0, open.IndexOf('}'));
        Assert.That(open, Does.Contain("PracticeActor = S.Party.All(id => id == GameState.PlayerId) ? GameState.PlayerId : null;"));
        Assert.That(open, Does.Not.Contain("StartPractice"), "打开盘不提交练习");
    }

    // ───────────── Q568 ─────────────

    /// <summary>Q568 选 A：搜集 7 级「危险辨识」正式说明全文「危险搜集划伤概率×0.8，不影响战斗」，技能 ID、等级、效果数值不变。</summary>
    [Test]
    public void Q568_HazardSense_Text()
    {
        var json = File.ReadAllText(Path.Combine(Root(), "data", "config.v0.6.json"));
        Assert.That(json, Does.Not.Contain("不影响战斗接敌"));
        Assert.That(json.Split("\"危险搜集划伤概率×0.8，不影响战斗\"").Length - 1, Is.EqualTo(2), "effectText 与 displayText");
        var i = json.IndexOf("\"id\": \"scavenging_07\"", StringComparison.Ordinal);
        var node = json.Substring(i, json.IndexOf("\"displayText\"", i, StringComparison.Ordinal) - i);
        Assert.That(node, Does.Contain("\"level\": 7").And.Contain("\"name\": \"危险辨识\"").And.Contain("\"key\": \"hazard.scavengeChance\"").And.Contain("\"value\": 0.8"));
    }
}
