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

    /// <summary>Q695-A01／Q696-A01：濒死只在濒死时露出——主要状态（左栏、队伍栏）、战斗站位“0/{上限} · 濒死”＋黯淡跪姿＋抵抗率角标、战斗信息常驻、休息预估原因。</summary>
    [Test]
    public void Q695_A01_DyingShownOnlyWhileDying()
    {
        var sv = Web("Services", "StatusView.cs");
        Assert.That(sv, Does.Contain("if (e.IsDying(s, s.Player))").And.Contain("Effect = GameEngine.DyingIconText"));
        var hud = Web("Components", "Hud.razor");
        Assert.That(hud, Does.Contain("e.IsDying(s, s.Player)").And.Contain("e.ResistanceShort(s.Player)"));
        Assert.That(Web("Components", "PartyPanel.razor"), Does.Contain("@e.ResistanceShort(a)"));
        var bs = Web("Components", "BattleScreen.razor");
        Assert.That(bs, Does.Contain("GameEngine.DyingHealthText(st.HealthMax)"));
        Assert.That(bs, Does.Contain("@(dying ? \"dying\" : \"\")"));
        Assert.That(bs, Does.Contain("G.E.ResistanceShort(s.Player)"));
        Assert.That(Web("Components", "BattlePanel.razor"), Does.Contain("GameEngine.DyingHealthText(va.Stats.HealthMax)"));
        Assert.That(Web("Components", "RestOverlay.razor"), Does.Contain("GameEngine.DyingIconText"));
        var css = Web("wwwroot", "css", "battle.css");
        Assert.That(css, Does.Contain(".anc.ally.dying .anc-art img { filter: brightness(var(--dying-dim, .72))"));
    }

    /// <summary>Q692-A01／A02、Q734：世界推进段提交后按真实顺序播远处生命镜头；快进不插入、主角确死不播；读档清掉待播。</summary>
    [Test]
    public void Q692_A02_WorldLifeCloseUps_PlayedAtSegmentBoundary()
    {
        var gs = Web("Services", "GameSession.cs");
        var loop = gs[gs.IndexOf("private async Task RunLoop()")..];
        loop = loop[..loop.IndexOf("public void AckDialogue")];
        var drain = loop.IndexOf("var lifeFx = E.DrainLifeFx();");
        Assert.That(drain, Is.GreaterThan(loop.IndexOf("var r = E.Advance(S);")), "段提交后取走");
        var play = loop.IndexOf("await PlayFx(lifeFx);");
        Assert.That(play, Is.GreaterThan(loop.IndexOf("CommitSegment();")), "段提交之后");
        Assert.That(loop, Does.Contain("if (!_skip && lifeFx.Count > 0 && State != null && !S.Progress.Dead)"), "快进时不插入镜头，主角确死不播");
        var pb = Web("Services", "GameSession.Playback.cs");
        var reset = pb[pb.IndexOf("private void ResetPlayback()")..];
        Assert.That(reset[..reset.IndexOf("private void Pulse")], Does.Contain("E.DrainLifeFx();"));
        var layer = Web("Components", "BattleFxLayer.razor");
        Assert.That(layer, Does.Contain("<p class=\"bfx-where\" aria-hidden=\"true\">@ev.Where</p>"));
    }

    /// <summary>Q618-A01：没有选指令时点人物／队列头像展开「战斗信息」显示该对象——不移焦点、不滚动、不选技；离场写「该对象已离场。」。</summary>
    [Test]
    public void Q618_A01_ClickReveal_NoFocusSteal_DepartedText()
    {
        Assert.That(Survival.Core.Engine.BattleInfoCopy.Departed, Is.EqualTo("该对象已离场。"));
        var bi = Web("Services", "GameSession.BattleInfo.cs");
        var rv = bi[bi.IndexOf("private void RevealBattleInfo")..];
        rv = rv[..rv.IndexOf("public bool BattleInfoGone")];
        Assert.That(rv, Does.Contain("BattleAction != null || PlaybackActive || DialogueLocked) return;"), "已选指令、播放、对白时不由点击展开");
        Assert.That(rv, Does.Not.Contain("BattleFocusId").And.Not.Contain("BattleScrollTo"), "不抢焦点、不滚战场");
        var gs = Web("Services", "GameSession.cs");
        Assert.That(gs, Does.Contain("if (actorId != null) RevealBattleInfo(\"ally:\" + actorId);"));
        Assert.That(gs, Does.Contain("RevealBattleInfo(\"foe:\" + index);"));
        var bp = Web("Components", "BattlePanel.razor");
        Assert.That(bp, Does.Contain("G.BattleInfoGone(s)").And.Contain("@BattleInfoCopy.Departed"));
        Assert.That(bp.IndexOf("G.BattleInfoGone(s)"), Is.LessThan(bp.IndexOf("else if ((peekAlly ?? G.BattleAllyView) != null")), "离场句先于原对象详情，不悄悄换人");
    }

    /// <summary>Q619-A01：ESC 先收小盘／锁级说明／选号位草稿，再收阅读区，再原取消瞄准或开菜单；每次一层，Esc 键帽同源。</summary>
    [Test]
    public void Q619_A01_EscOrder_OneLayerPerPress()
    {
        var gs = Web("Services", "GameSession.cs");
        var esc = gs[gs.IndexOf("public void BattleEscape()")..];
        esc = esc[..esc.IndexOf("ToggleBattleMenu(true);")];
        int At(string x) { var i = esc.IndexOf(x); Assert.That(i, Is.GreaterThan(0), x); return i; }
        Assert.That(At("if (MedTrayOpen)"), Is.LessThan(At("if (BattleLockNoteOpen)")));
        Assert.That(At("if (BattleLockNoteOpen)"), Is.LessThan(At("if (BattleSwapAiming)")));
        Assert.That(At("if (BattleSwapAiming)"), Is.LessThan(At("if (BattleInfoPage != null)")));
        Assert.That(At("if (BattleInfoPage != null)"), Is.LessThan(At("if (BattleAction != null)")));
        Assert.That(esc, Does.Contain("BattleFocusId = \"cmd-swap\"; ClearBattleAction(); return;"), "取消选位回「调整站位」卡");
        Assert.That(esc, Does.Contain("BattleFocusId = \"cmd-med\"; ToggleMedTray(false); return;"));
        var keys = Web("Services", "GameSession.Keys.cs");
        var own = keys[keys.IndexOf("private EscOwner BattleEscOwner()")..];
        Assert.That(own.IndexOf("EscOwner.LockNote"), Is.LessThan(own.IndexOf("EscOwner.SwapDraft")));
        Assert.That(own.IndexOf("EscOwner.SwapDraft"), Is.LessThan(own.IndexOf("return EscOwner.BattleInfo")));
        Assert.That(Web("Components", "BattleScreen.razor"), Does.Contain("G.BattleLockNoteCloser = CloseLockNote;"));
    }
}
