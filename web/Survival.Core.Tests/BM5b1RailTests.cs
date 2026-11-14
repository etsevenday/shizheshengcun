using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 BM5b1「加工菜单、焦点、装备首屏、敌踪交战风险」右栏部分（R224）。规则出处：S14「2026-11-14 BM4b Q606—Q614完整答复：右栏、腐败物与对白聚焦」
/// Q607、Q608、Q609 与本批界面验收 Q607-A01、Q608-A01、Q609-A01；正式句见《重墨 V2 开工裁定与界面逐字稿》同批节。
/// 网页壳的接线以源码检查（同 BM4bRailTests）。
/// </summary>
public class BM5b1RailTests : TestBase
{
    private ItemInstance Make(GameState s, string id) => E.CreateItems(s, id, 1, ItemInstance.Carried).Single();

    // ───────────── Q608 ─────────────

    /// <summary>Q608-A01：木盾首屏「生效效果」一条摘要「盾面：格挡时减伤60%。」（60%取实际盾类格挡系数），说明组留原特性句与原条件、不再写“上线后生效”，不被说成常驻全伤减免。</summary>
    [Test]
    public void Q608_A01_WoodenShieldSummary()
    {
        var s = New();
        var shield = Make(s, "wooden_shield");
        var pct = Fmt.T((1 - E.Config.CombatMath.ShieldGuardFactor) * 100);
        Assert.That(pct, Is.EqualTo("60"), "当前配置 0.4");
        var active = E.EquipActiveTraitLines(s, shield);
        Assert.That(active, Is.EqualTo(new[] { $"盾面：格挡时减伤{pct}%。" }));
        Assert.That(active[0], Does.Contain("格挡时"), "满足格挡触发时的能力，不是常驻");
        var lines = E.RailEquipLines(s, shield, out var act2);
        Assert.That(act2, Is.EqualTo(active));
        var trait = lines.Where(x => x.Label == "固定特性").Select(x => x.Value).ToList();
        Assert.That(trait, Is.EqualTo(new[] { "盾面：盾类格挡减伤60%，不被动增加护甲" }), "原特性句与独有条件保留在说明");
        // 说明首行原描述与类型、固定特性完全同义：只留一份
        Assert.That(E.RailEquipDesc(shield, E.Item("wooden_shield").Desc), Is.Null);
        // 耐久为 0：按原规则效果暂停，不列生效效果
        shield.Durability = 0;
        Assert.That(E.EquipActiveTraitLines(s, shield), Is.Empty);
    }

    /// <summary>Q608-A01：原已标「生效中」的忽略护甲类特性进首屏（原特性句一次），说明组不再重复；其余固定特性按 Q638 资格（BM5b1TraitTests）。</summary>
    [Test]
    public void Q608_A01_ArmorIgnoreTraitsOnFirstScreenOnce()
    {
        var s = New();
        foreach (var id in new[] { "hatchet", "crossbow", "rifle" })
        {
            var it = Make(s, id);
            var w = E.Weapon(id)!;
            Assert.That(w.ArmorIgnore, Is.GreaterThan(0));
            Assert.That(E.EquipActiveTraitLines(s, it), Is.EqualTo(new[] { w.TraitText }), id);
            Assert.That(E.RailEquipLines(s, it, out _).Any(x => x.Label == "固定特性"), Is.False, $"{id}：每条一次");
            Assert.That(E.EquipDetailLines(s, it).Any(x => x.Label == "固定特性"), Is.True, $"{id}：一级面板内页（整备比较）原行不改（Q609）");
        }
        var machete = Make(s, "machete");
        Assert.That(E.EquipActiveTraitLines(s, machete), Is.Empty);
        Assert.That(E.RailEquipLines(s, machete, out _).Single(x => x.Label == "固定特性").Value, Is.EqualTo(E.Weapon("machete")!.TraitText), "Q638：已实装但割伤未解锁，说明组留原特性句、不再写上线后生效");
    }

