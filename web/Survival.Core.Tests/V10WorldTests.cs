using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 系统策划总册 v1.0：P06/T07 局内时间分层开放刷新（R05 §6.1）；B03/T09 饱饱持续病毒输入（R04 §7.2、N01 §5）。
/// </summary>
public class V10WorldTests : BattleTestBase
{
    // 稀有层抽签值落在 [0.96, 0.995)，强敌层在 [0.995, 1)
    private static Func<double, bool> Rare() => x => x >= 0.961 && x < 0.994;
    private static Func<double, bool> Elite() => x => x >= 0.996;

    private GameState Quiet(int minute)
    {
        var s = New(77);
        s.WorldEnemies.Clear();
        s.GameMinute = minute;
        return s;
    }

    [Test]
    public void Spawn_RareLockedUntilDay3_EliteUntilDay7_NoReroll()
    {
        var s = Quiet(At(2, 10, 0));
        Force(s, Below(0.2), Rare());
        var before = s.Rng.State;
        Assert.That(E.DebugSpawnCheck(s), Is.Null, "第2日抽中稀有层：本次无刷新，不重抽");
        Assert.That(s.WorldEnemies, Is.Empty);

        var t = Quiet(At(3, 0, 0));
        Assert.That(t.GameMinute, Is.EqualTo(2880));
        Force(t, Below(0.2), Rare(), Any(), Any());
        var w = E.DebugSpawnCheck(t);
        Assert.That(w, Is.Not.Null, "第3日00:00起稀有层开放");
        Assert.That(E.Enemy(w!.EnemyId).Rarity, Is.EqualTo("rare"));

        Force(t, Below(0.2), Elite(), Any(), Any());
        Assert.That(E.DebugSpawnCheck(t), Is.Null, "第3日强敌层仍未开放");

        var u = Quiet(At(7, 0, 0));
        Assert.That(u.GameMinute, Is.EqualTo(8640));
        Force(u, Below(0.2), Elite(), Any(), Any());
        var b = E.DebugSpawnCheck(u);
        Assert.That(b, Is.Not.Null, "第7日00:00起强敌层开放");
        Assert.That(E.Enemy(b!.EnemyId).Rarity, Is.EqualTo("elite"));

        // 普通层第1日即开放（新局第2日00:00起才有刷新检查，规则不变）
        var c = Quiet(At(2, 0, 0));
        Force(c, Below(0.2), Below(0.5), Any(), Any());
        Assert.That(E.DebugSpawnCheck(c), Is.Not.Null);
    }

    [Test]
    public void Spawn_OpeningDoesNotBuffOrCatchUp()
    {
        // 已存在的高层敌人（如旧档）保留，不删除、不改种、不回满；开放不涨属性、不在00:00补刷
        var s = New(78);
        s.SpawnNextCheck = int.MaxValue;
        s.PatrolNextCheck = int.MaxValue;
        var riot = E.DebugSpawnEnemy(s, "riot", "village");
        riot.Hp = 1.5;
        var count = s.WorldEnemies.Count;
        s.GameMinute = At(2, 23, 30);
        AdvanceTo(s, At(3, 0, 30));
        Assert.That(s.WorldEnemies.Count, Is.EqualTo(count), "开放时不额外造一批");
        Assert.That(E.FindWorldEnemy(s, riot.Id)!.Hp, Is.EqualTo(1.5), "旧敌不回满、不涨属性");
        Assert.That(E.Enemy("shambler").Hp, Is.EqualTo(3.6));
    }

    [Test]
    public void Baobao_NewGame_FirstTickInfectsAtLoad1()
    {
        var s = NewStory(5);
        Assert.That(s.Story.BaobaoPresent, Is.True);
        Assert.That(s.Body.Infected, Is.False, "新局 I70/V0 未感染开始");
        Assert.That(s.Body.VirusLoad, Is.EqualTo(0));
        // 一个世界 tick：B(70)=0.0333/h>0 → 进入感染，V=max(1, 实际增加) = 1，不套咬伤 V20
        E.DebugLegacyStory(s);
        s.Story.BaobaoPresent = true;
        Start(s, ActionRequest.Wait(30));
        E.Advance(s);
        Assert.That(s.Body.Infected, Is.True);
        Assert.That(s.Body.VirusLoad, Is.EqualTo(1).Within(1e-12));
        Run(s);
        // 之后已感染：净速率 = R(70)=0.5 + B(I)
        var expect = 1 + 5 * (0.5 + Cfg.Infection.BaobaoInput.Rate(s.Body.Immunity)) / 12;
        Assert.That(s.Body.VirusLoad, Is.EqualTo(expect).Within(2e-3));
    }

