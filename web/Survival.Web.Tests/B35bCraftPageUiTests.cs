using Bunit;
using Survival.Core.Engine;
using Survival.Core.State;
using Survival.Web.Components;
using Survival.Web.Services;

namespace Survival.Web.Tests;

/// <summary>
/// B35b「UI 重做：制作页三列与来源侧页」网页层行为（开发记录 doc/开发记录/B35b 制作页三列与来源侧页.md）：
/// 真正渲染制作窗口（CraftOverlay）、来源侧页与地图（MapOverlay），点按钮，断玩家看到的文字、属性与状态。
/// 出处：05 P06／F08、逐字稿 P06a／P06b 与 B35 表；S14 2026-11-13 B35 Q592 A、Q593 A，2026-11-14 B35 次要 Q597 A、Q598、Q599 B，
/// 2026-11-17 B35 次要 Q911 A，2026-11-18 B35b Q947 A、Q949 A（合同 assets/20261118/b35b-q946-q949-craft-ui.json）。
/// </summary>
public class B35bCraftPageUiTests : WebTestBase
{
    private void Ok(ActionResult r) => Assert.That(r.Ok, Is.True, r.Reason);

    /// <summary>石刀（石头1、纤维1，基础 30 分钟）：清掉随身与地面的石头、纤维，再按需放到营地地面。</summary>
    private void KnifeMats(int stone, int fiber)
    {
        S.Items.RemoveAll(i => i.ItemId is "stone" or "fiber");
        if (stone > 0) E.CreateItems(S, "stone", stone, S.Ground);
        if (fiber > 0) E.CreateItems(S, "fiber", fiber, S.Ground);
    }

    /// <summary>荒地（field，开局已知）的纤维丛设为已发现：石刀缺纤维时有一条“可获取”且有已知地点的来源。</summary>
    private void DiscoverFieldFiber() => S.Resources["field_fiber"].Discovered = true;

    private IRenderedComponent<CraftOverlay> OpenKnife()
    {
        G.OpenHandCraft("knife");
        Assert.That(G.Panel, Is.EqualTo("craft"));
        Assert.That(G.CraftRecipe, Is.EqualTo("knife"));
        return Ctx.RenderComponent<CraftOverlay>();
    }

    /// <summary>地图组件渲染后量视口（survivalMap.size 等）；bUnit 宽松模式下数组返回 null，这里给固定视口。</summary>
    private IRenderedComponent<MapOverlay> RenderMap()
    {
        Ctx.JSInterop.Setup<double[]>("survivalMap.size", _ => true).SetResult(new double[] { 900, 600 });
        Ctx.JSInterop.Setup<double[]>("survivalMap.getCenter", _ => true).SetResult(new double[] { 450, 300 });
        return Ctx.RenderComponent<MapOverlay>();
    }

    private static bool HasEsc(AngleSharp.Dom.IElement el) => el.QuerySelector("kbd.kc") != null;

    // ───────────── R72／Q593／Q597：三列与“本次制作” ─────────────

