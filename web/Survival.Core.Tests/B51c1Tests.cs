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
        // BM11 改断言出处：S08 文首 2026-11-17 §Q882 用户新裁定（返料不附恢复保质说明）
        Assert.That(E.ActionProgress(s)!.ExitNote, Is.EqualTo($"退出将取消制作。材料将退回{zone}地面。已耗时间不退。"));
        Assert.That(E.Cancel(s).Ok, Is.True);
        Assert.That(s.Log.Last().Text, Is.EqualTo($"已取消制作。材料已退回{zone}地面。已耗时间不退。"));
        Assert.That(Clock.DayTime(s.Items.Single(i => i.ItemId == "herb").ExpiresAt!.Value), Is.EqualTo(at), "详情仍真实 C＋R");
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
        // BM11 改断言出处：S08 文首 2026-11-17 §Q882（后果词同步“维修”；Q874 A）
        Assert.That(E.ActionProgress(s)!.ExitNote, Is.EqualTo($"退出将取消维修。材料将退回{zone}地面。不会完成本次维修。已耗时间不退。"));
        Assert.That(E.Cancel(s).Ok, Is.True);
        Assert.That(s.Log.Last().Text, Is.EqualTo($"已取消维修。材料已退回{zone}地面。未完成本次维修。已耗时间不退。"));
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
        // BM11 改断言出处：S08 文首 2026-11-17 §Q882 用户新裁定（返料不附恢复保质说明）
        Assert.That(E.ActionProgress(s)!.ExitNote, Is.EqualTo($"退出将放弃本次处理。尸体将返还{zone}地面。不会获得产物。已耗时间不退。"));
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
        const string own = "开始制作后暂停原料保质计时";   // S11 Q666 原句（取出动词随 Q778）；BM11 改断言出处：S08 文首 2026-11-17 §Q882 用户新裁定（返料不附恢复保质说明）：删“，取消后恢复剩余时长”
        Assert.That(o.Note.Split(own).Length - 1, Is.EqualTo(1), "Q666 自身说明一次");
        Assert.That(o.Note, Does.Not.Contain("所选原料暂停保质").And.Not.Contain("退料恢复剩余保质"), "不叠通用说明");
        Assert.That(E.StartDrying(s, "dry_meat").Ok, Is.True);
        Assert.That(s.Log.Last().Text, Does.Not.Contain("退料恢复剩余保质").And.Not.Contain("恢复剩余保质，保质到"), "投料日志不加通用恢复句");
    }

    // ───────────── M3 Q775 未知后值写原因、Q776 可能产物／配方产物 ─────────────

    /// <summary>
    /// Q873 A（暂用）：括号短原因取同源真实原因——续做被拒原因去句末标点；中止日志只取「原因：」后正文（S01 Q518 原句「{行动名}已中止。原因：{原因正文}」）；
    /// 日志没有「原因：」或没有原因为 null（只写「未预估」）。
    /// </summary>
    [Test]
    public void Q873_A_ShortFailReason_FromResumeOrReasonMarkOnly()
    {
        Assert.That(GameEngine.ShortFailReason("需要燃烧的火堆", true), Is.EqualTo("需要燃烧的火堆"));
        Assert.That(GameEngine.ShortFailReason("当前行动还没结束。", true), Is.EqualTo("当前行动还没结束"), "续做原因去句末标点");
        Assert.That(GameEngine.ShortFailReason("制作绷带已中止。原因：所需工具已不在。预留材料已返还当前地面。已耗时间保留。", false), Is.EqualTo("所需工具已不在"), "只取「原因：」后正文到句末");
        Assert.That(GameEngine.ShortFailReason("制作绷带中止：原料已腐败，预留物返还当前地面，未生成产物。", false), Is.Null, "日志没有「原因：」不编原因");
        Assert.That(GameEngine.ShortFailReason(null, false), Is.Null);
        Assert.That(GameEngine.ShortFailReason(null, true), Is.Null);
    }

    /// <summary>B51-Q775-01：依赖完成的未知值「未预估（{实际原因}）」；没有原因只写「未预估」不加括号（Q873 A 暂用）；耗时、完成、工具后值、经验同一写法。</summary>
    [Test]
    public void Q775_A01_UnknownText_ReasonInBracket_OrPlain()
    {
        var p = new ActionPreview { Ok = true, PlanFailed = true, PlanFailShort = "燃料不足", ToolDurabilityBefore = 5, ToolDurabilityAfter = 4 };
        Assert.That(GameEngine.UnknownText(p), Is.EqualTo("未预估（燃料不足）"));
        Assert.That(GameEngine.ToolAfterText(p), Is.EqualTo("未预估（燃料不足）"));
        Assert.That(GameEngine.XpAfterText(p), Is.EqualTo("未预估（燃料不足）"));
        p.PlanFailShort = null;
        Assert.That(GameEngine.UnknownText(p), Is.EqualTo("未预估"));
        Assert.That(GameEngine.ToolAfterText(p), Is.EqualTo("未预估"));
        p.PlanFailed = false;
        Assert.That(GameEngine.ToolAfterText(p), Is.EqualTo("4.00"), "可靠照旧（两位小数）");
        var pv = Web("Components", "PreviewView.razor");
        Assert.That(pv, Does.Contain("<div><span>预计耗时</span><b>@(p.PlanFailed ? GameEngine.UnknownText(p) : Clock.Duration(p.EstimatedMinutes))</b></div>"));
        Assert.That(pv, Does.Contain("<div><span>预计完成</span><b>@(p.PlanFailed ? GameEngine.UnknownText(p) : Clock.HHmm(p.EstimatedEndMinute))</b></div>"));
        Assert.That(pv, Does.Not.Contain("p.PlanFailed ? GameEngine.NotEstimated"), "依赖完成的值不再写不带原因的裸「未预估」（Q683 致死判定健康仍裸写，见 BodyUnknown 测试）");
        Assert.That(Web("Components", "CraftOverlay.razor"), Does.Contain("<b>@GameEngine.UnknownText(p)</b>"));
    }

    /// <summary>
    /// B51-Q775-01：同类余量保留当前可信值——可靠「{当前} → {完成后}分钟」；完成后值不可靠「{当前}分钟 → 未预估（{实际原因}）」（S08 B51 §Q775 例「2.5分钟 → 未预估（燃料不足）」）；
    /// 没有完成后值且完成可靠时只写当前。
    /// </summary>
    [Test]
    public void Q775_A01_CarryRow_KeepsCurrent_AfterUnknownWithReason()
    {
        var p = new ActionPreview { Ok = true, PlanFailed = true, PlanFailShort = "燃料不足", CarryBefore = 2.5 };
        Assert.That(GameEngine.CarryRowText(p), Is.EqualTo("2.50分钟 → 未预估（燃料不足）"), "数值沿全局两位小数（Fmt.N，S14 数值格式），策划例 2.5 即 2.50");
        p.PlanFailShort = null;
        Assert.That(GameEngine.CarryRowText(p), Is.EqualTo("2.50分钟 → 未预估"));
        var ok = new ActionPreview { Ok = true, PlanFailed = false, CarryBefore = 2.5, CarryAfter = 1 };
        Assert.That(GameEngine.CarryRowText(ok), Is.EqualTo("2.50 → 1.00分钟"), "可靠照原写法");
        ok.CarryAfter = null;
        Assert.That(GameEngine.CarryRowText(ok), Is.EqualTo("2.50分钟"));
        Assert.That(Web("Components", "PreviewView.razor"), Does.Contain("<span>同类余量</span><b>@GameEngine.CarryRowText(p)</b>"));
    }

    /// <summary>
    /// B51-Q775-01（端到端，Q873 A）：处理尸体推到预估上限仍未完成、没有中止事件也没有续做原因——不编原因，所有依赖完成的值只写「未预估」；
    /// 配对：可靠完成时 PlanFailShort 为空、数值照旧。
    /// </summary>
    [Test]
    public void Q775_A01_EndToEnd_NoRealReason_PlainUnknown()
    {
        var cfg = LoadConfig();
        cfg.AnimalCarcasses.Single(x => x.CarcassItemId == "carcass_feral_dog").ButcherBaseMinutes = 100000;
        var e = new GameEngine(cfg);
        var (s, _, carcass) = Carcass(5, e);
        var p = e.Preview(s, ActionRequest.Butcher(carcass.InstanceId));
        Assert.That((p.Ok, p.PlanFailed, p.PlanFailReason, p.PlanFailShort), Is.EqualTo((true, true, (string?)null, (string?)null)), p.Reason);
        Assert.That(GameEngine.UnknownText(p), Is.EqualTo("未预估"));
        Assert.That(GameEngine.ToolAfterText(p), Is.EqualTo("未预估"));
        var (s2, _, carcass2) = Carcass(5);
        var ok = E.Preview(s2, ActionRequest.Butcher(carcass2.InstanceId));
        Assert.That((ok.Ok, ok.PlanFailed, ok.PlanFailShort), Is.EqualTo((true, false, (string?)null)), ok.Reason);
        Assert.That(GameEngine.ToolAfterText(ok), Is.EqualTo("4.00"), "5−野犬成功磨损1");
    }

    /// <summary>B51-Q775-01：身体后值完成不可信（Q792）带同源原因；只取决于致死判定的健康未知（Q683）仍只写「未预估」。</summary>
    [Test]
    public void Q775_A01_BodyUnknown_SameSourceReason_Q683Plain()
    {
        var p = new ActionPreview { Ok = true, PlanFailed = true, PlanFailShort = "燃料不足" };
        Assert.That(GameEngine.BodyAfterUnknown(p), Is.True);
        Assert.That(GameEngine.BodyUnknownText(p), Is.EqualTo("未预估（燃料不足）"));
        var q683 = new ActionPreview { Ok = true, PlanFailed = false, PlanFailShort = null };
        Assert.That(GameEngine.BodyAfterUnknown(q683), Is.False);
        Assert.That(GameEngine.BodyUnknownText(q683), Is.EqualTo("未预估"));
        Assert.That(Web("Components", "PreviewView.razor"), Does.Contain("var after = unknown ? GameEngine.BodyUnknownText(p) : shown;"));
    }

    /// <summary>
    /// B51-Q776-01：采集完成不可靠时状态「可能产物」、数量「未预估（{实际原因}）」（S08 B51 §Q776）；可靠照旧「产出 · 留在{地点}」「×{数量}」；
    /// 固定配方不可靠仍「配方产物 · 留在{地点}」、数量配方×份数（Q725）。
    /// </summary>
    [Test]
    public void Q776_A01_GatherUnreliable_PossibleOutput_QtyUnknown()
    {
        var p = new ActionPreview { Ok = true, PlanFailed = true, PlanFailShort = "燃料不足", OutputPlace = "林地地面" };
        Assert.That(GameEngine.OutputStatusText(p, ActionType.Gather), Is.EqualTo("可能产物"));
        Assert.That(GameEngine.OutputQtyText(p, ActionType.Gather, 3), Is.EqualTo("未预估（燃料不足）"));
        Assert.That(GameEngine.OutputStatusText(p, ActionType.Craft), Is.EqualTo("配方产物 · 留在林地地面"));
        Assert.That(GameEngine.OutputQtyText(p, ActionType.Craft, 3), Is.EqualTo("×3"));
        p.PlanFailed = false;
        Assert.That(GameEngine.OutputStatusText(p, ActionType.Gather), Is.EqualTo("产出 · 留在林地地面"));
        Assert.That(GameEngine.OutputQtyText(p, ActionType.Gather, 3), Is.EqualTo("×3"));
        Assert.That(Web("Components", "PreviewView.razor"), Does.Contain("Qty=\"@GameEngine.OutputQtyText(p, Request.Type, o.Count)\""));
        Assert.That(Web("Components", "ActionPops.razor"), Does.Contain("@GameEngine.OutputQtyText(p, ActionType.Gather, o.Count)"));
    }

    /// <summary>
    /// B51-Q776-01：搜索／清理候选清单标题「可能产物」，原括注另起一行「完成时只抽1次，物品留在当前地面。」（S08／S14 B51 §Q776）；旧「可能物资（…）」不再出现。
    /// </summary>
    [Test]
    public void Q776_A01_LootOddsTitle_TwoLines()
    {
        Assert.That(GameEngine.PossibleOutputLabel, Is.EqualTo("可能产物"));
        Assert.That(GameEngine.PossibleOutputDrawNote, Is.EqualTo("完成时只抽1次，物品留在当前地面。"));
        var pv = Web("Components", "PreviewView.razor").Replace("\r\n", "\n");
        Assert.That(pv, Does.Contain("<div class=\"tiny muted\">@GameEngine.PossibleOutputLabel</div>\n            <div class=\"tiny muted\">@GameEngine.PossibleOutputDrawNote</div>"));
        Assert.That(pv, Does.Not.Contain("可能物资（"));
    }

    /// <summary>
    /// B51-Q776-01：不能开工的制作只列已知静态「配方产物：{产物清单}」（S08 B51 §Q776 例「配方产物：绷带×1」），不写「产出」；
    /// 配对：能开工照原「产出 · 留在{地点}」。
    /// </summary>
    [Test]
    public void Q776_A01_CannotStartCraft_StaticRecipeLine()
    {
        var s = New();
        s.Items.RemoveAll(i => i.ItemId is "cloth" or "herb" or "bandage");
        var no = E.Preview(s, ActionRequest.Craft("bandage"));
        Assert.That(no.Ok, Is.False);
        Assert.That(no.Outputs, Has.Count.EqualTo(1));
        Assert.That(GameEngine.StaticRecipeOutputLine(no.Outputs.Select(o => $"{o.Name}×{o.Count}")), Is.EqualTo("配方产物：草药绷带×1"), "策划例「绷带×1」，配置 bandage 名「草药绷带」");
        E.DebugGive(s, "cloth", 1);
        E.DebugGive(s, "herb", 1);
        var yes = E.Preview(s, ActionRequest.Craft("bandage"));
        Assert.That((yes.Ok, yes.PlanFailed), Is.EqualTo((true, false)), yes.Reason);
        Assert.That(GameEngine.OutputStatusText(yes, ActionType.Craft), Does.StartWith("产出 · 留在"));
        var pv = Web("Components", "PreviewView.razor");
        Assert.That(pv, Does.Contain("else if (!p.Ok && p.Outputs.Count > 0 && GameEngine.IsFixedRecipeOutput(Request.Type))"));
        Assert.That(pv, Does.Contain("@GameEngine.StaticRecipeOutputLine(p.Outputs.Select(o => $\"{o.Name}×{o.Count}\"))"));
    }
}
