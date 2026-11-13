using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 BM4a「顶栏覆盖与逐层返回」（R212：Q586、Q590；R205：Q552—Q555）。规则出处：S14「2026-11-13 B45 Q581—Q590试玩答复与下方对白布局」
/// §Q586、§Q590 及验收 Q586-A01—A02、Q590-A01—A03；正式句见《重墨 V2 开工裁定与界面逐字稿》同日节与「B41e2 Q552—Q555 补充逐字稿」。
/// 返回链层序用内核 <see cref="PanelReturnChain"/> 直接测；网页壳接线以源码检查（同 B45bFacMenuTests）。
/// </summary>
public class BM4aPanelReturnTests : TestBase
{
    // ───────────── Q590 返回链层序 ─────────────

    /// <summary>Q590-A01：火堆锅层→M地图→C制作，逐次返回：制作→地图→火堆，一次只退一层。</summary>
    [Test]
    public void Q590_A01_CoverThenReturnOneLayerAtATime()
    {
        var c = new PanelReturnChain();
        Assert.That(c.Cover("fire", "map"), Is.False, "新开地图");
        Assert.That(c.Cover("map", "craft"), Is.False, "新开制作");
        Assert.That(c.Under, Is.EqualTo(new[] { "fire", "map" }));
        Assert.That(c.Pop(), Is.EqualTo("map"), "第一次返回回地图");
        Assert.That(c.Pop(), Is.EqualTo("fire"), "第二次回原火堆菜单");
        Assert.That(c.Pop(), Is.Null, "再往下没有窗口：火堆菜单自己的返回规则接管");
    }

    /// <summary>Q590-A02：同一顶栏页已在下层时只提到最上层（沿用原状态），其余层保持相对顺序，不复制第二份。</summary>
    [Test]
    public void Q590_A02_SameKindUnderIsBroughtUpNotDuplicated()
    {
        var c = new PanelReturnChain();
        c.Cover("fire", "map");
        c.Cover("map", "craft");
        Assert.That(c.Cover("craft", "map"), Is.True, "地图原在下层：提上来、不新开");
        Assert.That(c.Under, Is.EqualTo(new[] { "fire", "craft" }), "火堆→制作→地图");
        Assert.That(c.Pop(), Is.EqualTo("craft"), "随后 ESC 回制作");
        Assert.That(c.Pop(), Is.EqualTo("fire"), "再回火堆");
        Assert.That(c.Cover("map", "map"), Is.False);
        Assert.That(c.Count, Is.EqualTo(0), "同页不入链");
    }

    /// <summary>Q590-A03：换场、读档等清理整条链（返回被清掉的层供壳丢草稿）；替换跳转重新打开的同类页从下层拿掉。</summary>
    [Test]
    public void Q590_A03_ClearAndRemove()
    {
        var c = new PanelReturnChain();
        c.Cover("fire", "map");
        c.Cover("map", "craft");
        Assert.That(c.Remove("fire"), Is.True);
        Assert.That(c.Under, Is.EqualTo(new[] { "map" }));
        Assert.That(c.Clear(), Is.EqualTo(new[] { "map" }));
        Assert.That(c.Count, Is.EqualTo(0));
        Assert.That(c.Pop(), Is.Null);
    }

    /// <summary>逐字稿 Q590：有下层「返回 ESC」、无下层「关闭 ESC」；系统说明「ESC 与界面层级」加入的正式段落逐字一致。</summary>
    [Test, Category("DesignSync")]
    public void Q590_CloseLabelsAndHelpParagraphVerbatim()
    {
        var verb = Verbatim();
        Assert.That(verb, Does.Contain("有下层时用“返回 ESC”、无下层用“关闭 ESC”"));
        var c = new PanelReturnChain();
        Assert.That(c.CloseLabelFor(), Is.EqualTo("关闭"));
        c.Cover("fire", "map");
        Assert.That(c.CloseLabelFor(), Is.EqualTo("返回"));
        Assert.That(verb, Does.Contain($"“{PanelReturnChain.EscHelpParagraph}”"));
        Assert.That(ShellKeyHelp.EscParagraphs, Has.Member(PanelReturnChain.EscHelpParagraph));
        Assert.That(verb, Does.Contain($"| {GameEngine.FacReturnGoneText} |"), "Q586 返回时设施失效句");
        Assert.That(verb, Does.Contain($"| {GameEngine.ContentChangedText} |"));
        Assert.That(GameEngine.FacGoneText, Is.EqualTo("这处设施已无法操作，请关闭后重新选择。"), "原仍开着菜单时的关闭提示不改");
    }

    // ───────────── Q590 网页壳接线 ─────────────

