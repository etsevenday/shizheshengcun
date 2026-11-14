using Survival.Core.Engine;

namespace Survival.Core.Tests;

/// <summary>
/// B34（R218：S14 文首「2026-11-13 B34 Q573—Q580完整答复」，验收 Q573-A01—Q580-A01）：战斗文字、人物信息与行动者提示的接线。
/// </summary>
public class B34R218Tests
{
    private static string Root()
    {
        var d = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (d != null && !File.Exists(Path.Combine(d.FullName, "web", "Survival.sln"))) d = d.Parent;
        return d!.FullName;
    }

    private static string Web(string relative) => File.ReadAllText(Path.Combine(Root(), "web", "Survival.Web", relative)).Replace("\r\n", "\n");

    /// <summary>Q574-A01：名牌模板；站位、悬停、读屏、阅读区都不读敌人当前锁定的计划。</summary>
    [Test]
    public void Q574_Plate_AndNoFutureIntent()
    {
        Assert.That(BattleInfoCopy.Plate(2, "游荡尸"), Is.EqualTo("2号位 · 游荡尸"));
        Assert.That(BattleInfoCopy.Plate(3, null), Is.EqualTo("3号位 · 空位"));
        Assert.That(BattleInfoCopy.TakenBy, Is.EqualTo("实际承伤"));
        foreach (var f in new[] { "Components/BattleScreen.razor", "Components/BattlePanel.razor", "Components/BattleFxLayer.razor" })
        {
            var src = Web(f);
            foreach (var api in new[] { "IntentShown(", "IntentLabel(", "IntentTargetNote(", "IntentOutOfReach(", "PlanFor(", ".Intent ", ".Intent==", "本轮意图", "公开意图" })
                Assert.That(src, Does.Not.Contain(api), $"{f} 不读／不写敌人未执行计划：{api}");
        }
        var bs = Web("Components/BattleScreen.razor");
        Assert.That(bs, Does.Contain("@FoeChips(b, en, ec)\n                <div class=\"anc-nums"), "状态角标在数值条上方");
        Assert.That(bs, Does.Contain("<div class=\"anc-chips\">@AllyChips(id)</div>\n                <div class=\"anc-nums\">"), "我方状态角标在数值条上方");
    }
}
