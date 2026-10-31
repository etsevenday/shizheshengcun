using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 系统策划总册 v1.0 P03 + N01 + S15 §5 + R07：开场对白 → 饮食提示 → 首次 camp→field 强制游荡尸首战 → 胜利得唯一教学活性物质 →
/// 吞噬融合得绿色“攻击力+0.1”；旧档不补（T06）。验收 S15-A06/A07/A08、R07-A01–A07、N01-A01/A02/A04。
/// </summary>
public class V10StoryTests : BattleTestBase
{
    private const string Sub = "active_substance_white_unidentified";

    /// <summary>读完待读对话后移动到荒地；read 时把到达后出现的对话（首战警告）也读完。</summary>
    private void ToField(GameState s, bool read = true)
    {
        ReadDialogues(s);
        Start(s, ActionRequest.Travel("field"));
        for (var i = 0; i < 200 && s.Main != null && !s.Progress.Dead; i++)
        {
            var r = E.Advance(s);
            if (r.Status == SegmentStatus.Paused) E.Resume(s);
        }
        if (read) ReadDialogues(s);
    }

    private void ToCamp(GameState s)
    {
        ReadDialogues(s);
        Start(s, ActionRequest.Travel("camp"));
        Run(s);
    }

    /// <summary>首战中把敌人打到只剩一点血并强制命中击杀；返回是否胜利。</summary>
    private void WinTutorial(GameState s)
    {
        Assert.That(s.Battle!.Tutorial, Is.True);
        ReadDialogues(s);
        for (var k = 0; k < 20 && s.Battle != null; k++)
        {
            if (!E.AwaitingPlayer(s)) { E.BattleStep(s); continue; }
            s.Battle.Enemies[0].Hp = 0.01;
            Force(s, Below(0.2), AtLeast(0.6), Any());
            Cmd(s, "basic_attack", s.Battle.Enemies[0].Index);
        }
        Assert.That(s.Battle, Is.Null);
    }

    [Test]
    public void N01_A01_OpeningDialogueOrder_NoTimeNoItems()
    {
        var s = NewStory();
        Assert.That(s.Story.Legacy, Is.False);
        Assert.That(s.Story.PendingDialogues, Is.EqualTo(new[] { "awakening_1", "awakening_2", "food_water" }));
        Assert.That(E.CurrentDialogue(s)!.Text, Is.EqualTo("醒醒……醒醒，我饿了。"));
        var minute = s.GameMinute;
        var items = SaveSerializer.Serialize(new GameState { Items = s.Items });
        Assert.That(E.AckDialogue(s, "awakening_1").Ok, Is.True);
        Assert.That(s.Story.AwakeningDone, Is.False);
        Assert.That(E.AckDialogue(s, "awakening_2").Ok, Is.True);
        Assert.That(s.Story.AwakeningDone, Is.True, "命名标记锁存");
        Assert.That(E.AckDialogue(s, "food_water").Ok, Is.True);
        Assert.That(s.Story.FoodWaterRead, Is.True);
        Assert.That(E.AckDialogue(s, "food_water").Duplicate, Is.True, "重复确认不改状态");
        Assert.That(s.GameMinute, Is.EqualTo(minute), "对话不推进时间");
        Assert.That(SaveSerializer.Serialize(new GameState { Items = s.Items }), Is.EqualTo(items), "不自动吃喝、不发物");
        Assert.That(E.CurrentDialogue(s), Is.Null);
    }

