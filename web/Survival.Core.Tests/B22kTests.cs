using Newtonsoft.Json.Linq;
using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 批次 B22k（需求 R213、R214）：策划答复 Q214（选 A：晾架自动选料与预览确认）、Q215（肉干在制暂停原料保质）。
/// 规则出处：S11/S06/S08/S09/A07「Q214—Q215 晾架选料与肉干保质」、assets/20261103/drying-material-adjudications.json（selection、preservation、acceptance）。
/// 验收 DRY-Q214-01/02、DRY-Q215-01/02/03。
/// </summary>
public class B22kTests : TestBase
{
    private void Ok(ActionResult r) => Assert.That(r.Ok, Is.True, r.Reason);

    private static ActionRequest Craft(string id) => ActionRequest.Craft(id, null, null, 1);

    /// <summary>营地、第 2 日 08:00、烹饪 5 级、清掉随身与地面杂物，建好晾制架。</summary>
    private GameState Rack()
    {
        var s = New();
        s.GameMinute = At(2, 8, 0);
        s.Skills["cooking"].Level = 5;
        s.Items.RemoveAll(i => i.Owner == s.Ground || i.Owner == ItemInstance.Carried);
        E.CreateItems(s, "wood", 4, s.Ground);
        E.CreateItems(s, "rope", 2, s.Ground);
        Start(s, Craft("build_drying_rack"));
        Run(s);
        Assert.That(E.DryingRackHere(s), Is.True);
        s.Items.RemoveAll(i => i.Owner == s.Ground || i.Owner == ItemInstance.Carried);
        return s;
    }

    private ItemInstance Make(GameState s, string id, string owner, int? expiresAt, int n = 1)
    {
        var it = E.CreateItems(s, id, n, owner).Single();
        it.ExpiresAt = expiresAt;
        return it;
    }

    private int CookXp(GameState s) => E.DailyCount(s, s.Player, GameEngine.DailyXp, "cooking");

    private List<DryingSlot> Slots(GameState s) => s.Locations["camp"].Facility.DryingSlots;

