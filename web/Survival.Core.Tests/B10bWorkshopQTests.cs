using Survival.Core.Config;
using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 B10b：策划提交 a2e4c35 答复 Q107–Q111——工坊位置（Q107）、登记知识（Q108）、军械台蓝图（Q109）核对，
/// 医疗台三材料经验归医疗（Q110，WS-Q110），火堆煅烧木炭 fire_charcoal（Q111，WS-Q111a–g；A10 本轮 11 类工程用例）。
/// </summary>
public class B10bWorkshopQTests : TestBase
{
    private const string Fire = "facility:camp:fire";

    private static ActionRequest Craft(string id, int batch = 1) => ActionRequest.Craft(id, null, null, batch);

    private void Ok(GameState s, InstantRequest r)
    {
        var res = E.DoInstant(s, r);
        Assert.That(res.Ok, Is.True, res.Reason);
    }

    private static void ClearWood(GameState s) => s.Items.RemoveAll(i => i.ItemId == "wood");

    private static int Wood(GameState s, string owner) => s.Items.Where(i => i.ItemId == "wood" && i.Owner == owner).Sum(i => i.Count);

    private static int Charcoal(GameState s) => s.Items.Where(i => i.ItemId == "charcoal").Sum(i => i.Count);

    private static FacilityState Fac(GameState s) => s.Locations["camp"].Facility;

    private static List<FacilityProcess> Burns(GameState s) => Fac(s).Processes.Where(p => p.RecipeId == "fire_charcoal").ToList();

    private void Lit(GameState s, int fuel)
    {
        Fac(s).FireLit = true;
        Fac(s).FuelMinutes = fuel;
    }

    /// <summary>新局清掉木材后：随身 carried 份、营地地面 ground 份。</summary>
    private GameState WithWood(int carried, int ground)
    {
        var s = New();
        ClearWood(s);
        if (carried > 0) E.CreateItems(s, "wood", carried, ItemInstance.Carried);
        if (ground > 0) E.CreateItems(s, "wood", ground, "camp");
        return s;
    }

    private void Ignite(GameState s)
    {
        var lighter = E.IgniteToolOptions(s).First(o => o.Usable);
        Start(s, ActionRequest.Ignite(lighter.InstanceId));
        Run(s);
    }

    /// <summary>推进 minutes 世界分钟（普通内部等待，代表“其他正常世界行动”）。</summary>
    private void Pass(GameState s, int minutes)
    {
        var end = s.GameMinute + minutes;
        while (s.GameMinute < end)
        {
            if (s.Main == null) Start(s, ActionRequest.Wait(60));
            else if (s.Main.Status == ActionStatus.Paused) E.Resume(s);
            E.Advance(s);
        }
        if (s.Main != null) E.Cancel(s);
    }

    [Test]
    public void ProcessConfig_FireCharcoal_MatchesAdjudication()
    {
        var p = Cfg.Processes.Single(x => x.Id == "fire_charcoal");
        Assert.That(p.IsSlotBatch, Is.True);
        Assert.That((p.InputId, p.InputCount, p.OutputId, p.OutputCount, p.HeatMinutes), Is.EqualTo(("wood", 4, "charcoal", 3, 60)));
        Assert.That(p.RequiredOwner, Is.EqualTo(ProcessOwners.FireSlot));
        Assert.That(Cfg.Fire.ProcessingSlots, Is.EqualTo(2), "仍是原两加工位");
        Assert.That(Cfg.Fire.MaxPots, Is.EqualTo(1));
        Assert.That(Cfg.Fire.DirectFoodInputIds, Does.Not.Contain("wood"), "木材不进直烤白名单");
        var charcoal = Cfg.Items.Single(i => i.Id == "charcoal");
        Assert.That((charcoal.WeightGrams, charcoal.StackMax, charcoal.ShelfLifeMinutes), Is.EqualTo((200, 30, (int?)null)), "复用原木炭 200g/堆叠30");
        Assert.That(Cfg.Progression.Cooking.FoodProcesses, Does.Not.Contain("fire_charcoal"), "不占烹饪经验");
    }

