using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 B21 R65：进入预告与进入提交。出处：R03「2026-10-30 B4」D2「两种ID与状态」、D3「进入前预告与进入提交」、D4 末段；
/// A10「7. 退出、通关出口与重置预告」；开发假设 B21-2、B21-3、B21-12、B21-14、B21-15。验收 DR01、DR03、DR19（部分）。
/// </summary>
public class B21EntryTests : TestBase
{
    /// <summary>白天站在 c03_crypt，入口边已开（FA02 公开路线）。</summary>
    internal static GameState AtCrypt(GameEngine e, uint seed)
    {
        var s = e.NewGame(seed);
        e.DebugLegacyStory(s);
        s.GameMinute = (2 - 1) * 1440 + 9 * 60;
        s.SpawnNextCheck = int.MaxValue;
        s.PatrolNextCheck = int.MaxValue;
        e.DebugTeleport(s, "c03_crypt");
        e.DebugUnlockEdge(s, "c03_crypt__d01_01");
        return s;
    }

    private void Go(GameState s, string to, bool confirm = false)
    {
        Start(s, ActionRequest.Travel(to, confirm));
        Assert.That(Run(s), Is.EqualTo(SegmentStatus.Completed));
        Assert.That(s.CurrentLocation, Is.EqualTo(to));
    }

    /// <summary>站到 01（已探索到 100%，01→02/03 已开）。</summary>
    internal static GameState AtBuffer(GameEngine e, uint seed)
    {
        var s = AtCrypt(e, seed);
        e.DebugTeleport(s, "d01_01");
        e.DebugSetProgress(s, "d01_01", 100);
        return s;
    }

    /// <summary>DR01：看入口预告不建实例、不抽物资；父地点↔01 按正常 10 分钟移动，01 未进危险门返回不扣惩罚、不提交远征。</summary>
    [Test]
    public void DR01_PreviewNoInstance_BufferRoundTripFree()
    {
        var s = AtCrypt(E, 2111);
        var res = s.Resources.ToDictionary(kv => kv.Key, kv => kv.Value.Available);
        var p = E.ExpeditionPreview(s, "d01");
        Assert.That(s.DungeonInstances.ContainsKey("d01"), Is.False, "预览不建实例");
        Assert.That(s.Resources.All(kv => res[kv.Key] == kv.Value.Available), Is.True, "预览不抽物资");
        Assert.That(p.Status, Is.EqualTo(DungeonStatuses.Unopened));
        Assert.That(p.PenaltyText, Does.Contain("饱食−25").And.Contain("水分−30").And.Contain("体力−8").And.Contain("清醒−20").And.Contain("精神−15").And.Contain("健康最多−2").And.Contain("惊魂未定6小时"));
        Assert.That(p.ExtractMinMinutes, Is.EqualTo(30));
        Assert.That(p.ExtractEstimate, Is.EqualTo(30), "从 01 撤出 10 基础分钟→T=30");
        Assert.That(p.Notes, Has.Some.Contains("探索、移动与整备照常消耗"));
        Assert.That(p.ExploreLow, Is.GreaterThan(0).And.LessThanOrEqualTo(p.ExploreHigh));
        Assert.That(E.NeedsExpeditionConfirm(s, "d01_01"), Is.False, "父地点→01 只是预告，正常移动");

        var t0 = s.GameMinute;
        Go(s, "d01_01");
        Assert.That(s.GameMinute - t0, Is.EqualTo(10));
        Assert.That(s.Expedition, Is.Null);
        Assert.That(E.DungeonStatus(s, "d01"), Is.EqualTo(DungeonStatuses.Unopened));
        E.DebugSetProgress(s, "d01_01", 100);
        var before = s.Stats.Clone();
        var t1 = s.GameMinute;
        Go(s, "c03_crypt");
        Assert.That(s.GameMinute - t1, Is.EqualTo(10), "正常 10 分钟，不是 30 分钟撤出");
        Assert.That(s.Stats.Satiety, Is.GreaterThan(before.Satiety - 2), "没有扣惩罚包");
        Assert.That(s.ExpeditionLog, Is.Empty);
        Assert.That(s.AppliedEventKeys, Is.Empty);
    }

