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

        Ok(E.StartRecruit(s, c0.CandidateId));
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
        Ok(E.StartRecruit(s, s.Recruit.Candidates[0].CandidateId));
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
}
