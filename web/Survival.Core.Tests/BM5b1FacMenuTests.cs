using Survival.Core.Config;
using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 BM5b1「加工菜单、焦点、装备首屏、敌踪交战风险」加工菜单部分（R223）。规则出处：S14「2026-11-14 B45b Q601—Q604：独立加工目标、明确入锅与键盘收尾」
/// 及本批验收 Q601-A01—A04、Q602-A01/A02、Q603-A01/A02、Q604-A01/A02；正式句见《重墨 V2 开工裁定与界面逐字稿》「2026-11-14 B45b Q601—Q604：明确入锅与焦点逐字」。
/// 网页壳的接线以源码检查（同 B45bFacMenuTests）。
/// </summary>
public class BM5b1FacMenuTests : TestBase
{
    private const string Here = "camp";

    private static void Ok(ActionResult r) => Assert.That(r.Ok, Is.True, r.Reason);

    private ItemInstance Pot(GameState s) => s.Items.First(i => i.ItemId == "pot");

    private void LightFire(GameState s, int fuel = 240)
    {
        var f = s.Locations[Here].Facility;
        f.FireLit = true;
        f.FuelMinutes = fuel;
    }

    private void WaitMinutes(GameState s, int minutes)
    {
        var end = s.GameMinute + minutes;
        while (s.GameMinute < end)
        {
            if (s.Main == null) Start(s, ActionRequest.Wait(30));
            else if (s.Main.Status == ActionStatus.Paused) E.Resume(s);
            E.Advance(s);
        }
        if (s.Main?.Type == ActionType.Wait) E.Cancel(s);
    }

    /// <summary>加工位1：一份生肉直烤；加工位2：锅（净水 n 份、生块根 roots 份）。</summary>
    private (ItemInstance meat, ItemInstance pot) Setup(GameState s, int water = 2, int roots = 1)
    {
        var pot = Pot(s);
        foreach (var w in s.Items.Where(i => i.ItemId == "clean_water" && i.Owner == Here).ToList()) s.Items.Remove(w);
        if (water > 0)
        {
            E.DebugGive(s, "clean_water", water, Here);
            Ok(E.DoInstant(s, InstantRequest.FillPot(First(s, "clean_water", Here).InstanceId, pot.InstanceId, water)));
        }
        if (roots > 0)
        {
            E.DebugGive(s, "raw_root", roots, Here);
            var r = s.Items.First(i => i.ItemId == "raw_root" && i.Owner == Here);
            Ok(E.DoInstant(s, InstantRequest.PutFoodInPot(r.InstanceId, pot.InstanceId, roots)));
        }
        Ok(E.DoInstant(s, InstantRequest.PutInFireSlot(pot.InstanceId, 1)));
        E.DebugGive(s, "raw_meat", 1, Here);
        var meat0 = s.Items.First(i => i.ItemId == "raw_meat" && i.Owner == Here);
        Ok(E.DoInstant(s, InstantRequest.PutInFireSlot(meat0.InstanceId, 0)));
        var meat = E.FireSlots(s, Here)[0]!;
        Assert.That(meat.ItemId, Is.EqualTo("raw_meat"));
        Assert.That(E.FireSlots(s, Here)[1], Is.SameAs(pot));
        return (meat, pot);
    }

    private static string Snap(GameState s) => SaveSerializer.Serialize(s);

    // ───────────── Q601 ─────────────

    /// <summary>Q601-A01：一槽生肉、另一槽锅时，原推进时间的行动按原有效受热推进各工序；浏览／预演不推时间、不改状态；熄火不凭空加热。</summary>
    [Test]
    public void Q601_A01_PassiveHeatingIndependent()
    {
        var s = New();
        LightFire(s);
        var (meat, pot) = Setup(s);
        var root = E.PotFood(s, pot.InstanceId).Single();
        var before = Snap(s);
        // 浏览：菜单入口预演、拖放解析都只读
        _ = E.MoveToPotOptionFor(s, 0);
        _ = E.ResolveDrop(s, DropSource.Parse($"card:{meat.Owner}|{meat.ItemId}|{meat.InstanceId}")!, DropTarget.Parse($"potfood:{pot.InstanceId}")!);
        Assert.That(Snap(s), Is.EqualTo(before), "浏览不推时、不改任何状态");

        WaitMinutes(s, 10);
        Assert.That(E.ProcessOf(s, meat)!.HeatDoneMinutes, Is.EqualTo(10), "直烤照常受热");
        Assert.That(E.ProcessOf(s, root)!.HeatDoneMinutes, Is.EqualTo(10), "锅里的食材同时受热");

        s.Locations[Here].Facility.FireLit = false;
        WaitMinutes(s, 10);
        Assert.That(E.ProcessOf(s, meat)!.HeatDoneMinutes, Is.EqualTo(10), "熄火不凭空加热");
        Assert.That(E.ProcessOf(s, root)!.HeatDoneMinutes, Is.EqualTo(10));
    }

