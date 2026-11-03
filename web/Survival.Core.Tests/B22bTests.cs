using Newtonsoft.Json.Linq;
using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 B22b：品阶野外修补包（R168，Q168 策划答复与本轮「修补包完整定稿」、survival-adjudications.json patch）。
/// 验收 PLAN-Q168-01（白绿蓝三级、每包 100、金属依据含防具、排除枪械/锅/打火器、15 基础分钟、取代 10%）、
/// PLAN-Q168-02（五例公式；满目标拒绝不耗；取消不耗；旧快照只结算一次）。
/// </summary>
public class B22bTests : TestBase
{
    private const string Repair = GameEngine.FieldPatchRepairId;

    /// <summary>当前地面放一件指定品质的装备，最大耐久固定为 max、当前为 cur。</summary>
    private ItemInstance Gear(GameState s, string itemId, string quality, double max, double cur, string? owner = null)
    {
        var it = E.DebugCreateEquipment(s, itemId, quality, owner ?? s.Ground);
        it.MaxDurability = max;
        it.Durability = cur;
        return it;
    }

    private ItemInstance Pack(GameState s, string itemId, int count = 1, string? owner = null) =>
        E.CreateItems(s, itemId, count, owner ?? s.Ground).Single();

    private static ActionRequest Req(ItemInstance target, ItemInstance? pack = null) =>
        new ActionRequest { Type = ActionType.Craft, TargetId = Repair, InstanceId = target.InstanceId, PatchInstanceId = pack?.InstanceId };

    private GameState Fresh()
    {
        var s = New();
        s.Items.RemoveAll(i => i.ItemId.StartsWith("field_patch"));
        return s;
    }

    // ───────────── PLAN-Q168-02 公式 ─────────────

    [Test]
    public void PLAN_Q168_02_FiveExamples_Formula()
    {
        Assert.That(GameEngine.FieldPatchQuote(100, 0, 100, 0, 0), Is.EqualTo((1000, 20)), "白修白");
        Assert.That(GameEngine.FieldPatchQuote(100, 0, 100, 0, 2), Is.EqualTo((1000, 5)), "蓝修白");
        Assert.That(GameEngine.FieldPatchQuote(100, 0, 100, 1, 0), Is.EqualTo((500, 20)), "白修绿：单次最多 50%");
        Assert.That(GameEngine.FieldPatchQuote(100, 0, 100, 2, 0), Is.EqualTo((250, 20)), "白修蓝：单次最多 25%");
        Assert.That(GameEngine.FieldPatchQuote(100, 20, 3, 0, 0), Is.EqualTo((150, 3)), "白包余 3 修白：恢复 15 耗 3");
        // 缺损小只耗对应耐久、最低 1；不足 0.1 拒绝；当前耐久不是整十分位时 M−d 向下取十分位
        Assert.That(GameEngine.FieldPatchQuote(100, 99.9, 100, 0, 0), Is.EqualTo((1, 1)));
        Assert.That(GameEngine.FieldPatchQuote(100, 99.95, 100, 0, 0), Is.EqualTo((0, 0)));
        Assert.That(GameEngine.FieldPatchQuote(24.3, 10, 100, 1, 1), Is.EqualTo((143, 12)), "ceil(20×14.3/24.3)=12");
        Assert.That(GameEngine.FieldPatchQuote(100, 37.4562, 100, 0, 0), Is.EqualTo((625, 13)), "62.5438 取 62.5");
        Assert.That(GameEngine.FieldPatchQuote(100, 0, 0, 0, 0), Is.EqualTo((0, 0)), "空包");
    }

