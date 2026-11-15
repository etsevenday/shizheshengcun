using System.Reflection;
using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// B52b 战斗真实生命反馈文字（需求 R260）：R03 文首「2026-11-15 B47b Q729—Q738」§Q729／§Q730／§Q731，R09 同批 §Q732／§Q733／§Q735，
/// 逐字稿同批短句表；验收 Q729-A01／A02、Q730-A01、Q731-A01、Q732-A01／A02、Q733-A01。开发记录 B52b。
/// </summary>
public class B52bTests : BattleTestBase
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

    private void EnemyAct(GameState s, BattleEnemy e, bool pursuit = false) =>
        typeof(GameEngine).GetMethod("EnemyActivation", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(E, new object[] { s, e, pursuit });

    private void Lock(GameState s, BattleEnemy e) =>
        typeof(GameEngine).GetMethod("LockIntent", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(E, new object[] { s, s.Battle!, e });

    /// <summary>把敌人的意图拨到循环第 k 项（0 起）并重新锁定（同 B20EnemyTests）。</summary>
    private void AtStep(GameState s, BattleEnemy e, int k)
    {
        if (s.Battle!.Round - k < 1) s.Battle.Round = k + 1;
        e.JoinRound = s.Battle.Round - k;
        e.Acted = false;
        e.RoundApUsed = 0;
        Lock(s, e);
    }

    /// <summary>拨到第一个意图为 <paramref name="intent"/> 的循环项。</summary>
    private void AtIntent(GameState s, BattleEnemy e, string intent)
    {
        for (var k = 0; k < 8; k++)
        {
            AtStep(s, e, k);
            if (e.Intent == intent) return;
        }
        Assert.Fail($"{e.EnemyId} 循环里没有 {intent}");
    }

    /// <summary>主角与一名同行者对一只蹒跚者（同行者 1 号位、主角 2 号位）。</summary>
    private GameState PartyFight(uint seed, out string mate)
    {
        var s = New(seed);
        UnlockCapacity(s, 3);
        E.DebugQuickParty(s, 2);
        mate = s.Party.Single(id => id != GameState.PlayerId);
        E.DebugTeleport(s, "field");
        s.GameMinute = At(2, 9, 0);
        s.SpawnNextCheck = int.MaxValue;
        s.PatrolNextCheck = int.MaxValue;
        s.WorldEnemies.Clear();
        var id = E.DebugSpawnEnemy(s, "shambler", "field").Id;
        Assert.That(E.DebugStartBattle(s, new[] { id }, GameEngine.FearEncounter), Is.True);
        s.Battle!.Allies = new List<string> { mate, GameState.PlayerId };
        PlayerFirst(s);
        E.BattleStep(s);
        Assume.That(E.AwaitingPlayer(s));
        return s;
    }

    // ───────────── M994：Q729 真实增益短名、Q730 提示贴、Q731 敌方短反馈 ─────────────

    /// <summary>Q729-A01：鼓舞写“命中强化”、总攻令写“伤害强化”，标题仍是技能名；不复制技能名作结果字。</summary>
    [Test]
    public void Q729_A01_Inspire_Accuracy_AllOut_Damage()
    {
        var s = Fight(5201, "worker");
        Ready(s);
        Arm2(s, "signal_whistle");
        StandAt(s, 2);
        s.Skills["command"].Level = 10;
        s.Battle!.PlayerAp = 9;
        E.DrainBattleFx();
        Assert.That(Cmd(s, "command_01").Ok, Is.True);
        var ev = One(E.DrainBattleFx());
        Assert.That(ev.Name, Is.EqualTo("鼓舞"));
        Assert.That(ev.Results.Single().Changes, Is.EqualTo(new[] { GameEngine.GainAccuracy }));
        s.Battle.PlayerAp = 9;
        Assert.That(Cmd(s, "command_10").Ok, Is.True);
        var all = One(E.DrainBattleFx());
        Assert.That(all.Results.Single().Changes, Is.EqualTo(new[] { GameEngine.GainDamage }));
        Assert.That(all.Results.Single().Changes, Does.Not.Contain(all.Name));
    }

    /// <summary>Q729-A01／A02：掩护——被保护者“减伤”“受保护”（按表序），施术者只有“减伤”（不误贴受保护）；同次一段。</summary>
    [Test]
    public void Q729_A02_Cover_ProtectedOnlyOnTarget_CoverOnBoth()
    {
        var s = PartyFight(5202, out var mate);
        Arm(s, "wooden_shield");
        s.Battle!.ActiveWeaponSlot = 1;
        s.Skills["shield"].Level = 10;
        s.Battle.PlayerAp = 9;
        E.DrainBattleFx();
        var cmd = BattleCommand.Of("shield_07");
        cmd.AllyTarget = mate;
        var r = E.ExecuteCommand(s, cmd);
        Assume.That(r.Ok, r.Reason);
        var ev = One(E.DrainBattleFx());
        Assert.That(ev.Results.Single(x => x.Target.Id == mate).Changes, Is.EqualTo(new[] { GameEngine.GainCover, GameEngine.GainProtected }));
        Assert.That(ev.Results.Single(x => x.Target.Id == GameState.PlayerId).Changes, Is.EqualTo(new[] { GameEngine.GainCover }));
    }

    /// <summary>Q729-A01：稳固写“位移抵抗提升”（格挡仍原状态名）；坚守给其他在场队员“减伤”，独自坚守不造队友减伤。</summary>
    [Test]
    public void Q729_A01_Steady_Displace_Stand_CoverOthersOnly()
    {
        var s = Fight(5203, "worker");
        Ready(s);
        Arm(s, "wooden_shield");
        s.Skills["shield"].Level = 10;
        s.Battle!.PlayerAp = 9;
        E.DrainBattleFx();
        Assert.That(Cmd(s, "shield_05").Ok, Is.True);
        var ev = One(E.DrainBattleFx());
        var me = ev.Results.Single();
        Assert.That(me.Changes, Is.EqualTo(new[] { GameEngine.GainDisplace }));
        Assert.That(me.States, Is.Not.Empty, "格挡沿原状态名");
        var u = Fight(5213, "worker");
        Ready(u);
        Arm(u, "wooden_shield");
        u.Skills["shield"].Level = 10;
        u.Battle!.PlayerAp = 9;
        E.DrainBattleFx();
        var rs = Cmd(u, "shield_10");
        Assume.That(rs.Ok, rs.Reason);
        var solo = One(E.DrainBattleFx());
        Assert.That(solo.Results.SelectMany(x => x.Changes), Does.Not.Contain(GameEngine.GainCover), "独自坚守不造队友减伤");

        var t = PartyFight(5204, out var mate);
        Arm(t, "wooden_shield");
        t.Battle!.ActiveWeaponSlot = 1;
        t.Skills["shield"].Level = 10;
        t.Battle.PlayerAp = 9;
        t.Battle.Allies = new List<string> { GameState.PlayerId, mate };   // 坚守只在 1 号位
        E.DrainBattleFx();
        var rt = Cmd(t, "shield_10");
        Assert.That(rt.Ok, Is.True, rt.Reason);
        var stand = One(E.DrainBattleFx());
        Assert.That(stand.Results.Single(x => x.Target.Id == mate).Changes, Is.EqualTo(new[] { GameEngine.GainCover }));
        Assert.That(stand.Results.Single(x => x.Target.Id == GameState.PlayerId).Changes, Does.Not.Contain(GameEngine.GainCover));
    }

    /// <summary>Q729-A01：支撑护带包写“位移抵抗提升”；已有更强更长的同类效果时不算强化（0 变化不写）。</summary>
    [Test]
    public void Q729_A01_BracePack_DisplaceOnlyWhenGained()
    {
        var s = Fight(5205, "shambler", "shambler");
        Ready(s);
        E.CreateItems(s, "brace_pack", 2, ItemInstance.Carried);
        E.DrainBattleFx();
        Assert.That(E.ExecuteCommand(s, BattleCommand.Of("tactic_brace_pack")).Ok, Is.True);
        Assert.That(One(E.DrainBattleFx()).Results.Single().Changes, Is.EqualTo(new[] { GameEngine.GainDisplace }));
        var b = s.Battle!;
        b.Me.DisplaceResist = 99;
        b.Me.DisplaceUntilRound = b.Round + 9;
        b.Me.TacticalRound = 0;
        b.PlayerAp = 6;
        E.DrainBattleFx();
        var r = E.ExecuteCommand(s, BattleCommand.Of("tactic_brace_pack"));
        // 原合法性：已有更强更长的效果时原规则可能拒绝（拒绝不出任何表现）；受理时也不写位移抵抗提升
        Assert.That(E.DrainBattleFx().Where(x => !x.Short).SelectMany(x => x.Results).SelectMany(x => x.Changes), Does.Not.Contain(GameEngine.GainDisplace));
    }

    /// <summary>Q730-A01：鼓舞提示贴写真实“恐慌−{实际减少量}”（剩余回合额度更小按真实差），支援表现、不画回血数字。</summary>
    [Test]
    public void Q730_A01_RallyPatch_RealPanicCut_NoHealVisual()
    {
        var s = Fight(5206, "shambler", "shambler");
        Ready(s);
        E.CreateItems(s, "rally_patch", 2, ItemInstance.Carried);
        var b = s.Battle!;
        b.Panic = 10;
        E.DrainBattleFx();
        Assert.That(E.ExecuteCommand(s, BattleCommand.Of("tactic_rally_patch")).Ok, Is.True);
        var ev = One(E.DrainBattleFx());
        Assert.That(ev.Motion, Is.EqualTo(BattleFxMotion.Support), "不用治疗式表现");
        var r = ev.Results.Single();
        Assert.That((r.Outcome, r.Amount), Is.EqualTo(("effect", 0.0)), "没有健康加号");
        Assert.That(r.Changes, Is.EqualTo(new[] { GameEngine.PanicCutText(6) }));
        b.Me.TacticalRound = 0;
        b.PlayerAp = 3;
        b.Me.RallyAmount = 3;
        Assert.That(E.ExecuteCommand(s, BattleCommand.Of("tactic_rally_patch")).Ok, Is.True);
        Assert.That(One(E.DrainBattleFx()).Results.Single().Changes, Is.EqualTo(new[] { "恐慌−3.00" }), "按真实前后差");
    }

    /// <summary>Q729-A01：敌方蓄力实际完成写“蓄力完成”；敌方增益写“伤害强化”。</summary>
    [Test]
    public void Q729_A01_EnemyCharge_And_Buff()
    {
        var s = Fight(5207, "boar");
        Ready(s);
        var e = s.Battle!.Alive.First();
        AtIntent(s, e, "charge_prepare");
        e.Charged = false;
        E.DrainBattleFx();
        EnemyAct(s, e);
        var ev = One(E.DrainBattleFx());
        Assert.That(ev.Results.Single().Changes, Is.EqualTo(new[] { GameEngine.GainCharged }));

        var t = Fight(5208, "wild_dog_leader");
        Ready(t);
        var d = t.Battle!.Alive.First();
        AtIntent(t, d, "buff");
        d.NextHitFactor = 1;
        E.DrainBattleFx();
        EnemyAct(t, d);
        Assert.That(One(E.DrainBattleFx()).Results.Single().Changes, Is.EqualTo(new[] { GameEngine.GainDamage }));
        // 同名取强：再蓄势不提高倍率，不写结果字
        AtIntent(t, d, "buff");
        E.DrainBattleFx();
        EnemyAct(t, d);
        Assert.That(E.DrainBattleFx().SelectMany(x => x.Results).SelectMany(x => x.Changes), Is.Empty);
    }

    /// <summary>Q729-A01：低吟鼓动给实际被鼓动的同伴“伤害强化”；敌方保护给被保护的同伴“受保护”。</summary>
    [Test]
    public void Q729_A01_EnemyRally_And_Protect()
    {
        var s = Fight(5209, "ec_cantor", "shambler");
        Ready(s);
        var c = s.Battle!.Alive.First(x => x.EnemyId == "ec_cantor");
        AtIntent(s, c, "rally");
        E.DrainBattleFx();
        EnemyAct(s, c);
        var ev = One(E.DrainBattleFx());
        Assert.That(ev.Results, Is.Not.Empty);
        Assert.That(ev.Results.All(r => r.Changes.SequenceEqual(new[] { GameEngine.GainDamage })), Is.True);

        var t = Fight(5210, "z_guard", "shambler");
        Ready(t);
        var g = t.Battle!.Alive.First(x => x.EnemyId == "z_guard");
        AtIntent(t, g, "protect");
        E.DrainBattleFx();
        EnemyAct(t, g);
        var pv = E.DrainBattleFx().Where(x => !x.Short && x.Results.Count > 0).ToList();
        Assume.That(pv, Is.Not.Empty, string.Join(" | ", t.Battle!.Log.TakeLast(2)));
        var res = pv.Single().Results.Single();
        Assert.That(res.Target.Id, Is.Not.EqualTo("e" + g.Index), "受保护给被保护者");
        Assert.That(res.Changes, Is.EqualTo(new[] { GameEngine.GainProtected }));
    }

    /// <summary>Q731-A01：守卫标记真实执行后在行动敌人旁短反馈“标记”；警戒观察（含追击分支）短反馈“警戒观察”；原战报保留、不进特写。</summary>
    [Test]
    public void Q731_A01_EnemyMark_And_Watch_ShortFeedback()
    {
        var s = Fight(5211, "ec_boss_cold");
        Ready(s);
        StandAt(s, 4);
        var e = s.Battle!.Alive.First();
        e.Slot = "1";
        AtIntent(s, e, "mark");
        var n0 = s.Battle.Log.Count;
        E.DrainBattleFx();
        EnemyAct(s, e);
        var fx = E.DrainBattleFx();
        Assert.That(fx.Where(x => !x.Short), Is.Empty, "不出全屏特写");
        var mark = fx.Single(x => x.Short);
        Assert.That((mark.Name, mark.Actor.Id), Is.EqualTo((GameEngine.EnemyMarkShort, "e" + e.Index)));
        Assert.That(s.Battle.Log.Count, Is.GreaterThan(n0), "原战报保留");

        var t = Fight(5212, "shambler");
        Ready(t);
        var w = t.Battle!.Alive.First();
        w.Intent = "watch";
        w.Acted = false;
        w.RoundApUsed = 0;
        E.DrainBattleFx();
        EnemyAct(t, w);
        var watch = E.DrainBattleFx().Single();
        Assert.That((watch.Short, watch.Name), Is.EqualTo((true, GameEngine.EnemyWatchShort)));
        w.Hostile = false;
        w.Acted = false;
        E.DrainBattleFx();
        EnemyAct(t, w, pursuit: true);
        Assert.That(E.DrainBattleFx().Single().Name, Is.EqualTo(GameEngine.EnemyWatchShort), "追击分支真正执行观察");
    }

    // ───────────── M996：Q732 生命特写标题、Q733 战外治疗脱离濒死 ─────────────

    /// <summary>Q732-A01：本人两个真实原因合成的一次身体损伤标题“多原因伤害：饥饿、缺水”；原伤害事件与进入濒死句只一次。</summary>
    [Test]
    public void Q732_A01_MultiCause_Title()
    {
        var s = New(5221);
        SetStats(s, 0.01, 0, 0, 18, 80, 60);
        E.DrainLifeFx();
        Start(s, ActionRequest.Wait(30));
        E.Advance(s);
        Assume.That(s.Player.Stats.Health, Is.EqualTo(0));
        var ev = E.DrainLifeFx().Single();
        Assert.That(ev.Name, Is.EqualTo("多原因伤害：饥饿、缺水"));
        Assert.That(ev.Name, Is.EqualTo(GameEngine.MultiCauseTitle(new[] { "饥饿", "缺水" })));
        Assert.That(ev.Results.Single().Life, Is.EqualTo(BattleFxLife.Dying));
        Assert.That(s.Log.Select(l => l.Text), Has.One.EqualTo(GameEngine.EnterDyingText("你")), "一次身体损伤");
        Assert.That(s.Progress.LastDamageCause, Is.EqualTo("饥饿"), "原死因字段不改");
    }

    /// <summary>Q732-A01：单因不造多因；同伴各取本人真实原因（不只取主角或首因）。</summary>
    [Test]
    public void Q732_A01_SingleCause_And_CompanionOwnCauses()
    {
        var s = New(5222);
        E.DebugQuickParty(s, 2);
        var mate = s.Party.Single(id => id != GameState.PlayerId);
        SetStats(s, 50, 0, 80, 18, 80, 60);
        var a = s.Actor(mate);
        a.Stats.Health = 0.01; a.Stats.Satiety = 80; a.Stats.Hydration = 0; a.Stats.Wakefulness = 0;
        E.DrainLifeFx();
        Start(s, ActionRequest.Wait(30));
        E.Advance(s);
        var ev = E.DrainLifeFx().Single(x => x.Actor.Id == mate);
        Assert.That(ev.Name, Is.EqualTo("多原因伤害：缺水、缺乏睡眠"), "同伴取本人原因");

        var t = New(5223);
        SetStats(t, 0.01, 0, 80, 18, 80, 60);
        E.DrainLifeFx();
        Start(t, ActionRequest.Wait(30));
        E.Advance(t);
        Assert.That(E.DrainLifeFx().Single().Name, Is.EqualTo("饥饿"), "单因用原名");
    }

    /// <summary>Q732-A02：伤因短名取公开 Label、去重复前缀；搜集划伤“搜索：划伤”、勘整“勘整：划伤”（真实进入濒死才有镜头，普通轻伤不新增）。</summary>
    [Test]
    public void Q732_A02_SearchAndSurvey_Titles()
    {
        Assert.That(GameEngine.SearchLifeTitle("划伤"), Is.EqualTo("搜索：划伤"));
        Assert.That(GameEngine.SearchLifeTitle("搜集时划伤"), Is.EqualTo("搜索：划伤"));
        Assert.That(GameEngine.SurveyLifeTitle("勘整时划伤"), Is.EqualTo("勘整：划伤"));

        var s = New(5224);
        E.DebugTeleport(s, "woods");
        E.DebugSetProgress(s, "woods", 25);
        SetStats(s, 0.01, 80, 80, 18, 90, 60);
        E.DrainLifeFx();
        Start(s, ActionRequest.Gather("wood"));
        s.Main!.HazardHit = true;
        Run(s);
        Assume.That(s.Player.Stats.Health, Is.EqualTo(0));
        var g = E.DrainLifeFx().Single();
        Assert.That((g.Name, g.Results.Single().Life), Is.EqualTo(("搜索：划伤", BattleFxLife.Dying)));
        Assert.That(s.Progress.LastDamageCause, Is.EqualTo("搜集时划伤"), "不改伤因本身");

        var v = New(5225);
        E.DebugTeleport(v, "village");
        E.DebugSetProgress(v, "village", 100);
        SetStats(v, 0.01, 80, 80, 18, 90, 60);
        E.DrainLifeFx();
        Start(v, ActionRequest.Survey());
        v.Main!.HazardHit = true;
        Run(v);
        Assume.That(v.Player.Stats.Health, Is.EqualTo(0));
        Assert.That(E.DrainLifeFx().Single().Name, Is.EqualTo("勘整：划伤"));

        var h = New(5226);
        E.DebugTeleport(h, "woods");
        E.DebugSetProgress(h, "woods", 25);
        SetStats(h, 50, 80, 80, 18, 90, 60);
        E.DrainLifeFx();
        Start(h, ActionRequest.Gather("wood"));
        h.Main!.HazardHit = true;
        Run(h);
        Assert.That(E.DrainLifeFx(), Is.Empty, "普通轻伤不新增镜头");
    }

    /// <summary>Q732-A02：同伴尸变——标题“尸变：病毒”、结果“尸变”，标识行带人物与地点；主角尸变终局不播。</summary>
    [Test]
    public void Q732_A02_CompanionZombie_CloseUp()
    {
        var s = New(5227);
        E.DebugQuickParty(s, 2);
        var mate = s.Party.Single(id => id != GameState.PlayerId);
        var a = s.Actor(mate);
        a.Body.Infected = true;
        a.Body.VirusLoad = Cfg.Infection.ZombificationAtLoad;
        var where = E.LocName(s.CurrentLocation);
        E.DrainLifeFx();
        Start(s, ActionRequest.Wait(30));
        E.Advance(s);
        Assume.That(a.Dead, Is.True);
        var ev = E.DrainLifeFx().Single(x => x.Actor.Id == mate);
        Assert.That((ev.World, ev.Name, ev.Results.Single().Life), Is.EqualTo((true, GameEngine.ZombieLifeTitle, BattleFxLife.Zombie)));
        Assert.That(GameEngine.LifeWord(BattleFxLife.Zombie), Is.EqualTo("尸变"));
        Assert.That(ev.Where, Is.EqualTo(GameEngine.LifeWhereText(E.ActorDisplayName(s, mate), where)));

        var p = New(5228);
        p.Body.Infected = true;
        p.Body.VirusLoad = Cfg.Infection.ZombificationAtLoad;
        E.DrainLifeFx();
        Start(p, ActionRequest.Wait(30));
        E.Advance(p);
        Assume.That(p.Progress.Dead, Is.True);
        Assert.That(E.DrainLifeFx(), Is.Empty, "主角尸变终局优先");
    }

    /// <summary>Q733-A01：战外用药使濒死者脱离——标题为本次实际物品名，结果“脱离濒死”，真实正健康差写“+{数值}”；非濒死用药不造镜头。</summary>
    [Test]
    public void Q733_A01_OutsideBandage_Revive_CloseUp()
    {
        var s = New(5229);
        var inst = E.CreateItems(s, "bandage", 2, ItemInstance.Carried).First();
        s.Player.Stats.Health = 0;
        s.Player.DyingNotified = true;
        s.BandageReadyAt = 0;
        E.DrainLifeFx();
        Start(s, ActionRequest.UseInstance(inst.InstanceId));
        Assume.That(s.Player.Stats.Health, Is.GreaterThan(0));
        var ev = E.DrainLifeFx().Single();
        var r = ev.Results.Single();
        Assert.That(ev.Name, Is.EqualTo(E.ItemName("bandage")));
        Assert.That((ev.World, r.Life, r.Outcome), Is.EqualTo((true, BattleFxLife.Revived, "heal")));
        Assert.That(r.Amount, Is.EqualTo(s.Player.Stats.Health).Within(1e-9), "真实正健康差");
        Assert.That(GameEngine.LifeWord(r.Life), Is.EqualTo("脱离濒死"));

        var t = New(5230);
        var i2 = E.CreateItems(t, "bandage", 1, ItemInstance.Carried).First();
        t.Player.Stats.Health = t.Player.Stats.HealthMax / 2;
        t.BandageReadyAt = 0;
        E.DrainLifeFx();
        Start(t, ActionRequest.UseInstance(i2.InstanceId));
        Assert.That(E.DrainLifeFx(), Is.Empty, "非濒死不造脱离濒死");
    }

    /// <summary>Q733：战斗中用药脱离濒死仍并入同次医疗特写（不另造战外镜头，回血数仍由医疗段写）。</summary>
    [Test]
    public void Q733_InBattleRevive_StillMergedIntoHealCloseUp()
    {
        var s = Fight(5231, "worker");
        Ready(s);
        E.CreateItems(s, "bandage", 1, ItemInstance.Carried);
        s.Stats.Health = 0;
        s.Player.DyingNotified = true;
        s.BandageReadyAt = 0;
        s.Battle!.PlayerAp = 6;
        E.DrainBattleFx();
        E.DrainLifeFx();
        var o = E.BattleOptions(s).FirstOrDefault(x => x.Action.Contains("bandage") && x.Available);
        Assume.That(o, Is.Not.Null);
        Assert.That(Cmd(s, o!.Action).Ok, Is.True);
        var ev = One(E.DrainBattleFx());
        var r = ev.Results.Single();
        Assert.That((r.Outcome, r.Life), Is.EqualTo(("heal", BattleFxLife.Revived)));
        Assert.That(E.DrainLifeFx(), Is.Empty);
    }
}

/// <summary>B52b 网页壳部分（源码断言）：R03 §Q737、R09 §Q734／§Q736／§Q738；验收 Q737-A01／A02、Q734-A01／A02、Q736-A01／A02、Q738-A01／A02。</summary>
public class B52bWebTests
{
    private static string Root()
    {
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "web", "Survival.sln"))) dir = dir.Parent;
        Assert.That(dir, Is.Not.Null, "找到仓库根目录");
        return dir!.FullName;
    }

    private static string Web(params string[] p) => File.ReadAllText(Path.Combine(new[] { Root(), "web", "Survival.Web" }.Concat(p).ToArray()));

    /// <summary>Q737-A01／A02：特写全屏盖住战斗信息与普通全记录（不再退层）；播放中滚轮／触屏不传到底层；ESC 一律开菜单冻结当前帧。</summary>
    [Test]
    public void Q737_A01_CloseUpCoversFullRecord_NoScrollDuringPlayback()
    {
        var pb = Web("Services", "GameSession.Playback.cs");
        Assert.That(pb, Does.Not.Contain("FxUnderReading =>"), "撤去退层");
        Assert.That(pb, Does.Contain("public bool FxPaused => MenuOpen || BattleMenuOpen || BattleDrawer == \"help\";"), "菜单与系统子页仍冻结");
        var layer = Web("Components", "BattleFxLayer.razor");
        Assert.That(layer, Does.Not.Contain("\"under\""));
        Assert.That(Web("Components", "BattleScreen.razor"), Does.Contain("var fxLock = G.PlaybackActive;"));
        var css = Web("wwwroot", "css", "battle.css");
        Assert.That(css, Does.Not.Contain(".bfx-layer.under"));
        Assert.That(css, Does.Contain(".bfx-layer, .bfx-hold { touch-action: none; overscroll-behavior: contain; }"));
        var wheel = Web("wwwroot", "js", "wheel.js");
        Assert.That(wheel, Does.Contain("ev.target.closest('.bfx-layer[data-active], .bfx-hold[data-active]')) { ev.preventDefault(); return; }"));
        var app = Web("wwwroot", "js", "app.js");
        Assert.That(app, Does.Not.Contain("fxl.classList.contains('under')"), "按键也归播放层");
        var gs = Web("Components", "GameScreen.razor");
        Assert.That(gs, Does.Not.Contain("FxUnderReading"));
        Assert.That(Web("Services", "GameSession.Keys.cs"), Does.Contain("if (PlaybackActive) return EscOwner.OpenMenu;"));
    }
}
