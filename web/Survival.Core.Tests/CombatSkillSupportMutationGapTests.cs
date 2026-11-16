using System.Reflection;
using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 变异测试补测（全量基线 10da301d）：GameEngine.CombatSkillSupport.cs —— 增益期限、指挥类技能（鼓舞/稳心/协同/总攻令/预判）、
/// 增益被攻击用掉与指挥经验归属、斧蓄力与架矛、挣脱。期望值出处写在每条注释里：
/// A06 §2 技能主表与 §3 经验；A05 武器特性表；R03「Q75—Q82」章（Q76 持续 N 轮到 r+N 轮末、Q80 挣脱、Q82 反应不吃主动增益、指挥经验真实使用才发）；
/// 开发计划 v0.9 B7-3（持续口径）、B7-15（反应）、B7-17（指挥经验）。
/// </summary>
public class CombatSkillSupportMutationGapTests : BattleTestBase
{
    protected override bool NoEnemyApproach => true;

    private const string Me = GameState.PlayerId;
    private const string Lin = "npc_lin_zhao";
    private const string Zhou = "npc_zhou_yan";
    private const string Shen = "npc_shen_he";

    // ───────────── 场景工具 ─────────────

    private void Ready(GameState s)
    {
        PlayerFirst(s);
        E.BattleStep(s);
        Assert.That(E.AwaitingPlayer(s), Is.True);
    }

    private ItemInstance Arm2(GameState s, string id)
    {
        var slot = ItemInstance.EquipOwner(Me, EquipSlots.Weapon2);
        foreach (var old in s.Items.Where(i => i.Owner == slot)) old.Owner = ItemInstance.Carried;
        var it = E.CreateItems(s, id, 1, slot).Single();
        s.Battle!.ActiveWeaponSlot = 2;
        return it;
    }

    /// <summary>主角：武器Ⅰ厨房刀、武器Ⅱ指定指挥器具（当前页），站 2 号位（指挥使用位 234），指挥 level 级 0 经验，AP 充足。</summary>
    private GameState Commander(uint seed, string tool, int level, out ItemInstance whistle, params string[] enemies)
    {
        var s = Fight(seed, enemies.Length > 0 ? enemies : new[] { "worker" });
        Ready(s);
        Arm(s, "kitchen_knife");
        whistle = Arm2(s, tool);
        StandAt(s, 2);
        s.Skills["command"].Level = level;
        s.Skills["command"].Xp = 0;
        s.Battle!.PlayerAp = 9;
        foreach (var e in s.Battle.Alive) e.Hp = 50;
        return s;
    }

    /// <summary>主角切回武器Ⅰ（厨房刀）对 1 号敌人普通攻击，强制未中（R03 Q82：未命中也是真实使用增益）。</summary>
    private void MissWithKnife(GameState s)
    {
        E.SelectWeaponPage(s, 1);
        s.Battle!.PlayerAp = Math.Max(s.Battle.PlayerAp, 2);
        Force(s, AtLeast(0.999));
        var r = Cmd(s, "basic_attack", s.Battle.Alive.First().Index);
        Assert.That(r.Ok, Is.True, r.Reason);
    }

    private long XpOf(GameState s, string actor, string skill) =>
        s.Actors[actor].Skills.TryGetValue(skill, out var sk) ? sk.Level * 100000L + sk.Xp : 0;

    private GameState Party(uint seed, int n, params string[] enemies)
    {
        var s = New(seed);
        E.DebugQuickParty(s, n);
        E.DebugTeleport(s, "field");
        s.GameMinute = At(2, 9, 0);
        s.SpawnNextCheck = int.MaxValue;
        s.PatrolNextCheck = int.MaxValue;
        s.WorldEnemies.Clear();
        var ids = enemies.Select(id => E.DebugSpawnEnemy(s, id, "field").Id).ToList();
        Assert.That(E.DebugStartBattle(s, ids, GameEngine.FearEncounter), Is.True);
        return s;
    }

    private void TurnOf(GameState s, string actorId)
    {
        var b = s.Battle!;
        if (GameEngine.ActingAlly(b) is string cur) { b.Fighter(cur).Acting = false; b.Fighter(cur).Ap = 0; b.ActiveAlly = ""; }
        var code = GameEngine.QueueCode(b, actorId);
        b.Queue.Remove(code);
        b.Queue.Insert(0, code);
        b.Cursor = 0;
        E.BattleStep(s);
        Assert.That(GameEngine.ActingAlly(b), Is.EqualTo(actorId));
        b.Fighter(actorId).Ap = 9;
    }

    private ItemInstance ArmActor(GameState s, string actor, string weaponId, int page)
    {
        var slot = ItemInstance.EquipOwner(actor, page == 1 ? EquipSlots.Weapon1 : EquipSlots.Weapon2);
        foreach (var old in s.Items.Where(i => i.Owner == slot).ToList()) old.Owner = ItemInstance.CarriedOwner(actor);
        var it = E.CreateItems(s, weaponId, 1, slot).Single();
        s.Battle!.Fighter(actor).ActiveWeaponSlot = page;
        return it;
    }

    private static BattleCommand On(string action, string target) => new BattleCommand { Action = action, AllyTarget = target };