    /// <summary>
    /// Q593-A01、Q597-A01：内右列“本次制作”有「耗时」「产物」、预计完成与唯一开工按钮；石刀白天倍率 1、基础 30 分钟——
    /// 耗时“30分钟”、产物“石刀×1”、按钮“开始制作（30分钟）”；份数控件在“本次制作”区，不在中列配方卡里（Q597：份数移进本次制作）。
    /// </summary>
    [Test]
    public void RunArea_TimeOutputButton_BatchMovedIn()
    {
        KnifeMats(1, 1);
        var cut = OpenKnife();
        var run = cut.Find("[data-pt=craft-run]");
        Assert.That(run.QuerySelector(".cr-title")!.TextContent, Is.EqualTo("本次制作"));
        Assert.That(run.QuerySelector("[data-pt=craft-run-time] dt")!.TextContent, Is.EqualTo("耗时"));
        Assert.That(run.QuerySelector("[data-pt=craft-run-time] dd")!.TextContent.Trim(), Is.EqualTo("30分钟"));
        Assert.That(run.QuerySelector("[data-pt=craft-run-output] dt")!.TextContent, Is.EqualTo("产物"));
        Assert.That(run.QuerySelector("[data-pt=craft-run-output] dd")!.TextContent, Is.EqualTo("石刀×1"));
        Assert.That(run.QuerySelector("[data-pt=craft-run-eta]")!.TextContent, Does.StartWith("预计08:30完成"));
        var start = run.QuerySelector("[data-pt=craft-start]")!;
        Assert.That(start.HasAttribute("disabled"), Is.False);
        Assert.That(start.TextContent, Does.StartWith("开始制作（30分钟）"));
        Assert.That(cut.FindAll("[data-pt=craft-run] [data-pt=craft-batch]"), Has.Count.EqualTo(1), "份数在本次制作区");
        Assert.That(cut.FindAll(".craft-mats [data-pt=craft-batch]"), Is.Empty, "中列配方卡不再放份数");
        Assert.That(cut.FindAll("[data-pt=craft-start]"), Has.Count.EqualTo(1), "唯一开工按钮");

        // 份数 2：按钮跟着同源预估变（石刀 2 份 60 分钟），不再拼“×2份”
        E.CreateItems(S, "stone", 1, S.Ground);
        E.CreateItems(S, "fiber", 1, S.Ground);
        cut.Render();
        cut.Find("[data-pt=craft-batch] button[aria-label='增加份数']").Click();
        Assert.That(cut.Find("[data-pt=craft-start]").TextContent, Does.StartWith("开始制作（1小时）").And.Not.Contain("×2份"));
        Assert.That(cut.Find("[data-pt=craft-run-output] dd").TextContent, Is.EqualTo("石刀×2"));
    }

    /// <summary>
    /// B35B-Q947-01：缺纤维不能开工——按钮置灰、只写“开始制作”，原具体原因在按钮旁（data-pt=craft-why）；没有耗时行（无预估数据省略）。
    /// </summary>
    [Test]
    public void CannotStart_VerbOnly_ReasonBeside()
    {
        KnifeMats(1, 0);
        var cut = OpenKnife();
        var start = cut.Find("[data-pt=craft-start]");
        Assert.That(start.HasAttribute("disabled"), Is.True);
        Assert.That(start.TextContent.Trim(), Is.EqualTo("开始制作"));
        Assert.That(cut.Find("[data-pt=craft-why]").TextContent, Does.Contain("纤维"));
        Assert.That(cut.FindAll("[data-pt=craft-run-time]"), Is.Empty);
        Assert.That(cut.Find("[data-pt=craft-run-output] dd").TextContent, Is.EqualTo("石刀×1"), "配方产物照列");
    }

    /// <summary>
    /// Q598-A01／A02：材料主数量“{可选}/{所需}”——石头 3（不封顶）写“3/1”，纤维全无“0/1”；悬停与读屏“可选3，需要1”；
    /// 第二行不再写“需／缺少”，保留现有（随身／地面）明细；缺料行整行标出。材料表标题“所需材料”（逐字稿 P06a）。
    /// </summary>
    [Test]
    public void Materials_RatioAvailableOverNeed()
    {
        KnifeMats(3, 0);
        var cut = OpenKnife();
        Assert.That(cut.Find("[data-pt=craft-mats-head]").TextContent, Is.EqualTo("所需材料"));
        var rows = cut.FindAll(".mat-row");
        Assert.That(rows, Has.Count.EqualTo(2));
        var stone = rows.Single(r => r.GetAttribute("data-drop") == "slot:knife:stone");
        var qty = stone.QuerySelector(".ir-qty")!;
        Assert.That(qty.TextContent, Is.EqualTo("3/1"));
        Assert.That(qty.GetAttribute("title"), Is.EqualTo("可选3，需要1"));
        Assert.That(qty.GetAttribute("aria-label"), Is.EqualTo("可选3，需要1"));
        Assert.That(stone.QuerySelector(".ir-status")!.TextContent, Is.EqualTo("现有3（随身0／地面3）"));
        Assert.That(stone.ClassList, Does.Not.Contain("missing"));
        var fiber = rows.Single(r => r.GetAttribute("data-drop") == "slot:knife:fiber");
        Assert.That(fiber.QuerySelector(".ir-qty")!.TextContent, Is.EqualTo("0/1"));
        Assert.That(fiber.ClassList, Does.Contain("missing"));
        Assert.That(fiber.QuerySelector(".ir-status")!.TextContent, Does.Not.Contain("缺少").And.Not.Contain("需"));
    }

