using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// B41e2 新版敌踪界面二用到的内核只读数据与原句（开关开的配置，见 <see cref="B41aTestBase"/>）。
/// 出处：《重墨 V2 开工裁定与界面逐字稿》「B41e1 Q549—Q551 补充逐字稿」「B41e Q537—Q541 补充逐字稿」「B41 Q426–Q428 正式文案」队长与练习潜行、先手；
/// R05 Q537—Q541、Q549—Q551；Q446 ①⑩。
/// </summary>
public class B41e2Tests : B41cTestBase
{
    private EnemyGroupCard FormGroup(GameState s)
    {
        s.SuppressEnemyApproach = true;
        s.Locations["field"].ClearCharges = Math.Max(1, s.Locations["field"].ClearCharges);
        Start(s, ActionRequest.Clear());
        s.Main!.EncounterRoll = 0;
        Assert.That(Run(s), Is.EqualTo(SegmentStatus.Completed));
        return s.EnemyCards.Groups.Single();
    }

    private GameState EliteHere(uint seed = 5201)
    {
        var cfg = Cfg.WorldEnemies.LegacyElites.Single(x => x.EnemyId == "z_guard");
        var s = New(seed);
        s.Progress.Flags.Add("hub_registered");
        s.GameMinute = At(3, 9, 0);
        s.SpawnNextCheck = int.MaxValue;
        s.PatrolNextCheck = int.MaxValue;
        E.DebugMapTeleport(s, cfg.NodeId);
        s.WorldEnemies.RemoveAll(w => w.FixedOf == null);
        E.DebugSetProgress(s, cfg.NodeId, 100);
        return s;
    }

    private GameState EnemyApproached()
    {
        var (s, key) = OneShambler();
        SetLeft(s, key, 0);
        Assert.That(E.CheckEnemyApproach(s), Is.True);
        return s;
    }

    // ───────────── Q539 随机组卡副标与悬停 ─────────────

    [Test]
    public void Q539_A01_GroupSubtitleMembers_HoverFootnoteThenStatus()
    {
        var s = Field(3, "shambler", "shambler", "runner");
        var g = FormGroup(s);
        var card = E.EnemyCardHere(s, GameEngine.GroupCardKey(g.Id))!;
        var names = card.Members.Select(w => Cfg.Enemies.Single(x => x.Id == w.EnemyId).Name).ToList();
        Assert.That(E.EnemyCardMemberNames(card), Is.EqualTo(string.Join("、", names)), "锁定名单原序、「、」连接");
        Assert.That(names.Distinct().Count(), Is.LessThan(names.Count), "同名不合并");
        var st = E.CardApproachStatus(s, card);
        Assert.That(E.EnemyCardHoverLines(s, card), Is.EqualTo(new[] { GameEngine.EnemyCardFootnote }.Concat(st == null ? Array.Empty<string>() : new[] { st })));
        Assert.That(E.EnemyCardHoverLines(s, card), Has.No.Member(GameEngine.KeyEnemyDetailNote), "随机组卡不误套必须战胜说明");
        Assert.That(card.Title, Does.EndWith($"等{card.Members.Count}只"), "标题不变");
    }

    // ───────────── Q550 教学卡悬停补关键句 ─────────────

    [Test]
    public void Q550_A01_TutorialHover_FootnoteKeyThenStatus_Once()
    {
        var s = NewStory(3);
        ReadDialogues(s);
        s.SpawnNextCheck = int.MaxValue;
        s.PatrolNextCheck = int.MaxValue;
        Start(s, ActionRequest.Travel("field"));
        for (var i = 0; i < 200 && s.Main != null && !s.Progress.Dead; i++)
            if (E.Advance(s).Status == SegmentStatus.Paused) E.Resume(s);
        ReadDialogues(s);
        var card = E.EnemyCardHere(s, GameEngine.SingleCardKey(s.Story.TutorialEnemyId!.Value))!;
        Assert.That(card.KeyEnemy, Is.True);
        var lines = E.EnemyCardHoverLines(s, card);
        Assert.That(lines.Take(2), Is.EqualTo(new[] { GameEngine.EnemyCardFootnote, GameEngine.KeyEnemyDetailNote }));
        Assert.That(lines.Count(l => l == GameEngine.KeyEnemyDetailNote), Is.EqualTo(1), "只出现一次");
        Assert.That(lines.Skip(2), Is.EqualTo(new[] { E.CardApproachStatus(s, card) }.Where(x => x != null)), "最后接真实状态");
    }