    /// <summary>Q590-A01／A02：顶栏按钮与快捷键走同一个覆盖打开（再按当前最上层的键＝关闭并返回下层）；只渲染最上层面板，按层记滚动与焦点。</summary>
    [Test]
    public void Q590_ShellTopBarCoversAndOnlyTopRenders()
    {
        var screen = Web("Components", "GameScreen.razor");
        Assert.That(screen, Does.Contain("private void Toggle(string panel) => G.OpenTopPanel(panel);"));
        Assert.That(screen, Does.Contain("<div class=\"panel-host\" data-panel=\"@G.Panel\""));
        Assert.That(screen, Does.Contain("survivalPanels.restore").And.Contain("survivalPanels.forget"));
        Assert.That(screen, Does.Contain("G.PanelsUnder.Contains(\"formation\")"), "编队页在下层时提上来，不另建草稿");
        var stack = Web("Services", "GameSession.PanelStack.cs");
        Assert.That(stack, Does.Contain("if (Panel == panel) { OpenPanel(null); return; }"), "再按当前页快捷键＝关闭该页");
        var session = Web("Services", "GameSession.cs");
        var sw = Slice(session, "private void SwitchPanel(", "public void Guide(");
        Assert.That(sw, Does.Contain("restore = _chain.Cover(from, panel);"));
        Assert.That(sw, Does.Contain("next = PopRestorable();"), "关闭只退当前最上层，恢复紧挨着的下层");
        Assert.That(sw, Does.Contain("if (from == FireMenuPanel && !InChainAfter(FireMenuPanel)) LeaveFireMenu(next == null);"), "加工菜单盖在下层时不丢草稿");
        Assert.That(sw, Does.Contain("if (!KeepState(\"craft\"))"), "制作页盖在下层时保留配方／份数／批次与选择设施父链");
        Assert.That(sw, Does.Contain("if (!InChainAfter(\"growth\")) ClosePracticeDraft();"));
        Assert.That(sw, Does.Contain("if (!InChainAfter(\"formation\"))"));
        var guide = sw.IndexOf("case \"craft\": Guide(\"recipe\", CraftRecipe); break;", StringComparison.Ordinal);
        Assert.That(sw.LastIndexOf("if (!restore)", guide, StringComparison.Ordinal), Is.GreaterThan(0), "恢复下层不再给一次操作前引导");
        var js = Web("wwwroot", "js", "app.js");
        Assert.That(js, Does.Contain("window.survivalPanels = (function () {"));
        Assert.That(js, Does.Contain("if (!ev.repeat) dotnet.invokeMethodAsync('OnKey', up);"), "按住顶栏键不连续开关（原有）");
    }

    /// <summary>逐字稿 Q590：原只有「关闭」的顶栏阅读页按有无下层显示「返回／关闭」，键帽仍只在承接 ESC 时显示。</summary>
    [Test]
    public void Q590_CloseButtonsUseChainLabel()
    {
        foreach (var f in new[] { "MapOverlay", "RestOverlay", "LogOverlay", "GoalsOverlay", "TipsOverlay", "DialogueLogOverlay", "GrowthOverlay", "JourneyOverlay", "CityServiceOverlay", "DryingOverlay", "FireMenu" })
            Assert.That(Web("Components", f + ".razor"), Does.Contain("@G.PanelCloseLabel<EscCap On=\"@(G.EscOwner == EscOwner.Panel)\" />"), f);
        Assert.That(Web("Components", "HelpOverlay.razor"), Does.Contain("@G.PanelCloseLabel<EscCap On=\"@G.EscOnHelpClose\" />"));
        Assert.That(Web("Components", "GearOverlay.razor"), Does.Contain("G.PanelHasUnder ? PanelReturnChain.ReturnLabel : GearPanelCopy.Close"));
        Assert.That(Web("Components", "CraftOverlay.razor"), Does.Contain("G.CraftFromChooser && !G.PanelHasUnder ? \"返回\" : G.PanelCloseLabel"));
        Assert.That(Web("Components", "FormationOverlay.razor"), Does.Contain(">取消<EscCap"), "编队原是「取消」，不改");
    }

