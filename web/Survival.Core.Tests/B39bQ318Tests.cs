using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 批次 B39b（R130）：Q318 事项盘忙碌反馈清理。
/// 规则出处：05 世界观与叙事/N02 附录 §2「2026-11-10 Q318 · 事项盘忙碌反馈清理」；00 总则/02 开发方待确认问题 Q318（选 A）。
/// 归属与清理判定在 <see cref="BusyNoticeTracker"/>（网页壳 GameSession 每次给反馈位赋值时 Rearm、每次刷新前 Observe），
/// 右栏去重用 <see cref="BusyNoticeTracker.RightColumnNotice"/>；壳层接线以源码检查。
/// </summary>
public class B39bQ318Tests : TestBase
{
    private const string Busy = "当前行动未结束，暂时无法办理。";
    private const string Luo = "npc_luo_zheng";
    private const string Other = "到营区工坊后才能办理。";

    private GameState Registered()
    {
        var s = New();
        s.Progress.Flags.UnionWith(new[] { "registration_authorized", "hub_registered" });
        s.GameMinute = At(2, 9, 0);
        s.SpawnNextCheck = int.MaxValue;
        s.PatrolNextCheck = int.MaxValue;
        E.DebugMapTeleport(s, Cfg.City.Npcs.Single(n => n.NpcId == Luo).LocationId);
        ReadDialogues(s);
        return s;
    }

    /// <summary>睡眠中在罗铮事项盘点「接取」得到的反馈（Q317 句），模拟壳层：赋值即 Rearm，随后刷新 Observe。</summary>
    private string Trigger(GameState s, BusyNoticeTracker t)
    {
        var why = E.CityPanelQuests(s, Luo).First(q => q.AcceptHere).AcceptBlocked;
        Assert.That(why, Is.EqualTo(Busy), "睡眠中事项盘接取受阻");
        t.Rearm();
        Assert.That(t.Observe(why, s), Is.False, "触发时有行动占用，保留");
        return why!;
    }

    /// <summary>Q318-A01：睡眠中触发受阻反馈，睡眠自然完成后下一次刷新清掉旧句；进行中每次刷新都保留。</summary>
    [Test]
    public void Q318_A01_Sleep_NaturalCompletion_Clears()
    {
        var s = Registered();
        var t = new BusyNoticeTracker();
        Start(s, ActionRequest.Sleep(Cfg.Actions.SleepMinutes.Min()));
        var id = s.Main!.ActionId;
        var notice = Trigger(s, t);
        Assert.That(t.Owners, Is.EqualTo(new[] { id }), "依附触发它的睡眠");
        E.Advance(s);
        Assert.That(t.Observe(notice, s), Is.False, "睡眠仍在进行，保留");
        Run(s);
        Assert.That(s.Main, Is.Null);
        Assert.That(t.Observe(notice, s), Is.True, "自然完成后清除");
        Assert.That(E.CityPanelQuests(s, Luo).First(q => q.AcceptHere).AcceptBlocked, Is.Null, "按钮已恢复可用");
    }

    /// <summary>Q318-A01：睡眠中触发受阻反馈后取消睡眠，实际停止后清掉旧句。</summary>
    [Test]
    public void Q318_A01_Sleep_Cancel_Clears()
    {
        var s = Registered();
        var t = new BusyNoticeTracker();
        Start(s, ActionRequest.Sleep(Cfg.Actions.SleepMinutes.Min()));
        var notice = Trigger(s, t);
        Assert.That(E.RequestExit(s).Ok, Is.True);
        Assert.That(s.Main, Is.Null, "睡眠退出在段边界立即停止");
        Assert.That(t.Observe(notice, s), Is.True, "取消后清除");
    }