    // ───────────── R73：来源侧页 ─────────────

    /// <summary>
    /// Q911 A、Q599 B、Q593 A：缺纤维点“查看来源”——制作窗口内开材料来源侧页（不开地图），本次制作区隐藏；
    /// 来源分“可获取”“其他已知来源”两组（概率来源在后一组），默认选第一个可获取且有已知地点的“荒地 · 荒地纤维丛”，内右列显示它并有“在地图上定位”；
    /// 有已知地点的行带地点悬停（右栏只读地点详情），概率来源不带。
    /// </summary>
    [Test]
    public void Sources_DefaultPick_Groups_RunHidden()
    {
        KnifeMats(1, 0);
        DiscoverFieldFiber();
        var cut = OpenKnife();
        cut.Find("[data-source-item=fiber]").Click();

        Assert.That(G.Panel, Is.EqualTo("craft"), "不开地图");
        Assert.That(cut.FindAll("[data-pt=craft-run]"), Is.Empty, "看来源时本次制作区隐藏");
        Assert.That(cut.FindAll("[data-pt=craft-start]"), Is.Empty, "不能在看来源时误开工");
        var pane = cut.Find("[data-pt=craft-sources]");
        Assert.That(pane.QuerySelector("h3")!.TextContent, Is.EqualTo("材料来源"));
        Assert.That(pane.QuerySelector(".cs-sub")!.TextContent, Is.EqualTo("已知来源"));
        Assert.That(cut.Find("[data-pt=src-group-usable]").TextContent, Is.EqualTo("可获取"));
        Assert.That(cut.Find("[data-pt=src-group-other]").TextContent, Is.EqualTo("其他已知来源"));
        var rows = cut.FindAll("[data-pt=src-row]");
        Assert.That(rows, Has.Count.EqualTo(2));
        Assert.That(rows[0].TextContent, Does.Contain("荒地纤维丛"));
        Assert.That(rows[0].GetAttribute("aria-pressed"), Is.EqualTo("true"), "Q911：默认选首个可获取且有已知地点");
        Assert.That(rows[0].GetAttribute("data-peek"), Is.EqualTo(Peek.Node("field")), "Q599：有已知地点的行悬停看地点详情");
        Assert.That(rows[1].GetAttribute("data-source-kind"), Is.EqualTo("drop"));
        Assert.That(rows[1].GetAttribute("aria-pressed"), Is.EqualTo("false"));
        Assert.That(rows[1].HasAttribute("data-peek"), Is.False, "无地点不造地点详情");
        Assert.That(cut.Find("[data-pt=src-detail-title]").TextContent, Does.Contain("荒地纤维丛"));
        Assert.That(cut.Find("[data-pt=src-locate]").TextContent, Is.EqualTo("在地图上定位"));
        Assert.That(cut.Find("[data-pt=src-back]").TextContent, Does.StartWith("返回配方"));
    }

    /// <summary>
    /// Q911 A 配对：没有“可获取且有已知地点”的来源（只有概率来源）时不预选、内右列留空（无定位）；手选概率来源行可读其详情，但无地点不给定位。
    /// </summary>
    [Test]
    public void Sources_NoLocatableUsable_NoDefault_ThenManualPickNoLocate()
    {
        KnifeMats(1, 0);
        var cut = OpenKnife();
        cut.Find("[data-source-item=fiber]").Click();
        Assert.That(cut.FindAll("[data-pt=src-group-usable]"), Is.Empty, "空组隐藏");
        Assert.That(cut.FindAll("[data-pt=src-detail-title]"), Is.Empty, "不预选，内右留空");
        Assert.That(cut.FindAll("[data-pt=src-locate]"), Is.Empty);

        cut.Find("[data-pt=src-row]").Click();
        Assert.That(cut.Find("[data-pt=src-detail-title]").TextContent, Does.Contain("概率来源"));
        Assert.That(cut.FindAll("[data-pt=src-locate]"), Is.Empty, "没有已知地点不提供定位");
    }

