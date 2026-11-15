using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 批次 B52a（需求 R259；R03 文首「2026-11-15 B47b Q729—Q738」§Q735 与验收 Q735-A01—A06，S01／S06／S08／S11／R01／R04／R05／R08 同批 Q735 节）：
/// 实际入战到收束世界时间冻结——轮末与提前结束不再推进 5 分钟世界 tick，战后不补算；本地战斗规则照常；旧在战只冻结未来、保留可信已耗。
/// </summary>
public class B52aTests : BattleTestBase
{
    protected override bool NoEnemyApproach => true;

    private const string Gu = "npc_gu_cheng";

    private void Ready(GameState s)
    {
        PlayerFirst(s);
        E.BattleStep(s);
        Assert.That(E.AwaitingPlayer(s), Is.True);
    }

    /// <summary>防御着走完 n 个大回合（每步把主角健康拉满，避免被打死；不改世界时间）。</summary>
    private void Rounds(GameState s, int n)
    {
        var b = s.Battle!;
        var start = b.Round;
        for (var i = 0; i < 400 && s.Battle != null && b.Round < start + n; i++)
        {
            s.Stats.Health = s.Stats.HealthMax;
            if (E.AwaitingPlayer(s) && !b.ActingPursuit && b.PlayerAp >= 1 && E.CommandBlocked(s, BattleCommand.Of("guard")) == null) Cmd(s, "guard");
            else Step(s);
        }
    }

    private void RetreatNow(GameState s)
    {
        ToPlayer(s);
        Assert.That(E.AwaitingPlayer(s), Is.True);
        Force(s, Below(0.0001));
        Assert.That(Cmd(s, "retreat").Ok, Is.True);
        Assert.That(s.Battle, Is.Null, "强制撤退成功");
    }

    // ───────────── Q735-A01 ─────────────

    /// <summary>Q735-A01：多轮后撤退收束，世界分钟、段计数、日期等于入战时；没有轮末／收尾 5 分钟，也没有战后补算；战前已提交的段保留。</summary>
    [Test]
    public void Q735_A01_MultiRoundsThenRetreat_MinuteAndSegmentUnchanged()
    {
        var s = New(52001);
        E.DebugTeleport(s, "field");
        s.GameMinute = At(2, 9, 0);
        s.SpawnNextCheck = int.MaxValue;
        s.PatrolNextCheck = int.MaxValue;
        s.WorldEnemies.Clear();
        Start(s, ActionRequest.Wait(30));
        Run(s);
        Assert.That(s.GameMinute, Is.EqualTo(At(2, 9, 30)), "战前等待已真实提交");
        var id = E.DebugSpawnEnemy(s, "stag", "field").Id;
        Assert.That(E.DebugStartBattle(s, new List<int> { id }, GameEngine.FearEncounter), Is.True);
        var (m0, seg0) = (s.GameMinute, s.SegmentCount);
        Ready(s);
        Rounds(s, 3);
        Assert.That(s.Battle, Is.Not.Null);
        Assert.That(s.Battle!.Round, Is.GreaterThanOrEqualTo(4));
        Assert.That((s.GameMinute, s.SegmentCount, s.Battle.Ticks), Is.EqualTo((m0, seg0, 0)), "三轮都不推进世界时间");
        RetreatNow(s);
        Assert.That((s.GameMinute, s.SegmentCount), Is.EqualTo((m0, seg0)), "撤退收束不加 5 分钟、不补算");
        Assert.That(s.GameMinute, Is.EqualTo(At(2, 9, 30)), "战前已提交段不回滚");
    }

    /// <summary>Q735-A01：第 1 回合中途提前胜利，同样不加收尾 5 分钟；结算句回合耗时片段换成逐字稿 battleEndTime。</summary>
    [Test]
    public void Q735_A01_EarlyVictory_NoEndTick_EndText()
    {
        var s = Fight(52002, "shambler");
        Ready(s);
        Arm(s, "hatchet");
        Enemy(s, 1).Hp = 0.01;
        var (m0, seg0) = (s.GameMinute, s.SegmentCount);
        Force(s, Below(0.01), AtLeast(0.99), Any(), Any());
        Cmd(s, "basic_attack", 1);
        Assert.That(s.Battle, Is.Null);
        Assert.That((s.GameMinute, s.SegmentCount), Is.EqualTo((m0, seg0)));
        var end = s.Log.Last(l => l.Text.StartsWith("战斗胜利")).Text;
        Assert.That(end, Does.StartWith("战斗胜利：共1回合（世界时间冻结）"));
        Assert.That(end, Does.Not.Contain("分钟）"));
    }