    /// <summary>四人：沈禾 1 号、你 2 号、林昭 3 号、周砚 4 号；三人武器Ⅰ木矛（123→123）、武器Ⅱ信号哨（当前页），指挥 3 级 0 经验；敌人在 1 号位。</summary>
    private GameState CommandParty(uint seed)
    {
        var s = Party(seed, 4, "worker");
        var b = s.Battle!;
        b.Allies = new List<string> { Shen, Me, Lin, Zhou };
        foreach (var id in new[] { Zhou, Me, Lin })
        {
            ArmActor(s, id, "wooden_spear", 1);
            ArmActor(s, id, "signal_whistle", 2);
            s.Actors[id].Skills["command"].Level = 3;
            s.Actors[id].Skills["command"].Xp = 0;
            b.Fighter(id).Panic = 30;
        }
        foreach (var e in b.Alive) { e.Hp = 50; e.Slot = "1"; }
        return s;
    }

    private void AllyMiss(GameState s, string actor)
    {
        using (E.FocusOn(s, actor)) Assert.That(E.SelectWeaponPage(s, 1).Ok, Is.True);
        Force(s, AtLeast(0.999));
        var r = E.ExecuteCommand(s, new BattleCommand { Action = "basic_attack", Target = s.Battle!.Alive.First().Index, ActorId = actor });
        Assert.That(r.Ok, Is.True, r.Reason);
    }

    // ───────────── 增益期限：持续 N 轮到第 r+N 轮末（Q76 / B7-3），斧蓄力到下一大回合末（A06 axe_05） ─────────────

    /// <summary>
    /// 鼓舞（A06 command_01 “持续2轮”）在第 r 轮施加：第 r+2 轮仍生效、第 r+3 轮失效（Q76：第4轮施加2轮到第6轮末）。
    /// 总攻令（command_10 “最多持续2轮”）同口径；斧蓄力（axe_05 “持续至下一大回合末”）到 r+1。抓 16/17/19 行的边界变异。
    /// </summary>
    [Test]
    public void BuffWindow_InspireAllOutCharge_LastThroughRoundREndPlusN()
    {
        var s = Commander(9101, "signal_whistle", 10, out _);
        var b = s.Battle!;
        var r = b.Round;
        Assert.That(Cmd(s, "command_01").Ok, Is.True);
        Assert.That(Cmd(s, "command_10").Ok, Is.True);
        Assert.That((b.InspireBonus, b.InspireUntilRound), Is.EqualTo((6.0, r + 2)), "A06：命中能力+6、2轮");
        Assert.That((b.AllOutFactor, b.AllOutUntilRound), Is.EqualTo((1.15, r + 2)), "A06：伤害×1.15、2轮");
        b.Round = r + 2;
        Assert.That(GameEngine.InspireActive(b), Is.True, "第 r+2 轮末之前仍生效");
        Assert.That(GameEngine.AllOutActive(b), Is.True, "第 r+2 轮末之前仍生效");
        b.Round = r + 3;
        Assert.That(GameEngine.InspireActive(b), Is.False, "第 r+3 轮已失效");
        Assert.That(GameEngine.AllOutActive(b), Is.False, "第 r+3 轮已失效");

        var a = Fight(9102, "worker");
        Ready(a);
        Arm(a, "hatchet");
        a.Skills["axe"].Level = 5;
        var ab = a.Battle!;
        var ra = ab.Round;
        Assert.That(Cmd(a, "axe_05").Ok, Is.True);
        Assert.That((ab.ChargeFactor, ab.ChargeUntilRound), Is.EqualTo((1.35, ra + 1)), "A06：伤害×1.35，持续至下一大回合末");
        ab.Round = ra + 1;
        Assert.That(GameEngine.AxeChargeOn(ab), Is.True, "下一大回合仍在");
        ab.Round = ra + 2;
        Assert.That(GameEngine.AxeChargeOn(ab), Is.False, "再下一回合失效");
    }

    /// <summary>敌人给我方的虚弱、破甲同按“到期轮末仍生效”（Q76 泛称 N 轮）：期限等于当前轮仍生效，早一轮已失效。抓 18/20 行。</summary>
    [Test]
    public void BuffWindow_PlayerWeakAndArmorBreak_ActiveOnLastRound()
    {
        var s = Fight(9103, "worker");
        var b = s.Battle!;
        b.PlayerWeakFactor = 0.8;
        b.PlayerArmorBreak = 0.2;
        b.PlayerWeakUntilRound = b.Round;
        b.PlayerArmorBreakUntilRound = b.Round;
        Assert.That(GameEngine.PlayerWeakActive(b), Is.True);
        Assert.That(GameEngine.PlayerArmorBroken(b), Is.True);
        b.PlayerWeakUntilRound = b.Round - 1;
        b.PlayerArmorBreakUntilRound = b.Round - 1;
        Assert.That(GameEngine.PlayerWeakActive(b), Is.False);
        Assert.That(GameEngine.PlayerArmorBroken(b), Is.False);
    }

    // ───────────── 斧蓄力只给“同一把斧” ─────────────

    /// <summary>A06 axe_05“下次该斧攻击伤害×1.35”：换到另一页的另一把手斧不吃蓄力（倍率不变），原斧吃 ×1.35。抓 27 行。</summary>
    [Test]
    public void AxeCharge_OtherAxeInstance_NoBonus()
    {
        var s = Fight(9104, "worker");
        Ready(s);
        Arm(s, "hatchet");
        Arm2(s, "hatchet");
        s.Skills["axe"].Level = 5;
        var b = s.Battle!;
        b.PlayerAp = 6;
        E.SelectWeaponPage(s, 1);
        var m1 = E.PreviewCommand(s, BattleCommand.Of("basic_attack", 1)).Targets.Single().Multiplier;
        E.SelectWeaponPage(s, 2);
        var m2 = E.PreviewCommand(s, BattleCommand.Of("basic_attack", 1)).Targets.Single().Multiplier;
        E.SelectWeaponPage(s, 1);
        Assert.That(Cmd(s, "axe_05").Ok, Is.True);
        Assert.That(E.PreviewCommand(s, BattleCommand.Of("basic_attack", 1)).Targets.Single().Multiplier / m1, Is.EqualTo(1.35).Within(1e-9), "蓄力的那把斧 ×1.35");
        E.SelectWeaponPage(s, 2);
        Assert.That(E.PreviewCommand(s, BattleCommand.Of("basic_attack", 1)).Targets.Single().Multiplier, Is.EqualTo(m2).Within(1e-9), "另一把斧不吃蓄力");
    }

