namespace Survival.Web.Services;

/// <summary>
/// Presentation-only assets. Scene identity and sprite identity never change game data.
/// B37a1（Q806）：场景与物件图按稳定 ID 从 ink-v2 清单取（<see cref="ArtCatalog"/>），清单嵌在程序集里。
/// </summary>
public static class ArtAssets
{
    public static readonly ArtCatalog Catalog = Load();

    /// <summary>地点场景图（B37a1，Q837 A）：清单 99 处用新图、16 号稿批准的 7 处沿用旧地景、其余 37 处统一占位并登记。</summary>
    public static string Scene(string id) => Catalog.Scene(id);

    /// <summary>
    /// 清单裁窗：内层 viewport 只露出裁窗，外层按裁窗宽高适配图位。不预缩、不另存单图，浏览器按需取整张图集。
    /// 缩放观感由全局 image-rendering: pixelated 统一为最近邻（Q840 B）。
    /// </summary>
    internal static string? Sprite(string key, string? cls = null, string? label = null)
    {
        if (!Catalog.Sprites.TryGetValue(key, out var f)) return null;
        var (x, y, w, h) = (f.Crop[0], f.Crop[1], f.Crop[2], f.Crop[3]);
        var a11y = label == null ? "aria-hidden=\"true\"" : $"role=\"img\" aria-label=\"{System.Net.WebUtility.HtmlEncode(label)}\"";
        return $"<svg class=\"ink-sprite{(cls == null ? "" : " " + cls)}\" data-art=\"{key}\" viewBox=\"0 0 {w} {h}\" {a11y} focusable=\"false\"><svg width=\"{w}\" height=\"{h}\" viewBox=\"{x} {y} {w} {h}\" style=\"overflow:hidden;width:{w}px;height:{h}px;max-width:none;max-height:none\"><image href=\"{ArtCatalog.Root}{f.File}\" width=\"{f.Size[0]}\" height=\"{f.Size[1]}\" preserveAspectRatio=\"none\" /></svg></svg>";
    }

    /// <summary>
    /// Q883 待答：顶栏负重图标沿用 v08 creatures 图集第 14 格（原 B2 实测主体范围 12,0,79,88），等答复后再换；B37a1 只保留这一张旧图集。
    /// </summary>
    internal static string LegacyBackpack() =>
        "<svg class=\"ink-sprite\" data-art=\"backpack\" viewBox=\"0 0 79 88\" aria-hidden=\"true\" focusable=\"false\"><svg width=\"79\" height=\"88\" viewBox=\"12 0 79 88\" style=\"overflow:hidden;width:79px;height:88px;max-width:none;max-height:none\"><image href=\"art/v08/creatures.png\" x=\"-200\" y=\"-300\" width=\"400\" height=\"400\" /></svg></svg>";

    /// <summary>
    /// B37a2：人物肖像图地址（对白、对话记录、整备队员列表、战斗队列与操作台、人物详情缩略图）。身份由 <see cref="ActorArt"/> 决定；
    /// 清单没有该肖像的落到 glyph:unknown 肖像；随机招募同伴保持现有临时形象（Q884 待答）。
    /// </summary>
    public static string Portrait(string key) =>
        key == ActorArt.RandomRecruitLegacy ? ActorArt.RandomRecruitLegacyFile
        : ArtCatalog.Root + (Catalog.Portraits.TryGetValue(key, out var f) ? f : Catalog.Portraits[ActorArt.Unknown]);

    /// <summary>
    /// B37a2：人物全身图（整备立绘、战斗站位、战斗特写、地点人物卡），清单裁窗原样显示；随机招募同伴保持现有临时形象 &lt;img&gt;（Q884 待答）。
    /// label 非空时整张图带读屏名（沿原 &lt;img alt&gt;），否则是装饰图。
    /// </summary>
    public static Microsoft.AspNetCore.Components.MarkupString Figure(string key, string? cls = null, string? label = null) => new(
        key == ActorArt.RandomRecruitLegacy
            ? $"<img{(cls == null ? "" : $" class=\"{cls}\"")} src=\"{ActorArt.RandomRecruitLegacyFile}\" alt=\"{System.Net.WebUtility.HtmlEncode(label ?? "")}\" draggable=\"false\" />"
            : Sprite(key, cls, label) ?? Sprite(ActorArt.Unknown, cls, label)!);

    /// <summary>队伍角色的肖像地址（<see cref="ActorArt.ForActor"/>）。</summary>
    public static string ActorPortrait(Survival.Core.State.GameState s, string actorId) => Portrait(ActorArt.ForActor(Catalog, s, actorId));

    /// <summary>队伍角色的全身图（<see cref="ActorArt.ForActor"/>）。</summary>
    public static Microsoft.AspNetCore.Components.MarkupString ActorFigure(Survival.Core.State.GameState s, string actorId, string? cls = null) =>
        Figure(ActorArt.ForActor(Catalog, s, actorId), cls);

    /// <summary>一句对白发言者的肖像地址（Q811 A，<see cref="ActorArt.ForLine"/>）；非人物行按查不到处理。</summary>
    public static string LinePortrait(Survival.Core.Config.GameConfig cfg, Survival.Core.State.DialogueLine line) =>
        Portrait(ActorArt.ForLine(Catalog, cfg, line) ?? ActorArt.Unknown);

    private static ArtCatalog Load()
    {
        using var stream = typeof(ArtAssets).Assembly.GetManifestResourceStream("ink-v2.json")!;
        using var reader = new StreamReader(stream);
        return ArtCatalog.Parse(reader.ReadToEnd());
    }
}
