using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 B25（R75）：COMP20 端到端新局/旧局路线（更新说明 2026-10-30「01 开发执行与跨系统兼容性自检」§3 COMP20）。
/// 新局序章→营区→三城服务→两线核心记录→队伍扩容→一次可选地牢→撤出→72h 重置/招募→安全屋恢复；两势力先后各跑一遍；
/// 失败（守卫战撤退）/死亡另验；旧存档（schema 5）迁移后重复关键链。全部只用 GameEngine 公开接口与 Comp20Driver 真实移动、探索、接战、吃喝睡；
/// 用开发入口替代的环节写进账本“【开发入口】”行。路线食水、卡重与等待逐段写进测试输出（纸面预算不代替）。
/// B30（R15，PLAN-Q232-04）：两条线都带同行者真实远征；食水只来自营区配给处真实采集与随身携带（不再有开发补给），同行者用 B28 同行食饮事务喂；
/// 固定战全部实打（不再用开发入口记第二座地牢固定战胜利）。测试输出逐线写“走通：”摘要与“开发入口（未覆盖项）：”。
/// 环境变量 COMP20_TRACE=路径前缀 时把逐步轨迹（含战斗逐轮）写到“前缀.测试名.txt”（排查用）。
/// </summary>
public class Comp20EndToEndTests : TestBase
{
    private const string Lin = "npc_lin_zhao";
    private int? _cycleAtOpen;

    private static void Ok(ActionResult r, string what) => Assert.That(r.Ok, Is.True, $"{what}：{r.Reason}");

    private static void Dump(Comp20Driver d, string title)
    {
        TestContext.WriteLine($"===== {title} =====");
        foreach (var l in d.Ledger) TestContext.WriteLine(l);
        TestContext.WriteLine($"合计：主角吃{d.FoodEaten}份 喝{d.WaterDrunk}份；给同行者吃{d.CompanionFood}份 喝{d.CompanionWater}份（{string.Join("、", d.FedBy.Select(kv => $"{kv.Key}食{kv.Value.Food}/水{kv.Value.Water}"))}；出物 主角随身{d.FedFromPlayer}次、目标本人随身{d.FedFromSelf}次）；开发补给 0；探索{d.ExploreMinutes}分钟 移动{d.TravelMinutes}分钟 等待{d.WaitMinutes}分钟 睡眠{d.SleepMinutes}分钟；"
                              + $"战斗{d.Battles}场 撤退{d.Retreats}次 绕行{d.Bypasses}次；最终 {Clock.DayTime(d.S.GameMinute)} H{d.S.Stats.Health:0.0} N{d.S.Stats.Satiety:0} W{d.S.Stats.Hydration:0}");
        foreach (var l in d.LineSummaries) TestContext.WriteLine("走通：" + l);
        foreach (var f in d.FixedResults) TestContext.WriteLine("固定战实打：" + f);
        TestContext.WriteLine(d.Shortages.Count == 0 ? "缺食缺水：无" : "缺食缺水：" + string.Join("；", d.Shortages));
        if (d.Shortcuts.Count > 0) TestContext.WriteLine("开发入口（未覆盖项）：" + string.Join("；", d.Shortcuts));
    }

    private static void RunLogged(Comp20Driver d, string title, Action body)
    {
        var ok = false;
        try { body(); ok = true; }
        finally
        {
            Dump(d, title);
            if (Environment.GetEnvironmentVariable("COMP20_TRACE") is string tf) File.WriteAllLines(tf + "." + TestContext.CurrentContext.Test.Name + ".txt", d.Trace);
            if (!ok || d.S.Progress.Dead)
                foreach (var l in d.Trace.TakeLast(40)) TestContext.WriteLine("  " + l);
        }
    }

    // ───────────── 路线片段 ─────────────

