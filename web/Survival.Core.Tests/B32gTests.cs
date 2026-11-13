using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v1.0 B32g：答复 Q400—Q402。
/// 规则出处：S08「Q400 · B32e 燃料明细的批数用词」（验收 Q400-A01）；《重墨 V2 开工裁定与界面逐字稿》§1.5「Q401–Q402 · B32f 后续裁定」
/// （验收 Q401-A01、Q402-A01，Q401 取代 Q394 保留原盘的做法）。内核文字直接测；网页壳接线（Q400 燃料行、Q401 收盘与焦点）以源码检查（同 B31b—B32f）。
/// </summary>
public class B32gTests : TestBase
{
    private const string Kiln = "ex_ceramic_shell";
    private const string Camp = "camp";

    private static ActionRequest Craft(string id, int n = 1) => ActionRequest.Craft(id, null, null, n);

    /// <summary>营地外部区建好陶窑，清掉陶土与木炭，制造 3 级（同 B32e）。</summary>
    private GameState KilnCamp()
    {
        var s = New();
        s.Skills["crafting"].Level = 3;
        s.Progress.Flags.Add("h03_ceramic_notice_read");
        foreach (var (id, n) in new[] { ("stone", 8), ("clay", 6), ("iron_plate", 2) }) E.CreateItems(s, id, n, s.Ground);
        Start(s, Craft("build_kiln"));
        Run(s);
        Assert.That(E.WorkshopBuilt(s, s.Ground, "kiln"), Is.True);
        s.Items.RemoveAll(i => i.ItemId is "clay" or "charcoal");
        return s;
    }

    // ───────────── Q400 窑台燃料明细按「批」 ─────────────

    /// <summary>
    /// Q400-A01：陶窑选 3 批，燃料规则「陶窑每批另需木炭1」、执行次数合计「执行3批」（右栏预览）；
    /// 燃料行计算部分「每批1×3批」、分账块「批数3」（制作面板源码）。锻炉同类文字仍用「份」。
    /// </summary>
    [Test]
    public void Q400_A01_KilnFuelBatchWording()
    {
        var kiln = Cfg.Recipes.Single(r => r.Id == Kiln);
        Assert.That(kiln.Fuel, Is.EquivalentTo(new Dictionary<string, int> { ["charcoal"] = 1 }));
        Assert.That(E.RecipeFuelRule(kiln), Is.EqualTo("陶窑每批另需木炭1"));

        var s = KilnCamp();
        E.CreateItems(s, "clay", 6, s.Ground);
        E.CreateItems(s, "charcoal", 3, s.Ground);
        var note = E.Preview(s, Craft(Kiln, 3)).Notes.Single(n => n.StartsWith("执行3批"));
        Assert.That(note, Does.StartWith("执行3批 · ").And.Contain("燃料木炭3").And.EndWith("（陶窑每批另需木炭1）"));
        Assert.That(note, Does.Not.Contain("份"));

        // 锻炉仍用「份」
        var forge = Cfg.Recipes.Single(r => r.Id == "make_steel");
        Assert.That(E.RecipeFuelRule(forge), Is.EqualTo("锻炉每份配方另需木炭1"));
        Assert.That(E.RecipeFuelSummary(forge, 3), Does.StartWith("执行3份配方 · "));

        var razor = Web("Components", "CraftOverlay.razor");
        Assert.That(razor, Does.Contain("Status=\"@($\"{(stationRecipe ? $\"每批{kv.Value}×{batch}批\" : $\"每份配方{kv.Value}×{batch}份\")} · 现有{fhave}"), "燃料行计算部分");
        Assert.That(razor, Does.Contain("<span>@(stationRecipe ? \"批数\" : \"份数\")<b>@batch</b></span>"), "Q398 批数仍在");
        Assert.That(razor, Does.Contain("<span class=\"muted\">@e.RecipeFuelRule(rc)</span>"), "分账块规则句走内核");
    }

    // ───────────── Q401 新面板一律收盘 ─────────────

