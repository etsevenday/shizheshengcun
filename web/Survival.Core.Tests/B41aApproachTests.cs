using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// B41a 里程碑 4：统一敌踪卡视图与「主动接近」入口（R05「Q417–Q419」「Q423」，逐字稿「新操作的禁用与拒绝」「Q427 卡名、角标」；
/// 验收 Q342-A01 内核、Q419-A01、Q423-A01、Q427-A01、Q428-A01 内核）。
/// </summary>
public class B41aApproachTests : B41aTestBase
{
    private GameState Field(int capacity, params string[] enemyIds)
    {
        var s = V2Field();
        UnlockCapacity(s, capacity);
        foreach (var id in enemyIds) E.DebugSpawnEnemy(s, id, "field");
        return s;
    }

    [Test]
    public void SingleCard_ApproachStartsBattleWithThatInstance_ZeroMinutes()
    {
        var s = Field(3, "shambler", "crawler", "stag");
        var ids = s.WorldEnemies.Select(w => w.Id).ToList();
        var cards = E.EnemyCardsHere(s);
        Assert.That(cards.Select(c => c.Key), Is.EqualTo(ids.Select(GameEngine.SingleCardKey)));
        Assert.That(cards.Select(c => c.Kind), Has.All.EqualTo("single"));
        Assert.That(cards.Select(c => c.Title), Is.EqualTo(new[] { "游荡尸", "爬行尸", "警戒雄鹿" }));
        Assert.That(cards.Select(c => c.Vigilance), Is.EqualTo(new[] { 1, 2, 5 }), "普通常驻列");
        Assert.That(cards.Select(c => c.Badge), Is.EqualTo(new[] { "敌对", "敌对", "中立" }));
        Assert.That(cards.Select(c => c.Danger), Has.All.False, "普通常驻不挂危险叠层");
        Assert.That(cards.Select(c => c.KeyEnemy || c.Optional || c.TutorialGated), Has.All.False);

        var minute = s.GameMinute;
        var morale = s.Stats.Morale;
        Assert.That(E.CanApproachCard(s, cards[1].Key), Is.Null);
        Assert.That(E.ApproachCard(s, cards[1].Key).Ok, Is.True);
        var b = s.Battle!;
        Assert.That(b.Enemies.Select(e => e.WorldId), Is.EqualTo(new[] { ids[1] }), "只与这一只真实实例交战");
        Assert.That(b.CardKey, Is.EqualTo(GameEngine.SingleCardKey(ids[1])));
        Assert.That(b.Context, Is.EqualTo(GameEngine.FearApproach));
        Assert.That(b.FixedKey, Is.Null);
        Assert.That(s.GameMinute, Is.EqualTo(minute), "主动接近 0 世界分钟");
        var crawler = Cfg.Enemies.Single(e => e.Id == "crawler");
        Assert.That(b.Fighter(GameState.PlayerId).FearChance, Is.EqualTo(crawler.FearChance * (1 - morale / 100) * 0.5).Within(1e-12), "受惊情境系数 0.5");

        Assert.That(E.CanApproachCard(s, cards[0].Key), Is.EqualTo("当前正在战斗。"));
        Assert.That(E.EnemyCardsHere(s).Select(c => c.Key), Is.EqualTo(new[] { cards[0].Key, cards[2].Key }), "交战中的卡不在可操作列表里");
    }

