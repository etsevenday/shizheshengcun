using Survival.Core.Config;
using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// B35b「UI 重做：制作页三列与来源侧页」内核部分（开发记录 doc/开发记录/B35b 制作页三列与来源侧页.md）：
/// 开工按钮“{动作名}（{时长}）”与右栏预估同源（S14 2026-11-14 B35 次要 Q597 A、2026-11-16 B35 Q801 A／Q802 A、S14 B51c Q873 A），
/// 不能开工只写动作名（S14 2026-11-18 B35b Q947 A），维修统一“开始维修”（Q912 A、Q965），
/// 陶窑／净水台“投料（{批数}批 · {时长}）”，右栏燃料注记删混算只留“每份耗／每批耗”（Q946 B），位置句改“本次制作”区（Q593 A）。
/// </summary>
public class B35bCraftButtonTests : TestBase
{
    private const string Kiln = "ex_ceramic_shell";

    private static ActionRequest Craft(string id, int batch = 1) => ActionRequest.Craft(id, null, null, batch);

    private static RecipeConfig Rc(string id) => Cfg.Recipes.Single(r => r.Id == id);

    /// <summary>营地建成全部工坊、学会全部蓝图、制造 10 级；清掉相关材料，只放 give 的数量在当前地面（同 B10c Forge）。</summary>
    private GameState Forge(params (string id, int n)[] give)
    {
        var s = New();
        E.DebugLearnAllKnowledge(s);
        E.DebugBuildAllWorkshops(s);
        s.Skills["crafting"].Level = 10;
        var ids = new[] { "charcoal", "iron_ore", "steel", "spring", "leather", "fastener", "clay" };
        s.Items.RemoveAll(i => ids.Contains(i.ItemId));
        foreach (var (id, n) in give) E.CreateItems(s, id, n, s.CurrentLocation);
        return s;
    }

    // ───────────── Q597／Q802：能开工且可靠——时长与右栏“预计耗时”同一次预估 ─────────────

    /// <summary>
    /// B35-Q802-01、Q597-A01：体力低（S01 时间倍率吃低体力）时预计耗时长于基础分钟；按钮写“开始制作（{预计耗时}）”，
    /// 与右栏“预计耗时”同一次预估、同一格式（Clock.Duration），不是基础 30 分钟；份数不再拼进按钮（原“开始制作×3份”取消）。
    /// </summary>
    [Test]
    public void Q597_ReliableCraft_ButtonUsesSameEstimate_NotBaseMinutes()
    {
        var s = Forge(("steel", 3), ("charcoal", 3));
        E.DebugSetStats(s, 20, 80, 80, 2, 80, 60);
        var p = E.Preview(s, Craft("make_spring", 3));
        Assert.That(p.Ok, Is.True, p.Reason);
        Assert.That(p.PlanFailed, Is.False);
        // 前提：这里预计耗时确实不等于基础工作量 3×30 分钟（否则测不出“误用基础分钟”）
        Assert.That(p.EstimatedMinutes, Is.Not.EqualTo(90), "低体力使预计耗时偏离基础分钟");
        Assert.That(p.ConfirmText, Is.EqualTo($"开始制作（{Clock.Duration(p.EstimatedMinutes)}）"), "Q597：与右栏预计耗时同源");
        Assert.That(p.ConfirmText, Does.Not.Contain("×3份").And.Not.Contain("90分钟").And.Not.Contain("1小时30分"));
    }

    /// <summary>Q597：钢材 1 份、正常体力白天——基础 60 分钟、倍率 1 时按钮写“开始制作（1小时）”（时长格式同右栏 Clock.Duration）。</summary>
    [Test]
    public void Q597_ReliableCraft_ExactText_OneHour()
    {
        var s = Forge(("iron_ore", 3), ("charcoal", 2));
        var p = E.Preview(s, Craft("make_steel"));
        Assert.That(p.Ok, Is.True, p.Reason);
        Assert.That(p.EstimatedMinutes, Is.EqualTo(60), "前提：白天体力正常、锻炉在室内工坊，倍率 1");
        Assert.That(p.ConfirmText, Is.EqualTo("开始制作（1小时）"));
    }

