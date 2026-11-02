using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 批次 B13d（R133、R134）：策划答复 Q130 选 A、Q133 选 A（完成口径保持现状）与 Q132 选 A（旧档主动点交谈接续）的核对测试
/// （S15/A10 Q128—Q133 本轮章、prologue-adjudications.json completionRules/legacyPolicy；验收 TUT-Q130-01、TUT-Q132-01、TUT-Q133-01/02）。
/// 与现状一致，不改行为。
/// </summary>
public class B13dCompletionTests : TestBase
{
    private const int D5 = 6240;

    private GameState Won()
    {
        var s = NewStory();
        ReadDialogues(s);
        E.DebugPrologue(s, "won");
        ReadDialogues(s);
        return s;
    }

    private void Travel(GameState s, string to)
    {
        Start(s, ActionRequest.Travel(to));
        Assert.That(Run(s), Is.EqualTo(SegmentStatus.Completed));
        ReadDialogues(s);
        Assert.That(s.CurrentLocation, Is.EqualTo(to));
    }

    private void Use(GameState s, string itemId)
    {
        Start(s, ActionRequest.UseItem(itemId));
        Run(s);
        ReadDialogues(s);
    }

    // ───────────── TUT-Q130-01 ─────────────

    /// <summary>TUT-Q130-01：自制的熟块根、自己烧的净水，只要实际提高主角饱食或水分就完成 PRO02；满值吃喝、只预览都不算；不限开局物品、不发奖励。</summary>
    [Test]
    public void TUT_Q130_01_AnySourceRealGain_FullOrPreviewDoesNot()
    {
        var s = Won();
        SetStats(s, 20, 100, 100, 18, 90, 80);
        E.DebugGive(s, "cooked_root", 1);
        Assert.That(E.Preview(s, ActionRequest.UseItem("cooked_root")).Ok, Is.True);
        Assert.That(GameEngine.TaskDone(s, "PRO02"), Is.False, "只预览不算");
        s.Items.RemoveAll(i => i.ItemId == "cooked_root");
        E.DebugGive(s, "clean_water", 1);
        Use(s, "clean_water");
        Assert.That(GameEngine.TaskDone(s, "PRO02"), Is.False, "饱食与水分都满：吃喝没有实际提高，不算");

        var t = Won();
        SetStats(t, 20, 50, 100, 18, 90, 80);
        t.Items.RemoveAll(i => i.ItemId is "canned_food" or "clean_water");
        E.DebugGive(t, "cooked_root", 1);
        var items = t.Items.Sum(i => i.Count);
        Use(t, "cooked_root");
        Assert.That(GameEngine.TaskDone(t, "PRO02"), Is.True, "自制熟块根实际提高饱食");
        Assert.That(t.Prologue.Tasks["PRO02"].EventId, Does.StartWith("use:"));
        Assert.That(t.Items.Sum(i => i.Count), Is.EqualTo(items - 1), "无额外教程奖励");

        var u = Won();
        SetStats(u, 20, 100, 60, 18, 90, 80);
        u.Items.RemoveAll(i => i.ItemId is "canned_food" or "clean_water");
        E.DebugGive(u, "clean_water", 1);
        Use(u, "clean_water");
        Assert.That(GameEngine.TaskDone(u, "PRO02"), Is.True, "饱食满、水分有提高也算（任一项）");
    }

    // ───────────── TUT-Q133-01 ─────────────

