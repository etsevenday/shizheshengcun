using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 B16（R50）：复合锅食谱（S10 X4、A07 X1.5、S11 X4、R06 X4）——硬饼 grain×2 占两位＋盐辅料不占位＋锅水 500 一次扣、
/// 336 小时保质；谷肉炖食 grain1+raw_meat1 占两位＋1000 毫升、出两份、24 小时；多输入进程带辅料预留；取出/取下作废返辅料；
/// 熄火保进度；读档不重复；生肉实际受热才保护。验收 ES06、SUP04、SUP05、POT01—POT05、CO01—CO05 中本批能做的部分。
/// </summary>
public class B16PotRecipeTests : TestBase
{
    private const string Here = "camp";

    private void Ok(ActionResult r) => Assert.That(r.Ok, Is.True, r.Reason);

    /// <summary>营地、火堆燃烧、锅放进火堆并装 water 份净水；学会全部知识、烹饪 5 级；谷物/盐/生肉按 give 放地面。</summary>
    private (GameState s, ItemInstance pot) Setup(int water, params (string id, int n)[] give)
    {
        var s = New();
        E.DebugLearnAllKnowledge(s);
        s.Skills["cooking"].Level = 5;
        s.Items.RemoveAll(i => i.ItemId is "grain" or "salt" or "raw_meat" or "clean_water" or "dirty_water");
        var pot = s.Items.First(i => i.ItemId == "pot");
        if (water > 0)
        {
            E.DebugGive(s, "clean_water", water, Here);
            Ok(E.DoInstant(s, InstantRequest.FillPot(First(s, "clean_water", Here).InstanceId, pot.InstanceId, water)));
        }
        if (pot.IsCarried) Ok(E.DoInstant(s, InstantRequest.Drop(pot.InstanceId, 1)));
        Ok(E.DoInstant(s, InstantRequest.PutInFire(pot.InstanceId)));
        var f = s.Locations[Here].Facility;
        f.FireLit = true;
        f.FuelMinutes = 240;
        foreach (var (id, n) in give) E.CreateItems(s, id, n, Here);
        return (s, pot);
    }

    private FacilityProcess? Recipe(GameState s) => E.AllProcesses(s).FirstOrDefault(p => p.InputInstanceIds != null);

    private void WaitMinutes(GameState s, int minutes)
    {
        var end = s.GameMinute + minutes;
        while (s.GameMinute < end)
        {
            Start(s, ActionRequest.Wait(30));
            Run(s);
        }
    }

    [Test]
    public void ES06_Hardtack_TwoGrainTwoSlots_SaltAuxNoSlot_Water500Once()
    {
        var (s, pot) = Setup(2, ("grain", 3), ("salt", 2));
        var opt = E.PotRecipeOptions(s, pot).Single(o => o.ProcessId == "pot_hardtack");
        Assert.That(opt.Ok, Is.True, opt.Reason);
        Assert.That(opt.InputText, Does.Contain("占2个食材位").And.Contain("辅料，不占位").And.Contain("500毫升"));
        var g0 = E.ItemGrams(s, pot);
        Ok(E.DoInstant(s, InstantRequest.LoadPotRecipe("pot_hardtack", pot.InstanceId)));
        Assert.That(E.PotFood(s, pot.InstanceId).Select(i => i.ItemId), Is.EqualTo(new[] { "grain", "grain" }), "POT01：两粮占两位");
        Assert.That(E.PotContents(s, pot.InstanceId).Count(i => i.ItemId == "salt"), Is.EqualTo(1), "POT02：盐锁进锅里，重量计一次");
        Assert.That(E.ItemGrams(s, pot), Is.EqualTo(g0 + 300 * 2 + 100));
        Assert.That(Count(s, "grain", Here), Is.EqualTo(1));
        Assert.That(Count(s, "salt", Here), Is.EqualTo(1));
        var p = Recipe(s)!;
        Assert.That(p.ReservedWaterMl, Is.EqualTo(500));
        Assert.That(E.PotFreeWaterUnits(s, pot), Is.EqualTo(1), "SUP04：只预留500，另500可用");
        // 第三食材不能放（食材位满）
        E.DebugGive(s, "raw_root", 1, Here);
        Assert.That(E.MaxPutFood(s, First(s, "raw_root", Here), pot, out _), Is.EqualTo(0));

        var xp0 = s.Skills["cooking"].Xp;
        WaitMinutes(s, 60);
        var foods = E.PotFood(s, pot.InstanceId);
        Assert.That(foods.Select(i => i.ItemId), Is.EqualTo(new[] { "hardtack", "hardtack" }));
        Assert.That(E.PotContents(s, pot.InstanceId).Any(i => i.ItemId is "grain" or "salt"), Is.False, "主食材与辅料一次核销");
        Assert.That(E.PotUnits(s, pot.InstanceId), Is.EqualTo(1), "POT03：锅水只扣一次 500");
        Assert.That(foods.All(f => f.ExpiresAt > s.GameMinute + 335 * 60 && f.ExpiresAt <= s.GameMinute + 336 * 60), Is.True, "完成起 336 小时");
        Assert.That(s.Skills["cooking"].Xp - xp0, Is.EqualTo(6), "Q60：clamp(ceil(60/10)) 给投料人一次");
        Assert.That(Recipe(s), Is.Null);
        // 硬饼可以吃
        Assert.That(E.CanStart(s, ActionRequest.UseInstance(foods[0].InstanceId)), Is.Null);
    }

