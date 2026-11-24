using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Survival.Core.Engine;
using Survival.Core.State;
using Survival.Web.Components;
using Survival.Web.Services;

namespace Survival.Web.Tests;

/// <summary>
/// B35b 物品卡维修／拆解按钮（开发记录 doc/开发记录/B35b 制作页三列与来源侧页.md）：真实渲染 CardActions（选中一把砍刀），断按钮文字、标签分组与点按后的真实事务。
/// 出处：S08／A10／R02 2026-11-16 B35 Q801—Q802（物品卡按同一动作模板＋同源实际预计时长）、2026-11-17 B35 次要 Q912 A（现场修补按钮统一“开始维修（{实际时长}）”，取代 Q802 的“开始现场修补”）、
/// 2026-11-18 B35b Q947 A（不能开工只写动作名）、Q948 A（两种维修各一组原配方标签“工具维护”“现场修补”，读屏关联本组标签）；BM11 Q965 沿用。
/// </summary>
public class B35bItemCardRepairUiTests : WebTestBase
{
    private ItemInstance _mach = null!;

    /// <summary>当前地面一把绿色砍刀（耐久 0/100）；清掉修补包、废料、零件，按需再放。</summary>
    private IRenderedComponent<CardActions> OpenMachete(bool patchPack, bool repairMats)
    {
        S.Items.RemoveAll(i => i.ItemId.StartsWith("field_patch") || i.ItemId is "scrap" or "parts");
        _mach = E.DebugCreateEquipment(S, "machete", "green", S.Ground);
        _mach.MaxDurability = 100;
        _mach.Durability = 0;
        if (patchPack) E.CreateItems(S, "field_patch_blue", 1, S.Ground);
        if (repairMats) { E.CreateItems(S, "scrap", 10, S.Ground); E.CreateItems(S, "parts", 10, S.Ground); }
        var sel = new Selection { Kind = SelectionKind.Tool, Id = _mach.ItemId, Owner = _mach.Owner, InstanceId = _mach.InstanceId };
        return Ctx.RenderComponent<CardActions>(p => p.Add(x => x.Sel, sel));
    }

    private ActionRequest Req(string recipeId) => new() { Type = ActionType.Craft, TargetId = recipeId, InstanceId = _mach.InstanceId };

