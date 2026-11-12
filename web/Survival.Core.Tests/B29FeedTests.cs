using Newtonsoft.Json.Linq;
using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 B29 同行食饮入口（R14、R67、R90—R93、R96）。合同 assets/20261105/b29-entry-water-q258-268.json feeding；
/// 验收 Q258-268-A06—A09、A13、A14。
/// </summary>
public class B29FeedTests : TestBase
{
    private const string Me = GameState.PlayerId;
    private const string Lin = "npc_lin_zhao";
    private const string Tang = "npc_tang_li";

    private GameState Party(params string[] ids)
    {
        var s = New();
        s.Progress.Flags.Add("hub_registered");
        s.Progress.Flags.Add("church_route_record");
        s.Progress.Flags.Add("academy_safety_record");
        s.SpawnNextCheck = int.MaxValue;
        s.PatrolNextCheck = int.MaxValue;
        s.GameMinute = At(1, 10, 0);
        foreach (var id in ids.Length == 0 ? new[] { Lin, Tang } : ids) E.DebugRecruit(s, id);
        foreach (var a in s.Actors.Values)
        {
            a.Stats.Satiety = 50;
            a.Stats.Hydration = 50;
            a.Stats.Morale = 50;
        }
        return s;
    }

    private static string Bag(string actor) => ItemInstance.CarriedOwner(actor);

    private ItemInstance Give(GameState s, string item, string owner, int count = 1) => E.CreateItems(s, item, count, owner).Single();

    private static FeedRequest Req(string target, string source, ItemInstance item) =>
        new FeedRequest { TargetActorId = target, SourceActorId = source, InstanceId = item.InstanceId };

    // ───────────── Q268 零收益与加工拒绝（A13、A14） ─────────────

    [Test]
    public void A13_ZeroDelta_EachStat_FormalSubstring()
    {
        var c = (JObject)JObject.Parse(File.ReadAllText(Doc()))["feeding"]!["deltaCopy"]!;
        Assert.That(c["zero"]!.Value<string>(), Is.EqualTo(FeedCopy.ZeroDelta));
        Assert.That(c["processingFood"]!.Value<string>(), Is.EqualTo(FeedCopy.ProcessingFood));

        var s = Party();
        s.Actors[Lin].Stats.Satiety = 100;
        Assert.That(E.Feed(s, Req(Lin, Me, Give(s, "canned_food", ItemInstance.Carried))).Ok, Is.True);
        Assert.That(s.Log.Last().Text, Does.EndWith("：饱食无变化。"));

        var t = Party();
        t.Actors[Lin].Stats.Hydration = 100;
        var water = Give(t, "clean_water", ItemInstance.Carried);
        Assert.That(E.FeedPreview(t, Req(Lin, Me, water)).Feed!.ChangeText, Is.EqualTo("水分无变化"));
    }

    [Test]
    public void A13_ZeroDelta_MergesWithOtherFeedback_OriginalSeparator()
    {
        var s = Party();
        s.Actors[Lin].Stats.Hydration = 100;
        var dirty = Give(s, "dirty_water", ItemInstance.Carried);
        Assert.That(E.FeedPreview(s, Req(Lin, Me, dirty)).Feed!.ChangeText, Is.EqualTo("水分无变化，出现腹泻（12小时）"));
    }

    [Test]
    public void A13_TinyNonZero_KeepsSignAndPrecision()
    {
        var s = Party();
        s.Actors[Lin].Stats.Hydration = 100 - 1e-5;
        var water = Give(s, "clean_water", ItemInstance.Carried);
        var text = E.FeedPreview(s, Req(Lin, Me, water)).Feed!.ChangeText;
        Assert.That(text, Does.StartWith("水分+").And.Not.Contain("无变化"), "极小非零不写无变化");
    }

    [Test]
    public void A13_SelfUseCopy_Unchanged()
    {
        var s = Party();
        s.Player.Stats.Satiety = 100;
        var can = Give(s, "canned_food", ItemInstance.Carried);
        Start(s, ActionRequest.UseInstance(can.InstanceId));
        Assert.That(string.Join("\n", s.Log.Select(l => l.Text)), Does.Contain("饱食+").And.Not.Contain("无变化"), "主角自用仍写配置量");
    }

