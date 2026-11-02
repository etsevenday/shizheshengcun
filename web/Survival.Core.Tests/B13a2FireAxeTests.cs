using Newtonsoft.Json.Linq;
using Survival.Core.Config;
using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 批次 B13a2（R137、R138；策划提交 5b618c9 答复 Q136 A / Q137 C）：
/// Q136 消防车器材箱的消防斧最大耐久按 40—52 / 0.1 正常抽一次、当前=最大（与 B13b 现状一致，核对）；
/// Q137 30 个锁闭储物点新增 fire_axe 合格工具，实际选用时破门工作量×0.75，与装备、搜集 3/10 级、5 级工具运用同在 ×0.70 下限内相乘，
/// 工具基时 −5、产量不增、成功磨 1/取消 0，旧在办沿开工快照。验收 EQ-Q136-01/02、TOOL-Q137-01—06（A10 本轮章）。
/// </summary>
public class B13a2FireAxeTests : TestBase
{
    private static JObject Contract() => JObject.Parse(File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "design.fire-axe-adjudications.json")));

    private static void Lv(GameState s, string id, int lv) => s.Skills[id].Level = lv;

    private GameState AtNode(string node, int progress = 100, uint seed = 12345)
    {
        var s = New(seed);
        E.DebugMapTeleport(s, node);
        if (progress > s.Locations[node].Progress) E.DebugSetProgress(s, node, progress);
        return s;
    }

    private ItemInstance Axe(GameState s, string owner = ItemInstance.Carried) => E.DebugCreateEquipment(s, "fire_axe", "white", owner);

    private ItemInstance Give(GameState s, string itemId)
    {
        E.DebugGive(s, itemId, 1);
        return s.Items.Where(i => i.ItemId == itemId && i.IsCarried).OrderByDescending(i => i.InstanceId).First();
    }

    private GameState ClaimAxe(uint seed, out ItemInstance axe)
    {
        var s = AtNode("m01_road", 50, seed);
        Start(s, ActionRequest.Gather("firetruck_cache"));
        Run(s);
        axe = s.Items.Single(i => i.ItemId == "fire_axe");
        return s;
    }

    /// <summary>EQ-Q136-01：固定箱只出白色 0 词条消防斧 1，max 在 40—52、0.1 步长正常抽一次、current=max，不固定 52；预览只写区间、不消耗真实随机或创建物。</summary>
    [Test]
    public void EQ_Q136_01_FiretruckAxeDurability()
    {
        var c = (JObject)Contract()["durability"]!;
        Assert.That(((JArray)c["templateMaxRange"]!).Select(x => (double)x), Is.EqualTo(new[] { 40.0, 52.0 }));
        Assert.That((bool)c["fixedMax52"]!, Is.False);
        Assert.That(E.Item("fire_axe").DurabilityMaxRange, Is.EqualTo(new[] { 40.0, 52.0 }));

        var pre = AtNode("m01_road", 50);
        var before = SaveSerializer.Serialize(pre);
        var p = E.Preview(pre, ActionRequest.Gather("firetruck_cache"));
        Assert.That(p.Ok, Is.True, p.Reason);
        Assert.That(p.Notes, Has.Some.Contains("最大耐久在40—52内随机生成一次").And.Some.Contains("当前耐久为满"));
        Assert.That(p.Notes, Has.Some.Contains("白色、无附加词条"));
        Assert.That(SaveSerializer.Serialize(pre), Is.EqualTo(before), "预览不消耗真实随机、不预生成可领取物");

        var maxes = new HashSet<double>();
        for (uint seed = 1; seed <= 8; seed++)
        {
            ClaimAxe(seed, out var axe);
            Assert.That(axe.Quality, Is.EqualTo("white"), $"种子{seed}");
            Assert.That(axe.Affixes, Is.Null, $"种子{seed}：0 附加词条");
            Assert.That(axe.MaxDurability, Is.InRange(40.0, 52.0), $"种子{seed}");
            Assert.That(axe.MaxDurability!.Value * 10, Is.EqualTo(Math.Round(axe.MaxDurability.Value * 10)).Within(1e-9), $"种子{seed}：0.1 步长");
            Assert.That(axe.Durability, Is.EqualTo(axe.MaxDurability), $"种子{seed}：current=max");
            maxes.Add(axe.MaxDurability.Value);
        }
        Assert.That(maxes.Count, Is.GreaterThan(1), "每件正常随机，不是固定 52");
    }

    /// <summary>EQ-Q136-02：已有实例的 current/max 在读档、拾取放下、装备时保持，不重抽、不补满；箱子不重复奖励。</summary>
    [Test]
    public void EQ_Q136_02_ExistingAxeKept()
    {
        var s = ClaimAxe(3, out var axe);
        var max = axe.MaxDurability!.Value;
        axe.Durability = max - 7.3;
        var loaded = SaveSerializer.Deserialize(SaveSerializer.Serialize(s), Cfg, out _);
        var a2 = loaded.Items.Single(i => i.ItemId == "fire_axe");
        Assert.That((a2.MaxDurability, a2.Durability), Is.EqualTo(((double?)max, (double?)(max - 7.3))), "读档不重抽、不补满");
        Assert.That(E.CanStart(loaded, ActionRequest.Gather("firetruck_cache")), Does.Contain("已领取"), "不重复奖励");
        // 拾取 / 放下 / 装备：同一实例，max 不变
        a2.Owner = ItemInstance.Carried;
        a2.Owner = ItemInstance.EquipOwner(GameState.PlayerId, EquipSlots.Weapon1);
        Assert.That((a2.MaxDurability, a2.Durability), Is.EqualTo(((double?)max, (double?)(max - 7.3))));
        Assert.That(loaded.Items.Count(i => i.ItemId == "fire_axe"), Is.EqualTo(1));
    }

    /// <summary>TOOL-Q137-01：恰好 30 个名单点新增 fire_axe 并标锁闭，原工具/徒手继续；未列点（含其他撬棍行、旧 20、领取）不能用消防斧。</summary>
    [Test]
    public void TOOL_Q137_01_ExactLockedWhitelist()
    {
        var trait = (JObject)Contract()["trait"]!;
        Assert.That((double)trait["workMultiplier"]!, Is.EqualTo(0.75));
        var rows = ((JArray)trait["lockedRows"]!).Cast<JObject>().ToDictionary(r => (string)r["resourceId"]!);
        Assert.That(rows, Has.Count.EqualTo(30));
        Assert.That(((JArray)trait["lockedResourceIds"]!).Select(x => (string)x!), Is.EquivalentTo(rows.Keys));

        var withAxe = Cfg.Resources.Where(r => r.ToolBonus != null && r.ToolBonus.ItemIds.Contains("fire_axe")).Select(r => r.NodeId).ToList();
        Assert.That(withAxe, Is.EquivalentTo(rows.Keys), "fire_axe 只在 30 个名单点");
        Assert.That(Cfg.Resources.Where(r => r.ToolBonus is { LockedSearch: true }).Select(r => r.NodeId), Is.EquivalentTo(rows.Keys));
        foreach (var (id, row) in rows)
        {
            var r = E.Resource(id);
            Assert.That(r.LocationId, Is.EqualTo((string)row["nodeId"]!), id);
            Assert.That(r.Name, Is.EqualTo((string)row["name"]!), id);
            var original = ((JArray)row["originalTools"]!).Select(x => (string)x!).ToList();
            Assert.That(r.ToolBonus!.ItemIds, Is.EqualTo(original.Append("fire_axe").ToList()), $"{id}：原工具保留");
            Assert.That(r.ToolBonus.BaseMinutesOverride, Is.EqualTo((int)row["toolBaseMinutes"]!), id);
            Assert.That(r.ToolBonus.OutputMultiplier, Is.EqualTo(1), id);
            Assert.That(r.ToolBonus.ConditionalFactor("fire_axe"), Is.EqualTo(0.75), id);
            Assert.That(r.ToolBonus.ConditionalFactor("crowbar"), Is.EqualTo(1), id);
        }
        Assert.That(Cfg.Resources.Where(r => r.IsCampaign).All(r => r.ToolBonus == null), Is.True, "唯一领取/核心记录无工具");

        var s = AtNode("m01_road", 50);
        var axe = Axe(s);
        var knife = Give(s, "stone_knife");
        // 名单点：消防斧、原工具石刀、徒手都可
        Assert.That(E.CanStart(s, ActionRequest.Gather("m01_road_crate", axe.InstanceId)), Is.Null);
        Assert.That(E.CanStart(s, ActionRequest.Gather("m01_road_crate", knife.InstanceId)), Is.Null);
        Assert.That(E.CanStart(s, ActionRequest.Gather("m01_road_crate", null, true)), Is.Null);
        var p = E.Preview(s, ActionRequest.Gather("m01_road_crate", axe.InstanceId));
        Assert.That(p.Notes, Has.Some.Contains("锁闭储物点：合格工具石刀或消防斧").And.Some.Contains("选消防斧时破门工作量×0.75"));
        Assert.That(p.Notes, Has.Some.Contains("本次实际使用消防斧：破门生效"));
        var pk = E.Preview(s, ActionRequest.Gather("m01_road_crate", knife.InstanceId));
        Assert.That(pk.Notes, Has.None.Contains("本次实际使用"), "没选消防斧不写本次生效");
        // 名单外的撬棍行：显式消防斧整次拒绝，不暗换撬棍；拖放同样提示
        var bar = Give(s, "crowbar");
        Assert.That(E.CanStart(s, ActionRequest.Gather("m01_road_barrier", axe.InstanceId)), Is.EqualTo("这件工具对该资源点没有加成"));
        var drop = E.ResolveDrop(s, DropSource.Parse($"card:carried|fire_axe|{axe.InstanceId}")!, DropTarget.Parse("res:m01_road_barrier")!);
        Assert.That((drop.Ok, drop.Reason), Is.EqualTo((false, "这件工具对该资源点没有加成")));
        var auto = E.Preview(s, ActionRequest.Gather("m01_road_barrier"));
        Assert.That(auto.ToolName, Is.EqualTo("撬棍"), "名单外自动候选只有撬棍");
        Assert.That(E.QualifiedToolChoices(s, E.Resource("m01_road_barrier")).Select(i => i.ItemId), Is.EqualTo(new[] { "crowbar" }));
        Assert.That(bar.InstanceId, Is.GreaterThan(0));
    }

    /// <summary>TOOL-Q137-02：实际选择本人合法消防斧才×0.75；只穿戴/包里另一把/地面/别的工具不借；两把不叠；显式失效整次拒绝。</summary>
    [Test]
    public void TOOL_Q137_02_ActualSelectedInstanceOnly()
    {
        var s = AtNode("w19_survey");
        Assert.That(E.TimeFactor(s, s.Player, ActionType.Gather), Is.EqualTo(1), "白天、轻装");
        var bar = Give(s, "crowbar");
        var worn = Axe(s, ItemInstance.EquipOwner(GameState.PlayerId, EquipSlots.Weapon1));
        worn.Durability = worn.MaxDurability!.Value * 0.5;   // 成色低于撬棍：自动候选选撬棍
        var p = E.Preview(s, ActionRequest.Gather("w19_survey_b"));
        Assert.That(p.ToolName, Is.EqualTo("撬棍"));
        Assert.That(p.FactorAtStart, Is.EqualTo(1).Within(1e-12), "已装备消防斧但没选用：不是光环");
        Assert.That(p.FactorReasons, Has.None.Contains("破门"));
        Assert.That(E.QualifiedToolChoices(s, E.Resource("w19_survey_b")).Select(i => i.InstanceId), Is.EqualTo(new[] { bar.InstanceId, worn.InstanceId }), "界面可改用已装备的消防斧");

        var pw = E.Preview(s, ActionRequest.Gather("w19_survey_b", worn.InstanceId));
        Assert.That(pw.Ok, Is.True, pw.Reason);
        Assert.That(pw.FactorAtStart, Is.EqualTo(0.75).Within(1e-12), "已装备在武器页的消防斧明确选用即生效");
        Assert.That(pw.FactorReasons, Has.Some.EqualTo("破门（消防斧）工作量×0.75"));

        // 第二把消防斧在包里：仍只乘一次 0.75
        var spare = Axe(s);
        Assert.That(E.Preview(s, ActionRequest.Gather("w19_survey_b", spare.InstanceId)).FactorAtStart, Is.EqualTo(0.75).Within(1e-12), "多把不叠");
        // 地面的消防斧不隔空借用
        var ground = Axe(s, "w19_survey");
        Assert.That(E.CanStart(s, ActionRequest.Gather("w19_survey_b", ground.InstanceId)), Is.EqualTo("先拾取工具"));
        // 显式指定的那一把耐久不足：整次拒绝，不换撬棍或另一把
        spare.Durability = 0.5;
        Assert.That(E.CanStart(s, ActionRequest.Gather("w19_survey_b", spare.InstanceId)), Does.Contain("耐久不足"));
        // 其他工具（撬棍）不借消防斧特性；工作倍率接口只认实际物品 ID
        Assert.That(E.WorkFactor(s, ActionType.Gather, null, "w19_survey_b", true, "crowbar"), Is.EqualTo(1).Within(1e-12));
        Assert.That(E.WorkFactor(s, ActionType.Gather, null, "w19_survey_b", true, "fire_axe"), Is.EqualTo(0.75).Within(1e-12));
        Assert.That(E.WorkFactor(s, ActionType.Gather, null, "w19_survey_b", false, "fire_axe"), Is.EqualTo(1).Within(1e-12), "徒手不吃");
        Assert.That(E.WorkFactor(s, ActionType.Gather, null, "w20_powercut_a", true, "fire_axe"), Is.EqualTo(1).Within(1e-12), "名单外行没有破门");
    }

    /// <summary>TOOL-Q137-03：成功只磨 1、取消 0、耐久不足 1 拒绝；产量/经验/库存与原工具相同。</summary>
    [Test]
    public void TOOL_Q137_03_WearOutputXp()
    {
        var s = AtNode("w19_survey");
        var axe = Axe(s);
        var max = axe.Durability!.Value;
        var stock = s.Resources["w19_survey_b"].Available;
        Start(s, ActionRequest.Gather("w19_survey_b", axe.InstanceId));
        Assert.That(s.Main!.LockedItemIds, Does.Contain(axe.InstanceId), "锁实例至结算");
        E.Advance(s);
        Assert.That(E.Cancel(s).Ok, Is.True);
        Assert.That(axe.Durability, Is.EqualTo(max), "取消不磨损");
        Assert.That(s.Resources["w19_survey_b"].Available, Is.EqualTo(stock), "取消退回库存");
        var outItem = E.Resource("w19_survey_b").Outputs.Single();
        Start(s, ActionRequest.Gather("w19_survey_b", axe.InstanceId));
        Run(s);
        Assert.That(axe.Durability, Is.EqualTo(max - 1).Within(1e-9), "成功只磨 1（不叠战斗磨损）");
        Assert.That(Count(s, outItem.Key, "w19_survey"), Is.EqualTo(outItem.Value), "产量不增");
        Assert.That(s.Resources["w19_survey_b"].Available, Is.EqualTo(stock - outItem.Value));
        Assert.That(s.Skills["scavenging"].Xp, Is.EqualTo(E.FieldXp(E.Resource("w19_survey_b"))), "经验按原 30 分钟");
        axe.Durability = 0.9;
        Assert.That(E.CanStart(s, ActionRequest.Gather("w19_survey_b", axe.InstanceId)), Does.Contain("耐久不足"));
    }

    /// <summary>
    /// TOOL-Q137-04（S05/A06 本轮章算例，白天 M=1、余额 0）：原 30 分钟点工具后 25：1 级消防斧需 18.75、实际 20 分钟、余额 1.25；
    /// 搜集 5 级（3 级×0.95、工具运用×0.90）原工具需 21.375，消防斧合成 0.64125 触下限需 17.5、实际 20、余额 2.5；
    /// 原 20 分钟点 B=15，消防斧需 11.25，首笔 15 分钟余额 3.75，第二笔只再需 10 分钟。
    /// </summary>
    [Test]
    public void TOOL_Q137_04_FormulaFloorAndCarry()
    {
        var s = AtNode("w19_survey");
        var axe = Axe(s);
        var bar = Give(s, "crowbar");
        Assert.That(E.Resource("w19_survey_b").BaseMinutes, Is.EqualTo(30));
        var p = E.Preview(s, ActionRequest.Gather("w19_survey_b", axe.InstanceId));
        Assert.That((p.BaseMinutes, p.WorkMinutes, p.EstimatedMinutes), Is.EqualTo((25, 18.75, 20)));
        Assert.That(p.CarryAfter, Is.EqualTo(1.25).Within(1e-9));
        Assert.That(p.Notes, Has.Some.Contains("基础工时30→25分钟"));
        Start(s, ActionRequest.Gather("w19_survey_b", axe.InstanceId));
        Assert.That(s.Main!.BaseWorkMinutes, Is.EqualTo(18.75).Within(1e-9));
        var t0 = s.GameMinute;
        Run(s);
        Assert.That(s.GameMinute - t0, Is.EqualTo(20));
        Assert.That(E.CarryOf(s, E.CarryKeyOf(s, ActionType.Gather, "w19_survey_b")), Is.EqualTo(1.25).Within(1e-9));

        var s5 = AtNode("w19_survey");
        var axe5 = Axe(s5);
        var bar5 = Give(s5, "crowbar");
        Lv(s5, "scavenging", 5);
        var pc = E.Preview(s5, ActionRequest.Gather("w19_survey_b", bar5.InstanceId));
        Assert.That(pc.WorkMinutes, Is.EqualTo(21.375).Within(1e-9), "原工具 25×0.95×0.90");
        var pa = E.Preview(s5, ActionRequest.Gather("w19_survey_b", axe5.InstanceId));
        Assert.That(pa.WorkMinutes, Is.EqualTo(17.5).Within(1e-9), "0.95×0.90×0.75=0.64125 触 ×0.70 下限");
        Assert.That((pa.EstimatedMinutes, pa.CarryAfter), Is.EqualTo((20, (double?)2.5)));
        Assert.That(pa.FactorReasons, Has.Some.Contains("工具运用").And.Some.EqualTo("破门（消防斧）工作量×0.75").And.Some.EqualTo("求生工作量合成下限×0.7"));

        var r = AtNode("m01_road", 25);
        var axeR = Axe(r);
        Assert.That(E.Resource("m01_road_crate").BaseMinutes, Is.EqualTo(20));
        Start(r, ActionRequest.Gather("m01_road_crate", axeR.InstanceId));
        Assert.That(r.Main!.BaseWorkMinutes, Is.EqualTo(11.25).Within(1e-9));
        var r0 = r.GameMinute;
        Run(r);
        Assert.That(r.GameMinute - r0, Is.EqualTo(15), "首笔仍 15 分钟");
        var key = E.CarryKeyOf(r, ActionType.Gather, "m01_road_crate");
        Assert.That(E.CarryOf(r, key), Is.EqualTo(3.75).Within(1e-9), "余额 3.75 不丢");
        var p2 = E.Preview(r, ActionRequest.Gather("m01_road_crate", axeR.InstanceId));
        Assert.That(p2.EstimatedMinutes, Is.EqualTo(10), "第二笔只再需 10 分钟");
        Assert.That(bar.InstanceId + bar5.InstanceId, Is.GreaterThan(0));
    }

    /// <summary>TOOL-Q137-05：唯一奖励/记录/公共服务不受破门（显式消防斧整次拒绝、拖放不接受）；搜集锁闭点不开图、不写钥匙/开门/领取账。</summary>
    [Test]
    public void TOOL_Q137_05_NoClaimsNoDoors()
    {
        var s = AtNode("m01_road", 50);
        var axe = Axe(s);
        Assert.That(E.CanStart(s, ActionRequest.Gather("firetruck_cache", axe.InstanceId)), Is.EqualTo("这件工具对该资源点没有加成"));
        Assert.That(E.ResolveDrop(s, DropSource.Parse($"card:carried|fire_axe|{axe.InstanceId}")!, DropTarget.Parse("res:firetruck_cache")!).Ok, Is.False);
        var unique = Cfg.Resources.Where(r => r.IsCampaign).Select(r => r.NodeId).ToHashSet();
        var locked = ((JArray)Contract()["trait"]!["lockedResourceIds"]!).Select(x => (string)x!).ToList();
        Assert.That(locked.Where(unique.Contains), Is.Empty, "名单不含唯一奖励/核心记录/知识包/支援包");
        Assert.That((bool)Contract()["trait"]!["mapDoorsOrLocksOpened"]!, Is.False);

        var flags = s.Progress.Flags.ToHashSet();
        var visited = s.Locations.Where(kv => kv.Value.Visited).Select(kv => kv.Key).ToHashSet();
        var claims = s.Resources["firetruck_cache"].Available;
        Start(s, ActionRequest.Gather("m01_road_crate", axe.InstanceId));
        Run(s);
        Assert.That(s.Progress.Flags.Except(flags), Is.Empty, "不写钥匙/开门/永久解锁/领取事实");
        Assert.That(s.Locations.Where(kv => kv.Value.Visited).Select(kv => kv.Key), Is.EquivalentTo(visited), "不打开地图");
        Assert.That(s.Resources["firetruck_cache"].Available, Is.EqualTo(claims), "不重置 claim");
    }

    /// <summary>TOOL-Q137-06：旧在办动作沿开工快照（工具、需求工作、进度）不改价；后续新请求才用新系数；旧消防斧无需重造或修复即可用。</summary>
    [Test]
    public void TOOL_Q137_06_RunningSnapshotKept()
    {
        var s = AtNode("w19_survey");
        var bar = Give(s, "crowbar");
        Start(s, ActionRequest.Gather("w19_survey_b", bar.InstanceId));
        Assert.That(s.Main!.BaseWorkMinutes, Is.EqualTo(25).Within(1e-9));
        E.Advance(s);
        Axe(s);   // 开工后才拿到消防斧：在办动作不换工具、不改价
        var loaded = SaveSerializer.Deserialize(SaveSerializer.Serialize(s), Cfg, out _);
        Assert.That(loaded.Main!.BaseWorkMinutes, Is.EqualTo(25).Within(1e-9));
        Assert.That(loaded.Main.ToolInstanceId, Is.EqualTo(bar.InstanceId));
        var t0 = loaded.GameMinute;
        Run(loaded);
        Assert.That(loaded.GameMinute - t0, Is.EqualTo(20), "已推进 5 分钟后再 20 分钟，共 25");

        // 模拟改稿前开工的消防斧搜集（当时不算破门，需求工作 25）：读档后照快照结算，不中途套 ×0.75、不补磨或退磨
        var o = AtNode("w19_survey");
        var old = Axe(o);
        old.Durability = 30;   // 旧实例：不补满、不重造
        Start(o, ActionRequest.Gather("w19_survey_b", old.InstanceId));
        o.Main!.BaseWorkMinutes = 25;
        var lo = SaveSerializer.Deserialize(SaveSerializer.Serialize(o), Cfg, out _);
        var start = lo.GameMinute;
        Run(lo);
        Assert.That(lo.GameMinute - start, Is.EqualTo(25), "沿开工快照 25");
        var oldAxe = lo.Items.Single(i => i.ItemId == "fire_axe");
        Assert.That(oldAxe.Durability, Is.EqualTo(29).Within(1e-9), "只磨 1");
        // 新请求才用新系数：同一把旧斧
        var p = E.Preview(lo, ActionRequest.Gather("w19_survey_b", oldAxe.InstanceId));
        Assert.That(p.FactorAtStart, Is.EqualTo(0.75).Within(1e-12));
    }
}