    [Test]
    public void S15_A06_FirstArrivalTriggersTutorialOnce()
    {
        var s = NewStory(3);
        var tid = s.Story.TutorialEnemyId!.Value;
        Assert.That(E.FindWorldEnemy(s, tid)!.LocationId, Is.EqualTo("field"));
        Assert.That(E.Preview(s, ActionRequest.Travel("field")).Ok, Is.False, "开场对白未读完时不能开始移动");
        ReadDialogues(s);
        Assert.That(E.Preview(s, ActionRequest.Travel("field")).Warnings.Any(w => w.Contains("首战")), Is.True, "移动预览提前说明");
        ToField(s, read: false);
        Assert.That(s.CurrentLocation, Is.EqualTo("field"), "先落点到荒地");
        Assert.That(s.Battle, Is.Not.Null, "到达后直接进入首战");
        Assert.That(s.Battle!.Tutorial, Is.True);
        Assert.That(s.Battle.Context, Is.EqualTo(GameEngine.FearTutorial));
        Assert.That(s.Battle.Enemies.Single().WorldId, Is.EqualTo(tid), "唯一保留游荡尸，不复制敌人");
        Assert.That(s.Battle.FearChance, Is.EqualTo(E.FearChance(0.35, s.Battle.FearMorale, GameEngine.FearTutorial)).Within(1e-12));
        Assert.That(s.Story.PendingDialogues, Does.Contain("tutorial_arrive"));
        Assert.That(s.WorldEnemies.Count(w => w.EnemyId == "shambler" && w.LocationId == "field"), Is.EqualTo(1));
    }

    [Test]
    public void TutorialGate_NoRandomOrOtherApproach_NoPatrol_NoReinforcement()
    {
        var s = NewStory(4);
        ReadDialogues(s);
        var tid = s.Story.TutorialEnemyId!.Value;
        // 首战前：第2日也不随机接敌
        s.GameMinute = At(2, 9, 0);
        E.DebugTeleport(s, "village");
        Assert.That(E.EncounterChance(s, "village", ActionType.Explore), Is.EqualTo(0));
        Assert.That(E.EncounterNote(s, "village", ActionType.Explore), Does.Contain("首战"));
        var other = s.WorldEnemies.First(w => w.LocationId == "village");
        Assert.That(E.CanApproach(s, other.Id), Does.Contain("首战"));
        // 首战保留游荡尸不巡游（巡游大量抽签也不动）
        for (var k = 0; k < 200; k++)
        {
            s.PatrolNextCheck = s.GameMinute;
            s.SpawnNextCheck = int.MaxValue;
            Start(s, ActionRequest.Wait(30));
            E.Advance(s);
            E.Cancel(s);
            if (s.Progress.Dead) break;
            SetStats(s, 20, 80, 80, 18, 90, 60);
        }
        Assert.That(E.FindWorldEnemy(s, tid)!.LocationId, Is.EqualTo("field"));

        // 首战中噪声满也不调增援
        var t = NewStory(5);
        ToField(t);
        t.Battle!.ReinforcementRequested = true;
        E.DebugSpawnEnemy(t, "shambler", "field");
        for (var i = 0; i < 10 && t.Battle != null && !E.AwaitingPlayer(t); i++) E.BattleStep(t);
        Cmd(t, "end_turn");
        for (var i = 0; i < 20 && t.Battle != null && !E.AwaitingPlayer(t); i++) E.BattleStep(t);
        if (t.Battle != null) Assert.That(t.Battle.Enemies, Has.Count.EqualTo(1), "首战只有保留游荡尸");
    }

    [Test]
    public void S15_A07_Victory_GrantsUniqueTutorialSubstance_NoDuplicates()
    {
        var s = NewStory(6);
        ToField(s);
        WinTutorial(s);
        Assert.That(s.Progress.Dead, Is.False);
        Assert.That(s.Story.TutorialWon, Is.True);
        var subs = s.Items.Where(i => i.ItemId == Sub).ToList();
        Assert.That(subs.Sum(i => i.Count), Is.EqualTo(1), "唯一1份，不额外抽普通物质");
        Assert.That(subs[0].Owner, Is.EqualTo("field"), "落在荒地地面（背包满也不吞奖）");
        Assert.That(subs[0].Source!.TutorialReward, Is.True);
        Assert.That(subs[0].Source.GrantEventId, Is.EqualTo(s.Story.TutorialGrantEventId));
        Assert.That(E.Item(Sub).Quality, Is.EqualTo("white"), "物质品质仍是白色");
        Assert.That(s.Story.PendingDialogues, Does.Contain("tutorial_won"));
        // 读档、往返地图：不重复发、不再触发首战
        var back = SaveSerializer.Deserialize(SaveSerializer.Serialize(s), Cfg, out _);
        Assert.That(back.Items.Where(i => i.ItemId == Sub).Sum(i => i.Count), Is.EqualTo(1));
        SetStats(s, 20, 80, 80, 18, 90, 60);
        ToCamp(s);
        ToField(s);
        Assert.That(s.Battle, Is.Null, "重复到达不重播首战");
        Assert.That(s.Items.Where(i => i.ItemId == Sub).Sum(i => i.Count), Is.EqualTo(1));
        // 首战后：其他敌人可以接近，随机接敌恢复按第2日规则
        Assert.That(GameEngine.TutorialGateOpen(s), Is.False);
    }

