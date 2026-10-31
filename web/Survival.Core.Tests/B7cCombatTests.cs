using Newtonsoft.Json.Linq;
using Survival.Core.Config;
using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 B7c（策划提交 f58ead8 答复 Q62–Q74）R98：容量 C 来自真实事实、人物基础防御与位移抗性限域、拳的目标位与护拳绑带、
/// 命中 +8 与单次疲劳 ×0.95、当前盾页格挡、小数速度，以及 Q65/66/68/70/72/73/74 的现状核对。
/// 出处：R03「2026-10-31 Q62—Q74战斗数值与操作裁定（当前优先）」、A05/A06/A11 同名章、combat-adjudications.json（= A12 combatAdjudications）、
/// 更新说明/2026-10-31/Q62-Q74 更新说明；验收 CB-Q62—CB-Q74。
/// </summary>
public class B7cCombatTests : BattleTestBase
{
    private static JObject Adj() =>
        (JObject)JObject.Parse(File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "design.a12.json")))["combatAdjudications"]!;

    private GameState Field(uint seed)
    {
        var s = New(seed);
        E.DebugTeleport(s, "field");
        s.GameMinute = At(2, 9, 0);
        s.SpawnNextCheck = int.MaxValue;
        s.PatrolNextCheck = int.MaxValue;
        s.WorldEnemies.Clear();
        return s;
    }

    private List<int> Spot(GameState s, params string[] ids)
    {
        var list = ids.Select(id => E.DebugSpawnEnemy(s, id, "field").Id).ToList();
        s.Encounter = new PendingEncounter { LocationId = "field", EnemyIds = list, Source = "explore", Minute = s.GameMinute };
        return list;
    }

    // ───────────── M164：容量 C 来自真实事实（Q62 选 B） ─────────────

    /// <summary>CB-Q62：新局没有任何解锁事实为 C1；敌踪三只只入第一只，其余留在原地、不交战、不判死亡/奖励。</summary>
    [Test]
    public void CB_Q62_NewGameC1_OnlyFirstOfOrderedListJoins_RestStayUntouched()
    {
        var cap = Adj()["capacity"]!;
        Assert.That((int)cap["newAndNoMilestoneDefault"]!, Is.EqualTo(1));
        Assert.That(Cfg.Scope.Capacity.NewGame, Is.EqualTo(1));
        var s = Field(1);
        var ids = Spot(s, "worker", "shambler", "crawler");
        Assert.That(E.BattleCapacity(s), Is.EqualTo(1), "新局无事实 C1，不沿临时 3");
        Assert.That(E.EncounterJoiners(s).Select(w => w.Id), Is.EqualTo(ids.Take(1)), "按有序名单取前 C 个");
        Assert.That(E.EncounterCapacityNote(s), Does.Contain("容量C1").And.Contain("其余2只留在原地"));
        var hp = s.WorldEnemies.ToDictionary(w => w.Id, w => w.Hp);
        Assert.That(E.Engage(s).Ok, Is.True);
        var b = s.Battle!;
        Assert.That(b.Capacity, Is.EqualTo(1));
        Assert.That(b.Enemies.Select(e => e.WorldId), Is.EqualTo(ids.Take(1)));
        Assert.That(b.Log.First(), Does.Contain("本场容量C1，另有2只留在原地"));
        foreach (var id in ids.Skip(1))
        {
            var w = s.WorldEnemies.Single(x => x.Id == id);
            Assert.That(w.Engaged, Is.False, "未选的不入场");
            Assert.That(w.Hp, Is.EqualTo(hp[id]), "未选的不受伤");
        }
        // 打完：未选的仍在世界里（不算击杀、无战利品）
        Enemy(s, 1).Hp = 0.01;
        ToPlayer(s);
        Force(s, Below(0.001), AtLeast(0.99), Any(), Any(), Any());
        Cmd(s, "basic_attack", 1);
        Assert.That(s.Battle, Is.Null, "唯一入场者倒下即胜利");
        Assert.That(s.WorldEnemies.Where(w => ids.Skip(1).Contains(w.Id)).Select(w => w.Hp), Is.EqualTo(ids.Skip(1).Select(id => hp[id])), "未选世界敌继续世界账");
    }

    /// <summary>CB-Q62：登记 C2；两种关键记录之一 C3、两种都有 C4；只看事实，只增不减（事实不删）。</summary>
    [Test]
    public void CB_Q62_FactsUnlockC2C3C4()
    {
        var s = Field(2);
        Assert.That(E.BattleCapacity(s), Is.EqualTo(1));
        s.Progress.Flags.Add("hub_registered");
        Assert.That(E.BattleCapacity(s), Is.EqualTo(2));
        s.Progress.Flags.Add("academy_safety_record");
        Assert.That(E.BattleCapacity(s), Is.EqualTo(3), "任一关键记录 C3");
        s.Progress.Flags.Add("church_route_record");
        Assert.That(E.BattleCapacity(s), Is.EqualTo(4), "两种都有 C4");
        var t = Field(3);
        t.Progress.Flags.Add("church_route_record");
        Assert.That(E.BattleCapacity(t), Is.EqualTo(3), "解锁取已满足档的最大值");
        Assert.That(E.CapacityText(Field(4)), Does.Contain("容量C1").And.Contain("营区登记后C2"));
        Assert.That(E.DebugCapacityFact(t, null), Does.Contain("C1"));
        Assert.That(E.BattleCapacity(t), Is.EqualTo(1), "开发入口清除事实");
    }

    /// <summary>CB-Q62：增援本场累计（含已死）≤C；C1 时噪声增援不进场。</summary>
    [Test]
    public void CB_Q62_C1_ReinforcementCumulativeAtMostOne()
    {
        var s = FightC1(20, "shambler");
        E.DebugSpawnEnemy(s, "crawler", "field");
        ToPlayer(s);
        var b = s.Battle!;
        Assert.That(b.Capacity, Is.EqualTo(1));
        b.ReinforcementRequested = true;
        Cmd(s, "end_turn");
        while (s.Battle != null && b.Round == 1) { if (b.Cursor < b.Queue.Count && b.Queue[b.Cursor] > 0) { b.Cursor++; continue; } Step(s); }
        Assert.That(b.Enemies.Count, Is.EqualTo(1), "累计投入已达 C1，不增援");
        Assert.That(b.ReinforcementUsed, Is.False);
    }

    /// <summary>CB-Q62：进行中的战斗保持开战锁存的 C 到结束（存读档不重算）；下一场新战斗按当时事实。</summary>
    [Test]
    public void CB_Q62_ActiveBattleKeepsLockedC_NextBattleUsesFacts()
    {
        var s = Fight(5, "shambler", "shambler", "shambler");
        Assert.That(s.Battle!.Capacity, Is.EqualTo(3));
        s.Progress.Flags.Clear();   // 例如 B6 线上旧档：战斗中 C3 已锁存、存档里并没有真实解锁事实
        var t = SaveSerializer.Deserialize(SaveSerializer.Serialize(s), Cfg);
        Assert.That(t.Battle!.Capacity, Is.EqualTo(3), "旧战 C 保留到结束");
        Assert.That(t.Battle.Enemies.Count(e => e.Alive), Is.EqualTo(3));
        Assert.That(E.BattleCapacity(t), Is.EqualTo(1), "下一场新战斗用真实事实：无事实 C1");
    }
}
