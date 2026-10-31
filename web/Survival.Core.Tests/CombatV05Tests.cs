using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 人物技能与回合制战斗参考策划案 v0.5 第 20 节验收 B501–B528（同名用例）。v0.6 冲突处以 v0.6 为准：
/// B512/B513 改为无夹紧、无取整语义；B501 按用户 2026-10-26 拍板“直接开启”改为断言战斗已启用。
/// </summary>
public class CombatV05Tests : BattleTestBase
{
    /// <summary>B501 原为“开关关闭时无敌人”。不适用：用户 2026-10-26 拍板全量接入、直接开启，不做开关。</summary>
    [Test]
    public void B501_NotApplicable_CombatEnabledByDecision()
    {
        Assert.That(Cfg.CombatEnabled, Is.True);
        var s = New();
        Assert.That(s.Skills, Is.Not.Empty, "技能已启用");
        Assert.That(s.WorldEnemies, Is.Not.Empty, "世界敌人已启用");
    }

    /// <summary>就绪：玩家先手并开始本次激活（2AP）。</summary>
    private void Ready(GameState s)
    {
        PlayerFirst(s);
        E.BattleStep(s);
        Assert.That(E.AwaitingPlayer(s), Is.True);
    }

    /// <summary>B502 战斗进入/结束：进入时敌踪清除、敌人交战锁定、世界交互锁住；结束后恢复，战利品落地。</summary>
    [Test]
    public void B502_EnterAndExit()
    {
        var s = Fight(1, "shambler");
        Assert.That(s.Encounter, Is.Null);
        Assert.That(s.WorldEnemies.Single().Engaged, Is.True);
        Assert.That(E.IsBusy(s), Is.True);
        Assert.That(E.Advance(s).Status, Is.EqualTo(SegmentStatus.Idle), "战斗中普通推进不动");
        Assert.That(E.CanStart(s, ActionRequest.Rest(30)), Does.Contain("战斗"));
        Ready(s);
        Enemy(s, 1).Hp = 0.05;
        Arm(s, "hatchet");
        Force(s, Below(0.01), AtLeast(0.99), Any(), Any());
        Assert.That(Cmd(s, "basic_attack", 1).Ok, Is.True);
        Assert.That(s.Battle, Is.Null, "最后一敌死亡即胜利");
        Assert.That(E.IsBusy(s), Is.False);
        Assert.That(s.WorldEnemies, Is.Empty, "死亡敌从世界移除");
        Assert.That(s.Log.Any(l => l.Text.StartsWith("战斗胜利")), Is.True);
    }

    /// <summary>B503 点击目标、查看预览、取消：不扣AP/弹药/体力，不掷骰。</summary>
    [Test]
    public void B503_PreviewChangesNothing()
    {
        var s = Fight(2, "shambler", "crawler");
        Ready(s);
        Arm(s, "pistol").LoadedAmmo = 8;
        var json = SaveSerializer.Serialize(s);
        E.PreviewCommand(s, BattleCommand.Of("basic_attack", 1));
        E.PreviewCommand(s, BattleCommand.Of("retreat"));
        E.BattleOptions(s);
        E.CommandBlocked(s, BattleCommand.Of("basic_attack", 2));
        Assert.That(SaveSerializer.Serialize(s), Is.EqualTo(json));
    }

    /// <summary>B504 同一射击 requestId 重复：成本、伤害、耐久只提交一次。</summary>
    [Test]
    public void B504_DuplicateRequest_OnlyOnce()
    {
        var s = Fight(3, "shambler", "shambler");
        Ready(s);
        var gun = Arm(s, "pistol");
        StandAt(s, 2);   // Q80：手枪使用位234
        gun.LoadedAmmo = 8;
        var d0 = gun.Durability;
        var c = BattleCommand.Of("basic_attack", 1).WithRequestId("shot-1");
        Assert.That(E.ExecuteCommand(s, c).Ok, Is.True);
        var json = SaveSerializer.Serialize(s);
        var again = E.ExecuteCommand(s, c);
        Assert.That(again.Duplicate, Is.True);
        Assert.That(SaveSerializer.Serialize(s), Is.EqualTo(json), "重复请求不改状态");
        Assert.That(gun.LoadedAmmo, Is.EqualTo(7));
        Assert.That(gun.Durability, Is.EqualTo(d0 - 1));
    }

    /// <summary>B505 弹仓0/武器耐久0：对应攻击禁用，可装填/换武器/徒手。</summary>
    [Test]
    public void B505_EmptyOrBroken_Disabled_AlternativesOk()
    {
        var s = Fight(4, "shambler");
        Ready(s);
        var gun = Arm(s, "pistol");
        Assert.That(E.CommandBlocked(s, BattleCommand.Of("basic_attack", 1)), Does.Contain("弹仓空"));
        Assert.That(E.CommandBlocked(s, BattleCommand.Of("reload")), Does.Contain("随身没有"));
        E.DebugGive(s, "ammo_9mm", 3);
        Assert.That(E.CommandBlocked(s, BattleCommand.Of("reload")), Is.Null);
        // B3 H6 改写：旧“换徒手/换随身武器 1AP”改为两个武器页 0AP 切换；武器Ⅱ空槽页即拳
        Assert.That(E.CommandBlocked(s, BattleCommand.Of("switch_weapon", null, 2)), Is.Null, "可切到空的武器Ⅱ页（拳）");
        var axe = E.CreateItems(s, "hatchet", 1, ItemInstance.EquipOwner(GameState.PlayerId, EquipSlots.Weapon2)).Single();
        axe.Durability = 0;
        Assert.That(Cmd(s, "switch_weapon", null, 2).Ok, Is.True);
        Assert.That(E.CommandBlocked(s, BattleCommand.Of("basic_attack", 1)), Does.Contain("耐久不足"));
        Assert.That(E.CommandBlocked(s, BattleCommand.Of("switch_weapon", null, 1)), Is.Null, "可切回手枪页");
        Assert.That(gun.IsWornBy(GameState.PlayerId), Is.True);

        Assert.That(E.BattleOptions(s).Any(o => o.Action == "aim"), Is.False, "没有瞄准");
    }