    /// <summary>TUT-Q133-01：PRO06 = 到访过溪流 AND 任一合法水源采集产出脏水，先后不限（别处水源也算）；捡地上的脏水不算取水。PRO01 选卡展开预览即完成（随身卡同样由界面上报）。</summary>
    [Test]
    public void TUT_Q133_01_Pro06_EitherOrder_AnySource_PickupNotGather_Pro01()
    {
        // 先在别处水源取水、后到访溪流
        var s = Won();
        E.DebugMapTeleport(s, "w12_canal");
        ReadDialogues(s);
        SetStats(s, 20, 90, 90, 18, 90, 80);
        Start(s, ActionRequest.Gather("w12_canal_water"));
        Run(s);
        ReadDialogues(s);
        Assert.That(s.Prologue.DrewWater, Is.True);
        Assert.That(GameEngine.TaskDone(s, "PRO06"), Is.False, "还没到访溪流");
        Assert.That(E.StepText(s, E.ProTask("PRO06")!), Is.EqualTo("沿已知道路到溪流浅滩确认水源。"));
        E.DebugMapTeleport(s, "stream");
        ReadDialogues(s);
        Assert.That(GameEngine.TaskDone(s, "PRO06"), Is.True, "先取水后到访也完成");

        // 先到访溪流、后在溪流取水
        var t = Won();
        E.DebugMapTeleport(t, "stream");
        ReadDialogues(t);
        Assert.That(GameEngine.TaskDone(t, "PRO06"), Is.False, "只到访不算");
        // 捡地上的脏水不冒充采水
        E.DebugGive(t, "dirty_water", 1, "stream");
        var w = t.Items.First(i => i.ItemId == "dirty_water" && i.Owner == "stream");
        Assert.That(E.DoInstant(t, InstantRequest.Pickup(w.InstanceId, 1)).Ok, Is.True);
        Assert.That(t.Prologue.DrewWater, Is.False);
        Assert.That(GameEngine.TaskDone(t, "PRO06"), Is.False, "拾取地上的水不算取水");
        SetStats(t, 20, 90, 90, 18, 90, 80);
        Start(t, ActionRequest.Gather("water"));
        Run(t);
        ReadDialogues(t);
        Assert.That(GameEngine.TaskDone(t, "PRO06"), Is.True);

        // PRO01：界面选中地面或随身物资卡并展开预览即上报（0 分钟）
        var u = NewStory();
        ReadDialogues(u);
        var m = u.GameMinute;
        E.NoteObjectInspected(u);
        Assert.That(GameEngine.TaskDone(u, "PRO01"), Is.True);
        Assert.That(u.GameMinute, Is.EqualTo(m));
    }

    // ───────────── TUT-Q133-02 ─────────────

    /// <summary>TUT-Q133-02：手工木盾完成 PRO10 但不完成伤害武器 PRO03；箭矢、绷带、工坊成品不算 PRO10；到访村庄后真实返营完成 PRO14，不查营地出发记录。</summary>
    [Test]
    public void TUT_Q133_02_Pro10_ShieldYes_ArrowsBandageWorkshopNo_Pro14NoDepartureRecord()
    {
        var s = Won();
        E.DebugSetSkillLevels(s, 10, false);
        SetStats(s, 20, 90, 90, 18, 90, 80);
        foreach (var id in new[] { "make_arrows", "bandage" })
        {
            foreach (var kv in E.Recipe(id).AllInputs) E.DebugGive(s, kv.Key, kv.Value * 2, "camp");
            Start(s, ActionRequest.Craft(id));
            Run(s);
            ReadDialogues(s);
            Assert.That(s.Items.Any(i => E.Recipe(id).Outputs.ContainsKey(i.ItemId)), Is.True, id + " 实际做出");
            Assert.That(GameEngine.TaskDone(s, "PRO10"), Is.False, id + " 不是工具或武器");
        }

        // 工坊配方（工作台）做出武器也不算手工 PRO10
        E.DebugLearnAllKnowledge(s);
        s.Locations["camp"].Facility.Workshops.Add("workbench");
        foreach (var kv in E.Recipe("make_machete").AllInputs) E.DebugGive(s, kv.Key, kv.Value, "camp");
        SetStats(s, 20, 90, 90, 18, 90, 80);
        var r = E.Start(s, ActionRequest.Craft("make_machete"));
        Assert.That(r.Ok, Is.True, r.Reason);
        Run(s);
        ReadDialogues(s);
        Assert.That(s.Items.Any(i => i.ItemId == "machete"), Is.True);
        Assert.That(GameEngine.TaskDone(s, "PRO10"), Is.False, "工坊成品不算");

        // 手工木盾：完成 PRO10，不冒充伤害武器 PRO03
        foreach (var kv in E.Recipe("make_wooden_shield").AllInputs) E.DebugGive(s, kv.Key, kv.Value, "camp");
        SetStats(s, 20, 90, 90, 18, 90, 80);
        Start(s, ActionRequest.Craft("make_wooden_shield"));
        Run(s);
        ReadDialogues(s);
        Assert.That(s.Prologue.Tasks["PRO10"].EventId, Does.StartWith("craft:"), "木盾算手工工具/武器");
        var shield = s.Items.First(i => i.ItemId == "wooden_shield");
        E.Equip(s, GameState.PlayerId, shield.InstanceId, EquipSlots.Weapon1);
        Assert.That(GameEngine.TaskDone(s, "PRO03"), Is.False, "盾不是伤害型武器");

        // PRO14：开发传送到村庄（按实际到达处理）后沿路真实走回营地；从未从营地“出发”过也完成
        var t = Won();
        E.DebugUnlockEdge(t, "field__village");
        E.DebugMapTeleport(t, "village");
        ReadDialogues(t);
        Assert.That(GameEngine.TaskDone(t, "PRO14"), Is.False);
        Travel(t, "field");
        Assert.That(GameEngine.TaskDone(t, "PRO14"), Is.False, "到荒地不算");
        Travel(t, "camp");
        Assert.That(GameEngine.TaskDone(t, "PRO14"), Is.True, "到访后真实返营");
    }

