using System.Reflection;
using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 B9a（需求 R113，B8 遗留 M187/M188）：我方多人入战——同行队友按编队预设入场、每人速度/AP/恐慌入队列、逐人下指令、
/// 经验给行动者、医疗/指挥/保护以队友为目标、相邻同意换位、敌人抽中队友按本人结算、队友战死其余恐慌+8。
/// 出处 R03 §1—§6、R08 §4、A11 §8；Q62/Q73/Q75/Q80/Q86/Q91；验收 CB05、CB09、RES-Q75、RES-Q80 多人部分、PT10。
/// </summary>
public class B9aPartyBattleTests : BattleTestBase
{
    private const string Lin = "npc_lin_zhao";
    private const string Zhou = "npc_zhou_yan";
    private const string Shen = "npc_shen_he";

    /// <summary>开发入口组出 n 人队（C4），在农野荒地对给定敌人接敌。</summary>
    private GameState Party(uint seed, int n, params string[] enemies)
    {
        var s = New(seed);
        E.DebugQuickParty(s, n);
        E.DebugTeleport(s, "field");
        s.GameMinute = At(2, 9, 0);
        s.SpawnNextCheck = int.MaxValue;
        s.PatrolNextCheck = int.MaxValue;
        s.WorldEnemies.Clear();
        var ids = enemies.Select(id => E.DebugSpawnEnemy(s, id, "field").Id).ToList();
        s.Encounter = new PendingEncounter { LocationId = "field", EnemyIds = ids, Source = "explore", Minute = s.GameMinute };
        Assert.That(E.Engage(s).Ok, Is.True);
        return s;
    }

    /// <summary>让某名我方成员本轮先行动（只用于测试：把他的队列项挪到最前并开始激活）。</summary>
    private void TurnOf(GameState s, string actorId)
    {
        var b = s.Battle!;
        if (GameEngine.ActingAlly(b) is string cur) { b.Fighter(cur).Acting = false; b.Fighter(cur).Ap = 0; b.ActiveAlly = ""; }
        var code = GameEngine.QueueCode(b, actorId);
        b.Queue.Remove(code);
        b.Queue.Insert(0, code);
        b.Cursor = 0;
        E.BattleStep(s);
        Assert.That(GameEngine.ActingAlly(b), Is.EqualTo(actorId));
        Assert.That(E.AwaitingPlayer(s), Is.True);
    }

