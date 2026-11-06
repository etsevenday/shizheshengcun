using Newtonsoft.Json.Linq;
using Survival.Core.Config;
using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 批次 B27a（R42 Q252、R43 Q253、R44 Q254）。规则来源：assets/20261105/ui-card-feeding-q252-257.json 的 resourceCompact、knowledgeShortCopy、randomYield；
/// 验收 Q252-257-A01—A04。内核部分在这里断言，卡面接线和 CSS 用文本断言。
/// </summary>
public class B27aResourceCardTests : TestBase
{
    private const string ColdBox = "village_cold_box";
    private const string Quarry = "w03_quarry_blueprints";

    private static string Root()
    {
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "web", "Survival.sln"))) dir = dir.Parent;
        Assert.That(dir, Is.Not.Null, "找到仓库根目录");
        return dir!.FullName;
    }

    private static readonly Lazy<JObject> Contract = new(() =>
        JObject.Parse(File.ReadAllText(Path.Combine(Root(), "doc", "策划案", "系统策划总册 v1.0", "assets", "20261105", "ui-card-feeding-q252-257.json"))));

    private GameState AtNode(string node, int progress = 100)
    {
        var s = New();
        E.DebugMapTeleport(s, node);
        if (progress > s.Locations[node].Progress) E.DebugSetProgress(s, node, progress);
        return s;
    }

    /// <summary>当前位置有已发现的冷藏箱、没有全局锁的档。</summary>
    private GameState Village()
    {
        var s = AtNode("village");
        ReadDialogues(s);
        Assert.That(s.Resources[ColdBox].Discovered, Is.True);
        Assert.That(E.CanStart(s, ActionRequest.Gather(ColdBox)), Is.Null);
        return s;
    }

    /// <summary>开局对白没读完的档，当前地点有一个已发现的普通资源。</summary>
    private GameState DialogueOpen(out ResourceConfig r)
    {
        var s = NewStory();
        E.DebugMapTeleport(s, "h02_square");
        Assert.That(E.DialogueLocked(s), Is.True);
        r = E.Resource("h02_square_water");
        s.Resources[r.NodeId].Discovered = true;
        return s;
    }

    // ───────────── R42 Q252 ─────────────

    /// <summary>A01：行动进行中、暂停中都不写进卡面；原动作锁还在。</summary>
    [Test]
    public void Q252_GlobalAction_NotOnCard_LockKept()
    {
        var s = Village();
        var req = ActionRequest.Gather(ColdBox);
        Start(s, ActionRequest.Wait(30));
        Assert.That(E.IsBusy(s), Is.True);
        Assert.That(E.CanStart(s, req), Is.Not.Null, "在办时动作仍被拒");
        Assert.That(E.ResourceCardReason(s, req, true), Is.Null);
        s.Main!.Status = ActionStatus.Paused;
        Assert.That(E.CanStart(s, req), Is.Not.Null);
        Assert.That(E.ResourceCardReason(s, req, true), Is.Null);
    }

    /// <summary>A01：请先读完对话不写进卡面；原动作锁还在。</summary>
    [Test]
    public void Q252_DialogueLock_NotOnCard_LockKept()
    {
        var s = DialogueOpen(out var r);
        var req = ActionRequest.Gather(r.NodeId);
        Assert.That(E.CanStart(s, req), Is.EqualTo(GameEngine.DialogueLockedReason));
        Assert.That(E.ResourceCardReason(s, req, false), Is.Null);
    }

    /// <summary>A01：资源自身状态不被全局状态盖住，闲着、在办、对白里写的是同一句，主图不会跟着行动一开一关。</summary>
    [Test]
    public void Q252_SelfState_SameLine_IdleBusyAndDialogue()
    {
        var s = Village();
        s.Resources[ColdBox].Available = 0;
        var req = ActionRequest.Gather(ColdBox);
        var idle = E.CanStart(s, req);
        Assert.That(idle, Does.Contain("搜空"));
        Assert.That(E.ResourceCardReason(s, req, false), Is.EqualTo(idle));
        Assert.That(E.ResourceCardReason(s, req, true), Is.EqualTo(idle), "壳的短暂运行态也一样");
        Start(s, ActionRequest.Wait(30));
        Assert.That(E.ResourceCardReason(s, req, true), Is.EqualTo(idle));

        var d = DialogueOpen(out var r);
        d.Resources[r.NodeId].Available = 0;
        Assert.That(E.ResourceCardReason(d, ActionRequest.Gather(r.NodeId), false), Is.Not.Null.And.Not.EqualTo(GameEngine.DialogueLockedReason));
    }

    /// <summary>试玩修复：本卡在办时自己的预留算作还在，不写已搜空，主图不消失；暂停也一样。锁和库存账不动，完成后才是真的空。</summary>
    [Test]
    public void Q252_OwnReservation_NotEmpty_WhileGathering()
    {
        const string locker = "village_weapon_locker";
        var s = Village();
        var req = ActionRequest.Gather(locker);
        Assert.That(s.Resources[locker].Available, Is.EqualTo(1));
        Start(s, req);
        Assert.That(s.Resources[locker].Available, Is.EqualTo(0));
        Assert.That(s.Resources[locker].Reserved, Is.EqualTo(1));
        Assert.That(E.CanStart(s, req), Is.Not.Null, "动作锁不变");
        Assert.That(E.ResourceCardReason(s, req, true), Is.Null);
        s.Main!.Status = ActionStatus.Paused;
        Assert.That(E.ResourceCardReason(s, req, true), Is.Null);
        Assert.That(s.Resources[locker].Available, Is.EqualTo(0), "读卡面不改账");
        Assert.That(s.Resources[locker].Reserved, Is.EqualTo(1));
        s.Main!.Status = ActionStatus.Running;
        Run(s);
        Assert.That(s.Main, Is.Null);
        Assert.That(E.ResourceCardReason(s, req, false), Does.Contain("搜空"), "搜完才写空");
    }

    /// <summary>试玩修复：在办占的是这张卡，别的已空卡照写空；已空的卡不因别人的预留变成“还在”。</summary>
    [Test]
    public void Q252_OtherEmptyCard_StillEmpty_WhileGathering()
    {
        var s = Village();
        s.Resources[ColdBox].Available = 0;
        var empty = E.CanStart(s, ActionRequest.Gather(ColdBox));
        Start(s, ActionRequest.Gather("village_weapon_locker"));
        Assert.That(E.ResourceCardReason(s, ActionRequest.Gather(ColdBox), true), Is.EqualTo(empty));
    }

    /// <summary>试玩修复：补充点抽走最后一批时不提前写库存为0；库存本来就是 0 的照写，完成后才写。</summary>
    [Test]
    public void Q252_RefillPoint_LastBatch_NotZero_UntilDone()
    {
        const string node = "h02_square_water";
        var r = E.Resource(node);
        Assert.That(r.Refills, Is.True);
        var s = AtNode("h02_square");
        ReadDialogues(s);
        s.Resources[node].Available = r.Outputs.Values.First();
        var req = ActionRequest.Gather(node);
        Start(s, req);
        Assert.That(s.Resources[node].Available, Is.EqualTo(0), "最后一批已被预留");
        Assert.That(E.ResourceCardReason(s, req, true), Is.Null);
        Run(s);
        var zero = $"库存为0，下次00:00补充{r.DailyRefill}";
        Assert.That(E.ResourceCardReason(s, req, false), Is.EqualTo(zero));
        Start(s, ActionRequest.Wait(30));
        Assert.That(E.ResourceCardReason(s, req, true), Is.EqualTo(zero), "别的行动在办时真空照写");
    }

    /// <summary>已领取也算资源自身状态，在办时照样写。</summary>
    [Test]
    public void Q252_Claimed_StaysOnCard_WhileBusy()
    {
        var s = AtNode("w03_quarry");
        ReadDialogues(s);
        var req = ActionRequest.Gather(Quarry);
        Start(s, req);
        Run(s);
        var claimed = E.CanStart(s, req);
        Assert.That(claimed, Does.Contain("已领取"));
        Start(s, ActionRequest.Wait(30));
        Assert.That(E.ResourceCardReason(s, req, true), Is.EqualTo(claimed));
    }

    /// <summary>其他 CanStart 原因维持现状：闲着时卡面就是 CanStart 那一句。</summary>
    [Test]
    public void Q252_OtherReasons_Unchanged_WhenIdle()
    {
        var s = Village();
        var ok = ActionRequest.Gather(ColdBox);
        Assert.That(E.ResourceCardReason(s, ok, false), Is.Null);
        var away = ActionRequest.Gather(Quarry);
        Assert.That(E.CanStart(s, away), Is.Not.Null);
        Assert.That(E.ResourceCardReason(s, away, false), Is.EqualTo(E.CanStart(s, away)));
    }

    /// <summary>卡面接线：第二行读 ResourceCardReason，按钮的 disabled 和 title 仍读原来的 why；主图让位的 CSS 只在 ≤800 档。</summary>
    [Test]
    public void Q252_Card_Wiring_And_Css()
    {
        var razor = File.ReadAllText(Path.Combine(Root(), "web", "Survival.Web", "Components", "LocationStrip.razor"));
        Assert.That(razor, Does.Contain("@if (cardWhy != null) { <div class=\"oc-line warn-line res-why\" title=\"@cardWhy\">@cardWhy</div> }"));
        Assert.That(razor, Does.Not.Contain("res-why\" title=\"@why\""));
        Assert.That(razor, Does.Contain("<button type=\"button\" class=\"loc-btn res-btn\" disabled=\"@(why != null)\" title=\"@(why ?? \"单击直接开始；右侧显示预测\")\""));
        Assert.That(razor, Does.Contain("var why = busy ? (s.Main?.Status == ActionStatus.Paused ? \"行动暂停中\" : \"行动进行中\") : e.CanStart(s, req);"));

        var css = File.ReadAllLines(Path.Combine(Root(), "web", "Survival.Web", "wwwroot", "css", "fourrows.css"));
        var hide = Array.FindIndex(css, l => l.Contains(".desk-group.resources .res-card:has(.oc-line + .oc-line) .oc-art { display: none; }"));
        Assert.That(hide, Is.GreaterThan(0));
        var media = Array.FindLastIndex(css, hide, l => l.StartsWith("@media"));
        Assert.That(css[media], Does.Contain("max-height: 800px"), "801 以上不隐藏主图");
    }

    // ───────────── R43 Q253 ─────────────

    /// <summary>A02：两种知识正好是合同那 23 配方，未掌握短句逐字定稿；完整原因（来源）不动，学会后不再有锁定原因。</summary>
    [Test]
    public void Q253_TwentyThreeRecipes_ShortCopy()
    {
        var c = (JObject)Contract.Value["knowledgeShortCopy"]!;
        var ids = c["knowledgeIds"]!.Select(x => (string)x!).ToList();
        var unlearned = (string)c["unlearned"]!;
        Assert.That(unlearned, Is.EqualTo("未掌握：查看告示或说明"));
        var recipes = Cfg.Recipes.Where(r => r.Knowledge != null && ids.Contains(r.Knowledge)).ToList();
        Assert.That(recipes.Select(r => r.Id), Is.EquivalentTo(c["recipeIds"]!.Select(x => (string)x!)));
        Assert.That(recipes, Has.Count.EqualTo(23));

        var s = AtNode("h03_workshop");
        foreach (var r in recipes)
        {
            var why = E.RecipeUnknownReason(s, r);
            var k = Cfg.Knowledge.Single(x => x.Id == r.Knowledge);
            Assert.That(why, Is.EqualTo($"还不知道{k.Name}（{k.Source}）"), r.Id);
            Assert.That(E.RecipeLockShort(r, why!), Is.EqualTo(unlearned), r.Id);
        }
        s.Progress.Flags.UnionWith(new[] { "h03_notice_read", "h03_ceramic_notice_read" });
        foreach (var r in recipes) Assert.That(E.RecipeUnknownReason(s, r), Is.Null, r.Id);
    }

    /// <summary>A02：其他知识的短句不泛改；同一配方上的其他锁定原因也不变。</summary>
    [Test]
    public void Q253_OtherKnowledge_Unchanged()
    {
        var s = New();
        var two = new[] { "expedition_basics", "ceramic_methods" };
        foreach (var r in Cfg.Recipes.Where(r => r.Knowledge != null && !two.Contains(r.Knowledge)))
        {
            var why = E.RecipeUnknownReason(s, r);
            if (why == null) continue;
            var k = Cfg.Knowledge.Single(x => x.Id == r.Knowledge);
            var expected = k.Blueprint || Cfg.City.Registration.LegacyKnowledge.Contains(k.Id) ? "未掌握：需要蓝图" : "未掌握：营区登记后学会";
            Assert.That(E.RecipeLockShort(r, why), Is.EqualTo(expected), r.Id);
        }
        var shell = Cfg.Recipes.First(r => r.Knowledge == two[0]);
        Assert.That(E.RecipeLockShort(shell, "需要火堆：先建"), Is.EqualTo("需要火堆"));
        Assert.That(E.RecipeLockShort(shell, "材料不足"), Is.EqualTo("材料不足"));
    }

    // ───────────── R44 Q254 ─────────────

    /// <summary>A03：冷藏箱逐字定稿，物品、数量、概率都来自配置且和合同一致。</summary>
    [Test]
    public void Q254_ColdBox_Copy()
    {
        var c = (JObject)Contract.Value["randomYield"]!["coldBox"]!;
        var box = E.Resource(ColdBox);
        Assert.That(E.NoOutputYieldLine(box), Is.EqualTo("有限 · 随机其一：抗病毒前体×1（20%）；草药绷带×2（80%）"));
        Assert.That(E.NoOutputYieldLine(box), Is.EqualTo((string)c["copy"]!));
        Assert.That(E.ItemName("bandage"), Is.EqualTo("草药绷带"), "这一句用物品全名（Q258）");
        Assert.That(box.ExclusiveLoot!.Select(x => (x.ItemId, x.Count, x.Weight)),
            Is.EqualTo(c["entries"]!.Select(x => ((string)x["itemId"]!, (int)x["count"]!, (int)x["percent"]!))));
    }

    /// <summary>A04：图册逐字定稿，蓝图名按 claim 事实从配置读，顺序与定稿一致；不造物品或概率。</summary>
    [Test]
    public void Q254_QuarryBlueprints_Copy()
    {
        var c = (JObject)Contract.Value["randomYield"]!["knowledgeOnly"]!;
        var r = E.Resource(Quarry);
        Assert.That(E.NoOutputYieldLine(r), Is.EqualTo("本局一次 · 做法：倒刺短矛蓝图、猎手弓蓝图、侦察护具蓝图（领取时授予，已掌握不重复授予）"));
        Assert.That(E.NoOutputYieldLine(r), Is.EqualTo((string)c["copy"]!));
        Assert.That(Cfg.Knowledge.Where(k => k.GrantedByFacts.Contains("claim:" + Quarry)).Select(k => k.Id), Is.EqualTo(c["knowledgeIds"]!.Select(x => (string)x!)));
        Assert.That(r.Outputs, Is.Empty);
        Assert.That(r.ExclusiveLoot, Is.Null);
    }

    /// <summary>两者都没有时不拼悬空点号；账名随账走。</summary>
    [Test]
    public void Q254_Fallback_NoDanglingDot()
    {
        Assert.That(E.NoOutputYieldLine(new ResourceConfig { NodeId = "t1", Name = "空箱", PoolMode = PoolModes.SingleCache }), Is.EqualTo("有限 · 无物品产出"));
        Assert.That(E.NoOutputYieldLine(new ResourceConfig { NodeId = "t2", Name = "空包", PoolMode = PoolModes.CampaignClaim }), Is.EqualTo("本局一次 · 无物品产出"));
        Assert.That(E.NoOutputYieldLine(new ResourceConfig { NodeId = "t3", Name = "空箱", PoolMode = PoolModes.SingleCache, ExclusiveLoot = new List<CacheLootEntry>() }), Is.EqualTo("有限 · 无物品产出"));
    }

    /// <summary>有确定产出的资源和核心记录不走这句，配置里只有这两张卡走。</summary>
    [Test]
    public void Q254_OnlyTheTwoNodes_InConfig()
    {
        Assert.That(Cfg.Resources.Where(r => E.NoOutputYieldLine(r) != null).Select(r => r.NodeId), Is.EquivalentTo(new[] { ColdBox, Quarry }));
    }

    /// <summary>A03：看不抽随机、不改状态；开工后已抽中的结果不露出，抽前抽后读到的是同一句。</summary>
    [Test]
    public void Q254_Reading_NoRng_NoInFlightLeak()
    {
        var box = E.Resource(ColdBox);
        var s = Village();
        var req = ActionRequest.Gather(ColdBox);
        var before = E.NoOutputYieldLine(box);
        var json = SaveSerializer.Serialize(s);
        var rng = s.Rng.State;
        E.Preview(s, req);
        E.ResourceCardReason(s, req, false);
        Assert.That(s.Rng.State, Is.EqualTo(rng));
        Assert.That(SaveSerializer.Serialize(s), Is.EqualTo(json));

        Start(s, req);
        Assert.That(s.Main!.LootItemId, Is.Not.Null, "开工已抽");
        Assert.That(E.NoOutputYieldLine(box), Is.EqualTo(before));
        var p = E.Preview(s, req);
        Assert.That(p.Ok, Is.False);
        Assert.That(p.Notes, Has.Member(before), "在办时点选详情仍是全表");
    }

    /// <summary>A03：可开工时原来的“随机得到其一”预览不动；搜空后点选详情也读得到全句，状态照旧；未发现时不公开。</summary>
    [Test]
    public void Q254_ColdBox_Preview()
    {
        var req = ActionRequest.Gather(ColdBox);
        var line = E.NoOutputYieldLine(E.Resource(ColdBox));
        var s = Village();
        var p = E.Preview(s, req);
        Assert.That(p.Ok, Is.True);
        Assert.That(p.Notes, Has.Member("随机得到其一：抗病毒前体×1（20%）；草药绷带×2（80%）"));
        Assert.That(p.Notes, Has.No.Member(line), "同一张表不写两遍");

        s.Resources[ColdBox].Available = 0;
        p = E.Preview(s, req);
        Assert.That(p.Ok, Is.False);
        Assert.That(p.Reason, Does.Contain("搜空"));
        Assert.That(p.Notes, Has.Member(line));
        Assert.That(E.ResourceStock(s, E.Resource(ColdBox)).Badge, Is.EqualTo("已搜空"));

        var hidden = AtNode("village", 0);
        ReadDialogues(hidden);
        Assert.That(hidden.Resources[ColdBox].Discovered, Is.False);
        Assert.That(E.Preview(hidden, req).Notes, Has.No.Member(line), "未发现的箱子不公开");
    }

    /// <summary>A04：选中图册读得到全句（领取前后都读得到），原授予预览和已领取状态不变；不造物品。</summary>
    [Test]
    public void Q254_Quarry_Preview_And_Claim()
    {
        var req = ActionRequest.Gather(Quarry);
        var line = E.NoOutputYieldLine(E.Resource(Quarry));
        var s = AtNode("w03_quarry");
        ReadDialogues(s);
        var p = E.Preview(s, req);
        Assert.That(p.Ok, Is.True);
        Assert.That(p.Notes, Has.Member(line));
        Assert.That(p.Notes, Has.Member("获得做法知识：倒刺短矛蓝图、猎手弓蓝图、侦察护具蓝图"));
        var items = s.Items.Count;
        Start(s, req);
        Run(s);
        Assert.That(s.Items, Has.Count.EqualTo(items), "不发实物");
        Assert.That(E.KnowsKnowledge(s, "blueprint_barbed_spear"), Is.True);
        Assert.That(E.ResourceStock(s, E.Resource(Quarry)).Badge, Is.EqualTo("已领取"));
        p = E.Preview(s, req);
        Assert.That(p.Ok, Is.False);
        Assert.That(p.Reason, Does.Contain("已领取"));
        Assert.That(p.Notes, Has.Member(line));

        var hidden = AtNode("w03_quarry", 0);
        ReadDialogues(hidden);
        Assert.That(hidden.Resources[Quarry].Discovered, Is.False);
        Assert.That(E.Preview(hidden, req).Notes, Has.No.Member(line), "未发现的图册不公开");
    }

    /// <summary>卡面接线与 CSS：两张卡只用内核那句，图册不再走“本局唯一 · 做法知识包”；整句单行省略，不缩字、不改主图。</summary>
    [Test]
    public void Q254_Card_Wiring_And_Css()
    {
        var razor = File.ReadAllText(Path.Combine(Root(), "web", "Survival.Web", "Components", "LocationStrip.razor"));
        Assert.That(razor, Does.Contain("<div class=\"oc-line res-yield\" title=\"@noOutputLine\"><AccountIcon Label=\"@GameEngine.AccountLabel(r)\" />@noOutputLine</div>"));
        Assert.That(razor, Does.Not.Contain("做法知识包"));
        var css = File.ReadAllLines(Path.Combine(Root(), "web", "Survival.Web", "wwwroot", "css", "fourrows.css"));
        var rule = css.Single(l => l.Contains(".oc-line.res-yield"));
        Assert.That(rule, Does.Contain("white-space: nowrap").And.Contain("text-overflow: ellipsis"));
        Assert.That(rule, Does.Contain("font-size: 12px"), "低屏有第二行时这一句也不缩到 11px");
        var low = Array.FindIndex(css, l => l.Contains(":has(.oc-line + .oc-line) .oc-body .oc-line { font-size: 11px"));
        Assert.That(Array.IndexOf(css, rule), Is.GreaterThan(low), "同优先级时写在后面才盖得住");
    }
}
