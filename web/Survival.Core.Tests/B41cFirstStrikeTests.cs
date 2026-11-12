using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// B41c 先手加成（R05「Q422 首次出手与受惊」、R03；逐字稿 B41「先手说明与攻击预览」；验收 Q342-A04、Q342-A05、Q426—Q428 先手部分）。
/// </summary>
public class B41cFirstStrikeTests : B41cTestBase
{
    private GameState SneakWon(int capacity = 1)
    {
        var (s, key) = OneShambler(capacity);
        Assert.That(E.StartStealth(s, key, "sneak").Ok, Is.True);
        AdvanceUntil(s, t => t.Battle?.FirstStrike == "ally");
        return s;
    }

    private GameState EnemyApproached()
    {
        var (s, key) = OneShambler();
        SetLeft(s, key, 0);
        Assert.That(E.CheckEnemyApproach(s), Is.True);
        return s;
    }

    private void EnemyAttack(GameState s, BattleEnemy e) =>
        typeof(GameEngine).GetMethod("EnemyAttack", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .Invoke(E, new object[] { s, e, Cfg.Enemies.Single(x => x.Id == e.EnemyId), false });

    [Test]
    public void AllyFirstStrike_LogAndRound1Speed_QueueUsesBoost()
    {
        var s = SneakWon();
        var b = s.Battle!;
        Assert.That(b.Log, Has.Some.EndsWith(" · 我方获得先手。每名开场参战者首次攻击伤害+50%，第一回合速度+50%。"));
        Assert.That(b.FirstStrikeDamage, Is.EquivalentTo(b.Members));
        Assert.That(b.Round, Is.EqualTo(1));
        var boosted = E.PlayerSpeedParts(s, b, null, 1.5).Speed;
        Assert.That(boosted, Is.EqualTo(E.QuantizeSpeed(E.PlayerSpeedParts(s, b).Speed * 1.5)).Within(0.011));
        var playerPos = b.Queue.IndexOf(b.Queue.First(i => i <= 0));
        Assert.That(b.Initiative[playerPos], Is.EqualTo(boosted), "第一回合行动队列用×1.5 后量化的速度");
        var e = b.Enemies.Single();
        Assert.That(b.Initiative[b.Queue.IndexOf(e.Index)], Is.EqualTo(E.EnemyEffectiveSpeed(e)), "敌方不加");
    }

    [Test]
    public void EnemyFirstStrike_FromEnemyApproach_SpeedAndDamageConsumedOnFirstAttack()
    {
        var s = EnemyApproached();
        var b = s.Battle!;
        Assert.That(b.FirstStrike, Is.EqualTo("enemy"));
        Assert.That(b.Log, Has.Some.EndsWith(" · 敌方获得先手。每名开场参战者首次攻击伤害+50%，第一回合速度+50%。"));
        var e = b.Enemies.Single();
        Assert.That(b.Initiative[b.Queue.IndexOf(e.Index)], Is.EqualTo(E.EnemySpeedWithFirstStrike(e, 1.5)));
        // 同一随机序列：有先手的这一击伤害是无先手的 1.5 倍（主角无护甲）
        Force(s, Draw(), Below(0.01), Any());
        var noFs = SaveSerializer.Deserialize(SaveSerializer.Serialize(s), CfgV2);
        noFs.Battle!.FirstStrikeDamage!.Clear();
        var hp0 = s.Stats.Health;
        EnemyAttack(s, s.Battle!.Enemies.Single());
        EnemyAttack(noFs, noFs.Battle!.Enemies.Single());
        var d1 = hp0 - s.Stats.Health;
        var d2 = hp0 - noFs.Stats.Health;
        Assert.That(d2, Is.GreaterThan(0));
        Assert.That(d1, Is.EqualTo(d2 * 1.5).Within(1e-6));
        Assert.That(s.Battle!.FirstStrikeDamage, Is.Empty, "实际首次攻击后消耗");
    }

    [Test]
    public void EnemyFirstStrike_MissStillConsumes()
    {
        var s = EnemyApproached();
        Force(s, Draw(), AtLeast(0.9999));
        EnemyAttack(s, s.Battle!.Enemies.Single());
        Assert.That(s.Battle!.FirstStrikeDamage, Is.Empty, "未命中也消耗");
    }

    [Test]
    public void AllyFirstStrike_PreviewTimes15_NoteVerbatim_ConsumedAfterCommand()
    {
        var s = SneakWon();
        ToPlayer(s);
        if (s.Battle == null) Assert.Inconclusive("战斗已结束");
        var idx = s.Battle!.Enemies.Single().Index;
        var with = E.PreviewCommand(s, BattleCommand.Of("basic_attack", idx));
        var noFs = SaveSerializer.Deserialize(SaveSerializer.Serialize(s), CfgV2);
        noFs.Battle!.FirstStrikeDamage!.Clear();
        var without = E.PreviewCommand(noFs, BattleCommand.Of("basic_attack", idx));
        Assert.That(with.Targets[0].Multiplier, Is.EqualTo(without.Targets[0].Multiplier * 1.5).Within(1e-9), "独立×1.5，不进增益递减");
        Assert.That(with.Notes, Does.Contain("本次攻击伤害×1.5，出手后消耗先手加成。"));
        Assert.That(without.Notes, Has.None.Contains("先手"));
        Assert.That(s.Battle.FirstStrikeDamage, Does.Contain(GameState.PlayerId), "预览不消耗");
        Assert.That(Cmd(s, "basic_attack", idx).Ok, Is.True);
        Assert.That(s.Battle?.FirstStrikeDamage ?? new List<string>(), Does.Not.Contain(GameState.PlayerId), "提交后消耗（未命中、0 伤害也算）");
    }

    [Test]
    public void Round2_NoSpeedBoost()
    {
        var s = SneakWon();
        var b = s.Battle!;
        b.Round = 2;
        var e = b.Enemies.Single();
        Assert.That(E.PlayerSpeedParts(s, b, null, 1).Speed, Is.EqualTo(E.PlayerEffectiveSpeed(s, b)));
        var m = typeof(GameEngine).GetMethod("FirstStrikeSpeedFactor", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        Assert.That((double)m.Invoke(E, new object[] { b, "ally" })!, Is.EqualTo(1.0), "第二回合恢复原速度公式");
    }

    [Test]
    public void ActiveApproach_NoFirstStrike_SwitchOffUnchanged()
    {
        var (s, key) = OneShambler();
        Assert.That(E.ApproachCard(s, key).Ok, Is.True);
        Assert.That(s.Battle!.FirstStrike, Is.Null, "主动接近没有先手");
        Assert.That(s.Battle.FirstStrikeDamage, Is.Null);
        var o = Fight(3, "shambler");
        Assert.That(o.Battle!.FirstStrike, Is.Null);
    }
}
