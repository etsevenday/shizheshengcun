using Newtonsoft.Json;

namespace Survival.Web.Services;

/// <summary>取图方式：清单位图、保留的旧图（登记缺图对象的现有占位）、或统一缺图占位。</summary>
public enum ArtKind { Sprite, Legacy, Missing }

/// <summary>
/// B37a1（Q806、Q838、Q837、Q883）：ink-v2 运行时清单与稳定 ID 取图规则。纯数据与字符串，不依赖界面，测试直接编译本文件。
/// 稳定 ID 命名按 16 号稿“开发维护”表：item:／enemy:／status:／action:／facility:／skill:／skillFamily:／resource:&lt;地点&gt;:&lt;节点&gt;／glyph:。
/// 清单由 tools/import-b37a1-ink-v2.py 从 a9b93f0 生成（同内容文件只留一份）。
/// </summary>
public sealed class ArtCatalog
{
    public const string Root = "art/ink-v2/";

    /// <summary>Q837 A：没有场景图的地点保持原统一占位场景（登记缺项）。</summary>
    public const string PlaceholderScene = "scenes/placeholder.svg";

    /// <summary>16 号稿“已核定的旧版独立地景继续用于农野荒地、溪流、林地、村庄、五金店、诊所和废料场”；五金店沿用 v10 版。</summary>
    public static readonly IReadOnlyDictionary<string, string> ApprovedOldScenes = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["field"] = "art/v08/field.png",
        ["stream"] = "art/v08/stream.png",
        ["woods"] = "art/v08/woods.png",
        ["village"] = "art/v08/village.png",
        ["hardware"] = "art/v10/hardware.png",
        ["clinic"] = "art/v08/clinic.png",
        ["scrapyard"] = "art/v08/scrapyard.png",
    };

    /// <summary>旧键与稳定 ID 的准确同义（Q838 A 徒手／南口路标；帮助目录按名字直接对上；原 v08 的 fire 即 fire_lit 别名）。</summary>
    private static readonly Dictionary<string, string> Synonyms = new(StringComparer.Ordinal)
    {
        ["fists"] = "skillFamily:unarmed",
        ["route"] = "glyph:route",
        ["camp"] = "glyph:camp",
        ["fire"] = "facility:fire_lit",
    };

    /// <summary>
    /// 保留现有图的旧键：探索帮助 outdoor、统一缺图占位 placeholder（公开救助残页、其他缺图兜底）；腐败物 spoiled_matter（B53 专用 SVG，Q885 A 暂接、待开发替换）。
    /// BM11（Q883 用户新裁定）：负重、昼夜、帮助四题、空加工位、修理、战斗未知指令 9 处改接同义 glyph（暂用，待开发替换），backpack／slot_empty／repair 旧图撤下。
    /// 濒死用 status_unknown（清单内统一状态占位）。
    /// </summary>
    public static readonly IReadOnlySet<string> LegacyKept = new HashSet<string>(StringComparer.Ordinal)
    {
        "outdoor", "placeholder", "spoiled_matter",
    };

    /// <summary>BM11（Q883 用户新裁定）：9 处暂用同义 glyph（图位 → 稳定 ID），素材清单记“暂用，待开发替换”。</summary>
    public static readonly IReadOnlyList<(string Slot, string Key)> Q883Interim = new[]
    {
        ("顶栏负重", "glyph:storage"),
        ("帮助“世界时钟与昼夜”", "glyph:watch"),
        ("制作帮助“空加工位”", "glyph:add"),
        ("修理配方", "skillFamily:crafting"),
        ("帮助“按键”", "glyph:help"),
        ("帮助“Esc”", "glyph:return"),
        ("帮助“中央推进条、暂停与退出”", "glyph:progress"),
        ("帮助“保存与继续”", "glyph:save"),
        ("战斗未知指令兜底", "glyph:unknown"),
    };

    /// <summary>Q838：继续占位、登记缺项的五个对象（对象名 → 应有的稳定 ID）。</summary>
    public static readonly IReadOnlyList<(string Name, string Key)> MissingObjects = new[]
    {
        ("腐败物", "item:spoiled_matter"),
        ("濒死", "status:dying"),
        ("潜行", "skillFamily:stealth"),
        ("意志", "skillFamily:willpower"),
        ("公开救助残页", "document:relief_page_village"),
    };

    /// <summary>
    /// B37a2：顶栏 12 键键面图标，顺序同 ShellText.TopKeys（区域地图、手工制作、休息、行动日志、编队、整备、成长、生存目标、生存提示、对话记录、系统说明、菜单）；
    /// 取自 a9b93f0《资料/逐页坐标与组件槽.json》P01a 顶栏 12 个 control 槽内的 art 槽（18×18）。
    /// </summary>
    public static readonly IReadOnlyList<string> TopKeyGlyphs = new[]
    {
        "glyph:map", "glyph:craft", "glyph:rest", "glyph:log", "glyph:formation", "glyph:equipment",
        "glyph:growth", "glyph:goal", "glyph:warning", "glyph:dialogue", "glyph:help", "glyph:menu",
    };

    public IReadOnlyDictionary<string, Frame> Sprites { get; }
    public IReadOnlyDictionary<string, string> Portraits { get; }
    public IReadOnlyDictionary<string, string> Scenes { get; }
    public IReadOnlyDictionary<string, FileEntry> Files { get; }
    public string Source { get; }
    public string SourceSha256 { get; }

    private ArtCatalog(Model m)
    {
        Sprites = m.Sprites;
        Portraits = m.Portraits;
        Scenes = m.Scenes;
        Files = m.Files;
        Source = m.Source;
        SourceSha256 = m.SourceSha256;
    }

    public static ArtCatalog Parse(string json) => new(JsonConvert.DeserializeObject<Model>(json)!);

    public bool HasSprite(string key) => Sprites.ContainsKey(key);

    /// <summary>界面键 → 稳定 ID。已带命名空间的原样返回；旧键按准确同义、物品、敌人、状态、指令、设施依次对上；对不上的原样返回。</summary>
    public string Resolve(string key)
    {
        if (key.Contains(':')) return key;
        if (Synonyms.TryGetValue(key, out var syn)) return syn;
        if (HasSprite("item:" + key)) return "item:" + key;
        if (key.StartsWith("enemy_", StringComparison.Ordinal)) return "enemy:" + key[6..];
        if (key.StartsWith("status_", StringComparison.Ordinal)) return "status:" + key;
        if (key.StartsWith("cmd_", StringComparison.Ordinal)) return "action:" + key[4..];
        if (HasSprite("facility:" + key)) return "facility:" + key;
        return key;
    }

    /// <summary>取图方案：清单有图用清单；保留键用现有图；其余为缺图（状态用清单内统一状态占位，其他用统一物品占位）。</summary>
    public (ArtKind Kind, string Key) Plan(string key)
    {
        var stable = Resolve(key);
        if (HasSprite(stable)) return (ArtKind.Sprite, stable);
        if (LegacyKept.Contains(key)) return (ArtKind.Legacy, key);
        return (ArtKind.Missing, key.StartsWith("status_", StringComparison.Ordinal) || key.StartsWith("status:", StringComparison.Ordinal) ? "status:status_unknown" : "placeholder");
    }

    /// <summary>地点场景：清单 99 处用新图；16 号稿批准的 7 处沿用旧地景；其余统一占位（Q837 A）。</summary>
    public string Scene(string locationId) =>
        Scenes.TryGetValue(locationId, out var file) ? Root + file
        : ApprovedOldScenes.TryGetValue(locationId, out var old) ? old
        : PlaceholderScene;

    /// <summary>营地设施卡：工坊建造卡用工作台（Q838 A），遮蔽棚／床垫按设施键。</summary>
    public static string FacilityCardKey(string kind) => kind == "workshops" ? "facility:workbench" : "facility:" + kind;

    /// <summary>火堆卡与详情：展开的柴炉用 item:folding_stove（Q838 A），火堆按燃烧状态。</summary>
    public static string FireKey(bool lit, bool stove) => stove ? "item:folding_stove" : lit ? "facility:fire_lit" : "facility:fire_out";

    /// <summary>文档卡：南口路标用 glyph:route（Q838 A）；公开救助残页无图，保持统一占位（登记缺项）。</summary>
    public static string DocumentKey(string docId) => docId == "village_south_sign" ? "glyph:route" : "placeholder";

    /// <summary>制作目录配方图：建造类按设施键；修理暂用 skillFamily:crafting（BM11 Q883 用户新裁定，待开发替换）；其余用首个产物。</summary>
    public static string RecipeKey(string? facilityResult, bool isRepair, string? firstOutput) => facilityResult switch
    {
        "shelterBuilt" => "facility:shelter",
        "bedBuilt" => "facility:bed",
        "safehouse" => "facility:safehouse",
        "fireBuilt" => "facility:fire_out",
        string r when r.StartsWith("workshop:", StringComparison.Ordinal) => "facility:" + r[9..],
        _ => isRepair ? "skillFamily:crafting" : firstOutput ?? "stone",
    };

    /// <summary>资源点卡：resource:&lt;地点&gt;:&lt;节点&gt;。</summary>
    public static string ResourceKey(string locationId, string nodeId) => $"resource:{locationId}:{nodeId}";

    public sealed record Frame(string File, int[] Size, int[] Crop);
    public sealed record FileEntry(string Sha256, long Bytes, List<string> Sources);
    private sealed record Model(string Source, string SourceSha256, Dictionary<string, FileEntry> Files,
        Dictionary<string, Frame> Sprites, Dictionary<string, string> Portraits, Dictionary<string, string> Scenes);
}
