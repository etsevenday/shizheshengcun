using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 批次 B54「返料去向、保质短句与身体未知后值」（开发记录 B54）：S08 文首「2026-11-15 B51a Q787—Q790」「B51b Q791—Q792」，
/// 逐字稿同两节，辅助合同 q787ToQ790、q791ToQ792。本类为撤退离地保留按来源返还（Q787 五根卡溢出、Q789 多去向恢复分句）。
/// </summary>
public class B54KeptTests : B41bTestBase
{
    private string Herb => E.ItemName("herb");
    private string Cloth => E.ItemName("cloth");
    private string FieldName => E.ZoneName(V2Field(), "field");

    /// <summary>清空随身与草药／布，布×cloth 放在荒地地面，随身放草药×herbs（截止 600 分钟后）与给定的其他物品各 1 份。</summary>
    private Func<GameState, ActionRequest> Setup(int herbs, int batch, params string[] carriedOthers) => g =>
    {
        g.Items.RemoveAll(i => i.Owner == ItemInstance.Carried || i.ItemId is "cloth" or "herb" or "bandage");
        E.DebugGive(g, "cloth", batch, "field");
        foreach (var i in E.CreateItems(g, "herb", herbs, ItemInstance.Carried)) i.ExpiresAt = g.GameMinute + 600;
        foreach (var x in carriedOthers) E.DebugGive(g, x, 1, ItemInstance.Carried);
        return ActionRequest.Craft("bandage", batchCount: batch);
    };

    /// <summary>
    /// Q787-A01：随身五卡已满，草药×2 恢复 C＋R 后不能并回原堆，留原工作地点（荒地）地面而不是撤退落点（营地）；
    /// 随身不新增第六张根卡，物数与新截止守恒；返还段按实际去向写 actual／overflow 句。
    /// </summary>
    [Test]
    public void Q787_A01_FullCarry_OverflowToWorkGround_NotRetreatGround()
    {
        var s = Field(3);
        var id = KeptAtField(s, Setup(3, 2, "stone", "wood", "fiber", "scrap"));
        Assert.That(E.RootCardCount(s), Is.EqualTo(5));
        var bound = Reserved(s, "herb", id);
        var r = bound.ShelfPausedMinutes!.Value;
        var e0 = s.Items.Single(i => i.ItemId == "herb" && i.ReservedBy == null).ExpiresAt;
        var at = Clock.DayTime(s.GameMinute + r);
        // 只读预告：同源容量与原工作地，不挪物
        var before = SaveSerializer.Serialize(s);
        Assert.That(E.ActionProgress(s)!.ExitNote, Is.EqualTo(
            $"退出将放弃本次制作。{Cloth}×2将退回{FieldName}地面。随身放不下，{Herb}×2将留在{FieldName}地面。{Herb}×2退回后恢复剩余保质，预计保质到{at}。不会生成半成品。已耗时间不退。"));
        Assert.That(SaveSerializer.Serialize(s), Is.EqualTo(before), "预告只读");
        Assert.That(E.Cancel(s).Ok, Is.True);
        Assert.That(s.Log.Last().Text, Is.EqualTo(
            $"已取消制作。{Cloth}×2已退回{FieldName}地面。随身已满，{Herb}×2留在{FieldName}地面。{Herb}×2恢复剩余保质，保质到{at}。未生成半成品。已耗时间保留。"));
        Assert.That(E.RootCardCount(s), Is.EqualTo(5), "不新增第六张根卡");
        var dropped = s.Items.Single(i => i.ItemId == "herb" && i.Owner == "field");
        Assert.That((dropped.Count, dropped.ExpiresAt, dropped.ShelfPausedMinutes, dropped.ReservedBy), Is.EqualTo((2, (int?)(s.GameMinute + r), (int?)null, (int?)null)));
        var kept = s.Items.Single(i => i.ItemId == "herb" && i.Owner == ItemInstance.Carried);
        Assert.That((kept.Count, kept.ExpiresAt), Is.EqualTo((1, e0)), "原随身余料不动");
        Assert.That(s.Items.Where(i => i.ItemId == "herb" && i.Owner == "camp").Sum(i => i.Count), Is.EqualTo(0), "不落撤退落点");
        Assert.That(s.Items.Where(i => i.Owner == ItemInstance.Carried).Select(i => i.ItemId).OrderBy(x => x), Is.EqualTo(new[] { "fiber", "herb", "scrap", "stone", "wood" }), "不丢其他随身物");
        Assert.That(E.Cancel(s).Ok, Is.False, "同一工作不双退");
    }

