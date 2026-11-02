using Survival.Core.Config;
using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 B16（R48）：配方框架三种模式、19 条新配方、知识 expedition_basics / ceramic_methods、按实例预留完成只出一次、
/// 口粮棒/恢复剂保质不晚于最早原料截止。验收 A07 SUP02、SUP03、ES03、S08 CR01—CR07 中本批能做的部分。
/// </summary>
public class B16RecipeTests : TestBase
{
    private static RecipeConfig Rc(string id) => Cfg.Recipes.Single(r => r.Id == id);

    /// <summary>营地、学会全部知识、建好全部工坊、制造/医疗/烹饪 5 级；清掉相关材料后按 give 放到当前地面。</summary>
    private GameState Camp(params (string id, int n)[] give)
    {
        var s = New();
        E.DebugLearnAllKnowledge(s);
        E.DebugBuildAllWorkshops(s);
        foreach (var k in new[] { "crafting", "medicine", "cooking" }) s.Skills[k].Level = 5;
        var ids = give.Select(g => g.id).Concat(new[] { "dirty_water", "clean_water" }).ToHashSet();
        s.Items.RemoveAll(i => ids.Contains(i.ItemId));
        foreach (var (id, n) in give) E.CreateItems(s, id, n, s.CurrentLocation);
        return s;
    }

    [Test]
    public void SUP02_NineteenRecipes_ModesStationsKnowledge()
    {
        var ex = Cfg.Recipes.Where(r => r.Id.StartsWith("ex_")).ToList();
        Assert.That(ex, Has.Count.EqualTo(19));
        Assert.That(ex.Where(r => r.IsFixedWait).Select(r => r.Id), Is.EquivalentTo(new[] { "ex_tablet_water", "ex_cartridge_water" }));
        Assert.That(ex.Where(r => r.IsPassive).Select(r => r.Id), Is.EquivalentTo(new[] { "ex_ceramic_shell", "ex_hardtack", "ex_stew_portion", "ex_station_water" }));
        Assert.That(ex.Count(r => r.ModeOrDefault == RecipeConfig.ModeActive), Is.EqualTo(13));
        Assert.That(ex.Where(r => r.IsPot).Select(r => r.Id), Is.EquivalentTo(new[] { "ex_hardtack", "ex_stew_portion" }));
        Assert.That(ex.Where(r => r.Knowledge == "ceramic_methods").Select(r => r.Id), Is.EquivalentTo(new[] { "ex_ceramic_shell", "ex_filter_cartridge", "ex_station_water" }));
        Assert.That(ex.Where(r => r.Knowledge != "ceramic_methods").All(r => r.Knowledge == "expedition_basics"), Is.True);
        Assert.That(Rc("ex_ceramic_shell").Fuel["charcoal"], Is.EqualTo(1));
        Assert.That(Rc("ex_cartridge_water").Catalysts, Is.EqualTo(new[] { "water_filter" }));
        Assert.That(Rc("ex_expedition_bar").ShelfRule, Is.EqualTo("min_inputs"));
        Assert.That(Rc("ex_emergency_tonic").ShelfRule, Is.EqualTo("min_inputs"));
        Assert.That(Rc("ex_purification_tablet").XpSkill, Is.EqualTo("medicine"));
        Assert.That(Rc("ex_expedition_bar").XpSkill, Is.EqualTo("cooking"));
        // 旧新手配方不变（SUP02/CR03）
        Assert.That(Rc("make_wooden_spear").Inputs, Is.EqualTo(new Dictionary<string, int> { ["wood"] = 2, ["stone"] = 1, ["fiber"] = 2 }));
    }

