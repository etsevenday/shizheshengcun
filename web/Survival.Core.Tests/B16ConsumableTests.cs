using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 B16（R51）：止泻药粉与口服补液共用 6 小时缓解冷却、应急恢复剂战外部分、野外修补包（B16 旧 10% 规则已被 Q168 取代，见 B22bTests）、
/// A07「二十种食品用品」与 X1.2 新食品的食用效果。验收 ES09、ES10（战术效果不在本批）、SUP06、SUP08 中本批能做的部分。
/// </summary>
public class B16ConsumableTests : TestBase
{
    private GameState Give(params (string id, int n)[] give)
    {
        var s = New();
        foreach (var (id, n) in give) E.CreateItems(s, id, n, ItemInstance.Carried);
        return s;
    }

    private void Use(GameState s, string itemId)
    {
        Start(s, ActionRequest.UseInstance(First(s, itemId).InstanceId));
        Run(s);
    }

    [Test]
    public void ES09_Antidiarrheal_ORS_SharedReliefCooldown()
    {
        var s = Give(("antidiarrheal_powder", 2), ("oral_rehydration", 2));
        s.DiarrheaUntil = null;
        Assert.That(E.CanStart(s, ActionRequest.UseInstance(First(s, "antidiarrheal_powder").InstanceId)), Does.Contain("没有腹泻"));
        s.DiarrheaUntil = s.GameMinute + 600;
        var stats = (s.Stats.Health, s.Stats.Satiety, s.Stats.Energy);
        Use(s, "antidiarrheal_powder");
        Assert.That(s.DiarrheaUntil!.Value - s.GameMinute, Is.EqualTo(600 - 120 - 5), "腹泻剩余减 120");
        Assert.That(s.Stats.Health, Is.EqualTo(stats.Health), "不回 H");
        Assert.That(Count(s, "antidiarrheal_powder"), Is.EqualTo(1));
        // 共用 6 小时缓解冷却：药粉与补液都拒绝
        Assert.That(E.CanStart(s, ActionRequest.UseInstance(First(s, "antidiarrheal_powder").InstanceId)), Does.Contain("冷却中"));
        Assert.That(E.CanStart(s, ActionRequest.UseInstance(First(s, "oral_rehydration").InstanceId)), Does.Contain("冷却中"));
        AdvanceTo(s, s.GameMinute + 360);
        s.DiarrheaUntil = s.GameMinute + 300;
        s.Stats.Hydration = 40;
        Use(s, "oral_rehydration");
        Assert.That(s.Stats.Hydration, Is.GreaterThan(74).And.LessThanOrEqualTo(75), "W+35（减 5 分钟消耗）");
        Assert.That(s.DiarrheaUntil!.Value - s.GameMinute, Is.EqualTo(300 - 60 - 5));
        // 水分满且没有腹泻：补液拒绝
        var t = Give(("oral_rehydration", 1));
        t.DiarrheaUntil = null;
        t.Stats.Hydration = t.Stats.Max(StatId.Hydration);
        Assert.That(E.CanStart(t, ActionRequest.UseInstance(First(t, "oral_rehydration").InstanceId)), Does.Contain("水分已满"));
    }

    [Test]
    public void ES09_EmergencyTonic_OutOfBattle_NoCureNoRevive()
    {
        var s = Give(("emergency_tonic", 2));
        s.Stats.Energy = 10;
        s.Stats.Wakefulness = 50;
        s.Stats.Morale = 50;
        s.Body.Infected = true;
        s.Body.VirusLoad = 30;
        Use(s, "emergency_tonic");
        Assert.That(s.Stats.Energy, Is.GreaterThan(11.5).And.LessThanOrEqualTo(12));
        Assert.That(s.Stats.Wakefulness, Is.LessThan(48.1));
        Assert.That(s.Stats.Morale, Is.LessThan(49.1));
        Assert.That(s.Body.Infected, Is.True, "不清感染");
        Assert.That(s.Body.VirusLoad, Is.GreaterThanOrEqualTo(30), "不减 V");
        Assert.That(E.CanStart(s, ActionRequest.UseInstance(First(s, "emergency_tonic").InstanceId)), Does.Contain("给药冷却中"));
        var t = Give(("emergency_tonic", 1));
        t.Stats.Energy = t.Stats.EnergyMax;
        Assert.That(E.CanStart(t, ActionRequest.UseInstance(First(t, "emergency_tonic").InstanceId)), Does.Contain("体力已满"));
    }

    [TestCase("hardtack", 24, -3, 0)]
    [TestCase("expedition_bar", 38, -2, 0)]
    [TestCase("stew_portion", 35, 12, 2)]
    [TestCase("canned_beans", 30, 3, 0)]
    [TestCase("dried_meat", 28, -5, 0)]
    [TestCase("root_porridge", 38, 10, 2)]
    [TestCase("herbal_drink", 0, 25, 2)]
    public void A07_FoodEffects(string id, int n, int w, int m)
    {
        var u = Cfg.Items.Single(i => i.Id == id).Use!;
        Assert.That((u.Satiety, u.Hydration, u.Morale), Is.EqualTo(((double)n, (double)w, (double)m)));
        var s = Give((id, 1));
        s.Stats.Satiety = 30;
        s.Stats.Hydration = 50;
        var before = (s.Stats.Satiety, s.Stats.Hydration);
        Use(s, id);
        Assert.That(s.Stats.Satiety - before.Satiety, Is.EqualTo(n).Within(0.6), "食用 5 分钟");
        Assert.That(s.Stats.Hydration - before.Hydration, Is.EqualTo(w).Within(0.6));
    }

    // 野外修补包 B16 旧用例（整份消耗、恢复 10%、只修非枪金属武器/工具）已被 Q168 取代，新规则见 B22bTests。
}
