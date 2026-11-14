using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 BM7b「战前查看与装备显示」（R239，答复 Q646—Q651、Q653、Q654）。规则出处：S14 文首「2026-11-14 Q644—Q655：简化说明、真实估算与多途径感染」
/// §Q646、§Q647／Q654、§Q648／Q649、§Q650／Q651、§Q652／Q653 与验收 Q646-A01—Q654-A01；R05 文首「2026-11-14 Q646—Q654」及 Q648 名称表；
/// 正式句见《重墨 V2 开工裁定与界面逐字稿》文首「2026-11-14 Q644—Q655正式文字与感染覆盖」。网页壳接线以源码检查（同 BM5b1RiskTests）。
/// </summary>
public class BM7bTests : B41cTestBase
{
    private EnemyCardView Card(GameState s) => E.EnemyCardsHere(s).Single();

    // ───────────── Q646 ─────────────

    /// <summary>Q646-A01：卡离开及无有效成员两种情况都不进敌踪详情（回当前地块／默认右栏），右栏不再写两套失效原因；不移动、不改状态。</summary>
    [Test]
    public void Q646_A01_GoneOrEmptyCardFallsBackToPlace()
    {
        var (s, key) = OneShambler();
        Assert.That(E.EnemyCardShowable(s, key), Is.True);
        var w = s.WorldEnemies.Single();
        var before = (s.CurrentLocation, s.GameMinute, s.Rng.State);
        w.Hp = 0;
        Assert.That(E.EnemyCardShowable(s, key), Is.False, "卡还有壳但无有效活成员");
        Assert.That(E.EngageRiskFor(s, Card(s)).Invalid, Is.True, "领域仍标 Invalid 供内核处理");
        w.Hp = E.Enemy(w.EnemyId).Hp;
        s.WorldEnemies.Remove(w);
        Assert.That(E.EnemyCardShowable(s, key), Is.False, "整卡离开");
        Assert.That(E.EnemyCardShowable(s, null), Is.False);
        Assert.That((s.CurrentLocation, s.GameMinute, s.Rng.State), Is.EqualTo(before), "只读回退，不移动、不推进");
        Assert.That(E.CurrentPlaceSummary(s), Is.Not.Empty, "当前地块详情可解析");

        var sel = Web("Components", "SelectionDetail.razor");
        Assert.That(sel, Does.Contain("case SelectionKind.Enemy when e.EnemyCardShowable(s, sel.Id):\n        <EnemyDetail Sel=\"sel\" />"));
        var def = sel.Substring(sel.IndexOf("    default:", StringComparison.Ordinal));
        Assert.That(def, Does.Contain("e.CurrentPlaceSummary(s)").And.Contain("@ShellText.PlaceTitle"), "回当前地块／区域详情（原默认右栏）");
        var d = Web("Components", "EnemyDetail.razor");
        Assert.That(d, Does.Not.Contain("这个敌人已经离开").And.Not.Contain("选择地点、路线、资源或物品查看信息").And.Not.Contain("@GameEngine.EnemyCardInvalidText"));
        Assert.That(d, Does.Contain("@if (!risk.Invalid)"));
        // 悬停查看同一组件：新对象的悬停直接换到新对象（PeekDetail 按当前键分派），旧敌踪消失不会盖回
        Assert.That(Web("Components", "PeekDetail.razor"), Does.Contain("<SelectionDetail Sel=\"sel\" Peek=\"true\""));
    }

    // ───────────── Q647 ─────────────