    /// <summary>Q601-A02：明确换目标清旧来源／数量／提交意图；主层外锅来源区仍只列随身／当前地面（不把另一工位食材接进来）。</summary>
    [Test]
    public void Q601_A02_RetargetClearsDraft()
    {
        var fac = Web("Services", "GameSession.FacMenu.cs");
        var retarget = Slice(fac, "private void FireMenuRetarget()", "/// <summary>\n    /// 当前层来源区的放入目标");
        Assert.That(retarget, Does.Contain("if (m.SourceFor == key) return;"));
        foreach (var line in new[] { "m.SourceId = null;", "m.Qty = \"1\";", "m.Fail = null;", "m.SrcWhy = null;" })
            Assert.That(retarget, Does.Contain(line));
        Assert.That(Slice(fac, "public void FireMenuPickSlot(", "public void FireMenuPickInner("), Does.Contain("FireMenuRetarget();"), "切工位／打开锅");
        Assert.That(Slice(fac, "public void FireMenuPickInner(", "public void FireMenuGoLevel("), Does.Contain("FireMenuRetarget();"), "进入新的内容目标");
        Assert.That(Slice(fac, "public void FireMenuGoLevel(", "private string FireMenuReturnFocus("), Does.Contain("FireMenuRetarget();"));
        Assert.That(Slice(fac, "public void FireMenuPickSource(", "public bool FireMenuTarget("), Does.Contain("m.SourceFor = FireMenuTargetKey();"), "来源绑定为之选的目标");
        Assert.That(Slice(fac, "private string? FireMenuTargetKey()", "private void FireMenuRetarget()"), Does.Contain("$\"pot:{k}:{pot.InstanceId}\""), "锅与空位是不同目标");
        // 内容变化核对不经过换目标（Q582 被占位保留草稿不变）
        Assert.That(Slice(fac, "private void ReconcileFireMenu()", "private void CloseFireMenuForAction()"), Does.Not.Contain("FireMenuRetarget();"));
        var menu = Web("Components", "FireMenu.razor");
        Assert.That(menu, Does.Contain("var all = s.Items.Where(i => (i.IsCarried || i.Owner == here) && !i.InFacility && !i.InContainer && i.Count > 0)"), "来源区不列工位内容");
    }

    /// <summary>Q601-A03：主层明确移入锅一次 0 分钟移物：源位释放、直烤进度／保护不继承、期限不刷新，锅内旧工序不重置；去重不重复日志。</summary>
    [Test]
    public void Q601_A03_MoveToPotAtomicSuccess()
    {
        var s = New();
        LightFire(s);
        var (meat, pot) = Setup(s);
        var root = E.PotFood(s, pot.InstanceId).Single();
        WaitMinutes(s, 10);
        var roast = E.ProcessOf(s, meat)!;
        Assert.That(roast.HeatDoneMinutes, Is.EqualTo(10));
        Assert.That(roast.HeatProtected, Is.True, "首个有效段后受保护");
        var exp = meat.ExpiresAt;
        var minute = s.GameMinute;
        var rootHeat = E.ProcessOf(s, root)!.HeatDoneMinutes;
        var potName = E.ItemName(pot.ItemId);

        var mv = E.MoveToPotOptionFor(s, 0)!;
        Assert.That(mv, Is.Not.Null);
        Assert.That(E.MoveToPotOptionFor(s, 1), Is.Null, "锅位本身没有这个入口");
        Assert.That(mv.Reason, Is.Null);
        Assert.That(mv.Label, Is.EqualTo("移入锅（加工位2）"));
        Assert.That(mv.Hint, Is.EqualTo($"将加工位1的生肉移入加工位2的{potName}。原直烤受热进度不继承。"));

        var req = mv.Request.WithRequestId("mtp-test");
        Ok(E.DoInstant(s, req));
        Assert.That(s.GameMinute, Is.EqualTo(minute), "0 额外世界分钟");
        Assert.That(meat.Owner, Is.EqualTo(ItemInstance.ContainerOwner(pot.InstanceId)));
        Assert.That(E.FireSlots(s, Here)[0], Is.Null, "源位释放");
        Assert.That(E.FireSlots(s, Here)[1], Is.SameAs(pot), "锅不挪位");
        Assert.That(meat.ExpiresAt, Is.EqualTo(exp), "原期限不刷新");
        var boil = E.ProcessOf(s, meat)!;
        Assert.That(boil.RecipeId, Is.EqualTo("boil_meat"));
        Assert.That(boil.HeatDoneMinutes, Is.EqualTo(0), "不继承直烤热量");
        Assert.That(boil.HeatProtected, Is.False, "保护不带入新工序");
        Assert.That(s.Locations[Here].Facility.Processes.Any(p => p.RecipeId == "roast_meat"), Is.False, "原直烤工序移除");
        Assert.That(E.ProcessOf(s, root)!.HeatDoneMinutes, Is.EqualTo(rootHeat), "锅内旧工序不重置");
        var done = $"已将加工位1的生肉移入加工位2的{potName}。原直烤受热进度已清零。";
        Assert.That(s.Log.Last().Text, Is.EqualTo(done));

        var again = E.DoInstant(s, req);
        Assert.That(again.Ok && again.Duplicate, Is.True, "同一请求只生效一次");
        Assert.That(s.Log.Count(l => l.Text == done), Is.EqualTo(1), "无重复日志");
    }

