using Survival.Core.Config;
using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 B10c：策划提交 8098c4a 答复 Q112 选 A——锻炉每份配方额外燃料木炭1，N 份扣 N（N 是配方执行份数，不是成品件数）；
/// 原料炭 m×N + 燃料炭 N = 总炭 (m+1)×N，合计校验、一次预留、成功一次核销；取消整批返料；旧在办不追溯；火堆煅烧与拆解预算不受影响。
/// 验收 A10 WS-Q112-01—04、workshop-adjudications.json forgeBatchFuel 三个算例。
/// </summary>
public class B10cForgeFuelTests : TestBase
{
    private static ActionRequest Craft(string id, int batch = 1, string? requestId = null)
    {
        var r = ActionRequest.Craft(id, null, null, batch);
        r.RequestId = requestId;
        return r;
    }

    /// <summary>新局、学会全部蓝图、营地建成全部工坊、制造10级；清掉开局里可能有的相关材料，只放 give 的数量在营地地面。</summary>
    private GameState Forge(params (string id, int n)[] give)
    {
        var s = New();
        E.DebugLearnAllKnowledge(s);
        E.DebugBuildAllWorkshops(s);
        s.Skills["crafting"].Level = 10;
        var ids = new[] { "charcoal", "iron_ore", "steel", "spring", "leather", "fastener" };
        s.Items.RemoveAll(i => ids.Contains(i.ItemId));
        foreach (var (id, n) in give) E.CreateItems(s, id, n, s.CurrentLocation);
        return s;
    }

    private static int Reserved(GameState s, string itemId) => s.Items.Where(i => i.ItemId == itemId && i.ReservedBy != null).Sum(i => i.Count);

    private static RecipeConfig Rc(string id) => Cfg.Recipes.Single(r => r.Id == id);

    /// <summary>workshop-adjudications.json forgeBatchFuel 三个算例 + 公式（分账函数与预览同源）。</summary>
    [Test]
    public void WS_Q112_00_AdjudicationExamples_Split()
    {
        Assert.That(GameEngine.RecipeItemSplit(Rc("make_steel"), "charcoal", 1), Is.EqualTo((1, 1, 2)));
        Assert.That(GameEngine.RecipeItemSplit(Rc("make_steel"), "charcoal", 3), Is.EqualTo((3, 3, 6)));
        Assert.That(GameEngine.RecipeItemSplit(Rc("make_spring"), "charcoal", 3), Is.EqualTo((0, 3, 3)));
        Assert.That(GameEngine.RecipeItemSplit(Rc("make_guard_sword"), "charcoal", 3), Is.EqualTo((0, 3, 3)), "锻炉装备单份输出1件，做3份燃料3");

        // 所有带燃料的配方都是锻炉、每份木炭1；锻炉配方都带燃料（appliesToStation forge）
        Assert.That(Cfg.Recipes.Where(r => r.Fuel.Count > 0 && !r.Id.StartsWith("ex_")).Select(r => r.Station).Distinct(), Is.EqualTo(new[] { "forge" }), "B16 陶壳的陶窑燃料另计");
        Assert.That(Cfg.Recipes.Where(r => r.Station == "forge").All(r => r.Fuel.Count == 1 && r.Fuel["charcoal"] == 1), Is.True);

        var s = Forge(("iron_ore", 9), ("charcoal", 6));
        var note = E.Preview(s, Craft("make_steel", 3)).Notes.Single(n => n.StartsWith("执行3份配方"));
        Assert.That(note, Does.Contain("成品钢材6").And.Contain("原料木炭3＋燃料木炭3＝共6").And.Contain("锻炉每份配方另需木炭1"));
        Assert.That(note, Does.Not.Contain("每批"));
        var t = Forge(("steel", 3), ("charcoal", 3));
        Assert.That(E.Preview(t, Craft("make_spring", 3)).Notes, Has.Some.Contains("成品弹簧6").And.Some.Contains("燃料木炭3（原料无木炭）"));
        Assert.That(E.Preview(t, Craft("make_spring", 3)).ConfirmText, Is.EqualTo("开始制作×3份"));
    }

