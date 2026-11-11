using System.Reflection;
using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// B41a 里程碑 5：新版敌踪的战后口径与测试入口（开关开；R05「Q416」保护期、「Q417」组卡存续与增援、「Q436」动物单卡；
/// 验收 Q417-A01 撤退同组／组员不增援、Q436-A01 狼鹿逃离与旧在途战斗）。
/// </summary>
public class B41aAftermathTests : B41aTestBase
{
    private GameState Field(int capacity, params string[] enemyIds)
    {
        var s = V2Field();
        UnlockCapacity(s, capacity);
        foreach (var id in enemyIds) E.DebugSpawnEnemy(s, id, "field");
        return s;
    }

    [Test]
    public void Q416_NoLocationWideGraceAfterVictoryOrRetreat_HistoricalValueUntouched()
    {
        var s = Field(3, "shambler", "crawler");
        var a = GameEngine.SingleCardKey(s.WorldEnemies[0].Id);
        var bKey = GameEngine.SingleCardKey(s.WorldEnemies[1].Id);
        Assert.That(E.ApproachCard(s, a).Ok, Is.True);
        WinBattle(s);
        Assert.That(s.EncounterGraceUntil, Is.EqualTo(0), "胜利不新增全地点保护");
        Assert.That(E.EncounterChance(s, "field", ActionType.Clear), Is.GreaterThan(0));

        Assert.That(E.ApproachCard(s, bKey).Ok, Is.True);
        RetreatOk(s);
        Assert.That(s.EncounterGraceUntil, Is.EqualTo(0), "撤退也不新增全地点保护");

        // 旧档历史保护：原值不动、不延期
        var h = Field(3, "shambler");
        h.EncounterGraceUntil = h.GameMinute + 40;
        var until = h.EncounterGraceUntil;
        Assert.That(E.ApproachCard(h, GameEngine.SingleCardKey(h.WorldEnemies[0].Id)).Ok, Is.True, "保护期内玩家仍可主动处理");
        WinBattle(h);
        Assert.That(h.EncounterGraceUntil, Is.EqualTo(until));
    }

    [Test]
    public void Q417_A01_RetreatKeepsSameGroup_HpPersists_AllDeadRemovesCard()
    {
        var s = Field(3, "shambler", "crawler", "runner");
        var ids = s.WorldEnemies.Select(w => w.Id).ToList();
        Assert.That(E.DebugForceEncounter(s), Is.True);
        var g = s.EnemyCards.Groups.Single();
        var key = GameEngine.GroupCardKey(g.Id);
        Assert.That(s.Log[^1].Text, Is.EqualTo("（调试）发现敌踪：游荡尸等3只。"));
        Assert.That(E.DebugForceEncounter(s), Is.False, "已有可见组时不再形成新组");

        Assert.That(E.ApproachCard(s, key).Ok, Is.True);
        s.Battle!.Enemies[1].Hp -= 0.5;
        var hp = s.Battle.Enemies[1].Hp;
        KillInBattle(s, 1);
        Assert.That(s.EnemyCards.Groups.Single().MemberIds, Is.EqualTo(new[] { ids[1], ids[2] }), "成员死亡从名单删去");
        RetreatOk(s);

        // 撤退后幸存成员仍属原组（同一张卡）
        Assert.That(s.EnemyCards.Groups.Single().Id, Is.EqualTo(g.Id));
        Assert.That(s.WorldEnemies.Select(w => w.GroupId), Has.All.EqualTo(g.Id));
        Assert.That(E.FindWorldEnemy(s, ids[1])!.Hp, Is.EqualTo(hp).Within(1e-9), "保留 HP");
        Assert.That(s.WorldEnemies.Select(w => w.Engaged), Has.All.False);
        if (s.CurrentLocation != "field") E.DebugTeleport(s, "field");
        var card = E.EnemyCardsHere(s).Single();
        Assert.That(card.Key, Is.EqualTo(key));
        Assert.That(card.Title, Is.EqualTo("爬行尸等2只"));

        // 存读档后仍是同一组
        var t = SaveSerializer.Deserialize(SaveSerializer.Serialize(s), CfgV2);
        Assert.That(t.EnemyCards.Groups.Single().MemberIds, Is.EqualTo(new[] { ids[1], ids[2] }));
        Assert.That(E.FindEnemyCard(t, key, out _)!.Title, Is.EqualTo("爬行尸等2只"));

        // 再次接近：同一批实例；全灭后组卡移除
        Assert.That(E.ApproachCard(s, key).Ok, Is.True);
        Assert.That(s.Battle!.Enemies.Select(e => e.WorldId), Is.EqualTo(new[] { ids[1], ids[2] }));
        Assert.That(s.Battle.Enemies[0].Hp, Is.EqualTo(hp).Within(1e-9));
        WinBattle(s);
        Assert.That(s.EnemyCards.Groups, Is.Empty, "成员全灭后组卡移除");
        Assert.That(E.CanApproachCard(s, key), Is.EqualTo("这处敌踪已不在这里。"));
        Assert.That(E.EnemyCardsHere(s), Is.Empty);
    }