    // ───────────── 增益被下一次攻击用掉，指挥经验（A06 §3、B7-17、R03 Q82） ─────────────

    /// <summary>
    /// 自己鼓舞自己：攻击（未中也算真实使用）用掉鼓舞并发指挥经验+2，战报写“指挥经验+2（鼓舞生效）”；同一场再鼓舞再用掉不再发（每场每技能一次）。
    /// 抓 34、58（取反）、59、60 行与 63 行经验句。
    /// </summary>
    [Test]
    public void Inspire_ConsumedByAttack_CommandXpOncePerBattle()
    {
        var s = Commander(9105, "signal_whistle", 3, out _);
        var b = s.Battle!;
        var r = b.Round;
        Assert.That(Cmd(s, "command_01").Ok, Is.True);
        Assert.That(XpOf(s, Me, "command"), Is.EqualTo(3 * 100000L), "施放时不发经验（增益用掉才发）");
        MissWithKnife(s);
        Assert.That(b.InspireBonus, Is.EqualTo(0), "鼓舞被这次攻击用掉");
        Assert.That(GameEngine.InspireActive(b), Is.False);
        Assert.That(XpOf(s, Me, "command"), Is.EqualTo(3 * 100000L + 2), "A06 §3：+2");
        Assert.That(b.Log.Count(l => l.Contains("指挥经验+2（鼓舞生效）")), Is.EqualTo(1));
        // 同一场第二次：冷却 1（r+2 可用）后再鼓舞、再用掉，不再给经验
        b.Round = r + 2;
        b.PlayerAp = 9;
        E.SelectWeaponPage(s, 2);
        Assert.That(Cmd(s, "command_01").Ok, Is.True);
        MissWithKnife(s);
        Assert.That(b.InspireBonus, Is.EqualTo(0));
        Assert.That(XpOf(s, Me, "command"), Is.EqualTo(3 * 100000L + 2), "每场每技能一次");
        Assert.That(b.Log.Count(l => l.Contains("指挥经验+")), Is.EqualTo(1));
    }

    /// <summary>指挥 10 级（A06 §2 SK02 达到 10 停止累计）：鼓舞照样被用掉，但不加经验、不写“指挥经验+2”。抓 61 行。</summary>
    [Test]
    public void Inspire_Consumed_MaxedCommand_NoXpLine()
    {
        var s = Commander(9106, "signal_whistle", 10, out _);
        var b = s.Battle!;
        Assert.That(Cmd(s, "command_01").Ok, Is.True);
        MissWithKnife(s);
        Assert.That(b.InspireBonus, Is.EqualTo(0));
        Assert.That(XpOf(s, Me, "command"), Is.EqualTo(10 * 100000L));
        Assert.That(b.Log.Any(l => l.Contains("指挥经验+")), Is.False);
    }

    /// <summary>总攻令（A06 command_10 伤害×1.15）被下一次攻击用掉（未中也算）：用掉后伤害倍率回到原值。抓 35 行。</summary>
    [Test]
    public void AllOut_ConsumedByNextAttack()
    {
        var s = Commander(9107, "signal_whistle", 10, out _);
        var b = s.Battle!;
        E.SelectWeaponPage(s, 1);
        StandAt(s, 1);
        var m0 = E.PreviewCommand(s, BattleCommand.Of("basic_attack", 1)).Targets.Single().Multiplier;
        StandAt(s, 2);
        E.SelectWeaponPage(s, 2);
        Assert.That(Cmd(s, "command_10").Ok, Is.True);
        E.SelectWeaponPage(s, 1);
        Assert.That(E.PreviewCommand(s, BattleCommand.Of("basic_attack", 1)).Targets.Single().Multiplier / m0, Is.EqualTo(1.15).Within(1e-9));
        MissWithKnife(s);
        Assert.That(GameEngine.AllOutActive(b), Is.False, "总攻令被用掉");
        Assert.That(E.PreviewCommand(s, BattleCommand.Of("basic_attack", 1)).Targets.Single().Multiplier, Is.EqualTo(m0).Within(1e-9));
    }

