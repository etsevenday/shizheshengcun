using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 批次 B11（R35、Q31 答复）：新节点危险度与随机接敌（node-expansion-rules.json、R05）。
/// 城市与公共城内不随机接敌；中立旧城废墟按野外；地牢房间只有固定遭遇；危险度只作显示；旧 8 地点参数不变。
/// 新人口区的刷新/巡游在 B20：本批新节点没有自然敌人，只有开发入口放的敌人会触发接敌。
/// </summary>
public class B11EncounterTests : TestBase
{
    private GameState Day2()
    {
        var s = New();
        s.GameMinute = At(2, 8, 0);
        return s;
    }

    [Test]
    public void NewWild_UsesNodeBaseAndRegionLocalCap_DangerDisplayOnly()
    {
        var s = Day2();
        Assert.That(E.EncounterChance(s, "w01_farm", ActionType.Explore), Is.EqualTo(0), "无敌人时为 0");
        E.DebugSpawnEnemy(s, "shambler", "w01_farm");
        E.DebugSpawnEnemy(s, "shambler", "w21_pine");
        Assert.That(E.LocalCapOf("w01_farm"), Is.EqualTo(4));
        Assert.That(E.EncounterChance(s, "w01_farm", ActionType.Explore), Is.EqualTo(0.02 * 1 / 4).Within(1e-12), "2%×1/4");
        Assert.That(E.EncounterChance(s, "w01_farm", ActionType.Clear), Is.EqualTo(0.01 * 1 / 4).Within(1e-12), "1%×1/4");
        Assert.That(E.Location("w01_farm").Danger, Is.EqualTo(1));
        Assert.That(E.Location("w21_pine").Danger, Is.EqualTo(3));
        Assert.That(E.EncounterChance(s, "w21_pine", ActionType.Explore), Is.EqualTo(E.EncounterChance(s, "w01_farm", ActionType.Explore)), "危险度不乘概率");
        // 中立旧城废墟按野外
        E.DebugSpawnEnemy(s, "shambler", "o02_market");
        Assert.That(E.EncounterChance(s, "o02_market", ActionType.Explore), Is.GreaterThan(0));
    }

    [Test]
    public void CityAndDungeon_NoRandomEncounter()
    {
        var s = Day2();
        foreach (var loc in new[] { "h02_square", "c02_church", "r04_reception", "h05_homestead", "d05_02" })
        {
            E.DebugSpawnEnemy(s, "shambler", loc);
            Assert.That(E.EncounterChance(s, loc, ActionType.Explore), Is.EqualTo(0), loc);
            Assert.That(E.DebugSpawnBlocked(s, loc), Is.Not.Null, loc);
        }
        Assert.That(E.EncounterNote(s, "c02_church", ActionType.Explore), Does.Contain("城市与公共地点"));
        Assert.That(E.EncounterNote(s, "d05_02", ActionType.Explore), Does.Contain("地牢"));
        Assert.That(E.DebugSpawnBlocked(s, "w01_farm"), Is.Null, "野外可放（上限 4）");
    }

    [Test]
    public void Legacy8_EncounterUnchanged()
    {
        var s = Day2();
        var before = s.WorldEnemies.Count(w => w.LocationId == "field" && w.Hostile);
        Assert.That(E.LocalCapOf("field"), Is.EqualTo(3));
        Assert.That(E.EncounterChance(s, "field", ActionType.Explore), Is.EqualTo(0.02 * before / 3).Within(1e-12));
        Assert.That(E.LocalCapOf("camp"), Is.EqualTo(0));
    }

    /// <summary>旧 8 地点种群不巡游进新区（不与新人口区共享 12）；新区里的敌人本批不巡游。</summary>
    [Test]
    public void Patrol_StaysInLegacyPopulation()
    {
        var s = Day2();
        var a = E.DebugSpawnEnemy(s, "shambler", "m01_road");
        var moved = false;
        var start = s.WorldEnemies.ToDictionary(w => w.Id, w => w.LocationId);
        for (var i = 0; i < 72; i++)
        {
            SetStats(s, 100, 80, 80, 90, 90, 60);
            Start(s, ActionRequest.Wait(60));
            Run(s);
            Assert.That(s.WorldEnemies.Where(w => w.Id != a.Id).All(w => E.Location(w.LocationId).IsStarter), Is.True, "旧种群不进新区");
            moved |= s.WorldEnemies.Any(w => start.TryGetValue(w.Id, out var l) && l != w.LocationId);
        }
        Assert.That(moved, Is.True, "旧种群照常巡游");
        Assert.That(a.LocationId, Is.EqualTo("m01_road"), "新区敌人本批不巡游");
    }

    [Test]
    public void NewNode_ClearNotOpenYet()
    {
        var s = Day2();
        E.DebugTeleport(s, "w01_farm");
        Assert.That(E.Start(s, ActionRequest.Clear()).Reason, Is.EqualTo("这里的清理在后续版本开放"));
        Start(s, ActionRequest.Explore());
        Run(s);
        Assert.That(s.Locations["w01_farm"].Progress, Is.EqualTo(25));
    }
}
