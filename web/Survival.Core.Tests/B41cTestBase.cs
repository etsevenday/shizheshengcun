using Newtonsoft.Json.Linq;
using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// B41c 新版敌踪潜行、小队队长、隐藏重现与先手的公共工具（开关开的配置，见 <see cref="B41aTestBase"/>）。
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
}