    [Test]
    public void PLAN_Q168_02_EndToEnd_FiveExamples()
    {
        var cases = new (string pack, string quality, double cur, int u, double restored, int left)[]
        {
            ("field_patch", "white", 0, 100, 100, 80),
            ("field_patch_blue", "white", 0, 100, 100, 95),
            ("field_patch", "green", 0, 100, 50, 80),
            ("field_patch", "blue", 0, 100, 25, 80),
            ("field_patch", "white", 20, 3, 35, 0),
        };
        foreach (var c in cases)
        {
            var s = Fresh();
            var mach = Gear(s, "machete", c.quality, 100, c.cur);
            var pack = Pack(s, c.pack);
            pack.Durability = c.u;
            var t0 = s.GameMinute;
            Start(s, Req(mach, pack));
            Assert.That(s.Main!.Label, Does.StartWith("现场修补"));
            Run(s);
            Assert.That(s.GameMinute - t0, Is.LessThanOrEqualTo(15), "15 基础分钟");
            Assert.That(mach.Durability, Is.EqualTo(c.restored).Within(1e-9), $"{c.pack} 修 {c.quality}");
            Assert.That(mach.MaxDurability, Is.EqualTo(100), "不改最大耐久");
            Assert.That(mach.Quality, Is.EqualTo(c.quality), "不改品质");
            if (c.left == 0) Assert.That(Count(s, c.pack), Is.EqualTo(0), "零耐久包销毁");
            else Assert.That(First(s, c.pack).Durability, Is.EqualTo(c.left));
            Assert.That(s.Log.Any(l => l.Text.Contains("耐久") && l.Text.Contains("制造经验+2")), Is.True, "成功给制造 2");
        }
    }

    [Test]
    public void PLAN_Q168_02_FullTargetRejected_CancelDoesNotConsume()
    {
        var s = Fresh();
        var mach = Gear(s, "machete", "white", 100, 100);
        var pack = Pack(s, "field_patch");
        Assert.That(E.CanStart(s, Req(mach, pack)), Does.Contain("耐久已满"));
        mach.Durability = 99.95;
        Assert.That(E.CanStart(s, Req(mach, pack)), Does.Contain("缺损不足0.1"));
        Assert.That(pack.Durability, Is.EqualTo(100));

        mach.Durability = 40;
        Start(s, Req(mach, pack));
        Assert.That(E.IsLocked(s, mach) && E.IsLocked(s, pack), Is.True, "目标与包锁到提交");
        Steps(s, 1);
        Assert.That(E.Cancel(s).Ok, Is.True);
        Assert.That(mach.Durability, Is.EqualTo(40), "取消不修");
        Assert.That(pack.Durability, Is.EqualTo(100), "取消不耗包");
        Assert.That(pack.Owner, Is.EqualTo(s.Ground), "包留在原处");
        Assert.That(E.IsLocked(s, mach) || E.IsLocked(s, pack), Is.False, "取消解锁");
    }

    [Test]
    public void DefaultPack_MostRestoreThenLeastCost()
    {
        var s = Fresh();
        var mach = Gear(s, "machete", "green", 100, 0);
        var white = Pack(s, "field_patch");
        var green = Pack(s, "field_patch_green");
        var blue = Pack(s, "field_patch_blue");
        var p = E.Preview(s, Req(mach));
        Assert.That(p.Ok, Is.True, p.Reason);
        // 绿/蓝都能修满 100；蓝耗 10、绿耗 20 → 默认蓝
        Assert.That(p.Notes, Has.Some.Contains($"#{blue.InstanceId}"));
        Assert.That(p.Notes, Has.Some.Contains("恢复100"));
        Assert.That(p.ConfirmText, Is.EqualTo("开始修补"));
        // 只剩白包：可恢复 50，预览写“这次修不满”
        s.Items.Remove(blue);
        s.Items.Remove(green);
        p = E.Preview(s, Req(mach));
        Assert.That(p.Notes, Has.Some.Contains($"#{white.InstanceId}"));
        Assert.That(p.Notes, Has.Some.Contains("这次修不满"));
        Assert.That(p.BaseMinutes, Is.EqualTo(15));
        // 没有包
        s.Items.Remove(white);
        Assert.That(E.CanStart(s, Req(mach)), Does.Contain("没有可用的野外修补包"));
    }

    // ───────────── PLAN-Q168-01 可修范围 ─────────────

