using System.Reflection;
using Newtonsoft.Json.Linq;
using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 B8a（策划提交 51304bb 答复 Q84–Q87）战斗部分：R101 增援落位（Q84）、R102 迫近触发架矛（Q85）、R104 前方同伴主动补位（Q87）。
/// 出处：R03/R05「Q84—Q87 增援、迫近与编队联动（2026-10-31，当前优先）」、A06/A09 同轮章、
/// combat-resolution-adjudications.json（= A12 combatResolutionAdjudications）；验收 A10 POS-Q84-01、POS-Q85-01—02、POS-Q87-01—04。
/// </summary>
public class B8aCombatTests : BattleTestBase
{
    private static JObject Res() =>
        (JObject)JObject.Parse(File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "design.a12.json")))["combatResolutionAdjudications"]!;

    private void Ready(GameState s)
    {
        PlayerFirst(s);
        E.BattleStep(s);
        Assert.That(E.AwaitingPlayer(s), Is.True);
    }

    private static void Place(GameState s, params (int index, int slot)[] at)
    {
        foreach (var (i, p) in at) Enemy(s, i).Slot = p.ToString();
    }

    private void EnemyAct(GameState s, BattleEnemy e) =>
        typeof(GameEngine).GetMethod("EnemyActivation", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(E, new object[] { s, e, false });

    private void Reinforce(GameState s) =>
        typeof(GameEngine).GetMethod("TryReinforce", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(E, new object[] { s });

    // ───────────── R101：增援落位（Q84） ─────────────

    /// <summary>
    /// POS-Q84-01：仅 2 有人→3；仅 4 有人→1；1/3 有人→4；无存活→1；四格全满→0（不生成）。
    /// 落位不压紧其他人、不触发架矛；累计达 C 不增援、请求不核销。
    /// </summary>
    [Test]
    public void POS_Q84_01_ReinforcementSlot_BehindRearmost_ElseFrontmost_NoCompaction()
    {
        var rp = (JObject)Res()["enemyActivation"]!["reinforcementPlacement"]!;
        Assert.That((string)rp["priority"]!, Is.EqualTo("first_empty_slot_after_rearmost_living_else_frontmost_empty"));
        Assert.That((string)rp["noEmpty"]!, Is.EqualTo("reject_spawn_without_consuming_count_or_trigger"));
        Assert.That((bool)rp["triggersActiveForwardMove"]!, Is.False);

        var s = FightWith(8401, 4, "shambler", "shambler", "shambler");
        var b = s.Battle!;
        void Layout(params int[] slots)
        {
            var k = 0;
            foreach (var e in b.Enemies)
            {
                e.Dead = k >= slots.Length;
                e.Slot = k < slots.Length ? slots[k].ToString() : "";
                k++;
            }
        }
        Layout(2); Assert.That(E.ReinforceSlot(b), Is.EqualTo(3), "仅 2 有人→3");
        Layout(4); Assert.That(E.ReinforceSlot(b), Is.EqualTo(1), "仅 4 有人→1");
        Layout(1, 3); Assert.That(E.ReinforceSlot(b), Is.EqualTo(4), "1/3 有人→4");
        Layout(2, 4); Assert.That(E.ReinforceSlot(b), Is.EqualTo(1), "末位在 4：身后无空槽取最前空槽 1");
        Layout(); Assert.That(E.ReinforceSlot(b), Is.EqualTo(1), "无存活敌人→1");
        Layout(1, 2, 4); Assert.That(E.ReinforceSlot(b), Is.EqualTo(3), "1/2/4 有人：身后无空槽→最前空槽 3");
        Layout(1, 2, 3);
        b.Enemies.Add(new BattleEnemy { Index = 9, EnemyId = "shambler", Slot = "4", Hp = 1 });
        Assert.That(E.ReinforceSlot(b), Is.EqualTo(0), "四格全满：不生成");
        b.Enemies.RemoveAt(b.Enemies.Count - 1);

        // 真实增援：仅 2 号位存活，加入者落 3 号位；其他人号位不变、不触发架矛
        Layout(2);
        E.DebugSpawnEnemy(s, "crawler", "field");
        b.BraceCharges = 1;
        b.BracePage = 1;
        b.ReinforcementRequested = true;
        Reinforce(s);
        var joined = b.Enemies.Last();
        Assert.That(joined.EnemyId, Is.EqualTo("crawler"));
        Assert.That(joined.Pos, Is.EqualTo(3), "仅 2 有人→增援在 3");
        Assert.That(b.Enemies[0].Pos, Is.EqualTo(2), "不压紧：原敌仍在 2");
        Assert.That(b.ReinforcementUsed, Is.True);
        Assert.That(b.BraceCharges, Is.EqualTo(1), "增援落位不算主动前移，不触发架矛");
        Assert.That(b.Log.Last(), Does.Contain("（3号位）"));
    }

    /// <summary>POS-Q84-01：累计投入（含已死）已达 C 时不增援，请求保留不核销；落位资格先按 C 判。</summary>
    [Test]
    public void POS_Q84_01_CapacityReached_NoReinforcement_RequestKept()
    {
        var s = FightWith(8402, 2, "shambler", "shambler");
        var b = s.Battle!;
        Enemy(s, 1).Dead = true;
        Enemy(s, 1).Slot = "";
        E.DebugSpawnEnemy(s, "crawler", "field");
        b.ReinforcementRequested = true;
        Reinforce(s);
        Assert.That(b.Enemies.Count, Is.EqualTo(2), "累计（含已死）已达 C2：不增援");
        Assert.That(b.ReinforcementUsed, Is.False, "不核销请求");
    }
}
