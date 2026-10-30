using Newtonsoft.Json.Linq;
using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 策划提交 5493951 S06 §8 旧档超过五张根卡（S06-A10，开发假设 H3）：迁移不删物/不丢地；安全空闲时由玩家明确保留至多5张，
/// 其余原子移到当前地面；占用期间不能借此增根；读档不重复返物。
/// </summary>
public class V11OverflowTests : TestBase
{
    /// <summary>7 张随身卡的旧档（版本 7）读进来。</summary>
    private GameState LegacyOverflow()
    {
        var s = New();
        foreach (var id in new[] { "canned_food", "wood", "fiber", "cloth", "stone" })
            E.DebugGive(s, id, 1);
        Assert.That(E.RootCardCount(s), Is.EqualTo(7));
        var j = JObject.Parse(SaveSerializer.Serialize(s));
        j["SchemaVersion"] = "7";
        var m = SaveSerializer.Deserialize(j.ToString(), Cfg);
        Assert.That(m.SchemaVersion, Is.EqualTo(GameState.CurrentSchema));
        return m;
    }

    [Test]
    public void S06_A10_MigrationKeepsItems_ResolveAtIdle_RestToGround()
    {
        var s = LegacyOverflow();
        Assert.That(E.RootCardCount(s), Is.EqualTo(7), "迁移不改随身物品");
        Assert.That(E.CarriedOverflow(s), Is.True);
        Assert.That(E.OverflowResolvable(s), Is.True);
        var total = s.Items.Sum(i => i.Count);

        // 超额时不能再增加根数；能合堆的仍可拾取
        Assert.That(E.DoInstant(s, InstantRequest.Pickup(First(s, "clean_water", "camp").InstanceId, 1)).Ok, Is.False, "不允许增加根数");
        Assert.That(E.DoInstant(s, InstantRequest.Pickup(First(s, "wood", "camp").InstanceId, 1)).Ok, Is.True, "并进已有木材堆不增根");

        var roots = E.CarriedRootItems(s);
        var six = roots.Take(6).Select(i => i.InstanceId).ToList();
        var bad = E.ResolveCarriedOverflow(s, six);
        Assert.That(bad.Ok, Is.False, "保留超过5张不行");
        Assert.That(E.RootCardCount(s), Is.EqualTo(7), "失败不改");

        var keep = new List<int> { First(s, "pot").InstanceId, First(s, "lighter").InstanceId, First(s, "canned_food", ItemInstance.Carried).InstanceId };
        var ok = E.ResolveCarriedOverflow(s, keep, "req-1");
        Assert.That(ok.Ok, Is.True, ok.Reason);
        Assert.That(E.RootCardCount(s), Is.EqualTo(3));
        Assert.That(E.CarriedOverflow(s), Is.False);
        Assert.That(s.Items.Sum(i => i.Count), Is.EqualTo(total + 0), "不删物、不复制");
        Assert.That(Count(s, "stone", ItemInstance.Carried), Is.EqualTo(0));
        Assert.That(Count(s, "stone", "camp"), Is.GreaterThan(0), "其余放在当前地面");
        Assert.That(E.ResolveCarriedOverflow(s, keep, "req-1").Duplicate, Is.True, "重复请求不再执行");
    }

    [Test]
    public void S06_A10_NotResolvableWhileBusy()
    {
        var s = LegacyOverflow();
        Start(s, ActionRequest.Wait(30));
        Assert.That(E.OverflowResolvable(s), Is.False, "行动中先保留原状");
        var r = E.ResolveCarriedOverflow(s, new List<int>());
        Assert.That(r.Ok, Is.False);
        Assert.That(E.RootCardCount(s), Is.EqualTo(7));
        E.Cancel(s);
        Assert.That(E.OverflowResolvable(s), Is.True);
    }
}
