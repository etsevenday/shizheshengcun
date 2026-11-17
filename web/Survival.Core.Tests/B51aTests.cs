using Newtonsoft.Json.Linq;
using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 批次 B51a「生产原料保质与工具本单保障」（开发记录 B51a）：S08 文首「2026-11-15 B51 Q720—Q728」与「2026-11-15 Q701—Q703」的 B51a 部分
/// （Q701.1 原料暂停保质、Q701.3 工具本单保障、Q720 两成品独立期限、Q722／Q723 恢复句、Q724 保护显示、Q726 工具拒绝句、旧档迁移）。
/// 设施并行（Q701.2、Q721、Q727）与预估 Q725、Q702／Q703／Q728 属 B51b，不在这里。正式句见《重墨 V2 开工裁定与界面逐字稿》B51 表。
/// </summary>
// B51c1 改期望（出处 S08 文首 2026-11-16 B51 §Q771 拼接顺序与短稿、02 Q874 开发方建议 A 暂用）：撤退保留／普通退出预告引句「退出将放弃本次{动作}。」→「退出将取消{动作}。」（维护→维修，现场修补用原名）、
// 结果尾句「已耗时间保留。」→「已耗时间不退。」、删「未生成半成品。」「不会生成半成品。」；处理尸体沿 Q722 引句，只改尾句。锁定自动中止仍「已耗时间保留。」。
public class B51aTests : TestBase
{
    protected override bool NoEnemyApproach => true;

