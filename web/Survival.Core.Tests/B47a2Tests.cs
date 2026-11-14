using System.Reflection;
using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// B47a2 意志经验结账与濒死暂停（含死亡喷溅来源门槛）：R09 文首 Q673—Q681、Q688—Q689；dying-willpower-q616.json v3；开发记录 B47a2。
/// 行动与撤出预估截断（Q674、Q675、Q682—Q686）属 B47a3，不在这里。
/// </summary>
public class B47a2Tests : BattleTestBase
{
    protected override bool NoEnemyApproach => true;

    private static void Rolls(GameEngine e, params double[] u) => e.TestDeathRolls = new Queue<double>(u);

    private bool RoundBleed(GameState s) =>
        (bool)typeof(GameEngine).GetMethod("RoundStartBleed", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(E, new object[] { s })!;

    private void Reaction(GameState s, BattleEnemy t, string label)
    {
        var b = s.Battle!;
        typeof(GameEngine).GetMethod("ReactionStrike", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(E, new object[] { s, b, b.ActiveWeaponSlot <= 0 ? 1 : b.ActiveWeaponSlot, 1.0, t, label });
    }

    private void Activate(GameState s, BattleEnemy e) =>
        typeof(GameEngine).GetMethod("EnemyActivation", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(E, new object[] { s, e, false });

    private void Ready(GameState s)
    {
        PlayerFirst(s);
        E.BattleStep(s);
        Assert.That(E.AwaitingPlayer(s), Is.True);
    }

    private static string Root()
    {
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "AGENTS.md"))) dir = dir.Parent;
        return dir!.FullName;
    }

    private static int SplashLines(GameState s) => s.Battle?.Log.Count(l => l.Contains("死亡喷溅")) ?? s.Log.Count(l => l.Text.Contains("死亡喷溅"));

    // ───────────── M915 Q679 死亡喷溅来源门槛 ─────────────

    /// <summary>Q679-A01：有喷溅配置的敌人被我方主动直接伤害击杀触发原喷溅一次（1 号位按护甲扣，原死亡与掉落一次）。</summary>
    [Test]
    public void Q679_A01_ActiveDirectKill_Splashes()
    {
        var s = Fight(47201, "z_bloater", "shambler");
        Ready(s);
        var e = Enemy(s, 1);
        Assert.That(e.EnemyId, Is.EqualTo("z_bloater"));
        e.Hp = 0.01;
        var h0 = s.Player.Stats.Health;
        Force(s, Below(0.001), AtLeast(0.999), Any(), Any());
        Cmd(s, "basic_attack", 1);
        Assert.That(e.Alive, Is.False);
        Assert.That(SplashLines(s), Is.EqualTo(1), "1 号位的主角受喷溅一次");
        Assert.That(s.Player.Stats.Health, Is.EqualTo(h0 - 0.3).Within(1e-9), "基础伤害 0.3，主角护甲 0");
        Assert.That(s.WorldEnemies.Any(w => w.Id == e.WorldId), Is.False, "原死亡：世界实例删除");
        Assert.That(s.Battle!.Loot.Count(l => l.WorldId == e.WorldId), Is.GreaterThanOrEqualTo(1), "原掉落首次死亡锁定");
    }

    /// <summary>Q679-A01／A02：先挂出血后被回合开始出血致死不喷溅：无喷溅伤害、致死判定或日志，原死亡与掉落仍一次。</summary>
    [Test]
    public void Q679_A01_BleedKill_NoSplash_DeathAndLootOnce()
    {
        var s = Fight(47202, "z_bloater", "shambler");
        var e = Enemy(s, 1);
        e.Hp = 0.1;
        e.BleedDamage = 0.4;   // 测试直接挂出血（膨胀感染者原本流血免疫，这里只验喷溅门槛按真实致死来源分类）
        e.BleedTriggers = 2;
        s.Player.Stats.Health = 0;   // 濒死者：若错误喷溅会抽致死判定
        Rolls(E, 0.999);
        var h0 = s.Player.Stats.Health;
        var rng = s.Rng.State;
        var loot = s.Battle!.Loot.Count;
        Assert.That(RoundBleed(s), Is.True);
        Assert.That(e.Alive, Is.False, "出血致死");
        Assert.That(SplashLines(s), Is.EqualTo(0), "无喷溅日志");
        Assert.That((s.Player.Stats.Health, s.Player.Dead, E.TestDeathRolls!.Count), Is.EqualTo((h0, false, 1)), "无喷溅伤害、无致死判定");
        Assert.That(s.Battle!.Loot.Count(l => l.WorldId == e.WorldId), Is.EqualTo(s.Battle.Loot.Count - loot), "原掉落照常锁定");
        Assert.That(s.WorldEnemies.Any(w => w.Id == e.WorldId), Is.False);
        Assert.That(s.Battle.Log.Count(l => l.Contains("流血")), Is.EqualTo(1));
        _ = rng;
    }