    /// <summary>Q590-A03：换场、战斗、终局、读档／新局／回标题与原规则直接收盘时整条链作废；原需回中央进度的真实开工清链而不是退一层。</summary>
    [Test]
    public void Q590_A03_ChainClearedOnWorldChangeAndRealStart()
    {
        var stack = Web("Services", "GameSession.PanelStack.cs");
        var rec = stack.Substring(stack.IndexOf("private void ReconcilePanelChain()", StringComparison.Ordinal));
        Assert.That(rec, Does.Contain("Panel == null").And.Contain("S.Progress.Dead").And.Contain("InBattleUi").And.Contain("at.Location != S.CurrentLocation").And.Contain("at.Ground != S.Ground"));
        var session = Web("Services", "GameSession.cs");
        var notify = Slice(session, "public void Notify()", "Changed?.Invoke();");
        Assert.That(notify.IndexOf("ReconcilePanelChain();", StringComparison.Ordinal), Is.LessThan(notify.IndexOf("ReconcileFireMenu();", StringComparison.Ordinal)));
        Assert.That(Slice(session, "private void ResetUi()", "FireMenu = null;"), Does.Contain("ClearPanelChain();"));
        Assert.That(Slice(session, "public async Task CityGo(", "await RunLoop();"), Does.Contain("CloseAllPanels();"));
        Assert.That(Web("Components", "ActionPops.razor"), Does.Contain("G.CloseAllPanels();"), "从地图真实出发");
        Assert.That(Web("Components", "RestOverlay.razor"), Does.Contain("G.Panel == \"rest\") G.CloseAllPanels();"));
        Assert.That(Web("Services", "GameSession.EnemyCardsV2.cs"), Does.Contain("if (Panel == \"growth\") CloseAllPanels();"));
        var fac = Web("Services", "GameSession.FacMenu.cs");
        Assert.That(Slice(fac, "private void CloseFireMenuForAction()", "public bool FireMaintBlocked"), Does.Contain("if (_chain.Remove(FireMenuPanel)) FireMenu = null;"), "菜单在下层时开工也收起");
        Assert.That(Slice(fac, "private void ReconcileFireMenu()", "var passive"), Does.Contain("if (!_chain.Contains(FireMenuPanel)) FireMenu = null;"));
    }

    // ───────────── Q586 查看来源与返回重验 ─────────────

    /// <summary>Q586-A01：「查看来源」把地图盖在制作页上（制作页的选择与选择设施父链留在下层），关地图按返回链恢复；不再另存旧快照。</summary>
    [Test]
    public void Q586_A01_SourcesCoverCraftAndReturn()
    {
        var session = Web("Services", "GameSession.cs");
        var open = Slice(session, "public void OpenSources(string itemId)", "public void FocusSource(");
        Assert.That(open, Does.Contain("SwitchPanel(\"map\", null, true);"));
        Assert.That(session, Does.Not.Contain("_craftReturn"), "不恢复库存快照，恢复时重读");
        var fac = Web("Services", "GameSession.FacMenu.cs");
        Assert.That(Slice(fac, "public void PanelEscape()", "OpenPop(StationsPop"), Does.Contain("&& _chain.Count == 0"), "制作页在最底层时返回仍先回选择层");
    }

    /// <summary>Q586-A02：恢复时设施已无法操作→不恢复执行页、显示定稿句并回当前合法画面；来源／内容变化→清掉并显示「内容已变化，请重新选择。」，数量文本不动。</summary>
    [Test]
    public void Q586_A02_RestoreRevalidates()
    {
        var stack = Web("Services", "GameSession.PanelStack.cs");
        var block = Slice(stack, "private string? PanelRestoreBlock(string p)", "private void RestorePanel(");
        Assert.That(block, Does.Contain("!E.HasFireAt(S, m.Ground)").And.Contain("return GameEngine.FacReturnGoneText;"));
        Assert.That(block, Does.Contain("E.ProcessingStationChoices(S).All(c => c.StationId != st)"));
        var restore = Slice(stack, "private void RestorePanel(string p)", "private void DropPanelState(");
        Assert.That(restore, Does.Contain("m.Minute = int.MinValue;"), "下层期间被别处用掉的来源按变化处理");
        Assert.That(restore, Does.Contain("Notice = GameEngine.ContentChangedText;"));
        Assert.That(restore, Does.Not.Contain("CraftBatch ="), "数量原样保留");
        Assert.That(restore, Does.Not.Contain("Qty ="));
    }

    // ───────────── Q554 成长页练习盘 ESC ─────────────

    /// <summary>Q554-A01／A02：练习盘开着时 ESC 由盘内「返回」承接（键帽「返回 ESC」），只收盘丢草稿、焦点回入口；下一次 ESC 才关成长页。</summary>
    [Test]
    public void Q554_PracticeDiskTakesEscFirst()
    {
        Assert.That(GameEngine.EnemyDiskBack, Is.EqualTo("返回"));
        Assert.That(Verbatim(), Does.Contain("| 练习盘返回业务名＋当前层键帽 | 返回 ESC |"));
        var keys = Web("Services", "GameSession.Keys.cs");
        var owner = Slice(keys, "public EscOwner NormalEscOwner()", "public bool EscOnDisk");
        var practice = owner.IndexOf("if (Panel == \"growth\" && PracticeOpen) return EscOwner.PracticeBack;", StringComparison.Ordinal);
        Assert.That(practice, Is.GreaterThan(0));
        Assert.That(practice, Is.LessThan(owner.IndexOf("if (Panel != null) return EscOwner.Panel;", StringComparison.Ordinal)), "盘先于成长页");
        Assert.That(Web("Components", "GameScreen.razor"), Does.Contain("case EscOwner.PracticeBack: G.ClosePractice(); break;"), "与盘内返回同一入口");
        var growth = Web("Components", "GrowthOverlay.razor");
        Assert.That(growth, Does.Contain("@GameEngine.EnemyDiskBack<EscCap On=\"@(G.EscOwner == EscOwner.PracticeBack)\" />"));
        Assert.That(growth, Does.Contain("id=\"@GameSession.PracticeEntryId\" data-practice"));
        var close = Slice(Web("Services", "GameSession.EnemyCardsV2.cs"), "public void ClosePractice()", "private void ClosePracticeDraft()");
        Assert.That(close, Does.Contain("FocusIdPending = PracticeEntryId;"));
        Assert.That(close, Does.Not.Contain("OpenPanel("), "不关成长页");
    }

