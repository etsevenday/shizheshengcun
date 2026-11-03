using Newtonsoft.Json.Linq;
using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 B21 R65：地牢实例 ID、状态与存档 33。出处：R03「2026-10-30 B4 地牢远征、战术品与生命周期」D2/D3/D5.1/D10；
/// S12「B4 撤出事件包」、A01「B4 事件属性索引」；开发假设 B21-1、B21-2、B21-13。验收 DR19/DR20（部分）。
/// </summary>
public class B21StateTests : TestBase
{
    private static readonly string[] Dungeons = { "d01", "d02", "d03", "d04", "d05", "d06", "d07", "d08" };

    /// <summary>把队伍沿已开通的边带到 d01_05：01、02、04 各探索到 100%（开路），实际到过 01/02/04/05。</summary>
    internal static GameState ToD01_05(GameEngine e, GameState s)
    {
        foreach (var n in new[] { "d01_01", "d01_02", "d01_04" })
        {
            e.DebugTeleport(s, n);
            e.DebugSetProgress(s, n, 100);
        }
        e.DebugTeleport(s, "d01_05");
        return s;
    }

    /// <summary>做成一份“存档 32”：去掉 B21 新字段后改版本号。</summary>
    private static string AsSchema32(GameState s)
    {
        var j = JObject.Parse(SaveSerializer.Serialize(s));
        j["SchemaVersion"] = "32";
        j.Remove("Expedition");
        j.Remove("ExpeditionLog");
        j.Remove("AppliedEventKeys");
        foreach (var p in ((JObject)j["DungeonInstances"]!).Properties())
        {
            var o = (JObject)p.Value;
            o.Remove("Status");
            o.Remove("ExpeditionSeq");
            o.Remove("VisitedRooms");
        }
        foreach (var p in ((JObject)j["Actors"]!).Properties()) ((JObject)p.Value).Remove("ShakenUntil");
        return j.ToString();
    }

    /// <summary>DesignSync：终止惩罚包与惊魂未定按 R03 D5.1（S12/A01 同表）；T 下限 30、取 5；预告警告线按 D3；8 座地牢入口缓冲 = safeNodes 首项 dXX_01，与父地点有双向入口边。</summary>
    [Category("DesignSync")]
    [Test]
    public void DesignSync_PenaltyShakenWarnAndBufferNodes()
    {
        var x = Cfg.DungeonExpedition;
        var p = x.AbortPenalty;
        Assert.That((p.Satiety, p.Hydration, p.Energy, p.Wakefulness, p.Morale, p.HealthMax, p.HealthKeep), Is.EqualTo((-25d, -30d, -8d, -20d, -15d, 2d, 1d)));
        Assert.That((x.ShakenMinutes, x.ShakenPanic, x.ExtractMinMinutes, x.ExtractRoundMinutes), Is.EqualTo((360, 10d, 30, 5)));
        var w = x.Warn;
        Assert.That((w.SatietyBelow, w.HydrationBelow, w.EnergyBelow, w.WakefulnessBelow, w.MoraleBelow, w.HealthAtMost, w.VirusAtLeast), Is.EqualTo((40d, 40d, 8d, 40d, 30d, 5d, 80d)));
        Assert.That(x.BufferNodes.Keys, Is.EquivalentTo(Dungeons));
        foreach (var d in Cfg.Map.Dungeons)
        {
            Assert.That(x.BufferNodes[d.Id], Is.EqualTo(d.SafeNodes[0]).And.EqualTo(d.Id + "_01"));
            Assert.That(E.EdgeBetween(d.SafeNodes[0], d.Parent), Is.Not.Null, d.Id);
            Assert.That(E.EdgeBetween(d.Parent, d.SafeNodes[0]), Is.Not.Null, d.Id);
            Assert.That(E.IsDangerRoom(d.SafeNodes[0]), Is.False);
            Assert.That(E.IsDangerRoom(d.GuardNode), Is.True);
        }
    }

