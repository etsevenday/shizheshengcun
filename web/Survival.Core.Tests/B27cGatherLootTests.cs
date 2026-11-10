using Newtonsoft.Json.Linq;
using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 批次 B27c（策划答复 Q271：随机资源点掉落按点持久化，存档 44）。规则来源：更新说明 2026-11-06 Q269-Q273 与
/// assets/20261106/q269-q273-display-loot-adjudications.json（loot）；验收 Q269-273-A03—A05。
/// </summary>
public class B27cGatherLootTests : TestBase
{
    private const string ColdBox = "village_cold_box";
    private const string Medical = "clinic_medical_supply";
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

    private static GameState Reload(GameState s) => SaveSerializer.Deserialize(SaveSerializer.Serialize(s), Cfg);

    /// <summary>把当前档写成 43 版（去掉 44 的 LootDraw 字段，等同旧版存出来的档）再走完整读档入口。</summary>
    private static GameState As43(GameState s, bool stripDraws = true, Action<JObject>? edit = null)
    {
        var j = JObject.Parse(SaveSerializer.Serialize(s));
        j["SchemaVersion"] = SaveMigrator.Schema43;
        if (stripDraws)
            foreach (var p in ((JObject)j["Resources"]!).Properties()) ((JObject)p.Value).Remove("LootDraw");
        edit?.Invoke(j);
        return SaveSerializer.Deserialize(j.ToString(), Cfg, out _);
    }

    /// <summary>把随机序列拨到“下一次 NextDouble 满足条件”的位置（只为构造命中/未命中样例）。</summary>
    private static void SteerRng(GameState s, Func<double, bool> next)
    {
        for (var i = 0; i < 100000; i++)
        {
            var peek = new Rng { State = s.Rng.State };
            if (next(peek.NextDouble())) return;
            s.Rng.NextDouble();
        }
        Assert.Fail("没找到满足条件的随机位置");
    }

    private (string? Item, int Count, bool Rare) Snapshot(GameState s) => (s.Main!.LootItemId, s.Main.LootCount, s.Main.RareHit);

    // ───────────── Q271 冷藏箱 ─────────────

    /// <summary>A03：首次开工抽定并记在资源点；取消返还库存、记录保留；重开、存读档后再开都沿用同一物品和数量，且不再推进随机。</summary>
    [Test]
    public void A03_ColdBox_DrawPersists_AcrossCancelReload()
    {
        var s = AtNode("village");
        Assert.That(s.Resources[ColdBox].LootDraw, Is.Null, "新局未抽定");
        var cap = s.Resources[ColdBox].Available;
        Start(s, ActionRequest.Gather(ColdBox));
        var first = Snapshot(s);
        Assert.That(first.Item, Is.AnyOf("antiviral_precursor", "bandage"));
        var d = s.Resources[ColdBox].LootDraw!;
        Assert.That((d.ExclusiveItemId, d.ExclusiveCount), Is.EqualTo((first.Item, first.Count)));
        Assert.That(d.RareHit, Is.False);
        Assert.That(s.Resources[Medical].LootDraw, Is.Null, "不同资源点各自独立");

        Steps(s, 2);
        Assert.That(E.Cancel(s).Ok, Is.True);
        Assert.That(s.Resources[ColdBox].Available, Is.EqualTo(cap), "取消返还库存预留");
        Assert.That(s.Resources[ColdBox].Reserved, Is.Zero);
        Assert.That(s.Resources[ColdBox].LootDraw, Is.Not.Null, "记录保留");
        Assert.That(Count(s, first.Item!), Is.Zero, "取消不发放");

        var rng = s.Rng.State;
        Start(s, ActionRequest.Gather(ColdBox));
        Assert.That(Snapshot(s), Is.EqualTo(first));
        Assert.That(s.Rng.State, Is.EqualTo(rng), "复用不推进随机");
        E.Cancel(s);

        var r = Reload(s);
        Assert.That(r.Resources[ColdBox].LootDraw!.ExclusiveItemId, Is.EqualTo(first.Item));
        var rng2 = r.Rng.State;
        Start(r, ActionRequest.Gather(ColdBox));
        Assert.That(Snapshot(r), Is.EqualTo(first));
        Assert.That(r.Rng.State, Is.EqualTo(rng2));
    }