    /// <summary>序章：脚本玩家（SurvivalBot）活到第5日 08:30（首战、饮水、烹饪照常），回营地与程望交谈→村庄→远行确认→出区→检查哨拦路战→营门登记→营区广场。</summary>
    private void Prologue(Comp20Driver d, bool legacy)
    {
        var s = d.S;
        d.Leg("序章：第1—5日求生（SurvivalBot）", () => new SurvivalBot(E, s, SurvivalBot.Route.StreamAndRoots).Play(At(5, 8, 30)));
        Assert.That(s.Progress.Dead, Is.False, "序章脚本活到第5日");
        if (!legacy) Assert.That(s.Story.TutorialWon, Is.True, "新局首战胜利");
        d.Leg("回营地", () => d.Go("camp"));
        E.PrologueBoundary(s);
        d.ReadDialogues();
        Assert.That(s.Progress.Flags, Does.Contain(GameEngine.FactVisitorAvailable), "D5 08:00 后首战已胜（或旧档等效）来客可用");
        Ok(E.TalkNpc(s, "npc_cheng_wang"), "与程望交谈");
        d.ReadDialogues();
        d.Leg("营地→村庄", () => d.Go("village"));
        Ok(E.ConfirmJourney(s), "PRO17 远行确认");
        d.ReadDialogues();
        d.Leg("村庄→营门（含 m01 探索、检查哨拦路战）", () => d.Go("h01_gate"));
        Assert.That(s.Progress.Flags, Does.Contain("checkpoint_obstruction_cleared"));
        Ok(E.StartCityService(s, "hub_registration_existing"), "营门登记");
        d.ReadDialogues();
        d.Leg("营门→营区广场（登记生效）", () => d.Go("h02_square"));
        Assert.That(s.Progress.Flags, Does.Contain("hub_registered"));
        Assert.That(E.BattleCapacity(s), Is.EqualTo(2), "登记 C1→C2");
        Assert.That(GameEngine.QuestDone(s, "FA01"), Is.True);
    }

    /// <summary>营区服务：林昭同行邀约入名册、收容所首次开放并登记一名候选、医务室观察休息 30 分钟；主角换一套开发装备（开发入口）。</summary>
    private void HubServices(Comp20Driver d)
    {
        var s = d.S;
        Ok(E.TalkNpc(s, Lin), "林昭交谈");
        Ok(E.AnswerRecruit(s, "RN01", E.CurrentDialogueLine(s)!.LineId, true), "RN01 登记同行");
        d.ReadDialogues();
        Assert.That(E.RosterOf(s, Lin), Is.Not.Null);
        d.Leg("广场→收容所", () => d.Go("h06_shelter"));
        Assert.That(E.RecruitOpened(s), Is.True, "登记后首次实到收容所开放候选");
        Assert.That(s.Recruit.Candidates, Has.Count.EqualTo(3));
        _cycleAtOpen = E.RecruitCycleAt(s, s.GameMinute);
        var c = s.Recruit.Candidates[0];
        d.Leg("收容所登记一名候选（10分钟）", () =>
        {
            Ok(E.StartRecruit(s, c.CandidateId, RecruitPlacement.Square, "comp20-recruit-1"), "收容所登记");
            d.Drive();
            d.ReadDialogues();
        });
        Assert.That(s.Roster.Any(r => r.CandidateId == c.CandidateId), Is.True);
        d.Note($"名册：{string.Join("、", s.Roster.Select(r => E.ActorDisplayName(s, r.ActorId)))}（候补，留营区公共居所）");
        d.Leg("收容所→医务室，观察休息30分钟", () =>
        {
            d.Go("h04_infirmary");
            Ok(E.StartCityService(s, "clinic_observation_rest"), "医务室观察休息");
            d.Drive();
            d.ReadDialogues();
        });
        E.DebugGiveEquipmentSet(s, "blue");
        var worn = d.EquipFromGround(GameState.PlayerId);
        d.Shortcut($"主角穿上开发入口给的蓝色装备一套（{worn}件：砍刀、木盾、皮甲等），代替远征前的制作与采购");
    }

