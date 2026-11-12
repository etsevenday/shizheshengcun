using Newtonsoft.Json.Linq;
using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// B41a 里程碑 2：存档 46 与旧档采纳新版敌踪卡模型（R05「Q425 旧档与持久化」、Q436、Q437；验收 Q425-A01 前半、Q436-A01 旧名单部分、Q437-A01）。
/// 开关关着：45→46 只补空结构；开关开着：读档时采纳一次（旧绕行取消、旧待处理名单转组卡且动物还原单卡），幂等、不抽随机。
/// </summary>
public class B41aMigrationTests : B41aTestBase
{
    private static string RngOf(GameState s) => JObject.Parse(SaveSerializer.Serialize(s))["Rng"]!.ToString();

    private GameState WithEncounter(int capacity, params string[] enemyIds)
    {
        var s = LegacyField();
        UnlockCapacity(s, capacity);
        foreach (var id in enemyIds) Off.DebugSpawnEnemy(s, id, "field");
        s.Encounter = new PendingEncounter
        {
            LocationId = "field",
            EnemyIds = s.WorldEnemies.Take(Cfg.Encounters.GroupMax).Select(w => w.Id).ToList(),
            Source = "explore",
            Minute = s.GameMinute,
        };
        return s;
    }

    [Test]
    public void NewGame_ModelVersionFollowsSwitch()
    {
        Assert.That(GameState.CurrentSchema, Is.EqualTo("48"));
        Assert.That(Off.NewGame(1).EnemyCards.ModelVersion, Is.EqualTo(0));
        Assert.That(Off.CardsV2(Off.NewGame(1)), Is.False);
        var s = E.NewGame(1);
        Assert.That(s.EnemyCards.ModelVersion, Is.EqualTo(GameEngine.EnemyCardsModelV2));
        Assert.That(E.CardsV2(s), Is.True);
        // 开关开着但存档还没采纳：仍按旧版
        s.EnemyCards.ModelVersion = 0;
        Assert.That(E.CardsV2(s), Is.False);
    }

    [Test]
    public void SwitchOff_45To46_OnlyAddsStructure()
    {
        var s = WithEncounter(3, "shambler", "wolf", "crawler");
        var rng = RngOf(s);
        var log = s.Log.Count;
        var t = SaveSerializer.Deserialize(AsSchema45(s), CfgOff);
        Assert.That(t.SchemaVersion, Is.EqualTo("48"));
        Assert.That(t.EnemyCards.ModelVersion, Is.EqualTo(0));
        Assert.That(t.EnemyCards.Groups, Is.Empty);
        Assert.That(t.Encounter, Is.Not.Null, "开关关着：旧待处理名单原样");
        Assert.That(t.Encounter!.EnemyIds, Is.EqualTo(s.Encounter!.EnemyIds));
        Assert.That(t.WorldEnemies.Select(w => w.GroupId), Has.All.Null);
        Assert.That(t.Log.Count, Is.EqualTo(log), "静默");
        Assert.That(RngOf(t), Is.EqualTo(rng), "不抽随机");

        // 在办绕行也原样
        Assert.That(Off.Start(s, ActionRequest.Bypass()).Ok, Is.True);
        Off.Advance(s);
        var u = SaveSerializer.Deserialize(AsSchema45(s), CfgOff);
        Assert.That(u.Main?.Type, Is.EqualTo(ActionType.Bypass));
        Assert.That(u.Main!.ElapsedMinutes, Is.EqualTo(5));

        // 幂等：再存再读不变
        var once = SaveSerializer.Serialize(u);
        Assert.That(SaveSerializer.Serialize(SaveSerializer.Deserialize(once, CfgOff)), Is.EqualTo(once));
    }