    [Test]
    public void PLAN_Q168_01_Eligibility_MetalWeaponsToolsArmor()
    {
        foreach (var id in new[] { "machete", "kitchen_knife", "crowbar", "metal_spear", "crossbow", "scrap_shield", "hunter_bow", "scrap_armor", "guard_armor", "heavy_armor" })
            Assert.That(E.FieldPatchEligible(id), Is.True, id);
        foreach (var id in new[] { "pistol", "shotgun", "rifle", "service_pistol", "lighter", "pot", "wooden_spear", "wooden_bow", "stone_knife", "hatchet", "padded_vest", "leather_armor", "field_patch" })
            Assert.That(E.FieldPatchEligible(id), Is.False, id);

        var s = Fresh();
        Pack(s, "field_patch");
        var gun = Gear(s, "pistol", "white", 50, 10);
        var vest = Gear(s, "scrap_armor", "white", 50, 10, ItemInstance.EquipOwner(GameState.PlayerId, EquipSlots.Armor));
        var spear = Gear(s, "wooden_spear", "white", 50, 10);
        var bow = Gear(s, "hunter_bow", "white", 50, 10);
        Assert.That(E.CanStart(s, Req(gun)), Does.Contain("枪械"));
        Assert.That(E.CanStart(s, Req(spear)), Does.Contain("不能用它修"));
        Assert.That(E.CanStart(s, Req(vest)), Is.Null, "穿戴中的防具可修");
        Assert.That(E.CanStart(s, Req(bow)), Is.Null, "制作材料命中（铁片）可修");
        var rc = E.FieldPatchRecipe!;
        Assert.That(E.RepairCandidates(s, rc).Select(i => i.ItemId), Does.Contain("scrap_armor").And.Contain("hunter_bow").And.Not.Contain("pistol").And.Not.Contain("wooden_spear"));
        // 未知品质拒绝；没有品质记录按白
        bow.Quality = "purple";
        Assert.That(E.CanStart(s, Req(bow)), Does.Contain("品质不明"));
        bow.Quality = null;
        Assert.That(E.CanStart(s, Req(bow)), Is.Null);
    }

    [Test]
    public void Pack_NotTargetOfAnyRepair_NotMaterial()
    {
        var s = Fresh();
        var pack = Pack(s, "field_patch");
        pack.Durability = 40;
        Assert.That(E.CanStart(s, Req(pack)), Does.Contain("不能修补自己"));
        Assert.That(E.RepairCandidates(s).Any(i => i.ItemId == "field_patch"), Is.False, "工具维护不补包耐久");
        Assert.That(E.RepairBlocked(s, pack.InstanceId, out _), Does.Contain("不能维护"));
        Assert.That(Cfg.Recipes.Where(r => r.Kind != "repair").SelectMany(r => r.AllInputs.Keys).Any(k => k.StartsWith("field_patch")), Is.False, "不作配方材料");
        Assert.That(E.SalvageBlocked(s, pack.InstanceId, out _), Does.Contain("不是装备"), "不能拆解");
    }

    // ───────────── 配方与品阶 ─────────────

