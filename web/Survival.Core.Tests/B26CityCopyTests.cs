using Newtonsoft.Json.Linq;
using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 批次 B26（R2、R4、R5；Q222—Q224）。规则来源：策划合同 content-feeding-adjudications.json cityCopy（逐字）；
/// 验收 PLAN-Q222-01、PLAN-Q223-01、PLAN-Q224-01。这里用合同原模板填真实字段，与 CityCopy 输出逐字比对。
/// </summary>
public class B26CityCopyTests : TestBase
{
    private static readonly JObject Copy = (JObject)JObject.Parse(File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "design.content-feeding.json")))["cityCopy"]!;

    private static string T(string key, params (string K, object V)[] fields)
    {
        var t = (string)Copy[key]!;
        foreach (var (k, v) in fields) t = t.Replace("{" + k + "}", v.ToString());
        Assert.That(t, Does.Not.Contain("{"), "合同字段都已填");
        return t;
    }

    private GameState Registered(string loc)
    {
        var s = New();
        s.Progress.Flags.UnionWith(new[] { "registration_authorized", "hub_registered" });
        s.GameMinute = At(2, 9, 0);
        s.SpawnNextCheck = int.MaxValue;
        s.PatrolNextCheck = int.MaxValue;
        E.DebugMapTeleport(s, loc);
        ReadDialogues(s);
        return s;
    }

    private void DiscoverCity(GameState s, string city)
    {
        foreach (var b in E.CityOverviewOf(s)!.Buildings) s.Locations[b.NodeId].Discovered = true;
    }

    private static CityBuildingView B(CityOverviewView v, string id) => v.Buildings.Single(b => b.NodeId == id);

    /// <summary>五句定稿与合同逐字一致。</summary>
    [Test]
    public void Q223_FiveLinesVerbatim()
    {
        Assert.That(T("readOnly"), Is.EqualTo(CityCopy.ReadOnly));
        Assert.That(T("legacyReveal"), Is.EqualTo(CityCopy.LegacyReveal));
        Assert.That(CityCopy.Confirm(2, "营区工坊", "营区广场"), Is.EqualTo(T("confirm", ("段数", 2), ("当前地点", "营区工坊"), ("下一地点", "营区广场"))));
        Assert.That(CityCopy.Arrival("营区广场", "鉴定所", 1), Is.EqualTo(T("arrival", ("到达地点", "营区广场"), ("最终目标", "鉴定所"), ("剩余段数", 1))));
        Assert.That(CityCopy.Arrived("鉴定所"), Is.EqualTo(T("arrived", ("最终目标", "鉴定所"))));
    }

    /// <summary>PLAN-Q222-01：只有已发现的城外独立地点写提示，句子逐字；它们不进城内路线图。</summary>
    [TestCase("h02_square", "h05_homestead")]
    [TestCase("c02_church", "c03_crypt")]
    [TestCase("r02_academy", "r03_lab")]
    public void Q222_OutsideNote_OnlyDiscovered(string city, string outside)
    {
        var s = Registered(city);
        s.Locations[outside].Discovered = false;
        Assert.That(E.CityOutsideNote(s, city), Is.Null, "未发现不写，不泄露地点名");
        s.Locations[outside].Discovered = true;
        Assert.That(E.CityOutsideNote(s, city), Is.EqualTo(T("outside", ("地点名", E.LocName(outside)))));
        Assert.That(E.CityOverviewOf(s)!.Buildings.Select(b => b.NodeId), Does.Not.Contain(outside), "不塞进城内路线图");
    }

    /// <summary>PLAN-Q223-01：多段路线到中途写还剩几段（只走了第一段，没有自动多走）；再走完写“已到达”，不写“还有0段”。</summary>
    [Test]
    public void Q223_MultiLegArrival_ThenFinal()
    {
        var s = Registered("h03_workshop");
        DiscoverCity(s, "h02_square");
        var path = B(E.CityOverviewOf(s)!, "h05_appraisal").Path!;
        Assert.That(path.Count, Is.EqualTo(2));
        Assert.That(E.CityArrivalNote(s, "h05_appraisal", path[0].To), Is.Null, "还没到不提示");
        Assert.That(E.StartCityOverviewTravel(s, "h05_appraisal", "req-1").Ok, Is.True);
        Assert.That(E.CityArrivalNote(s, "h05_appraisal", path[0].To), Is.Null, "路上不提示");
        Run(s);
        ReadDialogues(s);
        Assert.That(s.CurrentLocation, Is.EqualTo(path[0].To), "只走第一段");
        Assert.That(E.CityArrivalNote(s, "h05_appraisal", path[0].To), Is.EqualTo(T("arrival", ("到达地点", E.LocName(path[0].To)), ("最终目标", E.LocName("h05_appraisal")), ("剩余段数", 1))));

        Assert.That(E.StartCityOverviewTravel(s, "h05_appraisal", "req-2").Ok, Is.True);
        Run(s);
        ReadDialogues(s);
        Assert.That(s.CurrentLocation, Is.EqualTo("h05_appraisal"));
        var note = E.CityArrivalNote(s, "h05_appraisal", "h05_appraisal");
        Assert.That(note, Is.EqualTo(T("arrived", ("最终目标", E.LocName("h05_appraisal")))));
        Assert.That(note, Does.Not.Contain("段"));
    }

    /// <summary>单段路线也写“已到达”；中途路线失效写真实原因（不写“还有N段”）。</summary>
    [Test]
    public void Q223_SingleLeg_And_RouteLost()
    {
        var s = Registered("h03_workshop");
        DiscoverCity(s, "h02_square");
        Assert.That(B(E.CityOverviewOf(s)!, "h02_square").Path!.Count, Is.EqualTo(1));
        Assert.That(E.StartCityOverviewTravel(s, "h02_square", "req-1").Ok, Is.True);
        Run(s);
        ReadDialogues(s);
        Assert.That(E.CityArrivalNote(s, "h02_square", "h02_square"), Is.EqualTo(T("arrived", ("最终目标", E.LocName("h02_square")))));

        s.Locations["h05_appraisal"].Discovered = false;
        var why = E.CityArrivalNote(s, "h05_appraisal", "h02_square");
        Assert.That(why, Is.Not.Null);
        Assert.That(why, Does.Not.Contain("还有"));
    }

    /// <summary>PLAN-Q224-01：已接且记录已取、交谈可完成标“可交”；已领奖不再标；标记不代办。</summary>
    [Test]
    public void Q224_TurnIn_RecordAndTalk()
    {
        var s = Registered("h03_workshop");
        DiscoverCity(s, "h02_square");
        Assert.That(E.AcceptQuest(s, "EXQ05").Ok, Is.True);
        ReadDialogues(s);
        Assert.That(B(E.CityOverviewOf(s)!, "h03_workshop").QuestNote, Does.Not.Contain("可交：水从哪里来"), "记录没取到不标");
        s.Progress.Flags.Add("claim:d05_07_unique");
        var b = B(E.CityOverviewOf(s)!, "h03_workshop");
        Assert.That(b.CanTurnIn, Is.GreaterThan(0));
        Assert.That(b.QuestNote, Does.Contain("可交：水从哪里来"));
        Assert.That(GameEngine.QuestDone(s, "EXQ05"), Is.False, "标记不代办");
        s.Progress.Flags.Add(GameEngine.QuestFact("EXQ05", "completed"));
        Assert.That(B(E.CityOverviewOf(s)!, "h03_workshop").QuestNote, Does.Not.Contain("水从哪里来"), "已领奖不再标");
    }

    /// <summary>PLAN-Q224-01（B26 核对补上）：经事项办理的委托工作（AC01、CH02）在事项所在楼栋也标“可交”；完成后不再标。</summary>
    [Test]
    public void Q224_TurnIn_ServiceWork()
    {
        var s = Registered("r02_academy");
        DiscoverCity(s, "r02_academy");
        s.Progress.Flags.Add(GameEngine.QuestFact("AC01", "accepted"));
        s.Progress.Flags.UnionWith(Cfg.Quests.List.Single(q => q.Id == "AC01").RequiresAll);
        var b = B(E.CityOverviewOf(s)!, "r05_testing");
        Assert.That(b.CanTurnIn, Is.EqualTo(1));
        Assert.That(b.QuestNote, Does.Contain("可交："));
        Assert.That(GameEngine.QuestDone(s, "AC01"), Is.False);
        s.Progress.Flags.Add(GameEngine.QuestFact("AC01", "completed"));
        Assert.That(B(E.CityOverviewOf(s)!, "r05_testing").CanTurnIn, Is.EqualTo(0));

        var c = Registered("c02_church");
        DiscoverCity(c, "c02_church");
        c.Progress.Flags.Add(GameEngine.QuestFact("CH02", "accepted"));
        c.Progress.Flags.UnionWith(Cfg.Quests.List.Single(q => q.Id == "CH02").RequiresAll);
        var v = E.CityOverviewOf(c)!;
        Assert.That(B(v, "c05_admonition").CanTurnIn + B(v, "c06_archive").CanTurnIn, Is.GreaterThan(0), "CH02 两个事项点至少一处可办");
        Assert.That(B(v, "c06_archive").CanAccept, Is.EqualTo(0), "可接只标任务所在楼栋");
    }

    /// <summary>CH01 这类任务条目工作：缺工作前置时不算“可办理”，满足后才标。</summary>
    [Test]
    public void Q224_TurnIn_WorkRequiresMet()
    {
        var s = Registered("c02_church");
        DiscoverCity(s, "c02_church");
        s.Progress.Flags.Add(GameEngine.QuestFact("CH01", "accepted"));
        s.Progress.Flags.UnionWith(Cfg.Quests.List.Single(q => q.Id == "CH01").RequiresAll);
        s.Progress.Flags.Remove("FA02_record_credit");
        Assert.That(B(E.CityOverviewOf(s)!, "c04_alms").CanTurnIn, Is.EqualTo(0), "工作前置没满足");
        s.Progress.Flags.Add("FA02_record_credit");
        Assert.That(B(E.CityOverviewOf(s)!, "c04_alms").CanTurnIn, Is.EqualTo(1));
    }
}
