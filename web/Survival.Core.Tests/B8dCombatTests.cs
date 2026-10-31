using System.Reflection;
using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 B8d（策划提交 572f24a 答复 Q95–Q97）战斗部分：R109 非攻击主意图 AP 账（Q95）、R110 协同补位的普通攻击资格（Q96）、
/// R111 迫近意图括注随架矛状态（Q97）。出处：R03「Q95—Q97 敌方AP、补位资格与迫近提示（2026-10-31，当前优先）」、R05/A09/A06 同轮章；
/// 验收 A10「Q95—Q98 兼容性验收」AI-Q95-01—02、AI-Q96-01—02、UI-Q97-01—02。
/// </summary>
public class B8dCombatTests : BattleTestBase
{
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

    private static void Reset(GameState s)
    {
        foreach (var x in s.Battle!.Alive) { x.Hp = 50; x.Intent = "attack"; x.IntentStep = -2; x.Acted = false; x.RoundApUsed = 0; x.Guard = false; }
    }

    private void EnemyAct(GameState s, BattleEnemy e) =>
        typeof(GameEngine).GetMethod("EnemyActivation", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(E, new object[] { s, e, false });

    private static void SetIntent(BattleEnemy e, string intent)
    {
        e.Intent = intent;
        e.IntentStep = -1;   // StepOf 按意图找同类步骤
    }

    // ───────────── R109：非攻击主意图 AP 账（Q95） ─────────────

    /// <summary>
    /// AI-Q95-01：防御/嚎叫/蓄力/保护/鸣响/增益各 2AP、装填 1AP、逃离/警戒 0AP；只做这一个主意图，不自动追加动作、不额外激活；
    /// 普通 3AP 敌人嚎叫后余 1AP，仍可替后方同伴补位一次；够不到又付不起迫近时的合法兜底防御也是 2AP。
    /// </summary>
    [Test]
    public void AI_Q95_01_NonAttackIntentCosts_LeftoverCanAssist_NoExtraAction()
    {
        // 各意图花费（在 1 号位、你在 1 号位，不涉及够不到）
        foreach (var (enemy, intent, ap) in new[] { ("riot", "guard", 2), ("howler", "howl", 2), ("boar", "charge_prepare", 2), ("z_signal", "signal", 2), ("wild_dog_leader", "buff", 2), ("raider_gunner", "reload", 1) })
        {
            var s = Fight(9501, enemy);
            Ready(s);
            Reset(s);
            var z = Enemy(s, 1);
            Place(s, (1, 1));
            StandAt(s, 1);
            if (intent == "reload") { z.Mag = 0; z.Reserve = 4; }
            SetIntent(z, intent);
            var h0 = s.Stats.Health;
            var n0 = s.Battle!.Log.Count;
            EnemyAct(s, z);
            Assert.That(z.RoundApUsed, Is.EqualTo(ap), $"{enemy} {intent}：" + string.Join(" | ", s.Battle!.Log.Skip(n0)));
            Assert.That(s.Battle!.Log.Count - n0, Is.EqualTo(1), $"{enemy} {intent} 只做一个主意图、一条日志，不追加攻击/防御");
            Assert.That(s.Stats.Health, Is.EqualTo(h0), "非攻击意图不打人");
            if (intent != "guard") Assert.That(z.Guard, Is.False, "不自动追加防御");
            if (intent == "reload") Assert.That((z.Mag, z.Reserve), Is.EqualTo((2, 2)), "装填 1AP 装满弹仓，不另射击");
        }

        // 保护 2AP（相邻同伴）
        {
            var s = Fight(9502, "z_guard", "shambler");
            Ready(s);
            Reset(s);
            var g = Enemy(s, 1);
            Place(s, (1, 1), (2, 2));
            StandAt(s, 1);
            SetIntent(g, "protect");
            g.IntentTarget = 2;
            EnemyAct(s, g);
            Assert.That((g.RoundApUsed, g.ProtectingIndex), Is.EqualTo((2, 2)));
        }

        // 逃离/警戒 0AP
        {
            var s = Fight(9503, "shambler", "shambler");
            Ready(s);
            Reset(s);
            var a = Enemy(s, 1);
            var w = Enemy(s, 2);
            SetIntent(a, "flee");
            w.Hostile = false;
            SetIntent(w, "watch");
            EnemyAct(s, w);
            Assert.That(w.RoundApUsed, Is.EqualTo(0), "警戒 0AP");
            EnemyAct(s, a);
            Assert.That((a.Fled, a.RoundApUsed), Is.EqualTo((true, 0)), "逃离 0AP");
        }

        // 嚎叫后余 1AP：后方同伴够不到时，它用剩下的 1AP 主动补位（Q87），不额外激活
        {
            var s = Fight(9504, "howler", "shambler");
            Ready(s);
            Reset(s);
            var f = Enemy(s, 1);
            var e = Enemy(s, 2);
            Place(s, (1, 2), (2, 3));
            StandAt(s, 1);
            SetIntent(f, "howl");
            var panic = s.Battle!.Panic;
            EnemyAct(s, f);
            Assert.That(f.RoundApUsed, Is.EqualTo(2));
            Assert.That(s.Battle!.Panic, Is.GreaterThan(panic), "嚎叫照常执行");
            Assert.That(E.EnemyApLeft(f), Is.EqualTo(1));
            Assert.That(E.AssistFor(s.Battle!, e, out var who), Is.EqualTo(GameEngine.AssistKind.Assist), "余 1AP 可替后方补位一次");
            Assert.That(who, Is.SameAs(f));
            EnemyAct(s, e);
            Assert.That((f.Pos, e.Pos), Is.EqualTo((1, 2)));
            Assert.That(f.RoundApUsed, Is.EqualTo(3), "F 用掉剩下的 1AP，不重置、不借下一轮");
        }

        // 合法兜底防御 2AP：在己位够不到你、迫近付不起（本轮已预扣 1，只剩 2 < 1+2）
        {
            var s = Fight(9505, "crawler");
            Ready(s);
            Reset(s);
            var z = Enemy(s, 1);
            Place(s, (1, 1));
            StandAt(s, 3);
            z.RoundApUsed = 1;
            Assert.That(E.PlanFor(s.Battle!, z, E.StepOf(z)), Is.EqualTo(GameEngine.ReachPlan.Guard));
            Assert.That(E.IntentShown(s.Battle!, z), Is.EqualTo("防御（够不到目标）"));
            EnemyAct(s, z);
            Assert.That((z.Guard, z.RoundApUsed), Is.EqualTo((true, 3)), "兜底防御 2AP");
        }
    }

    /// <summary>
    /// AI-Q95-02：还没行动就被预扣 2AP 的普通 3AP 敌人只剩 1AP，轮到它时 2AP 主意图付不起：本次结束，不执行、不扣未发生费用、不换意图、
    /// 不追加防御，已扣的补位成本不返；敌卡与日志都写实际剩余、预扣量和不够执行的意图。装填 1AP 余 1 照常，余 0 也结束。不抽随机。
    /// </summary>
    [Test]
    public void AI_Q95_02_PrepaidTwo_LeftOne_MainIntentRefused_NoCost_NoSwap()
    {
        var s = Fight(9510, "howler");
        Ready(s);
        Reset(s);
        var b = s.Battle!;
        var z = Enemy(s, 1);
        Place(s, (1, 1));
        StandAt(s, 1);
        SetIntent(z, "howl");
        z.RoundApUsed = 2;   // 替同伴补位预扣两次
        var label = E.IntentLabel(z, b);
        Assert.That(E.IntentShown(b, z), Is.EqualTo($"等待（本轮只剩1AP，替同伴补位预扣了2AP，不够{label}）"), "敌卡与执行同源");
        var panic = b.Panic;
        var rng = s.Rng.State;
        var n0 = b.Log.Count;
        EnemyAct(s, z);
        Assert.That(b.Log.Skip(n0).Single(), Does.EndWith($"{E.EnemyName(z)}本轮只剩1AP（替同伴补位预扣了2AP），不够{label}，结束行动。"));
        Assert.That(z.RoundApUsed, Is.EqualTo(2), "不扣未发生费用，旧补位成本不返");
        Assert.That(b.Panic, Is.EqualTo(panic), "嚎叫没执行");
        Assert.That((z.Intent, z.Guard), Is.EqualTo(("howl", false)), "不换意图、不自动防御");
        Assert.That(s.Rng.State, Is.EqualTo(rng), "不抽随机");

        // 装填 1AP：余 1 照常装填，余 0 结束
        var t = Fight(9511, "raider_gunner");
        Ready(t);
        Reset(t);
        var g = Enemy(t, 1);
        Place(t, (1, 3));
        StandAt(t, 1);
        g.Mag = 0;
        g.Reserve = 4;
        SetIntent(g, "reload");
        g.RoundApUsed = 3;
        Assert.That(E.IntentShown(t.Battle!, g), Does.StartWith("等待（本轮只剩0AP"));
        EnemyAct(t, g);
        Assert.That((g.Mag, g.RoundApUsed), Is.EqualTo((0, 3)));
        g.Acted = false;
        g.RoundApUsed = 2;
        Assert.That(E.IntentShown(t.Battle!, g), Does.StartWith("装填"));
        EnemyAct(t, g);
        Assert.That((g.Mag, g.RoundApUsed), Is.EqualTo((2, 3)));
    }

    // ───────────── R110：协同补位的普通攻击资格（Q96） ─────────────

    /// <summary>
    /// AI-Q96-01：F 须敌对、存活，且普通攻击步骤（有序意图首个 kind=attack，己位/目标位缺省继承面板）在当前槽与补到 i−2 后都能打到存活对手：
    /// 迅捷感染者（123→123）在 3、2 都能打你 → 补位；攀附感染者（抓击己位继承面板 23）补到 1 号位就不合法 → 沿 Q78 交换；
    /// 中立未激怒、已死、你不在它目标位都不算。判断不抽随机。
    /// </summary>
    [Test]
    public void AI_Q96_01_PlainAttackSlots_HostileAlive_BothSlots()
    {
        var s = Fight(9601, "z_darter", "shambler");
        Ready(s);
        Reset(s);
        var b = s.Battle!;
        var f = Enemy(s, 1);
        var e = Enemy(s, 2);
        Place(s, (1, 3), (2, 4));
        StandAt(s, 1);
        var rng = s.Rng.State;
        Assert.That(E.AssistFor(b, e, out var who), Is.EqualTo(GameEngine.AssistKind.Assist), "迅捷感染者普通攻击 123→123：3 号位、2 号位都能打你");
        Assert.That(who, Is.SameAs(f));
        Assert.That(s.Rng.State, Is.EqualTo(rng), "资格判断不抽随机");
        f.Hostile = false;
        Assert.That(E.AssistFor(b, e, out _), Is.EqualTo(GameEngine.AssistKind.None), "中立未激怒不算");
        f.Hostile = true;
        f.Dead = true;
        Assert.That(E.AssistFor(b, e, out _), Is.EqualTo(GameEngine.AssistKind.None), "死者不算前方同伴");
        f.Dead = false;
        StandAt(s, 4);
        Assert.That(E.AssistFor(b, e, out _), Is.EqualTo(GameEngine.AssistKind.None), "你不在它普通攻击目标位里（空格不算目标）");

        // 攀附感染者：抓击步骤没写己位，继承面板 23；在 2 号位能打你，补到 1 号位不合法 → 不补位，沿 Q78 相邻交换
        var t = Fight(9602, "z_clinger", "shambler");
        Ready(t);
        Reset(t);
        var tb = t.Battle!;
        var c = Enemy(t, 1);
        var m = Enemy(t, 2);
        Place(t, (1, 2), (2, 3));
        StandAt(t, 1);
        Assert.That(E.AssistFor(tb, m, out _), Is.EqualTo(GameEngine.AssistKind.None));
        EnemyAct(t, m);
        Assert.That((c.Pos, m.Pos), Is.EqualTo((3, 2)), "沿 Q78 相邻交换");
        Assert.That(c.RoundApUsed, Is.EqualTo(0), "被动交换不花 F 的 AP");
    }

    /// <summary>
    /// AI-Q96-02：酸液感染者在 3 号位能照原样喷吐（公开意图能出手），但它的普通攻击（抓击 12→12）在 3 号位不合法 → 不触发补位，按 Q78 交换；
    /// 流窜枪手没有抓击步骤、面板 34 → 近身兜底 12→12，同样不算。资格检查不抽随机、不让 F 攻击、不刷新它的意图。
    /// </summary>
    [Test]
    public void AI_Q96_02_RangedPublicIntentDoesNotQualify_FallsBackToQ78()
    {
        foreach (var (enemy, intent) in new[] { ("z_spitter", "spit"), ("raider_gunner", "shoot") })
        {
            var s = Fight(9610, enemy, "shambler");
            Ready(s);
            Reset(s);
            var b = s.Battle!;
            var f = Enemy(s, 1);
            var e = Enemy(s, 2);
            Place(s, (1, 3), (2, 4));
            StandAt(s, 1);
            SetIntent(f, intent);
            if (intent == "shoot") { f.Mag = 2; f.Reserve = 4; }
            Assert.That(E.PlanFor(b, f, E.StepOf(f)), Is.EqualTo(GameEngine.ReachPlan.Execute), $"{enemy}：公开的{intent}在 3 号位能照原样出手");
            var rng = s.Rng.State;
            Assert.That(E.AssistFor(b, e, out _), Is.EqualTo(GameEngine.AssistKind.None), $"{enemy}：普通攻击己位 12，在 3 号位不算能普通攻击");
            Assert.That(E.IntentShown(b, e), Is.EqualTo("前移1位（在4号位够不到）"));
            Assert.That(s.Rng.State, Is.EqualTo(rng), "资格检查与预览不抽随机");
            var h0 = s.Stats.Health;
            EnemyAct(s, e);
            Assert.That((f.Pos, e.Pos), Is.EqualTo((4, 3)), $"{enemy}：沿 Q78 相邻交换");
            Assert.That(f.RoundApUsed, Is.EqualTo(0), "F 不花 AP");
            Assert.That((f.Intent, f.Acted), Is.EqualTo((intent, false)), "不刷新 F 的意图、不让 F 额外行动");
            Assert.That(s.Stats.Health, Is.EqualTo(h0), "没有额外攻击");
        }
    }

    // ───────────── R111：迫近意图括注随架矛状态（Q97） ─────────────

    /// <summary>主角在 3 号位、爬行尸在 1 号位（12→12）够不到：迫近。</summary>
    private GameState Exposed(uint seed, out BattleEnemy z)
    {
        var s = Fight(seed, "crawler", "shambler");
        Ready(s);
        Reset(s);
        z = Enemy(s, 1);
        Place(s, (1, 1), (2, 4));
        StandAt(s, 3);
        Assert.That(E.PlanFor(s.Battle!, z, E.StepOf(z)), Is.EqualTo(GameEngine.ReachPlan.Approach));
        return s;
    }

    private void Brace(GameState s, string weapon, int charges)
    {
        Arm(s, weapon);
        var b = s.Battle!;
        b.BraceCharges = charges;
        b.BraceFactor = 1;
        b.BracePage = 1;
    }

    /// <summary>
    /// UI-Q97-01：没有矛、或装着矛但没架 → “算主动前移”；架势还有剩余次数 → “触发架矛”；次数清零（用完/到期）后立即改回“算主动前移”。
    /// 先前移再迫近的写法同样；预览不抽随机、不重锁意图。
    /// </summary>
    [Test]
    public void UI_Q97_01_ApproachTag_FollowsBraceState()
    {
        var s = Exposed(9701, out var z);
        var b = s.Battle!;
        var (intent, step, label) = (z.Intent, z.IntentStep, z.IntentLabel);
        var rng = s.Rng.State;
        Assert.That(GameEngine.BraceActive(b), Is.False);
        Assert.That(E.IntentShown(b, z), Does.StartWith("迫近你（3号位，算主动前移）→弱近袭"), "没有矛");
        Assert.That(s.Rng.State, Is.EqualTo(rng), "预览不抽随机");
        Arm(s, "wooden_spear");
        rng = s.Rng.State;   // 造武器实例本身会用随机数，之后只看预览
        Assert.That(E.IntentShown(b, z), Does.StartWith("迫近你（3号位，算主动前移）"), "装着矛但没架：不是架矛状态");
        b.BraceCharges = 2;
        b.BraceFactor = 1;
        b.BracePage = 1;
        Assert.That(GameEngine.BraceActive(b), Is.True);
        Assert.That(E.IntentShown(b, z), Does.StartWith("迫近你（3号位，触发架矛）→弱近袭"), "架势生效");
        Assert.That(GameEngine.ApproachBraceTag(b), Is.EqualTo("触发架矛"));
        b.BraceCharges = 0;   // 用完或到你下次正常激活开始清除
        Assert.That(E.IntentShown(b, z), Does.StartWith("迫近你（3号位，算主动前移）"), "过期/耗尽后即时更新");
        Assert.That(s.Rng.State, Is.EqualTo(rng), "预览不抽随机");
        Assert.That((z.Intent, z.IntentStep, z.IntentLabel), Is.EqualTo((intent, step, label)), "不重锁意图");

        // 先前移再迫近（精英 4AP：1 + 1 + 2）
        var t = Fight(9702, "z_pump_foreman");
        Ready(t);
        Reset(t);
        var p = Enemy(t, 1);
        Place(t, (1, 3));
        StandAt(t, 3);
        Assert.That(E.PlanFor(t.Battle!, p, E.StepOf(p)), Is.EqualTo(GameEngine.ReachPlan.AdvanceThenApproach));
        Assert.That(E.IntentShown(t.Battle!, p), Does.StartWith("前移到2号位，再迫近你（3号位，算主动前移）"));
        Brace(t, "wooden_spear", 1);
        Assert.That(E.IntentShown(t.Battle!, p), Does.StartWith("前移到2号位，再迫近你（3号位，触发架矛）"));
    }

    /// <summary>
    /// UI-Q97-02：提示“触发架矛”但执行时来源页够不到（手斧 12→1 在 3 号位）→ 不迎击、不耗次数，迫近与弱近袭照常 1+2AP、号位不变；
    /// 没架矛时迫近照样是一次主动前移（1+2AP、号位不变，只是没人迎击）；真迎击一次用完次数后，敌卡立即改写“算主动前移”。
    /// </summary>
    [Test]
    public void UI_Q97_02_TagIsDisplayOnly_RealInterceptionRevalidated_ApproachRulesKept()
    {
        // 提示触发但来源页非法
        var s = Exposed(9711, out var z);
        var b = s.Battle!;
        Brace(s, "hatchet", 1);
        Assert.That(E.IntentShown(b, z), Does.StartWith("迫近你（3号位，触发架矛）"), "提示只看架势状态");
        var n0 = b.Log.Count;
        EnemyAct(s, z);
        var log = b.Log.Skip(n0).ToList();
        Assert.That(log.Any(l => l.Contains("迫近3号位的你") && l.Contains("算主动前移")), Is.True, string.Join(" | ", log));
        Assert.That(log.Any(l => l.Contains("你的迎击没有出手")), Is.True, "执行时重验来源页/射程");
        Assert.That(b.BraceCharges, Is.EqualTo(1), "不迎击不耗次数");
        Assert.That(z.RoundApUsed, Is.EqualTo(3), "迫近 1 + 弱近袭 2");
        Assert.That((z.Pos, GameEngine.PlayerPos(b)), Is.EqualTo((1, 3)), "双方号位不变");

        // 没架矛：迫近照样算主动前移，1+2AP，没有迎击
        var t = Exposed(9712, out var y);
        var tb = t.Battle!;
        var m0 = tb.Log.Count;
        EnemyAct(t, y);
        var tlog = tb.Log.Skip(m0).ToList();
        Assert.That(tlog.Any(l => l.Contains("迫近3号位的你") && l.Contains("算主动前移")), Is.True, string.Join(" | ", tlog));
        Assert.That(tlog.Any(l => l.Contains("迎击")), Is.False);
        Assert.That(tlog.Any(l => l.Contains("弱近袭")), Is.True, "弱近袭照常");
        Assert.That(y.RoundApUsed, Is.EqualTo(3));
        Assert.That((y.Pos, GameEngine.PlayerPos(tb)), Is.EqualTo((1, 3)));

        // 真迎击一次：次数 1→0，敌卡立即改写
        var u = Exposed(9713, out var x);
        var ub = u.Battle!;
        Brace(u, "wooden_spear", 1);
        Force(u, AtLeast(0.99), Any(), Any(), Any(), Any());   // 迎击落空（不打死迫近者）
        var k0 = ub.Log.Count;
        EnemyAct(u, x);
        Assert.That(ub.Log.Skip(k0).Any(l => l.Contains("你迎击")), Is.True, string.Join(" | ", ub.Log.Skip(k0)));
        Assert.That(ub.BraceCharges, Is.EqualTo(0));
        x.Acted = false;
        x.RoundApUsed = 0;
        Assert.That(E.IntentShown(ub, x), Does.StartWith("迫近你（3号位，算主动前移）"), "次数用完后即时更新");
    }
}
