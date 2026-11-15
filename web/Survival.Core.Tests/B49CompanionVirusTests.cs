using Survival.Core.Config;
using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 批次 B49 同伴病毒（R04 Q652「同伴病毒不自然增殖」、Q748-A02）：r_companion=min(0,r)；主角原分档与饱饱输入不变；
/// 高免疫下降、自然治愈、抗生素／绷带／特效药与新暴露照旧；同行／候补／预估同一口径。
/// </summary>
public class B49CompanionVirusTests : BattleTestBase
{
    protected override bool NoEnemyApproach => true;

    private const string Lin = "npc_lin_zhao";

    private GameState Party(uint seed = 4901)
    {
        var s = New(seed);
        E.DebugQuickParty(s, 2);
        Assert.That(s.Party, Does.Contain(Lin));
        return s;
    }

    private static void Infect(BodyState b, double immunity, double v)
    {
        b.Immunity = immunity;
        b.Infected = true;
        b.VirusLoad = v;
    }

    /// <summary>Q652-A05：同为 I50（原 +2/小时）——主角每小时 +2，同伴自然正增长 0；速率函数按角色区分。</summary>
    [Test]
    public void Q652_A05_SameImmunity_PlayerGrows_CompanionFlat()
    {
        var s = Party();
        Infect(s.Body, 50, 30);
        var lin = s.Actors[Lin];
        Infect(lin.Body, 50, 30);
        Assert.That(E.ActorVirusRate(s.Actors[GameState.PlayerId], 50, false), Is.EqualTo(2));
        Assert.That(E.ActorVirusRate(lin, 50, false), Is.EqualTo(0));
        Assert.That(E.ActorVirusRate(lin, 10, true), Is.EqualTo(0), "抗生素不能把 0 变成负");
        Start(s, ActionRequest.Wait(60));
        Run(s);
        Assert.That(s.Body.VirusLoad, Is.EqualTo(32).Within(0.05), "主角原分档");
        Assert.That(lin.Body.VirusLoad, Is.EqualTo(30), "同伴不自然上涨");
        Assert.That(lin.Body.Infected, Is.True);
    }

    /// <summary>Q652-A05：候补同伴同口径（不在同行也不自然上涨）；同伴没有饱饱输入，未感染者不会因自然进程转感染。</summary>
    [Test]
    public void Q652_A05_Reserve_SameRule_NoBaobao()
    {
        var s = Party();
        var lin = s.Actors[Lin];
        s.Party.Remove(Lin);
        Assert.That(E.Reserves(s).Any(r => r.ActorId == Lin), Is.True, "林昭留作候补");
        Infect(lin.Body, 20, 40);
        s.Story.BaobaoPresent = true;
        Start(s, ActionRequest.Wait(60));
        Run(s);
        Assert.That(lin.Body.VirusLoad, Is.EqualTo(40), "候补同伴自然正增长 0");

        var t = Party(4902);
        t.Story.BaobaoPresent = true;
        var tl = t.Actors[Lin];
        tl.Body.Immunity = 10;
        tl.Body.Infected = false;
        tl.Body.VirusLoad = 0;
        Start(t, ActionRequest.Wait(60));
        Run(t);
        Assert.That(tl.Body.Infected, Is.False, "饱饱只在主角体内");
    }

    /// <summary>Q652-A06：同伴 I85 时沿原 −1.5/小时下降；未治愈时最低 1。</summary>
    [Test]
    public void Q652_A06_HighImmunity_Decrease_Kept()
    {
        var s = Party();
        var lin = s.Actors[Lin];
        Infect(lin.Body, 85, 30);
        Assert.That(E.ActorVirusRate(lin, 85, false), Is.EqualTo(-1.5));
        Start(s, ActionRequest.Wait(60));
        Run(s);
        Assert.That(lin.Body.VirusLoad, Is.EqualTo(28.5).Within(1e-9));
        Infect(lin.Body, 85, 1.2);
        Start(s, ActionRequest.Wait(60));
        Run(s);
        Assert.That(lin.Body.VirusLoad, Is.EqualTo(1).Within(1e-9), "最低 1，仍感染");
        Assert.That(lin.Body.Infected, Is.True);
    }

    /// <summary>Q652-A06：同伴高免疫连续 720 分钟且 V≤10 仍自然治愈。</summary>
    [Test]
    public void Q652_A06_NaturalCure_Kept()
    {
        var s = Party();
        var lin = s.Actors[Lin];
        Infect(lin.Body, 95, 5);
        lin.Body.HighImmunityMinutes = Cfg.Infection.NaturalCure.Minutes - 10;
        Start(s, ActionRequest.Wait(30));
        Run(s);
        Assert.That((lin.Body.Infected, lin.Body.VirusLoad), Is.EqualTo((false, 0.0)));
    }

