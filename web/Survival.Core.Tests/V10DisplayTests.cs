using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>策划提交 9fdb44f：Q13 重量统一公斤两位（S06 §7，S06-A07）与地块相对方位（S02 §3.1，S02-A07/A08）。</summary>
public class V10DisplayTests : TestBase
{
    [Test]
    public void S06_A07_WeightTwoDecimalKg()
    {
        Assert.That(Fmt.Kg(10), Is.EqualTo("0.01公斤"));
        Assert.That(Fmt.Kg(30), Is.EqualTo("0.03公斤"));
        Assert.That(Fmt.Kg(50), Is.EqualTo("0.05公斤"));
        Assert.That(Fmt.Kg(1000), Is.EqualTo("1.00公斤"));
        Assert.That(Fmt.Kg(0), Is.EqualTo("0.00公斤"));
        Assert.That(Fmt.Kg(1005), Is.EqualTo("1.01公斤"), "中点远离零");
        Assert.That(Fmt.Kg(1004), Is.EqualTo("1.00公斤"));
        Assert.That(Fmt.Kg(25000), Is.EqualTo("25.00公斤"));
        Assert.That(Fmt.KgValue(12345), Is.EqualTo("12.35"));

        // 先按精确克数合计再显示：3×5g 应显示 0.02（合计15g），不是 3×0.01=0.03
        Assert.That(Fmt.Kg(3 * 5), Is.EqualTo("0.02公斤"));

        // 整锅（锅+内容）与批量：显示用 ItemGrams 合计；计重阈值仍用克
        var s = New();
        var pot = First(s, "pot");
        E.DebugGive(s, "dirty_water", 2, "camp");
        E.DoInstant(s, InstantRequest.FillPot(First(s, "dirty_water", "camp").InstanceId, pot.InstanceId, 2));
        var grams = E.ItemGrams(s, pot);
        Assert.That(grams, Is.EqualTo(E.Item("pot").WeightGrams + E.PotContents(s, pot.InstanceId).Sum(c => E.ItemGrams(s, c))));
        Assert.That(Fmt.Kg(grams), Is.EqualTo((grams / 1000m).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + "公斤"));
        Assert.That(E.IsOverweight(s), Is.EqualTo(E.CarriedGrams(s) > Cfg.Carry.SoftGrams), "阈值按克比较");
    }

    /// <summary>S02-A07：现行 8 条连接往返方位与 §3.1 表一致、反向相反；不改连通性。</summary>
    [Test]
    public void S02_A07_EightEdgesRoundTrip()
    {
        var table = new (string a, string b, MapDirection ab, MapDirection ba)[]
        {
            ("camp", "field", MapDirection.Right, MapDirection.Left),
            ("field", "stream", MapDirection.DownLeft, MapDirection.UpRight),
            ("field", "woods", MapDirection.Right, MapDirection.Left),
            ("field", "village", MapDirection.Down, MapDirection.Up),
            ("village", "hardware", MapDirection.Right, MapDirection.Left),
            ("village", "clinic", MapDirection.Left, MapDirection.Right),
            ("woods", "scrapyard", MapDirection.DownRight, MapDirection.UpLeft),
            ("hardware", "scrapyard", MapDirection.UpRight, MapDirection.DownLeft),
        };
        Assert.That(Cfg.Edges, Has.Count.EqualTo(8));
        foreach (var (a, b, ab, ba) in table)
        {
            Assert.That(Cfg.Edges.Any(e => e.Touches(a) && e.Other(a) == b), Is.True, $"{a}—{b} 是现行连接");
            Assert.That(GameEngine.DirectionBetween(E.Location(a), E.Location(b)), Is.EqualTo(ab), $"{a}→{b}");
            Assert.That(GameEngine.DirectionBetween(E.Location(b), E.Location(a)), Is.EqualTo(ba), $"{b}→{a}");
            Assert.That(GameEngine.Opposite(ab), Is.EqualTo(ba));
        }
        Assert.That(GameEngine.DirectionArrow(MapDirection.UpRight), Is.EqualTo("↗"));
        Assert.That(GameEngine.DirectionHint(MapDirection.Right), Is.EqualTo("位于当前地点右侧"));
        Assert.That(GameEngine.DirectionHint(MapDirection.DownLeft), Is.EqualTo("位于当前地点左下方"));
        // 全部地点两两往返都相反（含 22.5° 边界归顺时针下一扇区的统一规则）
        foreach (var x in Cfg.Locations)
        foreach (var y in Cfg.Locations.Where(y => y.Id != x.Id))
            Assert.That(GameEngine.DirectionBetween(y, x), Is.EqualTo(GameEngine.Opposite(GameEngine.DirectionBetween(x, y))), $"{x.Id}/{y.Id}");
        // 恰好 22.5°（tan22.5°）：归“右上”；反向 202.5° 归“左下”
        var o = new Config.LocationConfig { MapX = 0, MapY = 0 };
        var p = new Config.LocationConfig { MapX = Math.Tan(Math.PI / 8) * 100, MapY = -100 };
        Assert.That(GameEngine.DirectionBetween(o, p), Is.EqualTo(MapDirection.UpRight));
        Assert.That(GameEngine.DirectionBetween(p, o), Is.EqualTo(MapDirection.DownLeft));
        // 坐标重合：不编方向
        Assert.That(GameEngine.DirectionBetween(o, new Config.LocationConfig()), Is.EqualTo(MapDirection.None));
    }

    /// <summary>S02-A08：移动途中/取消仍以起点为参照，到达后刷新；未知节点不给方位；读档按位置重算。</summary>
    [Test]
    public void S02_A08_ReferenceUpdatesOnlyOnArrival_UnknownHidden()
    {
        var s = New();
        Assert.That(E.DirectionFromHere(s, "camp"), Is.EqualTo(MapDirection.None), "当前地点无自身箭头");
        Assert.That(E.DirectionFromHere(s, "field"), Is.EqualTo(MapDirection.Right));
        Assert.That(s.Locations["woods"].Discovered, Is.False);
        Assert.That(E.DirectionFromHere(s, "woods"), Is.EqualTo(MapDirection.None), "未发现不泄露方位");

        Start(s, ActionRequest.Travel("field"));
        E.Advance(s);
        Assert.That(s.Main, Is.Not.Null, "移动途中");
        Assert.That(E.DirectionFromHere(s, "field"), Is.EqualTo(MapDirection.Right), "途中仍按起点");
        E.Cancel(s);
        Assert.That(E.DirectionFromHere(s, "field"), Is.EqualTo(MapDirection.Right), "取消留起点");

        Start(s, ActionRequest.Travel("field"));
        Run(s);
        Assert.That(s.CurrentLocation, Is.EqualTo("field"));
        Assert.That(E.DirectionFromHere(s, "camp"), Is.EqualTo(MapDirection.Left), "到达后刷新：营地在左");
        var back = Survival.Core.Save.SaveSerializer.Deserialize(Survival.Core.Save.SaveSerializer.Serialize(s), Cfg, out _);
        Assert.That(E.DirectionFromHere(back, "camp"), Is.EqualTo(MapDirection.Left), "读档同源重算");
    }
}