    /// <summary>Q787-A02：返还移除自身旧占位后正好五卡的份额仍回随身；没有溢出沿原 Q471 返还句。</summary>
    [Test]
    public void Q787_A02_ExactlyFiveStillCarried_OriginalSentence()
    {
        var s = Field(3);
        var id = KeptAtField(s, Setup(3, 2, "stone", "wood", "fiber"));
        Assert.That(E.RootCardCount(s), Is.EqualTo(4));
        var r = Reserved(s, "herb", id).ShelfPausedMinutes!.Value;
        Assert.That(E.ActionProgress(s)!.ExitNote, Does.StartWith($"退出将放弃本次制作。随身预留材料解除预留后留在随身，地面预留材料会返还{FieldName}地面。"));
        Assert.That(E.Cancel(s).Ok, Is.True);
        Assert.That(s.Log.Last().Text, Is.EqualTo(
            $"已取消制作。随身预留材料已解除预留，仍在随身；地面预留材料已返还{FieldName}地面。{Herb}×2恢复剩余保质，保质到{Clock.DayTime(s.GameMinute + r)}。未生成半成品。已耗时间保留。"));
        Assert.That(E.RootCardCount(s), Is.EqualTo(5));
        Assert.That(s.Items.Where(i => i.ItemId == "herb" && i.Owner == ItemInstance.Carried).Sum(i => i.Count), Is.EqualTo(3));
    }

    /// <summary>Q787-A02：多返料沿原预留清单顺序尽量回随身（布先回），超出的草药才落原工作地面。</summary>
    [Test]
    public void Q787_A02_ReservedOrder_EarlierFitsLaterOverflows()
    {
        var s = Field(3);
        var id = KeptAtField(s, g =>
        {
            g.Items.RemoveAll(i => i.Owner == ItemInstance.Carried || i.ItemId is "cloth" or "herb" or "bandage");
            E.DebugGive(g, "cloth", 2, ItemInstance.Carried);
            foreach (var i in E.CreateItems(g, "herb", 3, ItemInstance.Carried)) i.ExpiresAt = g.GameMinute + 600;
            foreach (var x in new[] { "stone", "wood", "fiber" }) E.DebugGive(g, x, 1, ItemInstance.Carried);
            return ActionRequest.Craft("bandage", batchCount: 2);
        });
        Assert.That(E.RootCardCount(s), Is.EqualTo(5));
        var r = Reserved(s, "herb", id).ShelfPausedMinutes!.Value;
        Assert.That(E.Cancel(s).Ok, Is.True);
        Assert.That(s.Log.Last().Text, Does.StartWith($"已取消制作。{Cloth}×2已退回随身。随身已满，{Herb}×2留在{FieldName}地面。"));
        Assert.That(E.RootCardCount(s), Is.EqualTo(5));
        Assert.That(s.Items.Single(i => i.ItemId == "cloth").Owner, Is.EqualTo(ItemInstance.Carried));
        Assert.That(s.Items.Single(i => i.ItemId == "herb" && i.Owner == "field").ExpiresAt, Is.EqualTo(s.GameMinute + r));
    }

    /// <summary>
    /// 随身已有同截止 C＋R 的草药堆（18 份，堆上限 20）时，按原堆上限只并回 2 份，余 1 份落原工作地面（最大合法份额，不整组扔下）。
    /// 返回 (状态, 恢复后截止)。
    /// </summary>
    private (GameState s, int at) PartialFit()
    {
        var s = Field(3);
        ItemInstance? holder = null;
        var id = KeptAtField(s, g =>
        {
            g.Items.RemoveAll(i => i.Owner == ItemInstance.Carried || i.ItemId is "cloth" or "herb" or "bandage");
            E.DebugGive(g, "cloth", 3, "field");
            foreach (var i in E.CreateItems(g, "herb", 4, ItemInstance.Carried)) i.ExpiresAt = g.GameMinute + 600;
            holder = E.CreateItems(g, "herb", 18, ItemInstance.Carried).Single();
            holder.ExpiresAt = g.GameMinute + 5000;
            foreach (var x in new[] { "stone", "wood", "fiber" }) E.DebugGive(g, x, 1, ItemInstance.Carried);
            return ActionRequest.Craft("bandage", batchCount: 3);
        });
        var bound = Reserved(s, "herb", id);
        Assert.That(bound.Count, Is.EqualTo(3));
        var at = s.GameMinute + bound.ShelfPausedMinutes!.Value;
        s.Items.Single(i => i.InstanceId == holder!.InstanceId).ExpiresAt = at;   // 随身另一堆恰与恢复后截止同键
        Assert.That(E.RootCardCount(s), Is.EqualTo(5));
        return (s, at);
    }

