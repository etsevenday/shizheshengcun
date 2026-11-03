using Newtonsoft.Json.Linq;
using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 批次 B22d：策划答复 Q171、Q172、Q174—Q178（策划提交 20262b0，需求 R170、R171、R173—R177）。
/// 规则出处：assets/20261103/camping-appraisal-adjudications.json（passiveXp、appraisal、acceptance）、expansion-supply.json passiveCraftXp、
/// appraisal-service-contract.json q176Q178。验收 PLAN-Q171-01 … PLAN-Q178-01、PLAN-Q174-02、PLAN-Q176-02。Q173（野外野营布与固定火堆）属 B22e，不在这里。
/// </summary>
public class B22dTests : TestBase
{
    private void Ok(ActionResult r) => Assert.That(r.Ok, Is.True, r.Reason);

    private static ActionRequest Craft(string id) => ActionRequest.Craft(id, null, null, 1);

    private static JObject Design(string name) => JObject.Parse(File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, name)));

    private static int Sum(GameState s, string itemId, string? owner = null) =>
        s.Items.Where(i => i.ItemId == itemId && (owner == null || i.Owner == owner)).Sum(i => i.Count);

    private static int StationXpToday(GameState s, int? day = null) =>
        s.Player.DailyCounts.TryGetValue($"{day ?? Clock.Day(s.GameMinute)}|stationXp|crafting", out var n) ? n : 0;

    /// <summary>制造级内经验（Workshop() 建完清零，本文件里的增量不会跨级）。</summary>
    private static long CraftXp(GameState s) => s.Skills["crafting"].Xp;

    // ───────────── Q174 陶窑/净水台被动批次制造经验 ─────────────

    /// <summary>营地外部区建好陶窑与固定净水台的制造 3 级角色（建造本身照原规则给经验，与被动日额无关）。</summary>
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
        Assert.That(E.WorkshopBuilt(s, s.Ground, "kiln") && E.WorkshopBuilt(s, s.Ground, "water_station"), Is.True);
        Assert.That(StationXpToday(s), Is.EqualTo(0), "建造经验不计被动批次日额");
        s.Items.RemoveAll(i => i.ItemId is "clay" or "charcoal" or "dirty_water" or "filter_media" or "ceramic_shell" or "clean_water");
        s.Skills["crafting"].Level = 3;
        s.Skills["crafting"].Xp = 0;
        return s;
    }

    private void Kiln(GameState s)
    {
        E.CreateItems(s, "clay", 2, s.Ground);
        E.CreateItems(s, "charcoal", 1, s.Ground);
        Ok(E.StartStationBatch(s, "ex_ceramic_shell"));
    }

    private void Water(GameState s)
    {
        E.CreateItems(s, "dirty_water", 4, s.Ground);
        E.CreateItems(s, "filter_media", 1, s.Ground);
        Ok(E.StartStationBatch(s, "ex_station_water"));
    }

    [Test, Category("DesignSync")]
    public void PLAN_Q174_Contract_MatchesConfig()
    {
        var px = Design("design.camping-appraisal.json")["passiveXp"]!;
        var c = Cfg.Progression.StationXp;
        Assert.That((c.PerBatch["kiln"], c.PerBatch["water_station"], c.DailyCap, c.Skill),
            Is.EqualTo(((int)px["kilnPerBatch"]!, (int)px["waterStationPerBatch"]!, (int)px["sharedDailyCapPerActor"]!, (string)px["skill"]!)));
    }

    /// <summary>PLAN-Q174-01 / PLAN-Q174-02：陶窑 6 + 净水台 3 + 净水台 3 = 12，再完成给 0（产物照出）；不另给烹饪；投料与等待不给。</summary>
    [Test]
    public void PLAN_Q174_02_KilnSixWaterThree_SharedDailyTwelve()
    {
        var s = Workshop();
        var cook0 = s.Skills["cooking"].Xp;
        var xp0 = CraftXp(s);
        var day = Clock.Day(s.GameMinute);
        var craftDaily0 = E.DailyCount(s, s.Player, GameEngine.DailyXp, "crafting");
        Assert.That(E.StationBatchXpPreview(s, Cfg.Recipes.Single(r => r.Id == "ex_ceramic_shell")), Does.Contain("制造经验+6"));
        Kiln(s);
        Assert.That(CraftXp(s), Is.EqualTo(xp0), "投料不给");
        Assert.That(E.StationBatchHere(s, "kiln")!.XpSnapshot, Is.EqualTo(6), "投料时存经验快照");
        AdvanceTo(s, s.GameMinute + 60);
        Assert.That(Sum(s, "ceramic_shell"), Is.EqualTo(2));
        Assert.That(CraftXp(s) - xp0, Is.EqualTo(6), "陶窑整批 6（两个陶壳共 6，不按份）");
        Assert.That(s.Log.Last(l => l.Text.Contains("陶壳")).Text, Does.Contain("制造经验+6"));

        Water(s);
        AdvanceTo(s, s.GameMinute + 30);
        Assert.That(Sum(s, "clean_water"), Is.EqualTo(4));
        Assert.That(CraftXp(s) - xp0, Is.EqualTo(9), "净水台整批 3（2L 共 3）");
        Water(s);
        Assert.That(E.StationBatchXpNote(s, E.StationBatchHere(s, "water_station")!), Does.Contain("+3"));
        AdvanceTo(s, s.GameMinute + 30);
        Assert.That((CraftXp(s) - xp0, StationXpToday(s, day)), Is.EqualTo((12, 12)), "两类共享每日 12");

        Water(s);
        Assert.That(E.StationBatchXpNote(s, E.StationBatchHere(s, "water_station")!), Does.Contain("已满12"));
        AdvanceTo(s, s.GameMinute + 30);
        Assert.That(Sum(s, "clean_water"), Is.EqualTo(12), "日额满了产物照出");
        Assert.That(CraftXp(s) - xp0, Is.EqualTo(12), "日额满后再完成给 0");
        Assert.That(s.Log.Last(l => l.Text.Contains("净水")).Text, Does.Contain("已满12"));
        Assert.That(s.Skills["cooking"].Xp, Is.EqualTo(cook0), "不另给烹饪净化经验");
        Assert.That(E.DailyCount(s, s.Player, GameEngine.DailyXp, "crafting"), Is.EqualTo(craftDaily0), "与主动制造/其他日额独立");
    }

    /// <summary>PLAN-Q174-02：剩余不足给剩余；次日新批按新日额。</summary>
    [Test]
    public void PLAN_Q174_02_RemainderThenNextDay()
    {
        var s = Workshop();
        var xp0 = CraftXp(s);
        Kiln(s);
        AdvanceTo(s, s.GameMinute + 60);
        Water(s);
        AdvanceTo(s, s.GameMinute + 30);
        Kiln(s);   // 已得 9，只剩 3
        Assert.That(E.StationBatchXpNote(s, E.StationBatchHere(s, "kiln")!), Does.Contain("只剩3"));
        AdvanceTo(s, s.GameMinute + 60);
        Assert.That(CraftXp(s) - xp0, Is.EqualTo(12), "剩余不足给剩余（6 只给 3）");
        // 次日
        var day = Clock.Day(s.GameMinute);
        AdvanceTo(s, day * Clock.MinutesPerDay + 8 * 60);
        Kiln(s);
        AdvanceTo(s, s.GameMinute + 60);
        Assert.That(CraftXp(s) - xp0, Is.EqualTo(18), "次日新批按新日额");
    }

    /// <summary>PLAN-Q174-02：旧档在途批次（没有经验快照）按原 0 经验完成；读档不重给；取消 0。</summary>
    [Test]
    public void PLAN_Q174_02_OldBatchZero_SaveLoadOnce_CancelZero()
    {
        var s = Workshop();
        var xp0 = CraftXp(s);
        Kiln(s);
        // 模拟上线前开始的批次：存档里没有 xpSnapshot 字段
        var obj = JObject.Parse(SaveSerializer.Serialize(s));
        foreach (var b in obj.SelectTokens("$..StationBatches[*]").OfType<JObject>()) b.Remove("XpSnapshot");
        var old = SaveSerializer.Deserialize(obj.ToString(), Cfg);
        AdvanceTo(old, old.GameMinute + 60);
        Assert.That(Sum(old, "ceramic_shell"), Is.EqualTo(2));
        Assert.That(CraftXp(old), Is.EqualTo(xp0), "旧批按原 0 经验完成，不补");
        Assert.That(old.Log.Last(l => l.Text.Contains("陶壳")).Text, Does.Contain("开始时被动加工不给经验"));

        // 新批：存档读档后只结算一次（另开一局，免得上面的陶窑批次一起完成）
        var w = Workshop();
        Water(w);
        var t = SaveSerializer.Deserialize(SaveSerializer.Serialize(w), Cfg);
        AdvanceTo(t, t.GameMinute + 30);
        var after = CraftXp(t);
        Assert.That(after, Is.EqualTo(3), "快照随存档保留");
        var again = SaveSerializer.Deserialize(SaveSerializer.Serialize(t), Cfg);
        AdvanceTo(again, again.GameMinute + 60);
        Assert.That(CraftXp(again), Is.EqualTo(after), "领取/读档不重复给");

        // 取消 0
        var c = Workshop();
        var cx = CraftXp(c);
        Water(c);
        AdvanceTo(c, c.GameMinute + 15);
        Ok(E.CancelStationBatch(c, "water_station"));
        AdvanceTo(c, c.GameMinute + 60);
        Assert.That(CraftXp(c), Is.EqualTo(cx), "取消没有经验");
    }
}
