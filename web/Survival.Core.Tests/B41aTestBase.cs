using Newtonsoft.Json.Linq;
using Survival.Core.Config;
using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// B41a 新版敌踪用例的公共工具：野外布置、把当前状态写成“上一版（schema 45）旧档”再读回、在状态上摆出旧版才有的
/// 待处理敌踪与在办绕行（B41e3 起旧流程已删，只能直接改状态模拟旧档）。
/// </summary>
public abstract class B41aTestBase : BattleTestBase
{
    /// <summary>发布配置（B41e3 起新版敌踪是唯一流程）。</summary>
    protected static GameConfig CfgV2 => Cfg;

    /// <summary>旧档式新局：同 <see cref="V2Field"/> 的布置，但敌踪卡模型标记退回 0（等同新版上线前的旧档，读档时才采纳）。</summary>
    protected GameState LegacyField(uint seed = 7)
    {
        var s = New(seed);
        s.SuppressEnemyApproach = true;   // 旧版没有敌踪接近；不存档，读档后的新版状态照常接近
        PrepareField(E, s);
        s.EnemyCards.ModelVersion = 0;
        return s;
    }

    /// <summary>旧版“发现敌踪、待选接敌／绕行”的待处理名单（只存在于旧档）：按给定顺序摆在当前地点。</summary>
    protected static void LegacyEncounter(GameState s, IEnumerable<WorldEnemy> group, string source = "clear")
    {
        s.Encounter = new PendingEncounter { LocationId = s.CurrentLocation, EnemyIds = group.Select(w => w.Id).ToList(), Source = source, Minute = s.GameMinute };
    }

    /// <summary>
    /// 旧版在办绕行（只存在于旧档）：用等待开工、推进一段，再把行动改成旧绕行 10 分钟的样子（类型、名称、目标、计划时长），
    /// 已耗时间与身体变化按真实推进保留。
    /// </summary>
    protected void LegacyBypass(GameState s, bool advance = true)
    {
        Start(s, ActionRequest.Wait(30));
        if (advance) E.Advance(s);
        s.Main!.Type = ActionType.Bypass;
        s.Main.PlannedMinutes = 10;
        s.Main.Label = "绕行敌踪";
        s.Main.TargetId = s.CurrentLocation;
    }

    /// <summary>
    /// 新局：去掉新剧情，冻结刷新与巡游，第2日白天在农野荒地，清掉原有敌人。B41a 的用例按 B41a 口径写（没有接近时限与敌方开战），这里冻结敌踪接近（不存档的 SuppressEnemyApproach）；
    /// B41b 的用例用 B41bTestBase 的布置放开。
    /// </summary>
    protected GameState V2Field(uint seed = 7)
    {
        var s = New(seed);
        s.SuppressEnemyApproach = true;
        PrepareField(E, s);
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