    [Test]
    public void Q787_A02_StackCapMaximumLegalPortion()
    {
        var (s, at) = PartialFit();
        Assert.That(E.ActionProgress(s)!.ExitNote, Does.StartWith($"退出将放弃本次制作。{Cloth}×3将退回{FieldName}地面。{Herb}×2将退回随身。随身放不下，{Herb}×1将留在{FieldName}地面。"));
        Assert.That(E.Cancel(s).Ok, Is.True);
        Assert.That(s.Log.Last().Text, Does.StartWith($"已取消制作。{Cloth}×3已退回{FieldName}地面。{Herb}×2已退回随身。随身已满，{Herb}×1留在{FieldName}地面。"));
        Assert.That(E.RootCardCount(s), Is.EqualTo(5));
        Assert.That(s.Items.Where(i => i.ItemId == "herb" && i.Owner == ItemInstance.Carried && i.ExpiresAt == at).Sum(i => i.Count), Is.EqualTo(20));
        Assert.That(s.Items.Single(i => i.ItemId == "herb" && i.Owner == "field").Count, Is.EqualTo(1));
        Assert.That(s.Items.Where(i => i.ItemId == "herb").Sum(i => i.Count), Is.EqualTo(22), "物数守恒");
    }

    /// <summary>Q787-A03：预告不挪物，提交按当时事实重算（预告后随身多一张卡，提交即判溢出）。</summary>
    [Test]
    public void Q787_A03_NoStateChangeUntilRelease_PlanRecomputedAtCommit()
    {
        var s = Field(3);
        KeptAtField(s, Setup(3, 2, "stone", "wood", "fiber"));
        // 预告时四卡、能回随身；提交前随身又多一张卡，提交按当时事实重算为溢出
        Assert.That(E.ActionProgress(s)!.ExitNote, Does.Not.Contain("随身放不下"));
        E.DebugGive(s, "scrap", 1, ItemInstance.Carried);
        Assert.That(E.ActionProgress(s)!.ExitNote, Does.Contain($"随身放不下，{Herb}×2将留在{FieldName}地面。"));
        Assert.That(E.Cancel(s).Ok, Is.True);
        Assert.That(s.Log.Last().Text, Does.Contain($"随身已满，{Herb}×2留在{FieldName}地面。"));
        Assert.That(E.RootCardCount(s), Is.EqualTo(5));
    }
    // ───────────── Q789 多去向恢复分句标真实去向 ─────────────

    /// <summary>Q789-A01：随身与原工作地面同名同截止草药各一句，前缀真实去向；不跨目的地合计。预告带“预计”，结果按提交时 C。</summary>
    [Test]
    public void Q789_A01_SameNameSameExpiry_TwoDestinations_Labeled()
    {
        var s = Field(3);
        var id = KeptAtField(s, g =>
        {
            g.Items.RemoveAll(i => i.ItemId is "cloth" or "herb" or "bandage");
            E.DebugGive(g, "cloth", 2);
            var e1 = g.GameMinute + 600;
            foreach (var i in E.CreateItems(g, "herb", 1, ItemInstance.Carried)) i.ExpiresAt = e1;
            foreach (var i in E.CreateItems(g, "herb", 1, "field")) i.ExpiresAt = e1;
            return ActionRequest.Craft("bandage", batchCount: 2);
        });
        var r = s.Items.Where(i => i.ItemId == "herb" && i.ReservedBy == id).Select(i => i.ShelfPausedMinutes!.Value).Distinct().Single();
        var at = Clock.DayTime(s.GameMinute + r);
        Assert.That(E.ActionProgress(s)!.ExitNote, Does.Contain($"随身：{Herb}×1，预计保质至{at}。{FieldName}地面：{Herb}×1，预计保质至{at}。不会生成半成品。"));

        Assert.That(E.Cancel(s).Ok, Is.True);
        var text = s.Log.Last().Text;
        Assert.That(text, Does.Contain($"随身：{Herb}×1，保质至{at}。{FieldName}地面：{Herb}×1，保质至{at}。未生成半成品。已耗时间保留。"));
        Assert.That(text, Does.Not.Contain("恢复剩余保质"), "多去向不再用原单去向句");
    }

