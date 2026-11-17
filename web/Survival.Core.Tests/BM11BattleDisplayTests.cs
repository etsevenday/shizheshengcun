using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发记录 BM11「次要问题合并」：R280（R03 §Q617 AP 跟结果帧）与 Q919 选 B（装填／快速装填弹数到短反馈结束才更新）。
/// 规则出处：R03 §Q617 策划答复（“常驻战场HP／体力／AP……都跟结果帧／跳过进度”）；R03／S14 文首「2026-11-17 B37a1／BM10／B37a2三个单题原批Q918—Q920」§Q919
/// 与验收 ARTF-Q919-01／02／03（assets/20261117/art-feedback-q918-q920.json）。
/// </summary>
public class BM11BattleDisplayTests : BattleTestBase
{
    protected override bool NoEnemyApproach => true;

    private void Ready(GameState s)
    {
        PlayerFirst(s);
        E.BattleStep(s);
        Assert.That(E.AwaitingPlayer(s), Is.True);
    }

    private static List<BattleFxEvent> Full(List<BattleFxEvent> fx) => fx.Where(x => !x.Short && x.Results.Count > 0).ToList();

    private static int Carried(GameState s, string itemId) => s.Items.Where(i => GameEngine.CarriedByMe(s, i) && i.ItemId == itemId).Sum(i => i.Count);

    // ───────────── R280：AP 跟结果帧 ─────────────

    /// <summary>R280（R03 §Q617 A：AP 跟结果帧）：攻击出手第一段开始前的显示快照仍是提交前 AP 3，真实 AP 已在提交时扣掉（只扣一次）。</summary>
    [Test]
    public void R280_Attack_FirstSegmentShowsApBeforeCommand()
    {
        var s = Fight(28001, "worker");
        Ready(s);
        Arm(s, "machete");
        s.Battle!.PlayerAp = 3;   // 布场：本次激活剩 3AP
        E.DrainBattleFx();
        Assert.That(Cmd(s, "basic_attack", 1).Ok, Is.True);
        var full = Full(E.DrainBattleFx());
        Assert.That(full, Has.Count.GreaterThanOrEqualTo(1));
        var view = full[0].View!;
        Assert.That(view.Battle!.Fighter(GameState.PlayerId).Ap, Is.EqualTo(3), "R03 §Q617：结果帧之前顶栏 AP 仍为提交前 3");
        Assert.That(s.Battle!.Fighter(GameState.PlayerId).Ap, Is.EqualTo(1), "真实 AP 提交时扣普通攻击 2AP（R03 §3 普通攻击2AP，同 B6CombatTests），只扣一次");
    }

    /// <summary>R280：同一指令只有第一段出手用提交前 AP；短反馈指令（防御）沿原规则立即显示，不把提交前 AP 留给下一条指令。</summary>
    [Test]
    public void R280_ShortFeedbackCommand_NoHeldAp_NextCommandUsesItsOwnBefore()
    {
        var s = Fight(28002, "worker");
        Ready(s);
        Arm(s, "machete");
        s.Battle!.PlayerAp = 3;
        E.DrainBattleFx();
        Assert.That(Cmd(s, "guard").Ok, Is.True);
        var g = E.DrainBattleFx();
        Assert.That(g, Has.Count.EqualTo(1));
        Assert.That((g[0].Short, g[0].View), Is.EqualTo((true, (GameState?)null)), "短反馈没有结果帧：AP 直接显示真实值");
        Assert.That(s.Battle!.PlayerAp, Is.EqualTo(2));
        Assert.That(Cmd(s, "basic_attack", 1).Ok, Is.True);
        var full = Full(E.DrainBattleFx());
        Assert.That(full, Has.Count.GreaterThanOrEqualTo(1));
        Assert.That(full[0].View!.Battle!.Fighter(GameState.PlayerId).Ap, Is.EqualTo(2), "下一条指令的第一段按它自己提交前的 2AP");
    }

