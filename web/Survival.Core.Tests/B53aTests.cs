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
}
