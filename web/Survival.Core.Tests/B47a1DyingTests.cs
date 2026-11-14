using Newtonsoft.Json.Linq;
using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// B47a1 濒死生命态、统一受伤、世界段、战斗与医疗脱离（R09 濒死、意志与致死判定系统；dying-willpower-q616.json；开发记录 B47a1）。
/// 意志经验结账（Q676—Q677）、进入濒死暂停（Q673）与预估截断（Q674—Q675）属 B47a2／B47a3，不在这里。
/// </summary>
public class B47a1DyingTests : TestBase
{
    protected override bool NoEnemyApproach => true;

    /// <summary>找一个种子，使从它开始的随机序列依次满足条件（同 B22hTests.ForceRng）。</summary>
    private static void ForceRng(GameState s, params Func<double, bool>[] next)
    {
        for (uint c = 1; c < 2_000_000; c++)
        {
            var r = new Rng { State = c * 2654435761u + 7 };
            if (next.All(p => p(r.NextDouble()))) { s.Rng.State = c * 2654435761u + 7; return; }
        }
        Assert.Fail("找不到满足条件的随机种子");
    }

    private static string AsSchema51(GameState s)
    {
        var o = JObject.Parse(SaveSerializer.Serialize(s));
        o["SchemaVersion"] = "51";
        foreach (var kv in (JObject)o["Actors"]!)
        {
            var a = (JObject)kv.Value!;
            foreach (var f in new[] { "Dead", "DyingNotified", "WillpowerEnemyAwards", "WillpowerXpDay", "WillpowerXpToday" }) a.Remove(f);
            ((JObject)a["Skills"]!).Remove("willpower");
        }
        return o.ToString();
    }

    // ───────────── M1 生命态、意志熟练度与存档 52 ─────────────

    [Test]
    public void M1_Config_WillpowerSkillAndParams()
    {
        var sk = Cfg.Skills.Single(k => k.Id == GameEngine.Willpower);
        Assert.That((sk.Name, sk.Category, sk.InitialLevel, sk.LevelCap, sk.TrackOnly), Is.EqualTo(("意志", "survival", 1, (int?)10, true)), "R09 §4：第 17 门被动熟练度，成长组“求生与行动”");
        Assert.That(Cfg.Skills.Select(k => k.Id).ToList().IndexOf("willpower"), Is.EqualTo(Cfg.Skills.Select(k => k.Id).ToList().IndexOf("stealth") + 1), "排在潜行之后");
        var d = Cfg.Dying;
        Assert.That((d.ResistanceBasePercent, d.ResistancePerLevelPercent, d.WillpowerXpPerEnemyAction, d.WillpowerMaxAwardsPerActorPerEnemy, d.WillpowerMaxXpPerActorPerDay),
            Is.EqualTo((65, 2, 10, 3, 60)), "Q616 合同 willpower");
        Assert.That(Enumerable.Range(1, 10).Select(E.ResistancePercent), Is.EqualTo(new[] { 65, 67, 69, 71, 73, 75, 77, 79, 81, 83 }));
        Assert.That(E.TotalXpToCap(GameEngine.Willpower), Is.EqualTo(2700));
    }

    [Test]
    public void M1_NewGame_Willpower1_SeventeenSkills_LifeStates()
    {
        var s = New();
        Assert.That((s.Skills[GameEngine.Willpower].Level, s.Skills[GameEngine.Willpower].Xp), Is.EqualTo((1, 0L)));
        Assert.That(E.SkillViews(s), Has.Count.EqualTo(17));
        var p = s.Player;
        Assert.That(E.LifeOf(s, p), Is.EqualTo(LifeState.Normal));
        p.Stats.Health = 0;
        Assert.That((E.LifeOf(s, p), E.IsDying(s, p), E.IsDead(s, p)), Is.EqualTo((LifeState.Dying, true, false)), "H0 未确死即濒死");
        p.Stats.Health = 1e-6;
        Assert.That(E.LifeOf(s, p), Is.EqualTo(LifeState.Normal), "内部>0 即正常，不按显示取整");
        p.Dead = true;
        Assert.That(E.LifeOf(s, p), Is.EqualTo(LifeState.Dead));
    }

