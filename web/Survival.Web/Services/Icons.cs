using Microsoft.AspNetCore.Components;

namespace Survival.Web.Services;

public static partial class Icons
{
    /// <summary>
    /// 补画的同风格占位图标：熟块根、草药、废金属、零件，地点与状态示意；v0.4 新增水煮块根、火堆燃烧/熄灭两态、
    /// 空加工位框与统一占位图（统一卡片案第 7 节）。同一 itemId 全局只用这一张图。
    /// </summary>
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
        ["boiled_root"] = @"<path d=""M14 52H86Q84 84 50 86 16 84 14 52Z"" fill=""#8c9a97"" stroke=""#4e5752"" stroke-width=""3""/><path d=""M14 52H86"" stroke=""#4e5752"" stroke-width=""4""/><path d=""M26 50Q30 36 46 38 56 40 54 50M50 50Q54 34 68 36 78 40 74 50"" fill=""#c99a62"" stroke=""#7a5532"" stroke-width=""3""/><path d=""M32 62Q50 68 68 62"" fill=""none"" stroke=""#c9d6d8"" stroke-width=""3"" opacity="".8""/><path d=""M36 28Q32 18 38 10M52 26Q48 16 54 8M66 28Q62 18 68 10"" fill=""none"" stroke=""#b9b39a"" stroke-width=""3"" stroke-linecap=""round""/>",
        ["fire_lit"] = @"<path d=""M18 84L82 70M18 70L82 84"" stroke=""#6f5236"" stroke-width=""9"" stroke-linecap=""round""/><path d=""M50 12Q34 34 30 48 26 66 50 72 74 66 70 48 66 36 58 30 58 44 50 42 52 28 50 12Z"" fill=""#e8913f"" stroke=""#a85a22"" stroke-width=""2""/><path d=""M50 40Q42 52 42 60 44 68 50 68 58 68 58 60 58 52 50 40Z"" fill=""#ffd27a""/>",
        ["fire_out"] = @"<path d=""M18 84L82 70M18 70L82 84"" stroke=""#4b3d30"" stroke-width=""9"" stroke-linecap=""round""/><path d=""M30 70Q34 58 46 62 52 54 60 60 70 58 72 70Z"" fill=""#5a524a"" stroke=""#3b3530"" stroke-width=""2""/><circle cx=""44"" cy=""66"" r=""3"" fill=""#a9563a""/><path d=""M46 50Q40 40 48 32 44 24 50 16"" fill=""none"" stroke=""#9c968a"" stroke-width=""3"" stroke-linecap=""round"" opacity="".7""/>",
        ["slot_empty"] = @"<rect x=""14"" y=""14"" width=""72"" height=""72"" rx=""8"" fill=""none"" stroke=""#8a8466"" stroke-width=""4"" stroke-dasharray=""10 8""/><path d=""M50 34V66M34 50H66"" stroke=""#8a8466"" stroke-width=""5"" stroke-linecap=""round""/>",
        ["placeholder"] = @"<rect x=""12"" y=""12"" width=""76"" height=""76"" rx=""8"" fill=""#c9bf9f"" stroke=""#8a8466"" stroke-width=""4""/><path d=""M24 72L42 46 54 60 62 52 78 72Z"" fill=""#9a9478""/><circle cx=""66"" cy=""32"" r=""7"" fill=""#9a9478""/><path d=""M12 12L88 88"" stroke=""#8a8466"" stroke-width=""3"" opacity="".5""/>",
        ["camp"] = @"<path d=""M8 86L36 34 64 86Z"" fill=""#6d6346"" stroke=""#4a432f"" stroke-width=""3""/><path d=""M66 62Q60 76 66 84 74 90 82 84 88 74 78 60 78 72 72 70Z"" fill=""#d8a25a""/><path d=""M56 90L92 86"" stroke=""#786449"" stroke-width=""6"" stroke-linecap=""round""/>",
    };

    /// <summary>
    /// B4 新物品与新装备的分类占位图（开发计划 v0.9 B4 H10）：按武器类型、装备槽、物品类别各一张同画风示意图，同一 ID 全局只用这一张。
    /// 正式美术到位前，这些 ID 记在 Placeholder（缺图清单），不算 Missing。
    /// </summary>
    private static readonly Dictionary<string, string> CategoryArt = new()
    {
        ["ph_blade"] = @"<path d=""M22 80L70 22 78 26 34 86Z"" fill=""#b8bdb4"" stroke=""#4e5752"" stroke-width=""3""/><path d=""M18 74L32 88"" stroke=""#6f5236"" stroke-width=""9"" stroke-linecap=""round""/>",
        ["ph_axe"] = @"<path d=""M30 88L62 20"" stroke=""#6f5236"" stroke-width=""7"" stroke-linecap=""round""/><path d=""M52 18Q78 14 84 36 70 42 58 40Z"" fill=""#9aa39b"" stroke=""#4e5752"" stroke-width=""3""/>",
        ["ph_spear"] = @"<path d=""M20 88L74 22"" stroke=""#6f5236"" stroke-width=""6"" stroke-linecap=""round""/><path d=""M70 14L88 10 84 28 74 30Z"" fill=""#b8bdb4"" stroke=""#4e5752"" stroke-width=""3""/>",
        ["ph_blunt"] = @"<path d=""M28 86L58 34"" stroke=""#6f5236"" stroke-width=""8"" stroke-linecap=""round""/><rect x=""50"" y=""14"" width=""26"" height=""30"" rx=""6"" transform=""rotate(30 63 29)"" fill=""#8e978f"" stroke=""#4e5752"" stroke-width=""3""/>",
        ["ph_archery"] = @"<path d=""M30 12Q78 50 30 88"" fill=""none"" stroke=""#8a5a35"" stroke-width=""6""/><path d=""M30 12V88"" stroke=""#d8cfaf"" stroke-width=""2""/><path d=""M18 50H84M76 44L86 50 76 56"" fill=""none"" stroke=""#6f6648"" stroke-width=""3""/>",
        ["ph_firearms"] = @"<path d=""M12 40H80V52H44L38 76H24L28 52H12Z"" fill=""#5d625c"" stroke=""#3b3f3a"" stroke-width=""3""/><rect x=""80"" y=""42"" width=""10"" height=""6"" fill=""#3b3f3a""/>",
        ["ph_shield"] = @"<path d=""M50 10L84 22V50Q82 76 50 90 18 76 16 50V22Z"" fill=""#8b7a52"" stroke=""#5c4f36"" stroke-width=""4""/><path d=""M50 18V82M24 46H76"" stroke=""#b29d6c"" stroke-width=""4""/>",
        ["ph_command"] = @"<path d=""M28 90V12"" stroke=""#6f6648"" stroke-width=""5"" stroke-linecap=""round""/><path d=""M30 14H80L70 30 80 46H30Z"" fill=""#bd8c50"" stroke=""#7a5532"" stroke-width=""3""/>",
        ["ph_medical"] = @"<rect x=""14"" y=""28"" width=""72"" height=""54"" rx=""8"" fill=""#d8cfaf"" stroke=""#6f6648"" stroke-width=""3""/><path d=""M40 28V18H60V28"" fill=""none"" stroke=""#6f6648"" stroke-width=""3""/><path d=""M50 40V70M35 55H65"" stroke=""#a8443a"" stroke-width=""8""/>",
        ["ph_armor"] = @"<path d=""M30 12L44 20H56L70 12 84 28 76 40V88H24V40L16 28Z"" fill=""#7b8580"" stroke=""#4e5752"" stroke-width=""3""/><path d=""M24 52H76M24 68H76"" stroke=""#a9b1a8"" stroke-width=""3""/>",
        ["ph_combat_acc"] = @"<path d=""M50 10V36"" stroke=""#8a8466"" stroke-width=""3""/><circle cx=""50"" cy=""58"" r=""22"" fill=""#c9a24e"" stroke=""#7a5532"" stroke-width=""4""/><circle cx=""50"" cy=""58"" r=""9"" fill=""#e8d9a4""/>",
        ["ph_top"] = @"<path d=""M34 14L50 22 66 14 88 30 78 44 70 38V88H30V38L22 44 12 30Z"" fill=""#8c9a72"" stroke=""#4f5a3e"" stroke-width=""3""/>",
        ["ph_bottom"] = @"<path d=""M26 12H74L80 88H58L50 40 42 88H20Z"" fill=""#6d6346"" stroke=""#4a432f"" stroke-width=""3""/><path d=""M26 22H74"" stroke=""#b29d6c"" stroke-width=""3""/>",
        ["ph_survival_acc"] = @"<circle cx=""50"" cy=""50"" r=""34"" fill=""#d8cfaf"" stroke=""#6f6648"" stroke-width=""4""/><path d=""M50 22L58 50 50 78 42 50Z"" fill=""#a8443a""/><circle cx=""50"" cy=""50"" r=""4"" fill=""#4e5752""/>",
        ["ph_material"] = @"<rect x=""16"" y=""34"" width=""68"" height=""50"" rx=""4"" fill=""#a6ad9c"" stroke=""#4e5752"" stroke-width=""3""/><path d=""M16 50H84M38 34V84M62 34V84"" stroke=""#7b8580"" stroke-width=""3""/><path d=""M28 34L40 18H60L72 34"" fill=""none"" stroke=""#4e5752"" stroke-width=""3""/>",
        ["ph_food"] = @"<path d=""M12 48H88Q86 84 50 86 14 84 12 48Z"" fill=""#c99a62"" stroke=""#7a5532"" stroke-width=""3""/><path d=""M26 44Q34 28 50 32 66 28 74 44"" fill=""#e0c48a"" stroke=""#7a5532"" stroke-width=""3""/>",
        ["ph_medicine"] = @"<rect x=""30"" y=""24"" width=""40"" height=""62"" rx=""8"" fill=""#c9d6d8"" stroke=""#4e5752"" stroke-width=""3""/><rect x=""36"" y=""12"" width=""28"" height=""14"" rx=""3"" fill=""#8e978f"" stroke=""#4e5752"" stroke-width=""3""/><path d=""M50 44V70M37 57H63"" stroke=""#a8443a"" stroke-width=""6""/>",
    };

    /// <summary>物品 ID → 分类占位图键（RegisterCategories 按配置登记，只登记没有正式图的 ID）。</summary>
    private static readonly Dictionary<string, string> CategoryOf = new();

    /// <summary>正在用分类占位图的物品 ID（缺图清单：正式美术待补）。</summary>
    public static readonly HashSet<string> Placeholder = new();

    /// <summary>
    /// 按配置给没有正式图的物品登记分类占位图：武器按类型、防具饰品衣物按槽、其余按物品类别（材料/食物/医疗）。只在启动时调用一次。
    /// </summary>
    public static void RegisterCategories(Survival.Core.Config.GameConfig cfg)
    {
        foreach (var it in cfg.Items)
        {
            if (Has(it.Id)) continue;
            var w = cfg.Weapons.FirstOrDefault(x => x.Id == it.Id);
            var key = w?.WeaponKind != null ? "ph_" + w.WeaponKind
                : it.EquipSlot != null ? "ph_" + it.EquipSlot
                : it.Kind == "food" ? "ph_food"
                : it.Kind == "medical" ? "ph_medicine"
                : "ph_material";
            if (CategoryArt.ContainsKey(key)) CategoryOf[it.Id] = key;
        }
    }

    /// <summary>界面请求过但没有图的键（缺图清单，见 README）。缺图时显示统一占位图，不再用石块兜底。</summary>
    public static readonly HashSet<string> Missing = new();

    public static bool Has(string key) => ArtAssets.Sprites.ContainsKey(key) || Extracted.ContainsKey(key) || Extra.ContainsKey(key) || Combat.ContainsKey(key) || Status.ContainsKey(key);

    public static string Svg(string key)
    {
        if (ArtAssets.Sprite(key) is { } sprite) return sprite;
        if (!Extracted.TryGetValue(key, out var inner) && !Extra.TryGetValue(key, out inner) && !Combat.TryGetValue(key, out inner) && !Status.TryGetValue(key, out inner)
            && CategoryOf.TryGetValue(key, out var cat) && CategoryArt.TryGetValue(cat, out inner))
            Placeholder.Add(key);   // B4 新物品：分类占位图（缺图清单）
        if (inner == null)
        {
            Missing.Add(key);
            // 状态图标缺图用统一状态占位（S14 §12.2），其他对象用统一物品占位
            inner = key.StartsWith("status_") ? Status["status_unknown"] : Extra["placeholder"];
        }
        return $"<svg viewBox=\"0 0 100 100\" aria-hidden=\"true\">{inner}</svg>";
    }

    public static MarkupString Markup(string key) => new(Svg(key));

    public static string ForResource(string nodeId, IReadOnlyDictionary<string, int> outputs) =>
        ArtAssets.Sprites.ContainsKey("resource_" + nodeId) ? "resource_" + nodeId : nodeId switch
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
        "hardware_weapon_cache" => "crowbar",
        "village_weapon_locker" => "pistol",
        "clinic_medical_supply" => "antibiotics",
        "village_cold_box" => "antiviral_precursor",
        _ => outputs.Keys.FirstOrDefault() ?? "stone",
    };
}
