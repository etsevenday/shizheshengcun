using Newtonsoft.Json.Linq;
using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 批次 B53a「永久丢弃与晾制待清理」（开发记录 B53a）：S06／S10／S11／S12／S14／逐字稿文首「2026-11-15 B50a Q763—Q768与永久丢弃」与
/// 「2026-11-17 B53主要原批Q857—Q861」中有右栏的入口（随身、当前地面、锅内食材、锅水、壶水）；合同 assets/20261117/b53-discard-main-q857-q861.json。
/// 无右栏清单行末按钮（同伴随身／柜子／遗物认领／回收／封存）属 B53b，不在这里。
/// </summary>
public class B53aTests : TestBase
{
    protected override bool NoEnemyApproach => true;

    private const string Rot = GameEngine.SpoiledMatterId;
    private const string Lin = "npc_lin_zhao";

    // 逐字稿「2026-11-15 B50a」表 rotNotEdible；S12 文首 Q767
    private const string NotEdible = "腐败物不可食用。";

    private static string Bag(string actor) => ItemInstance.CarriedOwner(actor);

    private ItemInstance Give(GameState s, string item, string owner, int count = 1) => E.CreateItems(s, item, count, owner).Single();

    /// <summary>空闲等待 minutes 分钟（按 5 分钟段推进，世界流程里做到期转换）。</summary>
    private void Wait(GameState s, int minutes)
    {
        var until = s.GameMinute + minutes;
        while (s.GameMinute < until)
        {
            if (s.Main == null) Start(s, ActionRequest.Wait(30));
            E.Advance(s);
        }
        if (s.Main?.Type == ActionType.Wait) E.Cancel(s);
    }

    /// <summary>野果在 5 分钟后到期，等一段让世界流程把它真实转为腐败物（不直接写 ItemId）。</summary>
    private ItemInstance RotBerries(GameState s, string owner, int count)
    {
        var b = Give(s, "berry", owner, count);
        b.ExpiresAt = s.GameMinute + 5;
        Wait(s, 5);
        var rot = s.Items.Single(i => i.InstanceId == b.InstanceId);
        Assert.That(rot.ItemId, Is.EqualTo(Rot), "前提：到期后原位转为腐败物");
        return rot;
    }

    private GameState Party()
    {
        var s = New();
        s.Progress.Flags.Add("hub_registered");
        s.Progress.Flags.Add("church_route_record");
        s.Progress.Flags.Add("academy_safety_record");
        s.SpawnNextCheck = int.MaxValue;
        s.PatrolNextCheck = int.MaxValue;
        s.GameMinute = At(1, 10, 0);
        E.DebugRecruit(s, Lin);
        s.Items.RemoveAll(i => i.Owner == Bag(Lin));
        foreach (var a in s.Actors.Values)
        {
            a.Stats.Satiety = 50;
            a.Stats.Hydration = 50;
        }
        return s;
    }

    // ───────────── M1 Q767：腐败物不能食用／喂食，同伴行去重复括注 ─────────────

    /// <summary>Q767-A01：本人食用腐败物，预检与真实提交都拒「腐败物不可食用。」，不扣物、不改属性、不耗时间。</summary>
    [Test]
    public void Q767_A01_SelfUse_RotRejected_NothingChanges()
    {
        var s = New();
        var rot = RotBerries(s, ItemInstance.Carried, 2);
        Assert.That(E.CanStart(s, ActionRequest.UseInstance(rot.InstanceId)), Is.EqualTo(NotEdible));
        var before = SaveSerializer.Serialize(s);
        var res = E.Start(s, ActionRequest.UseInstance(rot.InstanceId));
        Assert.That((res.Ok, res.Reason), Is.EqualTo((false, NotEdible)));
        Assert.That(SaveSerializer.Serialize(s), Is.EqualTo(before), "拒绝不扣物、不改人物、不推进时间");
        // 按物品 ID 的旧请求同样拒绝
        Assert.That(E.CanStart(s, ActionRequest.UseItem(Rot)), Is.EqualTo(NotEdible));
    }

