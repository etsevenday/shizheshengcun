using System.Text.RegularExpressions;
using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>开发计划 v0.7 试玩修复（M68）中内核与配置部分：生存提示不写死门槛（B4）、退出后果提示（附带）、战斗用药效果数值（附带）。</summary>
public class V10PlaytestFixM68Tests : BattleTestBase
{
    [Test]
    public void B4_TipsDoNotHardcodeImmunityThresholds()
    {
        // 提示里涉及免疫门槛的地方只能用占位符，由界面按现行配置替换（现为 80，不再是旧的 85）
        foreach (var tip in Cfg.Progress.Tips)
            Assert.That(Regex.IsMatch(tip, @"免疫≥\d"), Is.False, $"提示写死了免疫门槛：{tip}");
        Assert.That(Cfg.Progress.Tips.Any(t => t.Contains("{baobaoSuppressImmunity}")), Is.True);
        Assert.That(Cfg.Progress.Tips.Any(t => t.Contains("{cureImmunity}")), Is.True);
        Assert.That(Cfg.Infection.BaobaoInput.FullSuppressImmunityAtLeast, Is.EqualTo(80));
        Assert.That(Cfg.Infection.NaturalCure.ConsecutiveImmunityAtLeast, Is.EqualTo(80));
    }

    [Test]
    public void ExitNote_WorkThatForfeitsResult_SaysSo_RestDoesNot()
    {
        var s = New();
        E.DebugTeleport(s, "field");
        Start(s, ActionRequest.Explore().WithRequestId("x1"));
        E.Advance(s);
        var p = E.ActionProgress(s)!;
        Assert.That(p.ExitForfeits, Is.True);
        Assert.That(p.ExitNote, Does.Contain("放弃本次探索"));
        // 与实际退出结算一致：探索度不变
        var progress = s.Locations["field"].Progress;
        Assert.That(E.RequestExit(s).Ok, Is.True);
        Run(s);
        Assert.That(s.Locations["field"].Progress, Is.EqualTo(progress));

        var r = New();
        Start(r, ActionRequest.Rest(60).WithRequestId("r1"));
        E.Advance(r);
        var q = E.ActionProgress(r)!;
        Assert.That(q.ExitForfeits, Is.False);
        Assert.That(q.ExitNote, Does.Not.Contain("放弃"));
    }

    [Test]
    public void BattleMedicinePreview_HasEffectNumbers_SameAsResult()
    {
        var s = Fight(4242, "shambler");
        ToPlayer(s);
        E.CreateItems(s, "bandage", 1, ItemInstance.Carried);
        s.Stats.Health = 10;
        s.BandageReadyAt = 0;
        var p = E.PreviewCommand(s, BattleCommand.Of("bandage"));
        Assert.That(p.Ok, Is.True, p.Reason);
        Assert.That(p.EffectText, Does.Contain("健康10.00→"));
        Assert.That(p.Notes[0], Is.EqualTo(p.EffectText), "效果数值放在第一条，战斗区执行条直接显示");
        var expected = p.EffectText!.Split("→")[1].Substring(0, 5);
        Assert.That(E.ExecuteCommand(s, BattleCommand.Of("bandage").WithRequestId("m1")).Ok, Is.True);
        Assert.That(Fmt.N(s.Stats.Health), Is.EqualTo(expected), "预览数值与执行结果同源");
    }
}
