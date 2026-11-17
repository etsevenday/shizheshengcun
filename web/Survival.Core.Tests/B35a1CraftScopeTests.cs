using Newtonsoft.Json.Linq;
using Survival.Core.Config;
using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 B35a1「UI 重做：制作分流、独立设施卡、秘密配方、设施建造命名」。
/// 出处：S08／S14 文首 2026-11-14 Q600（recipe-visibility-q600.json）、2026-11-16 B35 节 Q594／Q795—Q797／Q805（S14 1452—1464 验收表）、
/// 2026-11-17 Q904（b35-three-single-originals-q904-q906.json）、Q907—Q917（b35-secondary-ui-q907-q917.json）、
/// Q930／Q931（b35a1-primary-q930-q931.json）、Q932—Q937（b35a1-secondary-q932-q937-construction.json）。
/// 内核规则直接测；网页壳的接线以源码检查（同 B31b—B45）。
/// </summary>
public class B35a1CraftScopeTests : TestBase
{
    private static FacilityState Fac(GameState s) => s.Locations["camp"].Facility;

    private static void Ok(ActionResult r) => Assert.That(r.Ok, Is.True, r.Reason);

    private static readonly string[] HandTabIds = { "all", "hand", "build", "repair" };

    // ───────────── 合同读取 ─────────────