    /// <summary>
    /// B35B-Q949-01／02：来源侧页开着时左列仍可点——点另一配方一次回到配方视图并改选（本次制作区重新出现，但这次点击不开工、不扣物）；
    /// 再开侧页后点分类同样一次退出；不保留旧来源选择。
    /// </summary>
    [Test]
    public void Sources_LeftListClick_ExitsInOneStep()
    {
        KnifeMats(1, 0);
        DiscoverFieldFiber();
        var cut = OpenKnife();
        cut.Find("[data-source-item=fiber]").Click();
        Assert.That(G.CraftSourcesOpen, Is.True);
        var before = S.Items.Sum(i => i.Count);
        var minute = S.GameMinute;

        cut.Find("[data-recipe=axe]").Click();
        Assert.That(G.CraftSourcesOpen, Is.False);
        Assert.That(G.CraftSourceKey, Is.Null, "不保留旧来源选择");
        Assert.That(G.CraftRecipe, Is.EqualTo("axe"));
        Assert.That(cut.FindAll("[data-pt=craft-run]"), Has.Count.EqualTo(1));
        Assert.That(S.Main, Is.Null, "导航不开工");
        Assert.That((S.Items.Sum(i => i.Count), S.GameMinute), Is.EqualTo((before, minute)), "不扣物、不推进时间");

        G.SelectCraftRecipe("knife");
        cut.Render();
        cut.Find("[data-source-item=fiber]").Click();
        Assert.That(G.CraftSourcesOpen, Is.True);
        cut.Find($"[data-tab={GameEngine.HandCraftDefaultTab}]").Click();
        Assert.That(G.CraftSourcesOpen, Is.False, "改分类同样一次退出");
        Assert.That(G.Panel, Is.EqualTo("craft"));
    }

    /// <summary>
    /// B35B-Q949-03、Q592-A01：侧页开着时 ESC 归侧页“返回配方”（键帽在它上面、不在标题栏关闭键上）；一次 ESC 只回配方、不关窗口；再一次才关窗口。
    /// </summary>
    [Test]
    public void Sources_EscCapMovesToBackToRecipe()
    {
        KnifeMats(1, 0);
        var cut = OpenKnife();
        Assert.That(HasEsc(cut.Find("[data-craft-close]")), Is.True, "配方视图：关闭键承接 ESC");
        cut.Find("[data-source-item=fiber]").Click();
        Assert.That(G.EscOwner, Is.EqualTo(EscOwner.Panel));
        Assert.That(HasEsc(cut.Find("[data-pt=src-back]")), Is.True, "侧页：返回配方承接 ESC");
        Assert.That(HasEsc(cut.Find("[data-craft-close]")), Is.False);

        G.PanelEscape();   // ESC（GameScreen 对 EscOwner.Panel 调同一入口）
        cut.Render();
        Assert.That(G.Panel, Is.EqualTo("craft"), "不连退两层");
        Assert.That(G.CraftSourcesOpen, Is.False);
        Assert.That(cut.FindAll("[data-pt=craft-run]"), Has.Count.EqualTo(1));
        Assert.That(HasEsc(cut.Find("[data-craft-close]")), Is.True);

        G.PanelEscape();
        Assert.That(G.Panel, Is.Null);
    }