    /// <summary>B506 耐久1攻击且未命中：本次完成且耐久0，后续禁用，未凭命中返耐久。</summary>
    [Test]
    public void B506_LastDurabilityMiss()
    {
        var s = Fight(5, "shambler");
        Ready(s);
        var axe = Arm(s, "hatchet");
        axe.Durability = 1;
        Force(s, AtLeast(0.999));
        Assert.That(Cmd(s, "basic_attack", 1).Ok, Is.True);
        Assert.That(axe.Durability, Is.EqualTo(0), "未命中也扣");
        s.Battle!.PlayerAp = 3;   // B6：普通攻击2AP，3AP用掉2AP后先报行动点不足；这里只看耐久原因
        Assert.That(E.CommandBlocked(s, BattleCommand.Of("basic_attack", 1)), Does.Contain("耐久不足"));
        Assert.That(Enemy(s, 1).Hp, Is.EqualTo(Cfg.Enemies.Single(x => x.Id == "shambler").Hp));
    }

    /// <summary>B507 枪内弹药与随身弹药转移：总重量守恒，不复制弹药。</summary>
    [Test]
    public void B507_ReloadConservesWeight()
    {
        var s = Fight(6, "shambler");
        Ready(s);
        var gun = Arm(s, "pistol");
        E.DebugGive(s, "ammo_9mm", 12);
        var grams = E.CarriedGrams(s);
        Assert.That(Cmd(s, "reload").Ok, Is.True);
        Assert.That(gun.LoadedAmmo, Is.EqualTo(8));
        Assert.That(Count(s, "ammo_9mm", ItemInstance.Carried), Is.EqualTo(4));
        Assert.That(E.CarriedGrams(s), Is.EqualTo(grams), "装填前后总重量不变");
        Assert.That(E.CommandBlocked(s, BattleCommand.Of("reload")), Is.EqualTo("弹仓已满"));
    }

    /// <summary>
    /// B508 按新规则改写（R03 §1、A05 §1 目标位，取代 v0.5 §07 前排屏障与矛后排×0.75）：石刀目标位 12 打不到 3 号位；
    /// 木矛目标位 123 可打 3 号位，且伤害与打 1 号位相同（没有后排减伤）。
    /// </summary>
    [Test]
    public void B508_RearNeedsReach()
    {
        var s = Fight(7, "shambler", "shambler", "crawler");
        Ready(s);
        Assert.That(Enemy(s, 3).Slot, Is.EqualTo("3"));
        Arm(s, "stone_knife");
        Assert.That(E.CommandBlocked(s, BattleCommand.Of("basic_attack", 3)), Does.StartWith("射程不足").And.Contain("3号位").And.Contain("1、2号位"));
        Arm(s, "wooden_spear");
        Assert.That(E.CommandBlocked(s, BattleCommand.Of("basic_attack", 3)), Is.Null);
        var p = E.PreviewCommand(s, BattleCommand.Of("basic_attack", 3)).Targets.Single();
        Assert.That(p.RearPenalty, Is.False);
        var front = E.PreviewCommand(s, BattleCommand.Of("basic_attack", 1)).Targets.Single();
        Assert.That(p.DamageMax, Is.EqualTo(front.DamageMax).Within(1e-9));
    }

    /// <summary>
    /// B509 按 Q80 再改写（取代“完整指令后从后往前压紧”）：打倒 1 号位后空位保留，后面的敌人不自动往前补；
    /// 只打 1 号位的简易斧打不到留在 2 号位的敌人，要等它自己前移（或换能打 12 的武器）。
    /// </summary>
    [Test]
    public void B509_NoBarrier_RearReachable()
    {
        var s = Fight(8, "shambler", "shambler", "crawler");
        Ready(s);
        Arm(s, "hatchet");
        Assert.That(E.CommandBlocked(s, BattleCommand.Of("basic_attack", 2)), Does.StartWith("射程不足"), "斧只打1号位");
        Enemy(s, 1).Hp = 0.01;
        Force(s, Below(0.01), AtLeast(0.99), Any(), Any());
        Cmd(s, "basic_attack", 1);
        Assert.That((Enemy(s, 2).Slot, Enemy(s, 3).Slot), Is.EqualTo(("2", "3")), "Q80：不压紧，空位保留");
        s.Battle!.PlayerActing = true;
        s.Battle.PlayerAp = 2;
        Assert.That(E.CommandBlocked(s, BattleCommand.Of("basic_attack", 2)), Does.StartWith("射程不足"), "斧只打1号位，2号位的敌人没有补上来");
        Arm(s, "stone_knife");
        Assert.That(E.CommandBlocked(s, BattleCommand.Of("basic_attack", 2)), Is.Null, "刀打12，可以打2号位");
    }

    /// <summary>B510 按 Q80 再改写（不压紧）：每轮每人只行动一次，队列游标正确；后面的敌人靠自己的激活 1AP 前移，不会被自动补到 1 号位以外的空位。</summary>
    [Test]
    public void B510_RearAdvancesAtRoundEnd_EachActsOnce()
    {
        var s = Fight(9, "shambler", "crawler", "shambler");
        var b = s.Battle!;
        Ready(s);
        Enemy(s, 1).Hp = 0.01;
        Arm(s, "hatchet");
        Force(s, Below(0.01), AtLeast(0.99), Any(), Any());
        Cmd(s, "basic_attack", 1);
        Assert.That((Enemy(s, 2).Slot, Enemy(s, 3).Slot), Is.EqualTo(("2", "3")), "Q80：指令后不压紧");
        Cmd(s, "end_turn");
        var round = b.Round;
        var acted = new List<int>();
        while (s.Battle != null && b.Round == round)
        {
            var before = b.Cursor < b.Queue.Count ? b.Queue[b.Cursor] : -1;
            Step(s);
            if (before > 0) acted.Add(before);
        }
        Assert.That(acted, Is.Unique, "每个敌人每轮只激活一次");
        // B8a 按 Q87 改写：2 号位的爬行尸已能出手、1 号位空，3 号位的游荡尸前移时由爬行尸花自己本轮 1AP 补到 1、游荡尸再花 1AP 跟进到 2
        //（原断言“邻格交换、1 号位不补”被 Q87 取代）；仍是每只只走一格、不压紧
        Assert.That((Enemy(s, 2).Slot, Enemy(s, 3).Slot), Is.EqualTo(("1", "2")), "Q87：前方能出手的同伴先补位、后者跟进");
        Assert.That(b.Round, Is.EqualTo(round + 1));
    }

