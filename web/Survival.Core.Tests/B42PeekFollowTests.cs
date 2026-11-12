using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// B42 试玩修复 BUG-1（S14「2026-11-12 悬停卡片／条目立即显示右栏详情」HOVER-A05：「可见对象数量、归属……变化时以同一真实引用更新」
/// 「对象被移除、隐藏、失效时立即清」）：悬停的随身物品被放下（拖到地面、卡上▲放下同走 Drop）、放进火堆加工位后，
/// 原「物品+归属」不能再显示；同一实例仍在随身或当前地点就跟到新归属，找不到或不可见就清，不改指别的物品。
/// 网页壳 GameSession.FollowPeekItem 每次刷新先调这里，再做有效性校验。
/// </summary>
public class B42PeekFollowTests : TestBase
{
    private static void Ok(ActionResult r) => Assert.That(r.Ok, Is.True, r.Reason);

    private static PeekItemRef Group(string id, string owner, string? batch = null) => new(PeekItems.Group, id, owner, null, batch);
    private static PeekItemRef Tool(ItemInstance i) => new(PeekItems.Tool, i.ItemId, i.Owner, i.InstanceId, null);

    private GameState Clean(params string[] ids)
    {
        var s = New();
        s.Items.RemoveAll(i => ids.Contains(i.ItemId));
        return s;
    }

    /// <summary>整组随身物品放到地面（拖到地面与卡上▲放下同走 Drop）：原归属不再显示，跟到地面同一批实例。</summary>
    [Test]
    public void GroupDroppedToGround_FollowsToGround()
    {
        var s = Clean("wood");
        var wood = E.CreateItems(s, "wood", 3, ItemInstance.Carried).Single();
        var r = Group("wood", ItemInstance.Carried);
        Assert.That(PeekItems.Shown(E, s, r), Is.True);
        var members = PeekItems.Members(s, r);
        Ok(E.DoInstant(s, InstantRequest.Drop(wood.InstanceId, 3)));
        Assert.That(PeekItems.Shown(E, s, r), Is.False, "原归属已没有，不能停在「这件物品已不在此处」");
        var n = PeekItems.Follow(E, s, r, members);
        Assert.That(n, Is.Not.Null);
        Assert.That((n!.Value.Kind, n.Value.Id, n.Value.Owner), Is.EqualTo((PeekItems.Group, "wood", s.Ground)));
        Assert.That(PeekItems.Shown(E, s, n.Value), Is.True);
    }

    /// <summary>只放下一部分：原归属还有，悬停不动（同一引用的剩余部分）。</summary>
    [Test]
    public void PartialDrop_StaysOnOriginal()
    {
        var s = Clean("wood");
        var wood = E.CreateItems(s, "wood", 3, ItemInstance.Carried).Single();
        var r = Group("wood", ItemInstance.Carried);
        Ok(E.DoInstant(s, InstantRequest.Drop(wood.InstanceId, 1)));
        Assert.That(PeekItems.Shown(E, s, r), Is.True);
    }

    /// <summary>单件工具放到地面：按实例跟到地面。</summary>
    [Test]
    public void ToolDroppedToGround_FollowsSameInstance()
    {
        var s = Clean("stone_knife");
        var knife = E.CreateItems(s, "stone_knife", 1, ItemInstance.Carried).Single();
        var r = Tool(knife);
        var members = PeekItems.Members(s, r);
        Ok(E.DoInstant(s, InstantRequest.Drop(knife.InstanceId, 1)));
        Assert.That(PeekItems.Shown(E, s, r), Is.False, "只核对实例号会误判仍在，必须连同归属核对");
        var n = PeekItems.Follow(E, s, r, members);
        Assert.That(n, Is.Not.Null);
        Assert.That((n!.Value.Kind, n.Value.Owner, n.Value.Inst), Is.EqualTo((PeekItems.Tool, s.Ground, (int?)knife.InstanceId)));
    }

    /// <summary>拖进火堆成为加工位物品：跟到火堆加工位里的同一实例（按实例，与点击选中的工位内物品同一身份）。</summary>
    [Test]
    public void PutInFire_FollowsToFireSlot()
    {
        var s = Clean("raw_root");
        var root = E.CreateItems(s, "raw_root", 1, ItemInstance.Carried).Single();
        var r = Group("raw_root", ItemInstance.Carried);
        var members = PeekItems.Members(s, r);
        Ok(E.DoInstant(s, InstantRequest.PutInFire(root.InstanceId)));
        Assert.That(PeekItems.Shown(E, s, r), Is.False);
        var n = PeekItems.Follow(E, s, r, members);
        Assert.That(n, Is.Not.Null);
        var moved = E.FindItem(s, n!.Value.Inst ?? -1)!;
        Assert.That(moved.InFacility, Is.True);
        Assert.That((n.Value.Kind, n.Value.Owner), Is.EqualTo((PeekItems.Tool, moved.Owner)));
        Assert.That(PeekItems.Shown(E, s, n.Value), Is.True);
    }

