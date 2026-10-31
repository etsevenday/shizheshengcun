using Newtonsoft.Json.Linq;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 B8 M186：存档 14→15（名册、编成预设、遗物、借用位）与战斗主角字段搬进每人状态 Fighters（只重构、不改行为）。
/// </summary>
public class B8MigrationTests : BattleTestBase
{
    /// <summary>把当前存档改写成 schema 14 的样子：Fighters.p0 的字段放回 BattleState 顶层旧名，去掉 B8 新字段。</summary>
    private static string AsSchema14(GameState s)
    {
        var j = JObject.Parse(SaveSerializer.Serialize(s));
        j["SchemaVersion"] = "14";
        foreach (var f in new[] { "Roster", "FormationPreset", "FormationVersion", "FormationMigrated", "Remains", "LoanSlots", "NextEscortSeq" }) j.Remove(f);
        if (j["Battle"] is JObject b)
        {
            var me = (JObject)b["Fighters"]![GameState.PlayerId]!;
            foreach (var (old, now) in BattleState.LegacyFighterFields) b[old] = me[now]!.DeepClone();
            b.Remove("Fighters");
        }
        return j.ToString();
    }

    /// <summary>14→15 无战斗：名册空、预设=[p0]、遗物与借用位空；提示“队伍与候补”；再读不再迁移（幂等）。</summary>
    [Test]
    public void Migrate14To15_NoBattle_DefaultsAndNote()
    {
        var s = E.NewGame(77);
        var m = SaveSerializer.Deserialize(AsSchema14(s), Cfg, out _);
        Assert.That(m.SchemaVersion, Is.EqualTo(GameState.CurrentSchema), "14→15→16（B8a 编队号位）");
        Assert.That(m.Roster, Is.Empty);
        Assert.That(m.FormationPreset, Is.EqualTo(new[] { "p0", "", "", "" }), "B8a：预设为 4 格");
        Assert.That(m.Remains, Is.Empty);
        Assert.That(m.LoanSlots, Is.Empty);
        Assert.That(m.Log.Any(l => l.Text.Contains("存档已升级到版本15（队伍与候补）")), Is.True);
        var again = SaveSerializer.Deserialize(SaveSerializer.Serialize(m), Cfg, out var notes2);
        Assert.That(notes2, Is.Empty);
        Assert.That(SaveSerializer.Serialize(again), Is.EqualTo(SaveSerializer.Serialize(m)));
    }

    /// <summary>14→15 进行中战斗：AP、恐慌与峰值、格挡次数、减速、追击进度、u、技能冷却、武器页、受惊结果逐项相等，读档后可以接着打。</summary>
    [Test]
    public void Migrate14To15_InBattle_PlayerFieldsMoveToFighterP0()
    {
        var s = Fight(9201, "shambler", "crawler");
        ToPlayer(s);
        var b = s.Battle!;
        b.Panic = 23.5; b.PanicPeak = 31; b.PlayerGuardCharges = 2; b.PlayerSlowActive = true; b.PlayerPursuit = 47.5;
        b.SkillUsedRound["spear_01"] = 1; b.ActiveWeaponSlot = 2; b.PlayerNextDamageFactor = 0.8; b.FearRoll = 0.42; b.Frightened = true;
        var ap = b.PlayerAp;
        var tie = b.PlayerTieRoll;
        var json = AsSchema14(s);
        Assert.That(JObject.Parse(json)["Battle"]!["PlayerAp"]!.Value<int>(), Is.EqualTo(ap), "构造的旧档确实是顶层字段");
        var m = SaveSerializer.Deserialize(json, Cfg, out _);
        var mb = m.Battle!;
        Assert.That(mb.Fighters.Keys, Is.EqualTo(new[] { "p0" }));
        Assert.That(mb.PlayerAp, Is.EqualTo(ap)); Assert.That(mb.Fighter("p0").Ap, Is.EqualTo(ap));
        Assert.That(mb.Panic, Is.EqualTo(23.5)); Assert.That(mb.PanicPeak, Is.EqualTo(31));
        Assert.That(mb.PlayerGuardCharges, Is.EqualTo(2)); Assert.That(mb.PlayerSlowActive, Is.True);
        Assert.That(mb.PlayerPursuit, Is.EqualTo(47.5)); Assert.That(mb.PlayerTieRoll, Is.EqualTo(tie));
        Assert.That(mb.SkillUsedRound["spear_01"], Is.EqualTo(1)); Assert.That(mb.ActiveWeaponSlot, Is.EqualTo(2));
        Assert.That(mb.PlayerNextDamageFactor, Is.EqualTo(0.8)); Assert.That(mb.FearRoll, Is.EqualTo(0.42)); Assert.That(mb.Frightened, Is.True);
        Assert.That(E.AwaitingPlayer(m), Is.True, "读档后可以继续");
        // 新档序列化里不再有顶层旧字段，只有 Fighters
        var nj = JObject.Parse(SaveSerializer.Serialize(m));
        Assert.That(nj["Battle"]!["PlayerAp"], Is.Null);
        Assert.That(nj["Battle"]!["Fighters"]!["p0"]!["Ap"]!.Value<int>(), Is.EqualTo(ap));
    }

    /// <summary>Clone 深拷贝每人战斗状态（含冷却表），改副本不影响原件。</summary>
    [Test]
    public void BattleClone_DeepCopiesFighters()
    {
        var b = new BattleState();
        b.SkillUsedRound["x"] = 3;
        b.Panic = 5;
        var c = b.Clone();
        c.SkillUsedRound["x"] = 9;
        c.Panic = 7;
        Assert.That(b.SkillUsedRound["x"], Is.EqualTo(3));
        Assert.That(b.Panic, Is.EqualTo(5));
    }

    /// <summary>旧档同行里有非主角（测试注入）：补名册条目，预设与同行对齐。</summary>
    [Test]
    public void Migrate14To15_PartyWithoutRoster_GetsEntryAndPreset()
    {
        var s = E.NewGame(78);
        s.Actors["c9"] = new ActorState { Id = "c9" };
        s.Party.Add("c9");
        var m = SaveSerializer.Deserialize(AsSchema14(s), Cfg, out _);
        Assert.That(m.Roster.Single().ActorId, Is.EqualTo("c9"));
        Assert.That(m.FormationPreset, Is.EqualTo(new[] { "p0", "c9", "", "" }));
    }
}