    /// <summary>Q679-A01：反击、迎击直接打死有喷溅配置的敌人不喷溅；反应攻击经验照常。</summary>
    [TestCase("反击")]
    [TestCase("迎击")]
    public void Q679_A01_ReactionKill_NoSplash(string label)
    {
        var s = Fight(47203, "z_bloater", "shambler");
        var e = Enemy(s, 1);
        e.Hp = 0.01;
        s.Player.Stats.Health = 0;
        Rolls(E, 0.999);
        Force(s, Below(0.001), AtLeast(0.999), Any(), Any());
        Reaction(s, e, label);
        Assert.That(e.Alive, Is.False);
        Assert.That(SplashLines(s), Is.EqualTo(0));
        Assert.That((s.Player.Stats.Health, s.Player.Dead, E.TestDeathRolls!.Count), Is.EqualTo((0.0, false, 1)));
        Assert.That(s.Battle!.Log[^1], Does.Contain(label).And.Contain("倒下").And.Contain("经验+"), "原攻击／反应成长照旧");
    }

    /// <summary>Q679-A02：带着出血但最后被主动直接伤害击杀仍按主动击杀喷溅；两只喷溅敌一只主动杀一只出血杀，只喷一次。</summary>
    [Test]
    public void Q679_A02_BleedingButActiveKill_Splashes_PerRealDeath()
    {
        var s = Fight(47204, "z_bloater", "z_bloater");
        Ready(s);
        var a = Enemy(s, 1);
        var c = Enemy(s, 2);
        a.Hp = 0.01; a.BleedDamage = 0.4; a.BleedTriggers = 2;
        c.Hp = 0.1; c.BleedDamage = 0.4; c.BleedTriggers = 2;
        Force(s, Below(0.001), AtLeast(0.999), Any(), Any());
        Cmd(s, "basic_attack", 1);
        Assert.That((a.Alive, SplashLines(s)), Is.EqualTo((false, 1)), "带出血仍主动击杀 → 喷溅");
        var h = s.Player.Stats.Health;
        Assert.That(RoundBleed(s), Is.False, "最后一只出血倒下 → 胜利");
        Assert.That(s.Log.Count(l => l.Text.Contains("死亡喷溅")), Is.EqualTo(1), "第二只出血致死不喷");
        Assert.That(s.Player.Stats.Health, Is.EqualTo(h).Within(1e-9));
    }

    /// <summary>Q679-A03：合法喷溅仍按 1／2 号位与本人护甲结算（3 号位不受）；保存重读不重喷。</summary>
    [Test]
    public void Q679_A03_LegalSplash_FrontSlots_SaveReloadNoRepeat()
    {
        var s = New(47205);
        UnlockCapacity(s, 3);
        E.DebugQuickParty(s, 3);
        var mates = s.Party.Where(id => id != GameState.PlayerId).ToList();
        E.DebugTeleport(s, "field");
        s.GameMinute = At(2, 9, 0);
        s.SpawnNextCheck = int.MaxValue;
        s.PatrolNextCheck = int.MaxValue;
        s.WorldEnemies.Clear();
        var id = E.DebugSpawnEnemy(s, "z_bloater", "field").Id;
        E.DebugSpawnEnemy(s, "shambler", "field");
        Assert.That(E.DebugStartBattle(s, new[] { id, s.WorldEnemies.Last().Id }, GameEngine.FearEncounter), Is.True);
        var b = s.Battle!;
        b.Allies = new List<string> { GameState.PlayerId, mates[0], mates[1] };
        var h1 = s.Actors[mates[0]].Stats.Health;
        var h2 = s.Actors[mates[1]].Stats.Health;
        Ready(s);
        var e = b.Enemies.Single(x => x.WorldId == id);
        e.Hp = 0.01;
        Force(s, Below(0.001), AtLeast(0.999), Any(), Any());
        Cmd(s, "basic_attack", e.Index);
        Assert.That(SplashLines(s), Is.EqualTo(2), "1、2 号位各一次");
        Assert.That(s.Actors[mates[0]].Stats.Health, Is.LessThan(h1));
        Assert.That(s.Actors[mates[1]].Stats.Health, Is.EqualTo(h2), "3 号位不受");
        var json = SaveSerializer.Serialize(s);
        var t = SaveSerializer.Deserialize(json, Cfg);
        Assert.That(t.Battle!.Log.Count(l => l.Contains("死亡喷溅")), Is.EqualTo(2), "读档不重喷");
        Assert.That(t.Actors[GameState.PlayerId].Stats.Health, Is.EqualTo(s.Player.Stats.Health));
    }

