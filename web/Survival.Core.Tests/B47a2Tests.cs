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
}
