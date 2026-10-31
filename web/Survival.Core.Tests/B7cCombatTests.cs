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

    // ───────────── M165：人物基础防御与位移抵抗限域（Q63） ─────────────

    private ItemInstance Wear(GameState s, string itemId, string slot, params string[] affixes)
    {
        var it = E.DebugCreateEquipment(s, itemId, "white", ItemInstance.EquipOwner(GameState.PlayerId, slot));
        it.Affixes = affixes.Length > 0 ? affixes.ToList() : null;
        return it;
    }

    /// <summary>CB-Q63a：24 名固定人物档、随机区间与主角档对合同；主角 5/36/0 同步到 scope（队友与随机人物 B8 才接入）。</summary>
    [Test]
    public void CB_Q63a_Profiles_MatchContract_PlayerFiveThirtySixZero()
    {
        var d = Adj()["actorBaseDefense"]!;
        var fixedRows = (JArray)d["fixedProfiles"]!;
        Assert.That(Cfg.ActorBaseDefense.FixedProfiles, Has.Count.EqualTo(24));
        foreach (var r in fixedRows)
        {
            var p = Cfg.ActorBaseDefense.Profile((string)r["actorId"]!)!;
            Assert.That((p.ControlResist, p.Evasion, p.BaseArmor, p.BaseSpeed, p.Role), Is.EqualTo(((double)r["controlResist"]!, (double)r["evasion"]!, (double)r["baseArmor"]!, (double)r["baseSpeed"]!, (string)r["role"]!)));
        }
        Assert.That((Cfg.Scope.BasePlayerControlResist, Cfg.Scope.BasePlayerDodge, Cfg.Scope.BasePlayerArmor, Cfg.Scope.BasePlayerSpeed), Is.EqualTo((5.0, 36.0, 0.0, 4.0)));
        var rr = Cfg.ActorBaseDefense.RandomRecruit!;
        Assert.That(((double)rr["controlResist"]!["min"]!, (double)rr["controlResist"]!["max"]!, (double)rr["controlResist"]!["step"]!), Is.EqualTo((0.0, 10.0, 0.5)));
        Assert.That(((double)rr["evasion"]!["min"]!, (double)rr["evasion"]!["max"]!, (double)rr["evasion"]!["step"]!), Is.EqualTo((34.0, 38.0, 0.5)));
        Assert.That(((double)rr["baseSpeed"]!["min"]!, (double)rr["baseSpeed"]!["max"]!, (double)rr["baseSpeed"]!["step"]!), Is.EqualTo((3.0, 5.0, 0.1)));
        Assert.That(E.BaseDefenseLine(), Does.Contain("控制抗性5").And.Contain("闪避36").And.Contain("护甲0"));
    }

    /// <summary>CB-Q63c：位移抵抗只防拉扯/击退；不同来源相加封顶 40、同名取强，再与主动稳固取强；稳态另 +30；最终 10%—95%。</summary>
    [Test]
    public void CB_Q63c_DisplacementGear_OnlyPullKnockback_Cap40_StrongestWithActive()
    {
        var s = Fight(7, "shambler");
        var b = s.Battle!;
        Assert.That(E.PlayerControlChance(s, b, "stagger"), Is.EqualTo(0.70).Within(1e-12), "基础抗性5：75%−5%");
        Wear(s, "heavy_armor", EquipSlots.Armor);
        Assert.That(E.PlayerControlResist(s, b, "pull"), Is.EqualTo(5 + 15));
        Assert.That(E.PlayerControlResist(s, b, "stagger"), Is.EqualTo(5), "位移装备不防失衡");
        Assert.That(E.PlayerControlResist(s, b, "slow"), Is.EqualTo(5), "位移装备不防减速");
        Wear(s, "riot_shield", EquipSlots.Weapon1);
        Assert.That(E.PlayerControlResist(s, b, "knockback"), Is.EqualTo(5 + 40), "15+25=40 正好封顶");
        Wear(s, "steady_ring", EquipSlots.CombatAccessory, "anchored");
        var fx = E.EquipmentEffects(s);
        Assert.That((fx.DisplaceResistRaw, fx.DisplaceResist), Is.EqualTo((50.0, 40.0)), "15+25+10 合计 50，封顶 40");
        Assert.That(E.PlayerControlResist(s, b, "pull"), Is.EqualTo(45));
        Assert.That(E.EquipSummaryLines(s)[0], Does.Contain("位移抵抗+40个百分点（只防拉扯/击退，合计封顶40）"));
        // 两面同款防暴盾：同名固定特性取强，只算一面
        var t = Fight(8, "shambler");
        Wear(t, "riot_shield", EquipSlots.Weapon1);
        Wear(t, "riot_shield", EquipSlots.Weapon2);
        Assert.That(E.EquipmentEffects(t).DisplaceResist, Is.EqualTo(25));
        // 主动稳固（盾技能状态 25）与装备 15 取强，不相加
        var u = Fight(9, "shambler");
        var ub = u.Battle!;
        Wear(u, "heavy_armor", EquipSlots.Armor);
        ub.DisplaceResist = 25;
        ub.DisplaceUntilRound = ub.Round;
        Assert.That(E.PlayerControlResist(u, ub, "pull"), Is.EqualTo(5 + 25));
        ub.DisplaceUntilRound = ub.Round - 1;
        Assert.That(E.PlayerControlResist(u, ub, "pull"), Is.EqualTo(5 + 15), "稳固过期只剩装备");
    }

    /// <summary>CB-Q63c/Q70 文档样例：控制能力0对主角：普通70%、稳态40%；位移抵抗25时拉扯/击退45%、稳态15%，失衡/减速仍70%/40%；下限10%。</summary>
    [Test]
    public void CB_Q63c_DocExample_SeventyFortyFortyFiveFifteen()
    {
        var s = Fight(10, "shambler");
        var b = s.Battle!;
        Wear(s, "riot_shield", EquipSlots.Weapon1);
        Assert.That(E.PlayerControlChance(s, b, "stagger"), Is.EqualTo(0.70).Within(1e-12));
        Assert.That(E.PlayerControlChance(s, b, "pull"), Is.EqualTo(0.45).Within(1e-12));
        b.PlayerSteadyUntilRound = b.Round;
        Assert.That(E.PlayerControlChance(s, b, "slow"), Is.EqualTo(0.40).Within(1e-12));
        Assert.That(E.PlayerControlChance(s, b, "knockback"), Is.EqualTo(0.15).Within(1e-12));
        Wear(s, "heavy_armor", EquipSlots.Armor);
        Assert.That(E.PlayerControlChance(s, b, "pull"), Is.EqualTo(0.10).Within(1e-12), "5+40+30 → 下限10%");
    }

    // ───────────── M166：命中 +8 与单次疲劳 ×0.95（Q67） ─────────────

    private void Ready(GameState s)
    {
        PlayerFirst(s);
        E.BattleStep(s);
        Assert.That(E.AwaitingPlayer(s), Is.True);
    }

    private BattleTargetPreview KnifeOnShambler(GameState s, double energyAfter, double wake, double panic, bool stagger)
    {
        var cost = E.PreviewCommand(s, BattleCommand.Of("basic_attack", 1)).EnergyCost;
        s.Stats.Energy = energyAfter + cost;
        s.Stats.Wakefulness = wake;
        s.Battle!.Panic = panic;
        s.Battle.PlayerStagger = stagger;
        return E.PreviewCommand(s, BattleCommand.Of("basic_attack", 1)).Targets.Single();
    }

    /// <summary>CB-Q67 文档样例：1级石刀对游荡尸 正常53.7%→59.7%、双低45.6%→58.4%、双低+P90+失衡37.6%→50.3%。</summary>
    [Test]
    public void CB_Q67_DocSamples_StoneKnifeVsShambler()
    {
        var a = Adj()["accuracy"]!;
        Assert.That(((double)a["baseAssistance"]!, (double)a["newLowEnergyOrWakefulnessFactor"]!), Is.EqualTo((Cfg.CombatMath.PlayerAccuracyAssistance, Cfg.CombatMath.FatigueAccuracyFactor)));
        var s = Fight(11, "shambler");
        Ready(s);
        Arm(s, "stone_knife");
        Assert.That(E.SkillLevel(s, "blade"), Is.EqualTo(1));
        var normal = KnifeOnShambler(s, 18, 90, 20, false);
        Assert.That(normal.AccuracyRating, Is.EqualTo(26 + 3 + 8).Within(1e-9));
        Assert.That(Math.Round(normal.HitChance * 100, 1), Is.EqualTo(59.7));
        var both = KnifeOnShambler(s, 3, 20, 20, false);
        Assert.That(both.AccuracyRating, Is.EqualTo(37 * 0.95).Within(1e-9), "双低只乘一次×0.95");
        Assert.That(Math.Round(both.HitChance * 100, 1), Is.EqualTo(58.4));
        var worst = KnifeOnShambler(s, 3, 20, 90, true);
        Assert.That(worst.AccuracyRating, Is.EqualTo(37 * 0.95 * 0.8 * 0.9).Within(1e-9));
        Assert.That(Math.Round(worst.HitChance * 100, 1), Is.EqualTo(50.3));
        // 旧口径对照（无+8、两条×0.85）：53.7% / 45.6% / 37.6%
        Assert.That(Math.Round(GameEngine.HitChance(29, 25) * 100, 1), Is.EqualTo(53.7));
        Assert.That(Math.Round(GameEngine.HitChance(29 * 0.85 * 0.85, 25) * 100, 1), Is.EqualTo(45.6));
        Assert.That(Math.Round(GameEngine.HitChance(29 * 0.85 * 0.85 * 0.8 * 0.9, 25) * 100, 1), Is.EqualTo(37.6));
    }

    /// <summary>CB-Q67 边界：支付后 E=4 不罚、3.99 罚；S=20 罚、20.01 不罚；单低与双低同为×0.95；恐慌60×0.9；敌人对你的命中不加 8。</summary>
    [Test]
    public void CB_Q67_Boundaries_SingleFatigue_EnemyNoAssistance()
    {
        var s = Fight(12, "shambler");
        Ready(s);
        Arm(s, "stone_knife");
        Assert.That(KnifeOnShambler(s, 4, 90, 0, false).AccuracyRating, Is.EqualTo(37).Within(1e-9), "支付后 E=4 不算低体力");
        Assert.That(KnifeOnShambler(s, 3.99, 90, 0, false).AccuracyRating, Is.EqualTo(37 * 0.95).Within(1e-9));
        Assert.That(KnifeOnShambler(s, 18, 20, 0, false).AccuracyRating, Is.EqualTo(37 * 0.95).Within(1e-9), "S≤20");
        Assert.That(KnifeOnShambler(s, 18, 20.01, 0, false).AccuracyRating, Is.EqualTo(37).Within(1e-9));
        Assert.That(KnifeOnShambler(s, 18, 90, 60, false).AccuracyRating, Is.EqualTo(37 * 0.9).Within(1e-9), "恐慌≥60×0.9");
        var why = new List<string>();
        E.BodyAccuracyFactor(s, s.Battle!, 3, why);
        Assert.That(why, Has.Some.Contains("只算一次"));
        // 应急拳同样吃 +8（我方攻击能力）：(24+3+8)×0.65
        var dp = (24 + 3 + 8) * Cfg.CombatActions.Single(x => x.Id == "desperate_punch").AccuracyRatingFactor;
        s.Stats.Energy = 0;
        s.Stats.Wakefulness = 90;
        s.Battle!.Panic = 0;
        Assert.That(E.PreviewCommand(s, BattleCommand.Of("desperate_punch", 1)).Targets.Single().AccuracyRating, Is.EqualTo(dp * 0.95).Within(1e-9), "应急拳：+8、招式×0.65、体力0×0.95");
        // 战斗外估算与战斗中同源
        var t = Field(13);
        var shambler = Cfg.Enemies.Single(x => x.Id == "shambler");
        Assert.That(E.OutOfBattleHit(t, shambler).Accuracy, Is.EqualTo(24 + 3 + 8).Within(1e-9), "空手开战页：拳 24+3+8");
        Assert.That(GameEngine.HitChance(shambler.AttackRating, Cfg.Scope.BasePlayerDodge), Is.EqualTo(20.0 / 56).Within(1e-12), "敌人命中不加 8");
    }

    // ───────────── M167：拳只打 1 号位、10 级连拳 12、护拳绑带（Q64） ─────────────

    private static ActionRequest CraftWraps() => new ActionRequest { Type = ActionType.Craft, TargetId = "make_knuckle_wraps" };

    /// <summary>CB-Q64a：基础拳、应急拳、推挡只打 1 号位，10 级连拳 12；预览、指令可选目标与执行同源。</summary>
    [Test]
    public void CB_Q64a_PunchOnlyPos1_ComboPos12()
    {
        var a = Adj()["unarmed"]!;
        Assert.That(a["ordinaryDefaultTarget"]!.Select(x => (int)x).ToArray(), Is.EqualTo(new[] { 1 }));
        var s = Fight(21, "shambler", "shambler");
        Ready(s);
        Assert.That(E.CurrentWeapon(s, out var wi).IsFists && wi == null, Is.True, "两槽都空：拳页");
        var basic = E.BattleOptions(s).Single(o => o.Action == "basic_attack");
        Assert.That(basic.Targets, Is.EqualTo(new[] { 1 }), "普通拳击只亮 1 号位");
        Assert.That(E.CommandBlocked(s, BattleCommand.Of("basic_attack", 2)), Does.Contain("射程不足").And.Contain("只能打1号位"));
        Assert.That(Cmd(s, "basic_attack", 2).Ok, Is.False, "执行同样拒绝");
        s.Stats.Energy = 0;
        Assert.That(E.CommandBlocked(s, BattleCommand.Of("desperate_punch", 2)), Does.Contain("射程不足"), "应急拳只打1");
        Assert.That(E.CommandBlocked(s, BattleCommand.Of("desperate_punch", 1)), Is.Null);
        s.Stats.Energy = 18;
        s.Skills["unarmed"].Level = 10;
        Assert.That(E.CommandBlocked(s, BattleCommand.Of("unarmed_03", 2)), Does.Contain("射程不足"), "推挡只打1");
        Assert.That(E.CommandBlocked(s, BattleCommand.Of("unarmed_10", 2)), Is.Null, "10级连拳可打2号位");
        Assert.That(E.SkillNode("unarmed_10")!.Ap, Is.EqualTo(3), "Q64 改射程不改 AP");
        Assert.That(E.SkillNode("unarmed_01")!.Ap, Is.EqualTo(2));
        Assert.That(E.PreviewCommand(s, BattleCommand.Of("unarmed_10", 2)).Targets.Single().Index, Is.EqualTo(2));
    }

    /// <summary>CB-Q64b：护拳绑带只让“当前空槽徒手页”的普通拳击打 12；武器页、应急拳、推挡不变；占战斗饰品槽、200g。</summary>
    [Test]
    public void CB_Q64b_Wraps_OnlyEmptySlotBasicPunch()
    {
        var s = Fight(22, "shambler", "shambler");
        Ready(s);
        var wraps = Wear(s, "knuckle_wraps", EquipSlots.CombatAccessory);
        Assert.That(Cfg.Items.Single(i => i.Id == "knuckle_wraps").WeightGrams, Is.EqualTo(200));
        Assert.That(E.BattleOptions(s).Single(o => o.Action == "basic_attack").Targets, Is.EquivalentTo(new[] { 1, 2 }), "空槽拳页 + 绑带：普通拳击 12");
        var before = E.PreviewCommand(s, BattleCommand.Of("basic_attack", 2)).Targets.Single();
        Assert.That(Cmd(s, "basic_attack", 2).Ok, Is.True, "执行与预览一致");
        s.Battle!.PlayerAp = 3;
        // 不加伤害/命中：与 1 号位同一攻击能力、伤害区间
        var on1 = E.PreviewCommand(s, BattleCommand.Of("basic_attack", 1)).Targets.Single();
        Assert.That((on1.AccuracyRating, on1.DamageMin, on1.DamageMax), Is.EqualTo((before.AccuracyRating, before.DamageMin, before.DamageMax)));
        Assert.That(on1.AccuracyRating, Is.EqualTo(24 + 3 + 8).Within(1e-9), "绑带不加命中");
        s.Stats.Energy = 0;
        Assert.That(E.CommandBlocked(s, BattleCommand.Of("desperate_punch", 2)), Does.Contain("射程不足"), "应急拳仍只打1");
        s.Stats.Energy = 18;
        s.Skills["unarmed"].Level = 3;
        Assert.That(E.CommandBlocked(s, BattleCommand.Of("unarmed_03", 2)), Does.Contain("射程不足"), "推挡仍只打1");
        // 当前页是武器（坏的也算占槽）：不是拳页，绑带无关
        var axe = Arm(s, "hatchet");
        Assert.That(E.CurrentWeapon(s, out _).IsFists, Is.False);
        Assert.That(E.CommandBlocked(s, BattleCommand.Of("basic_attack", 2)), Does.Contain("射程不足"), "斧只打1，绑带不改武器页");
        axe.Durability = 0;
        Assert.That(E.CurrentWeapon(s, out _).IsFists, Is.False, "坏武器仍占槽，不变拳");
        // 切到空的武器Ⅱ页：又是空槽徒手页，绑带生效
        Assert.That(E.SelectWeaponPage(s, 2).Ok, Is.True);
        Assert.That(E.CommandBlocked(s, BattleCommand.Of("basic_attack", 2)), Is.Null);
        // 两槽都满：没有拳页
        var t = Fight(23, "shambler", "shambler");
        Ready(t);
        Wear(t, "knuckle_wraps", EquipSlots.CombatAccessory);
        Wear(t, "hatchet", EquipSlots.Weapon1);
        Wear(t, "wooden_shield", EquipSlots.Weapon2);
        Assert.That(E.SelectWeaponPage(t, 2).Ok, Is.True);
        Assert.That(E.CurrentWeapon(t, out _).IsFists, Is.False, "双槽满不另开拳页");
        // 占战斗饰品槽：与稳手指环互斥（同一槽）
        Assert.That(E.WornItem(s, GameState.PlayerId, EquipSlots.CombatAccessory)!.InstanceId, Is.EqualTo(wraps.InstanceId));
        Assert.That(E.EquipSummaryLines(s)[0], Does.Contain("空槽徒手页的普通拳击可打敌方1、2号位"));
        Assert.That(E.EquipDetailLines(s, wraps).Single(l => l.Label == "固定效果").Value, Does.Contain("（生效中）"));
    }

    /// <summary>CB-Q64c：73 装备/38 武器/75 节点；做法是营区登记默认知识（不赠实物），制造3级、45 基础分钟、布2+皮革1+纤维2；拆解布1、制造10级专家+布1（≤80%预算）。</summary>
    [Test]
    public void CB_Q64c_Catalog_RecipeKnowledge_Salvage()
    {
        Assert.That(Cfg.Weapons.Count(w => !w.IsFists) + Cfg.Gear.Count, Is.EqualTo(73));
        Assert.That(Cfg.Weapons.Count(w => !w.IsFists), Is.EqualTo(38));
        Assert.That(Cfg.SkillNodes, Has.Count.EqualTo(75));
        var ng = Adj()["unarmed"]!["newGear"]!;
        var g = Cfg.Gear.Single(x => x.Id == "knuckle_wraps");
        Assert.That((g.Slot, g.Tier, g.WeightGrams, g.DurabilityMaxRange), Is.EqualTo(("combat_acc", (int)ng["tier"]!, (int)ng["weight_g"]!, (double[]?)null)));
        Assert.That(g.Effects, Is.Empty, "不加伤害/命中");
        var rc = E.Recipe("make_knuckle_wraps");
        Assert.That((rc.BaseMinutes, rc.MinSkills["crafting"], rc.Outputs["knuckle_wraps"]), Is.EqualTo(((int)ng["minutes"]!, (int)ng["craft_level"]!, 1)));
        Assert.That(rc.Inputs, Is.EqualTo(new Dictionary<string, int> { ["cloth"] = 2, ["leather"] = 1, ["fiber"] = 2 }));
        var s = New(24);
        s.Skills["crafting"].Level = 3;
        E.DebugGiveCarriedOrGround(s, rc.Inputs.Select(kv => (kv.Key, kv.Value)));
        Assert.That(E.CanStart(s, CraftWraps()), Does.Contain("还不知道护拳绑带的做法").And.Contain("营区登记"), "未登记不会做");
        Assert.That(E.RecipeKnown(s, rc), Is.False);
        var items0 = s.Items.Count(i => i.ItemId == "knuckle_wraps");
        E.DebugCapacityFact(s, "hub_registered");
        Assert.That(E.RecipeKnown(s, rc), Is.True, "登记即有默认知识");
        Assert.That(s.Items.Count(i => i.ItemId == "knuckle_wraps"), Is.EqualTo(items0), "知识不赠实物");
        s.Skills["crafting"].Level = 2;
        Assert.That(E.CanStart(s, CraftWraps()), Does.Contain("需要制造3级"));
        s.Skills["crafting"].Level = 3;
        var p = E.Preview(s, CraftWraps());
        Assert.That(p.Ok, Is.True, p.Reason);
        Assert.That(p.BaseMinutes, Is.EqualTo(45));
        Start(s, CraftWraps());
        Run(s);
        var made = s.Items.Single(i => i.ItemId == "knuckle_wraps");
        Assert.That(made.Durability, Is.Null, "无耐久");
        // 拆解：制造3级可拆 T1，布1；制造10级回收专家再+布1（总价值4≤8）
        Assert.That(E.SalvageYield(s, made), Is.EqualTo(new List<(string, int)> { ("cloth", 1) }));
        s.Skills["crafting"].Level = 10;
        Assert.That(E.SalvageYield(s, made), Is.EqualTo(new List<(string, int)> { ("cloth", 2) }));
        s.Skills["crafting"].Level = 2;
        Assert.That(E.SalvageBlocked(s, made.InstanceId, out _), Does.Contain("需要制造3级"));
    }

    // ───────────── M168：当前盾页格挡（Q69 选 B） ─────────────

    /// <summary>CB-Q69：当前页盾 0.4 并磨 1；备用页盾 0.5 不磨；0.9 耐久按 0.5；1 耐久可提交、磨到 0 仍保留本次 0.4；提交后切页/读档不改；触发不再磨。</summary>
    [Test]
    public void CB_Q69_CurrentPageShieldOnly_SnapshotAtCommand()
    {
        var sg = Adj()["shieldGuard"]!;
        Assert.That(((double)sg["factor"]!, (double)sg["otherwiseFactor"]!), Is.EqualTo((Cfg.CombatMath.ShieldGuardFactor, Cfg.CombatMath.GuardFactor)));
        // 备用页盾：通用 0.5，不磨备用盾
        var s = Fight(31, "worker");
        var shield = Wear(s, "wooden_shield", EquipSlots.Weapon2);
        var d0 = shield.Durability!.Value;
        Ready(s);
        Assert.That(s.Battle!.ActiveWeaponSlot, Is.EqualTo(1));
        Assert.That(Cmd(s, "guard").Ok, Is.True);
        Assert.That((s.Battle.PlayerGuardFactor, s.Battle.PlayerGuardSourceId), Is.EqualTo((0.5, (int?)null)));
        Assert.That(shield.Durability, Is.EqualTo(d0), "不磨备用盾");
        Assert.That(s.Battle.Log.Last(), Does.Contain("木盾在另一页"));
        // 切到盾页后才防御：0.4、磨 1、锁来源；之后切回拳页、存读档都不变
        var t = Fight(32, "worker");
        var sh = Wear(t, "wooden_shield", EquipSlots.Weapon2);
        Ready(t);
        Assert.That(E.SelectWeaponPage(t, 2).Ok, Is.True);
        var d1 = sh.Durability!.Value;
        Assert.That(Cmd(t, "guard").Ok, Is.True);
        Assert.That((t.Battle!.PlayerGuardFactor, t.Battle.PlayerGuardSourceId), Is.EqualTo((0.4, (int?)sh.InstanceId)));
        Assert.That(sh.Durability, Is.EqualTo(d1 - 1).Within(1e-9));
        Assert.That(E.SelectWeaponPage(t, 1).Ok, Is.True);
        var u = SaveSerializer.Deserialize(SaveSerializer.Serialize(t), Cfg);
        Assert.That((u.Battle!.PlayerGuardFactor, u.Battle.PlayerGuardSourceId), Is.EqualTo((0.4, (int?)sh.InstanceId)), "切页、读档不改已授格挡");
        // 0.9 耐久付不起 1：普通 0.5
        var v = Fight(33, "worker");
        var weak = Wear(v, "wooden_shield", EquipSlots.Weapon1);
        weak.Durability = 0.9;
        Ready(v);
        v.Battle!.ActiveWeaponSlot = 1;
        Assert.That(E.PreviewCommand(v, BattleCommand.Of("guard")).Notes.Single(), Does.Contain("耐久不足1"));
        Assert.That(Cmd(v, "guard").Ok, Is.True);
        Assert.That(v.Battle.PlayerGuardFactor, Is.EqualTo(0.5));
        Assert.That(weak.Durability, Is.EqualTo(0.9).Within(1e-9));
        // 正好 1 耐久：可提交，磨到 0 仍保留本次 0.4；命中触发不再磨
        var w = Fight(34, "worker");
        var one = Wear(w, "wooden_shield", EquipSlots.Weapon1);
        one.Durability = 1;
        Ready(w);
        w.Battle!.ActiveWeaponSlot = 1;
        Assert.That(Cmd(w, "guard").Ok, Is.True);
        Assert.That((w.Battle.PlayerGuardFactor, one.Durability), Is.EqualTo((0.4, (double?)0.0)));
        var e = w.Battle.Alive.First();
        var hp = w.Stats.Health;
        Force(w, Draw(), Below(0.01), Below(0.0001));
        typeof(GameEngine).GetMethod("EnemyAttack", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .Invoke(E, new object[] { w, e, Cfg.Enemies.Single(x => x.Id == e.EnemyId), false });
        Assert.That(hp - w.Stats.Health, Is.EqualTo(Cfg.Enemies.Single(x => x.Id == "worker").DamageMin * 0.4).Within(1e-3), "已授 0.4 不因盾归零回收");
        Assert.That(one.Durability, Is.EqualTo(0.0));
    }

    /// <summary>CB-Q69：格挡类技能同口径——拳页“护身”时另一页的盾不算（0.5、不磨盾）。</summary>
    [Test]
    public void CB_Q69_GuardSkill_OnFistPage_IgnoresOtherPageShield()
    {
        var s = Fight(35, "worker");
        var shield = Wear(s, "wooden_shield", EquipSlots.Weapon2);
        var d0 = shield.Durability!.Value;
        s.Skills["unarmed"].Level = 5;
        Ready(s);
        Assert.That(E.PreviewCommand(s, BattleCommand.Of("unarmed_05")).Notes.Any(n => n.Contains("木盾在另一页")), Is.True);
        Assert.That(Cmd(s, "unarmed_05").Ok, Is.True);
        Assert.That((s.Battle!.PlayerGuardFactor, s.Battle.PlayerGuardSourceId, shield.Durability), Is.EqualTo((0.5, (int?)null, (double?)d0)));
    }

    // ───────────── M169：小数速度（Q71） ─────────────

    /// <summary>另起一个引擎：主角基础速度与游荡尸面板速度改成小数（现有数据都是整数，小数来源在 B8 随机人物）。</summary>
    private void DecimalSpeeds(double player, double shambler)
    {
        var cfg = ConfigLoader.FromJson(File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "config.v0.6.json")));
        cfg.Scope.BasePlayerSpeed = player;
        cfg.Enemies.Single(x => x.Id == "shambler").Speed = shambler;
        E = new GameEngine(cfg);
    }

    private void BeginRound(GameState s) =>
        typeof(GameEngine).GetMethod("BeginRound", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(E, new object[] { s });

    /// <summary>CB-Q71a：4.01 与 4.02 按小数排；真同速 4.0 按锁定 u 降序、再 worldActorId 升序，u 存档不重抽。</summary>
    [Test]
    public void CB_Q71a_DecimalOrder_TrueTieByLockedU()
    {
        var np = Adj()["numericPrecision"]!;
        Assert.That(((double)np["speedStep"]!, (double)np["pursuitProgressStoredPrecision"]!), Is.EqualTo((0.01, 0.0001)));
        DecimalSpeeds(4.01, 4.02);
        var s = Fight(41, "shambler");
        var b = s.Battle!;
        Assert.That((E.PlayerEffectiveSpeed(s, b), E.EnemyEffectiveSpeed(b.Enemies[0])), Is.EqualTo((4.01, 4.02)));
        Assert.That(b.Queue.Take(2), Is.EqualTo(new[] { 1, 0 }), "4.02 先于 4.01，不先取整成同速");
        Assert.That(b.Log.Last(), Does.Contain("游荡尸(4.02) → 你(4.01)"));
        DecimalSpeeds(4.0, 4.0);
        var t = Fight(42, "shambler", "shambler");
        var tb = t.Battle!;
        tb.PlayerTieRoll = 0.5;
        tb.Enemies[0].TieRoll = 0.7;
        tb.Enemies[1].TieRoll = 0.5;
        BeginRound(t);
        Assert.That(tb.Queue.Take(3), Is.EqualTo(new[] { 1, 0, 2 }), "u 降序；主角与 2 号 u 同为 0.5 时 worldActorId 升序（主角记 0）");
        var u = SaveSerializer.Deserialize(SaveSerializer.Serialize(t), E.Config);
        Assert.That((u.Battle!.PlayerTieRoll, u.Battle.Enemies[0].TieRoll), Is.EqualTo((0.5, 0.7)), "u 随存档不重抽");
    }

    /// <summary>CB-Q71b：4.5 对 3.0 每轮 q=25；4.04 对 4.0 每轮 q=0.5，按 0.0001 累计、不取整也不清小数。</summary>
    [Test]
    public void CB_Q71b_PursuitProgress_KeepsDecimals()
    {
        Assert.That(E.PursuitGain(4.5, 3.0), Is.EqualTo(25).Within(1e-12));
        Assert.That(E.PursuitGain(4.04, 4.0), Is.EqualTo(0.5).Within(1e-9));
        DecimalSpeeds(4.04, 4.0);
        var s = Fight(43, "shambler");
        var b = s.Battle!;
        Assert.That(b.PlayerPursuit, Is.EqualTo(0.5).Within(1e-12), "第1轮轮初 +0.5");
        BeginRound(s);
        BeginRound(s);
        Assert.That(b.PlayerPursuit, Is.EqualTo(1.5).Within(1e-12), "累计，不每轮取整");
        Assert.That(b.PlayerPursuit * 10000, Is.EqualTo(Math.Round(b.PlayerPursuit * 10000)).Within(1e-6), "按 0.0001 保存");
        DecimalSpeeds(4.0, 3.0);
        var t = Fight(44, "shambler");
        Assert.That(t.Battle!.PlayerPursuit, Is.EqualTo(16.6667).Within(1e-12), "50×(4/3−1)=16.666…→0.0001 精度");
    }

    /// <summary>CB-Q71c：量化与显示——0.01 四舍五入、最低1；显示1—2位小数；撤退用同一小数速度；AP 仍整数。</summary>
    [Test]
    public void CB_Q71c_Quantize_Display_RetreatSameSource()
    {
        Assert.That(E.QuantizeSpeed(4.005), Is.EqualTo(4.01));
        Assert.That(E.QuantizeSpeed(4.004), Is.EqualTo(4.0));
        Assert.That(E.QuantizeSpeed(0.3), Is.EqualTo(1.0), "最低1");
        Assert.That((Fmt.Speed(4), Fmt.Speed(3.5), Fmt.Speed(4.05), Fmt.Speed(4.04)), Is.EqualTo(("4.0", "3.5", "4.05", "4.04")));
        DecimalSpeeds(4.04, 4.0);
        var s = Fight(45, "shambler");
        var b = s.Battle!;
        b.Panic = 0;
        var r = E.RetreatChance(s, b);
        Assert.That(r.chance, Is.EqualTo(1 / (1 + Math.Exp(-((4.04 - 4.0) / 3 + 0.7)))).Within(1e-12), "撤退与追击同源小数速度");
        Assert.That(E.NormalActivationAp(s.Player), Is.EqualTo(3), "AP 仍整数");
    }

    // ───────────── M170：Q65/66/68/70/72/73/74 选 A 的现状核对 ─────────────

    /// <summary>CB-Q65：敌人追击只做一次合法普通攻击——不重复咬击（不判感染）、不重复嚎叫/蓄力等特殊意图、不另推进世界时间。</summary>
    [Test]
    public void CB_Q65_EnemyPursuit_PlainAttackOnly_NoBiteNoTick()
    {
        var p = Adj()["pursuit"]!;
        Assert.That(((string)p["enemyAction"]!, (bool)p["biteExposureFromPlainPursuit"]!, (bool)p["extraWorldTick"]!), Is.EqualTo(("one_legal_plain_attack_only", false, false)));
        var s = Fight(51, "shambler");
        var b = s.Battle!;
        var e = Enemy(s, 1);
        e.Intent = "bite";
        var minute = s.GameMinute;
        var ticks = b.Ticks;
        var log0 = b.Log.Count;
        Force(s, Draw(), Below(0.01), Any(), Any(), Any());
        typeof(GameEngine).GetMethod("EnemyActivation", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .Invoke(E, new object[] { s, e, true });
        var lines = b.Log.Skip(log0).ToList();
        Assert.That(lines, Has.Some.Contains("追击"));
        Assert.That(lines, Has.None.Contains("咬击").And.None.Contains("病毒"), "追击不改判咬击、不判感染");
        Assert.That(s.Body.Infected, Is.False);
        Assert.That((s.GameMinute, b.Ticks), Is.EqualTo((minute, ticks)), "追击不额外 tick");
        // 嚎叫尸当轮意图是嚎叫：追击仍是普通攻击，不再嚎叫加恐慌
        var t = Fight(52, "howler");
        var tb = t.Battle!;
        var h = Enemy(t, 1);
        h.Intent = "howl";
        var panic = tb.Panic;
        Force(t, Draw(), AtLeast(0.99), Any(), Any());
        typeof(GameEngine).GetMethod("EnemyActivation", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .Invoke(E, new object[] { t, h, true });
        Assert.That(tb.Panic, Is.EqualTo(panic), "追击不重复嚎叫");
        Assert.That(tb.Log.Last(), Does.Contain("追击").And.Contain("落空"));
    }

    /// <summary>CB-Q66/Q68/Q72/Q73/Q74：合同值与现行配置/规则一致（行为另由 B6 R23_EnemyBleedKillsLast…、CB03、R27_RetreatFormula、CB01_SingleAlly…、B7 技能表用例覆盖）。</summary>
    [Test]
    public void CB_Q66_68_72_73_74_ContractMatchesCurrentRules()
    {
        var a = Adj();
        Assert.That((int)a["bleedStartVictory"]!["ifNoEffectiveActionThisRoundTickMinutes"]!, Is.Zero);
        var br = Cfg.CombatActions.Single(x => x.Id == "breathe");
        Assert.That((br.RestoreEnergy, br.ReducePanic, br.ApCost), Is.EqualTo(((double)a["breathing"]!["energy"]!, (double)a["breathing"]!["panicReduction"]!, 2)));
        var rt = Cfg.CombatActions.Single(x => x.Id == "retreat");
        Assert.That((rt.ApCost, rt.LowEnergyChanceFactor), Is.EqualTo(((int)a["retreat"]!["ap"]!, (double)a["retreat"]!["forcedLowEnergyFactor"]!)));
        Assert.That(Cfg.Panic.Penalties.Select(x => x.RetreatFactor), Is.EqualTo(new[] { (double)a["retreat"]!["panic60"]!, (double)a["retreat"]!["panic90"]! }));
        Assert.That((Cfg.Retreat.SpeedDivisor, Cfg.Retreat.Offset), Is.EqualTo((3.0, 0.7)), "sigmoid((己速−敌最高速)/3+0.7)");
        Assert.That((bool)a["swap"]!["newFixedPositionLockImplemented"]!, Is.False);
        var sw = Cfg.CombatActions.Single(x => x.Id == "swap");
        Assert.That((sw.ApCost, sw.Energy), Is.EqualTo((1, (double?)0.1)));
        var s = Fight(53, "shambler");
        Ready(s);
        Assert.That(E.CommandBlocked(s, BattleCommand.Of("swap", 2)), Is.Null, "Q80 起单人也可调整站位（取代 Q73 时的单人换位禁用）");
        // Q74：AP 逐 A06 技能表（75 节点），推挡/护身/盾击等 1AP 例外保留，不统一改 2
        var rows = ((JArray)JObject.Parse(File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "design.a12.json")))["equipment"]!["skills"]!).ToDictionary(r => (string)r["id"]!, r => (int)r["ap"]!);
        Assert.That(Cfg.SkillNodes.ToDictionary(n => n.Id, n => n.Ap), Is.EqualTo(rows));
        Assert.That((E.SkillNode("unarmed_03")!.Ap, E.SkillNode("shield_01")!.Ap), Is.EqualTo((1, rows["shield_01"])));
        Assert.That((bool)a["skillAp"]!["allControlsForcedTo2"]!, Is.False);
    }

    /// <summary>CB-Q70：失衡先命中后判控制（未命中不判、不给稳态）；成功给稳态、同轮不叠第二层。</summary>
    [Test]
    public void CB_Q70_StaggerNeedsHitThenControl()
    {
        var c = Adj()["control"]!;
        Assert.That(((bool)c["staggerRequiresHitThenControl"]!, (bool)c["successfulControlGrantsSteady"]!), Is.EqualTo((true, true)));
        var s = Fight(54, "worker");
        s.Skills["unarmed"].Level = 3;
        Ready(s);
        var e = Enemy(s, 1);
        Force(s, AtLeast(0.999), Any(), Any(), Any());
        Assert.That(Cmd(s, "unarmed_03", 1).Ok, Is.True);
        Assert.That((e.Stagger, e.SteadyUntilRound >= s.Battle!.Round), Is.EqualTo((false, false)), "推空：不判控制、不给稳态");
        Assert.That(s.Battle.Log.Last(), Does.Contain("落空").Or.Contain("没有打中").Or.Contain("未命中"));
    }

    // ───────────── M172：B7c 线上试玩第 1 轮修复 ─────────────

    /// <summary>修复1：攻击预测写出命中能力构成（武器+熟练+基础8+装备）与疲劳“只算一次”，数值与 AccuracyRating 同源。</summary>
    [Test]
    public void Fix_B7c_AccuracyText_ShowsBaseEightAndFatigue()
    {
        var s = Fight(61, "shambler");
        Ready(s);
        Arm(s, "stone_knife");
        var t = KnifeOnShambler(s, 3, 90, 0, false);
        Assert.That(t.AccuracyText, Does.StartWith("武器26＋熟练3＋基础8").And.Contain("低体力×0.95（只算一次）"));
        var u = KnifeOnShambler(s, 18, 90, 0, false);
        Assert.That(u.AccuracyText, Is.EqualTo("武器26＋熟练3＋基础8"));
    }

    /// <summary>修复2：容量说明有不带“本场容量C”前缀的版本，右栏不重复；修复4：精神没变时胜利日志不写“精神−0.00”。</summary>
    [Test]
    public void Fix_B7c_StayNoteNoPrefix_NoZeroMoraleText()
    {
        var s = Field(62);
        Spot(s, "shambler", "crawler");
        Assert.That(E.EncounterStayNote(s), Does.StartWith("只有排在最前的1只接战").And.Not.Contain("容量C"));
        Assert.That(E.EncounterCapacityNote(s), Does.StartWith("本场容量C1：只有排在最前的1只接战"));
        Assert.That(E.Engage(s).Ok, Is.True);
        Enemy(s, 1).Hp = 0.01;
        s.Battle!.Panic = 0;
        ToPlayer(s);
        s.Battle!.Panic = 0;
        Force(s, Below(0.001), AtLeast(0.99), Any(), Any(), Any());
        Cmd(s, "basic_attack", 1);
        Assert.That(s.Battle, Is.Null);
        var last = s.Log.Last(l => l.Text.Contains("战斗胜利")).Text;
        Assert.That(last, Does.Not.Contain("精神−0.00").And.Not.Contain("精神"), last);
    }
}
