using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 BM5b1 续做（R236）Q639：战前敌踪右栏只突出身份、风险和敌人重要属性；所有敌踪展示出口隐藏主动接近与保护的剩余时长。
/// 规则出处：S14「2026-11-14 Q637—Q643：分层原因、装备资格与精简敌踪详情」Q639、R05「2026-11-14 Q639：身份与核心属性常显，敌踪倒计时隐藏」
/// 与验收 Q639-A01—A03；正式句见《重墨 V2 开工裁定与界面逐字稿》「2026-11-14 Q637—Q643正式文字与显示分工」。网页壳以源码检查。
/// </summary>
public class BM5b1EnemyRailTests : B41cTestBase
{
    private static readonly string[] TimerWords = { "剩余", "距敌人接近", "已到期", "即将", "分钟", "小时" };

    private static void NoTimer(string? text, string what)
    {
        if (text == null) return;
        foreach (var w in TimerWords) Assert.That(text, Does.Not.Contain(w), $"{what}：{text}");
    }

    /// <summary>Q639-A01：主区依次身份、风险、当前成员重要属性；我方命中、开战武器、感染／空弹仓、成员类别与其他成员在「战斗详情」，原成员与长说明不丢；中央预估保留。</summary>
    [Test]
    public void Q639_A01_MainAreaOrderAndFolds()
    {
        var d = Web("Components", "EnemyDetail.razor");
        var name = d.IndexOf("<Name>@GameEngine.EnemyRailIdentity(card)</Name>", StringComparison.Ordinal);
        var risk = d.IndexOf("<div class=\"rail-risk\"", StringComparison.Ordinal);
        var core = d.IndexOf("@CoreLines(card, members[0])", StringComparison.Ordinal);
        var battle = d.IndexOf("Group=\"@RailGroups.BattleDetail\"", StringComparison.Ordinal);
        var desc = d.IndexOf("Group=\"@RailGroups.Desc\"", StringComparison.Ordinal);
        Assert.That(name, Is.GreaterThan(0));
        Assert.That(risk, Is.GreaterThan(name));
        Assert.That(core, Is.GreaterThan(risk));
        Assert.That(battle, Is.GreaterThan(core));
        Assert.That(desc, Is.GreaterThan(battle));
        // 重要属性：健康（当前／上限）、护甲、闪避、速度、攻击能力、伤害依次，组卡等先写当前成员名
        var coreDef = Slice(d, "private RenderFragment CoreLines(", "    };");
        var at = 0;
        foreach (var label in new[] { "@if (ec.Name != card.Title) { <div class=\"wide rail-member\"><b>@ec.Name</b></div> }", "<span>健康</span><b>@Ui.N(m.Enemy?.Hp ?? ec.Hp)/@Ui.N(ec.Hp)</b>",
                     "<span>护甲</span>", "<span>闪避</span>", "<span>速度</span>", "<span>攻击能力</span>", "<span>伤害</span>" })
        {
            var i = coreDef.IndexOf(label, at, StringComparison.Ordinal);
            Assert.That(i, Is.GreaterThan(at - 1), label);
            at = i;
        }
        foreach (var no in new[] { "命中率", "开战武器", "Intents", "PlanFor", "StepOf" })
            Assert.That(coreDef, Does.Not.Contain(no), $"主区不放我方预估、不读未来意图：{no}");
        // 战斗详情：开战武器、空弹仓、感染、首位成员类别与命中、其他成员完整属性（原成员组并入）
        var fold = d.Substring(battle, d.IndexOf("</RailFold>", battle, StringComparison.Ordinal) - battle);
        foreach (var x in new[] { "<span>开战武器</span>", "弹仓为0", "单次咬伤感染概率", "@GameEngine.NonBiteNote", "@MemberLines(members[0], false)", "@foreach (var m in members.Skip(1))", "@MemberLines(m)" })
            Assert.That(fold, Does.Contain(x), x);
        Assert.That(d, Does.Not.Contain("RailGroups.Members"));
        var lines = Slice(d, "private RenderFragment MemberLines(", "private RenderFragment MemberKind(");
        foreach (var x in new[] { "@MemberKind(m)", "<span>对你命中率</span>", "<span>你的命中率</span>", "@GameEngine.NoAttackHitText" })
            Assert.That(lines, Does.Contain(x), x);
        // 说明：状态（无时长）、原操作规则、关键限制、特殊掉落
        var descFold = d.Substring(desc);
        foreach (var x in new[] { "@status", "@e.EnemyCardFearNote(s, card, false)", "@e.RetreatOutlookHere(s)", "@GameEngine.KeyEnemyDetailNote", "e.EliteDropNote(card)" })
            Assert.That(descFold, Does.Contain(x), x);
        // 中央盘：命中、伤害、感染、空弹仓、受惊仍在对应行动预估里
        var pops = Web("Components", "ActionPops.razor");
        foreach (var x in new[] { "咬伤可能感染", "弹仓为0：开战后需先装填（1AP）", "命中@(Ui.Pct(hit.HitChance))", "@e.EnemyCardFearNote(s, c, false)", "@GameEngine.NonBiteNote" })
            Assert.That(pops, Does.Contain(x), x);
        Assert.That(pops, Does.Not.Contain("意图里没有咬击"));
        Assert.That(d, Does.Not.Contain("意图里没有咬击"));
        Assert.That(GameEngine.NonBiteNote, Is.EqualTo("它不会咬伤，攻击不传播病毒。"));
        Assert.That(Verbatim(), Does.Contain("| 原确实无咬伤传播能力者 | " + GameEngine.NonBiteNote + " |"));
        Assert.That(Verbatim(), Does.Contain("| 敌踪右栏主属性 | 健康／护甲／闪避／速度／攻击能力／伤害；"));
    }

