using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 B45「设施加工移至中央次级菜单、右栏只读」内核部分（R206、R207）。
/// 规则出处：S14「2026-11-13 B45 Q556—Q557 主要问题定稿」与验收 Q556-A01—A03、Q557-A01—A04；《重墨 V2 开工裁定与界面逐字稿》Q546「内核通用煅烧引导」。
/// </summary>
public class B45FacMenuCoreTests : TestBase
{
    private static FacilityState Fac(GameState s) => s.Locations["camp"].Facility;

    private static void Ok(ActionResult r) => Assert.That(r.Ok, Is.True, r.Reason);

    private GameState Clean()
    {
        var s = New();
        s.Items.RemoveAll(i => i.ItemId is "raw_root" or "wood" or "pot");
        return s;
    }

    // ───────────── Q557 明确工位 ─────────────

    /// <summary>Q557-A01：两位都空时明确位2放入1份，真实占位2、位1仍空；本位最大1，旧数量2请求整单拒绝。</summary>
    [Test]
    public void Q557_A01_SlotTargetedPutLandsInThatSlot()
    {
        var s = Clean();
        var roots = E.CreateItems(s, "raw_root", 3, ItemInstance.Carried).Single();
        Assert.That(E.MaxPutInFire(s, roots, out var why), Is.EqualTo(2), why);
        Assert.That(E.MaxPutInFire(s, roots, out why, 1), Is.EqualTo(1), "明确工位最多 1 份");

        var bad = E.DoInstant(s, InstantRequest.PutInFireSlot(roots.InstanceId, 1, 2));
        Assert.That(bad.Ok, Is.False);
        Assert.That(bad.Reason, Is.EqualTo("请输入 1 到 1 之间的整数。"));
        Assert.That(E.FireSlots(s, "camp").All(x => x == null), "拒绝不部分投");
        Assert.That(roots.Count, Is.EqualTo(3));

        Ok(E.DoInstant(s, InstantRequest.PutInFireSlot(roots.InstanceId, 1)));
        var slots = E.FireSlots(s, "camp");
        Assert.That(slots[0], Is.Null, "位1仍空");
        Assert.That(slots[1]?.ItemId, Is.EqualTo("raw_root"));
        Assert.That(slots[1]!.Count, Is.EqualTo(1));
        Assert.That(E.FireSlotOf(s, slots[1]!), Is.EqualTo(1));
        Assert.That(roots.Count, Is.EqualTo(2));

        // 整卡原规则：自动第一空位
        Ok(E.DoInstant(s, InstantRequest.PutInFire(roots.InstanceId)));
        Assert.That(E.FireSlots(s, "camp")[0]?.ItemId, Is.EqualTo("raw_root"));
    }

    /// <summary>Q557-A01：一整锅同样可定点放到位2。</summary>
    [Test]
    public void Q557_A01_PotToSlot2()
    {
        var s = Clean();
        var pot = E.CreateItems(s, "pot", 1, ItemInstance.Carried).Single();
        Ok(E.DoInstant(s, InstantRequest.PutInFireSlot(pot.InstanceId, 1)));
        var slots = E.FireSlots(s, "camp");
        Assert.That(slots[0], Is.Null);
        Assert.That(slots[1]?.InstanceId, Is.EqualTo(pot.InstanceId));
    }

    /// <summary>Q557-A02：目标位被占而另一位空仍拒绝占位句，不挪旧物、不改投。</summary>
    [Test]
    public void Q557_A02_TakenSlotRejectedEvenIfOtherFree()
    {
        var s = Clean();
        var roots = E.CreateItems(s, "raw_root", 2, ItemInstance.Carried).Single();
        Ok(E.DoInstant(s, InstantRequest.PutInFireSlot(roots.InstanceId, 1)));
        var first = E.FireSlots(s, "camp")[1]!;
        Assert.That(E.MaxPutInFire(s, roots, out var why, 1), Is.EqualTo(0));
        Assert.That(why, Is.EqualTo("加工位2已被占用，请重新选择。"));
        var r = E.DoInstant(s, InstantRequest.PutInFireSlot(roots.InstanceId, 1));
        Assert.That(r.Ok, Is.False);
        Assert.That(r.Reason, Is.EqualTo("加工位2已被占用，请重新选择。"));
        var slots = E.FireSlots(s, "camp");
        Assert.That(slots[0], Is.Null, "不改投第一空位");
        Assert.That(slots[1]!.InstanceId, Is.EqualTo(first.InstanceId), "不挪旧物");
        Assert.That(roots.Count, Is.EqualTo(1));
    }