    /// <summary>
    /// B511 按新规则改写（R03 §3：普通激活 3AP、普通攻击 2AP、防御 1AP，取代“每次激活 2AP、普通攻击 1AP”）：
    /// 普通攻击后剩 1AP，2AP 的重劈禁用；再防御 1AP 用完即结束激活；下一次普通激活仍是 3AP，不积攒。
    /// </summary>
    [Test]
    public void B511_TwoApMax_NoCarry()
    {
        var s = Fight(10, "worker");
        Ready(s);
        Arm(s, "hatchet");
        s.Skills["axe"].Level = 3;   // B5：简易斧练“斧”
        Assert.That(s.Battle!.PlayerAp, Is.EqualTo(3));
        Cmd(s, "basic_attack", 1);
        Assert.That(s.Battle!.PlayerAp, Is.EqualTo(1));
        Assert.That(E.CommandBlocked(s, BattleCommand.Of("axe_03", 1)), Does.Contain("行动点不足"), "B7：2AP 的破甲劈（取代重劈）");
        Assert.That(E.CommandBlocked(s, BattleCommand.Of("basic_attack", 1)), Does.Contain("行动点不足"));
        Cmd(s, "guard");
        Assert.That(E.AwaitingPlayer(s), Is.False, "3AP 用完即结束激活");
        ToPlayer(s);
        if (s.Battle != null) Assert.That(s.Battle.PlayerAp, Is.EqualTo(3), "下一次普通激活仍是3AP，不积攒");
    }

    /// <summary>B512（v0.6 语义）低体力/低清醒/恐慌叠加：身体修正乘在命中能力上，概率=A/(A+D)，无5%—95%夹紧。</summary>
    [Test]
    public void B512_BodyFactorsMultiply_NoClamp()
    {
        var s = Fight(11, "shambler");
        Ready(s);
        Arm(s, "hatchet");
        SetStats(s, 20, 80, 80, 3.5, 15, 60);
        s.Battle!.Panic = 65;
        var t = E.PreviewCommand(s, BattleCommand.Of("basic_attack", 1)).Targets.Single();
        // B6 按新规则改写（R03 §5）：恐慌≥60 命中能力×0.9、≥90 改×0.8；B7c 按 Q67 改写：攻击能力+8，低体力且低清醒只乘一次×0.95（取代 B6-7 两条×0.85）
        var a = (24 + 3 * 1 + 8) * 0.95 * 0.9;
        Assert.That(t.AccuracyRating, Is.EqualTo(a).Within(1e-9), "按支付成本后的体力判低体力");
        Assert.That(t.HitChance, Is.EqualTo(a / (a + 25)).Within(1e-12));
        s.Battle.Panic = 95;
        Assert.That(E.PreviewCommand(s, BattleCommand.Of("basic_attack", 1)).Targets.Single().AccuracyRating, Is.EqualTo((24 + 3 + 8) * 0.95 * 0.8).Within(1e-9), "恐慌≥90改×0.8（择一）");
        SetStats(s, 20, 80, 80, 18, 90, 60);
        s.Battle.Panic = 0;
        // B5（A06 §1）：熟练度最高10级，简易斧改练“斧”；“无95%硬夹紧”改按公式核对
        s.Skills["axe"].Level = 2000;
        var hi = E.PreviewCommand(s, BattleCommand.Of("basic_attack", 1)).Targets.Single();
        Assert.That(hi.AccuracyRating, Is.EqualTo(24 + 3 * 10 + 8), "状态里超过10级按10级取用（+8 为 Q67 基础加值）");
        Assert.That(GameEngine.HitChance(24 + 3 * 2000, 25), Is.GreaterThan(0.95).And.LessThan(1), "公式无95%硬夹紧，也不超过1");
    }

    /// <summary>B513（v0.6 语义）伤害、护甲、防御：先护甲后防御×0.5，不取整、无最低1伤；出血不被护甲或防御减免。</summary>
    [Test]
    public void B513_ArmorThenGuard_NoRounding()
    {
        var s = Fight(12, "worker");
        Ready(s);
        Arm(s, "hatchet");
        var t = Enemy(s, 1);
        t.Guard = true;
        var hp0 = t.Hp;
        Force(s, Below(0.001), AtLeast(0.999), x => Math.Abs(x - 0.5) < 0.01);
        var dmgRoll = 0.0;
        { var r = new Rng { State = s.Rng.State }; r.NextDouble(); r.NextDouble(); dmgRoll = r.NextDouble(); }
        Cmd(s, "basic_attack", 1);
        var raw = 1.6 + dmgRoll * (2.4 - 1.6);
        var expect = (raw * 1.035 - (0.3 - 0.1)) * 0.5;   // B6：简易斧“劈砍”忽略护甲0.1（A05 §1）作为穿透
        Assert.That(hp0 - t.Hp, Is.EqualTo(expect).Within(1e-9));
        Assert.That(t.Guard, Is.False, "防御只挡一次");
        // 玩家出血：激活开始扣0.4，不吃防御
        s.Battle!.PlayerGuard = true;
        s.Body.BleedTriggers = 3;
        s.Body.BleedDamage = 0.4;
        Cmd(s, "end_turn");
        var h0 = s.Stats.Health;
        var b = s.Battle!;
        while (s.Battle != null && !E.AwaitingPlayer(s))
        {
            if (b.Cursor < b.Queue.Count && b.Queue[b.Cursor] > 0) { b.Cursor++; continue; }   // 跳过敌方行动，只看出血
            E.BattleStep(s);
        }
        Assert.That(s.Body.BleedTriggers, Is.EqualTo(2));
        Assert.That(h0 - s.Stats.Health, Is.EqualTo(0.4).Within(1e-9), "出血不减免");
    }

