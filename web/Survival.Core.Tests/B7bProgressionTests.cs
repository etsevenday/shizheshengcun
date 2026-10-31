using Newtonsoft.Json.Linq;
using Survival.Core.Config;
using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 B7b（策划提交 d75d149 答复 Q53–Q61）：制造许可与拆解归属、取消制造等级工时、采集/搜集分类与经验、遗迹勘整、
/// 行旅已走边、特效药过渡许可、合格工具未开放、烹饪经验、工作量余量结转。
/// 出处：A06/S01/S05/S08/S11「2026-10-31 Q53—Q61成长与行动裁定（当前优先）」、progression-adjudications.json；验收 PROG-Q53—Q60、TIME-Q61a—e。
/// </summary>
public class B7bProgressionTests : TestBase
{
    private static JObject Contract() =>
        JObject.Parse(File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "design.progression.json")));

    private static ActionRequest Salvage(int id) => new ActionRequest { Type = ActionType.Craft, TargetId = "salvage_equipment", InstanceId = id };

    private static void Lv(GameState s, string id, int lv) => s.Skills[id].Level = lv;

    private static int Sum(GameState s, string itemId) => s.Items.Where(i => i.ItemId == itemId).Sum(i => i.Count);

    // ───────────── M157：制造许可、拆解归制造、取消制造等级工时（Q53/Q54/Q57） ─────────────

    [Test]
    public void PROG_Q53_OldRecipesKeepMinimums_NoW0WearGate()
    {
        var s = New();
        Assert.That(Cfg.Recipes.Single(r => r.Id == "make_metal_spear").MinSkills["crafting"], Is.EqualTo(1), "旧 T1 金属矛仍制造1级");
        Assert.That(Cfg.Recipes.Single(r => r.Id == "make_crossbow").MinSkills["crafting"], Is.EqualTo(1), "旧 T1 弩仍制造1级");
        foreach (var (k, v) in new[] { ("wood", 3), ("scrap", 2), ("fiber", 4), ("parts", 2), ("cloth", 1) }) E.DebugGive(s, k, v);
        Assert.That(E.CanStart(s, ActionRequest.Craft("make_crossbow")), Is.Null, "制造1级可做弩，不按 T0 倒锁");
        var node = Cfg.SkillNodes.Single(n => n.Id == "crafting_01");
        Assert.That((node.Status, node.Text.Contains("W0")), Is.EqualTo(("baseline", false)), "W0 误记删除，不新增穿戴限制");
        // 穿 T1 装备不看制造等级
        var it = E.DebugCreateEquipment(s, "machete", "white", ItemInstance.Carried);
        Assert.That(E.Equip(s, GameState.PlayerId, it.InstanceId, EquipSlots.Weapon1).Ok, Is.True);
    }

    [Test]
    public void PROG_Q54a_CraftingLevelTierMatrix_QualityAndScavengingDontMatter()
    {
        var byTier = new Dictionary<int, string> { [0] = "stone_knife", [1] = "machete", [2] = "guard_sword", [3] = "rescue_axe" };
        var allow = new Dictionary<int, int> { [1] = 0, [2] = 0, [3] = 1, [6] = 1, [7] = 2, [9] = 2, [10] = 3 };
        foreach (var (lv, maxTier) in allow)
            foreach (var (tier, id) in byTier)
                foreach (var q in new[] { "white", "blue" })
                {
                    var s = New();
                    Lv(s, "crafting", lv);
                    Lv(s, "scavenging", 10);   // 搜集等级不影响许可
                    var it = E.DebugCreateEquipment(s, id, q, ItemInstance.Carried);
                    var why = E.SalvageBlocked(s, it.InstanceId, out _);
                    if (tier <= maxTier) Assert.That(why, Is.Null, $"制造{lv} 拆 T{tier} {q}");
                    else Assert.That(why, Does.Contain($"拆解{tier}阶装备需要制造"), $"制造{lv} 拆 T{tier} {q}");
                }
        var c = (JArray)Contract()["manufacturing"]!["dismantleStages"]!;
        Assert.That(Cfg.Progression.Dismantle.Stages.Select(x => (x.CraftingLevel, x.MaxTier)),
            Is.EqualTo(c.Select(x => ((int)x["craftingLevel"]!, (int)x["maxTier"]!))), "与合同同源");
        Assert.That(Cfg.SkillNodes.Count, Is.EqualTo(75), "拆解许可是常驻被动，不加第六节点");
    }

    [Test]
    public void PROG_Q54b_DismantleXpTwoOnce_CancelZero()
    {
        var s = New();
        var knife = E.DebugCreateEquipment(s, "stone_knife", "white", ItemInstance.Carried);
        var p = E.Preview(s, Salvage(knife.InstanceId));
        Assert.That(p.XpGains, Is.EqualTo(new[] { "制造经验+2" }));
        Start(s, Salvage(knife.InstanceId));
        E.Advance(s);
        Assert.That(E.Cancel(s).Ok, Is.True);
        Assert.That(s.Skills["crafting"].Xp, Is.Zero, "取消 0");
        Start(s, Salvage(knife.InstanceId));
        Run(s);
        Assert.That(s.Skills["crafting"].Xp, Is.EqualTo(2), "完成 +2");
        Assert.That(s.Skills["scavenging"].Xp, Is.Zero, "不再给搜集经验");
        Assert.That(s.Log.Last(l => l.Text.Contains("拆解")).Text, Does.Contain("制造经验+2"));
        Assert.That(E.Start(s, Salvage(knife.InstanceId)).Ok, Is.False, "源实例已消耗，重复请求无效");
        Assert.That(s.Skills["crafting"].Xp, Is.EqualTo(2));
    }

    [Test]
    public void PROG_Q54c_ExpertAtCraftingTen_NotScavengingTen()
    {
        var s = New();
        var spear = E.DebugCreateEquipment(s, "wooden_spear", "green", ItemInstance.Carried);
        Lv(s, "scavenging", 10);
        Assert.That(E.SalvageExpertBonus(s, spear).extra, Is.Zero, "搜集10级不追加");
        Lv(s, "crafting", 9);
        Assert.That(E.SalvageExpertBonus(s, spear).extra, Is.Zero, "制造9级不追加");
        Lv(s, "crafting", 10);
        var (extra, note) = E.SalvageExpertBonus(s, spear);
        Assert.That(extra, Is.EqualTo(1));
        Assert.That(note, Does.StartWith("制造10级·回收专家"));
        Assert.That(E.CraftingStandingLines(s.Player), Has.Some.Contains("可拆解0—3阶装备").And.Some.Contains("回收专家").And.Some.Contains("生效中"));
        var s10 = Cfg.SkillNodes.Single(n => n.Id == "scavenging_10");
        Assert.That((s10.Name, s10.Effects.Single().Key), Is.EqualTo(("周密检索", "work.scavenge")));
    }

    [Test]
    public void PROG_Q57_NoCraftingLevelWorkFactor_FourFamilies()
    {
        foreach (var lv in new[] { 1, 10 })
        {
            var s = New();
            Lv(s, "crafting", lv);
            E.DebugGive(s, "cloth", 4);
            E.DebugGive(s, "herb", 4);
            var p = E.Preview(s, ActionRequest.Craft("bandage", batchCount: 2));
            Assert.That(p.EstimatedMinutes, Is.EqualTo(30), $"制造{lv}级 绷带×2 = 30 分钟");
            Start(s, ActionRequest.Craft("bandage", batchCount: 2));
            var t0 = s.GameMinute;
            Assert.That(s.Main!.BaseWorkMinutes, Is.EqualTo(30).Within(1e-9));
            Run(s);
            Assert.That(s.GameMinute - t0, Is.EqualTo(30), "预估与推进一致");
            var it = E.DebugCreateEquipment(s, "stone_knife", "white", ItemInstance.Carried);
            Start(s, Salvage(it.InstanceId));
            Assert.That(s.Main!.BaseWorkMinutes, Is.EqualTo(15).Within(1e-9), $"制造{lv}级拆解 15");
            E.Cancel(s);
        }
        // 战斗体力同形公式保留
        Assert.That(E.SkillEnergyCost(1, 10), Is.EqualTo(1 / 1.25).Within(1e-12));
    }

    [Test]
    public void PROG_Q57_OldInProgressCraft_KeepsSavedWork()
    {
        var s = New();
        E.DebugGive(s, "cloth", 2);
        E.DebugGive(s, "herb", 2);
        Start(s, ActionRequest.Craft("bandage"));
        s.Main!.BaseWorkMinutes = 15 * 0.8;   // 模拟旧版本开工时按制造10级折减后保存的工作量
        var loaded = SaveSerializer.Deserialize(SaveSerializer.Serialize(s));
        Assert.That(loaded.Main!.BaseWorkMinutes, Is.EqualTo(12).Within(1e-9), "读档不重套新公式");
    }

    // ───────────── M158：采集/搜集分类与经验、遗迹勘整、合格工具未开放（Q55/Q59） ─────────────

    private GameState At(string loc, int progress = 100, uint seed = 7)
    {
        var s = New(seed);
        E.DebugTeleport(s, loc);
        E.DebugSetProgress(s, loc, progress);
        return s;
    }

    private void Do(GameState s, ActionRequest r)
    {
        Start(s, r);
        s.Main!.HazardHit = false;
        Run(s);
    }

    [Test]
    public void PROG_Q55a_LegacyTwentyRows_Classification_PassiveScopes()
    {
        var rows = ((JArray)Contract()["fieldExperience"]!["resourceRows"]!).ToDictionary(r => (string)r["resourceId"]!);
        Assert.That(rows, Has.Count.EqualTo(116), "合同 116 行（96 行属 v3 地图，只登记）");
        foreach (var r in Cfg.Resources)
            Assert.That(GameEngine.GatherSkillOf(r), Is.EqualTo((string)rows[r.NodeId]["proficiencyId"]!), r.NodeId);
        var heap = E.Resource("metal_heap");
        Assert.That(GameEngine.GatherSkillOf(heap), Is.EqualTo("scavenging"), "废金属堆改练搜集");
        var s = At("scrapyard");
        Lv(s, "gathering", 10);
        Lv(s, "scavenging", 1);
        Assert.That(E.WorkFactor(s, ActionType.Gather, null, "metal_heap"), Is.EqualTo(1).Within(1e-12), "工业资源不吃熟练采伐");
        s.Resources["metal_heap"].Available = 10;
        Assert.That(E.Preview(s, ActionRequest.Gather("metal_heap")).Outputs.Single().Count, Is.EqualTo(2), "不吃合理取材/精取");
        Assert.That(GameEngine.IsWoodOrScrapGather(heap), Is.True, "采集手套物理条件仍适用");
        Lv(s, "scavenging", 3);
        Assert.That(E.WorkFactor(s, ActionType.Gather, null, "metal_heap"), Is.EqualTo(0.95).Within(1e-12), "分类勘整×0.95");
        Lv(s, "scavenging", 10);
        Assert.That(E.WorkFactor(s, ActionType.Gather, null, "metal_heap"), Is.EqualTo(0.90).Within(1e-12), "周密检索×0.90 替代3级");
    }

    [Test]
    public void PROG_Q55b_XpFormula_ThreePerNode_SixtyPerSkill_StartDayLocked()
    {
        foreach (var r in Cfg.Resources)
            Assert.That(E.FieldXp(r), Is.EqualTo(Math.Clamp(2 * (int)Math.Ceiling(r.BaseMinutes / 5.0), 2, 12)), r.NodeId);
        Assert.That((E.FieldXp(E.Resource("water")), E.FieldXp(E.Resource("berries")), E.FieldXp(E.Resource("pot_box"))), Is.EqualTo((4, 8, 12)));
        // 村庄 4 个箱子 48 + 勘整 12 = 60，第二次勘整日额已满（仍正常完成）
        var s = At("village");
        foreach (var id in new[] { "supply_box", "misc_box", "village_weapon_locker", "village_cold_box" }) Do(s, ActionRequest.Gather(id));
        Assert.That(s.Skills["scavenging"].Xp, Is.EqualTo(48));
        Do(s, ActionRequest.Survey());
        Assert.That(s.Skills["scavenging"].Xp, Is.EqualTo(60), "每门每日 60");
        Assert.That(E.Preview(s, ActionRequest.Survey()).XpGains.Single(), Does.Contain("已满60"));
        Do(s, ActionRequest.Survey());
        Assert.That(s.Skills["scavenging"].Xp, Is.EqualTo(60));
        Assert.That(s.Log.Last(l => l.Text.Contains("遗迹勘整完成")).Text, Does.Contain("今日第2/2次").And.Contain("已满60"));
        // 开工日锁：23:50 开工、次日完成仍记开工日
        var t = New(7);
        E.DebugTeleport(t, "woods");
        E.DebugSetProgress(t, "woods", 75);
        E.DebugSetStats(t, 20, 90, 90, 18, 90, 60);
        AdvanceTo(t, At(1, 23, 50));
        t.Resources["berries"].Available = 99;
        Do(t, ActionRequest.Gather("berries"));
        Assert.That(Clock.Day(t.GameMinute), Is.EqualTo(2));
        Assert.That(t.Player.DailyCounts.ContainsKey("1|gather|berries"), Is.True, "按开工日记账");
        Assert.That(t.Player.DailyCounts.ContainsKey("2|gather|berries"), Is.False);
    }

    [Test]
    public void PROG_Q55c_Survey_TwoPerDay_TwelveXp_NoOutputs_NoDiscount()
    {
        var s = At("hardware", 75);
        Assert.That(E.CanStart(s, ActionRequest.Survey()), Does.Contain("探索度100%"));
        E.DebugSetProgress(s, "hardware", 100);
        Assert.That(E.CanStart(s, ActionRequest.Survey()), Is.Null);
        var field = At("field");
        Assert.That(E.CanStart(field, ActionRequest.Survey()), Does.Contain("没有可勘整"));
        // 装备与搜集被动不缩短；不产物、不动库存与清理次数
        E.DebugCreateEquipment(s, "scavenge_vest", "white", ItemInstance.EquipOwner(GameState.PlayerId, EquipSlots.Top));
        Lv(s, "scavenging", 10);
        var items0 = s.Items.Select(i => (i.ItemId, i.Count, i.Owner)).ToList();
        var res0 = s.Resources.ToDictionary(k => k.Key, k => k.Value.Available);
        var clear0 = s.Locations["hardware"].ClearCharges;
        var p = E.Preview(s, ActionRequest.Survey());
        Assert.That(p.EstimatedMinutes, Is.EqualTo(30), "工具/装备/搜集速度被动不缩短");
        Assert.That(p.Outputs, Is.Empty);
        var lv = s.Skills["scavenging"];
        lv.Level = 9; lv.Xp = 0;
        var t0 = s.GameMinute;
        Start(s, ActionRequest.Survey());
        E.Advance(s);
        Assert.That(E.Cancel(s).Ok, Is.True);
        Assert.That((lv.Xp, E.SurveyDoneToday(s, "hardware")), Is.EqualTo((0L, 0)), "取消不给经验、不占次数");
        Do(s, ActionRequest.Survey());
        Do(s, ActionRequest.Survey());
        Assert.That(lv.Xp, Is.EqualTo(24), "每次 +12");
        Assert.That(E.CanStart(s, ActionRequest.Survey()), Does.Contain("今日已在这里勘整2次"));
        Assert.That(s.Items.Select(i => (i.ItemId, i.Count, i.Owner)).ToList(), Is.EquivalentTo(items0), "0 物品");
        Assert.That(s.Resources.ToDictionary(k => k.Key, k => k.Value.Available), Is.EqualTo(res0), "不动库存/箱子");
        Assert.That(s.Locations["hardware"].ClearCharges, Is.EqualTo(clear0), "不扣清理次数");
        // 风险按点：废料场 10%、危险辨识×0.8
        var y = At("scrapyard");
        Assert.That(E.SurveyHazardChance(y, "scrapyard"), Is.EqualTo(0.10).Within(1e-12));
        Lv(y, "scavenging", 7);
        Assert.That(E.SurveyHazardChance(y, "scrapyard"), Is.EqualTo(0.08).Within(1e-12));
        Assert.That(E.SurveyHazardChance(y, "clinic"), Is.Zero);
        // 存读档后续原动作
        var z = At("clinic");
        Start(z, ActionRequest.Survey());
        E.Advance(z);
        var loaded = SaveSerializer.Deserialize(SaveSerializer.Serialize(z));
        Run(loaded);
        Assert.That(loaded.Skills["scavenging"].Xp, Is.EqualTo(12));
    }

    [Test]
    public void PROG_Q59_ToolUse_ShownClosed_NoBonus()
    {
        var n = Cfg.SkillNodes.Single(x => x.Id == "scavenging_05");
        Assert.That(n.Status, Is.EqualTo("closed"));
        Assert.That(GameEngine.PassiveStatusText(n), Does.Contain("暂未开放"));
        Assert.That(n.Text, Does.Contain("暂未开放"));
        var s = At("village");
        Lv(s, "scavenging", 5);
        Assert.That(E.WorkFactor(s, ActionType.Gather, null, "misc_box"), Is.EqualTo(0.95).Within(1e-12), "只有分类勘整，工具运用不加成");
        Assert.That(E.SurvivalPassiveLines(s.Player), Has.Some.Contains("工具运用").And.Some.Contains("暂未开放"));
    }

    // ───────────── M159：烹饪经验（Q60） ─────────────

    private (GameState s, ItemInstance pot) PotOnFire(string? water, int units)
    {
        var s = New();
        var pot = First(s, "pot");
        if (water != null && units > 0)
        {
            E.DebugGive(s, water, units, "camp");
            Assert.That(E.DoInstant(s, InstantRequest.FillPot(First(s, water, "camp").InstanceId, pot.InstanceId, units)).Ok, Is.True);
        }
        Assert.That(E.DoInstant(s, InstantRequest.PutInFire(pot.InstanceId)).Ok, Is.True);
        var fac = s.Locations["camp"].Facility;
        fac.FireLit = true;
        fac.FuelMinutes = 240;
        return (s, pot);
    }

    private void Wait(GameState s, int minutes)
    {
        var end = s.GameMinute + minutes;
        while (s.GameMinute < end) { if (s.Main == null) Start(s, ActionRequest.Wait(30)); E.Advance(s); }
        if (s.Main != null) E.Cancel(s);
    }

    [Test]
    public void PROG_Q60a_FourFoods_ThreeEach_Reboil0_DailyCap30()
    {
        Assert.That(new[] { "roast_root", "boil_root", "roast_meat", "boil_meat" }.Select(E.CookFoodXpFor), Is.All.EqualTo(3), "30 受热分钟 = 3");
        Assert.That(E.CookFoodXpFor("reboil_cooked_meat"), Is.Zero, "复煮 0");
        var (s, pot) = PotOnFire("dirty_water", 2);
        E.DebugGive(s, "raw_root", 2, "camp");
        Assert.That(E.DoInstant(s, InstantRequest.PutFoodInPot(First(s, "raw_root", "camp").InstanceId, pot.InstanceId, 2)).Ok, Is.True);
        Assert.That(E.ProcessOf(s, E.PotFood(s, pot.InstanceId).First())!.InitiatorActorId, Is.EqualTo(GameState.PlayerId), "投料即锁发起者");
        Wait(s, 45);
        Assert.That(E.PotFood(s, pot.InstanceId).Select(f => f.ItemId), Is.All.EqualTo("boiled_root"));
        Assert.That(s.Skills["cooking"].Xp, Is.EqualTo(2 + 3 + 3), "1L 净化 2 + 两份水煮各 3");
        Assert.That(s.Log.Any(l => l.Text.Contains("已煮成") && l.Text.Contains("烹饪经验+3")), Is.True);
        // 日 30：已得 28，再烤一份只给 2
        var t = New();
        var fac = t.Locations["camp"].Facility;
        fac.FireLit = true; fac.FuelMinutes = 240;
        t.Player.DailyCounts[$"{Clock.Day(t.GameMinute)}|xp|cooking"] = 28;
        E.DebugGive(t, "raw_root", 1, "camp");
        Assert.That(E.DoInstant(t, InstantRequest.PutInFire(First(t, "raw_root", "camp").InstanceId, 1)).Ok, Is.True);
        Wait(t, 30);
        Assert.That(t.Skills["cooking"].Xp, Is.EqualTo(2), "食品每日合计 30");
    }

    [Test]
    public void PROG_Q60b_Water_500mlPerXp_DailyFour()
    {
        var (s, pot) = PotOnFire("dirty_water", 1);
        Wait(s, 15);
        Assert.That(s.Skills["cooking"].Xp, Is.EqualTo(1), "500ml = 1");
        s.Player.DailyCounts[$"{Clock.Day(s.GameMinute)}|waterXp|cooking"] = 4;
        var u = E.PotUnits(s, pot.InstanceId);
        E.DebugGive(s, "dirty_water", 1, "camp");
        // 倒掉净水再装脏水另做一锅：日 4 已满，不再给
        foreach (var w in s.Items.Where(i => i.Owner == ItemInstance.ContainerOwner(pot.InstanceId)).ToList()) s.Items.Remove(w);
        Assert.That(E.DoInstant(s, InstantRequest.FillPot(First(s, "dirty_water", "camp").InstanceId, pot.InstanceId, 1)).Ok, Is.True);
        Wait(s, 15);
        Assert.That(s.Skills["cooking"].Xp, Is.EqualTo(1), "净化每日最多 4");
        Assert.That(u, Is.EqualTo(1));
    }

    [Test]
    public void PROG_Q60c_OldProcessWithoutInitiator_NoXp_CancelAndTakeOutZero()
    {
        var s = New();
        var fac = s.Locations["camp"].Facility;
        fac.FireLit = true; fac.FuelMinutes = 240;
        E.DebugGive(s, "raw_root", 1, "camp");
        Assert.That(E.DoInstant(s, InstantRequest.PutInFire(First(s, "raw_root", "camp").InstanceId, 1)).Ok, Is.True);
        var root = E.FireContents(s, "camp").Single(i => !E.IsPot(i));
        E.ProcessOf(s, root)!.InitiatorActorId = null;   // 模拟 B7b 之前建立的旧进程
        Assert.That(E.CookXpNote(s, E.ProcessOf(s, root)!), Does.Contain("旧进程"));
        var loaded = SaveSerializer.Deserialize(SaveSerializer.Serialize(s));
        Wait(loaded, 30);
        Assert.That(loaded.Items.Single(i => i.InstanceId == root.InstanceId).ItemId, Is.EqualTo("cooked_root"));
        Assert.That(loaded.Skills["cooking"].Xp, Is.Zero, "旧进程无发起者不补经验");
        // 投料、点火、等待都不发；未完成就取出也不发
        var t = New();
        var tf = t.Locations["camp"].Facility;
        tf.FireLit = true; tf.FuelMinutes = 240;
        E.DebugGive(t, "raw_root", 1, "camp");
        Assert.That(E.DoInstant(t, InstantRequest.PutInFire(First(t, "raw_root", "camp").InstanceId, 1)).Ok, Is.True);
        Wait(t, 20);
        Assert.That(t.Skills["cooking"].Xp, Is.Zero, "投料/等待不发");
    }
}
