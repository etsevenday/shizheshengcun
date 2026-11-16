using System.Text.RegularExpressions;
using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发记录 BM9「次要问题合并」（R254 Q704—Q706、R255 Q707、R256 Q708—Q710、R270 Q841—Q842）。
/// 规则出处：00 总纲／S14／R03／R09／N02／05 页面流程文首「2026-11-15 Q704—Q710」节、S08 文首「2026-11-16 文案策划定稿 Q841—Q842」；
/// 正式句见《重墨 V2 开工裁定与界面逐字稿》同名两节与 assets/20261115/concise-copy-and-keys-q704-q710.json。网页壳接线以源码检查（同 BM8Tests）。
/// </summary>
public class BM9Tests : B41cTestBase
{
    // ───────────── 逐字稿 Q704—Q710 正式句（与 JSON copy 键同名） ─────────────
    private const string ConfirmBadge = "[空格]";
    private const string ConfirmHint = "执行当前操作一次。";
    private const string AimGuide = "选好目标，点击或按空格执行；右键/ESC取消。";
    private const string AimDefault = "悬停亮起的敌人看预估，右栏看属性；点击或空格执行，ESC取消。";
    private const string FirstBattle = "选攻击技能，悬停亮起的敌人看预估，点击或空格执行。防御、结束行动需再确认。";
    private const string TutorialNote = "轮到我方时使用当前人物的AP。选技能后，悬停亮起的敌人看预估，右栏看属性；←/→切目标，点击或空格执行。自用或无目标指令需确认；对我方施放时点亮起的成员。AP用尽或确认“结束行动”后，战斗继续。";
    private const string HelpParallel = "并列选项不自动代选；Tab选择后再激活。";
    private const string HelpConfirm = "空格执行当前唯一确认。编辑、勾选、分支和菜单按当前控件操作。中央行动可用空格跳过动画，战斗出手不可跳过。";
    private const string HelpCompatibility = "Enter兼容确认，也可在支持提交的数量框内使用；空格在编辑框内用于输入。";
    private const string CardQuantityHelp = "选中物品后，按数字键进入数量框；点确认执行，ESC退出。";
    private const string BattleAttackHelp = "选攻击技能，亮起的敌人可作为目标。悬停或←/→切目标，查看预计生命与敌人旁的战斗信息浮框；右栏看详细属性，预测详情看逐项来源。点击目标、点“执行{指令名}”或按空格执行。未选技能时点敌人只查看；只剩一名敌人也不自动攻击。右键/ESC取消瞄准，不耗AP。";
    private const string BattleNoTargetHelp = "防御、调息、装填、撤退需选中后再确认；按钮为“确认 · {指令名} · {AP}AP”，结束行动为“确认结束行动 · 剩余{AP}AP清零”。“医疗与物品”内药品仅当前行动者自用，战术物品每人每完整回合限1次，均需确认。对我方施放的技能，选中后点亮起的成员。";
    private const string BattleReadingHelp = "“战斗信息”查看对象和预测详情；“战报”看最近5条，点记录或“全记录”看本场完整记录，均只读。“系统说明”从右上“菜单”打开，阅读时敌方暂停。1—5选技能，Q/E切武器页，←/→切目标，空格执行或确认，ESC逐层返回；不能借此脱战。";

    // ───────────── Q704 瞄准与无目标引导 ─────────────

    /// <summary>Q704-A01：战斗信息区瞄准指引、默认攻击引导、无目标／结束行动引导为正式精简句，不带 Enter／回车；瞄准指引覆盖全部调用位置。</summary>
    [Test]
    public void Q704_A01_AimAndConsoleGuides()
    {
        Assert.That(ShellAim.Guide, Is.EqualTo(AimGuide));
        Assert.That(ShellAim.ConsoleGuide("确认结束行动 · 剩余2AP清零"), Is.EqualTo("点击“确认结束行动 · 剩余2AP清零”或按空格确认；ESC取消。"));
        Assert.That(ShellAim.ConsoleGuide(ShellAim.ConsoleConfirmText(false, "防御", 1)), Is.EqualTo("点击“确认 · 防御 · 1AP”或按空格确认；ESC取消。"), "变量为实际确认按钮的完整业务文字");
        Assert.That(AimDefaultHintSrc(), Is.EqualTo(AimDefault));
        foreach (var t in new[] { ShellAim.Guide, ShellAim.ConsoleGuide("x"), AimDefaultHintSrc() })
            Assert.That(t, Does.Not.Contain("Enter").And.Not.Contain("回车"), t);
        var panel = Web("Components", "BattlePanel.razor");
        Assert.That(Regex.Matches(panel, Regex.Escape("@ShellAim.Guide<")).Count, Is.EqualTo(2), "战斗信息区两处调用都换新句");
        Assert.That(panel, Does.Not.Contain("ShellAim.Guide("));
    }

