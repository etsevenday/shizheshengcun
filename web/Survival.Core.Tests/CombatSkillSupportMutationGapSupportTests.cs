using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 变异测试补测（全量基线 10da301d）：GameEngine.CombatSkillSupport.cs —— 医疗技能、快速装填、格挡类技能、保护/掩护/坚守、整队与各技能预览说明。
/// 出处：A06 §2 技能主表（medicine_01—10、firearms_05、unarmed_05、blade_05、shield_03—10、command_01—10）与 §3 经验；
/// A05 器具固定特性（创伤处置、稳定、分诊、协同、预判、反击、金属尖、架阵、盾面）；R03「Q62—Q74」Q69 盾格挡、「Q75—Q82」Q75 医疗任一友方／Q76 持续；
/// 人物与战斗修订参考 v0.6「回血冷却仅在回血实际生效时启动，只止血不延长原回血冷却」；R03/A06 Q105 整队、Q106 掩护坚守；开发计划 v0.9 B7-5、B7-15、B7-16。
/// </summary>
public class CombatSkillSupportMutationGapSupportTests : BattleTestBase
{
    protected override bool NoEnemyApproach => true;

    private const string Me = GameState.PlayerId;
    private const string Lin = "npc_lin_zhao";
    private const string Zhou = "npc_zhou_yan";
    private const string Shen = "npc_shen_he";

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

    private long XpOf(GameState s, string actor, string skill) =>
        s.Actors[actor].Skills.TryGetValue(skill, out var sk) ? sk.Level * 100000L + sk.Xp : 0;

    /// <summary>主角武器Ⅱ医疗器具（当前页）、站 2 号位（医疗使用位 234），医疗 level 级 0 经验，随身纱布/盐水/绷带各 n。</summary>
    private GameState Medic(uint seed, string kit, int level, out ItemInstance tool, int n = 3)
    {
        var s = Fight(seed, "worker");
        Ready(s);
        Arm(s, "kitchen_knife");
        tool = Arm2(s, kit);
        StandAt(s, 2);
        s.Skills["medicine"].Level = level;
        s.Skills["medicine"].Xp = 0;
        s.Battle!.PlayerAp = 9;
        if (n > 0)
        {
            E.DebugGive(s, "medical_gauze", n);
            E.DebugGive(s, "saline", n);
            E.DebugGive(s, "bandage", n);
        }
        return s;
    }

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

    private static BattleCommand On(string action, string? target) => new BattleCommand { Action = action, AllyTarget = target };

    /// <summary>三人：你 2 号（武器Ⅱ医疗器具，医疗 level 级，随身各 3 份）、林昭 1 号、周砚 3 号；轮到你。</summary>
    private GameState MedicParty(uint seed, string kit, int level)
    {
        var s = Party(seed, 3, "worker");
        var b = s.Battle!;
        b.Allies = new List<string> { Lin, Me, Zhou };
        TurnOf(s, Me);
        ArmActor(s, Me, kit, 2);
        s.Skills["medicine"].Level = level;
        s.Skills["medicine"].Xp = 0;
        E.DebugGive(s, "medical_gauze", 3);
        E.DebugGive(s, "saline", 3);
        return s;
    }

    private static List<BattleFxEvent> Main(List<BattleFxEvent> fx) => fx.Where(x => !x.Short && x.Results.Count > 0).ToList();

    // ───────────── 医疗：止血 / 回血 / 冷却 / 上限 ─────────────

    /// <summary>
    /// 创伤处理（A06 medicine_03 止血并回血）：健康满、没有出血 → 拒绝“健康已满”；出血但健康满 → 可用，只止血、不回血、
    /// 不启动回血冷却（v0.6“只止血不延长原回血冷却”），战报“已止血”，有效医疗经验+4（A06 §3）。抓 93、103、517、518 行。
    /// </summary>
    [Test]
    public void TraumaCare_BleedOnly_AtFullHealth_StopsBleedNoCooldown()
    {
        var s = Medic(9201, "field_medkit", 3, out _);
        var b = s.Battle!;
        s.Stats.Health = s.Stats.HealthMax;
        Assert.That(E.CommandBlocked(s, BattleCommand.Of("medicine_03")), Is.EqualTo("健康已满"));
        E.ApplyPlayerBleed(s);
        Assert.That(s.Body.BleedTriggers, Is.GreaterThan(0));
        Assert.That(E.CommandBlocked(s, BattleCommand.Of("medicine_03")), Is.Null, "出血可止");
        var ready0 = s.BandageReadyAt;
        Assert.That(Cmd(s, "medicine_03").Ok, Is.True);
        Assert.That(s.Body.BleedTriggers, Is.EqualTo(0));
        Assert.That(s.Stats.Health, Is.EqualTo(s.Stats.HealthMax).Within(1e-9));
        Assert.That(s.BandageReadyAt, Is.EqualTo(ready0), "只止血不启动回血冷却");
        Assert.That(b.Log.Last(), Does.Contain("已止血"));
        Assert.That(XpOf(s, Me, "medicine"), Is.EqualTo(3 * 100000L + 4));
    }

    /// <summary>
    /// 稳定伤势（A06 medicine_05 只回血，不止血）与非“应急护理”技能不解失衡：出血+失衡时用它回血，出血与失衡都还在；
    /// 健康满时即使出血也拒绝“健康已满”。抓 93 行逻辑变异、106 行两个逻辑变异。
    /// </summary>
    [Test]
    public void StabilizeWound_DoesNotStopBleedNorClearStagger()
    {
        var s = Medic(9202, "field_medkit", 5, out _);
        var b = s.Battle!;
        E.ApplyPlayerBleed(s);
        b.PlayerStagger = true;
        s.Stats.Health = s.Stats.HealthMax;
        Assert.That(E.CommandBlocked(s, BattleCommand.Of("medicine_05")), Is.EqualTo("健康已满"), "不能止血，健康满就没有可处理的");
        s.Stats.Health = s.Stats.HealthMax - 3;
        Assert.That(Cmd(s, "medicine_05").Ok, Is.True);
        Assert.That(s.Body.BleedTriggers, Is.GreaterThan(0), "不止血");
        Assert.That(b.PlayerStagger, Is.True, "不解失衡");
        Assert.That(b.Log.Last(), Does.Not.Contain("已止血").And.Not.Contain("解除"));
    }

    /// <summary>共享 60 分钟回血冷却（A06 medicine_05）：还差 1 分钟时拒绝并写“还需1分钟”；到点（冷却结束分钟=当前分钟）即可用。抓 98、102 行。</summary>
    [Test]
    public void HealCooldown_RemainingMinutes_AndReadyExactlyAtEnd()
    {
        var s = Medic(9203, "field_medkit", 5, out _);
        s.Stats.Health = 5;
        s.BandageReadyAt = s.GameMinute + 1;
        Assert.That(E.CommandBlocked(s, BattleCommand.Of("medicine_05")), Is.EqualTo("回血冷却中（与绷带共享），还需1分钟"));
        s.BandageReadyAt = s.GameMinute;
        Assert.That(E.CommandBlocked(s, BattleCommand.Of("medicine_05")), Is.Null);
        Assert.That(Cmd(s, "medicine_05").Ok, Is.True);
        Assert.That(s.BandageReadyAt, Is.EqualTo(s.GameMinute + 60), "回血后共享 60 分钟冷却");
    }

