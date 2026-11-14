using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 BM5b1「敌踪交战风险」（R224）。规则出处：R05「2026-11-14 Q613主控定稿：当前队伍对真实敌踪的即时风险」与验收 Q613-A01—A05；
/// 网页壳的接线以源码检查（同 BM4bRailTests）。
/// </summary>
public class BM5b1RiskTests : B41cTestBase
{
    private EnemyCardView Card(GameState s) => E.EnemyCardsHere(s).Single();

    // ───────────── Q613-A02 分档 ─────────────

    /// <summary>Q613-A02：速度比 1 且非零时，R=0.349999／0.35／0.75／1.35／2.20 分别得到很低／较低／中等／较高／极高；不先四舍五入分档。</summary>
    [Test]
    public void Q613_A02_Thresholds()
    {
        var cases = new (double R, string Text)[] { (0.349999, "交战风险：很低"), (0.35, "交战风险：较低"), (0.75, "交战风险：中等"), (1.35, "交战风险：较高"), (2.20, "交战风险：极高") };
        foreach (var (r, text) in cases)
        {
            // H_A=D_A=1、V_A=V_E=4：R 就是 H_E×D_E
            var f = new EngageRiskFactors { Allies = 1, Enemies = 1, HA = 1, DA = 1, VA = 4, HE = r, DE = 1, VE = 4 };
            Assert.That(GameEngine.EngageRiskIndex(f.HA, f.DA, f.VA, f.HE, f.DE, f.VE), Is.EqualTo(r));
            Assert.That(GameEngine.EngageRiskText(GameEngine.EngageRiskClassify(f)!.Value), Is.EqualTo(text), $"R={r}");
        }
        Assert.That(GameEngine.EngageRiskLevelOf(0.7499999), Is.EqualTo(EngageRiskLevel.Low));
        Assert.That(GameEngine.EngageRiskLevelOf(2.1999999), Is.EqualTo(EngageRiskLevel.High));
        // 速度修正 clamp(sqrt(V_E/V_A),0.75,1.5)，速度最低 1
        Assert.That(GameEngine.EngageRiskIndex(1, 1, 4, 1, 1, 16), Is.EqualTo(1.5), "sqrt(4)=2 → 封顶 1.5");
        Assert.That(GameEngine.EngageRiskIndex(1, 1, 16, 1, 1, 4), Is.EqualTo(0.75), "sqrt(0.25)=0.5 → 托底 0.75");
        Assert.That(GameEngine.EngageRiskIndex(1, 1, 0, 1, 1, 0), Is.EqualTo(1), "速度最低 1");
    }

    // ───────────── Q613-A04 零值与失效 ─────────────

    /// <summary>Q613-A04：H_A 或我方输出为 0 一律极高（双方都 0 也极高）、我方有输出而 D_E=0 为很低，不除 0、不出 NaN。</summary>
    [Test]
    public void Q613_A04_ZeroRules()
    {
        Assert.That(GameEngine.EngageRiskClassify(new EngageRiskFactors { Allies = 1, HA = 0, DA = 3, VA = 4, HE = 10, DE = 1, VE = 4 }), Is.EqualTo(EngageRiskLevel.VeryHigh));
        Assert.That(GameEngine.EngageRiskClassify(new EngageRiskFactors { Allies = 1, HA = 20, DA = 0, VA = 4, HE = 10, DE = 1, VE = 4 }), Is.EqualTo(EngageRiskLevel.VeryHigh));
        Assert.That(GameEngine.EngageRiskClassify(new EngageRiskFactors { Allies = 1, HA = 20, DA = 0, VA = 4, HE = 10, DE = 0, VE = 4 }), Is.EqualTo(EngageRiskLevel.VeryHigh), "双方输出均 0 归极高");
        Assert.That(GameEngine.EngageRiskClassify(new EngageRiskFactors { Allies = 0, HA = 0, DA = 0, VA = 1, HE = 10, DE = 1, VE = 4 }), Is.EqualTo(EngageRiskLevel.VeryHigh), "无有效我方活成员");
        Assert.That(GameEngine.EngageRiskClassify(new EngageRiskFactors { Allies = 1, HA = 20, DA = 2, VA = 4, HE = 10, DE = 0, VE = 4 }), Is.EqualTo(EngageRiskLevel.VeryLow));
    }

