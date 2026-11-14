using System.Text.RegularExpressions;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v1.0 B31a：阅读字体与动画速度偏好（逐字稿 §1 Q295、§1.1 Q321，验收 Q295-A01、Q321-A01），
/// 字号四档（Q327-A01）、细像素字体随发与切换（Q275），新框体素材（R50）。
/// </summary>
public class B31aPresentationTests
{
    /// <summary>Q321-A01：三档时间段 60／120／180 毫秒；敌方逐条展示 240／480／720；其他基准时长同乘（Q297 特写标准 1200）。</summary>
    [TestCase("fast", 60, 240, 600)]
    [TestCase("standard", 120, 480, 1200)]
    [TestCase("slow", 180, 720, 1800)]
    public void Q321_A01_SpeedTiers(string stored, int segment, int enemy, int closeUp)
    {
        var p = UiPresentationPrefs.Parse(null, stored);
        Assert.That(p.SegmentDelayMs, Is.EqualTo(segment), "世界推进每 5 游戏分钟段");
        Assert.That(p.EnemyStepDelayMs, Is.EqualTo(enemy), "当前敌方逐条展示");
        Assert.That(UiPresentationPrefs.Scale(1200, p.Speed), Is.EqualTo(closeUp));
        Assert.That(UiPresentationPrefs.Scale(150, p.Speed), Is.EqualTo((int)System.Math.Round(150 * UiPresentationPrefs.Multiplier(p.Speed), System.MidpointRounding.AwayFromZero)));
    }

    [Test]
    public void Q321_Multipliers()
    {
        Assert.That(UiPresentationPrefs.Multiplier(AnimationSpeed.Fast), Is.EqualTo(0.5));
        Assert.That(UiPresentationPrefs.Multiplier(AnimationSpeed.Standard), Is.EqualTo(1.0));
        Assert.That(UiPresentationPrefs.Multiplier(AnimationSpeed.Slow), Is.EqualTo(1.5));
        Assert.That(UiPresentationPrefs.SegmentBaseMs, Is.EqualTo(120));
        Assert.That(UiPresentationPrefs.EnemyStepBaseMs, Is.EqualTo(480));
    }

    /// <summary>Q295-A01：无浏览器偏好时默认细像素、标准；无效值同样回默认；保存值读回不变（重载、换档、新游戏沿用的前提）。</summary>
    [Test]
    public void Q295_A01_DefaultsAndRoundTrip()
    {
        var d = UiPresentationPrefs.Parse(null, null);
        Assert.That(d.Font, Is.EqualTo(ReadingFont.Pixel));
        Assert.That(d.Speed, Is.EqualTo(AnimationSpeed.Standard));
        foreach (var bad in new[] { "", " ", "Clear", "FAST", "40", "250", "normal", "{}" })
        {
            var p = UiPresentationPrefs.Parse(bad, bad);
            Assert.That(p.Font, Is.EqualTo(ReadingFont.Pixel), bad);
            Assert.That(p.Speed, Is.EqualTo(AnimationSpeed.Standard), bad);
        }
        foreach (var f in new[] { ReadingFont.Pixel, ReadingFont.Clear })
        foreach (var s in new[] { AnimationSpeed.Fast, AnimationSpeed.Standard, AnimationSpeed.Slow })
        {
            var back = UiPresentationPrefs.Parse(UiPresentationPrefs.FontValue(f), UiPresentationPrefs.SpeedValue(s));
            Assert.That(back.Font, Is.EqualTo(f));
            Assert.That(back.Speed, Is.EqualTo(s));
        }
    }