    /// <summary>Q789-A01：Q787 溢出份标原工作地面，不按开工来源误标随身；同目的地同截止合计。</summary>
    [Test]
    public void Q789_A01_OverflowPortionLabeledWorkGround()
    {
        var (s, at) = PartialFit();
        var t = Clock.DayTime(at);
        Assert.That(E.ActionProgress(s)!.ExitNote, Does.Contain($"随身：{Herb}×2，预计保质至{t}。{FieldName}地面：{Herb}×1，预计保质至{t}。"));
        Assert.That(E.Cancel(s).Ok, Is.True);
        Assert.That(s.Log.Last().Text, Is.EqualTo(
            $"已取消制作。{Cloth}×3已退回{FieldName}地面。{Herb}×2已退回随身。随身已满，{Herb}×1留在{FieldName}地面。随身：{Herb}×2，保质至{t}。{FieldName}地面：{Herb}×1，保质至{t}。未生成半成品。已耗时间保留。"));
    }

    /// <summary>Q789-A02：只有一个实际恢复目的地时沿原 Q723 单去向句（溢出全部落地也是单目的地）。</summary>
    [Test]
    public void Q789_A02_SingleDestinationKeepsOriginalSentence()
    {
        var s = Field(3);
        var id = KeptAtField(s, Setup(3, 2, "stone", "wood", "fiber", "scrap"));
        var r = Reserved(s, "herb", id).ShelfPausedMinutes!.Value;
        Assert.That(E.Cancel(s).Ok, Is.True);
        var text = s.Log.Last().Text;
        Assert.That(text, Does.Contain($"{Herb}×2恢复剩余保质，保质到{Clock.DayTime(s.GameMinute + r)}。"));
        Assert.That(text, Does.Not.Contain("地面：").And.Not.Contain("随身："));
    }
}


/// <summary>B54 失败返料短恢复句（Q788 建造完工地点失效、Q791 陶窑／净水台缺料失败）、操作窗总数（Q790）与身体未知后值（Q792）。</summary>
public class B54Tests : TestBase
{
    private const string Water = "ex_station_water";

    protected override bool NoEnemyApproach => true;

    private void Ok(ActionResult r) => Assert.That(r.Ok, Is.True, r.Reason);