    /// <summary>Q557-A01/A04：菜单工位落点 fslot:{序号}:{地点} 只放 1 份并落在该位；整卡 fire: 落点仍按全部空位（Shift 上限 2）。</summary>
    [Test]
    public void Q557_SlotDropAndWholeCardDrop()
    {
        var s = Clean();
        E.CreateItems(s, "raw_root", 3, ItemInstance.Carried);
        var t = DropTarget.Parse("fslot:1:camp")!;
        Assert.That(t.Kind, Is.EqualTo(DropTargetKind.FireSlot));
        Assert.That(t.Slot, Is.EqualTo(1));
        Assert.That(t.Id, Is.EqualTo("camp"));
        var safe = DropTarget.Parse("fslot:0:safehouseFloor:sh_x")!;
        Assert.That(safe.Id, Is.EqualTo("safehouseFloor:sh_x"), "地点 ID 含冒号");

        var whole = E.ResolveDrop(s, DropSource.Parse("card:carried|raw_root")!, DropTarget.Parse("fire:camp")!);
        Assert.That(whole.Ok, Is.True, whole.Reason);
        Assert.That(whole.MaxQuantity, Is.EqualTo(2), "整卡仍按全部空位");
        Assert.That(whole.Instant!.Slot, Is.Null);

        var drop = E.ResolveDrop(s, DropSource.Parse("card:carried|raw_root")!, t);
        Assert.That(drop.Ok, Is.True, drop.Reason);
        Assert.That(drop.MaxQuantity, Is.EqualTo(1));
        Assert.That(drop.Instant!.Slot, Is.EqualTo(1));
        Ok(E.DoInstant(s, drop.Instant));
        Assert.That(E.FireSlots(s, "camp")[0], Is.Null);
        Assert.That(E.FireSlots(s, "camp")[1]?.ItemId, Is.EqualTo("raw_root"));

        var again = E.ResolveDrop(s, DropSource.Parse("card:carried|raw_root")!, t);
        Assert.That(again.Ok, Is.False);
        Assert.That(again.Reason, Is.EqualTo("加工位2已被占用，请重新选择。"));
    }

    /// <summary>Q557-A04：新工位分配随存档保存，读档后仍在原指定工位。</summary>
    [Test]
    public void Q557_A04_SlotSurvivesSave()
    {
        var s = Clean();
        var root = E.CreateItems(s, "raw_root", 1, ItemInstance.Carried).Single();
        Ok(E.DoInstant(s, InstantRequest.PutInFireSlot(root.InstanceId, 1)));
        var t = SaveSerializer.Deserialize(SaveSerializer.Serialize(s));
        var slots = E.FireSlots(t, "camp");
        Assert.That(slots[0], Is.Null);
        Assert.That(slots[1]?.ItemId, Is.EqualTo("raw_root"));
    }

    // ───────────── Q556 木材 ─────────────

    /// <summary>Q556-A02：木材作为明确工位放入一律拒绝，原因为燃料说明＋Q546 当前配方新引导；不加燃料、不占位。</summary>
    [Test]
    public void Q556_A02_WoodSlotPutRejected()
    {
        var s = Clean();
        var wood = E.CreateItems(s, "wood", 4, ItemInstance.Carried).Single();
        var fuel = Fac(s).FuelMinutes;
        const string reason = "拖木材到火堆卡是添加燃料。要煅烧木炭，请点击火堆或柴炉卡，在加工菜单的空加工位点「煅烧木炭」（每批木材×4）。";
        Assert.That(E.MaxPutInFire(s, wood, out var why, 0), Is.EqualTo(0));
        Assert.That(why, Is.EqualTo(reason));
        var r = E.DoInstant(s, InstantRequest.PutInFireSlot(wood.InstanceId, 0));
        Assert.That(r.Ok, Is.False);
        Assert.That(r.Reason, Is.EqualTo(reason));
        Assert.That(Fac(s).FuelMinutes, Is.EqualTo(fuel));
        Assert.That(E.FireSlots(s, "camp").All(x => x == null));
    }

    /// <summary>Q556-A03：菜单里把木材拖到工位仍是添加燃料（原燃料预览），不读定点 1 份规则。</summary>
    [Test]
    public void Q556_A03_WoodDropOnSlotIsFuel()
    {
        var s = Clean();
        E.CreateItems(s, "wood", 3, ItemInstance.Carried);
        var drop = E.ResolveDrop(s, DropSource.Parse("card:carried|wood")!, DropTarget.Parse("fslot:0:camp")!);
        Assert.That(drop.Verb, Is.EqualTo(DropVerb.AddFuel));
        Assert.That(drop.Label, Is.EqualTo($"投放1木材，燃料+{E.Config.Fire.BurnMinutesPerWood}分钟"));
        Ok(E.DoInstant(s, drop.Instant!));
        Assert.That(E.FireSlots(s, "camp").All(x => x == null), "木材不占加工位");
    }

    /// <summary>Q556-A02：没有煅烧配方时只留前句。</summary>
    [Test]
    public void Q556_A02_NoCharcoalRecipeOnlyFirstSentence()
    {
        Assert.That(GameEngine.WoodIsFuelNote, Is.EqualTo("拖木材到火堆卡是添加燃料。"));
        Assert.That(GameEngine.SlotTakenText(1), Is.EqualTo("加工位1已被占用，请重新选择。"));
        Assert.That(GameEngine.QtyRangeText(5), Is.EqualTo("请输入 1 到 5 之间的整数。"));
    }
}
