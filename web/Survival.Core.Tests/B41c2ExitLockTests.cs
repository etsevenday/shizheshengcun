using Newtonsoft.Json.Linq;
using Survival.Core.Config;
using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// B41c2 锁定行动不可退出（S01 Q506、Q514—Q519、Q523—Q527；S07／S11／S14 同批；R05 Q521；逐字稿本批表）。
/// 开工同源真实总预计恰为 5 分钟即锁定，锁定行动没有退出入口、旧请求拒绝「该行动不可中途取消」；暂停的锁定行动「继续行动」分类复核，
/// 永久失效自动中止、暂时原因保持暂停、等待加工全部完成只完成；原工具例外拾取；旧档只按原固定计划认定。
/// </summary>
public class B41c2ExitLockTests : TestBase
{
    private const string NoCancel = "该行动不可中途取消";

    /// <summary>带改动配置的新引擎与旧系统新局（只改时长，用来造出「开工 5 分钟」的工作量型行动）。</summary>
    private (GameEngine e, GameState s) With(Action<GameConfig> tweak)
    {
        var cfg = LoadConfig();
        tweak(cfg);
        var e = new GameEngine(cfg);
        var s = e.NewGame(12345);
        e.DebugLegacyStory(s);
        return (e, s);
    }

    private static void Pause(GameState s)
    {
        s.Main!.Status = ActionStatus.Paused;
        s.Main.PauseReason = PauseReason.StatAlert;
    }

    private static string AsSchema48(GameState s)
    {
        var o = JObject.Parse(SaveSerializer.Serialize(s));
        o["SchemaVersion"] = "48";
        foreach (var key in new[] { "Main", "Sub", "StashedWork" })
            if (o[key] is JObject a) a.Remove("NoExit");
        return o.ToString();
    }

    private GameState Camping(GameEngine e)
    {
        var s = e.NewGame(12345);
        e.DebugLegacyStory(s);
        e.DebugTeleport(s, "w19_survey");
        s.WorldEnemies.RemoveAll(x => x.LocationId == "w19_survey");
        return s;
    }

    /// <summary>锁定行动的退出与取消：拒绝原句，整份状态不变（不返料、不清行动、不推进时间）。</summary>
    private void AssertNoExit(GameEngine e, GameState s)
    {
        var p = e.ActionProgress(s)!;
        Assert.That((p.Locked, p.Exit, p.ExitNote, p.ExitForfeits), Is.EqualTo((true, ExitKind.None, GameEngine.NoExitNote, false)));
        var before = SaveSerializer.Serialize(s);
        Assert.That((e.RequestExit(s).Ok, e.RequestExit(s).Reason), Is.EqualTo((false, NoCancel)));
        Assert.That((e.Cancel(s).Ok, e.Cancel(s).Reason), Is.EqualTo((false, NoCancel)));
        Assert.That(SaveSerializer.Serialize(s), Is.EqualTo(before));
    }

    // ───────────── Q506／Q516 开工认定 ─────────────

    [Test]
    public void Q506_A01_StoveDeploy_LockedFromStart_NoExit_RunsToEnd()
    {
        var s = Camping(E);
        var stove = E.CreateItems(s, "folding_stove", 1, ItemInstance.Carried).Single();
        Assert.That(E.Preview(s, GameEngine.Deploy(stove)).NoExit, Is.True, "预览与真实开工同一认定");
        var t0 = s.GameMinute;
        Start(s, GameEngine.Deploy(stove));
        Assert.That(s.Main!.NoExit, Is.True);
        AssertNoExit(E, s);
        Run(s);
        Assert.That((s.Main, s.GameMinute - t0, E.IsDeployed(s, stove)), Is.EqualTo(((ActionState?)null, 5, true)));
    }

    [Test]
    public void Q516_A01_TenMinuteDeploy_NotLocked_LongActionAtFiveRemaining_StillExitable()
    {
        var s = Camping(E);
        var tarp = E.CreateItems(s, "field_tarp", 1, ItemInstance.Carried).Single();
        Assert.That(E.Preview(s, GameEngine.Deploy(tarp)).NoExit, Is.False);
        Start(s, GameEngine.Deploy(tarp));
        Assert.That(s.Main!.NoExit, Is.False, "10 分钟部署不锁");
        E.Advance(s);
        var p = E.ActionProgress(s)!;
        Assert.That((p.Locked, p.Exit), Is.EqualTo((false, ExitKind.StopAtBoundary)), "长行动剩 5 分钟仍按原规则可退出");
        Assert.That(E.RequestExit(s).Ok, Is.True);
        Assert.That(s.Main, Is.Null);
    }

