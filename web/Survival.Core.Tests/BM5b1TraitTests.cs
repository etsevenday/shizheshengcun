using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 BM5b1 续做（R236）Q638：固定特性资格按真实持有人／穿戴者判断，未穿戴可介绍。规则出处：S14「2026-11-14 Q637—Q643：分层原因、装备资格与精简敌踪详情」Q638
/// 与验收 Q638-A01／A02；正式句见《重墨 V2 开工裁定与界面逐字稿》「2026-11-14 Q637—Q643正式文字与显示分工」。
/// </summary>
public class BM5b1TraitTests : TestBase
{
    private const string OldSuffix = "（相关技能或多人战斗上线后生效）";

    private ItemInstance Make(GameState s, string id, string owner) => E.CreateItems(s, id, 1, owner).Single();

    private string? TraitRow(GameState s, ItemInstance it) =>
        E.RailEquipLines(s, it, out _).Where(x => x.Label == "固定特性").Select(x => x.Value).SingleOrDefault();

    /// <summary>Q638-A01：未穿戴但持有人技能已解锁的已实装特性进主区（组名「具备特性」、不写生效中）；地面按你、同伴按本人；缺技能或未实装留说明。</summary>
    [Test]
    public void Q638_A01_QualifyByRealHolder()
    {
        var s = New();
        var machete = Make(s, "machete", ItemInstance.Carried);
        var trait = E.Weapon("machete")!.TraitText!;
        // 绑定技能（割伤：流血）未解锁 → 说明组原特性句，不再写“上线后生效”
        Assert.That(E.SkillLevel(s, "blade"), Is.LessThan(3));
        Assert.That(E.EquipActiveTraitLines(s, machete), Is.Empty);
        Assert.That(TraitRow(s, machete), Is.EqualTo(trait), "已实装但未解锁：完整原句留说明");
        // 你解锁割伤 → 随身未穿戴也进主区
        E.DebugSetSkillLevel(s, "blade", 3);
        Assert.That(E.EquipActiveTraitLines(s, machete), Is.EqualTo(new[] { trait }));
        Assert.That(TraitRow(s, machete), Is.Null, "每条一次");
        Assert.That(E.EquipTraitGroupLabel(machete), Is.EqualTo("具备特性"));
        Assert.That(GameEngine.WornTriggerHint, Is.EqualTo("穿戴后可触发"));
        // 地面按主角「你」
        var ground = Make(s, "machete", s.Ground);
        Assert.That(E.TraitHolderId(s, ground), Is.EqualTo(GameState.PlayerId));
        Assert.That(E.EquipActiveTraitLines(s, ground), Is.EqualTo(new[] { trait }));
        // 同伴随身按本人技能，不拿主角熟练度覆盖
        E.DebugQuickParty(s, 2);
        var mate = s.Party.First(id => id != GameState.PlayerId);
        var mateKnife = Make(s, "machete", ItemInstance.CarriedOwner(mate));
        Assert.That(E.TraitHolderId(s, mateKnife), Is.EqualTo(mate));
        Assert.That(E.SkillLevel(s.Actors[mate], "blade"), Is.LessThan(3));
        Assert.That(E.EquipActiveTraitLines(s, mateKnife), Is.Empty, "同伴没解锁割伤");
        // 无技能前提的已实装特性直接具备（狩猎：任一攻击；破门：Q137 条件工具倍率）
        foreach (var id in new[] { "hunter_bow", "fire_axe", "riot_shield", "signal_whistle", "wooden_spear" })
            Assert.That(E.EquipActiveTraitLines(s, Make(s, id, ItemInstance.Carried)), Is.EqualTo(new[] { E.Weapon(id)!.TraitText }), id);
        // 未实装：留说明原行
        foreach (var id in new[] { "wooden_bow", "repeater_crossbow", "stone_knife", "rescue_shield" })
        {
            var it = Make(s, id, ItemInstance.Carried);
            Assert.That(E.EquipActiveTraitLines(s, it), Is.Empty, id);
            Assert.That(TraitRow(s, it), Is.EqualTo(E.Weapon(id)!.TraitText + OldSuffix), id);
        }
        // 只读：阅读不加人物属性、不写“生效中”
        Assert.That(E.RailEquipLines(s, machete, out var act).Any(x => x.Value.Contains("生效中")), Is.False);
        Assert.That(act.Any(x => x.Contains("生效中")), Is.False);
    }