    // ───────────── TUT-Q132-01 ─────────────

    /// <summary>
    /// TUT-Q132-01：Legacy 与迁移档来客满足原条件登记可见，但不自动弹抵达对白、不自动弹 PRO16 短句；人在外只一次非阻断通知、不传送不中断；
    /// 不点交谈不写 visitor_met/camp_route_known；主动交谈才接续，不补饱饱/奖励。新局原抵达剧情不变。
    /// </summary>
    [Test]
    public void TUT_Q132_01_Legacy_TalkToContinue_NoAutoArrival()
    {
        var s = New();
        E.DebugTeleport(s, "field");
        s.GameMinute = D5 + 60;
        var items = s.Items.Sum(i => i.Count);
        E.PrologueBoundary(s);
        Assert.That(GameEngine.VisitorRegistered(s), Is.True);
        Assert.That(s.CurrentLocation, Is.EqualTo("field"), "不传送");
        Assert.That(E.DialogueLocked(s), Is.False);
        Assert.That(s.Log.Count(l => l.Text.Contains("临时营地出现了来客")), Is.EqualTo(1));
        E.PrologueBoundary(s);
        Assert.That(s.Log.Count(l => l.Text.Contains("临时营地出现了来客")), Is.EqualTo(1), "只通知一次");
        E.DebugTeleport(s, "camp");
        E.PrologueBoundary(s);
        Assert.That(E.DialogueLocked(s), Is.False, "到营地也不自动弹抵达对白或 PRO16 短句");
        Assert.That(E.NpcsHere(s), Has.Count.EqualTo(1), "人物卡可见");
        Assert.That(E.CurrentHint(s)!.Value.TaskId, Is.EqualTo("PRO16"), "仍有中性系统提示");
        Assert.That(s.Progress.Flags.Contains(GameEngine.FactVisitorMet) || s.Progress.Flags.Contains(GameEngine.FactCampRouteKnown), Is.False, "不点不写路线");
        Assert.That(E.TalkNpc(s, "npc_cheng_wang").Ok, Is.True);
        ReadDialogues(s);
        Assert.That(s.Progress.Flags.Contains(GameEngine.FactCampRouteKnown), Is.True, "主动交谈才接续");
        Assert.That(s.Story.DialogueLog.SelectMany(d => d.Lines).Any(l => l.Speaker == "饱饱"), Is.False, "不补饱饱");
        Assert.That(s.Items.Sum(i => i.Count), Is.EqualTo(items), "不补奖励");

        // 新局原抵达剧情不变：营地空闲时自动播抵达对白
        var n = Won();
        n.GameMinute = D5;
        E.PrologueBoundary(n);
        Assert.That(E.ActiveDialogue(n)?.EventId, Is.EqualTo("visitor_arrival_camp"));
    }
}
