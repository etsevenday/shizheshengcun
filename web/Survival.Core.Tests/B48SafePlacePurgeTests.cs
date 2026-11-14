using System.Reflection;
using Newtonsoft.Json.Linq;
using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// B48（需求 R237；02 Q636 策划答复；R05 文首「2026-11-14 Q636：直接清除安全地点残留敌人」验收 Q636-A01—A04；
/// 逐字稿文首「Q629—Q636」表安全区两句）：岗位安全地点上已残留的敌人实例与敌人卡读档时、接敌边界上直接清除，过期接敌请求给安全区拒绝。
/// </summary>
public class B48SafePlacePurgeTests : B41bTestBase
{
    /// <summary>R05 §Q566 当前 16 个岗位地点（验证基线）。</summary>
    private static readonly string[] Posts =
    {
        "h01_gate", "h02_square", "h03_workshop", "h04_infirmary", "h05_appraisal", "h06_shelter",
        "c02_church", "c04_alms", "c05_admonition", "c06_archive", "c07_sanctuary",
        "r04_reception", "r05_testing", "r06_archive", "r07_workshop", "r08_observation",
    };

    /// <summary>岗位旁边的独立危险检查哨（不是安全地点）。</summary>
    private const string Checkpoint = "m02_checkpoint";

    private string Purged(string loc) => GameEngine.SafeZonePurgedText(E.LocName(loc));

    private static List<string> LogTexts(GameState s) => s.Log.Select(l => l.Text).ToList();

