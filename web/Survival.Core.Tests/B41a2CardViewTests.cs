using Newtonsoft.Json.Linq;
using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// B41a2 里程碑 M711：预览优先级、两种校验原因、无有效卡时的卡视图与过期请求（内核部分）
/// （R05「B41a 实装补充 Q454—Q457」Q455—Q457；逐字稿「B41a 实装补充 Q454—Q457 显示定稿」；验收 Q455-A01、Q456-A01、Q457-A01—A03 内核）。
/// 开盘后目标失效收盘、清目标详情等界面接线归 B41e。
/// </summary>
public class B41a2CardViewTests : B41aTestBase
{
    private const string Gone = "这处敌踪已不在这里。";
    private const string Zone = "只能处理当前地点、当前区域可见的敌踪。";

    private static string RngOf(GameState s) => JObject.Parse(SaveSerializer.Serialize(s))["Rng"]!.ToString();

    private GameState Field(int capacity, params string[] enemyIds)
    {
        var s = V2Field();
        UnlockCapacity(s, capacity);
        foreach (var id in enemyIds) E.DebugSpawnEnemy(s, id, "field");
        return s;
    }

    /// <summary>清理一次并让开工时锁定的随机遭遇判定值必中，形成一张随机组卡。</summary>
    private EnemyGroupCard FormGroup(GameState s)
    {
        s.Locations["field"].ClearCharges = Math.Max(1, s.Locations["field"].ClearCharges);
        Start(s, ActionRequest.Clear());
        s.Main!.EncounterRoll = 0;
        Assert.That(Run(s), Is.EqualTo(SegmentStatus.Completed));
        return s.EnemyCards.Groups.Single();
    }

    /// <summary>拒绝不留下任何后果：不开战、不扣费、不抽签、不生成敌人、不写日志、不排对白、不改组卡或关卡状态。</summary>
    private sealed class Snapshot
    {
        private readonly string _rng;
        private readonly int _log, _enemies, _dialogues, _sessions, _minute;
        private readonly string _cards, _population;

        public Snapshot(GameState s)
        {
            _rng = RngOf(s);
            _log = s.Log.Count;
            _enemies = s.WorldEnemies.Count;
            _dialogues = s.Story.PendingDialogues.Count;
            _sessions = s.Story.DialogueLog.Count;
            _minute = s.GameMinute;
            _cards = JObject.FromObject(s.EnemyCards).ToString();
            _population = JObject.FromObject(s.Population).ToString();
        }

        public void AssertUnchanged(GameState s)
        {
            Assert.That(s.Battle, Is.Null, "不开战");
            Assert.That(RngOf(s), Is.EqualTo(_rng), "不抽签");
            Assert.That(s.Log.Count, Is.EqualTo(_log), "不写日志");
            Assert.That(s.WorldEnemies.Count, Is.EqualTo(_enemies), "不生成敌人");
            Assert.That(s.Story.PendingDialogues.Count, Is.EqualTo(_dialogues), "不排对白");
            Assert.That(s.Story.DialogueLog.Count, Is.EqualTo(_sessions));
            Assert.That(s.GameMinute, Is.EqualTo(_minute), "不耗时");
            Assert.That(JObject.FromObject(s.EnemyCards).ToString(), Is.EqualTo(_cards), "不改组卡");
            Assert.That(JObject.FromObject(s.Population).ToString(), Is.EqualTo(_population), "不改关卡与挑战记录");
            Assert.That(s.WorldEnemies.Any(w => w.Engaged), Is.False, "不改选别的敌人");
        }
    }

    // ───────────── Q455 ─────────────