    /// <summary>Q647-A01：按本人精神／有效惊魂未定得确定 P，不消耗状态／随机；精神0且有效惊魂未定 P60 乘原0.9，截止等于当前分钟无加值；风险与命中同源。</summary>
    [Test]
    public void Q647_A01_DeterministicEntryPanic()
    {
        var (s, _) = OneShambler();
        Arm(s, "hatchet");
        var me = s.Actors[GameState.PlayerId];
        s.Stats.Energy = 100;
        s.Stats.Wakefulness = 100;
        s.Stats.Morale = 100;
        Assert.That(E.DeterministicEntryPanic(s, GameState.PlayerId), Is.EqualTo(0));
        var calm = E.PreBattleHits(s, Card(s)).Single();
        var calmDa = E.EngageRiskFactorsFor(s, Card(s))!.DA;
        Assert.That(calm.Legal, Is.True);

        s.Stats.Morale = 0;
        Assert.That(E.DeterministicEntryPanic(s, GameState.PlayerId), Is.EqualTo(50), "(100−0)/2");
        Assert.That(E.PreBattleHits(s, Card(s)).Single().Accuracy, Is.EqualTo(calm.Accuracy).Within(1e-9), "50 未到 60 门槛");

        me.ShakenUntil = s.GameMinute;
        Assert.That(E.DeterministicEntryPanic(s, GameState.PlayerId), Is.EqualTo(50), "截止等于当前分钟不再有效");
        me.ShakenUntil = s.GameMinute + 30;
        Assert.That(E.DeterministicEntryPanic(s, GameState.PlayerId), Is.EqualTo(60), "有效惊魂未定 +10");
        var factor = E.Config.Panic.PenaltyFor(60)!.AccuracyRatingFactor;
        Assert.That(factor, Is.EqualTo(0.9));

        var before = SaveSerializer.Serialize(s);
        var rng = s.Rng.State;
        var shaken = E.PreBattleHits(s, Card(s)).Single();
        Assert.That(shaken.Accuracy, Is.EqualTo(calm.Accuracy * factor).Within(1e-9), "原门槛 ×0.9，只乘一次");
        Assert.That(shaken.HitChance, Is.LessThan(calm.HitChance));
        Assert.That(E.EngageRiskFactorsFor(s, Card(s))!.DA, Is.LessThan(calmDa), "风险用同一份确定恐慌");
        Assert.That(me.ShakenUntil, Is.EqualTo(s.GameMinute + 30), "不清惊魂未定");
        Assert.That(s.Rng.State, Is.EqualTo(rng), "不抽随机受惊");
        Assert.That(s.Battle, Is.Null);
        Assert.That(SaveSerializer.Serialize(s), Is.EqualTo(before), "只读");

        // 说明原句替换旧「恐慌、失衡开战后才计」
        Assert.That(GameEngine.PreBattleHitNote, Is.EqualTo("已计入确定的入场恐慌；未计入随机受惊。"));
        var d = Web("Components", "EnemyDetail.razor");
        Assert.That(d, Does.Contain("@GameEngine.PreBattleHitNote").And.Not.Contain("恐慌、失衡开战后才计"));
        Assert.That(Verbatim(), Does.Contain("| 战前命中口径说明 | 已计入确定的入场恐慌；未计入随机受惊。 |"));
    }

    /// <summary>Q647：各参战者用本人精神，不把主角精神复用给全队。</summary>
    [Test]
    public void Q647_PerActorMorale()
    {
        var (s, _) = OneShambler(2);
        E.DebugQuickParty(s, 2);
        var mate = s.Party.First(id => id != GameState.PlayerId);
        s.Actors[GameState.PlayerId].Stats.Morale = 100;
        s.Actors[mate].Stats.Morale = 20;
        Assert.That(E.DeterministicEntryPanic(s, GameState.PlayerId), Is.EqualTo(0));
        Assert.That(E.DeterministicEntryPanic(s, mate), Is.EqualTo(40));
    }

    // ───────────── Q648 ─────────────