    /// <summary>D3：01→危险房间未确认被拒；确认后移动开始即提交（成员快照、轨迹 01、ACTIVE），只提交一次；取消移动也算已开始（B21-3）。</summary>
    [Test]
    public void D3_ConfirmCommitsOnce_SnapshotAndTrail()
    {
        var s = AtBuffer(E, 2112);
        Assert.That(E.NeedsExpeditionConfirm(s, "d01_02"), Is.True);
        Assert.That(E.CanStart(s, ActionRequest.Travel("d01_02")), Is.EqualTo(GameEngine.ExpeditionConfirmReason));
        Assert.That(s.Expedition, Is.Null);
        var pv = E.Preview(s, ActionRequest.Travel("d01_02", true));
        Assert.That(pv.Ok, Is.True, pv.Reason);
        Assert.That(s.Expedition, Is.Null, "预估不提交");

        Start(s, ActionRequest.Travel("d01_02", true));
        var e = s.Expedition!;
        Assert.That(e, Is.Not.Null);
        Assert.That(e.Id, Is.EqualTo($"{s.Seed:x8}:d01:g1:e1"));
        Assert.That(e.Members, Is.EqualTo(new[] { GameState.PlayerId }));
        Assert.That(e.Trail, Is.EqualTo(new[] { "d01_01" }));
        Assert.That(e.StartedMinute, Is.EqualTo(s.GameMinute));
        Assert.That(E.DungeonStatus(s, "d01"), Is.EqualTo(DungeonStatuses.Active));
        Assert.That(s.Log.Last(l => l.Text.StartsWith("远征开始")).Text, Does.Contain("撤出至少30分钟"));
        // 取消这段移动：人还在 01，远征已开始（B21-3）
        Assert.That(E.Cancel(s).Ok, Is.True);
        Assert.That(s.CurrentLocation, Is.EqualTo("d01_01"));
        Assert.That(s.Expedition, Is.Not.Null);
        Assert.That(E.CanStart(s, ActionRequest.Travel("c03_crypt")), Does.Contain("未通关离开要先终止本次远征"));
        // 再走向危险房间不再要确认、不再 +1
        Assert.That(E.NeedsExpeditionConfirm(s, "d01_02"), Is.False);
        Go(s, "d01_02");
        Assert.That(s.Expedition!.Id, Is.EqualTo(e.Id));
        Assert.That(s.DungeonInstances["d01"].ExpeditionSeq, Is.EqualTo(1));
        Assert.That(s.Expedition.Trail, Is.EqualTo(new[] { "d01_01", "d01_02" }));
    }

    /// <summary>DR03：提交后走回 01 再出外部被拒，提示走终止远征（同一惩罚，不绕过确认）；轨迹回头截断。</summary>
    [Test]
    public void DR03_WalkBackTo01_LeaveRejected()
    {
        var s = AtBuffer(E, 2113);
        Go(s, "d01_02", true);
        Go(s, "d01_01");
        Assert.That(s.Expedition!.Trail, Is.EqualTo(new[] { "d01_01" }));
        var why = E.CanStart(s, ActionRequest.Travel("c03_crypt"));
        Assert.That(why, Does.Contain("未通关离开要先终止本次远征").And.Contain("饱食−25").And.Contain("撤出至少30分钟"));
        Assert.That(E.Start(s, ActionRequest.Travel("c03_crypt")).Ok, Is.False);
        Assert.That(s.CurrentLocation, Is.EqualTo("d01_01"));
    }

    /// <summary>B21-12：本代核心记录已领取/核对（通关）后从 01 原路离开不扣惩罚，远征按“通关离开”结束。</summary>
    [Test]
    public void ClearedLeave_ByBuffer_EndsExpeditionFree()
    {
        var s = AtBuffer(E, 2114);
        Go(s, "d01_02", true);
        Go(s, "d01_01");
        E.DebugInstanceFact(s, "d01", GameEngine.RecordClaimedFact);
        Assert.That(E.DungeonStatus(s, "d01"), Is.EqualTo(DungeonStatuses.Cleared));
        var before = s.Stats.Clone();
        Go(s, "c03_crypt");
        Assert.That(s.Expedition, Is.Null);
        Assert.That(s.ExpeditionLog.Single().EndKind, Is.EqualTo("cleared_exit"));
        Assert.That(s.Stats.Satiety, Is.GreaterThan(before.Satiety - 2));
        Assert.That(s.Log.Any(l => l.Text.Contains("通关离开")), Is.True);
        Assert.That(E.DungeonStatus(s, "d01"), Is.EqualTo(DungeonStatuses.Cleared));
    }

