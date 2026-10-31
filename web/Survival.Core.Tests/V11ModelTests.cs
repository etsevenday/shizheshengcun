using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 策划提交 5493951（M69）：配置与数据模型——随身五张根物品卡上限（S06 §8）、去掉复煮继承期限（Q16、S11 §10）、
/// 首战操作附注按 N01 §3.1 新流程、加工进程的受热保护标记与存档版本 8。
/// </summary>
public class V11ModelTests : TestBase
{
    [Test]
    public void Config_MaxRootCards5_ReboilNoInheritance_TutorialNote()
    {
        Assert.That(Cfg.Carry.MaxRootCards, Is.EqualTo(5), "S06 §8：随身最多5张根物品卡");
        Assert.That(Cfg.Carry.HardGrams, Is.EqualTo(30000), "原30公斤上限保持，与五卡同时校验");
        var note = Cfg.Story.Dialogues.Single(d => d.Id == "tutorial_arrive").Note;
        Assert.That(note, Is.EqualTo("轮到你时可使用行动点（AP）。先选技能，再把鼠标移到亮起的目标上看右侧预测，点击目标就会执行。自用或无目标指令按旁边的明确按钮确认；用完行动点或点‘结束行动’后继续战斗。"),
            "N01 §3.1 首战操作附注原文");
        Assert.That(note, Does.Not.Contain("执行”"), "不再要求另点“执行”");
        Assert.That(GameState.CurrentSchema, Is.EqualTo("14"), "B7d 真实站位与空位（B7b 为 13、B6 为 12）");
    }

    [Test]
    public void RootCards_StacksBatchesToolsPot()
    {
        var s = New();
        // 新局随身：锅、打火器
        Assert.That(E.RootCardCount(s), Is.EqualTo(2));
        E.DebugGive(s, "wood", 25);   // 堆叠上限20：25份占2张
        Assert.That(E.RootCardCount(s), Is.EqualTo(4));
        E.DebugGive(s, "wood", 10);   // 35份仍是2张
        Assert.That(E.RootCardCount(s), Is.EqualTo(4));
        E.DebugGive(s, "wood", 6);    // 41份→3张
        Assert.That(E.RootCardCount(s), Is.EqualTo(5));

        // 锅连同内容只算1张
        var t = New();
        var pot = First(t, "pot");
        E.DebugGive(t, "clean_water", 2, "camp");
        Assert.That(E.DoInstant(t, InstantRequest.FillPot(First(t, "clean_water", "camp").InstanceId, pot.InstanceId, 2)).Ok, Is.True);
        Assert.That(E.RootCardCount(t), Is.EqualTo(2), "锅与锅里的水合计1张");

        // 不同到期批次分别计数
        var u = New();
        E.DebugGive(u, "canned_food", 1);
        var a = u.Items.Last(i => i.ItemId == "canned_food");
        E.DebugGive(u, "raw_root", 1);
        E.DebugGive(u, "raw_root", 1);
        var roots = u.Items.Where(i => i.ItemId == "raw_root" && i.IsCarried).ToList();
        Assert.That(E.RootCardCount(u), Is.EqualTo(4));
        Assert.That(roots.Count, Is.EqualTo(1), "同批合堆为1张");
        {
            var other = roots[0].Clone();
            other.InstanceId = u.NextInstanceId++;
            other.Count = 1;
            other.ExpiresAt = roots[0].ExpiresAt + 60;
            roots[0].Count -= 1;
            u.Items.Add(other);
            Assert.That(E.RootCardCount(u), Is.EqualTo(5), "同物品不同到期批次各算1张");
        }
        Assert.That(a.IsCarried, Is.True);
    }

    [Test]
    public void RootCards_ReservationSplit_NotCounted()
    {
        var s = New();
        E.DebugGive(s, "wood", 15);
        var before = E.RootCardCount(s);
        var w = s.Items.First(i => i.ItemId == "wood" && i.IsCarried);
        // 模拟制作预留拆堆：拆出5份标记预留，仍在随身
        w.Count -= 5;
        var r = w.Clone();
        r.InstanceId = s.NextInstanceId++;
        r.Count = 5;
        r.ReservedBy = 99;
        s.Items.Add(r);
        Assert.That(E.RootCardCount(s), Is.EqualTo(before), "制作预留临时拆堆不算新卡（开发假设 H1）");
    }

    [Test]
    public void HeatProtected_RoundTripsInSave()
    {
        var s = New();
        s.Locations["camp"].Facility.Processes.Add(new FacilityProcess { ProcessId = "roast_root#1", RecipeId = "roast_root", TargetInstanceId = 5, HeatProtected = true });
        var json = SaveSerializer.Serialize(s);
        var back = SaveSerializer.Deserialize(json, Cfg);
        Assert.That(back.Locations["camp"].Facility.Processes.Single().HeatProtected, Is.True);
        Assert.That(back.SchemaVersion, Is.EqualTo(GameState.CurrentSchema));
    }
}
