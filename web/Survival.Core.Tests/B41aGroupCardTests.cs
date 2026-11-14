using System.Reflection;
using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// B41a 里程碑 3：随机组卡的形成与存续（R05「Q417–Q419」「B41a Q436 动物单卡」，逐字稿预览与日志句；
/// 验收 Q417-A01、Q427-A01 标题、Q436-A01 新组筛选、Q438-A01 预览）。
/// </summary>
public class B41aGroupCardTests : B41aTestBase
{
    private GameState Field(int capacity, params string[] enemyIds)
    {
        var s = V2Field();
        UnlockCapacity(s, capacity);
        foreach (var id in enemyIds) E.DebugSpawnEnemy(s, id, "field");
        return s;
    }

    /// <summary>做完一次清理并让开工时锁定的判定值必中（roll=0）或必不中（roll=1）。</summary>
    private void Clear(GameState s, double roll = 0)
    {
        s.Locations["field"].ClearCharges = Math.Max(1, s.Locations["field"].ClearCharges);
        Start(s, ActionRequest.Clear());
        s.Main!.EncounterRoll = roll;
        Assert.That(Run(s), Is.EqualTo(SegmentStatus.Completed));
    }

    [Test]
    public void Q417_A01_GroupFormsAfterResult_LocksByCapacity_DoesNotBlock()
    {
        var s = Field(3, "shambler", "crawler", "runner", "worker");
        var ids = s.WorldEnemies.Select(w => w.Id).ToList();
        Assert.That(E.EncounterChance(s, "field", ActionType.Clear), Is.GreaterThan(0));
        Clear(s);

        Assert.That(s.Encounter, Is.Null, "新版不产生待处理敌踪");
        var g = s.EnemyCards.Groups.Single();
        Assert.That(g.MemberIds, Is.EqualTo(ids.Take(3)), "按最早出现取至多 groupMax 只，再按容量锁名单");
        Assert.That(g.Capacity, Is.EqualTo(3));
        Assert.That(g.Source, Is.EqualTo("clear"));
        Assert.That(E.FindWorldEnemy(s, ids[3])!.GroupId, Is.Null, "超额保留独立单卡");
        Assert.That(s.Log.Last(l => l.Kind == GameEventKind.Encounter.ToString()).Text, Is.EqualTo("发现敌踪：游荡尸等3只。"));
        Assert.That(s.Log.Any(l => l.Text.Contains("清理")), Is.True, "原结果先提交");

        // 组卡出现后不阻断其他操作
        Assert.That(E.InCombatFlow(s), Is.False);
        Assert.That(E.IsBusy(s), Is.False);
        Assert.That(E.ManualSaveBlocked(s), Is.EqualTo(ManualSaveBlock.None), "可按其他条件保存");
        Assert.That(E.FormationEditBlocked(s), Is.Null, "可编队");
        Assert.That(E.CanStart(s, ActionRequest.Rest(30)), Is.Null, "可开始别的行动");
        Assert.That(E.CanStart(s, new ActionRequest { Type = ActionType.Bypass }), Is.Not.Null, "绕行入口下线");
    }

    [TestCase(1, 1)]
    [TestCase(2, 2)]
    [TestCase(4, 3)]
    public void Q417_A01_CapacityAtFormation(int capacity, int expected)
    {
        var s = Field(capacity, "shambler", "crawler", "runner", "worker");
        var ids = s.WorldEnemies.Select(w => w.Id).ToList();
        Clear(s);
        var g = s.EnemyCards.Groups.Single();
        Assert.That(g.MemberIds, Is.EqualTo(ids.Take(expected)));
        Assert.That(E.GroupCardTitle(s, g), Is.EqualTo($"游荡尸等{expected}只"), "只剩/只有 1 只也写“等1只”");
        Assert.That(s.WorldEnemies.Count(w => w.GroupId == null), Is.EqualTo(4 - expected));
        // 成组后扩容不重编
        UnlockCapacity(s, 4);
        Assert.That(E.GroupMembers(s, g).Select(w => w.Id), Is.EqualTo(ids.Take(expected)));
    }

    [Test]
    public void Q436_A01_AnimalsNeverJoinGroups()
    {
        var s = Field(3, "wolf", "shambler", "feral_dog", "crawler", "boar");
        var ids = s.WorldEnemies.Select(w => w.Id).ToList();
        Clear(s);
        var g = s.EnemyCards.Groups.Single();
        Assert.That(g.MemberIds, Is.EqualTo(new[] { ids[1], ids[3] }), "先排除动物，再按原规则取");
        foreach (var w in s.WorldEnemies.Where(w => E.IsAnimal(w.EnemyId))) Assert.That(w.GroupId, Is.Null, w.EnemyId);
        Assert.That(Cfg.Enemies.Where(e => e.Family == "animal").Select(e => e.Id),
            Is.EquivalentTo(new[] { "feral_dog", "wolf", "boar", "stag", "wild_dog_leader", "feral_boar" }), "当前 6 种动物，按配置 family 判断");
    }

    [Test]
    public void Q436_A01_OnlyAnimals_NoGroup_NoRefill()
    {
        var s = Field(3, "wolf", "feral_dog", "boar");
        Assert.That(E.EncounterChance(s, "field", ActionType.Clear), Is.GreaterThan(0), "风险基数沿原规则（本地敌对含动物）");
        var log = s.Log.Count(l => l.Kind == GameEventKind.Encounter.ToString());
        Clear(s);
        Assert.That(s.EnemyCards.Groups, Is.Empty, "没有非动物合法候选就不形成组卡");
        Assert.That(s.Log.Count(l => l.Kind == GameEventKind.Encounter.ToString()), Is.EqualTo(log));
        Assert.That(s.WorldEnemies, Has.Count.EqualTo(3), "不制造敌人");
    }