    /// <summary>
    /// 指挥经验归施术者（A06 §3 每施术者/目标/skillId；R03 Q82）：林昭鼓舞你、你鼓舞林昭；你的攻击只用掉你身上的鼓舞——经验给林昭，
    /// 你给林昭的那份待发不动；林昭攻击用掉他身上的鼓舞——经验给你。抓 48—51 行。
    /// </summary>
    [Test]
    public void PendingCommandXp_GoesToCasterOfTheConsumedBuff()
    {
        var s = CommandParty(9108);
        var b = s.Battle!;
        TurnOf(s, Lin);
        var r1 = E.ExecuteCommand(s, new BattleCommand { Action = "command_01", AllyTarget = Me, ActorId = Lin });
        Assert.That(r1.Ok, Is.True, r1.Reason);
        TurnOf(s, Me);
        Assert.That(E.ExecuteCommand(s, On("command_01", Lin)).Ok, Is.True);
        Assert.That(b.Fighter(Me).InspireBonus, Is.EqualTo(6));
        Assert.That(b.Fighter(Lin).InspireBonus, Is.EqualTo(6));

        AllyMiss(s, Me);
        Assert.That(b.Fighter(Me).InspireBonus, Is.EqualTo(0), "你的攻击用掉你身上的鼓舞");
        Assert.That(b.Fighter(Lin).InspireBonus, Is.EqualTo(6), "林昭身上的不动");
        Assert.That(XpOf(s, Lin, "command"), Is.EqualTo(3 * 100000L + 2), "经验给施术者林昭");
        Assert.That(XpOf(s, Me, "command"), Is.EqualTo(3 * 100000L), "你给林昭的那份还没被用掉");

        TurnOf(s, Lin);
        AllyMiss(s, Lin);
        Assert.That(b.Fighter(Lin).InspireBonus, Is.EqualTo(0));
        Assert.That(XpOf(s, Me, "command"), Is.EqualTo(3 * 100000L + 2), "林昭用掉你给的鼓舞 → 经验给你");
        Assert.That(XpOf(s, Lin, "command"), Is.EqualTo(3 * 100000L + 2), "林昭不再多拿");
    }

    /// <summary>
    /// 稳心对不同目标各计一次（A06 §3“每场每目标每个skillId最多计一次+2”）：你稳心林昭 +2、再稳心周砚 +2、再稳心林昭不再给。抓 488/489 行。
    /// </summary>
    [Test]
    public void Calm_CommandXpPerTarget()
    {
        var s = CommandParty(9109);
        var b = s.Battle!;
        TurnOf(s, Me);
        s.Actors[Me].Skills["command"].Level = 5;
        var r = b.Round;
        Assert.That(E.ExecuteCommand(s, On("command_05", Lin)).Ok, Is.True);
        Assert.That(b.Fighter(Lin).Panic, Is.EqualTo(22).Within(1e-9), "A06 稳心恐慌−8");
        Assert.That(XpOf(s, Me, "command"), Is.EqualTo(5 * 100000L + 2));
        b.Round = r + 3;   // A06 稳心冷却 2：r+3 可再用
        b.Me.Ap = 9;
        var r2 = E.ExecuteCommand(s, On("command_05", Zhou));
        Assert.That(r2.Ok, Is.True, r2.Reason);
        Assert.That(XpOf(s, Me, "command"), Is.EqualTo(5 * 100000L + 4), "不同目标各计一次");
        b.Round = r + 6;
        b.Me.Ap = 9;
        var r3 = E.ExecuteCommand(s, On("command_05", Lin));
        Assert.That(r3.Ok, Is.True, r3.Reason);
        Assert.That(b.Fighter(Lin).Panic, Is.EqualTo(14).Within(1e-9));
        Assert.That(XpOf(s, Me, "command"), Is.EqualTo(5 * 100000L + 4), "同目标同技能不再给");
    }

    /// <summary>
    /// 协同（A06 command_07：目标下一正常激活 AP+1）：林昭下一次正常激活 AP 多 1，用掉时施术者（你）得指挥经验+2；
    /// 有效技能指挥器具耐久−1（A06 §1/B7 实现同步“指挥/医疗器具有效技能磨损1”）。抓 437/439 行与 438 行战报。
    /// </summary>
    [Test]
    public void Coordinate_BonusApUsed_CasterGetsXp_ToolWears()
    {
        var s = CommandParty(9110);
        var b = s.Battle!;
        TurnOf(s, Me);
        var wh = s.Items.Single(i => i.Owner == ItemInstance.EquipOwner(Me, EquipSlots.Weapon2));
        var d0 = wh.Durability!.Value;
        s.Actors[Me].Skills["command"].Level = 7;
        var r = E.ExecuteCommand(s, On("command_07", Lin));
        Assert.That(r.Ok, Is.True, r.Reason);
        Assert.That(wh.Durability!.Value, Is.EqualTo(d0 - 1).Within(1e-9));
        Assert.That(b.Log.Last(), Does.Contain("林昭下一次正常行动AP+1"));
        var ap0 = E.NormalActivationAp(s.Actors[Lin]);
        TurnOf(s, Lin);
        Assert.That(b.Log.Last(l => l.Contains("轮到林昭行动")), Does.Contain($"{ap0 + 1}AP"), "下一次正常激活 AP+1");
        Assert.That(XpOf(s, Me, "command"), Is.EqualTo(7 * 100000L + 2), "协同 AP 被用掉 → 施术者+2");
    }

