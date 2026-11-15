using System.Reflection;
using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 BM8「次要问题合并（A 类：策划答复）」（R242、R245、R246、R250、R251；答复 Q656—Q665、Q672、Q698—Q700）。
/// 规则出处：S14 文首「2026-11-15 Q656／Q657／Q659—Q662／Q664／Q665」「Q663／Q679—Q681／Q688—Q689」「Q658」节；R05 文首 Q700、Q663、Q656／Q657／Q665、Q672 节；
/// R03 文首 Q700、Q660—Q662 节；R09 文首 Q698—Q699 节；正式句见《重墨 V2 开工裁定与界面逐字稿》对应节。网页壳接线以源码检查（同 BM7bTests）。
/// </summary>
public class BM8Tests : B41cTestBase
{
    private EnemyCardView Card(GameState s) => E.EnemyCardsHere(s).Single();

    // ───────────── Q700 行动标签不带死亡喷溅 ─────────────

    /// <summary>Q700-A01：有喷溅配置的敌人，通用与咬击标签分支都不带“死亡时喷溅”后缀，标点完整、原动作文字不误删；AP 不足战报用同一标签。</summary>
    [Test]
    public void Q700_A01_IntentLabelNoSplashSuffix()
    {
        var s = Fight(70001, "z_bloater");
        var b = s.Battle!;
        var e = b.Enemies.Single();
        Assert.That(E.Enemy(e.EnemyId).DeathSplash, Is.GreaterThan(0), "布场：膨胀感染者有喷溅配置");
        foreach (var intent in new[] { "attack", "bite", "charge" })
        {
            e.Intent = intent;
            var label = E.IntentLabel(e, b);
            Assert.That(label, Does.Not.Contain("喷溅"), intent);
            Assert.That(label, Does.Not.Contain("，，").And.Not.Contain("（）").And.Not.EndWith("，"), intent);
        }
        e.Intent = "bite";
        Assert.That(E.IntentLabel(e, b), Does.StartWith("咬击 ").And.EndWith("（可能感染）"), "咬击分支保留原动作与感染说明");
        var src = Src("src", "Core", "Engine", "GameEngine.EnemyAi.cs");
        Assert.That(src, Does.Not.Contain("死亡时喷溅1、2号位各"), "标签生成处所有分支都不再拼接");
        Assert.That(src, Does.Contain("不够{IntentLabel(e, b)}，结束行动。"), "AP 不足战报模板不变，标签同源");
    }

    // ───────────── Q698 濒死不再出健康 5 高危提醒／暂停 ─────────────

    private GameState NearDying(uint seed)
    {
        var s = New(seed);
        E.DebugTeleport(s, "field");
        s.GameMinute = At(2, 9, 0);
        s.SpawnNextCheck = int.MaxValue;
        s.PatrolNextCheck = int.MaxValue;
        s.WorldEnemies.Clear();
        E.DebugDying(s, "pause1");
        return s;
    }

    /// <summary>Q698：本段进入濒死的人不出“健康降至5，处于高危状态。”、不因健康阈值另停；锁存照记，脱离濒死后按原规则重新判定。</summary>
    [Test]
    public void Q698_DyingNoHealthAlertNoAlertPause()
    {
        var s = NearDying(69801);
        s.Player.AlertLatched[StatId.Health] = false;
        Start(s, ActionRequest.Wait(60));
        var mark = s.Log.Count;
        Assert.That(E.Advance(s).Status, Is.EqualTo(SegmentStatus.Paused));
        var ev = s.Log.Skip(mark).ToList();
        Assert.That(ev.Select(l => l.Text), Does.Not.Contain(E.AlertText(StatId.Health, false)), "濒死不写健康高危提醒");
        Assert.That(s.Main!.PauseReason, Is.EqualTo(PauseReason.Dying), "暂停原因只有濒死");
        Assert.That(ev.Count(l => l.Kind == "ActionPaused"), Is.EqualTo(1));
        Assert.That(s.Player.AlertLatched[StatId.Health], Is.True, "锁存照记，不在濒死期间反复判定");
        // 再推进：濒死期间健康仍 0，不补出提醒
        E.TestDeathRolls = new Queue<double>(Enumerable.Repeat(0.0, 10));   // 抵抗成功
        E.Resume(s);
        var mark2 = s.Log.Count;
        E.Advance(s);
        Assert.That(s.Log.Skip(mark2).Select(l => l.Text), Does.Not.Contain(E.AlertText(StatId.Health, false)));
    }

