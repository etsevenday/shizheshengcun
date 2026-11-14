using Survival.Core.Config;
using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// B47a3 行动与撤出预估截断：R09 文首「B47a3 Q682—Q687」（验收 Q682-A01—A03、Q683-A01、Q684-A01—A02、Q685-A01、Q686-A01、Q687-A01）
/// 与「B47a Q673—Q678」中的 Q674、Q675（Q674-A01—A02、Q675-A01）；dying-willpower-q616.json v4 q682ToQ687；开发记录 B47a3。
/// </summary>
public class B47a3Tests : BattleTestBase
{
    protected override bool NoEnemyApproach => true;

    private static string Enter(string who, int pct, bool extract = false) => GameEngine.DyingRiskText(DangerKind.Enter, who, pct, extract);
    private static string Certain(string who, int pct, bool extract = false) => GameEngine.DyingRiskText(DangerKind.Certain, who, pct, extract);
    private static string Possible(string who, int pct, bool extract = false) => GameEngine.DyingRiskText(DangerKind.Possible, who, pct, extract);

    private static void Willpower(ActorState a, int level) => a.Skills[GameEngine.Willpower] = new SkillState { Level = level, Xp = 0 };

    /// <summary>荒地探索（白天开工）：主角健康 h、饱食水分 0（每段确定扣健康 2.0/h×5/60）。</summary>
    private GameState FieldStarving(double h, int minute = 480)
    {
        var s = New();
        E.DebugTeleport(s, "field");
        s.GameMinute = minute;
        SetStats(s, h, 0, 0, 90, 90, 60);
        return s;
    }

    private (GameEngine e, GameState s) With(Action<GameConfig> tweak)
    {
        var cfg = LoadConfig();
        tweak(cfg);
        var e = new GameEngine(cfg);
        var s = e.NewGame(12345);
        e.DebugLegacyStory(s);
        s.SuppressEnemyApproach = true;
        return (e, s);
    }

    private static void Six(ActorState a, double n, double w, double e, double sl, double m, double h)
    {
        a.Stats.Set(StatId.Satiety, n);
        a.Stats.Set(StatId.Hydration, w);
        a.Stats.Set(StatId.Energy, e);
        a.Stats.Set(StatId.Wakefulness, sl);
        a.Stats.Set(StatId.Morale, m);
        a.Stats.Set(StatId.Health, h);
    }

    /// <summary>d01 远征进行中、站在 04 安全点（同 B21AbortTests.At04）。</summary>
    private GameState At04(uint seed, params string[] recruits)
    {
        var s = B21EntryTests.AtBuffer(E, seed);
        UnlockCapacity(s, 4);
        foreach (var r in recruits) E.DebugRecruit(s, r);
        foreach (var to in new[] { "d01_02", "d01_04" })
        {
            Start(s, ActionRequest.Travel(to, to == "d01_02"));
            Assert.That(Run(s), Is.EqualTo(SegmentStatus.Completed));
            if (to == "d01_02") { E.DebugWinFixedHere(s); E.DebugSetProgress(s, "d01_02", 100); }
        }
        return s;
    }

    // ───────────── Q675 六句 ─────────────

    /// <summary>Q675-A01：六句正式全文（普通／撤出 × 首次归零／确定正伤／仅概率正伤），抵抗率取本人整数百分点，意志 1／10 级为 65%／83%。</summary>
    [Test]
    public void Q675_A01_SixSentences_ResistancePercent()
    {
        Assert.That(Enter("你", 65), Is.EqualTo("按当前状态，你预计在本段进入濒死。此后仍处于濒死状态时，每次再受到正伤害都将进行致死判定；你当前致死抵抗为65%。"));
        Assert.That(Certain("林昭", 83), Is.EqualTo("林昭已处于濒死状态，本段将受到正伤害；濒死期间每次受到正伤害都将进行致死判定，林昭当前致死抵抗为83%。"));
        Assert.That(Possible("你", 65), Is.EqualTo("你已处于濒死状态。若本段在濒死状态下受到正伤害，每次都将进行致死判定；你当前致死抵抗为65%。"));
        Assert.That(Enter("你", 65, true), Is.EqualTo("撤出途中，按当前状态，你预计在本段进入濒死。此后仍处于濒死状态时，每次再受到正伤害都将进行致死判定；你当前致死抵抗为65%。"));
        Assert.That(Certain("你", 65, true), Is.EqualTo("撤出途中，你已处于濒死状态，本段将受到正伤害；濒死期间每次受到正伤害都将进行致死判定，你当前致死抵抗为65%。"));
        Assert.That(Possible("你", 65, true), Is.EqualTo("撤出途中，你已处于濒死状态。若本段在濒死状态下受到正伤害，每次都将进行致死判定；你当前致死抵抗为65%。"));

        var s = FieldStarving(0);
        s.Player.DyingNotified = true;
        E.DebugSetSkillLevel(s, GameEngine.Willpower, 10);
        var p = E.Preview(s, ActionRequest.Explore());
        Assert.That(p.BodyRisks, Is.EqualTo(new[] { Certain("你", 83) }), "意志 10 级取本人 83%");
        Assert.That(p.ResistanceNote, Is.EqualTo("致死抵抗是濒死后每次再受正伤害时的抵抗机会，不是整段行动的存活率。"));
        Assert.That(p.Warnings, Has.None.Contains("可能死亡"), "仅濒死／归零不再出旧直接死亡暗示");
    }

