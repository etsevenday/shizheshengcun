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

