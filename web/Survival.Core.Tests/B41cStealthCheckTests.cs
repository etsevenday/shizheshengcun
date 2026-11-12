using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// B41c 潜行接近与躲避（R05「潜行与躲避判定」「Q420」「Q505」「Q506」；逐字稿 B41「结果、取消与队长回退」「新操作的禁用与拒绝」；
/// 验收 Q342-A03、Q420-A01、Q505-A01、Q505-A02、Q506-A01（潜行部分）、B41-TEXT01）。
/// </summary>
public class B41cStealthCheckTests : B41cTestBase
{
    private static bool Won(GameState t) => t.Log.Any(l => l.Text.Contains("成功，获得先手。") || l.Text.StartsWith("你避开了这处敌踪。"));
    private static bool Lost(GameState t) => Lost(t, 0);
    private static bool Lost(GameState t, int from) => t.Log.Skip(from).Any(l => l.Text.Contains("失败，进入战斗。") || l.Text.Contains("未能避开"));

    [Test]
    public void Rate_FormulaExamples_RoundHalfUp_Clamp()
    {
        Assert.That((E.StealthRate("sneak", 1, 90, 60, 1), E.StealthRate("evade", 1, 90, 60, 1)), Is.EqualTo((50, 60)), "R05 例：潜行1对警惕1");
        Assert.That((E.StealthRate("sneak", 4, 90, 60, 2), E.StealthRate("evade", 4, 90, 60, 2)), Is.EqualTo((60, 70)), "R05 例：潜行4对警惕2");
        Assert.That(E.StealthRate("sneak", 1, 92, 60, 1), Is.EqualTo(51), "0.5 向上");
        Assert.That(E.StealthRate("sneak", 1, 91, 58, 1), Is.EqualTo(50), "0.25−0.4=−0.15 → 49.85 → 50");
        Assert.That((E.StealthRate("sneak", 10, 100, 100, 1), E.StealthRate("evade", 10, 100, 100, 1)), Is.EqualTo((90, 95)), "上限");
        Assert.That((E.StealthRate("sneak", 1, 0, 0, 10), E.StealthRate("evade", 1, 0, 0, 10)), Is.EqualTo((10, 15)), "下限");
    }

    [Test]
    public void Sneak_Success_FirstStrike_Xp20_NotCancellable()
    {
        var (s, key) = OneShambler();
        var title = E.FindEnemyCard(s, key, out _)!.Title;
        var t0 = s.GameMinute;
        var n = s.Log.Count;
        var r = E.StartStealth(s, key, "sneak", "req-1");
        Assert.That(r.Ok, Is.True, r.Reason);
        Assert.That(s.Main!.Label, Is.EqualTo("潜行接近"));
        Assert.That(s.Main.PlannedMinutes, Is.EqualTo(5));
        Assert.That(s.Log.Count, Is.EqualTo(n), "开工不另写开始句，只写判定结果");
        Assert.That(E.StartStealth(s, key, "sneak", "req-1").Ok, Is.True, "重复请求不另开");
        Assert.That(s.NextActionId, Is.EqualTo(s.Main.ActionId + 1));
        Assert.That(E.Cancel(s).Reason, Is.EqualTo("该行动不可中途取消"));
        Assert.That(E.RequestExit(s).Deferred, Is.True, "退出入口不取消：本段做完");
        Assert.That(s.Main, Is.Not.Null);
        AdvanceUntil(s, Won);
        Assert.That(s.GameMinute, Is.EqualTo(t0 + 5));
        Assert.That(s.Log.Select(l => l.Text), Does.Contain($"你潜行接近{title}成功，获得先手。你潜行经验+20。"));
        Assert.That(s.Battle, Is.Not.Null);
        Assert.That(s.Battle!.FirstStrike, Is.EqualTo("ally"));
        Assert.That(s.Battle.Context, Is.EqualTo(GameEngine.FearApproach), "潜行成功受惊 0.5");
        Assert.That(s.Battle.Log.First(), Does.Contain(" · 潜行接近："), "Q444 战报前缀");
        Assert.That(s.Battle.CardKey, Is.EqualTo(key));
        Assert.That(Stealth(s), Is.EqualTo((1, 20L)));
        Assert.That(s.EnemyCards.StealthXpClaims, Is.EqualTo(new[] { key + "@0" }));
        Assert.That(s.Main, Is.Null);
    }