    private static string Web(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "Survival.Web" }.Concat(parts).ToArray()));

    // ───────────── Q788 建造完工地点失效 ─────────────

    /// <summary>荒地外部区开建野外火堆（石块注入有限截止）；开工后本区已有火堆，完工复核失败。返回 (状态, 石块 R)。</summary>
    private (GameState s, int? r) WildFireSiteLost(int? stoneLeft)
    {
        var s = New();
        E.DebugTeleport(s, "field");
        s.WorldEnemies.RemoveAll(w => w.LocationId == "field");
        s.Items.RemoveAll(i => i.ItemId is "stone" or "wood");
        foreach (var i in E.CreateItems(s, "stone", 4, s.Ground)) i.ExpiresAt = stoneLeft is int m ? s.GameMinute + m : null;
        E.CreateItems(s, "wood", 2, s.Ground);
        Start(s, ActionRequest.Craft("wild_fixed_fire", null, null, 1));
        var r = s.Items.First(i => i.ItemId == "stone" && i.ReservedBy == s.Main!.ActionId).ShelfPausedMinutes;
        s.Locations["field"].Facility.FireBuilt = true;   // 开工后本区已有火源（每区一处）
        Run(s);
        return (s, r);
    }

    /// <summary>Q788-A01：仍原失败、当前地面返料、无建筑无经验；有可信 R 时同一结果接「{原料名}×{数量}保质至{日期时刻}。」（实际 C＋R）。</summary>
    [Test]
    public void Q788_A01_BuildSiteFailure_RestoreShortClause()
    {
        var (s, r) = WildFireSiteLost(100);
        Assert.That(r, Is.EqualTo(100));
        var rc = Cfg.Recipes.Single(x => x.Id == "wild_fixed_fire");
        var stone = s.Items.Single(i => i.ItemId == "stone");
        Assert.That((stone.Owner, stone.Count, stone.ShelfPausedMinutes, stone.ReservedBy), Is.EqualTo(("field", 4, (int?)null, (int?)null)));
        var log = s.Log.Last(l => l.Kind == nameof(GameEventKind.ActionAborted));
        var why = E.WildFireSiteReasons(s).Single();
        Assert.That(log.Text, Is.EqualTo($"{rc.Name}未完成：{why}。材料已退回{E.ZoneName(s, "field")}地面。{E.ItemName("stone")}×4保质至{Clock.DayTime(stone.ExpiresAt!.Value)}。无经验。"));
        Assert.That(stone.ExpiresAt!.Value - 100, Is.InRange(log.Minute - 5, log.Minute), "截止＝实际释放 C＋R");
        Assert.That(s.Log.Count(l => l.Kind == nameof(GameEventKind.ActionAborted)), Is.EqualTo(1), "仍一条日志");
    }

    /// <summary>Q788-A01：无期限原料无 R 时省略恢复分句，不留占位或重复句号。</summary>
    [Test]
    public void Q788_A01_NoShelfLife_NoRestoreClause()
    {
        var (s, r) = WildFireSiteLost(null);
        Assert.That(r, Is.Null);
        var rc = Cfg.Recipes.Single(x => x.Id == "wild_fixed_fire");
        var why = E.WildFireSiteReasons(s).Single();
        Assert.That(s.Log.Last(l => l.Kind == nameof(GameEventKind.ActionAborted)).Text,
            Is.EqualTo($"{rc.Name}未完成：{why}。材料已退回{E.ZoneName(s, "field")}地面。无经验。"));
    }

    // ───────────── Q791 陶窑／净水台缺料失败 ─────────────

    private GameState Workshop()
    {
        var s = New();
        s.Skills["crafting"].Level = 3;
        s.Progress.Flags.Add("h03_ceramic_notice_read");
        foreach (var (id, n) in new[] { ("stone", 8), ("clay", 6), ("iron_plate", 2) }) E.CreateItems(s, id, n, s.Ground);
        Start(s, ActionRequest.Craft("build_kiln", null, null, 1));
        Run(s);
        foreach (var (id, n) in new[] { ("ceramic_shell", 4), ("plank", 2), ("cloth", 2) }) E.CreateItems(s, id, n, s.Ground);
        Start(s, ActionRequest.Craft("build_water_station", null, null, 1));
        Run(s);
        s.Items.RemoveAll(i => i.ItemId is "clay" or "charcoal" or "dirty_water" or "filter_media" or "ceramic_shell" or "clean_water");
        return s;
    }

    /// <summary>净水台一批投料（脏水可注入有限截止）后拿走本批滤料，到时缺料失败。返回失败日志。</summary>
    private (GameState s, LogEntry log) WaterMissing(int? dirtyLeft)
    {
        var s = Workshop();
        foreach (var i in E.CreateItems(s, "dirty_water", 4, s.Ground)) i.ExpiresAt = dirtyLeft is int m ? s.GameMinute + m : null;
        E.CreateItems(s, "filter_media", 1, s.Ground);
        var t0 = s.GameMinute;
        Ok(E.StartStationBatch(s, Water, "q791"));
        s.Items.RemoveAll(i => i.ItemId == "filter_media");   // 本批原料不全
        AdvanceTo(s, t0 + 35);
        return (s, s.Log.Last(l => l.Text.Contains("原料不全")));
    }

    /// <summary>Q791-A01：原失败句后按本次实际释放的可信 R 追加 C＋R 实际日期时刻；同物同截止合计；同一条日志。</summary>
    [Test]
    public void Q791_A01_StationMissingFailure_RestoreShortClause()
    {
        var (s, log) = WaterMissing(20);
        var dirty = s.Items.Where(i => i.ItemId == "dirty_water").ToList();
        Assert.That(dirty.Sum(i => i.Count), Is.EqualTo(4));
        var at = dirty.Select(i => i.ExpiresAt!.Value).Distinct().Single();
        Assert.That(dirty.All(i => i.ShelfPausedMinutes == null && i.ReservedBy == null), Is.True);
        Assert.That(log.Text, Does.EndWith($"：原料不全，这一批没有产物，剩下的原料已解锁。{E.ItemName("dirty_water")}×4保质至{Clock.DayTime(at)}。"));
        Assert.That(at - 20, Is.EqualTo(log.Minute), "截止＝实际释放 C＋R");
        Assert.That(s.Log.Count(l => l.Text.Contains("原料不全")), Is.EqualTo(1));
        Assert.That(s.Items.Any(i => i.ItemId == "clean_water"), Is.False, "无产物");
    }

    /// <summary>Q791-A02：现有无期限原料只原失败句，不造期限。</summary>
    [Test]
    public void Q791_A02_NoShelfLife_OnlyOriginalSentence()
    {
        var (s, log) = WaterMissing(null);
        Assert.That(log.Text, Does.EndWith("：原料不全，这一批没有产物，剩下的原料已解锁。"));
        Assert.That(s.Items.Where(i => i.ItemId == "dirty_water").All(i => i.ExpiresAt == null), Is.True, "不为现有原料新增期限");
    }
}

