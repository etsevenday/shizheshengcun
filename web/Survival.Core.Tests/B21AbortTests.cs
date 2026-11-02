using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 B21 R66：主动终止远征的惩罚包与惊魂未定。出处：R03「2026-10-30 B4」D5「玩家主动终止」、D5.1「单次明确惩罚」、D10；
/// S12「B4 撤出事件包与生存消耗分界」；A01 惊魂未定行；A02 主动包样例；开发假设 B21-6、B21-11。验收 DR04、DR05（前半）、DR08、COMP09。
/// </summary>
public class B21AbortTests : BattleTestBase
{
    private void Go(GameState s, string to, bool confirm = false)
    {
        Start(s, ActionRequest.Travel(to, confirm));
        Assert.That(Run(s), Is.EqualTo(SegmentStatus.Completed));
        Assert.That(s.CurrentLocation, Is.EqualTo(to));
    }

    /// <summary>01→02（打赢）→04（安全点）的进行中远征；recruits 个命名队友先编入同行。</summary>
    internal GameState At04(uint seed, params string[] recruits)
    {
        var s = B21EntryTests.AtBuffer(E, seed);
        UnlockCapacity(s, 4);
        foreach (var r in recruits) E.DebugRecruit(s, r);
        Go(s, "d01_02", true);
        E.DebugWinFixedHere(s);
        E.DebugSetProgress(s, "d01_02", 100);
        Go(s, "d01_04");
        return s;
    }

    private static void Set(ActorState a, double n, double w, double e, double sl, double m, double h)
    {
        a.Stats.Set(StatId.Satiety, n);
        a.Stats.Set(StatId.Hydration, w);
        a.Stats.Set(StatId.Energy, e);
        a.Stats.Set(StatId.Wakefulness, sl);
        a.Stats.Set(StatId.Morale, m);
        a.Stats.Set(StatId.Health, h);
    }

    private static (double, double, double, double, double, double) Six(ActorState a) =>
        (Math.Round(a.Stats.Satiety, 6), Math.Round(a.Stats.Hydration, 6), Math.Round(a.Stats.Energy, 6), Math.Round(a.Stats.Wakefulness, 6), Math.Round(a.Stats.Morale, 6), Math.Round(a.Stats.Health, 6));

    /// <summary>DR04 例 1：N80/W80/E18/S90/M60/H20 → 55/50/10/70/45/18（确认即扣，预估与实扣一致）。</summary>
    [Test]
    public void DR04_Example1_Exact()
    {
        var s = At04(2131);
        Set(s.Player, 80, 80, 18, 90, 60, 20);
        var pv = E.AbortPreview(s);
        Assert.That(pv.Reason, Is.Null);
        Assert.That(pv.Rows.Single().After.Satiety, Is.EqualTo(55));
        Assert.That(pv.Route, Is.EqualTo(new[] { "d01_04", "d01_02", "d01_01", "c03_crypt" }.Select(E.LocName)));
        Assert.That((pv.BaseMinutes, pv.Factor, pv.TotalMinutes), Is.EqualTo((20, 1d, 30)), "A02：20 基础分钟 → T=30");
        Assert.That(E.AbortExpedition(s, "abort-1").Ok, Is.True);
        Assert.That(Six(s.Player), Is.EqualTo((55d, 50d, 10d, 70d, 45d, 18d)));
        Assert.That(s.Player.ShakenUntil, Is.EqualTo(s.GameMinute + 360));
        Assert.That(s.Expedition!.Phase, Is.EqualTo(ExpeditionState.Extracting));
        Assert.That(E.DungeonStatus(s, "d01"), Is.EqualTo(DungeonStatuses.Extracting));
        Assert.That(s.Main!.Type, Is.EqualTo(ActionType.Extract));
        Assert.That(s.Main.PlannedMinutes, Is.EqualTo(30));
        Assert.That(s.AppliedEventKeys, Is.EquivalentTo(new[] { s.Expedition.Id + GameEngine.AbortPenaltyKeySuffix }));
    }

