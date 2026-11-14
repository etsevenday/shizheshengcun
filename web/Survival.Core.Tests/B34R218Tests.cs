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

    /// <summary>Q575-A01／Q576-A01：顶条只剩标题、回合、当前行动与菜单；容量、噪声、追击条件在队列行右端；系统说明只在战斗菜单。</summary>
    [Test]
    public void Q575_TopBarFacts_MovedToQueueRow()
    {
        Assert.That(BattleInfoCopy.Title("农野荒地"), Is.EqualTo("战斗 · 农野荒地"));
        Assert.That(BattleInfoCopy.RoundText(3), Is.EqualTo("第3回合"));
        var bs = Web("Components/BattleScreen.razor");
        var top = bs[bs.IndexOf("<header class=\"bfs-top\"")..bs.IndexOf("</header>")];
        Assert.That(top, Does.Not.Contain("bfs-cap").And.Not.Contain("bt-noise").And.Not.Contain("系统说明").And.Not.Contain("首战"));
        var queue = bs[bs.IndexOf("<div class=\"bfs-queue\"")..bs.IndexOf("@* 场景：八个永久锚点")];
        Assert.That(queue, Does.Contain("<span class=\"bq-facts\">").And.Contain("bfs-cap").And.Contain("bt-noise").And.Contain("追击条件"));
        Assert.That(Web("Components/MenuOverlay.razor"), Does.Contain("G.OpenBattleDrawer(\"help\")"), "系统说明仍在战斗菜单");
    }

    /// <summary>Q577-A01／A02：不再有文字催促；当前实际行动者在等待下令时加视觉提示，读屏为「当前行动者：{姓名}」；减少动态效果只留静态高亮。</summary>
    [Test]
    public void Q577_NoNagText_VisualActorCue()
    {
        var bs = Web("Components/BattleScreen.razor");
        Assert.That(bs, Does.Not.Contain("选一个技能，再点亮起的目标").And.Not.Contain("请选择指令。").And.Not.Contain("选择技能，再选择目标。"));
        Assert.That(Web("Components/BattlePanel.razor"), Does.Not.Contain("\"选择指令\""));
        Assert.That(bs, Does.Contain("var awaitCmd = acting && G.BattleAction == null && !G.DialogueLocked && !G.PlaybackActive && G.E.AwaitingPlayer(s);"));
        Assert.That(BattleInfoCopy.CurrentActorAria("你"), Is.EqualTo("当前行动者：你"));
        var css = Web("wwwroot/css/battle.css");
        Assert.That(css, Does.Contain("@media (prefers-reduced-motion: reduce) { .anc.ally.await-cmd .anc-art > img { animation: none !important; transform: none;"));
    }
}
