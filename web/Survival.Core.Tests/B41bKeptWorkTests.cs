using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 战后撤退与暂停工作。原 B41b 里程碑 5（R05 Q440、Q463—Q464、Q472—Q474：撤退离地保留暂停、暂存原工作去走地图、回原地续接）
/// 已被 S01「2026-11-24 B60 暂停继续与撤退后重新开始」Q1100 取代：撤退实际离开原行动地点／区域时，本次暂停工作按原中止结算结束、未完成进度清零，
/// 未用预留与工具锁按原规则释放一次（Q466 开工来源返还仍用于结算），已真实结算的时间、消耗与结果保留；之后走正常路线回原地从原入口重新开工、从零算。
/// B60a 删去的旧测试（出处 Q1100 取代 Q463—Q465、Q473、Q569、R05 Q440 的保留安排，Q472 暂存移动随之不再发生）：Q472_A01／A02／A03、Q473 两条、Q474。
/// </summary>
public class B41bKeptWorkTests : B41bTestBase
{
    private string FieldName => E.LocName("field");

    /// <summary>
    /// Q1100 验收「战斗撤到相邻地点后，这次未完成工作结束」：状态提醒先停、再敌方到期开战、撤退回营地（不看最初暂停原因）——
    /// 主行动位清空，结果句按原中止格式「{行动名}已中止。原因：{原因}」＋Q466／Q471 分来源返还句＋「已耗时间保留。」（S01 Q506 第 4 条系统中止、S08 拼接顺序）；
    /// 原因正文为暂用文案（策划案未给原句）。原随身石头解除预留留随身，原地面纤维回原行动地点荒地（不落到当前营地），不给成品；之后移动不受原工作限制。
    /// </summary>
    [Test]
    public void Q1100_StatAlertPauseThenRetreatAway_WorkEnds_SourcesReturned_FreeToTravel()
    {
        var s = Field(3);
        var id = RetreatEndsCraft(s, pauseFirstByAlert: true);
        var stone = s.Items.Single(i => i.ItemId == "stone" && i.Owner == ItemInstance.Carried);
        var fiber = s.Items.Single(i => i.ItemId == "fiber" && i.Owner == "field");
        Assert.That((stone.ReservedBy, fiber.ReservedBy), Is.EqualTo(((int?)null, (int?)null)), "预留只释放一次、不留给本行动");
        Assert.That(s.Items.Any(i => i.ReservedBy == id), Is.False);
        Assert.That(s.Items.Any(i => i.ItemId == "stone_knife"), Is.False, "不给未完成成品");
        Assert.That(s.Log.Last().Text,
            Is.EqualTo($"{LastLabel}已中止。原因：已撤退离开{FieldName}。随身预留材料已解除预留，仍在随身；地面预留材料已返还{FieldName}地面。已耗时间保留。"));
        Assert.That(s.Log[^2].Text, Does.StartWith("撤退成功"), "中止句写在撤退战报之后");
        Assert.That(E.CanStart(s, ActionRequest.Travel("field")), Is.Null, "空闲：走正常路线回原地");
        Assert.That(E.Resume(s).Reason, Is.EqualTo("没有暂停中的行动"));
    }

    /// <summary>
    /// Q1100「之后玩家走正常路线回到原地，从原入口主动重新开始。新行动重新按原开工规则校验，进度从零起算」：走回荒地、材料拾回后重新开工——
    /// 新行动号、已提交工作量为 0，正常做完得 1 把石刀。
    /// </summary>
    [Test]
    public void Q1100_WalkBackAndRestart_NewActionFromZero()
    {
        var s = Field(3);
        var id = RetreatEndsCraft(s, stoneCarried: true, fiberOnGround: false);
        s.WorldEnemies.Clear();
        s.EnemyCards.Timers.Clear();
        TravelTo(s, "field");
        Start(s, ActionRequest.Craft("knife"));
        Assert.That(s.Main!.ActionId, Is.Not.EqualTo(id), "新开工，不是原行动");
        Assert.That(s.Main.WorkDone, Is.EqualTo(0), "进度从零起算");
        Assert.That(Run(s), Is.EqualTo(SegmentStatus.Completed));
        Assert.That(s.Items.Count(i => i.ItemId == "stone_knife"), Is.EqualTo(1));
    }