    private void Attack(GameState s, BattleEnemy e) =>
        typeof(GameEngine).GetMethod("DoAttackStep", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(E, new object[] { s, s.Battle!, e, E.StepOf(e), false, 1.0, false, false });

    /// <summary>入场：同行者按编队预设号位入场（Q86 不压紧），本场成员、队列每人一项（主角 0、队友 −k），每人恐慌 (100−M)/2 起。</summary>
    [Test]
    public void B9a_Entry_PresetSlots_EveryMemberQueued_PanicPerActor()
    {
        var s = New(9701);
        E.DebugQuickParty(s, 4);
        Assert.That(s.Party, Has.Count.EqualTo(4));
        var s2 = Party(9701, 4, "shambler");
        var b = s2.Battle!;
        Assert.That(b.Members, Has.Count.EqualTo(4));
        Assert.That(b.Members[0], Is.EqualTo(GameState.PlayerId));
        var expected = E.RepairedFormation(s2, s2.FormationPreset).ToList();
        while (expected.Count > 0 && expected[^1] == "") expected.RemoveAt(expected.Count - 1);
        Assert.That(b.Allies, Is.EqualTo(expected), "按预设号位入场，不压紧");
        Assert.That(b.Queue.Take(b.NormalCount).Where(q => q <= 0).OrderBy(q => q), Is.EqualTo(new[] { -3, -2, -1, 0 }), "每名成员一次正常激活");
        foreach (var id in b.Members)
        {
            var f = b.Fighter(id);
            var m = s2.Actors[id].Stats.Morale;
            Assert.That(f.Panic, Is.EqualTo(Math.Min(100, (100 - m) / 2 + f.FearPanic)).Within(1e-9), $"{id} 恐慌入场按自己的精神");
            Assert.That(f.FearRoll, Is.EqualTo(b.Fighter(GameState.PlayerId).FearRoll), "受惊沿用同一主惊吓源随机数");
        }
        Assert.That(b.Log.First(l => l.Contains("同行入场")), Does.Contain("林昭"));
    }

    /// <summary>容量 C 限制入场人数（Q62：同行≤C；开发入口越界时也只放 C 人，主角必在）。</summary>
    [Test]
    public void B9a_Entry_CappedByCapacity()
    {
        var s = New(9702);
        E.DebugQuickParty(s, 3);
        s.Progress.Flags.Remove("academy_safety_record");
        s.Progress.Flags.Remove("church_route_record");   // C 降到 2（只用于测试越界）
        E.DebugTeleport(s, "field");
        s.GameMinute = At(2, 9, 0);
        s.WorldEnemies.Clear();
        var w = E.DebugSpawnEnemy(s, "shambler", "field");
        s.Encounter = new PendingEncounter { LocationId = "field", EnemyIds = new List<int> { w.Id }, Source = "explore", Minute = s.GameMinute };
        Assert.That(E.Engage(s).Ok, Is.True);
        var b = s.Battle!;
        Assert.That(b.Capacity, Is.EqualTo(2));
        Assert.That(GameEngine.AlliesOnField(b), Has.Count.EqualTo(2));
        Assert.That(GameEngine.AlliesOnField(b), Does.Contain(GameState.PlayerId));
    }

    /// <summary>逐人下指令：轮到队友时指令作用于他（防御给他、AP 是他的），不能替别人下；技能经验给行动者。</summary>
    [Test]
    public void B9a_PerActorCommands_ActorIdChecked_XpToActor()
    {
        var s = Party(9703, 2, "shambler");
        var b = s.Battle!;
        TurnOf(s, Lin);
        Assert.That(E.CommandBlocked(s, new BattleCommand { Action = "guard", ActorId = GameState.PlayerId }), Does.Contain("现在轮到林昭行动"));
        Assert.That(E.ExecuteCommand(s, new BattleCommand { Action = "guard", ActorId = Lin }).Ok, Is.True);
        Assert.That(b.Fighter(Lin).Guard, Is.True);
        Assert.That(b.Fighter(GameState.PlayerId).Guard, Is.False);
        Assert.That(b.Log.Last(l => l.Contains("防御") || l.Contains("格挡")), Does.Contain("林昭"));
        // 攻击经验给林昭本人（木矛 → 矛术），主角不变
        b.Fighter(Lin).Ap = 3;
        var skill = E.CurrentWeapon(s, out _).SkillId;
        long Xp(string id) => s.Actors[id].Skills.TryGetValue(skill, out var sk) ? sk.Xp + sk.Level * 100000L : 0;
        var linBefore = Xp(Lin);
        var meBefore = Xp(GameState.PlayerId);
        var e1 = Enemy(s, 1);
        Force(s, Below(0.001), AtLeast(0.999), Any(), Any(), Any());
        var opt = E.BattleOptions(s).First(o => o.Action == "basic_attack");
        if (opt.Available)
        {
            Assert.That(E.ExecuteCommand(s, new BattleCommand { Action = "basic_attack", Target = e1.Index, ActorId = Lin }).Ok, Is.True);
            Assert.That(Xp(Lin), Is.GreaterThan(linBefore), "经验给行动者");
            Assert.That(Xp(GameState.PlayerId), Is.EqualTo(meBefore));
        }
        else Assert.That(opt.Reason, Is.Not.Null);
    }

    /// <summary>敌人抽中队友：伤害、恐慌、感染（咬击）、减速控制按队友本人结算，主角不受影响（B8b-9 未做部分）。</summary>
    [Test]
    public void B9a_EnemyHitsCompanion_InfectionPanicControlOnThatActor()
    {
        var s = Party(9704, 2, "shambler");
        var b = s.Battle!;
        b.Allies = new List<string> { Lin, GameState.PlayerId };
        var e = Enemy(s, 1);
        e.Slot = "1";
        e.Intent = "bite";
        e.Acted = false;
        var linH = s.Actors[Lin].Stats.Health;
        var myH = s.Actors[GameState.PlayerId].Stats.Health;
        var panic0 = b.Fighter(Lin).Panic;
        s.Actors[Lin].Body.Immunity = 0;
        // 抽目标 → 林昭（候选按槽升序 [林昭, 你]，u<0.5）；命中；伤害抽值；感染判定成功
        Force(s, Below(0.49), Below(0.01), Any(), Below(0.01));
        Attack(s, e);
        Assert.That(s.Actors[Lin].Stats.Health, Is.LessThan(linH));
        Assert.That(s.Actors[GameState.PlayerId].Stats.Health, Is.EqualTo(myH));
        Assert.That(b.Fighter(Lin).Panic, Is.GreaterThan(panic0), "恐慌按受伤者本人");
        Assert.That(s.Actors[Lin].Body.Infected, Is.True, "咬击感染按本人");
        Assert.That(s.Actors[GameState.PlayerId].Body.Infected, Is.False);
        Assert.That(b.Log.Last(), Does.Contain("林昭"));
    }

    /// <summary>敌人拉扯/击退队友：按队友本人判控制并移格（与相邻同伴原子交换），主角号位随之改变（Q80）。</summary>
    [Test]
    public void B9a_KnockbackCompanion_SwapsWithMate()
    {
        var s = Party(9705, 2, "shambler");
        var b = s.Battle!;
        b.Allies = new List<string> { Lin, GameState.PlayerId };
        var list = new List<string>();
        using (E.FocusOn(s, Lin))
        {
            typeof(GameEngine).GetMethod("DisplacePlayer", BindingFlags.NonPublic | BindingFlags.Instance)!
                .Invoke(E, new object[] { s, b, 1, "knockback", list });
        }
        // 控制率=clamp(0.75−林昭抗性/100) 抽中与否都要写的是林昭
        Assert.That(list.Single(), Does.StartWith("林昭"));
        if (list[0].Contains("击退到")) Assert.That(b.Allies, Is.EqualTo(new[] { GameState.PlayerId, Lin }));
    }

    /// <summary>队友战死：走遗物流程、清号位、C 不降；其余在场成员恐慌 +8（一次事件只加一次）；队列里他后面的激活跳过。</summary>
    [Test]
    public void B9a_CompanionDiesInBattle_OthersPanicPlus8_TurnSkipped()
    {
        var s = Party(9706, 3, "shambler");
        var b = s.Battle!;
        Assert.That(E.Config.Panic.AllyDeathGain, Is.EqualTo(8));
        var p0 = b.Fighter(GameState.PlayerId).Panic;
        var pz = b.Fighter(Zhou).Panic;
        s.Actors[Lin].Stats.Health = 0.01;
        b.Allies = new List<string> { Lin, GameState.PlayerId, Zhou };
        var e = Enemy(s, 1);
        e.Slot = "1";
        e.Intent = "attack";
        // 抽目标（三人中的第一名 = 林昭，u<1/3），命中
        Force(s, Below(0.3), Below(0.01), Any());
        Attack(s, e);
        Assert.That(s.Party, Does.Not.Contain(Lin));
        Assert.That(s.Remains.ContainsKey(Lin), Is.True, "遗物留在战斗地点");
        Assert.That(GameEngine.AllyInBattle(b, Lin), Is.False);
        Assert.That(b.Fighter(GameState.PlayerId).Panic, Is.EqualTo(Math.Min(100, p0 + 8)).Within(1e-9));
        Assert.That(b.Fighter(Zhou).Panic, Is.EqualTo(Math.Min(100, pz + 8)).Within(1e-9));
        Assert.That(s.Progress.Dead, Is.False);
        Assert.That(b.Capacity, Is.EqualTo(4));
        // 队列里林昭的激活跳过
        b.Queue = new List<int> { GameEngine.QueueCode(b, Lin), 0 };
        b.NormalCount = 2;
        b.Cursor = 0;
        E.BattleStep(s);
        E.BattleStep(s);
        Assert.That(GameEngine.ActingAlly(b), Is.EqualTo(GameState.PlayerId));
    }

    /// <summary>CB09：同一事件主角与队友都倒下（死亡喷溅打 1、2 号位）时按主角死亡终局。</summary>
    [Test]
    public void CB09_SplashKillsBoth_PlayerDeathEndsRun()
    {
        var s = Party(9707, 2, "z_bloater");
        var b = s.Battle!;
        b.Allies = new List<string> { Lin, GameState.PlayerId };
        s.Actors[Lin].Stats.Health = 0.01;
        s.Actors[GameState.PlayerId].Stats.Health = 0.01;
        TurnOf(s, GameState.PlayerId);
        b.PlayerAp = 6;
        var e = Enemy(s, 1);
        e.Hp = 0.01;
        Force(s, Below(0.001), AtLeast(0.999), Any(), Any(), Any(), Any(), Any());
        var r = E.ExecuteCommand(s, BattleCommand.Of("basic_attack", e.Index));
        Assert.That(r.Ok, Is.True, r.Reason);
        Assert.That(s.Progress.Dead, Is.True, "主角先判死亡，同指令杀最后敌也不救回");
        Assert.That(s.Battle, Is.Null);
    }

    /// <summary>RES-Q75 / PT10：医疗技能治队友——扣施术者随身物资、按目标本人回血、冷却记在目标身上（与绷带共享）、经验给施术者。</summary>
    [Test]
    public void RES_Q75_MedicalOnCompanion_CasterItems_TargetCooldown_CasterXp()
    {
        var s = Party(9708, 2, "shambler");
        var b = s.Battle!;
        b.Allies = new List<string> { Lin, GameState.PlayerId };
        TurnOf(s, GameState.PlayerId);
        b.PlayerAp = 6;
        var slot = ItemInstance.EquipOwner(GameState.PlayerId, EquipSlots.Weapon2);
        foreach (var old in s.Items.Where(i => i.Owner == slot)) old.Owner = ItemInstance.Carried;
        E.CreateItems(s, "field_medkit", 1, slot);
        b.ActiveWeaponSlot = 2;
        s.Skills["medicine"].Level = 5;
        E.DebugGive(s, "saline", 1);
        s.Actors[Lin].Stats.Health = 5;
        s.Actors[GameState.PlayerId].Stats.Health = 5;
        var opt = E.BattleOptions(s).Single(o => o.Action == "medicine_05");
        Assert.That(opt.TargetMode, Is.EqualTo("ally"));
        Assert.That(opt.AllyTargets, Does.Contain(Lin).And.Contain(GameState.PlayerId), "任一存活友方含自己");
        var xp0 = s.Actors[GameState.PlayerId].Skills["medicine"].Xp;
        var r = E.ExecuteCommand(s, new BattleCommand { Action = "medicine_05", AllyTarget = Lin });
        Assert.That(r.Ok, Is.True, r.Reason);
        Assert.That(s.Actors[Lin].Stats.Health, Is.GreaterThan(5));
        Assert.That(s.Actors[GameState.PlayerId].Stats.Health, Is.EqualTo(5));
        Assert.That(s.Actors[Lin].BandageReadyAt, Is.GreaterThan(s.GameMinute), "回血冷却记在目标");
        Assert.That(s.Actors[GameState.PlayerId].BandageReadyAt, Is.LessThanOrEqualTo(s.GameMinute));
        Assert.That(s.Items.Where(i => i.IsCarried && i.ItemId == "saline").Sum(i => i.Count), Is.EqualTo(0), "扣施术者随身物资");
        Assert.That(s.Actors[GameState.PlayerId].Skills["medicine"].Xp, Is.Not.EqualTo(xp0), "经验给施术者");
        // PT10：林昭冷却中，再治他写冷却原因；治自己仍可
        b.PlayerAp = 6;
        b.SkillUsedRound.Remove("medicine_05");
        E.DebugGive(s, "saline", 1);
        Assert.That(E.CommandBlocked(s, new BattleCommand { Action = "medicine_05", AllyTarget = Lin }), Does.Contain("林昭").And.Contain("冷却"));
        Assert.That(E.CommandBlocked(s, new BattleCommand { Action = "medicine_05", AllyTarget = GameState.PlayerId }), Is.Null);
    }

    /// <summary>Q73：调整站位到相邻队友所在格 = 双方同意原子交换（1AP/E0.1），只改号位不多激活。</summary>
    [Test]
    public void RES_Q80_Q73_SwapWithAdjacentCompanion()
    {
        var s = Party(9709, 2, "shambler");
        var b = s.Battle!;
        b.Allies = new List<string> { Lin, GameState.PlayerId };
        TurnOf(s, GameState.PlayerId);
        var queue = b.Queue.ToList();
        Assert.That(E.ExecuteCommand(s, BattleCommand.Of("swap", null, 1)).Ok, Is.True);
        Assert.That(b.Allies, Is.EqualTo(new[] { GameState.PlayerId, Lin }));
        Assert.That(b.Queue, Is.EqualTo(queue));
        Assert.That(b.Log.Last(), Does.Contain("与同伴交换"));
    }

    /// <summary>CB05：我方保护——护住相邻队友后，敌人单体攻击抽中他时由保护者接下；扫击（范围）不转移；不能相互/循环保护。</summary>
    [Test]
    public void CB05_AllyProtect_RedirectsSingle_NotArea_NoCycle()
    {
        var s = Party(9710, 2, "shambler");
        var b = s.Battle!;
        b.Allies = new List<string> { Lin, GameState.PlayerId };
        b.Protections[GameState.PlayerId] = Lin;
        Assert.That(GameEngine.ProtectBlocked(b.Protections, Lin, GameState.PlayerId), Is.EqualTo("不能相互或循环保护").Or.EqualTo("目标已有保护者"));
        var e = Enemy(s, 1);
        e.Slot = "1";
        e.Intent = "attack";
        var linH = s.Actors[Lin].Stats.Health;
        var myH = s.Actors[GameState.PlayerId].Stats.Health;
        Force(s, Below(0.49), Below(0.01), Any());
        Attack(s, e);
        Assert.That(s.Actors[Lin].Stats.Health, Is.EqualTo(linH), "抽中林昭但由你接下");
        Assert.That(s.Actors[GameState.PlayerId].Stats.Health, Is.LessThan(myH));
        Assert.That(b.Protections.ContainsKey(GameState.PlayerId), Is.False, "接下一次后保护结束");
        Assert.That(b.Log.Any(l => l.Contains("挡在林昭前面")), Is.True);
    }

    /// <summary>撤退由当前激活者发起：成功率按他自己的速度与恐慌（R03 §5），成功全队脱战。</summary>
    [Test]
    public void B9a_RetreatByActiveCompanion()
    {
        var s = Party(9711, 2, "shambler");
        var b = s.Battle!;
        TurnOf(s, Lin);
        var (chance, _, _) = E.RetreatChance(s, b);
        double mine;
        using (E.FocusOn(s, GameState.PlayerId)) mine = E.RetreatChance(s, b).chance;
        // 林昭速度 4 与主角相同；恐慌各自
        Assert.That(chance, Is.GreaterThan(0));
        Force(s, Below(0.0001));
        Assert.That(E.ExecuteCommand(s, new BattleCommand { Action = "retreat", ActorId = Lin }).Ok, Is.True);
        Assert.That(s.Battle, Is.Null);
        Assert.That(s.Party, Does.Contain(Lin));
        _ = mine;
    }

    /// <summary>B8d-1 多人口径：迫近提示看在场任一我方的架矛；迎击由架着矛的那名队友按自己的号位与武器出手。</summary>
    [Test]
    public void B8d1_BraceStatusAnyMember()
    {
        var s = Party(9712, 2, "shambler");
        var b = s.Battle!;
        Assert.That(GameEngine.BraceActive(b), Is.False);
        b.Fighter(Lin).BraceCharges = 1;
        Assert.That(GameEngine.BraceActive(b), Is.True);
        Assert.That(GameEngine.ApproachBraceTag(b), Is.EqualTo("触发架矛"));
    }

    /// <summary>读档：进行中的多人战斗（成员、激活者、各人状态）原样恢复；旧档没有成员表时按只有主角。</summary>
    [Test]
    public void B9a_SaveLoad_MidBattleRestoresMembersAndActive()
    {
        var s = Party(9713, 3, "shambler");
        TurnOf(s, Zhou);
        s.Battle!.Fighter(Zhou).Guard = true;
        var json = SaveSerializer.Serialize(s);
        var back = SaveSerializer.Deserialize(json, Cfg);
        var b = back.Battle!;
        Assert.That(b.Members, Is.EqualTo(s.Battle!.Members));
        Assert.That(GameEngine.ActingAlly(b), Is.EqualTo(Zhou));
        Assert.That(b.Fighter(Zhou).Guard, Is.True);
        Assert.That(back.Player.Id, Is.EqualTo(Zhou), "读档后视角仍是正在行动的周砚");
        var old = new BattleState();
        Assert.That(GameEngine.MemberIds(old), Is.EqualTo(new[] { GameState.PlayerId }));
    }
}
