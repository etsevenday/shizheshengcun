using Newtonsoft.Json.Linq;
using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 批次 B22c（需求 R169）：策划答复 Q170 八位晾架。规则出处：S11/A07「Q163—Q170 生存物资与任务接续」八位晾架完整定稿、
/// assets/20261103/survival-adjudications.json drying、Q207（晾制计入烹饪每日 30）。验收 PLAN-Q170-01/02。
/// </summary>
public class B22cTests : TestBase
{
    /// <summary>与敌踪无关的旧系统用例：冻结到期敌踪主动开战（见 TestBase.NoEnemyApproach）。</summary>
    protected override bool NoEnemyApproach => true;

    private void Ok(ActionResult r) => Assert.That(r.Ok, Is.True, r.Reason);

    private static ActionRequest Craft(string id) => ActionRequest.Craft(id, null, null, 1);

    /// <summary>营地、第 2 日 08:00、烹饪 5 级、清掉随身与地面杂物（只验晾架）。</summary>
    private GameState Cook(int cooking = 5)
    {
        var s = New();
        s.GameMinute = At(2, 8, 0);
        s.Skills["cooking"].Level = cooking;
        s.Items.RemoveAll(i => i.Owner == s.Ground || i.Owner == ItemInstance.Carried);
        return s;
    }

    private void BuildRack(GameState s)
    {
        E.CreateItems(s, "wood", 4, s.Ground);
        E.CreateItems(s, "rope", 2, s.Ground);
        Start(s, Craft("build_drying_rack"));
        Run(s);
        Assert.That(E.DryingRackHere(s), Is.True);
    }

    /// <summary>今日烹饪食品/主动烹饪账（Q207 每日 30 的同一账；晾制计在这里）。</summary>
    private int CookXp(GameState s) => E.DailyCount(s, s.Player, GameEngine.DailyXp, "cooking");

    [Test]
    public void Build_Wood4Rope2_60Min_Crafting1_OnePerZone_CampOrSafehouseOnly()
    {
        var rc = Cfg.Recipes.Single(r => r.Id == "build_drying_rack");
        Assert.That((rc.BaseMinutes, rc.Inputs["wood"], rc.Inputs["rope"], rc.MinSkills["crafting"], rc.Knowledge), Is.EqualTo((60, 4, 2, 1, (string?)null)));
        Assert.That(Cfg.Workshops.Stations.Single(x => x.Id == "drying_rack").Slots, Is.EqualTo(8));
        var w = Cook();
        E.DebugTeleport(w, "w13_mill");
        Assert.That(E.CanStart(w, Craft("build_drying_rack")), Is.EqualTo(GameEngine.WorkshopBuildOnly));

        var s = Cook();
        BuildRack(s);
        E.CreateItems(s, "wood", 4, s.Ground);
        E.CreateItems(s, "rope", 2, s.Ground);
        Assert.That(E.CanStart(s, Craft("build_drying_rack")), Does.Contain("已有晾制架（每区每类一座）"));
        Assert.That(E.CanStart(s, Craft("dry_meat")), Does.Contain("晾制架"), "晾制不在制作面板开工");
    }

