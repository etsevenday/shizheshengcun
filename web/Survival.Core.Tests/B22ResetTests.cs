using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 B22 R69/R68：72 世界小时重置、generation+1、旧实例遗物原子迁存、写盘失败回滚与死亡优先。出处：R03 D2/D6/D7.2/D8/D9/D10、A09「E8」、
/// S01「B3/B4 世界时钟与实例原子事务」、A10 §7；开发假设 B22-6—B22-8。验收 DR13—DR18、EC15、SCB03、COMP11、COMP15、COMP17。
/// </summary>
public class B22ResetTests : BattleTestBase
{
    private void Claim(GameState s)
    {
        E.DebugTeleport(s, "d01_08");
        E.DebugSetProgress(s, "d01_08", 100);
        E.DebugInstanceFact(s, "d01", "guardianDefeated");
        Start(s, ActionRequest.Gather("d01_08_unique"));
        Run(s);
        Assert.That(E.DungeonCleared(s, "d01"), Is.True);
    }

    /// <summary>d01 本代已通关（领取核心记录），人回到父地点教堂墓园（开发传送离开，远征结束）。</summary>
    private GameState ClearedAtCrypt(uint seed)
    {
        var s = B21EntryTests.AtCrypt(E, seed);
        UnlockCapacity(s, 3);
        Claim(s);
        E.DebugTeleport(s, "c03_crypt");
        Assert.That(s.Expedition, Is.Null);
        return s;
    }

    private static int Eligible(GameState s) => s.DungeonInstances["d01"].ResetEligibleAt!.Value;

    /// <summary>
    /// DR14/COMP11：通关后 71h55 不可重置；72h 只标“可重置”（RESET_READY）；有人在地牢里照样只标，不踢人、不能确认；
    /// 预告写“可重置于…”/“已可重置”。
    /// </summary>
    [Test]
    public void DR14_7155Not_72hMarked_PeopleInsideNotKicked()
    {
        var s = ClearedAtCrypt(2241);
        var cleared = s.DungeonInstances["d01"].ClearedAt!.Value;
        Assert.That(Eligible(s), Is.EqualTo(cleared + 4320));
        s.GameMinute = cleared + 4315;
        Assert.That(E.DungeonResetReady(s, "d01"), Is.False);
        Assert.That(E.DungeonStatus(s, "d01"), Is.EqualTo(DungeonStatuses.Cleared));
        Assert.That(E.ResetBlocked(s, "d01"), Does.Contain("满72世界小时"));
        Assert.That(E.ExpeditionPreview(s, "d01").ResetText, Does.Contain("可重置于").And.Contain("还要5分钟"));
        s.GameMinute = cleared + 4320;
        Assert.That(E.DungeonResetReady(s, "d01"), Is.True);
        Assert.That(E.DungeonStatus(s, "d01"), Is.EqualTo(DungeonStatuses.ResetReady));
        Assert.That(GameEngine.DungeonStatusName(E.DungeonStatus(s, "d01")), Is.EqualTo("可重置"));
        Assert.That(E.ExpeditionPreview(s, "d01").ResetText, Does.Contain("已可重置"));
        Assert.That(E.ResetBlocked(s, "d01"), Is.Null);
        E.DebugTeleport(s, "d01_01");
        Assert.That(E.DungeonStatus(s, "d01"), Is.EqualTo(DungeonStatuses.ResetReady), "有人在里面也只标可重置");
        Assert.That(E.ResetBlocked(s, "d01"), Does.Contain("全队都在教堂墓园"));
        Assert.That(E.ResetDungeon(s, "d01").Ok, Is.False);
        Assert.That(s.CurrentLocation, Is.EqualTo("d01_01"), "不强踢");
        Assert.That(s.DungeonInstances["d01"].Generation, Is.EqualTo(1));
    }

