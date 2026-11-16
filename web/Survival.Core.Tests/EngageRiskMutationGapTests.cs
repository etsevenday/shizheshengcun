using System.Reflection;
using Survival.Core.Config;
using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 变异测试补测（Stryker.NET，基线 10da301d）：<c>src/Core/Engine/GameEngine.EngageRisk.cs</c> 交战风险五档与战前命中预估。
/// 规则出处：R05「2026-11-14 Q613主控定稿：当前队伍对真实敌踪的即时风险」§只读、确定性的标准战力估计 第1—6条、分档表与零值优先段；
/// R05「2026-11-15 Q665」（武器Ⅰ／Ⅱ页联合枚举，切页 0AP／0 体力才枚举另一页）；S14／R05「Q646—Q654」（确定入场恐慌、战前命中）、Q663（本场不入场）。
/// 单次期望 d 的输入（命中、暴击、伤害区间）按 R03 §4 原战斗公式（combatMath：我方攻击能力＝武器命中＋3×熟练＋基础8，命中＝A/(A+闪避)，
/// 暴击率＝暴击等级/(暴击等级＋100)，暴击等级＝武器基础＋0.3×熟练，暴击×1.5，伤害＝抽值×(1＋0.035×熟练)×招式倍率＋平加−护甲，最低 0）
/// 与发布配置里的武器／敌种数值手算，期望值写字面量并注明来源，不从被测函数取。
/// </summary>
public class EngageRiskMutationGapTests : B41cTestBase
{
    private EnemyCardView Card(GameState s) => E.EnemyCardsHere(s).Single();
    private EngageRiskFactors Factors(GameState s) => E.EngageRiskFactorsFor(s, Card(s))!;

    /// <summary>Q613 §3 单次期望 d=p_hit×[(1−p_crit)×(d_min+d_max)/2＋p_crit×(c_min+c_max)/2]；区间按 R03 §4 由武器抽值×乘数（×暴击1.5）＋平加−护甲，最低 0。</summary>
    private static double D(double acc, double eva, double critRating, double wMin, double wMax, double mult, double flat = 0, double armor = 0)
    {
        double Dmg(double raw, double cf) => Math.Max(0, raw * mult * cf + flat - armor);
        var p = acc / (acc + eva);
        var c = critRating / (critRating + 100);
        return p * ((1 - c) * (Dmg(wMin, 1) + Dmg(wMax, 1)) / 2 + c * (Dmg(wMin, 1.5) + Dmg(wMax, 1.5)) / 2);
    }

    /// <summary>Q613 §5：敌方一击 d=命中（敌攻击能力对我方闪避 36，config scope.basePlayerDodge）×区间均值（伤害×倍率−本人护甲，最低 0；敌方无暴击）。</summary>
    private static double DEnemy(double atk, double dmgMin, double dmgMax, double factor = 1, double armor = 0, double dodge = 36) =>
        atk / (atk + dodge) * (Math.Max(0, dmgMin * factor - armor) + Math.Max(0, dmgMax * factor - armor)) / 2;

    // 发布配置手算的常用 d（熟练 1 级：乘数 1.035、命中＋3、暴击等级＋0.3）
    /// <summary>徒手普通拳击对游荡尸：拳 命中24 暴击3 伤害0.6—0.9（weapons.fists）；游荡尸闪避25（enemies.shambler）。</summary>
    private static readonly double FistVsShambler = D(24 + 3 + 8, 25, 3.3, 0.6, 0.9, 1.035);
    /// <summary>手斧普通攻击对游荡尸：手斧 命中24 暴击5 伤害1.6—2.4（weapons.hatchet；穿甲0.1 对护甲0 无效）。</summary>
    private static readonly double HatchetVsShambler = D(24 + 3 + 8, 25, 5.3, 1.6, 2.4, 1.035);
    /// <summary>游荡尸普通抓击对主角：攻击能力20、伤害0.7—1.1（enemies.shambler）。</summary>
    private static readonly double ShamblerHit = DEnemy(20, 0.7, 1.1);