    /// <summary>PLAN-Q170-02（前半）：8 位 4 肉 4 果同时投，第 9 批拒绝；180 世界分钟全部完成并留在原位占位；原料一次核销，干品截止 = 完成 + 168 小时。</summary>
    [Test]
    public void PLAN_Q170_02_EightSlots_FourMeatFourBerry_AllDoneIn180_NinthRejected_StaysInSlot()
    {
        var s = Cook();
        BuildRack(s);
        E.DebugDryingKit(s);
        s.Items.RemoveAll(i => i.ItemId is "wood" or "rope");
        var t0 = s.GameMinute;
        for (var k = 0; k < 4; k++) Ok(E.StartDrying(s, "dry_meat"));
        for (var k = 0; k < 4; k++) Ok(E.StartDrying(s, "dry_berries"));
        Assert.That(s.GameMinute, Is.EqualTo(t0), "投料 0 分钟");
        Assert.That(s.Main, Is.Null, "不占行动");
        Assert.That(Count(s, "raw_meat", s.Ground) + Count(s, "salt", s.Ground) + Count(s, "berry", s.Ground), Is.EqualTo(0), "原料全部迁入晾制位");
        Assert.That(s.Items.Where(i => i.ItemId is "raw_meat" or "salt" or "berry").All(i => i.InDrying), Is.True);
        E.CreateItems(s, "berry", 2, s.Ground);
        Assert.That(E.StartDrying(s, "dry_berries").Reason, Does.Contain("8个晾制位都在用"), "第 9 批拒绝");
        Assert.That(Count(s, "berry", s.Ground), Is.EqualTo(2), "拒绝不扣料");
        s.Items.RemoveAll(i => i.ItemId == "berry" && i.Owner == s.Ground);

        AdvanceTo(s, t0 + 120);
        var slots = s.Locations["camp"].Facility.DryingSlots;
        Assert.That(slots.Count(b => b.Status == DryingSlot.Done && b.RecipeId == "dry_berries"), Is.EqualTo(4), "果干 120 分钟完成");
        Assert.That(slots.Count(b => b.Status == DryingSlot.Drying && b.RecipeId == "dry_meat"), Is.EqualTo(4));
        AdvanceTo(s, t0 + 180);
        Assert.That(slots.Count, Is.EqualTo(8), "完成品占位直到取出");
        Assert.That(slots.All(b => b.Status == DryingSlot.Done), Is.True, "4 肉 4 果 180 世界分钟全部完成");
        Assert.That(Count(s, "dried_meat"), Is.EqualTo(4));
        Assert.That(Count(s, "dried_berries"), Is.EqualTo(4));
        Assert.That(s.Items.Where(i => i.ItemId is "dried_meat" or "dried_berries").All(i => i.InDrying), Is.True, "成品留在原位，不自动落地/入包");
        Assert.That(Count(s, "raw_meat") + Count(s, "salt") + Count(s, "berry"), Is.EqualTo(0), "原料一次核销，无免费原料、不复制盐");
        Assert.That(s.Items.Where(i => i.ItemId == "dried_meat").All(i => i.ExpiresAt == t0 + 180 + 168 * 60), Is.True, "肉干截止 = 完成 + 168 小时");
        Assert.That(s.Items.Where(i => i.ItemId == "dried_berries").All(i => i.ExpiresAt == t0 + 120 + 168 * 60), Is.True, "果干截止 = 完成 + 168 小时");
        Assert.That(E.StartDrying(s, "dry_berries").Reason, Does.Contain("都在用"), "完成品仍占位");

        // 取出：到当地地面；到随身验五卡
        Ok(E.TakeDrying(s, 0, false));
        Assert.That(Count(s, "dried_meat", s.Ground), Is.EqualTo(1));
        Assert.That(E.TakeDrying(s, 0, false).Reason, Does.Contain("是空的"), "取出幂等：不重复出");
        for (var k = 0; k < 5; k++) E.CreateItems(s, "stone_knife", 1, ItemInstance.Carried);
        var before = s.Items.Count(i => i.InDrying);
        Assert.That(E.TakeDrying(s, 1, true).Reason, Does.Contain("随身卡位不够"));
        Assert.That(s.Items.Count(i => i.InDrying), Is.EqualTo(before), "失败不清空");
        s.Items.RemoveAll(i => i.ItemId == "stone_knife");
        Ok(E.TakeDrying(s, 1, true));
        Assert.That(Count(s, "dried_meat", ItemInstance.Carried), Is.EqualTo(1));
        Assert.That(E.StartDrying(s, "dry_berries", 9).Reason, Does.Contain("没有这个晾制位"));
    }