    /// <summary>WS-Q111a：基础开放投 4 木占一位，0 分钟、不加燃料、无工具蓝图经验；随身+地面合计；缺料/无空位整次拒绝；拖木仍是加燃料。</summary>
    [Test]
    public void WS_Q111a_Load_FromCarriedAndGround_OneSlot_RejectsWhole()
    {
        var s = WithWood(2, 3);
        E.CreateItems(s, "wood", 5, "field");   // 别处的木材不取
        var t0 = s.GameMinute;
        var info = E.CharcoalLoadPreview(s);
        Assert.That(info.Ok, Is.True, info.Reason);
        Assert.That(info.WoodAvailable, Is.EqualTo(5));
        Assert.That(info.Sources, Has.Count.EqualTo(2));
        Assert.That(info.Sources[0], Does.StartWith("随身木材").And.EndWith("×2"));
        Assert.That(info.Sources[1], Does.StartWith("地面木材").And.EndWith("×2"));
        var xp = s.Skills.ToDictionary(k => k.Key, k => k.Value.Xp);
        Ok(s, InstantRequest.LoadCharcoal());
        Assert.That(s.GameMinute, Is.EqualTo(t0), "投料 0 世界分钟");
        Assert.That(Wood(s, ItemInstance.Carried), Is.EqualTo(0));
        Assert.That(Wood(s, "camp"), Is.EqualTo(1));
        Assert.That(Wood(s, "field"), Is.EqualTo(5));
        var batch = E.FireContents(s, "camp").Single();
        Assert.That((batch.ItemId, batch.Count, batch.Owner), Is.EqualTo(("wood", 4, Fire)));
        var p = Burns(s).Single();
        Assert.That((p.TargetInstanceId, p.HeatDoneMinutes, p.RequiredHeatMinutes), Is.EqualTo((batch.InstanceId, 0, 60)));
        Assert.That(Fac(s).FuelMinutes, Is.EqualTo(0), "原料不当燃料");
        Assert.That(Fac(s).FireLit, Is.False, "可在熄灭时投料，不自动点火");
        Assert.That(s.Skills.ToDictionary(k => k.Key, k => k.Value.Xp), Is.EqualTo(xp), "投料不给经验");
        Assert.That(E.ContentStatus(s, batch), Is.EqualTo("火堆未燃烧 · 煅烧暂停 0/60分钟"));

        // 缺料：只剩 1 份，整次拒绝、什么都不变
        var before = SaveSerializer.Serialize(s);
        var miss = E.DoInstant(s, InstantRequest.LoadCharcoal());
        Assert.That(miss.Ok, Is.False);
        Assert.That(miss.Reason, Does.Contain("木材不足").And.Contain("1/4"));
        Assert.That(SaveSerializer.Serialize(s), Is.EqualTo(before));

        // 第二批占另一位（锅不在火堆里）；两位满后第三批拒绝
        E.CreateItems(s, "wood", 7, "camp");
        Ok(s, InstantRequest.LoadCharcoal());
        Assert.That(Burns(s), Has.Count.EqualTo(2));
        var full = E.DoInstant(s, InstantRequest.LoadCharcoal());
        Assert.That(full.Ok, Is.False);
        Assert.That(full.Reason, Does.StartWith("火堆加工位已满"));

        // 拖木到火堆仍是加燃料（AddFuel），不能拖进加工位
        var w = s.Items.First(i => i.ItemId == "wood" && i.Owner == "camp");
        Assert.That(E.MaxPutInFire(s, w, out var why), Is.EqualTo(0));
        Assert.That(why, Does.Contain("添加燃料").And.Contain("煅烧木炭"));
        Ok(s, InstantRequest.AddFuel(1, w.InstanceId));
        Assert.That(Fac(s).FuelMinutes, Is.EqualTo(60));
        Assert.That(Burns(s), Has.Count.EqualTo(2), "加燃料不改加工位");
    }

