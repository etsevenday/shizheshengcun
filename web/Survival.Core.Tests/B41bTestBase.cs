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

    /// <summary>把这张卡的接近余量直接设为给定值（测试布置到期场景；不经过随机）。</summary>
    protected static void SetLeft(GameState s, string key, int left)
    {
        if (!s.EnemyCards.Timers.TryGetValue(key, out var t)) s.EnemyCards.Timers[key] = t = new EnemyCardTimer();
        t.Left = left;
        t.DueMinute = left == 0 ? s.GameMinute : null;
    }
}
