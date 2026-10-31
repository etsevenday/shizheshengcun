using Newtonsoft.Json.Linq;
using Survival.Core.Config;
using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 B8c「答复 Q90 候补自主照护与恢复模块」（R108）：主角恢复只有休息/睡眠两模块、全天有效；
/// 候补按自身 S/E 自主 rest/sleep，N/W/M 慢速公共补给到 80，恢复设施按营区实际已完工设施；旧档迁移一次。
/// 出处：R08/S13「Q90 休息睡眠与候补自主恢复」本轮章、S12/S09/S01/S14/A11 Q90 本轮章、合同 assets/20261031/party/party-adjudications.json reserveCare/recoveryUI；
/// 验收 A10 PTY-Q90a—e。
/// </summary>
public class B8cReserveCareTests : TestBase
{
    private const string Gu = "npc_gu_cheng";

    /// <summary>新局（无剧情），在营地、C4，林昭/周砚/沈禾同行、顾承候补；不刷新敌人。时间定在第 1 日 hh:00。</summary>
    private GameState CampWithReserve(int hh = 10)
    {
        var s = New();
        s.Progress.Flags.Add("hub_registered");
        s.Progress.Flags.Add("church_route_record");
        s.Progress.Flags.Add("academy_safety_record");
        s.SpawnNextCheck = int.MaxValue;
        s.PatrolNextCheck = int.MaxValue;
        foreach (var id in new[] { "npc_lin_zhao", "npc_zhou_yan", "npc_shen_he", Gu }) E.DebugRecruit(s, id);
        Assert.That(E.IsReserve(s, Gu), Is.True);
        s.GameMinute = At(1, hh, 0);
        s.Player.Stats.Wakefulness = 100; s.Player.Stats.Satiety = 100; s.Player.Stats.Hydration = 100;
        return s;
    }

    private void Hours(GameState s, int hours)
    {
        for (var k = 0; k < hours; k++) { Start(s, ActionRequest.Wait(60)); Run(s); s.Main = null; }
    }

    private static void Body(ActorState a, double n, double w, double e, double sl, double m, double h = 20)
    {
        a.Stats.Satiety = n; a.Stats.Hydration = w; a.Stats.Energy = e; a.Stats.Wakefulness = sl; a.Stats.Morale = m; a.Stats.Health = h;
    }

    // ───────────── PTY-Q90a：主角两恢复模块、全天有效；内部 wait 不变成恢复 ─────────────

    /// <summary>PTY-Q90a：恢复面板只有休息、睡眠两模块；普通等待不是恢复模块。</summary>
    [Test]
    public void PTY_Q90a_RecoveryModules_OnlyRestAndSleep()
    {
        Assert.That(GameEngine.RecoveryModules, Is.EqualTo(new[] { "rest", "sleep" }));
        Assert.That(GameEngine.IsRecoveryModule("wait"), Is.False);
        Assert.That(GameEngine.IsRecoveryModule("rest"), Is.True);
        Assert.That(GameEngine.IsRecoveryModule("sleep"), Is.True);
    }

    /// <summary>PTY-Q90a：休息与睡眠全天可开始、效果不按钟点（03:00 与 14:00 同样的速率）。</summary>
    [Test]
    public void PTY_Q90a_RestAndSleep_AllDaySameEffect()
    {
        foreach (var kind in new[] { "rest", "sleep" })
        {
            var deltas = new List<(double e, double sl)>();
            foreach (var hh in new[] { 3, 14 })
            {
                var s = New();
                s.SpawnNextCheck = int.MaxValue; s.PatrolNextCheck = int.MaxValue;
                s.GameMinute = At(2, hh, 0);
                SetStats(s, 20, 80, 80, 5, 50, 60);
                Assert.That(E.CanStart(s, kind == "rest" ? ActionRequest.Rest(60) : ActionRequest.Sleep(120)), Is.Null, $"{kind} {hh}:00 可以开始");
                Start(s, kind == "rest" ? ActionRequest.Rest(60) : ActionRequest.Sleep(120));
                Run(s);
                deltas.Add((s.Stats.Energy - 5, s.Stats.Wakefulness - 50));
            }
            Assert.That(deltas[0].e, Is.EqualTo(deltas[1].e).Within(1e-9), $"{kind} 体力恢复不分昼夜");
            Assert.That(deltas[0].sl, Is.EqualTo(deltas[1].sl).Within(1e-9), $"{kind} 清醒变化不分昼夜");
        }
    }