    /// <summary>经验：每批成功 12（clamp(ceil(分钟/10),1,12)），计烹饪每日 30（与食品、净化同一账）；8 批同日只得 30。</summary>
    [Test]
    public void PLAN_Q170_Xp_Twelve_PerBatch_SharesDailyThirty()
    {
        var s = Cook();
        BuildRack(s);
        Assert.That(E.DryingXpFor(Cfg.Recipes.Single(r => r.Id == "dry_berries")), Is.EqualTo(12));
        Assert.That(E.DryingXpFor(Cfg.Recipes.Single(r => r.Id == "dry_meat")), Is.EqualTo(12));
        E.CreateItems(s, "berry", 2, s.Ground);
        Assert.That(E.DryingPreview(s, "dry_berries").XpText, Does.Contain("经验+12").And.Contain("与净化合计每日最多30"));
        var x0 = CookXp(s);
        var t0 = s.GameMinute;
        Ok(E.StartDrying(s, "dry_berries"));
        Assert.That(CookXp(s), Is.EqualTo(x0), "投料不给");
        AdvanceTo(s, t0 + 120);
        Assert.That(CookXp(s) - x0, Is.EqualTo(12), "完成给 12");
        Ok(E.TakeDrying(s, 0, false));
        Assert.That(CookXp(s) - x0, Is.EqualTo(12), "取出不再给");

        // 同日再 7 批：今日已得 12，最多再 18
        E.DebugDryingKit(s);
        for (var k = 0; k < 4; k++) Ok(E.StartDrying(s, "dry_meat"));
        for (var k = 0; k < 3; k++) Ok(E.StartDrying(s, "dry_berries"));
        Assert.That(E.DryingPreview(s, "dry_berries").XpText, Does.Contain("预计已满30").Or.Contain("没有空位").Or.Contain("已满"), "预览把同日先完成的晾制位算进去");
        var t1 = s.GameMinute;
        AdvanceTo(s, t1 + 180);
        Assert.That(CookXp(s) - x0, Is.EqualTo(30), "与其他烹饪共用每日 30");
        Assert.That(Clock.Day(s.GameMinute), Is.EqualTo(Clock.Day(t0)));
    }

    /// <summary>
    /// PLAN-Q170-02（后半）：未腐败取消清进度不退时间、0 经验；腐败停止不产可食干品、0 经验、只能清理。
    /// B22k（Q215）起肉干取消恢复投料时的剩余保质（原截止＋已晾时长），腐败停止只对果干；本例的腐败段改用野果。
    /// </summary>
    [Test]
    public void PLAN_Q170_02_Cancel_ReturnsOriginalExpiry_Spoil_NoProduct()
    {
        var s = Cook();
        BuildRack(s);
        E.CreateItems(s, "raw_meat", 1, ItemInstance.Carried);
        E.CreateItems(s, "salt", 1, s.Ground);
        var meatExp = First(s, "raw_meat").ExpiresAt;
        var x0 = CookXp(s);
        var t0 = s.GameMinute;
        Ok(E.StartDrying(s, "dry_meat", 3));
        Assert.That(E.DryingSlotAt(s, s.Ground, 3), Is.Not.Null, "投到所选的第 4 位");
        AdvanceTo(s, t0 + 60);
        Assert.That(E.DryingSlotAt(s, s.Ground, 3)!.DoneMinutes, Is.EqualTo(60));
        Ok(E.CancelDrying(s, 3, true));
        Assert.That(s.GameMinute, Is.EqualTo(t0 + 60), "已过时间不退");
        Assert.That(E.DryingSlotAt(s, s.Ground, 3), Is.Null, "清本批进度");
        Assert.That(First(s, "raw_meat").Owner, Is.EqualTo(ItemInstance.Carried));
        Assert.That(First(s, "raw_meat").ExpiresAt, Is.EqualTo(meatExp + 60), "Q215：恢复投料时的剩余保质（退料分钟＋R）");
        Assert.That(Count(s, "salt"), Is.EqualTo(1), "盐不复制");
        Assert.That(CookXp(s), Is.EqualTo(x0), "取消 0 经验");
        Assert.That(E.CancelDrying(s, 3, true).Reason, Does.Contain("是空的"), "取消幂等");

        // 腐败（果干）：野果在完成前到期 → 到期即停止，不出果干
        s.Items.RemoveAll(i => i.ItemId is "raw_meat" or "salt");
        E.CreateItems(s, "berry", 2, ItemInstance.Carried);
        foreach (var bi in s.Items.Where(i => i.ItemId == "berry")) bi.ExpiresAt = s.GameMinute + 60;
        var t1 = s.GameMinute;
        var pv = E.DryingPreview(s, "dry_berries");
        Assert.That(pv.Ok, Is.True);
        Assert.That(pv.Warnings, Has.Count.EqualTo(1), "预览提示会在完成前腐败");
        Ok(E.StartDrying(s, "dry_berries"));
        AdvanceTo(s, t1 + 120);
        var b = s.Locations["camp"].Facility.DryingSlots.Single();
        Assert.That(b.Status, Is.EqualTo(DryingSlot.Spoiled));
        Assert.That(b.DoneMinutes, Is.EqualTo(55), "到期那一段停止（到期时刻不能算完成）");
        Assert.That(Count(s, "dried_berries"), Is.EqualTo(0), "不产可食干品");
        Assert.That(CookXp(s), Is.EqualTo(x0), "腐败 0 经验");
        Assert.That(E.CancelDrying(s, b.Slot, false).Reason, Does.Contain("只能清理"));
        Ok(E.TakeDrying(s, b.Slot, false));
        var rotten = First(s, GameEngine.SpoiledMatterId, s.Ground);
        Assert.That((E.IsSpoiled(s, rotten), rotten.SpoiledFrom), Is.EqualTo((true, "berry")), "腐败原料按原腐败规则，不回活（BM5b2：已是腐败物）");
        Assert.That(s.Locations["camp"].Facility.DryingSlots, Is.Empty);
        // 已腐败的原料不能投（BM5b2：腐败物是另一物品，野果堆只计新鲜实物）
        Assert.That(E.StartDrying(s, "dry_berries").Reason, Does.Contain("缺少野果").And.Not.Contain("已腐败"));
    }

