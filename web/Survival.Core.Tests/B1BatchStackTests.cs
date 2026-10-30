using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 策划提交 f172677 B1 · Q24（S06 X4 用户答复；验收 COMP01、COMP02、UI-DEC01）：随身同物品按真实批次（到期 + 来源 + 预留状态）
/// 叠卡，最早到期在最前、无保质在后、同期限按实例号稳定排序；默认指向最前合法可用批次；所选批次真正作用于食用与转移；
/// 真实批次仍按批占格，五卡计数不因叠卡改变。
/// </summary>
public class B1BatchStackTests : TestBase
{
    /// <summary>清空随身，再按顺序放入烤肉：B(2000) → D(2000, 发放事件 g1) → A(3000) → C(无保质)，每批 2 份。</summary>
    private (GameState s, ItemInstance a, ItemInstance b, ItemInstance c, ItemInstance d) FourBatches()
    {
        var s = New();
        foreach (var i in s.Items.Where(i => i.IsCarried).ToList()) i.Owner = "camp";
        ItemInstance Make(int? exp, string? grant = null)
        {
            var it = E.CreateItems(s, "cooked_meat", 2, ItemInstance.Carried,
                source: grant == null ? null : new ItemSource { GrantEventId = grant }).Single();
            it.ExpiresAt = exp;
            return it;
        }
        var b = Make(2000);
        var d = Make(2000, "g1");
        var a = Make(3000);
        var c = Make(null);
        return (s, a, b, c, d);
    }

    [Test]
    public void BatchesOf_OrderAndKey_DefaultFirstUsable_CardCountUnchanged()
    {
        var (s, a, b, c, d) = FourBatches();
        var list = E.BatchesOf(s, ItemInstance.Carried, "cooked_meat");
        Assert.That(list.Select(x => x.Stacks[0].InstanceId), Is.EqualTo(new[] { b.InstanceId, d.InstanceId, a.InstanceId, c.InstanceId }),
            "最早到期在前、同期限按实例号稳定、无保质在后");
        Assert.That(list.Select(x => x.Key).Distinct().Count(), Is.EqualTo(4), "到期相同但来源不同 = 两个真实批次");
        Assert.That(list[1].GrantEventId, Is.EqualTo("g1"));
        Assert.That(GameEngine.DefaultBatch(list)!.Key, Is.EqualTo(list[0].Key), "默认最前合法可用批次");
        Assert.That(E.RootCardCount(s), Is.EqualTo(4), "真实批次按批占格，叠卡不改计数");
        Assert.That(E.RootCardsOf(s, list.SelectMany(x => x.Stacks).Select(i => i.InstanceId)), Is.EqualTo(4));

        // 最前两批过期 → 默认跳到第一个仍可用的批次
        s.GameMinute = 2000;
        list = E.BatchesOf(s, ItemInstance.Carried, "cooked_meat");
        Assert.That(list[0].Spoiled && list[1].Spoiled, Is.True);
        Assert.That(GameEngine.DefaultBatch(list)!.Stacks[0].InstanceId, Is.EqualTo(a.InstanceId));

        // 预留状态单独成批（制作预留临时拆堆），但占格仍按合堆键算
        var bb = E.BatchesOf(s, ItemInstance.Carried, "cooked_meat").First(x => x.Stacks[0].InstanceId == a.InstanceId);
        var split = bb.Stacks[0].Clone();
        split.InstanceId = s.NextInstanceId++;
        split.Count = 1;
        split.ReservedBy = 99;
        bb.Stacks[0].Count = 1;
        s.Items.Add(split);
        list = E.BatchesOf(s, ItemInstance.Carried, "cooked_meat");
        Assert.That(list.Count, Is.EqualTo(5));
        Assert.That(list.Single(x => x.Reserved).Usable, Is.False);
        Assert.That(E.RootCardCount(s), Is.EqualTo(4), "预留拆堆不算新卡");
    }

    /// <summary>所选批次作用于食用：只扣这一批；作用于转移（按批次键取堆）：只放下这一批，其余批次不动。</summary>
    [Test]
    public void ChosenBatch_UseAndDropOnlyThatBatch()
    {
        var (s, a, b, c, d) = FourBatches();
        SetStats(s, 20, 40, 60, 18, 90, 60);
        var chosen = E.BatchesOf(s, ItemInstance.Carried, "cooked_meat").Single(x => x.Stacks[0].InstanceId == a.InstanceId);
        Start(s, ActionRequest.UseInstance(chosen.Stacks[0].InstanceId));
        Run(s);
        Assert.That(a.Count, Is.EqualTo(1), "所选的 A 批扣 1 份");
        Assert.That(new[] { b.Count, c.Count, d.Count }, Is.All.EqualTo(2), "最早到期的 B 批没被动");

        // 放下所选批次（来源 g1 的 D 批）：与 B 同到期，但按批次键只取 D
        var dk = GameEngine.BatchKeyOf(d);
        var stacks = s.Items.Where(i => i.IsCarried && i.ItemId == "cooked_meat" && i.ReservedBy == null && GameEngine.BatchKeyOf(i) == dk).ToList();
        Assert.That(stacks.Select(i => i.InstanceId), Is.EqualTo(new[] { d.InstanceId }));
        foreach (var st in stacks) Assert.That(E.DoInstant(s, InstantRequest.Drop(st.InstanceId, st.Count)).Ok, Is.True);
        Assert.That(b.IsCarried && b.Count == 2, Is.True, "同到期的 B 批留在随身");
        Assert.That(s.Items.Where(i => i.Owner == "camp" && i.ItemId == "cooked_meat").Sum(i => i.Count), Is.EqualTo(2));
        Assert.That(E.RootCardCount(s), Is.EqualTo(3));
    }
}
