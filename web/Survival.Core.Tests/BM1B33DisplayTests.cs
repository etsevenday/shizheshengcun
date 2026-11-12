using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// BM1 次要问题合并（R150、R169）：B33 整备实装补充 Q477—Q482（《重墨 V2 开工裁定与界面逐字稿》「B33 Q477—Q482 实机显示补充」，
/// 验收 Q477-A01—Q482-A01）与 S01「B40 Q415 外部存档改动」（Q415-A01）。网页壳接线按组件源码核对（测试工程不引用网页壳，同 B33GearUiWiringTests）。
/// </summary>
public class BM1B33DisplayTests : BattleTestBase
{
    private const string P0 = GameState.PlayerId;

    private static string Repo(params string[] parts)
    {
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "doc", "策划案"))) dir = dir.Parent;
        Assert.That(dir, Is.Not.Null, "找不到仓库根目录");
        return File.ReadAllText(Path.Combine(new[] { dir!.FullName }.Concat(parts).ToArray()));
    }

    private static string Web(string relative) => Repo("web", "Survival.Web", relative);

    private static string Body(string src, string anchor, int len = 900)
    {
        var at = src.IndexOf(anchor, StringComparison.Ordinal);
        Assert.That(at, Is.GreaterThanOrEqualTo(0), anchor);
        return src.Substring(at, Math.Min(len, src.Length - at));
    }

    // ───────────── Q477 效果暂停单列（核对现状） ─────────────

    /// <summary>Q477-A01：效果暂停名单在「战斗」「求生」两组之后单独一行，不带组名、不归入任一组。</summary>
    [Test]
    public void Q477_PausedLineAfterBothGroups()
    {
        var c = Web("Components/GearCompare.razor");
        var iCombat = c.IndexOf("@GearPanelCopy.CombatGroup", StringComparison.Ordinal);
        var iSurvival = c.IndexOf("@GearPanelCopy.SurvivalGroup", StringComparison.Ordinal);
        var iPaused = c.IndexOf("@if (p.PausedChange != null) { <div class=\"g3-eg\">@p.PausedChange</div> }", StringComparison.Ordinal);
        Assert.That(iCombat, Is.GreaterThan(0));
        Assert.That(iSurvival, Is.GreaterThan(iCombat));
        Assert.That(iPaused, Is.GreaterThan(iSurvival), "暂停名单在两组之后单列、不带组名");
    }

    // ───────────── Q478 空值统一写「空」 ─────────────

    /// <summary>Q478-A01：七槽列表与比较列的空装备值都写「空」；不可比项目的「—」不变。</summary>
    [Test]
    public void Q478_EmptyValueIsKong()
    {
        Assert.That(GearPanelCopy.EmptySlot, Is.EqualTo("空"));
        Assert.That(GearPanelCopy.Dash, Is.EqualTo("—"), "不可比项目仍写「—」");
        var overlay = Web("Components/GearOverlay.razor");
        Assert.That(overlay, Does.Contain("<span class=\"muted\">@GearPanelCopy.EmptySlot</span>"), "七槽列表空值");
        var cmp = Web("Components/GearCompare.razor");
        Assert.That(cmp, Does.Contain("p.Old == null ? GearPanelCopy.EmptySlot : e.ItemName(p.Old.ItemId)"), "比较当前装备列空值");
        var core = Repo("src", "Core", "Engine", "GameEngine.GearPreview.cs");
        Assert.That(core, Does.Not.Contain("\"空槽\""), "不再有「空槽」文案常量");
    }

    /// <summary>Q478-A01：比较一侧缺少的项目仍写「—」（空槽当前装备列的各行），只有名称行写「空」。</summary>
    [Test]
    public void Q478_CompareEmptySideRowsKeepDash()
    {
        var s = New();
        var shirt = E.CreateItems(s, "work_shirt", 1, ItemInstance.Carried).Single();
        var p = E.EquipPreview(s, P0, shirt.InstanceId, EquipSlots.Top, null)!;
        Assert.That(p.Old, Is.Null, "目标槽为空");
        Assert.That(p.Rows.Single(r => r.Label == "固定效果").Current, Is.EqualTo(GearPanelCopy.Dash), "空槽一侧缺项仍写「—」");
        Assert.That(p.Rows.All(r => r.Current != GearPanelCopy.EmptySlot), Is.True, "「空」只用于名称行，不替换缺项");
    }

    // ───────────── Q479 两处旧悬停说明不恢复（核对现状） ─────────────

    /// <summary>Q479-A01：整备槽上可用「卸下」不恢复旧悬停说明（受阻原因照常），地面卡不恢复「直接从地面装备到{槽}」。</summary>
    [Test]
    public void Q479_OldHoverTextsStayRemoved()
    {
        var overlay = Web("Components/GearOverlay.razor");
        Assert.That(overlay, Does.Contain("disabled=\"@(why != null)\" title=\"@why\""), "可用时无说明，受阻时悬停写原因");
        Assert.That(overlay, Does.Not.Contain("放回随身（占一张物资卡）"));
        Assert.That(overlay, Does.Not.Contain("直接从地面装备到"));
    }

    // ───────────── Q480 数量无效（核对现状） ─────────────

    /// <summary>Q480-A01：交接数量 0、超过整堆时提示「数量无效」、确认禁用、不给结果，不自动截断。</summary>
    [Test]
    public void Q480_InvalidCount_BlockedNoClamp()
    {
        var s = New();
        E.DebugQuickParty(s, 2);
        var mate = s.Party.First(id => id != P0);
        var cloth = E.CreateItems(s, "cloth", 3, ItemInstance.Carried)[0];
        foreach (var n in new[] { 0, 4, -1 })
        {
            var p = E.GivePreview(s, P0, mate, cloth.InstanceId, n);
            Assert.That(p.Blocked, Is.EqualTo("数量无效"), n.ToString());
            Assert.That(p.HasOutcome, Is.False, "不给结果、不截断");
            Assert.That(p.Count, Is.EqualTo(n), "不改所填数量");
        }
        var give = Web("Components/GearGive.razor");
        Assert.That(give, Does.Contain("else G.GearGiveCount(0);"), "非数字按无效数量处理");
        Assert.That(give, Does.Contain("disabled=\"@(p.Blocked != null)\""), "确认禁用");
        Assert.That(Body(Web("Services/GameSession.Gear.cs"), "public void GearGiveCount(int n)", 200), Does.Contain("GivePending.Count = n;").And.Not.Contain("Math.Min"));
    }

    // ───────────── Q481 「人物」页签清临时详情 ─────────────

    /// <summary>Q481-A01：临时整备详情显示时点「人物」直接进人物页、清该临时详情与悬停覆盖；不清比较／交接草稿、不改真正选中、不切队员。</summary>
    [Test]
    public void Q481_CharacterTabEndsGearPeek()
    {
        var s = Web("Services/GameSession.cs");
        var tab = Body(s, "public void SetDetailTab(DetailTab tab)", 700);
        Assert.That(tab, Does.Contain("PeekClear();"));
        Assert.That(tab, Does.Contain("if (tab == DetailTab.Character) EndGearPeekForCharacterTab();"));
        var g = Web("Services/GameSession.Gear.cs");
        Assert.That(g, Does.Contain("private void EndGearPeekForCharacterTab() => GearPeekId = null;"), "只清临时详情，不碰来源、比较槽、旧物去向与交接草稿");
        var d = Web("Components/DetailPanel.razor");
        Assert.That(d, Does.Contain("var gearPeek = battlePanel || G.BattleFullscreen ? null : G.GearPeekItem;"), "临时详情为空后按原页签显示人物页");
        Assert.That(d, Does.Contain("@onclick=\"() => G.SetDetailTab(DetailTab.Character)\""));
    }

    // ───────────── Q482 品质统一可见 ─────────────

    /// <summary>Q482-A01：品质标签「{品质}色」只给装备；非装备为空串（不伪造品质）。</summary>
    [Test]
    public void Q482_QualityTag_EquipmentOnly()
    {
        var s = New();
        var knife = E.CreateItems(s, "stone_knife", 1, ItemInstance.Carried).Single();
        knife.Quality = "green";
        var cloth = E.CreateItems(s, "cloth", 1, ItemInstance.Carried).Single();
        Assert.That(GearPanelCopy.QualityTag(E.QualityName(knife)), Is.EqualTo("绿色"));
        Assert.That(GearPanelCopy.QualityTag(E.QualityName(cloth)), Is.EqualTo(""), "非装备不写品质");
        knife.Quality = null;
        Assert.That(GearPanelCopy.QualityTag(E.QualityName(knife)), Is.EqualTo("白色"), "装备品质为空按白");
    }

    /// <summary>Q482-A01：本人随身装备卡、当前地面装备卡、同类武器整理窗都在名称后显示同一品质标签；数量／耐久／重量沿各自原副标；品质不随长名截去。</summary>
    [Test]
    public void Q482_QualityVisibleOnSourcesAndDupWindow()
    {
        var overlay = Web("Components/GearOverlay.razor");
        var src = Body(overlay, "private RenderFragment SourceCard(", 4000);
        Assert.That(src.Split("</b>@QualityOf(inst)<small>").Length - 1, Is.EqualTo(2), "随身与地面来源卡都在名称后接品质标签");
        Assert.That(src, Does.Contain("<small>×@inst.Count@(inst.MaxDurability.HasValue ? \" · 耐久\" + GameEngine.DurabilityText(inst) : \"\")</small>"), "随身卡数量与耐久沿原副标");
        Assert.That(src, Does.Contain("<small>· @string.Join(\" · \", StatParts(inst))</small>"), "地面卡耐久与重量沿原副标");
        Assert.That(overlay, Does.Contain("var q = GearPanelCopy.QualityTag(G.E.QualityName(it));"));
        Assert.That(overlay, Does.Contain("<span class=\"gs-q\" data-quality="));
        var dup = Web("Components/DuplicateWeaponOverlay.razor");
        Assert.That(dup, Does.Contain("<span class=\"dup-name\"><b>@e.ItemName(it.ItemId)</b>@if (GearPanelCopy.QualityTag(e.QualityName(it)) is { Length: > 0 } q) {<span class=\"gs-q\""));
        Assert.That(dup, Does.Contain("<small>@EquipSlots.Name(slot) · @Ui.ItemLine(e, s, it) · @Ui.Kg(e.ItemGrams(s, it))公斤</small>"), "槽、耐久与重量沿原第二行");
        var css = Web("wwwroot/css/app.css");
        Assert.That(css, Does.Contain(".gs-q, .dup-name .gs-q { flex: 0 0 auto; white-space: nowrap;"), "品质标签不收缩、不被省略");
        Assert.That(css, Does.Contain(".g3-card .gs-item b { flex: 0 1 auto; min-width: 2.5em; overflow: hidden; text-overflow: ellipsis; }"), "长名只省略名称");
    }

    // ───────────── Q415 外部改动存档槽（核对现状） ─────────────

    /// <summary>
    /// Q415-A01：读档进入本局后，某手动槽被外部替换或破坏，再覆盖它：整页按 Q381／Q408 拒绝写入——拒绝句常驻、接回键隐藏，
    /// 自动、手动、「保存并返回标题」与地牢重置都不再写；浏览器单次读取抛错仍走读取失败分支，不判为外部改动。
    /// </summary>
    [Test]
    public void Q415_A01_ExternalSlotChange_RefusesWholePage()
    {
        var st = new B40aSaveSlotsTests.MemStorage();
        var rev = 0;
        var a = new SaveCoordinator(st, E, "A", () => "r" + (++rev));
        const long T0 = 1_700_000_000_000;
        var s0 = New();
        a.StartNewGame(s0, T0);
        Assert.That(a.SaveManual(s0, null, T0 + 1000).Ok, Is.True);
        var key = a.ManualSlots().Single().Key;

        var c = new SaveCoordinator(st, E, "B", () => "r" + (++rev));
        var s = SaveCatalog.Load(st, key, Cfg).State!;
        c.StartLoaded(s, T0 + 10_000);
        Assert.That(c.Permission, Is.EqualTo(SavePermission.Holding));

        st.D[key] = "garbage";   // 外部（开发工具）把手动槽改坏
        var r = c.SaveManual(s, key, T0 + 20_000);
        Assert.That(r.Status, Is.EqualTo(SaveWriteStatus.Blocked));
        Assert.That(c.Permission, Is.EqualTo(SavePermission.Refused));
        Assert.That(c.PermissionNotice, Is.EqualTo("另一个页面已保存了更新的进度。请返回标题重新读取。"));
        Assert.That(c.CanReclaim, Is.False, "隐藏「在此页面继续保存」");
        Assert.That(st.D[key], Is.EqualTo("garbage"), "不覆盖");
        var auto5 = st.D[SaveSlots.Auto5Key];
        c.Tick(c.SessionId, T0 + 400_000);
        c.Pump(s, T0 + 400_000);
        Assert.That(st.D[SaveSlots.Auto5Key], Is.EqualTo(auto5), "自动存档停写");
        Assert.That(c.SaveAndExit(s, T0 + 401_000).Status, Is.EqualTo(SaveWriteStatus.Blocked), "保存并返回标题停写");
        Assert.That(c.PersistReset(s, T0 + 402_000), Is.False, "确认重置地牢停写");

        // 读取抛错：仍按读取失败分支，不判为外部改动
        var st2 = new B40aSaveSlotsTests.MemStorage();
        var d = new SaveCoordinator(st2, E, "D", () => "r" + (++rev));
        var s2 = New();
        d.StartNewGame(s2, T0);
        Assert.That(d.SaveManual(s2, null, T0 + 1000).Ok, Is.True);
        var key2 = d.ManualSlots().Single().Key;
        st2.ReadFailOn = k => k == key2;
        Assert.That(d.SaveManual(s2, key2, T0 + 2000).Status, Is.EqualTo(SaveWriteStatus.Failed));
        Assert.That(d.Permission, Is.EqualTo(SavePermission.Holding), "读失败不整页停写");
    }
}
