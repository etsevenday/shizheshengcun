using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 批次 B23 M480：来源查询与配方状态（R72；A10-EXUI §3 四类账、§4 配方可达与缺料查询，开发假设 B23-6）。
/// 验收 EXUI04（有限/日补/持续/本实例/本局唯一分清，件数与补量、重入不刷新）、EXUI05（未知不列、已耗尽/已领取/未重置不亮“可获取”、概率来源不冒充保底）。
/// </summary>
public class B23SourceTests : TestBase
{
    private GameState Fresh()
    {
        var s = New();
        s.GameMinute = At(2, 9, 0);
        s.SpawnNextCheck = int.MaxValue;
        s.PatrolNextCheck = int.MaxValue;
        return s;
    }

    private static void Discover(GameState s, params string[] ids)
    {
        foreach (var id in ids) s.Resources[id].Discovered = true;
    }

    /// <summary>EXUI04：五类账短标与详情——日补（库存+每日补量）、有限（不补）、持续（源头无上限、仍要时间）、本实例（本代剩余）、本局一次（已领取）。</summary>
    [Test]
    public void EXUI04_AccountLabelsAndDetails()
    {
        var s = Fresh();
        Discover(s, "w11_orchard_a", "w34_depot_a", "w12_canal_water", "d01_04_cache0", "d01_08_unique");
        var roots = E.ItemSources(s, "raw_root").Single(v => v.Title.Contains(E.Config.Resources.Single(r => r.NodeId == "w11_orchard_a").Name));
        Assert.That((roots.Kind, roots.Account), Is.EqualTo(("resource", "每日补充")));
        Assert.That(roots.Detail, Does.Contain("每次固定×2").And.Contain("每日00:00补4"));
        var beans = E.ItemSources(s, "canned_beans").Single(v => v.Kind == "resource");
        Assert.That(beans.Account, Is.EqualTo("有限"));
        Assert.That(beans.Detail, Does.Contain("6/6·不补"));
        var water = E.ItemSources(s, "dirty_water").Single(v => v.LocationId == "w12_canal");
        Assert.That((water.Account, water.Detail.Contains("源头无库存上限（采集仍要时间）")), Is.EqualTo(("持续", true)));
        var cloth = E.ItemSources(s, "cloth").Single(v => v.LocationId == "d01_04" && v.Kind == "resource");
        Assert.That((cloth.Account, cloth.Status, cloth.Usable || cloth.Status.Contains("路线")), Is.EqualTo(("本实例", cloth.Status, true)));
        Assert.That(cloth.Detail, Does.Contain("本代剩余2/2"));
        s.Resources["d01_04_cache0"].Available = 0;
        cloth = E.ItemSources(s, "cloth").Single(v => v.LocationId == "d01_04" && v.Kind == "resource");
        Assert.That((cloth.Usable, cloth.Status), Is.EqualTo((false, "本代已搜空（地牢通关满72世界小时、在父地点确认重置后的新一代恢复）")));
    }

    /// <summary>EXUI05：未发现的资源点不列；有限来源耗尽写“已耗尽”、本局一次已领写“已领取”，都不亮可获取；概率来源一行写“不是保底”；只读不改存档。</summary>
    [Test]
    public void EXUI05_UnknownHidden_ExhaustedClaimed_DropsNotGuaranteed_ReadOnly()
    {
        var s = Fresh();
        Assert.That(E.ItemSources(s, "canned_beans").Any(v => v.Kind == "resource"), Is.False, "未发现不列");
        Discover(s, "w34_depot_a");
        s.Resources["w34_depot_a"].Available = 0;
        var beans = E.ItemSources(s, "canned_beans").Single(v => v.Kind == "resource");
        Assert.That((beans.Usable, beans.Status), Is.EqualTo((false, "已耗尽（不会补充）")));
        Discover(s, "m02_checkpoint_duty");
        s.Progress.Flags.Add(GameEngine.CampaignKey("m02_checkpoint_duty"));
        var duty = E.ItemSources(s, "canned_food").Single(v => v.LocationId == "m02_checkpoint");
        Assert.That((duty.Account, duty.Usable, duty.Status), Is.EqualTo(("本局一次", false, "已领取（本局一次，不再发放）")));
        var drop = E.ItemSources(s, "cloth").Single(v => v.Kind == "drop");
        Assert.That(drop.Detail, Does.Contain("击败感染者后按概率掉落（30%）"));
        Assert.That((drop.Usable, drop.Status), Is.EqualTo((false, "概率，不是保底")));
        var json = SaveSerializer.Serialize(s);
        E.ItemSources(s, "cloth");
        E.RecipeStatus(s, E.Recipe("make_repair_manual"));
        Assert.That(SaveSerializer.Serialize(s), Is.EqualTo(json), "只读");
    }

    /// <summary>A10 §4 配方状态逐类：知识未得、熟练不足、设施未到、材料缺口（带物品）分别列出；蓝图已得不等于等级满足。</summary>
    [Test]
    public void RecipeStatus_Categories()
    {
        var s = Fresh();
        var rc = E.Recipe("make_repair_manual");
        var st = E.RecipeStatus(s, rc);
        Assert.That(st.Select(x => x.Category), Does.Contain("知识未得").And.Contain("熟练不足").And.Contain("材料缺口"));
        Assert.That(st.Where(x => x.Category == "材料缺口").Select(x => x.ItemId), Is.EquivalentTo(new[] { "cloth", "parts" }.Where(id => E.AvailableCount(s, id, true) < 1)));
        s.Progress.Flags.Add("claim:d05_07_unique");
        st = E.RecipeStatus(s, rc);
        Assert.That(st.Any(x => x.Category == "知识未得"), Is.False);
        Assert.That(st.Any(x => x.Category == "熟练不足" && x.Text.Contains("制造7级")), Is.True, "蓝图已得不代表等级满足");
    }

    /// <summary>试玩修复：有库存但没有已开通路线的来源不写“可获取”，原因只写一次（状态写原因、路线栏留空）。</summary>
    [Test]
    public void NoRoute_NotAvailable_ReasonOnce()
    {
        var s = Fresh();
        Discover(s, "w34_depot_a");
        var v = E.ItemSources(s, "canned_beans").Single(x => x.Kind == "resource");
        Assert.That((v.Usable, v.Status, v.Route), Is.EqualTo((false, "现在没有已开通的路线", "")));
    }

    /// <summary>已掌握配方与火堆加工也作来源：净水有“火堆加工：烧水”。</summary>
    [Test]
    public void RecipeAndProcessSources()
    {
        var s = Fresh();
        Assert.That(E.ItemSources(s, "clean_water").Any(v => v.Kind == "process" && v.Title.Contains("烧水")), Is.True);
        Assert.That(E.ItemSources(s, "stone_knife").Any(v => v.Kind == "recipe"), Is.True);
    }
}