    /// <summary>
    /// 出发整备（B30，PLAN-Q232-04）：回营区，同行者先留作候补（营区公共照护），主角在广场配给处真实采集净水与罐头、
    /// 在医务室（室内）休息等配给处次日补货，直到囤够这条线的食水（每人净水 waterPerHead 份、食物 foodPerHead 份）且全队健康回到 85%，最多 maxDays 日；
    /// 然后把在营区的名册成员按容量编入同行（林昭在前），食水按人头平分给同行者。第二条线 C3 固定战首次揭示按 C3 出 3 名敌人（A09 E4、EC03），带满同行实打。
    /// </summary>
    private void Depart(Comp20Driver d, string label, bool takeMates = true, int waterPerHead = 9, int foodPerHead = 5, int maxDays = 5)
    {
        var s = d.S;
        d.Leg($"{label}：→营区广场", () => d.Go("h02_square"));
        foreach (var a in s.Party.Where(a => a != GameState.PlayerId).ToList()) Ok(E.SetActive(s, a, false), $"{a} 囤货期间留作候补");
        var heads = takeMates ? Math.Min(E.PartyCapacity(s), 1 + s.Roster.Count(r => r.Present)) : 1;
        int needW = waterPerHead * heads, needF = foodPerHead * heads;
        var until = s.GameMinute + maxDays * 1440;
        d.Leg($"{label}：营区配给处囤食水（{heads}人，目标净水{needW} 食物{needF}）", () =>
        {
            for (var k = 0; k < 40 && s.GameMinute < until; k++)
            {
                d.Go("h02_square");
                d.Provision();
                if (d.Stock("clean_water") >= needW && d.Stock("canned_food") + d.Stock("canned_beans") >= needF && d.MinHealthFrac() >= 0.85) break;
                d.Go("h04_infirmary");
                var used = d.Recover(1.0, 6);
                if (used < 360) d.WaitUntil(Math.Min(until, s.GameMinute + 360 - used));
            }
            d.Go("h02_square");
        });
        foreach (var r in s.Roster.Where(r => r.Present && !s.Party.Contains(r.ActorId)).OrderBy(r => r.ActorId == Lin ? 0 : 1).ToList())
        {
            if (!takeMates || s.Party.Count >= E.PartyCapacity(s)) break;
            Ok(E.SetActive(s, r.ActorId, true), $"编入 {r.ActorId}");
        }
        d.Note($"{label}编队：{d.PartyText()}（{s.Party.Count}/{E.PartyCapacity(s)}）");
        if (takeMates) d.RefitWorn(label);
        d.Pack();
    }

    /// <summary>一条线走完：同行者全员还在队里（没有渴死饿死）。</summary>
    private static void MatesAlive(Comp20Driver d, List<string> mates, string what)
    {
        foreach (var m in mates) Assert.That(d.S.Party, Does.Contain(m), $"{what}：{m} 全程存活同行");
    }

    /// <summary>教会线：接待庭领公开路线 → 地下圣所逐房真实探索与固定战 → 核心领取圣所旧路档册 → 单向出口回墓园。</summary>
    private void ChurchLine(Comp20Driver d)
    {
        var s = d.S;
        var c0 = E.BattleCapacity(s);
        var mates = s.Party.Where(a => a != GameState.PlayerId).ToList();
        var mark = d.Mark();
        d.Leg("→教会接待庭", () => d.Go("c02_church"));
        Ok(E.StartCityService(s, "church_reception_existing", null, "FA02_accept_no_oath"), "教会接待");
        d.ReadDialogues();
        Assert.That(s.Progress.Flags, Does.Contain("FA02_route_briefed"));
        d.Leg("接待庭→圣堂（室内）休整到健康≥85%", () => { d.Go("c07_sanctuary"); d.Recover(); });
        d.Leg("圣堂→地下圣所核心（逐房探索、固定战与守卫战）", () => d.Go("d01_08"));
        Assert.That(E.InstanceFact(s, "d01", "guardianDefeated"), Is.True, "守卫已击败");
        d.Leg("核心：探索并领取圣所旧路档册", () =>
        {
            d.ExploreFull();
            d.Do(ActionRequest.Gather("d01_08_unique"), required: true);
        });
        Assert.That(s.Progress.Flags, Does.Contain("church_route_record"));
        Assert.That(E.DungeonCleared(s, "d01"), Is.True);
        Assert.That(GameEngine.QuestDone(s, "FA02"), Is.True);
        Assert.That(E.BattleCapacity(s), Is.EqualTo(c0 + 1), "第一份核心记录 C+1");
        d.Leg("核心→单向出口回墓园", () => d.Go("c03_crypt"));
        Assert.That(s.Expedition, Is.Null, "走出口结束远征");
        MatesAlive(d, mates, "教会线");
        d.LineSummary($"教会线（C{c0}→C{c0 + 1}，营区出发→墓园出口）", mark, mates);
    }

