using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 B45「设施加工移至中央次级菜单、右栏只读」网页壳部分（R185、R202、R203）与 FacilityMenuRules 只读规则。
/// 规则出处：S14「2026-11-12 用户直接裁定：设施加工改在中央次级菜单操作」（FACPROC 第 1—5 条、FACPROC-A01—A09）、
/// 「2026-11-12 B45 Q542—Q548」「2026-11-13 B45 Q558—Q562」；正式句见《重墨 V2 开工裁定与界面逐字稿》同名节。
/// 内核规则直接测；网页壳的接线以源码检查（同 B31b—B32g）。
/// </summary>
public class B45FacMenuShellTests : TestBase
{
    private static FacilityState Fac(GameState s) => s.Locations["camp"].Facility;

    // ───────────── Q543／Q561 位置行 ─────────────

    /// <summary>Q543／Q561：无安全屋分区只写地点；在屋内分区写「 · 屋内」。</summary>
    [Test]
    public void Q561_PlaceLine()
    {
        var s = New();
        Assert.That(E.FacMenuPlaceLine(s), Is.EqualTo($"当前位置：{E.LocName("camp")}"), "未建安全屋不加分区");
        s.CurrentZone = GameState.SafehouseZone;
        Assert.That(E.FacMenuPlaceLine(s), Is.EqualTo($"当前位置：{E.LocName("camp")} · 屋内"));
    }

    // ───────────── Q542／Q559／Q560 选择设施 ─────────────

    /// <summary>Q542／Q559：只列已建的陶窑、固定净水台（原工坊顺序），摘要取当前批实际进度，无批写「空闲」；工作台等不列。</summary>
    [Test]
    public void Q559_StationChoices()
    {
        var s = New();
        Assert.That(E.ProcessingStationChoices(s), Is.Empty, "都没建");
        Fac(s).Workshops.AddRange(new[] { "workbench", "water_station" });
        var one = E.ProcessingStationChoices(s);
        Assert.That(one.Select(c => c.StationId), Is.EqualTo(new[] { "water_station" }), "工作台不列");
        Assert.That(one[0].Line, Is.EqualTo($"{E.StationName("water_station")} · 空闲"));

        Fac(s).Workshops.Add("kiln");
        Fac(s).StationBatches.Add(new StationBatch { BatchId = 1, StationId = "kiln", RecipeId = "x", DoneMinutes = 12, RequiredMinutes = 60 });
        var two = E.ProcessingStationChoices(s);
        Assert.That(two.Select(c => c.StationId), Is.EqualTo(new[] { "kiln", "water_station" }), "原工坊设施顺序");
        Assert.That(two[0].Line, Is.EqualTo($"{E.StationName("kiln")} · 12/60分"));
        Assert.That(GameEngine.ChooserTitle, Is.EqualTo("选择设施"));
        Assert.That(GameEngine.ChooserEmpty, Is.EqualTo("当前区域没有可操作的加工设施。"));
    }

    /// <summary>Q542／Q560：工坊卡点击走 OpenWorkshops；两座弹选择设施、一座直开、没有去建工作台；从选择设施进的制作界面「返回」回选择层并聚焦原行。</summary>
    [Test]
    public void Q560_ChooserWiring()
    {
        var fac = Web("Services", "GameSession.FacMenu.cs");
        Assert.That(fac, Does.Contain("public void OpenWorkshops()"));
        Assert.That(fac, Does.Contain("OpenPanel(\"craft\", \"build_workbench\");"));
        Assert.That(fac, Does.Contain("st-pick-"), "返回后聚焦原行");
        var pops = Web("Components", "ActionPops.razor");
        Assert.That(pops, Does.Contain("case GameSession.StationsPop"));
        Assert.That(pops, Does.Contain("GameEngine.ChooserEmpty"));
        var craft = Web("Components", "CraftOverlay.razor");
        Assert.That(craft, Does.Contain("G.CraftFromChooser ? \"返回\" : \"关闭\""));
        Assert.That(craft, Does.Contain("G.PanelEscape()"));
        Assert.That(Web("Components", "FacilityCard.razor"), Does.Contain("G.OpenWorkshops();"));
    }

