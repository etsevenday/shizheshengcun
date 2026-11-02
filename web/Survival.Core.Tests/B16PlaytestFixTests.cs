using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>开发计划 v0.9 B16 线上试玩修复（M351）：锅食谱作废提示、锅内生料显示、已掌握知识的告示、固定等待预估。</summary>
public class B16PlaytestFixTests : TestBase
{
    private void Ok(ActionResult r) => Assert.That(r.Ok, Is.True, r.Reason);

    private (GameState s, ItemInstance pot) Pot()
    {
        var s = New();
        E.DebugLearnAllKnowledge(s);
        s.Skills["cooking"].Level = 5;
        s.Items.RemoveAll(i => i.ItemId is "grain" or "salt" or "clean_water");
        var pot = s.Items.First(i => i.ItemId == "pot");
        E.DebugGive(s, "clean_water", 2, "camp");
        Ok(E.DoInstant(s, InstantRequest.FillPot(First(s, "clean_water", "camp").InstanceId, pot.InstanceId, 2)));
        if (pot.IsCarried) Ok(E.DoInstant(s, InstantRequest.Drop(pot.InstanceId, 1)));
        Ok(E.DoInstant(s, InstantRequest.PutInFire(pot.InstanceId)));
        E.CreateItems(s, "grain", 2, "camp");
        E.CreateItems(s, "salt", 1, "camp");
        Ok(E.DoInstant(s, InstantRequest.LoadPotRecipe("pot_hardtack", pot.InstanceId)));
        return (s, pot);
    }

    [Test]
    public void TakeOut_Recipe_LogAndConsequence_SayAbandonAndAuxReturn()
    {
        var (s, pot) = Pot();
        var grain = E.PotFood(s, pot.InstanceId).First();
        var c = E.TakeOutConsequenceOf(s, grain);
        Assert.That(c.Text, Does.Contain("远行硬饼整份作废").And.Contain("辅料盐×1退回临时营地地面"));
        Assert.That(c.Text, Does.Not.Contain("不损失进度"));
        Assert.That(E.TakeOutConsequenceOf(s, pot).Text, Does.Contain("整份作废"), "受热 0 分钟时取下锅也写作废");
        Ok(E.DoInstant(s, InstantRequest.TakeOut(grain.InstanceId, "camp")));
        Assert.That(s.Log.Last().Text, Does.Contain("远行硬饼整份作废").And.Contain("辅料盐×1退回"));
        // 作废后留在锅里的生谷物不写“已煮熟”
        var left = E.PotFood(s, pot.InstanceId).Single();
        Assert.That(E.ContentStatus(s, left), Does.Contain("未加工").And.Not.Contain("煮熟"));
    }

    [Test]
    public void TakeOut_Pot_LogSaysAbandon()
    {
        var (s, pot) = Pot();
        Ok(E.DoInstant(s, InstantRequest.TakeOut(pot.InstanceId, "camp")));
        Assert.That(s.Log.Last().Text, Does.Contain("整份作废").And.Contain("辅料盐×1退回"));
    }

    [Test]
    public void CeramicNotice_KnownElsewhere_ShowsKnown_NoLearnText()
    {
        var s = New();
        E.DebugMapTeleport(s, "h03_workshop");
        s.Progress.Flags.Add("mining_blueprint_pack");
        var doc = E.DocumentsHere(s).Single(d => d.Id == "h03_ceramic_notice");
        Assert.That(doc.Status, Does.StartWith("已掌握"));
        Assert.That(doc.ReadBlocked, Is.Null, "制造不足 3 级也可查看");
        Ok(E.ReadDocument(s, "h03_ceramic_notice"));
        Assert.That(s.Log.Last().Text, Does.Contain("已掌握").And.Not.Contain("学会做法知识"));
    }

    [Test]
    public void FixedWait_Preview_NoBaseWorkNote()
    {
        var s = New();
        E.DebugLearnAllKnowledge(s);
        E.CreateItems(s, "dirty_water", 4, s.CurrentLocation);
        E.CreateItems(s, "purification_tablet", 1, s.CurrentLocation);
        var p = E.Preview(s, ActionRequest.Craft("ex_tablet_water"));
        Assert.That(p.Notes, Has.None.Contains("基础工作量"));
        Assert.That(p.EstimatedMinutes, Is.EqualTo(20));
        Assert.That(p.IsWork, Is.False);
    }
}
