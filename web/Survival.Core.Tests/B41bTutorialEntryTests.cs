using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// B41b 里程碑 7：首战入口改成教学敌踪卡（R05「Q416、Q421」末段、待确认 Q421 三项均 A；S15 首战；验收 Q421-A01）。
/// </summary>
public class B41bTutorialEntryTests : B41bTestBase
{
    private static readonly int[] Options = { 20, 25, 30, 35, 40 };

    /// <summary>序章新局读完开场对白，冻结刷新巡游，从营地走到荒地（途中提醒自动继续）。</summary>
    private GameState ArriveField(uint seed = 3, bool withOther = false)
    {
        var s = NewStory(seed);
        ReadDialogues(s);
        s.SpawnNextCheck = int.MaxValue;
        s.PatrolNextCheck = int.MaxValue;
        if (withOther) E.DebugSpawnEnemy(s, "crawler", "field");
        Start(s, ActionRequest.Travel("field"));
        for (var i = 0; i < 200 && s.Main != null && !s.Progress.Dead; i++)
            if (E.Advance(s).Status == SegmentStatus.Paused) E.Resume(s);
        Assert.That(s.CurrentLocation, Is.EqualTo("field"));
        return s;
    }

    [Test]
    public void Q421_FirstArrival_CardNotBattle_WarningAndGuideOnRealStart_NotReplayed()
    {
        var s = NewStory(3);
        ReadDialogues(s);
        Assert.That(E.Preview(s, ActionRequest.Travel("field")).Warnings,
            Does.Contain($"第一次到达{E.LocName("field")}会遇到一只{Cfg.Enemies.Single(x => x.Id == "shambler").Name}（首战）"), "移动预览改用定稿短句");

        s = ArriveField(withOther: true);
        var tid = s.Story.TutorialEnemyId!.Value;
        var key = GameEngine.SingleCardKey(tid);
        Assert.That(s.Battle, Is.Null, "到达时不直接开战");
        Assert.That(s.Story.TutorialTriggered, Is.True);
        Assert.That(s.Story.DialogueLog.Any(d => d.EventId == GameEngine.DialogueTutorialArrive), Is.False, "警告不在到达时播");
        Assert.That(E.EnemyCardsHere(s).Select(c => c.Key), Does.Contain(key), "教学敌作为敌踪卡现身");
        Assert.That(Options, Does.Contain(Left(s, key)), "教学卡首次可操作时抽接近时间");
        var other = GameEngine.SingleCardKey(s.WorldEnemies.Single(w => w.Id != tid && w.LocationId == "field" && w.EnemyId == "crawler").Id);
        Assert.That(E.CardTimer(s, other), Is.Null, "其他门控卡首胜前不抽、不计时");

        Assert.That(E.ApproachCard(s, key).Ok, Is.True);
        Assert.That(s.Battle!.Tutorial, Is.True);
        Assert.That(s.Battle.Context, Is.EqualTo(GameEngine.FearApproach), "按实际入战方式取受惊系数");
        Assert.That(s.Story.PendingDialogues, Does.Contain("tutorial_arrive"), "真正开战时播饱饱警告");
        Assert.That(E.DialogueLocked(s), Is.True, "读完警告才接受第一条战斗指令");
        Assert.That(s.Story.TutorialBattleId, Is.EqualTo(s.Battle.BattleId));
        ReadDialogues(s);

        RetreatOk(s);
        var sessions = s.Story.DialogueLog.Count(d => d.EventId == GameEngine.DialogueTutorialArrive);
        if (s.CurrentLocation != "field") TravelTo(s, "field");
        Assert.That(E.ApproachCard(s, key).Ok, Is.True);
        Assert.That(s.Battle!.Tutorial, Is.True);
        Assert.That(s.Story.DialogueLog.Count(d => d.EventId == GameEngine.DialogueTutorialArrive), Is.EqualTo(sessions), "撤退后再进教学战不重播");
    }

    [Test]
    public void Q421_EnemyApproachIntoTutorial_PlaysWarning_PrefixEnemyApproach()
    {
        var s = ArriveField(5);
        ReadDialogues(s);
        var key = GameEngine.SingleCardKey(s.Story.TutorialEnemyId!.Value);
        SetLeft(s, key, 0);
        Assert.That(E.CheckEnemyApproach(s), Is.True);
        Assert.That(s.Battle!.Tutorial, Is.True);
        Assert.That(s.Battle.Context, Is.EqualTo(GameEngine.FearEncounter));
        Assert.That(s.Battle.Log.First(), Does.Contain(" · 敌方接近："), "敌方到期前缀只写敌方接近");
        Assert.That(s.Story.PendingDialogues, Does.Contain("tutorial_arrive"));
    }
}
