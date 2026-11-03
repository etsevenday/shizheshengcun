using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 批次 B14b：策划提交 ff71cb1 答复 Q141—Q146（Q141 选 B 公共工坊站、Q142 选 A 换药自带材料、Q143 选 B 队友正式背景、
/// Q144 选 A 登记不发物、Q145 选 B 正式名称、Q146 选 B 未实装事项隐藏）。规则出处：A11/S08/S09/A07/A08/N01/S14 本轮章、
/// assets/20261102/city/city-adjudications.json；验收 CITY-Q141-01—05、CITY-Q142-01、CITY-Q143-01、CITY-Q144-01、CITY-Q145-01、CITY-Q146-01/02。
/// </summary>
public class B14bCityQTests : TestBase
{
    private static JObject Design(string name) => JObject.Parse(File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, name)));

    private static readonly string[] Hidden =
    {
        // B15 起主线与城市支线的 7 项任务类服务已实装，B18 起鉴定所 appraisal_white_v1 已实装，B23 起 copy_public_knowledge 已实装：九项都已开放
    };
    /// <summary>B14 快照（Q146 的 b14UnimplementedSnapshot，只是版本快照，不是永久隐藏表）。</summary>
    private static readonly string[] B14Snapshot =
    {
        "hub_affairs_record", "copy_public_knowledge", "appraisal_white_v1", "church_reception_existing", "church_public_mediation",
        "church_archive_copy", "academy_reception_existing", "academy_noninvasive_observation", "academy_report_compare",
    };

    /// <summary>玩家文字里的内部任务编号（FA04、CH02、AC01、QMAP01、EXQ05、RN01 等）。</summary>
    private static readonly Regex InternalId = new Regex(@"(QMAP|FA|CH|AC|EXQ|RN|HU)\d{2}");

    private GameState At(string loc)
    {
        var s = New();
        E.DebugMapTeleport(s, loc);
        return s;
    }

    private void Ok(ActionResult r) => Assert.That(r.Ok, Is.True, r.Reason);

    private static ActionRequest Craft(string id, int batch = 1) => ActionRequest.Craft(id, null, null, batch);

    /// <summary>可做全部配方（学会全部知识、制造10级）的角色，放到某地点（按实际到达处理）。</summary>
    private GameState Crafter(string loc, params string[] facts)
    {
        var s = At(loc);
        E.DebugLearnAllKnowledge(s);
        s.Skills["crafting"].Level = 10;
        s.Progress.Flags.UnionWith(facts);
        return s;
    }

    private string? StationWhy(GameState s, string recipeId) => E.StationBlocked(s, Cfg.Recipes.Single(r => r.Id == recipeId));

    // ───────────── Q141 公共工坊站 ─────────────

    /// <summary>数据：6 个公共站逐条对照 city-adjudications.json；可信旧 QMAP02 映射到 B13b 同一事实键；0 额外使用费。</summary>
    [Category("DesignSync")]
    [Test]
    public void CITY_Q141_00_PublicStationsFromDesign()
    {
        var st = (JArray)Design("design.city-adjudications.json")["publicStations"]!["stations"]!;
        Assert.That(Cfg.City.PublicStations, Has.Count.EqualTo(st.Count));
        foreach (var x in st)
        {
            var c = Cfg.City.PublicStations.Single(p => p.Id == (string)x["id"]!);
            Assert.That((c.LocationId, c.StationId, c.Name), Is.EqualTo(((string)x["locationId"]!, (string)x["stationId"]!, (string)x["displayName"]!)));
            Assert.That(c.AllFacts, Is.EqualTo(x["allFacts"]!.Select(f => (string)f!)));
            var any = x["anyQualification"]!.Type == JTokenType.Null ? new List<string>() : x["anyQualification"]!.Select(f => (string)f!).ToList();
            Assert.That(c.AnyFacts, Is.EqualTo(any.Select(f => f == "legacy_QMAP02_completed" ? "trusted_legacy_QMAP02_completed" : f)));
            Assert.That((int)x["accessFee"]!["worldMinutes"]! + (int)x["accessFee"]!["xp"]! + (int)x["accessFee"]!["currency"]!, Is.EqualTo(0));
        }
    }

    /// <summary>CITY-Q141-01：登记后实到 h03 工作台可用；锻炉/军械台要 FA02 记录资格或可信旧 QMAP02 完成；任务接取、公开路线、手里有枪都不算。</summary>
    [Test]
    public void CITY_Q141_01_H03_WorkbenchAfterRegistration_ForgeArmoryNeedQualification()
    {
        var s = Crafter("h03_workshop");
        Assert.That(StationWhy(s, "make_machete"), Does.Contain("营区工坊工作台还没开放").And.Contain("完成营区登记"), "未登记不开");
        s.Progress.Flags.Add("hub_registered");
        Assert.That(StationWhy(s, "make_machete"), Is.Null, "登记后工作台可用");
        Assert.That(StationWhy(s, "make_steel"), Does.Contain("营区工坊锻炉还没开放").And.Contain("圣所旧路档册"));
        Assert.That(StationWhy(s, "make_gun_service_kit"), Does.Contain("营区工坊军械台还没开放"));
        // 接取任务、领公开路线、手里有枪都不冒充完成
        s.Progress.Flags.UnionWith(new[] { GameEngine.QuestFact("QMAP02", "accepted"), GameEngine.QuestFact("FA02", "accepted"), "FA02_route_briefed" });
        E.DebugGive(s, "pistol", 1);
        Assert.That(StationWhy(s, "make_steel"), Is.Not.Null);
        Assert.That(StationWhy(s, "make_gun_service_kit"), Is.Not.Null);
        s.Progress.Flags.Add("FA02_record_credit");
        Assert.That(StationWhy(s, "make_steel"), Is.Null);
        Assert.That(StationWhy(s, "make_gun_service_kit"), Is.Null);

        var t = Crafter("h03_workshop", "hub_registered", "trusted_legacy_QMAP02_completed");
        Assert.That(StationWhy(t, "make_steel"), Is.Null, "可信旧 QMAP02 完成也可");
        Assert.That(StationWhy(t, "make_repeater_crossbow"), Is.Null);

        var views = E.PublicStationsHere(s);
        Assert.That(views.Select(v => v.StationId), Is.EquivalentTo(new[] { "workbench", "forge", "armory" }), "h03 只有三站");
        Assert.That(views.All(v => v.Blocked == null), Is.True);
    }

    /// <summary>CITY-Q141-02：实到 r07 且 academy_contacted 可用工作台；不要求检测/宣誓/通道证；没有高级站；打开菜单不写通道事实、0 分钟。</summary>
    [Test]
    public void CITY_Q141_02_R07_WorkbenchOnly_NoPassWritten()
    {
        var s = Crafter("r07_workshop", "academy_contacted");
        Assert.That(s.Progress.Flags, Does.Not.Contain("academy_observation_pass"));
        Assert.That(StationWhy(s, "make_machete"), Is.Null);
        foreach (var r in new[] { "make_steel", "make_gun_service_kit", "make_leather_armor", "make_aid_badge" })
            Assert.That(StationWhy(s, r), Is.Not.Null, r);
        Assert.That(E.PublicStationsHere(s).Select(v => v.StationId), Is.EqualTo(new[] { "workbench" }));
        var minute = s.GameMinute;
        var flags = s.Progress.Flags.Count;
        var v = E.CityServicePreview(s, "academy_workshop_access")!;
        Assert.That(v.EffectText, Does.Contain("已开放：试制工坊工作台"));
        Ok(E.StartCityService(s, "academy_workshop_access"));
        Assert.That(s.GameMinute, Is.EqualTo(minute), "0 分钟");
        Assert.That(s.Progress.Flags.Count, Is.EqualTo(flags), "打开菜单不写事实");
        Assert.That(s.Progress.Flags, Does.Not.Contain("academy_observation_pass"));

        s.Progress.Flags.Remove("academy_contacted");
        Assert.That(StationWhy(s, "make_machete"), Does.Contain("试制工坊工作台还没开放"));
    }

    /// <summary>CITY-Q141-03：登记后实到 h02 缝纫台、h04 医疗台可用；h03/r07 不远程提供这两站；其余地点不生成公共站。</summary>
    [Test]
    public void CITY_Q141_03_SquareSewing_InfirmaryMedical_NotElsewhere()
    {
        var s = Crafter("h02_square", "hub_registered");
        Assert.That(StationWhy(s, "make_leather_armor"), Is.Null);
        Assert.That(StationWhy(s, "make_machete"), Is.Not.Null, "广场没有工作台");
        E.DebugMapTeleport(s, "h04_infirmary");
        Assert.That(StationWhy(s, "make_aid_badge"), Is.Null);
        Assert.That(StationWhy(s, "make_leather_armor"), Is.Not.Null);
        E.DebugMapTeleport(s, "h03_workshop");
        Assert.That(StationWhy(s, "make_leather_armor"), Is.Not.Null, "h03 不提供缝纫台");
        Assert.That(StationWhy(s, "make_aid_badge"), Is.Not.Null, "h03 不提供医疗台");
        Assert.That(Cfg.City.PublicStations.Select(p => p.LocationId).Distinct(), Is.EquivalentTo(new[] { "h02_square", "h03_workshop", "h04_infirmary", "r07_workshop" }));
        foreach (var loc in new[] { "h01_gate", "h05_appraisal", "c02_church", "r02_academy" })
        {
            E.DebugMapTeleport(s, loc);
            Assert.That(E.PublicStationsHere(s), Is.Empty, loc);
        }
    }

    /// <summary>
    /// CITY-Q141-04：公共站菜单 0 分钟 0 费用 0 经验；锻炉 N 份照扣 N 木炭燃料与原料；公共军械台不要求自建蓝图，但所做装备的配方知识仍要。
    /// </summary>
    [Test]
    public void CITY_Q141_04_NoAccessFee_ForgeFuelStillCharged_ArmoryNoStationBlueprint()
    {
        var s = Crafter("h03_workshop", "hub_registered", "FA02_record_credit");
        s.Items.RemoveAll(i => i.ItemId is "charcoal" or "iron_ore" or "steel");
        var (minute, xp, items) = (s.GameMinute, s.Skills.Sum(k => k.Value.Xp), s.Items.Sum(i => i.Count));
        Ok(E.StartCityService(s, "hub_workshop_access"));
        Assert.That((s.GameMinute, s.Skills.Sum(k => k.Value.Xp), s.Items.Sum(i => i.Count)), Is.EqualTo((minute, xp, items)), "开菜单 0 分钟 0 费用 0 经验");

        E.CreateItems(s, "iron_ore", 6, "h03_workshop");
        E.CreateItems(s, "charcoal", 4, "h03_workshop");
        Start(s, Craft("make_steel", 2));
        Run(s);
        Assert.That(Count(s, "charcoal") + Count(s, "iron_ore"), Is.EqualTo(0), "原料炭2＋燃料炭2、矿6全扣");
        Assert.That(Count(s, "steel"), Is.EqualTo(4));
        Assert.That(s.Locations["h03_workshop"].Facility.Workshops, Is.Empty, "公共站不是私人建造，不占私人限额");

        // 公共军械台：没有自建军械台蓝图也能用；连弩仍需自己的蓝图知识
        var t = At("h03_workshop");
        t.Skills["crafting"].Level = 10;
        t.Progress.Flags.UnionWith(new[] { "hub_registered", "FA02_record_credit" });
        Assert.That(E.KnowsKnowledge(t, "blueprint_armory_station"), Is.False);
        var crossbow = Cfg.Recipes.Single(r => r.Id == "make_repeater_crossbow");
        Assert.That(E.StationBlocked(t, crossbow), Is.Null);
        Assert.That(E.RecipeLocks(t, crossbow), Has.Some.Contains("未掌握"), "装备知识门槛仍在");
        Assert.That(E.RecipeLocks(t, Cfg.Recipes.Single(r => r.Id == "make_gun_service_kit")), Is.Empty);
    }

    /// <summary>CITY-Q141-05：异地不能远程用（营地没有自建时仍缺站；只到过 h03 但人在别处不开）；材料只取随身与当地地面；战斗/忙碌照原规则拒绝。</summary>
    [Test]
    public void CITY_Q141_05_NoRemote_LocalMaterialsOnly()
    {
        var s = Crafter("h03_workshop", "hub_registered");
        E.DebugMapTeleport(s, "h02_square");
        Assert.That(StationWhy(s, "make_machete"), Is.Not.Null, "人不在 h03 不能用 h03 工作台");
        E.DebugMapTeleport(s, "camp");
        Assert.That(StationWhy(s, "make_machete"), Does.Contain("还没有建成"), "营地仍按私人自建");
        s.CurrentLocation = "r07_workshop";   // 没有实际到达
        s.Progress.Flags.Add("academy_contacted");
        Assert.That(StationWhy(s, "make_machete"), Does.Contain("须实际到达"));

        var t = Crafter("h03_workshop", "hub_registered");
        t.Items.RemoveAll(i => i.ItemId is "iron_plate" or "wood" or "cloth");
        E.CreateItems(t, "iron_plate", 2, "h02_square");   // 别处地面的材料不算
        E.CreateItems(t, "wood", 1, "h03_workshop");
        E.DebugGive(t, "cloth", 1);
        Assert.That(E.CanStart(t, Craft("make_machete")), Does.Contain("缺少"));
        E.CreateItems(t, "iron_plate", 2, "h03_workshop");
        Assert.That(E.CanStart(t, Craft("make_machete")), Is.Null);
        Start(t, Craft("make_machete"));
        Assert.That(E.CanStart(t, Craft("make_machete")), Is.Not.Null, "忙碌不能再开工");
        Assert.That(E.CityServicePreview(t, "hub_workshop_access")!.Blocked, Is.EqualTo("行动进行中"));
    }

    // ───────────── Q148—Q150 公共站与施济显示定稿 ─────────────

    /// <summary>CITY-Q148-01：无资格时 h03 锻炉/军械台只提示档册条件；未登记仍显示登记条件；可信旧 QMAP02 加登记可开放，界面不出现旧档资格句。</summary>
    [Test]
    public void CITY_Q148_01_ForgeArmoryShowOnlyRecordCondition()
    {
        const string need = "需要：在教会地下圣所核心领取圣所旧路档册";
        var s = Crafter("h03_workshop");
        Assert.That(E.PublicStationsHere(s).Single(v => v.StationId == "forge").Blocked, Is.EqualTo("需要：完成营区登记（在营门办理登记后走到营区广场）"), "未登记先写登记");
        s.Progress.Flags.Add("hub_registered");
        foreach (var st in new[] { "forge", "armory" })
            Assert.That(E.PublicStationsHere(s).Single(v => v.StationId == st).Blocked, Is.EqualTo(need), st);
        Assert.That(StationWhy(s, "make_steel"), Does.EndWith($"（{need}）"));
        var text = E.CityServicePreview(s, "hub_workshop_access")!.EffectText;
        Assert.That(text, Does.Contain(need));
        foreach (var t0 in new[] { text, StationWhy(s, "make_steel")!, StationWhy(s, "make_gun_service_kit")! })
            Assert.That(t0, Does.Not.Contain("旧存档").And.Not.Contain("工业支援").And.Not.Contain("QMAP"));

        var t = Crafter("h03_workshop", "hub_registered", "trusted_legacy_QMAP02_completed");
        Assert.That(E.PublicStationsHere(t).All(v => v.Blocked == null), Is.True, "可信旧资格加登记直接开放，不要求重领档册");
        Assert.That(E.CityServicePreview(t, "hub_workshop_access")!.EffectText, Does.Not.Contain("旧存档").And.Contain("已开放：营区工坊工作台、营区工坊锻炉、营区工坊军械台"));
    }

    /// <summary>CITY-Q149-01：两个施济选项逐字用定稿名称；正好 40 不算低于 40；其他领取限制与发物照原规则。</summary>
    [Test]
    public void CITY_Q149_01_ReliefChoiceNamesAndThreshold()
    {
        var relief = Cfg.City.Services.Single(x => x.Id == "church_relief_ration");
        Assert.That(relief.Choices.Select(c => c.Label), Is.EqualTo(new[] { "熟块根1份（饱食低于40）", "净水1份（水分低于40）" }));
        var s = At("c04_alms");
        Assert.That(E.CityServicePreview(s, "church_relief_ration")!.Choices.Select(c => c.Label), Is.EqualTo(new[] { "熟块根1份（饱食低于40）", "净水1份（水分低于40）" }));
        SetStats(s, 15, 40, 40, 15, 80, 70);
        Assert.That(E.CityServicePreview(s, "church_relief_ration")!.Targets.Single().Blocked, Is.Not.Null, "正好40不算低于40");
        Assert.That(E.StartCityService(s, "church_relief_ration", GameState.PlayerId, "relief_food").Ok, Is.False);
        SetStats(s, 15, 39.99, 40, 15, 80, 70);
        Ok(E.StartCityService(s, "church_relief_ration", GameState.PlayerId, "relief_food"));
        Run(s);
        Assert.That(Count(s, "cooked_root", "c04_alms"), Is.EqualTo(1));
        ReadDialogues(s);
        Assert.That(E.CityServicePreview(s, "church_relief_ration")!.Blocked, Does.Contain("额度已用完"), "原每日额度不变");
    }

    /// <summary>
    /// CITY-Q150-01：条件不足的已实装公共站仍出卡并写真实原因，可浏览配方但不能制作；满足后可用；未实装事项仍隐藏；
    /// 刷新只读重算，不新增存档字段、不改进度。
    /// </summary>
    [Test]
    public void CITY_Q150_01_LockedStationsVisible_ReadOnlyRefresh()
    {
        var s = Crafter("h03_workshop");
        E.CreateItems(s, "iron_plate", 2, "h03_workshop");
        E.CreateItems(s, "wood", 1, "h03_workshop");
        E.CreateItems(s, "cloth", 1, "h03_workshop");
        var views = E.PublicStationsHere(s);
        Assert.That(views.Select(v => v.StationId), Is.EquivalentTo(new[] { "workbench", "forge", "armory" }), "未开放的也出卡");
        Assert.That(views.All(v => v.Blocked != null), Is.True);
        Assert.That(E.RecipeLocks(s, Cfg.Recipes.Single(r => r.Id == "make_machete")), Has.Some.Contains("营区工坊工作台还没开放"), "可浏览配方与门槛");
        Assert.That(E.CanStart(s, Craft("make_machete")), Does.Contain("还没开放"), "条件不足不能制作");
        var json = SaveSerializer.Serialize(s);
        E.PublicStationsHere(s);
        E.CityServicePreview(s, "hub_workshop_access");
        Assert.That(SaveSerializer.Serialize(s), Is.EqualTo(json), "查看与刷新只读");
        s.Progress.Flags.Add("hub_registered");
        Assert.That(E.PublicStationsHere(s).Single(v => v.StationId == "workbench").Blocked, Is.Null);
        Assert.That(E.CanStart(s, Craft("make_machete")), Is.Null, "满足后按原规则可用");
        Assert.That(E.CityServicesOf(s, "npc_luo_zheng").Select(v => v.Id), Is.EqualTo(new[] { "hub_workshop_access", "copy_public_knowledge", GameEngine.TrainingServiceId }), "B23 起查抄公开资料已实装（B15：h03 另有战术训练）");
    }

    // ───────────── Q142 换药 / Q144 登记 ─────────────

    /// <summary>CITY-Q142-01：换药玩家自带绷带1+布1，缺任一拒绝；别处地面的布不补；没有机构库存。</summary>
    [Test]
    public void CITY_Q142_01_DressingPlayerSuppliesBoth()
    {
        var s = At("h04_infirmary");
        SetStats(s, 12, 80, 80, 15, 80, 70);
        s.Items.RemoveAll(i => i.ItemId is "bandage" or "cloth");
        E.DebugGive(s, "bandage", 1);
        E.CreateItems(s, "cloth", 3, "h02_square");
        var v = E.CityServicePreview(s, "clinic_dressing")!;
        Assert.That(v.Blocked, Does.Contain("材料不足").And.Contain("0/1"));
        Assert.That(v.CostText, Does.Contain("×1"));
        Assert.That(E.StartCityService(s, "clinic_dressing", GameState.PlayerId).Ok, Is.False);
        E.CreateItems(s, "cloth", 1, "h04_infirmary");
        Ok(E.StartCityService(s, "clinic_dressing", GameState.PlayerId));
        Run(s);
        Assert.That(Count(s, "bandage") + Count(s, "cloth", "h04_infirmary"), Is.EqualTo(0), "完成扣随身绷带与医务室地面的布");
        Assert.That(Count(s, "cloth", "h02_square"), Is.EqualTo(3), "别处的布不动");
    }

    /// <summary>CITY-Q144-01：营门授权不提前给 C2；真实走到广场得 C2 与 26 项默认知识补缺，物品增量 0、广场配给点库存不变；再到/读档不重发。</summary>
    [Test]
    public void CITY_Q144_01_RegistrationGivesNoItems()
    {
        var s = At("h01_gate");
        var know = Cfg.Knowledge.Where(k => k.GrantedByFacts.Contains("hub_registered")).ToList();
        Assert.That(know, Has.Count.EqualTo(26));
        Assert.That(know.Any(k => k.Id == "expedition_basics"), Is.False, "远行基础做法不是第 27 项");
        Ok(E.StartCityService(s, "hub_registration_existing"));
        ReadDialogues(s);
        Assert.That(E.BattleCapacity(s), Is.EqualTo(1));
        var items = s.Items.Sum(i => i.Count);
        var square = Cfg.Resources.Where(r => r.LocationId == "h02_square").Select(r => r.NodeId).ToList();
        // 库存/日补/领取账不变（首次实到时揭示资源点只改 Discovered，不算刷新）
        string Nodes(GameState g) => string.Join("|", square.Select(id =>
        {
            var j = JObject.FromObject(g.Resources[id]);
            j.Remove("Discovered");
            return j.ToString(Newtonsoft.Json.Formatting.None);
        }));
        var before = Nodes(s);
        Start(s, ActionRequest.Travel("h02_square"));
        Run(s);
        ReadDialogues(s);
        Assert.That(E.BattleCapacity(s), Is.EqualTo(2));
        Assert.That(know.All(k => E.KnowsKnowledge(s, k.Id)), Is.True);
        Assert.That(E.KnowsKnowledge(s, "expedition_basics"), Is.False);
        Assert.That(s.Items.Sum(i => i.Count), Is.EqualTo(items), "登记不发物品");
        Assert.That(Nodes(s), Is.EqualTo(before), "不刷新、不重置广场配给点");
        var back = SaveSerializer.Deserialize(SaveSerializer.Serialize(s), Cfg, out _);
        Start(back, ActionRequest.Travel("h01_gate"));
        Run(back);
        Start(back, ActionRequest.Travel("h02_square"));
        Run(back);
        Assert.That(back.Items.Sum(i => i.Count), Is.EqualTo(items), "读档后再到广场不重发");
    }

    // ───────────── Q143 队友背景 ─────────────

    /// <summary>CITY-Q143-01：六名固定队友背景逐字匹配表；名字、职能不变；背景从配置读、不进存档（旧档读档后显示同一新句），招募后名字与数值不受影响。</summary>
    [Category("DesignSync")]
    [Test]
    public void CITY_Q143_01_CompanionBackgrounds()
    {
        var bg = (JArray)Design("design.city-adjudications.json")["companionBackgrounds"]!;
        Assert.That(Cfg.Party.NamedCompanions, Has.Count.EqualTo(6));
        foreach (var x in bg)
        {
            var c = Cfg.Party.Named((string)x["actorId"]!)!;
            Assert.That(c.Name, Is.EqualTo((string)x["name"]!));
            Assert.That(c.Affiliation, Is.EqualTo((string)x["affiliation"]!));
            Assert.That(c.Background, Is.EqualTo((string)x["background"]!));
            Assert.That(c.Background, Does.Not.Contain("灯塔").And.Not.Contain("工务联合会"));
        }
        var s = New();
        s.Progress.Flags.Add("hub_registered");
        E.DebugRecruit(s, "npc_shen_he");
        var a = s.Actors["npc_shen_he"];
        var json = SaveSerializer.Serialize(s);
        Assert.That(json, Does.Not.Contain(Cfg.Party.Named("npc_shen_he")!.Background), "背景不固化进存档，显示时从配置读");
        Assert.That(json, Does.Not.Contain("灯塔转运护理员"));
        var back = SaveSerializer.Deserialize(json, Cfg, out _);
        var b = back.Actors["npc_shen_he"];
        Assert.That((E.ActorDisplayName(back, b.Id), b.Stats.Health, b.Stats.HealthMax, back.Skills.Count),
            Is.EqualTo((E.ActorDisplayName(s, a.Id), a.Stats.Health, a.Stats.HealthMax, s.Skills.Count)));
    }

    // ───────────── Q145 名称 ─────────────

    /// <summary>CITY-Q145-01：19 服务名/摘要、知识名、两份告示名符合表；ID/工时/费用不变；玩家可见文字没有内部任务编号。</summary>
    [Category("DesignSync")]
    [Test]
    public void CITY_Q145_01_FormalNames_NoInternalIds()
    {
        var d = Design("design.city-adjudications.json");
        foreach (var x in (JArray)d["serviceNames"]!)
        {
            var c = Cfg.City.Services.Single(y => y.Id == (string)x["serviceId"]!);
            Assert.That(c.Title, Is.EqualTo((string)x["displayName"]!));
            Assert.That(c.Summary, Is.EqualTo((string)x["playerSummary"]!));
            Assert.That(InternalId.IsMatch(c.Title + c.Summary), Is.False, c.Id);
        }
        Assert.That(Cfg.Knowledge.Single(k => k.Id == "expedition_basics").Name, Is.EqualTo("远行基础做法"));
        foreach (var x in (JArray)d["notices"]!)
            Assert.That(Cfg.City.Notices.Single(n => n.LocationId == (string)x["locationId"]! && n.Id != "h03_ceramic_notice").Title, Is.EqualTo((string)x["displayName"]!));
        Assert.That(Cfg.City.Services.Single(x => x.Id == "clinic_dressing").WorldMinutes, Is.EqualTo(10));

        // 每个岗位所在地：人物卡与事项面板的全部玩家文字
        var s = New();
        s.Progress.Flags.UnionWith(new[] { "hub_registered", "registration_authorized" });
        foreach (var n in Cfg.City.Npcs)
        {
            E.DebugMapTeleport(s, n.LocationId);
            ReadDialogues(s);
            var card = E.NpcsHere(s).Single(c => c.Kind == "post");
            Assert.That(InternalId.IsMatch(card.Status + card.QuestText + card.Role), Is.False, n.NpcId);
            Assert.That(card.QuestText, Does.Not.Contain("后续版本开放"), "B15：只写已实装委托的正式名与状态，不写占位");
            foreach (var v in E.CityServicesOf(s, n.NpcId))
            {
                var text = string.Join("|", new[] { v.Title, v.Summary, v.EffectText, v.CostText, v.TimeText, v.Blocked ?? "" }
                    .Concat(v.Targets.Select(t => t.Detail + t.Blocked)).Concat(v.Choices.Select(c => c.Label)));
                Assert.That(InternalId.IsMatch(text), Is.False, $"{v.Id}：{text}");
                Assert.That(text, Does.Not.Contain("后续版本开放"), v.Id);
            }
        }
        Assert.That(s.Log.Any(l => l.Text.Contains("营门登记已授权")), Is.False);
        E.DebugMapTeleport(s, "h04_infirmary");
        Ok(E.StartCityService(s, "clinic_consult", GameState.PlayerId));
        Assert.That(s.Log.Last().Text, Does.StartWith("伤病咨询"));
    }

    // ───────────── Q146 未实装隐藏 ─────────────

    /// <summary>CITY-Q146-01：九项未实装从事项列表与计数消失；全未实装岗位没有事项按钮（ServiceCount 0）、卡面不写事项数，仍可交谈；不因未实装播 locked。</summary>
    [Test]
    public void CITY_Q146_01_UnimplementedHidden_TalkStillWorks()
    {
        Assert.That(Cfg.City.Services.Where(x => !x.Implemented).Select(x => x.Id), Is.EquivalentTo(Hidden));
        var d = Design("design.city-adjudications.json");
        Assert.That(d["displayPolicy"]!["b14UnimplementedSnapshot"]!.Select(x => (string)x!), Is.EquivalentTo(B14Snapshot));

        var s = New();
        var noButton = new List<string>();
        foreach (var n in Cfg.City.Npcs)
        {
            E.DebugMapTeleport(s, n.LocationId);
            var card = E.NpcsHere(s).Single(c => c.Kind == "post");
            var list = E.CityServicesOf(s, n.NpcId);
            Assert.That(list.Select(v => v.Id).Intersect(Hidden), Is.Empty, n.NpcId);
            var impl = n.ServiceIds.Count(id => !Hidden.Contains(id)) + (n.NpcId == Cfg.Scope.Training.NpcId ? 1 : 0)   // B15：h03 战术训练挂在罗铮事项里
                       + Cfg.ExpansionQuests.Packs.Where(p => p.CopyNpcId == n.NpcId && !n.ServiceIds.Contains(p.CopyServiceId)).Select(p => p.CopyServiceId).Distinct().Count();   // B23：h02/h04 公开抄录
            Assert.That(card.ServiceCount, Is.EqualTo(impl), n.NpcId);
            if (impl == 0)
            {
                noButton.Add(n.Name);
                Assert.That(card.Status, Is.EqualTo("可交谈"), "没有事项数占位");
                Ok(E.TalkNpc(s, n.NpcId));
                ReadDialogues(s);
                Ok(E.TalkNpc(s, n.NpcId));
                ReadDialogues(s);
                Assert.That(s.Story.DialogueLog.Any(x => x.EventId == n.States["locked"]), Is.False, n.NpcId);
                Assert.That(s.Story.DialogueLog.Any(x => x.EventId == n.States["idle"]), Is.True, n.NpcId);
            }
            else Assert.That(card.Status, Does.Match($@"事项 \d+/{impl} 项可办"));
        }
        // B15：许安（整理调查记录）、祁闻/温澄（接待路线）、纪牧/宋烛（CH02）、季闻（AC01）、邵宁（AC02）已有实装事项；B18：陶岑（鉴定所）也有
        Assert.That(noButton, Is.Empty);
    }

    /// <summary>
    /// CITY-Q146-02：已实装但缺材料/额度用完的照常显示真实原因；可办数按当前资格；直接请求隐藏事项零扣费零效果；
    /// 实装后同 ID 自然出现、不重置配额（以配置开关模拟）。
    /// </summary>
    [Test]
    public void CITY_Q146_02_IneligibleVisible_StaleRequestRejected_EnableLater()
    {
        var s = At("h04_infirmary");
        s.Items.RemoveAll(i => i.ItemId is "bandage" or "cloth");
        SetStats(s, 12, 80, 80, 15, 80, 70);
        var dress = E.CityServicesOf(s, "npc_chen_zhi").Single(v => v.Id == "clinic_dressing");
        Assert.That(dress.Blocked, Does.StartWith("材料不足"), "缺材料仍显示并写原因");
        Assert.That(E.NpcsHere(s).Single().Status, Is.EqualTo("可交谈 · 事项 2/4 项可办"), "B23：另有查抄公开资料（未登记不可办）");

        // 直接请求隐藏事项：拒绝，状态完全不变（B23 起九项都已实装，以配置开关模拟隐藏）
        E.DebugMapTeleport(s, "h03_workshop");
        var json = SaveSerializer.Serialize(s);
        var hidden = Cfg.City.Services.Single(x => x.Id == "copy_public_knowledge");
        try
        {
            hidden.Implemented = false;
            var r = E.StartCityService(s, "copy_public_knowledge");
            Assert.That(r.Ok, Is.False);
            Assert.That(r.Reason, Does.Not.Contain("后续版本"));
            Assert.That(SaveSerializer.Serialize(s), Is.EqualTo(json), "零扣费零效果");
        }
        finally { hidden.Implemented = true; }

        // 施济额度用完：仍显示、写真实原因
        var t = At("c04_alms");
        SetStats(t, 15, 30, 80, 15, 80, 70);
        Ok(E.StartCityService(t, "church_relief_ration", GameState.PlayerId, "relief_food"));
        Run(t);
        ReadDialogues(t);
        var relief = E.CityServicesOf(t, "npc_bai_ling").Single();
        Assert.That(relief.Blocked, Does.Contain("救助配给额度已用完"));
        Assert.That(E.NpcsHere(t).Single().ServiceCount, Is.EqualTo(1));

        // 后续实装同 ID：自然出现在列表里（配额账不动）。B23 起抄录资料包已实装，以配置开关模拟“先隐藏后实装”
        var svc = Cfg.City.Services.Single(x => x.Id == "copy_public_knowledge");
        var npc = Cfg.City.Npcs.Single(n => n.ServiceIds.Contains(svc.Id));
        var quota = new Dictionary<string, int>(t.City.QuotaUsed);
        var before = E.CityServicesOf(t, npc.NpcId).Count;
        try
        {
            svc.Implemented = false;
            E.DebugMapTeleport(t, npc.LocationId);
            Assert.That(E.CityServicesOf(t, npc.NpcId).Select(v => v.Id), Does.Not.Contain("copy_public_knowledge"));
            svc.Implemented = true;
            Assert.That(E.CityServicesOf(t, npc.NpcId).Select(v => v.Id), Does.Contain("copy_public_knowledge"));
            Assert.That(E.NpcsHere(t).Single(c => c.NpcId == npc.NpcId).ServiceCount, Is.EqualTo(npc.ServiceIds.Count(id => Cfg.City.Services.Single(x => x.Id == id).Implemented) + (npc.NpcId == Cfg.Scope.Training.NpcId ? 1 : 0)));
            Assert.That(t.City.QuotaUsed, Is.EqualTo(quota));
        }
        finally { svc.Implemented = true; }
    }

    // ───────────── Q151 尹珂台词 ─────────────

    /// <summary>CITY-Q151-01/02 数据：观察记录服务台词与 NTL_0304 逐字对照 city-adjudications.json dialogueQ151，尹珂玩家文字没有内部任务编号（科学院支线未实装，NTL_0304 只验数据）。</summary>
    [Category("DesignSync")]
    [Test]
    public void CITY_Q151_00_YinKeLinesFromDesign()
    {
        var q = Design("design.city-adjudications.json")["dialogueQ151"]!;
        var npc = Cfg.City.Npcs.Single(n => n.NpcId == "npc_yin_ke");
        Assert.That(npc.ServiceOfferLines[(string)q["serviceId"]!], Is.EqualTo((string)q["serviceText"]!));
        Assert.That(Cfg.Story.Dialogues.Single(d => d.Id == (string)q["lineId"]!).Text, Is.EqualTo((string)q["questText"]!));
        foreach (var d in Cfg.Story.Dialogues.Where(d => d.Event.StartsWith("npc_yin_ke_")))
            Assert.That(InternalId.IsMatch(d.Text), Is.False, d.Id);
        Assert.That(npc.ServiceOfferLines.Values.Any(InternalId.IsMatch), Is.False);
    }

    /// <summary>CITY-Q151-01：r08 观察区事项面板显示新台词；观察记录仍 0 分钟只读、不收费，开始后状态只多一条提示。</summary>
    [Test]
    public void CITY_Q151_01_ObservationRecordPanel_RulesUnchanged()
    {
        var s = At("r08_observation");
        var v = E.CityServicesOf(s, "npc_yin_ke").Single(x => x.Id == "academy_observation_record");
        Assert.That(v.NpcLine, Is.EqualTo("这里只读你已经同意的范围和现有观察记录；不再办理第二次观察协作收费。"));
        Assert.That(v.EffectText, Does.Contain("0分钟").And.Not.Match(InternalId.ToString()));
        var minutes = s.GameMinute;
        var items = s.Items.Sum(i => i.Count);
        Ok(E.StartCityService(s, "academy_observation_record"));
        Assert.That((s.GameMinute, s.Items.Sum(i => i.Count)), Is.EqualTo((minutes, items)), "不耗时、不收费");
    }
}