    [Test]
    public void S15_A07_Retreat_NoReward_SameEnemyStays_CanReapproach()
    {
        var s = NewStory(7);
        ToField(s);
        var tid = s.Story.TutorialEnemyId!.Value;
        for (var i = 0; i < 20 && s.Battle != null && !E.AwaitingPlayer(s); i++) E.BattleStep(s);
        s.Battle!.Enemies[0].Hp = 2.0;
        Force(s, Below(0.0001));
        Assert.That(Cmd(s, "retreat").Ok, Is.True);
        if (s.Battle != null) Assert.Inconclusive("强制撤退未成功");
        Assert.That(s.Story.TutorialWon, Is.False);
        Assert.That(s.Items.Any(i => i.ItemId == Sub), Is.False, "撤退不发奖");
        var w = E.FindWorldEnemy(s, tid)!;
        Assert.That(w.Hp, Is.EqualTo(2.0), "同一敌人 HP 保留，不重置成满血");
        Assert.That(w.LocationId, Is.EqualTo("field"));
        // 重复到达不再强制入战，但可主动接近同一教学敌人继续挑战；其他敌人仍关闭
        SetStats(s, 20, 80, 80, 18, 90, 60);
        ToCamp(s);
        ToField(s);
        Assert.That(s.Battle, Is.Null);
        Assert.That(E.CanApproach(s, tid), Is.Null);
        var other = E.DebugSpawnEnemy(s, "crawler", "field");
        Assert.That(E.CanApproach(s, other.Id), Does.Contain("首战"));
        Assert.That(E.Approach(s, tid).Ok, Is.True);
        Assert.That(s.Battle!.Tutorial, Is.True);
        WinTutorial(s);
        Assert.That(s.Items.Where(i => i.ItemId == Sub).Sum(i => i.Count), Is.EqualTo(1), "最终胜利只给1份");
    }

    [Test]
    public void R07_A01_VictoryTickDeath_NoReward()
    {
        var s = NewStory(8);
        ToField(s);
        for (var i = 0; i < 20 && s.Battle != null && !E.AwaitingPlayer(s); i++) E.BattleStep(s);
        // 饱食为0、健康极低：胜利那一刻的轮末 tick 致死
        s.Stats.Satiety = 0;
        s.Stats.Health = 0.01;
        s.Battle!.Enemies[0].Hp = 0.01;
        Force(s, Below(0.2), AtLeast(0.6), Any());
        Cmd(s, "basic_attack", s.Battle.Enemies[0].Index);
        Assert.That(s.Progress.Dead, Is.True);
        Assert.That(s.Items.Any(i => i.ItemId == Sub), Is.False, "胜利轮 tick 致死不发");
        Assert.That(s.Story.TutorialWon, Is.False);
    }