    /// <summary>Q613-A04：当前武器页不能直接攻击（信号哨）且不满足应急拳击条件时，我方输出为 0 → 极高；「战斗详情」命中率写「当前无可用攻击」。</summary>
    [Test]
    public void Q613_A04_NoAttackIsVeryHigh()
    {
        var (s, _) = OneShambler();
        Arm(s, "signal_whistle");
        var f = E.EngageRiskFactorsFor(s, Card(s))!;
        Assert.That(f.DA, Is.EqualTo(0));
        Assert.That(E.EngageRiskFor(s, Card(s)).Text, Is.EqualTo("交战风险：极高"));
        Assert.That(E.OutOfBattleHasAttack(s), Is.False);
        Arm(s, "hatchet");
        Assert.That(E.OutOfBattleHasAttack(s), Is.True);
        Assert.That(E.EngageRiskFactorsFor(s, Card(s))!.DA, Is.GreaterThan(0));
    }

    /// <summary>Q613-A04：没有有效敌方活成员不留旧等级（失效句）；必要定义缺失不猜等级（暂无法评估）。</summary>
    [Test]
    public void Q613_A04_InvalidAndUnknown()
    {
        var (s, _) = OneShambler();
        var card = Card(s);
        Assert.That(E.EngageRiskFor(s, card).Invalid, Is.False);
        var w = s.WorldEnemies.Single();
        w.Hp = 0;
        var gone = E.EngageRiskFor(s, card);
        Assert.That(gone.Invalid, Is.True);
        Assert.That(gone.Text, Is.EqualTo("这处敌踪已失效，请重新选择。"));
        w.Hp = E.Enemy(w.EnemyId).Hp;
        w.EnemyId = "no_such_enemy";
        var unknown = E.EngageRiskFor(s, card);
        Assert.That(unknown.Unknown, Is.True);
        Assert.That(unknown.Text, Is.EqualTo("交战风险：暂无法评估"));
    }

    // ───────────── Q613-A03 只读、确定、按当前事实 ─────────────

    /// <summary>Q613-A03：相同快照连续查看／读档评级相同，随机序列、世界分钟、物品、战报不变；不开战、不锁意图。</summary>
    [Test]
    public void Q613_A03_ReadOnlyDeterministic()
    {
        var (s, _) = OneShambler();
        var before = SaveSerializer.Serialize(s);
        var rng = s.Rng.State;
        var r1 = E.EngageRiskFor(s, Card(s));
        var f1 = E.EngageRiskFactorsFor(s, Card(s))!;
        var r2 = E.EngageRiskFor(s, Card(s));
        Assert.That(r1.Unknown || r1.Invalid, Is.False);
        Assert.That(r2.Text, Is.EqualTo(r1.Text));
        Assert.That(s.Rng.State, Is.EqualTo(rng), "不消耗随机序列");
        Assert.That(s.Battle, Is.Null, "不开战");
        Assert.That(SaveSerializer.Serialize(s), Is.EqualTo(before), "世界分钟、物品、AP、战报都不变");
        var loaded = SaveSerializer.Deserialize(before, CfgV2);
        var f2 = E.EngageRiskFactorsFor(loaded, Card(loaded))!;
        Assert.That((f2.HA, f2.DA, f2.VA, f2.HE, f2.DE, f2.VE), Is.EqualTo((f1.HA, f1.DA, f1.VA, f1.HE, f1.DE, f1.VE)), "读档后按同一事实重算");
        Assert.That(E.EngageRiskFor(loaded, Card(loaded)).Text, Is.EqualTo(r1.Text));
    }

