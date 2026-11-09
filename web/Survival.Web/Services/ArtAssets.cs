namespace Survival.Web.Services;

/// <summary>Presentation-only assets. Scene identity and sprite identity never change game data.</summary>
public static class ArtAssets
{
    private static readonly HashSet<string> Scenes = new(StringComparer.Ordinal)
    {
        "camp", "field", "woods", "stream", "village", "hardware", "scrapyard", "clinic"
    };

    public static string Scene(string id, string? fallback = null) =>
        id == "hardware" ? "art/v10/hardware.png" :
        Scenes.Contains(id) ? $"art/v08/{id}.png" : fallback ?? $"scenes/{id}.svg";

    public static readonly IReadOnlyDictionary<string, (string Sheet, int Cell)> Sprites = BuildSprites();

    private static IReadOnlyDictionary<string, (string Sheet, int Cell)> BuildSprites()
    {
        var result = new Dictionary<string, (string Sheet, int Cell)>(StringComparer.Ordinal);
        void Sheet(string name, params string[] keys)
        {
            for (var i = 0; i < keys.Length; i++) result.Add(keys[i], (name, i));
        }
        Sheet("supplies", "wood", "stone", "berry", "fiber", "clean_water", "dirty_water",
            "canned_food", "hatchet", "pot", "lighter", "bandage", "stone_knife", "cloth",
            "raw_root", "cooked_root", "boiled_root");
        Sheet("tools", "herb", "scrap", "parts", "shelter", "bed", "fire_lit", "fire_out",
            "fists", "repair", "kitchen_knife", "crowbar", "wooden_spear", "metal_spear",
            "wooden_bow", "crossbow", "pistol");
        Sheet("equipment", "shotgun", "rifle", "arrow", "bolt", "ammo_9mm", "shell_12g",
            "ammo_rifle", "raw_meat", "hide", "cooked_meat", "boiled_meat",
            "active_substance_white_unidentified", "antibiotics", "antiviral_cure",
            "antiviral_precursor", "lab_reagent");
        Sheet("creatures", "enemy_shambler", "enemy_crawler", "enemy_runner", "enemy_worker",
            "enemy_riot", "enemy_howler", "enemy_feral_dog", "enemy_wolf", "enemy_boar",
            "enemy_stag", "carcass_feral_dog", "carcass_wolf", "carcass_boar", "carcass_stag",
            "backpack", "crate");
        Sheet("resources", "resource_roots", "resource_water", "resource_herbs",
            "resource_metal_heap", "resource_parts_box", "resource_med_cabinet",
            "resource_cloth_bag", "resource_pot_box", "resource_tool_box", "resource_supply_box",
            "resource_misc_box", "resource_hardware_weapon_cache", "resource_village_weapon_locker",
            "resource_clinic_medical_supply", "resource_village_cold_box", "resource_wood");
        result["fire"] = result["fire_lit"];
        return result;
    }

