using Newtonsoft.Json.Linq;
using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// B41a2 里程碑 M710：旧档暂停绕行下的在途子行动（R05「B41a 实装补充 Q454—Q457」Q454、S01「B41a Q454 旧绕行下的在途子行动」；验收 Q454-A01）。
/// 采纳时只取消旧绕行；在途补给／休息／点火转为独立行动续做，沿原行动身份、已耗时间、进度、已提交效果与自身预留／工具锁，
/// 完成后回空闲、不恢复绕行；迁移日志只一次「原绕行行动已取消。」，重复读档不再转换或写日志。
/// </summary>
public class B41a2SubActionTests : B41aTestBase
{
    private static string RngOf(GameState s) => JObject.Parse(SaveSerializer.Serialize(s))["Rng"]!.ToString();

    /// <summary>旧版（开关关）新局在临时营地：摆一条旧待处理敌踪、开绕行、走一段、暂停，再在暂停下开一个子行动。</summary>
    private GameState PausedBypassWithSub(string sub)
    {
        var s = Off.NewGame(7);
        Off.DebugLegacyStory(s);
        s.SpawnNextCheck = int.MaxValue;
        s.PatrolNextCheck = int.MaxValue;
        Off.DebugSetStats(s, 100, 60, 80, 90, 90, 60);
        s.Encounter = new PendingEncounter { LocationId = s.CurrentLocation, EnemyIds = new List<int>(), Source = "explore", Minute = s.GameMinute };
        Assert.That(Off.Start(s, ActionRequest.Bypass()).Ok, Is.True);
        Off.Advance(s);
        Assert.That(s.Main!.Type, Is.EqualTo(ActionType.Bypass));
        s.Main.Status = ActionStatus.Paused;
        s.Main.PauseReason = PauseReason.StatAlert;

        var req = sub switch
        {
            "use" => ActionRequest.UseItem("canned_food"),
            "rest" => ActionRequest.Rest(30),
            _ => ActionRequest.Ignite(First(s, "lighter").InstanceId),
        };
        var r = Off.Start(s, req);
        Assert.That(r.Ok, Is.True, r.Reason);
        Assert.That(s.Sub!.IsSub, Is.True);
        if (sub == "rest") Off.Advance(s);   // 休息先走一段，带着已耗时间和进度存档
        return s;
    }