    [Test]
    public void A14_ProcessingFood_OnlyForFoodProcessBranch()
    {
        var s = Party();
        var pot = Give(s, "pot", ItemInstance.Carried);
        var food = Give(s, "boiled_root", ItemInstance.ContainerOwner(pot.InstanceId));
        var water = Give(s, "dirty_water", ItemInstance.ContainerOwner(pot.InstanceId), 2);
        var fac = s.Locations["camp"].Facility;
        fac.Processes.Add(new FacilityProcess { RecipeId = "boil_root", TargetInstanceId = food.InstanceId, PotInstanceId = pot.InstanceId });
        Assert.That(E.FeedBlocked(s, Req(Lin, Me, food)), Is.EqualTo(FeedCopy.ProcessingFood));

        // 真实预留先于加工分支，仍写原句
        food.ReservedBy = 5;
        Assert.That(E.FeedBlocked(s, Req(Lin, Me, food)), Is.EqualTo("该物品已被预留"));
        food.ReservedBy = null;

        // 锅水不套“食物正在加工”
        fac.Processes.Clear();
        fac.Processes.Add(new FacilityProcess { RecipeId = "boil_water", TargetInstanceId = water.InstanceId, PotInstanceId = pot.InstanceId });
        Assert.That(E.FeedBlocked(s, Req(Lin, Me, water)), Is.EqualTo("该物品已被预留"));
    }

    [Test]
    public void A14_SelfUseQ23Entry_Unchanged()
    {
        var s = Party();
        var pot = Give(s, "pot", ItemInstance.Carried);
        var food = Give(s, "boiled_root", ItemInstance.ContainerOwner(pot.InstanceId));
        s.Locations["camp"].Facility.Processes.Add(new FacilityProcess { RecipeId = "boil_root", TargetInstanceId = food.InstanceId, PotInstanceId = pot.InstanceId });
        Assert.That(E.CanStart(s, ActionRequest.UseInstance(food.InstanceId)), Is.Null, "主角直接吃受保护食材的入口不变");
    }

    // ───────────── 局部盘逐字稿 ─────────────

    private static IEnumerable<(string path, string text)> Flat(JObject o, string prefix = "")
    {
        foreach (var p in o.Properties())
        {
            if (p.Value is JObject child)
                foreach (var x in Flat(child, prefix + p.Name + ".")) yield return x;
            else if (p.Value.Type == JTokenType.String) yield return (prefix + p.Name, p.Value.Value<string>()!);
        }
    }

    [Test]
    public void Panel_CopyVerbatimAgainstContract()
    {
        var c = (JObject)JObject.Parse(File.ReadAllText(Doc()))["feeding"]!["panelCopy"]!;
        var flat = Flat(c).ToList();
        Assert.That(flat.Select(x => x.path), Is.EquivalentTo(FeedPanelCopy.All.Keys));
        foreach (var (path, text) in flat) Assert.That(FeedPanelCopy.All[path], Is.EqualTo(text), path);
        Assert.That(FeedPanelCopy.HelpText, Is.EqualTo(c["helpText"]!.Values<string>()));
    }

    private FeedDraft Open(GameState s, ItemInstance i) => FeedDraft.From(E.FeedEntry(s, i.InstanceId)!);

    private FeedPanelView Pick(GameState s, FeedDraft d, string target)
    {
        d.TargetActorId = target;
        d.SourceCleared = false;
        return E.FeedPanel(s, d);
    }

    // ───────────── A06 同类来源 ─────────────

    [Test]
    public void A06_Food_SameItemIdOnly_PlayerFirst_ThirdPersonNotListed()
    {
        var s = Party();
        var mine = Give(s, "canned_food", ItemInstance.Carried, 2);
        var theirs = Give(s, "canned_food", Bag(Lin));
        Give(s, "canned_food", Bag(Tang));
        Give(s, "berry", ItemInstance.Carried);
        E.CreateItems(s, "canned_food", 1, s.Ground);
        var pot = Give(s, "pot", ItemInstance.Carried);
        var inPot = Give(s, "canned_food", ItemInstance.ContainerOwner(pot.InstanceId));
        var rows = E.FeedClassRows(s, Lin, "canned_food");
        Assert.That(rows.Select(r => r.InstanceId), Is.EqualTo(new[] { mine.InstanceId, inPot.InstanceId, theirs.InstanceId }));
        Assert.That(rows.Select(r => r.SourceActorId), Is.EqualTo(new[] { Me, Me, Lin }));
        Assert.That(rows[1].Kind, Is.EqualTo(FeedSourceKind.PotFood));
    }

