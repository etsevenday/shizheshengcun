using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// MT1 变异补测发现的两个实现错误的回归测试（doc/开发记录/MT1 战斗技能与交战风险变异补测.md）：
/// 1. 应急拳击吃到鼓舞／总攻令却不用掉（A06 command_01「目标下一次攻击命中能力+6」、command_10「全队下一次攻击伤害×1.15」；
///    R03 Q82 只排除反应攻击，RES-Q82e）。
/// 2. 救援医疗箱「稳定」治疗队友时记到施术者身上（A05 rescue_medkit「有效医疗后目标获下一次直接伤害−10%」）。
/// </summary>
public class MT1CombatBugfixTests : BattleTestBase
{
    protected override bool NoEnemyApproach => true;

    private const string Me = GameState.PlayerId;
    private const string Lin = "npc_lin_zhao";
    private const string Zhou = "npc_zhou_yan";

    private void Ready(GameState s)
    {
        PlayerFirst(s);
        E.BattleStep(s);
        Assert.That(E.AwaitingPlayer(s), Is.True);
    }

    private long XpOf(GameState s, string skill) => s.Skills[skill].Level * 100000L + s.Skills[skill].Xp;

    /// <summary>主角武器Ⅰ厨房刀、武器Ⅱ信号哨（当前页）、站 2 号位，指挥 level 级 0 经验。</summary>
    private GameState Commander(uint seed, int level)
    {
        var s = Fight(seed, "worker");
        Ready(s);
        Arm(s, "kitchen_knife");
        var slot = ItemInstance.EquipOwner(Me, EquipSlots.Weapon2);
        foreach (var old in s.Items.Where(i => i.Owner == slot)) old.Owner = ItemInstance.Carried;
        E.CreateItems(s, "signal_whistle", 1, slot);
        s.Battle!.ActiveWeaponSlot = 2;
        StandAt(s, 2);
        s.Skills["command"].Level = level;
        s.Skills["command"].Xp = 0;
        s.Battle.PlayerAp = 9;
        foreach (var e in s.Battle.Alive) e.Hp = 50;
        return s;
    }

    /// <summary>切回武器Ⅰ，体力降到付不起任何已装备攻击技能（R03：应急拳击只在此时可用），对 1 号敌人应急拳击，强制未中（Q82：未命中也算用掉）。</summary>
    private void DesperatePunch(GameState s)
    {
        E.SelectWeaponPage(s, 1);
        s.Stats.Energy = 0;
        s.Battle!.PlayerAp = Math.Max(s.Battle.PlayerAp, 2);
        Force(s, AtLeast(0.999));
        var target = s.Battle.Alive.First().Index;
        Assert.That(E.CommandBlocked(s, BattleCommand.Of("desperate_punch", target)), Is.Null);
        var r = E.ExecuteCommand(s, BattleCommand.Of("desperate_punch", target));
        Assert.That(r.Ok, Is.True, r.Reason);
    }

    [Test]
    public void DesperatePunch_UsesUpInspireAndAllOut()
    {
        var s = Commander(9301, 10);
        var b = s.Battle!;
        Assert.That(E.ExecuteCommand(s, BattleCommand.Of("command_01")).Ok, Is.True);
        Assert.That(E.ExecuteCommand(s, BattleCommand.Of("command_10")).Ok, Is.True);
        Assert.That(GameEngine.InspireActive(b) && GameEngine.AllOutActive(b), Is.True, "起点：两个增益都在");

        DesperatePunch(s);

        Assert.That(b.InspireBonus, Is.EqualTo(0), "应急拳击是下一次攻击，鼓舞用掉");
        Assert.That(b.AllOutFactor, Is.EqualTo(0), "总攻令用掉");
        Assert.That(GameEngine.InspireActive(b) || GameEngine.AllOutActive(b), Is.False);
    }

    /// <summary>鼓舞被应急拳击用掉也算"实际生效"：施术者指挥经验+2（A06 §3），战报一条。</summary>
    [Test]
    public void DesperatePunch_UsingInspire_GrantsCommandXp()
    {
        var s = Commander(9302, 3);
        Assert.That(E.ExecuteCommand(s, BattleCommand.Of("command_01")).Ok, Is.True);
        Assert.That(XpOf(s, "command"), Is.EqualTo(3 * 100000L), "施放时不发经验");

        DesperatePunch(s);

        Assert.That(XpOf(s, "command"), Is.EqualTo(3 * 100000L + 2));
        Assert.That(s.Battle!.Log.Count(l => l.Contains("指挥经验+2（鼓舞生效）")), Is.EqualTo(1));
    }

    /// <summary>三人：你 2 号（武器Ⅱ救援医疗箱，医疗 5 级）治疗林昭：「稳定」×0.9 记在林昭身上，你自己不变。</summary>
    [Test]
    public void RescueMedkit_HealAlly_StabilizeGoesToTarget()
    {
        var s = New(9303);
        E.DebugQuickParty(s, 3);
        E.DebugTeleport(s, "field");
        s.GameMinute = At(2, 9, 0);
        s.SpawnNextCheck = int.MaxValue;
        s.PatrolNextCheck = int.MaxValue;
        s.WorldEnemies.Clear();
        var ids = new[] { E.DebugSpawnEnemy(s, "worker", "field").Id }.ToList();
        Assert.That(E.DebugStartBattle(s, ids, GameEngine.FearEncounter), Is.True);
        var b = s.Battle!;
        b.Allies = new List<string> { Lin, Me, Zhou };
        if (GameEngine.ActingAlly(b) is string cur) { b.Fighter(cur).Acting = false; b.Fighter(cur).Ap = 0; b.ActiveAlly = ""; }
        var code = GameEngine.QueueCode(b, Me);
        b.Queue.Remove(code);
        b.Queue.Insert(0, code);
        b.Cursor = 0;
        E.BattleStep(s);
        Assert.That(GameEngine.ActingAlly(b), Is.EqualTo(Me));
        b.Fighter(Me).Ap = 9;
        var slot = ItemInstance.EquipOwner(Me, EquipSlots.Weapon2);
        foreach (var old in s.Items.Where(i => i.Owner == slot).ToList()) old.Owner = ItemInstance.CarriedOwner(Me);
        E.CreateItems(s, "rescue_medkit", 1, slot);
        b.Fighter(Me).ActiveWeaponSlot = 2;
        s.Skills["medicine"].Level = 5;
        E.DebugGive(s, "saline", 3);
        s.Actors[Lin].Stats.Health = 10;

        var r = E.ExecuteCommand(s, new BattleCommand { Action = "medicine_05", AllyTarget = Lin });
        Assert.That(r.Ok, Is.True, r.Reason);
        Assert.That(s.Actors[Lin].Stats.Health, Is.GreaterThan(10), "有效医疗");

        Assert.That(b.Fighter(Lin).NextDamageFactor, Is.EqualTo(0.9).Within(1e-9), "A05：目标获下一次直接伤害−10%");
        Assert.That(b.Fighter(Me).NextDamageFactor, Is.EqualTo(1), "施术者不受益");
    }
}
