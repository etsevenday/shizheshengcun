using Survival.Core.Engine;

namespace Survival.Core.Tests;

/// <summary>
/// BM5c 网页壳接线与正式句（R232：R05 Q622—Q624；R220：S14「2026-11-14 Q605：正式入战清窗口，探索随机与到期分流」Q605-A01、A06；逐字稿 Q605）。
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

    /// <summary>Q622-A01：五种排除句与正常风险行只换探索分支，优先级与条件不变；地牢／旧档保护／清理原句保持。</summary>
    [Test]
    public void Q622_A01_ExplorePreviewSentences_ClearKept()
    {
        var r05 = R05();
        foreach (var t in new[] { "首战胜利前不会触发探索随机遭遇。", "安全区不会触发探索随机遭遇。", "城市与公共地点不会触发探索随机遭遇。", "当前已有一组敌踪，本次探索不会触发探索随机遭遇。" })
            Assert.That(r05, Does.Contain($"| {t} |"), t);
        Assert.That(r05, Does.Contain("| 从第{n}日起，探索途中才可能触发随机遭遇。 |"));
        Assert.That(r05, Does.Contain("探索正常风险行改“本次探索途中随机遭遇概率{概率}（本地敌对{数量}/上限{上限}）；随机遭遇会短暂提示后直接进入战斗。”"));
        var src = Slice(File.ReadAllText(Path.Combine(Root(), "src", "Core", "Engine", "GameEngine.EnemyCards.cs")), "public string GroupCardPreviewNote(", "// ───────────── 统一卡视图");
        var order = new[] { "TutorialGateOpen(s)", "NoRandomBeforeDay", "en.SafeLocations.Contains(loc)", "lc.IsDungeon", "EncounterGraceUntil", "VisibleGroupCardAt(s, loc)", "EncounterChance(s, loc, type)" }
            .Select(x => src.IndexOf(x, StringComparison.Ordinal)).ToList();
        Assert.That(order, Is.All.GreaterThanOrEqualTo(0));
        Assert.That(order, Is.Ordered, "原优先顺序不变");
        Assert.That(src, Does.Contain("\"地牢房间按固定敌踪处理。\"").And.Contain("return LegacyGraceText;"));
    }

    /// <summary>Q623-A01：帮助第1段不动，第2段探索、第3段清理（只删「探索或」），后续段原相对顺序。</summary>
    [Test]
    public void Q623_A01_HelpExploreThenClear()
    {
        var help = Web("Services", "HelpCatalog.cs");
        var entry = Slice(help, "Id = \"encounter\"", "Id = \"rounds\"");
        var first = entry.IndexOf("\"敌人是世界里已有的个体", StringComparison.Ordinal);
        var explore = entry.IndexOf("ExploreEncounterHelp,", StringComparison.Ordinal);
        var clear = entry.IndexOf("在有敌对敌人的地点完成清理后可能发现一组敌踪。先结算行动结果，再显示敌踪卡。你可以先做其他事。\" + EnemyTimingHelp,", StringComparison.Ordinal);
        var cards = entry.IndexOf("\"在第二轨的「敌踪」中选择卡片", StringComparison.Ordinal);
        Assert.That(new[] { first, explore, clear, cards }, Is.All.GreaterThanOrEqualTo(0));
        Assert.That(new[] { first, explore, clear, cards }, Is.Ordered, "先探索、后清理，其余后移");
        Assert.That(entry, Does.Not.Contain("完成探索或清理后"), "旧混写段不重复");
        var line = help.Split((char)10).Single(l => l.Contains("public const string ExploreEncounterHelp"));
        var text = line[(line.IndexOf('"') + 1)..line.LastIndexOf('"')];
        Assert.That(R05(), Does.Contain($"**探索段正式全文：**“{text}”"), "探索段与 R05 Q623 逐字一致");
    }

    /// <summary>Q624-A01：旧在办缺检查段的探索在中央推进条（暂停层嵌同一组件）行动名说明可读旧句；鼠标、键盘、触屏可达。</summary>
    [Test]
    public void Q624_A01_LegacyNoteOnActionName()
    {
        Assert.That(GameEngine.LegacyExploreNote, Is.EqualTo("本次探索沿原开工规则，在结束时检查随机遭遇。"));
        Assert.That(R05(), Does.Contain("“本次探索沿原开工规则，在结束时检查随机遭遇。”"));
        var cp = Web("Components", "CentralProgress.razor");
        var head = Slice(cp, "@if (p.LabelNote is { } labelNote)", "<span class=\"cp-chip");
        Assert.That(head, Does.Contain("<button type=\"button\" class=\"cp-name cp-name-note\" title=\"@labelNote\"").And.Contain("aria-label=\"@($\"{p.Label}。{labelNote}\")\"").And.Contain("@onclick"));
        Assert.That(cp, Does.Contain("<p class=\"cp-note cp-label-note\">@shownNote</p>"));
        Assert.That(Web("Components", "PauseLayer.razor"), Does.Contain("<CentralProgress InFlow=\"true\""), "暂停层嵌同一组件");
    }

    private static string R05() => File.ReadAllText(Directory.GetFiles(Path.Combine(Root(), "doc", "策划案"), "R05 敌人卡、种群刷新与遭遇系统.md", SearchOption.AllDirectories).Single());

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
