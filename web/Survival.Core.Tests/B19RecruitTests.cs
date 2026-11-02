using Newtonsoft.Json.Linq;
using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 批次 B19（R60 候选生成、R61 登记/名额/退役）。规则来源：R08「2026-10-30 B3 实体收容所、周期招募与战利品owner」RC1—RC8、
/// 「v3建筑入口校准」、「Q62—Q74」当前优先节；S01 本轮章；A06「B3 随机模板与技能解锁边界」；A11 Q33 shelter_recruitment。
/// 验收 RC01—RC17、A06-C01、COMP12、SCB04。
/// </summary>
public class B19RecruitTests : TestBase
{
    private const string Shelter = "h06_shelter";

    private static JObject Design(string name) => JObject.Parse(File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, name)));

    private static void Ok(ActionResult r) => Assert.That(r.Ok, Is.True, r.Reason);

    /// <summary>营区登记后实际到达收容所（开放候选）；c 为容量 C。</summary>
    private GameState AtShelter(int c = 2, uint seed = 12345)
    {
        var s = New(seed);
        s.SpawnNextCheck = int.MaxValue;
        s.PatrolNextCheck = int.MaxValue;
        s.GameMinute = At(1, 10, 0);
        if (c >= 3) s.Progress.Flags.Add("church_route_record");
        if (c >= 4) s.Progress.Flags.Add("academy_safety_record");
        E.DebugRecruitment(s, "go");
        Assert.That(s.CurrentLocation, Is.EqualTo(Shelter));
        return s;
    }

    /// <summary>空闲等待 n 个 30 分钟（世界 tick 照常推进）。</summary>
    private void WaitHalfHours(GameState s, int n)
    {
        for (var i = 0; i < n; i++)
        {
            E.DebugSetStats(s, 20, 90, 90, 20, 90, 80);
            Start(s, ActionRequest.Wait(30));
            Run(s);
        }
    }

    // ───────────── 配置（DesignSync） ─────────────

    /// <summary>config.party.recruitment 与 expansion-combat.json recruitment、R08 RC1—RC3 一致；防御档读 Q62 randomRecruit。</summary>
    [Category("DesignSync")]
    [Test]
    public void RC_Config_FromDesign()
    {
        var d = (JObject)Design("design.expansion-combat.json")["recruitment"]!;
        var r = Cfg.Party.Recruitment;
        Assert.That(r.LocationId, Is.EqualTo((string)d["locationId"]!));
        Assert.That(r.ContractServiceId, Is.EqualTo((string)d["serviceId"]!));
        Assert.That(r.RefreshMinutes, Is.EqualTo((int)d["refreshMinutes"]!));
        Assert.That(r.CandidatesPerCycle, Is.EqualTo((int)d["candidatesPerCycle"]!));
        Assert.That((string)d["rosterCapFormula"]!, Is.EqualTo($"min({r.SharedRosterMax},C-1)"));
        Assert.That(r.RegistrationMinutes, Is.EqualTo(10));
        Assert.That(r.RosterAliveCap, Is.EqualTo(10));
        Assert.That(r.Templates.Count, Is.EqualTo(9));
        Assert.That(r.Templates.Select(t => t.Main).Distinct().Count(), Is.EqualTo(9), "九个模板主熟练各不相同");
        Assert.That(r.Templates.All(t => t.Subs.Count == 2 && !t.Subs.Contains(t.Main)), Is.True);
        Assert.That(r.MainLevelWeights, Is.EquivalentTo(new Dictionary<string, int> { ["2"] = 70, ["3"] = 30 }));
        Assert.That(r.SubLevelWeights, Is.EquivalentTo(new Dictionary<string, int> { ["1"] = 50, ["2"] = 50 }));
        Assert.That((r.Body.Health, r.Body.Energy, r.Body.Wakefulness, r.Body.Morale, r.Body.Immunity),
            Is.EqualTo((new[] { 14, 20 }, new[] { 10, 16 }, new[] { 50, 75 }, new[] { 40, 65 }, new[] { 60, 75 })));
        Assert.That((r.Body.Satiety, r.Body.Hydration, r.Body.HealthMax, r.Body.EnergyMax, r.Body.Virus), Is.EqualTo((40.0, 40.0, 20.0, 20.0, 0.0)));
        var rr = Cfg.ActorBaseDefense.RandomRecruit!;
        Assert.That(((double)rr["baseSpeed"]!["min"]!, (double)rr["baseSpeed"]!["max"]!, (double)rr["baseSpeed"]!["step"]!), Is.EqualTo((3.0, 5.0, 0.1)));
        var svc = Cfg.City.Services.Single(x => x.Id == r.ServiceId);
        Assert.That(svc.Inputs, Is.Empty, "Q33：招募按 R07/R08 原契约，不在服务表里重复收费");
    }

    // ───────────── R60 开放、周期与种子 ─────────────

    /// <summary>RC01/COMP12：营区登记后首次实际到达收容所记开放时刻并生成 3 名；没登记到达不开放；72 小时（4320 分钟）后换一批。</summary>
    [Test]
    public void RC01_OpenOnFirstArrival_ThreeCandidates_Refresh72h()
    {
        var s = New();
        s.SpawnNextCheck = int.MaxValue;
        s.PatrolNextCheck = int.MaxValue;
        E.DebugMapTeleport(s, Shelter);
        Assert.That(E.RecruitOpened(s), Is.False, "没有营区登记不开放");
        Assert.That(E.RecruitPreview(s).OpenText, Does.Contain("营区登记"));

        s = New();
        s.SpawnNextCheck = int.MaxValue;
        s.PatrolNextCheck = int.MaxValue;
        s.GameMinute = At(1, 10, 0);
        var text = E.DebugRecruitment(s, "go");
        Assert.That(text, Does.Contain("周期候选开放"), "到达说明里写开放");
        var opened = s.GameMinute;
        Assert.That(s.Recruit.ShelterOpenedAt, Is.EqualTo(opened));
        Assert.That(s.Recruit.Cycle, Is.EqualTo(0));
        Assert.That(s.Recruit.Candidates.Count, Is.EqualTo(3));
        var first = s.Recruit.Candidates.Select(c => c.Name).ToList();

        // 离开再回来、过了 71 小时 55 分仍是同一批
        s.GameMinute = opened + 4320 - 5;
        E.EnsureRecruitCycle(s);
        Assert.That(s.Recruit.Candidates.Select(c => c.Name), Is.EqualTo(first));
        // 真实世界 tick 跨过 72 小时交界：换成第 1 期
        WaitHalfHours(s, 1);
        Assert.That(s.Recruit.Cycle, Is.EqualTo(1));
        Assert.That(s.Recruit.Candidates.Count, Is.EqualTo(3));
        Assert.That(s.Recruit.Candidates.All(c => c.Cycle == 1), Is.True);
        Assert.That(s.Recruit.Candidates.Select(c => c.Name).Intersect(first), Is.Empty, "未招者离开，新一批有新名字");
        Assert.That(s.Recruit.ShelterOpenedAt, Is.EqualTo(opened), "起点不随再访改变");
    }

    /// <summary>RC02：同周期进出、打开面板、读档都不重抽（候选整份快照保存）。</summary>
    [Test]
    public void RC02_SameCycle_NoReroll_AcrossVisitsAndReload()
    {
        var s = AtShelter();
        var snap = JArray.FromObject(s.Recruit.Candidates).ToString();
        E.DebugMapTeleport(s, "h02_square");
        E.DebugMapTeleport(s, Shelter);
        E.EnsureRecruitCycle(s);
        _ = E.RecruitPreview(s);
        Assert.That(JArray.FromObject(s.Recruit.Candidates).ToString(), Is.EqualTo(snap));
        var m = SaveSerializer.Deserialize(SaveSerializer.Serialize(s), Cfg);
        E.EnsureRecruitCycle(m);
        Assert.That(JArray.FromObject(m.Recruit.Candidates).ToString(), Is.EqualTo(snap));
    }

    /// <summary>RC03/SCB04：跨过多个周期只生成当前这一批（不积 9/12 人、不补历史周期）；同局同期由种子决定，与生成时刻无关。</summary>
    [Test]
    public void RC03_ManyCyclesSkipped_OnlyCurrentBatch()
    {
        var s = AtShelter();
        var start = s.Recruit.ShelterOpenedAt!.Value;
        s.GameMinute = start + 4320 * 5 + 100;
        E.EnsureRecruitCycle(s);
        Assert.That(s.Recruit.Cycle, Is.EqualTo(5));
        Assert.That(s.Recruit.Candidates.Count, Is.EqualTo(3));
        Assert.That(s.Recruit.Candidates.All(c => c.Cycle == 5), Is.True);

        // 同一局另一份存档：经过 1、2、3、4 期再到第 5 期，第 5 期的模板与身体相同（只有名字流水号不同）
        var t = AtShelter();
        for (var k = 1; k <= 5; k++)
        {
            t.GameMinute = start + 4320 * k + 7;
            E.EnsureRecruitCycle(t);
        }
        Assert.That(t.Recruit.Candidates.Select(Body), Is.EqualTo(s.Recruit.Candidates.Select(Body)));
    }

    private static string Body(RecruitCandidate c) =>
        $"{c.TemplateId}|{c.MainSkill}{c.MainLevel}|{c.SubSkill}{c.SubLevel}|{c.Health}|{c.Energy}|{c.Wakefulness}|{c.Morale}|{c.Immunity}|{c.BaseSpeed}|{c.ControlResist}|{c.Evasion}|{c.BaseArmor}";

    /// <summary>
    /// RC02 种子：本局随机根 + 收容所 + 周期号。同局同期相同，不同周期/不同局不同（统计上）；离线不走——不推进世界时间就不换批。
    /// </summary>
    [Test]
    public void RC02_SeedIsCampaignShelterCycle()
    {
        var a = AtShelter(seed: 777);
        var b = AtShelter(seed: 777);
        b.GameMinute += 300;   // 晚 5 小时到达也一样（同局第 0 期）
        b.Recruit = new RecruitState();
        Assert.That(E.TryOpenShelterRecruitment(b), Is.True);
        Assert.That(b.Recruit.Candidates.Select(Body), Is.EqualTo(a.Recruit.Candidates.Select(Body)));
        Assert.That(GameEngine.RecruitSeed(a, Shelter, 0), Is.Not.EqualTo(GameEngine.RecruitSeed(a, Shelter, 1)));
        var other = AtShelter(seed: 778);
        Assert.That(GameEngine.RecruitSeed(other, Shelter, 0), Is.Not.EqualTo(GameEngine.RecruitSeed(a, Shelter, 0)));
    }

    /// <summary>
    /// RC02/RC03 与 A06-C01：模板期内不重复、主副不同；主 Lv2/3、副 Lv1/2、其余 Lv1；身体与人物档落在策划区间、步长；
    /// 大样本下主 Lv2 约 70%、副 Lv2 约 50%、模板与副熟练近似等权。
    /// </summary>
    [Test]
    public void RC03_A06C01_TemplatesSkillsBody_InRangeAndDistribution()
    {
        var s = AtShelter();
        var start = s.Recruit.ShelterOpenedAt!.Value;
        var all = new List<RecruitCandidate>();
        for (var k = 0; k < 1200; k++)
        {
            s.GameMinute = start + 4320 * k;
            E.EnsureRecruitCycle(s);
            var batch = s.Recruit.Candidates;
            Assert.That(batch.Select(c => c.TemplateId).Distinct().Count(), Is.EqualTo(3), "期内模板不重复");
            all.AddRange(batch);
        }
        var r = Cfg.Party.Recruitment;
        foreach (var c in all)
        {
            var t = r.Template(c.TemplateId)!;
            Assert.That(c.MainSkill, Is.EqualTo(t.Main));
            Assert.That(t.Subs, Does.Contain(c.SubSkill));
            Assert.That(c.MainLevel, Is.InRange(2, 3));
            Assert.That(c.SubLevel, Is.InRange(1, 2));
            Assert.That(c.Health, Is.InRange(14, 20));
            Assert.That(c.Energy, Is.InRange(10, 16));
            Assert.That((c.Satiety, c.Hydration, c.VirusLoad, c.HealthMax, c.EnergyMax), Is.EqualTo((40.0, 40.0, 0.0, 20.0, 20.0)));
            Assert.That(c.Wakefulness, Is.InRange(50, 75));
            Assert.That(c.Morale, Is.InRange(40, 65));
            Assert.That(c.Immunity, Is.InRange(60, 75));
            foreach (var x in new[] { c.Health, c.Energy, c.Wakefulness, c.Morale, c.Immunity }) Assert.That(x % 1, Is.EqualTo(0), "身体为整数");
            Assert.That(c.BaseSpeed, Is.InRange(3.0, 5.0));
            Assert.That(Math.Abs(c.BaseSpeed * 10 - Math.Round(c.BaseSpeed * 10)), Is.LessThan(1e-9), "速度步长 0.1（Q62 当前优先）");
            Assert.That(c.ControlResist, Is.InRange(0.0, 10.0));
            Assert.That(c.ControlResist * 2 % 1, Is.EqualTo(0).Within(1e-9));
            Assert.That(c.Evasion, Is.InRange(34.0, 38.0));
            Assert.That(c.Evasion * 2 % 1, Is.EqualTo(0).Within(1e-9));
            Assert.That(c.BaseArmor, Is.EqualTo(0));
        }
        var n = all.Count;
        Assert.That(all.Count(c => c.MainLevel == 2) / (double)n, Is.EqualTo(0.7).Within(0.04));
        Assert.That(all.Count(c => c.SubLevel == 2) / (double)n, Is.EqualTo(0.5).Within(0.04));
        foreach (var g in all.GroupBy(c => c.TemplateId)) Assert.That(g.Count() / (double)n, Is.EqualTo(1 / 9.0).Within(0.025), g.Key);
        foreach (var g in all.GroupBy(c => c.TemplateId))
            Assert.That(g.Count(c => c.SubSkill == r.Template(g.Key)!.Subs[0]) / (double)g.Count(), Is.EqualTo(0.5).Within(0.1), g.Key + " 副熟练二选一");
        Assert.That(all.Select(c => c.Health).Distinct().Count(), Is.EqualTo(7));
        Assert.That(all.Select(c => c.BaseSpeed).Distinct().Count(), Is.EqualTo(21));
        Assert.That(all.Select(c => c.Evasion).Distinct().Count(), Is.EqualTo(9));
        Assert.That(all.Select(c => c.ControlResist).Distinct().Count(), Is.EqualTo(21));
    }

    /// <summary>RC13：姓名与数值独立——同一种子同一期，名字流水号不同、模板/熟练/身体/人物档完全相同；没有性别/外观字段影响数值。</summary>
    [Test]
    public void RC13_NameIndependentOfStats()
    {
        var a = AtShelter(seed: 4242);
        var b = New(4242);
        b.SpawnNextCheck = int.MaxValue;
        b.PatrolNextCheck = int.MaxValue;
        b.GameMinute = At(1, 10, 0);
        b.Recruit.NextNameSeq = 500;
        E.DebugRecruitment(b, "go");
        Assert.That(b.Recruit.Candidates.Select(Body), Is.EqualTo(a.Recruit.Candidates.Select(Body)));
        Assert.That(b.Recruit.Candidates.Select(c => c.Name), Is.Not.EqualTo(a.Recruit.Candidates.Select(c => c.Name)));
        Assert.That(b.Recruit.Candidates[0].Name, Does.EndWith("500"));
        Assert.That(a.Recruit.Candidates[0].Name, Does.StartWith(Cfg.Party.Recruitment.NamePrefix));
    }

    /// <summary>RC14：未招候选只是资料快照——不进 Actors、不结算身体、不随时间变化、不模拟死亡感染。</summary>
    [Test]
    public void RC14_CandidatesNotSimulated()
    {
        var s = AtShelter();
        var snap = JArray.FromObject(s.Recruit.Candidates).ToString();
        var actors = s.Actors.Keys.ToList();
        WaitHalfHours(s, 4);
        Assert.That(JArray.FromObject(s.Recruit.Candidates).ToString(), Is.EqualTo(snap));
        Assert.That(s.Actors.Keys, Is.EquivalentTo(actors));
        Assert.That(s.Roster, Is.Empty);
    }

    /// <summary>RC07 旧档：30→31 补空招募状态（不推算开放、不补历史周期），之后首次实际安全到达收容所才起算；日志一条；幂等。</summary>
    [Test]
    public void RC07_Migration30To31_OpensOnFirstArrivalAfterLoad()
    {
        var s = New();
        s.SpawnNextCheck = int.MaxValue;
        s.PatrolNextCheck = int.MaxValue;
        s.Progress.Flags.Add("hub_registered");
        s.GameMinute = At(9, 8, 0);
        var json = SaveSerializer.Serialize(s).Replace($"\"SchemaVersion\":\"{GameState.CurrentSchema}\"", "\"SchemaVersion\":\"30\"");
        var j = JObject.Parse(json);
        j.Remove("Recruit");
        var m = SaveSerializer.Deserialize(j.ToString(), Cfg);
        Assert.That(m.SchemaVersion, Is.EqualTo(GameState.CurrentSchema));
        Assert.That(m.Recruit.ShelterOpenedAt, Is.Null);
        Assert.That(m.Recruit.Candidates, Is.Empty);
        Assert.That(m.Log.Count(l => l.Text.Contains($"版本{GameState.CurrentSchema}")), Is.EqualTo(1));
        E.DebugMapTeleport(m, Shelter);
        Assert.That(m.Recruit.ShelterOpenedAt, Is.EqualTo(At(9, 8, 0)), "旧档首次实到才起算");
        Assert.That(m.Recruit.Cycle, Is.EqualTo(0));
        var again = SaveSerializer.Deserialize(SaveSerializer.Serialize(m), Cfg);
        Assert.That(again.Log.Count(l => l.Text.Contains($"版本{GameState.CurrentSchema}")), Is.EqualTo(1));
    }

    /// <summary>不在安全访问时不开放：战斗/敌踪中到达不记起点（RC1“安全空闲”）。</summary>
    [Test]
    public void RC01_NotOpenedWhenUnsafe()
    {
        var s = New();
        s.Progress.Flags.Add("hub_registered");
        s.CurrentLocation = Shelter;
        s.Encounter = new PendingEncounter();
        Assert.That(E.TryOpenShelterRecruitment(s), Is.False);
        s.Encounter = null;
        Assert.That(E.TryOpenShelterRecruitment(s), Is.True);
        Assert.That(E.TryOpenShelterRecruitment(s), Is.False, "只开放一次");
    }
}