    /// <summary>对照：同样的新鲜野果可以吃（证明上面的拒绝走的是腐败物判定，不是别的阻断）。</summary>
    [Test]
    public void Q767_A01_SelfUse_FreshBerry_Allowed()
    {
        var s = New();
        var b = Give(s, "berry", ItemInstance.Carried, 2);
        Assert.That(E.CanStart(s, ActionRequest.UseInstance(b.InstanceId)), Is.Null);
    }

    /// <summary>Q767-A01：屠宰等其他原用途仍是原拒绝句（S06 只改食饮）。</summary>
    [Test]
    public void Q767_Butcher_KeepsOriginalRejectText()
    {
        var s = New();
        Give(s, "hatchet", ItemInstance.Carried);
        var dog = Give(s, "carcass_feral_dog", s.Ground);
        dog.ExpiresAt = s.GameMinute + 5;
        Wait(s, 5);
        Assert.That(s.Items.Single(i => i.InstanceId == dog.InstanceId).ItemId, Is.EqualTo(Rot));
        Assert.That(E.CanStart(s, ActionRequest.Butcher(dog.InstanceId)), Is.EqualTo("腐败物不能用于此操作。"));
    }

    /// <summary>Q767-A01：给同伴喂腐败物（直接实例请求）拒「腐败物不可食用。」，不扣物、不改人、不耗时。</summary>
    [Test]
    public void Q767_A01_Feed_RotRejected()
    {
        var s = Party();
        var rot = RotBerries(s, ItemInstance.Carried, 1);
        var f = new FeedRequest { TargetActorId = Lin, SourceActorId = GameState.PlayerId, InstanceId = rot.InstanceId };
        var before = SaveSerializer.Serialize(s);
        Assert.That(E.FeedBlocked(s, f), Is.EqualTo(NotEdible));
        var res = E.Feed(s, f);
        Assert.That((res.Ok, res.Reason), Is.EqualTo((false, NotEdible)));
        Assert.That(SaveSerializer.Serialize(s), Is.EqualTo(before));
        Assert.That(E.FeedSources(s, Lin).Any(r => r.InstanceId == rot.InstanceId), Is.False, "腐败物不进合法喂食候选");
    }

    /// <summary>Q767-A01：预览时还是新鲜野果，确认前变成腐败物——真实提交拒「腐败物不可食用。」（不是「情况已变」）；对照：未变腐败时可以喂。</summary>
    [Test]
    public void Q767_A01_Feed_TurnedRotAfterPreview_RejectedAsNotEdible()
    {
        var s = Party();
        var b = Give(s, "berry", ItemInstance.Carried, 1);
        var f = new FeedRequest { TargetActorId = Lin, SourceActorId = GameState.PlayerId, InstanceId = b.InstanceId };
        var p = E.FeedPreview(s, f);
        Assert.That(p.Ok, Is.True, p.Reason);
        f.ConfirmStamp = p.Feed!.Source.Stamp;
        Assert.That(E.FeedBlocked(s, f), Is.Null, "对照：没变腐败时确认可通过");

        b.ExpiresAt = s.GameMinute + 5;
        Wait(s, 5);
        Assert.That(b.ItemId, Is.EqualTo(Rot));
        var res = E.Feed(s, f);
        Assert.That((res.Ok, res.Reason), Is.EqualTo((false, NotEdible)));
        Assert.That(b.Count, Is.EqualTo(1));
    }

    /// <summary>Q767 选 A：同伴随身腐败物行只写名称与数量，不再接「（已腐败）」；尚未转换的过期原物仍附（S06「不全局删词」）。</summary>
    [Test]
    public void Q767_CompanionRow_RotHasNoSuffix_ExpiredOriginalKeeps()
    {
        var s = Party();
        var rot = RotBerries(s, Bag(Lin), 2);
        Assert.That(E.CompanionItemLine(s, rot), Is.EqualTo("腐败物×2"));
        var old = Give(s, "berry", Bag(Lin), 2);
        old.ExpiresAt = s.GameMinute;   // 到期但还没经过转换事务的原物
        Assert.That(E.CompanionItemLine(s, old), Is.EqualTo("野果×2（已腐败）"));
    }
}
