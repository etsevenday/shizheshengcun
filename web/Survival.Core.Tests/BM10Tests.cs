using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发记录 BM10「次要问题合并」：Q749—Q755（B47b 原批；Q756 见 BM10Q756Tests）、Q856（B52b 原批）、Q869（BM9 线上试玩，文案）。
/// 规则出处：S14／R03／R09 文首「2026-11-17 B51c2、B47b与B52b三个完整原批」节（验收 LIFE-Q749-01 至 LIFE-Q856-01）、
/// S08 文首「2026-11-16 Q869」节（验收 COPY-Q869-01）、《开发方待确认问题》同号策划答复。网页壳接线以源码检查（同 BM9Tests）。
/// </summary>
public class BM10Tests : BattleTestBase
{
    protected override bool NoEnemyApproach => true;

    // ───────────── 策划原句 ─────────────
    /// <summary>Q749 A：「濒死与意志」主题摘要。</summary>
    private const string DyingSummary = "濒死、致死判定与意志成长";
    /// <summary>Q842／Q869：独立提示原句（无句末标点）。</summary>
    private const string XpFull = "今日可获得生产制造经验已满";

    // ───────────── Q749 主题摘要 ─────────────

    /// <summary>LIFE-Q749-01：「濒死与意志」主题摘要为批准短句，走与其他主题同一 Summary 槽；正文段落与条目不变。</summary>
    [Test]
    public void Q749_DyingTopicSummary()
    {
        var help = Web("Services", "HelpCatalog.cs");
        Assert.That(help, Does.Contain($"public const string DyingSummary = \"{DyingSummary}\";"));
        var at = help.IndexOf("Id = DyingTopic, Title = \"濒死与意志\"", StringComparison.Ordinal);
        Assert.That(at, Is.GreaterThan(0));
        var line = help[at..help.IndexOf('\n', at)];
        Assert.That(line, Does.Contain("Summary = DyingSummary,"), "主题摘要填批准短句（不再留空）");
        Assert.That(help, Does.Contain("Paragraphs = (e, s) => new[] { GameEngine.HelpThreeStates, GameEngine.HelpEnemies, GameEngine.HelpWillXp },"), "正文三段不变");
        // 摘要显示沿主题目录同一槽位（Q812 左右分栏另批实现，本批不恢复旧卡页、不另加显示位置）
        var overlay = Web("Components", "HelpOverlay.razor");
        Assert.That(overlay, Does.Contain("<small>@t.Summary</small>"));
    }

    // ───────────── Q750／Q856 左栏去重、其他位置两标签 ─────────────

    /// <summary>LIFE-Q856-01：左栏健康行数值处“0/{上限} · 濒死”，同行状态条只留「致死抵抗{抵抗率}」，不再有第二枚「濒死」。</summary>
    [Test]
    public void Q856_LeftHealthRow_OneDyingOnly()
    {
        var hud = Web("Components", "Hud.razor");
        var bar = hud.Split('\n').Single(l => l.Contains("<div class=\"vital-bar\">@if (id == StatId.Health && e.IsDying(s, s.Player))"));
        Assert.That(bar, Does.Contain("<em class=\"flag dying-res\" title=\"@GameEngine.DyingIconText\">@e.ResistanceShort(s.Player)</em>"), "保留真实致死抵抗标记");
        Assert.That(bar, Does.Not.Contain("GameEngine.DyingWord"), "同行不再重复「濒死」标签");
        Assert.That(hud, Does.Contain("@if (id == StatId.Health && e.IsDying(s, s.Player)) { <b>@GameEngine.DyingHealthText(s.Stats.Max(id))</b> }"), "数值处那次濒死保留");
        Assert.That(GameEngine.DyingHealthText(100), Is.EqualTo("0/100 · 濒死"), "R09 Q738");
    }

    /// <summary>LIFE-Q750-01：其他原位置仍是相邻两标签「濒死」「致死抵抗{抵抗率}」（队伍栏、战斗站位），抵抗率为本人值且只在真濒死时显示。</summary>
    [Test]
    public void Q750_OtherPlacesKeepTwoTags()
    {
        var party = Web("Components", "PartyPanel.razor");
        Assert.That(party, Does.Contain("@if (e.IsDying(s, a)) {<text> </text><b class=\"danger-text\" title=\"@GameEngine.DyingIconText\">@GameEngine.DyingWord</b><text> </text><span class=\"dying-res\">@e.ResistanceShort(a)</span>}"));
        var bs = Web("Components", "BattleScreen.razor");
        Assert.That(bs, Does.Contain("var hpText = dying ? GameEngine.DyingHealthText(st.HealthMax)"));
        Assert.That(bs, Does.Contain("if (G.E.IsDying(s, s.Player)) { <span class=\"st bad bdg dying-res\" title=\"@GameEngine.DyingIconText\">@G.E.ResistanceShort(s.Player)</span> }"));
        Assert.That(GameEngine.DyingWord, Is.EqualTo("濒死"));
    }