    // ───────────── Q682／Q674 身体截止与完成计划 ─────────────

    /// <summary>
    /// Q682-A01／Q674-A01：探索在第 2 段首次进入濒死——身体在该段截止（从现在起 10 分钟、健康 0 可确定），后续仍按入夜 ×1.5 求完整计划；
    /// 总耗时与健康者相同，不被危险段时间替换；计划终身体值不回填；真实状态与随机不变。
    /// </summary>
    [Test]
    public void Q682_A01_BodyStopsAtFirstDanger_PlanKeepsNightFactor()
    {
        var dusk = 1080 - 10;
        var s = FieldStarving(0.3, dusk);
        var before = SaveSerializer.Serialize(s);
        var rng = s.Rng.State;
        var p = E.Preview(s, ActionRequest.Explore());
        Assert.That(p.Ok, Is.True, p.Reason);
        Assert.That(SaveSerializer.Serialize(s), Is.EqualTo(before), "预估只读");
        Assert.That(s.Rng.State, Is.EqualTo(rng));

        var healthy = FieldStarving(100, dusk);
        var full = E.Preview(healthy, ActionRequest.Explore());
        Assert.That(full.BodyCut, Is.False, "无危险不强加截断");
        Assert.That(full.EstimatedMinutes, Is.GreaterThan(20));

        Assert.That(p.BodyCutoffMinutes, Is.EqualTo(10), "0.3 每段扣 1/6：第 2 段归零");
        Assert.That(p.EstimatedMinutes, Is.EqualTo(full.EstimatedMinutes), "完整计划不被危险段截短");
        Assert.That(p.PlanWarnings, Has.Some.Contains("工作倍率变为×1.5"), "截止后入夜仍进入完成计划");
        Assert.That(p.PlanFailed, Is.False);
        Assert.That(p.StatsAfter.Health, Is.EqualTo(0));
        Assert.That(p.HealthUnknown, Is.Empty, "首次归零本身可确定为 0");
        Assert.That(p.StatsAfter.Energy, Is.EqualTo(s.Stats.Energy + E.Config.HourlyRates["explore"].Energy * 10 / 60.0).Within(1e-9), "身体值取截止时刻，不是计划终值");
        Assert.That(p.BodyRisks, Is.EqualTo(new[] { Enter("你", 65) }));
        Assert.That(p.Warnings[p.PlanWarnings.Count], Is.EqualTo("身体预估截至从现在起10分钟；此后身体变化未预估。"));
        Assert.That(E.Preview(s, ActionRequest.Explore()).Warnings, Is.EqualTo(p.Warnings), "反复查看同事实输出一致");
    }

    /// <summary>Q683-A01：已濒死、本段确定再受正伤——健康后值取决于抵抗结果写“未预估”，其余属性取截止段末；风险用确定正伤句。</summary>
    [Test]
    public void Q683_A01_CertainHitHealthUnknown_OtherStatsAtCutoff()
    {
        var s = FieldStarving(0);
        s.Player.DyingNotified = true;
        var p = E.Preview(s, ActionRequest.Explore());
        Assert.That(p.BodyCutoffMinutes, Is.EqualTo(5));
        Assert.That(p.HealthUnknown, Is.EquivalentTo(new[] { GameState.PlayerId }));
        Assert.That(p.StatsAfter.Energy, Is.EqualTo(s.Stats.Energy + E.Config.HourlyRates["explore"].Energy * 5 / 60.0).Within(1e-9));
        Assert.That(p.BodyRisks, Is.EqualTo(new[] { Certain("你", 65) }));
        Assert.That(p.EstimatedMinutes, Is.GreaterThan(5), "完成计划按假设存活求全程");
        Assert.That(p.Notes, Has.Some.Contains("探索度"), "完成后探索度等计划明细照常");
    }

