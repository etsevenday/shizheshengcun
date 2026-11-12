using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 B29.7 试玩修复：M553 视角成员的取数由内核测。（BattlePanel 旧敌踪分支的源码检查随旧接敌流程在 B41e3 删除。）
/// </summary>
public class B29PlaytestFixTests : TestBase
{
    [Test]
    public void M553_Encounter_ViewMemberIsPlayer_DodgeWithoutBattle()
    {
        var s = New();
        Assert.That(s.Battle, Is.Null);
        Assert.That(GameEngine.MeId(s), Is.EqualTo(GameState.PlayerId));
        Assert.That(E.MyBaseDodge(s), Is.EqualTo(Cfg.Scope.BasePlayerDodge));
    }
}