    /// <summary>LIFE-Q751-01：战斗信息队员页健康／体力行不加抵抗率，真濒死仍写“0/{上限} · 濒死”。</summary>
    [Test]
    public void Q751_BattleInfoNoResistanceRow()
    {
        var bp = Web("Components", "BattlePanel.razor");
        var row = bp.Split('\n').Single(l => l.Contains("<span>健康 / 体力</span>"));
        Assert.That(row, Does.Contain("GameEngine.DyingHealthText(va.Stats.HealthMax)"));
        Assert.That(row, Does.Not.Contain("ResistanceShort"));
        Assert.That(bp, Does.Not.Contain("ResistanceShort"), "战斗信息不另加抵抗明细行");
    }

    // ───────────── Q752 城内换药目标行 ─────────────

    /// <summary>LIFE-Q752-01：城内换药目标行，真濒死者带「致死抵抗{抵抗率}」（与「濒死」并排）；非濒死不加；原预估、资格不变。</summary>
    [Test]
    public void Q752_ClinicDressing_DyingTargetTags()
    {
        var s = New();
        s.Progress.Flags.UnionWith(new[] { "registration_authorized", "hub_registered" });
        s.GameMinute = At(2, 9, 0);
        s.SpawnNextCheck = int.MaxValue;
        s.PatrolNextCheck = int.MaxValue;
        E.DebugMapTeleport(s, "h04_infirmary");
        ReadDialogues(s);
        E.DebugQuickParty(s, 2);
        Assert.That(s.Party, Has.Count.EqualTo(2), "布场：主角＋一名同行者");
        var mate = s.Party.Single(id => id != GameState.PlayerId);
        E.DebugGive(s, "bandage", 2);
        E.DebugGive(s, "cloth", 2);
        s.Player.Stats.Health = 10;
        var m = s.Actors[mate];
        m.Stats.Health = 0;
        m.Skills[GameEngine.Willpower] = new SkillState { Level = 4, Xp = 0 };
        var v = E.CityServicePreview(s, "clinic_dressing")!;
        Assert.That(v.Targets, Has.Count.EqualTo(2));
        var mt = v.Targets.Single(t => t.ActorId == mate);
        var pt = v.Targets.Single(t => t.ActorId == GameState.PlayerId);
        // 出处：R09 §4 P = 65 + 2×(L−1)，L=4 → 71%；Q695 格式“致死抵抗{抵抗率}”
        Assert.That(mt.DyingResist, Is.EqualTo("致死抵抗71%"), "真濒死：本人抵抗率");
        Assert.That(pt.DyingResist, Is.Null, "非濒死不加");
        Assert.That(mt.Blocked, Is.Null, "资格不变：濒死者照常可换药");
        Assert.That(mt.Detail, Does.StartWith("预计：").And.Contain(GameEngine.ReviveText), "原治疗预估与“脱离濒死状态”不变");
        Assert.That(pt.Detail, Does.StartWith("预计："));
        // 界面：两标签在原预估之前
        var ov = Web("Components", "CityServiceOverlay.razor");
        var tags = ov.IndexOf("@if (x.DyingResist != null) {<text> </text><b class=\"danger-text\" title=\"@GameEngine.DyingIconText\">@GameEngine.DyingWord</b><text> </text><span class=\"dying-res\">@x.DyingResist</span>}", StringComparison.Ordinal);
        Assert.That(tags, Is.GreaterThan(0));
        Assert.That(tags, Is.LessThan(ov.IndexOf("<span class=\"small\">@x.Detail</span>", StringComparison.Ordinal)), "排在原预估前");
    }

    // ───────────── Q753／Q754／Q755 核对 ─────────────

    /// <summary>LIFE-Q753-01／Q754-01／Q755-01：健康说明现句、濒死状态图标用状态占位图（不借归零图）、未选指令再点同一队友保持查看。</summary>
    [Test]
    public void Q753_Q754_Q755_Unchanged()
    {
        var ui = Web("Services", "Ui.cs");
        Assert.That(ui, Does.Contain("归0先进入濒死（见“濒死与意志”），濒死后再受伤可能死亡。"));
        Assert.That(ui, Does.Not.Contain("归0即死亡"));
        var sv = Web("Services", "StatusView.cs");
        var dying = sv[sv.IndexOf("Id = \"dying\"", StringComparison.Ordinal)..];
        dying = dying[..dying.IndexOf("});", StringComparison.Ordinal)];
        Assert.That(dying, Does.Contain("Icon = \"status_unknown\""), "状态占位图");
        Assert.That(dying, Does.Not.Contain("status_zero"), "不借归零图");
        Assert.That(Web("Services", "GameSession.cs"), Does.Contain("BattleAllyView = BattleAllyView == actorId && BattleAction != null ? null : actorId;"), "只有已选指令时才切换");
    }

    // ───────────── Q869 投料成功日志句号 ─────────────

