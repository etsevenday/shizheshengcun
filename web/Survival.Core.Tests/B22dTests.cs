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

    // ───────────── Q171 基础陶工做法 / Q172 camp 两区 / Q175 部署等待（核对现状） ─────────────

    /// <summary>PLAN-Q171-01：知识 ceramic_methods 正式名“基础陶工做法”；制造3 首读学会、0 分钟 0 物资 0 经验，再读幂等；d06_08 矿道作业手册同事务授予。</summary>
    [Test]
    public void PLAN_Q171_01_CeramicMethods_Name_FreeOnce()
    {
        var k = Cfg.Knowledge.Single(x => x.Id == "ceramic_methods");
        Assert.That(k.Name, Is.EqualTo("基础陶工做法"));
        Assert.That(k.GrantedByFacts, Is.EquivalentTo(new[] { "h03_ceramic_notice_read", "mining_blueprint_pack" }));
        var s = New();
        E.DebugMapTeleport(s, "h03_workshop");
        ReadDialogues(s);
        s.Skills["crafting"].Level = 3;
        var before = (s.GameMinute, s.Items.Sum(i => i.Count), CraftXp(s));
        Ok(E.ReadDocument(s, "h03_ceramic_notice"));
        Assert.That(E.KnowsKnowledge(s, "ceramic_methods"), Is.True);
        Ok(E.ReadDocument(s, "h03_ceramic_notice"));
        Assert.That((s.GameMinute, s.Items.Sum(i => i.Count), CraftXp(s)), Is.EqualTo(before), "0 分钟 0 物资 0 经验，再读幂等");
        var t = New();
        t.Progress.Flags.Add("mining_blueprint_pack");
        Assert.That(E.KnowsKnowledge(t, "ceramic_methods"), Is.True, "矿道作业手册同事务授予");
    }

    /// <summary>PLAN-Q172-01：camp 外部既有设施照旧保留，安全屋屋内是独立区（设施另建），柜子只在屋内；旧 camp 不补柜、不合并搬迁。</summary>
    [Test]
    public void PLAN_Q172_01_CampExteriorKept_InteriorSeparate_CabinetInsideOnly()
    {
        var s = New();
        var ext = s.Locations["camp"].Facility;
        ext.ShelterBuilt = true;
        ext.BedBuilt = true;
        ext.FireBuilt = true;
        ext.Workshops.Add("kiln");
        Assert.That(E.CabinetOwnerOf(s, "camp"), Is.Null, "没建屋的旧 camp 没有柜");
        E.DebugSetProgress(s, "camp", 100);
        s.Progress.Flags.Add("hub_registered");
        s.WorldEnemies.RemoveAll(w => w.LocationId == "camp");
        foreach (var (id, n) in new[] { ("wood", 8), ("fiber", 6), ("cloth", 2), ("stone", 4) }) E.CreateItems(s, id, n, s.Ground);
        Start(s, Craft("safehouse_base"));
        Run(s);
        Assert.That(E.SafehouseBuilt(s, "camp"), Is.True);
        var after = s.Locations["camp"].Facility;
        Assert.That((after.ShelterBuilt, after.BedBuilt, after.FireBuilt, after.Workshops.Contains("kiln")), Is.EqualTo((true, true, true, true)), "外部旧设施原样保留");
        var inside = s.Locations["camp"].Safehouse!.Interior;
        Assert.That((inside.BedBuilt, inside.FireBuilt, inside.Workshops.Count), Is.EqualTo((false, false, 0)), "屋内是独立区，设施另建，不搬迁");
        Assert.That(E.CabinetBlocked(s), Does.Contain("屋内"), "外部区不能用柜");
        Ok(E.EnterSafehouse(s));
        Assert.That(E.CabinetBlocked(s), Is.Null, "柜只在屋内");
    }

    /// <summary>PLAN-Q175-01：野营布铺开 10 分钟、柴炉部署 5 分钟，按等待结算身体消耗（与同长等待一致）、不给经验；中途取消物品原样、已过时间不退。</summary>
    [Test]
    public void PLAN_Q175_01_DeployAsWait_NoXp_CancelKeepsItem()
    {
        var s = New();
        E.DebugTeleport(s, "w19_survey");
        s.WorldEnemies.RemoveAll(w => w.LocationId == "w19_survey");
        var tarp = E.CreateItems(s, "field_tarp", 1, ItemInstance.Carried).Single();
        var stove = E.CreateItems(s, "folding_stove", 1, ItemInstance.Carried).Single();
        foreach (var (req, minutes) in new[] { (GameEngine.Deploy(tarp), 10), (GameEngine.Deploy(stove), 5) })
        {
            Assert.That(E.Preview(s, req).IsWork, Is.False, "不是工作");
            var skills = Newtonsoft.Json.JsonConvert.SerializeObject(s.Skills);
            var t0 = s.GameMinute;
            Start(s, req);
            var seg = E.ComputeSegment(s, s.Main!, s.Player);
            var wait = E.ComputeSegment(s, new ActionState { Type = ActionType.Wait }, s.Player);
            Assert.That(Newtonsoft.Json.JsonConvert.SerializeObject(seg.Rates), Is.EqualTo(Newtonsoft.Json.JsonConvert.SerializeObject(wait.Rates)), "身体消耗按等待");
            Run(s);
            Assert.That(s.GameMinute - t0, Is.EqualTo(minutes), "固定分钟，不吃熟练加速");
            Assert.That(Newtonsoft.Json.JsonConvert.SerializeObject(s.Skills), Is.EqualTo(skills), "不给经验");
        }
        // 收起中途取消：布仍部署、原实例不变，已过时间不退
        var t1 = s.GameMinute;
        Start(s, GameEngine.Pack(tarp));
        Steps(s, 1);
        Ok(E.Cancel(s));
        Assert.That(s.GameMinute - t1, Is.EqualTo(5), "已过时间不退");
        Assert.That((E.FindItem(s, tarp.InstanceId)!.Owner, E.IsDeployed(s, tarp)), Is.EqualTo(("w19_survey", true)), "物品不变");
    }

    // ───────────── Q176 报价锁定 / Q177 成功不弹对白 / Q178 已鉴定显示名 ─────────────

    private const string Sub = "active_substance_white_unidentified";
    private const string ServiceEndText = "看看结果和剩余物品，没提交的就还在原处。";

    private ItemInstance Ordinary(GameState s, string owner, string grant = "substance-b9-w1") =>
        E.CreateItems(s, Sub, 1, owner, false, new ItemSource { WorldEnemyId = 1, BattleId = 9, GrantEventId = grant }).Single();

    private GameState AtOffice(int fiber = 2)
    {
        var s = New();
        E.DebugMapTeleport(s, "h05_appraisal");
        ReadDialogues(s);
        s.Items.RemoveAll(i => i.ItemId == "fiber");
        if (fiber > 0) E.CreateItems(s, "fiber", fiber, ItemInstance.Carried);
        return s;
    }

    /// <summary>PLAN-Q176-01/02：安全到达即锁；当日未锁时在当地直接开工（详细鉴定）也锁；同日锁的数值只由种子与世界日决定，不重抽。</summary>
    [Test]
    public void PLAN_Q176_LockOnArrivalOrDirectStart_SameValues()
    {
        var s = AtOffice();
        var arrived = s.City.Quotes.Single();
        var values = new Dictionary<string, int>(arrived.Offers);
        s.City.Quotes.Clear();   // 当日还没锁（例如旧档或到达时不安全）
        var o = Ordinary(s, ItemInstance.Carried);
        Ok(E.StartAppraise(s, o.InstanceId));
        var locked = s.City.Quotes.Single();
        Assert.That(locked.Day, Is.EqualTo(Clock.Day(s.GameMinute)), "直接开工也锁当日整表");
        Assert.That(locked.Offers, Is.EquivalentTo(values), "访问时刻不参与随机：同日同值");
        Run(s);
        Assert.That(s.City.Quotes, Has.Count.EqualTo(1), "已锁不重抽");
    }

    /// <summary>PLAN-Q177-01：鉴定、换物成功只写日志与提示，不弹陶岑结束句、不要求结束对话；可连着换两包。</summary>
    [Test]
    public void PLAN_Q177_01_Success_NoServiceEndDialogue()
    {
        var s = AtOffice();
        var a = Ordinary(s, ItemInstance.Carried, "g-a");
        var b = Ordinary(s, ItemInstance.Carried, "g-b");
        var sessions = s.Story.DialogueLog.Count;
        Ok(E.StartAppraise(s, a.InstanceId));
        Run(s);
        Assert.That(E.DialogueLocked(s), Is.False, "鉴定成功不弹对白");
        Assert.That(s.Log.Any(l => l.Text.Contains("详细鉴定完成")), Is.True);
        Ok(E.StartAppraisalTrade(s, a.InstanceId, "white_for_cloth"));
        Run(s);
        Assert.That(E.DialogueLocked(s), Is.False, "换物成功不弹对白");
        Ok(E.StartAppraisalTrade(s, b.InstanceId, "white_for_scrap"));
        Run(s);
        Assert.That(E.DialogueLocked(s), Is.False);
        Assert.That(s.Log.Count(l => l.Text.StartsWith("换物完成")), Is.EqualTo(2), "两包都写日志");
        Assert.That(s.Story.DialogueLog.Count, Is.EqualTo(sessions), "没有新对白");
    }

    /// <summary>PLAN-Q177-01：已开工后取消、或事务失败（未知报价版本）时，播一次陶岑原结束句；开工前校验失败只给原因不弹。</summary>
    [Test]
    public void PLAN_Q177_01_CancelOrFailure_PlaysServiceEndOnce()
    {
        var s = AtOffice();
        var a = Ordinary(s, ItemInstance.Carried, "g-a");
        Ok(E.StartAppraise(s, a.InstanceId));
        Steps(s, 1);
        Ok(E.Cancel(s));
        Assert.That(E.DialogueLocked(s), Is.True, "取消后播一次结束句");
        Assert.That(E.CurrentDialogueLine(s)!.Text, Is.EqualTo(ServiceEndText));
        ReadDialogues(s);
        Assert.That(E.DialogueLocked(s), Is.False);

        Ok(E.StartAppraisalTrade(s, a.InstanceId, "white_for_cloth"));
        s.City.Pending!.QuoteVersion = "white_barter_v1";
        Run(s);
        Assert.That(E.CurrentDialogueLine(s)?.Text, Is.EqualTo(ServiceEndText), "事务失败播一次");
        ReadDialogues(s);

        var bad = E.StartAppraise(s, 999999);
        Assert.That(bad.Ok, Is.False);
        Assert.That(E.DialogueLocked(s), Is.False, "开工前校验失败只给原因");
    }

    /// <summary>
    /// PLAN-Q178-01：已鉴定普通白色实例显示名“活性物质（白色·已鉴定）”，itemId 不变；卡片分开（未鉴定同名卡另一张）、面板与新日志一致；
    /// 教学样本看过免费说明也不改名；历史日志不重写。
    /// </summary>
    [Test]
    public void PLAN_Q178_01_IdentifiedName_SeparateCard_NewLogsOnly()
    {
        var s = AtOffice();
        var a = Ordinary(s, s.Ground, "g-a");
        var u = Ordinary(s, s.Ground, "g-u");
        var oldName = Cfg.Items.Single(i => i.Id == Sub).Name;
        Assert.That(E.InstanceName(a), Is.EqualTo(oldName));
        var oldLogs = s.Log.Select(l => l.Text).ToList();
        Ok(E.StartAppraise(s, a.InstanceId));
        Run(s);
        var ai = E.FindItem(s, a.InstanceId)!;
        Assert.That(ai.ItemId, Is.EqualTo(Sub), "内部物品 ID 不变");
        Assert.That((E.InstanceName(ai), E.SubstanceLabel(ai)), Is.EqualTo(("活性物质（白色·已鉴定）", "活性物质（白色·已鉴定）")));
        Assert.That(E.InstanceName(E.FindItem(s, u.InstanceId)!), Is.EqualTo(oldName), "未鉴定保持原名");
        Assert.That(s.Log.Last(l => l.Text.Contains("详细鉴定完成")).Text, Does.Contain("活性物质（白色·已鉴定）已记录为普通白色"));
        Assert.That(s.Log.Take(oldLogs.Count).Select(l => l.Text), Is.EqualTo(oldLogs), "历史日志不重写");
        // 地面分两张卡：未鉴定一张、已鉴定一张；标题不混
        var cards = E.OrderedCards(s, s.Ground).Where(k => k.Split('|')[1] == Sub).ToList();
        Assert.That(cards, Is.EquivalentTo(new[] { $"{s.Ground}|{Sub}", $"{s.Ground}|{Sub}|{GameEngine.IdentifiedCardSuffix}" }));
        Assert.That(E.CardTitle(Sub, GameEngine.IdentifiedCardSuffix), Is.EqualTo("活性物质（白色·已鉴定）"));
        Assert.That(E.CardTitle(Sub, null), Is.EqualTo(oldName));
        // 拖已鉴定卡只拿已鉴定那份；拖普通卡只拿未鉴定那份
        Assert.That(E.CardHead(s, DropSource.Parse($"card:{s.Ground}|{Sub}|{GameEngine.IdentifiedCardSuffix}")!)!.InstanceId, Is.EqualTo(a.InstanceId));
        Assert.That(E.CardHead(s, DropSource.Parse($"card:{s.Ground}|{Sub}")!)!.InstanceId, Is.EqualTo(u.InstanceId));
        // 面板里的样本名
        var view = E.AppraisalPreview(s);
        Assert.That(view.Samples.Single(x => x.InstanceId == a.InstanceId).Name, Is.EqualTo("活性物质（白色·已鉴定）"));
        Assert.That(view.Samples.Single(x => x.InstanceId == u.InstanceId).Name, Does.Not.Contain("已鉴定"));
        // 换物日志用新名
        Ok(E.StartAppraisalTrade(s, a.InstanceId, "white_for_cloth"));
        Run(s);
        Assert.That(s.Log.Last(l => l.Text.StartsWith("换物完成")).Text, Does.Contain("交出活性物质（白色·已鉴定）1份"));
        // 教学样本看过免费说明：B22f2（Q209 选 B）起算已鉴定，但归已鉴定教学组（tutapr），不混入普通已鉴定白色（见 B22fTests APP-Q209）
        var t = E.CreateItems(s, Sub, 1, ItemInstance.Carried, false, new ItemSource { GrantEventId = "tutorial-reward-b1", TutorialReward = true }).Single();
        Ok(E.AppraisalTutorialNote(s, t.InstanceId));
        Assert.That(E.InstanceName(E.FindItem(s, t.InstanceId)!), Is.EqualTo("活性物质（白色·已鉴定·首战教学）"));
        Assert.That(E.CardSideOf(E.FindItem(s, t.InstanceId)!), Is.EqualTo(GameEngine.TutorialIdentifiedCardSuffix));
        Assert.That(E.IsIdentifiedWhite(E.FindItem(s, t.InstanceId)!), Is.False);
    }

    // ───────────── B22d 试玩修复 M418 ─────────────

    /// <summary>M418-5：经验预估扣掉同一投料者在途、同一完成日先完成的批次（只改显示，结算不变）。</summary>
    [Test]
    public void M418_StationXpPreview_CountsInFlightBatches()
    {
        var s = Workshop();
        var xp0 = CraftXp(s);
        Kiln(s);   // 在途 6（60 分钟后完成）
        AdvanceTo(s, s.GameMinute + 5);
        Water(s);  // 在途 3（30 分钟后，先完成）
        // 已得 0，在途陶窑 6、净水台 3
        Assert.That(E.StationBatchXpNote(s, E.StationBatchHere(s, "water_station")!), Does.Contain("+3").And.Not.Contain("只剩"), "净水台先完成，不被陶窑占");
        Assert.That(E.StationBatchXpNote(s, E.StationBatchHere(s, "kiln")!), Does.Contain("+6").And.Not.Contain("只剩"), "陶窑在净水台之后完成：12−3 仍够 6");
        // 模拟当天已得 6：净水台先完成占 3，陶窑预计只剩 3
        s.Player.DailyCounts[$"{Clock.Day(s.GameMinute)}|stationXp|crafting"] = 6;
        Assert.That(E.StationBatchXpNote(s, E.StationBatchHere(s, "kiln")!), Does.Contain("只剩3").And.Contain("在途批次预计占3"));
        // 结算不变：净水台 +3、陶窑只剩 3
        AdvanceTo(s, s.GameMinute + 60);
        Assert.That(CraftXp(s) - xp0, Is.EqualTo(6));
    }

    /// <summary>M418-6：被动配方（陶窑/净水台）预估不写工作倍率、不写基础工作量，经验写被动批次经验。</summary>
    [Test]
    public void M418_StationRecipePreview_NoWorkFactor()
    {
        var s = Workshop();
        s.GameMinute = At(3, 0, 40);   // 夜间
        var p = E.Preview(s, ActionRequest.Craft("ex_ceramic_shell", null, null, 1));
        Assert.That(p.IsWork, Is.False);
        Assert.That(p.Notes, Has.None.Contains("工作倍率").And.None.Contains("基础工作量"));
        Assert.That(p.XpGains, Has.Some.Contains("陶窑与净水台被动批次合计每日最多12"));
    }

    /// <summary>M418-4：鉴定面板下次刷新说明与 Q176 一致（到达或在当地开面板/开工时锁，过午夜不自动锁）。</summary>
    [Test]
    public void M418_QuoteRefreshText_MatchesQ176()
    {
        var s = AtOffice();
        var t = E.AppraisalPreview(s).NextRefreshText;
        Assert.That(t, Does.Contain("安全到达鉴定所").And.Contain("打开本面板/开工时锁定").And.Contain("不会自动换表"));
        Assert.That(t, Does.Not.Contain("第一次安全到访"));
    }
}
