using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 B10d：策划提交 09f2dc7 答复 Q113（选 A）——火堆“煅烧木炭”系统自动取料（主角随身优先、不足再取当前地面），
/// 浮窗逐堆列来源/实例/数量，确认才投；确认时按玩家看到的明细重验，来源失效整条拒绝、不静默换另一堆（A10 WS-Q113-01—04）。
/// </summary>
public class B10dCharcoalSourceTests : TestBase
{
    private const string Fire = "facility:camp:fire";
    private const string Mate = "carried:p1";

    private static void ClearWood(GameState s) => s.Items.RemoveAll(i => i.ItemId == "wood");

    private static int Wood(GameState s, string owner) => s.Items.Where(i => i.ItemId == "wood" && i.Owner == owner).Sum(i => i.Count);

    private static FacilityState Fac(GameState s) => s.Locations["camp"].Facility;

    private static List<FacilityProcess> Burns(GameState s) => Fac(s).Processes.Where(p => p.RecipeId == "fire_charcoal").ToList();

    private static ItemInstance Pile(GameState s, string owner) => s.Items.Single(i => i.ItemId == "wood" && i.Owner == owner);

    /// <summary>新局清掉木材后：随身 carried 份、营地地面 ground 份（各一堆）。</summary>
    private GameState WithWood(int carried, int ground)
    {
        var s = New();
        ClearWood(s);
        if (carried > 0) E.CreateItems(s, "wood", carried, ItemInstance.Carried);
        if (ground > 0) E.CreateItems(s, "wood", ground, "camp");
        return s;
    }

    private static (int Id, int Take, bool Carried)[] Picks(CharcoalLoadInfo info) =>
        info.Plan.Picks.Select(p => (p.InstanceId, p.Take, p.Carried)).ToArray();

    private ActionResult Confirm(GameState s, CharcoalPlan plan, string? requestId = null) =>
        E.DoInstant(s, InstantRequest.LoadCharcoal(plan).WithRequestId(requestId));

    private void AssertRejectedUnchanged(GameState s, CharcoalPlan plan, string reasonPart)
    {
        var before = SaveSerializer.Serialize(s);
        var res = Confirm(s, plan);
        Assert.That(res.Ok, Is.False);
        Assert.That(res.Reason, Does.Contain(reasonPart).And.Contain("请重新查看明细后再确认"));
        Assert.That(SaveSerializer.Serialize(s), Is.EqualTo(before), "整条拒绝：不取其中几份、不换另一堆、什么都不变");
        Assert.That(Burns(s), Is.Empty);
    }

