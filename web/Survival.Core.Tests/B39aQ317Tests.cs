using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 批次 B39a（R129）：Q317 城市事项盘行动占用统一提示句。
/// 规则出处：05 世界观与叙事/N02 附录 §2「2026-11-10 Q317 · 城市事项盘行动占用提示」；00 总则/02 开发方待确认问题 Q317（选 C）。
/// 验收 Q317-A01：内核视图可判的部分在这里验；按钮旁、读屏名与触发受阻后的反馈位共用同一原因由壳层接线检查（GateButton 本身见 B39ReadingLayerTests）。
/// </summary>
public class B39aQ317Tests : TestBase
{
    private const string Busy = "当前行动未结束，暂时无法办理。";
    private const string Luo = "npc_luo_zheng";
    private const string Sign = "village_south_sign";

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

    private static string LocOf(string npcId) => Cfg.City.Npcs.Single(n => n.NpcId == npcId).LocationId;

    /// <summary>h03 罗铮事项盘里 A01 点名的四个按钮当前的受阻原因：「接取」（委托条目）、「打开制作」「查抄公开资料」「开始训练」（服务）。</summary>
    private (string? Accept, string? Craft, string? Copy, string? Train) Reasons(GameState s)
    {
        var accept = E.CityPanelQuests(s, Luo).First(q => q.AcceptHere);
        var svcs = E.CityServicesOf(s, Luo);
        return (accept.AcceptBlocked,
                svcs.Single(v => v.OpensCraft).Blocked,
                svcs.Single(v => v.Title == "查抄公开资料").Blocked,
                svcs.Single(v => v.StartLabel == "开始训练").Blocked);
    }

    /// <summary>Q317-A01：睡眠进行中打开事项盘，四个按钮的忙碌原因一致为统一句；盘外人物卡「交谈」沿原句；行动结束后恢复原条件。</summary>
    [Test]
    public void Q317_A01_Sleep_FourButtonsSameReason_RestoredAfter()
    {
        Assert.That(GameEngine.CityBusyText, Is.EqualTo(Busy));
        var s = Registered(LocOf(Luo));
        var before = Reasons(s);
        Assert.That(new[] { before.Accept, before.Craft, before.Copy, before.Train }, Has.None.EqualTo(Busy), "空闲时不出忙碌句");
        Assert.That(before.Accept, Is.Null, "空闲可接取");

        Start(s, ActionRequest.Sleep(Cfg.Actions.SleepMinutes.Min()));
        var busy = Reasons(s);
        Assert.That(busy.Accept, Is.EqualTo(Busy), "接取");
        Assert.That(busy.Craft, Is.EqualTo(Busy), "打开制作");
        Assert.That(busy.Copy, Is.EqualTo(Busy), "查抄公开资料");
        Assert.That(busy.Train, Is.EqualTo(Busy), "开始训练");

        // 盘外入口沿现行规则：人物卡「交谈」、交谈与引擎接取（人物卡交谈的同行邀约也走它）仍是“行动进行中”
        Assert.That(E.NpcsHere(s).Single(n => n.NpcId == Luo).TalkBlocked, Is.EqualTo("行动进行中"));
        Assert.That(E.TalkNpc(s, Luo).Reason, Is.EqualTo("行动进行中"));
        var qid = E.CityPanelQuests(s, Luo).First(q => q.AcceptHere).Id;
        Assert.That(E.AcceptQuest(s, qid).Reason, Is.EqualTo("行动进行中"), "引擎原因不改，事项盘入口经 CityPanelReason 换句");
        Assert.That(GameEngine.CityPanelReason(E.AcceptQuest(s, qid).Reason), Is.EqualTo(Busy));
        Assert.That(E.QuestsOfNpc(s, Luo).First(q => q.Id == qid).AcceptBlocked, Is.EqualTo("行动进行中"), "任务册等盘外视图不换句");

        // 主行动暂停：事项盘原校验没有专用暂停分支，仍是行动占用
        s.Main!.Status = ActionStatus.Paused;
        Assert.That(Reasons(s), Is.EqualTo((Busy, Busy, Busy, Busy)));
        E.Resume(s);

        Assert.That(E.Cancel(s).Ok, Is.True);
        Run(s);
        Assert.That(s.Main, Is.Null);
        Assert.That(Reasons(s), Is.EqualTo(before), "行动结束后按现有条件恢复");
    }

    /// <summary>Q317-A01：委托工作按钮（同行邀约“选择登记”、任务条目里的工作）行动占用时同句。</summary>
    [Test]
    public void Q317_A01_QuestWorkButton_SameReason()
    {
        var s = Registered(LocOf(Luo));
        Start(s, ActionRequest.Wait(30));
        foreach (var npc in Cfg.City.Npcs.Select(n => n.NpcId))
            foreach (var q in E.CityPanelQuests(s, npc).Where(q => q.WorkLabel != null && q.WorkBlocked != null))
                Assert.That(q.WorkBlocked, Is.Not.EqualTo("行动进行中"), $"{q.Id} 事项盘内不再出现“行动进行中”");
        // 直接核对映射：原校验的“行动进行中”换句，其他原因与优先级原样
        Assert.That(GameEngine.CityPanelReason("行动进行中"), Is.EqualTo(Busy));
        Assert.That(GameEngine.CityPanelReason(null), Is.Null);
        foreach (var other in new[] { "先处理眼前的敌踪", GameEngine.DocumentPausedReason, "角色已死亡", "须实际到达营区工坊" })
            Assert.That(GameEngine.CityPanelReason(other), Is.EqualTo(other));
    }