    /// <summary>Q601-A03：旧直烤受热为 0 时成功日志不追加清零句；拖工位食材到同火源锅解析为同一移入请求。</summary>
    [Test]
    public void Q601_A03_NoHeatNoResetSentence_DropRoutes()
    {
        var s = New();
        var (meat, pot) = Setup(s);
        var src = DropSource.Parse($"card:{meat.Owner}|{meat.ItemId}|{meat.InstanceId}")!;
        foreach (var dst in new[] { $"potfood:{pot.InstanceId}", $"potfill:{pot.InstanceId}", $"card:{pot.Owner}|pot|{pot.InstanceId}" })
        {
            var d = E.ResolveDrop(s, src, DropTarget.Parse(dst)!);
            Assert.That(d.Ok, Is.True, d.Reason + " " + dst);
            Assert.That(d.Verb, Is.EqualTo(DropVerb.MoveToPot), dst);
            Assert.That(d.Instant!.Kind, Is.EqualTo(InstantKind.MoveToPot));
            Assert.That(d.Quantifiable, Is.False, "只转这一份，不读通用数量");
        }
        var intent = E.ResolveDrop(s, src, DropTarget.Parse($"potfood:{pot.InstanceId}")!);
        intent.Instant!.Count = 5;
        Ok(E.DoInstant(s, intent.Instant));
        var potName = E.ItemName(pot.ItemId);
        Assert.That(s.Log.Last().Text, Is.EqualTo($"已将加工位1的生肉移入加工位2的{potName}。"));
        Assert.That(E.PotFood(s, pot.InstanceId).Count(f => f.ItemId == "raw_meat"), Is.EqualTo(1));
    }