    // ───────────── Q705 [空格] 提示、悬停与快捷键帮助 ─────────────

    /// <summary>Q705-A01／A02：关键确认旁「[空格]」、悬停「执行当前操作一次。」；数量编辑中整个提示隐藏、不标 Enter；中央跳过同一提示样式。</summary>
    [Test]
    public void Q705_A01_A02_BadgeAndHiddenWhileEditing()
    {
        Assert.That(ShellKeys.ConfirmBadge, Is.EqualTo(ConfirmBadge));
        Assert.That(ShellKeys.ConfirmKeysTitle, Is.EqualTo(ConfirmHint));
        var cap = Web("Components", "EnterCap.razor");
        Assert.That(cap, Does.Contain("@if (On && !Edit)"), "不可用或编辑中不显示");
        Assert.That(cap, Does.Contain("<kbd class=\"kc kc-hint\" title=\"@ShellKeys.ConfirmKeysTitle\">@ShellKeys.ConfirmBadge</kbd>"));
        Assert.That(cap, Does.Not.Contain("@ShellKeys.Enter").And.Not.Contain("@ShellKeys.SpaceCap"), "游玩中不再有 Enter 键帽");
        Assert.That(Web("Components", "CentralProgress.razor"), Does.Contain("@ShellKeys.SkipAnim <kbd class=\"kc kc-hint\">@ShellKeys.ConfirmBadge</kbd>"), "中央「跳过动画」旁同一提示样式");
        Assert.That(Web("wwwroot", "css", "shell.css"), Does.Contain(".kc.kc-hint, .wasteland kbd.kc.kc-hint { border: 0; padding: 0; }"));
        // 对白唯一继续／结束同一组件
        Assert.That(Web("Components", "DialogueLayer.razor"), Does.Contain("@(more ? \"继续\" : \"结束对话\") <EnterCap On=\"true\" /></button>"));
        // 锅层多确认区沿 Q584 不标无焦点默认键
        Assert.That(Web("Components", "FireMenu.razor"), Does.Contain("<EnterCap On=\"@(go && cap)\" Edit=\"@_qtyEditing\" />"));
    }

    /// <summary>Q705-A03：键盘快捷键主题表旁第二句、通用帮助、三行主键逐字；Enter 兼容说明只在该主题出现一次。</summary>
    [Test]
    public void Q705_A03_KeyHelpAndSingleCompatibility()
    {
        Assert.That(ShellKeyHelp.Notes[1], Is.EqualTo(HelpParallel));
        Assert.That(ShellKeyHelp.Notes, Has.Some.EqualTo(HelpConfirm));
        Assert.That(ShellKeyHelp.Notes.Count(n => n == HelpCompatibility), Is.EqualTo(1));
        Assert.That(ShellKeys.CompatibilityHelp, Is.EqualTo(HelpCompatibility));
        var rows = ShellKeyHelp.Groups().SelectMany(g => g.Rows).Select(r => r.Label).ToList();
        Assert.That(rows, Has.Some.EqualTo("当前确认 空格").And.Some.EqualTo("继续／结束对话 空格").And.Some.EqualTo("执行（当前指令） 空格"));
        Assert.That(rows, Has.None.Contain("Enter"), "键表主键不带「Enter也可」");
        // 除该主题一处兼容说明外，其他帮助正文、悬停与引导常量都不提 Enter／回车（只看字符串字面量，注释不算）
        var visible = (Literals(Web("Services", "HelpCatalog.cs")) + Literals(Src("src", "Core", "Engine", "ShellRules.cs")))
            .Replace("public const string Enter = \"Enter\";", "");   // 内部键名常量，不显示
        Assert.That(Regex.Matches(visible, "Enter").Count, Is.EqualTo(1), "只剩键盘快捷键主题的兼容说明");
        Assert.That(visible, Does.Not.Contain("回车"));
        Assert.That(Src("data", "config.v0.6.json"), Does.Not.Contain("Enter也可").And.Not.Contain("回车"), "配置里的教学附注与任务提示不提 Enter");
    }

    /// <summary>Q705-A03：卡片数量操作段、指令一览攻击／无目标／阅读三段为正式全文，其余段保留。</summary>
    [Test]
    public void Q705_A03_HelpParagraphs()
    {
        var help = Web("Services", "HelpCatalog.cs");
        foreach (var t in new[] { CardQuantityHelp, BattleAttackHelp, BattleNoTargetHelp, BattleReadingHelp })
            Assert.That(help, Does.Contain($"\"{t}\""), t);
        Assert.That(help, Does.Contain("ShellKeys.BattleNoSkipHelp,"), "战斗表现帮助段保留");
        Assert.That(help, Does.Contain("通用行动里的“调整站位”只有一张卡"), "调整站位段保留");
    }

    // ───────────── Q706 首战 PRO04 与 tutorial_arrive ─────────────

