using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// S14 §14.2/§14.3、R03 §7.2（开发计划 v0.7 M65）：战斗左右分区与带图指令只改界面映射。
/// 选目标、选动作、展开就近预览只调只读接口；本地“执行”与原右栏共用同一真实指令（ExecuteCommand），
/// 一次执行一个 requestId；预览后目标/武器/AP 变化时重验拒绝，不暗换目标、不多扣 AP（R03-A09/A10/A11、S14-A58/A59/A61）。
/// </summary>
public class V10BattleLocalExecTests : BattleTestBase
{
    private static string Snapshot(GameState s) => Survival.Core.Save.SaveSerializer.Serialize(s);

    private GameState Ready(params string[] enemies)
    {
        var s = Fight(4242, enemies);
        ToPlayer(s);
        Assert.That(E.AwaitingPlayer(s), Is.True);
        return s;
    }

    [Test]
    public void SelectAndPreview_DoNotCommit_SingleEnemyNotAutoAttacked()
    {
        var s = Ready("shambler");
        var only = s.Battle!.Alive.Single();
        var before = Snapshot(s);
        // 单敌：界面自动选中目标、选“攻击”并展开预览，都只是只读查询
        var opts = E.BattleOptions(s);
        var attack = opts.Single(o => o.Action == "basic_attack");
        Assert.That(attack.Targets, Does.Contain(only.Index));
        var p = E.PreviewCommand(s, BattleCommand.Of("basic_attack", only.Index));
        Assert.That(p.Ok, Is.True);
        Assert.That(p.Targets.Single().Index, Is.EqualTo(only.Index));
        Assert.That(Snapshot(s), Is.EqualTo(before), "选目标/选动作/预览不扣 AP、不抽随机、不攻击");
    }

    [Test]
    public void LocalExecute_SameRequestOnce_NewRequestSpendsAgain()
    {
        var s = Ready("shambler");
        var b = s.Battle!;
        var idx = b.Alive.Single().Index;
        var ap = b.PlayerAp;
        var cmd = BattleCommand.Of("guard").WithRequestId("bt-local-1");
        Assert.That(E.ExecuteCommand(s, cmd).Ok, Is.True);
        Assert.That(b.PlayerAp, Is.EqualTo(ap - 1));
        // 动画期间/连点：同一请求号再到达只受理一次，不多扣 AP
        Assert.That(E.ExecuteCommand(s, cmd).Duplicate, Is.True);
        Assert.That(b.PlayerAp, Is.EqualTo(ap - 1));
        Assert.That(b.PlayerGuard, Is.True);
        Assert.That(idx, Is.GreaterThan(0));
    }

    [Test]
    public void PreviewThenTargetGone_RevalidatedAndRejected_NoApSpent()
    {
        var s = Ready("shambler", "shambler");
        var b = s.Battle!;
        var target = b.Alive.First(x => x.Slot.StartsWith("F"));
        var p = E.PreviewCommand(s, BattleCommand.Of("basic_attack", target.Index));
        Assert.That(p.Ok, Is.True);
        // 预览后目标倒下：近处确认重验，原目标的旧预览不能被执行，也不暗换到另一只
        target.Hp = 0;
        target.Dead = true;
        var ap = b.PlayerAp;
        var other = b.Alive.Single();
        var otherHp = other.Hp;
        var res = E.ExecuteCommand(s, BattleCommand.Of("basic_attack", target.Index).WithRequestId("stale"));
        Assert.That(res.Ok, Is.False);
        Assert.That(b.PlayerAp, Is.EqualTo(ap));
        Assert.That(other.Hp, Is.EqualTo(otherHp));
        Assert.That(E.CommandBlocked(s, BattleCommand.Of("basic_attack", target.Index)), Is.EqualTo("目标已不在"));
    }

    [Test]
    public void PreviewThenApChanged_Disabled()
    {
        var s = Ready("shambler");
        var b = s.Battle!;
        var idx = b.Alive.Single().Index;
        Assert.That(E.PreviewCommand(s, BattleCommand.Of("bandage")).Ap, Is.EqualTo(2));
        // 先花 1AP，原本 2AP 的用药预览随之禁用（不可用原因来自真实规则）
        Assert.That(Cmd(s, "guard").Ok, Is.True);
        Assert.That(E.CommandBlocked(s, BattleCommand.Of("bandage")), Does.StartWith("行动点不足"));
        Assert.That(E.PreviewCommand(s, BattleCommand.Of("bandage")).Ok, Is.False);
        Assert.That(idx, Is.GreaterThan(0));
    }

    /// <summary>
    /// B3 H6 改写（原“换武器浮窗就地执行 1AP”）：武器页签切换 0AP、带 requestId 只执行一次、不结束激活；
    /// 随身的第三把武器不出现在指令里，也不能换入。
    /// </summary>
    [Test]
    public void SwitchWeapon_FromNearbyPopover_SameCommandOnce()
    {
        var s = Ready("shambler");
        var b = s.Battle!;
        var knife = E.CreateItems(s, "stone_knife", 1, ItemInstance.EquipOwner(GameState.PlayerId, EquipSlots.Weapon2)).Single();
        E.CreateItems(s, "wooden_spear", 1, ItemInstance.Carried);
        Assert.That(E.BattleOptions(s).Any(o => o.Action == "switch_weapon"), Is.False, "切页不在指令条，随身武器不能换入");
        var ap = b.PlayerAp;
        var cmd = BattleCommand.Of("switch_weapon", null, 2).WithRequestId("sw-1");
        Assert.That(E.ExecuteCommand(s, cmd).Ok, Is.True);
        Assert.That(E.ExecuteCommand(s, cmd).Duplicate, Is.True);
        Assert.That(b.ActiveWeaponSlot, Is.EqualTo(2));
        Assert.That(E.CurrentWeapon(s, out var wi).Id, Is.EqualTo("stone_knife"));
        Assert.That(wi!.InstanceId, Is.EqualTo(knife.InstanceId));
        Assert.That(b.PlayerAp, Is.EqualTo(ap), "切页 0AP");
        Assert.That(E.AwaitingPlayer(s), Is.True, "不结束激活");
    }


    [Test]
    public void LeftRightLayout_DoesNotChangeSlotsOrReach()
    {
        // 前排 F1/F2、后排 B1：屏幕左右只是视觉映射，前排武器打不到后排、槽位与实例 ID 不变
        var s = Ready("shambler", "shambler", "shambler");
        var b = s.Battle!;
        Assert.That(b.Alive.Select(x => x.Slot), Is.EquivalentTo(new[] { "F1", "F2", "B1" }));
        var back = b.Alive.Single(x => x.Slot == "B1");
        var attack = E.BattleOptions(s).Single(o => o.Action == "basic_attack");
        Assert.That(attack.Targets, Does.Not.Contain(back.Index), "徒手只打前排，前排有敌时后排不可选");
        Assert.That(E.CommandBlocked(s, BattleCommand.Of("basic_attack", back.Index)), Does.StartWith("后排"));
    }
}
