using Survival.Core.Config;
using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

public abstract class TestBase
{
    protected static readonly GameConfig Cfg = LoadConfig();

    /// <summary>读发布配置（B41e3 起新版敌踪是唯一流程，没有开关可改）。</summary>
    protected static GameConfig LoadConfig() =>
        ConfigLoader.FromJson(File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "config.v0.6.json")));

    protected GameEngine E = null!;

    [SetUp]
    public void BaseSetUp() => E = new GameEngine(Cfg);

    /// <summary>
    /// 旧系统用例的新局：去掉本轮新剧情与饱饱输入（DebugLegacyStory，同旧档 T06），只验证各自系统。
    /// 开场、首战、饱饱病毒与通关脚本用 NewStory（完整新局）。
    /// </summary>
    protected GameState New(uint seed = 12345)
    {
        var s = E.NewGame(seed);
        E.DebugLegacyStory(s);
        if (ServiceStandIn) s.ServiceMapMigrated = false;
        if (NoEnemyApproach) s.SuppressEnemyApproach = true;
        return s;
    }

    /// <summary>
    /// 到达荒地后像玩家一样主动接近教学卡开首战（B41b R05 Q421：到达只出教学卡、不直接开战；B41e3 起是唯一流程）。
    /// 已在战斗、首战已胜或没有教学卡时什么也不做；返回是否开了首战。
    /// </summary>
    protected bool ApproachTutorial(GameState s)
    {
        if (s.Battle != null || s.Story.TutorialWon || s.Story.TutorialEnemyId is not int tid) return false;
        var key = GameEngine.SingleCardKey(tid);
        if (E.EnemyCardHere(s, key) == null) return false;
        var r = E.ApproachCard(s, key);
        Assert.That(r.Ok, Is.True, r.Reason);
        return true;
    }

    /// <summary>
    /// 与敌踪无关的旧系统用例（采集、清理、地图、成长等）写于新版敌踪上线前，那时没有“到期敌踪主动开战”。B41e3 翻开开关后，
    /// 这些用例的 New() 冻结敌踪接近（测试专用、不存档的 SuppressEnemyApproach，B41a 起已有），只验证各自系统；敌踪接近本身见 B41b 用例。
    /// </summary>
    protected virtual bool NoEnemyApproach => false;

    /// <summary>
    /// 队伍服务的旧用例（B8—B8d）按“迁址前”口径测服务规则本身：营区与收容所服务都在替身临时营地（camp）。Q119 选 B（B13a）起
    /// 新局直接用 h02_square / h06_shelter，这些用例的 New() 把迁址标记退回 false（等同迁址前的旧档）；新址规则见 B13aQTests。
    /// </summary>
    protected virtual bool ServiceStandIn => false;

    /// <summary>完整新局（系统策划总册 v1.0：饱饱在体内、开场对白、首战门控）。</summary>
    protected GameState NewStory(uint seed = 12345) => E.NewGame(seed);

    /// <summary>第 day 日 hh:mm 的绝对分钟。</summary>
    protected static int At(int day, int hh, int mm) => (day - 1) * 1440 + hh * 60 + mm;

    protected void Start(GameState s, ActionRequest r)
    {
        var res = E.Start(s, r);
        Assert.That(res.Ok, Is.True, $"开始行动失败：{res.Reason}");
    }

    /// <summary>推进直到当前（主或子）行动结束、暂停或死亡；autoResume 时阈值暂停自动继续。</summary>
    protected SegmentStatus Run(GameState s, bool autoResume = true, int max = 3000)
    {
        var last = SegmentStatus.Idle;
        for (var i = 0; i < max; i++)
        {
            var r = E.Advance(s);
            last = r.Status;
            if (r.Status == SegmentStatus.Paused)
            {
                if (!autoResume || s.Main?.PauseReason == PauseReason.FireOut) return last;
                E.Resume(s);
                continue;
            }
            if (r.Status != SegmentStatus.Running) return last;
        }
        Assert.Fail("行动未在限定段数内结束");
        return last;
    }

    /// <summary>像玩家一样把当前所有待读对话逐句“继续/结束对话”读完（不耗时；Q10 读完才能操作）。</summary>
    protected void ReadDialogues(GameState s)
    {
        for (var k = 0; k < 64 && E.DialogueLocked(s); k++)
        {
            var line = E.CurrentDialogueLine(s);
            Assert.That(line, Is.Not.Null, "输入锁下必须有可确认的一句");
            Assert.That(E.AckDialogue(s, line!.LineId).Ok, Is.True);
        }
        Assert.That(E.DialogueLocked(s), Is.False);
    }

    protected void Steps(GameState s, int n)
    {
        for (var i = 0; i < n; i++) E.Advance(s);
    }

    protected void SetStats(GameState s, double h, double n, double w, double e, double sl, double m) =>
        E.DebugSetStats(s, h, n, w, e, sl, m);

    protected int Count(GameState s, string itemId, string? owner = null) =>
        s.Items.Where(i => i.ItemId == itemId && (owner == null || i.Owner == owner)).Sum(i => i.Count);

    protected ItemInstance First(GameState s, string itemId, string? owner = null) =>
        s.Items.First(i => i.ItemId == itemId && (owner == null || i.Owner == owner));

    /// <summary>空闲时用等待推进世界时间到指定分钟（每段都会结算清理次数等世界状态），多余的等待在边界取消。</summary>
    protected void AdvanceTo(GameState s, int minute)
    {
        while (s.GameMinute < minute)
        {
            StopIfBattle(s);
            if (s.Main == null) Start(s, ActionRequest.Wait(30));
            else if (s.Main.Status == ActionStatus.Paused) E.Resume(s);
            E.Advance(s);
        }
        if (s.Main?.Type == ActionType.Wait) E.Cancel(s);
    }

    /// <summary>
    /// 按世界时间推进的循环里调用：到期敌踪会主动开战（B41b），推进停在战斗上不再前进——直接判失败而不是空转。
    /// </summary>
    protected static void StopIfBattle(GameState s)
    {
        if (s.Battle != null) Assert.Fail("推进途中进入了战斗（新版敌踪：到期敌踪主动开战），时间不再前进");
        if (s.Progress.Dead) Assert.Fail("推进途中角色已死亡，时间不再前进");
    }

    /// <summary>覆盖当前主行动已锁定的随机结果（itemId 为 null 表示空结果），用于验收表里的“强制抽中”。</summary>
    protected static void ForceLoot(GameState s, string? itemId)
    {
        s.Main!.LootItemId = itemId;
        s.Main.LootCount = itemId == null ? 0 : 1;
    }
}
