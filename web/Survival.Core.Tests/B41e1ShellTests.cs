using Survival.Core.Engine;

namespace Survival.Core.Tests;

/// <summary>
/// B41e1 网页壳（源码检查）：开关 encounters.enemyCardsV2 开时的敌踪卡分组、居中操作盘、敌踪详情；开关关时旧组件分支保留。
/// 另有一项读逐字稿原文（DesignSync），核对界面原句常量与策划案一致。
/// </summary>
public class B41e1ShellTests
{
    private static string Root()
    {
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "doc", "策划案"))) dir = dir.Parent;
        Assert.That(dir, Is.Not.Null, "找不到仓库根目录");
        return dir!.FullName;
    }

    private static string Web(string relative) => File.ReadAllText(Path.Combine(Root(), "web", "Survival.Web", relative)).Replace("\r\n", "\n");

    private static string Verbatim() => File.ReadAllText(Directory.GetFiles(Path.Combine(Root(), "doc", "策划案"), "重墨 V2 开工裁定与界面逐字稿.md", SearchOption.AllDirectories).Single());

    /// <summary>从 start 起到 end 之前的一段源码。</summary>
    private static string Segment(string src, string start, string end)
    {
        var i = src.IndexOf(start, StringComparison.Ordinal);
        Assert.That(i, Is.GreaterThanOrEqualTo(0), start);
        var j = src.IndexOf(end, i + start.Length, StringComparison.Ordinal);
        Assert.That(j, Is.GreaterThan(i), end);
        return src.Substring(i, j - i);
    }

    [Test, Category("DesignSync")]
    public void Texts_FromVerbatim()
    {
        var v = Verbatim();
        foreach (var t in new[]
                 {
                     GameEngine.EnemyCardFootnote, GameEngine.KeyEnemyDetailNote, GameEngine.EnemyCardDetailTitle, GameEngine.EvadeFailFearPrefix,
                     "| 操作盘信息标签 | " + string.Join("、", GameEngine.EnemyDiskLabels) + " |", "| 警惕值 | 警惕{等级}级 |", "| 队长值 | 队长：{姓名} |",
                     "| 潜行／躲避成功率 | 成功率{概率}% |", "| 潜行／躲避耗时 | 耗时5分钟 |", "| 三种处理按钮 | 主动接近、潜行接近、躲避 |", "| 操作盘关闭 | 返回 |",
                 })
            Assert.That(v, Does.Contain(t), t);
    }

    [Test]
    public void Cards_OneGroup_FromEnemyCardsHere_OldGroupsOnlyWhenOff()
    {
        var ls = Web("Components/LocationStrip.razor");
        Assert.That(ls, Does.Contain("var cardsV2 = e.CardsV2(s);"));
        Assert.That(ls, Does.Contain("var cards = cardsV2 && !inside ? e.EnemyCardsHere(s) : new List<EnemyCardView>();"));
        Assert.That(ls, Does.Contain("var enemies = inside || cardsV2 ? new List<WorldEnemy>() : e.VisibleEnemiesAt(s, here);"), "开关开时旧单卡组不出");
        Assert.That(ls, Does.Contain("var fixedHere = inside || cardsV2 ? new List<WorldEnemy>() : e.FixedEnemiesAt(s, here);"));
        Assert.That(ls, Does.Contain("var elites = cardsV2 ? new List<LegacyEliteConfig>() : e.EliteChallengesHere(s);"));
        var grp = Segment(ls, "@if (cards.Count > 0)", "@* B17 试玩修复 B1");
        Assert.That(grp, Does.Contain("<div class=\"dg-title\">敌踪</div>"));
        Assert.That(grp, Does.Contain("Title=\"@c.Title\"").And.Contain("Badge=\"@c.Badge\"").And.Contain("Danger=\"@c.Danger\""), "Q427 标题、角标、危险");
        Assert.That(grp, Does.Contain("e.CardApproachStatus(s, c)").And.Contain("e.EnemyCardHoverLines(s, c)"), "第二行状态句、Q446 ⑥ 悬停");
        Assert.That(grp, Does.Contain("TODO Q539"));
        Assert.That(grp, Does.Contain("OnClick=\"() => OpenEnemyCard(key)\""));
        Assert.That(grp, Does.Not.Contain("<Footer>"), "处理按钮都在操作盘里");
    }

    [Test]
    public void Disk_ReasonsFromCore_SubmitWithLeaderAndRate_NoEnter_Q447_Q457()
    {
        var ap = Web("Components/ActionPops.razor");
        var disk = Segment(ap, "case GameSession.EnemyCardPop:", "case \"lockstack\":");
        Assert.That(disk, Does.Contain("e.CanApproachCard(s, key)"));
        Assert.That(disk, Does.Contain("e.CanStealthCard(s, key, GameEngine.SneakKind, leader, sneakRate)"));
        Assert.That(disk, Does.Contain("e.CanStealthCard(s, key, GameEngine.EvadeKind, leader, evadeRate)"));
        Assert.That(disk, Does.Contain("StealthCard(key, GameEngine.SneakKind, leader, sneakRate)").And.Contain("StealthCard(key, GameEngine.EvadeKind, leader, evadeRate)"));
        Assert.That(disk, Does.Contain("e.EnemyCardPreviewMembers(s, c)"), "逐成员预览");
        Assert.That(disk, Does.Contain("e.EnemyCardFearNote(s, c, false)").And.Contain("GameEngine.EvadeFailFearPrefix</span>@e.EnemyCardFearNote(s, c, true)"));
        Assert.That(disk, Does.Not.Contain("data-enter").And.Not.Contain("EnterCap"), "不贴 Enter 快捷（Q499）");
        Assert.That(disk, Does.Contain("GameEngine.EnemyDiskBack<EscCap"));
        Assert.That(ap, Does.Contain("GameSession.EnemyCardPop => s.Battle == null && s.Encounter == null && e.CardsV2(s) && e.EnemyCardHere(s, pop.Id) != null"), "Q457 收盘重验");
        var session = Web("Services/GameSession.cs");
        Assert.That(session, Does.Contain("E.StartStealth(S, cardKey, kind, Guid.NewGuid().ToString(\"N\"), leaderId, expectedRate)"));
        Assert.That(session, Does.Contain("E.ApproachCard(S, cardKey, Guid.NewGuid().ToString(\"N\"))"));
        Assert.That(session, Does.Contain("if (Pop?.Kind == EnemyCardPop && State != null && Selected.Kind == SelectionKind.Enemy && Selected.Id == Pop.Id && E.EnemyCardHere(S, Pop.Id) == null)"));
        Assert.That(session, Does.Contain("if (E.CardsV2(S) ? E.EnemyCardHere(S, Selected.Id) == null"), "选中按卡键重验");
        Assert.That(Web("Services/GameSession.Peek.cs"), Does.Contain("if (E.CardsV2(S)) return E.EnemyCardHere(S, sel.Id) != null;"), "悬停按卡键重验");
    }

    [Test]
    public void Detail_V2Branch_Q446_4()
    {
        var d = Web("Components/EnemyDetail.razor");
        var v2 = Segment(d, "@if (cardsV2)", "else if (w == null || w.LocationId != s.CurrentLocation)");
        Assert.That(v2, Does.Contain("GameEngine.EnemyCardDetailTitle"));
        Assert.That(v2, Does.Contain("GameEngine.EnemyCardFootnote"));
        Assert.That(v2, Does.Contain("@if (card.KeyEnemy) { <li>@GameEngine.KeyEnemyDetailNote</li> }"));
        Assert.That(v2, Does.Not.Contain("；不受随机接敌保护期限制；"), "Q446 ④ 删旧失效说明");
        Assert.That(v2, Does.Contain("card.Kind is \"single\" or \"elite\" ? \"只与这一只交战\""), "组卡不写「只与这一只交战」");
        Assert.That(d.Substring(d.IndexOf("else if (w == null")), Does.Contain("；不受随机接敌保护期限制；只与这一只交战。"), "开关关时原详情不变");
    }

    [Test]
    public void Q538_NotPlaced()
    {
        foreach (var f in new[] { "Components/LocationStrip.razor", "Components/ActionPops.razor", "Components/EnemyDetail.razor" })
            Assert.That(Web(f), Does.Not.Contain("期间可主动处理"), f);
    }
}
