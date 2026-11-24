using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// B41c 续接补充（R05「Q488 返还来源持续到本次工作终结」「Q489 出安全屋不触发敌方主动入战检查」「Q490 维护、现场修补与拆解的当前目标复核」「Q491 移动的子行动阻断句」；
/// 逐字稿「B41b Q488—Q491 续接补充逐字稿」；验收 Q488-A01、Q489-A01、Q490-A01—A02、Q491-A01）。
/// </summary>
// B51c1 改期望（出处 S08 文首 2026-11-16 B51 §Q771 拼接顺序与短稿、02 Q874 开发方建议 A 暂用）：撤退保留／普通退出预告引句「退出将放弃本次{动作}。」→「退出将取消{动作}。」（维护→维修，现场修补用原名）、
// 结果尾句「已耗时间保留。」→「已耗时间不退。」、删「未生成半成品。」「不会生成半成品。」；处理尸体沿 Q722 引句，只改尾句。锁定自动中止仍「已耗时间保留。」。
public class B41cResumeTests : B41cTestBase
{
    private string FieldName => E.LocName("field");

    private string Name(string itemId) => Cfg.Items.Single(i => i.Id == itemId).Name;

    /// <summary>重验失败：给定原因，且整份状态不变（不推时间、不扣物、不改预留、进度、耐久与暂停原因）。</summary>
    private void AssertBlocked(GameState s, string why)
    {
        var before = SaveSerializer.Serialize(s);
        Assert.That(E.ResumeBlockReason(s), Is.EqualTo(why));
        var r = E.Resume(s);
        Assert.That(r.Ok, Is.False);
        Assert.That(r.Reason, Is.EqualTo(why));
        Assert.That(SaveSerializer.Serialize(s), Is.EqualTo(before));
    }

    /// <summary>普通暂停（状态提醒），不是撤退离地保留。</summary>
    private static void Pause(GameState s)
    {
        s.Main!.Status = ActionStatus.Paused;
        s.Main.PauseReason = PauseReason.StatAlert;
    }

    // ───────────── Q488 ─────────────
    // B60a 改起点（出处 S01「2026-11-24 B60 暂停继续与撤退后重新开始」Q1100）：新规则下撤退离地不再保留工作，Q488「保留过、回原点继续后仍按开工来源」只剩旧档；
    // 起点改为旧档里已走回原地、尚未继续的保留工作（LegacyKeptCraft away:false）。撤 Q473 地图放行后 RetreatMoveOpen 不再被读，不再断言它。

    [Test]
    public void Q488_A01_ResumedThenPausedAgain_SourcesKept_MoveNotReopened_ExitReturnsByOrigin_Once()
    {
        var s = Field(3);
        var id = LegacyKeptCraft(s, away: false);
        var stone = s.Items.Single(i => i.ItemId == "stone" && i.ReservedBy == id);
        var fiber = s.Items.Single(i => i.ItemId == "fiber" && i.ReservedBy == id);
        var sources = s.Main!.ReserveSources!.ToDictionary(kv => kv.Key, kv => kv.Value);
        Assert.That(E.Resume(s).Ok, Is.True, E.ResumeBlockReason(s));
        Assert.That(s.Main.RetreatKept, Is.True, "来源标记保留到最终结算");
        Assert.That(s.Main.ReserveSources, Is.EquivalentTo(sources));

        // 继续后的普通暂停：不能凭历史标记开放地图移动
        Pause(s);
        Assert.That(E.CanStart(s, ActionRequest.Travel("camp")), Is.EqualTo("暂停中只能补给、休息或点火维护"));
        Assert.That(s.StashedWork, Is.Null, "不把原工作暂存去移动");

        // 存读档后相同：退出仍按开工来源返还、用 Q471 整句、只返一次
        var t = SaveSerializer.Deserialize(SaveSerializer.Serialize(s), CfgV2);
        foreach (var g in new[] { s, t })
        {
            var main = g.Main!;
            Assert.That(main.RetreatKept, Is.True);
            // 人在原地时，地面部分返还原行动地＝当前地点；随身部分仍留随身（不落地）
            Assert.That(E.ActionProgress(g)!.ExitNote,
                Is.EqualTo($"退出将取消制作。随身预留材料解除预留后留在随身，地面预留材料会返还{FieldName}地面。已耗时间不退。"));
            var allStone = g.Items.Where(i => i.ItemId == "stone").Sum(i => i.Count);
            var allFiber = g.Items.Where(i => i.ItemId == "fiber").Sum(i => i.Count);
            Assert.That(E.Cancel(g).Ok, Is.True);
            Assert.That(g.Log.Last().Text,
                Is.EqualTo($"已取消制作。随身预留材料已解除预留，仍在随身；地面预留材料已返还{FieldName}地面。已耗时间不退。"));
            var gs = g.Items.Single(i => i.InstanceId == stone.InstanceId);
            var gf = g.Items.Single(i => i.InstanceId == fiber.InstanceId);
            Assert.That((gs.Owner, gs.ReservedBy), Is.EqualTo((ItemInstance.Carried, (int?)null)), "随身部分留在原物主随身");
            Assert.That((gf.Owner, gf.ReservedBy), Is.EqualTo(("field", (int?)null)), "地面部分回原行动地");
            Assert.That(g.Items.Where(i => i.ItemId == "stone").Sum(i => i.Count), Is.EqualTo(allStone));
            Assert.That(g.Items.Where(i => i.ItemId == "fiber").Sum(i => i.Count), Is.EqualTo(allFiber), "只返一次");
            Assert.That(E.Cancel(g).Ok, Is.False);
        }
    }