    /// <summary>
    /// 鼓舞与总攻令是不同增益，互不覆盖（A06：各自“同名不叠”）：先鼓舞再总攻令，鼓舞还在；先总攻令再鼓舞，总攻令还在。
    /// 战报只写本技能自己的效果（鼓舞不写“伤害×”“恐慌”“AP+”，稳心不写“命中能力”）。抓 428/444/455/465 行。
    /// </summary>
    [Test]
    public void InspireAndAllOut_DoNotOverwriteEachOther_LogsOwnEffectOnly()
    {
        var s = Commander(9111, "signal_whistle", 10, out _);
        var b = s.Battle!;
        b.Panic = 30;
        Assert.That(Cmd(s, "command_01").Ok, Is.True);
        var inspireLog = b.Log.Last();
        Assert.That(inspireLog, Does.Not.Contain("伤害×").And.Not.Contain("恐慌").And.Not.Contain("AP+"));
        Assert.That(Cmd(s, "command_10").Ok, Is.True);
        Assert.That(b.Log.Last(), Does.Not.Contain("命中能力").And.Not.Contain("恐慌").And.Not.Contain("AP+"));
        Assert.That((b.InspireBonus, GameEngine.InspireActive(b)), Is.EqualTo((6.0, true)), "总攻令不清鼓舞");
        Assert.That(GameEngine.AllOutActive(b), Is.True);
        Assert.That(E.CommandBlocked(s, BattleCommand.Of("command_05")), Is.Null, "鼓舞/总攻令不占用本回合稳心次数");
        Assert.That(Cmd(s, "command_05").Ok, Is.True);
        Assert.That(b.Log.Last(), Does.Not.Contain("命中能力").And.Not.Contain("伤害×"));
        Assert.That(b.Panic, Is.EqualTo(22).Within(1e-9));

        var t = Commander(9112, "signal_whistle", 10, out _);
        var tb = t.Battle!;
        Assert.That(Cmd(t, "command_10").Ok, Is.True);
        Assert.That(Cmd(t, "command_01").Ok, Is.True);
        Assert.That((tb.AllOutFactor, GameEngine.AllOutActive(tb)), Is.EqualTo((1.15, true)), "鼓舞不清总攻令");
    }

    /// <summary>
    /// 有效的鼓舞、总攻令各自都让指挥器具耐久−1（B7 实现同步“指挥/医疗器具有效技能磨损1”）。抓 453/475 行（effective 被改成 false 不磨）。
    /// </summary>
    [Test]
    public void InspireAllOut_EachWearsCommandTool()
    {
        var s = Commander(9113, "signal_whistle", 10, out var wh);
        var d0 = wh.Durability!.Value;
        Assert.That(Cmd(s, "command_01").Ok, Is.True);
        Assert.That(wh.Durability!.Value, Is.EqualTo(d0 - 1).Within(1e-9), "鼓舞有效 −1");
        Assert.That(Cmd(s, "command_10").Ok, Is.True);
        Assert.That(wh.Durability!.Value, Is.EqualTo(d0 - 2).Within(1e-9), "总攻令有效 −1");
    }

    /// <summary>
    /// 战术电台（A05“协同：指挥增益持续多1轮”）：鼓舞、总攻令到第 r+3 轮末（2 轮 +1）；战报写到期轮。抓 448/469 行与 450/471 行战报数值。
    /// </summary>
    [Test]
    public void FieldRadio_CommandBuffsLastOneMoreRound()
    {
        var s = Commander(9114, "field_radio", 10, out _);
        var b = s.Battle!;
        var r = b.Round;
        Assert.That(Cmd(s, "command_01").Ok, Is.True);
        Assert.That(b.InspireUntilRound, Is.EqualTo(r + 3));
        Assert.That(b.Log.Last(), Does.Contain($"下一次攻击命中能力+6（到第{r + 3}回合末）"));
        Assert.That(Cmd(s, "command_10").Ok, Is.True);
        Assert.That(b.AllOutUntilRound, Is.EqualTo(r + 3));
        Assert.That(b.Log.Last(), Does.Contain($"下一次攻击伤害×1.15（到第{r + 3}回合末）"));
    }

    /// <summary>
    /// 便携指挥终端“预判”（A05：每场首次号令额外赋予目标下一击命中能力+4）：首次鼓舞 = 6+4，战报写“预判”；本场第二次号令不再加。
    /// 首次号令是稳心时，+4 当下就可用（本轮生效）。抓 478—483 行。
    /// </summary>
    [Test]
    public void CommandTerminal_FirstCommandOnlyAddsFour()
    {
        var s = Commander(9115, "command_terminal", 10, out _);
        var b = s.Battle!;
        var r = b.Round;
        Assert.That(Cmd(s, "command_01").Ok, Is.True);
        Assert.That(b.InspireBonus, Is.EqualTo(10), "6 + 预判 4");
        Assert.That(b.Log.Last(), Does.Contain("预判：下一击命中能力再+4"));
        Assert.That(b.FirstCommandUsed, Is.True);
        b.Panic = 30;
        Assert.That(Cmd(s, "command_05").Ok, Is.True);
        Assert.That(b.InspireBonus, Is.EqualTo(10), "本场第二次号令不再加");
        Assert.That(b.Log.Last(), Does.Not.Contain("预判"));

        var t = Commander(9116, "command_terminal", 10, out _);
        var tb = t.Battle!;
        tb.Panic = 30;
        Assert.That(Cmd(t, "command_05").Ok, Is.True);
        Assert.That(tb.InspireBonus, Is.EqualTo(4), "稳心是本场首次号令：预判+4");
        Assert.That(GameEngine.InspireActive(tb), Is.True, "+4 当下可用");
    }

    // ───────────── 同名不叠与“已有”判断 ─────────────