    /// <summary>
    /// 危急救护（A06 medicine_10：H≤25%上限时 H+1.5×(1+0.02L)）：恰好 25% 可用；分诊器材组（A05“分诊：危急救护的回血额外+0.3H”）只加在危急救护上，
    /// 稳定伤势（0.8×(1+0.02L)）不加。抓 99 行算术、100 行 criticalCare 三个变异。
    /// </summary>
    [Test]
    public void CriticalCare_QuarterBoundary_TriageBonusOnlyOnCriticalCare()
    {
        var s = Medic(9204, "triage_kit", 10, out _);
        var max = s.Stats.HealthMax;
        s.Stats.Health = max * 0.25;
        Assert.That(E.CommandBlocked(s, BattleCommand.Of("medicine_10")), Is.Null, "恰好 25% 可用");
        Assert.That(Cmd(s, "medicine_10").Ok, Is.True);
        Assert.That(s.Stats.Health, Is.EqualTo(max * 0.25 + 1.5 * (1 + 0.02 * 10) + 0.3).Within(1e-9));

        var t = Medic(9205, "triage_kit", 10, out _);
        t.Stats.Health = 5;
        Assert.That(Cmd(t, "medicine_05").Ok, Is.True);
        Assert.That(t.Stats.Health, Is.EqualTo(5 + 0.8 * (1 + 0.02 * 10)).Within(1e-9), "分诊只加危急救护");
    }

    /// <summary>
    /// 医疗量按施术者的医疗等级（A06 公式里的 L；Q75 经验给施术者）：医疗 10 级的你给医疗 1 级的林昭用稳定伤势 → +0.8×1.2；
    /// 回血不超过上限（只补到满），预览与战报写实际回的量。抓 100 行空合并、104 行、266 行、522 行。
    /// </summary>
    [Test]
    public void MedicalHeal_UsesCasterLevel_CappedAtMax()
    {
        var s = MedicParty(9206, "field_medkit", 10);
        var lin = s.Actors[Lin];
        lin.Skills["medicine"].Level = 1;
        lin.Stats.Health = 5;
        Assert.That(E.ExecuteCommand(s, On("medicine_05", Lin)).Ok, Is.True);
        Assert.That(lin.Stats.Health, Is.EqualTo(5 + 0.8 * 1.2).Within(1e-9), "按施术者 10 级");

        var t = Medic(9207, "field_medkit", 10, out _);
        var max = t.Stats.HealthMax;
        t.Stats.Health = max - 0.1;
        var pv = E.PreviewCommand(t, BattleCommand.Of("medicine_05"));
        Assert.That(pv.EffectText, Is.EqualTo($"健康{Fmt.N(max - 0.1)}→{Fmt.N(max)}（共享绷带60分钟回血冷却）"));
        Assert.That(Cmd(t, "medicine_05").Ok, Is.True);
        Assert.That(t.Stats.Health, Is.EqualTo(max).Within(1e-9));
        Assert.That(t.Battle!.Log.Last(), Does.Contain("健康" + Fmt.Delta(0.1)));
    }

    /// <summary>
    /// 应急护理（A06 medicine_07：解除失衡/虚弱二者之一，若均有优先虚弱）：两者都有 → 预览/战报“解除虚弱”，失衡还在，结果字“解除虚弱”；
    /// 只有失衡 → “解除失衡”。抓 268、526、527、534、535 行。
    /// </summary>
    [Test]
    public void EmergencyCare_PrefersWeak_ThenStagger()
    {
        var s = Medic(9208, "field_medkit", 7, out _);
        var b = s.Battle!;
        b.PlayerStagger = true;
        b.PlayerWeakFactor = 0.8;
        b.PlayerWeakUntilRound = b.Round + 1;
        Assert.That(E.PreviewCommand(s, BattleCommand.Of("medicine_07")).EffectText, Is.EqualTo("解除虚弱"));
        E.DrainBattleFx();
        Assert.That(Cmd(s, "medicine_07").Ok, Is.True);
        Assert.That(GameEngine.PlayerWeakActive(b), Is.False, "优先虚弱");
        Assert.That(b.PlayerStagger, Is.True, "失衡留着");
        Assert.That(b.Log.Last(), Does.Contain("解除虚弱").And.Not.Contain("解除失衡"));
        var res = Main(E.DrainBattleFx()).Single().Results.Single();
        Assert.That((res.Outcome, res.Changes.ToList()), Is.EqualTo(("effect", new List<string> { GameEngine.RemovedText("虚弱") })));

        var t = Medic(9209, "field_medkit", 7, out _);
        var tb = t.Battle!;
        tb.PlayerStagger = true;
        Assert.That(E.PreviewCommand(t, BattleCommand.Of("medicine_07")).EffectText, Is.EqualTo("解除失衡"));
        E.DrainBattleFx();
        Assert.That(Cmd(t, "medicine_07").Ok, Is.True);
        Assert.That(tb.PlayerStagger, Is.False);
        Assert.That(tb.Log.Last(), Does.Contain("解除失衡"));
        var tr = Main(E.DrainBattleFx()).Single().Results.Single();
        Assert.That((tr.Outcome, tr.Changes.ToList()), Is.EqualTo(("effect", new List<string> { GameEngine.RemovedText("失衡") })));
    }

    /// <summary>
    /// 应急护理没有失衡/虚弱可解时拒绝，原因写“没有失衡或虚弱”；对自己不加名字前缀，对队友前缀“林昭：”。抓 107、153 行。
    /// </summary>
    [Test]
    public void EmergencyCare_NothingToRemove_ReasonAndPrefix()
    {
        var s = MedicParty(9210, "field_medkit", 7);
        Assert.That(E.CommandBlocked(s, On("medicine_07", Me)), Is.EqualTo("你没有失衡或虚弱"));
        Assert.That(E.CommandBlocked(s, On("medicine_07", Lin)), Is.EqualTo("林昭：林昭没有失衡或虚弱"));
    }

    /// <summary>
    /// 医疗预览：效果放在说明第一条；健康满（无效）时效果栏写原因；有效时写“医疗经验+4”（A06 §3）、器具耐久−1。抓 266、269、270、273 行。
    /// </summary>
    [Test]
    public void MedicalPreview_EffectFirst_ReasonWhenUseless_XpNote()
    {
        var s = Medic(9211, "field_medkit", 5, out _);
        s.Stats.Health = 10;
        var heal = 0.8 * (1 + 0.02 * 5);
        var pv = E.PreviewCommand(s, BattleCommand.Of("medicine_05"));
        Assert.That(pv.EffectText, Is.EqualTo($"健康{Fmt.N(10)}→{Fmt.N(10 + heal)}（共享绷带60分钟回血冷却）"));
        Assert.That(pv.Notes[0], Is.EqualTo(pv.EffectText));
        Assert.That(pv.Notes, Has.Some.EqualTo("有效时随行医疗包耐久−1、医疗经验+4；治疗不暴击"));
        s.Stats.Health = s.Stats.HealthMax;
        var full = E.PreviewCommand(s, BattleCommand.Of("medicine_05"));
        Assert.That(full.EffectText, Is.EqualTo("健康已满"));
        Assert.That(full.Notes[0], Is.EqualTo("健康已满"));
    }

