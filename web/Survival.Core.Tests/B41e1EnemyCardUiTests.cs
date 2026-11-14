using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// B41e1 新版敌踪界面一（敌踪卡、居中操作盘、敌踪详情）用到的内核只读数据与原句（B41e3 起新版敌踪是唯一流程，见 <see cref="B41aTestBase"/>）。
/// 原句出处：《重墨 V2 开工裁定与界面逐字稿》「B41 Q426–Q428 正式文案」卡片、操作盘与倒计时；Q446 ④⑥；Q447。验收 B41-TEXT02（卡状态句部分）。
/// </summary>
public class B41e1EnemyCardUiTests : B41cTestBase
{
    private GameState AtNode(string node, int capacity, uint seed)
    {
        var s = New(seed);
        UnlockCapacity(s, capacity);
        s.GameMinute = At(2, 9, 0);
        s.SpawnNextCheck = int.MaxValue;
        s.PatrolNextCheck = int.MaxValue;
        E.DebugMapTeleport(s, node);
        return s;
    }

    /// <summary>清理一次并让开工时锁定的随机遭遇判定值必中，形成一张随机组卡（同 B41a2CardViewTests）。</summary>
    private EnemyGroupCard FormGroup(GameState s)
    {
        s.SuppressEnemyApproach = true;
        s.Locations["field"].ClearCharges = Math.Max(1, s.Locations["field"].ClearCharges);
        Start(s, ActionRequest.Clear());
        s.Main!.EncounterRoll = 0;
        Assert.That(Run(s), Is.EqualTo(SegmentStatus.Completed));
        return s.EnemyCards.Groups.Single();
    }

    [Test]
    public void DiskTexts_Verbatim()
    {
        var (s, key) = OneShambler();
        Assert.That(GameEngine.EnemyDiskLabels, Is.EqualTo(new[] { "警惕", "接近", "队长", "成功率", "耗时" }));
        Assert.That(GameEngine.VigilanceText(3), Is.EqualTo("警惕3级"));
        Assert.That(E.SquadLeaderText(s), Is.EqualTo("队长：你"), "主角写「你」");
        Assert.That(GameEngine.StealthRateText(55), Is.EqualTo("成功率55%"));
        Assert.That(E.StealthCheckMinutesText, Is.EqualTo("耗时5分钟"));
        Assert.That((GameEngine.ApproachCardButton, GameEngine.StealthActionName(GameEngine.SneakKind), GameEngine.StealthActionName(GameEngine.EvadeKind), GameEngine.EnemyDiskBack),
            Is.EqualTo(("主动接近", "潜行接近", "躲避", "返回")));
        Assert.That(E.EnemyCardHere(s, key), Is.Not.Null);
    }

    [Test]
    public void SingleCard_HoverAndStatus_Q446_6()
    {
        var (s, key) = OneShambler();
        var card = E.EnemyCardHere(s, key)!;
        // BM5b1（R05 Q639）：还没抽／已抽接近时间都只写无时长句
        Assert.That(E.CardApproachStatus(s, card), Is.EqualTo(GameEngine.MayApproachText), "还没抽接近时间");
        Assert.That(E.EnemyCardHoverLines(s, card), Is.EqualTo(new[] { GameEngine.EnemyCardFootnote, GameEngine.MayApproachText }));
        E.CheckEnemyApproach(s);
        var st = E.CardApproachStatus(s, card);
        Assert.That(st, Is.EqualTo(GameEngine.MayApproachText));
        Assert.That(E.EnemyCardHoverLines(s, card), Is.EqualTo(new[] { GameEngine.EnemyCardFootnote, st }), "常驻卡不补必须战胜说明、不承诺不会主动开战");
        Assert.That(E.EnemyCardPreviewMembers(s, card).Select(m => m.Enemy!.Id), Is.EqualTo(new[] { s.WorldEnemies.Single().Id }));
    }

    [Test]
    public void SingleCard_Neutral_StatusLine()
    {
        var s = Field(1, "stag");
        var w = s.WorldEnemies.Single();
        Assume.That(w.Hostile, Is.False, "雄鹿默认中立");
        var card = E.EnemyCardHere(s, Key(w))!;
        Assert.That(card.Badge, Is.EqualTo("中立"));
        Assert.That(E.EnemyCardHoverLines(s, card), Is.EqualTo(new[] { GameEngine.EnemyCardFootnote, "中立，受到攻击后才转为敌对。" }));
    }

    [Test]
    public void FixedCard_HoverAddsMustWin_PreviewInLockedOrder()
    {
        var s = AtNode("m02_checkpoint", 3, 3201);
        var card = E.EnemyCardsHere(s).Single(c => c.Kind == "fixed");
        var lines = E.EnemyCardHoverLines(s, card);
        Assert.That(lines.Take(2), Is.EqualTo(new[] { GameEngine.EnemyCardFootnote, GameEngine.KeyEnemyDetailNote }));
        Assert.That(lines.Skip(2), Is.EqualTo(new[] { E.CardApproachStatus(s, card) }.Where(x => x != null)));
        Assert.That(card.KeyEnemy, Is.True, "详情补句与躲避禁用都按关键敌人");
        Assert.That(E.EnemyCardPreviewMembers(s, card).Select(m => m.Enemy!.Id), Is.EqualTo(s.Population.Fixed["m02_checkpoint"].WorldIds.Where(id => card.Members.Any(w => w.Id == id))),
            "按锁定名单原顺序");
        Assert.That(E.CanStealthCard(s, card.Key, GameEngine.EvadeKind), Is.EqualTo("这是必须战胜的敌踪，无法躲避。"), "盘内躲避的禁用原因");
    }

