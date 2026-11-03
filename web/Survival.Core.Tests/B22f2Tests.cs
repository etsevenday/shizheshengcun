using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 批次 B22f2：策划答复 Q208（选 A）、Q209（选 B）（策划提交 765b304）。规则出处：更新说明 2026-11-03「Q208-Q209」
/// （TUT-Q208-01/02、APP-Q209-01/02）、00 总则/02 开发方待确认问题 Q208/Q209 策划答复。
/// </summary>
public class B22f2Tests : BattleTestBase
{
    private void Ok(ActionResult r) => Assert.That(r.Ok, Is.True, r.Reason);

    private const string Fight = "先战胜检查哨拦路敌人。";
    private const string Gate = "h01_gate";
    private const string M02 = "m02_checkpoint";

    /// <summary>序章新局：首战已胜、已知营区路线，主目标 route_onward，人在 m01（还没到检查哨）。</summary>
    private GameState RouteOnwardAtM01()
    {
        var s = NewStory();
        ReadDialogues(s);
        E.DebugPrologue(s, "won");
        ReadDialogues(s);
        s.Progress.Flags.Add(GameEngine.FactCampRouteKnown);
        s.Progress.Flags.Add(GameEngine.FactVisitorMet);
        s.SpawnNextCheck = int.MaxValue;
        s.PatrolNextCheck = int.MaxValue;
        E.DebugMapTeleport(s, "m01_road");
        ReadDialogues(s);
        E.PrologueBoundary(s);
        Assert.That(s.Prologue.ObjectiveId, Is.EqualTo("route_onward"));
        return s;
    }

    private GameState RouteOnwardAtM02()
    {
        var s = RouteOnwardAtM01();
        E.DebugMapTeleport(s, M02);
        ReadDialogues(s);
        E.PrologueBoundary(s);
        Assert.That(E.FixedBlockHere(s), Is.Not.Null, "拦路战未胜");
        Assert.That(s.Locations[Gate].Discovered, Is.False, "营门未知");
        return s;
    }

    // ───────────── Q208 选 A：m02 拦路战未胜时序章下一步 ─────────────

    /// <summary>
    /// TUT-Q208-01：m02 / route_onward / 营门未知 / 拦路战未胜时逐字显示“先战胜检查哨拦路敌人。”，不点亮探索或敌卡（探索度满 100 也一样，
    /// 覆盖 Q139 的探索句与查询句）；胜利提交后恢复真实已知可走营门句并点亮营门；其他目标、其他地点、下一段已知、已胜走原规则。
    /// </summary>
    [Test]
    public void TUT_Q208_01_M02_FightFirstStep_ThenRestore()
    {
        // 其他地点：m01 不用本句
        var s = RouteOnwardAtM01();
        Assert.That(E.CurrentObjective(s)!.Step, Is.Not.EqualTo(Fight), "不在 m02 走原分支");

        s = RouteOnwardAtM02();
        Assert.That(E.NextTravelSegment(s)!.Kind, Is.EqualTo(TravelSegmentKind.UnknownFixedBlocked));
        Assert.That(E.CurrentObjective(s)!.Step, Is.EqualTo(Fight));
        Assert.That(E.ObjectiveFocus(s), Is.Null, "不点亮探索入口或拦路敌卡");
        // 探索度满 100（营门仍未发现）：仍是本句，不回到“查看当前已知路线与通行条件。”
        s.Locations[M02].Progress = 100;
        Assert.That(E.CurrentObjective(s)!.Step, Is.EqualTo(Fight), "覆盖查询句");
        Assert.That(E.ObjectiveFocus(s), Is.Null);
        s.Locations[M02].Progress = 0;

        // 其他目标：不是 route_onward 不用本句
        var keep = s.Prologue.ObjectiveId;
        s.Prologue.ObjectiveId = "PRO04";
        Assert.That(E.NextTravelSegment(s)?.Kind, Is.Not.EqualTo(TravelSegmentKind.UnknownFixedBlocked), "其他目标走原规则");
        s.Prologue.ObjectiveId = keep;

        // 下一段已知（营门已发现、路线已揭示但未开通）：不改成战斗句，仍是 Q139 已知未开通原分支
        s.Locations[M02].Progress = 100;
        s.Locations[Gate].Discovered = true;
        var seg = E.NextTravelSegment(s)!;
        Assert.That(seg.Kind, Is.EqualTo(TravelSegmentKind.KnownBlocked));
        Assert.That(E.CurrentObjective(s)!.Step, Is.Not.EqualTo(Fight));
        Assert.That(E.ObjectiveFocus(s), Is.EqualTo("loc:" + Gate));
        s.Locations[Gate].Discovered = false;
        s.Locations[M02].Progress = 0;
        Assert.That(E.CurrentObjective(s)!.Step, Is.EqualTo(Fight));

        // 胜利提交：清障揭出营门路线，恢复“沿已知路线前往营门。”
        E.DebugWinFixedHere(s);
        Assert.That(E.FixedBlockHere(s), Is.Null);
        Assert.That(E.NextTravelSegment(s)!.Kind, Is.EqualTo(TravelSegmentKind.KnownOpen));
        Assert.That(E.CurrentObjective(s)!.Step, Is.EqualTo("沿已知路线前往营门。"));
        Assert.That(E.ObjectiveFocus(s), Is.EqualTo("loc:" + Gate));
    }

