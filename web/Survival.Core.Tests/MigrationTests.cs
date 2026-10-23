using Newtonsoft.Json.Linq;
using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 00 案第 5 节存档升级与 03 案 E14：用首版程序真实写出的 0.2-proto-1 存档（Fixtures/）验证迁移。
/// </summary>
public class MigrationTests : TestBase
{
    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "Fixtures", $"save-0.2-proto-1-{name}.json"));

    [Test]
    public void LegacyWithoutConfig_Rejected_UnknownRejected()
    {
        var json = Fixture("idle");
        Assert.That(SaveSerializer.NeedsMigration(json), Is.True);
        Assert.Throws<SaveVersionException>(() => SaveSerializer.Deserialize(json));
        var bogus = JObject.Parse(json);
        bogus["SchemaVersion"] = "9";
        Assert.Throws<SaveVersionException>(() => SaveSerializer.Deserialize(bogus.ToString(), Cfg));
    }

    [Test]
    public void Idle_MigratedFields()
    {
        var json = Fixture("idle");
        var old = JObject.Parse(json);
        var s = SaveSerializer.Deserialize(json, Cfg);
        Assert.That(s.SchemaVersion, Is.EqualTo("3"));
        Assert.That(s.GameMinute, Is.EqualTo((int)old["GameMinute"]!));
        foreach (var l in Cfg.Locations)
        {
            var ls = s.Locations[l.Id];
            Assert.That(ls.ClearCharges, Is.EqualTo(l.Clear.Cap), l.Id);
            Assert.That(ls.ClearRefillProgress, Is.EqualTo(0), l.Id);
            Assert.That(ls.ClearUpdatedMinute, Is.EqualTo(s.GameMinute), l.Id);
        }
        Assert.That(s.Layout, Is.Empty);
        Assert.That(s.RecentRequestIds, Is.Empty);
        Assert.That(s.Locations["field"].Progress, Is.EqualTo(25), "已完成探索不重置");
        Assert.That(s.Items.Count, Is.EqualTo(((JArray)old["Items"]!).Count), "已完成探索不补发随机物品");
        var pot = s.Items.Single(i => i.ItemId == "pot");
        Assert.That(E.PotUnits(s, pot.InstanceId), Is.EqualTo(0), "旧锅初始化为空锅");
        Assert.That(Count(s, "clean_water", "camp"), Is.EqualTo(2), "旧水卡原样保留，不自动倒入");
        Assert.That(s.Rng.State, Is.EqualTo((uint)old["Rng"]!["State"]!), "随机状态不回退");

        // 幂等：迁移后的档再次保存读取不再迁移
        var again = SaveSerializer.Serialize(s);
        Assert.That(SaveSerializer.NeedsMigration(again), Is.False);
        Assert.That(SaveSerializer.Serialize(SaveSerializer.Deserialize(again, Cfg)), Is.EqualTo(again));
        SaveMigrator.Migrate(s, Cfg);
        Assert.That(SaveSerializer.Serialize(s), Is.EqualTo(again), "对 schema 3 调用迁移不改任何东西");

        // 升级后新行动按新规则：清理可用，探索带掉落
        Start(s, ActionRequest.Clear());
        Assert.That(s.Locations["camp"].ClearCharges, Is.EqualTo(2));
        Assert.That(s.Main!.LootPlanned, Is.True);
    }

    [Test]
    public void E14_ActiveExplore_LegacyNoLoot_ThenNewRules()
    {
        var s = SaveSerializer.Deserialize(Fixture("explore"), Cfg);
        Assert.That(s.Main!.Type, Is.EqualTo(ActionType.Explore));
        Assert.That(s.Main.LegacyNoLoot, Is.True);
        Assert.That(s.Main.LootPlanned, Is.False);
        var items = s.Items.Sum(i => i.Count);
        Assert.That(Run(s), Is.EqualTo(SegmentStatus.Completed));
        Assert.That(s.Locations["field"].Progress, Is.EqualTo(50));
        Assert.That(s.UnlockedEdges, Does.Contain("field__woods"));
        Assert.That(s.Items.Sum(i => i.Count), Is.EqualTo(items), "旧活动探索按旧流程完成一次，不掉落");

        Start(s, ActionRequest.Explore());
        Assert.That(s.Main!.LegacyNoLoot, Is.False, "后续新探索使用本版");
        Assert.That(s.Main.LootPlanned, Is.True);
        ForceLoot(s, "fiber");
        Run(s);
        Assert.That(Count(s, "fiber", "field"), Is.EqualTo(1));
    }

    [Test]
    public void ActiveBoil_LegacyGroundOutput_CompletesOnGround()
    {
        var s = SaveSerializer.Deserialize(Fixture("boil"), Cfg);
        var pot = s.Items.Single(i => i.ItemId == "pot");
        Assert.That(s.Main!.LegacyGroundOutput, Is.True);
        Assert.That(s.Main.PotInstanceId, Is.EqualTo(pot.InstanceId));
        Assert.That(s.Main.LockedItemIds, Does.Contain(pot.InstanceId), "锅在旧活动结束前锁定");
        Assert.That(E.PotUnits(s, pot.InstanceId), Is.EqualTo(0), "锅保持空");
        Assert.That(E.DoInstant(s, InstantRequest.FillPot(First(s, "clean_water", "camp").InstanceId, pot.InstanceId)).Reason,
            Does.StartWith("行动占用"));
        var cleanBefore = Count(s, "clean_water", "camp");
        Assert.That(Run(s), Is.EqualTo(SegmentStatus.Completed));
        Assert.That(Count(s, "clean_water", "camp"), Is.EqualTo(cleanBefore + 2), "完成仍按旧案生成地面净水");
        Assert.That(Count(s, "dirty_water"), Is.EqualTo(0));
        Assert.That(E.PotUnits(s, pot.InstanceId), Is.EqualTo(0));
        Assert.That(s.Progress.Flags, Does.Contain("boiled"));
    }

    [Test]
    public void ActiveBoil_LegacyCancel_ReturnsRawToGround()
    {
        var s = SaveSerializer.Deserialize(Fixture("boil"), Cfg);
        var pot = s.Items.Single(i => i.ItemId == "pot");
        E.Cancel(s);
        Assert.That(Count(s, "dirty_water", "camp"), Is.EqualTo(2), "取消按旧案返还原料到地面");
        Assert.That(E.PotUnits(s, pot.InstanceId), Is.EqualTo(0), "锅保持空");
        Assert.That(s.Items.All(i => i.ReservedBy == null));
        Assert.That(E.IsLocked(s, pot), Is.False);
    }
}