    /// <summary>科学院线：外院→接待处领环境路线 → 灰塔封存区逐房 → 核心领取封存区安全规程 → 出口 09 回封存实验楼。</summary>
    private void AcademyLine(Comp20Driver d)
    {
        var s = d.S;
        var c0 = E.BattleCapacity(s);
        var mates = s.Party.Where(a => a != GameState.PlayerId).ToList();
        var mark = d.Mark();
        d.Leg("→科学院外院→接待处", () => { d.Go("r02_academy"); d.Go("r04_reception"); });
        var pv = E.CityServicePreview(s, "academy_reception_existing")!;
        Ok(E.StartCityService(s, "academy_reception_existing", null, pv.Choices[0].Id), "科学院接待");
        d.ReadDialogues();
        Assert.That(s.Progress.Flags, Does.Contain("FA03_environment_briefed"));
        d.Leg("接待处（室内）休整到健康≥85%", () => d.Recover());
        d.Leg("接待处→灰塔封存区核心（逐房探索、固定战与守卫战）", () => d.Go("d02_08"));
        Assert.That(E.InstanceFact(s, "d02", "guardianDefeated"), Is.True, "守卫已击败");
        d.Leg("核心：探索并领取封存区安全规程", () =>
        {
            d.ExploreFull();
            d.Do(ActionRequest.Gather("d02_08_unique"), required: true);
        });
        Assert.That(s.Progress.Flags, Does.Contain("academy_safety_record"));
        Assert.That(GameEngine.QuestDone(s, "FA03"), Is.True);
        Assert.That(E.BattleCapacity(s), Is.EqualTo(c0 + 1));
        d.Leg("核心→出口 09→封存实验楼", () => d.Go("r03_lab"));
        Assert.That(s.Expedition, Is.Null);
        MatesAlive(d, mates, "科学院线");
        d.LineSummary($"科学院线（C{c0}→C{c0 + 1}，营区出发→封存实验楼出口）", mark, mates);
    }

    /// <summary>
    /// 回营区：C4 时名册里在营区的成员都编入同行（队伍扩容，两线已带的继续在队），营区事务整理 FA04 开警备仓入口；
    /// 两线远征到此结束，同行者留营区作候补（之后可选地牢、72h 重置等待与安全屋是单人路线，与 B25 相同），主角再采一次食水。
    /// </summary>
    private void Expand(Comp20Driver d)
    {
        var s = d.S;
        d.Leg("→营区广场", () => d.Go("h02_square"));
        Assert.That(E.PartyCapacity(s), Is.EqualTo(4));
        foreach (var r in s.Roster.Where(r => r.Present && !s.Party.Contains(r.ActorId)).ToList())
            Ok(E.SetActive(s, r.ActorId, true), $"编入 {r.ActorId}");
        var mates = s.Party.Where(a => a != GameState.PlayerId).ToList();
        Assert.That(mates, Has.Count.EqualTo(s.Roster.Count(r => r.Present)));
        d.Note($"C4 编队：{d.PartyText()}（{s.Party.Count}/{E.PartyCapacity(s)}）");
        d.Leg("营区事务整理两份记录（FA04，10分钟）", () =>
        {
            Ok(E.StartCityService(s, "hub_affairs_record"), "FA04 整理");
            d.Drive();
            d.ReadDialogues();
        });
        Assert.That(s.Progress.Flags, Does.Contain("FA04_merged"));
        Assert.That(s.UnlockedEdges, Does.Contain("o03_police__d03_01"));
        Depart(d, "单人后续路线", takeMates: false, waterPerHead: 14, foodPerHead: 6);
    }

    /// <summary>一次可选地牢：警备仓 d03 进入危险房间（确认提交远征）→ 主动撤出（惩罚包 + 撤出时长）→ 回父地点。</summary>
    private void OptionalDungeon(Comp20Driver d)
    {
        var s = d.S;
        d.Leg("营区→旧城警署（d03 父地点）", () => d.Go("o03_police"));
        d.Leg("进入警备仓缓冲→第一间危险房间", () =>
        {
            d.Go("d03_01");
            d.ExploreFull();
            var next = E.Config.Edges.First(e => e.TraversableFrom("d03_01") && e.DungeonId == "d03" && E.IsDangerRoom(e.Other("d03_01"))).Other("d03_01");
            d.Go(next);
        });
        Assert.That(s.Expedition, Is.Not.Null, "确认进入危险房间即提交远征");
        var before = s.Stats.Clone();
        var pv = E.AbortPreview(s);
        Assert.That(pv.Reason, Is.Null, pv.Reason);
        d.Note($"撤出预估：{string.Join("→", pv.Route)}，基础{pv.BaseMinutes}分钟×{pv.Factor:0.##}={pv.TotalMinutes}分钟");
        d.Leg("主动撤出警备仓", () =>
        {
            Ok(E.AbortExpedition(s, "comp20-abort"), "撤出");
            d.Drive();
            d.ReadDialogues();
        });
        Assert.That(s.Expedition, Is.Null);
        Assert.That(s.CurrentLocation, Is.EqualTo("o03_police"));
        Assert.That(s.Stats.Satiety, Is.LessThan(before.Satiety), "撤出惩罚包实扣");
        Assert.That(E.DungeonCleared(s, "d03"), Is.False);
    }