    [Test]
    public void Q550_A01_ResidentAndEliteNoKeyLine()
    {
        var (s, key) = OneShambler();
        Assert.That(E.EnemyCardHoverLines(s, E.EnemyCardHere(s, key)!), Has.No.Member(GameEngine.KeyEnemyDetailNote));
        var es = EliteHere();
        Assert.That(E.EnemyCardHoverLines(es, E.EnemyCardsHere(es).Single()), Has.No.Member(GameEngine.KeyEnemyDetailNote));
    }

    // ───────────── Q551 可选强敌特殊掉落说明 ─────────────

    [Test]
    public void Q551_A01_EliteDropNote_HoverAndDetailSameSource_FirstHandleWording()
    {
        var s = EliteHere();
        var cfg = Cfg.WorldEnemies.LegacyElites.Single(x => x.EnemyId == "z_guard");
        Assume.That(cfg.RewardChance, Is.LessThan(1));
        var card = E.EnemyCardsHere(s).Single();
        var note = E.EliteDropNote(card);
        Assert.That(note, Is.EqualTo($"可选强敌 · 单局一次 · 特殊掉落{E.ItemName(cfg.RewardItemId)}×{cfg.RewardCount}（{Fmt.Pct(cfg.RewardChance)}，首次处理时抽定）"));
        Assert.That(note, Does.Not.Contain("首次接战时抽定").And.Not.Contain("已交过手"));
        Assert.That(E.EnemyCardHoverLines(s, card), Is.EqualTo(new[] { GameEngine.EnemyCardFootnote, note, "可选挑战，由你决定何时处理。" }));
        Assert.That(s.WorldEnemies.Any(w => w.FixedOf == card.Key), Is.False, "只读，不生成实例");
        Assert.That(s.Population.Elites.TryGetValue(cfg.Id, out var st) && st.RewardWon != null, Is.False, "不抽签");
        Assert.That(E.EliteNote(s, cfg), Does.Contain("首次接战时抽定"), "开关关的旧告示说明不变");
    }

    [Test]
    public void Q551_A01_NonEliteHasNoDropNote()
    {
        var (s, key) = OneShambler();
        Assert.That(E.EliteDropNote(E.EnemyCardHere(s, key)!), Is.Null);
    }

    // ───────────── Q549 受惊整卡每情境一行 ─────────────

    [Test]
    public void Q549_A01_FearOneLinePerContext_EvadeFailPrefixWithColon()
    {
        var s = Field(3, "shambler", "crawler", "runner");
        var g = FormGroup(s);
        var card = E.EnemyCardHere(s, GameEngine.GroupCardKey(g.Id))!;
        var group = E.EnemyCardPreviewMembers(s, card).Select(m => m.Enemy!).ToList();
        Assert.That(group.Count, Is.EqualTo(3));
        Assert.That(E.EnemyCardFearNote(s, card, false), Is.EqualTo(E.FearNote(s, group, GameEngine.FearApproach)), "主动／潜行共用 0.5 一行");
        Assert.That(E.EvadeFailFearLine(s, card), Is.EqualTo("躲避失败时：" + E.FearNote(s, group, GameEngine.FearEncounter)), "躲避失败 1.0 一行");
    }

    [Test]
    public void Q549_A01_KeyEnemyNoEvadeLine()
    {
        var s = New(3201);
        UnlockCapacity(s, 3);
        s.GameMinute = At(2, 9, 0);
        s.SpawnNextCheck = int.MaxValue;
        s.PatrolNextCheck = int.MaxValue;
        E.DebugMapTeleport(s, "m02_checkpoint");
        var card = E.EnemyCardsHere(s).Single(c => c.Kind == "fixed");
        Assert.That(E.EvadeFailFearLine(s, card), Is.Null, "固定关键敌人不能躲避，不造躲避风险分支");
    }