    /// <summary>Q601-A04：锅满／内容变化／非同火源／取出后过期整次失败，源热量、保护、位置与目标内容不变。</summary>
    [Test]
    public void Q601_A04_FailuresAreAtomic()
    {
        // 锅满（原句）
        var s = New();
        LightFire(s);
        var (meat, pot) = Setup(s, roots: 2);
        WaitMinutes(s, 10);
        var snap = Snap(s);
        var mv = E.MoveToPotOptionFor(s, 0)!;
        Assert.That(mv.Reason, Does.StartWith("锅内净水"), "原锅满句");
        var bad = E.DoInstant(s, mv.Request);
        Assert.That(bad.Ok, Is.False);
        Assert.That(bad.Reason, Is.EqualTo(mv.Reason));
        Assert.That(Snap(s), Is.EqualTo(snap), "整次失败，全部原位");

        // 内容变化：预期种类不符／目标位不符
        var t = New();
        var (m2, p2) = Setup(t);
        var snap2 = Snap(t);
        var wrong = InstantRequest.MoveToPot(m2.InstanceId, 0, "cooked_meat", p2.InstanceId, 1);
        Assert.That(E.DoInstant(t, wrong).Reason, Is.EqualTo("内容已变化，请重新选择。"), "旧意图不改搬已变种类物");
        var wrongSlot = InstantRequest.MoveToPot(m2.InstanceId, 1, m2.ItemId, p2.InstanceId, 0);
        Assert.That(E.DoInstant(t, wrongSlot).Reason, Is.EqualTo("内容已变化，请重新选择。"));
        Assert.That(Snap(t), Is.EqualTo(snap2));

        // 非同火源
        var keep = p2.Owner;
        p2.Owner = GameEngine.FireOwner("elsewhere");
        Assert.That(E.MoveToPotBlocked(t, InstantRequest.MoveToPot(m2.InstanceId, 0, m2.ItemId, p2.InstanceId, 1)), Is.EqualTo("只能在同一火堆的加工位之间移入锅。"));
        p2.Owner = keep;

        // 受热保护中、已过原保质期：取出即腐败，整次拒绝且不先取出
        var u = New();
        LightFire(u);
        var (m3, p3) = Setup(u);
        WaitMinutes(u, 10);
        Assert.That(E.ProcessOf(u, m3)!.HeatProtected, Is.True);
        m3.ExpiresAt = u.GameMinute - 1;
        var snap3 = Snap(u);
        var r3 = E.DoInstant(u, E.MoveToPotOptionFor(u, 0)!.Request);
        Assert.That(r3.Reason, Is.EqualTo("生肉取出后已腐败，不能移入锅。"));
        Assert.That(Snap(u), Is.EqualTo(snap3), "源热量／保护／位置不变");
        Assert.That(E.ProcessOf(u, m3)!.HeatDoneMinutes, Is.EqualTo(10));
    }

    /// <summary>Q601-A04：目标锅无水但原允许存该食材时仍可存，缺水不自动受热。</summary>
    [Test]
    public void Q601_A04_DryPotStoresWithoutHeat()
    {
        var s = New();
        LightFire(s);
        var (meat, pot) = Setup(s, water: 0, roots: 0);
        Ok(E.DoInstant(s, E.MoveToPotOptionFor(s, 0)!.Request));
        Assert.That(meat.Owner, Is.EqualTo(ItemInstance.ContainerOwner(pot.InstanceId)));
        WaitMinutes(s, 10);
        Assert.That(E.ProcessOf(s, meat)?.HeatDoneMinutes ?? 0, Is.EqualTo(0), "缺水不加工");
    }

    /// <summary>Q601 壳：主层才有入口与拖动；按钮独立激活、不作默认 Enter；成功清草稿、焦点回目标锅工位；拖放与按钮同一提交。</summary>
    [Test]
    public void Q601_Shell_MainLayerEntryOnly()
    {
        var menu = Web("Components", "FireMenu.razor");
        Assert.That(menu, Does.Contain("NoDrag=\"@(level != 0)\""), "主层外不从工位食材发起拖动");
        Assert.That(menu, Does.Contain("@if (level == 0 && e.MoveToPotOptionFor(s, si) is { } mv)"));
        var btn = Slice(menu, "class=\"secondary fm-move-btn\"", "</button>");
        Assert.That(btn, Does.Not.Contain("data-enter"), "不作为默认 Enter");
        Assert.That(btn, Does.Contain("disabled=\"@(mvWhy != null)\""));
        Assert.That(btn, Does.Contain("title=\"@mv.Hint\""));
        Assert.That(Web("Components", "FoodCard.razor"), Does.Contain("data-drag=\"@(ReadOnly || NoDrag ? null : $\"card:{key}\")\""));
        var fac = Web("Services", "GameSession.FacMenu.cs");
        var move = Slice(fac, "public ActionResult FireMenuMove(InstantRequest req)", "public void FireMenuSetQty(");
        Assert.That(move, Does.Contain("m.SourceId = null;"));
        Assert.That(move, Does.Contain("FocusIdPending = FireMenuSlotId(to);"), "焦点回目标锅所在工位");
        Assert.That(move, Does.Contain("else m.Fail = res.Reason;"));
        Assert.That(Web("Services", "GameSession.cs"), Does.Contain("if (r.Kind == InstantKind.MoveToPot) { r.RequestId = requestId; return FireMenuMove(r); }"));
    }

    // ───────────── Q602 最高菜单内 Tab 循环 ─────────────