    [Test]
    public void A06_Water_CardCanteenPotTogether_CleanDirtySeparate_OwnPortion()
    {
        var s = Party();
        var card = Give(s, "clean_water", ItemInstance.Carried, 2);
        var canteen = Give(s, "ration_canteen", Bag(Lin));
        canteen.LiquidId = "clean_water";
        canteen.LiquidMl = 750;
        var empty = Give(s, "ration_canteen", ItemInstance.Carried);
        var pot = Give(s, "pot", ItemInstance.Carried);
        Give(s, "clean_water", ItemInstance.ContainerOwner(pot.InstanceId), 2);
        var dirty = Give(s, "dirty_water", ItemInstance.Carried);
        var rows = E.FeedClassRows(s, Lin, "clean_water");
        Assert.That(rows.Select(r => r.Kind), Is.EqualTo(new[] { FeedSourceKind.Water, FeedSourceKind.PotWater, FeedSourceKind.Canteen }));
        Assert.That(rows.Select(r => r.PortionText), Is.EqualTo(new[] { "净水500毫升", "净水500毫升", "净水250毫升" }));
        Assert.That(rows[0].Text, Is.EqualTo($"你的随身物品 · 净水1000毫升（2份，每份500毫升）"));
        Assert.That(rows[1].Text, Is.EqualTo($"你的随身锅 · 净水1000毫升（2份，每份500毫升）"));
        Assert.That(rows[2].Text, Is.EqualTo($"{E.ActorDisplayName(s, Lin)}的随身{E.ItemName("ration_canteen")} · 净水750/2000毫升（3次，每次250毫升）"));
        Assert.That(rows.Any(r => r.InstanceId == empty.InstanceId || r.InstanceId == dirty.InstanceId), Is.False, "空壶不假定水种，脏水不混入");
        Assert.That(E.FeedClassRows(s, Lin, "dirty_water").Single().InstanceId, Is.EqualTo(dirty.InstanceId));
    }

    [Test]
    public void A06_InvalidRows_Listed_WithOriginalReason_NotSubmittable()
    {
        var s = Party();
        var rotten = Give(s, "berry", ItemInstance.Carried);
        rotten.ExpiresAt = s.GameMinute;
        var held = Give(s, "berry", Bag(Lin));
        held.ExpiresAt = s.GameMinute + 600;
        held.ReservedBy = 3;
        var rows = E.FeedClassRows(s, Lin, "berry");
        Assert.That(rows.Select(r => r.Blocked), Is.EqualTo(new[] { "已腐败，不能食用", "该物品已被预留" }));

        var d = Open(s, held);
        d.SourceKey = rows[0].Key;
        var v = Pick(s, d, Lin);
        Assert.That(v.Selected!.InstanceId, Is.EqualTo(rotten.InstanceId));
        Assert.That(v.ConfirmWhy, Is.EqualTo("已腐败，不能食用"));
        Assert.That(v.BlockedLine, Is.EqualTo("不可用：已腐败，不能食用"));
        Assert.That(v.Request, Is.Null);
    }

    [Test]
    public void A06_PotWaterShort_PhysicalAndUsableNotMixed_ZeroPortions()
    {
        var s = Party();
        var pot = Give(s, "pot", ItemInstance.Carried);
        Give(s, "clean_water", ItemInstance.ContainerOwner(pot.InstanceId));
        s.Locations["camp"].Facility.Processes.Add(new FacilityProcess { RecipeId = "boil_root", TargetInstanceId = 99999, PotInstanceId = pot.InstanceId, ReservedWaterMl = 250 });
        var row = E.FeedClassRows(s, Lin, "clean_water").Single();
        Assert.That(row.Text, Is.EqualTo($"你的随身锅 · 净水500毫升（0份，每份500毫升）"));
        Assert.That(row.Blocked, Is.EqualTo("锅内净水不足500毫升，无法饮用1份。"));
    }

