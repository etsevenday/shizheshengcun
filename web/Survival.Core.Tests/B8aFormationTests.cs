using Newtonsoft.Json.Linq;
using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 B8a（策划提交 51304bb 答复 Q86）R103：顶栏编队与真实开战预设。
/// 出处：R08/S14/S01/R03「Q86 顶栏编队与真实开战预设」本轮章、combat-resolution-adjudications.json formation；验收 A10 POS-Q86-01—05。
/// 界面草稿（点角色再点号位、取消/Esc、无改动禁保存的按钮态、三档屏幕可达）在网页壳里，内核这里验保存/校验/迁移/开战读取。
/// </summary>
public class B8aFormationTests : BattleTestBase
{
    private static JObject Formation() =>
        (JObject)JObject.Parse(File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "design.a12.json")))["combatResolutionAdjudications"]!["formation"]!;

    private GameState Camp(int c = 1, uint seed = 8601)
    {
        var s = New(seed);
        UnlockCapacity(s, c);
        s.SpawnNextCheck = int.MaxValue;
        s.PatrolNextCheck = int.MaxValue;
        Assert.That(s.CurrentLocation, Is.EqualTo("camp"));
        return s;
    }

    /// <summary>从当前局面去农野荒地以敌踪方式入战（不改编队预设）。</summary>
    private GameState FightFrom(GameState s, params string[] enemyIds)
    {
        E.DebugTeleport(s, "field");
        s.GameMinute = At(2, 9, 0);
        s.WorldEnemies.Clear();
        var ids = enemyIds.Select(id => E.DebugSpawnEnemy(s, id, "field").Id).ToList();
        s.Encounter = new PendingEncounter { LocationId = "field", EnemyIds = ids, Source = "explore", Minute = s.GameMinute };
        Assert.That(E.Engage(s).Ok, Is.True);
        return s;
    }

    private static string[] Slots(params string[] x) => x;

    // ───────────── POS-Q86-01：单人 C1 可选 1—4，下一战按预设，不按武器自动改位 ─────────────

    [Test]
    public void POS_Q86_01_SoloC1_AnySlot_NextBattleUsesPreset_NoWeaponAutoPlacement()
    {
        var f = Formation();
        Assert.That((bool)f["availableBeforeRecruitment"]!, Is.True);
        Assert.That((bool)f["capacityLimitsCountNotSlot"]!, Is.True);
        Assert.That((bool)f["weaponAutoPlacement"]!, Is.False);
        Assert.That(((JArray)f["slots"]!).Select(x => (int)x!), Is.EqualTo(new[] { 1, 2, 3, 4 }));

        var s = Camp(1);
        Assert.That(E.PartyCapacity(s), Is.EqualTo(1));
        Assert.That(s.FormationPreset, Is.EqualTo(Slots("p0", "", "", "")), "新局单人在 1 号位");
        foreach (var slot in new[] { 2, 3, 4, 1 })
        {
            var want = Enumerable.Repeat("", 4).ToArray();
            want[slot - 1] = "p0";
            Assert.That(E.SaveFormation(s, want, s.FormationVersion).Ok, Is.True, $"C1 单人也能选 {slot} 号位");
            Assert.That(s.FormationPreset, Is.EqualTo(want));
        }
        // 手斧只能在 1、2 号位用：存到 4 号位也照存，开战就在 4 号位，不自动更正
        Arm(s, "hatchet");
        Assert.That(E.StartWeaponOf(s, GameState.PlayerId).own, Is.EquivalentTo(new[] { 1, 2 }), "编队面板参考：开战武器页的合法使用位");
        Assert.That(E.SaveFormation(s, Slots("", "", "", "p0"), s.FormationVersion).Ok, Is.True);
        FightFrom(s, "shambler");
        Assert.That(GameEngine.PlayerPos(s.Battle!), Is.EqualTo(4), "下一场按预设开局，不按武器找位");
        ToPlayer(s);
        Assert.That(E.CommandBlocked(s, BattleCommand.Of("basic_attack", 1)), Is.Not.Null, "技能仍按真实己位校验（手斧在 4 号位不可用）");
    }

    /// <summary>
    /// 试玩修复 B8a 第 1 轮 B2：空手（两页都空）时编队面板参考的可用号位与战斗里拳击的己位判断同源——拳击只能 1、2 号位，
    /// 存到 3/4 号位照存（面板标“此位用不了”），开战拳击按真实号位禁用；调整站位预览也写拳在 3 号位不可用。
    /// </summary>
    [Test]
    public void POS_Q86_01_Unarmed_FistPositionsMatchBattle()
    {
        var s = Camp(1, 8606);
        s.Items.RemoveAll(i => i.Owner == ItemInstance.EquipOwner(GameState.PlayerId, EquipSlots.Weapon1) || i.Owner == ItemInstance.EquipOwner(GameState.PlayerId, EquipSlots.Weapon2));
        var (w, own) = E.StartWeaponOf(s, GameState.PlayerId);
        Assert.That(w.IsFists, Is.True, "双空为拳");
        Assert.That(own, Is.EquivalentTo(new[] { 1, 2 }), "拳击 unarmed_01 只能在我方1、2号位（不是拳的 1—4）");
        Assert.That(E.SaveFormation(s, Slots("", "", "p0", ""), s.FormationVersion).Ok, Is.True, "用不了的号位也照存");
        FightFrom(s, "shambler");
        Assert.That(GameEngine.PlayerPos(s.Battle!), Is.EqualTo(3));
        ToPlayer(s);
        Assert.That(E.CommandBlocked(s, BattleCommand.Of("basic_attack", 1)), Does.Contain("只能在我方1、2号位"), "与面板同源：拳击在 3 号位不可用");
        Assert.That(E.PositionHint(s, s.Battle!, 3), Does.Contain("拳不可用（要在1、2号位）"));
        Assert.That(E.PositionHint(s, s.Battle!, 2), Does.Contain("拳可用"));
    }

    // ───────────── POS-Q86-02：空白操作时期之外一律拒绝，只有后台加工不阻断 ─────────────

    [Test]
    public void POS_Q86_02_BlockedOutsideIdle_BackgroundProcessingDoesNot()
    {
        var blocked = ((JArray)Formation()["blockedBy"]!).Select(x => (string)x!).ToList();
        Assert.That(blocked, Does.Contain("main_action_including_paused").And.Contain("travel").And.Contain("pending_encounter").And.Contain("unread_forced_dialogue"));
        Assert.That((bool)Formation()["backgroundFacilityAloneBlocks"]!, Is.False);

        var s = Camp(1);
        Assert.That(E.FormationEditBlocked(s), Is.Null, "营地空闲");
        var want = Slots("", "p0", "", "");

        Start(s, ActionRequest.Wait(30));
        Assert.That(E.FormationEditBlocked(s), Does.Contain("正在等待"));
        Assert.That(E.SaveFormation(s, want, s.FormationVersion).Ok, Is.False, "主行动中拒绝保存");
        s.Main!.Status = ActionStatus.Paused;
        Assert.That(E.FormationEditBlocked(s), Does.Contain("已暂停"), "暂停/等待继续也算主行动");
        E.Cancel(s);
        s.Main = null;

        s.Sub = new ActionState { Type = ActionType.Use };
        Assert.That(E.FormationEditBlocked(s), Does.Contain("正在"), "子行动");
        s.Sub = null;

        s.Main = new ActionState { Type = ActionType.Travel, TargetId = "field" };
        Assert.That(E.FormationEditBlocked(s), Does.Contain("移动中"));
        s.Main = null;

        s.Encounter = new PendingEncounter { LocationId = "camp", Source = "explore" };
        Assert.That(E.FormationEditBlocked(s), Does.Contain("敌踪"));
        s.Encounter = null;

        var t = NewStory(8602);
        Assert.That(E.DialogueLocked(t), Is.True, "完整新局开场对白");
        Assert.That(E.FormationEditBlocked(t), Does.Contain("对话"));

        var d = Camp(1);
        d.Progress.Dead = true;
        Assert.That(E.FormationEditBlocked(d), Does.Contain("结束"));

        var o = Camp(1);
        foreach (var id in new[] { "wood", "stone", "fiber", "rope", "bandage", "raw_root" })
            E.CreateItems(o, id, 1, ItemInstance.Carried);
        Assert.That(E.CarriedOverflow(o), Is.True);
        Assert.That(E.FormationEditBlocked(o), Does.Contain("五张卡"), "旧档强制整理");

        // 只有后台设施加工：不阻断
        var bg = Camp(1);
        bg.Locations["camp"].Facility.Processes.Add(new FacilityProcess { ProcessId = "p1", RecipeId = "boil_water", Status = ProcessStatus.Heating });
        Assert.That(E.FormationEditBlocked(bg), Is.Null, "仅后台加工不构成主行动锁");

        var fight = FightFrom(Camp(1), "shambler");
        Assert.That(E.FormationEditBlocked(fight), Does.Contain("战斗中"));
        Assert.That(E.SaveFormation(fight, want, fight.FormationVersion).Ok, Is.False);
    }

    // ───────────── POS-Q86-03：保存校验、幂等、0 成本、失败整份不变 ─────────────

    [Test]
    public void POS_Q86_03_SaveValidation_Idempotent_ZeroCost_NoPartialChange()
    {
        var f = Formation();
        Assert.That(((JObject)f["cost"]!).Properties().Select(p => (int)p.Value), Is.All.EqualTo(0));
        Assert.That((string)f["commit"]!, Does.Contain("atomic_idempotent"));

        var s = Camp(3);
        E.AddNamedToRoster(s, "npc_tang_li");
        Assert.That(E.SetActive(s, "npc_tang_li", true).Ok, Is.True);
        Assert.That(s.FormationPreset, Is.EqualTo(Slots("p0", "npc_tang_li", "", "")), "新成员补到最前空槽");
        var v = s.FormationVersion;
        var before = s.FormationPreset.ToList();

        Assert.That(E.SaveFormationBlocked(s, before, v), Does.Contain("没有改动"), "无改动不能保存");
        Assert.That(E.SaveFormationBlocked(s, Slots("p0", "p0", "", ""), v), Does.Contain("每人只能占一个号位"));
        Assert.That(E.SaveFormationBlocked(s, Slots("npc_tang_li", "", "", ""), v), Does.Contain("你必须在编队里"));
        Assert.That(E.SaveFormationBlocked(s, Slots("p0", "", "", ""), v), Does.Contain("每名当前同行者"));
        Assert.That(E.SaveFormationBlocked(s, Slots("p0", "npc_lin_zhao", "npc_tang_li", ""), v), Does.Contain("不在当前同行"));
        Assert.That(E.SaveFormationBlocked(s, Slots("p0", "npc_tang_li", ""), v), Does.Contain("4个号位"));
        Assert.That(E.SaveFormationBlocked(s, Slots("", "npc_tang_li", "", "p0"), v - 1), Does.Contain("过期"), "草稿基于旧版本");
        Assert.That(s.FormationPreset, Is.EqualTo(before), "被拒的请求整份不变");
        Assert.That(s.FormationVersion, Is.EqualTo(v));

        var minute = s.GameMinute; var seg = s.SegmentCount; var rng = s.Rng.State; var energy = s.Stats.Energy;
        var tang = s.Actors["npc_tang_li"].Stats.Energy; var xp = s.Skills.Values.Sum(k => k.Xp); var items = s.Items.Count;
        var want = Slots("", "npc_tang_li", "", "p0");
        Assert.That(E.SaveFormation(s, want, v, "fm1").Ok, Is.True);
        Assert.That(s.FormationPreset, Is.EqualTo(want), "空位与不连续都可以");
        Assert.That(s.FormationVersion, Is.EqualTo(v + 1));
        Assert.That(E.SaveFormation(s, want, v, "fm1").Duplicate, Is.True, "同一请求重放幂等");
        Assert.That(s.FormationVersion, Is.EqualTo(v + 1), "重放不再加版本");
        Assert.That((s.GameMinute, s.SegmentCount, s.Rng.State, s.Stats.Energy), Is.EqualTo((minute, seg, rng, energy)), "0 分钟、0E、不推进世界、不抽随机");
        Assert.That(s.Actors["npc_tang_li"].Stats.Energy, Is.EqualTo(tang), "不恢复队友状态");
        Assert.That(s.Skills.Values.Sum(k => k.Xp), Is.EqualTo(xp), "0 经验");
        Assert.That(s.Items.Count, Is.EqualTo(items));
        Assert.That(s.Log.Last().Text, Does.Contain("编队已保存：1号位空，2号位唐梨，3号位空，4号位你"));
    }

    // ───────────── POS-Q86-04：迁移、旧战保持、无效槽补空、死亡离队不压紧、战内位移不反写 ─────────────

    /// <summary>把当前存档改写成 schema 15 的样子：紧凑预设、没有阵容版本/迁移标记、敌人没有本轮 AP 账。</summary>
    private static string AsSchema15(GameState s, params string[] compactPreset)
    {
        var j = JObject.Parse(SaveSerializer.Serialize(s));
        j["SchemaVersion"] = "15";
        j["FormationPreset"] = new JArray(compactPreset);
        j.Remove("FormationVersion");
        j.Remove("FormationMigrated");
        if (j["Battle"] is JObject b) foreach (var e in (JArray)b["Enemies"]!) ((JObject)e).Remove("RoundApUsed");
        return j.ToString();
    }

    [Test]
    public void POS_Q86_04_Migration15To16_OldBattleKept_InvalidSlotsRepaired_DeathNoCompaction_BattleMovesNotWrittenBack()
    {
        var f = Formation();
        Assert.That((bool)f["ongoingBattleDoesNotWritePreset"]!, Is.True);
        Assert.That((string)f["memberRemoved"]!, Is.EqualTo("remove_only_preserve_other_slots_and_holes"));

        // 旧档紧凑预设按原顺序落到 1..n，只迁移一次
        var s = Camp(4);
        foreach (var id in new[] { "npc_lin_zhao", "npc_tang_li" }) { E.AddNamedToRoster(s, id); E.SetActive(s, id, true); }
        var m = SaveSerializer.Deserialize(AsSchema15(s, "npc_tang_li", "p0", "npc_lin_zhao"), Cfg, out var notes);
        Assert.That(m.SchemaVersion, Is.EqualTo(GameState.CurrentSchema));
        Assert.That(m.FormationPreset, Is.EqualTo(Slots("npc_tang_li", "p0", "npc_lin_zhao", "")));
        Assert.That(m.FormationMigrated, Is.True);
        Assert.That(m.Log.Any(l => l.Text.Contains("存档已升级到版本16（编队号位）")), Is.True, "16→17（B8b 服务地点）接着升级");
        var again = SaveSerializer.Deserialize(SaveSerializer.Serialize(m), Cfg, out var notes2);
        Assert.That(notes2, Is.Empty, "只迁移一次");
        Assert.That(again.FormationPreset, Is.EqualTo(m.FormationPreset));

        // 无效/重复/越界/缺主角：去掉无效的，合法唯一的保留原槽，缺的人按同行顺序补最前空槽，不挤走合法成员
        var bad = SaveSerializer.Deserialize(AsSchema15(s, "npc_lin_zhao", "ghost", "npc_lin_zhao", "", "npc_tang_li"), Cfg, out _);
        Assert.That(bad.FormationPreset, Is.EqualTo(Slots("npc_lin_zhao", "p0", "npc_tang_li", "")), "第 5 格越界、ghost 不在队、重复取最前；主角与唐梨补最前空槽");
        Assert.That(E.RepairedFormation(s, Slots("", "", "npc_tang_li", "npc_tang_li")), Is.EqualTo(Slots("p0", "npc_lin_zhao", "npc_tang_li", "")));

        // 进行中的旧战：战场槽保持（不因补预设改变本场）；已行动的敌人本轮 AP 按用完
        var fs = Camp(2, 8611);
        E.SaveFormation(fs, Slots("", "", "p0", ""), fs.FormationVersion);
        FightFrom(fs, "shambler", "crawler");
        ToPlayer(fs);
        var fb = fs.Battle!;
        StandAt(fs, 2);
        fb.Enemies[0].Acted = true;
        var mm = SaveSerializer.Deserialize(AsSchema15(fs, "p0"), Cfg, out _);
        Assert.That(GameEngine.PlayerPos(mm.Battle!), Is.EqualTo(2), "旧战保持已存战场槽");
        Assert.That(mm.FormationPreset, Is.EqualTo(Slots("p0", "", "", "")), "预设按旧档内容迁移，不读战场槽");
        Assert.That(mm.Battle!.Enemies[0].RoundApUsed, Is.EqualTo(E.EnemyAp(mm.Battle.Enemies[0])), "旧档轮中已行动的敌人本轮 AP 按用完（不能替后方补位）");
        Assert.That(mm.Battle.Enemies[1].RoundApUsed, Is.EqualTo(0));

        // 战内位移不反写；下一战按预设（3 号位），不继承上一战的 2 号位
        Assert.That(fs.FormationPreset, Is.EqualTo(Slots("", "", "p0", "")), "战斗中调整站位不改预设");
        fs.Battle = null;
        foreach (var w in fs.WorldEnemies) w.Engaged = false;
        FightFrom(fs, "shambler");
        Assert.That(GameEngine.PlayerPos(fs.Battle!), Is.EqualTo(3), "下一战按预设");

        // 成员死亡：只清他那一格，其余号位与空位不动，版本 +1
        var d = Camp(4, 8612);
        foreach (var id in new[] { "npc_lin_zhao", "npc_tang_li" }) { E.AddNamedToRoster(d, id); E.SetActive(d, id, true); }
        Assert.That(E.SaveFormation(d, Slots("npc_tang_li", "", "npc_lin_zhao", "p0"), d.FormationVersion).Ok, Is.True);
        var v = d.FormationVersion;
        d.Actors["npc_lin_zhao"].Stats.Health = 0.01;
        d.Actors["npc_lin_zhao"].Stats.Wakefulness = 0;
        Start(d, ActionRequest.Wait(60));
        Run(d);
        Assert.That(E.RosterOf(d, "npc_lin_zhao")!.Dead, Is.True);
        Assert.That(d.FormationPreset, Is.EqualTo(Slots("npc_tang_li", "", "", "p0")), "死亡不压紧");
        Assert.That(d.FormationVersion, Is.GreaterThan(v), "阵容版本随成员变更增加");
    }

    // ───────────── POS-Q86-05：异地只改站位、换人仍营区；C 限人数；主角必在；新成员补最前空槽 ─────────────

    [Test]
    public void POS_Q86_05_PositionsAnywhere_RosterOnlyAtHub_CapacityAndUniqueness()
    {
        var f = Formation();
        Assert.That((string)f["editLocation"]!, Is.EqualTo("any_location_when_idle_for_current_party_positions_only"));
        Assert.That((string)f["memberAdded"]!, Does.Contain("fill_frontmost_empty_increment_revision"));

        var s = Camp(2);
        Assert.That(E.SaveFormation(s, Slots("", "", "p0", ""), s.FormationVersion).Ok, Is.True);
        E.AddNamedToRoster(s, "npc_zhou_yan");
        E.AddNamedToRoster(s, "npc_shen_he");
        var v = s.FormationVersion;
        Assert.That(E.SetActive(s, "npc_zhou_yan", true).Ok, Is.True);
        Assert.That(s.FormationPreset, Is.EqualTo(Slots("npc_zhou_yan", "", "p0", "")), "新成员在同一换队事务补最前空槽，不挤走你");
        Assert.That(s.FormationVersion, Is.EqualTo(v + 1));
        Assert.That(E.SetActive(s, "npc_shen_he", true).Reason, Does.Contain("同行已满 2/2"), "C 限制人数");

        // 野外空闲：当前同行站位可改；换人仍只在营区
        E.DebugTeleport(s, "field");
        Assert.That(E.FormationEditBlocked(s), Is.Null, "当前同行站位不受地点限制");
        Assert.That(E.SaveFormation(s, Slots("", "p0", "", "npc_zhou_yan"), s.FormationVersion).Ok, Is.True);
        Assert.That(E.SetActive(s, "npc_zhou_yan", false).Reason, Does.Contain("营区").And.EndsWith("安全空闲时留作候补").And.Not.Contain("编队"), "留作候补仍只在营区；原因用按钮自己的动词，不和顶栏“编队”混淆（B8a 第 1 轮 B7）");
        Assert.That(E.SetActiveBlocked(s, "npc_shen_he", true), Does.EndWith("安全空闲时编入同行").Or.Contain("同行已满"));
        Assert.That(E.SetActive(s, "npc_shen_he", true).Reason, Does.Contain("营区").Or.Contain("同行已满"), "不远程召回候补");
        Assert.That(E.SaveFormationBlocked(s, Slots("npc_shen_he", "p0", "", "npc_zhou_yan"), s.FormationVersion), Does.Contain("不在当前同行"), "候补不能经编队换入");

        // 留作候补（营区）：只清他那一格
        E.DebugTeleport(s, "camp");
        Assert.That(E.SetActive(s, "npc_zhou_yan", false).Ok, Is.True);
        Assert.That(s.FormationPreset, Is.EqualTo(Slots("", "p0", "", "")), "离队只清自己那格、不压紧");
    }
}
