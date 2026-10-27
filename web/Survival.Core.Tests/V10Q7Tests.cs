using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 策划提交 9fdb44f Q7 温和初值（S12 §8、R01 §7、R04 §8、S13 §6）：非战斗日常负小时率×0.8、免疫正恢复×2、
/// 取消已感染免疫惩罚、高免疫门槛 85→80、饱饱 Bmax 0.05/带宽 40。验收 S12-A08、R01-A06、R04-A11–A13、S13-A08。
/// </summary>
public class V10Q7Tests : BattleTestBase
{
    private void Ready(GameState s)
    {
        PlayerFirst(s);
        E.BattleStep(s);
        Assert.That(E.AwaitingPlayer(s), Is.True);
    }

    /// <summary>S12-A08/R01-A06：非战斗按新表只结算一次；战斗轮末仍用 battleTime 原率；腹泻/归零损血不缩放；预览与真实同源。</summary>
    [Test]
    public void S12_A08_R01_A06_NonCombatScaledOnce_CombatAndDamageUnchanged()
    {
        // 非战斗：等待60分钟 N−1.2/W−2.4/S−4/M−0.4；预览结果与实跑一致
        var s = New();
        SetStats(s, 20, 80, 80, 18, 90, 60);
        var preview = E.Preview(s, ActionRequest.Wait(60));
        Start(s, ActionRequest.Wait(60));
        Run(s);
        Assert.That(s.Stats.Satiety, Is.EqualTo(78.8).Within(1e-9));
        Assert.That(s.Stats.Hydration, Is.EqualTo(77.6).Within(1e-9));
        Assert.That(s.Stats.Wakefulness, Is.EqualTo(86).Within(1e-9));
        Assert.That(s.Stats.Morale, Is.EqualTo(59.6).Within(1e-9));
        Assert.That(s.Stats.Energy, Is.EqualTo(18).Within(1e-9));
        Assert.That(preview.StatsAfter.Hydration, Is.EqualTo(s.Stats.Hydration).Within(1e-9), "预览与真实同源");
        Assert.That(preview.StatsAfter.Satiety, Is.EqualTo(s.Stats.Satiety).Within(1e-9));

        // 腹泻额外水耗−3/h 不乘0.8：等待1小时共−5.4
        var d = New();
        SetStats(d, 20, 80, 80, 18, 90, 60);
        d.DiarrheaUntil = d.GameMinute + 720;
        Start(d, ActionRequest.Wait(60));
        Run(d);
        Assert.That(80 - d.Stats.Hydration, Is.EqualTo(2.4 + 3).Within(1e-9));

        // 归零损血不缩放（S12-A02）：N、W 同为0等待1小时 H−2.0
        var z = New();
        SetStats(z, 20, 0, 0, 18, 90, 60);
        Start(z, ActionRequest.Wait(60));
        Run(z);
        Assert.That(z.Stats.Health, Is.EqualTo(18).Within(1e-6));

        // 战斗：轮末 tick 用 battleTime 原率 N−1.875/W−4.5/S−5 每小时（不继承新非战斗率），体力自然率0
        var b = Fight(14, "stag");
        Ready(b);
        SetStats(b, 20, 80, 80, 18, 90, 60);
        var m0 = b.GameMinute;
        Assert.That(Cmd(b, "end_turn").Ok, Is.True);
        while (b.Battle != null && b.GameMinute == m0 && !b.Progress.Dead) E.BattleStep(b);
        Assert.That(b.GameMinute, Is.EqualTo(m0 + 5));
        Assert.That(b.Stats.Satiety, Is.EqualTo(80 - 1.875 * 5 / 60).Within(1e-9), "战斗饱食率保持");
        Assert.That(b.Stats.Hydration, Is.EqualTo(80 - 4.5 * 5 / 60).Within(1e-9), "战斗水分率保持");
        Assert.That(b.Stats.Wakefulness, Is.EqualTo(90 - 5 * 5 / 60.0).Within(1e-9), "战斗清醒率保持");
        Assert.That(b.Stats.Energy, Is.EqualTo(18).Within(1e-9), "战斗自然体力率0");
    }