    /// <summary>DR04 例 2：N12/W8/E4/S10/M9/H0.4 → 0/0/0/0/0/0.4（直接惩罚不致死、H0.4 不补到 1）。</summary>
    [Test]
    public void DR04_Example2_LowStats_H04Kept()
    {
        var s = At04(2132);
        Set(s.Player, 12, 8, 4, 10, 9, 0.4);
        Assert.That(E.AbortExpedition(s).Ok, Is.True);
        Assert.That(Six(s.Player), Is.EqualTo((0d, 0d, 0d, 0d, 0d, 0.4d)));
        Assert.That(s.Progress.Dead, Is.False);
    }

    /// <summary>每名存活同行各扣一次（同一包），死去的队友不扣；装备不折减。</summary>
    [Test]
    public void EachLivingMemberOnce_DeadNotCharged()
    {
        var s = At04(2133, "npc_lin_zhao", "npc_gu_cheng");
        Assert.That(s.Expedition!.Members, Has.Count.EqualTo(3));
        foreach (var a in s.PartyActors) Set(a, 80, 80, 18, 90, 60, 20);
        var dead = s.Actor("npc_gu_cheng");
        E.DebugActor(s, "npc_gu_cheng", "kill");
        Assert.That(E.AbortExpedition(s).Ok, Is.True);
        Assert.That(Six(s.Player), Is.EqualTo((55d, 50d, 10d, 70d, 45d, 18d)));
        Assert.That(Six(s.Actor("npc_lin_zhao")), Is.EqualTo((55d, 50d, 10d, 70d, 45d, 18d)));
        Assert.That(dead.Stats.Satiety, Is.EqualTo(80), "死人不扣");
        Assert.That(dead.ShakenUntil, Is.Null);
        Assert.That(s.Actor("npc_lin_zhao").ShakenUntil, Is.EqualTo(s.GameMinute + 360));
    }

    /// <summary>DR05 前半：双击同 requestId / 换 requestId 再提交 / 读档后再提交，都只扣一次；撤出不能取消（COMP09）。</summary>
    [Test]
    public void DR05_DoubleConfirm_Reload_OnlyOnce_NotCancelable()
    {
        var s = At04(2134);
        Set(s.Player, 80, 80, 18, 90, 60, 20);
        Assert.That(E.AbortExpedition(s, "x").Ok, Is.True);
        var after = Six(s.Player);
        Assert.That(E.AbortExpedition(s, "x").Duplicate, Is.True);
        Assert.That(E.AbortExpedition(s, "y").Ok, Is.False);
        Assert.That(Six(s.Player), Is.EqualTo(after));
        var t = SaveSerializer.Deserialize(SaveSerializer.Serialize(s), Cfg);
        Assert.That(t.Main!.Type, Is.EqualTo(ActionType.Extract));
        Assert.That(t.Expedition!.IsExtracting, Is.True);
        Assert.That(E.AbortExpedition(t, "z").Ok, Is.False);
        Assert.That(Six(t.Player), Is.EqualTo(after), "读档不重扣");
        Assert.That(E.Cancel(t).Ok, Is.False, "撤出不可取消");
        Assert.That(E.Cancel(t).Reason, Does.Contain("不能取消"));
        Assert.That(E.CanStart(t, ActionRequest.Explore()), Does.Contain("正在撤出"));
    }

    /// <summary>D5：战斗中只解释（先普通撤退）；01 未开始远征提示可免费原路离开；已通关提示直接离开；有行动时拒绝。</summary>
    [Test]
    public void CanAbort_Reasons()
    {
        var s = B21EntryTests.AtBuffer(E, 2135);
        Assert.That(E.CanAbortExpedition(s), Does.Contain("没有开始远征").And.Contain("不扣惩罚"));
        UnlockCapacity(s, 2);
        Go(s, "d01_02", true);
        Assert.That(E.CanAbortExpedition(s), Is.Null, "远征里任何房间都可发起（先看同一预估）");
        Assert.That(E.EngageFixed(s).Ok, Is.True);
        Assert.That(E.CanAbortExpedition(s), Does.Contain("先在战斗里撤退"));
        Assert.That(E.AbortExpedition(s).Ok, Is.False);
        s.Battle = null;
        Start(s, ActionRequest.Wait(30));
        Assert.That(E.CanAbortExpedition(s), Does.Contain("行动进行中"));
        E.Cancel(s);
        E.DebugInstanceFact(s, "d01", GameEngine.RecordClaimedFact);
        Assert.That(E.CanAbortExpedition(s), Does.Contain("已通关"));
    }