    /// <summary>组卡（内核测试布场：直接用内部建组，绕开正式入口的安全资格过滤，模拟旧档残留）。</summary>
    private EnemyGroupCard Group(GameState s, string loc, params WorldEnemy[] members) =>
        (EnemyGroupCard)typeof(GameEngine).GetMethod("AddGroupCard", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(E, new object[] { s, loc, members.ToList(), "explore", s.GameMinute })!;

    private static GameState Reload(GameState s) => SaveSerializer.Deserialize(SaveSerializer.Serialize(s), Cfg);

    /// <summary>新局：第2日白天、冻结刷新巡游、清掉原有敌人，队伍在 at（默认营区广场），容量 3，对白已读。</summary>
    private GameState Post(string at = "h02_square")
    {
        var s = Field(3);
        E.DebugTeleport(s, at);
        ReadDialogues(s);
        return s;
    }

    /// <summary>去掉清理会动到的部分（敌人、敌踪卡、日志、敌人编号游标），用于比对“其余一概不变”。</summary>
    private static string Rest(GameState s)
    {
        var o = JObject.Parse(SaveSerializer.Serialize(s));
        foreach (var k in new[] { "WorldEnemies", "EnemyCards", "Log", "NextWorldEnemyId" }) o.Remove(k);
        return o.ToString();
    }

    // ───────────── Q636-A01 ─────────────

    /// <summary>
    /// Q636-A01：旧档在岗位地点布置单卡、组卡、躲避隐藏待重现与旧版逃走隐藏的敌人（含已到期时限、撤退保护、现身计数），读档后真实清除；
    /// 16 个岗位逐一清理；旁边危险检查哨的敌人不删；安全资格只看配置岗位绑定。
    /// </summary>
    [Test]
    public void Q636_A01_LoadClearsSingleGroupHidden_CheckpointKept()
    {
        var s = Post(Checkpoint);
        var keep = E.DebugSpawnEnemy(s, "shambler", Checkpoint);
        var single = E.DebugSpawnEnemy(s, "shambler", "h02_square");
        var g1 = E.DebugSpawnEnemy(s, "shambler", "c02_church");
        var g2 = E.DebugSpawnEnemy(s, "crawler", "c02_church");
        var group = Group(s, "c02_church", g1, g2);
        var evaded = E.DebugSpawnEnemy(s, "shambler", "r04_reception");
        evaded.EvadeHiddenUntil = s.GameMinute + 720;
        evaded.PopAccount = GameEngine.PopAccountLegacy;
        var fled = E.DebugSpawnEnemy(s, "shambler", "h05_appraisal");
        fled.HiddenUntil = s.GameMinute + 60;
        var singleKey = GameEngine.SingleCardKey(single.Id);
        var groupKey = GameEngine.GroupCardKey(group.Id);
        s.EnemyCards.Timers[singleKey] = new EnemyCardTimer { Left = 0, DueMinute = s.GameMinute };
        s.EnemyCards.Timers[groupKey] = new EnemyCardTimer { Left = 15, GuardUntil = s.GameMinute + 60 };
        s.EnemyCards.Appearances[GameEngine.SingleCardKey(evaded.Id)] = 1;
        var keepKey = GameEngine.SingleCardKey(keep.Id);
        s.EnemyCards.Timers[keepKey] = new EnemyCardTimer { Left = 20 };
        var logBefore = s.Log.Count;

        var t = Reload(s);
        var legal = s.WorldEnemies.Where(w => !Posts.Contains(w.LocationId)).Select(w => w.Id).ToList();
        Assert.That(legal, Does.Contain(keep.Id));
        Assert.That(t.WorldEnemies.Select(w => w.Id), Is.EquivalentTo(legal), "岗位残留全部清除，检查哨敌人（含其固定战）保留");
        Assert.That(t.EnemyCards.Groups, Is.Empty, "组卡随成员清空移除");
        Assert.That(t.EnemyCards.Timers.Keys, Is.EquivalentTo(new[] { keepKey }), "清掉的卡的时限与保护移除，检查哨卡时限原样");
        Assert.That(t.EnemyCards.Timers[keepKey].Left, Is.EqualTo(20), "不重置合法卡的计时");
        Assert.That(t.EnemyCards.Appearances, Is.Empty, "清掉的卡的现身计数移除");
        var added = t.Log.Skip(logBefore).Select(l => l.Text).ToList();
        Assert.That(added, Is.EqualTo(new[] { Purged("h02_square"), Purged("c02_church"), Purged("r04_reception"), Purged("h05_appraisal") }),
            "每处安全地点一条，检查哨不写");
        Assert.That(added[0], Is.EqualTo("已清除安全区域“营区广场”中的残留敌人卡。"), "逐字稿原句，地点取原公开名称");

        // 16 个岗位逐一：放一只即清一只，每地一条
        var u = Post(Checkpoint);
        foreach (var p in Posts) E.DebugSpawnEnemy(u, "shambler", p);
        var v = Reload(u);
        Assert.That(v.WorldEnemies.Where(w => Posts.Contains(w.LocationId)), Is.Empty);
        Assert.That(LogTexts(v).Count(x => x.StartsWith("已清除安全区域")), Is.EqualTo(16));
        foreach (var p in Posts) Assert.That(LogTexts(v), Does.Contain(Purged(p)), p);
    }

    /// <summary>Q636-A01：队伍就在岗位，残留卡时限已到期——读档后不开战、无主动接敌，卡视图里没有敌踪。</summary>
    [Test]
    public void Q636_A01_DueResidue_NoApproachAfterLoad()
    {
        var s = Post();
        var w = E.DebugSpawnEnemy(s, "shambler", "h02_square");
        var key = GameEngine.SingleCardKey(w.Id);
        s.EnemyCards.Timers[key] = new EnemyCardTimer { Left = 0, DueMinute = s.GameMinute };
        var t = Reload(s);
        Assert.That(E.EnemyCardsHere(t), Is.Empty, "读档后呈现前已清");
        Assert.That(E.CheckEnemyApproach(t), Is.False);
        Assert.That(t.Battle, Is.Null);
        Assert.That(E.Advance(t).Status, Is.Not.EqualTo(SegmentStatus.Paused));
        Assert.That(t.Battle, Is.Null, "空闲推进也不开战");
    }

    /// <summary>Q636-A01：运行中残留（内核测试布场）在接敌边界先复核清理：到期卡不开战、写一条清理日志。</summary>
    [Test]
    public void Q636_A01_RuntimeResidue_ClearedAtApproachBoundary()
    {
        var s = Post();
        var w = E.DebugSpawnEnemy(s, "shambler", "h02_square");
        SetLeft(s, GameEngine.SingleCardKey(w.Id), 0);
        Assert.That(E.CheckEnemyApproach(s), Is.False);
        Assert.That(s.Battle, Is.Null);
        Assert.That(s.WorldEnemies, Is.Empty);
        Assert.That(LogTexts(s).Count(x => x == Purged("h02_square")), Is.EqualTo(1));
        Assert.That(E.CheckEnemyApproach(s), Is.False);
        Assert.That(LogTexts(s).Count(x => x == Purged("h02_square")), Is.EqualTo(1), "无残留不再写");
    }

    // ───────────── Q636-A02 ─────────────

    /// <summary>
    /// Q636-A02：组员同时被组卡与旧单卡时限键引用，只移除一次、人口账只释放一次；合法危险地点的组卡与成员保持；
    /// 同地物品、遗物、NPC 等不动；不迁回、不补生（敌人编号游标不动）。
    /// </summary>
    [Test]
    public void Q636_A02_DedupRelease_NoCollateral_NoRespawn()
    {
        var s = Post();
        var region = Cfg.WorldEnemies.Regions.First();
        var a = E.DebugSpawnEnemy(s, "shambler", "h02_square");
        var b = E.DebugSpawnEnemy(s, "shambler", "h02_square");
        a.PopAccount = GameEngine.PopAccountRegion + region.Id;   // 曾躲避隐藏、锁定原人口区账的实例
        var g = Group(s, "h02_square", a, b);
        s.EnemyCards.Timers[GameEngine.SingleCardKey(a.Id)] = new EnemyCardTimer { Left = 5 };   // 旧单卡键残留的第二处引用
        s.EnemyCards.Timers[GameEngine.GroupCardKey(g.Id)] = new EnemyCardTimer { Left = 0, DueMinute = s.GameMinute };
        var c1 = E.DebugSpawnEnemy(s, "shambler", Checkpoint);
        var c2 = E.DebugSpawnEnemy(s, "crawler", Checkpoint);
        var legit = Group(s, Checkpoint, c1, c2);
        E.DebugGive(s, "wood", 2, s.Ground);
        var regionBefore = E.RegionLiveCount(s, region);
        var legacyBefore = E.LiveCount(s);
        var countBefore = s.WorldEnemies.Count;
        var nextId = s.NextWorldEnemyId;
        var baseline = Rest(Reload(Clean(s)));

        var t = Reload(s);
        Assert.That(t.WorldEnemies.Count, Is.EqualTo(countBefore - 2), "两只岗位实例各移除一次");
        Assert.That(E.RegionLiveCount(t, region), Is.EqualTo(regionBefore - 1), "原人口区账只释放一次");
        Assert.That(E.LiveCount(t), Is.EqualTo(legacyBefore));
        Assert.That(t.NextWorldEnemyId, Is.EqualTo(nextId), "不补生、不复制");
        Assert.That(t.EnemyCards.Groups.Select(x => x.Id), Is.EqualTo(new[] { legit.Id }), "空组消失，合法组保留");
        Assert.That(t.EnemyCards.Groups[0].MemberIds, Is.EqualTo(new[] { c1.Id, c2.Id }));
        Assert.That(t.WorldEnemies.All(w => w.LocationId == Checkpoint && w.GroupId == legit.Id), Is.True, "合法实例原地不动");
        Assert.That(t.EnemyCards.Timers, Is.Empty, "两处引用的时限都清掉");
        Assert.That(Rest(t), Is.EqualTo(baseline), "物品、遗物、NPC、设施、任务等其余状态与没有残留时一致");
        Assert.That(LogTexts(t).Count(x => x == Purged("h02_square")), Is.EqualTo(1), "同地多实例只一条");
    }

    /// <summary>同一状态去掉岗位上的残留（对照组：没有残留时读档的结果）。</summary>
    private static GameState Clean(GameState s)
    {
        var c = SaveSerializer.Deserialize(SaveSerializer.Serialize(s));
        var gone = c.WorldEnemies.Where(w => w.LocationId != Checkpoint).Select(w => w.Id).ToHashSet();
        c.WorldEnemies.RemoveAll(w => gone.Contains(w.Id));
        c.EnemyCards.Groups.RemoveAll(g => g.LocationId != Checkpoint);
        c.EnemyCards.Timers.Clear();
        return c;
    }

    // ───────────── Q636-A03 ─────────────

    /// <summary>
    /// Q636-A03：清理不发经验、掉落、尸体、任务／通关奖励，不写战报、无恐慌与开战对白（除清理日志外状态与无残留时一致）；
    /// 无残留读档不写日志；有残留按地点一条；保存后再读幂等。
    /// </summary>
    [Test]
    public void Q636_A03_NoRewards_LogOncePerPlace_Idempotent()
    {
        var s = Post();
        var clean = Reload(s);
        Assert.That(LogTexts(clean), Is.EqualTo(LogTexts(s)), "无残留不写日志");

        E.DebugSpawnEnemy(s, "shambler", "h02_square");
        E.DebugSpawnEnemy(s, "feral_dog", "h02_square");
        E.DebugSpawnEnemy(s, "shambler", "h03_workshop");
        var t = Reload(s);
        Assert.That(Rest(t), Is.EqualTo(Rest(clean)), "经验、物品、尸体、精神、任务、对白、战斗记录都不变");
        Assert.That(t.Battle, Is.Null);
        Assert.That(t.Story.PendingDialogues, Is.EqualTo(clean.Story.PendingDialogues), "不弹对白");
        var added = t.Log.Skip(s.Log.Count).Select(l => l.Text).ToList();
        Assert.That(added, Is.EqualTo(new[] { Purged("h02_square"), Purged("h03_workshop") }), "只有按地点各一条清理日志");

        var again = Reload(t);
        Assert.That(LogTexts(again), Is.EqualTo(LogTexts(t)), "保存后再读不重复清理／记账");
        Assert.That(SaveSerializer.Serialize(again), Is.EqualTo(SaveSerializer.Serialize(t)));

        // 另一份尚未修复的旧快照独立修复，不受当前档影响
        var other = Reload(s);
        Assert.That(other.Log.Skip(s.Log.Count).Select(l => l.Text), Is.EqualTo(added));
    }

    // ───────────── Q636-A04 ─────────────

    /// <summary>
    /// Q636-A04：过期卡视图的主动接近／潜行接近／躲避与本地固定入口命中安全地点：原反馈位只给安全区句，0 时间、不开工、不开战，先清无效引用。
    /// 离开岗位后同一过期键沿原「这处敌踪已不在这里。」。
    /// </summary>
    [Test]
    public void Q636_A04_StaleRequests_SafeZoneRejection()
    {
        var s = Post();
        var w = E.DebugSpawnEnemy(s, "shambler", "h02_square");
        var key = GameEngine.SingleCardKey(w.Id);
        var minute = s.GameMinute;
        var stats = Newtonsoft.Json.JsonConvert.SerializeObject(s.Actors[GameState.PlayerId]);

        var r = E.ApproachCard(s, key, "req-1");
        Assert.That(r.Ok, Is.False);
        Assert.That(r.Reason, Is.EqualTo("这里是安全区域，不能接敌。"));
        Assert.That(s.Battle, Is.Null);
        Assert.That(s.WorldEnemies, Is.Empty, "先清无效引用");
        Assert.That(LogTexts(s).Count(x => x == Purged("h02_square")), Is.EqualTo(1));

        Assert.That(E.ApproachCard(s, key, "req-2").Reason, Is.EqualTo(GameEngine.SafeZoneNoEngageText), "卡已清后同一过期键仍是安全区句");
        Assert.That(E.StartStealth(s, key, GameEngine.SneakKind, "req-3").Reason, Is.EqualTo(GameEngine.SafeZoneNoEngageText));
        Assert.That(E.StartStealth(s, key, GameEngine.EvadeKind, "req-4").Reason, Is.EqualTo(GameEngine.SafeZoneNoEngageText));
        Assert.That(E.EngageFixed(s).Reason, Is.EqualTo(GameEngine.SafeZoneNoEngageText), "异常固定入口");
        Assert.That(s.Main, Is.Null, "潜行／躲避不开工");
        Assert.That(s.GameMinute, Is.EqualTo(minute), "0 时间");
        Assert.That(Newtonsoft.Json.JsonConvert.SerializeObject(s.Actors[GameState.PlayerId]), Is.EqualTo(stats), "不扣体力、精神等");
        Assert.That(s.Battle, Is.Null);
        Assert.That(LogTexts(s).Count(x => x == Purged("h02_square")), Is.EqualTo(1));

        E.DebugTeleport(s, Checkpoint);
        Assert.That(E.CanApproachCard(s, key), Is.EqualTo("这处敌踪已不在这里。"), "危险地点沿原失效句");
    }

    /// <summary>
    /// Q636-A04：已真实提交的在办战斗按原进度恢复（交战实例不动，不伪造胜负），同地未参战的残留读档即清；
    /// 撤退收束后幸存者在战后边界清掉，安全世界不留非法卡，不发奖励。
    /// </summary>
    [Test]
    public void Q636_A04_CommittedBattle_ResumesThenCleared()
    {
        var s = Post();
        var a = E.DebugSpawnEnemy(s, "shambler", "h02_square");
        var b = E.DebugSpawnEnemy(s, "shambler", "h02_square");
        Assert.That(E.DebugStartBattle(s, new List<int> { a.Id }), Is.True);
        var battleId = s.Battle!.BattleId;

        var t = Reload(s);
        Assert.That(t.Battle, Is.Not.Null, "在办战斗原样恢复");
        Assert.That(t.Battle!.BattleId, Is.EqualTo(battleId));
        Assert.That(t.WorldEnemies.Select(w => w.Id), Is.EqualTo(new[] { a.Id }), "交战实例保留，未参战的残留清除");
        Assert.That(LogTexts(t).Count(x => x == Purged("h02_square")), Is.EqualTo(1));

        RetreatOk(t);
        Assert.That(t.Battle, Is.Null);
        Assert.That(LogTexts(t).Any(x => x.StartsWith("撤退成功")), Is.True, "原场次按原规则收束");
        Assert.That(t.WorldEnemies.Where(w => w.LocationId == "h02_square"), Is.Empty, "战后世界不留非法卡");
        Assert.That(t.EnemyCards.Timers, Is.Empty, "撤退保护随卡一起清掉");
        Assert.That(E.EnemyCardsHere(t), Is.Empty);
    }
}