    /// <summary>PTY-Q90a：RB01 登记的 30 分钟内部 wait 仍按 wait 费率（不回体力、清醒 −4/h），不被改成休息恢复。</summary>
    [Test]
    public void PTY_Q90a_ServiceWait_StaysWaitRate()
    {
        var s = New();
        s.Progress.Flags.Add("hub_registered");
        s.SpawnNextCheck = int.MaxValue; s.PatrolNextCheck = int.MaxValue;
        s.GameMinute = At(1, 10, 0);
        SetStats(s, 20, 80, 80, 5, 80, 60);
        Assert.That(E.StartEscortRegistration(s, "rb").Ok, Is.True);
        Assert.That(s.Main!.Type, Is.EqualTo(ActionType.Wait));
        Run(s);
        Assert.That(s.Stats.Energy, Is.EqualTo(5).Within(1e-9), "登记等待不回体力");
        Assert.That(s.Stats.Wakefulness, Is.EqualTo(78).Within(1e-9), "按 wait：清醒 −4/h × 0.5h");
    }

    /// <summary>PTY-Q90a：火堆“等待烹饪”的身体费率仍是 wait（不回体力），不变成休息。</summary>
    [Test]
    public void PTY_Q90a_CookWait_StaysWaitRate()
    {
        var s = New();
        var plan = E.PreviewCookWait(s);
        Assert.That(plan.BodyRates.Energy, Is.EqualTo(Cfg.HourlyRates["wait"].Energy));
        Assert.That(plan.BodyRates.Wakefulness, Is.EqualTo(Cfg.HourlyRates["wait"].Wakefulness));
    }

    // ───────────── PTY-Q90b：自主 rest/sleep 阈值与滞回 ─────────────

    /// <summary>PTY-Q90b：缺模式按进入条件判定；rest 中 S&lt;60 或 E&lt;6 进 sleep；sleep 中 S≥90 且 E≥14 才回 rest，其余保持。</summary>
    [Test]
    public void PTY_Q90b_Thresholds_AndHysteresis()
    {
        var a = new ActorState { Id = "x" };
        string Mode(string? cur, double sl, double e) { a.ReserveMode = cur; a.Stats.Wakefulness = sl; a.Stats.Energy = e; return E.NextReserveMode(a); }
        Assert.That(Mode(null, 60, 6), Is.EqualTo("rest"), "首次：S=60、E=6 不满足睡眠触发");
        Assert.That(Mode(null, 59.9, 20), Is.EqualTo("sleep"));
        Assert.That(Mode(null, 100, 5.9), Is.EqualTo("sleep"));
        Assert.That(Mode("rest", 70, 8), Is.EqualTo("rest"));
        Assert.That(Mode("rest", 59, 8), Is.EqualTo("sleep"));
        Assert.That(Mode("rest", 95, 5), Is.EqualTo("sleep"));
        Assert.That(Mode("sleep", 95, 13.9), Is.EqualTo("sleep"), "E 未到 14 继续睡");
        Assert.That(Mode("sleep", 89.9, 20), Is.EqualTo("sleep"), "S 未到 90 继续睡");
        Assert.That(Mode("sleep", 70, 10), Is.EqualTo("sleep"), "已过进入线但未到退出线：保持原模式，不抖动");
        Assert.That(Mode("sleep", 90, 14), Is.EqualTo("rest"));
    }

    /// <summary>PTY-Q90b：世界 tick 里段首判定并保存模式；睡到 S≥90 且 E≥14 那一段起改回休息。</summary>
    [Test]
    public void PTY_Q90b_WorldTick_SwitchesBySelfState()
    {
        var s = CampWithReserve(10);
        var gu = s.Actors[Gu];
        Body(gu, 70, 70, 13, 50, 60);
        Assert.That(gu.ReserveMode, Is.Null, "刚留作候补：模式未定");
        Hours(s, 1);
        Assert.That(gu.ReserveMode, Is.EqualTo("sleep"));
        Assert.That(gu.Stats.Wakefulness, Is.EqualTo(56).Within(1e-6), "营地露天睡眠 S+6/h");
        // 直接置到 S=89.5、E=14：下一段还睡（S 未到 90），睡一段 S+0.5 后再下一段改休息
        gu.Stats.Wakefulness = 89.5; gu.Stats.Energy = 14;
        Start(s, ActionRequest.Wait(30));
        E.Advance(s);
        Assert.That(gu.ReserveMode, Is.EqualTo("sleep"));
        Assert.That(gu.Stats.Wakefulness, Is.EqualTo(90).Within(1e-6));
        E.Advance(s);
        Assert.That(gu.ReserveMode, Is.EqualTo("rest"), "段首 S≥90、E≥14 回休息");
        Assert.That(gu.Stats.Wakefulness, Is.EqualTo(90 - 4.0 / 12).Within(1e-6), "本段按休息：S −4/h");
    }

