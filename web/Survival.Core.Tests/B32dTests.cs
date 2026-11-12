using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v1.0 B32d：答复 Q377—Q379（B32b 线上试玩定稿）。
/// 规则出处：《重墨 V2 开工裁定与界面逐字稿》§1.5「Q377–Q379 · B32b 线上试玩定稿」与验收 Q377-A01、Q378-A01；02 待确认 Q377、Q378 选 A。
/// Q379（火堆／柴炉卡底键三档放主图右侧）认可 M664 现状，代码不动、不另加用例（B32c 已有）。
/// 内核文字直接测；网页壳的接线（Q378）以源码检查（同 B31b—B31d）。
/// </summary>
public class B32dTests : TestBase
{
    private const string Guide = "要煅烧木炭，请在右栏空加工位点「煅烧木炭」（每批木材×4）。";

    private static FacilityState Fac(GameState s) => s.Locations["camp"].Facility;

    private GameState WithGroundWood(int n)
    {
        var s = New();
        s.Items.RemoveAll(i => i.ItemId == "wood");
        E.CreateItems(s, "wood", n, "camp");
        return s;
    }

    // ───────────── Q377 煅烧指引改到右栏空加工位 ─────────────

    /// <summary>Q377-A01：拖木材到火堆卡加燃料成功，日志写原投入量与燃料数。B32f（Q393）起不再接「已作为燃料添加。」与煅烧指引（指引句本身仍按配方取份数）。</summary>
    [Test]
    public void Q377_A01_FuelSuccessGuide()
    {
        var s = WithGroundWood(3);
        var cp = E.CharcoalProcess!;
        Assert.That(cp.InputCount, Is.EqualTo(4), "当前煅烧配方每批 4 份");
        var drop = E.ResolveDrop(s, DropSource.Parse("card:camp|wood")!, DropTarget.Parse("fire:camp")!);
        Assert.That(drop.Verb, Is.EqualTo(DropVerb.AddFuel));
        var res = E.DoInstant(s, drop.Instant!);
        Assert.That(res.Ok, Is.True, res.Reason);
        Assert.That(s.Log.Last().Text, Is.EqualTo($"投入1木材，火堆燃料{Fac(s).FuelMinutes}分钟。"), "Q393 取代 Q377 的附句");
        Assert.That(E.CharcoalSlotGuide(cp), Is.EqualTo(Guide));
    }

    /// <summary>Q377-A01：燃料容量不足时拖木材到火堆卡的拒绝原因。B32f（Q393）起与「投入木材」按钮同为容量句，不再补说明与指引。</summary>
    [Test]
    public void Q377_A01_FuelFullGuide()
    {
        var s = WithGroundWood(3);
        Fac(s).FuelMinutes = E.Config.Fire.MaxFuelMinutes;
        var full = $"至少需要{E.Config.Fire.BurnMinutesPerWood}分钟空余容量（燃料上限{E.Config.Fire.MaxFuelMinutes}分钟）";
        var drop = E.ResolveDrop(s, DropSource.Parse("card:camp|wood")!, DropTarget.Parse("fire:camp")!);
        Assert.That(drop.Ok, Is.False);
        Assert.That(drop.Reason, Is.EqualTo(full), "Q393 取代 Q377 的附句");
        Assert.That(E.DoInstant(s, InstantRequest.AddFuel(1)).Reason, Is.EqualTo(full), "按钮入口沿原阻断句");

        // 加工位判定里木材的拒绝原因（不是加燃料提示，Q393 不涉及；不再写“火堆燃料区”“点火堆卡”）
        var w = s.Items.First(i => i.ItemId == "wood" && i.Owner == "camp");
        Assert.That(E.MaxPutInFire(s, w, out var why), Is.EqualTo(0));
        Assert.That(why, Is.EqualTo("拖木材到火堆卡是添加燃料。" + Guide));

        // 其他原因的拒绝不加这句（只在容量不足时补）
        Fac(s).FuelMinutes = 0;
        w.ReservedBy = 999;
        var reserved = E.ResolveDrop(s, DropSource.Parse("card:camp|wood")!, DropTarget.Parse("fire:camp")!);
        Assert.That(reserved.Reason, Does.Not.Contain("煅烧"));
    }

