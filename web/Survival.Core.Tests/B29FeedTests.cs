using Newtonsoft.Json.Linq;
using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 B29 同行食饮入口（R14、R67、R90—R93、R96）。合同 assets/20261105/b29-entry-water-q258-268.json feeding；
/// 验收 Q258-268-A06—A09、A13、A14。
/// </summary>
public class B29FeedTests : TestBase
{
    private const string Me = GameState.PlayerId;
    private const string Lin = "npc_lin_zhao";
    private const string Tang = "npc_tang_li";

    private GameState Party(params string[] ids)
    {
        var s = New();
        s.Progress.Flags.Add("hub_registered");
        s.Progress.Flags.Add("church_route_record");
        s.Progress.Flags.Add("academy_safety_record");
        s.SpawnNextCheck = int.MaxValue;
        s.PatrolNextCheck = int.MaxValue;
        s.GameMinute = At(1, 10, 0);
        foreach (var id in ids.Length == 0 ? new[] { Lin, Tang } : ids) E.DebugRecruit(s, id);
        foreach (var a in s.Actors.Values)
        {
            a.Stats.Satiety = 50;
            a.Stats.Hydration = 50;
            a.Stats.Morale = 50;
        }
        return s;
    }

    private static string Bag(string actor) => ItemInstance.CarriedOwner(actor);

    private ItemInstance Give(GameState s, string item, string owner, int count = 1) => E.CreateItems(s, item, count, owner).Single();

    private static FeedRequest Req(string target, string source, ItemInstance item) =>
        new FeedRequest { TargetActorId = target, SourceActorId = source, InstanceId = item.InstanceId };

    // ───────────── Q268 零收益与加工拒绝（A13、A14） ─────────────

    [Test]
    public void A13_ZeroDelta_EachStat_FormalSubstring()
    {
        var c = (JObject)JObject.Parse(File.ReadAllText(Doc()))["feeding"]!["deltaCopy"]!;
        Assert.That(c["zero"]!.Value<string>(), Is.EqualTo(FeedCopy.ZeroDelta));
        Assert.That(c["processingFood"]!.Value<string>(), Is.EqualTo(FeedCopy.ProcessingFood));

        var s = Party();
        s.Actors[Lin].Stats.Satiety = 100;
        Assert.That(E.Feed(s, Req(Lin, Me, Give(s, "canned_food", ItemInstance.Carried))).Ok, Is.True);
        Assert.That(s.Log.Last().Text, Does.EndWith("：饱食无变化。"));

        var t = Party();
        t.Actors[Lin].Stats.Hydration = 100;
        var water = Give(t, "clean_water", ItemInstance.Carried);
        Assert.That(E.FeedPreview(t, Req(Lin, Me, water)).Feed!.ChangeText, Is.EqualTo("水分无变化"));
    }

    [Test]
    public void A13_ZeroDelta_MergesWithOtherFeedback_OriginalSeparator()
    {
        var s = Party();
        s.Actors[Lin].Stats.Hydration = 100;
        var dirty = Give(s, "dirty_water", ItemInstance.Carried);
        Assert.That(E.FeedPreview(s, Req(Lin, Me, dirty)).Feed!.ChangeText, Is.EqualTo("水分无变化，出现腹泻（12小时）"));
    }

    [Test]
    public void A13_TinyNonZero_KeepsSignAndPrecision()
    {
        var s = Party();
        s.Actors[Lin].Stats.Hydration = 100 - 1e-5;
        var water = Give(s, "clean_water", ItemInstance.Carried);
        var text = E.FeedPreview(s, Req(Lin, Me, water)).Feed!.ChangeText;
        Assert.That(text, Does.StartWith("水分+").And.Not.Contain("无变化"), "极小非零不写无变化");
    }

    [Test]
    public void A13_SelfUseCopy_Unchanged()
    {
        var s = Party();
        s.Player.Stats.Satiety = 100;
        var can = Give(s, "canned_food", ItemInstance.Carried);
        Start(s, ActionRequest.UseInstance(can.InstanceId));
        Assert.That(string.Join("\n", s.Log.Select(l => l.Text)), Does.Contain("饱食+").And.Not.Contain("无变化"), "主角自用仍写配置量");
    }

    [Test]
    public void A14_ProcessingFood_OnlyForFoodProcessBranch()
    {
        var s = Party();
        var pot = Give(s, "pot", ItemInstance.Carried);
        var food = Give(s, "boiled_root", ItemInstance.ContainerOwner(pot.InstanceId));
        var water = Give(s, "dirty_water", ItemInstance.ContainerOwner(pot.InstanceId), 2);
        var fac = s.Locations["camp"].Facility;
        fac.Processes.Add(new FacilityProcess { RecipeId = "boil_root", TargetInstanceId = food.InstanceId, PotInstanceId = pot.InstanceId });
        Assert.That(E.FeedBlocked(s, Req(Lin, Me, food)), Is.EqualTo(FeedCopy.ProcessingFood));

        // 真实预留先于加工分支，仍写原句
        food.ReservedBy = 5;
        Assert.That(E.FeedBlocked(s, Req(Lin, Me, food)), Is.EqualTo("该物品已被预留"));
        food.ReservedBy = null;

        // 锅水不套“食物正在加工”
        fac.Processes.Clear();
        fac.Processes.Add(new FacilityProcess { RecipeId = "boil_water", TargetInstanceId = water.InstanceId, PotInstanceId = pot.InstanceId });
        Assert.That(E.FeedBlocked(s, Req(Lin, Me, water)), Is.EqualTo("该物品已被预留"));
    }

    [Test]
    public void A14_SelfUseQ23Entry_Unchanged()
    {
        var s = Party();
        var pot = Give(s, "pot", ItemInstance.Carried);
        var food = Give(s, "boiled_root", ItemInstance.ContainerOwner(pot.InstanceId));
        s.Locations["camp"].Facility.Processes.Add(new FacilityProcess { RecipeId = "boil_root", TargetInstanceId = food.InstanceId, PotInstanceId = pot.InstanceId });
        Assert.That(E.CanStart(s, ActionRequest.UseInstance(food.InstanceId)), Is.Null, "主角直接吃受保护食材的入口不变");
    }

    private static string Doc()
    {
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "doc", "策划案"))) dir = dir.Parent;
        Assert.That(dir, Is.Not.Null, "找不到仓库根目录");
        return Path.Combine(dir!.FullName, "doc", "策划案", "系统策划总册 v1.0", "assets", "20261105", "b29-entry-water-q258-268.json");
    }
}