    [Test]
    public void Sneak_Fail_NormalBattle_Xp10()
    {
        var (s, key) = OneShambler();
        var title = E.FindEnemyCard(s, key, out _)!.Title;
        Assert.That(E.StartStealth(s, key, "sneak").Ok, Is.True);
        AdvanceUntil(s, Lost);
        Assert.That(s.Log.Select(l => l.Text), Does.Contain($"你潜行接近{title}失败，进入战斗。你潜行经验+10。"));
        Assert.That(s.Battle!.FirstStrike, Is.Null);
        Assert.That(s.Battle.Context, Is.EqualTo(GameEngine.FearApproach), "潜行失败受惊 0.5");
        Assert.That(s.Battle.Log.First(), Does.Contain(" · 潜行失败："), "Q444 战报前缀");
        Assert.That(Stealth(s), Is.EqualTo((1, 10L)));
    }

    [Test]
    public void Evade_Success_HidesCard_720To1440_Step5_ResetsTimer()
    {
        var (s, key) = OneShambler();
        E.CheckEnemyApproach(s);
        Assert.That(E.CardTimer(s, key)?.Left, Is.Not.Null, "已抽接近时间");
        Assert.That(E.StartStealth(s, key, "evade").Ok, Is.True);
        Assert.That(s.Main!.Label, Is.EqualTo("躲避"));
        AdvanceUntil(s, Won);
        Assert.That(s.Log.Select(l => l.Text), Does.Contain("你避开了这处敌踪。你潜行经验+20。"));
        Assert.That(s.Battle, Is.Null);
        var w = s.WorldEnemies.Single();
        Assert.That(w.EvadeHiddenUntil, Is.Not.Null);
        var d = w.EvadeHiddenUntil!.Value - s.GameMinute;
        Assert.That(d, Is.InRange(720, 1440));
        Assert.That(d % 5, Is.EqualTo(0));
        Assert.That(w.PopAccount, Is.EqualTo("legacy"), "旧 8 地点的敌人记原总账");
        Assert.That(w.Hp, Is.GreaterThan(0));
        Assert.That(E.EnemyCardsHere(s).Select(c => c.Key), Does.Not.Contain(key), "隐藏期间不显示");
        Assert.That(E.CanApproachCard(s, key), Is.EqualTo("这处敌踪暂时不可见。"));
        Assert.That(E.CardTimer(s, key), Is.Null, "本次现身的时限结束");
    }

    [Test]
    public void Evade_Fail_EncounterFear()
    {
        var (s, key) = OneShambler();
        var title = E.FindEnemyCard(s, key, out _)!.Title;
        Assert.That(E.StartStealth(s, key, "evade").Ok, Is.True);
        AdvanceUntil(s, Lost);
        Assert.That(s.Log.Select(l => l.Text), Does.Contain($"你未能避开{title}，进入战斗。你潜行经验+10。"));
        Assert.That(s.Battle!.Context, Is.EqualTo(GameEngine.FearEncounter), "躲避失败受惊 1.0");
        Assert.That(s.Battle.Log.First(), Does.Contain(" · 躲避失败："), "Q444 战报前缀");
        Assert.That(s.Battle.FirstStrike, Is.Null);
    }