    /// <summary>
    /// DRY-Q214-01：只取主角随身与当前地面，按确定性顺序（BM5b2 Q606 起：截止先到先用、同截止随身优先、实例号，不再把能撑到完成的排前）；预览逐实例写来源/数量/原截止，
    /// 开预览不取料；不足、预留、异地、队友包、屋柜拒绝或不取；临期果干红字但可确认，肉干写保护说明、不报制作中腐败。
    /// </summary>
    [Test]
    public void DRY_Q214_01_AutoPick_CarriedAndGroundOnly_Order_Preview_Warn()
    {
        var s = Rack();
        var t0 = s.GameMinute;
        var finish = t0 + 120;
        var early = Make(s, "berry", ItemInstance.Carried, t0 + 60);       // 会在完成前腐败：Q606 起最早有效到期先取（临期警告照旧）
        var g500 = Make(s, "berry", s.Ground, t0 + 500);
        var c500 = Make(s, "berry", ItemInstance.Carried, t0 + 500);       // 同截止随身优先
        var g300 = Make(s, "berry", s.Ground, t0 + 300);
        Make(s, "berry", ItemInstance.CarriedOwner("c1"), t0 + 900, 4);   // 队友包
        Make(s, "berry", ItemInstance.SafehouseStorageOwner("sh_x"), t0 + 900, 4);   // 屋柜
        Make(s, "berry", "w13_mill", t0 + 900, 4);                        // 异地
        var before = s.Items.Select(i => (i.InstanceId, i.Owner, i.Count)).ToList();

        var pv = E.DryingPreview(s, "dry_berries");
        Assert.That(pv.Ok, Is.True, pv.Reason);
        Assert.That(pv.Sources, Has.Count.EqualTo(2));
        Assert.That(pv.Sources[0], Does.StartWith($"随身野果×1（保质到{Clock.DayTime(t0 + 60)}）"), "BM5b2（Q606）：取代 Q214 能撑到完成先取，最早有效到期先用");
        Assert.That(pv.Sources[1], Does.StartWith($"地面野果×1（保质到{Clock.DayTime(t0 + 300)}）"));
        Assert.That(pv.Warnings.Single(), Does.Contain($"野果（保质到{Clock.DayTime(t0 + 60)}").And.Contain("会在完成前腐败，届时停止且没有成品"), "临期果干红字但可确认");
        Assert.That((pv.Slot, pv.Minutes, pv.FinishMinute), Is.EqualTo((0, 120, finish)), "预览写位号与时间");
        Assert.That(pv.XpText, Does.Contain("经验+12"));
        Assert.That(s.Items.Select(i => (i.InstanceId, i.Owner, i.Count)).ToList(), Is.EqualTo(before), "开预览不取料、不预留");

        Ok(E.StartDrying(s, "dry_berries", null, null, pv.SourceKey));
        Assert.That(E.DryingContents(s, s.Ground, 0).Select(i => i.InstanceId), Is.EquivalentTo(new[] { early.InstanceId, g300.InstanceId }));
        Assert.That(c500.Owner, Is.EqualTo(ItemInstance.Carried));
        Assert.That(g500.Owner, Is.EqualTo(s.Ground));
        Assert.That(E.DryingSlotViews(s)[0].Line, Does.Contain("会在完成前腐败"));
        Assert.That(s.Items.Where(i => i.Owner == ItemInstance.CarriedOwner("c1") || i.Owner.StartsWith(ItemInstance.SafehouseStoragePrefix) || i.Owner == "w13_mill")
            .Sum(i => i.Count), Is.EqualTo(12), "队友包、屋柜、异地都不取");

        // 剩下同截止的两份：同截止随身优先，都能撑到完成
        var pv2 = E.DryingPreview(s, "dry_berries");
        Assert.That(pv2.Ok, Is.True);
        Assert.That(pv2.Sources[0], Does.StartWith($"随身野果×1（保质到{Clock.DayTime(t0 + 500)}）"), "同截止随身优先");
        Assert.That(pv2.Sources[1], Does.StartWith($"地面野果×1（保质到{Clock.DayTime(t0 + 500)}）"));
        Assert.That(pv2.Warnings, Is.Empty, "选中的都能撑到完成");
        Assert.That(pv2.PausesShelf, Is.False);
        Ok(E.StartDrying(s, "dry_berries", null, null, pv2.SourceKey));
        Assert.That(E.DryingSlotViews(s)[1].Line, Does.Not.Contain("会在完成前腐败"));

        // 不足不半投、预留不取
        var one = Make(s, "berry", ItemInstance.Carried, t0 + 500);
        Assert.That(E.StartDrying(s, "dry_berries").Reason, Does.StartWith("缺少野果（随身与当前地面可用1/2"));
        var held = Make(s, "berry", s.Ground, t0 + 500);
        held.ReservedBy = 999;
        Assert.That(E.StartDrying(s, "dry_berries").Reason, Does.Contain("已预留或占用1份"));
        Assert.That(one.Owner, Is.EqualTo(ItemInstance.Carried), "拒绝不扣料");
        Assert.That(Slots(s), Has.Count.EqualTo(2));

        // 肉干：原截止早于完成也不报腐败红字，改写暂停保质说明
        var meat = Make(s, "raw_meat", ItemInstance.Carried, t0 + 30);
        Make(s, "salt", s.Ground, null);
        var pm = E.DryingPreview(s, "dry_meat");
        Assert.That(pm.Ok, Is.True, pm.Reason);
        Assert.That(pm.Warnings, Is.Empty, "肉干不显示制作中会腐败");
        Assert.That(pm.PausesShelf, Is.True);
        Assert.That(pm.Note, Does.Contain(GameEngine.DryingShelfPauseText).And.Not.Contain("到期即腐败"));
        Assert.That(pm.Sources[0], Is.EqualTo($"随身生肉×1（保质到{Clock.DayTime(t0 + 30)}）"), "预览仍写原截止");
        Ok(E.StartDrying(s, "dry_meat", null, null, pm.SourceKey));
        Assert.That(s.Log.Last().Text, Does.Contain(GameEngine.DryingShelfPauseText).And.Not.Contain("会在完成前腐败"));
        var v = E.DryingSlotViews(s)[2];
        Assert.That(v.ShelfPaused, Is.True);
        Assert.That(v.Line, Does.Contain("原料保质暂停计时，取消后恢复剩余时长").And.Not.Contain("腐败"));
        Assert.That(v.Contents.First(c => c.StartsWith("生肉")), Does.Contain("保质暂停，剩余30分钟"));
        Assert.That(v.CancelHint, Does.Contain("恢复投料时的剩余保质"));
    }

