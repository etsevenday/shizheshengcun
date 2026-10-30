using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.5 第 4b 节“试玩修复”（M48）里涉及规则的条目：L1 勉强拳击收紧、L3 撤退文案、L7 块根烹饪目标、
/// L8 换武器预估口径、L10 开发入口遵守上限、G4 药品预估与实际一致。L2 见 CombatV06Tests.R613，D1 见显示格式用例。
/// </summary>
public class PlaytestFixV05Tests : BattleTestBase
{
    private void Ready(GameState s)
    {
        PlayerFirst(s);
        E.BattleStep(s);
        Assert.That(E.AwaitingPlayer(s), Is.True);
    }

    /// <summary>L1：v0.5 动作表“仅体力不足正常攻击时”可用勉强拳击；弹仓空而体力充足时应装填或换武器。</summary>
    [Test]
    public void L1_DesperatePunch_OnlyWhenEnergyShort()
    {
        var s = Fight(41, "shambler");
        Ready(s);
        var pistol = Arm(s, "pistol");
        pistol.LoadedAmmo = 0;
        SetStats(s, 20, 80, 80, 18, 90, 60);
        var why = E.CommandBlocked(s, BattleCommand.Of("desperate_punch", 1));
        Assert.That(why, Does.Contain("只在体力不足以使用任何已装备攻击技能时可用").And.Contain("装填"));
        Assert.That(E.CommandBlocked(s, BattleCommand.Of("basic_attack", 1)), Does.Contain("弹仓空"));
        SetStats(s, 20, 80, 80, 0, 90, 60);
        Assert.That(E.CommandBlocked(s, BattleCommand.Of("desperate_punch", 1)), Is.Null, "体力不足时仍可勉强拳击（B528 无死锁）");
    }

    /// <summary>L3：撤退成功时场上没有倒下的敌人，不写“放弃了场上的尸体与掉落”；有倒下的敌人才写。</summary>
    [Test]
    public void L3_RetreatText_OnlyMentionsLootWhenEnemiesFell()
    {
        var s = Fight(42, "shambler");
        Ready(s);
        Force(s, Below(0.0001));
        Assert.That(Cmd(s, "retreat").Ok, Is.True);
        Assert.That(s.Battle, Is.Null, "撤退成功");
        var text = s.Log.Last(l => l.Text.StartsWith("撤退成功")).Text;
        Assert.That(text, Does.Not.Contain("放弃"));

        var t = Fight(21, "shambler", "shambler");
        Ready(t);
        Arm(t, "hatchet");
        Enemy(t, 1).Hp = 0.01;
        Force(t, Below(0.01), AtLeast(0.99), Any(), Any());
        Cmd(t, "basic_attack", 1);
        Assert.That(Enemy(t, 1).Alive, Is.False);
        ToPlayer(t);
        if (t.Battle != null && !E.AwaitingPlayer(t)) Assert.Inconclusive("未轮到玩家");
        if (t.Battle!.PlayerAp < 2) { Cmd(t, "end_turn"); ToPlayer(t); }
        Force(t, Below(0.0001));
        Assert.That(Cmd(t, "retreat").Ok, Is.True);
        Assert.That(t.Battle, Is.Null);
        Assert.That(t.Log.Last(l => l.Text.StartsWith("撤退成功")).Text, Does.Contain("放弃了场上的尸体与掉落"));
    }

    /// <summary>L7：第2日目标“完成一次块根烹饪”（以及沿用同一标记的七日检查）只认块根；只烤肉、煮肉不打勾。</summary>
    [Test]
    public void L7_CookedFlag_RootOnly()
    {
        var s = New();
        E.DebugGive(s, "raw_meat", 1, "camp");
        var fac = s.Locations["camp"].Facility;
        E.DoInstant(s, InstantRequest.PutInFire(First(s, "raw_meat", "camp").InstanceId, 1));
        fac.FireLit = true;
        fac.FuelMinutes = 120;
        Start(s, ActionRequest.Wait(60));
        Run(s);
        Assert.That(Count(s, "cooked_meat"), Is.EqualTo(1));
        Assert.That(s.Progress.Flags, Does.Not.Contain("cooked"), "烤肉不算块根烹饪");

        E.DebugGive(s, "raw_root", 1, "camp");
        E.DoInstant(s, InstantRequest.PutInFire(First(s, "raw_root", "camp").InstanceId, 1));
        fac.FireLit = true;
        fac.FuelMinutes = 120;
        Start(s, ActionRequest.Wait(60));
        Run(s);
        Assert.That(s.Progress.Flags, Does.Contain("cooked"), "块根烤熟才打勾");
    }

