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
        Assert.That(m.SchemaVersion, Is.EqualTo("10"));
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
