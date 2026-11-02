using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 B21 R66：普通战斗撤退 ≠ 终止远征。出处：R03「2026-10-30 B4」D4（撤退成功回最近已通过且无活跃敌的节点，无则 01；首间不出地牢；
/// 含在唯一轮末 5 分钟；只退一场再走到 01 离开仍须终止）；开发假设 B21-5。验收 DR02。
/// </summary>
public class B21RetreatTests : BattleTestBase
{
    private GameState Buffer(uint seed)
    {
        var s = B21EntryTests.AtBuffer(E, seed);
        UnlockCapacity(s, 2);
        return s;
    }

    private void Go(GameState s, string to, bool confirm = false)
    {
        Start(s, ActionRequest.Travel(to, confirm));
        Assert.That(Run(s), Is.EqualTo(SegmentStatus.Completed));
        Assert.That(s.CurrentLocation, Is.EqualTo(to));
    }

    /// <summary>接战本地固定战，轮到玩家后撤退（success=true 强制成功、false 强制失败）。</summary>
    private void FightAndRetreat(GameState s, bool success)
    {
        Assert.That(E.EngageFixed(s).Ok, Is.True);
        PlayerFirst(s);
        E.BattleStep(s);
        Assert.That(E.AwaitingPlayer(s), Is.True);
        Force(s, success ? Below(0.0001) : AtLeast(0.9999));
        Assert.That(Cmd(s, "retreat").Ok, Is.True);
    }

    /// <summary>DR02：首个危险房间撤退成功回 01（不出地牢），世界时间只多本场唯一的 5 分钟，不扣包、不结束远征；之后走 01 离开仍须终止（D4 末段）。</summary>
    [Test]
    public void DR02_FirstRoomRetreat_BackTo01_OneTick_NoPenalty()
    {
        var s = Buffer(2121);
        Go(s, "d01_02", true);
        var id = s.Expedition!.Id;
        var t0 = s.GameMinute;
        var n0 = s.Stats.Satiety;
        FightAndRetreat(s, true);
        Assert.That(s.Battle, Is.Null);
        Assert.That(s.CurrentLocation, Is.EqualTo("d01_01"), "回 01，不直接穿出外部边界");
        Assert.That(s.GameMinute - t0, Is.EqualTo(5), "唯一 tick，不另加 5 分钟路程");
        Assert.That(s.Expedition!.Id, Is.EqualTo(id), "不结束远征");
        Assert.That(s.Expedition.Trail, Is.EqualTo(new[] { "d01_01" }));
        Assert.That(s.Stats.Satiety, Is.GreaterThan(n0 - 1), "不扣终止包");
        Assert.That(s.AppliedEventKeys, Is.Empty);
        Assert.That(s.Log.Any(l => l.Text.Contains("全队撤退到" + E.LocName("d01_01"))), Is.True);
        Assert.That(E.FixedEnemiesAt(s, "d01_02"), Is.Not.Empty, "固定敌留在原房间");
        Assert.That(E.CanStart(s, ActionRequest.Travel("c03_crypt")), Does.Contain("未通关离开要先终止本次远征"), "只退一场再离开仍属未通关终止");
    }

    /// <summary>DR02：撤退失败不移动，战斗继续（AP 用尽，原失败流程）。</summary>
    [Test]
    public void DR02_RetreatFail_NoMove()
    {
        var s = Buffer(2122);
        Go(s, "d01_02", true);
        FightAndRetreat(s, false);
        Assert.That(s.Battle, Is.Not.Null);
        Assert.That(s.CurrentLocation, Is.EqualTo("d01_02"));
        Assert.That(s.Battle!.PlayerAp, Is.EqualTo(0));
    }

    /// <summary>B21-5：沿来路往回找第一个没有活跃敌人的房间——上一个房间安全就退到它；它有敌人继续往回；整条来路都有敌人回 01。</summary>
    [Test]
    public void TrailBackToFirstRoomWithoutEnemies()
    {
        var s = Buffer(2123);
        Go(s, "d01_02", true);
        E.DebugWinFixedHere(s);
        E.DebugSetProgress(s, "d01_02", 100);
        Go(s, "d01_04");
        E.DebugSetProgress(s, "d01_04", 100);
        Go(s, "d01_05");
        Assert.That(s.Expedition!.Trail, Is.EqualTo(new[] { "d01_01", "d01_02", "d01_04", "d01_05" }));
        Assert.That(E.DungeonRetreatTarget(s, "d01_05"), Is.EqualTo("d01_04"), "上一个房间（安全点）没有敌人");
        var w4 = E.DebugSpawnEnemy(s, "z_slow", "d01_04");
        Assert.That(E.DungeonRetreatTarget(s, "d01_05"), Is.EqualTo("d01_02"), "04 有敌人，继续往回到已清的 02");
        var w2 = E.DebugSpawnEnemy(s, "z_slow", "d01_02");
        Assert.That(E.DungeonRetreatTarget(s, "d01_05"), Is.EqualTo("d01_01"), "来路都有敌人 → 01");
        s.WorldEnemies.Remove(w2);
        FightAndRetreat(s, true);
        Assert.That(s.CurrentLocation, Is.EqualTo("d01_02"));
        Assert.That(s.Expedition!.Trail, Is.EqualTo(new[] { "d01_01", "d01_02" }), "轨迹截到落点");
        Assert.That(s.WorldEnemies.Contains(w4), Is.True);
    }

    /// <summary>回归：野外普通战撤退仍留在原地（retreat.successMovesLocation=false），不在远征里不换位置。</summary>
    [Test]
    public void WildRetreat_StaysInPlace()
    {
        var s = Fight(2124, "z_slow");
        var here = s.CurrentLocation;
        Assert.That(s.Expedition, Is.Null);
        PlayerFirst(s);
        E.BattleStep(s);
        ToPlayer(s);
        Force(s, Below(0.0001));
        Assert.That(Cmd(s, "retreat").Ok, Is.True);
        Assert.That(s.Battle, Is.Null);
        Assert.That(s.CurrentLocation, Is.EqualTo(here));
        Assert.That(E.DungeonRetreatTarget(s, here), Is.Null);
    }
}
