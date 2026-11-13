using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>BM4a 核对 R205（Q552、Q553、Q555）：现状已按答复，只补测试。</summary>
public class BM4aEnemyCardAnswerTests : B41cTestBase
{
    private GameState EliteHere(uint seed = 5201)
    {
        var cfg = Cfg.WorldEnemies.LegacyElites.Single(x => x.EnemyId == "z_guard");
        var s = New(seed);
        s.Progress.Flags.Add("hub_registered");
        s.GameMinute = At(3, 9, 0);
        s.SpawnNextCheck = int.MaxValue;
        s.PatrolNextCheck = int.MaxValue;
        E.DebugMapTeleport(s, cfg.NodeId);
        s.WorldEnemies.RemoveAll(w => w.FixedOf == null);
        E.DebugSetProgress(s, cfg.NodeId, 100);
        return s;
    }

    /// <summary>Q552-A01：可选强敌卡悬停按通用操作句→完整特殊掉落说明→真实状态句，无空行、不补必须战胜句。</summary>
    [Test]
    public void Q552_A01_EliteHoverOrder_NoEmptyLine()
    {
        var s = EliteHere();
        var card = E.EnemyCardsHere(s).Single();
        var lines = E.EnemyCardHoverLines(s, card);
        Assert.That(lines[0], Is.EqualTo(GameEngine.EnemyCardFootnote));
        Assert.That(lines[1], Is.EqualTo(E.EliteDropNote(card)));
        Assert.That(lines.Skip(2), Is.EqualTo(new[] { E.CardApproachStatus(s, card) }.Where(x => x != null)), "状态在最后，没有就不造行");
        Assert.That(lines, Has.None.Matches<string>(string.IsNullOrWhiteSpace));
        Assert.That(lines, Has.No.Member(GameEngine.KeyEnemyDetailNote));
    }

    /// <summary>Q553-A01／A02：悬停与详情同源保留「可选强敌 · 单局一次 · 」完整前缀（详情复用同一说明，不另叠身份行）。</summary>
    [Test]
    public void Q553_EliteDropPrefixSameSourceInDetail()
    {
        var s = EliteHere();
        var card = E.EnemyCardsHere(s).Single();
        Assert.That(E.EliteDropNote(card), Does.StartWith("可选强敌 · 单局一次 · 特殊掉落"));
        var detail = File.ReadAllText(Path.Combine(Root(), "web", "Survival.Web", "Components", "EnemyDetail.razor"));
        Assert.That(detail, Does.Contain("@if (e.EliteDropNote(card) is string drop) { <li class=\"ecard-drop\">@drop</li> }"));
    }

    /// <summary>Q555-A01／A02：撤退成功率预览与结算同源，都不含先手×1.5（首轮行动队列仍用×1.5）；预览行不附含先手括注。</summary>
    [Test]
    public void Q555_RetreatIgnoresFirstStrike_PreviewSameSource()
    {
        var (s, key) = OneShambler();
        Assert.That(E.StartStealth(s, key, "sneak").Ok, Is.True);
        AdvanceUntil(s, t => t.Battle?.FirstStrike == "ally");
        var b = s.Battle!;
        Assert.That(E.FirstStrikeRoundSpeedFactor(b, GameState.PlayerId), Is.EqualTo(1.5), "本轮队列速度有先手");
        var noFs = SaveSerializer.Deserialize(SaveSerializer.Serialize(s), CfgV2);
        noFs.Battle!.FirstStrike = null;
        noFs.Battle!.FirstStrikeDamage?.Clear();
        Assert.That(E.RetreatChance(s, b).chance, Is.EqualTo(E.RetreatChance(noFs, noFs.Battle!).chance).Within(1e-12), "撤退率不乘先手");
        Assert.That(E.PlayerEffectiveSpeed(s, b), Is.EqualTo(E.PlayerSpeedParts(s, b).Speed), "预览速度即结算所用、不含先手");
        var panel = File.ReadAllText(Path.Combine(Root(), "web", "Survival.Web", "Components", "BattlePanel.razor"));
        var line = panel.Substring(panel.IndexOf("<span>撤退成功率</span>", StringComparison.Ordinal));
        line = line.Substring(0, line.IndexOf("</div></div>", StringComparison.Ordinal));
        Assert.That(line, Does.Contain("e.PlayerEffectiveSpeed(s, b)").And.Contain("Select(e.EnemyEffectiveSpeed)"));
        Assert.That(line, Does.Not.Contain("先手"));
    }

    private static string Root()
    {
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "web", "Survival.sln"))) dir = dir.Parent;
        Assert.That(dir, Is.Not.Null, "找到仓库根目录");
        return dir!.FullName;
    }
}
