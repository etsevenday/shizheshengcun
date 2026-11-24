using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// B41b 新版敌踪接近时限、到期开战、撤退保护与战后撤退离地保留的公共工具（B41e3 起新版敌踪是唯一流程，见 <see cref="B41aTestBase"/>）。
/// </summary>
public abstract class B41bTestBase : B41aTestBase
{
    /// <summary>新局：第2日白天在农野荒地、冻结刷新巡游、清掉原有敌人，放下给定敌人（容量 C）。</summary>
    protected GameState Field(int capacity, params string[] enemyIds)
    {
        var s = V2Field();
        s.SuppressEnemyApproach = false;
        UnlockCapacity(s, capacity);
        foreach (var id in enemyIds) E.DebugSpawnEnemy(s, id, "field");
        return s;
    }

    /// <summary>某张卡的接近余量（还没抽为 null）。</summary>
    protected int? Left(GameState s, string key) => E.CardTimer(s, key)?.Left;

    /// <summary>推进一段并要求仍在进行。</summary>
    protected SegmentStatus Step(GameState s) => E.Advance(s).Status;

    /// <summary>走一次地图移动直到到达（途中阈值暂停自动继续）。</summary>
    protected void TravelTo(GameState s, string to)
    {
        Start(s, ActionRequest.Travel(to));
        Assert.That(Run(s), Is.EqualTo(SegmentStatus.Completed));
        Assert.That(s.CurrentLocation, Is.EqualTo(to));
    }

    /// <summary>
    /// 农野荒地（上一次从营地走来）开工 req；一只游荡尸到期接近，行动在首段后暂停开战（或先被状态提醒停下），撤退成功退回营地。
    /// B60a（S01「2026-11-24 B60 暂停继续与撤退后重新开始」Q1100，取代 R05 Q440 撤退离地保留）：撤退实际离开原行动地点，本次暂停工作按原中止结算结束，
    /// 主行动位清空。before 在开工前布置材料等。返回原行动号。
    /// </summary>
    protected int RetreatEndsAtField(GameState s, ActionRequest req, Action<GameState>? before = null, bool pauseFirstByAlert = false) =>
        RetreatEndsAtField(s, g => { before?.Invoke(g); return req; }, pauseFirstByAlert);

    /// <summary>同上，请求在布置之后才生成（要用布置出来的实例号时）。</summary>
    protected int RetreatEndsAtField(GameState s, Func<GameState, ActionRequest> make, bool pauseFirstByAlert = false)
    {
        TravelTo(s, "camp");
        TravelTo(s, "field");
        E.DebugSpawnEnemy(s, "shambler", "field");
        var req = make(s);
        var key = GameEngine.SingleCardKey(s.WorldEnemies.Single().Id);
        Start(s, req);
        var id = s.Main!.ActionId;
        LastLabel = s.Main.Label;
        if (pauseFirstByAlert)
        {
            // 状态提醒先把行动停下（不看最初暂停原因）
            s.Main.Status = ActionStatus.Paused;
            s.Main.PauseReason = PauseReason.StatAlert;
            SetLeft(s, key, 0);
            Assert.That(E.CheckEnemyApproach(s), Is.True);
            Assert.That(s.Main.PauseReason, Is.EqualTo(PauseReason.StatAlert), "原本已暂停的不伪造第二次暂停");
        }
        else
        {
            SetLeft(s, key, 0);
            Assert.That(Step(s), Is.EqualTo(SegmentStatus.Paused));
            Assert.That(s.Battle, Is.Not.Null);
        }
        RetreatOk(s);
        Assert.That(s.CurrentLocation, Is.EqualTo("camp"), "撤退退回最近一次走来的地点");
        Assert.That(s.Main, Is.Null, "Q1100：撤退离开原地点，本次暂停工作结束");
        return id;
    }

    /// <summary>最近一次 <see cref="RetreatEndsAtField(GameState, Func{GameState, ActionRequest}, bool)"/> 开工的行动名（结束后主行动位已清空）。</summary>
    protected string LastLabel = "";