    /// <summary>72 世界小时：回墓园等到 d01 可重置→重置换代；再回收容所看候选换批。</summary>
    private void ResetAndRecruit(Comp20Driver d)
    {
        var s = d.S;
        var gen = s.DungeonInstances["d01"].Generation;
        var eligible = s.DungeonInstances["d01"].ResetEligibleAt!.Value;
        var cycle0 = _cycleAtOpen;
        d.Leg("→教堂墓园（d01 父地点）", () => d.Go("c03_crypt"));
        d.Leg("墓园等到 d01 满 72 世界小时", () => d.WaitUntil(eligible));
        Assert.That(E.DungeonResetReady(s, "d01"), Is.True);
        Ok(E.ResetDungeon(s, "d01", "comp20-reset"), "重置 d01");
        Assert.That(s.DungeonInstances["d01"].Generation, Is.EqualTo(gen + 1));
        E.ResetDungeon(s, "d01", "comp20-reset");
        Assert.That(s.DungeonInstances["d01"].Generation, Is.EqualTo(gen + 1), "同请求号重放不生第二代");
        Assert.That(s.Progress.Flags, Does.Contain("church_route_record"), "核心记录本局唯一，不随换代回收");
        d.Leg("墓园→收容所", () => d.Go("h06_shelter"));
        var cycle = E.RecruitCycleAt(s, s.GameMinute);
        Assert.That(cycle, Is.GreaterThan(cycle0), "跨 72 小时候选换批");
        d.Note($"收容所候选期 {cycle0}→{cycle}：{string.Join("、", s.Recruit.Candidates.Select(c => c.Name))}");
    }

    /// <summary>安全屋：营外安全屋地块探索满→建基础安全屋（建材由开发入口放地面）→进屋睡一觉，记恢复。</summary>
    private void Safehouse(Comp20Driver d)
    {
        var s = d.S;
        d.Leg("→营外安全屋地块", () => { d.Go("h05_homestead"); d.ExploreFull(); });
        foreach (var (id, n) in new[] { ("wood", 8), ("fiber", 6), ("cloth", 2), ("stone", 4) }) E.CreateItems(s, id, n, s.Ground);
        d.Shortcut("基础安全屋建材（木8、纤维6、布2、石4）由开发入口放到地面");
        d.Leg("建基础安全屋", () => d.Do(ActionRequest.Craft("safehouse_base", null, null, 1), required: true));
        Assert.That(E.SafehouseBuilt(s, "h05_homestead"), Is.True);
        Ok(E.EnterSafehouse(s), "进屋");
        var st0 = s.Stats.Clone();
        d.Leg("安全屋内睡眠", () => d.Do(ActionRequest.Sleep(E.Config.Actions.SleepMinutes.Max()), required: true));
        Assert.That(s.Stats.Wakefulness, Is.GreaterThan(st0.Wakefulness));
        Assert.That(s.Stats.Health, Is.GreaterThanOrEqualTo(st0.Health), "屋内遮蔽睡眠不掉血");
        d.Note($"屋内睡眠前后：H{st0.Health:0.0}→{s.Stats.Health:0.0} E{st0.Energy:0.0}→{s.Stats.Energy:0.0} S{st0.Wakefulness:0}→{s.Stats.Wakefulness:0}");
        Ok(E.ExitSafehouse(s), "出门");
    }

    // ───────────── 新局两条顺序 ─────────────

