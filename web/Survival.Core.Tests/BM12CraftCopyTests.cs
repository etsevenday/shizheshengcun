using Newtonsoft.Json.Linq;
using Survival.Core.Config;
using Survival.Core.Engine;
using Survival.Core.State;
using Survival.Web.Services;

namespace Survival.Core.Tests;

/// <summary>
/// 开发记录 BM12「次要问题合并」：制作服务与公共设施（02 Q968—Q972，R305，合同 assets/20261118/b35a1-q968-q972-craft-services-ui.json，验收 B35R-Q968-01—Q972-02）、
/// 火堆处理说明与公共工作台文字（02 Q996—Q998，R312，合同 assets/20261118/b35a1-q996-q998-fire-help-public-station-copy.json，验收 B35A1-Q996-01—Q998-01）。
/// 期望句读合同或写字面量（注明 Q 号）；内核规则直接测，网页壳接线按源码核对（同 B35a1CraftScopeTests）。
/// </summary>
public class BM12CraftCopyTests : TestBase
{
    private static string Root()
    {
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "web", "Survival.sln"))) dir = dir.Parent;
        Assert.That(dir, Is.Not.Null, "找到仓库根目录");
        return dir!.FullName;
    }

    private static JObject Asset(string file) =>
        JObject.Parse(File.ReadAllText(Path.Combine(Root(), "doc", "策划案", "系统策划总册 v1.0", "assets", "20261118", file)));

    private static JObject Services() => Asset("b35a1-q968-q972-craft-services-ui.json");
    private static JObject FireCopy() => Asset("b35a1-q996-q998-fire-help-public-station-copy.json");

    private static string Web(params string[] parts) => File.ReadAllText(Path.Combine(new[] { Root(), "web", "Survival.Web" }.Concat(parts).ToArray()));

    /// <summary>去掉 Razor 注释（@* … *@）与 C# 单行注释，只核实际渲染的标记与代码。</summary>
    private static string Code(string src) =>
        System.Text.RegularExpressions.Regex.Replace(System.Text.RegularExpressions.Regex.Replace(src, @"@\*.*?\*@", "", System.Text.RegularExpressions.RegexOptions.Singleline), @"(?m)^\s*//.*$", "");

    private static string Slice(string text, string from, string to)
    {
        var i = text.IndexOf(from, StringComparison.Ordinal);
        Assert.That(i, Is.GreaterThanOrEqualTo(0), from);
        var j = text.IndexOf(to, i + from.Length, StringComparison.Ordinal);
        Assert.That(j, Is.GreaterThan(i), to);
        return text.Substring(i, j - i);
    }

    /// <summary>已登记、在指定城市地点的新局（同 B35a1CraftScopeTests.Registered）。</summary>
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

    private ProcessConfig P(string id) => Cfg.Processes.Single(p => p.Id == id);

    // ═════════════ Q968 工坊服务提交按钮 ═════════════

    /// <summary>
    /// B35R-Q968-01：两项工坊服务的提交按钮“使用营区工坊（0分钟）”“使用试制工坊（0分钟）”（合同 workshopServices.hubButton／academyButton），
    /// 按钮按通用格式“{服务名}（{工时}）”拼（不另设 StartLabel），无“打开制作”旧名；预览首句“从设施卡进入。”保持。
    /// </summary>
    [Test]
    public void Q968_WorkshopServiceButtons()
    {
        var c = (JObject)Services()["workshopServices"]!;
        var hub = Registered("h03_workshop");
        var hv = E.CityServicePreview(hub, "hub_workshop_access")!;
        Assert.That(hv.StartLabel, Is.Null, "不另设按钮名，走通用格式");
        Assert.That($"{hv.Title}（{hv.TimeText}）", Is.EqualTo((string)c["hubButton"]!));
        Assert.That(hv.EffectText, Is.EqualTo((string)c["previewFirstLine"]!));
        var aloc = Cfg.City.Services.Single(x => x.Id == "academy_workshop_access").LocationId;
        var av = E.CityServicePreview(Registered(aloc), "academy_workshop_access")!;
        Assert.That(av.StartLabel, Is.Null);
        Assert.That($"{av.Title}（{av.TimeText}）", Is.EqualTo((string)c["academyButton"]!));
        var ov = Code(Web("Components", "CityServiceOverlay.razor"));
        Assert.That(ov, Does.Contain("var startLabel = v.StartLabel ?? $\"{v.Title}（{v.TimeText}）\";"));
        Assert.That(ov, Does.Contain("Name=\"@startLabel\"").And.Contain("@startLabel"));
        Assert.That(ov, Does.Not.Contain("打开制作"), "Q968：无打开制作旧名");
    }

    // ═════════════ Q970 候选表头“显示全部” ═════════════

    /// <summary>
    /// B35R-Q970-01／02：候选表头保留“显示全部”，点了退出候选（清候选与返回链、清工作区）进顶栏手工制作“全部配方”；
    /// 选中配方不在顶栏范围（设施配方）时换成顶栏“全部配方”第一条；只导航，不调用开工。
    /// </summary>
    [Test]
    public void Q970_ShowAllGoesToTopLevelAllRecipes()
    {
        Assert.That((string)Services()["showAll"]!["label"]!, Is.EqualTo("显示全部"));
        Assert.That((bool)Services()["showAll"]!["includeFacilityRecipes"]!, Is.False);
        var co = Code(Web("Components", "CraftOverlay.razor"));
        Assert.That(co, Does.Contain("<button class=\"linkish\" data-candidates-all @onclick=\"G.CandidatesShowAll\">显示全部</button>"));
        var craft = Code(Web("Services", "GameSession.Craft.cs"));
        var show = Slice(craft, "public void CandidatesShowAll()", "Notify();");
        foreach (var must in new[] { "CraftCandidates = null;", "_candidateReturn = null;", "CraftSite = null;", "CraftTab = GameEngine.TabAll;", "!InCraftScope(rc)", "CraftTabRecipes(GameEngine.TabAll)" })
            Assert.That(show, Does.Contain(must), must);
        foreach (var no in new[] { "EngineDo", "StartCraft", "E.Start(" })
            Assert.That(show, Does.Not.Contain(no), "只导航不开工：" + no);
    }

    /// <summary>B35R-Q970-01：落点顶栏“全部配方”（会话 CraftSite 已清，CraftTabRecipes 走 HandCraftRecipes）不含设施配方（砍刀只在工作台工作区）。</summary>
    [Test]
    public void Q970_TopLevelAllExcludesFacilityRecipes()
    {
        var s = Registered("h03_workshop");
        var top = E.HandCraftRecipes(s, GameEngine.TabAll);
        Assert.That(top, Is.Not.Empty);
        Assert.That(top.Select(r => r.Id), Does.Not.Contain("make_machete"), "砍刀是工作台配方，不在顶栏全部");
        Assert.That(top.Where(r => E.IsWorkspaceRecipe(r)), Is.Empty);
        var wb = E.WorkspacesHere(s).Single(w => w.Public && w.StationId == "workbench");
        Assert.That(E.WorkspaceRecipes(s, wb.StationId, GameEngine.TabAll).Select(r => r.Id), Does.Contain("make_machete"), "对照：在工作台工作区里列");
        var craft = Code(Web("Services", "GameSession.Craft.cs"));
        Assert.That(craft, Does.Contain("CraftSite is { } w ? E.WorkspaceRecipes(S, w.StationId, tab) : E.HandCraftRecipes(S, tab)"));
    }

    // ═════════════ Q971 安全屋副标 ═════════════

    /// <summary>B35R-Q971-01：安全屋面板屋内“{名}屋内 · 柜子存取0分钟”与屋外“收藏与改名 · 柜子只能在屋内存取”两行副标都不出现；按钮悬停原句保留（对照）。</summary>
    [Test]
    public void Q971_SafehouseSubtitlesRemoved()
    {
        var sh = (JObject)Services()["shelterSubtitle"]!;
        Assert.That(((bool)sh["removeIndoor"]!, (bool)sh["removeOutdoor"]!), Is.EqualTo((true, true)));
        var ov = Code(Web("Components", "SafehouseOverlay.razor"));
        Assert.That(ov, Does.Not.Contain("屋内 · 柜子存取").And.Not.Contain("柜子存取0分钟"), (string)sh["indoorRemoved"]!);
        Assert.That(ov, Does.Not.Contain("收藏与改名 · 柜子只能在屋内存取"), (string)sh["outdoorRemoved"]!);
        var card = Code(Web("Components", "SafehouseCard.razor"));
        Assert.That(card, Does.Contain("\"收藏与改名（柜子只能在屋内存取）\""), "原按钮悬停保持（Q937 B）");
    }

    // ═════════════ Q972 暂停时设施卡状态 ═════════════

    /// <summary>
    /// B35R-Q972-01：在营区工坊工作台主动制作，途中水分越过提醒阈值自动暂停（主行动未退出）——这座卡仍写“制作中”；
    /// 同地别的公共设施不串；取消（退出）后回“空闲”。开工前“空闲”（对照）。
    /// </summary>
    [Test]
    public void Q972_PausedByBodyAlert_StillCrafting_CancelIdle()
    {
        var s = Registered("h03_workshop");
        string Wb() => E.FacilityCardsHere(s).Single(c => c.Kind == "public" && c.StationId == "workbench").State;
        string Armory() => E.FacilityCardsHere(s).Single(c => c.Kind == "public" && c.StationId == "armory").State;
        Assert.That(Wb(), Is.EqualTo("空闲"), "开工前（对照）");
        E.CreateItems(s, "scrap", 1, s.Ground);
        SetStats(s, 100, 80, 20.05, 90, 90, 60);   // 起点：水分贴近提醒阈值
        Start(s, ActionRequest.Craft("make_fastener"));
        Assert.That(Run(s, autoResume: false), Is.EqualTo(SegmentStatus.Paused));
        Assert.That((s.Main!.Status, s.Main.PauseReason), Is.EqualTo((ActionStatus.Paused, PauseReason.StatAlert)), "前提：身体提醒自动暂停，主行动未退出");
        Assert.That(Wb(), Is.EqualTo("制作中"), "Q972 A：暂停未退出仍算制作中");
        Assert.That(Armory(), Is.Not.EqualTo("制作中"), "不串同地另一座");
        Assert.That(E.Cancel(s).Ok, Is.True);
        Assert.That(s.Main, Is.Null);
        Assert.That(Wb(), Is.EqualTo("空闲"), "退出后按真状态空闲");
    }

    // ═════════════ Q996 火堆处理说明：烘制、炖煮 ═════════════

    /// <summary>
    /// B35A1-Q996-01：烘制（远行硬饼）说明句从“谷物放进有净水的锅”起，第 2 步“2. 把谷物×2拖到锅的食材区”；
    /// 净水 500 毫升、受热 60 分钟与其余步骤照旧（02 Q996 答复“硬饼仍用……净水500毫升，受热60分钟”）。
    /// </summary>
    [Test]
    public void Q996_Hardtack_LeadAndStep2()
    {
        var c = (JObject)FireCopy()["heatHelp"]!["hardtack"]!;
        var p = P("pot_hardtack");
        var hint = ProcessGuide.Hint(E, p);
        Assert.That(hint, Does.StartWith((string)c["sentenceLead"]! + "、锅放进燃烧的火堆"));
        Assert.That(hint, Is.EqualTo("谷物放进有净水的锅、锅放进燃烧的火堆，每份60分钟煮成远行硬饼，完成时扣500毫升；空锅只存放不加工。"), "Q996 A：只补句首食材名，其余照原锅煮模板");
        var steps = ProcessGuide.Steps(E, p);
        Assert.That(steps, Has.Count.EqualTo(5));
        Assert.That(steps[1].note, Is.EqualTo((string)c["secondStep"]!));
        Assert.That((steps[1].icon, steps[1].name), Is.EqualTo(("grain", "谷物")), "第2步图标与名称写出食材（不再是空图标）");
        Assert.That(steps[0].note, Is.EqualTo("1. 锅里装净水，每份食材预留500毫升"), "其余步骤照旧");
        Assert.That(steps[2].note, Is.EqualTo("3. 锅放进火堆的空加工位"));
        Assert.That(steps[3].note, Is.EqualTo("4. 受热60分钟，完成时扣水"));
    }

    /// <summary>B35A1-Q996-01：炖煮（谷肉炖食）说明句从“谷物和生肉放进有净水的锅”起，第 2 步“2. 把谷物×1、生肉×1拖到锅的食材区”；净水 1000 毫升、受热 45 分钟照旧。</summary>
    [Test]
    public void Q996_Stew_LeadAndStep2()
    {
        var c = (JObject)FireCopy()["heatHelp"]!["stew"]!;
        var p = P("pot_stew");
        var hint = ProcessGuide.Hint(E, p);
        Assert.That(hint, Does.StartWith((string)c["sentenceLead"]! + "、锅放进燃烧的火堆"));
        Assert.That(hint, Is.EqualTo("谷物和生肉放进有净水的锅、锅放进燃烧的火堆，每份45分钟煮成谷肉炖食，完成时扣1000毫升；空锅只存放不加工。"));
        var steps = ProcessGuide.Steps(E, p);
        Assert.That(steps, Has.Count.EqualTo(5));
        Assert.That(steps[1].note, Is.EqualTo((string)c["secondStep"]!));
        Assert.That(steps[1].icon, Is.EqualTo("grain"));
        Assert.That(steps[3].note, Is.EqualTo("4. 受热45分钟，完成时扣水"));
    }

    /// <summary>Q996（对照）：单食材锅煮（水煮块根）与直烤原句不变，只有两条锅食谱补食材名。</summary>
    [Test]
    public void Q996_OtherProcessesUnchanged()
    {
        var root = P("boil_root");
        Assert.That(ProcessGuide.Hint(E, root), Does.StartWith("生块根放进有净水的锅、锅放进燃烧的火堆"));
        Assert.That(ProcessGuide.Steps(E, root)[1].note, Is.EqualTo("2. 拖到锅的食材区（最多2份）"));
        Assert.That(ProcessGuide.Hint(E, P("roast_root")), Does.StartWith("生块根直接放进燃烧火堆的空加工位"));
        var pots = Cfg.Processes.Where(p => p.IsPotRecipe).ToList();
        Assert.That(pots.Select(p => p.Id), Is.EquivalentTo(new[] { "pot_hardtack", "pot_stew" }));
        foreach (var p in pots) Assert.That(ProcessGuide.Hint(E, p), Does.Not.StartWith("放进"), p.Id + "：句首有主语");
    }

    // ═════════════ Q997 公共工作台配方详情抬头 ═════════════

    /// <summary>B35A1-Q997-01：营区工坊工作台工作区里选砍刀，抬头“营区工坊工作台 · 砍刀”（合同 publicWorktableDetail）；顶栏手工制作沿原短名（对照）。</summary>
    [Test]
    public void Q997_PublicWorkbenchDetailUsesCardName()
    {
        Assert.That((string)FireCopy()["publicWorktableDetail"]!, Is.EqualTo("{实际设施卡名} · {配方名}"));
        var s = Registered("h03_workshop");
        var wb = E.WorkspacesHere(s).Single(w => w.Public && w.StationId == "workbench");
        var machete = Cfg.Recipes.Single(r => r.Id == "make_machete");
        Assert.That(E.RecipeDetailEyebrow(machete, wb), Is.EqualTo("营区工坊工作台 · 砍刀"));
        Assert.That(E.RecipeDetailEyebrow(machete, null), Is.EqualTo("工作台 · 砍刀"), "不在设施工作区时沿原写法（对照）");
        var co = Code(Web("Components", "CraftOverlay.razor"));
        Assert.That(co, Does.Contain("@e.RecipeDetailEyebrow(rc, site) ⓘ"));
        Assert.That(co, Does.Not.Contain("e.StationName(rc.Station)) · @rc.Name"), "旧短名拼法撤掉");
    }

    // ═════════════ Q998 未开放公共卡 ═════════════

    /// <summary>
    /// B35A1-Q998-01：营区工坊锻炉未开放——卡面状态行只写“未开放”；悬停“营区工坊锻炉未开放。需要：在教会地下圣所领取圣所旧路档册”（02 Q998 答复例句，冒号不重复），
    /// 右栏说明取同一悬停句；已开放的工作台仍写“空闲”（对照）。
    /// </summary>
    [Test]
    public void Q998_LockedPublicCard_StateAndTooltip()
    {
        var lc = (JObject)FireCopy()["lockedPublicFacility"]!;
        Assert.That((string)lc["card"]!, Is.EqualTo("未开放"));
        var s = Registered("h03_workshop");
        var cards = E.FacilityCardsHere(s).Where(c => c.Kind == "public").ToList();
        Assert.That(cards, Has.Count.EqualTo(3));
        var forge = cards.Single(c => c.StationId == "forge");
        Assert.That(forge.Blocked, Is.EqualTo("需要：在教会地下圣所领取圣所旧路档册"), "前提：锻炉未开放的真实原因");
        Assert.That(forge.State, Is.EqualTo((string)lc["card"]!));
        Assert.That(GameEngine.LockedPublicTooltip(forge.Name, forge.Blocked!), Is.EqualTo("营区工坊锻炉未开放。需要：在教会地下圣所领取圣所旧路档册"));
        Assert.That(cards.Single(c => c.StationId == "workbench").State, Is.EqualTo("空闲"), "已开放（对照）");
        var fc = Code(Web("Components", "FacilityCard.razor"));
        Assert.That(fc, Does.Contain("tip = blocked ? GameEngine.LockedPublicTooltip(v.Name, v.Blocked!) : GameEngine.PublicTooltip;"));
        Assert.That(fc, Does.Contain("sub = v.State;"));
        Assert.That(fc, Does.Not.Contain("未开放：{v.Blocked}"), "旧“未开放：需要：”双冒号撤掉");
        var ov = Code(Web("Components", "ObjectCard.razor"));
        Assert.That(ov, Does.Contain("Desc = Tooltip ?? SubTitle,"), "右栏说明取悬停句（完整原因）");
    }
}
