using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// B41e3（R135、R151）：新版敌踪翻开关与收尾——翻开后的风险复核。
/// ① 一场战斗结束、回到非战斗边界时另一张到期敌踪在同一次指令／推进里立刻开下一场（Q416 第 4 条）：内核换新战斗号、原行动仍暂停，
///    壳层按“上一场收尾 + 下一场入战”处理（OnChainedBattle）；
/// ② 敌方开战打断推进：本段先提交再暂停、战后可继续（B41bEnemyApproachTests Q416／Q467 已覆盖，这里在①里连同下一场一起复核）；
/// ③ 旧档首次读档采纳新版（B41a 迁移规则）：默认配置即采纳，待处理名单转组卡、旧绕行取消；
/// ④ Q534：翻开后「当前行动还没结束。」仍成立；
/// ⑤ B41c2 锁定复核与 Q521-A01：默认配置下潜行接近、躲避两种判定都是不可中途退出的 5 分钟行动。
/// </summary>
public class B41e3Tests : B41cTestBase
{
    private static string Web(string rel) =>
        File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "Survival.Web", rel));

    [Test]
    public void Risk1_BattleEndsByCommand_NextDueCardOpensNewBattle_SameCall_MainStaysPaused()
    {
        var s = Field(3, "shambler", "crawler");
        var a = GameEngine.SingleCardKey(s.WorldEnemies[0].Id);
        var b = GameEngine.SingleCardKey(s.WorldEnemies[1].Id);
        SetLeft(s, a, 5);
        Start(s, ActionRequest.Clear());   // BM5c：探索在原地延后普通到期，这里用同为可暂停多段工作的清理
        var id = s.Main!.ActionId;
        Assert.That(Step(s), Is.EqualTo(SegmentStatus.Paused), "敌方开战打断推进");
        var first = s.Battle!;
        Assert.That(first.CardKey, Is.EqualTo(a));
        SetLeft(s, b, 0);   // 战斗中另一张卡已到期：只能等战后边界
        Assert.That(s.Battle, Is.SameAs(first), "战斗中不接第二场");

        // 上一场以玩家指令（撤退成功）结束 → 同一次 ExecuteCommand 里战后边界立刻开下一场
        var firstId = first.BattleId;
        PlayerFirst(s);
        E.BattleStep(s);
        Force(s, Below(0.0001));
        Assert.That(Cmd(s, "retreat").Ok, Is.True);
        Assert.That(s.Battle, Is.Not.Null, "结算上一场的同一次指令调用里下一场已开（中间没有“无战斗”的空档给壳层收尾）");
        Assert.That(s.Battle!.BattleId, Is.Not.EqualTo(firstId), "换新战斗号（壳层据此收尾上一场、接下一场）");
        Assert.That(s.Battle.CardKey, Is.EqualTo(b));
        Assert.That(s.Battle.Context, Is.EqualTo(GameEngine.FearEncounter), "下一场也是敌方开战");
        Assert.That(s.Main!.ActionId, Is.EqualTo(id));
        Assert.That(s.Main.Status, Is.EqualTo(ActionStatus.Paused), "原行动仍暂停，不在两场之间偷偷继续");
        Assert.That(E.Resume(s).Ok, Is.False, "战斗中不能继续原行动");

        WinBattle(s);
        Assert.That(s.Battle, Is.Null);
        Assert.That(s.Main!.ActionId, Is.EqualTo(id));
        Assert.That(s.Main.Status, Is.EqualTo(ActionStatus.Paused), "两场打完原行动仍在、仍暂停，由玩家继续或退出");
        if (E.ResumeBlockReason(s) == null)
        {
            Assert.That(E.Resume(s).Ok, Is.True);
            Assert.That(Run(s), Is.EqualTo(SegmentStatus.Completed));
        }
        else Assert.That(E.Cancel(s).Ok, Is.True, "不能继续时可以退出");
    }

    [Test]
    public void Risk1_Shell_ChainedBattle_WrapsUpPreviousThenEntersNext()
    {
        var g = Web("Services/GameSession.cs");
        Assert.That(g, Does.Contain("private void OnChainedBattle()"));
        var chained = g[g.IndexOf("private void OnChainedBattle()")..];
        chained = chained[..chained.IndexOf('}')];
        Assert.That(chained, Does.Contain("OnBattleEnded();"), "先按战斗结束收尾上一场");
        Assert.That(chained, Does.Contain("Selected = new Selection();").And.Contain("DetailTab = DetailTab.Object;").And.Contain("AutoTarget();"), "再按原入战流程接下一场");
        // 执行指令与逐条推进两处都按战斗号判断“换了一场”
        Assert.That(g, Does.Contain("var chainedNow = State != null && S.Battle != null && S.Battle.BattleId != battleId;").And.Contain("else if (chainedNow) OnChainedBattle();"));
        Assert.That(g, Does.Contain("var chained = State != null && S.Battle != null && S.Battle.BattleId != stepBattle;"));
        Assert.That(g, Does.Contain("if (chained) { if (!played) await StepPause(); continue; }"), "下一场接着走，不停在“已结束”（B34b：上一场的特写先播完；BM7a Q633 已无跳过意图）");
    }

    [Test]
    public void Risk3_OldSave_FirstLoad_AdoptsNewModel_WithDefaultConfig()
    {
        var s = LegacyField(3);
        var w = E.DebugSpawnEnemy(s, "shambler", "field");
        LegacyEncounter(s, new[] { w }, "explore");
        var json = SaveSerializer.Serialize(s);
        var t = SaveSerializer.Deserialize(json, Cfg);
        Assert.That(t.EnemyCards.ModelVersion, Is.EqualTo(GameEngine.EnemyCardsModelV2), "默认配置首次读档即采纳新版");
        Assert.That(t.Encounter, Is.Null, "旧待处理名单清空");
        Assert.That(E.EnemyCardsHere(t).Any(c => c.Kind == "group" && c.Members.Any(m => m.Id == w.Id)), Is.True, "转成组卡");
        var again = SaveSerializer.Deserialize(SaveSerializer.Serialize(t), Cfg);
        Assert.That(again.EnemyCards.Groups, Has.Count.EqualTo(t.EnemyCards.Groups.Count), "再读档不重复采纳");
    }

    [TestCase("sneak", "潜行接近")]
    [TestCase("evade", "躲避")]
    public void B41c2_Q521_A01_StealthChecks_LockedAfterFlip(string mode, string label)
    {
        var (s, key) = OneShambler();
        s.SuppressEnemyApproach = true;
        Assert.That(E.StartStealth(s, key, mode).Ok, Is.True);
        Assert.That(s.Main!.Label, Is.EqualTo(label));
        var p = E.ActionProgress(s)!;
        Assert.That((p.Locked, p.Exit, p.ExitNote), Is.EqualTo((true, ExitKind.None, "本次行动不可中途退出。")), "运行中：中央条与窄条同源只读说明、无退出");
        Assert.That(E.Cancel(s).Reason, Is.EqualTo("该行动不可中途取消"));
        Assert.That(E.RequestExit(s).Reason, Is.EqualTo("该行动不可中途取消"), "旧调用同样拒绝");
        Assert.That(s.Main, Is.Not.Null, "拒绝不取消");
        var shell = Web("Components/CentralProgress.razor");
        Assert.That(shell, Does.Contain("disabled=\"@(p.Exit == ExitKind.None)\""), "退出按钮按同源 Exit 禁用");
    }

    [Test]
    public void Q534_AfterFlip_ActionNotFinishedReasonStillHolds()
    {
        var s = Field(3, "shambler");
        var a = GameEngine.SingleCardKey(s.WorldEnemies[0].Id);
        s.SuppressEnemyApproach = true;
        Start(s, ActionRequest.Explore());
        Assert.That(E.CanApproachCard(s, a), Is.EqualTo("当前行动还没结束。"));
    }
    /// <summary>
    /// B41e3 试玩修复 B1（R05 Q464、Q473；逐字稿「原暂停层需允许进入既有地图和合法返程移动入口」）：
    /// 农野荒地采集中敌方到期开战、撤退成功退到营地，原采集保留暂停；内核判定移动入口开放，可走回原地，到达后仍暂停、「继续行动」可用。
    /// </summary>
    [Test]
    public void PlayFix_B1_RetreatKeptGather_PausedTravelOpen_WalkBack_Resume()
    {
        var s = Field(3);
        var id = KeptAtField(s, ActionRequest.Gather("field_fiber"), g =>
        {
            var rs = g.Resources["field_fiber"];
            rs.Discovered = true;
            rs.Available = Math.Max(3, rs.Available ?? 0);
        });
        Assert.That(E.IsBusy(s), Is.True, "原工作仍在（暂停），不是空闲");
        Assert.That(E.PausedTravelOpen(s), Is.True, "撤退离地保留的暂停：地图前往与移动确认开放");
        Assert.That(E.Resume(s).Ok, Is.False, "离地时不能继续");
        Assert.That(E.Preview(s, ActionRequest.Travel("field")).Ok, Is.True, "移动预览可走");

        Start(s, ActionRequest.Travel("field"));
        Assert.That(E.PausedTravelOpen(s), Is.False, "移动途中（原工作暂存）不再算暂停层的移动入口");
        Assert.That(Run(s), Is.EqualTo(SegmentStatus.Completed));
        Assert.That(s.CurrentLocation, Is.EqualTo("field"));
        Assert.That(s.Main!.ActionId, Is.EqualTo(id), "到达后原采集放回");
        Assert.That(s.Main.Status, Is.EqualTo(ActionStatus.Paused));
        Assert.That(E.PausedTravelOpen(s), Is.True, "回到原地也不收回移动例外（Q473）");
        Assert.That(E.Resume(s).Ok, Is.True, "回到原地「继续行动」可用");
        Assert.That(E.PausedTravelOpen(s), Is.False, "继续后收回");
    }

    [Test]
    public void PlayFix_B1_PausedTravelOpen_FalseForOrdinaryPause_AndWithSub()
    {
        var s = Field(3);
        Start(s, ActionRequest.Explore());
        s.Main!.Status = ActionStatus.Paused;
        s.Main.PauseReason = PauseReason.StatAlert;
        Assert.That(E.PausedTravelOpen(s), Is.False, "普通暂停不开放移动");
        Assert.That(E.CanStart(s, ActionRequest.Travel("camp")), Is.EqualTo("暂停中只能补给、休息或点火维护"), "与内核校验一致");

        var k = Field(3);
        KeptCraft(k);
        Start(k, ActionRequest.Rest(30));
        Assert.That(k.Sub, Is.Not.Null);
        Assert.That(E.PausedTravelOpen(k), Is.False, "补给／休息子行动进行中不开放");
    }

    [Test]
    public void PlayFix_B1_Shell_MapAndTravelPopUsePausedTravelOpen()
    {
        var pops = Web("Components/ActionPops.razor");
        Assert.That(pops, Does.Contain("var moveBusy = (e.IsBusy(s) && !e.PausedTravelOpen(s)) || G.Running || G.BattleBusy || _go;"));
        Assert.That(pops, Does.Contain("disabled=\"@(!p.Ok || moveBusy)\""), "移动确认按 moveBusy 判定");
        Assert.That(pops, Does.Contain("<EnterCap On=\"@(p.Ok && !moveBusy)\" />"));
        var map = Web("Components/MapOverlay.razor");
        Assert.That(map, Does.Contain("else if (adjacent.Contains(target) && (!e.IsBusy(s) || e.PausedTravelOpen(s)))"), "右栏显示移动预估");
        Assert.That(map, Does.Contain("@if (e.IsBusy(s) && !e.PausedTravelOpen(s))"), "不再提示「行动进行中，地图只能浏览」");
        Assert.That(map, Does.Contain("if (adjacent && (!G.E.IsBusy(s) || G.E.PausedTravelOpen(s)) && !G.Running && !G.DialogueLocked) G.OpenPop(\"mapnode\""), "节点旁打开移动确认");
        var card = Web("Components/LocationCard.razor");
        Assert.That(card, Does.Contain("前往 ▸"), "地点卡「前往 ▸」入口沿用");
    }

    [Test]
    public void PlayFix_PracticeDisk_BackButton_NoWrap()
    {
        var css = Web("wwwroot/css/app.css");
        Assert.That(css, Does.Contain(".practice-actions .practice-start { flex: 1 1 auto; width: auto; min-width: 0; }"));
        Assert.That(css, Does.Contain(".practice-actions .practice-back { flex: 0 0 auto; white-space: nowrap; }"));
    }
}