    /// <summary>门槛与来源：烹饪等级不够、预留材料、队友包与异地不自动取；离开营地仍推进、不能远程操作。</summary>
    [Test]
    public void Gates_SkillReservedRemote_LeaveKeepsDrying()
    {
        var s = Cook(cooking: 1);
        BuildRack(s);
        E.CreateItems(s, "raw_meat", 1, s.Ground);
        E.CreateItems(s, "salt", 1, s.Ground);
        Assert.That(E.StartDrying(s, "dry_meat").Reason, Does.Contain("需要烹饪5级"));
        E.CreateItems(s, "berry", 2, s.Ground);
        First(s, "berry").ReservedBy = 999;
        Assert.That(E.StartDrying(s, "dry_berries").Reason, Does.Contain("已预留或占用2份"));
        First(s, "berry").ReservedBy = null;
        var t0 = s.GameMinute;
        Ok(E.StartDrying(s, "dry_berries"));
        var next = E.AdjacentUnlocked(s).First().Other("camp");
        Start(s, ActionRequest.Travel(next));
        Run(s);
        Assert.That(E.DryingRackHere(s), Is.False);
        Assert.That(E.TakeDrying(s, 0, false).Reason, Does.Contain("没有晾制架"), "异地不能取");
        AdvanceTo(s, t0 + 120);
        Assert.That(s.Locations["camp"].Facility.DryingSlots.Single().Status, Is.EqualTo(DryingSlot.Done), "离开地点仍随世界时间推进");
    }

    /// <summary>PLAN-Q170-02：读档不重发经验/物品；投料请求号幂等。</summary>
    [Test]
    public void PLAN_Q170_02_Reload_NoDuplicateXpOrItems_RequestIdempotent()
    {
        var s = Cook();
        BuildRack(s);
        E.CreateItems(s, "berry", 4, s.Ground);
        Ok(E.StartDrying(s, "dry_berries", null, "req-1"));
        Assert.That(E.StartDrying(s, "dry_berries", null, "req-1").Ok, Is.True);
        Assert.That(s.Locations["camp"].Facility.DryingSlots, Has.Count.EqualTo(1), "同一请求号不重复投");
        var t0 = s.GameMinute;
        AdvanceTo(s, t0 + 60);
        var mid = SaveSerializer.Deserialize(SaveSerializer.Serialize(s), Cfg);
        Assert.That(mid.Locations["camp"].Facility.DryingSlots.Single().DoneMinutes, Is.EqualTo(60), "读档按存档进度续算，不补现实时间");
        Assert.That(mid.Items.Where(i => i.InDrying).Sum(i => i.Count), Is.EqualTo(2));
        var x0 = CookXp(mid);
        AdvanceTo(mid, t0 + 120);
        Assert.That(CookXp(mid) - x0, Is.EqualTo(12));
        var after = SaveSerializer.Deserialize(SaveSerializer.Serialize(mid), Cfg);
        AdvanceTo(after, t0 + 180);
        Assert.That(CookXp(after) - x0, Is.EqualTo(12), "读档不重发经验");
        Assert.That(Count(after, "dried_berries"), Is.EqualTo(1), "读档不重复出");
        Ok(E.TakeDrying(after, 0, true));
        var again = SaveSerializer.Deserialize(SaveSerializer.Serialize(after), Cfg);
        Assert.That(CookXp(again) - x0, Is.EqualTo(12), "取出后读档也不再给");
        Assert.That(Count(again, "dried_berries"), Is.EqualTo(1));
    }