    /// <summary>Q679（R05／S14／逐字稿）：喷溅静态特性句换成“主动攻击直接击杀”条件句（Q652 感染未接入，用过渡版）。</summary>
    [Test, Category("DesignSync")]
    public void Q679_TraitText_TransitionalCopy()
    {
        Assert.That(E.EnemyPublicTraits(E.Enemy("z_bloater")).Last(), Is.EqualTo(
            "死亡喷溅：被我方主动攻击直接击杀时，伤及我方1、2号位，基础伤害0.30，护甲照常计算。因流血等伤害型负面状态、反击或迎击死亡时不触发。"));
        var v = File.ReadAllText(Path.Combine(Root(), "doc", "策划案", "重墨像素风界面与卡片 v0.9", "重墨 V2 开工裁定与界面逐字稿.md"));
        Assert.That(v, Does.Contain("| 喷溅触发已对齐，Q652感染尚未实际接入的过渡特性 | 死亡喷溅：被我方主动攻击直接击杀时，伤及我方1、2号位，基础伤害{伤害值}，护甲照常计算。因流血等伤害型负面状态、反击或迎击死亡时不触发。 |"));
    }

    // ───────────── M916 Q676／Q677 意志经验结账 ─────────────

    private void Invoke(string name, params object[] args) =>
        typeof(GameEngine).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(E, args);

    private void EnemyAttack(GameState s, BattleEnemy e) =>
        Invoke("EnemyAttack", s, e, Cfg.Enemies.Single(x => x.Id == e.EnemyId), false);

    private static long WpXp(GameState s, string id) => s.Actors[id].Skills[GameEngine.Willpower].Xp;

    /// <summary>主角 1 号位、两名同伴 2、3 号位，对一只膨胀感染者＋一只蹒跚者入战，轮到主角；膨胀感染者健康压到 0.01。</summary>
    private GameState SplashFight(uint seed, out string m1, out string m2, out BattleEnemy bloater)
    {
        var s = New(seed);
        UnlockCapacity(s, 3);
        E.DebugQuickParty(s, 3);
        var mates = s.Party.Where(id => id != GameState.PlayerId).ToList();
        m1 = mates[0];
        m2 = mates[1];
        E.DebugTeleport(s, "field");
        s.GameMinute = At(2, 9, 0);
        s.SpawnNextCheck = int.MaxValue;
        s.PatrolNextCheck = int.MaxValue;
        s.WorldEnemies.Clear();
        var id = E.DebugSpawnEnemy(s, "z_bloater", "field").Id;
        var other = E.DebugSpawnEnemy(s, "shambler", "field").Id;
        Assert.That(E.DebugStartBattle(s, new[] { id, other }, GameEngine.FearEncounter), Is.True);
        s.Battle!.Allies = new List<string> { GameState.PlayerId, m1, m2 };
        Ready(s);
        bloater = s.Battle.Enemies.Single(x => x.WorldId == id);
        bloater.Hp = 0.01;
        return s;
    }

    /// <summary>
    /// Q676-A01／Q677-A01：一敌死亡喷溅伤及两名仍活者，各按自己的额度在我方指令末 +10；死敌 ID 记同敌账；
    /// 每人一行（主角空前缀、同伴真实姓名），在出手结果与喷溅之后；保存重读不重发。
    /// </summary>
    [Test]
    public void Q676_A01_Q677_A01_SplashXp_TwoSurvivors_OneLineEach()
    {
        var s = SplashFight(47211, out var m1, out _, out var bl);
        var key = bl.WorldId.ToString();
        Force(s, Below(0.001), AtLeast(0.999), Any(), Any());
        Cmd(s, "basic_attack", bl.Index);
        var log = s.Battle!.Log;
        var name = E.ActorDisplayName(s, m1);
        Assert.That((WpXp(s, GameState.PlayerId), WpXp(s, m1)), Is.EqualTo((10L, 10L)));
        Assert.That((s.Player.WillpowerEnemyAwards[key], s.Actors[m1].WillpowerEnemyAwards[key]), Is.EqualTo((1, 1)), "死敌原稳定 ID 记同敌账");
        Assert.That((s.Player.WillpowerXpToday, s.Player.WillpowerXpDay), Is.EqualTo((10, 2)));
        Assert.That(log[^2], Does.EndWith(" · 意志经验+10"), "主角空前缀");
        Assert.That(log[^1], Does.EndWith($" · {name}：意志经验+10"), "同伴真实姓名前缀");
        Assert.That(log.FindLastIndex(l => l.Contains("死亡喷溅")), Is.LessThan(log.Count - 2), "经验行在出手结果与喷溅之后");
        Assert.That(log.FindLastIndex(l => l.Contains("倒下")), Is.LessThan(log.Count - 2));
        Assert.That(log.Count(l => l.Contains("意志经验")), Is.EqualTo(2), "3 号位未受喷溅，不得");
        var t = SaveSerializer.Deserialize(SaveSerializer.Serialize(s), Cfg);
        Assert.That((WpXp(t, GameState.PlayerId), t.Battle!.Log.Count(l => l.Contains("意志经验"))), Is.EqualTo((10L, 2)), "读档不重发");
    }