    [Test]
    public void A06_QueriesAreReadOnly()
    {
        var s = Party();
        var food = Give(s, "canned_food", ItemInstance.Carried);
        var before = SaveSerializer.Serialize(s);
        var d = Open(s, food);
        Pick(s, d, Lin);
        E.FeedClassRows(s, Lin, "canned_food");
        E.CompanionCarried(s, Lin);
        Assert.That(SaveSerializer.Serialize(s), Is.EqualTo(before));
    }

    // ───────────── A07 入口显示与置灰 ─────────────

    [Test]
    public void A07_Hidden_NoCompanion_NotCarried_NotFeedType_EmptyCanteen()
    {
        var alone = New();
        Assert.That(E.FeedEntry(alone, Give(alone, "canned_food", ItemInstance.Carried).InstanceId), Is.Null, "没有同行者隐藏");

        var s = Party();
        Assert.That(E.FeedEntry(s, E.CreateItems(s, "canned_food", 1, s.Ground).Single().InstanceId), Is.Null, "地面隐藏");
        Assert.That(E.FeedEntry(s, Give(s, "bandage", ItemInstance.Carried).InstanceId), Is.Null, "药品隐藏");
        Assert.That(E.FeedEntry(s, Give(s, "raw_root", ItemInstance.Carried).InstanceId), Is.Null, "生食隐藏");
        Assert.That(E.FeedEntry(s, Give(s, "ration_canteen", ItemInstance.Carried).InstanceId), Is.Null, "空壶隐藏");
        Assert.That(E.FeedEntry(s, Give(s, "wood", ItemInstance.Carried).InstanceId), Is.Null);

        var reserve = Give(s, "canned_food", Bag(Tang));
        s.Party.Remove(Tang);
        Assert.That(E.FeedEntry(s, reserve.InstanceId), Is.Null, "候补随身隐藏");
    }

    [Test]
    public void A07_Shown_PlayerAndCompanionBag_PotContent_ByType()
    {
        var s = Party();
        Assert.That(E.FeedEntry(s, Give(s, "canned_food", ItemInstance.Carried).InstanceId)!.Title, Is.EqualTo("给同行者食用"));
        var water = E.FeedEntry(s, Give(s, "dirty_water", Bag(Lin)).InstanceId)!;
        Assert.That(water.Title, Is.EqualTo("给同行者饮用"));
        Assert.That(water.OwnerActorId, Is.EqualTo(Lin));
        Assert.That(water.Blocked, Is.Null);
        var canteen = Give(s, "ration_canteen", ItemInstance.Carried);
        canteen.LiquidId = "dirty_water";
        canteen.LiquidMl = 250;
        Assert.That(E.FeedEntry(s, canteen.InstanceId)!.ClassItemId, Is.EqualTo("dirty_water"));
        var pot = Give(s, "pot", ItemInstance.Carried);
        var inPot = Give(s, "clean_water", ItemInstance.ContainerOwner(pot.InstanceId));
        Assert.That(E.FeedEntry(s, inPot.InstanceId)!.SourceKey, Is.EqualTo($"pot:{pot.InstanceId}:clean_water"));
        s.Actors[Me].Stats.Satiety = 100;
        Assert.That(E.FeedEntry(s, Give(s, "berry", ItemInstance.Carried).InstanceId)!.Blocked, Is.Null, "不因主角饱不饱改变");
    }

    [Test]
    public void A07_Grey_GlobalLocks_NotTreatedAsNoCompanion()
    {
        var s = Party();
        var id = Give(s, "canned_food", ItemInstance.Carried).InstanceId;
        s.Expedition = new ExpeditionState { Id = "x", DungeonId = "d01", Phase = ExpeditionState.Extracting };
        Assert.That(E.FeedEntry(s, id)!.Blocked, Is.EqualTo(FeedCopy.Extracting));
        s.Expedition = null;
        s.Battle = new BattleState { BattleId = 9, LocationId = "camp", Allies = new List<string> { Me, Lin } };
        Assert.That(E.FeedEntry(s, id)!.Blocked, Is.EqualTo(FeedCopy.Battle));
    }

