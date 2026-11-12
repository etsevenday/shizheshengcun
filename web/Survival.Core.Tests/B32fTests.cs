using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v1.0 B32f：答复 Q393—Q396（B32d 线上复验定稿）。
/// 规则出处：《重墨 V2 开工裁定与界面逐字稿》§1.5「Q393–Q396 · B32d 线上复验定稿」与验收 Q393-A01、Q395-A01、Q396-A01；
/// S09「Q393 加燃料与火源建造指引」与验收 Q393-A02。内核文字与状态直接测；网页壳的接线（Q395）以源码检查（同 B31b—B32d）。
/// Q394（面板未盖住来源时焦点进面板）本批未做，见开发计划 B32f 节。
/// </summary>
public class B32fTests : TestBase
{
    private const string Camp = "camp";

    private static FacilityState Fac(GameState s) => s.Locations[Camp].Facility;

    private static int Wood(GameState s) => s.Items.Where(i => i.ItemId == "wood" && (i.IsCarried || i.Owner == Camp)).Sum(i => i.Count);

    private void Ok(GameState s, InstantRequest r)
    {
        var res = E.DoInstant(s, r);
        Assert.That(res.Ok, Is.True, res.Reason);
    }

    private GameState WithGroundWood(int n)
    {
        var s = New();
        s.Items.RemoveAll(i => i.ItemId == "wood");
        E.CreateItems(s, "wood", n, Camp);
        return s;
    }

    /// <summary>推进 minutes 世界分钟（普通等待）。</summary>
    private void Pass(GameState s, int minutes)
    {
        var end = s.GameMinute + minutes;
        while (s.GameMinute < end)
        {
            if (s.Main == null) Start(s, ActionRequest.Wait(60));
            else if (s.Main.Status == ActionStatus.Paused) E.Resume(s);
            E.Advance(s);
        }
        if (s.Main != null) E.Cancel(s);
    }

    /// <summary>两个加工位的完整内容（实例、物品、数量、工序进度与状态），用来核对加燃料没改任何工位。</summary>
    private string Slots(GameState s) =>
        string.Join("|", E.FireContents(s, Camp).Select(i => $"{i.InstanceId}:{i.ItemId}×{i.Count}"))
        + "#" + string.Join("|", Fac(s).Processes.Select(p => $"{p.RecipeId}:{p.TargetInstanceId}:{p.HeatDoneMinutes}/{p.RequiredHeatMinutes}:{p.Status}"));

    /// <summary>三种工位状态：全空、两批都在煅烧、两批都已烧好待取（每种都留 4 份木材在地面用来加燃料）。</summary>
    private GameState SlotState(string state)
    {
        switch (state)
        {
            case "empty":
                return WithGroundWood(4);
            case "processing":
            {
                var s = WithGroundWood(12);
                Ok(s, InstantRequest.LoadCharcoal());
                Ok(s, InstantRequest.LoadCharcoal());
                Fac(s).FireLit = true;
                Fac(s).FuelMinutes = 120;
                Pass(s, 30);
                Assert.That(Fac(s).Processes.Where(p => p.RecipeId == "fire_charcoal").ToList(), Has.Count.EqualTo(2), "两批都在煅烧");
                Assert.That(Fac(s).Processes.All(p => p.HeatDoneMinutes > 0 && p.HeatDoneMinutes < p.RequiredHeatMinutes), Is.True);
                return s;
            }
            case "done":
            {
                var s = WithGroundWood(12);
                Ok(s, InstantRequest.LoadCharcoal());
                Ok(s, InstantRequest.LoadCharcoal());
                Fac(s).FireLit = true;
                Fac(s).FuelMinutes = 60;
                Pass(s, 60);
                Assert.That(E.FireContents(s, Camp).Select(i => (i.ItemId, i.Count)), Is.EqualTo(new[] { ("charcoal", 3), ("charcoal", 3) }), "两个工位都是待取成品");
                Assert.That(E.FireFreeSlots(s, Camp), Is.EqualTo(0));
                return s;
            }
        }
        throw new ArgumentException(state);
    }

    // ───────────── Q393 加燃料只说燃料 ─────────────

