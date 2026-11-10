using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v1.0 B32a：主界面卡片模板、状态挂载、隐藏实例号（R57—R61）。
/// 规则出处：《重墨 V2 开工裁定与界面逐字稿》§1.2「Q340–Q342、Q345–Q352 · 卡片与场景」与验收 Q340-A01、Q342-A01、Q345-A01、Q348-A01、Q349-A01、Q351-A01、Q352-A01；§2 P01b、P01c。
/// 判定在内核（CardStates、HideInstanceTags、FeedCopy.HideInstanceFields）的直接测；网页壳的接线与 CSS 以源码检查（同 B31b—B31d）。
/// </summary>
public class B32aCardTests : TestBase
{
    // ───────────── Q349 物品实例号 ─────────────

    /// <summary>Q349-A01：两把同名打火器都只显示「打火器」，靠来源区分；预演不带编号；提交仍作用于所选那一把。</summary>
    [Test]
    public void Q349_A01_TwoSameNameLighters_NoInstanceNumber_SubmitBindsPicked()
    {
        var s = New();
        var first = s.Items.First(i => i.ItemId == "lighter");
        E.DebugGive(s, "lighter", 1, "camp");
        var second = s.Items.Last(i => i.ItemId == "lighter");
        Assert.That(second.InstanceId, Is.Not.EqualTo(first.InstanceId));

        var opts = E.IgniteToolOptions(s);
        Assert.That(opts.Select(o => o.Name), Is.All.EqualTo("打火器"));
        Assert.That(opts.Select(o => o.Source).Distinct().Count(), Is.EqualTo(2), "同名靠真实来源区分");
        Assert.That(opts.Select(o => o.InstanceId), Is.EquivalentTo(new[] { first.InstanceId, second.InstanceId }), "选项仍绑定实际实例");

        s.Locations["camp"].Facility.FuelMinutes = 60;
        var pv = E.Preview(s, ActionRequest.Ignite(second.InstanceId));
        Assert.That(pv.Notes.Concat(new[] { pv.Reason, pv.Label, pv.ToolName ?? "" }), Has.None.Contains("#"), "预演不显示内部编号");

        var res = E.Start(s, ActionRequest.Ignite(second.InstanceId));
        Assert.That(res.Ok, Is.True, res.Reason);
        Assert.That(s.Main!.LockedItemIds, Does.Contain(second.InstanceId).And.Not.Contain(first.InstanceId), "提交作用于所选那一把");
        Assert.That(s.Log.Select(l => l.Text), Has.None.Contains("打火器#"));
    }

    /// <summary>Q349：记录回看遇到旧日志里的物品编号时只隐藏显示，保存的原记录不改；人物代号等非物品编号不动。</summary>
    [Test]
    public void Q349_OldLogDisplay_HidesItemNumbers_KeepsOriginal()
    {
        var old = "林昭饮用了净水×1（取自你的随身锅#5）：水分+30。使用打火器#12（随身），磨损1；随身木材#9×4投入。";
        Assert.That(E.HideInstanceTags(old), Is.EqualTo("林昭饮用了净水×1（取自你的随身锅）：水分+30。使用打火器（随身），磨损1；随身木材×4投入。"));
        Assert.That(E.HideInstanceTags("护送员#3跟上了"), Is.EqualTo("护送员#3跟上了"), "不是物品名后面的「#」不动");
        Assert.That(E.HideInstanceTags(null), Is.EqualTo(""));

        var s = New();
        s.Log.Add(new LogEntry { Minute = s.GameMinute, Text = old });
        _ = E.HideInstanceTags(s.Log.Last().Text);
        Assert.That(s.Log.Last().Text, Is.EqualTo(old), "保存的原记录不改");
    }

    /// <summary>Q349：食饮面板行与结果句的契约模板原样保留，填充时省略批次/编号/锅号/壶号字段。</summary>
    [Test]
    public void Q349_FeedRows_DropInstanceFields()
    {
        Assert.That(FeedCopy.HideInstanceFields(FeedPanelCopy.RowFood), Does.Not.Contain("#").And.Not.Contain("批次"));
        Assert.That(FeedCopy.HideInstanceFields(FeedPanelCopy.RowPotFood), Does.Not.Contain("#"));
        Assert.That(FeedCopy.HideInstanceFields(FeedPanelCopy.RowWaterCard), Does.Not.Contain("#").And.Not.Contain("编号"));
        Assert.That(FeedCopy.HideInstanceFields(FeedPanelCopy.RowCanteen), Does.Not.Contain("#"));
        Assert.That(FeedCopy.HideInstanceFields(FeedCopy.ResultPotFood), Is.EqualTo("{目标}食用了{物品}×1（取自{出物人}的随身锅）：{变化}。"));
    }