    [Test]
    public void A07_Grey_ItemReasons_OriginalSentences()
    {
        var s = Party();
        var old = Give(s, "berry", ItemInstance.Carried);
        old.ExpiresAt = s.GameMinute;
        Assert.That(E.FeedEntry(s, old.InstanceId)!.Blocked, Is.EqualTo("已腐败，不能食用"));
        var held = Give(s, "canned_food", ItemInstance.Carried);
        held.ReservedBy = 7;
        Assert.That(E.FeedEntry(s, held.InstanceId)!.Blocked, Is.EqualTo("该物品已被预留"));
        var pot = Give(s, "pot", ItemInstance.Carried);
        var food = Give(s, "boiled_root", ItemInstance.ContainerOwner(pot.InstanceId));
        s.Locations["camp"].Facility.Processes.Add(new FacilityProcess { RecipeId = "boil_root", TargetInstanceId = food.InstanceId, PotInstanceId = pot.InstanceId });
        Assert.That(E.FeedEntry(s, food.InstanceId)!.Blocked, Is.EqualTo(FeedCopy.ProcessingFood));
    }

    // ───────────── A08 局部盘四区 ─────────────

    [Test]
    public void A08_BeforeTarget_NoSubmit_CandidateKept()
    {
        var s = Party();
        var food = Give(s, "canned_food", ItemInstance.Carried);
        var d = Open(s, food);
        Assert.That(d.TargetActorId, Is.Null, "开盘目标为空");
        var v = E.FeedPanel(s, d);
        Assert.That(v.Title, Is.EqualTo("给同行者食用"));
        Assert.That(v.Targets.Select(t => t.ActorId), Is.EquivalentTo(new[] { Lin, Tang }));
        Assert.That(v.Rows, Is.Empty);
        Assert.That(v.SourceNotes, Is.EqualTo(new[] { FeedPanelCopy.BeforeTarget }));
        Assert.That(v.EffectHint, Is.EqualTo(FeedCopy.NoTarget));
        Assert.That(v.ConfirmText, Is.EqualTo("给同行者食用"));
        Assert.That(v.ConfirmWhy, Is.EqualTo(FeedCopy.NoTarget));
        Assert.That(d.SourceKey, Is.Not.Null, "保留所点实例");
    }

    [Test]
    public void A08_FullPreview_LabelsAndStats_FoodWithExpiry()
    {
        var s = Party();
        var food = Give(s, "berry", ItemInstance.Carried, 3);
        food.ExpiresAt = At(3, 8, 0);
        s.Actors[Lin].Stats.Satiety = 40;
        var v = Pick(s, Open(s, food), Lin);
        var name = E.ActorDisplayName(s, Lin);
        var to = Clock.DayTime(food.ExpiresAt.Value);
        Assert.That(v.Selected!.Text, Is.EqualTo($"你的随身物品 · {E.ItemName("berry")}×3 · 到期：{to}"));
        Assert.That(v.EffectLines, Is.EqualTo(new[]
        {
            $"目标：{name}", "出物人：你", $"本次用量：{E.ItemName("berry")}×1", $"当前余量：{E.ItemName("berry")}×3", $"到期：{to}", "预计耗时：5分钟",
        }));
        Assert.That(v.StatLines.Count(l => l.StartsWith($"饱食{Fmt.N(40)}→")), Is.EqualTo(1));
        Assert.That(v.ConfirmText, Is.EqualTo($"给{name}食用：{E.ItemName("berry")}×1 · 5分钟"));
        Assert.That(v.ConfirmWhy, Is.Null);
        Assert.That(E.Feed(s, v.Request!).Ok, Is.True, "视图给的请求能直接提交");
    }

    [Test]
    public void A08_NoExpiry_SegmentOmitted_ZeroDeltaAndOverflow()
    {
        var s = Party();
        var water = Give(s, "clean_water", ItemInstance.Carried);
        s.Actors[Lin].Stats.Hydration = 100;
        var v = Pick(s, Open(s, water), Lin);
        Assert.That(string.Join("", v.EffectLines) + v.Selected!.Text, Does.Not.Contain("到期"));
        Assert.That(v.StatLines, Does.Contain("水分无变化"));
        Assert.That(v.OverflowText, Is.EqualTo("超出上限的部分不会增加。"));
    }

