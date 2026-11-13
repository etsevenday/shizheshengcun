using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 B45b「加工菜单补齐与禁用来源」（R208、R209）。规则出处：S14「2026-11-13 B45 Q581—Q590试玩答复与下方对白布局」
/// Q581、Q582、Q583、Q584、Q585（首段）、Q587、Q588 及本批验收；正式句见《重墨 V2 开工裁定与界面逐字稿》同名节。
/// 网页壳的接线以源码检查（同 B45FacMenuShellTests）。
/// </summary>
public class B45bFacMenuTests : TestBase
{
    // ───────────── Q581 主层补齐火源快捷操作 ─────────────

    /// <summary>
    /// Q581-A01：加工菜单主层与火堆卡面挂同一个快捷入口组件（不复制一份）——按钮原句、可用条件、原因、点火盘、耗时与结算同源；
    /// 点火盘锚在发起它的按钮；耗时行动成功开始才收起菜单（BeginAction → CloseFireMenuForAction），未成功留在原层。
    /// </summary>
    [Test]
    public void Q581_A01_MenuMainLayerSharesCardQuickActions()
    {
        var card = Web("Components", "FireCard.razor");
        var menu = Web("Components", "FireMenu.razor");
        var quick = Web("Components", "FireQuickActions.razor");
        Assert.That(card, Does.Contain("<FireQuickActions />"), "卡面入口保留");
        Assert.That(menu, Does.Contain("<FireQuickActions InMenu=\"true\" />"), "主层同一组件");
        foreach (var label in new[] { "<strong>投入木材</strong>", "<strong>熄灭</strong>", "<strong>点燃</strong>", "<strong>等待加工</strong>" })
            Assert.That(quick, Does.Contain(label));
        Assert.That(quick, Does.Contain("G.ToggleIgniteMenu(igniteId)"), "点燃只打开原点火盘");
        Assert.That(quick, Does.Contain("await G.StartAction(ActionRequest.WaitCook());"));
        Assert.That(quick, Does.Contain("G.FireWoodWhy(out var wood)"));
        Assert.That(quick, Does.Contain("G.CookWaitPreview()"));
        Assert.That(card, Does.Not.Contain("ActionRequest.WaitCook()"), "卡面不再自带一份");
        Assert.That(Web("Components", "IgniteMenu.razor"), Does.Contain("AnchorId=\"@G.IgniteAnchor\""));
        var session = Web("Services", "GameSession.cs");
        Assert.That(session, Does.Contain("IgniteAnchorMenu = \"fm-ignite\""));
        // 成功开始才收起：BeginAction 里 res.Ok 之后
        var begin = session.IndexOf("private ActionResult BeginAction(", StringComparison.Ordinal);
        var fail = session.IndexOf("if (!res.Ok)", begin, StringComparison.Ordinal);
        var close = session.IndexOf("CloseFireMenuForAction();", begin, StringComparison.Ordinal);
        Assert.That(fail, Is.GreaterThan(0));
        Assert.That(close, Is.GreaterThan(fail), "开工失败先返回，不收菜单");
    }

    // ───────────── Q582 放入后不自动换动作；空位被别处占用保留草稿 ─────────────

    /// <summary>Q582-A01：菜单放入成功停在提交前的层（不自动进入新物件子层），清空来源选择、数量文本不动；Enter 不沿旧来源改做装水／放食材。</summary>
    [Test]
    public void Q582_A01_StayAndClearSourceAfterPut()
    {
        var fac = Web("Services", "GameSession.FacMenu.cs");
        Assert.That(fac, Does.Contain("public ActionResult FireMenuSubmit(InstantRequest req)"));
        var submit = Slice(fac, "public ActionResult FireMenuSubmit(", "public void FireMenuRefuse(");
        Assert.That(submit, Does.Contain("m.SourceId = null;"));
        Assert.That(submit, Does.Not.Contain("m.Qty ="), "清来源不擅改数量");
        Assert.That(submit, Does.Contain("_fireMenuOwnPut = true;"));
        var rec = Slice(fac, "private void ReconcileFireMenu()", "private void CloseFireMenuForAction()");
        Assert.That(rec, Does.Contain("m.Open = false;"), "新内容只显示在加工位卡上");
        Assert.That(rec, Does.Not.Contain("m.Open = occ != null;"));
        var menu = Web("Components", "FireMenu.razor");
        Assert.That(menu, Does.Contain("G.FireMenuSubmit(req);"));
        Assert.That(menu, Does.Not.Contain("G.Instant(req)"));
        Assert.That(Web("Services", "GameSession.cs"), Does.Contain("_fireMenuOwnPut = menu;"), "煅烧确认也是菜单自己的放入");
    }