    [Test]
    public void Q455_A01_HistoricalGraceWinsOverExistingGroup_ThenGroupLine()
    {
        var s = Field(3, "shambler", "crawler", "runner");
        var g = FormGroup(s);
        var members = g.MemberIds.ToList();
        Assert.That(E.EncounterNote(s, "field", ActionType.Explore), Is.EqualTo("当前已有一组敌踪，本次探索或清理不会形成新组卡。"));

        // 旧档历史全地点保护仍有效且已有组卡：只写历史保护句，时长取原截止减当前时刻
        s.EncounterGraceUntil = s.GameMinute + 45;
        var grace = s.EncounterGraceUntil;
        foreach (var t in new[] { ActionType.Explore, ActionType.Clear })
            Assert.That(E.EncounterNote(s, "field", t), Is.EqualTo(GameEngine.LegacyGraceText));
        s.GameMinute += 20;
        Assert.That(E.EncounterNote(s, "field", ActionType.Clear), Is.EqualTo(GameEngine.LegacyGraceText));

        // 到期后转已有组卡句；预览不移除组卡、不刷新保护
        s.GameMinute = grace;
        foreach (var t in new[] { ActionType.Explore, ActionType.Clear })
            Assert.That(E.EncounterNote(s, "field", t), Is.EqualTo("当前已有一组敌踪，本次探索或清理不会形成新组卡。"));
        Assert.That(s.EncounterGraceUntil, Is.EqualTo(grace));
        Assert.That(s.EnemyCards.Groups.Single().MemberIds, Is.EqualTo(members));
    }

    // ───────────── Q456 ─────────────

    [Test]
    public void Q456_A01_GoneOrElsewhere_vs_LocalButWrongZone()
    {
        var s = Field(3, "shambler", "crawler", "runner", "worker");
        var g = FormGroup(s);
        var gKey = GameEngine.GroupCardKey(g.Id);
        var single = s.WorldEnemies.Single(w => w.GroupId == null);
        var sKey = GameEngine.SingleCardKey(single.Id);
        Assert.That(E.CanApproachCard(s, gKey), Is.Null);
        Assert.That(E.CanApproachCard(s, sKey), Is.Null);
        var snap = new Snapshot(s);

        // 卡在本地，玩家在安全屋屋内（区域不匹配）：当前地点区域句
        s.CurrentZone = GameEngine.SafehouseZone;
        Assert.That(E.CanApproachCard(s, gKey), Is.EqualTo(Zone));
        Assert.That(E.CanApproachCard(s, sKey), Is.EqualTo(Zone));
        Assert.That(E.ApproachCard(s, gKey, "q456-a").Reason, Is.EqualTo(Zone));
        Assert.That(E.EnemyCardsHere(s), Is.Empty, "屋内不出卡");

        // 卡在别的地点：即使玩家此时在屋内，也不当成本地跨区卡
        single.LocationId = "village";
        Assert.That(E.CanApproachCard(s, sKey), Is.EqualTo(Gone));
        Assert.That(E.ApproachCard(s, sKey, "q456-b").Reason, Is.EqualTo(Gone));
        s.CurrentZone = GameEngine.ExteriorZone;
        Assert.That(E.CanApproachCard(s, sKey), Is.EqualTo(Gone));
        Assert.That(E.EnemyCardsHere(s).Select(c => c.Key), Is.EqualTo(new[] { gKey }));
        single.LocationId = "field";

        // 组卡在别的地点
        g.LocationId = "village";
        Assert.That(E.CanApproachCard(s, gKey), Is.EqualTo(Gone));
        g.LocationId = "field";

        // 卡已失效
        Assert.That(E.CanApproachCard(s, "we:99999"), Is.EqualTo(Gone));
        Assert.That(E.CanApproachCard(s, "group:99"), Is.EqualTo(Gone));
        Assert.That(E.CanApproachCard(s, "bogus"), Is.EqualTo(Gone));

        // 其他状态沿原对应原因
        single.HiddenUntil = s.GameMinute + 60;
        Assert.That(E.CanApproachCard(s, sKey), Is.EqualTo("这处敌踪暂时不可见。"));
        single.HiddenUntil = null;

        snap.AssertUnchanged(s);
    }

    // ───────────── Q457 ─────────────