    /// <summary>WS-Q113-01：随身≥4 只取随身；随身2+地面≥2 各取2；随身0 地面≥4 全取地面；均不碰队友/别处/预留木材。</summary>
    [Test]
    public void WS_Q113_01_AutoAllocation_CarriedFirstThenGround()
    {
        // 随身 6、地面 5：只取随身 4
        var a = WithWood(6, 5);
        var aw = Pile(a, ItemInstance.Carried);
        var info = E.CharcoalLoadPreview(a);
        Assert.That(info.Ok, Is.True, info.Reason);
        Assert.That(Picks(info), Is.EqualTo(new[] { (aw.InstanceId, 4, true) }));
        Assert.That(info.Sources, Is.EqualTo(new[] { $"随身木材#{aw.InstanceId}×4" }));
        Assert.That(info.Plan.Location, Is.EqualTo("camp"));
        Assert.That(Confirm(a, info.Plan).Ok, Is.True);
        Assert.That((Wood(a, ItemInstance.Carried), Wood(a, "camp"), Wood(a, Fire)), Is.EqualTo((2, 5, 4)));

        // 随身 2、地面 3：随身 2 + 地面 2（最后一堆只取所缺）
        var b = WithWood(2, 3);
        var bc = Pile(b, ItemInstance.Carried);
        var bg = Pile(b, "camp");
        info = E.CharcoalLoadPreview(b);
        Assert.That(Picks(info), Is.EqualTo(new[] { (bc.InstanceId, 2, true), (bg.InstanceId, 2, false) }));
        Assert.That(info.Sources, Is.EqualTo(new[] { $"随身木材#{bc.InstanceId}×2", $"地面木材#{bg.InstanceId}×2" }));
        Assert.That(Confirm(b, info.Plan).Ok, Is.True);
        Assert.That((Wood(b, ItemInstance.Carried), Wood(b, "camp"), Wood(b, Fire)), Is.EqualTo((0, 1, 4)));

        // 随身 0、地面 4：全取地面（恰好一整堆原样移入加工位）
        var c = WithWood(0, 4);
        var cg = Pile(c, "camp");
        info = E.CharcoalLoadPreview(c);
        Assert.That(Picks(info), Is.EqualTo(new[] { (cg.InstanceId, 4, false) }));
        Assert.That(Confirm(c, info.Plan).Ok, Is.True);
        Assert.That(cg.Owner, Is.EqualTo(Fire));
        Assert.That(Wood(c, "camp"), Is.EqualTo(0));

        // 队友包、别处地面、预留的木材都不取：随身 1 + 地面 2 + 队友 5 + 别处 5 + 预留 3 → 只有 3 可用，整次拒绝
        var d = WithWood(1, 2);
        E.CreateItems(d, "wood", 5, Mate);
        E.CreateItems(d, "wood", 5, "field");
        var reserved = new ItemInstance { InstanceId = d.NextInstanceId++, ItemId = "wood", Count = 3, Owner = "camp", ReservedBy = 999 };
        d.Items.Add(reserved);
        info = E.CharcoalLoadPreview(d);
        Assert.That(info.Ok, Is.False);
        Assert.That(info.WoodAvailable, Is.EqualTo(3));
        Assert.That(info.Reason, Does.Contain("3/4").And.Contain("不取队友或别处的"));
        Assert.That(info.Plan.Picks.Select(p => p.InstanceId), Does.Not.Contain(reserved.InstanceId));
        var before = SaveSerializer.Serialize(d);
        Assert.That(Confirm(d, info.Plan).Ok, Is.False);
        Assert.That(E.DoInstant(d, InstantRequest.LoadCharcoal()).Ok, Is.False);
        Assert.That(SaveSerializer.Serialize(d), Is.EqualTo(before));
        Assert.That((Wood(d, Mate), Wood(d, "field")), Is.EqualTo((5, 5)));

        // 补足后仍只取随身+当前地面，队友/别处/预留原样
        E.CreateItems(d, "wood", 1, "camp");
        info = E.CharcoalLoadPreview(d);
        Assert.That(info.Ok, Is.True, info.Reason);
        Assert.That(info.Plan.Picks.All(p => p.Carried || d.Items.Single(i => i.InstanceId == p.InstanceId).Owner == "camp"), Is.True);
        Assert.That(Confirm(d, info.Plan).Ok, Is.True);
        Assert.That((Wood(d, Mate), Wood(d, "field"), reserved.Count, reserved.Owner), Is.EqualTo((5, 5, 3, "camp")));
    }

    /// <summary>WS-Q113-02：预览逐堆列真实来源/实例/数量合计 4；反复预览不扣料、不占位、不推进时间、不点火加燃料。</summary>
    [Test]
    public void WS_Q113_02_Preview_IsReadOnly_ListsEachPile()
    {
        var s = WithWood(2, 5);
        var before = SaveSerializer.Serialize(s);
        var t0 = s.GameMinute;
        CharcoalLoadInfo? first = null;
        for (var k = 0; k < 5; k++)
        {
            var info = E.CharcoalLoadPreview(s);
            first ??= info;
            Assert.That(info.Plan.SameAs(first.Plan), Is.True, "反复预览明细一致");
            Assert.That(info.Plan.Picks.Sum(p => p.Take), Is.EqualTo(4));
            Assert.That(info.Sources, Has.Count.EqualTo(info.Plan.Picks.Count));
            for (var i = 0; i < info.Sources.Count; i++)
                Assert.That(info.Sources[i], Does.Contain($"#{info.Plan.Picks[i].InstanceId}×{info.Plan.Picks[i].Take}"));
        }
        // 只构造请求（浮窗打开/取消/Esc 都不提交）也不改状态
        _ = InstantRequest.LoadCharcoal(first!.Plan);
        Assert.That(SaveSerializer.Serialize(s), Is.EqualTo(before), "预览/取消 0 扣料、0 占位、0 时间");
        Assert.That(s.GameMinute, Is.EqualTo(t0));
        Assert.That(E.FireContents(s, "camp"), Is.Empty);
        Assert.That((Fac(s).FireLit, Fac(s).FuelMinutes), Is.EqualTo((false, 0)));
    }

