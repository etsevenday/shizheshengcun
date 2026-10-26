using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 人物技能与回合制战斗参考策划案 v0.5 第 20 节验收 B501–B528（同名用例）。v0.6 冲突处以 v0.6 为准：
/// B512/B513 改为无夹紧、无取整语义；B501 按用户 2026-10-26 拍板“直接开启”改为断言战斗已启用。
/// </summary>
public class CombatV05Tests : TestBase
{
    /// <summary>B501 原为“开关关闭时无敌人”。不适用：用户 2026-10-26 拍板全量接入、直接开启，不做开关。</summary>
    [Test]
    public void B501_NotApplicable_CombatEnabledByDecision()
    {
        Assert.That(Cfg.CombatEnabled, Is.True);
        var s = New();
        Assert.That(s.Skills, Is.Not.Empty, "技能已启用");
    }

    /// <summary>B524 制造/医疗经验只按规则发一次；预览、取消、满血空治疗不发经验。</summary>
    [Test]
    public void B524_CraftAndMedicalXp_OnlyOnce()
    {
        var s = New();
        E.DebugGive(s, "stone", 4);
        E.DebugGive(s, "fiber", 4);
        var before = SaveSerializer.Serialize(s);
        E.Preview(s, ActionRequest.Craft("knife"));
        Assert.That(SaveSerializer.Serialize(s), Is.EqualTo(before), "预览不刷经验");

        Start(s, ActionRequest.Craft("knife"));
        Steps(s, 1);
        E.Cancel(s);
        Assert.That(s.Skills["crafting"].Xp, Is.EqualTo(0), "取消无经验");

        Start(s, ActionRequest.Craft("knife"));
        Run(s);
        Assert.That(s.Skills["crafting"].Xp, Is.EqualTo(3), "clamp(ceil(30/10),1,12)=3");
        Start(s, ActionRequest.Craft("knife", batchCount: 2));
        Run(s);
        Assert.That(s.Skills["crafting"].Xp, Is.EqualTo(9), "整批按实际提交的配方次数计");
        Assert.That(Count(s, "stone_knife"), Is.EqualTo(3));

        // 满血无出血：绷带不能用（无实际收益），不发经验
        E.DebugGive(s, "bandage", 2);
        SetStats(s, 20, 80, 80, 18, 90, 60);
        Assert.That(E.CanStart(s, ActionRequest.UseItem("bandage")), Is.Not.Null, "满血、未感染、无出血时禁用");
        Assert.That(s.Skills["medicine"].Xp, Is.EqualTo(0));
        SetStats(s, 15, 80, 80, 18, 90, 60);
        Start(s, ActionRequest.UseItem("bandage"));
        Run(s);
        Assert.That(s.Skills["medicine"].Xp, Is.EqualTo(Cfg.Xp.Medical), "真实恢复健康 +4，只发一次");
    }

    /// <summary>制造熟练度折减工作量，同批次余量结转：高等级批量在更少段内完成（v0.6 §03）。</summary>
    [Test]
    public void CraftSkill_ReducesWork_BatchCarryOver()
    {
        var s = New();
        E.DebugGive(s, "cloth", 10);
        E.DebugGive(s, "herb", 10);
        s.Skills["crafting"].Level = 40;                 // 工作量 15/(1+1)=7.5 分钟
        Start(s, ActionRequest.Craft("bandage", batchCount: 4));
        Assert.That(s.Main!.BaseWorkMinutes, Is.EqualTo(15 * 4 / 2.0).Within(1e-9), "整批30分钟工作量");
        Run(s);
        Assert.That(s.GameMinute - 480, Is.EqualTo(30), "4件合计30分钟，余量在同批内结转");
        Assert.That(Count(s, "bandage"), Is.EqualTo(4));
        var t = New();
        E.DebugGive(t, "cloth", 1);
        E.DebugGive(t, "herb", 1);
        t.Skills["crafting"].Level = 100;              // 15/(1+2.5)≈4.29 分钟
        Start(t, ActionRequest.Craft("bandage"));
        Run(t);
        Assert.That(t.GameMinute - 480, Is.EqualTo(5), "单件不足5分钟也占一段（时钟精度），不倒流");
        Assert.That(E.CanStart(t, ActionRequest.Craft("shelter") is var b ? WithBatch(b, 2) : b), Does.Contain("只能做1件"));
    }

    private static ActionRequest WithBatch(ActionRequest r, int n)
    {
        r.BatchCount = n;
        return r;
    }
}
