using Survival.Core.Config;
using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 B8「队伍与共同生存」（R31、R32）：名册、一次初始化、容量 C 内编队与预设、候补公共照护、死亡与遗物、借用矛与 RB01。
/// 出处：R08 §1—§5（PT01—PT10）、A11 §5—§8（NP-A06/07/12/13/19/20/21/23）、S01 多角色段（TX01/TX02/TX06）、COMP04/COMP17。
/// </summary>
public class B8PartyTests : TestBase
{
    private static readonly string[] Six = { "npc_lin_zhao", "npc_zhou_yan", "npc_shen_he", "npc_tang_li", "npc_gu_cheng", "npc_ye_xun" };

    /// <summary>新局（无剧情），在营地；按 c 记下容量事实。</summary>
    protected GameState Camp(int c = 4, uint seed = 12345)
    {
        var s = New(seed);
        if (c >= 2) s.Progress.Flags.Add("hub_registered");
        if (c >= 3) s.Progress.Flags.Add("church_route_record");
        if (c >= 4) s.Progress.Flags.Add("academy_safety_record");
        s.SpawnNextCheck = int.MaxValue;
        s.PatrolNextCheck = int.MaxValue;
        Assert.That(s.CurrentLocation, Is.EqualTo("camp"));
        return s;
    }

    private static List<ItemInstance> Gear(GameState s, string id) =>
        s.Items.Where(i => i.Owner.StartsWith("equip:" + id + ":") || i.Owner == ItemInstance.CarriedOwner(id)).ToList();

    // ───────────── M183：名册、六人数据与编队 ─────────────

    /// <summary>NP-A19：六人速度 4/3/5/6/4/5，H 上限 20，15 门技能完整、列出的等级正确，两件武器 ID/槽正确且白色 0 词条，其余五槽与五卡空。</summary>
    [Test]
    public void NP_A19_SixNamedCompanions_DataAndFirstEquipment()
    {
        var s = Camp(4);
        Assert.That(Cfg.Party.NamedCompanions.Select(n => n.ActorId), Is.EqualTo(Six));
        var speeds = new[] { 4.0, 3, 5, 6, 4, 5 };
        var weapons = new[] { ("wooden_spear", "wooden_shield"), ("crowbar", "signal_whistle"), ("stone_knife", "field_medkit"),
                              ("wooden_bow", "stone_knife"), ("wooden_spear", "signal_whistle"), ("stone_knife", "wooden_shield") };
        for (var k = 0; k < 6; k++)
        {
            var id = Six[k];
            Assert.That(E.AddNamedToRoster(s, id).Ok, Is.True);
            var a = s.Actors[id];
            Assert.That(E.ActorBaseSpeed(s, id), Is.EqualTo(speeds[k]), id);
            Assert.That(E.ActorProfile(s, id)!.BaseSpeed, Is.EqualTo(speeds[k]), "与 actorBaseDefense 同源");
            Assert.That(a.Stats.Health, Is.EqualTo(20)); Assert.That(a.Stats.HealthMax, Is.EqualTo(20));
            Assert.That(a.Stats.Satiety, Is.EqualTo(85)); Assert.That(a.Stats.Hydration, Is.EqualTo(85));
            Assert.That(a.Stats.Energy, Is.EqualTo(18)); Assert.That(a.Stats.Wakefulness, Is.EqualTo(90));
            Assert.That(a.Stats.Morale, Is.EqualTo(70)); Assert.That(a.Body.Immunity, Is.EqualTo(70));
            Assert.That(a.Body.VirusLoad, Is.EqualTo(0)); Assert.That(a.Body.Infected, Is.False);
            Assert.That(a.Skills.Count, Is.EqualTo(15));
            Assert.That(a.Skills.Values.All(x => x.Xp == 0), Is.True);
            var named = Cfg.Party.Named(id)!;
            foreach (var kv in a.Skills)
                Assert.That(kv.Value.Level, Is.EqualTo(named.Skills.TryGetValue(kv.Key, out var lv) ? lv : 1), $"{id} {kv.Key}");
            var gear = Gear(s, id);
            Assert.That(gear.Count, Is.EqualTo(2), "只有两件武器，五卡与其余五槽空、不附赠耗材");
            Assert.That(s.Items.Single(i => i.Owner == ItemInstance.EquipOwner(id, EquipSlots.Weapon1)).ItemId, Is.EqualTo(weapons[k].Item1));
            Assert.That(s.Items.Single(i => i.Owner == ItemInstance.EquipOwner(id, EquipSlots.Weapon2)).ItemId, Is.EqualTo(weapons[k].Item2));
            Assert.That(gear.All(i => i.Quality == "white" && i.Affixes == null), Is.True, "未写品质概率的来源：白色 0 词条（Q44）");
            Assert.That(gear.All(i => i.Source?.GrantEventId == "recruit:" + id), Is.True);
        }
        Assert.That(s.Actors["npc_tang_li"].Skills["archery"].Level, Is.EqualTo(4));
        Assert.That(s.Actors["npc_gu_cheng"].Skills["firearms"].Level, Is.EqualTo(5));
        Assert.That(s.Roster.Select(r => r.Name), Is.EqualTo(new[] { "林昭", "周砚", "沈禾", "唐梨", "顾承", "叶洵" }));
        Assert.That(E.AddNamedToRoster(s, "npc_lin_zhao").Ok, Is.False, "同一人不重复登记、不重发装备");
        Assert.That(Gear(s, "npc_lin_zhao").Count, Is.EqualTo(2));
    }

