using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// B41b 里程碑 4：同卡撤退保护（R05「Q416、Q421」末段、「B41b Q439—Q444」Q442、Q443；验收 Q416-A01 保护部分、Q442-A01、Q443-A01）。
/// </summary>
public class B41bRetreatGuardTests : B41bTestBase
{
    /// <summary>强制撤退成功（不要求战后没有下一场：其他到期卡可在战后边界接战）。</summary>
    private void Retreat(GameState s)
    {
        PlayerFirst(s);
        E.BattleStep(s);
        Force(s, Below(0.0001));
        Assert.That(Cmd(s, "retreat").Ok, Is.True);
    }

    [Test]
    public void Q416_SameCardGuard60_OtherDueCardEngagesAfterBattle()
    {
        var s = Field(3, "shambler", "crawler");
        var a = GameEngine.SingleCardKey(s.WorldEnemies[0].Id);
        var b = GameEngine.SingleCardKey(s.WorldEnemies[1].Id);
        Assert.That(E.ApproachCard(s, a).Ok, Is.True);
        SetLeft(s, a, 0);
        SetLeft(s, b, 0);
        var grace = s.EncounterGraceUntil;
        Retreat(s);
        var at = s.Battle!.StartMinute;
        Assert.That(E.CardTimer(s, a)!.GuardUntil, Is.EqualTo(at + 60), "同卡 60 世界分钟，从撤退结算时刻起算");
        Assert.That(E.CardTimer(s, b)!.GuardUntil, Is.Null);
        Assert.That(s.Battle!.CardKey, Is.EqualTo(b), "其他到期卡在战后边界接战");
        Assert.That(s.EncounterGraceUntil, Is.EqualTo(grace), "不新增全地点保护");
    }

    [Test]
    public void Q416_GuardHoldsDueCard_ThenEngagesAfterExpiry_RepeatTakesNewDeadline()
    {
        var s = Field(3, "shambler");
        var a = GameEngine.SingleCardKey(s.WorldEnemies[0].Id);
        Assert.That(E.ApproachCard(s, a).Ok, Is.True);
        Retreat(s);
        Assert.That(s.Battle, Is.Null);
        var first = E.CardTimer(s, a)!.GuardUntil!.Value;
        Assert.That(first, Is.EqualTo(s.GameMinute + 60));
        Assert.That(E.CanApproachCard(s, a), Is.Null, "保护期内玩家仍可主动处理");

        // 再接近、再撤退：取新的截止
        Start(s, ActionRequest.Wait(30));
        Assert.That(Run(s), Is.EqualTo(SegmentStatus.Completed));
        Assert.That(E.ApproachCard(s, a).Ok, Is.True);
        Retreat(s);
        var second = E.CardTimer(s, a)!.GuardUntil!.Value;
        Assert.That(second, Is.EqualTo(s.GameMinute + 60));
        Assert.That(second, Is.GreaterThan(first));

        SetLeft(s, a, 0);
        Assert.That(E.CheckEnemyApproach(s), Is.False, "到期后等待保护结束");
        Assert.That(E.CardApproachStatus(s, E.FindEnemyCard(s, a, out _)!), Is.EqualTo("撤退保护剩余1小时。"));
        Start(s, ActionRequest.Wait(60));
        Assert.That(Run(s), Is.EqualTo(SegmentStatus.Completed));
        Assert.That(s.GameMinute, Is.EqualTo(second));
        Assert.That(s.Battle!.CardKey, Is.EqualTo(a), "保护结束后在下一边界开战");
    }

    [Test]
    public void Q443_LegacyBattleWithoutCard_EachSurvivorGetsOwnGuard_Once()
    {
        var s = Field(3, "shambler", "crawler", "worker");
        var ids = s.WorldEnemies.Select(w => w.Id).ToList();
        Assert.That(E.DebugStartBattle(s, ids.Take(2)), Is.True);
        Assert.That(s.Battle!.CardKey, Is.Null, "无对应卡的旧战斗");
        var grace = s.EncounterGraceUntil;
        var enemies = s.WorldEnemies.Count;
        Retreat(s);
        var until = s.GameMinute + 60;
        Assert.That(E.CardTimer(s, GameEngine.SingleCardKey(ids[0]))!.GuardUntil, Is.EqualTo(until));
        Assert.That(E.CardTimer(s, GameEngine.SingleCardKey(ids[1]))!.GuardUntil, Is.EqualTo(until));
        Assert.That(E.CardTimer(s, GameEngine.SingleCardKey(ids[2]))?.GuardUntil, Is.Null, "没参战的不得保护");
        Assert.That(s.EncounterGraceUntil, Is.EqualTo(grace), "不创建全地点保护");
        Assert.That(s.WorldEnemies.Count, Is.EqualTo(enemies), "不生成新敌人");
    }

    [Test]
    public void Victory_NoGuard()
    {
        var s = Field(3, "shambler", "crawler");
        var b = GameEngine.SingleCardKey(s.WorldEnemies[1].Id);
        Assert.That(E.ApproachCard(s, GameEngine.SingleCardKey(s.WorldEnemies[0].Id)).Ok, Is.True);
        WinBattle(s);
        Assert.That(E.CardTimer(s, b)?.GuardUntil, Is.Null);
        Assert.That(s.EnemyCards.Timers.Values.Any(t => t.GuardUntil != null), Is.False, "胜利不新增保护");
    }
}