    /// <summary>
    /// 鼓舞“同名不叠”（A06 command_01 持续 2 轮、冷却 1）：第 r 轮鼓舞未用，第 r+2 轮冷却好了但鼓舞仍在 → 拒绝“已有鼓舞（同名不叠）”；
    /// 给队友时原因写队友名（目标是主角时不写名字）。预判 +4 后（4 &lt; 6）再鼓舞是更强，可用。抓 135 行。
    /// </summary>
    [Test]
    public void Inspire_SameNameNoStack_Reasons()
    {
        var s = Commander(9117, "signal_whistle", 10, out _);
        var b = s.Battle!;
        var r = b.Round;
        Assert.That(Cmd(s, "command_01").Ok, Is.True);
        b.Round = r + 2;
        Assert.That(E.CommandBlocked(s, BattleCommand.Of("command_01")), Is.EqualTo("已有鼓舞（同名不叠）"));
        b.Round = r + 3;
        Assert.That(E.CommandBlocked(s, BattleCommand.Of("command_01")), Is.Null, "鼓舞到期后可再用");

        var t = Commander(9118, "command_terminal", 10, out _);
        t.Battle!.Panic = 30;
        Assert.That(Cmd(t, "command_05").Ok, Is.True);
        Assert.That(t.Battle.InspireBonus, Is.EqualTo(4));
        Assert.That(E.CommandBlocked(t, BattleCommand.Of("command_01")), Is.Null, "已有的命中加成 4 < 6，鼓舞更强可用");

        var p = CommandParty(9119);
        TurnOf(p, Me);
        Assert.That(E.ExecuteCommand(p, On("command_01", Lin)).Ok, Is.True);
        TurnOf(p, Zhou);
        Assert.That(E.CommandBlocked(p, new BattleCommand { Action = "command_01", AllyTarget = Lin, ActorId = Zhou }), Is.EqualTo("林昭已有鼓舞（同名不叠）"));
        Assert.That(E.CommandBlocked(p, new BattleCommand { Action = "command_01", AllyTarget = Me, ActorId = Zhou }), Is.Null, "你身上没有鼓舞");
    }

    /// <summary>总攻令同名不叠（A06 command_10）：队友身上已有时，对他用写“林昭已有总攻令”；对自己写“已有总攻令”。抓 136 行文字分支。</summary>
    [Test]
    public void AllOut_SameNameNoStack_AllyReasonNamesTarget()
    {
        var p = CommandParty(9120);
        foreach (var id in new[] { Me, Lin }) p.Actors[id].Skills["command"].Level = 10;
        TurnOf(p, Lin);
        Assert.That(E.ExecuteCommand(p, new BattleCommand { Action = "command_10", AllyTarget = Lin, ActorId = Lin }).Ok, Is.True);
        Assert.That(E.CommandBlocked(p, new BattleCommand { Action = "command_10", AllyTarget = Lin, ActorId = Lin }), Does.StartWith("冷却中"));
        TurnOf(p, Me);
        Assert.That(E.CommandBlocked(p, On("command_10", Lin)), Is.EqualTo("林昭已有总攻令（同名不叠）"));
        Assert.That(E.CommandBlocked(p, On("command_10", Me)), Is.Null, "你身上没有，可以用");
        Assert.That(E.ExecuteCommand(p, On("command_10", Me)).Ok, Is.True);
        TurnOf(p, Zhou);
        p.Actors[Zhou].Skills["command"].Level = 10;
        Assert.That(E.CommandBlocked(p, new BattleCommand { Action = "command_10", AllyTarget = Me, ActorId = Zhou }), Is.EqualTo("已有总攻令（同名不叠）"), "目标是主角时不写名字");
    }

    /// <summary>
    /// 稳心对恐慌 0 的目标没有效果（A06 §3 空转不算/R03）：拒绝写“恐慌已是0”；对队友写队友名。恐慌下限 0（R03 恐慌 0—100）。抓 140 行文字分支、247 行下限。
    /// </summary>
    [Test]
    public void Calm_PanicZero_ReasonNamesTarget_PreviewFloorsAtZero()
    {
        var p = CommandParty(9121);
        var b = p.Battle!;
        TurnOf(p, Me);
        p.Actors[Me].Skills["command"].Level = 5;
        b.Fighter(Lin).Panic = 0;
        Assert.That(E.CommandBlocked(p, On("command_05", Lin)), Is.EqualTo("林昭恐慌已是0，没有效果"));
        b.Fighter(Me).Panic = 0;
        Assert.That(E.CommandBlocked(p, On("command_05", Me)), Is.EqualTo("恐慌已是0，没有效果"));
        b.Fighter(Lin).Panic = 5;
        var pv = E.PreviewCommand(p, On("command_05", Lin));
        Assert.That(pv.Ok, Is.True, pv.Reason);
        Assert.That(pv.Notes, Has.Some.Contains("恐慌5.00→0.00"), "−8 到下限 0");
        Assert.That(E.ExecuteCommand(p, On("command_05", Lin)).Ok, Is.True);
        Assert.That(b.Fighter(Lin).Panic, Is.EqualTo(0));
        Assert.That(b.Log.Last(), Does.Contain("恐慌5.00→0.00"));
    }

    /// <summary>
    /// 协同不叠（A06 command_07“不叠”实现同步“已有协同”）：你给林昭协同后，周砚再给林昭协同被拒“林昭已有协同（不叠）”；
    /// 第一次（林昭身上没有）可用。抓 142 行两个比较变异。
    /// </summary>
    [Test]
    public void Coordinate_AlreadyHasBonus_Blocked()
    {
        var p = CommandParty(9122);
        foreach (var id in new[] { Me, Zhou }) p.Actors[id].Skills["command"].Level = 7;
        TurnOf(p, Me);
        Assert.That(E.CommandBlocked(p, On("command_07", Lin)), Is.Null, "林昭身上还没有协同");
        Assert.That(E.ExecuteCommand(p, On("command_07", Lin)).Ok, Is.True);
        TurnOf(p, Zhou);
        Assert.That(E.CommandBlocked(p, new BattleCommand { Action = "command_07", AllyTarget = Lin, ActorId = Zhou }), Is.EqualTo("林昭已有协同（不叠）"));
    }

