using Newtonsoft.Json.Linq;
using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// B41b 里程碑 1：敌踪接近时限、撤退保护、战后撤退离地保留与暂存工作的存档结构（存档 47；46→47 只补默认值；R05 Q416、Q425、Q472）。
/// </summary>
public class B41bMigrationTests : B41aTestBase
{
    /// <summary>把状态写成上一版（schema 46）旧档 JSON：版本号改回 46，去掉 47 才有的字段。</summary>
    protected static string AsSchema46(GameState s)
    {
        var o = JObject.Parse(SaveSerializer.Serialize(s));
        o["SchemaVersion"] = "46";
        ((JObject)o["EnemyCards"]!).Remove("Timers");
        o.Remove("StashedWork");
        foreach (var key in new[] { "Main", "Sub" })
            if (o[key] is JObject a)
                foreach (var f in new[] { "PauseNote", "StartZone", "StartGeneration", "RetreatKept", "RetreatMoveOpen", "ReserveSources" }) a.Remove(f);
        return o.ToString();
    }

    private static uint RngOf(GameState s) => s.Rng.State;

    [Test]
    public void Schema47_ConfigValuesFromR05()
    {
        Assert.That(GameState.CurrentSchema, Is.EqualTo("47"));
        Assert.That(Cfg.Encounters.ApproachMinutes, Is.EqualTo(new[] { 20, 25, 30, 35, 40 }), "R05 Q416 五档");
        Assert.That(Cfg.Encounters.CardRetreatGuardMinutes, Is.EqualTo(60), "R05 Q416 同卡撤退保护 60 世界分钟");
        Assert.That(Cfg.Encounters.EnemyCardsV2, Is.False, "发布配置仍关着（B41e 翻开）");
    }

    [Test]
    public void SwitchOff_46To47_OnlyDefaults_Silent()
    {
        var s = LegacyField();
        Off.DebugSpawnEnemy(s, "shambler", "field");
        Assert.That(Off.Start(s, ActionRequest.Clear()).Ok, Is.True);
        Off.Advance(s);
        var rng = RngOf(s);
        var log = s.Log.Count;
        var t = SaveSerializer.Deserialize(AsSchema46(s), CfgOff);
        Assert.That(t.SchemaVersion, Is.EqualTo("47"));
        Assert.That(t.EnemyCards.Timers, Is.Empty);
        Assert.That(t.StashedWork, Is.Null, "旧档无暂存记录即按无暂存处理");
        Assert.That(t.Main!.Type, Is.EqualTo(ActionType.Clear));
        Assert.That(t.Main.ElapsedMinutes, Is.EqualTo(5));
        Assert.That(t.Main.PauseNote, Is.Null);
        Assert.That(t.Main.RetreatKept, Is.False);
        Assert.That(t.Main.ReserveSources, Is.Null);
        Assert.That(t.Log.Count, Is.EqualTo(log), "静默");
        Assert.That(RngOf(t), Is.EqualTo(rng), "不抽随机");
        var once = SaveSerializer.Serialize(t);
        Assert.That(SaveSerializer.Serialize(SaveSerializer.Deserialize(once, CfgOff)), Is.EqualTo(once), "幂等");
    }

    [Test]
    public void NewFields_RoundTrip()
    {
        var s = V2Field();
        s.EnemyCards.Timers["we:3"] = new EnemyCardTimer { Left = 15, GuardUntil = 1234 };
        s.EnemyCards.Timers["group:1"] = new EnemyCardTimer { Left = 0, DueMinute = 999 };
        s.Main = new ActionState
        {
            ActionId = 7, Type = ActionType.Craft, Status = ActionStatus.Paused, PauseReason = PauseReason.EnemyApproach,
            PauseNote = "游荡尸主动接近", StartZone = GameState.ExteriorZone, StartGeneration = 2, RetreatKept = true, RetreatMoveOpen = true,
            ReserveSources = new Dictionary<int, string> { [11] = "p0", [12] = "field" },
        };
        s.StashedWork = s.Main.Clone();
        var t = SaveSerializer.Deserialize(SaveSerializer.Serialize(s), CfgV2);
        Assert.That(t.EnemyCards.Timers["we:3"].Left, Is.EqualTo(15));
        Assert.That(t.EnemyCards.Timers["we:3"].GuardUntil, Is.EqualTo(1234));
        Assert.That(t.EnemyCards.Timers["group:1"].DueMinute, Is.EqualTo(999));
        Assert.That(t.Main!.PauseReason, Is.EqualTo(PauseReason.EnemyApproach));
        Assert.That(t.Main.PauseNote, Is.EqualTo("游荡尸主动接近"));
        Assert.That(t.Main.StartGeneration, Is.EqualTo(2));
        Assert.That(t.Main.RetreatKept && t.Main.RetreatMoveOpen, Is.True);
        Assert.That(t.Main.ReserveSources, Is.EqualTo(new Dictionary<int, string> { [11] = "p0", [12] = "field" }));
        Assert.That(t.StashedWork!.ActionId, Is.EqualTo(7));
        var c = s.Clone();
        c.EnemyCards.Timers["we:3"].Left = 5;
        c.StashedWork!.ReserveSources![11] = "x";
        Assert.That(s.EnemyCards.Timers["we:3"].Left, Is.EqualTo(15), "Clone 深拷贝");
        Assert.That(s.StashedWork.ReserveSources![11], Is.EqualTo("p0"));
    }
}