    /// <summary>
    /// 医疗执行战报：先写“消耗盐水剂×1”，再写回血量、器具耐久 前→后（A06：有效时器具耐久−1）、体力；结果段是治疗、数值为实际回血量。
    /// 抓 533、541、543、547、561 行与 522 行战报。
    /// </summary>
    [Test]
    public void MedicalExecute_LogsConsumptionHealWearEnergy_FxHealAmount()
    {
        var s = Medic(9212, "field_medkit", 5, out var kit);
        s.Stats.Health = 10;
        var heal = 0.8 * (1 + 0.02 * 5);
        var d0 = kit.Durability!.Value;
        E.DrainBattleFx();
        Assert.That(Cmd(s, "medicine_05").Ok, Is.True);
        Assert.That(kit.Durability!.Value, Is.EqualTo(d0 - 1).Within(1e-9));
        var log = s.Battle!.Log.Last();
        Assert.That(log, Does.Contain("稳定伤势（随行医疗包）：消耗盐水剂×1，"));
        Assert.That(log, Does.Contain("健康" + Fmt.Delta(heal)));
        Assert.That(log, Does.Contain($"随行医疗包耐久{Fmt.N(d0)}→{Fmt.N(d0 - 1)}"));
        Assert.That(log, Does.Contain("体力−"));
        var ev = Main(E.DrainBattleFx()).Single();
        Assert.That(ev.Motion, Is.EqualTo(BattleFxMotion.Heal));
        var r = ev.Results.Single();
        Assert.That(r.Outcome, Is.EqualTo("heal"));
        Assert.That(r.Amount, Is.EqualTo(heal).Within(1e-9));
    }

    /// <summary>战地包扎（A06 medicine_01 按绷带效果）：医疗经验只发一次 +4（A06 §3“同次物资与技能不重复发”）。抓 513 行。</summary>
    [Test]
    public void FieldBandage_MedicalXpOnlyOnce()
    {
        var s = Medic(9213, "field_medkit", 3, out _);
        s.Stats.Health = 10;
        E.ApplyPlayerBleed(s);
        Assert.That(Cmd(s, "medicine_01").Ok, Is.True);
        Assert.That(s.Body.BleedTriggers, Is.EqualTo(0));
        Assert.That(XpOf(s, Me, "medicine"), Is.EqualTo(3 * 100000L + 4));
    }

    /// <summary>
    /// 救援医疗箱“稳定”（A05：有效医疗后目标获下一次直接伤害−10%）：自己用后下一次直接伤害×0.9，战报写明；普通医疗包没有这一条，倍率保持 1。抓 545 行。
    /// </summary>
    [Test]
    public void RescueMedkit_NextDirectDamageTimesPointNine()
    {
        var s = Medic(9214, "rescue_medkit", 5, out _);
        s.Stats.Health = 10;
        Assert.That(Cmd(s, "medicine_05").Ok, Is.True);
        Assert.That(s.Battle!.PlayerNextDamageFactor, Is.EqualTo(0.9).Within(1e-9));
        Assert.That(s.Battle.Log.Last(), Does.Contain("下一次受到的直接伤害×0.9"));

        var t = Medic(9215, "field_medkit", 5, out _);
        t.Stats.Health = 10;
        Assert.That(Cmd(t, "medicine_05").Ok, Is.True);
        Assert.That(t.Battle!.PlayerNextDamageFactor, Is.EqualTo(1));
        Assert.That(t.Battle.Log.Last(), Does.Not.Contain("下一次受到的直接伤害"));
    }

    /// <summary>用掉最后一份医疗物资：这一叠从随身消失（不留 0 个的物品）。抓 598 行。</summary>
    [Test]
    public void MedicalItem_LastOneUsed_StackGone()
    {
        var s = Medic(9216, "field_medkit", 5, out _, 0);
        E.DebugGive(s, "saline", 1);
        s.Stats.Health = 10;
        Assert.That(Cmd(s, "medicine_05").Ok, Is.True);
        Assert.That(s.Items.Any(i => i.ItemId == "saline"), Is.False);
        s.Battle!.Round += 3;
        Assert.That(E.CommandBlocked(s, BattleCommand.Of("medicine_05")), Does.StartWith("随身没有盐水剂"));
    }

    /// <summary>
    /// 医疗预览的消耗与目标（Q75：任一存活友方含自己，回血冷却按目标本人）：单人“目标：你自己（我方只有你一人）”；
    /// 多人点林昭“目标：林昭（可点我方任一在场成员；回血冷却按目标本人）”，没点写“自己”；执行战报写“目标林昭”。抓 255、257、504 行。
    /// </summary>
    [Test]
    public void MedicalPreview_TargetNotes_SoloAndParty()
    {
        var s = Medic(9217, "field_medkit", 5, out _);
        s.Stats.Health = 10;
        Assert.That(E.PreviewCommand(s, BattleCommand.Of("medicine_05")).Notes, Has.Some.EqualTo("消耗你随身盐水剂×1；目标：你自己（我方只有你一人）"));

        var p = MedicParty(9218, "field_medkit", 5);
        p.Actors[Lin].Stats.Health = 10;
        Assert.That(E.PreviewCommand(p, On("medicine_05", Lin)).Notes, Has.Some.EqualTo("消耗你随身盐水剂×1；目标：林昭（可点我方任一在场成员；回血冷却按目标本人）"));
        Assert.That(E.PreviewCommand(p, On("medicine_05", null)).Notes, Has.Some.StartsWith("消耗你随身盐水剂×1；目标：自己（"));
        Assert.That(E.ExecuteCommand(p, On("medicine_05", Lin)).Ok, Is.True);
        Assert.That(p.Battle!.Log.Last(), Does.Contain("：消耗盐水剂×1，目标林昭，"));
    }

    /// <summary>战地包扎预览按绷带效果（A06 medicine_01“完全按R04绷带效果”），效果放第一条。抓 260 行。</summary>
    [Test]
    public void FieldBandagePreview_BandageEffectFirst()
    {
        var s = Medic(9219, "field_medkit", 3, out _);
        s.Stats.Health = 10;
        var pv = E.PreviewCommand(s, BattleCommand.Of("medicine_01"));
        Assert.That(pv.EffectText, Is.Not.Null.And.Contains("健康"));
        Assert.That(pv.Notes[0], Is.EqualTo(pv.EffectText));
    }

    // ───────────── 快速装填（A06 firearms_05） ─────────────

    /// <summary>
    /// 快速装填“装入本武器完整弹仓可容数量”：弹仓满拒绝“弹仓已满”；差 1 发且随身有弹可用；随身没有弹拒绝“随身没有…”。
    /// 战斗中与战斗外（指令一览）同一措辞。抓 175—178 行。
    /// </summary>
    [Test]
    public void QuickReload_FullOrNoAmmo_Reasons_InAndOutOfBattle()
    {
        var s = Fight(9220, "worker");
        Ready(s);
        var p = Arm(s, "pistol");
        StandAt(s, 2);
        s.Skills["firearms"].Level = 5;
        p.LoadedAmmo = 8;
        E.DebugGive(s, "ammo_9mm", 5);
        Assert.That(E.CommandBlocked(s, BattleCommand.Of("firearms_05")), Is.EqualTo("弹仓已满"));
        p.LoadedAmmo = 7;
        Assert.That(E.CommandBlocked(s, BattleCommand.Of("firearms_05")), Is.Null);
        foreach (var a in s.Items.Where(i => i.ItemId == "ammo_9mm").ToList()) s.Items.Remove(a);
        Assert.That(E.CommandBlocked(s, BattleCommand.Of("firearms_05")), Is.EqualTo("随身没有9mm弹"));

        var o = New(9221);
        var op = E.CreateItems(o, "pistol", 1, ItemInstance.EquipOwner(Me, EquipSlots.Weapon1)).Single();
        o.Skills["firearms"].Level = 5;
        op.LoadedAmmo = 8;
        E.DebugGive(o, "ammo_9mm", 5);
        Assert.That(E.CompatibleCommands(o).Single(c => c.Action == "firearms_05").Reason, Is.EqualTo("弹仓已满"));
        op.LoadedAmmo = 7;
        Assert.That(E.CompatibleCommands(o).Single(c => c.Action == "firearms_05").Reason, Is.Null);
        foreach (var a in o.Items.Where(i => i.ItemId == "ammo_9mm").ToList()) o.Items.Remove(a);
        Assert.That(E.CompatibleCommands(o).Single(c => c.Action == "firearms_05").Reason, Is.EqualTo("随身没有9mm弹"));
    }