    /// <summary>
    /// 配对（Q1100「同地暂停后继续」、测试规则第 4 条）：撤退没有离开原地点（没有可退的来路，留在原地）时，暂停工作不结束——
    /// 同一行动仍暂停、预留仍归本行动，「继续行动」从已提交进度接着做、正常完成。
    /// </summary>
    [Test]
    public void Q1100_RetreatStaysAtSite_WorkKeptPaused_ResumeContinuesSameProgress()
    {
        var s = Field(3);
        E.DebugTeleport(s, "field");   // 起点：开发传送清掉来路记录，撤退只能留在原地
        E.DebugGive(s, "stone", 1, ItemInstance.Carried);
        E.DebugGive(s, "fiber", 1, "field");
        E.DebugSpawnEnemy(s, "shambler", "field");
        Start(s, ActionRequest.Craft("knife"));
        var id = s.Main!.ActionId;
        SetLeft(s, GameEngine.SingleCardKey(s.WorldEnemies.Single().Id), 0);
        Assert.That(Step(s), Is.EqualTo(SegmentStatus.Paused));
        var work = s.Main.WorkDone;
        Assert.That(work, Is.GreaterThan(0), "起点：已提交一段进度");
        RetreatOk(s);
        Assert.That(s.CurrentLocation, Is.EqualTo("field"), "起点核对：留在原地");
        Assert.That((s.Main?.ActionId, s.Main?.Status), Is.EqualTo(((int?)id, (ActionStatus?)ActionStatus.Paused)), "没离开原地点：不结束");
        Assert.That(s.Items.Count(i => i.ReservedBy == id), Is.EqualTo(2), "预留仍归本行动");
        Assert.That(s.Log.Last().Text, Does.Not.Contain("已中止"));
        s.WorldEnemies.Clear();
        s.EnemyCards.Timers.Clear();
        Assert.That(E.Resume(s).Ok, Is.True, E.ResumeBlockReason(s));
        Assert.That(s.Main!.WorkDone, Is.EqualTo(work), "从已提交进度接着做");
        Assert.That(Run(s), Is.EqualTo(SegmentStatus.Completed));
        Assert.That(s.Items.Count(i => i.ItemId == "stone_knife"), Is.EqualTo(1));
    }

    /// <summary>
    /// Q1100「实际离开原地点或原工作区域时」：撤退留在同一地点、但已不在原工作区域（起点：本次工作记在该地点的屋内区域，人在屋外）——同样结束本次工作。
    /// 配对：同一地点同一区域不结束（<see cref="Q1100_RetreatStaysAtSite_WorkKeptPaused_ResumeContinuesSameProgress"/>）。
    /// </summary>
    [Test]
    public void Q1100_RetreatStaysAtSiteButLeftWorkZone_WorkEnds()
    {
        var s = Field(3);
        E.DebugTeleport(s, "field");   // 起点：清掉来路记录，撤退只能留在原地
        E.DebugGive(s, "stone", 1, ItemInstance.Carried);
        E.DebugGive(s, "fiber", 1, "field");
        E.DebugSpawnEnemy(s, "shambler", "field");
        Start(s, ActionRequest.Craft("knife"));
        var id = s.Main!.ActionId;
        var label = s.Main.Label;
        s.Main.StartZone = GameState.SafehouseZone;   // 起点：原工作区域为屋内（只造起点）
        SetLeft(s, GameEngine.SingleCardKey(s.WorldEnemies.Single().Id), 0);
        Assert.That(Step(s), Is.EqualTo(SegmentStatus.Paused));
        RetreatOk(s);
        Assert.That((s.CurrentLocation, s.CurrentZone), Is.EqualTo(("field", GameState.ExteriorZone)), "起点核对：同一地点、屋外");
        Assert.That(s.Main, Is.Null, "离开原工作区域：本次工作结束");
        Assert.That(s.Items.Any(i => i.ReservedBy == id), Is.False);
        Assert.That(s.Log.Last().Text, Does.StartWith($"{label}已中止。原因：已撤退离开"));
    }

    /// <summary>
    /// 系统中止也是行动结束边界（R05 Q416、Q467：合法退出、结束都在边界检查敌方接近；AbortLocked 同原合法退出）：锁定工作预留被改、点「继续行动」自动中止——
    /// 当地有到期的敌踪卡时随即开战。配对：没有到期卡时中止后不开战。
    /// </summary>
    [Test]
    public void LockedAbort_IsActionEndBoundary_DueCardFights()
    {
        foreach (var due in new[] { true, false })
        {
            var s = Field(3);
            var id = PausedCraft(s);
            s.Main!.NoExit = true;
            var w = E.DebugSpawnEnemy(s, "shambler", "field");
            if (due) SetLeft(s, GameEngine.SingleCardKey(w.Id), 0);
            Reserved(s, "fiber", id).ReservedBy = null;   // 起点：预留被改（永久原因）
            Assert.That(E.LockedResume(s).Kind, Is.EqualTo(LockedResumeKind.Permanent));
            Assert.That(s.Battle, Is.Null, "起点：还没开战");
            var r = E.Resume(s);
            Assert.That((r.Ok, r.Aborted, s.Main), Is.EqualTo((true, true, (ActionState?)null)));
            Assert.That(s.Battle != null, Is.EqualTo(due), due ? "到期卡在中止边界开战" : "配对：没有到期卡不开战");
        }
    }

