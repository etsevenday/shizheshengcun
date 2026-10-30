using Newtonsoft.Json.Linq;
using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 B3（策划提交 f172677）R10 多角色模型：主角 actorId=p0，个人状态按角色存（R08 §2/§5、PT01–PT02、COMP04）。
/// </summary>
public class B3ActorTests : TestBase
{
    /// <summary>把当前 schema 9 存档改写成 schema 8 的样子：主角个人字段搬回顶层、删掉 Actors/Party（模拟旧档）。</summary>
    internal static JObject ToSchema8(GameState s)
    {
        var j = JObject.Parse(SaveSerializer.Serialize(s));
        var p0 = (JObject)j["Actors"]![GameState.PlayerId]!;
        foreach (var f in new[] { "Stats", "Body", "Skills", "DiarrheaUntil", "BandageReadyAt", "AlertLatched", "ZeroLatched" })
            j[f] = p0[f]!.DeepClone();
        j.Remove("Actors");
        j.Remove("Party");
        j["SchemaVersion"] = "8";
        return j;
    }

    [Test]
    public void NewGame_HasPlayerActorP0_TopLevelForwards()
    {
        var s = New();
        Assert.That(s.Party, Is.EqualTo(new[] { GameState.PlayerId }));
        Assert.That(s.Actors.Keys, Is.EquivalentTo(new[] { "p0" }));
        Assert.That(s.Player.Id, Is.EqualTo("p0"));
        Assert.That(ReferenceEquals(s.Stats, s.Actors["p0"].Stats), Is.True, "顶层 Stats 转发到 p0");
        Assert.That(ReferenceEquals(s.Body, s.Actors["p0"].Body), Is.True);
        Assert.That(s.Skills.Count, Is.GreaterThan(0));
        Assert.That(s.AlertLatched.Count, Is.EqualTo(6));
    }

    [Test]
    public void Save_StoresActorsNotTopLevel_RoundTrip()
    {
        var s = New();
        s.Stats.Satiety = 33.5;
        s.Body.Immunity = 61;
        s.DiarrheaUntil = 999;
        var json = SaveSerializer.Serialize(s);
        var j = JObject.Parse(json);
        Assert.That(j["Stats"], Is.Null, "个人状态不再写在顶层");
        Assert.That(j["Actors"]!["p0"]!["Stats"]!["Satiety"]!.Value<double>(), Is.EqualTo(33.5));
        var back = SaveSerializer.Deserialize(json, Cfg);
        Assert.That(back.Stats.Satiety, Is.EqualTo(33.5));
        Assert.That(back.Body.Immunity, Is.EqualTo(61));
        Assert.That(back.DiarrheaUntil, Is.EqualTo(999));
        Assert.That(back.Party, Is.EqualTo(new[] { "p0" }), "Party 不因默认值叠加");
        Assert.That(SaveSerializer.Serialize(back), Is.EqualTo(json));
    }

    [Test]
    public void Clone_DeepCopiesActors()
    {
        var s = New();
        var c = s.Clone();
        c.Stats.Health = 1;
        c.Party.Add("p1");
        Assert.That(s.Stats.Health, Is.Not.EqualTo(1));
        Assert.That(s.Party.Count, Is.EqualTo(1));
    }

    /// <summary>测试注入一名同行队友 p1（身体与主角相同的起点），本批游戏里还没有招募入口。</summary>
    internal static ActorState InjectCompanion(GameState s, string id = "p1")
    {
        var p1 = s.Player.Clone();
        p1.Id = id;
        s.Actors[id] = p1;
        s.Party.Add(id);
        return p1;
    }

    [Test]
    public void PT02_COMP04_TwoActors_OneClock_EachBodyOnce()
    {
        var solo = New(77);
        var duo = New(77);
        foreach (var x in new[] { solo, duo })
        {
            var fac = x.Locations[x.CurrentLocation].Facility;
            fac.FireLit = true;
            fac.FuelMinutes = 120;
        }
        var p1 = InjectCompanion(duo);
        Start(solo, ActionRequest.Wait(60));
        Start(duo, ActionRequest.Wait(60));
        Run(solo);
        Run(duo);
        Assert.That(duo.GameMinute, Is.EqualTo(solo.GameMinute), "世界时间只推进一次，不按人数重复");
        Assert.That(duo.SegmentCount, Is.EqualTo(solo.SegmentCount));
        Assert.That(duo.Locations[duo.CurrentLocation].Facility.FuelMinutes, Is.EqualTo(solo.Locations[solo.CurrentLocation].Facility.FuelMinutes), "燃料只烧一次");
        Assert.That(duo.Player.Stats.Satiety, Is.EqualTo(solo.Player.Stats.Satiety).Within(1e-9), "主角身体与单人时逐段一致");
        Assert.That(duo.Player.Body.Immunity, Is.EqualTo(solo.Player.Body.Immunity).Within(1e-9));
        // 队友同起点、同为 wait：各自结算一次，结果与主角相同（不是两倍）
        Assert.That(p1.Stats.Satiety, Is.EqualTo(duo.Player.Stats.Satiety).Within(1e-9));
        Assert.That(p1.Stats.Hydration, Is.EqualTo(duo.Player.Stats.Hydration).Within(1e-9));
        Assert.That(p1.Stats.Satiety, Is.LessThan(New(77).Stats.Satiety), "队友身体确实被结算");
        // 保质按绝对时间：同一物品到期判定不因人数变化
        Assert.That(duo.Items.Count(i => E.IsSpoiled(duo, i)), Is.EqualTo(solo.Items.Count(i => E.IsSpoiled(solo, i))));
    }

