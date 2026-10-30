using Newtonsoft.Json.Linq;
using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 策划提交 5493951 Q16（S11 §10、S01 §10、S06 §8 末段、S10、R06 §5）：已变质不得开工；新鲜食材实际完整受热一段后受本次加工保护，
/// 熄火/退出等待保留，成功取出解除并按原期限立即判断，失败取出不变；所有合法复煮完成起 24 小时；等待烹饪按受热保护资格选目标。
/// 验收 S11-A13~A16、S06-A11、S10（取出解除）、R06-A06、S13-A09（烹饪等待资格）、S01-A14 迁移边界。
/// </summary>
public class V11CookProtectTests : TestBase
{
    private const string Fire = "facility:camp:fire";

    private (GameState s, ItemInstance pot) PotOnFire(string? water, int units, int fuel = 180)
    {
        var s = New();
        var pot = First(s, "pot");
        if (water != null && units > 0)
        {
            E.DebugGive(s, water, units, "camp");
            Assert.That(E.DoInstant(s, InstantRequest.FillPot(First(s, water, "camp").InstanceId, pot.InstanceId, units)).Ok, Is.True);
        }
        Assert.That(E.DoInstant(s, InstantRequest.PutInFire(pot.InstanceId)).Ok, Is.True);
        var fac = s.Locations["camp"].Facility;
        fac.FireLit = true;
        fac.FuelMinutes = fuel;
        return (s, pot);
    }

    private ItemInstance RootInPot(GameState s, ItemInstance pot)
    {
        E.DebugGive(s, "raw_root", 1, "camp");
        Assert.That(E.DoInstant(s, InstantRequest.PutFoodInPot(First(s, "raw_root", "camp").InstanceId, pot.InstanceId, 1)).Ok, Is.True);
        return E.PotFood(s, pot.InstanceId).Last();
    }

    /// <summary>空闲等待指定分钟（按 5 分钟段推进，多余的等待在边界取消）。</summary>
    private void Wait(GameState s, int minutes)
    {
        var end = s.GameMinute + minutes;
        while (s.GameMinute < end)
        {
            if (s.Main == null) Start(s, ActionRequest.Wait(30));
            E.Advance(s);
        }
        if (s.Main?.Type == ActionType.Wait) E.Cancel(s);
    }

    /// <summary>S11-A13：新鲜食材首个有效段跨过原期限即取得保护；仅预留/缺水/待净化的不受保护、照常腐败。</summary>
    [Test]
    public void S11_A13_FirstHeatedSegmentProtects_ReservedOnlyDoesNot()
    {
        var (s, pot) = PotOnFire("clean_water", 2);
        var root = RootInPot(s, pot);
        root.ExpiresAt = s.GameMinute + 5;            // 首段内到期
        Assert.That(E.ProcessOf(s, root)!.HeatProtected, Is.False, "放入/预留时不保护");
        Wait(s, 5);
        var p = E.ProcessOf(s, root)!;
        Assert.That(p.HeatProtected, Is.True, "首段实际完整受热后保护");
        Assert.That(p.HeatDoneMinutes, Is.EqualTo(5));
        Assert.That(E.IsSpoiled(s, root), Is.False, "受保护不判腐败");
        Assert.That(root.ExpiresAt, Is.EqualTo(s.GameMinute), "不改写原 expiresAt");
        Assert.That(E.ContentStatus(s, root), Does.Contain("受热保护中").And.Contain("取出即腐败"));

        // 待净化（脏水）：食材还没受热，到期照常腐败
        var (t, tpot) = PotOnFire("dirty_water", 2);
        var r2 = RootInPot(t, tpot);
        r2.ExpiresAt = t.GameMinute + 10;
        Wait(t, 10);
        Assert.That(E.ProcessOf(t, r2)?.HeatProtected ?? false, Is.False, "等水净化不算开始烹饪");
        Assert.That(E.IsSpoiled(t, r2), Is.True, "未受热食材到期腐败");

        // 缺水只存放：到期照常腐败
        var (u, upot) = PotOnFire(null, 0);
        var r3 = RootInPot(u, upot);
        r3.ExpiresAt = u.GameMinute + 5;
        Wait(u, 10);
        Assert.That(E.IsSpoiled(u, r3), Is.True);
    }