    /// <summary>PXb 逐字：「阅读字体」「细像素」「清晰阅读」「动画速度」「快」「标准」「慢」。</summary>
    [Test]
    public void PXb_Labels()
    {
        Assert.That(UiPresentationPrefs.Label(ReadingFont.Pixel), Is.EqualTo("细像素"));
        Assert.That(UiPresentationPrefs.Label(ReadingFont.Clear), Is.EqualTo("清晰阅读"));
        Assert.That(UiPresentationPrefs.Label(AnimationSpeed.Fast), Is.EqualTo("快"));
        Assert.That(UiPresentationPrefs.Label(AnimationSpeed.Standard), Is.EqualTo("标准"));
        Assert.That(UiPresentationPrefs.Label(AnimationSpeed.Slow), Is.EqualTo("慢"));
        var settings = Web("Components", "PresentationSettings.razor");
        Assert.That(settings, Does.Contain("<div class=\"eyebrow\">阅读字体</div>"));
        Assert.That(settings, Does.Contain("<div class=\"eyebrow\">动画速度</div>"));
    }

    /// <summary>Q321：常态与战斗菜单用同一偏好组件；旧 40／120／250 档与 max(160, 4×档位) 去掉；速度只在网页壳呈现层，内核结算不读它。</summary>
    [Test]
    public void Q321_ShellWiring()
    {
        var menu = Web("Components", "MenuOverlay.razor");
        Assert.That(Regex.Matches(menu, "<PresentationSettings />").Count, Is.EqualTo(2), "常态与战斗（含敌踪选择）菜单");
        Assert.That(menu, Does.Not.Contain("(\"慢\", 250)").And.Not.Contain("SetSpeed"));
        var session = Web("Services", "GameSession.cs");
        Assert.That(session, Does.Contain("public int SegmentDelayMs => Presentation.SegmentDelayMs;"));
        Assert.That(session, Does.Contain("private Task StepPause() => Task.Delay(_fxSkipAll ? 1 : EnemyStepDelayMs);"), "B34b：没有特写的一步沿原逐条停顿（本批已跳过余下时只让出线程）");
        Assert.That(session, Does.Not.Contain("SegmentDelayMs * 4"));
        Assert.That(session, Does.Contain("return Math.Max(SegmentDelayMs, (int)Math.Ceiling(ExtractMinVisibleMs / (double)segs));"), "撤出专用最短可见时长沿原边界");
        Assert.That(session, Does.Contain("await Task.Delay(_skip ? 1 : SegmentDelay(S.Sub ?? S.Main));"), "跳过动画与退出响应沿原逻辑");
        foreach (var f in Directory.GetFiles(Path.Combine(Root(), "src", "Core", "Engine"), "*.cs", SearchOption.AllDirectories))
            Assert.That(File.ReadAllText(f), Does.Not.Contain("UiPresentationPrefs"), "三档世界分钟与结果一致：内核不读呈现偏好 " + f);
    }

    /// <summary>Q295/Q275：浏览器偏好键与取值两端一致；清晰阅读把 --font-read 换成系统黑体；页面载入即套用。</summary>
    [Test]
    public void Q295_FontSwitchWiring()
    {
        var css = Web("wwwroot", "css", "app.css");
        Assert.That(css, Does.Contain(":root[data-read-font=\"clear\"] { --font-read: var(--font-clear); }"));
        Assert.That(css, Does.Contain("--font-read: var(--font-pixel);"));
        Assert.That(css, Does.Contain("src: url('../fonts/chill-bitmap-16.woff2') format('woff2');"));
        Assert.That(css, Does.Contain("font-family: var(--font-read);"));
        Assert.That(css, Does.Contain("font-synthesis: none;"));
        var js = Web("wwwroot", "js", "app.js");
        Assert.That(js, Does.Contain("'survival.pref.' + key"));
        Assert.That(js, Does.Contain("localStorage.getItem('survival.pref." + UiPresentationPrefs.FontKey + "')"));
        Assert.That(js, Does.Contain("localStorage.getItem('survival.pref." + UiPresentationPrefs.SpeedKey + "')"));
        Assert.That(js, Does.Contain("speed === 'fast' ? 0.5 : speed === 'slow' ? 1.5 : 1"));
        Assert.That(js, Does.Contain("font === 'clear' ? 'clear' : 'pixel'"));
        Assert.That(js, Does.Contain("r.setAttribute('data-read-font', 'clear')"));
        Assert.That(js, Does.Contain("r.style.setProperty('--motion-k'"));
        Assert.That(Web("wwwroot", "index.html"), Does.Contain("<link rel=\"preload\" href=\"fonts/chill-bitmap-16.woff2\" as=\"font\" type=\"font/woff2\" crossorigin />"));
    }

