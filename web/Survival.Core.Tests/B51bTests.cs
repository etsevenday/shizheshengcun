using Newtonsoft.Json.Linq;
using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 批次 B51b（需求 R252、R253、R258 的 B51b 部分；S08 文首 Q701.2、B51 Q721／Q725／Q727／Q728，R09 Q702／Q703）：
/// 陶窑与固定净水台全部并行的原料保质、取消本批恢复句、存档 54→55 迁移、未预估字段与预估提醒。
/// </summary>
public class B51bTests : TestBase
{
    private const string Water = "ex_station_water";
    private const string Kiln = "ex_ceramic_shell";

    private void Ok(ActionResult r) => Assert.That(r.Ok, Is.True, r.Reason);

    private static ActionRequest Craft(string id, int n = 1) => ActionRequest.Craft(id, null, null, n);

    private static int Sum(GameState s, string itemId) => s.Items.Where(i => i.ItemId == itemId).Sum(i => i.Count);

    private List<StationBatch> Batches(GameState s, string station) =>
        E.FacOf(s, s.Ground)!.StationBatches.Where(b => b.StationId == station).ToList();

    private GameState Workshop()
    {
        var s = New();
        s.Skills["crafting"].Level = 3;
        s.Progress.Flags.Add("h03_ceramic_notice_read");
        foreach (var (id, n) in new[] { ("stone", 8), ("clay", 6), ("iron_plate", 2) }) E.CreateItems(s, id, n, s.Ground);
        Start(s, Craft("build_kiln"));
        Run(s);
        foreach (var (id, n) in new[] { ("ceramic_shell", 4), ("plank", 2), ("cloth", 2) }) E.CreateItems(s, id, n, s.Ground);
        Start(s, Craft("build_water_station"));
        Run(s);
        s.Items.RemoveAll(i => i.ItemId is "clay" or "charcoal" or "dirty_water" or "filter_media" or "ceramic_shell" or "clean_water");
        s.Skills["crafting"].Level = 3;
        s.Skills["crafting"].Xp = 0;
        return s;
    }

    /// <summary>给当前地面放 batches 批净水原料；脏水带一个有限截止（现有配置的设施原料都无期限，用测试注入验证通用保质路径）。</summary>
    private void WaterInputs(GameState s, int batches, int? expiresIn = null)
    {
        E.CreateItems(s, "dirty_water", 4 * batches, s.Ground);
        E.CreateItems(s, "filter_media", batches, s.Ground);
        if (expiresIn is int m)
            foreach (var i in s.Items.Where(i => i.ItemId == "dirty_water")) i.ExpiresAt = s.GameMinute + m;
    }

    // ───────────── Q701.1：设施批次输入暂停保质 ─────────────

    /// <summary>Q701-A02（设施批）：投料即对本批有期限输入记 R＝E−S，在制越过原截止不腐败；取消在 C 恢复 C＋R 并接恢复句；未投入余料照常计时。</summary>
    [Test]
    public void Q701_StationInputs_PauseShelf_CancelRestores()
    {
        var s = Workshop();
        WaterInputs(s, 1, 20);
        E.CreateItems(s, "dirty_water", 2, s.Ground);
        foreach (var i in s.Items.Where(i => i.ItemId == "dirty_water")) i.ExpiresAt = s.GameMinute + 20;
        var t0 = s.GameMinute;
        Ok(E.StartStationBatch(s, Water, "p1"));
        var bound = s.Items.Where(i => i.ReservedBy != null && i.ItemId == "dirty_water").ToList();
        Assert.That(bound.Sum(i => i.Count), Is.EqualTo(4));
        Assert.That(bound.All(i => i.ShelfPausedMinutes == 20), Is.True, "本批输入 R＝E−S");
        var free = s.Items.Single(i => i.ItemId == "dirty_water" && i.ReservedBy == null);
        Assert.That(free.ShelfPausedMinutes, Is.Null, "未投入余料不保护");
        AdvanceTo(s, t0 + 25);
        Assert.That(s.Items.Where(i => i.ReservedBy != null && i.ItemId == "dirty_water").Sum(i => i.Count), Is.EqualTo(4), "在制越过原截止不转腐败物");
        Assert.That(s.Items.Any(i => i.ItemId == "dirty_water" && i.ReservedBy == null), Is.False, "余料照常到期");
        var b = Batches(s, "water_station").Single();
        Ok(E.CancelStationBatch(s, "water_station", b.BatchId, "p1c", 1));
        var back = s.Items.Where(i => i.ItemId == "dirty_water").ToList();
        Assert.That(back.Sum(i => i.Count), Is.EqualTo(4));
        Assert.That(back.All(i => i.ShelfPausedMinutes == null && i.ExpiresAt == s.GameMinute + 20 && i.ReservedBy == null), Is.True, "恢复 C＋R");
        var zone = E.ZoneName(s, s.Ground);
        Assert.That(s.Log.Last(l => l.Kind == nameof(GameEventKind.ActionCancelled)).Text,
            Is.EqualTo($"已取消{zone}固定净水台的批次1 · {Cfg.Recipes.Single(r => r.Id == Water).Name}（已进行25分钟）。未消耗原料已退回{zone}地面。" +
                       $"{E.ItemName("dirty_water")}×4恢复剩余保质，保质到{Clock.DayTime(s.GameMinute + 20)}。没有产物和经验。已耗时间保留。"));
    }

    /// <summary>Q701-A03：受保护输入超过原截止仍按时完成核销；完成产物正常出、无残留 R。</summary>
    [Test]
    public void Q701_StationInputs_ProtectedStillComplete()
    {
        var s = Workshop();
        WaterInputs(s, 2, 10);
        var t0 = s.GameMinute;
        Ok(E.StartStationBatch(s, Water, "p2", 2));
        AdvanceTo(s, t0 + 30);
        Assert.That(Sum(s, "clean_water"), Is.EqualTo(8));
        Assert.That(Sum(s, "dirty_water") + Sum(s, GameEngine.SpoiledMatterId), Is.EqualTo(0), "受保护原料直接核销，不转腐败物");
    }

    /// <summary>Q727：取消无实际可返料的批用无返料版全文。</summary>
    [Test]
    public void Q727_CancelNoReturn_Text()
    {
        var s = Workshop();
        WaterInputs(s, 1);
        Ok(E.StartStationBatch(s, Water, "nr"));
        var b = Batches(s, "water_station").Single();
        s.Items.RemoveAll(i => i.ReservedBy == b.BatchId);
        Ok(E.CancelStationBatch(s, "water_station", b.BatchId, "nrc"));
        var zone = E.ZoneName(s, s.Ground);
        Assert.That(s.Log.Last(l => l.Kind == nameof(GameEventKind.ActionCancelled)).Text,
            Is.EqualTo($"已取消{zone}固定净水台的批次1 · {Cfg.Recipes.Single(r => r.Id == Water).Name}（已进行0分钟）。没有可返还的剩余原料，没有产物和经验。已耗时间保留。"));
    }
}