    [Test]
    public void M1_Migration51To52_DyingFromH0_DeadFromRoster_WillpowerForLivingOnly()
    {
        var s = New();
        E.DebugQuickParty(s, 3);
        var mates = s.Party.Where(id => id != GameState.PlayerId).ToList();
        var dead = mates[0];
        var alive = mates[1];
        E.DebugActor(s, dead, "kill");
        Assert.That(s.Actors[dead].Dead, Is.True, "CompanionDown 置确死");
        s.Actors[alive].Stats.Health = 0;
        s.Player.Stats.Health = 0;
        s.Actors[alive].Skills[GameEngine.Willpower] = new SkillState { Level = 4, Xp = 30 };
        var rng = s.Rng.State;
        var log = s.Log.Count;
        var json = AsSchema51(s);
        Assert.That(json, Does.Not.Contain("willpower").And.Not.Contain("DyingNotified"));
        // 存活者的意志补上：AsSchema51 删掉了所有人的意志，这里把“已有合法意志不覆盖”单独放回一个
        var o = JObject.Parse(json);
        ((JObject)o["Actors"]![alive]!["Skills"]!)["willpower"] = JObject.FromObject(new { Level = 4, Xp = 30 });
        var t = SaveSerializer.Deserialize(o.ToString(), Cfg);
        Assert.That(t.SchemaVersion, Is.EqualTo("52"));
        Assert.That((t.Actors[dead].Dead, t.Actors[dead].Skills.ContainsKey(GameEngine.Willpower)), Is.EqualTo((true, false)), "旧死者不复活、不补意志");
        Assert.That((E.LifeOf(t, t.Actors[alive]), t.Actors[alive].DyingNotified, t.Actors[alive].Skills[GameEngine.Willpower].Level, t.Actors[alive].Skills[GameEngine.Willpower].Xp),
            Is.EqualTo((LifeState.Dying, true, 4, 30L)), "旧 H0 活人归濒死（提醒锁置已提醒），已有意志不覆盖");
        var p = t.Actors[GameState.PlayerId];
        Assert.That((E.LifeOf(t, p), p.DyingNotified, p.Skills[GameEngine.Willpower].Level, p.Skills[GameEngine.Willpower].Xp), Is.EqualTo((LifeState.Dying, true, 1, 0L)), "缺意志补 1 级 0 经验");
        Assert.That((t.Rng.State, t.Log.Count), Is.EqualTo((rng, log)), "不抽随机、不写日志");
        var once = SaveSerializer.Serialize(t);
        Assert.That(SaveSerializer.Serialize(SaveSerializer.Deserialize(once, Cfg)), Is.EqualTo(once), "幂等");
    }

    [Test]
    public void M1_Migration51To52_OldPlayerDeath_StaysDead()
    {
        var s = New();
        s.Progress.Dead = true;
        s.Progress.Death = new DeathInfo { Cause = "伤势过重", Minute = s.GameMinute, LocationId = s.CurrentLocation };
        s.Player.Stats.Health = 0;
        var t = SaveSerializer.Deserialize(AsSchema51(s), Cfg);
        Assert.That((t.Progress.Dead, t.Player.Dead, E.LifeOf(t, t.Player)), Is.EqualTo((true, true, LifeState.Dead)), "旧终局保持");
        Assert.That(t.Player.Skills.ContainsKey(GameEngine.Willpower), Is.False);
    }

    [Test]
    public void M1_DyingCompanion_StillEntersBattleAndLeadsSquad()
    {
        var s = New();
        E.DebugQuickParty(s, 2);
        var mate = s.Party.Single(id => id != GameState.PlayerId);
        s.Actors[mate].Stats.Health = 0;
        Assert.That(E.EntryAllies(s, 2), Does.Contain(mate), "濒死同行者照常入战");
        Assert.That(E.SquadMemberValid(s, mate), Is.True, "濒死同行者仍可当队长");
        s.Actors[mate].Dead = true;
        Assert.That(E.SquadMemberValid(s, mate), Is.False);
    }
}
