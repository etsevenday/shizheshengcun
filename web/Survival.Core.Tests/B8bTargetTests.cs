using System.Reflection;
using Newtonsoft.Json.Linq;
using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 B8b M201（R106）：策划答复 Q91——敌人每个独立单体攻击在提交时从当前合法存活目标里等概率抽一名（持久随机源，单人也抽一次、0 名不抽），
/// 意图只公开范围；扫击按原范围全体、迫近只打最前暴露者；预览不抽，读档重放同一结果。
/// 出处 R03/A09/A06/S01/S14「Q91」本轮章、combatResolutionAdjudications.enemyTargetSelection；验收 PTY-Q91a—c。
/// 队友入场随 B9 多人战斗，这里用测试直接把林昭放进我方号位来构造多人场面。
/// </summary>
public class B8bTargetTests : BattleTestBase
{
    private const string Lin = "npc_lin_zhao";

    private static JObject Sel() =>
        (JObject)JObject.Parse(File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "design.a12.json")))["combatResolutionAdjudications"]!["enemyTargetSelection"]!;

    /// <summary>入战后把林昭放进我方号位（slots 为 1—4 号位的 actorId，空位写 ""）。</summary>
    private GameState Duo(uint seed, string[] slots, params string[] enemies)
    {
        var s = Fight(seed, enemies);
        Assert.That(E.AddNamedToRoster(s, Lin, false).Ok, Is.True);
        s.Party.Add(Lin);
        var list = slots.ToList();
        while (list.Count > 0 && list[^1] == "") list.RemoveAt(list.Count - 1);
        s.Battle!.Allies = list;
        return s;
    }

    private void Attack(GameState s, BattleEnemy e) =>
        typeof(GameEngine).GetMethod("DoAttackStep", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(E, new object[] { s, s.Battle!, e, E.StepOf(e), false, 1.0, false, false });

    private void Activate(GameState s, BattleEnemy e) =>
        typeof(GameEngine).GetMethod("EnemyActivation", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(E, new object[] { s, e, false });

    private static BattleEnemy Ready(GameState s, int index, int slot)
    {
        var e = Enemy(s, index);
        e.Slot = slot.ToString();
        e.Intent = "attack";
        e.IntentStep = -2;
        e.Acted = false;
        e.RoundApUsed = 0;
        return e;
    }

    /// <summary>PTY-Q91a：两名合法目标时等概率（按真实槽升序映射）、每次独立攻击各抽、允许重复；抽中队友时打队友本人并公开抽中的人与号位。</summary>
    [Test]
    public void PTY_Q91a_UniformAmongLegalLiving_EachAttackRedraws_RepeatsAllowed()
    {
        var sel = Sel();
        Assert.That((string)sel["selection"]!, Is.EqualTo("uniform_from_current_legal_living_actorIds_sorted_by_actual_slot"));
        Assert.That((bool)sel["redrawEveryIndependentAttack"]!, Is.True);
        Assert.That((bool)sel["repeatsAllowed"]!, Is.True);

        var s = Duo(9101, new[] { "p0", Lin }, "shambler", "shambler");
        var e1 = Ready(s, 1, 1);
        var e2 = Ready(s, 2, 2);
        Assert.That(GameEngine.EnemyTargetCandidates(s.Battle!, new HashSet<int> { 1, 2 }), Is.EqualTo(new[] { "p0", Lin }), "候选按真实槽升序");

        // 抽值 <1/2 → 1 号位的你；≥1/2 → 2 号位的林昭（两次独立攻击各抽一次，第二只抽到林昭、命中扣林昭本人的健康）
        var hp = s.Stats.Health;
        var linHp = s.Actors[Lin].Stats.Health;
        Force(s, Below(0.5), AtLeast(0.999), AtLeast(0.5), Below(0.001), Any());
        Attack(s, e1);
        Assert.That(s.Battle!.Log.Last(), Does.Contain("落空"));
        Assert.That(s.Battle.Log[^2], Does.Contain("随机选中你（1号位；你、林昭各1/2）"));
        Attack(s, e2);
        Assert.That(s.Battle.Log.Any(l => l.Contains("随机选中林昭（2号位；你、林昭各1/2）")), Is.True);
        Assert.That(s.Battle.Log.Last(), Does.Contain("命中林昭"));
        Assert.That(s.Actors[Lin].Stats.Health, Is.LessThan(linHp));
        Assert.That(s.Stats.Health, Is.EqualTo(hp), "没抽中你就不扣你");

        // 允许重复：两次都抽到你
        Force(s, Below(0.5), AtLeast(0.999), Below(0.5), AtLeast(0.999));
        var n0 = s.Battle.Log.Count;
        Attack(s, e1);
        Attack(s, e2);
        Assert.That(s.Battle.Log.Skip(n0).Count(l => l.Contains("随机选中你")), Is.EqualTo(2));

        // 等概率：600 次独立攻击两人各约一半
        var picks = new Dictionary<string, int> { ["你"] = 0, ["林昭"] = 0 };
        for (uint k = 1; k <= 600; k++)
        {
            var c = s.Clone();
            c.Rng.State = k * 2654435761u + 13;
            var ce = c.Battle!.Enemies.Single(x => x.Index == 1);
            var before = c.Battle.Log.Count;
            Attack(c, ce);
            var line = c.Battle!.Log.Skip(before).First(l => l.Contains("随机选中"));
            picks[line.Contains("随机选中你") ? "你" : "林昭"]++;
        }
        Assert.That(picks["你"], Is.InRange(240, 360));
        Assert.That(picks["林昭"], Is.InRange(240, 360));
    }

    /// <summary>PTY-Q91b：只有 1 名合法目标也抽一次（消耗一次随机）；0 名不抽、不攻击；意图文字/预演只公开范围、不推进随机。</summary>
    [Test]
    public void PTY_Q91b_SingletonDraws_EmptyNoDraw_PreviewNoDraw_IntentShowsRange()
    {
        var sel = Sel();
        Assert.That((bool)sel["singletonConsumesOneTargetDraw"]!, Is.True);
        Assert.That((bool)sel["emptyConsumesNoTargetDraw"]!, Is.True);

        // 单人：抽目标 1 次 + 命中 1 次（落空），之后的随机不动
        var s = Fight(9102, "shambler");
        var e = Ready(s, 1, 1);
        Force(s, Draw(), AtLeast(0.999));
        var r0 = s.Rng.State;
        var b = s.Battle!;
        Assert.That(E.IntentTargetNote(s, b, e), Is.EqualTo("攻击时在1、2号位的存活我方中随机选一名（现在可选：你）"));
        E.IntentShown(b, e);
        E.PlanFor(b, e, E.StepOf(e));
        E.PreviewCommand(s, BattleCommand.Of("basic_attack", 1));
        Assert.That(s.Rng.State, Is.EqualTo(r0), "看意图/预演不推进随机");
        Attack(s, e);
        var probe = new Rng { State = r0 };
        probe.NextDouble();
        probe.NextDouble();
        Assert.That(s.Rng.State, Is.EqualTo(probe.State), "单人：目标抽 1 次 + 命中 1 次");
        Assert.That(b.Log.Last(), Does.Contain("落空"));
        Assert.That(b.Log.Any(l => l.Contains("随机选中")), Is.False, "只有你一人时不写抽签日志");

        // 两人：意图写范围与当前可选、各 1/2
        var d = Duo(9103, new[] { "p0", Lin }, "shambler");
        var de = Ready(d, 1, 1);
        Assert.That(E.IntentTargetNote(d, d.Battle!, de), Is.EqualTo("攻击时在1、2号位的存活我方中随机选一名（现在可选：你、林昭，各1/2）"));

        // 0 名合法目标：不抽、不攻击（你在 3 号位，抓击只打 1、2 号位）
        StandAt(s, 3);
        var r1 = s.Rng.State;
        Attack(s, e);
        Assert.That(s.Rng.State, Is.EqualTo(r1), "0 目标不消费随机");
        Assert.That(b.Log.Last(), Does.Contain("落在空处"));
    }

    /// <summary>
    /// PTY-Q91c：读档重试复用同一游标，抽中的人与结果不变；迫近只打最前暴露者（单元素集合仍抽一次）；扫击按原范围打 1、2 号位全体、不抽签。
    /// </summary>
    [Test]
    public void PTY_Q91c_RetryReusesCursor_ApproachFrontmost_SweepWholeRange()
    {
        var sel = Sel();
        Assert.That((string)sel["aoe"]!, Is.EqualTo("original_full_range_no_single_target_draw"));
        Assert.That((string)sel["exposureApproach"]!, Does.StartWith("original_foremost_exposed_target_constraint_singleton"));

        // 读档重试：同一份存档两次执行同一敌人激活，抽中的人、伤害、随机游标都相同
        var s = Duo(9104, new[] { "p0", Lin }, "shambler");
        Ready(s, 1, 1);
        Force(s, AtLeast(0.5), Below(0.001), Any());
        var json = SaveSerializer.Serialize(s);
        var runs = Enumerable.Range(0, 2).Select(_ =>
        {
            var c = SaveSerializer.Deserialize(json, Cfg);
            var n = c.Battle!.Log.Count;
            Activate(c, c.Battle.Enemies.Single(x => x.Index == 1));
            return (log: string.Join("|", c.Battle!.Log.Skip(n)), rng: c.Rng.State, lin: c.Actors[Lin].Stats.Health, me: c.Stats.Health);
        }).ToList();
        Assert.That(runs[1], Is.EqualTo(runs[0]), "重试不换人、不重抽");
        Assert.That(runs[0].log, Does.Contain("随机选中林昭"));

        // 迫近：1、2 号位都空，最前的是 3 号位的林昭——只打他（候选 1 人仍抽一次），不打 4 号位的你
        var a = Duo(9105, new[] { "", "", Lin, "p0" }, "shambler");
        var z = Ready(a, 1, 1);
        var ab = a.Battle!;
        Assert.That(E.PlanFor(ab, z, E.StepOf(z)), Is.EqualTo(GameEngine.ReachPlan.Approach));
        Assert.That(E.IntentShown(ab, z), Does.StartWith("迫近最前的同伴（3号位，算主动前移）"), "B8d Q97：你没架矛");
        Assert.That(E.IntentTargetNote(a, ab, z), Is.EqualTo("迫近只打最前的林昭（3号位）"));
        var hp = a.Stats.Health;
        var lin0 = a.Actors[Lin].Stats.Health;
        Force(a, Draw(), Below(0.001), Any());
        Activate(a, z);
        Assert.That(ab.Log.Any(l => l.Contains("迫近3号位的林昭")), Is.True, string.Join(" | ", ab.Log.TakeLast(4)));
        Assert.That(a.Actors[Lin].Stats.Health, Is.LessThan(lin0));
        Assert.That(a.Stats.Health, Is.EqualTo(hp));
        Assert.That(ab.Log.Any(l => l.Contains("随机选中")), Is.False, "迫近候选只有 1 人，不写抽签");

        // 扫击：范围攻击打 1、2 号位全体，不抽签
        var w = Duo(9106, new[] { "p0", Lin }, "z_relay_guard");
        var g = Ready(w, 1, 1);
        g.Intent = "sweep";
        g.IntentStep = 0;
        Assert.That(E.StepOf(g).Kind, Is.EqualTo("sweep"));
        Assert.That(E.IntentTargetNote(w, w.Battle!, g), Does.Contain("全部我方"));
        Force(w, Below(0.001), Any(), Below(0.001), Any());
        var n0 = w.Battle!.Log.Count;
        Attack(w, g);
        var lines = w.Battle!.Log.Skip(n0).ToList();
        Assert.That(lines.Any(l => l.Contains("随机选中")), Is.False);
        Assert.That(lines.Count(l => l.Contains("命中")), Is.EqualTo(2), "你和林昭各判一次：" + string.Join(" | ", lines));
    }
}
