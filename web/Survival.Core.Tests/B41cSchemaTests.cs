using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// B41c 存档 48、潜行配置与第 16 门潜行（R05 Q342、R01；B41e3 起开关已删）。
/// </summary>
public class B41cSchemaTests : B41cTestBase
{
    [Test]
    public void Schema48_ConfigValuesFromR05()
    {
        Assert.That(GameState.CurrentSchema, Is.EqualTo("51"));
        var st = CfgV2.Encounters.Stealth;
        Assert.That((st.CheckMinutes, st.PracticeMinutes), Is.EqualTo((5, 30)), "潜行接近／躲避判定 5 分钟，练习潜行 30 分钟");
        Assert.That((st.LevelFactor, st.WakefulnessFactor, st.WakefulnessRef, st.MoraleFactor, st.MoraleRef), Is.EqualTo((5.0, 0.25, 90.0, 0.2, 60.0)));
        Assert.That((st.SneakBase, st.SneakMin, st.SneakMax), Is.EqualTo((50.0, 10.0, 90.0)));
        Assert.That((st.EvadeBase, st.EvadeMin, st.EvadeMax), Is.EqualTo((60.0, 15.0, 95.0)));
        Assert.That((st.XpSuccess, st.XpFail, st.PracticeXp), Is.EqualTo((20, 10, 20)));
        Assert.That((st.HideMinMinutes, st.HideMaxMinutes, st.HideStepMinutes), Is.EqualTo((720, 1440, 5)));
        Assert.That((CfgV2.Encounters.FirstStrike.SpeedFactor, CfgV2.Encounters.FirstStrike.DamageFactor), Is.EqualTo((1.5, 1.5)));
        var sk = CfgV2.Skills.Single(k => k.Id == "stealth");
        Assert.That((sk.Name, sk.InitialLevel, sk.LevelCap), Is.EqualTo(("潜行", 1, 10)));
    }

    [Test]
    public void NewGame_SixteenthSkill_StealthLevel1()
    {
        var s = E.NewGame(1);
        Assert.That(Stealth(s), Is.EqualTo((1, 0L)));
        var views = E.SkillViews(s);
        Assert.That(views.Count, Is.EqualTo(16));
        var v = views.Single(x => x.Id == "stealth");
        Assert.That((v.Name, v.Level, v.Cap, v.NextXp), Is.EqualTo(("潜行", 1, 10, 100L)), "R01：升级门槛 50×(L+1)");
    }

    [Test]
    public void Schema47To48_OnlyDefaults_Silent()
    {
        var s = V2Field();
        E.DebugSpawnEnemy(s, "shambler", "field");
        s.Locations["field"].ClearCharges = Math.Max(1, s.Locations["field"].ClearCharges);
        Assert.That(E.Start(s, ActionRequest.Clear()).Ok, Is.True);
        E.Advance(s);
        var rng = s.Rng.State;
        var log = s.Log.Count;
        var t = SaveSerializer.Deserialize(AsSchema47(s), CfgV2);
        Assert.That(t.SchemaVersion, Is.EqualTo("51"));
        Assert.That(t.SquadLeader, Is.Null, "队长为主角");
        Assert.That(t.EnemyCards.Appearances, Is.Empty);
        Assert.That(t.EnemyCards.StealthXpClaims, Is.Empty);
        Assert.That(t.WorldEnemies.All(w => w.EvadeHiddenUntil == null && w.PopAccount == null), Is.True);
        Assert.That(t.Main!.Stealth, Is.Null);
        Assert.That(Stealth(t), Is.EqualTo((1, 0L)), "补第 16 门潜行 1 级");
        Assert.That(t.Log.Count, Is.EqualTo(log), "静默");
        Assert.That(t.Rng.State, Is.EqualTo(rng), "不抽随机");
        var once = SaveSerializer.Serialize(t);
        Assert.That(SaveSerializer.Serialize(SaveSerializer.Deserialize(once, CfgV2)), Is.EqualTo(once), "幂等");
    }

    [Test]
    public void Schema47To48_AddsStealthLevel1_KeepsOtherSkills()
    {
        var s = V2Field();
        s.Player.Skills["athletics"] = new SkillState { Level = 4, Xp = 30 };
        var t = SaveSerializer.Deserialize(AsSchema47(s), CfgV2);
        Assert.That(t.SchemaVersion, Is.EqualTo("51"));
        Assert.That(Stealth(t), Is.EqualTo((1, 0L)), "补潜行 1 级 0 经验");
        Assert.That((t.Player.Skills["athletics"].Level, t.Player.Skills["athletics"].Xp), Is.EqualTo((4, 30L)), "已有技能不动");
        // 开关关着时已是 48 的档（没有潜行、卡模型标记 0）在 B41e3 打开开关后读入也补
        var u = E.NewGame(2);
        u.Player.Skills.Remove("stealth");
        u.EnemyCards.ModelVersion = 0;
        var v = SaveSerializer.Deserialize(SaveSerializer.Serialize(u), CfgV2);
        Assert.That(Stealth(v), Is.EqualTo((1, 0L)));
    }
}