    /// <summary>Q377：界面与内核源码里不再有指向旧位置的两句（策划案原文不在此检查）。</summary>
    [Test]
    public void Q377_NoOldWording()
    {
        foreach (var dir in new[] { Path.Combine(Root(), "src"), Path.Combine(Root(), "web", "Survival.Web") })
            foreach (var f in Directory.EnumerateFiles(dir, "*.*", SearchOption.AllDirectories)
                         .Where(p => !p.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar) && !p.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar))
                         .Where(p => p.EndsWith(".cs") || p.EndsWith(".razor") || p.EndsWith(".js")))
            {
                var t = File.ReadAllText(f);
                Assert.That(t, Does.Not.Contain("点火堆卡“煅烧木炭”").And.Not.Contain("火堆燃料区是添加燃料"), f);
            }
    }

    // ───────────── Q378 覆盖来源卡的面板收起操作盘 ─────────────

    /// <summary>
    /// Q378-A01：打开会盖住来源卡的面板时，survivalPop 以面板实际覆盖范围判断来源失效（.panel-host、.overflow-layer；来源在面板里的不算；露出不足四分之一），
    /// 收盘只撤回本盘未提交的意图——选卡盘保留选中（CardPopCovered），其余盘同各自 ×；焦点交给盖住它的面板；玩家重新点来源（Select/PickBatch）再打开。
    /// 来源仍露在可见轨道的盘沿原显示条件（不在覆盖判断里的不动）。
    /// </summary>
    [Test]
    public void Q378_A01_CoveredSourceClosesDisk()
    {
        var js = Web("wwwroot", "js", "app.js");
        Assert.That(js, Does.Contain("var COVER_SEL = '.center-scroll.four .panel-host, .overflow-layer';"));
        Assert.That(js, Does.Contain("return !c.contains(a) && c.offsetWidth > 0 && c.offsetHeight > 0;"), "来源在面板里不算被盖住");
        Assert.That(js, Does.Contain("if (A.vis * open / (n * n) >= 0.25) return null;"), "与滚出同一口径：露出不足四分之一");
        Assert.That(js, Does.Contain("if (!a || A.vis < 0.25)"), "原滚出／消失判定照旧");
        Assert.That(js, Does.Contain("if (t - cur.coveredAt >= 250) covered(cover);"));
        var cov = js.Substring(js.IndexOf("function covered(cover) {"));
        cov = cov.Substring(0, cov.IndexOf("\n  }"));
        Assert.That(cov, Does.Contain("d.invokeMethodAsync('AnchorCovered')").And.Contain("cover.querySelectorAll("), "收盘后焦点交给面板");
        Assert.That(cov, Does.Not.Contain("restoreFocus"), "不把焦点还给被盖住的触发控件");

        var ap = Web("Components", "AnchorPop.razor");
        Assert.That(ap, Does.Contain("public Task AnchorCovered() => InvokeAsync(() => OnCovered.HasDelegate ? OnCovered.InvokeAsync() : OnClose.InvokeAsync());"));

        var cp = Web("Components", "CardPop.razor");
        Assert.That(cp, Does.Contain("OnClose=\"G.ClearSelection\" OnCovered=\"G.CoverCardPop\""));
        Assert.That(cp, Does.Contain("&& !G.CardPopCovered && G.Selected.Kind is"), "与 CardPopShown 同条件");

        var g = Web("Services", "GameSession.cs");
        Assert.That(g, Does.Contain("&& !MenuOpen && !BattleMenuOpen && !CardPopCovered && Selected.Kind is"));
        var cover = g.Substring(g.IndexOf("public void CoverCardPop()"));
        cover = cover.Substring(0, cover.IndexOf("\n    }"));
        Assert.That(cover, Does.Contain("CardPopCovered = true;").And.Not.Contain("Selected =").And.Not.Contain("ClearSelection").And.Not.Contain("Instant("),
            "只收盘：不清选中、不提交");
        var sel = g.Substring(g.IndexOf("public void Select(Selection sel)"));
        Assert.That(sel.Substring(0, sel.IndexOf("Notify();")), Does.Contain("CardPopCovered = false;"), "重新点来源再打开");
        var pick = g.Substring(g.IndexOf("public void PickBatch("));
        Assert.That(pick.Substring(0, pick.IndexOf("Notify();")), Does.Contain("CardPopCovered = false;"), "点同一张叠卡也算重新打开");

        var screen = Web("Components", "GameScreen.razor");
        Assert.That(Web("Services", "GameSession.Keys.cs"), Does.Contain("if (Panel != null && Panel != \"help\" && !CardPopCovered && ShelfSelectionKind) return EscOwner.PanelSelection;"), "盘已收起时 Esc 退面板");
    }

    // ───────────── Q390—Q392 B32d 实装后定稿 ─────────────

    /// <summary>Q390-A01：游戏菜单（含战斗菜单）期间选卡操作盘只隐藏、不卸载（盘内数量、去向保留），关菜单后原样恢复；覆盖来源卡的面板仍按 Q378 收盘。</summary>
    [Test]
    public void Q390_A01_MenuHidesDiskKeepsState()
    {
        var cp = Web("Components", "CardPop.razor");
        var cond = cp.Substring(cp.IndexOf("@if (G.State != null"));
        cond = cond.Substring(0, cond.IndexOf("{"));
        Assert.That(cond, Does.Not.Contain("!G.MenuOpen").And.Not.Contain("!G.BattleMenuOpen"), "菜单期间不卸载");
        Assert.That(cond, Does.Contain("!G.CardPopCovered"), "面板覆盖仍收盘");
        Assert.That(cp, Does.Contain("var menuHide = G.MenuOpen || G.BattleMenuOpen ? \" menu-hidden\" : \"\";"));
        Assert.That(Web("wwwroot", "css", "shell.css"), Does.Contain(".wasteland .apop.card-pop.menu-hidden { display: none !important; }"));
        Assert.That(Web("Services", "GameSession.cs"), Does.Contain("&& !MenuOpen && !BattleMenuOpen && !CardPopCovered && Selected.Kind is"), "菜单期间不算显示（不加遮罩）");
        Assert.That(Web("wwwroot", "js", "app.js"), Does.Contain("if (document.querySelector('.menu-modal')) {"), "菜单期间按键不落到隐藏的操作条");
    }

    /// <summary>Q391-A01：木炭物品说明与系统说明两处入口指向右栏空加工位；煅烧条仍带实际配方规则。</summary>
    [Test]
    public void Q391_A01_CharcoalEntryTexts()
    {
        Assert.That(E.Item("charcoal").Desc, Is.EqualTo("锻炉燃料与材料。在右栏空加工位点「煅烧木炭」：木材×4→木炭×3，占1个加工位、需60有效受热分钟，火堆燃料另计。"));
        var help = Web("Services", "HelpCatalog.cs");
        Assert.That(help, Does.Contain("$\"在右栏空加工位点「煅烧木炭」，看清后点「确认投料」：{e.CharcoalRuleText()}。原料只取你随身和当前地面的木材，投料不耗时间；火堆熄灭时也能先投，点燃后才开始计热。\""));
        Assert.That(help, Does.Contain("\"转移、食用、点火、等待加工这些直接操作不额外弹确认；在右栏空加工位点「煅烧木炭」，看清原料与燃料后点「确认投料」。\""));
        Assert.That(help, Does.Not.Contain("火堆卡上点“煅烧木炭”").And.Not.Contain("火堆“煅烧木炭”在卡旁浮窗"));
    }

    /// <summary>Q392-A01：拖木材悬停在火堆卡上，松手仍是添加燃料。B32f（Q393）起落点提示只写燃料，不再带 Q392 附句。</summary>
    [Test]
    public void Q392_A01_FuelDropHint()
    {
        var s = WithGroundWood(2);
        var drop = E.ResolveDrop(s, DropSource.Parse("card:camp|wood")!, DropTarget.Parse("fire:camp")!);
        Assert.That(drop.Verb, Is.EqualTo(DropVerb.AddFuel));
        Assert.That(drop.Label, Is.EqualTo($"投放1木材，燃料+{E.Config.Fire.BurnMinutesPerWood}分钟"), "Q393 取代 Q392 的附句");
        Assert.That(E.DoInstant(s, drop.Instant!).Ok, Is.True);
        Assert.That(Fac(s).FuelMinutes, Is.EqualTo(E.Config.Fire.BurnMinutesPerWood));
        Assert.That(Fac(s).Processes.Where(p => p.RecipeId == "fire_charcoal"), Is.Empty, "没有变成煅烧");
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
