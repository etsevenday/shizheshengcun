using Newtonsoft.Json.Linq;
using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 批次 B13c（R130；答复 Q124 选 A）：13 个世界分区正式名进面包屑（“主世界 / 分区 / 当前地点”，地牢按父入口分区）与主世界分区导航；
/// 未知分区不出现，导航只读（0 分钟、不发现、不移动），土地归属/建屋许可不随分区名变。验收 NAME-Q124-01。
/// </summary>
public class B13cRegionTests : TestBase
{
    private static JObject Adj() => JObject.Parse(File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "design.resource-adjudications.json")));

    [Category("DesignSync")]
    [Test]
    public void NAME_Q124_01_RegionNamesFromDesign()
    {
        var names = ((JObject)Adj()["regionNames"]!).Properties().ToDictionary(p => p.Name, p => (string)p.Value!);
        Assert.That(names, Has.Count.EqualTo(13));
        Assert.That(Cfg.Map.RegionNames, Is.EquivalentTo(names));
        foreach (var l in Cfg.Locations) Assert.That(names.ContainsKey(E.RegionOf(l.Id)), Is.True, l.Id + " 有分区");
        Assert.That(E.RegionOf("d05_04"), Is.EqualTo(E.Location("w33_waterworks").Zone), "地牢按父入口分区");
        Assert.That(E.RegionName("deep_wild"), Is.EqualTo("深野腹地"));
    }

    /// <summary>NAME-Q124-01：面包屑与分区导航读同一名字；主世界/城内/地牢三种写法。</summary>
    [Test]
    public void NAME_Q124_01_Breadcrumbs()
    {
        var s = New();
        E.DebugMapTeleport(s, "w10_watchtower");
        var w = E.BuildMapView(s, GameEngine.WorldLayer);
        var region = E.RegionName(E.Location("w10_watchtower").Zone);
        Assert.That(w.Breadcrumb, Is.EqualTo(new[] { "主世界", region, E.LocName("w10_watchtower") }));
        Assert.That(E.KnownMapRegions(s).Select(r => r.Name), Does.Contain(region));

        E.DebugMapTeleport(s, "h02_square");
        var city = E.BuildMapView(s, "city:h02_square");
        Assert.That(city.Breadcrumb, Is.EqualTo(new[] { "主世界", "幸存者营区", E.LocName("h02_square") }), "不写“营区广场城内 / 营区广场”");
        E.DebugMapTeleport(s, "d05_04");
        var d = E.BuildMapView(s);
        Assert.That(d.Breadcrumb, Is.EqualTo(new[] { "主世界", E.RegionName(E.Location("w33_waterworks").Zone), E.MapLayerName("dungeon:d05"), E.LocName("d05_04") }));
        var cityAway = E.BuildMapView(s, "city:h02_square");
        Assert.That(cityAway.Breadcrumb, Is.EqualTo(new[] { "主世界", "幸存者营区", E.LocName("h02_square") + "城内" }), "人不在该城时末段写城内层");
        Assert.That(E.BuildMapView(s, GameEngine.WorldLayer).Breadcrumb, Is.EqualTo(new[] { "主世界" }), "当前位置不在主世界层");
    }

    /// <summary>NAME-Q124-01：分区导航只列有已知地点的分区（新局只有初醒荒野），点选只读、不发现、不移动；分区名不改土地归属。</summary>
    [Test]
    public void NAME_Q124_01_RegionNav_NoLeak_ReadOnly()
    {
        var s = New();
        var before = SaveSerializer.Serialize(s);
        var regions = E.KnownMapRegions(s);
        Assert.That(SaveSerializer.Serialize(s), Is.EqualTo(before), "只读");
        var knownZones = s.Locations.Where(kv => kv.Value.Discovered && E.Location(kv.Key).NodeKind != "dungeon").Select(kv => E.RegionOf(kv.Key)).Distinct().ToList();
        Assert.That(regions.Select(r => r.Key), Is.EquivalentTo(knownZones), "未知分区不出现");
        Assert.That(regions.Single(r => r.HasHere).Key, Is.EqualTo(E.RegionOf(s.CurrentLocation)));
        Assert.That(regions.Select(r => r.Name), Does.Not.Contain("深野腹地"));
        E.DebugMapTeleport(s, "w10_watchtower");
        regions = E.KnownMapRegions(s);
        var r = regions.Single(x => x.Key == E.Location("w10_watchtower").Zone);
        var known = E.BuildMapView(s, GameEngine.WorldLayer).Nodes.Where(n => E.RegionOf(n.Id) == r.Key).ToList();
        Assert.That(r.Known, Is.EqualTo(known.Count));
        Assert.That(r.CenterX, Is.EqualTo(known.Average(n => n.X)).Within(0.01));
        // 分区名只是显示：church/academy/works 分区的土地归属与建屋许可仍读地点自身
        foreach (var l in Cfg.Locations.Where(l => l.Zone is "church" or "academy" or "works"))
            Assert.That(l.LandOwner, Is.Not.Null, l.Id);
    }
}