    /// <summary>
    /// DRY-Q214-02：预览后原料失效或来源变了，确认按实时状态重验：拒绝、不取料、不换料，按更新后的预览再确认才投；重复请求只投一批；队友包/屋柜不被取走。
    /// </summary>
    [Test]
    public void DRY_Q214_02_ConfirmRevalidates_NoSilentSwap_Idempotent()
    {
        var s = Rack();
        var t0 = s.GameMinute;
        var a = Make(s, "raw_meat", ItemInstance.Carried, t0 + 600);
        var b = Make(s, "raw_meat", s.Ground, t0 + 900);
        var salt = Make(s, "salt", s.Ground, null);
        Make(s, "raw_meat", ItemInstance.CarriedOwner("c1"), t0 + 2000);
        Make(s, "salt", ItemInstance.SafehouseStorageOwner("sh_x"), null);

        // 1) 预览选中的生肉在确认前失效（被预留）→ 拒绝，不悄悄换成地面那份
        var pv = E.DryingPreview(s, "dry_meat");
        Assert.That(pv.Sources[0], Does.StartWith($"随身生肉×1（保质到{Clock.DayTime(t0 + 600)}）"));
        a.ReservedBy = 77;
        var r = E.StartDrying(s, "dry_meat", null, "req-a", pv.SourceKey);
        Assert.That(r.Ok, Is.False);
        Assert.That(r.Reason, Is.EqualTo(GameEngine.DryingPreviewChanged));
        Assert.That(Slots(s), Is.Empty, "没投");
        Assert.That((b.Owner, salt.Owner), Is.EqualTo((s.Ground, s.Ground)), "没取料");
        var pv2 = E.DryingPreview(s, "dry_meat");
        Assert.That(pv2.SourceKey, Is.Not.EqualTo(pv.SourceKey), "预览已更新");
        Assert.That(pv2.Sources[0], Does.StartWith($"地面生肉×1（保质到{Clock.DayTime(t0 + 900)}）"));

        // 2) 来源变了（同一实例从随身放到地面）也要再确认
        a.ReservedBy = null;
        var pv3 = E.DryingPreview(s, "dry_meat");
        Assert.That(pv3.Sources[0], Does.StartWith($"随身生肉×1（保质到{Clock.DayTime(t0 + 600)}）"));
        a.Owner = s.Ground;
        Assert.That(E.StartDrying(s, "dry_meat", null, null, pv3.SourceKey).Reason, Is.EqualTo(GameEngine.DryingPreviewChanged));
        var pv4 = E.DryingPreview(s, "dry_meat");
        Assert.That(pv4.Sources[0], Does.StartWith($"地面生肉×1（保质到{Clock.DayTime(t0 + 600)}）"));

        // 3) 按更新后的预览确认才投；同一请求号重复只投一批
        Ok(E.StartDrying(s, "dry_meat", null, "req-b", pv4.SourceKey));
        Assert.That(E.StartDrying(s, "dry_meat", null, "req-b", pv4.SourceKey).Ok, Is.True);
        Assert.That(Slots(s), Has.Count.EqualTo(1), "重复请求只投一批");
        Assert.That(E.DryingContents(s, s.Ground, 0).Select(i => i.InstanceId), Is.EquivalentTo(new[] { a.InstanceId, salt.InstanceId }));
        Assert.That(b.Owner, Is.EqualTo(s.Ground));
        Assert.That(Count(s, "raw_meat", ItemInstance.CarriedOwner("c1")) + Count(s, "salt", ItemInstance.SafehouseStorageOwner("sh_x")), Is.EqualTo(2), "队友包/柜物不被取走");
        // 4) 位号变了（预览的第一个空位被别的批占了）也拒绝，不悄悄投到另一位
        Make(s, "salt", s.Ground, null);
        var pv5 = E.DryingPreview(s, "dry_meat");
        Assert.That((pv5.Ok, pv5.Slot), Is.EqualTo((true, 1)));
        var berry = Make(s, "berry", s.Ground, t0 + 900, 2);
        Ok(E.StartDrying(s, "dry_berries", 1));
        Assert.That(E.StartDrying(s, "dry_meat", null, null, pv5.SourceKey).Reason, Is.EqualTo(GameEngine.DryingPreviewChanged));
        Assert.That(b.Owner, Is.EqualTo(s.Ground));
        Assert.That(berry.InDrying, Is.True);
    }