    /// <summary>Q648-A01：8 座地牢最终守卫均「BOSS·本体专名」；本体移出后不改称杂兵；普通固定战沿原名角标；卡面、处理盘、右栏同源。</summary>
    [Test]
    public void Q648_A01_BossNames()
    {
        var expect = new Dictionary<string, string>
        {
            ["d01_07"] = "BOSS·封锁祭室守卫", ["d02_07"] = "BOSS·隔离核心守卫", ["d03_07"] = "BOSS·军械库守卫", ["d04_06"] = "BOSS·天线台守卫",
            ["d05_06"] = "BOSS·泵房推挤者", ["d06_07"] = "BOSS·深井重击者", ["d07_06"] = "BOSS·扶梯拦截者", ["d08_07"] = "BOSS·仓管守卫",
        };
        Assert.That(E.Config.Map.Dungeons.Select(d => d.GuardNode), Is.EquivalentTo(expect.Keys));
        foreach (var (node, name) in expect)
        {
            var s = At(node, 4);
            var card = E.EnemyCardsHere(s).Single(c => c.Kind == "fixed");
            Assert.That((card.Guardian, card.Boss), Is.EqualTo((true, true)), node);
            Assert.That(GameEngine.EnemyRailIdentity(card), Is.EqualTo(name), node);
            Assert.That(card.Badge, Is.EqualTo("守卫"), "角标不改");
        }
        // 本体移出名单后，标题仍是本体专名
        var t = At("d01_07", 4);
        var body = t.WorldEnemies.Single(w => w.EnemyId == "ec_boss_crypt");
        t.WorldEnemies.Remove(body);
        var left = E.EnemyCardsHere(t).Single(c => c.Kind == "fixed");
        Assert.That(left.Members.Select(w => w.EnemyId), Does.Not.Contain("ec_boss_crypt"));
        Assert.That(GameEngine.EnemyRailIdentity(left), Is.EqualTo("BOSS·封锁祭室守卫"));
        // 普通固定战：原名＋原角标，不补前缀
        var plain = E.EnemyCardsHere(At("d01_02", 4)).Single(c => c.Kind == "fixed");
        Assert.That((plain.Boss, plain.Guardian, GameEngine.EnemyRailIdentity(plain), plain.Badge), Is.EqualTo((false, false, GameEngine.FixedCardTitle, "固定")));
        // 非 BOSS 的真实守卫（数据里暂无）：「守卫·{守卫名字}」
        Assert.That(GameEngine.EnemyRailIdentity(new EnemyCardView { Guardian = true, Title = "地牢守卫者" }), Is.EqualTo("守卫·地牢守卫者"));
        Assert.That(GameEngine.EnemyRailIdentity(new EnemyCardView { Guardian = true, Boss = true, Title = "BOSS·某守卫" }), Is.EqualTo("BOSS·某守卫"), "身份前缀只一次");
        // 卡面、中央处理盘、右栏同源
        Assert.That(Web("Components", "LocationStrip.razor"), Does.Contain("Title=\"@GameEngine.EnemyRailIdentity(c)\""));
        Assert.That(Web("Components", "ActionPops.razor"), Does.Contain("<AnchorPop AnchorId=\"@pop.AnchorId\" Title=\"@GameEngine.EnemyRailIdentity(c)\""));
        Assert.That(Web("Components", "EnemyDetail.razor"), Does.Contain("<Name>@GameEngine.EnemyRailIdentity(card)</Name>"));
        foreach (var name in expect.Values) Assert.That(R05(), Does.Contain(name), name);
    }

    // ───────────── Q649 ─────────────

