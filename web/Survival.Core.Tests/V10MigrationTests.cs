using Newtonsoft.Json.Linq;
using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 存档 schema 5 → 6（系统策划总册 v1.0，T06/P02）：夹具 save-5-*.json 由提交 756fc19（schema 5）的程序真实写出。
/// 锅去耐久并保留 ID/内容/受热进度；旧档不补开场、首战、教学物质与+0.1，不启用饱饱输入；进行中的锅维护取消并返还材料；迁移幂等。
/// </summary>
public class V10MigrationTests : TestBase
{
    private static string Fixture5(string name) =>
        File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "Fixtures", $"save-5-{name}.json"));

    [Test]
    public void Schema5To6_PotLosesDurability_KeepsContentAndProgress()
    {
        var json = Fixture5("midgame");
        var old = JObject.Parse(json);
        Assert.That((string)old["SchemaVersion"]!, Is.EqualTo("5"));
        Assert.That(SaveSerializer.NeedsMigration(json), Is.True);
        var s = SaveSerializer.Deserialize(json, Cfg, out var notes);
        Assert.That(s.SchemaVersion, Is.EqualTo(GameState.CurrentSchema), "5→6→7 整条链");

        var pots = s.Items.Where(i => i.ItemId == "pot").ToList();
        Assert.That(pots, Has.Count.EqualTo(2), "不新建、不复制锅");
        Assert.That(pots.All(p => p.Durability == null && p.MaxDurability == null), Is.True, "旧锅去掉耐久（含耐久0的备用锅）");
        var firePot = pots.Single(p => p.Owner == "facility:camp:fire");
        Assert.That(firePot.InstanceId, Is.EqualTo(1), "保留锅 ID 与归属");
        Assert.That(E.PotLiquid(s, firePot.InstanceId), Is.EqualTo("dirty_water"), "保留内容");
        Assert.That(E.PotUnits(s, firePot.InstanceId), Is.EqualTo(2));
        var proc = E.ProcessOf(s, firePot)!;
        Assert.That(proc.HeatDoneMinutes, Is.EqualTo(10), "保留受热进度");
        Assert.That(s.Items.Select(i => i.InstanceId), Is.EquivalentTo(old["Items"]!.Select(i => (int)i["InstanceId"]!)));

        // 旧档：不补新剧情与奖励，不启用饱饱输入；敌人与人物数据原样
        Assert.That(s.Story.Legacy, Is.True);
        Assert.That(s.Story.BaobaoPresent, Is.False);
        Assert.That(s.Story.PendingDialogues, Is.Empty, "不补开场");
        Assert.That(s.Traits, Is.Empty, "不补+0.1");
        Assert.That(s.Items.Any(i => i.ItemId == "active_substance_white_unidentified"), Is.False, "不补教学物质");
        Assert.That(s.WorldEnemies.Count, Is.EqualTo(((JArray)old["WorldEnemies"]!).Count), "旧敌保留");
        var sh = s.WorldEnemies.First(w => w.LocationId == "field");
        Assert.That(sh.Hp, Is.EqualTo(2.1));
        Assert.That(sh.XpHits["unarmed"], Is.EqualTo(1));
        Assert.That(notes.Any(n => n.Contains("没有耐久")), Is.True);
        Assert.That(notes.Any(n => n.Contains("新游戏")), Is.True);

        // 继续游戏：锅照常净水（不因旧耐久0阻塞），到荒地不触发首战，饱饱不输入病毒
        s.Locations["camp"].Facility.FuelMinutes = 120;
        s.Locations["camp"].Facility.FireLit = true;
        Start(s, ActionRequest.Wait(30));
        Run(s);
        Assert.That(E.PotLiquid(s, firePot.InstanceId), Is.EqualTo("clean_water"));
        Assert.That(s.Body.Infected, Is.False);
        Start(s, ActionRequest.Travel("field"));
        Run(s);
        Assert.That(s.Battle, Is.Null, "旧档不触发首战");
        Assert.That(E.EncounterChance(s, "field", ActionType.Explore), Is.EqualTo(0), "第1日仍按原规则无随机接敌");

        // 幂等：再存读不再迁移、不变
        var once = SaveSerializer.Serialize(s);
        Assert.That(SaveSerializer.NeedsMigration(once), Is.False);
        Assert.That(SaveSerializer.Serialize(SaveSerializer.Deserialize(once, Cfg)), Is.EqualTo(once));
        Assert.That(SaveMigrator.Migrate(SaveSerializer.Deserialize(once, Cfg), Cfg), Is.Empty);
    }

    [Test]
    public void Schema5To6_RepairingPot_CancelledMaterialsReturned()
    {
        var json = Fixture5("repair-pot");
        var old = JObject.Parse(json);
        Assert.That((string)old["Main"]!["Type"]!, Is.EqualTo("Repair"));
        var s = SaveSerializer.Deserialize(json, Cfg, out var notes);
        Assert.That(s.Main, Is.Null, "锅维护取消");
        Assert.That(s.Items.Where(i => i.ItemId is "scrap" or "parts").All(i => i.ReservedBy == null && i.Owner == s.CurrentLocation), Is.True, "材料返还当前地面");
        Assert.That(Count(s, "scrap"), Is.EqualTo(1));
        Assert.That(Count(s, "parts"), Is.EqualTo(1));
        Assert.That(notes.Any(n => n.Contains("维护已取消")), Is.True);
        Assert.That(s.Items.Single(i => i.ItemId == "pot").Durability, Is.Null);
    }

    [Test]
    public void Schema5To6_LegacyStillGetsPhaseFilteredSpawns()
    {
        var s = SaveSerializer.Deserialize(Fixture5("midgame"), Cfg);
        // 旧档同样按世界日过滤未来刷新：第2日抽中稀有层不刷
        s.GameMinute = 2000;
        s.Rng.State = 0;
        for (uint c = 1; c < 5_000_000; c++)
        {
            var r = new Rng { State = c * 2654435761u + 7 };
            if (r.NextDouble() < 0.2 && r.NextDouble() is var x && x >= 0.961 && x < 0.994) { s.Rng.State = c * 2654435761u + 7; break; }
        }
        Assert.That(E.DebugSpawnCheck(s), Is.Null);
    }
}