    [Test]
    public void Q457_A01_A02_RemovedSingleOrEmptiedGroup_NoCard_StaleRequestRejectedSafely()
    {
        var s = Field(3, "shambler", "crawler", "runner", "worker", "wolf");
        var g = FormGroup(s);
        var gKey = GameEngine.GroupCardKey(g.Id);
        var singles = s.WorldEnemies.Where(w => w.GroupId == null).ToList();
        var removed = singles[0];
        var rKey = GameEngine.SingleCardKey(removed.Id);
        var keep = GameEngine.SingleCardKey(singles[1].Id);
        Assert.That(E.EnemyCardsHere(s).Select(c => c.Key), Is.EquivalentTo(new[] { gKey, rKey, keep }));

        // 普通卡已移除：不出卡；过期请求不执行、不改选别的敌人
        s.WorldEnemies.Remove(removed);
        Assert.That(E.EnemyCardsHere(s).Select(c => c.Key), Is.EquivalentTo(new[] { gKey, keep }));
        var snap = new Snapshot(s);
        var r = E.ApproachCard(s, rKey, "stale-single");
        Assert.That(r.Ok, Is.False);
        Assert.That(r.Reason, Is.EqualTo(Gone), "内核兜底沿现有句");
        Assert.That(E.Approach(s, removed.Id).Ok, Is.False, "旧入口转调同样拒绝");
        snap.AssertUnchanged(s);

        // 组卡清空：不出卡；按组卡或按原组员提交都拒绝
        var memberIds = g.MemberIds.ToList();
        s.WorldEnemies.RemoveAll(w => memberIds.Contains(w.Id));
        Assert.That(E.EnemyCardsHere(s).Select(c => c.Key), Is.EqualTo(new[] { keep }));
        Assert.That(E.FindEnemyCard(s, gKey, out _), Is.Null);
        snap = new Snapshot(s);
        Assert.That(E.ApproachCard(s, gKey, "stale-group").Reason, Is.EqualTo(Gone));
        Assert.That(E.Approach(s, memberIds[0]).Ok, Is.False);
        snap.AssertUnchanged(s);

        // 仍有效的卡照常处理
        Assert.That(E.ApproachCard(s, keep).Ok, Is.True);
        Assert.That(s.Battle!.CardKey, Is.EqualTo(keep));
    }

    private GameState AtNode(string node, int capacity, uint seed)
    {
        var s = New(seed);
        UnlockCapacity(s, capacity);
        s.GameMinute = At(2, 9, 0);
        s.SpawnNextCheck = int.MaxValue;
        s.PatrolNextCheck = int.MaxValue;
        E.DebugMapTeleport(s, node);
        return s;
    }

    [Test]
    public void Q457_A01_A02_FixedBattleNotEstablishedOrWon_NoCard()
    {
        const string node = "m02_checkpoint";
        var key = GameEngine.FixedCardKey(node);

        // 固定战不成立（本代没有揭示记录）：不出卡，拒绝不补建名单或敌人
        var s = AtNode(node, 1, 3101);
        Assert.That(E.EnemyCardsHere(s).Any(c => c.Kind == "fixed"), Is.True);
        s.Population.Fixed.Remove(node);
        Assert.That(E.EnemyCardsHere(s).Any(c => c.Kind == "fixed"), Is.False);
        Assert.That(E.EnemyCardsHere(s).Any(c => c.Members.Any(w => w.FixedOf != null)), Is.False, "固定敌不改作单卡");
        var snap = new Snapshot(s);
        Assert.That(E.ApproachCard(s, key, "stale-fixed").Reason, Is.EqualTo(Gone));
        Assert.That(E.EngageFixed(s).Ok, Is.False);
        snap.AssertUnchanged(s);
        Assert.That(s.Population.Fixed.ContainsKey(node), Is.False);

        // 固定战已完成：不出卡；拒绝不清胜利事实
        var t = AtNode(node, 1, 3102);
        Assert.That(E.ApproachCard(t, key).Ok, Is.True);
        WinBattle(t);
        Assert.That(t.Progress.Flags, Does.Contain("checkpoint_obstruction_cleared"));
        Assert.That(E.EnemyCardsHere(t).Any(c => c.Kind == "fixed"), Is.False);
        snap = new Snapshot(t);
        Assert.That(E.ApproachCard(t, key, "stale-fixed-won").Reason, Is.EqualTo(Gone));
        snap.AssertUnchanged(t);
        Assert.That(t.Population.Fixed[node].Won, Is.True, "真实胜利结果保留");
        Assert.That(t.Progress.Flags, Does.Contain("checkpoint_obstruction_cleared"));
    }

