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
}
