using Newtonsoft.Json.Linq;
using Survival.Core.Config;
using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 B7a（策划提交 6dab1bb 答复 Q44–Q52）R92–R96：装备实例效果、72 件拆解表与 80% 材料价值预算、
/// 38 件武器技能绑定、求生词条场景、饰品兼容池。
/// 出处：A05「2026-10-31 Q44—Q52装备裁定（当前优先）」、A06 同名章、equipment-adjudications.json（= A12 equipmentAdjudications）、
/// 更新说明/2026-10-31/Q44-Q52 更新说明；验收 EQ-Q44—EQ-Q52。
/// </summary>
public class B7aEquipTests : BattleTestBase
{
    private const string P0 = GameState.PlayerId;

    private static JObject Adj() =>
        (JObject)JObject.Parse(File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "design.a12.json")))["equipmentAdjudications"]!;

    private static ActionRequest Salvage(int id) => new ActionRequest { Type = ActionType.Craft, TargetId = "salvage_equipment", InstanceId = id };

    private ItemInstance Make(GameState s, string id, string q = "white", string? owner = null) =>
        E.DebugCreateEquipment(s, id, q, owner ?? ItemInstance.Carried);

    private ItemInstance Wear(GameState s, string itemId, string slot, string quality = "white", params string[] affixes)
    {
        var it = E.DebugCreateEquipment(s, itemId, quality, ItemInstance.EquipOwner(P0, slot));
        it.Affixes = affixes.Length > 0 ? affixes.ToList() : null;
        return it;
    }

    private static void Lv(GameState s, string id, int lv) => s.Skills[id].Level = lv;

    private static int Sum(GameState s, string itemId) => s.Items.Where(i => i.ItemId == itemId).Sum(i => i.Count);

    private static IEnumerable<(string id, SalvageOutputConfig? def)> AllEquipment() =>
        Cfg.Weapons.Where(w => !w.IsFists).Select(w => (w.Id, w.Salvage)).Concat(Cfg.Gear.Select(g => (g.Id, g.Salvage)));

    // ───────────── M152：拆解表（R93，Q46/Q52） ─────────────

    [Test]
    public void EQ_Q46a_AllSeventyTwo_HaveRow_MatchingAdjudication()
    {
        var rows = ((JArray)Adj()["dismantle"]!["rows"]!).ToDictionary(r => (string)r["itemId"]!);
        var all = AllEquipment().ToList();
        Assert.That(all, Has.Count.EqualTo(72), "38 件武器槽 + 34 件其他装备");
        foreach (var (id, def) in all)
        {
            Assert.That(def, Is.Not.Null, $"{id} 没有拆解表");
            var r = rows[id];
            Assert.That(def!.ItemId, Is.EqualTo((string)r["material"]!), id);
            Assert.That(def.Counts, Is.EqualTo(new Dictionary<string, int> { ["white"] = (int)r["white"]!, ["green"] = (int)r["green"]!, ["blue"] = (int)r["blue"]! }), id);
            Assert.That(def.NonCraftable, Is.EqualTo((bool)r["nonCraftableFixedTable"]!), id);
            Assert.That(def.RecipeValue, Is.EqualTo((int?)r["canonicalRecipeMaterialValue"]), id);
            Assert.That((def.ExpertMaterial, def.ExpertMax), Is.EqualTo(((string)r["expertBonusMaterial"]!, (int)r["expertBonusMax"]!)), id);
        }
        var values = (JObject)Adj()["dismantle"]!["materialValues"]!;
        Assert.That(Cfg.Equipment.Salvage.MaterialValues, Is.EqualTo(values.Properties().ToDictionary(p => p.Name, p => (int)p.Value)));
        // 木石按材质：木矛返木材、石刀返石块、木弓返木材；器具/饰品/防具都有明确产出
        Assert.That((Cfg.Weapons.Single(w => w.Id == "wooden_spear").Salvage!.ItemId, Cfg.Weapons.Single(w => w.Id == "stone_knife").Salvage!.ItemId),
            Is.EqualTo(("wood", "stone")));
        Assert.That(Cfg.Gear.Single(g => g.Id == "steady_ring").Salvage!.ItemId, Is.EqualTo("scrap"));
    }

    [Test]
    public void EQ_Q46a_EveryQuality_WithAndWithoutExpert_WithinBudget()
    {
        var rules = Cfg.Equipment.Salvage;
        foreach (var (id, def) in AllEquipment())
            foreach (var q in new[] { "white", "green", "blue" })
                foreach (var expert in new[] { false, true })
                {
                    var s = New();
                    if (expert) Lv(s, "scavenging", 10);
                    var it = Make(s, id, q);
                    var yield = E.SalvageYield(s, it);
                    var value = yield.Sum(x => x.count * E.SalvageValue(x.itemId));
                    Assert.That(yield.Sum(x => x.count), Is.EqualTo(def!.Counts[q] + E.SalvageExpertBonus(s, it).extra), $"{id} {q} {expert}");
                    Assert.That(yield.All(x => x.itemId == def.ItemId), Is.True, $"{id} 只返同表材料");
                    if (def.RecipeValue is int i)
                        Assert.That(value * rules.CapDenominator, Is.LessThanOrEqualTo(i * rules.CapNumerator), $"{id} {q} 专家{expert}：5R≤4I");
                    else if (expert)
                        Assert.That(E.SalvageExpertBonus(s, it).extra, Is.EqualTo(1), $"{id} 不可自制固定追加1");
                    if (!expert) Assert.That(E.SalvageExpertBonus(s, it), Is.EqualTo((0, (string?)null)));
                }
    }

    [Test]
    public void EQ_Q46b_StoneKnife_NoExpertBonus_GreenWoodenSpear_Three()
    {
        var s = New();
        Lv(s, "scavenging", 10);
        var knife = Make(s, "stone_knife");
        Assert.That(E.SalvageYield(s, knife), Is.EqualTo(new List<(string, int)> { ("stone", 1) }), "石刀 I2 只返石1");
        var (extra, note) = E.SalvageExpertBonus(s, knife);
        Assert.That(extra, Is.Zero);
        Assert.That(note, Does.Contain("超过").And.Contain("不追加"));
        var spear = Make(s, "wooden_spear", "green");
        Assert.That(E.SalvageYield(s, spear), Is.EqualTo(new List<(string, int)> { ("wood", 3) }), "绿木矛 木2+专家木1=3≤4");
        var p = E.Preview(s, Salvage(spear.InstanceId));
        Assert.That(p.Outputs.Select(o => (o.ItemId, o.Count)), Is.EqualTo(new[] { ("wood", 3) }));
        Assert.That(p.Notes.Any(n => n.Contains("回收专家：追加木材×1")), Is.True, string.Join("|", p.Notes));
        var w0 = Sum(s, "wood");
        Start(s, Salvage(spear.InstanceId));
        Run(s);
        Assert.That(Sum(s, "wood") - w0, Is.EqualTo(3), "完成与预估同源");
    }

    [Test]
    public void EQ_Q46b_RecipeValue_EqualsConfigRecipeMaterials()
    {
        // 现有手工配方的产物：标准配方材料价值 I 与拆解表一致（防止配方与审表脱节）
        var values = Cfg.Equipment.Salvage.MaterialValues;
        var checkedIds = new List<string>();
        foreach (var rc in Cfg.Recipes.Where(r => r.Kind == "craft" && r.Outputs.Count == 1))
        {
            var (outId, n) = (rc.Outputs.Keys.Single(), rc.Outputs.Values.Single());
            var def = AllEquipment().FirstOrDefault(x => x.id == outId).def;
            if (def == null || n != 1) continue;
            Assert.That(rc.Inputs.Sum(kv => kv.Value * values[kv.Key]), Is.EqualTo(def.RecipeValue), outId);
            checkedIds.Add(outId);
        }
        Assert.That(checkedIds, Is.EquivalentTo(new[] { "stone_knife", "hatchet", "wooden_spear", "wooden_bow", "metal_spear", "crossbow" }));
    }

    [Test]
    public void EQ_Q46c_NonCraftable_FixedTable_RepairAndDurabilityDontMatter_MagazineMustBeEmpty()
    {
        var s = New();
        Lv(s, "scavenging", 10);
        var kitchen = Make(s, "kitchen_knife", "blue");
        Assert.That(E.SalvageYield(s, kitchen), Is.EqualTo(new List<(string, int)> { ("scrap", 4) }), "不可自制：固定3+专家1，不受百分比");
        // 耐久 0 仍可拆，产出与满耐久相同；维修不提高回收
        var broken = Make(s, "machete", "green");
        broken.Durability = 0;
        Assert.That(E.SalvageBlocked(s, broken.InstanceId, out _), Is.Null, "耐久0仍可拆");
        var fresh = Make(s, "machete", "green");
        Assert.That(E.SalvageYield(s, broken), Is.EqualTo(E.SalvageYield(s, fresh)));
        // 医疗器具不返无菌纱布/药品
        foreach (var id in new[] { "field_medkit", "trauma_bag", "rescue_medkit", "triage_kit" })
            Assert.That(E.SalvageOutput(Make(s, id))!.Value.itemId, Is.Not.EqualTo("medical_gauze").And.Not.EqualTo("saline"), id);
        // 弹仓非空拒绝，打空后可拆、不返填充弹药
        var pistol = Make(s, "pistol");
        pistol.LoadedAmmo = 2;
        pistol.LoadedAmmoId = "ammo_9mm";
        Assert.That(E.SalvageBlocked(s, pistol.InstanceId, out _), Does.Contain("弹仓里还有2发"));
        pistol.LoadedAmmo = 0;
        Assert.That(E.SalvageYield(s, pistol).Select(x => x.itemId), Is.EqualTo(new[] { "scrap" }));
        // 词条/品质不转移给原料：产物是普通材料
        var blue = Make(s, "work_shirt", "blue");
        var c0 = Sum(s, "cloth");
        Start(s, Salvage(blue.InstanceId));
        Run(s);
        Assert.That(s.Items.Where(i => i.ItemId == "cloth").All(i => i.Quality == null && i.Affixes == null), Is.True);
        Assert.That(Sum(s, "cloth") - c0, Is.EqualTo(2), "耐磨工作衣 布1+专家布1（I7，价值4≤5.6）");
    }

    [Test]
    public void EQ_Q46c_UnknownItem_RejectedWithReason()
    {
        var cfg = ConfigLoader.FromJson(File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "config.v0.6.json")));
        cfg.Gear.Single(g => g.Id == "steady_ring").Salvage = null;
        var e = new GameEngine(cfg);
        var s = e.NewGame(1);
        var ring = e.DebugCreateEquipment(s, "steady_ring", "white", ItemInstance.Carried);
        Assert.That(e.SalvageBlocked(s, ring.InstanceId, out _), Is.EqualTo("稳手指环没有拆解表，暂不能拆解"));
        Assert.That(e.EquipDetailLines(s, ring).Single(x => x.Label == "拆解").Value, Does.Contain("没有拆解表"));
    }

    [Test]
    public void EQ_Q46d_LockCancelStaleDoubleSave_NoDuplication_15Minutes_NoXp()
    {
        var s = New();
        var it = Make(s, "machete", "blue");
        var xp0 = s.Skills.ToDictionary(k => k.Key, k => k.Value.Xp);
        var scrap0 = Sum(s, "scrap");
        Start(s, Salvage(it.InstanceId));
        Assert.That(E.IsLocked(s, it), Is.True, "开工锁唯一实例");
        Assert.That(E.Start(s, Salvage(it.InstanceId)).Ok, Is.False, "双击/重复提交被拒");
        // 中途存读档（存档失败/回退同理）：读回后完成仍只发一次
        E.Advance(s);
        var loaded = SaveSerializer.Deserialize(SaveSerializer.Serialize(s));
        Run(loaded);
        Assert.That(Sum(loaded, "scrap") - scrap0, Is.EqualTo(3));
        Assert.That(loaded.Items.Any(i => i.InstanceId == it.InstanceId), Is.False);
        Assert.That(loaded.Skills.ToDictionary(k => k.Key, k => k.Value.Xp), Is.EqualTo(xp0), "拆解无经验（Q54 的制造经验2属 B7b）");
        // 取消：原物不变、无产出
        Assert.That(E.Cancel(s).Ok, Is.True);
        Assert.That(s.Items.Single(i => i.InstanceId == it.InstanceId).Quality, Is.EqualTo("blue"));
        Assert.That(Sum(s, "scrap"), Is.EqualTo(scrap0));
        // 陈旧请求：目标已不在
        var gone = Make(s, "machete");
        var id = gone.InstanceId;
        s.Items.Remove(gone);
        Assert.That(E.Start(s, Salvage(id)).Reason, Does.Contain("已不在"));
        // 15 基础分钟（制造1级、白天）
        var it2 = Make(s, "work_shirt");
        var t0 = s.GameMinute;
        Start(s, Salvage(it2.InstanceId));
        Run(s);
        Assert.That(s.GameMinute - t0, Is.EqualTo(15));
    }

    [Test]
    public void EQ_Q52_CraftWorkEffects_ApplyToRepairAndSalvage_PreviewEqualsProgress()
    {
        var s = New();
        Wear(s, "work_shirt", EquipSlots.Top);                       // 制造/建造工作量×0.95
        Wear(s, "repair_manual", EquipSlots.SurvivalAccessory, "green", "craftsman");   // 饰品熟作×0.97（Q51）
        var it = Make(s, "machete");
        var p = E.Preview(s, Salvage(it.InstanceId));
        Assert.That(p.FactorAtStart, Is.EqualTo(0.95 * 0.97).Within(1e-12));
        Assert.That(p.FactorReasons.Any(r => r.Contains("装备工作量×")), Is.True);
        Start(s, Salvage(it.InstanceId));
        E.Advance(s);
        Assert.That(s.Main!.WorkDone, Is.EqualTo(5 / (0.95 * 0.97)).Within(1e-9), "推进与预估同源");
        E.Cancel(s);
        it.Durability = it.MaxDurability!.Value / 2;
        E.DebugGive(s, "scrap", 1);
        E.DebugGive(s, "parts", 1);
        var rp = E.Preview(s, new ActionRequest { Type = ActionType.Craft, TargetId = "repair_selected_tool", InstanceId = it.InstanceId });
        Assert.That(rp.FactorAtStart, Is.EqualTo(0.95 * 0.97).Within(1e-12), "维修也吃制造类工作量");
        Assert.That(rp.XpGains, Has.Count.EqualTo(1), "经验不因装备改变");
    }

    // ───────────── M153：饰品兼容池（R96，Q51） ─────────────

    [Test]
    public void EQ_Q51a_CharmPools_WhiteZeroGreenOneBlueTwo_EqualWeight_NoDurable_SlotGroups()
    {
        Assert.That(E.AffixPool("steady_ring").Select(a => a.Id), Is.EquivalentTo(new[] { "accurate", "swift", "anchored" }));
        Assert.That(E.AffixPool("gather_gloves").Select(a => a.Id), Is.EquivalentTo(new[] { "craftsman", "economical", "patient", "surefoot" }));
        foreach (var id in Cfg.Gear.Where(g => g.Slot is "combat_acc" or "survival_acc").Select(g => g.Id))
            Assert.That(E.AffixPool(id).Any(a => a.Id == "durable"), Is.False, $"{id} 无耐久饰品不能耐用");
        var s = New();
        var first = new Dictionary<string, int>();
        for (var k = 0; k < 3000; k++)
        {
            foreach (var id in new[] { "steady_ring", "gather_gloves" })
            {
                var a = E.RollAffixes(s, id, 2);
                var slot = Cfg.Items.Single(i => i.Id == id).EquipSlot;
                Assert.That(a, Has.Count.EqualTo(2), id);
                Assert.That(a.Distinct().Count(), Is.EqualTo(2), $"{id} 不放回");
                Assert.That(a.Select(x => E.Affix(x)!.GroupFor(slot)).Distinct().Count(), Is.EqualTo(2), $"{id} 饰品专用组最多1条");
                Assert.That(a.All(x => E.AffixPool(id).Any(p => p.Id == x)), Is.True, $"{id} 不跨池");
                if (id == "gather_gloves") first[a[0]] = first.TryGetValue(a[0], out var c) ? c + 1 : 1;
            }
        }
        Assert.That(first.Keys, Is.EquivalentTo(new[] { "craftsman", "economical", "patient", "surefoot" }));
        Assert.That(first.Values.All(v => Math.Abs(v - 750) < 120), Is.True, "等权：" + string.Join(",", first.Select(kv => kv.Key + kv.Value)));
        foreach (var (q, n) in new[] { ("white", 0), ("green", 1), ("blue", 2) })
        {
            var it = Make(s, "focus_lens", q);
            Assert.That(it.Affixes?.Count ?? 0, Is.EqualTo(n), q);
            Assert.That(it.MaxDurability, Is.Null);
        }
        // 原衣物组不变：上衣池三条同属 survival 组，蓝色上衣仍只得 1 条
        Assert.That(E.RollAffixes(s, "work_shirt", 2), Has.Count.EqualTo(1));
    }

    [Test]
    public void EQ_Q51a_LegacyCharm_ZeroAffixes_KeptOnLoad_NotRerolled()
    {
        var s = New();
        var old = Make(s, "steady_ring", "green");
        old.Affixes = null;                    // 饰品池上线前生成的绿色饰品
        var rng = SaveSerializer.Serialize(s);
        var loaded = SaveSerializer.Deserialize(rng);
        var it = loaded.Items.Single(i => i.InstanceId == old.InstanceId);
        Assert.That((it.Quality, it.Affixes), Is.EqualTo(("green", (List<string>?)null)), "读档不补抽");
        Assert.That(E.EquipDetailLines(loaded, it).Single(x => x.Label == "品质").Value, Does.Contain("旧实例，无附加词条"));
        Assert.That(E.Equip(loaded, P0, it.InstanceId, EquipSlots.CombatAccessory).Ok, Is.True);
        Assert.That(E.Unequip(loaded, P0, EquipSlots.CombatAccessory, false).Ok, Is.True);
        Assert.That(it.Affixes, Is.Null, "穿脱不洗词条");
        var fresh = Make(s, "steady_ring", "green");
        Assert.That(E.EquipDetailLines(s, fresh).Single(x => x.Label == "品质").Value, Is.EqualTo("绿（附加词条1条）"));
    }

    [Test]
    public void EQ_Q51b_CharmAccurate_AppliesToFists_WeaponAffixNotBorrowed_Duplicate_Strongest()
    {
        var s = New();
        Wear(s, "steady_ring", EquipSlots.CombatAccessory, "green", "accurate");
        // 武器Ⅰ空（拳），武器Ⅱ一把带校准的砍刀：用武器Ⅰ页时砍刀词条不借，但饰品校准照样 +4
        Wear(s, "machete", EquipSlots.Weapon2, "green", "accurate");
        var fx1 = E.EquipmentEffects(s, P0, 1);
        Assert.That(fx1.Accuracy, Is.EqualTo(3 + 4), "稳手指环固定+3 + 饰品校准+4（含空槽拳）");
        var fx2 = E.EquipmentEffects(s, P0, 2);
        Assert.That(fx2.Accuracy, Is.EqualTo(3 + 4), "砍刀页：同名校准只取强，不叠成+8");
        var lines = E.EquipDetailLines(s, s.Items.Single(i => i.ItemId == "steady_ring"));
        Assert.That(lines.Single(x => x.Label == "附加词条").Value, Does.StartWith("校准：佩戴者全部合法攻击命中能力+4（含空槽拳击）"));
        // 实战：空手拳击的命中能力含饰品 +4
        var b = Fight(7, "feral_dog");
        Wear(b, "steady_ring", EquipSlots.CombatAccessory, "green", "accurate");
        var with = E.EquipmentEffects(b, P0, b.Battle!.ActiveWeaponSlot).Accuracy;
        Assert.That(with, Is.EqualTo(7));
    }

    [Test]
    public void EQ_Q51b_CharmSwift_SpeedCap3_Anchored_DisplaceResist_DuplicateStrongest()
    {
        var s = New();
        Wear(s, "swift_pendant", EquipSlots.CombatAccessory, "blue", "swift", "anchored");   // 固定速度+1 + 轻量+1
        Wear(s, "scout_armor", EquipSlots.Armor, "green", "swift");                          // 固定+1；轻量重复只取强
        Wear(s, "shock_baton", EquipSlots.Weapon1);                                          // 武器速度+1
        var fx = E.EquipmentEffects(s, P0, 1);
        Assert.That(fx.SpeedPositiveRaw, Is.EqualTo(1 + 1 + 1 + 1), "两件轻量只算一次");
        Assert.That(fx.Speed, Is.EqualTo(3), "正速度封顶+3");
        Assert.That(fx.DisplaceResist, Is.EqualTo(10));
        Wear(s, "heavy_armor", EquipSlots.Armor);
        s.Items.RemoveAll(i => i.ItemId == "scout_armor");
        Assert.That(E.EquipmentEffects(s, P0, 1).DisplaceResist, Is.EqualTo(25), "重型防护甲15 + 饰品稳固10");
    }

    [Test]
    public void EQ_Q51c_SurvivalCharm_FourEffects_InOriginalScenes()
    {
        var s = New();
        var charm = Wear(s, "gather_gloves", EquipSlots.SurvivalAccessory, "blue", "craftsman", "surefoot");
        Assert.That(E.GearWorkFactor(s, P0, ActionType.Craft, "knife"), Is.EqualTo(0.97).Within(1e-12));
        Assert.That(E.GearWorkFactor(s, P0, ActionType.Build, "shelter"), Is.EqualTo(0.97).Within(1e-12));
        Assert.That(E.GearWorkFactor(s, P0, ActionType.Repair, "repair_selected_tool"), Is.EqualTo(0.97).Within(1e-12));
        Assert.That(E.GearWorkFactor(s, P0, ActionType.Salvage, "salvage_equipment"), Is.EqualTo(0.97).Within(1e-12));
        Assert.That(E.EquipmentEffects(s).Factor("rate.move.energy"), Is.EqualTo(0.95));
        charm.Affixes = new List<string> { "economical", "patient" };
        var fx = E.EquipmentEffects(s);
        Assert.That(fx.Factor("rate.nonCombat.hydration"), Is.EqualTo(0.97));
        Assert.That(fx.Add("pos.shelteredRest.energy"), Is.EqualTo(0.1));
        // 与衣物上的同名词条只取强，不相乘
        Wear(s, "work_shirt", EquipSlots.Top, "green", "economical");
        Assert.That(E.EquipmentEffects(s).Factor("rate.nonCombat.hydration"), Is.EqualTo(0.97));
        // 存读档与穿脱不洗词条
        var loaded = SaveSerializer.Deserialize(SaveSerializer.Serialize(s));
        Assert.That(loaded.Items.Single(i => i.InstanceId == charm.InstanceId).Affixes, Is.EqualTo(new[] { "economical", "patient" }));
    }
}