    /// <summary>
    /// Q393-A01：两工位全空、全在加工、全有待取成品时，卡上「投入木材」与拖木材到整张卡各一次：只更新燃料、扣实际投入的木材，工位内容不变；
    /// 落点预览与成功日志只写燃料信息。
    /// </summary>
    [TestCase("empty", false)]
    [TestCase("empty", true)]
    [TestCase("processing", false)]
    [TestCase("processing", true)]
    [TestCase("done", false)]
    [TestCase("done", true)]
    public void Q393_A01_AddFuelAnySlotState(string state, bool drag)
    {
        var s = SlotState(state);
        var slots = Slots(s);
        var fuel0 = Fac(s).FuelMinutes;
        var wood0 = Wood(s);
        var per = E.Config.Fire.BurnMinutesPerWood;
        if (drag)
        {
            var drop = E.ResolveDrop(s, DropSource.Parse("card:camp|wood")!, DropTarget.Parse("fire:camp")!);
            Assert.That(drop.Ok, Is.True, drop.Reason);
            Assert.That(drop.Verb, Is.EqualTo(DropVerb.AddFuel));
            Assert.That(drop.Label, Is.EqualTo($"投放1木材，燃料+{per}分钟"), "落点预览只说燃料");
            Ok(s, drop.Instant!);
        }
        else Ok(s, InstantRequest.AddFuel());
        Assert.That(Fac(s).FuelMinutes, Is.EqualTo(fuel0 + per));
        Assert.That(Wood(s), Is.EqualTo(wood0 - 1), "只扣实际投入的木材");
        Assert.That(Slots(s), Is.EqualTo(slots), "不改任何工位内容");
        Assert.That(s.Log.Last().Text, Is.EqualTo($"投入1木材，火堆燃料{Fac(s).FuelMinutes}分钟。"), "成功日志只说燃料");
    }

    /// <summary>Q393-A01：三种工位状态下燃料满，按钮与拖放的拒绝都只写容量句，不附煅烧指引，也不改工位。</summary>
    [TestCase("empty")]
    [TestCase("processing")]
    [TestCase("done")]
    public void Q393_A01_FuelFullOnlyCapacity(string state)
    {
        var s = SlotState(state);
        Fac(s).FuelMinutes = E.FuelCapOf(Fac(s));
        var slots = Slots(s);
        var full = $"至少需要{E.Config.Fire.BurnMinutesPerWood}分钟空余容量（燃料上限{E.FuelCapOf(Fac(s))}分钟）";
        var drop = E.ResolveDrop(s, DropSource.Parse("card:camp|wood")!, DropTarget.Parse("fire:camp")!);
        Assert.That(drop.Ok, Is.False);
        Assert.That(drop.Reason, Is.EqualTo(full));
        Assert.That(E.DoInstant(s, InstantRequest.AddFuel(1)).Reason, Is.EqualTo(full));
        Assert.That(Slots(s), Is.EqualTo(slots));
    }

    /// <summary>Q393：柴炉用同一套文案（日志照原文写「火堆燃料」），容量拒绝写柴炉自己的燃料上限。</summary>
    [Test]
    public void Q393_StoveSameWording()
    {
        var s = New();
        E.DebugTeleport(s, "w19_survey");
        s.WorldEnemies.RemoveAll(w => w.LocationId == "w19_survey");
        var stove = E.CreateItems(s, "folding_stove", 1, ItemInstance.Carried).Single();
        Start(s, GameEngine.Deploy(stove));
        Run(s);
        var fac = s.Locations["w19_survey"].Facility;
        s.Items.RemoveAll(i => i.ItemId == "wood");
        E.CreateItems(s, "wood", 3, "w19_survey");
        var per = E.Config.Fire.BurnMinutesPerWood;
        var drop = E.ResolveDrop(s, DropSource.Parse("card:w19_survey|wood")!, DropTarget.Parse("fire:w19_survey")!);
        Assert.That(drop.Label, Is.EqualTo($"投放1木材，燃料+{per}分钟"));
        Ok(s, drop.Instant!);
        Assert.That(s.Log.Last().Text, Is.EqualTo($"投入1木材，火堆燃料{fac.FuelMinutes}分钟。"));
        var full = $"至少需要{per}分钟空余容量（燃料上限{E.FuelCapOf(fac)}分钟）";
        Assert.That(E.ResolveDrop(s, DropSource.Parse("card:w19_survey|wood")!, DropTarget.Parse("fire:w19_survey")!).Reason, Is.EqualTo(full));
        Assert.That(E.DoInstant(s, InstantRequest.AddFuel(1)).Reason, Is.EqualTo(full));
    }