    /// <summary>
    /// 原「继续后又一次撤退离地保留、不覆盖最初来源」：Q1100 起再次撤退离地即结束这次工作——仍按最初开工来源返还（原地面纤维回荒地、原随身石头留随身），不按新落点覆盖。
    /// </summary>
    [Test]
    public void Q488_ResumedThenRetreatAwayAgain_EndsWithOriginalSources()
    {
        var s = Field(3);
        var id = LegacyKeptCraft(s, away: false);
        var fiber = s.Items.Single(i => i.ItemId == "fiber" && i.ReservedBy == id);
        var stone = s.Items.Single(i => i.ItemId == "stone" && i.ReservedBy == id);
        Assert.That(E.Resume(s).Ok, Is.True);
        s.WorldEnemies.Clear();
        s.EnemyCards.Timers.Clear();
        var w2 = E.DebugSpawnEnemy(s, "shambler", "field");
        SetLeft(s, GameEngine.SingleCardKey(w2.Id), 0);
        Assert.That(Step(s), Is.EqualTo(SegmentStatus.Paused));
        RetreatOk(s);
        Assert.That(s.CurrentLocation, Is.EqualTo("camp"));
        Assert.That(s.Main, Is.Null, "Q1100：撤退离地结束本次工作");
        Assert.That(s.Items.Any(i => i.ReservedBy == id), Is.False);
        Assert.That((fiber.Owner, fiber.ReservedBy), Is.EqualTo(("field", (int?)null)), "原地面部分回原行动地");
        Assert.That((stone.Owner, stone.ReservedBy), Is.EqualTo((ItemInstance.Carried, (int?)null)), "原随身部分留随身");
    }

    // ───────────── Q489 ─────────────

    [Test]
    public void Q489_A01_ExitSafehouse_NoApproachCheck_TimerKept_NextBoundaryFights()
    {
        var s = Field(1);
        var w = E.DebugSpawnEnemy(s, "shambler", "field");
        var key = Key(w);
        s.CurrentZone = GameEngine.SafehouseZone;
        SetLeft(s, key, 0);
        var due = s.EnemyCards.Timers[key].DueMinute;
        var rng = s.Rng.State;
        Assert.That(E.ExitSafehouse(s).Ok, Is.True);
        Assert.That(s.CurrentZone, Is.EqualTo(GameEngine.ExteriorZone));
        Assert.That(s.Battle, Is.Null, "出屋本身不检查敌方接近");
        Assert.That(s.Log.Last().Text, Does.StartWith("走出"), "不追加开战句");
        Assert.That((Left(s, key), s.EnemyCards.Timers[key].DueMinute), Is.EqualTo((0, due)), "到期身份保留，不重抽不刷新");
        Assert.That(s.Rng.State, Is.EqualTo(rng));
        // 界面重绘（只读视图）不补检查
        _ = E.EnemyCardsHere(s);
        _ = E.CanApproachCard(s, key);
        Assert.That(s.Battle, Is.Null);
        // 主动接近仍可用；下一个合法边界按原顺序开战
        Assert.That(E.CanApproachCard(s, key), Is.Null);
        Assert.That(E.CheckEnemyApproach(s), Is.True);
        Assert.That(s.Battle, Is.Not.Null);
    }