    /// <summary>
    /// COMP20 新局：教会线先 / 科学院线先各跑一遍完整链路。B30（PLAN-Q232-04）：两条线都是多人真实远征——第一条线 C2 带林昭，
    /// 第二条线 C3 带林昭与收容所登记的候选；食水全部来自营区配给处真实采集与随身携带，同行者用同行食饮入口喂；固定战全部实打。
    /// </summary>
    [TestCase(true, 1u, TestName = "COMP20_NewGame_ChurchFirst")]
    [TestCase(false, 1u, TestName = "COMP20_NewGame_AcademyFirst")]
    public void COMP20_NewGame(bool churchFirst, uint seed)
    {
        var s = NewStory(seed);
        var d = new Comp20Driver(E, s);
        RunLogged(d, $"COMP20 新局（{(churchFirst ? "教会线先" : "科学院线先")}，种子{seed}）", () =>
        {
            Prologue(d, legacy: false);
            HubServices(d);
            Depart(d, "第一条线出发");
            Assert.That(s.Party, Does.Contain(Lin), "C2 带林昭同行");
            if (churchFirst) ChurchLine(d); else AcademyLine(d);
            Assert.That(E.BattleCapacity(s), Is.EqualTo(3), "第一份核心记录 → C3");
            Depart(d, "第二条线出发");
            Assert.That(s.Party, Has.Count.EqualTo(3), "C3 带满两名同行者");
            if (churchFirst) AcademyLine(d); else ChurchLine(d);
            Assert.That(E.BattleCapacity(s), Is.EqualTo(4), "两份核心记录 → C4");
            Assert.That(d.CompanionWater, Is.GreaterThan(0), "途中用同行食饮入口给同行者喝过水");
            Assert.That(d.CompanionFood, Is.GreaterThan(0), "途中用同行食饮入口给同行者吃过东西");
            Assert.That(d.FedFromPlayer, Is.GreaterThan(0), "出物用过主角随身");
            Assert.That(d.FedFromSelf, Is.GreaterThan(0), "出物用过目标本人随身");
            Assert.That(d.FixedResults, Is.Not.Empty, "固定战实打");
            Assert.That(d.Shortcuts.Any(x => x.Contains("固定战")), Is.False, "不再用开发入口记固定战胜利");
            Assert.That(d.Shortages, Is.Empty, "两线远征全程没有人缺食缺水（食水全是真实采集与携带）");
            Expand(d);
            OptionalDungeon(d);
            ResetAndRecruit(d);
            Safehouse(d);
            Assert.That(s.Progress.Dead, Is.False);
        });
    }

    // ───────────── 失败与死亡 ─────────────

    /// <summary>新局走到地下圣所 d01_05（守卫前一间，固定战已打完、探索满）后存档，返回存档 JSON。</summary>
    private string SaveBeforeCryptGuard(Comp20Driver d)
    {
        var s = d.S;
        Prologue(d, legacy: false);
        HubServices(d);
        d.Leg("→营区广场", () => d.Go("h02_square"));
        d.Leg("营区配给处采集食水（单人）", () => d.Provision());
        d.Leg("→教会接待庭", () => d.Go("c02_church"));
        Ok(E.StartCityService(s, "church_reception_existing", null, "FA02_accept_no_oath"), "教会接待");
        d.ReadDialogues();
        d.Leg("接待庭→地下圣所 d01_05（守卫前一间）", () => d.Go("d01_05"));
        d.ExploreFull();
        Assert.That(s.Expedition, Is.Not.Null);
        return SaveSerializer.Serialize(s);
    }

