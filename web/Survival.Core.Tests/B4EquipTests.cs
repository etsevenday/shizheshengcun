using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 B4（策划提交 f172677）R14–R17：七槽开放、实例品质与词条、效果合成上限、磨损/维修/拆解新口径。
/// 出处：A05 §1、§2、§4、§5，R02 §2/§3/§5；验收 EQ01–EQ08。
/// </summary>
public class B4EquipTests : BattleTestBase
{
    private const string P0 = GameState.PlayerId;

    private ItemInstance Give(GameState s, string itemId)
    {
        E.DebugGive(s, itemId, 1);
        return s.Items.Where(i => i.IsCarried && i.ItemId == itemId).OrderByDescending(i => i.InstanceId).First();
    }

    private ItemInstance Ground(GameState s, string itemId) => E.CreateItems(s, itemId, 1, s.CurrentLocation).Single();

    // ───────────── M113：七槽开放（H2） ─────────────

    [Test]
    public void H2_FiveNonWeaponSlots_OpenByEquipSlot()
    {
        var s = New();
        var cases = new (string id, string slot)[]
        {
            ("padded_vest", EquipSlots.Armor), ("steady_ring", EquipSlots.CombatAccessory), ("work_shirt", EquipSlots.Top),
            ("work_trousers", EquipSlots.Bottom), ("gather_gloves", EquipSlots.SurvivalAccessory),
        };
        foreach (var (id, slot) in cases)
        {
            var it = Ground(s, id);
            var choices = E.EquipChoices(s, P0, it.InstanceId);
            Assert.That(choices.Select(c => c.slot), Is.EqualTo(new[] { slot }), id);
            Assert.That(choices[0].reason, Is.Null, id);
            Assert.That(E.Equip(s, P0, it.InstanceId, slot).Ok, Is.True, id);
            Assert.That(it.Owner, Is.EqualTo(ItemInstance.EquipOwner(P0, slot)));
        }
        Assert.That(E.WornItems(s), Has.Count.EqualTo(5));
        var shirt2 = Ground(s, "craft_coat");
        Assert.That(E.EquipBlocked(s, P0, shirt2.InstanceId, EquipSlots.Bottom), Does.Contain("只能装备到上衣"));
        Assert.That(E.EquipBlocked(s, P0, shirt2.InstanceId, EquipSlots.Weapon1), Does.Contain("只能装备到上衣"));
        // 交换：旧上衣回随身
        Assert.That(E.Equip(s, P0, shirt2.InstanceId, EquipSlots.Top).Ok, Is.True);
        Assert.That(s.Items.Single(i => i.ItemId == "work_shirt").IsCarried, Is.True);
        var wood = s.Items.First(i => i.ItemId == "wood");
        Assert.That(E.EquipChoices(s, P0, wood.InstanceId), Is.Empty);
        Assert.That(E.EquipBlocked(s, P0, wood.InstanceId, EquipSlots.Armor), Does.Contain("不是装备"));
    }

    [Test]
    public void EQ02_ShieldOccupiesWeaponSlot_NoThirdWeapon_SameKindRejected()
    {
        var s = New();
        var knife = Give(s, "stone_knife");
        var shield = Give(s, "wooden_shield");
        Assert.That(E.Equip(s, P0, knife.InstanceId, EquipSlots.Weapon1).Ok, Is.True);
        Assert.That(E.Equip(s, P0, shield.InstanceId, EquipSlots.Weapon2).Ok, Is.True, "刀+盾合法");
        var shield2 = Give(s, "scrap_shield");
        Assert.That(E.EquipBlocked(s, P0, shield2.InstanceId, EquipSlots.Weapon1), Does.Contain("已装备同类型武器"), "盾占 shield kind");
        var medkit = Give(s, "field_medkit");
        var whistle = Give(s, "signal_whistle");
        Assert.That(E.EquipChoices(s, P0, medkit.InstanceId).Select(c => c.slot), Is.EqualTo(EquipSlots.Weapons));
        // 两槽已满：再穿只能交换，不会出现第三件
        Assert.That(E.Equip(s, P0, medkit.InstanceId, EquipSlots.Weapon1).Ok, Is.True);
        Assert.That(E.WornItems(s).Count(i => E.Weapon(i.ItemId) != null), Is.EqualTo(2));
        Assert.That(knife.IsCarried, Is.True);
        Assert.That(E.EquipBlocked(s, P0, whistle.InstanceId, EquipSlots.Weapon2), Is.Null, "指挥与医疗不同 kind");
    }

    [Test]
    public void H2_SkillLockedTools_CannotAttack_GeneralCommandsStay()
    {
        var s = Fight(7, "shambler");
        var shield = Arm(s, "wooden_shield");
        ToPlayer(s);
        PlayerFirst(s);
        ToPlayer(s);
        Assert.That(E.CurrentWeapon(s, out var wi).Id, Is.EqualTo("wooden_shield"));
        Assert.That(wi!.InstanceId, Is.EqualTo(shield.InstanceId));
        var why = E.CommandBlocked(s, BattleCommand.Of("basic_attack", s.Battle!.Alive.First().Index));
        Assert.That(why, Does.Contain("技能尚未开放"));
        var opts = E.BattleOptions(s);
        Assert.That(opts.Single(o => o.Action == "basic_attack").Reason, Does.Contain("技能尚未开放"));
        Assert.That(opts.Single(o => o.Action == "guard").Available, Is.True, "通用指令照旧");
        Assert.That(opts.Single(o => o.Action == "retreat").Available, Is.True);
    }

    // ───────────── M114：实例品质与词条（R15，H4–H6） ─────────────