    [Test]
    public void Grades_Recipes_AndCraftedPacksAreFull()
    {
        foreach (var (id, rank, rid, n, lv, mins) in new[] { ("field_patch", 0, "ex_field_patch", 1, 3, 30), ("field_patch_green", 1, "ex_field_patch_green", 3, 5, 60), ("field_patch_blue", 2, "ex_field_patch_blue", 6, 7, 90) })
        {
            var it = E.Item(id);
            Assert.That(it.FieldPatch!.Rank, Is.EqualTo(rank));
            Assert.That(it.FieldPatch.MaxDurability, Is.EqualTo(100));
            Assert.That(it.WeightGrams, Is.EqualTo(150));
            Assert.That(it.ShelfLifeMinutes, Is.Null);
            Assert.That(it.StackMax, Is.EqualTo(10));
            var rc = E.Recipe(rid);
            Assert.That(rc.Inputs, Is.EquivalentTo(new Dictionary<string, int> { ["iron_plate"] = n, ["adhesive"] = n, ["cloth"] = n }));
            Assert.That(rc.Outputs, Is.EquivalentTo(new Dictionary<string, int> { [id] = 2 }));
            Assert.That(rc.MinSkills["crafting"], Is.EqualTo(lv));
            Assert.That(rc.BaseMinutes, Is.EqualTo(mins));
            Assert.That(rc.Station, Is.EqualTo("workbench"));
            Assert.That(rc.Knowledge, Is.EqualTo("expedition_basics"));
        }
        Assert.That(E.Item("field_patch").Name, Is.EqualTo("野外修补包（白）"));
        Assert.That(E.Item("field_patch_green").Name, Is.EqualTo("野外修补包（绿）"));
        Assert.That(E.Item("field_patch_blue").Name, Is.EqualTo("野外修补包（蓝）"));
        Assert.That(E.Recipe(Repair).Inputs, Is.Empty, "现场修补不再整份消耗");

        var s = Fresh();
        E.DebugLearnAllKnowledge(s);
        E.DebugBuildAllWorkshops(s);
        s.Skills["crafting"].Level = 5;
        s.Items.RemoveAll(i => i.ItemId is "iron_plate" or "adhesive" or "cloth");
        foreach (var m in new[] { "iron_plate", "adhesive", "cloth" }) E.CreateItems(s, m, 3, s.CurrentLocation);
        Start(s, ActionRequest.Craft("ex_field_patch_green"));
        Run(s);
        var made = First(s, "field_patch_green", s.CurrentLocation);
        Assert.That(made.Count, Is.EqualTo(2));
        Assert.That((made.Durability, made.MaxDurability), Is.EqualTo(((double?)100, (double?)100)));
        Assert.That(made.Quality, Is.Null, "确定品阶，不抽品质词条");
        Assert.That(s.Log.Any(l => l.Text.Contains("野外修补包（绿）+2") && l.Text.Contains("制造经验+6")), Is.True);
    }

    // ───────────── 叠放与五卡 ─────────────

    [Test]
    public void Stacking_FullStackTen_UsedPackOwnCard()
    {
        var s = Fresh();
        E.CreateItems(s, "field_patch", 12, ItemInstance.Carried);
        var stacks = s.Items.Where(i => i.ItemId == "field_patch").OrderBy(i => i.InstanceId).ToList();
        Assert.That(stacks.Select(i => i.Count), Is.EqualTo(new[] { 10, 2 }), "满包同品阶最多叠 10");
        Assert.That(stacks.All(i => i.Durability == 100 && i.MaxDurability == 100), Is.True, "拆出的新堆也是满包");
        var cards0 = E.RootCardCount(s);
        var mach = Gear(s, "machete", "white", 100, 90);
        Start(s, Req(mach, stacks[0]));
        Run(s);
        Assert.That(mach.Durability, Is.EqualTo(100));
        var used = s.Items.Single(i => i.ItemId == "field_patch" && i.Durability < 100);
        Assert.That((used.Count, used.Durability), Is.EqualTo((1, (double?)98)), "只拆出一包，恢复 10 耗 2");
        Assert.That(Count(s, "field_patch"), Is.EqualTo(12));
        Assert.That(E.RootCardCount(s), Is.EqualTo(cards0 + 1), "用过的包单独一张卡");
        // 两个用过的包不合堆（剩余相同也不合）
        var other = E.CreateItems(s, "field_patch", 1, "tmp:b22b").Single();
        other.Durability = 98;
        other.Owner = ItemInstance.Carried;
        E.CreateItems(s, "field_patch", 1, ItemInstance.Carried);   // 触发随身合堆
        Assert.That(s.Items.Count(i => i.ItemId == "field_patch" && i.Durability == 98), Is.EqualTo(2));
        Assert.That(GameEngine.BatchKeyOf(used), Is.Not.EqualTo(GameEngine.BatchKeyOf(other)));
        Assert.That(Count(s, "field_patch"), Is.EqualTo(14));
        Assert.That(E.RootCardCount(s), Is.EqualTo(cards0 + 2), "满包 12 份仍 2 张，用过的 2 包各 1 张");
    }