    /// <summary>不可中途退出的行动（B41c2：点火等开工 5 分钟行动的退出请求被拒）仍占用，保留；本段结束、行动停止后清除。</summary>
    [Test]
    public void Q318_ExitDeferred_KeepsUntilStopped()
    {
        var s = New();
        var t = new BusyNoticeTracker();
        var lighter = E.IgniteToolOptions(s).FirstOrDefault(o => o.Usable);
        if (lighter == null) { E.CreateItems(s, "lighter", 1, ItemInstance.Carried); lighter = E.IgniteToolOptions(s).First(o => o.Usable); }
        Start(s, ActionRequest.Ignite(lighter.InstanceId));
        t.Rearm();
        Assert.That(t.Observe(Busy, s), Is.False);
        var exit = E.RequestExit(s);
        Assert.That((exit.Ok, exit.Reason), Is.EqualTo((false, "该行动不可中途取消")), "点火开工 5 分钟，不可中途退出");
        Assert.That(t.Observe(Busy, s), Is.False, "行动仍占用，保留");
        Run(s);
        Assert.That(s.Main, Is.Null);
        Assert.That(t.Observe(Busy, s), Is.True, "停止后清除");
    }

    /// <summary>新行动不继承旧反馈：旧行动结束后紧接着开始新行动（中间没有刷新），下一次刷新仍清掉旧句；新行动中再次触发则依附新行动。</summary>
    [Test]
    public void Q318_NewActionDoesNotInherit()
    {
        var s = Registered();
        var t = new BusyNoticeTracker();
        Start(s, ActionRequest.Wait(30));
        var notice = Trigger(s, t);
        Assert.That(E.Cancel(s).Ok, Is.True);
        Start(s, ActionRequest.Wait(30));
        Assert.That(t.Observe(notice, s), Is.True, "新行动不继承旧反馈");
        var id = s.Main!.ActionId;
        Trigger(s, t);
        Assert.That(t.Owners, Is.EqualTo(new[] { id }), "再次触发依附新行动");
    }

    /// <summary>补给子行动：触发时主行动暂停、子行动进行，两者都算占用；子行动结束而主行动仍暂停占用时保留，主行动也结束后清除。</summary>
    [Test]
    public void Q318_SubAction_FollowsActualOccupancy()
    {
        var s = Registered();
        var t = new BusyNoticeTracker();
        Start(s, ActionRequest.Wait(30));
        s.Main!.Status = ActionStatus.Paused;
        s.Sub = new ActionState { ActionId = s.NextActionId++, IsSub = true, Type = ActionType.Use, Label = "补给" };
        t.Rearm();
        Assert.That(t.Observe(Busy, s), Is.False);
        Assert.That(t.Owners, Is.EqualTo(new[] { s.Main.ActionId, s.Sub.ActionId }));
        s.Sub = null;
        Assert.That(t.Observe(Busy, s), Is.False, "主行动仍暂停占用，事项盘仍受阻，保留");
        s.Main = null;
        Assert.That(t.Observe(Busy, s), Is.True);
    }

    /// <summary>Q318-A01：主行动暂停时重复触发，忙碌句只在暂停层底部出现一次（右栏省略）；另一条有效反馈照常在右栏。</summary>
    [Test]
    public void Q318_A01_Paused_ShownOnlyInPauseLayer()
    {
        var s = Registered();
        var t = new BusyNoticeTracker();
        Start(s, ActionRequest.Sleep(Cfg.Actions.SleepMinutes.Min()));
        s.Main!.Status = ActionStatus.Paused;
        Assert.That(BusyNoticeTracker.PauseLayerShown(s, "city"), Is.True, "事项盘开着，暂停层显示");
        for (var i = 0; i < 3; i++)
        {
            var notice = Trigger(s, t);
            Assert.That(notice, Is.EqualTo(Busy), "暂停时事项盘原校验仍是行动占用（Q317）");
            Assert.That(BusyNoticeTracker.RightColumnNotice(notice, s, "city"), Is.Null, "右栏省略，只在暂停层底部（PauseLayer 显示 Notice）");
        }
        Assert.That(BusyNoticeTracker.RightColumnNotice(Other, s, "city"), Is.EqualTo(Other), "其他反馈照常");
        // 制作面板开着时暂停层不显示（GameScreen 原条件），这条反馈仍在右栏显示一次
        Assert.That(BusyNoticeTracker.PauseLayerShown(s, "craft"), Is.False);
        Assert.That(BusyNoticeTracker.RightColumnNotice(Busy, s, "craft"), Is.EqualTo(Busy));
        // 进行中（未暂停）没有暂停层，右栏照常
        E.Resume(s);
        Assert.That(BusyNoticeTracker.RightColumnNotice(Busy, s, "city"), Is.EqualTo(Busy));
    }