    /// <summary>Q639-A02：主动接近／撤退保护／旧档保护在卡面、悬停、右栏、中央盘、帮助和读屏均无剩余时长／截止／阶段；内部计时、保护与到期接近照旧。</summary>
    [Test]
    public void Q639_A02_NoCountdownAnyExit_InternalTimersKept()
    {
        var s = Field(3, "shambler");
        var key = GameEngine.SingleCardKey(s.WorldEnemies[0].Id);
        var card = E.FindEnemyCard(s, key, out _)!;
        void All(string what)
        {
            NoTimer(E.CardApproachStatus(s, card), what + "·状态");
            NoTimer(E.CardGuardNote(s, card), what + "·中央风险");
            foreach (var l in E.EnemyCardHoverLines(s, card)) NoTimer(l, what + "·悬停");
            NoTimer(E.EncounterNote(s, "field", ActionType.Explore), what + "·探索预览");
        }
        Assert.That(E.CardApproachStatus(s, card), Is.EqualTo("可能主动接近。"));
        All("未抽");
        SetLeft(s, key, 25);
        All("计时中");
        Assert.That(Left(s, key), Is.EqualTo(25), "读取不停表、不改值");
        SetLeft(s, key, 0);
        All("已到期");
        s.EnemyCards.Timers[key].GuardUntil = s.GameMinute + 60;
        Assert.That(E.CardApproachStatus(s, card), Is.EqualTo(GameEngine.RetreatGuardText));
        Assert.That(E.CardGuardNote(s, card), Is.EqualTo(GameEngine.RetreatGuardText));
        All("撤退保护");
        s.EncounterGraceUntil = s.GameMinute + 90;
        Assert.That(E.CardApproachStatus(s, card), Is.EqualTo(GameEngine.LegacyGraceText), "并存仍取较晚截止对应身份");
        All("旧档保护");
        Assert.That(s.EnemyCards.Timers[key].GuardUntil, Is.EqualTo(s.GameMinute + 60));
        Assert.That(s.EncounterGraceUntil, Is.EqualTo(s.GameMinute + 90), "内部截止不变");
        // 保护结束、到期：仍按原规则主动接近
        s.EnemyCards.Timers[key].GuardUntil = null;
        s.EncounterGraceUntil = 0;
        Assert.That(E.CheckEnemyApproach(s), Is.True, "到期且合法即主动接近（不靠停表隐藏）");
        // 五类无倒计时正式句与 R05 表逐字
        var r05 = File.ReadAllText(Directory.GetFiles(Path.Combine(Root(), "doc", "策划案"), "R05 敌人卡、种群刷新与遭遇系统.md", SearchOption.AllDirectories).Single());
        foreach (var t in new[] { GameEngine.MayApproachText, GameEngine.NeutralCardText, GameEngine.OptionalCardText, GameEngine.RetreatGuardText, GameEngine.LegacyGraceText })
            Assert.That(r05, Does.Contain($" | {t} |"), t);
        // 展示出口源码：不读计时字段、不格式化剩余时长
        var engine = Path.Combine(Root(), "src", "Core", "Engine");
        var status = Slice(File.ReadAllText(Path.Combine(engine, "GameEngine.EnemyApproach.cs")), "public string? CardApproachStatus(", "/// <summary>");
        var guard = Slice(File.ReadAllText(Path.Combine(engine, "GameEngine.EnemyCardsUi.cs")), "public string? CardGuardNote(", "/// <summary>");
        var preview = Slice(File.ReadAllText(Path.Combine(engine, "GameEngine.EnemyCards.cs")), "public string GroupCardPreviewNote(", "/// <summary>");
        foreach (var (src, what) in new[] { (status, "状态"), (guard, "撤退保护句"), (preview, "探索预览") })
            Assert.That(src, Does.Not.Contain("Clock.Duration"), what);
        foreach (var file in new[] { "EnemyDetail.razor", "LocationStrip.razor", "ActionPops.razor" })
        {
            var w = Web("Components", file);
            foreach (var bad in new[] { "GuardUntil", "EncounterGraceUntil", "CardTimer(", ".Left" })
                Assert.That(w, Does.Not.Contain(bad), $"{file}：{bad}");
        }
        var help = Web("Services", "HelpCatalog.cs");
        Assert.That(help, Does.Not.Contain("CardRetreatGuardMinutes"), "帮助不再透露撤退保护分钟数");
        Assert.That(help, Does.Not.Contain("接近倒计时").And.Not.Contain("接近时间"));
        Assert.That(help, Does.Contain("你可以先做其他事。\" + EnemyTimingHelp,"));
        Assert.That(help, Does.Contain("退回处的敌踪照常显示。\" + EnemyTimingHelp + \"失败则这一回合的行动点用尽。"));
    }

