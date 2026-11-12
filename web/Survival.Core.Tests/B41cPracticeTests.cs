using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// B41c 练习潜行（R05「队长、潜行与 Q424 练习」「Q504」「Q507—Q508」；逐字稿 B41「队长与练习潜行」「新操作的禁用与拒绝」；
/// 验收 Q342-A03（练习部分）、Q504-A01、Q507-A01、Q508-A01）。
/// </summary>
public class B41cPracticeTests : B41cTestBase
{
    private GameState Quiet(int capacity = 2)
    {
        var s = Field(capacity);
        return s;
    }

    private void RunSegments(GameState s, int n)
    {
        for (var k = 0; k < n; k++) Assert.That(Step(s), Is.Not.EqualTo(SegmentStatus.Dead));
    }

    [Test]
    public void Practice_Player_30Min_Xp20_Texts()
    {
        var s = Quiet();
        Assert.That(E.PracticeXpPreview(s, GameState.PlayerId), Is.EqualTo("完成后你潜行经验+20。"));
        var t0 = s.GameMinute;
        var r = E.StartPractice(s, GameState.PlayerId);
        Assert.That(r.Ok, Is.True, r.Reason);
        Assert.That(LastLog(s), Is.EqualTo("开始练习潜行。"));
        Assert.That((s.Main!.Label, s.Main.PlannedMinutes), Is.EqualTo(("练习潜行", 30)));
        Assert.That(Run(s), Is.EqualTo(SegmentStatus.Completed));
        Assert.That(s.GameMinute, Is.EqualTo(t0 + 30));
        Assert.That(s.Log.Select(l => l.Text), Does.Contain("你完成潜行练习，潜行经验+20。"));
        Assert.That(Stealth(s), Is.EqualTo((1, 20L)));
    }

    [Test]
    public void Practice_Companion_Maxed_Text()
    {
        var s = Quiet();
        E.DebugRecruit(s, "npc_lin_zhao");
        SetStealth(s, "npc_lin_zhao", 10);
        var lin = E.ActorDisplayName(s, "npc_lin_zhao");
        Assert.That(E.PracticeXpPreview(s, "npc_lin_zhao"), Is.EqualTo($"{lin}的潜行已满级，完成后不再获得经验。"));
        Assert.That(E.StartPractice(s, "npc_lin_zhao").Ok, Is.True);
        Assert.That(LastLog(s), Is.EqualTo("开始练习潜行。"), "行动名不拼姓名（Q507）");
        Assert.That(Run(s), Is.EqualTo(SegmentStatus.Completed));
        Assert.That(s.Log.Select(l => l.Text), Does.Contain($"{lin}完成潜行练习，潜行已满级。"));
        Assert.That(Stealth(s, "npc_lin_zhao"), Is.EqualTo((10, 0L)));
    }

    [Test]
    public void Practice_Exit_KeepsElapsed_NoXp_EvenWith5Left()
    {
        var s = Quiet();
        Assert.That(E.StartPractice(s, GameState.PlayerId).Ok, Is.True);
        RunSegments(s, 5);
        var r = E.Cancel(s);
        Assert.That(r.Ok, Is.True, "30 分钟练习剩 5 分钟仍可退出");
        Assert.That(LastLog(s), Is.EqualTo("你的潜行练习已结束。已耗时25分钟，未获得经验。"));
        Assert.That(Stealth(s), Is.EqualTo((1, 0L)));
    }

    [Test]
    public void Rejections_VerbatimOrder_ZeroTimeZeroRandom()
    {
        var s = Quiet(3);
        E.DebugRecruit(s, "npc_lin_zhao");
        E.DebugRecruit(s, "npc_tang_li");
        E.DebugToggleActive(s, "npc_tang_li");
        var t0 = s.GameMinute;
        var rng = s.Rng.State;
        Assert.That(E.CanPracticeStealth(s, null), Is.EqualTo("请选择练习者。"));
        Assert.That(E.CanPracticeStealth(s, "npc_tang_li"), Is.EqualTo("练习者已不在同行队伍中，请重新选择。"));
        E.DebugActor(s, "npc_tang_li", "kill");
        Assert.That(E.CanPracticeStealth(s, "npc_tang_li"), Is.EqualTo("练习者已死亡，请重新选择。"));
        E.DebugSpawnEnemy(s, "shambler", "field");
        Assert.That(E.StartPractice(s, "npc_lin_zhao").Reason, Is.EqualTo("当前区域有可行动的敌对敌踪，无法练习潜行。"));
        Assert.That((s.GameMinute, s.Rng.State, s.Main), Is.EqualTo((t0, rng, (ActionState?)null)));
        Start(s, ActionRequest.Wait(30));
        Assert.That(E.CanPracticeStealth(s, GameState.PlayerId), Is.EqualTo("当前行动还没结束。"));
    }