    /// <summary>WS-Q113-03：确认前预览里的一堆被移走/预留/数量变化、分配结果变了、换了地点 → 整次拒绝且状态不变、不换另一堆；重新预览后可确认。</summary>
    [Test]
    public void WS_Q113_03_StaleSource_RejectsWhole_NoSilentSwap()
    {
        // ① 移走：随身 2 + 地面 5 预览后，随身那堆放到地面（并入地面堆）——不得改取地面 4
        var s = WithWood(2, 5);
        var carried = Pile(s, ItemInstance.Carried);
        var plan = E.CharcoalLoadPreview(s).Plan;
        Assert.That(E.DoInstant(s, InstantRequest.Drop(carried.InstanceId, 2)).Ok, Is.True);
        AssertRejectedUnchanged(s, plan, $"随身木材#{carried.InstanceId}已不可用");
        Assert.That(Wood(s, "camp"), Is.EqualTo(7));
        var fresh = E.CharcoalLoadPreview(s);
        Assert.That(fresh.Plan.SameAs(plan), Is.False);
        Assert.That(Confirm(s, fresh.Plan).Ok, Is.True, "刷新明细后重新确认可投");
        Assert.That((Wood(s, "camp"), Wood(s, Fire)), Is.EqualTo((3, 4)));

        // ② 预留/锁定：随身 4 + 地面 4 预览取随身 4；随身那堆被预留 → 不得改取地面那堆
        var t = WithWood(4, 4);
        var tc = Pile(t, ItemInstance.Carried);
        plan = E.CharcoalLoadPreview(t).Plan;
        tc.ReservedBy = 7;
        AssertRejectedUnchanged(t, plan, $"随身木材#{tc.InstanceId}已不可用");
        Assert.That(Wood(t, "camp"), Is.EqualTo(4), "地面那堆原位没动");
        tc.ReservedBy = null;
        Assert.That(Confirm(t, plan).Ok, Is.True, "原来源恢复且明细一致时可确认");
        Assert.That((Wood(t, ItemInstance.Carried), Wood(t, "camp")), Is.EqualTo((0, 4)));

        // ③ 数量变化：随身 6 预览后变成 5（仍够也算预览条件失效）
        var u = WithWood(6, 0);
        var uc = Pile(u, ItemInstance.Carried);
        plan = E.CharcoalLoadPreview(u).Plan;
        uc.Count = 5;
        AssertRejectedUnchanged(u, plan, "由6份变为5份");
        Assert.That(Confirm(u, E.CharcoalLoadPreview(u).Plan).Ok, Is.True);
        Assert.That(Wood(u, ItemInstance.Carried), Is.EqualTo(1));

        // ④ 原堆还在且数量没变，但自动分配变了：预览时随身 0 只取地面 4，之后随身又拾到 4 份新木材 → 现在会只取随身，原明细整条拒绝
        var v = WithWood(0, 4);
        var vg = Pile(v, "camp");
        plan = E.CharcoalLoadPreview(v).Plan;
        E.CreateItems(v, "wood", 4, ItemInstance.Carried);
        AssertRejectedUnchanged(v, plan, "现在会取：随身木材#");
        Assert.That((vg.Count, vg.Owner), Is.EqualTo((4, "camp")));
        Assert.That(Confirm(v, E.CharcoalLoadPreview(v).Plan).Ok, Is.True);
        Assert.That((Wood(v, ItemInstance.Carried), Wood(v, "camp")), Is.EqualTo((0, 4)), "重新确认后按随身优先取随身 4");

        // ⑤ 地点不符（预览时在别处）
        var w = WithWood(4, 0);
        plan = E.CharcoalLoadPreview(w).Plan;
        plan.Location = "field";
        AssertRejectedUnchanged(w, plan, "已不在预览时的地点");

        // ⑥ 位满 / 缺料 / 行动中：同样整次拒绝不扣（沿 Q111 原因）
        var x = WithWood(0, 12);
        plan = E.CharcoalLoadPreview(x).Plan;
        Assert.That(Confirm(x, plan).Ok, Is.True);
        Assert.That(Confirm(x, E.CharcoalLoadPreview(x).Plan).Ok, Is.True);
        var before = SaveSerializer.Serialize(x);
        var res = Confirm(x, E.CharcoalLoadPreview(x).Plan);
        Assert.That(res.Reason, Does.StartWith("火堆加工位已满"));
        Assert.That(SaveSerializer.Serialize(x), Is.EqualTo(before));

        var y = WithWood(0, 4);
        plan = E.CharcoalLoadPreview(y).Plan;
        Start(y, ActionRequest.Wait(30));
        Assert.That(Confirm(y, plan).Reason, Does.Contain("行动进行中"));
        Assert.That(Wood(y, "camp"), Is.EqualTo(4));
    }