    // ───────────── Q490 ─────────────

    private ItemInstance PausedRepair(GameState s, double durabilityFraction = 0.5)
    {
        var mach = E.DebugCreateEquipment(s, "machete", "white", ItemInstance.Carried);
        mach.Durability = Math.Round(mach.MaxDurability!.Value * durabilityFraction);
        E.DebugGive(s, "scrap", 1);
        E.DebugGive(s, "parts", 1);
        Start(s, new ActionRequest { Type = ActionType.Craft, TargetId = "repair_selected_tool", InstanceId = mach.InstanceId });
        Assert.That(s.Main!.Type, Is.EqualTo(ActionType.Repair));
        Assert.That(Step(s), Is.EqualTo(SegmentStatus.Running));
        Pause(s);
        Assert.That(E.IsLocked(s, mach), Is.True, "开工锁定目标");
        return mach;
    }

    [Test]
    public void Q490_A01_Repair_OwnLockOk_ZeroOk_FullAndNoDurabilityBlocked_Verbatim()
    {
        var s = Field(1);
        var mach = PausedRepair(s);
        var max = mach.MaxDurability!.Value;
        var name = Name("machete");
        Assert.That(E.ResumeBlockReason(s), Is.Null, "自己的锁不阻断");
        mach.Durability = 0;
        Assert.That(E.ResumeBlockReason(s), Is.Null, "耐久0仍可维护");
        mach.Durability = max;
        AssertBlocked(s, $"{name}耐久已满，无法继续维护，请退出行动。");
        mach.Durability = null;
        AssertBlocked(s, $"{name}没有耐久，无法继续维护，请退出行动。");
        mach.Durability = double.NaN;
        AssertBlocked(s, GameEngine.ResumeTargetGone);
        mach.Durability = max - 1;
        mach.ReservedBy = 99999;
        AssertBlocked(s, "行动占用：这件工具正被使用");
        mach.ReservedBy = null;
        mach.Owner = "camp";
        AssertBlocked(s, "只能维护随身或当前地面的工具");
        mach.Owner = ItemInstance.Carried;
        // 合法续接：沿同一进度，不重扣料
        var elapsed = s.Main!.ElapsedMinutes;
        var scrap = s.Items.Where(i => i.ItemId == "scrap").Sum(i => i.Count);
        Assert.That(E.Resume(s).Ok, Is.True);
        Assert.That(s.Main!.ElapsedMinutes, Is.EqualTo(elapsed));
        Assert.That(s.Items.Where(i => i.ItemId == "scrap").Sum(i => i.Count), Is.EqualTo(scrap));
        // 原目标消失
        Pause(s);
        s.Items.Remove(mach);
        AssertBlocked(s, GameEngine.ResumeTargetGone);
    }

    [Test]
    public void Q490_A01_FieldPatch_FullBeforeDeficit_DeficitUnder01_PackShort_Verbatim()
    {
        var s = Field(1);
        s.Items.RemoveAll(i => i.ItemId.StartsWith("field_patch"));
        var mach = E.DebugCreateEquipment(s, "machete", "white", s.Ground);
        mach.MaxDurability = 100;
        mach.Durability = 50;
        var pack = E.CreateItems(s, "field_patch", 1, s.Ground).Single();
        Start(s, new ActionRequest { Type = ActionType.Craft, TargetId = GameEngine.FieldPatchRepairId, InstanceId = mach.InstanceId, PatchInstanceId = pack.InstanceId });
        Pause(s);
        var name = Name("machete");
        Assert.That(E.ResumeBlockReason(s), Is.Null, "自己的锁不阻断");
        mach.Durability = 0;
        Assert.That(E.ResumeBlockReason(s), Is.Null);
        mach.Durability = 100;
        AssertBlocked(s, $"{name}耐久已满，无法继续修补，请退出行动。");
        mach.Durability = 99.95;
        AssertBlocked(s, $"{name}缺损不足0.1，无需修补，请退出行动。");
        mach.Durability = null;
        AssertBlocked(s, $"{name}没有耐久，无法继续修补，请退出行动。");
        mach.Durability = 50;
        mach.MaxDurability = double.PositiveInfinity;
        AssertBlocked(s, GameEngine.ResumeTargetGone);
        mach.MaxDurability = 100;
        pack.Durability = 0;
        AssertBlocked(s, $"{E.ItemName(pack.ItemId)}剩余耐久0，不够恢复0.1");
        pack.Durability = pack.MaxDurability;
        var elapsed = s.Main!.ElapsedMinutes;
        Assert.That(E.Resume(s).Ok, Is.True, "不换目标、不换包");
        Assert.That((s.Main!.ToolInstanceId, s.Main.PatchInstanceId, s.Main.ElapsedMinutes), Is.EqualTo(((int?)mach.InstanceId, (int?)pack.InstanceId, elapsed)));
        Assert.That(pack.Durability, Is.EqualTo(pack.MaxDurability), "继续只检查，不先扣包");
    }

