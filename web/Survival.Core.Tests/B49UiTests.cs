using Survival.Core.Config;
using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 批次 B49 界面（R04／逐字稿「2026-11-15 B49 Q739—Q748」）：静态途径并集与表序（Q740—Q741）、条件预估参考者（Q742）、组摘要与无源成员（Q743）、
/// 动作标签（Q744）、「无感染源。」（Q745）、五处帮助（Q746）、同伴病毒短句（Q748-A01）。网页壳以源码检查。
/// </summary>
public class B49UiTests : B41cTestBase
{
    private const string Lin = "npc_lin_zhao";

    private static string Root()
    {
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "web", "Survival.sln"))) dir = dir.Parent;
        Assert.That(dir, Is.Not.Null);
        return dir!.FullName;
    }

    private static string Web(params string[] parts) => File.ReadAllText(Path.Combine(new[] { Root(), "web", "Survival.Web" }.Concat(parts).ToArray()));
    private static string Verbatim() => File.ReadAllText(Path.Combine(Root(), "doc", "策划案", "重墨像素风界面与卡片 v0.9", "重墨 V2 开工裁定与界面逐字稿.md"));

    private string Types(string enemyId) => E.ExposureTypeList(E.Enemy(enemyId));

    // ───────────── Q740—Q741 ─────────────

    /// <summary>Q740-A01：配电守卫静态列表精确为「抓伤／刮伤、冲撞擦伤」（追击默认类型不漏）；Q741-A01 顿号、表序、内部斜杠、去重。</summary>
    [Test]
    public void Q740_Q741_PowerGuard_And_Order()
    {
        Assert.That(Types("z_power_guard"), Is.EqualTo("抓伤／刮伤、冲撞擦伤"));
        Assert.That(E.EnemyInfectionMemberText("配电守卫", E.Enemy("z_power_guard")), Is.EqualTo("配电守卫：可通过抓伤／刮伤、冲撞擦伤传播病毒。"));
        Assert.That(Types("shambler"), Is.EqualTo("咬伤、抓伤／刮伤"), "咬击替换＋默认");
        Assert.That(Types("z_bloater"), Does.Contain("变异体液"), "合法喷溅并入");
        Assert.That(Types("z_spitter"), Is.EqualTo("抓伤／刮伤、变异体液"));
    }

    /// <summary>Q740-A02：42 敌种并集＝全部步骤 exposure＋咬击替换＋默认＋合法喷溅，去 none，按 profile 表序；不读锁定意图（改锁定意图／站位不变）。</summary>
    [Test]
    public void Q740_A02_AllEnemies_UnionFromConfig_IgnoresLockedIntent()
    {
        var order = Cfg.Infection.ExposureProfiles.Select(p => p.Id).ToList();
        foreach (var ec in Cfg.Enemies)
        {
            var ids = new HashSet<string>();
            foreach (var st in ec.Intents.Opening.Concat(ec.Intents.Cycle))
            {
                ids.Add(st.Exposure!);
                if (st.BiteReplacementExposure != null) ids.Add(st.BiteReplacementExposure);
            }
            ids.Add(ec.DefaultPhysicalExposure!);
            if (ec.DeathSplash > 1e-12) ids.Add(ec.DeathSplashExposure!);
            ids.Remove("none");
            var want = order.Where(ids.Contains).ToList();
            Assert.That(E.EnemyExposureTypes(ec).Select(p => p.Id), Is.EqualTo(want), ec.Id);
            Assert.That(E.HasInfectionSource(ec), Is.EqualTo(want.Count > 0), ec.Id);
        }
        var s = Fight(4950, "z_power_guard");
        var t = Enemy(s, 1);
        var before = E.EnemyExposureInfoFor(s, E.Enemy(t.EnemyId), E.EnemyName(t), E.InBattleExposureReference(s));
        t.Intent = "charge";
        t.Slot = "4";
        var after = E.EnemyExposureInfoFor(s, E.Enemy(t.EnemyId), E.EnemyName(t), E.InBattleExposureReference(s));
        Assert.That(after.Line, Is.EqualTo(before.Line));
        Assert.That(after.Estimates, Is.EqualTo(before.Estimates));
    }

    // ───────────── Q742 ─────────────

    /// <summary>Q742-A01：战前参考主角（标题「你 · 命中且正伤害后：」，本人 I／V）；卡无有效成员时只静态途径、无标题与概率，不造 0%。</summary>
    [Test]
    public void Q742_A01_PreBattle_ReferencePlayer()
    {
        var s = Field(2, "z_power_guard");
        var card = E.EnemyCardsHere(s).Single();
        Assert.That(E.PreBattleExposureReference(s, card), Is.EqualTo(GameState.PlayerId));
        s.Body.Immunity = 70;
        s.Body.Infected = false;
        var info = E.EnemyExposureInfoFor(s, E.Enemy("z_power_guard"), "配电守卫", E.PreBattleExposureReference(s, card));
        Assert.That(info.ReferenceTitle, Is.EqualTo("你 · 命中且正伤害后："));
        Assert.That(info.Estimates, Is.EqualTo(new[] { "抓伤／刮伤：概率10.75%，感染后病毒10。", "冲撞擦伤：概率5.38%，感染后病毒5。" }));
        s.Body.Infected = true;
        s.Body.VirusLoad = 97.5;
        info = E.EnemyExposureInfoFor(s, E.Enemy("z_power_guard"), "配电守卫", GameState.PlayerId);
        Assert.That(info.Estimates, Is.EqualTo(new[] { "抓伤／刮伤：概率10.75%，病毒+2.5。", "冲撞擦伤：概率5.38%，病毒+2.5。" }), "再次按封顶后实际增量");

        var none = E.EnemyExposureInfoFor(s, E.Enemy("z_power_guard"), "配电守卫", null);
        Assert.That((none.Line, none.ReferenceTitle, none.Estimates.Count), Is.EqualTo(("配电守卫：可通过抓伤／刮伤、冲撞擦伤传播病毒。", (string?)null, 0)));
        foreach (var w in s.WorldEnemies) w.Hp = 0;
        Assert.That(E.PreBattleExposureReference(s, card), Is.Null, "无可信入场投影：不给参考");
    }

    /// <summary>Q742-A02：战中参考当前查看／控制的我方角色，切人按本人 I／V 立即更新；查询不抽签、不加 V。</summary>
    [Test]
    public void Q742_A02_InBattle_FocusSwitch()
    {
        var s = New(4951);
        E.DebugQuickParty(s, 2);
        E.DebugTeleport(s, "field");
        s.WorldEnemies.Clear();
        var id = E.DebugSpawnEnemy(s, "shambler", "field").Id;
        Assert.That(E.DebugStartBattle(s, new[] { id }, GameEngine.FearEncounter), Is.True);
        s.Battle!.Allies = new List<string> { GameState.PlayerId, Lin };
        s.Actors[GameState.PlayerId].Body.Immunity = 0;
        s.Actors[Lin].Body.Immunity = 100;
        var ec = E.Enemy("shambler");
        var r0 = s.Rng.State;
        var me = E.EnemyExposureInfoFor(s, ec, "游荡尸", E.InBattleExposureReference(s));
        Assert.That(me.ReferenceTitle, Is.EqualTo("你 · 命中且正伤害后："));
        Assert.That(me.Estimates, Is.EqualTo(new[] { "咬伤：概率60%，感染后病毒20。", "抓伤／刮伤：概率30%，感染后病毒10。" }));
        using (E.FocusOn(s, Lin))
        {
            var lin = E.EnemyExposureInfoFor(s, ec, "游荡尸", E.InBattleExposureReference(s));
            Assert.That(lin.ReferenceTitle, Is.EqualTo("林昭 · 命中且正伤害后："));
            Assert.That(lin.Estimates, Is.EqualTo(new[] { "咬伤：概率5%，感染后病毒20。", "抓伤／刮伤：概率2.5%，感染后病毒10。" }));
        }
        Assert.That((s.Rng.State, s.Body.Infected), Is.EqualTo((r0, false)), "只读");
        var src = Web("Components", "BattlePanel.razor");
        Assert.That(src, Does.Contain("<ExposureLines Info=\"@e.EnemyExposureInfoFor(s, ec, e.EnemyName(t), e.InBattleExposureReference(s))\" />"));
    }

    // ───────────── Q743 ─────────────

    /// <summary>Q743-A01：组摘要只在有效成员≥2 且至少一人有源；位置：右栏战斗详情折叠顶、中央盘成员列表前，各一次。</summary>
    [Test]
    public void Q743_A01_GroupSummary_Conditions_And_Places()
    {
        Assert.That(E.ShowGroupInfectionSummary(new[] { "shambler", "raider_melee" }), Is.True);
        Assert.That(E.ShowGroupInfectionSummary(new[] { "shambler" }), Is.False, "单人组无本组摘要");
        Assert.That(E.ShowGroupInfectionSummary(new[] { "raider_melee", "wild_dog_leader" }), Is.False, "全无源不造摘要");
        Assert.That(GameEngine.GroupInfectionSourceText, Is.EqualTo("本组有感染源，详见成员。"));

        var d = Web("Components", "EnemyDetail.razor").Replace("\r\n", "\n");
        var fold = d.IndexOf("Group=\"@RailGroups.BattleDetail\"", StringComparison.Ordinal);
        var weapon = d.IndexOf("<span>开战武器</span>", StringComparison.Ordinal);
        var group = d.IndexOf("@GameEngine.GroupInfectionSourceText", StringComparison.Ordinal);
        Assert.That(group, Is.GreaterThan(fold).And.LessThan(weapon), "折叠顶");
        Assert.That(d.Split("@GameEngine.GroupInfectionSourceText").Length - 1, Is.EqualTo(1));
        var pops = Web("Components", "ActionPops.razor");
        var pg = pops.IndexOf("@GameEngine.GroupInfectionSourceText", StringComparison.Ordinal);
        var loop = pops.IndexOf("@for (var k = 0; k < members.Count; k++)", pg, StringComparison.Ordinal);
        Assert.That(pg, Is.GreaterThan(0));
        Assert.That(loop, Is.GreaterThan(pg), "中央盘成员列表前");
        Assert.That(pops.Split("@GameEngine.GroupInfectionSourceText").Length - 1, Is.EqualTo(1));
    }

    /// <summary>Q743-A02／Q745-A01：无源成员（人类、普通动物）在右栏、中央盘、战中各自段写「无感染源。」，不残留旧长句；动物原出血仍在意图标签里。</summary>
    [Test]
    public void Q743_A02_Q745_NoSource_EachPlace_OldTextGone()
    {
        var s = Fight(4952, "raider_melee");
        foreach (var id in new[] { "raider_melee", "wild_dog_leader", "feral_dog" })
        {
            var info = E.EnemyExposureInfoFor(s, E.Enemy(id), E.Enemy(id).Name, GameState.PlayerId);
            Assert.That((info.HasSource, info.Line, info.ReferenceTitle), Is.EqualTo((false, "无感染源。", (string?)null)), id);
        }
        Assert.That(GameEngine.NoInfectionSourceText, Is.EqualTo("无感染源。"));
        foreach (var f in new[] { "EnemyDetail.razor", "ActionPops.razor", "BattlePanel.razor" })
        {
            var src = Web("Components", f);
            Assert.That(src, Does.Contain("<ExposureLines Info="), f);
            foreach (var old in new[] { "不会传染僵尸病毒", "不传播病毒", "单次咬伤感染概率", "咬伤可能感染", "NonBiteNote", "抓击、喷吐、尖啸不会感染" })
                Assert.That(src, Does.Not.Contain(old), $"{f}：{old}");
        }
        var lines = Web("Components", "ExposureLines.razor");
        Assert.That(lines, Does.Contain("data-exposure=\"none\">@Info.Line"));
        var dog = Fight(4953, "feral_dog");
        Assert.That(Cfg.Enemies.Single(x => x.Id == "feral_dog").Intents.Cycle.Any(x => x.BleedPct > 0) ? E.IntentShown(dog.Battle!, Enemy(dog, 1)) : "出血", Does.Contain("流血"), "动物流血信息仍在原意图标签");
    }

    // ───────────── Q744 ─────────────

    /// <summary>Q744-A01：喷吐标签无「（不感染）」与空括号；咬击保留「（可能感染）」，其他动作不加后缀。</summary>
    [Test]
    public void Q744_A01_IntentLabels()
    {
        var s = Fight(742, "shambler", "shambler", "z_spitter");
        var sp = Enemy(s, 3);
        Assert.That(sp.Intent, Is.EqualTo("spit"));
        Assert.That(E.IntentShown(s.Battle!, sp), Does.Not.Contain("感染").And.Not.Contain("（）"));
        var src = File.ReadAllText(Path.Combine(Root(), "src", "Core", "Engine", "GameEngine.EnemyAi.cs"));
        Assert.That(src, Does.Contain("（可能感染）"), "咬击后缀保留");
        Assert.That(src, Does.Not.Contain("（不感染）\"").And.Not.Contain("（不感染）。"), "喷吐标签与喷溅句都不再拼「（不感染）」");
    }

    // ───────────── Q746 ─────────────

    /// <summary>Q746-A01／A02：五处帮助按正式表；病毒六段变量从配置绑定（80／0.05／1、分段边界、1／100／1、80／12小时／10）。</summary>
    [Test]
    public void Q746_FivePlaces_And_VirusSixParagraphs()
    {
        // ① 战斗帮助
        Assert.That(GameEngine.ExposureCombatHelpText, Is.EqualTo("咬伤、抓伤／刮伤、污染器械伤、变异体液、冲撞擦伤：感染源造成正伤害后可能传播病毒。人类、普通动物：无感染源。"));
        var help = Web("Services", "HelpCatalog.cs");
        Assert.That(help, Does.Contain("GameEngine.ExposureCombatHelpText,"));
        Assert.That(help, Does.Not.Contain("抓击、喷吐、尖啸和人类、动物的攻击不会传染"));
        // ② 生存提示（咬伤句换 risk，饱饱两句保留）
        Assert.That(Cfg.Progress.Tips, Has.Some.StartsWith("传播途径见敌人详情；免疫越低，同一途径越易感染。"));
        Assert.That(Cfg.Progress.Tips, Has.None.Contains("咬伤"));
        // ③ 免疫说明末句
        Assert.That(GameEngine.ImmunityTailText, Is.EqualTo("免疫越低越易感染；同伴病毒不自然增长。离线不恢复。"));
        var ui = Web("Services", "Ui.cs");
        Assert.That(ui, Does.Contain("GameEngine.ImmunityTailText;"));
        Assert.That(ui, Does.Not.Contain("被丧尸咬伤越容易感染"));
        Assert.That(ui, Does.Contain("e.VirusHelpParagraphs()"));
        // ④ 病毒说明六段
        Assert.That(E.VirusHelpParagraphs(), Is.EqualTo(new[]
        {
            "外部暴露分五类，概率和病毒量各异；仅主角有饱饱输入。同伴病毒不自然增长，新暴露仍可加量。",
            "主角有饱饱时：免疫低于80，每小时输入最多0.05病毒；免疫越高越少，净输入为正即感染（病毒至少1）。",
            "自然变化：主角感染后每小时0≤免疫<40：+4/小时、40≤免疫<70：+2/小时、70≤免疫<80：+0.5/小时、80≤免疫≤100：−1.5/小时；同伴仅保留其中下降，正增长为0。未治愈时病毒最低1。",
            "病毒达100即尸变；主角尸变结束本局。",
            "绷带、抗生素可降病毒，最低1；不能清除感染。",
            "治愈：免疫≥80持续12小时且病毒≤10，或使用病毒特效药。",
        }));
        var attr = Web("Components", "AttrDetail.razor");
        Assert.That(attr, Does.Contain("@foreach (var para in Ui.VirusExplain(e)) { <p class=\"attr-explain\">@para</p> }"));
        Assert.That(help, Does.Contain("Paragraphs = (e, s) => Ui.VirusExplain(e) },"));
        // ⑤ 免疫页咬伤概率行（标签与原 p0）
        Assert.That(GameEngine.BiteProbabilityLabel, Is.EqualTo("咬伤感染概率"));
        Assert.That(attr, Does.Contain("<span>@GameEngine.BiteProbabilityLabel</span><b>@Ui.Pct(e.ExposureChance(s.Body.Immunity))</b>"));
        // 正式表逐字（防漂移）
        var v = Verbatim();
        foreach (var x in new[] { GameEngine.ExposureCombatHelpText, GameEngine.ExposureRiskTipText, GameEngine.ImmunityTailText, GameEngine.CompanionVirusNote, GameEngine.GroupInfectionSourceText, GameEngine.NoInfectionSourceText })
            Assert.That(v, Does.Contain("| " + x + " |"), x);
    }

    // ───────────── Q748 ─────────────

    /// <summary>Q748-A01：仅已感染非主角同伴详情病毒行下一次短句；V 低仍感染继续，治愈后隐藏；主角不套用。</summary>
    [Test]
    public void Q748_A01_CompanionNote()
    {
        var s = New(4954);
        E.DebugQuickParty(s, 2);
        var lin = s.Actors[Lin];
        Assert.That(E.CompanionVirusNoteFor(lin), Is.Null, "未感染不显示");
        lin.Body.Infected = true;
        lin.Body.VirusLoad = 1;
        Assert.That(E.CompanionVirusNoteFor(lin), Is.EqualTo("同伴病毒不自然增长；新暴露可增加，高免疫和药物可降低。"));
        lin.Body.Infected = false;
        lin.Body.VirusLoad = 0;
        Assert.That(E.CompanionVirusNoteFor(lin), Is.Null, "治愈后隐藏");
        s.Body.Infected = true;
        s.Body.VirusLoad = 30;
        Assert.That(E.CompanionVirusNoteFor(s.Actors[GameState.PlayerId]), Is.Null, "主角不套用");
        foreach (var (f, row) in new[] { ("MemberPeekDetail.razor", "<span>病毒</span>"), ("BattlePanel.razor", "<span>感染</span>") })
        {
            var src = Web("Components", f);
            var at = src.IndexOf(row, StringComparison.Ordinal);
            var note = src.IndexOf("e.CompanionVirusNoteFor(", StringComparison.Ordinal);
            Assert.That(note, Is.GreaterThan(at).And.GreaterThan(0), f);
            Assert.That(src.Split("e.CompanionVirusNoteFor(").Length - 1, Is.EqualTo(1), f);
        }
        Assert.That(Web("Components", "AttrDetail.razor"), Does.Not.Contain("CompanionVirusNote"), "病毒系统说明已由 virusIntro／natural 覆盖");
    }

    /// <summary>喷溅特性完整版（逐字稿「喷溅与Q652感染都已实际接入时的静态特性」）。</summary>
    [Test]
    public void SplashTrait_FullVersion()
    {
        Assert.That(E.EnemyPublicTraits(E.Enemy("z_bloater")), Has.Some.EqualTo("死亡喷溅：被我方主动攻击直接击杀时，伤及我方1、2号位，基础伤害0.30，护甲照常计算；造成正伤害时可能传播病毒。因流血等伤害型负面状态、反击或迎击死亡时不触发。"));
    }

    /// <summary>B49 试玩修复：1280×720 敌踪处理盘按内容长高、放不下才滚动；战中阅读区内层不再被压到可视高度（底部说明不压行）。</summary>
    [Test]
    public void PlaytestFix_PopHeightAndBattleInfoFoot()
    {
        var shell = Web("wwwroot", "css", "shell.css");
        Assert.That(shell, Does.Contain(".wasteland .apop.apop-c.enemycard { height: fit-content; min-height: min(var(--pop-h), calc(100vh - 16px)); max-height: calc(100vh - 16px); }"));
        Assert.That(shell, Does.Contain("no-repeat local,"), "底部阴影只在还能往下滚时出现");
        Assert.That(Web("wwwroot", "css", "battle.css"), Does.Contain(".bi-body :is(.detail-body, .detail-scroll) { flex: 0 0 auto; }"));
    }
}