    /// <summary>Q602-A01：加工菜单是最高输入层时（C# 判定后写 data-tab-trap）Tab／Shift+Tab 在可见可用控件间首尾循环，跳禁用／隐藏／inert；无控件留在标题；不点按钮。</summary>
    [Test]
    public void Q602_A01_TabCyclesInsideTopFireMenu()
    {
        var menu = Web("Components", "FireMenu.razor");
        Assert.That(menu, Does.Contain("data-tab-trap=\"@(G.FireMenuTop ? GameSession.FireMenuTitleId : null)\""), "C# 告诉 JS 哪层在最上，值是安全落点（标题）");
        var js = Web("wwwroot", "js", "app.js");
        var trap = Slice(js, "if (k === 'Tab' && !mod) {", "// B46 试玩修复（Q535-A04）");
        Assert.That(trap, Does.Contain("document.querySelector('[data-tab-trap]')"));
        Assert.That(trap, Does.Contain("!el.disabled && el.tabIndex >= 0 && shown(el) && !(el.closest && el.closest('[inert], [hidden]'))"), "跳过禁用、隐藏与 inert");
        Assert.That(trap, Does.Contain("(at + (ev.shiftKey ? -1 : 1) + items.length) % items.length"), "首尾循环");
        Assert.That(trap, Does.Contain("document.getElementById(trap.getAttribute('data-tab-trap'))"), "无控件留在标题");
        Assert.That(trap, Does.Not.Contain(".click()"), "Tab 只移焦点");
        // 「原因」按钮是可聚焦按钮（禁用行旁仍可达）
        Assert.That(Slice(menu, "class=\"fm-src-whybtn", "</button>"), Does.Not.Contain("disabled"));
    }

    /// <summary>Q602-A02：游戏菜单、对白、反馈窗在前面先拦；就近盘／点火盘／数量框等更高层与被顶栏页盖住时 C# 不写属性；ESC 层级不变。</summary>
    [Test]
    public void Q602_A02_HigherLayersOwnTab()
    {
        var js = Web("wwwroot", "js", "app.js");
        var menuModal = js.IndexOf("if (document.querySelector('.menu-modal')) {", StringComparison.Ordinal);
        var dlg = js.IndexOf("if (document.querySelector('.dlg-layer')) {", StringComparison.Ordinal);
        var trap = js.IndexOf("var trap = document.querySelector('[data-tab-trap]');", StringComparison.Ordinal);
        Assert.That(menuModal, Is.GreaterThan(0));
        Assert.That(dlg, Is.GreaterThan(menuModal));
        Assert.That(trap, Is.GreaterThan(dlg), "更高层先接管");
        var fac = Web("Services", "GameSession.FacMenu.cs");
        Assert.That(fac, Does.Contain("public bool FireMenuTop => State != null && Panel == FireMenuPanel && FireMenu != null && EscOwner is EscOwner.FacBack or EscOwner.Panel;"));
        var keys = Web("Services", "GameSession.Keys.cs");
        var order = new[] { "if (DiskMaskShown) return EscOwner.Disk;", "if (Pop != null) return EscOwner.Pop;", "if (IgniteMenuOpen) return EscOwner.Ignite;", "if (Panel == FireMenuPanel && FireMenuLevel > 0) return EscOwner.FacBack;" };
        var at = keys.IndexOf("public EscOwner NormalEscOwner()", StringComparison.Ordinal);
        foreach (var o in order)
        {
            var i = keys.IndexOf(o, at, StringComparison.Ordinal);
            Assert.That(i, Is.GreaterThan(at), o);
            at = i;
        }
    }

    // ───────────── Q603 木材从主层起灰显 ─────────────