    [Test]
    public void EliteCard_NotInstantiated_PreviewFromChallenge_HoverStatus()
    {
        var cfg = Cfg.WorldEnemies.LegacyElites.Single(x => x.EnemyId == "z_guard");
        var s = New(5201);
        s.Progress.Flags.Add("hub_registered");
        s.GameMinute = At(3, 9, 0);
        s.SpawnNextCheck = int.MaxValue;
        s.PatrolNextCheck = int.MaxValue;
        E.DebugMapTeleport(s, cfg.NodeId);
        s.WorldEnemies.RemoveAll(w => w.FixedOf == null);
        E.DebugSetProgress(s, cfg.NodeId, 100);
        var card = E.EnemyCardsHere(s).Single();
        Assert.That(card.Kind, Is.EqualTo("elite"));
        Assert.That(E.EnemyCardHoverLines(s, card), Is.EqualTo(new[] { GameEngine.EnemyCardFootnote, E.EliteDropNote(card), "可选强敌，不会主动接近。" }), "B41e2（Q551）：通用句后补特殊掉落说明");
        var members = E.EnemyCardPreviewMembers(s, card);
        Assert.That(members.Select(m => (m.EnemyId, m.Enemy)), Is.EqualTo(new[] { ("z_guard", (WorldEnemy?)null) }));
        Assert.That(E.EnemyCardFearNote(s, card, false), Is.Not.Empty);
        Assert.That(s.WorldEnemies.Any(w => w.FixedOf == card.Key), Is.False, "预览不创建敌人");
    }

    [Test]
    public void GroupCard_Hover_Q539_PreviewLockedOrder_FearContexts_Q447()
    {
        var s = Field(3, "shambler", "crawler", "runner", "worker");
        var g = FormGroup(s);
        var card = E.EnemyCardHere(s, GameEngine.GroupCardKey(g.Id))!;
        Assert.That(card.Badge, Is.EqualTo("敌踪待处理"));
        Assert.That(card.Danger, Is.True);
        Assert.That(E.EnemyCardHoverLines(s, card), Is.EqualTo(new[] { GameEngine.EnemyCardFootnote }.Concat(new[] { E.CardApproachStatus(s, card) }.Where(x => x != null))), "B41e2（Q539）：通用句接同一状态句");
        var members = E.EnemyCardPreviewMembers(s, card);
        Assert.That(members.Select(m => m.Enemy!.Id), Is.EqualTo(g.MemberIds), "按锁定名单原顺序逐个成员");
        var group = members.Select(m => m.Enemy!).ToList();
        Assert.That(E.EnemyCardFearNote(s, card, false), Is.EqualTo(E.FearNote(s, group, GameEngine.FearApproach)), "主动接近／潜行按 0.5");
        Assert.That(E.EnemyCardFearNote(s, card, true), Is.EqualTo(E.FearNote(s, group, GameEngine.FearEncounter)), "躲避按失败入战 1.0");
    }

    [Test]
    public void EnemyCardHere_RevalidatesLikeCardsHere_Q457()
    {
        var (s, key) = OneShambler();
        Assert.That(E.EnemyCardHere(s, key), Is.Not.Null);
        Assert.That(E.EnemyCardHere(s, ""), Is.Null);
        Assert.That(E.EnemyCardHere(s, "we:999"), Is.Null);
        Assert.That(E.EnemyCardHere(s, "group:99"), Is.Null);
        var w = s.WorldEnemies.Single();
        w.EvadeHiddenUntil = s.GameMinute + 720;
        Assert.That(E.EnemyCardHere(s, key), Is.Null, "隐藏中不可操作");
        w.EvadeHiddenUntil = null;
        s.WorldEnemies.Clear();
        Assert.That(E.EnemyCardHere(s, key), Is.Null, "卡已失效");
    }

    [Test]
    public void StealthSubmit_WithShownLeaderAndRate_ChangedRejected()
    {
        var (s, key) = OneShambler();
        var card = E.EnemyCardHere(s, key)!;
        var leader = E.SquadLeaderId(s);
        var rate = E.StealthRate(s, GameEngine.SneakKind, leader, card.Vigilance);
        Assert.That(E.CanStealthCard(s, key, GameEngine.SneakKind, leader, rate + 1), Is.EqualTo("当前情况已变化，请重新选择后再操作。"), "盘里显示的成功率过期");
        Assert.That(E.StartStealth(s, key, GameEngine.SneakKind, "r1", leader, rate).Ok, Is.True);
    }
}