    /// <summary>Q674-A02：已濒死但全程没有正伤风险，不凭空判致死、不截断；濒死自然回血仍为 0。</summary>
    [Test]
    public void Q674_A02_DyingWithoutHits_NoCut()
    {
        var s = New();
        SetStats(s, 0, 80, 80, 90, 90, 60);
        s.Player.DyingNotified = true;
        var p = E.Preview(s, ActionRequest.Rest(30));
        Assert.That((p.BodyCut, p.BodyRisks.Count, p.ResistanceNote), Is.EqualTo((false, 0, (string?)null)));
        Assert.That(p.StatsAfter.Health, Is.EqualTo(0));
    }

    /// <summary>Q682-A02：新开工可信动态预计恰为 5 分钟按原规则锁定（已濒死且本段确定正伤也照计划存活求得 5）；长行动身体早截断不误锁。</summary>
    [Test]
    public void Q682_A02_ExitLockFromSamePlan()
    {
        var (e, s) = With(c => c.Recipes.Single(r => r.Id == "bandage").BaseMinutes = 5);
        e.DebugGive(s, "cloth", 1);
        e.DebugGive(s, "herb", 1);
        e.DebugSetStats(s, 0, 0, 0, 90, 90, 60);
        s.Player.DyingNotified = true;
        var pv = e.Preview(s, ActionRequest.Craft("bandage"));
        Assert.That((pv.EstimatedMinutes, pv.NoExit, pv.BodyCutoffMinutes), Is.EqualTo((5, true, (int?)5)));
        Assert.That(e.Start(s, ActionRequest.Craft("bandage")).Ok, Is.True);
        Assert.That(s.Main!.NoExit, Is.True, "真实开工与预估同一认定");

        var t = FieldStarving(0.3);
        Start(t, ActionRequest.Explore());
        Assert.That(t.Main!.NoExit, Is.False, "预计长于 5 分钟：身体 10 分钟就截止也不锁");
    }

    /// <summary>Q682：求不出可信完成（原料在途中腐败中止）——计划失败写原因，不列中途设施快照，开工估时同源不猜完成时刻。</summary>
    [Test]
    public void Q682_PlanFailed_WhenActionAbortsMidway()
    {
        var s = New();
        E.DebugGive(s, "cloth", 1);
        E.DebugGive(s, "herb", 1);
        foreach (var it in s.Items.Where(i => i.ItemId == "herb")) it.ExpiresAt = s.GameMinute + 5;
        var p = E.Preview(s, ActionRequest.Craft("bandage"));
        Assert.That(p.Ok, Is.True, p.Reason);
        Assert.That(p.PlanFailed, Is.True);
        Assert.That(p.PlanFailReason, Does.Contain("原料已腐败"));
        Assert.That(p.Warnings, Has.Some.StartsWith("按当前条件无法预估完成。"));
        Assert.That(p.FacilityChanges, Is.Empty);
    }

    // ───────────── Q686 同行者 ─────────────