    /// <summary>B514 包扎冷却中且出血：只止血不回血，耗1绷带2AP；无出血则禁用。</summary>
    [Test]
    public void B514_BandageCooldown_StopsBleedOnly()
    {
        var s = Fight(13, "shambler");
        Ready(s);
        E.DebugGive(s, "bandage", 2);
        SetStats(s, 15, 80, 80, 18, 90, 60);
        s.BandageReadyAt = s.GameMinute + 30;
        Assert.That(E.CommandBlocked(s, BattleCommand.Of("bandage")), Does.Contain("冷却"), "无出血、冷却中禁用");
        s.Body.BleedTriggers = 3;
        s.Body.BleedDamage = 0.4;
        Assert.That(Cmd(s, "bandage").Ok, Is.True);
        Assert.That(s.Body.BleedTriggers, Is.EqualTo(0));
        Assert.That(s.Stats.Health, Is.EqualTo(15), "冷却中不回血");
        Assert.That(s.BandageReadyAt, Is.EqualTo(s.GameMinute + 30), "只止血不延长回血冷却");
        Assert.That(Count(s, "bandage"), Is.EqualTo(1));
        Assert.That(s.Battle!.PlayerAp, Is.EqualTo(1), "包扎2AP（B6：3AP 里剩1AP）");
    }

    /// <summary>B515 回合中使用绷带：不额外跳旧5分钟，仅轮末一次 tick。</summary>
    [Test]
    public void B515_BandageNoExtraFiveMinutes()
    {
        var s = Fight(14, "stag");
        Ready(s);
        E.DebugGive(s, "bandage", 1);
        SetStats(s, 15, 80, 80, 18, 90, 60);
        var m0 = s.GameMinute;
        Cmd(s, "bandage");
        Assert.That(s.GameMinute, Is.EqualTo(m0), "确认时不推进时间");
        Assert.That(s.Stats.Health, Is.GreaterThan(15));
        while (s.Battle!.Round == 1) Step(s);
        Assert.That(s.GameMinute, Is.EqualTo(m0 + 5), "轮末一次5分钟");
        Assert.That(E.Start(s, ActionRequest.UseItem("bandage")).Ok, Is.False, "战斗中不走旧5分钟自用");
    }

    /// <summary>B516 一轮并行料理与两次攻击：料理/燃料只推进5分钟，体力只付招式成本（不再套采集消耗）。</summary>
    [Test]
    public void B516_OneRoundOneTick_EnergyOnlyCommandCost()
    {
        var s = New(15);
        // 在营地火堆上烧水，同时在营地与野犬交战（调试放置）
        var pot = First(s, "pot");
        E.DebugGive(s, "dirty_water", 2, "camp");
        E.DoInstant(s, InstantRequest.FillPot(First(s, "dirty_water", "camp").InstanceId, pot.InstanceId, 2));
        E.DoInstant(s, InstantRequest.PutInFire(pot.InstanceId));
        var fac = s.Locations["camp"].Facility;
        fac.FireLit = true;
        fac.FuelMinutes = 60;
        s.SpawnNextCheck = int.MaxValue;
        s.PatrolNextCheck = int.MaxValue;
        var dog = E.DebugSpawnEnemy(s, "feral_dog", "camp");
        s.Encounter = new PendingEncounter { LocationId = "camp", EnemyIds = { dog.Id } };
        E.Engage(s);
        Ready(s);
        Arm(s, "hatchet");
        var e0 = s.Stats.Energy;
        var heat0 = E.ProcessOf(s, pot)!.HeatDoneMinutes;
        Force(s, AtLeast(0.999));
        Cmd(s, "basic_attack", 1);
        Cmd(s, "end_turn");   // B6：普通攻击 2AP，一次激活只够一击
        var cost = E.SkillEnergyCost(0.65, 1);
        Assert.That(e0 - s.Stats.Energy, Is.EqualTo(cost).Within(1e-9));
        var b = s.Battle!;
        while (s.Battle != null && b.Round == 1)
        {
            if (b.Cursor < b.Queue.Count && b.Queue[b.Cursor] > 0) { b.Cursor++; continue; }
            Step(s);
        }
        Assert.That(E.ProcessOf(s, pot)!.HeatDoneMinutes - heat0, Is.EqualTo(5), "料理只推进5分钟");
        Assert.That(fac.FuelMinutes, Is.EqualTo(55));
        Assert.That(e0 - s.Stats.Energy, Is.EqualTo(cost).Within(1e-9), "轮末 tick 体力自然率0");
    }

    /// <summary>B517 最后一敌在轮中被杀：只结算本轮5分钟，不重复推进。</summary>
    [Test]
    public void B517_MidRoundVictory_SingleTick()
    {
        var s = Fight(16, "shambler");
        Ready(s);
        Arm(s, "hatchet");
        Enemy(s, 1).Hp = 0.01;
        var m0 = s.GameMinute;
        Force(s, Below(0.01), AtLeast(0.99), Any(), Any());
        Cmd(s, "basic_attack", 1);
        Assert.That(s.Battle, Is.Null);
        Assert.That(s.GameMinute, Is.EqualTo(m0 + 5));
    }

    /// <summary>B518 世界 tick 伤害导致死亡：优先死亡，无胜利掉落或复活。</summary>
    [Test]
    public void B518_TickDeath_NoLoot()
    {
        var s = Fight(17, "shambler");
        Ready(s);
        Arm(s, "hatchet");
        Enemy(s, 1).Hp = 0.01;
        SetStats(s, 0.01, 80, 0, 18, 90, 60);
        Force(s, Below(0.01), AtLeast(0.99), Any(), Below(0.5));
        Cmd(s, "basic_attack", 1);
        Assert.That(s.Progress.Dead, Is.True, "退出时的 tick 致死");
        Assert.That(s.Items.Count(i => i.Owner == "field" && i.ItemId is "cloth" or "scrap"), Is.EqualTo(0), "不提交掉落");
        Assert.That(s.Battle, Is.Null);
    }