    /// <summary>WS-Q112-01：单份钢材2要炭2；3份钢材6要矿9、炭6；3份弹簧6要钢3、燃料炭3。完成后正好核销、产物=单份输出×N。</summary>
    [Test]
    public void WS_Q112_01_SteelAndSpring_ConsumeExactly()
    {
        var s = Forge(("iron_ore", 3), ("charcoal", 2));
        Start(s, Craft("make_steel"));
        Assert.That(Reserved(s, "charcoal"), Is.EqualTo(2));
        Run(s);
        Assert.That(Count(s, "charcoal"), Is.EqualTo(0));
        Assert.That(Count(s, "iron_ore"), Is.EqualTo(0));
        Assert.That(Count(s, "steel"), Is.EqualTo(2));

        s = Forge(("iron_ore", 9), ("charcoal", 6));
        Start(s, Craft("make_steel", 3));
        Assert.That(s.Main!.BatchCount, Is.EqualTo(3));
        Assert.That(s.Main.Label, Is.EqualTo("制作钢材×3份"));
        Assert.That(Reserved(s, "charcoal"), Is.EqualTo(6));
        Assert.That(Reserved(s, "iron_ore"), Is.EqualTo(9));
        Run(s);
        Assert.That(Count(s, "charcoal") + Count(s, "iron_ore"), Is.EqualTo(0));
        Assert.That(Count(s, "steel"), Is.EqualTo(6), "产物按份数×单份输出，不按产物数反推燃料");

        s = Forge(("steel", 3), ("charcoal", 3));
        Start(s, Craft("make_spring", 3));
        Assert.That(Reserved(s, "charcoal"), Is.EqualTo(3));
        Run(s);
        Assert.That(Count(s, "charcoal") + Count(s, "steel"), Is.EqualTo(0));
        Assert.That(Count(s, "spring"), Is.EqualTo(6));
    }

    /// <summary>WS-Q112-02：按 N 预留/核销，原料炭与燃料炭不共用同份；只够其中一项时整批拒绝，什么都不扣不预留；原因写分账与缺几个。</summary>
    [Test]
    public void WS_Q112_02_CombinedCheck_RejectWhole()
    {
        // 5 炭：够原料炭3或燃料炭3，但不够合计6
        var s = Forge(("iron_ore", 9), ("charcoal", 5));
        var before = SaveSerializer.Serialize(s);
        var why = E.CanStart(s, Craft("make_steel", 3));
        Assert.That(why, Does.Contain("缺少木炭").And.Contain("现有5/需6").And.Contain("原料木炭3＋燃料木炭3").And.Contain("缺1")
            .And.Contain("锻炉每份配方另需木炭1"));
        Assert.That(E.Start(s, Craft("make_steel", 3)).Ok, Is.False);
        Assert.That(SaveSerializer.Serialize(s), Is.EqualTo(before), "拒绝时无先扣料、无预留");

        // 只缺燃料（弹簧不用原料炭）
        var t = Forge(("steel", 3), ("charcoal", 2));
        Assert.That(E.CanStart(t, Craft("make_spring", 3)), Does.Contain("缺少燃料木炭").And.Contain("现有2/需3").And.Contain("缺1"));
        Assert.That(E.CanStart(t, Craft("make_spring", 2)), Is.Null);

        // 炭够、矿不够：同样整批拒绝
        var u = Forge(("iron_ore", 8), ("charcoal", 6));
        Assert.That(E.CanStart(u, Craft("make_steel", 3)), Does.Contain("缺少铁矿石").Or.Contain("（8/9）"));
        Assert.That(Reserved(u, "charcoal"), Is.EqualTo(0));

        // 多余库存不被占：8 炭做 3 份只预留 6，剩 2 份可自由使用
        var v = Forge(("iron_ore", 9), ("charcoal", 8));
        Start(v, Craft("make_steel", 3));
        Assert.That(Reserved(v, "charcoal"), Is.EqualTo(6));
        Assert.That(E.AvailableCount(v, "charcoal", true), Is.EqualTo(2));
        Run(v);
        Assert.That(Count(v, "charcoal"), Is.EqualTo(2));
    }

