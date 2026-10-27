using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// S14 修订1.7 §14 就近执行（开发计划 v0.7 M64）：执行入口移到卡旁/浮窗只改界面位置，确认语义与次数不变。
/// 界面上的“预览/浮窗打开/关闭”只调只读的 Preview、CanApproach；“确认/开始/接近并开战”才调 Start/Approach，
/// 内核按真实 ID、所在地、材料与占用重验，同一请求号重复到达只生效一次（S14-A56/A57、S02-A06、S08-A06、R05-A08）。
/// </summary>
public class V10NearbyExecTests : TestBase
{
    private static string Snapshot(GameState s) => Survival.Core.Save.SaveSerializer.Serialize(s);

    [Test]
    public void Travel_CardPreviewAndClose_NoChange_ConfirmOnce()
    {
        var s = New();
        var before = Snapshot(s);
        // 卡旁浮窗打开：只读预览（时间、预计完成、负重、入夜影响），关闭不移动
        var p = E.Preview(s, ActionRequest.Travel("field"));
        Assert.That(p.Ok, Is.True);
        Assert.That(p.ConfirmText, Is.EqualTo("确认移动"));
        Assert.That(p.EstimatedEndMinute, Is.EqualTo(s.GameMinute + p.EstimatedMinutes));
        Assert.That(Snapshot(s), Is.EqualTo(before), "预览/关闭浮窗不改任何状态、不推进时间");

        // 确认移动：一次确认；同一请求号重复点击只生效一次，新请求号在行动中被拒
        var r1 = E.Start(s, ActionRequest.Travel("field").WithRequestId("go-1"));
        Assert.That(r1.Ok, Is.True);
        var again = E.Start(s, ActionRequest.Travel("field").WithRequestId("go-1"));
        Assert.That(again.Duplicate, Is.True, "重复点击同一确认不重复开始");
        var other = E.Start(s, ActionRequest.Travel("field").WithRequestId("go-2"));
        Assert.That(other.Ok, Is.False, "行动中再点确认不叠加第二个移动");
        Run(s);
        Assert.That(s.CurrentLocation, Is.EqualTo("field"));
    }

    [Test]
    public void Travel_FirstArrivalWarningShownLocally_AndArrivalTransactionUnchanged()
    {
        var s = NewStory();
        ReadDialogues(s);
        var p = E.Preview(s, ActionRequest.Travel("field"));
        Assert.That(p.Warnings.Any(w => w.Contains("首战")), Is.True, "首战预告在就近预览里可见（S02-A06）");
        Start(s, ActionRequest.Travel("field").WithRequestId("t"));
        for (var i = 0; i < 20 && s.Main != null; i++) E.Advance(s);
        Assert.That(s.CurrentLocation, Is.EqualTo("field"));
        Assert.That(s.Battle, Is.Not.Null, "原到达事务不变：第一次到荒地直接进入首战");
    }

    [Test]
    public void Craft_PreviewNoReserve_StartReservesOnce_ChangedMaterialsRevalidated()
    {
        var s = New();
        E.DebugGive(s, "stone", 1);
        E.DebugGive(s, "fiber", 1);
        var req = ActionRequest.Craft("knife");
        var p = E.Preview(s, req);
        Assert.That(p.Ok, Is.True);
        Assert.That(s.Items.Any(i => i.ReservedBy != null), Is.False, "配方卡上预览、改件数、关闭都不预留");

        // 预览后材料被移走：点“开始制作”时重验，缺料不扣时间、不预留（S08-A02/A06）
        var fibers = s.Items.Where(i => i.ItemId == "fiber" && (i.IsCarried || i.Owner == s.CurrentLocation)).ToList();
        foreach (var f in fibers) s.Items.Remove(f);
        var minute = s.GameMinute;
        var fail = E.Start(s, req.WithRequestId("c-1"));
        Assert.That(fail.Ok, Is.False);
        Assert.That(s.GameMinute, Is.EqualTo(minute));
        Assert.That(s.Main, Is.Null);

        E.DebugGive(s, "fiber", 1);
        var ok = E.Start(s, req.WithRequestId("c-2"));
        Assert.That(ok.Ok, Is.True);
        var reserved = s.Items.Where(i => i.ReservedBy != null).Sum(i => i.Count);
        Assert.That(E.Start(s, req.WithRequestId("c-2")).Duplicate, Is.True, "重复点击同一开始不重复制作");
        Assert.That(E.Start(s, req.WithRequestId("c-3")).Ok, Is.False, "制作中再点开始不叠加第二件");
        Assert.That(s.Items.Where(i => i.ReservedBy != null).Sum(i => i.Count), Is.EqualTo(reserved), "材料只预留一次");
    }

    [Test]
    public void Approach_LocalConfirmOnly_RevalidatedAtSubmit()
    {
        var s = New();
        E.DebugTeleport(s, "field");
        s.GameMinute = At(2, 9, 0);
        s.SpawnNextCheck = int.MaxValue;
        s.PatrolNextCheck = int.MaxValue;
        s.WorldEnemies.Clear();
        var w = E.DebugSpawnEnemy(s, "shambler", "field");
        var before = Snapshot(s);
        // 点“接近”只打开卡旁对象/风险预览：只读判定，不入战
        Assert.That(E.CanApproach(s, w.Id), Is.Null);
        Assert.That(E.FearNote(s, new[] { w }, GameEngine.FearApproach), Is.Not.Empty);
        Assert.That(Snapshot(s), Is.EqualTo(before));
        Assert.That(s.Battle, Is.Null);

        // 目标在预览后离开：本地确认时重验拒绝，不入战（R05-A08）
        w.LocationId = "camp";
        var gone = E.Approach(s, w.Id, "a-1");
        Assert.That(gone.Ok, Is.False);
        Assert.That(s.Battle, Is.Null);

        w.LocationId = "field";
        Assert.That(E.Approach(s, w.Id, "a-2").Ok, Is.True, "点“接近并开战”才接敌");
        Assert.That(s.Battle, Is.Not.Null);
        var battleId = s.Battle!.BattleId;
        Assert.That(E.Approach(s, w.Id, "a-2").Duplicate, Is.True, "重复确认只生效一次");
        Assert.That(s.Battle!.BattleId, Is.EqualTo(battleId));
    }

    [Test]
    public void PotInCarried_ArrowTakesToCarried_TotalWeightUnchanged()
    {
        // S10-A06、S07 §7：随身锅里的水/食物箭头“从锅中取到随身”，独立随身卡，总携重不变
        var s = New();
        var pot = s.Items.First(i => i.ItemId == "pot");
        if (!pot.IsCarried) Assert.That(E.DoInstant(s, InstantRequest.Pickup(pot.InstanceId, 1)).Ok, Is.True);
        E.DebugGive(s, "clean_water", 1);
        var water = s.Items.First(i => i.ItemId == "clean_water" && i.IsCarried);
        Assert.That(E.DoInstant(s, InstantRequest.FillPot(water.InstanceId, pot.InstanceId, 1)).Ok, Is.True);
        var grams = E.CarriedGrams(s);
        var inPot = E.PotLiquids(s, pot.InstanceId).Single();
        Assert.That(E.DoInstant(s, InstantRequest.Decant(inPot.InstanceId, ItemInstance.Carried, 1)).Ok, Is.True);
        Assert.That(E.PotLiquids(s, pot.InstanceId), Is.Empty);
        Assert.That(s.Items.Any(i => i.ItemId == "clean_water" && i.Owner == ItemInstance.Carried), Is.True);
        Assert.That(E.CarriedGrams(s), Is.EqualTo(grams));
    }
}
