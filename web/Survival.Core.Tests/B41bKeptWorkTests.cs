using System.Reflection;
using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// B41b 里程碑 5：战后撤退离地保留暂停、暂存原工作的地图移动与续接（开关开；R05「B41b Q439—Q444」Q440、「Q463—Q471」Q463—Q464、
/// 「Q472—Q476」Q472—Q474；逐字稿「B41b Q472—Q476 暂存与移动逐字稿」；验收 Q440-A01 地点部分、Q463-A01、Q464-A01、Q472-A01—A03、Q473-A01、Q474-A01）。
/// </summary>
public class B41bKeptWorkTests : B41bTestBase
{
    [Test]
    public void Q463_StatAlertPauseThenEnemyRetreat_KeepsWork_MayTravel()
    {
        var s = Field(3);
        var id = KeptCraft(s, pauseFirstByAlert: true);
        Assert.That(s.Main!.PauseReason, Is.EqualTo(PauseReason.StatAlert), "原暂停原因保留");
        Assert.That(E.CanStart(s, ActionRequest.Travel("field")), Is.Null, "不因暂停起因卡死");
        Assert.That(s.Main.ReserveSources![Reserved(s, "stone", id).InstanceId], Is.EqualTo(ItemInstance.Carried));
        Assert.That(s.Main.ReserveSources![Reserved(s, "fiber", id).InstanceId], Is.EqualTo("field"));
    }

    [Test]
    public void Q464_OffSite_NoResume_NoNewMainWork_SuppliesStillAllowed()
    {
        var s = Field(3);
        KeptCraft(s);
        var r = E.Resume(s);
        Assert.That(r.Ok, Is.False);
        Assert.That(r.Reason, Is.EqualTo($"请返回{E.LocName("field")}后再继续{s.Main!.Label}。"));
        Assert.That(s.Main.Status, Is.EqualTo(ActionStatus.Paused), "失败不改状态");
        Assert.That(E.CanStart(s, ActionRequest.Wait(30)), Is.EqualTo("暂停中只能补给、休息或点火维护"), "不开第二项主工作");
        Assert.That(E.CanStart(s, ActionRequest.Rest(30)), Is.Null, "原补给、休息白名单保持");
        Assert.That(E.CanStart(s, ActionRequest.Travel("field")), Is.Null);
    }

    [Test]
    public void Q472_A01_TravelStashesWork_PausedTravelSubOnlyActsOnTravel()
    {
        var s = Field(3);
        var id = KeptCraft(s);
        var progress = s.Main!.WorkDone;
        // 校验失败不留下半次转接
        Assert.That(E.Start(s, ActionRequest.Travel("stream")).Ok, Is.False);
        Assert.That(s.Main.ActionId, Is.EqualTo(id));
        Assert.That(s.StashedWork, Is.Null);

        Start(s, ActionRequest.Travel("field"));
        Assert.That(s.Main.Type, Is.EqualTo(ActionType.Travel));
        Assert.That(s.StashedWork!.ActionId, Is.EqualTo(id), "只有一份暂存");
        Assert.That(s.StashedWork.Status, Is.EqualTo(ActionStatus.Paused));
        Assert.That(Reserved(s, "stone", id).Owner, Is.EqualTo(ItemInstance.Carried), "随身预留随物主移动");

        Assert.That(Step(s), Is.EqualTo(SegmentStatus.Running));
        // 移动在段后被提醒停下：只可随身补给／露天休息，继续作用于当前移动
        s.Main.Status = ActionStatus.Paused;
        s.Main.PauseReason = PauseReason.StatAlert;
        Assert.That(E.CanStart(s, ActionRequest.Craft("knife")), Is.EqualTo("暂停中只能补给、休息或点火维护"));
        Assert.That(E.CanStart(s, ActionRequest.Travel("camp")), Is.EqualTo("暂停中只能补给、休息或点火维护"), "不嵌套第二次移动");
        Start(s, ActionRequest.Rest(30));
        Assert.That(s.Sub!.ForceOutdoor, Is.True, "途中休息按露天");
        Assert.That(Run(s, autoResume: false), Is.EqualTo(SegmentStatus.Completed));
        Assert.That(s.Main.Type, Is.EqualTo(ActionType.Travel), "补给完成后移动保持暂停");
        Assert.That(s.Main.Status, Is.EqualTo(ActionStatus.Paused));
        Assert.That(E.Resume(s).Ok, Is.True, "继续作用于当前移动");
        Assert.That(Run(s), Is.EqualTo(SegmentStatus.Completed));

        Assert.That(s.CurrentLocation, Is.EqualTo("field"));
        Assert.That(s.StashedWork, Is.Null);
        Assert.That(s.Main.ActionId, Is.EqualTo(id), "到达后原工作放回、仍暂停");
        Assert.That(s.Main.Status, Is.EqualTo(ActionStatus.Paused));
        Assert.That(s.Main.WorkDone, Is.EqualTo(progress), "移动不推进原工作");
        Assert.That(s.Log.Last(l => l.Text.StartsWith("到达")).Text, Does.EndWith($"{s.Main.Label}仍保持暂停。"));
    }