    /// <summary>Q318-A01：清理只针对这一句——另一条有效反馈在行动结束时不被清除，归属也随之清空。</summary>
    [Test]
    public void Q318_A01_OtherNoticeUntouched()
    {
        var s = Registered();
        var t = new BusyNoticeTracker();
        Start(s, ActionRequest.Wait(30));
        Trigger(s, t);
        // 被别的提示替换（壳层赋值即 Rearm）
        t.Rearm();
        Assert.That(t.Observe(Other, s), Is.False);
        Assert.That(t.Owners, Is.Empty);
        Run(s);
        Assert.That(s.Main, Is.Null);
        foreach (var other in new[] { Other, "存档失败，可重试", "先处理眼前的敌踪", null })
            Assert.That(t.Observe(other, s), Is.False, $"{other ?? "空"} 不受影响");
        // 没有行动时出现的忙碌句视为已失效
        t.Rearm();
        Assert.That(t.Observe(Busy, s), Is.True);
    }

    // ───────────── 壳层接线 ─────────────

    private static string Root()
    {
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "web", "Survival.sln"))) dir = dir.Parent;
        Assert.That(dir, Is.Not.Null, "找到仓库根目录");
        return dir!.FullName;
    }

    private static string Web(params string[] parts) => File.ReadAllText(Path.Combine(new[] { Root(), "web", "Survival.Web" }.Concat(parts).ToArray()));

    /// <summary>Q318-A01：反馈位赋值即重新归属、每次刷新前清理、退出不顺手清掉忙碌句；右栏各处取去重后的反馈，暂停层底部取原反馈；暂停层显示条件共用。</summary>
    [Test]
    public void Q318_A01_ShellWiring()
    {
        var session = Web("Services", "GameSession.cs");
        Assert.That(session, Does.Contain("set { _notice = value; _busyNotice.Rearm(); }"));
        Assert.That(session, Does.Contain("ClearStaleFormationNotice();\r\n        ClearEndedBusyNotice();").Or.Contain("ClearStaleFormationNotice();\n        ClearEndedBusyNotice();"));
        Assert.That(session, Does.Contain("if (_busyNotice.Observe(_notice, State)) _notice = null;"));
        Assert.That(session, Does.Contain("else if (!BusyNoticeTracker.IsBusyNotice(Notice)) Notice = null;"), "退出");
        Assert.That(session, Does.Contain("public string? RightNotice => BusyNoticeTracker.RightColumnNotice(Notice, State, Panel);"));

        // B42：右栏选中分支（含空态「当前位置」与其反馈）移到 SelectionDetail，点击与悬停共用
        foreach (var f in new[] { "SelectionDetail", "CharacterPanel", "PreviewView", "ItemDetail", "FireDetail", "EnemyDetail", "BattlePanel" })
        {
            var src = Web("Components", f + ".razor");
            Assert.That(src, Does.Contain("G.RightNotice"), f);
            Assert.That(src, Does.Not.Contain("G.Notice"), f + " 右栏不直接显示原反馈");
        }
        Assert.That(Web("Components", "PauseLayer.razor"), Does.Contain("@if (G.Notice != null) { <div class=\"notice\" style=\"max-width:720px\">@G.Notice</div> }"), "暂停层底部显示一次");
        var screen = Web("Components", "GameScreen.razor");
        Assert.That(screen, Does.Contain("@if (G.PauseLayerShown)"));
        Assert.That(Web("Components", "GateButton.razor"), Does.Contain("G.Notice = Blocked;"), "受阻控件仍写原反馈位");
    }
}