    /// <summary>Q582-A02：先选空位与来源、该位被别处占用——保留选择不跟随新物；提交时重验出含真实序号的占位句，不扣物、另一空位不被代投。</summary>
    [Test]
    public void Q582_A02_TakenKeepsDraftAndRefusesOnSubmit()
    {
        var fac = Web("Services", "GameSession.FacMenu.cs");
        Assert.That(fac, Does.Contain("if (m.SlotItem == null && occ != null && !_fireMenuOwnPut && m.SourceId != null)"));
        Assert.That(fac, Does.Contain("m.Taken = true;"));
        Assert.That(fac, Does.Contain("if (occ == null || m.Taken) return true;"), "仍是这一位的放入层");
        var menu = Web("Components", "FireMenu.razor");
        Assert.That(menu, Does.Contain("if (why != null) { G.FireMenuRefuse(why); return; }"));
        Assert.That(menu, Does.Contain("var go = st.QtyOk || (st.Pending && st.Parsed >= 1);"), "提交按钮可按，按下才重验");

        // 内核：明确工位被占即拒绝、原子不扣、不改投另一空位
        var s = New();
        s.Items.RemoveAll(i => i.ItemId is "raw_root" or "pot");
        var a = E.CreateItems(s, "raw_root", 2, ItemInstance.Carried).Single();
        var b = E.CreateItems(s, "raw_root", 1, s.Ground).Single();
        Assert.That(E.DoInstant(s, InstantRequest.PutInFireSlot(b.InstanceId, 1)).Ok, Is.True, "别处先占了加工位2");
        var res = E.DoInstant(s, InstantRequest.PutInFireSlot(a.InstanceId, 1, 1));
        Assert.That(res.Ok, Is.False);
        Assert.That(res.Reason, Is.EqualTo("加工位2已被占用，请重新选择。"));
        Assert.That(E.FindItem(s, a.InstanceId)!.Count, Is.EqualTo(2), "不扣物");
        Assert.That(E.FireSlots(s, s.Ground)[0], Is.Null, "另一空位不被代投");
    }

    // ───────────── Q583 返回的键盘位置 ─────────────

    /// <summary>Q583-A01：子层返回聚焦刚离开的加工位卡／锅卡；煅烧投料成功聚焦新内容所在加工位；失效回标题；只聚焦不执行。</summary>
    [Test]
    public void Q583_A01_ReturnFocus()
    {
        var fac = Web("Services", "GameSession.FacMenu.cs");
        Assert.That(fac, Does.Contain("if (level < from && left is int k) FocusIdPending = FireMenuReturnFocus(Math.Max(0, level), k);"));
        Assert.That(fac, Does.Contain("return $\"#{FireMenuSlotId(k)} .pc-head\";"), "锅这一层回锅卡");
        Assert.That(fac, Does.Contain("return FireMenuTitleId;"), "失效回标题");
        var menu = Web("Components", "FireMenu.razor");
        Assert.That(menu, Does.Contain("id=\"@GameSession.FireMenuSlotId(si)\""));
        Assert.That(menu, Does.Contain("<h2 id=\"@GameSession.FireMenuTitleId\" tabindex=\"-1\">"));
        Assert.That(Web("Services", "GameSession.cs"), Does.Contain("FocusIdPending = k >= 0 ? FireMenuSlotId(k) : FireMenuTitleId;"));
        Assert.That(Web("wwwroot", "js", "app.js"), Does.Contain("id.charAt(0) === '#' ? document.querySelector(id) : document.getElementById(id)"));
    }

