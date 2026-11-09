using System.Text.RegularExpressions;
using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 B29.7 试玩修复。测试工程不引用网页壳，组件渲染测不了：M553 读 BattlePanel 源码核对敌踪分支不再读空的战斗状态，
/// 视角成员的取数由内核测。
/// </summary>
public class B29PlaytestFixTests : TestBase
{
    private static string Web(string relative)
    {
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "doc", "策划案"))) dir = dir.Parent;
        Assert.That(dir, Is.Not.Null, "找不到仓库根目录");
        return File.ReadAllText(Path.Combine(dir!.FullName, "web", "Survival.Web", relative));
    }

    [Test]
    public void M553_EncounterBranch_DoesNotDereferenceNullBattle()
    {
        var src = Web("Components/BattlePanel.razor");
        var start = src.IndexOf("@if (b == null && s.Encounter != null)", StringComparison.Ordinal);
        var end = src.IndexOf("else if (b != null)", StringComparison.Ordinal);
        Assert.That(start, Is.GreaterThanOrEqualTo(0));
        Assert.That(end, Is.GreaterThan(start));
        var branch = src.Substring(start, end - start);
        Assert.That(Regex.Matches(branch, @"(?<![\w.])b\.\w").Select(m => m.Value), Is.Empty, "敌踪分支里 b 恒为空，不能读它的成员");
        Assert.That(branch, Does.Contain("GameEngine.MeId(s)"));
    }

    [Test]
    public void M553_Encounter_ViewMemberIsPlayer_DodgeWithoutBattle()
    {
        var s = New();
        Assert.That(s.Battle, Is.Null);
        Assert.That(GameEngine.MeId(s), Is.EqualTo(GameState.PlayerId));
        Assert.That(E.MyBaseDodge(s), Is.EqualTo(Cfg.Scope.BasePlayerDodge));
    }
}
