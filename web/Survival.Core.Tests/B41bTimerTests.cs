using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// B41b 里程碑 2：敌踪接近时限（R05「Q416、Q421 接近时间与到期入战」「B41b Q439—Q444」Q439、Q442；
/// 逐字稿「卡片、操作盘与倒计时」；验收 Q342-A02 前半、Q418-A01 时限部分、Q439-A01、Q442-A01 时限部分）。
/// </summary>
public class B41bTimerTests : B41bTestBase
{
    private static readonly int[] Options = { 20, 25, 30, 35, 40 };

    [Test]
    public void Q416_DrawOnceAtFirstOperable_MinusFivePerSegment_NoRedrawOnLoad()
    {
        var s = Field(3, "shambler");
        var key = GameEngine.SingleCardKey(s.WorldEnemies[0].Id);
        Assert.That(E.CardTimer(s, key), Is.Null, "还没到可操作边界，不抽");
        Start(s, ActionRequest.Wait(30));
        Assert.That(Step(s), Is.EqualTo(SegmentStatus.Running));
        var l = Left(s, key);
        Assert.That(Options, Does.Contain(l), "五档之一");
        Assert.That(Step(s), Is.EqualTo(SegmentStatus.Running));
        Assert.That(Left(s, key), Is.EqualTo(l - 5), "同地同区非战斗一段减 5");

        var rng = s.Rng.State;
        var t = SaveSerializer.Deserialize(SaveSerializer.Serialize(s), CfgV2);
        Assert.That(Left(t, key), Is.EqualTo(l - 5), "读档不重抽");
        Assert.That(t.Rng.State, Is.EqualTo(rng));
        Assert.That(E.Advance(t).Status, Is.EqualTo(SegmentStatus.Running));
        Assert.That(Left(t, key), Is.EqualTo(l - 10));
        Assert.That(E.CardApproachStatus(t, E.FindEnemyCard(t, key, out _)!), Is.EqualTo(GameEngine.MayApproachText), "（BM5b1 R05 Q639：不显示剩余时长）内部时限照减");
    }

    [Test]
    public void Q416_FiveOptionsAllReachable()
    {
        var seen = new HashSet<int>();
        for (uint seed = 1; seed <= 80; seed++)
        {
            var s = V2Field(seed);
            s.SuppressEnemyApproach = false;
            E.DebugSpawnEnemy(s, "shambler", "field");
            Start(s, ActionRequest.Wait(30));
            Step(s);
            seen.Add(Left(s, GameEngine.SingleCardKey(s.WorldEnemies[0].Id))!.Value);
        }
        Assert.That(seen, Is.EquivalentTo(Options));
    }

    [Test]
    public void Q416_TravelFreezesOrigin_ReturnKeepsRemaining()
    {
        var s = Field(3, "shambler");
        var key = GameEngine.SingleCardKey(s.WorldEnemies[0].Id);
        Start(s, ActionRequest.Wait(30));
        Step(s);
        Assert.That(E.Cancel(s).Ok, Is.True);
        var l = Left(s, key);
        TravelTo(s, "camp");
        Assert.That(Left(s, key), Is.EqualTo(l), "移动开始即离开起点：途中与到达都不扣");
        TravelTo(s, "field");
        Assert.That(Left(s, key), Is.EqualTo(l), "回来沿余量，完成移动不额外扣一段");
        Start(s, ActionRequest.Wait(30));
        Step(s);
        Assert.That(Left(s, key), Is.EqualTo(l - 5));
    }

    [Test]
    public void Q439_NeutralSingle_NoTimer_StatusLine()
    {
        var s = Field(3, "stag");
        var key = GameEngine.SingleCardKey(s.WorldEnemies[0].Id);
        Start(s, ActionRequest.Wait(30));
        Step(s);
        Step(s);
        Assert.That(E.CardTimer(s, key), Is.Null, "中立单卡不抽、不扣");
        Assert.That(E.CardApproachStatus(s, E.FindEnemyCard(s, key, out _)!), Is.EqualTo("中立，受到攻击后才转为敌对。"));
        // 转为敌对后首次可操作时才抽
        s.WorldEnemies[0].Hostile = true;
        Step(s);
        Assert.That(Options, Does.Contain(Left(s, key)));
    }

    [Test]
    public void Q421_Q419_GatedAndOptionalCards_NoTimer_StatusLines()
    {
        var s = Field(3, "shambler");
        var card = E.FindEnemyCard(s, GameEngine.SingleCardKey(s.WorldEnemies[0].Id), out _)!;
        card.TutorialGated = true;
        Assert.That(E.CardTimed(card), Is.False);
        Assert.That(E.CardApproachStatus(s, card), Is.EqualTo("首战胜利后开始接近计时。"));
        card.TutorialGated = false;
        card.Optional = true;
        Assert.That(E.CardTimed(card), Is.False);
        Assert.That(E.CardApproachStatus(s, card), Is.EqualTo("可选强敌，不会主动接近。"));
    }

    [Test]
    public void StatusLines_GuardLegacyDue()
    {
        var s = Field(3, "shambler");
        var key = GameEngine.SingleCardKey(s.WorldEnemies[0].Id);
        var card = E.FindEnemyCard(s, key, out _)!;
        // BM5b1（R05 Q639）：普通敌对卡还没抽／已到期都只写「可能主动接近。」；保护只给无时长句，并存时仍按原逻辑取较晚截止对应的身份
        Assert.That(E.CardApproachStatus(s, card), Is.EqualTo("可能主动接近。"), "还没抽");
        SetLeft(s, key, 0);
        Assert.That(E.CardApproachStatus(s, card), Is.EqualTo("可能主动接近。"));
        s.EnemyCards.Timers[key].GuardUntil = s.GameMinute + 60;
        Assert.That(E.CardApproachStatus(s, card), Is.EqualTo("此敌踪处于撤退保护中，期间可主动处理。"));
        s.EncounterGraceUntil = s.GameMinute + 90;
        Assert.That(E.CardApproachStatus(s, card), Is.EqualTo("旧存档接敌保护中。"), "两种保护并存取较晚截止的对应句");
        s.EncounterGraceUntil = s.GameMinute + 30;
        Assert.That(E.CardApproachStatus(s, card), Is.EqualTo("此敌踪处于撤退保护中，期间可主动处理。"));
        Assert.That(s.EnemyCards.Timers[key].GuardUntil, Is.EqualTo(s.GameMinute + 60), "内部截止不变");
    }

    [Test]
    public void Q442_SingleJoiningGroup_LosesTimerAndGuard_GroupDrawsNew()
    {
        var s = Field(3, "shambler", "crawler");
        var k0 = GameEngine.SingleCardKey(s.WorldEnemies[0].Id);
        var k1 = GameEngine.SingleCardKey(s.WorldEnemies[1].Id);
        SetLeft(s, k0, 40);
        s.EnemyCards.Timers[k0].GuardUntil = s.GameMinute + 600;
        SetLeft(s, k1, 40);
        ClearField(s);
        var g = s.EnemyCards.Groups.Single();
        var gk = GameEngine.GroupCardKey(g.Id);
        Assert.That(E.CardTimer(s, k0), Is.Null, "原单卡计时与保护失效");
        Assert.That(E.CardTimer(s, k1), Is.Null);
        Assert.That(Options, Does.Contain(Left(s, gk)), "组卡在首次可操作时新抽");
        Assert.That(E.CardTimer(s, gk)!.GuardUntil, Is.Null, "不继承最晚保护");
    }
}