    /// <summary>WS-Q111a：行动中、暂停中、敌踪时投料拒绝；带 requestId 重发只生效一次。</summary>
    [Test]
    public void WS_Q111a_Load_BlockedWhileBusy_Idempotent()
    {
        var s = WithWood(0, 8);
        Start(s, ActionRequest.Wait(30));
        Assert.That(E.DoInstant(s, InstantRequest.LoadCharcoal()).Reason, Does.Contain("行动进行中"));
        Assert.That(E.CharcoalLoadPreview(s).Ok, Is.False);
        Run(s);
        var r = InstantRequest.LoadCharcoal().WithRequestId("req-charcoal-1");
        Ok(s, r);
        var again = E.DoInstant(s, InstantRequest.LoadCharcoal().WithRequestId("req-charcoal-1"));
        Assert.That(again.Ok, Is.True);
        Assert.That(Burns(s), Has.Count.EqualTo(1), "同一请求重发不双投");
        Assert.That(Wood(s, "camp"), Is.EqualTo(4));
    }

    /// <summary>WS-Q111b：已燃足 60 燃料：单批 4 木→3 炭；两批 8 木→6 炭共扣 60 燃料；12 完整段；锅与 1 批并存。</summary>
    [Test]
    public void WS_Q111b_LitSixty_SingleAndDouble_ShareFuel()
    {
        var s = WithWood(0, 8);
        Ok(s, InstantRequest.LoadCharcoal());
        Ok(s, InstantRequest.LoadCharcoal());
        Lit(s, 60);
        Assert.That(E.CharcoalLoadPreview(s).HeatAvailable, Is.EqualTo(60));
        s.Skills["crafting"].Level = 10;   // 不受熟练/工作倍率加速
        Pass(s, 55);
        Assert.That(Burns(s).Select(p => p.HeatDoneMinutes), Is.EqualTo(new[] { 55, 55 }));
        Assert.That(Charcoal(s), Is.EqualTo(0));
        Pass(s, 5);
        Assert.That(Burns(s), Is.Empty);
        Assert.That(Charcoal(s), Is.EqualTo(6));
        Assert.That(Wood(s, Fire) + Wood(s, "camp"), Is.EqualTo(0), "8 木全部核销");
        Assert.That(Fac(s).FuelMinutes, Is.EqualTo(0), "并行不倍扣：只烧 60");
        Assert.That(E.FireContents(s, "camp").Select(i => (i.ItemId, i.Count)), Is.EqualTo(new[] { ("charcoal", 3), ("charcoal", 3) }), "成品留原位");

        // 一口锅 + 一批煅烧并存；锅占位后只能再放 1 批
        var t = WithWood(0, 8);
        var pot = t.Items.First(i => i.ItemId == "pot");
        Ok(t, InstantRequest.PutInFire(pot.InstanceId));
        Ok(t, InstantRequest.LoadCharcoal());
        Assert.That(E.DoInstant(t, InstantRequest.LoadCharcoal()).Reason, Does.StartWith("火堆加工位已满"));
        Lit(t, 60);
        Pass(t, 60);
        Assert.That(Charcoal(t), Is.EqualTo(3));
    }

