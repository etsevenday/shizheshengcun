using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发记录 BM10「次要问题合并」Q756 选 B：播放中操作台上医疗／战术品／弹药的随身份数跟结果帧（显示快照），真实扣物仍在提交时、只扣一次。
/// 规则出处：R03／S14 文首「2026-11-17 B51c2、B47b与B52b三个完整原批」§Q755—Q756 与验收 LIFE-Q756-01、LIFE-Q756-02；R03 §Q617 结果帧合同。
/// </summary>
public class BM10Q756Tests : BattleTestBase
{
    protected override bool NoEnemyApproach => true;

    // ───────────── Q756 操作台份数跟结果帧 ─────────────

    private void Ready(GameState s)
    {
        PlayerFirst(s);
        E.BattleStep(s);
        Assert.That(E.AwaitingPlayer(s), Is.True);
    }

    private static BattleFxEvent One(List<BattleFxEvent> fx) => fx.Single(x => !x.Short && x.Results.Count > 0);

    private static int Carried(GameState s, string itemId) => s.Items.Where(i => GameEngine.CarriedByMe(s, i) && i.ItemId == itemId).Sum(i => i.Count);

    private void Only(GameState s, string itemId, int n)
    {
        s.Items.RemoveAll(i => i.ItemId == itemId);
        if (n > 0) E.DebugGive(s, itemId, n);
        Assert.That(Carried(s, itemId), Is.EqualTo(n));
    }

    private ItemInstance Arm2(GameState s, string id)
    {
        var slot = ItemInstance.EquipOwner(GameState.PlayerId, EquipSlots.Weapon2);
        foreach (var old in s.Items.Where(i => i.Owner == slot)) old.Owner = ItemInstance.Carried;
        var it = E.CreateItems(s, id, 1, slot).Single();
        s.Battle!.ActiveWeaponSlot = 2;
        return it;
    }

    /// <summary>LIFE-Q756-01：绷带3用1——结果帧前（该段开始显示）仍3，真实库存已在提交时扣成2且只扣一次；别人随身的同名物不串数。</summary>
    [Test]
    public void Q756_01_Bandage_ThreeBeforeResultFrame()
    {
        var s = Fight(75601, "worker");
        Ready(s);
        Only(s, "bandage", 3);
        E.DebugGive(s, "bandage", 5, ItemInstance.CarriedOwner("someone_else"));
        s.Player.Stats.Health = 10;
        E.DrainBattleFx();
        Assert.That(Cmd(s, "bandage").Ok, Is.True);
        var ev = One(E.DrainBattleFx());
        Assert.That(ev.View, Is.Not.Null);
        Assert.That(Carried(ev.View!, "bandage"), Is.EqualTo(3), "S14 Q756 例：使用结果帧到来前仍×3");
        Assert.That(Carried(s, "bandage"), Is.EqualTo(2), "真实库存提交时扣 1（只扣一次）");
        Assert.That(E.BattleOptions(ev.View!).Single(o => o.Action == "bandage").Label, Does.EndWith("（随身×3）"));
        Assert.That(E.BattleOptions(s).Single(o => o.Action == "bandage").Label, Does.EndWith("（随身×2）"), "结果帧后（回真实状态）×2");
    }

    /// <summary>LIFE-Q756-02：连续两次动作逐帧——第一段开始3、第二段开始2、结束回真实1；读取快照不再扣物。</summary>
    [Test]
    public void Q756_02_ConsecutiveUses_FrameByFrame()
    {
        var s = Fight(75602, "worker");
        Ready(s);
        Only(s, "bandage", 3);
        s.Player.Stats.Health = 10;
        E.DrainBattleFx();
        Assert.That(Cmd(s, "bandage").Ok, Is.True);
        var v1 = One(E.DrainBattleFx()).View!;
        s.Player.BandageReadyAt = s.GameMinute;   // 布场：清回血冷却，让第二次合法
        s.Player.Stats.Health = 10;
        s.Battle!.PlayerAp = 3;
        Assert.That(Cmd(s, "bandage").Ok, Is.True);
        var v2 = One(E.DrainBattleFx()).View!;
        Assert.That((Carried(v1, "bandage"), Carried(v2, "bandage"), Carried(s, "bandage")), Is.EqualTo((3, 2, 1)), "不提前跳到整串最终值");
        _ = E.BattleOptions(v1);
        Assert.That(Carried(s, "bandage"), Is.EqualTo(1), "播放快照只读，不再次消费");
    }

    /// <summary>LIFE-Q756-02：战术品最后1份——真实已扣到0，结果帧前快照仍有这1份（图标与×1不提前消失）；到帧后按原0数量规则。</summary>
    [Test]
    public void Q756_02_TacticalLastOne_IconStaysUntilFrame()
    {
        var s = Fight(75603, "worker");
        Ready(s);
        Only(s, "flash_marker", 1);
        E.DrainBattleFx();
        Assert.That(Cmd(s, "tactic_flash_marker", 1).Ok, Is.True);
        var ev = One(E.DrainBattleFx());
        Assert.That(Carried(ev.View!, "flash_marker"), Is.EqualTo(1));
        Assert.That(Carried(s, "flash_marker"), Is.EqualTo(0));
        Assert.That(s.Items.Any(i => i.ItemId == "flash_marker"), Is.False, "真实实例已扣完");
        Assert.That(E.BattleOptions(ev.View!).Single(o => o.Action == "tactic_flash_marker").Label, Does.EndWith("（随身×1）"));
        Assert.That(E.BattleOptions(s).Single(o => o.Action == "tactic_flash_marker").Label, Does.EndWith("（随身×0）"));
    }

