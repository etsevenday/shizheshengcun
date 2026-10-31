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

    // ───────────── M137：B6 线上第 1 轮试玩修复 ─────────────

    /// <summary>
    /// BUG1/UX3：3、4 号位够不到，近战攻击意图（抓击/咬击/冲撞）实际只前压/等待，敌卡意图同源显示“前压/等待（在3号位够不到）”；
    /// 嚎叫等非攻击意图照常执行（旧 R03 §1“远程/嚎叫可按合法意图行动”）。
    /// </summary>
    [Test]
    public void M137_Pos3_MeleeIntentShownAsWait_HowlStillHowls()
    {
        var s = Fight(31, "shambler", "shambler", "howler");
        var b = s.Battle!;
        var z = Enemy(s, 2);
        var howler = Enemy(s, 3);
        Assert.That(howler.Slot, Is.EqualTo("3"));
        z.Intent = "bite";
        z.IntentLabel = E.IntentLabel(z);
        Assert.That(GameEngine.IntentOutOfReach(z), Is.False, "2号位够得到");
        Assert.That(GameEngine.IntentShown(z), Does.StartWith("咬击"));
        z.Slot = "4";
        Assert.That(GameEngine.IntentOutOfReach(z), Is.True);
        Assert.That(GameEngine.IntentShown(z), Is.EqualTo("前压/等待（在4号位够不到）"));
        z.Slot = "2";
        // 嚎叫尸第1轮（奇数轮）在3号位：意图仍显示嚎叫，并照常嚎叫
        Assert.That(howler.Intent, Is.EqualTo("howl"));
        Assert.That(GameEngine.IntentOutOfReach(howler), Is.False);
        Assert.That(GameEngine.IntentShown(howler), Does.StartWith("嚎叫"));
        var panic = b.Panic;
        for (var i = 0; i < 20 && !howler.Acted && s.Battle != null; i++) Step(s);
        Assert.That(b.Panic, Is.GreaterThan(panic), "3号位嚎叫照常（不是近战攻击）");
        Assert.That(b.Log.Any(x => x.Contains("嚎叫尸嚎叫")), Is.True);
    }

    /// <summary>UX4：本轮已调息且剩余 AP 不足 2 时，调息写“本回合已调整过呼吸”，不写“行动点不足”；撤退同理。</summary>
    [Test]
    public void M137_PerRoundLimit_ReasonBeforeApShortage()
    {
        var s = Fight(32, "worker");
        Ready(s);
        Assert.That(Cmd(s, "breathe").Ok, Is.True);
        Assert.That(s.Battle!.PlayerAp, Is.EqualTo(1));
        Assert.That(E.CommandBlocked(s, BattleCommand.Of("breathe")), Is.EqualTo("本回合已调整过呼吸"));
        s.Battle.RetreatUsed = true;
        Assert.That(E.CommandBlocked(s, BattleCommand.Of("retreat")), Is.EqualTo("本回合已尝试撤退"));
        s.Battle.RetreatUsed = false;
        Assert.That(E.CommandBlocked(s, BattleCommand.Of("retreat")), Does.StartWith("行动点不足"), "没用过时照常报行动点不足");
    }

    /// <summary>UX5：人物页战斗效果汇总标签写清是全身装备合计（攻击类词条只算当前武器页），不再写“本页武器”。</summary>
    [Test]
    public void M137_EquipSummaryLabel_WholeBody()
    {
        var s = New();
        Wear(s, "wooden_shield", EquipSlots.Weapon2);
        Wear(s, "leather_armor", EquipSlots.Armor);
        Wear(s, "steady_ring", EquipSlots.CombatAccessory);
        var line = E.EquipSummaryLines(s)[0];
        Assert.That(line, Does.StartWith("战斗（全身装备合计；攻击类词条只算当前武器页）：").And.Contain("速度−1").And.Contain("护甲0.2").And.Contain("命中能力+3"));
        Assert.That(line, Does.Not.Contain("本页武器"));
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
        var outside = Fight(30, "shambler");
        outside.Battle = null;
        Assert.That(E.CompatibleCommands(outside).Single(o => o.Action == "swap").Reason, Does.StartWith("只有你一人在场"), "系统说明指令一览同源写原因");

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

    // ───────────── M131：命中、伤害、控制、状态（R23；验收 CB05、CB06） ─────────────

    [Test]
    public void R23_HitCritDamage_GearAccuracyCritPenetrationBuffCap()
    {
        var s = Fight(16, "riot");    // 护甲0.7、闪避20
        Ready(s);
        var axe = Wear(s, "hatchet", EquipSlots.Weapon1, "blue", "accurate", "piercing");   // 命中+4、忽略护甲+0.1（攻击类，只算本武器）
        Wear(s, "steady_ring", EquipSlots.CombatAccessory);                                 // 命中+3
        var fx = E.EquipmentEffects(s, GameState.PlayerId, 1);
        Assert.That(fx.Accuracy, Is.EqualTo(7), "校准+4（本页武器）+ 稳手指环+3");
        var t = E.PreviewCommand(s, BattleCommand.Of("basic_attack", 1)).Targets.Single();
        var acc = 24 + 3 * 1 + fx.Accuracy;
        Assert.That(t.AccuracyRating, Is.EqualTo(acc).Within(1e-9), "A = 武器A + 3L + 装备命中");
        Assert.That(t.HitChance, Is.EqualTo(acc / (acc + 20)).Within(1e-12), "P = A/(A+D)");
        Assert.That(t.CritRating, Is.EqualTo(5 + 0.3 + fx.Crit).Within(1e-9), "C = 武器C + 0.3L + 装备暴击");
        Assert.That(t.CritChance, Is.EqualTo(t.CritRating / (t.CritRating + 100)).Within(1e-12));
        Assert.That(t.Penetration, Is.EqualTo(0.1 + 0.1).Within(1e-9), "劈砍0.1 + 破甲词条0.1");
        Assert.That(t.DamageMin, Is.EqualTo(1.6 * 1.035 - (0.7 - 0.2)).Within(1e-9), "扣 max(0,护甲−穿透)");
        // 伤害增益：锐利+0.06；不同来源乘算总上限×1.5
        axe.Affixes = new List<string> { "keen" };
        t = E.PreviewCommand(s, BattleCommand.Of("basic_attack", 1)).Targets.Single();
        Assert.That((t.DamageBuff, t.Multiplier), Is.EqualTo((1.06, 1.035 * 1.06)).Within(1e-9));
        Assert.That(t.DamageMin, Is.EqualTo(1.6 * 1.035 * 1.06 - (0.7 - 0.1)).Within(1e-9));
        Assert.That(Cfg.CombatMath.DamageBuffCap, Is.EqualTo(1.5));
        // 实际结算与预览同源
        var hp0 = Enemy(s, 1).Hp;
        Force(s, Below(0.5), AtLeast(0.2), Below(1e-3));
        Cmd(s, "basic_attack", 1);
        Assert.That(hp0 - Enemy(s, 1).Hp, Is.EqualTo(t.DamageMin).Within(2e-3));
    }

    [Test]
    public void R23_EnemyHit_PlayerDodge36_GearArmor()
    {
        var s = Fight(17, "worker");
        Wear(s, "leather_armor", EquipSlots.Armor);   // 护甲0.2
        Assert.That(E.PlayerArmor(s), Is.EqualTo(0.2).Within(1e-9));
        var e = s.Battle!.Alive.First();
        var hp = s.Stats.Health;
        Force(s, Below(0.01), Below(1e-3));
        typeof(GameEngine).GetMethod("EnemyAttack", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .Invoke(E, new object[] { s, e, Cfg.Enemies.Single(x => x.Id == e.EnemyId), false });
        Assert.That(hp - s.Stats.Health, Is.EqualTo(1.0 - 0.2).Within(1e-3), "敌人伤害扣主角装备护甲");
        Assert.That(GameEngine.HitChance(18, Cfg.Scope.BasePlayerDodge), Is.EqualTo(18.0 / 54).Within(1e-12), "主角闪避沿用36（开发假设 B6-2）");
    }

    [Test]
    public void R23_ControlChance_ClampAndSteady()
    {
        Assert.That(E.ControlChance(0, 0), Is.EqualTo(0.75).Within(1e-12));
        Assert.That(E.ControlChance(50, 0), Is.EqualTo(0.95).Within(1e-12), "上限0.95");
        Assert.That(E.ControlChance(0, 90), Is.EqualTo(0.10).Within(1e-12), "下限0.10");
        Assert.That(E.ControlChance(0, 30), Is.EqualTo(0.45).Within(1e-12), "稳态+30个百分点");

        // 推挡：命中后再判控制；成功给稳态到下一大回合末，同轮再成功不加第二层
        // B7：厚衣工尸控制抗性按 A09 §1 为 20 → 0.75−0.20=55%，稳态中 25%
        // B7：推挡改为拳3级技能 unarmed_03（A06 §2，12→1、1AP）
        var s = Fight(18, "worker");
        Ready(s);
        s.Skills["unarmed"].Level = 3;
        var t = Enemy(s, 1);
        Force(s, Below(0.001), AtLeast(0.999), Any(), Below(0.55));
        Assert.That(Cmd(s, "unarmed_03", 1).Ok, Is.True);
        Assert.That((t.Stagger, t.SteadyUntilRound), Is.EqualTo((true, s.Battle!.Round + 1)));
        Assert.That(s.Battle.Log.Last(), Does.Contain("控制55%"));
        t.Stagger = false;
        Force(s, Below(0.001), AtLeast(0.999), Any(), x => x >= 0.25 && x < 0.55);
        Assert.That(Cmd(s, "unarmed_03", 1).Ok, Is.True);
        Assert.That(t.Stagger, Is.False, "稳态中只有25%");
        Assert.That(s.Battle.Log.Last(), Does.Contain("稳住了").And.Contain("控制25%"));
        Assert.That(t.SteadyUntilRound, Is.EqualTo(s.Battle.Round + 1), "不加第二层、不延长");
    }

    [Test]
    public void R23_EnemySlowOnHit_IsControl_PlayerResist0()
    {
        var s = Fight(19, "crawler");
        var e = s.Battle!.Alive.First();
        Force(s, Below(0.01), Any(), AtLeast(0.75));
        typeof(GameEngine).GetMethod("EnemyAttack", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .Invoke(E, new object[] { s, e, Cfg.Enemies.Single(x => x.Id == e.EnemyId), false });
        Assert.That(s.Battle.PlayerSlowPending, Is.False, "控制判定失败（75%）不减速");
        Force(s, Below(0.01), Any(), Below(0.75));
        typeof(GameEngine).GetMethod("EnemyAttack", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .Invoke(E, new object[] { s, e, Cfg.Enemies.Single(x => x.Id == e.EnemyId), false });
        Assert.That((s.Battle.PlayerSlowPending, s.Battle.PlayerSteadyUntilRound), Is.EqualTo((true, s.Battle.Round + 1)));
    }

    [Test]
    public void R23_Bleed_AtRoundStart_Once_PursuitNoExtra()
    {
        var s = Fight(20, "crawler");   // 4 对 1：每轮都有追击
        var b = s.Battle!;
        s.Body.BleedTriggers = 3;
        s.Body.BleedDamage = 0.4;
        var h0 = s.Stats.Health;
        Ready(s);
        Assert.That(s.Body.BleedTriggers, Is.EqualTo(3), "激活开始不再扣出血");
        Cmd(s, "end_turn");
        for (var i = 0; i < 20 && b.Round == 1; i++)
        {
            if (b.Cursor < b.Queue.Count && b.Queue[b.Cursor] > 0) { b.Cursor++; continue; }   // 跳过敌方行动
            Step(s);
        }
        Assert.That(b.Round, Is.EqualTo(2));
        Assert.That(s.Body.BleedTriggers, Is.EqualTo(2), "一个大回合只扣一次（追击不多扣）");
        Assert.That(h0 - s.Stats.Health, Is.EqualTo(0.4).Within(1e-9));
        Assert.That(b.Log.Any(x => x.StartsWith("第2轮") && x.Contains("你流血")), Is.True, "在第2轮开始扣");
    }

    [Test]
    public void R23_EnemyBleedKillsLastAtRoundStart_VictoryWithoutExtraTick()
    {
        var s = Fight(21, "feral_dog");
        var b = s.Battle!;
        var dog = Enemy(s, 1);
        dog.Hp = 0.3;
        dog.BleedTriggers = 2;
        dog.BleedDamage = 0.4;
        Ready(s);
        Cmd(s, "end_turn");
        var minute = s.GameMinute;
        for (var i = 0; i < 20 && s.Battle != null; i++)
        {
            if (b.Cursor < b.Queue.Count && b.Queue[b.Cursor] > 0) { b.Cursor++; continue; }
            Step(s);
        }
        Assert.That(s.Battle, Is.Null, "第2轮开始流血倒下即胜利");
        Assert.That(s.GameMinute, Is.EqualTo(minute + 5), "只有第1轮那一次5分钟，不为没有行动的第2轮再推进");
        Assert.That(b.Ticks, Is.EqualTo(1));
    }

    [Test]
    public void CB05_Protection_NoSelfNoMutualNoLoop_OneTargetEach()
    {
        var links = new Dictionary<string, string>();
        Assert.That(GameEngine.ProtectBlocked(links, "a", "a"), Is.EqualTo("不能保护自己"));
        Assert.That(GameEngine.ProtectBlocked(links, "a", "b"), Is.Null);
        links["a"] = "b";
        Assert.That(GameEngine.ProtectBlocked(links, "b", "a"), Is.EqualTo("不能相互或循环保护"), "不允许相互保护");
        Assert.That(GameEngine.ProtectBlocked(links, "a", "c"), Is.EqualTo("每名保护者只能保护一名目标"));
        Assert.That(GameEngine.ProtectBlocked(links, "c", "b"), Is.EqualTo("目标已有保护者"));
        Assert.That(GameEngine.ProtectBlocked(links, "b", "c"), Is.Null, "链条不成环可以");
        links["b"] = "c";
        Assert.That(GameEngine.ProtectBlocked(links, "c", "a"), Is.EqualTo("不能相互或循环保护"), "a→b→c→a 成环");
        Assert.That(new BattleState().Protections, Is.Empty, "本批战斗里没有保护（保护技能在 B7、队友在 B8）");
    }

    [Test]
    public void CB06_TwoWeapons_CostAndAffixesBelongToOwnPage()
    {
        var s = Fight(22, "shambler");
        var knife = Wear(s, "stone_knife", EquipSlots.Weapon1, "green", "balanced");   // 平衡：本武器 E×0.92
        Wear(s, "wooden_spear", EquipSlots.Weapon2);
        Ready(s);
        var basic = Cfg.CombatActions.Single(a => a.Id == "basic_attack");
        Assert.That(E.AttackEnergy(s, basic, Cfg.Weapons.Single(w => w.Id == "stone_knife"), 1), Is.EqualTo(E.SkillEnergyCost(0.4, 1) * 0.92).Within(1e-9));
        Assert.That(E.AttackEnergy(s, basic, Cfg.Weapons.Single(w => w.Id == "wooden_spear"), 2), Is.EqualTo(E.SkillEnergyCost(0.5, 1)).Within(1e-9), "词条不借给另一把");
        var e0 = s.Stats.Energy;
        E.SelectWeaponPage(s, 2);
        Force(s, AtLeast(0.999));
        Cmd(s, "basic_attack", 1);
        Assert.That(e0 - s.Stats.Energy, Is.EqualTo(E.SkillEnergyCost(0.5, 1)).Within(1e-9), "切到矛页攻击付矛的成本");
        Assert.That(knife.Durability, Is.EqualTo(knife.Durability), "刀不磨损");
    }

    // ───────────── M132：战斗中的时间与食水（R24；验收 CB07—CB09、CB12、RG03、COMP04） ─────────────

    [Test]
    public void CB07_BattleFreezesNegativeNW_IncludingDiarrhea_NoCatchUpAfter()
    {
        var s = Fight(23, "stag");
        SetStats(s, 20, 50, 50, 18, 90, 60);
        s.DiarrheaUntil = s.GameMinute + 600;
        var minute = s.GameMinute;
        var heat = s.Stats.Wakefulness;
        var fight = s.Battle!;
        for (var round = 0; round < 3 && s.Battle != null; round++)
        {
            if (!E.AwaitingPlayer(s)) ToPlayer(s);
            if (s.Battle == null) break;
            Cmd(s, "guard");
            while (s.Battle != null && fight.Round == round + 1) Step(s);
        }
        Assert.That(s.GameMinute, Is.EqualTo(minute + 15), "三个大回合各一次5分钟");
        Assert.That((s.Stats.Satiety, s.Stats.Hydration), Is.EqualTo((50.0, 50.0)), "战内 N/W 负变化（含腹泻扣水）都为0");
        Assert.That(s.Stats.Wakefulness, Is.EqualTo(heat - 5 * 15 / 60.0).Within(1e-9), "S 仍按战斗率−5/h");
        // 撤退脱战后不补扣：之后的普通时间才按正常速率扣
        s.Battle!.PlayerActing = true;
        s.Battle.PlayerAp = 3;
        Force(s, Below(0.0001));
        Assert.That(Cmd(s, "retreat").Ok, Is.True);
        Assert.That(s.Battle, Is.Null);
        Assert.That((s.Stats.Satiety, s.Stats.Hydration), Is.EqualTo((50.0, 50.0)), "脱战那一段也是战斗段，战后不补扣");
    }

    [Test]
    public void CB08_ZeroHydrationHealthRiskKeptInBattle_CB09_PlayerDeathFirst()
    {
        var s = Fight(24, "shambler");
        Ready(s);
        SetStats(s, 0.1, 50, 0, 18, 90, 60);
        var e = Enemy(s, 1);
        e.Hp = 0.01;
        Force(s, Below(0.001), AtLeast(0.999), Any(), Any());
        Cmd(s, "basic_attack", 1);
        Assert.That(s.Progress.Dead, Is.True, "水分0的健康伤害照旧，轮末 tick 致死");
        Assert.That(s.Battle, Is.Null);
        Assert.That(s.Items.Any(i => i.Owner == "field" && i.ItemId is "cloth" or "scrap"), Is.False, "同指令杀最后一敌也不救回、不发掉落");
    }

    [Test]
    public void RG03_COMP04_PartyTickOncePerRound_NotMultiplied()
    {
        var s = Fight(25, "stag");
        var mate = s.Actor("c1");
        mate.Stats.Satiety = 60;
        mate.Stats.Hydration = 60;
        mate.Stats.Wakefulness = 90;
        mate.Stats.Health = 20;
        mate.Stats.Energy = 18;
        s.Party.Add("c1");
        var minute = s.GameMinute;
        Ready(s);
        Cmd(s, "guard");
        while (s.Battle != null && s.Battle.Round == 1) Step(s);
        Assert.That(s.GameMinute, Is.EqualTo(minute + 5), "两名角色共用同一个5分钟，不按人数倍乘");
        Assert.That((mate.Stats.Satiety, mate.Stats.Hydration), Is.EqualTo((60.0, 60.0)), "同行者同样冻结 N/W");
        Assert.That(mate.Stats.Wakefulness, Is.EqualTo(90 - 5 * 5 / 60.0).Within(1e-9), "各自结算一次身体");
    }

    [Test]
    public void CB12_SaveLoad_RestoresPursuitTieSteadyAp_NoReroll()
    {
        var s = Fight(26, "shambler", "crawler");
        s.Player.TacticalTier = 1;
        Ready(s);
        var b = s.Battle!;
        Enemy(s, 1).SteadyUntilRound = 2;
        var json = SaveSerializer.Serialize(s);
        var t = SaveSerializer.Deserialize(json, Cfg);
        var tb = t.Battle!;
        Assert.That((tb.PlayerPursuit, tb.NormalCount, tb.PlayerAp, tb.ActivationAp, tb.PlayerTieRoll), Is.EqualTo((b.PlayerPursuit, b.NormalCount, b.PlayerAp, b.ActivationAp, b.PlayerTieRoll)));
        Assert.That(tb.Queue, Is.EqualTo(b.Queue));
        Assert.That(tb.Enemies.Select(e => (e.PursuitProgress, e.TieRoll, e.SteadyUntilRound)), Is.EqualTo(b.Enemies.Select(e => (e.PursuitProgress, e.TieRoll, e.SteadyUntilRound))));
        // 读档后接着打，与不读档完全同一序列
        Cmd(s, "end_turn");
        Cmd(t, "end_turn");
        for (var i = 0; i < 6; i++) { Step(s); Step(t); }
        Assert.That(SaveSerializer.Serialize(t), Is.EqualTo(SaveSerializer.Serialize(s)));
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
