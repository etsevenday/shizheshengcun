using Survival.Core.Engine;
using Survival.Core.State;
using Survival.Web.Services;

namespace Survival.Core.Tests;

/// <summary>
/// 批次 B53b「永久丢弃行末入口」（开发记录 B53b）：
/// R282——S06／S14／逐字稿文首「2026-11-17 B53主要原批Q857—Q861」§Q857 B：同伴随身／柜子／遗物认领／回收／封存这几类无右栏清单在行末加「丢弃」，
/// 共用 B53a 的「数量／全部→最终确认」，只对真实有权、同地点的物品（合同 assets/20261117/b53-discard-main-q857-q861.json entry；验收 B53-Q857-01／02）；
/// R283——同文首「2026-11-17 B53次要原批Q862—Q868」§Q862—Q868 定稿（合同 assets/20261117/b53-discard-copy-q862-q868.json；验收 B53-Q862-01—Q868-02）。
/// </summary>
public class B53bTests : BattleTestBase
{
    protected override bool NoEnemyApproach => true;

    private const string Lin = "npc_lin_zhao";
    private const string Rot = GameEngine.SpoiledMatterId;

    // 逐字稿 2026-11-15 B50a 表（PD-A04 拒绝句）与 B53 节水句
    private const string Processing = "加工中：先停止加工。";
    private const string Busy = "物品被占用，不能丢弃。";
    private const string Worn = "先卸下再丢弃。";
    private const string NoPermission = "当前无法丢弃该物品。";
    private const string Irreversible = "丢弃后无法找回。";
    private const string NotEdible = "腐败物不可食用。";
    private const string BusyLock = "行动进行中，不能整理物品";   // 原整理输入锁句（S06 B50a「不能通过删物绕过…整理输入锁」）

    private static string Bag(string actor) => ItemInstance.CarriedOwner(actor);

    private ItemInstance Give(GameState s, string item, string owner, int count = 1) => E.CreateItems(s, item, count, owner).Single();

    private ActionResult Confirm(GameState s, DiscardTarget t, int count, bool all)
    {
        var p = E.DiscardPreview(s, t, count, all, out var why);
        Assert.That(p, Is.Not.Null, why);
        return E.Discard(s, new DiscardRequest { Target = t, Count = count, All = all, Stamp = p!.Stamp });
    }