    /// <summary>DR15：主动终止未通关后等 72h——同一实例继续，早期箱不刷、敌人伤势保留，不触发新代、不可重置。</summary>
    [Test]
    public void DR15_AbortUncleared_Wait72h_SameInstance()
    {
        var s = B21EntryTests.AtBuffer(E, 2242);
        UnlockCapacity(s, 2);
        Start(s, ActionRequest.Travel("d01_02", true));
        Run(s);
        E.DebugWinFixedHere(s);
        E.DebugSetProgress(s, "d01_02", 100);
        Start(s, ActionRequest.Travel("d01_04"));
        Run(s);
        E.DebugSetProgress(s, "d01_04", 100);
        Start(s, ActionRequest.Gather("d01_04_cache0"));
        Run(s);
        Assert.That(E.AbortExpedition(s).Ok, Is.True);
        Run(s);
        Assert.That(s.CurrentLocation, Is.EqualTo("c03_crypt"));
        s.GameMinute += 4320 * 2;
        Assert.That(E.DungeonResetReady(s, "d01"), Is.False);
        Assert.That(E.DungeonStatus(s, "d01"), Is.EqualTo(DungeonStatuses.Active));
        Assert.That(E.ResetBlocked(s, "d01"), Does.Contain("本代已通关"));
        Assert.That(E.ExpeditionPreview(s, "d01").ResetText, Does.Contain("未通关：不计重置时间"));
        Assert.That(s.Resources["d01_04_cache0"].Available, Is.EqualTo(0), "早期箱不刷新");
        Assert.That(s.DungeonInstances["d01"].Generation, Is.EqualTo(1));
    }

    /// <summary>
    /// DR16/COMP17：重置前的旧实例遗留物（地面物、已腐败食物、死在地牢里的队友遗物）清单准确，确认后一次原子迁到父地点“旧实例遗物认领箱”，
    /// 保质不刷新、件数不少（无静默删除）；队友遗物 owner 唯一、只迁一次。
    /// </summary>
    [Test]
    public void DR16_COMP17_LeftoversAtomicToRelicBox_ExpiryKept()
    {
        var s = B21EntryTests.AtCrypt(E, 2243);
        UnlockCapacity(s, 3);
        E.DebugRecruit(s, "npc_lin_zhao");
        E.DebugTeleport(s, "d01_04");
        E.CreateItems(s, "rope", 1, ItemInstance.CarriedOwner("npc_lin_zhao"));
        E.DebugActor(s, "npc_lin_zhao", "kill");
        var mateItems = E.RemainsItems(s, "npc_lin_zhao").Select(i => i.InstanceId).ToList();
        Assert.That(mateItems, Is.Not.Empty);
        var meat = E.CreateItems(s, "dried_meat", 2, "d01_04")[0];
        meat.ExpiresAt = s.GameMinute - 1;   // 已腐败
        E.CreateItems(s, "cloth", 3, "d01_02");
        Claim(s);
        E.DebugTeleport(s, "c03_crypt");
        s.GameMinute = Eligible(s);
        var pv = E.ResetPreview(s, "d01");
        Assert.That(pv.Reason, Is.Null);
        Assert.That(pv.Leftovers, Has.Some.Contains("肉干×2").And.Some.Contains("已腐败"));
        Assert.That(pv.Leftovers, Has.Some.Contains("布料×3（捐献仓地面）"));
        Assert.That(pv.Leftovers, Has.Some.Contains("的遗物"));
        Assert.That(pv.Leftovers, Has.Some.Contains("布料×3（旧路线档案地面）").Or.Some.Contains("缝线×2"), "首通补给留在核心房地面也算");
        Assert.That(pv.SpoiledCount, Is.EqualTo(1));
        Assert.That(pv.Destination, Is.EqualTo("教堂墓园的旧实例遗物认领箱"));
        Assert.That(pv.NotReissued, Has.Some.Contains("首次通关补给"));
        var before = E.DungeonLeftovers(s, "d01").Select(x => (x.Item.InstanceId, x.Item.Count, x.Item.ExpiresAt)).ToList();
        var total = s.Items.Count;
        Assert.That(E.ResetDungeon(s, "d01", "reset-1").Ok, Is.True);
        Assert.That(s.DungeonInstances["d01"].Generation, Is.EqualTo(2));
        var box = E.RemainsItems(s, GameEngine.RelicBoxKey("d01"));
        Assert.That(box.Select(i => (i.InstanceId, i.Count, i.ExpiresAt)), Is.EquivalentTo(before), "全部迁入、保质不变");
        Assert.That(s.Items.Count, Is.EqualTo(total), "无静默删除、无复制");
        Assert.That(s.Remains.ContainsKey("npc_lin_zhao"), Is.False, "队友遗物容器迁空后移除");
        Assert.That(mateItems.All(id => s.Items.Single(i => i.InstanceId == id).Owner == ItemInstance.RemainsOwner(GameEngine.RelicBoxKey("d01"))), Is.True, "owner 唯一");
        Assert.That(s.Remains[GameEngine.RelicBoxKey("d01")].LocationId, Is.EqualTo("c03_crypt"));
        Assert.That(E.DungeonLeftovers(s, "d01"), Is.Empty);
        Assert.That(s.Log.Any(l => l.Text.Contains("地牢重置") && l.Text.Contains("旧实例遗物认领箱")), Is.True);
    }