    /// <summary>快速装填随身不够装满时装随身全部：预览“弹仓1→4/8（随身…−3）”与说明；执行后弹仓 4、战报“装填…3发（弹仓4/8）”。抓 279—281、557 行。</summary>
    [Test]
    public void QuickReload_PartialFill_PreviewAndLog()
    {
        var s = Fight(9222, "worker");
        Ready(s);
        var p = Arm(s, "pistol");
        StandAt(s, 2);
        s.Skills["firearms"].Level = 5;
        p.LoadedAmmo = 1;
        E.DebugGive(s, "ammo_9mm", 3);
        var pv = E.PreviewCommand(s, BattleCommand.Of("firearms_05"));
        Assert.That(pv.AmmoText, Is.EqualTo("弹仓1→4/8（随身9mm弹−3）"));
        Assert.That(pv.Notes, Has.Some.EqualTo("装满本武器弹仓可容数量，本次不再另付通用装填"));
        Assert.That(Cmd(s, "firearms_05").Ok, Is.True);
        Assert.That(p.LoadedAmmo, Is.EqualTo(4));
        Assert.That(s.Battle!.Log.Last(), Does.Contain("装填9mm弹3发（弹仓4/8）"));

        // 随身足够时只补到弹仓上限（A06 firearms_05“装入本武器完整弹仓可容数量”）：5/8、随身 10 → 装 3 发、随身剩 7
        var t = Fight(9225, "worker");
        Ready(t);
        var tp = Arm(t, "pistol");
        StandAt(t, 2);
        t.Skills["firearms"].Level = 5;
        tp.LoadedAmmo = 5;
        E.DebugGive(t, "ammo_9mm", 10);
        Assert.That(E.PreviewCommand(t, BattleCommand.Of("firearms_05")).AmmoText, Is.EqualTo("弹仓5→8/8（随身9mm弹−3）"));
        Assert.That(Cmd(t, "firearms_05").Ok, Is.True);
        Assert.That(tp.LoadedAmmo, Is.EqualTo(8));
        Assert.That(t.Items.Where(i => i.ItemId == "ammo_9mm").Sum(i => i.Count), Is.EqualTo(7));
    }

    // ───────────── 格挡类技能（护身/反击架势/稳固/坚守） ─────────────

    /// <summary>
    /// 格挡系数（R03 Q69）：当前页不是盾 ×0.5，当前页是可付耐久的盾 ×0.4 并磨 1；预览写明，执行战报写盾耐久变化；结果段是“effect”。
    /// 已在防御时同类不叠（R03 Q69“格挡同类不乘叠”）。抓 195、196、308、311 行与 121 行原因。
    /// </summary>
    [Test]
    public void GuardSkills_PlainVsShieldFactor_PreviewLogWear()
    {
        var s = Fight(9223, "worker");
        Ready(s);
        s.Skills["unarmed"].Level = 5;
        s.Battle!.PlayerAp = 6;
        Assert.That(E.PreviewCommand(s, BattleCommand.Of("unarmed_05")).Notes, Has.Some.StartsWith("格挡1次：下一次受到的直接伤害×0.5"));
        E.DrainBattleFx();
        Assert.That(Cmd(s, "unarmed_05").Ok, Is.True);
        Assert.That(s.Battle.PlayerGuardFactor, Is.EqualTo(0.5));
        Assert.That(s.Battle.Log.Last(), Does.Contain("格挡1次（直接伤害×0.5）").And.Not.Contain("耐久"));
        Assert.That(Main(E.DrainBattleFx()).Single().Results.Single().Outcome, Is.EqualTo("effect"));
        Assert.That(E.CommandBlocked(s, BattleCommand.Of("unarmed_05")), Is.EqualTo("已在防御（不叠层）"));

        var t = Fight(9224, "worker");
        Ready(t);
        var sh = Arm(t, "wooden_shield");
        t.Skills["shield"].Level = 5;
        t.Battle!.PlayerAp = 6;
        var d0 = sh.Durability!.Value;
        Assert.That(E.PreviewCommand(t, BattleCommand.Of("shield_05")).Notes, Has.Some.StartsWith("格挡1次：下一次受到的直接伤害×0.4（盾类格挡：当前页木盾耐久−1）"));
        Assert.That(Cmd(t, "shield_05").Ok, Is.True);
        Assert.That(t.Battle.PlayerGuardFactor, Is.EqualTo(0.4));
        Assert.That(sh.Durability!.Value, Is.EqualTo(d0 - 1).Within(1e-9));
        Assert.That(t.Battle.Log.Last(), Does.Contain("格挡1次（直接伤害×0.4）").And.Contain($"木盾耐久{Fmt.N(d0)}→{Fmt.N(d0 - 1)}"));
    }

    /// <summary>
    /// 反击架势（A06 blade_05 反击×0.70；A05 守备短剑“反击×1.15”）：预览与战报写反击倍率 0.7×1.15；护身（unarmed_05 只格挡）不写反击、不改反击倍率。
    /// 抓 197、312、316 行。
    /// </summary>
    [Test]
    public void CounterStance_PreviewAndLogFactor_GuardOnlySkillHasNoCounter()
    {
        var s = Fight(9225, "worker");
        Ready(s);
        Arm(s, "guard_sword");
        s.Skills["blade"].Level = 5;
        var f = Fmt.T(0.70 * 1.15);
        Assert.That(E.PreviewCommand(s, BattleCommand.Of("blade_05")).Notes, Has.Some.EqualTo($"反击：受到直接攻击命中后，用守备短剑反击1次，伤害×{f}（反击不再触发反应）"));
        Assert.That(Cmd(s, "blade_05").Ok, Is.True);
        Assert.That(s.Battle!.Log.Last(), Does.Contain($"受到直接攻击命中后反击1次（伤害×{f}）"));

        var t = Fight(9226, "worker");
        Ready(t);
        t.Skills["unarmed"].Level = 5;
        t.Battle!.CounterFactor = 0;
        Assert.That(E.PreviewCommand(t, BattleCommand.Of("unarmed_05")).Notes, Has.None.Contains("反击"));
        Assert.That(Cmd(t, "unarmed_05").Ok, Is.True);
        Assert.That(t.Battle.Log.Last(), Does.Not.Contain("反击"));
        Assert.That(t.Battle.CounterFactor, Is.EqualTo(0));
    }