    [TestCase("use", false)]
    [TestCase("rest", false)]
    [TestCase("ignite", false)]
    [TestCase("ignite", true)]
    [TestCase("rest", true)]
    public void Q454_A01_SubActionContinuesAsIndependentAction(string sub, bool via46)
    {
        var s = PausedBypassWithSub(sub);
        var sa = s.Sub!;
        var subId = sa.ActionId;
        var label = sa.Label;
        var elapsed = sa.ElapsedMinutes;
        var planned = sa.PlannedMinutes;
        var locked = sa.LockedItemIds.ToList();
        var tool = sa.ToolInstanceId;
        var reservedBy = s.Items.Where(i => i.ReservedBy == subId).Select(i => i.InstanceId).ToList();
        var minute = s.GameMinute;
        var food = Count(s, "canned_food");
        var wood = Count(s, "wood");
        var lighter = s.Items.FirstOrDefault(i => i.ItemId == "lighter");
        var lighterDur = lighter?.Durability;
        var fire = s.Locations[s.CurrentLocation].Facility.FireLit;
        var fuel = s.Locations[s.CurrentLocation].Facility.FuelMinutes;
        var rng = RngOf(s);
        var log = s.Log.Count;

        // 对照：同一状态在旧版下照常把子行动做完（主行动仍暂停）
        var control = SaveSerializer.Deserialize(SaveSerializer.Serialize(s), CfgOff);
        for (var i = 0; i < 100 && control.Sub != null; i++) Off.Advance(control);
        Assert.That(control.Sub, Is.Null);

        var json = via46 ? SaveSerializer.Serialize(SaveSerializer.Deserialize(AsSchema45(s), CfgOff)) : AsSchema45(s);
        if (via46) Assert.That(JObject.Parse(json)["Sub"]!.Type, Is.Not.EqualTo(JTokenType.Null), "开关关着升到 46 不动行动");
        var t = SaveSerializer.Deserialize(json, CfgV2);

        // 只取消绕行；子行动沿原身份、已耗时间、进度、占用转成独立行动
        Assert.That(t.Sub, Is.Null, "不留无父行动的子行动");
        Assert.That(t.Main, Is.Not.Null);
        Assert.That(t.Main!.ActionId, Is.EqualTo(subId), "同一行动身份");
        Assert.That(t.Main.Type, Is.Not.EqualTo(ActionType.Bypass));
        Assert.That(t.Main.IsSub, Is.False);
        Assert.That(t.Main.Status, Is.EqualTo(ActionStatus.Running));
        Assert.That(t.Main.Label, Is.EqualTo(label), "中央行动位沿原名称");
        Assert.That(t.Main.ElapsedMinutes, Is.EqualTo(elapsed), "已耗时间保留");
        Assert.That(t.Main.PlannedMinutes, Is.EqualTo(planned), "剩余进度保留");
        Assert.That(t.Main.LockedItemIds, Is.EqualTo(locked), "自身工具锁保留");
        Assert.That(t.Main.ToolInstanceId, Is.EqualTo(tool));
        Assert.That(t.Items.Where(i => i.ReservedBy == subId).Select(i => i.InstanceId), Is.EqualTo(reservedBy), "自身预留保留");
        Assert.That(t.GameMinute, Is.EqualTo(minute), "转接不推进时间");
        Assert.That(Count(t, "canned_food"), Is.EqualTo(food), "不重复扣物");
        Assert.That(Count(t, "wood"), Is.EqualTo(wood));
        Assert.That(t.Items.FirstOrDefault(i => i.ItemId == "lighter")?.Durability, Is.EqualTo(lighterDur), "不重复扣耐久");
        Assert.That(t.Locations[t.CurrentLocation].Facility.FireLit, Is.EqualTo(fire), "已提交的开工效果不重结");
        Assert.That(t.Locations[t.CurrentLocation].Facility.FuelMinutes, Is.EqualTo(fuel));
        Assert.That(t.Log.Count, Is.EqualTo(log + 1), "迁移只写一条，不写子行动重开或取消");
        Assert.That(t.Log[^1].Text, Is.EqualTo("原绕行行动已取消。"));
        Assert.That(RngOf(t), Is.EqualTo(rng), "不重抽随机");
        Assert.That(E.IsBusy(t), Is.True);
        if (sub == "ignite")
            Assert.That(E.CanStart(t, ActionRequest.Ignite(tool)), Is.Not.Null, "打火器仍被本行动锁住");

        // 重复读档／重复采纳：不再转换、不再写日志
        var once = SaveSerializer.Serialize(t);
        var again = SaveSerializer.Deserialize(once, CfgV2);
        E.AdoptEnemyCards(again);
        Assert.That(SaveSerializer.Serialize(again), Is.EqualTo(once));

        // 按真实剩余过程做完，回空闲，不恢复绕行
        for (var i = 0; i < 100 && t.Main != null; i++) E.Advance(t);
        Assert.That(t.Main, Is.Null, "完成后回正常空闲态");
        Assert.That(t.Sub, Is.Null);
        Assert.That(E.IsBusy(t), Is.False);
        Assert.That(t.Log.Skip(log + 1).Any(l => l.Text.Contains("绕行")), Is.False, "不恢复已取消的绕行");
        Assert.That(t.GameMinute, Is.EqualTo(control.GameMinute), "剩余时间与原子行动一致");
        Assert.That(Count(t, "canned_food"), Is.EqualTo(Count(control, "canned_food")), "补给只扣一次");
        Assert.That(Count(t, "wood"), Is.EqualTo(Count(control, "wood")));
        Assert.That(t.Items.FirstOrDefault(i => i.ItemId == "lighter")?.Durability,
            Is.EqualTo(control.Items.FirstOrDefault(i => i.ItemId == "lighter")?.Durability), "点火磨损只结一次");
        Assert.That(t.Locations[t.CurrentLocation].Facility.FireLit, Is.EqualTo(control.Locations[control.CurrentLocation].Facility.FireLit));
        Assert.That(t.Stats.Satiety, Is.EqualTo(control.Stats.Satiety).Within(1e-6), "补给效果只结一次");
        Assert.That(t.Stats.Energy, Is.EqualTo(control.Stats.Energy).Within(1e-6), "休息按原剩余过程");
        Assert.That(t.Items.All(i => i.ReservedBy != subId), Is.True, "完成后占用释放");
        Assert.That(t.Main?.LockedItemIds ?? new List<int>(), Is.Empty);
    }

    [Test]
    public void Q454_SwitchOff_PausedBypassWithSub_Unchanged()
    {
        var s = PausedBypassWithSub("rest");
        var t = SaveSerializer.Deserialize(AsSchema45(s), CfgOff);
        Assert.That(t.Main!.Type, Is.EqualTo(ActionType.Bypass), "开关关着旧绕行原样");
        Assert.That(t.Main.Status, Is.EqualTo(ActionStatus.Paused));
        Assert.That(t.Sub!.Type, Is.EqualTo(ActionType.Rest));
        Assert.That(t.Sub.IsSub, Is.True);
        Assert.That(t.Log.Count, Is.EqualTo(s.Log.Count));
    }
}
