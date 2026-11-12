using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// B41b 里程碑 3：到期队列与敌方主动开战（R05「Q416、Q421」「Q441」「Q467」「Q468—Q470」；逐字稿「结果、取消与队长回退」「B41b Q439—Q444 正式文案」；
/// 验收 Q342-A02 后半、Q416-A01 前半、Q441-A01、Q444-A01 敌方部分、Q467-A01、Q468-A01、Q469-A01、Q470-A01）。
/// </summary>
public class B41bEnemyApproachTests : B41bTestBase
{
    private string Single(GameState s, int k = 0) => GameEngine.SingleCardKey(s.WorldEnemies[k].Id);

    [Test]
    public void Q416_PausableWork_SegmentCommittedThenPaused_BattleThenContinueSameProgress()
    {
        var s = Field(3, "shambler");
        var key = Single(s);
        SetLeft(s, key, 5);
        var progress = s.Locations["field"].Progress;
        Start(s, ActionRequest.Explore());
        var id = s.Main!.ActionId;
        E.DrainEvents(s);
        Assert.That(Step(s), Is.EqualTo(SegmentStatus.Paused));
        var a = s.Main!;
        Assert.That(a.ActionId, Is.EqualTo(id));
        Assert.That(a.ElapsedMinutes, Is.EqualTo(5), "本段完整提交后暂停");
        Assert.That(a.PauseReason, Is.EqualTo(PauseReason.EnemyApproach));
        Assert.That(a.PauseNote, Is.EqualTo("游荡尸主动接近"));
        var ev = E.DrainEvents(s);
        Assert.That(ev.Where(e => e.Kind == GameEventKind.ActionPaused).Select(e => e.Text), Is.EqualTo(new[] { "游荡尸主动接近了。探索周边已暂停，战后可以继续或退出。" }));
        var b = s.Battle!;
        Assert.That(b.CardKey, Is.EqualTo(key));
        Assert.That(b.Context, Is.EqualTo(GameEngine.FearEncounter), "敌方主动开战受惊情境系数 1.0");
        Assert.That(b.Log.First(), Does.Contain(" · 敌方接近："), "战报入战方式前缀");
        Assert.That(E.Resume(s).Ok, Is.False, "战斗中不能继续原行动");

        WinBattle(s);
        Assert.That(s.Main!.Status, Is.EqualTo(ActionStatus.Paused));
        Assert.That(E.ActionProgress(s)!.PauseText, Is.EqualTo("游荡尸主动接近"), "敌人已死，原因仍是开战快照");
        Assert.That(E.Resume(s).Ok, Is.True);
        Assert.That(Run(s), Is.EqualTo(SegmentStatus.Completed));
        Assert.That(s.Locations["field"].Progress, Is.EqualTo(Math.Min(100, progress + Cfg.Actions.ExploreProgress)), "同一进度续完，不重复提交旧段");
    }

    [Test]
    public void Q416_WorkCompletesSameSegment_ResultFirstThenBattle()
    {
        var s = Field(3, "shambler");
        var key = Single(s);
        SetLeft(s, key, 30);
        Start(s, ActionRequest.Explore());
        for (var k = 0; k < 5; k++) Assert.That(Step(s), Is.EqualTo(SegmentStatus.Running));
        E.DrainEvents(s);
        Assert.That(Step(s), Is.EqualTo(SegmentStatus.Completed));
        Assert.That(s.Main, Is.Null);
        Assert.That(s.Battle!.CardKey, Is.EqualTo(key));
        var ev = E.DrainEvents(s).Select(e => e.Kind).ToList();
        Assert.That(ev.IndexOf(GameEventKind.ActionCompleted), Is.LessThan(ev.IndexOf(GameEventKind.Encounter)), "先完成原结算再开战");
        Assert.That(ev, Does.Not.Contain(GameEventKind.ActionPaused));
    }

