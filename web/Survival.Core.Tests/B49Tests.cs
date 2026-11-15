using System.Reflection;
using Newtonsoft.Json.Linq;
using Survival.Core.Config;
using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 批次 B49（需求 R240、R261；R04 文首 Q652「多途径病毒暴露与同伴不自然增殖」与「B49 Q739—Q748」，exposure-profiles-q652.json）：
/// 五类暴露配置与校验、真实正伤后的暴露判定、同伴自然正增长 0、静态途径／条件预估／三态结果与喷溅拼接、旧档。
/// </summary>
public class B49Tests : BattleTestBase
{
    protected override bool NoEnemyApproach => true;

    private static JObject Contract() =>
        JObject.Parse(File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "design.exposure-profiles-q652.json")));

    private static string ConfigJson() => File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "config.v0.6.json"));

    // ───────────── M974 配置与校验（Q652-A01） ─────────────

    /// <summary>Q652-A01：五类倍率／首次／再次值与合同一致，按原 profile 表序；咬伤沿原 20／10。</summary>
    [Test]
    public void Q652_A01_Profiles_MatchContract()
    {
        var c = Contract();
        var order = c["q739ToQ748"]!["staticTypes"]!["order"]!.Select(x => (string)x!).ToList();
        Assert.That(Cfg.Infection.ExposureProfiles.Select(p => p.Id), Is.EqualTo(order));
        foreach (var p in Cfg.Infection.ExposureProfiles)
        {
            var j = c["profiles"]![p.Id]!;
            Assert.That((p.Name, p.Factor, p.FirstLoad, p.RepeatDelta),
                Is.EqualTo(((string)j["name"]!, (double)j["factor"]!, (double)j["firstLoad"]!, (double)j["repeatDelta"]!)), p.Id);
        }
        var bite = Cfg.Infection.ExposureProfiles.Single(p => p.Id == "bite");
        Assert.That((bite.FirstLoad, bite.RepeatDelta), Is.EqualTo((Cfg.Infection.OnFirstSuccessfulExposure.Load, Cfg.Infection.OnRepeatedSuccessfulExposure.LoadDelta)));
    }

    /// <summary>Q652-A01：42 敌种每个 opening／cycle 步骤、咬击替换、普通回退与死亡喷溅都与合同逐项一致。</summary>
    [Test]
    public void Q652_A01_EnemySteps_MatchContract()
    {
        var c = Contract();
        var list = (JArray)c["enemyProfiles"]!;
        Assert.That(list.Count, Is.EqualTo(42));
        Assert.That(Cfg.Enemies.Count, Is.EqualTo(42));
        foreach (var j in list)
        {
            var en = Cfg.Enemies.Single(x => x.Id == (string)j["enemyId"]!);
            Assert.That(en.DefaultPhysicalExposure, Is.EqualTo((string)j["defaultPhysicalExposure"]!), en.Id);
            Assert.That(en.DeathSplashExposure, Is.EqualTo((string)j["deathSplashExposure"]!), en.Id);
            foreach (var (part, steps) in new[] { ("opening", en.Intents.Opening), ("cycle", en.Intents.Cycle) })
            {
                var js = (JArray)j[part]!;
                Assert.That(steps.Count, Is.EqualTo(js.Count), $"{en.Id}.{part}");
                for (var i = 0; i < js.Count; i++)
                {
                    Assert.That(steps[i].Kind, Is.EqualTo((string)js[i]["kind"]!), $"{en.Id}.{part}[{i}]");
                    Assert.That(steps[i].Exposure, Is.EqualTo((string)js[i]["exposure"]!), $"{en.Id}.{part}[{i}]");
                    Assert.That(steps[i].BiteReplacementExposure, Is.EqualTo((string?)js[i]["biteReplacementExposure"]), $"{en.Id}.{part}[{i}]");
                }
            }
        }
    }

    /// <summary>Q652-A01：当前普通人类、普通动物全部步骤与回退都是 none；纯控制 0 伤步骤（攀附感染者拉扯）为 none。</summary>
    [Test]
    public void Q652_A01_HumanAnimal_AllNone()
    {
        foreach (var en in Cfg.Enemies.Where(x => !x.IsZombie))
        {
            Assert.That(en.DefaultPhysicalExposure, Is.EqualTo("none"), en.Id);
            Assert.That(en.DeathSplashExposure, Is.EqualTo("none"), en.Id);
            Assert.That(en.Intents.Opening.Concat(en.Intents.Cycle).All(st => st.Exposure == "none" && st.BiteReplacementExposure == null), Is.True, en.Id);
        }
        var pull = Cfg.Enemies.Single(x => x.Id == "z_clinger").Intents.Cycle.Single(x => x.Kind == "pull");
        Assert.That((pull.Factor <= 0, pull.Exposure), Is.EqualTo((true, "none")));
    }

    /// <summary>Q652-A01：新增敌种／步骤缺映射时配置校验能定位到敌种与步骤，不静默当 none。</summary>
    [Test]
    public void Q652_A01_MissingMapping_ValidationLocates()
    {
        var j = JObject.Parse(ConfigJson());
        var shambler = (JObject)((JArray)j["enemies"]!).First(x => (string?)x["id"] == "shambler");
        ((JObject)shambler["intents"]!["cycle"]![0]!).Remove("exposure");
        var guard = (JObject)((JArray)j["enemies"]!).First(x => (string?)x["id"] == "z_power_guard");
        guard.Remove("defaultPhysicalExposure");
        var spit = (JObject)((JArray)j["enemies"]!).First(x => (string?)x["id"] == "z_spitter");
        spit["intents"]!["cycle"]![0]!["exposure"] = "acid";
        var ex = Assert.Throws<ConfigException>(() => ConfigLoader.FromJson(j.ToString()))!;
        Assert.That(ex.Errors, Has.Some.Contains("shambler").And.Contains("cycle[0]"));
        Assert.That(ex.Errors, Has.Some.Contains("z_power_guard").And.Contains("defaultPhysicalExposure"));
        Assert.That(ex.Errors, Has.Some.Contains("z_spitter").And.Contains("acid"));
    }

    /// <summary>咬伤表值改了也报错（咬伤须沿原概率与首次／再次量）。</summary>
    [Test]
    public void Q652_A01_BiteProfile_MustKeepOriginal()
    {
        var j = JObject.Parse(ConfigJson());
        var bite = ((JArray)j["infection"]!["exposureProfiles"]!).First(x => (string?)x["id"] == "bite");
        bite["firstLoad"] = 15;
        var ex = Assert.Throws<ConfigException>(() => ConfigLoader.FromJson(j.ToString()))!;
        Assert.That(ex.Errors, Has.Some.Contains("咬伤"));
    }
}