    [Test]
    public void Q472_A02_CancelTravel_RestoresSamePausedWork_ReservationsKept_NoNesting()
    {
        var s = Field(3);
        var id = KeptCraft(s);
        var stone = Reserved(s, "stone", id);
        var fiber = Reserved(s, "fiber", id);
        Start(s, ActionRequest.Travel("field"));
        Step(s);
        Assert.That(E.Cancel(s).Ok, Is.True);
        Assert.That(s.CurrentLocation, Is.EqualTo("camp"), "沿原结果留在移动起点");
        Assert.That(s.Main!.ActionId, Is.EqualTo(id));
        Assert.That(s.Main.Status, Is.EqualTo(ActionStatus.Paused));
        Assert.That(s.StashedWork, Is.Null);
        Assert.That(stone.ReservedBy, Is.EqualTo(id), "移动取消不释放原预留");
        Assert.That(fiber.ReservedBy, Is.EqualTo(id));
        Assert.That(fiber.Owner, Is.EqualTo("field"), "原地面材料仍在原地点");
        var last = s.Log.Last().Text;
        Assert.That(last, Does.StartWith("已取消移动，留在"));
        Assert.That(last, Does.EndWith($"{s.Main.Label}仍保持暂停。"));

        // 多步：每次新开移动都只暂存这一份
        Start(s, ActionRequest.Travel("field"));
        Assert.That(Run(s), Is.EqualTo(SegmentStatus.Completed));
        Assert.That(s.Main.ActionId, Is.EqualTo(id));
        Start(s, ActionRequest.Travel("camp"));
        Assert.That(s.StashedWork!.ActionId, Is.EqualTo(id));
        Assert.That(Run(s), Is.EqualTo(SegmentStatus.Completed));
        Assert.That(s.Main.ActionId, Is.EqualTo(id));
        Assert.That(s.StashedWork, Is.Null);
    }

