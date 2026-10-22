namespace Survival.Core.Tests;
public class TraceDump : TestBase
{
    [Test, Explicit]
    public void Dump()
    {
        var s = New(1);
        var bot = new SurvivalBot(E, s, SurvivalBot.Route.StreamAndRoots);
        bot.Play(Cfg.Progress.SevenDayCheckMinute);
        File.WriteAllLines(Path.Combine(TestContext.CurrentContext.TestDirectory, "trace.txt"), bot.Trace);
    }
}