    /// <summary>Q676-A01：已确死者不得；同敌 3 次已满、当日 60 剩 5、满级各按自己的额度（0 收益不写 +0、不占回执）。</summary>
    [Test]
    public void Q676_A01_Caps_DeadNone_PerEnemy3_Daily60_MaxLevel()
    {
        var s = SplashFight(47212, out var m1, out _, out var bl);
        var key = bl.WorldId.ToString();
        var p = s.Player;
        p.WillpowerXpDay = Clock.Day(s.GameMinute);
        p.WillpowerXpToday = 55;
        var m = s.Actors[m1];
        m.WillpowerEnemyAwards[key] = 3;
        Force(s, Below(0.001), AtLeast(0.999), Any(), Any());
        Cmd(s, "basic_attack", bl.Index);
        Assert.That((WpXp(s, GameState.PlayerId), p.WillpowerXpToday, p.WillpowerEnemyAwards[key]), Is.EqualTo((5L, 60, 1)), "日余额只剩 5");
        Assert.That((WpXp(s, m1), m.WillpowerEnemyAwards[key]), Is.EqualTo((0L, 3)), "同敌 3 次已满");
        Assert.That(s.Battle!.Log[^1], Does.EndWith(" · 意志经验+5"));
        Assert.That(s.Battle.Log.Any(l => l.Contains("意志经验+0")), Is.False);

        var t = SplashFight(47213, out var n1, out _, out var b2);
        var k2 = b2.WorldId.ToString();
        t.Player.Skills[GameEngine.Willpower] = new SkillState { Level = 10, Xp = 0 };
        t.Actors[n1].Stats.Health = 0;   // 濒死者喷溅后致死判定失败 → 已确死不得
        Rolls(E, 0.999);
        Force(t, Below(0.001), AtLeast(0.999), Any(), Any());
        Cmd(t, "basic_attack", b2.Index);
        Assert.That(t.Actors[n1].Dead, Is.True);
        Assert.That((t.Player.WillpowerEnemyAwards.ContainsKey(k2), t.Actors[n1].WillpowerEnemyAwards.ContainsKey(k2)), Is.EqualTo((false, false)), "满级与确死都不占回执");
        Assert.That(t.Battle!.Log.Any(l => l.Contains("意志经验")), Is.False);
    }

    /// <summary>Q676：距 10 级不足 10 点时只给到满级（升级沿原句一次，溢出清零）。</summary>
    [Test]
    public void Q676_NearCap_GivesRemainderOnly_LevelUpOnce()
    {
        var s = SplashFight(47214, out _, out _, out var bl);
        s.Player.Skills[GameEngine.Willpower] = new SkillState { Level = 9, Xp = E.NextLevelXp(GameEngine.Willpower, 9) - 4 };
        var log0 = s.Log.Count;
        Force(s, Below(0.001), AtLeast(0.999), Any(), Any());
        Cmd(s, "basic_attack", bl.Index);
        Assert.That((s.Player.Skills[GameEngine.Willpower].Level, s.Player.Skills[GameEngine.Willpower].Xp, s.Player.WillpowerXpToday), Is.EqualTo((10, 0L, 4)));
        Assert.That(s.Battle!.Log.Any(l => l.EndsWith(" · 意志经验+4")), Is.True);
        Assert.That(s.Log.Skip(log0).Count(l => l.Text.StartsWith("意志熟练度升到10级")), Is.EqualTo(1));
    }