    // ───────────── FACPROC 中央菜单 ─────────────

    /// <summary>FACPROC 第 1 条、FACPROC-A01：点击火堆／柴炉卡打开中央加工菜单（全幅面板），卡上快捷动作保留。</summary>
    [Test]
    public void FacProc_A01_CardClickOpensMenu()
    {
        var card = Web("Components", "FireCard.razor");
        Assert.That(card, Does.Contain("G.OpenFireMenu"));
        Assert.That(card, Does.Contain("点击打开加工菜单"));
        var screen = Web("Components", "GameScreen.razor");
        Assert.That(screen, Does.Contain("case GameSession.FireMenuPanel:"));
        Assert.That(screen, Does.Contain("<FireMenu />"));
    }

    /// <summary>FACPROC 第 3 条、FACPROC-A04：右栏火堆详情只读——没有落点、煅烧按钮；锅卡与内容卡以只读方式渲染。</summary>
    [Test]
    public void FacProc_A04_RightColumnReadOnly()
    {
        var detail = Web("Components", "FireDetail.razor");
        Assert.That(detail, Does.Not.Contain("data-drop"));
        Assert.That(detail, Does.Not.Contain("fc-charcoal"));
        Assert.That(detail, Does.Contain("ReadOnly=\"true\""));
        Assert.That(Web("Components", "PotCard.razor"), Does.Contain("[Parameter] public bool ReadOnly"));
        Assert.That(Web("Components", "FoodCard.razor"), Does.Contain("[Parameter] public bool ReadOnly"));
        Assert.That(Web("Components", "ItemDetail.razor"), Does.Contain("G.InFireCrumbs(sel) == null"), "右栏只读时不显示旧的操作窗脚注");
    }

    /// <summary>Q544／Q545／Q557：菜单的来源区、数量步进器与提交按钮文案；明确工位放入走 PutInFireSlot。</summary>
    [Test]
    public void Q545_MenuSourcesAndQty()
    {
        var menu = Web("Components", "FireMenu.razor");
        Assert.That(menu, Does.Contain("GameEngine.SourceTitle"));
        Assert.That(menu, Does.Contain("GameEngine.PickTargetText"));
        Assert.That(menu, Does.Contain("GameEngine.NoQtyText"));
        Assert.That(menu, Does.Contain("data-qty-jump"));
        Assert.That(menu, Does.Contain("InstantRequest.PutInFireSlot("));
        Assert.That(menu, Does.Contain("ev.Detail > 1"), "连点只提交一次");
        Assert.That(menu, Does.Contain("GameEngine.FacGoneText"));
        Assert.That(GameEngine.SourceCarried, Is.EqualTo("随身"));
        Assert.That(GameEngine.SourceGround, Is.EqualTo("当前地面"));
    }

    /// <summary>Q547／Q562：Esc 先逐层返回菜单层级（FacBack），主层再关闭；数字键优先跳到菜单数量框。</summary>
    [Test]
    public void Q562_EscAndDigits()
    {
        var keys = Web("Services", "GameSession.Keys.cs");
        Assert.That(keys, Does.Contain("if (Panel == FireMenuPanel && FireMenuLevel > 0) return EscOwner.FacBack;"));
        Assert.That(Web("Components", "GameScreen.razor"), Does.Contain("G.FireMenuBack()"));
        var js = Web("wwwroot", "js", "app.js");
        Assert.That(js, Does.Contain("input[data-qty-jump]"));
    }