    [Test]
    public void H4_RollQuality_ByOriginTable_UnknownOriginIsWhite()
    {
        var s = New();
        var counts = new Dictionary<string, int> { ["white"] = 0, ["green"] = 0, ["blue"] = 0 };
        for (var i = 0; i < 4000; i++) counts[E.RollQuality(s, GameEngine.Origins.MainlineScavenge)]++;
        Assert.That(counts["white"] / 4000.0, Is.EqualTo(0.5).Within(0.04));
        Assert.That(counts["green"] / 4000.0, Is.EqualTo(0.45).Within(0.04));
        Assert.That(counts["blue"] / 4000.0, Is.EqualTo(0.05).Within(0.02));
        var state = s.Rng.State;
        Assert.That(E.RollQuality(s, null), Is.EqualTo("white"));
        Assert.That(E.RollQuality(s, "fixed_cache"), Is.EqualTo("white"), "没写概率的来源按白色");
        Assert.That(E.RollQuality(s, GameEngine.Origins.Craft), Is.EqualTo("green"), "普通制造默认绿");
        Assert.That(s.Rng.State, Is.EqualTo(state), "白色或唯一结果不消耗随机数");
        for (var i = 0; i < 200; i++) Assert.That(E.RollQuality(s, GameEngine.Origins.DungeonBoss), Is.Not.EqualTo("white"));
    }

    [Test]
    public void R15_AffixRoll_PoolCompatible_NoDuplicateIdOrGroup()
    {
        var s = New();
        for (var i = 0; i < 300; i++)
        {
            foreach (var id in new[] { "machete", "wooden_shield", "padded_vest", "work_shirt", "wooden_bow", "field_radio" })
            {
                var a = E.RollAffixes(s, id, 2);
                var pool = E.AffixPool(id).Select(x => x.Id).ToList();
                Assert.That(a.All(pool.Contains), Is.True, id);
                Assert.That(a.Distinct().Count(), Is.EqualTo(a.Count), id);
                Assert.That(a.Select(x => E.Affix(x)!.Group).Distinct().Count(), Is.EqualTo(a.Count), $"{id} 同互斥组最多 1 条");
            }
        }
        Assert.That(E.AffixPool("machete").Select(a => a.Id), Is.EquivalentTo(new[] { "accurate", "keen", "piercing", "balanced", "swift", "durable" }));
        Assert.That(E.AffixPool("work_shirt").Select(a => a.Id), Is.EquivalentTo(new[] { "craftsman", "economical", "patient" }));
        Assert.That(E.AffixPool("work_trousers").Select(a => a.Id), Is.EquivalentTo(new[] { "economical", "patient", "surefoot" }));
        Assert.That(E.AffixPool("steady_ring"), Is.Empty, "战斗饰品没有兼容池 → 0 条");
        Assert.That(E.RollAffixes(s, "steady_ring", 2), Is.Empty);
        // 池不足少条：指挥器具只有 swift、tactical、durable，其中 durable 与 swift 不同组 → 2 条都能抽到
        Assert.That(E.AffixPool("signal_whistle").Select(a => a.Id), Is.EquivalentTo(new[] { "swift", "tactical", "durable" }));
    }

    [Test]
    public void H5_Durable_MultipliesRolledMax_RoundedToTenth_ConditionKept()
    {
        var s = New();
        ItemInstance? it = null;
        for (var i = 0; i < 400 && it == null; i++)
        {
            var x = E.DebugCreateEquipment(s, "machete", "blue", s.CurrentLocation);
            if (x.Affixes!.Contains("durable")) it = x;
        }
        Assert.That(it, Is.Not.Null);
        Assert.That(it!.MaxDurability, Is.GreaterThan(36 * 1.15 - 0.051).And.LessThan(48 * 1.15 + 0.051));
        Assert.That(Math.Abs(it.MaxDurability!.Value * 10 - Math.Round(it.MaxDurability.Value * 10)), Is.LessThan(1e-6), "按 0.1 取整");
        Assert.That(it.Durability, Is.EqualTo(it.MaxDurability), "新制品满耐久");
        var plain = E.DebugCreateEquipment(s, "machete", "white", s.CurrentLocation);
        Assert.That(plain.Affixes, Is.Null);
        Assert.That(plain.Quality, Is.EqualTo("white"));
        Assert.That(plain.MaxDurability, Is.InRange(36, 48));
    }

    [Test]
    public void H4_CraftedEquipment_GreenOneAffix_StartingAndDevKitWhite()
    {
        var s = New();
        var lighter = s.Items.Single(i => i.ItemId == "lighter");
        Assert.That(lighter.Quality, Is.Null, "非装备没有品质");
        E.DebugGive(s, "stone", 1, s.CurrentLocation);
        E.DebugGive(s, "fiber", 1, s.CurrentLocation);
        Start(s, ActionRequest.Craft("knife"));
        Run(s);
        var knife = s.Items.Single(i => i.ItemId == "stone_knife");
        Assert.That(knife.Quality, Is.EqualTo("green"), "A05 §4：普通制造默认绿色1条");
        Assert.That(knife.Affixes, Has.Count.EqualTo(1));
        Assert.That(s.Log.Last(l => l.Text.Contains("石刀+1")).Text, Does.Contain("（绿·"));
        E.DebugGiveDevKit(s);
        Assert.That(s.Items.Where(i => i.ItemId == "rifle").All(i => i.Quality == "white" && i.Affixes == null), Is.True, "开发箱没写概率 → 白");
    }