    // ───────────── Q345 状态挂载 ─────────────

    /// <summary>Q345-A01：加工中只挂在当前行动作用的对象上（资源、路线、建造中的设施）；耐久为 0 的工具为危险。</summary>
    [Test]
    public void Q345_A01_BusyAndDangerStates()
    {
        var s = New();
        Assert.That(CardStates.BusyResource(s, "roots"), Is.False, "无行动不挂加工中");
        s.Main = new ActionState { Type = ActionType.Gather, TargetId = "roots" };
        Assert.That(CardStates.BusyResource(s, "roots"), Is.True);
        Assert.That(CardStates.BusyResource(s, "fiber"), Is.False);
        Assert.That(CardStates.BusyRoute(s, "roots"), Is.False, "采集不算前往");
        s.Main = new ActionState { Type = ActionType.Travel, TargetId = "field" };
        Assert.That(CardStates.BusyRoute(s, "field"), Is.True);
        Assert.That(CardStates.BusyRoute(s, "camp"), Is.False);
        s.Main = new ActionState { Type = ActionType.Build, TargetId = "shelter" };
        Assert.That(CardStates.BusyBuild(s, "shelter"), Is.True);
        Assert.That(CardStates.BusyBuild(s, "bed"), Is.False);

        var lighter = s.Items.First(i => i.ItemId == "lighter");
        Assert.That(CardStates.BrokenTool(lighter), Is.False);
        lighter.Durability = 0;
        Assert.That(CardStates.BrokenTool(lighter), Is.True);
        var water = E.CreateItems(s, "clean_water", 1, "camp").Single();
        Assert.That(CardStates.BrokenTool(water), Is.False, "没有耐久的物品不算");
    }

    /// <summary>Q345 接线：资源卡库存为 0 与自身原因为不可用、当前采集为加工中；敌踪敌对与发现敌踪为危险；路线未开通为不可用；腐败与耐久 0 为危险。</summary>
    [Test]
    public void Q345_Wiring()
    {
        var strip = Web("Components", "LocationStrip.razor");
        Assert.That(strip, Does.Contain("Dim=\"empty\"\n                                Blocked=\"@(cardWhy != null)\" Busy=\"@CardStates.BusyResource(s, nodeId)\"").Or.Contain("Dim=\"empty\"\r\n                                Blocked=\"@(cardWhy != null)\" Busy=\"@CardStates.BusyResource(s, nodeId)\""));
        Assert.That(strip, Does.Contain("Danger=\"@ec.Elite\""), "Q357 取代：普通常驻敌踪不挂危险，精英挂");
        Assert.That(Web("Components", "LocationCard.razor"), Does.Contain("Busy=\"@CardStates.BusyRoute(s, LocationId)\"").And.Contain("Badge=\"未开通\" BadgeClass=\"unbuilt\" Dim=\"true\""));
        var shelf = Web("Components", "ItemShelf.razor");
        Assert.That(shelf, Does.Contain("@(spoiledFront ? \"st-danger\" : \"\")").And.Contain("CardStates.BrokenTool(t) ? \"broken st-danger\""));
        var css = Web("wwwroot", "css", "cards.css");
        Assert.That(css, Does.Contain(".ocard.k2:is(.dim, .st-disabled) > .k2-art::after"), "不可用斜线只盖主图区");
    }

    // ───────────── Q340 卡面 ─────────────