    [Test]
    public void Q505_A01_MaxedLeaderNoClaim_ThenLowLeaderGets_ThenClaimed()
    {
        var (s, key) = OneShambler(3);
        UnlockCapacity(s, 3);
        E.DebugRecruit(s, "npc_lin_zhao");
        E.DebugRecruit(s, "npc_tang_li");
        SetStealth(s, GameState.PlayerId, 10);
        // 1. 满级主角判定：0 经验、不占回执
        Assert.That(E.StartStealth(s, key, "evade").Ok, Is.True);
        AdvanceUntil(s, Lost);
        Assert.That(LastLogContaining(s, "未能避开"), Does.EndWith("进入战斗。你的潜行已满级。"));
        Assert.That(s.EnemyCards.StealthXpClaims, Is.Empty);
        RetreatOk(s);
        // 2. 换低级队长再判定：按实际得 10
        Assert.That(E.SaveFormation(s, s.FormationPreset.ToList(), s.FormationVersion, null, "npc_lin_zhao").Ok, Is.True);
        var lin = E.ActorDisplayName(s, "npc_lin_zhao");
        Assert.That(E.StartStealth(s, key, "sneak").Ok, Is.True);
        var n2 = s.Log.Count;
        AdvanceUntil(s, x => Lost(x, n2));
        Assert.That(LastLogContaining(s, "潜行接近"), Does.EndWith($"失败，进入战斗。{lin}潜行经验+10。"));
        Assert.That(Stealth(s, "npc_lin_zhao"), Is.EqualTo((1, 10L)));
        Assert.That(s.EnemyCards.StealthXpClaims, Is.EqualTo(new[] { key + "@0" }));
        RetreatOk(s);
        // 3. 已领：再换人、读档都不再给
        var t = SaveSerializer.Deserialize(SaveSerializer.Serialize(s), CfgV2);
        Assert.That(E.SaveFormation(t, t.FormationPreset.ToList(), t.FormationVersion, null, "npc_tang_li").Ok, Is.True);
        Assert.That(E.StartStealth(t, key, "sneak").Ok, Is.True);
        var n3 = t.Log.Count;
        AdvanceUntil(t, x => Lost(x, n3));
        Assert.That(LastLogContaining(t, "潜行接近"), Does.EndWith("失败，进入战斗。本次现身的潜行经验已获得。"));
        Assert.That(Stealth(t, "npc_tang_li"), Is.EqualTo((1, 0L)));
    }

    [Test]
    public void Q505_A02_NearCap_PartialXpStillClaims()
    {
        var (s, key) = OneShambler();
        // 9 级升 10 级需 500，差 5
        SetStealth(s, GameState.PlayerId, 9, 495);
        Assert.That(E.XpRoomToCap(s.Player, "stealth"), Is.EqualTo(5));
        Assert.That(E.StartStealth(s, key, "sneak").Ok, Is.True);
        AdvanceUntil(s, Won);
        Assert.That(LastLogContaining(s, "潜行接近"), Does.EndWith("获得先手。你潜行经验+5。"));
        Assert.That(Stealth(s), Is.EqualTo((10, 0L)));
        Assert.That(s.EnemyCards.StealthXpClaims, Is.EqualTo(new[] { key + "@0" }), "实际只加部分也算已领");
    }

    [Test]
    public void Cancel_LeaderInvalid_NoRollNoXp_KeepsElapsed()
    {
        var (s, key) = OneShambler(2);
        E.DebugRecruit(s, "npc_lin_zhao");
        Assert.That(E.SaveFormation(s, s.FormationPreset.ToList(), s.FormationVersion, null, "npc_lin_zhao").Ok, Is.True);
        Assert.That(E.StartStealth(s, key, "sneak").Ok, Is.True);
        Assert.That(s.Main!.Stealth!.ActorId, Is.EqualTo("npc_lin_zhao"), "快照队长");
        E.DebugActor(s, "npc_lin_zhao", "kill");
        Assert.That(LastLog(s), Is.EqualTo("原队长已无法带队，小队队长已改为你。"));
        var t0 = s.GameMinute;
        Assert.That(E.Advance(s).Status, Is.EqualTo(SegmentStatus.Aborted));
        Assert.That(s.Log.Select(l => l.Text), Does.Contain("本次潜行接近已取消。队长已失效，请重新选择。已耗时5分钟。"));
        Assert.That(s.GameMinute, Is.EqualTo(t0 + 5));
        Assert.That(s.Battle, Is.Null);
        Assert.That(s.EnemyCards.StealthXpClaims, Is.Empty, "失效取消不记回执");
        Assert.That(Stealth(s), Is.EqualTo((1, 0L)));
    }