    /// <summary>
    /// Q401-A01：打开新的一级浏览／工具面板（OpenPanel 任意非空面板、从别处直接打开系统说明）时，一律按 Q378 收起原物品操作盘——
    /// 只记 CardPopCovered（不清选中、不提交）；盘原本真正显示时渲染后把焦点交给新面板第一个可用控件（survivalPop.focusPanel，等四轨遮罩解除）。
    /// 关面板后玩家重新点来源再打开（Select/PickBatch 复位，沿 Q378）；Esc 在盘已收起时直接退面板（沿 Q378）；游戏菜单不走这里（Q390／Q395）。
    /// </summary>
    [Test]
    public void Q401_A01_NewPanelAlwaysCollapsesCardPop()
    {
        var g = Web("Services", "GameSession.cs");
        var collapse = g.Substring(g.IndexOf("private void CollapseCardPopForPanel(bool shown)"));
        collapse = collapse.Substring(0, collapse.IndexOf("\n    }"));
        Assert.That(collapse, Does.Contain("CardPopCovered = true;").And.Contain("if (shown) PanelFocusPending = true;"));
        Assert.That(collapse, Does.Not.Contain("Selected =").And.Not.Contain("ClearSelection").And.Not.Contain("Instant(").And.Not.Contain("DetailTab"),
            "只收盘：不清选中、不提交、不动右栏");

        // BM4a（Q590）：OpenPanel 改由 SwitchPanel 实现；新面板仍先收盘（盖在下层，整条链按返回关完后原样再显示，见 BM4aPanelReturnTests）
        var open = g.Substring(g.IndexOf("private void SwitchPanel(string? panel, string? recipe, bool cover)"));
        open = open.Substring(0, open.IndexOf("public void Guide("));
        var at = open.IndexOf("CollapseCardPopForPanel(CardPopShown);");
        Assert.That(at, Is.GreaterThan(0), "任意新面板都收盘，不看是否盖住来源");
        Assert.That(open.LastIndexOf("if (panel != null)", at), Is.GreaterThan(0));
        Assert.That(at, Is.LessThan(open.IndexOf("Pop = null;")), "在清就近盘之前判断盘是否正显示");
        Assert.That(open.IndexOf("if (panel != null && DialogueLocked) return;"), Is.LessThan(at), "对白锁不开面板时不动盘");

        var help = g.Substring(g.IndexOf("public void OpenHelp(string? topic = null, string? entry = null)"));
        help = help.Substring(0, help.IndexOf("\n    }"));
        Assert.That(help, Does.Contain("if (Panel != \"help\")").And.Contain("if (CardPopShown) CollapseCardPopForPanel(true);"), "系统说明直接定位打开同样收盘");

        var setMenu = g.Substring(g.IndexOf("public void SetMenu(bool open)"));
        setMenu = setMenu.Substring(0, setMenu.IndexOf("\n    }"));
        Assert.That(setMenu, Does.Not.Contain("CollapseCardPopForPanel").And.Not.Contain("CardPopCovered"), "游戏菜单仍按 Q390 只隐藏并恢复");

        var screen = Web("Components", "GameScreen.razor");
        Assert.That(screen, Does.Contain("if (G.PanelFocusPending)"));
        Assert.That(screen, Does.Contain("G.PanelFocusPending = false;"));
        Assert.That(screen, Does.Contain("await JS.InvokeVoidAsync(\"survivalPop.focusPanel\");"));
        Assert.That(Web("Services", "GameSession.Keys.cs"), Does.Contain("if (Panel != null && Panel != \"help\" && Panel != FireMenuPanel && !CardPopCovered && ShelfSelectionKind) return EscOwner.PanelSelection;"), "盘已收起时 Esc 退面板（沿 Q378；B45 火堆加工菜单除外）");

        var js = Web("wwwroot", "js", "app.js");
        var fp = js.Substring(js.IndexOf("focusPanel: function () {"));
        fp = fp.Substring(0, fp.IndexOf("\n    },"));
        Assert.That(fp, Does.Contain("document.querySelector('.wasteland .center-scroll[data-disk-inert]')"), "等遮罩解除再移焦点");
        Assert.That(fp, Does.Contain("document.querySelectorAll(COVER_SEL)"), "目标是新开的面板");
        Assert.That(fp, Does.Contain("hosts[h].contains(ae)) return;"), "焦点已在面板里就不动");

        // Q378 的复位与覆盖判定不变
        Assert.That(g, Does.Contain("CardPopCovered = false;   // B32d（Q378）：玩家重新点来源，操作盘照原条件再打开"));
        Assert.That(js, Does.Contain("if (t - cur.coveredAt >= 250) covered(cover);"));
    }

    // ───────────── Q402 拖木材预览与系统说明 ─────────────

    /// <summary>Q402-A01：木材可投多份时拖到火堆卡，预览沿 Q393 原句并可选数量（通用 Shift 后缀由 drag.js 按 Quantifiable 加）；只有 1 份时不带。系统说明「拖拽与落点」指向整张卡。</summary>
    [Test]
    public void Q402_A01_WoodDragShiftSuffixAndHelp()
    {
        var s = New();
        s.Items.RemoveAll(i => i.ItemId == "wood");
        E.CreateItems(s, "wood", 3, Camp);
        var per = E.Config.Fire.BurnMinutesPerWood;
        var drop = E.ResolveDrop(s, DropSource.Parse("card:camp|wood")!, DropTarget.Parse("fire:camp")!);
        Assert.That(drop.Ok, Is.True, drop.Reason);
        Assert.That(drop.Label, Is.EqualTo($"投放1木材，燃料+{per}分钟"));
        Assert.That(drop.Quantifiable, Is.True, "可投多份时带通用 Shift 后缀");
        Assert.That(drop.MaxQuantity, Is.GreaterThan(1));

        s.Items.RemoveAll(i => i.ItemId == "wood");
        E.CreateItems(s, "wood", 1, Camp);
        Assert.That(E.ResolveDrop(s, DropSource.Parse("card:camp|wood")!, DropTarget.Parse("fire:camp")!).Quantifiable, Is.False, "只有1份不带后缀");

        Assert.That(Web("wwwroot", "js", "drag.js"), Does.Contain("label.textContent = h.r.label + (h.r.qty ? ' · 按住 Shift 松手可选数量' : '');"));

        var help = Web("Services", "HelpCatalog.cs");
        var drag = help.Substring(help.IndexOf("Id = \"drag\", Title = \"拖拽与落点\""));
        drag = drag.Substring(0, drag.IndexOf("new HelpEntry"));
        Assert.That(drag, Does.Contain("松手才执行：木材拖到火堆或柴炉卡添加燃料，打火器拖到火堆点火，"));
        Assert.That(help, Does.Not.Contain("木材拖到火堆燃料区投料"));
    }

    // ───────────── 工具 ─────────────

    private static string Root()
    {
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "web", "Survival.sln"))) dir = dir.Parent;
        Assert.That(dir, Is.Not.Null, "找到仓库根目录");
        return dir!.FullName;
    }

    private static string Web(params string[] parts) => File.ReadAllText(Path.Combine(new[] { Root(), "web", "Survival.Web" }.Concat(parts).ToArray()));
}
