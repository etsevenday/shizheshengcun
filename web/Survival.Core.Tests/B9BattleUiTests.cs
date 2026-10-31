using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 批次 B9（策划提交 f172677，需求 R28 全屏四对四战斗界面）的内核部分：界面读到的预测与实际结算同源。
/// 出处：A10 §4.1“目标重定向时预测同时写‘瞄准A，实际由B承受’”、§4.5“预测只读且与实际同源”；验收 UI-E05、UI-E10。
/// </summary>
public class B9BattleUiTests : BattleTestBase
{
    private (GameState s, BattleEnemy guard, BattleEnemy back) Protected()
    {
        var s = Fight(744, "z_guard", "shambler");
        PlayerFirst(s);
        E.BattleStep(s);
        Assert.That(E.AwaitingPlayer(s), Is.True);
        var guard = Enemy(s, 1);
        var back = Enemy(s, 2);
        guard.ProtectingIndex = back.Index;
        Arm(s, "stone_knife");   // 刀打 1、2 号位
        s.Battle!.PlayerAp = 3;
        return (s, guard, back);
    }

    /// <summary>UI-E05/UI-E10：瞄准被保护者，预测改按真实承伤者算并写明“瞄准A，实际由B承受”；执行时确实由保护者接下，伤害落在预测区间内。</summary>
    [Test]
    public void Protect_PreviewShowsRealReceiver_SameAsExecution()
    {
        var (s, guard, back) = Protected();
        var p = E.PreviewCommand(s, BattleCommand.Of("basic_attack", back.Index));
        Assert.That(p.Ok, Is.True, p.Reason);
        Assert.That(p.Targets.Select(t => t.Index), Is.EqualTo(new[] { guard.Index }), "预测目标是真实承伤者");
        Assert.That(p.Targets[0].AimedIndex, Is.EqualTo(back.Index));
        Assert.That(p.Notes, Has.Some.Contains($"瞄准2号位游荡尸，实际由1号位{E.EnemyName(guard)}承受"));
        // 预览只读：不抽随机、不改保护
        Assert.That(guard.ProtectingIndex, Is.EqualTo(back.Index));

        var hpGuard = guard.Hp;
        var hpBack = back.Hp;
        var (min, max) = (p.Targets[0].DamageMin, p.Targets[0].DamageMax);
        Force(s, Below(0.01), AtLeast(0.99), Any());   // 命中、不暴击
        Assert.That(Cmd(s, "basic_attack", back.Index).Ok, Is.True);
        Assert.That(back.Hp, Is.EqualTo(hpBack), "被保护者没受伤");
        var dealt = hpGuard - guard.Hp;
        Assert.That(dealt, Is.InRange(min - 1e-9, max + 1e-9), "实际伤害在预测区间内");
    }

    /// <summary>没有保护时预测不带重定向标记（AimedIndex 为空、没有“实际由”说明）。</summary>
    [Test]
    public void NoProtect_NoRedirectMark()
    {
        var (s, guard, back) = Protected();
        guard.ProtectingIndex = 0;
        var p = E.PreviewCommand(s, BattleCommand.Of("basic_attack", back.Index));
        Assert.That(p.Targets.Single().Index, Is.EqualTo(back.Index));
        Assert.That(p.Targets.Single().AimedIndex, Is.Null);
        Assert.That(p.Notes, Has.None.Contains("实际由"));
    }
}
