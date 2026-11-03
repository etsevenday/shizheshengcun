using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 B22 R70：三种战术品。出处：R03「战术物合法性与效果引用」、A09「E7 制作战术消耗品」、A07 X1.13；R01 更新说明（R01-C01）。
/// 验收 EC10、EC11、EC12、R01-C01。
/// </summary>
public class B22TacticalTests : BattleTestBase
{
    private GameState Ready(uint seed)
    {
        var s = Fight(seed, "shambler", "shambler");
        ToPlayer(s);
        Assert.That(E.AwaitingPlayer(s), Is.True);
        foreach (var id in new[] { "flash_marker", "brace_pack", "rally_patch" }) E.CreateItems(s, id, 2, ItemInstance.Carried);
        return s;
    }

    private static int Have(GameState s, string id) => s.Items.Where(i => i.IsCarried && i.ItemId == id).Sum(i => i.Count);

    /// <summary>
    /// EC10 闪光标记器：2AP/体力0.1、敌方任一存活单体；标记 2 大回合全队命中能力+6、噪声+5；与已有标记取强、不叠层；更强更长的已有标记拒绝不消耗。
    /// 不给经验、不磨武器。
    /// </summary>
    [Test]
    public void EC10_FlashMarker_TakeStrongest_Noise_NoXpNoWear()
    {
        var s = Ready(2251);
        var b = s.Battle!;
        var t = b.Alive.Last();
        var ap = b.PlayerAp;
        var energy = s.Stats.Energy;
        var noise = b.Noise;
        var xp = s.Skills.Sum(kv => kv.Value.Xp);
        var opt = E.BattleOptions(s).Single(o => o.Action == "tactic_flash_marker");
        Assert.That((opt.Group, opt.TargetMode, opt.NeedsTarget, opt.Available), Is.EqualTo(("item", "single", true, true)));
        Assert.That(opt.Targets, Does.Contain(t.Index), "1—4 号位都可以");
        var pv = E.PreviewCommand(s, BattleCommand.Of("tactic_flash_marker", t.Index));
        Assert.That(pv.Ok, Is.True);
        Assert.That((pv.Ap, pv.EnergyCost, pv.NoiseAfter - pv.NoiseBefore), Is.EqualTo((2, 0.1, 5d)));
        Assert.That(E.ExecuteCommand(s, BattleCommand.Of("tactic_flash_marker", t.Index)).Ok, Is.True);
        Assert.That(b.PlayerAp, Is.EqualTo(ap - 2));
        Assert.That(s.Stats.Energy, Is.EqualTo(energy - 0.1).Within(1e-9));
        Assert.That(b.Noise, Is.EqualTo(noise + 5));
        Assert.That(Have(s, "flash_marker"), Is.EqualTo(1));
        Assert.That((t.MarkBonus, t.MarkUntilRound), Is.EqualTo((6d, b.Round + 2)));
        Assert.That(GameEngine.EnemyMarked(b, t), Is.True);
        Assert.That(s.Skills.Sum(kv => kv.Value.Xp), Is.EqualTo(xp), "不给经验");
        Assert.That(t.Hp, Is.GreaterThan(0), "无伤害");

        // 下一大回合：已有更强更长的标记 → 拒绝不消耗；较弱的标记 → 取强
        b.Round += 1;
        b.Me.TacticalRound = 0;
        b.PlayerAp = 3;
        t.MarkBonus = 8; t.MarkUntilRound = b.Round + 3;
        Assert.That(E.CommandBlocked(s, BattleCommand.Of("tactic_flash_marker", t.Index)), Does.Contain("没有收益"));
        Assert.That(E.ExecuteCommand(s, BattleCommand.Of("tactic_flash_marker", t.Index)).Ok, Is.False);
        Assert.That(Have(s, "flash_marker"), Is.EqualTo(1), "拒绝不扣费");
        t.MarkBonus = 4; t.MarkUntilRound = b.Round + 3;
        Assert.That(E.ExecuteCommand(s, BattleCommand.Of("tactic_flash_marker", t.Index)).Ok, Is.True);
        Assert.That((t.MarkBonus, t.MarkUntilRound), Is.EqualTo((6d, b.Round + 3)), "同名取强、不叠层（+6 不是 +10），持续取较长");
    }

    /// <summary>EC10 支撑护带包：1AP、自身位移抵抗+25 两大回合；与稳固取强；已有更强更长的稳固拒绝。</summary>
    [Test]
    public void EC10_BracePack_TakeStrongest()
    {
        var s = Ready(2252);
        var b = s.Battle!;
        var o = E.BattleOptions(s).Single(x => x.Action == "tactic_brace_pack");
        Assert.That((o.TargetMode, o.Ap, o.Available), Is.EqualTo(("self", 1, true)));
        Assert.That(E.ExecuteCommand(s, BattleCommand.Of("tactic_brace_pack")).Ok, Is.True);
        Assert.That((b.Me.DisplaceResist, b.Me.DisplaceUntilRound), Is.EqualTo((25d, b.Round + 2)));
        Assert.That(b.PlayerGuard, Is.False, "不格挡");
        b.Round += 1;
        b.Me.TacticalRound = 0;
        b.PlayerAp = 3;
        b.Me.DisplaceResist = 30; b.Me.DisplaceUntilRound = b.Round + 2;
        Assert.That(E.CommandBlocked(s, BattleCommand.Of("tactic_brace_pack")), Does.Contain("没有收益"));
        b.Me.DisplaceResist = 20;
        Assert.That(E.ExecuteCommand(s, BattleCommand.Of("tactic_brace_pack")).Ok, Is.True);
        Assert.That(b.Me.DisplaceResist, Is.EqualTo(25), "取强不相加");
    }