    /// <summary>B21-1：instanceId = 局种子8位十六进制:dungeonId:g代号；没有实例记录按第 1 代；八座地牢状态互不影响。</summary>
    [Test]
    public void DR19_InstanceIdFormat_StatusPerDungeon()
    {
        var s = New(0x1234abcd);
        Assert.That(E.InstanceId(s, "d01"), Is.EqualTo("1234abcd:d01:g1"));
        foreach (var d in Dungeons) Assert.That(E.DungeonStatus(s, d), Is.EqualTo(DungeonStatuses.Unopened));
        E.DebugTeleport(s, "d02_01");
        Assert.That(s.DungeonInstances.ContainsKey("d02"), Is.True, "到达 01 只建实例记录（B11）");
        Assert.That(E.DungeonStatus(s, "d02"), Is.EqualTo(DungeonStatuses.Unopened), "预览/缓冲不提交远征");
        E.DebugTeleport(s, "w10_watchtower");
        Assert.That(E.NewDungeonGeneration(s, "d02"), Is.Null);
        Assert.That(E.InstanceId(s, "d02"), Is.EqualTo("1234abcd:d02:g2"));
        Assert.That(E.InstanceId(s, "d01"), Is.EqualTo("1234abcd:d01:g1"), "各地牢代号独立");
        Assert.That(E.DungeonResetReady(s, "d02"), Is.False, "RESET_READY 属 B22，本批恒假");
    }

    /// <summary>B21-13：存档 32 人在 01——升到 33 不建远征（可免费离开），实例 UNOPENED，不扣包不加时间。</summary>
    [Test]
    public void Migration_32To33_AtBuffer_NoExpedition()
    {
        var s = New(2101);
        E.DebugTeleport(s, "d01_01");
        s.Expedition = null;
        var minute = s.GameMinute;
        var stats = s.Stats.Clone();
        var t = SaveSerializer.Deserialize(AsSchema32(s), Cfg);
        Assert.That(t.SchemaVersion, Is.EqualTo(GameState.CurrentSchema));   // B22 起逐级升到 34
        Assert.That(t.Expedition, Is.Null);
        Assert.That(E.DungeonStatus(t, "d01"), Is.EqualTo(DungeonStatuses.Unopened));
        Assert.That(t.GameMinute, Is.EqualTo(minute));
        Assert.That(t.Stats.Satiety, Is.EqualTo(stats.Satiety));
        Assert.That(t.ExpeditionLog, Is.Empty);
        Assert.That(t.Log.Last().Text, Does.Contain("存档已升级到版本34"));
    }

    /// <summary>B21-13：存档 32 人在 d01_05——补建一次已提交远征（成员=当前同行，轨迹=01→02→04→05），实例 ACTIVE，日志写明；不补扣包。迁移幂等。</summary>
    [Test]
    public void Migration_32To33_InDangerRoom_CommittedExpedition_Idempotent()
    {
        var s = ToD01_05(E, New(2102));
        s.Expedition = null;
        var stats = s.Stats.Clone();
        var t = SaveSerializer.Deserialize(AsSchema32(s), Cfg);
        Assert.That(t.SchemaVersion, Is.EqualTo(GameState.CurrentSchema));   // B22 起逐级升到 34
        var e = t.Expedition!;
        Assert.That(e, Is.Not.Null);
        Assert.That(e.Origin, Is.EqualTo("migrated32"));
        Assert.That(e.Id, Is.EqualTo($"{t.Seed:x8}:d01:g1:e1"));
        Assert.That(e.Members, Is.EqualTo(t.Party));
        Assert.That(e.Trail, Is.EqualTo(new[] { "d01_01", "d01_02", "d01_04", "d01_05" }));
        Assert.That(e.Phase, Is.EqualTo(ExpeditionState.Committed));
        Assert.That(E.DungeonStatus(t, "d01"), Is.EqualTo(DungeonStatuses.Active));
        Assert.That(t.DungeonInstances["d01"].VisitedRooms, Is.SupersetOf(new[] { "d01_01", "d01_02", "d01_04", "d01_05" }));
        Assert.That(t.Stats.Satiety, Is.EqualTo(stats.Satiety), "不补扣包");
        Assert.That(t.Log.Any(l => l.Text.Contains("存档已升级到版本33") && l.Text.Contains("按已开始的远征处理")), Is.True);
        Assert.That(t.AppliedEventKeys, Is.Empty);
        // 幂等：再跑一次不重建、不 +1 序号
        Assert.That(E.MigrateExpeditions32(t), Is.Empty);
        Assert.That(t.Expedition!.Id, Is.EqualTo(e.Id));
        Assert.That(t.DungeonInstances["d01"].ExpeditionSeq, Is.EqualTo(1));
        // 当前版本读档不再迁移
        var u = SaveSerializer.Deserialize(SaveSerializer.Serialize(t), Cfg);
        Assert.That(u.Expedition!.Id, Is.EqualTo(e.Id));
    }

