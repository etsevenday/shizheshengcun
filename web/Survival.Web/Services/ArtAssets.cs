namespace Survival.Web.Services;

/// <summary>
/// Presentation-only assets. Scene identity and sprite identity never change game data.
/// B37a1（Q806）：场景与物件图按稳定 ID 从 ink-v2 清单取（<see cref="ArtCatalog"/>），清单嵌在程序集里。
/// </summary>
public static class ArtAssets
{
    public static readonly ArtCatalog Catalog = Load();

    private static readonly HashSet<string> Scenes = new(StringComparer.Ordinal)
    {
        "camp", "field", "woods", "stream", "village", "hardware", "scrapyard", "clinic"
    };

    public static string Scene(string id, string? fallback = null) =>
        id == "hardware" ? "art/v10/hardware.png" :
        Scenes.Contains(id) ? $"art/v08/{id}.png" : fallback ?? $"scenes/{id}.svg";

    /// <summary>
    /// 清单裁窗：内层 viewport 只露出裁窗，外层按裁窗宽高适配图位。不预缩、不另存单图，浏览器按需取整张图集。
    /// 缩放观感由全局 image-rendering: pixelated 统一为最近邻（Q840 B）。
    /// </summary>
    internal static string? Sprite(string key)
    {
        if (!Catalog.Sprites.TryGetValue(key, out var f)) return null;
        var (x, y, w, h) = (f.Crop[0], f.Crop[1], f.Crop[2], f.Crop[3]);
        return $"<svg class=\"ink-sprite\" data-art=\"{key}\" viewBox=\"0 0 {w} {h}\" aria-hidden=\"true\" focusable=\"false\"><svg width=\"{w}\" height=\"{h}\" viewBox=\"{x} {y} {w} {h}\" style=\"overflow:hidden;width:{w}px;height:{h}px;max-width:none;max-height:none\"><image href=\"{ArtCatalog.Root}{f.File}\" width=\"{f.Size[0]}\" height=\"{f.Size[1]}\" preserveAspectRatio=\"none\" /></svg></svg>";
    }

    /// <summary>
    /// Q883 待答：顶栏负重图标沿用 v08 creatures 图集第 14 格（原 B2 实测主体范围 12,0,79,88），等答复后再换；B37a1 只保留这一张旧图集。
    /// </summary>
    internal static string LegacyBackpack() =>
        "<svg class=\"ink-sprite\" data-art=\"backpack\" viewBox=\"0 0 79 88\" aria-hidden=\"true\" focusable=\"false\"><svg width=\"79\" height=\"88\" viewBox=\"12 0 79 88\" style=\"overflow:hidden;width:79px;height:88px;max-width:none;max-height:none\"><image href=\"art/v08/creatures.png\" x=\"-200\" y=\"-300\" width=\"400\" height=\"400\" /></svg></svg>";

    private static ArtCatalog Load()
    {
        using var stream = typeof(ArtAssets).Assembly.GetManifestResourceStream("ink-v2.json")!;
        using var reader = new StreamReader(stream);
        return ArtCatalog.Parse(reader.ReadToEnd());
    }
}