    /// <summary>
    /// DRY-Q215-01：生肉只剩 30 分钟也能投 180 分钟肉干，越过原截止仍继续，180 分钟完成肉干 1；生肉与盐各 1 核销，成品截止＝完成＋10080 分钟，在位里 168 小时后照常腐败。
    /// </summary>
    [Test]
    public void DRY_Q215_01_Meat30MinLeft_CompletesAt180_Product168h()
    {
        var s = Rack();
        var t0 = s.GameMinute;
        var meat = Make(s, "raw_meat", ItemInstance.Carried, t0 + 30);
        Make(s, "salt", s.Ground, null);
        var x0 = CookXp(s);
        Ok(E.StartDrying(s, "dry_meat"));
        Assert.That(meat.InDrying, Is.True);
        Assert.That((meat.ShelfPausedMinutes, meat.ExpiresAt), Is.EqualTo(((int?)30, (int?)null)), "快照 R＝E−S＝30");
        Assert.That(Slots(s).Single().ShelfPaused, Is.True);
        Assert.That(E.IsSpoiled(s, meat), Is.False);

        AdvanceTo(s, t0 + 60);
        var slot = Slots(s).Single();
        Assert.That(slot.Status, Is.EqualTo(DryingSlot.Drying), "越过原截止仍继续");
        Assert.That(slot.DoneMinutes, Is.EqualTo(60));
        Assert.That(meat.ShelfPausedMinutes, Is.EqualTo(30), "生产中 R 不减");

        AdvanceTo(s, t0 + 180);
        Assert.That(slot.Status, Is.EqualTo(DryingSlot.Done));
        Assert.That(Count(s, "dried_meat"), Is.EqualTo(1));
        Assert.That(Count(s, "raw_meat") + Count(s, "salt"), Is.EqualTo(0), "生肉与盐各 1 核销");
        var dm = First(s, "dried_meat");
        Assert.That(dm.InDrying, Is.True, "成品留在位里");
        Assert.That(dm.ShelfPausedMinutes, Is.Null, "保护在转为成品时结束");
        Assert.That(dm.ExpiresAt, Is.EqualTo(t0 + 180 + 10080), "成品截止＝完成＋10080 分钟");
        Assert.That(CookXp(s) - x0, Is.EqualTo(12), "成功经验沿 Q170");
        s.GameMinute = t0 + 180 + 10079;
        Assert.That(E.IsSpoiled(s, dm), Is.False);
        s.GameMinute = t0 + 180 + 10080;
        Assert.That(E.IsSpoiled(s, dm), Is.True, "成品在晾架位中正常腐败，不是保险箱");
    }