    /// <summary>Q340-A01：新模板基准 194×155，三档按窗口高度（≤800 0.8 倍、≥1000 1.2 倍）；主操作 ≤800 在主图右、≥801 在信息条右（Q37）。</summary>
    [Test]
    public void Q340_A01_TemplateSizesAndActionPlacement()
    {
        var css = Web("wwwroot", "css", "cards.css");
        Assert.That(css, Does.Contain("--k2-w: 155px;\n  --k2-h: 124px;").Or.Contain("--k2-w: 155px;\r\n  --k2-h: 124px;"));
        Assert.That(css, Does.Contain("@media (min-height: 801px) { :root { --k2-w: 194px; --k2-h: 155px;"));
        Assert.That(css, Does.Contain("@media (min-height: 1000px) { :root { --k2-w: 233px; --k2-h: 186px;"));
        var low = css.IndexOf("@media (max-height: 800px) {\n  .ocard.k2.has-act > .k2-art { grid-column: 1; }", StringComparison.Ordinal);
        if (low < 0) low = css.IndexOf("@media (max-height: 800px) {\r\n  .ocard.k2.has-act > .k2-art { grid-column: 1; }", StringComparison.Ordinal);
        Assert.That(low, Is.GreaterThan(0), "≤800 主操作移到主图右侧");
        var html = Web("wwwroot", "index.html");
        Assert.That(html.IndexOf("css/cards.css", StringComparison.Ordinal), Is.GreaterThan(html.IndexOf("css/shell.css", StringComparison.Ordinal)), "卡模板样式在 shell.css 之后加载");

        var card = Web("Components", "ObjectCard.razor");
        Assert.That(card, Does.Contain("<div class=\"k2-art\">").And.Contain("<b class=\"k2-name\" title=\"@Title\">@Title</b>").And.Contain("<div class=\"k2-act\">@Footer</div>"));
        Assert.That(card, Does.Not.Contain(">查看<"), "卡面点选只查看，不另加通用「查看」钮");
    }

    /// <summary>Q340：资源副标「账短标 · 每次产量」（日补只在悬停）；敌踪副标健康数值；路线副标方向与单程时间；物品副标按保质 → 耐久 → 数量取一项。</summary>
    [Test]
    public void Q340_Subtitles()
    {
        var strip = Web("Components", "LocationStrip.razor");
        Assert.That(strip, Does.Contain("var perLine = $\"{GameEngine.AccountLabel(r)} · 每次{per}{e.ItemName(r.Outputs.Keys.First())}\";"));
        Assert.That(strip, Does.Contain("var perTip = perLine + (r.Refills ? $\" · 日补{r.DailyRefill}\" : \"\");"));
        Assert.That(strip, Does.Contain("Sub=\"@($\"健康 {Ui.N(w.Hp)}/{Ui.N(ec.Hp)}\")\""));
        var loc = Web("Components", "LocationCard.razor");
        Assert.That(loc, Does.Contain("<span class=\"k2-subtext\">单程@(edge?.BaseMinutes ?? 0)分钟</span>").And.Contain("<span class=\"dir-mark\""));
        var shelf = Web("Components", "ItemShelf.razor");
        Assert.That(shelf, Does.Contain("<div class=\"k2-sub is-qty\" title=\"@(qtyTip ?? infoLine)\">@qtyText</div>"), "Q356 取代：副标固定数量");
        Assert.That(shelf, Does.Not.Contain("#@"), "物品卡不显示内部编号");
    }

    // ───────────── Q342 过渡 ─────────────

    /// <summary>Q342-A01：现行随机敌踪仍在覆盖层整组办理「接敌」「绕行（{时长}）」，不参战者「留在原地」，参战敌人角标「敌踪待处理」（P01c）；常驻敌人仍走「接近」。1280 下卡片区不再撑出最小高度。</summary>
    [Test]
    public void Q342_A01_EncounterOverlayTransition()
    {
        var o = Web("Components", "BattleOverlay.razor");
        Assert.That(o, Does.Contain("Badge=\"@(stays ? \"留在原地\" : \"敌踪待处理\")\""));
        Assert.That(o, Does.Contain("<strong>接敌</strong>").And.Contain("<strong>绕行（@Clock.Duration(cfg.Encounters.BypassMinutes)）</strong>"));
        Assert.That(o, Does.Not.Contain("绕行 · "));
        Assert.That(Clock.Duration(10), Is.EqualTo("10分钟"));
        Assert.That(Web("Components", "LocationStrip.razor"), Does.Contain("<strong>接近 ▸</strong>"));
        var css = Web("wwwroot", "css", "cards.css");
        Assert.That(css, Does.Contain(".battle-overlay.encounter > .bt-cards { grid-area: cards; min-height: 0;"));
        // B32a 试玩修复：敌人卡保持模板高度（不再按剩余高度压扁），说明行移到标题行，覆盖层过高时自身滚动
        Assert.That(css, Does.Contain(".wasteland .battle-overlay.encounter .bt-cards > .ocard.k2.k2 { height: var(--k2-h); min-height: 0; flex: 0 0 var(--k2-w); }"));
        Assert.That(css, Does.Contain("grid-template-areas: \"head log\" \"cards cards\" \"act act\";"));
    }

