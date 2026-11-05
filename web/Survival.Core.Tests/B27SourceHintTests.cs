using Newtonsoft.Json.Linq;
using Survival.Core.Config;
using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 批次 B27（R10 Q219、R40 Q250）：来源查询的公开线索。规则来源：content-feeding-adjudications.json hints/hintPolicy、
/// knowledge-source-adjudications.json hintDisplay；验收 SOURCE-Q250-01—03。Q251 待定的物品不给线索，保持旧显示。
/// </summary>
public class B27SourceHintTests : TestBase
{
    private static readonly JObject Hints = (JObject)JObject.Parse(File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "design.content-feeding.json")))["hints"]!;

    private static string Hint(string cat) => (string)Hints[cat]!;

    private GameState Fresh()
    {
        var s = New();
        s.GameMinute = At(2, 9, 0);
        s.SpawnNextCheck = int.MaxValue;
        s.PatrolNextCheck = int.MaxValue;
        return s;
    }

    private string? HintOf(GameState s, string itemId) => E.ItemSourceHint(E.ItemSources(s, itemId), itemId);

    /// <summary>新局里来源全空的物品（配置里挑，不写死）。</summary>
    private string EmptyItem(GameState s, string kind) => Cfg.Items.First(i => i.Kind == kind && !Cfg.SourceHints.PendingItems.Contains(i.Id) && E.ItemSources(s, i.Id).Count == 0).Id;

    private static void Discover(GameState s, params string[] ids)
    {
        foreach (var id in ids) s.Resources[id].Discovered = true;
    }

    /// <summary>十句线索逐字读进配置，优先顺序与合同一致。</summary>
    [Test]
    public void SOURCE_Q250_03_TenHints_VerbatimInConfig()
    {
        Assert.That(Cfg.SourceHints.Hints.Keys, Is.EquivalentTo(Hints.Properties().Select(p => p.Name)));
        Assert.That(Hints.Properties().Count(), Is.EqualTo(10));
        foreach (var p in Hints.Properties()) Assert.That(Cfg.SourceHints.Hints[p.Name], Is.EqualTo((string)p.Value!), p.Name);
        Assert.That(Cfg.SourceHints.Priority, Is.EqualTo(new[] { "water", "food", "medicine", "ammo", "weapon", "armor", "container", "tool", "material", "fallback" }));
        foreach (var p in Hints.Properties())
            Assert.That(Cfg.Locations.Any(l => ((string)p.Value!).Contains(l.Name)), Is.False, $"{p.Name} 不报地点");
    }

    /// <summary>来源全空：只有一条线索（界面用它替换旧空列表句），按类别取对应原句。</summary>
    [TestCase("food", "food")]
    [TestCase("medical", "medicine")]
    [TestCase("material", "material")]
    [TestCase("ammo", "ammo")]
    [TestCase("weapon", "weapon")]
    public void SOURCE_Q250_01_AllEmpty_OneHintByKind(string kind, string cat)
    {
        var s = Fresh();
        var id = EmptyItem(s, kind);
        Assert.That(E.ItemSources(s, id), Is.Empty);
        Assert.That(HintOf(s, id), Is.EqualTo(Hint(cat)));
    }

    /// <summary>水：脏水在溪流之外没发现、没有可获取行时给水线索；tool kind 的打火器给工具线索。</summary>
    [Test]
    public void SOURCE_Q250_01_WaterAndTool()
    {
        var s = Fresh();
        foreach (var r in Cfg.Resources.Where(r => r.Outputs.ContainsKey("dirty_water"))) s.Resources[r.NodeId].Discovered = false;
        Assert.That(E.ItemSources(s, "dirty_water").Any(v => v.Usable), Is.False);
        Assert.That(HintOf(s, "dirty_water"), Is.EqualTo(Hint("water")));
        Assert.That(E.ItemSources(s, "lighter").Any(v => v.Usable), Is.False);
        Assert.That(HintOf(s, "lighter"), Is.EqualTo(Hint("tool")));
    }

    /// <summary>冲突按 hintPolicy：石刀、简易斧是 tool 也在武器表，weapon 在前；便携滤水器是 material 也有工具标签，tool 在前。</summary>
    [TestCase("stone_knife", "weapon")]
    [TestCase("hatchet", "weapon")]
    [TestCase("water_filter", "tool")]
    public void SOURCE_Q250_01_ConflictPriority(string id, string cat)
    {
        var rows = new List<ItemSourceView>();   // 只看类别：这几件在新局有已知配方，空列表等于没有可获取行
        Assert.That(E.ItemSourceHint(rows, id), Is.EqualTo(Hint(cat)));
    }

    /// <summary>已耗尽行保留原状态与排序，线索不进来源行；不重复追加（多行不可用仍一条）。</summary>
    [Test]
    public void SOURCE_Q250_01_DepletedRowsKept_HintAppendedOnce()
    {
        var s = Fresh();
        E.DebugMapTeleport(s, "w34_depot");
        ReadDialogues(s);
        Discover(s, "w34_depot_a");
        Assert.That(E.ItemSources(s, "canned_beans").Single(v => v.Kind == "resource").Usable, Is.True);
        Assert.That(HintOf(s, "canned_beans"), Is.Null);

        s.Resources["w34_depot_a"].Available = 0;
        var rows = E.ItemSources(s, "canned_beans");
        Assert.That(rows.Any(v => v.Usable), Is.False);
        var row = rows.Single(v => v.Kind == "resource");
        Assert.That(row.Status, Does.Contain("已耗尽"));
        Assert.That(E.ItemSourceHint(rows, "canned_beans"), Is.EqualTo(Hint("food")));
        Assert.That(E.ItemSources(s, "canned_beans").Select(v => v.Title), Is.EqualTo(rows.Select(v => v.Title)), "线索不是来源行");
    }

    /// <summary>没有路线的行不算可获取：原行与原因保留，追加线索。</summary>
    [Test]
    public void SOURCE_Q250_01_NoRouteRowKept_Hint()
    {
        var s = Fresh();
        Discover(s, "w34_depot_a");
        var rows = E.ItemSources(s, "canned_beans");
        Assert.That(rows.Single(v => v.Kind == "resource").Status, Is.EqualTo("现在没有已开通的路线"));
        Assert.That(E.ItemSourceHint(rows, "canned_beans"), Is.EqualTo(Hint("food")));
    }

    /// <summary>只看 Usable 字段，不从 Status 文字反推：写着“可获取”但 Usable=false 仍给线索，Usable=true 就不给；多行不可用也只有一句。</summary>
    [Test]
    public void SOURCE_Q250_01_UsesUsableFieldOnly()
    {
        var off = new ItemSourceView { Status = "可获取", Usable = false };
        var on = new ItemSourceView { Status = "已耗尽（不会补充）", Usable = true };
        Assert.That(E.ItemSourceHint(new[] { off, off, off }, "cloth"), Is.EqualTo(Hint("material")));
        Assert.That(E.ItemSourceHint(new[] { off, on }, "cloth"), Is.Null);
    }

    /// <summary>只有概率行：不算确定可获取，追加线索；概率行原样在前。</summary>
    [Test]
    public void SOURCE_Q250_02_DropOnly_NotUsable_Hint()
    {
        var s = Fresh();
        var rows = E.ItemSources(s, "cloth");
        Assert.That(rows.Single(v => v.Kind == "drop").Status, Is.EqualTo("概率，不是保底"));
        Assert.That(rows.Any(v => v.Usable), Is.False);
        Assert.That(E.ItemSourceHint(rows, "cloth"), Is.EqualTo(Hint("material")));
    }

    /// <summary>任一可获取就不追加：火堆加工原 Usable=true 算可获取（不额外压成不可用）；已知配方可做也算。</summary>
    [Test]
    public void SOURCE_Q250_02_AnyUsable_NoHint()
    {
        var s = Fresh();
        var clean = E.ItemSources(s, "clean_water");
        Assert.That(clean.Single(v => v.Kind == "process" && v.Title.Contains("烧水")).Usable, Is.True);
        Assert.That(E.ItemSourceHint(clean, "clean_water"), Is.Null);
    }

    /// <summary>资格变化后下一次查询重算：耗尽→线索；补回→线索消失。</summary>
    [Test]
    public void SOURCE_Q250_02_RecomputedWhenEligibilityChanges()
    {
        var s = Fresh();
        E.DebugMapTeleport(s, "w34_depot");
        ReadDialogues(s);
        Discover(s, "w34_depot_a");
        s.Resources["w34_depot_a"].Available = 0;
        Assert.That(HintOf(s, "canned_beans"), Is.Not.Null);
        s.Resources["w34_depot_a"].Available = 1;
        Assert.That(HintOf(s, "canned_beans"), Is.Null);
    }

    /// <summary>查询 0 时间 0 结算：状态序列化前后一字不变，不授知识、不开图、不接任务。</summary>
    [Test]
    public void SOURCE_Q250_03_ReadOnly()
    {
        var s = Fresh();
        var before = SaveSerializer.Serialize(s);
        foreach (var i in Cfg.Items) HintOf(s, i.Id);
        Assert.That(SaveSerializer.Serialize(s), Is.EqualTo(before));
    }

    /// <summary>Q251 待定：gear、carcass、substance 以及金属锅、分量水壶一律没有线索（界面保持旧句）。container/armor 目前没有物品映射到。</summary>
    [Test]
    public void SOURCE_Q250_03_Q251Pending_NoHint()
    {
        var s = Fresh();
        var pending = Cfg.Items.Where(i => i.Kind is "gear" or "carcass" or "substance").Select(i => i.Id).Concat(new[] { "pot", "ration_canteen" }).ToList();
        Assert.That(pending, Has.Count.EqualTo(35 + 4 + 1 + 2));
        foreach (var id in pending) Assert.That(E.ItemSourceHint(new List<ItemSourceView>(), id), Is.Null, id);
        var used = Cfg.Items.Select(i => E.ItemSourceHint(new List<ItemSourceView>(), i.Id)).Where(h => h != null).ToHashSet();
        Assert.That(used, Does.Not.Contain(Hint("container")).And.Not.Contain(Hint("armor")));
        Assert.That(E.ItemSourceHint(new List<ItemSourceView>(), "no_such_item"), Is.Null);
    }

    /// <summary>未映射的 kind 用 fallback 原句（配置校验不放行未知 kind，这里在内存里改一件）。</summary>
    [Test]
    public void SOURCE_Q250_03_UnknownKind_Fallback()
    {
        var cfg = ConfigLoader.FromJson(File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "config.v0.6.json")));
        cfg.Items.Single(i => i.Id == "cloth").Kind = "mystery";
        var e = new GameEngine(cfg);
        Assert.That(e.ItemSourceHint(new List<ItemSourceView>(), "cloth"), Is.EqualTo(Hint("fallback")));
    }
}