    /// <summary>Q649-A01：免流血、护盔、喷溅、低血逃跑、保护能力逐实际配置显示、无则不列；阈值／基础伤害取配置；组卡按成员；不读本回合意图。</summary>
    [Test]
    public void Q649_A01_PublicTraits()
    {
        Assert.That(E.EnemyPublicTraits(E.Enemy("z_helmet")), Is.EqualTo(new[] { "流血免疫：不会流血。", "护盔：抵挡本场首次失衡效果。" }));
        Assert.That(E.EnemyPublicTraits(E.Enemy("z_bloater")), Is.EqualTo(new[] { "流血免疫：不会流血。", "死亡喷溅：死亡时伤及我方1、2号位，基础伤害0.30，护甲照常计算。" }));
        Assert.That(E.EnemyPublicTraits(E.Enemy("wolf")), Is.EqualTo(new[] { "低血逃跑：健康不高于1.20时可能尝试逃离。" }), "fleeAtHp 是绝对健康阈值，沿健康精度");
        Assert.That(E.EnemyPublicTraits(E.Enemy("feral_dog")), Is.Empty, "无则不造空行");
        var protectors = E.Config.Enemies.Where(x => x.Intents.Opening.Concat(x.Intents.Cycle).Any(st => st.Kind == "protect")).ToList();
        Assert.That(protectors, Is.Not.Empty);
        foreach (var p in protectors) Assert.That(E.EnemyPublicTraits(p).Last(), Is.EqualTo("保护同伴：具备保护同伴的能力，需满足实际站位和目标条件。"), p.Id);
        foreach (var x in E.Config.Enemies.Where(x => protectors.All(p => p.Id != x.Id)))
            Assert.That(E.EnemyPublicTraits(x), Does.Not.Contain(GameEngine.TraitProtectText), x.Id);
        // 正式句表（喷溅句前半）
        var v = Verbatim();
        foreach (var t in new[] { GameEngine.TraitImmuneBleedText, GameEngine.TraitStaggerOnceText, GameEngine.TraitProtectText })
            Assert.That(v, Does.Contain(t), t);
        Assert.That(v, Does.Contain("| deathSplash>0 | 死亡喷溅：死亡时伤及我方1、2号位，基础伤害{伤害值}，护甲照常计算；"));
        Assert.That(v, Does.Contain("| fleeAtHp有值 | 低血逃跑：健康不高于{健康阈值}时可能尝试逃离。 |"));
        // 只读静态配置：特性查询不碰 Intents 锁定／PlanFor
        var ui = File.ReadAllText(Path.Combine(Root(), "src", "Core", "Engine", "GameEngine.EnemyCardsUi.cs"));
        var body = ui.Substring(ui.IndexOf("public List<string> EnemyPublicTraits(", StringComparison.Ordinal));
        body = body.Substring(0, body.IndexOf("\n        }", StringComparison.Ordinal));
        Assert.That(body, Does.Not.Contain("PlanFor").And.Not.Contain("Intent ").And.Not.Contain("s.Battle"));
        // 右栏：当前成员特性在主区主要属性后、折叠组前；其他成员跟各自完整属性
        var d = Web("Components", "EnemyDetail.razor");
        var core = d.IndexOf("@CoreLines(card, members[0]) @Traits(members[0].EnemyId)", StringComparison.Ordinal);
        Assert.That(core, Is.GreaterThan(0).And.LessThan(d.IndexOf("<RailFold", StringComparison.Ordinal)));
        var lines = d.Substring(d.IndexOf("private RenderFragment MemberLines(", StringComparison.Ordinal));
        Assert.That(lines.Substring(0, lines.IndexOf("<span>对你命中率</span>", StringComparison.Ordinal)), Does.Contain("e.EnemyPublicTraits(ec)"));
        Assert.That(GameEngine.EnemyTraitsLabel, Is.EqualTo("特性"));
    }

    // ───────────── Q650 ─────────────

    private ItemInstance Make(GameState s, string id, string owner) => E.CreateItems(s, id, 1, owner).Single();

    private string? FixedRow(GameState s, ItemInstance it) =>
        E.RailEquipLines(s, it, out _).Where(x => x.Label == "固定效果").Select(x => x.Value).SingleOrDefault();

