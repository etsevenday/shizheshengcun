using System.Text.RegularExpressions;
using Survival.Core.Engine;

namespace Survival.Core.Tests;

/// <summary>
/// B43 快捷键常驻显示并补齐顶栏绑定（需求 R170；S14 2026-11-12「快捷键在对应按钮常驻显示」，验收 KEYHINT-A01—A08；
/// 重墨 V2 逐字稿同日「对应按钮常驻快捷键」）。键位与键帽文字在内核 <see cref="ShellKeys"/>；网页壳接线（按钮面、按键路由）按源码核对。
/// </summary>
public class B43KeyHintTests : TestBase
{
    private static string Web(string relative)
    {
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "doc", "策划案"))) dir = dir.Parent;
        Assert.That(dir, Is.Not.Null, "找不到仓库根目录");
        return File.ReadAllText(Path.Combine(dir!.FullName, "web", "Survival.Web", relative));
    }

    // ───────────── 顶栏 12 键（KEYHINT-A01、A02） ─────────────

    /// <summary>A01：顶栏 12 个按钮的常驻原文逐字照 S14 表「常驻按钮原文」列，功能原顺序。</summary>
    [Test]
    public void A01_TopLabels_Verbatim()
    {
        var expect = new[] { "区域地图 M", "手工制作 C", "休息 R", "行动日志 L", "编队 P", "整备 G", "成长 K", "生存目标 J", "生存提示 T", "对话记录 D", "系统说明 H", "菜单 ESC" };
        Assert.That(ShellKeys.TopCaps.Count, Is.EqualTo(ShellText.TopKeys.Count));
        for (var i = 0; i < expect.Length; i++) Assert.That(ShellKeys.TopLabel(i), Is.EqualTo(expect[i]), $"第{i + 1}个");
        Assert.That(ShellKeys.Esc, Is.EqualTo("ESC"), "ESC 显示为「ESC」");
    }

    /// <summary>A02：字母不区分大小写、每个键只对应一个顶栏功能；F 不直达反馈；菜单走 Escape 层级、不是字母。</summary>
    [Test]
    public void A02_TopIndex_CaseInsensitiveAndUnique()
    {
        var letters = "MCRLPGKJTDH";
        for (var i = 0; i < letters.Length; i++)
        {
            Assert.That(ShellKeys.TopIndexOf(letters[i].ToString()), Is.EqualTo(i), letters[i].ToString());
            Assert.That(ShellKeys.TopIndexOf(char.ToLowerInvariant(letters[i]).ToString()), Is.EqualTo(i), "小写同效");
        }
        Assert.That(ShellKeys.TopCaps.Distinct().Count(), Is.EqualTo(ShellKeys.TopCaps.Count), "每个键只绑定一个顶栏功能");
        foreach (var k in new[] { "F", "f", "Q", "E", "1", "0", "ESC", "Escape", "", null, "Enter" })
            Assert.That(ShellKeys.TopIndexOf(k), Is.EqualTo(-1), k ?? "null");
    }

    /// <summary>A08：键表（游戏菜单、标题页玩法说明）与按钮同源，三处一致；不再有旧的「M 地图 · C 手工制作」四键表。</summary>
    [Test]
    public void A08_KeyTable_SameSourceAsButtons()
    {
        Assert.That(ShellKeys.TopTable(), Is.EqualTo("区域地图 M · 手工制作 C · 休息 R · 行动日志 L · 编队 P · 整备 G · 成长 K · 生存目标 J · 生存提示 T · 对话记录 D · 系统说明 H · 菜单 ESC"));
        var menu = Web("Components/MenuOverlay.razor");
        var start = Web("Components/StartScreen.razor");
        Assert.That(menu, Does.Contain("快捷键：@ShellKeys.TopTable()"));
        Assert.That(start, Does.Contain("快捷键：@ShellKeys.TopTable()"));
        Assert.That(menu + start, Does.Not.Contain("M 地图"));
    }

    /// <summary>A01、A08：顶栏 12 个按钮的面都是「功能名＋键帽」（TopFace），点击与按键共用 ActivateTop；悬停提示带同一键位；键帽三档都不隐藏。</summary>
    [Test]
    public void A01_TopBar_Wiring()
    {
        var screen = Web("Components/GameScreen.razor");
        var nav = screen.Substring(screen.IndexOf("<nav class=\"nav\""), screen.IndexOf("</nav>") - screen.IndexOf("<nav class=\"nav\""));
        for (var i = 0; i < 12; i++) Assert.That(nav, Does.Contain($">@TopFace({i})</button>"), $"第{i + 1}个按钮面");
        for (var i = 0; i < 11; i++) Assert.That(nav, Does.Contain($"@onclick=\"() => ActivateTop({i})\""), $"第{i + 1}个按钮点击");
        Assert.That(Regex.Matches(nav, "aria-keyshortcuts=").Count, Is.EqualTo(12));
        Assert.That(screen, Does.Contain("<span class=\"nk-name\">@ShellText.TopKeys[i]</span> <kbd class=\"kc\">@ShellKeys.TopCaps[i]</kbd>"));
        Assert.That(screen, Does.Contain("$\"{what}（{ShellKeys.TopCaps[i]}）\""), "悬停提示与键帽同源");
        var onKey = screen.Substring(screen.IndexOf("public void OnKey(string key)"));
        onKey = onKey.Substring(0, onKey.IndexOf("/// <summary>"));
        Assert.That(onKey, Does.Contain("var top = ShellKeys.TopIndexOf(key);").And.Contain("if (top >= 0) ActivateTop(top);"));
        Assert.That(onKey.IndexOf("if (G.DialogueLocked || G.MenuOpen || G.BattleMenuOpen) return;"), Is.LessThan(onKey.IndexOf("ActivateTop(top)")), "对白、菜单时不越权");
        Assert.That(onKey.IndexOf("if (G.InBattleUi) return;"), Is.LessThan(onKey.IndexOf("ActivateTop(top)")), "战斗与敌踪时不越权");
        Assert.That(onKey.IndexOf("if (G.S.Progress.Dead) return;"), Is.LessThan(onKey.IndexOf("ActivateTop(top)")), "终局不越权");
        var act = screen.Substring(screen.IndexOf("private void ActivateTop(int i)"));
        Assert.That(act, Does.Contain("case 4: ToggleFormation(); break;"), "编队沿原编队流程");
        Assert.That(Web("wwwroot/css/app.css"), Does.Not.Contain(".nav kbd { display: none; }"), "1280 档不再藏键位");
        Assert.That(Web("wwwroot/css/shell.css"), Does.Contain(".wasteland .top .nav kbd { display: inline-block; }"));
    }

    // ───────────── ESC 层级（KEYHINT-A03、A06） ─────────────

    /// <summary>
    /// A03、A06：ESC 键帽只显示在承接本次 Esc 的那个控件上——判定与 GameScreen 实际执行同源（GameSession.EscOwner／NormalEscOwner／EncounterEscOwner），
    /// 面板关闭、整备返回、说明返回、局部盘取消、数量框取消、人物二级页返回、鸟瞰返回、战斗抽屉关闭各按自己那一层；菜单关闭键与「菜单」键常驻 ESC。
    /// </summary>
    [Test]
    public void A03_A06_EscCapFollowsEscOwner()
    {
        Assert.That(Web("Components/EscCap.razor"), Does.Contain("@if (On) {<kbd class=\"kc\">@ShellKeys.Esc</kbd>}"));
        var screen = Web("Components/GameScreen.razor");
        Assert.That(screen, Does.Contain("switch (G.NormalEscOwner())").And.Contain("switch (G.EncounterEscOwner())"), "Esc 执行与键帽同一判定");
        Assert.That(screen, Does.Not.Contain("private bool NormalLayer()"));
        var panel = "<EscCap On=\"@(G.EscOwner == EscOwner.Panel)\" />";
        foreach (var f in new[] { "MapOverlay", "CraftOverlay", "RestOverlay", "LogOverlay", "GoalsOverlay", "TipsOverlay", "DialogueLogOverlay", "GearOverlay", "GrowthOverlay",
                                  "FormationOverlay", "SafehouseOverlay", "DryingOverlay", "JourneyOverlay", "CityServiceOverlay", "ExpeditionAbort", "ExpeditionSeal", "DungeonReset" })
        {
            var src = Web($"Components/{f}.razor");
            Assert.That(src, Does.Contain(panel), f);
            Assert.That(src, Does.Not.Contain("<kbd>Esc</kbd>"), f + "：旧的常驻 Esc 换成按层显示的 ESC");
        }
        Assert.That(Web("Components/HelpOverlay.razor"), Does.Contain("关闭<EscCap On=\"@G.EscOnHelpClose\" />").And.Contain("‹ 返回<EscCap On=\"@G.EscOnHelpBack\" />"));
        Assert.That(Web("Components/GearCompare.razor") + Web("Components/GearGive.razor"), Does.Contain("@GearPanelCopy.Back<EscCap On=\"@(G.EscOwner == EscOwner.GearSub)\" />"));
        Assert.That(Web("Components/AttrDetail.razor"), Does.Contain("‹ 人物<EscCap On=\"@(G.EscOwner == EscOwner.CharPage)\" />"));
        Assert.That(Web("Components/CityOverview.razor"), Does.Contain("返回当前地点<EscCap On=\"@(G.EscOwner == EscOwner.CityMap)\" />"));
        Assert.That(Web("Components/DropQuantity.razor"), Does.Contain("取消<EscCap On=\"@(G.EscOwner == EscOwner.DropQty)\" />"));
        var disk = "<EscCap On=\"@G.EscOnDisk\" />";
        Assert.That(Regex.Matches(Web("Components/ActionPops.razor"), Regex.Escape(disk)).Count, Is.GreaterThanOrEqualTo(5), "就近盘的取消／关闭");
        Assert.That(Web("Components/IgniteMenu.razor") + Web("Components/FeedPop.razor"), Does.Contain("取消" + disk).And.Contain("@FeedPanelCopy.Cancel" + disk));
        var battle = Web("Components/BattleScreen.razor");
        Assert.That(battle, Does.Contain("关闭<EscCap On=\"@(G.EscOwner == EscOwner.BattleDrawer)\" />"));
        Assert.That(battle, Does.Contain(">@ShellText.Menu <kbd class=\"kc\">@ShellKeys.Esc</kbd></button>"), "战斗区「菜单 ESC」");
        var keys = Web("Services/GameSession.Keys.cs");
        Assert.That(keys, Does.Contain("if (DialogueLocked) return EscOwner.OpenMenu;"), "对白锁：Esc 不收背后子层，也就不在那些控件上标 ESC");
        // 中央「退出行动」不标 ESC（S14：不能把中央「退出行动」标成ESC）
        Assert.That(Web("Components/CentralProgress.razor") + Web("Components/PauseLayer.razor"), Does.Not.Contain("ShellKeys.Esc").And.Not.Contain("EscCap"));
    }

    // ───────────── 局部唯一确认 Enter、对白继续、数量与拖拽（KEYHINT-A03、A05、A07） ─────────────

    /// <summary>
    /// A03：局部盘／工作区唯一主确认带 data-enter 与可用时的「Enter」键帽（EnterCap 与按钮 disabled 同一条件），所在区带 data-enter-scope；
    /// 晾制收取「随身／地面」、招募两选项等并列分支不带 Enter；回车只在该区内恰好一个可用确认时 click 同一按钮。
    /// </summary>
    [Test]
    public void A03_UniqueConfirmEnter()
    {
        Assert.That(Web("Components/EnterCap.razor"), Does.Contain("@if (On) {<kbd class=\"kc\">@ShellKeys.Enter</kbd>}"));
        Assert.That(ShellKeys.Enter, Is.EqualTo("Enter"));
        void Has(string file, string button, int count = 1)
        {
            var src = Web($"Components/{file}.razor");
            Assert.That(src, Does.Contain("data-enter-scope"), file + " 区");
            Assert.That(Regex.Matches(src, "data-enter[ >]").Count, Is.EqualTo(count), file + " 确认按钮数");
            Assert.That(src, Does.Contain(button), file);
        }
        Has("DropQuantity", "<EnterCap On=\"@(!invalid)\" />");
        Has("CardActions", "<EnterCap On=\"@(why == null)\" />", 3);
        Has("GearCompare", "@GearPanelCopy.ConfirmEquip<EnterCap On=\"@(p.Blocked == null)\" />");
        Has("GearGive", "@GearPanelCopy.ConfirmGive<EnterCap On=\"@(p.Blocked == null)\" />");
        Has("OverflowOverlay", "<EnterCap On=\"@(!(over || _busy))\" />");
        Has("DuplicateWeaponOverlay", "@GearPanelCopy.DupConfirm<EnterCap");
        Has("ExpeditionAbort", "确认终止并撤出<EnterCap");
        Has("DungeonReset", "确认重置<EnterCap");
        Has("FormationOverlay", "保存编队<EnterCap On=\"@(saveWhy == null)\" />");
        Has("RestOverlay", "<EnterCap On=\"@(p.Ok && !busy)\" />");
        Has("JourneyOverlay", "准备好了<EnterCap");
        Has("CraftOverlay", "<EnterCap On=\"@(p.Ok && !startBusy)\" />", 2);
        Assert.That(Web("Components/AnchorPop.razor"), Does.Contain("data-enter-scope tabindex=\"-1\""), "居中局部盘是确认区");
        var pops = Web("Components/ActionPops.razor");
        Assert.That(Regex.Matches(pops, "data-enter ").Count, Is.EqualTo(4), "移动、接近、投炭、采集各一个唯一确认");
        var harvest = pops.Substring(pops.IndexOf("@CardStates.HarvestToCarried") - 200, 400);
        Assert.That(harvest, Does.Not.Contain("data-enter"), "收取随身／地面是并列分支，不贴默认 Enter");
        Assert.That(pops, Does.Contain("终止本次远征…</button>").And.Not.Contain("data-enter disabled=\"@busy\" @onclick=\"G.OpenAbort\""), "打开终止确认窗不是提交");
        Assert.That(Web("Components/FeedPop.razor") + Web("Components/IgniteMenu.razor"), Does.Contain("@v.ConfirmText<EnterCap On=\"@(why == null)\" />").And.Contain("@CardStates.IgniteButton(minutes)<EnterCap"));
        Assert.That(Web("Components/DropQuantity.razor"), Does.Not.Contain("if (ev.Key == \"Enter\") Confirm();"), "回车只走一条路由，已聚焦的取消键不再同时确认");
        var js = Web("wwwroot/js/app.js");
        Assert.That(js, Does.Contain("return own.length === 1 && !own[0].disabled ? own[0] : null;"), "恰好一个可用确认");
        Assert.That(js, Does.Contain("if (k === 'Enter' && !mod && !control(document.activeElement))"), "已聚焦按钮上的 Enter 走原生语义");
    }

    /// <summary>A03、A05：对白唯一推进键「继续 Enter／结束对话 Enter」带 data-dlg-next；招募两选项不带；Esc 不结束对白。</summary>
    [Test]
    public void A03_DialogueContinueEnter()
    {
        var dlg = Web("Components/DialogueLayer.razor");
        Assert.That(Regex.Matches(dlg, "data-dlg-next").Count, Is.EqualTo(2), "注释一次、按钮一次");
        Assert.That(dlg, Does.Contain("@(more ? \"继续\" : \"结束对话\") <kbd class=\"kc\">@ShellKeys.Enter</kbd></button>"));
        var recruit = dlg.Substring(dlg.IndexOf("@if (v.RecruitQuestId is string rq)"), dlg.IndexOf("else", dlg.IndexOf("@if (v.RecruitQuestId is string rq)")) - dlg.IndexOf("@if (v.RecruitQuestId is string rq)"));
        Assert.That(recruit, Does.Not.Contain("data-dlg-next").And.Not.Contain("ShellKeys.Enter"), "招募分支不由 Enter 代选");
        var js = Web("wwwroot/js/app.js");
        var layer = js.Substring(js.IndexOf("if (document.querySelector('.dlg-layer')) {"));
        layer = layer.Substring(0, layer.IndexOf("\n      }\n") > 0 ? layer.IndexOf("\n      }\n") : 900);
        Assert.That(layer, Does.Contain("document.querySelector('.dlg-layer [data-dlg-next]')").And.Contain("if (k === 'Escape') { send('Escape'); return; }"));
    }

    /// <summary>A07：转移条数量框旁「数量 0–9」，拖拽合法多份落点「选数量 Shift」并保留原解释；字样与内核同源。</summary>
    [Test]
    public void A07_QuantityAndDragShift()
    {
        Assert.That(ShellKeys.Qty + " " + ShellKeys.Digits, Is.EqualTo("数量 0–9"));
        Assert.That(ShellKeys.DragQty + " " + ShellKeys.Shift, Is.EqualTo("选数量 Shift"));
        var ca = Web("Components/CardActions.razor");
        Assert.That(Regex.Matches(ca, Regex.Escape("<span class=\"tbar-qlabel\">@ShellKeys.Qty <kbd class=\"kc\">@ShellKeys.Digits</kbd></span>")).Count, Is.EqualTo(2), "整堆与倒水两处数量框");
        var drag = Web("wwwroot/js/drag.js");
        Assert.That(drag, Does.Contain($"var DRAG_QTY = '{ShellKeys.DragQty}', SHIFT = '{ShellKeys.Shift}';"));
        Assert.That(drag, Does.Contain("qkey.hidden = !(h && h.r && h.r.ok && h.r.qty);"), "只在合法多份落点显示");
        Assert.That(drag, Does.Contain("' · 按住 Shift 松手可选数量'"), "原解释保留");
    }

    // ───────────── 地图与战斗（KEYHINT-A04） ─────────────

    /// <summary>A04：地图「缩小 -」「放大 +」「回到当前位置 0」键值写在按钮上；键盘只走 app.js 点同一按钮（视口不再另挂 Blazor 键处理，避免一次按键缩放两级）。</summary>
    [Test]
    public void A04_MapKeysOnButtons()
    {
        Assert.That($"{ShellKeys.ZoomOut} {ShellKeys.ZoomOutCap}|{ShellKeys.ZoomIn} {ShellKeys.ZoomInCap}|{ShellKeys.BackToHere} {ShellKeys.BackToHereCap}", Is.EqualTo("缩小 -|放大 +|回到当前位置 0"));
        var map = Web("Components/MapOverlay.razor");
        Assert.That(map, Does.Contain("data-map-cmd=\"out\"").And.Contain("@ShellKeys.ZoomOut <kbd class=\"kc\">@ShellKeys.ZoomOutCap</kbd>"));
        Assert.That(map, Does.Contain("data-map-cmd=\"in\"").And.Contain("@ShellKeys.ZoomIn <kbd class=\"kc\">@ShellKeys.ZoomInCap</kbd>"));
        Assert.That(map, Does.Contain("data-map-cmd=\"here\"").And.Contain("@ShellKeys.BackToHere <kbd class=\"kc\">@ShellKeys.BackToHereCap</kbd>"));
        Assert.That(map, Does.Not.Contain("@onkeydown=\"Key\""), "视口不再自己处理 +/-/0");
        var js = Web("wwwroot/js/app.js");
        Assert.That(js, Does.Contain("mapPanel.querySelector('[data-map-cmd=\"' + which + '\"]')"));
        Assert.That(js, Does.Contain("if (mb && !mb.disabled && !(ev.repeat && which === 'here')) mb.click();"), "按钮禁用时键也无效；放缩按住连续，定位不连发");
        Assert.That(Web("wwwroot/css/fourrows.css"), Does.Not.Contain(".map-tools kbd { display: none; }"), "窄屏不藏地图键帽");
    }

    /// <summary>
    /// A04：武器页签「武器Ⅰ Q／武器Ⅱ E」；技能卡位键帽 1—5 按卡位序号（不是等级），主动、被动、灰卡都有；
    /// 目标预测区「上一目标 ←」「下一目标 →」走 BattleCycleTarget；底部确认「{原名} Enter」与按钮同一可用条件。
    /// </summary>
    [Test]
    public void A04_BattleKeys()
    {
        Assert.That(ShellKeys.WeaponPage1 + " " + ShellKeys.WeaponPage1Cap + "／" + ShellKeys.WeaponPage2 + " " + ShellKeys.WeaponPage2Cap, Is.EqualTo("武器Ⅰ Q／武器Ⅱ E"));
        Assert.That(ShellKeys.SkillSlotLevels.Select(ShellKeys.SkillSlotCap), Is.EqualTo(new[] { "1", "2", "3", "4", "5" }));
        Assert.That(ShellKeys.SkillSlotCap(2), Is.EqualTo(""), "不是卡位等级的没有键帽");
        var bs = Web("Components/BattleScreen.razor");
        Assert.That(bs, Does.Contain("@(page == 1 ? ShellKeys.WeaponPage1 : ShellKeys.WeaponPage2) <kbd class=\"kc\">@pageCap</kbd> @(pi == null ? \"拳\" : pw.Name)"));
        Assert.That(Regex.Matches(bs, "@SlotCap\\(").Count, Is.EqualTo(3), "已解锁主动、被动、灰卡三处");
        Assert.That(bs, Does.Contain("var cap = ShellKeys.SkillSlotCap(level);"));
        Assert.That(Web("Services/GameSession.cs"), Does.Contain("var lv = ShellKeys.SkillSlotLevels[slot - 1];"), "按键与键帽同一张表");
        Assert.That(bs, Does.Contain("<EnterCap On=\"@(p.Ok && !locked)\" /></button>").And.Contain("id=\"bt-confirm\" disabled=\"@(!p.Ok || locked)\""));
        var bp = Web("Components/BattlePanel.razor");
        Assert.That(Regex.Matches(bp, "TargetCycle\\(aimSet.Count\\)").Count, Is.EqualTo(2), "瞄准中两种预测区都有");
        Assert.That(bp, Does.Contain("@onclick=\"() => G.BattleCycleTarget(-1)\">@ShellKeys.PrevTarget <kbd class=\"kc\">@ShellKeys.PrevTargetCap</kbd></button>"));
        Assert.That(bp, Does.Contain("@onclick=\"() => G.BattleCycleTarget(1)\">@ShellKeys.NextTarget <kbd class=\"kc\">@ShellKeys.NextTargetCap</kbd></button>"));
        Assert.That(bp, Does.Contain("if (!G.BattleFullscreen) return;"), "方向键只在全屏战斗路由，按钮也只在那里给");
    }

    // ───────────── 按键路由（KEYHINT-A02、A05） ─────────────

    /// <summary>A02、A05：顶栏字母表含 11 个字母、不含 F；输入法组合、组合键、输入控件不触发；按住重复不重复开关；Enter/Space 重复在捕获阶段挡住。</summary>
    [Test]
    public void A05_KeyRouting()
    {
        var js = Web("wwwroot/js/app.js");
        Assert.That(js, Does.Contain("TOP_LETTERS: ['M', 'C', 'R', 'L', 'P', 'G', 'K', 'J', 'T', 'D', 'H'],"));
        Assert.That(js, Does.Not.Contain("'f', 'F'"));
        Assert.That(js, Does.Contain("if (ev.isComposing || ev.keyCode === 229) return;"), "输入法组合");
        Assert.That(js, Does.Contain("var mod = ev.ctrlKey || ev.altKey || ev.metaKey;"));
        Assert.That(js, Does.Contain("if (!ev.repeat) dotnet.invokeMethodAsync('OnKey', up);"), "按住不重复开关顶栏");
        Assert.That(js, Does.Contain("window.addEventListener('keydown', window.__survivalKeyGuard, true);"), "重复 Enter／Space 捕获阶段挡住");
        Assert.That(js, Does.Contain("if (tg === 'TEXTAREA' || tg === 'SELECT' || el.isContentEditable) return true;"), "下拉与可编辑文字区算输入控件");
        var hook = js.Substring(js.IndexOf("window.__survivalKeys = function (ev)"));
        Assert.That(hook.IndexOf("if (editable(tgt))"), Is.LessThan(hook.IndexOf("TOP.indexOf(up)")), "输入控件先于字母导航");
        Assert.That(hook.IndexOf("if (modal) {"), Is.LessThan(hook.IndexOf("TOP.indexOf(up)")), "模态层先处理自己的键");
        Assert.That(hook.IndexOf("document.querySelector('.menu-modal')"), Is.LessThan(hook.IndexOf("document.querySelector('.dlg-layer')")), "菜单先于对白");
        Assert.That(hook.IndexOf("document.querySelector('.dlg-layer')"), Is.LessThan(hook.IndexOf("document.querySelector('.bfs')")), "对白输入锁先于战斗");
    }
}