    [Test]
    public void POT_Stew_GrainMeat_1000ml_TwoPortions_ProtectionAndSpoil()
    {
        var (s, pot) = Setup(2, ("grain", 1), ("raw_meat", 1));
        var meat = First(s, "raw_meat", Here);
        meat.ExpiresAt = s.GameMinute + 20;   // 投料后 20 分钟到原期限：实际受热后受保护
        Ok(E.DoInstant(s, InstantRequest.LoadPotRecipe("pot_stew", pot.InstanceId)));
        Assert.That(Recipe(s)!.ReservedWaterMl, Is.EqualTo(1000));
        Assert.That(E.PotFreeWaterUnits(s, pot), Is.EqualTo(0));
        Assert.That(E.AllProcesses(s).Count(), Is.EqualTo(1), "生肉不再另起水煮进程");
        WaitMinutes(s, 60);
        var foods = E.PotFood(s, pot.InstanceId);
        Assert.That(foods.Select(i => i.ItemId), Is.EqualTo(new[] { "stew_portion", "stew_portion" }));
        Assert.That(E.PotUnits(s, pot.InstanceId), Is.EqualTo(0), "完成水归 0");

        // 原料受热前就腐败：不加工
        var (t, pot2) = Setup(2, ("grain", 1), ("raw_meat", 1));
        t.Locations[Here].Facility.FireLit = false;
        First(t, "raw_meat", Here).ExpiresAt = t.GameMinute + 5;
        Ok(E.DoInstant(t, InstantRequest.LoadPotRecipe("pot_stew", pot2.InstanceId)));
        WaitMinutes(t, 30);
        t.Locations[Here].Facility.FireLit = true;
        WaitMinutes(t, 60);
        Assert.That(E.PotFood(t, pot2.InstanceId).Any(i => i.ItemId == "stew_portion"), Is.False, "SUP05：坏料不复活");
        Assert.That(E.ContentStatus(t, E.PotFood(t, pot2.InstanceId).First()), Does.Contain("腐败"));
    }