    [Test]
    public void GroupCard_FullLockedRosterEnters_MembersHaveNoSingleCard()
    {
        var s = Field(3, "shambler", "crawler", "runner", "worker");
        var ids = s.WorldEnemies.Select(w => w.Id).ToList();
        ClearField(s);
        var g = s.EnemyCards.Groups.Single();
        var cards = E.EnemyCardsHere(s);
        Assert.That(cards.Select(c => c.Key), Is.EqualTo(new[] { GameEngine.GroupCardKey(g.Id), GameEngine.SingleCardKey(ids[3]) }), "组成员不另出可交战单卡");
        var card = cards[0];
        Assert.That(card.Kind, Is.EqualTo("group"));
        Assert.That(card.Title, Is.EqualTo("游荡尸等3只"));
        Assert.That(card.Badge, Is.EqualTo("敌踪待处理"));
        Assert.That(card.Danger, Is.True);
        Assert.That(card.KeyEnemy, Is.False, "随机组可躲避");
        Assert.That(card.Members.Select(w => w.Id), Is.EqualTo(ids.Take(3)));
        Assert.That(card.Vigilance, Is.EqualTo(4), "组警惕取当前存活成员最高值（狂奔尸 4）");

        // 组员的单卡不可用（旧「接敌」入口 B41e3 已删）
        Assert.That(E.CanApproachCard(s, GameEngine.SingleCardKey(ids[0])), Is.EqualTo("这处敌踪已不在这里。"));
        Assert.That(E.Approach(s, ids[0]).Ok, Is.False);
        Assert.That(s.Battle, Is.Null);

        var minute = s.GameMinute;
        Assert.That(E.ApproachCard(s, card.Key).Ok, Is.True);
        var b = s.Battle!;
        Assert.That(b.Enemies.Select(e => e.WorldId), Is.EqualTo(ids.Take(3)), "按锁定名单全员入场");
        Assert.That(b.CardKey, Is.EqualTo(GameEngine.GroupCardKey(g.Id)));
        Assert.That(b.Context, Is.EqualTo(GameEngine.FearApproach));
        Assert.That(s.GameMinute, Is.EqualTo(minute));
        Assert.That(E.CanApproachCard(s, card.Key), Is.EqualTo("当前正在战斗。"));
    }

    [Test]
    public void Q423_A01_GroupVigilanceFollowsSurvivors_TitleFollowsFirstAlive()
    {
        var s = Field(3, "runner", "shambler", "crawler");
        var ids = s.WorldEnemies.Select(w => w.Id).ToList();
        ClearField(s);
        var key = GameEngine.GroupCardKey(s.EnemyCards.Groups.Single().Id);
        Assert.That(E.FindEnemyCard(s, key, out _)!.Vigilance, Is.EqualTo(4));
        Assert.That(E.ApproachCard(s, key).Ok, Is.True);
        KillInBattle(s, 1);   // 狂奔尸倒下
        RetreatOk(s);
        var card = E.FindEnemyCard(s, key, out _)!;
        Assert.That(card.Members.Select(w => w.Id), Is.EqualTo(new[] { ids[1], ids[2] }));
        Assert.That(card.Title, Is.EqualTo("游荡尸等2只"), "按锁定名单原顺序找首名存活者");
        Assert.That(card.Vigilance, Is.EqualTo(2), "按真实剩余成员更新（爬行尸 2）");
    }

