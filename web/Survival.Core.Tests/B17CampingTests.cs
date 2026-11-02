using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 B17（R56）：野营遮雨布与折叠柴炉（A07 X1.6、S11 X4、operationalRules.camping）。验收 ES07、SUP06（部署不藏物）。
/// </summary>
public class B17CampingTests : TestBase
{
    private void Ok(ActionResult r) => Assert.That(r.Ok, Is.True, r.Reason);

    private GameState At(string loc)
    {
        var s = New();
        E.DebugTeleport(s, loc);
        s.WorldEnemies.RemoveAll(w => w.LocationId == loc);
        return s;
    }

    [Test]
    public void ES07_Stove_DeployFuelCapIgniteAndPack()
    {
        var s = At("w19_survey");
        Assert.That(E.HasFireAt(s, s.Ground), Is.False);
        var stove = E.CreateItems(s, "folding_stove", 1, ItemInstance.Carried).Single();
        // 非许可点不能部署
        var c = New();
        var cs = E.CreateItems(c, "folding_stove", 1, ItemInstance.Carried).Single();
        Assert.That(E.CanStart(c, GameEngine.Deploy(cs)), Does.Contain("临时露营许可点"));

        var t0 = s.GameMinute;
        Start(s, GameEngine.Deploy(stove));
        Run(s);
        Assert.That(s.GameMinute - t0, Is.EqualTo(5));
        Assert.That(E.HasFireAt(s, s.Ground), Is.True);
        Assert.That((stove.Owner, E.IsDeployed(s, stove)), Is.EqualTo(("w19_survey", true)), "部署是真实转到当地地面");
        var fac = s.Locations["w19_survey"].Facility;
        Assert.That((fac.FacilityId, fac.FireLit, fac.FuelMinutes, E.FuelCapOf(fac)), Is.EqualTo(("w19_survey:fire", false, 0, 60)));
        Assert.That(E.DoInstant(s, InstantRequest.Pickup(stove.InstanceId, 1)).Reason, Does.Contain("先收起"));
        Assert.That(E.CanStart(s, GameEngine.Deploy(E.CreateItems(s, "folding_stove", 1, ItemInstance.Carried).Single())), Does.Contain("已经部署"));

        // 燃料上限 60：投 2 份木材拒绝，1 份可以
        s.Items.RemoveAll(i => i.ItemId == "wood");
        E.CreateItems(s, "wood", 3, ItemInstance.Carried);
        Assert.That(E.DoInstant(s, InstantRequest.AddFuel(2)).Ok, Is.False);
        Ok(E.DoInstant(s, InstantRequest.AddFuel(1)));
        Assert.That(fac.FuelMinutes, Is.EqualTo(60));
        // 点火 5 分钟耗燃料、不加热
        var lighter = E.IgniteToolOptions(s).FirstOrDefault(o => o.Usable);
        if (lighter == null) { E.CreateItems(s, "lighter", 1, ItemInstance.Carried); lighter = E.IgniteToolOptions(s).First(o => o.Usable); }
        Start(s, ActionRequest.Ignite(lighter.InstanceId));
        Run(s);
        Assert.That((fac.FireLit, fac.FuelMinutes), Is.EqualTo((true, 55)));

        // 收起：燃烧中/有锅都不行；熄火后剩燃料要明确放弃
        var pot = E.CreateItems(s, "pot", 1, ItemInstance.Carried).Single();
        Ok(E.DoInstant(s, InstantRequest.PutInFire(pot.InstanceId)));
        Assert.That(E.CanStart(s, GameEngine.Pack(stove)), Does.Contain("取出"));
        Ok(E.DoInstant(s, InstantRequest.TakeOut(pot.InstanceId, s.Ground)));
        Assert.That(E.CanStart(s, GameEngine.Pack(stove)), Does.Contain("熄火"));
        Ok(E.DoInstant(s, InstantRequest.Extinguish()));
        Assert.That(E.CanStart(s, GameEngine.Pack(stove)), Does.Contain("放弃"));
        var wood = s.Items.Where(i => i.ItemId == "wood").Sum(i => i.Count);
        Start(s, GameEngine.Pack(stove, abandonFuel: true));
        Run(s);
        Assert.That(E.HasFireAt(s, s.Ground), Is.False);
        Assert.That((E.IsDeployed(s, stove), fac.FuelMinutes, fac.StoveInstanceId), Is.EqualTo((false, 0, (int?)null)));
        Assert.That(s.Items.Where(i => i.ItemId == "wood").Sum(i => i.Count), Is.EqualTo(wood), "放弃燃料不退木材");
        s.Items.RemoveAll(i => i.IsCarried && i.ItemId != "folding_stove");
        Ok(E.DoInstant(s, InstantRequest.Pickup(stove.InstanceId, 1)));
    }

    [Test]
    public void ES07_StoveSurvivesSaveLoad_BurnsWithWorldClock()
    {
        var s = At("w21_pine");
        var stove = E.CreateItems(s, "folding_stove", 1, ItemInstance.Carried).Single();
        Start(s, GameEngine.Deploy(stove));
        Run(s);
        var fac = s.Locations["w21_pine"].Facility;
        fac.FireLit = true;
        fac.FuelMinutes = 60;
        var t = SaveSerializer.Deserialize(SaveSerializer.Serialize(s), Cfg);
        AdvanceTo(t, t.GameMinute + 60);
        var tf = t.Locations["w21_pine"].Facility;
        Assert.That((tf.FireLit, tf.FuelMinutes, tf.StoveInstanceId), Is.EqualTo((false, 0, (int?)stove.InstanceId)), "耗尽熄火，柴炉仍部署");
    }

    [Test]
    public void Tarp_SleepShelterOnly_NoRestBonus_PackBack()
    {
        var s = At("w23_hunting");
        var tarp = E.CreateItems(s, "field_tarp", 1, ItemInstance.Carried).Single();
        var sleep = new ActionState { Type = ActionType.Sleep };
        var rest = new ActionState { Type = ActionType.Rest };
        SetStats(s, 15, 80, 80, 10, 50, 60);
        var exposedSleep = E.ComputeSegment(s, sleep, s.Player);
        var restBefore = E.ComputeSegment(s, rest, s.Player);
        Assert.That(exposedSleep.Sheltered, Is.False);
        var t0 = s.GameMinute;
        Start(s, GameEngine.Deploy(tarp));
        Run(s);
        Assert.That(s.GameMinute - t0, Is.EqualTo(10));
        SetStats(s, 15, 80, 80, 10, 50, 60);
        var tarpSleep = E.ComputeSegment(s, sleep, s.Player);
        Assert.That(tarpSleep.Sheltered, Is.True, "铺开后睡眠算遮蔽");
        var restAfter = E.ComputeSegment(s, rest, s.Player);
        Assert.That((restAfter.Sheltered, restAfter.Rates.Morale), Is.EqualTo((false, restBefore.Rates.Morale)), "不给休息遮蔽与 M+2");
        Assert.That(E.IsSheltered(s, s.Ground), Is.False);
        // 收起 10 分钟，回到露天
        Start(s, GameEngine.Pack(tarp));
        Run(s);
        Assert.That(E.ComputeSegment(s, sleep, s.Player).Sheltered, Is.False);
        Ok(E.DoInstant(s, InstantRequest.Pickup(tarp.InstanceId, 1)));
        // 非许可点（普通野外）不能铺
        E.DebugTeleport(s, "field");
        Assert.That(E.CanStart(s, GameEngine.Deploy(tarp)), Does.Contain("临时露营许可点"));
    }
}