    /// <summary>EC10 鼓舞提示贴：恐慌−6（最低 0）；每目标每大回合来自此道具合计最多 6；恐慌 0 无收益禁用；单人自用。</summary>
    [Test]
    public void EC10_RallyPatch_PerTargetPerRoundMax6()
    {
        var s = Ready(2253);
        var b = s.Battle!;
        b.Panic = 10;
        var o = E.BattleOptions(s).Single(x => x.Action == "tactic_rally_patch");
        Assert.That(o.TargetMode, Is.EqualTo("self"), "单人自用");
        Assert.That(E.PreviewCommand(s, BattleCommand.Of("tactic_rally_patch")).EffectText, Does.Contain("恐慌10.00→4.00"));
        Assert.That(E.ExecuteCommand(s, BattleCommand.Of("tactic_rally_patch")).Ok, Is.True);
        Assert.That(b.Panic, Is.EqualTo(4));
        // 同一大回合同一目标已减满 6（例如另一名队友刚给过）→ 拒绝
        b.Me.TacticalRound = 0;
        b.PlayerAp = 3;
        Assert.That(E.CommandBlocked(s, BattleCommand.Of("tactic_rally_patch")), Does.Contain("已经从鼓舞提示贴减过6点"));
        // 已减 3 → 这次只能再减 3
        b.Me.RallyAmount = 3;
        Assert.That(E.ExecuteCommand(s, BattleCommand.Of("tactic_rally_patch")).Ok, Is.True);
        Assert.That(b.Panic, Is.EqualTo(1));
        b.Round += 1;
        b.Me.TacticalRound = 0;
        b.PlayerAp = 3;
        b.Panic = 0;
        E.CreateItems(s, "rally_patch", 1, ItemInstance.Carried);
        Assert.That(E.CommandBlocked(s, BattleCommand.Of("tactic_rally_patch")), Does.Contain("恐慌已经是0"));
    }

    /// <summary>EC11：随身没有（地面/封存/队友包里的不算）、目标已死、体力不足都拒绝且不扣 AP/体力/物品。</summary>
    [Test]
    public void EC11_NoItem_InvalidTarget_LowEnergy_RejectedNoCost()
    {
        var s = Fight(2254, "shambler", "shambler");
        ToPlayer(s);
        var b = s.Battle!;
        E.CreateItems(s, "flash_marker", 1, s.Ground);
        Assert.That(E.CommandBlocked(s, BattleCommand.Of("tactic_flash_marker", b.Alive.First().Index)), Does.Contain("随身没有"));
        E.CreateItems(s, "flash_marker", 1, ItemInstance.Carried);
        var dead = b.Alive.First();
        dead.Dead = true;
        Assert.That(E.CommandBlocked(s, BattleCommand.Of("tactic_flash_marker", dead.Index)), Does.Contain("不在了"));
        dead.Dead = false;
        s.Stats.Set(StatId.Energy, 0.05);
        var ap = b.PlayerAp;
        var r = E.ExecuteCommand(s, BattleCommand.Of("tactic_flash_marker", dead.Index));
        Assert.That(r.Ok, Is.False);
        Assert.That(r.Reason, Does.Contain("体力不足"));
        Assert.That((b.PlayerAp, Have(s, "flash_marker"), s.Stats.Energy), Is.EqualTo((ap, 1, 0.05)));
    }

    /// <summary>EC12：三种战术品共享“施用者每大回合 1 次”，追击不刷新；下一大回合恢复。</summary>
    [Test]
    public void EC12_SharedOncePerRound_PursuitNoRefresh()
    {
        var s = Ready(2255);
        var b = s.Battle!;
        Assert.That(E.ExecuteCommand(s, BattleCommand.Of("tactic_brace_pack")).Ok, Is.True);
        Assert.That(E.CommandBlocked(s, BattleCommand.Of("tactic_flash_marker", b.Alive.First().Index)), Does.Contain("本大回合已经用过战术品"));
        Assert.That(E.BattleOptions(s).Single(o => o.Action == "tactic_rally_patch").Available, Is.False);
        b.ActingPursuit = true;
        b.PlayerAp = 2;
        Assert.That(E.CommandBlocked(s, BattleCommand.Of("tactic_flash_marker", b.Alive.First().Index)), Does.Contain("追击不刷新"));
        b.ActingPursuit = false;
        b.Round += 1;
        Assert.That(E.CommandBlocked(s, BattleCommand.Of("tactic_flash_marker", b.Alive.First().Index)), Is.Null, "下一大回合恢复");
    }

    /// <summary>R01-C01：同一标记不叠层、到期即失效，不写进敌人永久面板（配置里的敌人数值不变）。</summary>
    [Test]
    public void R01_C01_MarkNotStacked_Expires_NoPermanentPanel()
    {
        var s = Ready(2256);
        var b = s.Battle!;
        var t = b.Alive.First();
        var cfg = Cfg.Enemies.Single(x => x.Id == t.EnemyId);
        var panel = (cfg.Dodge, cfg.Armor);
        Assert.That(E.ExecuteCommand(s, BattleCommand.Of("tactic_flash_marker", t.Index)).Ok, Is.True);
        var until = t.MarkUntilRound;
        b.Round = until + 1;
        Assert.That(GameEngine.EnemyMarked(b, t), Is.False, "到期失效");
        Assert.That((cfg.Dodge, cfg.Armor), Is.EqualTo(panel));
    }
}