    // B2 主界面截图复核（S14 §B、UI-M05，开发计划 v0.9 M86）：每个图位里主体的实际范围（单位=图位的1%，[左,上,宽,高]），
    // 由浏览器逐像素读透明度量出（阈值 alpha>24，外扩2个单位留抗锯齿与投影）；边缘 12 个单位内与主体之间隔着全空行/列的
    // 稀疏像素是相邻图位漏进来的碎片，范围从断档处开始。显示时只取这个范围，再按等比完整适配（等价 contain）：
    // 不用 clip-path 再裁外圈，主体不被切掉，相邻碎片也不出现；透明留白不再算进图片区。
    private static readonly IReadOnlyDictionary<string, (int L, int T, int W, int H)[]> Frames = new Dictionary<string, (int L, int T, int W, int H)[]>(StringComparer.Ordinal)
    {
        ["supplies"] = new[] { (6, 21, 94, 79), (10, 24, 85, 72), (4, 17, 90, 83), (0, 12, 96, 88), (30, 3, 46, 97), (26, 5, 48, 95), (11, 16, 77, 75), (3, 6, 93, 94), (3, 10, 97, 79), (21, 4, 60, 90), (3, 11, 94, 83), (3, 0, 89, 97), (3, 4, 97, 82), (6, 0, 94, 94), (5, 8, 90, 72), (1, 6, 95, 77) },
        ["tools"] = new[] { (4, 9, 95, 91), (3, 20, 97, 77), (2, 21, 95, 75), (0, 3, 100, 96), (2, 8, 98, 88), (1, 1, 99, 97), (0, 29, 100, 67), (9, 9, 79, 87), (3, 4, 96, 86), (2, 5, 96, 85), (4, 4, 91, 89), (0, 0, 96, 95), (3, 0, 95, 94), (6, 0, 88, 95), (0, 1, 97, 87), (3, 1, 91, 92) },
        ["equipment"] = new[] { (5, 16, 95, 79), (2, 5, 98, 95), (1, 5, 97, 94), (1, 19, 96, 77), (5, 25, 91, 62), (15, 13, 79, 79), (10, 1, 79, 96), (0, 13, 97, 79), (1, 9, 99, 84), (7, 18, 87, 70), (0, 12, 100, 79), (13, 5, 74, 90), (22, 1, 56, 92), (28, 3, 43, 87), (37, 0, 26, 95), (15, 0, 68, 94) },
        ["creatures"] = new[] { (33, 3, 51, 97), (4, 42, 94, 58), (11, 3, 75, 97), (15, 0, 75, 100), (15, 2, 75, 98), (16, 3, 77, 97), (5, 10, 94, 89), (9, 7, 85, 93), (1, 13, 98, 86), (5, 0, 86, 100), (0, 53, 99, 39), (0, 50, 99, 44), (1, 25, 99, 59), (0, 26, 100, 58), (12, 0, 79, 88), (2, 9, 92, 76) },
        ["resources"] = new[] { (2, 4, 97, 93), (2, 10, 97, 85), (1, 7, 97, 89), (1, 7, 96, 89), (3, 6, 94, 91), (5, 2, 92, 95), (0, 7, 99, 90), (1, 1, 96, 97), (2, 3, 97, 89), (1, 3, 98, 92), (0, 2, 98, 93), (0, 1, 100, 92), (5, 0, 95, 95), (7, 0, 92, 94), (8, 0, 86, 94), (0, 1, 98, 90) },
    };

    /// <summary>图位内主体范围（见 <see cref="Frames"/>）；没有登记时为整格。</summary>
    public static (int L, int T, int W, int H) FrameOf(string sheet, int cell) =>
        Frames.TryGetValue(sheet, out var fs) && cell >= 0 && cell < fs.Length ? fs[cell] : (0, 0, 100, 100);

    public static string? Sprite(string key)
    {
        if (!Sprites.TryGetValue(key, out var sprite)) return null;
        // v1.0: independently composed resource illustrations. The nested
        // viewport clips exactly one cell; the outer viewBox removes only its
        // transparent guard band, keeping the complete silhouette undistorted.
        var focus = key switch
        {
            "resource_pot_box" => (Cell: 0, Top: 10, Height: 89),
            "resource_tool_box" => (Cell: 1, Top: 10, Height: 81),
            "resource_hardware_weapon_cache" => (Cell: 2, Top: 8, Height: 88),
            _ => (Cell: -1, Top: 0, Height: 100)
        };
        if (focus.Cell >= 0)
            return $"<svg class=\"ink-sprite focus-art\" data-art=\"{key}\" viewBox=\"0 0 100 {focus.Height}\" aria-hidden=\"true\" focusable=\"false\"><svg width=\"100\" height=\"{focus.Height}\" viewBox=\"0 {focus.Top} 100 {focus.Height}\" style=\"overflow:hidden;width:100%;height:100%;max-width:none;max-height:none\"><image href=\"art/v10/resource-focus.png\" x=\"{-focus.Cell * 100}\" y=\"0\" width=\"300\" height=\"100\" /></svg></svg>";
        // A nested viewport clips one cell even when the outer SVG is letterboxed;
        // B2: the inner viewBox keeps only the measured subject frame of that cell.
        var x = sprite.Cell % 4 * 100;
        var y = sprite.Cell / 4 * 100;
        var f = FrameOf(sprite.Sheet, sprite.Cell);
        return $"<svg class=\"ink-sprite\" data-art=\"{key}\" viewBox=\"0 0 {f.W} {f.H}\" aria-hidden=\"true\" focusable=\"false\"><svg width=\"{f.W}\" height=\"{f.H}\" viewBox=\"{f.L} {f.T} {f.W} {f.H}\" style=\"overflow:hidden;width:{f.W}px;height:{f.H}px;max-width:none;max-height:none\"><image href=\"art/v08/{sprite.Sheet}.png\" x=\"{-x}\" y=\"{-y}\" width=\"400\" height=\"400\" /></svg></svg>";
    }
}
