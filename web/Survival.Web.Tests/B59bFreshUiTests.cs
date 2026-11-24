using Bunit;
using Survival.Core.Engine;
using Survival.Core.State;
using Survival.Web.Components;
using Survival.Web.Services;

namespace Survival.Web.Tests;

/// <summary>
/// B59b「晾制保鲜冻结与到期即转腐败」网页壳（S11「2026-11-20」§Q769、「2026-11-24」§Q1034／§Q1036／§Q1037；S14 Q769-01、Q770-03、Q1036-02）：
/// 真正渲染晾制架面板（DryingOverlay）与火堆加工位食材卡（FoodCard），断玩家看到的保质状态行；系统说明「保质与腐败」「锅、水与食材」按新规则改写的段落。
/// 起点用内核入口或 Debug 造，时间只经真实世界推进（等待行动）。
/// </summary>
public class B59bFreshUiTests : WebTestBase
{
    private void Ok(ActionResult r) => Assert.That(r.Ok, Is.True, r.Reason);

    /// <summary>空闲等待推进到指定分钟（按 5 分钟段），多余的等待在边界取消。</summary>
    private void AdvanceTo(int minute)
    {
        while (S.GameMinute < minute)
        {
            if (S.Main == null) Assert.That(E.Start(S, ActionRequest.Wait(30)).Ok, Is.True);
            E.Advance(S);
        }
        if (S.Main?.Type == ActionType.Wait) E.Cancel(S);
        G.Notify();
    }

    /// <summary>
    /// Q769-01（网页壳）：临期野果（原截止＝投入后 30 分钟）开晾，推进 60 分钟越过原截止后取出一份——展开工位1 只剩一张野果卡，
    /// 状态行写冻结的剩余「保质暂停，剩余30分钟」（逐字稿 B51 表「保质暂停，剩余{时长}」，时长＝冻结量 30），不是「保质到…」或「已腐败」。
    /// 配对：取出前在制的两张卡同样是这句（齐料开晾冻结，B50a Q666）。
    /// </summary>
    [Test]
    public void Q769_ResidualCard_ShowsFrozenRemainder_AfterOldDeadline()
    {
        E.DebugBuildAllWorkshops(S);
        S.Skills["cooking"].Level = 5;
        S.Items.RemoveAll(i => i.ItemId is "berry" or "dried_berries");
        var t0 = S.GameMinute;
        foreach (var b in E.CreateItems(S, "berry", 2, ItemInstance.Carried)) b.ExpiresAt = t0 + 30;
        Ok(E.StartDrying(S, "dry_berries"));
        AdvanceTo(t0 + 60);

        var cut = Ctx.RenderComponent<DryingOverlay>();
        cut.Find("[data-pt=dry-toggle][data-slot='0']").Click();
        var before = cut.FindAll("[data-dry-items='0'] [data-pt=fac-item] .ir-status").Select(x => x.TextContent).ToList();
        Assert.That(before, Is.EqualTo(new[] { "保质暂停，剩余30分钟", "保质暂停，剩余30分钟" }), "配对：在制两份冻结");

        var card = E.DryingContents(S, S.Ground, 0).Single();
        Ok(E.DoInstant(S, InstantRequest.FacilityTake(card.InstanceId, 1, S.Ground, E.FacilityItemStamp(S, card))));
        G.Notify();
        cut.Render();
        var after = cut.FindAll("[data-dry-items='0'] [data-pt=fac-item] .ir-status").Select(x => x.TextContent).ToList();
        Assert.That(after, Is.EqualTo(new[] { "保质暂停，剩余30分钟" }), "留位余料继续冻结");
    }

    /// <summary>
    /// Q1036／Q770-03（网页壳）：直烤生肉原截止＝开烤后 5 分钟，受热 10 分钟越过旧截止——加工位卡的保质行写冻结剩余「保质暂停，剩余5分钟」，
    /// 状态行仍「受热保护中」、不写「取出即腐败」「已过原期限」。
    /// 配对：迁移时已越过原截止、没有正余量可冻结的旧档受热食材（R 为空）仍沿原 Q16 写「受热保护中（已过原期限，取出即腐败）」。
    /// </summary>
    [Test]
    public void Q1036_FoodCard_FrozenPastOldDeadline_NoSpoilWarning_LegacyKeepsOld()
    {
        S.Items.RemoveAll(i => i.ItemId == "raw_meat");
        var t0 = S.GameMinute;
        var meat = E.CreateItems(S, "raw_meat", 1, S.Ground).Single();
        meat.ExpiresAt = t0 + 5;
        Ok(E.DoInstant(S, InstantRequest.PutInFireSlot(meat.InstanceId, 0, 1)));
        var f = S.Locations[S.Ground].Facility;
        f.FireLit = true;
        f.FuelMinutes = 240;
        AdvanceTo(t0 + 10);
        Assert.That(E.ProcessOf(S, meat)!.HeatProtected, Is.True, "起点：受热保护中");

        var cut = Ctx.RenderComponent<FoodCard>(p => p.Add(x => x.Item, meat).Add(x => x.Slot, 1).Add(x => x.ReadOnly, true));
        Assert.That(cut.Find(".ff-note").TextContent, Is.EqualTo("保质暂停，剩余5分钟"));
        Assert.That(cut.Find(".ff-status").TextContent, Does.Contain("受热保护中").And.Not.Contain("取出即腐败"));
        Assert.That(cut.Markup, Does.Not.Contain("已过原期限"));

        meat.ShelfPausedMinutes = null;   // 起点：造旧档受热食材（S06 Q770 补正「旧期限兼容沿原办法处理」）
        cut.Render();
        Assert.That(cut.Find(".ff-note").TextContent, Is.EqualTo("受热保护中（已过原期限，取出即腐败）"), "配对：旧档沿原 Q16");
    }

    /// <summary>
    /// 系统说明（S14 §13.3 玩家主题「物品、容器与负重」）按 §Q1036 改写：「保质与腐败」与「锅、水与食材」里写取出后从剩下的保质时间接着计时，
    /// 不再写「原保质时间已过……取出后立刻腐败」或「即使过了原保质时间也可以直接吃」（暂用文案，见开发记录 B59b）。
    /// </summary>
    [Test]
    public void Q1036_HelpText_ResumeFromRemainder_NoTakeOutSpoil()
    {
        var spoil = HelpCatalog.Entry("items", "spoil")!.Paragraphs(E, S).ToList();
        var pot = HelpCatalog.Entry("items", "pot")!.Paragraphs(E, S).ToList();
        Assert.That(spoil, Has.Some.Contains("第一次实际受热时，这次投入的全部原料保质暂停").And.Some.Contains("从剩下的保质时间接着计时"));
        Assert.That(pot, Has.Some.EqualTo("取出正在受热的食材或整口锅，会清掉对应的加工进度并结束烹饪保护，食材从剩下的保质时间接着计时。取出失败（超重、卡位满）时什么都不变。"));
        Assert.That(spoil.Concat(pot), Has.None.Contains("取出后立刻腐败").And.None.Contains("即使过了原保质时间也可以直接吃"));
    }
}