    /// <summary>
    /// L8：切换武器页的预估写明切到哪一页、那页是哪一把；武器数值与普通攻击预览同一口径（含熟练度）。
    /// B3 H6 改写：旧“从随身换武器 1AP”改为两个已锁定武器页之间 0AP 切换（武器在开战前已装备到武器Ⅱ）。
    /// </summary>
    [Test]
    public void L8_SwitchWeaponPreview_NamesTarget_SameNumbers()
    {
        var s = Fight(43, "shambler");
        Ready(s);
        var axe = E.CreateItems(s, "hatchet", 1, ItemInstance.EquipOwner(GameState.PlayerId, EquipSlots.Weapon2)).Single();
        var p = E.PreviewCommand(s, BattleCommand.Of("switch_weapon", null, 2));
        Assert.That(p.Title, Does.Contain($"武器Ⅱ页：简易斧#{axe.InstanceId}"));
        Assert.That(p.Ap, Is.EqualTo(0));
        var st = E.WeaponStats(s, E.Weapon("hatchet")!);
        Assert.That(p.Notes[0], Does.Contain($"伤害{Fmt.N(st.DamageMin)}—{Fmt.N(st.DamageMax)}").And.Contain($"命中能力{Fmt.N(st.Accuracy)}"));
        Assert.That(Cmd(s, "switch_weapon", null, 2).Ok, Is.True);

        var atk = E.PreviewCommand(s, BattleCommand.Of("basic_attack", 1)).Targets.Single();
        Assert.That(atk.DamageMin, Is.EqualTo(Math.Max(0, st.DamageMin - E.Enemy("shambler").Armor)).Within(1e-9), "普通攻击伤害=同口径伤害−护甲");
    }

    /// <summary>L10：开发入口放敌人遵守安全区与上限（内核测试布置场景仍可直接放）。</summary>
    [Test]
    public void L10_DevSpawn_RespectsSafeZoneAndCaps()
    {
        var s = New();
        Assert.That(E.DebugSpawnBlocked(s, "camp"), Does.Contain("安全区"));
        s.WorldEnemies.Clear();
        var cap = Cfg.WorldEnemies.CapOf("field");
        for (var i = 0; i < cap; i++) Assert.That(E.DebugSpawnBlocked(s, "field"), Is.Null);
        for (var i = 0; i < cap; i++) E.DebugSpawnEnemy(s, "shambler", "field");
        Assert.That(E.DebugSpawnBlocked(s, "field"), Does.Contain("上限"));
    }

    /// <summary>G4：草药绷带回血冷却中——预估写“本次不回血”并写出会生效的病毒变化，与实际结果一致。</summary>
    [Test]
    public void G4_BandagePreview_MatchesActualEffect()
    {
        var s = New();
        s.Body.Infected = true;
        s.Body.VirusLoad = 12.02;
        SetStats(s, 16.01, 80, 80, 18, 90, 60);
        s.BandageReadyAt = s.GameMinute + 30;
        E.DebugGive(s, "bandage", 1);
        var u = E.Item("bandage").Use!;
        var text = E.MedicineEffectText(s, u)!;
        var expectV = Math.Max(Cfg.Infection.AllSuppressantsMinLoad, 12.02 - E.BandageVirusReduction(s));
        Assert.That(text, Does.Contain("本次不回血").And.Contain($"病毒12.02→{Fmt.N(expectV)}"));
        var p = E.Preview(s, ActionRequest.UseItem("bandage"));
        Assert.That(p.StatsAfterImmediate!.Health, Is.EqualTo(16.01).Within(1e-9), "即时健康不变");
        Start(s, ActionRequest.UseItem("bandage"));
        Assert.That(s.Body.VirusLoad, Is.EqualTo(expectV).Within(1e-9));
        Assert.That(s.Stats.Health, Is.EqualTo(16.01).Within(0.01), "冷却中不回血");
    }
}
