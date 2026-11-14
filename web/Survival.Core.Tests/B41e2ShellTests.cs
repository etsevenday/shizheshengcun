using Survival.Core.Engine;

namespace Survival.Core.Tests;

/// <summary>
/// B41e2 网页壳（源码检查）：新版敌踪的成长页练习盘、编队队长、先手显示、中央条暂存摘要、帮助与 Q446 旧句；
/// B41e3 起开关关时的旧句已删。另有一项读逐字稿原文（DesignSync），核对原句常量与策划案一致。
/// </summary>
public class B41e2ShellTests
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

    [Test, Category("DesignSync")]
    public void Texts_FromVerbatim()
    {
        var v = Verbatim();
        foreach (var t in new[]
                 {
                     GameEngine.FirstStrikeReadyText, GameEngine.SquadLeaderLabel, GameEngine.SquadLeaderNote, GameEngine.PracticeTitle, GameEngine.PracticeNote,
                     GameEngine.PracticeStart, GameEngine.PracticeBodyLabel, GameEngine.StashedWorkNote,
                     "| 暂存原工作摘要 | 暂存行动：{原行动名} |", "| 原工作地点摘要 | 行动地点：{原地点} |",
                     "| 可躲避卡失败入战整卡行 | 躲避失败时：{原受惊预览} |", "（{概率}，首次处理时抽定）",
                     "此敌踪撤退保护剩余{时长}，期间可主动处理。", "等待期间每小时 {变化列表}", "等待期间每小时 无变化", "（含先手×1.5）",
                     "人物对话和顶栏面板只盖住上面两行，当前地面和随身两行位置不变；进入战斗后改为全屏战斗画面，战斗结束原样回到这四行。",
                     "点击敌踪卡打开操作盘，查看风险后选择「主动接近」。",
                     "敌人是世界里已有的个体，伤势和身份会保留。动物各自显示为单卡，不加入随机组卡。普通单体按地点规则巡游，随机组卡的成员留在组中。",
                     "必经关卡的固定敌人必须战胜，无法躲避。可选强敌由你决定何时处理，不会倒计时主动接战。",
                     "潜行提高接近和躲避的成功率，判定使用小队队长的等级与身体状态。",
                 })
            Assert.That(v, Does.Contain(t), t);
        // 帮助原句（网页壳 HelpCatalog 常量）：逐字稿与源码各出现
        var h = Web("Services/HelpCatalog.cs");
        foreach (var t in new[]
                 {
                     "躲避成功会让可躲避的敌踪暂时消失。普通单卡重现后继续按地点规则巡游，组卡停留在重现地点。",
                     "先手加成按每名开场参战者分别记录。普通攻击、攻击技能、迎击和反击均可用掉首次攻击加成，未命中或伤害为0也会消耗。多段或多目标的同一次攻击共享这次加成。第一完整回合的有效速度提高50%。",
                     "潜行经验来自潜行接近、躲避与练习：有效判定成功给队长20经验、失败给10经验，同一次现身只在实际获得经验时记一次，满级判定不占领取次数；练习满30游戏分钟给练习者20经验。",
                     "潜行影响潜行接近和躲避的成功率，判定使用小队队长的潜行等级、清醒和精神，并与敌踪警惕等级比较。",
                     "人物对话显示在中部下方，覆盖「当前地面」和「随身物品」两行；上方场景和设施区域保持可见。对白读完前不能进行场景或物品操作，ESC仍可打开游戏菜单。顶栏面板按各自范围显示；进入战斗后改为全屏战斗画面，战斗结束原样回到主界面。",
                     "敌人是世界里已有的个体，伤势和身份会保留。动物各自显示为单卡，不加入随机组卡。普通单体按地点规则巡游，随机组卡的成员留在组中。",
                     "必经关卡的固定敌人必须战胜，无法躲避。可选强敌由你决定何时处理，不会倒计时主动接战。",
                     "在第二轨的「敌踪」中选择卡片，查看警惕、接近时间和风险，再选择主动接近、潜行接近或躲避。潜行与躲避各用5分钟，由小队队长判定成功率。",
                 })
        {
            Assert.That(v, Does.Contain(t), t);
            Assert.That(h, Does.Contain(t), t);
        }
        Assert.That(GameEngine.StashedWorkText("制作"), Is.EqualTo("暂存行动：制作"));
        Assert.That(GameEngine.WorkPlaceText("农野荒地"), Is.EqualTo("行动地点：农野荒地"));
    }

    [Test]
    public void Growth_PracticeDisk_Q428_Q540()
    {
        var g = Web("Components/GrowthOverlay.razor");
        Assert.That(g, Does.Contain("(\"survival\", \"求生与行动\")").And.Not.Contain("cardsV2"), "Q446 ⑨、Q428 组名（B41e3 删开关关的旧组名）");
        Assert.That(g, Does.Contain("共@(views.Count)项熟练度"));
        Assert.That(g, Does.Contain("潜行提高接近和躲避的成功率，判定使用小队队长的等级与身体状态。"));
        Assert.That(g, Does.Contain("\"潜行已满级\""));
        Assert.That(g, Does.Contain("@GameEngine.PracticeTitle").And.Contain("@GameEngine.PracticeActorLabel").And.Contain("@e.PracticeMinutesText")
            .And.Contain("e.PracticeXpPreview(s, who!)").And.Contain("e.PracticeBodyRateText(s, who!)").And.Contain("@GameEngine.PracticeNote")
            .And.Contain("@GameEngine.PracticeStart").And.Contain("@GameEngine.EnemyDiskBack"));
        Assert.That(g, Does.Contain("var why = G.PracticeBlocked;"), "禁用原因来自内核 CanPracticeStealth");
        Assert.That(g, Does.Contain("var valid = who != null && e.PracticeCandidates(s).Contains(who);"), "无选人／失效不给预估");
        var sess = Web("Services/GameSession.EnemyCardsV2.cs");
        Assert.That(sess, Does.Contain("E.CanPracticeStealth(S, PracticeActor)").And.Contain("E.StartPractice(S, PracticeActor"));
        Assert.That(sess, Does.Contain("PracticeActor = null;"), "收盘／离页丢弃草稿");
        // BM5a（Q567 选 A）：多人队打开时未选人，只有主角本人时预选「你」，不自动开工
        Assert.That(sess, Does.Contain("PracticeActor = S.Party.All(id => id == GameState.PlayerId) ? GameState.PlayerId : null;"));
    }

    [Test]
    public void Formation_LeaderPick_SubmittedWithSave()
    {
        var f = Web("Components/FormationOverlay.razor");
        Assert.That(f, Does.Contain("@if (G.FormationLeader is string leader)"));
        Assert.That(f, Does.Contain("@GameEngine.SquadLeaderLabel").And.Contain("@GameEngine.SquadLeaderNote").And.Contain("G.PickFormationLeader(aid)"));
        var s = Web("Services/GameSession.cs");
        Assert.That(s, Does.Contain("FormationLeader = E.SquadLeaderId(S);"));
        Assert.That(s, Does.Contain("E.SaveFormationBlocked(S, FormationDraft, FormationDraftVersion, FormationLeader)"));
        Assert.That(s, Does.Contain("Guid.NewGuid().ToString(\"N\"), FormationLeader)"));
    }

    [Test]
    public void Battle_FirstStrikeShown_Q537()
    {
        var p = Web("Components/BattlePanel.razor");
        Assert.That(p, Does.Contain("@if (e.FirstStrikeReady(b, vid))"), "我方成员详情");
        Assert.That(p, Does.Contain("@if (e.FirstStrikeReady(b, GameEngine.FirstStrikeKeyOf(t)))"), "敌人详情");
        Assert.That(p, Does.Contain("e.PlayerSpeedParts(s, b, null, vFs)").And.Contain("GameEngine.FirstStrikeTag(vFs)"), "构成说明追加先手项");
        Assert.That(p, Does.Contain("e.EnemyRoundSpeed(b, t).Note"));
        var q = Web("Components/BattleScreen.razor");
        Assert.That(q, Does.Contain("回合初有效速度{qSpeed}{qSrc}"));
    }

    [Test]
    public void CentralProgress_StashAndWorkPlace_Q472_Q440()
    {
        var c = Web("Components/CentralProgress.razor");
        Assert.That(c, Does.Contain("GameEngine.StashedWorkText(p.StashedLabel)").And.Contain("GameEngine.WorkPlaceText(p.WorkPlace)").And.Contain("GameEngine.StashedWorkNote"));
        Assert.That(c, Does.Contain("disabled=\"@(busy || p.KeptResumeBlock != null)\""), "异地禁用继续并写原因");
    }

    /// <summary>Q446 旧句替换：B41e3 翻开开关后只留新句，开关关时的旧分支删除。</summary>
    [Test]
    public void Q446_OldSentences_NewOnly()
    {
        var sh = Web("Components/SafehouseCard.razor");
        Assert.That(sh, Does.Contain("（战斗、行动中不能进出）").And.Not.Contain("（战斗、敌踪待处理、行动中不能进出）"), "⑦");
        var gs = Web("Components/GameScreen.razor");
        Assert.That(gs, Does.Contain("\"成长：十六门熟练度的十级轨道与技能节点\"").And.Not.Contain("十五门"), "⑧");
        Assert.That(Web("Components/CharacterPanel.razor"), Does.Contain("(\"survival\", \"求生与行动\")").And.Not.Contain("CardsV2"), "⑨");
        Assert.That(Web("Components/TipsOverlay.razor"), Does.Not.Contain("TipForState"), "⑩ 配置里的提示已是新句");
        var h = Web("Services/HelpCatalog.cs");
        Assert.That(h, Does.Contain("退回处的敌踪照常显示。撤退成功后，只有同一张敌踪卡在{e.Config.Encounters.CardRetreatGuardMinutes}游戏分钟内不会主动开战。"), "②");
        Assert.That(h, Does.Contain("\" + CapacityCardsV2 + \""), "③");
        Assert.That(h, Does.Contain("\" + StealthXpSource,").And.Contain("\" + StealthRole,"), "Q447 熟练度两句");
        Assert.That(h, Does.Contain("FirstStrikeHelp,"), "Q447 先手帮助");
        Assert.That(h, Does.Contain("StealthEvadeNote,"), "Q447 潜行与躲避补句");
        Assert.That(h, Does.Not.Contain("CardsV2(s)").And.Not.Contain("敌踪比容量多时"), "B41e3 删开关关的旧帮助");
    }
}
