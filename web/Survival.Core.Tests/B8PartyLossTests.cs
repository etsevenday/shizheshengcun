using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 B8 M185：队友死亡与遗物（PT04/PT06/PT07、COMP17 的“死亡先结、遗物唯一”）、RB01 同行补员（NP-A06）、
/// 借用矛限制与报失（PT05、NP-A07、NP-A20）。出处 R08 §4、A11 §7/§8。
/// </summary>
public class B8PartyLossTests : TestBase
{
    private GameState Camp(int c = 4, uint seed = 12345)
    {
        var s = New(seed);
        if (c >= 2) s.Progress.Flags.Add("hub_registered");
        if (c >= 3) s.Progress.Flags.Add("church_route_record");
        if (c >= 4) s.Progress.Flags.Add("academy_safety_record");
        s.SpawnNextCheck = int.MaxValue;
        s.PatrolNextCheck = int.MaxValue;
        s.GameMinute = At(1, 10, 0);
        return s;
    }

    private void WaitHour(GameState s)
    {
        Start(s, ActionRequest.Wait(60));
        Run(s);
        s.Main = null;
    }

    /// <summary>PT04/COMP17：世界 tick 里队友死亡——名册记死亡、离队、C 不降；穿戴与随身一次迁到唯一遗物，一件不多不少、保质到期分钟不变；之后不再生成。</summary>
    [Test]
    public void PT04_WorldTickDeath_SingleRemains_ItemsExact_ExpiryKept()
    {
        var s = Camp(4);
        E.DebugRecruit(s, "npc_lin_zhao");
        var lin = s.Actors["npc_lin_zhao"];
        var food = E.CreateItems(s, "berry", 2, ItemInstance.CarriedOwner("npc_lin_zhao")).Single();
        var expires = food.ExpiresAt;
        Assert.That(expires, Is.Not.Null);
        var ids = s.Items.Where(i => i.Owner.Contains("npc_lin_zhao")).Select(i => i.InstanceId).OrderBy(x => x).ToList();
        Assert.That(ids.Count, Is.EqualTo(3));
        var total = s.Items.Count;
        lin.Stats.Health = 0.01;
        lin.Stats.Wakefulness = 0;
        WaitHour(s);
        var r = E.RosterOf(s, "npc_lin_zhao")!;
        Assert.That(r.Dead, Is.True);
        Assert.That(r.DeathLocation, Is.EqualTo("camp"));
        Assert.That(s.Party, Is.EqualTo(new[] { "p0" }));
        Assert.That(s.FormationPreset, Is.EqualTo(new[] { "p0", "", "", "" }), "死者那格清空（B8a 预设 4 格）");
        Assert.That(E.PartyCapacity(s), Is.EqualTo(4), "C 不降");
        Assert.That(s.Remains.Keys, Is.EqualTo(new[] { "npc_lin_zhao" }));
        Assert.That(E.RemainsItems(s, "npc_lin_zhao").Select(i => i.InstanceId).OrderBy(x => x), Is.EqualTo(ids), "一件不多不少");
        Assert.That(s.Items.Count, Is.EqualTo(total));
        Assert.That(E.FindItem(s, food.InstanceId)!.ExpiresAt, Is.EqualTo(expires), "保质不刷新");
        Assert.That(s.Log.Count(l => l.Text.Contains("林昭死了")), Is.EqualTo(1));
        WaitHour(s);
        Assert.That(s.Remains.Count, Is.EqualTo(1));
        Assert.That(s.Log.Count(l => l.Text.Contains("林昭死了")), Is.EqualTo(1), "死人不再结算");
        Assert.That(E.AddNamedToRoster(s, "npc_lin_zhao").Reason, Does.Contain("不会复活"));
    }

    /// <summary>候补死亡：遗物在营区（替身 camp）；战斗中死亡的遗物在战斗结束前不能取，结束后可取到随身或地面，取空后容器移除。</summary>
    [Test]
    public void Remains_BattleDeathLockedUntilEnd_ThenTakeToCarriedOrGround()
    {
        var s = Camp(4);
        E.DebugRecruit(s, "npc_tang_li");
        s.Battle = new BattleState { BattleId = 9, LocationId = "camp", Allies = new List<string> { "p0", "npc_tang_li" } };
        Assert.That(E.DebugActor(s, "npc_tang_li", "kill"), Does.Contain("已死亡"));
        Assert.That(s.Battle.Allies, Is.EqualTo(new[] { "p0" }), "号位清空（末尾空位省略）");
        Assert.That(s.Remains["npc_tang_li"].BattleId, Is.EqualTo(9));
        var items = E.RemainsItems(s, "npc_tang_li");
        Assert.That(items.Select(i => i.ItemId), Is.EquivalentTo(new[] { "wooden_bow", "stone_knife" }));
        Assert.That(E.TakeFromRemainsBlocked(s, "npc_tang_li", items[0].InstanceId, false), Does.Contain("战斗结束后"));
        s.Battle = null;
        Assert.That(E.TakeFromRemains(s, "npc_tang_li", items[0].InstanceId, false).Ok, Is.True);
        Assert.That(E.FindItem(s, items[0].InstanceId)!.IsCarried, Is.True);
        E.DebugTeleport(s, "field");
        Assert.That(E.TakeFromRemainsBlocked(s, "npc_tang_li", items[1].InstanceId, true), Does.Contain("要到那里"));
        E.DebugTeleport(s, "camp");
        Assert.That(E.TakeFromRemains(s, "npc_tang_li", items[1].InstanceId, true).Ok, Is.True);
        Assert.That(E.FindItem(s, items[1].InstanceId)!.Owner, Is.EqualTo("camp"));
        Assert.That(s.Remains.ContainsKey("npc_tang_li"), Is.False, "取空后容器移除，不再生成");
    }

