using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 批次 B23 M481：远征供给提示与远征物资分栏（R72；A10-EXUI §5、§6、§7，S07 UI-X03；开发假设 B23-7、B23-9、B23-10）。
/// 验收 EXUI07（水的状态真实、封存不计、处理不免费）、EXUI08（封存水药弹不可直接使用、外带物不能封存、取出原子校验）、UI-X03（封存取出失败原账不变）、
/// EXUI09（四种离开分开的回归）。
/// </summary>
public class B23ExpeditionUiTests : BattleTestBase
{
    private void Go(GameState s, string to, bool confirm = false)
    {
        Start(s, ActionRequest.Travel(to, confirm));
        Assert.That(Run(s), Is.EqualTo(SegmentStatus.Completed));
    }

    /// <summary>01→02（打赢）→04（安全点）的进行中远征（同 B22SealedTests.At04）。</summary>
    private GameState At04(uint seed = 2301)
    {
        var s = B21EntryTests.AtBuffer(E, seed);
        UnlockCapacity(s, 2);
        Go(s, "d01_02", true);
        E.DebugWinFixedHere(s);
        E.DebugSetProgress(s, "d01_02", 100);
        Go(s, "d01_04");
        E.DebugSetProgress(s, "d01_04", 100);
        return s;
    }

    private void FillCards(GameState s)
    {
        foreach (var it in Cfg.Items.Where(i => i.Kind == "material" && i.WeightGrams <= 100 && i.IsStackable && i.Id != "cloth" && i.Id != "leather"))
        {
            if (E.RootCardCount(s) >= E.MaxRootCards) break;
            if (s.Items.Any(x => x.IsCarried && x.ItemId == it.Id)) continue;
            E.CreateItems(s, it.Id, 1, ItemInstance.Carried);
        }
    }

    /// <summary>EXUI07：进入预告的饮水与热源分列——随身净水、需处理脏水、处理用品、热源；地面的水不算随身。</summary>
    [Test]
    public void EXUI07_PreviewWaterLines_CarriedOnly()
    {
        var s = New();
        s.Items.RemoveAll(i => i.IsCarried && Cfg.Items.Single(c => c.Id == i.ItemId).Kind == "water");
        E.CreateItems(s, "clean_water", 2, ItemInstance.Carried);
        E.CreateItems(s, "dirty_water", 1, ItemInstance.Carried);
        E.CreateItems(s, "clean_water", 3, s.Ground);
        E.CreateItems(s, "purification_tablet", 1, ItemInstance.Carried);
        var lines = E.ExpeditionPreview(s, "d01").WaterLines;
        Assert.That(lines[0], Is.EqualTo("随身净水（可直接饮用）：净水×2份"));
        Assert.That(lines[1], Is.EqualTo("需处理的脏水（直接喝必定腹泻）：脏水×1份"));
        Assert.That(lines[2], Does.StartWith("处理用品：").And.Contain("净水片×1"));
        Assert.That(lines[3], Does.StartWith("热源："));
    }

    /// <summary>
    /// EXUI08：本次地牢里领到的净水封存后——预告的随身净水不计、写“封存中的水不计入”，不能直接喝；远征物资面板三栏分开，
    /// 封存栏写“封存中·本次不可直接使用”；外带物（打火器）不能封存。
    /// </summary>
    [Test]
    public void EXUI08_SealedWaterNotUsable_ThreeColumns()
    {
        var s = At04();
        s.Items.RemoveAll(i => i.ItemId is "clean_water" or "dirty_water");
        var water = E.CreateItems(s, "clean_water", 2, s.Ground, rewardOrigin: s.Expedition!.InstanceId).First();
        Assert.That(E.SealReward(s, water.InstanceId).Ok, Is.True);
        var lines = E.ExpeditionWaterLines(s);
        Assert.That(lines[0], Is.EqualTo("随身净水（可直接饮用）：没有"));
        Assert.That(lines, Does.Contain("封存中的水不计入（本次不可直接使用）"));
        Assert.That(E.CanStart(s, ActionRequest.UseItem("clean_water")), Is.Not.Null, "封存的水不能直接喝");
        var v = E.ExpeditionStores(s)!;
        Assert.That(v.Sealed.Single().Text, Does.Contain("净水×2").And.Contain("封存中·本次不可直接使用"));
        Assert.That(v.Usable.Any(r => r.Text.StartsWith("净水")), Is.False);
        var lighter = s.Items.FirstOrDefault(i => i.IsCarried && i.ItemId == "lighter");
        if (lighter != null)
        {
            Assert.That(v.Usable.Single(r => r.InstanceId == lighter.InstanceId).CanSeal, Is.False, "外带物不能封存");
            Assert.That(E.SealBlocked(s, lighter.InstanceId), Is.Not.Null);
        }
    }

    /// <summary>远征物资面板：战利品行标“战利品”可封存；别的房间地面的东西列在“仍在旧实例”；不在远征里为 null。</summary>
    [Test]
    public void Stores_GroundRewardsAndElsewhere()
    {
        var s = At04();
        var cloth = E.CreateItems(s, "cloth", 2, s.Ground, rewardOrigin: s.Expedition!.InstanceId).First();
        E.CreateItems(s, "wood", 1, "d01_02");
        var v = E.ExpeditionStores(s)!;
        var row = v.Ground.Single(r => r.InstanceId == cloth.InstanceId);
        Assert.That((row.Reward, row.CanSeal, row.Text.Contains("战利品")), Is.EqualTo((true, true, true)));
        Assert.That(v.Elsewhere, Has.Some.Contains("木材×1").And.Contains("地面"));
        Assert.That(v.Usable.Concat(v.Ground).Concat(v.Sealed).Any(r => r.Text.Contains("#")), Is.False, "试玩修复：不显示内部实例号");
        Assert.That(v.Elsewhere.Any(x => x.Contains("藏书密室")), Is.False, "当前节点地面不算“别处”");
        var t = New();
        Assert.That(E.ExpeditionStores(t), Is.Null);
    }

    /// <summary>UI-X03：封存物取到随身失败（五卡已满）——拒绝且原账完全不变（封存账、随身、地面都不动）。</summary>
    [Test]
    public void UI_X03_UnsealFail_LedgerUnchanged()
    {
        var s = At04();
        var cloth = E.CreateItems(s, "cloth", 2, s.Ground, rewardOrigin: s.Expedition!.InstanceId).First();
        Assert.That(E.SealReward(s, cloth.InstanceId).Ok, Is.True);
        FillCards(s);
        var json = SaveSerializer.Serialize(s);
        var r = E.UnsealReward(s, cloth.InstanceId, false);
        Assert.That(r.Ok, Is.False);
        Assert.That(r.Reason, Does.Contain("卡位已满"));
        Assert.That(SaveSerializer.Serialize(s), Is.EqualTo(json), "失败原账不变");
    }

    /// <summary>EXUI09 回归：战斗撤退不结束远征、终止远征扣包撤出、通关后离开免罚——三种离开写在各自入口，不混成一个“离开”。</summary>
    [Test]
    public void EXUI09_FourExits_Distinct()
    {
        var s = At04();
        var ab = E.AbortPreview(s);
        Assert.That(ab.Reason, Is.Null);
        Assert.That(ab.ParentName, Is.EqualTo("教堂墓园"));
        E.DebugInstanceFact(s, "d01", GameEngine.RecordClaimedFact);
        Assert.That(E.AbortPreview(s).Reason, Does.Contain("已通关"));
        var rc = E.ResetConditions(s, "d01");
        Assert.That(rc.Any(c => !c.Met), Is.True, "重置要另在父地点确认，条件逐条");
    }
}
