using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// B41b 新版敌踪接近时限、到期开战、撤退保护与战后撤退离地保留的公共工具（开关开的配置，见 <see cref="B41aTestBase"/>）。
/// </summary>
public abstract class B41bTestBase : B41aTestBase
{
    /// <summary>开关开的新局：第2日白天在农野荒地、冻结刷新巡游、清掉原有敌人，放下给定敌人（容量 C）。</summary>
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
    /// 农野荒地（上一次从营地走来）开工 req；一只游荡尸到期接近，行动在首段后暂停开战（或先被状态提醒停下），撤退成功退回营地，
    /// 原行动按 Q440 保留暂停。before 在开工前布置材料等。返回原行动号。
    /// </summary>
    protected int KeptAtField(GameState s, ActionRequest req, Action<GameState>? before = null, bool pauseFirstByAlert = false) =>
        KeptAtField(s, g => { before?.Invoke(g); return req; }, pauseFirstByAlert);

    /// <summary>同上，请求在布置之后才生成（要用布置出来的实例号时）。</summary>
    protected int KeptAtField(GameState s, Func<GameState, ActionRequest> make, bool pauseFirstByAlert = false)
    {
        TravelTo(s, "camp");
        TravelTo(s, "field");
        E.DebugSpawnEnemy(s, "shambler", "field");
        var req = make(s);
        var key = GameEngine.SingleCardKey(s.WorldEnemies.Single().Id);
        Start(s, req);
        var id = s.Main!.ActionId;
        if (pauseFirstByAlert)
        {
            // 状态提醒先把行动停下（Q463：不看最初暂停原因）
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
        Assert.That(s.Main!.ActionId, Is.EqualTo(id));
        Assert.That(s.Main.Status, Is.EqualTo(ActionStatus.Paused));
        Assert.That(s.Main.RetreatKept && s.Main.RetreatMoveOpen, Is.True);
        return id;
    }

    /// <summary>石刀：石头在随身、纤维在荒地地面（默认），撤退保留后返回原制作行动号。</summary>
    protected int KeptCraft(GameState s, bool pauseFirstByAlert = false, bool stoneCarried = true, bool fiberOnGround = true) =>
        KeptAtField(s, ActionRequest.Craft("knife"), g =>
        {
            E.DebugGive(g, "stone", 1, stoneCarried ? ItemInstance.Carried : "field");
            E.DebugGive(g, "fiber", 1, fiberOnGround ? "field" : ItemInstance.Carried);
        }, pauseFirstByAlert);

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