    /// <summary>实例不在了（吃掉、合进别的堆等）或不在随身／当前地点：不跟随，交给有效性校验清掉；不改指同名别的物品。</summary>
    [Test]
    public void Gone_OrElsewhere_NoFollow()
    {
        var s = Clean("stone_knife");
        var knife = E.CreateItems(s, "stone_knife", 1, ItemInstance.Carried).Single();
        var other = E.CreateItems(s, "stone_knife", 1, s.Ground).Single();
        var r = Tool(knife);
        var members = PeekItems.Members(s, r);
        s.Items.Remove(knife);
        Assert.That(PeekItems.Shown(E, s, r), Is.False);
        Assert.That(PeekItems.Follow(E, s, r, members), Is.Null, "不按同物品改指地面上另一把");

        var far = E.CreateItems(s, "stone_knife", 1, ItemInstance.Carried).Single();
        var r2 = Tool(far);
        var m2 = PeekItems.Members(s, r2);
        far.Owner = s.CurrentLocation == "camp" ? "h02_square" : "camp";   // 留在别处（不在随身、不在当前地点）
        Assert.That(PeekItems.Follow(E, s, r2, m2), Is.Null);
        Assert.That(other.Owner, Is.EqualTo(s.Ground));
    }

    /// <summary>叠卡后层（带批次）挪走：跟到同一批实例的新归属，批次键按新位置的真实批次。</summary>
    [Test]
    public void BatchLayerMoved_FollowsWithBatch()
    {
        var s = Clean("berry");
        var a = E.CreateItems(s, "berry", 2, ItemInstance.Carried).Single();
        var key = GameEngine.BatchKeyOf(a);
        var r = Group("berry", ItemInstance.Carried, key);
        var members = PeekItems.Members(s, r);
        Ok(E.DoInstant(s, InstantRequest.Drop(a.InstanceId, 2)));
        var n = PeekItems.Follow(E, s, r, members);
        Assert.That(n, Is.Not.Null);
        Assert.That(n!.Value.Owner, Is.EqualTo(s.Ground));
        Assert.That(n.Value.Batch, Is.Not.Null);
        Assert.That(PeekItems.Shown(E, s, n.Value), Is.True);
    }

    /// <summary>网页壳接线：每次刷新先跟随再校验；物品键的有效性按归属、实例与批次核对（不只看实例号）；换对象时右栏滚动回顶。</summary>
    [Test]
    public void SessionWiring()
    {
        var dir = TestContext.CurrentContext.TestDirectory;
        string Web(string rel)
        {
            var d = new DirectoryInfo(dir);
            while (d != null && !Directory.Exists(Path.Combine(d.FullName, "Survival.Web"))) d = d.Parent;
            return File.ReadAllText(Path.Combine(d!.FullName, "Survival.Web", rel));
        }
        var peek = Web("Services/GameSession.Peek.cs");
        Assert.That(peek, Does.Contain("FollowPeekItem();\n        _peek.Reconcile(PeekContext(), PeekValid);").Or.Contain("FollowPeekItem();\r\n        _peek.Reconcile(PeekContext(), PeekValid);"));
        Assert.That(peek, Does.Contain("if (PeekItemOf(key) is PeekItemRef ir) return !BattleFullscreen && PeekItems.Shown(E, S, ir);"));
        Assert.That(peek, Does.Contain("PeekItems.Follow(E, S, r, _peekItems)"));
        Assert.That(Web("Components/PeekDetail.razor"), Does.Contain("JS.InvokeVoidAsync(\"survivalPeek.top\", \"[data-peek-shown]\")").And.Contain("if (_shownSerial == G.PeekSerial) return;"));
        Assert.That(Web("Components/BattlePanel.razor"), Does.Contain("survivalPeek.top"));
        Assert.That(Web("wwwroot/js/app.js"), Does.Contain("top: function (sel)"));
        var css = Web("wwwroot/css/cards.css");
        Assert.That(css, Does.Contain(".peek-card .peek-body .dir-mark { flex: 0 0 auto;").And.Contain(".peek-card .peek-body :is(.warn-line, .oc-line.warn-line) { color: #e7b46d; }"));
    }
}