    /// <summary>PT08：名册 6 人但同行（含主角）≤ C；C1 只能主角，第 C+1 人被拒并写原因。主角不能候补。</summary>
    [Test]
    public void PT08_RosterSixButPartyCappedByC()
    {
        var s = Camp(1);
        foreach (var id in Six) E.AddNamedToRoster(s, id);
        Assert.That(E.PartyCapacity(s), Is.EqualTo(1));
        Assert.That(E.Reserves(s).Count, Is.EqualTo(6));
        Assert.That(E.SetActive(s, "npc_lin_zhao", true).Reason, Does.Contain("同行已满 1/1"));
        s.Progress.Flags.Add("hub_registered");
        s.Progress.Flags.Add("church_route_record");
        s.Progress.Flags.Add("academy_safety_record");
        Assert.That(E.PartyCapacity(s), Is.EqualTo(4));
        foreach (var id in Six.Take(3)) Assert.That(E.SetActive(s, id, true).Ok, Is.True);
        Assert.That(s.Party, Is.EqualTo(new[] { "p0", "npc_lin_zhao", "npc_zhou_yan", "npc_shen_he" }));
        Assert.That(E.SetActive(s, "npc_tang_li", true).Reason, Does.Contain("同行已满 4/4"));
        Assert.That(E.SetActive(s, "p0", false).Reason, Does.Contain("主角必须在队伍里"));
        Assert.That(E.Reserves(s).Select(r => r.ActorId), Is.EqualTo(Six.Skip(3)));
        Assert.That(s.FormationPreset, Is.EqualTo(s.Party), "编入时接在预设末尾");
    }

    /// <summary>PT09/NP-A12：入队→候补→入队（0 时间反复）：时间、状态、装备都不变，不重发、不重抽、不托底。</summary>
    [Test]
    public void PT09_ToggleRepeatedly_NoTimeNoResetNoRegrant()
    {
        var s = Camp(2);
        E.AddNamedToRoster(s, "npc_shen_he");
        var a = s.Actors["npc_shen_he"];
        a.Stats.Satiety = 12; a.Stats.Energy = 3; a.Body.VirusLoad = 33;
        var minute = s.GameMinute; var seg = s.SegmentCount; var items = s.Items.Count; var next = s.NextInstanceId; var rng = s.Rng.State;
        for (var k = 0; k < 5; k++)
        {
            Assert.That(E.SetActive(s, "npc_shen_he", true).Ok, Is.True);
            Assert.That(E.SetActive(s, "npc_shen_he", false).Ok, Is.True);
        }
        Assert.That(s.GameMinute, Is.EqualTo(minute)); Assert.That(s.SegmentCount, Is.EqualTo(seg));
        Assert.That(s.Items.Count, Is.EqualTo(items)); Assert.That(s.NextInstanceId, Is.EqualTo(next)); Assert.That(s.Rng.State, Is.EqualTo(rng));
        Assert.That(a.Stats.Satiety, Is.EqualTo(12), "换人本身不触发 N/W 托底（只在实际世界 tick）");
        Assert.That(a.Stats.Energy, Is.EqualTo(3)); Assert.That(a.Body.VirusLoad, Is.EqualTo(33));
        Assert.That(E.SetActive(s, "npc_shen_he", true, "r1").Ok, Is.True);
        Assert.That(E.SetActive(s, "npc_shen_he", true, "r1").Duplicate, Is.True, "同一 requestId 重放不重复执行");
    }

