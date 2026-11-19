using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发记录 BM12「次要问题合并」：Q967 选 A（出手特写期间顶栏体力与 AP 同步到结果帧）。
/// 规则出处：02 Q967 策划答复（R304）与合同 assets/20261118/bm11-q965-q967-repair-drying-energy.json energy 段、验收 BM11F-Q967-01／02／03；
/// R03 §Q617（“常驻战场HP／体力／AP……都跟结果帧”）。
/// </summary>
public class BM12BattleEnergyTests : BattleTestBase
{
    protected override bool NoEnemyApproach => true;

    private void Ready(GameState s)
    {
        PlayerFirst(s);
        E.BattleStep(s);
        Assert.That(E.AwaitingPlayer(s), Is.True);
    }

    private static List<BattleFxEvent> Full(List<BattleFxEvent> fx) => fx.Where(x => !x.Short && x.Results.Count > 0).ToList();

    /// <summary>
    /// BM11F-Q967-01／02：攻击出手第一段开始前的显示快照里，行动者体力仍是提交前的 15（与 AP 3 一起）；
    /// 真实体力已在提交时扣一次（低于 15），显示快照不再重扣——结果帧之后（该指令只有一段时回到真实状态）就是真实后值。
    /// </summary>
    [Test]
    public void Q967_01_Attack_FirstSegmentHoldsEnergyBeforeCommand()
    {
        var s = Fight(96701, "worker");
        Ready(s);
        Arm(s, "machete");
        s.Battle!.PlayerAp = 3;   // 布场：本次激活剩 3AP
        s.Stats.Energy = 15;      // 布场：提交前体力 15
        E.DrainBattleFx();
        Assert.That(Cmd(s, "basic_attack", 1).Ok, Is.True);
        var full = Full(E.DrainBattleFx());
        Assert.That(full, Has.Count.GreaterThanOrEqualTo(1));
        var view = full[0].View!;
        Assert.That(view.Actor(GameState.PlayerId).Stats.Energy, Is.EqualTo(15), "Q967 A：特写开始显示本次行动者出手前体力");
        Assert.That(view.Battle!.Fighter(GameState.PlayerId).Ap, Is.EqualTo(3), "与 AP 同步（R280）");
        Assert.That(s.Stats.Energy, Is.LessThan(15), "真实体力在提交时已扣（原时机）");
        if (full.Count > 1) Assert.That(full[1].View!.Actor(GameState.PlayerId).Stats.Energy, Is.EqualTo(s.Stats.Energy), "后续段开始前已是真实后值，不再还原");
    }

    /// <summary>
    /// BM11F-Q967-02（不串人）：主角出手时，同场同伴的体力在显示快照里照真实值，不被改成主角的提交前值。
    /// </summary>
    [Test]
    public void Q967_02_OnlyActorHeld_MateUntouched()
    {
        var s = New(96702);
        UnlockCapacity(s, 3);
        E.DebugQuickParty(s, 2);
        var mate = s.Party.Single(id => id != GameState.PlayerId);
        E.DebugTeleport(s, "field");
        s.GameMinute = At(2, 9, 0);
        s.SpawnNextCheck = int.MaxValue;
        s.PatrolNextCheck = int.MaxValue;
        s.WorldEnemies.Clear();
        var id = E.DebugSpawnEnemy(s, "worker", "field").Id;
        Assert.That(E.DebugStartBattle(s, new[] { id }, GameEngine.FearEncounter), Is.True);
        s.Battle!.Allies = new List<string> { GameState.PlayerId, mate };
        Ready(s);
        Arm(s, "machete");
        s.Battle.PlayerAp = 3;
        s.Stats.Energy = 15;
        s.Actor(mate).Stats.Energy = 9;   // 布场：同伴体力 9（与主角不同）
        E.DrainBattleFx();
        Assert.That(Cmd(s, "basic_attack", 1).Ok, Is.True, string.Join(" | ", s.Battle?.Log.TakeLast(2) ?? Enumerable.Empty<string>()));
        var full = Full(E.DrainBattleFx());
        Assert.That(full, Has.Count.GreaterThanOrEqualTo(1));
        var view = full[0].View!;
        Assert.That(view.Actor(GameState.PlayerId).Stats.Energy, Is.EqualTo(15), "行动者显示提交前值");
        Assert.That(view.Actor(mate).Stats.Energy, Is.EqualTo(s.Actor(mate).Stats.Energy), "同伴不串：显示其真实体力");
        Assert.That(s.Actor(mate).Stats.Energy, Is.EqualTo(9), "同伴真实体力不受主角出手影响");
    }

    /// <summary>
    /// BM11F-Q967-03（无特写短反馈保持）：调整站位是短反馈（无结果帧），体力照原规则立即显示真实值，提交前体力也不留给下一条指令；
    /// 下一条攻击指令的第一段按它自己提交前的体力。
    /// </summary>
    [Test]
    public void Q967_03_ShortFeedbackKeepsImmediate_NextCommandUsesOwnBefore()
    {
        var s = Fight(96703, "worker");
        Ready(s);
        StandAt(s, 1);
        Arm(s, "machete");
        s.Battle!.PlayerAp = 5;
        s.Stats.Energy = 15;
        E.DrainBattleFx();
        var swap = E.BattleOptions(s).First(o => o.Action == "swap" && o.Available);
        Assert.That(Cmd(s, "swap", null, swap.ItemInstanceId).Ok, Is.True);
        var g = E.DrainBattleFx();
        Assert.That(g, Has.Count.EqualTo(1));
        Assert.That((g[0].Short, g[0].View), Is.EqualTo((true, (GameState?)null)), "短反馈没有结果帧：体力直接显示真实值");
        var afterSwap = s.Stats.Energy;
        Assert.That(afterSwap, Is.LessThan(15), "调整站位真实扣体力（R03 调整站位费用）");
        Assert.That(Cmd(s, "basic_attack", 1).Ok, Is.True, string.Join(" | ", s.Battle!.Log.TakeLast(2)));
        var full = Full(E.DrainBattleFx());
        Assert.That(full, Has.Count.GreaterThanOrEqualTo(1));
        Assert.That(full[0].View!.Actor(GameState.PlayerId).Stats.Energy, Is.EqualTo(afterSwap), "下一条指令的第一段按它自己提交前的体力");
    }
}