    /// <summary>Q275／R112：字体与授权一起随发；R50：新框体与纹理在发行目录。</summary>
    [Test]
    public void Assets_Shipped()
    {
        var www = Path.Combine(Root(), "web", "Survival.Web", "wwwroot");
        Assert.That(new FileInfo(Path.Combine(www, "fonts", "chill-bitmap-16.woff2")).Length, Is.EqualTo(1397704));
        Assert.That(File.ReadAllText(Path.Combine(www, "fonts", "chill-bitmap-license.txt")), Does.Contain("SIL Open Font License"));
        foreach (var f in new[] { "frame-action.svg", "frame-card.svg", "frame-panel.svg", "frame-selected.svg", "texture-iron.svg", "texture-paper.svg" })
            Assert.That(File.Exists(Path.Combine(www, "art", "ink2", f)), f);
        var mat = Web("wwwroot", "css", "materials.css");
        Assert.That(mat, Does.Contain("--ink2-frame-l: 8px;").And.Contain("--ink2-frame-m: 6px;").And.Contain("--ink2-frame-s: 4px;"), "frame() 粗细 8/6/4");
        Assert.That(mat, Does.Not.Contain("art/v09/"));
    }

    /// <summary>B31a 试玩修复（构建 5220202）：不可用斜线盖过后加载的纸卡底；纸卡只换默认阴影层，九状态阴影可叠加；标题页按钮也走新按钮令牌。</summary>
    [Test]
    public void PlaytestFix_StatesAndTitleButtons()
    {
        var mat = Web("wwwroot", "css", "materials.css");
        Assert.That(mat, Does.Contain(".wasteland :is(.card, .ocard, .pot-card, .pot-card.in-fire, .fc-food, .map-card):is(.dim, .st-disabled) {"), "0,4,0 盖过 illustration.css 0,3,0");
        Assert.That(mat, Does.Contain(".wasteland :is(.card, .ocard, .pot-card, .pot-card.in-fire, .fc-food, .map-card) {\n  --st-hover").Or.Contain(".wasteland :is(.card, .ocard, .pot-card, .pot-card.in-fire, .fc-food, .map-card) {\r\n  --st-hover"));
        Assert.That(mat, Does.Contain(":is(.wasteland, .title-screen) :is(.loc-btn, .primary, .apop-go, .rest-go button, .ca-use, .fc-btn.ignite, .fc-btn.cook) {"));
        var ill = Web("wwwroot", "css", "illustration.css");
        var start = ill.IndexOf(".wasteland :is(.card, .ocard, .pot-card, .pot-card.in-fire, .fc-food) {");
        var rule = ill.Substring(start, ill.IndexOf('}', start) - start);
        Assert.That(rule, Does.Contain("--st-base:").And.Not.Contain("box-shadow:"));
    }

    /// <summary>Q327-A01：四档字号令牌；样式表里除图标字符外不再有零散像素字号（留言组件不在本批）。</summary>
    [Test]
    public void Q327_A01_FontTiers()
    {
        var css = Web("wwwroot", "css", "app.css");
        foreach (var t in new[] { "--fs-aux: 14px;", "--fs-body: 16px;", "--fs-dialogue: 20px;", "--fs-title: 24px;", "--fs-display: 32px;", "--lh-body: 1.5;", "--lh-dialogue: 1.7;" })
            Assert.That(css, Does.Contain(t));
        var allowed = new[] { ".exp-card .exp-art", ".anc-aim" };
        foreach (var f in Directory.GetFiles(Path.Combine(Root(), "web", "Survival.Web", "wwwroot", "css"), "*.css"))
            foreach (var line in File.ReadAllLines(f))
                if (Regex.IsMatch(line, @"font-size:\s*\d+px") || Regex.IsMatch(line, @"font:\s*\d+px"))
                    Assert.That(allowed.Any(a => line.Contains(a)), Is.True, Path.GetFileName(f) + ": " + line);
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