    /// <summary>R04-A11：已感染/未感染同身体条件，休息/睡眠免疫变化相同（不再有感染−0.5/h）；健康奖励与休息/睡眠正率翻倍，其他惩罚照常。</summary>
    [Test]
    public void R04_A11_InfectionNoLongerPenalizesImmunity_PositiveRatesDoubled()
    {
        foreach (var (sheltered, sleep, expect) in new[] { (true, false, 1 + 2.0), (false, false, 1 + 1.0), (true, true, 1 + 4.0), (false, true, 1 + 2.0) })
        {
            var gains = new List<double>();
            foreach (var infected in new[] { false, true })
            {
                var s = New();
                s.Locations["camp"].Facility.ShelterBuilt = sheltered;
                SetStats(s, 20, 80, 80, 10, 90, 60);
                s.Body.Immunity = 50;
                if (infected) { s.Body.Infected = true; s.Body.VirusLoad = 20; }
                Start(s, sleep ? ActionRequest.Sleep(120) : ActionRequest.Rest(60));
                Steps(s, 12);
                gains.Add(s.Body.Immunity - 50);
            }
            Assert.That(gains[0], Is.EqualTo(expect).Within(1e-9), $"遮蔽{sheltered} 睡眠{sleep}");
            Assert.That(gains[1], Is.EqualTo(gains[0]).Within(1e-9), "已感染不另扣免疫");
        }
        // 腹泻−1/h 等其他惩罚保留
        var dz = New();
        SetStats(dz, 20, 80, 80, 18, 90, 60);
        dz.Body.Immunity = 50;
        dz.Body.Infected = true;
        dz.Body.VirusLoad = 20;
        dz.DiarrheaUntil = dz.GameMinute + 720;
        Start(dz, ActionRequest.Wait(60));
        Run(dz);
        Assert.That(dz.Body.Immunity, Is.EqualTo(50 + 1 - 1).Within(1e-9), "健康+1、腹泻−1，感染0");
    }

    /// <summary>R04-A12：I79.99/80 边界——饱饱完全压制、病毒负增长、高免疫计时三处统一80；跌线清零，连续720分钟且V≤10才治愈。</summary>
    [Test]
    public void R04_A12_Threshold80_UnifiedForSuppressDeclineAndTimer()
    {
        // 未感染：I79.99 饱饱输入仍为正 → 首tick转感染 V1；I80 被完全压制，不转感染
        foreach (var (imm, infects) in new[] { (79.99, true), (80.0, false) })
        {
            var s = New();
            s.Story.BaobaoPresent = true;
            SetStats(s, 20, 80, 80, 18, 90, 60);
            s.Body.Immunity = imm;
            Start(s, ActionRequest.Wait(30));
            E.Advance(s);
            Assert.That(s.Body.Infected, Is.EqualTo(infects), $"I{imm}");
            if (infects) Assert.That(s.Body.VirusLoad, Is.EqualTo(1).Within(1e-12));
        }
        // 已感染：段首段末均≥80 计时+5；I80 时 V 每小时−1.5（B=0）
        var t = New();
        t.Story.BaobaoPresent = true;
        SetStats(t, 20, 80, 80, 18, 90, 60);
        t.Body.Immunity = 80;
        t.Body.Infected = true;
        t.Body.VirusLoad = 20;
        Start(t, ActionRequest.Wait(60));
        Run(t);
        Assert.That(t.Body.HighImmunityMinutes, Is.EqualTo(60), "I80 起算");
        Assert.That(t.Body.VirusLoad, Is.EqualTo(18.5).Within(1e-9), "−1.5/h，饱饱被压制");
        // 跌线：段首<80 当段清零
        t.Body.Immunity = 79.99;
        SetStats(t, 20, 30, 80, 18, 90, 60);   // 不满足健康奖励，免疫不回升
        Start(t, ActionRequest.Wait(30));
        E.Advance(t);
        Assert.That(t.Body.HighImmunityMinutes, Is.EqualTo(0), "跌破80清零");
        // 成功外部暴露同样清零
        var u = New();
        u.Body.Immunity = 90;
        u.Body.Infected = true;
        u.Body.VirusLoad = 20;
        u.Body.HighImmunityMinutes = 600;
        Force(u, Below(0.01));
        Assert.That(E.ExposeToVirus(u, out var chance), Is.True);
        Assert.That(chance, Is.EqualTo(0.05 + 0.55 * 0.1).Within(1e-12));
        Assert.That(u.Body.HighImmunityMinutes, Is.EqualTo(0), "再次暴露清零");
    }