    private void ToNextRound(GameState s)
    {
        var r = s.Battle!.Round;
        for (var i = 0; i < 200 && s.Battle != null && s.Battle.Round == r && !s.Progress.Dead; i++) Step(s);
        Assert.That(s.Battle, Is.Not.Null);
    }

    [Test]
    public void Q417_A01_GroupMembersAreNotCalledAsReinforcements()
    {
        // C2：组卡锁两只（游荡尸、爬行尸），狂奔尸留单卡
        var s = Field(2, "shambler", "crawler", "runner");
        var ids = s.WorldEnemies.Select(w => w.Id).ToList();
        Assert.That(E.DebugForceEncounter(s), Is.True);
        Assert.That(s.EnemyCards.Groups.Single().MemberIds, Is.EqualTo(new[] { ids[0], ids[1] }));
        s.Stats.Health = s.Stats.HealthMax;

        Assert.That(E.ApproachCard(s, GameEngine.SingleCardKey(ids[2])).Ok, Is.True);
        s.Battle!.ReinforcementRequested = true;
        ToNextRound(s);
        Assert.That(s.Battle!.ReinforcementUsed, Is.True);
        Assert.That(s.Battle.Enemies.Select(e => e.WorldId), Is.EqualTo(new[] { ids[2] }), "同地只有组成员：不被噪声征用");
        Assert.That(s.Battle.Log.Any(l => l.Contains("附近没有被吸引的丧尸。")), Is.True);
        Assert.That(E.FindWorldEnemy(s, ids[0])!.Engaged, Is.False);

        // 对照：同地另有独立单卡丧尸时照常被引来
        var c = Field(2, "shambler", "crawler", "runner");
        var cid = c.WorldEnemies.Select(w => w.Id).ToList();
        Assert.That(E.DebugForceEncounter(c), Is.True);
        var extra = E.DebugSpawnEnemy(c, "worker", "field");
        Assert.That(E.ApproachCard(c, GameEngine.SingleCardKey(cid[2])).Ok, Is.True);
        c.Battle!.ReinforcementRequested = true;
        ToNextRound(c);
        Assert.That(c.Battle!.Enemies.Select(e => e.WorldId), Is.EqualTo(new[] { cid[2], extra.Id }));
        Assert.That(extra.GroupId, Is.Null);
    }