    [Test]
    public void Q425_PendingEncounter_BecomesGroupCard_AnimalsStaySingle_CapacityCut()
    {
        // 名单按最早出现取前 3：游荡尸、灰狼、爬行尸；狂奔尸不在名单里。容量 C2
        var s = WithEncounter(2, "shambler", "wolf", "crawler", "runner");
        var ids = s.WorldEnemies.Select(w => w.Id).ToList();
        var hp = s.WorldEnemies.ToDictionary(w => w.Id, w => w.Hp);
        var rng = RngOf(s);
        var log = s.Log.Count;

        var t = SaveSerializer.Deserialize(AsSchema45(s), CfgV2);
        Assert.That(t.SchemaVersion, Is.EqualTo("48"));
        Assert.That(E.CardsV2(t), Is.True);
        Assert.That(t.Encounter, Is.Null, "旧待处理名单转成组卡后清掉");
        Assert.That(t.EnemyCards.Groups, Has.Count.EqualTo(1));
        var g = t.EnemyCards.Groups[0];
        Assert.That(g.LocationId, Is.EqualTo("field"));
        Assert.That(g.MemberIds, Is.EqualTo(new[] { ids[0], ids[2] }), "动物还原单卡，其余按原顺序和容量成组");
        Assert.That(g.Source, Is.EqualTo("explore"));
        Assert.That(g.Capacity, Is.EqualTo(2));
        Assert.That(E.FindWorldEnemy(t, ids[0])!.GroupId, Is.EqualTo(g.Id));
        Assert.That(E.FindWorldEnemy(t, ids[2])!.GroupId, Is.EqualTo(g.Id));
        Assert.That(E.FindWorldEnemy(t, ids[1])!.GroupId, Is.Null, "灰狼是单卡");
        Assert.That(E.FindWorldEnemy(t, ids[3])!.GroupId, Is.Null, "不在原名单里的保持单卡");
        Assert.That(t.WorldEnemies.ToDictionary(w => w.Id, w => w.Hp), Is.EqualTo(hp), "HP 不变、不制造敌人");
        Assert.That(E.GroupCardTitle(t, g), Is.EqualTo("游荡尸等2只"));
        Assert.That(t.Log.Count, Is.EqualTo(log), "静默");
        Assert.That(RngOf(t), Is.EqualTo(rng), "全程不抽随机");

        // 幂等：再存再读，组卡与编号不变
        var once = SaveSerializer.Serialize(t);
        Assert.That(SaveSerializer.Serialize(SaveSerializer.Deserialize(once, CfgV2)), Is.EqualTo(once));
    }

    [Test]
    public void Q425_PendingEncounter_OverCapacityStaysSingle()
    {
        var s = WithEncounter(1, "shambler", "crawler", "runner");
        var ids = s.WorldEnemies.Select(w => w.Id).ToList();
        var t = SaveSerializer.Deserialize(AsSchema45(s), CfgV2);
        Assert.That(t.EnemyCards.Groups.Single().MemberIds, Is.EqualTo(new[] { ids[0] }), "C1：只锁排在最前的一只");
        Assert.That(E.GroupCardTitle(t, t.EnemyCards.Groups[0]), Is.EqualTo("游荡尸等1只"));
        Assert.That(t.WorldEnemies.Where(w => w.GroupId == null).Select(w => w.Id), Is.EqualTo(new[] { ids[1], ids[2] }), "超容量者保持单卡");
    }

    [Test]
    public void Q436_PendingEncounter_AllAnimals_NoEmptyGroup()
    {
        var s = WithEncounter(3, "wolf", "feral_dog", "boar");
        var t = SaveSerializer.Deserialize(AsSchema45(s), CfgV2);
        Assert.That(t.Encounter, Is.Null);
        Assert.That(t.EnemyCards.Groups, Is.Empty, "筛完无成员不留空组");
        Assert.That(t.WorldEnemies.Select(w => w.GroupId), Has.All.Null);
        Assert.That(t.WorldEnemies, Has.Count.EqualTo(3));
    }