    /// <summary>LIFE-Q756-01：弹药同理——弹仓6发打1发，结果帧前显示6，真实5；弓的随身箭3用1，结果帧前3，真实2。</summary>
    [Test]
    public void Q756_01_Ammo_MagazineAndArrows()
    {
        var s = Fight(75604, "worker");
        Ready(s);
        var pistol = Arm(s, "pistol");
        pistol.LoadedAmmo = 6;
        StandAt(s, 2);   // 手枪使用位 234
        E.DrainBattleFx();
        Assert.That(Cmd(s, "basic_attack", 1).Ok, Is.True);
        var ev = One(E.DrainBattleFx());
        Assert.That(ev.View!.Items.Single(i => i.InstanceId == pistol.InstanceId).LoadedAmmo, Is.EqualTo(6), "结果帧前弹仓仍6");
        Assert.That(pistol.LoadedAmmo, Is.EqualTo(5), "真实提交即扣1发");
        Assert.That(s.Battle!.Log.Any(l => l.Contains("弹仓5/8")), Is.True, "战报扣量文字不变");

        var t = Fight(75605, "worker");
        Ready(t);
        Arm(t, "wooden_bow");
        StandAt(t, 2);
        Only(t, "arrow", 3);
        E.DrainBattleFx();
        Assert.That(Cmd(t, "basic_attack", 1).Ok, Is.True);
        var ev2 = One(E.DrainBattleFx());
        Assert.That(Carried(ev2.View!, "arrow"), Is.EqualTo(3));
        Assert.That(Carried(t, "arrow"), Is.EqualTo(2));
    }

    /// <summary>LIFE-Q756-01：医疗技能消耗的施术者随身物资（医用纱布）同理：结果帧前2，真实1。</summary>
    [Test]
    public void Q756_01_MedicalSkill_Supplies()
    {
        var s = Fight(75606, "worker");
        Ready(s);
        Arm(s, "kitchen_knife");
        Arm2(s, "trauma_bag");
        StandAt(s, 2);
        s.Skills["medicine"].Level = 10;
        s.Battle!.PlayerAp = 6;
        s.Stats.Health = 10;
        E.ApplyPlayerBleed(s);
        Only(s, "medical_gauze", 2);
        E.DrainBattleFx();
        Assert.That(Cmd(s, "medicine_03").Ok, Is.True);
        var ev = One(E.DrainBattleFx());
        Assert.That(Carried(ev.View!, "medical_gauze"), Is.EqualTo(2));
        Assert.That(Carried(s, "medical_gauze"), Is.EqualTo(1));
    }

    /// <summary>LIFE-Q756-02：操作台份数读显示状态（G.VS），播放结束回真实状态；显示快照只深拷人物随身／穿戴物品。</summary>
    [Test]
    public void Q756_02_ConsoleReadsDisplayState()
    {
        var bs = Web("Components", "BattleScreen.razor");
        Assert.That(bs, Does.Contain("return itemId == null ? 0 : G.VS.Items.Where(i => GameEngine.CarriedByMe(G.VS, i) && i.ItemId == itemId).Sum(i => i.Count);"), "医疗与战术品入口、小盘份数");
        Assert.That(bs, Does.Contain("return itemId == null ? null : $\"×{G.VS.Items.Where(i => GameEngine.CarriedByMe(G.VS, i) && i.ItemId == itemId).Sum(i => i.Count)}\";"), "指令图份数");
        Assert.That(bs, Does.Contain("var pi = e.WeaponOnPage(s, page, out var pw);"), "武器页弹仓读显示状态");
        var pb = Web("Services", "GameSession.Playback.cs");
        Assert.That(pb, Does.Contain("finally { if (gen == _fxGen) { FxView = null; _fxAfter.Clear(); } }"), "播放结束回真实库存");
        var s = New(75607);
        E.CreateItems(s, "stone", 1, s.Ground);
        var ground = s.Items.First(i => i.Owner == s.Ground);
        Only(s, "bandage", 2);
        var view = s.DisplayClone();
        Assert.That(view.Items, Has.Count.EqualTo(s.Items.Count));
        s.Items.First(i => i.ItemId == "bandage").Count = 1;
        Assert.That(Carried(view, "bandage"), Is.EqualTo(2), "随身物品在快照里定住");
        Assert.That(view.Items.Single(i => i.InstanceId == ground.InstanceId), Is.SameAs(ground), "其余物品仍引用原状态");
    }

    // ───────────── 源码读取 ─────────────

    private static string Root()
    {
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "AGENTS.md"))) dir = dir.Parent;
        return dir!.FullName;
    }

    private static string Src(params string[] parts) => File.ReadAllText(Path.Combine(new[] { Root() }.Concat(parts).ToArray())).Replace("\r\n", "\n");

    private static string Web(params string[] parts) => Src(new[] { "web", "Survival.Web" }.Concat(parts).ToArray());
}