    /// <summary>S11-A14/S06-A11/S10：熄火与退出等待保留保护；成功取出解除并立即按原期限腐败；失败取出（超重）什么都不变。</summary>
    [Test]
    public void S11_A14_S06_A11_ExtinguishKeeps_TakeOutClears_FailedTakeOutUnchanged()
    {
        var (s, pot) = PotOnFire("clean_water", 2);
        var root = RootInPot(s, pot);
        root.ExpiresAt = s.GameMinute + 10;
        Wait(s, 10);
        Assert.That(E.ProcessOf(s, root)!.HeatProtected, Is.True);
        Assert.That(E.DoInstant(s, InstantRequest.Extinguish()).Ok, Is.True);
        Wait(s, 30);
        Assert.That(E.ProcessOf(s, root)!.HeatProtected, Is.True, "熄火保留保护");
        Assert.That(E.ProcessOf(s, root)!.HeatDoneMinutes, Is.EqualTo(10), "熄火保留热进度");
        Assert.That(E.IsSpoiled(s, root), Is.False);
        Assert.That(E.CanStart(s, ActionRequest.UseInstance(root.InstanceId)), Does.Contain("需先烹饪"), "生块根本来不可直接食用：受保护也不能吃（Q23 只放开原本可食的实例）");

        // 失败取出：随身超重 → 整次拒绝，保护与进度不变
        E.DebugGive(s, "stone", 1);
        var stone = s.Items.First(i => i.ItemId == "stone" && i.IsCarried);
        stone.Count = Cfg.Carry.HardGrams / E.Item("stone").WeightGrams;
        var cons = E.TakeOutConsequenceOf(s, root);
        Assert.That(cons.Spoils, Has.Count.EqualTo(1), "提前写明取出即腐败");
        Assert.That(cons.Text, Does.Contain("取出即解除烹饪保护并立即腐败"));
        var fail = E.DoInstant(s, InstantRequest.TakeOut(root.InstanceId, ItemInstance.Carried));
        Assert.That(fail.Ok, Is.False, "超重取出失败");
        Assert.That(E.ProcessOf(s, root)!.HeatProtected, Is.True, "失败取出不先解除保护");
        Assert.That(E.ProcessOf(s, root)!.HeatDoneMinutes, Is.EqualTo(10));

        // 成功取到地面：解除保护，原期限已过立即腐败，不能再放回烹饪
        Assert.That(E.DoInstant(s, InstantRequest.TakeOut(root.InstanceId, "camp")).Ok, Is.True);
        Assert.That(E.ProcessOf(s, root), Is.Null);
        Assert.That(E.IsSpoiled(s, root), Is.True, "取出后按原 expiresAt 立即腐败");
        Assert.That(s.Log.Last().Text, Does.Contain("解除烹饪保护"));
        var back = E.DoInstant(s, InstantRequest.PutFoodInPot(root.InstanceId, pot.InstanceId, 1));
        Assert.That(back.Ok, Is.False, "已变质不能重新放回烹饪");

        // 仍新鲜的受保护食材取出：只解除保护，不腐败
        var (t, tpot) = PotOnFire("clean_water", 2);
        var r2 = RootInPot(t, tpot);
        Wait(t, 5);
        Assert.That(E.ProcessOf(t, r2)!.HeatProtected, Is.True);
        Assert.That(E.DoInstant(t, InstantRequest.TakeOut(tpot.InstanceId, "camp")).Ok, Is.True, "取下整锅");
        Assert.That(E.ProcessOf(t, r2), Is.Null, "整锅取下：锅内进程与保护一并解除");
        Assert.That(E.IsSpoiled(t, r2), Is.False);
    }