    /// <summary>B519 出血战后继承：次数正确，结束瞬间不重复扣一次，之后每个世界 tick 扣。</summary>
    [Test]
    public void B519_BleedCarriesOver()
    {
        var s = Fight(18, "shambler");
        Ready(s);
        Arm(s, "hatchet");
        Enemy(s, 1).Hp = 0.01;
        s.Body.BleedTriggers = 2;
        s.Body.BleedDamage = 0.4;
        var h0 = s.Stats.Health;
        Force(s, Below(0.01), AtLeast(0.99), Any(), Any());
        Cmd(s, "basic_attack", 1);
        Assert.That(s.Battle, Is.Null);
        Assert.That(s.Body.BleedTriggers, Is.EqualTo(2), "结束的那一段不额外触发");
        Assert.That(s.Stats.Health, Is.EqualTo(h0).Within(1e-9));
        Start(s, ActionRequest.Wait(30));
        Steps(s, 1);
        Assert.That(s.Body.BleedTriggers, Is.EqualTo(1));
        Assert.That(h0 - s.Stats.Health, Is.EqualTo(0.4).Within(1e-9));
    }

    /// <summary>B520 撤退失败后读取：不重掷；已消耗AP与敌方队列位置保留。</summary>
    [Test]
    public void B520_FailedRetreat_ReloadNoReroll()
    {
        var s = Fight(19, "runner");
        Ready(s);
        Force(s, AtLeast(0.999));
        Cmd(s, "retreat");
        Assert.That(s.Battle, Is.Not.Null, "撤退失败");
        Assert.That(E.AwaitingPlayer(s), Is.False, "失败耗尽行动点");
        var json = SaveSerializer.Serialize(s);
        var t = SaveSerializer.Deserialize(json);
        Assert.That(t.Battle!.Cursor, Is.EqualTo(s.Battle!.Cursor));
        Assert.That(E.CommandBlocked(t, BattleCommand.Of("retreat")), Is.Not.Null, "不能读档重掷");
        E.BattleStep(s);
        E.BattleStep(t);
        Assert.That(SaveSerializer.Serialize(t), Is.EqualTo(SaveSerializer.Serialize(s)), "读档后继续同一序列");
    }

    /// <summary>
    /// B521 按新规则改写（R05 §4、R03 §1 容量 C：场上存活加增援、本场累计投入都不超过 C，取代“敌满3等空位”）：
    /// 容量 3、开场 3 敌时打倒一只后累计投入仍是 3，噪声满也不增援；开场 2 敌时下一轮补进 3 号位，整场最多一次，只调用同地现有丧尸。
    /// </summary>
    [Test]
    public void B521_Reinforcement_CapacityCumulative()
    {
        var full = Fight(20, "shambler", "shambler", "shambler");
        E.DebugSpawnEnemy(full, "crawler", "field");
        Ready(full);
        Assert.That(full.Battle!.Capacity, Is.EqualTo(3));
        full.Battle.ReinforcementRequested = true;
        Enemy(full, 1).Dead = true;
        Enemy(full, 1).Slot = "";
        Cmd(full, "end_turn");
        var fb = full.Battle!;
        while (full.Battle != null && fb.Round == 1) { if (fb.Cursor < fb.Queue.Count && fb.Queue[fb.Cursor] > 0) { fb.Cursor++; continue; } Step(full); }
        Assert.That(fb.Enemies.Count, Is.EqualTo(3), "累计投入已达容量C=3，不增援");
        Assert.That(fb.ReinforcementUsed, Is.False);

        var s = Fight(20, "shambler", "shambler");
        var extra1 = E.DebugSpawnEnemy(s, "crawler", "field");
        var extra2 = E.DebugSpawnEnemy(s, "shambler", "field");
        Ready(s);
        var gun = Arm(s, "pistol");
        StandAt(s, 2);   // Q80：手枪使用位234
        gun.LoadedAmmo = 8;
        s.Battle!.Noise = 75;
        Force(s, AtLeast(0.999));
        Cmd(s, "basic_attack", 1);
        Assert.That(s.Battle.ReinforcementRequested, Is.True);
        var live = E.LiveCount(s);
        Cmd(s, "end_turn");
        var b = s.Battle;
        while (s.Battle != null && b.Round == 1) { if (b.Cursor < b.Queue.Count && b.Queue[b.Cursor] > 0) { b.Cursor++; continue; } Step(s); }
        Assert.That(b.Enemies.Count, Is.EqualTo(3), "下一轮轮初补进");
        Assert.That(b.Enemies.Last().Slot, Is.EqualTo("3"), "排在最后一个存活敌人身后的空位（B7d-4）");
        Assert.That(b.Enemies.Last().WorldId, Is.EqualTo(extra1.Id), "调用同地现有、最早的丧尸，保留原ID与类型");
        Assert.That(b.Enemies.Last().EnemyId, Is.EqualTo("crawler"));
        Assert.That(E.LiveCount(s), Is.EqualTo(live), "世界活敌总数不增加");
        Assert.That(b.ReinforcementUsed, Is.True);
        Assert.That(s.WorldEnemies.Single(w => w.Id == extra2.Id).Engaged, Is.False, "整场最多一次");
    }

