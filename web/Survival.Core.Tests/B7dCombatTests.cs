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
}