    /// <summary>
    /// 旧档（B60a 之前存下的撤退离地保留、人在异地）：Q1100 起不再有撤退返程的移动例外（取代 Q464／Q473 的地图放行）——暂停中开新移动按普通暂停拒绝；
    /// 非锁定工作仍可「退出行动」，按 Q466／Q471 开工来源返还收束。
    /// </summary>
    [Test]
    public void Legacy_KeptAway_NoTravelException_ExitReturnsBySource()
    {
        var s = Field(3);
        var id = LegacyKeptCraft(s);
        var fiber = Reserved(s, "fiber", id);
        Assert.That(E.CanStart(s, ActionRequest.Travel("field")), Is.EqualTo("暂停中只能补给、休息或点火维护"), "不再开放撤退返程");
        Assert.That(E.Start(s, ActionRequest.Travel("field")).Ok, Is.False);
        Assert.That(s.StashedWork, Is.Null, "不暂存原工作");
        Assert.That(E.Cancel(s).Ok, Is.True);
        Assert.That(s.Log.Last().Text,
            Is.EqualTo($"已取消制作。随身预留材料已解除预留，仍在随身；地面预留材料已返还{FieldName}地面。已耗时间不退。"));
        Assert.That(s.Items.Any(i => i.ReservedBy == id), Is.False);
        Assert.That((fiber.Owner, fiber.ReservedBy), Is.EqualTo(("field", (int?)null)));
    }

    /// <summary>
    /// 旧档锁定工作在异地（没有退出入口、又不能再走回去）：只读复核给自动中止说明，「继续行动」按同一中止结算收束（Q1100 撤退离地即结束），不卡在暂停里。
    /// 配对：人在原地时同一旧档工作可正常继续（见 <see cref="Q1100_RetreatStaysAtSite_WorkKeptPaused_ResumeContinuesSameProgress"/> 与 B41c Q488）。
    /// </summary>
    [Test]
    public void Legacy_LockedKeptAway_ResumeAbortsWithRetreatReason()
    {
        var s = Field(3);
        var id = LegacyKeptCraft(s);
        s.Main!.NoExit = true;
        var label = s.Main.Label;
        var p = E.ActionProgress(s)!;
        Assert.That((p.ResumeNote, p.ResumeAborts), Is.EqualTo(($"已撤退离开{FieldName}", true)));
        var t0 = s.GameMinute;
        var r = E.Resume(s);
        Assert.That((r.Ok, r.Aborted), Is.EqualTo((true, true)));
        Assert.That((s.Main, s.GameMinute), Is.EqualTo(((ActionState?)null, t0)), "不推进时间");
        Assert.That(s.Log.Last().Text,
            Is.EqualTo($"{label}已中止。原因：已撤退离开{FieldName}。随身预留材料已解除预留，仍在随身；地面预留材料已返还{FieldName}地面。已耗时间保留。"));
        Assert.That(s.Items.Any(i => i.ReservedBy == id), Is.False);
    }

    [Test]
    public void StartRecordsZoneAndSources_UnderV2()
    {
        var s = Field(3);
        PausedCraft(s);
        Assert.That(s.Main!.StartZone, Is.EqualTo(GameState.ExteriorZone));
        Assert.That(s.Main.StartGeneration, Is.Null, "不在地牢");
        Assert.That(s.Main.ReserveSources!.Keys, Is.EquivalentTo(s.Main.ReservedItemIds));
    }

    /// <summary>旧档保留标记存读档不丢（结算仍按开工来源）。</summary>
    [Test]
    public void Legacy_KeptFlags_RoundTrip()
    {
        var s = Field(3);
        LegacyKeptCraft(s);
        var t = SaveSerializer.Deserialize(SaveSerializer.Serialize(s), CfgV2);
        Assert.That(t.Main!.RetreatKept, Is.True);
        Assert.That(t.Main.ReserveSources, Is.EquivalentTo(s.Main!.ReserveSources!));
    }
}
