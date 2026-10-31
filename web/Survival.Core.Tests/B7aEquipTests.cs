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
}