    [Test]
    public void H4_ScavengedCache_NoviceScavengeOrigin()
    {
        var green = 0;
        for (uint seed = 1; seed <= 60; seed++)
        {
            var s = New(seed);
            E.DebugTeleport(s, "hardware");
            E.DebugSetProgress(s, "hardware", 100);
            Start(s, ActionRequest.Gather("hardware_weapon_cache"));
            Run(s);
            var knife = s.Items.Single(i => i.ItemId == "kitchen_knife");
            Assert.That(knife.Quality, Is.AnyOf("white", "green"), "新手普通拾荒只有白/绿");
            Assert.That(knife.Affixes?.Count ?? 0, Is.EqualTo(knife.Quality == "green" ? 1 : 0));
            Assert.That(knife.Durability, Is.LessThanOrEqualTo(knife.MaxDurability!.Value * 1.0 + 1e-9).And.GreaterThanOrEqualTo(knife.MaxDurability.Value * 0.6 - 1e-9));
            if (knife.Quality == "green") green++;
        }
        Assert.That(green, Is.InRange(3, 25), "约 20% 绿");
    }

    [Test]
    public void EQ04_QualityAndAffixes_SurviveSaveLoad_NotRerolled()
    {
        var s = New();
        var it = E.DebugCreateEquipment(s, "pike", "blue", ItemInstance.Carried);
        var json = Survival.Core.Save.SaveSerializer.Serialize(s);
        var back = Survival.Core.Save.SaveSerializer.Deserialize(json, Cfg);
        var b = back.Items.Single(i => i.InstanceId == it.InstanceId);
        Assert.That(b.Quality, Is.EqualTo("blue"));
        Assert.That(b.Affixes, Is.EqualTo(it.Affixes));
        Assert.That(b.MaxDurability, Is.EqualTo(it.MaxDurability));
        // 穿脱、移动不重抽
        Assert.That(E.Equip(back, P0, b.InstanceId, EquipSlots.Weapon1).Ok, Is.True);
        Assert.That(E.Unequip(back, P0, EquipSlots.Weapon1, true).Ok, Is.True);
        Assert.That(b.Affixes, Is.EqualTo(it.Affixes));
        var clone = back.Clone();
        clone.Items.Single(i => i.InstanceId == it.InstanceId).Affixes!.Add("x");
        Assert.That(b.Affixes, Has.Count.EqualTo(it.Affixes!.Count), "Clone 深拷贝词条列表");
    }

    [Test]
    public void H6_Migration9To10_OldEquipmentWhite_NoRngNoDurabilityChange()
    {
        var s = New();
        var knife = E.CreateItems(s, "stone_knife", 1, ItemInstance.EquipOwner(P0, EquipSlots.Weapon1)).Single();
        var j = Newtonsoft.Json.Linq.JObject.Parse(Survival.Core.Save.SaveSerializer.Serialize(s));
        foreach (var it in (Newtonsoft.Json.Linq.JArray)j["Items"]!) { ((Newtonsoft.Json.Linq.JObject)it).Remove("Quality"); ((Newtonsoft.Json.Linq.JObject)it).Remove("Affixes"); }
        j["SchemaVersion"] = "9";
        var rng = s.Rng.State;
        var m = Survival.Core.Save.SaveSerializer.Deserialize(j.ToString(), Cfg, out var notes);
        Assert.That(m.SchemaVersion, Is.EqualTo(GameState.CurrentSchema), "9→10→11（B5 起再升到 11）");
        var k = m.Items.Single(i => i.InstanceId == knife.InstanceId);
        Assert.That((k.Quality, k.Affixes), Is.EqualTo(("white", (List<string>?)null)));
        Assert.That((k.Durability, k.MaxDurability), Is.EqualTo((knife.Durability, knife.MaxDurability)));
        Assert.That(m.Items.Single(i => i.ItemId == "lighter").Quality, Is.Null, "非装备不写品质");
        Assert.That(m.Rng.State, Is.EqualTo(rng), "迁移不消耗随机数");
        Assert.That(notes.Single(), Does.Contain("白色品质"));
        // 已是 10 的档不重复迁移
        var again = Survival.Core.Save.SaveSerializer.Deserialize(Survival.Core.Save.SaveSerializer.Serialize(m), Cfg, out var notes2);
        Assert.That(notes2, Is.Empty);
    }

    // ───────────── M115：效果合成（R16） ─────────────

    private ItemInstance Wear(GameState s, string itemId, string slot, string quality = "white", params string[] affixes)
    {
        var it = E.DebugCreateEquipment(s, itemId, quality, ItemInstance.EquipOwner(P0, slot));
        if (affixes.Length > 0) it.Affixes = affixes.ToList();
        return it;
    }

    [Test]
    public void R16_SpeedPositiveCap3_NegativeStillCounts_ArmorCap09()
    {
        var s = New();
        Wear(s, "barbed_spear", EquipSlots.Weapon1, "white", "swift");      // +1 +1
        Wear(s, "hunter_bow", EquipSlots.Weapon2);                         // +1
        Wear(s, "scout_armor", EquipSlots.Armor, "white", "swift");        // +1（swift 同名只取一次）
        Wear(s, "swift_pendant", EquipSlots.CombatAccessory);              // +1，护甲−0.05
        var fx = E.EquipmentEffects(s);
        Assert.That(fx.SpeedPositiveRaw, Is.EqualTo(5), "固定 +1×4，两件上的 swift 同名只算一次 +1");
        Assert.That(fx.Speed, Is.EqualTo(3), "正加成最多 +3");
        Assert.That(fx.Armor, Is.EqualTo(0.15).Within(1e-9));
        // 换重甲：负修正照算
        var s2 = New();
        Wear(s2, "heavy_armor", EquipSlots.Armor, "white", "heavy");
        Wear(s2, "barbed_spear", EquipSlots.Weapon1);
        var fx2 = E.EquipmentEffects(s2);
        Assert.That(fx2.Speed, Is.EqualTo(1 - 2 - 1));
        Assert.That(fx2.ArmorRaw, Is.EqualTo(0.73).Within(1e-9));
        var s3 = New();
        Wear(s3, "heavy_armor", EquipSlots.Armor, "white", "heavy");
        Wear(s3, "wooden_shield", EquipSlots.Weapon1);
        s3.Items.Single(i => i.ItemId == "heavy_armor").Affixes = new List<string> { "heavy" };
        var fx3 = E.EquipmentEffects(s3);
        Assert.That(fx3.Armor, Is.LessThanOrEqualTo(0.9));
    }

