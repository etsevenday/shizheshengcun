using Newtonsoft.Json.Linq;
using Survival.Core.Config;
using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 批次 B27b（R86 Q258、R87 Q259、R88 Q260）。规则来源：assets/20261105/b29-entry-water-q258-268.json 的 resource；
/// 验收 Q258-268-A01—A03。内核部分在这里断言，卡面接线用文本断言。
/// </summary>
public class B27bCardDisplayTests : TestBase
{
    private const string ColdBox = "village_cold_box";
    private const string Locker = "village_weapon_locker";
    private const string Sign = "village_south_sign";
    private const string RefillNode = "h02_square_water";

    private static string Root()
    {
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "web", "Survival.sln"))) dir = dir.Parent;
        Assert.That(dir, Is.Not.Null, "找到仓库根目录");
        return dir!.FullName;
    }

    private static readonly Lazy<JObject> Contract = new(() =>
        JObject.Parse(File.ReadAllText(Path.Combine(Root(), "doc", "策划案", "系统策划总册 v1.0", "assets", "20261105", "b29-entry-water-q258-268.json"))));

    private static string Razor(string name) => File.ReadAllText(Path.Combine(Root(), "web", "Survival.Web", "Components", name));

    private GameState AtNode(string node, int progress = 100)
    {
        var s = New();
        E.DebugMapTeleport(s, node);
        if (progress > s.Locations[node].Progress) E.DebugSetProgress(s, node, progress);
        ReadDialogues(s);
        return s;
    }

    private DocumentCardView Doc(GameState s, string id) => E.DocumentsHere(s).Single(d => d.Id == id);

    /// <summary>手摆一个本资源点在办的搜集：库存挪进预留，和 Start 的账一样。</summary>
    private static void FakeGather(GameState s, string node, int units)
    {
        var rs = s.Resources[node];
        rs.Available = (rs.Available ?? 0) - units;
        rs.Reserved += units;
        s.Main = new ActionState { Type = ActionType.Gather, TargetId = node, ReservedResourceUnits = units };
    }

    // ───────────── R86 Q258 ─────────────

    /// <summary>A01：冷藏箱写全名，和本轮合同逐字一致；稳定 ID、数量、20/80 不变。</summary>
    [Test]
    public void Q258_ColdBox_FullName_LootUnchanged()
    {
        var c = (JObject)Contract.Value["resource"]!["coldBox"]!;
        var box = E.Resource(ColdBox);
        Assert.That((string)c["nodeId"]!, Is.EqualTo(ColdBox));
        Assert.That(E.NoOutputYieldLine(box), Is.EqualTo("有限 · 随机其一：抗病毒前体×1（20%）；草药绷带×2（80%）"));
        Assert.That(E.NoOutputYieldLine(box), Is.EqualTo((string)c["copy"]!));
        Assert.That(box.ExclusiveLoot!.Select(x => (x.ItemId, x.Count, x.Weight)),
            Is.EqualTo(new[] { ("antiviral_precursor", 1, 20), ((string)c["itemId"]!, 2, 80) }));
        Assert.That(box.Outputs, Is.Empty, "仍是互斥表，不是确定产出");
    }

    /// <summary>A01：可开工、在办、已搜空时详情读到的都是全名，不再有选中前后两种写法。</summary>
    [Test]
    public void Q258_Detail_SameName_AllStates()
    {
        var s = AtNode("village");
        var req = ActionRequest.Gather(ColdBox);
        var line = E.NoOutputYieldLine(E.Resource(ColdBox))!;
        Assert.That(E.Preview(s, req).Notes, Has.Member("随机得到其一：抗病毒前体×1（20%）；草药绷带×2（80%）"));
        Start(s, req);
        Assert.That(E.Preview(s, req).Notes, Has.Member(line));
        Run(s);
        Assert.That(E.Preview(s, req).Notes, Has.Member(line));
        Assert.That(string.Join("|", E.Preview(s, req).Notes), Does.Not.Contain("；绷带"));
    }

    /// <summary>A01：别的绷带不混；有确定产出的资源点不走这句，配置里仍只有冷藏箱和图册两张卡走。</summary>
    [Test]
    public void Q258_OtherBandages_NotMixed()
    {
        Assert.That(E.ItemName("bandage"), Is.EqualTo("草药绷带"));
        var bandages = Cfg.Items.Where(i => i.Name.Contains("绷带")).ToList();
        Assert.That(bandages.Select(i => i.Name), Is.Unique);
        Assert.That(bandages.Select(i => i.Name), Is.SupersetOf(new[] { "草药绷带", "无菌绷带", "加压绷带" }));
        var line = E.NoOutputYieldLine(E.Resource(ColdBox))!;
        foreach (var other in bandages.Where(i => i.Id != "bandage")) Assert.That(line, Does.Not.Contain(other.Name));
        Assert.That(Cfg.Resources.Where(r => E.NoOutputYieldLine(r) != null).Select(r => r.NodeId), Is.EquivalentTo(new[] { ColdBox, "w03_quarry_blueprints" }));
    }

    // ───────────── R87 Q259 ─────────────

    /// <summary>A02：行动中、暂停中状态行不变，按钮锁还在，读不了，任务不触发。</summary>
    [Test]
    public void Q259_Action_And_Pause_KeepOwnStatus_LockKept()
    {
        var s = AtNode("village");
        var line = Doc(s, Sign).CardLine;
        Assert.That(line, Is.EqualTo(Doc(s, Sign).Status));
        Assert.That(Doc(s, Sign).ReadBlocked, Is.Null);

        Start(s, ActionRequest.Wait(30));
        Assert.That(Doc(s, Sign).CardLine, Is.EqualTo(line));
        Assert.That(Doc(s, Sign).ReadBlocked, Is.EqualTo("行动进行中"), "全局原因留给按钮和悬停");
        s.Main!.Status = ActionStatus.Paused;
        Assert.That(Doc(s, Sign).CardLine, Is.EqualTo(line));
        Assert.That(Doc(s, Sign).ReadBlocked, Is.Not.Null);
        Assert.That(E.ReadDocument(s, Sign).Ok, Is.False, "锁不穿透");
    }

    /// <summary>A02：未读对白时状态行不变；对白锁不穿透，阅读和任务都不触发。</summary>
    [Test]
    public void Q259_Dialogue_KeepsOwnStatus_NoReadThrough()
    {
        var s = AtNode("village");
        var line = Doc(s, Sign).CardLine;
        Assert.That(E.ReadDocument(s, "relief_page_village").Ok, Is.True);
        Assert.That(E.DialogueLocked(s), Is.True);
        Assert.That(Doc(s, Sign).CardLine, Is.EqualTo(line));
        Assert.That(Doc(s, Sign).ReadBlocked, Is.EqualTo(GameEngine.DialogueLockedReason));
        var flags = s.Progress.Flags.Count;
        var r = E.ReadDocument(s, Sign);
        Assert.That(r.Ok, Is.False);
        Assert.That(r.Reason, Is.EqualTo(GameEngine.DialogueLockedReason));
        Assert.That(s.Progress.Flags, Has.Count.EqualTo(flags), "没有读到就不写凭证");
    }

    /// <summary>A02：读完后的状态变化是文书自己的，仍写上来；阅读和任务触发照旧。</summary>
    [Test]
    public void Q259_OwnStatus_StillChanges_AfterRead()
    {
        var s = AtNode("village");
        const string page = "relief_page_village";
        Assert.That(Doc(s, page).CardLine, Is.EqualTo("可查看 · 不耗时"));
        Assert.That(E.ReadDocument(s, page).Ok, Is.True);
        ReadDialogues(s);
        Assert.That(GameEngine.TaskDone(s, "PRO15"), Is.True);
        Assert.That(Doc(s, page).CardLine, Is.EqualTo("已读过 · 可重读"));
        Start(s, ActionRequest.Wait(30));
        Assert.That(Doc(s, page).CardLine, Is.EqualTo("已读过 · 可重读"));
    }

    /// <summary>告示自己的前置不够不是全局原因：闲着和行动中都写在状态行，读权限不变。</summary>
    [Test]
    public void Q259_NoticeOwnNeed_StaysOnLine()
    {
        var s = AtNode("h03_workshop");
        const string id = "h03_craft_notice";
        var need = Doc(s, id).CardLine;
        Assert.That(need, Does.Contain("营区登记"));
        Assert.That(Doc(s, id).ReadBlocked, Is.EqualTo(need));
        Start(s, ActionRequest.Wait(30));
        Assert.That(Doc(s, id).CardLine, Is.EqualTo(need));
        Assert.That(E.ReadDocument(s, id).Ok, Is.False);
    }

    /// <summary>做法从别处学到、看过告示后熟练仍不够：按钮被自身原因锁着，状态行和原来一样写这个原因，不写“可重读”。</summary>
    [Test]
    public void Q259_DoneNotice_BlockedByOwnNeed_LineUnchanged()
    {
        var s = AtNode("h03_workshop");
        const string id = "h03_ceramic_notice";
        s.Progress.Flags.Add("mining_blueprint_pack");
        Assert.That(E.ReadDocument(s, id).Ok, Is.True);
        var d = Doc(s, id);
        Assert.That(d.Status, Does.StartWith("已学会"));
        Assert.That(d.ReadBlocked, Does.Contain("制造"));
        Assert.That(d.CardLine, Is.EqualTo(d.ReadBlocked));
    }

    /// <summary>卡面接线：状态行读 CardLine，按钮和点卡仍读原来的 why。</summary>
    [Test]
    public void Q259_Card_Wiring()
    {
        var razor = Razor("PrologueCards.razor");
        Assert.That(razor, Does.Contain("<div class=\"oc-line\" title=\"@d.CardLine\">@d.CardLine</div>"));
        Assert.That(razor, Does.Not.Contain("(why ?? d.Status)"));
        Assert.That(razor, Does.Contain("var why = busy ? \"行动进行中\" : d.ReadBlocked;"));
        // B39 Q309：按钮改 GateButton（禁用语义 + 可聚焦，原因仍读 why）
        Assert.That(razor, Does.Contain("<GateButton Class=\"loc-btn res-btn\" Name=\"查看\" Blocked=\"@why\" Title=\"查看（0分钟）\""));
        Assert.That(razor, Does.Contain("OnClick=\"() => Read(id, why)\""));
    }

    // ───────────── R88 Q260 ─────────────

    /// <summary>A03：单次箱开工、暂停时角标和灰显按开工前，读卡面不改账，按钮锁不变；退出读回真实库存。</summary>
    [Test]
    public void Q260_Cache_Gathering_And_Paused_ThenCancel()
    {
        var s = AtNode("village");
        var r = E.Resource(Locker);
        var req = ActionRequest.Gather(Locker);
        Assert.That(E.ResourceCardStock(s, r), Is.EqualTo(("可搜1次", false)));
        Start(s, req);
        Assert.That(E.ResourceCardStock(s, r), Is.EqualTo(("可搜1次", false)));
        s.Main!.Status = ActionStatus.Paused;
        Assert.That(E.ResourceCardStock(s, r), Is.EqualTo(("可搜1次", false)));
        Assert.That(E.ResourceStock(s, r), Is.EqualTo(("已搜空", true)), "真实库存读法不变");
        Assert.That(s.Resources[Locker].Available, Is.EqualTo(0), "读卡面不改账");
        Assert.That(s.Resources[Locker].Reserved, Is.EqualTo(1));
        Assert.That(E.CanStart(s, req), Is.Not.Null, "按钮锁不变");
        s.Main!.Status = ActionStatus.Running;
        Assert.That(E.Cancel(s).Ok, Is.True);
        Assert.That(s.Main, Is.Null);
        Assert.That(E.ResourceCardStock(s, r), Is.EqualTo(("可搜1次", false)));
        Assert.That(s.Resources[Locker].Available, Is.EqualTo(1));
        Assert.That(s.Resources[Locker].Reserved, Is.EqualTo(0));
    }

    /// <summary>A03：完成才耗尽。</summary>
    [Test]
    public void Q260_Cache_EmptyOnlyAfterDone()
    {
        var s = AtNode("village");
        var r = E.Resource(Locker);
        Start(s, ActionRequest.Gather(Locker));
        Assert.That(E.ResourceCardStock(s, r).Empty, Is.False);
        Run(s);
        Assert.That(s.Main, Is.Null);
        Assert.That(E.ResourceCardStock(s, r), Is.EqualTo(("已搜空", true)));
        Assert.That(s.Resources[Locker].Reserved, Is.EqualTo(0));
    }

    /// <summary>A03：补充点抽走最后一批时不写 0、不灰显，完成后才写；抽走一部分时角标含这次预留。</summary>
    [Test]
    public void Q260_RefillPoint_LastBatch_And_Partial()
    {
        var r = E.Resource(RefillNode);
        var per = r.Outputs.Values.First();
        var s = AtNode("h02_square");
        s.Resources[RefillNode].Available = per;
        Start(s, ActionRequest.Gather(RefillNode));
        Assert.That(s.Resources[RefillNode].Available, Is.EqualTo(0));
        Assert.That(E.ResourceCardStock(s, r), Is.EqualTo(($"{per}/{r.Cap}", false)));
        Assert.That(E.ResourceStock(s, r), Is.EqualTo(($"0/{r.Cap}", true)));
        Run(s);
        Assert.That(E.ResourceCardStock(s, r), Is.EqualTo(($"0/{r.Cap}", true)));

        var f = AtNode("h02_square");
        f.Resources[RefillNode].Available = r.Cap;
        Start(f, ActionRequest.Gather(RefillNode));
        Assert.That(E.ResourceCardStock(f, r).Badge, Is.EqualTo($"{r.Cap}/{r.Cap}"));
        Assert.That(E.ResourceStock(f, r).Badge, Is.EqualTo($"{r.Cap - f.Main!.ReservedResourceUnits}/{r.Cap}"));
        Run(f);
        Assert.That(E.ResourceCardStock(f, r), Is.EqualTo(E.ResourceStock(f, r)));
    }

    /// <summary>A03：不补点和本代箱的角标同样含这次预留。</summary>
    [Test]
    public void Q260_FiniteAndInstance_Badges()
    {
        var s = New();
        var bottles = E.Resource("w34_depot_bottles");
        FakeGather(s, bottles.NodeId, bottles.Cap!.Value);
        Assert.That(E.ResourceStock(s, bottles), Is.EqualTo(("搜空不补", true)));
        Assert.That(E.ResourceCardStock(s, bottles), Is.EqualTo(($"{bottles.Cap}/{bottles.Cap}·不补", false)));

        var d = New();
        var box = E.Resource("d05_04_cache0");
        d.Resources[box.NodeId].Available = 1;
        FakeGather(d, box.NodeId, 1);
        Assert.That(E.ResourceStock(d, box), Is.EqualTo(("本代已搜空", true)));
        Assert.That(E.ResourceCardStock(d, box), Is.EqualTo(($"本代剩余1/{box.Cap}", false)));
    }

    /// <summary>A03：别的卡真空照写；别的行动不加回；本卡只加这次的预留，旧档残留的 Reserved 不算。</summary>
    [Test]
    public void Q260_OnlyThisMainsReservation()
    {
        var s = AtNode("village");
        var locker = E.Resource(Locker);
        var box = E.Resource(ColdBox);
        s.Resources[ColdBox].Available = 0;
        s.Resources[ColdBox].Reserved = 3;
        Assert.That(E.ResourceCardStock(s, box), Is.EqualTo(("已搜空", true)), "没有在办行动，残留不加回");
        Start(s, ActionRequest.Gather(Locker));
        Assert.That(E.ResourceCardStock(s, box), Is.EqualTo(("已搜空", true)), "在办的是别的资源点");
        Assert.That(E.ResourceCardStock(s, locker), Is.EqualTo(("可搜1次", false)));

        var w = AtNode("h02_square");
        var water = E.Resource(RefillNode);
        w.Resources[RefillNode].Available = 0;
        w.Resources[RefillNode].Reserved = 5;
        Start(w, ActionRequest.Wait(30));
        Assert.That(E.ResourceCardStock(w, water), Is.EqualTo(($"0/{water.Cap}", true)), "在办的不是搜集");

        var o = AtNode("h02_square");
        o.Resources[RefillNode].Available = 0;
        o.Resources[RefillNode].Reserved = 5;
        o.Main = new ActionState { Type = ActionType.Gather, TargetId = RefillNode, ReservedResourceUnits = 2 };
        Assert.That(E.ResourceCardStock(o, water), Is.EqualTo(($"2/{water.Cap}", false)), "只加这次的 2，不加全部 Reserved 5");
    }

    /// <summary>A03：已领取、待守卫胜利、无限这些不看库存的状态不被盖掉。</summary>
    [Test]
    public void Q260_IndependentStates_NotOverridden()
    {
        const string quarry = "w03_quarry_blueprints";
        var s = AtNode("w03_quarry");
        var r = E.Resource(quarry);
        Assert.That(E.ResourceCardStock(s, r), Is.EqualTo(("可领取", false)));
        Start(s, ActionRequest.Gather(quarry));
        Assert.That(E.ResourceCardStock(s, r), Is.EqualTo(("可领取", false)));
        Run(s);
        Assert.That(E.ResourceCardStock(s, r), Is.EqualTo(("已领取", true)));
        s.Main = new ActionState { Type = ActionType.Gather, TargetId = quarry, ReservedResourceUnits = 1 };
        Assert.That(E.ResourceCardStock(s, r), Is.EqualTo(("已领取", true)), "预留不把已领取读回可领取");

        var g = New();
        var officer = E.Resource("officer_cache");
        var locked = E.ResourceStock(g, officer);
        g.Main = new ActionState { Type = ActionType.Gather, TargetId = officer.NodeId, ReservedResourceUnits = 1 };
        Assert.That(E.ResourceCardStock(g, officer), Is.EqualTo(locked));

        var u = New();
        var canal = E.Resource("w12_canal_water");
        u.Main = new ActionState { Type = ActionType.Gather, TargetId = canal.NodeId };
        Assert.That(E.ResourceCardStock(u, canal), Is.EqualTo(("无限", false)));
    }

    /// <summary>角标和灰显接到卡面读法；第二行、按钮仍读原来的。</summary>
    [Test]
    public void Q260_Card_Wiring()
    {
        var razor = Razor("LocationStrip.razor");
        Assert.That(razor, Does.Contain("var (stock, empty) = e.ResourceCardStock(s, r);"));
        Assert.That(razor, Does.Not.Contain("e.ResourceStock(s, r)"));
        Assert.That(razor, Does.Contain("Badge=\"@stock\" BadgeClass=\"@(empty ? \"unbuilt\" : \"\")\" Dim=\"empty\""));
        Assert.That(razor, Does.Contain("var why = busy ? (s.Main?.Status == ActionStatus.Paused ? \"行动暂停中\" : \"行动进行中\") : e.CanStart(s, req);"));
    }
}