    /// <summary>Q590：物品操作盘被覆盖打开的顶栏页盖住时同游戏菜单一样只隐藏不卸载（盘内数量保留），整条链关完原样恢复。</summary>
    [Test]
    public void Q590_CardPopHeldUnderPanel()
    {
        var pop = Web("Components", "CardPop.razor");
        Assert.That(pop, Does.Contain("(!G.CardPopCovered || G.CardPopHeldUnderPanel)"));
        Assert.That(pop, Does.Contain("G.MenuOpen || G.BattleMenuOpen || G.CardPopHeldUnderPanel ? \" menu-hidden\" : \"\""));
        var session = Web("Services", "GameSession.cs");
        var sw = Slice(session, "private void SwitchPanel(", "public void Guide(");
        Assert.That(sw, Does.Contain("if (from == null && cover && CardPopShown) _cardPopBase = Selected.Key;"));
        Assert.That(sw, Does.Contain("if (_cardPopBase == Selected.Key) CardPopCovered = false;"));
    }

    /// <summary>
    /// BM4a.6 试玩修复 B1、B2（§Q590「仅最上层可见且接收焦点」「焦点进入新面板首个合法控件」，Q590-A02「再按当前页快捷键关闭」）：
    /// 新打开的一级面板（覆盖打开或从无面板画面打开）都把焦点交进新面板，不留在被盖住的下层卡上（其焦点提示不浮在面板上）；恢复层不走这里。
    /// 落点不取下拉／文字／数量框（B43 规定它们吞掉顶栏字母：覆盖打开地图后焦点落在层级下拉框，M／L／C 都失效）；
    /// 面板本身是确认区时交给面板（Enter 仍归唯一确认），只有输入控件时回标题。下拉框、数量框里字母不当快捷键的原规则不改。
    /// </summary>
    [Test]
    public void BM4a6_NewPanelTakesFocusButNotIntoTypingControls()
    {
        var session = Web("Services", "GameSession.cs");
        var sw = Slice(session, "private void SwitchPanel(", "public void Guide(");
        Assert.That(sw, Does.Contain("if (next != null && next != from && !restore && IsChainPanel(next)) PanelFocusPending = true;"),
            "从无面板画面打开同样交焦点（B2），恢复层仍由 survivalPanels.restore 取回原焦点");
        Assert.That(sw, Does.Not.Contain("if (covered && !restore) PanelFocusPending = true;"));

        var js = Web("wwwroot", "js", "app.js");
        var fp = js.Substring(js.IndexOf("focusPanel: function () {"));
        fp = fp.Substring(0, fp.IndexOf("\n    },"));
        Assert.That(fp, Does.Contain("if (tg === 'SELECT' || tg === 'TEXTAREA' || c.isContentEditable) return true;"), "下拉、文字区不作落点（B1）");
        Assert.That(fp, Does.Contain("if (c.disabled || typing(c) ||"), "输入控件跳过");
        Assert.That(fp, Does.Contain("sp.matches('[data-enter-scope][tabindex]')"), "面板本身是确认区时交给面板，Enter 仍归唯一确认");
        Assert.That(fp, Does.Contain("hosts[k].querySelector('#fm-title, h2, h3')"), "只有输入控件时回标题");
        Assert.That(fp, Does.Contain("hosts[h].contains(ae)) return;"), "焦点已在新面板里就不动");

        // 原 B43 输入控件规则不改：下拉、文字框、数量框里普通字母仍不当顶栏快捷键
        var keys = js.Substring(js.IndexOf("hookKeys: function (dotnet) {"));
        Assert.That(keys, Does.Contain("if (tg === 'TEXTAREA' || tg === 'SELECT' || el.isContentEditable) return true;"));
        Assert.That(keys, Does.Contain("if (k !== 'Escape' || mod) return;"));
    }

    private static string Verbatim() => File.ReadAllText(Path.Combine(Root(), "doc", "策划案", "重墨像素风界面与卡片 v0.9", "重墨 V2 开工裁定与界面逐字稿.md"));

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