    [Test]
    public void A08_CanteenRemaining_OwnAmount_RiskOnDirty()
    {
        var s = Party();
        var c = Give(s, "ration_canteen", ItemInstance.Carried);
        c.LiquidId = "dirty_water";
        c.LiquidMl = 500;
        var v = Pick(s, Open(s, c), Lin);
        Assert.That(v.EffectLines[2], Is.EqualTo("本次用量：脏水250毫升"));
        Assert.That(v.EffectLines[3], Is.EqualTo("当前余量：脏水500/2000毫升"));
        Assert.That(v.RiskText, Is.EqualTo(E.UseRiskText(E.Item("dirty_water"))));
    }

    [Test]
    public void A08_NoRows_And_NoUsableRows_Distinct()
    {
        var s = Party();
        var food = Give(s, "canned_food", Bag(Tang));
        var v = Pick(s, Open(s, food), Lin);
        Assert.That(v.Rows, Is.Empty);
        Assert.That(v.SourceNotes, Does.Contain($"你和{E.ActorDisplayName(s, Lin)}随身没有同类食物或饮水。"));
        Assert.That(v.SourceNotes, Does.Contain(FeedPanelCopy.SourceCleared), "第三人的候选被清");

        var t = Party();
        var old = Give(t, "berry", Bag(Lin));
        old.ExpiresAt = t.GameMinute;
        var e = new FeedDraft { ClassItemId = "berry", TargetActorId = Lin };
        var w = E.FeedPanel(t, e);
        Assert.That(w.SourceNotes, Is.EqualTo(new[] { FeedPanelCopy.NoUsableRows }));
        Assert.That(w.ConfirmWhy, Is.EqualTo(FeedPanelCopy.SourceRequired));
    }

    [Test]
    public void A08_SourceRequired_WhenUsableRowsButNonePicked()
    {
        var s = Party();
        Give(s, "canned_food", ItemInstance.Carried);
        var v = E.FeedPanel(s, new FeedDraft { ClassItemId = "canned_food", TargetActorId = Lin });
        Assert.That(v.SourceNotes, Is.EqualTo(new[] { FeedPanelCopy.SourceRequired }));
        Assert.That(v.EffectHint, Is.EqualTo(FeedPanelCopy.SourceRequired));
        Assert.That(v.Request, Is.Null);
    }

    [Test]
    public void A08_GlobalLockAfterOpen_RowStays_ConfirmBlocked()
    {
        var s = Party();
        var food = Give(s, "canned_food", ItemInstance.Carried);
        var d = Open(s, food);
        Pick(s, d, Lin);
        // B41e3：旧版待处理敌踪锁随旧接敌流程删除，全局锁用战斗
        s.Battle = new BattleState { BattleId = 9, LocationId = "camp", Allies = new List<string> { Me, Lin } };
        var v = E.FeedPanel(s, d);
        Assert.That(v.Selected, Is.Not.Null);
        Assert.That(v.ConfirmWhy, Is.EqualTo(FeedCopy.Battle));
        Assert.That(v.BlockedLine, Is.EqualTo("不可用：" + FeedCopy.Battle));
    }

    // ───────────── A09 队伍面板与目标留空 ─────────────

    [Test]
    public void A09_CompanionCarried_RootsAndPotContents_WithEntries()
    {
        var s = Party();
        s.Items.RemoveAll(i => i.Owner == Bag(Lin));
        Assert.That(E.CompanionCarried(s, Lin), Is.Empty);

        var food = Give(s, "canned_food", Bag(Lin), 2);
        var wood = Give(s, "wood", Bag(Lin));
        var pot = Give(s, "pot", Bag(Lin));
        var water = Give(s, "clean_water", ItemInstance.ContainerOwner(pot.InstanceId));
        var rows = E.CompanionCarried(s, Lin);
        Assert.That(rows.Select(r => r.InstanceId), Is.EqualTo(new[] { food.InstanceId, wood.InstanceId, pot.InstanceId, water.InstanceId }));
        Assert.That(rows.Select(r => r.InPot), Is.EqualTo(new[] { false, false, false, true }));
        Assert.That(rows[0].Text, Is.EqualTo($"{E.ItemName("canned_food")}×2"), "Q273 起不带实例编号");
        Assert.That(rows.Select(r => r.Feed != null), Is.EqualTo(new[] { true, false, false, true }));
    }