    /// <summary>A03：两种互斥结果各沿用一次；开工、取消与预览不透露结果，完成时才公布并发放一次，读档不补发。</summary>
    [TestCase(0.0, "antiviral_precursor", 1)]
    [TestCase(0.5, "bandage", 2)]
    public void A03_ColdBox_RevealOnlyOnCompletion_Once(double rollAtLeast, string item, int count)
    {
        var s = AtNode("village");
        SteerRng(s, x => rollAtLeast == 0 ? x < 0.2 : x >= 0.2);
        var req = ActionRequest.Gather(ColdBox);
        var notesBefore = E.Preview(s, req).Notes.ToList();
        var logs = s.Log.Count;
        Start(s, req);
        Assert.That(Snapshot(s), Is.EqualTo(((string?)item, count, false)));
        E.Cancel(s);
        var shown = string.Join("|", s.Log.Skip(logs).Select(l => l.Text));
        Assert.That(shown, Does.Not.Contain(E.ItemName(item) + "+"), "开工、取消不公布");
        Assert.That(E.Preview(s, req).Notes, Is.EqualTo(notesBefore), "预览只有公开概率，不因已抽定而变");

        // 换一个“本应抽到另一项”的随机位置，重开仍是原结果
        SteerRng(s, x => rollAtLeast == 0 ? x >= 0.2 : x < 0.2);
        Start(s, req);
        Assert.That(Snapshot(s), Is.EqualTo(((string?)item, count, false)));
        var before = Count(s, item);
        Run(s);
        Assert.That(Count(s, item) - before, Is.EqualTo(count), "完成发放一次");
        Assert.That(s.Log.Last(l => l.Kind == GameEventKind.ActionCompleted.ToString()).Text, Does.Contain($"{E.ItemName(item)}+{count}"));
        Assert.That(s.Resources[ColdBox].LootDraw, Is.Not.Null, "完成后记录仍保留");
        Assert.That(s.Resources[ColdBox].Available, Is.Zero);

        var r = Reload(Reload(s));
        Assert.That(Count(r, item), Is.EqualTo(Count(s, item)), "读档不补发");
        Assert.That(E.CanStart(r, req), Is.Not.Null, "已领耗尽");
    }

    // ───────────── Q271 医药补给箱 ─────────────

    /// <summary>A04：独立稀有命中与未命中各一例；取消重开沿用、复用不消耗随机；固定产出照原规则。</summary>
    [TestCase(true)]
    [TestCase(false)]
    public void A04_MedicalSupply_RareHitAndMiss(bool hit)
    {
        var rare = E.Resource(Medical).IndependentRare!;
        Assert.That((rare.ItemId, rare.Count, rare.Chance), Is.EqualTo(("antiviral_cure", 1, 0.01)), "原概率 1%");
        var pct = (JObject)Contract.Value["loot"]!["probabilities"]![Medical]!;
        Assert.That(((string)pct["itemId"]!, (int)pct["count"]!, (int)pct["percent"]!), Is.EqualTo(("antiviral_cure", 1, 1)));

        var s = AtNode("clinic");
        SteerRng(s, x => hit ? x < rare.Chance : x >= rare.Chance);
        var req = ActionRequest.Gather(Medical);
        Start(s, req);
        Assert.That(s.Main!.RareHit, Is.EqualTo(hit));
        Assert.That(s.Main.LootPlanned, Is.False, "医药补给箱没有互斥表");
        var d = s.Resources[Medical].LootDraw!;
        Assert.That(d, Is.Not.Null, "未命中也记为已抽定");
        Assert.That(d.RareHit, Is.EqualTo(hit));
        Assert.That(d.ExclusiveItemId, Is.Null);
        Assert.That((d.RareItemId, d.RareCount), Is.EqualTo(hit ? ("antiviral_cure", 1) : ((string?)null, 0)));
        E.Cancel(s);

        // 反向拨随机：重开仍是原结果，且不消耗随机
        SteerRng(s, x => hit ? x >= rare.Chance : x < rare.Chance);
        var rng = s.Rng.State;
        Start(s, req);
        Assert.That(s.Main!.RareHit, Is.EqualTo(hit));
        Assert.That(s.Rng.State, Is.EqualTo(rng));

        var fixedBefore = E.Resource(Medical).Outputs.ToDictionary(kv => kv.Key, kv => Count(s, kv.Key));
        var cureBefore = Count(s, "antiviral_cure");
        Run(s);
        foreach (var kv in E.Resource(Medical).Outputs) Assert.That(Count(s, kv.Key) - fixedBefore[kv.Key], Is.EqualTo(kv.Value), "固定产出照原规则");
        Assert.That(Count(s, "antiviral_cure") - cureBefore, Is.EqualTo(hit ? 1 : 0));
        Assert.That(Count(Reload(s), "antiviral_cure"), Is.EqualTo(Count(s, "antiviral_cure")));
    }