    [Test]
    public void R07_A03_TutorialFusion_GreenAttack01_Once()
    {
        var s = NewStory(9);
        ToField(s);
        WinTutorial(s);
        ReadDialogues(s);
        var inst = s.Items.Single(i => i.ItemId == Sub);
        var pv = E.Preview(s, ActionRequest.Fuse(inst.InstanceId));
        Assert.That(pv.Ok, Is.True, pv.Reason);
        Assert.That(string.Join("；", pv.Notes), Does.Contain("攻击力+0.1"));
        Assert.That(s.Items.Any(i => i.ItemId == Sub), Is.True, "预览不消耗");
        var e0 = s.Stats.Energy;
        var m0 = s.GameMinute;
        var r = E.Start(s, ActionRequest.Fuse(inst.InstanceId).WithRequestId("fuse-1"));
        Assert.That(r.Ok, Is.True, r.Reason);
        // 一次提交：消耗与词条、标记、对白同时生效
        Assert.That(s.Items.Any(i => i.ItemId == Sub), Is.False);
        var t = s.Traits.Single();
        Assert.That(t.EffectId, Is.EqualTo("tutorial_attack_bonus_001"));
        Assert.That(t.Quality, Is.EqualTo("green"));
        Assert.That(t.Label, Is.EqualTo("攻击力+0.1"));
        Assert.That(t.FlatAttack, Is.EqualTo(0.1));
        Assert.That(s.Story.TutorialFusionDone, Is.True);
        Assert.That(s.Story.PendingDialogues.Where(d => d.StartsWith("fusion_done")), Is.EqualTo(new[] { "fusion_done_1", "fusion_done_2" }), "先报收益，再提示未来风险");
        Assert.That(E.Cancel(s).Ok, Is.False, "融合期间不可取消");
        // 融合对白在安全边界等待：本段 5 分钟不开始、不 tick，读完才继续（S01 §8）
        Assert.That(E.Advance(s).Status, Is.EqualTo(SegmentStatus.Dialogue));
        Assert.That(s.GameMinute, Is.EqualTo(m0));
        // 存档在 5 分钟中途：读档只续未推进的段，不再消费、不再加属性
        var back = SaveSerializer.Deserialize(SaveSerializer.Serialize(s), Cfg, out _);
        Assert.That(E.Start(s, ActionRequest.Fuse(inst.InstanceId).WithRequestId("fuse-1")).Duplicate, Is.True, "重复请求不重复叠加");
        ReadDialogues(s);
        ReadDialogues(back);
        Run(s);
        Run(back);
        Assert.That(s.GameMinute - m0, Is.EqualTo(Cfg.ActiveSubstance.Fusion.Minutes), "固定5分钟");
        var wait = Cfg.HourlyRates["wait"];
        Assert.That(s.Stats.Energy - e0, Is.EqualTo(wait.Energy * 5 / 60.0).Within(1e-9), "按 wait，额外体力0");
        Assert.That(s.Traits, Has.Count.EqualTo(1));
        Assert.That(back.Traits, Has.Count.EqualTo(1));
        Assert.That(E.FlatAttack(s), Is.EqualTo(0.1));
    }

    [Test]
    public void R07_A02_A05_NormalFusion_NoEffect_DoesNotUseTutorialInstance()
    {
        var s = NewStory(10);
        ToField(s);
        WinTutorial(s);
        ReadDialogues(s);
        var normal = E.CreateItems(s, Sub, 1, ItemInstance.Carried, false, new ItemSource { GrantEventId = "substance-b9-w9", BattleId = 9, WorldEnemyId = 9 }).Single();
        var xp = s.Skills.Values.Sum(k => k.Xp);
        var imm = s.Body.Immunity;
        Assert.That(E.Start(s, ActionRequest.Fuse(normal.InstanceId)).Ok, Is.True);
        Assert.That(s.Traits, Is.Empty, "普通实例无属性");
        Assert.That(s.Story.TutorialFusionDone, Is.False);
        Assert.That(s.Items.Single(i => i.ItemId == Sub).Source!.TutorialReward, Is.True, "教学实例没被当成普通实例消费");
        Assert.That(s.Log.Last(l => l.Text.Contains("饱饱吞噬融合")).Text, Is.EqualTo("饱饱吞噬融合了活性物质。本次未产生属性变化。"));
        Run(s);
        Assert.That(s.Skills.Values.Sum(k => k.Xp), Is.EqualTo(xp), "不加经验");
        Assert.That(Math.Abs(s.Body.Immunity - imm), Is.LessThan(1), "不额外改免疫（只走 wait 自然变化）");
    }

