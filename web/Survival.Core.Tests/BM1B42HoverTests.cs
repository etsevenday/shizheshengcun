using Survival.Core.Engine;

namespace Survival.Core.Tests;

/// <summary>
/// BM1 次要问题合并（R171）：B42 全局悬停右栏补充 Q483—Q487（S14「B42 Q483—Q487 悬停范围与详情补充」，验收 Q483-A01—Q487-A01；
/// 逐字稿「B42 Q483—Q487 展示与原句复用」）。测试工程不引用网页壳，接线按组件源码核对（同 B42HoverPeekTests）。
/// </summary>
public class BM1B42HoverTests : TestBase
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

    // Q483-A01 原「已穿戴槽行与队员行是悬停热区」已由 B44（Q492 选 B）取代：触发方式改为点击名称，见 B44NameViewTests。

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

    /// <summary>Q485-A01：右栏里内嵌的对象卡悬停或键盘聚焦都不替换整页（B44 起右栏内一律不发送悬停，人物页两类行改为点名称）；右栏外的嵌套对象仍按最内层；原点击不变。</summary>
    [Test]
    public void Q485_EmbeddedCardsInRailDoNotReplace()
    {
        var js = Web("wwwroot/js/app.js");
        var i = js.IndexOf("window.survivalPeek", StringComparison.Ordinal);
        var peek = js.Substring(i);
        var hot = peek.Substring(peek.IndexOf("function hotOf(t)", StringComparison.Ordinal), 600);
        Assert.That(hot, Does.Contain("if (el.closest('[data-detail-rail]')) return null;"));
        Assert.That(peek, Does.Contain("var el = hotOf(ev.target);"), "鼠标走同一过滤");
        Assert.That(peek, Does.Contain("var el = hotOf(t);"), "键盘聚焦走同一过滤");
        Assert.That(Web("Components/DetailPanel.razor"), Does.Contain("<aside class=\"panel detail\" data-detail-rail"));
        // 右栏外的火堆卡里锅／锅内物仍是各自热区（最内层）
        Assert.That(Web("Components/PotCard.razor"), Does.Contain("data-peek=\"@LiquidPeek(w.ItemId)\"").And.Contain("data-peek=\"@FoodPeek(f)\""));
    }

    // ───────────── Q486 说明与副标去重 ─────────────

    /// <summary>Q486-A01：去首尾空白后相同或任一句整段包含另一句只留说明；说明空留副标；互不包含各留；不做近义或拆词判断。</summary>
    [Test]
    public void Q486_SubRepeatsDesc()
    {
        Assert.That(PeekText.SubRepeatsDesc("营地可遮蔽睡眠，休息额外恢复精神", "营地可遮蔽睡眠，休息额外恢复精神"), Is.True, "相同");
        Assert.That(PeekText.SubRepeatsDesc("  已建工作台  ", "已建工作台"), Is.True, "去首尾空白后相同");
        Assert.That(PeekText.SubRepeatsDesc("遮蔽棚未建造：点击查看建造条件", "点击查看建造条件"), Is.True, "说明整段包含副标");
        Assert.That(PeekText.SubRepeatsDesc("可以建", "可以建：90分钟"), Is.True, "副标整段包含说明");
        Assert.That(PeekText.SubRepeatsDesc(null, "点击查看建造条件"), Is.False, "说明空保留副标");
        Assert.That(PeekText.SubRepeatsDesc("  ", "点击查看建造条件"), Is.False);
        Assert.That(PeekText.SubRepeatsDesc("遮蔽棚未建造", null), Is.False);
        Assert.That(PeekText.SubRepeatsDesc("营地可遮蔽睡眠", "可遮蔽 睡眠"), Is.False, "不拆词、不按近义判断");
        Assert.That(PeekText.SubRepeatsDesc("建安全屋还差：缺木材", "遮蔽·有柜"), Is.False, "互不包含各留");
    }

    /// <summary>Q486-A01：只在右栏去重（卡面副标不变）；标题、角标、第二行、正文照留；图标片段副标不做文字比较。</summary>
    [Test]
    public void Q486_OnlyRailBodyDeduped()
    {
        var oc = Web("Components/ObjectCard.razor");
        Assert.That(oc, Does.Contain("private bool PeekSubDup => PeekText.SubRepeatsDesc(Tooltip ?? SubTitle, PeekSubText ?? Sub);"));
        Assert.That(oc, Does.Contain("else if (!string.IsNullOrEmpty(Sub) && !PeekSubDup) { <div class=\"k2-sub @SubClass\">@Sub</div> }"), "文字副标");
        Assert.That(oc, Does.Contain("@if (SubContent != null) { if (PeekSubText == null || !PeekSubDup) {"), "片段副标只在给了原文时比较");
        Assert.That(oc, Does.Contain("@if (ChildContent != null && (SubContent != null || PeekSubText == null || !PeekSubDup))"), "旧模板单行正文同理");
        // 安全屋卡、文书卡的副标是单行片段：传该行原文参与比较
        Assert.That(Web("Components/SafehouseCard.razor"), Does.Contain("PeekSubText=\"@(cab != null ? lead + cab : line + (!built && reasons.Count > 1 ? $\"（另{reasons.Count - 1}条）\" : \"\"))\""));
        Assert.That(Web("Components/PrologueCards.razor"), Does.Contain("PeekSubText=\"@d.CardLine\"").And.Contain("<div class=\"oc-line\" title=\"@d.CardLine\">@d.CardLine</div>"));
        Assert.That(oc, Does.Contain("else if (!string.IsNullOrEmpty(Sub)) { <div class=\"k2-sub @SubClass\" title=\"@(SubTitle ?? Sub)\">@Sub</div> }"), "卡面副标照旧");
        Assert.That(oc, Does.Contain("Desc = Tooltip ?? SubTitle,"));
        Assert.That(oc, Does.Contain("@if (Line2 != null) { <div class=\"k2-l2\">@Line2</div> }"));
    }

    // ───────────── Q487 未建设施建造条件 ─────────────

    /// <summary>Q487-A01：未建遮蔽棚、床垫（及一座工坊都没有时的工坊卡）悬停直接带与原点击同源的建造条件详情；原说明句保留；建成后不再带。</summary>
    [Test]
    public void Q487_UnbuiltFacilityShowsBuildConditions()
    {
        foreach (var id in new[] { "shelter", "bed", "build_workbench" })
            Assert.That(E.Recipe(id).IsBuild, Is.True, id);
        var fc = Web("Components/FacilityCard.razor");
        Assert.That(fc, Does.Contain("var buildRecipe = built ? null : Kind == \"workshops\" ? \"build_workbench\" : Kind;"), "按原点击对应的设施配方");
        Assert.That(fc, Does.Contain("if (Kind != \"workshops\") { G.OpenPanel(\"craft\", Kind); return; }"), "原点击照旧打开对应配方");
        Assert.That(fc, Does.Contain("G.OpenPanel(\"craft\", \"build_workbench\");"));
        Assert.That(fc, Does.Contain("PeekExtra=\"@(buildRecipe == null ? null : BuildConditions(buildRecipe))\""));
        Assert.That(fc, Does.Contain("=> @<CraftOverlay Part=\"detail\" PeekRecipe=\"@recipeId\" UnbuiltPeek=\"true\" />;"), "与点击后右栏同一份详细预估（B44 Q493：只换底部脚注）");
        Assert.That(fc, Does.Contain("Tooltip=\"@(built ? effect : $\"{title}未建造：{need}\")\""), "原说明句保留");
        Assert.That(fc, Does.Contain("\"点击查看建造条件\"").And.Contain("\"需先建成遮蔽棚\"").And.Contain("\"点击在“营地建造”里建\""));
        var oc = Web("Components/ObjectCard.razor");
        Assert.That(oc, Does.Contain("Extra = PeekExtra,"));
        var pd = Web("Components/PeekDetail.razor");
        Assert.That(pd, Does.Contain("if (G.PeekFactsOf(Key) is PeekFacts fx && fx.Extra != null)"));
        Assert.That(pd, Does.Contain("@if (!string.IsNullOrEmpty(fx.Desc)) { <p>@fx.Desc</p> }"), "原说明句在上");
        Assert.That(pd, Does.Contain("@fx.Extra"));
        // 同源详细预估本身只读：不给开工键，悬停的别的配方按点选初值、不改所选配方／份数／材料槽
        var co = Web("Components/CraftOverlay.razor");
        Assert.That(co, Does.Contain("var peekOn = Part != \"center\" && PeekRecipe != null;"));
        Assert.That(co, Does.Contain("Confirmable=\"false\" FootNote=\"@(UnbuiltPeek ? PeekText.UnbuiltBuildFootNote : rc.IsDrying"), "配方详情不给确认键");
        var at = co.IndexOf("@code {", StringComparison.Ordinal);
        var detailPart = co.Substring(co.IndexOf("else if (proc != null)", StringComparison.Ordinal), at - co.IndexOf("else if (proc != null)", StringComparison.Ordinal));
        Assert.That(detailPart, Does.Not.Contain("G.CraftRecipe =").And.Not.Contain("G.CraftBatch =").And.Not.Contain("StartAction"), "右栏部分不改制作选择、不开工");
    }
}