    /// <summary>
    /// 稳固（A06 shield_05：格挡1次且位移抵抗+25个百分点，2轮）：预览与战报写到第 r+2 回合末；护身不带位移抵抗（不写、不改）。抓 198、318、325 行。
    /// </summary>
    [Test]
    public void Steady_DisplaceResistTwoRounds_GuardOnlySkillHasNone()
    {
        var s = Fight(9227, "worker");
        Ready(s);
        Arm(s, "wooden_shield");
        s.Skills["shield"].Level = 5;
        var b = s.Battle!;
        var r = b.Round;
        Assert.That(E.PreviewCommand(s, BattleCommand.Of("shield_05")).Notes, Has.Some.EqualTo($"位移抵抗+25个百分点，到第{r + 2}回合末（被拉扯/击退判控制时生效，与装备位移抵抗取强）"));
        Assert.That(Cmd(s, "shield_05").Ok, Is.True);
        Assert.That((b.DisplaceResist, b.DisplaceUntilRound), Is.EqualTo((25.0, r + 2)));
        Assert.That(b.Log.Last(), Does.Contain($"位移抵抗+25个百分点到第{r + 2}回合末"));

        var t = Fight(9228, "worker");
        Ready(t);
        t.Skills["unarmed"].Level = 5;
        var tb = t.Battle!;
        Assert.That(E.PreviewCommand(t, BattleCommand.Of("unarmed_05")).Notes, Has.None.Contains("位移抵抗"));
        Assert.That(Cmd(t, "unarmed_05").Ok, Is.True);
        Assert.That(tb.Log.Last(), Does.Not.Contain("位移抵抗"));
        Assert.That(tb.DisplaceUntilRound, Is.LessThan(tb.Round), "没有位移抵抗状态");
    }

    /// <summary>
    /// 坚守（A06 shield_10：自己格挡2次；其他队员下一次直接伤害×0.8，1轮）：独自一人时预览与战报写没有队友可受减伤（B7-5）；
    /// 护身不写这一条。有队友时队友得 ×0.8 到第 r+1 回合末，战报写队友名，队友段是“effect”。抓 199、327、332、342 行。
    /// </summary>
    [Test]
    public void Stand_SoloNoOthers_PartyOthersCovered()
    {
        var s = Fight(9229, "worker");
        Ready(s);
        Arm(s, "wooden_shield");
        s.Skills["shield"].Level = 10;
        s.Battle!.PlayerAp = 6;
        Assert.That(E.PreviewCommand(s, BattleCommand.Of("shield_10")).Notes, Has.Some.EqualTo("其他队员的减伤：现在只有你一人，没有效果"));
        Assert.That(Cmd(s, "shield_10").Ok, Is.True);
        Assert.That(s.Battle.Log.Last(), Does.Contain("没有其他队员可受减伤"));
        Assert.That((s.Battle.PlayerGuardCharges, s.Battle.Me.CoverFactor), Is.EqualTo((2, 1.0)));

        var u = Fight(9230, "worker");
        Ready(u);
        u.Skills["unarmed"].Level = 5;
        Assert.That(E.PreviewCommand(u, BattleCommand.Of("unarmed_05")).Notes, Has.None.Contains("其他队员"));
        Assert.That(Cmd(u, "unarmed_05").Ok, Is.True);
        Assert.That(u.Battle!.Log.Last(), Does.Not.Contain("队员"));

        var p = Party(9231, 2, "worker");
        var pb = p.Battle!;
        pb.Allies = new List<string> { Me, Lin };
        TurnOf(p, Me);
        ArmActor(p, Me, "wooden_shield", 1);
        p.Skills["shield"].Level = 10;
        var r = pb.Round;
        E.DrainBattleFx();
        Assert.That(Cmd(p, "shield_10").Ok, Is.True);
        Assert.That((pb.Fighter(Lin).CoverFactor, pb.Fighter(Lin).CoverUntilRound), Is.EqualTo((0.8, r + 1)));
        Assert.That(pb.Log.Last(), Does.Contain($"林昭下一次直接伤害×0.8（到第{r + 1}回合末）"));
        var ev = Main(E.DrainBattleFx()).Single();
        Assert.That(ev.Results.Single(x => x.Target.Id == Lin).Outcome, Is.EqualTo("effect"));
    }

    // ───────────── 保护 / 掩护（A06 shield_03/07；Q106） ─────────────

    /// <summary>两人：你 1 号（武器Ⅰ木盾，盾 10 级）、林昭 2 号；轮到你。</summary>
    private GameState ShieldParty(uint seed, out ItemInstance shield)
    {
        var s = Party(seed, 2, "worker");
        var b = s.Battle!;
        b.Allies = new List<string> { Me, Lin };
        TurnOf(s, Me);
        shield = ArmActor(s, Me, "wooden_shield", 1);
        s.Skills["shield"].Level = 10;
        return s;
    }

    /// <summary>
    /// 保护（shield_03）：护住相邻队友，战报写“目标林昭”“护住林昭（…到你下次正常行动前）”，不给任何人减伤；有效保护盾耐久−1（A05 盾执行保护指令磨损1）。
    /// 掩护（shield_07）：双方下一次直接伤害×0.8 到第 r+2 回合末（A06 2轮）。抓 407、415、416、425、426 行。
    /// </summary>
    [Test]
    public void Protect_NoCover_Wears_Cover_BothPointEightTwoRounds()
    {
        var s = ShieldParty(9232, out var sh);
        var b = s.Battle!;
        var d0 = sh.Durability!.Value;
        Assert.That(E.ExecuteCommand(s, On("shield_03", Lin)).Ok, Is.True);
        Assert.That(b.Protections[Me], Is.EqualTo(Lin));
        Assert.That((b.Fighter(Me).CoverFactor, b.Fighter(Lin).CoverFactor), Is.EqualTo((1.0, 1.0)), "保护不带减伤");
        Assert.That(sh.Durability!.Value, Is.EqualTo(d0 - 1).Within(1e-9));
        var log = b.Log.Last();
        Assert.That(log, Does.Contain("目标林昭").And.Contain("护住林昭（接下它下一次单体直接攻击，到你下次正常行动前）").And.Not.Contain("双方"));

        var t = ShieldParty(9233, out _);
        var tb = t.Battle!;
        var r = tb.Round;
        Assert.That(E.ExecuteCommand(t, On("shield_07", Lin)).Ok, Is.True);
        Assert.That((tb.Fighter(Me).CoverFactor, tb.Fighter(Me).CoverUntilRound), Is.EqualTo((0.8, r + 2)));
        Assert.That((tb.Fighter(Lin).CoverFactor, tb.Fighter(Lin).CoverUntilRound), Is.EqualTo((0.8, r + 2)));
        Assert.That(tb.Log.Last(), Does.Contain($"双方下一次直接伤害×0.8（到第{r + 2}回合末）"));
    }