    /// <summary>
    /// Q23 选 B（S11 X4、S10 X4、S01 2026-10-30 裁定；验收 COMP03、COMP17）：受保护、原本可食、已过原期限的烤肉可直接吃，按原物结算；
    /// 食用原子：只扣这一份、只终止它自己的复煮进程、只释放它自己的 500 毫升预留；同锅生块根的进程/保护/预留不动；
    /// 已耗燃料不退、锅里的水不扣；之后不会再出水煮肉；同一请求重发不重复扣。
    /// </summary>
    [Test]
    public void Q23_ProtectedPastExpiryEdible_AtomicOwnProcessOnly()
    {
        var (s, pot) = PotOnFire("clean_water", 2);
        var meat = E.CreateItems(s, "cooked_meat", 1, "camp").Single();
        meat.ExpiresAt = s.GameMinute + 5;            // 首段内到期
        Assert.That(E.DoInstant(s, InstantRequest.PutFoodInPot(meat.InstanceId, pot.InstanceId, 1)).Ok, Is.True);
        var root = RootInPot(s, pot);
        Assert.That(E.PotReservedMl(s, pot.InstanceId), Is.EqualTo(1000), "两份食材各预留500毫升");
        Wait(s, 5);
        var mp = E.ProcessOf(s, meat)!;
        Assert.That(mp.HeatProtected, Is.True);
        Assert.That(E.PastExpiryButProtected(s, meat), Is.True, "受保护且已过原期限");
        Assert.That(E.ProcessOf(s, root)!.HeatProtected, Is.True);

        SetStats(s, 20, 50, 50, 18, 90, 60);
        var req = ActionRequest.UseInstance(meat.InstanceId).WithRequestId("eat-q23");
        Assert.That(E.CanStart(s, req), Is.Null, "Q23：受保护期间过原期限仍可直接食用");
        var pv = E.Preview(s, req);
        Assert.That(pv.Ok, Is.True, pv.Reason);
        Assert.That(string.Join("；", pv.Notes), Does.Contain("复煮随之终止").And.Contain("释放预留净水500毫升").And.Contain("已耗燃料不退"));

        var fuel = s.Locations["camp"].Facility.FuelMinutes;
        var water = E.PotUnits(s, pot.InstanceId);
        var meatId = meat.InstanceId;
        Start(s, req);
        Assert.That(s.Stats.Get(StatId.Satiety), Is.EqualTo(85).Within(1e-9), "按原物（烤肉）结算饱食+35");
        Assert.That(s.Items.Any(i => i.InstanceId == meatId), Is.False, "只扣这一份");
        Assert.That(E.AllProcesses(s).Any(p => p.TargetInstanceId == meatId), Is.False, "终止它自己的复煮进程");
        var rp = E.ProcessOf(s, root)!;
        Assert.That(rp.HeatProtected, Is.True, "同锅生块根的保护不动");
        Assert.That(rp.HeatDoneMinutes, Is.EqualTo(5), "同锅生块根的热进度不动");
        Assert.That(E.PotReservedMl(s, pot.InstanceId), Is.EqualTo(500), "只释放烤肉那份预留");
        Assert.That(E.PotUnits(s, pot.InstanceId), Is.EqualTo(water), "锅里的水不扣");
        Assert.That(s.Locations["camp"].Facility.FuelMinutes, Is.EqualTo(fuel), "燃料不退");
        Assert.That(s.Log.Last().Text, Does.Contain("食用1份烤肉").And.Contain("复煮随之终止"));

        var again = E.Start(s, req);
        Assert.That(again.Duplicate, Is.True, "同一请求重发不重复扣");
        Assert.That(E.Start(s, ActionRequest.UseInstance(meatId)).Ok, Is.False, "这一份已不在");

        Run(s);
        Wait(s, 30);
        Assert.That(s.Items.Any(i => i.ItemId == "boiled_meat"), Is.False, "不会再产出水煮肉");
        Assert.That(E.PotFood(s, pot.InstanceId).Single().ItemId, Is.EqualTo("boiled_root"), "生块根照常煮熟");
    }

    /// <summary>Q23 边界：未受热（只预留）的过期烤肉照常腐败、不能吃；受保护的烤肉取出后解除保护立即腐败、也不能吃。</summary>
    [Test]
    public void Q23_UnprotectedOrTakenOutStillSpoiled()
    {
        // 锅不在火堆上：只存放，不受热，到期腐败
        var s = New();
        var pot = First(s, "pot");
        var meat = E.CreateItems(s, "cooked_meat", 1, "camp").Single();
        Assert.That(E.DoInstant(s, InstantRequest.PutFoodInPot(meat.InstanceId, pot.InstanceId, 1)).Ok, Is.True);
        meat.ExpiresAt = s.GameMinute - 1;
        Assert.That(E.CanStart(s, ActionRequest.UseInstance(meat.InstanceId)), Is.EqualTo("已腐败，不能食用"));

        // 受保护后取出：按原期限立即腐败
        var (t, tpot) = PotOnFire("clean_water", 2);
        var m2 = E.CreateItems(t, "cooked_meat", 1, "camp").Single();
        m2.ExpiresAt = t.GameMinute + 5;
        Assert.That(E.DoInstant(t, InstantRequest.PutFoodInPot(m2.InstanceId, tpot.InstanceId, 1)).Ok, Is.True);
        Wait(t, 5);
        Assert.That(E.CanStart(t, ActionRequest.UseInstance(m2.InstanceId)), Is.Null);
        Assert.That(E.DoInstant(t, InstantRequest.TakeOut(m2.InstanceId, "camp")).Ok, Is.True);
        Assert.That(E.CanStart(t, ActionRequest.UseInstance(m2.InstanceId)), Is.EqualTo("已腐败，不能食用"), "取出先解除保护再判腐败");
    }

