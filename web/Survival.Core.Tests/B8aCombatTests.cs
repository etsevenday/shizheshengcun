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

    // ───────────── R102：迫近视作主动前移、触发架矛（Q85） ─────────────

    private void EnemyActPursuit(GameState s, BattleEnemy e) =>
        typeof(GameEngine).GetMethod("EnemyActivation", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(E, new object[] { s, e, true });

    /// <summary>主角架好木矛（123→123）：迎击次数 n、倍率 1、武器Ⅰ。</summary>
    private void Brace(GameState s, int charges)
    {
        Arm(s, "wooden_spear");
        var b = s.Battle!;
        b.BraceCharges = charges;
        b.BraceFactor = 1;
        b.BracePage = 1;
    }

    private static int LogIndex(BattleState b, string text, int from = 0)
    {
        for (var i = Math.Max(0, from); i < b.Log.Count; i++) if (b.Log[i].Contains(text)) return i;
        return -1;
    }

    /// <summary>
    /// POS-Q85-01：迫近 1+2AP 足额才做；先扣 1AP、双方号位都不变，派发一次主动前移事件触发架矛，合法迎击一次且先于弱近袭；
    /// 意图写明“迫近（触发架矛）→弱近袭”；一次迫近只派发一次（剩余次数 2 也只用 1 次）。
    /// </summary>
    [Test]
    public void POS_Q85_01_Approach_PaysOne_BraceOnceBeforeWeakStrike_SlotsUnchanged()
    {
        var ea = (JObject)Res()["enemyActivation"]!["exposureApproach"]!;
        Assert.That((bool)ea["countsAsActiveForwardMove"]!, Is.True);
        Assert.That((bool)ea["triggersSpearInterception"]!, Is.True);
        Assert.That((bool)ea["changesAttackerActualSlot"]!, Is.False);
        Assert.That((bool)ea["oneEventPerApproach"]!, Is.True);
        Assert.That((string)ea["eventTiming"]!, Is.EqualTo("after_pay_1AP_before_revalidate_and_pay_2AP_attack"));

        var s = Fight(8501, "crawler", "shambler");
        Ready(s);
        var b = s.Battle!;
        var z = Enemy(s, 1);
        Place(s, (2, 4));
        z.Hp = 50;
        z.Intent = "attack";
        z.IntentStep = -2;
        StandAt(s, 3);
        Brace(s, 2);
        Assert.That(E.PlanFor(b, z, E.StepOf(z)), Is.EqualTo(GameEngine.ReachPlan.Approach));
        Assert.That(E.IntentShown(b, z), Does.StartWith("迫近你（3号位，触发架矛）→弱近袭"));
        var n0 = b.Log.Count;
        var minute = s.GameMinute;
        Force(s, Below(0.01), AtLeast(0.99), Any(), Below(0.01), Any());   // 迎击命中、不暴击、抽值；弱近袭命中、抽值
        EnemyAct(s, z);
        var approach = LogIndex(b, "迫近3号位的你", n0);
        var brace = LogIndex(b, "你迎击", n0);
        var strike = LogIndex(b, "弱近袭", approach + 1);
        Assert.That(approach, Is.GreaterThanOrEqualTo(0), string.Join(" | ", b.Log.Skip(n0)));
        Assert.That(brace, Is.GreaterThan(approach), "付 1AP 后派发主动前移事件，迎击在弱近袭之前");
        Assert.That(strike, Is.GreaterThan(brace));
        Assert.That(b.BraceCharges, Is.EqualTo(1), "一次迫近只派发一次：剩余 2 次只用 1 次");
        Assert.That(z.Pos, Is.EqualTo(1), "迫近者号位不变");
        Assert.That(GameEngine.PlayerPos(b), Is.EqualTo(3), "你的号位不变");
        Assert.That(z.RoundApUsed, Is.EqualTo(3), "迫近 1 + 弱近袭 2");
        Assert.That(s.GameMinute, Is.EqualTo(minute), "同一次激活，不额外推进时间");
        Assert.That(E.PlanFor(b, z, E.StepOf(z), 2), Is.Not.EqualTo(GameEngine.ReachPlan.Approach), "只有 2AP（追击）付不起 1+2");
    }

    /// <summary>
    /// POS-Q85-02：迎击致死时，未执行的 2AP 不扣、已花 1AP 不返；迎击来源页够不到（非法己位）时不耗架势次数，弱近袭照常；
    /// 追击不迫近；迎击不产生反应链。
    /// </summary>
    [Test]
    public void POS_Q85_02_BraceKills_NoChargeForStrike_IllegalSourceKeepsCharges_NoPursuitApproach()
    {
        var em = (JObject)Res()["enemyActivation"]!["exposureApproach"]!;
        Assert.That((string)em["interrupted"]!, Is.EqualTo("keep_spent_approach_AP_do_not_charge_unexecuted_attack_AP_end"));

        // 迎击打死迫近者
        var s = Fight(8502, "crawler", "shambler");
        Ready(s);
        var b = s.Battle!;
        var z = Enemy(s, 1);
        Place(s, (2, 4));
        z.Hp = 0.01;
        z.Intent = "attack";
        z.IntentStep = -2;
        StandAt(s, 3);
        Brace(s, 1);
        var h0 = s.Stats.Health;
        var n0 = b.Log.Count;
        Force(s, Below(0.01), AtLeast(0.99), Any());
        EnemyAct(s, z);
        Assert.That(z.Dead, Is.True, string.Join(" | ", b.Log.Skip(n0)));
        Assert.That(z.RoundApUsed, Is.EqualTo(1), "已付迫近 1AP 不返，未发出的弱近袭 2AP 不扣");
        Assert.That(s.Stats.Health, Is.EqualTo(h0), "近袭取消");
        Assert.That(b.Log.Skip(n0).Any(l => l.Contains("弱近袭命中") || l.Contains("弱近袭落空")), Is.False);

        // 来源页够不到：手斧（12→1）在 3 号位不能迎击，不耗次数；弱近袭照常
        var t = Fight(8503, "crawler", "shambler");
        Ready(t);
        var tb = t.Battle!;
        var y = Enemy(t, 1);
        Place(t, (2, 4));
        y.Hp = 50;
        y.Intent = "attack";
        y.IntentStep = -2;
        StandAt(t, 3);
        Arm(t, "hatchet");
        tb.BraceCharges = 1;
        tb.BraceFactor = 1;
        tb.BracePage = 1;
        var m0 = tb.Log.Count;
        EnemyAct(t, y);
        Assert.That(tb.BraceCharges, Is.EqualTo(1), "非法来源/射程不耗架势次数");
        Assert.That(tb.Log.Skip(m0).Any(l => l.Contains("你的迎击没有出手")), Is.True);
        Assert.That(tb.Log.Skip(m0).Any(l => l.Contains("弱近袭命中") || l.Contains("弱近袭落空")), Is.True, "迎击没发生，弱近袭照常");
        Assert.That(y.RoundApUsed, Is.EqualTo(3));

        // 追击 2AP 不迫近
        y.RoundApUsed = 0;
        var k0 = tb.Log.Count;
        EnemyActPursuit(t, y);
        Assert.That(tb.Log.Skip(k0).Any(l => l.Contains("迫近")), Is.False, "追击只有 2AP，不能迫近");
        Assert.That(y.RoundApUsed, Is.EqualTo(0), "追击 AP 不进本轮正常预算账");
    }

    // ───────────── R104：前方同伴主动补位（Q87） ─────────────

    /// <summary>两只游荡尸：F 在 2、当前敌在 3、1 号位空，你在 1 号位（游荡尸 12→12：F 已能出手，补到 1 仍能出手）。</summary>
    private GameState Column(uint seed, out BattleEnemy f, out BattleEnemy e)
    {
        var s = Fight(seed, "shambler", "shambler");
        Ready(s);
        f = Enemy(s, 1);
        e = Enemy(s, 2);
        foreach (var x in s.Battle!.Alive) { x.Hp = 50; x.Intent = "attack"; x.IntentStep = -2; x.Acted = false; x.RoundApUsed = 0; }
        Place(s, (1, 2), (2, 3));
        StandAt(s, 1);
        return s;
    }

    /// <summary>
    /// POS-Q87-01：F 在 2、敌在 3、1 空，F 本轮 3AP 已攻击花 2 余 1：F 付自己的 1 补到 1，敌付自己的 1 到 2，余 2 才攻击；
    /// 不改队列、不多激活、F 的已行动标记不变；意图写明补位者与当前敌各自花费。
    /// </summary>
    [Test]
    public void POS_Q87_01_ActedHelperUsesLeftover_MoverFollowsAndAttacks()
    {
        var ca = (JObject)Res()["enemyActivation"]!["cooperativeAdvance"]!;
        Assert.That(((int)ca["helperAP"]!, (int)ca["moverAP"]!), Is.EqualTo((Cfg.CombatMath.CooperativeAdvance.HelperAp, Cfg.CombatMath.CooperativeAdvance.MoverAp)));
        Assert.That((bool)ca["extraActivation"]!, Is.False);
        Assert.That((bool)ca["queueOrderChanges"]!, Is.False);
        Assert.That((int)ca["maxHelperRequestsPerActivation"]!, Is.EqualTo(1));

        var s = Column(8701, out var f, out var e);
        var b = s.Battle!;
        f.Acted = true;
        f.RoundApUsed = 2;   // 已攻击花 2，余 1
        Assert.That(E.AssistFor(b, e, out var who), Is.EqualTo(GameEngine.AssistKind.Assist));
        Assert.That(who, Is.SameAs(f));
        Assert.That(E.IntentShown(b, e), Does.StartWith($"等{E.EnemyName(f)}补到1号位（它花自己1AP），再前移到2号位（1AP）后"));
        var q = b.Queue.ToList();
        var cursor = b.Cursor;
        var n0 = b.Log.Count;
        var minute = s.GameMinute;
        EnemyAct(s, e);
        Assert.That((f.Pos, e.Pos), Is.EqualTo((1, 2)), string.Join(" | ", b.Log.Skip(n0)));
        Assert.That(f.RoundApUsed, Is.EqualTo(3), "F 用掉自己剩下的 1AP");
        Assert.That(E.EnemyApLeft(f), Is.EqualTo(0));
        Assert.That(e.RoundApUsed, Is.EqualTo(3), "当前敌：跟进 1 + 攻击 2");
        Assert.That(b.Log.Skip(n0).Any(l => l.Contains("主动补到1号位")), Is.True);
        Assert.That(b.Log.Skip(n0).Any(l => l.Contains("跟进到2号位")), Is.True);
        Assert.That(b.Log.Last(), Does.Contain(E.EnemyName(e)), "跟进后余 2AP 普通攻击一次");
        Assert.That(b.Queue, Is.EqualTo(q), "不改锁定队列");
        Assert.That(b.Cursor, Is.EqualTo(cursor));
        Assert.That(f.Acted, Is.True);
        Assert.That(s.GameMinute, Is.EqualTo(minute), "同一次激活，不额外推进时间");
    }

    /// <summary>
    /// POS-Q87-02：未行动的 F 先预扣 1，轮到它时只剩 2（不重置成 3）；F 余 0 时当前敌等待、不换位；
    /// 追击激活不发起补位（沿 Q78 交换），也不动 F 的本轮账；不递归推动更前的人（F 前面有人就按 Q78 交换）。
    /// </summary>
    [Test]
    public void POS_Q87_02_UnactedHelperReserved_NoBudgetWaits_NoPursuit_NoRecursion()
    {
        var s = Column(8702, out var f, out var e);
        var b = s.Battle!;
        EnemyAct(s, e);
        Assert.That((f.Pos, e.Pos), Is.EqualTo((1, 2)));
        Assert.That(f.RoundApUsed, Is.EqualTo(1), "未行动的 F 预扣 1");
        Assert.That(f.Acted, Is.False, "F 不是新激活");
        Assert.That(E.EnemyApLeft(f), Is.EqualTo(2), "轮到 F 时只剩 2，不重置成 3");
        Assert.That(b.Log.Any(l => l.Contains("轮到它时只剩2AP")), Is.True);
        var activate = typeof(GameEngine).GetMethod("EnemyActivation", BindingFlags.NonPublic | BindingFlags.Instance)!;
        activate.Invoke(E, new object[] { s, f, false });   // 不清账：F 的正常激活从剩下的 2AP 开始
        Assert.That(f.RoundApUsed, Is.EqualTo(3), "F 正常激活用剩下的 2AP 攻击一次");

        // F 余 0：当前敌等待，不把 F 换下去
        var t = Column(8703, out var f2, out var e2);
        var tb = t.Battle!;
        f2.Acted = true;
        f2.RoundApUsed = 3;
        Assert.That(E.AssistFor(tb, e2, out _), Is.EqualTo(GameEngine.AssistKind.Wait));
        Assert.That(E.IntentShown(tb, e2), Does.StartWith("等待（"));
        EnemyAct(t, e2);
        Assert.That((f2.Pos, e2.Pos), Is.EqualTo((2, 3)), "等待：不换位、不免费推人");
        Assert.That(e2.RoundApUsed, Is.EqualTo(0), "等待不花 AP");
        Assert.That(e2.Guard, Is.False, "不自动防御");
        Assert.That(tb.Log.Last(), Does.Contain("等待（不换位）"));

        // 追击：不发起补位，沿 Q78 交换，F 的本轮账不动
        var u = Column(8704, out var f3, out var e3);
        EnemyActPursuit(u, e3);
        Assert.That((f3.Pos, e3.Pos), Is.EqualTo((3, 2)), "追击按 Q78 相邻交换");
        Assert.That(f3.RoundApUsed, Is.EqualTo(0), "不借追击 AP、不动 F 的正常预算");

        // 不递归：F 前面（1 号位）还有同伴 → 不推更前的人，按 Q78 与 F 交换
        var v = Fight(8705, "shambler", "shambler", "shambler");
        Ready(v);
        foreach (var x in v.Battle!.Alive) { x.Hp = 50; x.Intent = "attack"; x.IntentStep = -2; x.Acted = false; x.RoundApUsed = 0; }
        Place(v, (3, 1), (1, 2), (2, 3));
        StandAt(v, 1);
        Assert.That(E.AssistFor(v.Battle!, Enemy(v, 2), out _), Is.EqualTo(GameEngine.AssistKind.None));
        EnemyAct(v, Enemy(v, 2));
        Assert.That((Enemy(v, 3).Pos, Enemy(v, 1).Pos, Enemy(v, 2).Pos), Is.EqualTo((1, 3, 2)), "不推 1 号位的人；与 F 相邻交换");
        Assert.That(Enemy(v, 1).RoundApUsed, Is.EqualTo(0), "被动交换不花 F 的 AP");
    }

    /// <summary>
    /// POS-Q87-03：F 补位与当前敌跟进各触发一次架矛；迎击打死 F 后重验，前格已空、当前敌照常跟进；当前敌跟进时被打死则不攻击、
    /// 已付的跟进 1AP 不返、没执行的攻击 2AP 不扣；整次激活不额外推进时间。
    /// </summary>
    [Test]
    public void POS_Q87_03_EachVoluntaryStepTriggersBrace_RevalidateAfterReaction()
    {
        var ca = (JObject)Res()["enemyActivation"]!["cooperativeAdvance"]!;
        Assert.That((bool)ca["eachVoluntaryStepTriggersInterception"]!, Is.True);
        Assert.That((bool)ca["passiveSwapTriggersInterception"]!, Is.False);
        Assert.That((bool)ca["extraWorldTick"]!, Is.False);

        // 两次迎击都出手
        var s = Column(8711, out var f, out var e);
        var b = s.Battle!;
        Brace(s, 2);
        var n0 = b.Log.Count;
        Force(s, Below(0.3), AtLeast(0.6), Any(), Below(0.3), AtLeast(0.6), Any());
        EnemyAct(s, e);
        Assert.That(b.BraceCharges, Is.EqualTo(0), "F 补位、当前敌跟进各触发一次：" + string.Join(" | ", b.Log.Skip(n0)));
        Assert.That(b.Log.Skip(n0).Count(l => l.Contains("你迎击")), Is.EqualTo(2));

        // 迎击打死 F：已发生的补位与 F 的 1AP 不回滚，前格已空，当前敌照常跟进
        var t = Column(8712, out var f2, out var e2);
        var tb = t.Battle!;
        f2.Hp = 0.01;
        Brace(t, 1);
        var m0 = t.GameMinute;
        var t0 = tb.Log.Count;
        Force(t, Below(0.3), AtLeast(0.6), Any());
        EnemyAct(t, e2);
        Assert.That(f2.Dead, Is.True, string.Join(" | ", tb.Log.Skip(t0)));
        Assert.That(f2.RoundApUsed, Is.EqualTo(1), "F 已付的 1AP 不回滚");
        Assert.That(e2.Pos, Is.EqualTo(2), "重验：前格空、自己有 1AP，跟进");
        Assert.That(t.GameMinute, Is.EqualTo(m0));

        // 当前敌跟进时被迎击打死：不攻击，跟进 1AP 已付、攻击 2AP 不扣
        var u = Column(8713, out var f3, out var e3);
        var ub = u.Battle!;
        e3.Hp = 0.01;
        Brace(u, 2);
        var u0 = ub.Log.Count;
        Force(u, AtLeast(0.99), Below(0.3), AtLeast(0.6), Any());   // 第一次迎击（打 F）落空，第二次（打跟进者）命中
        var h0 = u.Stats.Health;
        EnemyAct(u, e3);
        Assert.That(e3.Dead, Is.True, string.Join(" | ", ub.Log.Skip(u0)));
        Assert.That(e3.RoundApUsed, Is.EqualTo(1), "跟进已付 1AP，攻击未执行不扣");
        Assert.That(u.Stats.Health, Is.EqualTo(h0), "跟进者死了，不攻击");
        Assert.That(f3.Pos, Is.EqualTo(1));
    }

    /// <summary>POS-Q87-04：不满足补位条件（F 现在不能普通攻击，例如中立未激怒）时沿 Q78 相邻交换，被动交换不触发架矛、不花 F 的 AP。</summary>
    [Test]
    public void POS_Q87_04_GeometryNotMet_FallsBackToQ78Swap_PassiveNoBrace()
    {
        var ca = (JObject)Res()["enemyActivation"]!["cooperativeAdvance"]!;
        Assert.That((string)ca["otherGeometry"]!, Is.EqualTo("original_Q78_adjacent_move_or_swap"));
        Assert.That((bool)ca["recursive"]!, Is.False);

        var s = Column(8721, out var f, out var e);
        var b = s.Battle!;
        f.Hostile = false;   // 不会主动攻击：F 现在不能合法普通攻击
        Brace(s, 2);
        Assert.That(E.AssistFor(b, e, out _), Is.EqualTo(GameEngine.AssistKind.None));
        Force(s, AtLeast(0.99), Any(), Any());   // 当前敌前移的那一次迎击落空
        EnemyAct(s, e);
        Assert.That((f.Pos, e.Pos), Is.EqualTo((3, 2)), "沿 Q78 相邻交换");
        Assert.That(f.RoundApUsed, Is.EqualTo(0), "被动交换不花 F 的 AP");
        Assert.That(b.BraceCharges, Is.EqualTo(1), "只有当前敌的主动前移触发一次，被换下去的 F 不触发");
    }
}