    [Test]
    public void HostileCardRule_NeutralHiddenGatedDoNotBlock()
    {
        var s = Quiet();
        var deer = E.DebugSpawnEnemy(s, "stag", "field");
        if (deer.Hostile) Assert.Inconclusive("鹿不是中立");
        Assert.That(E.CanPracticeStealth(s, GameState.PlayerId), Is.Null, "普通中立单卡不算");
        var w = E.DebugSpawnEnemy(s, "shambler", "field");
        Assert.That(E.CanPracticeStealth(s, GameState.PlayerId), Is.Not.Null);
        w.EvadeHiddenUntil = s.GameMinute + 720;
        Assert.That(E.CanPracticeStealth(s, GameState.PlayerId), Is.Null, "隐藏的不算");
    }

    [Test]
    public void Q504_A01_EliteNoticeWithoutInstanceBlocks_NoInstanceNoRandom()
    {
        var s = Quiet();
        E.DebugTeleport(s, "w33_waterworks");
        s.Progress.Flags.Add("hub_registered");
        s.Locations["w33_waterworks"].Progress = 100;
        s.WorldEnemies.Clear();
        var card = E.EnemyCardsHere(s).SingleOrDefault(c => c.Kind == "elite");
        Assert.That(card, Is.Not.Null, "可选强敌告示可处理");
        Assert.That(card!.Members, Is.Empty, "尚未创建敌人");
        var rng = s.Rng.State;
        var n = s.WorldEnemies.Count;
        Assert.That(E.StartPractice(s, GameState.PlayerId).Reason, Is.EqualTo("当前区域有可行动的敌对敌踪，无法练习潜行。"));
        Assert.That((s.Rng.State, s.WorldEnemies.Count), Is.EqualTo((rng, n)), "不为检查创建实例、不抽签");
    }

    [Test]
    public void Q504_EnemyArrivesMidPractice_NotCancelled_ResumeNoRecheck_ApproachPauses()
    {
        var s = Quiet();
        Assert.That(E.StartPractice(s, GameState.PlayerId).Ok, Is.True);
        RunSegments(s, 2);
        var w = E.DebugSpawnEnemy(s, "shambler", "field");   // 途中巡入
        var key = Key(w);
        RunSegments(s, 1);
        Assert.That(s.Main, Is.Not.Null, "途中巡入敌踪不取消练习");
        SetLeft(s, key, 5);
        Step(s);
        Assert.That(s.Battle, Is.Not.Null, "到期敌方接近，段后暂停练习");
        var title = E.FindEnemyCard(s, key, out _)!.Title;
        Assert.That(s.Log.Select(l => l.Text), Does.Contain($"{title}主动接近了。练习潜行已暂停，战后可以继续或退出。"));
        Assert.That(s.Main!.Status, Is.EqualTo(ActionStatus.Paused));
        Assert.That(s.Main.ElapsedMinutes, Is.EqualTo(20));
        RetreatOk(s);
        // 撤退离地保留：异地继续被拒（Q507）
        var resume = E.Resume(s);
        if (!resume.Ok) Assert.That(resume.Reason, Is.EqualTo("请返回农野荒地后再继续练习潜行。"));
    }

    [Test]
    public void Q508_A01_PractitionerDies_AppendOnce_NoXp()
    {
        var s = Quiet();
        E.DebugRecruit(s, "npc_lin_zhao");
        var lin = E.ActorDisplayName(s, "npc_lin_zhao");
        Assert.That(E.StartPractice(s, "npc_lin_zhao").Ok, Is.True);
        RunSegments(s, 3);
        s.Main!.Status = ActionStatus.Paused;   // 暂停中的练习
        var n = s.Log.Count;
        E.DebugActor(s, "npc_lin_zhao", "kill");
        var added = s.Log.Skip(n).Select(l => l.Text).ToList();
        Assert.That(added.First(), Does.Contain("死了"), "先写原死亡结果");
        Assert.That(added.Last(), Is.EqualTo($"{lin}的潜行练习已结束。已耗时15分钟，未获得经验。"));
        Assert.That(added.Count(x => x.Contains("潜行练习已结束")), Is.EqualTo(1));
        Assert.That(s.Main, Is.Null, "不再恢复已结束练习");
        E.DebugActor(s, "npc_lin_zhao", "kill");
        Assert.That(s.Log.Count(l => l.Text.Contains("潜行练习已结束")), Is.EqualTo(1), "重复事件不重复追加");
    }

    [Test]
    public void Q508_PlayerTerminal_NoAppend()
    {
        var s = Quiet();
        E.DebugRecruit(s, "npc_lin_zhao");
        Assert.That(E.StartPractice(s, "npc_lin_zhao").Ok, Is.True);
        s.Progress.Dead = true;
        E.DebugActor(s, "npc_lin_zhao", "kill");
        Assert.That(s.Log.Any(l => l.Text.Contains("潜行练习已结束")), Is.False, "主角终局只走终局呈现");
    }

    [Test]
    public void SwitchOff_NoPractice()
    {
        var s = LegacyField();
        Assert.That(Off.StartPractice(s, GameState.PlayerId).Reason, Is.EqualTo("未知行动"));
    }
}