    [Test]
    public void Q457_A01_A03_OptionalElite_CardOnlyWhileEstablishedAndUnfinished()
    {
        var cfg = Cfg.WorldEnemies.LegacyElites.Single(x => x.EnemyId == "z_guard");
        var key = GameEngine.EliteCardKey(cfg.Id);
        var s = New(5101);
        s.Progress.Flags.Add("hub_registered");
        s.GameMinute = At(3, 9, 0);
        s.SpawnNextCheck = int.MaxValue;
        s.PatrolNextCheck = int.MaxValue;
        E.DebugMapTeleport(s, cfg.NodeId);
        s.WorldEnemies.RemoveAll(w => w.FixedOf == null);

        // 告示尚未成立：没有卡；过期请求不创建挑战记录、不建敌人、不抽签
        Assert.That(E.EnemyCardsHere(s), Is.Empty);
        var snap = new Snapshot(s);
        Assert.That(E.ApproachCard(s, key, "stale-elite").Ok, Is.False);
        Assert.That(E.EngageElite(s, cfg.Id).Ok, Is.False);
        snap.AssertUnchanged(s);
        Assert.That(s.Population.Elites.ContainsKey(cfg.Id), Is.False, "不为拒绝补一条挑战记录");

        // 已成立未完成、尚未实例化：仍是合法卡
        E.DebugSetProgress(s, cfg.NodeId, 100);
        var card = E.EnemyCardsHere(s).Single();
        Assert.That(card.Key, Is.EqualTo(key));
        Assert.That(card.Members, Is.Empty, "未实例化不等于告示不存在");
        Assert.That(E.CanApproachCard(s, key), Is.Null);
        Assert.That(s.WorldEnemies.Any(w => w.FixedOf == key), Is.False, "看卡不创建敌人");

        // 别的地点看这张卡：卡在异地
        var away = SaveSerializer.Deserialize(SaveSerializer.Serialize(s), CfgV2);
        E.DebugMapTeleport(away, "field");
        Assert.That(E.CanApproachCard(away, key), Is.EqualTo(Gone));
        Assert.That(away.WorldEnemies.Any(w => w.FixedOf == key), Is.False);

        // 首次实际处理只创建一次敌人与一次奖励抽签
        var rng = RngOf(s);
        Assert.That(E.ApproachCard(s, key).Ok, Is.True);
        Assert.That(RngOf(s), Is.Not.EqualTo(rng), "首次处理才抽奖励");
        var st = s.Population.Elites[cfg.Id];
        var wid = st.WorldId;
        var reward = st.RewardWon;
        Assert.That(s.WorldEnemies.Count(w => w.FixedOf == key), Is.EqualTo(1));
        WinBattle(s);

        // 本局已完成：不出卡；过期请求不清历史记录与胜利结果
        Assert.That(E.EnemyCardsHere(s), Is.Empty);
        snap = new Snapshot(s);
        Assert.That(E.ApproachCard(s, key, "stale-elite-won").Ok, Is.False);
        Assert.That(E.EngageElite(s, cfg.Id).Ok, Is.False);
        snap.AssertUnchanged(s);
        Assert.That(s.Population.Elites[cfg.Id].Settled, Is.True);
        Assert.That(s.Population.Elites[cfg.Id].WorldId, Is.EqualTo(wid));
        Assert.That(s.Population.Elites[cfg.Id].RewardWon, Is.EqualTo(reward));
    }
}