    /// <summary>PT06：主角死亡终局，不续局、不生成可玩遗物；队友不因此死亡或接管。</summary>
    [Test]
    public void PT06_PlayerDeath_NoPlayableRemains()
    {
        var s = Camp(2);
        E.DebugRecruit(s, "npc_shen_he");
        s.Stats.Health = 0.01;
        s.Stats.Wakefulness = 0;
        Start(s, ActionRequest.Wait(60));
        Run(s);
        Assert.That(s.Progress.Dead, Is.True);
        Assert.That(s.Remains, Is.Empty);
        Assert.That(E.RosterOf(s, "npc_shen_he")!.Dead, Is.False);
        Assert.That(E.SetActive(s, "npc_shen_he", false).Reason, Does.Contain("本局已结束"));
    }

    /// <summary>PT07/NP-A05：任一命名队友死亡后编队仍可用；NP-A06：三名队员死亡后 C 不降，RB01 补员（30 分钟 wait），新人技能 1、速度 4、借用矛。</summary>
    [Test]
    public void PT07_NP_A06_AfterDeaths_PartyAndRb01StillWork()
    {
        var s = Camp(4);
        foreach (var id in new[] { "npc_lin_zhao", "npc_zhou_yan", "npc_shen_he", "npc_tang_li" }) E.DebugRecruit(s, id);
        Assert.That(E.SetActive(s, "npc_lin_zhao", false).Ok, Is.True);
        Assert.That(E.SetActive(s, "npc_tang_li", true).Ok, Is.True, "PT07：编队照常");
        foreach (var id in new[] { "npc_zhou_yan", "npc_shen_he", "npc_tang_li" }) E.DebugActor(s, id, "kill");
        Assert.That(E.PartyCapacity(s), Is.EqualTo(4));
        Assert.That(E.EscortRegistrationBlocked(s), Does.Contain("林昭"), "有存活候补先编候补");
        Assert.That(E.SetActive(s, "npc_lin_zhao", true).Ok, Is.True);
        var t0 = s.GameMinute;
        Assert.That(E.StartEscortRegistration(s, "rb-1").Ok, Is.True);
        Assert.That(s.Main!.Label, Does.Contain("登记同行补员"));
        Run(s);
        Assert.That(s.GameMinute - t0, Is.EqualTo(30));
        var esc = E.AliveEscorts(s).Single();
        Assert.That(esc.Name, Is.EqualTo("护送员#1"));
        Assert.That(s.Party, Does.Contain(esc.ActorId));
        var a = s.Actors[esc.ActorId];
        Assert.That(a.Skills.Values.All(k => k.Level == 1), Is.True);
        Assert.That(E.ActorBaseSpeed(s, esc.ActorId), Is.EqualTo(4));
        Assert.That(E.ActorProfile(s, esc.ActorId)!.ControlResist, Is.EqualTo(5));
        var spear = s.Items.Single(i => i.Owner == ItemInstance.EquipOwner(esc.ActorId, EquipSlots.Weapon1));
        Assert.That(spear.ItemId, Is.EqualTo("wooden_spear"));
        Assert.That(spear.LoanSlot, Is.EqualTo("loan_1"));
        Assert.That(spear.Quality, Is.EqualTo("white")); Assert.That(spear.Affixes, Is.Null);
        Assert.That(spear.MaxDurability, Is.EqualTo(30), "固定模板取最大值，不抽随机耐久");
        Assert.That(s.Items.Count(i => i.Owner.Contains(esc.ActorId)), Is.EqualTo(1), "其余六槽与五卡空");
    }

    /// <summary>RB01 条件：无营区登记、同行已满、护送员达 C−1、离开营区各自拒绝；中途取消不建人；requestId 重放不双发。</summary>
    [Test]
    public void Rb01_Conditions_AndCancelCreatesNobody()
    {
        var s = Camp(1);
        Assert.That(E.EscortRegistrationBlocked(s), Does.Contain("营区登记"));
        s.Progress.Flags.Add("hub_registered");
        Assert.That(E.EscortRegistrationBlocked(s), Is.Null);
        Assert.That(E.StartEscortRegistration(s, "x").Ok, Is.True);
        Assert.That(E.StartEscortRegistration(s, "x").Duplicate, Is.True);
        Advance(s, 2);
        E.Cancel(s);
        Assert.That(E.AliveEscorts(s), Is.Empty, "中途取消不建人");
        Assert.That(E.DebugAddEscort(s), Does.Contain("编入同行"));
        Assert.That(E.EscortRegistrationBlocked(s), Does.Contain("同行没有空位"));
        Assert.That(E.DebugAddEscort(s), Does.Contain("上限 1"), "C2 时护送员最多 1 名");
        E.DebugTeleport(s, "field");
        Assert.That(E.EscortRegistrationBlocked(s), Does.Contain("暂代收容所"), "B8b：RB01 是收容所服务（Q88），迁址前在 camp 代办");
    }