    /// <summary>PTY-Q90b：22:00/06:00/午夜不强制切换，跨边界每段只结算一次（一小时恰好一小时的休息量）。</summary>
    [Test]
    public void PTY_Q90b_ClockBoundaries_NoSwitch_NoDoubleSettle()
    {
        var s = CampWithReserve(21);
        s.GameMinute = At(1, 21, 30);
        var gu = s.Actors[Gu];
        Body(gu, 70, 70, 7, 80, 60);
        var seg = s.SegmentCount;
        Hours(s, 1);   // 21:30—22:30 跨 22:00
        Assert.That(s.SegmentCount - seg, Is.EqualTo(12));
        Assert.That(gu.ReserveMode, Is.EqualTo("rest"), "22:00 不强制睡眠");
        Assert.That(gu.Stats.Energy, Is.EqualTo(13).Within(1e-6), "休息 E+6/h，恰好一小时，没有双结算");
        Assert.That(gu.Stats.Wakefulness, Is.EqualTo(76).Within(1e-6));
        // 跨午夜与 06:00：睡眠中的候补不因天亮改休息
        var t = CampWithReserve(5);
        t.GameMinute = At(1, 5, 30);
        var g2 = t.Actors[Gu];
        Body(g2, 70, 70, 3, 40, 60);
        Hours(t, 1);   // 05:30—06:30
        Assert.That(g2.ReserveMode, Is.EqualTo("sleep"), "06:00 不强制起床");
        Assert.That(g2.Stats.Wakefulness, Is.EqualTo(46).Within(1e-6));
        Assert.That(g2.Stats.Energy, Is.EqualTo(6).Within(1e-6));
        var u = CampWithReserve(23);
        u.GameMinute = At(1, 23, 30);
        var g3 = u.Actors[Gu];
        Body(g3, 70, 70, 10, 80, 60);
        Hours(u, 1);   // 23:30—00:30
        Assert.That(g3.ReserveMode, Is.EqualTo("rest"));
        Assert.That(g3.Stats.Energy, Is.EqualTo(16).Within(1e-6));
    }

    // ───────────── PTY-Q90c：慢速公共补给 ─────────────

    /// <summary>PTY-Q90c：N/W 净 +4/h、M 公共 +1/h（无棚休息 M 净 +1），按段提交、补到 80 为止；不生成物品、不扣玩家库存。</summary>
    [Test]
    public void PTY_Q90c_PublicCare_NetRates_TargetAndNoItems()
    {
        var s = CampWithReserve(10);
        var gu = s.Actors[Gu];
        Body(gu, 50, 50, 18, 95, 50);
        var items = s.Items.Select(i => (i.InstanceId, i.Owner, i.ItemId, i.Count)).ToList();
        Start(s, ActionRequest.Wait(30));
        E.Advance(s);
        Assert.That(gu.Stats.Satiety, Is.EqualTo(50 + 4.0 / 12).Within(1e-9), "每段 1/12 小时分次提交");
        s.Main = null;
        Hours(s, 1);
        Assert.That(gu.ReserveMode, Is.EqualTo("rest"));
        Assert.That(gu.Stats.Satiety, Is.EqualTo(54 + 4.0 / 12).Within(1e-6));
        Assert.That(gu.Stats.Hydration, Is.EqualTo(54 + 4.0 / 12).Within(1e-6));
        Assert.That(gu.Stats.Morale, Is.EqualTo(51 + 1.0 / 12).Within(1e-6), "无棚休息 M 0 + 公共 1");
        Body(gu, 79, 78, 18, 95, 79.9);
        Hours(s, 1);
        Assert.That(gu.Stats.Satiety, Is.EqualTo(80).Within(1e-9), "公共补给只补到 80");
        Assert.That(gu.Stats.Hydration, Is.EqualTo(80).Within(1e-9));
        Assert.That(gu.Stats.Morale, Is.EqualTo(80).Within(1e-9));
        Assert.That(s.Items.Select(i => (i.InstanceId, i.Owner, i.ItemId, i.Count)), Is.EqualTo(items), "不生成、不扣任何物品");
    }