    [Test]
    public void Q549_A02_PreviewDoesNotConsumeRandom()
    {
        var s = Field(3, "shambler", "crawler");
        var g = FormGroup(s);
        var card = E.EnemyCardHere(s, GameEngine.GroupCardKey(g.Id))!;
        var before = s.Rng.State;
        for (var i = 0; i < 3; i++) { E.EnemyCardFearNote(s, card, false); E.EvadeFailFearLine(s, card); }
        Assert.That(s.Rng.State, Is.EqualTo(before), "反复开盘不抽签");
    }

    // ───────────── Q538 同卡保护长句 ─────────────

    [Test]
    public void Q538_A01_GuardNoteOnlyWhileGuardActive()
    {
        var (s, key) = OneShambler();
        E.CheckEnemyApproach(s);
        var card = E.EnemyCardHere(s, key)!;
        Assert.That(E.CardGuardNote(s, card), Is.Null);
        E.CardTimer(s, key)!.GuardUntil = s.GameMinute + 45;
        Assert.That(E.CardGuardNote(s, card), Is.EqualTo($"此敌踪撤退保护剩余{Clock.Duration(45)}，期间可主动处理。"));
        Assert.That(E.CardApproachStatus(s, card), Is.EqualTo($"撤退保护剩余{Clock.Duration(45)}。"), "卡面原短句保持");
        s.GameMinute += 45;
        Assert.That(E.CardGuardNote(s, card), Is.Null, "到期移除");
        s.EncounterGraceUntil = s.GameMinute + 30;
        Assert.That(E.CardGuardNote(s, card), Is.Null, "旧档历史保护不套此句");
    }

    // ───────────── Q540 练习盘身体预估 ─────────────

    [Test]
    public void Q540_A01_PracticeBodyRate_PractitionerOwnWaitRates()
    {
        var (s, _) = OneShambler();
        var r = Cfg.HourlyRates["wait"];
        var expect = new[] { ("饱食", r.Satiety), ("水分", r.Hydration), ("体力", r.Energy), ("清醒", r.Wakefulness), ("精神", r.Morale) }
            .Where(x => x.Item2 != 0).Select(x => x.Item1 + Fmt.SignedT(x.Item2));
        Assert.That(E.PracticeBodyRateText(s, GameState.PlayerId), Is.EqualTo("等待期间每小时 " + string.Join(" ", expect)));
        s.Player.DiarrheaUntil = s.GameMinute + 600;
        Assert.That(E.PracticeBodyRates(s, GameState.PlayerId)!.Hydration, Is.EqualTo(r.Hydration + Cfg.ItemActions.DiarrheaExtraHydrationPerHour).Within(1e-9), "适用修正按本人");
        Assert.That(E.PracticeBodyRateText(s, "nobody"), Is.Null, "无此人不给假数据");
        Assert.That(E.PracticeCandidates(s), Is.EqualTo(new[] { GameState.PlayerId }));
    }

    [Test]
    public void Q540_A02_PreviewDoesNotChargeOrChangeLeader()
    {
        var (s, _) = OneShambler();
        var st = s.Player.Stats.Satiety;
        var leader = E.SquadLeaderId(s);
        E.PracticeBodyRateText(s, GameState.PlayerId);
        E.PracticeXpPreview(s, GameState.PlayerId);
        Assert.That(s.Player.Stats.Satiety, Is.EqualTo(st));
        Assert.That(E.SquadLeaderId(s), Is.EqualTo(leader));
        Assert.That(E.PracticeMinutesText, Is.EqualTo("耗时30分钟"));
    }

    // ───────────── Q537 先手显示 ─────────────