    [Test]
    public void RefusalTable_TopDown()
    {
        var s = Field(3, "shambler", "crawler");
        var a = GameEngine.SingleCardKey(s.WorldEnemies[0].Id);
        var bKey = GameEngine.SingleCardKey(s.WorldEnemies[1].Id);
        Assert.That(E.CanApproachCard(s, a), Is.Null);

        // 卡已失效／不在本地
        Assert.That(E.CanApproachCard(s, "we:99999"), Is.EqualTo("这处敌踪已不在这里。"));
        Assert.That(E.CanApproachCard(s, "group:99"), Is.EqualTo("这处敌踪已不在这里。"));
        s.WorldEnemies[1].LocationId = "village";
        Assert.That(E.CanApproachCard(s, bKey), Is.EqualTo("这处敌踪已不在这里。"));
        s.WorldEnemies[1].LocationId = "field";

        // 隐藏
        s.WorldEnemies[1].HiddenUntil = s.GameMinute + 60;
        Assert.That(E.CanApproachCard(s, bKey), Is.EqualTo("这处敌踪暂时不可见。"));
        Assert.That(E.EnemyCardsHere(s).Select(c => c.Key), Is.EqualTo(new[] { a }));
        s.WorldEnemies[1].HiddenUntil = null;

        // 不在当前可操作区域（安全屋屋内）
        s.CurrentZone = GameEngine.SafehouseZone;
        Assert.That(E.CanApproachCard(s, a), Is.EqualTo("只能处理当前地点、当前区域可见的敌踪。"));
        Assert.That(E.EnemyCardsHere(s), Is.Empty);
        s.CurrentZone = GameEngine.ExteriorZone;

        // 目标已在交战
        s.WorldEnemies[1].Engaged = true;
        Assert.That(E.CanApproachCard(s, bKey), Is.EqualTo("这处敌踪正在交战。"));
        s.WorldEnemies[1].Engaged = false;

        // 强制物品整理（随身超过五张根卡）
        var extra = new[] { "cloth", "wood", "stone", "fiber", "bandage", "rope" }.Where(id => Cfg.Items.Any(i => i.Id == id)).ToList();
        var made = extra.SelectMany(id => E.CreateItems(s, id, 1, ItemInstance.Carried)).ToList();
        Assert.That(E.CarriedOverflow(s), Is.True);
        Assert.That(E.CanApproachCard(s, a), Is.EqualTo("请先完成随身物品整理。"));
        // 招募待安置排在整理之后
        s.Recruit.Pending = new RecruitTxn { AwaitingPlacement = true };
        Assert.That(E.CanApproachCard(s, a), Is.EqualTo("请先完成随身物品整理。"));
        foreach (var it in made) s.Items.Remove(it);
        Assert.That(E.CarriedOverflow(s), Is.False);
        Assert.That(E.CanApproachCard(s, a), Is.EqualTo("请先办完当前事务。"));
        s.Recruit.Pending = null;

        // 主行动进行中／暂停
        Start(s, ActionRequest.Rest(30));
        Assert.That(E.CanApproachCard(s, a), Is.EqualTo("当前行动还没结束。"));
        s.Main!.Status = ActionStatus.Paused;
        Assert.That(E.CanApproachCard(s, a), Is.EqualTo("行动暂停中，请先继续或退出行动。"));
        Assert.That(E.CanApproachCard(s, "we:99999"), Is.EqualTo("行动暂停中，请先继续或退出行动。"), "忙碌类原因排在卡失效之前");
        Assert.That(E.ApproachCard(s, a).Ok, Is.False);
        s.Main = null;

        // 已在战斗
        Assert.That(E.ApproachCard(s, a).Ok, Is.True);
        Assert.That(E.CanApproachCard(s, bKey), Is.EqualTo("当前正在战斗。"));
        Assert.That(E.ApproachCard(s, bKey).Reason, Is.EqualTo("当前正在战斗。"));

        // 终局最优先
        s.Progress.Dead = true;
        Assert.That(E.CanApproachCard(s, bKey), Is.EqualTo("本局已结束。"));
    }

    [Test]
    public void TutorialGate_OnlyTutorialCardOperable_TutorialVigilanceIs1()
    {
        var s = NewStory(11);
        ReadDialogues(s);
        Assert.That(GameEngine.TutorialGateOpen(s), Is.True);
        s.SpawnNextCheck = int.MaxValue;
        s.PatrolNextCheck = int.MaxValue;
        E.DebugTeleport(s, "field");
        var tutorialId = s.Story.TutorialEnemyId!.Value;
        var other = E.DebugSpawnEnemy(s, "shambler", "field");

        var cards = E.EnemyCardsHere(s).ToDictionary(c => c.Key);
        var tut = cards[GameEngine.SingleCardKey(tutorialId)];
        Assert.That(tut.KeyEnemy, Is.True, "教学敌属于固定关键敌踪（躲避禁用）");
        Assert.That(tut.TutorialGated, Is.False);
        Assert.That(tut.Vigilance, Is.EqualTo(1), "首战游荡尸为 1");
        var normal = cards[GameEngine.SingleCardKey(other.Id)];
        Assert.That(normal.TutorialGated, Is.True);
        Assert.That(normal.KeyEnemy, Is.False);
        Assert.That(normal.Vigilance, Is.EqualTo(1), "同种普通常驻取常驻列");
        Assert.That(E.CanApproachCard(s, normal.Key), Is.EqualTo("先战胜农野荒地的游荡尸，再处理其他敌踪。"));
        Assert.That(E.ApproachCard(s, normal.Key).Ok, Is.False);

        Assert.That(E.ApproachCard(s, tut.Key).Ok, Is.True);
        Assert.That(s.Battle!.Tutorial, Is.True);
        Assert.That(s.Battle.Context, Is.EqualTo(GameEngine.FearApproach), "按实际入战方式取受惊系数");
        Assert.That(s.Story.TutorialBattleId, Is.EqualTo(s.Battle.BattleId));

        // 未读强制对白排在战斗之前
        var s2 = NewStory(12);
        Assert.That(E.DialogueLocked(s2), Is.True);
        Assert.That(E.CanApproachCard(s2, "we:1"), Is.EqualTo("请先读完对话"));
    }