    /// <summary>
    /// 保护/掩护/协同预览（A06 目标“相邻友方/其他友方”）：选了写“目标：林昭（2号位）”，没选写要选谁；掩护写双方×0.8 与到期轮；
    /// 协同写 AP+1、上限 6、不加追击；经验说明分盾/指挥。单人时说明里有“没有可保护的队友”。抓 218、234—240 行与 79 行原因。
    /// </summary>
    [Test]
    public void ProtectCoverCoordinatePreview_Notes()
    {
        var s = ShieldParty(9234, out _);
        var r = s.Battle!.Round;
        var p3 = E.PreviewCommand(s, On("shield_03", Lin)).Notes;
        Assert.That(p3, Has.Some.EqualTo("目标：林昭（2号位）"));
        Assert.That(p3, Has.Some.EqualTo("接下目标下一次单体直接攻击（范围攻击、流血不转移），到你下次正常行动开始"));
        Assert.That(p3, Has.Some.EqualTo("有效拦截时得盾经验（每场每目标一次）"));
        Assert.That(p3, Has.None.Contains("AP+"), "保护不加行动点（A06 shield_03 无 AP 加成）");
        Assert.That(E.PreviewCommand(s, On("shield_03", null)).Notes, Has.Some.EqualTo("目标：与你相邻的一名队友"));
        Assert.That(E.PreviewCommand(s, On("shield_07", Lin)).Notes, Has.Some.EqualTo($"接下目标下一次单体直接攻击（范围攻击、流血不转移），到你下次正常行动开始；双方下一次受到的直接伤害×0.8，到第{r + 2}回合末"));

        var c = Party(9235, 2, "worker");
        c.Battle!.Allies = new List<string> { Lin, Me };
        TurnOf(c, Me);
        ArmActor(c, Me, "signal_whistle", 2);
        c.Skills["command"].Level = 7;
        var p7 = E.PreviewCommand(c, On("command_07", Lin)).Notes;
        Assert.That(p7, Has.Some.EqualTo("目标：林昭（1号位）"));
        Assert.That(p7, Has.Some.EqualTo("目标下一次正常行动 AP+1（总数不超过6，不加追击）"));
        Assert.That(p7, Has.Some.EqualTo("增益被实际用掉才得指挥经验（每场每目标每技能一次）"));
        Assert.That(p7, Has.None.Contains("接下目标"));
        Assert.That(E.PreviewCommand(c, On("command_07", null)).Notes, Has.Some.EqualTo("目标：一名其他队友"));

        var solo = Fight(9236, "worker");
        Ready(solo);
        Arm(solo, "wooden_shield");
        solo.Skills["shield"].Level = 10;
        Assert.That(E.CommandBlocked(solo, BattleCommand.Of("shield_03")), Is.EqualTo("只有你一人在场，没有可保护的队友"));
        Assert.That(E.PreviewCommand(solo, BattleCommand.Of("shield_07")).Notes, Has.Some.EqualTo("只有你一人在场，没有可保护的队友"));
    }

    /// <summary>需要队友的技能在战斗外按同行人数判断（B10a 修正）：独自一人写原因，有同行者不写。抓 175 行条件与比较变异。</summary>
    [Test]
    public void NeedsAllies_OutOfBattle_ByPartySize()
    {
        var o = New(9237);
        E.CreateItems(o, "wooden_shield", 1, ItemInstance.EquipOwner(Me, EquipSlots.Weapon1));
        o.Skills["shield"].Level = 3;
        Assert.That(E.CompatibleCommands(o).Single(c => c.Action == "shield_03").Reason, Is.EqualTo("只有你一人在场，没有可保护的队友"));
        E.DebugQuickParty(o, 2);
        Assert.That(E.CompatibleCommands(o).Single(c => c.Action == "shield_03").Reason, Is.Null);
    }

    /// <summary>医疗技能在战斗外按随身物资写原因（A06 医疗消耗随身物资）。抓 176 行。</summary>
    [Test]
    public void MedicalSkill_OutOfBattle_MissingItemReason()
    {
        var o = New(9238);
        E.CreateItems(o, "field_medkit", 1, ItemInstance.EquipOwner(Me, EquipSlots.Weapon1));
        o.Skills["medicine"].Level = 3;
        foreach (var g in o.Items.Where(i => i.ItemId == "medical_gauze").ToList()) o.Items.Remove(g);
        Assert.That(E.CompatibleCommands(o).Single(c => c.Action == "medicine_03").Reason, Is.EqualTo("随身没有医用纱布"));
        E.DebugGive(o, "medical_gauze", 1);
        Assert.That(E.CompatibleCommands(o).Single(c => c.Action == "medicine_03").Reason, Is.Null);
    }

    // ───────────── 整队（Q105）预览与换位 ─────────────

    /// <summary>三人：林昭 1 号、你 2 号（武器Ⅱ信号哨、指挥 3 级）、周砚 3 号；轮到你。</summary>
    private GameState ReorderScene(uint seed, out ItemInstance whistle)
    {
        var s = Party(seed, 3, "worker");
        var b = s.Battle!;
        b.Allies = new List<string> { Lin, Me, Zhou };
        TurnOf(s, Me);
        whistle = ArmActor(s, Me, "signal_whistle", 2);
        s.Skills["command"].Level = 3;
        return s;
    }

    /// <summary>
    /// 整队预览（Q105“前两步选择只做预览……选齐后展示A/B姓名与前后槽号、施术成本”）：没选写先点 A；只选 A 写“已选林昭（1号位）”；
    /// A 不在场也不出交换预览；选齐写“整队：林昭 1号位→2号位，你 2号位→1号位”放第一条；成本说明写 2AP、冷却1回合、信号哨耐久−1。
    /// 执行后两人都有换位动作。抓 224、228、230—232、390、391 行。
    /// </summary>
    [Test]
    public void ReorderPreview_Steps_AndExecuteMovesBoth()
    {
        var s = ReorderScene(9239, out _);
        var me = E.ActorDisplayName(s, Me);
        var none = E.PreviewCommand(s, new BattleCommand { Action = "command_03" });
        Assert.That(none.Notes, Has.Some.EqualTo("先点要交换的第一名友方A（可以是自己），再点与A相邻的友方B，然后确认"));
        Assert.That(none.EffectText, Is.Null);
        var one = E.PreviewCommand(s, new BattleCommand { Action = "command_03", AllyTarget = Lin });
        Assert.That(one.Notes, Has.Some.EqualTo("已选林昭（1号位）；再点与他相邻的一名友方"));
        Assert.That(one.EffectText, Is.Null);
        var off = E.PreviewCommand(s, new BattleCommand { Action = "command_03", AllyTarget = Lin, AllyTarget2 = Shen });
        Assert.That(off.EffectText, Is.Null, "B 不在场，不出交换预览");
        var offA = E.PreviewCommand(s, new BattleCommand { Action = "command_03", AllyTarget = Shen, AllyTarget2 = Lin });
        Assert.That(offA.EffectText, Is.Null, "A 不在场，不出交换预览");
        var both = E.PreviewCommand(s, new BattleCommand { Action = "command_03", AllyTarget = Lin, AllyTarget2 = Me });
        Assert.That(both.EffectText, Is.EqualTo($"整队：林昭 1号位→2号位，{me} 2号位→1号位"));
        Assert.That(both.Notes[0], Is.EqualTo(both.EffectText));
        Assert.That(both.Notes, Has.Some.EqualTo($"由你付一次成本（2AP、体力、冷却1回合、信号哨耐久−1），两人不另付调整站位费；只交换号位，不多激活、不改行动顺序、不改战前编队；换位无经验，也不触发架矛迎击"));
        Assert.That(both.Notes, Has.Some.EqualTo("交换后两人的技能仍按真实号位判断（可能暂时站在不适合自己武器的号位）"));
        E.DrainBattleFx();
        var r = E.ExecuteCommand(s, new BattleCommand { Action = "command_03", AllyTarget = Lin, AllyTarget2 = Me, AllyTargetPos = 1, AllyTarget2Pos = 2 });
        Assert.That(r.Ok, Is.True, r.Reason);
        Assert.That(s.Battle!.Allies.Take(2), Is.EqualTo(new[] { Me, Lin }));
        Assert.That(s.Battle.Log.Last(), Does.Contain("换位无经验"));
        var ev = Main(E.DrainBattleFx()).Single();
        Assert.That(ev.Results.Single(x => x.Target.Id == Lin).Moved, Is.True);
        Assert.That(ev.Results.Single(x => x.Target.Id == Me).Moved, Is.True);
    }