    /// <summary>
    /// Q686-A01：同伴先在第 1 段进入濒死而主角尚可——共同身体区在同伴的危险段截止，同伴风险用本人姓名／抗性；
    /// 主角第 3 段才归零，单列“各人风险”并附说明；主角身体值取 5 分钟截止处，不取主角自己更晚的值。
    /// </summary>
    [Test]
    public void Q686_A01_CompanionFirstDanger_CommonCutoff_LaterRiskSeparate()
    {
        var s = FieldStarving(0.45);
        E.DebugQuickParty(s, 2);
        var mate = s.Actors[s.Party.Single(id => id != GameState.PlayerId)];
        Six(mate, 0, 0, 90, 90, 60, 0.1);
        Willpower(mate, 10);
        var name = E.ActorDisplayName(s, mate.Id);
        var p = E.Preview(s, ActionRequest.Explore());
        Assert.That(p.BodyCutoffMinutes, Is.EqualTo(5), "取同行者中最早的危险段");
        Assert.That(p.BodyRisks, Is.EqualTo(new[] { Enter(name, 83) }));
        Assert.That(p.LaterRisks, Is.EqualTo(new[] { Enter("你", 65) }));
        Assert.That(p.StatsAfter.Health, Is.EqualTo(0.45 - 2.0 * 5 / 60).Within(1e-9), "主角身体只到共同截止");
        var flat = p.Warnings;
        var note = flat.IndexOf("以下为各人截至计划结束的首个风险检查，不表示已预估危险处以后的身体数值。");
        Assert.That(note, Is.GreaterThan(flat.IndexOf(Enter(name, 83))));
        Assert.That(flat[note + 1], Is.EqualTo(Enter("你", 65)));
        Assert.That(flat.Count(x => x == GameEngine.ResistanceMeaning), Is.EqualTo(1), "抵抗含义只附一次");
    }

    /// <summary>Q686-A01：候补（不在同行）不纳入；只读不改同伴。</summary>
    [Test]
    public void Q686_A01_ReserveNotIncluded()
    {
        var s = FieldStarving(100);
        s.Player.Stats.Set(StatId.Satiety, 80);
        s.Player.Stats.Set(StatId.Hydration, 80);
        E.DebugQuickParty(s, 2);
        var mate = s.Party.Single(id => id != GameState.PlayerId);
        Six(s.Actors[mate], 0, 0, 90, 90, 60, 0.1);
        s.Party.Remove(mate);   // 留在名册、不在同行（候补）
        Assert.That(E.RosterOf(s, mate)!.Present, Is.True);
        var p = E.Preview(s, ActionRequest.Explore());
        Assert.That((p.BodyCut, p.BodyRisks.Count, p.LaterRisks.Count), Is.EqualTo((false, 0, 0)));
    }

    // ───────────── Q687 概率伤 ─────────────

    /// <summary>Q687-A01：尚未濒死、仅搜集划伤可能归零——不截断、不抽签，保留原概率行并附限定；已濒死后可能正伤仍截断（第三句）。</summary>
    [Test]
    public void Q687_A01_ProbabilityOnly_NoCut_DyingPossibleStillCuts()
    {
        var s = New();
        E.DebugTeleport(s, "woods");
        E.DebugSetProgress(s, "woods", 25);
        SetStats(s, 0.5, 80, 80, 18, 90, 60);
        var p = E.Preview(s, ActionRequest.Gather("wood"));
        Assert.That(p.HazardProbability, Is.GreaterThan(0), "原概率风险行保留");
        Assert.That(p.HazardHealthLoss, Is.GreaterThanOrEqualTo(0.5));
        Assert.That(p.BodyCut, Is.False, "仅概率伤可能归零不截断");
        Assert.That(p.ProbabilityNote, Is.EqualTo("身体变化按未触发本次概率受伤估算；概率受伤风险另列，不代表保证安全。"));
        Assert.That(p.StatsAfter.Health, Is.EqualTo(0.5).Within(1e-9), "按未触发估算确定变化");

        var d = New();
        E.DebugTeleport(d, "woods");
        E.DebugSetProgress(d, "woods", 25);
        SetStats(d, 0, 80, 80, 18, 90, 60);
        d.Player.DyingNotified = true;
        var q = E.Preview(d, ActionRequest.Gather("wood"));
        Assert.That(q.BodyCutoffMinutes, Is.EqualTo(q.EstimatedMinutes), "完成段的可能正伤是危险段");
        Assert.That(q.BodyRisks, Is.EqualTo(new[] { Possible("你", 65) }));
        Assert.That(q.HealthUnknown, Does.Contain(GameState.PlayerId));
        Assert.That(q.ProbabilityNote, Is.Null);
    }

    /// <summary>Q687：确定损伤会归零仍截断（概率伤之外的确定变化）。</summary>
    [Test]
    public void Q687_CertainLossStillCuts()
    {
        var s = New();
        E.DebugTeleport(s, "woods");
        E.DebugSetProgress(s, "woods", 25);
        SetStats(s, 0.1, 0, 0, 18, 90, 60);
        var p = E.Preview(s, ActionRequest.Gather("wood"));
        Assert.That(p.BodyCutoffMinutes, Is.EqualTo(5));
        Assert.That(p.BodyRisks, Is.EqualTo(new[] { Enter("你", 65) }));
    }

