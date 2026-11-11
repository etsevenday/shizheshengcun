using Newtonsoft.Json.Linq;
using Survival.Core.Config;
using Survival.Core.Engine;

namespace Survival.Core.Tests;

/// <summary>
/// B41a 里程碑 1：警惕配置与上线开关（R05「Q423 警惕逐项定稿」，验收 Q423-A01 的配置部分）。
/// 表值由 tools/import-b41-vigilance.py 从 R05 读入；这里再对着 R05 原表逐格核对一遍，并验证缺种、越界、低于身份下限都是配置错误。
/// </summary>
public class B41aVigilanceTests : TestBase
{
    private static string Root()
    {
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "doc", "策划案"))) dir = dir.Parent;
        Assert.That(dir, Is.Not.Null, "找不到仓库根目录");
        return dir!.FullName;
    }

    /// <summary>R05 Q423 表：敌人ID → 四列（普通常驻、固定战成员、守卫本体、可选强敌）。</summary>
    private static Dictionary<string, int[]> DesignTable()
    {
        var path = Path.Combine(Root(), "doc", "策划案", "系统策划总册 v1.0", "02 参考扩展", "R05 敌人卡、种群刷新与遭遇系统.md");
        var lines = File.ReadAllLines(path).ToList();
        var start = lines.IndexOf("### Q423 警惕逐项定稿");
        Assert.That(start, Is.GreaterThanOrEqualTo(0));
        var table = new Dictionary<string, int[]>();
        for (var i = start + 1; i < lines.Count && !lines[i].StartsWith("### ") && !lines[i].StartsWith("## "); i++)
        {
            if (!lines[i].StartsWith("|")) continue;
            var cells = lines[i].Trim().Trim('|').Split('|').Select(c => c.Trim()).ToArray();
            if (cells.Length != 6 || cells[0] == "敌人ID" || cells[0].StartsWith("-")) continue;
            table[cells[0]] = cells.Skip(2).Select(int.Parse).ToArray();
        }
        return table;
    }

    [Test]
    public void Q423_A01_All42EnemiesMatchDesignTable()
    {
        var design = DesignTable();
        Assert.That(design.Count, Is.EqualTo(42));
        Assert.That(Cfg.Enemies.Select(e => e.Id), Is.EquivalentTo(design.Keys), "42 个敌种全部有表值");
        foreach (var en in Cfg.Enemies)
        {
            var row = design[en.Id];
            Assert.That(en.Vigilance, Is.Not.Null, en.Id);
            Assert.That(new[] { en.Vigilance!.Resident, en.Vigilance.FixedMember, en.Vigilance.GuardianBody, en.Vigilance.OptionalElite }, Is.EqualTo(row), en.Id);
            Assert.That(row, Has.All.InRange(1, 10), en.Id);
            Assert.That(row[1], Is.GreaterThanOrEqualTo(3), en.Id + " 固定战成员至少 3");
            Assert.That(row[2], Is.GreaterThanOrEqualTo(5), en.Id + " 守卫本体至少 5");
            Assert.That(row[3], Is.GreaterThanOrEqualTo(6), en.Id + " 可选强敌至少 6");
        }
    }

    [Test]
    public void Q423_A01_SameSpeciesTakesColumnByIdentity()
    {
        // 同种不同遭遇取对应列
        Assert.That(E.VigilanceOf("shambler", EnemyCardIdentity.Resident), Is.EqualTo(1));
        Assert.That(E.VigilanceOf("shambler", EnemyCardIdentity.FixedMember), Is.EqualTo(3));
        Assert.That(E.VigilanceOf("shambler", EnemyCardIdentity.GuardianBody), Is.EqualTo(5));
        Assert.That(E.VigilanceOf("shambler", EnemyCardIdentity.OptionalElite), Is.EqualTo(6));
        Assert.That(E.VigilanceOf("z_slow", EnemyCardIdentity.FixedMember), Is.EqualTo(3), "检查哨拦路战的迟缓感染者取固定战成员列");
        Assert.That(E.VigilanceOf("z_guard", EnemyCardIdentity.Resident), Is.EqualTo(4));
        Assert.That(E.VigilanceOf("z_guard", EnemyCardIdentity.OptionalElite), Is.EqualTo(6));
        Assert.That(E.VigilanceOf("z_isolation_guard", EnemyCardIdentity.OptionalElite), Is.EqualTo(7));
        Assert.That(E.VigilanceOf("ec_boss_lab", EnemyCardIdentity.GuardianBody), Is.EqualTo(7));
        Assert.That(E.VigilanceOf("wolf", EnemyCardIdentity.Resident), Is.EqualTo(5));
        // 教学首战的游荡尸特例为 1
        Assert.That(Cfg.Story.TutorialBattle.Vigilance, Is.EqualTo(1));
        Assert.That(E.VigilanceOf(Cfg.Story.TutorialBattle.EnemyId, EnemyCardIdentity.Tutorial), Is.EqualTo(1));
    }

    [Test]
    public void Switch_DefaultsOff()
    {
        Assert.That((bool)Raw()["encounters"]!["enemyCardsV2"]!, Is.False, "B41a—B41e 挂在关着的开关后面，B41f 才打开");
        Assert.That(LoadConfig(false).Encounters.EnemyCardsV2, Is.False);
        Assert.That(LoadConfig(true).Encounters.EnemyCardsV2, Is.True);
    }

    private static JObject Raw() => JObject.Parse(File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "config.v0.6.json")));

    private static JObject Enemy(JObject cfg, string id) => (JObject)((JArray)cfg["enemies"]!).First(e => (string?)e["id"] == id);

    [Test]
    public void Config_MissingOrOutOfRange_IsError()
    {
        var missing = Raw();
        Enemy(missing, "runner").Remove("vigilance");
        var ex = Assert.Throws<ConfigException>(() => ConfigLoader.FromJson(missing.ToString()));
        Assert.That(ex!.Errors, Has.Some.Contains("runner").And.Contains("缺少警惕等级"));

        foreach (var bad in new[] { 0, 11 })
        {
            var over = Raw();
            Enemy(over, "runner")["vigilance"]!["resident"] = bad;
            ex = Assert.Throws<ConfigException>(() => ConfigLoader.FromJson(over.ToString()));
            Assert.That(ex!.Errors, Has.Some.Contains("runner").And.Contains("1—10"));
        }

        foreach (var (key, floor) in new[] { ("fixedMember", 3), ("guardianBody", 5), ("optionalElite", 6) })
        {
            var low = Raw();
            Enemy(low, "shambler")["vigilance"]![key] = floor - 1;
            ex = Assert.Throws<ConfigException>(() => ConfigLoader.FromJson(low.ToString()));
            Assert.That(ex!.Errors, Has.Some.Contains("shambler").And.Contains($"不能低于 {floor}"));
        }

        var tut = Raw();
        tut["story"]!["tutorialBattle"]!["vigilance"] = 0;
        ex = Assert.Throws<ConfigException>(() => ConfigLoader.FromJson(tut.ToString()));
        Assert.That(ex!.Errors, Has.Some.Contains("首战敌人的警惕等级"));
    }
}