    [Test]
    public void A09_TargetAlwaysEmpty_FromCompanionBag_OwnerNotPrefilled()
    {
        var s = Party();
        var food = Give(s, "canned_food", Bag(Lin));
        var d = FeedDraft.From(E.CompanionCarried(s, Lin).Single(r => r.InstanceId == food.InstanceId).Feed!);
        Assert.That(d.TargetActorId, Is.Null);
    }

    [Test]
    public void A09_OtherTarget_ThirdPartyCandidateCleared_NoAutoSwap()
    {
        var s = Party();
        var hers = Give(s, "canned_food", Bag(Lin));
        Give(s, "canned_food", ItemInstance.Carried);
        var d = Open(s, hers);
        var v = Pick(s, d, Tang);
        Assert.That(d.SourceKey, Is.Null);
        Assert.That(v.Selected, Is.Null, "不自动换成你的那份");
        Assert.That(v.SourceNotes, Does.Contain(FeedPanelCopy.SourceCleared));
        Assert.That(v.Rows, Has.Count.EqualTo(1), "第三人的那份不列");

        var back = Pick(s, d, Lin);
        Assert.That(back.Selected, Is.Null, "换回物主也不自动恢复");
        Assert.That(back.SourceNotes, Does.Not.Contain(FeedPanelCopy.SourceCleared));
    }

    [Test]
    public void A09_TargetIsOwner_CandidateKept()
    {
        var s = Party();
        var hers = Give(s, "canned_food", Bag(Lin));
        var v = Pick(s, Open(s, hers), Lin);
        Assert.That(v.Selected!.InstanceId, Is.EqualTo(hers.InstanceId));
        Assert.That(v.ConfirmWhy, Is.Null);
    }

    [Test]
    public void A09_SourceVanished_Cleared_BecomesIllegal_Kept()
    {
        var s = Party();
        var food = Give(s, "canned_food", ItemInstance.Carried);
        var d = Open(s, food);
        Pick(s, d, Lin);
        food.ReservedBy = 3;
        var v = E.FeedPanel(s, d);
        Assert.That(v.Selected, Is.Not.Null, "变非法保留所选行");
        Assert.That(v.ConfirmWhy, Is.EqualTo("该物品已被预留"));
        food.ReservedBy = null;
        s.Items.Remove(food);
        v = E.FeedPanel(s, d);
        Assert.That(v.Selected, Is.Null, "消失才清");
        Assert.That(v.SourceNotes, Does.Contain(FeedPanelCopy.SourceCleared));
    }

    [Test]
    public void A09_StaleStamp_RejectedWithChanged()
    {
        var s = Party();
        var food = Give(s, "canned_food", ItemInstance.Carried, 2);
        var d = Open(s, food);
        var req = Pick(s, d, Lin).Request!;
        food.Count = 1;
        Assert.That(E.FeedBlocked(s, req), Is.EqualTo(FeedCopy.Changed));
    }

    [Test]
    public void DevKit_BuildsPlaytestScene()
    {
        var s = New();
        E.DebugFeedKit(s);
        var targets = E.FeedTargets(s);
        Assert.That(targets, Has.Count.EqualTo(2));
        var rows = E.CompanionCarried(s, targets[0].ActorId);
        Assert.That(rows.Any(r => r.InPot && r.Feed != null), Is.True, "锅水有入口");
        Assert.That(rows.Any(r => r.Feed?.Blocked == "已腐败，不能食用"), Is.True, "腐败行置灰");
        Assert.That(E.FeedClassRows(s, targets[0].ActorId, "clean_water").Select(r => r.Kind), Is.EquivalentTo(new[] { FeedSourceKind.Canteen, FeedSourceKind.PotWater }));
    }

    private static string Doc()
    {
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "doc", "策划案"))) dir = dir.Parent;
        Assert.That(dir, Is.Not.Null, "找不到仓库根目录");
        return Path.Combine(dir!.FullName, "doc", "策划案", "系统策划总册 v1.0", "assets", "20261105", "b29-entry-water-q258-268.json");
    }
}