    /// <summary>ES03/CR：缺知识、缺工具、缺材料一律拒绝且零扣；知识不在时写来源。</summary>
    [Test]
    public void ES03_MissingKnowledgeToolOrMaterial_RejectZeroCost()
    {
        var s = New();
        s.Items.RemoveAll(i => i.ItemId is "dirty_water" or "purification_tablet");
        E.CreateItems(s, "dirty_water", 4, s.CurrentLocation);
        E.CreateItems(s, "purification_tablet", 1, s.CurrentLocation);
        Assert.That(E.CanStart(s, ActionRequest.Craft("ex_tablet_water")), Does.Contain("远行基础做法"));
        s.Progress.Flags.Add("h03_notice_read");
        Assert.That(E.CanStart(s, ActionRequest.Craft("ex_tablet_water")), Is.Null);

        var t = Camp(("dirty_water", 4), ("filter_cartridge", 1));
        t.Items.RemoveAll(i => i.ItemId == "water_filter");
        Assert.That(E.CanStart(t, ActionRequest.Craft("ex_cartridge_water")), Does.Contain("便携滤水器"));
        var u = Camp(("dirty_water", 3), ("purification_tablet", 1));
        var before = u.Items.Sum(i => i.Count);
        Assert.That(E.Start(u, ActionRequest.Craft("ex_tablet_water")).Ok, Is.False);
        Assert.That(u.Items.Sum(i => i.Count), Is.EqualTo(before));
        Assert.That(u.Main, Is.Null);
    }

    /// <summary>SUP03/ES04 前半：净水片 fixed_wait 固定 20 分钟（制造 10 级也不加速）、全队按等待、不给经验；锁 4 份脏水+1 片，完成一次 4 份净水落当前地面。</summary>
    [Test]
    public void SUP03_TabletWater_FixedWait20_NoSpeedNoXp()
    {
        var s = Camp(("dirty_water", 4), ("purification_tablet", 2));
        s.Skills["crafting"].Level = 10;
        var xp = s.Skills.ToDictionary(k => k.Key, k => k.Value.Xp + k.Value.Level * 100000L);
        var pv = E.Preview(s, ActionRequest.Craft("ex_tablet_water"));
        Assert.That(pv.EstimatedMinutes, Is.EqualTo(20));
        Assert.That(pv.XpGains, Has.Some.Contains("固定等待不给经验"));
        var t0 = s.GameMinute;
        Start(s, ActionRequest.Craft("ex_tablet_water"));
        Assert.That(s.Main!.FixedWait, Is.True);
        Assert.That(s.Main.ReservedItemIds, Has.Count.GreaterThanOrEqualTo(2));
        Assert.That(s.Items.Where(i => i.ItemId == "dirty_water" && i.ReservedBy != null).Sum(i => i.Count), Is.EqualTo(4));
        Run(s);
        Assert.That(s.GameMinute - t0, Is.EqualTo(20));
        Assert.That(Count(s, "dirty_water"), Is.EqualTo(0));
        Assert.That(Count(s, "clean_water", s.CurrentLocation), Is.EqualTo(4));
        Assert.That(Count(s, "purification_tablet"), Is.EqualTo(1));
        Assert.That(s.Skills.ToDictionary(k => k.Key, k => k.Value.Xp + k.Value.Level * 100000L), Is.EqualTo(xp));
        Assert.That(E.CanStart(s, ActionRequest.Craft("ex_tablet_water", null, null, 2)), Does.Contain("每次只做1批"));
    }

    /// <summary>SUP06/ES04：中途取消无产物，释放原水/耗材（返当前地面），时间不退；滤芯净水另需滤水器、不消耗工具。</summary>
    [Test]
    public void SUP06_CartridgeWater_CancelReturns_ToolNotConsumed()
    {
        var s = Camp(("dirty_water", 4), ("filter_cartridge", 1), ("water_filter", 1));
        Start(s, ActionRequest.Craft("ex_cartridge_water"));
        Steps(s, 2);
        Assert.That(E.Cancel(s).Ok, Is.True);
        Assert.That(Count(s, "clean_water"), Is.EqualTo(0));
        Assert.That(Count(s, "dirty_water"), Is.EqualTo(4));
        Assert.That(Count(s, "filter_cartridge"), Is.EqualTo(1));
        Start(s, ActionRequest.Craft("ex_cartridge_water"));
        Run(s);
        Assert.That(Count(s, "clean_water"), Is.EqualTo(4));
        Assert.That(Count(s, "filter_cartridge"), Is.EqualTo(0));
        Assert.That(Count(s, "water_filter"), Is.EqualTo(1));
    }