    [Test]
    public void Q490_A02_Salvage_DurabilityZeroOrFullStillOk_UseRulesBlock()
    {
        var s = Field(1);
        s.Skills["crafting"].Level = 3;
        var mach = E.DebugCreateEquipment(s, "machete", "green", ItemInstance.Carried);
        var max = mach.MaxDurability!.Value;
        mach.Durability = Math.Round(max / 2);
        var yield0 = E.SalvageYield(s, mach);
        Start(s, new ActionRequest { Type = ActionType.Craft, TargetId = "salvage_equipment", InstanceId = mach.InstanceId });
        Assert.That(s.Main!.Type, Is.EqualTo(ActionType.Salvage));
        Pause(s);
        Assert.That(E.ResumeBlockReason(s), Is.Null, "自己的锁不阻断");
        mach.Durability = 0;
        Assert.That(E.ResumeBlockReason(s), Is.Null, "耐久0仍可拆");
        mach.Durability = max;
        Assert.That(E.ResumeBlockReason(s), Is.Null, "满耐久仍可拆");
        Assert.That(E.SalvageYield(s, mach), Is.EqualTo(yield0), "不按耐久变化增减产出");
        mach.Owner = ItemInstance.EquipOwner(GameState.PlayerId, EquipSlots.Weapon1);
        AssertBlocked(s, "穿戴中：先在装备区卸下再拆解");
        mach.Owner = "camp";
        AssertBlocked(s, "只能拆解随身或当前地面的装备");
        mach.Owner = ItemInstance.Carried;
        s.Skills["crafting"].Level = 1;
        AssertBlocked(s, $"拆解1阶装备需要{E.SkillName("crafting")}3级（当前1级）");
        s.Skills["crafting"].Level = 3;
        mach.Durability = double.NaN;
        AssertBlocked(s, GameEngine.ResumeTargetGone);
        mach.Durability = 0;
        Assert.That(E.Resume(s).Ok, Is.True);
        Assert.That(Run(s), Is.EqualTo(SegmentStatus.Completed));
        Assert.That(s.Items.Any(i => i.InstanceId == mach.InstanceId), Is.False);
    }

    // ───────────── Q491 ─────────────

    [Test]
    public void Q491_A01_TravelPausedWithSub_SameText_ThenContinuesWithoutReconfirm()
    {
        var s = Field(1);
        Start(s, ActionRequest.Travel("camp"));
        Step(s);
        Pause(s);
        var progress = s.Main!.ElapsedMinutes;
        Start(s, ActionRequest.Rest(30));
        Assert.That(E.ResumeBlockReason(s), Is.EqualTo("当前行动还没结束。"));
        Assert.That(E.Resume(s).Reason, Is.EqualTo("当前行动还没结束。"));
        Assert.That(Run(s, autoResume: false), Is.EqualTo(SegmentStatus.Completed));
        Assert.That(s.Sub, Is.Null);
        Assert.That(E.ResumeBlockReason(s), Is.Null, "子行动结束后解除");
        Assert.That(E.Resume(s).Ok, Is.True);
        Assert.That((s.Main!.Type, s.Main.ElapsedMinutes), Is.EqualTo((ActionType.Travel, progress)), "沿原进度，不当新开工");
    }
}