    /// <summary>
    /// TUT-Q208-02：提示只读（0 分钟 0 资源，不揭路、不开门、不改拦路敌人）；旧档已有可信清障事实不反锁、不出本句；移动途中不先换目的地、
    /// 到达前不出本句；本句不拦接战（不造新入口，战斗照原入口开），战斗中拦路战未胜时仍保持本句、提示不改战斗。
    /// </summary>
    [Test]
    public void TUT_Q208_02_ReadOnly_OldSave_Moving_Battle()
    {
        var s = RouteOnwardAtM02();
        var minute = s.GameMinute;
        var items = s.Items.Sum(i => i.Count);
        var edges = s.UnlockedEdges.Count;
        var hp = E.FixedEnemiesAt(s, M02).Single().Hp;
        for (var k = 0; k < 3; k++) { E.CurrentObjective(s); E.ObjectiveFocus(s); E.NextTravelSegment(s); }
        Assert.That((s.GameMinute, s.Items.Sum(i => i.Count), s.UnlockedEdges.Count), Is.EqualTo((minute, items, edges)), "0 分钟 0 资源，不开路");
        Assert.That(s.Locations[Gate].Discovered, Is.False, "不提前揭营门");
        Assert.That(E.FixedEnemiesAt(s, M02).Single().Hp, Is.EqualTo(hp), "不代打");
        Assert.That(s.Progress.Flags, Does.Not.Contain("checkpoint_obstruction_cleared"));

        // 本句不拦接战；战斗中（未胜）仍是本句，不改战斗
        Assert.That(E.CanEngageFixed(s), Is.Null);
        Ok(E.EngageFixed(s));
        Assert.That(s.Battle, Is.Not.Null);
        Assert.That(E.CurrentObjective(s)!.Step, Is.EqualTo(Fight));
        Assert.That(s.Battle, Is.Not.Null);

        // 旧档已有可信清障事实：不生成拦路战、不出本句
        var old = RouteOnwardAtM01();
        old.Progress.Flags.Add("checkpoint_obstruction_cleared");
        E.DebugMapTeleport(old, M02);
        ReadDialogues(old);
        E.PrologueBoundary(old);
        Assert.That(E.FixedBlockHere(old), Is.Null, "旧清障不反锁");
        Assert.That(E.CurrentObjective(old)!.Step, Is.Not.EqualTo(Fight));

        // 移动途中（m01 → m02 还没到）：不出本句，目的地不变
        var mv = RouteOnwardAtM01();
        var edge = E.EdgeBetween("m01_road", M02)!;
        if (!mv.UnlockedEdges.Contains(edge.Id)) mv.UnlockedEdges.Add(edge.Id);
        mv.Locations[M02].Discovered = true;
        Start(mv, ActionRequest.Travel(M02));
        Steps(mv, 1);
        Assert.That(mv.Main, Is.Not.Null, "仍在路上");
        var dest = mv.Main!.TargetId;
        Assert.That(E.CurrentObjective(mv)!.Step, Is.Not.EqualTo(Fight), "到达前不出本句");
        Assert.That(mv.Main!.TargetId, Is.EqualTo(dest), "不改进行中移动目的地");
        Run(mv);
        ReadDialogues(mv);
        Assert.That(mv.CurrentLocation, Is.EqualTo(M02));
        E.PrologueBoundary(mv);
        Assert.That(E.CurrentObjective(mv)!.Step, Is.EqualTo(Fight), "实际到达后才出本句");
    }

    // ───────────── Q209 选 B：教学样本看完免费说明即算已鉴定 ─────────────

    private const string Sub = "active_substance_white_unidentified";

    private ItemInstance Sample(GameState s, string owner, string grant, bool tutorial, int count = 1) =>
        E.CreateItems(s, Sub, count, owner, false, new ItemSource { GrantEventId = grant, TutorialReward = tutorial }).Single();

