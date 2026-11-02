using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 B21 R66：EXTRACTING 有耗时的撤出。出处：R03「2026-10-30 B4」D5.2「有耗时的撤出，禁止零时间快递」、D10；
/// S01「B3/B4 世界时钟与实例原子事务」（SCB01/SCB02）；S13「B4 地牢整备与撤出恢复边界」；R04「B4 远征感染与死亡优先」；
/// 开发假设 B21-7、B21-8、B21-9、B21-10。验收 DR05、DR06、DR07、SCB01、SCB02、COMP09。
/// </summary>
public class B21ExtractTests : BattleTestBase
{
    private void Go(GameState s, string to, bool confirm = false)
    {
        Start(s, ActionRequest.Travel(to, confirm));
        Assert.That(Run(s), Is.EqualTo(SegmentStatus.Completed));
        Assert.That(s.CurrentLocation, Is.EqualTo(to));
    }

    /// <summary>01→02 的进行中远征（02 固定战未打）。</summary>
    private GameState At02(uint seed)
    {
        var s = B21EntryTests.AtBuffer(E, seed);
        UnlockCapacity(s, 2);
        Go(s, "d01_02", true);
        return s;
    }

    /// <summary>01→02→04→05 的进行中远征（02 已打赢）。</summary>
    private GameState At05(uint seed)
    {
        var s = At02(seed);
        E.DebugWinFixedHere(s);
        E.DebugSetProgress(s, "d01_02", 100);
        Go(s, "d01_04");
        E.DebugSetProgress(s, "d01_04", 100);
        Go(s, "d01_05");
        return s;
    }

    private static void High(GameState s) => s.Player.Stats = new Stats { HealthMax = 20, EnergyMax = 20, Health = 20, Satiety = 95, Hydration = 95, Energy = 20, Wakefulness = 95, Morale = 95 };

    /// <summary>DR06：10+5 分钟路径 T=30（最低 30、已含 01 出口边、不另加）；逐 5 分钟推进，先在原地用掉 T−路径，再逐边到达；终点是父地点不是营区。</summary>
    [Test]
    public void DR06_ShortPath_T30_PositionsAndArrival()
    {
        var s = At02(2141);
        High(s);
        var t0 = s.GameMinute;
        var seg0 = s.SegmentCount;
        Assert.That(E.AbortExpedition(s).Ok, Is.True);
        var e = s.Expedition!;
        Assert.That(e.ExtractRoute, Is.EqualTo(new[] { "d01_02", "d01_01", "c03_crypt" }));
        Assert.That(e.ExtractLegMinutes, Is.EqualTo(new[] { 5, 10 }));
        Assert.That(e.TotalMinutes, Is.EqualTo(30));
        var seen = new List<string>();
        while (s.Main != null) { Assert.That(E.Advance(s).Status, Is.Not.EqualTo(SegmentStatus.Paused)); seen.Add(s.CurrentLocation); }
        Assert.That(s.GameMinute - t0, Is.EqualTo(30));
        Assert.That(s.SegmentCount - seg0, Is.EqualTo(6), "SCB02：每 5 分钟只结算一次");
        // 等 15 分钟（30−15 路径）后到 01 的那条边 5 分钟：15、20 分钟时仍在 02/到 01；最后到父地点
        Assert.That(seen, Is.EqualTo(new[] { "d01_02", "d01_02", "d01_02", "d01_01", "d01_01", "c03_crypt" }));
        Assert.That(s.CurrentLocation, Is.EqualTo("c03_crypt"), "不回营区");
        Assert.That(s.Expedition, Is.Null);
        Assert.That(s.ExpeditionLog.Single().EndKind, Is.EqualTo("abort"));
        Assert.That(E.DungeonStatus(s, "d01"), Is.EqualTo(DungeonStatuses.Active));
        Assert.That(s.Log.Last(l => l.Text.StartsWith("撤出完成")).Text, Does.Contain("不是营区"));
    }

