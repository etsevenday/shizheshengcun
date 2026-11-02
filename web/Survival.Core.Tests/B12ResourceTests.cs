using Newtonsoft.Json.Linq;
using Survival.Core.Config;
using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 批次 B12（R36 + Q31 清理）：96 条增量资源并入资源表（S05「唯一资源表与增量边界」「定量采集与四生命周期」、A08、A07 X1.7），
/// world_stock 按件扣库存与午夜日补、flowing 不限量、finite 不补；新节点清理开放（S04 Q31 节：串行 480/720/1440、地牢每代 1 次、clearLabel）；
/// schema 21 迁移（B11 标记 → 已发现、旧档持有记录 → campaignId 已领）。验收 RES30-01/02、RES31-01/02/04、NODE01/03/04、CLR30-04、COMP19、X1.7。
/// </summary>
public class B12ResourceTests : TestBase
{
    private static JObject Load(string name) => JObject.Parse(File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, name)));

    private void Gather(GameState s, string id)
    {
        Start(s, ActionRequest.Gather(id));
        Run(s);
    }

    /// <summary>RES30-01/COMP19：96 条与 map-v3 逐条一致、引用 143 节点中的合法地点；旧 20 条原样保留；物品产物都在物品表里。</summary>
    [Category("DesignSync")]
    [Test]
    public void RES30_01_Import96_KeepsLegacy20()
    {
        var map = (JArray)Load("design.map-v3.json")["resources"]!;
        Assert.That(map, Has.Count.EqualTo(96));
        var rows = ((JArray)Load("design.progression.json")["fieldExperience"]!["resourceRows"]!).ToDictionary(r => (string)r["resourceId"]!);
        foreach (var j in map)
        {
            var r = E.Resource((string)j["id"]!);
            Assert.That(r.LocationId, Is.EqualTo((string)j["nodeId"]!));
            Assert.That(E.Location(r.LocationId), Is.Not.Null);
            Assert.That(r.BaseMinutes, Is.EqualTo((int)j["workMinutes"]!), r.NodeId);
            Assert.That(r.Outputs, Is.EquivalentTo(((JObject)j["items"]!).Properties().ToDictionary(p => p.Name, p => (int)p.Value)), r.NodeId);
            Assert.That(r.Scope, Is.EqualTo((string)j["scope"]!), r.NodeId);
            Assert.That(r.Kind, Is.EqualTo((string)j["kind"]!), r.NodeId);
            var cap = int.Parse(j["cap"]!.ToString());
            if (cap < 0) Assert.That(r.IsUnlimited, Is.True, $"{r.NodeId} flowing −1 不限量");
            else Assert.That(r.Cap, Is.EqualTo(cap), r.NodeId);
            Assert.That(r.DailyRefill, Is.EqualTo((int)j["dailyRefill"]!), r.NodeId);
            var reveal = (int)j["reveal"]!;
            Assert.That(reveal == 0 ? r.RevealOnVisit : r.RevealAt == reveal, Is.True, r.NodeId);
            // B13b（Q121）取代 H2：96 行逐行有/无合格工具，见 B13bResourceTests.TOOL_Q121_01
            if (!r.IsCampaign) Assert.That(r.Outputs.Keys.All(k => Cfg.Items.Any(i => i.Id == k)), Is.True, r.NodeId);
        }
        // 旧 20 条：与 116 行合同里的 legacy_20 一致（数值不变）
        foreach (var row in rows.Values.Where(r => (string)r["origin"]! == "legacy_20"))
        {
            var r = E.Resource((string)row["resourceId"]!);
            Assert.That(r.Scope, Is.EqualTo(ResourceScopes.WorldStock));
            Assert.That(r.BaseMinutes, Is.EqualTo((int)row["baseMinutes"]!), r.NodeId);
            Assert.That(r.LocationId, Is.EqualTo((string)row["nodeId"]!), r.NodeId);
        }
        Assert.That(Cfg.Resources.Count(r => r.Scope == ResourceScopes.WorldStock), Is.EqualTo(20 + 64 + 43), "B13b：44 条主世界恢复来源里 43 条 world_stock（检查哨值勤补给箱是单局唯一）");
        Assert.That(Cfg.Resources.Count(r => r.IsInstance), Is.EqualTo(24));
        Assert.That(Cfg.Resources.Count(r => r.IsRecord), Is.EqualTo(8));
        Assert.That(Cfg.Resources.Count(r => r.IsCampaign), Is.EqualTo(8 + 4 + 6), "B13b：检查哨值勤补给箱、工坊首次支援包、消防车器材箱、采石坡图册；B13c：6 个旧 d03 唯一箱");
        Assert.That(Cfg.Resources.Where(r => r.IsInstance).All(r => r.Cap == 2 && !r.Refills && E.Location(r.LocationId).DungeonId != null), Is.True, "普通箱每箱 2/0、在地牢房间");
    }

    /// <summary>A07 X1.7：6 条调整与 3 个新保底点按表实装（产物、每次、cap、日补、揭示、分钟）。</summary>
    [Test]
    public void X17_SixAdjustments_ThreeSupplyPoints()
    {
        void Row(string id, string item, int per, int cap, int refill)
        {
            var r = E.Resource(id);
            Assert.That(r.Outputs, Is.EquivalentTo(new Dictionary<string, int> { [item] = per }), id);
            Assert.That(r.Cap, Is.EqualTo(cap), id);
            Assert.That(r.DailyRefill, Is.EqualTo(refill), id);
        }
        Row("w11_orchard_a", "raw_root", 2, 12, 4);
        Row("w26_island_a", "raw_root", 2, 16, 6);
        Row("w34_depot_a", "canned_beans", 1, 6, 0);
        Row("w34_depot_b", "saline", 1, 6, 0);
        Row("w11_orchard_b", "cloth", 2, 8, 2);
        Row("w14_cemeteryroad_b", "cloth", 2, 8, 2);
        Assert.That(E.Resource("w34_depot_a").PoolMode, Is.EqualTo(PoolModes.Finite), "kind 改 finite");
        Row("h03_workshop_supply_disinfectant", "disinfectant", 1, 4, 1);
        Row("w13_mill_supply_salt", "salt", 1, 6, 2);
        Row("w15_shrinewoods_supply_berry", "berry", 2, 8, 2);
        Assert.That(E.Resource("h03_workshop_supply_disinfectant").RevealAt, Is.EqualTo(100));
        Assert.That(E.Resource("w13_mill_supply_salt").RevealAt, Is.EqualTo(50));
        Assert.That(E.Resource("w15_shrinewoods_supply_berry").BaseMinutes, Is.EqualTo(25));
    }

    /// <summary>RES30-02：按件扣库存（产 2 扣 2）、取消原样退回；午夜日补夹到 cap 且含预留；采集给经验（Q55 归属）。</summary>
    [Test]
    public void RES30_02_WorldStock_ByItem_Refill_CancelLossless()
    {
        var s = New();
        E.DebugMapTeleport(s, "w11_orchard");
        E.DebugExploreHere100(s);
        Assert.That(s.Resources["w11_orchard_a"].Discovered, Is.True);
        Assert.That(s.Resources["w11_orchard_a"].Available, Is.EqualTo(12));
        Start(s, ActionRequest.Gather("w11_orchard_a"));
        Assert.That(s.Main!.BaseWorkMinutes, Is.EqualTo(25));
        Assert.That(s.Resources["w11_orchard_a"].Available, Is.EqualTo(10));
        Assert.That(s.Resources["w11_orchard_a"].Reserved, Is.EqualTo(2));
        E.Cancel(s);
        Assert.That(s.Resources["w11_orchard_a"].Available, Is.EqualTo(12), "取消无损");
        Assert.That(s.Resources["w11_orchard_a"].Reserved, Is.EqualTo(0));

        var roots = Count(s, "raw_root", "w11_orchard");
        Gather(s, "w11_orchard_a");
        Assert.That(Count(s, "raw_root", "w11_orchard"), Is.EqualTo(roots + 2), "产物落当前地面");
        Assert.That(s.Resources["w11_orchard_a"].Available, Is.EqualTo(10));
        Assert.That(s.Log.Last(l => l.Kind == GameEventKind.ActionCompleted.ToString()).Text, Does.Contain("采集经验"));

        // 日补 4 夹到 12（含预留）：先采到 4，跨午夜后 8
        s.Resources["w11_orchard_a"].Available = 4;
        AdvanceTo(s, At(2, 0, 30));
        Assert.That(s.Resources["w11_orchard_a"].Available, Is.EqualTo(8));
        s.Resources["w11_orchard_a"].Available = 10;
        AdvanceTo(s, At(3, 0, 30));
        Assert.That(s.Resources["w11_orchard_a"].Available, Is.EqualTo(12), "不越过 cap");
    }

    /// <summary>finite 不日补、flowing 不限量不扣；采尽后写“已采尽，不再补充”。</summary>
    [Test]
    public void Finite_NoRefill_Flowing_Unlimited()
    {
        var s = New();
        E.DebugMapTeleport(s, "w34_depot");
        E.DebugExploreHere100(s);
        s.Resources["w34_depot_bottles"].Available = 2;
        Gather(s, "w34_depot_bottles");
        Assert.That(s.Resources["w34_depot_bottles"].Available, Is.EqualTo(0));
        Assert.That(E.CanStart(s, ActionRequest.Gather("w34_depot_bottles")), Is.EqualTo("已采尽，不再补充"));
        AdvanceTo(s, At(2, 0, 30));
        Assert.That(s.Resources["w34_depot_bottles"].Available, Is.EqualTo(0), "finite 午夜不补");

        E.DebugMapTeleport(s, "w12_canal");
        Assert.That(s.Resources["w12_canal_water"].Discovered, Is.True, "reveal 0 到访即见");
        var before = Count(s, "dirty_water", "w12_canal");
        Gather(s, "w12_canal_water");
        Assert.That(Count(s, "dirty_water", "w12_canal"), Is.EqualTo(before + 4));
        Assert.That(s.Resources["w12_canal_water"].Available, Is.Null, "不限量");
    }

    /// <summary>H6/RES31-04：h03 消毒液配给在公开地点到访自动 100 时揭示；城市配给不给采集经验、不吃熟练被动。</summary>
    [Test]
    public void H03_SupplyRevealedOnArrival_NoXp()
    {
        var s = New();
        E.DebugMapTeleport(s, "h03_workshop");
        Assert.That(s.Resources["h03_workshop_supply_disinfectant"].Discovered, Is.True);
        var res = E.Resource("h03_workshop_supply_disinfectant");
        Assert.That(res.XpAllowed, Is.False);
        Assert.That(E.GatherXpPreview(s, res), Does.Contain("不计"));
        var xp = s.Skills.ToDictionary(kv => kv.Key, kv => kv.Value.Xp);
        Gather(s, res.NodeId);
        Assert.That(s.Skills.All(kv => kv.Value.Xp == xp[kv.Key]), Is.True, "不加经验");
        Assert.That(s.Resources[res.NodeId].Available, Is.EqualTo(3));
    }

    /// <summary>NODE01：135 新节点全部开放清理、参数齐全（城市/公开 1/1/1440、低危 3/3/480、中高危 2/2/720、地牢每代 1 次），旧 8 不变。</summary>
    [Category("DesignSync")]
    [Test]
    public void NODE01_AllNewNodesClearEnabled()
    {
        var rules = (JArray)Load("design.node-expansion.json")["nodes"]!;
        Assert.That(rules, Has.Count.EqualTo(135));
        foreach (var n in rules)
        {
            var lc = E.Location((string)n["nodeId"]!);
            Assert.That(lc.ClearEnabled, Is.True, lc.Id);
            Assert.That(lc.ClearLabel, Is.EqualTo((string)n["clearLabel"]!), lc.Id);
            Assert.That(E.ClearMinutesAt(lc.Id), Is.EqualTo(20), lc.Id);
            Assert.That(lc.Clear.Cap, Is.EqualTo((int)n["cap"]!), lc.Id);
            Assert.That(lc.ClearPerGeneration, Is.EqualTo((string)n["clearRefillMode"]! == "instance_generation_once"), lc.Id);
            if (!lc.ClearPerGeneration) Assert.That(lc.Clear.RefillIntervalMinutes, Is.EqualTo((int)n["serialRefillMinutes"]!), lc.Id);
        }
        Assert.That(E.ClearLabelAt("field"), Is.EqualTo("清理"));
        Assert.That(E.ClearMinutesAt("field"), Is.EqualTo(Cfg.Actions.ClearMinutes));
        Assert.That(E.Location("field").Clear.RefillIntervalMinutes, Is.EqualTo(240), "旧 8 参数不变");
    }

    /// <summary>NODE03/RES31-02/CLR30-04：低危野外 3/3、每 480 分钟串行恢复；清理不扣定向库存；取消不退次数；城市写“整理搜集”。</summary>
    [Test]
    public void NODE03_SerialRefill_ClearDoesNotTouchStock()
    {
        var s = New();
        E.DebugMapTeleport(s, "w11_orchard");
        E.DebugExploreHere100(s);
        var stock = s.Resources["w11_orchard_a"].Available;
        Start(s, ActionRequest.Clear());
        Assert.That(s.Main!.BaseWorkMinutes, Is.EqualTo(20));
        Assert.That(s.Main.Label, Is.EqualTo("清理翻找"));
        Run(s);
        Assert.That(E.ClearChargesAt(s, "w11_orchard").charges, Is.EqualTo(2));
        Assert.That(s.Resources["w11_orchard_a"].Available, Is.EqualTo(stock), "RES31-02：清理不扣定向库存");
        Assert.That(s.Log.Any(l => l.Text.StartsWith("清理翻找完成")), Is.True);
        Start(s, ActionRequest.Clear());
        E.Cancel(s);
        Assert.That(E.ClearChargesAt(s, "w11_orchard").charges, Is.EqualTo(1), "取消不退");
        var t0 = s.GameMinute;
        AdvanceTo(s, t0 + 480);
        Assert.That(E.ClearChargesAt(s, "w11_orchard").charges, Is.EqualTo(2), "480 分钟回 1 次");

        E.DebugMapTeleport(s, "h02_square");
        Assert.That(E.ClearLabelAt("h02_square"), Is.EqualTo("整理搜集"));
        Assert.That(E.CanStart(s, ActionRequest.Clear()), Is.Null, "城市公共也可整理搜集（1/1，1440）");
    }

    /// <summary>NODE04：地牢房间每代 1 次，用掉后跨 72 小时仍为 0；不改箱库存；01 缓冲房清理只出空结果。</summary>
    [Test]
    public void NODE04_DungeonRoom_OncePerGeneration()
    {
        var s = New();
        E.DebugMapTeleport(s, "d05_04");
        Assert.That(E.ClearCountText(s, "d05_04"), Is.EqualTo("本代次数1/1"));
        var box = s.Resources["d05_04_cache0"].Available;
        Start(s, ActionRequest.Clear());
        Run(s);
        Assert.That(E.ClearChargesAt(s, "d05_04").charges, Is.EqualTo(0));
        Assert.That(s.Resources["d05_04_cache0"].Available, Is.EqualTo(box), "不领箱");
        s.GameMinute += 4320 + 60;   // 直接跨 72 小时（只看次数计算，不模拟生存）
        Assert.That(E.ClearChargesAt(s, "d05_04").charges, Is.EqualTo(0), "72 小时未换代仍 0");
        Assert.That(E.CanStart(s, ActionRequest.Clear()), Does.Contain("每代1次"));
        Assert.That(E.ClearCountText(s, "d05_04"), Does.Contain("本代已用完"));

        E.DebugMapTeleport(s, "d05_01");
        var ground = s.Items.Count(i => i.Owner == "d05_01");
        Start(s, ActionRequest.Clear());
        Run(s);
        Assert.That(s.Items.Count(i => i.Owner == "d05_01"), Is.EqualTo(ground), "01 缓冲 q31_buffer_empty 只出空结果");
    }

    /// <summary>schema 20→21：B11 揭示标记 → 已发现、库存取 cap；旧档持有 church_route_record → d01_08 已领；幂等。</summary>
    [Test]
    public void Migration_20To21_MarkersToDiscovered_LegacyRecordsClaimed()
    {
        var s = New();
        E.DebugMapTeleport(s, "w11_orchard");
        E.DebugSetProgress(s, "w11_orchard", 25);
        s.Progress.Flags.Add("church_route_record");
        var j = JObject.Parse(SaveSerializer.Serialize(s));
        j["SchemaVersion"] = "20";
        var res = (JObject)j["Resources"]!;
        foreach (var r in Cfg.Resources.Skip(20)) res.Remove(r.NodeId);
        j["RevealedResourceMarkers"] = new JArray("w11_orchard_a");
        var m = SaveSerializer.Deserialize(j.ToString(), Cfg, out var notes);
        Assert.That(m.SchemaVersion, Is.EqualTo(GameState.CurrentSchema), "20→21→22→23（B13b 起再升到 23）");
        Assert.That(m.Resources, Has.Count.EqualTo(Cfg.Resources.Count), "B13c 起 169 条（B13b 另 47 条恢复来源、B13c 6 个旧唯一箱）");
        Assert.That(m.Resources["w11_orchard_a"].Discovered, Is.True);
        Assert.That(m.Resources["w11_orchard_a"].Available, Is.EqualTo(12));
        Assert.That(m.Resources["w11_orchard_b"].Discovered, Is.False, "50% 未到");
        Assert.That(m.RevealedResourceMarkers, Is.Null);
        Assert.That(m.Progress.Flags, Does.Contain(GameEngine.CampaignKey("d01_08_unique")), "已持有记录 → 已领不补");
        Assert.That(m.Progress.Flags, Does.Not.Contain(GameEngine.CampaignKey("d02_08_unique")));
        Assert.That(notes, Has.Some.Contains(Cfg.Resources.Count.ToString()));
        Assert.That(SaveSerializer.Serialize(m), Does.Not.Contain("RevealedResourceMarkers"));
        var again = SaveSerializer.Deserialize(SaveSerializer.Serialize(m), Cfg, out var notes2);
        Assert.That(notes2, Is.Empty);
        Assert.That(SaveSerializer.Serialize(again), Is.EqualTo(SaveSerializer.Serialize(m)), "幂等");
    }
}
