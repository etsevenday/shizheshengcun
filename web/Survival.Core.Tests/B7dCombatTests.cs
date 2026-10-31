using Newtonsoft.Json.Linq;
using Survival.Core.Config;
using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 B7d（策划提交 ccfdf7c/1309dc0 答复 Q75–Q80、Q82）R99：技能结算与真实稀疏阵位。
/// 出处：R03「2026-10-31 Q75—Q82技能、真实站位与伤害裁定（当前优先）」、A06/A09/A10/R08/R02/R01/R07/S01/A05 同名章、
/// combat-resolution-adjudications.json（= A12 combatResolutionAdjudications）、更新说明/2026-10-31/Q75-Q82 更新说明；验收 RES-Q75—RES-Q82。
/// </summary>
public class B7dCombatTests : BattleTestBase
{
    private static JObject Res() =>
        (JObject)JObject.Parse(File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "design.a12.json")))["combatResolutionAdjudications"]!;

    private void Ready(GameState s)
    {
        PlayerFirst(s);
        E.BattleStep(s);
        Assert.That(E.AwaitingPlayer(s), Is.True);
    }

    private ItemInstance Arm2(GameState s, string id)
    {
        var slot = ItemInstance.EquipOwner(GameState.PlayerId, EquipSlots.Weapon2);
        foreach (var old in s.Items.Where(i => i.Owner == slot)) old.Owner = ItemInstance.Carried;
        var it = E.CreateItems(s, id, 1, slot).Single();
        s.Battle!.ActiveWeaponSlot = 2;
        return it;
    }

    /// <summary>把敌人直接摆到某些号位（只用于布置场景）。</summary>
    private static void Place(GameState s, params (int index, int slot)[] at)
    {
        foreach (var (i, p) in at) Enemy(s, i).Slot = p.ToString();
    }

    // ───────────── M174：真实稀疏槽、调整站位、旧档迁移、贯穿后缀 ─────────────

    /// <summary>RES-Q80a：合同取消自动压紧与单人全兼容 1；死亡、指令后、轮初都不压紧，读档恢复真实空位。</summary>
    [Test]
    public void RES_Q80a_NoCompaction_DeathAndRoundKeepHoles_SaveRestores()
    {
        var p = (JObject)Res()["positions"]!;
        Assert.That((bool)p["realSlotsPersistWithHoles"]!, Is.True);
        Assert.That((bool)p["automaticCompressionAfterCommandDeathOrRound"]!, Is.False);
        Assert.That((bool)p["soleSurvivorNotTeleportedTo1"]!, Is.True);
        Assert.That((bool)p["blanketSoloOwnSlotBypass"]!, Is.False);
        Assert.That(((JArray)Res()["precedence"]!["overrides"]!).Select(x => (string)x!), Does.Contain("automatic_post_command_compression").And.Contain("single_survivor_all_own_positions_compatible_with_slot1"));

        var s = Fight(9101, "shambler", "shambler", "crawler");
        Ready(s);
        var b = s.Battle!;
        Assert.That(b.Enemies.Select(e => e.Slot), Is.EqualTo(new[] { "1", "2", "3" }), "新战初次生成仍按名单紧凑");
        Arm(s, "stone_knife");
        Enemy(s, 1).Hp = 0.01;
        Force(s, Below(0.01), AtLeast(0.99), Any(), Any());
        Assert.That(Cmd(s, "basic_attack", 1).Ok, Is.True);
        Assert.That(Enemy(s, 1).Dead, Is.True);
        Assert.That((Enemy(s, 2).Slot, Enemy(s, 3).Slot), Is.EqualTo(("2", "3")), "死亡与指令后不压紧");
        StandAt(s, 3);
        var back = SaveSerializer.Deserialize(SaveSerializer.Serialize(s), Cfg);
        Assert.That(GameEngine.PlayerPos(back.Battle!), Is.EqualTo(3), "读档恢复我方真实号位");
        Assert.That(back.Battle!.Enemies.Select(e => e.Slot), Is.EqualTo(new[] { "", "2", "3" }), "读档恢复敌方空位");
        // 轮初也不压紧（2 号位的敌人在合法位、只是够不到 3 号位的你）
        Cmd(s, "end_turn");
        var round = b.Round;
        while (s.Battle != null && b.Round == round) Step(s);
        Assert.That(GameEngine.PlayerPos(b), Is.EqualTo(3), "单人在 3 号位不被送回 1");
        Assert.That(b.Enemies.Where(e => e.Alive).All(e => e.Pos >= 1), Is.True);
    }

    /// <summary>RES-Q80c：单人 1AP/E0.1 调整站位；刀手在 4 号位要两次调整到 2 才能挥刀；弓手在 1 号位退到 2 再射。不再全武器兼容 1。</summary>
    [Test]
    public void RES_Q80c_SoloAdjustPosition_KnifeFrom4_BowFrom1()
    {
        var v = (JObject)Res()["positions"]!["voluntaryMove"]!;
        var sw = Cfg.CombatActions.Single(a => a.Id == "swap");
        Assert.That((sw.ApCost, sw.Energy, (bool)v["allowSolo"]!, (int)v["xp"]!), Is.EqualTo(((int)v["ap"]!, (double?)(double)v["energy"]!, true, 0)));
        Assert.That(GameEngine.ActionName("swap"), Is.EqualTo("调整站位"));

        var s = Fight(9102, "worker");
        Ready(s);
        var b = s.Battle!;
        s.Player.TacticalTier = 2;
        b.PlayerAp = 5;
        Arm(s, "stone_knife");
        StandAt(s, 4);
        Assert.That(E.CommandBlocked(s, BattleCommand.Of("basic_attack", 1)), Does.Contain("只能在我方1、2号位使用（你在4号位）"), "刀在 4 号位不能挥");
        var opts = E.BattleOptions(s).Where(o => o.Action == "swap").ToList();
        Assert.That(opts.Select(o => o.ItemInstanceId), Is.EqualTo(new int?[] { 3 }), "4 号位只能往前到 3");
        var xp = s.Skills.Values.Sum(k => k.Xp);
        var e0 = s.Stats.Energy;
        Assert.That(Cmd(s, "swap", null, 3).Ok, Is.True, "界面用 ItemInstanceId 传目的槽");
        Assert.That(Cmd(s, "swap", 2).Ok, Is.True);
        Assert.That(GameEngine.PlayerPos(b), Is.EqualTo(2));
        Assert.That(b.PlayerAp, Is.EqualTo(3), "两次调整共 2AP");
        Assert.That(s.Stats.Energy, Is.EqualTo(e0 - 0.2).Within(1e-9));
        Assert.That(s.Skills.Values.Sum(k => k.Xp), Is.EqualTo(xp), "调整站位 0 经验");
        Assert.That(b.Log.Last(), Does.Contain("3号位→2号位"));
        Assert.That(E.CommandBlocked(s, BattleCommand.Of("basic_attack", 1)), Is.Null, "到 2 号位后可以挥刀");

        var t = Fight(9103, "worker");
        Ready(t);
        Arm(t, "wooden_bow");
        E.DebugGive(t, "arrow", 3);
        Assert.That(E.CommandBlocked(t, BattleCommand.Of("basic_attack", 1)), Does.Contain("只能在我方2、3、4号位使用（你在1号位）"));
        var pv = E.PreviewCommand(t, BattleCommand.Of("swap", 2));
        Assert.That(pv.Ok, Is.True);
        Assert.That(pv.Notes, Has.Some.Contains("到2号位后：武器Ⅰ木弓可用"));
        Assert.That(Cmd(t, "swap", 2).Ok, Is.True);
        Assert.That(t.Battle!.PlayerAp, Is.EqualTo(2));
        Assert.That(E.CommandBlocked(t, BattleCommand.Of("basic_attack", 1)), Is.Null, "1AP 退到 2 号位后按原 2AP 射击");
    }

    /// <summary>RES-Q80a：旧档（schema 13）里的战斗站位迁移：合法号位原样保留，重复/越界的补到空位；我方表坏了主角回 1 号位。</summary>
    [Test]
    public void RES_Q80a_Migration13To14_KeepsValidSlots_FixesBroken()
    {
        var s = Fight(9104, "shambler", "shambler", "crawler");
        Ready(s);
        Enemy(s, 1).Dead = true;
        Enemy(s, 1).Slot = "";
        var j = JObject.Parse(SaveSerializer.Serialize(s));
        j["SchemaVersion"] = "13";
        var m = SaveSerializer.Deserialize(j.ToString(), Cfg, out var notes);
        Assert.That(m.SchemaVersion, Is.EqualTo("14"));
        Assert.That(m.Battle!.Enemies.Select(e => e.Slot), Is.EqualTo(new[] { "", "2", "3" }), "合法号位与空位原样保留，不压紧");
        Assert.That(notes.Single(), Does.Contain("真实站位").And.Not.Contain("补到空位"));
        Assert.That(E.AwaitingPlayer(m), Is.True, "读档后可以继续");

        var en = (JArray)j["Battle"]!["Enemies"]!;
        en[1]!["Slot"] = "3";   // 与 3 号重复
        en[2]!["Slot"] = "3";
        j["Battle"]!["Allies"] = new JArray("x", "y", "z", "w", "v");
        var bad = SaveSerializer.Deserialize(j.ToString(), Cfg, out var notes2);
        Assert.That(bad.Battle!.Enemies.Select(e => e.Slot), Is.EqualTo(new[] { "", "3", "1" }), "先到先得，重复的补到最前的空位");
        Assert.That(bad.Battle.Allies, Is.EqualTo(new[] { GameState.PlayerId }), "我方表缺主角时主角回 1 号位");
        Assert.That(notes2.Single(), Does.Contain("2处缺失或重复的站位已补到空位"));
    }

    /// <summary>RES-Q82a：贯穿选起点 k，作用真实 k 至 4 的存活敌，按槽偏移 0.65/0.50/0.35/0.25，空位不重排；AP2/E0.8/CD1/整指令耐久1。</summary>
    [Test]
    public void RES_Q82a_PierceSuffix_SlotOffsetFactors_HolesNotRenumbered()
    {
        var pc = (JObject)Res()["pierce"]!;
        Assert.That(((JArray)pc["damageFactorsBySlotOffset"]!).Select(x => (double)x!), Is.EqualTo(new[] { 0.65, 0.5, 0.35, 0.25 }));
        var node = E.SkillNode("spear_07")!;
        Assert.That((node.Ap, node.BaseEnergy, node.CooldownRounds, node.OwnPart, node.TargetPart), Is.EqualTo(((int)pc["ap"]!, (double)pc["baseEnergy"]!, (int)pc["cooldown"]!, "123", "1234")));

        var s = Fight(9105, "feral_dog", "feral_dog", "feral_dog");
        Ready(s);
        var b = s.Battle!;
        var spear = Arm(s, "wooden_spear");
        s.Skills["spear"].Level = 7;
        foreach (var e in b.Alive) e.Hp = 50;
        Place(s, (1, 1), (2, 3), (3, 4));   // 2 号位空
        var pv = E.PreviewCommand(s, BattleCommand.Of("spear_07", 1));
        Assert.That(pv.Ok, Is.True);
        Assert.That(pv.Targets.Select(t => t.Slot), Is.EqualTo(new[] { "1", "3", "4" }));
        var f1 = pv.Targets.Select(t => t.Multiplier / pv.Targets[0].Multiplier).ToList();
        Assert.That(f1[1], Is.EqualTo(0.35 / 0.65).Within(1e-9), "3 号位离起点 2 格：×0.35（空位占偏移，不重排成 0.50）");
        Assert.That(f1[2], Is.EqualTo(0.25 / 0.65).Within(1e-9));
        Assert.That(pv.Notes, Has.Some.Contains("贯穿从1号位打到4号位"));
        var pv3 = E.PreviewCommand(s, BattleCommand.Of("spear_07", 2));
        Assert.That(pv3.Targets.Select(t => t.Slot), Is.EqualTo(new[] { "3", "4" }), "点 3 号位的敌人：起点 3，打 3、4");
        Assert.That(pv3.Targets[1].Multiplier / pv3.Targets[0].Multiplier, Is.EqualTo(0.5 / 0.65).Within(1e-9));
        Assert.That(GameEngine.SuffixTargets(b, 2).Select(e => e.Pos), Is.EqualTo(new[] { 3, 4 }), "起点可以是空位，只要后缀有活目标");
        Assert.That(GameEngine.SuffixFactor(E.SkillExecOf("spear_07")!, 2, 3), Is.EqualTo(0.5), "空起点 2 → 3 号位偏移 1");

        // 途中倒下不补位：1 号位一击倒下，后面的目标仍按提交时的号位倍率
        Enemy(s, 1).Hp = 0.01;
        var d0 = spear.Durability;
        var hp3 = Enemy(s, 2).Hp;
        Enemy(s, 3).Dead = true;   // 只留 1 号位与 3 号位两个目标，少抽几次随机数
        Enemy(s, 3).Slot = "";
        Func<double, bool> mid = x => x >= 0.12 && x < 0.3;   // 命中、不暴击、抽值任意；倒下的掉落判定也落在这里
        Force(s, Enumerable.Repeat(mid, 8).ToArray());
        Assert.That(Cmd(s, "spear_07", 1).Ok, Is.True);
        Assert.That(Enemy(s, 1).Dead, Is.True);
        Assert.That(Enemy(s, 2).Slot, Is.EqualTo("3"), "不压紧");
        var d3 = hp3 - Enemy(s, 2).Hp;
        Assert.That(d3, Is.InRange(pv.Targets[1].DamageMin - 1e-9, pv.Targets[1].DamageMax + 1e-9), $"3 号位仍按 ×0.35（实际{d3}）");
        Assert.That(d3, Is.LessThan(pv.Targets[1].DamageMax * 0.5 / 0.35 * 0.75), "不是按偏移1的 ×0.50 补位");
        Assert.That(spear.Durability, Is.EqualTo(d0 - 1).Within(1e-9), "整条指令耐久 −1");
        Assert.That(b.SkillUsedRound["spear_07"], Is.EqualTo(b.Round), "CD1");
    }

    // ───────────── M175：强制位移、攀附拉扯、突进斩与挣脱 ─────────────

    private void EnemyAct(GameState s, BattleEnemy e) =>
        typeof(GameEngine).GetMethod("EnemyActivation", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .Invoke(E, new object[] { s, e, false });

    /// <summary>RES-Q79/RES-Q80b：攀附感染者拉扯 0 直接伤害，但命中后照判控制，成功则单人也真实前移一格；1 号位边界不移、不判控制、不给稳态。</summary>
    [Test]
    public void RES_Q79_Q80b_ClingerPull_ZeroDamage_RealMove_BoundaryNoRoll()
    {
        var fm = (JObject)Res()["positions"]!["forcedMove"]!;
        Assert.That(((int)fm["pull"]!, (int)fm["knockback"]!), Is.EqualTo((-1, 1)));
        Assert.That((double)Res()["enemyAssumptions"]!["z_clingerPullDamage"]!, Is.EqualTo(0));

        var s = Fight(9201, "z_clinger");
        Ready(s);
        var b = s.Battle!;
        var c = Enemy(s, 1);
        Assert.That(c.Intent, Is.EqualTo("pull"));
        Place(s, (1, 2));
        StandAt(s, 2);
        var h0 = s.Stats.Health;
        var q = b.Queue.ToList();
        Force(s, Below(0.01), Any(), Below(0.01));   // 命中、抽值（倍率0）、控制成功
        EnemyAct(s, c);
        Assert.That(GameEngine.PlayerPos(b), Is.EqualTo(1), "单人也被拉到 1 号位，不归位、不忽略：" + string.Join(" | ", b.Log.TakeLast(3)));
        Assert.That(s.Stats.Health, Is.EqualTo(h0), "拉扯 0 直接伤害");
        Assert.That(b.PlayerSteadyUntilRound, Is.EqualTo(b.Round + 1), "控制成功得稳态");
        Assert.That(b.Log.Last(), Does.Contain("不造成伤害").And.Contain("你被拉动到1号位"));
        Assert.That(b.Queue, Is.EqualTo(q), "强制位移不改已锁行动队列");

        // 1 号位再拉：边界，不移动、不判控制（只抽命中一次）
        b.PlayerSteadyUntilRound = 0;
        Force(s, Below(0.01), Any());
        var rng = s.Rng.State;
        EnemyAct(s, c);
        Assert.That(GameEngine.PlayerPos(b), Is.EqualTo(1));
        Assert.That(b.PlayerSteadyUntilRound, Is.EqualTo(0), "无效位移不给稳态");
        Assert.That(b.Log.Last(), Does.Contain("你已在1号位，无法再被拉动（不判控制）"));
        var probe = new Rng { State = rng };
        probe.NextDouble();
        probe.NextDouble();
        Assert.That(s.Rng.State, Is.EqualTo(probe.State), "边界只抽了命中与抽值，没有抽控制");

        // 控制失败：命中但稳住
        StandAt(s, 3);
        Force(s, Below(0.01), Any(), AtLeast(0.99));
        EnemyAct(s, c);
        Assert.That(GameEngine.PlayerPos(b), Is.EqualTo(3));
        Assert.That(b.Log.Last(), Does.Contain("你稳住了，没有被拉动"));
    }

    /// <summary>RES-Q80b：搬运感染者推撞击退：命中后判控制，成功则单人后移一格（伤害照算）；4 号位边界不移、不判控制。</summary>
    [Test]
    public void RES_Q80b_HaulerKnockback_MovesSolo_BoundaryKeepsDamage()
    {
        var s = Fight(9202, "z_hauler");
        Ready(s);
        var b = s.Battle!;
        var h = Enemy(s, 1);
        h.Intent = "charge";
        h.IntentStep = 1;
        var h0 = s.Stats.Health;
        Force(s, Below(0.01), Any(), Below(0.01));   // 命中、抽值、控制成功
        EnemyAct(s, h);
        Assert.That(s.Stats.Health, Is.LessThan(h0), "推撞伤害照算");
        Assert.That(GameEngine.PlayerPos(b), Is.EqualTo(2), "被击退到 2 号位（空位直接移入）");
        Assert.That(b.Log.Last(), Does.Contain("你被击退到2号位"));

        // 4 号位边界：直接调攻击步骤（4 号位本不在推撞目标位里），伤害照算、不移、不判控制
        StandAt(s, 4);
        h.Intent = "charge";
        h.IntentStep = 1;
        b.PlayerSteadyUntilRound = 0;
        s.Stats.Health = 20;
        Force(s, Below(0.01), Any());
        var rng = s.Rng.State;
        typeof(GameEngine).GetMethod("DoAttackStep", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .Invoke(E, new object[] { s, b, h, E.StepOf(h), false, 1.0, false, false });
        Assert.That(GameEngine.PlayerPos(b), Is.EqualTo(4));
        Assert.That(s.Stats.Health, Is.LessThan(20), "其他伤害照算");
        Assert.That(b.PlayerSteadyUntilRound, Is.EqualTo(0));
        Assert.That(b.Log.Last(), Does.Contain("你已在4号位，无法再被击退（不判控制）"));
        var probe = new Rng { State = rng };
        probe.NextDouble();
        probe.NextDouble();
        Assert.That(s.Rng.State, Is.EqualTo(probe.State), "只抽了命中与抽值");
    }

    /// <summary>RES-Q80b：我方逼退把敌人往后推一格：后格空则移入、有同伴则交换（不另判控制）。</summary>
    [Test]
    public void RES_Q80b_PlayerKnockback_EmptyMove_AllySwap()
    {
        var s = Fight(9203, "worker", "shambler");
        Ready(s);
        var b = s.Battle!;
        Arm(s, "wooden_spear");
        s.Skills["spear"].Level = 3;
        Place(s, (1, 1), (2, 3));
        foreach (var e in b.Alive) e.Hp = 50;
        Force(s, Below(0.01), AtLeast(0.99), Any(), Below(0.01));
        Assert.That(Cmd(s, "spear_03", 1).Ok, Is.True);
        Assert.That((Enemy(s, 1).Slot, Enemy(s, 2).Slot), Is.EqualTo(("2", "3")), "后格空：移入");
        b.PlayerAp = 3;
        b.SkillUsedRound.Clear();
        Enemy(s, 1).SteadyUntilRound = 0;
        Force(s, Below(0.01), AtLeast(0.99), Any(), Below(0.01));
        Assert.That(Cmd(s, "spear_03", 1).Ok, Is.True);
        Assert.That((Enemy(s, 1).Slot, Enemy(s, 2).Slot), Is.EqualTo(("3", "2")), "后格有同伴：一次原子交换");
        Assert.That(b.Log.Last(), Does.Contain("被击退到3号位（游荡尸到2号位）"));
    }

    /// <summary>RES-Q80d：突进斩 123→12：1 号位原地出手，2/3 号位前移一格再攻，4 号位不可用（不靠单人兼容绕过）。</summary>
    [Test]
    public void RES_Q80d_Lunge_InPlaceAt1_AdvanceFrom3_RejectedAt4()
    {
        var bs = (JObject)Res()["positions"]!["boundarySkillException"]!["blade_07"]!;
        Assert.That(((JArray)bs["ownSlots"]!).Select(x => (int)x!), Is.EqualTo(new[] { 1, 2, 3 }));
        Assert.That(E.SkillNode("blade_07")!.Positions, Is.EqualTo("123→12"));

        var s = Fight(9204, "worker");
        Ready(s);
        var b = s.Battle!;
        Arm(s, "stone_knife");
        s.Skills["blade"].Level = 7;
        Enemy(s, 1).Hp = 50;
        Assert.That(E.PreviewCommand(s, BattleCommand.Of("blade_07", 1)).Notes, Has.Some.Contains("在1号位：原地出手"));
        Force(s, Below(0.01), AtLeast(0.99), Any());
        Assert.That(Cmd(s, "blade_07", 1).Ok, Is.True);
        Assert.That(GameEngine.PlayerPos(b), Is.EqualTo(1));
        Assert.That(b.Log.Last(), Does.Contain("在1号位原地出手"));

        b.PlayerActing = true;
        b.PlayerAp = 3;
        b.SkillUsedRound.Clear();
        StandAt(s, 4);
        Assert.That(E.CommandBlocked(s, BattleCommand.Of("blade_07", 1)), Does.Contain("只能在我方1、2、3号位使用（你在4号位）"));
        StandAt(s, 3);
        Assert.That(E.PreviewCommand(s, BattleCommand.Of("blade_07", 1)).Notes, Has.Some.Contains("先前移到2号位再攻击"));
        Force(s, Below(0.01), AtLeast(0.99), Any());
        Assert.That(Cmd(s, "blade_07", 1).Ok, Is.True);
        Assert.That(GameEngine.PlayerPos(b), Is.EqualTo(2), "前移一格");
        Assert.That(b.PlayerAp, Is.EqualTo(1), "技能 2AP，自带移动不另收调整站位的 AP");
    }

    /// <summary>RES-Q80d：挣脱 1234→自身：清减速并后移一格；4 号位原地清；无可解状态且在 4 号位整条拒绝（不扣成本/冷却）；不解失衡/出血。</summary>
    [Test]
    public void RES_Q80d_Struggle_BackOne_InPlaceAt4_NoBenefitRejected()
    {
        var u = (JObject)Res()["positions"]!["boundarySkillException"]!["unarmed_07"]!;
        Assert.That(E.SkillExecOf("unarmed_07")!.CleanseStatuses, Is.EqualTo(((JArray)u["clears"]!).Select(x => (string)x!)));
        Assert.That(E.SkillNode("unarmed_07")!.Positions, Is.EqualTo("1234→自身"));

        var s = Fight(9205, "worker");
        Ready(s);
        var b = s.Battle!;
        s.Skills["unarmed"].Level = 7;
        b.PlayerAp = 6;
        StandAt(s, 4);
        Assert.That(E.CommandBlocked(s, BattleCommand.Of("unarmed_07")), Does.Contain("整条无收益"));
        var e0 = s.Stats.Energy;
        Assert.That(Cmd(s, "unarmed_07").Ok, Is.False);
        Assert.That((b.PlayerAp, s.Stats.Energy, b.SkillUsedRound.ContainsKey("unarmed_07")), Is.EqualTo((6, e0, false)), "拒绝不扣成本、不进冷却");
        b.PlayerSlowActive = true;
        Assert.That(E.PreviewCommand(s, BattleCommand.Of("unarmed_07")).EffectText, Is.EqualTo("解除减速；已在4号位，原地不移"));
        Assert.That(Cmd(s, "unarmed_07").Ok, Is.True);
        Assert.That((b.PlayerSlowActive, GameEngine.PlayerPos(b)), Is.EqualTo((false, 4)), "4 号位原地清");

        b.SkillUsedRound.Clear();
        b.PlayerAp = 6;
        StandAt(s, 2);
        b.PlayerSlowPending = true;
        b.PlayerStagger = true;
        E.ApplyPlayerBleed(s);
        Assert.That(Cmd(s, "unarmed_07").Ok, Is.True);
        Assert.That(GameEngine.PlayerPos(b), Is.EqualTo(3), "后移一格");
        Assert.That((b.PlayerSlowPending, b.PlayerStagger, s.Body.BleedTriggers > 0), Is.EqualTo((false, true, true)), "只解减速，不解失衡、出血");
        // 没有可解状态但还能后移：仍有收益，可用
        b.SkillUsedRound.Clear();
        b.PlayerAp = 6;
        Assert.That(E.CommandBlocked(s, BattleCommand.Of("unarmed_07")), Is.Null);
    }

    // ───────────── M176：空前排迫近、敌人前移与余 AP ─────────────

    /// <summary>
    /// RES-Q80e：敌在合法己位、够不到躲在 3 号位的单人、你前面全空 → 1AP 迫近 + 2AP 一次 ×0.70 弱近袭；不咬（即使意图是咬击）、不附控制、
    /// 不改你的真实号位、不触发架矛；你在 1 号位时照常攻击；追击 2AP 付不起迫近。
    /// </summary>
    [Test]
    public void RES_Q80e_ExposureApproach_WeakNoBite_KeepsSlot_ApBudget()
    {
        var ea = (JObject)Res()["enemyActivation"]!["exposureApproach"]!;
        var cfgA = Cfg.CombatMath.ExposureApproach;
        Assert.That((cfgA.ApproachAp, cfgA.PlainAttackAp, cfgA.DamageFactor), Is.EqualTo(((int)ea["approachAp"]!, (int)ea["plainAttackAp"]!, (double)ea["damageFactor"]!)));
        Assert.That((bool)ea["biteOrControlOrOtherSpecialIntent"]!, Is.False);
        Assert.That((bool)ea["changesOpponentActualSlot"]!, Is.False);

        var s = Fight(9301, "crawler");
        Ready(s);
        var b = s.Battle!;
        var z = Enemy(s, 1);
        z.Intent = "bite";   // 即使公开意图是咬击，迫近弱近袭也不咬
        StandAt(s, 3);
        Assert.That(E.PlanFor(b, z, E.StepOf(z)), Is.EqualTo(GameEngine.ReachPlan.Approach));
        Assert.That(E.IntentShown(b, z), Does.StartWith("迫近你（3号位）弱近袭 0.28—0.56（1+2AP，不咬不控）"));
        Assert.That(E.PlanFor(b, z, E.StepOf(z), 2), Is.EqualTo(GameEngine.ReachPlan.Guard), "追击 2AP 付不起 1+2AP 的迫近");
        b.BraceCharges = 1;
        b.BracePage = 1;
        var h0 = s.Stats.Health;
        var infected = s.Body.Infected;
        Force(s, Below(0.01), AtLeast(0.999));   // 命中、抽值取最高
        EnemyAct(s, z);
        Assert.That(h0 - s.Stats.Health, Is.EqualTo(0.8 * 0.7).Within(2e-3), "伤害×0.70（扯腿 0.4—0.8）");
        Assert.That(GameEngine.PlayerPos(b), Is.EqualTo(3), "不改你的真实号位");
        Assert.That(b.PlayerSlowPending, Is.False, "不附扯腿减速");
        Assert.That(s.Body.Infected, Is.EqualTo(infected), "不咬、不判感染");
        Assert.That(b.BraceCharges, Is.EqualTo(1), "迫近不是主动前移，不触发架矛");
        Assert.That(b.Log.Any(l => l.Contains("迫近3号位的你")), Is.True);
        Assert.That(b.Log.Last(), Does.Contain("弱近袭命中"));
        Assert.That(z.Pos, Is.EqualTo(1), "迫近不移动敌人自己的号位");

        // 你在 1 号位：有正常目标，照常攻击；你在 3 号位但前面有同伴挡住：不算暴露
        StandAt(s, 1);
        Assert.That(E.PlanFor(b, z, E.StepOf(z)), Is.EqualTo(GameEngine.ReachPlan.Execute));
        b.Allies = new List<string> { "", "", GameState.PlayerId, "ally" };
        Assert.That(GameEngine.AlliesExposed(b), Is.True, "最前者（你）前面全空");
        b.Allies = new List<string> { "", "ally", GameState.PlayerId };
        Assert.That(E.PlanFor(b, z, E.StepOf(z)), Is.Not.EqualTo(GameEngine.ReachPlan.Approach), "最前的是 2 号位同伴（不是暴露的你）：你不是迫近对象");
    }

    /// <summary>RES-Q80e：站太靠后的敌人先 1AP 前移；之后剩余 AP 不够 1+2 就结束（普通 3AP），精英 4AP 才能前移后再迫近。</summary>
    [Test]
    public void RES_Q80e_AdvanceThenApproach_CountsAdvanceAp()
    {
        var s = Fight(9302, "shambler");
        Ready(s);
        var b = s.Battle!;
        var z = Enemy(s, 1);
        z.Intent = "attack";
        Place(s, (1, 3));
        StandAt(s, 3);
        Assert.That(E.PlanFor(b, z, E.StepOf(z), 3), Is.EqualTo(GameEngine.ReachPlan.Advance), "普通 3AP：前移 1 后只剩 2AP");
        Assert.That(E.PlanFor(b, z, E.StepOf(z), 4), Is.EqualTo(GameEngine.ReachPlan.AdvanceThenApproach), "4AP：前移 1 + 迫近 1 + 弱近袭 2");
        var h0 = s.Stats.Health;
        EnemyAct(s, z);
        Assert.That(z.Pos, Is.EqualTo(2), "前移一格（前格空直接移入）");
        Assert.That(s.Stats.Health, Is.EqualTo(h0), "AP 不够迫近：不攻击");
        Assert.That(b.Log.Last(), Does.Contain("还够不到，结束行动"));
        Assert.That(z.Guard, Is.False, "余 AP 不自动防御（Q77）");
    }

    /// <summary>RES-Q77：每次正常激活最多一个主意图，够得到就一次攻击，余 AP 直接结束，不自动防御、不追加第二击。</summary>
    [Test]
    public void RES_Q77_OneMainIntent_RemainingApEnds_NoAutoGuard()
    {
        var ea = (JObject)Res()["enemyActivation"]!;
        Assert.That((string)ea["remainingAp"]!, Is.EqualTo("end_without_auto_defend"));
        foreach (var id in new[] { "shambler", "z_pump_foreman" })
        {
            var s = Fight(9303, id);
            Ready(s);
            var b = s.Battle!;
            var z = Enemy(s, 1);
            z.Intent = "attack";
            z.IntentStep = -2;
            s.Stats.Health = 20;
            var n0 = b.Log.Count;
            EnemyAct(s, z);
            var lines = b.Log.Skip(n0).Where(l => l.Contains(E.EnemyName(z))).ToList();
            Assert.That(lines, Has.Count.EqualTo(1), $"{id}（{E.EnemyAp(z)}AP）只出手一次：{string.Join(" | ", lines)}");
            Assert.That(z.Guard, Is.False, $"{id} 余 AP 不自动防御");
        }
    }

    /// <summary>
    /// RES-Q78：前移一格——前格空则移入、有同侧存活同伴则相邻交换（1AP）；被换的同伴不多激活、不改锁定队列；
    /// 主动前移触发架矛（次数有界），被换下去的同伴与强制位移不触发。
    /// </summary>
    [Test]
    public void RES_Q78_AdvanceEmptyOrSwap_PassiveNoActivation_BraceOnlyActive()
    {
        var em = (JObject)Res()["enemyActivation"]!;
        Assert.That((string)em["frontMove"]!, Is.EqualTo("adjacent_empty_move_or_adjacent_friendly_swap_cost1AP"));
        Assert.That((bool)em["passivelySwappedAllyDoesNotActivate"]!, Is.True);

        var s = Fight(9304, "shambler", "shambler");
        Ready(s);
        var b = s.Battle!;
        Arm(s, "pike");
        s.Skills["spear"].Level = 5;
        b.PlayerAp = 3;
        Assert.That(Cmd(s, "spear_05").Ok, Is.True);
        Assert.That(b.BraceCharges, Is.EqualTo(2), "加固长枪架阵：迎击最多 2 次");
        var a = Enemy(s, 1);
        var c = Enemy(s, 2);
        foreach (var e in b.Alive) { e.Hp = 50; e.Intent = "attack"; e.IntentStep = -2; }
        Place(s, (1, 2), (2, 3));   // 1 号位空
        var q = b.Queue.ToList();
        var actedA = a.Acted;
        EnemyAct(s, c);
        Assert.That((a.Pos, c.Pos), Is.EqualTo((3, 2)), "前格有同伴：相邻交换");
        Assert.That(a.Acted, Is.EqualTo(actedA), "被换下去的同伴不多激活");
        Assert.That(b.Queue, Is.EqualTo(q), "锁定队列不变");
        Assert.That(b.BraceCharges, Is.EqualTo(1), "主动前移触发一次迎击（被换下去的不触发）");
        Assert.That(b.Log.Any(l => l.Contains("前移到2号位（游荡尸退到3号位）")), Is.True);

        // 前格空：直接移入
        Place(s, (1, 4), (2, 3));
        EnemyAct(s, c);
        Assert.That(c.Pos, Is.EqualTo(2), "前格空：移入");
        Assert.That(b.BraceCharges, Is.EqualTo(0), "次数用完");
        // 我方逼退造成的强制位移不触发迎击
        b.BraceCharges = 1;
        b.PlayerAp = 3;
        b.SkillUsedRound.Clear();
        s.Skills["spear"].Level = 3;
        Force(s, Below(0.01), AtLeast(0.99), Any(), Below(0.01));
        Assert.That(Cmd(s, "spear_03", c.Index).Ok, Is.True);
        Assert.That(b.BraceCharges, Is.EqualTo(1), "强制位移不触发架矛");
    }

    // ───────────── M177：伤害增益递减、教学平加、反应与指挥经验 ─────────────

    /// <summary>RES-Q82b：rawB≤1.35 原值，以上 1.35+0.25×(1−exp(−(rawB−1.35)/0.25))，渐近 1.6、单调、每个新增正增益仍有正收益；不先 cap 1.5。</summary>
    [Test]
    public void RES_Q82b_DamageBuffSoftCap_Curve_AndInPreview()
    {
        var sc = (JObject)Res()["damage"]!["softCap"]!;
        Assert.That(((double)sc["knee"]!, (double)sc["tail"]!, (double)sc["asymptote"]!), Is.EqualTo((Cfg.CombatMath.DamageBuffKnee, Cfg.CombatMath.DamageBuffTail, 1.6)));
        Assert.That(E.DamageBuffCurve(1.0), Is.EqualTo(1.0));
        Assert.That(E.DamageBuffCurve(1.35), Is.EqualTo(1.35).Within(1e-12));
        Assert.That(E.DamageBuffCurve(1.35 * 1.15 * 1.15 * 1.06), Is.EqualTo(1.5715).Within(1e-4), "现有合法顶组合 rawB≈1.8925 → B≈1.5715（旧 cap 1.5）");
        var prev = 0.0;
        for (var r = 1.0; r < 4; r += 0.01)
        {
            var v = E.DamageBuffCurve(r);
            Assert.That(v, Is.GreaterThan(prev), $"单调递增 r={r}");
            Assert.That(v, Is.LessThan(1.6));
            prev = v;
        }

        var s = Fight(9401, "shambler");
        Ready(s);
        var b = s.Battle!;
        var axe = Arm(s, "hatchet");
        b.AllOutFactor = 1.3;
        b.AllOutUntilRound = b.Round + 1;
        b.ChargeFactor = 1.3;
        b.ChargeUntilRound = b.Round + 1;
        b.ChargeItem = axe.InstanceId;
        var pv = E.PreviewCommand(s, BattleCommand.Of("basic_attack", 1));
        var t = pv.Targets.Single();
        Assert.That(t.RawDamageBuff, Is.EqualTo(1.69).Within(1e-9), "总攻令×1.3、斧蓄力×1.3 先乘成 rawB");
        Assert.That(t.DamageBuff, Is.EqualTo(E.DamageBuffCurve(1.69)).Within(1e-12));
        Assert.That(t.DamageBuff, Is.GreaterThan(1.5), "不再先 cap 1.5");
        Assert.That(pv.Notes, Has.Some.Contains("伤害增益合计×1.69，超过×1.35的部分递减"));
    }

    /// <summary>RES-Q82d：教学 +0.1 在乘算后、扣甲前，每个目标每条指令只在第一下命中加一次；第一下未命中可留到第二下。</summary>
    [Test]
    public void RES_Q82d_TutorialFlat_OncePerTargetPerCommand()
    {
        var ft = (JObject)Res()["damage"]!["flatTutorial"]!;
        Assert.That(((double)ft["amount"]!, (bool)ft["multiHitSameTargetFirstSuccessfulHitOnly"]!), Is.EqualTo((0.1, true)));

        var s = Fight(9402, "shambler");
        Ready(s);
        var b = s.Battle!;
        s.Traits.Add(new TraitInstance { EffectId = "tutorial_attack_bonus_001", Quality = "green", Label = "攻击力+0.1", FlatAttack = 0.1 });
        s.Skills["unarmed"].Level = 10;
        b.PlayerAp = 6;
        Enemy(s, 1).Hp = 50;
        var t = E.PreviewCommand(s, BattleCommand.Of("unarmed_10", 1)).Targets.Single();
        Assert.That(t.Flat, Is.EqualTo(0.1));
        Force(s, Below(0.3), AtLeast(0.2), Below(0.01), Below(0.3), AtLeast(0.2), Below(0.01));   // 两下都命中、不暴击、抽值近最低
        Assert.That(Cmd(s, "unarmed_10", 1).Ok, Is.True);
        Assert.That(50 - Enemy(s, 1).Hp, Is.EqualTo(2 * t.DamageMin - 0.1).Within(0.02), "连拳两下只加一次 +0.1（不是 +0.2）");

        b.PlayerAp = 6;
        b.SkillUsedRound.Clear();
        Enemy(s, 1).Hp = 50;
        Force(s, AtLeast(0.99), Below(0.3), AtLeast(0.2), Below(0.01));
        Assert.That(Cmd(s, "unarmed_10", 1).Ok, Is.True);
        Assert.That(50 - Enemy(s, 1).Hp, Is.EqualTo(t.DamageMin).Within(0.01), "第一下未命中：+0.1 留给第二下");
    }

    /// <summary>
    /// RES-Q82e：反击/迎击用架势那一页的武器，按当前真实己位/射程重验；不合法不出手也不扣次数；
    /// 不另扣体力/耐久，不吃也不消耗鼓舞/总攻令/斧蓄力。
    /// </summary>
    [Test]
    public void RES_Q82e_Reaction_RevalidatesOwnSlot_NoCost_NoBuffUse()
    {
        var rx = (JObject)Res()["reactions"]!;
        Assert.That(((int)rx["additionalEnergyAmmoWear"]!, (bool)rx["benefitOrConsumeInspireAllOutAxeCharge"]!, (bool)rx["noReactionChains"]!), Is.EqualTo((0, false, true)));

        var s = Fight(9403, "shambler");
        Ready(s);
        var b = s.Battle!;
        var knife = Arm(s, "stone_knife");
        s.Skills["blade"].Level = 5;
        Assert.That(Cmd(s, "blade_05").Ok, Is.True);
        Assert.That(b.CounterFactor, Is.GreaterThan(0));
        var z = Enemy(s, 1);
        z.Hp = 50;
        z.Intent = "attack";
        z.IntentStep = -2;
        StandAt(s, 3);   // 刀使用位12：在 3 号位反击不合法
        s.Stats.Health = 20;
        Force(s, Below(0.01), Any());
        typeof(GameEngine).GetMethod("DoAttackStep", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .Invoke(E, new object[] { s, b, z, E.StepOf(z), true, 1.0, false, false });
        Assert.That(b.Log.Last(), Does.Contain("你的反击没有出手：石刀要在我方1、2号位（你在3号位）（架势保留）"));
        Assert.That(b.CounterFactor, Is.GreaterThan(0), "不合法不扣反击");

        StandAt(s, 1);
        b.InspireBonus = 6;
        b.InspireUntilRound = b.Round + 1;
        b.AllOutFactor = 1.2;
        b.AllOutUntilRound = b.Round + 1;
        var e0 = s.Stats.Energy;
        var d0 = knife.Durability;
        Force(s, Below(0.01), Any(), Below(0.01), AtLeast(0.99), Any());
        typeof(GameEngine).GetMethod("DoAttackStep", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .Invoke(E, new object[] { s, b, z, E.StepOf(z), true, 1.0, false, false });
        Assert.That(b.Log.Last(), Does.StartWith($"第{b.Round}轮 · 你反击（石刀）"));
        Assert.That(b.CounterFactor, Is.EqualTo(0), "反击用掉");
        Assert.That((s.Stats.Energy, knife.Durability), Is.EqualTo((e0, d0)), "不另扣体力、耐久");
        Assert.That((GameEngine.InspireActive(b), GameEngine.AllOutActive(b)), Is.EqualTo((true, true)), "反应不消耗鼓舞/总攻令");

        // 架矛：站在 4 号位（矛使用位123）迎击不合法，不扣次数
        var p = Fight(9404, "shambler");
        Ready(p);
        var pb = p.Battle!;
        Arm(p, "wooden_spear");
        p.Skills["spear"].Level = 5;
        Assert.That(Cmd(p, "spear_05").Ok, Is.True);
        var charges = pb.BraceCharges;
        StandAt(p, 4);
        var pz = Enemy(p, 1);
        pz.Intent = "attack";
        pz.IntentStep = -2;
        Place(p, (1, 3));
        EnemyAct(p, pz);
        Assert.That(pb.BraceCharges, Is.EqualTo(charges), "无合法反应目标不白扣迎击次数");
        Assert.That(pb.Log.Any(l => l.Contains("你的迎击没有出手") && l.Contains("不扣次数")), Is.True);
    }

    /// <summary>RES-Q82f：指挥经验真实使用才发：鼓舞被下一次攻击用掉（未命中也算）才 +2，每场每技能一次；群攻整条指令只消费一次增益。</summary>
    [Test]
    public void RES_Q82f_CommandXp_RealUseOnly_OncePerBattle_AreaConsumesOnce()
    {
        var cx = (JObject)Res()["commandXp"]!;
        Assert.That((bool)cx["areaCommandConsumesEffectOnceNotPerTarget"]!, Is.True);

        var s = Fight(9405, "shambler", "shambler");
        Ready(s);
        var b = s.Battle!;
        var sg = Arm(s, "shotgun");
        sg.LoadedAmmo = 4;
        Arm2(s, "signal_whistle");
        StandAt(s, 2);
        s.Skills["command"].Level = 1;
        s.Skills["firearms"].Level = 7;
        b.PlayerAp = 6;
        var xp0 = s.Skills["command"].Xp;
        Assert.That(Cmd(s, "command_01").Ok, Is.True);
        Assert.That(s.Skills["command"].Xp, Is.EqualTo(xp0), "施放鼓舞时不发经验");
        b.ActiveWeaponSlot = 1;
        foreach (var e in b.Alive) e.Hp = 50;
        Force(s, AtLeast(0.99), AtLeast(0.99));   // 两个目标都未命中
        Assert.That(Cmd(s, "firearms_07").Ok, Is.True, "专长射击打 1、2 号位");
        Assert.That(GameEngine.InspireActive(b), Is.False, "群攻整条指令消费一次鼓舞");
        Assert.That(s.Skills["command"].Xp - xp0, Is.EqualTo(Cfg.Xp.DamagingAttackCommand), "攻击未命中也是真实使用：+2");
        // 同场再鼓舞再用掉：不再发
        b.PlayerAp = 6;
        b.SkillUsedRound.Clear();
        b.ActiveWeaponSlot = 2;
        Assert.That(Cmd(s, "command_01").Ok, Is.True);
        b.ActiveWeaponSlot = 1;
        sg.LoadedAmmo = 4;
        Force(s, AtLeast(0.99), AtLeast(0.99));
        Assert.That(Cmd(s, "firearms_07").Ok, Is.True);
        Assert.That(s.Skills["command"].Xp - xp0, Is.EqualTo(Cfg.Xp.DamagingAttackCommand), "每场每技能一次");
    }

    // ───────────── M178：Q75/Q76/Q79 选 A 核对 ─────────────

    /// <summary>
    /// RES-Q75：装兼容医疗器具的医疗技能按真实己位（234）治存活友方（现在只有你自己），只扣施术者随身物资、与绷带共享回血冷却；
    /// 不装器具时 2AP 绷带/抗生素/特效药照样自用（1 号位也行），不远程取物。
    /// </summary>
    [Test]
    public void RES_Q75_MedicalSkillOwnSlotAndSelf_ItemSelfUseWithoutKit_SharedCooldown()
    {
        var md = (JObject)Res()["medical"]!;
        Assert.That(((bool)md["requiresCompatibleMedicalWeaponPageForSkills"]!, (bool)md["materialsFromCasterOnly"]!, (string)md["itemsWithoutMedicalKit"]!, (bool)md["existingTargetCooldownShared"]!),
            Is.EqualTo((true, true, "self_use_bandage_antibiotics_cure_2AP", true)));

        var s = Fight(9501, "worker");
        Ready(s);
        var b = s.Battle!;
        // 不装医疗器具：绷带物品 2AP 自用，1 号位也可
        E.DebugGive(s, "bandage", 2);
        s.Stats.Health = 10;
        var bd = E.BattleOptions(s).Single(o => o.Action == "bandage");
        Assert.That((bd.Available, bd.Ap), Is.EqualTo((true, 2)));
        Assert.That(Cmd(s, "bandage").Ok, Is.True);
        Assert.That(s.BandageReadyAt, Is.GreaterThan(s.GameMinute), "绷带回血冷却开始");
        // 装上野战医疗包：医疗技能在 1 号位不可用（使用位234，单人不再兼容1）
        b.PlayerAp = 6;
        Arm2(s, "field_medkit");
        s.Skills["medicine"].Level = 1;
        Assert.That(E.CommandBlocked(s, BattleCommand.Of("medicine_01")), Does.Contain("只能在我方2、3、4号位使用（你在1号位）"));
        StandAt(s, 2);
        var why = E.CommandBlocked(s, BattleCommand.Of("medicine_01"));
        Assert.That(why, Does.Contain("冷却").Or.Contain("没有").Or.Contain("随身"), "到 2 号位后只剩物资/冷却这类真实原因：" + why);
        Assert.That(E.PreviewCommand(s, BattleCommand.Of("medicine_01")).Notes, Has.Some.Contains("目标：你自己"));
    }

    /// <summary>RES-Q76：泛称持续 N 轮=施加当轮剩余+之后 N 个完整大回合，第 4 轮施加 2 轮到第 6 轮末；蓄力到 r+1；CD1 r+1 禁用 r+2 可用。</summary>
    [Test]
    public void RES_Q76_DurationRPlusN_ChargeRPlus1_CooldownSeparate()
    {
        var du = (JObject)Res()["duration"]!;
        Assert.That(((int)du["appliedAtRound"]!, (int)du["twoRoundExpiresEnd"]!), Is.EqualTo((4, 6)));

        var s = Fight(9502, "worker");
        Ready(s);
        var b = s.Battle!;
        b.Round = 4;
        b.PlayerAp = 6;
        Arm(s, "hatchet");
        s.Skills["axe"].Level = 5;
        Enemy(s, 1).Hp = 50;
        Force(s, Below(0.01), AtLeast(0.99), Any());
        Assert.That(Cmd(s, "axe_03", 1).Ok, Is.True);
        Assert.That(Enemy(s, 1).ArmorBreakUntilRound, Is.EqualTo(6), "破甲 2 轮：第 4 轮施加到第 6 轮末");
        Assert.That(E.CommandBlocked(s, BattleCommand.Of("axe_03", 1)), Does.StartWith("冷却中（第6轮起可用）"), "CD1：r+1 禁用、r+2 可用，与持续时间分开");
        Assert.That(Cmd(s, "axe_05").Ok, Is.True);
        Assert.That(b.ChargeUntilRound, Is.EqualTo(5), "蓄力“下一大回合末”到 r+1");
        Assert.That(E.SkillExecOf("command_01")!.BuffRounds, Is.EqualTo(2), "鼓舞持续 2 轮（r+2 轮末，与破甲同口径）");
    }

    /// <summary>RES-Q79：重装守卫重击面板×1.0；搬运/攀附抓击 80%/咬击 20%；未写咬击的近战不感染；攀附拉扯 0 直接伤害。</summary>
    [Test]
    public void RES_Q79_EnemyNumbersMatchContract()
    {
        var ea = (JObject)Res()["enemyAssumptions"]!;
        var guard = Cfg.Enemies.Single(x => x.Id == "z_guard");
        var heavy = guard.Intents.Cycle.Single(x => x.Verb == "重击");
        Assert.That(heavy.Factor, Is.EqualTo((double)ea["z_guardHeavyStrikeFactor"]!));
        foreach (var id in new[] { "z_hauler", "z_clinger" })
            Assert.That(Cfg.Enemies.Single(x => x.Id == id).Intents.Cycle.Single(x => x.Kind == "attack").BitePct, Is.EqualTo((double)ea["z_haulerAndZ_clingerMixedAttack"]!["bite"]!), id);
        Assert.That((bool)ea["implicitBiteOnUnspecifiedMelee"]!, Is.False);
        foreach (var id in new[] { "z_screamer", "z_spitter", "z_bloater" })
        {
            var cfg = Cfg.Enemies.Single(x => x.Id == id);
            Assert.That(cfg.Intents.Opening.Concat(cfg.Intents.Cycle).All(x => x.BitePct == 0), Is.True, $"{id} 没写咬击：不判感染");
        }
        var pull = Cfg.Enemies.Single(x => x.Id == "z_clinger").Intents.Cycle.Single(x => x.Kind == "pull");
        Assert.That(pull.Factor, Is.EqualTo((double)ea["z_clingerPullDamage"]!));
    }
}
