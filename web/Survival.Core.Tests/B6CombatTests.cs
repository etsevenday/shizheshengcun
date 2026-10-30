using Newtonsoft.Json.Linq;
using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 B6（策划提交 f172677）R20–R24：四对四阵型、速度/行动队列/追击、AP 阶位与通用动作、命中/伤害/控制公式、战斗时间与食水。
/// 出处：R03 §1—§6，R01 §3、§4；验收 CB01—CB09、CB12、RG02、RG03、COMP04。
/// </summary>
public class B6CombatTests : BattleTestBase
{
    private void Ready(GameState s)
    {
        PlayerFirst(s);
        E.BattleStep(s);
        Assert.That(E.AwaitingPlayer(s), Is.True);
    }

    // ───────────── M128：四对四站位、容量 C、换位（R20；验收 CB01） ─────────────

    [Test]
    public void CB01_EnemiesCompactFromPos1_CapacityLimitsCount()
    {
        var s = New(1);
        E.DebugTeleport(s, "field");
        s.GameMinute = At(2, 9, 0);
        s.SpawnNextCheck = int.MaxValue;
        s.PatrolNextCheck = int.MaxValue;
        s.WorldEnemies.Clear();
        var ids = new[] { "shambler", "crawler", "shambler", "worker" }.Select(id => E.DebugSpawnEnemy(s, id, "field").Id).ToList();
        s.Encounter = new PendingEncounter { LocationId = "field", EnemyIds = ids, Source = "explore", Minute = s.GameMinute };
        Assert.That(E.Engage(s).Ok, Is.True);
        var b = s.Battle!;
        Assert.That(b.Capacity, Is.EqualTo(3), "开发假设 B6-1：容量 C 默认 3");
        Assert.That(b.Enemies.Select(e => (e.WorldId, e.Slot)), Is.EqualTo(ids.Take(3).Select((id, k) => (id, (k + 1).ToString()))), "有序名单截前 C 个，从1号位紧凑排");
        Assert.That(s.WorldEnemies.Single(w => w.Id == ids[3]).Engaged, Is.False, "超出容量的敌人不入场");
        Assert.That(b.Allies, Is.EqualTo(new[] { GameState.PlayerId }), "我方只有主角，在1号位");
        Assert.That(GameEngine.PlayerPos(b), Is.EqualTo(1));
        Assert.That(b.Log.First(), Does.Contain("1号位游荡尸").And.Contain("3号位游荡尸"));
    }

    [Test]
    public void CB01_TargetPositions_PerWeapon()
    {
        var s = Fight(2, "shambler", "shambler", "shambler");
        Ready(s);
        var cases = new (string weapon, int[] reach)[]
        {
            ("hatchet", new[] { 1 }), ("stone_knife", new[] { 1, 2 }), ("wooden_spear", new[] { 1, 2, 3 }), ("wooden_bow", new[] { 1, 2, 3 }),
        };
        E.DebugGive(s, "arrow", 3);
        foreach (var (weapon, reach) in cases)
        {
            Arm(s, weapon);
            var opt = E.BattleOptions(s).Single(o => o.Action == "basic_attack");
            Assert.That(opt.Targets, Is.EqualTo(reach), weapon);
        }
    }

    [Test]
    public void CB01_SingleAlly_AllWeaponsUsableFromPos1_SwapBlocked()
    {
        var s = Fight(3, "shambler");
        Ready(s);
        var b = s.Battle!;
        var rifle = Arm(s, "rifle");
        rifle.LoadedAmmo = 3;
        Assert.That(E.CommandBlocked(s, BattleCommand.Of("basic_attack", 1)), Is.Null, "只剩一人时步枪（使用位34）临时兼容1号位");
        var swap = E.BattleOptions(s).Single(o => o.Action == "swap");
        Assert.That((swap.Available, swap.Ap, swap.Energy), Is.EqualTo((false, 1, 0.1)));
        Assert.That(swap.Reason, Does.StartWith("只有你一人在场"));

        // 我方两人时（队友参战在 B8；这里只往站位表里放一个占位，检验规则本身）：步枪不能在1号位用，相邻换位 1AP、E0.1 后可用
        b.Allies = new List<string> { GameState.PlayerId, "ally" };
        Assert.That(E.CommandBlocked(s, BattleCommand.Of("basic_attack", 1)), Does.Contain("只能在我方3、4号位使用"));
        Assert.That(E.SwapTargets(b), Is.EqualTo(new[] { 2 }));
        var e0 = s.Stats.Energy;
        var ap = b.PlayerAp;
        Assert.That(Cmd(s, "swap", 2).Ok, Is.True);
        Assert.That(b.Allies, Is.EqualTo(new[] { "ally", GameState.PlayerId }));
        Assert.That(s.Stats.Energy, Is.EqualTo(e0 - 0.1).Within(1e-9));
        Assert.That(b.PlayerAp, Is.EqualTo(ap - 1));
        Assert.That(E.CommandBlocked(s, BattleCommand.Of("swap", 4)), Is.EqualTo("请选择相邻的我方站位"));
    }

    [Test]
    public void CB01_Migration11To12_BattleSlotsToPositions()
    {
        var s = Fight(4, "shambler", "crawler", "worker");
        Ready(s);
        Enemy(s, 1).Dead = true;
        Enemy(s, 1).Slot = "";
        var j = JObject.Parse(SaveSerializer.Serialize(s));
        j["SchemaVersion"] = "11";
        var en = (JArray)j["Battle"]!["Enemies"]!;
        en[1]!["Slot"] = "F2";
        en[2]!["Slot"] = "B1";
        ((JObject)j["Battle"]!).Remove("Capacity");
        ((JObject)j["Battle"]!).Remove("Allies");
        var m = SaveSerializer.Deserialize(j.ToString(), Cfg, out var notes);
        Assert.That(m.SchemaVersion, Is.EqualTo(GameState.CurrentSchema));
        Assert.That(m.Battle!.Enemies.Select(e => e.Slot), Is.EqualTo(new[] { "", "1", "2" }), "F2→1、B1→2 紧凑排位，死者清位");
        Assert.That(m.Battle.Capacity, Is.EqualTo(3));
        Assert.That(m.Battle.Allies, Is.EqualTo(new[] { GameState.PlayerId }));
        Assert.That(m.Battle.PlayerAp, Is.EqualTo(s.Battle!.PlayerAp), "轮中进度原样保留");
        Assert.That(notes.Single(), Does.Contain("四对四"));
        Assert.That(E.AwaitingPlayer(m), Is.True, "读档后可以继续这场战斗");
        var again = SaveSerializer.Deserialize(SaveSerializer.Serialize(m), Cfg, out var notes2);
        Assert.That(notes2, Is.Empty, "再读档不再迁移");
        Assert.That(again.Battle!.Enemies.Select(e => e.Slot), Is.EqualTo(new[] { "", "1", "2" }));
    }
}
