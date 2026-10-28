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
        // A nested viewport clips one cell even when the outer SVG is letterboxed.
        var x = sprite.Cell % 4 * 100;
        var y = sprite.Cell / 4 * 100;
        return $"<svg class=\"ink-sprite\" data-art=\"{key}\" viewBox=\"0 0 100 100\" aria-hidden=\"true\" focusable=\"false\"><svg width=\"100\" height=\"100\" style=\"overflow:hidden;width:100px;height:100px;max-width:none;max-height:none\"><image href=\"art/v08/{sprite.Sheet}.png\" x=\"{-x}\" y=\"{-y}\" width=\"400\" height=\"400\" /></svg></svg>";
    }
}