    [Test]
    public void Q467_MainRestAndWait_WholeThenBattle_ExitAlsoEnds()
    {
        var s = Field(3, "shambler");
        var key = Single(s);
        SetLeft(s, key, 5);
        Start(s, ActionRequest.Rest(30));
        Assert.That(Step(s), Is.EqualTo(SegmentStatus.Running));
        Assert.That(s.Battle, Is.Null, "普通休息整次结束后再开战");
        Assert.That(Left(s, key), Is.EqualTo(0));
        Assert.That(E.CardApproachStatus(s, E.FindEnemyCard(s, key, out _)!), Is.EqualTo("已到期，等待交战。"));
        for (var k = 0; k < 4; k++) Assert.That(Step(s), Is.EqualTo(SegmentStatus.Running));
        Assert.That(s.Battle, Is.Null);
        Assert.That(Step(s), Is.EqualTo(SegmentStatus.Completed));
        Assert.That(s.Battle, Is.Not.Null);
        Assert.That(s.GameMinute, Is.EqualTo(s.Battle!.StartMinute));

        // 普通等待：主动合法退出也算结束
        var t = Field(3, "shambler");
        SetLeft(t, Single(t), 5);
        Start(t, ActionRequest.Wait(60));
        Step(t);
        Step(t);
        Assert.That(t.Battle, Is.Null);
        Assert.That(E.Cancel(t).Ok, Is.True);
        Assert.That(t.Battle, Is.Not.Null, "退出等待即结束，开战");
    }

    [Test]
    public void Q441_RestSubDuringPause_WholeThenBattle_ParentKeepsOriginalReason()
    {
        var s = Field(3, "shambler");
        var key = Single(s);
        SetLeft(s, key, 40);
        Start(s, ActionRequest.Explore());
        Step(s);
        var m = s.Main!;
        m.Status = ActionStatus.Paused;
        m.PauseReason = PauseReason.StatAlert;
        m.PauseStat = StatId.Satiety;
        var work = m.WorkDone;
        SetLeft(s, key, 5);
        Start(s, ActionRequest.Rest(30));
        Assert.That(s.Sub, Is.Not.Null);
        E.DrainEvents(s);
        for (var k = 0; k < 5; k++)
        {
            Assert.That(Step(s), Is.EqualTo(SegmentStatus.Running));
            Assert.That(s.Battle, Is.Null, "暂停中的休息整次结束后再开战");
        }
        Assert.That(Step(s), Is.EqualTo(SegmentStatus.Completed));
        Assert.That(s.Sub, Is.Null);
        Assert.That(s.Battle, Is.Not.Null);
        Assert.That(s.Main!.PauseReason, Is.EqualTo(PauseReason.StatAlert), "原本已暂停的行动不伪造第二次暂停");
        Assert.That(s.Main.PauseNote, Is.Null);
        Assert.That(s.Main.WorkDone, Is.EqualTo(work), "父行动不推进");
        var ev = E.DrainEvents(s);
        Assert.That(ev.Any(e => e.Text == "游荡尸主动接近了。"), Is.True);
        Assert.That(ev.Any(e => e.Kind == GameEventKind.ActionPaused), Is.False);
    }

    [Test]
    public void Q470_SameSegmentAlert_OneAlertLog_NoDuplicatePaused()
    {
        var s = Field(3, "shambler");
        SetLeft(s, Single(s), 5);
        s.Stats.Satiety = 20.05;
        s.AlertLatched[StatId.Satiety] = false;
        Start(s, ActionRequest.Explore());
        E.DrainEvents(s);
        Assert.That(Step(s), Is.EqualTo(SegmentStatus.Paused));
        var ev = E.DrainEvents(s);
        Assert.That(ev.Count(e => e.Kind == GameEventKind.Alert && e.Stat == StatId.Satiety), Is.EqualTo(1), "状态提醒日志照常");
        Assert.That(ev.Where(e => e.Kind == GameEventKind.ActionPaused).Select(e => e.Text), Is.EqualTo(new[] { "游荡尸主动接近了。探索周边已暂停，战后可以继续或退出。" }));
        Assert.That(s.Main!.PauseReason, Is.EqualTo(PauseReason.EnemyApproach), "暂停原因写敌方接近");
    }

