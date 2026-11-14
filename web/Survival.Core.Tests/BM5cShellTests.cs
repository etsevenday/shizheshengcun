using Survival.Core.Engine;

namespace Survival.Core.Tests;

/// <summary>
/// BM5c 网页壳接线（R220：S14「2026-11-14 Q605：正式入战清窗口，探索随机与到期分流」Q605-A01、A06；逐字稿 Q605）。
/// 同 BM4aPanelReturnTests 以源码检查：真实入战关闭最高顶栏页与整条返回链、战后不恢复；探索遭遇短提示 1200／600／1800 毫秒、
/// 点击／Enter／Space 只跳提示、ESC 打开菜单冻结计时、提示期间不放敌方一步、不接受指令、读档不补播。
/// </summary>
public class BM5cShellTests
{
    [Test]
    public void Q605_A01_BattleOnsetClosesTopPanelAndChain()
    {
        var session = Web("Services", "GameSession.cs");
        var notify = Slice(session, "public void Notify()", "Changed?.Invoke();");
        Assert.That(notify.IndexOf("OnBattleOnset();", StringComparison.Ordinal), Is.GreaterThanOrEqualTo(0));
        Assert.That(notify.IndexOf("OnBattleOnset();", StringComparison.Ordinal), Is.LessThan(notify.IndexOf("ReconcilePanelChain();", StringComparison.Ordinal)), "先关页再按原规则收链");
        var p = Web("Services", "GameSession.EncounterPrompt.cs");
        var onset = Slice(p, "private void OnBattleOnset()", "private void NoteEncounterPrompt");
        Assert.That(onset, Does.Contain("S.Battle.BattleId").And.Contain("CloseAllPanels();").And.Contain("Pop = null;").And.Contain("IgniteMenuOpen = false;").And.Contain("PendingAction = null;"));
        Assert.That(onset, Does.Not.Contain("Resume").And.Not.Contain("RunLoop"), "战后不自动续工");
        Assert.That(Slice(session, "private void ResetUi()", "// ───────────── 选择"), Does.Contain("ResetEncounterPrompt();"));
    }

    [Test]
    public void Q605_A06_PromptBeforeBattle_TimingSkipEsc()
    {
        var p = Web("Services", "GameSession.EncounterPrompt.cs");
        var show = Slice(p, "private async Task ShowEncounterPrompt()", "public void SkipEncounterPrompt()");
        Assert.That(show, Does.Contain("GameEngine.ExploreEncounterText").And.Contain("SingleSegmentShowMs").And.Contain("if (!FxPaused) elapsed += now - last;").And.Contain("_fxEndedMs = NowMs;"));
        Assert.That(show, Does.Contain("ExploreEncounter: true"), "只给探索遭遇的场次");
        Assert.That(show, Does.Not.Contain("E.Advance").And.Not.Contain("BattleStep"), "提示 0 额外世界时间、不放敌方一步");
        Assert.That(GameEngine.ExploreEncounterText, Is.EqualTo("探索中遭遇敌人，即将进入战斗。"));
        var session = Web("Services", "GameSession.cs");
        var run = Slice(session, "private async Task RunLoop()", "public void AckDialogue(string id)");
        Assert.That(run.IndexOf("await ShowEncounterPrompt();", StringComparison.Ordinal), Is.LessThan(run.LastIndexOf("await BattleLoop();", StringComparison.Ordinal)));
        Assert.That(Slice(session, "public void AckDialogue(string id)", "private int _toastSeq;"), Does.Contain("_ = PromptThenBattle();"));
        Assert.That(Slice(session, "public async Task BattleLoop()", "var at = S.Battle.Cursor"), Does.Contain("BattleReadingPause || EncounterPrompt != null"));
        Assert.That(session, Does.Contain("NoteEncounterPrompt(ev);"));
        // 主循环里 1200／600／1800 来自原单段展示时长
        Assert.That(session, Does.Contain("public int SingleSegmentShowMs => Presentation.SingleSegmentShowMs;"));

        var screen = Web("Components", "GameScreen.razor");
        var key = Slice(screen, "if (G.EncounterPromptActive)", "if (key == \"Escape\") { Escape(); return; }");
        Assert.That(key, Does.Contain("if (key == \"Skip\") G.SkipEncounterPrompt();").And.Contain("G.ToggleBattleMenu(true)").And.Contain("return;"));
        var battle = Web("Components", "BattleScreen.razor");
        Assert.That(battle, Does.Contain("var lockedUi = dlg || fxLock || G.EncounterPromptActive;"), "提示期间战斗画面 inert");
        Assert.That(battle, Does.Contain("class=\"enc-prompt").And.Contain("data-active").And.Contain("@onclick=\"G.SkipEncounterPrompt\""));
        Assert.That(battle.IndexOf("enc-prompt", StringComparison.Ordinal), Is.LessThan(battle.IndexOf("<MenuOverlay Battle=\"true\" />", StringComparison.Ordinal)), "菜单在提示之上");
        var js = Web("wwwroot", "js", "app.js");
        Assert.That(js, Does.Contain(".enc-prompt[data-active]"), "Enter／Space 只跳提示，ESC 交给游戏");
        Assert.That(js.IndexOf(".menu-modal", StringComparison.Ordinal), Is.LessThan(js.IndexOf(".enc-prompt[data-active]", StringComparison.Ordinal)), "菜单开着时按键归菜单");
        var css = Web("wwwroot", "css", "battle.css");
        Assert.That(css, Does.Contain("@media (prefers-reduced-motion: reduce) { .enc-prompt-text { animation: none; } }"), "减少动态效果为静态提示");
    }

    private static string Slice(string text, string from, string to)
    {
        var a = text.IndexOf(from, StringComparison.Ordinal);
        Assert.That(a, Is.GreaterThanOrEqualTo(0), from);
        var b = text.IndexOf(to, a, StringComparison.Ordinal);
        Assert.That(b, Is.GreaterThan(a), to);
        return text[a..b];
    }

    private static string Root()
    {
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "web", "Survival.sln"))) dir = dir.Parent;
        Assert.That(dir, Is.Not.Null, "找到仓库根目录");
        return dir!.FullName;
    }

    private static string Web(params string[] parts) => File.ReadAllText(Path.Combine(new[] { Root(), "web", "Survival.Web" }.Concat(parts).ToArray()));
}