    /// <summary>石刀：石头在随身、纤维在荒地地面（默认），撤退离地结束后返回原制作行动号。</summary>
    protected int RetreatEndsCraft(GameState s, bool pauseFirstByAlert = false, bool stoneCarried = true, bool fiberOnGround = true) =>
        RetreatEndsAtField(s, ActionRequest.Craft("knife"), g =>
        {
            E.DebugGive(g, "stone", 1, stoneCarried ? ItemInstance.Carried : "field");
            E.DebugGive(g, "fiber", 1, fiberOnGround ? "field" : ItemInstance.Carried);
        }, pauseFirstByAlert);

    /// <summary>
    /// 同地暂停（Q1100「同地暂停后继续」）：农野荒地开工 req、推进一段（有已提交进度），再被状态提醒停下；人仍在原地。返回原行动号。
    /// </summary>
    protected int PausedAtField(GameState s, Func<GameState, ActionRequest> make)
    {
        TravelTo(s, "camp");
        TravelTo(s, "field");
        var req = make(s);
        Start(s, req);
        var id = s.Main!.ActionId;
        Assert.That(Step(s), Is.EqualTo(SegmentStatus.Running), "起点：先提交一段进度");
        s.Main!.Status = ActionStatus.Paused;
        s.Main.PauseReason = PauseReason.StatAlert;
        return id;
    }

    protected int PausedAtField(GameState s, ActionRequest req, Action<GameState>? before = null) =>
        PausedAtField(s, g => { before?.Invoke(g); return req; });

    /// <summary>石刀同上布置，同地暂停。</summary>
    protected int PausedCraft(GameState s, bool stoneCarried = true, bool fiberOnGround = true) =>
        PausedAtField(s, ActionRequest.Craft("knife"), g =>
        {
            E.DebugGive(g, "stone", 1, stoneCarried ? ItemInstance.Carried : "field");
            E.DebugGive(g, "fiber", 1, fiberOnGround ? "field" : ItemInstance.Carried);
        });

    /// <summary>
    /// 旧档起点：B60a 之前存下的「撤退离地保留」暂停工作（R05 Q440 旧实现；S01 B60 Q1100 起不再产生）——工作在荒地开工、推进一段后暂停，
    /// 标 RetreatKept（开工来源已在开工时记下），人在营地。只造存档里的既有状态，用来验旧档的开工来源返还（Q466／Q471／Q787／Q789）仍成立、不卡死。
    /// </summary>
    /// <param name="away">false：旧档里已走回原地（Q473 旧流程）、尚未继续的那一刻。</param>
    protected int LegacyKeptAtField(GameState s, Func<GameState, ActionRequest> make, bool away = true)
    {
        var id = PausedAtField(s, make);
        s.Main!.RetreatKept = true;
        s.Main.RetreatMoveOpen = true;
        Assert.That(s.Main.ReserveSources?.Keys, Is.EquivalentTo(s.Main.ReservedItemIds), "起点：开工时已记来源");
        if (away) E.DebugTeleport(s, "camp");
        return id;
    }

    /// <summary>石刀（石头随身、纤维荒地地面）的旧档撤退离地保留。</summary>
    protected int LegacyKeptCraft(GameState s, bool away = true) =>
        LegacyKeptAtField(s, g =>
        {
            E.DebugGive(g, "stone", 1, ItemInstance.Carried);
            E.DebugGive(g, "fiber", 1, "field");
            return ActionRequest.Craft("knife");
        }, away);

    protected static ItemInstance Reserved(GameState s, string itemId, int actionId) =>
        s.Items.Single(i => i.ItemId == itemId && i.ReservedBy == actionId);

    /// <summary>把这张卡的接近余量直接设为给定值（测试布置到期场景；不经过随机）。</summary>
    protected static void SetLeft(GameState s, string key, int left)
    {
        if (!s.EnemyCards.Timers.TryGetValue(key, out var t)) s.EnemyCards.Timers[key] = t = new EnemyCardTimer();
        t.Left = left;
        t.DueMinute = left == 0 ? s.GameMinute : null;
    }
}