    /// <summary>Q393-A01：右栏空加工位的「煅烧木炭」入口不变——加燃料后仍可投料煅烧；内核与界面源码不再有加燃料附带的煅烧句。</summary>
    [Test]
    public void Q393_A01_CharcoalEntryStillWorks()
    {
        var s = WithGroundWood(5);
        Ok(s, InstantRequest.AddFuel());
        Assert.That(E.CharcoalLoadPreview(s).Ok, Is.True);
        Ok(s, InstantRequest.LoadCharcoal());
        Assert.That(Fac(s).Processes.Where(p => p.RecipeId == "fire_charcoal").ToList(), Has.Count.EqualTo(1));
        Assert.That(Web("Components", "FireDetail.razor"), Does.Contain("@onclick=\"ToggleCharcoal\">煅烧木炭</button>"), "右栏空位入口仍在");

        var drop = File.ReadAllText(Path.Combine(Root(), "src", "Core", "Engine", "GameEngine.Drop.cs"));
        var instant = File.ReadAllText(Path.Combine(Root(), "src", "Core", "Engine", "GameEngine.Instant.cs"));
        Assert.That(drop, Does.Not.Contain("（作燃料；要煅烧木炭"), "Q392 落点附句已取代");
        Assert.That(drop, Does.Not.Contain("CharcoalFuelDragNote"), "容量拒绝不再附 Q377 说明");
        Assert.That(instant, Does.Not.Contain("已作为燃料添加。"), "Q377 成功附句已取代");
    }

    // ───────────── Q393-A02 火源建造指引 ─────────────

    /// <summary>Q393-A02：系统说明「营地设施」补逐字段；两个火堆配方在「营地建造」，石块×4、木材×2，制造等级 1，30 基础分钟。</summary>
    [Test]
    public void Q393_A02_FireBuildHelpAndRecipes()
    {
        var help = Web("Services", "HelpCatalog.cs");
        var build = help.Substring(help.IndexOf("Id = \"build\", Title = \"营地设施\""));
        build = build.Substring(0, build.IndexOf("new HelpEntry"));
        Assert.That(build, Does.Contain("\"在已建成的安全屋里，打开「手工制作」的「营地建造」，选择「建屋内火堆」。在允许建造的野外营地，选择「建野外火堆」。两种火堆都需要石块×4和木材×2。火堆点燃后，可以放锅烧水或烹饪，也可以直接烤制适合的食材。\""));

        foreach (var (id, name) in new[] { ("safehouse_fire", "建屋内火堆"), ("wild_fixed_fire", "建野外火堆") })
        {
            var r = Cfg.Recipes.Single(x => x.Id == id);
            Assert.That(r.Name, Is.EqualTo(name));
            Assert.That(r.IsBuild, Is.True, "IsBuild 归「营地建造」分类");
            Assert.That(r.Inputs, Is.EquivalentTo(new Dictionary<string, int> { ["stone"] = 4, ["wood"] = 2 }));
            Assert.That(r.BaseMinutes, Is.EqualTo(30));
            Assert.That(r.MinSkills, Is.EquivalentTo(new Dictionary<string, int> { ["crafting"] = 1 }));
            Assert.That(r.FacilityResult, Is.EqualTo("fireBuilt"));
        }
        Assert.That(E.ItemName("stone"), Is.EqualTo("石块"));
        Assert.That(E.ItemName("wood"), Is.EqualTo("木材"));
    }

    // ───────────── Q395 菜单下小盘隐藏恢复 ─────────────