    // ───────────── Q735-A02 ─────────────

    /// <summary>
    /// Q735-A02：临期物、燃料、火堆加工、世界分钟冷却、自然免疫病毒、候补照护／自然率都不因多轮战斗变化，也不写远处生命日志；
    /// 战外下一真段才继续。
    /// </summary>
    [Test]
    public void Q735_A02_WorldSystemsFrozenDuringBattle_ResumeAfter()
    {
        var s = New(15);
        foreach (var f in new[] { "hub_registered", "church_route_record", "academy_safety_record" }) s.Progress.Flags.Add(f);
        s.SpawnNextCheck = int.MaxValue;
        s.PatrolNextCheck = int.MaxValue;
        foreach (var id in new[] { "npc_lin_zhao", "npc_zhou_yan", "npc_shen_he", Gu }) E.DebugRecruit(s, id);
        Assert.That(E.IsReserve(s, Gu), Is.True);
        var gu = s.Actor(Gu);
        gu.Stats.Satiety = 10; gu.Stats.Hydration = 10; gu.Stats.Wakefulness = 30; gu.Stats.Morale = 40;   // 照护低值会被补到 80 的那种
        // 火堆烧水 + 燃料
        var pot = First(s, "pot");
        E.DebugGive(s, "dirty_water", 2, "camp");
        E.DoInstant(s, InstantRequest.FillPot(First(s, "dirty_water", "camp").InstanceId, pot.InstanceId, 2));
        E.DoInstant(s, InstantRequest.PutInFire(pot.InstanceId));
        var fac = s.Locations["camp"].Facility;
        fac.FireLit = true;
        fac.FuelMinutes = 60;
        // 临期物（下一段就到期）、绷带冷却、感染与免疫
        var berry = E.CreateItems(s, "berry", 2, ItemInstance.Carried).First();
        berry.ExpiresAt = s.GameMinute + 5;
        s.BandageReadyAt = s.GameMinute + 30;
        s.Body.Infected = true;
        s.Body.VirusLoad = 30;
        s.Body.Immunity = 50;
        var dog = E.DebugSpawnEnemy(s, "stag", "camp");
        Assert.That(E.DebugStartBattle(s, new List<int> { dog.Id }, GameEngine.FearEncounter), Is.True);
        var m0 = s.GameMinute;
        var heat0 = E.ProcessOf(s, pot)!.HeatDoneMinutes;
        var gu0 = (gu.Stats.Satiety, gu.Stats.Hydration, gu.Stats.Wakefulness, gu.Stats.Morale, gu.Stats.Health);
        var logs0 = s.Log.Count(l => l.Text.Contains(E.ActorDisplayName(s, Gu)));
        Ready(s);
        Rounds(s, 3);
        Assert.That(s.GameMinute, Is.EqualTo(m0));
        Assert.That(E.ProcessOf(s, pot)!.HeatDoneMinutes, Is.EqualTo(heat0), "火堆加工不推进");
        Assert.That(fac.FuelMinutes, Is.EqualTo(60), "燃料不燃烧");
        Assert.That((berry.ItemId, berry.ExpiresAt), Is.EqualTo(("berry", (int?)(m0 + 5))), "临期物不腐败，截止不平移");
        Assert.That(s.Items.Any(i => i.ItemId == GameEngine.SpoiledMatterId), Is.False);
        Assert.That(s.BandageReadyAt, Is.EqualTo(m0 + 30), "世界分钟冷却不按回合减少");
        Assert.That(E.CommandBlocked(s, BattleCommand.Of("bandage")) ?? "", Does.Not.Contain("还需0分钟"));
        Assert.That((s.Body.VirusLoad, s.Body.Immunity), Is.EqualTo((30.0, 50.0)), "自然病毒／免疫不结算");
        Assert.That((gu.Stats.Satiety, gu.Stats.Hydration, gu.Stats.Wakefulness, gu.Stats.Morale, gu.Stats.Health), Is.EqualTo(gu0), "候补自然率与照护不结算");
        Assert.That(s.Log.Count(l => l.Text.Contains(E.ActorDisplayName(s, Gu))), Is.EqualTo(logs0), "不产生远处人物日志");
        if (s.Battle != null) RetreatNow(s);
        Assert.That(s.GameMinute, Is.EqualTo(m0), "收束不补算");
        Assert.That(fac.FuelMinutes, Is.EqualTo(60));
        // 战外下一真段才继续
        Start(s, ActionRequest.Wait(30));
        Run(s);
        Assert.That(s.GameMinute, Is.EqualTo(m0 + 30), "战外仍是原 5 分钟段");
        Assert.That(fac.FuelMinutes, Is.LessThan(60), "燃料战外照常燃烧");
        Assert.That(berry.ItemId == GameEngine.SpoiledMatterId || !s.Items.Contains(berry) || berry.ExpiresAt <= s.GameMinute, Is.True, "临期物到战外段才到期");
    }