    [Test]
    public void Cancel_CardLeft_NoRollNoXp()
    {
        var (s, key) = OneShambler();
        Assert.That(E.StartStealth(s, key, "evade").Ok, Is.True);
        s.WorldEnemies.Single().LocationId = "stream";   // 段内巡游离开
        Assert.That(E.Advance(s).Status, Is.EqualTo(SegmentStatus.Aborted));
        Assert.That(s.Log.Select(l => l.Text), Does.Contain("本次躲避已取消。这处敌踪已无法处理。已耗时5分钟。"));
        Assert.That(s.WorldEnemies.Single().EvadeHiddenUntil, Is.Null);
        Assert.That(s.EnemyCards.StealthXpClaims, Is.Empty);
    }

    [Test]
    public void Rejections_VerbatimOrder()
    {
        var (s, key) = OneShambler(2);
        E.DebugRecruit(s, "npc_lin_zhao");
        Assert.That(E.CanStealthCard(s, key, "sneak", "npc_gu_cheng"), Is.EqualTo("队长已失效，请重新选择。"));
        Assert.That(E.CanStealthCard(s, key, "sneak", "npc_lin_zhao"), Is.EqualTo("当前情况已变化，请重新选择后再操作。"), "提交时队长与界面所见不同");
        var rate = E.StealthRate(s, "sneak", GameState.PlayerId, E.FindEnemyCard(s, key, out _)!.Vigilance);
        Assert.That(E.CanStealthCard(s, key, "sneak", GameState.PlayerId, rate), Is.Null);
        Assert.That(E.CanStealthCard(s, key, "sneak", GameState.PlayerId, rate + 1), Is.EqualTo("当前情况已变化，请重新选择后再操作。"));
        Assert.That(E.CanStealthCard(s, "we:99999", "evade"), Is.EqualTo("这处敌踪已不在这里。"));
        var t0 = s.GameMinute;
        var rng = s.Rng.State;
        Assert.That(E.StartStealth(s, key, "sneak", null, "npc_gu_cheng").Ok, Is.False);
        Assert.That((s.GameMinute, s.Rng.State, s.Main), Is.EqualTo((t0, rng, (ActionState?)null)), "拒绝不耗时、不抽签");
        // 行动暂停中
        Start(s, ActionRequest.Wait(30));
        s.Main!.Status = ActionStatus.Paused;
        Assert.That(E.CanStealthCard(s, key, "sneak"), Is.EqualTo("行动暂停中，请先继续或退出行动。"));
        s.Main.Status = ActionStatus.Running;
        Assert.That(E.CanStealthCard(s, key, "sneak"), Is.EqualTo("当前行动还没结束。"));
    }

    [Test]
    public void Evade_KeyEnemy_Rejected_SneakAllowed()
    {
        // 固定关键敌（教学首战卡）不能躲避，可以潜行接近
        var s = New(7);
        s.SuppressEnemyApproach = true;
        E.DebugTeleport(s, "field");
        var tut = E.EnemyCardsHere(s).FirstOrDefault(c => c.KeyEnemy);
        if (tut == null) Assert.Inconclusive("新局农野荒地没有教学首战卡");
        Assert.That(E.CanStealthCard(s, tut!.Key, "evade"), Is.EqualTo("这是必须战胜的敌踪，无法躲避。"));
        Assert.That(E.CanStealthCard(s, tut.Key, "sneak"), Is.Null);
    }

    [Test]
    public void SwitchOff_NoStealthActions()
    {
        var s = LegacyField();
        var w = Off.DebugSpawnEnemy(s, "shambler", "field");
        Assert.That(Off.StartStealth(s, Key(w), "sneak").Reason, Is.EqualTo("当前无法发起战斗。"));
        var req = ActionRequest.Wait(5);
        req.TargetId = "stealth:sneak:" + Key(w);
        Assert.That(Off.Start(s, req).Reason, Is.EqualTo("未知行动"));
    }

    private static string LastLogContaining(GameState s, string part) => s.Log.Last(l => l.Text.Contains(part)).Text;
}