    // ───────────── 架矛与斧蓄力的执行数值 ─────────────

    /// <summary>
    /// 架矛（A06 spear_05 迎击1次×0.75）：加固长枪“架阵：迎击额外+1（最多2）”→ 2 次；金属矛“金属尖：迎击伤害×1.1”→ ×0.825。
    /// 预览与执行同源，战报写次数与倍率。抓 204—207、358/359/361 行。
    /// </summary>
    [Test]
    public void Brace_PikeTwoCharges_MetalSpearFactor_PreviewAndExecute()
    {
        var s = Fight(9123, "worker");
        Ready(s);
        Arm(s, "pike");
        s.Skills["spear"].Level = 5;
        var b = s.Battle!;
        var pv = E.PreviewCommand(s, BattleCommand.Of("spear_05"));
        Assert.That(pv.Notes, Has.Some.StartsWith("迎击2次：").And.Contains("伤害×0.75"));
        Assert.That(Cmd(s, "spear_05").Ok, Is.True);
        Assert.That((b.BraceCharges, b.BraceFactor), Is.EqualTo((2, 0.75)));
        Assert.That(b.Log.Last(), Does.Contain("迎击2次（伤害×0.75）"));

        var m = Fight(9124, "worker");
        Ready(m);
        Arm(m, "metal_spear");
        m.Skills["spear"].Level = 5;
        var mb = m.Battle!;
        Assert.That(E.PreviewCommand(m, BattleCommand.Of("spear_05")).Notes, Has.Some.StartsWith("迎击1次：").And.Contains($"伤害×{Fmt.T(0.75 * 1.1)}"));
        Assert.That(Cmd(m, "spear_05").Ok, Is.True);
        Assert.That(mb.BraceCharges, Is.EqualTo(1));
        Assert.That(mb.BraceFactor, Is.EqualTo(0.75 * 1.1).Within(1e-9));
        Assert.That(mb.Log.Last(), Does.Contain($"迎击1次（伤害×{Fmt.T(0.75 * 1.1)}）"));
    }

    /// <summary>斧蓄力（A06 axe_05）预览与战报写倍率与到期轮（下一大回合末 = r+1）。抓 203、354 行。</summary>
    [Test]
    public void AxeCharge_PreviewAndLogShowFactorAndRound()
    {
        var s = Fight(9125, "worker");
        Ready(s);
        Arm(s, "hatchet");
        s.Skills["axe"].Level = 5;
        var r = s.Battle!.Round;
        Assert.That(E.PreviewCommand(s, BattleCommand.Of("axe_05")).Notes, Has.Some.EqualTo($"下次用这把斧攻击伤害×1.35，到第{r + 1}回合末；不叠"));
        Assert.That(Cmd(s, "axe_05").Ok, Is.True);
        Assert.That(s.Battle.Log.Last(), Does.Contain($"下次用简易斧攻击伤害×1.35（到第{r + 1}回合末）"));
    }

    // ───────────── 挣脱（A06 unarmed_07、R03 Q80） ─────────────

    private GameState Struggle(uint seed, int pos, bool slowed, out BattleState b)
    {
        var s = Fight(seed, "worker");
        Ready(s);
        s.Skills["unarmed"].Level = 7;
        StandAt(s, pos);
        b = s.Battle!;
        b.PlayerAp = 6;
        b.PlayerSlowPending = slowed;
        return s;
    }

    /// <summary>
    /// 挣脱：解除减速并后移 1 位（2→3、3→4）；预览写“解除减速；后移到N号位”并放在说明第一条；战报“挣脱了束缚（减速解除）”“后移到N号位”；
    /// 结果字“解除减速”、真实换位。抓 209—214、366—375、576 行。
    /// </summary>
    [Test]
    public void Struggle_ClearsSlowAndMovesBackOne()
    {
        var s = Struggle(9126, 3, true, out var b);
        var pv = E.PreviewCommand(s, BattleCommand.Of("unarmed_07"));
        Assert.That(pv.EffectText, Is.EqualTo("解除减速；后移到4号位"));
        Assert.That(pv.Notes[0], Is.EqualTo(pv.EffectText));
        Assert.That(pv.Notes, Has.Some.EqualTo("只解减速/束缚/标记；不解失衡、虚弱、出血、破甲、感染、保护"));
        E.DrainBattleFx();
        Assert.That(Cmd(s, "unarmed_07").Ok, Is.True);
        Assert.That((b.PlayerSlowPending, b.PlayerSlowActive), Is.EqualTo((false, false)));
        Assert.That(GameEngine.AllyPos(b, Me), Is.EqualTo(4));
        Assert.That(b.Log.Last(), Does.Contain("挣脱了束缚（减速解除）").And.Contain("后移到4号位").And.Not.Contain("与同伴交换"));
        var ev = E.DrainBattleFx().Single(x => !x.Short && x.Results.Count > 0);
        var res = ev.Results.Single();
        Assert.That(res.Changes, Is.EqualTo(new[] { GameEngine.RemovedText("减速") }));
        Assert.That(res.Moved, Is.True);

        var t = Struggle(9127, 2, true, out var tb);
        Assert.That(Cmd(t, "unarmed_07").Ok, Is.True);
        Assert.That(GameEngine.AllyPos(tb, Me), Is.EqualTo(3), "2→3");
    }

