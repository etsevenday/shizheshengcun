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
}