    /// <summary>
    /// Q395-A01：点火工具盘、就近操作盘、食饮盘在游戏菜单（含战斗菜单）期间只隐藏（menu-hidden）、不卸载，所选与草稿保留；
    /// 点火盘的「点盘外收起」不把顶栏「菜单」和菜单里的点按算作盘外。关菜单后照原条件显示并现算；来源失效沿各盘原规则（不在这里改）。
    /// </summary>
    [Test]
    public void Q395_A01_SmallDisksHideUnderMenu()
    {
        var g = Web("Services", "GameSession.cs");
        Assert.That(g, Does.Contain("public bool MenuHidesDisks => MenuOpen || BattleMenuOpen;"));
        Assert.That(g, Does.Contain("public string MenuHideCss => MenuHidesDisks ? \" menu-hidden\" : \"\";"));
        var setMenu = g.Substring(g.IndexOf("public void SetMenu(bool open)"));
        setMenu = setMenu.Substring(0, setMenu.IndexOf("\n    }"));
        Assert.That(setMenu, Does.Not.Contain("Pop = null").And.Not.Contain("IgniteMenuOpen = false").And.Not.Contain("FeedPending = null"), "开菜单不收盘、不清草稿");

        var css = Web("wwwroot", "css", "shell.css");
        Assert.That(css, Does.Contain(".wasteland .apop.menu-hidden { display: none !important; }"));

        var ap = Web("Components", "ActionPops.razor");
        foreach (var c in new[] { "travel", "approach", "lockstack", "charcoal", "harvest", "gather" })
            Assert.That(ap, Does.Contain($"CssClass=\"@(\"{c}\" + G.MenuHideCss)\""), c);
        Assert.That(ap, Does.Not.Contain("MenuOpen"), "就近盘的显示条件不含菜单（不卸载）");

        var feed = Web("Components", "FeedPop.razor");
        var cond = feed.Substring(feed.IndexOf("@if (G.State != null && G.FeedPending"));
        cond = cond.Substring(0, cond.IndexOf("\n"));
        Assert.That(cond, Does.Not.Contain("MenuOpen"), "食饮盘菜单期间不卸载");
        Assert.That(feed, Does.Contain("CssClass=\"@(\"feed-pop\" + G.MenuHideCss)\""));

        var ig = Web("Components", "IgniteMenu.razor");
        Assert.That(ig, Does.Contain("CssClass=\"@(\"ignite\" + G.MenuHideCss)\""));
        Assert.That(ig, Does.Contain("\".fc-btn.ignite, .apop.ignite, [data-menu-key], .menu-scrim\""), "点「菜单」与菜单里不算点盘外");
        var outside = ig.Substring(ig.IndexOf("public void OutsideClick()"));
        outside = outside.Substring(0, outside.IndexOf("\n    }"));
        Assert.That(outside, Does.Contain("if (G.MenuHidesDisks) return;"));
        // B43：顶栏「菜单 ESC」悬停提示改由 ShellKeys.Esc 拼出（显示为 ESC），按钮仍带 data-menu-key、点击仍是 ToggleMenu
        Assert.That(Web("Components", "GameScreen.razor"), Does.Contain("data-menu-key aria-keyshortcuts=\"Escape\" title=\"@($\"{ShellText.Menu}（{ShellKeys.Esc}）\")\" @onclick=\"ToggleMenu\""));
        var screen = Web("Components", "GameScreen.razor");
        Assert.That(screen, Does.Contain("@if (G.IgniteMenuOpen && !battleUi && e.HasFireAt(s, s.Ground) && !e.FacOf(s, s.Ground)!.FireLit) { <IgniteMenu /> }"), "点火盘原失效条件不变");
    }

    // ───────────── Q396 系统说明「就近执行与浮窗」 ─────────────

    /// <summary>Q396-A01：同条首段最后一句换成新句，后文 Q391 的煅烧入口与「确认投料」保留。</summary>
    [Test]
    public void Q396_A01_NearbyHelpSentence()
    {
        var help = Web("Services", "HelpCatalog.cs");
        var nearby = help.Substring(help.IndexOf("Id = \"nearby\", Title = \"就近执行与浮窗\""));
        nearby = nearby.Substring(0, nearby.IndexOf("new HelpEntry"));
        Assert.That(nearby, Does.Contain("在所选时长旁“开始”。右栏显示对象详情和行动预估。选中火堆或柴炉后，可以在右栏操作加工位。\","));
        Assert.That(nearby, Does.Contain("在右栏空加工位点「煅烧木炭」，看清原料与燃料后点「确认投料」。"));
        Assert.That(help, Does.Not.Contain("右栏只看信息和预测，没有执行按钮。"));
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
