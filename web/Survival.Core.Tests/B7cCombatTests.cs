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
}