    /// <summary>Q603-A01：主层起木材灰行、不可选／不可拖，独立原因为逐字稿原句；加工位层／锅层仍原句；无木材不造空行。</summary>
    [Test]
    public void Q603_A01_WoodGreyFromMainLayer()
    {
        var doc = File.ReadAllText(Path.Combine(Root(), "doc", "策划案", "重墨像素风界面与卡片 v0.9", "重墨 V2 开工裁定与界面逐字稿.md"));
        Assert.That(doc, Does.Contain("| 木材原因 | " + GameEngine.WoodSourceWhy + " |"), "原因逐字");
        var fac = Web("Services", "GameSession.FacMenu.cs");
        var block = Slice(fac, "public string? FireMenuBlockWhy(ItemInstance it)", "public const string WoodItemId");
        Assert.That(block, Does.Contain("if (slot == null) return it.ItemId == WoodItemId ? WoodMainWhy(it) : null;"), "Q637 起主层更优先原因保持");
        Assert.That(fac, Does.Contain("if (FireMenuBlockWhy(it) != null) return;"), "选不上");
        var menu = Web("Components", "FireMenu.razor");
        Assert.That(menu, Does.Contain("if (!taken) why = G.FireMenuBlockWhy(it);"));
        Assert.That(Slice(menu, "<div class=\"fm-src-row no\"", "</div>"), Does.Not.Contain("data-drag"));
        Assert.That(menu, Does.Contain("if (list.Count == 0) continue;"), "空分组沿原隐藏");
        // 内核：加工位层的木材原句不变（Q637 选 A 保留）
        var s = New();
        var wood = s.Items.FirstOrDefault(i => i.ItemId == "wood" && i.IsCarried) ?? E.CreateItems(s, "wood", 2, ItemInstance.Carried).Single();
        E.MaxPutInFire(s, wood, out var why, 0);
        Assert.That(why, Does.StartWith("拖木材到火堆卡是添加燃料。"));
    }

    /// <summary>Q603-A02：投入木材每次 1 份、拖木到火堆仍是加燃料；灰行不改这两条。</summary>
    [Test]
    public void Q603_A02_FuelPathsUnchanged()
    {
        var s = New();
        var wood = s.Items.FirstOrDefault(i => i.ItemId == "wood" && i.IsCarried) ?? E.CreateItems(s, "wood", 2, ItemInstance.Carried).Single();
        var d = E.ResolveDrop(s, DropSource.Parse($"card:{wood.Owner}|wood|{wood.InstanceId}")!, DropTarget.Parse($"fire:{Here}")!);
        Assert.That(d.Ok, Is.True, d.Reason);
        Assert.That(d.Verb, Is.EqualTo(DropVerb.AddFuel));
        var quick = Web("Components", "FireQuickActions.razor");
        Assert.That(quick, Does.Contain("<strong>投入木材</strong>"));
        Assert.That(Web("Components", "FireMenu.razor"), Does.Contain("id=\"fc-charcoal\""), "煅烧入口保持");
    }

    // ───────────── Q637 木材三层原因 ─────────────

