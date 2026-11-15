using Newtonsoft.Json.Linq;
using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 批次 B50a「保质、腐败来源与晾制」（开发记录 B50a）：S06／S11／S14 文首「2026-11-15 BM5b2 Q666—Q671」与「2026-11-15 Q711—Q719」的 B50a 部分
/// （Q666、Q667、Q669、Q671、Q711—Q715；Q668 由 Q712 取代）。正式句见《重墨 V2 开工裁定与界面逐字稿》同两节；网页壳接线以源码检查。
/// 柜子数量操作（Q670、Q716—Q719）与地面／容器牌组（Q717）属 B50b，不在这里。
/// </summary>
public class B50aTests : TestBase
{
    protected override bool NoEnemyApproach => true;

    private const string Rot = GameEngine.SpoiledMatterId;

    private static string Web(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "Survival.Web" }.Concat(parts).ToArray()));

    private List<ItemInstance> Make(GameState s, string itemId, int count, string owner, int? expires = null)
    {
        var made = E.CreateItems(s, itemId, count, owner);
        if (expires.HasValue) foreach (var i in made) i.ExpiresAt = expires;
        return made;
    }

    /// <summary>空闲等待到指定分钟（按 5 分钟段推进）。</summary>
    private void WaitUntil(GameState s, int minute)
    {
        while (s.GameMinute < minute)
        {
            if (s.Main == null) Start(s, ActionRequest.Wait(30));
            E.Advance(s);
        }
        if (s.Main?.Type == ActionType.Wait) E.Cancel(s);
    }

    /// <summary>全局来源账：原物 ID → 全部腐败物上的已知份数合计。</summary>
    private static Dictionary<string, int> Ledger(GameState s) =>
        s.Items.Where(i => i.SpoiledOrigins != null).SelectMany(i => i.SpoiledOrigins!)
            .GroupBy(kv => kv.Key).ToDictionary(g => g.Key, g => g.Sum(kv => kv.Value));

    private static void AssertLedgerValid(GameState s)
    {
        foreach (var i in s.Items.Where(i => i.SpoiledOrigins != null))
        {
            Assert.That(i.ItemId, Is.EqualTo(Rot), "只有腐败物带来源账");
            Assert.That(i.SpoiledOrigins!.Values.All(v => v > 0), Is.True, "账内份数为正");
            Assert.That(i.SpoiledOrigins!.Values.Sum(), Is.LessThanOrEqualTo(i.Count), $"#{i.InstanceId} 已知份数不超过总份数");
        }
    }

    private static InstantRequest Rq(InstantRequest r, string id) { r.RequestId = id; return r; }

    private static string AsSchema(GameState s, string version, Action<JObject>? edit = null)
    {
        var o = JObject.Parse(SaveSerializer.Serialize(s));
        o["SchemaVersion"] = version;
        edit?.Invoke(o);
        return o.ToString();
    }

    // ───────────── M946 存档 53 骨架 ─────────────

    [Test]
    public void M946_Schema53_CloneDeepCopiesLedger_StopReasonField()
    {
        Assert.That(GameState.CurrentSchema, Is.EqualTo("53"));
        var i = new ItemInstance { ItemId = Rot, Count = 3, SpoiledOrigins = new Dictionary<string, int> { ["raw_meat"] = 1, ["cooked_meat"] = 2 } };
        var c = i.Clone();
        c.SpoiledOrigins!["raw_meat"] = 9;
        Assert.That(i.SpoiledOrigins!["raw_meat"], Is.EqualTo(1), "Clone 深拷来源账");
        var b = new DryingSlot { StopReason = DryingSlot.StopMissing };
        Assert.That(b.Clone().StopReason, Is.EqualTo("missing"));
    }

    /// <summary>Q711-A03／Q763（待定，按现状）：52 档的腐败物只有单一 SpoiledFrom，迁移不建来源账、不拆堆、不改重量；SpoiledFrom 原样保留。</summary>
    [Test]
    public void M946_Migrate52_OldSpoiledFromNotTrusted_Kept()
    {
        var s = New();
        Make(s, "raw_meat", 2, "camp", s.GameMinute + 5);
        WaitUntil(s, s.GameMinute + 5);
        var rot = s.Items.Single(i => i.ItemId == Rot && i.Owner == "camp");
        var json = AsSchema(s, "52", o =>
        {
            foreach (var it in o["Items"]!) ((JObject)it).Remove("SpoiledOrigins");
        });
        var t = SaveSerializer.Deserialize(json, Cfg);
        Assert.That(t.SchemaVersion, Is.EqualTo("53"));
        var r = t.Items.Single(i => i.InstanceId == rot.InstanceId);
        Assert.That((r.SpoiledFrom, r.SpoiledOrigins, r.Count, r.SpoiledUnitGrams), Is.EqualTo(("raw_meat", (Dictionary<string, int>?)null, 2, rot.SpoiledUnitGrams)));
        Assert.That(t.Log.Count, Is.EqualTo(s.Log.Count), "静默迁移，不写说明（Q714）");
    }
    // ───────────── M947 来源账守恒（Q711-A01／A02） ─────────────

    /// <summary>Q711-A01：同克重／上限的生肉1、熟肉2腐败后合为腐败物3（不因原物分堆），来源账生肉1、熟肉2；重量与根卡不变。</summary>
    [Test]
    public void Q711_A01_MixedOriginsMergeIntoOneStack_LedgerPerOrigin()
    {
        var s = New();
        s.Items.RemoveAll(i => i.IsCarried);
        var due = s.GameMinute + 5;
        Make(s, "raw_meat", 1, ItemInstance.Carried, due);
        Make(s, "cooked_meat", 2, ItemInstance.Carried, due);
        var grams = E.CarriedGrams(s);
        Assert.That(E.RootCardCount(s), Is.EqualTo(2));
        WaitUntil(s, due);
        var rot = s.Items.Where(i => i.IsCarried && i.ItemId == Rot).ToList();
        Assert.That(rot, Has.Count.EqualTo(1), "同一真实堆");
        Assert.That(rot[0].Count, Is.EqualTo(3));
        Assert.That(rot[0].SpoiledOrigins, Is.EquivalentTo(new Dictionary<string, int> { ["raw_meat"] = 1, ["cooked_meat"] = 2 }));
        Assert.That(E.KnownOrigins(rot), Is.EqualTo(new List<(string, int)> { ("cooked_meat", 2), ("raw_meat", 1) }), "按原物 ID 升序");
        Assert.That(E.CarriedGrams(s), Is.EqualTo(grams), "每份克重不变");
        Assert.That(E.RootCardCount(s), Is.EqualTo(1), "合成一张根卡");
        AssertLedgerValid(s);
    }

    /// <summary>
    /// Q711-A02：混堆部分转移（拾取／放下）、满堆拆分、Normalize、柜子存取、移入锅后，来源账随实物分配，已知≤份数、全局守恒；
    /// 拆分按原物 ID 升序先分已知再分未知，不向子堆复制整表；重复请求不增物或来源。
    /// </summary>
    [Test]
    public void Q711_A02_LedgerConservedAcrossSplitMergeNormalizeCabinetPot()
    {
        var s = New();
        s.Items.RemoveAll(i => i.Owner == "camp" || i.IsCarried);
        var due = s.GameMinute + 5;
        Make(s, "raw_meat", 1, "camp", due);
        Make(s, "cooked_meat", 2, "camp", due);
        Make(s, "boiled_meat", 3, "camp", due);
        WaitUntil(s, due);
        var heap = s.Items.Single(i => i.Owner == "camp" && i.ItemId == Rot);
        Assert.That(heap.Count, Is.EqualTo(6));
        var before = Ledger(s);
        Assert.That(before, Is.EquivalentTo(new Dictionary<string, int> { ["raw_meat"] = 1, ["cooked_meat"] = 2, ["boiled_meat"] = 3 }));
        // 拾取 2 份：按原物 ID 升序先分已知（boiled_meat 在前）
        var r = E.DoInstant(s, Rq(InstantRequest.Pickup(heap.InstanceId, 2), "rq-pick"));
        Assert.That(r.Ok, Is.True, r.Reason);
        var picked = s.Items.Single(i => i.IsCarried && i.ItemId == Rot);
        Assert.That(picked.SpoiledOrigins, Is.EquivalentTo(new Dictionary<string, int> { ["boiled_meat"] = 2 }), "子堆只拿本次份额");
        Assert.That(E.DoInstant(s, Rq(InstantRequest.Pickup(heap.InstanceId, 2), "rq-pick")).Ok, Is.True, "重复请求");
        Assert.That(s.Items.Where(i => i.ItemId == Rot).Sum(i => i.Count), Is.EqualTo(6), "重复请求不增物");
        Assert.That(Ledger(s), Is.EquivalentTo(before));
        AssertLedgerValid(s);
        // 放回 1 份再合堆（Normalize 汇总、按堆序再分）
        Assert.That(E.DoInstant(s, InstantRequest.Drop(picked.InstanceId, 1)).Ok, Is.True);
        Assert.That(Ledger(s), Is.EquivalentTo(before));
        AssertLedgerValid(s);
        // 未知份：手工让一堆含 2 份未知，拆分时已知先分完才分未知
        var u = new ItemInstance { InstanceId = s.NextInstanceId++, ItemId = Rot, Count = 3, Owner = "camp", SpoiledFrom = "raw_meat", SpoiledUnitGrams = 300, SpoiledStackMax = 20, SpoiledOrigins = new Dictionary<string, int> { ["raw_meat"] = 1 } };
        s.Items.Add(u);
        E.CreateItems(s, "stone", 1, "camp");   // 触发当地 Normalize：同键合堆，账汇总
        var merged = s.Items.Where(i => i.Owner == "camp" && i.ItemId == Rot).ToList();
        Assert.That(merged.Sum(i => i.Count), Is.EqualTo(8));
        Assert.That(merged.Sum(GameEngine.KnownOriginCount), Is.EqualTo(6), "未知 2 份不显示也不补");
        AssertLedgerValid(s);
        var total = Ledger(s);
        // 满堆拆分：上限 20 的堆超额时 Normalize 拆成新堆，账按堆分、不复制
        var big = merged[0];
        big.Count += 15;   // 视作又并进了 15 份未知
        E.CreateItems(s, "stone", 1, "camp");
        var stacks = s.Items.Where(i => i.Owner == "camp" && i.ItemId == Rot).OrderBy(i => i.InstanceId).ToList();
        Assert.That(stacks.Select(i => i.Count), Is.EqualTo(new[] { 20, 3 }));
        Assert.That(Ledger(s), Is.EquivalentTo(total), "满堆拆分不倍增来源");
        Assert.That(GameEngine.KnownOriginCount(stacks[0]), Is.EqualTo(6), "按堆序先分已知");
        AssertLedgerValid(s);
    }

    /// <summary>Q711-A02：柜子存取（部分份数拆堆）、锅内食材腐败后取出合堆、SplitOff 与存档往返，全局来源守恒。</summary>
    [Test]
    public void Q711_A02_CabinetAndPotAndSplitOffConserve()
    {
        var s = New();
        s.Progress.Flags.Add("hub_registered");
        E.DebugSetProgress(s, "camp", 100);
        s.Skills["crafting"].Level = Math.Max(1, s.Skills["crafting"].Level);
        foreach (var (id, n) in new[] { ("wood", 8), ("fiber", 6), ("cloth", 2), ("stone", 4) }) E.CreateItems(s, id, n, s.Ground);
        Start(s, ActionRequest.Craft("safehouse_base", null, null, 1));
        Run(s);
        Assert.That(E.EnterSafehouse(s).Ok, Is.True);
        s.Items.RemoveAll(i => i.IsCarried);
        var heap = new ItemInstance { InstanceId = s.NextInstanceId++, ItemId = Rot, Count = 5, Owner = ItemInstance.Carried, SpoiledFrom = "berry", SpoiledUnitGrams = 200, SpoiledStackMax = 20, SpoiledOrigins = new Dictionary<string, int> { ["berry"] = 2, ["dried_meat"] = 2 } };
        s.Items.Add(heap);
        var before = Ledger(s);
        var part = E.SplitOff(s, heap, 3);
        Assert.That(part.SpoiledOrigins, Is.EquivalentTo(new Dictionary<string, int> { ["berry"] = 2, ["dried_meat"] = 1 }), "按原物 ID 升序先分已知");
        Assert.That(heap.SpoiledOrigins, Is.EquivalentTo(new Dictionary<string, int> { ["dried_meat"] = 1 }), "留堆减同量（另 1 份未知）");
        Assert.That(Ledger(s), Is.EquivalentTo(before));
        s.Items.Remove(part);
        heap.Count += 3;
        heap.SpoiledOrigins = new Dictionary<string, int> { ["berry"] = 2, ["dried_meat"] = 2 };
        // 柜子存 3 取 1
        Assert.That(E.DoInstant(s, InstantRequest.StoreCabinet(heap.InstanceId, 3)).Ok, Is.True);
        var cab = E.CabinetItems(s, "camp").Single(i => i.ItemId == Rot);
        Assert.That(cab.SpoiledOrigins, Is.EquivalentTo(new Dictionary<string, int> { ["berry"] = 2, ["dried_meat"] = 1 }));
        Assert.That(E.DoInstant(s, InstantRequest.TakeCabinet(cab.InstanceId, 1)).Ok, Is.True);
        Assert.That(Ledger(s), Is.EquivalentTo(before));
        AssertLedgerValid(s);
        // 存档往返保留来源账
        var t = SaveSerializer.Deserialize(SaveSerializer.Serialize(s), Cfg);
        Assert.That(Ledger(t), Is.EquivalentTo(before));
    }

    /// <summary>Q711-A02：锅内食材（逐份）到期转成腐败物×1 带账，取出到地面与地面已有腐败物合堆，账合计。</summary>
    [Test]
    public void Q711_A02_PotFoodSpoilsThenTakeOutMergesLedger()
    {
        var s = New();
        var pot = s.Items.First(i => i.ItemId == "pot");
        E.DebugGive(s, "clean_water", 2, "camp");
        Assert.That(E.DoInstant(s, InstantRequest.FillPot(s.Items.First(i => i.ItemId == "clean_water" && i.Owner == "camp").InstanceId, pot.InstanceId, 2)).Ok, Is.True);
        Assert.That(E.DoInstant(s, InstantRequest.PutInFire(pot.InstanceId)).Ok, Is.True);
        var root = Make(s, "raw_root", 1, "camp").Single();
        Assert.That(E.DoInstant(s, InstantRequest.PutFoodInPot(root.InstanceId, pot.InstanceId, 1)).Ok, Is.True);
        var inPot = E.PotFood(s, pot.InstanceId).Single();
        inPot.ExpiresAt = s.GameMinute + 5;
        Make(s, "berry", 2, "camp", s.GameMinute + 5);
        WaitUntil(s, s.GameMinute + 5);
        var rotInPot = E.PotFood(s, pot.InstanceId).Single();
        Assert.That((rotInPot.ItemId, rotInPot.SpoiledOrigins!["raw_root"]), Is.EqualTo((Rot, 1)), "锅里未受热的食材到期，逐份转换带账");
        Assert.That(E.DoInstant(s, InstantRequest.TakeOut(rotInPot.InstanceId, "camp")).Ok, Is.True);
        var ground = s.Items.Where(i => i.Owner == "camp" && i.ItemId == Rot && i.SpoiledUnitGrams == 300).ToList();
        Assert.That(ground.Sum(i => i.Count), Is.EqualTo(1));
        Assert.That(Ledger(s), Is.EquivalentTo(new Dictionary<string, int> { ["raw_root"] = 1, ["berry"] = 2 }));
        AssertLedgerValid(s);
    }

    // ───────────── M948 来源与物理明细（Q711-A03、Q669-A01／A02） ─────────────

    private ItemInstance Rotten(GameState s, string owner, int count, int grams, int cap, Dictionary<string, int>? origins, string from = "raw_meat")
    {
        var i = new ItemInstance { InstanceId = s.NextInstanceId++, ItemId = Rot, Count = count, Owner = owner, SpoiledFrom = from, SpoiledUnitGrams = grams, SpoiledStackMax = cap, SpoiledOrigins = origins };
        s.Items.Add(i);
        return i;
    }

    /// <summary>Q711-A03：旧档无账的混堆不猜、不拆，与新已知来源合堆后只显示可信部分；完全无已知项不显示来源标题；总份数／克重仍真值。</summary>
    [Test]
    public void Q711_A03_UnknownHidden_PartialKnownOnly_NoTitleWhenNone()
    {
        var s = New();
        s.Items.RemoveAll(i => i.Owner == "camp" && !E.IsPot(i));
        var old = Rotten(s, "camp", 4, 300, 20, null);
        Assert.That(E.OriginListText(new[] { old }), Is.Null, "无已知项整段隐藏，不写未知、不写原物来源未记录");
        Assert.That(E.OriginEntries(new[] { old }), Is.Empty);
        Make(s, "cooked_meat", 1, "camp", s.GameMinute + 5);
        WaitUntil(s, s.GameMinute + 5);
        var heap = s.Items.Where(i => i.Owner == "camp" && i.ItemId == Rot).ToList();
        Assert.That(heap.Sum(i => i.Count), Is.EqualTo(5), "新旧合堆");
        Assert.That(E.OriginListText(heap), Is.EqualTo($"来源：{E.ItemName("cooked_meat")}×1"), "只列可信部分，不按比例补未知");
        Assert.That(heap.Sum(i => E.ItemGrams(s, i)), Is.EqualTo(1500), "原克重真值");
        Assert.That(heap.Single().SpoiledFrom, Is.EqualTo("raw_meat"), "业务字段照旧（SpoiledFrom 不被来源账替代）");
    }

    /// <summary>Q711：已知来源清单同原物合计、按原物 ID 升序、顿号连接；只算所指这组自身的账。</summary>
    [Test]
    public void Q711_OriginList_OrderedMergedScopedToGivenItems()
    {
        var s = New();
        var a = Rotten(s, "camp", 3, 300, 20, new Dictionary<string, int> { ["raw_meat"] = 1, ["cooked_meat"] = 2 });
        var b = Rotten(s, "w01", 2, 300, 20, new Dictionary<string, int> { ["raw_meat"] = 2 });
        Assert.That(E.OriginEntries(new[] { a }), Is.EqualTo(new[] { $"{E.ItemName("cooked_meat")}×2", $"{E.ItemName("raw_meat")}×1" }));
        Assert.That(E.OriginListText(new[] { a, b }), Is.EqualTo($"来源：{E.ItemName("cooked_meat")}×2、{E.ItemName("raw_meat")}×3"));
        Assert.That(E.OriginListText(new[] { b }), Is.EqualTo($"来源：{E.ItemName("raw_meat")}×2"), "不把邻卡来源算进来");
    }

    /// <summary>
    /// Q669-A01：不同原物腐败物视觉归一张卡，明细按「×{总份数} · 每份{重量}公斤」写，不再一处已腐败一处不会腐败；
    /// 同每份克重不同继承上限才附「每堆最多{上限}份」；真实分堆／重量／五卡不被配置占位 1 克／2 份影响。
    /// </summary>
    [Test]
    public void Q669_A01_PhysicalDetail_CapOnlyWhenNeeded_WeightFromInstance()
    {
        var s = New();
        s.Items.RemoveAll(i => i.IsCarried);
        Rotten(s, ItemInstance.Carried, 2, 250, 10, new Dictionary<string, int> { ["grain_cake"] = 2 }, "grain_cake");
        Rotten(s, ItemInstance.Carried, 3, 250, 20, new Dictionary<string, int> { ["expedition_bar"] = 3 }, "expedition_bar");
        Rotten(s, ItemInstance.Carried, 1, 300, 20, new Dictionary<string, int> { ["raw_meat"] = 1 });
        var batches = E.BatchesOf(s, ItemInstance.Carried, Rot);
        Assert.That(batches, Has.Count.EqualTo(3), "按每份克重与继承上限真实分批");
        var texts = batches.Select(b => E.SpoiledPhysicalText(b.Stacks, E.NeedsStackCapNote(batches, b))).ToList();
        Assert.That(texts, Is.EquivalentTo(new[] { "×2 · 每份0.25公斤 · 每堆最多10份", "×3 · 每份0.25公斤 · 每堆最多20份", "×1 · 每份0.30公斤" }));
        Assert.That(E.CarriedGrams(s), Is.EqualTo(2 * 250 + 3 * 250 + 300), "按实例每份克重");
        Assert.That(E.RootCardCount(s), Is.EqualTo(3), "不同克重／上限不真合堆");
        Assert.That(E.Item(Rot).WeightGrams, Is.EqualTo(1), "配置占位值仍在、不参与计算");
    }

    /// <summary>Q669-A02（来源兜底按 Q711 取代）：重开存档、转移后来源元数据保留，不恢复成原食品。</summary>
    [Test]
    public void Q669_A02_MetadataKeptThroughReloadAndTransfer_NotRestored()
    {
        var s = New();
        s.Items.RemoveAll(i => i.IsCarried);
        var r = Rotten(s, "camp", 2, 300, 20, new Dictionary<string, int> { ["raw_meat"] = 2 });
        Assert.That(E.DoInstant(s, InstantRequest.Pickup(r.InstanceId, 1)).Ok, Is.True);
        var t = SaveSerializer.Deserialize(SaveSerializer.Serialize(s), Cfg);
        var carried = t.Items.Single(i => i.IsCarried && i.ItemId == Rot);
        Assert.That((carried.SpoiledFrom, carried.SpoiledUnitGrams, carried.SpoiledStackMax, carried.SpoiledOrigins!["raw_meat"]), Is.EqualTo(("raw_meat", (int?)300, (int?)20, 1)));
        Assert.That(t.Items.Count(i => i.ItemId == "raw_meat" && (i.IsCarried || i.Owner == "camp")), Is.EqualTo(0), "不恢复成原食品");
        Assert.That(E.OriginListText(t.Items.Where(i => i.ItemId == Rot)), Is.EqualTo($"来源：{E.ItemName("raw_meat")}×2"));
    }

    /// <summary>来源账存档校验：已知合计超过总份数、非腐败物带账、非正份数的整张账作废（不显示、不补），实物不变。</summary>
    [Test]
    public void Q711_SaveCheck_InvalidLedgerDropped_ValidKept()
    {
        var s = New();
        var bad = Rotten(s, "camp", 2, 300, 20, new Dictionary<string, int> { ["raw_meat"] = 3 });
        var ok = Rotten(s, "w01", 2, 200, 20, new Dictionary<string, int> { ["berry"] = 1 }, "berry");
        var stone = E.CreateItems(s, "stone", 1, "w02").Single();
        stone.SpoiledOrigins = new Dictionary<string, int> { ["raw_meat"] = 1 };
        var t = SaveSerializer.Deserialize(SaveSerializer.Serialize(s), Cfg);
        Assert.That(t.Items.Single(i => i.InstanceId == bad.InstanceId).SpoiledOrigins, Is.Null);
        Assert.That(t.Items.Single(i => i.InstanceId == bad.InstanceId).Count, Is.EqualTo(2));
        Assert.That(t.Items.Single(i => i.InstanceId == ok.InstanceId).SpoiledOrigins!["berry"], Is.EqualTo(1));
        Assert.That(t.Items.Single(i => i.InstanceId == stone.InstanceId).SpoiledOrigins, Is.Null);
    }

    // ───────────── M949 全部晾制暂停保质（Q666-A01—A04、Q714-A01） ─────────────

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

    private long Xp(GameState s) => s.Skills["cooking"].Xp;

    /// <summary>Q666-A01：野果剩 30 分钟投料，晾 60 分钟后取消仍剩 30；退回后在外 10 分钟再投再取消，按剩 20 恢复，不刷新成完整保质。</summary>
    [Test]
    public void Q666_A01_BerryCancel_RestoresRemaining_ReStartUsesTrueRemaining()
    {
        var s = Rack();
        var t0 = s.GameMinute;
        Make(s, "berry", 2, ItemInstance.Carried, t0 + 30);
        var x0 = Xp(s);
        Assert.That(E.StartDrying(s, "dry_berries").Ok, Is.True);
        Assert.That(s.Items.Where(i => i.ItemId == "berry").Select(i => (i.ShelfPausedMinutes, i.ExpiresAt)),
            Is.All.EqualTo(((int?)30, (int?)null)), "R＝E−S＝30，截止清空");
        Assert.That(DrySlots(s).Single().ShelfPaused, Is.True);
        AdvanceTo(s, t0 + 60);
        Assert.That(DrySlots(s).Single().Status, Is.EqualTo(DryingSlot.Drying), "越过原截止仍在晾");
        Assert.That(E.CancelDrying(s, 0, false).Ok, Is.True);
        var c1 = s.GameMinute;
        Assert.That(s.Items.Where(i => i.ItemId == "berry").Select(i => (i.Owner, i.ExpiresAt, i.ShelfPausedMinutes)),
            Is.All.EqualTo((s.Ground, (int?)(c1 + 30), (int?)null)), "新截止＝C＋R，仍剩 30");
        Assert.That(Xp(s), Is.EqualTo(x0), "取消不给经验");
        Assert.That(s.Log.Last().Text, Does.Contain("没有成品，不给经验"));

        AdvanceTo(s, c1 + 10);
        Assert.That(E.StartDrying(s, "dry_berries").Ok, Is.True);
        Assert.That(s.Items.Where(i => i.ItemId == "berry").Select(i => i.ShelfPausedMinutes), Is.All.EqualTo(20), "按退回后的真实剩余 20 重新快照");
        AdvanceTo(s, c1 + 40);
        Assert.That(E.CancelDrying(s, 0, false).Ok, Is.True);
        Assert.That(s.Items.Where(i => i.ItemId == "berry").Select(i => i.ExpiresAt), Is.All.EqualTo(s.GameMinute + 20), "不刷新成完整保质");
    }

    /// <summary>Q666-A02：只剩 5 分钟的野果与生肉都能晾满（果干 120、肉干 180）；成品从完成时起按自身保质计时，留在架上照常到期。</summary>
    [Test]
    public void Q666_A02_NearExpiryInputsComplete_ProductExpiresFromFinish()
    {
        var s = Rack();
        var t0 = s.GameMinute;
        Make(s, "berry", 2, ItemInstance.Carried, t0 + 5);
        Make(s, "raw_meat", 1, ItemInstance.Carried, t0 + 5);
        Make(s, "salt", 1, s.Ground);
        var x0 = Xp(s);
        Assert.That(E.DryingPreview(s, "dry_berries").Warnings, Is.Empty, "不写会在完成前腐败");
        Assert.That(E.StartDrying(s, "dry_berries").Ok, Is.True);
        Assert.That(E.DryingPreview(s, "dry_meat").Warnings, Is.Empty);
        Assert.That(E.StartDrying(s, "dry_meat").Ok, Is.True);
        AdvanceTo(s, t0 + 120);
        var berrySlot = DrySlots(s).Single(b => b.RecipeId == "dry_berries");
        Assert.That(berrySlot.Status, Is.EqualTo(DryingSlot.Done), "果干 120 分钟完成");
        var db = s.Items.Single(i => i.ItemId == "dried_berries");
        Assert.That(db.ShelfPausedMinutes, Is.Null, "保护在转为成品时结束");
        Assert.That(db.ExpiresAt, Is.EqualTo(t0 + 120 + E.Item("dried_berries").ShelfLifeMinutes), "成品截止＝完成＋自身保质");
        AdvanceTo(s, t0 + 180);
        Assert.That(DrySlots(s).Single(b => b.RecipeId == "dry_meat").Status, Is.EqualTo(DryingSlot.Done), "肉干 180 分钟完成");
        var dm = s.Items.Single(i => i.ItemId == "dried_meat");
        Assert.That(dm.ExpiresAt, Is.EqualTo(t0 + 180 + E.Item("dried_meat").ShelfLifeMinutes));
        Assert.That(Xp(s), Is.GreaterThan(x0), "两批都照常给经验");
        Assert.That(s.Items.Count(i => i.ItemId == Rot), Is.EqualTo(0), "无腐败物");
        s.GameMinute = dm.ExpiresAt!.Value;
        Assert.That(E.IsSpoiled(s, dm), Is.True, "留在架上的成品也会到期");
    }

    /// <summary>Q666-A03：已到期原料不能投；取消目的地不合法整次拒绝，R 与归属不变；同请求号重复取消不重复退料。</summary>
    [Test]
    public void Q666_A03_ExpiredRejected_BlockedCancelKeepsSnapshot_Idempotent()
    {
        var s = Rack();
        var t0 = s.GameMinute;
        Make(s, "berry", 2, ItemInstance.Carried, t0);
        Assert.That(E.StartDrying(s, "dry_berries").Ok, Is.False, "已到期原料不投");
        Assert.That(DrySlots(s), Is.Empty);
        s.Items.RemoveAll(i => i.ItemId == "berry");

        var berries = Make(s, "berry", 2, ItemInstance.Carried, t0 + 40);
        Assert.That(E.StartDrying(s, "dry_berries").Ok, Is.True);
        AdvanceTo(s, t0 + 30);
        var knives = new List<ItemInstance>();
        for (var k = 0; k < 5; k++) knives.AddRange(E.CreateItems(s, "stone_knife", 1, ItemInstance.Carried));
        Assert.That(E.CancelDrying(s, 0, true).Ok, Is.False, "随身满整次拒绝");
        Assert.That(berries.Select(i => (i.InDrying, i.ShelfPausedMinutes, i.ExpiresAt)), Is.All.EqualTo((true, (int?)40, (int?)null)), "不半退、快照不变");
        s.Items.RemoveAll(i => knives.Contains(i));

        Assert.That(E.CancelDrying(s, 0, false, "rq-cancel").Ok, Is.True);
        var n = s.Items.Where(i => i.ItemId == "berry").Sum(i => i.Count);
        var logs = s.Log.Count;
        E.CancelDrying(s, 0, false, "rq-cancel");
        Assert.That(s.Items.Where(i => i.ItemId == "berry").Sum(i => i.Count), Is.EqualTo(n), "重复请求不重复退料");
        Assert.That(s.Log.Count, Is.EqualTo(logs), "重复请求不重复写日志");
        Assert.That(n, Is.EqualTo(2));
    }

    /// <summary>
    /// Q666-A04／Q714-A01：52 档在制果干位从升级分钟 U 起保护（R＝旧E−U），进度与经验快照不动；已有肉干快照的不重算；
    /// 已腐败停止的不复活（标旧档停止原因）；升级时已到期的不保护；反复读档不增加 R；迁移静默；预览、位行不再有腐败假警告。
    /// </summary>
    [Test]
    public void Q666_A04_Q714_A01_Migrate52_ProtectsFromUpgrade_TrustedKept_NoRevive()
    {
        var s = Rack();
        var u = s.GameMinute;
        var place = s.Ground;
        var fac = DrySlots(s);
        DryingSlot Slot(int k, string recipe, string status, int done, int req, bool paused = false)
        {
            var b = new DryingSlot { Slot = k, BatchId = 950 + k, RecipeId = recipe, Status = status, StartMinute = u - done, DoneMinutes = done, RequiredMinutes = req,
                                     InitiatorActorId = GameState.PlayerId, XpSnapshot = 12, ShelfPaused = paused };
            fac.Add(b);
            return b;
        }
        Slot(0, "dry_berries", DryingSlot.Drying, 40, 120);
        var fruit = Make(s, "berry", 2, ItemInstance.DryingOwner(place, 0), u + 45);
        Slot(1, "dry_meat", DryingSlot.Drying, 60, 180, true);
        var meat = Make(s, "raw_meat", 1, ItemInstance.DryingOwner(place, 1));
        meat[0].ShelfPausedMinutes = 30;
        meat[0].ExpiresAt = null;
        Make(s, "salt", 1, ItemInstance.DryingOwner(place, 1));
        Slot(2, "dry_berries", DryingSlot.Spoiled, 55, 120);
        Make(s, "berry", 2, ItemInstance.DryingOwner(place, 2), u - 30);
        Slot(3, "dry_berries", DryingSlot.Drying, 100, 120);
        Make(s, "berry", 2, ItemInstance.DryingOwner(place, 3), u);
        var logs = s.Log.Count;

        var t = SaveSerializer.Deserialize(AsSchema(s, "52"), Cfg);
        Assert.That(t.SchemaVersion, Is.EqualTo("53"));
        Assert.That(t.Log.Count, Is.EqualTo(logs), "52→53 静默，不写说明");
        var ts = DrySlots(t);
        var f0 = t.Items.Where(i => i.Owner == ItemInstance.DryingOwner(place, 0)).ToList();
        Assert.That(f0.Select(i => (i.ShelfPausedMinutes, i.ExpiresAt)), Is.All.EqualTo(((int?)45, (int?)null)), "R＝旧E−U，不倒推补偿");
        var b0 = ts.Single(b => b.Slot == 0);
        Assert.That((b0.ShelfPaused, b0.DoneMinutes, b0.XpSnapshot, b0.Status), Is.EqualTo((true, 40, 12, DryingSlot.Drying)), "进度与经验快照不动");
        Assert.That(t.Items.Single(i => i.InstanceId == meat[0].InstanceId).ShelfPausedMinutes, Is.EqualTo(30), "可信肉干快照不重算");
        var b2 = ts.Single(b => b.Slot == 2);
        Assert.That((b2.Status, b2.StopReason), Is.EqualTo((DryingSlot.Spoiled, DryingSlot.StopLegacy)), "已腐败停止不复活，记旧档停止原因");
        Assert.That(ts.Single(b => b.Slot == 3).ShelfPaused, Is.False, "升级时已到期的不保护");

        var view = E.DryingSlotViews(t).Single(v => v.Slot == 0);
        Assert.That(view.Line, Does.Contain(GameEngine.DryingShelfPauseText).And.Not.Contain("会在完成前腐败"), "位行不再有腐败假警告");

        var r1 = SaveSerializer.Deserialize(SaveSerializer.Serialize(t), Cfg);
        AdvanceTo(r1, u + 60);
        var r2 = SaveSerializer.Deserialize(SaveSerializer.Serialize(r1), Cfg);
        Assert.That(r2.Items.Where(i => i.Owner == ItemInstance.DryingOwner(place, 0)).Select(i => i.ShelfPausedMinutes), Is.All.EqualTo(45), "反复读档不增加 R");
        AdvanceTo(r2, u + 80);
        Assert.That(DrySlots(r2).Single(b => b.Slot == 0).Status, Is.EqualTo(DryingSlot.Done), "越过旧截止照常晾好（40＋80＝120）");
        Assert.That(r2.Items.Count(i => i.ItemId == "dried_berries"), Is.EqualTo(1), "只出一份");
    }
}