    private EnemyGroupCard Group(GameState s, params WorldEnemy[] members) =>
        (EnemyGroupCard)typeof(GameEngine).GetMethod("AddGroupCard", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(E, new object[] { s, "field", members.ToList(), "explore", s.GameMinute })!;

    /// <summary>农野荒地放一张组卡（成员按给定顺序），本场容量 capacity。</summary>
    private GameState GroupField(int capacity, params string[] enemyIds)
    {
        var s = Field(capacity);
        Group(s, enemyIds.Select(id => E.DebugSpawnEnemy(s, id, "field")).ToArray());
        return s;
    }

    /// <summary>用改过的配置副本建引擎（只为造出策划案已规定、发布配置里恰好没有的起点；不动共享配置）。</summary>
    private void UseConfig(Action<GameConfig> edit)
    {
        var cfg = LoadConfig();
        edit(cfg);
        E = new GameEngine(cfg);
    }

    private void SetPlayerSlot(GameState s, int slot)
    {
        s.FormationPreset = Enumerable.Repeat("", 4).ToList();
        s.FormationPreset[slot - 1] = GameState.PlayerId;
    }

    // ───────────── R 公式与零值优先（静态） ─────────────

    /// <summary>Q613 §6：R=(H_E×D_E)/(H_A×D_A)×clamp(sqrt(V_E/V_A),0.75,1.5)。六项各不相同时 R=3×5/(2×4)×1=1.875 → 较高。</summary>
    [Test]
    public void RiskIndex_UsesAllSixTermsInPlace()
    {
        Assert.That(GameEngine.EngageRiskIndex(2, 4, 4, 3, 5, 4), Is.EqualTo(1.875).Within(1e-12));
        var f = new EngageRiskFactors { Allies = 1, Enemies = 1, HA = 2, DA = 4, VA = 4, HE = 3, DE = 5, VE = 4 };
        Assert.That(GameEngine.EngageRiskClassify(f), Is.EqualTo(EngageRiskLevel.High), "1.35≤1.875<2.20");
        // 我方健康／输出翻倍，R 减半到 0.46875 → 较低
        Assert.That(GameEngine.EngageRiskClassify(new EngageRiskFactors { Allies = 1, Enemies = 1, HA = 4, DA = 8, VA = 4, HE = 3, DE = 5, VE = 4 }), Is.EqualTo(EngageRiskLevel.Low));
    }

    /// <summary>Q613 零值优先：无有效我方活成员一律极高——即使后台健康／输出项非零也不按 R 分档；同样数值有 1 名我方成员则按 R=0.04 很低。</summary>
    [Test]
    public void ZeroRule_NoAllyMembers_VeryHighRegardlessOfR()
    {
        Assert.That(GameEngine.EngageRiskClassify(new EngageRiskFactors { Allies = 0, HA = 5, DA = 5, VA = 4, HE = 1, DE = 1, VE = 4 }), Is.EqualTo(EngageRiskLevel.VeryHigh));
        Assert.That(GameEngine.EngageRiskClassify(new EngageRiskFactors { Allies = 1, HA = 5, DA = 5, VA = 4, HE = 1, DE = 1, VE = 4 }), Is.EqualTo(EngageRiskLevel.VeryLow), "配对：有成员时按 R 分档");
    }

    /// <summary>Q613 零值优先段：不输出 NaN、不猜等级——R 非有限（坏数据）时为暂无法评估（null），不落到极高。</summary>
    [Test]
    public void NonFiniteR_NotGuessed()
    {
        Assert.That(GameEngine.EngageRiskClassify(new EngageRiskFactors { Allies = 1, HA = 20, DA = 1, VA = 4, HE = double.PositiveInfinity, DE = 1, VE = 4 }), Is.Null, "R=+∞");
        Assert.That(GameEngine.EngageRiskClassify(new EngageRiskFactors { Allies = 1, HA = 20, DA = 1, VA = 4, HE = double.NaN, DE = 1, VE = 4 }), Is.Null, "R=NaN");
        Assert.That(GameEngine.EngageRiskClassify(new EngageRiskFactors { Allies = 1, HA = 20, DA = 1, VA = 4, HE = 100, DE = 1, VE = 4 }), Is.EqualTo(EngageRiskLevel.VeryHigh), "配对：有限的大 R 照常极高");
    }

    /// <summary>Q613 零值优先段：数据异常（敌踪健康非有限）显示「交战风险：暂无法评估」，不猜等级；正常数据给出等级。</summary>
    [Test]
    public void EngageRiskFor_NonFiniteHp_UnknownText()
    {
        var (s, _) = OneShambler();
        var ok = E.EngageRiskFor(s, Card(s));
        Assert.That((ok.Unknown, ok.Invalid), Is.EqualTo((false, false)), "配对：正常数据有等级");
        s.WorldEnemies.Single().Hp = double.PositiveInfinity;
        var r = E.EngageRiskFor(s, Card(s));
        Assert.That(r.Unknown, Is.True);
        Assert.That(r.Invalid, Is.False);
        Assert.That(r.Text, Is.EqualTo("交战风险：暂无法评估"));
    }

    // ───────────── 敌方 D_E（第5条） ─────────────

    /// <summary>
    /// 第5条：敌人用原普通攻击，命中按攻击能力对被打者本人闪避、区间按本人护甲扣减（最低 0）、无暴击。
    /// 游荡尸对无甲主角 D_E=20/56×0.9；穿重甲（护甲0.65，gear.heavy_armor）后区间 0.05—0.45。
    /// </summary>
    [Test]
    public void EnemyD_PlainAttack_HitAndArmorPerTarget()
    {
        var (s, _) = OneShambler();
        Assert.That(Factors(s).DE, Is.EqualTo(ShamblerHit).Within(1e-9));
        Assert.That(ShamblerHit, Is.EqualTo(20.0 / 56 * 0.9).Within(1e-12));
        E.CreateItems(s, "heavy_armor", 1, ItemInstance.EquipOwner(GameState.PlayerId, EquipSlots.Armor));
        Assert.That(E.PlayerArmor(s), Is.EqualTo(0.65).Within(1e-9));
        Assert.That(Factors(s).DE, Is.EqualTo(DEnemy(20, 0.7, 1.1, 1, 0.65)).Within(1e-9));
        Assert.That(DEnemy(20, 0.7, 1.1, 1, 0.65), Is.EqualTo(20.0 / 56 * 0.25).Within(1e-12));
    }

    /// <summary>
    /// 第5条：后排型（无抓击步骤、己位全在3—4号位）沿原弱近战×0.7（WeakAttackFactor）。抢掠者枪手被排到1号位：命中26/62、
    /// 区间 1.1×0.7、1.6×0.7 再扣本人护甲0.65。
    /// </summary>
    [Test]
    public void EnemyD_RearTypeWeakMelee_FactorBeforeArmor()
    {
        var s = Field(1, "raider_gunner");
        Assert.That(Factors(s).DE, Is.EqualTo(DEnemy(26, 1.1, 1.6, 0.7)).Within(1e-9));
        E.CreateItems(s, "heavy_armor", 1, ItemInstance.EquipOwner(GameState.PlayerId, EquipSlots.Armor));
        Assert.That(Factors(s).DE, Is.EqualTo(26.0 / 62 * ((1.1 * 0.7 - 0.65) + (1.6 * 0.7 - 0.65)) / 2).Within(1e-9));
    }

    /// <summary>
    /// 第5条：被压到前排的后排型（攀附者己位2—3，入场在1号位）改原弱攻击×0.7、打我方1、2号位；正常激活 3AP 够一次 2AP 主攻击。
    /// 攀附者攻击能力24、伤害0.7—1.1（enemies.z_clinger）。
    /// </summary>
    [Test]
    public void EnemyD_PushedToFront_WeakAttack()
    {
        var s = Field(1, "z_clinger");
        Assert.That(Factors(s).DE, Is.EqualTo(DEnemy(24, 0.7, 1.1, 0.7)).Within(1e-9));
        Assert.That(DEnemy(24, 0.7, 1.1, 0.7), Is.EqualTo(0.252).Within(1e-12));
    }

    /// <summary>
    /// 第5条＋R03／A09：每次正常激活一个 2AP 主攻击。正常激活恰好 2AP 的敌人仍能出手（够得到、被压前排两种路径）；只有 1AP 付不起主攻击，
    /// 完全无原合法伤害路径记 0。（发布配置敌人都是 3／4AP，这里用配置副本造 2AP、1AP 的敌种。）
    /// </summary>
    [Test]
    public void EnemyD_MainAttackNeedsTwoAp()
    {
        UseConfig(c => { c.Enemies.Single(x => x.Id == "shambler").Ap = 2; c.Enemies.Single(x => x.Id == "z_clinger").Ap = 2; });
        Assert.That(Factors(Field(1, "shambler")).DE, Is.EqualTo(ShamblerHit).Within(1e-9), "2AP 够得到：出手");
        Assert.That(Factors(Field(1, "z_clinger")).DE, Is.EqualTo(DEnemy(24, 0.7, 1.1, 0.7)).Within(1e-9), "2AP 被压前排：弱攻击");
        // 2AP 的 3 号位游荡尸：前移 1AP 后只剩 1AP，不够主攻击 → 0；前两名照常
        Assert.That(Factors(GroupField(3, "shambler", "shambler", "shambler")).DE, Is.EqualTo(2 * ShamblerHit).Within(1e-9));

        UseConfig(c => { c.Enemies.Single(x => x.Id == "shambler").Ap = 1; c.Enemies.Single(x => x.Id == "z_clinger").Ap = 1; });
        Assert.That(Factors(Field(1, "shambler")).DE, Is.EqualTo(0), "1AP 付不起 2AP 主攻击");
        Assert.That(Factors(Field(1, "z_clinger")).DE, Is.EqualTo(0), "1AP 付不起弱攻击");
    }

    /// <summary>
    /// 第5条（实现同步：站太后先前移 1 格再重验）：容量 3 时 3 号位游荡尸前移到 2 号位后剩 2AP 出手，三人都计；
    /// 容量 4 时 4 号位那只前移 1 格仍在 3 号位、够不到，本次激活无合法伤害路径，记 0。
    /// </summary>
    [Test]
    public void EnemyD_StandingTooFarBack_AdvanceThenAttack()
    {
        Assert.That(Factors(GroupField(3, "shambler", "shambler", "shambler")).DE, Is.EqualTo(3 * ShamblerHit).Within(1e-9));
        var four = Factors(GroupField(4, "shambler", "shambler", "shambler", "shambler"));
        Assert.That(four.Enemies, Is.EqualTo(4));
        Assert.That(four.DE, Is.EqualTo(3 * ShamblerHit).Within(1e-9));
    }

    /// <summary>
    /// 第5条＋R03 Q80：在合法己位但我方最前者躲在后排、前面全空时迫近（1AP）＋弱近袭×0.7（combatMath.exposureApproach），只打最前的暴露者。
    /// 主角单人站 3 号位：D_E=20/56×0.9×0.7=0.225。
    /// </summary>
    [Test]
    public void EnemyD_Approach_WeakHitOnFrontmostExposed()
    {
        var (s, _) = OneShambler();
        SetPlayerSlot(s, 3);
        Assert.That(Factors(s).DE, Is.EqualTo(DEnemy(20, 0.7, 1.1, 0.7)).Within(1e-9));
        Assert.That(DEnemy(20, 0.7, 1.1, 0.7), Is.EqualTo(0.225).Within(1e-12));
    }

    // ───────────── 人数、速度（第6条）与参战成员 ─────────────

    /// <summary>
    /// 第6条：V_A、V_E 取各自活成员当前派生速度的算术平均（最低 1）。主角徒手速度4（actorBaseDefense p0）、同伴林照基础4持手斧（速度−1）→ V_A=3.5；
    /// 敌方游荡尸2、奔尸6（enemies.*.speed）→ V_E=4；两名入场敌人都计数。
    /// </summary>
    [Test]
    public void Speeds_AreAveragesOfMembers()
    {
        var s = GroupField(2, "shambler", "runner");
        E.DebugQuickParty(s, 2);
        E.DebugTeleport(s, "field");
        var mate = s.Party.Single(id => id != GameState.PlayerId);
        Assert.That(mate, Is.EqualTo("npc_lin_zhao"));
        s.Items.RemoveAll(i => i.Owner == ItemInstance.EquipOwner(mate, EquipSlots.Weapon1) || i.Owner == ItemInstance.EquipOwner(mate, EquipSlots.Weapon2));
        E.CreateItems(s, "hatchet", 1, ItemInstance.EquipOwner(mate, EquipSlots.Weapon1));
        var f = Factors(s);
        Assert.That(f.Allies, Is.EqualTo(2));
        Assert.That(f.Enemies, Is.EqualTo(2));
        Assert.That(f.VA, Is.EqualTo(3.5).Within(1e-9));
        Assert.That(f.VE, Is.EqualTo(4).Within(1e-9));
        Assert.That(f.HE, Is.EqualTo(3.6 + 3.2).Within(1e-9), "游荡尸3.6＋奔尸3.2");
    }

    /// <summary>
    /// Q613「实际武器页」＋R03 开战默认页（武器Ⅰ空而Ⅱ有武器为Ⅱ页）＋Q665：每名同伴按他自己的开战默认页评估。切页要体力（配置副本 0.1）时只看默认页，
    /// 同伴武器Ⅰ空、Ⅱ持手斧与武器Ⅰ持手斧，默认页都是手斧，D_A 相同；配对：同伴只有拳页时 D_A 更低。
    /// </summary>
    [Test]
    public void MateEvaluatedOnOwnDefaultWeaponPage()
    {
        UseConfig(c => c.CombatActions.Single(a => a.Id == "switch_weapon").Energy = 0.1);
        double Da(string? slot)
        {
            var s = Field(2, "shambler");
            E.DebugQuickParty(s, 2);
            E.DebugTeleport(s, "field");
            var mate = s.Party.Single(id => id != GameState.PlayerId);
            s.Items.RemoveAll(i => i.Owner == ItemInstance.EquipOwner(mate, EquipSlots.Weapon1) || i.Owner == ItemInstance.EquipOwner(mate, EquipSlots.Weapon2));
            if (slot != null) E.CreateItems(s, "hatchet", 1, ItemInstance.EquipOwner(mate, slot));
            var f = Factors(s);
            Assert.That(f.Allies, Is.EqualTo(2));
            return f.DA;
        }
        var page1 = Da(EquipSlots.Weapon1);
        Assert.That(Da(EquipSlots.Weapon2), Is.EqualTo(page1).Within(1e-9));
        Assert.That(Da(null), Is.LessThan(page1), "配对：同伴只有拳页");
    }

    /// <summary>
    /// Q613「我方只取……真正会参战的存活成员……死亡……不计」＋零值优先：主角已确死（R09 §2：主角终局或本人确死标记任一）时不计，
    /// 单人即无有效我方活成员 → 极高（不报暂无法评估）。
    /// </summary>
    [Test]
    public void DeadPlayer_Alone_NoAllies_VeryHigh()
    {
        var (s, _) = OneShambler();
        Assert.That(Factors(s).Allies, Is.EqualTo(1), "配对：活着时计入");
        s.Progress.Dead = true;
        var f = Factors(s);
        Assert.That((f.Allies, f.HA, f.DA), Is.EqualTo((0, 0.0, 0.0)));
        var r = E.EngageRiskFor(s, Card(s));
        Assert.That(r.Unknown, Is.False);
        Assert.That(r.Level, Is.EqualTo(EngageRiskLevel.VeryHigh));
        Assert.That(r.Text, Is.EqualTo("交战风险：极高"));

        // 本人确死标记（R09 §2 确死来源之一）同样不计
        s.Progress.Dead = false;
        s.Actors[GameState.PlayerId].Dead = true;
        var g = Factors(s);
        Assert.That((g.Allies, g.HA, g.DA), Is.EqualTo((0, 0.0, 0.0)));
        Assert.That(E.EngageRiskFor(s, Card(s)).Text, Is.EqualTo("交战风险：极高"));
    }

    /// <summary>同上：主角确死不计，同行存活同伴照计（H_A 只是同伴当前健康）。</summary>
    [Test]
    public void DeadPlayer_LivingMateStillCounted()
    {
        var s = Field(2, "shambler");
        E.DebugQuickParty(s, 2);
        E.DebugTeleport(s, "field");
        var mate = s.Party.Single(id => id != GameState.PlayerId);
        s.Progress.Dead = true;
        var f = Factors(s);
        Assert.That(f.Allies, Is.EqualTo(1));
        Assert.That(f.HA, Is.EqualTo(s.Actors[mate].Stats.Health).Within(1e-9));
    }

    // ───────────── 我方 D_A（第2—4条、Q665） ─────────────

    /// <summary>
    /// 第4条：n=floor(AP预算/AP成本)。普通拳击 2AP（skillNodes.unarmed_01）：正常激活 3AP（阶位0）1 次、4AP（阶位1）2 次、5AP（阶位2）仍 2 次。
    /// </summary>
    [Test]
    public void AllyD_RepeatsByApBudget()
    {
        var (s, _) = OneShambler();
        Assert.That(Factors(s).DA, Is.EqualTo(FistVsShambler).Within(1e-9));
        E.DebugSetTacticalTier(s, 1);
        Assert.That(Factors(s).DA, Is.EqualTo(2 * FistVsShambler).Within(1e-9));
        E.DebugSetTacticalTier(s, 2);
        Assert.That(Factors(s).DA, Is.EqualTo(2 * FistVsShambler).Within(1e-9));
    }

    /// <summary>第4条：AP 成本为 0 的攻击最多 1 次，不能得到无限分（发布配置没有 0AP 攻击，用配置副本把普通拳击改 0AP）。</summary>
    [Test]
    public void AllyD_ZeroApAttack_AtMostOnce()
    {
        UseConfig(c => c.SkillNodes.Single(n => n.Id == "unarmed_01").Ap = 0);
        var (s, _) = OneShambler();
        E.DebugSetTacticalTier(s, 1);
        Assert.That(Factors(s).DA, Is.EqualTo(FistVsShambler).Within(1e-9));
    }

    /// <summary>第4条：有本回合冷却的攻击最多 1 次（用配置副本给普通拳击 1 回合冷却；4AP 本可 2 次）。配对见 <see cref="AllyD_RepeatsByApBudget"/> 无冷却 2 次。</summary>
    [Test]
    public void AllyD_CooldownAttack_AtMostOnce()
    {
        UseConfig(c => c.SkillNodes.Single(n => n.Id == "unarmed_01").CooldownRounds = 1);
        var (s, _) = OneShambler();
        E.DebugSetTacticalTier(s, 1);
        Assert.That(Factors(s).DA, Is.EqualTo(FistVsShambler).Within(1e-9));
    }

    /// <summary>
    /// 第4条：n 不超过 floor(当前体力/体力成本)。普通拳击体力成本约0.29（weapons.fists 0.3 经熟练修正），4AP 下体力0.59 够 2 次、0.5 只够 1 次；
    /// 付费后体力&lt;4 触发原疲劳命中×0.95（combatMath.fatigueAccuracyFactor）。
    /// </summary>
    [Test]
    public void AllyD_RepeatsByEnergy()
    {
        var (s, _) = OneShambler();
        E.DebugSetTacticalTier(s, 1);
        var tired = D((24 + 3 + 8) * 0.95, 25, 3.3, 0.6, 0.9, 1.035);
        s.Stats.Energy = 0.59;
        Assert.That(Factors(s).DA, Is.EqualTo(2 * tired).Within(1e-9));
        s.Stats.Energy = 0.5;
        Assert.That(Factors(s).DA, Is.EqualTo(tired).Within(1e-9));
    }

    /// <summary>
    /// 第2条：含原允许的应急拳击。体力付不起任何攻击技能（0.2&lt;拳击成本）时只剩应急拳击（combatActions.desperate_punch：1AP、0体力、伤害×0.5、命中×0.65），
    /// 3AP 可 3 次；付费后体力&lt;4 另乘疲劳×0.95。
    /// </summary>
    [Test]
    public void AllyD_DesperatePunchCounted()
    {
        var (s, _) = OneShambler();
        s.Stats.Energy = 0.2;
        var desperate = D((24 + 3 + 8) * 0.65 * 0.95, 25, 3.3, 0.6, 0.9, 1.035 * 0.5);
        Assert.That(Factors(s).DA, Is.EqualTo(3 * desperate).Within(1e-9));
    }

    /// <summary>第4条：n 不超过剩余耐久可供次数 floor(耐久/每次磨损1)（config wear.perExecutedAttackCommand）。手斧耐久恰好 1 只够 1 次，2 够 2 次（4AP）。</summary>
    [Test]
    public void AllyD_RepeatsByDurability()
    {
        var (s, _) = OneShambler();
        E.DebugSetTacticalTier(s, 1);
        var axe = Arm(s, "hatchet");
        axe.Durability = 2;
        Assert.That(Factors(s).DA, Is.EqualTo(2 * HatchetVsShambler).Within(1e-9), "配对：耐久够 2 次");
        axe.Durability = 1;
        Assert.That(Factors(s).DA, Is.EqualTo(HatchetVsShambler).Within(1e-9));
    }

    /// <summary>第4条：耐久可供次数是耐久÷每次磨损（配置副本把每次磨损改 2：耐久 2 只够 1 次）。</summary>
    [Test]
    public void AllyD_DurabilityDividedByWearPerAttack()
    {
        UseConfig(c => c.Wear.PerExecutedAttackCommand = 2);
        var (s, _) = OneShambler();
        E.DebugSetTacticalTier(s, 1);
        Arm(s, "hatchet").Durability = 2;
        Assert.That(Factors(s).DA, Is.EqualTo(HatchetVsShambler).Within(1e-9));
    }

    /// <summary>
    /// 第4条：n 不超过实际弹仓可供次数／每次耗弹。手枪（weapons.pistol：命中25 暴击8 伤害2.4—3.6，使用位2—4）在 2 号位、4AP，
    /// 弹仓 1 发只够 1 次，2 发 2 次；随身没有备弹也不补装填。
    /// </summary>
    [Test]
    public void AllyD_RepeatsByMagazine()
    {
        var (s, _) = OneShambler();
        SetPlayerSlot(s, 2);
        E.DebugSetTacticalTier(s, 1);
        var gun = Arm(s, "pistol");
        var d = D(25 + 3 + 8, 25, 8.3, 2.4, 3.6, 1.035);
        gun.LoadedAmmo = 2;
        Assert.That(Factors(s).DA, Is.EqualTo(2 * d).Within(1e-9), "配对：2 发够 2 次");
        gun.LoadedAmmo = 1;
        Assert.That(Factors(s).DA, Is.EqualTo(d).Within(1e-9));
    }

    /// <summary>第4条：无弹仓的弓按随身箭数计次数（weapons.wooden_bow：命中25 暴击8 伤害1.4—2.3，使用位2—4）。2 号位、4AP、随身 1 支箭只够 1 次。</summary>
    [Test]
    public void AllyD_BowUsesCarriedArrows()
    {
        var (s, _) = OneShambler();
        SetPlayerSlot(s, 2);
        E.DebugSetTacticalTier(s, 1);
        Arm(s, "wooden_bow");
        E.DebugGive(s, "arrow", 1);
        var d = D(25 + 3 + 8, 25, 8.3, 1.4, 2.3, 1.035);
        Assert.That(Factors(s).DA, Is.EqualTo(d).Within(1e-9));
        E.DebugGive(s, "arrow", 1);
        Assert.That(Factors(s).DA, Is.EqualTo(2 * d).Within(1e-9), "配对：2 支够 2 次");
    }

    /// <summary>
    /// 第3条：多段每段各算后相加。徒手熟练10级时连击（skillExec.unarmed_10：2 段、每段×0.85，3AP）每段 d 相同，合计 2 段；
    /// 命中24+30+8、暴击等级3+3、乘数(1+0.35)×0.85。它比普通拳击（×1.35，1 次）高，取最大。
    /// </summary>
    [Test]
    public void AllyD_MultiHit_SegmentsAdded()
    {
        var s = Field(1, "shambler");
        E.DebugSetSkillLevel(s, "unarmed", 10);
        var seg = D(24 + 30 + 8, 25, 3 + 3, 0.6, 0.9, 1.35 * 0.85);
        Assert.That(Factors(s).DA, Is.EqualTo(2 * seg).Within(1e-9));
    }

    /// <summary>
    /// 第3条＋Q82 答复（教学平加按每目标每指令首个有效命中只加一次）：多段攻击第二段起不含平加，区间沿原倍率与扣甲（护甲−穿透）。
    /// 木矛熟练10级突刺（skillExec.spear_10：2 段×0.85）对工人尸（护甲0.3、闪避22），矛上「破甲」词条穿透0.1，主角有教学平加0.1。
    /// </summary>
    [Test]
    public void AllyD_MultiHit_FlatOnlyOnFirstSegment()
    {
        var s = Field(1, "worker");
        var spear = Arm(s, "wooden_spear");
        E.DebugSetSkillLevel(s, "spear", 10);
        const double acc = 27 + 30 + 8, crit = 5 + 3, mult = 1.35 * 0.85;
        // 配对：无平加、无穿透时两段相同
        Assert.That(Factors(s).DA, Is.EqualTo(2 * D(acc, 22, crit, 1.4, 2.1, mult, 0, 0.3)).Within(1e-9));
        s.Traits.Add(new TraitInstance { EffectId = "tutorial_attack_bonus_001", Quality = "green", Label = "攻击力+0.1", FlatAttack = 0.1 });
        spear.Affixes = new List<string> { "piercing" };
        var first = D(acc, 22, crit, 1.4, 2.1, mult, 0.1, 0.3 - 0.1);
        var second = D(acc, 22, crit, 1.4, 2.1, mult, 0, 0.3 - 0.1);
        Assert.That(Factors(s).DA, Is.EqualTo(first + second).Within(1e-9));
    }

    /// <summary>
    /// 第3条：单目标技能对所有合法目标的 d 取算术平均。菜刀（weapons.kitchen_knife：命中28 暴击6 伤害1.2—1.8，目标位1—2）对游荡尸（闪避25）与奔尸（闪避34）。
    /// </summary>
    [Test]
    public void AllyD_SingleTarget_AveragedOverLegalTargets()
    {
        var s = GroupField(2, "shambler", "runner");
        Arm(s, "kitchen_knife");
        var avg = (D(28 + 3 + 8, 25, 6.3, 1.2, 1.8, 1.035) + D(28 + 3 + 8, 34, 6.3, 1.2, 1.8, 1.035)) / 2;
        Assert.That(Factors(s).DA, Is.EqualTo(avg).Within(1e-9));
    }

    /// <summary>
    /// 第3条：一次影响多个目标的技能先对该次真实可影响集合加总。霰弹枪熟练7级专长射击（skillExec.firearms_07：范围×0.65，打敌方1、2号位全部）：
    /// 一个敌人时不如普通射击（×1），D_A=普通射击 d；两个敌人时 2×0.65=1.3 倍普通射击。霰弹枪 命中23 暴击5 伤害3.6—5.2，使用位2—3。
    /// </summary>
    [Test]
    public void AllyD_AreaShot_SumsAffectedTargets()
    {
        double Da(int n)
        {
            var s = n == 1 ? Field(2, "shambler") : GroupField(2, "shambler", "shambler");
            SetPlayerSlot(s, 2);
            Arm(s, "shotgun").LoadedAmmo = 4;
            E.DebugSetSkillLevel(s, "firearms", 7);
            return Factors(s).DA;
        }
        var basic = D(23 + 21 + 8, 25, 5 + 2.1, 3.6, 5.2, 1.245);
        Assert.That(Da(1), Is.EqualTo(basic).Within(1e-9));
        Assert.That(Da(2), Is.EqualTo(1.3 * basic).Within(1e-9));
    }

    // ───────────── Q665 两页联合枚举 ─────────────

    /// <summary>
    /// Q665：武器Ⅰ空、Ⅱ页信号哨（不能攻击）时开战默认页是Ⅱ页；另一页（空槽＝徒手拳击）也作只读候选，不能因默认页无合法攻击跳过它。
    /// </summary>
    [Test]
    public void CrossPage_DefaultPageTwo_PageOneStillEnumerated()
    {
        var (s, _) = OneShambler();
        E.CreateItems(s, "signal_whistle", 1, ItemInstance.EquipOwner(GameState.PlayerId, EquipSlots.Weapon2));
        Assert.That(E.DefaultWeaponPage(s), Is.EqualTo(2));
        Assert.That(Factors(s).DA, Is.EqualTo(FistVsShambler).Within(1e-9));
    }

    /// <summary>Q665：只有原切页规则 0AP 且 0 体力时才无额外成本枚举另一页；切页要体力（配置副本 0.1）时只看开战默认页。配对：发布配置 0 体力切页时另一页计入。</summary>
    [Test]
    public void CrossPage_OnlyWhenSwitchIsFree()
    {
        var (s, _) = OneShambler();
        Arm(s, "signal_whistle");
        Assert.That(Factors(s).DA, Is.EqualTo(FistVsShambler).Within(1e-9), "配对：0AP／0体力切页，Ⅱ页空槽拳击计入");

        UseConfig(c => c.CombatActions.Single(a => a.Id == "switch_weapon").Energy = 0.1);
        var (t, _) = OneShambler();
        Arm(t, "signal_whistle");
        Assert.That(Factors(t).DA, Is.EqualTo(0));
        Assert.That(E.EngageRiskFor(t, Card(t)).Text, Is.EqualTo("交战风险：极高"));
    }

    // ───────────── 战前「你的命中率」（Q647／Q654／Q663） ─────────────

    /// <summary>Q654：命中率只看主角真实开战默认页的普通攻击。武器Ⅰ空、Ⅱ页开山刀（weapons.machete 命中29）：命中能力 29+3+8=40，对游荡尸 40/65。</summary>
    [Test]
    public void PreBattleHit_UsesDefaultWeaponPage()
    {
        var (s, _) = OneShambler();
        E.CreateItems(s, "machete", 1, ItemInstance.EquipOwner(GameState.PlayerId, EquipSlots.Weapon2));
        var h = E.PreBattleHits(s, Card(s)).Single();
        Assert.That(h.Legal, Is.True);
        Assert.That(h.Accuracy, Is.EqualTo(40).Within(1e-9));
        Assert.That(h.HitChance, Is.EqualTo(40.0 / 65).Within(1e-9));
    }

    /// <summary>Q654：主角不参战（已确死：主角终局或本人确死标记，R09 §2）时命中率行全为不合法（当前无可用攻击）；配对：活着时合法。</summary>
    [Test]
    public void PreBattleHit_DeadPlayer_NoLegalAttack()
    {
        var (s, _) = OneShambler();
        Assert.That(E.PreBattleHits(s, Card(s)).Single().Legal, Is.True);
        s.Progress.Dead = true;
        var hits = E.PreBattleHits(s, Card(s));
        Assert.That(hits.Count, Is.EqualTo(1));
        Assert.That(hits.Single().Legal, Is.False);
        s.Progress.Dead = false;
        s.Actors[GameState.PlayerId].Dead = true;
        var marked = E.PreBattleHits(s, Card(s));
        Assert.That(marked.Count, Is.EqualTo(1));
        Assert.That(marked.Single().Legal, Is.False, "本人确死标记");
    }

    /// <summary>
    /// R05 Q613「可选尚未生成实体者只读其既定配置」＋Q654：尚未生成实体的可选强敌（守卫尸 z_guard：健康9、闪避20）按配置入场，
    /// 命中率对它的入场号位照常预估：拳 35/(35+20)；H_E 取配置健康 9。
    /// </summary>
    [Test]
    public void PreBattleHit_UnspawnedOptionalElite()
    {
        var cfg = Cfg.WorldEnemies.LegacyElites.Single(x => x.EnemyId == "z_guard");
        var s = New(5101);
        s.Progress.Flags.Add("hub_registered");
        s.GameMinute = At(3, 9, 0);
        s.SpawnNextCheck = int.MaxValue;
        s.PatrolNextCheck = int.MaxValue;
        E.DebugMapTeleport(s, cfg.NodeId);
        s.WorldEnemies.RemoveAll(w => w.FixedOf == null);
        E.DebugSetProgress(s, cfg.NodeId, 100);
        var card = Card(s);
        Assert.That(card.Members, Is.Empty, "尚未实例化");
        var hits = E.PreBattleHits(s, card);
        Assert.That(hits.Count, Is.EqualTo(1));
        Assert.That(hits[0].Legal, Is.True);
        Assert.That(hits[0].NotEntering, Is.False);
        Assert.That(hits[0].HitChance, Is.EqualTo(35.0 / 55).Within(1e-9));
        Assert.That(E.EngageRiskFactorsFor(s, card)!.HE, Is.EqualTo(9).Within(1e-9));
        Assert.That(s.WorldEnemies.Any(w => w.FixedOf == card.Key), Is.False, "不为评估生成实体");
    }

    /// <summary>Q663：只有超出本场容量的「有效活成员」才写本场不入场；组里已倒下（健康0）的成员不占容量。配对：容量 1 时第三名照常标不入场。</summary>
    [Test]
    public void PreBattleHit_DeadMemberDoesNotTakeCapacity()
    {
        var s = Field(2);
        var a = E.DebugSpawnEnemy(s, "shambler", "field");
        var b = E.DebugSpawnEnemy(s, "shambler", "field");
        var c = E.DebugSpawnEnemy(s, "shambler", "field");
        Group(s, a, b, c);
        a.Hp = 0;
        var hits = E.PreBattleHits(s, Card(s));
        Assert.That(hits.Count, Is.EqualTo(3));
        Assert.That(hits.Count(h => h.NotEntering), Is.EqualTo(0), "容量 2 正好装下两名活成员");
        Assert.That(hits[1].Legal, Is.True);
        foreach (var flag in new[] { "hub_registered", "church_route_record", "academy_safety_record" }) s.Progress.Flags.Remove(flag);
        var one = E.PreBattleHits(s, Card(s));
        Assert.That(one.Select(h => h.NotEntering), Is.EqualTo(new[] { false, false, true }), "配对：容量 1 时第二名活成员不入场");
    }
}