    /// <summary>Q639-A03：卡面直接给卡自身真实受阻原因（可换行、悬停读全）；守卫身份只来自真实守卫标记、不凭风险造 BOSS；保护卡仍可主动处理；不泄露未执行意图。</summary>
    [Test]
    public void Q639_A03_FaceReasonIdentityAndProtectedStillHandleable()
    {
        // 身份：真实守卫标记才「守卫·{原卡名}」，原名已含前缀不重复；其余原卡名
        Assert.That(GameEngine.EnemyRailIdentity(new EnemyCardView { Guardian = true, Title = "地牢守卫者" }), Is.EqualTo("守卫·地牢守卫者"));
        Assert.That(GameEngine.EnemyRailIdentity(new EnemyCardView { Guardian = true, Title = "守卫感染者" }), Is.EqualTo("守卫感染者"));
        Assert.That(GameEngine.EnemyRailIdentity(new EnemyCardView { Kind = "elite", Title = "强敌", Optional = true, Danger = true }), Is.EqualTo("强敌"), "可选强敌、高风险不造身份");
        Assert.That(GameEngine.EnemyRailIdentity(new EnemyCardView { Kind = "fixed", Title = GameEngine.FixedCardTitle }), Is.EqualTo(GameEngine.FixedCardTitle), "普通固定战不改称守卫");
        var cards = File.ReadAllText(Path.Combine(Root(), "src", "Core", "Engine", "GameEngine.EnemyCards.cs"));
        Assert.That(cards, Does.Contain("Badge = f.Guardian ? \"守卫\" : \"固定\", Danger = true, Guardian = f.Guardian,"), "守卫标记取固定战配置");
        // 卡面受阻原因：首战门控／固定关键限制；普通卡无
        Assert.That(GameEngine.EnemyCardFaceReason(new EnemyCardView { TutorialGated = true }), Is.EqualTo("先战胜农野荒地的游荡尸，再处理其他敌踪。"));
        Assert.That(GameEngine.EnemyCardFaceReason(new EnemyCardView { KeyEnemy = true }), Is.EqualTo(GameEngine.KeyEnemyDetailNote));
        Assert.That(GameEngine.EnemyCardFaceReason(new EnemyCardView()), Is.Null);
        var strip = Web("Components", "LocationStrip.razor");
        Assert.That(strip, Does.Contain("var faceWhy = GameEngine.EnemyCardFaceReason(c);"));
        var face = strip.IndexOf("@if (faceWhy != null) { <div class=\"oc-line ecard-status ecard-why\" title=\"@faceWhy\">@faceWhy</div> }", StringComparison.Ordinal);
        Assert.That(face, Is.GreaterThan(0));
        Assert.That(strip.IndexOf("@if (status != null) { <div class=\"oc-line ecard-status\" title=\"@status\">@status</div> }", StringComparison.Ordinal), Is.GreaterThan(face), "状态句不取代更优先的受阻原因");
        Assert.That(Web("wwwroot", "css", "cards.css"), Does.Contain(".ocard.k2 .k2-l2 > .oc-line.ecard-status { white-space: normal; overflow: visible; text-overflow: clip; overflow-wrap: anywhere; }"), "长句换行不藏");
        // 中央按钮的禁用／拒绝仍取内核真实原因
        var pops = Web("Components", "ActionPops.razor");
        Assert.That(pops, Does.Contain("disabled=\"@(whyApproach != null || busy)\" title=\"@whyApproach\""));
        Assert.That(pops, Does.Contain("@foreach (var r in reasons) { <div class=\"apop-risk ecard-why\">@r</div> }"));
        // 首战门控卡：悬停读全原因；处理拒绝同句
        var s = NewStory();
        var gated = E.EnemyCardsHere(s).FirstOrDefault(c => c.TutorialGated);
        if (gated != null)
        {
            Assert.That(E.EnemyCardHoverLines(s, gated), Does.Contain(GameEngine.TutorialGateReason));
            Assert.That(E.CanApproachCard(s, gated.Key), Is.EqualTo(GameEngine.TutorialGateReason));
        }
        // 撤退保护卡仍可主动处理（不把保护当新禁令）
        var f = Field(3, "shambler");
        var key = GameEngine.SingleCardKey(f.WorldEnemies[0].Id);
        SetLeft(f, key, 0);
        f.EnemyCards.Timers[key].GuardUntil = f.GameMinute + 60;
        Assert.That(E.CanApproachCard(f, key), Is.Null);
        Assert.That(E.ApproachCard(f, key).Ok, Is.True);
    }

    private static string Verbatim() => File.ReadAllText(Directory.GetFiles(Path.Combine(Root(), "doc", "策划案"), "重墨 V2 开工裁定与界面逐字稿.md", SearchOption.AllDirectories).Single());

    private static string Web(params string[] parts) => File.ReadAllText(Path.Combine(new[] { Root(), "web", "Survival.Web" }.Concat(parts).ToArray())).Replace("\r\n", "\n");

    private static string Slice(string text, string from, string to)
    {
        var a = text.IndexOf(from, StringComparison.Ordinal);
        Assert.That(a, Is.GreaterThanOrEqualTo(0), from);
        var b = text.IndexOf(to, a + from.Length, StringComparison.Ordinal);
        Assert.That(b, Is.GreaterThan(a), to);
        return text.Substring(a, b - a);
    }

    private static string Root()
    {
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "web", "Survival.sln"))) dir = dir.Parent;
        return dir!.FullName;
    }
}
