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
        E.DebugMapTeleport(s, "camp");
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

    // ───────────── R61 登记事务（RC4/RC7、S01） ─────────────

    /// <summary>登记一名当前候选并走完 10 分钟；返回新角色的名册条目。</summary>
    private RosterEntry Recruit(GameState s, int index = 0)
    {
        var c = s.Recruit.Candidates[index];
        Ok(E.StartRecruit(s, c.CandidateId, Guid.NewGuid().ToString("N")));
        Run(s);
        return s.Roster.Single(r => r.CandidateId == c.CandidateId && r.Cycle == c.Cycle);
    }

    /// <summary>
    /// RC04/A06-C01：0 材料 10 分钟 wait（两段）完成才建 actorId，按候选快照一次初始化（主/副/其余 Lv1、经验级内 0、身体与人物档、V0、装备与随身空），
    /// 入名册作候补；候选标记已招；换批后已招者不被替换。
    /// </summary>
    [Test]
    public void RC04_A06C01_RecruitCreatesExactSnapshot_NotReplacedByRefresh()
    {
        var s = AtShelter();
        var c = s.Recruit.Candidates[1].Clone();
        var t0 = s.GameMinute;
        var itemsBefore = s.Items.Count;
        Ok(E.StartRecruit(s, c.CandidateId));
        Assert.That(s.Main!.PlannedMinutes, Is.EqualTo(10));
        Assert.That(s.Recruit.Pending!.CandidateId, Is.EqualTo(c.CandidateId));
        Assert.That(E.SharedRosterLocked(s), Is.EqualTo(1), "开始即锁 1 个名额");
        E.Advance(s);
        Assert.That(s.Roster, Is.Empty, "第一段后还没建人");
        Run(s);
        Assert.That(s.GameMinute - t0, Is.EqualTo(10));
        var r = s.Roster.Single();
        Assert.That(r.IsRandom && r.Present, Is.True);
        Assert.That(s.Party, Does.Not.Contain(r.ActorId), "入名册作候补，不自动编入同行（开发假设 B19-6）");
        Assert.That(E.Reserves(s).Select(x => x.ActorId), Does.Contain(r.ActorId));
        var a = s.Actors[r.ActorId];
        Assert.That(a.Name, Is.EqualTo(c.Name));
        foreach (var sk in Cfg.Skills)
        {
            var want = sk.Id == c.MainSkill ? c.MainLevel : sk.Id == c.SubSkill ? c.SubLevel : 1;
            Assert.That(a.Skills[sk.Id].Level, Is.EqualTo(want), sk.Id);
            Assert.That(a.Skills[sk.Id].Xp, Is.EqualTo(0), "级内 0 经验（A06-C01）");
        }
        Assert.That((a.Stats.Health, a.Stats.HealthMax, a.Stats.Energy, a.Stats.EnergyMax, a.Stats.Satiety, a.Stats.Hydration, a.Stats.Wakefulness, a.Stats.Morale),
            Is.EqualTo((c.Health, c.HealthMax, c.Energy, c.EnergyMax, c.Satiety, c.Hydration, c.Wakefulness, c.Morale)));
        Assert.That((a.Body.Immunity, a.Body.Infected, a.Body.VirusLoad), Is.EqualTo((c.Immunity, false, 0.0)));
        Assert.That(E.ActorBaseSpeed(s, r.ActorId), Is.EqualTo(c.BaseSpeed));
        var prof = E.ActorProfile(s, r.ActorId)!;
        Assert.That((prof.ControlResist, prof.Evasion, prof.BaseArmor), Is.EqualTo((c.ControlResist, c.Evasion, c.BaseArmor)));
        Assert.That(s.Items.Count(i => i.Owner == ItemInstance.CarriedOwner(r.ActorId) || i.IsWornBy(r.ActorId)), Is.EqualTo(0), "不送装备与物资");
        Assert.That(s.Items.Count, Is.EqualTo(itemsBefore), "0 材料、不发物");
        Assert.That(s.Recruit.Candidates[1].Status, Is.EqualTo(RecruitCandidate.Recruited));
        Assert.That(E.RecruitBlocked(s, c.CandidateId), Does.Contain("已招入"));

        // 换批：已招者留在名册、状态与数值不被替换
        s.GameMinute = s.Recruit.ShelterOpenedAt!.Value + 4320;
        E.EnsureRecruitCycle(s);
        Assert.That(s.Recruit.Cycle, Is.EqualTo(1));
        Assert.That(s.Roster.Single().ActorId, Is.EqualTo(r.ActorId));
        Assert.That(s.Actors[r.ActorId].Skills[c.MainSkill].Level, Is.EqualTo(c.MainLevel));
    }

    /// <summary>RC05/RC10：K=min(3,C−1)：C1/2/3/4 分别 0/1/2/3；满额按钮禁用并写“随机/保底名册K/K；留守不释放名额”，满额不先耗时。</summary>
    [Test]
    public void RC05_SharedCapByCapacity_FullBlocksWithoutTime()
    {
        foreach (var (c, k) in new[] { (2, 1), (3, 2), (4, 3) })
        {
            var s = AtShelter(c);
            Assert.That(E.SharedRosterCap(s), Is.EqualTo(k), $"C{c}");
            for (var i = 0; i < k; i++) Recruit(s, i);
            Assert.That(E.SharedRoster(s).Count, Is.EqualTo(k));
            Assert.That(E.Reserves(s).Count, Is.EqualTo(k), "都在留守（候补）");
            var free = s.Recruit.Candidates.FirstOrDefault(x => x.Status == RecruitCandidate.Available);
            if (free == null)
            {
                s.GameMinute = s.Recruit.ShelterOpenedAt!.Value + 4320;
                E.EnsureRecruitCycle(s);
                free = s.Recruit.Candidates[0];
            }
            var t = s.GameMinute;
            Assert.That(E.RecruitBlocked(s, free.CandidateId), Is.EqualTo($"随机/保底名册{k}/{k}；留守不释放名额"));
            var res = E.StartRecruit(s, free.CandidateId);
            Assert.That(res.Ok, Is.False);
            Assert.That(s.GameMinute, Is.EqualTo(t), "名额不够不推进时间");
            Assert.That(s.Main, Is.Null);
            Assert.That(E.RecruitPreview(s).Candidates.Count, Is.EqualTo(3), "满额仍可查看三名");
            Assert.That(1 + s.Roster.Count(r => r.Present), Is.LessThanOrEqualTo(10));
        }
        Assert.That(E.SharedRosterCap(New()), Is.EqualTo(0), "C1 没有随机/保底名额");
    }

    /// <summary>RC07/COMP12/SCB04：周期交界前 5 分钟开始的登记跨期完成，只建这一人、名额不超；其余旧候选离开、新批 3 人；在办候选与名额一路保留。</summary>
    [Test]
    public void RC07_CrossCycleRegistration_OnlyOne_NoOverflow()
    {
        var s = AtShelter(2);
        var start = s.Recruit.ShelterOpenedAt!.Value;
        s.GameMinute = start + 4320 - 5;
        E.EnsureRecruitCycle(s);
        var c = s.Recruit.Candidates[2].Clone();
        var oldNames = s.Recruit.Candidates.Select(x => x.Name).ToList();
        const string req = "req-cross";
        Ok(E.StartRecruit(s, c.CandidateId, req));
        Assert.That(E.StartRecruit(s, c.CandidateId, req).Duplicate, Is.True, "同一 requestId 重放不另开");
        E.Advance(s);   // 第一段跨过交界
        Assert.That(s.Recruit.Cycle, Is.EqualTo(1), "交界换批");
        Assert.That(s.Recruit.Pending!.CandidateId, Is.EqualTo(c.CandidateId), "在办登记的候选与名额保留");
        Assert.That(s.Recruit.Candidates.Select(x => x.Name).Intersect(oldNames), Is.Empty);
        Assert.That(E.SharedRosterLocked(s), Is.EqualTo(1));
        foreach (var x in s.Recruit.Candidates) Assert.That(E.RecruitBlocked(s, x.CandidateId), Is.Not.Null, "在办期间不能再开第二笔");
        Run(s);
        var r = s.Roster.Single();
        Assert.That((r.Name, r.Cycle), Is.EqualTo((c.Name, (int?)0)));
        Assert.That(E.SharedRoster(s).Count, Is.EqualTo(1));
        Assert.That(s.Recruit.Pending, Is.Null);
        Assert.That(E.StartRecruit(s, c.CandidateId, req).Duplicate, Is.True, "完成后重放也不建第二人");
        Assert.That(s.Roster.Count, Is.EqualTo(1));
        Assert.That(s.Recruit.Candidates.All(x => x.Status == RecruitCandidate.Available), Is.True, "新批不受旧登记影响");
        Assert.That(E.RecruitBlocked(s, s.Recruit.Candidates[0].CandidateId), Is.EqualTo("随机/保底名册1/1；留守不释放名额"));
    }

    /// <summary>RC04：中途取消只取消登记——已耗时间不退、候选仍是同一人同属性、名额释放；读档后在办登记照常续完只建一人。</summary>
    [Test]
    public void RC04_CancelKeepsCandidate_ReloadMidwayCreatesOnce()
    {
        var s = AtShelter();
        var snap = JArray.FromObject(s.Recruit.Candidates).ToString();
        var c = s.Recruit.Candidates[0];
        var t = s.GameMinute;
        Ok(E.StartRecruit(s, c.CandidateId));
        E.Advance(s);
        Ok(E.Cancel(s));
        Assert.That(s.GameMinute, Is.EqualTo(t + 5), "已耗时间不退");
        Assert.That(s.Recruit.Pending, Is.Null);
        Assert.That(E.SharedRosterLocked(s), Is.EqualTo(0));
        Assert.That(JArray.FromObject(s.Recruit.Candidates).ToString(), Is.EqualTo(snap), "候选同一人、属性不重抽、回到可招");
        Assert.That(s.Roster, Is.Empty);

        Ok(E.StartRecruit(s, c.CandidateId));
        E.Advance(s);
        var m = SaveSerializer.Deserialize(SaveSerializer.Serialize(s), Cfg);
        Run(m);
        Assert.That(m.Roster.Count, Is.EqualTo(1));
        Assert.That(m.GameMinute, Is.EqualTo(t + 15), "读档不补扣时间");
    }

    /// <summary>RC15：主角在登记中途死亡——死亡优先，不建人、不续局。</summary>
    [Test]
    public void RC15_PlayerDeathStopsRegistration()
    {
        var s = AtShelter();
        Ok(E.StartRecruit(s, s.Recruit.Candidates[0].CandidateId));
        s.Stats.Health = 0;
        var res = E.Advance(s);
        Assert.That(res.Status, Is.EqualTo(SegmentStatus.Dead));
        Assert.That(s.Progress.Dead, Is.True);
        Assert.That(s.Roster, Is.Empty);
        Assert.That(E.StartRecruit(s, s.Recruit.Candidates[1].CandidateId).Ok, Is.False);
    }

    /// <summary>登记只在收容所安全空闲时办理（v3 校准：不从广场或别处远程）。</summary>
    [Test]
    public void RC04_OnlyAtShelter()
    {
        var s = AtShelter();
        var id = s.Recruit.Candidates[0].CandidateId;
        E.DebugMapTeleport(s, "camp");
        Assert.That(E.RecruitBlocked(s, id), Does.Contain("只能在"));
        Assert.That(E.StartRecruit(s, id).Ok, Is.False);
        E.DebugMapTeleport(s, Shelter);
        Assert.That(E.RecruitBlocked(s, id), Is.Null);
    }

    // ───────────── R61 退役与认领箱（RC4/RC6） ─────────────

    /// <summary>
    /// RC06/RC11：退役把本人全部个人物品原子迁到收容所认领箱（实例不变，不新造、不销毁），释放名额；同周期不能再招回；候选池不补人；
    /// 认领箱只能实地取；存档保留退役记录与认领箱。
    /// </summary>
    [Test]
    public void RC06_RetireMovesItemsToClaimBox_FreesSlot()
    {
        var s = AtShelter(2);
        var r = Recruit(s, 0);
        Assert.That(E.RecruitBlocked(s, s.Recruit.Candidates[1].CandidateId), Is.EqualTo("随机/保底名册1/1；留守不释放名额"));
        var spear = E.CreateItems(s, "wooden_spear", 1, ItemInstance.EquipOwner(r.ActorId, EquipSlots.Weapon1)).Single();
        var food = E.CreateItems(s, "fiber", 1, ItemInstance.CarriedOwner(r.ActorId)).Single();
        var items = s.Items.Count;
        var view = E.RecruitPreview(s).Retirables.Single();
        Assert.That(view.ConfirmText, Does.Contain("永久离开名册").And.Contain("技能不返还").And.Contain("认领箱"));
        Assert.That(view.Blocked, Is.Null);
        Ok(E.Retire(s, r.ActorId, "ret-1"));
        Assert.That(E.Retire(s, r.ActorId, "ret-1").Duplicate, Is.True);
        Assert.That(s.Items.Count, Is.EqualTo(items), "不新造、不销毁");
        Assert.That(spear.Owner, Is.EqualTo(ItemInstance.RemainsOwner(r.ActorId)));
        Assert.That(food.Owner, Is.EqualTo(ItemInstance.RemainsOwner(r.ActorId)));
        Assert.That(s.Remains[r.ActorId].IsClaim && s.Remains[r.ActorId].LocationId == Shelter, Is.True);
        Assert.That(r.Retired && !r.Present, Is.True);
        Assert.That(E.Reserves(s), Is.Empty);
        Assert.That(E.SharedRoster(s), Is.Empty, "退役释放名额");
        Assert.That(E.RecruitBlocked(s, s.Recruit.Candidates[0].CandidateId), Does.Contain("已招入"), "同周期不能再招回");
        Assert.That(E.RecruitBlocked(s, s.Recruit.Candidates[1].CandidateId), Is.Null, "可以再招别人");
        Assert.That(s.Recruit.Candidates.Count, Is.EqualTo(3), "候选池不补人");
        E.DebugMapTeleport(s, "camp");
        Assert.That(E.TakeFromRemainsBlocked(s, r.ActorId, food.InstanceId, false), Does.Contain("认领箱在"));
        E.DebugMapTeleport(s, Shelter);
        Ok(E.TakeFromRemains(s, r.ActorId, food.InstanceId, false));
        Assert.That(food.Owner, Is.EqualTo(ItemInstance.Carried));
        var m = SaveSerializer.Deserialize(SaveSerializer.Serialize(s), Cfg);
        Assert.That(m.Roster.Single().Retired, Is.True);
        Assert.That(m.Remains[r.ActorId].IsClaim, Is.True);
    }

    /// <summary>退役限制：主角、固定角色不行；不在收容所、物品被预留时不行；同行者退役离队只清自己的号位。</summary>
    [Test]
    public void RC06_RetireRules()
    {
        var s = AtShelter(4);
        E.DebugRecruit(s, "npc_lin_zhao");
        Assert.That(E.RetireBlocked(s, GameState.PlayerId), Is.Not.Null);
        Assert.That(E.RetireBlocked(s, "npc_lin_zhao"), Does.Contain("固定角色"));
        var r = Recruit(s, 0);
        E.DebugToggleActive(s, r.ActorId);
        Assert.That(s.Party, Does.Contain(r.ActorId));
        var ver = s.FormationVersion;
        var bandage = E.CreateItems(s, "bandage", 1, ItemInstance.CarriedOwner(r.ActorId)).Single();
        bandage.ReservedBy = 999;
        Assert.That(E.RetireBlocked(s, r.ActorId), Does.Contain("预留"));
        bandage.ReservedBy = null;
        E.DebugMapTeleport(s, "camp");
        Assert.That(E.RetireBlocked(s, r.ActorId), Does.Contain("只能在"));
        E.DebugMapTeleport(s, Shelter);
        var linSlot = GameEngine.FormationSlotOf(s, "npc_lin_zhao");
        Ok(E.Retire(s, r.ActorId));
        Assert.That(s.Party, Does.Not.Contain(r.ActorId));
        Assert.That(GameEngine.FormationSlotOf(s, r.ActorId), Is.EqualTo(0));
        Assert.That(GameEngine.FormationSlotOf(s, "npc_lin_zhao"), Is.EqualTo(linSlot), "其余号位不动");
        Assert.That(s.FormationVersion, Is.GreaterThan(ver));
        Assert.That(E.SetActiveBlocked(s, r.ActorId, true), Does.Contain("退役"));
    }

    // ───────────── RB01 共享 K、旧档超额（RC6/RC7） ─────────────

    /// <summary>RC10：随机与 RB01 共用 K（不各给三人）：C2 有一名随机同行时 RB01 写满额；有一名护送员时随机候选也写满额。</summary>
    [Test]
    public void RC10_RandomAndRb01ShareK()
    {
        var s = AtShelter(2);
        var r = Recruit(s, 0);
        Assert.That(E.EscortRegistrationBlocked(s), Does.Contain("候补"), "随机招入者留守时 RB01 先指向编入候补");
        E.DebugToggleActive(s, r.ActorId);
        Assert.That(E.EscortRegistrationBlocked(s), Does.Contain("同行没有空位"), "C2 同行已满");
        Assert.That(E.SharedRoster(s).Count, Is.EqualTo(1));

        var t = AtShelter(3);
        Assert.That(E.SharedRosterCap(t), Is.EqualTo(2));
        Assert.That(E.DebugAddEscort(t), Does.Contain("加入"));
        Assert.That(E.DebugAddEscort(t), Does.Contain("加入"));
        Assert.That(E.SharedRoster(t).Count, Is.EqualTo(2));
        Assert.That(E.RecruitBlocked(t, t.Recruit.Candidates[0].CandidateId), Is.EqualTo("随机/保底名册2/2；留守不释放名额"), "护送员占满 K 时随机候选不能招");
        Assert.That(E.DebugAddEscort(t), Does.Contain("2/2"), "护送员也不超 K");

        var u = AtShelter(3);
        Recruit(u, 0);
        E.DebugToggleActive(u, u.Roster[0].ActorId);
        Assert.That(E.EscortRegistrationBlocked(u), Is.Null, "C3：随机1人占1个名额，RB01 还剩1个");
        Recruit(u, 1);
        E.DebugToggleActive(u, u.Roster[1].ActorId);
        Assert.That(E.SharedRosterLocked(u), Is.EqualTo(2));
        Assert.That(E.EscortRegistrationBlocked(u), Does.Contain("同行没有空位").Or.Contain("2/2"));
    }

    /// <summary>RC09：三名生成队友都死后，RB01 在收容所随时可办（不等 72 小时周期），30 分钟只建一人。</summary>
    [Test]
    public void RC09_AllDead_Rb01WithoutWaitingCycle()
    {
        var s = AtShelter(4);
        for (var i = 0; i < 3; i++) Recruit(s, i);
        foreach (var r in s.Roster.ToList()) E.DebugActor(s, r.ActorId, "kill");
        Assert.That(E.SharedRoster(s), Is.Empty, "死亡不占名额");
        var cycle = s.Recruit.Cycle;
        Assert.That(E.EscortRegistrationBlocked(s), Is.Null);
        Ok(E.StartEscortRegistration(s, "rb-1"));
        Run(s);
        Assert.That(s.Recruit.Cycle, Is.EqualTo(cycle), "没有等到下一批");
        Assert.That(E.AliveEscorts(s).Count, Is.EqualTo(1));
        Assert.That(E.SharedRoster(s).Count, Is.EqualTo(1));
    }

    /// <summary>RC11：护送员退役时借用矛进认领箱、仍归原借用位；再登记 RB01 不另发矛（只指路到认领箱）；全程木矛实例数不增。</summary>
    [Test]
    public void RC11_RetireEscort_LoanSpearReturnsToSlot_NoNewSpear()
    {
        var s = AtShelter(2);
        Ok(E.StartEscortRegistration(s));
        Run(s);
        var esc = E.AliveEscorts(s).Single();
        var spearId = s.LoanSlots[esc.LoanSlot!];
        Assert.That(s.Items.Count(i => i.ItemId == "wooden_spear"), Is.EqualTo(1));
        Ok(E.Retire(s, esc.ActorId));
        var spear = E.FindItem(s, spearId)!;
        Assert.That(spear.LoanSlot, Is.EqualTo(esc.LoanSlot));
        Assert.That(spear.Owner, Is.EqualTo(ItemInstance.RemainsOwner(esc.ActorId)));
        Assert.That(E.LoanWhere(s, spear), Does.Contain("认领箱"));
        Assert.That(E.ReportLostLoanBlocked(s, esc.LoanSlot!), Does.Contain("可以去取回"), "在认领箱里只指路，不能报失");
        Ok(E.StartEscortRegistration(s));
        Run(s);
        var esc2 = E.AliveEscorts(s).Single();
        Assert.That(esc2.LoanSlot, Is.EqualTo(esc.LoanSlot));
        Assert.That(s.Items.Count(i => i.ItemId == "wooden_spear"), Is.EqualTo(1), "不因再招发第二把");
        Assert.That(s.LoanSlots[esc.LoanSlot!], Is.EqualTo(spearId));
        Assert.That(s.Log.Last(l => l.Text.Contains("登记完成")).Text, Does.Contain("认领箱"));
    }

    /// <summary>RC17：旧档在册生成角色超过现行 K——冻结新增（随机与 RB01），不随机删人；显式退役到合法人数后才恢复。</summary>
    [Test]
    public void RC17_OverK_FreezeNoDelete_RetireToLegal()
    {
        var s = AtShelter(4);
        Recruit(s, 0);
        Recruit(s, 1);
        // 模拟旧档：容量事实只剩 C2（K=1），名册仍有 2 名随机
        s.Progress.Flags.Remove("church_route_record");
        s.Progress.Flags.Remove("academy_safety_record");
        Assert.That(E.SharedRosterCap(s), Is.EqualTo(1));
        var m = SaveSerializer.Deserialize(SaveSerializer.Serialize(s), Cfg);
        Assert.That(m.Roster.Count(r => r.Present), Is.EqualTo(2), "读档不删人");
        var id = m.Recruit.Candidates[2].CandidateId;
        Assert.That(E.RecruitBlocked(m, id), Does.Contain("随机/保底名册2/1").And.Contain("冻结新增").And.Contain("不会随机删人"));
        Assert.That(E.EscortRegistrationBlocked(m), Is.Not.Null);
        Ok(E.Retire(m, m.Roster[0].ActorId));
        Assert.That(E.RecruitBlocked(m, id), Is.EqualTo("随机/保底名册1/1；留守不释放名额"));
        Ok(E.Retire(m, m.Roster[1].ActorId));
        Assert.That(E.RecruitBlocked(m, id), Is.Null);
    }

    /// <summary>RC08：六名固定角色不占随机/保底名额、不被周期替换；RC16：招人不改容量 C（敌人数上限仍按 C）。</summary>
    [Test]
    public void RC08_RC16_FixedRosterSeparate_CapacityUnchanged()
    {
        var s = AtShelter(2);
        foreach (var n in Cfg.Party.NamedCompanions) E.DebugRecruit(s, n.ActorId);
        Assert.That(s.Roster.Count(r => r.Present), Is.EqualTo(6));
        Assert.That(E.SharedRoster(s), Is.Empty);
        Assert.That(E.RecruitBlocked(s, s.Recruit.Candidates[0].CandidateId), Is.Null, "固定角色不占 K");
        Recruit(s, 0);
        Assert.That(E.PartyCapacity(s), Is.EqualTo(2), "招人不改 C");
        Assert.That(E.BattleCapacity(s), Is.EqualTo(2));
        s.GameMinute = s.Recruit.ShelterOpenedAt!.Value + 4320 * 3;
        E.EnsureRecruitCycle(s);
        Assert.That(Cfg.Party.NamedCompanions.All(n => E.RosterOf(s, n.ActorId)!.Present), Is.True, "换批不替换固定角色");
        Assert.That(E.RosterAliveWithPlayer(s), Is.EqualTo(8));
    }

    /// <summary>开发入口（?dev=1“周期招募（B19）”）：到收容所开放、容量 C4（K=3）、拨到本批结束前 5 分钟、拨到下一批；面板视图字段齐全。</summary>
    [Test]
    public void Dev_RecruitmentEntry_AndPanelView()
    {
        var s = New();
        s.SpawnNextCheck = int.MaxValue;
        s.PatrolNextCheck = int.MaxValue;
        Assert.That(E.DebugRecruitment(s, "next"), Does.Contain("还没开放"));
        Assert.That(E.DebugRecruitment(s, "go"), Does.Contain("周期候选开放"));
        Assert.That(E.DebugRecruitment(s, "c4"), Does.Contain("K=3"));
        var next = E.NextRecruitRefresh(s)!.Value;
        E.DebugRecruitment(s, "edge");
        Assert.That(s.GameMinute, Is.EqualTo(next - 5));
        Assert.That(s.Recruit.Cycle, Is.EqualTo(0));
        E.DebugRecruitment(s, "next");
        Assert.That((s.GameMinute, s.Recruit.Cycle), Is.EqualTo((next, (int?)1)));
        var v = E.RecruitPreview(s);
        Assert.That(v.Opened, Is.True);
        Assert.That(v.CycleText, Does.Contain("第2批"));
        Assert.That(v.QuotaText, Does.Contain("随机/保底名册 0/3"));
        Assert.That(v.CostText, Does.Contain("0材料").And.Contain("10分钟"));
        Assert.That(v.Candidates.Count, Is.EqualTo(3));
        Assert.That(v.Candidates.All(c => c.SkillText.Contains("主：") && c.BodyText.Contains("未感染") && c.CombatText.Contains("初始装备与随身：空")), Is.True);
        Assert.That(v.Candidates.All(c => c.Blocked == null), Is.True);
        var svc = E.CityService(Cfg.Party.Recruitment.ServiceId)!;
        Assert.That(E.IsRecruitService(svc.Id), Is.True);
    }

    /// <summary>RC12：随机招入者留守按 Q90 自主照护（只补到 80、不发物、不扣你的库存），病毒不被重置。</summary>
    [Test]
    public void RC12_RandomReserveCare_NoFreeItems()
    {
        var s = AtShelter(2);
        var r = Recruit(s, 0);
        var a = s.Actors[r.ActorId];
        a.Body.Infected = true;
        a.Body.VirusLoad = 30;
        var items = s.Items.Count;
        WaitHalfHours(s, 4);
        Assert.That(a.Stats.Satiety, Is.GreaterThan(40), "公共补给慢慢回升");
        Assert.That(a.Stats.Satiety, Is.LessThanOrEqualTo(80));
        Assert.That(a.Body.VirusLoad, Is.GreaterThanOrEqualTo(30), "照护不清病毒");
        Assert.That(s.Items.Count, Is.EqualTo(items), "不生成物品");
    }
}