    /// <summary>
    /// R09 §4.1（Q616-A08）：敌方一次完整出手多段打同人只给一次 10；同敌本局 3 次后不再给、不同敌各自记账、日额满 0 收益不占回执、
    /// 跨世界日日账重计；出血不给。
    /// </summary>
    [Test]
    public void Q616_A08_EnemyActionXp_OncePerAction_PerEnemy3_DayReset_BleedNone()
    {
        var s = Fight(47215, "shambler", "shambler");
        var e1 = Enemy(s, 1);
        var e2 = Enemy(s, 2);
        s.Player.Stats.Health = 20;
        void Hit(BattleEnemy e) { Force(s, Draw(), Below(0.01), Any()); EnemyAttack(s, e); s.Player.Stats.Health = 20; }
        Invoke("WillpowerBegin", s);
        Hit(e1); Hit(e1);
        Invoke("WillpowerEnd", s);
        Assert.That((WpXp(s, GameState.PlayerId), s.Player.WillpowerEnemyAwards[e1.WorldId.ToString()]), Is.EqualTo((10L, 1)), "同一完整出手两段只给一次");
        Assert.That(s.Battle!.Log.Count(l => l.Contains("意志经验+10")), Is.EqualTo(1));
        for (var k = 0; k < 3; k++) { Invoke("WillpowerBegin", s); Hit(e1); Invoke("WillpowerEnd", s); }
        Assert.That((WpXp(s, GameState.PlayerId), s.Player.WillpowerEnemyAwards[e1.WorldId.ToString()]), Is.EqualTo((30L, 3)), "同敌本局最多 3 次");
        Invoke("WillpowerBegin", s); Hit(e2); Invoke("WillpowerEnd", s);
        Assert.That(s.Player.WillpowerXpToday, Is.EqualTo(40), "另一敌各自记账");
        s.Player.WillpowerXpToday = 60;
        Invoke("WillpowerBegin", s); Hit(e2); Invoke("WillpowerEnd", s);
        Assert.That(s.Player.WillpowerEnemyAwards[e2.WorldId.ToString()], Is.EqualTo(1), "日额已满：0 收益不占回执");
        s.GameMinute += Clock.MinutesPerDay;
        Invoke("WillpowerBegin", s); Hit(e2); Invoke("WillpowerEnd", s);
        Assert.That((s.Player.WillpowerXpToday, s.Player.WillpowerEnemyAwards[e2.WorldId.ToString()]), Is.EqualTo((10, 2)), "新世界日日账重计");
        var xp = WpXp(s, GameState.PlayerId);
        s.Player.Body.BleedDamage = 0.4;
        s.Player.Body.BleedTriggers = 1;
        Invoke("WillpowerBegin", s); RoundBleed(s); Invoke("WillpowerEnd", s);
        Assert.That(WpXp(s, GameState.PlayerId), Is.EqualTo(xp), "出血不给");
    }

    /// <summary>真实战斗推进：敌方一次激活命中主角后，在该激活结束时写“意志经验+10”。</summary>
    [Test]
    public void EnemyActivation_ViaBattleStep_GrantsAtActivationEnd()
    {
        var s = Fight(47216, "shambler");
        var b = s.Battle!;
        var e = b.Alive.First();
        b.Queue = new List<int> { e.Index, 0 };
        b.NormalCount = 2;
        b.Cursor = 0;
        b.PlayerActing = false;
        s.Player.Stats.Health = 20;
        var granted = false;
        for (uint seed = 1; seed < 400 && !granted; seed++)
        {
            var t = SaveSerializer.Deserialize(SaveSerializer.Serialize(s), Cfg);
            t.Rng.State = seed * 2654435761u + 7;
            var before = t.Player.Stats.Health;
            E.BattleStep(t);
            if (t.Player.Stats.Health < before - 1e-9)
            {
                granted = true;
                Assert.That(WpXp(t, GameState.PlayerId), Is.EqualTo(10L));
                Assert.That(t.Battle!.Log[^1], Does.EndWith(" · 意志经验+10"), "激活末写一行");
            }
        }
        Assert.That(granted, Is.True, "找不到命中的随机");
    }

    /// <summary>Q679-A03：出血致死的喷溅敌不产生喷溅意志经验（不存在出血阶段末的喷溅结账路径）。</summary>
    [Test]
    public void Q679_A03_BleedKilledSplasher_NoWillpowerXp()
    {
        var s = SplashFight(47217, out var m1, out _, out var bl);
        bl.Hp = 0.1; bl.BleedDamage = 0.4; bl.BleedTriggers = 2;
        RoundBleed(s);
        Assert.That(bl.Alive, Is.False);
        Assert.That((WpXp(s, GameState.PlayerId), WpXp(s, m1)), Is.EqualTo((0L, 0L)));
        Assert.That(s.Battle!.Log.Any(l => l.Contains("意志经验")), Is.False);
    }

