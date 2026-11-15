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
        Assert.That(gs, Does.Contain("if (res.Ok && !res.Duplicate && gen == _fxGen) PrimeFxView(fx);"), "首次重绘前先定显示快照");
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

    /// <summary>Q692-A01／A02：世界推进段提交后按真实顺序播远处生命镜头；主角确死不播；读档清掉待播（快进照播见 B52bWebTests.Q734）。</summary>
    [Test]
    public void Q692_A02_WorldLifeCloseUps_PlayedAtSegmentBoundary()
    {
        var gs = Web("Services", "GameSession.cs");
        var loop = gs[gs.IndexOf("private async Task RunLoop()")..];
        loop = loop[..loop.IndexOf("public void AckDialogue")];
        var drain = loop.IndexOf("var lifeFx = E.DrainLifeFx();");
        Assert.That(drain, Is.GreaterThan(loop.IndexOf("var r = E.Advance(S);")), "段提交后取走");
        var play = loop.IndexOf("await PlayFx(lifeFx, rgen);");
        Assert.That(play, Is.GreaterThan(loop.IndexOf("CommitSegment();")), "段提交之后");
        Assert.That(loop, Does.Contain("if (lifeFx.Count > 0 && State != null && !S.Progress.Dead)"), "主角确死不播（B52b Q734：快进时也照播）");
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

    /// <summary>Q690-A02（Q736 待答复：沿节点卡样式、底部原句不换）、Q697-A01：意志页三处与帮助整句替换、「濒死与意志」主题附成长所选人物一行。</summary>
    [Test]
    public void Q690_Q697_GrowthPage_AndHelp()
    {
        var g = Web("Components", "GrowthOverlay.razor");
        Assert.That(g, Does.Contain("@if (sk.Id == GameEngine.Willpower)"));
        Assert.That(g, Does.Contain("e.WillpowerEffectText(actor)").And.Contain("e.WillpowerDayLeft(s, actor) is int left").And.Contain("e.WillpowerRefText(rl)"));
        Assert.That(g, Does.Contain("经验只给实际出手/工作的本人；两件同门类武器不叠经验、不多解锁技能。命中、伤害等公式见系统说明。"), "Q736 待答复：底部原句保持");
        var h = Web("Services", "HelpCatalog.cs");
        Assert.That(h, Does.Not.Contain("健康归0会死亡"));
        Assert.That(h, Does.Contain("GameEngine.HelpEndings + \"七日检查已退役"));
        Assert.That(Survival.Core.Engine.GameEngine.HelpEndings, Is.EqualTo("健康归0先进入濒死（见“濒死与意志”），濒死后再受伤可能死亡；病毒达到上限会尸变。主角死亡或尸变结束本局。"));
        Assert.That(h, Does.Contain("Title = \"濒死与意志\""));
        Assert.That(h, Does.Contain("GameEngine.HelpThreeStates, GameEngine.HelpEnemies, GameEngine.HelpWillXp"));
        Assert.That(Web("Components", "HelpOverlay.razor"), Does.Contain("entry.PersonLine?.Invoke(e, s, G.GearActorId)"), "取成长页当前所选人物");
        Assert.That(Web("Services", "Ui.cs"), Does.Not.Contain("归0即死亡"));
    }

    /// <summary>
    /// B47b 试玩修复 1（R03 Q617-A02、Q572）：读档、新局、回标题清播放队列与显示快照；之前起的播放与推进循环（战斗逐条推进、世界推进）随代号失效，
    /// 不在新读入的局上推进、不播旧镜头：播放入口带调用方代号，过期即返回；战斗循环阅读暂停等待后先验代号；世界推进循环每段验代号、过期不动新局的推进标记。
    /// </summary>
    [Test]
    public void Playtest1_LoadClearsQueue_StaleLoopsNeverPlayOnLoadedGame()
    {
        var pb = Web("Services", "GameSession.Playback.cs");
        Assert.That(pb, Does.Contain("private async Task<bool> PlayFx(List<BattleFxEvent> evs, int gen)"));
        var play = pb[pb.IndexOf("private async Task<bool> PlayFx(")..];
        play = play[..play.IndexOf("private async Task<bool> PlayFxCore")];
        Assert.That(play.IndexOf("if (gen != _fxGen) return false;"), Is.GreaterThan(0).And.LessThan(play.IndexOf("PrimeFxView(evs);")), "过期代号不碰新局的显示快照");
        Assert.That(play, Does.Not.Contain("var gen = _fxGen;"), "不在播放入口重新取当前代号");
        var reset = pb[pb.IndexOf("private void ResetPlayback()")..];
        reset = reset[..reset.IndexOf("private void Pulse")];
        foreach (var w in new[] { "_fxGen++;", "_fxQueue.Clear();", "FxNow = null;", "FxView = null;", "_fxAfter.Clear();", "E.DrainBattleFx();", "E.DrainLifeFx();" })
            Assert.That(reset, Does.Contain(w), w);
        var gs = Web("Services", "GameSession.cs");
        var ui = gs[gs.IndexOf("private void ResetUi()")..];
        Assert.That(ui[..ui.IndexOf("// ───────────── 选择")], Does.Contain("ResetPlayback();"));
        foreach (var m in new[] { "public string? LoadSlot(string key)", "public void BackToTitle()" })
        {
            var body = gs[gs.IndexOf(m)..];
            Assert.That(body[..body.IndexOf("\n    }")], Does.Contain("ResetUi();"), m);
        }
        Assert.That(gs, Does.Contain("await PlayFx(fx, gen);"));
        Assert.That(gs, Does.Contain(": fx, gen);"), "玩家指令的播放带本次代号");
        var loop = gs[gs.IndexOf("public async Task BattleLoop()")..];
        loop = loop[..loop.IndexOf("private Task StepPause()")];
        var wait = loop.IndexOf("await Task.Delay(120);");
        var chk = loop.IndexOf("if (gen != _fxGen) return;", wait);
        Assert.That(chk, Is.GreaterThan(wait).And.LessThan(loop.IndexOf("E.BattleStep(S)")), "阅读暂停等待之后、推进下一步之前先验代号");
        var run = gs[gs.IndexOf("private async Task RunLoop()")..];
        run = run[..run.IndexOf("public void AckDialogue")];
        Assert.That(run, Does.Contain("var rgen = _fxGen;"));
        Assert.That(run, Does.Contain("while (State != null && !S.Progress.Dead && rgen == _fxGen)"));
        Assert.That(run, Does.Contain("if (rgen == _fxGen)   // 旧循环"), "过期循环不动新局的推进标记");
        Assert.That(run.IndexOf("if (rgen != _fxGen) return;"), Is.LessThan(run.IndexOf("await BattleLoop();")), "过期循环不接新局的战斗");
    }

    /// <summary>
    /// B47b 试玩修复 2（R03 Q617-A03、Q633）：播放中只读子页（全记录等）的点击被拦住——「返回战斗」「回到最新」不受理，点击不落到背后。
    /// B52b（Q737）起特写不再退到子页之下（见 B52bWebTests），正文只在镜头间隙滚动。
    /// </summary>
    [Test]
    public void Playtest2_DrawerUnderPlayback_ScrollsButSwallowsClicks()
    {
        var bs = Web("Components", "BattleScreen.razor");
        Assert.That(bs, Does.Contain("<div class=\"bfs-drawer @(DrawerFxPlay ? \"fx-play\" : \"\")\""));
        Assert.That(bs, Does.Contain("private bool DrawerFxPlay => G.BattleDrawer is not (null or \"help\") && G.PlaybackActive;"));
        Assert.That(bs, Does.Contain("@onclick=\"DrawerBack\">@BattleInfoCopy.BackToBattle"));
        Assert.That(bs, Does.Not.Contain("@onclick=\"() => G.OpenBattleDrawer(null)\""));
        var back = bs[bs.IndexOf("private void DrawerBack()")..];
        Assert.That(back[..back.IndexOf("}")], Does.Contain("if (DrawerClickBlocked) return;"));
        var latest = bs[bs.IndexOf("private async Task LogToLatest()")..];
        Assert.That(latest.IndexOf("if (DrawerClickBlocked) return;"), Is.GreaterThan(0).And.LessThan(latest.IndexOf("_logNew = 0;")));
        var css = Web("wwwroot", "css", "battle.css");
        Assert.That(css, Does.Contain(".bfs-drawer.fx-play button, .bfs-drawer.fx-play .bfs-drawer-body * { pointer-events: none; }"));
        Assert.That(css, Does.Not.Contain(".bfs-drawer.fx-play { pointer-events: none"), "子页本身仍接住点击，不落到背后的战场／操作台");
        Assert.That(css, Does.Contain(".bfs-drawer-body { flex: 1 1 auto; min-height: 0; overflow-y: auto; }"), "正文滚动容器照常滚动");
    }

    /// <summary>B47b 试玩修复 3（R03 Q619-A01）：焦点停在灰卡上、锁级说明正显示时，ESC 不先让按钮失焦（失焦会先收掉说明、这一下落到阅读区）；一下只收锁级说明。</summary>
    [Test]
    public void Playtest3_EscOnLockedCard_ClosesOnlyLockNote()
    {
        var js = Web("wwwroot", "js", "app.js");
        Assert.That(js, Does.Contain("var noteOn = ae && ae.classList && ae.classList.contains('locked-card') && document.querySelector('.bfs .bc-locknote:not(.ghost)');"));
        Assert.That(js, Does.Contain("if (ae && ae.tagName === 'BUTTON' && !noteOn) ae.blur();"));
        var bs = Web("Components", "BattleScreen.razor");
        Assert.That(bs, Does.Contain("class=\"cmdt skill locked-card\""));
        Assert.That(bs, Does.Contain("<div class=\"bc-locknote @(lockShown == null ? \"ghost\" : \"\")\""));
        Assert.That(bs, Does.Contain("@onblur=\"() => SetLockFocus(null)\""), "失焦即收说明——所以 ESC 不能先失焦");
        var gs = Web("Services", "GameSession.cs");
        var esc = gs[gs.IndexOf("public void BattleEscape()")..];
        Assert.That(esc.IndexOf("if (BattleLockNoteOpen)"), Is.LessThan(esc.IndexOf("if (BattleInfoPage != null) { CollapseBattleInfo(); return; }")));
    }

    /// <summary>B47b 试玩修复 4（S14 B34 Q615／Q616）：当前行动者濒死时，待机微动态让位给濒死黯淡 0.72 与跪姿（动画会盖掉 filter／transform）；非当前行动者沿原规则。</summary>
    [Test]
    public void Playtest4_DyingActingAlly_DimAndKneelOverrideIdle()
    {
        var css = Web("wwwroot", "css", "battle.css");
        Assert.That(css, Does.Contain(".anc.ally.dying .anc-art img { filter: brightness(var(--dying-dim, .72)) saturate(.7); transform: scale(1, .86);"));
        Assert.That(css, Does.Contain(".anc.ally.await-cmd.dying .anc-art > img { animation: none; filter: brightness(var(--dying-dim, .72)) saturate(.7); transform: scale(1, .86);"));
        Assert.That(css, Does.Contain("@media (prefers-reduced-motion: reduce) { .anc.ally.await-cmd.dying .anc-art > img { animation: none !important; filter: brightness(var(--dying-dim, .72))"));
        Assert.That(css.IndexOf(".anc.ally.await-cmd.dying .anc-art > img"), Is.GreaterThan(css.IndexOf("@keyframes anc-idle")), "写在待机规则之后");
    }

    /// <summary>B47b 试玩修复 5：系统说明位置行沿“{主题} · {条目}”，条目与主题同名（「濒死与意志」）只写一次。</summary>
    [Test]
    public void Playtest5_HelpCrumb_NoDuplicateWhenEntryNamedLikeTopic()
    {
        var hc = Web("Services", "HelpCatalog.cs");
        Assert.That(hc, Does.Contain("entry == null || entry.Title == topic.Title ? topic.Title : $\"{topic.Title} · {entry.Title}\";"));
        var ho = Web("Components", "HelpOverlay.razor");
        Assert.That(ho, Does.Contain("<span class=\"sub\">@HelpCatalog.Crumb(topic, entry)</span>"));
        Assert.That(ho, Does.Not.Contain("$\" · {entry.Title}\""));
    }
}