    private GameState AtNode(string node, int capacity, uint seed = 3001)
    {
        var s = New(seed);
        UnlockCapacity(s, capacity);
        s.GameMinute = At(2, 9, 0);
        s.SpawnNextCheck = int.MaxValue;
        s.PatrolNextCheck = int.MaxValue;
        E.DebugMapTeleport(s, node);
        return s;
    }

    [Test]
    public void FixedBattleCards_KeepRosterTitleAndReceipt()
    {
        // 检查哨拦路战：固定战成员列
        var s = AtNode("m02_checkpoint", 1);
        var card = E.EnemyCardsHere(s).Single(c => c.Kind == "fixed");
        Assert.That(card.Key, Is.EqualTo(GameEngine.FixedCardKey("m02_checkpoint")));
        Assert.That(card.Title, Is.EqualTo("拦路的感染者"));
        Assert.That(card.Badge, Is.EqualTo("固定"));
        Assert.That(card.KeyEnemy, Is.True, "固定关键敌人：躲避禁用");
        Assert.That(card.Optional, Is.False);
        Assert.That(card.EnemyIds, Is.EqualTo(new[] { "z_slow" }));
        Assert.That(card.Vigilance, Is.EqualTo(3), "迟缓感染者常驻 1、固定战成员 3");
        Assert.That(E.EnemyCardsHere(s).Any(c => c.Kind == "single" && c.Members.Any(w => w.FixedOf != null)), Is.False, "固定敌不另出单卡");
        Assert.That(E.FixedBlockHere(s), Is.Not.Null, "原资源限制仍在");

        Assert.That(E.ApproachCard(s, card.Key).Ok, Is.True);
        Assert.That(s.Battle!.FixedKey, Is.EqualTo("fixed:m02_checkpoint"));
        Assert.That(s.Battle.CardKey, Is.EqualTo("fixed:m02_checkpoint"));
        Assert.That(s.Battle.Context, Is.EqualTo(GameEngine.FearApproach));
        WinBattle(s);
        Assert.That(s.Progress.Flags, Does.Contain("checkpoint_obstruction_cleared"), "原胜利回执");
        Assert.That(E.EnemyCardsHere(s).Any(c => c.Kind == "fixed"), Is.False);
        Assert.That(E.CanApproachCard(s, card.Key), Is.EqualTo("这处敌踪已不在这里。"));

        // 守卫战：名单首名是守卫本体，随从取固定战成员列；旧入口转到卡
        var guard = Cfg.WorldEnemies.FixedBattles.First(f => f.Guardian);
        var g = AtNode(guard.NodeId, 4, 3002);
        var gc = E.EnemyCardsHere(g).Single(c => c.Kind == "fixed");
        Assert.That(gc.Badge, Is.EqualTo("守卫"));
        Assert.That(gc.Title, Is.EqualTo(Cfg.Enemies.Single(e => e.Id == guard.EnemyOrder[0]).Name), "固定战卡沿现有标题");
        Assert.That(gc.EnemyIds, Is.EqualTo(guard.EnemyOrder.Take(4)));
        var expected = guard.EnemyOrder.Take(4).Select((id, i) => E.VigilanceOf(id, i == 0 ? EnemyCardIdentity.GuardianBody : EnemyCardIdentity.FixedMember)).Max();
        Assert.That(gc.Vigilance, Is.EqualTo(expected));
        Assert.That(E.CanEngageFixed(g), Is.Null);
        Assert.That(E.EngageFixed(g).Ok, Is.True);
        Assert.That(g.Battle!.CardKey, Is.EqualTo(GameEngine.FixedCardKey(guard.NodeId)));
        Assert.That(g.Battle.Enemies.Select(e => e.EnemyId), Is.EqualTo(guard.EnemyOrder.Take(4)), "固定战保留原名单");
    }