    /// <summary>FACPROC 第 5 条、FACPROC-A09：耗时行动成功开始后收起菜单；换地点／战斗／死亡关闭；来源用完右栏回设施摘要。</summary>
    [Test]
    public void FacProc_A09_CloseOnActionAndReconcile()
    {
        var s = Web("Services", "GameSession.cs");
        Assert.That(s, Does.Contain("CloseFireMenuForAction();"));
        Assert.That(s, Does.Contain("ReconcileFireMenu"));
        var fac = Web("Services", "GameSession.FacMenu.cs");
        Assert.That(fac, Does.Contain("S.CurrentLocation != m.Location || S.Ground != m.Ground"));
        Assert.That(fac, Does.Contain("if (Selected.Kind == SelectionKind.None) SyncRightSilently(FireSelection);"));
    }

    // ───────────── B45 试玩修复（M818） ─────────────

    /// <summary>Q557-A02：占用的加工位也是明确工位落点（fslot），被占即内核拒绝「加工位{序号}已被占用，请重新选择。」，不落到整卡自动空位。</summary>
    [Test]
    public void Q557_A02_OccupiedSlotIsExplicitDropTarget()
    {
        var menu = Web("Components", "FireMenu.razor");
        Assert.That(menu, Does.Contain("data-drop=\"@($\"fslot:{si}:{here}\")\" data-drop-level=\"card\""), "占用位卡面级落点");
        Assert.That(menu, Does.Contain("data-drop=\"@($\"fslot:{si}:{here}\")\" data-drop-level=\"hot\""), "空位热区落点");

        var s = New();
        s.Items.RemoveAll(i => i.ItemId is "raw_root" or "pot");
        var roots = E.CreateItems(s, "raw_root", 3, ItemInstance.Carried).Single();
        Assert.That(E.DoInstant(s, InstantRequest.PutInFireSlot(roots.InstanceId, 0)).Ok, Is.True);
        var src = DropSource.Parse($"card:{ItemInstance.Carried}|raw_root|{roots.InstanceId}")!;
        var r = E.ResolveDrop(s, src, DropTarget.Parse("fslot:0:camp")!);
        Assert.That(r.Ok, Is.False);
        Assert.That(r.Reason, Is.EqualTo("加工位1已被占用，请重新选择。"));
        Assert.That(E.FireSlots(s, "camp")[1], Is.Null, "另一空位不被改投");
    }

    /// <summary>FACPROC-A11：数量与提交按钮在菜单底部固定条（内容区之外），自成确认区。</summary>
    [Test]
    public void FacProc_A11_FootBarOutsideScroll()
    {
        var menu = Web("Components", "FireMenu.razor");
        Assert.That(menu, Does.Contain("<div class=\"fm-foot\" data-enter-scope"));
        Assert.That(menu, Does.Contain("@Foot(src)"));
        Assert.That(Web("wwwroot", "css", "cards.css"), Does.Contain(".fire-menu .fm-foot { flex: none;"));
    }

    /// <summary>S14 层级「{设施名} › 加工位{序号} › {锅名} › {内容}」逐级返回：加工位与锅是两层。</summary>
    [Test]
    public void FacProc_LevelsSlotThenPot()
    {
        var fac = Web("Services", "GameSession.FacMenu.cs");
        Assert.That(fac, Does.Contain("m.Inner != null ? 3 : m.Slot == null ? 0 : m.Open && m.SlotItem != null ? 2 : 1"));
        Assert.That(fac, Does.Contain("else if (level == 1) { m.Open = false; m.Inner = null; }"));
    }

    /// <summary>FACPROC 第 4 条、Q560：选择设施打开时焦点进第一座设施行；返回时焦点回明确点过的那一座（换过分类也不丢）。</summary>
    [Test]
    public void Q560_ChooserFocus()
    {
        var fac = Web("Services", "GameSession.FacMenu.cs");
        Assert.That(fac, Does.Contain("FocusIdPending = $\"st-pick-{first.StationId}\""));
        Assert.That(fac, Does.Contain("CraftFromChooser = true; ChooserReturnStation = stationId;"));
        Assert.That(fac, Does.Contain("var station = ChooserReturnStation ?? CraftTab;"));
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
