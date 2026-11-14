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
