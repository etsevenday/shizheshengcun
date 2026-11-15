using System.Reflection;
using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// B47b 战斗表现：支援与生命态特写（内核快照部分）。R03 文首 B34 §Q615 表、§Q616、§Q617；R09 §7、文首 B47b Q691—Q694；
/// 验收 Q615-A01／A02、Q617-A01（显示快照来源）、Q691-A01、Q692-A01、Q693-A01、Q694-A01、Q696-A01。开发记录 B47b。
/// </summary>
public class B47bFxTests : BattleTestBase
{
    protected override bool NoEnemyApproach => true;

    private static void Rolls(GameEngine e, params double[] u) => e.TestDeathRolls = new Queue<double>(u);

    private void Attack(GameState s, BattleEnemy e) =>
        typeof(GameEngine).GetMethod("EnemyAttack", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(E, new object[] { s, e, Cfg.Enemies.Single(x => x.Id == e.EnemyId), false });

    private bool RoundBleed(GameState s) =>
        (bool)typeof(GameEngine).GetMethod("RoundStartBleed", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(E, new object[] { s })!;

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

    /// <summary>主角与一名同行者对一只蹒跚者（同行者 1 号位、主角 3 号位）。</summary>
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
        s.Battle!.Allies = new List<string> { mate, "", GameState.PlayerId };
        return s;
    }

    // ───────────── M935：支援特写与结果字 ─────────────

    /// <summary>Q615-A01：自身技能（护身）是支援特写，行动者即受益者只一条，结果字是真实新生效状态名。</summary>
    [Test]
    public void Q615_A01_SelfSkill_SupportCloseUp_OneEntry()
    {
        var s = Fight(4801, "worker");
        Ready(s);
        s.Skills["unarmed"].Level = 5;
        E.DrainBattleFx();
        Assert.That(Cmd(s, "unarmed_05").Ok, Is.True);
        var ev = One(E.DrainBattleFx());
        Assert.That(ev.Motion, Is.EqualTo(BattleFxMotion.Support));
        Assert.That(ev.Look, Is.EqualTo(BattleFxMotion.Support));
        Assert.That(ev.Results, Has.Count.EqualTo(1), "自身同一人不复制");
        Assert.That(ev.Results[0].Target.Id, Is.EqualTo(GameState.PlayerId));
        Assert.That(ev.Results[0].States, Does.Contain("防御").Or.Contain("盾格挡"));
        Assert.That(ev.Results[0].Amount, Is.EqualTo(0), "无回血不造数值");
    }

    /// <summary>Q694-A01：挣脱实际解除减速才写“解除减速”；稳心写真实恐慌差（原 3 最多减 3）。</summary>
    [Test]
    public void Q694_A01_StruggleRemoves_CalmUsesRealDelta()
    {
        var s = Fight(4802, "worker");
        Ready(s);
        s.Skills["unarmed"].Level = 7;
        s.Battle!.PlayerSlowPending = true;
        StandAt(s, 4);
        E.DrainBattleFx();
        Assert.That(Cmd(s, "unarmed_07").Ok, Is.True);
        var ev = One(E.DrainBattleFx());
        Assert.That(ev.Results.Single().Changes, Is.EqualTo(new[] { GameEngine.RemovedText("减速") }));

        var t = Fight(4803, "worker");
        Ready(t);
        Arm2(t, "signal_whistle");
        StandAt(t, 2);
        t.Skills["command"].Level = 9;
        t.Battle!.PlayerAp = 6;
        t.Battle.Panic = 3;
        E.DrainBattleFx();
        Assert.That(Cmd(t, "command_05").Ok, Is.True);
        var calm = One(E.DrainBattleFx());
        Assert.That(calm.Results.Single().Changes, Is.EqualTo(new[] { "恐慌−3.00" }), "按真实前后差，不硬写 8");
    }

    /// <summary>Q615-A01：快速装填沿原装填（短反馈），不播支援特写。</summary>
    [Test]
    public void Q615_A01_QuickReload_IsShortFeedback()
    {
        var s = Fight(4804, "worker");
        Ready(s);
        var wi = Arm(s, "pistol");
        s.Skills["firearms"].Level = 5;
        E.DebugGive(s, "ammo_9mm", 10);
        wi.LoadedAmmo = 0;
        E.DrainBattleFx();
        var r = Cmd(s, "firearms_05");
        Assume.That(r.Ok, r.Reason);
        var fx = E.DrainBattleFx();
        Assert.That(fx.Where(x => !x.Short), Is.Empty);
        Assert.That(fx.Count(x => x.Short), Is.EqualTo(1));
    }

    /// <summary>Q694-A01：协同写“下次行动点+{实际增加量}”，当前 AP 不变；受益者是被协同的同伴。</summary>
    [Test]
    public void Q694_A01_Coordinate_NextApGain()
    {
        var s = PartyFight(4805, out var mate);
        PlayerFirst(s);
        E.BattleStep(s);
        Assume.That(E.AwaitingPlayer(s));
        Arm2(s, "signal_whistle");
        s.Battle!.Allies = new List<string> { mate, GameState.PlayerId };
        s.Skills["command"].Level = 9;
        s.Battle.PlayerAp = 6;
        var ap0 = s.Battle.Fighter(mate).Ap;
        E.DrainBattleFx();
        var cmd = BattleCommand.Of("command_07");
        cmd.AllyTarget = mate;
        var r = E.ExecuteCommand(s, cmd);
        Assume.That(r.Ok, r.Reason);
        var ev = One(E.DrainBattleFx());
        var res = ev.Results.Single(x => x.Target.Id == mate);
        Assert.That(res.Changes, Is.EqualTo(new[] { GameEngine.NextApText(1) }));
        Assert.That(s.Battle.Fighter(mate).Ap, Is.EqualTo(ap0), "不加当前 AP");
    }

    /// <summary>Q615-A02：敌方真实执行的蓄力属支援特写（行动者即受益者），出手之后才记，不是短反馈。</summary>
    [Test]
    public void Q615_A02_EnemyCharge_SupportCloseUp()
    {
        var s = Fight(4806, "boar");
        var e = s.Battle!.Alive.First();
        E.DrainBattleFx();
        var all = new List<BattleFxEvent>();
        for (var i = 0; i < 40 && s.Battle != null && !s.Progress.Dead && !all.Any(x => x.Name.Contains("蓄力")); i++)
        {
            if (E.AwaitingPlayer(s)) { Cmd(s, "guard"); Cmd(s, "end_turn"); }
            else E.BattleStep(s);
            all.AddRange(E.DrainBattleFx());
        }
        var ev = all.FirstOrDefault(x => x.Name.Contains("蓄力"));
        Assume.That(ev, Is.Not.Null, "本局抽到蓄力");
        Assert.That((ev!.Short, ev.Motion, ev.Actor.IsAlly), Is.EqualTo((false, BattleFxMotion.Support, false)));
        Assert.That(ev.Results.Single().Target.Id, Is.EqualTo("e" + e.Index));
    }

    // ───────────── M935：攻击／医疗里合并生命变化（Q691、Q693、Q696） ─────────────

    /// <summary>Q691-A01、Q693-A01、Q696-A01：攻击打到 0 结果“濒死”；再受正伤抵抗为“抵抗致死”并保留真实−伤害；失败为“死亡”（Down）。</summary>
    [Test]
    public void Q693_AttackMergesLife_DyingResistedDead()
    {
        var s = PartyFight(4807, out var mate);
        var e = s.Battle!.Alive.First();
        var m = s.Actor(mate);
        m.Stats.Health = 0.3;
        Rolls(E, 0.0, 0.999);
        E.DrainBattleFx();
        Force(s, Draw(), Below(0.01), Any());
        Attack(s, e);
        var r1 = One(E.DrainBattleFx()).Results.Single(r => r.Target.Id == mate);
        Assert.That((r1.Outcome, r1.Life, r1.Down), Is.EqualTo(("hit", BattleFxLife.Dying, false)));
        Force(s, Draw(), Below(0.01), Any());
        Attack(s, e);
        var r2 = One(E.DrainBattleFx()).Results.Single(r => r.Target.Id == mate);
        Assert.That((r2.Life, r2.Down), Is.EqualTo((BattleFxLife.Resisted, false)));
        Assert.That(r2.Amount, Is.GreaterThan(0), "H0→0 仍显示真实伤害，不按健康差当 0");
        Force(s, Draw(), Below(0.01), Any());
        Attack(s, e);
        var r3 = One(E.DrainBattleFx()).Results.Single(r => r.Target.Id == mate);
        Assert.That((r3.Down, r3.Life), Is.EqualTo((true, (string?)null)));
        Assert.That(GameEngine.LifeWord(null), Is.EqualTo("死亡"));
    }

    /// <summary>Q691-A01：医疗回血使濒死者脱离——并入医疗特写“脱离濒死”，不另播。</summary>
    [Test]
    public void Q691_MedicineRevive_MergedIntoHealCloseUp()
    {
        var s = Fight(4808, "worker");
        Ready(s);
        E.DebugGive(s, "bandage", 1);
        s.Player.Stats.Health = 0;
        s.Player.DyingNotified = true;
        E.DrainBattleFx();
        var o = E.BattleOptions(s).FirstOrDefault(x => x.Action == "bandage" && x.Available);
        Assume.That(o, Is.Not.Null);
        Assert.That(E.ExecuteCommand(s, BattleCommand.Of("bandage", null, o!.ItemInstanceId)).Ok, Is.True);
        var fx = E.DrainBattleFx().Where(x => !x.Short).ToList();
        Assert.That(fx, Has.Count.EqualTo(1), "不重复加独立特写");
        var r = fx[0].Results.Single();
        Assert.That((r.Outcome, r.Life), Is.EqualTo(("heal", BattleFxLife.Revived)));
    }

    // ───────────── M936：战斗内独立生命特写与敌人移出 ─────────────

    /// <summary>Q691-A01：回合开始出血致濒死——独立特写标题“出血”、结果“濒死”，开始前显示快照仍是受伤前健康。</summary>
    [Test]
    public void Q691_A01_BleedToDying_StandaloneCloseUp()
    {
        var s = Fight(4809, "worker");
        s.Player.Stats.Health = 0.2;
        s.Body.BleedTriggers = 2;
        s.Body.BleedDamage = 0.4;
        E.DrainBattleFx();
        RoundBleed(s);
        var ev = One(E.DrainBattleFx());
        Assert.That((ev.Name, ev.Motion), Is.EqualTo(("出血", BattleFxMotion.Life)));
        var r = ev.Results.Single();
        Assert.That((r.Target.Id, r.Life), Is.EqualTo((GameState.PlayerId, BattleFxLife.Dying)));
        Assert.That(ev.View, Is.Not.Null);
        Assert.That(ev.View!.Actor(GameState.PlayerId).Stats.Health, Is.EqualTo(0.2).Within(1e-9), "Q617：特写前显示受伤前");
        Assert.That(s.Player.Stats.Health, Is.EqualTo(0), "领域已先提交");
    }

    /// <summary>Q691-A01：濒死者出血抵抗成功——没有新姿态，不另播独立特写。</summary>
    [Test]
    public void Q691_A01_BleedResisted_NoStandalone()
    {
        var s = Fight(4810, "worker");
        s.Player.Stats.Health = 0;
        s.Player.DyingNotified = true;
        s.Body.BleedTriggers = 2;
        s.Body.BleedDamage = 0.4;
        Rolls(E, 0.0);
        E.DrainBattleFx();
        RoundBleed(s);
        Assert.That(E.DrainBattleFx().Where(x => !x.Short), Is.Empty);
    }

    /// <summary>Q693-A01：敌人出血死亡——独立特写“出血”结果“死亡”；特写前显示里它仍在站位，领域上已移出在场名单。</summary>
    [Test]
    public void Q693_A01_EnemyBleedDeath_ShownUntilResultFrame()
    {
        var s = Fight(4811, "worker", "worker");
        var e = s.Battle!.Alive.First();
        e.Hp = 0.3;
        e.BleedTriggers = 2;
        e.BleedDamage = 0.4;
        E.DrainBattleFx();
        RoundBleed(s);
        var ev = One(E.DrainBattleFx());
        Assert.That(ev.Name, Is.EqualTo("出血"));
        Assert.That(ev.Results.Single().Down, Is.True);
        Assert.That(ev.View!.Battle!.Enemies.Single(x => x.Index == e.Index).Alive, Is.True, "死亡结果帧前仍显示");
        Assert.That(s.Battle!.Enemies.Single(x => x.Index == e.Index).Alive, Is.False);
    }

    /// <summary>Q617-A01：两次出手的显示快照依次是出手前（H 未减）和第一下之后。</summary>
    [Test]
    public void Q617_A01_ViewSnapshots_FollowCommittedOrder()
    {
        var s = Fight(4812, "worker");
        Ready(s);
        var e = s.Battle!.Alive.First();
        e.Hp = 100;
        E.DrainBattleFx();
        var o = E.BattleOptions(s).First(x => x.Group == "attack" && x.Available && x.NeedsTarget && x.Targets.Count > 0);
        Force(s, Below(0.01), AtLeast(0.99), Any(), Any(), Any());
        Assert.That(Cmd(s, o.Action, o.Targets[0]).Ok, Is.True);
        var ev = One(E.DrainBattleFx());
        Assert.That(ev.View!.Battle!.Enemies.Single(x => x.Index == e.Index).Hp, Is.EqualTo(100).Within(1e-9));
        Assert.That(s.Battle!.Enemies.Single(x => x.Index == e.Index).Hp, Is.LessThan(100));
        Assert.That(ev.View.Battle.Log.Count, Is.LessThan(s.Battle.Log.Count), "最近战报只到已展示边界");
    }

    // ───────────── M939：战斗外生命镜头（Q692） ─────────────

    /// <summary>Q692-A01：异地候补单一原因（饥饿）进入濒死——战斗外记一段只读镜头，标识行“{人物} · {地点}”，不改原地／编队。</summary>
    [Test]
    public void Q692_A01_ReserveDying_WorldCloseUp()
    {
        var s = New(4813);
        E.DebugQuickParty(s, 2);
        var mate = s.Party.Single(id => id != GameState.PlayerId);
        s.Party.Remove(mate);   // 留作候补
        var a = s.Actor(mate);
        a.Stats.Health = 0.01;
        a.Stats.Satiety = 0;
        a.Stats.Hydration = 80;
        a.Stats.Wakefulness = 80;
        E.DrainLifeFx();
        Start(s, ActionRequest.Wait(30));
        E.Advance(s);
        var fx = E.DrainLifeFx();
        Assume.That(a.Stats.Health, Is.EqualTo(0));
        var ev = fx.Single(x => x.Actor.Id == mate);
        var r = E.RosterOf(s, mate)!;
        Assert.That((ev.World, ev.Name, ev.Results.Single().Life), Is.EqualTo((true, "饥饿", BattleFxLife.Dying)));
        Assert.That(ev.Where, Is.EqualTo(GameEngine.LifeWhereText(E.ActorDisplayName(s, mate), E.ReserveWhereName(s, r))));
        Assert.That(s.Party, Does.Not.Contain(mate), "不改编队");
        Assert.That(E.DrainLifeFx(), Is.Empty, "取走后不重复");
    }

    /// <summary>Q732（待答复）：多个原因合成的一次身体损伤本批不做独立镜头，只写日志。</summary>
    [Test]
    public void Q732_MultiCause_NoWorldCloseUp()
    {
        var s = New(4814);
        SetStats(s, 0.01, 0, 0, 18, 80, 60);
        E.DrainLifeFx();
        Start(s, ActionRequest.Wait(30));
        E.Advance(s);
        Assume.That(s.Player.Stats.Health, Is.EqualTo(0));
        Assert.That(E.DrainLifeFx(), Is.Empty);
        Assert.That(s.Log.Select(l => l.Text), Has.One.EqualTo(GameEngine.EnterDyingText("你")));
    }

    // ───────────── M937：濒死显示（Q695、Q696） ─────────────

    /// <summary>Q695-A01：濒死时包扎预估列真实回血及「脱离濒死状态」；正常时不列；抵抗率按本人意志、只带一个百分号。</summary>
    [Test]
    public void Q695_A01_BandagePreview_Revive_AndResistanceText()
    {
        var s = New(4814);
        var bu = Cfg.Items.Single(i => i.Id == "bandage").Use!;
        s.Player.Stats.Health = 0;
        s.BandageReadyAt = 0;
        Assert.That(E.MedicineEffectText(s, bu), Does.Contain("健康0.00→").And.EndWith(GameEngine.ReviveText));
        s.Player.Stats.Health = 30;
        Assert.That(E.MedicineEffectText(s, bu), Does.Not.Contain(GameEngine.ReviveText));
        s.Player.Stats.Health = 0;
        s.BandageReadyAt = s.GameMinute + 30;
        Assert.That(E.MedicineEffectText(s, bu), Does.Not.Contain(GameEngine.ReviveText), "没有实际回血不承诺脱离");
        Assert.That(E.ResistanceShort(s.Player), Is.EqualTo($"致死抵抗{E.ResistancePercent(s.Player)}%"));
        Assert.That(E.ResistanceShort(s.Player), Does.Not.Contain("%%"));
        Assert.That(GameEngine.DyingHealthText(100), Is.EqualTo("0/100 · 濒死"));
        Assert.That(GameEngine.DyingIconText, Is.EqualTo("濒死时无法自然恢复健康；需要用药或医疗实际回血。"));
    }

    // ───────────── M940：成长页意志与帮助（Q690、Q697） ─────────────

    /// <summary>Q690-A01／A02：日额余额 60／43／0，跨世界日按原日账；10级隐藏；五格 65／69／73／77／83%，2级当前效果 67%。</summary>
    [Test]
    public void Q690_A01_DayLeft_AndReferenceCells()
    {
        var s = New(4815);
        var me = s.Actor(GameState.PlayerId);
        Assert.That(E.WillpowerDayLeft(s, me), Is.EqualTo(60));
        me.WillpowerXpDay = Clock.Day(s.GameMinute);
        me.WillpowerXpToday = 17;
        Assert.That(E.WillpowerDayLeft(s, me), Is.EqualTo(43));
        Assert.That(E.WillpowerDayLeftText(43), Is.EqualTo("今日还可获得43点（每天最多60点）"));
        me.WillpowerXpToday = 60;
        Assert.That(E.WillpowerDayLeft(s, me), Is.EqualTo(0), "余额0也显示0");
        me.WillpowerXpDay = Clock.Day(s.GameMinute) - 1;
        Assert.That(E.WillpowerDayLeft(s, me), Is.EqualTo(60), "跨世界日沿原日账");
        Assert.That(GameEngine.WillpowerRefLevels.Select(E.WillpowerRefText),
            Is.EqualTo(new[] { "1级 · 致死抵抗65%", "3级 · 致死抵抗69%", "5级 · 致死抵抗73%", "7级 · 致死抵抗77%", "10级 · 致死抵抗83%" }));
        E.DebugSetSkillLevel(s, GameEngine.Willpower, 2);
        Assert.That(E.WillpowerEffectText(me), Is.EqualTo("致死抵抗67%；濒死后每次受到正伤害时判定。"));
        Assert.That(E.WillpowerPersonText(s, me), Is.EqualTo("你：意志2级，致死抵抗67%"));
        E.DebugSetSkillLevel(s, GameEngine.Willpower, 10);
        Assert.That(E.WillpowerDayLeft(s, me), Is.Null, "10级隐藏余额句");
        Assert.That(me.WillpowerXpToday, Is.EqualTo(60), "查看不改日账");
    }
}
