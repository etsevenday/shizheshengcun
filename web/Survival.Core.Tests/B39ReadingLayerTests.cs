using Newtonsoft.Json.Linq;
using Survival.Core.Config;
using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 批次 B39（R103、R128）：N02 阅读层与阻断展示。
/// 规则出处：05 世界观与叙事/N02 附录 §1「2026-11-09 Q305–Q316 · B39 开工定稿」、N02 §2；00 总则/02 开发方待确认问题 Q305—Q316。
/// 数据：assets/20261107/dialogue-rewrite.json 的 kind / noteKind（Q316 补进 data/config.v0.6.json）。
/// 界面部分（对白层、对话记录、人物卡、文档卡、事项盘）读这里验的内核视图；纯界面行为见开发计划 B39 实施记录。
/// </summary>
public class B39ReadingLayerTests : TestBase
{
    private const string ConfigPath = "data/config.v0.6.json";

    private static readonly string RawJson = File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "config.v0.6.json"));

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

    private GameState RouteKnown()
    {
        var s = New();
        s.Progress.Flags.Add(GameEngine.FactCampRouteKnown);
        ReadDialogues(s);
        return s;
    }

    private static DialogueLine Snap(DialogueConfig d) => new DialogueLine
    {
        LineId = d.Id, Speaker = d.Speaker, Text = d.Text, Note = d.Note, Kind = d.Kind, NoteKind = string.IsNullOrEmpty(d.Note) ? "" : d.NoteKind,
    };

    // ───────────── Q316 配置类型 ─────────────

    /// <summary>Q316：正式发布配置每条对白行都有有效 kind，有附注的都有有效 noteKind，且与 N02 逐字数据一致。</summary>
    [Test]
    public void Q316_PublishedConfig_AllLinesTyped_MatchN02()
    {
        Assert.That(DialogueKinds.ConfigErrors(Cfg, ConfigPath), Is.Empty);
        foreach (var r in N02.Rows)
        {
            var d = Cfg.Story.Dialogues.Single(x => x.Id == (string)r["id"]!);
            Assert.That(d.Kind, Is.EqualTo((string)r["kind"]!), d.Id);
            Assert.That(d.NoteKind, Is.EqualTo((string?)r["noteKind"] ?? ""), d.Id);
        }
        Assert.That(Cfg.Story.Dialogues.Count(d => d.Note.Length > 0), Is.EqualTo(6));
    }

    /// <summary>Q316-A01（前半）：从正式配置分别删去一条的 kind、一条有附注行的 noteKind，测试报告配置路径、组与行 ID；无效值也报。</summary>
    [Test]
    public void Q316_A01_MissingKindOrNoteKind_Reported()
    {
        var o = JObject.Parse(RawJson);
        var rows = (JArray)o["story"]!["dialogues"]!;
        var noKind = (JObject)rows.First(r => (string)r["id"]! == "NTL_0031");
        noKind.Remove("kind");
        var noNoteKind = (JObject)rows.First(r => (string)r["id"]! == "food_water");
        noNoteKind.Remove("noteKind");
        ((JObject)rows.First(r => (string)r["id"]! == "NTL_0001"))["kind"] = "aside";
        var c = ConfigLoader.FromJson(o.ToString());
        var errs = DialogueKinds.ConfigErrors(c, ConfigPath);
        Assert.That(errs, Has.Count.EqualTo(3), string.Join("\n", errs));
        Assert.That(errs, Has.Some.Contains(ConfigPath).And.Contains("PRO15_relief_note").And.Contains("NTL_0031").And.Contains("缺 kind"));
        Assert.That(errs, Has.Some.Contains("opening").And.Contains("food_water").And.Contains("noteKind"));
        Assert.That(errs, Has.Some.Contains("NTL_0001").And.Contains("aside"));
        // 无附注时可以省略 noteKind
        var o2 = JObject.Parse(RawJson);
        foreach (var r in (JArray)o2["story"]!["dialogues"]!) if ((string)r["note"]! == "") ((JObject)r).Remove("noteKind");
        Assert.That(DialogueKinds.ConfigErrors(ConfigLoader.FromJson(o2.ToString()), ConfigPath), Is.Empty);
    }

    /// <summary>
    /// Q316-A01（后半）、DLG-A07：缺类型的旧快照仍可回看；已展示正文与进度保持；有发言人按人物、空发言人按旁白、原附注按普通附注；
    /// 待展示台词按原 ID 首次读取已补齐类型的配置。
    /// </summary>
    [Test]
    public void Q316_A01_OldSnapshotWithoutKinds_Fallback_PendingReadsNewConfig()
    {
        var s = NewStory();
        var d = E.ActiveDialogue(s)!;
        Assert.That(d.Lines.Single().LineId, Is.EqualTo("awakening_1"));
        Assert.That((d.Lines[0].Kind, d.Lines[0].NoteKind), Is.EqualTo(("speech", "narration")), "新展示的句子快照带类型");
        // 再放一组已归档的旧记录：一句旁白、一句带附注的人物句
        s.Story.DialogueLog.Insert(0, new DialogueSession
        {
            Id = 99, EventId = "old_group", Ended = true, Lines =
            {
                new DialogueLine { LineId = "x1", Order = 1, Speaker = "", Text = "风停了。", Side = DialogueSide.Narration },
                new DialogueLine { LineId = "x2", Order = 2, Speaker = "饱饱", Text = "我饿了。", Note = "旧附注", Side = DialogueSide.Right },
            },
        });
        var json = JObject.Parse(SaveSerializer.Serialize(s));
        foreach (var t in json.Descendants().OfType<JProperty>().Where(p => p.Name is "Kind" or "NoteKind" && p.Parent?["LineId"] != null).ToList()) t.Remove();
        var back = SaveSerializer.Deserialize(json.ToString(), Cfg, out _);
        var cur = E.ActiveDialogue(back)!;
        Assert.That(cur.Lines.Single().Text, Is.EqualTo(d.Lines[0].Text), "已展示正文保持");
        Assert.That(cur.Pending, Is.EqualTo(d.Pending), "进度保持");
        Assert.That(cur.Lines[0].Kind, Is.Empty);
        Assert.That(DialogueKinds.Of(cur.Lines[0]), Is.EqualTo(DialogueKinds.Speech), "有发言人按原说话方");
        Assert.That(DialogueKinds.NoteOf(cur.Lines[0]), Is.Empty, "原附注按普通附注");
        Assert.That(DialogueKinds.NoteLabel(cur.Lines[0]), Is.Null);
        var old = back.Story.DialogueLog.Single(x => x.EventId == "old_group");
        Assert.That(E.DialogueHistory(back), Does.Contain(old), "旧记录仍可回看");
        Assert.That(DialogueKinds.Of(old.Lines[0]), Is.EqualTo(DialogueKinds.Narration), "空发言人按中性旁白");
        Assert.That(DialogueKinds.Of(old.Lines[1]), Is.EqualTo(DialogueKinds.Speech));
        Assert.That(DialogueKinds.NoteOf(old.Lines[1]), Is.Empty);
        // 待展示台词按原 ID 首次读取补齐类型的配置
        Assert.That(E.AckDialogue(back, "awakening_1").Ok, Is.True);
        var next = E.CurrentDialogueLine(back)!;
        Assert.That(next.LineId, Is.EqualTo("awakening_2"));
        Assert.That((next.Kind, next.NoteKind), Is.EqualTo(("speech", "narration")));
        Assert.That(E.AckDialogue(back, "awakening_2").Ok, Is.True);
        Assert.That(DialogueKinds.NoteLabel(E.CurrentDialogueLine(back)!), Is.EqualTo("操作提示"), "food_water 的教学附注");
    }

    // ───────────── Q305 非人物行 ─────────────

    /// <summary>
    /// Q305-A01：旁白、文书、结果、状态、工作提示和教学附注都能按类型区分；只有 noteKind=guide 的教学附注有「操作提示」，非人物行不点亮人物头像。
    /// </summary>
    [Test]
    public void Q305_A01_OnlyGuideNoteLabeled_KindsDistinct()
    {
        var lines = Cfg.Story.Dialogues.Select(Snap).ToList();
        foreach (var (l, d) in lines.Zip(Cfg.Story.Dialogues))
        {
            Assert.That(DialogueKinds.Of(l), Is.EqualTo(d.Kind), d.Id);
            Assert.That(DialogueKinds.NoteLabel(l), d.NoteKind == "guide" && d.Note.Length > 0 ? Is.EqualTo("操作提示") : Is.Null, d.Id);
            Assert.That(DialogueKinds.IsPerson(l), Is.EqualTo(d.Kind == "speech"), d.Id);
        }
        var kinds = lines.Select(DialogueKinds.Of).Distinct().ToList();
        Assert.That(kinds, Is.SupersetOf(new[] { "speech", "narration", "document", "guide", "result", "status" }));
        Assert.That(lines.Count(l => DialogueKinds.NoteLabel(l) != null), Is.EqualTo(3), "food_water、tutorial_arrive、tutorial_won 三条");
        // 工作提示行（kind=guide）不是附注，不显示标签
        Assert.That(DialogueKinds.NoteLabel(lines.Single(l => l.LineId == "NTL_0117")), Is.Null);

        // 实际播放：来客抵达首句旁白不点亮头像；路线日志文书行；状态与结果行
        var s = RouteKnown();
        Assert.That(E.OpenRouteLog(s).Ok, Is.True);
        var v = E.DialogueLayer(s)!;
        Assert.That(DialogueKinds.Of(v.Current), Is.EqualTo("document"));
        Assert.That(v.CurrentIsPerson, Is.False);
    }

    // ───────────── Q306 / Q307 标题与对象 ─────────────

    /// <summary>Q306-A01、Q307-A01：有题名用组名；有对象无题名用对象名；纯文书无伪对象、读屏用组名；无题名无对象省略标题、读屏用「当前对白」。</summary>
    [Test]
    public void Q306_Q307_A01_TitlePriority_NoPseudoCounterpart()
    {
        // 有对象、无题名：开场（饱饱）
        var s = NewStory();
        var v = E.DialogueLayer(s)!;
        Assert.That(E.DialogueSetTitle(v.Session.EventId), Is.Null);
        Assert.That((v.Title, v.Counterpart), Is.EqualTo(("饱饱", "饱饱")));
        Assert.That(v.AriaName, Does.Contain("饱饱"));

        // 有题名：EXQ05 接取（罗铮）
        var s2 = Registered("h03_workshop");
        Assert.That(E.AcceptQuest(s2, "EXQ05").Ok, Is.True);
        while (E.DialogueLayer(s2)!.Session.EventId != "EXQ05_offer") E.AckDialogue(s2, E.CurrentDialogueLine(s2)!.LineId);
        v = E.DialogueLayer(s2)!;
        Assert.That((v.Title, v.Counterpart), Is.EqualTo(("水从哪里来：接取", "罗铮")));

        // 纯文书：求医路线日志
        var s3 = RouteKnown();
        Assert.That(E.OpenRouteLog(s3).Ok, Is.True);
        v = E.DialogueLayer(s3)!;
        Assert.That(v.Counterpart, Is.Empty, "右侧留空，不写「对方」「旁白」");
        Assert.That(v.Title, Is.EqualTo("求医路线日志"));
        Assert.That(v.AriaName, Is.EqualTo("求医路线日志"));

        // 只有主角说话的文书组：南口路标
        var won = s3.Story.DialogueLog.Count;
        Assert.That(Cfg.Story.Dialogues.Where(d => d.Event == "village_south_sign").Select(d => d.Speaker), Is.EqualTo(new[] { "", "你" }));

        // 无题名、无对象：只有旁白的组
        var s4 = New();
        ReadDialogues(s4);
        s4.Story.DialogueLog.Add(new DialogueSession
        {
            Id = 500, EventId = "no_title_group",
            Lines = { new DialogueLine { LineId = "n1", Order = 1, Speaker = "", Text = "门开着。", Side = DialogueSide.Narration, Kind = "narration" } },
        });
        v = E.DialogueLayer(s4)!;
        Assert.That(v.Title, Is.Null, "省略可见标题行");
        Assert.That(v.Counterpart, Is.Empty);
        Assert.That(v.AriaName, Is.EqualTo("当前对白"));
        Assert.That(won, Is.GreaterThan(0));
    }

    // ───────────── Q310 控制条状态 ─────────────

    /// <summary>Q310-A01：有行动等待对白时状态位写「阅读期间，{行动名}的进度保持不变。」；无等待行动时省略；读完后续行或仍暂停按原条件。</summary>
    [Test]
    public void Q310_A01_WaitingActionStatusLine()
    {
        var s = RouteKnown();
        Assert.That(E.OpenRouteLog(s).Ok, Is.True);
        Assert.That(E.DialogueLayer(s)!.StatusLine, Is.Null, "没有行动");
        ReadDialogues(s);

        Start(s, ActionRequest.Wait(30));
        var label = s.Main!.Label;
        Assert.That(E.OpenRouteLog(s).Ok, Is.True);
        Assert.That(E.DialogueLayer(s)!.StatusLine, Is.EqualTo($"阅读期间，{label}的进度保持不变。"));
        Assert.That(E.ActionProgress(s)!.DialogueWaiting, Is.True);
        var m = s.GameMinute;
        Assert.That(E.Advance(s).Status, Is.EqualTo(SegmentStatus.Dialogue), "阅读期间不推进");
        Assert.That(s.GameMinute, Is.EqualTo(m));
        ReadDialogues(s);
        Assert.That(E.DialogueWaitingText(s), Is.Null);
        Assert.That(E.Advance(s).Status, Is.EqualTo(SegmentStatus.Running), "读完按原条件续行");

        // 主行动暂停中：读完仍暂停（原规则决定）
        s.Main!.Status = ActionStatus.Paused;
        Assert.That(E.OpenRouteLog(s).Ok, Is.True);
        Assert.That(E.DialogueLayer(s)!.StatusLine, Is.EqualTo($"阅读期间，{label}的进度保持不变。"));
        ReadDialogues(s);
        Assert.That(s.Main!.Status, Is.EqualTo(ActionStatus.Paused));
    }

    // ───────────── Q313 文书脚注 ─────────────

    /// <summary>Q313-A01：多段文书首段脚注「继续」、末段「结束对话」，按钮可用；非文书行与历史回看无脚注；与 Q310 状态句同时出现时各占一位。</summary>
    [Test]
    public void Q313_A01_DocumentFootnote_FirstAndLast()
    {
        var s = RouteKnown();
        Assert.That(E.OpenRouteLog(s).Ok, Is.True);
        var v = E.DialogueLayer(s)!;
        Assert.That(v.HasMore, Is.True);
        Assert.That(v.Footnote, Is.EqualTo("选择「继续」阅读下一段。"));
        E.AckDialogue(s, v.Current.LineId);
        E.AckDialogue(s, E.CurrentDialogueLine(s)!.LineId);
        v = E.DialogueLayer(s)!;
        Assert.That(v.HasMore, Is.False);
        Assert.That(v.Footnote, Is.EqualTo("选择「结束对话」收起文书。"));
        Assert.That(E.AckDialogue(s, v.Current.LineId).Ok, Is.True, "结束对话可用");
        Assert.That(E.DialogueLayer(s), Is.Null);

        // 文书后接主角发言：主角行没有脚注
        var s2 = Registered("h02_square");
        E.DebugMapTeleport(s2, "village");
        ReadDialogues(s2);
        Assert.That(E.ReadDocument(s2, "village_south_sign").Ok, Is.True);
        v = E.DialogueLayer(s2)!;
        Assert.That(DialogueKinds.Of(v.Current), Is.EqualTo("document"));
        Assert.That(v.Footnote, Is.EqualTo("选择「继续」阅读下一段。"));
        E.AckDialogue(s2, v.Current.LineId);
        Assert.That(E.DialogueLayer(s2)!.Footnote, Is.Null, "主角行没有文书脚注");

        // 同时有等待行动：两句都在
        var s3 = RouteKnown();
        Start(s3, ActionRequest.Wait(30));
        Assert.That(E.OpenRouteLog(s3).Ok, Is.True);
        v = E.DialogueLayer(s3)!;
        Assert.That(v.StatusLine, Is.Not.Null);
        Assert.That(v.Footnote, Is.Not.Null);
    }

    // ───────────── Q311 委托题名 ─────────────

    /// <summary>Q311-A01：EXQ05–EXQ08 接取与交付八个组名；实时对白与记录共用；代班沿用组名，发言人按实际。</summary>
    [TestCase("EXQ05", "h03_workshop", "npc_luo_zheng", "水从哪里来", false)]
    [TestCase("EXQ06", "h03_workshop", "npc_luo_zheng", "矿道里的旧工具", true)]
    [TestCase("EXQ07", "h02_square", "npc_xu_an", "最后一张疏散图", false)]
    [TestCase("EXQ08", "h04_infirmary", "npc_chen_zhi", "封存不是永存", true)]
    public void Q311_A01_ExqGroupTitles(string quest, string loc, string npc, string name, bool substitute)
    {
        Assert.That(E.DialogueSetTitle($"{quest}_offer"), Is.EqualTo($"{name}：接取"));
        Assert.That(E.DialogueSetTitle($"{quest}_turnin"), Is.EqualTo($"{name}：交付"));
        Assert.That(E.DialogueSetTitle($"{quest}_offer#3"), Is.EqualTo($"{name}：接取"), "回看的重读组同名");

        var s = Registered(loc);
        if (substitute) E.DebugToggleCityPostAbsent(s);
        Assert.That(s.City.Absent.Contains(npc), Is.EqualTo(substitute));
        Assert.That(E.AcceptQuest(s, quest).Ok, Is.True);
        while (E.DialogueLayer(s) is { } v && v.Session.EventId != $"{quest}_offer") E.AckDialogue(s, v.Current.LineId);
        var live = E.DialogueLayer(s)!;
        Assert.That(live.Title, Is.EqualTo($"{name}：接取"), "实时对白用组名");
        var speaker = live.Current.Speaker;
        Assert.That(speaker, Is.EqualTo(substitute ? Cfg.City.Npcs.Single(n => n.NpcId == npc).DutySpeaker : Cfg.City.Npcs.Single(n => n.NpcId == npc).Name));
        ReadDialogues(s);
        var hist = E.DialogueHistory(s).Single(h => h.EventId == $"{quest}_offer");
        Assert.That(E.DialogueSetTitle(hist.EventId), Is.EqualTo($"{name}：接取"), "记录用同一组名");
        Assert.That(hist.Lines.Single().Speaker, Is.EqualTo(speaker), "历史保留原发言人");
    }

    // ───────────── Q312 交谈接取预告 ─────────────

    /// <summary>Q312-A01：同一岗位两项可接时只预告交谈实际会接取的一项；接取后更新为下一项；全局阻断时省略；只剩回报时没有预告。</summary>
    [Test]
    public void Q312_A01_TalkPreview_MatchesActualAccept()
    {
        var npcId = "npc_luo_zheng";
        var loc = Cfg.City.Npcs.Single(n => n.NpcId == npcId).LocationId;
        var s = Registered(loc);
        NpcCardView Card() => E.NpcsHere(s).Single(n => n.NpcId == npcId);
        var avail = E.QuestsOfNpc(s, npcId).Where(q => q.Status == "available").Select(q => q.Id).ToList();
        Assert.That(avail, Is.SupersetOf(new[] { "EXQ05", "EXQ06" }), "两项可接");
        var preview = Card().TalkPreview;
        Assert.That(preview, Does.StartWith("这次交谈会接取「").And.EndWith("」。"));
        Assert.That(E.TalkNpc(s, npcId).Ok, Is.True);
        var accepted = avail.Where(id => GameEngine.QuestAccepted(s, id)).ToList();
        Assert.That(accepted, Has.Count.EqualTo(1), "一次只接一项");
        Assert.That(preview, Is.EqualTo(GameEngine.TalkOfferPreviewText(E.Quest(accepted[0])!.Title)), "预告就是实际接取的那项");
        Assert.That(Card().TalkPreview, Is.Null, "对白未读完（全局阻断）时省略");
        ReadDialogues(s);
        var other = avail.Single(id => id != accepted[0] && (id == "EXQ05" || id == "EXQ06"));
        Assert.That(Card().TalkPreview, Is.EqualTo(GameEngine.TalkOfferPreviewText(E.Quest(other)!.Title)), "接取后更新");
        Start(s, ActionRequest.Wait(30));
        Assert.That(Card().TalkPreview, Is.Null, "行动进行中省略");
        E.Cancel(s);
        Run(s);
        Assert.That(E.TalkNpc(s, npcId).Ok, Is.True);
        ReadDialogues(s);
        Assert.That(GameEngine.QuestAccepted(s, other), Is.True);
        Assert.That(Card().TalkPreview, Is.Null, "只会回报或闲谈时没有接取预告");
    }

    /// <summary>Q312：代班岗位交谈照接委托，预告同样成立。</summary>
    [Test]
    public void Q312_Substitute_PreviewStillMatches()
    {
        var s = Registered("h03_workshop");
        E.DebugToggleCityPostAbsent(s);
        Assert.That(s.City.Absent, Does.Contain("npc_luo_zheng"));
        var preview = E.NpcsHere(s).Single(n => n.NpcId == "npc_luo_zheng").TalkPreview;
        Assert.That(preview, Is.Not.Null);
        Assert.That(E.TalkNpc(s, "npc_luo_zheng").Ok, Is.True);
        var accepted = new[] { "EXQ05", "EXQ06" }.Where(id => GameEngine.QuestAccepted(s, id)).ToList();
        Assert.That(accepted, Has.Count.EqualTo(1));
        Assert.That(preview, Is.EqualTo(GameEngine.TalkOfferPreviewText(E.Quest(accepted[0])!.Title)));
    }

    // ───────────── Q314 / Q315 对话记录 ─────────────

    /// <summary>Q314-A01：已归档的操作提示在对话记录内可回看（带「操作提示」标签），回看无时间和任务变更。</summary>
    [Test]
    public void Q314_A01_GuideNoteInHistory_ReadOnly()
    {
        var s = NewStory();
        ReadDialogues(s);
        var h = E.DialogueHistory(s).Single(x => x.EventId == "opening");
        var guide = h.Lines.Single(l => l.LineId == "food_water");
        Assert.That(DialogueKinds.NoteLabel(guide), Is.EqualTo("操作提示"));
        Assert.That(guide.Note, Is.EqualTo(Cfg.Story.Dialogues.Single(d => d.Id == "food_water").Note));
        var json = SaveSerializer.Serialize(s);
        _ = E.DialogueHistory(s).SelectMany(x => x.Lines).Select(DialogueKinds.NoteLabel).ToList();
        Assert.That(SaveSerializer.Serialize(s), Is.EqualTo(json), "回看不改存档");
    }

    /// <summary>Q315-A01：空记录无副标题、正文空态只写定稿句；两组已结束记录时副标题「已收录2段 · 回看不消耗时间」。</summary>
    [Test]
    public void Q315_A01_LogSubtitleAndEmpty()
    {
        Assert.That(GameEngine.DialogueLogSubtitle(0), Is.Null);
        Assert.That(GameEngine.DialogueLogEmptyText, Is.EqualTo("结束一段对话后，就能在这里回看。"));
        var s = RouteKnown();
        var baseCount = E.DialogueHistory(s).Count;
        s.Story.DialogueLog.RemoveAll(d => d.Ended);
        Assert.That(E.DialogueHistory(s), Is.Empty);
        Assert.That(GameEngine.DialogueLogSubtitle(E.DialogueHistory(s).Count), Is.Null);
        E.OpenRouteLog(s);
        ReadDialogues(s);
        E.OpenRouteLog(s);
        ReadDialogues(s);
        Assert.That(E.DialogueHistory(s), Has.Count.EqualTo(2));
        Assert.That(GameEngine.DialogueLogSubtitle(E.DialogueHistory(s).Count), Is.EqualTo("已收录2段 · 回看不消耗时间"));
        Assert.That(baseCount, Is.GreaterThanOrEqualTo(0));
    }
}
