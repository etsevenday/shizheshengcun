using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// B41c 小队队长（R08 Q342、R05「队长、潜行与 Q424 练习」；逐字稿 B41「队长与练习潜行」「结果、取消与队长回退」）。
/// </summary>
public class B41cLeaderTests : B41cTestBase
{
    private GameState WithCompanions(GameEngine e)
    {
        var s = e == E ? V2Field() : LegacyField();
        UnlockCapacity(s, 3);
        e.DebugRecruit(s, "npc_lin_zhao");
        e.DebugRecruit(s, "npc_tang_li");
        Assert.That(s.Party, Does.Contain("npc_lin_zhao").And.Contain("npc_tang_li"));
        return s;
    }

    [Test]
    public void SaveFormation_WithLeader_LogVerbatim_ZeroMinutes()
    {
        var s = WithCompanions(E);
        Assert.That(E.SquadLeaderId(s), Is.EqualTo(GameState.PlayerId), "缺省主角");
        var t = s.GameMinute;
        var slots = s.FormationPreset.ToList();
        var r = E.SaveFormation(s, slots, s.FormationVersion, null, "npc_lin_zhao");
        Assert.That(r.Ok, Is.True, r.Reason);
        Assert.That(s.GameMinute, Is.EqualTo(t), "0 世界分钟");
        Assert.That(s.SquadLeader, Is.EqualTo("npc_lin_zhao"));
        var name = E.ActorDisplayName(s, "npc_lin_zhao");
        Assert.That(s.Log.Last().Text, Is.EqualTo("编队已保存：" + E.FormationText(s, s.FormationPreset) + "。小队队长：" + name + "。下一场战斗按此开局。"));
        // 改回主角：只改队长也算改动；主角记为 null
        Assert.That(E.SaveFormation(s, slots, s.FormationVersion, null, GameState.PlayerId).Ok, Is.True);
        Assert.That(s.SquadLeader, Is.Null);
        Assert.That(s.Log.Last().Text, Does.EndWith("。小队队长：你。下一场战斗按此开局。"));
        Assert.That(E.SaveFormation(s, slots, s.FormationVersion, null, GameState.PlayerId).Reason, Is.EqualTo("编队没有改动"));
    }

    [Test]
    public void SaveFormation_InvalidLeader_Rejected_Unchanged()
    {
        var s = WithCompanions(E);
        var slots = s.FormationPreset.ToList();
        var v = s.FormationVersion;
        var r = E.SaveFormation(s, slots, v, null, "npc_gu_cheng");
        Assert.That(r.Ok, Is.False);
        Assert.That(r.Reason, Is.EqualTo("队长已失效，请重新选择。"));
        Assert.That((s.SquadLeader, s.FormationVersion), Is.EqualTo(((string?)null, v)));
    }

    [Test]
    public void LeaderDies_FallsBackToPlayer_AfterDeathLine_Once()
    {
        var s = WithCompanions(E);
        Assert.That(E.SaveFormation(s, s.FormationPreset.ToList(), s.FormationVersion, null, "npc_tang_li").Ok, Is.True);
        var n = s.Log.Count;
        E.DebugActor(s, "npc_tang_li", "kill");
        var added = s.Log.Skip(n).Select(l => l.Text).ToList();
        Assert.That(added.Count, Is.EqualTo(2));
        Assert.That(added[0], Does.Contain("死了"), "先写原死亡结果");
        Assert.That(added[1], Is.EqualTo("原队长已无法带队，小队队长已改为你。"));
        Assert.That(s.SquadLeader, Is.Null);
        Assert.That(E.SquadLeaderId(s), Is.EqualTo(GameState.PlayerId));
    }

    [Test]
    public void LeaderLeavesParty_FallsBack_RejoinNeedsReselect()
    {
        var s = WithCompanions(E);
        Assert.That(E.SaveFormation(s, s.FormationPreset.ToList(), s.FormationVersion, null, "npc_lin_zhao").Ok, Is.True);
        E.DebugToggleActive(s, "npc_lin_zhao");
        Assert.That(s.Log.Last().Text, Is.EqualTo("原队长已无法带队，小队队长已改为你。"));
        Assert.That(s.SquadLeader, Is.Null);
        E.DebugToggleActive(s, "npc_lin_zhao");
        Assert.That(E.SquadLeaderId(s), Is.EqualTo(GameState.PlayerId), "再加入需重新选");
        // 非队长离开不写回退句
        var n = s.Log.Count;
        E.DebugToggleActive(s, "npc_tang_li");
        Assert.That(s.Log.Skip(n).Select(l => l.Text), Has.None.EqualTo("原队长已无法带队，小队队长已改为你。"));
    }

    [Test]
    public void SwitchOff_FormationLogUnchanged_LeaderIgnored()
    {
        var s = WithCompanions(Off);
        var slots = s.FormationPreset.ToList();
        (slots[0], slots[1]) = (slots[1], slots[0]);
        Assert.That(Off.SaveFormation(s, slots, s.FormationVersion, null, "npc_lin_zhao").Ok, Is.True);
        Assert.That(s.SquadLeader, Is.Null, "开关关：没有队长");
        Assert.That(s.Log.Last().Text, Is.EqualTo("编队已保存：" + Off.FormationText(s, s.FormationPreset) + "（下一场战斗按此开局）。"));
    }
}