    // ───────────── Q684 尸变 ─────────────

    /// <summary>Q684-A01：身体已先截断，病毒按本人已知规则在计划结束前到 100——仍给本人正式尸变句；只是已感染而到不了不报。</summary>
    [Test]
    public void Q684_A01_VirusCheckedToPlanEnd_IndependentOfBodyCut()
    {
        var s = FieldStarving(0.3);
        E.DebugSetVirus(s, 50);
        s.Body.Immunity = 0;
        var (_, vr, _) = E.CurrentBodyRates(s);
        Assume.That(vr, Is.GreaterThan(0));
        s.Body.VirusLoad = 100 - vr * 25 / 60.0;   // 第 5 段前后到上限：晚于身体第 2 段截止
        var p = E.Preview(s, ActionRequest.Wait(60));
        Assert.That(p.Ok, Is.True, p.Reason);
        Assert.That(p.BodyCutoffMinutes, Is.EqualTo(10));
        Assert.That(p.VirusRisks, Is.EqualTo(new[] { "按当前已知状态推算，若本次行动继续推进，你的病毒量预计在计划结束前或结束时达到尸变上限。致死抵抗不能阻止尸变。" }));
        Assert.That(p.EstimatedMinutes, Is.EqualTo(60), "副本不尸变，计划照常到结束");
        Assert.That(E.Preview(s, ActionRequest.Wait(10)).VirusRisks, Is.Empty, "计划在到上限前结束不报");
        Assert.That(p.WouldDie, Is.False);
        Assert.That(s.Progress.Dead, Is.False);

        var low = FieldStarving(100);
        E.DebugSetVirus(low, 20);
        Assert.That(E.Preview(low, ActionRequest.Wait(30)).VirusRisks, Is.Empty, "已感染但到不了上限不报");
    }

    /// <summary>Q684-A02：查询不消耗药物、不预抽感染；阈值按内部值（99.99 显示为 100.00 仍未到上限时不因显示取整误报）。</summary>
    [Test]
    public void Q684_A02_InternalThreshold_NoSideEffects()
    {
        var s = FieldStarving(100);
        s.Player.Stats.Set(StatId.Satiety, 80);
        s.Player.Stats.Set(StatId.Hydration, 80);
        E.DebugSetVirus(s, 20);
        s.Body.Immunity = 100;   // 高免疫：病毒不升
        var (_, vr, _) = E.CurrentBodyRates(s);
        Assume.That(vr, Is.LessThanOrEqualTo(0));
        s.Body.VirusLoad = 99.99;
        var before = SaveSerializer.Serialize(s);
        var p = E.Preview(s, ActionRequest.Wait(60));
        Assert.That(p.VirusRisks, Is.Empty, "自然 V 不涨就不报“已感染”");
        Assert.That(SaveSerializer.Serialize(s), Is.EqualTo(before));
    }

    // ───────────── Q685 撤出逐人 ─────────────