/// <summary>B54 Q790：物品操作窗标题副行显示实际总数，真实预留单独标出；操作资格仍按实际可用。</summary>
public class B54CardActionTests : TestBase
{
    protected override bool NoEnemyApproach => true;

    private static string Web(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "Survival.Web" }.Concat(parts).ToArray()));

    /// <summary>Q790-A01：全预留 ×2（预留2），部分预留 ×5（预留2），无预留只 ×5、不写（预留0）。</summary>
    [Test]
    public void Q790_A01_TitleTexts()
    {
        Assert.That(CardStates.CardActionTitleText("临时营地地面", 2, 2), Is.EqualTo("临时营地地面 · ×2（预留2）"));
        Assert.That(CardStates.CardActionTitleText("随身", 5, 2), Is.EqualTo("随身 · ×5（预留2）"));
        Assert.That(CardStates.CardActionTitleText("随身", 5, 0), Is.EqualTo("随身 · ×5"));
    }

    /// <summary>
    /// Q790-A01／A02：真实制作预留后草药组总数含预留、预留数只数本行动预留；实际可用（GroupStacks）仍不含预留，全部预留时可用 0。
    /// 部署占用不冒充预留；只读统计不改状态。
    /// </summary>
    [Test]
    public void Q790_A02_TotalIncludesReserved_AvailableUnchanged_DeployNotReserved()
    {
        var s = New();
        s.Items.RemoveAll(i => i.ItemId is "cloth" or "herb" or "bandage");
        E.DebugGive(s, "cloth", 2);
        E.DebugGive(s, "herb", 2);
        Start(s, ActionRequest.Craft("bandage", batchCount: 2));
        var herbs = s.Items.Where(i => i.Owner == ItemInstance.Carried && i.ItemId == "herb" && E.CardSideOf(i) == null).ToList();
        var before = SaveSerializer.Serialize(s);
        Assert.That(CardStates.TotalAndReserved(herbs), Is.EqualTo((2, 2)), "全部预留：×2（预留2）");
        Assert.That(E.GroupStacks(s, ItemInstance.Carried, "herb", null).Sum(i => i.Count), Is.EqualTo(0), "可用仍为 0，不可操作");
        Assert.That(SaveSerializer.Serialize(s), Is.EqualTo(before), "只读");
        E.Cancel(s);
        s.Items.RemoveAll(i => i.ItemId == "herb");   // 普通取消把草药退回当前地面；重新给随身草药×5
        E.DebugGive(s, "herb", 5);
        Start(s, ActionRequest.Craft("bandage", batchCount: 2));
        herbs = s.Items.Where(i => i.Owner == ItemInstance.Carried && i.ItemId == "herb").ToList();
        Assert.That(CardStates.TotalAndReserved(herbs), Is.EqualTo((5, 2)), "部分预留：×5（预留2）");
        Assert.That(E.GroupStacks(s, ItemInstance.Carried, "herb", null).Sum(i => i.Count), Is.EqualTo(3), "数量上限仍为实际可用 3");
        // 部署等锁不算预留：部署中的物品 ReservedBy 为空
        var deployed = new ItemInstance { InstanceId = 999999, ItemId = "tarp", Count = 1, Owner = s.Ground };
        Assert.That(CardStates.TotalAndReserved(new[] { deployed }), Is.EqualTo((1, 0)));
    }

    /// <summary>网页：操作窗副行改用 CardActionTitleText（总数、预留数），数量框校验与按钮仍按 m.Available；组总数只取所点卡侧。</summary>
    [Test]
    public void Q790_WebTitleUsesTotal_LimitsUseAvailable()
    {
        var src = Web("Components", "CardActions.razor");
        Assert.That(src, Does.Contain("CardStates.CardActionTitleText(m.From, m.Total, m.Reserved)"));
        Assert.That(src, Does.Not.Contain("@m.From · ×@m.Available"));
        Assert.That(src, Does.Contain("var invalid = n < 1 || n > m.Available;"), "数量校验仍按实际可用");
        Assert.That(src, Does.Contain("CardStates.TotalAndReserved(all.Where(i => e.CardSideOf(i) == side))"), "不跨卡侧");
    }
}
