using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using Survival.Core.Config;
using Survival.Web.Services;

namespace Survival.Core.Tests;

/// <summary>
/// B37a1 素材底座（S14 2026-11-17 B37 节、02 Q806、Q837—Q840、Q883；16 号稿“开发维护”运行时键表）：
/// ink-v2 清单原样入库并校验 sha256、全部配置 ID 按稳定 ID 取到新图或正好落在登记缺项、Q838 四个同义、旧图集不再引用、位图最近邻。
/// 取图规则 <see cref="ArtCatalog"/> 由网页工程源文件直接编译进本测试（见 csproj）。
/// </summary>
public class B37aArtTests
{
    private static string Root()
    {
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "web", "Survival.sln"))) dir = dir.Parent;
        Assert.That(dir, Is.Not.Null, "找到仓库根目录");
        return dir!.FullName;
    }

    private static string Web(params string[] parts) => Path.Combine(new[] { Root(), "web", "Survival.Web" }.Concat(parts).ToArray());
    private static string WebText(params string[] parts) => File.ReadAllText(Web(parts));
    private static string Sha(byte[] data) => Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();

    private static readonly Lazy<string> RuntimeJson = new(() => WebText("Art", "ink-v2.json"));
    private static ArtCatalog Cat => _cat.Value;
    private static readonly Lazy<ArtCatalog> _cat = new(() => ArtCatalog.Parse(RuntimeJson.Value));
    private static GameConfig Cfg => _cfg.Value;
    private static readonly Lazy<GameConfig> _cfg = new(() => ConfigLoader.FromJson(File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "config.v0.6.json"))));

    /// <summary>a9b93f0 原 manifest 的逐字节副本（tools/import-b37a1-ink-v2.py 从 git 取出）。</summary>
    private static string SourceCopy => Path.Combine(Root(), "doc", "验收", "重墨V2 UI重做 v1.0", "素材来源 a9b93f0 manifest.json");

    // ───── M1：清单与文件 ─────

    /// <summary>
    /// Q806／Q840：a9b93f0 清单引用的 176 个文件名按内容去重为 117 个文件（开工前核对 M1015：“manifest 176 引用、去重 117 文件”），
    /// 字节原样入库：每个文件存在、sha256 与清单一致，目录里没有清单外的文件；Q839：不拷 portrait-p0-design.webp。
    /// </summary>
    [Test]
    public void M1_FilesShippedByteExact()
    {
        var files = (JObject)JObject.Parse(RuntimeJson.Value)["files"]!;
        Assert.That(files.Count, Is.EqualTo(117), "去重后 117 个文件");
        Assert.That(files.Properties().Sum(p => ((JArray)p.Value["sources"]!).Count), Is.EqualTo(176), "原 176 个文件名都有落点");
        var dir = Web("wwwroot", "art", "ink-v2");
        foreach (var p in files.Properties())
        {
            var path = Path.Combine(dir, p.Name);
            Assert.That(File.Exists(path), p.Name);
            var bytes = File.ReadAllBytes(path);
            Assert.That(bytes.LongLength, Is.EqualTo((long)p.Value["bytes"]!), p.Name);
            Assert.That(Sha(bytes), Is.EqualTo((string)p.Value["sha256"]!), p.Name);
        }
        var onDisk = Directory.GetFiles(dir).Select(Path.GetFileName).ToHashSet();
        Assert.That(onDisk, Is.EquivalentTo(files.Properties().Select(p => p.Name)), "目录与清单一一对应");
        Assert.That(onDisk, Does.Not.Contain("portrait-p0-design.webp"), "Q839 A：不用效果图版主角");
    }

    /// <summary>Q839 A：主角肖像 actor:p0 → portrait-actor-p0.webp，sha256 等于 assets/20261117/b37-assets-and-appearance-q834-q840.json 记录值。</summary>
    [Test]
    public void M1_ProtagonistPortraitHash()
    {
        Assert.That(Cat.Portraits["actor:p0"], Is.EqualTo("portrait-actor-p0.webp"));
        var bytes = File.ReadAllBytes(Web("wwwroot", "art", "ink-v2", "portrait-actor-p0.webp"));
        Assert.That(Sha(bytes), Is.EqualTo("fe73a31aa1be969966fcc1c56533955a21657edb1eaaabc8e37b30b22678e32c"), "b37-assets…json protagonist.portraitSourceAudit.sha256");
        Assert.That(Cat.Sprites["actor:p0"].File, Is.EqualTo("atlas-actor-survivor-sample.webp"), "Q839 A：全身沿原裁窗");
    }

    /// <summary>
    /// 运行时清单的每条绑定（裁窗、尺寸、文件）与 a9b93f0 原 manifest 一致：原文件名都落在去重后同内容文件的来源名里。
    /// 原 manifest 副本 sha256 是 a9b93f0 该文件的值（6dee53b3…）。数量：精灵 647、肖像 52、场景 99（开工前核对 M1015 ID 对照）。
    /// </summary>
    [Test]
    public void M1_BindingsMatchA9b93f0()
    {
        var raw = File.ReadAllBytes(SourceCopy);
        Assert.That(Sha(raw), Is.EqualTo("6dee53b3729286588b149b1066925cb8eb7974ef66eb227d7258cf21763ff906"), "a9b93f0 manifest.json");
        Assert.That(Cat.SourceSha256, Is.EqualTo(Sha(raw)));
        var src = JObject.Parse(System.Text.Encoding.UTF8.GetString(raw));
        var sprites = (JObject)src["sprites"]!;
        var portraits = (JObject)src["portraits"]!;
        var scenes = (JObject)src["scenes"]!;
        Assert.That(sprites.Count, Is.EqualTo(647));
        Assert.That(portraits.Count, Is.EqualTo(52));
        Assert.That(scenes.Count, Is.EqualTo(99));
        Assert.That(Cat.Sprites.Count, Is.EqualTo(647));
        Assert.That(Cat.Portraits.Count, Is.EqualTo(52));
        Assert.That(Cat.Scenes.Count, Is.EqualTo(99));
        bool SameFile(string runtimeFile, string sourceFile) => Cat.Files[runtimeFile].Sources.Contains(sourceFile);
        foreach (var p in sprites.Properties())
        {
            var f = Cat.Sprites[p.Name];
            Assert.That(SameFile(f.File, (string)p.Value["file"]!), p.Name);
            Assert.That(f.Crop, Is.EqualTo(p.Value["crop"]!.ToObject<int[]>()), p.Name);
            Assert.That(f.Size, Is.EqualTo(p.Value["size"]!.ToObject<int[]>()), p.Name);
        }
        foreach (var p in portraits.Properties()) Assert.That(SameFile(Cat.Portraits[p.Name], (string)p.Value!), p.Name);
        foreach (var p in scenes.Properties()) Assert.That(SameFile(Cat.Scenes[p.Name], (string)p.Value!), p.Name);
    }

    // ───── M2：全部配置 ID 取图 ─────

    /// <summary>
    /// 物品 item:&lt;itemId&gt;（16 号稿键表）：配置 181 个，180 个取到清单图；唯一缺图是腐败物 spoiled_matter（Q838 登记，保持现有占位，不借相近图）。
    /// </summary>
    [Test]
    public void M2_Items()
    {
        Assert.That(Cfg.Items.Count, Is.EqualTo(181));
        var sprite = 0;
        foreach (var it in Cfg.Items)
        {
            var plan = Cat.Plan(it.Id);
            if (it.Id == "spoiled_matter")
            {
                Assert.That(plan, Is.EqualTo((ArtKind.Legacy, "spoiled_matter")), "Q838：腐败物保持现有占位");
                continue;
            }
            Assert.That(plan, Is.EqualTo((ArtKind.Sprite, "item:" + it.Id)), it.Id);
            sprite++;
        }
        Assert.That(sprite, Is.EqualTo(180));
        Assert.That(ArtCatalog.MissingObjects.Select(m => m.Key), Does.Contain("item:spoiled_matter"));
    }

    /// <summary>敌人 enemy:&lt;enemyId&gt;：42 种全部有图，界面旧键 enemy_&lt;id&gt; 直接对上（不再借旧敌人图）。</summary>
    [Test]
    public void M2_Enemies()
    {
        Assert.That(Cfg.Enemies.Count, Is.EqualTo(42));
        foreach (var en in Cfg.Enemies)
            Assert.That(Cat.Plan("enemy_" + en.Id), Is.EqualTo((ArtKind.Sprite, "enemy:" + en.Id)), en.Id);
    }

    /// <summary>
    /// 状态 status:&lt;图标键&gt;：左栏状态图标键（StatusView 源码里出现的 status_*）共 14 个（14 号稿“14 个现有 HUD 状态”），全部有图；
    /// 濒死（Q838 登记）沿用统一状态占位 status_unknown，清单里没有濒死专用键。
    /// </summary>
    [Test]
    public void M2_Statuses()
    {
        var src = WebText("Services", "StatusView.cs");
        var keys = Regex.Matches(src, "\"(status_[a-z_]+)\"").Select(m => m.Groups[1].Value).Distinct().ToList();
        Assert.That(keys.Count, Is.EqualTo(14), string.Join(",", keys));
        foreach (var k in keys) Assert.That(Cat.Plan(k), Is.EqualTo((ArtKind.Sprite, "status:" + k)), k);
        Assert.That(Regex.IsMatch(src, "Id = \"dying\"[^}]*Icon = \"status_unknown\""), "濒死用统一状态占位");
        Assert.That(Cat.HasSprite("status:dying"), Is.False);
        Assert.That(ArtCatalog.MissingObjects.Select(m => m.Key), Does.Contain("status:dying"));
    }

    /// <summary>技能 skill:&lt;节点ID&gt;：75 个节点全部有图。</summary>
    [Test]
    public void M2_SkillNodes()
    {
        Assert.That(Cfg.SkillNodes.Count, Is.EqualTo(75));
        foreach (var n in Cfg.SkillNodes) Assert.That(Cat.HasSprite("skill:" + n.Id), n.Id);
    }

    /// <summary>熟练度门类 skillFamily:&lt;门类ID&gt;：17 门里 15 门有图；潜行 stealth、意志 willpower 没有图（Q838 登记）。</summary>
    [Test]
    public void M2_SkillFamilies()
    {
        Assert.That(Cfg.Skills.Count, Is.EqualTo(17));
        var missing = Cfg.Skills.Where(s => !Cat.HasSprite("skillFamily:" + s.Id)).Select(s => s.Id).ToList();
        Assert.That(missing, Is.EquivalentTo(new[] { "stealth", "willpower" }));
        Assert.That(Cfg.Skills.Count(s => Cat.HasSprite("skillFamily:" + s.Id)), Is.EqualTo(15));
        Assert.That(ArtCatalog.MissingObjects.Select(m => m.Key), Is.SupersetOf(new[] { "skillFamily:stealth", "skillFamily:willpower" }));
    }

    /// <summary>资源点 resource:&lt;地点&gt;:&lt;节点&gt;：169 个全部有图（不再按产物猜图）。</summary>
    [Test]
    public void M2_Resources()
    {
        Assert.That(Cfg.Resources.Count, Is.EqualTo(169));
        foreach (var r in Cfg.Resources)
        {
            var key = ArtCatalog.ResourceKey(r.LocationId, r.NodeId);
            Assert.That(key, Is.EqualTo($"resource:{r.LocationId}:{r.NodeId}"));
            Assert.That(Cat.Plan(key), Is.EqualTo((ArtKind.Sprite, key)), key);
        }
        var razor = WebText("Components", "LocationStrip.razor") + WebText("Components", "ActionPops.razor");
        Assert.That(Regex.Matches(razor, @"ArtCatalog\.ResourceKey\(r\.LocationId, r\.NodeId\)").Count, Is.EqualTo(2), "资源卡与采集浮窗");
        Assert.That(razor, Does.Not.Contain("ForResource"));
    }

    /// <summary>
    /// 设施 facility:&lt;设施ID&gt;：9 个工坊站点、所有建造类配方、营地设施卡与火堆都取设施图（不再借零件／布／绷带等物品图）。
    /// </summary>
    [Test]
    public void M2_Facilities()
    {
        var stations = Cfg.Workshops.Stations.Select(x => x.Id).ToList();
        Assert.That(stations.Count, Is.EqualTo(9));
        foreach (var id in stations) Assert.That(Cat.HasSprite("facility:" + id), id);
        var builds = Cfg.Recipes.Where(r => r.FacilityResult != null).ToList();
        Assert.That(builds.Select(r => r.FacilityResult).Distinct().Count(), Is.EqualTo(12));
        foreach (var r in builds)
        {
            var key = ArtCatalog.RecipeKey(r.FacilityResult, r.IsRepair, r.Outputs.Keys.FirstOrDefault());
            Assert.That(key, Does.StartWith("facility:"), r.Id);
            Assert.That(Cat.HasSprite(key), r.Id + " → " + key);
        }
        Assert.That(ArtCatalog.RecipeKey("workshop:forge", false, null), Is.EqualTo("facility:forge"));
        Assert.That(ArtCatalog.FacilityCardKey("shelter"), Is.EqualTo("facility:shelter"));
        Assert.That(ArtCatalog.FacilityCardKey("bed"), Is.EqualTo("facility:bed"));
        Assert.That(ArtCatalog.FireKey(true, false), Is.EqualTo("facility:fire_lit"));
        Assert.That(ArtCatalog.FireKey(false, false), Is.EqualTo("facility:fire_out"));
        Assert.That(WebText("Components", "PublicStationCard.razor"), Does.Contain("Icons.Markup(\"facility:\" + Station.StationId)"));
        Assert.That(WebText("Components", "SafehouseCard.razor"), Does.Contain("Icons.Markup(\"facility:safehouse\")"));
        Assert.That(WebText("Components", "DryingRackCard.razor"), Does.Contain("Icons.Markup(\"facility:drying_rack\")"));
    }

    /// <summary>指令 action:&lt;动作ID&gt;：12 个战斗动作全部有图；界面旧键 cmd_* 对上同名动作，换位／换武器直接写动作键。</summary>
    [Test]
    public void M2_Commands()
    {
        Assert.That(Cfg.CombatActions.Count, Is.EqualTo(12));
        foreach (var a in Cfg.CombatActions) Assert.That(Cat.HasSprite("action:" + a.Id), a.Id);
        foreach (var a in new[] { "guard", "breathe", "reload", "retreat", "end_turn" })
            Assert.That(Cat.Plan("cmd_" + a), Is.EqualTo((ArtKind.Sprite, "action:" + a)), a);
        var battle = WebText("Components", "BattleScreen.razor");
        Assert.That(battle, Does.Not.Contain("cmd_switch"));
        Assert.That(Regex.Matches(battle, "\"action:swap\"").Count, Is.EqualTo(2), "调整站位卡与指令图");
        Assert.That(Regex.Matches(battle, "\"skill:\" \\+ (o\\.NodeId|node\\.Id)").Count, Is.EqualTo(3), "节点技能（可用、被动、未解锁）用技能图");
    }

    /// <summary>生命类小图标 glyph:&lt;属性&gt;：八个属性键都有图，VitalIcon 按属性键取。</summary>
    [Test]
    public void M2_VitalGlyphs()
    {
        foreach (var k in new[] { "health", "satiety", "hydration", "energy", "wakefulness", "morale", "immunity", "virus" })
            Assert.That(Cat.HasSprite("glyph:" + k), k);
        Assert.That(WebText("Components", "VitalIcon.razor"), Does.Contain("Icons.Markup(\"glyph:\" + Stat)"));
    }

    // ───── Q838、Q883 ─────

    /// <summary>Q838 A 四个准确同义：徒手→skillFamily:unarmed、工坊建造卡→facility:workbench、展开柴炉→item:folding_stove、南口路标→glyph:route。</summary>
    [Test]
    public void Q838_FourSynonyms()
    {
        Assert.That(Cat.Plan("fists"), Is.EqualTo((ArtKind.Sprite, "skillFamily:unarmed")));
        Assert.That(ArtCatalog.FacilityCardKey("workshops"), Is.EqualTo("facility:workbench"));
        Assert.That(ArtCatalog.FireKey(true, true), Is.EqualTo("item:folding_stove"));
        Assert.That(ArtCatalog.FireKey(false, true), Is.EqualTo("item:folding_stove"));
        Assert.That(ArtCatalog.DocumentKey("village_south_sign"), Is.EqualTo("glyph:route"));
        foreach (var k in new[] { "skillFamily:unarmed", "facility:workbench", "item:folding_stove", "glyph:route" })
            Assert.That(Cat.Plan(k), Is.EqualTo((ArtKind.Sprite, k)), k);
        Assert.That(WebText("Components", "FacilityCard.razor"), Does.Contain("ArtCatalog.FacilityCardKey(Kind)"));
        Assert.That(WebText("Components", "FireCard.razor"), Does.Contain("ArtCatalog.FireKey(fac.FireLit, fac.StoveInstanceId.HasValue)"));
        Assert.That(WebText("Components", "PrologueCards.razor"), Does.Contain("ArtCatalog.DocumentKey(id)"));
    }

    /// <summary>
    /// Q838：五个缺图对象登记且不借图——腐败物、公开救助残页保持现有占位矢量图；濒死用统一状态占位；潜行、意志无图。
    /// 对照：同一路径上有图的对象（南口路标、木材）确实取到清单图（见 <see cref="Q838_FourSynonyms"/>、<see cref="M2_Items"/>）。
    /// </summary>
    [Test]
    public void Q838_FiveMissingObjectsKeepPlaceholder()
    {
        Assert.That(ArtCatalog.MissingObjects.Select(m => m.Name), Is.EqualTo(new[] { "腐败物", "濒死", "潜行", "意志", "公开救助残页" }));
        Assert.That(Cat.Plan("spoiled_matter"), Is.EqualTo((ArtKind.Legacy, "spoiled_matter")));
        Assert.That(ArtCatalog.DocumentKey("relief_page_village"), Is.EqualTo("placeholder"));
        Assert.That(Cat.Plan(ArtCatalog.DocumentKey("relief_page_village")), Is.EqualTo((ArtKind.Legacy, "placeholder")));
        Assert.That(Cfg.Prologue.Documents.Select(d => d.Id), Is.EquivalentTo(new[] { "relief_page_village", "village_south_sign" }));
        foreach (var (_, key) in ArtCatalog.MissingObjects) Assert.That(Cat.HasSprite(key), Is.False, key);
    }

    /// <summary>
    /// BM11 改断言出处：S14／美术14／16 文首 2026-11-17 §Q883 用户新裁定（原 B37a1“保持现有图”取代）：9 处按同义 glyph 暂用接入，
    /// 负重 storage／昼夜 watch／空位 add／维修 crafting／按键 help／Esc return／推进 progress／保存 save／未知 unknown；backpack／slot_empty／repair 旧图撤下。
    /// </summary>
    [Test]
    public void Q883_SlotsUseInterimGlyphs()
    {
        Assert.That(ArtCatalog.Q883Interim, Has.Count.EqualTo(9));
        foreach (var (_, key) in ArtCatalog.Q883Interim) Assert.That(Cat.Plan(key), Is.EqualTo((ArtKind.Sprite, key)), key);
        foreach (var k in new[] { "backpack", "slot_empty", "repair" }) Assert.That(Cat.Plan(k).Kind, Is.EqualTo(ArtKind.Missing), k);
        Assert.That(Cat.Plan("outdoor"), Is.EqualTo((ArtKind.Legacy, "outdoor")), "探索帮助不在本题，仍原图");
        Assert.That(WebText("Components", "Hud.razor"), Does.Contain("@Icons.Markup(\"glyph:storage\")").And.Not.Contain("\"backpack\""));
        Assert.That(ArtCatalog.RecipeKey(null, true, "x"), Is.EqualTo("skillFamily:crafting"));
        var help = WebText("Services", "HelpCatalog.cs");
        Assert.That(Regex.Matches(help, "Icon = \"placeholder\"").Count, Is.EqualTo(0));
        foreach (var (id, icon) in new[] { ("keys", "glyph:help"), ("esc", "glyph:return"), ("progress", "glyph:progress"), ("save", "glyph:save") })
            Assert.That(help, Does.Match($"Id = \"{id}\", Title = [^,]+, Icon = \"{Regex.Escape(icon)}\""), id);
        Assert.That(help, Does.Contain("Id = \"clock\", Title = \"世界时钟与昼夜\", Icon = \"glyph:watch\""));
        Assert.That(WebText("Components", "CraftOverlay.razor"), Does.Contain("(\"glyph:add\", \"空加工位\","));
        Assert.That(WebText("Components", "BattleScreen.razor"), Does.Contain("_ => (\"glyph:unknown\", null, null),"));
    }

    // ───── M3：场景 ─────

    /// <summary>
    /// 143 个地点：清单 99 处用新图；16 号稿批准沿用旧地景 7 处（五金店 v10）；Q837 列出的 37 处（a01—a03、b02、b03、o03，w01—w34 除 w19、w23、w27）保持统一占位。
    /// </summary>
    [Test]
    public void M3_Scenes()
    {
        Assert.That(Cfg.Locations.Count, Is.EqualTo(143));
        var approved = new Dictionary<string, string>
        {
            ["field"] = "art/v08/field.png", ["stream"] = "art/v08/stream.png", ["woods"] = "art/v08/woods.png", ["village"] = "art/v08/village.png",
            ["hardware"] = "art/v10/hardware.png", ["clinic"] = "art/v08/clinic.png", ["scrapyard"] = "art/v08/scrapyard.png",
        };
        var q837 = new HashSet<string> { "a01", "a02", "a03", "b02", "b03", "o03" };
        for (var i = 1; i <= 34; i++) if (i is not (19 or 23 or 27)) q837.Add($"w{i:00}");
        Assert.That(q837.Count, Is.EqualTo(37));
        int fresh = 0, old = 0, hold = 0;
        foreach (var l in Cfg.Locations)
        {
            var scene = Cat.Scene(l.Id);
            if (approved.TryGetValue(l.Id, out var path)) { Assert.That(scene, Is.EqualTo(path), l.Id); Assert.That(File.Exists(Web("wwwroot", path)), path); old++; }
            else if (q837.Contains(l.Id.Split('_')[0])) { Assert.That(scene, Is.EqualTo("scenes/placeholder.svg"), l.Id); hold++; }
            else
            {
                Assert.That(scene, Is.EqualTo("art/ink-v2/" + Cat.Scenes[l.Id]), l.Id);
                Assert.That(File.Exists(Web("wwwroot", scene)), scene);
                fresh++;
            }
        }
        Assert.That((fresh, old, hold), Is.EqualTo((99, 7, 37)));
        Assert.That(File.Exists(Web("wwwroot", "scenes", "placeholder.svg")));
        Assert.That(Cat.Scene("camp"), Is.EqualTo("art/ink-v2/scene-camp-outskirts.webp"), "营地用清单新图");
    }

    // ───── M4、M5 ─────

    /// <summary>Q840 B：根样式 image-rendering: pixelated（继承到全部位图），且全站没有别处把它改回平滑。</summary>
    [Test]
    public void M4_NearestNeighborRule()
    {
        Assert.That(WebText("wwwroot", "css", "app.css"), Does.Contain(":root { image-rendering: pixelated; }"));
        var other = Directory.GetFiles(Web("wwwroot", "css"), "*.css").Concat(Directory.GetFiles(Web("Components"), "*.razor"))
            .Where(f => Regex.Matches(File.ReadAllText(f), "image-rendering\\s*:\\s*([a-z-]+)").Any(m => m.Groups[1].Value != "pixelated")).Select(Path.GetFileName).ToList();
        Assert.That(other, Is.Empty);
    }

    /// <summary>
    /// 删旧图：v08 的 supplies／tools／equipment／resources／camp／hardware 六张图集与场景、v10 resource-focus、八张旧 scenes/*.svg 不在发行目录、源码不再引用；
    /// v08 creatures 只因 Q883 负重图位保留。对照：批准沿用的旧地景仍在（见 <see cref="M3_Scenes"/>）。
    /// </summary>
    [Test]
    public void M5_OldArtRemoved()
    {
        var gone = new[] { "art/v08/supplies.png", "art/v08/tools.png", "art/v08/equipment.png", "art/v08/resources.png", "art/v08/camp.png", "art/v08/hardware.png",
            "art/v10/resource-focus.png", "scenes/camp.svg", "scenes/field.svg", "scenes/woods.svg", "scenes/stream.svg", "scenes/village.svg", "scenes/hardware.svg",
            "scenes/scrapyard.svg", "scenes/clinic.svg" };
        Assert.That(gone.Length, Is.EqualTo(15));
        foreach (var g in gone) Assert.That(File.Exists(Web("wwwroot", g)), Is.False, g);
        var sources = Directory.GetFiles(Web("Components"), "*.razor").Concat(Directory.GetFiles(Web("Services"), "*.cs"))
            .Concat(Directory.GetFiles(Web("wwwroot", "css"), "*.css")).Concat(Directory.GetFiles(Web("wwwroot", "js"), "*.js")).Append(Web("wwwroot", "index.html")).ToList();
        Assert.That(sources.Count, Is.GreaterThan(50));
        var all = string.Join("\n", sources.Select(File.ReadAllText));
        foreach (var g in gone) Assert.That(all, Does.Not.Contain(g), g);
        // BM11 改断言出处：S14 文首 2026-11-17 §Q883 用户新裁定——负重改 glyph:storage，代码里只剩旧 .weight 死样式一处
        Assert.That(Regex.Matches(all, "art/v08/creatures\\.png").Count, Is.EqualTo(1), "仅旧 .weight 样式（无元素使用）");
        Assert.That(all, Does.Not.Contain("EnemyArtAlias").And.Not.Contain("RegisterCategories").And.Not.Contain("\"ph_"));
        foreach (var f in new[] { "Icons.Extracted.cs", "Icons.Combat.cs", "Icons.Status.cs" }) Assert.That(File.Exists(Web("Services", f)), Is.False, f);
    }
}
