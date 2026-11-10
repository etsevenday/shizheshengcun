using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 B17（R56）：陶窑与固定净水台（A07 X1.4、S11 X4）——只建在 camp 外部或已完工安全屋屋内、每区每类一座；
/// 被动批次投料 0 分钟、离开继续、读档不重复、取消退料时间不退；陶窑每批另耗木炭1。验收 ES05、SUP07。
/// </summary>
public class B17StationTests : TestBase
{
    private void Ok(ActionResult r) => Assert.That(r.Ok, Is.True, r.Reason);

    private static ActionRequest Craft(string id) => ActionRequest.Craft(id, null, null, 1);

    private static int Sum(GameState s, string itemId, string? owner = null) =>
        s.Items.Where(i => i.ItemId == itemId && (owner == null || i.Owner == owner)).Sum(i => i.Count);

    private GameState Potter()
    {
        var s = New();
        s.Skills["crafting"].Level = 3;
        s.Progress.Flags.Add("h03_ceramic_notice_read");
        return s;
    }

    private void BuildKiln(GameState s)
    {
        foreach (var (id, n) in new[] { ("stone", 8), ("clay", 6), ("iron_plate", 2) }) E.CreateItems(s, id, n, s.Ground);
        Start(s, Craft("build_kiln"));
        Run(s);
        Assert.That(E.WorkshopBuilt(s, s.Ground, "kiln"), Is.True);
    }

    [Test]
    public void ES05_Kiln_BuildCampOnly_BatchConsumesCharcoal_LeavesAndCompletesOnce()
    {
        var s = Potter();
        var rc = Cfg.Recipes.Single(r => r.Id == "build_kiln");
        Assert.That((rc.BaseMinutes, rc.MinSkills["crafting"], rc.Knowledge), Is.EqualTo((180, 3, "ceramic_methods")));
        // 普通野外外部区不能建
        var w = Potter();
        E.DebugTeleport(w, "w13_mill");
        Assert.That(E.CanStart(w, Craft("build_kiln")), Is.EqualTo(GameEngine.WorkshopBuildOnly));

        BuildKiln(s);
        s.Items.RemoveAll(i => i.ItemId is "clay" or "charcoal" or "ceramic_shell");
        E.CreateItems(s, "clay", 2, ItemInstance.Carried);
        Assert.That(E.StationBatchBlocked(s, Cfg.Recipes.Single(r => r.Id == "ex_ceramic_shell")), Does.Contain("木炭"), "每批另耗木炭1");
        E.CreateItems(s, "charcoal", 1, "camp");
        var t0 = s.GameMinute;
        Ok(E.StartStationBatch(s, "ex_ceramic_shell"));
        Assert.That(s.GameMinute, Is.EqualTo(t0), "投料 0 分钟");
        Assert.That(s.Main, Is.Null, "不占行动");
        Assert.That(s.Items.Where(i => i.ItemId is "clay" or "charcoal").All(i => i.Owner == "camp" && i.ReservedBy != null), Is.True);
        // B32e（Q380）：取代「每次只能一批」——工位忙时可加入队尾，这里只因原料已全部预留而拒绝
        Assert.That(E.StartStationBatch(s, "ex_ceramic_shell").Reason, Does.Contain("缺少黏土"));

        // 离开营地：同一世界时钟继续；读档后只出一次
        var next = E.AdjacentUnlocked(s).First().Other("camp");
        Start(s, ActionRequest.Travel(next));
        Run(s);
        var json = SaveSerializer.Serialize(s);
        var t = SaveSerializer.Deserialize(json, Cfg);
        AdvanceTo(t, t0 + 60);
        Assert.That(Sum(t, "ceramic_shell", "camp"), Is.EqualTo(2), "产物落在陶窑所在区地面");
        Assert.That(Sum(t, "clay") + Sum(t, "charcoal"), Is.EqualTo(0), "黏土2与燃料木炭1一次核销");
        Assert.That(t.Locations["camp"].Facility.StationBatches, Is.Empty);
        AdvanceTo(t, t0 + 120);
        Assert.That(Sum(t, "ceramic_shell"), Is.EqualTo(2), "不重复出");
    }

    [Test]
    public void ES05_Cancel_ReleasesInputs_NoRemote()
    {
        var s = Potter();
        BuildKiln(s);
        s.Items.RemoveAll(i => i.ItemId is "clay" or "charcoal");
        E.CreateItems(s, "clay", 2, ItemInstance.Carried);
        E.CreateItems(s, "charcoal", 1, ItemInstance.Carried);
        Ok(E.StartStationBatch(s, "ex_ceramic_shell"));
        AdvanceTo(s, s.GameMinute + 30);
        var next = E.AdjacentUnlocked(s).First().Other("camp");
        Start(s, ActionRequest.Travel(next));
        Run(s);
        Assert.That(E.CancelStationBatch(s, "kiln").Ok, Is.False, "不能远程取消/出货");
        E.DebugTeleport(s, "camp");
        Ok(E.CancelStationBatch(s, "kiln"));
        Assert.That((Sum(s, "clay", "camp"), Sum(s, "charcoal", "camp"), Sum(s, "ceramic_shell")), Is.EqualTo((2, 1, 0)));
        Assert.That(s.Items.Any(i => i.ReservedBy != null), Is.False);
    }

    [Test]
    public void ES05_WaterStation_InsideSafehouse_FourDirtyToFourClean()
    {
        var s = Potter();
        E.DebugSetProgress(s, "camp", 100);
        s.Progress.Flags.Add("hub_registered");
        foreach (var (id, n) in new[] { ("wood", 8), ("fiber", 6), ("cloth", 2), ("stone", 4) }) E.CreateItems(s, id, n, s.Ground);
        Start(s, Craft("safehouse_base"));
        Run(s);
        Ok(E.EnterSafehouse(s));
        foreach (var (id, n) in new[] { ("ceramic_shell", 4), ("plank", 2), ("cloth", 2) }) E.CreateItems(s, id, n, s.Ground);
        Start(s, Craft("build_water_station"));
        Run(s);
        Assert.That(s.Locations["camp"].Safehouse!.Interior.Workshops, Does.Contain("water_station"));
        Assert.That(s.Locations["camp"].Facility.Workshops, Does.Not.Contain("water_station"), "建在屋内区，不在外部");
        s.Items.RemoveAll(i => i.ItemId is "dirty_water" or "clean_water" or "filter_media");
        E.CreateItems(s, "dirty_water", 4, s.Ground);
        E.CreateItems(s, "filter_media", 1, ItemInstance.Carried);
        var t0 = s.GameMinute;
        Ok(E.StartStationBatch(s, "ex_station_water"));
        // 屋外看不到/不能用屋内净水台
        Ok(E.ExitSafehouse(s));
        Assert.That(E.StationBatchHere(s, "water_station"), Is.Null);
        AdvanceTo(s, t0 + 30);
        Assert.That(Sum(s, "clean_water", "safehouseFloor:sh_camp"), Is.EqualTo(4));
        Assert.That(Sum(s, "dirty_water") + Sum(s, "filter_media"), Is.EqualTo(0));
    }
}
