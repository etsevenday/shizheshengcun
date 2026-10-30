using Newtonsoft.Json.Linq;
using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 存档升级链 0.2-proto-1 → 3 → 4 → 5：交互与地图扩展 00 案第 5 节、03 案 E14，设施与烹饪 00 案第 6 节，人物与战斗 v0.6 §16（V05_ 用例）。
/// 夹具是对应版本程序真实写出的存档（Fixtures/save-0.2-proto-1-*.json、save-3-*.json、save-4-*.json）。
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

    private static string Fixture4(string name) =>
        File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "Fixtures", $"save-4-{name}.json"));

    /// <summary>
    /// V05_ / R623：schema 4 真实存档升级到 5：H/E 只缩放一次（反复存读不再缩放），N/W/S/M 不动；
    /// 旧石刀/斧随机新最大耐久并保持旧成色，旧锅/打火器满耐久；技能1级、免疫70、世界敌人初始分布；进行中的主行动保留；迁移幂等。
    /// </summary>
    [Test]
    public void V05_R623_Schema4To5_ScaleOnce_KeepCondition()
    {
        var json = Fixture4("midgame");
        var old = JObject.Parse(json);
        Assert.That(SaveSerializer.NeedsMigration(json), Is.True);
        var s = SaveSerializer.Deserialize(json, Cfg, out var notes);
        Assert.That(s.SchemaVersion, Is.EqualTo(GameState.CurrentSchema), "4→5→6 整条链");
        var os = old["Stats"]!;
        Assert.That(s.Stats.Health, Is.EqualTo((double)os["Health"]! * 0.2).Within(1e-9));
        Assert.That(s.Stats.Energy, Is.EqualTo((double)os["Energy"]! * 0.2).Within(1e-9));
        Assert.That(s.Stats.Satiety, Is.EqualTo((double)os["Satiety"]!), "饱食不缩放");
        Assert.That(s.Stats.Hydration, Is.EqualTo((double)os["Hydration"]!));
        Assert.That(s.Stats.Wakefulness, Is.EqualTo((double)os["Wakefulness"]!));
        Assert.That(s.Stats.Morale, Is.EqualTo((double)os["Morale"]!));
        Assert.That((s.Stats.HealthMax, s.Stats.EnergyMax), Is.EqualTo((20.0, 20.0)));
        // 旧装备成色保持
        foreach (var (id, oldDur, legacy) in new[] { ("stone_knife", 13.0, 20.0), ("hatchet", 21.0, 30.0) })
        {
            var it = s.Items.Single(i => i.ItemId == id);
            var range = Cfg.Items.Single(i => i.Id == id).DurabilityMaxRange!;
            Assert.That(it.MaxDurability, Is.InRange(range[0], range[1]), id);
            Assert.That(it.Durability!.Value / it.MaxDurability!.Value, Is.EqualTo(oldDur / legacy).Within(1e-9), id + " 成色保持");
        }
        var lt = s.Items.Single(i => i.ItemId == "lighter");
        Assert.That(lt.Durability, Is.EqualTo(lt.MaxDurability), "打火器满耐久初始化一次");
        Assert.That(s.Items.Single(i => i.ItemId == "pot").Durability, Is.Null, "v1.0 P02：锅没有耐久");
        Assert.That(s.Story.Legacy, Is.True, "旧档不补新剧情（T06）");
        Assert.That(s.Story.BaobaoPresent, Is.False);
        Assert.That(s.Items.Select(i => i.InstanceId), Is.EquivalentTo(old["Items"]!.Select(i => (int)i["InstanceId"]!)), "实例ID不复制、不重排");
        Assert.That(s.Items.Single(i => i.ItemId == "pot").Owner, Is.EqualTo("facility:camp:fire"), "锅内物品与归属不变");
        Assert.That(s.Main, Is.Not.Null, "进行中的主行动保留");
        Assert.That(s.Main!.Type, Is.EqualTo(ActionType.Gather));
        Assert.That(s.Skills.Values.All(k => k.Level == 1 && k.Xp == 0));
        Assert.That(s.Body.Immunity, Is.EqualTo(70));
        Assert.That(s.Body.Infected, Is.False);
        Assert.That(s.WorldEnemies, Has.Count.EqualTo(6));
        Assert.That(notes.Any(n => n.Contains("0.2")), Is.True);
        // 旧档已越过揭示点的新箱子不补发（五金店75%：武器柜50%）
        Assert.That(s.Resources["hardware_weapon_cache"].Available, Is.EqualTo(0));
        Assert.That(s.Resources["clinic_medical_supply"].Available, Is.EqualTo(0), "诊所100%：医药补给箱不补发");
        // 反复存读：不再缩放、不重抽
        var once = SaveSerializer.Serialize(s);
        Assert.That(SaveSerializer.NeedsMigration(once), Is.False);
        var again = SaveSerializer.Deserialize(once, Cfg);
        Assert.That(SaveSerializer.Serialize(again), Is.EqualTo(once));
        Assert.That(again.Stats.Health, Is.EqualTo(s.Stats.Health), "只缩放一次");
        Assert.That(SaveMigrator.Migrate(again, Cfg), Is.Empty);
        // 医疗与环境伤害按新尺度生效：继续采集，结果按 v0.6 规则
        Run(again);
        Assert.That(again.Main, Is.Null);
        Assert.That(again.Stats.Energy, Is.LessThan(s.Stats.Energy));
    }

    /// <summary>空闲旧档同样升级；对迁移后的档再调用迁移不变。</summary>
    [Test]
    public void V05_IdleSchema4_Migrates()
    {
        var s = SaveSerializer.Deserialize(Fixture4("idle"), Cfg);
        Assert.That(s.SchemaVersion, Is.EqualTo(GameState.CurrentSchema));
        Assert.That(s.Stats.Health, Is.EqualTo(20));
        Assert.That(s.Stats.Energy, Is.EqualTo(18), "旧初值90×0.2=18");
        AssertIdempotent(s);
    }

    /// <summary>
    /// R624 药效/病毒/随机跨存读档：不丢连续时间，不重掷暴露与装备，不重复 tick。
    /// </summary>
    [Test]
    public void V05_R624_AcrossSaveLoad_NoRerollNoLoss()
    {
        var s = New(321);
        s.Body.Infected = true;
        s.Body.VirusLoad = 30;
        s.Body.Immunity = 86;
        s.Body.HighImmunityMinutes = 300;
        s.Body.AntibioticUntil = s.GameMinute + 200;
        s.Body.AntibioticReadyAt = s.GameMinute + 200;
        s.Body.BandageVirusReadyAt = s.GameMinute + 100;
        var knife = E.CreateItems(s, "stone_knife", 1, ItemInstance.Carried).Single();
        Start(s, ActionRequest.Wait(60));
        Steps(s, 3);
        var json = SaveSerializer.Serialize(s);
        var t = SaveSerializer.Deserialize(json);
        Assert.That(t.Body.HighImmunityMinutes, Is.EqualTo(s.Body.HighImmunityMinutes), "不丢连续时间");
        Assert.That(t.Body.AntibioticUntil, Is.EqualTo(s.Body.AntibioticUntil));
        Assert.That(t.Items.Single(i => i.InstanceId == knife.InstanceId).MaxDurability, Is.EqualTo(knife.MaxDurability), "不重抽装备");
        Assert.That(t.GameMinute, Is.EqualTo(s.GameMinute), "读档不推进、不重复 tick");
        Run(s);
        Run(t);
        Assert.That(SaveSerializer.Serialize(t), Is.EqualTo(SaveSerializer.Serialize(s)), "读档后结果完全一致");
        // 暴露判定在提交时抽一次，结果随存档；读档不能重抽
        var u = New(9);
        var before = u.Rng.State;
        E.ExposeToVirus(u, out _);
        var after = SaveSerializer.Deserialize(SaveSerializer.Serialize(u));
        Assert.That(after.Rng.State, Is.Not.EqualTo(before));
        Assert.That(after.Body.Infected, Is.EqualTo(u.Body.Infected));
    }

    [Test]
    public void V3_AttachedPotIdle_MovesIntoFireOnce_LayoutEntryRemoved()
    {
        var json = Fixture3("attached-idle");
        Assert.That(SaveSerializer.NeedsMigration(json), Is.True);
        Assert.Throws<SaveVersionException>(() => SaveSerializer.Deserialize(json), "没有配置不能迁移");
        var s = SaveSerializer.Deserialize(json, Cfg, out var notes);
        Assert.That(s.SchemaVersion, Is.EqualTo(GameState.CurrentSchema));
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
        Assert.That(notes.First(), Does.Contain("金属锅#1"));
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
        Assert.That(notes.First(), Does.Contain("旧烹饪已取消").And.Contain("生块根×2"));
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
        bogus["SchemaVersion"] = "13";   // B6 起 12 是当前版本
        Assert.Throws<SaveVersionException>(() => SaveSerializer.Deserialize(bogus.ToString(), Cfg));
    }

    [Test]
    public void Idle_MigratedFields()
    {
        var json = Fixture("idle");
        var old = JObject.Parse(json);
        var s = SaveSerializer.Deserialize(json, Cfg);
        Assert.That(s.SchemaVersion, Is.EqualTo(GameState.CurrentSchema), "0.2 → 3 → 4 → 5 链式升级");
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
        Assert.That(s.Rng.State, Is.Not.EqualTo(0u), "随机状态沿用存档（4→5 迁移只在其上为旧装备抽一次最大耐久）");
        Assert.That(SaveSerializer.Deserialize(Fixture("idle"), Cfg).Rng.State, Is.EqualTo(s.Rng.State), "同一旧档迁移结果确定，不另起随机");

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
        Assert.That(s.Log.Last().Text, Does.Contain($"升级到版本{GameState.CurrentSchema}"));
        AssertIdempotent(s);
        Assert.That(E.DoInstant(s, InstantRequest.FillPot(First(s, "dirty_water", "camp").InstanceId, pot.InstanceId, 2)).Ok, Is.True, "可按新方式装锅");
    }
}
