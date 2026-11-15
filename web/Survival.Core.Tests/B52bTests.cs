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
}
