using Newtonsoft.Json.Linq;
using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 批次 B51a「生产原料保质与工具本单保障」（开发记录 B51a）：S08 文首「2026-11-15 B51 Q720—Q728」与「2026-11-15 Q701—Q703」的 B51a 部分
/// （Q701.1 原料暂停保质、Q701.3 工具本单保障、Q720 两成品独立期限、Q722／Q723 恢复句、Q724 保护显示、Q726 工具拒绝句、旧档迁移）。
/// 设施并行（Q701.2、Q721、Q727）与预估 Q725、Q702／Q703／Q728 属 B51b，不在这里。正式句见《重墨 V2 开工裁定与界面逐字稿》B51 表。
/// </summary>
public class B51aTests : TestBase
{
    protected override bool NoEnemyApproach => true;

    private static string Web(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "Survival.Web" }.Concat(parts).ToArray()));

    private List<ItemInstance> Make(GameState s, string itemId, int count, string owner, int? expires = null)
    {
        var made = E.CreateItems(s, itemId, count, owner);
        if (expires.HasValue) foreach (var i in made) i.ExpiresAt = expires;
        return made;
    }

    private static string AsSchema(GameState s, string version, Action<JObject>? edit = null)
    {
        var o = JObject.Parse(SaveSerializer.Serialize(s));
        o["SchemaVersion"] = version;
        edit?.Invoke(o);
        return o.ToString();
    }

    private static GameState Reload(GameState s, Survival.Core.Config.GameConfig cfg) => SaveSerializer.Deserialize(SaveSerializer.Serialize(s), cfg);

    // ───────────── M956 存档 54 骨架与通用暂停／恢复 ─────────────

    [Test]
    public void M956_Schema54_ActionFieldsCloneAndSave()
    {
        Assert.That(GameState.CurrentSchema, Is.EqualTo("54"));
        var a = new ActionState { ToolGuardId = 7, ToolGuardSpent = true };
        var c = a.Clone();
        Assert.That((c.ToolGuardId, c.ToolGuardSpent), Is.EqualTo(((int?)7, true)));
        var i = new ItemInstance { ItemId = "herb", Count = 1, ExpiresAt = 100, ShelfPausedMinutes = 40 };
        Assert.That(i.Clone().ShelfPausedMinutes, Is.EqualTo(40));
    }

    /// <summary>通用暂停：R＝E−S、原截止保留；暂停期间越过原截止不算腐败、不转腐败物；恢复 C＋R 后照常计时。</summary>
    [Test]
    public void M956_PauseKeepsKey_NotSpoiledPastOriginal_ResumeCPlusR()
    {
        var s = New();
        var herb = Make(s, "herb", 1, s.Ground, s.GameMinute + 30).Single();
        herb.ReservedBy = 999;   // 模拟一份被工作绑定的输入（真实开工见 M957）
        s.Main = new ActionState { ActionId = 999, Type = ActionType.Craft, TargetId = "bandage", ReservedItemIds = { herb.InstanceId }, Status = ActionStatus.Paused };
        var e0 = herb.ExpiresAt;
        herb.ShelfPausedMinutes = 30;
        Assert.That(herb.ExpiresAt, Is.EqualTo(e0), "原截止保留作合堆键");
        s.GameMinute += 60;
        Assert.That(E.IsSpoiled(s, herb), Is.False, "按 R，不按原截止");
        E.UpdateFlags(s);
        Assert.That(herb.ItemId, Is.EqualTo("herb"), "暂停保质的不转腐败物");
        Assert.That(GameEngine.EffectiveExpiry(s, herb), Is.EqualTo(s.GameMinute + 30));
    }

    /// <summary>孤立保护收束：没有在办工作绑定的 R 读档时恢复 C＋R；晾制位与在办行动的保护不动；重复读档幂等。</summary>
    [Test]
    public void M956_OrphanProtectionSweptOnLoad_LiveKept_Idempotent()
    {
        var s = New();
        var orphan = Make(s, "herb", 1, s.Ground, s.GameMinute + 50).Single();
        orphan.ShelfPausedMinutes = 20;
        var live = Make(s, "berry", 1, s.Ground, s.GameMinute + 50).Single();
        live.ShelfPausedMinutes = 25;
        live.ReservedBy = 4242;
        s.Main = new ActionState { ActionId = 4242, Type = ActionType.Craft, TargetId = "bandage", Status = ActionStatus.Paused, ReservedItemIds = { live.InstanceId } };
        var t = Reload(s, Cfg);
        var o = t.Items.Single(i => i.InstanceId == orphan.InstanceId);
        Assert.That((o.ShelfPausedMinutes, o.ExpiresAt), Is.EqualTo(((int?)null, (int?)(t.GameMinute + 20))), "孤立保护按读档时刻 C＋R 恢复");
        var l = t.Items.Single(i => i.InstanceId == live.InstanceId);
        Assert.That(l.ShelfPausedMinutes, Is.EqualTo(25), "在办工作的保护不动");
        var t2 = Reload(t, Cfg);
        Assert.That(t2.Items.Single(i => i.InstanceId == orphan.InstanceId).ExpiresAt, Is.EqualTo(t.GameMinute + 20), "重复读档不再改");
    }

    /// <summary>保护份保留原截止：随身同堆拆出的保护份与原堆仍算同一张根卡；剩余 R 不同的两份不被 Normalize 合并、明细按 R 分批。</summary>
    [Test]
    public void M956_ProtectedPortion_SameRootCard_DifferentRNotMerged()
    {
        var s = New();
        s.Items.RemoveAll(i => i.IsCarried);
        var due = s.GameMinute + 300;
        var stack = Make(s, "herb", 5, ItemInstance.Carried, due).Single();
        Assert.That(E.RootCardCount(s), Is.EqualTo(1));
        var part = E.SplitOff(s, stack, 2);
        part.ReservedBy = 77;
        part.ShelfPausedMinutes = 300;
        Assert.That(E.RootCardCount(s), Is.EqualTo(1), "保护份不多算根卡");
        var berry = Make(s, "berry", 2, s.Ground, due).Single();
        var p1 = E.SplitOff(s, berry, 1);
        berry.ShelfPausedMinutes = 100;
        p1.ShelfPausedMinutes = 200;
        Assert.That(GameEngine.BatchKeyOf(berry), Is.Not.EqualTo(GameEngine.BatchKeyOf(p1)), "按各自 R 分批");
    }
}