    [Test]
    public void R16_AttackAffix_OnlyFromPageWeapon_NotBorrowed()
    {
        var s = New();
        Wear(s, "machete", EquipSlots.Weapon1, "white", "accurate");
        Wear(s, "wooden_spear", EquipSlots.Weapon2, "white", "piercing");
        var p1 = E.EquipmentEffects(s, P0, 1);
        var p2 = E.EquipmentEffects(s, P0, 2);
        Assert.That((p1.Accuracy, p1.ArmorIgnore), Is.EqualTo((4.0, 0.0)));
        Assert.That((p2.Accuracy, p2.ArmorIgnore), Is.EqualTo((0.0, 0.1)));
    }

    [Test]
    public void EQ06_SurvivalClothes_NoCombatArmorOrSpeed()
    {
        var s = New();
        foreach (var (id, slot) in new[] { ("expedition_coat", EquipSlots.Top), ("expedition_trousers", EquipSlots.Bottom), ("route_compass", EquipSlots.SurvivalAccessory) })
            Wear(s, id, slot, "white", id == "expedition_coat" ? "economical" : id == "expedition_trousers" ? "surefoot" : "");
        s.Items.Single(i => i.ItemId == "route_compass").Affixes = null;
        var fx = E.EquipmentEffects(s);
        Assert.That((fx.Speed, fx.Armor, fx.Accuracy, fx.Crit), Is.EqualTo((0.0, 0.0, 0.0, 0.0)), "求生衣物不加战斗甲/速");
        Assert.That(fx.Factor("work.move"), Is.EqualTo(0.9));
        Assert.That(fx.Factor("rate.nonCombat.hydration"), Is.EqualTo(0.95 * 0.97).Within(1e-12));
        Assert.That(fx.Factor("rate.move.energy"), Is.EqualTo(0.95).Within(1e-12));
    }

    [Test]
    public void EQ08_PassiveAffixes_NoApOrControlEffectsApplied()
    {
        // 词条里没有 AP、物资卡位、免疫类效果；控制/位移类只作展示，没有可叠加成无限链的数值钩子
        var keys = Cfg.Affixes.SelectMany(a => a.Effects).Select(e => e.Key).Concat(Cfg.Gear.SelectMany(g => g.Effects).Select(e => e.Key)).Distinct().ToList();
        Assert.That(keys.Any(k => k.Split('.').Any(p => p.Equals("ap", StringComparison.OrdinalIgnoreCase) || p.StartsWith("ap", StringComparison.OrdinalIgnoreCase) && p.Length <= 3)), Is.False);
        Assert.That(keys.Any(k => k.Contains("card") || k.Contains("immun")), Is.False);
    }

    [Test]
    public void R16_SameNameTakesStrongest_AffixMultiplies_FloorApplies()
    {
        var s = New();
        Wear(s, "forage_jacket", EquipSlots.Top, "white", "economical");   // 采集负E×0.90
        Wear(s, "work_trousers", EquipSlots.Bottom, "white", "economical"); // 同名 ×0.95 → 取强 0.90；同名词条只算一次
        var fx = E.EquipmentEffects(s);
        Assert.That(fx.Factor("rate.gather.energy"), Is.EqualTo(0.9));
        Assert.That(fx.Factor("rate.nonCombat.hydration"), Is.EqualTo(0.97));
        // 工作量：工匠围衣 0.90 × 熟作 0.97
        var c = New();
        Wear(c, "craft_coat", EquipSlots.Top, "white", "craftsman");
        Assert.That(E.GearWorkFactor(c, P0, ActionType.Craft, "knife"), Is.EqualTo(0.9 * 0.97).Within(1e-12));
        Assert.That(E.GearWorkFactor(c, P0, ActionType.Repair, "repair_selected_tool"), Is.EqualTo(0.9 * 0.97).Within(1e-12), "维修、拆解按制造类工作（M120，待确认 Q52）");
        Assert.That(E.GearWorkFactor(c, P0, ActionType.Salvage, "salvage_equipment"), Is.EqualTo(0.9 * 0.97).Within(1e-12));
        Assert.That(E.GearWorkFactor(c, P0, ActionType.Travel, null), Is.EqualTo(1));
    }

    [Test]
    public void R16_ZeroDurability_PausesEffects_StillWornAndWeighted()
    {
        var s = New();
        var vest = Wear(s, "heavy_armor", EquipSlots.Armor);
        vest.Durability = 0;
        var fx = E.EquipmentEffects(s);
        Assert.That(fx.Armor, Is.EqualTo(0));
        Assert.That(fx.Paused.Single(), Does.Contain("重型防护甲"));
        Assert.That(E.WornItems(s), Has.Count.EqualTo(1));
    }