    /// <summary>Q650-A01：非武器固定效果逐真实消费者分项；未穿戴不冒称生效；混合件不整件误标；位移抵抗与护拳绑带不漏；只有描述的效果不借显示补能力。</summary>
    [Test]
    public void Q650_A01_GearEffectsByConsumer()
    {
        var s = New();
        const string old = GameEngine.GearNotLiveNote;
        // 混合：护甲已接入，接受治疗×1.10 无消费者 → 分项
        var medic = Make(s, "medic_armor", ItemInstance.EquipOwner(GameState.PlayerId, EquipSlots.Armor));
        Assert.That(E.RailEquipLines(s, medic, out var act).Any(x => x.Label == "固定效果" && x.Value == "接受的非病毒医疗恢复×1.10" + old), Is.True);
        Assert.That(act, Is.EqualTo(new[] { "护甲+0.20" }));
        Assert.That(E.EquipTraitGroupLabel(medic), Is.EqualTo("生效效果"));
        var balanced = Make(s, "balanced_armor", ItemInstance.Carried);
        Assert.That(E.EquipActiveTraitLines(s, balanced), Is.EqualTo(new[] { "护甲+0.40" }));
        Assert.That(FixedRow(s, balanced), Is.EqualTo("首次受暴击时该击暴击倍率减0.25（每场一次）" + old));
        Assert.That(E.EquipTraitGroupLabel(balanced), Is.EqualTo("具备特性"), "未穿戴不写生效中");
        // 五件只有描述、没有消费者的饰品与净水套件：全部留说明，旧句只给它们
        foreach (var id in new[] { "guard_badge", "hunter_token", "aid_badge", "resolve_medal", "tactics_watch", "filter_token" })
        {
            var it = Make(s, id, ItemInstance.Carried);
            Assert.That(E.EquipActiveTraitLines(s, it), Is.Empty, id);
            Assert.That(FixedRow(s, it), Is.EqualTo(E.Gear(id)!.EffectText + old), id);
        }
        // 专用字段与位移抵抗不漏
        Assert.That(E.EquipActiveTraitLines(s, Make(s, "heavy_armor", ItemInstance.Carried)), Is.EqualTo(new[] { "护甲+0.65", "速度−2", "位移抵抗+15个百分点" }));
        Assert.That(E.EquipActiveTraitLines(s, Make(s, "knuckle_wraps", ItemInstance.Carried)), Is.EqualTo(new[] { E.Gear("knuckle_wraps")!.EffectText }));
        Assert.That(E.EquipActiveTraitLines(s, Make(s, "padded_vest", ItemInstance.Carried)), Is.EqualTo(new[] { "护甲+0.10", "速度0" }));
        Assert.That(E.EquipActiveTraitLines(s, Make(s, "expedition_coat", ItemInstance.Carried)), Is.EqualTo(new[] { "非战斗负N/W小时率×0.95；不改即时效果" }), "限定句随前一项");
        // 每件分项逐字取原句，不新编、不改数值；全部已接入的件不再有「固定效果」行
        foreach (var g in E.Config.Gear)
        {
            var clauses = E.GearEffectClauses(g);
            Assert.That(string.Join("；", clauses.Select(c => c.Text)), Is.EqualTo(g.EffectText), g.Id);
            var it = Make(s, g.Id, ItemInstance.Carried);
            if (clauses.All(c => c.Live)) Assert.That(FixedRow(s, it), Is.Null, g.Id);
        }
        // 耐久为 0：效果暂停不列，原句留说明（无旧“上线后生效”）
        var broken = Make(s, "leather_armor", ItemInstance.EquipOwner(GameState.PlayerId, EquipSlots.Armor));
        broken.Durability = 0;
        Assert.That(E.EquipActiveTraitLines(s, broken), Is.Empty);
        Assert.That(FixedRow(s, broken), Is.EqualTo("护甲+0.20；速度0"));
        // 一级面板（整备比较）原行不变
        Assert.That(E.EquipDetailLines(s, medic).Single(x => x.Label == "固定效果").Value, Does.Contain("部分生效中"));
        // 右栏：「固定效果」行不再固定进首屏
        foreach (var f in new[] { "ItemDetail.razor", "GearPeekDetail.razor" })
            Assert.That(Web("Components", f), Does.Contain("var mainEquip = equipLines.Where(x => x.Label is \"类型\" or \"状态\").ToList();"), f);
    }

    // ───────────── Q651 ─────────────

    /// <summary>Q651-A01：主角刀剑3／同伴1且同伴持刀，技能归属为同伴当前等级／解锁数并与资格一致；转地面按你；真实技能不变。</summary>
    [Test]
    public void Q651_A01_SkillOwnershipByHolder()
    {
        var s = New();
        E.DebugQuickParty(s, 2);
        var mate = s.Party.First(id => id != GameState.PlayerId);
        E.DebugSetSkillLevel(s, "blade", 3);
        s.Actors[mate].Skills["blade"] = new SkillState { Level = 1 };
        var knife = Make(s, "machete", ItemInstance.EquipOwner(mate, EquipSlots.Weapon1));
        string Row() => E.EquipDetailLines(s, knife).Single(x => x.Label == "技能归属").Value;
        Assert.That(Row(), Does.StartWith("刀剑熟练（当前1级，已解锁1个）："));
        Assert.That(E.EquipActiveTraitLines(s, knife), Is.Empty, "资格同为同伴（割伤未解锁）");
        knife.Owner = s.Ground;
        Assert.That(Row(), Does.StartWith("刀剑熟练（当前3级，已解锁"), "地面按你");
        Assert.That(E.SkillLevel(s.Actors[mate], "blade"), Is.EqualTo(1));
        Assert.That(E.SkillLevel(s, "blade"), Is.EqualTo(3));
    }

    // ───────────── Q653 ─────────────

