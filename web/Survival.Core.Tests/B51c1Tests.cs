using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 批次 B51c1「取消与预估文案」（开发记录 B51c1）：S01／S06／S08／S14／R02 文首「2026-11-16 B51 Q771—Q779」中 Q771—Q776 部分与
/// 《重墨 V2 开工裁定与界面逐字稿》同节；验收 B51-Q771-01／02、Q772-01、Q773-01、Q774-01、Q775-01、Q776-01。
/// 暂用（开发方建议 A，待文案定稿）：Q873 括号原因、Q874 拼句细节、Q875 不可信不接损坏后缀。
/// </summary>
public class B51c1Tests : TestBase
{
    protected override bool NoEnemyApproach => true;

    private static string Web(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "Survival.Web" }.Concat(parts).ToArray()));

    // ───────────── M1 Q771 普通取消／退出预告分句 ─────────────

    /// <summary>绷带：布料×1＋草药×1（草药可选有期限）。</summary>
    private GameState Bandage(int? herbMinutesLeft)
    {
        var s = New();
        s.Items.RemoveAll(i => i.ItemId is "cloth" or "herb" or "bandage");
        E.DebugGive(s, "cloth", 1);
        var herb = E.CreateItems(s, "herb", 1, ItemInstance.Carried).Single();
        herb.ExpiresAt = herbMinutesLeft is int m ? s.GameMinute + m : null;
        return s;
    }

    /// <summary>
    /// B51-Q771-01：无期限原料的普通制作——预告与结果逐字为用户批准短稿（S08 B51 §Q771）：
    /// 「退出将取消制作。材料将退回{地点}地面。已耗时间不退。」「已取消制作。材料已退回{地点}地面。已耗时间不退。」，无期限不加期限、无半成品旧话。
    /// </summary>
    [Test]
    public void Q771_A01_Craft_NoShelf_ApprovedShortDraft()
    {
        var s = Bandage(null);
        Start(s, ActionRequest.Craft("bandage"));
        Steps(s, 1);
        var zone = E.ZoneName(s, s.Ground);
        var p = E.ActionProgress(s)!;
        Assert.That(p.ExitNote, Is.EqualTo($"退出将取消制作。材料将退回{zone}地面。已耗时间不退。"));
        Assert.That(p.ExitForfeits, Is.True);
        Assert.That(E.Cancel(s).Ok, Is.True);
        Assert.That(s.Log.Last().Text, Is.EqualTo($"已取消制作。材料已退回{zone}地面。已耗时间不退。"));
        Assert.That(s.Items.Where(i => i.ItemId is "cloth" or "herb").All(i => i.Owner == s.Ground && i.ReservedBy == null), Is.True, "材料真实退回当前地面");
        Assert.That(s.Items.Any(i => i.ItemId == "bandage"), Is.False, "未完成不生成产物");
    }

    /// <summary>
    /// B51-Q771-01：有保质返料时，返还句后接到期句、再接一次已耗时间（S08 B51 §Q771 拼接顺序）；
    /// 结果「{原料名}×{数量}保质至{日期时刻}。」（Q771 原句），预告沿逐字稿 B51 表「立即取消的只读返料预告」原句，C＋R 只恢复一次。
    /// </summary>
    [Test]
    public void Q771_A01_Craft_WithShelf_ExpiryClauseOnce()
    {
        var s = Bandage(120);
        Start(s, ActionRequest.Craft("bandage"));
        Steps(s, 2);
        s.GameMinute += 30;   // 期间世界时间流逝，受保护份 R 不变（R＝120，开工时余量）
        var zone = E.ZoneName(s, s.Ground);
        var at = Clock.DayTime(s.GameMinute + 120);
        Assert.That(E.ActionProgress(s)!.ExitNote, Is.EqualTo($"退出将取消制作。材料将退回{zone}地面。草药×1退回后恢复剩余保质，预计保质到{at}。已耗时间不退。"));
        Assert.That(E.Cancel(s).Ok, Is.True);
        Assert.That(s.Log.Last().Text, Is.EqualTo($"已取消制作。材料已退回{zone}地面。草药×1保质至{at}。已耗时间不退。"));
        var herb = s.Items.Single(i => i.ItemId == "herb");
        Assert.That((herb.ExpiresAt, herb.ShelfPausedMinutes), Is.EqualTo(((int?)(s.GameMinute + 120), (int?)null)), "C＋R 恢复一次");
    }

    /// <summary>
    /// B51-Q771-01：维修（总册「维修」，Q874 A 暂用）普通取消／预告：引句「已取消维修。」，返还材料句，Q471 维护后果原句，一次已耗时间不退。
    /// </summary>
    [Test]
    public void Q771_A01_Repair_VerbWeixiu_Q471Consequence()
    {
        var s = New();
        var mach = E.DebugCreateEquipment(s, "machete", "white", ItemInstance.Carried);
        mach.Durability = Math.Round(mach.MaxDurability!.Value * 0.5);
        E.DebugGive(s, "scrap", 1);
        E.DebugGive(s, "parts", 1);
        Start(s, new ActionRequest { Type = ActionType.Craft, TargetId = "repair_selected_tool", InstanceId = mach.InstanceId });
        Assert.That(s.Main!.Type, Is.EqualTo(ActionType.Repair));
        Steps(s, 1);
        var zone = E.ZoneName(s, s.Ground);
        Assert.That(E.ActionProgress(s)!.ExitNote, Is.EqualTo($"退出将取消维修。材料将退回{zone}地面。不会完成本次维护。已耗时间不退。"));
        Assert.That(E.Cancel(s).Ok, Is.True);
        Assert.That(s.Log.Last().Text, Is.EqualTo($"已取消维修。材料已退回{zone}地面。未完成本次维护。已耗时间不退。"));
        Assert.That(mach.Owner, Is.EqualTo(ItemInstance.Carried), "维修目标只解锁，不当返还材料");
    }

    /// <summary>B51-Q771-02：拆解没有可返还材料——不造返还句与期限（S08 B51 §Q771「无期限、无返还则省略对应分句」），Q471 拆解后果原句。</summary>
    [Test]
    public void Q771_A02_Salvage_NoReturn_NoReturnClause()
    {
        var s = New();
        s.Skills["crafting"].Level = 3;
        var mach = E.DebugCreateEquipment(s, "machete", "green", ItemInstance.Carried);
        Start(s, new ActionRequest { Type = ActionType.Craft, TargetId = "salvage_equipment", InstanceId = mach.InstanceId });
        Assert.That(s.Main!.Type, Is.EqualTo(ActionType.Salvage));
        Steps(s, 1);
        Assert.That(E.ActionProgress(s)!.ExitNote, Is.EqualTo("退出将取消拆解。不会获得拆解产物。已耗时间不退。"));
        Assert.That(E.Cancel(s).Ok, Is.True);
        Assert.That(s.Log.Last().Text, Is.EqualTo("已取消拆解。未获得拆解产物。已耗时间不退。"));
        Assert.That(mach.Owner, Is.EqualTo(ItemInstance.Carried));
    }

    /// <summary>
    /// B51-Q771-01：处理尸体普通退出预告——沿 Q722 引句与后果、返还句 Q771 A「尸体将返还{地点}地面。」、只读恢复预告、一次已耗时间不退；预告不改状态。
    /// </summary>
    [Test]
    public void Q771_A01_Butcher_PlainPreview_Q722LeadAndCarcassReturn()
    {
        var s = New();
        var carcass = E.CreateItems(s, "carcass_feral_dog", 1, s.Ground).Single();
        E.CreateItems(s, "stone_knife", 1, ItemInstance.Carried);
        Start(s, ActionRequest.Butcher(carcass.InstanceId));
        var r = carcass.ShelfPausedMinutes!.Value;
        Steps(s, 2);
        var zone = E.ZoneName(s, s.Ground);
        var at = Clock.DayTime(s.GameMinute + r);
        Assert.That(E.ActionProgress(s)!.ExitNote, Is.EqualTo($"退出将放弃本次处理。尸体将返还{zone}地面。野犬尸体×1退回后恢复剩余保质，预计保质到{at}。不会获得产物。已耗时间不退。"));
        Assert.That((carcass.ReservedBy, carcass.ShelfPausedMinutes), Is.EqualTo(((int?)s.Main!.ActionId, (int?)r)), "预告只读");
    }

    /// <summary>
    /// Q874 A(c)（暂用）：锁定制作自动中止只删「未生成半成品」分句，其余（中止主句、返还句、已耗时间保留）照旧。
    /// </summary>
    [Test]
    public void Q874_A_LockedCraftAbort_OnlyHalfProductClauseRemoved()
    {
        var s = Bandage(null);
        Start(s, ActionRequest.Craft("bandage"));
        var label = s.Main!.Label;
        s.Main.NoExit = true;
        s.Main.Status = ActionStatus.Paused;
        s.Main.PauseReason = PauseReason.StatAlert;
        s.Items.Remove(s.Items.Single(i => i.ItemId == "herb"));   // 预留丢失 → 永久原因
        var r = E.Resume(s);
        Assert.That(r.Aborted, Is.True);
        Assert.That(s.Log.Last().Text, Is.EqualTo($"{label}已中止。原因：本次行动的预留已变化，无法继续。预留材料已返还{E.ZoneName(s, s.Ground)}地面。已耗时间保留。"));
    }

    /// <summary>Q874 A(d)（暂用）：暂停层与帮助只删「半成品」分句，其余原样。</summary>
    [Test]
    public void Q874_A_PauseLayerAndHelp_HalfProductRemoved_RestKept()
    {
        var pause = Web("Components", "PauseLayer.razor");
        Assert.That(pause, Does.Contain("ActionType.Craft or ActionType.Build => \"预留材料返还当前地面。\""));
        var help = Web("Services", "HelpCatalog.cs");
        Assert.That(help, Does.Contain("中途退出：材料全部返还当前地面，已经过去的时间不退。材料在完成前腐败则制作中止。"));
        foreach (var src in new[] { pause, help, Web("..", "..", "src", "Core", "Engine", "GameEngine.cs"), Web("..", "..", "src", "Core", "Engine", "GameEngine.KeptWork.cs"),
                                    Web("..", "..", "src", "Core", "Engine", "GameEngine.ExitLock.cs"), Web("..", "..", "src", "Core", "Engine", "GameEngine.Progress.cs") })
            Assert.That(src, Does.Not.Contain("半成品。\""), "不再有玩家可见的半成品旧话");
    }

    // ───────────── M2 Q772 工具本单损坏提示、Q773 部分暂停副标、Q774 晾制说明 ─────────────

    private const string BreakSuffix = " · 预计本次生产结束后损坏";        // S08／R02 B51 §Q772
    private const string BreakState = "本次生产结束后损坏";                 // 同上
    private const string GuardNote = "耐久不足不会中断本次生产；工具将在本次生产结束后损坏。";   // 逐字稿 B51 表原工具本单保障说明

    /// <summary>野犬尸体＋石刀（耐久 d）；野犬处理成功磨损 1（策划数据 animalCarcasses.carcass_feral_dog.toolWearOnSuccess）。</summary>
    private (GameState s, ItemInstance knife, ItemInstance carcass) Carcass(double knifeDurability, GameEngine? eng = null)
    {
        var e = eng ?? E;
        var s = e.NewGame(12345);
        e.DebugLegacyStory(s);
        s.Items.RemoveAll(i => i.ItemId is "stone_knife" or "kitchen_knife" or "hatchet");
        var knife = e.CreateItems(s, "stone_knife", 1, ItemInstance.Carried).Single();
        knife.Durability = knifeDurability;
        var carcass = e.CreateItems(s, "carcass_feral_dog", 1, s.Ground).Single();
        return (s, knife, carcass);
    }

    /// <summary>B51-Q772-01：本单成功磨损 1 把正耐久 1 的刀用到 0——预估工具行带后缀、保障说明在说明区只一次。</summary>
    [Test]
    public void Q772_A01_Forecast_ToolHitsZero_SuffixAndNoteOnce()
    {
        var (s, knife, carcass) = Carcass(1);
        var p = E.Preview(s, ActionRequest.Butcher(carcass.InstanceId));
        Assert.That((p.Ok, p.PlanFailed), Is.EqualTo((true, false)), p.Reason);
        Assert.That((p.ToolDurabilityBefore, p.ToolDurabilityAfter), Is.EqualTo(((double?)1, (double?)0)));
        Assert.That(p.ToolBreaksAfterOrder, Is.True);
        Assert.That(GameEngine.ToolBreakForecastSuffix, Is.EqualTo(BreakSuffix));
        Assert.That(p.Notes.Count(n => n == GuardNote), Is.EqualTo(1), "原保障说明只一次");
        Assert.That(Web("Components", "PreviewView.razor"), Does.Contain("@(p.ToolBreaksAfterOrder ? GameEngine.ToolBreakForecastSuffix : \"\")</b>"), "接在工具行耐久前后值之后");
    }

    /// <summary>B51-Q772-01 配对：完成后仍大于 0 不误报（无后缀、无说明）。</summary>
    [Test]
    public void Q772_A01_Forecast_ToolStaysPositive_NoSuffix()
    {
        var (s, _, carcass) = Carcass(1.5);
        var p = E.Preview(s, ActionRequest.Butcher(carcass.InstanceId));
        Assert.That((p.Ok, p.PlanFailed, p.ToolDurabilityAfter), Is.EqualTo((true, false, (double?)0.5)), p.Reason);
        Assert.That(p.ToolBreaksAfterOrder, Is.False);
        Assert.That(p.Notes, Does.Not.Contain(GuardNote));
    }

    /// <summary>Q875 A（暂用）：完成计划不可信（推演算不完）时，即便当前耐久−本单磨损为 0 也不接后缀、不出说明。</summary>
    [Test]
    public void Q875_A_PlanUnreliable_NoSuffixNoNote()
    {
        var cfg = LoadConfig();
        cfg.AnimalCarcasses.Single(x => x.CarcassItemId == "carcass_feral_dog").ButcherBaseMinutes = 100000;   // 远超预估推演上限，求不出完成
        var e = new GameEngine(cfg);
        var (s, _, carcass) = Carcass(1, e);
        var p = e.Preview(s, ActionRequest.Butcher(carcass.InstanceId));
        Assert.That((p.Ok, p.PlanFailed), Is.EqualTo((true, true)), p.Reason);
        Assert.That(p.ToolBreaksAfterOrder, Is.False);
        Assert.That(p.Notes, Does.Not.Contain(GuardNote));
    }

    /// <summary>
    /// B51-Q772-01：进行中被本单锁定、按本单磨损确定完成后归 0 的同一工具实例写「本次生产结束后损坏」；同名另一把不标；完成后不再挂。
    /// </summary>
    [Test]
    public void Q772_A01_InProgress_SameLockedToolOnly_ClearsAfterFinish()
    {
        var (s, knife, carcass) = Carcass(1);
        var other = E.CreateItems(s, "stone_knife", 1, s.Ground).Single();
        other.Durability = 1;
        Start(s, ActionRequest.Butcher(carcass.InstanceId));
        Assert.That(s.Main!.ToolGuardId, Is.EqualTo(knife.InstanceId));
        Assert.That(CardStates.ToolOrderState(E, s, knife), Is.EqualTo(BreakState));
        Assert.That(CardStates.ToolOrderState(E, s, other), Is.Null, "同名未锁定的另一把不标");
        Run(s);
        Assert.That(s.Main, Is.Null);
        Assert.That(knife.Durability, Is.EqualTo(0), "按原规则完成磨到 0");
        Assert.That(CardStates.ToolOrderState(E, s, knife), Is.Null, "完成后不再挂状态");
        Assert.That(Web("Components", "ItemShelf.razor"), Does.Contain("CardStates.ToolOrderState(e, s, t) ?? Ui.CardLine2(e, s, t)"));
        Assert.That(Web("Components", "ItemDetail.razor"), Does.Contain("CardStates.ToolOrderState(e, s, inst) is string toolOrder) { <div><span>状态</span><b>@toolOrder</b></div> }"));
    }

    /// <summary>B51-Q772-01 配对：进行中工具完成后仍为正（2−1＝1）不标。</summary>
    [Test]
    public void Q772_A01_InProgress_ToolStaysPositive_NotMarked()
    {
        var (s, knife, carcass) = Carcass(2);
        Start(s, ActionRequest.Butcher(carcass.InstanceId));
        Assert.That(CardStates.ToolOrderState(E, s, knife), Is.Null);
    }

    /// <summary>草药 5 份（有期限 600 分钟）随身，制作 n 份绷带只绑定 n 份草药。</summary>
    private GameState Herbs5(int batch)
    {
        var s = New();
        s.Items.RemoveAll(i => i.ItemId is "cloth" or "herb" or "bandage");
        E.DebugGive(s, "cloth", batch);
        foreach (var i in E.CreateItems(s, "herb", 5, ItemInstance.Carried)) i.ExpiresAt = s.GameMinute + 600;
        Start(s, ActionRequest.Craft("bandage", batchCount: batch));
        return s;
    }

    /// <summary>
    /// B51-Q773-01：5 份中 2 份暂停保质——副标只写「含保质暂停」（S06／S14 B51 §Q773 选 B）；详情真实批：2 份保护（R）与 3 份未保护（原到期）分列；总数／预留数保持。
    /// </summary>
    [Test]
    public void Q773_A01_PartlyPaused_SubtitleOnly_DetailSplitsBatches()
    {
        var s = Herbs5(2);
        var e0 = s.GameMinute + 600;
        var herbs = s.Items.Where(i => i.Owner == ItemInstance.Carried && i.ItemId == "herb").ToList();
        Assert.That(CardStates.PausedCardLine(E, s, herbs), Is.EqualTo("含保质暂停"));
        Assert.That(CardStates.TotalAndReserved(herbs), Is.EqualTo((5, 2)), "数量与预留不变");
        var batches = E.BatchesOf(s, ItemInstance.Carried, "herb");
        Assert.That(batches, Has.Count.EqualTo(2));
        var paused = batches.Single(b => b.Stacks[0].ShelfPausedMinutes != null);
        var free = batches.Single(b => b.Stacks[0].ShelfPausedMinutes == null);
        Assert.That((paused.Count, paused.Stacks[0].ShelfPausedMinutes, paused.Reserved), Is.EqualTo((2, (int?)600, true)), "保护 2 份按 R 自成一批");
        Assert.That((free.Count, free.ExpiresAt, free.Reserved), Is.EqualTo((3, (int?)e0, false)), "未保护 3 份原到期");
        Assert.That(Web("Components", "ItemShelf.razor"), Does.Contain("pausedLine != GameEngine.ShelfPartlyPausedText"), "部分暂停不接批数");
    }

    /// <summary>B51-Q773-01 配对：全部保护沿 Q724 原「保质暂停，剩余{时长}」；没有保护不写暂停（沿原到期副标）。</summary>
    [Test]
    public void Q773_A01_AllPaused_OrNonePaused_Unchanged()
    {
        var all = Herbs5(5);
        Assert.That(CardStates.PausedCardLine(E, all, all.Items.Where(i => i.Owner == ItemInstance.Carried && i.ItemId == "herb")), Is.EqualTo("保质暂停，剩余10小时"));
        var none = New();
        none.Items.RemoveAll(i => i.ItemId == "herb");
        foreach (var i in E.CreateItems(none, "herb", 5, ItemInstance.Carried)) i.ExpiresAt = none.GameMinute + 600;
        Assert.That(CardStates.PausedCardLine(E, none, none.Items.Where(i => i.Owner == ItemInstance.Carried && i.ItemId == "herb")), Is.Null);
    }

    /// <summary>
    /// B51-Q774-01（S06／S14 B51 §Q774 选 A）：晾制投料预估只有 Q666 自身说明一次，不叠通用生产说明「开工后，所选原料暂停保质；退料恢复剩余保质。」；
    /// 动词改「取出」随 Q778（B51c2）一起做，本批仍为原句。
    /// </summary>
    [Test]
    public void Q774_A01_DryingFeed_OnlyOwnShelfNote()
    {
        var s = New();
        s.GameMinute = At(2, 8, 0);
        s.Skills["cooking"].Level = 5;
        s.Items.RemoveAll(i => i.Owner == s.Ground || i.Owner == ItemInstance.Carried);
        E.CreateItems(s, "wood", 4, s.Ground);
        E.CreateItems(s, "rope", 2, s.Ground);
        Start(s, ActionRequest.Craft("build_drying_rack"));
        Run(s);
        Assert.That(E.DryingRackHere(s), Is.True);
        foreach (var i in E.CreateItems(s, "raw_meat", 1, ItemInstance.Carried)) i.ExpiresAt = s.GameMinute + 300;
        E.CreateItems(s, "salt", 1, s.Ground);
        var o = E.DryingPreview(s, "dry_meat");
        Assert.That(o.PausesShelf, Is.True);
        const string own = "开始制作后暂停原料保质计时，取消后恢复剩余时长";   // S11 Q666 原句（取出动词随 Q778）
        Assert.That(o.Note.Split(own).Length - 1, Is.EqualTo(1), "Q666 自身说明一次");
        Assert.That(o.Note, Does.Not.Contain("所选原料暂停保质").And.Not.Contain("退料恢复剩余保质"), "不叠通用说明");
        Assert.That(E.StartDrying(s, "dry_meat").Ok, Is.True);
        Assert.That(s.Log.Last().Text, Does.Not.Contain("退料恢复剩余保质").And.Not.Contain("恢复剩余保质，保质到"), "投料日志不加通用恢复句");
    }
}
