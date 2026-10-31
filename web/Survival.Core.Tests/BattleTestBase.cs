using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>战斗用例的公共工具：在指定地点放敌人入战、推进到玩家行动、强制下一串随机数满足条件。</summary>
public abstract class BattleTestBase : TestBase
{
    /// <summary>
    /// 在农野荒地（第2日白天、冻结刷新巡游、清掉原有敌人）放下指定敌人并以敌踪方式入战。
    /// B6/B7 的旧用例按“单场最多 3 敌”写，Q62 起容量 C 来自真实解锁事实：这里先记下营区登记与一种关键记录（C3）再入战；
    /// 要测新局 C1 的用 <see cref="FightC1"/>。
    /// </summary>
    protected GameState Fight(uint seed, params string[] enemyIds) => FightWith(seed, 3, enemyIds);

    /// <summary>新局容量 C1（没有任何解锁事实）入战。</summary>
    protected GameState FightC1(uint seed, params string[] enemyIds) => FightWith(seed, 1, enemyIds);

    /// <summary>容量事实：C2 营区登记；C3 再加教会路线记录；C4 再加学院安全记录。</summary>
    protected static void UnlockCapacity(GameState s, int c)
    {
        if (c >= 2) s.Progress.Flags.Add("hub_registered");
        if (c >= 3) s.Progress.Flags.Add("church_route_record");
        if (c >= 4) s.Progress.Flags.Add("academy_safety_record");
    }

    protected GameState FightWith(uint seed, int capacity, params string[] enemyIds)
    {
        var s = New(seed);
        UnlockCapacity(s, capacity);
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

    /// <summary>
    /// 推进一步；轮到玩家（含 B6 的 3AP 剩余与追击激活）时结束行动。用于“把这一轮走完”的循环，避免玩家还剩 AP 时原地空转。
    /// </summary>
    protected void Step(GameState s)
    {
        if (E.AwaitingPlayer(s)) E.ExecuteCommand(s, BattleCommand.Of("end_turn"));
        else E.BattleStep(s);
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

    /// <summary>
    /// 把主角直接放到我方某个真实号位（Q80 起单人也按真实己位判断使用位；只用于布置场景，不扣 AP/体力）。
    /// 弓/枪/指挥/医疗器具的旧用例需要在 2—4 号位，原先靠“单人全武器兼容1号位”的豁免。
    /// </summary>
    protected static void StandAt(GameState s, int pos)
    {
        var b = s.Battle!;
        b.Allies = Enumerable.Repeat("", pos - 1).Append(GameState.PlayerId).ToList();
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
    /// <summary>敌人每次独立单体攻击提交时先抽一次目标（Q91，B8b：只有 1 名合法目标也抽一次），之后才是命中、伤害等判定。</summary>
    protected static Func<double, bool> Draw() => _ => true;

    /// <summary>敌人意图步骤的副本，目标位放宽到 1—4（直接调攻击步骤测边界时用；B8b Q91 起敌人只打目标位里的存活我方）。</summary>
    protected static Survival.Core.Config.EnemyIntentStepConfig AnySlot(Survival.Core.Config.EnemyIntentStepConfig st)
    {
        var c = Newtonsoft.Json.Linq.JObject.FromObject(st).ToObject<Survival.Core.Config.EnemyIntentStepConfig>()!;
        c.TargetSlots = "1234";
        return c;
    }

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
