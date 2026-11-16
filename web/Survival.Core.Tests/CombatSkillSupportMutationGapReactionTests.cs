using System.Reflection;
using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 变异测试补测（全量基线 10da301d）：GameEngine.CombatSkillSupport.cs —— 反应攻击（反击、迎击）。
/// 出处：R03「Q75—Q82」§贯穿与反应（来源页武器单段、当前己位/射程重验、不另付 E/弹/耐久、不受益/不消耗鼓舞总攻令斧蓄力、
/// 命中含护甲挡成0可触发反击一次、无合法目标不白扣）与 §完整伤害层次（G=1+0.035L、技能倍率、暴击×1.5、单段伤害=max(0,抽值×G×S×B×C+平加−max(0,护甲−穿透))再乘格挡）；
/// R03 格挡/防御“下一次直接伤害×0.5……先触发即消耗”与 Q106“被甲完全挡成0时不消费格挡”；R05 Q422（反击/迎击未命中也消耗先手资格）；
/// A05 守备短剑“反击×1.15”、词条“破甲：忽略护甲0.10”；A09 敌人面板（厚衣工尸 H5.6/甲0.3、防护尸甲0.7、灰狼 H≤1.2 下次逃离）；
/// 开发计划 v0.9 B7-15。
/// </summary>
public class CombatSkillSupportMutationGapReactionTests : BattleTestBase
{
    protected override bool NoEnemyApproach => true;

    private const string Me = GameState.PlayerId;

    private void Ready(GameState s)
    {
        PlayerFirst(s);
        E.BattleStep(s);
        Assert.That(E.AwaitingPlayer(s), Is.True);
    }

