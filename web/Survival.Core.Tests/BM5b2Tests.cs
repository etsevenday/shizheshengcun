using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 BM5b2「腐败物与自动配批」（R223、R224、R235；答复 Q606、Q610、Q640—Q643）。规则出处：S06 文首「2026-11-14 Q640—Q643」与
/// 「2026-11-14 BM4b Q606／Q610」两节及验收 Q606-A01—A03、Q610-A01—A07、Q640-A01／A02、Q641-A01／A02、Q643-A02；S14 文首 Q642／Q643 来源行；
/// 正式句见《重墨 V2 开工裁定与界面逐字稿》Q610／Q640—Q643 表。网页壳接线以源码检查。
/// </summary>
public class BM5b2Tests : TestBase
{
    private const string Rot = GameEngine.SpoiledMatterId;

    private static string Web(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "Survival.Web" }.Concat(parts).ToArray()));

    /// <summary>空闲等待到指定分钟（按 5 分钟段推进）。</summary>
    private void WaitUntil(GameState s, int minute)
    {
        while (s.GameMinute < minute)
        {
            if (s.Main == null) Start(s, ActionRequest.Wait(30));
            E.Advance(s);
        }
        if (s.Main?.Type == ActionType.Wait) E.Cancel(s);
    }

    private List<ItemInstance> Make(GameState s, string itemId, int count, string owner, int? expires = null)
    {
        var made = E.CreateItems(s, itemId, count, owner);
        if (expires.HasValue) foreach (var i in made) i.ExpiresAt = expires;
        return made;
    }

    private int Rotten(GameState s, string from, string? owner = null) =>
        s.Items.Where(i => i.ItemId == Rot && i.SpoiledFrom == from && (owner == null || i.Owner == owner)).Sum(i => i.Count);

    private List<string> LogSince(GameState s, int n) => s.Log.Skip(n).Select(l => l.Text).ToList();

    /// <summary>随身清空后放 n 张不腐败的根卡（石头、木材、纤维、绳索、布料各一张）。</summary>
    private void CarriedCards(GameState s, int n)
    {
        s.Items.RemoveAll(i => i.IsCarried);
        foreach (var id in new[] { "stone", "wood", "fiber", "rope", "cloth" }.Take(n)) E.CreateItems(s, id, 1, ItemInstance.Carried);
        Assert.That(E.RootCardCount(s), Is.EqualTo(n));
    }

    // ───────────── Q610 ─────────────

    /// <summary>Q610-A01：生肉2份仅1份到期——原生肉剩1、独立腐败物1；草药、药剂、动物尸体同理；无期限物不转；无副本、无副产物。</summary>
    [Test]
    public void Q610_A01_OnlyExpiredPortionConverts_AllShelfLifeKinds()
    {
        var s = New();
        var due = s.GameMinute + 10;
        Make(s, "raw_meat", 1, "camp", due);
        Make(s, "raw_meat", 1, "camp");
        Make(s, "herb", 2, "camp", due);
        Make(s, "oral_rehydration", 1, "camp", due);
        Make(s, "carcass_feral_dog", 1, "camp", due);
        var stone0 = Count(s, "stone", "camp");
        Make(s, "stone", 3, "camp");
        var count0 = s.Items.Count;
        WaitUntil(s, due);
        Assert.That(Count(s, "raw_meat", "camp"), Is.EqualTo(1), "未到期的生肉留原堆");
        Assert.That(Rotten(s, "raw_meat", "camp"), Is.EqualTo(1));
        Assert.That(Rotten(s, "herb", "camp"), Is.EqualTo(2), "草药同理");
        Assert.That(Rotten(s, "oral_rehydration", "camp"), Is.EqualTo(1), "药剂同理");
        Assert.That(Rotten(s, "carcass_feral_dog", "camp"), Is.EqualTo(1), "动物尸体同理");
        Assert.That(Count(s, "herb") + Count(s, "oral_rehydration") + Count(s, "carcass_feral_dog"), Is.EqualTo(0), "不留原物副本");
        Assert.That(Count(s, "stone", "camp"), Is.EqualTo(stone0 + 3), "无期限物不转");
        Assert.That(Count(s, "raw_meat") + Count(s, "hide") + Count(s, "bone"), Is.EqualTo(1), "不生成副产物");
        var rot = s.Items.Where(i => i.ItemId == Rot).ToList();
        Assert.That(rot.All(i => i.ExpiresAt == null && E.IsSpoiled(s, i)), Is.True, "腐败物无再次腐败期限");
        Assert.That(E.Item(Rot).Name, Is.EqualTo("腐败物"));
        Assert.That(E.Item(Rot).Desc, Is.EqualTo("已腐败的物品，不能食用、治疗或加工。"));
        Assert.That(s.Items.Count, Is.LessThanOrEqualTo(count0), "原位转换不复制实例");
    }

    /// <summary>Q610-A02：6000克野猪尸体1份与100克草药2份转换——总质量6200克、腐败物3份；不同每份克重不误合批，卡位与转移按真实克重。</summary>
    [Test]
    public void Q610_A02_ExactGramsKept_DifferentUnitWeightsNotMerged()
    {
        var s = New();
        var due = s.GameMinute + 5;
        Make(s, "carcass_boar", 1, "camp", due);
        Make(s, "herb", 2, "camp", due);
        var grams0 = s.Items.Where(i => i.Owner == "camp" && (i.ItemId == "carcass_boar" || i.ItemId == "herb")).Sum(i => E.ItemGrams(s, i));
        Assert.That(grams0, Is.EqualTo(6200));
        WaitUntil(s, due);
        var rot = s.Items.Where(i => i.ItemId == Rot && i.Owner == "camp").ToList();
        Assert.That(rot.Sum(i => i.Count), Is.EqualTo(3));
        Assert.That(rot.Sum(i => E.ItemGrams(s, i)), Is.EqualTo(6200), "质量守恒");
        Assert.That(rot.Select(GameEngine.BatchKeyOf).Distinct().Count(), Is.EqualTo(2), "不同每份克重分批");
        var boar = rot.Single(i => i.SpoiledFrom == "carcass_boar");
        Assert.That((E.UnitGrams(boar), E.StackMaxOf(boar), E.IsStackableInst(boar)), Is.EqualTo((6000, 1, false)), "尸体仍不可堆叠");
        var herb = rot.Single(i => i.SpoiledFrom == "herb");
        Assert.That((E.UnitGrams(herb), E.StackMaxOf(herb)), Is.EqualTo((100, 20)), "沿原单堆上限");
        // 拾取：按真实克重与卡位
        CarriedCards(s, 3);
        Assert.That(E.DoInstant(s, InstantRequest.Pickup(boar.InstanceId, 1)).Ok, Is.True);
        Assert.That(E.DoInstant(s, InstantRequest.Pickup(herb.InstanceId, 2)).Ok, Is.True);
        Assert.That(E.RootCardCount(s), Is.EqualTo(5), "腐败尸体与腐败草药各占一张");
        Assert.That(E.CarriedGrams(s), Is.EqualTo(500 + 500 + 100 + 6200));
        // 存读档：元数据保留
        var t = SaveSerializer.Deserialize(SaveSerializer.Serialize(s), Cfg);
        var tb = t.Items.Single(i => i.ItemId == Rot && i.SpoiledFrom == "carcass_boar");
        Assert.That((E.UnitGrams(tb), E.StackMaxOf(tb), tb.IsCarried), Is.EqualTo((6000, 1, true)));
        Assert.That(E.RootCardCount(t), Is.EqualTo(5));
    }

    /// <summary>Q610-A03：根卡已5张时随身根物腐败落持有者当前地面（落地句）；4张且转换后合法则原位；锅内子物不因包满外扔。</summary>
    [Test]
    public void Q610_A03_FullBagDropsToGround_NotFullStays_PotContentsStay()
    {
        var s = New();
        CarriedCards(s, 4);
        var due = s.GameMinute + 5;
        var meat = Make(s, "raw_meat", 1, ItemInstance.Carried, due).Single();
        Assert.That(E.RootCardCount(s), Is.EqualTo(5));
        var n0 = s.Log.Count;
        WaitUntil(s, due);
        Assert.That(Rotten(s, "raw_meat", "camp"), Is.EqualTo(1), "已满5张：即使整批转换也落到当前地面");
        Assert.That(Rotten(s, "raw_meat", ItemInstance.Carried), Is.EqualTo(0));
        Assert.That(LogSince(s, n0), Has.Some.EqualTo("你的生肉×1已腐败，变为腐败物并放在临时营地地面。"));
        Assert.That(LogSince(s, n0), Has.None.Contains("变为腐败物×"), "同次转换不另写原位句");

        // 4 张：原位
        var t = New();
        CarriedCards(t, 3);
        var due2 = t.GameMinute + 5;
        Make(t, "raw_meat", 1, ItemInstance.Carried, due2);
        var m0 = t.Log.Count;
        WaitUntil(t, due2);
        Assert.That(Rotten(t, "raw_meat", ItemInstance.Carried), Is.EqualTo(1), "未满且转换后合法：原位");
        Assert.That(LogSince(t, m0), Has.Some.EqualTo("生肉×1已腐败，变为腐败物×1。"));

        // 包满时随身锅里的食材：仍在锅里转换
        var u = New();
        CarriedCards(u, 4);
        var pot = E.CreateItems(u, "pot", 1, ItemInstance.Carried).Single();
        Assert.That(E.RootCardCount(u), Is.EqualTo(5));
        var root = Make(u, "raw_root", 1, "camp").Single();
        Assert.That(E.DoInstant(u, InstantRequest.PutFoodInPot(root.InstanceId, pot.InstanceId, 1)).Ok, Is.True);
        var inPot = E.PotFood(u, pot.InstanceId).Single();
        inPot.ExpiresAt = u.GameMinute + 5;
        WaitUntil(u, inPot.ExpiresAt.Value);
        var rot = E.PotFood(u, pot.InstanceId).Single();
        Assert.That((rot.ItemId, rot.SpoiledFrom, pot.Owner), Is.EqualTo((Rot, "raw_root", ItemInstance.Carried)), "锅内子物不外扔，锅仍在随身");
    }

    /// <summary>Q610-A03：未满（快照4张）时原位转换并与同键旧腐败物合堆——按每份克重与原上限合堆，卡位以转换后真实合堆核对，不落地。</summary>
    [Test]
    public void Q610_A03_NotFull_MergesIntoSameKeyRot_StaysCarried()
    {
        var s = New();
        CarriedCards(s, 2);
        var due = s.GameMinute + 5;
        Make(s, "berry", 1, ItemInstance.Carried, due);
        WaitUntil(s, due);
        Assert.That(Rotten(s, "berry", ItemInstance.Carried), Is.EqualTo(1));
        Make(s, "berry", 2, ItemInstance.Carried, s.GameMinute + 5);
        Make(s, "herb", 1, ItemInstance.Carried);
        Assert.That(E.RootCardCount(s), Is.EqualTo(5), "石头、木材、腐败野果、野果、草药");
        s.Items.RemoveAll(i => i.IsCarried && i.ItemId == "herb");
        Assert.That(E.RootCardCount(s), Is.EqualTo(4), "快照4张：石头、木材、腐败野果、野果");
        WaitUntil(s, s.GameMinute + 5);
        Assert.That(Rotten(s, "berry", ItemInstance.Carried), Is.EqualTo(3), "同每份克重与原上限合堆，原位");
        Assert.That(s.Items.Count(i => i.IsCarried && i.ItemId == Rot), Is.EqualTo(1), "合成一堆");
        Assert.That(E.RootCardCount(s), Is.EqualTo(3));
        Assert.That(Rotten(s, "berry", "camp"), Is.EqualTo(0));
    }

    /// <summary>Q610-A04：首段实际受热且段内到原截止仍取得保护／熟成；无热等水不获保护照常转换；成品之后正常到期转换；肉干暂停计时不被破坏。</summary>
    [Test]
    public void Q610_A04_HeatProtectionKept_UnheatedConverts_ProductLaterConverts()
    {
        var s = New();
        var pot = First(s, "pot");
        E.DebugGive(s, "clean_water", 2, "camp");
        Assert.That(E.DoInstant(s, InstantRequest.FillPot(First(s, "clean_water", "camp").InstanceId, pot.InstanceId, 2)).Ok, Is.True);
        Assert.That(E.DoInstant(s, InstantRequest.PutInFire(pot.InstanceId)).Ok, Is.True);
        var fac = s.Locations["camp"].Facility;
        fac.FireLit = true;
        fac.FuelMinutes = 180;
        var root = Make(s, "raw_root", 1, "camp").Single();
        Assert.That(E.DoInstant(s, InstantRequest.PutFoodInPot(root.InstanceId, pot.InstanceId, 1)).Ok, Is.True);
        var r = E.PotFood(s, pot.InstanceId).Single();
        r.ExpiresAt = s.GameMinute + 5;
        WaitUntil(s, s.GameMinute + 5);
        Assert.That(r.ItemId, Is.EqualTo("raw_root"), "首段实际受热保护，不转换");
        Assert.That(E.ProcessOf(s, r)!.HeatProtected, Is.True);

        // 无热（火没点）：到期照常转换
        var t = New();
        var pot2 = First(t, "pot");
        var root2 = Make(t, "raw_root", 1, "camp").Single();
        Assert.That(E.DoInstant(t, InstantRequest.PutFoodInPot(root2.InstanceId, pot2.InstanceId, 1)).Ok, Is.True);
        var r2 = E.PotFood(t, pot2.InstanceId).Single();
        r2.ExpiresAt = t.GameMinute + 5;
        WaitUntil(t, t.GameMinute + 5);
        Assert.That((r2.ItemId, r2.Owner), Is.EqualTo((Rot, ItemInstance.ContainerOwner(pot2.InstanceId))), "无热不获保护，在原锅里转换");
        Assert.That(E.ContentStatus(t, r2), Is.EqualTo("已腐败，不再加工"));

        // 已完成成品之后照常到期转换
        var cooked = Make(t, "cooked_meat", 1, "camp", t.GameMinute + 5).Single();
        WaitUntil(t, t.GameMinute + 5);
        Assert.That(Rotten(t, "cooked_meat", "camp"), Is.EqualTo(1));
    }

    /// <summary>Q610-A05：预留输入到期——只原输入转腐败物，原腐败中止生效，辅料只退一次，无成果／经验，不自动补新料；结果句接停工分句。</summary>
    [Test]
    public void Q610_A05_ReservedInputSpoils_AbortWithClause_AuxReturnedOnce()
    {
        var s = New();
        s.Items.RemoveAll(i => i.ItemId == "cloth" || i.ItemId == "herb" || i.ItemId == "bandage");
        E.DebugGive(s, "cloth", 1);
        E.DebugGive(s, "herb", 1);
        E.DebugGive(s, "herb", 1, "camp");   // 另有一份新鲜草药：不自动补料
        var herb = First(s, "herb", ItemInstance.Carried);
        herb.ExpiresAt = s.GameMinute + 5;
        Start(s, ActionRequest.Craft("bandage"));
        var xp = s.Skills.ToDictionary(k => k.Key, k => k.Value.Xp);
        var n0 = s.Log.Count;
        Run(s);
        Assert.That(s.Main, Is.Null);
        var log = LogSince(s, n0);
        Assert.That(log, Has.Some.EqualTo("制作草药绷带中止：原料已腐败，预留物返还当前地面，未生成产物。腐败部分已变为腐败物。"));
        Assert.That(log, Has.None.Contains("变为腐败物×"), "在办工作的输入不另写普通腐败句");
        Assert.That(Rotten(s, "herb", "camp"), Is.EqualTo(1), "只原输入转腐败物，返还当前地面");
        Assert.That(Count(s, "cloth"), Is.EqualTo(1), "辅料只退一次");
        Assert.That(Count(s, "herb", "camp"), Is.EqualTo(1), "未自动补用新草药");
        Assert.That(Count(s, "bandage"), Is.EqualTo(0), "无成果");
        Assert.That(s.Skills.ToDictionary(k => k.Key, k => k.Value.Xp), Is.EqualTo(xp), "无经验");
    }

    /// <summary>Q610-A05：晾制果干原料到期——工位停止，原料原位转腐败物，结果句接停工分句；清理取出得腐败物。</summary>
    [Test]
    public void Q610_A05_DryingSpoil_ConvertsInSlot_WithClause()
    {
        var s = New();
        s.GameMinute = At(2, 8, 0);
        s.Skills["cooking"].Level = 5;
        s.Items.RemoveAll(i => i.Owner == s.Ground || i.Owner == ItemInstance.Carried);
        E.CreateItems(s, "wood", 4, s.Ground);
        E.CreateItems(s, "rope", 2, s.Ground);
        Start(s, ActionRequest.Craft("build_drying_rack"));
        Run(s);
        Assert.That(E.DryingRackHere(s), Is.True);
        E.DebugGive(s, "berry", 2);
        var t1 = s.GameMinute;
        foreach (var b in s.Items.Where(i => i.ItemId == "berry")) b.ExpiresAt = t1 + 60;
        Assert.That(E.StartDrying(s, "dry_berries").Ok, Is.True);
        var n0 = s.Log.Count;
        WaitUntil(s, t1 + 70);
        var log = LogSince(s, n0);
        Assert.That(log, Has.Some.EndsWith("原料留在这一位，回到当地清理。腐败部分已变为腐败物。").And.Some.Contains("野果已腐败"));
        Assert.That(log, Has.None.Contains("变为腐败物×"), "晾制中原料由停工结果说明，不另写普通句");
        var slot = s.Locations["camp"].Facility.DryingSlots.Single();
        Assert.That(slot.Status, Is.EqualTo(DryingSlot.Spoiled));
        Assert.That(E.TakeDrying(s, slot.Slot, false).Ok, Is.True);
        Assert.That(Rotten(s, "berry", "camp"), Is.EqualTo(2));
    }

    /// <summary>Q610-A06／Q641-A01：保护中已过期食品合法取出同事务转换；预估写风险原句；成功主句后一次原位句，旧尾句消失。</summary>
    [Test]
    public void Q641_A01_TakeOutProtectedPastExpiry_RiskThenOneSpoilLine()
    {
        var s = New();
        var pot = First(s, "pot");
        E.DebugGive(s, "clean_water", 2, "camp");
        Assert.That(E.DoInstant(s, InstantRequest.FillPot(First(s, "clean_water", "camp").InstanceId, pot.InstanceId, 2)).Ok, Is.True);
        Assert.That(E.DoInstant(s, InstantRequest.PutInFire(pot.InstanceId)).Ok, Is.True);
        var fac = s.Locations["camp"].Facility;
        fac.FireLit = true;
        fac.FuelMinutes = 180;
        foreach (var k in new[] { 0, 1 })
        {
            var root = Make(s, "raw_root", 1, "camp").Single();
            Assert.That(E.DoInstant(s, InstantRequest.PutFoodInPot(root.InstanceId, pot.InstanceId, 1)).Ok, Is.True);
        }
        foreach (var f in E.PotFood(s, pot.InstanceId)) f.ExpiresAt = s.GameMinute + 5;
        WaitUntil(s, s.GameMinute + 5);
        Assert.That(E.PotFood(s, pot.InstanceId).All(f => f.ItemId == "raw_root" && E.PastExpiryButProtected(s, f)), Is.True);
        var c = E.TakeOutConsequenceOf(s, pot);
        Assert.That(c.Text, Does.Contain("取出后解除烹饪保护，已过期的食材将变为腐败物。"));
        var n0 = s.Log.Count;
        Assert.That(E.DoInstant(s, InstantRequest.TakeOut(pot.InstanceId, "camp")).Ok, Is.True, "取下整锅");
        var line = LogSince(s, n0).Single(l => l.StartsWith("把"));
        Assert.That(line, Does.EndWith("。生块根×2已腐败，变为腐败物×2。"), "按原名与实际转换量聚合，一次");
        Assert.That(line, Does.Not.Contain("解除烹饪保护、已过原保质期而腐败"));
        Assert.That(LogSince(s, n0).Count(l => l.Contains("已腐败，变为腐败物")), Is.EqualTo(1), "不另重复发同一句");
        var foods = E.PotFood(s, pot.InstanceId);
        Assert.That(foods.Select(f => f.ItemId), Is.All.EqualTo(Rot), "整锅取出内容仍在锅里转换");
        Assert.That(E.PotLiquid(s, pot.InstanceId), Is.EqualTo("clean_water"), "锅水保留");
        Assert.That(pot.Owner, Is.EqualTo("camp"));

        var ca = Web("Components", "CardActions.razor");
        Assert.That(ca, Does.Contain("@c.Text@(c.Spoils.Count > 0 ? takeLabel : \"\")"), "风险原句后紧接取出按钮已有去向文案");
    }

    /// <summary>Q641-A02：取出失败保护与归属不变、无成功日志、不转换。</summary>
    [Test]
    public void Q641_A02_FailedTakeOut_NoChange()
    {
        var s = New();
        var pot = First(s, "pot");
        E.DebugGive(s, "clean_water", 2, "camp");
        Assert.That(E.DoInstant(s, InstantRequest.FillPot(First(s, "clean_water", "camp").InstanceId, pot.InstanceId, 2)).Ok, Is.True);
        Assert.That(E.DoInstant(s, InstantRequest.PutInFire(pot.InstanceId)).Ok, Is.True);
        var fac = s.Locations["camp"].Facility;
        fac.FireLit = true;
        fac.FuelMinutes = 180;
        var root = Make(s, "raw_root", 1, "camp").Single();
        Assert.That(E.DoInstant(s, InstantRequest.PutFoodInPot(root.InstanceId, pot.InstanceId, 1)).Ok, Is.True);
        var r = E.PotFood(s, pot.InstanceId).Single();
        r.ExpiresAt = s.GameMinute + 5;
        WaitUntil(s, s.GameMinute + 5);
        CarriedCards(s, 5);
        var n0 = s.Log.Count;
        var fail = E.DoInstant(s, InstantRequest.TakeOut(r.InstanceId, ItemInstance.Carried));
        Assert.That(fail.Ok, Is.False, "随身卡位已满整次拒绝");
        Assert.That((r.ItemId, r.Owner, E.ProcessOf(s, r)!.HeatProtected), Is.EqualTo(("raw_root", ItemInstance.ContainerOwner(pot.InstanceId), true)));
        Assert.That(s.Log.Count, Is.EqualTo(n0), "无成功或转换日志");
    }

    // ───────────── Q640 ─────────────

    /// <summary>Q640-A01：身边（主角随身、同区在场同伴随身、当前区地面／锅）写日志；别的地点静默转换，到场不补刷。</summary>
    [Test]
    public void Q640_A01_NearLogsOnly_FarSilent_NoBackfill()
    {
        var s = New();
        var due = s.GameMinute + 5;
        Make(s, "berry", 2, "camp", due);
        Make(s, "herb", 1, ItemInstance.Carried, due);
        Make(s, "berry", 3, "stream", due);   // 别的地点
        var n0 = s.Log.Count;
        WaitUntil(s, due);
        var log = LogSince(s, n0);
        Assert.That(log, Has.Some.EqualTo("野果×2已腐败，变为腐败物×2。"));
        Assert.That(log, Has.Some.EqualTo("草药×1已腐败，变为腐败物×1。"));
        Assert.That(log, Has.None.Contains("野果×3"), "远处静默");
        Assert.That(Rotten(s, "berry", "stream"), Is.EqualTo(3), "远处真实转换，留原位");
        var n1 = s.Log.Count;
        E.DebugTeleport(s, "stream");
        WaitUntil(s, s.GameMinute + 10);
        Assert.That(LogSince(s, n1), Has.None.Contains("已腐败，变为腐败物"), "到场不补刷历史腐败日志");
        Assert.That(Rotten(s, "berry", "stream"), Is.EqualTo(3), "不再次转换");
    }

    /// <summary>Q640-A01：同地同区在场同伴随身到期写日志（持有者为同伴本人，原位）。</summary>
    [Test]
    public void Q640_A01_PresentCompanionCarried_Logs()
    {
        var s = New();
        s.Progress.Flags.Add("hub_registered");
        s.Progress.Flags.Add("church_route_record");
        E.DebugRecruit(s, "npc_lin_zhao");
        var owner = ItemInstance.CarriedOwner("npc_lin_zhao");
        var due = s.GameMinute + 5;
        Make(s, "raw_meat", 1, owner, due);
        var n0 = s.Log.Count;
        WaitUntil(s, due);
        Assert.That(Rotten(s, "raw_meat", owner), Is.EqualTo(1));
        Assert.That(LogSince(s, n0), Has.Some.EqualTo("生肉×1已腐败，变为腐败物×1。"));
    }

    /// <summary>Q640-A02／Q610-A07：旧档迁移一次转换、一次总提示；保护中的不转；连续读档数量质量一致、不重复；腐败物不能食用。</summary>
    [Test]
    public void Q610_A07_Q640_A02_OldSaveMigratesOnce()
    {
        var s = New();
        var pot = First(s, "pot");
        E.DebugGive(s, "clean_water", 2, "camp");
        Assert.That(E.DoInstant(s, InstantRequest.FillPot(First(s, "clean_water", "camp").InstanceId, pot.InstanceId, 2)).Ok, Is.True);
        Assert.That(E.DoInstant(s, InstantRequest.PutInFire(pot.InstanceId)).Ok, Is.True);
        var fac = s.Locations["camp"].Facility;
        fac.FireLit = true;
        fac.FuelMinutes = 180;
        var root = Make(s, "raw_root", 1, "camp").Single();
        Assert.That(E.DoInstant(s, InstantRequest.PutFoodInPot(root.InstanceId, pot.InstanceId, 1)).Ok, Is.True);
        var r = E.PotFood(s, pot.InstanceId).Single();
        r.ExpiresAt = s.GameMinute + 5;
        WaitUntil(s, s.GameMinute + 5);
        Assert.That(E.ProcessOf(s, r)!.HeatProtected, Is.True);
        // 旧档：过期却仍以原名留着的物品（50 版没有腐败物）
        var old = Make(s, "berry", 3, "camp").Single();
        var o = Newtonsoft.Json.Linq.JObject.Parse(SaveSerializer.Serialize(s));
        o["SchemaVersion"] = "50";
        foreach (var it in o["Items"]!)
            if ((int)it["InstanceId"]! == old.InstanceId) it["ExpiresAt"] = s.GameMinute - 1;
        var json = o.ToString();
        Assert.That(json, Does.Contain("\"SchemaVersion\": \"50\""));
        var t = SaveSerializer.Deserialize(json, Cfg);
        Assert.That(t.SchemaVersion, Is.EqualTo("51"));
        Assert.That(Rotten(t, "berry", "camp"), Is.EqualTo(3), "过期物一次迁移为腐败物");
        Assert.That(t.Items.Count(i => i.ItemId == "berry"), Is.EqualTo(0), "原名下不留副本");
        Assert.That(t.Items.Single(i => i.InstanceId == r.InstanceId).ItemId, Is.EqualTo("raw_root"), "可信烹饪保护保留");
        Assert.That(t.Log.Count(l => l.Text == "已将存档中过期的物品转换为腐败物。"), Is.EqualTo(1));
        Assert.That(t.Log.Count(l => l.Text.Contains("变为腐败物×")), Is.EqualTo(0), "不逐物补历史");
        var grams = t.Items.Where(i => i.ItemId == Rot).Sum(i => E.ItemGrams(t, i));
        var u = SaveSerializer.Deserialize(SaveSerializer.Serialize(t), Cfg);
        Assert.That(u.Items.Where(i => i.ItemId == Rot).Sum(i => i.Count), Is.EqualTo(3), "连续读档不重复生成");
        Assert.That(u.Items.Where(i => i.ItemId == Rot).Sum(i => E.ItemGrams(u, i)), Is.EqualTo(grams));
        Assert.That(u.Log.Count(l => l.Text == "已将存档中过期的物品转换为腐败物。"), Is.EqualTo(1), "提示只一次");
        var rot = u.Items.First(i => i.ItemId == Rot);
        Assert.That(E.CanStart(u, ActionRequest.UseInstance(rot.InstanceId)), Is.EqualTo("腐败物不能用于此操作。"));
    }

    /// <summary>Q610：腐败物不能食用、投料、处理尸体；菜单不呈现原用途（无 use、不是尸体）；开发入口与调试箱不造腐败物。</summary>
    [Test]
    public void Q610_SpoiledMatterRejectsOriginalUses()
    {
        var s = New();
        var due = s.GameMinute + 5;
        Make(s, "carcass_feral_dog", 1, "camp", due);
        Make(s, "raw_root", 1, "camp", due);
        E.CreateItems(s, "hatchet", 1, ItemInstance.Carried);
        WaitUntil(s, due);
        var dog = s.Items.Single(i => i.ItemId == Rot && i.SpoiledFrom == "carcass_feral_dog");
        var rootRot = s.Items.Single(i => i.ItemId == Rot && i.SpoiledFrom == "raw_root");
        Assert.That(E.CanStart(s, ActionRequest.Butcher(dog.InstanceId)), Is.EqualTo("腐败物不能用于此操作。"));
        Assert.That(E.CarcassOf(dog.ItemId), Is.Null, "菜单不再出现处理尸体");
        var pot = First(s, "pot");
        Assert.That(E.MaxPutFood(s, rootRot, pot, out var why), Is.EqualTo(0));
        Assert.That(why, Is.EqualTo("腐败物不能用于此操作。"));
        Assert.That(E.Item(Rot).Use, Is.Null);
        var t = New();
        E.DebugFillGround(t);
        Assert.That(t.Items.Any(i => i.ItemId == Rot), Is.False);
        Assert.That(Web("Components", "MenuOverlay.razor"), Does.Contain("G.E.DebugSpoilageKit(G.S)"));
    }
}
