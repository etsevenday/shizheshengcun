using Survival.Core.Engine;

namespace Survival.Core.Tests;

/// <summary>
/// B44（R175—R177）：S14「BM1 Q492—Q494 人物页入口与悬停版文案（2026-11-12）」，验收 Q492-A01—A02、Q493-A01、Q494-A01；
/// 逐字稿「BM1 Q492—Q494 名称查看与悬停脚注定稿」。测试工程不引用网页壳，接线按组件源码核对（同 BM1B42HoverTests）。
/// </summary>
public class B44NameViewTests : TestBase
{
    private static string Web(string relative)
    {
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "doc", "策划案"))) dir = dir.Parent;
        Assert.That(dir, Is.Not.Null, "找不到仓库根目录");
        return File.ReadAllText(Path.Combine(dir!.FullName, "web", "Survival.Web", relative));
    }

    private static string Body(string src, string anchor, int len)
    {
        var at = src.IndexOf(anchor, StringComparison.Ordinal);
        Assert.That(at, Is.GreaterThanOrEqualTo(0), anchor);
        return src.Substring(at, Math.Min(len, src.Length - at));
    }

    // ───────────── Q492 人物页名称点击查看 ─────────────

    /// <summary>Q492：可访问名称与键帽逐字（「查看{物品名}详情」「查看{姓名}详情」「ENTER」）；只有已穿戴装备与队员两类键有名称入口。</summary>
    [Test]
    public void Q492_LabelCapAndAllowedKinds()
    {
        Assert.That(PeekNameView.Label("砍刀"), Is.EqualTo("查看砍刀详情"));
        Assert.That(PeekNameView.Label("林照"), Is.EqualTo("查看林照详情"));
        Assert.That(PeekNameView.Cap, Is.EqualTo("ENTER"));
        Assert.That(PeekNameView.Allowed(PeekKey.Make(PeekKey.Worn, "7")), Is.True);
        Assert.That(PeekNameView.Allowed(PeekKey.Make(PeekKey.Member, "player")), Is.True);
        foreach (var k in new[] { PeekKey.Make(PeekKey.Gear, "7"), PeekKey.Make(PeekKey.Card, "fac-shelter"), PeekKey.Make(PeekKey.Status, "cold"), "", null })
            Assert.That(PeekNameView.Allowed(k), Is.False, k ?? "null");
    }

    /// <summary>Q492-A02：点「人物」返回时右栏仍显示由名称打开的那一项才回到原名称；已被别的悬停替换或已清掉不恢复；返回后记录结束（恢复焦点不再进详情）。</summary>
    [Test]
    public void Q492_ReturnRestoresOnlyTheSameObject()
    {
        var v = new PeekNameView();
        var w = PeekKey.Make(PeekKey.Worn, "12");
        Assert.That(v.Open(PeekKey.Make(PeekKey.Card, "x")), Is.False, "别的键不受理");
        Assert.That(v.Key, Is.Null);
        Assert.That(v.Open(w), Is.True);
        Assert.That(v.Return(w), Is.EqualTo(w), "仍在显示：焦点回原名称");
        Assert.That(v.Key, Is.Null, "返回即结束");
        Assert.That(v.Return(w), Is.Null, "不会第二次恢复");
        v.Open(w);
        Assert.That(v.Return(PeekKey.Make(PeekKey.Sel, "Resource", "wood", "", "")), Is.Null, "已被右栏外悬停替换：不拉回焦点");
        v.Open(w);
        Assert.That(v.Return(null), Is.Null, "已清掉：不拉回焦点");
        v.Open(w);
        v.Reset();
        Assert.That(v.Key, Is.Null, "读档、新局作废");
    }

    /// <summary>Q492-A01：已穿戴槽行、队员行不再是悬停热区（不带 data-peek／data-peek-rail，行本身不可 Tab），名称是独立按钮；空槽写「空」无入口。</summary>
    [Test]
    public void Q492_RowsNotHotZones_NameIsButton()
    {
        var c = Web("Components/CharacterPanel.razor");
        Assert.That(c, Does.Contain("<div class=\"equip-row @(it == null ? \"empty\" : \"\")\" data-slot=\"@slot\">"), "槽行不带悬停键、不可聚焦");
        Assert.That(c, Does.Not.Contain("data-peek=").And.Not.Contain("data-peek-rail"));
        Assert.That(c, Does.Contain("<button type=\"button\" class=\"eq-name peek-name q-@(it.Quality ?? \"white\")\" data-peek-name=\"@Peek.Worn(it.InstanceId)\" aria-label=\"@PeekNameView.Label(name)\" @onclick=\"() => G.ViewByName(Peek.Worn(it.InstanceId))\" @onclick:stopPropagation><span class=\"peek-name-text\">@name</span><kbd class=\"kc peek-name-cap\" aria-hidden=\"true\">@PeekNameView.Cap</kbd></button>"));
        Assert.That(c, Does.Contain(">空</span>"), "空槽仍写「空」");
        Assert.That(c.IndexOf("ViewByName", StringComparison.Ordinal), Is.GreaterThan(c.IndexOf("@if (it == null)", StringComparison.Ordinal)), "名称入口只在有装备的分支里");
        Assert.That(c, Does.Contain("<span class=\"eq-btns\">"), "卸下、维修、现场修补、放当前地面按钮照旧、与名称分开");

        var p = Web("Components/PartyPanel.razor");
        Assert.That(p, Does.Contain("data-actor=\"@id\">"));
        Assert.That(p, Does.Not.Contain("data-peek=").And.Not.Contain("data-peek-rail").And.Not.Contain("tabindex=\"0\""));
        Assert.That(p, Does.Contain("<button type=\"button\" class=\"peek-name\" data-peek-name=\"@Peek.Member(id)\" aria-label=\"@PeekNameView.Label(name)\" @onclick=\"() => G.ViewByName(Peek.Member(id))\" @onclick:stopPropagation><b>@name</b><kbd class=\"kc peek-name-cap\" aria-hidden=\"true\">@PeekNameView.Cap</kbd></button>"));
        Assert.That(p, Does.Contain("var name = isMe ? \"你\""), "「你」同样有姓名入口");
        foreach (var btn in new[] { "G.StayInSafehouse(id)", "G.SetActive(id, !active)", "G.TogglePartyItems(id)" })
            Assert.That(p, Does.Contain(btn), "行内按钮照旧：" + btn);
    }

    /// <summary>Q492-A01：右栏里一律不发送悬停（人物页两类行悬停只高亮）；名称按钮不是悬停热区，聚焦它或恢复焦点不会进入详情。</summary>
    [Test]
    public void Q492_RailSendsNoHover()
    {
        var js = Web("wwwroot/js/app.js");
        var peek = js.Substring(js.IndexOf("window.survivalPeek", StringComparison.Ordinal));
        var hot = Body(peek, "function hotOf(t)", 700);
        Assert.That(hot, Does.Contain("if (el.closest('[data-detail-rail]')) return null;"));
        Assert.That(js, Does.Not.Contain("data-peek-rail"));
        var css = Web("wwwroot/css/cards.css");
        Assert.That(css, Does.Contain(".detail :is(.equip-row:not(.empty), .party-row):is(:hover, :focus-within) { background: #ffffff1f; }"), "悬停／聚焦只高亮所在行");
        Assert.That(css, Does.Contain(".detail .peek-name .peek-name-cap { display: none;").And.Contain(".detail .peek-name:focus .peek-name-cap { display: inline-block; }"), "焦点在名称上才显示 ENTER");
        Assert.That(css, Does.Contain(".detail .peek-name .peek-name-cap { display: none; position: absolute; left: 0; bottom: 100%;"), "M756：键帽显示／隐藏不改变布局（绝对定位），聚焦后再点同一行按钮不会移走");
        Assert.That(Web("Components/CharacterPanel.razor"), Does.Contain("<span class=\"eq-item\"><span class=\"eq-head\"><span class=\"eq-ico\""), "图标与名称成一组，重量放不下时整块换行");
    }

    /// <summary>Q492：名称激活与悬停同一受理条件、同一只读详情（PeekDetail 的 Worn／Member 分支），焦点交给详情标题；不改真正选中、页签记忆、当前成员与草稿。</summary>
    [Test]
    public void Q492_ViewByName_ReadOnlyAndFocusTitle()
    {
        var p = Web("Services/GameSession.Peek.cs");
        var body = Body(p, "public void ViewByName(string? key)", 400);
        Assert.That(body, Does.Contain("if (PeekLocked || PendingDrop != null || !PeekNameView.Allowed(key) || !PeekValid(key!)) return;"));
        Assert.That(body, Does.Contain("_peek.Enter(key, PeekContext());"));
        Assert.That(body, Does.Contain("_railFocus = \"title\";"));
        foreach (var bad in new[] { "Selected =", "Select(", "DetailTab =", "GearActor =", "SetActive(", "TogglePartyItems(", "Guide(", "NoteObjectInspected", "Commit(" })
            Assert.That(body, Does.Not.Contain(bad), bad);
        Assert.That(Body(p, "private void ResetPeek()", 200), Does.Contain("_nameView.Reset();"));
        // 点「人物」：仍显示由名称打开的那一项才回原名称；「对象」只结束记录；回收箱跳转不是读者返回
        var s = Web("Services/GameSession.cs");
        var tab = Body(s, "public void SetDetailTab(DetailTab tab)", 900);
        Assert.That(tab, Does.Contain("if (tab == DetailTab.Character) ReturnFromNameView(); else _nameView.Reset();"));
        Assert.That(tab.IndexOf("ReturnFromNameView()", StringComparison.Ordinal), Is.LessThan(tab.IndexOf("PeekClear();", StringComparison.Ordinal)), "先按当前显示判定再清");
        Assert.That(Body(s, "public void ShowDungeonBoxes(string dungeonId)", 500), Does.Contain("_nameView.Reset();"));
        // 右栏绘制后交焦点：标题或原名称（原名称不在回「人物」页签）
        var d = Web("Components/DetailPanel.razor");
        Assert.That(d, Does.Contain("if (G.TakeRailFocus() is not string req) return;"));
        Assert.That(d, Does.Contain("await JS.InvokeVoidAsync(\"survivalPeek.railFocus\", req);"));
        Assert.That(d, Does.Contain("data-tab-character @onclick=\"() => G.SetDetailTab(DetailTab.Character)\""));
        var js = Web("wwwroot/js/app.js");
        var rf = Body(js, "railFocus: function (req)", 800);
        Assert.That(rf, Does.Contain("el = rail.querySelector('[data-peek-shown] h2');"));
        Assert.That(rf, Does.Contain("|| rail.querySelector('[data-tab-character]');"));
        // 详情仍是原 Q483 同一份只读事实
        var pd = Web("Components/PeekDetail.razor");
        Assert.That(pd, Does.Contain("<GearPeekDetail Item=\"wit\" />").And.Contain("<MemberPeekDetail ActorId=\"@first\" />"));
    }

    /// <summary>Q492-A02：二级面板开着时，键盘聚焦在右栏控件（名称、行内按钮）上按回车只作用该控件，不穿透到面板的唯一确认。</summary>
    [Test]
    public void Q492_EnterOnRailControlNotStolenByPanel()
    {
        var js = Web("wwwroot/js/app.js");
        Assert.That(js, Does.Contain("&& !(fae.closest('[data-detail-rail]') && (focusVisible(fae) || fae.hasAttribute('data-peek-name')))) fae = null;"));
        Assert.That(js, Does.Contain("function focusVisible(el) { try { return !!el.matches(':focus-visible'); } catch (e) { return false; } }"));
    }

    // ───────────── Q493 未建设施悬停脚注 ─────────────

    /// <summary>Q493-A01：未建设施悬停版底部脚注逐字「时间为预计值」；点击进入制作后的右栏原脚注（含「在中间配方卡下方点…」）不变。</summary>
    [Test]
    public void Q493_UnbuiltHoverFootNote()
    {
        Assert.That(PeekText.UnbuiltBuildFootNote, Is.EqualTo("时间为预计值"));
        var co = Web("Components/CraftOverlay.razor");
        Assert.That(co, Does.Contain("FootNote=\"@(UnbuiltPeek ? PeekText.UnbuiltBuildFootNote : rc.IsDrying ?"));
        Assert.That(co, Does.Contain("$\"在中间配方卡下方点“{StartLabel(rc)}”；时间为预计值\""), "点击进入制作后原脚注保持");
        Assert.That(co, Does.Contain("[Parameter] public bool UnbuiltPeek { get; set; }"));
        var fc = Web("Components/FacilityCard.razor");
        Assert.That(fc, Does.Contain("=> @<CraftOverlay Part=\"detail\" PeekRecipe=\"@recipeId\" UnbuiltPeek=\"true\" />;"), "只有未建设施悬停版传入");
        Assert.That(Web("Components/PeekDetail.razor"), Does.Contain("<CraftOverlay Part=\"detail\" PeekRecipe=\"@first\" />"), "制作面板里悬停配方仍是原脚注");
        Assert.That(fc, Does.Contain("\"点击查看建造条件\"").And.Contain("\"需先建成遮蔽棚\""), "卡面／顶部原说明保留");
    }

    // ───────────── Q494 严格去重（维持 Q486） ─────────────

    /// <summary>Q494-A01：副标末尾「（另N条）」不剥离：完整原文不满足相同或包含就两行都留；满足（含后缀仍被整段包含）照样去重。</summary>
    [Test]
    public void Q494_CountSuffixNotStripped()
    {
        Assert.That(PeekText.SubRepeatsDesc("建安全屋还差：缺木材、缺布料", "建安全屋还差：缺木材（另1条）"), Is.False, "近乎同句但互不包含：两行都留");
        Assert.That(PeekText.SubRepeatsDesc("建安全屋还差：缺木材", "建安全屋还差：缺木材（另1条）"), Is.True, "副标连后缀整段包含说明：照原规则去重");
        Assert.That(PeekText.SubRepeatsDesc("建安全屋还差：缺木材（另1条）；其余见点击", "缺木材（另1条）"), Is.True, "说明整段包含带后缀的副标：去重");
        Assert.That(PeekText.SubRepeatsDesc("建安全屋还差：缺木材（另2条）", "建安全屋还差：缺木材（另1条）"), Is.False, "后缀不同不算相同");
        // 安全屋卡传给比较的仍是带后缀的原文
        Assert.That(Web("Components/SafehouseCard.razor"), Does.Contain("$\"（另{reasons.Count - 1}条）\""));
    }
}