    /// <summary>DR06：25 基础分钟、夜间×负重倍率 → T=向上取 5(25×1.875)=50，不小于正常路径；锁定后扣 E 跌破阈值、途中不重算。</summary>
    [Test]
    public void DR06_NightOverweight_Rounded_LockedAfterEnergyDrop()
    {
        var s = At05(2142);
        High(s);
        s.GameMinute = (Clock.Day(s.GameMinute)) * 1440 - 1440 + 22 * 60;   // 当天 22:00
        E.DebugGive(s, "heavy_armor", 4);
        var f = Cfg.WorkFactors;
        Assert.That(E.TimeFactor(s, s.Player, ActionType.Travel), Is.EqualTo(Math.Min(f.Cap, f.Night * f.Overweight)).Within(1e-9));
        s.Player.Stats.Set(StatId.Energy, 11);   // 扣 8 后 3 < 4，低体力倍率不再乘
        Assert.That(E.AbortExpedition(s).Ok, Is.True);
        var e = s.Expedition!;
        Assert.That(e.ExtractLegMinutes.Sum(), Is.EqualTo(25));
        Assert.That(e.LockedFactor, Is.EqualTo(1.875).Within(1e-9));
        Assert.That(e.TotalMinutes, Is.EqualTo(50));
        Assert.That(e.TotalMinutes, Is.GreaterThanOrEqualTo(25 * 1.875));
        Assert.That(s.Player.Stats.Energy, Is.LessThan(4));
        var t0 = s.GameMinute;
        Assert.That(Run(s, false), Is.EqualTo(SegmentStatus.Completed));
        Assert.That(s.GameMinute - t0, Is.EqualTo(50), "不因低体力/入夜重算、不另加 30 或 10");
    }

    /// <summary>B21-7：白天、不超重 25 基础分钟 → T=30（下限）；夜间只乘 1.5 → 40。</summary>
    [Test]
    public void DR06_TotalMinutesFormula()
    {
        Assert.That(E.ExtractTotalMinutes(10, 1), Is.EqualTo(30));
        Assert.That(E.ExtractTotalMinutes(25, 1), Is.EqualTo(30));
        Assert.That(E.ExtractTotalMinutes(25, 1.5), Is.EqualTo(40));
        Assert.That(E.ExtractTotalMinutes(40, 1), Is.EqualTo(40));
        Assert.That(E.ExtractTotalMinutes(41, 1), Is.EqualTo(45));
        Assert.That(E.ExtractTotalMinutes(20, 2), Is.EqualTo(40));
    }

    /// <summary>DR07：途中 N/W/E/S/M 不再下降、腹泻不扣 W、无回血；已归零的 N/W/S 照常扣健康；病毒照涨；阈值提醒不暂停。</summary>
    [Test]
    public void DR07_NoFiveStatDrain_ZeroDamageContinues_VirusGrows()
    {
        var s = At02(2143);
        High(s);
        s.Player.Stats.Set(StatId.Satiety, 20);   // 扣 25 → 0
        s.Player.DiarrheaUntil = s.GameMinute + 600;
        E.DebugSetVirus(s, 40);
        Assert.That(E.AbortExpedition(s).Ok, Is.True);
        var st = s.Player.Stats.Clone();
        Assert.That(st.Satiety, Is.EqualTo(0));
        var v0 = s.Player.Body.VirusLoad;
        while (s.Main != null) Assert.That(E.Advance(s).Status, Is.Not.EqualTo(SegmentStatus.Paused), "阈值不暂停撤出");
        var now = s.Player.Stats;
        Assert.That((now.Hydration, now.Energy, now.Wakefulness, now.Morale), Is.EqualTo((st.Hydration, st.Energy, st.Wakefulness, st.Morale)), "五项不再下降（腹泻负 W 也已含在包里）");
        Assert.That(now.Health, Is.EqualTo(st.Health + Cfg.HealthRules.ZeroSatietyPerHour * 30 / 60).Within(1e-9), "饥饿归零照常扣健康，无回血");
        Assert.That(s.Player.Body.VirusLoad, Is.GreaterThan(v0), "病毒照涨");
    }

    /// <summary>DR07：主角途中死亡立即终局，停止后续段、不到父地点；远征按终局结束（CAMPAIGN_ENDED）。</summary>
    [Test]
    public void DR07_DeathMidway_StopsAndNoArrival()
    {
        var s = At05(2144);
        High(s);
        Assert.That(E.AbortExpedition(s).Ok, Is.True);
        s.Player.Stats.Set(StatId.Hydration, 0);
        s.Player.Stats.Set(StatId.Health, 0.2);
        var r = Run(s, false);
        Assert.That(r, Is.EqualTo(SegmentStatus.Dead));
        Assert.That(s.Progress.Dead, Is.True);
        Assert.That(s.CurrentLocation, Is.Not.EqualTo("c03_crypt"));
        Assert.That(s.Progress.Death!.LocationId, Is.EqualTo("d01_05"), "死在返程边起点");
        Assert.That(s.Main, Is.Null);
        Assert.That(s.Expedition, Is.Null);
        Assert.That(s.ExpeditionLog.Last().EndKind, Is.EqualTo("death"));
        Assert.That(E.DungeonStatus(s, "d01"), Is.EqualTo(DungeonStatuses.CampaignEnded));
        Assert.That(E.Advance(s).Status, Is.EqualTo(SegmentStatus.Dead));
    }