    private void TryCounter(GameState s, BattleEnemy attacker) =>
        typeof(GameEngine).GetMethod("TryCounter", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(E, new object[] { s, s.Battle!, attacker });

    private void TryBrace(GameState s, BattleEnemy mover) =>
        typeof(GameEngine).GetMethod("TryBrace", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(E, new object[] { s, s.Battle!, mover });

    /// <summary>当前随机状态往后第 k 次（1 起）NextDouble 的值（只读复算，不动存档里的随机）。</summary>
    private static double Peek(GameState s, int k)
    {
        var r = new Rng { State = s.Rng.State };
        var v = 0.0;
        for (var i = 0; i < k; i++) v = r.NextDouble();
        return v;
    }

    /// <summary>
    /// 主角 1 号位，武器Ⅰ守备短剑（A05 1.60–2.30，12→12，反击×1.15），刀剑 5 级，已用“反击架势”（A06 blade_05 反击×0.70）；
    /// 没有先手（R05：普通接敌没有先手）。敌人 1 号位、H50。
    /// </summary>
    private GameState CounterScene(uint seed, string enemy, out BattleEnemy foe, out ItemInstance sword, params string[] affixes)
    {
        var s = Fight(seed, enemy);
        Ready(s);
        sword = Arm(s, "guard_sword");
        if (affixes.Length > 0) sword.Affixes = affixes.ToList();
        s.Skills["blade"].Level = 5;
        var b = s.Battle!;
        b.FirstStrikeDamage = null;
        Assert.That(Cmd(s, "blade_05").Ok, Is.True);
        foe = Enemy(s, 1);
        foe.Slot = "1";
        foe.Hp = 50;
        foe.Guard = false;
        return s;
    }

    private const double G5 = 1 + 0.035 * 5;          // R03 Q82 成长 G=1+0.035L
    private const double CounterF = 0.70 * 1.15;      // A06 blade_05 ×0.70 × A05 守备短剑 ×1.15
    private static double SwordRaw(double u) => 1.60 + u * (2.30 - 1.60);   // A05 守备短剑伤害区间

    /// <summary>
    /// 反击命中的确切伤害（R03 单段伤害公式）：抽值×G×反击倍率 −（厚衣工尸甲 0.3 − 破甲词条 0.1）；身上有总攻令也不吃（Q82 反击不受益主动增益），
    /// 总攻令仍在；战报写“命中−伤害”，出手段是近战、结果“hit”、数值同伤害。抓 624、632—634、638、640 行。
    /// </summary>
    [Test]
    public void Counter_Hit_ExactDamage_IgnoresAllOut()
    {
        var s = CounterScene(9301, "worker", out var foe, out _, "piercing");
        var b = s.Battle!;
        b.AllOutFactor = 1.15;
        b.AllOutUntilRound = b.Round + 2;
        Force(s, Below(0.001), AtLeast(0.999), Any());
        var u = Peek(s, 3);
        var expected = SwordRaw(u) * G5 * CounterF - (0.3 - 0.1);
        E.DrainBattleFx();
        TryCounter(s, foe);
        Assert.That(50 - foe.Hp, Is.EqualTo(expected).Within(1e-9));
        Assert.That(b.CounterFactor, Is.EqualTo(0), "只反击 1 次");
        Assert.That(GameEngine.AllOutActive(b), Is.True, "反击不消耗总攻令");
        Assert.That(b.Log.Last(), Does.Contain($"你反击（守备短剑）：厚衣工尸命中−{Fmt.N(expected)}"));
        var ev = E.DrainBattleFx().Single(x => x.Name == "反击");
        Assert.That(ev.Motion, Is.EqualTo(BattleFxMotion.Melee));
        var r = ev.Results.Single();
        Assert.That((r.Outcome, r.Guarded), Is.EqualTo(("hit", false)));
        Assert.That(r.Amount, Is.EqualTo(expected).Within(1e-9));
    }

    /// <summary>反击暴击（R03 暴击 C=1.5）：伤害×1.5，战报“暴击−”，结果“crit”。抓 631、633、638、640 行暴击分支。</summary>
    [Test]
    public void Counter_Crit_TimesOnePointFive()
    {
        var s = CounterScene(9302, "shambler", out var foe, out _);
        Force(s, Below(0.001), Below(0.001), Any());
        var u = Peek(s, 3);
        var expected = SwordRaw(u) * G5 * CounterF * 1.5;   // 游荡尸甲 0（A09）
        E.DrainBattleFx();
        TryCounter(s, foe);
        Assert.That(50 - foe.Hp, Is.EqualTo(expected).Within(1e-9));
        Assert.That(s.Battle!.Log.Last(), Does.Contain($"游荡尸暴击−{Fmt.N(expected)}"));
        var r = E.DrainBattleFx().Single(x => x.Name == "反击").Results.Single();
        Assert.That(r.Outcome, Is.EqualTo("crit"));
        Assert.That(r.Amount, Is.EqualTo(expected).Within(1e-9));
    }

    /// <summary>
    /// 反击未命中：敌人不掉血，战报“未命中”，结果“miss”；反击次数照样用掉（一次为止）；先手资格也消耗（R05 Q422“反击……未命中……也消耗”）。抓 627、630 行。
    /// </summary>
    [Test]
    public void Counter_Miss_NoDamage_ConsumesFirstStrike()
    {
        var s = CounterScene(9303, "shambler", out var foe, out _);
        var b = s.Battle!;
        b.FirstStrikeDamage = new List<string> { Me };
        Force(s, AtLeast(0.999), Any(), Any());
        E.DrainBattleFx();
        TryCounter(s, foe);
        Assert.That(foe.Hp, Is.EqualTo(50));
        Assert.That(b.Log.Last(), Does.Contain("你反击游荡尸未命中"));
        Assert.That(b.CounterFactor, Is.EqualTo(0));
        Assert.That(b.FirstStrikeDamage, Does.Not.Contain(Me), "先手资格被这次反击用掉");
        Assert.That(E.DrainBattleFx().Single(x => x.Name == "反击").Results.Single().Outcome, Is.EqualTo("miss"));
    }

    /// <summary>
    /// 敌人在防御（R03“下一次直接伤害×0.5，先触发即消耗”）：反击正伤 → 伤害×0.5 并用掉敌人的防御；结果标“被格挡”。抓 635—637 行。
    /// </summary>
    [Test]
    public void Counter_EnemyGuarding_HalvesAndConsumesGuard()
    {
        var s = CounterScene(9304, "shambler", out var foe, out _);
        foe.Guard = true;
        Force(s, Below(0.001), AtLeast(0.999), Any());
        var u = Peek(s, 3);
        var expected = SwordRaw(u) * G5 * CounterF * 0.5;
        E.DrainBattleFx();
        TryCounter(s, foe);
        Assert.That(50 - foe.Hp, Is.EqualTo(expected).Within(1e-9));
        Assert.That(foe.Guard, Is.False, "防御被用掉");
        Assert.That(E.DrainBattleFx().Single(x => x.Name == "反击").Results.Single().Guarded, Is.True);
    }

    /// <summary>
    /// 被护甲完全挡成 0（防护尸甲 0.7，你虚弱×0.8、石刀低抽值）：敌人不掉血、战报“被护甲完全挡住”、结果“blocked”；敌人的防御不被用掉（Q106 0 伤不消费格挡）。
    /// 抓 635（逻辑、比较）、637、638、640 行挡住分支。
    /// </summary>
    [Test]
    public void Counter_FullyArmorBlocked_ZeroDamage_GuardKept()
    {
        var s = Fight(9305, "riot");
        Ready(s);
        Arm(s, "stone_knife");   // A05 石刀 1.00–1.50
        s.Skills["blade"].Level = 5;
        var b = s.Battle!;
        b.FirstStrikeDamage = null;
        Assert.That(Cmd(s, "blade_05").Ok, Is.True);
        var foe = Enemy(s, 1);
        foe.Slot = "1";
        foe.Hp = 50;
        foe.Guard = true;
        b.PlayerWeakFactor = 0.8;
        b.PlayerWeakUntilRound = b.Round;
        Force(s, Below(0.001), AtLeast(0.999), Below(0.05));
        Assert.That((1.00 + 0.05 * 0.50) * G5 * 0.70 * 0.8, Is.LessThan(0.7), "场景前提：最高抽值也挡不穿");
        E.DrainBattleFx();
        TryCounter(s, foe);
        Assert.That(foe.Hp, Is.EqualTo(50));
        Assert.That(foe.Guard, Is.True, "0 伤不消费防御");
        Assert.That(b.Log.Last(), Does.Contain("防护尸被护甲完全挡住"));
        var r = E.DrainBattleFx().Single(x => x.Name == "反击").Results.Single();
        Assert.That((r.Outcome, r.Amount), Is.EqualTo(("blocked", 0.0)));
    }

    /// <summary>反击打死最后一个敌人：敌人倒下（战报“，游荡尸倒下”），战斗胜利结束。抓 641、645、646 行。</summary>
    [Test]
    public void Counter_KillsLastEnemy_Victory()
    {
        var s = CounterScene(9306, "shambler", out var foe, out _);
        var b = s.Battle!;
        foe.Hp = 0.5;
        Force(s, Below(0.001), AtLeast(0.999), Any());
        TryCounter(s, foe);
        Assert.That(foe.Alive, Is.False);
        Assert.That(b.Log.Any(l => l.Contains("游荡尸倒下")), Is.True);
        Assert.That(s.Battle, Is.Null, "最后一个敌人倒下即胜利");
    }

    /// <summary>反击把灰狼打到 H≤1.2（A09“H≤1.2下次逃离”）：灰狼重伤、下次行动逃离。抓 642 行。</summary>
    [Test]
    public void Counter_WoundsWolf_WillFlee()
    {
        var s = CounterScene(9307, "wolf", out var foe, out _);
        Force(s, Below(0.001), AtLeast(0.999), Any());
        var u = Peek(s, 3);
        foe.Hp = SwordRaw(u) * G5 * CounterF + 1.0;   // 打完剩 1.0
        TryCounter(s, foe);
        Assert.That(foe.Hp, Is.EqualTo(1.0).Within(1e-9));
        Assert.That(foe.WillFlee, Is.True);
        Assert.That(s.Battle!.Log.Any(l => l.Contains("灰狼重伤，下次行动将逃离")), Is.True);
    }

    /// <summary>
    /// 反击只对存活的攻击者出手；攻击者已死不出手、架势不扣。来源页此刻不合法（够不到 3 号位的敌人）不出手、架势保留，战报写原因。
    /// 抓 652、653、656 行与 613 行原因。
    /// </summary>
    [Test]
    public void Counter_DeadOrOutOfReachAttacker_NoStrike_StanceKept()
    {
        var s = CounterScene(9308, "shambler", out var foe, out _);
        var b = s.Battle!;
        var f = b.CounterFactor;
        Assert.That(f, Is.EqualTo(CounterF).Within(1e-9));
        foe.Slot = "3";
        TryCounter(s, foe);
        Assert.That(foe.Hp, Is.EqualTo(50));
        Assert.That(b.CounterFactor, Is.EqualTo(f), "架势保留");
        Assert.That(b.Log.Last(), Does.EndWith("你的反击没有出手：够不到游荡尸（3号位）（架势保留）。"));
        foe.Slot = "1";
        foe.Dead = true;
        var n = b.Log.Count;
        TryCounter(s, foe);
        Assert.That(b.CounterFactor, Is.EqualTo(f), "攻击者已死：不出手、不扣");
        Assert.That(b.Log.Count, Is.EqualTo(n));
        foe.Dead = false;
        Force(s, Below(0.001), AtLeast(0.999), Any());
        TryCounter(s, foe);
        Assert.That(foe.Hp, Is.LessThan(50), "存活且合法时出手");
        Assert.That(b.CounterFactor, Is.EqualTo(0));
    }

    /// <summary>反击来源页按当前真实己位重验（Q82）：你被换到 3 号位时守备短剑（使用位12）不能出手，战报写己位原因、架势保留。抓 612 行原因。</summary>
    [Test]
    public void Counter_OwnPositionInvalid_ReasonNamesSlots()
    {
        var s = CounterScene(9309, "shambler", out var foe, out _);
        var b = s.Battle!;
        StandAt(s, 3);
        TryCounter(s, foe);
        Assert.That(b.Log.Last(), Does.EndWith("你的反击没有出手：守备短剑要在我方1、2号位（你在3号位）（架势保留）。"));
        Assert.That(b.CounterFactor, Is.GreaterThan(0));
    }

    /// <summary>
    /// 迎击（Q103/Q85）：存活的主动前移者 → 出手并扣 1 次；前移者已死、或你已死亡 → 不出手、不扣。抓 667 行。
    /// </summary>
    [Test]
    public void Brace_OnlyAliveMoverAndAlivePlayer()
    {
        GameState Scene(uint seed, out BattleEnemy z)
        {
            var s = Fight(seed, "shambler");
            Ready(s);
            Arm(s, "wooden_spear");
            s.Skills["spear"].Level = 5;
            s.Battle!.FirstStrikeDamage = null;
            Assert.That(Cmd(s, "spear_05").Ok, Is.True);
            z = Enemy(s, 1);
            z.Slot = "1";
            z.Hp = 50;
            return s;
        }
        var a = Scene(9310, out var za);
        Force(a, Below(0.001), AtLeast(0.999), Any());
        TryBrace(a, za);
        Assert.That(a.Battle!.BraceCharges, Is.EqualTo(0), "存活前移者：迎击并扣 1 次");
        Assert.That(za.Hp, Is.LessThan(50));

        var d = Scene(9311, out var zd);
        zd.Dead = true;
        TryBrace(d, zd);
        Assert.That(d.Battle!.BraceCharges, Is.EqualTo(1), "前移者已死：不出手不扣");

        var p = Scene(9312, out var zp);
        p.Progress.Dead = true;
        TryBrace(p, zp);
        Assert.That(p.Battle!.BraceCharges, Is.EqualTo(1), "你已死亡：不出手不扣");
        Assert.That(zp.Hp, Is.EqualTo(50));
    }
}
