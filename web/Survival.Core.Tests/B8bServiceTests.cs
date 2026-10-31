using Newtonsoft.Json.Linq;
using Survival.Core.Config;
using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 B8b M200（R105）：策划答复 Q88/Q89/Q94——服务地点拆成营区公共居所与收容所两个配置位（v3 地图前都在替身 camp），
/// 地图导入后幂等迁址（公共候补遗物迁 h02_square，不迁普通地面物/当地同行遗物；在办 RB01 按开始地点完成、不双发），
/// 报失补发新矛落实际办理地点地面。出处 R08/A11/S01/S14「Q88/Q89/Q92—Q94」本轮章、assets/20261031/party/party-adjudications.json；
/// 验收 PTY-Q88/PTY-Q89/PTY-Q94。v3 地图节点只在测试里临时加进配置（游戏数据里没有，B11 才导入）。
/// </summary>
public class B8bServiceTests : TestBase
{
    /// <summary>测试用配置：在现行地图上临时加 h02_square / h06_shelter 两个节点（复制农野荒地的数据，只改 ID 与名字）。</summary>
    private static GameConfig V3Cfg()
    {
        var j = JObject.Parse(File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "config.v0.6.json")));
        var locs = (JArray)j["locations"]!;
        var field = (JObject)locs.First(l => (string)l["id"]! == "field");
        foreach (var (id, name) in new[] { ("h02_square", "营区广场"), ("h06_shelter", "收容所") })
        {
            var c = (JObject)field.DeepClone();
            c["id"] = id;
            c["name"] = name;
            c["initiallyVisited"] = false;
            c["initiallyDiscovered"] = false;
            locs.Add(c);
        }
        return ConfigLoader.FromJson(j.ToString());
    }

    private static GameState Prep(GameEngine e, GameState s, int c = 4)
    {
        if (c >= 2) s.Progress.Flags.Add("hub_registered");
        if (c >= 3) s.Progress.Flags.Add("church_route_record");
        if (c >= 4) s.Progress.Flags.Add("academy_safety_record");
        s.SpawnNextCheck = int.MaxValue;
        s.PatrolNextCheck = int.MaxValue;
        s.GameMinute = At(1, 10, 0);
        return s;
    }

    private GameState Camp(int c = 4) => Prep(E, New(), c);

    /// <summary>A12 partyAdjudications（= assets/20261031/party/party-adjudications.json）。</summary>
    private static JObject Party() =>
        (JObject)JObject.Parse(File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "design.a12.json")))["partyAdjudications"]!;

    /// <summary>PTY-Q89：地图前营区与收容所两个配置位都映射 camp；没有 v3 节点时迁址入口什么也不做（不造节点、不改标记）。</summary>
    [Test]
    public void PTY_Q89_PreV3_BothServicesAtCamp_NoMigrationWithoutV3Nodes()
    {
        var sl = (JObject)Party()["serviceLocations"]!;
        Assert.That((string)sl["preV3"]!, Is.EqualTo("camp"));
        Assert.That((string)sl["rb01AndLoanLossV3"]!, Is.EqualTo(Cfg.Party.ShelterV3));
        Assert.That((string)sl["reserveHousingAndRosterV3"]!, Is.EqualTo(Cfg.Party.HubV3));
        Assert.That(Cfg.Party.HubStandIn, Is.EqualTo("camp"));
        Assert.That(Cfg.Party.ShelterStandIn, Is.EqualTo("camp"));

        var s = Camp();
        Assert.That(E.ServiceMapV3Available, Is.False, "现行地图没有 h02/h06");
        Assert.That(s.ServiceMapMigrated, Is.False);
        Assert.That(E.HubLocation(s), Is.EqualTo("camp"));
        Assert.That(E.ShelterLocation(s), Is.EqualTo("camp"));
        Assert.That(E.MigrateServiceLocations(s), Is.False);
        Assert.That(s.ServiceMapMigrated, Is.False);
        Assert.That(E.EscortRegistrationBlocked(s), Is.Null, "camp 代办 RB01");
        E.DebugTeleport(s, "field");
        Assert.That(E.EscortRegistrationBlocked(s), Does.Contain("只能在临时营地（暂代收容所）安全空闲时登记补员"));
        E.DebugRecruit(s, "npc_lin_zhao");
        Assert.That(E.SetActiveBlocked(s, "npc_lin_zhao", false), Does.Contain("只能在临时营地（营区）安全空闲时留作候补"));
    }

    /// <summary>
    /// PTY-Q88/Q89：地图导入后服务分流——换入/留守在 h02_square，RB01/报失在 h06_shelter；camp 不再提供这些入口（不双领取）；
    /// 公共候补遗物迁 h02（容器同 ID、内容物实例/owner/耐久不变），同行者在 camp 死亡的遗物、camp 普通地面物不迁；再调一次不再迁（幂等）。
    /// </summary>
    [Test]
    public void PTY_Q88_Q89_Migration_SplitsServices_MovesOnlyPublicReserveRemains_Idempotent()
    {
        var e = new GameEngine(V3Cfg());
        var s = e.NewGame(12345);
        e.DebugLegacyStory(s);
        s.ServiceMapMigrated = false;   // 模拟“地图导入前开的局”
        Prep(e, s);
        Assert.That(e.ServiceMapV3Available, Is.True);

        // 候补顾承死在营区（替身 camp）→ 公共候补遗物；同行林昭在 camp 死亡 → 当地遗物；camp 地面另有一件普通物资
        e.DebugRecruit(s, "npc_lin_zhao");
        e.DebugRecruit(s, "npc_gu_cheng");
        e.SetActive(s, "npc_gu_cheng", false);
        Assert.That(e.IsReserve(s, "npc_gu_cheng"), Is.True);
        var guItems = s.Items.Where(i => i.Owner.Contains("npc_gu_cheng")).Select(i => (i.InstanceId, i.Durability)).OrderBy(x => x.InstanceId).ToList();
        e.DebugActor(s, "npc_gu_cheng", "kill");
        e.DebugActor(s, "npc_lin_zhao", "kill");
        Assert.That(s.Remains["npc_gu_cheng"].PublicReserve, Is.True);
        Assert.That(s.Remains["npc_lin_zhao"].PublicReserve, Is.False);
        var ground = e.CreateItems(s, "wood", 1, "camp").Single();

        Assert.That(e.MigrateServiceLocations(s), Is.True);
        Assert.That(s.ServiceMapMigrated, Is.True);
        Assert.That(e.HubLocation(s), Is.EqualTo("h02_square"));
        Assert.That(e.ShelterLocation(s), Is.EqualTo("h06_shelter"));
        Assert.That(s.Remains["npc_gu_cheng"].LocationId, Is.EqualTo("h02_square"), "公共候补遗物随营区迁");
        Assert.That(s.Remains["npc_lin_zhao"].LocationId, Is.EqualTo("camp"), "同行者在 camp 死亡的当地遗物不迁");
        Assert.That(ground.Owner, Is.EqualTo("camp"), "camp 普通地面物不迁");
        Assert.That(e.RemainsItems(s, "npc_gu_cheng").Select(i => (i.InstanceId, i.Durability)).OrderBy(x => x.InstanceId), Is.EqualTo(guItems), "内容物实例、耐久不变，不复制");
        Assert.That(e.RemainsItems(s, "npc_gu_cheng").All(i => i.Owner == ItemInstance.RemainsOwner("npc_gu_cheng")), Is.True, "owner 不变");
        Assert.That(s.Log.Count(l => l.Text.Contains("营区服务迁到")), Is.EqualTo(1));

        // 幂等：再调不再迁、不再写日志
        s.Remains["npc_lin_zhao"].PublicReserve = true;   // 即使之后有标记也不会第二次迁
        Assert.That(e.MigrateServiceLocations(s), Is.False);
        Assert.That(s.Remains["npc_lin_zhao"].LocationId, Is.EqualTo("camp"));
        Assert.That(s.Log.Count(l => l.Text.Contains("营区服务迁到")), Is.EqualTo(1));
        s.Remains["npc_lin_zhao"].PublicReserve = false;

        // camp 不再提供服务入口；h02 办换入/留守，h06 办 RB01，号位编辑仍任意空闲地点
        Assert.That(e.EscortRegistrationBlocked(s), Does.Contain("只能在收容所（收容所）安全空闲时登记补员"));
        e.DebugRecruit(s, "npc_tang_li");
        Assert.That(e.SetActiveBlocked(s, "npc_tang_li", false), Does.Contain("只能在营区广场（营区）安全空闲时留作候补"));
        Assert.That(e.FormationEditBlocked(s), Is.Null, "当前同行号位编辑不限地点（Q86/Q93）");
        e.DebugTeleport(s, "h02_square");
        Assert.That(e.SetActiveBlocked(s, "npc_tang_li", false), Is.Null);
        Assert.That(e.EscortRegistrationBlocked(s), Does.Contain("收容所"));
        e.DebugTeleport(s, "h06_shelter");
        Assert.That(e.SetActiveBlocked(s, "npc_tang_li", false), Does.Contain("营区"));

        // 迁移后候补死亡的遗物落在 h02
        e.DebugTeleport(s, "h02_square");
        e.SetActive(s, "npc_tang_li", false);
        e.DebugActor(s, "npc_tang_li", "kill");
        Assert.That(s.Remains["npc_tang_li"].LocationId, Is.EqualTo("h02_square"));
    }

    /// <summary>PTY-Q89：在 camp 已开始的 RB01 登记，迁址后按原事务快照在 camp 完成（只建一人），不要求瞬移、不在 h06 另开一笔；之后新请求走 h06。</summary>
    [Test]
    public void PTY_Q89_InProgressRb01_CompletesAtOriginalSite_NoDoubleIssue()
    {
        var e = new GameEngine(V3Cfg());
        var s = e.NewGame(12345);
        e.DebugLegacyStory(s);
        s.ServiceMapMigrated = false;
        Prep(e, s, 2);
        Assert.That(e.StartEscortRegistration(s, "rb-1").Ok, Is.True);
        Assert.That(e.MigrateServiceLocations(s), Is.True, "登记进行中导入地图");
        Assert.That(e.StartEscortRegistration(s, "rb-2").Ok, Is.False, "在办期间不能再开第二笔");
        for (var i = 0; i < 200 && s.Main != null; i++) e.Advance(s);
        Assert.That(e.AliveEscorts(s).Count, Is.EqualTo(1), "按 camp 的原快照完成，只建一人");
        Assert.That(s.Log.Any(l => l.Text.Contains("登记完成")), Is.True);
        Assert.That(s.Items.Count(i => i.LoanSlot == "loan_1"), Is.EqualTo(1), "借用矛只发一件");
        Assert.That(e.StartEscortRegistration(s, "rb-1").Duplicate, Is.True, "requestId 重放不双发");
        Assert.That(e.EscortRegistrationBlocked(s), Does.Contain("收容所"), "新请求走新址");
    }

    /// <summary>
    /// PTY-Q94：报失补发的新矛固定白色无词条满耐久 30，落实际办理地点地面（迁址前 camp、迁址后 h06_shelter），不进任何人五卡/武器槽、不自动装备；
    /// 旧实例撤销与 loan_slot 改指同一事务；requestId 重试不双发；不花时间。
    /// </summary>
    [Test]
    public void PTY_Q94_ReplacementSpear_OnServiceGround_PreAndPostMigration()
    {
        var pa = (JObject)Party()["loanReplacement"]!;
        Assert.That((string)pa["item"]!, Is.EqualTo(Cfg.Party.EscortTemplate.LoanWeapon));
        Assert.That((int)pa["durability"]!, Is.EqualTo(30));
        Assert.That((bool)pa["autoEquip"]!, Is.False);
        Assert.That((bool)pa["autoPutInBag"]!, Is.False);

        foreach (var migrated in new[] { false, true })
        {
            var e = new GameEngine(V3Cfg());
            var s = e.NewGame(12345);
            e.DebugLegacyStory(s);
            s.ServiceMapMigrated = false;
            Prep(e, s, 2);
            e.DebugAddEscort(s);
            var esc = e.AliveEscorts(s).Single();
            var old = s.Items.Single(i => i.LoanSlot == "loan_1");
            old.Durability = 0;
            e.DebugActor(s, esc.ActorId, "kill");
            if (migrated) { e.MigrateServiceLocations(s); e.DebugTeleport(s, "h06_shelter"); }
            var site = migrated ? "h06_shelter" : "camp";
            Assert.That(e.ShelterLocation(s), Is.EqualTo(site));
            var minute = s.GameMinute;
            var carriedBefore = s.Items.Count(i => i.IsCarried || i.Owner.StartsWith("equip:"));
            Assert.That(e.ReportLostLoan(s, "loan_1", "lost-1").Ok, Is.True, $"migrated={migrated}");
            Assert.That(e.ReportLostLoan(s, "loan_1", "lost-1").Duplicate, Is.True, "重试不双发");
            Assert.That(e.FindItem(s, old.InstanceId), Is.Null, "旧实例撤销");
            var fresh = s.Items.Single(i => i.LoanSlot == "loan_1");
            Assert.That(s.LoanSlots["loan_1"], Is.EqualTo(fresh.InstanceId), "loan_slot 改指新实例");
            Assert.That(fresh.Owner, Is.EqualTo(site), "落实际办理地点地面");
            Assert.That(fresh.ItemId, Is.EqualTo("wooden_spear"));
            Assert.That(fresh.Quality, Is.EqualTo("white"));
            Assert.That(fresh.Affixes, Is.Null.Or.Empty, "无词条");
            Assert.That(fresh.Durability, Is.EqualTo(30));
            Assert.That(fresh.MaxDurability, Is.EqualTo(30));
            Assert.That(s.Items.Count(i => i.IsCarried || i.Owner.StartsWith("equip:")), Is.EqualTo(carriedBefore), "不进任何人的五卡/武器槽");
            Assert.That(s.GameMinute, Is.EqualTo(minute), "不花时间");
            Assert.That(s.Log.Last().Text, Does.Contain($"放在{e.LocName(site)}地面"));
        }
    }

    /// <summary>存档 16→17：服务迁址标记为 false；遗物来源能判断的写死（战斗中/不在替身地点→同行），替身地点非战斗死亡的记 null（不迁）；只迁一次。</summary>
    [Test]
    public void Save16To17_ServiceFlag_RemainsOrigin()
    {
        var s = Camp();
        E.DebugRecruit(s, "npc_lin_zhao");
        E.DebugActor(s, "npc_lin_zhao", "kill");
        E.DebugRecruit(s, "npc_tang_li");
        E.DebugTeleport(s, "field");
        E.DebugActor(s, "npc_tang_li", "kill");
        var j = JObject.Parse(SaveSerializer.Serialize(s));
        j["SchemaVersion"] = "16";
        j.Remove("ServiceMapMigrated");
        foreach (var r in ((JObject)j["Remains"]!).Properties()) ((JObject)r.Value).Remove("PublicReserve");
        var m = SaveSerializer.Deserialize(j.ToString(), Cfg, out _);
        Assert.That(m.SchemaVersion, Is.EqualTo(GameState.CurrentSchema));
        Assert.That(m.ServiceMapMigrated, Is.False);
        Assert.That(m.Remains["npc_lin_zhao"].PublicReserve, Is.Null, "camp 非战斗死亡：旧档分不清候补还是同行，不迁");
        Assert.That(m.Remains["npc_tang_li"].PublicReserve, Is.False, "死在 camp 以外的是同行者");
        Assert.That(m.Log.Any(l => l.Text.Contains("存档已升级到版本17（服务地点）")), Is.True);
        var again = SaveSerializer.Deserialize(SaveSerializer.Serialize(m), Cfg, out var notes2);
        Assert.That(notes2, Is.Empty);
        Assert.That(again.Remains["npc_lin_zhao"].PublicReserve, Is.Null);
    }

    /// <summary>Q89：地图已有 v3 节点时新档直接用新址服务（不保留替身入口）。</summary>
    [Test]
    public void PTY_Q89_NewGameOnV3Map_UsesNewSitesDirectly()
    {
        var e = new GameEngine(V3Cfg());
        var s = e.NewGame(7);
        Assert.That(s.ServiceMapMigrated, Is.True);
        Assert.That(e.HubLocation(s), Is.EqualTo("h02_square"));
        Assert.That(e.MigrateServiceLocations(s), Is.False, "新档无需再迁");
    }
}