    // ───────────── B32a 试玩修复（线上构建 c38c397） ─────────────

    /// <summary>试玩 1：不可堆叠物品（尸体等）走单件卡，腐败挂危险叠层与腐败样式。试玩 5 的副标取法已由 Q356 取代（见 Q356_A01）。</summary>
    [Test]
    public void PlaytestFix_SingleItemCardSpoiled()
    {
        var shelf = Web("Components", "ItemShelf.razor");
        Assert.That(shelf, Does.Contain("var spoiledT = e.IsSpoiled(s, t);"));
        Assert.That(shelf, Does.Contain("@(spoiledT ? \"spoiled st-danger\" : \"\")"));
        var carcass = Cfg.Items.First(i => i.Id == "carcass_wolf");
        Assert.That(carcass.IsStackable, Is.False, "尸体逐件成卡，走单件卡分支");
    }

    /// <summary>试玩 3、6、7：随身卡也按模板尺寸；≤800 高③④区内边距留出 124 卡高；来源行占整行；≤800 有主操作的卡角标贴角收紧。</summary>
    [Test]
    public void PlaytestFix_SizesAndLayout()
    {
        var css = Web("wwwroot", "css", "cards.css");
        Assert.That(css, Does.Contain(".card-col:not([style*=\"span\"]) > .card.item.k2i { width: min(var(--k2-w), 100%); }"));
        Assert.That(css, Does.Contain(".center-scroll.four > .section:is(.ground, .carried) { padding-top: 6px; padding-bottom: 6px; }"));
        Assert.That(css, Does.Contain(".facts.res-source { grid-template-columns: minmax(0, 1fr); }"));
        Assert.That(css, Does.Contain(".ocard.k2.has-act > .k2-art > .k2-tag { top: 4px; right: 4px; max-width: calc(100% - 8px); padding: 0 3px; }"));
    }

    /// <summary>试玩 4（R61、Q351）：指着已选中资源卡自己的采集键（含采到库存 0 后变禁用）时右栏保持「已选中」与来源行。</summary>
    [Test]
    public void PlaytestFix_SelectedResourceIgnoresOwnHover()
    {
        var d = Web("Components", "DetailPanel.razor");
        Assert.That(d, Does.Contain("var hover = sel.Kind == SelectionKind.Resource && G.HoverPreview is { Type: ActionType.Gather, ToolInstanceId: null, BareHands: false } hp && hp.TargetId == sel.Id"));
        Assert.That(d, Does.Contain("else if (hover != null && G.Panel == null)"));
    }

    // ───────────── Q348 分组 ─────────────

    /// <summary>Q348-A01：第二轨保留设施、敌踪、采集资源、远征等行内小标签；空分组不填虚构卡，空态沿现行文字。</summary>
    [Test]
    public void Q348_A01_GroupsKept_NoFakeCards()
    {
        var strip = Web("Components", "LocationStrip.razor");
        foreach (var t in new[] { "<div class=\"dg-title\">设施</div>", "<div class=\"dg-title\">敌踪</div>", "<div class=\"dg-title\">采集资源</div>", "<div class=\"dg-title\">远征</div>" })
            Assert.That(strip, Does.Contain(t));
        Assert.That(strip, Does.Contain("@if (enemies.Count > 0)"), "没有敌踪不出空分组");
        Assert.That(strip, Does.Contain("这里还没有发现资源点，探索周边可能找到线索"));
    }

    // ───────────── Q351 资源来源 ─────────────

    /// <summary>Q351-A01：右栏「来源」写「{资源所在地点名} · {资源账短标}」，取当前配置。</summary>
    [Test]
    public void Q351_A01_ResourceSource()
    {
        foreach (var r in Cfg.Resources.Take(40))
            Assert.That(CardStates.ResourceSource(E, r), Is.EqualTo($"{E.LocName(r.LocationId)} · {GameEngine.AccountLabel(r)}"), r.NodeId);
        Assert.That(Web("Components", "DetailPanel.razor"), Does.Contain("<span>来源</span><strong>@CardStates.ResourceSource(e, r)</strong>"));
    }

