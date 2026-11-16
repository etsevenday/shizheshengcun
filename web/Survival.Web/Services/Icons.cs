using Microsoft.AspNetCore.Components;

namespace Survival.Web.Services;

/// <summary>
/// 界面图标：按稳定 ID 取 ink-v2 清单图（B37a1，<see cref="ArtCatalog"/>）。原 v08 图集、分类占位图（ph_*）、借图别名与
/// 已有正式图的内联矢量图已删除；只保留 Q883 待答图位与 Q838 登记缺图对象正在用的现有图。
/// </summary>
public static class Icons
{
    /// <summary>
    /// 保留的现有矢量图（键同 <see cref="ArtCatalog.LegacyKept"/>）：Q883 待答的昼夜 outdoor、统一占位 placeholder（帮助四题、战斗兜底、公开救助残页）、
    /// 空加工位 slot_empty、修理 repair；腐败物 spoiled_matter 沿用原“材料”分类占位图（Q838 登记缺项）。负重 backpack 是 v08 图集格，见 ArtAssets.LegacyBackpack。
    /// </summary>
    private static readonly Dictionary<string, string> Legacy = new()
    {
        ["outdoor"] = @"<path d=""M0 100L22 46 40 70 58 36 82 72 100 52V100Z"" fill=""#355038""/><path d=""M10 100L16 70 22 100M70 100L76 64 82 100"" fill=""#24382a""/><circle cx=""78"" cy=""22"" r=""9"" fill=""#d9c48e""/>",
        ["placeholder"] = @"<rect x=""12"" y=""12"" width=""76"" height=""76"" rx=""8"" fill=""#c9bf9f"" stroke=""#8a8466"" stroke-width=""4""/><path d=""M24 72L42 46 54 60 62 52 78 72Z"" fill=""#9a9478""/><circle cx=""66"" cy=""32"" r=""7"" fill=""#9a9478""/><path d=""M12 12L88 88"" stroke=""#8a8466"" stroke-width=""3"" opacity="".5""/>",
        ["slot_empty"] = @"<rect x=""14"" y=""14"" width=""72"" height=""72"" rx=""8"" fill=""none"" stroke=""#8a8466"" stroke-width=""4"" stroke-dasharray=""10 8""/><path d=""M50 34V66M34 50H66"" stroke=""#8a8466"" stroke-width=""5"" stroke-linecap=""round""/>",
        ["repair"] = @"<g stroke=""#4e5752"" stroke-width=""3""><path d=""M20 76L58 38"" stroke-width=""9"" stroke-linecap=""round""/><path d=""M58 38Q52 24 62 16 72 10 82 16L72 26 76 32 86 24Q90 36 80 44 70 50 58 38Z"" fill=""#8e978f""/></g><path d=""M24 30L40 46M24 46L40 30"" stroke=""#c8b27a"" stroke-width=""5"" stroke-linecap=""round""/>",
        ["spoiled_matter"] = @"<rect x=""16"" y=""34"" width=""68"" height=""50"" rx=""4"" fill=""#a6ad9c"" stroke=""#4e5752"" stroke-width=""3""/><path d=""M16 50H84M38 34V84M62 34V84"" stroke=""#7b8580"" stroke-width=""3""/><path d=""M28 34L40 18H60L72 34"" fill=""none"" stroke=""#4e5752"" stroke-width=""3""/>",
    };

    /// <summary>界面请求过但清单和保留图都没有的键（缺图诊断）；显示统一占位。</summary>
    public static readonly HashSet<string> Missing = new();

    public static string Svg(string key)
    {
        var (kind, k) = ArtAssets.Catalog.Plan(key);
        if (kind == ArtKind.Sprite) return ArtAssets.Sprite(k)!;
        if (kind == ArtKind.Legacy)
            return k == "backpack" ? ArtAssets.LegacyBackpack() : $"<svg viewBox=\"0 0 100 100\" aria-hidden=\"true\">{Legacy[k]}</svg>";
        Missing.Add(key);
        return ArtAssets.Sprite(k) ?? $"<svg viewBox=\"0 0 100 100\" aria-hidden=\"true\">{Legacy["placeholder"]}</svg>";
    }

    public static MarkupString Markup(string key) => new(Svg(key));
}
