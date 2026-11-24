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

    // ───────────── Q542／Q559／Q560 选择设施（B35a1 Q795 B 撤） ─────────────

    /// <summary>
    /// 原 Q542／Q559／Q560「选择设施」中间层与工坊汇总卡：B35a1（S14 2026-11-16 Q795 B「不保留‘工坊’聚合卡或‘选择设施’中间层」）撤掉，
    /// 每座真实设施一张卡直接进入（新断言见 B35a1CraftScopeTests）。这里只核撤除：选择层的函数、弹层与旧句都不在了。
    /// </summary>
    [Test]
    public void Q795_ChooserRemoved()
    {
        var fac = Web("Services", "GameSession.FacMenu.cs");
        Assert.That(fac, Does.Not.Contain("public void OpenWorkshops()"));
        Assert.That(fac, Does.Not.Contain("st-pick-"));
        var pops = Web("Components", "ActionPops.razor");
        Assert.That(pops, Does.Not.Contain("StationsPop"));
        Assert.That(pops, Does.Not.Contain("ChooserEmpty"));
        var craft = Web("Components", "CraftOverlay.razor");
        Assert.That(craft, Does.Not.Contain("CraftFromChooser"));
        Assert.That(craft, Does.Contain("G.CraftFromCandidates && !G.PanelHasUnder ? \"返回\" : G.PanelCloseLabel"), "Q934：从候选进入的工作区写「返回」");
        Assert.That(craft, Does.Contain("G.PanelEscape()"));
        Assert.That(Web("Components", "FacilityCard.razor"), Does.Contain("G.OpenWorkspace(w)"), "Q795 B：点卡直接进入该座真实设施工作区");
        Assert.That(typeof(GameEngine).GetMethod("ProcessingStationChoices"), Is.Null);
        Assert.That(typeof(GameEngine).GetField("ChooserTitle"), Is.Null);
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

    /// <summary>
    /// FACPROC 第 5 条、FACPROC-A09：换地点／战斗／死亡关闭；来源用完右栏回设施摘要。
    /// B60a 改期望（S14 B60 Q1103 选 B，取代 FACPROC-A09／Q581「耗时行动成功开始后收起菜单」）：开工不再收菜单（行为见 Web 测试 B60aPageReturnUiTests）。
    /// </summary>
    [Test]
    public void FacProc_A09_CloseOnActionAndReconcile()
    {
        var s = Web("Services", "GameSession.cs");
        Assert.That(s, Does.Not.Contain("CloseFireMenuForAction"));
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
        // B59a 改期望（S11 §Q1028「拖到满位……不高亮、不转移、不追加提示」）：原拖放原因「加工位1已被占用，请重新选择。」——拖放静默
        Assert.That((r.Silent, r.Reason), Is.EqualTo((true, "")));
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

    private static string Root()
    {
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "web", "Survival.sln"))) dir = dir.Parent;
        Assert.That(dir, Is.Not.Null, "找到仓库根目录");
        return dir!.FullName;
    }

    private static string Web(params string[] parts) => File.ReadAllText(Path.Combine(new[] { Root(), "web", "Survival.Web" }.Concat(parts).ToArray()));
}
