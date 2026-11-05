using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 批次 B27（R11 Q220）：地图水源图标与四类账徽标的内核数据。图形本身在 web（AccountIcon.razor、MapOverlay.razor），这里验形状分类、
/// 只显示已发现的水源、不改库存/日补/水种。规则来源：city-adjudications.json overviewQ220Q225 cityCopy.handoff、A10-EXUI §3/§5。
/// </summary>
public class B27MapWaterTests : TestBase
{
    private GameState Fresh()
    {
        var s = New();
        s.GameMinute = At(2, 9, 0);
        s.SpawnNextCheck = int.MaxValue;
        s.PatrolNextCheck = int.MaxValue;
        return s;
    }

    private MapWaterMark Mark(GameState s, string layer, string loc) => E.BuildMapView(s, layer).Nodes.Single(n => n.Id == loc).Water.Single();

    private static void Know(GameState s, string resource, string loc)
    {
        s.Resources[resource].Discovered = true;
        s.Locations[loc].Discovered = true;
    }

    /// <summary>持续脏水、有限净水、每日补充脏水三种形状不同，悬停写资源点名、四类账短标、水种名。</summary>
    [TestCase("w12_canal_water", "w12_canal", "flow", false, "持续", "脏水")]
    [TestCase("w13_mill_bottles", "w13_mill", "bottle", true, "有限", "净水")]
    [TestCase("w16_service_cistern", "w16_service", "cistern", false, "每日补充", "脏水")]
    public void Q220_WaterMark_ByShape(string res, string loc, string shape, bool clean, string account, string water)
    {
        var s = Fresh();
        Know(s, res, loc);
        var m = Mark(s, "world", loc);
        Assert.That((m.Shape, m.Clean), Is.EqualTo((shape, (bool?)clean)));
        var name = Cfg.Resources.Single(r => r.NodeId == res).Name;
        Assert.That(m.Title, Is.EqualTo($"{name} · {account} · {E.ItemName(Cfg.Resources.Single(r => r.NodeId == res).Outputs.Keys.Single())}"));
        Assert.That(E.ItemName(Cfg.Resources.Single(r => r.NodeId == res).Outputs.Keys.Single()), Does.Contain(water));
    }

    /// <summary>城内的净水配给是每日补充的净水：形状同蓄水点，水种不同，“储水”不被读成净水，也不把净水读成脏水。</summary>
    [Test]
    public void Q220_CityCleanRation_CisternClean()
    {
        var s = Fresh();
        E.DebugMapTeleport(s, "h02_square");
        ReadDialogues(s);
        s.Resources["h02_square_water"].Discovered = true;
        var m = E.BuildMapView(s, E.MapLayerOf("h02_square")).Nodes.Single(n => n.Id == "h02_square").Water.Single(w => w.Title.StartsWith(Cfg.Resources.Single(r => r.NodeId == "h02_square_water").Name));
        Assert.That((m.Shape, m.Clean), Is.EqualTo(("cistern", (bool?)true)));
    }

    /// <summary>未发现的水源不出图标；混装补给箱不算水源。</summary>
    [Test]
    public void Q220_UnknownOrMixed_NoMark()
    {
        var s = Fresh();
        s.Locations["w12_canal"].Discovered = true;
        s.Resources["w12_canal_water"].Discovered = false;
        Assert.That(E.BuildMapView(s, "world").Nodes.Single(n => n.Id == "w12_canal").Water, Is.Empty);
        s.Locations["m02_checkpoint"].Discovered = true;
        s.Resources["m02_checkpoint_duty"].Discovered = true;
        var layer = E.MapLayerOf("m02_checkpoint");
        Assert.That(E.BuildMapView(s, layer).Nodes.Single(n => n.Id == "m02_checkpoint").Water, Is.Empty);
    }

    /// <summary>已建成的固定净水台是“可处理设施”：有形状、没有水种，悬停写工位名。</summary>
    [Test]
    public void Q220_BuiltWaterStation_Mark()
    {
        var s = Fresh();
        s.Locations["camp"].Facility.Workshops.Add("water_station");
        var m = E.BuildMapView(s, "world").Nodes.Single(n => n.Id == "camp").Water.Single(w => w.Shape == "station");
        Assert.That((m.Clean, m.Title), Is.EqualTo(((bool?)null, "固定净水台")));
    }

    /// <summary>净水台建在安全屋屋内区也算这个地点的设施。</summary>
    [Test]
    public void Q220_WaterStationInsideSafehouse_Mark()
    {
        var s = Fresh();
        s.Locations["camp"].Safehouse = new SafehouseState();
        s.Locations["camp"].Safehouse!.Interior.Workshops.Add("water_station");
        Assert.That(E.BuildMapView(s, "world").Nodes.Single(n => n.Id == "camp").Water.Count(w => w.Shape == "station"), Is.EqualTo(1));
    }

    /// <summary>四类账短标仍在，五种短标各不相同；出图标不改存档（库存、日补、水种、发现状态）。</summary>
    [Test]
    public void Q220_Labels_Kept_ViewIsReadOnly()
    {
        var labels = new[] { "w34_depot_a", "w11_orchard_a", "w12_canal_water", "d01_04_cache0", "d01_08_unique" }
            .Select(id => GameEngine.AccountLabel(Cfg.Resources.Single(r => r.NodeId == id))).ToList();
        Assert.That(labels, Is.EqualTo(new[] { "有限", "每日补充", "持续", "本实例", "本局一次" }));
        var s = Fresh();
        Know(s, "w13_mill_bottles", "w13_mill");
        var before = SaveSerializer.Serialize(s);
        E.BuildMapView(s, "world");
        Assert.That(SaveSerializer.Serialize(s), Is.EqualTo(before));
    }
}
