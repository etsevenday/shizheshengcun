using System.Security.Cryptography;
using Survival.Core.State;
using Survival.Web.Services;

namespace Survival.Core.Tests;

/// <summary>
/// 开发记录 BM11「次要问题合并」：美术与人物图位。
/// Q884 B、Q885 A、Q886 A（S14／A11／N02 附录文首 2026-11-17 §Q883—Q886）；Q918 A、Q920 A（S14／美术16 文首 2026-11-17 §Q918—Q920，
/// 验收 ARTF-Q918-01／02、ARTF-Q920-01／02，同源合同 assets/20261117/art-feedback-q918-q920.json）。Q883 见 B37aArtTests.Q883_SlotsUseInterimGlyphs。
/// </summary>
public class BM11ArtTests
{
    private static string Root()
    {
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "web", "Survival.sln"))) dir = dir.Parent;
        return dir!.FullName;
    }

    private static string Web(params string[] parts) => Path.Combine(new[] { Root(), "web", "Survival.Web" }.Concat(parts).ToArray());
    private static string WebText(params string[] parts) => File.ReadAllText(Web(parts)).Replace("\r\n", "\n");
    private static readonly Lazy<ArtCatalog> _cat = new(() => ArtCatalog.Parse(WebText("Art", "ink-v2.json")));
    private static ArtCatalog Cat => _cat.Value;
    private static string Sha(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();

    // ───────────── Q884／Q885／Q886 ─────────────

    /// <summary>Q884 B：随机有职业招募同伴统一 glyph:unknown；命名同伴、保底护送员、主角不变（配对）。</summary>
    [Test]
    public void Q884_RandomRecruit_Unknown_OthersUnchanged()
    {
        var s = new GameState();
        s.Roster.Add(new RosterEntry { ActorId = "rc_1_1", Kind = "random", Name = "某人", TemplateId = "hunter" });
        s.Roster.Add(new RosterEntry { ActorId = "escort_1", Kind = "escort", Name = "护送员" });
        s.Roster.Add(new RosterEntry { ActorId = "npc_lin_zhao", Kind = "named", Name = "林昭" });
        Assert.That(s.Roster, Has.Count.EqualTo(3));
        Assert.That(ActorArt.ForActor(Cat, s, "rc_1_1"), Is.EqualTo("glyph:unknown"));
        Assert.That(ActorArt.ForActor(Cat, s, "escort_1"), Is.EqualTo("recruitTemplate:legacy_neutral"), "Q835 补问 B 固定通用图不变");
        Assert.That(ActorArt.ForActor(Cat, s, "npc_lin_zhao"), Is.EqualTo("actor:npc_lin_zhao"));
        Assert.That(ActorArt.ForActor(Cat, s, GameState.PlayerId), Is.EqualTo("actor:p0"));
        Assert.That(Directory.Exists(Web("wwwroot", "img", "portraits")) && Directory.GetFiles(Web("wwwroot", "img", "portraits")).Length > 0, Is.False, "原临时 svg 已删");
    }

    /// <summary>Q885 A：腐败物 SVG 暂接（Legacy），素材清单记“暂用，待开发替换”。</summary>
    [Test]
    public void Q885_SpoiledMatter_InterimSvg_Listed()
    {
        Assert.That(Cat.Plan("spoiled_matter"), Is.EqualTo((ArtKind.Legacy, "spoiled_matter")));
        var inv = File.ReadAllText(Path.Combine(Root(), "doc", "验收", "重墨V2 UI重做 v1.0", "素材清单.json"));
        Assert.That(inv, Does.Contain("\"object\": \"spoiled_matter\""));
        Assert.That(inv, Does.Contain("暂用，待开发替换"));
    }

    /// <summary>Q886 A：正式肖像读屏只写名；问号占位写“{名}（临时形象）”；固定通用补员图不算临时。</summary>
    [Test]
    public void Q886_AltText()
    {
        Assert.That(ActorArt.AltText("actor:p0", "主角"), Is.EqualTo("主角"));
        Assert.That(ActorArt.AltText("glyph:unknown", "某人"), Is.EqualTo("某人（临时形象）"));   // N02 附录 Q886 A：“{显示名}（临时形象）”
        Assert.That(ActorArt.AltText("recruitTemplate:legacy_neutral", "护送员"), Is.EqualTo("护送员"));
        var dlg = WebText("Components", "DialogueLayer.razor");
        var log = WebText("Components", "DialogueLogOverlay.razor");
        Assert.That(dlg + log, Does.Not.Contain("（临时形象）\""));
        Assert.That(dlg, Does.Contain("alt=\"@ActorArt.AltText(ActorArt.ForLine(ArtAssets.Catalog, G.E.Config, rightLine) ?? ActorArt.Unknown, other)\""));
        Assert.That(log, Does.Contain("alt=\"@ActorArt.AltText(ActorArt.ForLine(ArtAssets.Catalog, G.E.Config, pickRight) ?? ActorArt.Unknown, pickRight.Speaker)\""));
    }

    // ───────────── Q918 ─────────────

    /// <summary>ARTF-Q918-01：标题页绑营区 scene-01 原图、死亡结算绑 scene-d01_07 原图（运行文件 SHA 等于交付值），不走当前 d01_07→d01_01 借图；死亡记录浮层不加图。</summary>
    [Test]
    public void Q918_TitleAndDeathBackgrounds()
    {
        Assert.That(Sha(Web("wwwroot", "art", "ink-v2", "scene-01-幸存者营区.webp")), Is.EqualTo("52ee5cb81e82353e1bf8edd3048abd64e9494993b83289d3078074e043e7cab7"));   // art-feedback-q918-q920.json
        Assert.That(Sha(Web("wwwroot", "art", "q918", "scene-d01_07.webp")), Is.EqualTo("c4f842771ff15fbaffa7cd7a49502eaeaca175da3370d29d964c9ac3781efa51"));
        var ws = WebText("wwwroot", "css", "wasteland.css");
        Assert.That(ws, Does.Contain(".title-screen {").And.Contain("url('../art/ink-v2/scene-01-幸存者营区.webp') center / cover;"));
        Assert.That(ws, Does.Not.Contain("village.png"));
        var app = WebText("wwwroot", "css", "app.css");
        Assert.That(app, Does.Contain(".end-back.end-death { background: linear-gradient(#050806d0, #050806d0), url('../art/q918/scene-d01_07.webp') center / cover; }"));
        Assert.That(app, Does.Not.Contain("scene-d01_01"));
        var end = WebText("Components", "EndScreens.razor");
        Assert.That(end.Split("class=\"end-back end-death\"").Length - 1, Is.EqualTo(1), "只死亡结算本层");
        Assert.That(end, Does.Contain("<div class=\"end-back\" style=\"z-index:70\""), "死亡记录浮层沿原样式");
        Assert.That(ArtCatalog.ApprovedOldScenes["village"], Is.EqualTo("art/v08/village.png"), "村庄地点地景不变");
    }

    // ───────────── Q920 ─────────────

    /// <summary>ARTF-Q920-01：主角小头像用 portrait-actor-p0（160×240）头脸裁窗；四处小图位与右栏缩略图都走 ActorFace；大立绘不变。</summary>
    [Test]
    public void Q920_ProtagonistFaceCrop_SmallSlots()
    {
        Assert.That(Cat.Portraits["actor:p0"], Is.EqualTo("portrait-actor-p0.webp"));
        Assert.That(ActorArt.ProtagonistPortraitSize, Is.EqualTo((160, 240)));
        Assert.That(ActorArt.ProtagonistFaceCrop, Is.EqualTo("54 0 60 60"));   // 开发假设：按原图头脸范围（约 x59—105、y2—56）取的方窗
        var aa = WebText("Services", "ArtAssets.cs");
        Assert.That(aa, Does.Contain("if (key != ActorArt.Protagonist)"));
        Assert.That(aa, Does.Contain("viewBox=\\\"{ActorArt.ProtagonistFaceCrop}\\\" preserveAspectRatio=\\\"xMidYMid slice\\\""));
        var bs = WebText("Components", "BattleScreen.razor");
        Assert.That(bs, Does.Contain("@if (qi <= 0) {@ArtAssets.ActorFace(s, qAlly!)}"), "战斗顺序 26px");
        Assert.That(bs, Does.Contain("<span class=\"bc-face\" aria-hidden=\"true\">@ArtAssets.ActorFace(s, whoId)</span>"), "操作台 28px");
        Assert.That(bs, Does.Contain("@ArtAssets.ActorFigure(s, id, \"anc-fig\")"), "站位大图不变");
        Assert.That(WebText("Components", "GearOverlay.razor"), Does.Contain("@ArtAssets.ActorFace(s, aid, \"g3-face\")"), "整备 34px");
        Assert.That(WebText("Components", "DialogueLogOverlay.razor"), Does.Contain("@ArtAssets.Face(ActorArt.Protagonist, null, ActorArt.AltText(ActorArt.Protagonist, \"主角\"))"), "记录表头 44×55");
        Assert.That(WebText("Components", "DialogueLayer.razor"), Does.Contain("<img class=\"dlg-portrait\" src=\"@ArtAssets.Portrait(ActorArt.Protagonist)\""), "对白大肖像不变");
        var css = WebText("wwwroot", "css", "battle.css");
        Assert.That(css, Does.Contain(".bc-face img, .bc-face svg {"));
        Assert.That(css, Does.Contain(".q-art img, .q-art svg {"));
        Assert.That(WebText("wwwroot", "css", "app.css"), Does.Contain(".dlog-read-head img, .dlog-read-head svg {"));
    }

    /// <summary>ARTF-Q920-02：地点人物卡细插画区各分辨率统一暂藏（只藏人物卡，不藏物品／敌人卡图）。</summary>
    [Test]
    public void Q920_NpcCardArtHidden()
    {
        var app = WebText("wwwroot", "css", "app.css");
        Assert.That(app, Does.Contain("\n.npc-card .oc-art { display: none; }\n"));
        Assert.That(app.Split("display: none; }").Count(x => x.EndsWith(".npc-card .oc-art { ")), Is.EqualTo(1));
        Assert.That(app, Does.Not.Contain(".ocard .oc-art { display: none"), "不全局隐藏卡图");
    }
}