    /// <summary>R280：战术品（闪光标记器）先扣 AP 再开出手记录——第一段快照同样是提交前 AP。</summary>
    [Test]
    public void R280_TacticalItem_FirstSegmentShowsApBefore()
    {
        var s = Fight(28003, "worker");
        Ready(s);
        E.DebugGive(s, "flash_marker", 1);
        s.Battle!.PlayerAp = 3;
        E.DrainBattleFx();
        Assert.That(Cmd(s, "tactic_flash_marker", 1).Ok, Is.True);
        var full = Full(E.DrainBattleFx());
        Assert.That(full, Has.Count.EqualTo(1));
        Assert.That(full[0].View!.Battle!.Fighter(GameState.PlayerId).Ap, Is.EqualTo(3));
        Assert.That(s.Battle!.Fighter(GameState.PlayerId).Ap, Is.LessThan(3), "真实 AP 已扣");
    }

    // ───────────── Q919：装填弹数到短反馈结束才更新 ─────────────

    /// <summary>ARTF-Q919-01：手枪 0/8 装填——短反馈带装填前 0 与本武器实例；真实已是 8/8，随身 9mm 扣 8，AP 扣 1（原事务）。</summary>
    [Test]
    public void Q919_01_Reload_ShortCarriesLoadedBefore_RealCommitted()
    {
        var s = Fight(91901, "worker");
        Ready(s);
        StandAt(s, 2);
        var pistol = Arm(s, "pistol");
        pistol.LoadedAmmo = 0;
        s.Items.RemoveAll(i => i.ItemId == "ammo_9mm");
        E.DebugGive(s, "ammo_9mm", 20);
        s.Battle!.PlayerAp = 3;
        E.DrainBattleFx();
        Assert.That(Cmd(s, "reload").Ok, Is.True);
        var fx = E.DrainBattleFx();
        Assert.That(fx, Has.Count.EqualTo(1));
        var ev = fx[0];
        Assert.That(ev.Short, Is.True, "不新增特写（ARTF-Q919-02）");
        Assert.That(ev.AmmoHold, Is.Not.Null);
        Assert.That((ev.AmmoHold!.WeaponInstanceId, ev.AmmoHold.LoadedBefore), Is.EqualTo((pistol.InstanceId, 0)), "Q919 例：短反馈期间仍 0/8");
        Assert.That(pistol.LoadedAmmo, Is.EqualTo(8), "真实装到 8/8");
        Assert.That(Carried(s, "ammo_9mm"), Is.EqualTo(12));
        Assert.That(s.Battle!.PlayerAp, Is.EqualTo(2), "AP 原事务立即结算");
    }

    /// <summary>ARTF-Q919-01：真实只够装到 3/8 就是 3（不按例子 8/8）；等待里仍是装填前的 0。</summary>
    [Test]
    public void Q919_01_Reload_PartialAmmo_RealThree()
    {
        var s = Fight(91902, "worker");
        Ready(s);
        StandAt(s, 2);
        var pistol = Arm(s, "pistol");
        pistol.LoadedAmmo = 0;
        s.Items.RemoveAll(i => i.ItemId == "ammo_9mm");
        E.DebugGive(s, "ammo_9mm", 3);
        E.DrainBattleFx();
        Assert.That(Cmd(s, "reload").Ok, Is.True);
        var ev = E.DrainBattleFx().Single();
        Assert.That(ev.AmmoHold!.LoadedBefore, Is.EqualTo(0));
        Assert.That(pistol.LoadedAmmo, Is.EqualTo(3));
    }