    [Test]
    public void CO_Rejections_ZeroChange()
    {
        var (s, pot) = Setup(0, ("grain", 2), ("salt", 1));
        string Why(string pid) => E.PotRecipeOptions(s, pot).Single(o => o.ProcessId == pid).Reason ?? "";
        Assert.That(Why("pot_hardtack"), Does.Contain("锅里没有水"));
        E.DebugGive(s, "dirty_water", 2, Here);
        // 锅在火堆里不能直接装？用开发给水：先取下装水再放回
        Ok(E.DoInstant(s, InstantRequest.TakeOut(pot.InstanceId, Here)));
        Ok(E.DoInstant(s, InstantRequest.FillPot(First(s, "dirty_water", Here).InstanceId, pot.InstanceId, 1)));
        Ok(E.DoInstant(s, InstantRequest.PutInFire(pot.InstanceId)));
        Assert.That(Why("pot_hardtack"), Does.Contain("先在火堆上烧成"));
        Assert.That(Why("pot_stew"), Does.Contain("锅里是脏水"));
        var before = SaveSerializer.Serialize(s);
        Assert.That(E.DoInstant(s, InstantRequest.LoadPotRecipe("pot_hardtack", pot.InstanceId)).Ok, Is.False);
        Assert.That(SaveSerializer.Serialize(s), Is.EqualTo(before), "失败零改动");

        var (u, pot3) = Setup(1, ("grain", 2), ("raw_meat", 1));
        Assert.That(E.PotRecipeOptions(u, pot3).Single(o => o.ProcessId == "pot_stew").Reason, Does.Contain("可用净水500/1000"), "缺水拒绝，不从随身补");
        Assert.That(E.PotRecipeOptions(u, pot3).Single(o => o.ProcessId == "pot_hardtack").Reason, Does.Contain("缺少盐"));
        var (v, pot4) = Setup(2, ("grain", 2), ("salt", 1));
        v.Skills["cooking"].Level = 2;
        Assert.That(E.PotRecipeOptions(v, pot4).Single(o => o.ProcessId == "pot_hardtack").Reason, Does.Contain("烹饪3级"));
        var w = New();
        Assert.That(E.CanStart(w, ActionRequest.Craft("ex_hardtack")), Does.Contain("锅卡"));
    }

    [Test]
    public void CO_TakeOutCancels_ReturnsAux_FireOutKeepsProgress_SaveNoDup()
    {
        var (s, pot) = Setup(2, ("grain", 2), ("salt", 1));
        Ok(E.DoInstant(s, InstantRequest.LoadPotRecipe("pot_hardtack", pot.InstanceId)));
        WaitMinutes(s, 30);
        var p = Recipe(s)!;
        Assert.That(p.HeatDoneMinutes, Is.EqualTo(30));
        // 熄火保进度
        s.Locations[Here].Facility.FireLit = false;
        WaitMinutes(s, 30);
        Assert.That(Recipe(s)!.HeatDoneMinutes, Is.EqualTo(30));
        // 读档后继续，完成一次
        var t = SaveSerializer.Deserialize(SaveSerializer.Serialize(s), Cfg);
        t.Locations[Here].Facility.FireLit = true;
        t.Locations[Here].Facility.FuelMinutes = 240;
        WaitMinutes(t, 30);
        var tPot = t.Items.Single(i => i.InstanceId == pot.InstanceId);
        Assert.That(E.PotFood(t, tPot.InstanceId).Count(i => i.ItemId == "hardtack"), Is.EqualTo(2));
        WaitMinutes(t, 60);
        Assert.That(t.Items.Where(i => i.ItemId == "hardtack").Sum(i => i.Count), Is.EqualTo(2), "不重复产出");

        // 取出一份谷物：整份作废，盐退回地面，进度清零
        var grain = E.PotFood(s, pot.InstanceId).First();
        var cons = E.TakeOutConsequenceOf(s, grain);
        Assert.That(cons.Text, Does.Contain("整份作废").And.Contain("辅料退回"));
        Ok(E.DoInstant(s, InstantRequest.TakeOut(grain.InstanceId, Here)));
        Assert.That(Recipe(s), Is.Null);
        Assert.That(Count(s, "salt", Here), Is.EqualTo(1));
        Assert.That(E.PotFood(s, pot.InstanceId).Select(i => i.ItemId), Is.EqualTo(new[] { "grain" }), "另一份谷物留在锅里");
        Assert.That(E.PotFreeWaterUnits(s, pot), Is.EqualTo(2), "预留水释放");

        // 取下整锅：作废、辅料退回火堆地面
        var (u, pot2) = Setup(2, ("grain", 2), ("salt", 1));
        Ok(E.DoInstant(u, InstantRequest.LoadPotRecipe("pot_hardtack", pot2.InstanceId)));
        Ok(E.DoInstant(u, InstantRequest.TakeOut(pot2.InstanceId, Here)));
        Assert.That(Recipe(u), Is.Null);
        Assert.That(Count(u, "salt", Here), Is.EqualTo(1));
    }
}