    /// <summary>
    /// 试玩修复 M470：先投 4 批肉干（3 小时）再按第 8、5、6、7 位投果干（2 小时）——经验预估与投料日志只算比自己先完成（同刻按结算顺序在前）的位，
    /// 不把刚投的这一位算进去；果干 12、12、6、0，肉干在果干之后完成都是 0；当日已满时不写“先完成的晾制位预计占0”。原料到期＝完成时写“不晚于完成”并进日志。
    /// </summary>
    [Test]
    public void M470_XpPreview_OnlyEarlierSlots_FullDayNoAhead_SpoilWarnInLog()
    {
        var s = Cook();
        BuildRack(s);
        E.DebugDryingKit(s);
        for (var k = 0; k < 4; k++)
        {
            Ok(E.StartDrying(s, "dry_meat", k));
            Assert.That(s.Log.Last().Text, Does.Contain(k < 2 ? "经验+12" : k == 2 ? "经验+6" : "预计已满30"), $"肉干第{k + 1}位投料日志");
        }
        var want = new[] { "经验+12", "经验+12", "经验+6", "预计已满30" };
        var slots = new[] { 7, 4, 5, 6 };
        for (var n = 0; n < 4; n++)
        {
            Assert.That(E.DryingPreview(s, "dry_berries", slots[n]).XpText, Does.Contain(want[n]), $"果干第{slots[n] + 1}位预估");
            Ok(E.StartDrying(s, "dry_berries", slots[n]));
            Assert.That(s.Log.Last().Text, Does.Contain(want[n]), $"果干第{slots[n] + 1}位投料日志");
        }
        var views = E.DryingSlotViews(s);
        Assert.That(views[4].XpText, Does.Contain("经验+12"), "第5位：只有第8位比它先结算");
        Assert.That(views.Take(4).All(v => v.XpText!.Contains("预计已满30")), Is.True, "肉干在果干之后完成");

        var t0 = s.GameMinute;
        AdvanceTo(s, t0 + 180);
        for (var k = 0; k < 8; k++) Ok(E.TakeDrying(s, k, false));
        E.CreateItems(s, "berry", 2, s.Ground);
        var full = E.DryingPreview(s, "dry_berries").XpText;
        Assert.That(full, Does.Contain("今日烹饪经验已满30"));
        Assert.That(full, Does.Not.Contain("预计占"), "已满只写已满");

        s.Items.RemoveAll(i => i.ItemId == "berry");
        var b = E.CreateItems(s, "berry", 2, s.Ground)[0];
        b.ExpiresAt = s.GameMinute + 120;
        var pv = E.DryingPreview(s, "dry_berries");
        Assert.That(pv.Warnings.Single(), Does.Contain("会在完成前腐败，届时停止且没有成品"), "到期＝完成也警告（B22k 起用 Q214 定的红字）");
        Ok(E.StartDrying(s, "dry_berries"));
        Assert.That(s.Log.Last().Text, Does.Contain("会在完成前腐败"), "投料日志带腐败警告");
        Assert.That(E.DryingSummaryParts(s), Is.EqualTo(new[] { "晾制中1", "空7" }));
    }

    [Test]
    public void Migration37To38_NoFreeRack_EmptySlots()
    {
        var s = Cook();
        var j = JObject.Parse(SaveSerializer.Serialize(s));
        j["SchemaVersion"] = "37";
        var t = SaveSerializer.Deserialize(j.ToString(), Cfg);
        Assert.That(t.SchemaVersion, Is.EqualTo(GameState.CurrentSchema));
        Assert.That(t.Locations["camp"].Facility.Workshops, Does.Not.Contain("drying_rack"), "未建不赠架");
        Assert.That(t.Locations["camp"].Facility.DryingSlots, Is.Empty);
        Assert.That(t.Log.Count(l => l.Text.Contains("存档已升级到版本38")), Is.EqualTo(1));
        var again = SaveSerializer.Deserialize(SaveSerializer.Serialize(t), Cfg);
        Assert.That(again.Log.Count(l => l.Text.Contains("存档已升级到版本38")), Is.EqualTo(1), "幂等");
    }
}