    // ───────────── M917 Q673／Q680／Q681 进入濒死暂停 ─────────────

    /// <summary>空闲新局（第 2 日白天、农野荒地、冻结刷新），主角健康 0.05、饱食水分 0 且提醒已锁存：下一段身体损伤让主角进入濒死。</summary>
    private GameState NearDying(uint seed)
    {
        var s = New(seed);
        E.DebugTeleport(s, "field");
        s.GameMinute = At(2, 9, 0);
        s.SpawnNextCheck = int.MaxValue;
        s.PatrolNextCheck = int.MaxValue;
        s.WorldEnemies.Clear();
        E.DebugDying(s, "pause1");
        return s;
    }

    private static List<LogEntry> Since(GameState s, int mark) => s.Log.Skip(mark).ToList();

    /// <summary>Q673-A01：未完多段行动内 H&gt;0→0：本段完整提交（世界 +5 分钟）后暂停一次，中央句与暂停原因按逐字稿；进入濒死日志只一条。</summary>
    [Test]
    public void Q673_A01_EnterDyingInMultiSegmentWork_PausesOnceAtBoundary()
    {
        var s = NearDying(47231);
        Start(s, ActionRequest.Wait(60));
        var t0 = s.GameMinute;
        var mark = s.Log.Count;
        var r = E.Advance(s);
        Assert.That(r.Status, Is.EqualTo(SegmentStatus.Paused));
        Assert.That((s.GameMinute - t0, s.Main!.ElapsedMinutes, s.Player.Stats.Health), Is.EqualTo((5, 5, 0.0)), "本段完整提交，不额外 tick");
        Assert.That((s.Main.PauseReason, s.Main.PauseNote, s.Main.PauseStat), Is.EqualTo((PauseReason.Dying, "你进入濒死状态", (StatId?)null)));
        var ev = Since(s, mark);
        Assert.That(ev.Count(e => e.Kind == "ActionPaused"), Is.EqualTo(1));
        Assert.That(ev.Single(e => e.Kind == "ActionPaused").Text, Is.EqualTo($"{s.Main.Label}已暂停：你进入濒死状态。"));
        Assert.That(ev.Count(e => e.Text == GameEngine.EnterDyingText("你")), Is.EqualTo(1), "不为暂停再造同义进入句");
        Assert.That(E.ActionProgress(s)!.PauseText, Is.EqualTo("你进入濒死状态"), "暂停原因");
        Assert.That(s.Progress.Dead, Is.False);
    }

    /// <summary>
    /// Q673-A02：手动继续后一直 H0，再过多段、新开工作都不因同次濒死再停（读档不清锁）；合法医疗 H&gt;0 后再次归零才再暂停一次。
    /// </summary>
    [Test]
    public void Q673_A02_NoRepeatWhileZero_ResetOnlyAfterMedicalHeal()
    {
        var s = NearDying(47232);
        Start(s, ActionRequest.Wait(60));
        Assert.That(E.Advance(s).Status, Is.EqualTo(SegmentStatus.Paused));
        Rolls(E, Enumerable.Repeat(0.0, 40).ToArray());   // 之后每段身体损伤都抵抗成功
        Assert.That(E.Resume(s).Ok, Is.True);
        for (var k = 0; k < 3; k++) Assert.That(E.Advance(s).Status, Is.EqualTo(SegmentStatus.Running), $"第{k + 2}段不再暂停");
        Assert.That(s.Log.Count(e => e.Text == GameEngine.ResistedDeathText("你")), Is.EqualTo(3), "Q678 逐段抵抗句照写");
        s = SaveSerializer.Deserialize(SaveSerializer.Serialize(s), Cfg);
        Assert.That(s.Player.DyingNotified, Is.True, "读档不清锁");
        E.Cancel(s);
        Start(s, ActionRequest.Wait(30));
        Assert.That(E.Advance(s).Status, Is.EqualTo(SegmentStatus.Running), "仍濒死时新开工作不伪造新进入事件");
        E.Cancel(s);
        // 合法医疗回血 → 脱离、清锁；再次归零重新暂停一次
        Assert.That(E.HealActor(s, s.Player, 0.02), Is.GreaterThan(0));
        Assert.That(s.Player.DyingNotified, Is.False);
        Start(s, ActionRequest.Wait(30));
        var r = E.Advance(s);
        Assert.That((r.Status, s.Main!.PauseReason), Is.EqualTo((SegmentStatus.Paused, PauseReason.Dying)));
    }