    [TestCase(false, true)]
    [TestCase(true, true)]
    [TestCase(false, false)]
    public void Q437_LegacyBypass_Cancelled_NoGroup_Idempotent(bool paused, bool enemiesRemain)
    {
        var s = WithEncounter(3, "shambler", "crawler");
        s.WorldEnemies[0].Hp -= 1;
        Assert.That(Off.Start(s, ActionRequest.Bypass()).Ok, Is.True);
        Assert.That(s.Encounter, Is.Null, "旧版绕行开工就清掉了名单");
        Off.Advance(s);
        if (paused) { s.Main!.Status = ActionStatus.Paused; s.Main.PauseReason = PauseReason.StatAlert; }
        if (!enemiesRemain) s.WorldEnemies.Clear();
        var minute = s.GameMinute;
        var energy = s.Stats.Energy;
        var hp = s.WorldEnemies.ToDictionary(w => w.Id, w => w.Hp);
        var grace = s.EncounterGraceUntil;
        var rng = RngOf(s);
        var log = s.Log.Count;

        var t = SaveSerializer.Deserialize(AsSchema45(s), CfgV2);
        Assert.That(t.Main, Is.Null, "在办或暂停的绕行取消");
        Assert.That(t.Sub, Is.Null);
        Assert.That(t.GameMinute, Is.EqualTo(minute), "已耗时间保留");
        Assert.That(t.Stats.Energy, Is.EqualTo(energy), "身体变化保留");
        Assert.That(t.EncounterGraceUntil, Is.EqualTo(grace), "不发绕行完成保护");
        Assert.That(t.EnemyCards.Groups, Is.Empty, "不重取名单、不建组卡");
        Assert.That(t.WorldEnemies.Select(w => w.GroupId), Has.All.Null, "当地敌人保持单卡");
        Assert.That(t.WorldEnemies.ToDictionary(w => w.Id, w => w.Hp), Is.EqualTo(hp), "HP 不重置");
        Assert.That(t.Log.Count, Is.EqualTo(log + 1));
        Assert.That(t.Log[^1].Text, Is.EqualTo("原绕行行动已取消。"), "当地有无敌人都是同一句");
        Assert.That(RngOf(t), Is.EqualTo(rng), "不抽随机");
        Assert.That(E.IsBusy(t), Is.False);

        // 重复迁移幂等：不再写日志、不重复返还
        var once = SaveSerializer.Serialize(t);
        var again = SaveSerializer.Deserialize(once, CfgV2);
        Assert.That(SaveSerializer.Serialize(again), Is.EqualTo(once));
        E.AdoptEnemyCards(again);
        Assert.That(again.Log.Count, Is.EqualTo(log + 1));
    }

    [Test]
    public void Q416_HistoricalGrace_KeptToOriginalDeadline()
    {
        var s = LegacyField();
        s.EncounterGraceUntil = s.GameMinute + 35;
        var t = SaveSerializer.Deserialize(AsSchema45(s), CfgV2);
        Assert.That(t.EncounterGraceUntil, Is.EqualTo(s.GameMinute + 35), "保留到原截止，不延期");
    }

    [Test]
    public void Q425_BattleInProgress_KeepsOldSnapshot()
    {
        var s = WithEncounter(3, "shambler", "wolf");
        Assert.That(Off.Engage(s).Ok, Is.True);
        var t = SaveSerializer.Deserialize(AsSchema45(s), CfgV2);
        Assert.That(t.Battle, Is.Not.Null, "原已开始的战斗按旧快照完成");
        Assert.That(t.Battle!.CardKey, Is.Null);
        Assert.That(t.Battle.Enemies.Select(e => e.EnemyId), Is.EqualTo(new[] { "shambler", "wolf" }), "不拆当前快照");
        Assert.That(t.EnemyCards.Groups, Is.Empty);
        Assert.That(t.WorldEnemies.Select(w => w.Engaged), Has.All.True);
        Assert.That(t.EnemyCards.ModelVersion, Is.EqualTo(GameEngine.EnemyCardsModelV2));
    }

    [Test]
    public void SwitchTurnedOnLater_CurrentSchemaSaveAdoptsOnLoad()
    {
        // 开关关着时已经升到 46 的档（线上实际情况）：B41f 打开开关后的第一次读档才采纳
        var s = WithEncounter(3, "shambler", "crawler");
        var v46 = SaveSerializer.Serialize(SaveSerializer.Deserialize(AsSchema45(s), CfgOff));
        var t = SaveSerializer.Deserialize(v46, CfgV2);
        Assert.That(t.Encounter, Is.Null);
        Assert.That(t.EnemyCards.ModelVersion, Is.EqualTo(GameEngine.EnemyCardsModelV2));
        Assert.That(t.EnemyCards.Groups.Single().MemberIds, Has.Count.EqualTo(2));
    }
}
