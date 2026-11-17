using System.Reflection;
using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发记录 BM11「次要问题合并」：B52 完整原批 Q781—Q786（Q780 本次不做）。
/// 规则出处：S14／R03 文首「2026-11-17 B52 Q780—Q786完整原批」各节与验收 B52-Q781-01—B52-Q786-02；逐字稿 2026-11-17 B52 节。
/// </summary>
public class BM11B52Tests : BattleTestBase
{
    protected override bool NoEnemyApproach => true;

    private void Ready(GameState s)
    {
        PlayerFirst(s);
        E.BattleStep(s);
        Assert.That(E.AwaitingPlayer(s), Is.True);
    }

    private ItemInstance Arm2(GameState s, string id)
    {
        var slot = ItemInstance.EquipOwner(GameState.PlayerId, EquipSlots.Weapon2);
        foreach (var old in s.Items.Where(i => i.Owner == slot)) old.Owner = ItemInstance.Carried;
        var it = E.CreateItems(s, id, 1, slot).Single();
        s.Battle!.ActiveWeaponSlot = 2;
        return it;
    }

    private static BattleFxEvent One(List<BattleFxEvent> fx) => fx.Single(x => !x.Short && x.Results.Count > 0);

    private void EnemyAct(GameState s, BattleEnemy e) =>
        typeof(GameEngine).GetMethod("EnemyActivation", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(E, new object[] { s, e, false });

    private void Lock(GameState s, BattleEnemy e) =>
        typeof(GameEngine).GetMethod("LockIntent", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(E, new object[] { s, s.Battle!, e });

    private void AtIntent(GameState s, BattleEnemy e, string intent)
    {
        for (var k = 0; k < 8; k++)
        {
            if (s.Battle!.Round - k < 1) s.Battle.Round = k + 1;
            e.JoinRound = s.Battle.Round - k;
            e.Acted = false;
            e.RoundApUsed = 0;
            Lock(s, e);
            if (e.Intent == intent) return;
        }
        Assert.Fail($"{e.EnemyId} 循环里没有 {intent}");
    }

    /// <summary>主角一人持指挥器（武器Ⅱ），号令 10 级、9AP（同 B52bTests.Q729_A01 布场）。</summary>
    private GameState Commander(uint seed, string tool)
    {
        var s = Fight(seed, "worker");
        Ready(s);
        Arm(s, "kitchen_knife");
        Arm2(s, tool);
        StandAt(s, 2);
        s.Skills["command"].Level = 10;
        s.Battle!.PlayerAp = 9;
        return s;
    }

    private static string Web(params string[] parts)
    {
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "web", "Survival.sln"))) dir = dir.Parent;
        return File.ReadAllText(Path.Combine(new[] { dir!.FullName, "web", "Survival.Web" }.Concat(parts).ToArray())).Replace("\r\n", "\n");
    }

    // ───────────── Q781：三处删“每回合推进5分钟” ─────────────

    /// <summary>B52-Q781-01 ①：撤退预估写“成功：脱战，{原真实后半句}”，不再有“本回合推进5分钟后”；失败半句原样。</summary>
    [Test]
    public void Q781_01_RetreatPreview_NoFiveMinutes_KeepsRealTail()
    {
        var s = Fight(78101, "worker");
        Ready(s);
        var notes = E.PreviewCommand(s, BattleCommand.Of("retreat")).Notes;
        var ok = notes.Where(n => n.StartsWith("成功：")).ToList();
        Assert.That(ok, Has.Count.EqualTo(1));
        // 期望出处：Q781 A①“成功：脱战，……”，省略号为原实际后半句（WorldRetreatPreview＋原“存活敌人保留血量与位置；失败：本回合行动点用尽”）
        Assert.That(ok[0], Is.EqualTo($"成功：脱战，{E.WorldRetreatPreview(s, s.Battle!)}，存活敌人保留血量与位置；失败：本回合行动点用尽"));
        Assert.That(notes, Has.None.Contains("推进5分钟"));
    }

    /// <summary>B52-Q781-01 ②③：回合末等待逐字“回合末结算”；敌人详情删“每回合推进5分钟世界时间；”，保留撤退去向句与原句号。</summary>
    [Test]
    public void Q781_02_RoundEndWait_And_EnemyDetail()
    {
        Assert.That(BattleInfoCopy.RoundEndWait, Is.EqualTo("回合末结算"));   // 逐字稿 2026-11-17 B52 节
        var bs = Web("Components", "BattleScreen.razor");
        Assert.That(bs, Does.Not.Contain("本回合推进5分钟"));
        Assert.That(bs.Split("return BattleInfoCopy.RoundEndWait;").Length - 1, Is.EqualTo(2), "队列走完、游标不是我方两处等待");
        var ed = Web("Components", "EnemyDetail.razor");
        Assert.That(ed, Does.Not.Contain("每回合推进5分钟世界时间"));
        Assert.That(ed, Does.Contain("<li>@e.RetreatOutlookHere(s)。</li>"));
    }

    // ───────────── Q782／Q783：帮助位置 ─────────────

    /// <summary>B52-Q782-01：系统说明「中央推进条」原“跳过动画只是加快显示……”后紧接一遍“生命特写不可跳过；播完恢复快进。”，全帮助只一次。</summary>
    [Test]
    public void Q782_01_LifeCloseUpHelp_AfterSkipAnimation_Once()
    {
        Assert.That(ShellKeys.LifeCloseUpHelp, Is.EqualTo("生命特写不可跳过；播完恢复快进。"));   // 逐字稿 2026-11-17 B52 节 lifeHelp
        var cat = Web("Services", "HelpCatalog.cs");
        const string anchor = "\"跳过动画只是加快显示，不改变任何结果；允许退出的行动在任何速度下都可以退出。\",\n                        ShellKeys.LifeCloseUpHelp,";
        Assert.That(cat, Does.Contain(anchor), "紧接原动画说明之后");
        Assert.That(cat.Split("ShellKeys.LifeCloseUpHelp").Length - 1, Is.EqualTo(1), "只一遍");
    }

    /// <summary>B52-Q783-02：战斗帮助“敌我出手表现均不可跳过……”之后另起一条精简说明，全帮助只一次；原 ESC 菜单句不变。</summary>
    [Test]
    public void Q783_02_PlaybackNoInputHelp_AfterNoSkip_Once()
    {
        Assert.That(ShellKeys.PlaybackNoInputHelp, Is.EqualTo("特写播放时无法操作；镜头间隙可查看战斗信息。"));   // 逐字稿 2026-11-17 B52 节精简 readingHelp
        Assert.That(ShellKeys.BattleNoSkipHelp, Is.EqualTo("敌我出手表现均不可跳过。播放期间可按ESC打开菜单，关闭菜单后从原处继续。"), "S14 Q633 原句不变");
        var cat = Web("Services", "HelpCatalog.cs");
        var i = cat.IndexOf("ShellKeys.BattleNoSkipHelp,", StringComparison.Ordinal);
        var j = cat.IndexOf("ShellKeys.PlaybackNoInputHelp,", StringComparison.Ordinal);
        Assert.That(i, Is.GreaterThan(0));
        Assert.That(j, Is.GreaterThan(i));
        Assert.That(cat.Substring(i, j - i).Split('\n').Length, Is.EqualTo(2), "紧接下一行");
        Assert.That(cat.Split("ShellKeys.PlaybackNoInputHelp").Length - 1, Is.EqualTo(1), "只一遍");
    }

    // ───────────── Q784：预判也写命中强化 ─────────────

    /// <summary>B52-Q784-01：首次号令为稳心时，只有武器特性“预判”带来命中能力正增益——也写“命中强化”（排在原结果字之后）。</summary>
    [Test]
    public void Q784_01_PredictOnly_WritesAccuracyGain()
    {
        var s = Commander(78401, "command_terminal");
        var b = s.Battle!;
        b.Panic = 30;   // 布场
        E.DrainBattleFx();
        Assert.That(Cmd(s, "command_05").Ok, Is.True);
        Assert.That(b.InspireBonus, Is.EqualTo(4), "A05 便携指挥终端预判+4，数值不变");
        var ch = One(E.DrainBattleFx()).Results.Single().Changes;
        Assert.That(ch, Has.Count.EqualTo(2));
        Assert.That(ch[0], Does.StartWith("恐慌−"), "Q694 原结果字在前");
        Assert.That(ch[1], Is.EqualTo("命中强化"));   // R03 Q729 表短名；Q784 A 含预判

        // 零变化：本场第二次号令不再预判，不写命中强化
        b.PlayerAp = 9;
        Assert.That(Cmd(s, "command_10").Ok, Is.True);
        Assert.That(One(E.DrainBattleFx()).Results.Single().Changes, Is.EqualTo(new[] { "伤害强化" }));
        Assert.That(b.InspireBonus, Is.EqualTo(4));
    }

    /// <summary>B52-Q784-01 配对：同样稳心但指挥器没有预判（信号哨）——不写命中强化。</summary>
    [Test]
    public void Q784_01_NoPredict_NoAccuracyGain()
    {
        var s = Commander(78402, "signal_whistle");
        s.Battle!.Panic = 30;
        E.DrainBattleFx();
        Assert.That(Cmd(s, "command_05").Ok, Is.True);
        var ch = One(E.DrainBattleFx()).Results.Single().Changes;
        Assert.That(ch, Has.Count.EqualTo(1));
        Assert.That(ch[0], Does.StartWith("恐慌−"));
    }

    /// <summary>B52-Q784-01：同次鼓舞＋预判，同一人只一份“命中强化”；数值 6+4 不变。</summary>
    [Test]
    public void Q784_01_InspirePlusPredict_OneAccuracyGain()
    {
        var s = Commander(78403, "command_terminal");
        E.DrainBattleFx();
        Assert.That(Cmd(s, "command_01").Ok, Is.True);
        Assert.That(s.Battle!.InspireBonus, Is.EqualTo(10), "A05：6 + 预判 4");
        Assert.That(One(E.DrainBattleFx()).Results.Single().Changes, Is.EqualTo(new[] { "命中强化" }));
    }

    // ───────────── Q785：标记落空不出短反馈 ─────────────

    /// <summary>B52-Q785-01：守卫标记没有合法目标落空——无“标记”短反馈、不造标记状态，原落空战报保留；成功仍一次（配对）。</summary>
    [Test]
    public void Q785_01_MarkWhiff_NoShortFeedback_SuccessStillOnce()
    {
        var s = Fight(78501, "ec_boss_cold");
        Ready(s);
        StandAt(s, 4);
        var e = s.Battle!.Alive.First();
        e.Slot = "1";
        AtIntent(s, e, "mark");
        s.Battle.Allies = new List<string>();   // 布场：没有可标记的人
        e.IntentActor = null;
        var n0 = s.Battle.Log.Count;
        E.DrainBattleFx();
        EnemyAct(s, e);
        var fx = E.DrainBattleFx();
        Assert.That(fx.Where(x => x.Short && x.Name == GameEngine.EnemyMarkShort), Is.Empty);
        Assert.That(e.MarkedActor, Is.Null, "不伪造标记状态");
        Assert.That(s.Battle.Log.Skip(n0).Count(l => l.Contains("落空（没有可标记的人）")), Is.EqualTo(1), "原落空战报");

        var t = Fight(78502, "ec_boss_cold");
        Ready(t);
        StandAt(t, 4);
        var g = t.Battle!.Alive.First();
        g.Slot = "1";
        AtIntent(t, g, "mark");
        E.DrainBattleFx();
        EnemyAct(t, g);
        var ok = E.DrainBattleFx().Where(x => x.Short && x.Name == GameEngine.EnemyMarkShort).ToList();
        Assert.That(ok, Has.Count.EqualTo(1));
        Assert.That(g.MarkedActor, Is.EqualTo(GameState.PlayerId));
    }

    // ───────────── Q786：所有真实当前健康分数标真濒死 ─────────────

    /// <summary>B52-Q786-01：真濒死时公共函数写“0/{上限} · 濒死”，恢复撤下按原格式；正小数（显示&lt;0.01）不误标。</summary>
    [Test]
    public void Q786_01_HealthFraction_Dying_Recovered_PositiveTiny()
    {
        var s = New(78601);
        var p = s.Player;
        p.Stats.Health = 0;
        Assert.That(E.HealthFractionText(s, p, Fmt.N, Fmt.N), Is.EqualTo("0/20 · 濒死"));   // 主角健康上限 20（配置 stateScale.healthMax）；R09 Q738／Q786：真濒死逐字“0/{上限} · 濒死”
        p.Stats.Health = 0.004;
        Assert.That(E.HealthFractionText(s, p, Fmt.N, Fmt.N), Is.EqualTo("<0.01/20.00"), "正小数不误标，沿原两位格式");
        p.Stats.Health = 15;
        Assert.That(E.HealthFractionText(s, p, Fmt.N, Fmt.T), Is.EqualTo("15.00/20"));
        p.Stats.Health = 0;
        s.Progress.Dead = true;
        Assert.That(E.HealthFractionText(s, p, Fmt.N, Fmt.N), Is.EqualTo("0.00/20.00"), "确死不混同濒死");
    }

    /// <summary>B52-Q786-01：诊所咨询、远行确认预览全员行与留守风险预览（HU04）的当前健康真濒死标记；恢复撤下（配对）。</summary>
    [Test]
    public void Q786_01_Consult_Journey_Hu04_CurrentRows()
    {
        var s = New(78602);
        var p = s.Player;
        p.Stats.Health = 0;
        Assert.That(E.ConsultText(s, p), Does.StartWith("健康0/20 · 濒死，免疫"));
        var line = E.JourneyPreviewOf(s).PartyLines.Single();
        Assert.That(line, Does.Contain("：健康0/20 · 濒死 饱食"));
        p.Stats.Health = 12.5;
        Assert.That(E.ConsultText(s, p), Does.StartWith("健康12.50/20.00，免疫"));
        Assert.That(E.JourneyPreviewOf(s).PartyLines.Single(), Does.Contain("：健康12.50/20.00 饱食"));

        E.DebugRecruit(s, "npc_lin_zhao");
        var r = s.Roster.Single(x => x.ActorId == "npc_lin_zhao");
        Assert.That(r.Present, Is.True);
        var mate = s.Actors["npc_lin_zhao"];
        var hu = typeof(GameEngine).GetMethod("QuestCompletionDetail", BindingFlags.NonPublic | BindingFlags.Instance)!;
        mate.Stats.Health = 0;
        var dying = (string)hu.Invoke(E, new object[] { s, E.Quest("HU04")! })!;
        Assert.That(dying, Does.Contain($"{r.Name}：健康0/{Fmt.T(mate.Stats.HealthMax)} · 濒死、病毒"));
        mate.Stats.Health = 10;
        var ok = (string)hu.Invoke(E, new object[] { s, E.Quest("HU04")! })!;
        Assert.That(ok, Does.Contain($"{r.Name}：健康10.00/{Fmt.N(mate.Stats.HealthMax)}、病毒"));
        Assert.That(ok, Does.Not.Contain("濒死"));
    }

    /// <summary>B52-Q786-01／02：属性详情标题分数改走同一函数；原无分数处（队伍栏、同伴卡）不新增，左栏仍原 Q856 写法。</summary>
    [Test]
    public void Q786_02_AttrDetail_Wired_NoNewFractions()
    {
        var ad = Web("Components", "AttrDetail.razor");
        Assert.That(ad, Does.Contain("@(id == StatId.Health ? e.HealthFractionText(s, s.Player, Ui.N, Ui.T) : $\"{Ui.N(v)}/{Ui.T(s.Stats.Max(id))}\")"));
        Assert.That(Web("Components", "PartyPanel.razor"), Does.Not.Contain("HealthFractionText"));
        Assert.That(Web("Components", "Hud.razor"), Does.Contain("@if (id == StatId.Health && e.IsDying(s, s.Player)) { <b>@GameEngine.DyingHealthText(s.Stats.Max(id))</b> }"), "左栏 Q856 原写法保持");
    }
}