    /// <summary>Q613-A03／A04：换武器、健康、体力按新事实重算；H_A 取当前残血（不用满血模板）。</summary>
    [Test]
    public void Q613_A03_RecomputesFromCurrentFacts()
    {
        var (s, _) = OneShambler();
        var fists = E.EngageRiskFactorsFor(s, Card(s))!;
        Assert.That(fists.HA, Is.EqualTo(s.Stats.Health));
        Assert.That(fists.DA, Is.GreaterThan(0), "徒手普通拳击也有直接伤害");
        Assert.That(fists.DE, Is.GreaterThan(0));
        Arm(s, "hatchet");
        var axe = E.EngageRiskFactorsFor(s, Card(s))!;
        Assert.That(axe.DA, Is.Not.EqualTo(fists.DA), "换武器重算");
        s.Stats.Health = 3;
        var hurt = E.EngageRiskFactorsFor(s, Card(s))!;
        Assert.That(hurt.HA, Is.EqualTo(3), "当前残血");
        Assert.That(hurt.DA, Is.EqualTo(axe.DA));
        s.Stats.Energy = 0.05;
        var tired = E.EngageRiskFactorsFor(s, Card(s))!;
        Assert.That(tired.DA, Is.LessThan(hurt.DA), "低体力：付不起体力成本的攻击不算，只剩应急拳击等");
        // 已损坏的武器：耐久不足以执行攻击
        Arm(s, "hatchet").Durability = 0;
        s.Stats.Energy = s.Stats.EnergyMax;
        var broken = E.EngageRiskFactorsFor(s, Card(s))!;
        Assert.That(broken.DA, Is.LessThan(axe.DA));
    }

    /// <summary>Q613-A03：我方只计按已保存编队与原入战资格真正参战的存活成员；容量 1 时同行者不入场、不计。</summary>
    [Test]
    public void Q613_A03_OnlyEnteringMembers()
    {
        var s = Field(2, "shambler");
        E.DebugQuickParty(s, 2);
        E.DebugTeleport(s, "field");
        var mate = s.Party.Single(id => id != GameState.PlayerId);
        var two = E.EngageRiskFactorsFor(s, Card(s))!;
        Assert.That(two.Allies, Is.EqualTo(2));
        Assert.That(two.HA, Is.EqualTo(s.Actors[GameState.PlayerId].Stats.Health + s.Actors[mate].Stats.Health).Within(1e-9));
        foreach (var flag in new[] { "hub_registered", "church_route_record", "academy_safety_record" }) s.Progress.Flags.Remove(flag);
        Assert.That(E.BattleCapacity(s), Is.EqualTo(1));
        var one = E.EngageRiskFactorsFor(s, Card(s))!;
        Assert.That(one.Allies, Is.EqualTo(1), "超出入场容量的同行者不计");
        UnlockCapacity(s, 2);
        s.Party.Remove(mate);
        Assert.That(E.EngageRiskFactorsFor(s, Card(s))!.Allies, Is.EqualTo(1), "不在同行队伍（候补／留屋）不计");
        s.Party.Add(mate);
        s.Actors[mate].Stats.Health = 0;
        Assert.That(E.EngageRiskFactorsFor(s, Card(s))!.Allies, Is.EqualTo(1), "死亡成员不计");
    }

    // ───────────── Q613-A05 不读未来意图 ─────────────

    /// <summary>Q613-A05：评估只用原普通攻击口径，不读、不锁敌方意图；改真实成员 HP 则相应重算。</summary>
    [Test]
    public void Q613_A05_NoIntentRead_HpRecomputes()
    {
        var src = File.ReadAllText(Path.Combine(Root(), "src", "Core", "Engine", "GameEngine.EngageRisk.cs"));
        foreach (var bad in new[] { "StepOf(", "LockIntent(", ".Intent", "BeginRound(", "RollFear(", "StartBattle(", "Rng.Next", "GrantFirstStrike(" })
            Assert.That(src, Does.Not.Contain(bad), bad);
        Assert.That(src, Does.Contain("var st = PlainStep(cfg);"));
        var (s, _) = OneShambler();
        var full = E.EngageRiskFactorsFor(s, Card(s))!;
        s.WorldEnemies.Single().Hp = 1;
        var low = E.EngageRiskFactorsFor(s, Card(s))!;
        Assert.That(low.HE, Is.EqualTo(1));
        Assert.That(low.HE, Is.LessThan(full.HE));
        Assert.That(low.DE, Is.EqualTo(full.DE), "健康不改攻击派生值");
    }