    /// <summary>WS-Q111c：冷火 0 燃料点火自动加 1 木后点火 5 分钟不计热、只余 55，不够 60；补第二木（共 120）后点火+受热 65 分钟余 55。</summary>
    [Test]
    public void WS_Q111c_ColdFire_OneWoodGives55_TwoWoodsFinishWith55Left()
    {
        var s = WithWood(0, 5);
        var pre = E.CharcoalLoadPreview(s);
        Assert.That(pre.HeatAvailable, Is.EqualTo(55));
        Assert.That(pre.FuelWoodShort, Is.EqualTo(1));
        Assert.That(pre.FuelNote, Does.Contain("点燃后只余55分钟").And.Contain("还需另投1份木材"));
        Ok(s, InstantRequest.LoadCharcoal());
        Ignite(s);   // 燃料 0：自动投剩下的 1 木
        Assert.That(Fac(s).FuelMinutes, Is.EqualTo(55));
        Assert.That(Burns(s).Single().HeatDoneMinutes, Is.EqualTo(0), "点火这 5 分钟不计热");
        Pass(s, 60);
        Assert.That(Fac(s).FireLit, Is.False);
        var p = Burns(s).Single();
        Assert.That(p.HeatDoneMinutes, Is.EqualTo(55), "单燃料木只能受热 55");
        Assert.That(Charcoal(s), Is.EqualTo(0));
        Assert.That(Wood(s, Fire), Is.EqualTo(4), "熄火保原料");

        // 冷火先投 2 木燃料（120），点火后 115；再连续 60 分钟完成、余 55；合计 6 木
        var t = WithWood(0, 6);
        Ok(t, InstantRequest.LoadCharcoal());
        Ok(t, InstantRequest.AddFuel(2));
        Assert.That(E.CharcoalLoadPreview(t).HeatAvailable, Is.EqualTo(115));
        var t0 = t.GameMinute;
        Ignite(t);
        Pass(t, 60);
        Assert.That(t.GameMinute - t0, Is.EqualTo(65));
        Assert.That(Charcoal(t), Is.EqualTo(3));
        Assert.That(Fac(t).FuelMinutes, Is.EqualTo(55));
        Assert.That(t.Items.Any(i => i.ItemId == "wood"), Is.False, "4 原料 + 2 燃料");
    }

    /// <summary>WS-Q111d：燃料不足整段不加部分热、熄火保进度、复燃继续；取出退 4 木清进度、燃料不退；失败取出保持原状。</summary>
    [Test]
    public void WS_Q111d_FuelOut_KeepsProgress_TakeOutReturnsFour_FuelNotRefunded()
    {
        var s = WithWood(0, 4);
        Ok(s, InstantRequest.LoadCharcoal());
        Lit(s, 30);
        Pass(s, 40);
        var p = Burns(s).Single();
        Assert.That(p.HeatDoneMinutes, Is.EqualTo(30), "燃料尽：只加完整段");
        Assert.That(Fac(s).FireLit, Is.False);
        Lit(s, 10);
        Pass(s, 10);
        Assert.That(Burns(s).Single().HeatDoneMinutes, Is.EqualTo(40), "复燃后继续，不重置");

        var batch = E.FireContents(s, "camp").Single();
        var cons = E.TakeOutConsequenceOf(s, batch);
        Assert.That(cons.Text, Does.Contain("煅烧受热40分钟清零").And.Contain("退回木材×4").And.Contain("燃料不退"));
        // 行动进行中取出失败：不清进度
        Start(s, ActionRequest.Wait(30));
        Assert.That(E.DoInstant(s, InstantRequest.TakeOut(batch.InstanceId, ItemInstance.Carried)).Ok, Is.False);
        Assert.That(Burns(s).Single().HeatDoneMinutes, Is.EqualTo(40));
        E.Cancel(s);
        Ok(s, InstantRequest.TakeOut(batch.InstanceId, ItemInstance.Carried));
        Assert.That(Wood(s, ItemInstance.Carried), Is.EqualTo(4));
        Assert.That(Burns(s), Is.Empty, "清该批热进度");
        Assert.That(Fac(s).FuelMinutes, Is.EqualTo(0), "已燃燃料不退");
        Assert.That(Charcoal(s), Is.EqualTo(0));
    }