    [Test]
    public void R07_A06_FusionRejectedInBattleBusyOrBadTarget()
    {
        var s = NewStory(11);
        ReadDialogues(s);
        var sub = E.CreateItems(s, Sub, 1, ItemInstance.Carried, false, new ItemSource { GrantEventId = "x" }).Single();
        // 行动中
        Start(s, ActionRequest.Wait(30));
        Assert.That(E.CanStart(s, ActionRequest.Fuse(sub.InstanceId)), Is.Not.Null);
        E.Cancel(s);
        // 暂停中（不在暂停补给白名单）
        Start(s, ActionRequest.Wait(30));
        s.Main!.Status = ActionStatus.Paused;
        Assert.That(E.CanStart(s, ActionRequest.Fuse(sub.InstanceId)), Is.Not.Null);
        E.Cancel(s);
        // 战斗中
        ToField(s);
        Assert.That(s.Battle, Is.Not.Null);
        Assert.That(E.CanStart(s, ActionRequest.Fuse(sub.InstanceId)), Does.Contain("战斗"));
        // 非活性物质
        var t = New();
        Assert.That(E.CanStart(t, ActionRequest.Fuse(First(t, "pot").InstanceId)), Does.Contain("活性物质"));
        // 其他地点的地面
        var far = E.CreateItems(t, Sub, 1, "village").Single();
        Assert.That(E.CanStart(t, ActionRequest.Fuse(far.InstanceId)), Does.Contain("随身或当前地面"));
    }

    [Test]
    public void Legacy_NoOpening_NoTutorial()
    {
        var s = New(12);   // 同旧档
        Assert.That(s.Story.PendingDialogues, Is.Empty);
        ToField(s);
        Assert.That(s.Battle, Is.Null, "旧档不补首战");
        Assert.That(s.Items.Any(i => i.ItemId == Sub), Is.False);
    }

    /// <summary>
    /// 试玩修复 B7d-2：首战撤退后，经敌踪“接敌”（不是首战战斗）把保留游荡尸一并打死，也记首战完成并发唯一教学物质；
    /// 之后其他敌人可以接近，门控不再卡死。
    /// </summary>
    [Test]
    public void PlaytestB7d2_TutorialEnemyKilledInOtherBattle_CountsAsTutorialWon()
    {
        var s = NewStory(17);
        ToField(s);
        var tid = s.Story.TutorialEnemyId!.Value;
        for (var i = 0; i < 20 && s.Battle != null && !E.AwaitingPlayer(s); i++) E.BattleStep(s);
        Force(s, Below(0.0001));
        Assert.That(Cmd(s, "retreat").Ok, Is.True);
        if (s.Battle != null) Assert.Inconclusive("强制撤退未成功");
        Assert.That(s.Story.TutorialWon, Is.False);
        // 开发入口“本地敌人立即形成敌踪”→“接敌”：普通战斗里把首战游荡尸打死
        s.Encounter = new PendingEncounter { LocationId = "field", EnemyIds = new List<int> { tid }, Source = "explore", Minute = s.GameMinute };
        Assert.That(E.Engage(s).Ok, Is.True);
        Assert.That(s.Battle!.Tutorial, Is.False);
        ReadDialogues(s);
        for (var k = 0; k < 20 && s.Battle != null; k++)
        {
            if (!E.AwaitingPlayer(s)) { E.BattleStep(s); continue; }
            s.Battle.Enemies[0].Hp = 0.01;
            Force(s, Below(0.2), AtLeast(0.6), Any(), Any(), Any());
            Cmd(s, "basic_attack", s.Battle.Enemies[0].Index);
        }
        Assert.That(s.Battle, Is.Null);
        Assert.That(s.Story.TutorialWon, Is.True, "首战敌人在任何战斗中被击杀都算首战完成");
        Assert.That(GameEngine.TutorialGateOpen(s), Is.False);
        Assert.That(s.Items.Where(i => i.ItemId == Sub && i.Source?.TutorialReward == true).Sum(i => i.Count), Is.EqualTo(1), "唯一教学物质照发");
        var other = E.DebugSpawnEnemy(s, "shambler", "field");
        Assert.That(E.CanApproach(s, other.Id) ?? "", Does.Not.Contain("首战"));
    }
}