    [Test]
    public void Q419_A01_OptionalElite_CreatedOnlyOnFirstHandling()
    {
        var cfg = Cfg.WorldEnemies.LegacyElites.Single(x => x.EnemyId == "z_guard");
        var s = New(5001);
        s.Progress.Flags.Add("hub_registered");
        s.GameMinute = At(3, 9, 0);
        s.SpawnNextCheck = int.MaxValue;
        s.PatrolNextCheck = int.MaxValue;
        E.DebugMapTeleport(s, cfg.NodeId);
        s.WorldEnemies.RemoveAll(w => w.FixedOf == null);
        var key = GameEngine.EliteCardKey(cfg.Id);
        Assert.That(E.CanApproachCard(s, key), Is.EqualTo("挑战还没有出现"), "关卡专属原因沿真实原句");
        E.DebugSetProgress(s, cfg.NodeId, 100);

        var card = E.EnemyCardsHere(s).Single();
        Assert.That(card.Key, Is.EqualTo(key));
        Assert.That(card.Kind, Is.EqualTo("elite"));
        Assert.That(card.Optional, Is.True);
        Assert.That(card.KeyEnemy, Is.False, "可选强敌可以躲避");
        Assert.That(card.Badge, Is.EqualTo("可选强敌"));
        Assert.That(card.Danger, Is.True);
        Assert.That(card.Title, Is.EqualTo("重装守卫感染者"));
        Assert.That(card.Vigilance, Is.EqualTo(6), "重装守卫感染者常驻 4、可选强敌 6");
        Assert.That(card.Members, Is.Empty, "未处理前不创建敌人");
        Assert.That(s.WorldEnemies, Is.Empty);
        Assert.That(E.IsBusy(s), Is.False, "未处理可选告示不挡其他事");
        Assert.That(E.CanStart(s, ActionRequest.Rest(30)), Is.Null);

        Assert.That(E.ApproachCard(s, key).Ok, Is.True);
        var st = s.Population.Elites[cfg.Id];
        var wid = st.WorldId!.Value;
        var locked = st.RewardWon;
        Assert.That(locked, Is.Not.Null, "首次实际处理锁定掉落抽签");
        Assert.That(s.Battle!.CardKey, Is.EqualTo(key));
        Assert.That(s.Battle.FixedKey, Is.EqualTo(key));
        Assert.That(s.Battle.Enemies.Single().WorldId, Is.EqualTo(wid));
        s.Battle.Enemies[0].Hp -= 1;
        var hp = s.Battle.Enemies[0].Hp;
        RetreatOk(s);
        E.DebugMapTeleport(s, cfg.NodeId);

        // 再次处理：同一敌人、同一 HP、同一抽签；旧入口转到卡
        var again = E.FindEnemyCard(s, key, out _)!;
        Assert.That(again.Members.Single().Id, Is.EqualTo(wid));
        Assert.That(again.Members.Single().Hp, Is.EqualTo(hp).Within(1e-9));
        Assert.That(E.EngageElite(s, cfg.Id).Ok, Is.True);
        Assert.That(s.Battle!.Enemies.Single().WorldId, Is.EqualTo(wid));
        Assert.That(s.Population.Elites[cfg.Id].RewardWon, Is.EqualTo(locked));
        Assert.That(s.WorldEnemies.Count(w => w.FixedOf == key), Is.EqualTo(1), "只创建一次");
        WinBattle(s);
        Assert.That(E.CanApproachCard(s, key), Is.EqualTo("本局已挑战成功"));
        Assert.That(E.EnemyCardsHere(s), Is.Empty);
    }
}