    [Test]
    public void R16_Hooks_WorkRatesRestHazardRepairBandageEat()
    {
        // 制造：耐磨工作衣 ×0.95 进工作倍率
        var s = New();
        var basef = E.WorkFactor(s, ActionType.Craft);
        Wear(s, "work_shirt", EquipSlots.Top);
        var reasons = new List<string>();
        Assert.That(E.WorkFactor(s, ActionType.Craft, reasons), Is.EqualTo(basef * 0.95).Within(1e-12));
        Assert.That(reasons.Last(), Does.Contain("装备工作量×0.95"));

        // 移动负体力：行路裤 ×0.90；非战斗负N/W：远征上衣 ×0.95
        var t = New();
        var a = new ActionState { Type = ActionType.Travel, TargetId = "field" };
        var before = E.ComputeSegment(t, a);
        Wear(t, "trail_trousers", EquipSlots.Bottom);
        Wear(t, "expedition_coat", EquipSlots.Top);
        var after = E.ComputeSegment(t, a);
        Assert.That(after.Rates.Energy, Is.EqualTo(before.Rates.Energy * 0.9).Within(1e-9));
        Assert.That(after.Rates.Satiety, Is.EqualTo(before.Rates.Satiety * 0.95).Within(1e-9));
        Assert.That(after.Rates.Wakefulness, Is.EqualTo(before.Rates.Wakefulness), "清醒不在 N/W/E 范围");

        // 睡眠正体力 +0.2（休养长裤）
        var z = New();
        var sleep = new ActionState { Type = ActionType.Sleep };
        var e0 = E.ComputeSegment(z, sleep).Rates.Energy;
        Wear(z, "recovery_trousers", EquipSlots.Bottom);
        Assert.That(E.ComputeSegment(z, sleep).Rates.Energy, Is.EqualTo(e0 + 0.2).Within(1e-9));

        // 划伤：采集手套概率 ×0.80（木材点）、加固长裤损失 ×0.80
        var h = New();
        var wood = Cfg.Resources.Single(r => r.NodeId == "wood");
        var p0 = E.HazardChance(h, wood);
        Wear(h, "gather_gloves", EquipSlots.SurvivalAccessory);
        Wear(h, "reinforced_trousers", EquipSlots.Bottom);
        Assert.That(E.HazardChance(h, wood), Is.EqualTo(p0 * 0.8).Within(1e-12));
        Assert.That(E.HazardLoss(h, wood), Is.EqualTo(wood.Hazard!.HealthLoss * 0.8).Within(1e-12));
        var box = Cfg.Resources.Single(r => r.NodeId == "misc_box");
        Assert.That(E.HazardChance(h, box), Is.EqualTo(box.Hazard!.Probability), "手套只管木材/废料");

        // 维修手册 25% 替代 20%
        var rp = New();
        var rc = Cfg.Recipes.Single(r => r.IsRepair);
        Assert.That(E.RepairFraction(rp, rc), Is.EqualTo(0.2));
        Wear(rp, "repair_manual", EquipSlots.SurvivalAccessory);
        Assert.That(E.RepairFraction(rp, rc), Is.EqualTo(0.25));

        // 护理笔记：非战斗包扎 +0.2；配给袋：非战斗食用正饱食 ×1.05
        var m = New();
        var heal = E.BandageHeal(m);
        Wear(m, "medical_notes", EquipSlots.SurvivalAccessory);
        Assert.That(E.BandageHeal(m), Is.EqualTo(heal + 0.2).Within(1e-12));
        var mm = New();
        Wear(mm, "ration_pouch", EquipSlots.SurvivalAccessory);
        Assert.That(E.UseSatiety(mm, Cfg.Items.Single(i => i.Id == "canned_food").Use!), Is.EqualTo(35 * 1.05).Within(1e-9));
        Assert.That(E.UseSatiety(mm, Cfg.Items.Single(i => i.Id == "cooked_root").Use!), Is.EqualTo(25 * 1.05).Within(1e-9));
    }