    /// <summary>R04-A13：新局 I70 首 tick 仍转感染 V1；净速率 0.5125/2.05/4.05/−1.5 由 V10WorldTests 覆盖；旧档（Legacy）不启用饱饱、不补历史。</summary>
    [Test]
    public void R04_A13_NewGameFirstTickStillV1_LegacyUnchanged()
    {
        var s = NewStory();
        Assert.That(s.Story.BaobaoPresent, Is.True);
        Assert.That(s.Body.Immunity, Is.EqualTo(70));
        // 新局有开场对白：读完后才能推进（S01 §8）；这里直接清掉待读，只验免疫规则
        s.Story.PendingDialogues.Clear();
        Start(s, ActionRequest.Wait(30));
        E.Advance(s);
        Assert.That(s.Body.Infected, Is.True, "I70 时 B=0.0125>0，首个5分钟转感染");
        Assert.That(s.Body.VirusLoad, Is.EqualTo(1).Within(1e-12), "V 最低置1，不套咬伤V20");

        var legacy = New();
        Assert.That(legacy.Story.BaobaoPresent, Is.False);
        Start(legacy, ActionRequest.Wait(60));
        Run(legacy);
        Assert.That(legacy.Body.Infected, Is.False, "旧档不启用饱饱输入");
    }

    /// <summary>S13-A08：休息/睡眠逐段 E/S/H/M 按原正恢复与新负率，I 按新表；低精神正E减半不变。</summary>
    [Test]
    public void S13_A08_RestSleepRecoveryUnchanged_OnlyImmunityDoubled()
    {
        // S13-A01：H10/N80/W80/E10/S80/M60 遮蔽休息1h → E16/H10.2/S76，N78.8/W77.6；I +1(健康)+2(遮蔽休息)
        var s = New();
        s.Locations["camp"].Facility.ShelterBuilt = true;
        SetStats(s, 10, 80, 80, 10, 80, 60);
        s.Body.Immunity = 50;
        Start(s, ActionRequest.Rest(60));
        Run(s);
        Assert.That(s.Stats.Energy, Is.EqualTo(16).Within(1e-9));
        Assert.That(s.Stats.Health, Is.EqualTo(10.2).Within(1e-9));
        Assert.That(s.Stats.Wakefulness, Is.EqualTo(76).Within(1e-9));
        Assert.That(s.Stats.Satiety, Is.EqualTo(78.8).Within(1e-9));
        Assert.That(s.Stats.Hydration, Is.EqualTo(77.6).Within(1e-9));
        Assert.That(s.Stats.Morale, Is.EqualTo(62).Within(1e-9));
        Assert.That(s.Body.Immunity, Is.EqualTo(53).Within(1e-9));

        // S13-A02：E10/S40/M10 有床垫遮蔽睡1h → E12.5/S52/M11，H不自然恢复；I：遮蔽睡眠+4、精神<20 −1、不满足健康奖励
        var t = New();
        var f = t.Locations["camp"].Facility;
        f.ShelterBuilt = true;
        f.BedBuilt = true;
        SetStats(t, 16, 80, 80, 10, 40, 10);
        t.Body.Immunity = 50;
        Start(t, ActionRequest.Sleep(120));
        Steps(t, 12);
        Assert.That(t.Stats.Energy, Is.EqualTo(12.5).Within(1e-9), "低精神正E恢复×0.5不变");
        Assert.That(t.Stats.Wakefulness, Is.EqualTo(52).Within(1e-9));
        Assert.That(t.Stats.Morale, Is.EqualTo(11).Within(1e-9));
        Assert.That(t.Stats.Health, Is.EqualTo(16).Within(1e-9));
        Assert.That(t.Body.Immunity, Is.EqualTo(50 + 4 - 1).Within(1e-9));
    }
}
