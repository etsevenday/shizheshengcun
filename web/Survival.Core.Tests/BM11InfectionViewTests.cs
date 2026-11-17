using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发记录 BM11「次要问题合并」：R04 文首「2026-11-17 B49两个原批与B51c1单题」§Q843 A、§Q844 A、§Q845 B、§Q846 A、§Q847 A；
/// 验收 SMALL-Q843-01、Q844-01、Q845-01／02／03、Q846-01、Q847-01（S14 本批），同源合同 assets/20261117/infection-view-and-return-copy-q843-q847-q882.json。
/// </summary>
public class BM11InfectionViewTests : TestBase
{
    private const string Lin = "npc_lin_zhao";

    protected override bool NoEnemyApproach => true;

    private static string Web(params string[] parts)
    {
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "web", "Survival.sln"))) dir = dir.Parent;
        return File.ReadAllText(Path.Combine(new[] { dir!.FullName, "web", "Survival.Web" }.Concat(parts).ToArray())).Replace("\r\n", "\n");
    }

    /// <summary>主角＋林昭对一只游荡尸（同 B49UiTests.Q742_A02 布场）：主角免疫 0、林昭免疫 100。</summary>
    private GameState Fight(uint seed)
    {
        var s = New(seed);
        E.DebugQuickParty(s, 2);
        E.DebugTeleport(s, "field");
        s.WorldEnemies.Clear();
        var id = E.DebugSpawnEnemy(s, "shambler", "field").Id;
        Assert.That(E.DebugStartBattle(s, new[] { id }, GameEngine.FearEncounter), Is.True);
        s.Battle!.Allies = new List<string> { GameState.PlayerId, Lin };
        s.Actors[GameState.PlayerId].Body.Immunity = 0;
        s.Actors[Lin].Body.Immunity = 100;
        return s;
    }

    // ───────────── Q843 ─────────────

    /// <summary>SMALL-Q843-01：生存提示饱饱两句合成“饱饱使主角病毒累积；免疫≥{阈值}时停止。”，阈值读 fullSuppressImmunityAtLeast（80）。</summary>
    [Test]
    public void Q843_01_BaobaoTip_Merged_ThresholdFromConfig()
    {
        var tips = Cfg.Progress.Tips.Where(t => t.StartsWith(GameEngine.ExposureRiskTipText)).ToList();
        Assert.That(tips, Has.Count.EqualTo(1));
        Assert.That(tips[0], Is.EqualTo("传播途径见敌人详情；免疫越低，同一途径越易感染。饱饱使主角病毒累积；免疫≥{baobaoSuppressImmunity}时停止。"));   // R04 §Q843 A／逐字稿 baobaoTip
        Assert.That(Cfg.Infection.BaobaoInput.FullSuppressImmunityAtLeast, Is.EqualTo(80));
        Assert.That(Cfg.Progress.Tips, Has.None.Contains("饱饱也会让体内的病毒缓慢累积"));
        Assert.That(Web("Services", "Ui.cs"), Does.Contain("tip.Replace(\"{baobaoSuppressImmunity}\", T(f.BaobaoInput.FullSuppressImmunityAtLeast))"));
    }

    // ───────────── Q844 ─────────────

    /// <summary>SMALL-Q844-01：组内两名有源成员各列一次同一我方参考标题＋本人途径条件概率；无源成员无概率组；界面逐成员用同一参考（不全员展开）。</summary>
    [Test]
    public void Q844_01_PerSourceMember_OwnGroup_SameReference()
    {
        var s = New(84401);
        var shamb = E.EnemyExposureInfoFor(s, E.Enemy("shambler"), "游荡尸", GameState.PlayerId);
        var bloat = E.EnemyExposureInfoFor(s, E.Enemy("z_bloater"), "膨胀感染者", GameState.PlayerId);
        Assert.That((shamb.HasSource, bloat.HasSource), Is.EqualTo((true, true)));
        Assert.That(shamb.ReferenceTitle, Is.EqualTo("你 · 命中且正伤害后："));   // R04 Q740 reference 逐字
        Assert.That(bloat.ReferenceTitle, Is.EqualTo(shamb.ReferenceTitle), "同一我方参考");
        Assert.That(shamb.Estimates, Has.Count.EqualTo(2));
        Assert.That(bloat.Estimates, Is.Not.Empty);
        Assert.That(bloat.Estimates, Is.Not.EqualTo(shamb.Estimates), "各成员本人途径");
        var human = Cfg.Enemies.First(x => !E.HasInfectionSource(x));
        var none = E.EnemyExposureInfoFor(s, human, human.Name, GameState.PlayerId);
        Assert.That((none.HasSource, none.ReferenceTitle, none.Estimates.Count), Is.EqualTo((false, (string?)null, 0)), "无源无概率组");
        var ed = Web("Components", "EnemyDetail.razor");
        Assert.That(ed, Does.Contain("<ExposureLines Info=\"@e.EnemyExposureInfoFor(s, e.Enemy(m.EnemyId), e.Enemy(m.EnemyId).Name, xref)\" />"), "逐成员、同一 xref");
    }

    // ───────────── Q845 ─────────────

    /// <summary>SMALL-Q845-01：主角行动时明确查看林昭，参考立即换林昭（本人 I）；FocusId／AP 不变。没有明确查看默认当前行动者。</summary>
    [Test]
    public void Q845_01_ExplicitView_SwitchesReference_ActorUnchanged()
    {
        var s = Fight(84501);
        var b = s.Battle!;
        Assert.That(b.FocusId, Is.EqualTo(GameState.PlayerId), "布场：主角是当前行动者");
        var ap0 = b.PlayerAp;
        Assert.That(E.InBattleExposureReference(s), Is.EqualTo(GameState.PlayerId), "无明确查看：默认当前行动者");
        Assert.That(E.InBattleExposureReference(s, Lin), Is.EqualTo(Lin));
        var info = E.EnemyExposureInfoFor(s, E.Enemy("shambler"), "游荡尸", E.InBattleExposureReference(s, Lin));
        Assert.That(info.ReferenceTitle, Is.EqualTo("林昭 · 命中且正伤害后："));
        Assert.That(info.Estimates, Is.EqualTo(new[] { "咬伤：概率5%，感染后病毒20。", "抓伤／刮伤：概率2.5%，感染后病毒10。" }), "林昭免疫100的本人值（同 B49UiTests.Q742_A02）");
        Assert.That((b.FocusId, b.PlayerAp), Is.EqualTo((GameState.PlayerId, ap0)), "实际行动者／AP 不变");
    }

    /// <summary>SMALL-Q845-02：明确参考失效（离场／死亡）只列静态途径，不借别人概率；网页只在明确查看时设参考，Esc 清空与换场重置。</summary>
    [Test]
    public void Q845_02_InvalidReference_NoBorrow_WebWiring()
    {
        var s = Fight(84502);
        s.Battle!.Allies = new List<string> { GameState.PlayerId };   // 布场：林昭已不在本场
        Assert.That(E.InBattleExposureReference(s, Lin), Is.Null);
        var info = E.EnemyExposureInfoFor(s, E.Enemy("shambler"), "游荡尸", E.InBattleExposureReference(s, Lin));
        Assert.That((info.HasSource, info.ReferenceTitle, info.Estimates.Count), Is.EqualTo((true, (string?)null, 0)));
        Assert.That(E.InBattleExposureReference(s, GameState.PlayerId), Is.EqualTo(GameState.PlayerId), "配对：仍在场者有效");
        s.Progress.Dead = true;
        Assert.That(E.InBattleExposureReference(s, GameState.PlayerId), Is.Null, "已死失效");

        var gs = Web("Services", "GameSession.cs");
        Assert.That(gs.Split("BattleExposureRef = actorId;").Length - 1, Is.EqualTo(1), "只在明确查看时设");
        Assert.That(gs, Does.Contain("if (actorId != null) BattleExposureRef = actorId;"));
        Assert.That(gs.Split("BattleExposureRef = null;").Length - 1, Is.EqualTo(2), "Esc 清空查看、换场／离场重置");
        Assert.That(Web("Components", "BattlePanel.razor"), Does.Contain("e.InBattleExposureReference(s, G.BattleExposureRef)"));
    }

    /// <summary>SMALL-Q845-03：浏览参考只读——不抽签、不加病毒、不改真实状态。</summary>
    [Test]
    public void Q845_03_ViewIsReadOnly()
    {
        var s = Fight(84503);
        var r0 = s.Rng.State;
        var v0 = (s.Actors[Lin].Body.VirusLoad, s.Actors[GameState.PlayerId].Body.VirusLoad);
        E.EnemyExposureInfoFor(s, E.Enemy("shambler"), "游荡尸", E.InBattleExposureReference(s, Lin));
        Assert.That(s.Rng.State, Is.EqualTo(r0));
        Assert.That((s.Actors[Lin].Body.VirusLoad, s.Actors[GameState.PlayerId].Body.VirusLoad), Is.EqualTo(v0));
    }

    // ───────────── Q846 ─────────────

    /// <summary>SMALL-Q846-01：natural 句首无“每小时”，各区间仍带“/小时”，数值与表序不变。</summary>
    [Test]
    public void Q846_01_NaturalSentence_NoLeadingPerHour()
    {
        var p = E.VirusHelpParagraphs();
        Assert.That(p, Has.Count.EqualTo(6));
        Assert.That(p[2], Is.EqualTo("自然变化：主角感染后0≤免疫<40：+4/小时、40≤免疫<70：+2/小时、70≤免疫<80：+0.5/小时、80≤免疫≤100：−1.5/小时；同伴仅保留其中下降，正增长为0。未治愈时病毒最低1。"));   // R04 §Q846 A
        Assert.That(p[2], Does.Not.Contain("每小时"));
    }

    // ───────────── Q847 ─────────────

    /// <summary>SMALL-Q847-01：已感染且真实病毒≤10 隐藏“自然治愈还需”整行；＞10 显示；按未舍入值（10.004 显示 10.00 仍列）；未感染不列。</summary>
    [Test]
    public void Q847_01_NaturalCureRow_HiddenWhenSatisfied()
    {
        var s = New(84701);
        var b = s.Body;
        b.Infected = true;
        b.VirusLoad = 3.03;
        Assert.That(E.ShowNaturalCureLoadRow(b), Is.False);
        b.VirusLoad = 10;   // naturalCure.loadAtMost = 10（含等号）
        Assert.That(E.ShowNaturalCureLoadRow(b), Is.False);
        b.VirusLoad = 10.004;
        Assert.That(E.ShowNaturalCureLoadRow(b), Is.True, "未舍入真实值＞阈值");
        b.VirusLoad = 30;
        Assert.That(E.ShowNaturalCureLoadRow(b), Is.True);
        b.Infected = false;
        Assert.That(E.ShowNaturalCureLoadRow(b), Is.False);
        var ad = Web("Components", "AttrDetail.razor");
        Assert.That(ad, Does.Contain("@if (e.ShowNaturalCureLoadRow(b)) { <div><span>自然治愈还需</span>"));
        Assert.That(ad, Does.Contain("<div><span>连续高免疫</span>"), "连续高免疫行照常");
    }
}
