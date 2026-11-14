using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// BM7a（R234：S14 文首「2026-11-14 Q629—Q636」§Q629／Q630／Q631／Q635，N02 附录文首 Q629／Q630／Q631 节，逐字稿「Q629—Q636正式文字」表；02 Q629—Q631、Q635）：
/// 首战 guide 附注、指令一览两处单人旧句、调息短反馈、战斗信息与预测详情的两层介绍。
/// </summary>
public class BM7aTeachingTextTests : BattleTestBase
{
    /// <summary>逐字稿／N02：tutorial_arrive 的 guide 附注（note）完整新句。</summary>
    public const string TutorialArriveNote =
        "轮到我方某人时，可以使用他的行动点（AP）。先选技能，再把鼠标移到亮起的敌人上，看敌人旁的战斗信息浮框；右侧信息栏可看敌人详细属性，也可用←／→切换合法目标。看好后，点击亮起的目标，或按Enter执行。自用或无目标指令，选好后点击底部确认按钮，或按Enter确认；需要选择我方成员的技能，点击亮起的我方成员。用完行动点，或选“结束行动”后再确认，战斗就会继续。";

    /// <summary>Q629-A01：只换 note；饱饱正文、说话人、类型、事件与附注类型不变；实时附注即配置原句（记录按展示时的配置快照写）。</summary>
    [Test]
    public void Q629_A01_TutorialArriveNoteOnly()
    {
        var d = Cfg.Story.Dialogues.Single(x => x.Id == "tutorial_arrive");
        Assert.That(d.Note, Is.EqualTo(TutorialArriveNote));
        Assert.That(d.Text, Is.EqualTo("等等。它走路的声音不对。你看它的脚。"), "饱饱正文不改");
        Assert.That((d.Event, d.Speaker, d.Kind, d.NoteKind), Is.EqualTo(("tutorial_arrive", "饱饱", "speech", "guide")));
        Assert.That(d.Note, Does.Not.Contain("右侧预测").And.Not.Contain("旁边的确认"));
        // 对白记录写的是展示时的配置快照（旧档里已存的历史附注不被改写）
        var story = File.ReadAllText(Path.Combine(Root(), "src", "Core", "Engine", "GameEngine.Story.cs"));
        Assert.That(story, Does.Contain("Text = cfg.Text, Note = cfg.Note"));
    }

    /// <summary>Q630-A01：指令一览末句与「站位与目标」敌人选目标段末句换指定句；原技能列表与目标随机段其余不变。</summary>
    [Test]
    public void Q630_A01_HelpTeamSentences()
    {
        var help = Web("Services", "HelpCatalog.cs");
        Assert.That(help, Does.Not.Contain("我方现在只有你一人").And.Not.Contain("现在队友还不会跟你进战斗"));
        Assert.That(help, Does.Contain("仍然列出并写原因。队友按实际编队参战，指挥、医疗等按技能范围选择对象；保护、掩护、整队、协同仍须满足各自的队友和站位条件。\""));
        Assert.That(help, Does.Contain("迫近只打最前那一名。有队友参战时，他们也可能成为合法攻击目标。\""));
        Assert.That(help, Does.Contain("才在它目标位里站着的存活我方中等概率随机选一名（不提前告诉你会打谁；选中谁写进战斗日志）"), "原目标随机规则段不变");
    }

    /// <summary>Q635-A01：两层介绍整句替换；保留原单目标不自动攻击、右键／ESC 取消、点击／Enter 执行。</summary>
    [Test]
    public void Q635_A01_InfoAndDetailLayers()
    {
        var help = Web("Services", "HelpCatalog.cs");
        var i = help.IndexOf("Id = \"actions\"", StringComparison.Ordinal);
        var actions = help[i..help.IndexOf("Figures =", i, StringComparison.Ordinal)];
        Assert.That(actions, Does.Contain("“战斗信息”显示实际目标、成本、命中与暴击、伤害和目标状态；“预测详情”提供逐项来源。选好攻击技能后，悬停亮起的敌人可在敌人旁查看战斗信息浮框，右侧信息栏显示该敌人的详细属性。"));
        Assert.That(actions, Does.Not.Contain("“战斗信息”里的“预测详情”写实际的目标"));
        Assert.That(actions, Does.Contain("没选技能时点敌人只是选中查看，不会攻击；场上只剩一个敌人时也不会自动攻击。右键或 ESC 取消瞄准，不耗行动点。"));
        Assert.That(actions, Does.Contain("点击亮起的敌人，或点底部“执行{指令名}”、按回车，就执行这一招。"));
    }

    /// <summary>Q631-A01：主角调息成功的短反馈「你喘了口气」，只一条；卡面仍「调息」，数值战报与内核行动名原样。</summary>
    [Test]
    public void Q631_A01_BreatheShortFeedback_Player()
    {
        var s = Fight(32, "worker");
        PlayerFirst(s);
        E.BattleStep(s);
        Assume.That(E.AwaitingPlayer(s));
        E.DrainBattleFx();
        var logN = s.Battle!.Log.Count;
        Assert.That(Cmd(s, "breathe").Ok, Is.True);
        var fx = E.DrainBattleFx();
        Assert.That(fx.Where(x => x.Short).Select(x => x.Name), Is.EqualTo(new[] { "你喘了口气" }), "只一条短反馈，不叠「调息／调整呼吸」");
        Assert.That(fx.Any(x => !x.Short), Is.False, "不当作特写");
        Assert.That(s.Battle!.Log.Skip(logN).Count(l => l.Contains("调整呼吸：体力")), Is.EqualTo(1), "原数值战报原样一次");
        Assert.That(GameEngine.ActionName("breathe"), Is.EqualTo("调整呼吸"), "内核行动名不改");
        Assert.That(GameEngine.BreatheShortText("林舟"), Is.EqualTo("林舟喘了口气"), "同伴取实际姓名");
        Assert.That(E.CommandBlocked(s, BattleCommand.Of("breathe")), Is.EqualTo("本回合已调整过呼吸"), "失败仍原真实原因，不给成功反馈");
        // 名字取本次真实行动者（FxAlly：主角「你」、同伴 ActorDisplayName）
        var src = File.ReadAllText(Path.Combine(Root(), "src", "Core", "Engine", "GameEngine.Combat.cs"));
        Assert.That(src, Does.Contain("var fxWho = FxAlly(s, actor);\n                    FxShort(s, BreatheShortText(fxWho.Name), fxWho);").Or.Contain("var fxWho = FxAlly(s, actor);\r\n                    FxShort(s, BreatheShortText(fxWho.Name), fxWho);"));
        // 卡面短名仍「调息」
        var gs = Web("Services", "GameSession.cs");
        Assert.That(gs, Does.Contain("\"breathe\" => \"调息\""));
    }

    private static string Root()
    {
        var d = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (d != null && !Directory.Exists(Path.Combine(d.FullName, "web", "Survival.Web"))) d = d.Parent;
        return d!.FullName;
    }

    private static string Web(params string[] parts) => File.ReadAllText(Path.Combine(new[] { Root(), "web", "Survival.Web" }.Concat(parts).ToArray())).Replace("\r\n", "\n");
}
