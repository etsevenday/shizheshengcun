using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// B34a（S14 Q528.2、Q570；验收 Q528-A04、Q570-A03）：本场战报不再截存 80 条；旧档进行中的战斗满 80 条时标“较早记录可能未保存”，
/// 不足 80 条记为完整；存档读档连续、不补造。
/// </summary>
public class B34aBattleLogTests : BattleTestBase
{
    [Test]
    public void NewBattle_LogComplete_AndKeepsMoreThan80()
    {
        var s = Fight(1, "shambler");
        var b = s.Battle!;
        Assert.That(b.LogComplete, Is.True, "新战斗从开战起完整");
        Assert.That(b.LogMayBeIncomplete, Is.False);
        var first = b.Log.First();
        for (var i = 0; i < 120; i++) b.Log.Add($"第{b.Round}轮 · 测试行{i}");
        ToPlayer(s);
        if (s.Battle != null && E.AwaitingPlayer(s)) E.ExecuteCommand(s, BattleCommand.Of("end_turn"));
        for (var i = 0; i < 20 && s.Battle != null; i++) Step(s);
        Assert.That(b.Log.Count, Is.GreaterThan(121), "超过 80 条仍全部保留");
        Assert.That(b.Log.First(), Is.EqualTo(first), "第一条仍在");
    }

    [Test]
    public void SaveLoad_KeepsFullLogAndFlag()
    {
        var s = Fight(2, "shambler");
        for (var i = 0; i < 150; i++) s.Battle!.Log.Add($"第1轮 · 行{i}");
        var n = s.Battle!.Log.Count;
        var t = SaveSerializer.Deserialize(SaveSerializer.Serialize(s), Cfg);
        Assert.That(t.Battle!.Log.Count, Is.EqualTo(n), "读档后条数不变、不重记");
        Assert.That(t.Battle.LogComplete, Is.True);
        Assert.That(t.Battle.LogMayBeIncomplete, Is.False);
    }

    [Test]
    public void LegacyBattle_At80_FlaggedIncomplete_Under80_Complete()
    {
        var s = Fight(3, "shambler");
        s.Battle!.Log.Clear();
        for (var i = 0; i < SaveMigrator.LegacyBattleLogCap; i++) s.Battle.Log.Add($"第1轮 · 旧行{i}");
        s.Battle.LogComplete = null;   // 旧档没有这个字段
        var t = SaveSerializer.Deserialize(SaveSerializer.Serialize(s), Cfg);
        Assert.That(t.Battle!.LogComplete, Is.False, "满 80 条的旧战斗无法确认早段完整");
        Assert.That(t.Battle.LogMayBeIncomplete, Is.True);
        Assert.That(t.Battle.Log.Count, Is.EqualTo(80), "不补造、不删");
        // 之后继续追加，标记不变
        t.Battle.Log.Add("第1轮 · 新行");
        var u = SaveSerializer.Deserialize(SaveSerializer.Serialize(t), Cfg);
        Assert.That(u.Battle!.LogMayBeIncomplete, Is.True);

        var v = Fight(4, "shambler");
        v.Battle!.LogComplete = null;
        var w = SaveSerializer.Deserialize(SaveSerializer.Serialize(v), Cfg);
        Assert.That(w.Battle!.LogComplete, Is.True, "旧档不足 80 条从未被截，记为完整");
        Assert.That(w.Battle.LogMayBeIncomplete, Is.False);
    }

    [Test]
    public void Clone_KeepsFlag()
    {
        var s = Fight(5, "shambler");
        s.Battle!.LogComplete = false;
        Assert.That(s.Battle.Clone().LogComplete, Is.False);
    }
}
