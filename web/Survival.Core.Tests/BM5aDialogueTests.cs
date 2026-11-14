namespace Survival.Core.Tests;

/// <summary>
/// BM5a（S14「2026-11-13 试玩后续修订：对白恢复背景并覆盖下两行」DLG-LOWER-A01—A06；S14「BM4b Q606—Q614」§Q611、§Q614；
/// 《重墨 V2 开工裁定与界面逐字稿》「2026-11-13 试玩后续：普通对白恢复底板并覆盖③④」）：
/// 普通对白移到③当前地面＋④随身物品、恢复不透明底板；全屏面板开着同一几何；战斗对白不动；「回到最新」离开已显示最新即出现；帮助「界面分区」段换全文。
/// 网页壳布局只能按源码核对，实际几何由无头浏览器三档自测（见开发记录 BM5a）。
/// </summary>
public class BM5aDialogueTests : TestBase
{
    private static string Web(params string[] parts) => File.ReadAllText(Path.Combine(new[] { Root(), "web", "Survival.Web" }.Concat(parts).ToArray())).Replace("\r\n", "\n");

    private static string Root()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d != null && !File.Exists(Path.Combine(d.FullName, "web", "Survival.sln"))) d = d.Parent;
        return d!.FullName;
    }

    private static string Rule(string css, string head)
    {
        var i = css.IndexOf(head, StringComparison.Ordinal);
        Assert.That(i, Is.GreaterThanOrEqualTo(0), head);
        var r = css.Substring(i);
        return r.Substring(0, r.IndexOf('}'));
    }

    /// <summary>DLG-LOWER-A01／A03、Q611-A01：普通对白层是四行网格的直接子项，锚定③④，不在①②合并区里；面板开着同一几何且在面板之上。</summary>
    [Test]
    public void DLG_LOWER_A01_A03_LayerAnchoredToRows3And4()
    {
        var g = Web("Components", "GameScreen.razor");
        var rows2 = g.IndexOf("<div class=\"rows2\" data-row=\"1-2\">", StringComparison.Ordinal);
        var carried = g.IndexOf("<ItemShelf Owner=\"@ItemInstance.Carried\" Title=\"随身物品\" />", StringComparison.Ordinal);
        var layer = g.IndexOf("@if (!G.BattleFullscreen) { <DialogueLayer /> }", StringComparison.Ordinal);
        Assert.That(rows2, Is.GreaterThan(0));
        Assert.That(layer, Is.GreaterThan(carried), "对白层放在四行之后、作为 .center-scroll.four 的直接子项");
        Assert.That(g.Split("<DialogueLayer />").Length - 1, Is.EqualTo(1), "主界面只有一处普通对白层");
        var close = g.IndexOf("</div>", carried, StringComparison.Ordinal);
        Assert.That(layer, Is.LessThan(close), "仍在四行网格容器内");

        var css = Web("wwwroot", "css", "fourrows.css");
        Assert.That(css, Does.Contain(".center-scroll.four > * { grid-column: 1; }"), "单列网格，对白层不把地面／随身挤到隐式列");
        var r = Rule(css, ".center-scroll.four > .dlg-layer {");
        Assert.That(r, Does.Contain("grid-row: 3 / span 2;").And.Contain("position: relative;").And.Contain("z-index: 40;"));
        // 全屏／高面板的①②合并区 z-index 31，对白层 40 在其上
        Assert.That(css, Does.Contain(".center-scroll.four.full-panel > .rows2 { grid-row: 1 / span 4; z-index: 31; }"));
        Assert.That(css, Does.Contain(".center-scroll.four.tall-panel > .rows2 { grid-row: 1 / span 3; z-index: 31; }"));
    }

    /// <summary>DLG-LOWER-A02：不透明底板（底色 alpha=1，铁纹叠在底色之上、铁框区也由底色垫满）；头像左下／右下，正文独立纵滚，控制条常在。</summary>
    [Test]
    public void DLG_LOWER_A02_OpaqueBoardAndReadingArea()
    {
        var css = Web("wwwroot", "css", "fourrows.css");
        var r = Rule(css, ".center-scroll.four > .dlg-layer {");
        Assert.That(r, Does.Contain("background: var(--iron-texture), var(--ink2-dark); background-clip: border-box;"));
        Assert.That(r, Does.Not.Contain("transparent"));
        Assert.That(Web("wwwroot", "css", "app.css"), Does.Contain("--ink2-dark: #161311;"), "底色为不透明色值");
        Assert.That(css, Does.Contain(".center-scroll.four > .dlg-layer > .dlg-side { align-self: end; }"));
        Assert.That(css, Does.Contain(".center-scroll.four > .dlg-layer > .dlg-main { align-self: stretch; max-height: 100%; }"));
        Assert.That(css, Does.Contain(".center-scroll.four > .dlg-layer .dlg-stream { flex: 1 1 auto; }"));
        // 矮屏只收留白与头像占位，不改对白字号
        var low = css.Substring(css.IndexOf("/* 矮屏先收留白与头像占位", StringComparison.Ordinal));
        Assert.That(low.Substring(0, low.IndexOf("\n}", StringComparison.Ordinal)), Does.Not.Contain("font-size"));
        var layer = Web("Components", "DialogueLayer.razor");
        Assert.That(layer.IndexOf("dlg-side left", StringComparison.Ordinal), Is.LessThan(layer.IndexOf("dlg-main", StringComparison.Ordinal)));
        Assert.That(layer.IndexOf("dlg-main", StringComparison.Ordinal), Is.LessThan(layer.IndexOf("dlg-side right", StringComparison.Ordinal)), "主角左、对象右不交换");
        Assert.That(layer, Does.Contain("<span class=\"dlg-latest-host\"><button type=\"button\" class=\"secondary dlg-latest\" @onclick=\"Latest\">回到最新 ↓</button></span>"), "回到最新在同一对白控制区");
    }

    /// <summary>DLG-LOWER-A04：上方①②、一级面板、就近浮窗与两条物品栏在对白锁期间都 inert（不能点、拖、取焦点）；ESC 菜单开着时对白层 inert（Q322）。</summary>
    [Test]
    public void DLG_LOWER_A04_BehindIsInert()
    {
        var g = Web("Components", "GameScreen.razor");
        Assert.That(g, Does.Contain("<div class=\"rows2-inner\" inert=\"@dlgLock\">"));
        Assert.That(g, Does.Contain("<div class=\"panel-host\" data-panel=\"@G.Panel\" inert=\"@dlgLock\">"));
        Assert.That(g, Does.Contain("<div class=\"apop-host\" inert=\"@dlgLock\">"));
        Assert.That(Web("Components", "ItemShelf.razor"), Does.Contain("inert=\"@(G.DialogueLocked || (!carried && G.CityBrowsing))\""));
        Assert.That(Web("Components", "DialogueLayer.razor"), Does.Contain("inert=\"@(G.MenuOpen || G.BattleMenuOpen)\""));
    }

    /// <summary>DLG-LOWER-A06：全屏战斗对白仍在战场专用区域、原整块铁框底板（本批 CSS 只作用于 .center-scroll.four &gt; .dlg-layer）。</summary>
    [Test]
    public void DLG_LOWER_A06_BattleDialogueUnchanged()
    {
        Assert.That(Web("Components", "BattleScreen.razor"), Does.Contain("<div class=\"bfs-dlg\" inert=\"@fxLock\"><DialogueLayer /></div>"));
        Assert.That(Web("wwwroot", "css", "battle.css"), Does.Contain(".bfs-dlg > .dlg-layer { padding: 10px 12px; border: 1px solid var(--ink2-meter-gold);"));
        Assert.That(Web("wwwroot", "css", "materials.css"), Does.Contain(".pause-layer, .bfs-dlg > .dlg-layer)"));
        var css = Web("wwwroot", "css", "fourrows.css");
        var block = css.Substring(css.IndexOf("/* ───── BM5a", StringComparison.Ordinal));
        var selectors = block.Split('\n').Where(l => l.TrimStart().StartsWith(".", StringComparison.Ordinal)).ToList();
        Assert.That(selectors, Is.Not.Empty);
        Assert.That(selectors.Any(l => l.Contains(".bfs-dlg")), Is.False, "本批规则不选中战斗对白");
    }

    /// <summary>DLG-LOWER-A06：帮助「界面分区」原段换成 S14／逐字稿给的全文，不再说人物对话只盖上两行。</summary>
    [Test, Category("DesignSync")]
    public void DLG_LOWER_A06_HelpParagraph()
    {
        const string text = "人物对话显示在中部下方，覆盖「当前地面」和「随身物品」两行；上方场景和设施区域保持可见。对白读完前不能进行场景或物品操作，ESC仍可打开游戏菜单。顶栏面板按各自范围显示；进入战斗后改为全屏战斗画面，战斗结束原样回到主界面。";
        var h = Web("Services", "HelpCatalog.cs");
        Assert.That(h, Does.Contain($"\"{text}\""));
        Assert.That(h, Does.Not.Contain("人物对话和顶栏面板只盖住上面两行"));
        var s14 = File.ReadAllText(Directory.GetFiles(Path.Combine(Root(), "doc", "策划案"), "S14 主界面与全局UI交互系统.md", SearchOption.AllDirectories).Single());
        Assert.That(s14, Does.Contain($"“{text}”"));
    }

    /// <summary>
    /// Q614-A01（选 A）：手动滚动只要不在“已显示最新”就亮「回到最新」，没有新句也亮；到底或回到最新即隐藏；点击只滚动（anchor）、不推进对白。
    /// Q627 未答：B39 长句句首锚定保持——锚点处及其下方都算“在最新”。
    /// </summary>
    [Test]
    public void Q614_A01_LatestShownWheneverNotAtLatest()
    {
        var js = Web("wwwroot", "js", "app.js");
        var dlg = js.Substring(js.IndexOf("window.survivalDlg = (function () {", StringComparison.Ordinal));
        dlg = dlg.Substring(0, dlg.IndexOf("})();", StringComparison.Ordinal));
        Assert.That(dlg, Does.Contain("function atLatest(el) { return bottom(el) || el.scrollTop >= anchorTop(el) - 8; }"));
        Assert.That(dlg, Does.Contain("var on = atLatest(el);\n          el.__dlg.follow = on;\n          showLatest(el, !on);"), "手动滚动即判定，不等新句");
        Assert.That(dlg, Does.Contain("function bottom(el) { return el.scrollTop + el.clientHeight >= el.scrollHeight - 8; }"), "沿原底部容差");
        Assert.That(dlg, Does.Contain("else showLatest(el, true);"), "回看期间新句不抢位置");
        Assert.That(dlg, Does.Not.Contain("invokeMethodAsync"), "只滚动，不回调推进对白");
        var layer = Web("Components", "DialogueLayer.razor");
        Assert.That(layer, Does.Contain("await JS.InvokeVoidAsync(\"survivalDlg.latest\", _stream);"));
    }
}