    /// <summary>DR05/SCB01/COMP09：撤出到一半存档→读档续剩余分钟（总时长不变、不重扣包）；途中取消、移动、探索、接战、再终止都被拒。</summary>
    [Test]
    public void DR05_SCB01_ReloadMidway_ResumesRemaining_NoSecondCharge()
    {
        var s = At05(2145);
        High(s);
        Assert.That(E.AbortExpedition(s, "abort-x").Ok, Is.True);
        var t0 = s.GameMinute;
        var total = s.Expedition!.TotalMinutes;
        var after = s.Player.Stats.Clone();
        Steps(s, 3);
        Assert.That(s.Expedition!.ElapsedMinutes, Is.EqualTo(15));
        var t = SaveSerializer.Deserialize(SaveSerializer.Serialize(s), Cfg);
        Assert.That(t.Main!.Type, Is.EqualTo(ActionType.Extract));
        Assert.That(t.Main.ElapsedMinutes, Is.EqualTo(15));
        Assert.That(E.Cancel(t).Ok, Is.False);
        Assert.That(E.CanStart(t, ActionRequest.Travel("d01_04")), Does.Contain("正在撤出"));
        Assert.That(E.CanStart(t, ActionRequest.Explore()), Does.Contain("正在撤出"));
        Assert.That(E.CanStart(t, ActionRequest.UseItem("clean_water")), Does.Contain("正在撤出"));
        Assert.That(E.CanEngageFixed(t), Is.Not.Null);
        Assert.That(E.AbortExpedition(t, "abort-y").Ok, Is.False);
        Assert.That(E.AbortExpedition(t, "abort-x").Duplicate, Is.True);
        Assert.That(E.DebugMapTeleport(t, "camp"), Does.Contain("先结束再传送"));
        Assert.That(Run(t, false), Is.EqualTo(SegmentStatus.Completed));
        Assert.That(t.GameMinute - t0, Is.EqualTo(total), "只续剩余分钟");
        Assert.That(t.Player.Stats.Morale, Is.EqualTo(after.Morale), "没有第二次扣包");
        Assert.That(t.AppliedEventKeys, Has.Count.EqualTo(1));
        Assert.That(t.CurrentLocation, Is.EqualTo("c03_crypt"));
    }

    /// <summary>SCB02：撤退那场的唯一 tick 与撤出第一段不重复——撤退 1 段 + 撤出 T/5 段，世界只推进这么多。</summary>
    [Test]
    public void SCB02_RetreatTickThenExtract_NoDoubleSegment()
    {
        var s = At02(2146);
        High(s);
        var seg0 = s.SegmentCount;
        var t0 = s.GameMinute;
        Assert.That(E.EngageFixed(s).Ok, Is.True);
        PlayerFirst(s);
        E.BattleStep(s);
        Force(s, Below(0.0001));
        Assert.That(Cmd(s, "retreat").Ok, Is.True);
        Assert.That(s.CurrentLocation, Is.EqualTo("d01_01"));
        Assert.That(E.AbortExpedition(s).Ok, Is.True);
        Assert.That(s.Expedition!.TotalMinutes, Is.EqualTo(30));
        Assert.That(Run(s, false), Is.EqualTo(SegmentStatus.Completed));
        Assert.That(s.SegmentCount - seg0, Is.EqualTo(1 + 6));
        Assert.That(s.GameMinute - t0, Is.EqualTo(5 + 30));
    }

    /// <summary>队友途中死亡：遗物落当前返程边起点，撤出继续，主角照常到达。</summary>
    [Test]
    public void CompanionDiesMidway_RemainsAtEdgeStart_Continue()
    {
        var s = B21EntryTests.AtBuffer(E, 2147);
        UnlockCapacity(s, 2);
        E.DebugRecruit(s, "npc_lin_zhao");
        Go(s, "d01_02", true);
        High(s);
        Assert.That(E.AbortExpedition(s).Ok, Is.True);
        var mate = s.Actor("npc_lin_zhao");
        mate.Stats.Set(StatId.Hydration, 0);
        mate.Stats.Set(StatId.Health, 0.1);
        Assert.That(Run(s, false), Is.EqualTo(SegmentStatus.Completed));
        Assert.That(s.CurrentLocation, Is.EqualTo("c03_crypt"));
        Assert.That(s.Remains.ContainsKey("npc_lin_zhao"), Is.True);
        Assert.That(s.Roster.Single(r => r.ActorId == "npc_lin_zhao").DeathLocation, Is.EqualTo("d01_02"));
    }
}
