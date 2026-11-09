using Newtonsoft.Json.Linq;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 批次 B38（R101、R102 配置字段部分）：N02 对白与玩家可见文字重写的逐字替换。
/// 规则出处：05 世界观与叙事/N02 对白与玩家可见文字重写.md §2「旧档与实际发言者」、§4 验收 DLG-A07、DLG-A08；
/// 数据 assets/20261107/dialogue-rewrite.json（dialogueRows 383 条、configCopy 95 项）。kind/noteKind 呈现分类属 B39，本批不验。
/// </summary>
public class B38N02RewriteTests : TestBase
{
    private static JObject Row(string id) => N02.Rows.Single(r => (string)r["id"]! == id);

    private static readonly JObject Raw =
        JObject.Parse(File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "config.v0.6.json")));

    /// <summary>DLG-A08：383 条按 id 原位换 text（6 条连 note）；id 集合、条目顺序、事件分组与 speaker 与 N02 记录的现行配置一致。</summary>
    [Test]
    public void DLG_A08_DialogueRows_TextAndNote_StableKeysOrderSpeaker()
    {
        var rows = N02.Rows.ToList();
        Assert.That(rows, Has.Count.EqualTo(383));
        Assert.That(Cfg.Story.Dialogues.Select(d => d.Id), Is.EqualTo(rows.Select(r => (string)r["id"]!)), "条目与顺序不变");
        var notes = 0;
        foreach (var r in rows)
        {
            var id = (string)r["id"]!;
            var d = Cfg.Story.Dialogues.Single(x => x.Id == id);
            Assert.That((d.Event, d.Speaker), Is.EqualTo(((string)r["eventId"]!, (string)r["speaker"]!)), id + "：事件与发言者不变");
            Assert.That(d.Text, Is.EqualTo((string)r["text"]!), id + "：正文为 N02 新稿");
            if (r["note"] != null)
            {
                notes++;
                Assert.That(d.Note, Is.EqualTo((string)r["note"]!), id + "：附注为 N02 新稿");
            }
        }
        Assert.That(notes, Is.EqualTo(6));
        // 每个事件里的台词顺序不变
        foreach (var g in rows.GroupBy(r => (string)r["eventId"]!))
            Assert.That(Cfg.Story.Dialogues.Where(d => d.Event == g.Key).Select(d => d.Id), Is.EqualTo(g.Select(r => (string)r["id"]!)), g.Key);
        // 原始 JSON 里也没有新增呈现字段（kind/noteKind 属 B39）
        foreach (var d in (JArray)Raw["story"]!["dialogues"]!)
            Assert.That(((JObject)d).Properties().Select(p => p.Name), Is.SubsetOf(new[] { "id", "eventId", "speaker", "text", "note" }));
    }

    private static JToken Section(string section, string? id) => section switch
    {
        "locations" => Raw["locations"]!.Single(x => (string)x["id"]! == id),
        "city.npcs" => Raw["city"]!["npcs"]!.Single(x => (string)x["npcId"]! == id),
        "quests.list" => Raw["quests"]!["list"]!.Single(x => (string)x["id"]! == id),
        "map.factLabels" => Raw["map"]!["factLabels"]!,
        "prologue.travelStep" => Raw["prologue"]!["travelStep"]!,
        "progress" => Raw["progress"]!,
        _ => throw new AssertionException("未知 section：" + section),
    };

    /// <summary>DLG-A08：95 项配置字段按 section/id/field 等于 N02 replacement，不再是 old。</summary>
    [Test]
    public void DLG_A08_ConfigCopy_95Fields()
    {
        var items = N02.ConfigCopy.ToList();
        Assert.That(items, Has.Count.EqualTo(95));
        foreach (var e in items)
        {
            var section = (string)e["section"]!;
            var id = (string?)e["id"];
            var field = (string)e["field"]!;
            JToken t = Section(section, id);
            foreach (var part in field.Split('.'))
                t = t is JArray a ? a[int.Parse(part)] : t[part]!;
            Assert.That((string)t!, Is.EqualTo((string)e["replacement"]!), $"{section}/{id}/{field}");
            Assert.That((string)t!, Is.Not.EqualTo((string)e["old"]!));
        }
        // 读入后的强类型配置同样是新稿（抽查每类字段）
        Assert.That(Cfg.Locations.Single(l => l.Id == "m01_road").Clue, Is.EqualTo(N02.Field("locations", "m01_road", "clue", "路肩残留着车辙和碎玻璃；路面开阔，停留时仍需留意两侧。")));
        Assert.That(Cfg.Prologue.TravelStep.KnownOpen, Is.EqualTo("下一段路已开通，前往{实际下一相邻地点名}。"));
        Assert.That(Cfg.Quests.List.Single(q => q.Id == "EXQ05").StepOffer, Is.EqualTo("到营区工坊找罗铮，了解水厂调查。"));
        Assert.That(Cfg.City.Npcs.Single(n => n.NpcId == "npc_yin_ke").ServiceOfferLines["academy_observation_record"], Is.EqualTo("只翻已经有的记录。先前同意看到哪里，就看到哪里。"));
    }

    /// <summary>
    /// DLG-A07：读档后已归档历史保留保存时的正文与发言者；在播对白保留已展示快照与进度；仍在队列的台词按原 ID 在首次展示时取新版正文；
    /// 回看只读，不推进时间、不改存档。
    /// </summary>
    [Test]
    public void DLG_A07_Snapshots_Kept_QueuedLines_NewText_ReviewReadOnly()
    {
        var s = NewStory();
        var live = s.Story.DialogueLog.Single(x => !x.Ended);
        Assert.That(live.Lines.Select(l => l.LineId), Is.EqualTo(new[] { "awakening_1" }));
        Assert.That(live.Pending, Is.EqualTo(new[] { "awakening_2", "food_water" }));
        // 模拟 B38 以前的存档：首句按旧稿展示过（快照），另有一段旧稿归档记录
        var a1 = Row("awakening_1");
        live.Lines[0].Text = (string)a1["oldText"]!;
        live.Lines[0].Note = (string)a1["oldNote"]!;
        var ch01 = Row("NTL_0139");
        var old = new DialogueSession { Id = s.Story.NextDialogueSessionId++, EventId = "CH01_offer#old", Ended = true, Minute = 600, LocationId = "c04_alms" };
        old.Lines.Add(new DialogueLine { LineId = "NTL_0139", Order = 1, Speaker = (string)ch01["speaker"]!, Text = (string)ch01["oldText"]!, Side = DialogueSide.Right });
        s.Story.DialogueLog.Add(old);

        var back = SaveSerializer.Deserialize(SaveSerializer.Serialize(s), Cfg, out _);
        var cur = E.CurrentDialogueLine(back)!;
        Assert.That((cur.LineId, cur.Text, cur.Note), Is.EqualTo(("awakening_1", (string)a1["oldText"]!, (string)a1["oldNote"]!)), "在播快照不倒改、不重启");
        Assert.That(E.ActiveDialogue(back)!.Pending, Is.EqualTo(new[] { "awakening_2", "food_water" }), "进度保留");

        // 回看只读
        var json = SaveSerializer.Serialize(back);
        var minute = back.GameMinute;
        var hist = E.DialogueHistory(back).Single(h => h.EventId == "CH01_offer#old");
        Assert.That((hist.Lines[0].Speaker, hist.Lines[0].Text), Is.EqualTo(((string)ch01["speaker"]!, (string)ch01["oldText"]!)), "归档按保存时的正文与发言者");
        foreach (var h in E.DialogueHistory(back)) E.DialogueSetTitle(h.EventId);
        Assert.That(SaveSerializer.Serialize(back), Is.EqualTo(json), "回看不改存档");

        // 继续：队列里的台词首次展示时取新版
        Assert.That(E.AckDialogue(back, "awakening_1").Ok, Is.True);
        cur = E.CurrentDialogueLine(back)!;
        Assert.That((cur.LineId, cur.Text, cur.Note), Is.EqualTo(("awakening_2", N02.Line("awakening_2"), (string)Row("awakening_2")["note"]!)));
        Assert.That(E.AckDialogue(back, "awakening_2").Ok, Is.True);
        Assert.That(E.CurrentDialogueLine(back)!.Text, Is.EqualTo(N02.Line("food_water")));
        Assert.That(E.AckDialogue(back, "food_water").Ok, Is.True);
        Assert.That(back.GameMinute, Is.EqualTo(minute), "对白不推进时间");

        var opening = E.DialogueHistory(back).Single(h => h.EventId == "opening");
        Assert.That(opening.Lines.Select(l => l.Text), Is.EqualTo(new[] { (string)a1["oldText"]!, N02.Line("awakening_2"), N02.Line("food_water") }),
            "同一次对话里已展示的旧句保留，后续句为新稿");
        Assert.That(E.DialogueHistory(back).Single(h => h.EventId == "CH01_offer#old").Lines[0].Text, Is.EqualTo((string)ch01["oldText"]!));
    }
}