    /// <summary>B522 同敌踪撤退后重进：死敌不复生，掉落不重复，存活敌HP与经验计数保留。</summary>
    [Test]
    public void B522_RetreatAndReengage()
    {
        var s = Fight(21, "shambler", "shambler");
        s.Player.TacticalTier = 2;   // B6：5AP 才够两次 2AP 普通攻击
        Ready(s);
        Arm(s, "kitchen_knife");   // Q80：不再压紧，2号位的敌人要用打12的刀
        Enemy(s, 1).Hp = 0.01;
        Force(s, Below(0.01), AtLeast(0.99), Any(), Any());
        Cmd(s, "basic_attack", 1);
        Force(s, Below(0.01), AtLeast(0.99), x => x < 0.1);
        Cmd(s, "basic_attack", 2);
        var hurt = Enemy(s, 2).Hp;
        var wid = Enemy(s, 2).WorldId;
        Assert.That(hurt, Is.LessThan(3.6));
        Cmd(s, "end_turn");   // B6：5AP 用掉4AP，剩1AP不够撤退（2AP）
        ToPlayer(s);
        Force(s, Below(0.0001));
        Cmd(s, "retreat");
        Assert.That(s.Battle, Is.Null, "撤退成功");
        Assert.That(s.CurrentLocation, Is.EqualTo("field"), "回当前地块，不传回营地");
        Assert.That(s.WorldEnemies.Single().Id, Is.EqualTo(wid), "死敌不复生");
        Assert.That(s.WorldEnemies.Single().Hp, Is.EqualTo(Enemy2Hp(hurt, s)).Within(1e-9));
        Assert.That(s.WorldEnemies.Single().XpHits["blade"], Is.EqualTo(1), "经验计数随 worldEnemyId 保存");
        Assert.That(s.Items.Count(i => i.Owner == "field" && i.ItemId is "cloth" or "scrap"), Is.EqualTo(0), "撤退不领取掉落");
        Assert.That(s.EncounterGraceUntil, Is.EqualTo(s.GameMinute + 60));
        // 主动重进：同一实例、HP 保留
        Assert.That(E.Approach(s, wid).Ok, Is.True, "保护期不挡主动接敌");
        Assert.That(s.Battle!.Enemies.Single().Hp, Is.EqualTo(s.WorldEnemies.Single().Hp));
        Assert.That(s.Battle.Enemies.Single().XpHits["blade"], Is.EqualTo(1));
    }

    private static double Enemy2Hp(double hurt, GameState s) => hurt;

    /// <summary>B523 探索完成后遇敌：进度/随机物资已提交，不因战斗回滚或重抽（另见 R618）。</summary>
    [Test]
    public void B523_ExploreCommittedBeforeBattle()
    {
        var s = New(22);
        E.DebugTeleport(s, "field");
        s.GameMinute = At(2, 9, 0);
        s.SpawnNextCheck = int.MaxValue;
        s.PatrolNextCheck = int.MaxValue;
        Start(s, ActionRequest.Explore());
        ForceLoot(s, "fiber");
        s.Main!.EncounterRoll = 0;
        Run(s);
        Assert.That(s.Encounter, Is.Not.Null);
        var progress = s.Locations["field"].Progress;
        var fiber = Count(s, "fiber", "field");
        E.Engage(s);
        Assert.That(s.Locations["field"].Progress, Is.EqualTo(progress));
        Assert.That(Count(s, "fiber", "field"), Is.EqualTo(fiber));
    }

    /// <summary>B524 制造/医疗/群攻经验：只按规则发一次；预览、取消、满血空治疗不发；群攻按命中目标逐个+2（v1.0 P01）。</summary>
    [Test]
    public void B524_CraftMedicalAndGroupXp_OnlyOnce()
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
        E.DebugGive(s, "bandage", 2);
        SetStats(s, 20, 80, 80, 18, 90, 60);
        Assert.That(E.CanStart(s, ActionRequest.UseItem("bandage")), Is.Not.Null, "满血、未感染、无出血时禁用");
        SetStats(s, 15, 80, 80, 18, 90, 60);
        Start(s, ActionRequest.UseItem("bandage"));
        Run(s);
        Assert.That(s.Skills["medicine"].Xp, Is.EqualTo(Cfg.Xp.Medical), "真实恢复健康+4，只发一次");