    // ───────────── Q352 加载 ─────────────

    /// <summary>Q352-A01：读档、新开、回标题回默认人物页并清临时对象、批次选择、待确认行动与草稿。</summary>
    [Test]
    public void Q352_A01_LoadClearsTemporarySelection()
    {
        var g = Web("Services", "GameSession.cs");
        var at = g.IndexOf("private void ResetUi()", StringComparison.Ordinal);
        Assert.That(at, Is.GreaterThan(0));
        var body = g.Substring(at, g.IndexOf("ResetBattleUi();", at, StringComparison.Ordinal) - at);
        foreach (var line in new[] { "Selected = new Selection();", "SelectedBatchKey = null;", "PendingAction = null;", "GivePending = null;", "FormationDraft = null;", "DetailTab = DetailTab.Character;", "Pop = null;", "FeedPending = null;", "PartyItemsOpen = null;" })
            Assert.That(body, Does.Contain(line), line);
    }

    // ───────────── Q356—Q360（逐字稿 §1.4） ─────────────

    /// <summary>Q356-A01：堆叠食物、部分预留物品、独立耐久工具副标都是本卡数量；第二行已腐败/已损坏优先，其次保质，再耐久；不再出主图数量角标；复合锅卡不变。</summary>
    [Test]
    public void Q356_A01_ItemSubIsQuantity_Line2StatusFirst()
    {
        var s = New();
        // 堆叠食物：副标数量，第二行保质；腐败后第二行「已腐败」
        var berries = E.CreateItems(s, "berry", 3, "camp");
        Assert.That(CardStates.ItemQtyText(berries.Sum(i => i.Count), 0), Is.EqualTo($"×{berries.Sum(i => i.Count)}"));
        var b0 = berries.First();
        Assert.That(b0.ExpiresAt, Is.Not.Null);
        Assert.That(CardStates.ItemLine2Of(E, s, b0), Is.EqualTo(ItemLine2.Expiry));
        b0.ExpiresAt = s.GameMinute;
        Assert.That(CardStates.ItemLine2Of(E, s, b0), Is.EqualTo(ItemLine2.Spoiled), "已腐败优先于保质");
        // 部分预留：沿「×{总数}（预留{数量}）」
        Assert.That(CardStates.ItemQtyText(5, 2), Is.EqualTo("×5（预留2）"));
        Assert.That(CardStates.ItemQtyText(2, 2), Is.EqualTo("×2"), "全部预留时只写总数（沿原格式）");
        // 独立耐久工具：副标「×1」，第二行耐久；损坏后「已损坏」优先
        var knife = E.CreateItems(s, "stone_knife", 1, "camp").Single();
        Assert.That(Cfg.Items.First(i => i.Id == "stone_knife").IsStackable, Is.False, "独立工具卡");
        Assert.That(knife.Count, Is.EqualTo(1));
        Assert.That(CardStates.ItemLine2Of(E, s, knife), Is.EqualTo(ItemLine2.Durability));
        knife.Durability = 0;
        Assert.That(CardStates.ItemLine2Of(E, s, knife), Is.EqualTo(ItemLine2.Broken));
        // 无保质无耐久：不出第二行
        var filter = E.CreateItems(s, "water_filter", 1, "camp").Single();
        Assert.That(CardStates.ItemLine2Of(E, s, filter), Is.EqualTo(ItemLine2.None));

        var shelf = Web("Components", "ItemShelf.razor");
        Assert.That(shelf, Does.Not.Contain("k2-tag qty"), "去掉重复的主图数量角标");
        Assert.That(shelf, Does.Contain("<div class=\"k2-sub is-qty\" title=\"@tLine\">@($\"×{t.Count}\")</div>"), "独立工具卡副标「×1」");
        Assert.That(shelf, Does.Contain("var tLine2 = deployed ? tLine : Ui.CardLine2(e, s, t);"), "自身阻断（已部署）排在第二行最前");
        Assert.That(shelf, Does.Contain("var line2 = spoiledFront ? \"已腐败\" : Ui.CardLine2(e, s, stacked ? head : whole);"));
        Assert.That(shelf, Does.Contain("<PotCard @key=\"@(\"t:\" + inst)\" Pot=\"t\" />"), "复合锅卡仍走锅卡（水量与食材）");
        var ui = Web("Services", "Ui.cs");
        var at = ui.IndexOf("public static string? CardLine2(", StringComparison.Ordinal);
        var body = ui.Substring(at, ui.IndexOf("public static string ItemLine(", at, StringComparison.Ordinal) - at);
        Assert.That(body, Does.Not.Contain("LoadedAmmo"), "第二行耐久不带弹仓");
        Assert.That(Web("wwwroot", "css", "cards.css"), Does.Contain(".card.item.k2i .k2-l2 {"));
    }