    /// <summary>Q698：未濒死者健康跌破 5 仍按原规则写提醒并暂停（不误删）。</summary>
    [Test]
    public void Q698_NotDyingStillAlerts()
    {
        var s = New(69802);
        E.DebugTeleport(s, "field");
        s.GameMinute = At(2, 9, 0);
        s.SpawnNextCheck = int.MaxValue;
        s.PatrolNextCheck = int.MaxValue;
        s.WorldEnemies.Clear();
        s.Player.Stats.Health = 5.0001;
        s.Player.Stats.Satiety = 0; s.Player.ZeroLatched[StatId.Satiety] = true; s.Player.AlertLatched[StatId.Satiety] = true;
        s.Player.Stats.Hydration = 0; s.Player.ZeroLatched[StatId.Hydration] = true; s.Player.AlertLatched[StatId.Hydration] = true;
        Start(s, ActionRequest.Wait(60));
        var mark = s.Log.Count;
        E.Advance(s);
        Assert.That(s.Player.Stats.Health, Is.GreaterThan(0).And.LessThan(5));
        Assert.That(s.Log.Skip(mark).Select(l => l.Text), Does.Contain(E.AlertText(StatId.Health, false)));
    }

    /// <summary>Q698：储备红线里的健康高危同义提示对濒死者静音（源码：Line 的 mute 参数取 IsDying）。</summary>
    [Test]
    public void Q698_ReserveHealthLineMutedWhenDying()
    {
        var src = Src("src", "Core", "Engine", "GameEngine.Party.cs");
        Assert.That(src, Does.Contain("bool mute = false"));
        Assert.That(src, Does.Contain("IsDying(s, actor)"));
    }

    // ───────────── Q699 世界段生命日志记段末分钟 ─────────────

    /// <summary>Q699：世界段内的生命状态句（进入濒死等）记该段末分钟 E，与段内其他结算日志同一分钟；段外调用不受影响。</summary>
    [Test]
    public void Q699_WorldSegmentLifeLogAtSegmentEnd()
    {
        var s = NearDying(69901);
        Start(s, ActionRequest.Wait(60));
        var t0 = s.GameMinute;
        var mark = s.Log.Count;
        E.Advance(s);
        var dying = s.Log.Skip(mark).Single(l => l.Text == GameEngine.EnterDyingText("你"));
        Assert.That(dying.Minute, Is.EqualTo(t0 + 5), "段末 E，不是段首 S");
        Assert.That(s.LifeLogMinute, Is.Null, "段结束后复原，不进存档");
        var t = SaveSerializer.Deserialize(SaveSerializer.Serialize(s), Cfg);
        Assert.That(t.LifeLogMinute, Is.Null);
    }

    /// <summary>Q699-A01：同一世界段里的抵抗成功句、致死抵抗失败与所接死亡主结果（同一条）都记段末 E；真实时钟只推进一段。</summary>
    [Test]
    public void Q699_A01_ResistAndDeathAtSegmentEnd()
    {
        var s = NearDying(69902);
        Start(s, ActionRequest.Wait(60));
        Assert.That(E.Advance(s).Status, Is.EqualTo(SegmentStatus.Paused));
        E.TestDeathRolls = new Queue<double>(new[] { 0.0 });   // 下一段抵抗成功
        Assert.That(E.Resume(s).Ok, Is.True);
        var t1 = s.GameMinute;
        var mark = s.Log.Count;
        E.Advance(s);
        Assert.That(s.GameMinute, Is.EqualTo(t1 + 5));
        Assert.That(s.Log.Skip(mark).Single(l => l.Text == GameEngine.ResistedDeathText("你")).Minute, Is.EqualTo(t1 + 5), "抵抗成功记段末");
        E.TestDeathRolls = new Queue<double>(new[] { 0.9999 });   // 再一段抵抗失败
        var t2 = s.GameMinute;
        mark = s.Log.Count;
        E.Advance(s);
        Assert.That(s.Progress.Dead, Is.True);
        var death = s.Log.Skip(mark).Single(l => l.Kind == "Death");
        Assert.That(death.Text, Does.StartWith(GameEngine.FailedResistText("你")), "失败前缀与死亡主结果同一条");
        Assert.That(death.Minute, Is.EqualTo(t2 + 5), "整条记段末");
        Assert.That(s.GameMinute, Is.EqualTo(t2 + 5), "不多推进");
    }