        // 群攻：霰弹专长射击（B7 取代散射，枪械7级）命中两个前排各 +2
        var f = Fight(23, "shambler", "shambler");
        Ready(f);
        var sg = Arm(f, "shotgun");
        StandAt(f, 2);   // Q80：霰弹枪使用位23
        sg.LoadedAmmo = 4;
        f.Skills["firearms"].Level = 7;
        var xp0 = f.Skills["firearms"].Xp;
        Enemy(f, 1).Hp = 50;   // 不让第一个目标倒下（倒下会抽掉落随机数）
        Enemy(f, 2).Hp = 50;
        Force(f, Below(0.4), AtLeast(0.1), Any(), Below(0.4), AtLeast(0.1), Any());
        Assert.That(Cmd(f, "firearms_07").Ok, Is.True);
        Assert.That(f.Skills["firearms"].Xp - xp0, Is.EqualTo(2 * Cfg.Xp.DamagingAttackCommand), "v1.0 P01：群攻逐命中目标计，两个都命中+4");
        Assert.That(sg.LoadedAmmo, Is.EqualTo(3), "只耗1发");
        // 同一敌人同技能最多计3次
        var g = Fight(24, "riot");
        Ready(g);
        Arm(g, "hatchet");
        var hits = 0;
        for (var k = 0; k < 16 && g.Battle != null; k++)
        {
            if (!E.AwaitingPlayer(g)) { ToPlayer(g); continue; }
            Enemy(g, 1).Hp = 7;
            Force(g, Below(0.01), AtLeast(0.99), Any());
            if (Cmd(g, "basic_attack", 1).Ok) hits++;
            else Cmd(g, "end_turn");   // B6：剩 1AP 不够普通攻击
        }
        Assert.That(hits, Is.GreaterThan(3));
        Assert.That(g.Skills["axe"].Xp, Is.EqualTo(3 * Cfg.Xp.DamagingAttackCommand), "同一世界敌人对同技能最多3次");
    }

    /// <summary>制造熟练度折减工作量，同批次余量结转：高等级批量在更少段内完成（v0.6 §03）。</summary>
    [Test]
    public void CraftSkill_ReducesWork_BatchCarryOver()
    {
        var s = New();
        E.DebugGive(s, "cloth", 10);
        E.DebugGive(s, "herb", 10);
        // B7b 按新规则改写（Q57 选 B：取消制造等级工作量倍率 1/(1+0.025L)）：制造10级整批仍是 15×4=60 分钟
        s.Skills["crafting"].Level = 10;
        Start(s, ActionRequest.Craft("bandage", batchCount: 4));
        Assert.That(s.Main!.BaseWorkMinutes, Is.EqualTo(15 * 4).Within(1e-9), "整批60分钟工作量，制造等级不折减");
        Run(s);
        Assert.That(s.GameMinute - 480, Is.EqualTo(60), "4件合计60分钟");
        Assert.That(Count(s, "bandage"), Is.EqualTo(4));
        var t = New();
        var b = ActionRequest.Craft("shelter");
        b.BatchCount = 2;
        Assert.That(E.CanStart(t, b), Does.Contain("只能做1件"));
    }

    /// <summary>B525 狼/鹿逃走：无尸体，不把逃离当击杀。</summary>
    [Test]
    public void B525_WolfFlees_NoCarcass()
    {
        var s = Fight(25, "wolf");
        Ready(s);
        Arm(s, "hatchet");
        Enemy(s, 1).Hp = 2.5;
        Force(s, Below(0.01), AtLeast(0.99), x => x < 0.05);
        Cmd(s, "basic_attack", 1);
        Assert.That(Enemy(s, 1).WillFlee, Is.True, "≤1.2HP 下次行动逃离");
        Assert.That(Enemy(s, 1).IntentLabel, Is.EqualTo("逃离"), "意图公开改为逃离");
        Cmd(s, "end_turn");
        ToPlayer(s);
        Assert.That(s.Battle, Is.Null, "唯一的敌人逃离，战斗结束");
        Assert.That(Count(s, "carcass_wolf"), Is.EqualTo(0), "无尸体");
        Assert.That(s.WorldEnemies.Count(w => w.EnemyId == "wolf"), Is.EqualTo(1), "逃离不删除，迁走或隐藏");
    }

    /// <summary>B526 处理尸体取消/到期：不同时保留尸体和肉，不刷新源保质；成功磨损刀斧1。</summary>
    [Test]
    public void B526_Butcher_CancelAndExpiry()
    {
        var s = New();
        var carcass = E.CreateItems(s, "carcass_feral_dog", 1, "camp").Single();
        var exp = carcass.ExpiresAt;
        Assert.That(E.CanStart(s, ActionRequest.Butcher(carcass.InstanceId)), Does.Contain("需要随身有效"));
        var knife = E.CreateItems(s, "stone_knife", 1, ItemInstance.Carried).Single();
        var d0 = knife.Durability!.Value;
        s.Skills["crafting"].Level = 100;
        Start(s, ActionRequest.Butcher(carcass.InstanceId));
        Assert.That(s.Main!.BaseWorkMinutes, Is.EqualTo(30), "制造熟练不加速");
        Steps(s, 2);
        E.Cancel(s);
        Assert.That(Count(s, "carcass_feral_dog", "camp"), Is.EqualTo(1), "取消返还原尸体");
        Assert.That(First(s, "carcass_feral_dog").ExpiresAt, Is.EqualTo(exp), "保质期不刷新");
        Assert.That(Count(s, "raw_meat"), Is.EqualTo(0));
        Start(s, ActionRequest.Butcher(carcass.InstanceId));
        Run(s);
        Assert.That(Count(s, "carcass_feral_dog"), Is.EqualTo(0), "尸体与产物不同时存在");
        Assert.That(Count(s, "raw_meat", "camp"), Is.EqualTo(2));
        Assert.That(Count(s, "hide", "camp"), Is.EqualTo(1));
        Assert.That(knife.Durability, Is.EqualTo(d0 - 1).Within(1e-9));
        // 完成时已腐败：中止，没有产物，尸体留地面
        var t = New();
        var c2 = E.CreateItems(t, "carcass_wolf", 1, "camp").Single();
        E.CreateItems(t, "hatchet", 1, ItemInstance.Carried);
        Start(t, ActionRequest.Butcher(c2.InstanceId));
        c2.ExpiresAt = t.GameMinute + 10;
        Run(t);
        Assert.That(Count(t, "raw_meat"), Is.EqualTo(0));
        Assert.That(Count(t, "carcass_wolf", "camp"), Is.EqualTo(1));
    }

    /// <summary>生肉→烤肉（火堆直烤）/水煮肉（锅内净水500毫升），锅白名单与2个食材位（v0.5 §14）。</summary>
    [Test]
    public void Meat_RoastAndBoil()
    {
        var s = New();
        E.DebugGive(s, "raw_meat", 3, "camp");
        Assert.That(E.CanStart(s, ActionRequest.UseItem("raw_meat")), Does.Contain("烹饪"), "生肉不可直接食用");
        var fac = s.Locations["camp"].Facility;
        E.DoInstant(s, InstantRequest.PutInFire(First(s, "raw_meat", "camp").InstanceId, 1));
        var pot = First(s, "pot");
        E.DebugGive(s, "clean_water", 2, "camp");
        E.DoInstant(s, InstantRequest.FillPot(First(s, "clean_water", "camp").InstanceId, pot.InstanceId, 2));
        Assert.That(E.DoInstant(s, InstantRequest.PutFoodInPot(First(s, "raw_meat", "camp").InstanceId, pot.InstanceId, 2)).Ok, Is.True);
        E.DoInstant(s, InstantRequest.PutInFire(pot.InstanceId));
        fac.FireLit = true;
        fac.FuelMinutes = 120;
        Start(s, ActionRequest.Wait(60));
        Run(s);
        Assert.That(Count(s, "cooked_meat"), Is.EqualTo(1), "直烤30分钟→烤肉");
        Assert.That(Count(s, "boiled_meat"), Is.EqualTo(2), "锅里30分钟→水煮肉，每份耗净水500毫升");
        Assert.That(E.PotUnits(s, pot.InstanceId), Is.EqualTo(0));
        var cooked = Cfg.Items.Single(i => i.Id == "cooked_meat").Use!;
        Assert.That((cooked.Satiety, cooked.Hydration, cooked.Morale), Is.EqualTo((35.0, -3.0, 2.0)));
    }

    /// <summary>武器取得（v0.5 §15）：木矛/木弓/箭矢配方；五金店武器柜（50%）、村庄旧储物柜（75%，空手枪+9mm弹12）一次性；开发测试箱只经调试入口。</summary>
    [Test]
    public void WeaponAcquisition()
    {
        var s = New();
        E.DebugGive(s, "wood", 5);
        E.DebugGive(s, "stone", 2);
        E.DebugGive(s, "fiber", 7);
        E.DebugGive(s, "cloth", 1);
        Start(s, ActionRequest.Craft("make_wooden_spear"));
        Run(s);
        Assert.That(Count(s, "wooden_spear"), Is.EqualTo(1));
        Start(s, ActionRequest.Craft("make_arrows"));
        Run(s);
        Assert.That(Count(s, "arrow"), Is.EqualTo(4));
        E.DebugGive(s, "wood", 1);
        E.DebugGive(s, "fiber", 4);
        Start(s, ActionRequest.Craft("make_wooden_bow"));
        Run(s);
        Assert.That(Count(s, "wooden_bow"), Is.EqualTo(1));

        var h = New();
        E.DebugTeleport(h, "hardware");
        Assert.That(h.Resources["hardware_weapon_cache"].Discovered, Is.False);
        E.DebugSetProgress(h, "hardware", 50);
        Assert.That(h.Resources["hardware_weapon_cache"].Discovered, Is.True);
        Start(h, ActionRequest.Gather("hardware_weapon_cache"));
        Run(h);
        Assert.That(Count(h, "crowbar", "hardware") + Count(h, "kitchen_knife", "hardware"), Is.EqualTo(2));
        Assert.That(GameEngine.Condition(First(h, "crowbar")), Is.InRange(0.6, 1.0), "拾荒成色");

        var v = New();
        E.DebugTeleport(v, "village");
        E.DebugSetProgress(v, "village", 75);
        Start(v, ActionRequest.Gather("village_weapon_locker"));
        Run(v);
        Assert.That(First(v, "pistol").LoadedAmmo, Is.EqualTo(0), "新手枪空弹仓");
        Assert.That(Count(v, "ammo_9mm", "village"), Is.EqualTo(12));
        Assert.That(E.CanStart(v, ActionRequest.Gather("village_weapon_locker")), Does.Contain("搜空"));

        // v1.0 P05/T04：金属矛、弩、弩矢可正常制作；霰弹枪、步枪仍不开放正常来源
        foreach (var id in new[] { "metal_spear", "crossbow", "bolt" })
            Assert.That(Cfg.Recipes.Any(r => r.Outputs.ContainsKey(id)), Is.True, id + " 有正常配方");
        foreach (var id in new[] { "shotgun", "rifle" })
        {
            Assert.That(Cfg.Recipes.Any(r => r.Outputs.ContainsKey(id)), Is.False, id + " 无正常配方");
            Assert.That(Cfg.Resources.Any(r => r.Outputs.ContainsKey(id)), Is.False, id + " 无正常箱子");
            Assert.That(Cfg.DevKit.Items.ContainsKey(id), Is.True, id + " 只在开发测试箱");
        }
        var d = New();
        E.DebugGiveDevKit(d);
        Assert.That(Count(d, "rifle"), Is.EqualTo(1));
        Assert.That(E.RootCardCount(d), Is.EqualTo(Cfg.Carry.MaxRootCards), "开发测试箱放不下的放当前地面（S06 §8、开发假设 H4）");
        Assert.That(d.Items.Count(i => i.Owner == d.CurrentLocation && Cfg.DevKit.Items.ContainsKey(i.ItemId)), Is.GreaterThan(0));
    }

    /// <summary>B527 地面或火堆内的武器/绷带：战斗不能远程使用，只读随身。</summary>
    [Test]
    public void B527_OnlyCarriedUsable()
    {
        var s = Fight(26, "shambler");
        Ready(s);
        E.DebugGive(s, "bandage", 1, "field");
        SetStats(s, 10, 80, 80, 18, 90, 60);
        Assert.That(E.CommandBlocked(s, BattleCommand.Of("bandage")), Does.Contain("随身没有"));
        var ground = E.CreateItems(s, "hatchet", 1, "field").Single();
        // B3 H6 改写：战内只能在两个已锁定的武器页之间切换，不能从地面或随身换入第三把
        Assert.That(E.CommandBlocked(s, BattleCommand.Of("switch_weapon", null, ground.InstanceId)), Does.Contain("只有武器Ⅰ、武器Ⅱ两页"));
        Assert.That(E.EquipBlocked(s, GameState.PlayerId, ground.InstanceId, EquipSlots.Weapon1), Does.Contain("战斗中"));
        Assert.That(E.DoInstant(s, InstantRequest.Pickup(ground.InstanceId, 1)).Ok, Is.False, "战斗中不能拾取");

    }

    /// <summary>B528 不足体力且空武器：仍可勉强拳击、防御、恢复呼吸或撤退，无输入死锁。</summary>
    [Test]
    public void B528_NoDeadlock()
    {
        var s = Fight(27, "shambler");
        Ready(s);
        Arm(s, "pistol");
        SetStats(s, 20, 80, 80, 0, 90, 60);
        Assert.That(E.CommandBlocked(s, BattleCommand.Of("basic_attack", 1)), Is.Not.Null);
        Assert.That(E.CommandBlocked(s, BattleCommand.Of("desperate_punch", 1)), Is.Null);
        Assert.That(E.CommandBlocked(s, BattleCommand.Of("guard")), Is.Null);
        Assert.That(E.CommandBlocked(s, BattleCommand.Of("breathe")), Is.Null);
        Assert.That(E.CommandBlocked(s, BattleCommand.Of("retreat")), Is.Null);
        var p = E.PreviewCommand(s, BattleCommand.Of("retreat"));
        Assert.That(p.EnergyCost, Is.EqualTo(0), "不足体力强行撤退不耗体力");
        Assert.That(p.RetreatChance, Is.LessThan(E.RetreatChance(Clone(s, 18), s.Battle!).chance), "成功率再×0.75");
        Assert.That(Cmd(s, "breathe").Ok, Is.True);
        Assert.That(s.Stats.Energy, Is.EqualTo(1.2).Within(1e-9), "B6 按新规则改写（R03 §3）：调息 E+1.2");
    }

    private GameState Clone(GameState s, double energy)
    {
        var c = s.Clone();
        c.Stats.Energy = energy;
        return c;
    }
}
