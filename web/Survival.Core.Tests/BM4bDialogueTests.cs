using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// BM4b（S14「2026-11-13 B45 Q581—Q590」§Q585；《重墨 V2 开工裁定与界面逐字稿》同日节 Q585 行）：
/// 普通对白去大框、下方贴头像布局；对话记录页原样。
/// </summary>
public class BM4bDialogueTests : TestBase
{
    private static string Web(params string[] parts) => File.ReadAllText(Path.Combine(new[] { Root(), "web", "Survival.Web" }.Concat(parts).ToArray()));

    private static string Root()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d != null && !File.Exists(Path.Combine(d.FullName, "web", "Survival.sln"))) d = d.Parent;
        return d!.FullName;
    }

    // ───────────── Q585 下方对白 ─────────────

    /// <summary>Q585-A01：对白层去掉整块底板与装饰边框（含重墨铁框），头像框在左下／右下，阅读区按正文高度从下往上长；本层仍承接点击。</summary>
    [Test]
    public void Q585_A01_BottomDialogueLayout()
    {
        var css = Web("wwwroot", "css", "app.css");
        Assert.That(css, Does.Contain(".dlg-layer { position: absolute; inset: 0; z-index: 40; display: grid;"));
        var layerRule = css.Substring(css.IndexOf(".dlg-layer { position: absolute;", StringComparison.Ordinal));
        layerRule = layerRule.Substring(0, layerRule.IndexOf('}'));
        Assert.That(layerRule, Does.Contain("background: transparent; border: 0;").And.Contain("align-items: end;").And.Contain("pointer-events: auto;"));
        Assert.That(css, Does.Contain(".dlg-layer .dlg-stream { flex: 0 1 auto;"), "短引导不留上方空框");
        Assert.That(css, Does.Contain(".dlg-main { align-self: end; max-height: 100%;"));
        Assert.That(Web("wwwroot", "css", "materials.css"), Does.Not.Contain(".pause-layer, .dlg-layer)"), "不再给对白层套铁框");
        Assert.That(Web("wwwroot", "css", "wasteland.css"), Does.Not.Contain(".dlg-layer { background:"));
        var layer = Web("Components", "DialogueLayer.razor");
        Assert.That(layer.IndexOf("dlg-side left", StringComparison.Ordinal), Is.LessThan(layer.IndexOf("dlg-main", StringComparison.Ordinal)));
        Assert.That(layer.IndexOf("dlg-main", StringComparison.Ordinal), Is.LessThan(layer.IndexOf("dlg-side right", StringComparison.Ordinal)), "主角左、对象右不交换");
        Assert.That(layer.Split("<div class=\"dlg-face\">").Length - 1, Is.EqualTo(2), "两侧头像框");
        Assert.That(layer, Does.Contain("inert=\"@(G.MenuOpen || G.BattleMenuOpen)\""), "ESC 菜单开着时对白不可操作（Q322）");
        Assert.That(layer, Does.Contain("<DialogueLines Lines=\"v.Session.Lines\" CurrentOrder=\"cur.Order\" />"), "已展示上下文保留");
    }

    /// <summary>Q585：中性类型沿 N02 用中性段落、不配头像；独立的对话记录页不改成头像气泡（共用台词流但样式只限对白层）。</summary>
    [Test]
    public void Q585_NeutralKindsAndLogUnchanged()
    {
        var lines = Web("Components", "DialogueLines.razor");
        Assert.That(lines, Does.Contain("<div class=\"dlg-narr k-@kind @(now ? \"now\" : \"\")\" role=\"note\">@l.Text</div>"));
        Assert.That(lines, Does.Not.Contain("dlg-face").And.Not.Contain("dlg-portrait"));
        var css = Web("wwwroot", "css", "app.css");
        Assert.That(css, Does.Contain(".dlg-layer .dlg-narr { background-color:"), "中性段落的底色只在对白层");
        Assert.That(css, Does.Contain(".dlg-stream.static { overflow: visible; flex: 0 0 auto; }"), "对话记录页原样");
        Assert.That(Web("Components", "DialogueLogOverlay.razor"), Does.Not.Contain("dlg-face"));
    }
}