    // ───────────── Q584 数量输入与 Enter 提示一致 ─────────────

    /// <summary>Q584-A01：锅层多个可能动作时两按钮都不标默认 Enter 键帽；点来源后焦点进数量框，框内 Enter 提交；按钮原生 Enter 不变。</summary>
    [Test]
    public void Q584_A01_PotLayerNoDefaultEnterCap()
    {
        var menu = Web("Components", "FireMenu.razor");
        Assert.That(menu, Does.Contain("HideEnterCap=\"@(pot != null && m.Inner == null && src is { Show: true })\""));
        Assert.That(menu, Does.Contain("var cap = pot == null;"));
        Assert.That(menu, Does.Contain("<EnterCap On=\"@(go && cap)\" />"));
        Assert.That(menu, Does.Contain("id=\"@GameSession.FireMenuQtyId\" data-qty-jump"));
        var actions = Web("Components", "CardActions.razor");
        Assert.That(actions, Does.Contain("[Parameter] public bool HideEnterCap"));
        Assert.That(actions, Does.Not.Contain("<EnterCap On=\"@(why == null)\" />"), "整锅拾取键帽随 HideEnterCap");
        Assert.That(Web("Services", "GameSession.FacMenu.cs"), Does.Contain("FocusIdPending = FireMenuQtyId;"));
    }

    // ───────────── Q585 首段：引导承接加工菜单 ─────────────

    /// <summary>Q585-A01（首段）：首开加工菜单后引导对白承接（菜单先开再给引导），读完回同一菜单并恢复焦点，不再开一次引导。</summary>
    [Test]
    public void Q585_A01_GuideOverMenuRestoresFocus()
    {
        var fac = Web("Services", "GameSession.FacMenu.cs");
        var open = Slice(fac, "public void OpenFireMenu()", "private void LeaveFireMenu(");
        Assert.That(open.IndexOf("OpenPanel(FireMenuPanel);", StringComparison.Ordinal), Is.LessThan(open.IndexOf("Guide(\"fire\");", StringComparison.Ordinal)), "先开菜单再给引导");
        var screen = Web("Components", "GameScreen.razor");
        Assert.That(screen, Does.Contain("else if (!locked && _lockSeen && G.Panel == GameSession.FireMenuPanel)"));
        Assert.That(screen, Does.Contain("\"survival.dialogueEnd\", \".fire-menu\""));
        var js = Web("wwwroot", "js", "app.js");
        Assert.That(js, Does.Contain("dialogueEnd: function (hostSel)"));
        Assert.That(js, Does.Contain("window.__survivalDlgPrev"));
    }

    // ───────────── Q587 路径首段不重复标题 ─────────────

    /// <summary>Q587-A01：主层只显示设施标题、不显示路径；子层显示完整「{设施名} › 加工位{序号} › {锅名} › {内容}」实际存在的层级。</summary>
    [Test]
    public void Q587_A01_NoPathOnMainLayer()
    {
        var menu = Web("Components", "FireMenu.razor");
        var nav = menu.IndexOf("<nav class=\"fire-crumbs fm-crumbs\"", StringComparison.Ordinal);
        var guard = menu.LastIndexOf("@if (level > 0)", nav, StringComparison.Ordinal);
        Assert.That(guard, Is.GreaterThan(0).And.LessThan(nav));
        Assert.That(menu, Does.Contain("var list = new List<(string, int)> { (name, 0) };"), "子层路径仍从设施名起");
    }

    // ───────────── Q588 不可放入的来源仍显示但不能选 ─────────────