    [Test]
    public void Q468_EarlierFirst_ThenGroupBeforeSingle_OneBattleOnly()
    {
        var s = Field(2, "shambler", "crawler", "worker");
        ClearField(s);
        var g = GameEngine.GroupCardKey(s.EnemyCards.Groups.Single().Id);
        var single = GameEngine.SingleCardKey(s.WorldEnemies.Single(w => w.GroupId == null).Id);
        SetLeft(s, g, 0);
        SetLeft(s, single, 0);
        s.EnemyCards.Timers[single].DueMinute = s.GameMinute - 10;
        Assert.That(E.DueCardsHere(s).Select(c => c.Key), Is.EqualTo(new[] { single, g }), "到期时刻早的先");
        s.EnemyCards.Timers[single].DueMinute = s.GameMinute;
        Assert.That(E.DueCardsHere(s).Select(c => c.Key), Is.EqualTo(new[] { g, single }), "同刻组卡先于单卡");
        Assert.That(E.CheckEnemyApproach(s), Is.True);
        Assert.That(s.Battle!.CardKey, Is.EqualTo(g), "一次只开一场");
        Assert.That(E.CheckEnemyApproach(s), Is.False);
    }

    [Test]
    public void Q468_FixedBeforeSingle_SameMinute_StableKeyNumeric()
    {
        var s = New(3101);
        UnlockCapacity(s, 3);
        s.GameMinute = At(2, 9, 0);
        s.SpawnNextCheck = int.MaxValue;
        s.PatrolNextCheck = int.MaxValue;
        E.DebugMapTeleport(s, "m02_checkpoint");
        var fixedKey = E.EnemyCardsHere(s).Single(c => c.Kind == "fixed").Key;
        var a = E.DebugSpawnEnemy(s, "shambler", "m02_checkpoint");
        var b = E.DebugSpawnEnemy(s, "crawler", "m02_checkpoint");
        foreach (var k in new[] { GameEngine.SingleCardKey(b.Id), GameEngine.SingleCardKey(a.Id), fixedKey }) SetLeft(s, k, 0);
        Assert.That(E.DueCardsHere(s).Select(c => c.Key), Is.EqualTo(new[] { fixedKey, GameEngine.SingleCardKey(a.Id), GameEngine.SingleCardKey(b.Id) }));
        Assert.That(E.CheckEnemyApproach(s), Is.True);
        Assert.That(s.Battle!.FixedKey, Is.EqualTo(fixedKey), "固定战身份留在内部记录");
        Assert.That(s.Battle.Log.First(), Does.Contain(" · 敌方接近："), "敌方到期进入固定战，前缀只写敌方接近");
    }

    [Test]
    public void Q416_GuardAndLegacyGrace_Wait_PlayerMayStillApproach()
    {
        var s = Field(3, "shambler");
        var key = Single(s);
        SetLeft(s, key, 0);
        s.EnemyCards.Timers[key].GuardUntil = s.GameMinute + 60;
        Assert.That(E.CheckEnemyApproach(s), Is.False, "保护只挡该卡主动开战");
        Assert.That(E.CanApproachCard(s, key), Is.Null, "玩家仍可主动处理");
        s.EnemyCards.Timers[key].GuardUntil = s.GameMinute;
        s.EncounterGraceUntil = s.GameMinute + 30;
        Assert.That(E.CheckEnemyApproach(s), Is.False, "旧档历史保护期内等待");
        s.EncounterGraceUntil = s.GameMinute;
        Assert.That(E.CheckEnemyApproach(s), Is.True);
    }

    [Test]
    public void Q416_DueBeforeLeaving_TravelNotIntercepted_ReturnEngagesAtArrival()
    {
        var s = Field(3, "shambler");
        var key = Single(s);
        SetLeft(s, key, 0);
        s.EnemyCards.Timers[key].GuardUntil = s.GameMinute + 5;   // 让它在出发时还没开战
        Start(s, ActionRequest.Travel("camp"));
        Assert.That(Run(s), Is.EqualTo(SegmentStatus.Completed));
        Assert.That(s.Battle, Is.Null, "移动途中不拦截");
        Start(s, ActionRequest.Travel("field"));
        Assert.That(Run(s), Is.EqualTo(SegmentStatus.Completed));
        Assert.That(s.Battle!.CardKey, Is.EqualTo(key), "回到同地同区，到达边界重验后开战");
    }
}
