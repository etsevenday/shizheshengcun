using Survival.Core.Engine;

namespace Survival.Core.Tests;

/// <summary>
/// B42（S14「2026-11-12 悬停卡片／条目立即显示右栏详情」，HOVER-A01—A08）：悬停覆盖状态的生命周期（替换、保留、清理、失效）与对象键，
/// 以及网页壳接线（测试工程不引用网页壳，接线按组件源码核对，同 B33GearUiWiringTests）。
/// </summary>
public class B42HoverPeekTests
{
    private static string Web(string relative)
    {
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "doc", "策划案"))) dir = dir.Parent;
        Assert.That(dir, Is.Not.Null, "找不到仓库根目录");
        return File.ReadAllText(Path.Combine(dir!.FullName, "web", "Survival.Web", relative));
    }

    private static string Ctx(string? panel = null, string? member = null, string loc = "camp", string zone = "exterior", string scene = "", string? page = null, int epoch = 0) =>
        HoverPeekContext.Of(epoch, panel, member, loc, zone, scene, page);

    // ───────────── 状态生命周期（内核） ─────────────

    /// <summary>HOVER-A01、A03：进入下一个对象立即替换；同一对象同一上下文不重复刷新。</summary>
    [Test]
    public void Enter_ReplacesImmediately()
    {
        var p = new HoverPeekState();
        Assert.That(p.Enter("sel|Tool|knife||7", Ctx()), Is.True);
        Assert.That(p.Key, Is.EqualTo("sel|Tool|knife||7"));
        Assert.That(p.Enter("sel|Tool|knife||7", Ctx()), Is.False, "同一对象不重复刷新");
        Assert.That(p.Enter("sel|Resource|r1||", Ctx()), Is.True);
        Assert.That(p.Key, Is.EqualTo("sel|Resource|r1||"), "悬停下一个立即替换");
    }

    /// <summary>HOVER-A03：移到空白、右栏正文、页签（没有对象键）不清，保留最后一项。</summary>
    [Test]
    public void Blank_KeepsLast()
    {
        var p = new HoverPeekState();
        p.Enter("card|npc-cheng", Ctx());
        Assert.That(p.Enter(null, Ctx()), Is.False);
        Assert.That(p.Enter("", Ctx()), Is.False);
        Assert.That(p.Key, Is.EqualTo("card|npc-cheng"));
        Assert.That(p.Reconcile(Ctx(), _ => true), Is.False, "上下文未变、对象仍在时保留");
        Assert.That(p.Key, Is.EqualTo("card|npc-cheng"));
    }

    /// <summary>HOVER-A05：明确点页签／返回控件／点选结束覆盖；没有覆盖时 Clear 返回 false。</summary>
    [Test]
    public void Clear_EndsOverlay()
    {
        var p = new HoverPeekState();
        Assert.That(p.Clear(), Is.False);
        p.Enter("node|camp", Ctx("map"));
        Assert.That(p.Clear(), Is.True);
        Assert.That(p.Key, Is.Null);
        Assert.That(p.Context, Is.Null);
    }

    /// <summary>HOVER-A05：打开／关闭一级面板、切换成员、切换场景（地点、区、战斗）、面板换页、读档／新局／回标题（序号）都清旧悬停。</summary>
    [Test]
    public void Reconcile_ContextChange_Clears()
    {
        var baseCtx = Ctx("gear", "player");
        foreach (var changed in new[]
                 {
                     Ctx(null, null), Ctx("map"), Ctx("gear", "npc_a"), Ctx("gear", "player", loc: "village"), Ctx("gear", "player", zone: "safehouse"),
                     Ctx("gear", "player", scene: "battle"), Ctx("gear", "player", page: "x"), Ctx("gear", "player", epoch: 1),
                 })
        {
            var p = new HoverPeekState();
            p.Enter("gear|12", baseCtx);
            Assert.That(p.Reconcile(changed, _ => true), Is.True, changed);
            Assert.That(p.Key, Is.Null);
        }
    }

    /// <summary>HOVER-A05：对象被移除、隐藏、失效时立即清，不按相同位置改指另一件（键是身份，不是序号）。</summary>
    [Test]
    public void Reconcile_Invalid_ClearsWithoutRetarget()
    {
        var p = new HoverPeekState();
        var key = Peek("sel", "Tool", "knife", "carried", "7");
        p.Enter(key, Ctx());
        var alive = new HashSet<string> { Peek("sel", "Tool", "knife", "carried", "8") };   // 同名的另一件还在
        Assert.That(p.Reconcile(Ctx(), alive.Contains), Is.True);
        Assert.That(p.Key, Is.Null, "不改指同名的另一件");
    }

    /// <summary>同一对象换了上下文（例如切页后再进入）按新上下文记下。</summary>
    [Test]
    public void Enter_SameKeyNewContext_Rebinds()
    {
        var p = new HoverPeekState();
        p.Enter("status|bleed", Ctx());
        Assert.That(p.Enter("status|bleed", Ctx("map")), Is.True);
        Assert.That(p.Reconcile(Ctx("map"), _ => true), Is.False);
    }

    private static string Peek(string kind, params string[] parts) => PeekKey.Make(kind, parts);

    /// <summary>对象键：批次键自带「|」也能原样还原；种类与段可单独取。</summary>
    [Test]
    public void PeekKey_RoundTrip()
    {
        var batch = "1500|grant:7||apr:ordinary_no_effect";
        var key = PeekKey.Make(PeekKey.Sel, "ItemGroup", "substance_white", "carried", null, batch);
        Assert.That(PeekKey.TryParse(key, out var kind, out var parts), Is.True);
        Assert.That(kind, Is.EqualTo(PeekKey.Sel));
        Assert.That(parts, Is.EqualTo(new[] { "ItemGroup", "substance_white", "carried", "", batch }));
        Assert.That(PeekKey.Part(key, 3), Is.Null, "空段为 null");
        Assert.That(PeekKey.Part(key, 4), Is.EqualTo(batch));
        Assert.That(PeekKey.KindOf(PeekKey.Make(PeekKey.Node, "camp")), Is.EqualTo(PeekKey.Node));
        Assert.That(PeekKey.TryParse(null, out _, out _), Is.False);
        Assert.That(PeekKey.KindOf(""), Is.EqualTo(""));
    }

    // ───────────── 网页壳接线 ─────────────

    /// <summary>HOVER-A03、A07、A08：app.js 只在鼠标／笔真实移动进入热区时发送（触屏、按键中、拖拽中、坐标未变、被 inert 遮挡的不发），键盘焦点只认 :focus-visible。</summary>
    [Test]
    public void Js_PointerAndFocusGuards()
    {
        var js = Web("wwwroot/js/app.js");
        var i = js.IndexOf("window.survivalPeek", StringComparison.Ordinal);
        Assert.That(i, Is.GreaterThan(0));
        var peek = js.Substring(i);
        Assert.That(peek, Does.Contain("ev.pointerType === 'touch' || ev.buttons"));
        Assert.That(peek, Does.Contain("ev.clientX === lx && ev.clientY === ly"), "指针没动不连锁切换");
        Assert.That(peek, Does.Contain("closest('[data-peek]')").And.Contain("closest('[inert]')"));
        Assert.That(peek, Does.Contain("classList.contains('dragging')"));
        Assert.That(peek, Does.Contain("if (k === lastKey) return;"));
        Assert.That(peek, Does.Contain(":focus-visible"));
        Assert.That(peek, Does.Not.Contain(".click()"), "悬停不补点击");
        var screen = Web("Components/GameScreen.razor");
        Assert.That(screen, Does.Contain("survivalPeek.init").And.Contain("new PeekController(G)"));
    }

    /// <summary>HOVER-A04：悬停只改显示——会话里的悬停代码不碰真正选中、批次、引导、任务、操作盘、战斗目标、配方、地图选点、整备与成员。</summary>
    [Test]
    public void Session_PeekIsDisplayOnly()
    {
        var p = Web("Services/GameSession.Peek.cs");
        foreach (var bad in new[] { "Selected =", "Select(", "PickBatch(", "SelectedBatchKey", "Guide(", "NoteObjectInspected", "OpenPop(", "StartAction", "BattleTarget =", "BattleAction =",
                     "CraftRecipe =", "MapPicked =", "GearSourceId =", "GearSlot =", "GearPeekId =", "GearActor =", "GrowthSkill =", "DetailTab =", "Commit(", "GivePending =" })
            Assert.That(p, Does.Not.Contain(bad), bad);
        Assert.That(p, Does.Contain("if (PeekLocked || PendingDrop != null || string.IsNullOrEmpty(key)) return;"));
        Assert.That(p, Does.Contain("DialogueLocked || MenuOpen || BattleMenuOpen"));
        var s = Web("Services/GameSession.cs");
        Assert.That(s, Does.Contain("ReconcilePeek();"));
        Assert.That(s, Does.Contain("ResetPeek();"));
        // 页签、人物二级页、明确点选、战斗点选结束悬停覆盖
        foreach (var anchor in new[] { "public void SetDetailTab(DetailTab tab)", "public void OpenCharPage(string? page)", "public void Select(Selection sel)", "public void PickBattleTarget(int index)", "public void ViewBattleAlly(string? actorId)" })
        {
            var at = s.IndexOf(anchor, StringComparison.Ordinal);
            Assert.That(at, Is.GreaterThan(0), anchor);
            var body = s.Substring(at, Math.Min(900, s.Length - at));
            Assert.That(body, Does.Contain("PeekClear();"), anchor);
        }
    }

    /// <summary>HOVER-A01、A03、A06：右栏优先级——具体操作预测与拖拽行动预览优先，其次悬停对象，再整备点击详情与原页签；悬停时页签高亮跟内容。</summary>
    [Test]
    public void DetailPanel_PriorityAndTab()
    {
        var d = Web("Components/DetailPanel.razor");
        Assert.That(d, Does.Contain("var peekShown = peekKey != null && !dragPreview && !(hover != null && G.Panel == null);"));
        Assert.That(d, Does.Contain("gearPeek != null || peekShown ||"));
        var iHover = d.IndexOf("else if (hover != null && G.Panel == null)", StringComparison.Ordinal);
        var iPeek = d.IndexOf("else if (peekShown)", StringComparison.Ordinal);
        var iGear = d.IndexOf("else if (gearPeek != null)", StringComparison.Ordinal);
        Assert.That(iHover, Is.LessThan(iPeek));
        Assert.That(iPeek, Is.LessThan(iGear));
        Assert.That(d, Does.Contain("<PeekDetail Key=\"@peekKey\" />"));
        Assert.That(d, Does.Contain("G.PeekFactsChanged += OnPeekChanged;"));
    }

    /// <summary>HOVER-A01、A02：点击与悬停共用 SelectionDetail；悬停只读——不写「已选中」、不给远征确认键、批次不可点。</summary>
    [Test]
    public void SelectionDetail_SharedAndReadOnly()
    {
        var sd = Web("Components/SelectionDetail.razor");
        Assert.That(sd, Does.Contain("var mark = Peek ? \"\" : \"已选中 · \";"));
        Assert.That(sd, Does.Contain("Confirmable=\"@(needExp && !Peek)\""));
        Assert.That(sd, Does.Contain("<ItemDetail Sel=\"sel\" ReadOnly=\"Peek\" PeekBatch=\"@Batch\" />"));
        Assert.That(sd, Does.Contain("<EnemyDetail Sel=\"sel\" />"));
        Assert.That(Web("Components/DetailPanel.razor"), Does.Contain("<SelectionDetail Sel=\"sel\" />"));
        Assert.That(Web("Components/PeekDetail.razor"), Does.Contain("<SelectionDetail Sel=\"sel\" Peek=\"true\""));
        var item = Web("Components/ItemDetail.razor");
        Assert.That(item, Does.Contain("<div class=\"batch readonly @(b == chosen ? \"on\" : \"\")\">@batchText</div>"));
        Assert.That(item, Does.Contain("var sel = Sel ?? G.Selected;"));
        Assert.That(item, Does.Contain("if (ReadOnly) return;"));
    }

    /// <summary>HOVER-A01、A06：主桌面热区——地图卡、相邻路线、资源、敌踪、火堆、物品卡（含叠卡后层）、锅与锅里的水／食材、直烤食材都带与点击同一身份的键。</summary>
    [Test]
    public void DeskHotZones()
    {
        Assert.That(Web("Components/ObjectCard.razor"), Does.Contain("data-peek=\"@EffPeek\"").And.Contain("G.SetPeekFacts(k, this, new PeekFacts").And.Contain("G.DropPeekFacts(_peekReg, this)"));
        var loc = Web("Components/LocationCard.razor");
        Assert.That(loc, Does.Contain("data-peek=\"@Peek.Sel(SelectionKind.Location, LocationId)\"").And.Contain("PeekId=\"@Peek.Sel(SelectionKind.Entrance, LocationId)\""));
        var strip = Web("Components/LocationStrip.razor");
        Assert.That(strip, Does.Contain("PeekId=\"@Peek.Sel(SelectionKind.Resource, nodeId)\"").And.Contain("PeekId=\"@Peek.Sel(SelectionKind.Enemy, wid.ToString(), \"\", wid)\""));
        Assert.That(Web("Components/FireCard.razor"), Does.Contain("PeekId=\"@Peek.Sel(SelectionKind.Facility, GameEngine.FireIdOf(here))\""));
        var shelf = Web("Components/ItemShelf.razor");
        Assert.That(shelf, Does.Contain("data-peek=\"@Peek.Sel(SelectionKind.ItemGroup, id, Owner, null, bk)\"").And.Contain("data-peek=\"@Peek.Sel(SelectionKind.Tool, toolId, Owner, inst)\""));
        var pot = Web("Components/PotCard.razor");
        Assert.That(pot, Does.Contain("data-peek=\"@LiquidPeek(w.ItemId)\"").And.Contain("data-peek=\"@FoodPeek(f)\"").And.Contain("class=\"pc-head\" data-peek=\"@PotPeek\""));
        Assert.That(Web("Components/FoodCard.razor"), Does.Contain("data-peek=\"@Peek.Sel(SelectionKind.Tool, Item.ItemId, Item.Owner, Item.InstanceId)\""));
        Assert.That(Web("Components/FacilityCard.razor"), Does.Contain("PeekId=\"@Peek.Card(\"fac-\" + Kind)\""));
        Assert.That(Web("Components/SafehouseCard.razor"), Does.Contain("PeekId=\"@Peek.Card(\"safehouse\")\""));
    }

    /// <summary>HOVER-A01、A06：页面热区——地图公开节点、配方与处理说明、整备来源卡与已穿戴装备、熟练度门类、任务册与事项盘条目、生存状态图标。</summary>
    [Test]
    public void PanelHotZones()
    {
        var map = Web("Components/MapOverlay.razor");
        Assert.That(map, Does.Contain("data-peek=\"@Peek.Node(id)\"").And.Contain("var target = PeekNode ?? picked ?? here;").And.Contain("@if (PeekNode == null)"));
        var craft = Web("Components/CraftOverlay.razor");
        Assert.That(craft, Does.Contain("data-peek=\"@Peek.Recipe(id)\"").And.Contain("data-peek=\"@Peek.Recipe(ProcPrefix + pid)\""));
        Assert.That(craft, Does.Contain("var cur = peekOn ? PeekRecipe : G.CraftRecipe;").And.Contain("var batch = batchable && !other ? Math.Max(1, G.CraftBatch) : 1;"));
        var gear = Web("Components/GearOverlay.razor");
        Assert.That(gear, Does.Contain("data-peek=\"@Peek.Gear(inst.InstanceId)\"").And.Contain("data-peek=\"@(it != null ? Peek.Gear(it.InstanceId) : null)\""));
        Assert.That(gear, Does.Not.Contain("data-gear-member=\"@aid\" data-peek"), "成员页签不是对象，不进右栏");
        var growth = Web("Components/GrowthOverlay.razor");
        Assert.That(growth, Does.Contain("data-peek=\"@Peek.Skill(sid)\"").And.Contain("@DetailOf(actor, cur)").And.Contain("@DetailOf(actor, pk)"));
        var goals = Web("Components/GoalsOverlay.razor");
        Assert.That(goals, Does.Contain("<PeekSource Key=\"@qkey\"").And.Contain("<PeekSource Key=\"@tkey\"").And.Contain("@QuestLines(v)").And.Contain("@TaskLines(t)"));
        var city = Web("Components/CityServiceOverlay.razor");
        Assert.That(city, Does.Contain("<PeekSource Key=\"@qkey\"").And.Contain("<PeekSource Key=\"@skey\" Title=\"@v.Title\" Body=\"@SvcLines(v)\" />"));
        Assert.That(Web("Components/StatusIcons.razor"), Does.Contain("data-peek=\"@Peek.Status(c.Id)\""));
        var src = Web("Components/PeekSource.razor");
        Assert.That(src, Does.Contain("G.SetPeekFacts(Key, this, new PeekFacts").And.Contain("G.DropPeekFacts(_reg, this)"));
    }

    /// <summary>HOVER-A01、A03：右栏按对象种类分派到点击时用的同一组件；页内明确点选结束悬停查看；悬停版本不给「选中下一段」「查看火堆」等操作。</summary>
    [Test]
    public void PanelPeekDetail_SameSourceReadOnly()
    {
        var pd = Web("Components/PeekDetail.razor");
        Assert.That(pd, Does.Contain("<GearPeekDetail Item=\"git\" />").And.Contain("<MapOverlay Part=\"detail\" PeekNode=\"@first\" />")
            .And.Contain("<CraftOverlay Part=\"detail\" PeekRecipe=\"@first\" />").And.Contain("<GrowthOverlay Part=\"detail\" PeekSkill=\"@first\" />")
            .And.Contain("StatusView.RemainingText(chip)"));
        Assert.That(Web("Components/CraftOverlay.razor"), Does.Contain("@if (!peekOn) { <button type=\"button\"").Or.Contain("@if (!peekOn) { <button class=\"primary\""));
        foreach (var f in new[] { "MapOverlay", "CraftOverlay", "GrowthOverlay" })
            Assert.That(Web($"Components/{f}.razor"), Does.Contain("G.PeekClear();"), f + " 明确点选要结束悬停查看");
    }

    /// <summary>HOVER-A01、A07：全屏战斗沿用战斗右侧对象信息区——敌我锚点与队列头像带键；悬停敌人／队友临时显示，离开保留，瞄准预测与指令预览优先，点选查看结束覆盖。</summary>
    [Test]
    public void Battle_UsesBattlePanel()
    {
        var bs = Web("Components/BattleScreen.razor");
        Assert.That(bs, Does.Contain("data-peek=\"@Peek.Foe(idx)\"").And.Contain("data-peek=\"@Peek.Ally(id)\"").And.Contain("data-peek=\"@(qi <= 0 ? Peek.Ally(qAlly!) : Peek.Foe(qi))\""));
        var bp = Web("Components/BattlePanel.razor");
        Assert.That(bp, Does.Contain("var focus = G.BattleHover ?? peekFoe ?? G.BattleTarget;"));
        Assert.That(bp, Does.Contain("var vid = (peekAlly ?? G.BattleAllyView)!;"));
        // 瞄准与指令预览分支排在悬停查看之前
        Assert.That(bp.IndexOf("else if (G.BattleAction != null)", StringComparison.Ordinal), Is.LessThan(bp.IndexOf("(peekAlly ?? G.BattleAllyView) != null", StringComparison.Ordinal)));
        var d = Web("Components/DetailPanel.razor");
        Assert.That(d, Does.Contain("if (PeekKey.KindOf(peekKey) is PeekKey.Foe or PeekKey.Ally) peekKey = null;"));
    }
}