    /// <summary>D2：同实例再入是新 expeditionId（e2），但不刷新敌人/箱子（固定战同一 worldEnemyId、伤势保留）；八地牢状态互不影响（DR19 部分）。</summary>
    [Test]
    public void ReEnterSameInstance_NewExpeditionId_NoRespawn()
    {
        var s = AtBuffer(E, 2115);
        Go(s, "d01_02", true);
        var fixedIds = E.FixedEnemiesAt(s, "d01_02").Select(w => w.Id).ToList();
        Assert.That(fixedIds, Is.Not.Empty);
        E.FixedEnemiesAt(s, "d01_02")[0].Hp -= 1;
        var hp = E.FixedEnemiesAt(s, "d01_02")[0].Hp;
        Assert.That(E.DebugMapTeleport(s, "c03_crypt"), Does.Contain("远征直接结束"));
        Assert.That(s.ExpeditionLog.Single().EndKind, Is.EqualTo("dev"));
        Go(s, "d01_01");
        var p = E.ExpeditionPreview(s, "d01");
        Assert.That(p.Continuation, Is.True);
        Assert.That(p.KnownEnemies, Has.Count.EqualTo(1).And.Some.Contains(E.LocName("d01_02")));
        Assert.That(E.NeedsExpeditionConfirm(s, "d01_02"), Is.True, "新远征仍要确认");
        Go(s, "d01_02", true);
        Assert.That(s.Expedition!.Id, Does.EndWith(":g1:e2"));
        Assert.That(E.FixedEnemiesAt(s, "d01_02").Select(w => w.Id), Is.EqualTo(fixedIds), "不刷新敌人");
        Assert.That(E.FixedEnemiesAt(s, "d01_02")[0].Hp, Is.EqualTo(hp), "伤势保留");
        foreach (var d in new[] { "d02", "d03", "d04", "d05", "d06", "d07", "d08" })
            Assert.That(E.DungeonStatus(s, d), Is.EqualTo(DungeonStatuses.Unopened), d);
    }

    /// <summary>D3 警告线：N/W&lt;40、E&lt;8、S&lt;40、M&lt;30、H≤5、V≥80 逐项（边界值不报）；随身药/弹/武器摘要。</summary>
    [Test]
    public void D3_PreviewWarnings_Thresholds()
    {
        var s = AtCrypt(E, 2116);
        SetStats(s, 5.01, 40, 40, 8, 40, 30);
        Assert.That(E.ExpeditionPreview(s, "d01").Members.Single().Warnings, Is.Empty, "边界值不报");
        SetStats(s, 5, 39.9, 39.9, 7.9, 39.9, 29.9);
        E.DebugSetVirus(s, 80);
        var w = E.ExpeditionPreview(s, "d01").Members.Single().Warnings;
        Assert.That(w, Has.Some.Contains("饱食").And.Some.Contains("水分").And.Some.Contains("体力").And.Some.Contains("清醒")
            .And.Some.Contains("精神").And.Some.Contains("健康").And.Some.Contains("病毒"));
        Assert.That(E.ExpeditionPreview(s, "d01").Members.Single().Supplies, Does.StartWith("药品"));
    }

    /// <summary>B21-14：开发传送进危险房间按已开始的远征补提交（轨迹 01→…→目标，不扣包），传到地牢外直接结束。</summary>
    [Test]
    public void DevTeleport_IntoDangerRoom_AutoCommits()
    {
        var s = AtCrypt(E, 2117);
        var note = E.DebugMapTeleport(s, "d01_02");
        Assert.That(note, Does.Contain("按已开始的远征处理"));
        Assert.That(s.Expedition!.Origin, Is.EqualTo("dev"));
        Assert.That(s.Expedition.Trail.First(), Is.EqualTo("d01_01"));
        Assert.That(s.Expedition.Trail.Last(), Is.EqualTo("d01_02"));
        Assert.That(s.AppliedEventKeys, Is.Empty);
        E.DebugMapTeleport(s, "camp");
        Assert.That(s.Expedition, Is.Null);
    }
}