    /// <summary>WS-Q112-03：预览/改数量/非法 N 不扣；取消整批返原料与燃料一次到当地地面、不退时间、无半成品；重复 requestId 与完成后推进不双扣。</summary>
    [Test]
    public void WS_Q112_03_PreviewIllegalCancelIdempotent()
    {
        var s = Forge(("iron_ore", 9), ("charcoal", 6));
        var before = SaveSerializer.Serialize(s);
        foreach (var n in new[] { 1, 2, 3, 4 }) E.Preview(s, Craft("make_steel", n));
        Assert.That(E.Preview(s, Craft("make_steel", 4)).Ok, Is.False, "超过材料上限拒绝");
        foreach (var n in new[] { 0, -1 })
        {
            Assert.That(E.CanStart(s, Craft("make_steel", n)), Does.Contain("正整数"), "N≤0 拒绝、不静默改成1");
            Assert.That(E.Start(s, Craft("make_steel", n)).Ok, Is.False);
        }
        Assert.That(SaveSerializer.Serialize(s), Is.EqualTo(before), "预览、改数量、非法 N 都不扣不预留");

        // 重复 requestId：第二次不再预留
        Start(s, Craft("make_steel", 3, "q112-r1"));
        Assert.That(E.Start(s, Craft("make_steel", 3, "q112-r1")).Ok, Is.True, "重放视为已受理");
        Assert.That(Reserved(s, "charcoal"), Is.EqualTo(6));
        Assert.That(Count(s, "charcoal"), Is.EqualTo(6));

        // 推进一段后取消：9 矿 + 6 炭（原料3+燃料3）全部回营地地面、解除预留；时间不退；没有钢材
        var t0 = s.GameMinute;
        E.Advance(s);
        E.Advance(s);
        Assert.That(s.GameMinute, Is.GreaterThan(t0));
        var t1 = s.GameMinute;
        Assert.That(E.Cancel(s).Ok, Is.True);
        Assert.That(s.Main, Is.Null);
        Assert.That(s.GameMinute, Is.EqualTo(t1), "不返已耗时间");
        Assert.That(Count(s, "charcoal", "camp"), Is.EqualTo(6));
        Assert.That(Count(s, "iron_ore", "camp"), Is.EqualTo(9));
        Assert.That(Reserved(s, "charcoal") + Reserved(s, "iron_ore"), Is.EqualTo(0));
        Assert.That(Count(s, "steel"), Is.EqualTo(0), "无半成品");

        // 重新开工完成后：只核销一次，继续推进/重放同一完成不再扣
        Start(s, Craft("make_steel", 3, "q112-r2"));
        Run(s);
        Assert.That(Count(s, "steel"), Is.EqualTo(6));
        E.CreateItems(s, "charcoal", 4, "camp");
        Assert.That(E.Start(s, Craft("make_steel", 3, "q112-r2")).Ok, Is.True, "同一请求重放");
        Assert.That(s.Main, Is.Null, "重放不再开工");
        Steps(s, 3);
        Assert.That(Count(s, "charcoal"), Is.EqualTo(4), "完成重放不双扣炭");
        Assert.That(Count(s, "steel"), Is.EqualTo(6), "不双发产物");
    }