    // ───────────── Q947：不能开工只写动作名 ─────────────

    /// <summary>B35B-Q947-01：缺料不能开工——按钮只写“开始制作”，无括号；原具体原因仍在（Reason），开工仍被拒。</summary>
    [Test]
    public void Q947_CannotStart_VerbOnly_ReasonKept()
    {
        var s = Forge(("charcoal", 1));
        var p = E.Preview(s, Craft("make_spring"));
        Assert.That(p.Ok, Is.False);
        Assert.That(p.ConfirmText, Is.EqualTo("开始制作"));
        Assert.That(p.Reason, Does.Contain("钢材"), "原具体原因不删");
        Assert.That(E.Start(s, Craft("make_spring")).Ok, Is.False, "只改文字，不放行");
    }

    /// <summary>B35B-Q947-01：建造缺料同样只写动作名“开始建造”；材料齐时写“开始建造（{预计耗时}）”（配对）。</summary>
    [Test]
    public void Q947_Build_VerbOnly_ThenTimedWhenStartable()
    {
        var s = New();
        s.Items.RemoveAll(i => i.ItemId is "wood" or "fiber" or "stick");
        var blocked = E.Preview(s, ActionRequest.Craft("shelter"));
        Assert.That(blocked.Ok, Is.False);
        Assert.That(blocked.ConfirmText, Is.EqualTo("开始建造"));

        foreach (var kv in Rc("shelter").AllInputs) E.CreateItems(s, kv.Key, kv.Value, s.Ground);
        var ok = E.Preview(s, ActionRequest.Craft("shelter"));
        Assert.That(ok.Ok, Is.True, ok.Reason);
        Assert.That(ok.ConfirmText, Is.EqualTo($"开始建造（{Clock.Duration(ok.EstimatedMinutes)}）"));
    }

    // ───────────── Q801／Q873：能开工但求不出可信完成 ─────────────

    /// <summary>
    /// B35-Q801-01、B35B-Q947-02、S14 B51c Q873 A：能开工但完成不可靠——有同源短原因写“开始制作（未预估：{原因}）”，
    /// 没有可靠原因写“开始制作（未预估）”；不能开工的仍只写动作名（不写未预估）。
    /// </summary>
    [Test]
    public void Q801_Q873_Unreliable_NotEstimatedWithReason()
    {
        var rc = Rc("make_spring");
        Assert.That(GameEngine.StartButtonText(rc, new ActionPreview { Ok = true, PlanFailed = true, PlanFailShort = "燃料不足" }), Is.EqualTo("开始制作（未预估：燃料不足）"));
        Assert.That(GameEngine.StartButtonText(rc, new ActionPreview { Ok = true, PlanFailed = true }), Is.EqualTo("开始制作（未预估）"));
        Assert.That(GameEngine.StartButtonText(rc, new ActionPreview { Ok = false, PlanFailed = true, PlanFailShort = "燃料不足" }), Is.EqualTo("开始制作"));
        Assert.That(GameEngine.StartButtonText(rc, new ActionPreview { Ok = true, EstimatedMinutes = 75 }), Is.EqualTo("开始制作（1小时15分）"));
    }

    // ───────────── Q912／Q965：维修；拆解 ─────────────

