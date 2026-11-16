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
}