    private static string Web(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "Survival.Web" }.Concat(parts).ToArray()));

    private List<ItemInstance> Make(GameState s, string itemId, int count, string owner, int? expires = null)
    {
        var made = E.CreateItems(s, itemId, count, owner);
        if (expires.HasValue) foreach (var i in made) i.ExpiresAt = expires;
        return made;
    }

    private static string AsSchema(GameState s, string version, Action<JObject>? edit = null)
    {
        var o = JObject.Parse(SaveSerializer.Serialize(s));
        o["SchemaVersion"] = version;
        edit?.Invoke(o);
        return o.ToString();
    }

    private static GameState Reload(GameState s, Survival.Core.Config.GameConfig cfg) => SaveSerializer.Deserialize(SaveSerializer.Serialize(s), cfg);

    // ───────────── M956 存档 54 骨架与通用暂停／恢复 ─────────────

    [Test]
    public void M956_Schema54_ActionFieldsCloneAndSave()
    {
        Assert.That(GameState.CurrentSchema, Is.EqualTo("56"));   // B53a 改期望：存档升 56（55→56 晾制旧停止位判因，S11 文首 2026-11-15 §Q764 选 A）
        var a = new ActionState { ToolGuardId = 7, ToolGuardSpent = true };
        var c = a.Clone();
        Assert.That((c.ToolGuardId, c.ToolGuardSpent), Is.EqualTo(((int?)7, true)));
        var i = new ItemInstance { ItemId = "herb", Count = 1, ExpiresAt = 100, ShelfPausedMinutes = 40 };
        Assert.That(i.Clone().ShelfPausedMinutes, Is.EqualTo(40));
    }

    /// <summary>通用暂停：R＝E−S、原截止保留；暂停期间越过原截止不算腐败、不转腐败物；恢复 C＋R 后照常计时。</summary>
    [Test]
    public void M956_PauseKeepsKey_NotSpoiledPastOriginal_ResumeCPlusR()
    {
        var s = New();
        var herb = Make(s, "herb", 1, s.Ground, s.GameMinute + 30).Single();
        herb.ReservedBy = 999;   // 模拟一份被工作绑定的输入（真实开工见 M957）
        s.Main = new ActionState { ActionId = 999, Type = ActionType.Craft, TargetId = "bandage", ReservedItemIds = { herb.InstanceId }, Status = ActionStatus.Paused };
        var e0 = herb.ExpiresAt;
        herb.ShelfPausedMinutes = 30;
        Assert.That(herb.ExpiresAt, Is.EqualTo(e0), "原截止保留作合堆键");
        s.GameMinute += 60;
        Assert.That(E.IsSpoiled(s, herb), Is.False, "按 R，不按原截止");
        E.UpdateFlags(s);
        Assert.That(herb.ItemId, Is.EqualTo("herb"), "暂停保质的不转腐败物");
        Assert.That(GameEngine.EffectiveExpiry(s, herb), Is.EqualTo(s.GameMinute + 30));
    }

    /// <summary>孤立保护收束：没有在办工作绑定的 R 读档时恢复 C＋R；晾制位与在办行动的保护不动；重复读档幂等。</summary>
    [Test]
    public void M956_OrphanProtectionSweptOnLoad_LiveKept_Idempotent()
    {
        var s = New();
        var orphan = Make(s, "herb", 1, s.Ground, s.GameMinute + 50).Single();
        orphan.ShelfPausedMinutes = 20;
        var live = Make(s, "berry", 1, s.Ground, s.GameMinute + 50).Single();
        live.ShelfPausedMinutes = 25;
        live.ReservedBy = 4242;
        s.Main = new ActionState { ActionId = 4242, Type = ActionType.Craft, TargetId = "bandage", Status = ActionStatus.Paused, ReservedItemIds = { live.InstanceId } };
        var t = Reload(s, Cfg);
        var o = t.Items.Single(i => i.InstanceId == orphan.InstanceId);
        Assert.That((o.ShelfPausedMinutes, o.ExpiresAt), Is.EqualTo(((int?)null, (int?)(t.GameMinute + 20))), "孤立保护按读档时刻 C＋R 恢复");
        var l = t.Items.Single(i => i.InstanceId == live.InstanceId);
        Assert.That(l.ShelfPausedMinutes, Is.EqualTo(25), "在办工作的保护不动");
        var t2 = Reload(t, Cfg);
        Assert.That(t2.Items.Single(i => i.InstanceId == orphan.InstanceId).ExpiresAt, Is.EqualTo(t.GameMinute + 20), "重复读档不再改");
    }

    /// <summary>保护份保留原截止：随身同堆拆出的保护份与原堆仍算同一张根卡；剩余 R 不同的两份不被 Normalize 合并、明细按 R 分批。</summary>
    [Test]
    public void M956_ProtectedPortion_SameRootCard_DifferentRNotMerged()
    {
        var s = New();
        s.Items.RemoveAll(i => i.IsCarried);
        var due = s.GameMinute + 300;
        var stack = Make(s, "herb", 5, ItemInstance.Carried, due).Single();
        Assert.That(E.RootCardCount(s), Is.EqualTo(1));
        var part = E.SplitOff(s, stack, 2);
        part.ReservedBy = 77;
        part.ShelfPausedMinutes = 300;
        Assert.That(E.RootCardCount(s), Is.EqualTo(1), "保护份不多算根卡");
        var berry = Make(s, "berry", 2, s.Ground, due).Single();
        var p1 = E.SplitOff(s, berry, 1);
        berry.ShelfPausedMinutes = 100;
        p1.ShelfPausedMinutes = 200;
        Assert.That(GameEngine.BatchKeyOf(berry), Is.Not.EqualTo(GameEngine.BatchKeyOf(p1)), "按各自 R 分批");
    }
    // ───────────── M957 开工保护、完成核销与释放恢复 C＋R ─────────────

    /// <summary>草药绷带：随身草药×3 只预留 1 份并暂停保质；未投入余料照常计时；根卡不变。</summary>
    private (GameState s, ItemInstance herbStack) BandageWithHerbs(int herbs, int minutesLeft)
    {
        var s = New();
        s.Items.RemoveAll(i => i.ItemId is "cloth" or "herb" or "bandage");
        E.DebugGive(s, "cloth", 2);
        var herb = Make(s, "herb", herbs, ItemInstance.Carried, s.GameMinute + minutesLeft).Single();
        return (s, herb);
    }

    [Test]
    public void Q701_A01_OnlyBoundPortionPaused_RestKeepsTicking_SameCards()
    {
        var (s, stack) = BandageWithHerbs(3, 120);
        var cards = E.RootCardCount(s);
        var e0 = stack.ExpiresAt!.Value;
        Start(s, ActionRequest.Craft("bandage"));
        var id = s.Main!.ActionId;
        var bound = s.Items.Single(i => i.ItemId == "herb" && i.ReservedBy == id);
        Assert.That((bound.Count, bound.ShelfPausedMinutes, bound.ExpiresAt), Is.EqualTo((1, (int?)120, (int?)e0)), "R＝E−S，原截止保留");
        var rest = s.Items.Single(i => i.ItemId == "herb" && i.ReservedBy == null);
        Assert.That((rest.Count, rest.ShelfPausedMinutes), Is.EqualTo((2, (int?)null)), "未投入的同堆余料不冻结");
        var cloth = s.Items.Single(i => i.ItemId == "cloth" && i.ReservedBy == id);
        Assert.That(cloth.ShelfPausedMinutes, Is.Null, "无期限原料不造 R");
        Assert.That(E.RootCardCount(s), Is.EqualTo(cards), "保护不改卡位");
    }

    /// <summary>Q701-A02：原料开工余 30 分钟，推进／暂停 60 分钟后取消仍余 30；架外放 10 分钟再重开取消只余 20；开工已坏仍拒绝。</summary>
    [Test]
    public void Q701_A02_CancelRestoresRemaining_RestartUsesCurrentRemaining()
    {
        var (s, _) = BandageWithHerbs(1, 30);
        Cfg.Recipes.Single(r => r.Id == "bandage");
        Start(s, ActionRequest.Craft("bandage"));
        Steps(s, 2);
        s.Main!.Status = ActionStatus.Paused;
        s.GameMinute += 50;   // 合法暂停期间（世界时间流逝）
        Assert.That(E.IsSpoiled(s, s.Items.Single(i => i.ItemId == "herb")), Is.False, "越过原截止仍不腐败");
        Assert.That(E.Cancel(s).Ok, Is.True);
        // B51c1（S08 B51 Q771 短稿＋保质至原句）：引句＋材料已退回当前地面＋实际恢复「{原料名}×{数量}保质至{日期时刻}」（仍余 30）＋已耗时间不退
        // BM11 改断言出处：S08 文首 2026-11-17 §Q882 用户新裁定（返料不附恢复保质说明）
        Assert.That(s.Log.Last().Text, Is.EqualTo($"已取消制作。材料已退回{E.ZoneName(s, s.Ground)}地面。已耗时间不退。"));
        var herb = s.Items.Single(i => i.ItemId == "herb");
        Assert.That((herb.ExpiresAt, herb.ShelfPausedMinutes, herb.ReservedBy), Is.EqualTo(((int?)(s.GameMinute + 30), (int?)null, (int?)null)), "C＋R：仍余 30");
        // 放在工作外 10 分钟正常倒计时，再重开以当时剩余 20 重新记录
        s.GameMinute += 10;
        Start(s, ActionRequest.Craft("bandage"));
        Assert.That(s.Items.Single(i => i.ItemId == "herb").ShelfPausedMinutes, Is.EqualTo(20));
        Assert.That(E.Cancel(s).Ok, Is.True);
        Assert.That(s.Items.Single(i => i.ItemId == "herb").ExpiresAt, Is.EqualTo(s.GameMinute + 20), "不增加剩余");
        // 开工已到期：原拒绝，不借先入料复活
        s.GameMinute += 20;
        Assert.That(E.CanStart(s, ActionRequest.Craft("bandage")), Is.Not.Null);
    }

    /// <summary>完成核销：受保护原料完成时消耗、不返 R；成品按完成时刻原保质计时、不继承冻结。</summary>
    [Test]
    public void Q701_A01_CompletionConsumesProtected_OutputNormalExpiry()
    {
        var (s, _) = BandageWithHerbs(1, 5);
        Start(s, ActionRequest.Craft("bandage"));
        Assert.That(Run(s), Is.EqualTo(SegmentStatus.Completed), "原截止早于完成也照常完成（不再假失败）");
        Assert.That(s.Items.Any(i => i.ItemId == "herb"), Is.False, "原料核销");
        var made = s.Items.Single(i => i.ItemId == "bandage");
        Assert.That(made.ShelfPausedMinutes, Is.Null, "成品不继承冻结");
    }

    /// <summary>Q701-A10：预估在副本里同源建立 R，不写回真实原料；临期原料不再判计划失败。</summary>
    [Test]
    public void Q701_A10_PreviewSimulatesProtection_NoRealWrite()
    {
        var (s, _) = BandageWithHerbs(1, 5);
        var before = SaveSerializer.Serialize(s);
        var p = E.Preview(s, ActionRequest.Craft("bandage"));
        Assert.That((p.Ok, p.PlanFailed), Is.EqualTo((true, false)), p.Reason);
        Assert.That(SaveSerializer.Serialize(s), Is.EqualTo(before));
        Assert.That(E.EstimateEndMinute(s), Is.Null, "没有在办行动");
    }

    /// <summary>
    /// 处理尸体普通取消（逐字稿 B51 表、Q722 拼接合同）：「已取消处理尸体。」＋「尸体已返还{地点}地面。」＋实际恢复句＋「没有产物。」＋「已耗时间不退。」（B51c1 按 S08 B51 Q771：一次已耗时间不退）。
    /// </summary>
    [Test]
    public void Q722_A01_ButcherNormalCancel_FullComposition()
    {
        var s = New();
        var carcass = E.CreateItems(s, "carcass_feral_dog", 1, s.Ground).Single();
        E.CreateItems(s, "stone_knife", 1, ItemInstance.Carried);
        Start(s, ActionRequest.Butcher(carcass.InstanceId));
        var r = carcass.ShelfPausedMinutes!.Value;
        Steps(s, 2);
        Assert.That(E.Cancel(s).Ok, Is.True);
        // BM11 改断言出处：S08 文首 2026-11-17 §Q882 用户新裁定（返料不附恢复保质说明）
        Assert.That(s.Log.Last().Text, Is.EqualTo($"已取消处理尸体。尸体已返还{E.ZoneName(s, s.Ground)}地面。没有产物。已耗时间不退。"));
        Assert.That(carcass.ExpiresAt, Is.EqualTo(s.GameMinute + r));
    }
    // ───────────── M958 Q720 两成品从完成起按自身保质 ─────────────

    private GameState Supply(params (string id, int n, int? left)[] give)
    {
        var s = New();
        E.DebugLearnAllKnowledge(s);
        E.DebugBuildAllWorkshops(s);
        foreach (var k in new[] { "crafting", "medicine", "cooking" }) s.Skills[k].Level = 5;
        var ids = give.Select(g => g.id).ToHashSet();
        s.Items.RemoveAll(i => ids.Contains(i.ItemId));
        foreach (var (id, n, left) in give) Make(s, id, n, s.Ground, left is int m ? s.GameMinute + m : null);
        return s;
    }

    /// <summary>Q720-A01：两原料合法临期，口粮棒真实完成 F 的新截止严格 F+10080；恢复剂每批两份严格 F+4320；不取旧 E 或 F+minR；预估写各自期限句、旧上限句撤去。</summary>
    [Test]
    public void Q720_A01_BarAndTonic_ShelfFromActualFinish()
    {
        var s = Supply(("dried_meat", 1, 5), ("dried_berries", 1, 600));
        var p = E.Preview(s, ActionRequest.Craft("ex_expedition_bar"));
        Assert.That(p.Notes, Has.Exactly(1).EqualTo("成品从完成起保质168小时。"));
        Assert.That(p.Notes.Any(n => n.Contains("不晚于原料")), Is.False, "旧 min_inputs 说明撤去");
        Start(s, ActionRequest.Craft("ex_expedition_bar"));
        Assert.That(Run(s), Is.EqualTo(SegmentStatus.Completed));
        var bar = s.Items.Single(i => i.ItemId == "expedition_bar");
        Assert.That((bar.Count, bar.ExpiresAt, bar.ShelfPausedMinutes), Is.EqualTo((1, (int?)(s.GameMinute + 10080), (int?)null)));

        var t = Supply(("herb_extract", 1, 10), ("clean_water", 1, null));
        Assert.That(E.Preview(t, ActionRequest.Craft("ex_emergency_tonic")).Notes, Has.Exactly(1).EqualTo("成品从完成起保质72小时。"));
        Start(t, ActionRequest.Craft("ex_emergency_tonic"));
        Assert.That(Run(t), Is.EqualTo(SegmentStatus.Completed));
        var tonic = t.Items.Where(i => i.ItemId == "emergency_tonic").ToList();
        Assert.That(tonic.Sum(i => i.Count), Is.EqualTo(2), "原每批 2 份不变");
        Assert.That(tonic.All(i => i.ExpiresAt == t.GameMinute + 4320), Is.True);
    }

    /// <summary>Q720-A02：开工已坏仍拒绝；其他配方期限不变（草药绷带无期限仍无期限）。</summary>
    [Test]
    public void Q720_A02_ExpiredInputStillRefused()
    {
        var s = Supply(("dried_meat", 1, 5), ("dried_berries", 1, 600));
        s.GameMinute += 5;
        Assert.That(E.CanStart(s, ActionRequest.Craft("ex_expedition_bar")), Is.Not.Null, "开工已到期原拒绝");
        Assert.That(Cfg.Recipes.Count(r => r.ShelfRule != null), Is.EqualTo(2), "只两条配方");
    }
    // ───────────── M959 预估说明：Q724 条件说明、处理尸体三句 ─────────────

    /// <summary>Q724-A01：只有本次将绑定有期限原料才显示一次保护说明；无期限配方不显示；预览不让真实原料取得保护。</summary>
    [Test]
    public void Q724_A01_PreviewNoteOnlyWhenBindingPerishable()
    {
        var (s, herb) = BandageWithHerbs(2, 300);
        var p = E.Preview(s, ActionRequest.Craft("bandage", batchCount: 2));
        // BM11 改断言出处：S08 文首 2026-11-17 §Q882 用户新裁定（返料不附恢复保质说明）：通用生产说明保留“加工时原料暂停保质。”
        Assert.That(p.Notes.Count(n => n == "加工时原料暂停保质。"), Is.EqualTo(1));
        Assert.That(herb.ShelfPausedMinutes, Is.Null, "预览不保护");

        var t = New();
        t.Items.RemoveAll(i => i.ItemId is "herb");
        E.DebugGive(t, "cloth", 2);
        foreach (var i in E.CreateItems(t, "herb", 1, ItemInstance.Carried)) i.ExpiresAt = null;
        Assert.That(E.Preview(t, ActionRequest.Craft("bandage")).Notes, Has.None.EqualTo(GameEngine.ProductionShelfNote), "无期限原料不造 R，不显示说明");
    }

    /// <summary>Q722（逐字稿 B51 表）：处理尸体预估改 butcherNote＋“尸体剩余保质：{时长}”，旧假失败句撤去；开工保护后同尸体读“保质暂停，剩余{时长}”；保护说明一次。</summary>
    [Test]
    public void Q722_ButcherPreviewSentences()
    {
        var s = New();
        var carcass = E.CreateItems(s, "carcass_feral_dog", 1, s.Ground).Single();
        E.CreateItems(s, "stone_knife", 1, ItemInstance.Carried);
        var left = carcass.ExpiresAt!.Value - s.GameMinute;
        var p = E.Preview(s, ActionRequest.Butcher(carcass.InstanceId));
        Assert.That(p.Notes, Does.Contain("按制造消耗，制造熟练不加速；取消退回原尸体，无产物。"));
        Assert.That(p.Notes, Does.Contain($"尸体剩余保质：{Clock.Duration(left)}"));
        Assert.That(p.Notes.Count(n => n == GameEngine.ProductionShelfNote), Is.EqualTo(1));
        Assert.That(p.Notes.Any(n => n.Contains("保质期不刷新") || n.Contains("完成时已腐败") || n.Contains("腐败")), Is.False);
        Start(s, ActionRequest.Butcher(carcass.InstanceId));
        var q = E.Preview(s, ActionRequest.Butcher(carcass.InstanceId));
        Assert.That(q.Notes, Does.Contain($"保质暂停，剩余{Clock.Duration(left)}"));
    }
    // ───────────── M960 工具本单保障（Q701.3、Q726） ─────────────

    /// <summary>Q726-A01：尸体刀 D=0.01 可开工；本单保障登记；成功扣 1 后归 0 且产物一次；完成后 0 刀不能开下一单；取消不扣成功磨损。</summary>
    [Test]
    public void Q726_A01_ButcherKnifeTinyPositive_CompletesOnce_ThenBroken()
    {
        var s = New();
        var c1 = E.CreateItems(s, "carcass_feral_dog", 1, s.Ground).Single();
        var knife = E.CreateItems(s, "stone_knife", 1, ItemInstance.Carried).Single();
        knife.Durability = 0.01;
        Assert.That(E.CanStart(s, ActionRequest.Butcher(c1.InstanceId)), Is.Null, "D>0 即可开工（显示取整 0 不代替真值）");
        Start(s, ActionRequest.Butcher(c1.InstanceId));
        Assert.That((s.Main!.ToolGuardId, s.Main.ToolGuardSpent), Is.EqualTo(((int?)knife.InstanceId, false)));
        Steps(s, 2);
        Assert.That(E.Cancel(s).Ok, Is.True);
        Assert.That(knife.Durability, Is.EqualTo(0.01).Within(1e-12), "取消不扣成功磨损");
        Start(s, ActionRequest.Butcher(c1.InstanceId));
        Assert.That(Run(s), Is.EqualTo(SegmentStatus.Completed));
        Assert.That(knife.Durability, Is.EqualTo(0), "成功磨损归 0、不负");
        Assert.That(s.Items.Where(i => i.ItemId == "raw_meat").Sum(i => i.Count), Is.EqualTo(2), "成果一次");
        var c2 = E.CreateItems(s, "carcass_feral_dog", 1, s.Ground).Single();
        Assert.That(E.CanStart(s, ActionRequest.Butcher(c2.InstanceId)), Is.EqualTo("这件工具已损坏，请更换或维修。"), "保障随单结束");
    }

    /// <summary>Q726-A02：全 0 刀斧与完全无刀斧分句；正 D 被占用不误判全坏（沿原句）。</summary>
    [Test]
    public void Q726_A02_AllZeroVsNone_OccupiedPositiveNotBroken()
    {
        var s = New();
        var carcass = E.CreateItems(s, "carcass_feral_dog", 1, s.Ground).Single();
        Assert.That(E.CanStart(s, ActionRequest.Butcher(carcass.InstanceId)), Does.StartWith("需要随身有效的"), "完全没有");
        var k0 = E.CreateItems(s, "stone_knife", 1, ItemInstance.Carried).Single();
        k0.Durability = 0;
        Assert.That(E.CanStart(s, ActionRequest.Butcher(carcass.InstanceId)), Is.EqualTo("这件工具已损坏，请更换或维修。"));
        var k1 = E.CreateItems(s, "stone_knife", 1, ItemInstance.Carried).Single();
        k1.Durability = 5;
        k1.ReservedBy = 999;
        Assert.That(E.CanStart(s, ActionRequest.Butcher(carcass.InstanceId)), Does.StartWith("需要随身有效的"), "正 D 被占用不是全坏");
        k1.ReservedBy = null;
        Assert.That(E.CanStart(s, ActionRequest.Butcher(carcass.InstanceId)), Is.Null);
    }

    /// <summary>Q701.3：续做复核认本单保障（自磨归 0 且已登记），外部归 0 不借保障。</summary>
    [Test]
    public void Q701_3_ResumeAcceptsGuardOnlyForOwnWear()
    {
        var s = New();
        var carcass = E.CreateItems(s, "carcass_feral_dog", 1, s.Ground).Single();
        var knife = E.CreateItems(s, "stone_knife", 1, ItemInstance.Carried).Single();
        Start(s, ActionRequest.Butcher(carcass.InstanceId));
        var a = s.Main!;
        Steps(s, 1);
        a.Status = ActionStatus.Paused;
        knife.Durability = 0.01;
        Assert.That(E.ResumeBlockReason(s), Is.Null, "D>0 不足一次磨损也能续做（原 HasWear 门槛撤去）");
        knife.Durability = 0;   // 外部归 0
        Assert.That(E.ResumeBlockReason(s), Does.StartWith("需要随身有效的"), "外部损毁不借保障");
        a.ToolGuardSpent = true;   // 本单自磨归 0 的记录
        Assert.That(E.ResumeBlockReason(s), Is.Null, "本单保障登记的这件自磨归 0 仍可走完本单");
        Assert.That(GameEngine.ProductionToolUsable(new ItemInstance { ItemId = "x", Durability = null }), Is.True, "无耐久工具沿原许可");
    }
    // ───────────── M961 Q724 显示 ─────────────

    /// <summary>Q724-A01／A02（核心侧）：保护份自成真实批次（BatchKey 带 R），不读原截止当腐败；释放后显示 C+R 正常期限、不残留暂停。</summary>
    [Test]
    public void Q724_A02_ProtectedPortionOwnBatch_ReleasedShowsNormalExpiry()
    {
        var (s, _) = BandageWithHerbs(3, 30);
        Start(s, ActionRequest.Craft("bandage"));
        s.GameMinute += 60;   // 越过原截止（模拟合法暂停）
        var batches = E.BatchesOf(s, ItemInstance.Carried, "herb");
        Assert.That(batches.Count, Is.EqualTo(2), "保护 1 份与未投入 2 份分批");
        var prot = batches.Single(b => b.Stacks[0].ShelfPausedMinutes != null);
        Assert.That((prot.Count, prot.Spoiled, prot.Stacks[0].ShelfPausedMinutes), Is.EqualTo((1, false, (int?)30)));
        Assert.That(batches.Single(b => b != prot).Spoiled, Is.True, "未投入余料照常腐败");
        Assert.That(GameEngine.ShelfPausedText(30), Is.EqualTo("保质暂停，剩余30分钟"));
        Assert.That(GameEngine.ShelfPausedBatchesText(2), Is.EqualTo("保质暂停 · 2批"));
        s.Main!.Status = ActionStatus.Paused;
        Assert.That(E.Cancel(s).Ok, Is.True);
        Assert.That(s.Items.Where(i => i.ItemId == "herb").All(i => i.ShelfPausedMinutes == null), Is.True, "释放后不残留暂停");
    }

    /// <summary>Q724（界面源码）：卡副标、批次层、详情批次、操作窗批次、装备速览、制作批次、安全屋都先读保护 R，不写“不会腐败”；混合沿原副标（Q773），不接“含保质暂停×”。</summary>
    [Test]
    public void Q724_WebReadsShelfPause()
    {
        var ui = Web("Services", "Ui.cs");
        Assert.That(ui, Does.Contain("if (i.ShelfPausedMinutes is int r && !e.IsSpoiled(s, i)) return GameEngine.ShelfPausedText(r);"));
        Assert.That(ui, Does.Contain("public static string BatchFresh(").And.Contain("public static string? PausedCardLine("));
        var shelf = Web("Components", "ItemShelf.razor");
        Assert.That(shelf, Does.Contain("Ui.BatchFresh(e, s, b.Stacks, b.ExpiresAt, b.Spoiled)").And.Contain("Ui.PausedCardLine("));
        Assert.That(Web("Components", "ItemDetail.razor"), Does.Contain("GroupBy(i => (i.ExpiresAt, i.ShelfPausedMinutes))").And.Contain("Ui.BatchFresh(e, s, b.Stacks, b.ExpiresAt, false)"));
        Assert.That(Web("Components", "CardActions.razor"), Does.Contain("Ui.BatchFresh(G.E, G.S, b.Stacks, b.ExpiresAt, b.Spoiled)"));
        Assert.That(Web("Components", "GearPeekDetail.razor"), Does.Contain("it.ShelfPausedMinutes != null ? Ui.FreshOf"));
        // B51c1 改期望（出处 S06／S14 B51 §Q773 选 B）：部分暂停副标只写「含保质暂停」（见 B51c1Tests.Q773_A01），任何位置都不接「×{数量}」
        foreach (var f in new[] { "ItemShelf.razor", "ItemDetail.razor", "CardActions.razor", "BattleSupplies.razor" })
            Assert.That(Web("Components", f), Does.Not.Contain("含保质暂停×"), f + "：Q773 选 B 不接数量");
    }
    // ───────────── M962 存档 53→54 ─────────────

    /// <summary>
    /// Q701-A09：旧在办制作的有效未过期输入从升级 U 起 R＝旧E−U（不补偿已耗）；U 时已到期的不保护（原路径）；无期限不造 R；
    /// 已有可信 R 不重算；处理尸体正耐久工具登记本单保障；重复读档不再加。
    /// </summary>
    [Test]
    public void Q701_A09_Migrate53To54_ProtectInProgressInputs_Idempotent()
    {
        var s = New();
        s.Items.RemoveAll(i => i.ItemId is "cloth" or "herb");
        E.DebugGive(s, "cloth", 2);
        Make(s, "herb", 1, ItemInstance.Carried, s.GameMinute + 100);
        Start(s, ActionRequest.Craft("bandage"));
        var id = s.Main!.ActionId;
        var herb = s.Items.Single(i => i.ItemId == "herb" && i.ReservedBy == id);
        var cloth = s.Items.Single(i => i.ItemId == "cloth" && i.ReservedBy == id);
        herb.ShelfPausedMinutes = null;   // 旧档：开工时没有保护
        s.GameMinute += 40;               // 开工到升级已耗 40 分钟
        var e0 = herb.ExpiresAt!.Value;
        var t = SaveSerializer.Deserialize(AsSchema(s, "53"), Cfg);
        var h = t.Items.Single(i => i.InstanceId == herb.InstanceId);
        Assert.That((h.ShelfPausedMinutes, h.ExpiresAt), Is.EqualTo(((int?)(e0 - t.GameMinute), (int?)e0)), "R＝旧E−U，原截止保留");
        Assert.That(t.Items.Single(i => i.InstanceId == cloth.InstanceId).ShelfPausedMinutes, Is.Null, "无期限不造 R");
        var t2 = SaveSerializer.Deserialize(SaveSerializer.Serialize(t), Cfg);
        Assert.That(t2.Items.Single(i => i.InstanceId == herb.InstanceId).ShelfPausedMinutes, Is.EqualTo(e0 - t.GameMinute), "重复读档不变");
        // 已到期的旧输入不保护（原路径）；已有可信 R 不重算
        var u = SaveSerializer.Deserialize(AsSchema(s, "53", o =>
        {
            foreach (var it in o["Items"]!)
                if ((int)it["InstanceId"]! == herb.InstanceId) it["ExpiresAt"] = s.GameMinute - 1;
        }), Cfg);
        Assert.That(u.Items.Single(i => i.InstanceId == herb.InstanceId).ShelfPausedMinutes, Is.Null, "U 时已到期不复活");
        herb.ShelfPausedMinutes = 77;
        var w = SaveSerializer.Deserialize(AsSchema(s, "53"), Cfg);
        Assert.That(w.Items.Single(i => i.InstanceId == herb.InstanceId).ShelfPausedMinutes, Is.EqualTo(77), "可信 R 不重算");
    }

    [Test]
    public void Q701_A09_Migrate53To54_ButcherToolGuard()
    {
        var s = New();
        var carcass = E.CreateItems(s, "carcass_feral_dog", 1, s.Ground).Single();
        var knife = E.CreateItems(s, "stone_knife", 1, ItemInstance.Carried).Single();
        Start(s, ActionRequest.Butcher(carcass.InstanceId));
        s.Main!.ToolGuardId = null;
        carcass.ShelfPausedMinutes = null;
        knife.Durability = 0.4;
        var d = knife.Durability;
        var t = SaveSerializer.Deserialize(AsSchema(s, "53"), Cfg);
        Assert.That((t.Main!.ToolGuardId, t.Main.ToolGuardSpent), Is.EqualTo(((int?)knife.InstanceId, false)));
        Assert.That(t.Items.Single(i => i.InstanceId == knife.InstanceId).Durability, Is.EqualTo(d), "不改耐久");
        Assert.That(t.Items.Single(i => i.InstanceId == carcass.InstanceId).ShelfPausedMinutes, Is.Not.Null, "尸体从升级起保护");
        knife.Durability = 0;
        var z = SaveSerializer.Deserialize(AsSchema(s, "53"), Cfg);
        Assert.That(z.Main!.ToolGuardId, Is.Null, "0 耐久无可信保障不补资格");
    }
}