    /// <summary>
    /// B35B-Q948-01、Q947：两组标签分别“工具维护”“现场修补”（原配方名），各自关联本组按钮（role=group＋aria-labelledby 指向本组标签）；
    /// 工具维护缺废料／零件不能开工——按钮只写“开始维修”、原因照旧在下面；现场修补能开工——“开始维修（{同源预计时长}）”，白天常态倍率 1、基础 15 分钟写“15分钟”；
    /// 拆解按钮不能开工只写“开始拆解”，能开工“开始拆解（{同源预计时长}）”。不再出现旧“维修·30分钟”“现场修补·…分钟”。
    /// </summary>
    [Test]
    public void Labels_GroupedWithButtons_DisabledVerbOnly()
    {
        var cut = OpenMachete(patchPack: true, repairMats: false);

        var rg = cut.Find("[data-pt=card-repair-group]");
        Assert.That(rg.GetAttribute("role"), Is.EqualTo("group"));
        var rlab = rg.QuerySelector("[data-pt=card-repair-label]")!;
        Assert.That(rlab.TextContent, Is.EqualTo("工具维护"));
        Assert.That(rg.GetAttribute("aria-labelledby"), Is.EqualTo(rlab.Id));
        var rbtn = rg.QuerySelector("[data-pt=card-repair]")!;
        Assert.That(rbtn.HasAttribute("disabled"), Is.True);
        Assert.That(rbtn.TextContent, Is.EqualTo("开始维修"));
        Assert.That(cut.FindAll(".ca-note.bad").Select(n => n.TextContent), Has.Some.StartsWith("维修："), "原因仍在旁边");

        var pg = cut.Find("[data-pt=card-patch-group]");
        var plab = pg.QuerySelector("[data-pt=card-patch-label]")!;
        Assert.That(plab.TextContent, Is.EqualTo("现场修补"));
        Assert.That(pg.GetAttribute("aria-labelledby"), Is.EqualTo(plab.Id));
        Assert.That(plab.Id, Is.Not.EqualTo(rlab.Id));
        var pbtn = pg.QuerySelector("[data-pt=card-patch]")!;
        Assert.That(pbtn.HasAttribute("disabled"), Is.False);
        var pp = G.Preview(Req(GameEngine.FieldPatchRepairId));
        Assert.That(pp.EstimatedMinutes, Is.EqualTo(15), "起点核对：白天常态倍率 1，基础 15 分钟");
        Assert.That(pbtn.TextContent, Is.EqualTo("开始维修（15分钟）"));

        // 拆解：开局制造 1 级拆不了 1 阶装备（“拆解1阶装备需要制造3级”）——只写动作名；升到 3 级后带同源时长
        var sbtn = cut.Find("[data-pt=card-salvage]");
        Assert.That(sbtn.HasAttribute("disabled"), Is.True);
        Assert.That(sbtn.TextContent, Is.EqualTo("开始拆解"));
        S.Skills["crafting"].Level = 3;
        G.Notify();
        cut.Render();
        var sp = G.Preview(Req(E.SalvageRecipe!.Id));
        Assert.That(sp.Ok, Is.True, sp.Reason);
        Assert.That(cut.Find("[data-pt=card-salvage]").TextContent, Is.EqualTo($"开始拆解（{Clock.Duration(sp.EstimatedMinutes)}）"));
        Assert.That(cut.Markup, Does.Not.Contain("维修·").And.Not.Contain("现场修补·").And.Not.Contain("拆解·"));
    }

    /// <summary>
    /// B35b 试玩修复 M1157（Q947 A“原因仍在原旁侧”）：不能开工的原因句在本组按钮旁（同一组内、紧跟按钮），按钮以 aria-describedby 指向它；
    /// 能开工的按钮旁没有原因句。原句不变（“维修：…”“拆解：…”）。
    /// </summary>
    [Test]
    public void DisabledReasons_BesideOwnButton()
    {
        var cut = OpenMachete(patchPack: true, repairMats: false);
        var rbtn = cut.Find("[data-pt=card-repair]");
        var rwhy = cut.Find("[data-pt=card-repair-group] .ca-note.bad");
        Assert.That(rwhy.TextContent, Does.StartWith("维修："));
        Assert.That(rbtn.NextElementSibling?.Id, Is.EqualTo(rwhy.Id), "原因紧跟本组按钮");
        Assert.That(rbtn.GetAttribute("aria-describedby"), Is.EqualTo(rwhy.Id));
        var sbtn = cut.Find("[data-pt=card-salvage]");
        var swhy = cut.Find("[data-pt=card-salvage-group] .ca-note.bad");
        Assert.That(swhy.TextContent, Is.EqualTo("拆解：拆解1阶装备需要制造3级（当前1级）"));
        Assert.That(sbtn.NextElementSibling?.Id, Is.EqualTo(swhy.Id));
        Assert.That(sbtn.GetAttribute("aria-describedby"), Is.EqualTo(swhy.Id));
        // 配对：现场修补能开工——组内没有原因句、按钮不挂 describedby
        Assert.That(cut.FindAll("[data-pt=card-patch-group] .ca-note.bad"), Is.Empty);
        Assert.That(cut.Find("[data-pt=card-patch]").HasAttribute("aria-describedby"), Is.False);
        Assert.That(cut.FindAll(".ca-note.bad").Count(n => n.TextContent.StartsWith("维修：")), Is.EqualTo(1), "原因只出现一次（不在组外重复）");
    }

