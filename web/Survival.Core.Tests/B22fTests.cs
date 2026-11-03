using System.Reflection;
using Newtonsoft.Json.Linq;
using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 批次 B22f：策划答复 Q179—Q207 里的小改与核对（策划提交 280f8be）。规则出处：各策划案「Q179—Q207 本节相关裁定」、
/// assets/20261103/party-combat-expedition-adjudications.json（PCE：shocks、checkpoint、xp、acceptance）、survival-adjudications.json、
/// 更新说明 2026-11-03「Q179-Q207」。Q180/Q181/Q183（招募身份、安全屋候补）、Q192（人口分层）、Q195（保护落空）、Q196（野外撤退来处）属 B22g/B22h，不在这里。
/// </summary>
public class B22fTests : BattleTestBase
{
    private void Ok(ActionResult r) => Assert.That(r.Ok, Is.True, r.Reason);

    private static JObject Design(string name) => JObject.Parse(File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, name)));

    private GameState At(string node, int capacity, uint seed = 3001)
    {
        var s = New(seed);
        UnlockCapacity(s, capacity);
        s.GameMinute = At(2, 9, 0);
        s.SpawnNextCheck = int.MaxValue;
        s.PatrolNextCheck = int.MaxValue;
        E.DebugMapTeleport(s, node);
        return s;
    }

    /// <summary>把本场敌人逐个判死并结算胜利。</summary>
    private void WinBattle(GameState s)
    {
        var kill = typeof(GameEngine).GetMethod("KillEnemy", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var check = typeof(GameEngine).GetMethod("CheckVictory", BindingFlags.NonPublic | BindingFlags.Instance)!;
        foreach (var e in s.Battle!.Enemies.Where(e => e.Alive).ToList()) kill.Invoke(E, new object[] { s, e });
        check.Invoke(E, new object[] { s });
        Assert.That(s.Battle, Is.Null);
    }

    // ───────────── Q193 / Q187 m02 检查哨拦路战未胜禁探索、清理翻找、采集 ─────────────

    /// <summary>
    /// PLAN-Q193-01 / PLAN-Q193-02 / PLAN-Q187-01（m02 部分）：首次到达出现 1 只迟缓感染者固定战；未胜时探索、清理翻找、资源采集都不能开始，
    /// 原因写“先战胜检查哨拦路敌人”；可以沿已开边离开；撤退、重访、探索度 100 都不清除、不刷 HP；最终胜利才开营门并放开三项。
    /// </summary>
    [Test]
    public void PLAN_Q193_01_M02_BlocksExploreClearGather_UntilWin()
    {
        var s = At("m02_checkpoint", 1);
        Assert.That(E.FixedEnemiesAt(s, "m02_checkpoint").Select(w => w.EnemyId), Is.EqualTo(new[] { "z_slow" }));
        var z = Cfg.Enemies.Single(e => e.Id == "z_slow");
        Assert.That((z.Hp, z.Armor, z.Speed), Is.EqualTo((4.2, 0.0, 2.0)), "强度沿原 4.2HP/零甲/速度2");
        Assert.That(s.Battle, Is.Null, "到达不自动开战");
        s.Resources["m02_checkpoint_duty"].Discovered = true;
        foreach (var r in new[] { ActionRequest.Explore(), ActionRequest.Clear(), ActionRequest.Gather("m02_checkpoint_duty") })
            Assert.That(E.CanStart(s, r), Does.StartWith(GameEngine.CheckpointBlockText), r.Type.ToString());
        var back = E.EdgeBetween("m02_checkpoint", "m01_road")!;
        s.UnlockedEdges.Add(back.Id);
        s.Locations["m01_road"].Discovered = true;
        Assert.That(E.CanStart(s, ActionRequest.Travel("m01_road")), Is.Null, "可以沿已开边离开");

        // 交手后撤退：HP 保留、仍禁
        Ok(E.EngageFixed(s));
        s.Battle!.Enemies[0].Hp = 2.0;
        PlayerFirst(s);
        E.BattleStep(s);
        Force(s, Below(0.0001));
        Ok(Cmd(s, "retreat"));
        Assert.That(s.Battle, Is.Null);
        Assert.That(E.CanStart(s, ActionRequest.Explore()), Does.StartWith(GameEngine.CheckpointBlockText), "撤退不清除");
        // 重访：同一只、HP 不刷
        E.DebugMapTeleport(s, "m01_road");
        E.DebugMapTeleport(s, "m02_checkpoint");
        var left = E.FixedEnemiesAt(s, "m02_checkpoint");
        Assert.That(left, Has.Count.EqualTo(1), "不补第二只");
        Assert.That(left[0].Hp, Is.EqualTo(2.0).Within(1e-9), "重访不刷 HP");
        Assert.That(E.CanStart(s, ActionRequest.Clear()), Does.StartWith(GameEngine.CheckpointBlockText), "重访不清除");
        // 探索度 100 也不清除
        E.DebugSetProgress(s, "m02_checkpoint", 100);
        Assert.That(E.CanStart(s, ActionRequest.Clear()), Does.StartWith(GameEngine.CheckpointBlockText), "探索度 100 不清除");
        Assert.That(s.Progress.Flags, Does.Not.Contain("checkpoint_obstruction_cleared"));

        // 最终胜利（主角存活）：写清障事实、营门开、三项放开
        Ok(E.EngageFixed(s));
        WinBattle(s);
        Assert.That(s.Progress.Flags, Does.Contain("checkpoint_obstruction_cleared"));
        Assert.That(s.UnlockedEdges, Does.Contain("m02_checkpoint__h01_gate"));
        Assert.That(E.FixedBlockHere(s), Is.Null);
        Assert.That(E.CanStart(s, ActionRequest.Gather("m02_checkpoint_duty")) ?? "", Does.Not.StartWith(GameEngine.CheckpointBlockText));
        Assert.That(E.CanStart(s, ActionRequest.Clear()) ?? "", Does.Not.StartWith(GameEngine.CheckpointBlockText));
    }

    /// <summary>PLAN-Q193-02：旧档已有可信清障事实（checkpoint_obstruction_cleared）不反锁、不生成拦路战；已探索进度不回收。</summary>
    [Test]
    public void PLAN_Q193_02_OldClearedFact_NoRelock()
    {
        var s = New(3001);
        UnlockCapacity(s, 1);
        s.Progress.Flags.Add("checkpoint_obstruction_cleared");
        s.Locations["m02_checkpoint"].Progress = 50;
        E.DebugMapTeleport(s, "m02_checkpoint");
        Assert.That(E.FixedEnemiesAt(s, "m02_checkpoint"), Is.Empty, "不生成新拦路战");
        Assert.That(E.FixedBlockHere(s), Is.Null);
        Assert.That(E.CanStart(s, ActionRequest.Explore()), Is.Null);
        Assert.That(s.Locations["m02_checkpoint"].Progress, Is.EqualTo(50), "已探索不回收");
    }

    /// <summary>PCE checkpoint.oldSaves：新规则只限制未开始的探索/清理/采集；已在办的探索沿旧开工快照完成（各段推进不重验拦路战）。</summary>
    [Test]
    public void PLAN_Q193_02_InProgressExplore_FinishesBySnapshot()
    {
        var s = At("m02_checkpoint", 1);
        var st = E.FixedStateAt(s, "m02_checkpoint")!;
        // 模拟旧档：开工时还没有这条限制（这里临时记为已胜来开工），开工后拦路战照旧在
        st.Won = true;
        Start(s, ActionRequest.Explore());
        st.Won = false;
        Assert.That(E.FixedBlockHere(s), Is.Not.Null);
        Run(s);
        Assert.That(s.Locations["m02_checkpoint"].Progress, Is.GreaterThan(0), "已开工的探索照常完成");
        Assert.That(E.CanStart(s, ActionRequest.Explore()), Does.StartWith(GameEngine.CheckpointBlockText), "完成后新开工仍禁");
    }

    /// <summary>
    /// 开发假设 B22f-2（待确认 Q208）：序章 route_onward 到检查哨、下一段未知且拦路战未胜时，下一步写“先战胜检查哨拦路敌人。”，不点亮探索入口；
    /// 胜利后回到 Q139 原句（探索句）。
    /// </summary>
    [Test]
    public void B22f_2_PrologueStep_AtM02_FightFirst()
    {
        var s = NewStory();
        ReadDialogues(s);
        E.DebugPrologue(s, "won");
        ReadDialogues(s);
        s.Progress.Flags.Add(GameEngine.FactCampRouteKnown);
        s.Progress.Flags.Add(GameEngine.FactVisitorMet);
        E.DebugMapTeleport(s, "m01_road");
        ReadDialogues(s);
        E.PrologueBoundary(s);
        ReadDialogues(s);
        Assert.That(s.Prologue.ObjectiveId, Is.EqualTo("route_onward"));
        E.DebugMapTeleport(s, "m02_checkpoint");
        ReadDialogues(s);
        E.PrologueBoundary(s);
        Assert.That(E.NextTravelSegment(s)!.Kind, Is.EqualTo(TravelSegmentKind.UnknownFixedBlocked));
        Assert.That(E.CurrentObjective(s)!.Step, Is.EqualTo("先战胜检查哨拦路敌人。"));
        Assert.That(E.ObjectiveFocus(s), Is.Null, "不点亮探索入口");
        E.DebugWinFixedHere(s);
        Assert.That(E.CurrentObjective(s)!.Step, Is.EqualTo("沿已知路线前往营门。"), "胜利后回到 Q139 原口径（清障揭出营门路线，已知可走）");
        Assert.That(E.ObjectiveFocus(s), Is.EqualTo("loc:h01_gate"));
    }

    // ───────────── Q207 净化也计烹饪每日 30（净化子限 4） ─────────────

    private static string Key(GameState s, string kind) => $"{Clock.Day(s.GameMinute)}|{kind}|cooking";

    private static int Get(GameState s, string kind) => s.Player.DailyCounts.TryGetValue(Key(s, kind), out var n) ? n : 0;

    /// <summary>营地、学会全部知识；清掉水与净水片后放 4 份脏水与 n 片净水片。</summary>
    private GameState WaterCamp(int tablets = 1)
    {
        var s = New();
        E.DebugLearnAllKnowledge(s);
        s.Items.RemoveAll(i => i.ItemId is "dirty_water" or "clean_water" or "purification_tablet");
        E.CreateItems(s, "dirty_water", 4, s.CurrentLocation);
        E.CreateItems(s, "purification_tablet", tablets, s.CurrentLocation);
        return s;
    }

    [Test, Category("DesignSync")]
    public void PLAN_Q207_Contract_TotalAndSubcap()
    {
        var x = Design("design.pce.json")["xp"]!;
        Assert.That(((int)x["totalDailyCookingCap"]!, (int)x["purificationDailySubcap"]!),
            Is.EqualTo((Cfg.Progression.Cooking.FoodDailyCap, Cfg.Progression.Cooking.WaterDailyCap)));
        Assert.That((Cfg.Progression.Cooking.FoodDailyCap, Cfg.Progression.Cooking.WaterDailyCap), Is.EqualTo((30, 4)));
    }

    /// <summary>PLAN-Q207-02 例一：当天食品已得 28，净水片净化应得 4 只给 2（总 30、净化 2）；随后净化/食品均 0。</summary>
    [Test]
    public void PLAN_Q207_02_Food28_ThenPurify_Gives2()
    {
        var s = WaterCamp(2);
        s.Player.DailyCounts[Key(s, GameEngine.DailyXp)] = 28;
        var pv = E.Preview(s, ActionRequest.Craft("ex_tablet_water"));
        Assert.That(pv.XpGains, Has.Some.Contains("烹饪经验+2").And.Some.Contains("今日只剩2"));
        var cook0 = s.Skills["cooking"].Xp;
        Start(s, ActionRequest.Craft("ex_tablet_water"));
        Run(s);
        Assert.That(s.Skills["cooking"].Xp - cook0, Is.EqualTo(2));
        Assert.That((Get(s, GameEngine.DailyXp), Get(s, GameEngine.DailyWaterXp)), Is.EqualTo((28, 2)), "总 30、净化 2");
        Assert.That(s.Log.Last(l => l.Kind == "ActionCompleted").Text, Does.Contain("烹饪经验+2（烹饪每日总上限30，只剩2）"));
        // 再一批：净化子限还剩 2，但总额已满 → 0
        E.CreateItems(s, "dirty_water", 4, s.CurrentLocation);
        Assert.That(E.Preview(s, ActionRequest.Craft("ex_tablet_water")).XpGains, Has.Some.Contains("今日烹饪经验已满30（含净化）"));
        Start(s, ActionRequest.Craft("ex_tablet_water"));
        Run(s);
        Assert.That(s.Skills["cooking"].Xp - cook0, Is.EqualTo(2));
        Assert.That(s.Log.Last(l => l.Kind == "ActionCompleted").Text, Does.Contain("今日烹饪经验已满30（含净化）"));
    }

    /// <summary>PLAN-Q207-02 例二：先净化 4，食品最多再 26（直烤肉 per 3 时 30 → 只剩 26 时第 9 份只给 2）。</summary>
    [Test]
    public void PLAN_Q207_02_Purify4_ThenFoodAtMost26()
    {
        var s = New();
        s.Player.DailyCounts[Key(s, GameEngine.DailyWaterXp)] = 4;
        s.Player.DailyCounts[Key(s, GameEngine.DailyXp)] = 24;   // 食品已 24：总 28，食品只剩 2（若不计净化会剩 6）
        var cookXp = typeof(GameEngine).GetMethod("CookFoodRoom", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var day = Clock.Day(s.GameMinute);
        Assert.That((int)cookXp.Invoke(E, new object[] { s.Player, day, 3 })!, Is.EqualTo(2), "30 − 4 − 24 = 2");
        s.Player.DailyCounts[Key(s, GameEngine.DailyXp)] = 0;
        Assert.That((int)cookXp.Invoke(E, new object[] { s.Player, day, 40 })!, Is.EqualTo(26), "先净化 4，食品最多 26");
        Assert.That(E.CookActiveXpPreview(s, 40), Does.Contain("烹饪经验+26").And.Contain("今日只剩26"));
    }

    /// <summary>
    /// PLAN-Q207-02 例三与旧档：旧档当天已得食品 30 + 净化 4 = 34 不倒扣，当天新增 0；完成日（次日）新额正常。
    /// 主动烹饪配方（远征口粮棒等 xpSkill=cooking）也走同一总额。
    /// </summary>
    [Test]
    public void PLAN_Q207_02_OldSave34_NoClawback_NextDayFresh()
    {
        var s = WaterCamp(1);
        s.Player.DailyCounts[Key(s, GameEngine.DailyXp)] = 30;
        s.Player.DailyCounts[Key(s, GameEngine.DailyWaterXp)] = 4;
        var t = SaveSerializer.Deserialize(SaveSerializer.Serialize(s), Cfg);
        var cook0 = t.Skills["cooking"].Xp;
        Assert.That((Get(t, GameEngine.DailyXp), Get(t, GameEngine.DailyWaterXp)), Is.EqualTo((30, 4)), "不倒扣、不迁移");
        Assert.That(E.CookActiveXpPreview(t, 5), Does.Contain("今日烹饪经验已满30（含净化）"));
        // 食品已 30、净化 0 的情形：净化也给 0
        t.Player.DailyCounts[Key(t, GameEngine.DailyWaterXp)] = 0;
        Assert.That(E.Preview(t, ActionRequest.Craft("ex_tablet_water")).XpGains, Has.Some.Contains("今日烹饪经验已满30（含净化）"));
        Start(t, ActionRequest.Craft("ex_tablet_water"));
        Run(t);
        Assert.That(t.Skills["cooking"].Xp, Is.EqualTo(cook0), "当天新增 0");
        // 次日 00:00 后完成：按完成日新额
        t.GameMinute = At(Clock.Day(t.GameMinute) + 1, 0, 30);
        E.CreateItems(t, "dirty_water", 4, t.CurrentLocation);
        E.CreateItems(t, "purification_tablet", 1, t.CurrentLocation);
        Start(t, ActionRequest.Craft("ex_tablet_water"));
        Run(t);
        Assert.That(t.Skills["cooking"].Xp - cook0, Is.EqualTo(4), "完成日新额正常");
    }

    /// <summary>PLAN-Q207-01：锅烧水净化也受总额 30：当天食品已 29，锅净化 1000ml 应得 2 只给 1。</summary>
    [Test]
    public void PLAN_Q207_01_PotWater_AlsoCountsTotal()
    {
        var s = New();
        s.Player.DailyCounts[Key(s, GameEngine.DailyXp)] = 29;
        var pot = First(s, "pot");
        E.DebugGive(s, "dirty_water", 2, "camp");
        Ok(E.DoInstant(s, InstantRequest.FillPot(First(s, "dirty_water", "camp").InstanceId, pot.InstanceId, 2)));
        Ok(E.DoInstant(s, InstantRequest.PutInFire(pot.InstanceId)));
        var fac = s.Locations["camp"].Facility;
        fac.FireLit = true; fac.FuelMinutes = 240;
        Assert.That(E.CookXpLine(s, pot), Does.Contain("也计烹饪每日总上限30").And.Contain("今日只剩1"));
        var cook0 = s.Skills["cooking"].Xp;
        Start(s, ActionRequest.Wait(30));
        Run(s);
        Assert.That(s.Skills["cooking"].Xp - cook0, Is.EqualTo(1), "min(2, 4−0, 30−29)=1");
        Assert.That((Get(s, GameEngine.DailyXp), Get(s, GameEngine.DailyWaterXp)), Is.EqualTo((29, 1)));
    }
}
