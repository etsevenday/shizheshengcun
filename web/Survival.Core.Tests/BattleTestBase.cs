using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>战斗用例的公共工具：在指定地点放敌人入战、推进到玩家行动、强制下一串随机数满足条件。</summary>
public abstract class BattleTestBase : TestBase
{
    /// <summary>在农野荒地（第2日白天、冻结刷新巡游、清掉原有敌人）放下指定敌人并以敌踪方式入战。</summary>
    protected GameState Fight(uint seed, params string[] enemyIds)
    {
        var s = New(seed);
        E.DebugTeleport(s, "field");
        s.GameMinute = At(2, 9, 0);
        s.SpawnNextCheck = int.MaxValue;
        s.PatrolNextCheck = int.MaxValue;
        s.WorldEnemies.Clear();
        var ids = enemyIds.Select(id => E.DebugSpawnEnemy(s, id, "field").Id).ToList();
        s.Encounter = new PendingEncounter { LocationId = "field", EnemyIds = ids, Source = "explore", Minute = s.GameMinute };
        Assert.That(E.Engage(s).Ok, Is.True);
        Assert.That(s.Battle, Is.Not.Null);
        return s;
    }

    /// <summary>推进敌方行动直到轮到玩家（或战斗结束）。</summary>
    protected void ToPlayer(GameState s, int max = 50)
    {
        for (var i = 0; i < max && s.Battle != null && !E.AwaitingPlayer(s) && !s.Progress.Dead; i++) E.BattleStep(s);
    }

    /// <summary>让玩家在本轮先手：把队列里玩家挪到最前（只用于测试，模拟先手抽中）。</summary>
    protected static void PlayerFirst(GameState s)
    {
        var b = s.Battle!;
        b.Queue.Remove(0);
        b.Queue.Insert(0, 0);
        b.Cursor = 0;
        b.PlayerActing = false;
    }

    protected ActionResult Cmd(GameState s, string action, int? target = null, int? item = null) =>
        E.ExecuteCommand(s, BattleCommand.Of(action, target, item));

    protected static BattleEnemy Enemy(GameState s, int index) => s.Battle!.Enemies.Single(e => e.Index == index);

    /// <summary>
    /// 把存档内随机数调到一个状态，使接下来的几次 NextDouble 依次满足条件（测试里强制“命中/未命中/暴击”等）。
    /// </summary>
    protected static void Force(GameState s, params Func<double, bool>[] next)
    {
        for (uint c = 1; c < 50_000_000; c++)
        {
            var r = new Rng { State = c * 2654435761u + 7 };
            var ok = true;
            foreach (var p in next)
            {
                if (!p(r.NextDouble())) { ok = false; break; }
            }
            if (!ok) continue;
            s.Rng.State = c * 2654435761u + 7;
            return;
        }
        Assert.Fail("找不到满足条件的随机序列");
    }

    protected static Func<double, bool> Below(double p) => x => x < p;
    protected static Func<double, bool> AtLeast(double p) => x => x >= p;
    protected static Func<double, bool> Any() => _ => true;

    /// <summary>
    /// 给主角一把武器并直接穿进武器Ⅰ（B3：装备是 owner "equip:p0:weapon1"；原武器Ⅰ里的武器退回随身）。只用于布置场景，不校验同类与卡位。
    /// </summary>
    protected ItemInstance Arm(GameState s, string weaponId)
    {
        var slot = ItemInstance.EquipOwner(GameState.PlayerId, EquipSlots.Weapon1);
        foreach (var old in s.Items.Where(i => i.Owner == slot)) old.Owner = ItemInstance.Carried;
        var it = E.CreateItems(s, weaponId, 1, slot).Single();
        if (s.Battle != null) s.Battle.ActiveWeaponSlot = 1;
        return it;
    }
}