    /// <summary>Q638-A02：换持有人／穿脱／技能／损坏状态刷新正确；触发型沿原条件句；旧“技能／多人未上线”句不覆盖已实装效果；整备比较原行与数值不变。</summary>
    [Test]
    public void Q638_A02_RefreshAndNoStaleText()
    {
        var s = New();
        E.DebugQuickParty(s, 2);
        var mate = s.Party.First(id => id != GameState.PlayerId);
        E.DebugSetSkillLevel(s, "blade", 3);
        var knife = Make(s, "machete", ItemInstance.Carried);
        Assert.That(E.EquipActiveTraitLines(s, knife), Is.Not.Empty);
        // 交给同伴：按本人重算
        knife.Owner = ItemInstance.CarriedOwner(mate);
        Assert.That(E.EquipActiveTraitLines(s, knife), Is.Empty);
        // 穿戴：取穿戴者；组名「生效效果」
        knife.Owner = ItemInstance.Carried;
        Assert.That(E.Equip(s, GameState.PlayerId, knife.InstanceId, EquipSlots.Weapon1).Ok, Is.True);
        Assert.That(knife.IsWornBy(GameState.PlayerId), Is.True);
        Assert.That(E.EquipActiveTraitLines(s, knife), Is.EqualTo(new[] { E.Weapon("machete")!.TraitText }));
        Assert.That(E.EquipTraitGroupLabel(knife), Is.EqualTo(GameEngine.ActiveEffectsLabel));
        // 损坏：效果暂停，不列；说明组原句（不写上线后生效）
        knife.Durability = 0;
        Assert.That(E.EquipActiveTraitLines(s, knife), Is.Empty);
        Assert.That(TraitRow(s, knife), Is.EqualTo(E.Weapon("machete")!.TraitText));
        // 木盾：条件句摘要、说明留原句与独有条件
        var shield = Make(s, "wooden_shield", ItemInstance.Carried);
        Assert.That(E.EquipActiveTraitLines(s, shield).Single(), Does.StartWith("盾面：格挡时减伤"));
        Assert.That(TraitRow(s, shield), Is.EqualTo("盾面：盾类格挡减伤60%，不被动增加护甲"));
        // 散布绑定专长射击（7 级）：未解锁留说明，解锁后进主区
        var shotgun = Make(s, "shotgun", ItemInstance.Carried);
        Assert.That(E.EquipActiveTraitLines(s, shotgun), Is.Empty);
        E.DebugSetSkillLevel(s, "firearms", 7);
        Assert.That(E.EquipActiveTraitLines(s, shotgun), Is.EqualTo(new[] { E.Weapon("shotgun")!.TraitText }));
        // 全部武器：已实装的特性在右栏不再带旧“上线后生效”句；整备比较原行不改（Q609）
        foreach (var w in E.Config.Weapons.Where(x => x.TraitText != null))
        {
            var it = Make(s, w.Id, ItemInstance.Carried);
            var q = E.WeaponTraitQualify(s, it, w);
            var row = TraitRow(s, it);
            if (q != GameEngine.TraitQualify.NotImplemented) Assert.That(row ?? "", Does.Not.Contain(OldSuffix), w.Id);
            Assert.That(E.EquipDetailLines(s, it).Any(x => x.Label == "固定特性"), Is.True, $"{w.Id}：一级面板内页原行");
        }
    }

    /// <summary>Q638 壳：对象详情与整备只读详情用同一组名（穿戴中「生效效果」、未穿戴「具备特性」）与「穿戴后可触发」小字。</summary>
    [Test]
    public void Q638_Shell_Wiring()
    {
        foreach (var (file, v) in new[] { ("ItemDetail.razor", "inst"), ("GearPeekDetail.razor", "it") })
        {
            var src = File.ReadAllText(Path.Combine(Root(), "web", "Survival.Web", "Components", file)).Replace("\r\n", "\n");
            Assert.That(src, Does.Contain($"<div class=\"wide rail-fx\"><span>@e.EquipTraitGroupLabel({v})</span><b>@fx</b></div>"), file);
            Assert.That(src, Does.Contain($"@if (activeTraits.Count > 0 && !{v}.IsWorn)"), file);
            Assert.That(src, Does.Contain("<div class=\"wide rail-fx-note\">@GameEngine.WornTriggerHint</div>"), file);
        }
        var s14 = File.ReadAllText(Path.Combine(Root(), "doc", "策划案", "系统策划总册 v1.0", "01 基础系统", "S14 主界面与全局UI交互系统.md"));
        Assert.That(s14, Does.Contain("组名“" + GameEngine.HeldTraitsLabel + "”"));
        Assert.That(s14, Does.Contain("可注“" + GameEngine.WornTriggerHint + "”"));
    }

    private static string Root()
    {
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "web", "Survival.sln"))) dir = dir.Parent;
        return dir!.FullName;
    }
}
