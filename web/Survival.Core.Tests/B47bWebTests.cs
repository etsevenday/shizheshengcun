namespace Survival.Core.Tests;

/// <summary>
/// B47b 战斗表现：网页壳部分（源码断言）。R03 文首 B34 §Q617／§Q618／§Q619，R09 文首 B47b Q690—Q697，S14 同批；开发记录 B47b。
/// </summary>
public class B47bWebTests
{
    private static string Root()
    {
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "web", "Survival.sln"))) dir = dir.Parent;
        Assert.That(dir, Is.Not.Null, "找到仓库根目录");
        return dir!.FullName;
    }

    private static string Web(params string[] p) => File.ReadAllText(Path.Combine(new[] { Root(), "web", "Survival.Web" }.Concat(p).ToArray()));

    /// <summary>Q617-A01／A02：播放中战斗画面常驻显示读显示快照，到结果帧才追齐；全记录读真实本场记录；读档清快照。</summary>
    [Test]
    public void Q617_A01_BattleScreen_ReadsViewState_FullLogStaysLive()
    {
        var pb = Web("Services", "GameSession.Playback.cs");
        Assert.That(pb, Does.Contain("public GameState VS =>"));
        Assert.That(pb, Does.Contain("FxResultFrame = 0.33"), "与 battle.css 结果字 33% 同一时刻");
        Assert.That(pb, Does.Contain("elapsed >= total * FxResultFrame"));
        var reset = pb[pb.IndexOf("private void ResetPlayback()")..];
        Assert.That(reset[..reset.IndexOf("}")], Does.Contain("FxView = null"), "读档／新局清显示快照");
        var gs = Web("Services", "GameSession.cs");
        Assert.That(gs, Does.Contain("if (res.Ok && !res.Duplicate) PrimeFxView(fx);"), "首次重绘前先定显示快照");
        Assert.That(gs, Does.Contain("PrimeFxView(fx);   // B47b（Q617）"));
        var bs = Web("Components", "BattleScreen.razor");
        Assert.That(bs, Does.Contain("var s = G.VS;"));
        Assert.That(bs, Does.Contain("@FullLog(liveB)"), "全记录提交即追加");
        Assert.That(bs, Does.Contain("LogSubtitle(liveB.Log.Count"));
        Assert.That(bs, Does.Contain("@RecentLog(b)"), "最近战报只到已展示边界");
        var bp = Web("Components", "BattlePanel.razor");
        Assert.That(bp, Does.Not.Contain("G.S."), "战斗信息读显示快照");
    }
}