    /// <summary>PTY-Q90c：已高于 80 不截降，照常自然消耗；露天睡眠 M 净 +0.6/h。</summary>
    [Test]
    public void PTY_Q90c_HighValuesNotTruncated_ExposedSleepMorale()
    {
        var s = CampWithReserve(10);
        var gu = s.Actors[Gu];
        Body(gu, 90, 90, 18, 95, 90);
        Hours(s, 1);
        Assert.That(gu.Stats.Satiety, Is.EqualTo(88.8).Within(1e-6), "N 高于 80：只自然消耗 −1.2/h");
        Assert.That(gu.Stats.Hydration, Is.EqualTo(87.6).Within(1e-6));
        Assert.That(gu.Stats.Morale, Is.EqualTo(90).Within(1e-6), "休息 M 0：不被截到 80");
        Body(gu, 90, 90, 3, 40, 50);
        Hours(s, 1);
        Assert.That(gu.ReserveMode, Is.EqualTo("sleep"));
        Assert.That(gu.Stats.Morale, Is.EqualTo(50.6).Within(1e-6), "露天睡眠 M −0.4 + 公共 1 = 净 +0.6/h");
    }

    /// <summary>PTY-Q90c：入队/留守 0 时间不补；编入同行后立即没有公共补给（按 wait 真实消耗）。</summary>
    [Test]
    public void PTY_Q90c_ZeroTimeToggle_NoTopUp_ActiveGetsNone()
    {
        var s = CampWithReserve(10);
        var gu = s.Actors[Gu];
        Body(gu, 30, 30, 18, 95, 50);
        Assert.That(E.SetActive(s, "npc_shen_he", false).Ok, Is.True);
        Assert.That(E.SetActive(s, Gu, true).Ok, Is.True);
        Assert.That(E.SetActive(s, Gu, false).Ok, Is.True);
        Assert.That(E.SetActive(s, Gu, true).Ok, Is.True);
        Assert.That(gu.Stats.Satiety, Is.EqualTo(30), "切换本身 0 时间不恢复");
        Assert.That(gu.ReserveMode, Is.Null, "编入同行清掉候补模式");
        Hours(s, 1);
        Assert.That(gu.Stats.Satiety, Is.EqualTo(28.8).Within(1e-6), "同行者无公共补给：按 wait −1.2/h");
        Assert.That(gu.Stats.Hydration, Is.EqualTo(27.6).Within(1e-6));
        var shen = s.Actors["npc_shen_he"];
        Assert.That(shen.ReserveMode, Is.EqualTo("rest"), "沈禾留作候补后下一真实 tick 才判定并得照护");
    }

    // ───────────── PTY-Q90d：实际完工设施 ─────────────

    /// <summary>PTY-Q90d：无设施露天睡眠 S+6；营地遮蔽棚建成后 S+12、M 净 +2；床垫再 E+1；棚内休息 M 净 +3。</summary>
    [Test]
    public void PTY_Q90d_FacilityByActualCampState()
    {
        var s = CampWithReserve(10);
        var gu = s.Actors[Gu];
        Body(gu, 70, 70, 3, 40, 50);
        Hours(s, 1);
        Assert.That(E.ReserveFacility(s), Is.EqualTo("none"));
        Assert.That(gu.Stats.Wakefulness, Is.EqualTo(46).Within(1e-6), "无设施：露天睡眠");
        Assert.That(gu.Stats.Energy, Is.EqualTo(6).Within(1e-6));
        s.Locations["camp"].Facility.ShelterBuilt = true;
        Body(gu, 70, 70, 3, 40, 50);
        Hours(s, 1);
        Assert.That(E.ReserveFacility(s), Is.EqualTo("shelter"));
        Assert.That(gu.Stats.Wakefulness, Is.EqualTo(52).Within(1e-6), "遮蔽睡眠 S+12");
        Assert.That(gu.Stats.Energy, Is.EqualTo(7).Within(1e-6));
        Assert.That(gu.Stats.Morale, Is.EqualTo(52).Within(1e-6), "遮蔽睡眠 M+1 + 公共 1");
        s.Locations["camp"].Facility.BedBuilt = true;
        Body(gu, 70, 70, 3, 40, 50);
        Hours(s, 1);
        Assert.That(E.ReserveFacility(s), Is.EqualTo("bed"));
        Assert.That(gu.Stats.Energy, Is.EqualTo(8).Within(1e-6), "床垫睡眠 E 再 +1");
        Body(gu, 70, 70, 18, 95, 50);
        gu.ReserveMode = "rest";
        Hours(s, 1);
        Assert.That(gu.Stats.Morale, Is.EqualTo(53).Within(1e-6), "棚内休息 M+2 + 公共 1");
        Assert.That(E.ReserveCareLabel(s, gu), Is.EqualTo("自主休息 · 床垫"));
    }