    [Test]
    public void FiveCards_UsedPackWouldBeSixth_Rejected_GroundPackOk()
    {
        var s = Fresh();
        s.Items.RemoveAll(i => i.IsCarried);
        var stack = Pack(s, "field_patch", 2, ItemInstance.Carried);
        foreach (var id in new[] { "machete", "crowbar", "kitchen_knife", "metal_spear" }) Gear(s, id, "white", 50, 50, ItemInstance.Carried);
        Assert.That(E.RootCardCount(s), Is.EqualTo(5));
        var target = Gear(s, "crossbow", "white", 100, 50);
        Assert.That(E.CanStart(s, Req(target, stack)), Does.Contain("随身卡位已满"));
        Assert.That(E.CanStart(s, Req(target)), Does.Contain("随身卡位已满"), "只有随身包时默认选也拒绝");
        var ground = Pack(s, "field_patch");
        var p = E.Preview(s, Req(target));
        Assert.That(p.Ok, Is.True, p.Reason);
        Assert.That(p.Notes, Has.Some.Contains($"#{ground.InstanceId}"), "默认改用当前地面的包");
        // 随身只剩最后一包且会用完：不多出卡
        stack.Count = 1;
        stack.Durability = 3;
        target.Durability = 85;
        Assert.That(E.CanStart(s, Req(target, stack)), Is.Null, "整包用完不留卡");
    }

    /// <summary>开发菜单“B22b 修补包试用组”：三品阶包各 2、三品质砍刀可修、手枪被拒。</summary>
    [Test]
    public void DevKit_FieldPatch()
    {
        var s = Fresh();
        E.DebugGiveFieldPatchKit(s);
        foreach (var id in new[] { "field_patch", "field_patch_green", "field_patch_blue" }) Assert.That(Count(s, id, s.Ground), Is.EqualTo(2), id);
        foreach (var m in s.Items.Where(i => i.ItemId == "machete" && i.Owner == s.Ground))
            Assert.That(E.CanStart(s, Req(m)), Is.Null, m.Quality);
        Assert.That(E.CanStart(s, Req(s.Items.First(i => i.ItemId == "pistol" && i.Owner == s.Ground))), Does.Contain("枪械"));
    }

    /// <summary>B22b 试玩修复 M463：用过的包每包单独一张卡（卡片键 owner|itemId|pu&lt;实例号&gt;），满包仍是普通卡；批次与卡侧一致。</summary>
    [Test]
    public void M463_UsedPackOwnCard_Ground_AndCarried()
    {
        var s = Fresh();
        var full = Pack(s, "field_patch_blue", 2);
        var used = E.CreateItems(s, "field_patch_blue", 1, "tmp:m463").Single();
        used.Durability = 96;
        used.Owner = s.Ground;
        var cards = E.OrderedCards(s, s.Ground).Where(k => k.Contains("|field_patch_blue")).ToList();
        Assert.That(cards, Is.EquivalentTo(new[] { $"{s.Ground}|field_patch_blue", $"{s.Ground}|field_patch_blue|pu{used.InstanceId}" }));
        Assert.That(GameEngine.CardSideOfKey(cards[1].Split('|')), Is.EqualTo("pu" + used.InstanceId));
        Assert.That(GameEngine.IsSampleSide("pu" + used.InstanceId), Is.True, "操作窗与详情只算所点这一侧");
        Assert.That(E.CardSideOf(full), Is.Null);
        Assert.That(E.PatchDurabilitySuffix(used), Is.EqualTo(" · 耐久96/100"));
        Assert.That(E.PatchGradeTag("field_patch_blue"), Is.EqualTo("蓝"));
        Assert.That(E.PatchGradeTag("machete"), Is.Null);
    }

    /// <summary>B22b 试玩修复 M463：恢复与消耗相同时默认先用已用过的包；制作面板包列表预标超五卡。</summary>
    [Test]
    public void M463_DefaultPrefersUsedPack_AndPackNote()
    {
        var s = Fresh();
        var mach = Gear(s, "machete", "white", 100, 95);
        var full = Pack(s, "field_patch");
        var used = E.CreateItems(s, "field_patch", 1, "tmp:m463").Single();
        used.Durability = 50;
        used.Owner = s.Ground;
        var p = E.Preview(s, Req(mach));
        Assert.That(p.Notes, Has.Some.Contains($"#{used.InstanceId}"), "同为恢复5耗1：先用已用过的包");
        Assert.That(E.FieldPatchPackNote(s, full, mach), Is.Null);

        s.Items.RemoveAll(i => i.IsCarried);
        var stack = Pack(s, "field_patch", 2, ItemInstance.Carried);
        foreach (var id in new[] { "machete", "crowbar", "kitchen_knife", "metal_spear" }) Gear(s, id, "white", 50, 50, ItemInstance.Carried);
        Assert.That(E.FieldPatchPackNote(s, stack, mach), Does.Contain("会多出一张卡"));
        Assert.That(E.FieldPatchPackNote(s, stack, null), Does.Contain("单独占一张卡"));
        Assert.That(E.FieldPatchPackNote(s, full, null), Is.Null, "地面包不占随身卡");
    }

