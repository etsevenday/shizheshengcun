using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 策划提交 f172677 B2 · 主界面截图复核 §C（验收 UI-M07、UI-M08）：右栏熟练度/词条各自的展开/收起是界面偏好，
/// 按 档案×角色ID×板块ID 分开记；没有记录时为原默认（展开）且不写回；序列化往返（刷新/重新进入）不丢；
/// 与游戏存档分开，读档不回滚；损坏内容退回默认、不抛异常。
/// </summary>
public class B2FoldPrefsTests : TestBase
{
    private const string P = UiFoldPrefs.DefaultProfile;
    private const string A = UiFoldPrefs.MainActor;

    [Test]
    public void FourCombinations_EachSectionKeepsItsOwnChoice_AcrossReload()
    {
        foreach (var (skills, traits) in new[] { (true, true), (true, false), (false, true), (false, false) })
        {
            var p = new UiFoldPrefs();
            Assert.That(p.IsOpen(P, A, UiFoldPrefs.Skills), Is.True, "无记录时沿原默认：展开");
            Assert.That(p.Count, Is.EqualTo(0), "读取默认值不写入");
            if (!skills) p.Toggle(P, A, UiFoldPrefs.Skills);
            if (!traits) p.Toggle(P, A, UiFoldPrefs.Traits);
            // 刷新/重新进入 = 从浏览器本地读回同一段 JSON；“组件重建”同样只读偏好，不再有组件字段默认值
            var back = UiFoldPrefs.Parse(p.Serialize());
            Assert.That(back.IsOpen(P, A, UiFoldPrefs.Skills), Is.EqualTo(skills), $"熟练度 {skills}/{traits}");
            Assert.That(back.IsOpen(P, A, UiFoldPrefs.Traits), Is.EqualTo(traits), $"词条 {skills}/{traits}");
        }
    }

    [Test]
    public void KeysSeparateProfileActorAndSection()
    {
        var p = new UiFoldPrefs();
        p.Set(P, A, UiFoldPrefs.Skills, false);
        Assert.That(p.IsOpen(P, A, UiFoldPrefs.Traits), Is.True, "另一板块不受影响");
        Assert.That(p.IsOpen(P, "p1", UiFoldPrefs.Skills), Is.True, "另一角色仍为默认（切角色再返回各自恢复）");
        Assert.That(p.IsOpen("slot2", A, UiFoldPrefs.Skills), Is.True, "另一档案仍为默认");
        p.Set(P, "p1", UiFoldPrefs.Skills, true);
        Assert.That(p.IsOpen(P, A, UiFoldPrefs.Skills), Is.False, "给新角色初始化/设置不倒改主角已记的选择");
        Assert.That(p.HasChoice(P, A, UiFoldPrefs.Skills) && !p.HasChoice(P, A, UiFoldPrefs.Traits), Is.True);
        Assert.That(UiFoldPrefs.KeyOf("a|b", "c", "d"), Is.Not.EqualTo(UiFoldPrefs.KeyOf("a", "b|c", "d")), "键里的分隔符不串位");
    }

    [Test]
    public void CorruptOrMissingStorage_FallsBackToDefaults()
    {
        foreach (var raw in new[] { null, "", "   ", "{坏", "[1,2]", "{\"slot1|p0|skills\":\"x\"}" })
        {
            var p = UiFoldPrefs.Parse(raw);
            Assert.That(p.IsOpen(P, A, UiFoldPrefs.Skills), Is.True, raw ?? "null");
            p.Toggle(P, A, UiFoldPrefs.Skills);
            Assert.That(p.IsOpen(P, A, UiFoldPrefs.Skills), Is.False, "存储坏了也能在内存里继续记");
        }
    }

    [Test]
    public void FoldPrefs_AreNotPartOfGameSave_LoadingOldSaveDoesNotRollBack()
    {
        var p = new UiFoldPrefs();
        p.Set(P, A, UiFoldPrefs.Skills, false);
        var s = New();
        var json = SaveSerializer.Serialize(s);
        Assert.That(json, Does.Not.Contain("slot1|p0"), "偏好不写进游戏存档");
        var loaded = SaveSerializer.Deserialize(json, Cfg);
        Assert.That(loaded, Is.Not.Null);
        Assert.That(p.IsOpen(P, A, UiFoldPrefs.Skills), Is.False, "读档后仍是玩家最近的折叠选择");
    }
}
