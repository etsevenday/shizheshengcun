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

    /// <summary>Q377-A01：拖木材到火堆卡加燃料成功，日志写原投入量与燃料数，后接「已作为燃料添加。」与右栏空加工位指引（份数沿配方）。</summary>
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
        Assert.That(s.Log.Last().Text, Is.EqualTo($"投入1木材，火堆燃料{Fac(s).FuelMinutes}分钟。已作为燃料添加。" + Guide));
        Assert.That(E.CharcoalSlotGuide(cp), Is.EqualTo(Guide));
    }

    /// <summary>Q377-A01：燃料容量不足时，拖木材到火堆卡的原拒绝原因（容量阻断）后补说明与指引；投入木材按钮的原阻断句不变。</summary>
    [Test]
    public void Q377_A01_FuelFullGuide()
    {
        var s = WithGroundWood(3);
        Fac(s).FuelMinutes = E.Config.Fire.MaxFuelMinutes;
        var full = $"至少需要{E.Config.Fire.BurnMinutesPerWood}分钟空余容量（燃料上限{E.Config.Fire.MaxFuelMinutes}分钟）";
        var drop = E.ResolveDrop(s, DropSource.Parse("card:camp|wood")!, DropTarget.Parse("fire:camp")!);
        Assert.That(drop.Ok, Is.False);
        Assert.That(drop.Reason, Is.EqualTo($"{full}。拖木材到火堆卡是添加燃料。" + Guide));
        Assert.That(E.DoInstant(s, InstantRequest.AddFuel(1)).Reason, Is.EqualTo(full), "按钮入口沿原阻断句");

        // 加工位判定里木材的拒绝原因同句（不再写“火堆燃料区”“点火堆卡”）
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
        Assert.That(screen, Does.Contain("else if (G.Panel != null && G.Panel != \"help\" && !G.CardPopCovered && G.Selected.Kind is"), "盘已收起时 Esc 退面板");
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
