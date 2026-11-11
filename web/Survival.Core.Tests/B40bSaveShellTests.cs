using System.Text.RegularExpressions;
using Survival.Core.Save;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v1.0 B40b：多存档网页壳接线、界面与说明（需求 R134、R143、R149）。规则出处：S01「2026-11-10 Q335 多存档定稿」、
/// 「B40 Q381–Q389 实装裁定」、「B40 Q403–Q411 第二轮定稿」。测试工程不引用网页壳，接线处读组件源码确认；
/// 玩家可见的定稿句与策划案原文逐字比对。
/// </summary>
public class B40bSaveShellTests
{
    private static string Root()
    {
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "doc", "策划案"))) dir = dir.Parent;
        Assert.That(dir, Is.Not.Null, "找不到仓库根目录");
        return dir!.FullName;
    }

    private static string Web(params string[] parts) => File.ReadAllText(Path.Combine(new[] { Root(), "web", "Survival.Web" }.Concat(parts).ToArray()));

    private static string S01() => File.ReadAllText(Directory.GetFiles(Path.Combine(Root(), "doc", "策划案"), "S01 世界时间、行动与存档系统.md", SearchOption.AllDirectories).Single());

    // ───────────── Q388 正式说明 ─────────────

    /// <summary>Q388：「保存与继续」三段与三处菜单说明句逐字取自 S01；系统说明不再写旧单槽口径。</summary>
    [Test]
    public void Q388_TextsVerbatim()
    {
        var doc = S01();
        Assert.That(SaveSlots.HelpParagraphs, Has.Length.EqualTo(3));
        foreach (var p in SaveSlots.HelpParagraphs) Assert.That(doc, Does.Contain(p));
        foreach (var p in new[] { SaveSlots.BattleMenuNote, SaveSlots.ExtractMenuNote, SaveSlots.EncounterMenuNote }) Assert.That(doc, Does.Contain(p));
        var help = Web("Services", "HelpCatalog.cs");
        Assert.That(help, Does.Contain("Paragraphs = (e, s) => Survival.Core.Save.SaveSlots.HelpParagraphs,"));
        Assert.That(help, Does.Not.Contain("游戏只有一个存档"));
        var menu = Web("Components", "MenuOverlay.razor");
        Assert.That(menu, Does.Contain("@SaveSlots.BattleMenuNote").And.Contain("@SaveSlots.EncounterMenuNote").And.Contain("@SaveSlots.ExtractMenuNote"));
        Assert.That(menu, Does.Not.Contain("每条指令与敌方行动后都已自动保存"));
    }

    /// <summary>Q388：存档相关状态实际提交才计未保存；页面在有未保存进度时请求原生离开确认，不定制文案。</summary>
    [Test]
    public void Q388_BeforeUnloadWiring()
    {
        var js = Web("wwwroot", "js", "app.js");
        Assert.That(js, Does.Contain("window.addEventListener('beforeunload'").And.Contain("if (!window.__survivalUnsaved) return;"));
        Assert.That(js, Does.Contain("setUnsaved: function (u) { window.__survivalUnsaved = !!u; }"));
        var g = Web("Services", "GameSession.cs");
        Assert.That(g, Does.Contain("var u = Saves?.HasUnsavedProgress == true;"));
        Assert.That(Regex.IsMatch(g, @"(?<![A-Za-z.])Save\(\);"), Is.False, "旧单槽 Save() 全部换成 Commit()");
        Assert.That(Regex.IsMatch(g, @"if \(!_skip\)\s*\{\s*Commit\(\);\s*Notify\(\);\s*\}"), Is.True, "推进中每段提交");
        Assert.That(g, Does.Contain("CommitSegment();"), "跳过动画时逐段提交（跨 06:00 那一段要抓到）");
    }

    // ───────────── Q335 / Q408 计时与写权限 ─────────────

    /// <summary>Q335 / Q381：页面每秒驱动现实计时；其他页面改了存档存储时重新确认写权限；适配器注册进依赖注入。</summary>
    [Test]
    public void Q335_TimerAndPermissionWiring()
    {
        var js = Web("wwwroot", "js", "app.js");
        Assert.That(js, Does.Contain("ev.key.indexOf('survival.save.v2.') === 0) bridge.invokeMethodAsync('OnStorage')"));
        Assert.That(js, Does.Contain("setInterval(function () { bridge.invokeMethodAsync('OnTimer'); }, 1000);"));
        Assert.That(Web("Program.cs"), Does.Contain("builder.Services.AddScoped<BrowserSaveStorage>();"));
        Assert.That(Web("Components", "Root.razor"), Does.Contain("survival.initSaves"));
        var g = Web("Services", "GameSession.cs");
        Assert.That(g, Does.Contain("if (!Saves.Tick(Saves.SessionId, NowMs) || Running || BattleBusy) return;"), "到期且在空闲边界才立即写");
        Assert.That(g, Does.Contain("if (Saves.CheckPermission() != before) Notify();"));
    }

    /// <summary>Q381 / Q408-4 / Q387 / Q406：底栏先写权限句与「在此页面继续保存」，再按槽失败句与「重试」（失权时禁用），否则「已保存 {时:分}」。</summary>
    [Test]
    public void Q406_FooterOrder()
    {
        var screen = Web("Components", "GameScreen.razor");
        var perm = screen.IndexOf("<span>@sv.PermissionNotice </span>");
        var reclaim = screen.IndexOf("@Survival.Core.Save.SaveSlots.ReclaimButton");
        var fails = screen.IndexOf("@foreach (var f in autoFails)");
        var saved = screen.IndexOf("SaveSlots.SavedPrefix + G.LocalTime(savedMs, false)");
        Assert.That(perm, Is.GreaterThan(0));
        Assert.That(perm, Is.LessThan(reclaim));
        Assert.That(reclaim, Is.LessThan(fails));
        Assert.That(fails, Is.LessThan(saved));
        Assert.That(screen, Does.Contain("@if (sv.CanReclaim)"), "拒绝接回后隐藏接回键（Q408-1）");
        Assert.That(screen, Does.Contain("disabled=\"@(sv!.RetryBlockedText != null)\""), "失权时重试禁用（Q408-4）");
        Assert.That(SaveSlots.SavedPrefix, Is.EqualTo("已保存 "));
    }

    /// <summary>Q381：无写权限时「保存并返回标题」与确认重置禁用，原因同底栏。</summary>
    [Test]
    public void Q381_WriteButtonsDisabledWithoutPermission()
    {
        var menu = Web("Components", "MenuOverlay.razor");
        Assert.That(Regex.Matches(menu, Regex.Escape("|| G.Saves?.WriteBlockedText != null)\" title=\"@(lockTip ?? G.Saves?.WriteBlockedText)\" @onclick=\"ToTitle\"")).Count, Is.EqualTo(2), "常态与战斗层菜单");
        Assert.That(Web("Components", "DungeonReset.razor"), Does.Contain("G.Saves?.WriteBlockedText != null)").And.Contain("title=\"@(p.Reason ?? G.Saves?.WriteBlockedText ??"));
    }

    // ───────────── Q387 保存并返回标题、菜单三行 ─────────────

    /// <summary>Q387：「保存并返回标题」成功才回标题；失败留在当前菜单显示失败句与「重试」；三行时刻在存档位置句下。</summary>
    [Test]
    public void Q387_ExitAndMenuRows()
    {
        var g = Web("Services", "GameSession.cs");
        var exit = g.Substring(g.IndexOf("public void SaveAndExit()"), 400);
        Assert.That(exit, Does.Contain("if (r.Ok)").And.Contain("BackToTitle();").And.Contain("ExitSaveProblem = r.Failure?.Text ?? r.BlockedText;"));
        var menu = Web("Components", "MenuOverlay.razor");
        Assert.That(menu, Does.Contain("private void ToTitle() => G.SaveAndExit();"));
        Assert.That(Regex.Matches(menu, "<ExitSaveStatus />").Count, Is.EqualTo(2));
        Assert.That(Web("Components", "ExitSaveStatus.razor"), Does.Contain("@onclick=\"G.SaveAndExit\">@SaveSlots.RetryButton</button>"));
        var note = menu.IndexOf("@ShellText.SaveNote");
        var a = menu.IndexOf("SaveSlots.MenuAuto5Prefix + G.SlotTimeText(sv.Auto5Time())");
        var d = menu.IndexOf("SaveSlots.MenuDailyPrefix + G.SlotTimeText(sv.DailyTime())");
        var m = menu.IndexOf("SaveSlots.MenuManualPrefix + G.SlotTimeText(sv.LatestManualTime())");
        Assert.That(note, Is.LessThan(a));
        Assert.That(a, Is.LessThan(d));
        Assert.That(d, Is.LessThan(m));
        Assert.That(g, Does.Contain("public string SlotTimeText(SlotTime t) => SaveSlots.MenuTimeText(t, ms => LocalTime(ms, true));"), "菜单三行走内核同一规则（Q413）");
    }

    // ───────────── Q385 / Q405 / Q406 手动存档 ─────────────

    /// <summary>Q385：入口只在普通游戏菜单（战斗、敌踪层与死亡结算菜单没有），不可用时显示第一条命中的原因。</summary>
    [Test]
    public void Q385_ManualEntry()
    {
        var menu = Web("Components", "MenuOverlay.razor");
        var battleEnd = menu.IndexOf("return;");
        var entry = menu.IndexOf("@onclick=\"OpenManual\">@SaveSlots.ManualPageTitle</button>");
        Assert.That(entry, Is.GreaterThan(battleEnd), "战斗层菜单（含敌踪）不提供入口");
        Assert.That(menu, Does.Contain("var manualWhy = sv.ManualBlockedText(G.S);"));
        Assert.That(menu, Does.Contain("@if (manualWhy != null) { <div class=\"tiny muted\">@manualWhy</div> }"));
        Assert.That(menu, Does.Contain("@if (manualPage && !dead)"));
        var deadGate = menu.LastIndexOf("@if (!dead)", entry);
        Assert.That(deadGate, Is.GreaterThan(battleEnd), "死亡结算菜单不提供入口");
    }

    /// <summary>Q405 / Q406：子页列表整条可点进覆盖确认；新建／满额句；成功句；失败只留子页可重试；「返回」回菜单并清失败。</summary>
    [Test]
    public void Q405_ManualPage()
    {
        var page = Web("Components", "ManualSavePage.razor");
        Assert.That(page, Does.Contain("<button type=\"button\" class=\"slot-card @(entry.Readable ? \"\" : \"unreadable\")\" @onclick=\"() => AskOverwrite(entry)\">"), "已有档（含不可读）整条按钮，可键盘激活");
        Assert.That(page, Does.Contain("@SaveSlots.OverwriteConfirmTitle").And.Contain("@SaveSlots.OverwriteConfirmButton").And.Contain("@SaveSlots.CancelButton"));
        Assert.That(page, Does.Contain("<SaveSlotCard Entry=\"confirmEntry\" />"), "覆盖确认带同卡摘要");
        Assert.That(page, Does.Contain("@SaveSlots.ManualFullText").And.Contain("@SaveSlots.ManualNewButton"));
        Assert.That(page, Does.Contain("status = r.Ok ? SaveSlots.ManualSavedText : r.BlockedText;"));
        Assert.That(page, Does.Contain("else if (status != null && status != why)"), "被拒句与顶部失权句相同时只显示一行（B40 收尾）");
        Assert.That(page, Does.Contain("@if (sv.ManualFailure is { } fail)").And.Contain("@onclick=\"Retry\">@SaveSlots.RetryButton</button>"));
        Assert.That(page, Does.Contain("@onclick=\"OnBack\">@SaveSlots.BackButton</button>"));
        var menu = Web("Components", "MenuOverlay.razor");
        var leave = menu.Substring(menu.IndexOf("private void LeaveManual()"), 120);
        Assert.That(leave, Does.Contain("G.LeaveManualPage();").And.Contain("manualPage = false;"));
        Assert.That(SaveSlots.ManualSavedText, Is.EqualTo("已保存到手动存档。"));
        Assert.That(SaveSlots.ManualFullText, Is.EqualTo("手动存档已满。请选择一份覆盖。"));
    }

    // ───────────── Q335 / Q384 / Q409 / Q411 标题页与新局 ─────────────

    /// <summary>Q384 / Q411：「继续游戏」始终可点打开「存档选择」；空态句；点选只预览，「读取存档」直接加载，「返回」回标题；标题页无升级预告。</summary>
    [Test]
    public void Q384_PickerWiring()
    {
        var t = Web("Components", "StartScreen.razor");
        Assert.That(t, Does.Contain("<button class=\"secondary\" @onclick=\"OpenPicker\">@ShellText.ContinueGame</button>"), "无 disabled");
        Assert.That(t, Does.Contain("@SaveSlots.PickerTitle").And.Contain("@SaveSlots.EmptyText").And.Contain("SaveSlots.AutoSection").And.Contain("SaveSlots.ManualSection"));
        Assert.That(t, Does.Contain("@onclick=\"() => Pick(entry)\""));
        Assert.That(t, Does.Contain("<button class=\"primary\" disabled=\"@(!CanLoad)\" @onclick=\"Load\">@SaveSlots.LoadButton</button>"));
        Assert.That(t, Does.Contain("loadError = G.LoadSlot(selected);"));
        Assert.That(t, Does.Contain("@onclick=\"ClosePicker\">@SaveSlots.BackButton</button>"));
        Assert.That(t, Does.Not.Contain("SaveUpgradeFrom").And.Not.Contain("将升级"));
        var card = Web("Components", "SaveSlotCard.razor");
        Assert.That(card, Does.Contain("Entry.SavedAtMs is long ms ? G.LocalTime(ms, true) : SaveSlots.TimeNotRecorded"));
        Assert.That(card, Does.Contain("@SaveSlots.EndedText"));
        var g = Web("Services", "GameSession.cs");
        Assert.That(g, Does.Contain("if (r.UpgradeNotice != null) Notice = r.UpgradeNotice;"), "Q410 一次性升级提示");
    }

    /// <summary>Q335 / Q386 / Q409：标题新游戏按每5分钟槽是否有数据确认，局中「重新开始…」总确认，终局重开同确认、取消保留结算卡；终局失败在结算卡可重试。</summary>
    [Test]
    public void Q409_NewGameConfirmAndTerminal()
    {
        var t = Web("Components", "StartScreen.razor");
        Assert.That(t, Does.Contain("if (G.NewGameNeedsConfirm(false)) confirmNew = true;"));
        var menu = Web("Components", "MenuOverlay.razor");
        Assert.That(menu, Does.Contain("@onclick=\"() => confirmNew = true\">@SaveSlots.RestartMenuButton</button>"));
        Assert.That(menu, Does.Contain("@SaveSlots.NewGameConfirmText").And.Contain("@onclick=\"Restart\">@SaveSlots.NewGameConfirmButton</button>"));
        Assert.That(menu, Does.Not.Contain("重新开始会清除当前存档"));
        var end = Web("Components", "EndScreens.razor");
        Assert.That(end, Does.Contain("if (G.NewGameNeedsConfirm(false)) confirmRestart = true;"));
        Assert.That(end, Does.Contain("@if (G.Saves?.TerminalFailure is { } tf)").And.Contain("@onclick=\"G.RetryTerminal\">@SaveSlots.RetryButton</button>"));
        Assert.That(end, Does.Not.Contain("ClearSave"));
        Assert.That(SaveSlots.RestartMenuButton, Is.EqualTo("重新开始…"));
    }
}