    [Test]
    public void Q516_A02_FiveMinuteWork_LocksBySameSourceEstimate_SlowerLaterStaysLocked()
    {
        var (e, s) = With(c => c.Recipes.Single(r => r.Id == "bandage").BaseMinutes = 5);
        e.DebugGive(s, "cloth", 1);
        e.DebugGive(s, "herb", 1);
        var pv = e.Preview(s, ActionRequest.Craft("bandage"));
        Assert.That(pv.Ok, Is.True, pv.Reason);
        Assert.That((pv.EstimatedMinutes, pv.NoExit), Is.EqualTo((5, true)), "同源真实总预计恰为 5 分钟");
        Assert.That(e.Start(s, ActionRequest.Craft("bandage")).Ok, Is.True);
        Assert.That(s.Main!.NoExit, Is.True);
        AssertNoExit(e, s);
        // 开工后变慢（暂停或倍率变化）也不恢复退出：锁定随存档保存
        Pause(s);
        var t = SaveSerializer.Deserialize(SaveSerializer.Serialize(s), e.Config);
        Assert.That(t.Main!.NoExit, Is.True);
        Assert.That(e.ActionProgress(t)!.Locked, Is.True);
        Assert.That(e.Cancel(t).Reason, Is.EqualTo(NoCancel));

        // 15 分钟原配方：剩余需求超过一段，不锁
        var s2 = New();
        E.DebugGive(s2, "cloth", 1);
        E.DebugGive(s2, "herb", 1);
        Start(s2, ActionRequest.Craft("bandage"));
        Assert.That(s2.Main!.NoExit, Is.False);
    }

    // ───────────── Q516／Q524 旧档 ─────────────

    [Test]
    public void Q524_A01_Migration48_FixedPlanFiveLocks_WaitCookAndWorkNot()
    {
        // 固定计划 5 分钟（柴炉部署）：迁移认定锁定
        var s = Camping(E);
        Start(s, GameEngine.Deploy(E.CreateItems(s, "folding_stove", 1, ItemInstance.Carried).Single()));
        var t = SaveSerializer.Deserialize(AsSchema48(s), Cfg);
        Assert.That((t.SchemaVersion, t.Main!.NoExit), Is.EqualTo(("49", true)));

        // 10 分钟部署：不锁
        var s2 = Camping(E);
        Start(s2, GameEngine.Deploy(E.CreateItems(s2, "field_tarp", 1, ItemInstance.Carried).Single()));
        Assert.That(SaveSerializer.Deserialize(AsSchema48(s2), Cfg).Main!.NoExit, Is.False);

        // 等待加工（计划分钟混有兜底值）与工作量型：一律不锁，保留原退出方式
        var wait = new ActionState { Type = ActionType.WaitCook, PlannedMinutes = 5 };
        Assert.That(GameEngine.LegacyFixedPlanLock(wait), Is.False);
        var work = new ActionState { Type = ActionType.Craft, BaseWorkMinutes = 5 };
        Assert.That(GameEngine.LegacyFixedPlanLock(work), Is.False);
        Assert.That(GameEngine.LegacyFixedPlanLock(new ActionState { Type = ActionType.Craft, BaseWorkMinutes = 5, FixedWait = true }), Is.True);
        Assert.That(GameEngine.LegacyFixedPlanLock(new ActionState { Type = ActionType.Ignite, PlannedMinutes = 5 }), Is.True);
        Assert.That(GameEngine.LegacyFixedPlanLock(new ActionState { Type = ActionType.Wait, PlannedMinutes = 30 }), Is.False);
    }

    // ───────────── Q514／Q518 永久失效自动中止 ─────────────

    private (GameEngine e, GameState s, ItemInstance knife, ItemInstance carcass, string label) LockedButcher()
    {
        var (e, s) = With(c => c.AnimalCarcasses.Single(x => x.CarcassItemId == "carcass_feral_dog").ButcherBaseMinutes = 5);
        var knife = e.CreateItems(s, "stone_knife", 1, ItemInstance.Carried).Single();
        var carcass = e.CreateItems(s, "carcass_feral_dog", 1, s.Ground).Single();
        Assert.That(e.Start(s, ActionRequest.Butcher(carcass.InstanceId)).Ok, Is.True);
        Assert.That(s.Main!.NoExit, Is.True, "5 分钟处理尸体开工即锁定");
        var label = s.Main.Label;
        Pause(s);
        return (e, s, knife, carcass, label);
    }

