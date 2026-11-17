using Newtonsoft.Json.Linq;
using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 批次 B53a「永久丢弃与晾制待清理」（开发记录 B53a）：S06／S10／S11／S12／S14／逐字稿文首「2026-11-15 B50a Q763—Q768与永久丢弃」与
/// 「2026-11-17 B53主要原批Q857—Q861」中有右栏的入口（随身、当前地面、锅内食材、锅水、壶水）；合同 assets/20261117/b53-discard-main-q857-q861.json。
/// 无右栏清单行末按钮（同伴随身／柜子／遗物认领／回收／封存）属 B53b，不在这里。
/// </summary>
public class B53aTests : TestBase
{
    protected override bool NoEnemyApproach => true;

    private const string Rot = GameEngine.SpoiledMatterId;
    private const string Lin = "npc_lin_zhao";

    // 逐字稿「2026-11-15 B50a」表 rotNotEdible；S12 文首 Q767
    private const string NotEdible = "腐败物不可食用。";

    private static string Bag(string actor) => ItemInstance.CarriedOwner(actor);

    private ItemInstance Give(GameState s, string item, string owner, int count = 1) => E.CreateItems(s, item, count, owner).Single();

    /// <summary>空闲等待 minutes 分钟（按 5 分钟段推进，世界流程里做到期转换）。</summary>
    private void Wait(GameState s, int minutes)
    {
        var until = s.GameMinute + minutes;
        while (s.GameMinute < until)
        {
            if (s.Main == null) Start(s, ActionRequest.Wait(30));
            E.Advance(s);
        }
        if (s.Main?.Type == ActionType.Wait) E.Cancel(s);
    }

    /// <summary>野果在 5 分钟后到期，等一段让世界流程把它真实转为腐败物（不直接写 ItemId）。</summary>
    private ItemInstance RotBerries(GameState s, string owner, int count)
    {
        var b = Give(s, "berry", owner, count);
        b.ExpiresAt = s.GameMinute + 5;
        Wait(s, 5);
        var rot = s.Items.Single(i => i.InstanceId == b.InstanceId);
        Assert.That(rot.ItemId, Is.EqualTo(Rot), "前提：到期后原位转为腐败物");
        return rot;
    }

    private GameState Party()
    {
        var s = New();
        s.Progress.Flags.Add("hub_registered");
        s.Progress.Flags.Add("church_route_record");
        s.Progress.Flags.Add("academy_safety_record");
        s.SpawnNextCheck = int.MaxValue;
        s.PatrolNextCheck = int.MaxValue;
        s.GameMinute = At(1, 10, 0);
        E.DebugRecruit(s, Lin);
        s.Items.RemoveAll(i => i.Owner == Bag(Lin));
        foreach (var a in s.Actors.Values)
        {
            a.Stats.Satiety = 50;
            a.Stats.Hydration = 50;
        }
        return s;
    }

    // ───────────── M1 Q767：腐败物不能食用／喂食，同伴行去重复括注 ─────────────

    /// <summary>Q767-A01：本人食用腐败物，预检与真实提交都拒「腐败物不可食用。」，不扣物、不改属性、不耗时间。</summary>
    [Test]
    public void Q767_A01_SelfUse_RotRejected_NothingChanges()
    {
        var s = New();
        var rot = RotBerries(s, ItemInstance.Carried, 2);
        Assert.That(E.CanStart(s, ActionRequest.UseInstance(rot.InstanceId)), Is.EqualTo(NotEdible));
        var before = SaveSerializer.Serialize(s);
        var res = E.Start(s, ActionRequest.UseInstance(rot.InstanceId));
        Assert.That((res.Ok, res.Reason), Is.EqualTo((false, NotEdible)));
        Assert.That(SaveSerializer.Serialize(s), Is.EqualTo(before), "拒绝不扣物、不改人物、不推进时间");
        // 按物品 ID 的旧请求同样拒绝
        Assert.That(E.CanStart(s, ActionRequest.UseItem(Rot)), Is.EqualTo(NotEdible));
    }

    /// <summary>对照：同样的新鲜野果可以吃（证明上面的拒绝走的是腐败物判定，不是别的阻断）。</summary>
    [Test]
    public void Q767_A01_SelfUse_FreshBerry_Allowed()
    {
        var s = New();
        var b = Give(s, "berry", ItemInstance.Carried, 2);
        Assert.That(E.CanStart(s, ActionRequest.UseInstance(b.InstanceId)), Is.Null);
    }

    /// <summary>Q767-A01：屠宰等其他原用途仍是原拒绝句（S06 只改食饮）。</summary>
    [Test]
    public void Q767_Butcher_KeepsOriginalRejectText()
    {
        var s = New();
        Give(s, "hatchet", ItemInstance.Carried);
        var dog = Give(s, "carcass_feral_dog", s.Ground);
        dog.ExpiresAt = s.GameMinute + 5;
        Wait(s, 5);
        Assert.That(s.Items.Single(i => i.InstanceId == dog.InstanceId).ItemId, Is.EqualTo(Rot));
        Assert.That(E.CanStart(s, ActionRequest.Butcher(dog.InstanceId)), Is.EqualTo("腐败物不能用于此操作。"));
    }

    /// <summary>Q767-A01：给同伴喂腐败物（直接实例请求）拒「腐败物不可食用。」，不扣物、不改人、不耗时。</summary>
    [Test]
    public void Q767_A01_Feed_RotRejected()
    {
        var s = Party();
        var rot = RotBerries(s, ItemInstance.Carried, 1);
        var f = new FeedRequest { TargetActorId = Lin, SourceActorId = GameState.PlayerId, InstanceId = rot.InstanceId };
        var before = SaveSerializer.Serialize(s);
        Assert.That(E.FeedBlocked(s, f), Is.EqualTo(NotEdible));
        var res = E.Feed(s, f);
        Assert.That((res.Ok, res.Reason), Is.EqualTo((false, NotEdible)));
        Assert.That(SaveSerializer.Serialize(s), Is.EqualTo(before));
        Assert.That(E.FeedSources(s, Lin).Any(r => r.InstanceId == rot.InstanceId), Is.False, "腐败物不进合法喂食候选");
    }

    /// <summary>Q767-A01：预览时还是新鲜野果，确认前变成腐败物——真实提交拒「腐败物不可食用。」（不是「情况已变」）；对照：未变腐败时可以喂。</summary>
    [Test]
    public void Q767_A01_Feed_TurnedRotAfterPreview_RejectedAsNotEdible()
    {
        var s = Party();
        var b = Give(s, "berry", ItemInstance.Carried, 1);
        var f = new FeedRequest { TargetActorId = Lin, SourceActorId = GameState.PlayerId, InstanceId = b.InstanceId };
        var p = E.FeedPreview(s, f);
        Assert.That(p.Ok, Is.True, p.Reason);
        f.ConfirmStamp = p.Feed!.Source.Stamp;
        Assert.That(E.FeedBlocked(s, f), Is.Null, "对照：没变腐败时确认可通过");

        b.ExpiresAt = s.GameMinute + 5;
        Wait(s, 5);
        Assert.That(b.ItemId, Is.EqualTo(Rot));
        var res = E.Feed(s, f);
        Assert.That((res.Ok, res.Reason), Is.EqualTo((false, NotEdible)));
        Assert.That(b.Count, Is.EqualTo(1));
    }