    private GameState Workshop()
    {
        var s = New(86901);
        s.Skills["crafting"].Level = 3;
        s.Progress.Flags.Add("h03_ceramic_notice_read");
        foreach (var (id, n) in new[] { ("stone", 8), ("clay", 6), ("iron_plate", 2) }) E.CreateItems(s, id, n, s.Ground);
        Start(s, ActionRequest.Craft("build_kiln", null, null, 1));
        Run(s);
        foreach (var (id, n) in new[] { ("ceramic_shell", 4), ("plank", 2), ("cloth", 2) }) E.CreateItems(s, id, n, s.Ground);
        Start(s, ActionRequest.Craft("build_water_station", null, null, 1));
        Run(s);
        s.Items.RemoveAll(i => i.ItemId is "clay" or "charcoal" or "dirty_water" or "filter_media" or "ceramic_shell" or "clean_water");
        s.Skills["crafting"].Level = 3;
        s.Skills["crafting"].Xp = 0;
        return s;
    }

    private int _seq;

    private ActionResult WaterBatch(GameState s)
    {
        E.CreateItems(s, "dirty_water", 4, s.Ground);
        E.CreateItems(s, "filter_media", 1, ItemInstance.Carried);
        var r = E.StartStationBatch(s, "ex_station_water", $"q869-{++_seq}", 1);
        Assert.That(r.Ok, Is.True, r.Reason);
        return r;
    }

    /// <summary>COPY-Q869-01：同一已满条件下，独立预估／在途批提示无句号；投料成功日志有且仅有一个模板句末句号。</summary>
    [Test]
    public void Q869_FeedLogKeepsOnePeriod()
    {
        var s = Workshop();
        var rc = Cfg.Recipes.Single(r => r.Id == "ex_station_water");
        var day = Clock.Day(s.GameMinute);
        for (var i = 0; i < 4; i++) { WaterBatch(s); AdvanceTo(s, s.GameMinute + 30); }   // 净水台每批 3，四批完成用满 12
        Assume.That(Clock.Day(s.GameMinute), Is.EqualTo(day), "布场：仍在同一日");
        Assert.That(s.Player.DailyCounts[$"{day}|stationXp|crafting"], Is.EqualTo(12));
        E.CreateItems(s, "dirty_water", 4, s.Ground);
        E.CreateItems(s, "filter_media", 1, ItemInstance.Carried);
        Assert.That(E.StationBatchXpPreview(s, rc, 1), Is.EqualTo(XpFull), "独立预估无句号");
        var r = E.StartStationBatch(s, "ex_station_water", $"q869-{++_seq}", 1);
        Assert.That(r.Ok, Is.True, r.Reason);
        Assert.That(E.StationBatchXpNote(s, E.StationBatchesHere(s, "water_station").Single()), Is.EqualTo(XpFull), "在途批提示无句号");
        var log = s.Log.Last(l => l.Text.Contains("投料（1批）")).Text;
        Assert.That(log, Does.EndWith("；各批同时开始加工；" + XpFull + "。"), "S08 Q869：……各批同时开始加工；今日可获得生产制造经验已满。");
        Assert.That(log, Does.Not.EndWith("。。"), "不重复追加句号");
    }

    // ───────────── 策划案对照 ─────────────

    /// <summary>策划案原句对照：Q749 摘要、Q869 日志形态与 S14 验收编号存在。</summary>
    [Test, Category("DesignSync")]
    public void DesignSync_Sentences()
    {
        var s14 = Doc("01 基础系统", "S14 主界面与全局UI交互系统.md");
        Assert.That(s14, Does.Contain($"Q749 A：系统说明“濒死与意志”主题的简短范围为“{DyingSummary}”"));
        foreach (var id in new[] { "LIFE-Q749-01", "LIFE-Q750-01", "LIFE-Q751-01", "LIFE-Q752-01", "LIFE-Q753-01", "LIFE-Q754-01", "LIFE-Q755-01", "LIFE-Q756-01", "LIFE-Q756-02", "LIFE-Q856-01" })
            Assert.That(s14, Does.Contain($"| {id} |"), id);
        var s08 = Doc("01 基础系统", "S08 手工制作系统.md");
        Assert.That(s08, Does.Contain($"形成“……各批同时开始加工；{XpFull}。”"));
    }

    // ───────────── 源码读取 ─────────────

    private static string Root()
    {
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "AGENTS.md"))) dir = dir.Parent;
        return dir!.FullName;
    }

    private static string Src(params string[] parts) => File.ReadAllText(Path.Combine(new[] { Root() }.Concat(parts).ToArray())).Replace("\r\n", "\n");

    private static string Web(params string[] parts) => Src(new[] { "web", "Survival.Web" }.Concat(parts).ToArray());

    private static string Doc(params string[] parts) => Src(new[] { "doc", "策划案", "系统策划总册 v1.0" }.Concat(parts).ToArray());
}