    /// <summary>B21-13：存档 32 本代核心记录已核对 → 33 显示 CLEARED；终局叠加 CAMPAIGN_ENDED。</summary>
    [Test]
    public void Migration_32To33_RecordClaimed_Cleared_DeadEnded()
    {
        var s = New(2103);
        E.DebugTeleport(s, "d01_01");
        E.DebugInstanceFact(s, "d01", GameEngine.RecordClaimedFact);
        E.DebugTeleport(s, "c03_crypt");
        s.Expedition = null;
        var t = SaveSerializer.Deserialize(AsSchema32(s), Cfg);
        Assert.That(E.DungeonStatus(t, "d01"), Is.EqualTo(DungeonStatuses.Cleared));
        Assert.That(t.Expedition, Is.Null);
        t.Progress.Dead = true;
        Assert.That(E.DungeonStatus(t, "d01"), Is.EqualTo(DungeonStatuses.CampaignEnded));
    }

    /// <summary>存档 33 往返：远征、记录、去重键、惊魂截止都存盘（D10 最低保存）。</summary>
    [Test]
    public void Save33_RoundTrip_ExpeditionFields()
    {
        var s = ToD01_05(E, New(2104));
        s.Expedition ??= new ExpeditionState { Id = "x", DungeonId = "d01" };
        s.Expedition.Phase = ExpeditionState.Extracting;
        s.Expedition.ExtractRoute = new List<string> { "d01_05", "d01_04", "d01_02", "d01_01", "c03_crypt" };
        s.Expedition.ExtractLegMinutes = new List<int> { 5, 5, 5, 10 };
        s.Expedition.LockedFactor = 1.5;
        s.Expedition.TotalMinutes = 40;
        s.Expedition.ElapsedMinutes = 15;
        s.Expedition.PenaltyApplied = true;
        s.AppliedEventKeys.Add(s.Expedition.Id + GameEngine.AbortPenaltyKeySuffix);
        s.Player.ShakenUntil = 777;
        s.ExpeditionLog.Add(new ExpeditionRecord { Id = "old", DungeonId = "d01", EndKind = "abort" });
        var t = SaveSerializer.Deserialize(SaveSerializer.Serialize(s), Cfg);
        Assert.That(SaveSerializer.Serialize(t), Is.EqualTo(SaveSerializer.Serialize(s)));
        Assert.That(t.Expedition!.ExtractRoute, Has.Count.EqualTo(5));
        Assert.That(t.Player.ShakenUntil, Is.EqualTo(777));
        var c = s.Clone();
        c.Expedition!.Trail.Add("zz");
        c.AppliedEventKeys.Add("zz");
        c.DungeonInstances["d01"].VisitedRooms.Add("zz");
        Assert.That(s.Expedition.Trail, Does.Not.Contain("zz"), "Clone 深拷贝");
        Assert.That(s.AppliedEventKeys, Does.Not.Contain("zz"));
        Assert.That(s.DungeonInstances["d01"].VisitedRooms, Does.Not.Contain("zz"));
    }
}