    /// <summary>Q357-A01：普通常驻敌踪不挂危险；精英常驻敌踪与可选强敌挂；固定战与待处理敌踪覆盖层沿 Q345 挂危险。</summary>
    [Test]
    public void Q357_A01_DangerOnlyEliteAndFixed()
    {
        var strip = Web("Components", "LocationStrip.razor");
        Assert.That(strip, Does.Contain("CssClass=\"k2-enemy\" Title=\"@ec.Name\" Badge=\"@(w.Hostile ? \"敌对\" : \"中立\")\""));
        Assert.That(strip, Does.Contain("Danger=\"@ec.Elite\" Sub=\"@($\"健康 {Ui.N(w.Hp)}/{Ui.N(ec.Hp)}\")\""));
        Assert.That(strip, Does.Not.Contain("Danger=\"@w.Hostile\""));
        Assert.That(strip, Does.Contain("<ObjectCard Ink2=\"true\" Danger=\"true\" @key=\"@(\"elite-\" + cid)\""), "可选强敌挂危险");
        Assert.That(strip, Does.Contain("<ObjectCard Ink2=\"true\" Danger=\"true\" @key=\"@(\"fixed-\" + here)\""), "固定战沿 Q345");
        Assert.That(Web("Components", "BattleOverlay.razor"), Does.Contain("Danger=\"true\""), "待处理敌踪覆盖层沿 Q345");
        Assert.That(Cfg.Enemies.Any(x => x.Elite) && Cfg.Enemies.Any(x => !x.Elite), Is.True, "配置里精英与普通敌人都有");
    }

    /// <summary>Q358-A01：固定战卡副标按锁定名单（fixedHere）顺序以「、」列敌人名称。</summary>
    [Test]
    public void Q358_A01_FixedBattleNamesInRosterOrder()
    {
        var strip = Web("Components", "LocationStrip.razor");
        Assert.That(strip, Does.Contain("var names = string.Join(\"、\", fixedHere.Select(w => e.Enemy(w.EnemyId).Name));"));
        Assert.That(strip, Does.Contain("<SubContent><div class=\"oc-line\" title=\"@names\">@names</div></SubContent>"));
        Assert.That(strip, Does.Contain("全名单一起入场（从1号位起）"), "接战同一份名单");
    }

    /// <summary>Q359-A01：资源副标单行、原字号，超宽省略号；悬停写完整副标（含日补）。</summary>
    [Test]
    public void Q359_A01_ResourceSubEllipsis()
    {
        var css = Web("wwwroot", "css", "cards.css");
        Assert.That(css, Does.Contain(".ocard.k2 .k2-sub > .oc-line { flex: 0 1 auto; min-width: 0; display: block; font-size: inherit; line-height: inherit; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }"));
        Assert.That(Web("Components", "LocationStrip.razor"), Does.Contain("<div class=\"oc-line\" title=\"@perTip\"><AccountIcon Label=\"@GameEngine.AccountLabel(r)\" />@perLine</div>"));
    }

    /// <summary>Q360-A01：常驻敌踪角标沿真实关系「敌对」「中立」；待处理敌踪覆盖层「敌踪待处理」「留在原地」。</summary>
    [Test]
    public void Q360_A01_EnemyBadges()
    {
        Assert.That(Web("Components", "LocationStrip.razor"), Does.Contain("Badge=\"@(w.Hostile ? \"敌对\" : \"中立\")\""));
        Assert.That(Web("Components", "BattleOverlay.razor"), Does.Contain("Badge=\"@(stays ? \"留在原地\" : \"敌踪待处理\")\""));
    }

    private static string Root()
    {
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "web", "Survival.sln"))) dir = dir.Parent;
        Assert.That(dir, Is.Not.Null, "找到仓库根目录");
        return dir!.FullName;
    }

    private static string Web(params string[] parts) => File.ReadAllText(Path.Combine(new[] { Root(), "web", "Survival.Web" }.Concat(parts).ToArray()));
}