    /// <summary>PTY-Q90d：低精神（M&lt;20）时全部正 E（含床垫）整体 ×0.5。</summary>
    [Test]
    public void PTY_Q90d_LowMorale_HalvesAllPositiveEnergy()
    {
        var s = CampWithReserve(10);
        s.Locations["camp"].Facility.ShelterBuilt = true;
        s.Locations["camp"].Facility.BedBuilt = true;
        var gu = s.Actors[Gu];
        Body(gu, 70, 70, 3, 40, 10);
        Hours(s, 1);
        Assert.That(gu.Stats.Energy, Is.EqualTo(3 + 2.5).Within(1e-6), "(4+1)×0.5");
    }

    /// <summary>PTY-Q90d：遮蔽棚施工中按无设施；本段末完工的下一段才按遮蔽恢复。</summary>
    [Test]
    public void PTY_Q90d_UnderConstruction_NoBonus_CompletedNextSegment()
    {
        var s = CampWithReserve(10);
        E.DebugGive(s, "wood", 4);
        E.DebugGive(s, "fiber", 4);
        var gu = s.Actors[Gu];
        Body(gu, 70, 70, 3, 0.5, 50);
        Start(s, ActionRequest.Craft("shelter"));
        var segs = 0;
        double before = 0;
        while (s.Main != null && s.Main.Type == ActionType.Build)
        {
            before = gu.Stats.Wakefulness;
            Assert.That(E.ReserveFacility(s), Is.EqualTo("none"), "施工中：无设施");
            var r = E.Advance(s);
            if (r.Status == SegmentStatus.Paused) E.Resume(s);
            segs++;
            Assert.That(segs, Is.LessThan(200));
        }
        Assert.That(s.Locations["camp"].Facility.ShelterBuilt, Is.True);
        Assert.That(gu.Stats.Wakefulness - before, Is.EqualTo(6.0 / 12).Within(1e-6), "完工那一段仍按露天");
        before = gu.Stats.Wakefulness;
        Start(s, ActionRequest.Wait(30));
        E.Advance(s);
        Assert.That(gu.Stats.Wakefulness - before, Is.EqualTo(12.0 / 12).Within(1e-6), "下一段起按遮蔽");
    }

    /// <summary>PTY-Q90d：设施按候补所在营区，不按玩家所在地；同行者按玩家所在地（异地设施不远程生效）。</summary>
    [Test]
    public void PTY_Q90d_ReserveUsesHubFacility_NotPlayerLocation()
    {
        var s = CampWithReserve(10);
        s.Locations["camp"].Facility.ShelterBuilt = true;
        E.DebugTeleport(s, "field");
        Assert.That(E.IsSheltered(s, "field"), Is.False);
        var gu = s.Actors[Gu];
        Body(gu, 70, 70, 3, 40, 50);
        Start(s, ActionRequest.Sleep(120));
        Run(s);
        Assert.That(gu.Stats.Wakefulness, Is.EqualTo(64).Within(1e-6), "候补在营地遮蔽处睡眠 S+12/h");
        Assert.That(E.ReserveFacility(s), Is.EqualTo("shelter"));
        // 反过来：营地没棚，玩家在别处也没有（现行地图没有可建棚的室内地点），候补不因别处任何设施受益
        var t = CampWithReserve(10);
        t.Locations["field"].Facility.ShelterBuilt = true;
        t.Locations["field"].Facility.BedBuilt = true;
        Assert.That(E.ReserveFacility(t), Is.EqualTo("none"));
    }

    // ───────────── PTY-Q90e：迁移、伤病、健康段首条件、死亡、同行 ─────────────