    /// <summary>Q653-A01：可选强敌帮助整句按新稿，无旧「倒计时主动接战」。</summary>
    [Test]
    public void Q653_A01_HelpSentence()
    {
        var h = Web("Services", "HelpCatalog.cs");
        const string now = "必经关卡的固定敌人必须战胜，无法躲避。可选强敌由你决定何时处理，不会主动接战。";
        Assert.That(h, Does.Contain(now).And.Not.Contain("不会倒计时主动接战"));
        Assert.That(Verbatim(), Does.Contain($"| 可选强敌帮助整句 | {now} |"));
    }

    // ───────────── Q654 ─────────────

    /// <summary>Q654-A01：空弹仓、错误站位时只「当前无可用攻击」，不出百分比、不免费装填／移动；合法后同源恢复；风险不读这一行。</summary>
    [Test]
    public void Q654_A01_IllegalBasicAttack()
    {
        var (s, _) = OneShambler();
        var pistol = Arm(s, "pistol");
        pistol.LoadedAmmo = 0;
        var before = SaveSerializer.Serialize(s);
        Assert.That(E.PreBattleHits(s, Card(s)).Single().Legal, Is.False, "空弹仓");
        Assert.That(SaveSerializer.Serialize(s), Is.EqualTo(before), "不免费装填");
        pistol.LoadedAmmo = E.Weapon("pistol")!.MagazineCapacity;
        Assert.That(E.Weapon("pistol")!.OwnPositions, Does.Not.Contain("1"));
        Assert.That(E.PreBattleHits(s, Card(s)).Single().Legal, Is.False, "单人入场在1号位，手枪只能2—4号位用");
        Arm(s, "hatchet");
        var ok = E.PreBattleHits(s, Card(s)).Single();
        Assert.That(ok.Legal, Is.True, "合法后实时恢复");
        Assert.That(ok.HitChance, Is.GreaterThan(0).And.LessThan(1));
        Assert.That(GameEngine.NoAttackHitText, Is.EqualTo("当前无可用攻击"));
        // 右栏与处理盘：不合法只写原句，不出百分比
        var d = Web("Components", "EnemyDetail.razor");
        Assert.That(d, Does.Contain("@if (hit.Legal)").And.Contain("else { <div><span>你的命中率</span><b>@GameEngine.NoAttackHitText</b></div> }"));
        Assert.That(Web("Components", "ActionPops.razor"), Does.Contain("@(hit.Legal ? \"命中\" + Ui.Pct(hit.HitChance) : GameEngine.NoAttackHitText)"));
        // 风险评分仍枚举原合法攻击（不读命中率行）
        var risk = File.ReadAllText(Path.Combine(Root(), "src", "Core", "Engine", "GameEngine.EngageRisk.cs"));
        var score = risk.Substring(risk.IndexOf("private double BestAllyAttackScore(", StringComparison.Ordinal));
        score = score.Substring(0, score.IndexOf("\n        }", StringComparison.Ordinal));
        Assert.That(score, Does.Not.Contain("PreBattleHits"));
    }

    // ───────────── helpers ─────────────

    private GameState At(string node, int capacity)
    {
        var s = New(3001);
        UnlockCapacity(s, capacity);
        s.GameMinute = TestBase_At(2, 9, 0);
        s.SpawnNextCheck = int.MaxValue;
        s.PatrolNextCheck = int.MaxValue;
        E.DebugMapTeleport(s, node);
        return s;
    }

    private static int TestBase_At(int day, int hh, int mm) => (day - 1) * 1440 + hh * 60 + mm;

    private static string Root()
    {
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "AGENTS.md"))) dir = dir.Parent;
        return dir!.FullName;
    }

    private static string Web(params string[] parts) => File.ReadAllText(Path.Combine(new[] { Root(), "web", "Survival.Web" }.Concat(parts).ToArray())).Replace("\r\n", "\n");

    private static string Verbatim() => File.ReadAllText(Directory.GetFiles(Path.Combine(Root(), "doc", "策划案"), "重墨 V2 开工裁定与界面逐字稿.md", SearchOption.AllDirectories).Single());

    private static string R05() => File.ReadAllText(Directory.GetFiles(Path.Combine(Root(), "doc", "策划案"), "R05 敌人卡、种群刷新与遭遇系统.md", SearchOption.AllDirectories).Single());
}