    [Test]
    public void Q436_A01_FleeingWolfStaysSingleCard_RelocatesOrHides60()
    {
        var s = Field(3, "wolf", "shambler", "crawler");
        var wolfId = s.WorldEnemies[0].Id;
        Assert.That(E.DebugForceEncounter(s), Is.True);
        Assert.That(E.FindWorldEnemy(s, wolfId)!.GroupId, Is.Null, "动物不进随机组卡");
        Assert.That(E.EnemyCardsHere(s).Select(c => c.Kind), Is.EqualTo(new[] { "group", "single" }));

        Assert.That(E.ApproachCard(s, GameEngine.SingleCardKey(wolfId)).Ok, Is.True);
        var be = s.Battle!.Enemies.Single();
        be.Hp = 1;
        be.Fled = true;
        be.Slot = "";
        var minute = s.GameMinute;
        typeof(GameEngine).GetMethod("CheckVictory", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(E, new object[] { s });
        Assert.That(s.Battle, Is.Null);
        var wolf = E.FindWorldEnemy(s, wolfId)!;
        Assert.That(wolf.GroupId, Is.Null);
        Assert.That(wolf.Hp, Is.EqualTo(1).Within(1e-9), "保留 HP");
        Assert.That(wolf.LocationId != "field" || wolf.HiddenUntil == minute + 5 + Cfg.WorldEnemies.FleeWithoutValidNeighborHiddenMinutes
                    || wolf.HiddenUntil == minute + Cfg.WorldEnemies.FleeWithoutValidNeighborHiddenMinutes,
            Is.True, "沿原规则：迁到合法相邻地点，无处可去原地隐藏 60 分钟");
        Assert.That(Cfg.WorldEnemies.FleeWithoutValidNeighborHiddenMinutes, Is.EqualTo(60));
        Assert.That(s.EnemyCards.Groups.Single().MemberIds, Has.Count.EqualTo(2), "非动物的组不受影响");
    }

    [Test]
    public void Q436_A01_LegacyBattleInProgress_FinishesOnOldSnapshot_SurvivorsAreSingleCards()
    {
        // 旧版流程里已开打的一场（灰狼 + 游荡尸同场），升级后在新版下打完
        var old = LegacyField();
        UnlockCapacity(old, 3);
        Off.DebugSpawnEnemy(old, "wolf", "field");
        Off.DebugSpawnEnemy(old, "shambler", "field");
        old.Encounter = new PendingEncounter { LocationId = "field", EnemyIds = old.WorldEnemies.Select(w => w.Id).ToList(), Source = "explore", Minute = old.GameMinute };
        Assert.That(Off.Engage(old).Ok, Is.True);
        var s = SaveSerializer.Deserialize(AsSchema45(old), CfgV2);
        Assert.That(E.CardsV2(s), Is.True);
        Assert.That(s.Battle!.Enemies.Select(e => e.EnemyId), Is.EqualTo(new[] { "wolf", "shambler" }), "不拆当前快照");
        Assert.That(s.Battle.CardKey, Is.Null);

        RetreatOk(s);
        Assert.That(s.EnemyCards.Groups, Is.Empty);
        Assert.That(s.WorldEnemies, Has.Count.EqualTo(2));
        Assert.That(s.WorldEnemies.Select(w => w.GroupId), Has.All.Null, "结束后幸存者各自是单卡");
        Assert.That(s.EncounterGraceUntil, Is.EqualTo(old.EncounterGraceUntil), "不新增全地点保护");
    }

    [Test]
    public void DebugStartBattle_WorksInBothFlows_NoCardKey()
    {
        var s = Field(3, "shambler", "crawler");
        Assert.That(E.DebugStartBattle(s, s.WorldEnemies.Select(w => w.Id), GameEngine.FearEncounter), Is.True);
        Assert.That(s.Battle!.Enemies, Has.Count.EqualTo(2));
        Assert.That(s.Battle.Context, Is.EqualTo(GameEngine.FearEncounter));
        Assert.That(s.Battle.CardKey, Is.Null);
        Assert.That(E.DebugStartBattle(s, s.WorldEnemies.Select(w => w.Id)), Is.False, "已在战斗");

        var o = LegacyField();
        var w = Off.DebugSpawnEnemy(o, "shambler", "field");
        Assert.That(Off.DebugStartBattle(o, new[] { w.Id }, GameEngine.FearApproach), Is.True);
        Assert.That(o.Battle!.Context, Is.EqualTo(GameEngine.FearApproach));
        Assert.That(o.Battle.Surprise, Is.False);
    }

    [Test]
    public void SwitchOff_GraceStillGrantedAfterBattle()
    {
        var o = LegacyField();
        var w = Off.DebugSpawnEnemy(o, "shambler", "field");
        Assert.That(Off.Approach(o, w.Id).Ok, Is.True);
        E = Off;   // WinBattle 用 E
        WinBattle(o);
        Assert.That(o.EncounterGraceUntil, Is.EqualTo(o.GameMinute + Cfg.Encounters.PostExitRandomEncounterGraceMinutes), "开关关：旧版战后全地点保护原样");
    }
}