    // ───────────── Q663 本场战斗不入场 ─────────────

    private EnemyGroupCard Group(GameState s, string loc, params WorldEnemy[] members) =>
        (EnemyGroupCard)typeof(GameEngine).GetMethod("AddGroupCard", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(E, new object[] { s, loc, members.ToList(), "explore", s.GameMinute })!;

    /// <summary>Q663-A01／A02：组卡成员多于本场容量时，未入场成员只标“本场战斗不入场”；容量放大后恢复原命中分支；不入场与无可用攻击不混。</summary>
    [Test]
    public void Q663_A01_A02_NotEnteringOnlyBeyondCapacity()
    {
        var s = Field(1);
        var a = E.DebugSpawnEnemy(s, "shambler", "field");
        var b = E.DebugSpawnEnemy(s, "shambler", "field");
        Group(s, "field", a, b);
        var card = Card(s);
        var hits = E.PreBattleHits(s, card);
        Assert.That(hits.Count, Is.EqualTo(2));
        Assert.That(hits.Count(h => h.NotEntering), Is.EqualTo(1), "容量 1：一人入场、一人不入场");
        Assert.That(hits.Single(h => !h.NotEntering).Legal, Is.True, "入场者沿原命中分支");
        Assert.That(GameEngine.NotEnteringHitText, Is.EqualTo("本场战斗不入场"));
        var before = (s.GameMinute, s.Rng.State);
        Assert.That((s.GameMinute, s.Rng.State), Is.EqualTo(before), "只读");

        UnlockCapacity(s, 2);
        Assert.That(E.PreBattleHits(s, Card(s)).Any(h => h.NotEntering), Is.False, "容量够了都入场，恢复原分支");

        // 实际入场但当前页不能攻击：仍「无可用攻击」，不是不入场
        Arm(s, "signal_whistle");
        E.CreateItems(s, "signal_whistle", 1, ItemInstance.EquipOwner(GameState.PlayerId, EquipSlots.Weapon2));
        var none = E.PreBattleHits(s, Card(s));
        Assert.That(none.All(h => !h.NotEntering && !h.Legal), Is.True);
    }

    /// <summary>Q663：右栏「战斗详情」与中央处理盘同源，不入场时只写原句、无“你的命中率”标签与百分比。</summary>
    [Test]
    public void Q663_ShellWiring()
    {
        var d = Web("Components", "EnemyDetail.razor");
        Assert.That(d, Does.Contain("@if (hit.NotEntering)"));
        Assert.That(d, Does.Contain("<div class=\"wide\" data-not-entering><b>@GameEngine.NotEnteringHitText</b></div>"));
        Assert.That(d, Does.Contain("else if (hit.Legal)"));
        var p = Web("Components", "ActionPops.razor");
        Assert.That(p, Does.Contain("@if (hit.NotEntering) { <span data-not-entering>@GameEngine.NotEnteringHitText</span> }"));
    }

    // ───────────── Q665 两页联合枚举 ─────────────

    /// <summary>Q665：武器Ⅰ页不能攻击、武器Ⅱ页空槽可徒手拳击时，我方输出不为 0；两页取最大、不相加。</summary>
    [Test]
    public void Q665_BothPagesMaxNotSum()
    {
        var (s, _) = OneShambler();
        Arm(s, "signal_whistle");
        var fistOnly = E.EngageRiskFactorsFor(s, Card(s))!.DA;
        Assert.That(fistOnly, Is.GreaterThan(0), "Ⅱ页空槽按原徒手拳击计入");

        var (u, _) = OneShambler();
        Arm(u, "hatchet");
        E.CreateItems(u, "signal_whistle", 1, ItemInstance.EquipOwner(GameState.PlayerId, EquipSlots.Weapon2));
        var hatchetOnly = E.EngageRiskFactorsFor(u, Card(u))!.DA;

        var (v, _) = OneShambler();
        Arm(v, "hatchet");
        var both = E.EngageRiskFactorsFor(v, Card(v))!.DA;
        Assert.That(both, Is.EqualTo(Math.Max(hatchetOnly, fistOnly)).Within(1e-9), "取最大不相加");
        Assert.That(v.Battle, Is.Null, "只读，不开战、不切页");
    }

    // ───────────── Q656 有敌人已蠢蠢欲动 ─────────────

    /// <summary>Q656：接近余量已到 0 且正被同一次未完探索延后时卡状态写“有敌人已蠢蠢欲动”；不在探索中或余量未到 0 仍“可能主动接近。”。</summary>
    [Test]
    public void Q656_ExploreDeferredStatus()
    {
        var s = Field(3, "shambler");
        var key = GameEngine.SingleCardKey(s.WorldEnemies[0].Id);
        SetLeft(s, key, 0);
        Assert.That(E.CardApproachStatus(s, E.FindEnemyCard(s, key, out _)!), Is.EqualTo(GameEngine.MayApproachText), "未开探索");
        Start(s, ActionRequest.Explore());
        s.Main!.EncounterRoll = 1;
        Assert.That(E.CardApproachStatus(s, E.FindEnemyCard(s, key, out _)!), Is.EqualTo("有敌人已蠢蠢欲动"));
        Assert.That(GameEngine.ExploreStirringText, Is.EqualTo("有敌人已蠢蠢欲动"));

        var u = Field(3, "shambler");
        var k2 = GameEngine.SingleCardKey(u.WorldEnemies[0].Id);
        SetLeft(u, k2, 30);
        Start(u, ActionRequest.Explore());
        Assert.That(E.CardApproachStatus(u, E.FindEnemyCard(u, k2, out _)!), Is.EqualTo(GameEngine.MayApproachText), "余量未到 0 不提前");
    }

    // ───────────── Q672 旧在办潜行目标被安全清理 ─────────────

    /// <summary>Q672-A01：已开工的潜行接近／躲避，目标在读档时被安全岗位清理，原段末给原取消句与真实时长；无战斗、无抽签、无经验回执。</summary>
    [Test]
    public void Q672_A01_StartedStealthTargetPurged_OriginalCancel()
    {
        foreach (var (kind, name) in new[] { (GameEngine.SneakKind, "潜行接近"), (GameEngine.EvadeKind, "躲避") })
        {
            var (s, key) = OneShambler();
            Assert.That(E.StartStealth(s, key, kind).Ok, Is.True, kind);
            // 模拟旧档：队伍与该敌人都在岗位安全地点
            s.CurrentLocation = "h02_square";
            s.WorldEnemies.Single().LocationId = "h02_square";
            var t = SaveSerializer.Deserialize(SaveSerializer.Serialize(s), Cfg);
            Assert.That(t.WorldEnemies, Is.Empty, "读档按 Q636 清卡");
            var xp = Stealth(t);
            var t0 = t.GameMinute;
            Assert.That(E.Advance(t).Status, Is.EqualTo(SegmentStatus.Aborted), kind);
            Assert.That(t.Log.Select(l => l.Text), Does.Contain($"本次{name}已取消。这处敌踪已无法处理。已耗时5分钟。"), kind);
            Assert.That(t.Log.Select(l => l.Text), Does.Not.Contain(GameEngine.SafeZoneNoEngageText), "不把安全区拒绝句塞进取消句");
            Assert.That(t.GameMinute, Is.EqualTo(t0 + 5));
            Assert.That(t.Battle, Is.Null);
            Assert.That(t.EnemyCards.StealthXpClaims, Is.Empty);
            Assert.That(Stealth(t), Is.EqualTo(xp), "不发经验");
        }
    }

    // ───────────── Q658 对白中顶栏菜单 ─────────────

    /// <summary>Q658：战斗对白期间顶栏「菜单 ESC」在白名单内（可点、可 Tab），顶栏只在播放／遭遇提示时 inert。</summary>
    [Test]
    public void Q658_BattleDialogueMenuWhitelisted()
    {
        Assert.That(DialogueFocus.BattleKeep, Does.Contain(".bfs-top .bfs-top-btns > button"));
        var bs = Web("Components", "BattleScreen.razor");
        Assert.That(bs, Does.Contain("var topLocked = fxLock || G.EncounterPromptActive;"));
        Assert.That(bs, Does.Contain("<header class=\"bfs-top\" inert=\"@topLocked\">"));
    }

    // ───────────── Q659 手动存档返回焦点 ─────────────

    /// <summary>Q659：从手动存档子页返回时焦点回「手动存档」按钮；该按钮不可用时回原首个按钮（继续游戏）。</summary>
    [Test]
    public void Q659_ManualReturnFocus()
    {
        var m = Web("Components", "MenuOverlay.razor");
        Assert.That(m, Does.Contain("@ref=\"_manualBtn\""));
        Assert.That(m, Does.Contain("_manualUsable = manualWhy == null;"));
        var at = m.IndexOf("if (backFromManual && _manualUsable)");
        Assert.That(at, Is.GreaterThan(0));
        Assert.That(m.IndexOf("await _manualBtn.FocusAsync(); return;", at), Is.GreaterThan(at));
    }

    // ───────────── Q660 空格主确认 ─────────────

    /// <summary>Q660：正式句逐字；默认确认键帽主显空格，数量编辑提交位仍 Enter；键表战斗行与通用帮助。</summary>
    [Test]
    public void Q660_TextsAndCaps()
    {
        Assert.That(ShellKeys.ConfirmKeysTitle, Is.EqualTo("空格或Enter均可确认，仅执行当前可用操作一次。"));
        Assert.That(ShellKeys.DefaultConfirmHelp, Is.EqualTo("空格用于当前界面的默认确认，Enter也可。编辑文字或数量、调整勾选和选择分支时，先按对应控件的规则操作；菜单只操作当前选中的按钮。中央行动的空格仍是跳过动画，战斗出手表现不可跳过。"));
        Assert.That(ShellAim.ConsoleGuide("确认结束行动 · 剩余2AP清零"), Is.EqualTo("在操作台点“确认结束行动 · 剩余2AP清零”或按空格确认（Enter也可）；ESC取消。"));
        var battle = ShellKeyHelp.Groups().Single(g => g.Title == "战斗").Rows;
        Assert.That(battle[3].Label, Is.EqualTo("执行（当前指令） 空格（Enter也可）"));
        Assert.That(ShellKeyHelp.Groups().Single(g => g.Title == "动画跳过").Rows.Single().Label, Is.EqualTo("跳过动画 空格"), "中央跳过保持");
        var doc = Verbatim();
        foreach (var t in new[] { ShellKeys.ConfirmKeysTitle, ShellKeys.DefaultConfirmHelp, BattleAimHint() })
            Assert.That(doc, Does.Contain(t), t);
        // 数量编辑中仍标 Enter 的位置
        foreach (var f in new[] { "CardActions.razor", "DropQuantity.razor", "FireMenu.razor", "GearGive.razor" })
            Assert.That(Web("Components", f), Does.Contain("Edit=\""), f);
    }

    /// <summary>
    /// BM8 试玩修复（S14 Q660 情境表“转移”行）：转移条、倒出、加工放入、放下数量框、交接数量的唯一确认，只在焦点在数量框里（编辑中）时标「Enter」；
    /// 没有数量框或焦点不在框里时标「空格」并带兼容键悬停句（EnterCap 非 Edit 分支）与 aria-keyshortcuts="Space Enter"。
    /// </summary>
    [Test]
    public void Q660_Playtest_EnterCapOnlyWhileEditingQuantity()
    {
        const string foc = "@onfocus=\"() => _qtyEditing = true\" @onblur=\"() => _qtyEditing = false\"";
        var ca = Web("Components", "CardActions.razor");
        Assert.That(ca, Does.Not.Contain("Edit=\"true\""), "不再无条件标 Enter");
        Assert.That(System.Text.RegularExpressions.Regex.Matches(ca, System.Text.RegularExpressions.Regex.Escape(foc)).Count, Is.EqualTo(2), "转移与倒出两个数量框跟踪编辑焦点");
        Assert.That(ca, Does.Contain("var goEdit = m.Mode == Mode.Stack && _qtyEditing;"), "单件（无数量框）永不标 Enter");
        Assert.That(ca, Does.Contain("aria-keyshortcuts=\"@(goEdit ? \"Enter\" : \"Space Enter\")\""));
        Assert.That(ca, Does.Contain("Edit=\"@goEdit\""));
        Assert.That(ca, Does.Contain("Edit=\"@(max > 1 && _qtyEditing)\""));
        foreach (var file in new[] { "FireMenu.razor", "DropQuantity.razor", "GearGive.razor" })
        {
            var src = Web("Components", file);
            Assert.That(src, Does.Not.Contain("Edit=\"true\""), file);
            Assert.That(src, Does.Contain(foc), file);
            Assert.That(src, Does.Contain("\"Space Enter\""), file);
        }
        var cap = Web("Components", "EnterCap.razor");
        Assert.That(cap, Does.Contain("else {<kbd class=\"kc\" title=\"@ShellKeys.ConfirmKeysTitle\">@ShellKeys.SpaceCap</kbd>}"), "非编辑：空格键帽＋兼容键悬停句");
    }

    private static string BattleAimHint()
    {
        var bs = Web("Components", "BattleScreen.razor");
        var at = bs.IndexOf("public const string AimDefaultHint = \"") + "public const string AimDefaultHint = \"".Length;
        return bs.Substring(at, bs.IndexOf('"', at) - at);
    }

    /// <summary>Q660：键盘路由——对白、局部唯一确认与战斗确认都接空格（编辑框里归输入），一次消费并吃掉松键；战斗里空格不再走中央跳过；号位上已明确敌人时提交一次。</summary>
    [Test]
    public void Q660_KeyRouting()
    {
        var js = Web("wwwroot", "js", "app.js");
        Assert.That(js, Does.Contain("if ((k === 'Enter' || (k === ' ' && !editable(document.activeElement))) && !control(document.activeElement)) {"), "对白");
        Assert.That(js, Does.Contain("if ((k === 'Enter' || (k === ' ' && !editable(tgt))) && !mod && !control(fae)) {"), "局部唯一确认");
        Assert.That(js.IndexOf("var sc = enterScope(fae);"), Is.LessThan(js.IndexOf("document.querySelector('.cprog[data-single-show]:not(.fading) [data-skip-anim]')")), "前景默认确认先于中央跳过");
        Assert.That(js, Does.Contain("if (k === ' ' && !document.querySelector('.bfs')) {"), "战斗中空格不走中央跳过");
        Assert.That(js, Does.Contain("var isOk = k === 'Enter' || k === ' ';"));
        Assert.That(js, Does.Contain("send(k === ' ' ? 'Enter' : "), "空格与 Enter 同一 C# 入口");
        Assert.That(js, Does.Contain("function eatSpace(ev)"), "松键不落到下一层");
        var bs = Web("Components", "BattleScreen.razor");
        Assert.That(bs, Does.Contain("if (G.BattleAiming && !G.BattleAllyAiming)"));
        Assert.That(bs, Does.Contain("if (!locked && !ev.Repeat && G.BattleAimTarget != null) await G.BattleAimSubmit(token);"));
    }

    // ───────────── Q661 PRO04 与教学文案 ─────────────

    /// <summary>Q661：PRO04 顶部系统提示、默认瞄准引导与 tutorial_arrive 附注逐字为逐字稿正式全文。</summary>
    [Test]
    public void Q661_TeachingTexts()
    {
        var doc = Verbatim();
        Assert.That(doc, Does.Contain($"| PRO04顶部系统提示替换全文 | {Cfg.Prologue.Tasks.Single(x => x.Id == "PRO04").Hint} |"));
        Assert.That(doc, Does.Contain($"| 默认攻击瞄准引导全文 | {BattleAimHint()} |"));
        Assert.That(doc, Does.Contain($"> {Cfg.Story.Dialogues.Single(d => d.Id == "tutorial_arrive").Note}"));
    }

    // ───────────── Q664 组名只显示一次 ─────────────

    /// <summary>Q664：生效效果／具备特性、敌人特性组名在同组多行里只显示首行，其余行组名留给读屏。</summary>
    [Test]
    public void Q664_GroupLabelOnce()
    {
        foreach (var (file, v) in new[] { ("ItemDetail.razor", "inst"), ("GearPeekDetail.razor", "it") })
            Assert.That(Web("Components", file), Does.Contain($"<span class=\"@(ti > 0 ? \"sr-only\" : null)\">@e.EquipTraitGroupLabel({v})</span>"), file);
        var d = Web("Components", "EnemyDetail.razor");
        Assert.That(System.Text.RegularExpressions.Regex.Matches(d, "<span class=\"@\\(ti > 0 \\? \"sr-only\" : null\\)\">@GameEngine.EnemyTraitsLabel</span>").Count, Is.EqualTo(2), "单卡与组卡成员两处");
    }

    // ───────────── 源码读取 ─────────────

    private static string Root()
    {
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "AGENTS.md"))) dir = dir.Parent;
        return dir!.FullName;
    }

    private static string Src(params string[] parts) => File.ReadAllText(Path.Combine(new[] { Root() }.Concat(parts).ToArray())).Replace("\r\n", "\n");

    private static string Web(params string[] parts) => Src(new[] { "web", "Survival.Web" }.Concat(parts).ToArray());

    private static string Verbatim() => File.ReadAllText(Directory.GetFiles(Path.Combine(Root(), "doc", "策划案"), "重墨 V2 开工裁定与界面逐字稿.md", SearchOption.AllDirectories).Single()).Replace("\r\n", "\n");
}
