using Microsoft.AspNetCore.Components;

namespace Survival.Web.Services;

public static class Icons
{
    public static MarkupString Markup(string key) => new(ArtAssets.Sprite(Resolve(key)));

    private static string Resolve(string key)
    {
        if (key.Contains(':')) return key;
        if (ArtAssets.HasSprite("item:" + key)) return "item:" + key;
        if (key.StartsWith("enemy_", StringComparison.Ordinal)) return "enemy:" + key[6..];
        if (key.StartsWith("status_", StringComparison.Ordinal)) return "status:" + key;
        if (key.StartsWith("cmd_", StringComparison.Ordinal)) return "action:" + key[4..];
        if (ArtAssets.HasSprite("facility:" + key)) return "facility:" + key;
        return key switch
        {
            "fists" => "skillFamily:unarmed",
            "backpack" or "crate" => "glyph:storage",
            "outdoor" => "glyph:camp",
            "indoor" => "glyph:service",
            "fire" => "facility:fire_lit",
            "slot_empty" => "glyph:add",
            "repair" => "skillFamily:crafting",
            _ => "glyph:" + key,
        };
    }
}