    /// <summary>WS-Q113-04：成功只迁移这 4 份、其余原位；同一请求号重发、同一份明细再次提交都不取第二批；存读档往返一致；燃料/受热/0XP 不变。</summary>
    [Test]
    public void WS_Q113_04_Success_MovesOnlyFour_NoDoubleTake_SaveRoundTrip()
    {
        var s = WithWood(8, 6);
        var sc = Pile(s, ItemInstance.Carried);
        var sg = Pile(s, "camp");
        var xp = s.Skills.ToDictionary(k => k.Key, k => k.Value.Xp);
        var t0 = s.GameMinute;
        var info = E.CharcoalLoadPreview(s);

        // 存读档往返：读回的局预览出同一份明细，按存档前的明细确认结果与原局一致
        var loaded = SaveSerializer.Deserialize(SaveSerializer.Serialize(s), Cfg);
        Assert.That(E.CharcoalLoadPreview(loaded).Plan.SameAs(info.Plan), Is.True);

        Assert.That(Confirm(s, info.Plan, "req-q113-1").Ok, Is.True);
        Assert.That(Confirm(loaded, info.Plan, "req-q113-1").Ok, Is.True);
        Assert.That(SaveSerializer.Serialize(loaded), Is.EqualTo(SaveSerializer.Serialize(s)), "读档后确认与原局结果一致");

        Assert.That(sc.Count, Is.EqualTo(4), "随身那堆只少 4");
        Assert.That((sg.Count, sg.Owner), Is.EqualTo((6, "camp")), "地面那堆原位不动");
        var batch = E.FireContents(s, "camp").Single();
        Assert.That((batch.ItemId, batch.Count, batch.Owner), Is.EqualTo(("wood", 4, Fire)));
        var p = Burns(s).Single();
        Assert.That((p.TargetInstanceId, p.HeatDoneMinutes, p.RequiredHeatMinutes), Is.EqualTo((batch.InstanceId, 0, 60)));
        Assert.That(s.GameMinute, Is.EqualTo(t0), "0 世界分钟");
        Assert.That((Fac(s).FireLit, Fac(s).FuelMinutes), Is.EqualTo((false, 0)), "不点火、不加燃料");
        Assert.That(s.Skills.ToDictionary(k => k.Key, k => k.Value.Xp), Is.EqualTo(xp), "0 经验");

        // 同一请求号重发：直接视为已处理，不取第二批
        var again = Confirm(s, info.Plan, "req-q113-1");
        Assert.That((again.Ok, again.Duplicate), Is.EqualTo((true, true)));
        Assert.That(Burns(s), Has.Count.EqualTo(1));

        // 同一份明细换请求号再提交：随身那堆已由 8 变 4，按来源失效整条拒绝，不取第二批
        var before = SaveSerializer.Serialize(s);
        var second = Confirm(s, info.Plan, "req-q113-2");
        Assert.That(second.Ok, Is.False);
        Assert.That(second.Reason, Does.Contain("由8份变为4份"));
        Assert.That(SaveSerializer.Serialize(s), Is.EqualTo(before));
        Assert.That((sc.Count, sg.Count, Burns(s).Count), Is.EqualTo((4, 6, 1)));

        // 燃料与受热沿 Q111：燃足 60 分钟后这一批出 3 木炭
        Fac(s).FireLit = true;
        Fac(s).FuelMinutes = 60;
        Start(s, ActionRequest.Wait(60));
        Run(s);
        Assert.That(Burns(s), Is.Empty);
        Assert.That(E.FireContents(s, "camp").Single().ItemId, Is.EqualTo("charcoal"));
        Assert.That(E.FireContents(s, "camp").Single().Count, Is.EqualTo(3));
        Assert.That(s.Skills.ToDictionary(k => k.Key, k => k.Value.Xp)["crafting"], Is.EqualTo(xp["crafting"]), "煅烧不给制造经验");
    }
}
