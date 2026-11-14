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

    /// <summary>Q578-A01：通用区「调整站位」只有一张卡；点卡只进入选位，明确点亮起的我方目的号位才按原 swap 请求提交；全局 Enter 不代选。</summary>
    [Test]
    public void Q578_SingleSwapCard_PickSlotToCommit()
    {
        var bs = Web("Components/BattleScreen.razor");
        Assert.That(bs, Does.Contain("if (o.Action == \"swap\") { if (!swapShown) { swapShown = true; @SwapCard("), "按目的号位拆出的多条 swap 合成一张卡");
        Assert.That(bs, Does.Contain("G.PickBattleAction(\"swap\")"), "点卡只进入选位（不带目的槽）");
        Assert.That(bs, Does.Contain("if (G.BattleSwapAiming) { if (!locked) await G.ClickSwapSlot(pos, token); return; }"), "点亮起的号位才提交");
        Assert.That(bs, Does.Contain("@onclick=\"() => G.ClickSwapSlot(pos, token)\""), "合法空号位也可点");
        var gs = Web("Services/GameSession.cs");
        Assert.That(gs, Does.Contain("if (BattleSwapAiming) return;"), "选位中全局 Enter 不代选目的号位");
        Assert.That(gs, Does.Contain("BattleConfirmFocusPending = !BattleAiming && !BattleAllyAiming && !BattleSwapAiming;"));
        var sw = Web("Services/GameSession.BattleSwap.cs");
        Assert.That(sw, Does.Contain("BattleAction == \"swap\" && BattleItem == null"));
        Assert.That(sw, Does.Contain("BattleItem = pos;").And.Contain("await ExecuteBattleCommand(token);"), "提交走原指令入口");
    }

    /// <summary>Q576-A01：战斗计时「轮」统一显示为「回合」；「轮到」等自然语不动；旧档已保存战报只在展示层同义转换。</summary>
    [Test]
    public void Q576_RoundWord_Unified()
    {
        Assert.That(BattleInfoCopy.RoundWords("第3轮 · 游荡尸本轮只剩1AP，下轮速度−2（到第5轮末），冷却2轮"),
            Is.EqualTo("第3回合 · 游荡尸本回合只剩1AP，下回合速度−2（到第5回合末），冷却2回合"));
        Assert.That(BattleInfoCopy.RoundWords("第2轮 · 轮到你行动：3AP。"), Is.EqualTo("第2回合 · 轮到你行动：3AP。"));
        Assert.That(BattleInfoCopy.RoundWords(null), Is.EqualTo(""));
        var bs = Web("Components/BattleScreen.razor");
        Assert.That(bs, Does.Contain("@BattleInfoCopy.RoundWords(b.Log.LastOrDefault())").And.Contain("@BattleInfoCopy.RoundWords(b.Log[i])").And.Contain("var shown = BattleInfoCopy.RoundWords(line);"),
            "最新一条、最近5条、全记录都过展示层转换");
        // 内核与界面的玩家可见字面量里不再有战斗计时「轮」
        var rx = new System.Text.RegularExpressions.Regex("\"[^\"\\n]*(第\\{[^}]+\\}轮|本轮|下轮|每轮|轮末|轮初|\\}轮)[^\"\\n]*\"");
        foreach (var f in Directory.GetFiles(Path.Combine(Root(), "src", "Core", "Engine"), "GameEngine.*.cs")
                     .Concat(new[] { "Components/BattleScreen.razor", "Components/BattlePanel.razor", "Components/GrowthOverlay.razor", "Services/HelpCatalog.cs" }.Select(x => Path.Combine(Root(), "web", "Survival.Web", x))))
            foreach (var line in File.ReadLines(f).Where(l => !l.TrimStart().StartsWith("//") && !l.TrimStart().StartsWith("@*")))
                Assert.That(rx.IsMatch(line) && !line.Contains("外部区按本轮显式清单"), Is.False, $"{Path.GetFileName(f)}：{line.Trim()}");   // 「本轮显式清单」是版本批次，不是战斗计时
    }

    /// <summary>Q579-A01：锁级技能卡面、说明、拒绝原因与成长面板都用「需要{门类}{所需等级}级（当前{当前等级}级）」，不另造「达到…级解锁」。</summary>
    [Test]
    public void Q579_SkillLevelText_Unified()
    {
        var bs = Web("Components/BattleScreen.razor");
        Assert.That(bs, Does.Contain("<span class=\"cmdt-why cmdt-need\">@why</span>"), "卡面用 LockedText 原句");
        Assert.That(bs, Does.Not.Contain("当前@(e.SkillLevel(G.S, node.SkillId))级"));
        var growth = Web("Components/GrowthOverlay.razor");
        Assert.That(growth, Does.Contain("$\"需要{sk.Name}{n.Level}级（当前{sk.Level}级）\"").And.Not.Contain("达到{n.Level}级"));
        var core = File.ReadAllText(Path.Combine(Root(), "src", "Core", "Engine", "GameEngine.CombatSkills.cs"));
        Assert.That(core, Does.Contain("$\"需要{SkillName(n.SkillId)}{n.Level}级（当前{SkillLevel(s, n.SkillId)}级）\""));
    }

    /// <summary>Q580-A01：指令一览按最终页面描述；无旧右列几何、空态催促、敌方未执行计划；回合与等级措辞一致。</summary>
    [Test]
    public void Q580_HelpActions_FinalLayout()
    {
        var help = Web("Services/HelpCatalog.cs");
        var i = help.IndexOf("Id = \"actions\"");
        var actions = help[i..help.IndexOf("Figures =", i)];
        foreach (var must in new[] { "战斗 · {地点}", "第{回合}回合", "占满全宽的战场", "{号位}号位 · {姓名}", "“战斗信息”", "“预测详情”", "“战报”", "“全记录”", "当前行动者：{姓名}",
                     "需要{门类}{所需等级}级（当前{当前等级}级）", "“调整站位”只有一张卡", "“医疗与物品”", "执行{指令名}", "确认 · {指令名} · {AP}AP", "“系统说明”在右上“菜单”里" })
            Assert.That(actions, Does.Contain(must), must);
        foreach (var gone in new[] { "右边是对象信息", "右下是战报", "物资查看", "在底部点“确认”才执行", "意图", "选择技能，再选择目标", "请选择指令" })
            Assert.That(actions, Does.Not.Contain(gone), gone);
        Assert.That(help, Does.Not.Contain("敌卡意图写").And.Not.Contain("意图仍写保护").And.Not.Contain("执行一个公开的主意图"));
    }

    /// <summary>Q578：内核每个目的号位一条 swap 选项（ItemInstanceId＝目的槽），合成卡的号位亮起与它同源。</summary>
    [Test]
    public void Q578_EngineSwapOptions_PerDestination()
    {
        var src = File.ReadAllText(Path.Combine(Root(), "src", "Core", "Engine", "GameEngine.Combat.cs"));
        Assert.That(src, Does.Contain("ItemInstanceId = to, Available = sw == null"), "目的槽由 ItemInstanceId 带上");
    }
}