    /// <summary>
    /// Q588-A01：选定目标后不兼容材料保留行、标灰、常驻「不可放入」；行不可点／不可聚焦／不可拖，鼠标·触屏·键盘都不能设为来源（会话层同源拒绝）；
    /// 行旁「原因」只读控件在底部说明位显示该行原真实拒绝句，不选材料、不投放；条件恢复只解禁。
    /// </summary>
    [Test]
    public void Q588_A01_DisabledRowsKeptGreyAndUnselectable()
    {
        var menu = Web("Components", "FireMenu.razor");
        Assert.That(menu, Does.Contain("private const string NotPlaceable = \"不可放入\";"));
        Assert.That(menu, Does.Contain("private const string WhyLabel = \"原因\";"));
        var dis = Slice(menu, "<div class=\"fm-src-row no\"", "</div>");
        Assert.That(dis, Does.Contain("aria-disabled=\"true\""));
        Assert.That(dis, Does.Not.Contain("data-drag"), "禁用行不发起拖放");
        Assert.That(dis, Does.Not.Contain("PickSource"), "禁用行不能选为来源");
        Assert.That(dis, Does.Not.Contain("tabindex=\"0\""), "禁用行本身不可聚焦");
        Assert.That(dis, Does.Contain("<span class=\"fm-src-no\">@NotPlaceable</span>"));
        Assert.That(dis, Does.Contain("class=\"fm-src-whybtn"), "独立「原因」控件");
        Assert.That(dis, Does.Contain("@onfocus=\"() => ShowWhy(iid)\""), "键盘可聚焦查看");
        Assert.That(menu, Does.Contain("if ((peekWhy ?? st.Reason) is string note)"), "底部说明位显示，离开恢复当前说明");
        Assert.That(menu, Does.Contain("if (hasTarget && !taken) why = G.FireMenuBlockWhy(it);"), "同一真实投放规则逐行判定");
        var fac = Web("Services", "GameSession.FacMenu.cs");
        Assert.That(fac, Does.Contain("if (FireMenuBlockWhy(it) != null) return;"), "会话层同源拒绝选为来源");
        Assert.That(fac, Does.Contain("return why ?? (max <= 0 ? GameEngine.NoQtyText : null);"));
    }

    /// <summary>
    /// Q588-A02：未选目标时沿 Q544 列料不判定；选目标或库存变化使已选来源失效即清来源、显示真实原因、禁止提交，不自动换料；
    /// 木材对普通加工位的拒绝句仍是 Q556 原句（加煅烧引导），主层投入木材与煅烧入口保持。
    /// </summary>
    [Test]
    public void Q588_A02_InvalidatedSourceClearedWithReason()
    {
        var fac = Web("Services", "GameSession.FacMenu.cs");
        Assert.That(fac, Does.Contain("if (FireMenu is not { } m || !FireMenuTarget(out var slot, out var pot) || slot == null || (m.Taken && pot == null)) return null;"), "主层无目标不判定");
        var rec = Slice(fac, "private void ReconcileFireMenu()", "private void CloseFireMenuForAction()");
        Assert.That(rec, Does.Contain("else if (!_fireMenuOwnPut && FireMenuBlockWhy(src) is string why)"));
        Assert.That(rec, Does.Contain("m.SrcWhy = why;"));
        var menu = Web("Components", "FireMenu.razor");
        Assert.That(menu, Does.Contain(": srcIt == null ? m.SrcWhy ?? GameEngine.PickSourceText"), "清源后显示真实原因、按钮禁用");
        Assert.That(menu, Does.Contain("<FireQuickActions InMenu=\"true\" />"), "主层投入木材保持");
        Assert.That(menu, Does.Contain("id=\"fc-charcoal\""), "煅烧入口保持");

        // 内核：木材选明确加工位时同一预演给出 Q556 原句（没有煅烧配方时）
        var s = New();
        var wood = s.Items.FirstOrDefault(i => i.ItemId == "wood" && i.IsCarried) ?? E.CreateItems(s, "wood", 2, ItemInstance.Carried).Single();
        var max = E.MaxPutInFire(s, wood, out var why, 0);
        Assert.That(max, Is.EqualTo(0));
        Assert.That(why, Does.StartWith("拖木材到火堆卡是添加燃料。"));
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

    private static string Web(params string[] parts) => File.ReadAllText(Path.Combine(new[] { Root(), "web", "Survival.Web" }.Concat(parts).ToArray()));
}