    /// <summary>DR17：首次奖/蓝图/容量重置三代——campaign_once 只一次；普通箱每代一次；新一代固定战是新敌人 ID；通关时间每代各自从核对起算。</summary>
    [Test]
    public void DR17_ThreeGenerations_CampaignOnce_RepeatablePerGeneration()
    {
        var s = ClearedAtCrypt(2244);
        int Campaign() => s.Progress.Flags.Count(f => f.StartsWith("campaignId:") || f.StartsWith("claim:"));
        var flags = Campaign();
        var firstCloth = s.Items.Count(i => i.ItemId == "cloth");
        var cap = E.BattleCapacity(s);
        E.DebugTeleport(s, "d01_02");
        var oldEnemies = E.FixedEnemiesAt(s, "d01_02").Select(w => w.Id).ToList();
        E.DebugTeleport(s, "c03_crypt");
        for (var gen = 2; gen <= 3; gen++)
        {
            s.GameMinute = Eligible(s);
            Assert.That(E.ResetDungeon(s, "d01").Ok, Is.True);
            Assert.That(s.DungeonInstances["d01"].Generation, Is.EqualTo(gen));
            Assert.That(s.Resources["d01_04_cache0"].Available, Is.EqualTo(2), "普通箱每代补满");
            Assert.That(E.SalvageClaimed(s, "d01"), Is.False, "守卫回收每代可再领");
            E.DebugTeleport(s, "d01_02");
            var now = E.FixedEnemiesAt(s, "d01_02").Select(w => w.Id).ToList();
            Assert.That(now.Intersect(oldEnemies), Is.Empty, "新一代新敌人 ID");
            oldEnemies = now;
            Claim(s);
            E.DebugTeleport(s, "c03_crypt");
            Assert.That(Campaign(), Is.EqualTo(flags), "本局唯一事实不重写");
            Assert.That(s.Items.Count(i => i.ItemId == "cloth"), Is.LessThanOrEqualTo(firstCloth + 1), "首通包不重发");
            Assert.That(E.BattleCapacity(s), Is.EqualTo(cap), "容量不重复加");
        }
    }

    /// <summary>
    /// DR18/SCB03/COMP11/COMP15：写盘失败——返回确认前状态（旧代、遗留物 owner、请求号全原样，不出现两代）；同请求号重试成功只生一代；
    /// 再点同号为重复、换号也不会再生一代（新代未通关）；成功后存读档 owner/代号/剩余时间一致。
    /// </summary>
    [Test]
    public void DR18_SCB03_WriteFailRollsBack_RetryOnce_NoTwoGenerations()
    {
        var s = ClearedAtCrypt(2245);
        E.CreateItems(s, "cloth", 2, "d01_02");
        s.GameMinute = Eligible(s);
        var leftover = E.DungeonLeftovers(s, "d01").Select(x => (x.Item.InstanceId, x.Item.Owner)).ToList();
        var r = E.ResetDungeonAtomic(s, "d01", "req-1", _ => false, out var after);
        Assert.That(r.Ok, Is.False);
        Assert.That(r.Reason, Does.Contain("写盘失败").And.Contain("旧一代"));
        Assert.That(after.DungeonInstances["d01"].Generation, Is.EqualTo(1), "旧代保持");
        Assert.That(E.DungeonCleared(after, "d01"), Is.True);
        Assert.That(E.DungeonLeftovers(after, "d01").Select(x => (x.Item.InstanceId, x.Item.Owner)), Is.EquivalentTo(leftover), "owner 原样");
        Assert.That(after.Remains.ContainsKey(GameEngine.RelicBoxKey("d01")), Is.False, "不半迁");
        Assert.That(E.ResetDungeonAtomic(after, "d01", "req-1", _ => throw new IOException("disk"), out var after2).Ok, Is.False, "抛错同样回滚");
        Assert.That(after2.DungeonInstances["d01"].Generation, Is.EqualTo(1));

        string? saved = null;
        Assert.That(E.ResetDungeonAtomic(after2, "d01", "req-1", st => { saved = SaveSerializer.Serialize(st); return true; }, out var ok).Ok, Is.True);
        Assert.That(ok.DungeonInstances["d01"].Generation, Is.EqualTo(2));
        Assert.That(E.ResetDungeonAtomic(ok, "d01", "req-1", _ => true, out var dup).Duplicate, Is.True, "同号重复");
        Assert.That(E.ResetDungeon(dup, "d01", "req-2").Ok, Is.False, "新一代未通关，换号也不再生一代");
        Assert.That(dup.DungeonInstances["d01"].Generation, Is.EqualTo(2));
        var t = SaveSerializer.Deserialize(saved!, Cfg);
        Assert.That(t.DungeonInstances["d01"].Generation, Is.EqualTo(2));
        Assert.That(E.RemainsItems(t, GameEngine.RelicBoxKey("d01")).Select(i => i.InstanceId), Is.EquivalentTo(E.RemainsItems(ok, GameEngine.RelicBoxKey("d01")).Select(i => i.InstanceId)));
    }