    /// <summary>active_work：净水片在医疗台主动 30 分钟、医疗门槛与经验；锅食谱不在制作面板开工；陶窑配方写后续开放。</summary>
    [Test]
    public void CR_ActiveRecipes_PotAndKilnEntries()
    {
        var s = Camp(("disinfectant", 1), ("salt", 1));
        var med = s.Skills["medicine"].Xp;
        var pv = E.Preview(s, ActionRequest.Craft("ex_purification_tablet"));
        Assert.That(pv.XpGains, Has.Some.Contains("医疗经验+3"));
        Start(s, ActionRequest.Craft("ex_purification_tablet"));
        Run(s);
        Assert.That(Count(s, "purification_tablet"), Is.EqualTo(4));
        Assert.That(s.Skills["medicine"].Xp - med, Is.EqualTo(3));
        Assert.That(E.CanStart(s, ActionRequest.Craft("ex_hardtack")), Does.Contain("锅卡“锅食谱”"));
        // B17：陶窑、固定净水台可以建了（A07 X1.4）；没建时写“需要陶窑/固定净水台”
        Assert.That(E.CanStart(s, ActionRequest.Craft("ex_ceramic_shell")), Does.Contain("黏土"), "陶窑已建（开发入口）：被动投料按原料校验");
        Assert.That(E.RecipeLocks(s, Rc("ex_station_water")), Is.Empty, "开发入口“建全部工坊”已含固定净水台");
    }

    /// <summary>ES08/SUP05：口粮棒非热组装，保质 = min(完成+168h, 两原料最早截止)；原料新鲜时按 168h。烹饪经验计入每日 30。</summary>
    [Test]
    public void ES08_ExpeditionBar_ShelfNotLaterThanEarliestInput()
    {
        var s = Camp(("dried_meat", 1), ("dried_berries", 1));
        var meat = First(s, "dried_meat");
        meat.ExpiresAt = s.GameMinute + 600;
        Start(s, ActionRequest.Craft("ex_expedition_bar"));
        Run(s);
        var bar = First(s, "expedition_bar");
        Assert.That(bar.ExpiresAt, Is.EqualTo(meat.ExpiresAt));

        var t = Camp(("dried_meat", 1), ("dried_berries", 1));
        var t0 = t.GameMinute;
        Start(t, ActionRequest.Craft("ex_expedition_bar"));
        Run(t);
        Assert.That(First(t, "expedition_bar").ExpiresAt, Is.InRange(t0 + 168 * 60, t.GameMinute + 168 * 60), "原料新鲜时从完成起 168 小时");

        // Q60：食品与主动食品配方合计每日 30
        var u = Camp(("dried_meat", 1), ("dried_berries", 1));
        u.Actors[GameState.PlayerId].DailyCounts[$"{Survival.Core.Engine.Clock.Day(u.GameMinute)}|xp|cooking"] = 29;
        var before = u.Skills["cooking"].Xp;
        Start(u, ActionRequest.Craft("ex_expedition_bar"));
        Run(u);
        Assert.That(u.Skills["cooking"].Xp - before, Is.EqualTo(1), "日额只剩 1");
    }

    /// <summary>A07 X1.3：ceramic_methods 在营区工坊“基础陶工说明”制造 3 级免费领（0 分钟、一次）；d06_08 矿道作业手册同知识替代。</summary>
    [Test]
    public void CeramicMethods_NoticeAtCrafting3_OrMiningRecord()
    {
        var s = New();
        E.DebugMapTeleport(s, "h03_workshop");
        Assert.That(E.KnowsKnowledge(s, "ceramic_methods"), Is.False);
        var doc = E.DocumentsHere(s).Single(d => d.Id == "h03_ceramic_notice");
        Assert.That(doc.ReadBlocked, Does.Contain("制造3级"));
        Assert.That(E.ReadDocument(s, "h03_ceramic_notice").Ok, Is.False);
        s.Skills["crafting"].Level = 3;
        var t0 = s.GameMinute;
        Assert.That(E.ReadDocument(s, "h03_ceramic_notice").Ok, Is.True);
        Assert.That(E.KnowsKnowledge(s, "ceramic_methods"), Is.True);
        Assert.That(s.GameMinute, Is.EqualTo(t0));

        var t = New();
        t.Progress.Flags.Add("mining_blueprint_pack");
        Assert.That(E.KnowsKnowledge(t, "ceramic_methods"), Is.True);
    }
}