    // ───────────── Q735-A03 ─────────────

    /// <summary>Q735-A03：回合出血原时机一次照扣、战后按恐慌扣精神照常；世界时间不动。</summary>
    [Test]
    public void Q735_A03_RoundBleedAndPostBattleMorale_Kept()
    {
        var s = Fight(20, "crawler");
        var b = s.Battle!;
        s.Body.BleedTriggers = 3;
        s.Body.BleedDamage = 0.4;
        s.Stats.Morale = 60;
        var h0 = s.Stats.Health;
        var m0 = s.GameMinute;
        Ready(s);
        Cmd(s, "end_turn");
        for (var i = 0; i < 20 && b.Round == 1; i++)
        {
            if (b.Cursor < b.Queue.Count && b.Queue[b.Cursor] > 0) { b.Cursor++; continue; }
            Step(s);
        }
        Assert.That(b.Round, Is.EqualTo(2));
        Assert.That(s.Body.BleedTriggers, Is.EqualTo(2), "回合出血照扣一次");
        Assert.That(h0 - s.Stats.Health, Is.EqualTo(0.4).Within(1e-9));
        Assert.That(s.GameMinute, Is.EqualTo(m0));
        b.Fighter(GameState.PlayerId).Panic = 60;
        var morale = s.Stats.Morale;
        var loss = Math.Min(Cfg.Panic.MoraleLossMax, 60 / Cfg.Panic.MoraleLossDivisor);
        for (var i = 0; i < 20 && s.Battle != null && !E.AwaitingPlayer(s); i++)
        {
            if (b.Cursor < b.Queue.Count && b.Queue[b.Cursor] > 0) { b.Cursor++; continue; }
            E.BattleStep(s);
        }
        Force(s, Below(0.0001));
        Assert.That(Cmd(s, "retreat").Ok, Is.True);
        Assert.That(s.Battle, Is.Null);
        Assert.That(morale - s.Stats.Morale, Is.EqualTo(loss).Within(1e-9), "战后恐慌精神结账保留");
        Assert.That(s.GameMinute, Is.EqualTo(m0));
    }

    // ───────────── Q735-A04 ─────────────

    /// <summary>Q735-A04：普通撤退退回来处 0 额外分钟；之后独立移动才付其原耗时。</summary>
    [Test]
    public void Q735_A04_WorldRetreat_ZeroMinutes_LaterTravelPaysOwnCost()
    {
        var s = New(52004);
        E.DebugTeleport(s, "camp");
        s.GameMinute = At(2, 9, 0);
        s.SpawnNextCheck = int.MaxValue;
        s.PatrolNextCheck = int.MaxValue;
        s.WorldEnemies.Clear();
        Start(s, ActionRequest.Travel("field"));
        Run(s);
        var travel = s.GameMinute - At(2, 9, 0);
        Assert.That(travel, Is.GreaterThan(0));
        var id = E.DebugSpawnEnemy(s, "shambler", "field").Id;
        Assert.That(E.DebugStartBattle(s, new List<int> { id }, GameEngine.FearEncounter), Is.True);
        var m0 = s.GameMinute;
        Ready(s);
        Force(s, Below(0.0001));
        Assert.That(Cmd(s, "retreat").Ok, Is.True);
        Assert.That(s.CurrentLocation, Is.EqualTo("camp"));
        Assert.That(s.GameMinute, Is.EqualTo(m0), "撤退原落点、0 额外分钟");
        Start(s, ActionRequest.Travel("field"));
        Run(s);
        Assert.That(s.GameMinute - m0, Is.EqualTo(travel), "之后独立移动付原时间，不拿来代替战斗 5 分钟");
    }