    /// <summary>PT01：同一事实重复记下 C 不再增；C 只取已满足档的最大值。</summary>
    [Test]
    public void PT01_SameFactTwice_CapacityDoesNotGrow()
    {
        var s = Camp(1);
        E.DebugCapacityFact(s, "hub_registered");
        E.DebugCapacityFact(s, "hub_registered");
        Assert.That(E.PartyCapacity(s), Is.EqualTo(2));
        E.DebugCapacityFact(s, "church_route_record");
        E.DebugCapacityFact(s, "church_route_record");
        Assert.That(E.PartyCapacity(s), Is.EqualTo(3));
    }

    /// <summary>编队限制：只在营区安全空闲；战斗、行动、对白中不行；预设必须正好是同行者各一次。</summary>
    [Test]
    public void PartyEdit_OnlyAtHubWhenIdle_AndFormationValidated()
    {
        var s = Camp(3);
        E.AddNamedToRoster(s, "npc_tang_li");
        E.AddNamedToRoster(s, "npc_lin_zhao");
        Assert.That(E.SetActive(s, "npc_tang_li", true).Ok, Is.True);
        Assert.That(E.SetActive(s, "npc_lin_zhao", true).Ok, Is.True);
        Assert.That(E.SetFormation(s, new[] { "npc_lin_zhao", "npc_tang_li" }).Ok, Is.False, "缺主角");
        Assert.That(E.SetFormation(s, new[] { "npc_lin_zhao", "npc_tang_li", "p0" }).Ok, Is.True);
        Assert.That(s.FormationPreset, Is.EqualTo(new[] { "npc_lin_zhao", "npc_tang_li", "p0" }));
        Assert.That(s.Party[0], Is.EqualTo("p0"), "Party 主角恒在首位");
        Start(s, ActionRequest.Wait(30));
        Assert.That(E.SetActive(s, "npc_tang_li", false).Reason, Does.Contain("正在行动"));
        E.Cancel(s);
        E.DebugTeleport(s, "field");
        Assert.That(E.SetActive(s, "npc_tang_li", false).Reason, Does.Contain("营区"));
        Assert.That(E.DebugToggleActive(s, "npc_tang_li"), Does.Contain("候补"), "开发入口跳过地点限制");
        Assert.That(s.FormationPreset, Is.EqualTo(new[] { "npc_lin_zhao", "p0" }));
    }

    /// <summary>配置：六人两件武器存在且 kind 不同，技能合法；坏配置被拒。</summary>
    [Test]
    public void Config_NamedCompanionWeaponsValidated()
    {
        Assert.That(ConfigLoader.Validate(Cfg), Is.Empty);
        var json = File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "config.v0.6.json"))
            .Replace("\"weapon2\": \"wooden_shield\"", "\"weapon2\": \"wooden_spear\"");
        var bad = ConfigLoader.FromJson(json, validate: false);
        Assert.That(ConfigLoader.Validate(bad), Has.Some.Contains("两件初始武器类型相同"));
    }

    /// <summary>开发入口：同一人加两次只一次；超 C 时进候补。</summary>
    [Test]
    public void Debug_Recruit_JoinsUntilFull_ThenReserve()
    {
        var s = Camp(2);
        Assert.That(E.DebugRecruit(s, "npc_lin_zhao"), Does.Contain("编入同行"));
        Assert.That(E.DebugRecruit(s, "npc_lin_zhao"), Does.Contain("已在名册"));
        Assert.That(E.DebugRecruit(s, "npc_zhou_yan"), Does.Contain("留作候补"));
        Assert.That(s.Party, Is.EqualTo(new[] { "p0", "npc_lin_zhao" }));
        Assert.That(E.ActorDisplayName(s, "npc_zhou_yan"), Is.EqualTo("周砚"));
    }
}