    /// <summary>
    /// Q685-A01：撤出先一次扣包再逐人按撤出规则推——主角 H0.4、五项归 0，撤出途中第 2 段（从现在起 10 分钟）进入濒死，标本人截止与撤出第一句；
    /// 同伴状态好不截断、健康到撤出结束；原复合风险句拆成机制句；已在撤出中再看只推剩余路线、不重扣。
    /// </summary>
    [Test]
    public void Q685_A01_ExtractPerActorCutoff_NoDoublePenalty()
    {
        var s = At04(2133, "npc_lin_zhao");
        Six(s.Player, 12, 8, 4, 10, 9, 0.3);
        var mate = s.Actors["npc_lin_zhao"];
        Six(mate, 80, 80, 80, 80, 80, 50);
        var before = SaveSerializer.Serialize(s);
        var pv = E.AbortPreview(s);
        Assert.That(pv.Reason, Is.Null);
        Assert.That(SaveSerializer.Serialize(s), Is.EqualTo(before), "只读");
        Assert.That(pv.Risks, Does.Contain("已经归零的饱食、水分、清醒继续扣健康，出血、感染与病毒照常发展。"));
        Assert.That(pv.Risks, Has.None.Contains("可能在途中死亡"));
        var me = pv.BodyRows.Single(r => r.Id == GameState.PlayerId);
        Assert.That((me.HealthFrom, me.HealthTo, me.CutoffMinutes), Is.EqualTo((0.3, (double?)0, (int?)10)), "2.4/h：0.3→0.1→0");
        Assert.That(me.CutoffText, Is.EqualTo("你的撤出身体预估截至从现在起10分钟；此后身体变化未预估。"));
        Assert.That(me.Risk, Is.EqualTo(Enter("你", 65, true)));
        var lin = pv.BodyRows.Single(r => r.Id == mate.Id);
        Assert.That((lin.CutoffMinutes, lin.CutoffText, lin.Risk), Is.EqualTo(((int?)null, (string?)null, (string?)null)), "无截断者不贴截止句");
        Assert.That(lin.HealthTo, Is.EqualTo(E.AbortPenaltyAfter(mate.Stats).Health).Within(1e-9), "撤出途中不再另扣五项，健康无负项");
        Assert.That(pv.ResistanceNote, Is.EqualTo(GameEngine.ResistanceMeaning));
        Assert.That(pv.TotalMinutes, Is.EqualTo(30), "路线总时长不受某人身体先截止影响");

        Assert.That(E.AbortExpedition(s, "abort-1").Ok, Is.True);
        Assert.That(s.Player.Stats.Health, Is.EqualTo(0.3));
        E.Advance(s);
        var mid = E.AbortPreview(s);
        Assert.That(mid.Reason, Is.Not.Null, "撤出中不能再终止");
        var me2 = mid.BodyRows.Single(r => r.Id == GameState.PlayerId);
        Assert.That((me2.HealthFrom, me2.CutoffMinutes), Is.EqualTo((s.Player.Stats.Health, (int?)5)), "只推剩余路线，不重扣开头代价");
        Assert.That(E.CanAbortExpedition(s), Is.Not.Null, "原禁用操作不扩");
    }

    /// <summary>Q685／Q675：初始已濒死但撤出全程没有正伤风险，不无条件给致死判定句。</summary>
    [Test]
    public void Q685_DyingWithoutHits_NoRisk()
    {
        var s = At04(2134);
        Six(s.Player, 80, 80, 80, 80, 80, 0);
        s.Player.DyingNotified = true;
        var pv = E.AbortPreview(s);
        var me = pv.BodyRows.Single();
        Assert.That((me.Risk, me.CutoffMinutes, me.HealthTo), Is.EqualTo(((string?)null, (int?)null, (double?)0)));
        Assert.That(pv.ResistanceNote, Is.Null);
    }

    /// <summary>Q684-A01（撤出）：撤出途中病毒预计到上限给撤出版尸变句。</summary>
    [Test]
    public void Q684_A01_ExtractVirusSentence()
    {
        var s = At04(2135);
        Six(s.Player, 80, 80, 80, 80, 80, 50);
        E.DebugSetVirus(s, 99.8);
        s.Body.Immunity = 0;
        var pv = E.AbortPreview(s);
        Assert.That(pv.VirusRisks, Is.EqualTo(new[] { "按当前已知状态推算，若继续撤出，你的病毒量预计在计划结束前或结束时达到尸变上限。致死抵抗不能阻止尸变。" }));
    }

    // ───────────── Q682-A03 只读 ─────────────

    /// <summary>Q682-A03：反复预估、读档后预估不改真实时间、生命、病毒、随机、XP、日志或退出锁；计划假设不进存档。</summary>
    [Test]
    public void Q682_A03_ReadOnly_SaveLoadSameForecast()
    {
        var s = FieldStarving(0);
        s.Player.DyingNotified = true;
        E.DebugQuickParty(s, 2);
        E.DebugSetVirus(s, 99.5);
        var before = SaveSerializer.Serialize(s);
        var p1 = E.Preview(s, ActionRequest.Explore());
        var p2 = E.Preview(s, ActionRequest.Explore());
        Assert.That(SaveSerializer.Serialize(s), Is.EqualTo(before));
        var t = SaveSerializer.Deserialize(before, E.Config);
        var p3 = E.Preview(t, ActionRequest.Explore());
        Assert.That(p2.Warnings, Is.EqualTo(p1.Warnings));
        Assert.That(p3.Warnings, Is.EqualTo(p1.Warnings), "读档同事实同输出");
        Assert.That(p1.VirusRisks, Is.Not.Empty);
        Assert.That(before, Does.Not.Contain("SimProbe"));
    }
}