    /// <summary>A05：新局与不同资源点各自独立：一个点抽定不影响另一点，另一局从未抽定开始。</summary>
    [Test]
    public void A05_NewGame_And_Nodes_Independent()
    {
        var s = AtNode("village");
        Start(s, ActionRequest.Gather(ColdBox));
        Assert.That(s.Resources[ColdBox].LootDraw, Is.Not.Null);
        Assert.That(s.Resources[Medical].LootDraw, Is.Null);
        var other = AtNode("village", 999);
        Assert.That(other.Resources.Values.All(r => r.LootDraw == null), Is.True, "新局没有记录");
        Assert.That(Cfg.Resources.Where(r => r.ExclusiveLoot != null || r.IndependentRare != null).Select(r => r.NodeId),
            Is.EquivalentTo(((JArray)Contract.Value["loot"]!["currentNodes"]!).Select(x => (string)x!)), "当前适用两个资源点");
    }

    // ───────────── Q271 旧档迁移 ─────────────

    /// <summary>A05 未开工：迁移后仍未抽定，不抽随机、不写日志；下次合法开工才抽。</summary>
    [Test]
    public void A05_Legacy_NotStarted()
    {
        var s = AtNode("village");
        var m = As43(s);
        Assert.That(m.SchemaVersion, Is.EqualTo(GameState.CurrentSchema));
        Assert.That(int.Parse(GameState.CurrentSchema), Is.GreaterThanOrEqualTo(44), "B27c 为 44，B32e 起 45");
        Assert.That(m.Resources.Values.All(r => r.LootDraw == null), Is.True);
        Assert.That(m.Rng.State, Is.EqualTo(s.Rng.State), "迁移不抽随机");
        Assert.That(m.Log.Select(l => l.Text), Is.EqualTo(s.Log.Select(l => l.Text)), "迁移静默");
        var rng = m.Rng.State;
        Start(m, ActionRequest.Gather(ColdBox));
        Assert.That(m.Resources[ColdBox].LootDraw, Is.Not.Null);
        Assert.That(m.Rng.State, Is.Not.EqualTo(rng), "首次开工才抽");
    }

    /// <summary>A05 原在办：从快照补写记录，原行动、时间和预留不动；取消后重开沿用。冷藏箱与医药补给箱（命中/未命中）各验。</summary>
    [TestCase(ColdBox, "village", false)]
    [TestCase(Medical, "clinic", false)]
    [TestCase(Medical, "clinic", true)]
    public void A05_Legacy_InProgress_FromSnapshot(string node, string loc, bool rareHit)
    {
        var s = AtNode(loc);
        if (node == Medical) SteerRng(s, x => rareHit ? x < 0.01 : x >= 0.01);
        Start(s, ActionRequest.Gather(node));
        Steps(s, 2);
        var snap = Snapshot(s);
        var elapsed = s.Main!.ElapsedMinutes;
        var reserved = s.Main.ReservedResourceUnits;
        var m = As43(s);
        Assert.That(m.Rng.State, Is.EqualTo(s.Rng.State), "迁移不抽随机");
        var d = m.Resources[node].LootDraw!;
        Assert.That(d, Is.Not.Null, "原在办承接快照");
        Assert.That(d.RareHit, Is.EqualTo(snap.Rare));
        if (node == ColdBox) Assert.That((d.ExclusiveItemId, d.ExclusiveCount), Is.EqualTo((snap.Item, snap.Count)));
        else
        {
            Assert.That(d.ExclusiveItemId, Is.Null);
            Assert.That(d.RareHit, Is.EqualTo(rareHit));
            Assert.That(d.RareItemId, Is.EqualTo(rareHit ? "antiviral_cure" : null));
        }
        Assert.That(Snapshot(m), Is.EqualTo(snap));
        Assert.That((m.Main!.ElapsedMinutes, m.Main.ReservedResourceUnits, m.Resources[node].Reserved), Is.EqualTo((elapsed, reserved, s.Resources[node].Reserved)));
        E.Cancel(m);
        var rng = m.Rng.State;
        Start(m, ActionRequest.Gather(node));
        Assert.That(Snapshot(m), Is.EqualTo(snap));
        Assert.That(m.Rng.State, Is.EqualTo(rng));
    }

