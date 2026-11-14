using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// BM4b（S14「2026-11-13 B45 Q581—Q590」§Q589；《重墨 V2 开工裁定与界面逐字稿》同日节 Q589 行）：
/// 常态右栏紧凑表头＋次要折叠组（折叠偏好键、批次标题、主信息不进折叠、来源行）。
/// </summary>
public class BM4bRailTests : TestBase
{
    private static string Web(params string[] parts) => File.ReadAllText(Path.Combine(new[] { Root(), "web", "Survival.Web" }.Concat(parts).ToArray()));

    private static string Root()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d != null && !File.Exists(Path.Combine(d.FullName, "web", "Survival.sln"))) d = d.Parent;
        return d!.FullName;
    }

    // ───────────── Q589 逐字 ─────────────

    /// <summary>Q589 逐字稿三行：只读批次标题「批次」；次要折叠标题九个；可访问名称「展开{分组名}」「收起{分组名}」；有数量时「{分组名}（{数量}）」。</summary>
    [Test]
    public void Q589_Verbatim()
    {
        Assert.That(RailText.Batch, Is.EqualTo("批次"));
        Assert.That(RailText.AllGroups, Is.EqualTo(new[] { "说明", "批次", "内容", "加工队列", "成员", "装备明细", "熟练度", "词条", "计算明细" }));
        Assert.That(RailText.Expand("批次"), Is.EqualTo("展开批次"));
        Assert.That(RailText.Collapse("说明"), Is.EqualTo("收起说明"));
        Assert.That(RailText.Title("批次", 3), Is.EqualTo("批次（3）"));
        Assert.That(RailText.Title("说明", null), Is.EqualTo("说明"));
    }

    // ───────────── Q589 折叠偏好 ─────────────

    /// <summary>Q589-A02：新次要分组没有明确偏好时默认收起；已有明确保存的装备／熟练度／词条偏好沿原键恢复，旧“默认展开”不当偏好写回。</summary>
    [Test]
    public void Q589_A02_FoldDefaultsAndLegacyKeys()
    {
        const string P = UiFoldPrefs.DefaultProfile;
        const string A = UiFoldPrefs.MainActor;
        var p = new UiFoldPrefs();
        foreach (var g in new[] { UiFoldPrefs.Equip, UiFoldPrefs.Skills, UiFoldPrefs.Traits, UiFoldPrefs.Party })
            Assert.That(p.IsOpen(P, A, g, UiFoldPrefs.RailDefaultOpen), Is.False, $"{g} 无记录时收起");
        Assert.That(p.Count, Is.Zero, "读默认值不写回");
        // 旧版本玩家明确点过的记录（原键 slot1|player|skills 等）原样恢复
        var legacy = UiFoldPrefs.Parse($"{{\"{UiFoldPrefs.KeyOf(P, A, UiFoldPrefs.Skills)}\":true,\"{UiFoldPrefs.KeyOf(P, A, UiFoldPrefs.Equip)}\":false}}");
        Assert.That(legacy.IsOpen(P, A, UiFoldPrefs.Skills, UiFoldPrefs.RailDefaultOpen), Is.True, "明确展开的熟练度保持展开");
        Assert.That(legacy.IsOpen(P, A, UiFoldPrefs.Equip, UiFoldPrefs.RailDefaultOpen), Is.False);
        Assert.That(legacy.IsOpen(P, A, UiFoldPrefs.Traits, UiFoldPrefs.RailDefaultOpen), Is.False, "没点过的词条按新默认收起");
        // 任务框仍沿原默认（展开）
        Assert.That(new UiFoldPrefs().IsOpen(P, A, "task_corner"), Is.True);
    }

    /// <summary>Q589：对象页折叠记忆按 档案×对象类型×分组 保存，人物另按角色区分；不含对象实例号，切对象不改别类偏好。</summary>
    [Test]
    public void Q589_A02_ObjectFoldKeyByTypeAndGroup()
    {
        const string P = UiFoldPrefs.DefaultProfile;
        var p = new UiFoldPrefs();
        var supply = UiFoldPrefs.ObjectActor(RailTypes.Supply);
        Assert.That(p.Toggle(P, supply, RailGroups.Batch, UiFoldPrefs.RailDefaultOpen), Is.True, "从收起点开");
        Assert.That(UiFoldPrefs.KeyOf(P, supply, RailGroups.Batch), Is.EqualTo("slot1|obj:supply|batch"));
        Assert.That(p.IsOpen(P, UiFoldPrefs.ObjectActor(RailTypes.Gear), RailGroups.Batch, UiFoldPrefs.RailDefaultOpen), Is.False, "另一类对象不受影响");
        Assert.That(p.IsOpen(P, supply, RailGroups.Desc, UiFoldPrefs.RailDefaultOpen), Is.False, "同类另一组不受影响");
        Assert.That(p.IsOpen(P, UiFoldPrefs.MainActor, RailGroups.Batch, UiFoldPrefs.RailDefaultOpen), Is.False, "人物页按角色另记");
        var back = UiFoldPrefs.Parse(p.Serialize());
        Assert.That(back.IsOpen(P, supply, RailGroups.Batch, UiFoldPrefs.RailDefaultOpen), Is.True, "刷新／读档后恢复（浏览器本地，不进存档）");
    }

    /// <summary>折叠组件：标题按「{分组名}（{数量}）」、可访问名称逐字、对象页走类型键、人物页走角色键；会话侧默认收起、写本地偏好、输入锁不处理。</summary>
    [Test]
    public void Q589_RailFoldWiring()
    {
        var fold = Web("Components", "RailFold.razor");
        Assert.That(fold, Does.Contain("aria-label=\"@(open ? RailText.Collapse(Name) : RailText.Expand(Name))\""));
        Assert.That(fold, Does.Contain("var title = RailText.Title(Name, Count);"));
        Assert.That(fold, Does.Contain("G.IsObjectFoldOpen(Type, Group) : G.IsRailFoldOpen(Group, Actor)"));
        var session = Web("Services", "GameSession.cs");
        Assert.That(session, Does.Contain("Fold.IsOpen(UiFoldPrefs.DefaultProfile, actorId, group, UiFoldPrefs.RailDefaultOpen)"));
        Assert.That(session, Does.Contain("Fold.IsOpen(UiFoldPrefs.DefaultProfile, UiFoldPrefs.ObjectActor(objectType), group, UiFoldPrefs.RailDefaultOpen)"));
        Assert.That(session, Does.Contain("if (FoldLocked) return;\n        Fold.Toggle(UiFoldPrefs.DefaultProfile, actorId, group, UiFoldPrefs.RailDefaultOpen);").Or.Contain("if (FoldLocked) return;\r\n        Fold.Toggle(UiFoldPrefs.DefaultProfile, actorId, group, UiFoldPrefs.RailDefaultOpen);"));
        var ch = Web("Components", "CharacterPanel.razor");
        foreach (var k in new[] { "UiFoldPrefs.Equip\" Name=\"@RailText.EquipDetail\"", "UiFoldPrefs.Skills\" Name=\"@RailText.Skills\"", "UiFoldPrefs.Traits\" Name=\"@RailText.Traits\"", "UiFoldPrefs.Party\" Name=\"@RailText.Members\"" })
            Assert.That(ch, Does.Contain("<RailFold Group=\"@" + k), k);
        Assert.That(ch, Does.Not.Contain("G.IsFoldOpen("), "人物页次要组不再按旧默认展开");
    }

    // ───────────── Q589 首屏主信息与批次标题 ─────────────

    /// <summary>Q589-A02：批次标题统一「批次」，原括号里的点选、食用、转移承诺不再出现；明细在批次组里仍可完整阅读（只读不可点）。</summary>
    [Test]
    public void Q589_A02_BatchTitleNoPromise()
    {
        foreach (var f in new[] { "ItemDetail.razor", "GearPeekDetail.razor" })
        {
            var d = Web("Components", f);
            Assert.That(d, Does.Not.Contain("批次（默认"), f);
            Assert.That(d, Does.Not.Contain("点选后"), f);
            Assert.That(d, Does.Contain("RailText.Batch"), f);
        }
        var item = Web("Components", "ItemDetail.razor");
        Assert.That(item, Does.Contain("<RailFold Type=\"@railType\" Group=\"@RailGroups.Batch\" Name=\"@RailText.Batch\" Count=\"@batches.Count\">"));
        Assert.That(item, Does.Contain("<div class=\"batch readonly @(b == chosen ? \"on\" : \"\")\">@batchText</div>"), "只读批次不可点");
    }

    /// <summary>Q589-A01：主要信息（身份、即时效果、重量、耐久、到期／预留、风险与不可用原因）在第一个折叠组之前，不随折叠藏掉；空组不渲染。</summary>
    [Test]
    public void Q589_A01_MainInfoBeforeFolds()
    {
        var item = Web("Components", "ItemDetail.razor");
        var firstFold = item.IndexOf("<RailFold", StringComparison.Ordinal);
        foreach (var main in new[] { "<RailHead Source=\"@e.RailSourceLine(s, inst)\">", "<span>即时效果</span>", "<span>合计重量</span>", "<span>耐久</span>", "<span>@RailText.Batch</span>",
                                     "<div class=\"blocked\">@p.Reason</div>", "已腐败，不能食用或用于配方。", "该批次正被当前行动预留", "行动占用：这口锅正被当前行动使用", "<span>水量</span>", "<span>处理进度</span>" })
        {
            var at = item.IndexOf(main, StringComparison.Ordinal);
            Assert.That(at, Is.GreaterThan(0).And.LessThan(firstFold), main);
        }
        Assert.That(item, Does.Contain("@if (batches.Count > 1)"), "只有一批时不造批次组（首屏已有该批）");
        Assert.That(item, Does.Contain("@if (affixLines.Count > 0)"), "无词条不造词条组");
        Assert.That(item, Does.Contain("@if (potLiquids.Count + potFoods.Count > 0)"), "空锅不造内容组");
        Assert.That(item, Does.Not.Contain("<div class=\"detail-icon\">"), "大图改为表头旁缩略图");

        var fire = Web("Components", "FireDetail.razor");
        var ff = fire.IndexOf("<RailFold", StringComparison.Ordinal);
        foreach (var main in new[] { "<span>剩余燃料</span>", "<span>本段受热</span>", "CardStates.FireSlotName(k)", "@G.RightNotice" })
            Assert.That(fire.IndexOf(main, StringComparison.Ordinal), Is.GreaterThan(0).And.LessThan(ff), main);
        Assert.That(fire, Does.Contain("@if (crumbs.Count > 1) { <FireCrumbs Path=\"@crumbs\" /> }"), "根层不在名称上方重复同名路径");

        var enemy = Web("Components", "EnemyDetail.razor");
        var ef = enemy.IndexOf("<RailFold", StringComparison.Ordinal);
        // BM5b1（S14／R05 Q639，取代 Q589／Q613 敌踪首屏）：主区只身份、风险与当前成员重要属性；开战武器、感染／空弹仓、成员类别与其他成员归「战斗详情」（第一个折叠组）
        Assert.That(enemy.IndexOf("@CoreLines(card, members[0])", StringComparison.Ordinal), Is.GreaterThan(0).And.LessThan(ef), "重要属性在主区");
        Assert.That(enemy.Substring(ef), Does.StartWith("<RailFold Type=\"@RailTypes.Enemy\" Group=\"@RailGroups.BattleDetail\" Name=\"@RailText.BattleDetail\">"));
        var battleEnd = enemy.IndexOf("</RailFold>", ef, StringComparison.Ordinal);
        foreach (var inFold in new[] { "<span>开战武器</span>", "单次咬伤感染概率", "弹仓为0", "@MemberLines(members[0], false)", "@foreach (var m in members.Skip(1))" })
            Assert.That(enemy.IndexOf(inFold, StringComparison.Ordinal), Is.GreaterThan(ef).And.LessThan(battleEnd), inFold);
        Assert.That(enemy, Does.Not.Contain("RailGroups.Members"), "原「成员」组并入战斗详情");
        Assert.That(enemy, Does.Not.Contain("@if (why != null)"), "受阻原因在卡面与中央按钮，右栏主区不重复");

        var pv = Web("Components", "PreviewView.razor");
        var pf = pv.IndexOf("<RailFold", StringComparison.Ordinal);
        foreach (var main in new[] { "<span>预计耗时</span>", "<span>预计完成</span>", "<div class=\"blocked\">@p.Reason</div>", "p.HazardProbability > 0", "@foreach (var w in p.Warnings)" })
            Assert.That(pv.IndexOf(main, StringComparison.Ordinal), Is.GreaterThan(0).And.LessThan(pf), main);
        Assert.That(pv, Does.Contain("<RailFold Type=\"@RailType\" Group=\"@RailGroups.Calc\" Name=\"@RailText.Calc\">"));
    }

    /// <summary>Q589：来源行「{真实地点}[ · {区域}] · {真实持有人或来源}」，无安全屋分区时省区域，不写内部编号。</summary>
    [Test]
    public void Q589_SourceLine()
    {
        var s = New();
        var carried = s.Items.First(i => i.IsCarried);
        Assert.That(E.RailSourceLine(s, carried), Is.EqualTo($"{E.LocName(s.CurrentLocation)} · 随身"));
        var ground = s.Items.First(i => i.Owner == s.Ground);
        Assert.That(E.RailSourceLine(s, ground), Is.EqualTo($"{E.LocName(s.CurrentLocation)} · 当前地面"));
        Assert.That(E.RailSourceLine(s, ground), Does.Not.Contain("#").And.Not.Contain(ground.InstanceId.ToString() + " "));
        Assert.That(Web("Components", "GearPeekDetail.razor"), Does.Contain("<RailHead Source=\"@e.RailSourceLine(s, it)\">"));
    }

    /// <summary>Q589：右栏样式只作用于主界面右栏，不碰全屏战斗右列；「对象详情」只留给读屏；样式表在 cards.css 之后加载。</summary>
    [Test]
    public void Q589_RailCssScoped()
    {
        var css = Web("wwwroot", "css", "rail.css");
        foreach (var line in css.Split('\n').Where(l => l.Contains('{') && !l.TrimStart().StartsWith("@") && !l.TrimStart().StartsWith("/*")))
            Assert.That(line.TrimStart(), Does.StartWith("[data-detail-rail]"), line);
        Assert.That(css, Does.Not.Contain("bfs-info"));
        Assert.That(css, Does.Contain("[data-detail-rail] .sr-title { position: absolute; width: 1px; height: 1px;"));
        var html = Web("wwwroot", "index.html");
        Assert.That(html.IndexOf("css/rail.css", StringComparison.Ordinal), Is.GreaterThan(html.IndexOf("css/cards.css", StringComparison.Ordinal)));
        Assert.That(Web("Components", "DetailPanel.razor"), Does.Contain("data-detail-rail"));
    }

    // ───────────── 试玩修复（M834） ─────────────

    /// <summary>试玩修复 1、2、3：敌踪表头下有卡面同源接近状态；资源来源行带卡面同源可用量；路线来源行写当前地点而不重复「前往{目的地}」。</summary>
    [Test]
    public void Fix_EnemyStatus_ResourceStock_RouteSource()
    {
        var enemy = Web("Components", "EnemyDetail.razor");
        // BM5b1（R05 Q639）：卡面同源的无时长状态句移到「说明」组（右栏风险与属性之间不另铺一段）
        Assert.That(enemy, Does.Contain("@if (status != null) { <li class=\"ecard-state\" data-approach>@status</li> }"));
        Assert.That(enemy.IndexOf("<li class=\"ecard-state\"", StringComparison.Ordinal), Is.GreaterThan(enemy.IndexOf("Group=\"@RailGroups.Desc\"", StringComparison.Ordinal)));
        var sel = Web("Components", "SelectionDetail.razor");
        Assert.That(sel, Does.Contain("var rStock = e.ResourceCardStock(s, r).Badge;"));
        Assert.That(sel, Does.Not.Contain("Eyebrow=\"@($\"{mark}前往{e.LocName(sel.Id)}\")\""), "路线来源行不重复标题");
        Assert.That(sel, Does.Contain("Eyebrow=\"@($\"{mark}{e.LocName(s.CurrentLocation)}\")\" Title=\"@($\"前往{e.LocName(sel.Id)}\")\""));
        // 资源首屏：产出与风险排在身体变化之前
        var pv = Web("Components", "PreviewView.razor");
        var outputs = pv.IndexOf("@if (p.Ok && p.Outputs.Count > 0)", StringComparison.Ordinal);
        var risk = pv.IndexOf("无随机伤害风险。", StringComparison.Ordinal);
        var body = pv.IndexOf("@foreach (var id in Stats.All)", StringComparison.Ordinal);
        Assert.That(outputs, Is.GreaterThan(0).And.LessThan(risk));
        Assert.That(risk, Is.LessThan(body));
    }

    /// <summary>试玩修复 4、5、6：同伴详情用标签列；没有词条时不显示词条组；锅主区总有水量／容量行；成员组内不重复队伍人数。</summary>
    [Test]
    public void Fix_MemberLabels_EmptyTraits_PotWater_PartyHead()
    {
        var m = Web("Components", "MemberPeekDetail.razor");
        Assert.That(m, Does.Contain("<div class=\"lines rail-main member-lines\">"));
        Assert.That(m, Does.Contain("<div><span>速度</span>"));
        Assert.That(m, Does.Not.Contain("健@(Fmt.T(st.Health))"), "不再用无标签串");
        var c = Web("Components", "CharacterPanel.razor");
        Assert.That(c, Does.Contain("@if (s.Traits.Count > 0)"));
        Assert.That(c, Does.Not.Contain("还没有词条。"));
        Assert.That(c, Does.Contain("<PartyPanel InFold=\"true\" />"));
        Assert.That(Web("Components", "PartyPanel.razor"), Does.Contain("@if (!InFold) { <b>队伍 @s.Party.Count/@cap</b> }"));
        foreach (var f in new[] { "ItemDetail.razor", "GearPeekDetail.razor" })
            Assert.That(Web("Components", f), Does.Contain("?? $\"空锅 0/{pc.CapacityMl}毫升\")</b></div>"), f);
    }

    /// <summary>顺手修：任务角 aria-expanded 写 true/false；火堆工位摘要数值连同单位不断行；对白读完后清掉残留的「请先读完对话」反馈。</summary>
    [Test]
    public void Fix_AriaExpanded_UnitNoWrap_DialogueNotice()
    {
        var tc = Web("Components", "TaskCorner.razor");
        Assert.That(tc, Does.Not.Contain("aria-expanded=\"@open\""));
        Assert.That(tc, Does.Contain("aria-expanded=\"@(open ? \"true\" : \"false\")\""));
        Assert.That(Web("Components", "FireDetail.razor"), Does.Contain("@Ui.KeepUnits(it == null ? \"空位\""));
        Assert.That(Web("wwwroot", "css", "rail.css"), Does.Contain("[data-detail-rail] .nb { white-space: nowrap; }"));
        var gs = Web("Services", "GameSession.cs");
        Assert.That(gs, Does.Contain("_notice.EndsWith(GameEngine.DialogueLockedReason, StringComparison.Ordinal) && !DialogueLocked"));
        Assert.That(gs, Does.Contain("ClearEndedDialogueNotice();"));
    }

    /// <summary>M835：数值连单位不断行在纯文本上切分——含「·」的工位摘要不再把实体里的数字包进 span；火源「内容」组工位头同样不断行；同伴详情有小缩略图。</summary>
    [Test]
    public void Fix835_UnitRunsPlainText_SlotHead_MemberThumb()
    {
        var html = UnitRuns.Html("金属锅 · 净水100%·1000/1000毫升 · 加热20分钟");
        Assert.That(html, Is.EqualTo("金属锅 &#183; 净水<span class=\"nb\">100%</span>&#183;<span class=\"nb\">1000/1000毫升</span> &#183; 加热<span class=\"nb\">20分钟</span>"));
        Assert.That(html, Does.Not.Contain("&amp;"));
        Assert.That(UnitRuns.Html("空位"), Is.EqualTo("空位"));
        Assert.That(UnitRuns.Html("<b>&"), Is.EqualTo("&lt;b&gt;&amp;"), "其余文字照常转义");
        var fire = Web("Components", "FireDetail.razor");
        Assert.That(fire, Does.Contain("<span class=\"muted\">@Ui.KeepUnits(SlotState(it))</span>"));
        Assert.That(Web("Services", "Ui.cs"), Does.Contain("new(UnitRuns.Html(text))"));
        Assert.That(Web("wwwroot", "css", "rail.css"), Does.Contain("[data-detail-rail] .fire-detail .fs-head > span { flex: 1 1 auto; overflow: visible; text-overflow: clip; }"), "工位头状态句不截成省略号");
        var m = Web("Components", "MemberPeekDetail.razor");
        Assert.That(m, Does.Contain("<Thumb><img src=\"@(isMe ? \"img/portraits/protagonist.svg\" : \"img/portraits/unknown.svg\")\""));
        Assert.That(Web("wwwroot", "css", "rail.css"), Does.Contain("[data-detail-rail] .rail-id h2 small { font-size: var(--fs-aux); margin-left: 6px; }"));
    }
}