    /// <summary>Q673-A03：同段已完成的行动不凭空暂停（成果照交）；旧档兼容的 H0 活人（提醒锁已置）不补历史暂停；已提交的濒死暂停读回照常恢复。</summary>
    [Test]
    public void Q673_A03_CompletedNoPause_OldH0NoPause_SavedPauseRestores()
    {
        var s = NearDying(47233);
        s.Player.Stats.Health = 0.05 + 5 * 2.0 * 5 / 60;   // 饱食、水分归零共 −2/小时：前 5 段仍正常，第 6 段（最后一段）进入濒死
        Start(s, ActionRequest.Wait(30));
        for (var k = 0; k < 5; k++) Assert.That(E.Advance(s).Status, Is.EqualTo(SegmentStatus.Running));
        Assert.That(s.Player.Stats.Health, Is.GreaterThan(0));
        var r = E.Advance(s);
        Assert.That((r.Status, s.Main, s.Player.Stats.Health), Is.EqualTo((SegmentStatus.Completed, (ActionState?)null, 0.0)), "同段完成 → 原完成收束，不暂停");
        Assert.That(s.Log.Count(e => e.Kind == "ActionPaused"), Is.EqualTo(0));

        var t = NearDying(47234);
        t.Player.Stats.Health = 0;
        t.Player.DyingNotified = true;   // 同存档 52 迁移：旧 H0 活人归濒死并置提醒锁
        Rolls(E, Enumerable.Repeat(0.0, 10).ToArray());
        Start(t, ActionRequest.Wait(60));
        Assert.That(E.Advance(t).Status, Is.EqualTo(SegmentStatus.Running), "不补历史进入事件或暂停");
        Assert.That(t.Log.Any(e => e.Text == GameEngine.EnterDyingText("你")), Is.False);

        var u = NearDying(47235);
        Start(u, ActionRequest.Wait(60));
        E.Advance(u);
        var v = SaveSerializer.Deserialize(SaveSerializer.Serialize(u), Cfg);
        Assert.That((v.Main!.Status, v.Main.PauseReason, E.ActionProgress(v)!.PauseText), Is.EqualTo((ActionStatus.Paused, PauseReason.Dying, "你进入濒死状态")));
        Assert.That(E.Resume(v).Ok, Is.True);
        Assert.That((v.Main!.Status, v.Main.PauseNote), Is.EqualTo((ActionStatus.Running, (string?)null)));
    }

    /// <summary>Q673-A03：候补（不在同行）在世界段进入濒死只写日志，不暂停同行者的工作。</summary>
    [Test]
    public void Q673_A03_ReserveEnteringDying_LogOnly()
    {
        var s = New(47236);
        E.DebugQuickParty(s, 2);
        var mate = s.Party.Single(id => id != GameState.PlayerId);
        s.Party.Remove(mate);   // 留作候补（名册在册、不在同行）
        Assert.That(E.IsReserve(s, mate), Is.True);
        var m = s.Actors[mate];
        m.Stats.Health = 0.05;
        m.Stats.Satiety = 0;
        m.Stats.Hydration = 0;
        Start(s, ActionRequest.Wait(60));
        var r = E.Advance(s);
        Assert.That(m.Stats.Health, Is.EqualTo(0.0));
        Assert.That(r.Status, Is.EqualTo(SegmentStatus.Running));
        Assert.That(s.Main?.PauseReason ?? PauseReason.None, Is.Not.EqualTo(PauseReason.Dying));
        Assert.That(s.Log.Any(e => e.Text == GameEngine.EnterDyingText(E.ActorDisplayName(s, mate))), Is.True);
    }

