using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>地图清理案 5.1 抽取契约：C09 区间边界、C14/E12 清理与探索共用同一张表。</summary>
public class LootTableTests : TestBase
{
    [Test]
    public void C09_WoodsBoundaries()
    {
        var woods = Cfg.LootTables["loose_woods"];
        var expect = new (int r, string? item)[]
        {
            (19, null), (20, "wood"), (49, "wood"), (50, "fiber"), (89, "stone"), (90, "berry"), (99, "berry"),
        };
        foreach (var (r, item) in expect)
            Assert.That(LootTable.Pick(woods, r).ItemId, Is.EqualTo(item), $"r={r}");
        Assert.That(LootTable.Pick(woods, 0).ItemId, Is.Null);
        Assert.That(LootTable.Pick(woods, 74).ItemId, Is.EqualTo("fiber"));
        Assert.That(LootTable.Pick(woods, 75).ItemId, Is.EqualTo("stone"));
    }

    [Test]
    public void EveryRollHitsExactlyOneEntry_CountsMatchWeights()
    {
        foreach (var kv in Cfg.LootTables)
        {
            var hits = new Dictionary<int, int>();
            for (var r = 0; r < 100; r++)
            {
                var idx = kv.Value.Entries.IndexOf(LootTable.Pick(kv.Value, r));
                hits[idx] = hits.TryGetValue(idx, out var n) ? n + 1 : 1;
            }
            for (var i = 0; i < kv.Value.Entries.Count; i++)
                Assert.That(hits.TryGetValue(i, out var n) ? n : 0, Is.EqualTo(kv.Value.Entries[i].Weight), $"{kv.Key}[{i}]");
        }
    }

    [Test]
    public void C14_ExploreAndClearShareOneTable()
    {
        foreach (var l in Cfg.Locations)
        {
            Assert.That(Cfg.LootTables.ContainsKey(l.LootTableId), l.Id);
            Assert.That(E.LootTableOf(l.Id), Is.SameAs(Cfg.LootTables[l.LootTableId]));
        }
        // 预览里两种行动列出的概率完全一致
        var s = New();
        E.DebugTeleport(s, "woods");
        var ex = E.Preview(s, ActionRequest.Explore()).LootOdds.Select(x => (x.ItemId, x.Percent)).ToList();
        var cl = E.Preview(s, ActionRequest.Clear()).LootOdds.Select(x => (x.ItemId, x.Percent)).ToList();
        Assert.That(ex, Is.EqualTo(cl));
        Assert.That(ex.First(), Is.EqualTo(((string?)null, 20)), "含未获得物资一行");
    }

    [Test]
    public void E12_SameRollSameResult_DifferentCost()
    {
        for (uint seed = 1; seed <= 40; seed++)
        {
            var a = New();
            E.DebugTeleport(a, "woods");
            var b = a.Clone();
            a.Rng.State = seed * 2654435761u;
            b.Rng.State = a.Rng.State;
            Start(a, ActionRequest.Explore());
            Start(b, ActionRequest.Clear());
            Assert.That(a.Main!.LootRoll, Is.EqualTo(b.Main!.LootRoll));
            Assert.That(a.Main.LootItemId, Is.EqualTo(b.Main.LootItemId));
            Assert.That(a.Main.LootCount, Is.EqualTo(b.Main.LootCount));
            Assert.That(a.Main.BaseWorkMinutes, Is.EqualTo(30));
            Assert.That(b.Main.BaseWorkMinutes, Is.EqualTo(20));
            Assert.That(a.Locations["woods"].ClearCharges, Is.EqualTo(4), "探索不扣清理次数");
            Assert.That(b.Locations["woods"].ClearCharges, Is.EqualTo(3));
        }
    }
}