    [Test]
    public void Q537_A02_EnemyFirstStrikeReadyUntilFirstAttack()
    {
        var s = EnemyApproached();
        var b = s.Battle!;
        var e = b.Enemies.Single();
        Assert.That(E.FirstStrikeReady(b, GameEngine.FirstStrikeKeyOf(e)), Is.True);
        Assert.That(E.FirstStrikeReady(b, GameState.PlayerId), Is.False, "敌方先手时我方没有");
        b.FirstStrikeDamage!.Clear();
        Assert.That(E.FirstStrikeReady(b, GameEngine.FirstStrikeKeyOf(e)), Is.False, "消耗后移除");
    }

    [Test]
    public void Q537_A03_EnemyIntentRangeIncludesFirstStrike_OnlyOneHanBracket()
    {
        var s = EnemyApproached();
        var b = s.Battle!;
        var e = b.Enemies.Single();
        e.Intent = "attack";
        var label = E.IntentLabel(e, b);
        Assert.That(label, Does.Contain("（含先手×1.5）"));
        e.NextHitFactor = 1.3;
        label = E.IntentLabel(e, b);
        Assert.That(label, Does.Contain("（含蓄势×1.3、先手×1.5）"), "与原项同一括注");
        e.NextHitFactor = 1;
        b.FirstStrikeDamage!.Clear();
        Assert.That(E.IntentLabel(e, b), Does.Not.Contain("先手"), "消耗后不再乘");
    }

    [Test]
    public void Q537_A03_RoundSpeed_Round1Only_OpeningParticipants()
    {
        var s = EnemyApproached();
        var b = s.Battle!;
        var e = b.Enemies.Single();
        var (sp, note) = E.EnemyRoundSpeed(b, e);
        Assert.That(sp, Is.EqualTo(b.Initiative[b.Queue.IndexOf(e.Index)]), "与行动队列同源");
        Assert.That(note, Is.EqualTo("（含先手×1.5）"));
        Assert.That(E.FirstStrikeRoundSpeedFactor(b, GameState.PlayerId), Is.EqualTo(1), "我方没有");
        b.Round = 2;
        Assert.That(E.EnemyRoundSpeed(b, e), Is.EqualTo((E.EnemyEffectiveSpeed(e), "")), "第二回合撤去");
    }

    [Test]
    public void Q537_Texts_Verbatim()
    {
        Assert.That(GameEngine.FirstStrikeReadyText, Is.EqualTo("先手伤害加成可用。"));
        Assert.That(GameEngine.FirstStrikeTag(1.5), Is.EqualTo("先手×1.5"));
        Assert.That((GameEngine.SquadLeaderLabel, GameEngine.SquadLeaderNote), Is.EqualTo(("小队队长", "潜行和躲避使用小队队长的潜行等级、清醒和精神进行判定。")));
        Assert.That((GameEngine.PracticeTitle, GameEngine.PracticeActorLabel, GameEngine.PracticeStart, GameEngine.PracticeBodyLabel),
            Is.EqualTo(("练习潜行", "练习者", "开始练习", "真实身体预估")));
        Assert.That(GameEngine.PracticeNote, Is.EqualTo("练习按等待消耗身体状态。做满30分钟才获得经验，退出不退已耗时间。"));
    }

    // ───────────── Q446 ⑩ 生存提示 ─────────────

    [Test]
    public void Q446_10_TipReplacedOnlyWhenCardsV2()
    {
        var tip = Cfg.Progress.Tips.Single(t => t.EndsWith(GameEngine.TipOldEncounterClause));
        var (s, _) = OneShambler();
        var shown = E.TipForState(s, tip);
        Assert.That(shown, Does.EndWith("可以主动接近、潜行接近或躲避。").And.Not.Contain("绕行"));
        Assert.That(shown.Substring(0, shown.Length - "可以主动接近、潜行接近或躲避。".Length), Is.EqualTo(tip.Substring(0, tip.Length - GameEngine.TipOldEncounterClause.Length)), "原句式其余部分不动");
        var off = LegacyField();
        Assert.That(Off.TipForState(off, tip), Is.EqualTo(tip), "开关关原样");
    }
}