    private static string Web(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "Survival.Web" }.Concat(parts).ToArray()));

    private static string Section(string text, string from, string to)
    {
        var i = text.IndexOf(from, StringComparison.Ordinal);
        Assert.That(i, Is.GreaterThanOrEqualTo(0), from);
        var j = text.IndexOf(to, i + from.Length, StringComparison.Ordinal);
        Assert.That(j, Is.GreaterThan(i), to);
        return text.Substring(i, j - i);
    }

    /// <summary>林照编入同行（营区登记等容量事实），清掉开局随身，冻结刷新。</summary>
    private GameState Party()
    {
        var s = New();
        UnlockCapacity(s, 3);
        s.SpawnNextCheck = int.MaxValue;
        s.PatrolNextCheck = int.MaxValue;
        E.DebugRecruit(s, Lin);
        Assert.That(s.Party, Does.Contain(Lin), "前提：编入同行");
        s.Items.RemoveAll(i => i.Owner == Bag(Lin));
        return s;
    }

    /// <summary>当前地点（当前区）放一个遗物类箱（只造起点）。</summary>
    private static string Box(GameState s, string key, string? kind, string? location = null)
    {
        s.Remains[key] = new RemainsState { Name = "试用者", LocationId = location ?? s.CurrentLocation, Minute = s.GameMinute, Cause = "测试", Kind = kind };
        return ItemInstance.RemainsOwner(key);
    }

    // ───────────── R282 §Q857 B：无右栏清单行末入口（内核可处置来源） ─────────────

    /// <summary>
    /// B53-Q857-01（同伴随身）：同行同伴随身的一行按该件丢弃，数量路径只删确认的份数、0 分钟；B53-Q857-02：同伴留作候补（不在同行）后，同一件拒「当前无法丢弃该物品。」。
    /// </summary>
    [Test]
    public void Q857_01_CompanionRow_Deletes_ReserveCompanion_NoPermission()
    {
        var s = Party();
        var b = Give(s, "berry", Bag(Lin), 3);
        b.ExpiresAt = At(3, 8, 0);
        var t = E.DiscardRowTarget(s, b.InstanceId);
        Assert.That((t.Kind, t.InstanceId), Is.EqualTo((DiscardKind.Instance, (int?)b.InstanceId)), "§Q857「只作用于点中的实际行」：一行就是这一件");
        var v = E.DiscardViewOf(s, t);
        Assert.That((v.Total, v.Available, v.Reasons.Count), Is.EqualTo((3, 3, 0)));
        var minute = s.GameMinute;
        Assert.That(Confirm(s, t, 2, false).Ok, Is.True);
        Assert.That(b.Count, Is.EqualTo(1));
        Assert.That(b.Owner, Is.EqualTo(Bag(Lin)), "留在同伴随身，不落地");
        Assert.That(s.GameMinute, Is.EqualTo(minute), "0 世界分钟");
        Assert.That(s.Log.Last().Text, Is.EqualTo("已永久丢弃：野果×2（保质至第3日 08:00）。"));   // §Q864 A
        E.DebugToggleActive(s, Lin);
        Assert.That(s.Party, Does.Not.Contain(Lin), "前提：留作候补");
        v = E.DiscardViewOf(s, t);
        Assert.That((v.Available, v.Reasons), Is.EqualTo((0, new List<string> { NoPermission })));
    }

    /// <summary>B53-Q857-01（柜子）：人在这间安全屋屋内时柜内物可丢（全部）；B53-Q857-02：出门后同一件拒「当前无法丢弃该物品。」（柜子只能屋内存取，外地只读）。</summary>
    [Test]
    public void Q857_01_Cabinet_InsideOnly()
    {
        var s = New();
        s.Progress.Flags.Add("hub_registered");
        E.DebugBuildSafehouse(s);
        Assert.That(E.SafehouseBuilt(s, s.CurrentLocation), Is.True, "前提：本地已建安全屋");
        Assert.That(E.EnterSafehouse(s).Ok, Is.True);
        var cab = E.CabinetOwnerOf(s, s.CurrentLocation)!;
        var wood = Give(s, "wood", cab, 3);
        var keep = Give(s, "berry", cab, 2);
        var t = E.DiscardRowTarget(s, wood.InstanceId);
        Assert.That(E.DiscardViewOf(s, t).Available, Is.EqualTo(3));
        Assert.That(E.ExitSafehouse(s).Ok, Is.True);
        Assert.That(E.DiscardViewOf(s, DiscardTarget.Instance(keep.InstanceId)).Reasons, Is.EqualTo(new[] { NoPermission }), "屋外不能删柜内物");
        Assert.That(E.EnterSafehouse(s).Ok, Is.True);
        Assert.That(Confirm(s, t, 0, true).Ok, Is.True);
        Assert.That(s.Items.Contains(wood), Is.False);
        Assert.That(E.CabinetItems(s, s.CurrentLocation), Is.EqualTo(new[] { keep }), "只删这一行，不一键清柜");
        Assert.That(s.Log.Last().Text, Is.EqualTo("已永久丢弃：木材×3。"));
    }

    /// <summary>
    /// B53-Q857-01（遗物／认领箱／远征回收箱／旧实例遗物认领箱）：当地四种箱的行都可丢；箱删空后与原「取出」一样不再列在当地（不留空箱），删一部分时箱仍在；
    /// B53-Q857-02：别处的箱拒「当前无法丢弃该物品。」，认领箱里的借用矛仍拒同句（借用保护）。
    /// </summary>
    [Test]
    public void Q857_01_RemainsBoxes_HereOnly_EmptyBoxGone()
    {
        var s = New();
        var dead = Give(s, "wood", Box(s, "t_dead", null), 2);
        var claim = Give(s, "canned_food", Box(s, "t_claim", RemainsState.ClaimKind), 1);
        var loan = Give(s, "wooden_spear", ItemInstance.RemainsOwner("t_claim"));
        loan.LoanSlot = "loan_1";
        var rec = Give(s, "raw_meat", Box(s, "t_rec", RemainsState.RecoveryKind), 2);
        var relic = Give(s, "cloth", Box(s, "t_relic", RemainsState.RelicKind), 1);
        var far = Give(s, "stone", Box(s, "t_far", null, "field"), 1);
        Assert.That(s.CurrentLocation, Is.Not.EqualTo("field"));
        foreach (var it in new[] { dead, claim, rec, relic })
            Assert.That(E.DiscardViewOf(s, E.DiscardRowTarget(s, it.InstanceId)).Available, Is.EqualTo(it.Count), it.ItemId);
        Assert.That(E.DiscardViewOf(s, DiscardTarget.Instance(far.InstanceId)).Reasons, Is.EqualTo(new[] { NoPermission }), "别处的遗物不能删");
        Assert.That(E.DiscardViewOf(s, DiscardTarget.Instance(loan.InstanceId)).Reasons, Is.EqualTo(new[] { NoPermission }), "借用矛仍受保护");

        Assert.That(Confirm(s, DiscardTarget.Instance(dead.InstanceId), 0, true).Ok, Is.True);
        Assert.That(s.Remains.ContainsKey("t_dead"), Is.False, "遗物删空即移除（同 TakeFromRemains）");
        Assert.That(E.RemainsHere(s).Select(kv => kv.Key), Does.Not.Contain("t_dead"));
        Assert.That(Confirm(s, DiscardTarget.Instance(rec.InstanceId), 1, false).Ok, Is.True);
        Assert.That((s.Remains.ContainsKey("t_rec"), rec.Count), Is.EqualTo((true, 1)), "对照：没删空的箱仍在");
        Assert.That(Confirm(s, DiscardTarget.Instance(claim.InstanceId), 0, true).Ok, Is.True);
        Assert.That(s.Remains.ContainsKey("t_claim"), Is.True, "对照：认领箱里还有借用矛，不移除");
        Assert.That(s.Items.Contains(loan), Is.True);
    }

    /// <summary>B53-Q857-01（封存栏）：人在本次远征地牢里时封存战利品可丢；B53-Q857-02：离开地牢后同一件拒「当前无法丢弃该物品。」（与「取出封存物」同一地点资格）。</summary>
    [Test]
    public void Q857_01_Sealed_InDungeonOnly()
    {
        var s = B21EntryTests.AtBuffer(E, 5301);
        UnlockCapacity(s, 2);
        Start(s, ActionRequest.Travel("d01_02", true));
        Assert.That(Run(s), Is.EqualTo(SegmentStatus.Completed));
        E.DebugWinFixedHere(s);
        Assert.That(s.Battle, Is.Null);
        Assert.That(s.Expedition, Is.Not.Null, "前提：本次远征进行中");
        var sealedOwner = GameEngine.SealedOwner(s.Expedition!.InstanceId);
        var a = Give(s, "canned_food", sealedOwner, 2);
        var b = Give(s, "cloth", sealedOwner, 1);
        Assert.That(E.DiscardViewOf(s, E.DiscardRowTarget(s, a.InstanceId)).Available, Is.EqualTo(2));
        Assert.That(Confirm(s, DiscardTarget.Instance(a.InstanceId), 1, false).Ok, Is.True);
        Assert.That((a.Count, a.Owner), Is.EqualTo((1, sealedOwner)));
        E.DebugTeleport(s, "camp");
        Assert.That(E.DiscardViewOf(s, DiscardTarget.Instance(b.InstanceId)).Reasons, Is.EqualTo(new[] { NoPermission }), "不在这座地牢里");
    }

    /// <summary>B53-Q857-02：清单行同样受原整理输入锁（行动进行中）；对照：行动结束后可丢。</summary>
    [Test]
    public void Q857_02_ListRow_InputLockStillApplies()
    {
        var s = Party();
        var b = Give(s, "berry", Bag(Lin), 2);
        var t = DiscardTarget.Instance(b.InstanceId);
        Start(s, ActionRequest.Wait(30));
        var v = E.DiscardViewOf(s, t);
        Assert.That((v.Available, v.Reasons), Is.EqualTo((0, new List<string> { BusyLock })));
        E.Cancel(s);
        Assert.That(E.DiscardViewOf(s, t).Available, Is.EqualTo(2), "对照：锁解除后可丢");
    }

    /// <summary>
    /// §Q857 B＋§Q859 B：同伴锅里的水行按「丢弃水」（该锅＋该水种，按毫升），只扣确认的水、锅与食材保留；锅里食材行按该件。
    /// 每份毫升取该锅现行配置（不写死），数量路径 1 份＝配置的一份。
    /// </summary>
    [Test]
    public void Q857_Q859_CompanionPotWaterRow_IsWaterDiscard()
    {
        var s = Party();
        var pot = Give(s, "pot", Bag(Lin));
        var inside = ItemInstance.ContainerOwner(pot.InstanceId);
        var water = Give(s, "clean_water", inside, 2);
        var berry = Give(s, "berry", inside);
        var tw = E.DiscardRowTarget(s, water.InstanceId);
        Assert.That((tw.Kind, tw.InstanceId, tw.ItemId), Is.EqualTo((DiscardKind.PotWater, (int?)pot.InstanceId, "clean_water")));
        Assert.That(E.DiscardRowTarget(s, berry.InstanceId).Kind, Is.EqualTo(DiscardKind.Instance), "对照：锅里食材行按该件");
        var v = E.DiscardViewOf(s, tw);
        Assert.That((v.IsWater, v.MaxUnits), Is.EqualTo((true, 2)));
        Assert.That(Confirm(s, tw, 1, false).Ok, Is.True);
        Assert.That(water.Count, Is.EqualTo(1));
        Assert.That(s.Items.Contains(pot) && s.Items.Contains(berry), Is.True, "锅与食材保留");
    }

    // ───────────── R283 §Q862 A：子区标题 ─────────────

    /// <summary>B53-Q862-01：子区标题「丢弃 · {物名}」用当前真实显示名（同组、单件、腐败物）。</summary>
    [Test]
    public void Q862_01_Title_RealName()
    {
        Assert.That(DiscardCopy.Title("木材"), Is.EqualTo("丢弃 · 木材"));   // §Q862 例句
        var s = New();
        s.Items.RemoveAll(i => i.ItemId == "wood");
        Give(s, "wood", s.Ground, 2);
        Assert.That(E.DiscardViewOf(s, DiscardTarget.Group(s.Ground, "wood", null)).Name, Is.EqualTo("木材"));
        var pot = Give(s, "pot", ItemInstance.Carried);
        Assert.That(E.DiscardViewOf(s, DiscardTarget.Instance(pot.InstanceId)).Name, Is.EqualTo("金属锅"));
        var rot = new ItemInstance { InstanceId = s.NextInstanceId++, ItemId = Rot, Count = 1, Owner = ItemInstance.Carried, SpoiledFrom = "berry", SpoiledUnitGrams = 200, SpoiledStackMax = 20 };
        s.Items.Add(rot);
        Assert.That(DiscardCopy.Title(E.DiscardViewOf(s, DiscardTarget.Instance(rot.InstanceId)).Name), Is.EqualTo("丢弃 · 腐败物"));   // §Q862 例句
    }

    // ───────────── §Q863 A／§Q866 B「合法7预留2」 ─────────────

    /// <summary>
    /// B53-Q863-01＋B53-Q866-02：同组 9 份其中 2 份被预留——总数 9、可丢弃 7，两数同一范围；合法子集照常可删、不列被过滤份的原因；全部丢弃只删 7，预留的 2 份不动。
    /// 水按真实毫升（壶 750）。
    /// </summary>
    [Test]
    public void Q863_01_Q866_02_TotalsSameScope_LegalSubsetDeletable()
    {
        Assert.That(DiscardCopy.Totals(9, 7), Is.EqualTo("总数9，可丢弃7"));
        Assert.That(DiscardCopy.WaterTotals(750, 750), Is.EqualTo("总数750毫升，可丢弃750毫升"));
        var s = New();
        s.Items.RemoveAll(i => i.ItemId == "wood");
        var free = Give(s, "wood", s.Ground, 9);
        var held = E.SplitOff(s, free, 2);   // 起点：同组拆出 2 份（不经合堆）
        held.ReservedBy = 5302;   // 起点：被行动预留
        var t = DiscardTarget.Group(s.Ground, "wood", null);
        var v = E.DiscardViewOf(s, t);
        Assert.That((v.Total, v.Available, v.Reasons.Count, v.Blocked), Is.EqualTo((9, 7, 0, (string?)null)));
        Assert.That(Confirm(s, t, 0, true).Ok, Is.True);
        Assert.That(s.Items.Contains(free), Is.False);
        Assert.That((s.Items.Contains(held), held.Count), Is.EqualTo((true, 2)));
        var c = Give(s, "ration_canteen", ItemInstance.Carried);
        c.LiquidId = "clean_water";
        c.LiquidMl = 750;
        var w = E.DiscardViewOf(s, DiscardTarget.CanteenWater(c.InstanceId));
        Assert.That((w.Total, w.Available), Is.EqualTo((750, 750)));
    }

    // ───────────── §Q864 A：清单逐批／逐件 ─────────────

    /// <summary>
    /// B53-Q864-01：期限不同的野果逐批「{物名}×{数量}（保质至{截止}）」；无保质期省括注；独立件逐件「{物名}×1」接非白品质与耐久（白色省略，不合并）；
    /// 腐败物按真实单重「腐败物×{数量} · {单重}公斤/份」；确认与成功日志同源。
    /// </summary>
    [Test]
    public void Q864_01_PerBatch_PerPiece_RotShortUnit()
    {
        var s = New();
        s.Items.RemoveAll(i => i.ItemId is "berry" or "wood" or "wooden_spear");
        var far = Give(s, "berry", s.Ground, 8);
        far.ExpiresAt = At(3, 8, 0);
        var near = E.SplitOff(s, far, 3);   // 起点：同组拆成两批（不经合堆）
        near.ExpiresAt = At(2, 8, 0);
        var tb = DiscardTarget.Group(s.Ground, "berry", null);
        var p = E.DiscardPreview(s, tb, 0, true, out _)!;
        Assert.That(p.Lines, Is.EqualTo(new[] { "永久丢弃：野果×3（保质至第2日 08:00）、野果×5（保质至第3日 08:00）。", Irreversible }));
        Assert.That(E.Discard(s, new DiscardRequest { Target = tb, All = true, Stamp = p.Stamp }).Ok, Is.True);
        Assert.That(s.Log.Last().Text, Is.EqualTo("已永久丢弃：野果×3（保质至第2日 08:00）、野果×5（保质至第3日 08:00）。"));

        Give(s, "wood", s.Ground, 4);
        Assert.That(E.DiscardPreview(s, DiscardTarget.Group(s.Ground, "wood", null), 0, true, out _)!.Lines[0], Is.EqualTo("永久丢弃：木材×4。"), "无保质期省括注");

        var w1 = Give(s, "wooden_spear", s.Ground);
        w1.Quality = "white";
        w1.Durability = 10; w1.MaxDurability = 30;
        var w2 = Give(s, "wooden_spear", s.Ground);
        w2.Quality = "green";
        w2.Durability = 20; w2.MaxDurability = 30;
        var lines = E.DiscardPreview(s, DiscardTarget.Group(s.Ground, "wooden_spear", null), 0, true, out _)!.Lines;
        Assert.That(lines[0], Is.EqualTo("永久丢弃：木矛×1（耐久10.00/30.00）、木矛×1（绿 · 耐久20.00/30.00）。"), "逐件、白色省略、不以首件代表");

        var r1 = new ItemInstance { InstanceId = s.NextInstanceId++, ItemId = Rot, Count = 2, Owner = ItemInstance.Carried, SpoiledFrom = "berry", SpoiledUnitGrams = 200, SpoiledStackMax = 20 };
        var r2 = new ItemInstance { InstanceId = s.NextInstanceId++, ItemId = Rot, Count = 1, Owner = ItemInstance.Carried, SpoiledFrom = "raw_meat", SpoiledUnitGrams = 300, SpoiledStackMax = 20 };
        s.Items.Add(r1);
        s.Items.Add(r2);
        var tr = DiscardTarget.Group(ItemInstance.Carried, Rot, null);
        var pr = E.DiscardPreview(s, tr, 0, true, out _)!;
        Assert.That(pr.Lines[0], Is.EqualTo("永久丢弃：腐败物×2 · 0.20公斤/份、腐败物×1 · 0.30公斤/份。"), "按真实单重分开（Q852 短单位）");
        Assert.That(E.Discard(s, new DiscardRequest { Target = tr, All = true, Stamp = pr.Stamp }).Ok, Is.True);
        Assert.That(s.Log.Last().Text, Is.EqualTo("已永久丢弃：腐败物×2 · 0.20公斤/份、腐败物×1 · 0.30公斤/份。"));
    }

    // ───────────── §Q865 B：多口锅内容合计一行 ─────────────

    /// <summary>
    /// B53-Q865-01：两口锅一起丢，父清单逐件，内容合计一条「包含：…」——同种水毫升相加、净水脏水分开；成功日志「同时丢弃：…」同源；重量只扣一次。
    /// 每份水 100 毫升（B56：S10 文首 B53a Q929 取代 S06 B53 主要原批 §Q859「目前锅与壶每份500毫升」；造数×5 保持原毫升）。
    /// </summary>
    [Test]
    public void Q865_01_TwoPots_OneContentsLine()
    {
        var s = New();
        s.Items.RemoveAll(i => i.ItemId == "pot");
        var a = Give(s, "pot", s.Ground);
        var b = Give(s, "pot", s.Ground);
        Give(s, "clean_water", ItemInstance.ContainerOwner(a.InstanceId), 5);   // B56：每份100毫升，×5 保持原 500 毫升
        Give(s, "raw_meat", ItemInstance.ContainerOwner(a.InstanceId)).ExpiresAt = At(1, 20, 0);
        Give(s, "dirty_water", ItemInstance.ContainerOwner(b.InstanceId), 10);   // B56：每份100毫升，×5 保持原 1000 毫升
        Give(s, "clean_water", ItemInstance.ContainerOwner(b.InstanceId), 5);   // B56：每份100毫升，×5 保持原 500 毫升
        var grams = s.Items.Where(i => i.Owner == s.Ground).Sum(i => E.ItemGrams(s, i));
        var both = E.ItemGrams(s, a) + E.ItemGrams(s, b);
        var t = DiscardTarget.Group(s.Ground, "pot", null);
        var p = E.DiscardPreview(s, t, 0, true, out _)!;
        // 水在前（按首次出现：净水、脏水），其余内容按 §Q864 逐批格式在后
        Assert.That(p.Lines, Is.EqualTo(new[] { "永久丢弃：金属锅×1、金属锅×1。", "包含：净水1000毫升、脏水1000毫升、生肉×1（保质至第1日 20:00）。", Irreversible }));
        Assert.That(p.Lines.Count(l => l.StartsWith("包含：")), Is.EqualTo(1), "不按第几口分行");
        Assert.That(E.Discard(s, new DiscardRequest { Target = t, All = true, Stamp = p.Stamp }).Ok, Is.True);
        Assert.That(s.Log.Last().Text, Is.EqualTo("已永久丢弃：金属锅×1、金属锅×1。" + p.Lines[1].Replace("包含：", "同时丢弃：")));
        Assert.That(s.Items.Where(i => i.Owner == s.Ground).Sum(i => E.ItemGrams(s, i)), Is.EqualTo(grams - both), "只扣一次");
        Assert.That(s.Items.Any(i => i.Owner == ItemInstance.ContainerOwner(a.InstanceId) || i.Owner == ItemInstance.ContainerOwner(b.InstanceId)), Is.False);
    }

    /// <summary>B53-Q865-02：所选容器全空时省「包含／同时丢弃」；部分空只列真实内容（不编「空×1」）。</summary>
    [Test]
    public void Q865_02_AllEmpty_NoLine_PartialEmpty_OnlyReal()
    {
        var s = New();
        s.Items.RemoveAll(i => i.ItemId == "pot");
        Give(s, "pot", s.Ground);
        Give(s, "pot", s.Ground);
        var t = DiscardTarget.Group(s.Ground, "pot", null);
        var p = E.DiscardPreview(s, t, 0, true, out _)!;
        Assert.That(p.Lines, Is.EqualTo(new[] { "永久丢弃：金属锅×1、金属锅×1。", Irreversible }));
        Assert.That(p.ResultText, Is.EqualTo("已永久丢弃：金属锅×1、金属锅×1。"));
        var full = s.Items.Where(i => i.Owner == s.Ground && i.ItemId == "pot").OrderBy(i => i.InstanceId).Last();
        Give(s, "dirty_water", ItemInstance.ContainerOwner(full.InstanceId), 5);   // B56：每份100毫升，×5 保持原 500 毫升
        p = E.DiscardPreview(s, t, 0, true, out _)!;
        Assert.That(p.Lines, Is.EqualTo(new[] { "永久丢弃：金属锅×1、金属锅×1。", "包含：脏水500毫升。", Irreversible }));
    }

    // ───────────── §Q866 B：命中的真实原因全列 ─────────────

    private GameState Kiln()
    {
        var s = New();
        s.Skills["crafting"].Level = 3;
        s.Progress.Flags.Add("h03_ceramic_notice_read");
        foreach (var (id, n) in new[] { ("stone", 8), ("clay", 6), ("iron_plate", 2) }) E.CreateItems(s, id, n, s.Ground);
        Start(s, ActionRequest.Craft("build_kiln", null, null, 1));
        Run(s);
        Assert.That(E.WorkshopBuilt(s, s.Ground, "kiln"), Is.True);
        s.Items.RemoveAll(i => i.ItemId is "clay" or "charcoal");
        return s;
    }

    /// <summary>
    /// B53-Q866-01：本次范围同时被加工与占用挡住时两句都列，按 加工→占用 的分类顺序；同句（两份都被预留）只写一次；输入锁在前、其后仍列范围原因。
    /// </summary>
    [Test]
    public void Q866_01_AllReasons_StableOrder_Deduped()
    {
        var s = Kiln();
        E.CreateItems(s, "clay", 2, ItemInstance.Carried);
        E.CreateItems(s, "charcoal", 1, s.Ground);
        Assert.That(E.StartStationBatch(s, "ex_ceramic_shell").Ok, Is.True);
        // B51c2a1 改起点（S11 Q872 选 B：黏土移进陶窑；Q921：木炭仍在地面按本批预留）：原用地面黏土组，改用地面木炭组
        var held = Give(s, "charcoal", s.Ground, 1);
        held.ReservedBy = 5303;
        var v = E.DiscardViewOf(s, DiscardTarget.Group(s.Ground, "charcoal", null));
        Assert.That((v.Available, v.Reasons), Is.EqualTo((0, new List<string> { Processing, Busy })));
        Assert.That(v.Blocked, Is.EqualTo(Processing));

        s.Items.RemoveAll(i => i.ItemId == "wood");
        var w1 = Give(s, "wood", ItemInstance.Carried, 2);
        var w2 = E.SplitOff(s, w1, 1);   // 起点：两份分开、各被一个行动预留
        Assert.That((w1.Count, w2.Count), Is.EqualTo((1, 1)));
        w1.ReservedBy = 5304;
        w2.ReservedBy = 5305;
        var tw = DiscardTarget.Group(ItemInstance.Carried, "wood", null);
        Assert.That(E.DiscardViewOf(s, tw).Reasons, Is.EqualTo(new[] { Busy }), "同句只写一次");
        Start(s, ActionRequest.Wait(30));
        Assert.That(E.DiscardViewOf(s, tw).Reasons, Is.EqualTo(new[] { BusyLock, Busy }), "输入锁在前");
    }

    /// <summary>§Q866 B「只有真实阻挡本次请求的原因」：穿着的武器只写「先卸下再丢弃。」，不另报无权限；借用且穿着时两句都列（借用是真实原因）。</summary>
    [Test]
    public void Q866_WornOnlyRealReasons()
    {
        var s = New();
        var spear = s.Items.FirstOrDefault(i => i.IsWornBy(GameState.PlayerId) && i.ItemId == "wooden_spear")
                    ?? Give(s, "wooden_spear", ItemInstance.EquipOwner(GameState.PlayerId, EquipSlots.Weapon1));
        var t = DiscardTarget.Instance(spear.InstanceId);
        Assert.That(E.DiscardViewOf(s, t).Reasons, Is.EqualTo(new[] { Worn }));
        spear.LoanSlot = "loan_1";
        Assert.That(E.DiscardViewOf(s, t).Reasons, Is.EqualTo(new[] { Worn, NoPermission }));
    }

    // ───────────── §Q867 B、§Q868 A ─────────────

    /// <summary>B53-Q867-01：两处「可放下丢弃」换定稿短句（合同 copy.spoiled／copy.expiredOriginal）；网页详情读同一常量。</summary>
    [Test]
    public void Q867_01_TwoSentences()
    {
        Assert.That(GameEngine.SpoiledMatterDetailText, Is.EqualTo("不可食用或用于配方。可放下或永久丢弃。"));
        Assert.That(GameEngine.ExpiredOriginalDetailText, Is.EqualTo("已腐败，不能食用或用于配方。仍有重量，可放下或永久丢弃。"));
        var d = Web("Components", "ItemDetail.razor");
        Assert.That(d, Does.Contain("@(rot ? GameEngine.SpoiledMatterDetailText : GameEngine.ExpiredOriginalDetailText)"));
        Assert.That(d, Does.Not.Contain("放下丢弃"));
    }

    private GameState Rack()
    {
        var s = New();
        s.GameMinute = At(2, 8, 0);
        s.Skills["cooking"].Level = 5;
        s.Items.RemoveAll(i => i.Owner == s.Ground || i.Owner == ItemInstance.Carried);
        E.CreateItems(s, "wood", 4, s.Ground);
        E.CreateItems(s, "rope", 2, s.Ground);
        Start(s, ActionRequest.Craft("build_drying_rack"));
        Run(s);
        Assert.That(E.DryingRackHere(s), Is.True);
        s.Items.RemoveAll(i => i.Owner == s.Ground || i.Owner == ItemInstance.Carried);
        return s;
    }

    /// <summary>腐败停止的晾制位（异常起点，只造起点）。</summary>
    private static void SpoiledStop(GameState s)
    {
        s.Locations[s.Ground].Facility.DryingSlots.Add(new DryingSlot
        {
            Slot = 0, BatchId = 5310, RecipeId = "dry_berries", Status = DryingSlot.Spoiled, StartMinute = s.GameMinute - 60, DoneMinutes = 55,
            RequiredMinutes = 120, EndMinute = s.GameMinute - 5, InitiatorActorId = GameState.PlayerId, StopReason = DryingSlot.StopSpoiled,
        });
    }

    /// <summary>
    /// B53-Q868-01：腐败停止位清理删旧括注「（腐败的不能吃，按腐败物处理）」，主句（去向、这一位空出）保留，确有腐败物时句末接一次「腐败物不可食用。」；
    /// 对照：位内没有腐败物时不接。
    /// </summary>
    [Test]
    public void Q868_01_SpoiledStopCleanup_NoParenthesis_NotEdibleOnlyWithRot()
    {
        var s = Rack();
        SpoiledStop(s);
        s.Items.Add(new ItemInstance { InstanceId = s.NextInstanceId++, ItemId = Rot, Count = 1, Owner = ItemInstance.DryingOwner(s.Ground, 0), SpoiledFrom = "berry", SpoiledUnitGrams = 200, SpoiledStackMax = 20 });
        Assert.That(E.TakeDrying(s, 0, false).Ok, Is.True);
        var log = s.Log.Last().Text;
        // B51c2a2 改期望（S11 Q880：余物取出统一原实际移物结果句，不以「清理{地点}…」作日志引句）
        Assert.That(log, Is.EqualTo($"从{E.ZoneName(s, s.Ground)}晾制架工位1取出腐败物×1到{E.ZoneName(s, s.Ground)}地面，这一位空出。腐败物不可食用。"));
        Assert.That(log, Does.Not.Contain("按腐败物处理"));

        var t = Rack();
        SpoiledStop(t);
        var fresh = Give(t, "salt", ItemInstance.DryingOwner(t.Ground, 0));
        Assert.That(E.TakeDrying(t, 0, false).Ok, Is.True);
        // B51c2a2 改期望（S11 Q880：「清理」只用于地图，余物取出不以「清理…」引句）：原 Does.Match(@"^清理.+晾制架工位1：盐×1.*取到.+，这一位空出。$")
        Assert.That(t.Log.Last().Text, Is.EqualTo($"从{E.ZoneName(t, t.Ground)}晾制架工位1取出盐×1到{E.ZoneName(t, t.Ground)}地面，这一位空出。"), "无腐败物不接、不编坏料");
        Assert.That(t.Items.Contains(fresh), Is.True);
    }

    /// <summary>B53-Q868-02：系统说明「保质与腐败」只删「或被制作预留」半句，其余原段保留。</summary>
    [Test]
    public void Q868_02_HelpHalfSentenceRemoved()
    {
        var help = Web("Services", "HelpCatalog.cs");
        Assert.That(help, Does.Contain("保质只随游戏时间减少，仅仅放进锅里、等水净化都不会暂停。"));
        Assert.That(help, Does.Not.Contain("或被制作预留"));
    }

    // ───────────── 网页壳：行末入口、子区、ESC、原因行 ─────────────

    /// <summary>
    /// B53-Q857-01（网页）：五类清单行末都有「丢弃」并把同一套子区画在该行下方——同伴随身与遗物类箱在右栏人物页（PartyPanel），柜内行在安全屋面板，
    /// 封存行在远征物资窗；随身／地面等已有右栏的行（柜子右列、远征物资窗前两栏）不另加。
    /// </summary>
    [Test]
    public void Web_Q857_01_RowEnds_InAllLists()
    {
        var party = Web("Components", "PartyPanel.razor");
        var remains = Section(party, "@foreach (var it in items)", "</section>");
        Assert.That(remains, Does.Contain("@if (!G.CityBrowsing) { @DiscardEnd(dt) }"));
        Assert.That(remains, Does.Contain("<DiscardPanel Target=\"@dt\" Host=\"@DiscardHost.Party\" HideButton=\"true\" />"));
        var mate = Section(party, "var rows = e.CompanionCarried(s, id);", "};");
        Assert.That(mate, Does.Contain("@if (!G.CityBrowsing) { @DiscardEnd(dt) }"));
        Assert.That(mate, Does.Contain("<DiscardPanel Target=\"@dt\" Host=\"@DiscardHost.Party\" HideButton=\"true\" />"));
        Assert.That(party, Does.Contain("G.OpenListDiscard(t, DiscardHost.Party)"));

        var sh = Web("Components", "SafehouseOverlay.razor");
        var inCab = Section(sh, "@foreach (var it in items)", "@if (items.Count == 0)");
        Assert.That(inCab, Does.Contain("G.OpenListDiscard(dt, DiscardHost.Cabinet)"));
        Assert.That(inCab, Does.Contain("<DiscardPanel Target=\"@dt\" Host=\"@DiscardHost.Cabinet\" HideButton=\"true\" />"));
        Assert.That(Section(sh, "@foreach (var it in mine)", "@if (mine.Count == 0)"), Does.Not.Contain("Discard"), "随身与屋内地面已有右栏入口");

        var seal = Web("Components", "ExpeditionSeal.razor");
        var sealedRows = Section(seal, "@foreach (var r in v.Sealed)", "@if (v.Elsewhere.Count > 0)");
        Assert.That(sealedRows, Does.Contain("G.OpenListDiscard(dt, DiscardHost.Sealed)"));
        Assert.That(sealedRows, Does.Contain("<DiscardPanel Target=\"@dt\" Host=\"@DiscardHost.Sealed\" HideButton=\"true\" />"));
        Assert.That(Section(seal, "private RenderFragment SealRow", "};"), Does.Not.Contain("Discard"));
    }

    /// <summary>B53-Q862-01／Q863-01（网页）：子区顶部标题（物品「丢弃 · {物名}」、水「丢弃水」不叠加），主层与数量层都写总数行，原因逐条。</summary>
    [Test]
    public void Web_Q862_Q863_SubTitleAndTotals()
    {
        var p = Web("Components", "DiscardPanel.razor");
        Assert.That(p, Does.Contain("var title = Target.IsWater ? DiscardCopy.DiscardWater : DiscardCopy.Title(v.Name);"));
        Assert.That(p, Does.Contain("var totals = Target.IsWater ? DiscardCopy.WaterTotals(v.Total, v.Available) : DiscardCopy.Totals(v.Total, v.Available);"));
        Assert.That(System.Text.RegularExpressions.Regex.Matches(p, @"<h4 class=""discard-title"">@title</h4>").Count, Is.EqualTo(1));
        Assert.That(System.Text.RegularExpressions.Regex.Matches(p, @"<div class=""discard-avail"">@totals</div>").Count, Is.EqualTo(2), "主层、数量层");
        Assert.That(p, Does.Not.Contain("DiscardCopy.Available").And.Not.Contain("WaterAvailable"));
        Assert.That(p, Does.Contain("@foreach (var why in DiscardRules.WhyLines(open.Error, v.Reasons))"));
    }

    /// <summary>§Q866 B（网页）：原因行——上一步的拒绝在前，再按内核顺序列全部原因，同句只写一次；没有原因时为空。</summary>
    [Test]
    public void Web_Q866_WhyLines()
    {
        Assert.That(DiscardRules.WhyLines(null, new[] { Processing, Busy }), Is.EqualTo(new[] { Processing, Busy }));
        Assert.That(DiscardRules.WhyLines("物品已变，请重选。", new[] { Busy }), Is.EqualTo(new[] { "物品已变，请重选。", Busy }));
        Assert.That(DiscardRules.WhyLines(Busy, new[] { Processing, Busy }), Is.EqualTo(new[] { Busy, Processing }), "相同句只写一次");
        Assert.That(DiscardRules.WhyLines(null, Array.Empty<string>()), Is.Empty);
    }

    /// <summary>
    /// §Q857 B「关闭返回真实发起清单」（网页）：清单发起的子区只在原窗口开着时有效——人物页（右栏人物页签）、柜子（安全屋面板）、封存（远征物资窗）；
    /// 窗口切走即收起（不删除），ESC 先逐层退子区再关窗口。
    /// </summary>
    [Test]
    public void Web_Q857_ListHost_ValidOnlyWhileHostShown_EscFirst()
    {
        Assert.That(DiscardRules.HostShown(DiscardHost.Party, true, null), Is.True);
        Assert.That(DiscardRules.HostShown(DiscardHost.Party, false, null), Is.False);
        Assert.That(DiscardRules.HostShown(DiscardHost.Cabinet, false, "safehouse"), Is.True);
        Assert.That(DiscardRules.HostShown(DiscardHost.Cabinet, true, "seal"), Is.False);
        Assert.That(DiscardRules.HostShown(DiscardHost.Sealed, false, "seal"), Is.True);
        Assert.That(DiscardRules.HostShown(DiscardHost.Sealed, true, null), Is.False);

        var keys = Web("Services", "GameSession.Keys.cs");
        var iList = keys.IndexOf("if (Discarding is { ListHost: not null }) return EscOwner.Discard;", StringComparison.Ordinal);
        var iPanel = keys.IndexOf("if (Panel != null) return EscOwner.Panel;", StringComparison.Ordinal);
        Assert.That(new[] { iList, iPanel }, Is.All.GreaterThanOrEqualTo(0));
        Assert.That(iList, Is.LessThan(iPanel), "子区先于所在窗口退");
        var sess = Web("Services", "GameSession.cs");
        Assert.That(sess, Does.Contain("if (_discard?.ListHost is DiscardHost.Cabinet or DiscardHost.Sealed && next != from) _discard = null;"));
        Assert.That(Web("Services", "GameSession.Discard.cs"), Does.Contain("DiscardRules.HostShown(_discard.ListHost, DetailTab == DetailTab.Character, Panel)"));
    }

    /// <summary>开发入口（只造起点）：同伴随身三样、当地四种箱各有物品；不在屋内／地牢时不放柜子与封存。</summary>
    [Test]
    public void DevKit_BuildsStartingPointOnly()
    {
        var s = New();
        var before = s.Items.Count;
        E.DebugDiscardListKit(s);
        Assert.That(s.Party.Count, Is.EqualTo(2), "主角＋一名固定队友");
        var mate = s.Party.Single(a => a != GameState.PlayerId);
        Assert.That(s.Items.Count(i => i.Owner == Bag(mate)), Is.EqualTo(3));
        // 试玩修复 B7（M1060）：箱键改为「试用箱名@当前地点」，原断言按固定键找改为按前缀找当地四种箱（出处：B53b 开发记录「试玩修复」B7）
        var here = E.RemainsHere(s).Select(kv => kv.Key).ToList();
        Assert.That(here.Count(k => k.StartsWith("dev_b53b_")), Is.EqualTo(4));
        Assert.That(new[] { "dev_b53b_remains@", "dev_b53b_claim@", "dev_b53b_recovery@", "dev_b53b_relic@" }.All(k => here.Any(h => h.StartsWith(k))), Is.True);
        Assert.That(s.Items.Any(i => i.InSafehouseStorage || i.Owner.StartsWith(GameEngine.SealedPrefix)), Is.False);
        Assert.That(s.Items.Count, Is.GreaterThan(before));
    }

    // ───────────── 变异测试补测（M1059；第一次 Stryker 3a1b74b9..2aecff51 存活／无覆盖） ─────────────

    /// <summary>
    /// §Q866 B「只有真实阻挡本次请求的原因」＋§Q857 B「原可管理同伴」：同行同伴身上穿着的木矛只报「先卸下再丢弃。」；
    /// 该同伴留作候补后不再可管理，再加「当前无法丢弃该物品。」。
    /// </summary>
    [Test]
    public void MT_CompanionWorn_ActiveOnlyWorn_ReserveAlsoNoPermission()
    {
        var s = Party();
        var worn = s.Items.First(i => i.IsWornBy(Lin));
        var t = DiscardTarget.Instance(worn.InstanceId);
        Assert.That(E.DiscardViewOf(s, t).Reasons, Is.EqualTo(new[] { Worn }));
        E.DebugToggleActive(s, Lin);
        Assert.That(s.Party, Does.Not.Contain(Lin), "前提：留作候补");
        Assert.That(E.DiscardViewOf(s, t).Reasons, Is.EqualTo(new[] { Worn, NoPermission }));
    }

    /// <summary>§Q857 B「真实地点／区域」：放在安全屋屋内的遗物（Zone＝屋内）只在屋内可丢，出门后拒「当前无法丢弃该物品。」；同地点屋外遗物（Zone 空）屋内外都可丢。</summary>
    [Test]
    public void MT_RemainsInSafehouseZone_InsideOnly()
    {
        var s = New();
        s.Progress.Flags.Add("hub_registered");
        E.DebugBuildSafehouse(s);
        Assert.That(E.EnterSafehouse(s).Ok, Is.True);
        s.Remains["t_in"] = new RemainsState { Name = "试用者", LocationId = s.CurrentLocation, Minute = s.GameMinute, Cause = "测试", Zone = GameEngine.SafehouseZone };
        var inside = Give(s, "wood", ItemInstance.RemainsOwner("t_in"), 2);
        var outside = Give(s, "stone", Box(s, "t_out", null), 1);
        Assert.That(E.DiscardViewOf(s, DiscardTarget.Instance(inside.InstanceId)).Available, Is.EqualTo(2));
        Assert.That(E.DiscardViewOf(s, DiscardTarget.Instance(outside.InstanceId)).Available, Is.EqualTo(1));
        Assert.That(E.ExitSafehouse(s).Ok, Is.True);
        Assert.That(E.DiscardViewOf(s, DiscardTarget.Instance(inside.InstanceId)).Reasons, Is.EqualTo(new[] { NoPermission }));
        Assert.That(E.DiscardViewOf(s, DiscardTarget.Instance(outside.InstanceId)).Available, Is.EqualTo(1), "对照：屋外遗物在屋外仍可丢");
    }

    /// <summary>行已不存在（实例号找不到）时按该件目标，不当锅水处理（§Q857「只作用于点中的实际行」）。</summary>
    [Test]
    public void MT_RowTarget_MissingInstance_IsInstance()
    {
        var s = New();
        var t = E.DiscardRowTarget(s, int.MaxValue);
        Assert.That((t.Kind, t.InstanceId), Is.EqualTo((DiscardKind.Instance, (int?)int.MaxValue)));
    }

    /// <summary>
    /// §Q862 A「使用当前真实显示名」：同组目标范围已空（都被拿走）时标题仍用该物品的显示名；找不到的单件、不存在的物品为空名（不显示内部 ID）。
    /// </summary>
    [Test]
    public void MT_ViewName_EmptyScope()
    {
        var s = New();
        s.Items.RemoveAll(i => i.ItemId == "wood");
        var v = E.DiscardViewOf(s, DiscardTarget.Group(s.Ground, "wood", null));
        Assert.That((v.Total, v.Name), Is.EqualTo((0, "木材")));
        Assert.That(E.DiscardViewOf(s, DiscardTarget.Instance(int.MaxValue)).Name, Is.EqualTo(""));
        Assert.That(E.DiscardViewOf(s, DiscardTarget.Group(s.Ground, "no_such_item", null)).Name, Is.EqualTo(""));
    }

    /// <summary>壶水目标指向的已不是水壶（实例号被别的物品占用）时拒「物品已变，请重选。」（逐字稿 B50a changed）；对照：真水壶有水可丢。</summary>
    [Test]
    public void MT_CanteenWaterTarget_NotCanteen_Changed()
    {
        var s = New();
        var berry = Give(s, "berry", ItemInstance.Carried, 1);
        var v = E.DiscardViewOf(s, DiscardTarget.CanteenWater(berry.InstanceId));
        Assert.That((v.Available, v.Reasons), Is.EqualTo((0, new List<string> { "物品已变，请重选。" })));
        var c = Give(s, "ration_canteen", ItemInstance.Carried);
        c.LiquidId = "clean_water";
        c.LiquidMl = 750;
        Assert.That(E.DiscardViewOf(s, DiscardTarget.CanteenWater(c.InstanceId)).Available, Is.EqualTo(750));
    }

    /// <summary>
    /// §Q864 A＋§Q865 B「同摘要合并真实数量」：同单重的两堆腐败物合成一项「腐败物×3 · 0.20公斤/份」；同写法的三堆木材合成「木材×6」（不止前两堆）。
    /// </summary>
    [Test]
    public void MT_ListMerge_SameRot_ThreeHeaps()
    {
        var s = New();
        s.Items.RemoveAll(i => i.ItemId == Rot || i.ItemId == "wood");
        s.Items.Add(new ItemInstance { InstanceId = s.NextInstanceId++, ItemId = Rot, Count = 2, Owner = ItemInstance.Carried, SpoiledFrom = "berry", SpoiledUnitGrams = 200, SpoiledStackMax = 20 });
        s.Items.Add(new ItemInstance { InstanceId = s.NextInstanceId++, ItemId = Rot, Count = 1, Owner = ItemInstance.Carried, SpoiledFrom = "berry", SpoiledUnitGrams = 200, SpoiledStackMax = 20 });
        Assert.That(s.Items.Count(i => i.ItemId == Rot), Is.EqualTo(2));
        Assert.That(E.DiscardPreview(s, DiscardTarget.Group(ItemInstance.Carried, Rot, null), 0, true, out _)!.Lines[0], Is.EqualTo("永久丢弃：腐败物×3 · 0.20公斤/份。"));

        var w = Give(s, "wood", s.Ground, 6);
        E.SplitOff(s, w, 2);   // 起点：同写法拆成三堆（不经合堆）
        E.SplitOff(s, w, 2);
        Assert.That(s.Items.Count(i => i.ItemId == "wood" && i.Owner == s.Ground), Is.EqualTo(3));
        Assert.That(E.DiscardPreview(s, DiscardTarget.Group(s.Ground, "wood", null), 0, true, out _)!.Lines[0], Is.EqualTo("永久丢弃：木材×6。"));
    }

    /// <summary>
    /// §Q864 A 独立件逐件接现有状态摘要（拼法为 B53b.5 暂用）：耐久为 0 写「已损坏」；未定品质（按白色）省略品质；手枪接弹仓「弹{n}」；
    /// 有保质期的独立件（野犬尸体）接「保质至{截止}」。
    /// </summary>
    [Test]
    public void MT_PieceNote_BrokenNullQualityAmmoExpiry()
    {
        var s = New();
        s.Items.RemoveAll(i => i.Owner == s.Ground);
        string Line(ItemInstance i) => E.DiscardPreview(s, DiscardTarget.Instance(i.InstanceId), 0, true, out _)!.Lines[0];
        var broken = Give(s, "wooden_spear", s.Ground);
        broken.Quality = "white";
        broken.Durability = 0; broken.MaxDurability = 30;
        Assert.That(Line(broken), Is.EqualTo("永久丢弃：木矛×1（已损坏）。"));
        var plain = Give(s, "wooden_spear", s.Ground);
        plain.Quality = null;
        plain.Durability = 10; plain.MaxDurability = 30;
        Assert.That(Line(plain), Is.EqualTo("永久丢弃：木矛×1（耐久10.00/30.00）。"), "未定品质按白色省略");
        var gun = Give(s, "pistol", s.Ground);
        gun.Quality = "white";
        gun.Durability = 20; gun.MaxDurability = 40;
        gun.LoadedAmmo = 2;
        gun.LoadedAmmoId = "ammo_9mm";
        Assert.That(Line(gun), Is.EqualTo("永久丢弃：手枪×1（耐久20.00/40.00 · 弹2）。"));
        var dog = Give(s, "carcass_feral_dog", s.Ground);
        dog.ExpiresAt = At(2, 8, 0);
        dog.Durability = null; dog.MaxDurability = null;
        Assert.That(Line(dog), Is.EqualTo("永久丢弃：野犬尸体×1（保质至第2日 08:00）。"));
    }

    /// <summary>§Q864 A：单项显示文字与确认清单同源（「{物名}×{数量}（保质至{截止}）」，数量为传入的真实份数）。</summary>
    [Test]
    public void MT_ItemLabel_SameAsList()
    {
        var s = New();
        var b = Give(s, "berry", ItemInstance.Carried, 3);
        b.ExpiresAt = At(2, 8, 0);
        Assert.That(E.DiscardItemLabel(s, b, 2), Is.EqualTo("野果×2（保质至第2日 08:00）"));
        var spear = Give(s, "wooden_spear", ItemInstance.Carried);
        spear.Quality = "green";
        spear.Durability = 20; spear.MaxDurability = 30;
        Assert.That(E.DiscardItemLabel(s, spear, 1), Is.EqualTo("木矛×1（绿 · 耐久20.00/30.00）"));
    }

    /// <summary>
    /// 试玩修复 B7：换地点再点试用组，四种箱放在当前地点（日志里的地点＝实际所在），不往前一地点的旧箱里加；同一地点再点复用当地箱、不另开第二组。
    /// </summary>
    [Test]
    public void Fix7_DevKit_BoxesFollowCurrentLocation()
    {
        var s = New();
        var first = s.CurrentLocation;
        E.DebugDiscardListKit(s);
        int Here(GameState g) => E.RemainsHere(g).Count(kv => kv.Key.StartsWith("dev_b53b_"));
        int ItemsIn(GameState g, string loc) => g.Items.Count(i => i.InRemains && g.Remains.TryGetValue(i.Owner.Substring(ItemInstance.RemainsPrefix.Length), out var r) && r.LocationId == loc && r.Cause == "开发试用");
        var firstItems = ItemsIn(s, first);
        Assert.That((Here(s), firstItems), Is.EqualTo((4, 6)), "直接放在箱里的堆数：遗物（野果、木材）2、认领（罐头、借用木矛）2、回收（生肉）1、旧实例（金属锅；脏水在锅里不算）1");
        E.DebugTeleport(s, "field");
        Assert.That(Here(s), Is.EqualTo(0), "前提：新地点还没有试用箱");
        var text = E.DebugDiscardListKit(s);
        Assert.That(Here(s), Is.EqualTo(4));
        Assert.That(ItemsIn(s, "field"), Is.EqualTo(6));
        Assert.That(ItemsIn(s, first), Is.EqualTo(firstItems), "前一地点的旧箱不变");
        Assert.That(text, Does.Contain(E.LocName("field") + "遗物（野果2、木材3）"));
        E.DebugDiscardListKit(s);
        Assert.That(Here(s), Is.EqualTo(4), "同一地点复用当地箱");
    }

    // ───────────── 试玩修复（M1060，本地 Release 1280×720） ─────────────

    /// <summary>
    /// 试玩修复 B1（开发假设 3「切走即清草稿」；§Q857 B「关闭返回真实发起清单」）：人物页清单（同伴随身、遗物／认领箱、回收箱）开着的丢弃子区，
    /// 右栏离开人物页（任何改页签的路径都经 DetailTab 设值）或打开／换到别的窗口时清草稿；柜子、封存沿原规则随所在窗离开清。
    /// </summary>
    [Test]
    public void Fix1_PartyDraft_ClearedOnTabOrPanelSwitch()
    {
        var sess = Web("Services", "GameSession.cs");
        var prop = Section(sess, "public DetailTab DetailTab", "private DetailTab _detailTab = DetailTab.Character;");
        Assert.That(prop, Does.Contain("if (value != DetailTab.Character && _discard?.ListHost == DiscardHost.Party) _discard = null;"));
        Assert.That(prop, Does.Contain("_detailTab = value;"));
        Assert.That(sess, Does.Not.Contain("public DetailTab DetailTab { get; private set; }"), "所有改页签都经设值器");
        Assert.That(sess, Does.Contain("if (_discard?.ListHost == DiscardHost.Party && next != null && next != from) _discard = null;"));
        Assert.That(sess, Does.Contain("if (_discard?.ListHost is DiscardHost.Cabinet or DiscardHost.Sealed && next != from) _discard = null;"), "对照：柜子／封存原规则不变");
    }

    /// <summary>试玩修复 B2：同伴随身清单开着本人行的丢弃子区时放开内层 200px 限高，子区（标题到确认／返回）完整可见；没开子区时仍限高。</summary>
    [Test]
    public void Fix2_PartyItems_ReleaseHeightWhileSubOpen()
    {
        var party = Web("Components", "PartyPanel.razor");
        Assert.That(party, Does.Contain("var subHere = G.Discarding is { ListHost: DiscardHost.Party } dd && rows.Any(r => r.InstanceId == dd.Target.InstanceId);"));
        Assert.That(party, Does.Contain("<div class=\"party-items @(subHere ? \"has-discard\" : \"\")\" data-wheel=\"y\" data-party-items=\"@id\">"));
        var css = Web("wwwroot", "css", "app.css");
        Assert.That(css, Does.Contain(".party-items { max-height: 200px; overflow-y: auto;"), "对照：平时仍限高");
        Assert.That(css, Does.Contain(".party-items.has-discard { max-height: none; overflow-y: visible; }"));
    }

    /// <summary>试玩修复 B3：清单子区展开或换层（键含阶段与请求号）后滚到可见，最近边对齐（已可见不动）；右栏子区不走此路径。</summary>
    [Test]
    public void Fix3_ListSub_RevealOnOpenAndStage()
    {
        var p = Web("Components", "DiscardPanel.razor");
        Assert.That(p, Does.Contain("var key = Host != null && G.DiscardOpenFor(Target, Host) && G.Discarding is DiscardDraft d ? $\"{Target.Key}|{d.Stage}|{d.RequestId}\" : null;"));
        Assert.That(p, Does.Contain("if (key == _shown) return;"));
        Assert.That(p, Does.Contain("await JS.InvokeVoidAsync(\"survival.revealNearest\", _sub);"));
        Assert.That(p, Does.Contain("<section @ref=\"_sub\" class=\"discard-sub"));
        var js = Web("wwwroot", "js", "app.js");
        Assert.That(Section(js, "revealNearest: function (el) {", "},"), Does.Contain("el.scrollIntoView({ block: 'nearest', inline: 'nearest' });"));
    }

    /// <summary>试玩修复 B5：本批新增的行末「丢弃／丢弃水」与清单子区内按钮（含数量加减）热区不低于 40px；同行原有按钮不改。</summary>
    [Test]
    public void Fix5_HitAreas_AtLeast40px()
    {
        var css = Web("wwwroot", "css", "app.css");
        Assert.That(css, Does.Contain(".eq-btn.discard-row-btn, .linkish.discard-row-btn { min-height: 40px; min-width: 44px; }"));
        Assert.That(css, Does.Contain(".discard-sub.in-list .discard-row > button { flex: 1 1 7em; min-height: 40px;"));
        Assert.That(css, Does.Contain(".discard-sub.in-list .discard-qty button { min-width: 40px; min-height: 40px; }"));
        Assert.That(css, Does.Contain(".party-items-btn { align-self: flex-start; min-height: 26px; padding: 0 6px; }"), "对照：同行原按钮不动");
    }
}