    // ───────────── 指挥技能预览（A06 command_01/05/10；A05 战术电台、便携指挥终端） ─────────────

    /// <summary>
    /// 鼓舞/稳心/总攻令预览：单人“目标：你自己（我方只有你一人）”；鼓舞写 +6 到第 r+2 回合末（电台 r+3）；稳心写恐慌前→后；
    /// 总攻令写 ×1.15 到第 r+2 回合末；器具耐久与经验说明（未满级写“增益被实际用掉才得指挥经验+2”，满级写已满）；冷却说明 CD1 → 第 r+2 回合起可再用。
    /// 抓 244—250、285 行。
    /// </summary>
    [Test]
    public void CommandPreview_SoloNotes()
    {
        var s = Fight(9240, "worker");
        Ready(s);
        Arm(s, "kitchen_knife");
        Arm2(s, "signal_whistle");
        StandAt(s, 2);
        s.Skills["command"].Level = 5;
        var b = s.Battle!;
        b.Panic = 30;
        var r = b.Round;
        var p1 = E.PreviewCommand(s, BattleCommand.Of("command_01")).Notes;
        Assert.That(p1, Has.Some.EqualTo("目标：你自己（我方只有你一人）"));
        Assert.That(p1, Has.Some.EqualTo($"下一次攻击命中能力+6，到第{r + 2}回合末"));
        Assert.That(p1, Has.Some.EqualTo("信号哨耐久−1（有效时）；增益被实际用掉才得指挥经验+2（每场每技能一次）"));
        Assert.That(p1, Has.Some.EqualTo($"冷却1回合：本回合用后第{r + 2}回合起可再用"));
        Assert.That(p1, Has.None.Contains("恐慌").And.None.Contains("伤害×").And.None.Contains("预判"));
        var p5 = E.PreviewCommand(s, BattleCommand.Of("command_05")).Notes;
        Assert.That(p5, Has.Some.EqualTo("恐慌30.00→22.00；同目标每大回合一次"));
        Assert.That(p5, Has.None.Contains("命中能力"));
        s.Skills["command"].Level = 10;
        var p10 = E.PreviewCommand(s, BattleCommand.Of("command_10")).Notes;
        Assert.That(p10, Has.Some.EqualTo($"下一次攻击伤害×1.15，到第{r + 2}回合末；同名不叠"));
        Assert.That(p10, Has.Some.StartsWith("信号哨耐久−1（有效时）；").And.None.Contains("增益被实际用掉才得指挥经验+2"));
        Assert.That(p10, Has.None.Contains("命中能力"));

        var t = Fight(9241, "worker");
        Ready(t);
        Arm2(t, "field_radio");
        StandAt(t, 2);
        t.Skills["command"].Level = 10;
        var rt = t.Battle!.Round;
        Assert.That(E.PreviewCommand(t, BattleCommand.Of("command_01")).Notes, Has.Some.EqualTo($"下一次攻击命中能力+6，到第{rt + 3}回合末"));
        Assert.That(E.PreviewCommand(t, BattleCommand.Of("command_10")).Notes, Has.Some.EqualTo($"下一次攻击伤害×1.15，到第{rt + 3}回合末；同名不叠"));
    }

    /// <summary>便携指挥终端“预判”（A05 每场首次号令+4）：本场还没号令时预览写预判，号令过后不再写。抓 249 行。</summary>
    [Test]
    public void CommandPreview_TerminalFirstCommandNoteOnlyBeforeFirstUse()
    {
        var s = Fight(9242, "worker");
        Ready(s);
        Arm2(s, "command_terminal");
        StandAt(s, 2);
        s.Skills["command"].Level = 10;
        s.Battle!.PlayerAp = 9;
        Assert.That(E.PreviewCommand(s, BattleCommand.Of("command_01")).Notes, Has.Some.EqualTo("便携指挥终端“预判”：本场首次号令，另加下一击命中能力+4"));
        Assert.That(Cmd(s, "command_10").Ok, Is.True);
        Assert.That(E.PreviewCommand(s, BattleCommand.Of("command_01")).Notes, Has.None.Contains("预判"));
    }

    /// <summary>指挥预览的目标说明（Q75 任一友方含自己）：多人时点林昭写“目标：林昭（可点我方任一在场成员）”，不点写“目标：自己（…）”。抓 244/245 行。</summary>
    [Test]
    public void CommandPreview_PartyTargetNotes()
    {
        var s = Party(9243, 2, "worker");
        s.Battle!.Allies = new List<string> { Lin, Me };
        TurnOf(s, Me);
        ArmActor(s, Me, "signal_whistle", 2);
        s.Skills["command"].Level = 3;
        Assert.That(E.PreviewCommand(s, On("command_01", Lin)).Notes, Has.Some.EqualTo("目标：林昭（可点我方任一在场成员）"));
        Assert.That(E.PreviewCommand(s, On("command_01", null)).Notes, Has.Some.EqualTo("目标：自己（可点我方任一在场成员）"));
    }

    /// <summary>稳心结果字写真实减少量（Q694/Q729）：恐慌 30 → 22 写“恐慌−8.00”，恐慌 5 → 0 写“恐慌−5.00”；受益者段为“effect”。抓 463、492、494 行。</summary>
    [Test]
    public void Calm_FxChangeIsRealPanicCut()
    {
        var s = Fight(9244, "worker");
        Ready(s);
        Arm2(s, "signal_whistle");
        StandAt(s, 2);
        s.Skills["command"].Level = 5;
        s.Battle!.Panic = 30;
        E.DrainBattleFx();
        Assert.That(Cmd(s, "command_05").Ok, Is.True);
        Assert.That(s.Battle.Log.Last(), Does.Contain("恐慌30.00→22.00"));
        var r = Main(E.DrainBattleFx()).Single().Results.Single();
        Assert.That((r.Outcome, r.Changes.ToList()), Is.EqualTo(("effect", new List<string> { GameEngine.PanicCutText(8) })));

        var t = Fight(9245, "worker");
        Ready(t);
        Arm2(t, "signal_whistle");
        StandAt(t, 2);
        t.Skills["command"].Level = 5;
        t.Battle!.Panic = 5;
        E.DrainBattleFx();
        Assert.That(Cmd(t, "command_05").Ok, Is.True);
        Assert.That(Main(E.DrainBattleFx()).Single().Results.Single().Changes, Is.EqualTo(new[] { GameEngine.PanicCutText(5) }));
    }

    // ───────────── 医疗使濒死队友脱离濒死；出手快照收尾 ─────────────