    /// <summary>Q597／Q912 A／Q965：工具维护与现场修补的按钮都是“开始维修”，拆解“开始拆解”，建造“开始建造”（配方名不改）。</summary>
    [Test]
    public void Q912_Q965_RepairVerbs_SalvageVerb()
    {
        Assert.That(GameEngine.StartVerb(Rc("repair_selected_tool")), Is.EqualTo("开始维修"));
        Assert.That(GameEngine.StartVerb(Rc(GameEngine.FieldPatchRepairId)), Is.EqualTo("开始维修"));
        Assert.That(GameEngine.StartVerb(Rc("salvage_equipment")), Is.EqualTo("开始拆解"));
        Assert.That(GameEngine.StartVerb(Rc("shelter")), Is.EqualTo("开始建造"));
        Assert.That(GameEngine.StartVerb(Rc("make_spring")), Is.EqualTo("开始制作"));
        Assert.That(Rc("repair_selected_tool").Name, Is.EqualTo("工具维护"), "配方名不改");
        Assert.That(Rc(GameEngine.FieldPatchRepairId).Name, Is.EqualTo("现场修补"));
    }

    /// <summary>
    /// B35-Q802-01：物品卡的工具维护（选定目标）预览——能开工时按钮“开始维修（{同源预计耗时}）”；目标满耐久不能维修时只写“开始维修”。
    /// </summary>
    [Test]
    public void Q802_ToolMaintenance_TimedWhenStartable_VerbWhenBlocked()
    {
        var s = New();
        var tool = E.DebugCreateEquipment(s, "machete", "white", s.Ground);
        tool.MaxDurability = 100;
        tool.Durability = 40;
        foreach (var kv in Rc("repair_selected_tool").AllInputs) E.CreateItems(s, kv.Key, kv.Value, s.Ground);
        var req = new ActionRequest { Type = ActionType.Craft, TargetId = "repair_selected_tool", InstanceId = tool.InstanceId };
        var p = E.Preview(s, req);
        Assert.That(p.Ok, Is.True, p.Reason);
        Assert.That(p.ConfirmText, Is.EqualTo($"开始维修（{Clock.Duration(p.EstimatedMinutes)}）"));

        tool.Durability = 100;
        var full = E.Preview(s, req);
        Assert.That(full.Ok, Is.False);
        Assert.That(full.ConfirmText, Is.EqualTo("开始维修"));
    }

    // ───────────── 陶窑／净水台投料按钮 ─────────────

    /// <summary>
    /// Q597、B35B-Q947-03：陶壳（陶窑，固定配方被动 60 分钟）投料按钮“投料（{批数}批 · 1小时）”；批数照所选，时长是原固定配方时长，不乘批数。
    /// </summary>
    [Test]
    public void Q947_StationLoadText_StaticDuration()
    {
        Assert.That(GameEngine.StationLoadText(Rc(Kiln), 1), Is.EqualTo("投料（1批 · 1小时）"));
        Assert.That(GameEngine.StationLoadText(Rc(Kiln), 3), Is.EqualTo("投料（3批 · 1小时）"));
    }

    /// <summary>
    /// Q593 A（逐字稿 B35 表“陶窑／净水台对应操作指引”）：把陶窑配方当主动制作请求时，原因句的位置改成“在‘本次制作’区点‘投料（{批数}批 · {时长}）’”，
    /// 不再说“中间配方卡下方”；0 分钟、不占行动的原说明保留。
    /// </summary>
    [Test]
    public void Q593_StationReason_PointsToCraftArea()
    {
        var s = Forge(("clay", 6), ("charcoal", 3));
        var why = E.CanStart(s, Craft(Kiln, 2));
        Assert.That(why, Is.EqualTo("陶窑是被动加工：在‘本次制作’区点‘投料（2批 · 1小时）’开始（0分钟、不占行动）"));
        Assert.That(why, Does.Not.Contain("中间配方卡"));
    }

    // ───────────── Q946：右栏燃料注记 ─────────────

