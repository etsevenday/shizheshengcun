using Newtonsoft.Json.Linq;
using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// B41c 新版敌踪潜行、小队队长、隐藏重现与先手的公共工具（B41e3 起新版敌踪是唯一流程，见 <see cref="B41aTestBase"/>）。
/// </summary>
public abstract class B41cTestBase : B41bTestBase
{
    /// <summary>把状态写成上一版（schema 47）的旧档 JSON：版本号改回 47，去掉 48 才有的字段。</summary>
    protected static string AsSchema47(GameState s)
    {
        var o = JObject.Parse(SaveSerializer.Serialize(s));
        o["SchemaVersion"] = "47";
        o.Remove("SquadLeader");
        if (o["EnemyCards"] is JObject ec) { ec.Remove("Appearances"); ec.Remove("StealthXpClaims"); }
        foreach (var w in (JArray)o["WorldEnemies"]!) { ((JObject)w).Remove("EvadeHiddenUntil"); ((JObject)w).Remove("PopAccount"); }
        foreach (var key in new[] { "Main", "Sub", "StashedWork" })
            if (o[key] is JObject a) a.Remove("Stealth");
        if (o["Battle"] is JObject b) { b.Remove("FirstStrike"); b.Remove("FirstStrikeDamage"); }
        foreach (var p in ((JObject)o["Actors"]!).Properties())
            if (p.Value["Skills"] is JObject sk) sk.Remove("stealth");
        return o.ToString();
    }

    /// <summary>某人潜行等级与级内经验。</summary>
    protected (int Level, long Xp) Stealth(GameState s, string actorId = GameState.PlayerId)
    {
        var sk = s.Actors[actorId].Skills["stealth"];
        return (sk.Level, sk.Xp);
    }

    /// <summary>
    /// 推进一段，让段内的随机数恰好给出想要的结果（在副本上逐个试随机状态，找到后用同一状态推进原局）。
    /// </summary>
    protected SegmentStatus AdvanceUntil(GameState s, Func<GameState, bool> want)
    {
        for (uint c = 1; c < 200_000; c++)
        {
            var state = c * 2654435761u + 7;
            var t = SaveSerializer.Deserialize(SaveSerializer.Serialize(s), CfgV2);
            t.SuppressEnemyApproach = s.SuppressEnemyApproach;
            t.Rng.State = state;
            E.Advance(t);
            if (!want(t)) continue;
            s.Rng.State = state;
            var st = E.Advance(s).Status;
            Assert.That(want(s), Is.True, "同一随机状态应得到同一结果");
            return st;
        }
        Assert.Fail("找不到满足条件的随机序列");
        return SegmentStatus.Idle;
    }

    /// <summary>最后一条日志文字。</summary>
    protected static string LastLog(GameState s) => s.Log.Last().Text;

    /// <summary>某敌人的单卡键（普通单卡）。</summary>
    protected static string Key(WorldEnemy w) => GameEngine.SingleCardKey(w.Id);

    /// <summary>新局，农野荒地放下一只游荡尸（容量 1），返回卡键。</summary>
    protected (GameState s, string key) OneShambler(int capacity = 1)
    {
        var s = Field(capacity, "shambler");
        return (s, Key(s.WorldEnemies.Single()));
    }

    protected static void SetStealth(GameState s, string actorId, int level, long xp = 0) =>
        s.Actors[actorId].Skills["stealth"] = new SkillState { Level = level, Xp = xp };
}
