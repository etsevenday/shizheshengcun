using Survival.Core.Engine;

namespace Survival.Core.Tests;

/// <summary>
/// BM1 次要问题合并（R171）：B42 全局悬停右栏补充 Q483—Q487（S14「B42 Q483—Q487 悬停范围与详情补充」，验收 Q483-A01—Q487-A01；
/// 逐字稿「B42 Q483—Q487 展示与原句复用」）。测试工程不引用网页壳，接线按组件源码核对（同 B42HoverPeekTests）。
/// </summary>
public class BM1B42HoverTests
{
    private static string Web(string relative)
    {
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "doc", "策划案"))) dir = dir.Parent;
        Assert.That(dir, Is.Not.Null, "找不到仓库根目录");
        return File.ReadAllText(Path.Combine(dir!.FullName, "web", "Survival.Web", relative));
    }

    // ───────────── Q483 人物页装备槽行、队员行 ─────────────

    /// <summary>Q483：两类新对象键用真实身份（实例号、成员 ID），可往返解析。</summary>
    [Test]
    public void Q483_Keys_RoundTrip()
    {
        var w = PeekKey.Make(PeekKey.Worn, "42");
        Assert.That(PeekKey.KindOf(w), Is.EqualTo(PeekKey.Worn));
        Assert.That(PeekKey.Part(w, 0), Is.EqualTo("42"));
        var m = PeekKey.Make(PeekKey.Member, "npc_lin_zhao");
        Assert.That(PeekKey.KindOf(m), Is.EqualTo(PeekKey.Member));
        Assert.That(PeekKey.Part(m, 0), Is.EqualTo("npc_lin_zhao"));
        var shell = Web("Services/GameSession.Peek.cs");
        Assert.That(shell, Does.Contain("public static string Worn(int instanceId) => PeekKey.Make(PeekKey.Worn, instanceId.ToString());"));
        Assert.That(shell, Does.Contain("public static string Member(string actorId) => PeekKey.Make(PeekKey.Member, actorId);"));
    }

    /// <summary>Q483-A01：已穿戴槽行与队员行是热区（右栏里只放行这两类，data-peek-rail），空槽不造对象；行内按钮与展开的随身物品不算热区；可键盘聚焦。</summary>
    [Test]
    public void Q483_CharacterPageRowsAreHotZones()
    {
        var c = Web("Components/CharacterPanel.razor");
        Assert.That(c, Does.Contain("data-peek=\"@(it != null ? Peek.Worn(it.InstanceId) : null)\" data-peek-rail=\"@(it != null ? \"\" : null)\" tabindex=\"@(it != null ? \"0\" : null)\""), "只有已穿戴槽是入口，空槽不造对象");
        Assert.That(c, Does.Contain("<span class=\"eq-btns\" data-peek=\"\">"), "卸下、维修、现场修补、放当前地面按钮区不触发");
        var p = Web("Components/PartyPanel.razor");
        Assert.That(p, Does.Contain("data-actor=\"@id\" data-peek=\"@Peek.Member(id)\" data-peek-rail tabindex=\"0\""));
        Assert.That(p, Does.Contain("<span class=\"party-acts\" data-peek=\"\">"));
        Assert.That(p, Does.Contain("party-items-btn\" data-peek=\"\""), "展开随身物品按钮不触发");
        Assert.That(p, Does.Contain("data-party-items=\"@id\" data-peek=\"\""), "展开的随身物品列表不触发");
    }

    /// <summary>Q483-A01：有效性只看对象本身（仍穿在同行／候补身上、成员仍在队伍），来源行因右栏换页卸载不算失效；全屏战斗不受理。</summary>
    [Test]
    public void Q483_ValidityByObjectNotRow()
    {
        var s = Web("Services/GameSession.Peek.cs");
        Assert.That(s, Does.Contain("case PeekKey.Worn:"));
        Assert.That(s, Does.Contain("&& ItemInstance.TryEquip(wi.Owner, out var wearer, out _) && MemberShown(wearer);"));
        Assert.That(s, Does.Contain("case PeekKey.Member:\n                return !BattleFullscreen && MemberShown(first);").Or.Contain("case PeekKey.Member:\r\n                return !BattleFullscreen && MemberShown(first);"));
        Assert.That(s, Does.Contain("S.Party.Contains(actorId) || E.Reserves(S).Any(r => r.ActorId == actorId);"));
        Assert.That(s, Does.Not.Contain("DetailTab ="), "悬停不改页签记忆");
    }

    /// <summary>Q483-A01：装备复用整备只读事实（Q462）；队员详情只读该队员自己的公开文字与数值，不带任何执行控件、不切当前成员、不展开队伍。</summary>
    [Test]
    public void Q483_DetailsReadOnly()
    {
        var d = Web("Components/PeekDetail.razor");
        Assert.That(d, Does.Contain("case PeekKey.Worn:"));
        Assert.That(d, Does.Contain("<GearPeekDetail Item=\"wit\" />"));
        Assert.That(d, Does.Contain("<MemberPeekDetail ActorId=\"@first\" />"));
        var m = Web("Components/MemberPeekDetail.razor");
        foreach (var bad in new[] { "<button", "@onclick", "SetActive(", "StayInSafehouse(", "TogglePartyItems(", "GearPickActor(", "OpenPanel(", "FedActor", "s.Player.Stats", "s.Stats" })
            Assert.That(m, Does.Not.Contain(bad), bad);
        Assert.That(m, Does.Contain("var st = a.Stats;"), "读该队员自己的数值");
        Assert.That(m, Does.Contain("健@(Fmt.T(st.Health)) 饱@(Fmt.T(st.Satiety)) 水@(Fmt.T(st.Hydration)) 体@(Fmt.T(st.Energy)) 醒@(Fmt.T(st.Wakefulness)) 精@(Fmt.T(st.Morale))"), "沿队伍行原写法");
        Assert.That(m, Does.Contain("武器：@weapons"));
        // 右栏优先级与页签：悬停显示时页签高亮「对象」，人物页仍是底层页签；点「人物」结束覆盖
        var dp = Web("Components/DetailPanel.razor");
        Assert.That(dp, Does.Contain("var shownTab = gearPeek != null || peekShown ||"));
        Assert.That(dp, Does.Contain("if (PeekKey.KindOf(peekKey) is PeekKey.Foe or PeekKey.Ally) peekKey = null;"), "只有战斗键交给战斗面板，人物页两类键照常显示");
    }

    // ───────────── Q485 右栏对象详情内嵌卡 ─────────────

    /// <summary>Q485-A01：右栏里内嵌的对象卡悬停或键盘聚焦都不替换整页（JS 在右栏内只放行 data-peek-rail）；右栏外的嵌套对象仍按最内层；原点击不变。</summary>
    [Test]
    public void Q485_EmbeddedCardsInRailDoNotReplace()
    {
        var js = Web("wwwroot/js/app.js");
        var i = js.IndexOf("window.survivalPeek", StringComparison.Ordinal);
        var peek = js.Substring(i);
        var hot = peek.Substring(peek.IndexOf("function hotOf(t)", StringComparison.Ordinal), 600);
        Assert.That(hot, Does.Contain("if (el.closest('[data-detail-rail]') && !el.hasAttribute('data-peek-rail')) return null;"));
        Assert.That(peek, Does.Contain("var el = hotOf(ev.target);"), "鼠标走同一过滤");
        Assert.That(peek, Does.Contain("var el = hotOf(t);"), "键盘聚焦走同一过滤");
        Assert.That(Web("Components/DetailPanel.razor"), Does.Contain("<aside class=\"panel detail\" data-detail-rail"));
        // 右栏外的火堆卡里锅／锅内物仍是各自热区（最内层）
        Assert.That(Web("Components/PotCard.razor"), Does.Contain("data-peek=\"@LiquidPeek(w.ItemId)\"").And.Contain("data-peek=\"@FoodPeek(f)\""));
    }
}