    [Test]
    public void Q514_A01_PermanentReason_ResumeAbortsOnce_ReturnClause_ElapsedKept()
    {
        var (e, s, knife, carcass, label) = LockedButcher();
        s.Items.Remove(knife);
        var tools = string.Join("/", Cfg.AnimalCarcasses.Single(x => x.CarcassItemId == "carcass_feral_dog").RequiredCarriedTools.Select(e.ItemName));
        var why = $"缺少本次处理所需的有效{tools}，无法继续。";
        // 只读：中央条写原因与系统收尾说明，不改状态
        var before = SaveSerializer.Serialize(s);
        var p = e.ActionProgress(s)!;
        Assert.That((p.Locked, p.Exit, p.ResumeNote, p.ResumeAborts), Is.EqualTo((true, ExitKind.None, why, true)));
        Assert.That(e.LockedResume(s).Kind, Is.EqualTo(LockedResumeKind.Permanent));
        Assert.That(SaveSerializer.Serialize(s), Is.EqualTo(before));
        // 真正继续：自动中止同一行动，按原规则返还，不推进时间
        var t0 = s.GameMinute;
        var r = e.Resume(s);
        Assert.That((r.Ok, r.Aborted), Is.EqualTo((true, true)));
        Assert.That((s.Main, s.GameMinute), Is.EqualTo(((ActionState?)null, t0)));
        Assert.That(s.Log.Last().Text, Is.EqualTo($"{label}已中止。原因：{why}尸体已返还{e.ZoneName(s, s.Ground)}地面。保质期未刷新，没有产物。已耗时间保留。"));
        Assert.That((carcass.Owner, carcass.ReservedBy), Is.EqualTo((s.Ground, (int?)null)));
        Assert.That(s.Items.Count(i => i.ItemId == "raw_meat"), Is.Zero);
        Assert.That(e.Resume(s).Ok, Is.False, "只收束一次");
    }

    [Test]
    public void Q514_A02_TemporaryReason_StaysPaused_NoAbort()
    {
        var (e, s, knife, _, _) = LockedButcher();
        knife.Owner = s.Ground;   // 原工具在当前地面：只缺拾取（暂时）
        var why = $"需要先拾取原工具：{e.ItemName(knife.ItemId)}。";
        var p = e.ActionProgress(s)!;
        Assert.That((p.ResumeNote, p.ResumeAborts), Is.EqualTo((why, false)));
        var before = SaveSerializer.Serialize(s);
        var r = e.Resume(s);
        Assert.That((r.Ok, r.Reason), Is.EqualTo((false, why)));
        Assert.That(SaveSerializer.Serialize(s), Is.EqualTo(before), "暂时原因保持暂停，不中止");
    }

    // ───────────── Q523 原工具例外拾取 ─────────────

    [Test]
    public void Q523_A01_OwnGroundTool_PickupWhole_ZeroTime_LockKept_ThenResume()
    {
        var (e, s, knife, _, _) = LockedButcher();
        knife.Owner = s.Ground;
        Assert.That(e.LockedToolPickupOpen(s, knife), Is.True);
        var t0 = s.GameMinute;
        var id = s.Main!.ActionId;
        var r = e.DoInstant(s, InstantRequest.Pickup(knife.InstanceId, knife.Count));
        Assert.That((r.Ok, r.Aborted), Is.EqualTo((true, false)), r.Reason);
        Assert.That((knife.Owner, s.GameMinute, s.Main!.ActionId, s.Main.Status), Is.EqualTo((ItemInstance.Carried, t0, id, ActionStatus.Paused)), "0 时间、同一行动仍暂停");
        Assert.That(s.Main.LockedItemIds, Does.Contain(knife.InstanceId), "原工具锁保持");
        Assert.That(e.Resume(s).Ok, Is.True, "拾取后手动继续");
        Run(s);
        Assert.That(s.Main, Is.Null);
        Assert.That(s.Items.Count(i => i.ItemId == "raw_meat"), Is.GreaterThan(0));
    }

    [Test]
    public void Q523_A02_OtherItemsStillBlocked_CapacityFull_PickupAbortsAndToolStays()
    {
        var (e, s, knife, _, label) = LockedButcher();
        knife.Owner = s.Ground;
        // 其他地面物品不因例外放开
        var rope = e.CreateItems(s, "cloth", 1, s.Ground).Single();
        Assert.That(e.DoInstant(s, InstantRequest.Pickup(rope.InstanceId, 1)).Ok, Is.False);
        // 塞满随身卡位
        foreach (var id in new[] { "herb", "wood", "stone", "fiber", "scrap", "parts", "salt", "thread" })
        {
            if (e.RootCardCount(s) >= e.MaxRootCards) break;
            e.CreateItems(s, id, 1, ItemInstance.Carried);
        }
        Assert.That(e.RootCardCount(s), Is.EqualTo(e.MaxRootCards));
        var why = $"随身卡位已满（{e.MaxRootCards}/{e.MaxRootCards}张），无法拾取原工具。";
        var p = e.ActionProgress(s)!;
        Assert.That((p.ResumeNote, p.ResumeAborts), Is.EqualTo((why, true)), "只读显示具体原因与自动中止说明");
        Assert.That(e.LockedToolPickupOpen(s, knife), Is.False);
        var r = e.DoInstant(s, InstantRequest.Pickup(knife.InstanceId, knife.Count));
        Assert.That((r.Ok, r.Aborted), Is.EqualTo((true, true)));
        Assert.That(s.Main, Is.Null);
        Assert.That((knife.Owner, e.IsLocked(s, knife)), Is.EqualTo((s.Ground, false)), "工具不移动，锁随行动释放");
        Assert.That(s.Log.Last().Text, Does.StartWith($"{label}已中止。原因：{why}").And.EndWith("已耗时间保留。"));
    }