    /// <summary>Q767 选 A：同伴随身腐败物行只写名称与数量，不再接「（已腐败）」；尚未转换的过期原物仍附（S06「不全局删词」）。</summary>
    [Test]
    public void Q767_CompanionRow_RotHasNoSuffix_ExpiredOriginalKeeps()
    {
        var s = Party();
        var rot = RotBerries(s, Bag(Lin), 2);
        Assert.That(E.CompanionItemLine(s, rot), Is.EqualTo("腐败物×2"));
        var old = Give(s, "berry", Bag(Lin), 2);
        old.ExpiresAt = s.GameMinute;   // 到期但还没经过转换事务的原物
        Assert.That(E.CompanionItemLine(s, old), Is.EqualTo("野果×2（已腐败）"));
    }

    // ───────────── M2 存档 56（Q764）、待清理（Q765／Q861）、帮助（Q768） ─────────────

    private static string Web(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "Survival.Web" }.Concat(parts).ToArray()));

    private static string AsSchema(GameState s, string version)
    {
        var o = JObject.Parse(SaveSerializer.Serialize(s));
        o["SchemaVersion"] = version;
        return o.ToString();
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

    private static List<DryingSlot> DrySlots(GameState s) => s.Locations[s.Ground].Facility.DryingSlots;

    /// <summary>造一个已停止的晾制位（旧档／异常起点，只造起点不造结果）。</summary>
    private static DryingSlot Stopped(GameState s, int k, string recipe, string? reason)
    {
        var b = new DryingSlot { Slot = k, BatchId = 5300 + k, RecipeId = recipe, Status = DryingSlot.Spoiled, StartMinute = s.GameMinute - 60,
                                 DoneMinutes = 55, RequiredMinutes = 120, EndMinute = s.GameMinute - 5, InitiatorActorId = GameState.PlayerId, StopReason = reason };
        DrySlots(s).Add(b);
        return b;
    }

    /// <summary>位内一份原物已转成的腐败物（旧档起点：原物 SpoiledFrom 是原业务字段）。</summary>
    private static ItemInstance RotIn(GameState s, string owner, string from, int count)
    {
        var i = new ItemInstance { InstanceId = s.NextInstanceId++, ItemId = Rot, Count = count, Owner = owner, SpoiledFrom = from, SpoiledUnitGrams = 100, SpoiledStackMax = 10 };
        s.Items.Add(i);
        return i;
    }

    [Test]
    public void Schema56_Current()
    {
        // 开发假设：B53a 把存档升到 56（Q764 迁移）；字面量对照，防止版本号被别处挪动
        Assert.That(GameState.CurrentSchema, Is.EqualTo("56"));
    }

    /// <summary>
    /// Q764-A01（S11 文首 2026-11-15 §Q764 选 A）：缺可信原因的旧停止位按当前可证明实物三分支补一次原因——
    /// 原料不齐且无腐败物＝缺料；齐料且有腐败物＝腐败；其他＝未知；已有可信原因不覆盖；不改实物、不写说明、反复读档不再变。
    /// </summary>
    [Test]
    public void Q764_A01_LegacyStoppedSlots_ClassifiedOnce()
    {
        var s = Rack();
        var place = s.Ground;
        string O(int k) => ItemInstance.DryingOwner(place, k);
        // 工位1 肉干：只剩盐，无腐败物 → 缺料
        Stopped(s, 0, "dry_meat", DryingSlot.StopLegacy);
        Give(s, "salt", O(0));
        // 工位2 肉干：生肉已成腐败物＋盐 → 齐料且有腐败物 → 腐败
        Stopped(s, 1, "dry_meat", DryingSlot.StopLegacy);
        RotIn(s, O(1), "raw_meat", 1);
        Give(s, "salt", O(1));
        // 工位3 果干：只剩 1 份腐败野果 → 不齐但有腐败物 → 未知
        Stopped(s, 2, "dry_berries", DryingSlot.StopLegacy);
        RotIn(s, O(2), "berry", 1);
        // 工位4 果干：野果 2 份完好 → 齐但无腐败物 → 未知
        Stopped(s, 3, "dry_berries", DryingSlot.StopLegacy);
        var fresh = Give(s, "berry", O(3), 2);
        fresh.ExpiresAt = null;
        fresh.ShelfPausedMinutes = 300;
        // 工位5 已有可信原因「缺料」，即使现在齐料又有腐败物也不覆盖
        Stopped(s, 4, "dry_meat", DryingSlot.StopMissing);
        RotIn(s, O(4), "raw_meat", 1);
        Give(s, "salt", O(4));
        // 工位6 原因字段为空的旧停止位同样补：只剩盐 → 缺料
        Stopped(s, 5, "dry_meat", null);
        Give(s, "salt", O(5));
        var items = s.Items.Select(i => (i.InstanceId, i.ItemId, i.Count, i.Owner)).ToList();
        var logs = s.Log.Count;

        var t = SaveSerializer.Deserialize(AsSchema(s, "55"), Cfg);
        Assert.That(t.SchemaVersion, Is.EqualTo("56"));
        var reasons = DrySlots(t).OrderBy(b => b.Slot).Select(b => b.StopReason).ToList();
        Assert.That(reasons, Is.EqualTo(new[] { "missing", "spoiled", "unknown", "unknown", "missing", "missing" }));
        Assert.That(t.Items.Select(i => (i.InstanceId, i.ItemId, i.Count, i.Owner)), Is.EquivalentTo(items), "只补原因，不改实物");
        Assert.That(t.Log.Count, Is.EqualTo(logs), "静默");
        Assert.That(DrySlots(t).Select(b => b.Status), Is.All.EqualTo(DryingSlot.Spoiled), "不复活");

        var again = SaveSerializer.Deserialize(SaveSerializer.Serialize(t), Cfg);
        Assert.That(DrySlots(again).OrderBy(b => b.Slot).Select(b => b.StopReason), Is.EqualTo(reasons), "56 档读档不再重判");
    }

    /// <summary>
    /// Q765-A03／Q861 选 A（B53-Q861-01）：已停止位的标题短状态都写「待清理」；架摘要仍按真实原因分「缺料{n}／停止{n}／腐败{n}」工位数。
    /// </summary>
    [Test]
    public void Q861_01_StoppedSlots_ShortStatusPending_SummaryKeepsCauses()
    {
        var s = Rack();
        Stopped(s, 0, "dry_meat", DryingSlot.StopMissing);
        Stopped(s, 1, "dry_meat", DryingSlot.StopMissing);
        Stopped(s, 2, "dry_berries", DryingSlot.StopUnknown);
        Stopped(s, 3, "dry_berries", DryingSlot.StopSpoiled);
        var views = E.DryingSlotViews(s);
        Assert.That(views.Take(4).Select(v => CardStates.DryingStatusText(v.Status)), Is.All.EqualTo("待清理"));
        Assert.That(views.Take(4).Select(v => v.StopKind), Is.EqualTo(new[] { "missing", "missing", "unknown", "spoiled" }), "各位原实际原因仍可查看");
        Assert.That(E.DryingSummaryParts(s), Is.EqualTo(new[] { "缺料2", "停止1", "腐败1", "空4" }), "不合并成待清理总数");
    }

    /// <summary>B53-Q861-01 对照：没有异常位时摘要不出异常计数，正常晾制位短状态仍是晾制中；正常齐料晾制越过原截止照常晾好（Q765-A01）。</summary>
    [Test]
    public void Q861_01_NoAbnormal_NoCounts_NormalDryingStillProtected()
    {
        var s = Rack();
        var t0 = s.GameMinute;
        var b = Give(s, "berry", ItemInstance.Carried, 2);
        b.ExpiresAt = t0 + 30;   // 原截止早于晾制完成
        Assert.That(E.StartDrying(s, "dry_berries").Ok, Is.True);
        var v = E.DryingSlotViews(s)[0];
        Assert.That(CardStates.DryingStatusText(v.Status), Is.EqualTo("晾制中"));
        Assert.That(E.DryingSummaryParts(s), Is.EqualTo(new[] { "晾制中1", "空7" }));
        AdvanceTo(s, t0 + E.Recipe("dry_berries").BaseMinutes + 5);
        Assert.That(DrySlots(s).Single().Status, Is.EqualTo(DryingSlot.Done), "越过原截止不缺料、不腐败停止");
        Assert.That(E.DryingSummaryParts(s), Is.EqualTo(new[] { "待取1", "空7" }));
    }

    /// <summary>Q765-A03：缺料／未知停止位里确有腐败物时，位内说明与清理日志末各接一次「腐败物不可食用。」。</summary>
    [Test]
    public void Q765_A03_RotInStoppedSlot_NotEdibleOnceInLineAndCleanupLog()
    {
        var s = Rack();
        Stopped(s, 0, "dry_berries", DryingSlot.StopUnknown);
        RotIn(s, ItemInstance.DryingOwner(s.Ground, 0), "berry", 1);
        var line = E.DryingSlotViews(s)[0].Line;
        Assert.That(line, Is.EqualTo($"{E.Recipe("dry_berries").Name}已停止，无成品；清理可取回剩余物品。腐败物不可食用。"));
        Assert.That(E.TakeDrying(s, 0, false).Ok, Is.True);
        Assert.That(s.Log.Last().Text, Does.Match(@"^清理.+晾制架工位1：腐败物×1取到.+，该位已空。腐败物不可食用。$"));
    }

    /// <summary>Q765-A03 对照：位内没有腐败物时不接「腐败物不可食用。」。</summary>
    [Test]
    public void Q765_A03_NoRot_NoNotEdibleNote()
    {
        var s = Rack();
        Stopped(s, 0, "dry_meat", DryingSlot.StopMissing);
        Give(s, "salt", ItemInstance.DryingOwner(s.Ground, 0));
        Assert.That(E.DryingSlotViews(s)[0].Line, Is.EqualTo($"{E.Recipe("dry_meat").Name}已因缺料停止，无成品；清理可取回剩余物品。"));
        Assert.That(E.TakeDrying(s, 0, false).Ok, Is.True);
        Assert.That(s.Log.Last().Text, Does.Not.Contain(NotEdible));
    }

    /// <summary>Q768-A01：面板 ⓘ 撤下旧果干无保护句，换被动信息＋晾制保质短句；系统说明「保质与腐败」补同源短句一次。</summary>
    [Test]
    public void Q768_A01_HelpTexts()
    {
        // 逐字稿 2026-11-15 B50a 表 dryShelf／dryPassive
        Assert.That(GameEngine.DryingShelfHelpText, Is.EqualTo("晾制时原料暂停保质，退料恢复剩余时长；成品从完成起计时，留在架上也会到期。"));
        Assert.That(GameEngine.DryingPassiveText, Is.EqualTo("按世界时间推进，不占行动；离开继续，暂停不推进。"));
        var overlay = Web("Components", "DryingOverlay.razor");
        Assert.That(overlay, Does.Contain("title=\"@(GameEngine.DryingPassiveText + GameEngine.DryingShelfHelpText)\""));
        Assert.That(overlay, Does.Not.Contain("果干原料没有受热保护"));
        var help = Web("Services", "HelpCatalog.cs");
        var spoil = help.Substring(help.IndexOf("Title = \"保质与腐败\"", StringComparison.Ordinal));
        spoil = spoil.Substring(0, spoil.IndexOf("new HelpEntry", StringComparison.Ordinal));
        Assert.That(System.Text.RegularExpressions.Regex.Matches(spoil, "GameEngine.DryingShelfHelpText").Count, Is.EqualTo(1));
    }

    // ───────────── M3 永久丢弃内核（PD-A01—A07、B53-Q857-02—Q860-01） ─────────────
    // 正式句：逐字稿「2026-11-15 B50a」表与「2026-11-17 B53」§Q859 水句（b53-discard-main-q857-q861.json water.text）

    private const string Changed = "物品已变，请重选。";
    private const string NoPermission = "当前无法丢弃该物品。";
    private const string Processing = "加工中：先停止加工。";
    private const string Busy = "物品被占用，不能丢弃。";
    private const string Worn = "先卸下再丢弃。";
    private const string Deployed = "先收起再丢弃。";
    private const string NoWater = "无可丢弃的水。";

    /// <summary>确认一次：取最终确认清单、带快照提交。</summary>
    private ActionResult Confirm(GameState s, DiscardTarget t, int count, bool all, string? requestId = null)
    {
        var p = E.DiscardPreview(s, t, count, all, out var why);
        Assert.That(p, Is.Not.Null, why);
        return E.Discard(s, new DiscardRequest { Target = t, Count = count, All = all, Stamp = p!.Stamp, RequestId = requestId });
    }

    /// <summary>同组临期野果 3 份（第 2 日 08:00 到期）、新鲜野果 5 份（第 3 日 08:00 到期），放在当前地面。</summary>
    private (ItemInstance near, ItemInstance far) Berries35(GameState s)
    {
        s.Items.RemoveAll(i => i.ItemId == "berry");
        var far = Give(s, "berry", s.Ground, 8);
        far.ExpiresAt = At(3, 8, 0);
        var near = E.SplitOff(s, far, 3);   // 起点：同组拆成两批（不经合堆）
        near.ExpiresAt = At(2, 8, 0);
        return (near, far);
    }

    private int Berries(GameState s) => s.Items.Where(i => i.ItemId == "berry").Sum(i => i.Count);

    /// <summary>B53-Q858-01：野果 3＋5，未手选「全部丢弃」删 8；手选临期批「全部丢弃」只删 3、新鲜批不动。</summary>
    [Test]
    public void Q858_01_NoPick_AllDeletes8_PickedNearBatch_AllDeletes3()
    {
        var s = New();
        var (near, far) = Berries35(s);
        var group = DiscardTarget.Group(s.Ground, "berry", null);
        Assert.That(E.DiscardViewOf(s, group).Available, Is.EqualTo(8));
        var picked = DiscardTarget.Group(s.Ground, "berry", null, GameEngine.DiscardBatchKeyOf(near));
        Assert.That(E.DiscardViewOf(s, picked).Available, Is.EqualTo(3));
        Assert.That(Confirm(s, picked, 0, true).Ok, Is.True);
        Assert.That(Berries(s), Is.EqualTo(5), "只删手选的临期批 3 份");
        Assert.That(s.Items.Single(i => i.ItemId == "berry").ExpiresAt, Is.EqualTo(At(3, 8, 0)), "新鲜批原样");

        var t = New();
        Berries35(t);
        Assert.That(Confirm(t, DiscardTarget.Group(t.Ground, "berry", null), 0, true).Ok, Is.True);
        Assert.That(Berries(t), Is.EqualTo(0), "未手选：同组全部 8 份");
    }

    /// <summary>B53-Q858-01：手选的临期批里有 1 份被预留，该批最多删 2，不拿新鲜批补；预留那份不动。</summary>
    [Test]
    public void Q858_01_PickedBatchWithReserved_Only2_NoTopUp()
    {
        var s = New();
        var (near, far) = Berries35(s);
        var held = E.SplitOff(s, near, 1);
        held.ReservedBy = 777;   // 起点：这一份被行动预留
        var picked = DiscardTarget.Group(s.Ground, "berry", null, GameEngine.DiscardBatchKeyOf(near));
        Assert.That(GameEngine.DiscardBatchKeyOf(held), Is.EqualTo(GameEngine.DiscardBatchKeyOf(near)), "预留份与本批同一批");
        var v = E.DiscardViewOf(s, picked);
        Assert.That((v.Total, v.Available, v.MaxUnits), Is.EqualTo((3, 2, 2)));
        Assert.That(E.DiscardPreview(s, picked, 3, false, out var why), Is.Null);
        Assert.That(why, Is.EqualTo("请输入1至2的整数。"), "数量超出用原句，变量为可丢弃数");
        Assert.That(Confirm(s, picked, 0, true).Ok, Is.True);
        Assert.That(held.Count, Is.EqualTo(1), "预留份不动");
        Assert.That(far.Count, Is.EqualTo(5), "不拿另一批补足");
        Assert.That(Berries(s), Is.EqualTo(6));
    }

    /// <summary>PD-A02：同组总 9、预留 2 → 全部最多删 7；按真实份数，不按牌组张数。</summary>
    [Test]
    public void PD_A02_Total9Reserved2_AllDeletes7()
    {
        var s = New();
        s.Items.RemoveAll(i => i.ItemId == "wood");
        var w = Give(s, "wood", s.Ground, 9);
        var held = E.SplitOff(s, w, 2);
        held.ReservedBy = 778;
        var t = DiscardTarget.Group(s.Ground, "wood", null);
        var v = E.DiscardViewOf(s, t);
        Assert.That((v.Total, v.Available), Is.EqualTo((9, 7)));
        Assert.That(Confirm(s, t, 0, true).Ok, Is.True);
        Assert.That(s.Items.Where(i => i.ItemId == "wood").Select(i => (i.Count, i.ReservedBy)), Is.EqualTo(new[] { (2, (int?)778) }));
    }

    /// <summary>选择数量 N 按既定序（早到期先）取前 N，末批只拆缺额。</summary>
    [Test]
    public void PD_A02_ChooseQuantity_TakesEarliestFirst()
    {
        var s = New();
        var (near, far) = Berries35(s);
        Assert.That(Confirm(s, DiscardTarget.Group(s.Ground, "berry", null), 4, false).Ok, Is.True);
        Assert.That(s.Items.Where(i => i.ItemId == "berry").Select(i => (i.Count, i.ExpiresAt)), Is.EqualTo(new[] { (4, (int?)At(3, 8, 0)) }), "先删临期 3，再从新鲜批拆 1");
    }

    /// <summary>B53-Q858-02：手选的批已没有了（例如被用完）——不改成整组、不换邻批：不能确认；新鲜批原样。</summary>
    [Test]
    public void Q858_02_PickedBatchGone_NoFallbackToGroup()
    {
        var s = New();
        var (near, far) = Berries35(s);
        var picked = DiscardTarget.Group(s.Ground, "berry", null, GameEngine.DiscardBatchKeyOf(near));
        s.Items.Remove(near);
        var v = E.DiscardViewOf(s, picked);
        Assert.That((v.Available, v.Blocked), Is.EqualTo((0, Changed)));
        Assert.That(E.DiscardPreview(s, picked, 0, true, out _), Is.Null);
        Assert.That(far.Count, Is.EqualTo(5));
    }

    /// <summary>B53-Q858-03：确认后数量、占用、物主、腐败变种变化都整次拒绝「物品已变，请重选。」，不部分删、不换邻批。</summary>
    [Test]
    public void Q858_03_ChangedAfterConfirm_WholeReject()
    {
        foreach (var change in new Action<GameState, ItemInstance, ItemInstance>[]
                 {
                     (s, n, f) => n.Count = 2,                         // 数量变了
                     (s, n, f) => n.ReservedBy = 779,                  // 被占用
                     (s, n, f) => n.Owner = ItemInstance.Carried,      // 换了物主（被拾起）
                     (s, n, f) => n.ExpiresAt = s.GameMinute,          // 到期：提交前先转为腐败物
                 })
        {
            var s = New();
            var (near, far) = Berries35(s);
            var t = DiscardTarget.Group(s.Ground, "berry", null);
            var p = E.DiscardPreview(s, t, 0, true, out _)!;
            change(s, near, far);
            var before = s.Items.Select(i => (i.InstanceId, i.ItemId, i.Count, i.Owner)).ToList();
            var logs = s.Log.Count;
            var res = E.Discard(s, new DiscardRequest { Target = t, All = true, Stamp = p.Stamp });
            Assert.That((res.Ok, res.Reason), Is.EqualTo((false, Changed)));
            Assert.That(far.Count, Is.EqualTo(5), "不部分删、不换邻批");
            Assert.That(s.Log.Count(l => l.Text.StartsWith("已永久丢弃")), Is.EqualTo(0));
        }
    }

    /// <summary>B53-Q858-03 对照与幂等：未变化时同快照删除成功、写一条日志；同请求号重发不再删、不再写日志。</summary>
    [Test]
    public void Q858_03_SameRequestTwice_DeletesOnce_OneLog()
    {
        var s = New();
        var (near, far) = Berries35(s);
        var t = DiscardTarget.Group(s.Ground, "berry", null);
        var p = E.DiscardPreview(s, t, 2, false, out _)!;
        var r = new DiscardRequest { Target = t, Count = 2, Stamp = p.Stamp, RequestId = "dq-1" };
        Assert.That(E.Discard(s, r).Ok, Is.True);
        Assert.That(Berries(s), Is.EqualTo(6));
        var again = E.Discard(s, r);
        Assert.That((again.Ok, again.Duplicate), Is.EqualTo((true, true)));
        Assert.That(Berries(s), Is.EqualTo(6), "不双删");
        Assert.That(s.Log.Count(l => l.Text.StartsWith("已永久丢弃")), Is.EqualTo(1), "只一条成功日志");
        var back = SaveSerializer.Deserialize(SaveSerializer.Serialize(s), Cfg);
        Assert.That(E.Discard(back, r).Duplicate, Is.True, "读档后同请求仍不重复");
    }

    /// <summary>PD-A01：只读预览与打开子区不删除；0 世界分钟；成功日志写真实清单（暂用逐项写法）。</summary>
    [Test]
    public void PD_A01_PreviewDoesNothing_OnlyConfirmDeletes_ZeroMinutes()
    {
        var s = New();
        Berries35(s);
        var t = DiscardTarget.Group(s.Ground, "berry", null);
        var before = SaveSerializer.Serialize(s);
        E.DiscardViewOf(s, t);
        var p = E.DiscardPreview(s, t, 4, false, out _)!;
        Assert.That(SaveSerializer.Serialize(s), Is.EqualTo(before), "预览与子区只读");
        Assert.That(p.Lines, Is.EqualTo(new[] { "永久丢弃：野果（保质到第2日 08:00）×3、野果（保质到第3日 08:00）×1。", "丢弃后无法找回。" }));
        var minute = s.GameMinute;
        Assert.That(E.Discard(s, new DiscardRequest { Target = t, Count = 4, Stamp = p.Stamp }).Ok, Is.True);
        Assert.That(s.GameMinute, Is.EqualTo(minute), "0 世界分钟");
        Assert.That(s.Main, Is.Null, "不开行动");
        Assert.That(s.Log.Last().Text, Is.EqualTo("已永久丢弃：野果（保质到第2日 08:00）×3、野果（保质到第3日 08:00）×1。"));
        Assert.That(s.Items.Any(i => i.ItemId == "berry" && i.Owner != s.Ground), Is.False, "不进随身／别处");
    }

    /// <summary>PD-A03：穿戴、部署、借用、行动输入锁各自拒绝；对照：同类普通物可丢。</summary>
    [Test]
    public void PD_A03_WornDeployedLoanAndInputLock_Rejected()
    {
        var s = New();
        var spear = Give(s, "wooden_spear", ItemInstance.Carried);
        Assert.That(E.DiscardViewOf(s, DiscardTarget.Instance(spear.InstanceId)).Blocked, Is.Null, "对照：普通木矛可丢");
        spear.LoanSlot = "loan_1";   // 起点：借用矛
        Assert.That(E.DiscardViewOf(s, DiscardTarget.Instance(spear.InstanceId)).Blocked, Is.EqualTo(NoPermission));
        spear.LoanSlot = null;
        spear.Owner = ItemInstance.EquipOwner(GameState.PlayerId, EquipSlots.Weapon1);   // 起点：穿戴中
        Assert.That(E.DiscardViewOf(s, DiscardTarget.Instance(spear.InstanceId)).Blocked, Is.EqualTo(Worn));
        var tarp = Give(s, "field_tarp", s.Ground);
        s.Locations[s.Ground].Facility.TarpInstanceId = tarp.InstanceId;   // 起点：已铺开
        Assert.That(E.DiscardViewOf(s, DiscardTarget.Instance(tarp.InstanceId)).Blocked, Is.EqualTo(Deployed));

        var wood = Give(s, "wood", ItemInstance.Carried, 2);
        Start(s, ActionRequest.Wait(30));
        var t = DiscardTarget.Group(ItemInstance.Carried, "wood", null);
        Assert.That(E.DiscardViewOf(s, t).Blocked, Is.EqualTo("行动进行中，不能整理物品"), "主行动整理锁");
        Assert.That(E.Discard(s, new DiscardRequest { Target = t, All = true, Stamp = "x" }).Reason, Is.EqualTo("行动进行中，不能整理物品"));
        Assert.That(wood.Count, Is.EqualTo(2));
    }

    /// <summary>B53-Q860-01：合法首战教学样本可以永久丢弃、不补发；借用矛仍拒「当前无法丢弃该物品。」。</summary>
    [Test]
    public void Q860_01_TutorialSampleDiscardable_NoReissue_LoanStillRejected()
    {
        var s = New();
        var sub = Cfg.Appraisal.SampleItemId;
        var sample = E.CreateItems(s, sub, 1, ItemInstance.Carried, false, new ItemSource { GrantEventId = "tutorial-reward-b1", TutorialReward = true }).Single();
        Assert.That(E.IsTutorialCardSample(sample), Is.True);
        var t = DiscardTarget.Group(ItemInstance.Carried, sub, E.CardSideOf(sample));
        Assert.That(E.DiscardViewOf(s, t).Available, Is.EqualTo(1));
        var story = Newtonsoft.Json.JsonConvert.SerializeObject(s.Story);
        Assert.That(Confirm(s, t, 0, true).Ok, Is.True);
        Assert.That(s.Items.Any(i => i.Source?.TutorialReward == true), Is.False, "删掉、不补发");
        Assert.That(Newtonsoft.Json.JsonConvert.SerializeObject(s.Story), Is.EqualTo(story), "教学记录／融合回执不撤");
        var loan = Give(s, "wooden_spear", ItemInstance.Carried);
        loan.LoanSlot = "loan_1";
        Assert.That(E.DiscardPreview(s, DiscardTarget.Instance(loan.InstanceId), 1, false, out var why), Is.Null);
        Assert.That(why, Is.EqualTo(NoPermission));
    }

    /// <summary>PD-A04：未占用的锅含净水 500 毫升、生肉 1，整锅连内容一次删除；确认先列包含；重量只扣一次；不落地、不返料。</summary>
    [Test]
    public void PD_A04_WholePotWithContents_OneShot()
    {
        var s = New();
        var pot = Give(s, "pot", ItemInstance.Carried);
        Give(s, "clean_water", ItemInstance.ContainerOwner(pot.InstanceId), 1);
        Give(s, "raw_meat", ItemInstance.ContainerOwner(pot.InstanceId));
        var grams = E.CarriedGrams(s);
        var potGrams = E.ItemGrams(s, pot);
        var t = DiscardTarget.Instance(pot.InstanceId);
        var p = E.DiscardPreview(s, t, 1, false, out _)!;
        Assert.That(p.Lines, Is.EqualTo(new[] { "永久丢弃：金属锅×1。", "包含：净水500毫升、生肉×1。", "丢弃后无法找回。" }));
        var ground = s.Items.Count(i => i.Owner == s.Ground);
        Assert.That(E.Discard(s, new DiscardRequest { Target = t, Count = 1, Stamp = p.Stamp }).Ok, Is.True);
        Assert.That(s.Items.Any(i => i.InstanceId == pot.InstanceId || i.Owner == ItemInstance.ContainerOwner(pot.InstanceId)), Is.False, "父与内容都没了，无孤儿子物");
        Assert.That(E.CarriedGrams(s), Is.EqualTo(grams - potGrams), "只扣一次（锅含内容的重量）");
        Assert.That(s.Items.Count(i => i.Owner == s.Ground), Is.EqualTo(ground), "不落地");
        Assert.That(s.Log.Last().Text, Is.EqualTo("已永久丢弃：金属锅×1。同时丢弃：净水500毫升、生肉×1。"));
    }

    /// <summary>PD-A04 对照：空锅不造「包含」行。</summary>
    [Test]
    public void PD_A04_EmptyPot_NoContentsLine()
    {
        var s = New();
        var pot = Give(s, "pot", ItemInstance.Carried);
        Assert.That(E.DiscardPreview(s, DiscardTarget.Instance(pot.InstanceId), 1, false, out _)!.Lines, Is.EqualTo(new[] { "永久丢弃：金属锅×1。", "丢弃后无法找回。" }));
    }

    /// <summary>PD-A06：部分腐败物堆删除后剩余数量与已知来源账准确（账不超过剩余份数）。</summary>
    [Test]
    public void PD_A06_PartialRotHeap_RemainderAndLedger()
    {
        var s = New();
        var rot = RotBerries(s, ItemInstance.Carried, 3);
        Assert.That(rot.SpoiledOrigins, Is.EqualTo(new Dictionary<string, int> { ["berry"] = 3 }));
        Assert.That(Confirm(s, DiscardTarget.Group(ItemInstance.Carried, Rot, null), 1, false).Ok, Is.True);
        Assert.That(rot.Count, Is.EqualTo(2));
        Assert.That(rot.SpoiledOrigins, Is.EqualTo(new Dictionary<string, int> { ["berry"] = 2 }));
        Assert.That(s.Log.Last().Text, Is.EqualTo("已永久丢弃：腐败物×1。"));
    }

    /// <summary>B53-Q859-01：锅内一份食材单删；锅、水、另一份食材不动，锅内不折叠。</summary>
    [Test]
    public void Q859_01_PotFoodChild_DeletesOnlyThat()
    {
        var s = New();
        var pot = Give(s, "pot", ItemInstance.Carried);
        var inside = ItemInstance.ContainerOwner(pot.InstanceId);
        var water = Give(s, "clean_water", inside, 1);
        var m1 = Give(s, "raw_meat", inside);
        var m2 = Give(s, "raw_meat", inside);
        Assert.That(Confirm(s, DiscardTarget.Instance(m1.InstanceId), 1, false).Ok, Is.True);
        Assert.That(s.Items.Where(i => i.Owner == inside).Select(i => i.InstanceId), Is.EquivalentTo(new[] { water.InstanceId, m2.InstanceId }));
        Assert.That(s.Items.Contains(pot), Is.True);
        Assert.That(s.Log.Last().Text, Is.EqualTo("已永久丢弃：生肉（保质到第1日 20:00）×1。"), "暂用逐项写法（Q864 待答）：生肉保质 720 分钟，开局第1日 08:00 起");
    }

    /// <summary>B53-Q859-01：锅在燃烧的火堆上水煮食材时，这份食材与整口锅都拒「加工中：先停止加工。」，不制造缺料。</summary>
    [Test]
    public void Q859_01_ProcessingPotFoodAndPot_Rejected()
    {
        var s = New();
        var pot = s.Items.First(i => i.ItemId == "pot");
        E.DebugGive(s, "clean_water", 2, s.Ground);
        Assert.That(E.DoInstant(s, InstantRequest.FillPot(s.Items.First(i => i.ItemId == "clean_water" && i.Owner == s.Ground).InstanceId, pot.InstanceId, 2)).Ok, Is.True);
        Assert.That(E.DoInstant(s, InstantRequest.PutInFire(pot.InstanceId)).Ok, Is.True);
        var fac = s.Locations[s.Ground].Facility;
        fac.FireLit = true;
        fac.FuelMinutes = 180;
        E.DebugGive(s, "raw_root", 1, s.Ground);
        var root = s.Items.First(i => i.ItemId == "raw_root" && i.Owner == s.Ground);
        Assert.That(E.DoInstant(s, InstantRequest.PutFoodInPot(root.InstanceId, pot.InstanceId, 1)).Ok, Is.True);
        var inPot = s.Items.Single(i => i.ItemId == "raw_root" && i.Owner == ItemInstance.ContainerOwner(pot.InstanceId));
        Assert.That(E.ProcessOf(s, inPot), Is.Not.Null, "前提：在水煮");
        Assert.That(E.DiscardViewOf(s, DiscardTarget.Instance(inPot.InstanceId)).Blocked, Is.EqualTo(Processing));
        Assert.That(E.DiscardViewOf(s, DiscardTarget.Instance(pot.InstanceId)).Blocked, Is.EqualTo(Processing));
        // 被水煮预留的 500 毫升不可丢，另一份可丢
        var w = E.DiscardViewOf(s, DiscardTarget.PotWater(pot.InstanceId, "clean_water"));
        Assert.That((w.Available, w.MaxUnits), Is.EqualTo((500, 1)));
    }

    /// <summary>B53-Q859-02／03：锅水每份 500 毫升选 N＝1，只删 500，锅与另一份水保留；确认列水种和毫升；只扣内容重量一次；不发水卡、不加水分。</summary>
    [Test]
    public void Q859_02_PotWater_ChooseOne_Deletes500_KeepsPot()
    {
        var s = New();
        var pot = Give(s, "pot", ItemInstance.Carried);
        var inside = ItemInstance.ContainerOwner(pot.InstanceId);
        Give(s, "clean_water", inside, 2);
        var meat = Give(s, "raw_meat", inside);
        var t = DiscardTarget.PotWater(pot.InstanceId, "clean_water");
        var v = E.DiscardViewOf(s, t);
        Assert.That((v.IsWater, v.Available, v.UnitMl, v.MaxUnits, v.WaterName), Is.EqualTo((true, 1000, 500, 2, "净水")));
        var grams = E.CarriedGrams(s);
        var hyd = s.Player.Stats.Hydration;
        var cards = s.Items.Count(i => i.ItemId == "clean_water" && !i.InContainer);
        var p = E.DiscardPreview(s, t, 1, false, out _)!;
        Assert.That(p.Lines, Is.EqualTo(new[] { "永久丢弃：净水500毫升。", "丢弃后无法找回。" }), "只删水不写包含");
        Assert.That(E.Discard(s, new DiscardRequest { Target = t, Count = 1, Stamp = p.Stamp }).Ok, Is.True);
        Assert.That(s.Items.Where(i => i.Owner == inside && i.ItemId == "clean_water").Sum(i => i.Count), Is.EqualTo(1));
        Assert.That(s.Items.Contains(pot) && s.Items.Contains(meat), Is.True, "锅与食材保留");
        Assert.That(E.CarriedGrams(s), Is.EqualTo(grams - 500), "净水 1 克／毫升，扣 500 克一次");
        Assert.That(s.Player.Stats.Hydration, Is.EqualTo(hyd), "无饮水收益");
        Assert.That(s.Items.Count(i => i.ItemId == "clean_water" && !i.InContainer), Is.EqualTo(cards), "不发水卡");
        Assert.That(s.Log.Last().Text, Is.EqualTo("已永久丢弃：净水500毫升。"));
    }

    /// <summary>B53-Q859-02：壶剩 750 选 1 份删 500 → 剩 250；剩 250 时数量路径上限 0，全部丢弃删实际 250，壶保留、空水状态。</summary>
    [Test]
    public void Q859_02_Canteen_TailWater250_OnlyAll()
    {
        var s = New();
        var c = Give(s, "ration_canteen", ItemInstance.Carried);
        c.LiquidId = "clean_water";
        c.LiquidMl = 750;
        var t = DiscardTarget.CanteenWater(c.InstanceId);
        Assert.That(E.DiscardViewOf(s, t).MaxUnits, Is.EqualTo(1));
        Assert.That(Confirm(s, t, 1, false).Ok, Is.True);
        Assert.That((c.LiquidId, c.LiquidMl), Is.EqualTo(("clean_water", 250)));
        var v = E.DiscardViewOf(s, t);
        Assert.That((v.Available, v.UnitMl, v.MaxUnits), Is.EqualTo((250, 500, 0)), "不足一份：数量路径禁用");
        Assert.That(E.DiscardPreview(s, t, 1, false, out _), Is.Null, "不造半份、不向上凑 500");
        var p = E.DiscardPreview(s, t, 0, true, out _)!;
        Assert.That(p.Lines[0], Is.EqualTo("永久丢弃：净水250毫升。"));
        var grams = E.CarriedGrams(s);
        Assert.That(E.Discard(s, new DiscardRequest { Target = t, All = true, Stamp = p.Stamp }).Ok, Is.True);
        Assert.That((c.LiquidId, c.LiquidMl), Is.EqualTo(((string?)null, 0)));
        Assert.That(s.Items.Contains(c), Is.True, "壶保留");
        Assert.That(E.CarriedGrams(s), Is.EqualTo(grams - 250));
        Assert.That(s.Log.Last().Text, Is.EqualTo("已永久丢弃：净水250毫升。"));
    }

    /// <summary>B53-Q859-03：确认后壶里水量变了（喝了一口）整次拒绝「物品已变，请重选。」，不从别的容器补。</summary>
    [Test]
    public void Q859_03_WaterChangedAfterConfirm_Rejected_NoTopUp()
    {
        var s = New();
        var c = Give(s, "ration_canteen", ItemInstance.Carried);
        c.LiquidId = "clean_water";
        c.LiquidMl = 1000;
        var other = Give(s, "ration_canteen", ItemInstance.Carried);
        other.LiquidId = "clean_water";
        other.LiquidMl = 1000;
        var t = DiscardTarget.CanteenWater(c.InstanceId);
        var p = E.DiscardPreview(s, t, 2, false, out _)!;
        c.LiquidMl = 750;
        var res = E.Discard(s, new DiscardRequest { Target = t, Count = 2, Stamp = p.Stamp });
        Assert.That((res.Ok, res.Reason), Is.EqualTo((false, Changed)));
        Assert.That((c.LiquidMl, other.LiquidMl), Is.EqualTo((750, 1000)));
    }

    /// <summary>B53-Q859-04：空壶／空锅没有 0 量确认「无可丢弃的水。」；锅在火上净化脏水时拒「加工中：先停止加工。」；整锅连内容仍可另选。</summary>
    [Test]
    public void Q859_04_EmptyNoZeroConfirm_PurifyingLocked_WholePotStillSeparate()
    {
        var s = New();
        var c = Give(s, "ration_canteen", ItemInstance.Carried);
        var tc = DiscardTarget.CanteenWater(c.InstanceId);
        var v = E.DiscardViewOf(s, tc);
        Assert.That((v.Available, v.Blocked), Is.EqualTo((0, NoWater)));
        Assert.That(E.DiscardPreview(s, tc, 0, true, out var why), Is.Null);
        Assert.That(why, Is.EqualTo(NoWater));

        var pot = s.Items.First(i => i.ItemId == "pot");
        Assert.That(E.DiscardViewOf(s, DiscardTarget.PotWater(pot.InstanceId, "dirty_water")).Blocked, Is.EqualTo(NoWater));
        E.DebugGive(s, "dirty_water", 2, s.Ground);
        Assert.That(E.DoInstant(s, InstantRequest.FillPot(s.Items.First(i => i.ItemId == "dirty_water" && i.Owner == s.Ground).InstanceId, pot.InstanceId, 2)).Ok, Is.True);
        var tp = DiscardTarget.PotWater(pot.InstanceId, "dirty_water");
        Assert.That(E.DiscardViewOf(s, tp).Available, Is.EqualTo(1000), "对照：锅不在火上时脏水可丢");
        Assert.That(E.DoInstant(s, InstantRequest.PutInFire(pot.InstanceId)).Ok, Is.True);
        Assert.That(E.DiscardViewOf(s, tp).Blocked, Is.EqualTo(Processing), "净化中的水不能借丢弃解除绑定");
        Assert.That(E.DiscardViewOf(s, DiscardTarget.Instance(pot.InstanceId)).Blocked, Is.EqualTo(Processing));
    }

    // ───────────── M4 右栏入口、子区与 ESC（S14 2026-11-15 B50a／2026-11-17 B53，B53-Q857-01 右栏部分、Q857-02） ─────────────

    private static string Section(string text, string from, string to)
    {
        var i = text.IndexOf(from, StringComparison.Ordinal);
        Assert.That(i, Is.GreaterThanOrEqualTo(0), from);
        var j = text.IndexOf(to, i + from.Length, StringComparison.Ordinal);
        Assert.That(j, Is.GreaterThan(i), to);
        return text.Substring(i, j - i);
    }

    /// <summary>B53-Q857-01（右栏部分）：正式点选的物品详情有「丢弃」，锅水有「丢弃水」，水壶在水量行末「丢弃水」；悬停只读、加工菜单只读、鸟瞰浏览不放入口。</summary>
    [Test]
    public void Q857_01_RightRailEntries_OnlyOnRealSelection()
    {
        var d = Web("Components", "ItemDetail.razor");
        Assert.That(d, Does.Contain("var canDiscard = !ReadOnly && !G.CityBrowsing;"));
        Assert.That(d, Does.Contain("var discardTarget = !canDiscard || potLiquid ? null"), "锅水只有丢弃水，不给整组丢弃");
        Assert.That(d, Does.Contain("potLiquid && sel.InstanceId is int potId ? DiscardTarget.PotWater(potId, sel.Id)"));
        Assert.That(d, Does.Contain("canteen ? DiscardTarget.CanteenWater(inst.InstanceId)"));
        Assert.That(d, Does.Contain("<div class=\"row-act\"><span>水量</span><b>@e.CanteenLabel(inst)</b>"), "水壶水量行");
        Assert.That(d, Does.Contain("@onclick=\"() => G.OpenDiscard(waterTarget)\">@DiscardCopy.DiscardWater</button>"), "水量行末丢弃水");
        Assert.That(d, Does.Contain("<DiscardPanel Target=\"@discardTarget\" />"));
        Assert.That(d, Does.Contain("<DiscardPanel Target=\"@waterTarget\" HideButton=\"@canteen\" />"));
        // 对照：悬停与加工菜单打开时传入只读
        Assert.That(Web("Components", "SelectionDetail.razor"), Does.Contain("ReadOnly=\"@(Peek || G.InFireCrumbs(sel) != null)\""));
        var p = Web("Components", "DiscardPanel.razor");
        Assert.That(p, Does.Contain("@(Target.IsWater ? DiscardCopy.DiscardWater : DiscardCopy.Discard)</button>"));
    }

    /// <summary>B53-Q857-02：入口只开子区；两个子按钮都只到确认层；只有确认层「确认丢弃」提交；取消／返回不删除。</summary>
    [Test]
    public void Q857_02_OnlyFinalConfirmDeletes()
    {
        var p = Web("Components", "DiscardPanel.razor");
        Assert.That(System.Text.RegularExpressions.Regex.Matches(p, @"G\.ConfirmDiscard\(").Count, Is.EqualTo(1), "全文件只一处提交");
        var confirm = Section(p, "case DiscardStage.Confirm", "@code");
        Assert.That(confirm, Does.Contain("@onclick=\"Confirm\">@DiscardCopy.Confirm</button>"));
        var menu = Section(p, "case DiscardStage.Menu:", "case DiscardStage.Quantity:");
        Assert.That(menu, Does.Contain("@onclick=\"G.DiscardChooseQuantity\">@DiscardCopy.ChooseQuantity</button>"));
        Assert.That(menu, Does.Contain("@onclick=\"G.DiscardAll\">@DiscardCopy.DiscardAll</button>"));
        Assert.That(menu, Does.Contain("@onclick=\"G.CancelDiscard\">@DiscardCopy.Cancel"));
        var qty = Section(p, "case DiscardStage.Quantity:", "case DiscardStage.Confirm");
        Assert.That(qty, Does.Contain("@onclick=\"G.DiscardNext\">@DiscardCopy.Next</button>"));
        Assert.That(qty, Does.Contain("@onclick=\"G.DiscardBack\">@DiscardCopy.Back"));
        var sess = Web("Services", "GameSession.Discard.cs");
        var all = Section(sess, "public void DiscardAll()", "private void ToConfirm");
        Assert.That(all, Does.Not.Contain("E.Discard("), "全部丢弃只到确认层");
        Assert.That(Section(sess, "private void ToConfirm", "public void DiscardBack()"), Does.Contain("E.DiscardPreview(S, d.Target, n, all, out var why)"));
        var commit = Section(sess, "public ActionResult ConfirmDiscard()", "private void DropDiscard()");
        Assert.That(commit, Does.Contain("Stamp = d.Plan.Stamp, RequestId = d.RequestId"), "带确认快照与请求号");
        Assert.That(System.Text.RegularExpressions.Regex.Matches(sess, @"E\.Discard\(").Count, Is.EqualTo(1));
    }

    /// <summary>S14 B50a：不接无焦点空格／Enter 默认删除——丢弃子区不挂 data-enter、不自动聚焦；对照：数量框（DropQuantity）挂了 data-enter，检查本身能发现。</summary>
    [Test]
    public void Q857_02_NoDefaultEnterOrAutofocus()
    {
        var p = Web("Components", "DiscardPanel.razor");
        Assert.That(p, Does.Not.Contain("data-enter"));
        Assert.That(p, Does.Not.Contain("autofocus"));
        Assert.That(p, Does.Not.Contain("focusSelect"));
        Assert.That(p, Does.Not.Contain("FocusAsync"));
        Assert.That(Web("Components", "DropQuantity.razor"), Does.Contain("data-enter"), "对照");
    }

    /// <summary>S14 B50a：ESC 每次退一层（确认→数量／主层→收起），在清选中之前；换卡、清选中、读档清草稿。</summary>
    [Test]
    public void Q857_02_EscStepsBackOneLevel_DraftClearedOnReselect()
    {
        var keys = Web("Services", "GameSession.Keys.cs");
        var order = Section(keys, "public EscOwner NormalEscOwner()", "public bool EscOnDisk");
        var iStatus = order.IndexOf("return EscOwner.Status;", StringComparison.Ordinal);
        var iDiscard = order.IndexOf("if (Discarding != null && ShelfSelectionKind) return EscOwner.Discard;", StringComparison.Ordinal);
        var iDisk = order.IndexOf("return EscOwner.Disk;", StringComparison.Ordinal);
        var iPanelSel = order.IndexOf("return EscOwner.PanelSelection;", StringComparison.Ordinal);
        var iSel = order.IndexOf("return EscOwner.Selection;", StringComparison.Ordinal);
        Assert.That(new[] { iStatus, iDiscard, iDisk, iPanelSel, iSel }, Is.All.GreaterThanOrEqualTo(0));
        Assert.That(iStatus < iDiscard && iDiscard < iDisk && iDisk < iPanelSel && iPanelSel < iSel, Is.True, "在选卡盘（会清选中）之前：先逐层退丢弃子区");
        Assert.That(Web("Components", "GameScreen.razor"), Does.Contain("case EscOwner.Discard: G.DiscardBack(); break;"));
        var sess = Web("Services", "GameSession.Discard.cs");
        var back = Section(sess, "public void DiscardBack()", "public void CancelDiscard()");
        Assert.That(back, Does.Contain("d.Stage = d.All ? DiscardStage.Menu : DiscardStage.Quantity;"));
        Assert.That(back, Does.Contain("d.Stage = DiscardStage.Menu;"));
        Assert.That(back, Does.Contain("_discard = null;"));
        Assert.That(back, Does.Not.Contain("E.Discard("));
        var main = Web("Services", "GameSession.cs");
        Assert.That(main, Does.Contain("if (sel.Key != Selected.Key) DropDiscard();"));
        Assert.That(System.Text.RegularExpressions.Regex.Matches(main, @"DropDiscard\(\);").Count, Is.EqualTo(4), "换卡、程序清选中、ESC 清选中、读档");
        Assert.That(sess, Does.Contain("_discard != null && _discard.SelectionKey == Selected.Key ? _discard : null"), "旧卡草稿不出现在新卡上");
        var p = Web("Components", "DiscardPanel.razor");
        Assert.That(System.Text.RegularExpressions.Regex.Matches(p, @"<EscCap On=""@escHere"" />").Count, Is.EqualTo(3), "每层承接 ESC 的那个按钮显示键帽");
    }

    // ───────────── M5 腐败物普通卡（S06／S14 2026-11-15 B50a §Q766；逐字稿 rotName／rotWeight） ─────────────

    /// <summary>Q766：腐败物卡第二行写这张卡的真实总重「{重量}公斤」（野果每份 200 克，S06 物品表；3 份 → 0.60公斤），不写「已腐败」。</summary>
    [Test]
    public void Q766_RotCard_SecondLineIsRealWeight()
    {
        var s = New();
        var rot = RotBerries(s, ItemInstance.Carried, 3);
        Assert.That(CardStates.RotWeightLine(E, s, new[] { rot }), Is.EqualTo("0.60公斤"));
        var more = RotBerries(s, s.Ground, 2);
        Assert.That(CardStates.RotWeightLine(E, s, new[] { rot, more }), Is.EqualTo("1.00公斤"), "整卡多堆按真实克数合计");
    }

    /// <summary>Q766：卡面只对腐败物去掉危险叠层、灰化与重复「已腐败」；未转换的过期原物仍「已腐败」并挂危险（对照）。</summary>
    [Test]
    public void Q766_RotCard_NoDangerOverlay_ExpiredOriginalKeeps()
    {
        var shelf = Web("Components", "ItemShelf.razor");
        Assert.That(shelf, Does.Contain("var spoiledFront = !rotGroup && (stacked ? kb![0].Spoiled : allSpoiled);"));
        Assert.That(shelf, Does.Contain("if (rotGroup) line2 = CardStates.RotWeightLine(e, s, list);"));
        Assert.That(shelf, Does.Contain("if (rotGroup) infoLine = CardStates.RotWeightLine(e, s, list)"));
        Assert.That(shelf, Does.Contain("@(allSpoiled && !rotGroup ? \"spoiled\" : \"\")"));
        var rotLayer = Section(shelf, "if (rotGroup)", "continue;");
        Assert.That(rotLayer, Does.Contain("class=\"stack-layer @(b.Reserved ? \"reserved\" : \"\")\""), "后层也不灰化");
        // 对照：过期原物照旧
        Assert.That(shelf, Does.Contain("var line2 = spoiledFront ? \"已腐败\" : pausedLine ?? Ui.CardLine2(e, s, stacked ? head : whole);"));
        Assert.That(shelf, Does.Contain("@(spoiledFront ? \"st-danger\" : \"\")"));
        Assert.That(shelf, Does.Contain("class=\"stack-layer @(b.Spoiled ? \"spoiled\" : \"\") @(b.Reserved ? \"reserved\" : \"\")\""));
        var sup = Web("Components", "BattleSupplies.razor");
        Assert.That(sup, Does.Contain("@(id == GameEngine.SpoiledMatterId ? CardStates.RotWeightLine(e, s, list) : e.IsSpoiled(s, head) ? \"已腐败\" : Ui.ItemLine(e, s, head))"));
    }

    /// <summary>Q766：腐败物用本批专用腐烂物图标（assets/20261115/永久丢弃与腐败物/腐败物图标.svg 原样内联），不再用原分类占位图。</summary>
    [Test]
    public void Q766_RotIcon_FromDesignAsset()
    {
        var icons = Web("Services", "Icons.cs");
        var line = icons.Split('\n').Single(l => l.Contains("[\"spoiled_matter\"]"));
        Assert.That(line, Does.Contain("<path d=\"\"M 12 77 C 8 66 17 55 28 52 C 29 40 40 31 52 36 C 63 27 78 37 77 51 C 90 55 96 70 87 79 C 72 91 28 91 12 77 Z\"\" fill=\"\"#89704e\"\""));
        Assert.That(line, Does.Contain("<ellipse cx=\"\"73.5\"\" cy=\"\"69.5\"\" rx=\"\"2.5\"\" ry=\"\"2.5\"\" fill=\"\"#a0ab65\"\"/>"));
        Assert.That(line, Does.Not.Contain("<rect x=\"\"16\"\" y=\"\"34\"\""), "旧分类占位图已换下");
    }

    // ───────────── M6 开发试玩起点 ─────────────

    /// <summary>开发：B53a 丢弃试用组只造起点——地面野果两批（3＋5）、锅水 1000 毫升＋生肉；随身壶水 750、待腐野果 3。</summary>
    [Test]
    public void DevKit_BuildsStartingPointOnly()
    {
        var s = New();
        s.Items.RemoveAll(i => i.ItemId == "berry" || i.ItemId == "ration_canteen");
        var minute = s.GameMinute;
        E.DebugDiscardKit(s);
        Assert.That(s.GameMinute, Is.EqualTo(minute));
        var ground = s.Items.Where(i => i.ItemId == "berry" && i.Owner == s.Ground).OrderBy(i => i.Count).Select(i => i.Count);
        Assert.That(ground, Is.EqualTo(new[] { 3, 5 }), "两批");
        var t = DiscardTarget.Group(s.Ground, "berry", null);
        Assert.That(E.DiscardViewOf(s, t).Available, Is.EqualTo(8));
        var pot = s.Items.Single(i => i.ItemId == "pot" && i.Owner == s.Ground && E.PotLiquids(s, i.InstanceId).Count > 0);
        Assert.That(E.DiscardViewOf(s, DiscardTarget.PotWater(pot.InstanceId, "clean_water")).Available, Is.EqualTo(1000));
        var c = s.Items.Single(i => i.ItemId == "ration_canteen");
        Assert.That((c.Owner, c.LiquidId, c.LiquidMl), Is.EqualTo((ItemInstance.Carried, "clean_water", 750)));
        Assert.That(s.Items.Single(i => i.ItemId == "berry" && i.Owner == ItemInstance.Carried).Count, Is.EqualTo(3));
    }
}