    /// <summary>PTY-Q90e：17→18 旧档初始化模式一次（按阈值）、规则版本 2，不补历史恢复、不截既有数值；再读不重复。</summary>
    [Test]
    public void PTY_Q90e_Save17To18_InitModeOnce_NoHistory()
    {
        var s = CampWithReserve(10);
        E.DebugRecruit(s, "npc_ye_xun");
        var gu = s.Actors[Gu];
        Body(gu, 85, 30, 18, 50, 90);
        var ye = s.Actors["npc_ye_xun"];
        Body(ye, 60, 60, 18, 95, 60);
        var j = JObject.Parse(SaveSerializer.Serialize(s));
        j["SchemaVersion"] = "17";
        j.Remove("ReserveCareRule");
        foreach (var a in ((JObject)j["Actors"]!).Properties()) ((JObject)a.Value).Remove("ReserveMode");
        var m = SaveSerializer.Deserialize(j.ToString(), Cfg, out _);
        Assert.That(m.SchemaVersion, Is.EqualTo("18"));
        Assert.That(m.ReserveCareRule, Is.EqualTo(2));
        Assert.That(m.Actors[Gu].ReserveMode, Is.EqualTo("sleep"), "S<60 → sleep");
        Assert.That(m.Actors["npc_ye_xun"].ReserveMode, Is.EqualTo("rest"));
        Assert.That(m.Actors["npc_lin_zhao"].ReserveMode, Is.Null, "同行者没有候补模式");
        Assert.That(m.GameMinute, Is.EqualTo(s.GameMinute), "不补历史、不推进时间");
        Assert.That(m.Actors[Gu].Stats.Satiety, Is.EqualTo(85), "不把既有 N 截到 80");
        Assert.That(m.Actors[Gu].Stats.Hydration, Is.EqualTo(30), "不瞬补 W");
        Assert.That(m.Actors[Gu].Stats.Morale, Is.EqualTo(90));
        Assert.That(m.Log.Last().Text, Does.Contain("存档已升级到版本18（候补自主照护）"));
        var again = SaveSerializer.Deserialize(SaveSerializer.Serialize(m), Cfg, out var notes2);
        Assert.That(notes2, Is.Empty);
        Assert.That(again.Actors[Gu].ReserveMode, Is.EqualTo("sleep"));
    }

    /// <summary>PTY-Q90e：腹泻额外失水照常（同段处理，补给不免疫），V 照推，H 按段首 N/W≥40 判定、不因本段补过线倒算。</summary>
    [Test]
    public void PTY_Q90e_DiseaseContinues_HealthBySegmentStart()
    {
        var s = CampWithReserve(10);
        s.Locations["camp"].Facility.ShelterBuilt = true;
        var gu = s.Actors[Gu];
        Body(gu, 70, 70, 18, 95, 60, 10);
        gu.DiarrheaUntil = s.GameMinute + 600;
        gu.Body.Infected = true; gu.Body.VirusLoad = 30; gu.Body.Immunity = 30;
        Hours(s, 1);
        Assert.That(gu.Stats.Hydration, Is.EqualTo(70 + 4 + Cfg.ItemActions.DiarrheaExtraHydrationPerHour).Within(1e-6), "腹泻额外失水照常");
        Assert.That(gu.Body.VirusLoad, Is.GreaterThan(30), "公共照护不清病毒");
        Assert.That(gu.DiarrheaUntil, Is.Not.Null, "腹泻计时照常");
        // 段首 N=39.9：本段补过 40 也不倒算本段健康恢复，下一段起才 +0.2/h
        Body(gu, 39.9, 70, 18, 95, 60, 10);
        gu.DiarrheaUntil = null;
        gu.Body.Infected = false; gu.Body.VirusLoad = 0; gu.Body.Immunity = 70;
        Start(s, ActionRequest.Wait(30));
        E.Advance(s);
        Assert.That(gu.Stats.Satiety, Is.GreaterThanOrEqualTo(40));
        Assert.That(gu.Stats.Health, Is.EqualTo(10).Within(1e-9), "段首 N<40：本段不自然恢复");
        E.Advance(s);
        Assert.That(gu.Stats.Health, Is.EqualTo(10 + 0.2 / 12).Within(1e-9), "下一段段首满足：遮蔽休息 +0.2/h");
    }

    /// <summary>PTY-Q90e：段首 N/W 归零的伤害照扣，补给不在判死后补救、无自动复活。</summary>
    [Test]
    public void PTY_Q90e_DeathNotRescuedByPublicCare()
    {
        var s = CampWithReserve(10);
        var gu = s.Actors[Gu];
        Body(gu, 0, 0, 18, 95, 60, 0.1);
        Start(s, ActionRequest.Wait(30));
        E.Advance(s);
        Assert.That(E.RosterOf(s, Gu)!.Dead, Is.True, "饥渴伤害本段把健康扣到 0：死亡优先");
        Assert.That(E.IsReserve(s, Gu), Is.False);
        Run(s);
        Assert.That(E.RosterOf(s, Gu)!.Dead, Is.True, "不复活");
    }
}