    /// <summary>WS-Q111e：完成炭留位、不自动取货或再加工、不吞物；保存读档后不双出或返原料；收取时满五卡整次拒绝。</summary>
    [Test]
    public void WS_Q111e_DoneStaysInSlot_ReceiptNoDouble_AfterReload()
    {
        var s = WithWood(0, 4);
        Ok(s, InstantRequest.LoadCharcoal());
        Lit(s, 120);
        Pass(s, 60);
        var done = E.FireContents(s, "camp").Single();
        Assert.That((done.ItemId, done.Count, done.Owner), Is.EqualTo(("charcoal", 3, Fire)));
        Assert.That(Fac(s).Completed.Count(x => x.StartsWith("fire_charcoal#")), Is.EqualTo(1), "完成回执");
        Assert.That(E.ContentStatus(s, done), Is.EqualTo("已完成，留在加工位"));
        Assert.That(s.Log.Any(l => l.Text.Contains("已煅烧成木炭×3") && l.Text.Contains("不给经验")), Is.True);

        var t = SaveSerializer.Deserialize(SaveSerializer.Serialize(s), Cfg);
        Pass(t, 30);
        Assert.That(Charcoal(t), Is.EqualTo(3), "读档后不再出一次");
        Assert.That(t.Items.Any(i => i.ItemId == "wood"), Is.False, "不返原料");
        Assert.That(t.Locations["camp"].Facility.Processes, Is.Empty, "成品不再加工");

        // 成品占位：两位都是成品时不能再投
        E.CreateItems(t, "wood", 8, "camp");
        Ok(t, InstantRequest.LoadCharcoal());
        Assert.That(E.DoInstant(t, InstantRequest.LoadCharcoal()).Reason, Does.StartWith("火堆加工位已满"));

        // 收取：取到随身成为随身卡，满五卡整次拒绝，成品留位
        var u = SaveSerializer.Deserialize(SaveSerializer.Serialize(s), Cfg);
        var c = E.FireContents(u, "camp").Single();
        foreach (var id in new[] { "stone", "fiber", "cloth", "canned_food", "clean_water" }.Take(5 - E.RootCardCount(u)))
            E.CreateItems(u, id, 1, ItemInstance.Carried);
        Assert.That(E.TakeOutBlocked(u, c, ItemInstance.Carried), Does.Contain("整次拒绝"));
        Ok(u, InstantRequest.TakeOut(c.InstanceId, "camp"));
        Assert.That(u.Items.Where(i => i.ItemId == "charcoal" && i.Owner == "camp").Sum(i => i.Count), Is.EqualTo(3));
    }

    /// <summary>WS-Q111f：“等待加工”含锅与炭批，并行取最晚（不相加）；退出不取料不灭火；0 经验；停止时保留进度。</summary>
    [Test]
    public void WS_Q111f_WaitProcess_IncludesCharcoal_LatestNotSum()
    {
        var s = WithWood(0, 4);
        var pot = s.Items.First(i => i.ItemId == "pot");
        E.DebugGive(s, "dirty_water", 2, "camp");
        Ok(s, InstantRequest.FillPot(s.Items.First(i => i.ItemId == "dirty_water" && i.Owner == "camp").InstanceId, pot.InstanceId, 2));
        Ok(s, InstantRequest.PutInFire(pot.InstanceId));
        Ok(s, InstantRequest.LoadCharcoal());
        Lit(s, 120);
        var plan = E.PreviewCookWait(s);
        Assert.That(plan.Ok, Is.True, plan.Reason);
        Assert.That(plan.Targets.Select(x => x.Label), Has.Some.Contains("木材").And.Some.Contains("煅烧"));
        Assert.That(plan.Targets, Has.Count.EqualTo(2));
        Assert.That(plan.EstimatedMinutes, Is.EqualTo(60), "取最晚 60，不是 15+60");
        var cookXp = s.Skills["cooking"].Xp;
        Start(s, ActionRequest.WaitCook());
        Assert.That(s.Main!.Label, Is.EqualTo("等待加工"));
        Run(s);
        Assert.That(s.Log.Last(l => l.Kind == "ActionCompleted").Text, Does.StartWith("等待加工结束").And.Contain("2项"));
        Assert.That(Charcoal(s), Is.EqualTo(3));
        Assert.That(Fac(s).FuelMinutes, Is.EqualTo(60));
        Assert.That(s.Skills["cooking"].Xp - cookXp, Is.EqualTo(2), "只有净水 1000 毫升自身的烹饪经验 +2，煅烧不给");

        // 燃料不足：等待到燃尽停止，保留实际进度；退出不清进度
        var t = WithWood(0, 4);
        Ok(t, InstantRequest.LoadCharcoal());
        Lit(t, 30);
        var p2 = E.PreviewCookWait(t);
        Assert.That(p2.FuelShort, Is.True);
        Assert.That(p2.FuelNote, Does.Contain("当前燃料不足"));
        Start(t, ActionRequest.WaitCook());
        Run(t);
        Assert.That(t.Log.Last(l => l.Kind == "ActionAborted").Text, Does.StartWith("等待加工停止"));
        Assert.That(Burns(t).Single().HeatDoneMinutes, Is.EqualTo(30));
        Assert.That(E.CookXpNote(t, Burns(t).Single()), Is.EqualTo("煅烧不给经验"));
    }