    // ───────────── Q515 等待加工全部完成 ─────────────

    [Test]
    public void Q515_A01_LockedWaitCook_AllDone_ResumeOnlyCompletes_NoTick()
    {
        var s = New();
        var pot = First(s, "pot");
        E.DebugGive(s, "dirty_water", 1, "camp");
        Assert.That(E.DoInstant(s, InstantRequest.FillPot(First(s, "dirty_water", "camp").InstanceId, pot.InstanceId, 1)).Ok, Is.True);
        Assert.That(E.DoInstant(s, InstantRequest.PutInFire(pot.InstanceId)).Ok, Is.True);
        var fac = s.Locations["camp"].Facility;
        fac.FireLit = true;
        fac.FuelMinutes = 180;
        Start(s, ActionRequest.WaitCook());
        s.Main!.NoExit = true;
        Pause(s);
        foreach (var pid in s.Main.CookTargets) fac.Completed.Add(pid + "@done");
        Assert.That(E.LockedResume(s).Kind, Is.EqualTo(LockedResumeKind.CookDone));
        var t0 = s.GameMinute;
        var r = E.Resume(s);
        Assert.That(r.Ok, Is.True);
        Assert.That((s.Main, s.GameMinute), Is.EqualTo(((ActionState?)null, t0)), "只完成，不额外 tick");
        Assert.That(s.Log.Last().Text, Does.StartWith("等待加工结束："));
    }

    // ───────────── Q525 维护／修补锁定版原因 ─────────────

    [Test]
    public void Q525_A01_LockedRepair_ReasonsWithoutExitTail_AbortSentence()
    {
        var s = New();
        var mach = E.DebugCreateEquipment(s, "machete", "white", ItemInstance.Carried);
        mach.Durability = Math.Round(mach.MaxDurability!.Value * 0.5);
        E.DebugGive(s, "scrap", 1);
        E.DebugGive(s, "parts", 1);
        Start(s, new ActionRequest { Type = ActionType.Craft, TargetId = "repair_selected_tool", InstanceId = mach.InstanceId });
        Assert.That(s.Main!.Type, Is.EqualTo(ActionType.Repair));
        s.Main.NoExit = true;   // 视作开工 5 分钟锁定的维护
        Pause(s);
        var name = E.ItemName("machete");
        var label = s.Main.Label;
        mach.Durability = null;
        Assert.That(E.ActionProgress(s)!.ResumeNote, Is.EqualTo($"{name}没有耐久，无法继续维护。"));
        mach.Durability = double.NaN;
        Assert.That(E.ActionProgress(s)!.ResumeNote, Is.EqualTo(GameEngine.LockedTargetGone));
        mach.Durability = mach.MaxDurability;
        var why = $"{name}耐久已满，无法继续维护。";
        Assert.That(E.ActionProgress(s)!.ResumeNote, Is.EqualTo(why));
        Assert.That(E.Resume(s).Aborted, Is.True);
        Assert.That(s.Log.Last().Text, Is.EqualTo($"{label}已中止。原因：{why}预留材料已返还{E.ZoneName(s, s.Ground)}地面。未完成本次维护。已耗时间保留。"));
        Assert.That(s.Items.Any(i => i.ReservedBy != null), Is.False);
    }

    // ───────────── Q517／Q526 锁定版提示句 ─────────────

    [Test]
    public void Q526_A01_LockedPaused_SaveAndCookWaitBusyTexts()
    {
        var (e, s, _, _, _) = LockedButcher();
        Assert.That(GameEngine.ManualSaveBlockText(e.ManualSaveBlocked(s)), Is.EqualTo("行动暂停中，先继续行动再存档。"));
        // 非锁定暂停保留原句
        var s2 = New();
        E.DebugGive(s2, "cloth", 1);
        E.DebugGive(s2, "herb", 1);
        Start(s2, ActionRequest.Craft("bandage"));
        Pause(s2);
        Assert.That(GameEngine.ManualSaveBlockText(E.ManualSaveBlocked(s2)), Is.EqualTo("行动暂停中，先继续或退出行动再存档。"));
    }
}