    /// <summary>
    /// WS-Q112-04：进行中旧动作按保存时的份数与预留快照完成，不补扣（用“只预留了原料、未预留燃料”的旧在办模拟）；存读档往返不变；
    /// 火堆煅烧仍按 Q111（不吃锻炉 N 倍规则）；拆解预算按标准配方材料价值、不含燃料炭。
    /// </summary>
    [Test]
    public void WS_Q112_04_LegacySnapshot_FireAndSalvageUnchanged()
    {
        var s = Forge(("iron_ore", 9), ("charcoal", 6));
        Start(s, Craft("make_steel", 3));
        // 旧快照：只预留了原料炭 3（把燃料炭 3 份的预留拿掉）
        var fuelIds = s.Main!.ReservedItemIds.Select(id => s.Items.First(i => i.InstanceId == id)).Where(i => i.ItemId == "charcoal").ToList();
        var drop = 3;
        foreach (var inst in fuelIds)
        {
            if (drop == 0) break;
            if (inst.Count > drop)
            {
                // 拆出 drop 份不预留的
                E.CreateItems(s, "charcoal", drop, "camp");
                inst.Count -= drop;
                drop = 0;
            }
            else
            {
                drop -= inst.Count;
                inst.ReservedBy = null;
                inst.Owner = "camp";
                s.Main.ReservedItemIds.Remove(inst.InstanceId);
            }
        }
        Assert.That(Reserved(s, "charcoal"), Is.EqualTo(3));
        var t = SaveSerializer.Deserialize(SaveSerializer.Serialize(s), Cfg);
        Assert.That(t.Main!.BatchCount, Is.EqualTo(3));
        Run(t);
        Assert.That(Count(t, "steel"), Is.EqualTo(6));
        Assert.That(Count(t, "charcoal"), Is.EqualTo(3), "不按新规则补扣燃料");

        // 火堆煅烧：仍是 fire_charcoal 被动进程（木4→炭3），不是配方、没有锻炉燃料
        Assert.That(Cfg.Recipes.Any(r => r.Outputs.ContainsKey("charcoal")), Is.False);
        Assert.That(Cfg.Recipes.Where(r => r.Fuel.Count > 0 && !r.Id.StartsWith("ex_")).All(r => r.Station == "forge"), Is.True);
        var f = New();
        f.Items.RemoveAll(i => i.ItemId == "wood" || i.ItemId == "charcoal");
        E.CreateItems(f, "wood", 8, "camp");
        Assert.That(E.DoInstant(f, InstantRequest.LoadCharcoal()).Ok, Is.True);
        Assert.That(E.DoInstant(f, InstantRequest.LoadCharcoal()).Ok, Is.True);
        var fac = f.Locations["camp"].Facility;
        fac.FireLit = true;
        fac.FuelMinutes = 60;
        var end = f.GameMinute + 60;
        while (f.GameMinute < end)
        {
            if (f.Main == null) Start(f, ActionRequest.Wait(60));
            E.Advance(f);
        }
        if (f.Main != null) E.Cancel(f);
        Assert.That(Count(f, "charcoal"), Is.EqualTo(6), "两批并行各出3炭");
        Assert.That(fac.FuelMinutes, Is.EqualTo(0), "并行不倍扣：火堆只烧 60 分钟燃料");

        // 拆解预算：可自制锻炉装备的 recipeValue = 配方原料价值（不含燃料炭）
        var mv = Cfg.Equipment.Salvage.MaterialValues;
        foreach (var rc in Cfg.Recipes.Where(r => r.Station == "forge" && r.Group == "equipment"))
        {
            var outId = rc.Outputs.Keys.Single();
            var def = E.SalvageDef(outId);
            Assert.That(def, Is.Not.Null, outId);
            var ingredients = rc.Inputs.Sum(kv => (mv.TryGetValue(kv.Key, out var v) ? v : 0) * kv.Value);
            Assert.That(def!.RecipeValue, Is.EqualTo(ingredients), $"{outId} 拆解预算不含燃料炭");
        }
    }
}