    /// <summary>
    /// Q680-A01：战斗中、工作已暂停、空闲时进入濒死只写日志并锁存；战后继续、恢复暂停的工作或新开工作都不补暂停；
    /// 正在推进的可暂停工作内真实进入濒死仍暂停（见 Q673-A01）。
    /// </summary>
    [Test]
    public void Q680_A01_NoDeferredPause_FromBattlePausedOrIdle()
    {
        // 战斗中：在办等待被打断入战，战中进入濒死；战后继续原工作不补停
        var s = NearDying(47241);
        s.Player.Stats.Health = 0.3;
        Start(s, ActionRequest.Wait(60));
        Assert.That(E.Advance(s).Status, Is.EqualTo(SegmentStatus.Running));
        s.Player.Stats.Satiety = 80; s.Player.Stats.Hydration = 80;
        var id = E.DebugSpawnEnemy(s, "shambler", "field").Id;
        Assert.That(E.DebugStartBattle(s, new[] { id }, GameEngine.FearEncounter), Is.True);
        Force(s, Draw(), Below(0.01), Any());
        EnemyAttack(s, s.Battle!.Alive.First());
        Assert.That((s.Player.Stats.Health, s.Player.DyingNotified), Is.EqualTo((0.0, true)), "战中只写战报并锁存");
        s.Battle = null;   // 收场（这里只验暂停，不走战斗收束）
        if (s.Main!.Status == ActionStatus.Paused) E.Resume(s);
        Assert.That(E.Advance(s).Status, Is.EqualTo(SegmentStatus.Running), "战后继续不补暂停");

        // 工作已因其他原因暂停：暂停期间进入濒死，继续后不补停
        var t = NearDying(47242);
        t.Player.Stats.Health = 0.3; t.Player.Stats.Satiety = 80; t.Player.Stats.Hydration = 80;
        Start(t, ActionRequest.Wait(60));
        E.Advance(t);
        t.Main!.Status = ActionStatus.Paused;
        t.Main.PauseReason = PauseReason.StatAlert;
        t.Main.PauseStat = StatId.Energy;
        Assert.That(E.DamageActor(t, t.Player, 1), Is.EqualTo(DamageOutcome.EnteredDying));
        Assert.That(E.Resume(t).Ok, Is.True);
        Assert.That(E.Advance(t).Status, Is.EqualTo(SegmentStatus.Running), "恢复后不补停");

        // 空闲：进入濒死后新开工作不补停
        var u = NearDying(47243);
        u.Player.Stats.Health = 0.3; u.Player.Stats.Satiety = 80; u.Player.Stats.Hydration = 80;
        Assert.That(E.DamageActor(u, u.Player, 1), Is.EqualTo(DamageOutcome.EnteredDying));
        Start(u, ActionRequest.Wait(60));
        Assert.That(E.Advance(u).Status, Is.EqualTo(SegmentStatus.Running));
        Assert.That(u.Log.Count(e => e.Text == GameEngine.EnterDyingText("你")), Is.EqualTo(1));
    }

    /// <summary>
    /// Q681-A01：同一边界新濒死＋饱食水分归零＋健康高危：中央只一次濒死句，其他真实原因都照原日志；两人同段进入濒死按原触发顺序取首个，另一人只日志。
    /// </summary>
    [Test]
    public void Q681_A01_DyingFirst_OthersLogged_OnePause_OriginalOrder()
    {
        var s = NearDying(47251);
        E.DebugDying(s, "multi");
        E.DebugQuickParty(s, 2);
        var mate = s.Actors[s.Party.Single(id => id != GameState.PlayerId)];
        mate.Stats.Health = 0.05; mate.Stats.Satiety = 0; mate.Stats.Hydration = 0;
        mate.ZeroLatched[StatId.Satiety] = true; mate.ZeroLatched[StatId.Hydration] = true; mate.AlertLatched[StatId.Health] = true;
        mate.AlertLatched[StatId.Satiety] = true; mate.AlertLatched[StatId.Hydration] = true;
        Start(s, ActionRequest.Wait(60));
        var mark = s.Log.Count;
        var t0 = s.GameMinute;
        Assert.That(E.Advance(s).Status, Is.EqualTo(SegmentStatus.Paused));
        Assert.That(s.GameMinute - t0, Is.EqualTo(5), "不多推进世界段");
        var ev = Since(s, mark);
        Assert.That(ev.Count(e => e.Kind == "ActionPaused"), Is.EqualTo(1), "只停一次");
        Assert.That(ev.Single(e => e.Kind == "ActionPaused").Text, Is.EqualTo($"{s.Main!.Label}已暂停：你进入濒死状态。"), "濒死优先，同级按原触发顺序（主角在前）");
        Assert.That(s.Main.PauseReason, Is.EqualTo(PauseReason.Dying));
        var alerts = ev.Where(e => e.Kind == "Alert").Select(e => e.Text).ToList();
        Assert.That(alerts, Does.Contain(E.AlertText(StatId.Satiety, true)).And.Contain(E.AlertText(StatId.Hydration, true)).And.Contain(E.AlertText(StatId.Health, false)), "其他真实原因照原日志");
        Assert.That(ev.Count(e => e.Text == GameEngine.EnterDyingText("你")), Is.EqualTo(1));
        Assert.That(ev.Count(e => e.Text == GameEngine.EnterDyingText(E.ActorDisplayName(s, mate.Id))), Is.EqualTo(1), "另一人进入濒死只日志、锁存");
        Assert.That(mate.DyingNotified, Is.True);
    }
}