    /// <summary>
    /// Q637-A01：同一木材主层／明确工位／锅层灰显不可选，原因分别为三句（工位层原料名／数取真实煅烧配方，无配方只前句兜底）；
    /// 更优先的忙碌等原拒绝句保持，不借原因文字放开灰行拖放。
    /// </summary>
    [Test]
    public void Q637_A01_WoodReasonPerLayer()
    {
        var s14 = File.ReadAllText(Path.Combine(Root(), "doc", "策划案", "系统策划总册 v1.0", "01 基础系统", "S14 主界面与全局UI交互系统.md"));
        Assert.That(s14, Does.Contain("主层用“" + GameEngine.WoodSourceWhy + "”"), "主层句逐字");
        Assert.That(s14, Does.Contain("原兜底短句“" + GameEngine.WoodIsFuelNote + "”"));
        Assert.That(s14, Does.Contain("锅层用原“木材不能放进锅里”"));
        var s = New();
        LightFire(s);
        var wood = E.CreateItems(s, "wood", 3, ItemInstance.Carried).Single();
        // 工位层：真实煅烧配方的原料名／数
        var cp = E.CharcoalProcess!;
        Assert.That(E.MaxPutInFire(s, wood, out var slotWhy, 0), Is.EqualTo(0));
        Assert.That(slotWhy, Is.EqualTo($"拖木材到火堆卡是添加燃料。要煅烧木炭，请点击火堆或柴炉卡，在加工菜单的空加工位点「煅烧木炭」（每批{E.ItemName(cp.InputId)}×{cp.InputCount}）。"));
        // 锅层
        var pot = Pot(s);
        Assert.That(E.MaxPutFood(s, wood, pot, out var potWhy), Is.EqualTo(0));
        Assert.That(potWhy, Is.EqualTo("木材不能放进锅里"));
        // 更优先的忙碌原因不被木材句覆盖（工位层／锅层／主层整卡预演同源）
        Start(s, ActionRequest.Wait(30));
        E.MaxPutInFire(s, wood, out var busySlot, 0);
        E.MaxPutFood(s, wood, pot, out var busyPot);
        E.MaxPutInFire(s, wood, out var busyMain);
        Assert.That(busySlot, Is.EqualTo("行动进行中，不能整理物品"));
        Assert.That(busyPot, Is.EqualTo("行动进行中，不能整理物品"));
        Assert.That(busyMain, Is.EqualTo("行动进行中，不能整理物品"));
        // 无煅烧配方：只前句
        var j = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "config.v0.6.json")));
        var procs = (Newtonsoft.Json.Linq.JArray)j["processes"]!;
        procs.Where(x => (string?)x["id"] == "fire_charcoal").ToList().ForEach(x => x.Remove());
        var e2 = new GameEngine(ConfigLoader.FromJson(j.ToString(), validate: false));
        Assert.That(e2.CharcoalProcess, Is.Null);
        var s2 = e2.NewGame(12345);
        e2.DebugLegacyStory(s2);
        s2.Locations[Here].Facility.FireLit = true;
        s2.Locations[Here].Facility.FuelMinutes = 240;
        var w2 = e2.CreateItems(s2, "wood", 2, ItemInstance.Carried).Single();
        e2.MaxPutInFire(s2, w2, out var noRecipe, 0);
        Assert.That(noRecipe, Is.EqualTo(GameEngine.WoodIsFuelNote), "不造可点的烧炭入口");
        // 壳：主层只在走到木材分支（或无原因）时换主层句，其他原拒绝句保持；灰行仍不可选、不可拖
        var fac = Web("Services", "GameSession.FacMenu.cs");
        var main = Slice(fac, "private string WoodMainWhy(ItemInstance it)", "/// <summary>BM5b1（Q603）");
        Assert.That(main, Does.Contain("E.MaxPutInFire(S, it, out var why);"));
        Assert.That(main, Does.Contain("if (why != null && !why.StartsWith(GameEngine.WoodIsFuelNote, StringComparison.Ordinal)) return why;"));
        Assert.That(main, Does.Contain("return GameEngine.WoodSourceWhy;"));
        Assert.That(Slice(fac, "public string? FireMenuBlockWhy(ItemInstance it)", "public const string WoodItemId"), Does.Contain("if (slot == null) return it.ItemId == WoodItemId ? WoodMainWhy(it) : null;"));
        Assert.That(fac, Does.Contain("if (FireMenuBlockWhy(it) != null) return;"), "选不上");
        Assert.That(Slice(Web("Components", "FireMenu.razor"), "<div class=\"fm-src-row no\"", "</div>"), Does.Not.Contain("data-drag"));
    }

    /// <summary>
    /// BM5b1 试玩修复（Q603／Q637-A01）：燃料接近上限、不能再加一份木材时，三层木材行仍保留灰行（不按能否加燃料决定是否列出）；
    /// 主层原因写真实燃料容量句（同「投入木材」／拖木到火堆），工位层、锅层原句不变；燃料有空余时主层仍是主层句。
    /// </summary>
    [Test]
    public void Q637_A02_WoodKeptNearFuelCap()
    {
        var s = New();
        var fac = s.Locations[Here].Facility;
        LightFire(s, E.FuelCapOf(fac) - E.Config.Fire.BurnMinutesPerWood + 1);
        var wood = E.CreateItems(s, "wood", 3, ItemInstance.Carried).Single();
        Assert.That(E.MaxWoodForFire(fac), Is.LessThan(1), "容量不足一份木材");
        var drop = E.ResolveDrop(s, DropSource.Parse($"card:{wood.Owner}|{wood.ItemId}|{wood.InstanceId}")!, DropTarget.Parse($"fire:{Here}")!);
        Assert.That(drop.Ok, Is.False);
        Assert.That(drop.Reason, Is.EqualTo(E.FuelRoomText(fac)));
        Assert.That(E.FuelRoomText(fac), Is.EqualTo($"至少需要{E.Config.Fire.BurnMinutesPerWood}分钟空余容量（燃料上限{E.FuelCapOf(fac)}分钟）"));
        // 工位层／锅层：原各自拒绝句不因容量改变
        E.MaxPutInFire(s, wood, out var slotWhy, 0);
        Assert.That(slotWhy, Does.StartWith(GameEngine.WoodIsFuelNote));
        Assert.That(E.MaxPutFood(s, wood, Pot(s), out var potWhy), Is.EqualTo(0));
        Assert.That(potWhy, Is.EqualTo("木材不能放进锅里"));
        // 壳：木材不经「能否加燃料」预演过滤；主层原因在木材分支内先看燃料容量
        var menu = Web("Components", "FireMenu.razor");
        var ok = Slice(menu, "private bool MainOk(ItemInstance it, string here, ItemInstance? firePot)", "return p.Ok && p.Verb == DropVerb.PutFood;");
        var keep = ok.IndexOf("if (it.ItemId == GameSession.WoodItemId) return true;", StringComparison.Ordinal);
        Assert.That(keep, Is.GreaterThan(0), "来源范围确有木材就保留");
        Assert.That(keep, Is.LessThan(ok.IndexOf("e.ResolveDrop", StringComparison.Ordinal)));
        var main = Slice(Web("Services", "GameSession.FacMenu.cs"), "private string WoodMainWhy(ItemInstance it)", "/// <summary>BM5b1（Q603）");
        var prior = main.IndexOf("return why;", StringComparison.Ordinal);
        var room = main.IndexOf("E.MaxWoodForFire(fac) < 1) return E.FuelRoomText(fac);", StringComparison.Ordinal);
        Assert.That(prior, Is.GreaterThan(0));
        Assert.That(room, Is.GreaterThan(prior), "更优先原拒绝句仍在容量句之前");
        Assert.That(room, Is.LessThan(main.IndexOf("return GameEngine.WoodSourceWhy;", StringComparison.Ordinal)));
    }

    // ───────────── Q604 操作后回到相关工位 ─────────────

    /// <summary>Q604-A01：取出／失效回相关工位（锅仍在时回锅卡）；放入成功回刚放入内容的工位；移入锅成功回目标锅工位；灰行不移焦点。</summary>
    [Test]
    public void Q604_A01_FocusReturnsToRelatedSlot()
    {
        var fac = Web("Services", "GameSession.FacMenu.cs");
        var rec = Slice(fac, "private void ReconcileFireMenu()", "private void CloseFireMenuForAction()");
        Assert.That(rec, Does.Contain("if (m.SlotItem != null && FireMenuTop) FocusIdSoftPending = FireMenuSlotId(k);"));
        Assert.That(rec, Does.Contain("FireMenuSoftBack(k);"), "锅里内容没了回锅卡");
        Assert.That(fac, Does.Contain("if (FireMenuTop) FocusIdSoftPending = FireMenuReturnFocus(2, k);"));
        var submit = Slice(fac, "public ActionResult FireMenuSubmit(", "public void FireMenuRefuse(");
        Assert.That(submit, Does.Contain("if (!res.Duplicate && (req.Slot ?? m.Slot) is int k) FocusIdPending = FireMenuReturnFocus(FireMenuLevel, k);"));
        var menu = Web("Components", "FireMenu.razor");
        var dis = Slice(menu, "<div class=\"fm-src-row no\"", "</div>");
        Assert.That(dis, Does.Contain("@onmousedown:preventDefault=\"true\""), "按灰行不移走焦点");
        Assert.That(dis, Does.Contain("@onmousedown:stopPropagation=\"true\""), "原因按钮照常可聚焦");
    }

    /// <summary>Q604-A02：设施失效回菜单标题；更高层显示时不排焦点；自动修复只在焦点已失效时；主动操作的恢复每次更新只做一次。</summary>
    [Test]
    public void Q604_A02_SoftFocusOnlyWhenLost()
    {
        var fac = Web("Services", "GameSession.FacMenu.cs");
        Assert.That(fac, Does.Contain("if (!m.Dead && FireMenuTop) FocusIdSoftPending = FireMenuTitleId;"));
        var screen = Web("Components", "GameScreen.razor");
        Assert.That(screen, Does.Contain("try { await JS.InvokeVoidAsync(\"survivalPop.focusIdIfLost\", sfid); }"));
        Assert.That(screen, Does.Contain("else if (G.FocusIdSoftPending is string sfid)"), "主动操作的恢复优先、同一次只做一次");
        var js = Web("wwwroot", "js", "app.js");
        var soft = Slice(js, "focusIdIfLost: function (id) {", "// B32g（逐字稿 §1.5 Q401 选 C）");
        Assert.That(soft, Does.Contain("return !a || a === document.body || !a.isConnected;"));
        Assert.That(soft, Does.Contain("if (!lost()) return;"), "仍有效的焦点不被抢走");
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

    private static string Web(params string[] parts) => File.ReadAllText(Path.Combine(new[] { Root(), "web", "Survival.Web" }.Concat(parts).ToArray())).Replace("\r\n", "\n");
}