    /// <summary>
    /// APP-Q209-01：一份教学样本首次成功看免费说明后有本实例 fixed_tutorial_attack 记录，名称“活性物质（白色·已鉴定·首战教学）”、移入 tutapr 卡；
    /// 0 分钟 0 纤维、不耗样本；同地其他未读教学样本（含同堆其余份）仍未鉴定，普通样本不被改。地面、随身、面板与新日志同名。
    /// </summary>
    [Test]
    public void APP_Q209_01_FreeNote_IdentifiesOnlySelectedInstance()
    {
        var s = New();
        s.Items.RemoveAll(i => i.ItemId == Sub);
        s.Items.RemoveAll(i => i.ItemId == "fiber");
        E.CreateItems(s, "fiber", 1, ItemInstance.Carried);
        var g = s.Ground;
        var t1 = Sample(s, g, "tutorial-reward-b1", true);
        var t2 = Sample(s, g, "dev-tutorial-x", true);
        var o = Sample(s, g, "g-o", false);
        var total = s.Items.Where(i => i.ItemId == Sub).Sum(i => i.Count);
        var minute = s.GameMinute;
        Assert.That(E.InstanceName(t1), Is.EqualTo("未鉴定的活性物质（首战教学）"));

        Ok(E.AppraisalTutorialNote(s, t1.InstanceId));
        var a = E.FindItem(s, t1.InstanceId)!;
        Assert.That(a.Appraisal!.Result, Is.EqualTo(AppraisalRecord.FixedTutorialAttack));
        Assert.That(a.Appraisal.InstanceId, Is.EqualTo(a.InstanceId), "记录跟本实例");
        Assert.That(E.InstanceName(a), Is.EqualTo("活性物质（白色·已鉴定·首战教学）"));
        Assert.That(E.SubstanceLabel(a), Is.EqualTo("活性物质（白色·已鉴定·首战教学）"));
        Assert.That(E.CardSideOf(a), Is.EqualTo(GameEngine.TutorialIdentifiedCardSuffix));
        Assert.That(E.IsIdentifiedWhite(a), Is.False, "不混入普通已鉴定组");
        Assert.That(s.Log.Last().Text, Does.Contain("已记为“活性物质（白色·已鉴定·首战教学）”"), "新日志用新名");
        Assert.That(s.GameMinute, Is.EqualTo(minute), "0 分钟");
        Assert.That(Count(s, "fiber"), Is.EqualTo(1), "0 纤维");
        Assert.That(s.Items.Where(i => i.ItemId == Sub).Sum(i => i.Count), Is.EqualTo(total), "不耗样本");
        // 同地另一份未读教学样本、普通样本不变
        Assert.That(E.FindItem(s, t2.InstanceId)!.Appraisal, Is.Null);
        Assert.That(E.InstanceName(E.FindItem(s, t2.InstanceId)!), Is.EqualTo("未鉴定的活性物质（首战教学）"));
        Assert.That(E.FindItem(s, o.InstanceId)!.Appraisal, Is.Null);
        Assert.That(E.InstanceName(E.FindItem(s, o.InstanceId)!), Is.EqualTo(Cfg.Items.Single(i => i.Id == Sub).Name));
        // 地面分卡：已鉴定教学、未鉴定教学、普通各一张
        var cards = E.OrderedCards(s, g).Where(k => k.Split('|')[1] == Sub).ToList();
        Assert.That(cards, Is.EquivalentTo(new[] { $"{g}|{Sub}", $"{g}|{Sub}|{GameEngine.TutorialCardSuffix}", $"{g}|{Sub}|{GameEngine.TutorialIdentifiedCardSuffix}" }));
        Assert.That(E.CardHead(s, DropSource.Parse($"card:{g}|{Sub}|{GameEngine.TutorialIdentifiedCardSuffix}")!)!.InstanceId, Is.EqualTo(a.InstanceId));
        // 鉴定面板同名、状态写已鉴定
        var sv = E.AppraisalPreview(s).Samples.Single(x => x.InstanceId == a.InstanceId);
        Assert.That(sv.Name, Is.EqualTo("活性物质（白色·已鉴定·首战教学）"));
        Assert.That(sv.AppraisalText, Does.StartWith("已鉴定"));
        // 拾取到随身仍是已鉴定教学卡
        Ok(E.DoInstant(s, InstantRequest.Pickup(a.InstanceId, 1)));
        var carried = E.OrderedCards(s, ItemInstance.Carried).Where(k => k.Split('|')[1] == Sub).ToList();
        Assert.That(carried, Is.EqualTo(new[] { $"{ItemInstance.Carried}|{Sub}|{GameEngine.TutorialIdentifiedCardSuffix}" }));

        // 同堆两份：只标被选的 1 份（拆出新实例写记录），其余仍未鉴定
        var s2 = New();
        s2.Items.RemoveAll(i => i.ItemId == Sub);
        var stack = Sample(s2, ItemInstance.Carried, "tutorial-reward-b1", true, 2);
        Assert.That(stack.Count, Is.EqualTo(2));
        Ok(E.AppraisalTutorialNote(s2, stack.InstanceId));
        var subs = s2.Items.Where(i => i.ItemId == Sub).ToList();
        Assert.That(subs.Sum(i => i.Count), Is.EqualTo(2), "不复制、不消耗");
        Assert.That(subs.Single(i => i.Appraisal != null).Count, Is.EqualTo(1), "只标 1 份");
        Assert.That(subs.Single(i => i.Appraisal == null).Count, Is.EqualTo(1), "其余份仍未鉴定");
        Assert.That(subs.All(i => i.Source!.TutorialReward && i.ItemId == Sub), Is.True, "来源、itemId 不变");
    }

