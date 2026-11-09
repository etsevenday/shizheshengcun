using Newtonsoft.Json.Linq;
using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 批次 B27c（策划答复 Q269、Q270：文档卡暂停提示与不可读悬停）。规则来源：更新说明 2026-11-06 Q269-Q273 与
/// assets/20261106/q269-q273-display-loot-adjudications.json（documents）；验收 Q269-273-A01、A02。
/// </summary>
public class B27cDocumentCardTests : TestBase
{
    private const string Sign = "village_south_sign";

    private static string Root()
    {
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "web", "Survival.sln"))) dir = dir.Parent;
        Assert.That(dir, Is.Not.Null, "找到仓库根目录");
        return dir!.FullName;
    }

    private static readonly Lazy<JObject> Contract = new(() =>
        JObject.Parse(File.ReadAllText(Path.Combine(Root(), "doc", "策划案", "系统策划总册 v1.0", "assets", "20261106", "q269-q273-display-loot-adjudications.json"))));

    private static string Razor(string name) => File.ReadAllText(Path.Combine(Root(), "web", "Survival.Web", "Components", name));

    private GameState AtNode(string node, uint seed = 12345)
    {
        var s = New(seed);
        E.DebugMapTeleport(s, node);
        if (100 > s.Locations[node].Progress) E.DebugSetProgress(s, node, 100);
        ReadDialogues(s);
        return s;
    }

    private DocumentCardView Doc(GameState s, string id) => E.DocumentsHere(s).Single(d => d.Id == id);

    // ───────────── Q269 文档卡暂停提示 ─────────────

    /// <summary>A01：暂停时查看提示为“行动暂停中”，实际推进仍“行动进行中”；卡面状态行不变，锁不穿透。</summary>
    [Test]
    public void A01_Paused_DocumentReason()
    {
        var docs = (JObject)Contract.Value["documents"]!;
        Assert.That((string)docs["pausedReason"]!, Is.EqualTo(GameEngine.DocumentPausedReason));
        var s = AtNode("village");
        var line = Doc(s, Sign).CardLine;
        Start(s, ActionRequest.Wait(30));
        Assert.That(Doc(s, Sign).ReadBlocked, Is.EqualTo((string)docs["runningReason"]!));
        s.Main!.Status = ActionStatus.Paused;
        Assert.That(Doc(s, Sign).ReadBlocked, Is.EqualTo("行动暂停中"));
        Assert.That(Doc(s, Sign).CardLine, Is.EqualTo(line), "卡面保留自身状态");
        var r = E.ReadDocument(s, Sign);
        Assert.That(r.Ok, Is.False);
        Assert.That(r.Reason, Is.EqualTo("行动暂停中"));
        // 暂停中又有子行动在推进：实际推进，仍写行动进行中
        s.Sub = new ActionState { Type = ActionType.Wait, IsSub = true };
        Assert.That(Doc(s, Sign).ReadBlocked, Is.EqualTo("行动进行中"));
        s.Sub = null;
        E.Resume(s);
        Assert.That(Doc(s, Sign).ReadBlocked, Is.EqualTo("行动进行中"));
    }

    /// <summary>A01：其他优先拒绝照旧——对白锁、告示自身前置优先于暂停；人物卡交谈不在本题范围，保持原句。</summary>
    [Test]
    public void A01_OtherReasons_KeepPriority()
    {
        var s = AtNode("village");
        Assert.That(E.ReadDocument(s, "relief_page_village").Ok, Is.True);
        Assert.That(E.DialogueLocked(s), Is.True);
        s.Main = new ActionState { Type = ActionType.Wait, Status = ActionStatus.Paused };
        Assert.That(Doc(s, Sign).ReadBlocked, Is.EqualTo(GameEngine.DialogueLockedReason));

        var h = AtNode("h03_workshop");
        const string notice = "h03_craft_notice";
        var need = Doc(h, notice).CardLine;
        Assert.That(need, Does.Contain("营区登记"));
        Start(h, ActionRequest.Wait(30));
        h.Main!.Status = ActionStatus.Paused;
        Assert.That(Doc(h, notice).ReadBlocked, Is.EqualTo(need), "告示自身前置仍优先");
        Assert.That(E.ReadDocument(h, notice).Reason, Is.EqualTo(need));

        // 人物卡沿原交谈拒绝（Q269 只改文档卡）
        var v = AtNode("village");
        Start(v, ActionRequest.Wait(30));
        v.Main!.Status = ActionStatus.Paused;
        foreach (var n in E.NpcsHere(v)) Assert.That(n.TalkBlocked, Is.EqualTo("行动进行中"));
    }

    /// <summary>A01：壳里推进中的 busy 仍写“行动进行中”，按钮锁与点卡原因都读 why。</summary>
    [Test]
    public void A01_Card_Wiring()
    {
        var razor = Razor("PrologueCards.razor");
        Assert.That(razor, Does.Contain("var why = busy ? \"行动进行中\" : d.ReadBlocked;"));
        // B39 Q309：按钮改 GateButton（禁用语义 + 可聚焦，原因仍读 why）
        Assert.That(razor, Does.Contain("<GateButton Class=\"loc-btn res-btn\" Name=\"查看\" Blocked=\"@why\" Title=\"查看（0分钟）\""));
    }

    // ───────────── Q270 不可读时卡面悬停 ─────────────

    /// <summary>A02：读不了（缺前置、忙碌、未读对白）时悬停只有“{标题}：{状态}。”；可读时原完整指引保留。</summary>
    [Test]
    public void A02_BlockedTooltip_TitleAndStatusOnly()
    {
        Assert.That((string)Contract.Value["documents"]!["blockedTooltip"]!, Is.EqualTo("{标题}：{状态}。"));
        var razor = Razor("PrologueCards.razor");
        Assert.That(razor, Does.Contain(
            "Tooltip=\"@(why != null ? $\"{d.Title}：{d.Status}。\" : $\"{d.Title}：{d.Status}。点卡片或“查看”阅读（不耗时，不移动、不揭开未知地点；可重读）\")\""));
    }

    /// <summary>A02：三种读不了的原因内核都给 ReadBlocked（悬停据此短写、按钮仍给原因）；可读时为空，查看照常生效。</summary>
    [Test]
    public void A02_ReadBlocked_Drives_Tooltip_ViewKept()
    {
        var h = AtNode("h03_workshop");
        Assert.That(Doc(h, "h03_craft_notice").ReadBlocked, Does.StartWith("需要："), "缺前置");

        var s = AtNode("village");
        Assert.That(Doc(s, Sign).ReadBlocked, Is.Null, "可读");
        Start(s, ActionRequest.Wait(30));
        Assert.That(Doc(s, Sign).ReadBlocked, Is.Not.Null, "忙碌");
        E.Cancel(s);
        Assert.That(E.ReadDocument(s, "relief_page_village").Ok, Is.True, "可读时查看效果保持");
        Assert.That(Doc(s, Sign).ReadBlocked, Is.EqualTo(GameEngine.DialogueLockedReason), "未读对白");
        ReadDialogues(s);
        Assert.That(GameEngine.TaskDone(s, "PRO15"), Is.True);
    }
}
