using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// B41c 躲避隐藏与重现、原人口账（R05「Q420 躲避重现与固定关键敌人」「Q445」「Q503」；验收 Q420-A01、Q445-A01、Q503-A01、Q503-A02）。
/// </summary>
public class B41cReappearTests : B41cTestBase
{
    private void Reappear(GameState s)
    {
        var m = typeof(GameEngine).GetMethod("ReappearEvaded", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        m.Invoke(E, new object[] { s });
    }

    /// <summary>在多个随机状态下各重现一次，收集落点。</summary>
    private HashSet<string> Targets(GameState s, int id, int tries = 200)
    {
        var set = new HashSet<string>();
        for (uint c = 1; c <= tries; c++)
        {
            var t = SaveSerializer.Deserialize(SaveSerializer.Serialize(s), CfgV2);
            t.Rng.State = c * 2654435761u + 7;
            Reappear(t);
            var w = t.WorldEnemies.Single(x => x.Id == id);
            Assert.That(w.EvadeHiddenUntil, Is.Null);
            set.Add(w.LocationId);
        }
        return set;
    }

    private static WorldEnemy HideNow(GameState s, WorldEnemy w, string? account = null)
    {
        w.PopAccount = account ?? w.PopAccount;
        w.EvadeHiddenUntil = s.GameMinute;
        return w;
    }

    [Test]
    public void Q420_A01_ReappearSilently_NewAppearance_TimerRedrawn_XpAgain()
    {
        var (s, key) = OneShambler();
        Assert.That(E.StartStealth(s, key, "evade").Ok, Is.True);
        AdvanceUntil(s, t => t.Log.Any(l => l.Text.StartsWith("你避开了这处敌踪。")));
        var w = s.WorldEnemies.Single();
        var hp = w.Hp;
        Assert.That(E.AppearanceOf(s, key), Is.EqualTo(0));
        // 隐藏期间不巡游、不交战
        Assert.That(E.CanStealthCard(s, key, "sneak"), Is.EqualTo("这处敌踪暂时不可见。"));
        s.GameMinute = w.EvadeHiddenUntil!.Value - 5;
        Start(s, ActionRequest.Wait(30));
        var n = s.Log.Count;
        // 到点那一段重现到农野荒地本地（让随机落在原地）
        AdvanceUntil(s, t => t.WorldEnemies.Single().EvadeHiddenUntil == null && t.WorldEnemies.Single().LocationId == "field");
        Assert.That(w.EvadeHiddenUntil, Is.Null);
        Assert.That(w.Hp, Is.EqualTo(hp), "伤势保留");
        Assert.That(E.AppearanceOf(s, key), Is.EqualTo(1), "新一次现身");
        Assert.That(s.Log.Skip(n).Any(l => l.Text.Contains("敌踪")), Is.False, "普通敌人重现不额外提醒");
        Assert.That(E.EnemyCardsHere(s).Select(c => c.Key), Does.Contain(key));
        // 新现身可再领一次潜行经验
        if (s.Main != null) { s.Main = null; }
        Assert.That(E.StartStealth(s, key, "sneak").Ok, Is.True);
        AdvanceUntil(s, t => t.Battle != null);
        Assert.That(s.EnemyCards.StealthXpClaims, Is.EquivalentTo(new[] { key + "@0", key + "@1" }));
    }

    [Test]
    public void Q445_A01_Candidates_OriginAndNeighbors_ExcludeSafeCityDungeon()
    {
        var s = Field(1);
        var w = HideNow(s, E.DebugSpawnEnemy(s, "shambler", "field"));
        var got = Targets(s, w.Id);
        var allowed = new[] { "field" }.Concat(E.PhysicalNeighbors("field")).ToHashSet();
        Assert.That(got.IsSubsetOf(allowed), Is.True, string.Join(",", got));
        Assert.That(got, Does.Not.Contain("camp"), "不进安全区");
        Assert.That(got, Does.Contain("field"), "原地也是候选");
        Assert.That(got.Count, Is.GreaterThan(1), "相邻合法地点等概率");
        foreach (var l in got)
        {
            var lc = Cfg.Locations.Single(x => x.Id == l);
            Assert.That(lc.IsDungeon || lc.SafeExterior || !lc.RandomEncounterEnabled, Is.False, l);
        }
    }

    [Test]
    public void Q420_DungeonEnemy_StaysInSameDungeonDangerRooms()
    {
        var s = Field(1);
        var room = Cfg.Locations.First(l => l.DungeonId != null && E.IsDangerRoom(l.Id) && E.PhysicalNeighbors(l.Id).Any(n => E.IsDangerRoom(n)));
        var w = HideNow(s, E.DebugSpawnEnemy(s, "shambler", room.Id));
        w.Generation = s.DungeonInstances.TryGetValue(room.DungeonId!, out var inst) ? inst.Generation : 0;
        var got = Targets(s, w.Id, 60);
        Assert.That(got.All(l => E.DungeonAt(l) == room.DungeonId && E.IsDangerRoom(l)), Is.True, string.Join(",", got));
    }

    [Test]
    public void Q503_A01_LegacyEnemyIntoRegion_CountsOriginalAccount_LocalCountsActual()
    {
        var s = Field(1);
        var legacyCaps = Cfg.WorldEnemies.LocalCaps;
        var edge = Cfg.Edges.FirstOrDefault(e =>
            (legacyCaps.ContainsKey(e.A) && E.PopRegionOf(e.B) != null) || (legacyCaps.ContainsKey(e.B) && E.PopRegionOf(e.A) != null));
        if (edge == null) Assert.Inconclusive("没有旧 8 地点与人口区相邻的边");
        var from = legacyCaps.ContainsKey(edge!.A) ? edge.A : edge.B;
        var to = from == edge.A ? edge.B : edge.A;
        var region = E.PopRegionOf(to)!;
        var w = E.DebugSpawnEnemy(s, "shambler", from);
        var live = E.LiveCount(s);
        var reg = E.RegionLiveCount(s, region);
        // 首次躲避隐藏按当时所在地点锁原账；重现到人口区节点
        w.PopAccount = "legacy";
        w.LocationId = to;
        Assert.That(E.LiveCount(s), Is.EqualTo(live), "旧总账仍计");
        Assert.That(E.RegionLiveCount(s, region), Is.EqualTo(reg), "不改计目标区");
        Assert.That(E.LocalCount(s, to), Is.GreaterThanOrEqualTo(1), "节点计实际所在地");
        // 读档不换账
        var t = SaveSerializer.Deserialize(SaveSerializer.Serialize(s), CfgV2);
        Assert.That(t.WorldEnemies.Single(x => x.Id == w.Id).PopAccount, Is.EqualTo("legacy"));
        Assert.That(E.LiveCount(t), Is.EqualTo(live));
    }

    [Test]
    public void Q503_A01_ReappearIgnoresTargetCaps_HiddenKeepsOccupying()
    {
        var s = Field(1);
        var cap = E.LocalCapOf("field");
        var h = E.DebugSpawnEnemy(s, "shambler", "field");
        for (var k = 1; k < cap; k++) E.DebugSpawnEnemy(s, "shambler", "field");
        foreach (var n in E.PhysicalNeighbors("field").Where(n => E.LocalCapOf(n) > 0))
            while (E.LocalCount(s, n) < E.LocalCapOf(n)) E.DebugSpawnEnemy(s, "shambler", n);
        var live = E.LiveCount(s);
        h.PopAccount = "legacy";
        h.EvadeHiddenUntil = s.GameMinute + 720;
        Assert.That(E.LiveCount(s), Is.EqualTo(live), "隐藏期间保留原占账");
        h.EvadeHiddenUntil = s.GameMinute;
        Reappear(s);
        Assert.That(h.EvadeHiddenUntil, Is.Null, "目标满额仍落地");
        Assert.That(s.WorldEnemies.Count(x => x.Id == h.Id), Is.EqualTo(1), "不删敌");
        Assert.That(E.LiveCount(s), Is.EqualTo(live), "不重计、不重生");
    }

    [Test]
    public void Q503_A02_MixedAccountGroup_EachKeepsOwn_GroupMovesTogether()
    {
        var s = Field(3);
        var a = E.DebugSpawnEnemy(s, "shambler", "field");
        var b = E.DebugSpawnEnemy(s, "crawler", "field");
        var gid = s.EnemyCards.NextGroupId++;
        s.EnemyCards.Groups.Add(new EnemyGroupCard { Id = gid, LocationId = "field", MemberIds = new List<int> { a.Id, b.Id }, Source = "explore", Capacity = 3 });
        a.GroupId = gid; b.GroupId = gid;
        var regionId = Cfg.WorldEnemies.Regions[0].Id;
        HideNow(s, a, "legacy");
        HideNow(s, b, "region:" + regionId);
        for (uint c = 1; c < 400; c++)
        {
            s.Rng.State = c * 2654435761u + 7;
            var t = SaveSerializer.Deserialize(SaveSerializer.Serialize(s), CfgV2);
            Reappear(t);
            var ta = t.WorldEnemies.Single(x => x.Id == a.Id);
            var tb = t.WorldEnemies.Single(x => x.Id == b.Id);
            if (ta.LocationId == "field") continue;
            Assert.That(tb.LocationId, Is.EqualTo(ta.LocationId), "整组一起落地");
            Assert.That(t.EnemyCards.Groups.Single().LocationId, Is.EqualTo(ta.LocationId), "组卡停在重现地");
            Assert.That((ta.PopAccount, tb.PopAccount), Is.EqualTo(("legacy", "region:" + regionId)), "各成员各归原账");
            Assert.That(t.EnemyCards.Appearances[GameEngine.GroupCardKey(gid)], Is.EqualTo(1));
            return;
        }
        Assert.Fail("没有抽到移到相邻地点的随机状态");
    }

    [Test]
    public void Q420_Elite_ReappearsElsewhere_CardShownAtActualPlace_NoNewInstance()
    {
        var s = Field(1);
        E.DebugTeleport(s, "w33_waterworks");
        s.Progress.Flags.Add("hub_registered");
        s.Locations["w33_waterworks"].Progress = 100;
        s.WorldEnemies.Clear();
        var key = E.EnemyCardsHere(s).Single(c => c.Kind == "elite").Key;
        Assert.That(E.StartStealth(s, key, "evade").Ok, Is.True);
        AdvanceUntil(s, t => t.Log.Any(l => l.Text.StartsWith("你避开了这处敌踪。")));
        var w = s.WorldEnemies.Single(x => x.FixedOf == key);
        Assert.That(w.PopAccount, Is.EqualTo("none"), "可选强敌原来不计随机人口");
        HideNow(s, w);
        for (uint c = 1; c < 400; c++)
        {
            var t = SaveSerializer.Deserialize(SaveSerializer.Serialize(s), CfgV2);
            t.Rng.State = c * 2654435761u + 7;
            Reappear(t);
            var tw = t.WorldEnemies.Single(x => x.FixedOf == key);
            if (tw.LocationId == "w33_waterworks") continue;
            Assert.That(E.EnemyCardsHere(t).Select(x => x.Key), Does.Not.Contain(key), "原告示处不显示");
            E.DebugTeleport(t, tw.LocationId);
            Assert.That(E.EnemyCardsHere(t).Select(x => x.Key), Does.Contain(key), "卡在实际所在地点显示");
            Assert.That(t.WorldEnemies.Count(x => x.FixedOf == key), Is.EqualTo(1), "不另补敌人");
            return;
        }
        Assert.Inconclusive("强敌告示处没有合法相邻地点");
    }
}
