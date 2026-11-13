using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// B34b（R03 文首「2026-11-13 B34主要Q570—Q572」§Q571／§Q572；验收 Q571-A01、Q571-A02、Q572-A04）：
/// 出手事件快照由内核在原事务里抄下，按真实顺序；结果与战报、实际 HP 同源；不进存档。
/// </summary>
public class B34bBattleFxTests : BattleTestBase
{
    private static BattleCommandOption FirstAttack(GameEngine e, GameState s) =>
        e.BattleOptions(s).First(o => o.Group == "attack" && o.Available && o.NeedsTarget && o.Targets.Count > 0);

    [Test]
    public void PlayerAttack_RecordsOneEvent_WithRealResultAndLogLine()
    {
        var s = Fight(11, "shambler");
        ToPlayer(s);
        Assume.That(E.AwaitingPlayer(s), "轮到玩家");
        E.DrainBattleFx();
        var o = FirstAttack(E, s);
        var t = o.Targets[0];
        var hp0 = Enemy(s, t).Hp;
        var logN = s.Battle!.Log.Count;
        Assert.That(Cmd(s, o.Action, t).Ok, Is.True);
        var fx = E.DrainBattleFx().Where(x => !x.Short).ToList();
        Assert.That(fx, Has.Count.GreaterThanOrEqualTo(1));
        var ev = fx[0];
        Assert.That(ev.Actor.IsAlly && ev.Actor.Id == GameState.PlayerId, "行动者是真实出手者");
        Assert.That(ev.Results, Is.Not.Empty);
        Assert.That(ev.Results.All(r => r.Target.Id == "e" + t), "承受者是真实目标");
        var dealt = ev.Results.Where(r => r.Outcome is "hit" or "crit").Sum(r => r.Amount);
        var hp1 = s.Battle?.Enemies.Single(x => x.Index == t).Hp ?? 0;
        if (s.Battle != null && !ev.Results.Any(r => r.Down)) Assert.That(hp0 - hp1, Is.EqualTo(dealt).Within(1e-6), "快照数值等于实际扣血");
        if (s.Battle != null) Assert.That(ev.Lines, Is.SubsetOf(s.Battle.Log.Skip(logN).ToList()), "特写底部的句子就是同一次写下的战报原文");
        Assert.That(E.DrainBattleFx(), Is.Empty, "取走后不再重复（同一结果只播一次）");
    }

    [Test]
    public void Guard_IsShortFeedback_NoNumbers()
    {
        var s = Fight(12, "shambler");
        ToPlayer(s);
        Assume.That(E.AwaitingPlayer(s));
        E.DrainBattleFx();
        Assume.That(E.BattleOptions(s).Any(o => o.Action == "guard" && o.Available));
        Assert.That(Cmd(s, "guard").Ok, Is.True);
        var fx = E.DrainBattleFx();
        Assert.That(fx.Count(x => x.Short), Is.EqualTo(1), "防御只给行动者短反馈");
        Assert.That(fx.Where(x => x.Short).All(x => x.Results.Count == 0 && x.Actor.Id == GameState.PlayerId));
        Assert.That(fx.Any(x => !x.Short && x.Actor.IsAlly), Is.False, "不播特写");
    }

    [Test]
    public void EnemySteps_RecordEnemyActor_InOrder_AndEndTurnNoFx()
    {
        var s = Fight(13, "shambler", "shambler");
        ToPlayer(s);
        Assume.That(E.AwaitingPlayer(s));
        E.DrainBattleFx();
        Assert.That(Cmd(s, "end_turn").Ok, Is.True);
        Assert.That(E.DrainBattleFx(), Is.Empty, "结束行动不播特写");
        var all = new List<BattleFxEvent>();
        for (var i = 0; i < 30 && s.Battle != null && !E.AwaitingPlayer(s) && !s.Progress.Dead; i++)
        {
            E.BattleStep(s);
            all.AddRange(E.DrainBattleFx());
        }
        Assert.That(all.Select(x => x.Seq), Is.Ordered, "按真实发生顺序");
        foreach (var ev in all.Where(x => !x.Short && x.Actor.Side == BattleFxUnit.EnemySide))
            Assert.That(ev.Results.All(r => r.Target.IsAlly), "敌方攻击的承受者是我方");
    }

    [Test]
    public void Fx_NotInSave()
    {
        var s = Fight(14, "shambler");
        ToPlayer(s);
        Assume.That(E.AwaitingPlayer(s));
        var o = FirstAttack(E, s);
        Cmd(s, o.Action, o.Targets[0]);
        var json = SaveSerializer.Serialize(s);
        Assert.That(json, Does.Not.Contain("\"Motion\"").And.Not.Contain("ContinuesSeq"), "快照不进存档");
        E.DrainBattleFx();
    }

    [Test]
    public void Look_Classification()
    {
        var a = new BattleFxUnit { Side = BattleFxUnit.EnemySide, Id = "e1" };
        var c = new BattleFxUnit { Side = BattleFxUnit.EnemySide, Id = "e2" };
        BattleFxEvent Ev(string motion, params BattleFxResult[] rs) { var e = new BattleFxEvent { Motion = motion }; e.Results.AddRange(rs); return e; }
        Assert.That(Ev(BattleFxMotion.Melee, new BattleFxResult { Target = a, Outcome = "miss" }).Look, Is.EqualTo("miss"));
        Assert.That(Ev(BattleFxMotion.Melee, new BattleFxResult { Target = a, Outcome = "hit", Amount = 1, Down = true }).Look, Is.EqualTo("defeat"));
        Assert.That(Ev(BattleFxMotion.Ranged, new BattleFxResult { Target = a, Outcome = "hit", Amount = 1, Guarded = true }).Look, Is.EqualTo("guard"));
        Assert.That(Ev(BattleFxMotion.Ranged, new BattleFxResult { Target = a, Outcome = "hit", Amount = 1 }).Look, Is.EqualTo("ranged"));
        Assert.That(Ev(BattleFxMotion.Melee, new BattleFxResult { Target = a, Outcome = "hit" }, new BattleFxResult { Target = c, Outcome = "miss" }).Look, Is.EqualTo("area"));
        Assert.That(Ev(BattleFxMotion.Melee, new BattleFxResult { Target = a, Outcome = "hit" }, new BattleFxResult { Target = a, Outcome = "miss" }).Look, Is.EqualTo("melee"), "同目标多段仍是一段特写");
        Assert.That(Ev(BattleFxMotion.Heal, new BattleFxResult { Target = a, Outcome = "effect" }).Look, Is.EqualTo("heal"));
    }
}