    /// <summary>
    /// Q592-A01：侧页“在地图上定位”——地图盖在制作窗口上，只高亮荒地（不设为所选目的地、不移动）、地图上不再浮来源列表，
    /// 关闭键写“返回材料来源”；点它先回侧页并保留所选来源，再点“返回配方”回配方。
    /// </summary>
    [Test]
    public void Sources_LocateOnMap_BackToSourcesKeepsPick()
    {
        KnifeMats(1, 0);
        DiscoverFieldFiber();
        var cut = OpenKnife();
        cut.Find("[data-source-item=fiber]").Click();
        var key = G.CraftSourceKey;
        Assert.That(key, Is.Not.Null);
        var here = S.CurrentLocation;

        cut.Find("[data-pt=src-locate]").Click();
        Assert.That(G.Panel, Is.EqualTo("map"));
        Assert.That(G.MapSourceSpot, Is.EqualTo("field"));
        Assert.That(G.MapPicked, Is.Null, "只高亮，不选为移动目的地");
        Assert.That(S.CurrentLocation, Is.EqualTo(here));
        var map = RenderMap();
        Assert.That(map.FindAll(".map-sources"), Is.Empty, "地图不再重复浮来源列表");
        Assert.That(map.Find("#mn-field").ClassList, Does.Contain("source-spot"));
        var close = map.Find("[data-pt=map-close]");
        Assert.That(close.TextContent, Does.StartWith("返回材料来源"));

        close.Click();
        Assert.That(G.Panel, Is.EqualTo("craft"));
        cut.Render();
        Assert.That(G.CraftSourcesOpen, Is.True, "先回材料来源侧页");
        Assert.That(G.CraftSourceKey, Is.EqualTo(key), "保留所选来源");
        Assert.That(cut.Find("[data-pt=src-row][aria-pressed=true]").TextContent, Does.Contain("荒地纤维丛"));

        cut.Find("[data-pt=src-back]").Click();
        Assert.That(G.CraftSourcesOpen, Is.False);
        Assert.That(G.Panel, Is.EqualTo("craft"));
        Assert.That(G.CraftRecipe, Is.EqualTo("knife"));
        Assert.That(G.MapSourceSpot, Is.Null, "离开来源链地图即清高亮");
    }

    /// <summary>
    /// B35b 试玩修复 M1157（Q599 B：“悬停有已知地点的来源行时，右栏只读显示该地点详情；悬停不改已选来源、配方／数量草稿或地图移动目标，不开图、不出发”）：
    /// 侧页里悬停“荒地纤维丛”行的地点（与该行 data-peek 同键）——右栏显示地点详情（data-peek-shown=node…）；制作窗口仍在、所选来源与配方不变、不开地图、不移动。
    /// 配对：侧页没列着的地点（营地）、以及回到配方视图后同一地点都不受理（右栏不换）。
    /// </summary>
    [Test]
    public void Sources_HoverLocatableRow_RightBarShowsNodeReadOnly()
    {
        KnifeMats(1, 0);
        DiscoverFieldFiber();
        var cut = OpenKnife();
        cut.Find("[data-source-item=fiber]").Click();
        var key = G.CraftSourceKey;
        var here = S.CurrentLocation;
        var rowPeek = cut.Find("[data-pt=src-row][aria-pressed=true]").GetAttribute("data-peek")!;
        Assert.That(rowPeek, Is.EqualTo(Peek.Node("field")));
        Ctx.JSInterop.Setup<double[]>("survivalMap.size", _ => true).SetResult(new double[] { 900, 600 });
        Ctx.JSInterop.Setup<double[]>("survivalMap.getCenter", _ => true).SetResult(new double[] { 450, 300 });
        var rail = Ctx.RenderComponent<DetailPanel>();

        G.PeekEnter(Peek.Node(here));   // 配对：营地不是侧页列着的地点
        Assert.That(G.PeekKeyNow, Is.Null);

        G.PeekEnter(rowPeek);
        Assert.That(G.PeekKeyNow, Is.EqualTo(rowPeek));
        rail.Render();
        Assert.That(rail.Find("[data-peek-shown]").GetAttribute("data-peek-shown"), Is.EqualTo(rowPeek));
        Assert.That((G.Panel, G.CraftSourcesOpen, G.CraftSourceKey, G.CraftRecipe), Is.EqualTo(("craft", true, key, "knife")), "悬停不换页、不改所选来源与配方");
        Assert.That((G.MapPicked, S.CurrentLocation), Is.EqualTo(((string?)null, here)), "不选目的地、不移动");

        // 配对：回到配方视图后，同一地点不再受理
        G.PeekClear();
        G.CloseCraftSources();
        G.PeekEnter(rowPeek);
        Assert.That(G.PeekKeyNow, Is.Null);
    }

