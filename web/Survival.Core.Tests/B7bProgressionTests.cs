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
        Assert.That(n.Text, Does.Not.Contain("暂未开放"), "B7b 试玩修复：说明不再重复状态");
        var s = At("village");
        Lv(s, "scavenging", 5);
        Assert.That(E.WorkFactor(s, ActionType.Gather, null, "misc_box"), Is.EqualTo(0.95).Within(1e-12), "只有分类勘整，工具运用不加成");
        var line = E.SurvivalPassiveLines(s.Player).Single(x => x.Contains("工具运用"));
        Assert.That(line.Split("暂未开放").Length - 1, Is.EqualTo(1), "“暂未开放”只出现一次：" + line);
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

    // ───────────── M160：工作量余额结转、行旅已走边、特效药过渡许可（Q61/Q56/Q58） ─────────────

    private int Timed(GameState s, ActionRequest r)
    {
        var t0 = s.GameMinute;
        Start(s, r);
        s.Main!.HazardHit = false;
        Run(s);
        return s.GameMinute - t0;
    }

    [Test]
    public void TIME_Q61a_W9_FiveTimes45_Bandage14_25_SevenTimes100()
    {
        var s = At("stream");
        Lv(s, "gathering", 7);                     // 熟练采伐：脏水 10×0.9 = 9
        var mins = new List<int>();
        var credits = new List<double>();
        for (var k = 0; k < 5; k++)
        {
            SetStats(s, 20, 90, 90, 18, 90, 80);
            mins.Add(Timed(s, ActionRequest.Gather("water")));
            credits.Add(E.CarryOf(s, "p0|gathering"));
        }
        Assert.That(mins, Is.EqualTo(new[] { 10, 10, 10, 10, 5 }), "前4次各10分钟，第5次5分钟，共45而非50");
        Assert.That(credits, Is.EqualTo(new[] { 1.0, 2.0, 3.0, 4.0, 0.0 }).Within(1e-9));
        // 耐磨工作衣：绷带 15×0.95 = 14.25，七次 6×15+10 = 100（而非105）
        var b = New();
        E.DebugCreateEquipment(b, "work_shirt", "white", ItemInstance.EquipOwner(GameState.PlayerId, EquipSlots.Top));
        var total = 0;
        for (var k = 0; k < 7; k++)
        {
            SetStats(b, 20, 90, 90, 18, 90, 80);
            E.DebugGive(b, "cloth", 1);
            E.DebugGive(b, "herb", 1);
            var p = E.Preview(b, ActionRequest.Craft("bandage"));
            var m = Timed(b, ActionRequest.Craft("bandage"));
            Assert.That(p.EstimatedMinutes, Is.EqualTo(m), $"第{k + 1}次预估与实际一致");
            total += m;
        }
        Assert.That(total, Is.EqualTo(100));
        Assert.That(E.CarryOf(b, "p0|crafting"), Is.EqualTo(0.25).Within(1e-9));
        Assert.That(E.CarryClamp(5.7), Is.EqualTo(4.9999), "上限 4.9999");
        Assert.That(E.CarryClamp(1.23456), Is.EqualTo(1.2345), "截到 0.0001");
    }

    [Test]
    public void TIME_Q61b_NightFactor_PerSegmentRealM()
    {
        // 夜间无照明 M=1.5：每段只推进 5/1.5；需求 9 要 3 段（15 分钟），余 1
        var s = At("stream");
        Lv(s, "gathering", 7);
        SetStats(s, 20, 90, 90, 18, 90, 80);
        AdvanceTo(s, At(1, 22, 0));
        SetStats(s, 20, 90, 90, 18, 90, 80);
        Assert.That(Timed(s, ActionRequest.Gather("water")), Is.EqualTo(15));
        Assert.That(E.CarryOf(s, "p0|gathering"), Is.EqualTo(1.0).Within(1e-4));
    }

    [Test]
    public void TIME_Q61c_FamiliesAndBucketsDontMix()
    {
        var s = At("stream");
        Lv(s, "gathering", 7);
        Timed(s, ActionRequest.Gather("water"));
        Assert.That(E.CarryOf(s, "p0|gathering"), Is.EqualTo(1).Within(1e-9));
        // 制造不借采集余额
        E.DebugGive(s, "cloth", 1);
        E.DebugGive(s, "herb", 1);
        Assert.That(Timed(s, ActionRequest.Craft("bandage")), Is.EqualTo(15));
        Assert.That(E.CarryOf(s, "p0|gathering"), Is.EqualTo(1).Within(1e-9), "采集余额不被制造花掉");
        Assert.That(E.CarryKeyOf(s, ActionType.Gather, "supply_box"), Is.EqualTo("p0|scavenging"), "搜集另一桶");
        Assert.That(E.CarryKeyOf(s, ActionType.Survey, null), Is.EqualTo("p0|ruin_survey_practice"));
        Assert.That(E.CarryKeyOf(s, ActionType.Travel, "field"), Is.EqualTo("travel|p0"), "移动按同行集合");
        s.Party.Add("a1");
        Assert.That(E.CarryKeyOf(s, ActionType.Travel, "field"), Is.EqualTo("travel|a1,p0"), "换队另起一桶");
    }

    [Test]
    public void TIME_Q61d_PreviewNoSpend_CancelRestores_SaveLoadContinues()
    {
        var s = At("stream");
        Lv(s, "gathering", 7);
        Timed(s, ActionRequest.Gather("water"));
        Timed(s, ActionRequest.Gather("water"));   // 余 2
        var p = E.Preview(s, ActionRequest.Gather("water"));
        Assert.That((p.CarryBefore, p.CarryAfter, p.EstimatedMinutes), Is.EqualTo((2.0, (double?)3.0, 10)));
        Assert.That(E.CarryOf(s, "p0|gathering"), Is.EqualTo(2).Within(1e-9), "预览不花余额");
        Start(s, ActionRequest.Gather("water"));
        Assert.That((s.Main!.CarryReserved, E.CarryOf(s, "p0|gathering")), Is.EqualTo((2.0, 0.0)), "开工整笔锁住");
        E.Advance(s);
        E.Cancel(s);
        Assert.That(E.CarryOf(s, "p0|gathering"), Is.EqualTo(2).Within(1e-9), "取消退回原余额，本次不结新余额");
        Start(s, ActionRequest.Gather("water"));
        E.Advance(s);
        var loaded = SaveSerializer.Deserialize(SaveSerializer.Serialize(s));
        Assert.That(loaded.Main!.CarryReserved, Is.EqualTo(2).Within(1e-9), "读档续原锁，不再锁第二次");
        Run(loaded);
        Assert.That(E.CarryOf(loaded, "p0|gathering"), Is.EqualTo(3).Within(1e-9));
        Assert.That(E.Start(loaded, ActionRequest.Gather("water").WithRequestId("x")).Ok, Is.True);
        var (id, held) = (loaded.Main!.ActionId, loaded.Main.CarryReserved);
        E.Start(loaded, ActionRequest.Gather("water").WithRequestId("x"));
        Assert.That((loaded.Main!.ActionId, loaded.Main.CarryReserved, E.CarryOf(loaded, "p0|gathering")), Is.EqualTo((id, held, 0.0)), "重复请求不再开工、不再锁");
    }

    [Test]
    public void TIME_Q61e_FixedDurationNoCarry_OldActionOldRule_MigrationZero()
    {
        var s = New();
        Start(s, ActionRequest.Wait(30));
        Assert.That(s.Main!.CarryKey, Is.Null, "等待/休息等固定时长不结转");
        E.Cancel(s);
        // 旧档在途（RuleVersion 0）：装备倍率逐段算、无余额
        var t = New();
        E.DebugCreateEquipment(t, "work_shirt", "white", ItemInstance.EquipOwner(GameState.PlayerId, EquipSlots.Top));
        E.DebugGive(t, "cloth", 1);
        E.DebugGive(t, "herb", 1);
        Start(t, ActionRequest.Craft("bandage"));
        var a = t.Main!;
        a.RuleVersion = 0; a.CarryKey = null; a.CarryReserved = 0; a.BaseWorkMinutes = 15;
        var json = JObject.Parse(SaveSerializer.Serialize(t));
        json["SchemaVersion"] = "12";
        ((JObject)json["Main"]!).Remove("RuleVersion");
        json.Remove("WorkCarry");
        var old = SaveSerializer.Deserialize(json.ToString(), Cfg);
        Assert.That(old.SchemaVersion, Is.EqualTo(GameState.CurrentSchema), "12→13→14");
        Assert.That(old.Main!.RuleVersion, Is.Zero);
        E.Advance(old);
        Assert.That(old.Main!.WorkDone, Is.EqualTo(5 / 0.95).Within(1e-9), "旧规则：每段 5/(M×装备)");
        Run(old);
        Assert.That(old.WorkCarry, Is.Empty, "旧在途完成不结余额");
        Assert.That(old.Log.Any(l => l.Text.Contains("按开工时的规则完成")), Is.True);
    }

    [Test]
    public void PROG_Q56_VisitedEdgeNoWeightGate_UnvisitedLevel5()
    {
        var s = New(7);
        E.DebugTeleport(s, "field");
        E.DebugUnlockEdge(s, "field__stream");
        Lv(s, "athletics", 10);
        E.DebugGive(s, "stone", 50);   // 超过 20 公斤
        var heavy = E.WorkFactor(s, ActionType.Travel, null, "stream");
        Assert.That(heavy, Is.EqualTo(Cfg.WorkFactors.Overweight).Within(1e-12), "没走过+超重：不吃轻装步伐");
        s.TraveledEdges.Add("field__stream");
        Assert.That(E.WorkFactor(s, ActionType.Travel, null, "stream"), Is.EqualTo(Cfg.WorkFactors.Overweight * 0.9).Within(1e-12), "走过的边×0.90 不看重量");
    }

    [Test]
    public void PROG_Q58_CureCraftableAtTenTen_NoBlueprintYet()
    {
        var rc = Cfg.Recipes.Single(r => r.Id == "make_antiviral_cure");
        Assert.That(rc.MinSkills, Is.EquivalentTo(new Dictionary<string, int> { ["medicine"] = 10, ["crafting"] = 10 }));
        Assert.That(Cfg.Recipes.Count(r => r.Outputs.ContainsKey("antiviral_cure")), Is.EqualTo(1), "不加第三条治愈路线");
        var u = New();
        foreach (var kv in rc.Inputs) E.DebugGive(u, kv.Key, kv.Value, "camp");
        Lv(u, "medicine", 10);
        Lv(u, "crafting", 10);
        var req = ActionRequest.Craft("make_antiviral_cure", First(u, "pot").InstanceId);
        Assert.That(E.CanStart(u, req), Is.Null, "蓝图系统上线前：满级+材料+场地+空锅即可");
    }

    // ───────────── M162：B7b 线上第 1 轮试玩修复 ─────────────

    [Test]
    public void Fix1_PotFoodCookXpLine_ReboilZero()
    {
        var (s, pot) = PotOnFire("clean_water", 2);
        E.DebugGive(s, "raw_root", 1, "camp");
        Assert.That(E.DoInstant(s, InstantRequest.PutFoodInPot(First(s, "raw_root", "camp").InstanceId, pot.InstanceId, 1)).Ok, Is.True);
        var f = E.PotFood(s, pot.InstanceId).Single();
        Assert.That(E.CookXpLine(s, f), Is.EqualTo("完成后烹饪经验+3（每日最多30）"));
        E.DebugGive(s, "cooked_meat", 1, "camp");
        Assert.That(E.DoInstant(s, InstantRequest.PutFoodInPot(First(s, "cooked_meat", "camp").InstanceId, pot.InstanceId, 1)).Ok, Is.True);
        var meat = E.PotFood(s, pot.InstanceId).Single(x => x.ItemId == "cooked_meat");
        Assert.That(E.CookXpLine(s, meat), Is.EqualTo("复煮不给烹饪经验"));
        var (w, wp) = PotOnFire("dirty_water", 2);
        Assert.That(E.CookXpLine(w, wp), Does.Contain("每500毫升烹饪经验+1"));
        Assert.That(E.CookXpLine(w, E.PotLiquids(w, wp.InstanceId).Single()), Does.Contain("每500毫升"), "锅内液体同锅的净化批次");
    }

    [Test]
    public void Fix2_Fix5_GatherVerbFollowsProficiency()
    {
        var y = At("scrapyard");
        y.Resources["metal_heap"].Available = 10;
        var p = E.Preview(y, ActionRequest.Gather("metal_heap"));
        Assert.That((p.Label, p.ConfirmText), Is.EqualTo(("搜集废金属堆", "开始搜集")));
        Start(y, ActionRequest.Gather("metal_heap"));
        Assert.That(E.ActionProgress(y)!.ExitNote, Does.Contain("放弃本次搜集"));
        E.Cancel(y);
        Assert.That(y.Log.Last().Text, Does.StartWith("已取消搜集"));
        var h = At("stream");
        Start(h, ActionRequest.Gather("herbs"));
        E.Cancel(h);
        Assert.That(h.Log.Last().Text, Does.StartWith("已取消采集"));
    }

    [Test]
    public void Fix3_MaxedTextBeatsDailyCount()
    {
        var s = At("stream");
        Lv(s, "gathering", 10);
        for (var k = 0; k < 4; k++) Do(s, ActionRequest.Gather("water"));
        Assert.That(s.Log.Where(l => l.Text.StartsWith("脏水+")).Select(l => l.Text).ToList(), Has.Count.EqualTo(4).And.All.Contains("采集已满10级").And.None.Contains("今日已计"));
    }

    [Test]
    public void Fix6_SalvageLogOrder_Fix9_MigrationClearsStaleCarry()
    {
        var s = New();
        var it = E.DebugCreateEquipment(s, "wooden_shield", "white", ItemInstance.Carried);
        Do(s, Salvage(it.InstanceId));
        Assert.That(s.Log.Last(l => l.Text.StartsWith("拆解")).Text, Does.Match(@"：木材\+\d+，已放在.+地面；制造经验\+2。$"));
        var t = New();
        E.DebugGive(t, "cloth", 1);
        E.DebugGive(t, "herb", 1);
        Start(t, ActionRequest.Craft("bandage"));
        var json = JObject.Parse(SaveSerializer.Serialize(t));
        json["SchemaVersion"] = "12";
        json["Main"]!["CarryKey"] = "p0|crafting";
        json["Main"]!["CarryReserved"] = 3.5;
        var old = SaveSerializer.Deserialize(json.ToString(), Cfg);
        Assert.That((old.Main!.CarryKey, old.Main.CarryReserved), Is.EqualTo(((string?)null, 0.0)));
        Run(old);
        Assert.That(old.WorkCarry, Is.Empty);
    }
}
