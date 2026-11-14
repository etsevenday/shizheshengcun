using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// BM5c 探索遭遇时序（R05 文首「Q605用户新增：探索随机突遇与在地到期分流」「BM5c Q620—Q624」；S14 Q605-A02—A05、A07，
/// R05 Q620-A01—A04、Q621-A01、Q624-A01；逐字稿 Q605、Q620—Q628 节；开发记录 BM5c）。
/// 农野荒地一次探索 30 分钟（6 段）；测试把开工锁好的命中签／检查段直接改写成必中／必不中、指定段，不经过随机。
/// </summary>
public class BM5cExploreEncounterTests : B41bTestBase
{
    private const string Head = "探索中遭遇敌人，即将进入战斗。";
    private string Single(GameState s, int k = 0) => GameEngine.SingleCardKey(s.WorldEnemies[k].Id);

    /// <summary>真实战斗结束走的战后边界（测试里 WinBattle 直接判死结算，不经过指令路径，这里补上同一边界）。</summary>
    private void AfterBattle(GameState s) =>
        typeof(GameEngine).GetMethod("EnemyApproachBoundary", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .Invoke(E, new object[] { s, true });

    /// <summary>开一次探索并改写锁好的命中签与检查段（roll=1 必不中）。</summary>
    private ActionState Explore(GameState s, double roll, int? segment)
    {
        Start(s, ActionRequest.Explore());
        var a = s.Main!;
        a.EncounterRoll = roll;
        a.EncounterSegment = segment;
        E.DrainEvents(s);
        return a;
    }

    [Test]
    public void Start_LocksOneSegmentWithinNMinus1()
    {
        for (uint seed = 1; seed <= 12; seed++)
        {
            var s = Field(3, "shambler");
            s.Rng = Rng.FromSeed(seed);
            Start(s, ActionRequest.Explore());
            var a = s.Main!;
            Assert.That(a.EncounterSegment, Is.InRange(1, 5), "30 分钟探索 N=6，K 取 1…N−1");
            Assert.That((a.EncounterChecked, a.EncounterPending), Is.EqualTo((false, false)));
            Assert.That(GameEngine.LegacyExploreCheck(a), Is.False, "新开探索不是旧开工规则");
        }
    }

    [Test]
    public void Q620_A01_DueBeforeStart_ExploreStartsAndCompletes_ThenResultFirstThenCard()
    {
        var s = Field(3, "shambler");
        var key = Single(s);
        SetLeft(s, key, 0);
        var progress = s.Locations["field"].Progress;
        Assert.That(E.CanStart(s, ActionRequest.Explore()), Is.Null, "已到期卡不拦开工");
        var a = Explore(s, 1, 2);
        var id = a.ActionId;
        for (var k = 0; k < 5; k++)
        {
            Assert.That(Step(s), Is.EqualTo(SegmentStatus.Running), $"第{k + 1}段不因开工前已到期的卡打断");
            Assert.That(s.Battle, Is.Null);
            Assert.That(s.Main!.ActionId, Is.EqualTo(id));
        }
        Assert.That(Left(s, key), Is.EqualTo(0), "只保留已到期，不重抽时限");
        Assert.That(Step(s), Is.EqualTo(SegmentStatus.Completed));
        Assert.That(s.Main, Is.Null);
        Assert.That(s.Locations["field"].Progress, Is.GreaterThan(progress), "成果先交");
        Assert.That(s.Battle!.CardKey, Is.EqualTo(key), "结束后检查原卡");
        var ev = E.DrainEvents(s);
        var done = ev.FindIndex(e => e.Kind == GameEventKind.ActionCompleted);
        var approach = ev.FindIndex(e => e.Text == "游荡尸主动接近了。");
        Assert.That(done, Is.GreaterThanOrEqualTo(0));
        Assert.That(approach, Is.GreaterThan(done), "先完成日志，再主动接近");
        Assert.That(E.CanStart(s, ActionRequest.Explore()), Is.Not.Null, "结束检查优先于下一次探索（Q620-A03）");
    }

    [Test]
    public void Q620_A01_DueDuringExplore_AlsoDeferred()
    {
        var s = Field(3, "shambler");
        var key = Single(s);
        SetLeft(s, key, 10);
        Explore(s, 1, 2);
        for (var k = 0; k < 5; k++) Assert.That(Step(s), Is.EqualTo(SegmentStatus.Running));
        Assert.That(Left(s, key), Is.EqualTo(0));
        Assert.That(s.Battle, Is.Null);
        Assert.That(Step(s), Is.EqualTo(SegmentStatus.Completed));
        Assert.That(s.Battle!.CardKey, Is.EqualTo(key));
    }

    [Test]
    public void Q620_A02_ClearIsNotDeferred()
    {
        var s = Field(3, "shambler");
        var key = Single(s);
        SetLeft(s, key, 0);
        var ls = s.Locations["field"];
        ls.ClearCharges = Math.Max(1, ls.ClearCharges);
        Start(s, ActionRequest.Clear());
        Assert.That(Step(s), Is.EqualTo(SegmentStatus.Paused), "清理与其他行动不套探索延后");
        Assert.That(s.Battle!.CardKey, Is.EqualTo(key));
    }

    [Test]
    public void Q605_A02_A04_Q621_A01_MidwayHit_PromptThenPassiveBattle_PauseKept_ResumeOnce()
    {
        var s = Field(3, "shambler");
        var progress = s.Locations["field"].Progress;
        var a = Explore(s, 0, 2);
        var id = a.ActionId;
        var label = a.Label;
        Assert.That(Step(s), Is.EqualTo(SegmentStatus.Running), "第1段不检查");
        Assert.That(s.Battle, Is.Null);
        Assert.That(Step(s), Is.EqualTo(SegmentStatus.Paused), "第2段末唯一检查点命中");
        var m = s.Main!;
        Assert.That(m.ActionId, Is.EqualTo(id));
        Assert.That(m.ElapsedMinutes, Is.EqualTo(10), "本段完整提交后暂停");
        Assert.That(m.PauseReason, Is.EqualTo(PauseReason.ExploreEncounter));
        Assert.That(m.PauseNote, Is.EqualTo("探索遭遇"));
        Assert.That(E.ActionProgress(s)!.PauseText, Is.EqualTo("探索遭遇"), "Q621 暂停原因");
        Assert.That((m.EncounterChecked, m.EncounterSegment), Is.EqualTo((true, 2)), "已检查回执");
        Assert.That(s.Locations["field"].Progress, Is.EqualTo(progress), "未完不先给探索度");
        var ev = E.DrainEvents(s);
        var paused = ev.Where(e => e.Kind == GameEventKind.ActionPaused).ToList();
        Assert.That(paused.Select(e => e.Text), Is.EqualTo(new[] { $"{Head}{label}已暂停，战后可以继续或退出。" }), "Q621 未完可退出态全文，日志只一条");
        Assert.That(paused[0].EncounterPrompt, Is.True, "壳据此播短提示");
        Assert.That(ev.Count(e => e.EncounterPrompt), Is.EqualTo(1));
        var b = s.Battle!;
        Assert.That(b.ExploreEncounter, Is.True);
        Assert.That(b.Context, Is.EqualTo(GameEngine.FearEncounter), "原被动接敌受惊口径");
        Assert.That(b.Log.First(), Does.Contain("探索遭遇"), "战报身份");
        Assert.That(b.CardKey, Does.StartWith("group:"), "名单锁成组卡");
        var g = s.EnemyCards.Groups.Single();
        Assert.That(g.Source, Is.EqualTo("explore"));

        WinBattle(s);
        Assert.That(s.Main!.Status, Is.EqualTo(ActionStatus.Paused), "战后不自动续工");
        Assert.That(E.Resume(s).Ok, Is.True, "原地手动继续");
        Assert.That(Run(s), Is.EqualTo(SegmentStatus.Completed));
        Assert.That(s.Battle, Is.Null, "同一次探索不再检查第二次");
        Assert.That(s.Locations["field"].Progress, Is.EqualTo(Math.Min(100, progress + Cfg.Actions.ExploreProgress)), "实际完成后只结一次");
    }

    [Test]
    public void Q621_A01_MidwayHit_LockedWork_TextWithoutExit()
    {
        var s = Field(3, "shambler");
        var a = Explore(s, 0, 1);
        a.NoExit = true;
        Assert.That(Step(s), Is.EqualTo(SegmentStatus.Paused));
        var ev = E.DrainEvents(s);
        Assert.That(ev.Where(e => e.Kind == GameEventKind.ActionPaused).Select(e => e.Text), Is.EqualTo(new[] { $"{Head}{a.Label}已暂停，战后可以继续。" }), "Q621 未完锁定态全文");
    }

    [Test]
    public void Q621_A01_HitAtCompletionBoundary_ResultFirst_NoFakePause()
    {
        var s = Field(3, "shambler");
        var progress = s.Locations["field"].Progress;
        Explore(s, 0, 99);   // 实际完成早于检查段：完成边界检查
        for (var k = 0; k < 5; k++) Assert.That(Step(s), Is.EqualTo(SegmentStatus.Running));
        Assert.That(Step(s), Is.EqualTo(SegmentStatus.Completed));
        Assert.That(s.Main, Is.Null, "不留假暂停工作");
        Assert.That(s.Locations["field"].Progress, Is.GreaterThan(progress), "成果先交");
        Assert.That(s.Battle!.ExploreEncounter, Is.True);
        var ev = E.DrainEvents(s);
        Assert.That(ev.Any(e => e.Kind == GameEventKind.ActionPaused), Is.False);
        var hit = ev.Single(e => e.EncounterPrompt);
        Assert.That((hit.Kind, hit.Text), Is.EqualTo((GameEventKind.Encounter, Head)), "Q621 完成态全文");
        Assert.That(ev.FindIndex(e => e.Kind == GameEventKind.ActionCompleted), Is.LessThan(ev.IndexOf(hit)));
    }

    [Test]
    public void Q605_A02_Miss_NoBattle_NoGroup()
    {
        var s = Field(3, "shambler");
        Explore(s, 1, 2);
        Assert.That(Run(s), Is.EqualTo(SegmentStatus.Completed));
        Assert.That(s.Battle, Is.Null);
        Assert.That(s.EnemyCards.Groups, Is.Empty, "探索不再完成后出组卡");
    }

    [Test]
    public void Q605_A02_BeforeRandomDay_NoHit()
    {
        var s = Field(3, "shambler");
        var day = Cfg.Encounters.NoRandomBeforeDay;
        if (day <= 1) Assert.Ignore("配置无日期门槛");
        s.GameMinute = At(day - 1, 9, 0);
        Explore(s, 0, 1);
        Assert.That(Run(s), Is.EqualTo(SegmentStatus.Completed), "原日期限制保持");
        Assert.That(s.Battle, Is.Null);
    }

    [Test]
    public void Q620_A04_CompletionHitAndDueCard_RandomFirst_ThenEndCheck()
    {
        var s = Field(1, "shambler", "shambler");
        var other = Single(s, 1);
        SetLeft(s, Single(s, 0), 0);
        SetLeft(s, other, 0);
        Explore(s, 0, 99);
        Assert.That(Run(s), Is.EqualTo(SegmentStatus.Completed));
        var b = s.Battle!;
        Assert.That(b.ExploreEncounter, Is.True, "先承接已锁随机战");
        Assert.That(b.Enemies.Count, Is.EqualTo(1), "容量 1 名单");
        WinBattle(s);
        AfterBattle(s);
        Assert.That(s.Battle!.CardKey, Is.EqualTo(other), "战后接续结束检查原到期卡");
        Assert.That(s.Battle.ExploreEncounter, Is.False);
    }

    [Test]
    public void Q605_A05_GroupedSingleCard_NotScheduledAgain()
    {
        var s = Field(3, "shambler");
        var key = Single(s);
        SetLeft(s, key, 0);
        Explore(s, 0, 99);
        Assert.That(Run(s), Is.EqualTo(SegmentStatus.Completed));
        Assert.That(s.Battle!.ExploreEncounter, Is.True, "同边界只先承接已锁随机战");
        WinBattle(s);
        AfterBattle(s);
        Assert.That(s.Battle, Is.Null, "组原单卡不再独立二次排战");
    }

    [Test]
    public void Q620_A03_Cancel_EndCheckRuns()
    {
        var s = Field(3, "shambler");
        var key = Single(s);
        SetLeft(s, key, 0);
        Explore(s, 1, 2);
        Assert.That(Step(s), Is.EqualTo(SegmentStatus.Running));
        Assert.That(s.Battle, Is.Null);
        Assert.That(E.Cancel(s).Ok, Is.True);
        Assert.That(s.Battle!.CardKey, Is.EqualTo(key), "合法放弃即结束，检查到期卡");
    }

    [Test]
    public void Q605_A07_Q624_A01_LegacyExplore_CheckOnlyAtCompletion_NoteShown()
    {
        var s = Field(3, "shambler");
        var progress = s.Locations["field"].Progress;
        Explore(s, 0, null);   // 旧档在办：缺检查段
        Assert.That(E.ActionProgress(s)!.LabelNote, Is.EqualTo("本次探索沿原开工规则，在结束时检查随机遭遇。"));
        for (var k = 0; k < 5; k++)
        {
            Assert.That(Step(s), Is.EqualTo(SegmentStatus.Running), "旧在办途中不检查");
            Assert.That(s.Battle, Is.Null);
        }
        Assert.That(Step(s), Is.EqualTo(SegmentStatus.Completed));
        Assert.That(s.Locations["field"].Progress, Is.GreaterThan(progress));
        Assert.That(s.Battle!.ExploreEncounter, Is.True, "原完成边界检查一次");
        WinBattle(s);

        Start(s, ActionRequest.Explore());
        Assert.That(s.Main!.EncounterSegment, Is.Not.Null);
        Assert.That(E.ActionProgress(s)!.LabelNote, Is.Null, "新探索无旧句");
    }

    [Test]
    public void Q605_A07_ReloadMidExplore_KeepsSegmentAndCheckedReceipt()
    {
        var s = Field(3, "shambler");
        var a = Explore(s, 0, 1);
        Assert.That(Step(s), Is.EqualTo(SegmentStatus.Paused));
        WinBattle(s);
        var t = SaveSerializer.Deserialize(SaveSerializer.Serialize(s), CfgV2);
        Assert.That((t.Main!.EncounterSegment, t.Main.EncounterChecked, t.Main.EncounterRoll), Is.EqualTo((1, true, 0.0)), "读档不重抽");
        Assert.That(E.Resume(t).Ok, Is.True);
        Assert.That(Run(t), Is.EqualTo(SegmentStatus.Completed));
        Assert.That(t.Battle, Is.Null, "已检查回执，读档后不再检查");
    }

    [Test]
    public void Schema49To50_LegacyExploreKeepsNullSegment()
    {
        var s = Field(3, "shambler");
        Explore(s, 0.5, null);
        var json = SaveSerializer.Serialize(s).Replace("\"SchemaVersion\": \"51\"", "\"SchemaVersion\": \"49\"").Replace("\"SchemaVersion\":\"51\"", "\"SchemaVersion\":\"49\"");
        Assert.That(json, Does.Contain("\"49\""));
        var t = SaveSerializer.Deserialize(json, CfgV2);
        Assert.That(t.SchemaVersion, Is.EqualTo("51"));
        Assert.That(t.Main!.EncounterSegment, Is.Null);
        Assert.That(GameEngine.LegacyExploreCheck(t.Main), Is.True, "旧在办探索按原完成边界检查");
        Assert.That(t.ExploreCheckPending, Is.Null);
    }
}