    /// <summary>
    /// APP-Q209-02：已鉴定教学仍禁交易、禁付费详细鉴定，融合效果（固定绿色词条）与来源不变；旧档同实例已有 fixed_tutorial_attack 记录读档即无声识别为已鉴定
    /// （不补奖励、不重播、不重写记录时刻）；重复阅读/读档不复制样本或记录；没有本实例记录不靠全局说明已看或其他样本记录推定。
    /// </summary>
    [Test]
    public void APP_Q209_02_StillTutorial_OldSaveSilent_NoInference()
    {
        var s = NewStory();
        E.DebugMapTeleport(s, "h05_appraisal");
        ReadDialogues(s);
        s.Items.RemoveAll(i => i.ItemId == Sub);
        var t = Sample(s, ItemInstance.Carried, "tutorial-reward-b1", true);
        var other = Sample(s, ItemInstance.Carried, "dev-tutorial-y", true);
        Ok(E.AppraisalTutorialNote(s, t.InstanceId));
        var a = E.FindItem(s, t.InstanceId)!;
        var at = a.Appraisal!.Minute;
        var sv = E.AppraisalPreview(s).Samples.Single(x => x.InstanceId == a.InstanceId);
        Assert.That(sv.TradeBlocked, Is.EqualTo("教学样本不可交易"));
        Assert.That(sv.AppraiseBlocked, Does.Contain("不接受付费详细鉴定"));
        Assert.That(E.StartAppraise(s, a.InstanceId).Ok, Is.False, "服务端重验");
        Assert.That(E.StartAppraisalTrade(s, a.InstanceId, "white_for_cloth").Ok, Is.False);
        Assert.That(a.Source!.TutorialReward, Is.True, "来源不变");
        Assert.That(E.FusionOutcomeText(s, a), Does.Contain(Cfg.ActiveSubstance.TutorialEffect.Label), "融合效果不变");
        // 重复阅读：不复制样本或记录
        var logs = s.Log.Count;
        var n = s.Items.Where(i => i.ItemId == Sub).Sum(i => i.Count);
        Ok(E.AppraisalTutorialNote(s, a.InstanceId));
        Assert.That(s.Items.Where(i => i.ItemId == Sub).Sum(i => i.Count), Is.EqualTo(n));
        Assert.That(E.FindItem(s, a.InstanceId)!.Appraisal!.Minute, Is.EqualTo(at), "不重写记录");
        Assert.That(s.Log.Last().Text, Does.Contain("回看不扣任何东西"));
        // 无记录不推定：另一份没看过的仍未鉴定（全局已看过说明也不推定）
        Assert.That(E.InstanceName(E.FindItem(s, other.InstanceId)!), Is.EqualTo("未鉴定的活性物质（首战教学）"));

        // 旧档：存档里该实例已有 fixed_tutorial_attack 记录 → 读档即已鉴定（无声：不补奖励、不加日志、不改记录时刻）
        var json = Survival.Core.Save.SaveSerializer.Serialize(s);
        var loaded = Survival.Core.Save.SaveSerializer.Deserialize(json, Cfg);
        var la = E.FindItem(loaded, a.InstanceId)!;
        Assert.That(E.InstanceName(la), Is.EqualTo("活性物质（白色·已鉴定·首战教学）"));
        Assert.That(E.CardSideOf(la), Is.EqualTo(GameEngine.TutorialIdentifiedCardSuffix));
        Assert.That(la.Appraisal!.Minute, Is.EqualTo(at));
        Assert.That(loaded.Traits.Count, Is.EqualTo(s.Traits.Count), "不补奖励");
        Assert.That(loaded.Items.Where(i => i.ItemId == Sub).Sum(i => i.Count), Is.EqualTo(n), "读档不复制");
        Assert.That(E.InstanceName(E.FindItem(loaded, other.InstanceId)!), Is.EqualTo("未鉴定的活性物质（首战教学）"), "无记录不推定");

        // 已鉴定后原入口直接融合仍得唯一绿色词条
        Start(s, ActionRequest.Fuse(a.InstanceId));
        Run(s);
        ReadDialogues(s);
        Assert.That(s.Traits.Single().Label, Is.EqualTo(Cfg.ActiveSubstance.TutorialEffect.Label));
    }
}
