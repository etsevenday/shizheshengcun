using System.Reflection;
using Survival.Core.Config;
using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 批次 B49 旧档（R04 Q652「预估、结果文字与旧档」、Q652-A08、Q739-Q748-C01）：不升存档版本；旧 I／V／感染标志与已提交日志原样；
/// 已锁未执行的攻击读档后按原步骤身份取新类型，随机不重抽；同伴旧 V 不倒扣，下一真段起不自然增长；查看／预估不抽签。
/// </summary>
public class B49SaveTests : BattleTestBase
{
    protected override bool NoEnemyApproach => true;

    private const string Lin = "npc_lin_zhao";

    private void Strike(GameState s, BattleEnemy e, EnemyIntentStepConfig st) =>
        typeof(GameEngine).GetMethod("DoAttackStep", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(E, new object[] { s, s.Battle!, e, st, false, 1.0, false, false });

    /// <summary>存档版本不变（B49 只加配置字段与运行规则，存档里不存类型）。</summary>
    [Test]
    public void C01_SchemaUnchanged()
    {
        Assert.That(GameState.CurrentSchema, Is.EqualTo("58"));   // B56 改期望：存档升 58（57→58 水卡与锅水每份100毫升、份数×5，SaveMigrator.From57To58；S10 文首 B53a Q929）：原 "57"
    }

    /// <summary>
    /// Q652-A08／C01：战中存档（已锁抓击、主角已感染 V30、旧咬伤日志）读回后 I／V／感染与日志原样；接着执行已锁步骤，原档与读回档同一随机下结果逐字一致（不重抽、不补历史），并按新类型（抓伤／刮伤）判。
    /// </summary>
    [Test]
    public void Q652_A08_LockedIntent_AfterLoad_NewProfile_SameRoll()
    {
        var s = Fight(4960, "crawler");
        var e = Enemy(s, 1);
        e.Intent = "attack";
        e.IntentStep = 0;
        s.Body.Immunity = 40;
        s.Body.Infected = true;
        s.Body.VirusLoad = 30;
        s.Battle!.Log.Add("第1回合 · 爬行尸咬击命中：你健康−0.50；你因咬伤再次受到感染暴露，病毒+10（判定概率38%）");
        Force(s, Draw(), Below(0.01), Any(), Below(0.1), AtLeast(0.9));
        var t = SaveSerializer.Deserialize(SaveSerializer.Serialize(s), Cfg);
        Assert.That((t.Body.Immunity, t.Body.Infected, t.Body.VirusLoad, t.Rng.State), Is.EqualTo((40.0, true, 30.0, s.Rng.State)));
        Assert.That(t.Battle!.Log, Is.EqualTo(s.Battle.Log), "旧日志不改写");

        var st = AnySlot(Cfg.Enemies.Single(x => x.Id == "crawler").Intents.Cycle.First(x => x.Kind == "attack"));
        Strike(s, e, st);
        Strike(t, Enemy(t, 1), st);
        Assert.That(t.Battle.Log.Last(), Is.EqualTo(s.Battle.Log.Last()), "同一随机、同一结果");
        Assert.That(t.Battle.Log.Last(), Does.Contain("你因抓伤／刮伤再次受到感染暴露，病毒+5（判定概率19%）"));
        Assert.That((t.Body.VirusLoad, t.Rng.State), Is.EqualTo((s.Body.VirusLoad, s.Rng.State)));
        // 再读一次不重复暴露
        var u = SaveSerializer.Deserialize(SaveSerializer.Serialize(t), Cfg);
        Assert.That((u.Body.VirusLoad, u.Battle!.Log.Count(l => l.Contains("抓伤／刮伤")), u.Rng.State), Is.EqualTo((t.Body.VirusLoad, 1, t.Rng.State)));
    }

    /// <summary>Q652-A08：旧档同伴当前 V 不倒扣、不归零；读回后下一真段起不自然增长（主角照原速率）。</summary>
    [Test]
    public void Q652_A08_CompanionOldV_Kept_NoGrowthAfterLoad()
    {
        var s = New(4961);
        E.DebugQuickParty(s, 2);
        var lin = s.Actors[Lin];
        lin.Body.Immunity = 20;
        lin.Body.Infected = true;
        lin.Body.VirusLoad = 64.3;
        var t = SaveSerializer.Deserialize(SaveSerializer.Serialize(s), Cfg);
        Assert.That((t.Actors[Lin].Body.Infected, t.Actors[Lin].Body.VirusLoad), Is.EqualTo((true, 64.3)));
        Start(t, ActionRequest.Wait(60));
        Run(t);
        Assert.That(t.Actors[Lin].Body.VirusLoad, Is.EqualTo(64.3));
    }

    /// <summary>C01：查看敌人传播段、预估与帮助不抽签、不加 V、不写日志。</summary>
    [Test]
    public void C01_Viewing_NoRoll_NoChange()
    {
        var s = Fight(4962, "z_bloater");
        var r0 = s.Rng.State;
        var logs = s.Battle!.Log.Count;
        for (var i = 0; i < 3; i++)
        {
            E.EnemyExposureInfoFor(s, E.Enemy("z_bloater"), "膨胀感染者", E.InBattleExposureReference(s));
            E.VirusHelpParagraphs();
            E.CurrentBodyRates(s);
        }
        Assert.That((s.Rng.State, s.Body.Infected, s.Battle.Log.Count), Is.EqualTo((r0, false, logs)));
    }
}