/// <summary>B51a 中撤退离地保留（Q471）的恢复句分组（Q723-A01）：同目的地／物主／卡侧内同名同精确截止合计，不同截止或不同目的地分句。</summary>
// B51c1 改期望（出处 S08 文首 2026-11-16 B51 §Q771 拼接顺序与短稿、02 Q874 开发方建议 A 暂用）：撤退保留／普通退出预告引句「退出将放弃本次{动作}。」→「退出将取消{动作}。」（维护→维修，现场修补用原名）、
// 结果尾句「已耗时间保留。」→「已耗时间不退。」、删「未生成半成品。」「不会生成半成品。」；处理尸体沿 Q722 引句，只改尾句。锁定自动中止仍「已耗时间保留。」。
public class B51aKeptTests : B41bTestBase
{
    [Test]
    public void Q723_A01_KeptExit_RestoreGroupedByDestinationAndExactExpiry()
    {
        var s = Field(3);
        var field = E.LocName("field");
        var id = KeptAtField(s, g =>
        {
            g.Items.RemoveAll(i => i.ItemId is "cloth" or "herb" or "bandage");
            E.DebugGive(g, "cloth", 3);
            var e1 = g.GameMinute + 600;
            foreach (var i in E.CreateItems(g, "herb", 2, ItemInstance.Carried)) i.ExpiresAt = e1;
            foreach (var i in E.CreateItems(g, "herb", 1, "field")) i.ExpiresAt = e1 + 60;
            return ActionRequest.Craft("bandage", batchCount: 3);
        });
        var a = s.Main!;
        var herbs = s.Items.Where(i => i.ItemId == "herb" && i.ReservedBy == id).ToList();
        Assert.That(herbs.Sum(i => i.Count), Is.EqualTo(3));
        var rc = herbs.Where(i => a.ReserveSources![i.InstanceId] == ItemInstance.Carried).Select(i => i.ShelfPausedMinutes!.Value).Distinct().Single();
        var rg = herbs.Single(i => a.ReserveSources![i.InstanceId] == "field").ShelfPausedMinutes!.Value;
        var name = E.ItemName("herb");
        // BM11 改断言出处：S08 文首 2026-11-17 §Q882 用户新裁定（返料不附恢复保质说明）：预告只保留 Q471 返还去向句
        Assert.That(E.ActionProgress(s)!.ExitNote, Is.EqualTo($"退出将取消制作。随身预留材料解除预留后留在随身，地面预留材料会返还{field}地面。已耗时间不退。"));
        var before = s.Items.Where(i => i.ItemId == "herb").Select(i => (i.ExpiresAt, i.ShelfPausedMinutes)).ToList();
        Assert.That(s.Items.Where(i => i.ItemId == "herb").Select(i => (i.ExpiresAt, i.ShelfPausedMinutes)).ToList(), Is.EqualTo(before));
        Assert.That(E.Cancel(s).Ok, Is.True);
        var text = s.Log.Last().Text;
        Assert.That(text, Does.StartWith("已取消制作。随身预留材料已解除预留，仍在随身；地面预留材料已返还" + field + "地面。"));
        // BM11 改断言出处：S08 文首 2026-11-17 §Q882 用户新裁定（返料不附恢复保质说明）
        Assert.That(text, Is.EqualTo("已取消制作。随身预留材料已解除预留，仍在随身；地面预留材料已返还" + field + "地面。已耗时间不退。"));
        // 真实 C＋R 照常：随身两份按 rc、地面一份按 rg
        Assert.That(s.Items.Where(i => i.ItemId == "herb" && i.ExpiresAt == s.GameMinute + rc).Sum(i => i.Count), Is.EqualTo(2));
        Assert.That(s.Items.Where(i => i.ItemId == "herb" && i.ExpiresAt == s.GameMinute + rg).Sum(i => i.Count), Is.EqualTo(1));
    }
}