    /// <summary>Q317-A01：事项盘内鉴定所、收容所子盘的原行动占用分支同句。</summary>
    [Test]
    public void Q317_A01_AppraisalAndShelterSubPanels()
    {
        var a = Registered(LocOf("npc_tao_cen"));
        Start(a, ActionRequest.Wait(30));
        Assert.That(E.AppraisalPreview(a).Blocked, Is.EqualTo(Busy), "鉴定所");

        var r = Registered(LocOf("npc_du_sui"));
        Start(r, ActionRequest.Wait(30));
        Assert.That(E.RecruitPreview(r).Blocked, Is.EqualTo(Busy), "收容所");
        Assert.That(E.RecruitServiceBlocked(r, "登记"), Is.EqualTo(Busy));
        E.Cancel(r);
        Run(r);
        Assert.That(E.RecruitServiceBlocked(r, "登记"), Is.Not.EqualTo(Busy), "行动结束后恢复");
    }

    /// <summary>Q317-A01：盘外文档卡「查看」的原专用暂停分支仍写“行动暂停中”，推进中仍“行动进行中”。</summary>
    [Test]
    public void Q317_A01_OutsidePanel_DocumentKeepsOwnReasons()
    {
        var s = New();
        E.DebugMapTeleport(s, "village");
        if (100 > s.Locations["village"].Progress) E.DebugSetProgress(s, "village", 100);
        ReadDialogues(s);
        DocumentCardView Doc() => E.DocumentsHere(s).Single(d => d.Id == Sign);
        Start(s, ActionRequest.Wait(30));
        Assert.That(Doc().ReadBlocked, Is.EqualTo("行动进行中"));
        s.Main!.Status = ActionStatus.Paused;
        Assert.That(Doc().ReadBlocked, Is.EqualTo("行动暂停中"));
    }

    // ───────────── 壳层接线（按钮旁、读屏名、触发受阻反馈位共用同一 why） ─────────────

    private static string Root()
    {
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "web", "Survival.sln"))) dir = dir.Parent;
        Assert.That(dir, Is.Not.Null, "找到仓库根目录");
        return dir!.FullName;
    }

    private static string Web(params string[] parts) => File.ReadAllText(Path.Combine(new[] { Root(), "web", "Survival.Web" }.Concat(parts).ToArray()));

    /// <summary>Q317-A01：事项盘取换句后的任务视图；三处界面忙碌提示都用统一句；事项盘入口的失败反馈同句；盘外卡片保持原句。</summary>
    [Test]
    public void Q317_A01_ShellWiring()
    {
        var overlay = Web("Components", "CityServiceOverlay.razor");
        Assert.That(overlay, Does.Contain("var quests = e.CityPanelQuests(s, npcId);"));
        Assert.That(overlay, Does.Not.Contain("QuestsOfNpc("));
        Assert.That(overlay, Does.Contain("var qbusy = G.Running ? GameEngine.CityBusyText : null;"));
        Assert.That(overlay, Does.Contain("?? (G.Running ? GameEngine.CityBusyText : null);"));
        Assert.That(overlay, Does.Contain("<GateButton Class=\"primary\" Name=\"接取（0分钟）\" Blocked=\"@why\""));
        Assert.That(overlay, Does.Contain("<GateButton Class=\"primary\" Name=\"@startLabel\" Blocked=\"@why\""));
        Assert.That(overlay, Does.Not.Contain("行动进行中"));
        Assert.That(Web("Components", "AppraisalPanel.razor"), Does.Contain("var busy = G.Running ? GameEngine.CityBusyText : null;"));
        var recruit = Web("Components", "RecruitPanel.razor");
        Assert.That(recruit, Does.Contain("var busy = G.Running ? GameEngine.CityBusyText : null;"));
        Assert.That(recruit, Does.Not.Contain("\"行动进行中\""));
        var session = Web("Services", "GameSession.cs");
        Assert.That(session, Does.Contain("CityPanelResult(E.AcceptQuest(S, questId"));
        Assert.That(session, Does.Contain("CityPanelResult(E.StartQuestWork(S, questId"));
        // 盘外：人物卡「交谈」、文档卡「查看」沿原句
        var cards = Web("Components", "PrologueCards.razor");
        Assert.That(cards, Does.Contain("var why = busy ? \"行动进行中\" : n.TalkBlocked;"));
        Assert.That(cards, Does.Contain("var why = busy ? \"行动进行中\" : d.ReadBlocked;"));
    }
}