    // ───────────── Q613-A01 壳 ─────────────

    /// <summary>Q613-A01：首屏顺序 身份 → 交战风险（等级＋通用提示）→ 真实状态／倒计时 → 原因；核心数值与命中预估在默认收起的「战斗详情」；会话缓存绑定事实版本。</summary>
    [Test]
    public void Q613_A01_ShellWiring()
    {
        var d = Web("Components", "EnemyDetail.razor");
        var src = d.IndexOf("<div class=\"rail-src\">@card.Badge", StringComparison.Ordinal);
        var risk = d.IndexOf("<div class=\"rail-risk\"", StringComparison.Ordinal);
        var note = d.IndexOf("@GameEngine.EngageRiskNote", StringComparison.Ordinal);
        var status = d.IndexOf("CardApproachStatus(s, card)", StringComparison.Ordinal);
        var why = d.IndexOf("@if (why != null)", StringComparison.Ordinal);
        var fold = d.IndexOf("<RailFold Type=\"@RailTypes.Enemy\" Group=\"@RailGroups.BattleDetail\" Name=\"@RailText.BattleDetail\">", StringComparison.Ordinal);
        Assert.That(src, Is.GreaterThan(0));
        Assert.That(risk, Is.GreaterThan(src));
        Assert.That(note, Is.GreaterThan(risk));
        Assert.That(status, Is.GreaterThan(note));
        Assert.That(why, Is.GreaterThan(status));
        Assert.That(fold, Is.GreaterThan(why));
        Assert.That(d, Does.Contain("var risk = G.EngageRisk(card);"));
        Assert.That(d, Does.Contain("@if (risk.Invalid) { <div class=\"risk\" data-engage-risk=\"invalid\">@GameEngine.EnemyCardInvalidText</div> }"));
        Assert.That(d, Does.Contain("<b>@GameEngine.NoAttackHitText</b>"));
        Assert.That(d, Does.Contain("@if (e.OutOfBattleHasAttack(s))"));
        Assert.That(RailText.BattleDetail, Is.EqualTo("战斗详情"));
        var session = Web("Services", "GameSession.cs");
        Assert.That(session, Does.Contain("if (_riskCache.ver == Version && ReferenceEquals(_riskCache.state, S) && _riskCache.key == card.Key && _riskCache.risk != null) return _riskCache.risk;"));
    }

    /// <summary>正式文字与 R05 Q613 原句逐字一致。</summary>
    [Test]
    public void Q613_TextsVerbatim()
    {
        var r05 = File.ReadAllText(Path.Combine(Root(), "doc", "策划案", "系统策划总册 v1.0", "02 参考扩展", "R05 敌人卡、种群刷新与遭遇系统.md"));
        foreach (var t in new[] { GameEngine.EngageRiskUnknownText, GameEngine.EngageRiskNote, GameEngine.EnemyCardInvalidText, GameEngine.NoAttackHitText, RailText.BattleDetail })
            Assert.That(r05, Does.Contain(t), t);
        foreach (var name in GameEngine.EngageRiskLevelNames)
            Assert.That(r05, Does.Contain($"| 交战风险：{name} |"), name);
    }

    private static string Root()
    {
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "web", "Survival.sln"))) dir = dir.Parent;
        Assert.That(dir, Is.Not.Null, "找到仓库根目录");
        return dir!.FullName;
    }

    private static string Web(params string[] parts) => File.ReadAllText(Path.Combine(new[] { Root(), "web", "Survival.Web" }.Concat(parts).ToArray())).Replace("\r\n", "\n");
}