    /// <summary>
    /// R09 §6/§8：战斗医疗技能实际回血使濒死者（H=0、未确死）脱离濒死 → 本条治疗战报之后写“林昭脱离濒死状态。”；
    /// 医疗结果段并入生命结果字“脱离濒死”（B47b Q691，常量 revived）。配对：队友没在濒死（H&gt;0）→ 不写脱离句、结果段无生命字。
    /// 抓 563 行（WriteLifeNotes）与 564 行（医疗出手快照收尾 Dispose）。
    /// </summary>
    [Test]
    public void MedicalHealOnDyingAlly_LeaveDyingNoteAfterLog_FxLifeRevived()
    {
        var s = MedicParty(9240, "field_medkit", 5);
        var lin = s.Actors[Lin];
        lin.Stats.Health = 0;
        Assert.That(E.IsDying(s, lin), Is.True, "前置：林昭濒死");
        var heal = 0.8 * (1 + 0.02 * 5);   // A06 medicine_05：0.8×(1+0.02×5)
        E.DrainBattleFx();
        var n0 = s.Battle!.Log.Count;
        Assert.That(E.ExecuteCommand(s, On("medicine_05", Lin)).Ok, Is.True);
        Assert.That(lin.Stats.Health, Is.EqualTo(heal).Within(1e-9));
        var added = s.Battle.Log.Skip(n0).ToList();
        Assert.That(added.Count, Is.GreaterThanOrEqualTo(2));
        var at = added.FindIndex(l => l.Contains("稳定伤势（随行医疗包）"));
        Assert.That(at, Is.GreaterThanOrEqualTo(0));
        Assert.That(added[at + 1], Does.EndWith("林昭脱离濒死状态。"), "R09 §8 正式句，接在治疗战报之后");
        // 医疗出手到本条战报写完即结束：随后的防御是独立的一次出手（R03 §Q571 防御短反馈），不挂在医疗出手之下
        Assert.That(E.ExecuteCommand(s, BattleCommand.Of("guard")).Ok, Is.True);
        var all = E.DrainBattleFx();
        var ev = Main(all).Single();
        Assert.That(ev.Results.Count, Is.EqualTo(1));
        var r = ev.Results[0];
        Assert.That((r.Target.Id, r.Life), Is.EqualTo((Lin, "revived")));
        Assert.That(r.Amount, Is.EqualTo(heal).Within(1e-9));
        var shorts = all.Where(x => x.Short).ToList();
        Assert.That(shorts.Count, Is.EqualTo(1));
        Assert.That(shorts[0].ParentSeq, Is.Null);

        var t = MedicParty(9241, "field_medkit", 5);
        var lt = t.Actors[Lin];
        lt.Stats.Health = 5;
        E.DrainBattleFx();
        var m0 = t.Battle!.Log.Count;
        Assert.That(E.ExecuteCommand(t, On("medicine_05", Lin)).Ok, Is.True);
        Assert.That(lt.Stats.Health, Is.EqualTo(5 + heal).Within(1e-9));
        Assert.That(t.Battle.Log.Skip(m0).Any(l => l.Contains("脱离濒死")), Is.False);
        var te = Main(E.DrainBattleFx()).Single();
        Assert.That(te.Results.Count, Is.EqualTo(1));
        Assert.That(te.Results[0].Life, Is.Null);
    }

    /// <summary>
    /// S14 Q573：出手结果段写这次出手真实新施加的状态名（出手结束时比对）。护身（unarmed_05）结束后你的结果段状态为“防御”。
    /// 抓 565 行（支援出手快照收尾 Dispose）。
    /// </summary>
    [Test]
    public void SupportSkillFx_StampsNewlyAppliedState()
    {
        var s = Fight(9242, "worker");
        Ready(s);
        s.Skills["unarmed"].Level = 5;
        s.Battle!.PlayerAp = 6;
        E.DrainBattleFx();
        Assert.That(Cmd(s, "unarmed_05").Ok, Is.True);
        var ev = Main(E.DrainBattleFx()).Single();
        Assert.That(ev.Results.Count, Is.EqualTo(1));
        Assert.That(ev.Results[0].States, Is.EqualTo(new[] { "防御" }));
    }

    /// <summary>
    /// R03 §Q615 表：快速装填按装填类原地动作给短反馈（只记行动者、不播支援特写）。抓 552 行。
    /// </summary>
    [Test]
    public void QuickReload_ShortFeedbackOnly()
    {
        var s = Fight(9243, "worker");
        Ready(s);
        var p = Arm(s, "pistol");
        StandAt(s, 2);
        s.Skills["firearms"].Level = 5;
        p.LoadedAmmo = 1;
        E.DebugGive(s, "ammo_9mm", 3);
        E.DrainBattleFx();
        Assert.That(Cmd(s, "firearms_05").Ok, Is.True);
        var fx = E.DrainBattleFx();
        Assert.That(fx.Count, Is.EqualTo(1));
        Assert.That((fx[0].Short, fx[0].Name, fx[0].Actor.Id), Is.EqualTo((true, "快速装填", Me)));
    }

    /// <summary>
    /// R03 §4“只可一名保护者接一名目标”：林昭已在保护周砚时，你保护周砚被拒“目标已有保护者”；配对：没人保护周砚时可以。
    /// 保护（无掩护）的受益只记目标，不记施术者自己。抓 131 行（只排除自己的旧保护）与 414 行。
    /// </summary>
    [Test]
    public void Protect_TargetAlreadyProtectedByOther_Blocked_FxOnlyTarget()
    {
        var s = Party(9245, 3, "worker");
        var b = s.Battle!;
        b.Allies = new List<string> { Lin, Me, Zhou };
        TurnOf(s, Me);
        ArmActor(s, Me, "wooden_shield", 1);
        s.Skills["shield"].Level = 10;
        b.Protections[Lin] = Zhou;
        Assert.That(E.CommandBlocked(s, On("shield_03", Zhou)), Is.EqualTo("目标已有保护者"));
        b.Protections.Remove(Lin);
        Assert.That(E.CommandBlocked(s, On("shield_03", Zhou)), Is.Null);
        E.DrainBattleFx();
        Assert.That(E.ExecuteCommand(s, On("shield_03", Zhou)).Ok, Is.True);
        var ev = Main(E.DrainBattleFx()).Single();
        Assert.That(ev.Results.Count, Is.EqualTo(1));
        Assert.That(ev.Results[0].Target.Id, Is.EqualTo(Zhou));
    }

    /// <summary>
    /// 医疗预览（R09 §6、B47b）：目标濒死且这次实际回血时，效果栏追加“脱离濒死状态”；没在濒死不写。抓 267 行。
    /// </summary>
    [Test]
    public void MedicalPreview_DyingTarget_RevivePart()
    {
        var s = MedicParty(9246, "field_medkit", 5);
        var lin = s.Actors[Lin];
        lin.Stats.Health = 0;
        var heal = 0.8 * (1 + 0.02 * 5);   // A06 medicine_05：0.8×(1+0.02×5)
        Assert.That(E.PreviewCommand(s, On("medicine_05", Lin)).EffectText,
            Is.EqualTo($"健康{Fmt.N(0)}→{Fmt.N(heal)}（共享绷带60分钟回血冷却）；脱离濒死状态"));
        lin.Stats.Health = 5;
        Assert.That(E.PreviewCommand(s, On("medicine_05", Lin)).EffectText,
            Is.EqualTo($"健康{Fmt.N(5)}→{Fmt.N(5 + heal)}（共享绷带60分钟回血冷却）"));
    }
}