    /// <summary>Q706-A01：PRO04 顶部 hint 与任务 guidance 同一正式句；tutorial_arrive 只换 guide 附注，正文、说话人、noteKind 不变；任务目标与完成句不变。</summary>
    [Test]
    public void Q706_A01_FirstBattleTexts()
    {
        var t = Cfg.Prologue.Tasks.Single(x => x.Id == "PRO04");
        Assert.That(t.Hint, Is.EqualTo(FirstBattle));
        Assert.That(t.Guidance, Is.EqualTo(FirstBattle));
        Assert.That((t.Goal, t.Done), Is.EqualTo(("处理农野荒地的游荡尸，战斗结束时活着。", "已战胜首战敌人。")), "任务目标与完成句不变");
        var d = Cfg.Story.Dialogues.Single(x => x.Id == "tutorial_arrive");
        Assert.That(d.Note, Is.EqualTo(TutorialNote));
        Assert.That((d.Text, d.Speaker, d.NoteKind), Is.EqualTo(("等等。它走路的声音不对。你看它的脚。", "饱饱", "guide")));
    }

    // ───────────── Q707 濒死候补健康红线与键表两行 ─────────────

    /// <summary>Q707-A01／A02：濒死候补／驻屋健康红线静音（锁存照置）、病毒红线照常；键表当前确认与继续／结束对话主键空格、说明列不变。</summary>
    [Test]
    public void Q707_A01_A02_ReserveMuteAndKeyRows()
    {
        var party = Src("src", "Core", "Engine", "GameEngine.Party.cs");
        Assert.That(party, Does.Contain("请回{where}救治。\", IsDying(s, actor));"), "健康红线对濒死者 mute");
        Assert.That(party, Does.Contain("if (on && !latched)\n                {\n                    actor.RedLatched[key] = true;\n                    if (!mute) Emit("), "锁存照置，只是不写提醒");
        Assert.That(party, Does.Contain("公共照护只能补给和休养，请回去处理。\");"), "病毒红线不传 mute");
        var local = ShellKeyHelp.Groups().Single(g => g.Title == "局部操作").Rows;
        Assert.That(local.Single(r => r.Label == "当前确认 空格").Note, Is.EqualTo("仅当前界面有唯一可用主确认时执行；多个并列选项不代选。"));
        Assert.That(local.Single(r => r.Label == "继续／结束对话 空格").Note, Is.EqualTo("只推进当前一句对白；有分支时先明确选择。"));
    }

    // ───────────── 逐字稿对照 ─────────────

    /// <summary>逐字稿 Q704—Q710 表：实现里的正式句与表行逐字一致。</summary>
    [Test, Category("DesignSync")]
    public void Q704_Q710_MatchVerbatimTable()
    {
        var doc = Verbatim();
        var at = doc.IndexOf("## 2026-11-15 Q704—Q710", StringComparison.Ordinal);
        var sec = doc[at..doc.IndexOf("\n## ", at + 10, StringComparison.Ordinal)];
        foreach (var t in new[] { ConfirmBadge, ConfirmHint, AimGuide, AimDefault, FirstBattle, TutorialNote, HelpParallel, HelpConfirm, HelpCompatibility, CardQuantityHelp, BattleAttackHelp, BattleNoTargetHelp, BattleReadingHelp,
                     "点击“{实际确认按钮业务文字}”或按空格确认；ESC取消。", "当前确认 空格", "继续／结束对话 空格", "执行（当前指令） 空格" })
            Assert.That(sec, Does.Contain($" | {t} |"), t);
    }

    // ───────────── 源码读取 ─────────────

    private static string AimDefaultHintSrc()
    {
        var bs = Web("Components", "BattleScreen.razor");
        const string key = "public const string AimDefaultHint = \"";
        var at = bs.IndexOf(key, StringComparison.Ordinal) + key.Length;
        return bs.Substring(at, bs.IndexOf('"', at) - at);
    }

    /// <summary>源文件里去掉 // 与 /// 注释行后的内容（只留可能显示给玩家的字符串所在行）。</summary>
    private static string Literals(string src) => string.Join("\n", src.Split('\n').Where(l => !l.TrimStart().StartsWith("//")));

    private static string Root()
    {
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "AGENTS.md"))) dir = dir.Parent;
        return dir!.FullName;
    }

    private static string Src(params string[] parts) => File.ReadAllText(Path.Combine(new[] { Root() }.Concat(parts).ToArray())).Replace("\r\n", "\n");

    private static string Web(params string[] parts) => Src(new[] { "web", "Survival.Web" }.Concat(parts).ToArray());

    private static string Verbatim() => File.ReadAllText(Directory.GetFiles(Path.Combine(Root(), "doc", "策划案"), "重墨 V2 开工裁定与界面逐字稿.md", SearchOption.AllDirectories).Single()).Replace("\r\n", "\n");
}