    /// <summary>
    /// 配对（Q801—Q802、Q948 A）：放上废料、零件后工具维护也能开工——两个按钮都叫“开始维修（…）”，时长各取各自预估；两组仍靠标签区分；
    /// 拿掉修补包后现场修补变灰只写“开始维修”，标签“现场修补”仍在（两个灰按钮也能分清）。
    /// </summary>
    [Test]
    public void BothEnabled_SameVerb_ThenPatchDisabledKeepsLabel()
    {
        var cut = OpenMachete(patchPack: true, repairMats: true);
        var rp = G.Preview(Req(E.RepairRecipe!.Id));
        Assert.That(rp.Ok, Is.True, rp.Reason);
        Assert.That(cut.Find("[data-pt=card-repair]").TextContent, Is.EqualTo($"开始维修（{Clock.Duration(rp.EstimatedMinutes)}）"));
        Assert.That(cut.Find("[data-pt=card-repair]").HasAttribute("disabled"), Is.False);
        Assert.That(cut.Find("[data-pt=card-patch]").TextContent, Is.EqualTo("开始维修（15分钟）"));

        S.Items.RemoveAll(i => i.ItemId.StartsWith("field_patch"));
        G.Notify();
        cut.Render();
        var pbtn = cut.Find("[data-pt=card-patch]");
        Assert.That(pbtn.HasAttribute("disabled"), Is.True);
        Assert.That(pbtn.TextContent, Is.EqualTo("开始维修"));
        Assert.That(cut.Find("[data-pt=card-patch-label]").TextContent, Is.EqualTo("现场修补"));
    }

    /// <summary>
    /// B35B-Q948-02：两按钮各走原真实事务——点“现场修补”组的开始维修：扣修补包耐久、不动废料／零件，砍刀耐久回升。
    /// </summary>
    [Test]
    public async Task PatchButton_RunsFieldPatch_NotToolMaintenance()
    {
        var cut = OpenMachete(patchPack: true, repairMats: true);
        var pack = S.Items.Single(i => i.ItemId == "field_patch_blue");
        var packBefore = pack.Durability;
        int Count(string id) => S.Items.Where(i => i.ItemId == id).Sum(i => i.Count);
        var (scrap, parts) = (Count("scrap"), Count("parts"));

        await cut.Find("[data-pt=card-patch]").ClickAsync(new MouseEventArgs());

        Assert.That(S.Main, Is.Null, "行动已结束");
        Assert.That(_mach.Durability, Is.GreaterThan(0), "砍刀修过");
        var packNow = S.Items.FirstOrDefault(i => i.InstanceId == pack.InstanceId);
        Assert.That(packNow == null || packNow.Durability < packBefore, Is.True, "扣的是修补包");
        Assert.That((Count("scrap"), Count("parts")), Is.EqualTo((scrap, parts)), "工具维护的废料／零件不动");
    }

    /// <summary>B35B-Q948-02 配对：点“工具维护”组的开始维修——消耗废料／零件、不动修补包。</summary>
    [Test]
    public async Task RepairButton_RunsToolMaintenance_NotFieldPatch()
    {
        var cut = OpenMachete(patchPack: true, repairMats: true);
        var pack = S.Items.Single(i => i.ItemId == "field_patch_blue");
        var packBefore = pack.Durability;
        int Count(string id) => S.Items.Where(i => i.ItemId == id).Sum(i => i.Count);
        var mats = Count("scrap") + Count("parts");

        await cut.Find("[data-pt=card-repair]").ClickAsync(new MouseEventArgs());

        Assert.That(S.Main, Is.Null, "行动已结束");
        Assert.That(_mach.Durability, Is.GreaterThan(0), "砍刀修过");
        Assert.That(Count("scrap") + Count("parts"), Is.LessThan(mats), "扣的是废料／零件");
        Assert.That(pack.Durability, Is.EqualTo(packBefore), "修补包不动");
    }
}
