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

    /// <summary>
    /// 定稿句与合同逐字一致。B35c 改：readOnly 由 Q913 A 取代为“在城市层只能查看；关闭地图后再操作。”（S14 2026-11-17 B35 次要节 Q913），
    /// confirm 多段句按 Q914 A 删除、arrival“继续前往”句按 Q804 删除，合同里这两句不再有对应输出；legacyReveal、arrived 仍逐字。
    /// </summary>
    [Test]
    public void Q223_LinesVerbatim_AfterB35c()
    {
        Assert.That(CityCopy.ReadOnly, Is.EqualTo("在城市层只能查看；关闭地图后再操作。"), "Q913 A 原句");
        Assert.That(T("readOnly"), Is.Not.EqualTo(CityCopy.ReadOnly), "旧只读句（要求点“返回当前地点”）已撤");
        Assert.That(T("legacyReveal"), Is.EqualTo(CityCopy.LegacyReveal));
        Assert.That(CityCopy.Arrived("鉴定所"), Is.EqualTo(T("arrived", ("最终目标", "鉴定所"))));
        Assert.That(CityCopy.Arrived("鉴定所"), Is.EqualTo("已到达鉴定所。"), "Q804“已到达{地点}。”");
        Assert.That(typeof(CityCopy).GetMethod("Confirm"), Is.Null, "Q914 A：删“共{n}段。本次先走…”且不加替句");
        Assert.That(typeof(CityCopy).GetMethod("Arrival"), Is.Null, "Q804：删“继续前往{目标}…还有{n}段”");
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

    /// <summary>
    /// B35-Q804-01（B35c 改写，原 PLAN-Q223-01 多段“还剩几段”提示按 Q804 删除）：城市层出发到达这一段写“已到达{地点}。”，
    /// 不再写继续前往原远处目标或剩余段数；还在路上、没到这一段的终点都不写。
    /// </summary>
    [Test]
    public void Q804_ArrivedOnly_NoContinueReminder()
    {
        var s = Registered("h03_workshop");
        DiscoverCity(s, "h02_square");
        Assert.That(E.CityArrivalNote(s, "h02_square"), Is.Null, "还没出发不提示");
        Assert.That(E.StartMapDeparture(s, "h02_square", "req-1").Ok, Is.True);
        Assert.That(E.CityArrivalNote(s, "h02_square"), Is.Null, "路上不提示");
        Run(s);
        ReadDialogues(s);
        Assert.That(s.CurrentLocation, Is.EqualTo("h02_square"));
        var note = E.CityArrivalNote(s, "h02_square");
        Assert.That(note, Is.EqualTo("已到达营区广场。"), "Q804 原句，地点名为 config 里 h02_square 的名称");
        Assert.That(note, Does.Not.Contain("继续前往").And.Not.Contain("段"));
        Assert.That(E.CityArrivalNote(s, "h05_appraisal"), Is.Null, "没到远处目标不写，也不提醒继续");
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