    /// <summary>
    /// DRY-Q215-02：生肉剩 30 分钟，晾 60 分钟后取消，退回的生肉仍剩 30 分钟、盐 1；再投再取消不刷新成完整保质；目的地不合法整次拒绝、不半退、快照不变；取消 0 经验。
    /// </summary>
    [Test]
    public void DRY_Q215_02_Cancel_RestoresRemaining_NoRefresh_BlockedDestKeepsSnapshot()
    {
        var s = Rack();
        var t0 = s.GameMinute;
        var meat = Make(s, "raw_meat", ItemInstance.Carried, t0 + 30);
        var salt = Make(s, "salt", s.Ground, null);
        var x0 = CookXp(s);
        Ok(E.StartDrying(s, "dry_meat"));
        AdvanceTo(s, t0 + 60);

        // 目的地不合法：随身已满五张 → 整次拒绝，仍留位、快照不变
        var knives = new List<ItemInstance>();
        for (var k = 0; k < 5; k++) knives.AddRange(E.CreateItems(s, "stone_knife", 1, ItemInstance.Carried));
        var blocked = E.CancelDrying(s, 0, true);
        Assert.That(blocked.Reason, Does.Contain("随身卡位不够"));
        Assert.That((meat.InDrying, salt.InDrying), Is.EqualTo((true, true)), "不半退");
        Assert.That((meat.ShelfPausedMinutes, meat.ExpiresAt), Is.EqualTo(((int?)30, (int?)null)), "保持原快照");
        Assert.That(Slots(s).Single().Status, Is.EqualTo(DryingSlot.Drying));
        s.Items.RemoveAll(i => knives.Contains(i));

        Ok(E.CancelDrying(s, 0, true));
        Assert.That(s.GameMinute, Is.EqualTo(t0 + 60), "已过时间不退");
        var back = First(s, "raw_meat");
        Assert.That(back.Owner, Is.EqualTo(ItemInstance.Carried));
        Assert.That(back.ExpiresAt, Is.EqualTo(t0 + 60 + 30), "新截止＝退料分钟 C＋R，仍剩 30 分钟");
        Assert.That(back.ShelfPausedMinutes, Is.Null);
        Assert.That(Count(s, "salt", ItemInstance.Carried), Is.EqualTo(1), "盐 1 原样退回");
        Assert.That(Count(s, "salt"), Is.EqualTo(1), "盐不复制");
        Assert.That(Slots(s), Is.Empty, "清本批进度");
        Assert.That(CookXp(s), Is.EqualTo(x0), "取消不给经验");
        Assert.That(s.Log.Last().Text, Does.Contain("恢复投料时的剩余保质"));

        // 再投再取消：R 仍是 30，不刷新成完整保质
        var t1 = s.GameMinute;
        Ok(E.StartDrying(s, "dry_meat"));
        Assert.That(First(s, "raw_meat").ShelfPausedMinutes, Is.EqualTo(30), "重新投料以退回后的真实剩余快照");
        AdvanceTo(s, t1 + 90);
        Ok(E.CancelDrying(s, 0, false));
        Assert.That(First(s, "raw_meat", s.Ground).ExpiresAt, Is.EqualTo(t1 + 90 + 30));
        Assert.That(Count(s, "salt"), Is.EqualTo(1));
        Assert.That(CookXp(s), Is.EqualTo(x0));
        AdvanceTo(s, t1 + 120);
        Assert.That(First(s, GameEngine.SpoiledMatterId, s.Ground).SpoiledFrom, Is.EqualTo("raw_meat"), "退出后照常计时（BM5b2：到期转为腐败物）");
    }