    /// <summary>Q652-A06／Q748-A02：同伴的绷带、抗生素、特效药沿原效果与冷却；新真实暴露仍按类型加量（可到尸变上限）。</summary>
    [Test]
    public void Q652_A06_Medicine_And_NewExposure_Kept()
    {
        var s = Party();
        var lin = s.Actors[Lin];
        Infect(lin.Body, 50, 30);
        s.FocusOutOfBattle = Lin;
        try
        {
            var parts = new List<string>();
            var antibiotic = Cfg.Items.Single(i => i.Use?.IsAntibiotic == true).Use!;
            Assert.That(E.MedicineBlocked(s, antibiotic), Is.Null);
            typeof(GameEngine).GetMethod("ApplyMedicine", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .Invoke(E, new object?[] { s, antibiotic, parts, null, false, true, null });
            Assert.That(lin.Body.VirusLoad, Is.EqualTo(30 - Cfg.Infection.Antibiotics.InstantLoadReduction).Within(1e-9), "抗生素即时减量照旧");
            Assert.That(lin.Body.AntibioticReadyAt, Is.GreaterThan(s.GameMinute), "给药间隔照旧");

            Force(s, Below(0.001));
            var before = lin.Body.VirusLoad;
            var r = E.ExposeToVirus(s, E.ExposureProfile("bite")!);
            Assert.That(r.Success, Is.True);
            Assert.That(lin.Body.VirusLoad, Is.EqualTo(before + 10).Within(1e-9), "新暴露仍加量");
            Assert.That(E.ExposureResultText(s, r), Does.StartWith("林昭因咬伤再次受到感染暴露，病毒+10"));

            lin.Body.VirusLoad = 95;
            Force(s, Below(0.001));
            E.ExposeToVirus(s, E.ExposureProfile("bite")!);
            Assert.That(lin.Body.VirusLoad, Is.EqualTo(100), "可到尸变上限");
        }
        finally { s.FocusOutOfBattle = null; }
    }

    /// <summary>Q748-A02：同伴视角的当前速率与状态说明用真实净值（I50 为 0、I85 为 −1.5），不列饱饱；主角不变。</summary>
    [Test]
    public void Q748_A02_CompanionRates_Shown_Real()
    {
        var s = Party();
        s.Story.BaobaoPresent = true;
        Infect(s.Body, 50, 30);
        var lin = s.Actors[Lin];
        Infect(lin.Body, 50, 30);
        var (_, pv, _) = E.CurrentBodyRates(s);
        Assert.That(pv, Is.GreaterThan(2), "主角原分档＋饱饱输入");
        Assert.That(E.ActiveConditions(s).Any(c => c.Id == "baobao"), Is.True);
        s.FocusOutOfBattle = Lin;
        try
        {
            Assert.That(E.CurrentBodyRates(s).virus, Is.EqualTo(0));
            Assert.That(E.ActiveConditions(s).Single(c => c.Id == "infected").Effect, Does.Contain("每小时+0").Or.Contain("每小时0"));
            Assert.That(E.ActiveConditions(s).Any(c => c.Id == "baobao"), Is.False, "饱饱只在主角体内");
            lin.Body.Immunity = 85;
            Assert.That(E.CurrentBodyRates(s).virus, Is.EqualTo(-1.5));
        }
        finally { s.FocusOutOfBattle = null; }
    }

    /// <summary>Q748-A02（Q684-A02 跟随）：预估副本里同伴自然 V 不涨——同伴 V99.9、I0 也不报尸变风险；主角同条件照报。</summary>
    [Test]
    public void Q748_A02_Forecast_NoCompanionNaturalRise()
    {
        var s = Party();
        var lin = s.Actors[Lin];
        Infect(lin.Body, 0, 99.9);
        var p = E.Preview(s, ActionRequest.Wait(60));
        Assert.That(p.Ok, Is.True, p.Reason);
        Assert.That(p.VirusRisks, Is.Empty, "同伴不自然上涨，不造尸变倒计时");
        Assert.That(lin.Body.VirusLoad, Is.EqualTo(99.9), "预估不改真值");

        Infect(s.Body, 0, 99.9);
        Assert.That(E.Preview(s, ActionRequest.Wait(60)).VirusRisks, Has.Some.Contains("你的病毒量预计"));
    }
}