    /// <summary>PT05/NP-A07/NP-A20：借用矛不能拆解、不作配方材料/燃料；活人持有/地面/遗物里报失被拒并指路；耐久 0 才原子撤销补一件；新护送员沿用已有有效矛。</summary>
    [Test]
    public void PT05_NP_A07_NP_A20_LoanSpear_RestrictionsAndReportLost()
    {
        var s = Camp(2);
        E.DebugAddEscort(s);
        var esc = E.AliveEscorts(s).Single();
        var spear = s.Items.Single(i => i.LoanSlot == "loan_1");
        Assert.That(E.ReportLostLoanBlocked(s, "loan_1"), Does.Contain("护送员#1身上"));
        // 卸到营地地面：仍可回收 → 只指路；不能拆、不能作材料
        spear.Owner = "camp";
        Assert.That(E.ReportLostLoanBlocked(s, "loan_1"), Does.Contain("可以去取回"));
        Assert.That(E.SalvageBlocked(s, spear.InstanceId, out _), Does.Contain("不能拆解"));
        Assert.That(E.UsableBatches(s, "wooden_spear", true), Is.Empty, "不作配方材料");
        // 护送员死了：矛在遗物里 → 只指路
        spear.Owner = ItemInstance.EquipOwner(esc.ActorId, EquipSlots.Weapon1);
        E.DebugActor(s, esc.ActorId, "kill");
        Assert.That(spear.InRemains, Is.True);
        Assert.That(E.ReportLostLoanBlocked(s, "loan_1"), Does.Contain("遗物里"));
        // 新护送员沿用有效矛，不另发
        var before = s.Items.Count(i => i.ItemId == "wooden_spear");
        E.DebugAddEscort(s);
        Assert.That(s.Items.Count(i => i.ItemId == "wooden_spear"), Is.EqualTo(before), "已有有效矛不再造");
        // 损毁后可报失：旧实例撤销、补一件到地面；重放不双发
        spear.Durability = 0;
        Assert.That(E.ReportLostLoanBlocked(s, "loan_1"), Is.Null);
        Assert.That(E.ReportLostLoan(s, "loan_1", "lost-1").Ok, Is.True);
        Assert.That(E.ReportLostLoan(s, "loan_1", "lost-1").Duplicate, Is.True);
        Assert.That(E.FindItem(s, spear.InstanceId), Is.Null, "旧实例作废，不能再拾取或拆解");
        Assert.That(s.Items.Count(i => i.LoanSlot == "loan_1"), Is.EqualTo(1));
        var fresh = s.Items.Single(i => i.LoanSlot == "loan_1");
        Assert.That(fresh.Owner, Is.EqualTo("camp"));
        Assert.That(s.Items.Count(i => i.ItemId == "wooden_spear"), Is.EqualTo(before), "撤销一件、补一件");
        Assert.That(fresh.Durability, Is.EqualTo(30));
        Assert.That(s.Remains.ContainsKey(esc.ActorId), Is.False, "遗物只剩作废的矛时随之移除");
        // 新矛又在活人身上 → 不能借报失偷走
        fresh.Owner = ItemInstance.Carried;
        Assert.That(E.ReportLostLoanBlocked(s, "loan_1"), Does.Contain("你的随身"));
    }

    /// <summary>借用位发过的矛已损毁：新护送员不另发，提示去报失（不能借补员绕过报失）。</summary>
    [Test]
    public void Rb01_BrokenLoanSpear_NotReissuedByNewEscort()
    {
        var s = Camp(3);
        E.DebugAddEscort(s);
        var first = E.AliveEscorts(s).Single();
        var spear = s.Items.Single(i => i.LoanSlot == "loan_1");
        spear.Durability = 0;
        E.DebugActor(s, first.ActorId, "kill");
        var n = s.Items.Count(i => i.ItemId == "wooden_spear");
        E.DebugAddEscort(s);
        Assert.That(s.Items.Count(i => i.ItemId == "wooden_spear"), Is.EqualTo(n), "不另发");
        Assert.That(s.Log.Any(l => l.Text.Contains("该借用位（loan_1）的木矛已损毁或找不到，请在营区报失补发")), Is.True, "M191：开发入口也写日志");
        Assert.That(E.LoanSlotIds(s), Is.EqualTo(new[] { "loan_1", "loan_2" }));
        Assert.That(E.ReportLostLoanBlocked(s, "loan_1"), Is.Null, "损毁的矛在遗物里：可以报失");
    }

    private void Advance(GameState s, int segments)
    {
        for (var k = 0; k < segments; k++) E.Advance(s);
    }
}