    [Test]
    public void Baobao_NetRates_MatchR04_7_2()
    {
        var s = New();
        s.Story.BaobaoPresent = true;
        s.Body.Infected = true;
        s.Body.VirusLoad = 10;
        double Rate(double immunity, bool antibiotic = false)
        {
            s.Body.Immunity = immunity;
            s.Body.AntibioticUntil = antibiotic ? s.GameMinute + 60 : null;
            return E.CurrentBodyRates(s).virus;
        }
        // R04 §7.2/§8、R04-A13（Q7）：B(I)=0.05×clamp((80−I)/40,0,1)
        Assert.That(Rate(70), Is.EqualTo(0.5125).Within(1e-9), "I70 已感染净+0.5125/h");
        Assert.That(Rate(40), Is.EqualTo(2.05).Within(1e-9), "I40 净+2.05/h");
        Assert.That(Rate(0), Is.EqualTo(4.05).Within(1e-9), "I0 净+4.05/h");
        Assert.That(Rate(80), Is.EqualTo(-1.5).Within(1e-9), "I80 以上 B0、净−1.5/h");
        Assert.That(Rate(79.99), Is.EqualTo(0.5 + 0.05 * 0.01 / 40).Within(1e-9), "I79.99 仍在 [70,80) 档 +0.5，B 尚未完全压制");
        Assert.That(Rate(40, antibiotic: true), Is.EqualTo(2 * 0.5 + 0.05).Within(1e-9), "抗生素只减缓 R，不关闭 B");
        // 旧档：不启用
        s.Story.BaobaoPresent = false;
        Assert.That(Rate(40), Is.EqualTo(2).Within(1e-9));
    }

    [Test]
    public void Baobao_LegacyOff_NoInputEver()
    {
        var s = New();   // 同旧档：BaobaoPresent=false
        AdvanceTo(s, At(2, 8, 0));
        Assert.That(s.Body.Infected, Is.False);
        Assert.That(s.Body.VirusLoad, Is.EqualTo(0));
        Assert.That(E.ActiveConditions(s).Any(c => c.Id == "baobao"), Is.False);
    }

    [Test]
    public void Baobao_CureClearsCurrentOnly_ThenReaccumulates()
    {
        var s = New();
        s.Story.BaobaoPresent = true;
        s.Body.Infected = true;
        s.Body.VirusLoad = 5;
        s.Body.Immunity = 95;
        SetStats(s, 20, 80, 80, 18, 90, 60);
        // 高免疫：B=0，连续 720 分钟且 V≤10 自然治愈；之后 I 仍≥80 不会无条件再种入
        AdvanceTo(s, s.GameMinute + 750);
        Assert.That(s.Body.Infected, Is.False, "高免疫自然治愈");
        Assert.That(s.Story.BaobaoPresent, Is.True, "治愈不移除饱饱");
        if (s.Body.Immunity >= 80)
        {
            Start(s, ActionRequest.Wait(30));
            Run(s);
            Assert.That(s.Body.Infected, Is.False, "I≥80 时饱饱输入被压制");
        }
        // 免疫跌下来：重新积累，再次进入感染（最低1）
        s.Body.Immunity = 60;
        Start(s, ActionRequest.Wait(30));
        E.Advance(s);
        Assert.That(s.Body.Infected, Is.True);
        Assert.That(s.Body.VirusLoad, Is.EqualTo(1).Within(1e-12));
        Run(s);

        // 特效药清除当前感染，饱饱仍在；之后 I<80 仍会再积累
        E.DebugGive(s, "antiviral_cure", 1);
        Start(s, ActionRequest.UseItem("antiviral_cure"));
        Assert.That(s.Body.Infected, Is.False);
        Assert.That(s.Story.BaobaoPresent, Is.True);
        Run(s);
        Assert.That(s.Body.Infected, Is.True, "5分钟用药 tick 内 B>0 → 重新积累");
        Assert.That(E.ActiveConditions(s).Single(c => c.Id == "baobao").Effect, Does.Contain("饱饱仍在"));
    }

    [Test]
    public void Baobao_HighImmunityTimerNotResetBySource()
    {
        var s = New();
        s.Story.BaobaoPresent = true;
        s.Body.Infected = true;
        s.Body.VirusLoad = 8;
        s.Body.Immunity = 90;
        SetStats(s, 20, 80, 80, 18, 90, 60);
        Start(s, ActionRequest.Wait(60));
        Run(s);
        Start(s, ActionRequest.Wait(60));
        Run(s);
        Assert.That(s.Body.HighImmunityMinutes, Is.EqualTo(120), "持续源不重置高免疫计时");
        Assert.That(s.Body.VirusLoad, Is.EqualTo(8 - 1.5 * 2).Within(1e-9));
    }
}
