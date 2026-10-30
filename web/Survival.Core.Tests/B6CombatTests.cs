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

    private ItemInstance Wear(GameState s, string itemId, string slot, string quality = "white", params string[] affixes)
    {
        var it = E.DebugCreateEquipment(s, itemId, quality, ItemInstance.EquipOwner(GameState.PlayerId, slot));
        if (affixes.Length > 0) it.Affixes = affixes.ToList();
        return it;
    }

    /// <summary>把这一轮剩下的行动走完（玩家激活一律结束行动），返回本轮玩家激活次数与追击标记。</summary>
    private (int playerActs, int playerChases, int enemyChases) FinishRound(GameState s)
    {
        var b = s.Battle!;
        var round = b.Round;
        int acts = 0, chases = 0, enemyChases = 0;
        for (var i = 0; i < 100 && s.Battle != null && b.Round == round && !s.Progress.Dead; i++)
        {
            if (E.AwaitingPlayer(s))
            {
                acts++;
                if (b.ActingPursuit) chases++;
                Cmd(s, "end_turn");
                continue;
            }
            if (b.Cursor < b.Queue.Count && b.Queue[b.Cursor] > 0 && GameEngine.IsPursuitSlot(b, b.Cursor)) enemyChases++;
            E.BattleStep(s);
        }
        return (acts, chases, enemyChases);
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

    // ───────────── M129：速度、行动队列与追击（R21；验收 CB02、CB03、RG02） ─────────────

    [Test]
    public void R21_EffectiveSpeed_GearCapStatusPenaltiesMin1()
    {
        var s = Fight(5, "shambler");
        var b = s.Battle!;
        Assert.That(E.PlayerEffectiveSpeed(s, b), Is.EqualTo(4), "玩家基础4");
        Wear(s, "swift_pendant", EquipSlots.CombatAccessory);                  // +1
        Wear(s, "scout_armor", EquipSlots.Armor, "white", "swift");        // +1 +1
        Wear(s, "barbed_spear", EquipSlots.Weapon1);                       // +1
        Assert.That(E.PlayerEffectiveSpeed(s, b), Is.EqualTo(7), "正速度合计最多+3");
        s.Stats.Energy = 3.9;
        s.Stats.Wakefulness = 19.9;
        Assert.That(E.PlayerEffectiveSpeed(s, b), Is.EqualTo(5), "E<4 与 S<20 各−1");
        s.Stats.Wakefulness = 20;
        Assert.That(E.PlayerEffectiveSpeed(s, b), Is.EqualTo(6), "S=20 不算低清醒（速度按 S<20）");
        foreach (var it in s.Items.Where(i => i.Owner.StartsWith("equip:")).ToList()) s.Items.Remove(it);
        Wear(s, "heavy_armor", EquipSlots.Armor);                          // −2
        Wear(s, "breach_axe", EquipSlots.Weapon1);                         // −2
        Assert.That(E.PlayerEffectiveSpeed(s, b), Is.EqualTo(1), "4−2−2−1=−1 → 最低1");
    }

    [Test]
    public void CB02_TwoTimesSpeed_PursuitEveryOtherRound_ThreeTimesEveryRound()
    {
        // 4 对 2：q=50 → 每两轮一次追击
        var s = Fight(6, "shambler");
        var b = s.Battle!;
        Assert.That(b.NormalCount, Is.EqualTo(2));
        Assert.That(b.Queue, Is.EqualTo(new[] { 0, 1 }), "第1轮进度50，未满100");
        Assert.That(b.PlayerPursuit, Is.EqualTo(50).Within(1e-9));
        var r1 = FinishRound(s);
        Assert.That(r1, Is.EqualTo((1, 0, 0)));
        Assert.That(b.Queue, Is.EqualTo(new[] { 0, 1, 0 }), "第2轮满100：正常队列之后追击一次");
        Assert.That(b.PlayerPursuit, Is.EqualTo(0).Within(1e-9), "扣100");
        Assert.That(b.Log.Last(x => x.Contains("行动顺序")), Does.Contain("追击：你"));
        var r2 = FinishRound(s);
        Assert.That(r2, Is.EqualTo((2, 1, 0)), "一轮最多一次追击");

        // 6 对 2（3倍速）：q=clamp(100)=100 → 每轮一次，进度不溢出累积
        var t = Fight(7, "shambler");
        Wear(t, "swift_pendant", EquipSlots.CombatAccessory);
        Wear(t, "scout_armor", EquipSlots.Armor);
        var tb = t.Battle!;
        FinishRound(t);
        for (var round = 0; round < 3 && t.Battle != null; round++)
        {
            Assert.That(tb.Queue.Count(x => x == 0), Is.EqualTo(2), "3倍速每轮一次追击");
            Assert.That(tb.PlayerPursuit, Is.EqualTo(50).Within(1e-9), "q 封顶100：第1轮（换装前）留下的50不因此多追一次，每轮仍只一次");
            FinishRound(t);
        }
    }

    [Test]
    public void CB02_EnemyPursuit_FasterEnemyGetsExtraAttack_TutorialNever()
    {
        // 跑尸 6 对玩家 4：q=25 → 第4轮追击；追击是一次普通攻击
        var s = Fight(8, "runner");
        var b = s.Battle!;
        Assert.That(b.Queue.First(), Is.EqualTo(1), "速度快的先动");
        int chases = 0;
        for (var round = 1; round <= 4 && s.Battle != null && !s.Progress.Dead; round++)
        {
            s.Stats.Health = 20;
            chases += FinishRound(s).enemyChases;
        }
        Assert.That(chases, Is.EqualTo(1), "4轮里追击一次");
        Assert.That(b.Log.Any(x => x.Contains("追击")), Is.True);
    }

    [Test]
    public void CB03_Pursuit_DoesNotRefreshPerRoundLimits_OrAddTick()
    {
        var s = Fight(9, "crawler");   // 4 对 1：每轮都有追击
        var b = s.Battle!;
        Ready(s);
        var minute = s.GameMinute;
        Assert.That(Cmd(s, "breathe").Ok, Is.True);
        Assert.That(b.BreatheUsed, Is.True);
        Cmd(s, "end_turn");
        while (s.Battle != null && !E.AwaitingPlayer(s)) E.BattleStep(s);
        Assert.That(b.ActingPursuit, Is.True, "同一轮的追击激活");
        Assert.That(b.PlayerAp, Is.EqualTo(2), "追击固定2AP");
        Assert.That(E.CommandBlocked(s, BattleCommand.Of("breathe")), Is.EqualTo("本回合已调整过呼吸"), "调息每大回合一次，追击不刷新");
        Assert.That(s.GameMinute, Is.EqualTo(minute), "追击不推进世界时间");
        Cmd(s, "end_turn");
        while (s.Battle != null && b.Round == 1) Step(s);
        Assert.That(s.GameMinute, Is.EqualTo(minute + 5), "整轮（含追击）只推进一次5分钟");
        Assert.That(b.Ticks, Is.EqualTo(1));
    }

    // ───────────── M130：AP 阶位与通用动作（R22；验收 CB04、RG02） ─────────────

    [Test]
    public void CB04_TacticalTier_3_4_5Ap_TempCap6_NotStackedBySkills()
    {
        var s = Fight(11, "worker");
        var p = s.Player;
        Assert.That(p.TacticalTier, Is.EqualTo(0), "新局阶位0");
        Assert.That((E.NormalActivationAp(p), E.NormalActivationAp(p, 9)), Is.EqualTo((3, 6)), "阶位0：3AP；临时+AP后最多6");
        Assert.That(E.DebugSetTacticalTier(s, 1), Does.Contain("4AP"));
        Assert.That(E.NormalActivationAp(p), Is.EqualTo(4));
        E.DebugSetTacticalTier(s, 2);
        Assert.That((E.NormalActivationAp(p), E.NormalActivationAp(p, 1), E.NormalActivationAp(p, 3)), Is.EqualTo((5, 6, 6)));
        E.DebugSetTacticalTier(s, 7);
        Assert.That(p.TacticalTier, Is.EqualTo(2), "最高阶位2");
        // RG02：AP 不按熟练度门类堆叠
        E.DebugSetSkillLevels(s, 10, false);
        Ready(s);
        Assert.That((s.Battle!.PlayerAp, s.Battle.ActivationAp), Is.EqualTo((5, 5)), "全部十级仍是阶位2的5AP");
        var json = SaveSerializer.Serialize(s);
        Assert.That(SaveSerializer.Deserialize(json, Cfg).Player.TacticalTier, Is.EqualTo(2), "阶位随角色存档");
    }

    [Test]
    public void R22_GeneralActionCosts()
    {
        var s = Fight(12, "worker");
        Ready(s);
        Arm(s, "pistol").LoadedAmmo = 1;
        var opts = E.BattleOptions(s).ToDictionary(o => o.Action);
        Assert.That(opts["basic_attack"].Ap, Is.EqualTo(2), "普通攻击2AP");
        Assert.That(opts["breathe"].Ap, Is.EqualTo(2), "调息2AP");
        Assert.That((opts["guard"].Ap, opts["swap"].Ap, opts["reload"].Ap), Is.EqualTo((1, 1, 1)), "防御/换位/装填1AP");
        Assert.That(opts["retreat"].Ap, Is.EqualTo(2), "撤退2AP");
        Assert.That(opts["desperate_punch"].Ap, Is.EqualTo(1), "应急拳1AP");
        Assert.That(opts["desperate_punch"].Energy, Is.EqualTo(0), "应急拳E0");
        Assert.That(Cfg.CombatActions.Single(a => a.Id == "breathe").RestoreEnergy, Is.EqualTo(1.2), "调息E+1.2");
    }

    [Test]
    public void R22_Guard_Half_ShieldGuard04_WearsShieldOnce_NoStack()
    {
        var s = Fight(13, "worker");
        Ready(s);
        Assert.That(Cmd(s, "guard").Ok, Is.True);
        Assert.That((s.Battle!.PlayerGuard, s.Battle.PlayerGuardFactor), Is.EqualTo((true, 0.5)));
        Assert.That(E.CommandBlocked(s, BattleCommand.Of("guard")), Is.EqualTo("已在防御（不叠层）"));

        var t = Fight(14, "worker");
        var shield = Wear(t, "wooden_shield", EquipSlots.Weapon2);
        var d0 = shield.Durability!.Value;
        Ready(t);
        Assert.That(E.PreviewCommand(t, BattleCommand.Of("guard")).Notes.Single(), Does.StartWith("盾类格挡（木盾）").And.Contain("×0.4"));
        Assert.That(Cmd(t, "guard").Ok, Is.True);
        Assert.That(t.Battle!.PlayerGuardFactor, Is.EqualTo(0.4), "装着盾（另一页也算）：盾类格挡×0.4");
        Assert.That(shield.Durability, Is.EqualTo(d0 - 1).Within(1e-9), "盾执行格挡指令磨损1");
        // 敌人命中时按 0.4 减伤，触发格挡不再重复磨损盾
        var e = t.Battle.Alive.First();
        var hp = t.Stats.Health;
        Force(t, Below(0.01), Below(0.0001));
        typeof(GameEngine).GetMethod("EnemyAttack", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .Invoke(E, new object[] { t, e, Cfg.Enemies.Single(x => x.Id == e.EnemyId), false });
        var raw = Cfg.Enemies.Single(x => x.Id == "worker").DamageMin;
        Assert.That(hp - t.Stats.Health, Is.EqualTo(raw * 0.4).Within(1e-3));
        Assert.That(shield.Durability, Is.EqualTo(d0 - 1).Within(1e-9), "触发格挡不重复扣");
        Assert.That(t.Battle.PlayerGuard, Is.False, "只挡一次");
    }

    [Test]
    public void R22_DesperatePunch_OnlyWhenNoEquippedAttackAffordable()
    {
        var s = Fight(15, "shambler");
        Wear(s, "stone_knife", EquipSlots.Weapon1);      // 基础E0.4
        Wear(s, "hatchet", EquipSlots.Weapon2);          // 基础E0.65
        Ready(s);
        var knifeE = E.SkillEnergyCost(0.4, 1);
        Assert.That(E.MinEquippedAttackEnergy(s), Is.EqualTo(knifeE).Within(1e-9), "两页里最便宜的攻击技能");
        E.SelectWeaponPage(s, 2);
        s.Stats.Energy = knifeE + 0.01;
        Assert.That(E.CommandBlocked(s, BattleCommand.Of("basic_attack", 1)), Does.StartWith("体力不足"), "斧页付不起");
        Assert.That(E.CommandBlocked(s, BattleCommand.Of("desperate_punch", 1)), Does.StartWith("只在体力不足以使用任何已装备攻击技能时可用"), "刀页还付得起，不放开应急拳");
        s.Stats.Energy = knifeE - 0.01;
        Assert.That(E.CommandBlocked(s, BattleCommand.Of("desperate_punch", 1)), Is.Null);
        var e0 = s.Stats.Energy;
        var ap = s.Battle!.PlayerAp;
        Force(s, AtLeast(0.999));
        Assert.That(Cmd(s, "desperate_punch", 1).Ok, Is.True);
        Assert.That((s.Stats.Energy, s.Battle.PlayerAp), Is.EqualTo((e0, ap - 1)), "1AP、E0");
    }

    [Test]
    public void R21_SameSpeed_OrderByLockedRandomThenActorId_NotReshuffled()
    {
        var s = Fight(10, "shambler", "worker");   // 同为速度2
        var b = s.Battle!;
        var e1 = Enemy(s, 1);
        var e2 = Enemy(s, 2);
        var first = e1.TieRoll >= e2.TieRoll ? 1 : 2;
        Assert.That(b.Queue.Skip(1).Take(2).First(), Is.EqualTo(first), "同速按锁定随机数u降序");
        FinishRound(s);
        Assert.That(b.Queue.Skip(1).Take(2).First(), Is.EqualTo(first), "每轮同一顺序，不重新洗");
        var json = SaveSerializer.Serialize(s);
        var t = SaveSerializer.Deserialize(json, Cfg);
        Assert.That(t.Battle!.Enemies.Select(e => e.TieRoll), Is.EqualTo(b.Enemies.Select(e => e.TieRoll)), "u 随存档，读档不重抽");
    }
}