    /// <summary>A05 原已取消无快照：迁移不补记录；下次合法开工抽一次，之后再取消重开沿用这一次。</summary>
    [Test]
    public void A05_Legacy_CancelledNoSnapshot()
    {
        var s = AtNode("village");
        Start(s, ActionRequest.Gather(ColdBox));
        E.Cancel(s);
        var m = As43(s);
        Assert.That(m.Resources[ColdBox].LootDraw, Is.Null, "无可恢复快照：等下次合法开工");
        Assert.That(m.Resources[ColdBox].Available, Is.EqualTo(s.Resources[ColdBox].Available));
        var rng = m.Rng.State;
        Start(m, ActionRequest.Gather(ColdBox));
        Assert.That(m.Rng.State, Is.Not.EqualTo(rng), "下次开工抽一次");
        var snap = Snapshot(m);
        E.Cancel(m);
        var rng2 = m.Rng.State;
        Start(m, ActionRequest.Gather(ColdBox));
        Assert.That(Snapshot(m), Is.EqualTo(snap));
        Assert.That(m.Rng.State, Is.EqualTo(rng2));
    }

    /// <summary>A05 原已领取：耗尽点保持耗尽，迁移不补奖励、不写记录。</summary>
    [Test]
    public void A05_Legacy_Claimed_StaysExhausted()
    {
        var s = AtNode("village");
        Start(s, ActionRequest.Gather(ColdBox));
        Run(s);
        var items = s.Items.Sum(i => i.Count);
        var m = As43(s);
        Assert.That(m.Resources[ColdBox].LootDraw, Is.Null);
        Assert.That(m.Resources[ColdBox].Available, Is.Zero);
        Assert.That(m.Items.Sum(i => i.Count), Is.EqualTo(items), "迁移不发奖励");
        Assert.That(E.CanStart(m, ActionRequest.Gather(ColdBox)), Is.Not.Null);
    }

    /// <summary>A05：已有记录不因迁移或重读覆盖/刷新；互斥快照缺损不替它抽、不写记录。</summary>
    [Test]
    public void A05_Legacy_ExistingRecordKept_BrokenSnapshotNotDrawn()
    {
        var s = AtNode("village");
        Start(s, ActionRequest.Gather(ColdBox));
        var kept = s.Resources[ColdBox].LootDraw!.ExclusiveItemId;
        var other = kept == "bandage" ? "antiviral_precursor" : "bandage";
        var m = As43(s, stripDraws: false, edit: j => j["Main"]!["LootItemId"] = other);
        Assert.That(m.Resources[ColdBox].LootDraw!.ExclusiveItemId, Is.EqualTo(kept), "已有记录不重复写入");
        Assert.That(Reload(Reload(m)).Resources[ColdBox].LootDraw!.ExclusiveItemId, Is.EqualTo(kept), "重读不刷新");

        var b = As43(s, edit: j => j["Main"]!["LootPlanned"] = false);
        Assert.That(b.Resources[ColdBox].LootDraw, Is.Null, "缺损快照不在迁移时替它抽取");
        Assert.That(b.Rng.State, Is.EqualTo(s.Rng.State));
    }
}