    // ───────────── 旧档 ─────────────

    [Test]
    public void Migration36To37_OldPacksBecomeWhiteFull_Idempotent()
    {
        var s = Fresh();
        var carried = Pack(s, "field_patch", 3, ItemInstance.Carried);
        var ground = Pack(s, "field_patch", 2);
        foreach (var p in new[] { carried, ground }) { p.Durability = null; p.MaxDurability = null; }
        var j = JObject.Parse(SaveSerializer.Serialize(s));
        j["SchemaVersion"] = "36";
        var t = SaveSerializer.Deserialize(j.ToString(), Cfg);
        Assert.That(t.SchemaVersion, Is.EqualTo(GameState.CurrentSchema));
        var packs = t.Items.Where(i => i.ItemId == "field_patch").OrderBy(i => i.InstanceId).ToList();
        Assert.That(packs.Select(i => (i.Count, i.Owner)), Is.EqualTo(new[] { (3, ItemInstance.Carried), (2, s.Ground) }), "数量与所有者不变");
        Assert.That(packs.All(i => i.Durability == 100 && i.MaxDurability == 100), Is.True);
        Assert.That(t.Log.Count(l => l.Text.Contains("存档已升级到版本37")), Is.EqualTo(1));
        var again = SaveSerializer.Deserialize(SaveSerializer.Serialize(t), Cfg);
        Assert.That(again.Log.Count(l => l.Text.Contains("存档已升级到版本37")), Is.EqualTo(1), "幂等");
        Assert.That(again.Items.Where(i => i.ItemId == "field_patch").Sum(i => i.Count), Is.EqualTo(5));
    }

    [Test]
    public void PLAN_Q168_02_LegacyInProgressRepair_SettlesOnceByOldSnapshot()
    {
        var s = Fresh();
        var mach = Gear(s, "machete", "white", 80, 10, ItemInstance.Carried);
        var stack = Pack(s, "field_patch", 2, ItemInstance.Carried);
        Start(s, Req(mach, stack));
        // 改造成 B16 旧档在途：没有 PatchInstanceId，旧包 1 份已预留（整份消耗）
        var a = s.Main!;
        a.LockedItemIds.Remove(stack.InstanceId);
        a.PatchInstanceId = null;
        stack.Count = 1;
        var reserved = new ItemInstance { InstanceId = s.NextInstanceId++, ItemId = "field_patch", Count = 1, Owner = ItemInstance.Carried, ReservedBy = a.ActionId };
        s.Items.Add(reserved);
        a.ReservedItemIds.Add(reserved.InstanceId);
        stack.Durability = stack.MaxDurability = null;
        var j = JObject.Parse(SaveSerializer.Serialize(s));
        j["SchemaVersion"] = "36";
        var t = SaveSerializer.Deserialize(j.ToString(), Cfg);
        Assert.That(t.Log.Any(l => l.Text.Contains("开工时的旧规则")), Is.True);
        Run(t);
        var m2 = t.Items.Single(i => i.InstanceId == mach.InstanceId);
        Assert.That(m2.Durability, Is.EqualTo(18).Within(1e-9), "旧快照：恢复最大耐久 10%");
        var left = t.Items.Where(i => i.ItemId == "field_patch").ToList();
        Assert.That(left.Sum(i => i.Count), Is.EqualTo(1), "预留的旧包 1 份已扣");
        Assert.That(left.Single().Durability, Is.EqualTo(100), "未预留的包迁成白色满包，没有再按新公式扣");
        Assert.That(t.Main, Is.Null);
    }
}
