using System.Reflection;
using Newtonsoft.Json.Linq;
using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 B20（策划提交队列 B20）R62：12 个新敌人面板与意图循环。出处：A09「E2 战斗通则与差异化环境」「E3 新增12个敌人面板与行为」
/// 「最终敌方资源/人口接口」；数据 assets/20261030/combat/expansion-combat.json newEnemies；验收 EC05（新敌不偷偷施加病毒/毒气/温度）、
/// E2“同名 buff 取强”“非咬击不判感染”。
/// </summary>
public class B20EnemyTests : BattleTestBase
{
    private static JObject Combat() =>
        JObject.Parse(File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "design.expansion-combat.json")));

    private void EnemyAct(GameState s, BattleEnemy e) =>
        typeof(GameEngine).GetMethod("EnemyActivation", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(E, new object[] { s, e, false });

    private void Lock(GameState s, BattleEnemy e) =>
        typeof(GameEngine).GetMethod("LockIntent", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(E, new object[] { s, s.Battle!, e });

    private void Ready(GameState s)
    {
        PlayerFirst(s);
        E.BattleStep(s);
        Assert.That(E.AwaitingPlayer(s), Is.True);
    }

    /// <summary>把敌人的意图拨到循环第 k 项（0 起）并重新锁定：JoinRound 让 Round−JoinRound=k。</summary>
    private void AtStep(GameState s, BattleEnemy e, int k)
    {
        if (s.Battle!.Round - k < 1) s.Battle.Round = k + 1;
        e.JoinRound = s.Battle.Round - k;
        e.Acted = false;
        e.RoundApUsed = 0;
        Lock(s, e);
    }

    // ───────────── 数据 ─────────────

    /// <summary>EC05 前提：12 个新敌人的面板逐项与 expansion-combat.json newEnemies 一致；4 普通 3AP、8 守卫 4AP；不另加附加掉落。</summary>
    [Category("DesignSync")]
    [Test]
    public void R62_TwelveNewEnemies_PanelsMatchJson()
    {
        var src = (JArray)Combat()["newEnemies"]!;
        Assert.That(src, Has.Count.EqualTo(12));
        var en = Cfg.Enemies.ToDictionary(e => e.Id);
        foreach (JObject j in src)
        {
            var id = (string)j["id"]!;
            Assert.That(en.ContainsKey(id), Is.True, id);
            var c = en[id];
            Assert.That((c.Name, c.Hp, c.Armor, c.Dodge, c.AttackRating, c.DamageMin, c.DamageMax, c.Speed, c.Ap, c.ControlResist),
                Is.EqualTo(((string)j["name"]!, (double)j["hp"]!, (double)j["armor"]!, (double)j["evasion"]!, (double)j["attack"]!,
                    (double)j["damage"]![0]!, (double)j["damage"]![1]!, (double)j["speed"]!, (int)j["ap"]!, (double)j["controlResist"]!)), id);
            Assert.That(c.OwnSlots, Is.EqualTo(string.Concat(((JArray)j["ownSlots"]!).Select(x => (string)x!))), id);
            Assert.That(c.TargetSlots, Is.EqualTo(string.Concat(((JArray)j["targetSlots"]!).Select(x => (string)x!))), id);
            Assert.That(c.Family, Is.EqualTo("zombie"));
            Assert.That(c.ExtraDrop, Is.Null, "E5：四新普通怪不另加稀有材料；守卫回收材料在奖励分账（B21）");
            Assert.That(c.Elite, Is.EqualTo(c.Ap == 4));
            // PLAN-Q189-01（Q189 选B，280f8be，取代开发假设 B20-2 的精英 60%/14、普通 35%/6 分组）：逐敌受惊参数取自 newEnemies
            Assert.That((c.FearChance, c.FearValue), Is.EqualTo(((double)j["fearChance"]!, (double)j["fearValue"]!)), id);
            Assert.That((c.FearChance, c.FearValue), Is.EqualTo(Q189Fear[id]), id);
        }
        Assert.That(Cfg.Enemies.Count(e => e.Id.StartsWith("ec_boss_")), Is.EqualTo(8));
    }

    /// <summary>PLAN-Q189-01：低吟45%/8、试制40%/8、缠线50%/10、货架40%/7、祭室60%/14、隔离65%/14、军械60%/15、天线65%/15、泵房55%/12、深井60%/16、扶梯60%/13、仓管55%/13。</summary>
    static readonly Dictionary<string, (double, double)> Q189Fear = new()
    {
        ["ec_cantor"] = (0.45, 8), ["ec_labwalker"] = (0.40, 8), ["ec_cablecrawler"] = (0.50, 10), ["ec_stockbracer"] = (0.40, 7),
        ["ec_boss_crypt"] = (0.60, 14), ["ec_boss_lab"] = (0.65, 14), ["ec_boss_armory"] = (0.60, 15), ["ec_boss_relay"] = (0.65, 15),
        ["ec_boss_water"] = (0.55, 12), ["ec_boss_mine"] = (0.60, 16), ["ec_boss_mall"] = (0.60, 13), ["ec_boss_cold"] = (0.55, 13),
    };

    /// <summary>
    /// EC05 / E2：只有明写“咬击”的意图才可能改判咬击（缠线爬行者、货架护卫感染者抓击 80%/咬击 20%）；其余新敌（守卫“普通抓击”、
    /// 低吟、试制区）咬击权重 0；拉扯/扫击/重击/推挤都不是咬击。
    /// </summary>
    [Test]
    public void EC05_OnlyExplicitBiteSteps_CanInfect()
    {
        foreach (var c in Cfg.Enemies.Where(e => e.Id.StartsWith("ec_")))
        {
            var steps = c.Intents.Opening.Concat(c.Intents.Cycle).ToList();
            var bite = steps.Where(x => x.BitePct > 0).ToList();
            if (c.Id is "ec_cablecrawler" or "ec_stockbracer")
                Assert.That(bite.Select(x => (x.Kind, x.BitePct)), Is.EqualTo(new[] { ("attack", 20.0) }), c.Id);
            else Assert.That(bite, Is.Empty, c.Id);
            Assert.That(steps.Any(x => x.Kind == "bite"), Is.False, c.Id);
        }
    }

    /// <summary>EC05：守卫重击/扫击/推挤、低伤拉扯、标记后的抓击命中造成正伤也不加病毒（非咬击不判感染）。</summary>
    [Test]
    public void EC05_NonBiteHits_NoVirus()
    {
        foreach (var (id, k) in new[] { ("ec_boss_crypt", 1), ("ec_boss_relay", 0), ("ec_boss_water", 0), ("ec_cablecrawler", 0), ("ec_labwalker", 0), ("ec_boss_mine", 1) })
        {
            var s = Fight(2001, id);
            Ready(s);
            StandAt(s, 1);
            var e = Enemy(s, 1);
            e.Slot = "1";
            if (id == "ec_boss_mine") e.Charged = true;
            AtStep(s, e, k);
            Assert.That(e.Intent, Is.Not.EqualTo("bite"), id);
            s.Stats.Health = 20;
            var v0 = s.Body.VirusLoad;
            var h0 = s.Stats.Health;
            Force(s, Draw(), Below(0.01), Any(), Any(), Any());
            EnemyAct(s, e);
            Assert.That(s.Stats.Health, Is.LessThan(h0), id + "：" + string.Join(" | ", s.Battle!.Log.TakeLast(3)));
            Assert.That(s.Body.Infected, Is.False, id);
            Assert.That(s.Body.VirusLoad, Is.EqualTo(v0), id);
        }
    }

    // ───────────── 意图 ─────────────

    /// <summary>
    /// 低吟感染者：激活1 鼓动至多两名同伴下一击×1.10、不叠层；同名 buff 取强（再鼓动仍 1.10，不变 1.21）；同伴下一击用掉；
    /// 没有同伴时改普通抓击；激活2 普通抓击（在后排先前移 1 位）。
    /// </summary>
    [Test]
    public void R62_Cantor_RallyTwoAllies_NoStack_ConsumedOnNextHit()
    {
        var s = Fight(2002, "z_slow", "ec_cantor", "shambler");
        Ready(s);
        var a = Enemy(s, 1); var c = Enemy(s, 2); var z = Enemy(s, 3);
        a.Slot = "1"; c.Slot = "3"; z.Slot = "2";
        AtStep(s, c, 0);
        Assert.That(c.Intent, Is.EqualTo("rally"));
        Assert.That(E.IntentShown(s.Battle!, c, s), Does.Contain("至多两名同伴下一击×1.1"));
        EnemyAct(s, c);
        Assert.That((a.RallyFactor, z.RallyFactor), Is.EqualTo((1.1, 1.1)));
        Assert.That(c.RoundApUsed, Is.EqualTo(GameEngine.EnemyMainIntentAp), "非攻击主意图 2AP（Q95）");
        AtStep(s, c, 2);   // 第 3 次激活 = 循环第 1 项，再鼓动
        EnemyAct(s, c);
        Assert.That(a.RallyFactor, Is.EqualTo(1.1), "同名取强、不叠层");
        // 同伴下一击用掉（命中：抽目标→命中→伤害）
        StandAt(s, 1);
        s.Stats.Health = 20;
        AtStep(s, a, 0);
        Force(s, Draw(), Below(0.01), Below(0.0001));
        var h0 = s.Stats.Health;
        EnemyAct(s, a);
        var dealt = h0 - s.Stats.Health;
        var cfg = Cfg.Enemies.Single(x => x.Id == "z_slow");
        Assert.That(dealt, Is.GreaterThanOrEqualTo(cfg.DamageMin * 1.1 - 1e-6), "伤害×1.10");
        Assert.That(a.RallyFactor, Is.EqualTo(1.0), "下一击用掉");

        // 没有同伴：改普通抓击
        var t = Fight(2003, "ec_cantor");
        Ready(t);
        var only = Enemy(t, 1);
        AtStep(t, only, 0);
        Assert.That(only.Intent, Is.EqualTo("attack"));
        // 激活2 抓击己位 12：在 3 号位先前移 1 位再抓
        var u = Fight(2004, "ec_cantor");
        Ready(u);
        var cu = Enemy(u, 1);
        cu.Slot = "3";
        StandAt(u, 1);
        AtStep(u, cu, 1);
        Assert.That(E.PlanFor(u.Battle!, cu, E.StepOf(cu)), Is.EqualTo(GameEngine.ReachPlan.AdvanceThenAttack));
    }

    /// <summary>试制区感染者：普通抓击命中后判控制，成功则目标下一次攻击失衡；咬击 0。</summary>
    [Test]
    public void R62_Labwalker_HitStaggers()
    {
        var s = Fight(2005, "ec_labwalker");
        Ready(s);
        StandAt(s, 1);
        var e = Enemy(s, 1);
        e.Slot = "1";
        AtStep(s, e, 0);
        Assert.That(e.IntentLabel, Does.Contain("命中后可能失衡"));
        s.Stats.Health = 20;
        Force(s, Draw(), Below(0.01), Any(), Below(0.0001));
        EnemyAct(s, e);
        Assert.That(s.Battle!.PlayerStagger, Is.True, string.Join(" | ", s.Battle.Log.TakeLast(2)));
    }

    /// <summary>缠线爬行者：奇数激活低伤×0.6 拉扯（伤害照算），控制成功后目标前移 1 位；偶数激活抓击 80%/咬击 20%。</summary>
    [Test]
    public void R62_CableCrawler_LowDamagePull_MovesTarget()
    {
        var s = Fight(2006, "ec_cablecrawler");
        Ready(s);
        StandAt(s, 2);
        var e = Enemy(s, 1);
        e.Slot = "1";
        AtStep(s, e, 0);
        Assert.That(e.Intent, Is.EqualTo("pull"));
        Assert.That(e.IntentLabel, Does.Contain("0.36—0.60"), "低伤×0.6 写在意图上");
        s.Stats.Health = 20;
        Force(s, Draw(), Below(0.01), Any(), Below(0.05));
        EnemyAct(s, e);
        Assert.That(s.Stats.Health, Is.LessThan(20), "低伤拉扯有伤害");
        Assert.That(GameEngine.PlayerPos(s.Battle!), Is.EqualTo(1), "控制成功前移 1 位：" + string.Join(" | ", s.Battle!.Log.TakeLast(2)));
    }

    /// <summary>
    /// 仓管守卫：标记我方最靠后的存活者（意图锁定时选定公开）→拉扯低伤×0.7 对被标记者（目标位 1—4）→对 12 普通抓击；
    /// 隔离核心守卫：随机标记 → 对该目标普通攻击（目标失效则在目标位里随机，不换大招）→ 格挡。
    /// </summary>
    [Test]
    public void R62_ColdGuard_MarkRearmost_PullsMarked()
    {
        var s = Fight(2007, "ec_boss_cold");
        Ready(s);
        StandAt(s, 4);
        var e = Enemy(s, 1);
        e.Slot = "1";
        AtStep(s, e, 0);
        Assert.That(e.Intent, Is.EqualTo("mark"));
        Assert.That(e.IntentActor, Is.EqualTo(GameState.PlayerId));
        Assert.That(e.IntentLabel, Does.Contain("最靠后的存活者").And.Contain("4号位"));
        EnemyAct(s, e);
        Assert.That(e.MarkedActor, Is.EqualTo(GameState.PlayerId));
        AtStep(s, e, 1);
        Assert.That(e.Intent, Is.EqualTo("pull"));
        Assert.That(E.IntentTargetNote(s, s.Battle!, e), Does.Contain("打它标记的"));
        s.Stats.Health = 20;
        Force(s, Draw(), Below(0.01), Any(), Below(0.05));
        EnemyAct(s, e);
        Assert.That(s.Stats.Health, Is.LessThan(20), "拉扯低伤×0.7");
        Assert.That(GameEngine.PlayerPos(s.Battle!), Is.EqualTo(3), "被拉前 1 位");
        AtStep(s, e, 2);
        Assert.That((e.Intent, E.StepOf(e).TargetSlots ?? Cfg.Enemies.Single(x => x.Id == "ec_boss_cold").TargetSlots), Is.EqualTo(("attack", "12")), "基础近战仍 12");
    }

    [Test]
    public void R62_LabGuard_MarkThenAttackMarked_FallbackRandomWhenInvalid()
    {
        var s = Fight(2008, "ec_boss_lab");
        Ready(s);
        StandAt(s, 1);
        var e = Enemy(s, 1);
        e.Slot = "1";
        AtStep(s, e, 0);
        Assert.That(e.Intent, Is.EqualTo("mark"));
        Force(s, Draw());
        EnemyAct(s, e);
        Assert.That(e.MarkedActor, Is.EqualTo(GameState.PlayerId));
        AtStep(s, e, 1);
        Assert.That(E.IntentTargetNote(s, s.Battle!, e), Does.Contain("打它标记的"));
        e.MarkedActor = "nobody";   // 标记失效：按普通 Q91 在目标位内随机
        Assert.That(E.IntentTargetNote(s, s.Battle!, e), Does.Contain("随机选一名"));
        AtStep(s, e, 2);
        Assert.That(e.Intent, Is.EqualTo("guard"));
    }

    /// <summary>守卫循环：封锁祭室守卫 格挡→重击×1.3→普通抓击；深井重击者 重击×1.4 并破甲 0.2 两轮；扶梯拦截者 保护→抓击→12 扫击×0.55。</summary>
    [Test]
    public void R62_GuardianCycles_FromE3()
    {
        var en = Cfg.Enemies.ToDictionary(e => e.Id);
        string Kinds(string id) => string.Join(",", en[id].Intents.Cycle.Select(x => x.Kind + (x.Factor != 1 ? "×" + x.Factor : "")));
        Assert.That(Kinds("ec_boss_crypt"), Is.EqualTo("guard,charge×1.3,attack"));
        Assert.That(Kinds("ec_boss_lab"), Is.EqualTo("mark,attack,guard"));
        Assert.That(Kinds("ec_boss_armory"), Is.EqualTo("charge_prepare,charge×1.4,protect"));
        Assert.That(Kinds("ec_boss_relay"), Is.EqualTo("sweep×0.6,charge×1.3,guard"));
        Assert.That(Kinds("ec_boss_water"), Is.EqualTo("charge×0.7,attack,protect"));
        Assert.That(Kinds("ec_boss_mine"), Is.EqualTo("charge_prepare,charge×1.4,attack"));
        Assert.That(Kinds("ec_boss_mall"), Is.EqualTo("protect,attack,sweep×0.55"));
        Assert.That(Kinds("ec_boss_cold"), Is.EqualTo("mark,pull×0.7,attack"));
        var mine = en["ec_boss_mine"].Intents.Cycle[1];
        Assert.That((mine.ArmorBreak, mine.ArmorBreakRounds), Is.EqualTo((0.2, 2)));
        Assert.That(en["ec_boss_water"].Intents.Cycle[0].Knockback, Is.EqualTo(1));
        // 守卫 4AP：一个 2AP 主意图后余 AP 不追加攻击
        var s = Fight(2009, "ec_boss_crypt");
        Ready(s);
        var g = Enemy(s, 1);
        g.Slot = "1";
        AtStep(s, g, 0);
        var n0 = s.Battle!.Log.Count;
        EnemyAct(s, g);
        Assert.That((g.Guard, g.RoundApUsed, s.Battle.Log.Count - n0), Is.EqualTo((true, 2, 1)), "格挡只做这一个主意图");
    }
}
