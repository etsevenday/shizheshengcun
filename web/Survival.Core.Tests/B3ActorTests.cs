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