    /// <summary>
    /// DRY-Q215-03：果干原料仍在原截止腐败停止；旧档（38）已腐败肉干批不复活、清理时盐只退一次；旧在制未腐败肉干批从升级时刻 U 的余量保护，
    /// 保留已晾分钟与经验快照；已完成肉干截止不改；反复读档不多加保质或产物。
    /// </summary>
    [Test]
    public void DRY_Q215_03_BerryStillSpoils_Migration38To39_ProtectFromUpgrade_NoRevive()
    {
        // 果干不变
        var f = Rack();
        var tf = f.GameMinute;
        var berry = Make(f, "berry", ItemInstance.Carried, tf + 60, 2);
        Ok(E.StartDrying(f, "dry_berries"));
        Assert.That(berry.ShelfPausedMinutes, Is.Null, "果干不暂停");
        Assert.That(berry.ExpiresAt, Is.EqualTo(tf + 60));
        AdvanceTo(f, tf + 120);
        Assert.That(Slots(f).Single().Status, Is.EqualTo(DryingSlot.Spoiled), "果干原料到原截止腐败停止");
        Assert.That(Count(f, "dried_berries"), Is.EqualTo(0));

        // 旧档：四个位按旧规则（不暂停）布置
        var s = Rack();
        var u = s.GameMinute;
        var place = s.Ground;
        var fac = Slots(s);
        DryingSlot Slot(int k, string status, int done, int? end = null)
        {
            var b = new DryingSlot { Slot = k, BatchId = 900 + k, RecipeId = "dry_meat", Status = status, StartMinute = u - done, DoneMinutes = done, RequiredMinutes = 180,
                                     EndMinute = end, InitiatorActorId = GameState.PlayerId, XpSnapshot = 12 };
            fac.Add(b);
            return b;
        }
        Slot(0, DryingSlot.Drying, 60);                                                   // 在制、未腐败
        var live = Make(s, "raw_meat", ItemInstance.DryingOwner(place, 0), u + 45);
        Make(s, "salt", ItemInstance.DryingOwner(place, 0), null);
        Slot(1, DryingSlot.Spoiled, 55, u - 30);                                           // 已标腐败
        Make(s, "raw_meat", ItemInstance.DryingOwner(place, 1), u - 30);
        Make(s, "salt", ItemInstance.DryingOwner(place, 1), null);
        Slot(2, DryingSlot.Drying, 100);                                                   // 在制但升级时已到期
        Make(s, "raw_meat", ItemInstance.DryingOwner(place, 2), u);
        Make(s, "salt", ItemInstance.DryingOwner(place, 2), null);
        Slot(3, DryingSlot.Done, 180, u - 10).XpNote = "烹饪经验+12";                       // 已完成
        var dmExp = u - 10 + 10080;
        Make(s, "dried_meat", ItemInstance.DryingOwner(place, 3), dmExp);
        var j = JObject.Parse(SaveSerializer.Serialize(s));
        j["SchemaVersion"] = "38";
        var t = SaveSerializer.Deserialize(j.ToString(), Cfg);
        Assert.That(t.SchemaVersion, Is.EqualTo(GameState.CurrentSchema));
        Assert.That(t.Log.Count(l => l.Text.Contains("存档已升级到版本39")), Is.EqualTo(1));

        var ts = Slots(t);
        var m0 = t.Items.Single(i => i.InstanceId == live.InstanceId);
        Assert.That((m0.ShelfPausedMinutes, m0.ExpiresAt), Is.EqualTo(((int?)45, (int?)null)), "R＝E−U，不倒推补偿");
        Assert.That(ts.Single(b => b.Slot == 0).ShelfPaused, Is.True);
        Assert.That((ts.Single(b => b.Slot == 0).DoneMinutes, ts.Single(b => b.Slot == 0).XpSnapshot), Is.EqualTo((60, 12)), "保留已晾分钟与经验快照");
        Assert.That(ts.Single(b => b.Slot == 1).Status, Is.EqualTo(DryingSlot.Spoiled), "已腐败不复活");
        Assert.That(ts.Single(b => b.Slot == 2).ShelfPaused, Is.False, "升级时已到期的不保护");
        Assert.That(t.Items.Single(i => i.ItemId == "dried_meat").ExpiresAt, Is.EqualTo(dmExp), "已完成肉干截止不改");

        // 反复读档：快照不重算、不多加
        var r1 = SaveSerializer.Deserialize(SaveSerializer.Serialize(t), Cfg);
        AdvanceTo(r1, u + 60);
        var r2 = SaveSerializer.Deserialize(SaveSerializer.Serialize(r1), Cfg);
        Assert.That(r2.Items.Single(i => i.InstanceId == live.InstanceId).ShelfPausedMinutes, Is.EqualTo(45), "读档不重新计算加时");
        Assert.That(r2.Log.Count(l => l.Text.Contains("存档已升级到版本39")), Is.EqualTo(1), "迁移只一次");
        Assert.That(Slots(r2).Single(b => b.Slot == 2).Status, Is.EqualTo(DryingSlot.Spoiled), "旧在制已到期批按旧规则停止、不出肉干");
        Assert.That(Slots(r2).Single(b => b.Slot == 0).Status, Is.EqualTo(DryingSlot.Drying), "越过旧原截止仍在晾");

        // 清理旧腐败批：腐肉按原规则、盐原样退回一次
        Ok(E.TakeDrying(r2, 1, false));
        Assert.That(Count(r2, "salt", r2.Ground), Is.EqualTo(1));
        Assert.That(E.TakeDrying(r2, 1, false).Reason, Does.Contain("是空的"), "盐只退一次");
        Assert.That(r2.Items.Where(i => i.ItemId == "raw_meat" && i.Owner == r2.Ground).All(i => E.IsSpoiled(r2, i)), Is.True, "腐肉不回活");

        // 在制批完成：只出一份肉干（加上原已完成的一份）
        AdvanceTo(r2, u + 120);
        var r3 = SaveSerializer.Deserialize(SaveSerializer.Serialize(r2), Cfg);
        Assert.That(Slots(r3).Single(b => b.Slot == 0).Status, Is.EqualTo(DryingSlot.Done));
        Assert.That(Count(r3, "dried_meat"), Is.EqualTo(2), "不补产物、不重复出");
        Assert.That(r3.Items.Single(i => i.ItemId == "dried_meat" && i.Owner == ItemInstance.DryingOwner(place, 0)).ExpiresAt, Is.EqualTo(u + 120 + 10080));
    }
}