    /// <summary>Q608：说明首行与类型／固定特性／固定效果完全同义时只显示一份；有独有内容的原描述原样保留。</summary>
    [Test]
    public void Q608_DescDedupe()
    {
        var s = New();
        foreach (var id in new[] { "machete", "fire_axe", "rescue_shield", "padded_vest", "knuckle_wraps", "work_shirt" })
            Assert.That(E.RailEquipDesc(Make(s, id), E.Item(id).Desc), Is.Null, id);
        foreach (var id in new[] { "stone_knife", "hatchet", "pistol" })
            Assert.That(E.RailEquipDesc(Make(s, id), E.Item(id).Desc), Is.EqualTo(E.Item(id).Desc), $"{id}：有独有内容，原样保留");
    }

    /// <summary>Q608 壳：对象详情与整备只读详情都在主要数值后列「生效效果」整行，说明组用去重后的描述；长攻击范围整行；不缩字（沿原 rail-main 规则）。</summary>
    [Test]
    public void Q608_Shell_Wiring()
    {
        foreach (var file in new[] { "ItemDetail.razor", "GearPeekDetail.razor" })
        {
            var src = Web("Components", file);
            Assert.That(src, Does.Contain("e.RailEquipLines(s, "), file);
            Assert.That(src, Does.Contain("<div class=\"wide rail-fx\"><span>@e.EquipTraitGroupLabel("), file);
            Assert.That(src, Does.Contain("e.RailEquipDesc("), file);
            Assert.That(src, Does.Contain("<div class=\"@(rangeText.Length > 14 ? \"wide\" : \"\")\"><span>攻击范围</span><b>@rangeText</b></div>"), file);
            Assert.That(src, Does.Not.Contain("e.EquipDetailLines("), file);
            var cond = src.IndexOf("<span>成色</span>", StringComparison.Ordinal);
            var fx = src.IndexOf("rail-fx", StringComparison.Ordinal);
            Assert.That(fx, Is.GreaterThan(cond), $"{file}：摘要在主要数值之后");
        }
        Assert.That(GameEngine.ActiveEffectsLabel, Is.EqualTo("生效效果"));
    }

    // ───────────── Q607／Q609 保持现状 ─────────────

    /// <summary>Q607-A01：任务角至多两行——第一行主目标短名（下一步全文在悬停）与收起键，第二行原入口；不把步骤全文搬成常显行。</summary>
    [Test]
    public void Q607_A01_TaskCornerTwoLines()
    {
        var src = Web("Components", "TaskCorner.razor");
        Assert.That(src, Does.Contain("<span class=\"tc-title\" title=\"@obj.Step\">@obj.Title</span>"), "短名常显、全文悬停");
        var row = Slice(src, "<div class=\"tc-row\">", "</div>");
        foreach (var entry in new[] { "建议 @(sugg.Count)", ">任务册</button>", ">远行准备</button>", "系统提示 @(showHint" })
            Assert.That(row, Does.Contain(entry), entry);
        Assert.That(row, Does.Not.Contain("@obj.Step"), "步骤全文不进常显第二行");
    }

    /// <summary>Q609-A01：本轮只改常态全局右栏；整备比较等一级面板内页仍用原装备行（不移到右栏、不重做）。</summary>
    [Test]
    public void Q609_A01_PanelInnerPagesUnchanged()
    {
        var preview = File.ReadAllText(Path.Combine(Root(), "src", "Core", "Engine", "GameEngine.GearPreview.cs"));
        Assert.That(preview, Does.Contain("var b = EquipDetailLines(s, cand);"), "整备比较仍用原行");
        Assert.That(preview, Does.Not.Contain("RailEquipLines"));
    }

    private static string Slice(string text, string from, string to)
    {
        var a = text.IndexOf(from, StringComparison.Ordinal);
        Assert.That(a, Is.GreaterThanOrEqualTo(0), from);
        var b = text.IndexOf(to, a, StringComparison.Ordinal);
        Assert.That(b, Is.GreaterThan(a), to);
        return text[a..b];
    }

    private static string Root()
    {
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "web", "Survival.sln"))) dir = dir.Parent;
        Assert.That(dir, Is.Not.Null, "找到仓库根目录");
        return dir!.FullName;
    }

    private static string Web(params string[] parts) => File.ReadAllText(Path.Combine(new[] { Root(), "web", "Survival.Web" }.Concat(parts).ToArray())).Replace("\r\n", "\n");
}
