using Newtonsoft.Json.Linq;
using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 B29.6（R98，Q273）：队伍面板随身展开行按逐字模板写，不带实例编号；行与入口仍绑定真实实例。
/// 合同 assets/20261106/q269-q273-display-loot-adjudications.json companionRows；验收 Q269-273-A07。
/// </summary>
public class B29CompanionRowTests : TestBase
{
    private const string Lin = "npc_lin_zhao";
    private const string Tang = "npc_tang_li";

    private GameState Party()
    {
        var s = New();
        s.Progress.Flags.Add("hub_registered");
        s.Progress.Flags.Add("church_route_record");
        s.Progress.Flags.Add("academy_safety_record");
        s.SpawnNextCheck = int.MaxValue;
        s.PatrolNextCheck = int.MaxValue;
        s.GameMinute = At(1, 10, 0);
        E.DebugRecruit(s, Lin);
        E.DebugRecruit(s, Tang);
        s.Items.RemoveAll(i => i.Owner == Bag(Lin));
        return s;
    }

    private static string Bag(string actor) => ItemInstance.CarriedOwner(actor);

    private ItemInstance Give(GameState s, string item, string owner, int count = 1) => E.CreateItems(s, item, count, owner).Single();

    private ItemInstance Canteen(GameState s, string? liquid, int ml)
    {
        var c = Give(s, "ration_canteen", Bag(Lin));
        c.LiquidId = liquid;
        c.LiquidMl = ml;
        return c;
    }

    [Test]
    public void A07_Templates_MatchContract()
    {
        var c = (JObject)JObject.Parse(File.ReadAllText(Doc()))["companionRows"]!;
        foreach (var (key, text) in CompanionRowCopy.All)
            Assert.That(c[key]!.Value<string>(), Is.EqualTo(text), key);
        Assert.That(c.Properties().Select(p => p.Name).Where(n => n != "policy" && n != "examples"), Is.EquivalentTo(CompanionRowCopy.All.Keys));
    }

    [Test]
    public void A07_PlainRows_SingleStack_NoSuffixWithoutExpiry()
    {
        var s = Party();
        Give(s, "canned_food", Bag(Lin));
        Give(s, "wood", Bag(Lin), 3);
        var rows = E.CompanionCarried(s, Lin);
        Assert.That(rows.Select(r => r.Text), Is.EqualTo(new[] { "罐头", $"{E.ItemName("wood")}×3" }), "一份省略×1，无截止不加后缀");
    }

    [Test]
    public void A07_FreshAndSpoiled_Suffixes()
    {
        var s = Party();
        var fresh = Give(s, "berry", Bag(Lin), 2);
        Assert.That(fresh.ExpiresAt, Is.EqualTo(At(2, 10, 0)));
        Assert.That(E.CompanionCarried(s, Lin).Single().Text, Is.EqualTo("野果×2（保质到第2日 10:00）"));

        fresh.ExpiresAt = s.GameMinute;
        Assert.That(E.CompanionCarried(s, Lin).Single().Text, Is.EqualTo("野果×2（已腐败）"), "已腐败只附腐败标记，不写截止");

        fresh.Count = 1;
        fresh.ExpiresAt = s.GameMinute + 1;
        Assert.That(E.CompanionCarried(s, Lin).Single().Text, Is.EqualTo("野果（保质到第1日 10:01）"), "到期前一分钟仍是保质");
    }

    [Test]
    public void A07_Pot_WaterAndSlots_ChildrenUsePlainTemplate()
    {
        var s = Party();
        var pot = Give(s, "pot", Bag(Lin));
        Assert.That(E.CompanionCarried(s, Lin).Single().Text, Is.EqualTo($"金属锅（{E.PotLabel(s, pot)}，食材0/2）"));
        Assert.That(E.PotLabel(s, pot), Does.StartWith("空锅"), "空锅沿原空态文字");

        var inside = ItemInstance.ContainerOwner(pot.InstanceId);
        var water = Give(s, "clean_water", inside, 2);
        var berry = Give(s, "berry", inside);
        var rows = E.CompanionCarried(s, Lin);
        Assert.That(rows.Select(r => r.Text), Is.EqualTo(new[] { "金属锅（净水100%·1000/1000毫升，食材1/2）", "净水×2", "野果（保质到第2日 10:00）" }));
        Assert.That(rows.Select(r => r.InPot), Is.EqualTo(new[] { false, true, true }));
        Assert.That(rows.Select(r => r.InstanceId), Is.EqualTo(new[] { pot.InstanceId, water.InstanceId, berry.InstanceId }));

        Give(s, "berry", inside);
        Assert.That(E.CompanionCarried(s, Lin)[0].Text, Is.EqualTo("金属锅（净水100%·1000/1000毫升，食材2/2）"), "满槽");
    }

    [Test]
    public void A07_Canteen_FullPartialEmpty()
    {
        var s = Party();
        Canteen(s, "clean_water", 750);
        Canteen(s, "clean_water", 2000);
        Canteen(s, null, 0);
        Assert.That(E.CompanionCarried(s, Lin).Select(r => r.Text),
            Is.EqualTo(new[] { "分量水壶（净水 750/2000毫升）", "分量水壶（净水 2000/2000毫升）", "分量水壶（空 0/2000毫升）" }));
    }

    [Test]
    public void A07_SameText_NotMerged_EachBoundToOwnInstance()
    {
        var s = Party();
        var a = Canteen(s, "clean_water", 750);
        var b = Canteen(s, "clean_water", 750);
        var rows = E.CompanionCarried(s, Lin);
        Assert.That(rows, Has.Count.EqualTo(2), "同名行不合并");
        Assert.That(rows[0].Text, Is.EqualTo(rows[1].Text));
        Assert.That(rows.Select(r => r.InstanceId), Is.EqualTo(new[] { a.InstanceId, b.InstanceId }), "按实例号稳定排序");
        Assert.That(rows.Select(r => r.Feed!.InstanceId), Is.EqualTo(new[] { a.InstanceId, b.InstanceId }), "入口绑定各自实例");
        Assert.That(rows[0].Feed!.SourceKey, Is.Not.EqualTo(rows[1].Feed!.SourceKey));
    }

    [Test]
    public void A07_DevKit_NoInstanceNumberInAnyRow()
    {
        var s = New();
        E.DebugFeedKit(s);
        var all = E.FeedTargets(s).SelectMany(t => E.CompanionCarried(s, t.ActorId)).ToList();
        Assert.That(all, Is.Not.Empty);
        Assert.That(all.Where(r => r.Text.Contains('#')).Select(r => r.Text), Is.Empty);
        Assert.That(all.Select(r => r.Text), Has.Some.Contains("（已腐败）").And.Some.Contains("（保质到").And.Some.Contains("×")
            .And.Some.StartsWith("金属锅（").And.Some.StartsWith("分量水壶（"));
        Assert.That(all.Any(r => r.InPot && r.Text.Contains("（保质到")), Is.True, "锅内子项有保质行");
    }

    private static string Doc()
    {
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "doc", "策划案"))) dir = dir.Parent;
        Assert.That(dir, Is.Not.Null, "找不到仓库根目录");
        return Path.Combine(dir!.FullName, "doc", "策划案", "系统策划总册 v1.0", "assets", "20261106", "q269-q273-display-loot-adjudications.json");
    }
}
