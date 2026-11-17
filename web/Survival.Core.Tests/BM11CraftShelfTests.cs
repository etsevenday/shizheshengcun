using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发记录 BM11「次要问题合并」：S08 文首「2026-11-17 B49两个原批与B51c1单题」§Q882 用户新裁定（返料保质变化不另说明；动作名／暂停标题与后果词统一维修，配方工具维护不动）；
/// 验收 SMALL-Q882-01—03（S14 本批）。其余被撤恢复句的断言改在原 B51a／B51b／B51c1／B54／B22k／B41b／B41c2 测试里（各处注明出处）。
/// </summary>
public class BM11CraftShelfTests : TestBase
{
    /// <summary>SMALL-Q882-03：普通维修行动名与暂停标题“维修{物名}”；配方名仍“工具维护”；退出预告后果词“不会完成本次维修。”。</summary>
    [Test]
    public void Q882_03_RepairActionName_Weixiu_RecipeNameKept()
    {
        var s = New();
        var mach = E.DebugCreateEquipment(s, "machete", "white", ItemInstance.Carried);
        mach.Durability = Math.Round(mach.MaxDurability!.Value * 0.5);
        E.DebugGive(s, "scrap", 1);
        E.DebugGive(s, "parts", 1);
        Start(s, new ActionRequest { Type = ActionType.Craft, TargetId = "repair_selected_tool", InstanceId = mach.InstanceId });
        Assert.That(s.Main!.Type, Is.EqualTo(ActionType.Repair));
        Assert.That(s.Main.Label, Is.EqualTo("维修砍刀"));   // Q874 A／Q882：动作名统一“维修{物名}”
        Assert.That(E.ActionProgress(s)!.Label, Is.EqualTo("维修砍刀"), "暂停／进度标题同源");
        Assert.That(Cfg.Recipes.Single(r => r.Id == "repair_selected_tool").Name, Is.EqualTo("工具维护"), "配方名不动");
        Assert.That(E.ActionProgress(s)!.ExitNote, Does.EndWith("不会完成本次维修。已耗时间不退。"));
        Assert.That(E.ActionProgress(s)!.ExitNote, Does.Not.Contain("维护"));
    }

    /// <summary>SMALL-Q882-01／02：通用生产与晾制说明换短句、取消ⓘ删退料恢复解释；真实暂停 R 仍照常（预估说明只在绑定有期限原料时出现）。</summary>
    [Test]
    public void Q882_01_ShelfNotes_ShortTexts()
    {
        Assert.That(GameEngine.ProductionShelfNote, Is.EqualTo("加工时原料暂停保质。"));   // S08 §Q882：通用生产原对应说明
        Assert.That(GameEngine.DryingShelfHelpText, Is.EqualTo("晾制时原料暂停保质；成品正常计时。"));   // S08 §Q882：晾制既有短说明
        Assert.That(GameEngine.DryingShelfPauseText, Is.EqualTo("开始制作后暂停原料保质计时"));   // 原句删“，取消后恢复剩余时长”（暂用）
        Assert.That(GameEngine.DryingCancelHintText, Is.EqualTo("已过制作时间不退，没有成品，不给经验。"));   // 原句删首分句“取消后恢复投料时剩余的保质时长；”
    }

    /// <summary>SMALL-Q882-01 配对：取消返料句不附期限，但物品详情的真实 C＋R 照常（截止＝取消时刻＋暂停 R）。</summary>
    [Test]
    public void Q882_01_CancelText_NoExpiry_ButRealCPlusR()
    {
        var s = New();
        s.Items.RemoveAll(i => i.ItemId is "herb" or "cloth" or "bandage");
        E.DebugGive(s, "cloth", 1);
        foreach (var i in E.CreateItems(s, "herb", 1, ItemInstance.Carried)) i.ExpiresAt = s.GameMinute + 120;
        Start(s, ActionRequest.Craft("bandage"));
        var herb = s.Items.Single(i => i.ItemId == "herb");
        Assert.That(herb.ShelfPausedMinutes, Is.EqualTo(120), "开工暂停 R");
        s.GameMinute += 30;
        var zone = E.ZoneName(s, s.Ground);
        Assert.That(E.ActionProgress(s)!.ExitNote, Is.EqualTo($"退出将取消制作。材料将退回{zone}地面。已耗时间不退。"));
        Assert.That(E.Cancel(s).Ok, Is.True);
        Assert.That(s.Log.Last().Text, Is.EqualTo($"已取消制作。材料已退回{zone}地面。已耗时间不退。"));
        Assert.That((herb.ExpiresAt, herb.ShelfPausedMinutes), Is.EqualTo(((int?)(s.GameMinute + 120), (int?)null)), "真实 C＋R");
    }
}
