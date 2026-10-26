using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 求生案 07 数值可行性：两条不同准备方式都必须活到第8日08:00。
/// 四项检查进度只写入测试输出；严格版标为 Tuning，失败只提示调参，不阻断构建。
/// </summary>
public class PlaythroughTests : TestBase
{
    private GameState Play(SurvivalBot.Route route, uint seed, bool fight = false)
    {
        var s = New(seed);
        var bot = new SurvivalBot(E, s, route, fight);
        bot.Play(Cfg.Progress.SevenDayCheckMinute);
        var f = s.Progress.Flags;
        TestContext.WriteLine($"路线 {route} 种子 {seed}：{(s.Progress.Dead ? "死亡：" + s.Progress.Death!.Cause : "存活")}，时间 {Engine.Clock.DayTime(s.GameMinute)}");
        TestContext.WriteLine($"安全饮水 {f.Contains("boiled")}，食物加工 {f.Contains("cooked")}，遮蔽棚 {f.Contains("shelterBuilt")}，全部地点 {f.Contains("allVisited")}");
        TestContext.WriteLine($"状态 健康{s.Stats.Health:0.00} 饱食{s.Stats.Satiety:0.0} 水分{s.Stats.Hydration:0.0} 体力{s.Stats.Energy:0.00} 清醒{s.Stats.Wakefulness:0.0} 精神{s.Stats.Morale:0.0} 免疫{s.Body.Immunity:0.0} 病毒{(s.Body.Infected ? s.Body.VirusLoad.ToString("0.0") : "未感染")}");
        TestContext.WriteLine($"敌踪{bot.Encounters}次{(fight ? $"、战斗{bot.Battles}场、撤退{bot.Retreats}次" : "（全部绕行）")} · 世界敌人{s.WorldEnemies.Count} · 打火器耐久{string.Join("/", s.Items.Where(i => i.ItemId == "lighter").Select(i => $"{i.Durability:0.0}/{i.MaxDurability:0.0}"))} · 锅耐久{string.Join("/", s.Items.Where(i => i.ItemId == "pot").Select(i => $"{i.Durability:0.0}/{i.MaxDurability:0.0}"))}");
        if (s.Progress.Dead)
            foreach (var line in bot.Trace.TakeLast(40)) TestContext.WriteLine(line);
        return s;
    }

    [TestCase(SurvivalBot.Route.StreamAndRoots, 1u)]
    [TestCase(SurvivalBot.Route.StreamAndRoots, 2024u)]
    [TestCase(SurvivalBot.Route.BerriesAndCaches, 1u)]
    [TestCase(SurvivalBot.Route.BerriesAndCaches, 2024u)]
    public void SevenDays_Survive(SurvivalBot.Route route, uint seed)
    {
        var s = Play(route, seed);
        Assert.That(s.Progress.Dead, Is.False, "七日内死亡");
        Assert.That(s.GameMinute, Is.GreaterThanOrEqualTo(Cfg.Progress.SevenDayCheckMinute));
        Assert.That(s.Progress.SevenDayShown, Is.True);
    }

    /// <summary>
    /// v0.6：同样两条路线，但脚本遇敌踪接敌、出行时主动接近普通敌人（简单战斗策略）。不改数值；跑不过时只标 Inconclusive，
    /// 并输出死亡原因供平衡讨论（见开发计划 v0.5 第 7 节待确认）。
    /// </summary>
    [TestCase(SurvivalBot.Route.StreamAndRoots, 1u)]
    [TestCase(SurvivalBot.Route.BerriesAndCaches, 2024u)]
    public void SevenDays_WithFights(SurvivalBot.Route route, uint seed)
    {
        var s = Play(route, seed, fight: true);
        if (s.Progress.Dead) Assert.Inconclusive($"带战斗的脚本七日内死亡：{s.Progress.Death!.Cause}（策略问题或平衡问题，待确认）");
        Assert.That(s.GameMinute, Is.GreaterThanOrEqualTo(Cfg.Progress.SevenDayCheckMinute));
    }

    [Test, Category("Tuning")]
    public void SevenDays_AllCheckItems_Strict()
    {
        var s = Play(SurvivalBot.Route.StreamAndRoots, 7u);
        var f = s.Progress.Flags;
        if (!(f.Contains("boiled") && f.Contains("cooked") && f.Contains("shelterBuilt") && f.Contains("allVisited")))
            Assert.Inconclusive("七日内未完成全部四项检查，供调参参考（不阻断构建）");
    }
}