    private static string Root()
    {
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "web", "Survival.sln"))) dir = dir.Parent;
        Assert.That(dir, Is.Not.Null, "找到仓库根目录");
        return dir!.FullName;
    }

    private static JObject Asset(string day, string file) =>
        JObject.Parse(File.ReadAllText(Path.Combine(Root(), "doc", "策划案", "系统策划总册 v1.0", "assets", day, file)));

    private static string Web(params string[] parts) => File.ReadAllText(Path.Combine(new[] { Root(), "web", "Survival.Web" }.Concat(parts).ToArray()));

    private static string Src(params string[] parts) => File.ReadAllText(Path.Combine(new[] { Root(), "src", "Core" }.Concat(parts).ToArray()));

    /// <summary>去掉 Razor 注释（@* … *@），只核实际渲染的标记与代码。</summary>
    private static string Code(string razor) => System.Text.RegularExpressions.Regex.Replace(razor, @"@\*.*?\*@", "", System.Text.RegularExpressions.RegexOptions.Singleline);

    private static string Slice(string text, string from, string to)
    {
        var i = text.IndexOf(from, StringComparison.Ordinal);
        Assert.That(i, Is.GreaterThanOrEqualTo(0), from);
        var j = text.IndexOf(to, i + from.Length, StringComparison.Ordinal);
        Assert.That(j, Is.GreaterThan(i), to);
        return text.Substring(i, j - i);
    }

    private static JArray Q600Records() => (JArray)Asset("20261114", "recipe-visibility-q600.json")["records"]!;

    private static RecipeConfig R(string id) => Cfg.Recipes.Single(r => r.Id == id);

    /// <summary>营地新局：去掉开局敌踪，便于在营地制作。</summary>
    private GameState Camp()
    {
        var s = New();
        s.WorldEnemies.RemoveAll(w => w.LocationId == "camp");
        return s;
    }

    /// <summary>已登记、在指定城市地点的新局（同 B38b Registered）。</summary>
    private GameState Registered(string loc)
    {
        var s = New();
        s.Progress.Flags.UnionWith(new[] { "registration_authorized", "hub_registered" });
        s.GameMinute = At(2, 9, 0);
        s.SpawnNextCheck = int.MaxValue;
        s.PatrolNextCheck = int.MaxValue;
        E.DebugMapTeleport(s, loc);
        ReadDialogues(s);
        return s;
    }

    // ═════════════ Q600 秘密配方显现 ═════════════

    /// <summary>Q600-A01：合同 38 条秘密／95 条公开在当前 133 条配置逐项落地（技能、任务、知识 Id 同源）。</summary>
    [Test]
    public void Q600_A01_ContractMatchesConfig()
    {
        var doc = Asset("20261114", "recipe-visibility-q600.json");
        Assert.That((int)doc["sourceRecipeCount"]!, Is.EqualTo(133));
        Assert.That((int)doc["explicitSecretRecipeCount"]!, Is.EqualTo(38));
        Assert.That((int)doc["publicRecipeCount"]!, Is.EqualTo(95));
        Assert.That(Cfg.Recipes, Has.Count.EqualTo(133));
        var recs = Q600Records();
        Assert.That(recs, Has.Count.EqualTo(38));
        Assert.That(Cfg.Recipes.Count(r => r.Reveal != null), Is.EqualTo(38));
        Assert.That(Cfg.Recipes.Count(r => r.Reveal == null), Is.EqualTo(95));
        foreach (var rec in recs)
        {
            var id = (string)rec["recipeId"]!;
            var rc = R(id);
            Assert.That(rc.Name, Is.EqualTo((string)rec["name"]!), id);
            Assert.That(rc.Knowledge, Is.EqualTo((string?)rec["knowledgeId"]), id);
            Assert.That(rc.Reveal, Is.Not.Null, id);
            Assert.That(rc.Reveal!.SkillsAll, Is.EquivalentTo(((JObject)rec["revealSkillsAll"]!).Properties().ToDictionary(p => p.Name, p => (int)p.Value)), id);
            Assert.That(rc.Reveal.AnyAcceptedQuest, Is.EqualTo(((JArray)rec["revealAnyAcceptedQuest"]!).Select(x => (string)x!).ToList()), id);
            Assert.That(rc.Reveal.AnyCompletedQuest, Is.EqualTo(((JArray)rec["revealAnyCompletedQuest"]!).Select(x => (string)x!).ToList()), id);
        }
    }

    /// <summary>Q600-A01：首次低等级、无相关事实时秘密配方在各目录都不列；公开配方照常列（对照）。</summary>
    [Test]
    public void Q600_A01_FreshGameHidesSecretsInEveryList()
    {
        var s = Camp();
        foreach (var st in Cfg.Workshops.Stations) Fac(s).Workshops.Add(st.Id);
        var secret = Q600Records().Select(r => (string)r["recipeId"]!).ToHashSet();
        Assert.That(secret, Has.Count.EqualTo(38));
        foreach (var id in secret) Assert.That(E.RecipeVisible(s, id), Is.False, id);
        var listed = HandTabIds.SelectMany(t => E.HandCraftRecipes(s, t))
            .Concat(Cfg.Workshops.Stations.SelectMany(st => E.WorkspaceRecipes(s, st.Id, GameEngine.TabAll))).Select(r => r.Id).ToList();
        Assert.That(listed, Is.Not.Empty);
        Assert.That(listed.Where(secret.Contains), Is.Empty, "秘密配方不列名");
        // 对照：公开配方（石刀、绷带、铁板）照常列
        Assert.That(listed, Does.Contain("knife").And.Contain("bandage").And.Contain("make_iron_plate"));
        Assert.That(E.RecipeVisible(s, "knife"), Is.True);
    }

    /// <summary>Q600-A02：蓝图条在原制造 7／10 级显现；已有对应知识提前可见；可见不等于已掌握（仍不能制作）。</summary>
    [Test]
    public void Q600_A02_BlueprintRevealAtCraftLevelOrKnowledge()
    {
        var s = Camp();
        var c7 = R("make_guard_sword");      // 合同 revealSkillsAll crafting 7
        Assert.That(c7.Reveal!.SkillsAll, Is.EquivalentTo(new Dictionary<string, int> { ["crafting"] = 7 }));
        s.Skills["crafting"].Level = 6;
        Assert.That(E.RecipeVisible(s, c7), Is.False, "制造6不显现");
        s.Skills["crafting"].Level = 7;
        Assert.That(E.RecipeVisible(s, c7), Is.True, "制造7显现");
        Assert.That(E.RecipeKnown(s, c7), Is.False, "显现不授知识");
        Assert.That(E.CanStart(s, ActionRequest.Craft(c7.Id)), Is.Not.EqualTo(GameEngine.RecipeNotFoundText).And.Not.Null, "可见仍按原条件拒绝");

        var t = Camp();
        Assert.That(E.RecipeVisible(t, c7), Is.False);
        t.Progress.Flags.Add("knowledge:" + c7.Knowledge);
        Assert.That(E.RecipeVisible(t, c7), Is.True, "已有知识，技能未达也不藏回");
    }

    /// <summary>Q600-A03：支援条制造3或指定 EXQ 接取／完成任一显现；接别的 EXQ 不显现。</summary>
    [Test]
    public void Q600_A03_SupportRevealBySkillOrQuest()
    {
        var gloves = R("make_gather_gloves");   // 合同：crafting 3 或接取 EXQ06
        var s = Camp();
        Assert.That(E.RecipeVisible(s, gloves), Is.False);
        s.Progress.Flags.Add(GameEngine.QuestFact("EXQ07", "accepted"));
        Assert.That(E.RecipeVisible(s, gloves), Is.False, "EXQ07 不揭示采集手套");
        s.Progress.Flags.Add(GameEngine.QuestFact("EXQ06", "accepted"));
        Assert.That(E.RecipeVisible(s, gloves), Is.True, "接取 EXQ06 揭示");

        var c = Camp();
        c.Progress.Flags.Add(GameEngine.QuestFact("EXQ06", "completed"));
        Assert.That(E.RecipeVisible(c, gloves), Is.True, "完成也算");

        var k = Camp();
        k.Skills["crafting"].Level = 2;
        Assert.That(E.RecipeVisible(k, gloves), Is.False);
        k.Skills["crafting"].Level = 3;
        Assert.That(E.RecipeVisible(k, gloves), Is.True, "制造3显现");
        Assert.That(k.Progress.Flags, Has.None.Contain("EXQ06"), "不额外接取／完成任务");
    }

    /// <summary>Q600-A04：病毒特效药制造10且医疗10、或完成 AC02 才显现；单门10、仅接取 AC02 不显现。</summary>
    [Test]
    public void Q600_A04_AntiviralRevealNeedsBothSkillsOrAc02Done()
    {
        var cure = R("make_antiviral_cure");
        var s = Camp();
        s.Skills["medicine"].Level = 10;
        Assert.That(E.RecipeVisible(s, cure), Is.False, "只医疗10");
        s.Skills["medicine"].Level = 1;
        s.Skills["crafting"].Level = 10;
        Assert.That(E.RecipeVisible(s, cure), Is.False, "只制造10");
        s.Skills["medicine"].Level = 10;
        Assert.That(E.RecipeVisible(s, cure), Is.True, "两门都10");

        var q = Camp();
        q.Progress.Flags.Add(GameEngine.QuestFact("AC02", "accepted"));
        Assert.That(E.RecipeVisible(q, cure), Is.False, "AC02 只接取不显现");
        q.Progress.Flags.Add(GameEngine.QuestFact("AC02", "completed"));
        Assert.That(E.RecipeVisible(q, cure), Is.True, "完成 AC02 显现");
        Assert.That(E.CanStart(q, ActionRequest.Craft(cure.Id)), Does.Contain("需要医疗10级"), "任务只揭示条目，原熟练门槛照旧");
    }

    /// <summary>Q600-A05：直接请求未显现配方只回通用句、不漏名；来源与拖料候选同一判定；显现后照常出现（对照）。</summary>
    [Test]
    public void Q600_A05_DirectRequestSourcesAndCandidatesShareOneRule()
    {
        var cure = R("make_antiviral_cure");
        var s = Camp();
        var why = E.CanStart(s, ActionRequest.Craft(cure.Id));
        Assert.That(why, Is.EqualTo("尚未发现这项配方。"));
        Assert.That(why, Does.Not.Contain(cure.Name));
        Assert.That(E.ItemSources(s, "antiviral_cure").Where(v => v.Kind == "recipe"), Is.Empty, "来源不列未显现配方");
        var input = cure.Inputs.Keys.First();
        Assert.That(E.DropRecipeCandidates(s, new[] { input }, false).Select(c => c.RecipeId), Does.Not.Contain(cure.Id), "拖料候选不列");

        s.Skills["medicine"].Level = 10;
        s.Skills["crafting"].Level = 10;
        Assert.That(E.CanStart(s, ActionRequest.Craft(cure.Id)), Is.Not.EqualTo("尚未发现这项配方。"));
        var src = E.ItemSources(s, "antiviral_cure").Where(v => v.Kind == "recipe").ToList();
        Assert.That(src, Has.Count.EqualTo(1));
        Assert.That(src[0].Title, Does.Contain(cure.Name));
        Assert.That(E.DropRecipeCandidates(s, new[] { input }, false).Select(c => c.RecipeId), Does.Contain(cure.Id));
    }

    /// <summary>Q600-A07：查看／显现判定 0 时间、0 物资、不写旗标或知识。</summary>
    [Test]
    public void Q600_A07_ReadOnly()
    {
        var s = Camp();
        s.Skills["crafting"].Level = 10;
        var flags = s.Progress.Flags.Count;
        var minute = s.GameMinute;
        var items = s.Items.Sum(i => i.Count);
        foreach (var rc in Cfg.Recipes) E.RecipeVisible(s, rc);
        foreach (var t in HandTabIds) E.HandCraftRecipes(s, t);
        foreach (var st in Cfg.Workshops.Stations) E.WorkspaceRecipes(s, st.Id, GameEngine.TabAll);
        Assert.That(s.Progress.Flags, Has.Count.EqualTo(flags));
        Assert.That(s.GameMinute, Is.EqualTo(minute));
        Assert.That(s.Items.Sum(i => i.Count), Is.EqualTo(items));
        Assert.That(E.RecipeVisible(s, "make_guard_sword"), Is.True, "对照：确实显现了");
    }

    // ═════════════ Q594／Q797 两种上下文与分类 ═════════════

    /// <summary>Q594（S14 2026-11-16 B35 节）：顶栏四类“全部配方／徒手制造／设施建造／维护与修补”，首次默认“徒手制造”；工作区默认“全部配方”。</summary>
    [Test]
    public void Q594_TopTabsAndDefaults()
    {
        Assert.That(GameEngine.HandCraftTabs, Has.Count.EqualTo(4));
        Assert.That(GameEngine.HandCraftTabs.Select(t => t.Id), Is.EqualTo(HandTabIds));
        Assert.That(GameEngine.HandCraftTabs.Select(t => t.Name), Is.EqualTo(new[] { "全部配方", "徒手制造", "设施建造", "维护与修补" }));
        Assert.That(GameEngine.HandCraftDefaultTab, Is.EqualTo("hand"));
        Assert.That(GameEngine.WorkspaceDefaultTab, Is.EqualTo("all"));
    }

    /// <summary>Q594：顶栏只汇总无需工位的三类；需设施的配方、锅食谱、晾制不在顶栏；“全部配方”等于三类并集且不重复。</summary>
    [Test]
    public void Q594_TopListsOnlyNoStationRecipes()
    {
        var s = Camp();
        var hand = E.HandCraftRecipes(s, "hand");
        var build = E.HandCraftRecipes(s, "build");
        var repair = E.HandCraftRecipes(s, "repair");
        var all = E.HandCraftRecipes(s, "all");
        Assert.That(hand, Is.Not.Empty);
        Assert.That(build, Is.Not.Empty);
        Assert.That(repair, Is.Not.Empty);
        Assert.That(hand.All(r => r.Station == "hand" && !r.IsBuild && !r.IsRepair), Is.True);
        Assert.That(build.All(r => r.IsBuild), Is.True);
        Assert.That(repair.All(r => r.IsRepair && r.Station == "hand"), Is.True);
        Assert.That(all.Select(r => r.Id), Is.EquivalentTo(hand.Concat(build).Concat(repair).Select(r => r.Id)));
        Assert.That(all.Select(r => r.Id).Distinct().Count(), Is.EqualTo(all.Count));
        Assert.That(all.Where(r => r.IsPot || r.IsDrying || r.IsSalvage || E.IsWorkspaceRecipe(r)), Is.Empty);
        Assert.That(build.Select(r => r.Id), Does.Contain("build_workbench").And.Contain("shelter").And.Contain("wild_fixed_fire"));
        Assert.That(all.Select(r => r.Id), Does.Not.Contain("make_iron_plate"), "工作台配方只在工作台工作区");
    }

    /// <summary>B35-Q797-01：工作区按原配方 Group 分组（equipment“装备”、material“材料”），只列本座配方；未分组的只在“全部配方”。</summary>
    [Test]
    public void Q797_WorkspaceGroups()
    {
        var s = Camp();
        Fac(s).Workshops.Add("workbench");
        var tabs = E.WorkspaceTabs(s, "workbench");
        Assert.That(tabs, Has.Count.EqualTo(3));
        Assert.That(tabs, Is.EqualTo(new List<(string, string)> { ("all", "全部配方"), ("equipment", "装备"), ("material", "材料") }));
        var all = E.WorkspaceRecipes(s, "workbench", "all");
        Assert.That(all, Is.Not.Empty);
        Assert.That(all.All(r => r.Station == "workbench"), Is.True);
        var eq = E.WorkspaceRecipes(s, "workbench", "equipment");
        var mat = E.WorkspaceRecipes(s, "workbench", "material");
        Assert.That(eq, Is.Not.Empty);
        Assert.That(mat, Is.Not.Empty);
        Assert.That(eq.All(r => r.Group == "equipment") && mat.All(r => r.Group == "material"), Is.True);
        Assert.That(all.Select(r => r.Id), Does.Contain("ex_field_patch"), "未分组配方留在全部配方");
        Assert.That(eq.Concat(mat).Select(r => r.Id), Does.Not.Contain("ex_field_patch"));
        Assert.That(GameEngine.RecipeGroupName("workshop"), Is.Null, "不凭物名新造分组");
    }

    /// <summary>B35S-Q908-01：每座真实设施的分类记忆互不覆盖（同类型不同世界、不同设施、顶栏各一份）。</summary>
    [Test]
    public void Q908_01_TabMemoryPerFacility()
    {
        var a = Camp();
        Fac(a).Workshops.AddRange(new[] { "workbench", "forge" });
        var b = New(999);
        b.Locations["camp"].Facility.Workshops.Add("workbench");
        var wa = E.WorkspaceForStationHere(a, "workbench")!;
        var fa = E.WorkspaceForStationHere(a, "forge")!;
        var wb = E.WorkspaceForStationHere(b, "workbench")!;
        Assert.That(new[] { wa.Key, fa.Key, wb.Key, CraftTabPrefs.TopKey }.Distinct().Count(), Is.EqualTo(4), "工作台／锻炉／别的世界的工作台／顶栏各自一把键");
        var p = new CraftTabPrefs();
        p.Set(wa.Key, "equipment");
        p.Set(CraftTabPrefs.TopKey, "repair");
        Assert.That(p.Get(fa.Key), Is.Null, "工作台甲选装备不覆盖锻炉");
        Assert.That(p.Get(wb.Key), Is.Null, "不同世界同类设施不串");
        Assert.That(p.Get(wa.Key), Is.EqualTo("equipment"));
        Assert.That(p.Get(CraftTabPrefs.TopKey), Is.EqualTo("repair"));
    }

    /// <summary>B35S-Q908-02：同一设施本机记忆往返恢复；无／失效记忆回默认“全部配方”；损坏的本地记录从空记忆开始，不授予资格。</summary>
    [Test]
    public void Q908_02_TabMemoryRestoreAndFallback()
    {
        var p = new CraftTabPrefs();
        p.Set("ws|1|camp|camp|workbench", "material");
        var back = CraftTabPrefs.Parse(p.Serialize());
        var legal = new[] { "all", "equipment", "material" };
        Assert.That(back.Resolve("ws|1|camp|camp|workbench", legal, GameEngine.WorkspaceDefaultTab), Is.EqualTo("material"), "刷新读档后恢复");
        Assert.That(back.Resolve("ws|1|camp|camp|workbench", new[] { "all", "equipment" }, GameEngine.WorkspaceDefaultTab), Is.EqualTo("all"), "记忆指向的分类已不合法回默认");
        Assert.That(back.Resolve("ws|1|camp|camp|forge", legal, GameEngine.WorkspaceDefaultTab), Is.EqualTo("all"), "无记忆回默认");
        Assert.That(back.Get("ws|1|camp|camp|workbench"), Is.EqualTo("material"), "回退不写回");
        var bad = CraftTabPrefs.Parse("{not json");
        Assert.That(bad.Get("ws|1|camp|camp|workbench"), Is.Null);
        Assert.That(CraftTabPrefs.Parse("[1,2]").Serialize(), Is.EqualTo("{}"));
    }

    /// <summary>
    /// B35S-Q908-01／02、Q594 网页壳接线：分类键按上下文（顶栏 top／工作区稳定身份），只在玩家明确点分类时写入本机；
    /// 新开按记忆恢复、失效回各自默认（顶栏徒手制造、工作区全部配方）；从顶栏打开需设施的配方提示“需要在{设施名}内查看和操作此配方。”。
    /// </summary>
    [Test]
    public void Q908_Q594_SessionTabWiring()
    {
        var craft = Web("Services", "GameSession.Craft.cs");
        Assert.That(craft, Does.Contain("private string CraftTabKey => CraftSite?.Key ?? CraftTabPrefs.TopKey;"));
        Assert.That(craft, Does.Contain("private string CraftDefaultTab => CraftSite != null ? GameEngine.WorkspaceDefaultTab : GameEngine.HandCraftDefaultTab;"));
        Assert.That(craft, Does.Contain("CraftTabs.Resolve(CraftTabKey, CraftTabList().Select(t => t.Id), CraftDefaultTab)"));
        var pick = Slice(craft, "public void PickCraftTab(string tab)", "public void SelectCraftRecipe(");
        Assert.That(pick, Does.Contain("CraftTabs.Set(CraftTabKey, tab);").And.Contain("_store.SetPref(\"craftTabs\", CraftTabs.Serialize())"));
        var init = Slice(craft, "private void InitCraftContext(string? recipe)", "public void OpenHandCraft(");
        Assert.That(init, Does.Not.Contain("CraftTabs.Set"), "恢复不写回");
        Assert.That(init, Does.Contain("Notice = GameEngine.NeedStationText(E.StationName(rr.Station));"));
        Assert.That(GameEngine.NeedStationText("工作台"), Is.EqualTo("需要在工作台内查看和操作此配方。"));
        Assert.That(GameEngine.WorkspaceGoneText, Is.EqualTo("该设施已不可用，请重新选择。"));
        Assert.That(Slice(craft, "private void ReconcileCraftAndSafehouse()", "if (Panel == \"safehouse\""), Does.Contain("E.WorkspaceHere(S, w.Key) == null").And.Contain("Notice = GameEngine.WorkspaceGoneText;"));
    }

    // ═════════════ Q795／Q796／Q909／Q936 独立设施卡 ═════════════

    /// <summary>B35-Q795-01：每座已建设施一张卡（无聚合卡），晾制架不重复；未建设施不出占位卡（Q594）。</summary>
    [Test]
    public void Q795_OneCardPerBuiltFacility()
    {
        var s = Camp();
        Assert.That(E.FacilityCardsHere(s), Is.Empty, "什么都没建：没有占位卡");
        Fac(s).Workshops.AddRange(new[] { "workbench", "forge", "drying_rack" });
        var cards = E.FacilityCardsHere(s);
        Assert.That(cards, Has.Count.EqualTo(2));
        Assert.That(cards.Select(c => (c.Kind, c.StationId, c.Name)), Is.EqualTo(new[] { ("station", "workbench", E.StationName("workbench")), ("station", "forge", E.StationName("forge")) }));
        Assert.That(cards.All(c => c.Stage == FacilityStage.Built && c.Workspace != null), Is.True);
        Fac(s).ShelterBuilt = true;
        var withShelter = E.FacilityCardsHere(s);
        Assert.That(withShelter, Has.Count.EqualTo(3));
        Assert.That(withShelter[0].Kind, Is.EqualTo("shelter"));
        Assert.That(withShelter.Where(c => c.Kind == "bed"), Is.Empty, "床垫未建不出卡");
    }

    /// <summary>B35-Q796-01：只有本人正在这座设施主动制作才“制作中”，别的设施仍“空闲”；做完回“空闲”。</summary>
    [Test]
    public void Q796_CraftingOnlyOnActualStation()
    {
        var s = Camp();
        Fac(s).Workshops.AddRange(new[] { "workbench", "sewing" });
        string State(string st) => E.FacilityCardsHere(s).Single(c => c.StationId == st).State;
        Assert.That(State("workbench"), Is.EqualTo($"{E.StationName("workbench")} · 空闲"));
        E.CreateItems(s, "scrap", 1, s.Ground);
        Start(s, ActionRequest.Craft("make_fastener"));
        Assert.That(State("workbench"), Is.EqualTo($"{E.StationName("workbench")} · 制作中"));
        Assert.That(State("sewing"), Is.EqualTo($"{E.StationName("sewing")} · 空闲"), "别的设施不跟着亮");
        Run(s);
        Assert.That(Count(s, "fastener"), Is.GreaterThan(0), "确实做完");
        Assert.That(State("workbench"), Is.EqualTo($"{E.StationName("workbench")} · 空闲"));
    }

    /// <summary>B35S-Q909-01／B35A1S-Q936-01：已开放公共设施状态只写“制作中／空闲”，悬停“公共设施。”；未开放保留真实原因。</summary>
    [Test]
    public void Q909_Q936_PublicCards()
    {
        Assert.That(GameEngine.PublicBadge, Is.EqualTo("公共"));
        Assert.That(GameEngine.PublicTooltip, Is.EqualTo("公共设施。"));
        var s = Registered("h03_workshop");
        var cards = E.FacilityCardsHere(s).Where(c => c.Kind == "public").ToList();
        Assert.That(cards, Has.Count.EqualTo(3));
        Assert.That(cards.Select(c => c.Name), Is.EqualTo(new[] { "营区工坊工作台", "营区工坊锻炉", "营区工坊军械台" }));
        var wb = cards[0];
        Assert.That(wb.Blocked, Is.Null);
        Assert.That(wb.State, Is.EqualTo("空闲"));
        Assert.That(cards[1].Blocked, Is.Not.Null, "锻炉未开放（缺 FA02 记录）");
        Assert.That(cards[1].State, Is.EqualTo(cards[1].Blocked), "未开放写原真实原因");
        E.CreateItems(s, "scrap", 1, s.Ground);
        Start(s, ActionRequest.Craft("make_fastener"));
        var busy = E.FacilityCardsHere(s).Where(c => c.Kind == "public").ToList();
        Assert.That(busy[0].State, Is.EqualTo("制作中"));
        Assert.That(busy[2].State, Is.EqualTo(cards[2].State), "同地别的公共设施不串");
        var fc = Web("Components", "FacilityCard.razor");
        Assert.That(fc, Does.Contain("GameEngine.PublicTooltip").And.Contain("GameEngine.PublicBadge"));
    }

    // ═════════════ Q904／Q933／Q934 拖拽候选 ═════════════

    /// <summary>B35X-Q904-01：当前区有工作台时材料拖拽候选列工作台配方并带所去工作区；B35X-Q904-02：没有对应已建设施不列。</summary>
    [Test]
    public void Q904_01_02_CandidatesNeedRealFacilityHere()
    {
        var s = Camp();
        Assert.That(E.DropRecipeCandidates(s, new[] { "scrap" }, false).Where(c => c.RecipeId == "make_fastener"), Is.Empty, "没有工作台不列");
        Fac(s).Workshops.Add("workbench");
        var c = E.DropRecipeCandidates(s, new[] { "scrap" }, false).Where(x => x.RecipeId == "make_fastener").ToList();
        Assert.That(c, Has.Count.EqualTo(1));
        Assert.That(c[0].Workspace, Is.Not.Null);
        Assert.That(c[0].Workspace!.StationId, Is.EqualTo("workbench"));
        Assert.That(c[0].Workspace!.Key, Is.EqualTo(E.WorkspaceForStationHere(s, "workbench")!.Key));
        E.DebugTeleport(s, "woods");
        Assert.That(E.DropRecipeCandidates(s, new[] { "scrap" }, false).Where(x => x.RecipeId == "make_fastener"), Is.Empty, "异地设施不列");
    }

    /// <summary>B35X-Q904-03：拖拽与列候选不预留、不扣物、不推进时间。</summary>
    [Test]
    public void Q904_03_CandidatesAreReadOnly()
    {
        var s = Camp();
        Fac(s).Workshops.Add("workbench");
        E.CreateItems(s, "scrap", 3, s.Ground);
        var minute = s.GameMinute;
        var list = E.DropRecipeCandidates(s, new[] { "scrap" }, false);
        Assert.That(list, Is.Not.Empty);
        Assert.That(s.GameMinute, Is.EqualTo(minute));
        Assert.That(Count(s, "scrap"), Is.EqualTo(3));
        Assert.That(s.Items.Where(i => i.ReservedBy != null), Is.Empty);
    }

    /// <summary>B35A1S-Q933-01：拖到营地地点卡列原建造＋当地已建设施相关配方，不额外列徒手；材料对材料原徒手范围保留（对照）。</summary>
    [Test]
    public void Q933_01_CampTargetCandidates()
    {
        var s = Camp();
        Fac(s).Workshops.Add("workbench");
        var camp = E.DropRecipeCandidates(s, new[] { "wood" }, true);
        Assert.That(camp, Is.Not.Empty);
        foreach (var c in camp)
        {
            var rc = R(c.RecipeId);
            Assert.That(c.Workspace != null ? E.IsWorkspaceRecipe(rc) : rc.IsBuild && rc.FacilityResult != null, Is.True, c.RecipeId);
        }
        Assert.That(camp.Select(c => c.RecipeId), Does.Contain("shelter"));
        Assert.That(camp.Where(c => c.Workspace == null && R(c.RecipeId).Group == "safehouse"), Is.Empty, "不含安全屋三项");
        Assert.That(camp.Select(c => c.RecipeId), Does.Not.Contain("wild_fixed_fire"));
        var mat = E.DropRecipeCandidates(s, new[] { "wood" }, false);
        Assert.That(mat.Where(c => c.Workspace == null && E.HandCraftTabOf(R(c.RecipeId)) == "hand"), Is.Not.Empty, "材料对材料仍列徒手配方");
        Assert.That(camp.Where(c => c.Workspace == null && E.HandCraftTabOf(R(c.RecipeId)) == "hand"), Is.Empty, "地点卡路径不额外加徒手");
    }

    /// <summary>B35A1S-Q933-02：通用候选不列锅食谱／晾制；未开放公共设施的已掌握配方可列（进入只浏览，开工按原原因拒绝）。</summary>
    [Test]
    public void Q933_02_NoPotOrDryingAndUnopenedPublicBrowsable()
    {
        var s = Camp();
        Fac(s).Workshops.Add("drying_rack");
        foreach (var rc in Cfg.Recipes.Where(r => r.IsPot || r.IsDrying))
            foreach (var input in rc.Inputs.Keys)
                Assert.That(E.DropRecipeCandidates(s, new[] { input }, false).Select(c => c.RecipeId), Does.Not.Contain(rc.Id), rc.Id);
        Assert.That(Cfg.Recipes.Count(r => r.IsPot || r.IsDrying), Is.GreaterThan(0));

        var h = Registered("h03_workshop");
        var forge = R("make_steel");
        var c = E.DropRecipeCandidates(h, new[] { "iron_ore" }, false).Where(x => x.RecipeId == forge.Id).ToList();
        Assert.That(c, Has.Count.EqualTo(1));
        Assert.That(c[0].Workspace!.PublicId, Is.EqualTo("public:h03_workshop:forge"));
        Assert.That(E.FacilityCardsHere(h).Single(x => x.Workspace?.Key == c[0].Workspace!.Key).Blocked, Is.Not.Null, "这座锻炉未开放");
        Assert.That(E.CanStart(h, ActionRequest.Craft(forge.Id)), Is.Not.Null, "只浏览不能开工");
    }

    /// <summary>B35A1S-Q934-01：设施候选小字前加真实设施名；进入后返回／ESC 先回保留候选（重验）再退出，不投料不扣物。</summary>
    [Test]
    public void Q934_CandidatePrefixAndReturn()
    {
        var co = Web("Components", "CraftOverlay.razor");
        Assert.That(co, Does.Contain("var prefix = ws != null ? $\"{ws.Name} · \" : \"\";"));
        Assert.That(co, Does.Contain("<span>@r.Name<small>@prefix@small</small></span>"));
        Assert.That(co, Does.Contain("if (cand != null) { G.PickCandidate(cand); return; }"));
        var craft = Web("Services", "GameSession.Craft.cs");
        var pick = Slice(craft, "public void PickCandidate(RecipeCandidate c)", "private void ReturnToCandidates()");
        Assert.That(pick, Does.Contain("_candidateReturn = new CandidateReturn(CraftCandidates, CraftRecipe, CraftPot);"));
        Assert.That(pick, Does.Contain("E.WorkspaceHere(S, c.Workspace.Key) is not { } real"), "进入前按稳定身份重验");
        var back = Slice(craft, "private void ReturnToCandidates()", "private bool CandidateStillValid(");
        Assert.That(back, Does.Contain("back.Candidates.Where(CandidateStillValid)"));
        Assert.That(back, Does.Contain("if (keep.Count == 0) { CraftCandidates = null; OpenPanel(null); return; }"));
        Assert.That(back, Does.Not.Contain("EngineDo").And.Not.Contain("StartAction"), "返回不投料、不开工");
        var valid = Slice(craft, "private bool CandidateStillValid(", "public void CandidatesShowAll()");
        Assert.That(valid, Does.Contain("E.RecipeKnown(S, rc) && E.RecipeVisible(S, rc)").And.Contain("E.WorkspaceHere(S, c.Workspace.Key) != null"));
        var fac = Web("Services", "GameSession.FacMenu.cs");
        Assert.That(Slice(fac, "public void PanelEscape()", "OpenPanel(null);"), Does.Contain("if (CraftFromCandidates && State != null && _chain.Count == 0) { ReturnToCandidates(); return; }"));
        Assert.That(co, Does.Contain("G.CraftFromCandidates && !G.PanelHasUnder ? \"返回\" : G.PanelCloseLabel"));
    }

    // ═════════════ Q907 工作区设施块 ═════════════

    /// <summary>B35S-Q907-01：顶栏手工制作无设施／燃料块；进入真实设施工作区后左列常驻本座状态（与卡面同一状态行）；Q904 入口保留。</summary>
    [Test]
    public void Q907_SiteBlockOnlyInWorkspace()
    {
        var co = Web("Components", "CraftOverlay.razor");
        var tabs = Slice(co, "<div class=\"craft-tabs\"", "@foreach (var (tid, tname) in tabs)");
        Assert.That(tabs, Does.Contain("@if (site != null)"), "只在工作区出设施块");
        Assert.That(tabs, Does.Contain("e.FacilityCardsHere(s).FirstOrDefault(c => c.Workspace?.Key == site.Key)"));
        Assert.That(co, Does.Not.Contain("fire-box"), "顶栏不放火堆状态块");
        Assert.That(co, Does.Contain("G.CandidatesShowAll"), "Q904 候选入口仍在");
    }

    // ═════════════ Q930 工坊服务 ═════════════

    /// <summary>B35A1-Q930-01／03：两处工坊服务首句“从设施卡进入。”，原开放清单与未开放真实原因仍在。</summary>
    [Test]
    public void Q930_01_03_WorkshopServiceHead()
    {
        var c = Asset("20261117", "b35a1-primary-q930-q931.json");
        Assert.That((string)c["workshop"]!["previewHead"]!, Is.EqualTo("从设施卡进入。"));
        Assert.That(GameEngine.WorkshopAccessHead, Is.EqualTo("从设施卡进入。"));
        var s = Registered("h03_workshop");
        var v = E.CityServicePreview(s, "hub_workshop_access")!;
        Assert.That(v.EffectText, Is.EqualTo("从设施卡进入。"));
        Assert.That(v.Lines, Has.Some.StartWith("已开放 "));
        Assert.That(v.Lines, Has.Some.Contain("未开放"), "锻炉／军械台未开放仍列真实原因");
        var r = Registered(Cfg.City.Services.Single(x => x.Id == "academy_workshop_access").LocationId);
        Assert.That(E.CityServicePreview(r, "academy_workshop_access")!.EffectText, Is.EqualTo("从设施卡进入。"));
    }

    /// <summary>B35A1-Q930-02：办完不自动打开任何制作页、不代选设施（OpensCraft 撤、会话不再按服务开制作）。</summary>
    [Test]
    public void Q930_02_NoAutoOpenCraft()
    {
        Assert.That(typeof(CityServiceView).GetProperty("OpensCraft"), Is.Null);
        var session = Web("Services", "GameSession.cs");
        var svc = Slice(session, "public async Task<ActionResult> StartCityService(", "    public ");
        Assert.That(svc, Does.Not.Contain("OpenPanel(\"craft\"").And.Not.Contain("OpenHandCraft").And.Not.Contain("OpenWorkspace"));
        Assert.That(Web("Components", "CityServiceOverlay.razor"), Does.Contain("startLabel = v.StartLabel ?? $\"{v.Title}（{v.TimeText}）\""), "Q968 暂用 A：通用按钮格式");
    }

    /// <summary>B35A1-Q930-04：三座营区公共设施各由其真实卡进入，配方仅本座。</summary>
    [Test]
    public void Q930_04_PublicWorkspacesListOwnRecipes()
    {
        var s = Registered("h03_workshop");
        var ws = E.WorkspacesHere(s);
        Assert.That(ws, Has.Count.EqualTo(3));
        Assert.That(ws.Select(w => w.StationId), Is.EqualTo(new[] { "workbench", "forge", "armory" }));
        foreach (var w in ws)
        {
            var list = E.WorkspaceRecipes(s, w.StationId, GameEngine.TabAll);
            Assert.That(list, Is.Not.Empty, w.Name);
            Assert.That(list.All(r => r.Station == w.StationId), Is.True, w.Name);
        }
    }

    // ═════════════ Q931 安全屋本地入口 ═════════════

    private GameState SafehouseAtCamp()
    {
        var s = Camp();
        E.DebugSetProgress(s, "camp", 100);
        s.Progress.Flags.Add("hub_registered");
        foreach (var (id, n) in new[] { ("wood", 8), ("fiber", 6), ("cloth", 2), ("stone", 4) }) E.CreateItems(s, id, n, s.Ground);
        Start(s, ActionRequest.Craft("safehouse_base", null, null, 1));
        Run(s);
        Assert.That(E.SafehouseBuilt(s, "camp"), Is.True);
        return s;
    }

    /// <summary>B35A1-Q931-02／03：在已建安全屋所在地屋外、屋内可开面板；人在别处即使别处有屋也不能开。</summary>
    [Test]
    public void Q931_02_03_PanelOnlyAtLocalBuiltSafehouse()
    {
        var none = Camp();
        Assert.That(E.SafehousePanelBlocked(none), Is.Not.Null, "没有屋");
        var s = SafehouseAtCamp();
        Assert.That(E.SafehousePanelBlocked(s), Is.Null, "屋外");
        s.CurrentZone = GameState.SafehouseZone;
        Assert.That(E.SafehousePanelBlocked(s), Is.Null, "屋内");
        s.CurrentZone = GameState.ExteriorZone;
        E.DebugTeleport(s, "woods");
        Assert.That(E.SafehousePanelBlocked(s), Is.Not.Null, "别处有屋也不能远程开");
    }

    /// <summary>B35A1-Q931-01／02／05：地图无返程按钮、卡上无“返程 ▸”；打开与恢复路径都校验本地，离开即收面板。</summary>
    [Test]
    public void Q931_01_05_NoRemoteEntry()
    {
        var map = Code(Web("Components", "MapOverlay.razor"));
        Assert.That(map, Does.Not.Contain("SafehouseReturn").And.Not.Contain(">安全屋返程"));
        var card = Code(Web("Components", "SafehouseCard.razor"));
        Assert.That(card, Does.Not.Contain("返程 ▸"));
        Assert.That(card, Does.Contain("if (G.E.SafehousePanelBlocked(s) == null) { G.OpenPanel(\"safehouse\"); return; }"));
        var session = Web("Services", "GameSession.cs");
        Assert.That(session, Does.Contain("E.SafehousePanelBlocked(S) != null"), "SwitchPanel 打开前校验");
        var stack = Web("Services", "GameSession.PanelStack.cs");
        Assert.That(Slice(stack, "case \"safehouse\":", "case \"formation\":"), Does.Contain("return E.SafehousePanelBlocked(S) != null ? \"\" : null;"), "恢复路径不绕过");
        var craft = Web("Services", "GameSession.Craft.cs");
        Assert.That(Slice(craft, "private void ReconcileCraftAndSafehouse()", "\n}"), Does.Contain("if (Panel == \"safehouse\" && E.SafehousePanelBlocked(S) != null)"), "离开本地点即收面板");
    }

    /// <summary>B35A1-Q931-04／B35S-Q910-01／B35A1S-Q937-01：面板无路线段落；外地柜子记录不展示；按钮悬停短句；删副标与无屋空态。</summary>
    [Test]
    public void Q931_04_Q910_Q937_01_SafehouseCopy()
    {
        var c = Asset("20261117", "b35a1-secondary-q932-q937-construction.json");
        var sh = (JObject)c["safehouse"]!;
        var ov = Code(Web("Components", "SafehouseOverlay.razor"));
        Assert.That(ov, Does.Not.Contain("收藏、改名与返程路线").And.Not.Contain("还没有建成的安全屋"));
        Assert.That(ov, Does.Not.Contain("RouteText").And.Not.Contain("逐段"));
        Assert.That(ov, Does.Contain("@if (l == here && !inside)"), "柜子上次记录只在当地屋外");
        var card = Code(Web("Components", "SafehouseCard.razor"));
        Assert.That(card, Does.Contain("\"柜子存取、改名与收藏\"").And.Contain("\"收藏与改名（柜子只能在屋内存取）\""));
        Assert.That(sh.ToString(), Does.Contain("柜子存取、改名与收藏").And.Contain("收藏与改名（柜子只能在屋内存取）"));
        Assert.That(card, Does.Not.Contain("返程路线"));
    }

    // ═════════════ Q932 处理说明迁火堆菜单 ═════════════

    /// <summary>B35A1S-Q932-01：火堆／柴炉主层有默认收起的只读“处理说明”四类；手工制作无“自动加工”分类、无“查看火堆”及其提示。</summary>
    [Test]
    public void Q932_ProcessGuideInFireMenu()
    {
        // 四类（烧水、直烤、锅煮、煅烧）在配置里都有，折叠区逐条列全部原“自动加工”条目
        Assert.That(Cfg.Processes.Count(p => p.IsPotBatch), Is.GreaterThan(0));
        Assert.That(Cfg.Processes.Count(p => p.IsSlotBatch), Is.GreaterThan(0));
        Assert.That(Cfg.Processes.Count(p => p.InPot && !p.IsPotBatch), Is.GreaterThan(0));
        Assert.That(Cfg.Processes.Count(p => !p.InPot && !p.IsPotBatch && !p.IsSlotBatch), Is.GreaterThan(0));
        var fm = Web("Components", "FireMenu.razor");
        var guide = Slice(fm, "<details class=\"fm-guide\"", "</details>");
        Assert.That(guide, Does.Contain("<summary>处理说明</summary>").And.Contain("@foreach (var p in e.Config.Processes)"));
        Assert.That(guide, Does.Not.Contain(" open").And.Not.Contain("OpenFireMenu").And.Not.Contain("EngineDo"), "默认收起、只读");
        Assert.That(fm.IndexOf("<details class=\"fm-guide\"", StringComparison.Ordinal), Is.GreaterThan(fm.IndexOf("@if (level == 0)", StringComparison.Ordinal)), "只在主层");
        var co = Web("Components", "CraftOverlay.razor");
        Assert.That(co, Does.Not.Contain("\"自动加工\"").And.Not.Contain("查看火堆 ▸").And.Not.Contain("关闭手工制作，打开火堆加工菜单"));
        var pg = Web("Services", "ProcessGuide.cs");
        Assert.That(pg, Does.Contain("public static string Title(GameEngine e, ProcessConfig p)").And.Contain("p.HeatMinutes"));
    }

    /// <summary>开发入口「B35a1 设施试用组」只造起点：营地记三座工坊、地面放材料，不开工不推进；不能建工坊的地点不动（对照）。</summary>
    [Test]
    public void DevKit_BuildsStartingPointOnly()
    {
        var s = Camp();
        var minute = s.GameMinute;
        E.DebugCraftScopeKit(s);
        Assert.That(Fac(s).Workshops, Is.EquivalentTo(new[] { "workbench", "forge", "sewing" }));
        Assert.That(Count(s, "scrap", s.Ground), Is.EqualTo(3));
        Assert.That(s.Main, Is.Null);
        Assert.That(s.GameMinute, Is.EqualTo(minute));
        Assert.That(E.FacilityCardsHere(s), Has.Count.EqualTo(3));
        var w = Camp();
        E.DebugTeleport(w, "woods");
        Assert.That(E.DebugCraftScopeKit(w), Does.Contain("不能建工坊"));
        Assert.That(Count(w, "scrap", w.Ground), Is.EqualTo(0));
    }

    // ═════════════ Q805／Q917／Q937-02 命名与系统说明 ═════════════

    /// <summary>B35-Q805-01：“营地建造”统一改“设施建造”（帮助、晾制、工坊提示、任务提示）；删旧虚线未建卡说明；“徒手制造”“手工制作”不误改。</summary>
    [Test]
    public void Q805_FacilityBuildNaming()
    {
        var help = Web("Services", "HelpCatalog.cs");
        Assert.That(help, Does.Not.Contain("营地建造").And.Not.Contain("虚线框的“未建造”卡"));
        Assert.That(help, Does.Contain("手工制作“设施建造”里的“建野外火堆”").And.Contain("打开「手工制作」的「设施建造」"));
        Assert.That(Web("Components", "DryingOverlay.razor"), Does.Contain("用“设施建造”建").And.Not.Contain("营地建造"));
        Assert.That(Src("Engine", "GameEngine.Workshop.cs"), Does.Contain("（在“设施建造”里建）"));
        var pro13 = Cfg.Prologue.Tasks.Single(t => t.Id == "PRO13");
        Assert.That(pro13.Hint, Is.EqualTo("在设施建造中查看遮蔽棚的材料和耗时；建成后改善恢复条件，仍要留意危险。"));
        Assert.That(GameEngine.HandCraftTabs.Single(t => t.Id == "hand").Name, Is.EqualTo("徒手制造"));
    }

    /// <summary>B35S-Q917-01／B35A1S-Q937-02：系统说明入口段逐字（窗口名“手工制作”），Q600 秘密配方说明原段紧随其后。</summary>
    [Test]
    public void Q917_Q937_02_HelpEntryAndSecretParagraph()
    {
        var naming = (string)Asset("20261117", "b35-secondary-ui-q907-q917.json")["naming"]!["entryParagraph"]!;
        var entry = naming.Substring(naming.IndexOf('“') + 1, naming.LastIndexOf('”') - naming.IndexOf('“') - 1);
        Assert.That(entry, Does.StartWith("不需要固定设施的物品在‘手工制作’中的‘徒手制造’里制作。"));
        var s08 = File.ReadAllText(Path.Combine(Root(), "doc", "策划案", "系统策划总册 v1.0", "01 基础系统", "S08 手工制作系统.md"));
        Assert.That(s08, Does.Contain(GameEngine.SecretRecipeHelp), "Q600 系统说明原段与 S08 同源");
        var help = Web("Services", "HelpCatalog.cs");
        var craft = Slice(help, "Id = \"craft\", Title = \"手工制作\"", "new HelpEntry");
        var at = craft.IndexOf("\"" + entry + "\",", StringComparison.Ordinal);
        Assert.That(at, Is.GreaterThan(0), "入口段逐字");
        var next = craft.IndexOf("GameEngine.SecretRecipeHelp,", StringComparison.Ordinal);
        Assert.That(next, Is.GreaterThan(at), "秘密配方段在入口段之后");
        Assert.That(craft.Substring(at, next - at).Count(ch => ch == '"'), Is.EqualTo(2), "两段之间没有别的段落");
    }
}