    /// <summary>S11-A15/R06-A06：复煮在原期限前、后完成都从完成起 1440 分钟；已变质不可开工；水煮肉再存放不加工不刷新。</summary>
    [Test]
    public void S11_A15_R06_A06_Reboil24hFromCompletion_SpoiledRejected_StoreOnlyNoRefresh()
    {
        // 原期限在复煮途中（第10分钟）：受保护照常完成
        var (s, pot) = PotOnFire("clean_water", 2);
        var meat = E.CreateItems(s, "cooked_meat", 1, "camp").Single();
        meat.ExpiresAt = s.GameMinute + 10;
        Assert.That(E.DoInstant(s, InstantRequest.PutFoodInPot(meat.InstanceId, pot.InstanceId, 1)).Ok, Is.True);
        var m0 = s.GameMinute;
        Wait(s, 15);
        var food = E.PotFood(s, pot.InstanceId).Single();
        Assert.That(food.ItemId, Is.EqualTo("boiled_meat"));
        Assert.That(food.ExpiresAt, Is.EqualTo(m0 + 15 + 1440), "完成起24小时（S11 §8 例：18:10完成→次日18:10）");
        Assert.That(E.ProcessOf(s, food), Is.Null, "完成清保护");

        // 水煮肉再存放：不加工、不耗水、不刷新
        var exp = food.ExpiresAt;
        var water = E.PotUnits(s, pot.InstanceId);
        Wait(s, 30);
        Assert.That(food.ExpiresAt, Is.EqualTo(exp), "水煮肉不循环刷新");
        Assert.That(E.PotUnits(s, pot.InstanceId), Is.EqualTo(water));

        // 已变质烤肉：不可放进锅开工
        var (t, tpot) = PotOnFire("clean_water", 2);
        var bad = E.CreateItems(t, "cooked_meat", 1, "camp").Single();
        bad.ExpiresAt = t.GameMinute - 1;
        Assert.That(E.DoInstant(t, InstantRequest.PutFoodInPot(bad.InstanceId, tpot.InstanceId, 1)).Ok, Is.False);
    }

    /// <summary>S11-A16/S13-A09：等待烹饪按首次受热资格纳入、预览不先保护；保存读档保留保护且不刷新截止。</summary>
    [Test]
    public void S11_A16_S13_A09_CookWaitIncludesProtectable_SaveKeepsProtection()
    {
        var (s, pot) = PotOnFire("clean_water", 2);
        var root = RootInPot(s, pot);
        root.ExpiresAt = s.GameMinute + 10;   // 30 分钟熟成，第10分钟过原期限
        var plan = E.PreviewCookWait(s);
        Assert.That(plan.Ok, Is.True, plan.Reason);
        Assert.That(plan.Targets.Single().TargetInstanceId, Is.EqualTo(root.InstanceId), "不因熟前过原期限排除");
        Assert.That(plan.EstimatedMinutes, Is.EqualTo(30));
        Assert.That(E.ProcessOf(s, root)!.HeatProtected, Is.False, "预览不先设保护");
        Start(s, ActionRequest.WaitCook());
        Steps(s, 3);
        Assert.That(E.ProcessOf(s, root)!.HeatProtected, Is.True);
        var json = SaveSerializer.Serialize(s);
        var back = SaveSerializer.Deserialize(json, Cfg);
        var r2 = back.Items.Single(i => i.InstanceId == root.InstanceId);
        Assert.That(E.ProcessOf(back, r2)!.HeatProtected, Is.True, "读档保留保护");
        Assert.That(r2.ExpiresAt, Is.EqualTo(root.ExpiresAt), "读档不刷新原截止");
        Assert.That(Run(back), Is.EqualTo(SegmentStatus.Completed), "等待按受保护目标走完，不因越过原期限停止");
        Assert.That(E.PotFood(back, pot.InstanceId).Single().ItemId, Is.EqualTo("boiled_root"));
    }

    /// <summary>存档 7→8（开发假设 H6）：已有热进度且新鲜的食材补保护；已腐败的不复活；净水批次不加。</summary>
    [Test]
    public void Migration7To8_ProtectsFreshHeated_NotSpoiled()
    {
        var (s, pot) = PotOnFire("clean_water", 2);
        var fresh = RootInPot(s, pot);
        var spoiled = RootInPot(s, pot);
        Wait(s, 5);
        foreach (var p in s.Locations["camp"].Facility.Processes) p.HeatProtected = false;
        spoiled.ExpiresAt = s.GameMinute - 1;
        var j = JObject.Parse(SaveSerializer.Serialize(s));
        j["SchemaVersion"] = "7";
        var m = SaveSerializer.Deserialize(j.ToString(), Cfg, out var notes);
        Assert.That(m.SchemaVersion, Is.EqualTo("8"));
        var pf = E.ProcessOf(m, m.Items.Single(i => i.InstanceId == fresh.InstanceId))!;
        Assert.That(pf.HeatProtected, Is.True);
        var sp = m.Items.Single(i => i.InstanceId == spoiled.InstanceId);
        Assert.That(E.IsSpoiled(m, sp), Is.True, "已腐败不能迁移复活");
        Assert.That(notes.Any(n => n.Contains("烹饪保护")), Is.True);
    }
}