    [Test]
    public void R16_WorkFloor070_RateFloor075()
    {
        // 人为叠出低于下限的组合：工作量下限 0.70（只有制造键能叠 0.90×0.97，不到下限）→ 用移动的两个键 0.90×0.95=0.855
        var s = New();
        Wear(s, "expedition_trousers", EquipSlots.Bottom);
        Wear(s, "route_compass", EquipSlots.SurvivalAccessory);
        Assert.That(E.GearWorkFactor(s, P0, ActionType.Travel, "field"), Is.EqualTo(0.9 * 0.95).Within(1e-12));
        // 下限：直接改配置副本里的倍率验证夹紧
        var cfg = Survival.Core.Config.ConfigLoader.FromJson(File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "config.v0.6.json")));
        cfg.Gear.Single(g => g.Id == "expedition_trousers").Effects[0].Value = 0.5;
        cfg.Gear.Single(g => g.Id == "trail_trousers").Effects[0].Value = 0.5;
        var e2 = new GameEngine(cfg);
        var s2 = e2.NewGame(1);
        e2.DebugLegacyStory(s2);
        e2.DebugCreateEquipment(s2, "expedition_trousers", "white", ItemInstance.EquipOwner(P0, EquipSlots.Bottom)).Affixes = null;
        Assert.That(e2.GearWorkFactor(s2, P0, ActionType.Travel, "field"), Is.EqualTo(0.7), "工作量合成下限×0.70");
        var s3 = e2.NewGame(1);
        e2.DebugLegacyStory(s3);
        e2.DebugCreateEquipment(s3, "trail_trousers", "white", ItemInstance.EquipOwner(P0, EquipSlots.Bottom)).Affixes = null;
        var a = new ActionState { Type = ActionType.Travel, TargetId = "field" };
        var raw = cfg.HourlyRates["travel"].Energy;
        Assert.That(e2.ComputeSegment(s3, a).Rates.Energy, Is.EqualTo(raw * 0.75).Within(1e-9), "负小时率合成下限×0.75");
    }

    // ───────────── M116：磨损与噪声（R17a） ─────────────

    [Test]
    public void R17_ArmorWearsOnePerPositiveHit_EvenIfFullyBlocked()
    {
        var s = Fight(11, "shambler");
        var vest = Wear(s, "padded_vest", EquipSlots.Armor);
        var d0 = vest.Durability!.Value;
        var e = s.Battle!.Alive.First();
        var hp = s.Stats.Health;
        // 敌人命中：强制命中判定与伤害抽签
        Force(s, Below(0.01), Any());
        typeof(GameEngine).GetMethod("EnemyAttack", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .Invoke(E, new object[] { s, e, Cfg.Enemies.Single(x => x.Id == e.EnemyId) });
        Assert.That(s.Stats.Health, Is.LessThan(hp));
        Assert.That(vest.Durability, Is.EqualTo(d0 - 1).Within(1e-9), "防具每次受正直接伤害磨损 1");
        Assert.That(s.Battle!.Log.Last(), Does.Contain("衬垫护具耐久"));
        // 落空不磨损
        Force(s, AtLeast(0.999));
        typeof(GameEngine).GetMethod("EnemyAttack", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .Invoke(E, new object[] { s, e, Cfg.Enemies.Single(x => x.Id == e.EnemyId) });
        Assert.That(vest.Durability, Is.EqualTo(d0 - 1).Within(1e-9));
        // 耐久 0：不再扣、效果暂停
        vest.Durability = 0;
        Force(s, Below(0.01), Any());
        typeof(GameEngine).GetMethod("EnemyAttack", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .Invoke(E, new object[] { s, e, Cfg.Enemies.Single(x => x.Id == e.EnemyId) });
        Assert.That(vest.Durability, Is.EqualTo(0));
    }

    [Test]
    public void R17_NoiseByKind_NewGunsAndCrossbowFollowSameClass()
    {
        double N(string id) => E.NoiseGain(Cfg.Weapons.Single(w => w.Id == id));
        Assert.That(N("pistol"), Is.EqualTo(Cfg.Noise.GunGain));
        Assert.That(N("service_pistol"), Is.EqualTo(Cfg.Noise.GunGain));
        Assert.That(N("marksman_rifle"), Is.EqualTo(Cfg.Noise.GunGain));
        Assert.That(N("crossbow"), Is.EqualTo(Cfg.Noise.CrossbowGain));
        Assert.That(N("repeater_crossbow"), Is.EqualTo(Cfg.Noise.CrossbowGain));
        Assert.That(N("hunter_bow"), Is.EqualTo(0), "弓无声");
        Assert.That(N("machete"), Is.EqualTo(0));
    }

    [Test]
    public void EQ01_AttackWearsOnlyTheUsedWeapon()
    {
        var s = Fight(5, "shambler");
        var knife = Wear(s, "machete", EquipSlots.Weapon1);
        var spear = Wear(s, "pike", EquipSlots.Weapon2);
        s.Battle!.ActiveWeaponSlot = 1;
        ToPlayer(s);
        PlayerFirst(s);
        ToPlayer(s);
        var k0 = knife.Durability;
        var p0 = spear.Durability;
        Force(s, AtLeast(0.999));
        Assert.That(Cmd(s, "basic_attack", s.Battle!.Alive.First().Index).Ok, Is.True);
        Assert.That(knife.Durability, Is.EqualTo(k0 - 1).Within(1e-9));
        Assert.That(spear.Durability, Is.EqualTo(p0));
    }

    // ───────────── M117：维修与拆解（R17b，H8/H9） ─────────────

    [Test]
    public void EQ05_RepairArmor_NoMaxIncrease_QualityAffixesKept()
    {
        var s = New();
        var vest = E.DebugCreateEquipment(s, "leather_armor", "blue", ItemInstance.EquipOwner(P0, EquipSlots.Armor));
        var max = vest.MaxDurability!.Value;
        var affixes = vest.Affixes!.ToList();
        vest.Durability = 10;
        Assert.That(E.RepairCandidates(s), Does.Contain(vest), "穿戴中的防具也可维护");
        E.DebugGive(s, "scrap", 1);
        E.DebugGive(s, "parts", 1);
        Start(s, new ActionRequest { Type = ActionType.Craft, TargetId = "repair_selected_tool", InstanceId = vest.InstanceId });
        Run(s);
        Assert.That(vest.MaxDurability, Is.EqualTo(max), "EQ05 修理不增最大耐久");
        Assert.That(vest.Durability, Is.EqualTo(Math.Min(max, 10 + max * 0.2)).Within(1e-9));
        Assert.That((vest.Quality, vest.Affixes), Is.EqualTo(("blue", affixes)));
    }

    private ActionRequest Salvage(int id) => new ActionRequest { Type = ActionType.Craft, TargetId = "salvage_equipment", InstanceId = id };

    [Test]
    public void H9_Salvage_WeaponScrapByQuality_15Minutes_ToGround()
    {
        foreach (var (q, n) in new[] { ("white", 1), ("green", 2), ("blue", 3) })
        {
            var s = New();
            var it = E.DebugCreateEquipment(s, "machete", q, ItemInstance.Carried);
            var scrap0 = s.Items.Where(i => i.ItemId == "scrap").Sum(i => i.Count);
            var p = E.Preview(s, Salvage(it.InstanceId));
            Assert.That(p.Ok, Is.True, p.Reason);
            Assert.That(p.BaseMinutes, Is.EqualTo(15));
            Assert.That(p.Outputs.Single().Count, Is.EqualTo(n));
            Assert.That(p.XpGains, Is.Empty, "拆解不给制造经验");
            var t0 = s.GameMinute;
            Start(s, Salvage(it.InstanceId));
            Assert.That(E.IsLocked(s, it), Is.True, "开工锁定目标");
            Run(s);
            Assert.That(s.GameMinute - t0, Is.EqualTo(15), "制造 1 级、白天无倍率时 15 分钟");
            Assert.That(s.Items.Any(i => i.InstanceId == it.InstanceId), Is.False, "原物移除");
            Assert.That(s.Items.Where(i => i.ItemId == "scrap" && i.Owner == s.CurrentLocation).Sum(i => i.Count), Is.EqualTo(n), q);
            Assert.That(s.Items.Where(i => i.ItemId == "scrap").Sum(i => i.Count) - scrap0, Is.EqualTo(n));
        }
    }

    [Test]
    public void H9_Salvage_ClothLeatherRadio_AndRefusals()
    {
        var s = New();
        var shirt = Ground(s, "work_shirt");
        Assert.That(E.SalvageOutput(shirt), Is.EqualTo(("cloth", 1)));
        Assert.That(E.SalvageOutput(Ground(s, "craft_coat")), Is.EqualTo(("leather", 1)), "主材皮革");
        Assert.That(E.SalvageOutput(Ground(s, "field_radio")), Is.EqualTo(("electronics", 1)));
        var ring = Ground(s, "steady_ring");
        Assert.That(E.SalvageBlocked(s, ring.InstanceId, out _), Does.Contain("拆解产出待策划确认"));
        Assert.That(E.SalvageBlocked(s, Ground(s, "scrap_armor").InstanceId, out _), Does.Contain("待策划确认"), "主材不是布/皮");
        var wood = s.Items.First(i => i.ItemId == "wood");
        Assert.That(E.SalvageBlocked(s, wood.InstanceId, out _), Does.Contain("不是装备"));
        var worn = Wear(s, "machete", EquipSlots.Weapon1);
        Assert.That(E.SalvageBlocked(s, worn.InstanceId, out _), Does.Contain("先在装备区卸下"));
        var pistol = Ground(s, "pistol");
        pistol.LoadedAmmo = 3;
        Assert.That(E.SalvageBlocked(s, pistol.InstanceId, out _), Does.Contain("弹仓里还有3发"));
        var away = E.CreateItems(s, "machete", 1, "field").Single();
        Assert.That(E.SalvageBlocked(s, away.InstanceId, out _), Does.Contain("随身或当前地面"));
        // 失败不改状态
        var before = Survival.Core.Save.SaveSerializer.Serialize(s);
        Assert.That(E.Start(s, Salvage(ring.InstanceId)).Ok, Is.False);
        Assert.That(Survival.Core.Save.SaveSerializer.Serialize(s), Is.EqualTo(before));
        Assert.That(E.Start(s, new ActionRequest { Type = ActionType.Craft, TargetId = "salvage_equipment", InstanceId = shirt.InstanceId, BatchCount = 2 }).Reason, Does.Contain("每次只能做1件"));
    }

    [Test]
    public void H9_SalvageCancelled_TargetStays()
    {
        var s = New();
        var it = E.DebugCreateEquipment(s, "machete", "green", ItemInstance.Carried);
        Start(s, Salvage(it.InstanceId));
        E.Advance(s);
        Assert.That(E.Cancel(s).Ok, Is.True);
        Assert.That(s.Items.Single(i => i.InstanceId == it.InstanceId).Quality, Is.EqualTo("green"));
        Assert.That(E.IsLocked(s, it), Is.False);
    }

    // ───────────── M118：界面文字与开发入口（H10） ─────────────

    [Test]
    public void H10_DetailLines_QualityAffixTierTraitSalvage()
    {
        var s = New();
        var w = E.DebugCreateEquipment(s, "pike", "blue", ItemInstance.Carried);
        var lines = E.EquipDetailLines(s, w).ToDictionary(x => x.Label + (x.Label == "附加词条" ? x.Value : ""), x => x.Value);
        Assert.That(lines["品质"], Is.EqualTo("蓝（附加词条2条）"));
        Assert.That(lines.Keys.Count(k => k.StartsWith("附加词条")), Is.EqualTo(2));
        Assert.That(lines["类型"], Does.Contain("矛·T2").And.Contain("速度修正−1"));
        Assert.That(lines["站位"], Does.Contain("使用位123 → 目标位123"));
        Assert.That(lines["固定特性"], Does.Contain("架阵"));
        Assert.That(lines["拆解"], Does.StartWith("废金属×3"));
        var shirt = E.DebugCreateEquipment(s, "work_shirt", "white", ItemInstance.Carried);
        var sl = E.EquipDetailLines(s, shirt).ToDictionary(x => x.Label, x => x.Value);
        Assert.That(sl["固定效果"], Does.Contain("（穿戴后生效）"), "没穿不写生效中（M120）");
        Assert.That(E.Equip(s, P0, shirt.InstanceId, EquipSlots.Top).Ok, Is.True);
        Assert.That(E.EquipDetailLines(s, shirt).Single(x => x.Label == "固定效果").Value, Does.Contain("（生效中）"));
        Assert.That(E.AffixText("craftsman", worn: false), Does.Contain("穿戴后生效"));
        var ring = E.DebugCreateEquipment(s, "steady_ring", "white", ItemInstance.Carried);
        Assert.That(E.EquipDetailLines(s, ring).Single(x => x.Label == "固定效果").Value, Does.Contain("当前只显示"));
        Assert.That(E.EquipDetailLines(s, ring).Single(x => x.Label == "拆解").Value, Does.Contain("待策划确认"));
        var shield = E.DebugCreateEquipment(s, "wooden_shield", "white", ItemInstance.Carried);
        Assert.That(E.EquipDetailLines(s, shield).Any(x => x.Label == "技能" && x.Value.Contains("尚未开放")), Is.True);
        Assert.That(E.EquipDetailLines(s, s.Items.First(i => i.ItemId == "lighter")), Is.Empty);
        Assert.That(E.AffixText("durable"), Does.Contain("生成时已计入"));
        Assert.That(E.AffixText("craftsman"), Does.Contain("生效中"));
        Assert.That(E.AffixText("accurate"), Does.Contain("当前只显示"));
    }

    [Test]
    public void H10_SummaryLines_CombatShownSurvivalLivePaused()
    {
        var s = New();
        Wear(s, "scout_armor", EquipSlots.Armor);
        Wear(s, "craft_coat", EquipSlots.Top);
        var lines = E.EquipSummaryLines(s);
        Assert.That(lines[0], Does.Contain("速度+1").And.Contain("护甲0.2").And.Contain("当前只显示"));
        Assert.That(lines[1], Does.Contain("制造/建造工作量×0.9").And.Contain("生效中"));
        s.Items.Single(i => i.ItemId == "scout_armor").Durability = 0;
        Assert.That(E.EquipSummaryLines(s).Last(), Does.Contain("效果暂停").And.Contain("侦察护具"));
    }

    [Test]
    public void H10_DevEntries_EquipmentSetAndNewItems()
    {
        var s = New();
        var text = E.DebugGiveEquipmentSet(s, "green");
        Assert.That(text, Does.Contain("绿色装备7件"));
        foreach (var id in GameEngine.DevEquipmentSet)
        {
            var it = s.Items.Single(i => i.ItemId == id);
            Assert.That(it.Owner, Is.EqualTo(s.CurrentLocation));
            Assert.That(it.Quality, Is.EqualTo("green"));
            Assert.That(E.SlotsFor(id), Is.Not.Empty, id);
        }
        Assert.That(GameEngine.DevEquipmentSet.Select(id => E.SlotsFor(id)[0]).Distinct().Count(), Is.EqualTo(6), "覆盖武器与五个非武器槽");
        Assert.That(E.DebugGiveNewItems(s, "material"), Does.Contain("新物品（材料）"));
        Assert.That(s.Items.Any(i => i.ItemId == "steel" && i.Owner == s.CurrentLocation), Is.True);
    }

    // ───────────── M120：B4 线上试玩第 1 轮修复 ─────────────

    [Test]
    public void M120_RepairPreview_ShowsBeforeAfter_WithManual()
    {
        var s = New();
        var vest = E.DebugCreateEquipment(s, "leather_armor", "white", ItemInstance.EquipOwner(P0, EquipSlots.Armor));
        vest.Durability = 20;
        var max = vest.MaxDurability!.Value;
        E.DebugGive(s, "scrap", 1);
        E.DebugGive(s, "parts", 1);
        Wear(s, "repair_manual", EquipSlots.SurvivalAccessory);
        var p = E.Preview(s, new ActionRequest { Type = ActionType.Craft, TargetId = "repair_selected_tool", InstanceId = vest.InstanceId });
        Assert.That(p.Ok, Is.True, p.Reason);
        Assert.That(p.Notes, Has.Some.Contains($"耐久20.00→{Fmt.N(Math.Min(max, 20 + max * 0.25))}").And.Some.Contains("25%"));
    }

    [Test]
    public void M120_GatherPreview_HasGearRateLine()
    {
        var s = New();
        Wear(s, "work_trousers", EquipSlots.Bottom);
        Assert.That(E.GearRateNote(s, ActionType.Gather, "roots"), Is.EqualTo("装备：采集负体力×0.95（只缩减消耗，下限×0.75）"));
        Assert.That(E.GearRateNote(s, ActionType.Craft, "knife"), Is.Null);
    }

    [Test]
    public void M120_NewItemDesc_ReadableChinese()
    {
        string D(string id) => Cfg.Items.Single(i => i.Id == id).Desc;
        Assert.That(D("root_porridge"), Does.StartWith("策划用途：饱食+38、水分+10、精神+2。"));
        Assert.That(D("dried_meat"), Does.Contain("水分−5"));
        Assert.That(D("first_aid_pack"), Does.Contain("止血，健康+2"));
        Assert.That(D("antidiarrheal_powder"), Does.Contain("与口服补液共享6小时"));
        foreach (var it in Cfg.Items.Where(i => i.Desc.Contains("本版只有物品定义")))
            Assert.That(System.Text.RegularExpressions.Regex.IsMatch(it.Desc, "(?<![A-Za-z])[NWMHESIV][+−-]"), Is.False, it.Id);
    }

    [Test]
    public void EQ03_GearWeightCountsOnPerson_NotRootCards()
    {
        var s = New();
        var cards = E.RootCardCount(s);
        var grams = E.CarriedGrams(s);
        var armor = Give(s, "heavy_armor");
        Assert.That(E.RootCardCount(s), Is.EqualTo(cards + 1));
        Assert.That(E.Equip(s, P0, armor.InstanceId, EquipSlots.Armor).Ok, Is.True);
        Assert.That(E.RootCardCount(s), Is.EqualTo(cards), "穿戴不占五卡");
        Assert.That(E.CarriedGrams(s), Is.EqualTo(grams + 6000), "重量计本人");
    }
}