    // ───────────── Q735-A05 ─────────────

    /// <summary>Q735-A05：旧在战存档（有真实旧 Ticks）读档后只冻结未来：旧世界钟保留、Ticks 不再增长，顶部与结算显示可信已耗。</summary>
    [Test]
    public void Q735_A05_LegacyActiveBattle_KeepsElapsed_FreezesFuture()
    {
        var s = Fight(52005, "stag");
        Ready(s);
        s.Battle!.Ticks = 3;   // 模拟上线前已真实结算 3 次轮末 tick 的旧在战存档
        var t = SaveSerializer.Deserialize(SaveSerializer.Serialize(s));
        Assert.That(t.Battle!.Ticks, Is.EqualTo(3));
        Assert.That(E.BattleWorldTimeText(t.Battle), Is.EqualTo("已耗15分钟，后续冻结"));
        var m0 = t.GameMinute;
        Rounds(t, 1);
        Assert.That(t.Battle, Is.Not.Null);
        Assert.That((t.GameMinute, t.Battle!.Ticks), Is.EqualTo((m0, 3)), "读档不补 tick、未来 0 分钟");
        var round = t.Battle.Round;
        RetreatNow(t);
        Assert.That(t.GameMinute, Is.EqualTo(m0));
        Assert.That(t.Log.Last(l => l.Text.StartsWith("撤退成功")).Text, Does.StartWith($"撤退成功：共{round}回合（世界时间已耗15分钟，后续冻结）"));
    }

    /// <summary>Q735-A05／A06：新战斗（没有旧 Ticks）顶部值“冻结”。</summary>
    [Test]
    public void Q735_A05_NewBattle_ShowsFrozen()
    {
        var s = Fight(52006, "stag");
        Assert.That(E.BattleWorldTimeText(s.Battle!), Is.EqualTo("冻结"));
        Assert.That(GameEngine.BattleTimeLabel, Is.EqualTo("本场世界时间"));
    }

    // ───────────── Q735-A06 ─────────────

    /// <summary>Q735-A06：战斗医疗预估附注换逐字稿 medicalNote，不再说回合末统一推进世界时间；药效条（第一条）保留。</summary>
    [Test]
    public void Q735_A06_MedicalPreviewNote()
    {
        var s = Fight(52007, "stag");
        Ready(s);
        E.DebugGive(s, "bandage", 1);
        SetStats(s, 15, 80, 80, 18, 90, 60);
        var pv = E.PreviewCommand(s, BattleCommand.Of("bandage"));
        Assert.That(pv.Notes, Does.Contain("确认时生效，不推进世界时间。"));
        Assert.That(pv.Notes.Any(n => n.Contains("统一推进") || n.Contains("5分钟")), Is.False);
        Assert.That(pv.Notes.Count, Is.GreaterThanOrEqualTo(2), "药效成本条件原样保留");
    }

    private static string Root()
    {
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "web", "Survival.sln"))) dir = dir.Parent;
        Assert.That(dir, Is.Not.Null, "找到仓库根目录");
        return dir!.FullName;
    }

    private static string Web(params string[] p) => File.ReadAllText(Path.Combine(new[] { Root(), "web", "Survival.Web" }.Concat(p).ToArray()));

    /// <summary>Q735-A06（网页源码）：顶部值读内核冻结文字、撤下“（每回合5分钟）”；系统说明战斗时间段整段换 battleHelp。</summary>
    [Test]
    public void Q735_A06_WebTextsReplaced()
    {
        Assert.That(GameEngine.BattleTimeHelp, Is.EqualTo("战斗中世界时间冻结；回合规则照常，战后不补算。"));
        var panel = Web("Components", "BattlePanel.razor");
        Assert.That(panel, Does.Contain("e.BattleWorldTimeText(b)"));
        Assert.That(panel, Does.Not.Contain("（每回合5分钟）"));
        var help = Web("Services", "HelpCatalog.cs");
        Assert.That(help, Does.Contain("GameEngine.BattleTimeHelp"));
        Assert.That(help, Does.Not.Contain("一整回合（含追击）只推进"));
    }
}