    /// <summary>
    /// B35B-Q946-01：锻炉 3 份钢材右栏注记“执行3份配方 · 成品钢材6 · 每份耗木炭1”——删原料＋燃料＝总计混算，执行份数与成品保留，不重复设施名。
    /// </summary>
    [Test]
    public void Q946_ForgeFuelNote_PerPortion_NoMixedTotal()
    {
        var s = Forge(("iron_ore", 9), ("charcoal", 6));
        var note = E.Preview(s, Craft("make_steel", 3)).Notes.Single(n => n.StartsWith("执行3份配方"));
        Assert.That(note, Is.EqualTo("执行3份配方 · 成品钢材6 · 每份耗木炭1"));
        Assert.That(note, Does.Not.Contain("＝共").And.Not.Contain("原料木炭").And.Not.Contain("锻炉"));
        Assert.That(E.RecipeFuelPerNote(Rc("make_steel")), Is.EqualTo("每份耗木炭1"));
    }

    /// <summary>B35B-Q946-01：陶窑写“每批耗木炭1”；无燃料配方不加注记（B35B-Q946-03 不给无燃料工序硬加一句）。</summary>
    [Test]
    public void Q946_KilnFuelNote_PerBatch_NoneForFuelless()
    {
        Assert.That(E.RecipeFuelPerNote(Rc(Kiln)), Is.EqualTo("每批耗木炭1"));
        Assert.That(E.RecipeFuelSummary(Rc(Kiln), 3), Does.StartWith("执行3批 · ").And.EndWith(" · 每批耗木炭1"));
        Assert.That(E.RecipeFuelPerNote(Rc("make_wooden_spear")), Is.Null);
        Assert.That(E.RecipeFuelSummary(Rc("make_wooden_spear"), 2), Is.Null);
    }

    // ───────────── Q593 位置句 ─────────────

    /// <summary>Q593 A（逐字稿 B35 表）：三句位置指引逐字，{按钮名}为实际按钮全文。</summary>
    [Test]
    public void Q593_GuideSentences_Verbatim()
    {
        Assert.That(GameEngine.CraftAreaFootNote("开始制作（1小时）"), Is.EqualTo("在‘本次制作’区点‘开始制作（1小时）’；时间为预计值。"));
        Assert.That(GameEngine.CraftAreaHint("开始建造"), Is.EqualTo("在‘本次制作’区点‘开始建造’；右栏显示完整预估。"));
        Assert.That(GameEngine.StationAreaGuide("投料（1批 · 1小时）"), Is.EqualTo("在‘本次制作’区点‘投料（1批 · 1小时）’。"));
    }
    // ───────────── 系统说明 ─────────────

    /// <summary>
    /// Q593 A（逐字稿“系统说明预估段”）：手工制作说明的“在配方卡下方点‘开始制作’”换成逐字预估段（期望句从逐字稿原文取）；
    /// 行动确认段里“在所选配方卡下方‘开始’”的旧位置也不再出现（暂用说法见开发记录“暂用文案”）。Q946 燃料说明句不在本批改（继承 Q943 C）。
    /// </summary>
    [Test]
    public void Help_CraftEstimateParagraph_Verbatim_OldPlacementGone()
    {
        var verbatim = File.ReadAllText(Path.Combine(SourceText.Root(), "doc", "策划案", "重墨像素风界面与卡片 v0.9", "重墨 V2 开工裁定与界面逐字稿.md"));
        const string head = "系统说明预估段：“";
        var at = verbatim.IndexOf(head, StringComparison.Ordinal);
        Assert.That(at, Is.GreaterThan(0));
        var para = verbatim.Substring(at + head.Length, verbatim.IndexOf('”', at + head.Length) - at - head.Length);
        Assert.That(para, Is.EqualTo("选择配方后，在‘本次制作’区查看耗时和产物，并点对应开工按钮；右栏显示完整预估。"));

        var help = SourceText.Web(Path.Combine("Services", "HelpCatalog.cs"));
        Assert.That(help, Does.Contain("火堆和锅里的要先取出。" + para));
        Assert.That(help, Does.Not.Contain("配方卡下方"));
        Assert.That(help, Does.Contain("制作和建造在‘本次制作’区点对应开工按钮；"));
    }
}
