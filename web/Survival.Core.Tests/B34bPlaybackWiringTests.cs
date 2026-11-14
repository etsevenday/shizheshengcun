using Survival.Core.Engine;

namespace Survival.Core.Tests;

/// <summary>
/// B34b（R03／S14 文首「2026-11-13 B34主要Q570—Q572」§Q571／§Q572；验收 Q571-A03、Q572-A01—A04）：
/// 网页壳播放层的接线——提交、存档在前，播放在后；两级跳过；ESC 开菜单停住进度；读档清播放；播放中背后不可操作。
/// </summary>
public class B34bPlaybackWiringTests
{
    private static string Root()
    {
        var d = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (d != null && !File.Exists(Path.Combine(d.FullName, "web", "Survival.sln"))) d = d.Parent;
        return d!.FullName;
    }

    private static string Web(string relative) => File.ReadAllText(Path.Combine(Root(), "web", "Survival.Web", relative)).Replace("\r\n", "\n");

    [Test]
    public void Q571A03_BaseDuration_ScaledBySpeed_NoOldStepPauseWhenPlayed()
    {
        var p = Web("Services/GameSession.Playback.cs");
        Assert.That(p, Does.Contain("public const int FxBaseMs = 1200;"), "Q297：每段基准 1200 毫秒");
        Assert.That(p, Does.Contain("public int FxDurationMs => MotionMs(FxBaseMs);"), "Q321：乘动画速度");
        var g = Web("Services/GameSession.cs");
        Assert.That(g, Does.Contain("if (r.Kind is BattleStepKind.EnemyActed or BattleStepKind.RoundEnded && !played)"), "播了特写的一步不再叠旧的逐条停顿");
    }

    [Test]
    public void Q572A01_CommitAndSaveBeforePlayback()
    {
        var g = Web("Services/GameSession.cs");
        var exec = g[g.IndexOf("var fx = E.DrainBattleFx();")..];
        Assert.That(exec.IndexOf("Commit();"), Is.LessThan(exec.IndexOf("await PlayFx(fx)")), "真实结果先提交、存档，再播放");
        Assert.That(g, Does.Contain("if (gen != _fxGen) return;   // 读档／新局：旧循环直接退出，不碰新局的锁"));
    }

    /// <summary>BM7a（S14 Q633）取代原两级跳过：播放中的点击／Enter／Space 不跳、不点穿（BM7aBattleTests 另验）；结束帧上的同一次 Enter 仍不确认背后指令。</summary>
    [Test]
    public void Q572A02_NoSkip_NoConfirmThrough()
    {
        var p = Web("Services/GameSession.Playback.cs");
        Assert.That(p, Does.Not.Contain("FxAtResult").And.Not.Contain("_fxSkipAll").And.Not.Contain("PlaybackSkip"), "BM7a（Q633）：不再有两级跳过");
        var g = Web("Services/GameSession.cs");
        Assert.That(g, Does.Contain("if (PlaybackActive || FxJustEnded) return;"), "播放刚结束的同一次 Enter 不确认背后指令");
        var js = Web("wwwroot/js/app.js");
        Assert.That(js, Does.Contain("if (!ev.repeat) dotnet.invokeMethodAsync('OnKey', 'Skip');"), "按住不重复");
    }

    [Test]
    public void Q572A03_EscOpensMenu_MenuPausesPlayback()
    {
        var p = Web("Services/GameSession.Playback.cs");
        Assert.That(p, Does.Contain("public bool FxPaused => MenuOpen || BattleMenuOpen || BattleDrawer == \"help\";"));
        Assert.That(p, Does.Contain("if (!FxPaused) elapsed += now - last;"), "菜单开着不计时，关上原处续播");
        var keys = Web("Services/GameSession.Keys.cs");
        Assert.That(keys, Does.Contain("if (PlaybackActive && !FxUnderReading) return EscOwner.OpenMenu;"));
        var gs = Web("Components/GameScreen.razor");
        Assert.That(gs, Does.Contain("if (G.PlaybackActive && !G.FxUnderReading) return;"), "播放中其余快捷键不穿透");
    }

    [Test]
    public void Q572A04_LoadClearsPlayback()
    {
        var g = Web("Services/GameSession.cs");
        Assert.That(g, Does.Contain("ResetPlayback();   // B34b（Q572）"));
        var p = Web("Services/GameSession.Playback.cs");
        var reset = p[p.IndexOf("private void ResetPlayback()")..];
        reset = reset[..reset.IndexOf("}")];
        Assert.That(reset, Does.Contain("_fxGen++;").And.Contain("_fxQueue.Clear();").And.Contain("FxNow = null;"));   // BM7a（Q633）：已无跳过意图可清
    }

    [Test]
    public void Q572_BackgroundInertWhilePlaying()
    {
        var bs = Web("Components/BattleScreen.razor");
        Assert.That(bs, Does.Contain("var fxLock = G.PlaybackActive && !G.FxUnderReading;"));
        foreach (var part in new[] { "bfs-top", "bfs-queue", "bfs-scene", "bfs-console", "bfs-infoarea" })
            Assert.That(bs, Does.Match(part + "\"[^>]*inert=\"@lockedUi\""), part + " 播放中不可操作");
        Assert.That(bs, Does.Contain("<BattleFxLayer />"));
        Assert.That(Web("Components/GameScreen.razor"), Does.Contain("@if (G.PlaybackActive && !G.InBattleUi) { <BattleFxLayer /> }"), "已收束时盖在结算画面上播完");
    }
}
