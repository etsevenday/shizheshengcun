using System.Reflection;
using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 B10a（需求 R114—R118）：策划 649f8f4 答复 Q101—Q106 的多人战斗规则实装。
/// 出处 R03/A06/A09/R08/S01/S14「Q101—Q106 本轮章」与 A10 验收表 MP-Q101/102、MP-Q103a—c、MP-Q104a—c、MP-Q105a—c、MP-Q106。
/// </summary>
public class B10aMultiplayerQTests : BattleTestBase
{
    private const string Me = GameState.PlayerId;
    private const string Lin = "npc_lin_zhao";
    private const string Zhou = "npc_zhou_yan";
    private const string Shen = "npc_shen_he";

    /// <summary>开发入口组出 n 人队（C4），在农野荒地对给定敌人接敌。</summary>
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
        s.Encounter = new PendingEncounter { LocationId = "field", EnemyIds = ids, Source = "explore", Minute = s.GameMinute };
        Assert.That(E.Engage(s).Ok, Is.True);
        return s;
    }

    /// <summary>让某名我方成员本轮先行动（只用于测试）。</summary>
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
    }

    /// <summary>给某名成员的武器页放一把武器并切到这一页（只布置场景）。</summary>
    private ItemInstance ArmActor(GameState s, string actor, string weaponId, int page = 1)
    {
        var slot = ItemInstance.EquipOwner(actor, page == 1 ? EquipSlots.Weapon1 : EquipSlots.Weapon2);
        foreach (var old in s.Items.Where(i => i.Owner == slot).ToList()) old.Owner = ItemInstance.CarriedOwner(actor);
        var it = E.CreateItems(s, weaponId, 1, slot).Single();
        s.Battle!.Fighter(actor).ActiveWeaponSlot = page;
        return it;
    }

    private void SetBrace(GameState s, string actor, int charges)
    {
        var f = s.Battle!.Fighter(actor);
        f.BraceCharges = charges;
        f.BraceFactor = 1;
        f.BracePage = 1;
    }

    private void TryBrace(GameState s, BattleEnemy mover) =>
        typeof(GameEngine).GetMethod("TryBrace", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(E, new object[] { s, s.Battle!, mover });

    private string AttackXp(GameState s, string actor, string skill, params BattleEnemy[] targets)
    {
        using (E.FocusOn(s, actor))
            return (string)typeof(GameEngine).GetMethod("AttackXp", BindingFlags.NonPublic | BindingFlags.Instance)!
                .Invoke(E, new object[] { s, skill, targets })!;
    }

    private void EnemyActivation(GameState s, BattleEnemy e) =>
        typeof(GameEngine).GetMethod("EnemyActivation", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(E, new object[] { s, e, false });

    private void Attack(GameState s, BattleEnemy e, double factor = 1.0) =>
        typeof(GameEngine).GetMethod("DoAttackStep", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(E, new object[] { s, s.Battle!, e, E.StepOf(e), false, factor, false, false });

    // ───────────── MP-Q101/102：群体恐慌与共用受惊随机数 ─────────────

    /// <summary>MP-Q101：嚎叫（未限定单体）给事件时在场全部存活我方各加原值，不在场的不加，不抽目标随机；到上限夹紧。</summary>
    [Test]
    public void MP_Q101_HowlAddsOriginalValueToAllOnField()
    {
        var s = Party(10101, 3, "howler");
        var b = s.Battle!;
        var e = Enemy(s, 1);
        Assert.That(e.Intent, Is.EqualTo("howl"));
        b.Allies = new List<string> { Me, Lin };   // 周砚不在场（模拟已倒下/离场）
        b.Fighter(Me).Panic = 10;
        b.Fighter(Lin).Panic = 98;
        b.Fighter(Zhou).Panic = 20;
        e.RoundApUsed = 0;
        var rng = s.Rng.State;
        EnemyActivation(s, e);
        Assert.That(b.Fighter(Me).Panic, Is.EqualTo(14).Within(1e-9), "原值+4");
        Assert.That(b.Fighter(Lin).Panic, Is.EqualTo(100).Within(1e-9), "按上限夹紧");
        Assert.That(b.Fighter(Zhou).Panic, Is.EqualTo(20).Within(1e-9), "不在场的不加");
        Assert.That(s.Rng.State, Is.EqualTo(rng), "不抽目标随机");
        Assert.That(b.Log.Last(), Does.Contain("你的恐慌+4").And.Contain("林昭恐慌+4"));
    }

    /// <summary>MP-Q102：全队共用本次主惊吓源的 u，每人按自己的入战精神 M 算 p，u&lt;p 才受惊；读档不重判、不多抽随机。</summary>
    [Test]
    public void MP_Q102_SharedU_PerActorMoraleP_NoReroll()
    {
        var s = New(10102);
        E.DebugQuickParty(s, 3);
        s.Actors[Lin].Stats.Morale = 0;
        s.Actors[Zhou].Stats.Morale = 100;
        E.DebugTeleport(s, "field");
        s.GameMinute = At(2, 9, 0);
        s.SpawnNextCheck = int.MaxValue;
        s.PatrolNextCheck = int.MaxValue;
        s.WorldEnemies.Clear();
        var w = E.DebugSpawnEnemy(s, "shambler", "field");
        s.Encounter = new PendingEncounter { LocationId = "field", EnemyIds = new List<int> { w.Id }, Source = "explore", Minute = s.GameMinute };
        Assert.That(E.Engage(s).Ok, Is.True);
        var b = s.Battle!;
        var u = b.Fighter(Me).FearRoll;
        Assert.That(w.FearRoll, Is.EqualTo(u), "u 存在主惊吓源世界敌人上");
        var q = E.Enemy("shambler").FearChance;
        foreach (var id in new[] { Me, Lin, Zhou })
        {
            var f = b.Fighter(id);
            Assert.That(f.FearRoll, Is.EqualTo(u), $"{id} 共用同一 u");
            Assert.That(f.FearChance, Is.EqualTo(E.FearChance(q, f.FearMorale, "explore")).Within(1e-12), $"{id} 按自己的 M 算 p");
            Assert.That(f.Frightened, Is.EqualTo(u < f.FearChance), $"{id} u<p 才受惊");
        }
        Assert.That(b.Fighter(Lin).FearChance, Is.GreaterThan(b.Fighter(Zhou).FearChance), "精神低的受惊率高");
        Assert.That(b.Fighter(Zhou).FearChance, Is.EqualTo(0).Within(1e-12), "精神 100 不会受惊");
        var back = SaveSerializer.Deserialize(SaveSerializer.Serialize(s), Cfg);
        foreach (var id in new[] { Me, Lin, Zhou })
            Assert.That((back.Battle!.Fighter(id).Frightened, back.Battle.Fighter(id).FearChance), Is.EqualTo((b.Fighter(id).Frightened, b.Fighter(id).FearChance)), "同 battleId 读档不重判");
        Assert.That(back.Rng.State, Is.EqualTo(s.Rng.State));
    }

    // ───────────── MP-Q103a—c：迎击从前起顺延，每事件只一人 ─────────────

    /// <summary>三人各架木矛的场面：你 1 号、林昭 2 号、周砚 3 号；敌人在 1 号位前移。</summary>
    private GameState BraceScene(uint seed, out BattleEnemy z, out ItemInstance mySpear, out ItemInstance linSpear)
    {
        var s = Party(seed, 3, "shambler");
        var b = s.Battle!;
        b.Allies = new List<string> { Me, Lin, Zhou };
        mySpear = ArmActor(s, Me, "wooden_spear");
        linSpear = ArmActor(s, Lin, "wooden_spear");
        ArmActor(s, Zhou, "wooden_spear");
        z = Enemy(s, 1);
        z.Slot = "1";
        return s;
    }

    /// <summary>MP-Q103a：最前者没有架矛/来源不合法可顺延到后排第一名合法者，只他扣 1 次；其他人不扣、不出手。</summary>
    [Test]
    public void MP_Q103a_FirstLegalFromFront_OnlyHeConsumes()
    {
        var s = BraceScene(10103, out var z, out var mySpear, out _);
        var b = s.Battle!;
        // 你在 1 号位没有架矛；林昭、周砚都架着 → 林昭（2 号）迎击
        SetBrace(s, Lin, 1);
        SetBrace(s, Zhou, 1);
        Force(s, AtLeast(0.999));   // 迎击未中，也不再递补第二人
        TryBrace(s, z);
        Assert.That((b.Fighter(Lin).BraceCharges, b.Fighter(Zhou).BraceCharges), Is.EqualTo((0, 1)), "只有实际迎击的林昭扣 1 次");
        Assert.That(b.Log.Count(l => l.Contains("迎击")), Is.EqualTo(1));
        Assert.That(b.Log.Last(), Does.Contain("林昭迎击"));

        // 你在 1 号位架着矛但矛耐久不足（不合法）→ 顺延到林昭；你的次数不扣
        var t = BraceScene(10104, out var z2, out var my2, out _);
        var tb = t.Battle!;
        my2.Durability = 0;
        SetBrace(t, Me, 2);
        SetBrace(t, Lin, 1);
        SetBrace(t, Zhou, 1);
        Force(t, Below(0.001), AtLeast(0.999), Any());
        TryBrace(t, z2);
        Assert.That((tb.Fighter(Me).BraceCharges, tb.Fighter(Lin).BraceCharges, tb.Fighter(Zhou).BraceCharges), Is.EqualTo((2, 0, 1)));
        Assert.That(tb.Log.Any(l => l.Contains("你的迎击没有出手") && l.Contains("耐久不足")), Is.True);
        Assert.That(tb.Log.Last(), Does.Contain("林昭迎击"));
        _ = mySpear;
    }

    /// <summary>MP-Q103b：全无合法者不迎击、不扣次数；同一事件最多一人；Q87 两件独立事件分别重验，第二件可轮到下一名合法者。</summary>
    [Test]
    public void MP_Q103b_NoneLegal_NoCharge_TwoEventsSeparately()
    {
        var s = BraceScene(10105, out var z, out var mySpear, out var linSpear);
        var b = s.Battle!;
        mySpear.Durability = 0;
        linSpear.Durability = 0;
        SetBrace(s, Me, 1);
        SetBrace(s, Lin, 1);
        var rng = s.Rng.State;
        TryBrace(s, z);
        Assert.That((b.Fighter(Me).BraceCharges, b.Fighter(Lin).BraceCharges), Is.EqualTo((1, 1)), "无人合法：不出手、不扣");
        Assert.That(s.Rng.State, Is.EqualTo(rng), "没有出手就不抽随机");

        // 两件独立事件（Q87 补位、跟进）：第一件林昭迎击，第二件林昭次数已尽 → 周砚
        var t = BraceScene(10106, out var z2, out _, out _);
        var tb = t.Battle!;
        SetBrace(t, Lin, 1);
        SetBrace(t, Zhou, 1);
        Force(t, AtLeast(0.999), AtLeast(0.999));
        TryBrace(t, z2);
        TryBrace(t, z2);
        Assert.That((tb.Fighter(Lin).BraceCharges, tb.Fighter(Zhou).BraceCharges), Is.EqualTo((0, 0)));
        Assert.That(tb.Log.Where(l => l.Contains("迎击")).Select(l => l.Contains("林昭迎击") ? "林昭" : l.Contains("周砚迎击") ? "周砚" : l).ToList(), Is.EqualTo(new[] { "林昭", "周砚" }));
    }

    /// <summary>MP-Q103c：敌卡“触发架矛/算主动前移”与执行同源——找得到合法迎击者才写触发；预览不抽随机、不扣次数。</summary>
    [Test]
    public void MP_Q103c_TagSameSourceAsExecution()
    {
        var s = BraceScene(10107, out var z, out var mySpear, out _);
        var b = s.Battle!;
        var rng = s.Rng.State;
        Assert.That(E.BraceResponder(s, b, z), Is.Null);
        Assert.That(E.ApproachBraceTag(s, b, z, z.Pos), Is.EqualTo("算主动前移"), "没人架矛");
        mySpear.Durability = 0;
        SetBrace(s, Me, 1);
        Assert.That(E.ApproachBraceTag(s, b, z, z.Pos), Is.EqualTo("算主动前移"), "唯一的架矛者不合法（耐久不足）");
        Assert.That(GameEngine.BraceActive(b), Is.True, "旧口径只看次数会误写“触发”");
        SetBrace(s, Zhou, 1);
        Assert.That(E.BraceResponder(s, b, z), Is.EqualTo(Zhou), "前者不合法，顺延到周砚");
        Assert.That(E.ApproachBraceTag(s, b, z, z.Pos), Is.EqualTo("触发架矛"));
        // 够不到：周砚的木矛打不到 4 号位
        Assert.That(E.BraceResponder(s, b, z, 4), Is.Null);
        Assert.That(s.Rng.State, Is.EqualTo(rng), "预览不抽随机");
        Assert.That((b.Fighter(Me).BraceCharges, b.Fighter(Zhou).BraceCharges), Is.EqualTo((1, 1)), "预览不扣次数");
    }

    // ───────────── MP-Q104a—c：每人独立攻击经验次数与旧档迁移 ─────────────

    /// <summary>MP-Q104a：两人对同一敌同一门各自 3 次、每次 +2 只给出手者；多段同目标一指令只记 1；群攻每目标独立。</summary>
    [Test]
    public void MP_Q104a_PerActorThreeHits()
    {
        var s = Party(10108, 2, "shambler", "shambler");
        var e1 = Enemy(s, 1);
        var e2 = Enemy(s, 2);
        for (var i = 0; i < 3; i++) Assert.That(AttackXp(s, Me, "spear", e1), Does.Contain("经验+2"), $"你第{i + 1}次");
        Assert.That(AttackXp(s, Me, "spear", e1), Is.Empty, "你第 4 次不再计");
        for (var i = 0; i < 3; i++) Assert.That(AttackXp(s, Lin, "spear", e1), Does.Contain("经验+2"), $"林昭第{i + 1}次：不被你的次数占用");
        Assert.That(AttackXp(s, Lin, "spear", e1), Is.Empty);
        Assert.That(AttackXp(s, Lin, "blade", e1), Does.Contain("经验+2"), "其他门类另计");
        Assert.That(AttackXp(s, Me, "spear", e2, e2), Does.Contain("经验+2").And.Not.Contain("+4"), "同一指令多段同目标只记 1");
        Assert.That(AttackXpKeys.Hits(e2.ActorXpHits, Me, "spear"), Is.EqualTo(1));
        var w1 = s.WorldEnemies.Single(w => w.Id == e1.WorldId);
        Assert.That((AttackXpKeys.Hits(w1.ActorXpHits, Me, "spear"), AttackXpKeys.Hits(w1.ActorXpHits, Lin, "spear")), Is.EqualTo((3, 3)), "随世界敌人保存");
        Assert.That(w1.XpHits.ContainsKey("spear"), Is.False, "旧全队共享账不再增加");
        Assert.That(E.AttackXpLeft(s, e1, "spear"), Is.EqualTo(0));
        using (E.FocusOn(s, Zhou)) Assert.That(E.AttackXpLeft(s, e1, "spear"), Is.EqualTo(3), "周砚还没用过");
    }

    /// <summary>MP-Q104b：撤退重接/读档不重置；反击/迎击与主动攻击共用本人额度。</summary>
    [Test]
    public void MP_Q104b_PersistsAndReactionsShareBudget()
    {
        var s = Party(10109, 2, "shambler");
        var b = s.Battle!;
        var e = Enemy(s, 1);
        for (var i = 0; i < 3; i++) AttackXp(s, Lin, "spear", e);
        // 林昭的迎击命中也不再给经验（共用本人额度）
        b.Allies = new List<string> { Lin, Me };
        ArmActor(s, Lin, "wooden_spear");
        SetBrace(s, Lin, 1);
        e.Slot = "1";
        e.Hp = 999;
        Force(s, Below(0.001), AtLeast(0.999), Any());
        TryBrace(s, e);
        Assert.That(b.Log.Last(), Does.Contain("林昭迎击").And.Not.Contain("经验"));
        // 读档后计数还在
        var back = SaveSerializer.Deserialize(SaveSerializer.Serialize(s), Cfg);
        Assert.That(AttackXpKeys.Hits(back.Battle!.Enemies.Single().ActorXpHits, Lin, "spear"), Is.EqualTo(3));
        Assert.That(AttackXpKeys.Hits(back.WorldEnemies.Single().ActorXpHits, Lin, "spear"), Is.EqualTo(3));
        // 你自己的迎击照常计
        SetBrace(s, Me, 1);
        b.Allies = new List<string> { Me, Lin };
        ArmActor(s, Me, "wooden_spear");
        Force(s, Below(0.001), AtLeast(0.999), Any());
        TryBrace(s, e);
        Assert.That(b.Log.Last(), Does.Contain("你迎击").And.Contain("经验+2"));
    }

    /// <summary>
    /// MP-Q104c：旧共享 2 → 迁移时名册每人已用 2（含主角），之后新加入的角色 0；不补发/倒扣经验；
    /// 世界与在途同 ID 账逐门取大；迁移重放不覆盖个人账。
    /// </summary>
    [Test]
    public void MP_Q104c_LegacyMigration()
    {
        var s = Party(10110, 3, "shambler");
        var b = s.Battle!;
        var e = Enemy(s, 1);
        var w = s.WorldEnemies.Single(x => x.Id == e.WorldId);
        var other = E.DebugSpawnEnemy(s, "shambler", "village");
        // 模拟旧档：只有共享账；在途副本比世界多 1（取大）
        w.XpHits["spear"] = 1;
        e.XpHits["spear"] = 2;
        other.XpHits["blade"] = 3;
        var skills0 = s.Actors.ToDictionary(kv => kv.Key, kv => kv.Value.Skills.ToDictionary(x => x.Key, x => (x.Value.Level, x.Value.Xp)));
        var j = Newtonsoft.Json.Linq.JObject.Parse(SaveSerializer.Serialize(s));
        j["SchemaVersion"] = "18";
        j.Remove("AttackXpLegacyRoster");
        foreach (var we in (Newtonsoft.Json.Linq.JArray)j["WorldEnemies"]!) ((Newtonsoft.Json.Linq.JObject)we).Remove("ActorXpHits");
        foreach (var be in (Newtonsoft.Json.Linq.JArray)j["Battle"]!["Enemies"]!) ((Newtonsoft.Json.Linq.JObject)be).Remove("ActorXpHits");
        var m = SaveSerializer.Deserialize(j.ToString(), Cfg, out _);
        Assert.That(m.SchemaVersion, Is.EqualTo(GameState.CurrentSchema), "18 → 19 → 20 链式升级");
        Assert.That(m.AttackXpLegacyRoster, Is.EquivalentTo(new[] { Me }.Concat(m.Roster.Select(r => r.ActorId))));
        var mw = m.WorldEnemies.Single(x => x.Id == w.Id);
        var me = m.Battle!.Enemies.Single();
        foreach (var id in new[] { Me, Lin, Zhou })
        {
            Assert.That(AttackXpKeys.Hits(mw.ActorXpHits, id, "spear"), Is.EqualTo(2), $"{id} 继承旧已用次数（世界1/在途2取大）");
            Assert.That(AttackXpKeys.Hits(me.ActorXpHits, id, "spear"), Is.EqualTo(2));
            Assert.That(AttackXpKeys.Hits(m.WorldEnemies.Single(x => x.Id == other.Id).ActorXpHits, id, "blade"), Is.EqualTo(3));
        }
        foreach (var kv in skills0)
            foreach (var sk in kv.Value)
                Assert.That((m.Actors[kv.Key].Skills[sk.Key].Level, m.Actors[kv.Key].Skills[sk.Key].Xp), Is.EqualTo(sk.Value), "不补发、不倒扣历史经验");
        Assert.That(mw.XpHits["spear"], Is.EqualTo(1), "旧账保留作来源证据");
        Assert.That(m.Battle.Enemies.Single().Hp, Is.EqualTo(e.Hp), "进行中战斗不动");
        // 迁移后每人只剩 1 次
        Assert.That(AttackXp(m, Lin, "spear", me), Does.Contain("经验+2"));
        Assert.That(AttackXp(m, Lin, "spear", me), Is.Empty);
        Assert.That(AttackXp(m, Zhou, "spear", me), Does.Contain("经验+2"), "队友互不占用");
        // 重放幂等：已有个人账不被旧账覆盖
        mw.XpHits["spear"] = 0;
        SaveMigrator.MigrateAttackXp(m, Cfg);
        Assert.That(AttackXpKeys.Hits(mw.ActorXpHits, Lin, "spear"), Is.EqualTo(3));
        // 迁移之后才入册的角色从 0 起
        E.DebugRecruit(m, "npc_ye_xun");
        SaveMigrator.MigrateAttackXp(m, Cfg);
        Assert.That(AttackXpKeys.Hits(mw.ActorXpHits, "npc_ye_xun", "spear"), Is.EqualTo(0), "未来新角色 0");
        Assert.That(m.AttackXpLegacyRoster, Does.Not.Contain("npc_ye_xun"), "名册快照只冻结一次");
    }

    // ───────────── MP-Q105a—c：整队两目标原子交换 ─────────────

    /// <summary>三人：林昭 1 号、你 2 号（武器Ⅱ信号哨、指挥 3 级）、周砚 3 号；轮到你。</summary>
    private GameState ReorderScene(uint seed, out ItemInstance whistle)
    {
        var s = Party(seed, 3, "shambler");
        var b = s.Battle!;
        b.Allies = new List<string> { Lin, Me, Zhou };
        TurnOf(s, Me);
        whistle = ArmActor(s, Me, "signal_whistle", 2);
        s.Skills["command"].Level = 3;
        b.PlayerAp = 6;
        return s;
    }

    private static BattleCommand Reorder(string? a, string? bb, int? pa = null, int? pb = null) =>
        new BattleCommand { Action = "command_03", AllyTarget = a, AllyTarget2 = bb, AllyTargetPos = pa, AllyTarget2Pos = pb };

    /// <summary>MP-Q105a：A 前后两邻都可选 B、自己可作 A/B；单人/空位/非邻/同一人都拒绝，拒绝不扣成本。</summary>
    [Test]
    public void MP_Q105a_TargetsAndRejections()
    {
        var s = ReorderScene(10111, out _);
        var b = s.Battle!;
        var opt = E.BattleOptions(s).Single(o => o.Action == "command_03");
        Assert.That(opt.Available, Is.True, opt.Reason);
        Assert.That(opt.TargetMode, Is.EqualTo("ally_pair"));
        Assert.That(opt.AllyTargets, Is.EqualTo(new[] { Lin, Me, Zhou }), "三人都有相邻友方，A 可以是自己");
        Assert.That(GameEngine.ReorderPartners(b, Me), Is.EqualTo(new[] { Lin, Zhou }), "A 前后两邻都可选");
        Assert.That(GameEngine.ReorderPartners(b, Lin), Is.EqualTo(new[] { Me }));
        var ap = b.PlayerAp;
        var en = s.Stats.Energy;
        Assert.That(E.CommandBlocked(s, Reorder(null, null)), Does.StartWith("请先点"));
        Assert.That(E.CommandBlocked(s, Reorder(Me, null)), Does.Contain("林昭").And.Contain("周砚"), "提示可选的相邻友方");
        Assert.That(E.CommandBlocked(s, Reorder(Lin, Zhou)), Does.Contain("不相邻"));
        Assert.That(E.CommandBlocked(s, Reorder(Lin, Lin)), Is.EqualTo("两个目标不能是同一人"));
        Assert.That(E.CommandBlocked(s, Reorder(Lin, Shen)), Does.Contain("不在场上"), "候补/不在场不能选");
        Assert.That(E.ExecuteCommand(s, Reorder(Lin, Zhou)).Ok, Is.False);
        Assert.That((b.PlayerAp, s.Stats.Energy, b.SkillUsedRound.ContainsKey("command_03")), Is.EqualTo((ap, en, false)), "拒绝不扣 AP/体力/冷却");
        Assert.That(b.Allies, Is.EqualTo(new[] { Lin, Me, Zhou }));
        // 中间隔空位不算相邻
        b.Allies = new List<string> { Lin, "", Me, Zhou };
        Assert.That(E.CommandBlocked(s, Reorder(Lin, Me)), Does.Contain("两侧没有相邻"));
        Assert.That(E.BattleOptions(s).Single(o => o.Action == "command_03").AllyTargets, Does.Not.Contain(Lin));
        // 单人：需要队友
        var solo = Fight(10112, "shambler");
        ToPlayer(solo);
        ArmActor(solo, Me, "signal_whistle", 2);
        solo.Skills["command"].Level = 3;
        Assert.That(E.BattleOptions(solo).Single(o => o.Action == "command_03").Reason, Does.Contain("没有可交换站位的队友"));
    }

    /// <summary>MP-Q105b：预览不付费；预览后目标换位则整条拒绝、不自动改选；合法确认原子交换，施术者只付一次 2AP/实际E/CD/器具磨损。</summary>
    [Test]
    public void MP_Q105b_PreviewFree_StaleRejected_AtomicSwapAndCost()
    {
        var s = ReorderScene(10113, out var whistle);
        var b = s.Battle!;
        var ap = b.PlayerAp;
        var en = s.Stats.Energy;
        var rng = s.Rng.State;
        var pv = E.PreviewCommand(s, Reorder(Me, Zhou, 2, 3));
        Assert.That(pv.Ok, Is.True, pv.Reason);
        Assert.That(pv.Ap, Is.EqualTo(2));
        Assert.That(pv.EffectText, Is.EqualTo("整队：你 2号位→3号位，周砚 3号位→2号位"));
        Assert.That((b.PlayerAp, s.Stats.Energy, s.Rng.State), Is.EqualTo((ap, en, rng)), "预览不扣费、不抽随机");
        Assert.That(b.Allies, Is.EqualTo(new[] { Lin, Me, Zhou }), "预览不改站位");
        // 快照过期：周砚已不在 3 号位
        Assert.That(E.CommandBlocked(s, Reorder(Me, Zhou, 2, 1)), Does.Contain("号位已变化"));
        var energy = pv.EnergyCost;
        var dur0 = whistle.Durability;
        var queue = b.Queue.ToList();
        var preset = s.FormationPreset.ToList();
        var cmdXp = s.Skills["command"].Xp;
        var r = E.ExecuteCommand(s, Reorder(Me, Zhou, 2, 3).WithRequestId("reorder-1"));
        Assert.That(r.Ok, Is.True, r.Reason);
        Assert.That(b.Allies, Is.EqualTo(new[] { Lin, Zhou, Me }), "原子交换两真实槽");
        Assert.That(b.PlayerAp, Is.EqualTo(ap - 2));
        Assert.That(s.Stats.Energy, Is.EqualTo(en - energy).Within(1e-9));
        Assert.That(b.SkillUsedRound["command_03"], Is.EqualTo(b.Round), "冷却 1 轮");
        if (dur0 != null) Assert.That(whistle.Durability, Is.EqualTo(dur0 - 1).Within(1e-9), "指挥器具磨 1");
        Assert.That(b.Queue, Is.EqualTo(queue), "不改行动队列、不多激活");
        Assert.That(s.FormationPreset, Is.EqualTo(preset), "不反写战前预设");
        Assert.That(s.Skills["command"].Xp, Is.EqualTo(cmdXp), "换位无经验");
        Assert.That(b.Log.Last(), Does.Contain("整队").And.Contain("你2号位→3号位").And.Contain("周砚3号位→2号位"));
    }

    /// <summary>MP-Q105c：重复请求不换回、不双扣；施术者可因交换离开 234（按提交前己位许可）；不触发架矛。</summary>
    [Test]
    public void MP_Q105c_IdempotentAndCasterMayLeave234()
    {
        var s = ReorderScene(10114, out _);
        var b = s.Battle!;
        var z = Enemy(s, 1);
        z.Slot = "1";
        ArmActor(s, Zhou, "wooden_spear");
        SetBrace(s, Zhou, 1);
        var cmd = Reorder(Lin, Me, 1, 2).WithRequestId("reorder-2");
        Assert.That(E.ExecuteCommand(s, cmd).Ok, Is.True);
        Assert.That(b.Allies, Is.EqualTo(new[] { Me, Lin, Zhou }), "你从 2 号位换到 1 号位（指挥器具用位 234，按提交前己位）");
        var ap = b.PlayerAp;
        Assert.That(E.ExecuteCommand(s, cmd).Duplicate, Is.True);
        Assert.That(b.Allies, Is.EqualTo(new[] { Me, Lin, Zhou }), "重复请求不换回");
        Assert.That(b.PlayerAp, Is.EqualTo(ap), "不双扣");
        Assert.That(b.Fighter(Zhou).BraceCharges, Is.EqualTo(1), "整队不触发架矛");
        // 换到 1 号位后再用指挥技能按真实己位判断
        b.SkillUsedRound.Remove("command_03");
        Assert.That(E.CommandBlocked(s, Reorder(Me, Lin)), Does.Contain("只能在我方2、3、4号位"));
    }

    // ───────────── MP-Q106：掩护/坚守与格挡分层 ─────────────

    /// <summary>MP-Q106：队友减伤 0.8 与普通格挡 0.5 相乘为 0.4、与盾格挡 0.4 相乘为 0.32，各消费一份；0 伤不消费。</summary>
    [Test]
    public void MP_Q106_CoverTimesGuard()
    {
        double Hit(Action<GameState>? setup, double factor = 1.0)
        {
            var s = Fight(10115, "shambler");
            var b = s.Battle!;
            var e = Enemy(s, 1);
            e.Slot = "1";
            e.Intent = "attack";
            StandAt(s, 1);
            setup?.Invoke(s);
            Force(s, Any(), Below(0.01), Any());
            var h = s.Stats.Health;
            Attack(s, e, factor);
            if (setup != null && factor > 0)
            {
                Assert.That(b.Me.CoverFactor, Is.EqualTo(1), "队友减伤用掉一份");
                Assert.That(b.PlayerGuard, Is.False, "格挡用掉一次");
            }
            return h - s.Stats.Health;
        }
        void Cover(GameState s) { var b = s.Battle!; b.Me.CoverFactor = 0.8; b.Me.CoverUntilRound = b.Round + 1; }
        var plain = Hit(null);
        Assert.That(plain, Is.GreaterThan(0));
        var normal = Hit(s => { Cover(s); var b = s.Battle!; b.PlayerGuard = true; b.PlayerGuardFactor = 0.5; b.PlayerGuardCharges = 1; });
        Assert.That(normal, Is.EqualTo(plain * 0.4).Within(1e-9), "0.8×0.5");
        var shield = Hit(s => { Cover(s); var b = s.Battle!; b.PlayerGuard = true; b.PlayerGuardFactor = 0.4; b.PlayerGuardCharges = 1; });
        Assert.That(shield, Is.EqualTo(plain * 0.32).Within(1e-9), "0.8×0.4");
        // 0 伤（无正直接伤害）不消费减伤与格挡
        var t = Fight(10116, "shambler");
        var tb = t.Battle!;
        var te = Enemy(t, 1);
        te.Slot = "1";
        te.Intent = "attack";
        StandAt(t, 1);
        Cover(t);
        tb.PlayerGuard = true;
        tb.PlayerGuardFactor = 0.5;
        tb.PlayerGuardCharges = 1;
        Force(t, Any(), Below(0.01), Any());
        Attack(t, te, 0);
        Assert.That((tb.Me.CoverFactor, tb.PlayerGuard), Is.EqualTo((0.8, true)), "0 伤不消费");
    }
}