    [Test]
    public void PT02_CompanionUsesWaitWhilePlayerWorks_TravelAll()
    {
        var a = new ActionState { Type = ActionType.Craft };
        Assert.That(GameEngine.CompanionAction(a).Type, Is.EqualTo(ActionType.Wait), "S01 一名执行者，其他同行 wait");
        Assert.That(GameEngine.CompanionAction(new ActionState { Type = ActionType.Travel }).Type, Is.EqualTo(ActionType.Travel), "移动全员 travel");
        Assert.That(GameEngine.CompanionAction(new ActionState { Type = ActionType.Sleep }).Type, Is.EqualTo(ActionType.Sleep));
        Assert.That(a.Type, Is.EqualTo(ActionType.Craft), "不改原行动");
    }

    [Test]
    public void PT02_CompanionOwnLatchesAndBodies()
    {
        var s = New();
        var p1 = InjectCompanion(s);
        p1.Stats.Satiety = 0.01;
        p1.Body.Infected = true;
        p1.Body.VirusLoad = 30;
        Start(s, ActionRequest.Wait(30));
        Run(s);
        Assert.That(p1.ZeroLatched[StatId.Satiety], Is.True, "队友归零提醒按本人锁存");
        Assert.That(s.ZeroLatched[StatId.Satiety], Is.False, "主角不受队友影响");
        Assert.That(s.Log.Any(l => l.Text.Contains("队友p1：饱食归零")), Is.True);
        Assert.That(p1.Body.VirusLoad, Is.Not.EqualTo(30), "队友病毒按本人免疫推进");
        Assert.That(s.Body.Infected, Is.False);
    }

    [Test]
    public void COMP04_BattleRoundTicksEachActorOnce()
    {
        var s = New();
        s.SpawnNextCheck = int.MaxValue;
        s.PatrolNextCheck = int.MaxValue;
        var p1 = InjectCompanion(s);
        var minute = s.GameMinute;
        var ctxSat = Cfg.BattleTime.HourlyRates.Satiety * 5 / 60.0;
        var sat0 = p1.Stats.Satiety;
        E.DebugTeleport(s, "field");
        s.WorldEnemies.Clear();
        var w = E.DebugSpawnEnemy(s, "shambler", "field");
        s.GameMinute = At(2, 9, 0);
        minute = s.GameMinute;
        Assert.That(E.Approach(s, w.Id).Ok, Is.True);
        for (var i = 0; i < 50 && s.Battle != null && s.Battle.Ticks == 0; i++)
        {
            if (E.AwaitingPlayer(s)) E.ExecuteCommand(s, BattleCommand.Of("end_turn"));
            else E.BattleStep(s);
        }
        Assert.That(s.GameMinute, Is.EqualTo(minute + 5), "一轮只推进一次 5 分钟");
        Assert.That(p1.Stats.Satiety, Is.EqualTo(sat0 + ctxSat).Within(1e-9), "队友按战斗段速率结算一次");
    }

    [Test]
    public void Migration8To9_MovesTopLevelIntoP0_Once()
    {
        var s = New();
        s.Stats.Hydration = 41.25;
        s.Body.VirusLoad = 0;
        s.Skills["blade"].Xp = 37;
        s.BandageReadyAt = 1234;
        var j = ToSchema8(s);
        Assert.That(SaveSerializer.NeedsMigration(j.ToString()), Is.True);
        var m = SaveSerializer.Deserialize(j.ToString(), Cfg, out _);
        Assert.That(m.SchemaVersion, Is.EqualTo(GameState.CurrentSchema));
        Assert.That(m.Actors.Keys, Is.EquivalentTo(new[] { "p0" }));
        Assert.That(m.Player.Stats.Hydration, Is.EqualTo(41.25));
        Assert.That(m.Player.Skills["blade"].Xp, Is.EqualTo(37));
        Assert.That(m.Player.BandageReadyAt, Is.EqualTo(1234));
        Assert.That(m.Party, Is.EqualTo(new[] { "p0" }));
        // 再存再读：不再迁移、数值不变（幂等）
        var again = SaveSerializer.Serialize(m);
        Assert.That(SaveSerializer.NeedsMigration(again), Is.False);
        var m2 = SaveSerializer.Deserialize(again, Cfg);
        Assert.That(m2.Player.Stats.Hydration, Is.EqualTo(41.25));
        Assert.That(SaveSerializer.Serialize(m2), Is.EqualTo(again));
    }
}