    /// <summary>Q919：快速装填（火器5级）同样带装填前值；被拒装填（弹仓已满）不出反馈、不造等待；防御等其他短反馈不带。</summary>
    [Test]
    public void Q919_QuickReload_AndRejected_AndOtherShort()
    {
        var s = Fight(91903, "worker");
        Ready(s);
        StandAt(s, 2);
        var pistol = Arm(s, "pistol");
        pistol.LoadedAmmo = 2;
        s.Items.RemoveAll(i => i.ItemId == "ammo_9mm");
        E.DebugGive(s, "ammo_9mm", 20);
        s.Skills["firearms"].Level = 5;
        s.Battle!.PlayerAp = 3;
        E.DrainBattleFx();
        Assert.That(Cmd(s, "firearms_05").Ok, Is.True, string.Join(" | ", s.Battle.Log.TakeLast(2)));
        var q = E.DrainBattleFx().Single();
        Assert.That((q.Short, q.AmmoHold?.WeaponInstanceId, q.AmmoHold?.LoadedBefore), Is.EqualTo((true, (int?)pistol.InstanceId, (int?)2)));
        Assert.That(pistol.LoadedAmmo, Is.EqualTo(8));

        Assert.That(Cmd(s, "reload").Ok, Is.False, "弹仓已满被拒");
        Assert.That(E.DrainBattleFx(), Is.Empty, "被拒装填不造等待（ARTF-Q919-03）");

        Assert.That(Cmd(s, "guard").Ok, Is.True);
        var g = E.DrainBattleFx().Single();
        Assert.That((g.Short, g.AmmoHold), Is.EqualTo((true, (BattleFxAmmoHold?)null)), "其他短反馈不推迟弹数");
    }

    /// <summary>
    /// ARTF-Q919-01／03（网页显示层）：武器页签的弹数与派生「弹仓空」读同一显示值；提交新指令时以本次出手重定等待（新动作取代旧等待），
    /// 短反馈播放开始后按原短反馈时长结束、序号不同的旧回调不覆盖；切武器页、换场、载入清理。
    /// </summary>
    [Test]
    public void Q919_03_WebHoldWiring()
    {
        var bs = Web("Components", "BattleScreen.razor");
        Assert.That(bs, Does.Contain("var shownAmmo = pi == null ? 0 : G.ShownLoadedAmmo(pi);"));
        Assert.That(bs, Does.Contain("pw.UsesMagazine && shownAmmo <= 0 ? \"弹仓空\""));
        Assert.That(bs, Does.Contain("$\"弹{shownAmmo}/{pw.MagazineCapacity}\""));
        Assert.That(bs, Does.Not.Contain("$\"弹{pi.LoadedAmmo}/{pw.MagazineCapacity}\""));
        var pb = Web("Services", "GameSession.Playback.cs");
        Assert.That(pb, Does.Contain("b.BattleId == h.BattleId && wi.InstanceId == h.WeaponId ? h.Loaded : wi.LoadedAmmo"), "只绑定本战本武器");
        Assert.That(pb, Does.Contain("if (gen != _fxGen || _ammoHold is not { } h || h.Seq != seq) return;"), "旧回调不覆盖更晚动作");
        Assert.That(pb, Does.Contain("if (ev.AmmoHold != null && _ammoHold is { } ah && ah.BattleId == ev.BattleId && ah.WeaponId == ev.AmmoHold.WeaponInstanceId) _ = ReleaseAmmoHold(ah.Seq, gen);"), "短反馈开始播才计结束");
        Assert.That(pb, Does.Contain("_ammoHold = null;   // BM11（Q919）：载入／新局"));
        var gs = Web("Services", "GameSession.cs");
        Assert.That(gs, Does.Contain("PrimeAmmoHold(fx);"));
        Assert.That(gs, Does.Contain("ClearAmmoHold();   // BM11（Q919）：换武器页"));
        Assert.That(gs, Does.Contain("ClearAmmoHold();   // BM11（Q919）：换场／离场"));
    }

    private static string Web(params string[] parts)
    {
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "web", "Survival.sln"))) dir = dir.Parent;
        return File.ReadAllText(Path.Combine(new[] { dir!.FullName, "web", "Survival.Web" }.Concat(parts).ToArray())).Replace("\r\n", "\n");
    }
}
