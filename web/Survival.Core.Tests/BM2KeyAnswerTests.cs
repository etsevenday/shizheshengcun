using System.Text.RegularExpressions;
using Survival.Core.Engine;
using Survival.Core.Save;

namespace Survival.Core.Tests;

/// <summary>
/// BM2 次要问题合并（需求 R178；策划提交 ae87ef7 答复 B43 的 Q495—Q502）：S14「B43 Q495—Q502 键表、确认入口与按键例外」，
/// 验收 Q495-A01、Q496-A01、Q497-A01—A02、Q498-A01、Q499-A01、Q500-A01、Q501-A01、Q502-A01—A02；原句以重墨 V2 逐字稿同日节为准。
/// 文字与原因顺序在内核 <see cref="ShellKeyHelp"/>、<see cref="ShellAim"/>、<see cref="ShellKeys"/>、<see cref="ShellEsc"/>；网页壳接线按源码核对。
/// </summary>
public class BM2KeyAnswerTests : TestBase
{
    private static string Root()
    {
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "doc", "策划案"))) dir = dir.Parent;
        Assert.That(dir, Is.Not.Null, "找不到仓库根目录");
        return dir!.FullName;
    }

    private static string Web(string relative) => File.ReadAllText(Path.Combine(Root(), "web", "Survival.Web", relative)).Replace("\r\n", "\n");

    private static string Script() => Web("wwwroot/js/app.js");

    private static string Verbatim() => File.ReadAllText(Path.Combine(Root(), "doc", "策划案", "重墨像素风界面与卡片 v0.9", "重墨 V2 开工裁定与界面逐字稿.md"));

    private static string S14() => File.ReadAllText(Directory.GetFiles(Path.Combine(Root(), "doc", "策划案"), "S14 主界面与全局UI交互系统.md", SearchOption.AllDirectories).Single());

    /// <summary>去掉 Razor 注释 @* … *@ 与 C#/JS 行注释后的源码（只留玩家可能看到的文字与标记）。</summary>
    private static string Visible(string src)
    {
        src = Regex.Replace(src, @"@\*.*?\*@", "", RegexOptions.Singleline);
        src = Regex.Replace(src, @"/\*.*?\*/", "", RegexOptions.Singleline);
        return string.Join("\n", src.Split('\n').Select(l => Regex.Replace(l, @"(^|\s)//.*$", "")));
    }

    // ───────────── Q495 系统说明「键盘快捷键」 ─────────────

    /// <summary>Q495-A01：条目名、导语、四个区名与表旁三句逐字；顶栏 12 行与按钮原文同源；未交付的战斗两级跳过不列。</summary>
    [Test]
    public void Q495_A01_KeyHelp_SameSourceAndVerbatim()
    {
        Assert.That(ShellKeyHelp.Title, Is.EqualTo("键盘快捷键"));
        Assert.That(ShellKeyHelp.Lead, Is.EqualTo("这里列出已可用的键盘操作。按键与对应按钮执行相同操作；按钮不可用时，快捷键也不可用。输入文字或打开菜单、对白、确认窗口时，按键优先作用于当前界面。"));
        var groups = ShellKeyHelp.Groups();
        Assert.That(groups.Select(g => g.Title), Is.EqualTo(new[] { "顶栏", "局部操作", "战斗", "动画跳过" }));
        Assert.That(groups[0].Rows.Select(r => r.Label), Is.EqualTo(Enumerable.Range(0, 12).Select(ShellKeys.TopLabel)), "顶栏 12 行与按钮原文同源");
        Assert.That(groups[0].Rows.All(r => r.Note == null));
        var all = groups.SelectMany(g => g.Rows).ToList();
        Assert.That(all.Select(r => r.Label), Has.Some.EqualTo("执行（当前指令） Enter"), "BM3（Q509）：帮助表固定泛称");
        Assert.That(all.Select(r => r.Label), Has.None.Contain("{"), "BM3（Q509）：不显示花括号占位符");
        Assert.That(all.Select(r => r.Label), Has.Some.EqualTo("跳过动画 空格"));
        Assert.That(all.Select(r => r.Label), Has.None.Contain("跳到结果").And.None.Contain("跳过余下"), "战斗两级跳过未交付，不列为可用");
        Assert.That(all.Select(r => r.Label), Has.None.Contain(" F"), "F 不恢复");
        Assert.That(ShellKeyHelp.Notes, Is.EqualTo(new[]
        {
            "字母键不区分大小写。",
            "多个并列选项不会由 Enter 自动代选；可用 Tab 选择控件后再激活。",
            "仅显示“×”的关闭键和手动存档页的“返回”按钮不标 ESC，仍可按 ESC 按界面层级返回。",
        }));
    }

    /// <summary>Q495-A01：局部操作、战斗、动画跳过各行的标签与说明逐字出现在逐字稿 Q495 表里（标签由 ShellKeys 键帽拼出也须一致）。</summary>
    [Test, Category("DesignSync")]
    public void Q495_A01_KeyHelp_MatchesVerbatimTable()
    {
        var doc = Verbatim();
        foreach (var g in ShellKeyHelp.Groups().Skip(1))
            foreach (var r in g.Rows)
                Assert.That(doc, Does.Contain($"| {g.Title} | {r.Label} | {r.Note} |"), r.Label);   // BM3（Q509）：战斗区第四行逐字稿已改「执行（当前指令） Enter」
        Assert.That(doc, Does.Contain($"> {ShellKeyHelp.Lead}"));
        foreach (var n in ShellKeyHelp.Notes) Assert.That(doc, Does.Contain($"「{n}」"), n);
    }

    /// <summary>Q495-A01：H 的「入门与操作」在「界面分区」之后新增「键盘快捷键」，后面原条目顺序不变；系统说明按分区渲染键表与补充句。</summary>
    [Test]
    public void Q495_A01_HelpCatalogWiring()
    {
        var cat = Web("Services/HelpCatalog.cs");
        int screen = cat.IndexOf("Id = \"screen\""), keys = cat.IndexOf("Id = \"keys\", Title = ShellKeyHelp.Title"), cards = cat.IndexOf("Id = \"cards\"");
        Assert.That(screen, Is.GreaterThan(0));
        Assert.That(keys, Is.GreaterThan(screen), "在「界面分区」之后");
        Assert.That(cards, Is.GreaterThan(keys), "「卡片、选择与转移」等原条目顺序不变");
        var entry = cat.Substring(keys, cards - keys);
        Assert.That(entry, Does.Contain("new[] { ShellKeyHelp.Lead }").And.Contain("KeyGroups = ShellKeyHelp.Groups").And.Contain("After = ShellKeyHelp.Notes"));
        var help = Web("Components/HelpOverlay.razor");
        Assert.That(help, Does.Contain("@foreach (var g in entry.KeyGroups())").And.Contain("<dt>@r.Label</dt>").And.Contain("<dd>@r.Note</dd>"));
        Assert.That(help, Does.Contain("@foreach (var para in entry.After)"));
        // 游戏菜单与标题页「玩法说明」的键表继续同源
        Assert.That(Web("Components/MenuOverlay.razor"), Does.Contain("快捷键：@ShellKeys.TopTable()"));
        Assert.That(Web("Components/StartScreen.razor"), Does.Contain("快捷键：@ShellKeys.TopTable()"));
    }

    // ───────────── Q496 菜单悬停 ─────────────

    /// <summary>Q496-A01：「菜单 ESC」悬停为正式句；顶栏、全屏战斗、结算页三处同源；点击仍开关菜单。</summary>
    [Test]
    public void Q496_A01_MenuHover()
    {
        Assert.That(ShellKeys.MenuHover, Is.EqualTo("点击打开或关闭菜单。按 ESC 时先处理当前可关闭层；没有可关闭层时打开菜单。"));
        Assert.That(Web("Components/GameScreen.razor"), Does.Contain("data-menu-key aria-keyshortcuts=\"Escape\" title=\"@ShellKeys.MenuHover\" @onclick=\"ToggleMenu\">@TopFace(11)</button>"));
        Assert.That(Web("Components/BattleScreen.razor"), Does.Contain("@onclick=\"() => G.ToggleBattleMenu()\" aria-keyshortcuts=\"Escape\" title=\"@ShellKeys.MenuHover\">@ShellText.Menu <kbd class=\"kc\">@ShellKeys.Esc</kbd></button>"));
        Assert.That(Web("Components/EndScreens.razor"), Does.Contain("title=\"@ShellKeys.MenuHover\" aria-keyshortcuts=\"Escape\" @onclick=\"() => G.SetMenu(true)\""));
        foreach (var f in new[] { "GameScreen", "BattleScreen", "EndScreens" })
            Assert.That(Web($"Components/{f}.razor"), Does.Not.Contain("菜单（ESC）").And.Not.Contain("保存并返回标题；不能脱战"), f);
    }

    // ───────────── Q497 瞄准预测区的明确提交 ─────────────

    /// <summary>Q497：按钮名、三条正式原因、指引与只读提示逐字。</summary>
    [Test]
    public void Q497_Texts()
    {
        Assert.That(ShellAim.ConfirmLabel("拳击"), Is.EqualTo("执行拳击"));
        Assert.That(ShellAim.NeedTarget, Is.EqualTo("请先指定目标。"));
        Assert.That(ShellAim.NoTargets, Is.EqualTo("没有可用目标。"));
        Assert.That(ShellAim.TargetChanged, Is.EqualTo("目标已变化，请重新选择。"));
        Assert.That(ShellAim.Guide("拳击"), Is.EqualTo("指定目标后，可点击亮起的目标或“执行拳击”按钮，也可按 Enter 执行；右键/ESC 取消。"));
        Assert.That(ShellAim.ReadOnly, Is.EqualTo("查看预测不扣费、不抽随机。"));
        // BM3（Q512）取代原「确认 · {指令名} · {AP}AP」另拼写法：指引引用真实按钮业务文字（见 BM3AimAnswerTests）
        Assert.That(ShellAim.ConsoleGuide(ShellAim.ConsoleConfirmText(false, "防御", 1)), Is.EqualTo("在操作台点“确认 · 防御 · 1AP”才提交；ESC 取消。"));
    }

    /// <summary>Q497-A01：原因顺序——通用锁优先；无目标看候选数；失效目标具体句优先、否则「目标已变化」；从未合法的候选沿锚点原句；合法再看成本等预览；群体按真实集合。</summary>
    [Test]
    public void Q497_A01_ReasonOrder()
    {
        // 通用锁（未轮到、对白）最先，哪怕还没指定目标
        Assert.That(ShellAim.ConfirmBlocked("还没轮到你行动", false, 2, false, false, false, null, null), Is.EqualTo("还没轮到你行动"));
        Assert.That(ShellAim.ConfirmBlocked("", false, 2, true, true, true, null, null), Is.EqualTo(""), "锁定但无原句：仍禁用");
        // 尚未指定
        Assert.That(ShellAim.ConfirmBlocked(null, false, 2, false, false, false, null, null), Is.EqualTo(ShellAim.NeedTarget));
        Assert.That(ShellAim.ConfirmBlocked(null, false, 0, false, false, false, null, null), Is.EqualTo(ShellAim.NoTargets));
        // 已指定但失效：更具体句优先；无具体句才写目标已变化；不自动改选
        Assert.That(ShellAim.ConfirmBlocked(null, false, 1, true, false, true, "射程不足", null), Is.EqualTo("射程不足"));
        Assert.That(ShellAim.ConfirmBlocked(null, false, 1, true, false, true, null, null), Is.EqualTo(ShellAim.TargetChanged));
        Assert.That(ShellAim.ConfirmBlocked(null, false, 0, true, false, true, null, null), Is.EqualTo(ShellAim.TargetChanged), "候选为 0 时原目标失效仍写目标变化");
        // 指向从未合法的候选：真实原因
        Assert.That(ShellAim.ConfirmBlocked(null, false, 1, true, false, false, null, null), Is.EqualTo(ShellAim.OutOfScope));
        // 合法目标再看成本等
        Assert.That(ShellAim.ConfirmBlocked(null, false, 1, true, true, true, null, "行动点不足"), Is.EqualTo("行动点不足"));
        Assert.That(ShellAim.ConfirmBlocked(null, false, 1, true, true, true, null, null), Is.Null, "可提交");
        // 群体：按真实受影响集合，不要求指定
        Assert.That(ShellAim.ConfirmBlocked(null, true, 0, false, false, false, null, null), Is.EqualTo(ShellAim.NoTargets));
        Assert.That(ShellAim.ConfirmBlocked(null, true, 2, false, false, false, null, null), Is.Null);
        Assert.That(ShellAim.ConfirmBlocked(null, true, 2, false, false, false, null, "弹药不足"), Is.EqualTo("弹药不足"));
    }

    /// <summary>Q497-A01、A02：按钮在目标预测区底部（瞄准中、全屏战斗），业务名与 Enter 同一按钮；点击、Enter、点亮起目标走同一提交与去重；明确目标移出锚点保留。</summary>
    [Test]
    public void Q497_A01_A02_Wiring()
    {
        var panel = Web("Components/BattlePanel.razor");
        Assert.That(panel, Does.Contain("if (!G.BattleFullscreen || !G.BattleAiming) return;"), "无所选瞄准指令不出现");
        Assert.That(panel, Does.Contain("data-aim-confirm aria-keyshortcuts=\"Enter\" disabled=\"@(!st.Ok)\" title=\"@st.Why\""));
        Assert.That(panel, Does.Contain("@onclick=\"() => G.BattleAimSubmit(token)\">@ShellAim.ConfirmLabel(st.Name)<EnterCap On=\"@st.Ok\" /></button>"));
        Assert.That(panel, Does.Contain("<div class=\"bp-why warn-text\" role=\"status\">@st.Why</div>"), "禁用时显示第一条真实原因");
        Assert.That(Regex.Matches(panel, "@AimConfirm\\(").Count, Is.EqualTo(2), "未指定目标与已有预测两种情况都在预测区底部");
        Assert.That(panel, Does.Contain("int? hoverTarget = aiming && G.BattleAimTarget is int hv && aimSet.Contains(hv) ? hv : null;"), "预测跟随本次明确目标");
        Assert.That(panel, Does.Contain("@ShellAim.Guide(aimName)").And.Contain("@ShellAim.ReadOnly").And.Contain("ShellAim.ConsoleGuide(confirmText)"));
        Assert.That(panel, Does.Not.Contain("点击亮起的目标即执行；右键/Esc 取消"));

        var g = Web("Services/GameSession.cs");
        var confirm = g.Substring(g.IndexOf("public async Task BattleConfirmHover()"));
        confirm = confirm.Substring(0, confirm.IndexOf("\n    }"));
        Assert.That(confirm, Does.Contain("await BattleAimSubmit(BattleExecToken());"), "回车与按钮同一入口");
        var submit = g.Substring(g.IndexOf("public async Task BattleAimSubmit(string token)"));
        submit = submit.Substring(0, submit.IndexOf("\n    }"));
        Assert.That(submit, Does.Contain("var st = BattleAimConfirm();").And.Contain("if (!st.Ok) return;"));
        Assert.That(submit, Does.Contain("if (AreaAction) await ExecuteBattleCommand(token);"), "群体按真实集合提交");
        Assert.That(submit, Does.Contain("else if (st.Target is int t) await ClickBattleTarget(t, token);"), "与点亮起目标同一路径与请求号去重");
        var hover = g.Substring(g.IndexOf("public void HoverBattleTarget(int? index)"));
        hover = hover.Substring(0, hover.IndexOf("\n    }"));
        Assert.That(hover, Does.Contain("if (index is int i && BattleAiming"), "离开锚点（null）不清本次明确目标");
        Assert.That(g, Does.Contain("if (_battleAction != value) _aimSeq++;"), "取消／换指令／切页／提交／读档后旧目标作废");
        Assert.That(g, Does.Contain("$\"{_aimSeq}:{b.BattleId}:{b.Round}:{b.Cursor}:{b.ActiveAlly}:{BattleItem}\""), "换行动者、换轮次即作废，不存档");
        Assert.That(g, Does.Contain("var at = BattleAimTarget is int h ? list.IndexOf(h) : -1;"), "←/→ 从本次明确目标起切");
        // 原「点亮起的合法目标即执行」保留：没有二次确认
        var click = g.Substring(g.IndexOf("public async Task ClickBattleTarget(int index, string token)"));
        click = click.Substring(0, click.IndexOf("\n    }"));
        Assert.That(click, Does.Contain("await ExecuteBattleCommand(token);"));
        var screen = Web("Components/BattleScreen.razor");
        Assert.That(screen, Does.Contain("if (aiming && G.BattleAimTarget is int hov && aimSet.Contains(hov))"), "生命条预计条纹与预测同一目标");
        Assert.That(screen, Does.Contain("<span class=\"bx-hint\">@AimHint(sel) · 右键/ESC 取消</span>"));
        // 键盘：焦点在新按钮上时回车按原生语义（只点一次），不在控件上时交给 BattleConfirmHover；按住不重复
        var js = Script();
        Assert.That(js, Does.Contain("if (ev.repeat && k !== 'ArrowLeft' && k !== 'ArrowRight') return;"));
    }

    // ───────────── Q498、Q499 维持现状 ─────────────

    /// <summary>Q498-A01：仅「×」的四类关闭／取消控件不加 ESC 键帽、不另造文字按钮；原可访问名称保留。</summary>
    [Test]
    public void Q498_A01_CrossOnlyKeepsNoCap()
    {
        var crosses = new List<string>();
        foreach (var f in new[] { "AnchorPop", "StatusPop", "BattleScreen" })
            crosses.AddRange(Web($"Components/{f}.razor").Split('\n').Where(l => l.Contains(">×</button>")));
        Assert.That(crosses.Count, Is.GreaterThanOrEqualTo(5), "选卡／就近浮窗×、状态浮窗×两处、战斗底部取消×、快捷医疗盘×");
        foreach (var l in crosses)
        {
            Assert.That(l, Does.Not.Contain("EscCap").And.Not.Contain("<kbd"), l.Trim());
            Assert.That(l, Does.Contain("aria-label="), "可访问名称保留");
        }
    }

    /// <summary>Q499-A01：晾制、三城鸟瞰、安全屋、远征物资窗不设默认 Enter、不贴 Enter 键帽。</summary>
    [Test]
    public void Q499_A01_ParallelPanelsNoDefaultEnter()
    {
        foreach (var f in new[] { "DryingOverlay", "CityOverview", "SafehouseOverlay", "ExpeditionSeal" })
        {
            var src = Web($"Components/{f}.razor");
            Assert.That(Regex.IsMatch(src, "data-enter[^-]"), Is.False, f);
            Assert.That(src, Does.Not.Contain("EnterCap"), f);
        }
    }

    // ───────────── Q500 手动存档子页返回层级 ─────────────

    /// <summary>Q500-A01：菜单开在手动存档子页时 ESC 走子页（先收覆盖确认、再回首页），首页再按才关菜单；死亡结算不受影响。</summary>
    [Test]
    public void Q500_A01_EscRoute()
    {
        Assert.That(ShellEsc.Route(new ShellEscInput { MenuOpen = true, MenuSubPage = true }), Is.EqualTo(ShellEscRoute.CloseMenuSubPage));
        Assert.That(ShellEsc.Route(new ShellEscInput { MenuOpen = true }), Is.EqualTo(ShellEscRoute.CloseMenu), "菜单首页再按才关菜单");
        Assert.That(ShellEsc.Route(new ShellEscInput { MenuOpen = true, MenuSubPage = true, FeedbackOpen = true }), Is.EqualTo(ShellEscRoute.Ignore), "反馈窗口由窗口处理");
        Assert.That(ShellEsc.Route(new ShellEscInput { MenuOpen = true, MenuSubPage = true, DialogueLocked = true }), Is.EqualTo(ShellEscRoute.CloseMenuSubPage));
        Assert.That(ShellEsc.Route(new ShellEscInput { Dead = true, MenuOpen = true }), Is.EqualTo(ShellEscRoute.CloseMenu));
    }

    /// <summary>Q500-A01：「返回」仍只写「返回」无 ESC 键帽；ESC 与「返回」同一入口；覆盖确认先取消、不写盘；菜单开关都回到首页。</summary>
    [Test]
    public void Q500_A01_Wiring()
    {
        var page = Web("Components/ManualSavePage.razor");
        var back = page.Split('\n').Single(l => l.Contains("@onclick=\"OnBack\""));
        Assert.That(back.Trim(), Is.EqualTo("<button class=\"primary\" @ref=\"_back\" @onclick=\"OnBack\">@SaveSlots.BackButton</button>"), "「返回」不显示 ESC 键帽");
        Assert.That(page, Does.Contain("<button class=\"secondary\" @onclick=\"() => G.ManualConfirmKey = null\">@SaveSlots.CancelButton<EscCap On=\"true\" /></button>"), "覆盖确认的「取消」此刻承接 ESC");
        var screen = Web("Components/GameScreen.razor");
        Assert.That(screen, Does.Contain("MenuSubPage = !dead && G.ManualPageOpen,"));
        Assert.That(screen, Does.Contain("case ShellEscRoute.CloseMenuSubPage: G.ManualPageEscape(); break;"));
        var g = Web("Services/GameSession.cs");
        var esc = g.Substring(g.IndexOf("public void ManualPageEscape()"));
        esc = esc.Substring(0, esc.IndexOf("\n    }"));
        Assert.That(esc, Does.Contain("if (ManualConfirmKey != null) { ManualConfirmKey = null; Notify(); return; }"), "先只取消覆盖确认，不保存");
        Assert.That(esc, Does.Contain("CloseManualPage();").And.Not.Contain("SaveManual").And.Not.Contain("SetMenu"));
        var setMenu = g.Substring(g.IndexOf("public void SetMenu(bool open)"));
        setMenu = setMenu.Substring(0, setMenu.IndexOf("\n    }"));
        Assert.That(setMenu, Does.Contain("ManualPageOpen = false;").And.Contain("ManualConfirmKey = null;"), "菜单每次从首页开始");
        Assert.That(SaveSlots.BackButton, Is.EqualTo("返回"));
    }

    // ───────────── Q501 ESC 写法与帮助 ─────────────

    /// <summary>Q501-A01：条目标题与六段与 S14 逐字一致；不再写战斗菜单只有两项。</summary>
    [Test, Category("DesignSync")]
    public void Q501_A01_EscHelpVerbatim()
    {
        Assert.That(ShellKeyHelp.EscTitle, Is.EqualTo("ESC 与界面层级"));
        Assert.That(ShellKeyHelp.EscParagraphs.Count, Is.EqualTo(6));
        var s14 = S14();
        var verb = Verbatim();
        foreach (var p in ShellKeyHelp.EscParagraphs)
        {
            Assert.That(s14, Does.Contain($"> {p}"), p);
            Assert.That(verb, Does.Contain($"> {p}"), p);
        }
        Assert.That(string.Concat(ShellKeyHelp.EscParagraphs), Does.Not.Contain("保存并返回标题”"));
    }

    /// <summary>Q501-A01：系统说明条目接线；所有玩家可见文字（组件与说明正文，不含注释与内部 Escape 名）里按键词写「ESC」，不再有「Esc」。</summary>
    [Test]
    public void Q501_A01_NoLowercaseEscVisible()
    {
        var cat = Web("Services/HelpCatalog.cs");
        Assert.That(cat, Does.Contain("Id = \"esc\", Title = ShellKeyHelp.EscTitle").And.Contain("Paragraphs = (e, s) => ShellKeyHelp.EscParagraphs,"));
        var dir = Path.Combine(Root(), "web", "Survival.Web", "Components");
        var files = Directory.GetFiles(dir, "*.razor").Select(f => (Path.GetFileName(f), File.ReadAllText(f).Replace("\r\n", "\n"))).ToList();
        files.Add(("HelpCatalog.cs", cat));
        var bad = new List<string>();
        foreach (var (name, src) in files)
        {
            var v = Visible(src);
            foreach (Match m in Regex.Matches(v, @"(?<![.\w])Esc(?![a-zA-Z])"))
            {
                var line = v.Substring(0, m.Index).Count(c => c == '\n') + 1;
                bad.Add($"{name}:{line}");
            }
        }
        Assert.That(bad, Is.Empty, "玩家可见的「Esc」应写「ESC」：" + string.Join("、", bad));
        Assert.That(cat, Does.Not.Contain("战斗菜单只有"));
    }

    // ───────────── Q502 中央跳过只绑空格 ─────────────

    /// <summary>Q502-A01：按钮常驻「跳过动画 空格」，悬停原句不变，点击仍是原跳过；只声明 Space，不加 Enter。</summary>
    [Test]
    public void Q502_A01_SkipButton()
    {
        Assert.That(ShellKeys.SkipAnim, Is.EqualTo("跳过动画"));
        Assert.That(ShellKeys.SpaceCap, Is.EqualTo("空格"));
        Assert.That(ShellKeys.SkipAnimTitle, Is.EqualTo("只跳过表现，不改变游戏时间与结果"));
        var cp = Web("Components/CentralProgress.razor");
        Assert.That(cp, Does.Contain("<button type=\"button\" class=\"secondary cp-skip\" data-skip-anim aria-keyshortcuts=\"Space\" @onclick=\"G.SkipAnimation\" title=\"@ShellKeys.SkipAnimTitle\">@ShellKeys.SkipAnim <kbd class=\"kc\">@ShellKeys.SpaceCap</kbd></button>"));
        Assert.That(cp, Does.Contain("else if (G.Running && !dlg)"), "只在行动推进、非等待对白时出现（暂停时是「继续行动」）");
        var skipLine = cp.Split('\n').Single(l => l.Contains("cp-skip"));
        Assert.That(skipLine, Does.Not.Contain("data-enter").And.Not.Contain("EnterCap"), "不新增中央 Enter");
        var screen = Web("Components/GameScreen.razor");
        var onKey = screen.Substring(screen.IndexOf("public void OnKey(string key)"));
        onKey = onKey.Substring(0, onKey.IndexOf("/// <summary>"));
        Assert.That(onKey, Does.Not.Contain("SkipAnimation"), "中央跳过只由空格点同一按钮");
    }

    /// <summary>Q502-A01、A02：空格的受理边界——编辑／输入法、菜单、对白、模态、前景面板与浮窗、拖拽、战斗与其他聚焦控件优先；按住不重复；阻止滚动；点同一按钮一次。</summary>
    [Test]
    public void Q502_A02_SpaceRouting()
    {
        var js = Script();
        var at = js.IndexOf("if (k === ' ') {");
        Assert.That(at, Is.GreaterThan(0));
        var block = js.Substring(at, js.IndexOf("\n      }\n", at) - at);
        Assert.That(block, Does.Contain("document.querySelector('.cprog:not(.fading) [data-skip-anim]')"), "淡出旧帧不触发");
        Assert.That(block, Does.Contain("sk.disabled || !shown(sk) || (sk.closest && sk.closest('[inert]'))"), "不可用、看不见、对白锁（inert）不触发");
        Assert.That(block, Does.Contain("if (sae === sk || control(sae)) return;"), "焦点在该按钮或其他具体控件上按原生语义");
        Assert.That(block, Does.Contain("topModal() || topPanel() || document.querySelector('.bfs') || document.body.classList.contains('dragging')"));
        Assert.That(block, Does.Contain("document.querySelectorAll('.apop, .st-pop.pinned')"));
        Assert.That(block, Does.Contain("ev.preventDefault();\n        sk.click();"), "阻止滚动，点同一按钮一次");
        // 空格分支之前：输入法、反馈、菜单、对白、浮窗内按钮、数量框等弹窗、编辑控件、修饰键都已先返回
        var head = js.Substring(js.IndexOf("window.__survivalKeys = function (ev)"), at - js.IndexOf("window.__survivalKeys = function (ev)"));
        foreach (var guard in new[] { "if (ev.isComposing || ev.keyCode === 229) return;", "FeedbackWidget.isOpen()) return;", "if (document.querySelector('.menu-modal'))", "if (document.querySelector('.dlg-layer'))", "if (tgt && tgt.closest && tgt.closest('.modal')) return;", "if (editable(tgt))", "if (mod) return;" })
            Assert.That(head, Does.Contain(guard), guard);
        // 按住：捕获阶段挡掉非编辑区的重复空格
        Assert.That(js, Does.Contain("if (ev.key === 'Enter' || (ev.key === ' ' && !editable(ev.target)))"));
    }

    [Test]
    public void M761_AimPanelLayout_PredictionKeepsRoom()
    {
        // BM2 试玩修复（纯排版）：瞄准时 foot 限高自身可滚、预测区与系统提示显示滚动条、原因与指引句同为左对齐；不删不折叠文字
        var css = Web("wwwroot/css/shell.css");
        Assert.That(css, Does.Contain(".bfs-info .detail-body:has(.bp-cycle) > .detail-foot { flex: 0 0 auto; min-height: 0; overflow-y: auto;"), "foot 不压缩按钮、超出在自身内滚动");
        Assert.That(css, Does.Contain(".bfs-info .detail-body:has(.bp-cycle):has(.pv-route) > .detail-foot { max-height: var(--bp-foot-max); }"), "已指定目标时 foot 限高，把高度让给预测");
        foreach (var tier in new[] { "--bp-foot-max: 92px;", "--bp-foot-max: 78px;", "--bp-foot-max: 134px;" })
            Assert.That(css, Does.Contain(tier), tier);
        Assert.That(css, Does.Contain(".bfs-info:has(.bp-cycle) :is(.detail-scroll, .detail-foot, .bfs-hint) { scrollbar-width: thin; scrollbar-color:"), "滚动条可见");
        Assert.That(css, Does.Contain(".bfs-info:has(.bp-cycle) > .bfs-hint .tc-hint-btns { position: sticky; bottom: 0;"), "系统提示限高后两个按钮常驻");
        Assert.That(css, Does.Contain(".bfs-info .detail-body:has(.bp-cycle) > .detail-foot .under { margin-top: 3px; font-size: var(--fs-aux); line-height: 1.35; text-align: left; }"));
        Assert.That(css, Does.Contain(".bfs-info .detail-body:has(.bp-cycle) .bp-confirm .bp-why { text-align: left; }"), "「请先指定目标。」与下一行对齐统一");
        // 不用隐藏/折叠：新增块里没有 display:none、line-clamp、text-overflow
        var block = css.Substring(css.IndexOf("/* BM2 试玩修复（M761"), css.IndexOf("/* 方向键帽") - css.IndexOf("/* BM2 试玩修复（M761"));
        Assert.That(block, Does.Not.Contain("display: none").And.Not.Contain("line-clamp").And.Not.Contain("text-overflow").And.Not.Contain("overflow: hidden"));
    }
}