    [Test]
    public void Q472_A03_SaveLoadMidTravelAndSub_RestoresLayers_TerminalReleasesOnce()
    {
        var s = Field(3);
        var id = KeptCraft(s);
        Start(s, ActionRequest.Travel("field"));
        Step(s);
        s.Main!.Status = ActionStatus.Paused;
        s.Main.PauseReason = PauseReason.StatAlert;
        Start(s, ActionRequest.Rest(30));
        var t = SaveSerializer.Deserialize(SaveSerializer.Serialize(s), CfgV2);
        Assert.That(t.Main!.Type, Is.EqualTo(ActionType.Travel));
        Assert.That(t.Sub!.Type, Is.EqualTo(ActionType.Rest));
        Assert.That(t.StashedWork!.ActionId, Is.EqualTo(id));
        Assert.That(t.StashedWork.RetreatKept, Is.True);
        Assert.That(t.StashedWork.ReserveSources, Has.Count.EqualTo(2));
        var visits = t.TraveledEdges.Count;

        // 终局：主角在途中倒下，移动、补给与暂存工作一起收束，预留按真实归属只释放一次
        var kill = typeof(GameEngine).GetMethod("Kill", BindingFlags.NonPublic | BindingFlags.Instance, new[] { typeof(GameState), typeof(string) })!;
        kill.Invoke(E, new object[] { t, "测试" });
        Assert.That(t.Main, Is.Null);
        Assert.That(t.Sub, Is.Null);
        Assert.That(t.StashedWork, Is.Null);
        Assert.That(t.Items.Any(i => i.ReservedBy == id), Is.False);
        Assert.That(t.Items.Count(i => i.ItemId == "fiber" && i.Owner == "field"), Is.EqualTo(1), "原地面材料不搬到终局地点");
        Assert.That(t.Items.Count(i => i.ItemId == "stone" && i.Owner == ItemInstance.Carried), Is.EqualTo(1), "随身部分留随身");
        Assert.That(t.Items.Any(i => i.ItemId == "stone_knife"), Is.False, "不给未完成成果");
        Assert.That(t.TraveledEdges.Count, Is.EqualTo(visits));
    }

    [Test]
    public void Q473_BackAtOrigin_MapStillOpen_UntilResumed()
    {
        var s = Field(3);
        var id = KeptCraft(s);
        TravelTo(s, "field");
        Assert.That(s.Main!.ActionId, Is.EqualTo(id));
        Assert.That(E.CanStart(s, ActionRequest.Travel("camp")), Is.Null, "回原地点不收回移动例外");
        Assert.That(E.Resume(s).Ok, Is.True, "回原地点手动继续");
        Assert.That(s.Main.RetreatMoveOpen, Is.False);
        Assert.That(s.Main.RetreatKept, Is.True);
        // 之后普通暂停不再能移动
        s.Main.Status = ActionStatus.Paused;
        s.Main.PauseReason = PauseReason.StatAlert;
        Assert.That(E.CanStart(s, ActionRequest.Travel("camp")), Is.EqualTo("暂停中只能补给、休息或点火维护"));
    }

    [Test]
    public void Q473_StashIsNotIdle_ForLocksAndOtherWork()
    {
        var s = Field(3);
        var id = KeptCraft(s);
        Start(s, ActionRequest.Travel("field"));
        Assert.That(E.CanStart(s, ActionRequest.Wait(30)), Is.EqualTo("已有进行中的行动"));
        Assert.That(E.Cancel(s).Ok, Is.True);
        Assert.That(s.Main!.ActionId, Is.EqualTo(id));
    }

    [Test]
    public void Q474_ResumeStartedTravel_OnlyGeneralChecks_NewTravelFullyValidated()
    {
        var s = Field(3);
        KeptCraft(s);
        Start(s, ActionRequest.Travel("field"));
        Step(s);
        s.Main!.Status = ActionStatus.Paused;
        s.Main.PauseReason = PauseReason.StatAlert;
        // 不套原工作返回地点的校验：人在途中（不在原地点）照样能继续这次移动
        Assert.That(E.Resume(s).Ok, Is.True);
        Assert.That(Run(s), Is.EqualTo(SegmentStatus.Completed));
        Assert.That(E.CanStart(s, ActionRequest.Travel("stream")), Is.Not.Null, "新移动仍走完整校验");
    }

    [Test]
    public void StartRecordsZoneAndSources_UnderV2()
    {
        var s = Field(3);
        var id = KeptCraft(s);
        Assert.That(s.Main!.StartZone, Is.EqualTo(GameState.ExteriorZone));
        Assert.That(s.Main.StartGeneration, Is.Null, "不在地牢");
        Assert.That(s.Main.ReserveSources!.Keys, Is.EquivalentTo(s.Main.ReservedItemIds));
    }
}