    /// <summary>WS-Q111g：旧档读入不赠炭、不补历史进度、原燃料保留；没有任何主动配方产木炭（晾架不炭化）；锻炉额外燃料与配方炭输入仍各自核销。</summary>
    [Test]
    public void WS_Q111g_LegacySave_NoGift_NoActiveCharcoalRecipe_ForgeFuelSeparate()
    {
        var json = File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "Fixtures", "save-5-midgame.json"));
        var old = SaveSerializer.Deserialize(json, Cfg);
        Assert.That(old.Items.Any(i => i.ItemId == "charcoal"), Is.False, "不赠木炭");
        Assert.That(old.Locations.Values.SelectMany(l => l.Facility.Processes).Any(p => p.RecipeId == "fire_charcoal"), Is.False, "没有历史煅烧进度");

        Assert.That(Cfg.Recipes.Where(r => r.Outputs.ContainsKey("charcoal")), Is.Empty, "旧 charcoal 主动行已迁为被动过程");
        Assert.That(Cfg.Workshops.Stations.Any(x => x.Id == "drying_rack"), Is.False, "晾架不在本批开放");
        var steel = Cfg.Recipes.Single(r => r.Id == "make_steel");
        Assert.That(steel.Inputs["charcoal"], Is.EqualTo(1));
        Assert.That(steel.Fuel["charcoal"], Is.EqualTo(1));
        Assert.That(steel.AllInputs["charcoal"], Is.EqualTo(2), "配方炭与锻炉燃料炭各自核销");

        // 已有火堆的新旧档都直接开放煅烧，不需要登记/工坊
        var s = New();
        Assert.That(s.Progress.Flags.Contains("hub_registered"), Is.False);
        Assert.That(Fac(s).Workshops, Is.Empty);
        E.CreateItems(s, "wood", 2, "camp");
        Assert.That(E.CharcoalLoadPreview(s).Ok, Is.True, "基础开放无门槛");
    }

    /// <summary>WS-Q110：医疗台三材料门槛医疗 1/5/3、完成经验医疗 2/2/3、不给制造；预估同口径；满级不转发；旧档在途按旧规则给制造一次。</summary>
    [Test]
    public void WS_Q110_MedicalMaterials_GiveMedicineXp_NotCrafting()
    {
        var expect = new (string recipe, int min, long xp, (string id, int n)[] mats)[]
        {
            ("make_saline", 1, 2, new[] { ("clean_water", 1), ("salt", 1) }),
            ("make_disinfectant", 5, 2, new[] { ("lab_reagent", 1), ("clean_water", 1) }),
            ("make_herb_extract", 3, 3, new[] { ("herb", 2), ("clean_water", 1) }),
        };
        foreach (var (recipe, min, xp, mats) in expect)
        {
            var rc = Cfg.Recipes.Single(r => r.Id == recipe);
            Assert.That(rc.XpSkill, Is.EqualTo("medicine"), recipe);
            Assert.That(rc.MinSkills["medicine"], Is.EqualTo(min), recipe);
            Assert.That(E.CraftXp(rc.BaseMinutes), Is.EqualTo(xp), recipe);

            var s = New();
            E.DebugBuildAllWorkshops(s);
            s.Skills["medicine"].Level = min;
            foreach (var (id, n) in mats) E.CreateItems(s, id, n, "camp");
            Assert.That(E.Preview(s, Craft(recipe)).XpGains, Is.EqualTo(new[] { $"医疗经验+{xp}" }), recipe);
            var med = s.Skills["medicine"].Xp;
            var cra = s.Skills["crafting"].Xp;
            Start(s, Craft(recipe));
            Assert.That(s.Main!.XpSkill, Is.EqualTo("medicine"), "开工快照");
            Run(s);
            Assert.That(s.Skills["medicine"].Xp - med, Is.EqualTo(xp), recipe);
            Assert.That(s.Skills["crafting"].Xp, Is.EqualTo(cra), $"{recipe} 不双发制造");
        }

        // 其他医疗/物资配方沿原归属：绷带仍给制造
        Assert.That(Cfg.Recipes.Single(r => r.Id == "bandage").XpSkill, Is.Null);
        Assert.That(Cfg.Recipes.Count(r => r.XpSkill != null), Is.EqualTo(3), "只覆盖这三条");

        // 医疗满 10：不再加经验，也不转给制造
        var m = New();
        E.DebugBuildAllWorkshops(m);
        m.Skills["medicine"].Level = 10;
        E.CreateItems(m, "clean_water", 1, "camp");
        E.CreateItems(m, "salt", 1, "camp");
        Assert.That(E.Preview(m, Craft("make_saline")).XpGains.Single(), Does.Contain("医疗已满10级"));
        var craftXp = m.Skills["crafting"].Xp;
        Start(m, Craft("make_saline"));
        Run(m);
        Assert.That(m.Skills["crafting"].Xp, Is.EqualTo(craftXp));

        // 旧档在途（开工时还没有经验门类快照）：按旧规则给制造经验一次
        var o = New();
        E.DebugBuildAllWorkshops(o);
        E.CreateItems(o, "clean_water", 1, "camp");
        E.CreateItems(o, "salt", 1, "camp");
        Start(o, Craft("make_saline"));
        o.Main!.XpSkill = null;
        var o2 = SaveSerializer.Deserialize(SaveSerializer.Serialize(o), Cfg);
        var c0 = o2.Skills["crafting"].Xp;
        var m0 = o2.Skills["medicine"].Xp;
        Run(o2);
        Assert.That(o2.Skills["crafting"].Xp - c0, Is.EqualTo(2));
        Assert.That(o2.Skills["medicine"].Xp, Is.EqualTo(m0));
    }

    /// <summary>WS-Q107/Q108/Q109 核对：只在 camp 自建；原 6 开局知识，其余登记后；军械台蓝图由 d03_08_unique 领取事实（或旧档 communication_power_claimed）授予。</summary>
    [Test]
    public void WS_Q107_Q108_Q109_Checks()
    {
        Assert.That(Cfg.Workshops.BuildLocations, Is.EqualTo(new[] { "camp" }), "Q107：现阶段只开放 camp");
        var initial = Cfg.Knowledge.Where(k => k.Initial).Select(k => k.Id).OrderBy(x => x).ToList();
        Assert.That(initial, Is.EqualTo(new[] { "blueprint_crossbow", "blueprint_hatchet", "blueprint_metal_spear", "blueprint_stone_knife", "blueprint_wooden_bow", "blueprint_wooden_spear" }));
        Assert.That(Cfg.Knowledge.Where(k => !k.Initial && !k.Blueprint).All(k => k.GrantedByFacts.SequenceEqual(new[] { "hub_registered" })), Is.True, "Q108：其余默认知识要真实登记");

        var s = New();
        var armory = Cfg.Knowledge.Single(k => k.Id == "blueprint_armory_station");
        Assert.That(armory.Source, Does.Contain("d03_08"));
        Assert.That(E.KnowsKnowledge(s, armory.Id), Is.False);
        s.Progress.Flags.Add("hub_registered");
        Assert.That(E.KnowsKnowledge(s, armory.Id), Is.False, "登记不给军械台蓝图");
        s.Progress.Flags.Add("claim:d03_08_unique");
        Assert.That(E.KnowsKnowledge(s, armory.Id), Is.True, "d03_08_unique 领取成功即会");
        var legacy = New();
        legacy.Progress.Flags.Add("communication_power_claimed");
        Assert.That(E.KnowsKnowledge(legacy, armory.Id), Is.True, "旧档原等效领取事实补知识");
        Assert.That(legacy.Items.Any(i => i.ItemId == "battery"), Is.False, "不补电源/箱奖");
    }
}
