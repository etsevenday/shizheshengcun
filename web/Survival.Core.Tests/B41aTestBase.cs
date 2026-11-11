using Newtonsoft.Json.Linq;
using Survival.Core.Config;
using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// B41a 新版敌踪用例的公共工具：开关 encounters.enemyCardsV2 打开的配置（发布配置默认关，这里在内存里改成开）、
/// 把当前状态写成“上一版（schema 45）旧档”再读回。
/// </summary>
public abstract class B41aTestBase : BattleTestBase
{
    /// <summary>发布配置把 encounters.enemyCardsV2 改成 true（其余原样）。</summary>
    protected static readonly GameConfig CfgV2 = LoadV2();

    private static GameConfig LoadV2()
    {
        var raw = JObject.Parse(File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "config.v0.6.json")));
        raw["encounters"]!["enemyCardsV2"] = true;
        return ConfigLoader.FromJson(raw.ToString());
    }

    /// <summary>开关关着的引擎（E 在本基类里是开关开着的）。</summary>
    protected GameEngine Off = null!;

    [SetUp]
    public void V2SetUp()
    {
        Off = new GameEngine(Cfg);
        E = new GameEngine(CfgV2);
    }

    /// <summary>旧版（开关关）新局：去掉新剧情，冻结刷新与巡游，第2日白天在农野荒地，清掉原有敌人。</summary>
    protected GameState LegacyField(uint seed = 7)
    {
        var s = Off.NewGame(seed);
        Off.DebugLegacyStory(s);
        PrepareField(Off, s);
        return s;
    }

    /// <summary>新版（开关开）新局：同上布置。</summary>
    protected GameState V2Field(uint seed = 7)
    {
        var s = New(seed);
        PrepareField(E, s);
        Assert.That(E.CardsV2(s), Is.True);
        return s;
    }

    protected static void PrepareField(GameEngine e, GameState s)
    {
        e.DebugTeleport(s, "field");
        s.GameMinute = At(2, 9, 0);
        s.SpawnNextCheck = int.MaxValue;
        s.PatrolNextCheck = int.MaxValue;
        s.WorldEnemies.Clear();
    }

    /// <summary>把状态写成上一版（schema 45）的旧档 JSON：版本号改回 45，去掉 46 才有的字段。</summary>
    protected static string AsSchema45(GameState s)
    {
        var o = JObject.Parse(SaveSerializer.Serialize(s));
        o["SchemaVersion"] = "45";
        o.Remove("EnemyCards");
        foreach (var w in (JArray)o["WorldEnemies"]!) ((JObject)w).Remove("GroupId");
        if (o["Battle"] is JObject b) b.Remove("CardKey");
        return o.ToString();
    }
}