    [Test]
    public void Q417_A01_NoSecondGroupWhileOneVisible()
    {
        var s = Field(1, "shambler", "crawler", "runner");
        Clear(s);
        Assert.That(s.EnemyCards.Groups, Has.Count.EqualTo(1));
        Assert.That(E.EncounterChance(s, "field", ActionType.Clear), Is.EqualTo(0));
        Assert.That(E.EncounterChance(s, "field", ActionType.Explore), Is.EqualTo(0));
        Assert.That(E.EncounterNote(s, "field", ActionType.Clear), Is.EqualTo("当前已有一组敌踪，本次探索或清理不会形成新组卡。"));
        Clear(s);
        Assert.That(s.EnemyCards.Groups, Has.Count.EqualTo(1), "已有可见组时不再形成新组");
        Assert.That(s.WorldEnemies.Count(w => w.GroupId != null), Is.EqualTo(1));
    }

    [Test]
    public void RollMiss_NoGroup()
    {
        var s = Field(3, "shambler", "crawler");
        Clear(s, roll: 1);
        Assert.That(s.EnemyCards.Groups, Is.Empty);
        Assert.That(s.WorldEnemies.Select(w => w.GroupId), Has.All.Null);
    }

    [Test]
    public void Q417_A01_MembersDoNotPatrol_NorDrawPatrolRolls()
    {
        var s = Field(3, "shambler", "crawler");
        Clear(s);
        Assert.That(s.WorldEnemies.Select(w => w.GroupId), Has.None.Null);
        var patrol = typeof(GameEngine).GetMethod("Patrol", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var rng = s.Rng.State;
        for (var i = 0; i < 200; i++) patrol.Invoke(E, new object[] { s });
        Assert.That(s.WorldEnemies.Select(w => w.LocationId), Has.All.EqualTo("field"), "组成员不自行巡游");
        Assert.That(s.Rng.State, Is.EqualTo(rng), "也不抽巡游随机数");
    }

    [Test]
    public void PreviewNotes_Verbatim()
    {
        // 首战未胜
        var story = NewStory();
        Assert.That(E.EncounterNote(story, "field", ActionType.Explore), Is.EqualTo("首战胜利前不会形成随机遭遇组卡。"));

        // 未到开放日
        var s = New();
        Assert.That(E.EncounterNote(s, "field", ActionType.Explore), Is.EqualTo($"从第{Cfg.Encounters.NoRandomBeforeDay}日起，探索或清理后才可能发现随机组卡。"));

        s = Field(3, "shambler", "wolf");
        Assert.That(E.EncounterNote(s, "camp", ActionType.Clear), Is.EqualTo("安全区不会形成随机遭遇组卡。"));
        var city = Cfg.Locations.First(l => !l.IsDungeon && (!l.RandomEncounterEnabled || l.SafeExterior) && !Cfg.Encounters.SafeLocations.Contains(l.Id)).Id;
        Assert.That(E.EncounterNote(s, city, ActionType.Explore), Is.EqualTo("城市与公共地点不会形成随机遭遇组卡。"));
        var room = Cfg.Locations.First(l => l.IsDungeon).Id;
        Assert.That(E.EncounterNote(s, room, ActionType.Explore), Is.EqualTo("地牢房间按固定敌踪处理。"));

        // 普通风险行：本地敌对含动物
        var p = E.EncounterChance(s, "field", ActionType.Explore);
        Assert.That(p, Is.GreaterThan(0));
        Assert.That(E.EncounterNote(s, "field", ActionType.Explore),
            Is.EqualTo($"完成后发现敌踪概率{Fmt.Pct(p)}（本地敌对2/上限{E.LocalCapOf("field")}）。"));
    }

    [Test]
    public void Q438_A01_HistoricalGracePreview_UsesOriginalDeadline()
    {
        var s = Field(3, "shambler", "crawler");
        s.EncounterGraceUntil = s.GameMinute + 45;
        Assert.That(E.EncounterChance(s, "field", ActionType.Explore), Is.EqualTo(0), "历史期限内不形成新的随机遭遇组卡");
        Assert.That(E.EncounterNote(s, "field", ActionType.Explore), Is.EqualTo(GameEngine.LegacyGraceText));
        Assert.That(E.EncounterNote(s, "field", ActionType.Clear), Is.EqualTo(GameEngine.LegacyGraceText));
        Clear(s);
        Assert.That(s.EnemyCards.Groups, Is.Empty);
        // 时长 = 原截止 − 当前世界时刻；读档不延期；到期移除该行
        var left = s.EncounterGraceUntil - s.GameMinute;
        Assert.That(left, Is.LessThan(45));
        Assert.That(E.EncounterNote(s, "field", ActionType.Clear), Is.EqualTo(GameEngine.LegacyGraceText));
        var t = Survival.Core.Save.SaveSerializer.Deserialize(Survival.Core.Save.SaveSerializer.Serialize(s), CfgV2);
        Assert.That(t.EncounterGraceUntil, Is.EqualTo(s.EncounterGraceUntil));
        s.GameMinute = s.EncounterGraceUntil;
        Assert.That(E.EncounterNote(s, "field", ActionType.Clear), Does.StartWith("完成后发现敌踪概率"));
    }
}