    /// <summary>EC15：冷却只按世界分钟——存读档不变、不受现实时间影响；八座地牢各自计时，重置 d01 不动 d02。</summary>
    [Test]
    public void EC15_CooldownWorldMinutes_IndependentPerDungeon()
    {
        var s = ClearedAtCrypt(2246);
        E.DebugTeleport(s, "d02_08");
        E.DebugSetProgress(s, "d02_08", 100);
        E.DebugInstanceFact(s, "d02", "guardianDefeated");
        Start(s, ActionRequest.Gather("d02_08_unique"));
        Run(s);
        E.DebugTeleport(s, "c03_crypt");
        var e1 = Eligible(s);
        var e2 = s.DungeonInstances["d02"].ResetEligibleAt!.Value;
        Assert.That(e2, Is.GreaterThan(e1));
        var t = SaveSerializer.Deserialize(SaveSerializer.Serialize(s), Cfg);
        Assert.That((t.DungeonInstances["d01"].ResetEligibleAt, t.GameMinute), Is.EqualTo((e1, s.GameMinute)), "读档不快进");
        t.GameMinute = e1;
        Assert.That(E.DungeonResetReady(t, "d01"), Is.True);
        Assert.That(E.DungeonResetReady(t, "d02"), Is.False);
        Assert.That(E.ResetDungeon(t, "d01").Ok, Is.True);
        Assert.That(t.DungeonInstances["d02"].Generation, Is.EqualTo(1));
        Assert.That(E.DungeonCleared(t, "d02"), Is.True, "不互相重置");
    }

    /// <summary>DR13：通关后走 10 分钟单向出口途中死亡——终局优先：不瞬移、不把封存物迁到回收箱、实例记终局。</summary>
    [Test]
    public void DR13_DeathOnExit_NoTransfer_CampaignEnded()
    {
        var s = B21EntryTests.AtCrypt(E, 2247);
        UnlockCapacity(s, 3);
        Claim(s);
        var loot = s.Items.First(i => i.Owner == "d01_08" && i.RewardOrigin != null);
        Assert.That(E.SealReward(s, loot.InstanceId).Ok, Is.True);
        foreach (var st in new[] { StatId.Satiety, StatId.Hydration, StatId.Wakefulness }) s.Player.Stats.Set(st, 0);
        s.Player.Stats.Set(StatId.Health, 0.05);
        Start(s, ActionRequest.Travel("c03_crypt"));
        Run(s);
        Assert.That(s.Progress.Dead, Is.True);
        Assert.That(s.CurrentLocation, Is.Not.EqualTo("c03_crypt"), "不瞬移继续");
        Assert.That(loot.Owner, Does.StartWith(GameEngine.SealedPrefix), "封存物不迁");
        Assert.That(s.Remains.ContainsKey(GameEngine.RecoveryBoxKey("d01")), Is.False);
        Assert.That(E.DungeonStatus(s, "d01"), Is.EqualTo(DungeonStatuses.CampaignEnded));
        Assert.That(E.ResetBlocked(s, "d01"), Is.EqualTo("本局已结束"));
    }
}