    /// <summary>普通地图（不是来源链）关闭键仍按原返回链写法，不写“返回材料来源”（Q592 配对）。</summary>
    [Test]
    public void PlainMap_CloseLabelUnchanged()
    {
        G.OpenPanel("map");
        var map = RenderMap();
        Assert.That(map.Find("[data-pt=map-close]").TextContent, Does.Not.Contain("返回材料来源"));
        Assert.That(map.FindAll(".source-spot"), Is.Empty);
    }

    // ───────────── Q593：位置句 ─────────────

    /// <summary>
    /// Q593 A（逐字稿 B35 表）：右栏脚注“在‘本次制作’区点‘开始制作（30分钟）’；时间为预计值。”；副标题“配方 · 完整预估在右栏 ⓘ”，
    /// 悬停“在‘本次制作’区点‘开始制作（30分钟）’；右栏显示完整预估。”；不能开工时引用动作名“开始制作”。
    /// </summary>
    [Test]
    public void RightBar_FootNote_PointsToCraftArea()
    {
        KnifeMats(1, 1);
        var cut = OpenKnife();
        var sub = cut.Find("[data-pt=craft-sub]");
        Assert.That(sub.TextContent, Is.EqualTo("配方 · 完整预估在右栏 ⓘ"));
        Assert.That(sub.GetAttribute("title"), Is.EqualTo("在‘本次制作’区点‘开始制作（30分钟）’；右栏显示完整预估。"));
        var detail = Ctx.RenderComponent<CraftOverlay>(p => p.Add(x => x.Part, "detail"));
        Assert.That(detail.Find(".under").TextContent, Is.EqualTo("在‘本次制作’区点‘开始制作（30分钟）’；时间为预计值。"));

        KnifeMats(1, 0);
        G.Notify();
        detail.Render();
        Assert.That(detail.Find(".under").TextContent, Is.EqualTo("在‘本次制作’区点‘开始制作’；时间为预计值。"));
    }

    // ───────────── 陶窑投料按钮 ─────────────

    /// <summary>
    /// Q597、B35B-Q947-03：陶窑工作区投料按钮“投料（{批数}批 · 1小时）”在“本次制作”区；缺黏土受阻时仍照写原固定时长，原因在旁。
    /// </summary>
    [Test]
    public void Station_LoadButton_BatchAndStaticDuration()
    {
        E.DebugBuildAllWorkshops(S);
        S.Skills["crafting"].Level = 3;
        S.Progress.Flags.Add("h03_ceramic_notice_read");
        S.Items.RemoveAll(i => i.ItemId is "clay" or "charcoal" or "ceramic_shell");
        E.CreateItems(S, "clay", 4, S.Ground);
        E.CreateItems(S, "charcoal", 2, S.Ground);
        G.OpenWorkspace(E.WorkspaceForStationHere(S, "kiln")!, "ex_ceramic_shell");
        var cut = Ctx.RenderComponent<CraftOverlay>();
        var load = cut.Find("[data-pt=craft-run] [data-pt=station-load]");
        Assert.That(load.TextContent, Does.StartWith("投料（1批 · 1小时）"));
        Assert.That(load.HasAttribute("disabled"), Is.False);
        cut.Find("[data-station-batches] button[aria-label='增加批数']").Click();
        Assert.That(cut.Find("[data-pt=station-load]").TextContent, Does.StartWith("投料（2批 · 1小时）"));

        S.Items.RemoveAll(i => i.ItemId == "clay");
        G.Notify();
        cut.Render();
        var blocked = cut.Find("[data-pt=station-load]");
        Assert.That(blocked.HasAttribute("disabled"), Is.True);
        Assert.That(blocked.TextContent, Does.StartWith("投料（2批 · 1小时）"), "受阻仍写原静态批数与时长");
        Assert.That(cut.Find("[data-pt=craft-run] [data-pt=craft-why]").TextContent, Is.Not.Empty);
    }
}