    /// <summary>
    /// 失败另验：同一存档读出两份——①健康压到 4（开发入口）接守卫战、低于三成即撤退：活着退回来路、守卫未击败、没有记录、可再来；
    /// ②健康压到 1 接守卫战不撤：死亡优先，不写通关/记录、不发守卫回收，营区候补名册不受影响，之后一切行动被拒。
    /// </summary>
    [Test]
    public void COMP20_NewGame_GuardRetreatAndDeath()
    {
        var s0 = NewStory(1);
        var d0 = new Comp20Driver(E, s0);
        string json = "";
        RunLogged(d0, "COMP20 失败/死亡：走到守卫前存档", () => json = SaveBeforeCryptGuard(d0));

        // ① 撤退
        var a = SaveSerializer.Deserialize(json, Cfg);
        var da = new Comp20Driver(E, a);
        RunLogged(da, "COMP20 失败：守卫战撤退", () =>
        {
            E.DebugSetStats(a, 4, a.Stats.Satiety, a.Stats.Hydration, a.Stats.Energy, a.Stats.Wakefulness, a.Stats.Morale);
            da.Shortcut("读守卫前存档，健康改为 4（开发入口）");
            da.Do(ActionRequest.Travel("d01_07"), required: true);
            Ok(E.EngageFixed(a), "接守卫战");
            for (var k = 0; k < 6 && a.Battle != null; k++) da.Fight(allowRetreat: true);
            Assert.That(a.Progress.Dead, Is.False, "撤退成功、活着");
            Assert.That(da.Retreats, Is.GreaterThan(0));
            Assert.That(a.CurrentLocation, Is.Not.EqualTo("d01_07"), "退回来路");
            Assert.That(E.InstanceFact(a, "d01", "guardianDefeated"), Is.False);
            Assert.That(a.Progress.Flags, Does.Not.Contain("church_route_record"));
            Assert.That(E.FixedEnemiesAt(a, "d01_07"), Is.Not.Empty, "守卫仍在，可再来");
            Assert.That(a.Expedition, Is.Not.Null, "普通战斗撤退不结束远征（COMP09）");
            da.Note($"撤退后在 {E.LocName(a.CurrentLocation)}，H{a.Stats.Health:0.0}，守卫仍在");
        });

        // ② 死亡
        var b = SaveSerializer.Deserialize(json, Cfg);
        var db = new Comp20Driver(E, b);
        RunLogged(db, "COMP20 死亡：守卫战不撤", () =>
        {
            E.DebugSetStats(b, 1, b.Stats.Satiety, b.Stats.Hydration, b.Stats.Energy, b.Stats.Wakefulness, b.Stats.Morale);
            db.Shortcut("读守卫前存档，健康改为 1（开发入口）");
            var roster = b.Roster.Count(r => r.Present);
            db.Do(ActionRequest.Travel("d01_07"), required: true);
            Ok(E.EngageFixed(b), "接守卫战");
            db.Fight(allowRetreat: false);
            Assert.That(b.Progress.Dead, Is.True);
            Assert.That(b.Progress.Death, Is.Not.Null);
            Assert.That(E.DungeonCleared(b, "d01"), Is.False, "死亡不写通关");
            Assert.That(b.Progress.Flags, Does.Not.Contain("church_route_record"));
            Assert.That(E.SalvageClaimed(b, "d01"), Is.False, "不发守卫回收");
            Assert.That(b.Roster.Count(r => r.Present), Is.EqualTo(roster), "营区候补不受影响");
            Assert.That(E.Start(b, ActionRequest.Wait(30)).Ok, Is.False, "死亡后行动被拒");
            db.Note($"死亡：{b.Progress.Death!.Cause}，在 {E.LocName(b.CurrentLocation)}");
        });
    }

    // ───────────── 旧局迁移 ─────────────

    /// <summary>
    /// 旧局：真实旧版本存档（Fixtures/save-5-midgame.json，schema 5、第1日 08:15 在营地）读入迁到当前 schema，
    /// 重复关键链：序章接续（旧档首战等效）→营区登记→营区服务→教会线核心记录（C3）→72h 重置/招募→安全屋恢复。
    /// </summary>
    [Test]
    public void COMP20_LegacySave5_KeyChain()
    {
        var json = File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "Fixtures", "save-5-midgame.json"));
        var s = SaveSerializer.Deserialize(json, Cfg, out var notes);
        Assert.That(s.SchemaVersion, Is.EqualTo(GameState.CurrentSchema));
        E.MigrateServiceLocations(s);   // 同网页读档（GameSession）：旧档服务从替身地点迁到营区广场/收容所
        var d = new Comp20Driver(E, s);
        d.Note($"存档 5 → {s.SchemaVersion}，迁移说明 {notes.Count} 条");
        RunLogged(d, "COMP20 旧局（schema 5 迁移）", () =>
        {
            Prologue(d, legacy: true);
            HubServices(d);
            Depart(d, "教会线出发");
            ChurchLine(d);
            Assert.That(E.BattleCapacity(s), Is.EqualTo(3));
            Depart(d, "单人后续路线", takeMates: false, waterPerHead: 14, foodPerHead: 6);
            ResetAndRecruit(d);
            Safehouse(d);
            Assert.That(s.Progress.Dead, Is.False);
        });
    }
}
