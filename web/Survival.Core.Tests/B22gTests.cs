using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using Survival.Core.Config;
using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 批次 B22g「招募身份与安全屋候补」（Q180、Q181、Q183；需求 R179、R180、R182—R186）。
/// 规则来源：02 待确认 Q180/Q181/Q183 策划答复；assets/20261103/party-combat-expedition-adjudications.json identity、shelter、rules；
/// 各策划案「Q179—Q207 本节相关裁定」。验收 PLAN-Q180-01/02、PLAN-Q181-01、PLAN-Q183-01/02。
/// </summary>
public class B22gTests : TestBase
{
    private const string Shelter = "h06_shelter";

    private static JObject Pce() => JObject.Parse(File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "design.pce.json")));

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

    // ───────────── R179 身份（Q180 选 C） ─────────────

    /// <summary>PLAN-Q180-01（DesignSync）：config.party.recruitment.identity 与 identity 合同逐项一致（32×32、性别3、肤色5、发型6、发色4、面部6）。</summary>
    [Category("DesignSync")]
    [Test]
    public void PLAN_Q180_01_IdentityConfig_FromContract()
    {
        var d = (JObject)Pce()["identity"]!;
        var id = Cfg.Party.Recruitment.Identity;
        Assert.That(id.Surnames, Is.EqualTo(d["surnames"]!.Values<string>()));
        Assert.That(id.GivenNames, Is.EqualTo(d["givenNames"]!.Values<string>()));
        Assert.That(id.Genders.Select(g => (g.Id, g.Name)), Is.EqualTo(((JArray)d["gender"]!).Select(g => ((string)g["id"]!, (string)g["name"]!))));
        Assert.That(id.SkinTones, Is.EqualTo(d["skinTone"]!.Values<string>()));
        Assert.That(id.HairStyles, Is.EqualTo(d["hairStyle"]!.Values<string>()));
        Assert.That(id.HairColors, Is.EqualTo(d["hairColor"]!.Values<string>()));
        Assert.That(id.FaceDetails, Is.EqualTo(d["faceDetail"]!.Values<string>()));
        Assert.That((id.Surnames.Count, id.GivenNames.Count, id.Genders.Count, id.SkinTones.Count, id.HairStyles.Count, id.HairColors.Count, id.FaceDetails.Count),
            Is.EqualTo((32, 32, 3, 5, 6, 4, 6)));
        Assert.That((id.LegacyGender, id.LegacyAppearance), Is.EqualTo(("未说明", "未记录")), "identity oldSaves：性别显示未说明、外观显示未记录");
    }

    /// <summary>
    /// PLAN-Q180-01：新候选每人有池内的姓+名、性别、肤色、发型、发色、面部细节，显示“姓名·004”（至少三位）；候选卡与名册显示姓名、性别、外观摘要；
    /// 招入后名册与角色名沿用候选名，字段原样复制；固定六人与 RB01 护送员不显示新身份行（既有名称不改）。
    /// </summary>
    [Test]
    public void PLAN_Q180_01_NewCandidates_NamePoolGenderAppearance_CopiedToRoster()
    {
        var s = AtShelter();
        var id = Cfg.Party.Recruitment.Identity;
        foreach (var c in s.Recruit.Candidates)
        {
            Assert.That(c.Seq, Is.Not.Null);
            var m = Regex.Match(c.Name, @"^(.)(..)·(\d{3,})$");
            Assert.That(m.Success, Is.True, c.Name);
            Assert.That(id.Surnames, Does.Contain(m.Groups[1].Value));
            Assert.That(id.GivenNames, Does.Contain(m.Groups[2].Value));
            Assert.That(int.Parse(m.Groups[3].Value), Is.EqualTo(c.Seq));
            Assert.That(id.Genders.Select(g => g.Id), Does.Contain(c.Gender));
            Assert.That(id.SkinTones, Does.Contain(c.SkinTone));
            Assert.That(id.HairStyles, Does.Contain(c.HairStyle));
            Assert.That(id.HairColors, Does.Contain(c.HairColor));
            Assert.That(id.FaceDetails, Does.Contain(c.FaceDetail));
        }
        var v = E.RecruitPreview(s).Candidates[0];
        var c0 = s.Recruit.Candidates[0];
        Assert.That(v.IdentityText, Is.EqualTo($"性别：{id.GenderName(c0.Gender)} · 外观：{c0.SkinTone}，{c0.HairColor}{c0.HairStyle}，{c0.FaceDetail}"));
        Assert.That(v.SkillText, Does.Contain("主："), "职业/熟练数值照常公开");

        Ok(E.StartRecruit(s, c0.CandidateId, RecruitPlacement.Square));
        Run(s);
        var r = s.Roster.Single(x => x.IsRandom);
        Assert.That((r.Name, s.Actors[r.ActorId].Name), Is.EqualTo((c0.Name, c0.Name)));
        Assert.That((r.Gender, r.SkinTone, r.HairStyle, r.HairColor, r.FaceDetail), Is.EqualTo((c0.Gender, c0.SkinTone, c0.HairStyle, c0.HairColor, c0.FaceDetail)));
        Assert.That(E.IdentityText(r), Is.EqualTo(v.IdentityText));

        E.DebugRecruit(s, "npc_gu_cheng");
        Assert.That(E.IdentityText(E.RosterOf(s, "npc_gu_cheng")!), Is.Null, "固定角色既有名称/资料不改，不显示新身份行");
    }

    /// <summary>
    /// PLAN-Q180-02：模板/身体/人物档随机序列不受外观池影响（换一套身份池，数值逐项相同、名字不同）；同周期进出/读档不重抽；
    /// 序号全局递增不重复（跨 40 期 120 人）；性别与肤色不改属性（同一份数值只是换了外观）。
    /// </summary>
    [Test]
    public void PLAN_Q180_02_NumbersIndependent_NoReroll_SeqUnique()
    {
        static string Numbers(RecruitCandidate c) =>
            $"{c.TemplateId}|{c.MainSkill}{c.MainLevel}|{c.SubSkill}{c.SubLevel}|{c.Health}|{c.Energy}|{c.Wakefulness}|{c.Morale}|{c.Immunity}|{c.ControlResist}|{c.Evasion}|{c.BaseArmor}|{c.BaseSpeed}";

        var s = AtShelter(seed: 777);
        var cfg2 = ConfigLoader.FromJson(File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "config.v0.6.json")));
        var id2 = cfg2.Party.Recruitment.Identity;
        id2.Surnames.Reverse();
        id2.GivenNames.Reverse();
        id2.Genders.Reverse();
        id2.SkinTones.Reverse();
        id2.HairStyles.Reverse();
        id2.FaceDetails.Reverse();
        var e2 = new GameEngine(cfg2);
        var t = e2.NewGame(777);
        e2.DebugLegacyStory(t);
        t.SpawnNextCheck = int.MaxValue;
        t.PatrolNextCheck = int.MaxValue;
        t.GameMinute = At(1, 10, 0);
        e2.DebugRecruitment(t, "go");
        Assert.That(t.Recruit.Candidates.Select(Numbers), Is.EqualTo(s.Recruit.Candidates.Select(Numbers)), "外观池变了，数值序列不变");
        Assert.That(t.Recruit.Candidates.Select(c => c.Name), Is.Not.EqualTo(s.Recruit.Candidates.Select(c => c.Name)));

        // 同周期进出、看面板、读档都不重抽
        var snap = JArray.FromObject(s.Recruit.Candidates).ToString();
        E.DebugMapTeleport(s, "h02_square");
        E.DebugMapTeleport(s, Shelter);
        E.EnsureRecruitCycle(s);
        _ = E.RecruitPreview(s);
        var m = SaveSerializer.Deserialize(SaveSerializer.Serialize(s), Cfg);
        E.EnsureRecruitCycle(m);
        Assert.That(JArray.FromObject(m.Recruit.Candidates).ToString(), Is.EqualTo(snap));

        // 跨 40 期：序号全局递增不重复；各字段都在池内
        var seqs = new List<int>();
        var start = s.Recruit.ShelterOpenedAt!.Value;
        for (var k = 0; k < 40; k++)
        {
            s.GameMinute = start + 4320 * k + 1;
            E.EnsureRecruitCycle(s);
            seqs.AddRange(s.Recruit.Candidates.Select(c => c.Seq!.Value));
            Assert.That(s.Recruit.Candidates.All(c => c.Name.EndsWith("·" + c.Seq!.Value.ToString("000"))), Is.True);
        }
        Assert.That(seqs.Distinct().Count(), Is.EqualTo(seqs.Count), "序号不重复");
        Assert.That(seqs, Is.Ordered);
    }

    /// <summary>identity seedRule：序号超过 999 不截断、不循环（第 1000 号显示“·1000”）。</summary>
    [Test]
    public void PLAN_Q180_02_SeqAbove999_NotTruncated()
    {
        var s = AtShelter();
        s.Recruit.NextNameSeq = 999;
        s.GameMinute = s.Recruit.ShelterOpenedAt!.Value + 4320 + 1;
        E.EnsureRecruitCycle(s);
        Assert.That(s.Recruit.Candidates.Select(c => c.Name.Split('·')[1]), Is.EqualTo(new[] { "999", "1000", "1001" }));
    }

    /// <summary>
    /// 34→35 迁移（identity oldSaves）：已生成的旧候选与已招随机角色保留原显示名、数值与 actorId，性别显示“未说明”、外观显示“未记录”，不补抽；
    /// 下一批候选用新池；日志一条；幂等。
    /// </summary>
    [Test]
    public void PLAN_Q180_Migration34To35_OldNamesKept_Undisclosed_NotRecorded()
    {
        var s = AtShelter(c: 3);
        Ok(E.StartRecruit(s, s.Recruit.Candidates[0].CandidateId, RecruitPlacement.Square));
        Run(s);
        var j = JObject.Parse(SaveSerializer.Serialize(s));
        j["SchemaVersion"] = "34";
        // 旧档的候选与名册：B19 中性名、没有身份字段
        var cands = (JArray)j["Recruit"]!["Candidates"]!;
        for (var i = 0; i < cands.Count; i++)
        {
            var c = (JObject)cands[i];
            foreach (var f in new[] { "Seq", "Gender", "SkinTone", "HairStyle", "HairColor", "FaceDetail" }) c.Remove(f);
            c["Name"] = $"幸存者·旧模板{i + 1:000}";
        }
        var rj = (JObject)((JArray)j["Roster"]!).First(x => (string)x["Kind"]! == "random");
        foreach (var f in new[] { "Gender", "SkinTone", "HairStyle", "HairColor", "FaceDetail" }) rj.Remove(f);
        rj["Name"] = "幸存者·旧模板001";
        var aid = (string)rj["ActorId"]!;
        j["Actors"]![aid]!["Name"] = "幸存者·旧模板001";
        var hp = s.Actors[aid].Stats.Health;
        var speed = s.Roster.Single(x => x.IsRandom).BaseSpeed;

        var t = SaveSerializer.Deserialize(j.ToString(), Cfg);
        Assert.That(t.SchemaVersion, Is.EqualTo(GameState.CurrentSchema));
        Assert.That(t.Log.Last().Text, Does.Contain("存档已升级到版本35").And.Contain("未说明").And.Contain("未记录"));
        var r = t.Roster.Single(x => x.IsRandom);
        Assert.That((r.ActorId, r.Name, r.BaseSpeed, t.Actors[aid].Stats.Health), Is.EqualTo((aid, "幸存者·旧模板001", speed, hp)), "名字、数值与 actorId 不变");
        Assert.That(E.IdentityText(r), Is.EqualTo("性别：未说明 · 外观：未记录"));
        Assert.That(E.RecruitPreview(t).Candidates.Select(c => (c.Name, c.IdentityText)),
            Is.EqualTo(new[] { ("幸存者·旧模板001", "性别：未说明 · 外观：未记录"), ("幸存者·旧模板002", "性别：未说明 · 外观：未记录"), ("幸存者·旧模板003", "性别：未说明 · 外观：未记录") }),
            "同周期旧候选不补抽");
        // 下一批起用新池
        t.GameMinute = t.Recruit.ShelterOpenedAt!.Value + 4320 + 1;
        E.EnsureRecruitCycle(t);
        Assert.That(t.Recruit.Candidates.All(c => c.Gender != null && c.Name.Contains('·') && !c.Name.StartsWith("幸存者")), Is.True);
        var again = SaveSerializer.Deserialize(SaveSerializer.Serialize(t), Cfg);
        Assert.That(again.Log.Count(x => x.Text.Contains("存档已升级到版本35")), Is.EqualTo(1), "幂等");
    }

    // ───────────── R182—R184 登记后去向、0 分钟送达与待安置确认（Q183） ─────────────

    private const string Homestead = "h05_homestead";

    /// <summary>在营外安全屋地块建成一间安全屋（开发入口：实际到访、探索 100、直接建成），再回到收容所。</summary>
    private void BuildSafehouseAndReturn(GameState s, string loc = Homestead)
    {
        E.DebugSafehouseSite(s, loc);
        Assert.That(E.DebugBuildSafehouse(s), Does.Contain("直接建成"));
        E.DebugMapTeleport(s, Shelter);
        Assert.That(s.CurrentLocation, Is.EqualTo(Shelter));
    }

    private RosterEntry Recruited(GameState s) => s.Roster.Last(r => r.IsRandom);

    /// <summary>
    /// PLAN-Q183-01/02（selection）：确认前必须明确二选一；同行人数 &lt; C 时“编入同行”可选并直接入队；满员时该选项禁用写真实原因（不挤出他人），
    /// 开工被拒且不推进时间；候补下拉总有营区广场。
    /// </summary>
    [Test]
    public void PLAN_Q183_01_ChoiceRequired_PartyWhenRoom_FullDisabled()
    {
        var s = AtShelter();
        var c = s.Recruit.Candidates[0];
        var minute = s.GameMinute;
        var none = E.StartRecruit(s, c.CandidateId, null);
        Assert.That(none.Ok, Is.False);
        Assert.That(none.Reason, Does.Contain("先选择登记后去向"));
        var v = E.RecruitPreview(s);
        Assert.That(v.PartyOptionBlocked, Is.Null, "同行 1/2 有空位");
        Assert.That(v.ReserveDestinations.Select(d => d.Key), Is.EqualTo(new[] { "square" }), "没有安全屋时只有营区广场");
        Assert.That(v.ReserveDestinations[0].Blocked, Is.Null);

        Ok(E.StartRecruit(s, c.CandidateId, RecruitPlacement.Party));
        Run(s);
        var r = Recruited(s);
        Assert.That(s.Party, Does.Contain(r.ActorId), "选同行：完成即编入同行");
        Assert.That(s.GameMinute, Is.EqualTo(minute + 10), "只走登记的 10 分钟");
        Assert.That(s.Log.Last().Text, Does.Contain("编入同行（2/2）"));

        // C3 时同行 3/3 满：先占满同行
        var t = AtShelter(c: 3);
        E.DebugRecruit(t, "npc_gu_cheng");
        E.DebugRecruit(t, "npc_shen_he");
        Assert.That(t.Party.Count, Is.EqualTo(3));
        var vt = E.RecruitPreview(t);
        Assert.That(vt.PartyOptionBlocked, Does.Contain("同行已满 3/3").And.Contain("不能挤出别人"));
        var m2 = t.GameMinute;
        var full = E.StartRecruit(t, t.Recruit.Candidates[0].CandidateId, RecruitPlacement.Party);
        Assert.That(full.Ok, Is.False);
        Assert.That(full.Reason, Does.Contain("同行已满"));
        Assert.That(t.GameMinute, Is.EqualTo(m2), "被拒不推进时间");
        Assert.That(t.Main, Is.Null);
        Assert.That(t.Recruit.Pending, Is.Null, "不锁名额");
        Assert.That(t.Party.Count, Is.EqualTo(3), "不挤出他人");
        Ok(E.StartRecruit(t, t.Recruit.Candidates[0].CandidateId, RecruitPlacement.Square));
        Run(t);
        Assert.That(t.Party, Does.Not.Contain(Recruited(t).ActorId));
        Assert.That(Recruited(t).ReserveSafehouse, Is.Null, "广场候补");
    }

    /// <summary>
    /// PLAN-Q183-01/02（selection/arrival）：已完工、已到访、从收容所有已知可通路线的自有安全屋可选，登记完成 0 分钟送到屋内作候补（不走路途、不额外花时间、
    /// 主队不移动、不带任何物品、柜子不变）；没建成/没到访/从收容所无已知通路的屋禁选并写原因，开工被拒。
    /// </summary>
    [Test]
    public void PLAN_Q183_02_SafehouseDestination_ZeroMinuteArrival_IneligibleDisabled()
    {
        var s = AtShelter();
        Assert.That(E.RecruitSafehouseBlocked(s, Homestead), Does.Contain("没有建成的安全屋"));
        BuildSafehouseAndReturn(s);
        var v = E.RecruitPreview(s);
        var d = v.ReserveDestinations.Single(x => x.Key == "sh:" + Homestead);
        Assert.That(d.Blocked, Is.Null, d.Blocked);
        Assert.That(d.Label, Does.Contain("营外安全屋地块安全屋屋内").And.Contain("0分钟送达"));

        var items = JArray.FromObject(s.Items).ToString();
        var minute = s.GameMinute;
        var c = s.Recruit.Candidates[1];
        Ok(E.StartRecruit(s, c.CandidateId, GameEngine.PlacementFromKey(d.Key)));
        Assert.That(s.Recruit.Pending!.Placement!.Safehouse, Is.EqualTo(Homestead), "开工快照锁目的地");
        Run(s);
        var r = Recruited(s);
        Assert.That(r.ReserveSafehouse, Is.EqualTo(Homestead));
        Assert.That(s.Party, Does.Not.Contain(r.ActorId));
        Assert.That(E.ReserveSafehouseOf(s, r.ActorId), Is.EqualTo(Homestead));
        Assert.That(s.GameMinute, Is.EqualTo(minute + 10), "0 分钟送达：只走登记 10 分钟");
        Assert.That(s.CurrentLocation, Is.EqualTo(Shelter), "主队不动");
        Assert.That(JArray.FromObject(s.Items).ToString(), Is.EqualTo(items), "不带走任何物品、不动屋柜");
        Assert.That(s.Log.Last().Text, Does.Contain("0分钟送到营外安全屋地块安全屋屋内作候补"));
        Assert.That(E.RecruitPreview(s).Retirables.Single(x => x.ActorId == r.ActorId).WhereText, Is.EqualTo("候补（营外安全屋地块安全屋屋内）"));

        // 没实际到访：禁选
        s.Locations[Homestead].Visited = false;
        Assert.That(E.RecruitSafehouseBlocked(s, Homestead), Does.Contain("还没实际到访"));
        s.Locations[Homestead].Visited = true;
        // 从收容所没有已知可通路线（中间的营区广场不在已知地图上）：禁选
        s.Locations["h02_square"].Discovered = false;
        Assert.That(E.RecruitSafehouseBlocked(s, Homestead), Does.Contain("没有已知可通的路线"));
        var why = E.StartRecruit(s, s.Recruit.Candidates[2].CandidateId, RecruitPlacement.AtSafehouse(Homestead));
        Assert.That(why.Ok, Is.False);
        Assert.That(E.RecruitPreview(s).ReserveDestinations.Single(x => x.Key == "sh:" + Homestead).Blocked, Does.Contain("没有已知可通的路线"));
        Assert.That(E.RecruitPreview(s).ReserveDestinations[0].Blocked, Is.Null, "无合格屋仍可选广场");
    }

    /// <summary>
    /// R184（confirm）：完成重验失败（同行空位没了 / 该屋不再合法可达）时停在“待安置确认”：不建人、不重收 10 分钟、名额仍锁、读档保留；
    /// 期间不能另登记；只能改选合法去向（0 分钟建一人，requestId 幂等）或取消（不发人不发物，已过时间不退，名额释放、候选回到可招）。
    /// </summary>
    [Test]
    public void PLAN_Q183_02_RevalidateFail_AwaitingPlacement_ResolveOrCancel()
    {
        var s = AtShelter(c: 3);
        var c = s.Recruit.Candidates[0];
        Ok(E.StartRecruit(s, c.CandidateId, RecruitPlacement.Party));
        E.Advance(s);
        // 期间同行被占满（开发入口模拟）
        E.DebugRecruit(s, "npc_gu_cheng");
        E.DebugRecruit(s, "npc_shen_he");
        Run(s);
        var actors = s.Actors.Count;
        Assert.That(s.Recruit.Pending, Is.Not.Null);
        Assert.That(s.Recruit.Pending!.AwaitingPlacement, Is.True);
        Assert.That(s.Recruit.Pending.PlacementFailure, Does.Contain("同行已满"));
        Assert.That(s.Roster.Any(r => r.IsRandom), Is.False, "不建人");
        Assert.That(s.Log.Last().Text, Does.Contain("待安置确认"));
        Assert.That(E.SharedRosterLocked(s), Is.EqualTo(1), "名额仍锁");
        var v = E.RecruitPreview(s);
        Assert.That(v.Awaiting, Is.Not.Null);
        Assert.That((v.Awaiting!.CandidateName, v.Awaiting.ChoiceText), Is.EqualTo((c.Name, "编入同行")));
        Assert.That(v.Awaiting.Blocked, Is.Null);
        Assert.That(E.RecruitBlocked(s, s.Recruit.Candidates[1].CandidateId), Does.Contain("等待安置确认"));

        // 读档、世界推进都保留
        s = SaveSerializer.Deserialize(SaveSerializer.Serialize(s), Cfg);
        E.EnsureRecruitCycle(s);
        Assert.That(s.Recruit.Pending?.AwaitingPlacement, Is.True);

        // 不在收容所不能处理
        E.DebugMapTeleport(s, "h02_square");
        ReadDialogues(s);
        Assert.That(E.ResolveRecruitPlacement(s, RecruitPlacement.Square).Reason, Does.Contain("只能在收容所"));
        E.DebugMapTeleport(s, Shelter);
        ReadDialogues(s);
        // 改选不合法：同行仍满
        Assert.That(E.ResolveRecruitPlacement(s, RecruitPlacement.Party).Reason, Does.Contain("同行已满"));
        var minute = s.GameMinute;
        const string req = "resolve-1";
        Ok(E.ResolveRecruitPlacement(s, RecruitPlacement.Square, req));
        Assert.That(E.ResolveRecruitPlacement(s, RecruitPlacement.Square, req).Duplicate, Is.True);
        Assert.That(s.GameMinute, Is.EqualTo(minute), "0 分钟、不重收 10 分钟");
        Assert.That(s.Roster.Count(r => r.IsRandom), Is.EqualTo(1), "只建一人");
        Assert.That(s.Actors.Count, Is.EqualTo(actors + 1));
        Assert.That(Recruited(s).Name, Is.EqualTo(c.Name), "建的是锁定那名候选");
        Assert.That(s.Recruit.Pending, Is.Null);
        Assert.That(s.Recruit.Candidates.Single(x => x.CandidateId == c.CandidateId).Status, Is.EqualTo(RecruitCandidate.Recruited));

        // 取消：安全屋目的地在完成前失效（从收容所不再有已知通路）
        var t = AtShelter();
        BuildSafehouseAndReturn(t);
        var c2 = t.Recruit.Candidates[1];
        Ok(E.StartRecruit(t, c2.CandidateId, RecruitPlacement.AtSafehouse(Homestead)));
        E.Advance(t);
        t.Locations["h02_square"].Discovered = false;
        Run(t);
        Assert.That(t.Recruit.Pending!.AwaitingPlacement, Is.True);
        Assert.That(t.Recruit.Pending.PlacementFailure, Does.Contain("没有已知可通的路线"));
        var m3 = t.GameMinute;
        var items = t.Items.Count;
        Ok(E.CancelRecruitPlacement(t));
        Assert.That(t.Recruit.Pending, Is.Null);
        Assert.That((t.GameMinute, t.Items.Count), Is.EqualTo((m3, items)), "不发人不发物、时间不退");
        Assert.That(t.Roster.Any(r => r.IsRandom), Is.False);
        Assert.That(t.Recruit.Candidates.Single(x => x.CandidateId == c2.CandidateId).Status, Is.EqualTo(RecruitCandidate.Available), "候选回到可招、同一人");
        Assert.That(E.SharedRosterLocked(t), Is.EqualTo(0), "名额释放");
        Assert.That(t.Log.Last().Text, Does.Contain("已取消").And.Contain("时间不退"));
    }

    /// <summary>R184（confirm）：待安置确认跨过换批仍保留已锁候选的身份与数值，改选后按快照建人。</summary>
    [Test]
    public void PLAN_Q183_02_AwaitingAcrossCycle_KeepsLockedCandidate()
    {
        var s = AtShelter(c: 3);
        E.DebugRecruit(s, "npc_gu_cheng");
        var c = s.Recruit.Candidates[2];
        Ok(E.StartRecruit(s, c.CandidateId, RecruitPlacement.Party));
        E.Advance(s);
        E.DebugRecruit(s, "npc_shen_he");
        Run(s);
        Assert.That(s.Recruit.Pending!.AwaitingPlacement, Is.True);
        s.GameMinute = s.Recruit.ShelterOpenedAt!.Value + 4320 + 1;
        E.EnsureRecruitCycle(s);
        Assert.That(s.Recruit.Cycle, Is.EqualTo(1));
        Assert.That(s.Recruit.Pending.AwaitingPlacement, Is.True, "换批不释放");
        Ok(E.ResolveRecruitPlacement(s, RecruitPlacement.Square));
        var r = Recruited(s);
        Assert.That((r.Name, r.BaseSpeed, r.Gender), Is.EqualTo((c.Name, c.BaseSpeed, c.Gender)));
    }

    /// <summary>shelter.compatibility：旧档在办登记没有去向选择，完成时默认营区广场候补、不自动入队（即使同行有空位）。</summary>
    [Test]
    public void PLAN_Q183_02_LegacyPendingWithoutChoice_DefaultsToSquareReserve()
    {
        var s = AtShelter();
        Ok(E.StartRecruit(s, s.Recruit.Candidates[0].CandidateId, RecruitPlacement.Party));
        E.Advance(s);
        var j = JObject.Parse(SaveSerializer.Serialize(s));
        j["SchemaVersion"] = "34";
        ((JObject)j["Recruit"]!["Pending"]!).Remove("Placement");
        var t = SaveSerializer.Deserialize(j.ToString(), Cfg);
        Assert.That(t.Recruit.Pending!.Placement, Is.Null);
        Run(t);
        var r = t.Roster.Single(x => x.IsRandom);
        Assert.That(t.Party, Does.Not.Contain(r.ActorId), "旧在办登记不自动入队");
        Assert.That(r.ReserveSafehouse, Is.Null, "默认营区广场候补");
    }
}
