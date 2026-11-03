using System.Reflection;
using Newtonsoft.Json.Linq;
using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 B22 R67/R69：守卫回收材料（instance_once）、首通补给包（campaign_once）、通关 clearedAt 与存档 34。出处：A09「E6.2」「E6.3」「E1」「E8」、
/// R03 D2/D7.2、Q127；开发假设 B22-1、B22-2、B22-9。验收 EC08、EC14、COMP10、COMP14（守卫/首通各按其账）。
/// </summary>
public class B22SalvageFirstClearTests : BattleTestBase
{
    private GameState At(string node, int capacity, uint seed = 2211)
    {
        var s = New(seed);
        UnlockCapacity(s, capacity);
        s.GameMinute = At(2, 9, 0);
        s.SpawnNextCheck = int.MaxValue;
        s.PatrolNextCheck = int.MaxValue;
        E.DebugMapTeleport(s, node);
        return s;
    }

    private void WinBattle(GameState s)
    {
        var kill = typeof(GameEngine).GetMethod("KillEnemy", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var check = typeof(GameEngine).GetMethod("CheckVictory", BindingFlags.NonPublic | BindingFlags.Instance)!;
        foreach (var e in s.Battle!.Enemies.Where(e => e.Alive).ToList()) kill.Invoke(E, new object[] { s, e });
        check.Invoke(E, new object[] { s });
    }

    private static int Ground(GameState s, string node, string item) => s.Items.Where(i => i.Owner == node && i.ItemId == item).Sum(i => i.Count);

    private void Claim(GameState s, string node, string rid, string dungeon)
    {
        E.DebugMapTeleport(s, node);
        E.DebugSetProgress(s, node, 100);
        E.DebugInstanceFact(s, dungeon, "guardianDefeated");
        Start(s, ActionRequest.Gather(rid));
        Run(s);
    }

    /// <summary>E6.2：守卫战最终胜利才发本代守卫回收（d05_06 橡胶×1，落战斗地点地面、记本实例来源、键 instanceId:d05_06:combat_salvage）；同代不重复，换代后再胜再发一次。</summary>
    [Test]
    public void E62_GuardSalvage_OncePerGeneration_OnVictory()
    {
        var s = At("d05_06", 3);
        Assert.That(E.EngageFixed(s).Ok, Is.True);
        WinBattle(s);
        Assert.That(s.Battle, Is.Null);
        Assert.That(Ground(s, "d05_06", "rubber"), Is.EqualTo(1));
        var iid = E.InstanceId(s, "d05");
        Assert.That(s.Items.Single(i => i.Owner == "d05_06" && i.ItemId == "rubber").RewardOrigin, Is.EqualTo(iid));
        Assert.That(s.AppliedEventKeys, Does.Contain($"{iid}:d05_06:combat_salvage"));
        Assert.That(E.SalvageClaimed(s, "d05"), Is.True);
        Assert.That(s.Log.Last(l => l.Kind == GameEventKind.BattleEnd.ToString()).Text, Does.Contain("守卫回收材料：橡胶+1"));
        Assert.That(E.DungeonCleared(s, "d05"), Is.False, "只杀守卫不写通关（COMP10）");
        Assert.That(s.DungeonInstances["d05"].ClearedAt, Is.Null);

        // 换代：新一代再胜再发一次（repeatable，DR17）
        E.DebugMapTeleport(s, "w33_waterworks");
        Assert.That(E.NewDungeonGeneration(s, "d05"), Is.Null);
        Assert.That(E.SalvageClaimed(s, "d05"), Is.False);
        E.DebugMapTeleport(s, "d05_06");
        Assert.That(E.EngageFixed(s).Ok, Is.True);
        WinBattle(s);
        Assert.That(s.AppliedEventKeys, Does.Contain($"{E.InstanceId(s, "d05")}:d05_06:combat_salvage"));
        Assert.That(s.Items.Where(i => i.ItemId == "rubber").Sum(i => i.Count), Is.EqualTo(2), "旧代一份已迁回收箱，新代再一份");
    }

    /// <summary>E6.2/D7.2：撤退未结不领——守卫战撤退收场不发回收材料，开发记“守卫已击败”也不发。</summary>
    [Test]
    public void E62_RetreatOrDevFact_NoSalvage()
    {
        var s = At("d01_07", 3);
        Assert.That(E.EngageFixed(s).Ok, Is.True);
        typeof(GameEngine).GetMethod("FinishBattle", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(E, new object[] { s, "retreat", true });
        Assert.That(s.Items.Any(i => i.ItemId == "cloth" && i.RewardOrigin != null), Is.False);
        E.DebugInstanceFact(s, "d01", "guardianDefeated");
        Assert.That(E.SalvageClaimed(s, "d01"), Is.False, "开发事实不发掉落");
    }

    /// <summary>EC14：主角在守卫战最后一个 tick 死亡——终局优先，不发守卫回收材料（不先发未来奖）。</summary>
    [Test]
    public void EC14_DeathInFinalTick_NoSalvage()
    {
        var s = At("d01_07", 3);
        Assert.That(E.EngageFixed(s).Ok, Is.True);
        s.Player.Stats.Set(StatId.Satiety, 0);
        s.Player.Stats.Set(StatId.Hydration, 0);
        s.Player.Stats.Set(StatId.Wakefulness, 0);
        s.Player.Stats.Set(StatId.Health, 0.001);
        WinBattle(s);
        Assert.That(s.Progress.Dead, Is.True);
        Assert.That(E.SalvageClaimed(s, "d01"), Is.False);
        Assert.That(s.Items.Any(i => i.ItemId == "cloth" && i.RewardOrigin != null), Is.False);
    }

    /// <summary>
    /// EC08/E6.3：首通补给包只在本局第一次通关时发（d01：布料×3、线×2，落核心记录房地面、记来源），与记录分开；
    /// 换代后核对不再发；clearedAt/resetEligibleAt 每代第一次核对时写（+4320）。
    /// </summary>
    [Test]
    public void EC08_FirstClear_OncePerCampaign_ClearedAtPerGeneration()
    {
        var s = At("d01_08", 3);
        var t0 = s.GameMinute;
        Claim(s, "d01_08", "d01_08_unique", "d01");
        Assert.That(Ground(s, "d01_08", "cloth"), Is.EqualTo(3));
        Assert.That(Ground(s, "d01_08", "thread"), Is.EqualTo(2));
        Assert.That(s.Items.Where(i => i.Owner == "d01_08").All(i => i.RewardOrigin == E.InstanceId(s, "d01")), Is.True);
        Assert.That(s.Progress.Flags, Does.Contain("campaignId:d01:combat_first"));
        var inst = s.DungeonInstances["d01"];
        Assert.That(inst.ClearedAt, Is.EqualTo(s.GameMinute), "领取的 10 分钟事务完成时写");
        Assert.That(inst.ClearedAt, Is.EqualTo(t0 + 10));
        Assert.That(inst.ResetEligibleAt, Is.EqualTo(inst.ClearedAt + 4320));
        Assert.That(s.Log.Last(l => l.Kind == GameEventKind.ActionCompleted.ToString()).Text, Does.Contain("本局首次通关补给：布料+3，缝线+2"));

        for (var gen = 2; gen <= 3; gen++)
        {
            E.DebugMapTeleport(s, "c03_crypt");
            Assert.That(E.NewDungeonGeneration(s, "d01"), Is.Null);
            Assert.That(s.DungeonInstances["d01"].ClearedAt, Is.Null, "新一代未通关、不计时");
            var cloth = s.Items.Where(i => i.ItemId == "cloth").Sum(i => i.Count);
            Claim(s, "d01_08", "d01_08_unique", "d01");
            Assert.That(s.Items.Where(i => i.ItemId == "cloth").Sum(i => i.Count), Is.EqualTo(cloth), "首通包跨代不重发");
            Assert.That(s.DungeonInstances["d01"].ClearedAt, Is.EqualTo(s.GameMinute), "新一代核对即本代通关");
        }
    }

    /// <summary>
    /// COMP10/E1：d02 核心记录 08、出口 09 分开——只胜守卫不写 clear；领取记录写一次 clearedAt；走 09 单向出口回父地点不第二次写记录、不再发首通包。
    /// </summary>
    [Test]
    public void COMP10_RecordAndExitSeparate_NoSecondWrite()
    {
        var d = Cfg.Map.Dungeons.Single(x => x.Id == "d02");
        Assert.That((d.RecordNode, d.ExitNode), Is.EqualTo(("d02_08", "d02_09")));
        Assert.That((Cfg.Map.Dungeons.Single(x => x.Id == "d04").RecordNode, Cfg.Map.Dungeons.Single(x => x.Id == "d04").ExitNode), Is.EqualTo(("d04_07", "d04_08")));
        var s = At("d02_07", 3);
        E.DebugInstanceFact(s, "d02", "guardianDefeated");
        Assert.That(s.DungeonInstances["d02"].ClearedAt, Is.Null, "只杀守卫不写 clear");
        Claim(s, "d02_08", "d02_08_unique", "d02");
        var cleared = s.DungeonInstances["d02"].ClearedAt;
        Assert.That(cleared, Is.Not.Null);
        var flags = s.Progress.Flags.Count;
        var items = s.Items.Count;
        E.DebugMapTeleport(s, "d02_09");
        E.DebugSetProgress(s, "d02_09", 100);
        Start(s, ActionRequest.Travel("r03_lab"));
        Run(s);
        Assert.That(s.CurrentLocation, Is.EqualTo("r03_lab"));
        Assert.That(s.DungeonInstances["d02"].ClearedAt, Is.EqualTo(cleared), "走出口不第二次写记录");
        Assert.That(s.Progress.Flags.Count, Is.EqualTo(flags));
        Assert.That(s.Items.Count(i => i.Owner != "r03_lab" && !i.Owner.StartsWith("remains:")), Is.LessThanOrEqualTo(items));
    }

    /// <summary>B22-9：存档 33→34——本代已通关的从升级时起算 72 小时；本局已领核心记录的首通补给记已领、不追补实物；未通关的不写。</summary>
    [Test]
    public void Migration_33To34_ClearedAtFromUpgrade_FirstClearMarked()
    {
        var s = At("d01_08", 3);
        Claim(s, "d01_08", "d01_08_unique", "d01");
        E.DebugMapTeleport(s, "c03_crypt");
        s.Progress.Flags.Remove("campaignId:d01:combat_first");
        foreach (var i in s.Items.Where(i => i.RewardOrigin != null).ToList()) s.Items.Remove(i);
        var j = JObject.Parse(SaveSerializer.Serialize(s));
        j["SchemaVersion"] = "33";
        foreach (var p in ((JObject)j["DungeonInstances"]!).Properties()) { ((JObject)p.Value).Remove("ClearedAt"); ((JObject)p.Value).Remove("ResetEligibleAt"); }
        var items = s.Items.Count;
        var t = SaveSerializer.Deserialize(j.ToString(), Cfg);
        Assert.That(t.SchemaVersion, Is.EqualTo(GameState.CurrentSchema));   // B22g 起再升到 35
        Assert.That(t.DungeonInstances["d01"].ClearedAt, Is.EqualTo(t.GameMinute));
        Assert.That(t.DungeonInstances["d01"].ResetEligibleAt, Is.EqualTo(t.GameMinute + 4320));
        Assert.That(t.Progress.Flags, Does.Contain("campaignId:d01:combat_first"));
        Assert.That(t.Items.Count, Is.EqualTo(items), "不发物资");
        Assert.That(t.Log.Single(x => x.Text.Contains("存档已升级到版本34")).Text, Does.Contain("首次通关补给按已领取处理"));
        var again = SaveSerializer.Deserialize(SaveSerializer.Serialize(t), Cfg);
        Assert.That(again.DungeonInstances["d01"].ClearedAt, Is.EqualTo(t.DungeonInstances["d01"].ClearedAt), "幂等");
    }
    /// <summary>顺带修正（B22-2）：d03 旧唯一箱（防暴盾箱，同为 campaign 领取）只发实物、记本实例来源，不写本代通关、不开出口、不发首通包——只有核心记录算通关。</summary>
    [Test]
    public void LegacyBox_ClaimDoesNotClear()
    {
        var s = At("d03_03", 3);
        E.DebugWinFixedHere(s);
        E.DebugSetProgress(s, "d03_03", 100);
        Start(s, ActionRequest.Gather("shield_cache"));
        Run(s);
        Assert.That(s.Items.Single(i => i.ItemId == "riot_shield").RewardOrigin, Is.EqualTo(E.InstanceId(s, "d03")));
        Assert.That(E.DungeonCleared(s, "d03"), Is.False);
        Assert.That(s.DungeonInstances["d03"].ClearedAt, Is.Null);
        Assert.That(E.FirstClearClaimed(s, "d03"), Is.False);
    }
}
