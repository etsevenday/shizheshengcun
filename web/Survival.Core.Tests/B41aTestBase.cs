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
    protected static readonly GameConfig CfgV2 = LoadConfig(true);

    /// <summary>开关明确关着的配置（不受 SURVIVAL_ENEMY_CARDS_V2 影响）。</summary>
    protected static readonly GameConfig CfgOff = LoadConfig(false);

    /// <summary>开关关着的引擎（E 在本基类里是开关开着的）。</summary>
    protected GameEngine Off = null!;

    [SetUp]
    public void V2SetUp()
    {
        Off = new GameEngine(CfgOff);
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

    /// <summary>
    /// 新版（开关开）新局：同上布置。B41a 的用例按 B41a 口径写（没有接近时限与敌方开战），这里冻结敌踪接近（不存档的 SuppressEnemyApproach）；
    /// B41b 的用例用 B41bTestBase 的布置放开。
    /// </summary>
    protected GameState V2Field(uint seed = 7)
    {
        var s = New(seed);
        s.SuppressEnemyApproach = true;
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

    /// <summary>做完一次清理并让开工时锁定的随机遭遇判定值必中（roll=0）或必不中（roll=1）。</summary>
    protected void ClearField(GameState s, double roll = 0)
    {
        var ls = s.Locations[s.CurrentLocation];
        ls.ClearCharges = Math.Max(1, ls.ClearCharges);
        Start(s, ActionRequest.Clear());
        s.Main!.EncounterRoll = roll;
        Assert.That(Run(s), Is.EqualTo(SegmentStatus.Completed));
    }

    /// <summary>把本场敌人逐个判死并结算胜利。</summary>
    protected void WinBattle(GameState s)
    {
        var kill = typeof(GameEngine).GetMethod("KillEnemy", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var check = typeof(GameEngine).GetMethod("CheckVictory", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        foreach (var e in s.Battle!.Enemies.Where(e => e.Alive).ToList()) kill.Invoke(E, new object[] { s, e });
        check.Invoke(E, new object[] { s });
        Assert.That(s.Battle, Is.Null);
    }

    /// <summary>判死本场一名敌人（不结算胜利）。</summary>
    protected void KillInBattle(GameState s, int index)
    {
        var kill = typeof(GameEngine).GetMethod("KillEnemy", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        kill.Invoke(E, new object[] { s, s.Battle!.Enemies.Single(e => e.Index == index) });
    }

    /// <summary>强制撤退成功。</summary>
    protected void RetreatOk(GameState s)
    {
        PlayerFirst(s);
        E.BattleStep(s);
        Force(s, Below(0.0001));
        Assert.That(Cmd(s, "retreat").Ok, Is.True);
        Assert.That(s.Battle, Is.Null);
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
