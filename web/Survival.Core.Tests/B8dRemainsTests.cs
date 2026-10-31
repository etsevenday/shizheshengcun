using Newtonsoft.Json.Linq;
using Survival.Core.Config;
using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 B8d（策划提交 572f24a 答复 Q98）R112：旧存档来源不明的遗物不迁 h02、留在原地；只有明确的公共候补遗物随营区迁址；
/// 来源初始化与迁址都幂等，不复制物品、不改 owner/耐久/保质。出处 R08/A11/S01「Q98」本轮章、assets/20261031/party/party-adjudications.json；
/// 验收 A10「Q95—Q98 兼容性验收」SV-Q98-01—02。v3 地图节点只在测试里临时加进配置（游戏数据里没有，B11 才导入）。
/// </summary>
public class B8dRemainsTests : TestBase
{
    /// <summary>测试用配置：在现行地图上临时加 h02_square / h06_shelter 两个节点（复制农野荒地的数据，只改 ID 与名字）。</summary>
    private static GameConfig V3Cfg()
    {
        var j = JObject.Parse(File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "config.v0.6.json")));
        var locs = (JArray)j["locations"]!;
        var field = (JObject)locs.First(l => (string)l["id"]! == "field");
        foreach (var (id, name) in new[] { ("h02_square", "营区广场"), ("h06_shelter", "收容所") })
        {
            var c = (JObject)field.DeepClone();
            c["id"] = id;
            c["name"] = name;
            c["initiallyVisited"] = false;
            c["initiallyDiscovered"] = false;
            locs.Add(c);
        }
        return ConfigLoader.FromJson(j.ToString());
    }

    private const string Lin = "npc_lin_zhao", Gu = "npc_gu_cheng", Zhou = "npc_zhou_yan", Tang = "npc_tang_li", Shen = "npc_shen_he";

    /// <summary>遗物内容的快照：每个实例的全部字段（实例号、物品、数量、owner、耐久、保质等）。</summary>
    private static List<string> Snap(GameState s) =>
        s.Items.Where(i => i.Owner.StartsWith("remains:")).OrderBy(i => i.InstanceId)
            .Select(i => JObject.FromObject(i).ToString(Newtonsoft.Json.Formatting.None)).ToList();

    /// <summary>
    /// SV-Q98-01/02：版本 16 旧档——camp 非战斗死亡（候补顾承、同行林昭）分不清来源记“未知”，camp 战斗中死亡（周砚）与 camp 以外死亡（唐梨）归同行；
    /// 读档后新死的候补（沈禾）是明确的公共候补遗物。地图导入迁址：只有沈禾的迁 h02，未知与同行的都留在 camp 原地、到 camp 仍能取，
    /// 未知遗物不阻塞候补遗物迁移；内容物实例、owner、耐久、保质不变，不复制；重复读档/迁址不再迁、不改来源。
    /// </summary>
    [Test]
    public void SV_Q98_UnknownOriginStaysPut_OnlyExplicitReserveMoves_Idempotent()
    {
        var e = new GameEngine(V3Cfg());
        var s = e.NewGame(9801);
        e.DebugLegacyStory(s);
        s.ServiceMapMigrated = false;   // 地图导入前开的局
        foreach (var f in new[] { "hub_registered", "church_route_record", "academy_safety_record" }) s.Progress.Flags.Add(f);
        s.SpawnNextCheck = int.MaxValue;
        s.PatrolNextCheck = int.MaxValue;
        s.GameMinute = At(1, 10, 0);
        Assert.That(e.HubLocation(s), Is.EqualTo("camp"));

        e.DebugRecruit(s, Lin);
        e.DebugRecruit(s, Gu);
        e.DebugRecruit(s, Zhou);
        e.SetActive(s, Gu, false);
        e.DebugActor(s, Gu, "kill");    // 候补在营区（camp）非战斗死亡
        e.DebugActor(s, Lin, "kill");   // 同行在 camp 非战斗死亡
        e.DebugActor(s, Zhou, "kill");  // 下面在旧档里改成“战斗中死亡”
        e.DebugRecruit(s, Tang);
        e.DebugTeleport(s, "field");
        e.DebugActor(s, Tang, "kill");  // 同行死在 camp 以外
        e.DebugTeleport(s, "camp");

        // 做成版本 16 旧档：没有 PublicReserve / ServiceMapMigrated，周砚的遗物记成战斗中留下的
        var j = JObject.Parse(SaveSerializer.Serialize(s));
        j["SchemaVersion"] = "16";
        j.Remove("ServiceMapMigrated");
        foreach (var r in ((JObject)j["Remains"]!).Properties()) ((JObject)r.Value).Remove("PublicReserve");
        j["Remains"]![Zhou]!["BattleId"] = 1;
        var m = SaveSerializer.Deserialize(j.ToString(), e.Config, out _);
        Assert.That(m.Log.Any(l => l.Text.Contains("存档已升级到版本17（服务地点）")), Is.True);
        Assert.That(m.Remains[Gu].PublicReserve, Is.Null, "旧 camp 非战斗死亡：不猜来源，记未知");
        Assert.That(m.Remains[Lin].PublicReserve, Is.Null);
        Assert.That(m.Remains[Zhou].PublicReserve, Is.False, "旧战斗死亡归同行");
        Assert.That(m.Remains[Tang].PublicReserve, Is.False, "旧非 camp 死亡归同行");
        Assert.That(m.ServiceMapMigrated, Is.False);

        // 读档后新死的候补：明确公共候补遗物
        e.DebugRecruit(m, Shen);
        e.SetActive(m, Shen, false);
        Assert.That(e.IsReserve(m, Shen), Is.True);
        e.DebugActor(m, Shen, "kill");
        Assert.That(m.Remains[Shen].PublicReserve, Is.True);

        var before = Snap(m);
        var itemCount = m.Items.Count;
        Assert.That(before, Is.Not.Empty);

        Assert.That(e.MigrateServiceLocations(m), Is.True);
        Assert.That(m.Remains[Shen].LocationId, Is.EqualTo("h02_square"), "明确公共候补遗物迁 h02");
        foreach (var id in new[] { Gu, Lin, Zhou })
            Assert.That(m.Remains[id].LocationId, Is.EqualTo("camp"), $"{id}：未知/同行不额外迁，留在原地");
        Assert.That(m.Remains[Tang].LocationId, Is.EqualTo("field"));
        Assert.That(m.Remains[Gu].PublicReserve, Is.Null, "迁址不改来源");
        Assert.That(Snap(m), Is.EqualTo(before), "内容物实例、owner、耐久、保质不变");
        Assert.That(m.Items.Count, Is.EqualTo(itemCount), "不复制、不丢物");

        // 原地仍可取；迁走的要到 h02 取
        e.DebugTeleport(m, "camp");
        var here = e.RemainsHere(m).Select(kv => kv.Key).ToList();
        Assert.That(here, Does.Contain(Gu).And.Contain(Lin).And.Contain(Zhou));
        Assert.That(here, Does.Not.Contain(Shen));
        var guItem = e.RemainsItems(m, Gu).First();
        Assert.That(e.TakeFromRemainsBlocked(m, Gu, guItem.InstanceId, true), Is.Null, "未知来源遗物在原地可取");
        Assert.That(e.TakeFromRemainsBlocked(m, Shen, e.RemainsItems(m, Shen).First().InstanceId, true), Does.Contain("要到那里才能取"));
        e.DebugTeleport(m, "h02_square");
        Assert.That(e.RemainsHere(m).Select(kv => kv.Key), Is.EqualTo(new[] { Shen }));

        // 幂等：存读档不再迁移、不改来源；再调迁址不再动
        var again = SaveSerializer.Deserialize(SaveSerializer.Serialize(m), e.Config, out var notes2);
        Assert.That(notes2, Is.Empty);
        Assert.That(again.Remains[Gu].PublicReserve, Is.Null);
        Assert.That(again.Remains[Shen].PublicReserve, Is.True);
        Assert.That(e.MigrateServiceLocations(again), Is.False);
        Assert.That(again.Remains.ToDictionary(kv => kv.Key, kv => kv.Value.LocationId),
            Is.EqualTo(m.Remains.ToDictionary(kv => kv.Key, kv => kv.Value.LocationId)));
        Assert.That(Snap(again), Is.EqualTo(before));
        Assert.That(again.Items.Count, Is.EqualTo(itemCount));
    }
}