    /// <summary>
    /// DR08：两次远征各扣一次包；惊魂未定不叠层，截止取 max(原截止, 现在+360)；截止前第一次入战恐慌初值 +10，下一场不再加（B21-6）。
    /// </summary>
    [Test]
    public void DR08_TwoAborts_ShakenMaxNotStacked_PanicOnce()
    {
        var s = At04(2136);
        Set(s.Player, 90, 90, 20, 95, 90, 20);
        Assert.That(E.AbortExpedition(s).Ok, Is.True);
        var first = s.Player.ShakenUntil!.Value;
        Assert.That(Run(s), Is.EqualTo(SegmentStatus.Completed));
        Assert.That(s.CurrentLocation, Is.EqualTo("c03_crypt"));
        Assert.That(s.Expedition, Is.Null);
        Assert.That(s.ExpeditionLog.Last().EndKind, Is.EqualTo("abort"));
        Assert.That(E.DungeonStatus(s, "d01"), Is.EqualTo(DungeonStatuses.Active));
        Assert.That(E.ActiveConditions(s).Any(c => c.Id == "shaken"), Is.True);

        // 第二次远征（同实例，新 expeditionId），30 分钟后再终止
        Go(s, "d01_01");
        Go(s, "d01_02", true);
        Set(s.Player, 90, 90, 20, 95, 90, 20);
        Assert.That(E.AbortExpedition(s).Ok, Is.True);
        Assert.That(s.Player.Stats.Satiety, Is.EqualTo(65), "新远征再扣一次");
        Assert.That(s.AppliedEventKeys, Has.Count.EqualTo(2));
        Assert.That(s.Player.ShakenUntil, Is.EqualTo(s.GameMinute + 360), "截止延后到最近+6小时");
        Assert.That(s.Player.ShakenUntil - first, Is.LessThan(360), "不叠成 12 小时");
        Assert.That(Run(s), Is.EqualTo(SegmentStatus.Completed));

        // 再进去打一场（d01_03 的固定战）：恐慌初值 +10 只这一次
        Go(s, "d01_01");
        Go(s, "d01_03", true);
        Assert.That(s.Player.ShakenUntil, Is.GreaterThan(s.GameMinute));
        var m = s.Player.Stats.Morale;
        Assert.That(E.EngageFixed(s).Ok, Is.True);
        var b = s.Battle!;
        var expect = Math.Min(100, (Cfg.Panic.InitialBase - m) / Cfg.Panic.InitialDivisor + b.FearPanic + 10);
        Assert.That(b.Fighter(GameState.PlayerId).Panic, Is.EqualTo(expect).Within(1e-9));
        Assert.That(s.Player.ShakenUntil, Is.Null, "用掉即清");
        Assert.That(b.Log.Any(l => l.Contains("惊魂未定")), Is.True, string.Join(" / ", b.Log));
        s.Battle = null;
        foreach (var w in s.WorldEnemies) w.Engaged = false;
        var m2 = s.Player.Stats.Morale;
        Assert.That(E.EngageFixed(s).Ok, Is.True);
        var b2 = s.Battle!;
        Assert.That(b2.Fighter(GameState.PlayerId).Panic, Is.EqualTo(Math.Min(100, (Cfg.Panic.InitialBase - m2) / Cfg.Panic.InitialDivisor + b2.FearPanic)).Within(1e-9), "第二场不再 +10");
    }
}
