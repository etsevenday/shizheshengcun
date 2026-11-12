using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 批次 B26（R7 Q227，取代 Q26 的操作台隐藏范围）。规则来源：progression-adjudications.json operatorDisplayQ227、
/// 合同 content-feeding-adjudications.json skillPolicy；验收 PLAN-Q227-01、PLAN-Q227-02（内核部分：卡片数据、不可执行、成长页不变）。
/// </summary>
public class B26OperatorSkillTests : BattleTestBase
{
    private static readonly int[] Levels = { 1, 3, 5, 7, 10 };

    private List<OperatorSkillCard> Cards(GameState s) => E.OperatorSkillCards(s, E.BattleOptions(s));

    private void Ready(GameState s) => ToPlayer(s);

    /// <summary>PLAN-Q227-02：每种武器（含徒手）当前门类恰五卡，按 1/3/5/7/10 排序、同一门类，不漏卡、不跨门。</summary>
    [Test]
    public void Q227_EveryWeapon_ExactlyFiveCards_InOrder()
    {
        foreach (var w in Cfg.Weapons)
        {
            var nodes = Cfg.SkillNodes.Where(n => n.SkillId == w.SkillId && !n.IsSurvival).OrderBy(n => n.Level).ToList();
            Assert.That(nodes.Select(n => n.Level), Is.EqualTo(Levels), $"{w.Id}（{w.SkillId}）五节点");
        }
        var s = Fight(731, "feral_dog");
        Ready(s);
        var cards = Cards(s);
        Assert.That(E.CurrentWeapon(s, out _).SkillId, Is.EqualTo(cards[0].Node.SkillId));
        Assert.That(cards.Select(c => c.Node.Level), Is.EqualTo(Levels));
        Assert.That(cards.Select(c => c.Node.SkillId).Distinct().Count(), Is.EqualTo(1));
    }

    /// <summary>
    /// PLAN-Q227-01/02：未解锁灰卡写所需与当前等级，不进指令列表；点执行（直接发指令）被拒、不扣 AP/体力、不进冷却、不伤敌。
    /// </summary>
    [Test]
    public void Q227_LockedCard_ShowsCondition_NotExecutable()
    {
        var s = Fight(732, "feral_dog");
        Ready(s);
        var w = E.CurrentWeapon(s, out _);
        E.DebugSetSkillLevel(s, w.SkillId, 3);
        var cards = Cards(s);
        var name = E.SkillName(w.SkillId);
        Assert.That(cards.Take(2).All(c => c.LockedText == null));
        foreach (var c in cards.Skip(2))
        {
            Assert.That(c.Option, Is.Null, "灰卡不是指令");
            Assert.That(c.LockedText, Is.EqualTo($"需要{name}{c.Node.Level}级（当前3级）"));
            Assert.That(c.Node.Text, Is.Not.Empty, "公开说明可读");
        }
        var opts = E.BattleOptions(s);
        Assert.That(opts.Select(o => o.NodeId).Intersect(cards.Skip(2).Select(c => c.Node.Id)), Is.Empty, "SkillOptions 不变");

        var locked = cards.First(c => c.LockedText != null && E.SkillExecOf(c.Node.Id) != null).Node;
        var target = s.Battle!.Alive.First();
        var cmd = BattleCommand.Of(locked.Id, target.Index);
        Assert.That(E.CommandBlocked(s, cmd), Is.EqualTo(E.SkillLockedText(s, locked)), "执行拒绝与灰卡同一句");
        Assert.That(E.PreviewCommand(s, cmd).Ok, Is.False);
        var snap = (s.Battle.PlayerAp, s.Stats.Energy, target.Hp, s.Battle.Round);
        var r = E.ExecuteCommand(s, cmd);
        Assert.That(r.Ok, Is.False);
        Assert.That((s.Battle.PlayerAp, s.Stats.Energy, target.Hp, s.Battle.Round), Is.EqualTo(snap), "不耗 AP、体力，不给效果");
    }

    /// <summary>真实解锁后灰卡变指令，技能等级不被显示改动；已解锁但 AP 不足仍显示并写真因（不当成灰卡）。</summary>
    [Test]
    public void Q227_UnlockedButUnavailable_KeepsRealReason()
    {
        var s = Fight(733, "feral_dog");
        Ready(s);
        var w = E.CurrentWeapon(s, out _);
        var before = E.SkillLevel(s, w.SkillId);
        _ = Cards(s);
        Assert.That(E.SkillLevel(s, w.SkillId), Is.EqualTo(before), "显示不改技能");
        E.DebugSetSkillLevel(s, w.SkillId, 10);
        var cards = Cards(s);
        Assert.That(cards.All(c => c.LockedText == null), Is.True);
        s.Battle!.PlayerAp = 0;
        var active = Cards(s).Where(c => c.Option != null).ToList();
        Assert.That(active, Is.Not.Empty);
        Assert.That(active.All(c => !c.Option!.Available && c.Option.Reason != null));
        Assert.That(active.Select(c => c.Option!.Reason), Has.Some.StartsWith("行动点不足"));
    }

    /// <summary>切到第二页武器（另一门）：五卡换成那一门，不跨门混用；空槽仍是拳门五项。</summary>
    [Test]
    public void Q227_FollowsCurrentPage_FistsOnEmptySlot()
    {
        var s = Fight(734, "feral_dog");
        Ready(s);
        foreach (var page in new[] { 1, 2 })
        {
            E.WeaponOnPage(s, page, out var w);
            s.Battle!.ActiveWeaponSlot = page;
            var cards = Cards(s);
            Assert.That(cards.Select(c => c.Node.SkillId).Distinct(), Is.EqualTo(new[] { w.SkillId }));
            Assert.That(cards.Select(c => c.Node.Level), Is.EqualTo(Levels));
        }
    }

    /// <summary>旧在办战斗的 AP、冷却不因显示变化改动；成长页 15 门 75 节点不变。</summary>
    [Test]
    public void Q227_DisplayOnly_GrowthStays75()
    {
        var s = Fight(735, "feral_dog");
        Ready(s);
        var json = Survival.Core.Save.SaveSerializer.Serialize(s);
        _ = Cards(s);
        Assert.That(Survival.Core.Save.SaveSerializer.Serialize(s), Is.EqualTo(json), "只读");
        var views = E.SkillViews(s);
        Assert.That(views.Count, Is.EqualTo(E.SkillsInPlay.Count()), "16 门（B41e3 起含潜行，潜行没有节点）");
        Assert.That(views.Sum(v => v.Nodes.Count), Is.EqualTo(75));
    }
}
