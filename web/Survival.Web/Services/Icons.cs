using Microsoft.AspNetCore.Components;

namespace Survival.Web.Services;

public static partial class Icons
{
    /// <summary>补画的同风格占位图标：熟块根、草药、废金属、零件，以及地点与状态示意。</summary>
    private static readonly Dictionary<string, string> Extra = new()
    {
        ["cooked_root"] = @"<path d=""M41 33Q71 18 78 38 85 56 61 71L26 84Q12 73 25 55Z"" fill=""#8a5a35"" stroke=""#5b4129"" stroke-width=""3""/><path d=""M31 62L42 68M49 47L59 54M60 36L68 42"" stroke=""#c9935c"" stroke-width=""3""/><path d=""M40 22Q36 12 42 6M56 20Q52 10 58 4"" fill=""none"" stroke=""#b9b39a"" stroke-width=""3"" stroke-linecap=""round""/>",
        ["herb"] = @"<g stroke=""#4f6a3e"" stroke-width=""3""><path d=""M50 88V30"" fill=""none""/><path d=""M50 52Q26 50 20 30 42 30 50 52M50 40Q74 38 80 18 58 18 50 40M50 66Q30 68 22 54 40 50 50 66M50 70Q70 72 80 58 60 54 50 70"" fill=""#7f9d5e""/></g><circle cx=""50"" cy=""26"" r=""6"" fill=""#c9b6d8"" stroke=""#7b6a8a"" stroke-width=""2""/>",
        ["scrap"] = @"<path d=""M14 70L30 38 58 30 70 50 88 56 76 82 36 84Z"" fill=""#7b8580"" stroke=""#4e5752"" stroke-width=""3""/><path d=""M30 38L44 60 70 50M44 60L36 84M58 30L62 46"" fill=""none"" stroke=""#a9b1a8"" stroke-width=""3""/><circle cx=""66"" cy=""68"" r=""5"" fill=""#a86e4f""/>",
        ["parts"] = @"<g stroke=""#4e5752"" stroke-width=""3""><circle cx=""38"" cy=""42"" r=""18"" fill=""#8e978f""/><circle cx=""38"" cy=""42"" r=""6"" fill=""#d1c5a8""/><path d=""M56 58L84 86M64 54L88 78"" stroke-width=""7"" stroke-linecap=""round""/><rect x=""20"" y=""66"" width=""22"" height=""14"" rx=""3"" fill=""#a6ad9c""/></g>",
        ["shelter"] = @"<path d=""M10 82L50 22 90 82Z"" fill=""#8b7a52"" stroke=""#5c4f36"" stroke-width=""3""/><path d=""M38 82L50 56 62 82Z"" fill=""#2d3328""/><path d=""M24 62L76 62M32 50L68 50"" stroke=""#b29d6c"" stroke-width=""3""/>",
        ["bed"] = @"<rect x=""12"" y=""52"" width=""76"" height=""20"" rx=""5"" fill=""#b9ad85"" stroke=""#6f6648"" stroke-width=""3""/><rect x=""16"" y=""42"" width=""22"" height=""14"" rx=""4"" fill=""#d8cfaf"" stroke=""#6f6648"" stroke-width=""3""/><path d=""M16 72V82M84 72V82"" stroke=""#6f6648"" stroke-width=""5""/>",
        ["route"] = @"<path d=""M14 80Q34 44 56 58T88 22"" fill=""none"" stroke=""#c8b27a"" stroke-width=""7"" stroke-dasharray=""10 8"" stroke-linecap=""round""/><circle cx=""14"" cy=""80"" r=""8"" fill=""#7f9d5e""/><path d=""M80 10L94 30H66Z"" fill=""#bd8c50""/>",
        ["outdoor"] = @"<path d=""M0 100L22 46 40 70 58 36 82 72 100 52V100Z"" fill=""#355038""/><path d=""M10 100L16 70 22 100M70 100L76 64 82 100"" fill=""#24382a""/><circle cx=""78"" cy=""22"" r=""9"" fill=""#d9c48e""/>",
        ["indoor"] = @"<path d=""M14 48L50 18 86 48V92H14Z"" fill=""#5d5a4a"" stroke=""#3c3a30"" stroke-width=""3""/><rect x=""40"" y=""60"" width=""20"" height=""32"" fill=""#2c2a22""/><rect x=""22"" y=""56"" width=""12"" height=""12"" fill=""#c9b77f""/><rect x=""66"" y=""56"" width=""12"" height=""12"" fill=""#c9b77f""/>",
        ["camp"] = @"<path d=""M8 86L36 34 64 86Z"" fill=""#6d6346"" stroke=""#4a432f"" stroke-width=""3""/><path d=""M66 62Q60 76 66 84 74 90 82 84 88 74 78 60 78 72 72 70Z"" fill=""#d8a25a""/><path d=""M56 90L92 86"" stroke=""#786449"" stroke-width=""6"" stroke-linecap=""round""/>",
    };

    public static string Svg(string key)
    {
        if (!Extracted.TryGetValue(key, out var inner) && !Extra.TryGetValue(key, out inner))
            inner = Extracted["stone"];
        return $"<svg viewBox=\"0 0 100 100\" aria-hidden=\"true\">{inner}</svg>";
    }

    public static MarkupString Markup(string key) => new(Svg(key));

    public static string ForResource(string nodeId, IReadOnlyDictionary<string, int> outputs) => nodeId switch
    {
        "roots" => "raw_root",
        "water" => "dirty_water",
        "herbs" => "herb",
        "metal_heap" => "scrap",
        "parts_box" => "parts",
        "med_cabinet" => "bandage",
        "cloth_bag" => "cloth",
        "pot_box" => "pot",
        "tool_box" => "hatchet",
        "supply_box" => "canned_food",
        "misc_box" => "cloth",
        _ => outputs.Keys.FirstOrDefault() ?? "stone",
    };
}