    /// <summary>没有可解状态时只后移（不白写“解除减速”）：预览“没有可解除的减速/束缚/标记；后移到2号位”，战报“没有束缚”，减速状态不被改。抓 367、368、574 行。</summary>
    [Test]
    public void Struggle_NoSlow_OnlyMoves()
    {
        var s = Struggle(9128, 1, false, out var b);
        var pv = E.PreviewCommand(s, BattleCommand.Of("unarmed_07"));
        Assert.That(pv.EffectText, Is.EqualTo("没有可解除的减速/束缚/标记；后移到2号位"));
        E.DrainBattleFx();
        Assert.That(Cmd(s, "unarmed_07").Ok, Is.True);
        Assert.That(b.Log.Last(), Does.Contain("没有束缚").And.Contain("后移到2号位"));
        Assert.That((b.PlayerSlowPending, b.PlayerSlowActive), Is.EqualTo((false, false)));
        Assert.That(E.DrainBattleFx().SelectMany(x => x.Results).SelectMany(x => x.Changes), Does.Not.Contain(GameEngine.RemovedText("减速")));
    }

    /// <summary>4 号位有减速：原地解除（R03 Q80“4位仍可原地清”），不移动；预览“解除减速；已在4号位，原地不移”，战报“已在4号位，原地挣脱”。抓 576 行条件、212/377 行。</summary>
    [Test]
    public void Struggle_AtFour_ClearsInPlace()
    {
        var s = Struggle(9129, 4, true, out var b);
        var pv = E.PreviewCommand(s, BattleCommand.Of("unarmed_07"));
        Assert.That(pv.EffectText, Is.EqualTo("解除减速；已在4号位，原地不移"));
        Assert.That(Cmd(s, "unarmed_07").Ok, Is.True);
        Assert.That(GameEngine.AllyPos(b, Me), Is.EqualTo(4));
        Assert.That(b.PlayerSlowPending, Is.False);
        Assert.That(b.Log.Last(), Does.Contain("已在4号位，原地挣脱"));
    }

    /// <summary>后移目的格有同伴：一次原子交换（R03 Q80），预览与战报写“（与同伴交换）”，同伴到你原来的号位。抓 212/373/375 行。</summary>
    [Test]
    public void Struggle_SwapsWithMateBehind()
    {
        var s = Party(9130, 2, "worker");
        var b = s.Battle!;
        b.Allies = new List<string> { Me, Lin };
        TurnOf(s, Me);
        s.Skills["unarmed"].Level = 7;
        foreach (var slot in new[] { EquipSlots.Weapon1, EquipSlots.Weapon2 })
            foreach (var it in s.Items.Where(i => i.Owner == ItemInstance.EquipOwner(Me, slot)).ToList()) it.Owner = ItemInstance.Carried;
        b.PlayerSlowPending = true;
        var pv = E.PreviewCommand(s, BattleCommand.Of("unarmed_07"));
        Assert.That(pv.Ok, Is.True, pv.Reason);
        Assert.That(pv.EffectText, Is.EqualTo("解除减速；后移到2号位（与同伴交换）"));
        Assert.That(Cmd(s, "unarmed_07").Ok, Is.True);
        Assert.That(b.Allies.Take(2), Is.EqualTo(new[] { Lin, Me }));
        Assert.That(b.Log.Last(), Does.Contain("后移到2号位（与同伴交换）"));
    }

    /// <summary>
    /// A06 command_05 稳心“同目标每大回合最多一次”：你本回合对林昭用过稳心后，周砚同回合再对林昭用被拒，原因写“本回合已对林昭用过稳心（同目标每大回合一次）”；
    /// 配对：同回合对别的目标（你）可以用；下一回合可再对林昭用。抓 139 行原因文字。
    /// </summary>
    [Test]
    public void Calm_SameTargetOncePerRound_AcrossCasters()
    {
        var s = CommandParty(9140);
        var b = s.Battle!;
        TurnOf(s, Me);
        s.Actors[Me].Skills["command"].Level = 5;
        Assert.That(E.ExecuteCommand(s, On("command_05", Lin)).Ok, Is.True);
        TurnOf(s, Zhou);
        s.Actors[Zhou].Skills["command"].Level = 5;
        var cmd = On("command_05", Lin);
        cmd.ActorId = Zhou;
        Assert.That(E.CommandBlocked(s, cmd), Is.EqualTo("本回合已对林昭用过稳心（同目标每大回合一次）"));
        var other = On("command_05", Me);
        other.ActorId = Zhou;
        Assert.That(E.CommandBlocked(s, other), Is.Null, "同回合对别的目标可以");
        b.Round += 1;
        Assert.That(E.CommandBlocked(s, cmd), Is.Null, "下一大回合可再对林昭");
    }

    /// <summary>
    /// R03 Q80 挣脱：4 号位且没有减速/束缚/标记时整条无收益，拒绝并说明原因、不扣成本；配对：4 号位有减速可原地解除（见 Struggle_AtFour_ClearsInPlace）。抓 127 行原因文字。
    /// </summary>
    [Test]
    public void Struggle_AtFourNoSlow_BlockedNoGain()
    {
        var s = Struggle(9141, 4, false, out var b);
        var ap = b.PlayerAp;
        Assert.That(E.CommandBlocked(s, BattleCommand.Of("unarmed_07")), Is.EqualTo("你在4号位不能再后移，也没有可解除的减速/束缚/标记（整条无收益，不扣成本）"));
        Assert.That(Cmd(s, "unarmed_07").Ok, Is.False);
        Assert.That(b.PlayerAp, Is.EqualTo(ap), "不扣成本");
        var t = Struggle(9142, 4, true, out _);
        Assert.That(E.CommandBlocked(t, BattleCommand.Of("unarmed_07")), Is.Null);
    }
}
