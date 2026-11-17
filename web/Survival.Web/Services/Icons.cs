using Microsoft.AspNetCore.Components;

namespace Survival.Web.Services;

/// <summary>
/// 界面图标：按稳定 ID 取 ink-v2 清单图（B37a1，<see cref="ArtCatalog"/>）。原 v08 图集、分类占位图（ph_*）、借图别名与
/// 已有正式图的内联矢量图已删除；只保留 Q883 待答图位与 Q838 登记缺图对象正在用的现有图。
/// </summary>
public static class Icons
{
    /// <summary>
    /// 保留的现有矢量图（键同 <see cref="ArtCatalog.LegacyKept"/>）：探索帮助 outdoor、统一占位 placeholder（公开救助残页、缺图兜底）；
    /// 腐败物 spoiled_matter 用 B50a Q766 专用腐烂物图标（assets/20261115/永久丢弃与腐败物/腐败物图标.svg 原样内联；Q885 A：B53 暂接，仍列“暂用，待开发替换”）。
    /// BM11（Q883）：backpack／slot_empty／repair 已改接同义 glyph，旧图删除。
    /// </summary>
    private static readonly Dictionary<string, string> Legacy = new()
    {
        ["outdoor"] = @"<path d=""M0 100L22 46 40 70 58 36 82 72 100 52V100Z"" fill=""#355038""/><path d=""M10 100L16 70 22 100M70 100L76 64 82 100"" fill=""#24382a""/><circle cx=""78"" cy=""22"" r=""9"" fill=""#d9c48e""/>",
        ["placeholder"] = @"<rect x=""12"" y=""12"" width=""76"" height=""76"" rx=""8"" fill=""#c9bf9f"" stroke=""#8a8466"" stroke-width=""4""/><path d=""M24 72L42 46 54 60 62 52 78 72Z"" fill=""#9a9478""/><circle cx=""66"" cy=""32"" r=""7"" fill=""#9a9478""/><path d=""M12 12L88 88"" stroke=""#8a8466"" stroke-width=""3"" opacity="".5""/>",
        ["spoiled_matter"] = @"<ellipse cx=""51"" cy=""84.5"" rx=""42"" ry=""6.5"" fill=""#493e30"" opacity=""0.22""/><path d=""M 12 77 C 8 66 17 55 28 52 C 29 40 40 31 52 36 C 63 27 78 37 77 51 C 90 55 96 70 87 79 C 72 91 28 91 12 77 Z"" fill=""#89704e"" stroke=""#493c2b"" stroke-width=""3"" stroke-linejoin=""round"" stroke-linecap=""round""/><path d=""M 22 63 C 18 54 26 46 37 47 C 43 39 58 43 57 55 C 68 60 62 73 50 71 C 39 78 23 75 22 63 Z"" fill=""#a08a5d"" stroke=""#584630"" stroke-width=""2"" stroke-linejoin=""round"" stroke-linecap=""round""/><path d=""M 50 68 C 46 59 54 52 64 54 C 70 46 82 53 79 63 C 88 69 80 80 69 77 C 59 82 49 78 50 68 Z"" fill=""#677b43"" stroke=""#435038"" stroke-width=""2"" stroke-linejoin=""round"" stroke-linecap=""round""/><ellipse cx=""36"" cy=""55.5"" rx=""4"" ry=""3.5"" fill=""#73874d""/><ellipse cx=""44"" cy=""64"" rx=""3"" ry=""3"" fill=""#5b663e""/><ellipse cx=""63.5"" cy=""65"" rx=""3.5"" ry=""3"" fill=""#a0ab65""/><ellipse cx=""73.5"" cy=""69.5"" rx=""2.5"" ry=""2.5"" fill=""#a0ab65""/><path d=""M 22 78 C 33 82 42 80 48 78 M 65 39 C 67 34 65 30 68 25"" fill=""none"" stroke=""#4d402d"" stroke-width=""2"" stroke-linejoin=""round"" stroke-linecap=""round""/>",
    };

    /// <summary>界面请求过但清单和保留图都没有的键（缺图诊断）；显示统一占位。</summary>
    public static readonly HashSet<string> Missing = new();

    public static string Svg(string key)
    {
        var (kind, k) = ArtAssets.Catalog.Plan(key);
        if (kind == ArtKind.Sprite) return ArtAssets.Sprite(k)!;
        if (kind == ArtKind.Legacy)
            return $"<svg viewBox=\"0 0 100 100\" aria-hidden=\"true\">{Legacy[k]}</svg>";
        Missing.Add(key);
        return ArtAssets.Sprite(k) ?? $"<svg viewBox=\"0 0 100 100\" aria-hidden=\"true\">{Legacy["placeholder"]}</svg>";
    }

    public static MarkupString Markup(string key) => new(Svg(key));
}
