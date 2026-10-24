using Newtonsoft.Json.Linq;
using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 存档升级链 0.2-proto-1 → 3 → 4：交互与地图扩展 00 案第 5 节、03 案 E14，设施与烹饪 00 案第 6 节。
/// 夹具是对应版本程序真实写出的存档（Fixtures/save-0.2-proto-1-*.json、save-3-*.json）。
/// </summary>
public class MigrationTests : TestBase
{
    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "Fixtures", $"save-0.2-proto-1-{name}.json"));

    private static string Fixture3(string name) =>
        File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "Fixtures", $"save-3-{name}.json"));

    private const string Fire = "facility:camp:fire";

    /// <summary>迁移后再存再读不变，对 4 再调用迁移也不变（迁移完成标记即 schema 4）。</summary>
    private void AssertIdempotent(GameState s)
    {
        var json = SaveSerializer.Serialize(s);
        Assert.That(SaveSerializer.NeedsMigration(json), Is.False);
        Assert.That(SaveSerializer.Serialize(SaveSerializer.Deserialize(json, Cfg)), Is.EqualTo(json));
        Assert.That(SaveMigrator.Migrate(s, Cfg), Is.Empty);
        Assert.That(SaveSerializer.Serialize(s), Is.EqualTo(json));
    }

    [Test]
    public void V3_AttachedPotIdle_MovesIntoFireOnce_LayoutEntryRemoved()
    {
        var json = Fixture3("attached-idle");
        Assert.That(SaveSerializer.NeedsMigration(json), Is.True);
        Assert.Throws<SaveVersionException>(() => SaveSerializer.Deserialize(json), "没有配置不能迁移");
        var s = SaveSerializer.Deserialize(json, Cfg, out var notes);
        Assert.That(s.SchemaVersion, Is.EqualTo("4"));
        var pot = s.Items.Single(i => i.ItemId == "pot");
        Assert.That(pot.InstanceId, Is.EqualTo(1), "保留锅 ID");
        Assert.That(pot.Owner, Is.EqualTo(Fire), "已架锅 owner 转为火堆");
        Assert.That(s.Items.Count(i => i.InstanceId == pot.InstanceId), Is.EqualTo(1), "每口锅只出现一次");
        Assert.That(E.PotLabel(s, pot), Is.EqualTo("脏水100%·1000/1000毫升"), "保留锅内水");
        var fac = s.Locations["camp"].Facility;
        Assert.That(fac.AttachedPotId, Is.Null);
        Assert.That(fac.FacilityId, Is.EqualTo("camp:fire"));
        Assert.That(fac.ProcessingSlots, Is.EqualTo(new int?[] { 1, null }));
        Assert.That(s.Layout["camp"], Does.Not.Contain("camp|pot|1"), "去掉原区域卡片顺序");
        Assert.That(E.OrderedCards(s, "camp").Any(k => k.Contains("|pot|")), Is.False);
        Assert.That(notes.Single(), Does.Contain("金属锅#1"));
        AssertIdempotent(s);
        // 迁移后按新规则：点火后自动烧水
        s.Locations["camp"].Facility.FireLit = true;
        s.Locations["camp"].Facility.FuelMinutes = 60;
        Start(s, ActionRequest.Wait(30));
        Run(s);
        Assert.That(E.PotLiquid(s, pot.InstanceId), Is.EqualTo("clean_water"));
    }

    [Test]
    public void V3_ActiveBoil_BecomesFacilityProcess_HeatKept_MainReleased_NoRefill()
    {
        var s = SaveSerializer.Deserialize(Fixture3("boil"), Cfg, out var notes);
        Assert.That(s.Main, Is.Null, "释放角色主行动");
        var pot = s.Items.Single(i => i.ItemId == "pot");
        Assert.That(pot.Owner, Is.EqualTo(Fire), "锅移入原营地火堆");
        Assert.That(s.Items.All(i => i.ReservedBy == null));
        var p = E.ProcessOf(s, pot)!;
        Assert.That(p.RecipeId, Is.EqualTo("boil_water"));
        Assert.That(p.HeatDoneMinutes, Is.EqualTo(5), "已完成工作分钟转为受热分钟");
        Assert.That(Count(s, "dirty_water", "pot:" + pot.InstanceId), Is.EqualTo(2), "不补水、不生成净水");
        Assert.That(Count(s, "clean_water"), Is.EqualTo(2), "只有开局的地面净水");
        Assert.That(notes.Any(n => n.Contains("转为火堆自动加工")), Is.True);
        AssertIdempotent(s);
        Start(s, ActionRequest.Wait(30));
        Steps(s, 2);
        Assert.That(E.PotLiquid(s, pot.InstanceId), Is.EqualTo("clean_water"), "再受热10分钟即完成，共15分钟");
        Assert.That(s.Progress.Flags, Does.Contain("boiled"));
    }

    [Test]
    public void V3_ActiveCook_CancelledOnce_RootsBackToGround_NoCookedFood()
    {
        var json = Fixture3("cook");
        var s = SaveSerializer.Deserialize(json, Cfg, out var notes);
        Assert.That(s.Main, Is.Null);
        Assert.That(Count(s, "raw_root", "camp"), Is.EqualTo(3), "预留的2份一次返还当前地面（另有未预留1份）");
        Assert.That(Count(s, "raw_root"), Is.EqualTo(3), "不重复返还");
        Assert.That(Count(s, "cooked_root") + Count(s, "boiled_root"), Is.EqualTo(0), "不补发熟食");
        Assert.That(s.Items.All(i => i.ReservedBy == null));
        Assert.That(E.IsLocked(s, s.Items.Single(i => i.ItemId == "pot")), Is.False);
        Assert.That(s.Items.Single(i => i.ItemId == "pot").Owner, Is.EqualTo("carried"), "不自动放进火堆");
        Assert.That(notes.Single(), Does.Contain("旧烹饪已取消").And.Contain("生块根×2"));
        AssertIdempotent(s);
        var again = SaveSerializer.Deserialize(SaveSerializer.Serialize(s), Cfg);
        Assert.That(Count(again, "raw_root"), Is.EqualTo(3), "读新档不重复返还");
    }

    [Test]
    public void V3_Conflict_DifferentPots_ThrowsAndKeepsOriginal()
    {
        var json = Fixture3("conflict");
        var ex = Assert.Throws<SaveMigrationConflictException>(() => SaveSerializer.Deserialize(json, Cfg))!;
        Assert.That(ex.Message, Does.Contain("金属锅#9").And.Contain("金属锅#1"));
        Assert.That(ex.Message, Does.Contain("不能凭默认顺序挑一把"));
        Assert.That(json, Is.EqualTo(Fixture3("conflict")), "原档原文不变");
        // 状态对象上直接迁移也不改任何东西
        var raw = Newtonsoft.Json.JsonConvert.DeserializeObject<GameState>(json, new Newtonsoft.Json.JsonSerializerSettings
        {
            ObjectCreationHandling = Newtonsoft.Json.ObjectCreationHandling.Replace,
            Converters = { new Newtonsoft.Json.Converters.StringEnumConverter() },
        })!;
        var before = SaveSerializer.Serialize(raw);
        Assert.Throws<SaveMigrationConflictException>(() => SaveMigrator.Migrate(raw, Cfg));
        Assert.That(SaveSerializer.Serialize(raw), Is.EqualTo(before), "冲突时不移动、不丢弃任何物品");
    }

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
        Assert.That(s.SchemaVersion, Is.EqualTo("4"), "0.2 → 3 → 4 链式升级");
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
        Assert.That(SaveSerializer.Serialize(s), Is.EqualTo(again), "对已升级的存档调用迁移不改任何东西");

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
    public void ActiveLegacyBoil_CancelledOnce_DirtyWaterBackOnGround()
    {
        // v0.4 烧水不再是角色配方：首版的进行中烧水（脏水是地面散卡、锅为空）取消，预留脏水一次返还当前地面，不生成净水
        var s = SaveSerializer.Deserialize(Fixture("boil"), Cfg);
        var pot = s.Items.Single(i => i.ItemId == "pot");
        Assert.That(s.Main, Is.Null, "释放角色主行动");
        Assert.That(Count(s, "dirty_water", "camp"), Is.EqualTo(2), "原料一次返还当前地面");
        Assert.That(Count(s, "dirty_water"), Is.EqualTo(2), "不重复返还");
        Assert.That(Count(s, "clean_water"), Is.EqualTo(2), "不补发净水");
        Assert.That(E.PotUnits(s, pot.InstanceId), Is.EqualTo(0), "不自动装锅");
        Assert.That(s.Items.All(i => i.ReservedBy == null));
        Assert.That(E.IsLocked(s, pot), Is.False);
        Assert.That(s.Log.Any(l => l.Text.Contains("旧版烧水已取消")), Is.True);
        Assert.That(s.Log.Last().Text, Does.Contain("升级到版本4"));
        AssertIdempotent(s);
        Assert.That(E.DoInstant(s, InstantRequest.FillPot(First(s, "dirty_water", "camp").InstanceId, pot.InstanceId, 2)).Ok, Is.True, "可按新方式装锅");
    }
}
