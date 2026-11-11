using Newtonsoft.Json.Linq;
using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v1.0 B40a：多存档内核与存储（需求 R134、R143、R149）。规则出处：S01「2026-11-10 Q335 多存档定稿」
/// （验收 Q335-A01—A03）、「B40 Q381–Q389 实装裁定」（Q381-A01—Q389-A01）、「B40 Q403–Q411 第二轮定稿」（Q403-A01—Q411-A01）。
/// 存储用内存实现（可模拟配额用尽、写失败、读失败、读回不一致）；现实时间由用例传入。
/// </summary>
public class B40aSaveSlotsTests : TestBase
{
    private const long T0 = 1_700_000_000_000;
    private const long Sec = 1000;

    /// <summary>内存存储：可按键注入配额用尽、其他写失败、读失败与读回篡改，并记每个键的写入次数。</summary>
    internal sealed class MemStorage : ISaveStorage
    {
        public Dictionary<string, string> D = new();
        public readonly Dictionary<string, int> Writes = new();
        public Func<string, bool>? QuotaOn;
        public Func<string, bool>? FailOn;
        public Func<string, bool>? ReadFailOn;
        public Func<string, string, string>? Mangle;

        public string? Get(string key)
        {
            if (ReadFailOn?.Invoke(key) == true) throw new SaveStorageReadException("SecurityError: denied");
            if (!D.TryGetValue(key, out var v)) return null;
            return Mangle != null ? Mangle(key, v) : v;
        }

        public void Set(string key, string value)
        {
            if (QuotaOn?.Invoke(key) == true) throw new SaveStorageWriteException("QuotaExceededError", "exceeded the quota");
            if (FailOn?.Invoke(key) == true) throw new SaveStorageWriteException("SecurityError", "boom");
            D[key] = value;
            Writes[key] = W(key) + 1;
        }

        public void Remove(string key) => D.Remove(key);

        public int W(string key) => Writes.TryGetValue(key, out var n) ? n : 0;
    }

    private int _rev;
    private string NextRev() => "r" + (++_rev);

    private SaveCoordinator Page(MemStorage st, string token) => new(st, E, token, NextRev);

    private static int MinuteOf(MemStorage st, string key) =>
        SaveSerializer.Deserialize(SaveSlotEnvelope.Parse(st.D[key]).State).GameMinute;

    private LegacyImportResult Import(MemStorage st) =>
        LegacySaveImport.Run(st, Cfg, E.SaveSummaryOf, E.SaveLocationName, NextRev);

    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "Fixtures", $"save-{name}.json"));

    // ───────────── Q335 定时与手动 ─────────────

    /// <summary>
    /// Q335-A01：暂停和正常推进分别跨过 300 现实秒，各只写现实槽一次；挂起错过多个间隔只补一次，下一到期点沿进入本局起的时间轴。
    /// </summary>
    [Test]
    public void Q335_A01_Auto5_Every300RealSeconds_OncePerDue()
    {
        var st = new MemStorage();
        var c = Page(st, "A");
        var s = New();
        Assert.That(c.StartNewGame(s, T0).Ok, Is.True, "新局初始快照写每5分钟槽");
        Assert.That(st.W(SaveSlots.Auto5Key), Is.EqualTo(1));

        Assert.That(c.Tick(c.SessionId, T0 + 299 * Sec), Is.False);
        c.Pump(s, T0 + 299 * Sec);
        Assert.That(st.W(SaveSlots.Auto5Key), Is.EqualTo(1), "未到 300 秒不写");

        // 暂停中（主行动暂停）跨过 300 秒
        s.Main = new ActionState { Type = ActionType.Rest, Status = ActionStatus.Paused };
        Assert.That(c.Tick(c.SessionId, T0 + 300 * Sec), Is.True);
        c.Pump(s, T0 + 300 * Sec);
        c.Tick(c.SessionId, T0 + 301 * Sec);
        c.Pump(s, T0 + 301 * Sec);
        Assert.That(st.W(SaveSlots.Auto5Key), Is.EqualTo(2), "暂停时跨过一次只写一次");

        // 正常推进中跨过 600 秒：到期后在下一个已提交边界写入
        s.Main = new ActionState { Type = ActionType.Rest, Status = ActionStatus.Running };
        c.Tick(c.SessionId, T0 + 600 * Sec);
        s.GameMinute += 5;
        c.Commit(s, T0 + 601 * Sec);
        c.Commit(s, T0 + 602 * Sec);
        Assert.That(st.W(SaveSlots.Auto5Key), Is.EqualTo(3), "正常推进跨过一次只写一次");
        Assert.That(MinuteOf(st, SaveSlots.Auto5Key), Is.EqualTo(s.GameMinute), "写的是到期后第一个边界的最新快照");

        // 休眠：错过 900、1200、1500 只补一次，下一到期点 1800
        c.Tick(c.SessionId, T0 + 1650 * Sec);
        c.Pump(s, T0 + 1650 * Sec);
        Assert.That(st.W(SaveSlots.Auto5Key), Is.EqualTo(4));
        Assert.That(c.NextAutoDueMs, Is.EqualTo(T0 + 1800 * Sec));
        Assert.That(st.W(SaveSlots.DailyKey), Is.EqualTo(0), "现实计时不写每日槽");
    }

    /// <summary>Q335：段前时间小于 06:00 且段后达到或超过 06:00 才算跨过。</summary>
    [TestCase(355, 360, true)]
    [TestCase(355, 365, true)]
    [TestCase(360, 365, false)]
    [TestCase(300, 355, false)]
    [TestCase(1435, 1800, true)]
    [TestCase(1000, 1795, false)]
    [TestCase(1000, 1800, true)]
    [TestCase(355, 355, false)]
    public void Q335_DailyCrossing(int before, int after, bool crosses) =>
        Assert.That(SaveCoordinator.CrossesDaily(before, after), Is.EqualTo(crosses));

    /// <summary>
    /// Q335-A01：正常推进跨过 06:00 单写每日槽（绑定跨过的那一段快照）；两个触发同一边界各写各槽；读档、旧档迁移不补存。
    /// </summary>
    [Test]
    public void Q335_A01_Daily_OnCrossing_BothTriggersSameBoundary_NoBackfill()
    {
        var st = new MemStorage();
        var c = Page(st, "A");
        var s = New();
        s.GameMinute = At(2, 5, 50);
        c.StartNewGame(s, T0);
        s.GameMinute = At(2, 5, 55);
        c.Commit(s, T0 + 10 * Sec);
        Assert.That(st.W(SaveSlots.DailyKey), Is.EqualTo(0));
        s.GameMinute = At(2, 6, 0);
        c.Commit(s, T0 + 20 * Sec);
        Assert.That(st.W(SaveSlots.DailyKey), Is.EqualTo(1));
        Assert.That(MinuteOf(st, SaveSlots.DailyKey), Is.EqualTo(At(2, 6, 0)));
        Assert.That(st.W(SaveSlots.Auto5Key), Is.EqualTo(1), "每日跨越不写现实槽");
        s.GameMinute = At(2, 6, 5);
        c.Commit(s, T0 + 30 * Sec);
        Assert.That(st.W(SaveSlots.DailyKey), Is.EqualTo(1), "同日不再写");

        // 同一边界：现实槽到期 + 跨过第 3 日 06:00
        c.Tick(c.SessionId, T0 + 300 * Sec);
        s.GameMinute = At(3, 6, 0);
        c.Commit(s, T0 + 300 * Sec);
        Assert.That(st.W(SaveSlots.Auto5Key), Is.EqualTo(2));
        Assert.That(st.W(SaveSlots.DailyKey), Is.EqualTo(2));
        Assert.That(MinuteOf(st, SaveSlots.DailyKey), Is.EqualTo(At(3, 6, 0)));

        // 读一份 05:00 的档：读入不补存；读后实际再次跨过才写新快照
        var c2 = Page(st, "A");
        var old = New();
        old.GameMinute = At(3, 5, 0);
        c2.StartLoaded(old, T0 + 400 * Sec);
        c2.Tick(c2.SessionId, T0 + 400 * Sec);
        c2.Pump(old, T0 + 400 * Sec);
        Assert.That(st.W(SaveSlots.DailyKey), Is.EqualTo(2), "读入不补存");
        old.GameMinute = At(3, 6, 0);
        c2.Commit(old, T0 + 410 * Sec);
        Assert.That(st.W(SaveSlots.DailyKey), Is.EqualTo(3));
    }

    /// <summary>
    /// Q335-A02：创建与覆盖手动档；安全锁内不可手动保存；只列已存在的手动档、满 10 份只能明确覆盖；读历史手动档恢复正确状态，其他手动档仍在。
    /// </summary>
    [Test]
    public void Q335_A02_Manual_CreateOverwrite_FullAndLoad()
    {
        var st = new MemStorage();
        var c = Page(st, "A");
        var s = New();
        c.StartNewGame(s, T0);
        Assert.That(c.ManualSlots(), Is.Empty, "只列已有档");
        var start = s.GameMinute;
        for (var i = 0; i < SaveSlots.ManualCap; i++)
        {
            s.GameMinute = start + i * 5;
            c.Commit(s, T0 + i * Sec);
            Assert.That(c.SaveManual(s, null, T0 + i * Sec).Ok, Is.True);
        }
        Assert.That(c.ManualSlots(), Has.Count.EqualTo(10));
        Assert.That(c.ManualFull, Is.True);
        var full = c.SaveManual(s, null, T0 + 20 * Sec);
        Assert.That(full.Status, Is.EqualTo(SaveWriteStatus.Blocked));
        Assert.That(full.BlockedText, Is.EqualTo("手动存档已满。请选择一份覆盖。"));

        var list = c.ManualSlots();
        Assert.That(list.Select(e => e.SavedAtMs), Is.Ordered.Descending, "从新到旧");
        Assert.That(list.All(e => e.TypeText == "手动存档"), Is.True, "类型不带序号");
        var oldest = list.Last();
        Assert.That(oldest.Minute, Is.EqualTo(start));

        // 安全锁：行动进行中不可保存，档不变
        s.Main = new ActionState { Type = ActionType.Rest, Status = ActionStatus.Running };
        var before = st.D[oldest.Key];
        var blocked = c.SaveManual(s, oldest.Key, T0 + 30 * Sec);
        Assert.That(blocked.BlockedText, Is.EqualTo("行动进行中，不能手动存档。"));
        Assert.That(st.D[oldest.Key], Is.EqualTo(before));
        s.Main = null;

        // 明确覆盖最旧一份
        s.GameMinute = start + 500;
        Assert.That(c.SaveManual(s, oldest.Key, T0 + 40 * Sec).Ok, Is.True);
        Assert.That(MinuteOf(st, oldest.Key), Is.EqualTo(start + 500));
        Assert.That(c.ManualSlots(), Has.Count.EqualTo(10));

        // 读历史手动档（第 2 份）恢复其世界状态，其他手动档仍在
        var second = c.ManualSlots().Single(e => e.Minute == start + 5);
        var load = SaveCatalog.Load(st, second.Key, Cfg);
        Assert.That(load.Ok, Is.True);
        Assert.That(load.State!.GameMinute, Is.EqualTo(start + 5));
        Assert.That(SaveCatalog.ManualEntries(st, Cfg), Has.Count.EqualTo(10));
    }

    /// <summary>
    /// Q335-A03 / Q387-A01 / Q406：配额用尽只影响目标槽，旧成功内容仍可读；失败句前标槽名；重试写原槽与对应快照；非配额失败也带槽名。
    /// </summary>
    [Test]
    public void Q335_A03_FailureOnlyTargetSlot_RetrySameSnapshot()
    {
        var st = new MemStorage();
        var c = Page(st, "A");
        var s = New();
        s.GameMinute = At(1, 5, 55);
        c.StartNewGame(s, T0);
        st.QuotaOn = k => k == SaveSlots.DailyKey;
        c.Tick(c.SessionId, T0 + 300 * Sec);
        s.GameMinute = At(1, 6, 0);
        c.Commit(s, T0 + 300 * Sec);
        Assert.That(MinuteOf(st, SaveSlots.Auto5Key), Is.EqualTo(At(1, 6, 0)), "现实槽照常成功");
        Assert.That(c.AutoFailures.Select(f => f.Text), Is.EqualTo(new[] { "自动存档 · 每日06:00：存档失败：浏览器存储空间已满。" }));
        Assert.That(c.LastSavedAtMs, Is.EqualTo(T0 + 300 * Sec));

        s.GameMinute = At(1, 7, 0);
        c.Commit(s, T0 + 310 * Sec);
        Assert.That(c.Retry(SaveSlotKind.Daily, T0 + 320 * Sec).Ok, Is.False, "配额仍满保持失败");
        Assert.That(c.AutoFailures, Has.Count.EqualTo(1));
        st.QuotaOn = null;
        Assert.That(c.Retry(SaveSlotKind.Daily, T0 + 330 * Sec).Ok, Is.True);
        Assert.That(MinuteOf(st, SaveSlots.DailyKey), Is.EqualTo(At(1, 6, 0)), "重试写的是跨过 06:00 那一段的快照");
        Assert.That(c.AutoFailures, Is.Empty);

        st.FailOn = k => k == SaveSlots.Auto5Key;
        c.Tick(c.SessionId, T0 + 600 * Sec);
        c.Pump(s, T0 + 600 * Sec);
        Assert.That(c.AutoFailures.Single().Text, Is.EqualTo("自动存档 · 每5分钟：保存失败：boom"));
        Assert.That(MinuteOf(st, SaveSlots.Auto5Key), Is.EqualTo(At(1, 6, 0)), "失败保留原槽");
    }

    // ───────────── Q381 / Q408 多页面写权限 ─────────────

    /// <summary>
    /// Q381-A01：第二页进入本局后，第一页自动、手动、保存返回与地牢重置写入均不能写；无新快照时可接回；任一槽更新后拒绝接回（拒绝句常驻、隐藏接回键）；
    /// 失权时旧页面的写入不会覆盖新页面的档。
    /// </summary>
    [Test]
    public void Q381_A01_SecondPageTakesOver_ReclaimRules()
    {
        var st = new MemStorage();
        var a = Page(st, "A");
        var sa = New();
        a.StartNewGame(sa, T0);
        var b = Page(st, "B");
        var sb = SaveCatalog.Load(st, SaveSlots.Auto5Key, Cfg).State!;
        b.StartLoaded(sb, T0 + 100 * Sec);
        var auto5 = st.D[SaveSlots.Auto5Key];

        a.Tick(a.SessionId, T0 + 300 * Sec);
        a.Pump(sa, T0 + 300 * Sec);
        Assert.That(a.Permission, Is.EqualTo(SavePermission.Lost));
        Assert.That(a.PermissionNotice, Is.EqualTo("另一个游戏页面正在保存。"));
        Assert.That(a.CanReclaim, Is.True);
        Assert.That(st.D[SaveSlots.Auto5Key], Is.EqualTo(auto5), "自动不写");
        Assert.That(a.SaveManual(sa, null, T0 + 301 * Sec).BlockedText, Is.EqualTo("另一个游戏页面正在保存。"), "手动不写，写权限句优先");
        Assert.That(a.ManualSlots(), Is.Empty);
        Assert.That(a.SaveAndExit(sa, T0 + 302 * Sec).Status, Is.EqualTo(SaveWriteStatus.Blocked), "保存返回不写");
        Assert.That(a.PersistReset(sa, T0 + 303 * Sec), Is.False, "地牢重置不写");
        Assert.That(a.InSession, Is.True, "保存返回没执行就不离开");
        Assert.That(st.D[SaveSlots.Auto5Key], Is.EqualTo(auto5));

        // 第二页没写过任何槽：第一页可接回，第二页随之失权
        Assert.That(a.Reclaim(T0 + 310 * Sec), Is.EqualTo(SavePermission.Holding));
        Assert.That(a.PermissionNotice, Is.Null);
        a.Pump(sa, T0 + 310 * Sec);
        Assert.That(st.W(SaveSlots.Auto5Key), Is.EqualTo(2), "接回后恢复原到期写入");
        Assert.That(b.CheckPermission(), Is.EqualTo(SavePermission.Lost));

        // 第一页写过更新的快照：第二页拒绝接回，拒绝句常驻、接回键隐藏，竞态下也写不进去
        Assert.That(b.Reclaim(T0 + 320 * Sec), Is.EqualTo(SavePermission.Refused));
        Assert.That(b.PermissionNotice, Is.EqualTo("另一个页面已保存了更新的进度。请返回标题重新读取。"));
        Assert.That(b.CanReclaim, Is.False);
        var now = st.D[SaveSlots.Auto5Key];
        b.Tick(b.SessionId, T0 + 600 * Sec);
        b.Pump(sb, T0 + 600 * Sec);
        Assert.That(st.D[SaveSlots.Auto5Key], Is.EqualTo(now));
        Assert.That(b.RetryBlockedText, Is.EqualTo("另一个游戏页面正在保存。"));
    }

    /// <summary>
    /// Q408-A01：失权前待写的每日快照及失败任务保留，接回后按原快照恢复；第 200 秒失权、第 230 秒接回，下一周期仍在第 300 秒；
    /// 第 950 秒才接回只请求一次最新现实快照，下一周期在第 1200 秒；失权时重试不可执行。
    /// </summary>
    [Test]
    public void Q408_A01_ReclaimRestoresTasksAndTimeline()
    {
        var st = new MemStorage();
        var a = Page(st, "A");
        var s = New();
        s.GameMinute = At(1, 5, 55);
        a.StartNewGame(s, T0);

        // 第 200 秒失权（另一页读档进入，未写盘）
        Page(st, "B").StartLoaded(New(), T0 + 200 * Sec);
        s.GameMinute = At(1, 6, 0);
        a.Commit(s, T0 + 210 * Sec);
        Assert.That(a.Permission, Is.EqualTo(SavePermission.Lost));
        Assert.That(st.W(SaveSlots.DailyKey), Is.EqualTo(0), "失权期间不写，每日快照待写");
        Assert.That(a.Reclaim(T0 + 230 * Sec), Is.EqualTo(SavePermission.Holding));
        Assert.That(a.NextAutoDueMs, Is.EqualTo(T0 + 300 * Sec), "接回不重算周期");
        s.GameMinute = At(1, 6, 30);
        a.Pump(s, T0 + 231 * Sec);
        Assert.That(MinuteOf(st, SaveSlots.DailyKey), Is.EqualTo(At(1, 6, 0)), "仍绑定原跨过 06:00 的快照");

        // 失败任务保留：每5分钟槽配额失败后失权，失权期间重试不可执行；接回后恢复原待重试项
        st.QuotaOn = k => k == SaveSlots.Auto5Key;
        a.Tick(a.SessionId, T0 + 300 * Sec);
        a.Pump(s, T0 + 300 * Sec);
        Assert.That(a.AutoFailures, Has.Count.EqualTo(1));
        st.QuotaOn = null;
        Page(st, "B").StartLoaded(New(), T0 + 320 * Sec);
        Assert.That(a.CheckPermission(), Is.EqualTo(SavePermission.Lost));
        var retry = a.Retry(SaveSlotKind.Auto5, T0 + 330 * Sec);
        Assert.That(retry.Status, Is.EqualTo(SaveWriteStatus.Blocked));
        Assert.That(retry.BlockedText, Is.EqualTo("另一个游戏页面正在保存。"));
        Assert.That(a.AutoFailures, Has.Count.EqualTo(1), "失败项保留");

        // 第 950 秒才接回：错过 600、900 只请求一次，下一周期 1200
        a.Tick(a.SessionId, T0 + 700 * Sec);
        Assert.That(a.Reclaim(T0 + 950 * Sec), Is.EqualTo(SavePermission.Holding));
        Assert.That(a.NextAutoDueMs, Is.EqualTo(T0 + 1200 * Sec));
        var w = st.W(SaveSlots.Auto5Key);
        a.Pump(s, T0 + 950 * Sec);
        a.Pump(s, T0 + 951 * Sec);
        Assert.That(st.W(SaveSlots.Auto5Key), Is.EqualTo(w + 1), "只写一次最新现实快照");
        Assert.That(a.AutoFailures, Is.Empty, "同槽较新的写入成功替换旧失败项");
    }

    // ───────────── Q382 终局 ─────────────

    /// <summary>
    /// Q382-A01：主角终局立即写现实槽一次（含结算状态），终局段不写每日槽；之后与读取死档再等待超过 300 秒，两类自动槽都不写；
    /// 终局写失败可在结算卡重试，离开或确认重开后旧终局任务不再写入（Q409-A01）。
    /// </summary>
    [Test]
    public void Q382_A01_TerminalWrite_StopsAutoSaves()
    {
        var st = new MemStorage();
        var c = Page(st, "A");
        var s = New();
        s.GameMinute = At(1, 5, 55);
        c.StartNewGame(s, T0);
        c.Tick(c.SessionId, T0 + 300 * Sec);
        s.GameMinute = At(1, 6, 0);
        s.Progress.Dead = true;
        c.Commit(s, T0 + 300 * Sec);
        Assert.That(st.W(SaveSlots.Auto5Key), Is.EqualTo(2), "终局立即写一次（旧到期任务不另写）");
        Assert.That(st.W(SaveSlots.DailyKey), Is.EqualTo(0), "终局段不另触发每日档");
        Assert.That(SaveSlotEnvelope.Parse(st.D[SaveSlots.Auto5Key]).Summary!.Ended, Is.True);
        Assert.That(c.Ended, Is.True);
        Assert.That(c.HasUnsavedProgress, Is.False);
        c.Tick(c.SessionId, T0 + 900 * Sec);
        s.GameMinute = At(2, 6, 0);
        c.Commit(s, T0 + 900 * Sec);
        Assert.That(st.W(SaveSlots.Auto5Key), Is.EqualTo(2));
        Assert.That(st.W(SaveSlots.DailyKey), Is.EqualTo(0));

        // 读取死档：既不启动定时器也不覆盖任何槽
        var dead = SaveCatalog.Load(st, SaveSlots.Auto5Key, Cfg);
        var c2 = Page(st, "A");
        c2.StartLoaded(dead.State!, T0 + 1000 * Sec);
        Assert.That(c2.Tick(c2.SessionId, T0 + 1400 * Sec), Is.False);
        c2.Pump(dead.State!, T0 + 1400 * Sec);
        Assert.That(st.W(SaveSlots.Auto5Key), Is.EqualTo(2));

        // 终局写失败：结算卡重试；返回标题后作废
        var st2 = new MemStorage();
        var c3 = Page(st2, "A");
        var s3 = New();
        c3.StartNewGame(s3, T0);
        st2.QuotaOn = _ => true;
        s3.Progress.Dead = true;
        c3.Commit(s3, T0 + 10 * Sec);
        Assert.That(c3.TerminalFailure!.Text, Is.EqualTo("自动存档 · 每5分钟：存档失败：浏览器存储空间已满。"));
        Assert.That(c3.AutoFailures, Is.Empty, "终局失败在结算卡，不进底栏待重试");
        Assert.That(c3.RetryTerminal(T0 + 20 * Sec).Ok, Is.False);
        st2.QuotaOn = null;
        Assert.That(c3.RetryTerminal(T0 + 30 * Sec).Ok, Is.True);
        Assert.That(SaveSlotEnvelope.Parse(st2.D[SaveSlots.Auto5Key]).Summary!.Ended, Is.True);

        var st3 = new MemStorage();
        var c4 = Page(st3, "A");
        var s4 = New();
        c4.StartNewGame(s4, T0);
        var initial = st3.D[SaveSlots.Auto5Key];
        st3.QuotaOn = _ => true;
        s4.Progress.Dead = true;
        c4.Commit(s4, T0 + 10 * Sec);
        st3.QuotaOn = null;
        c4.EndSession();   // 结算页「返回标题」
        Assert.That(c4.RetryTerminal(T0 + 20 * Sec).Ok, Is.False);
        c4.Pump(s4, T0 + 20 * Sec);
        Assert.That(st3.D[SaveSlots.Auto5Key], Is.EqualTo(initial), "旧终局任务不得写入");
    }

    // ───────────── Q383 / Q403 / Q404 / Q410 旧单槽导入与不可读档 ─────────────

    /// <summary>
    /// Q383-A01 / Q410-A01：合法旧档（版本 4）验证后导入一次、清旧键与备份，现实时刻「保存时间未记录」；
    /// 第一次成功读取提示一句「旧存档已升级到版本{当前存档版本}。」，重复读取不再提示；重复打开不重导。
    /// </summary>
    [Test]
    public void Q383_A01_Q410_A01_LegacyImportOnce_NoticeOnce()
    {
        var st = new MemStorage();
        st.D[SaveSlots.LegacyKey] = Fixture("4-midgame");
        st.D[SaveSlots.LegacyBackupKey] = "old backup";
        var r = Import(st);
        Assert.That(r.Imported, Is.True, r.Reason);
        Assert.That(st.D.ContainsKey(SaveSlots.LegacyKey), Is.False);
        Assert.That(st.D.ContainsKey(SaveSlots.LegacyBackupKey), Is.False);
        var view = SaveCatalog.List(st, Cfg, r);
        var card = view.Auto.Single();
        Assert.That(card.Kind, Is.EqualTo(SaveSlotKind.Auto5));
        Assert.That(card.Readable, Is.True);
        Assert.That(card.SavedAtKnown && card.SavedAtMs == null, Is.True, "保存时间未记录");
        Assert.That(st.W(SaveSlots.DailyKey), Is.EqualTo(0), "不补历史 06:00");

        var fail = new MemStorage { ReadFailOn = k => k == SaveSlots.Auto5Key };
        fail.D = st.D;
        Assert.That(SaveCatalog.Load(fail, SaveSlots.Auto5Key, Cfg).Ok, Is.False);
        Assert.That(st.D.ContainsKey(SaveSlots.UpgradeNoticeKey), Is.True, "读取失败不消耗一次性提示");
        var first = SaveCatalog.Load(st, SaveSlots.Auto5Key, Cfg);
        Assert.That(first.UpgradeNotice, Is.EqualTo($"旧存档已升级到版本{GameState.CurrentSchema}。"));
        Assert.That(SaveCatalog.Load(st, SaveSlots.Auto5Key, Cfg).UpgradeNotice, Is.Null, "重复读取不再提示");

        // 重复打开：旧键已清，不重导；旧键删不掉残留时也不重导
        var w = st.W(SaveSlots.Auto5Key);
        Assert.That(Import(st).Imported, Is.False);
        st.D[SaveSlots.LegacyKey] = Fixture("4-midgame");
        var again = Import(st);
        Assert.That(again.Imported || again.FailedCard != null, Is.False, "已导入的同一份旧档不重导、不出旧版卡");
        Assert.That(st.D.ContainsKey(SaveSlots.LegacyKey), Is.False);
        Assert.That(st.W(SaveSlots.Auto5Key), Is.EqualTo(w));
    }

    /// <summary>Q410-A01：原版本 42 的旧档导入后读取静默；原本就是当前版本的也无提示。</summary>
    [Test]
    public void Q410_A01_Schema42AndCurrent_Silent()
    {
        var s = New();
        var json = SaveSerializer.Serialize(s);
        var st = new MemStorage();
        st.D[SaveSlots.LegacyKey] = json;
        Assert.That(Import(st).Imported, Is.True);
        Assert.That(SaveCatalog.Load(st, SaveSlots.Auto5Key, Cfg).UpgradeNotice, Is.Null, "当前版本无提示");

        var o = JObject.Parse(json);
        o["SchemaVersion"] = SaveMigrator.Schema42;
        var st42 = new MemStorage();
        st42.D[SaveSlots.LegacyKey] = o.ToString(Newtonsoft.Json.Formatting.None);
        var r = Import(st42);
        Assert.That(r.Imported, Is.True, r.Reason);
        var load = SaveCatalog.Load(st42, SaveSlots.Auto5Key, Cfg);
        Assert.That(load.Ok, Is.True);
        Assert.That(load.UpgradeNotice, Is.Null, "原版本 42 静默");
    }

    /// <summary>
    /// Q383-A01 / Q403-A01：导入写入失败（配额、其他）、读回不一致、目标槽已有快照、读不出旧档时旧键原样保留，
    /// 「旧版存档」卡排在自动区最后，原因按 Q403 表，「读取存档」禁用（不可读）。
    /// </summary>
    [Test]
    public void Q383_A01_Q403_A01_ImportFailures_KeepLegacy()
    {
        var legacy = Fixture("4-midgame");
        void Check(MemStorage st, string reason)
        {
            st.D[SaveSlots.LegacyKey] = legacy;
            st.D[SaveSlots.LegacyBackupKey] = "bak";
            var r = Import(st);
            Assert.That(r.Imported, Is.False);
            Assert.That(st.D[SaveSlots.LegacyKey], Is.EqualTo(legacy), "旧主键原样");
            Assert.That(st.D[SaveSlots.LegacyBackupKey], Is.EqualTo("bak"), "备份原样");
            var view = SaveCatalog.List(st, Cfg, r);
            var card = view.Auto.Last();
            Assert.That(card.Kind, Is.EqualTo(SaveSlotKind.Legacy));
            Assert.That(card.TypeText, Is.EqualTo("旧版存档"));
            Assert.That(card.Readable, Is.False);
            Assert.That(card.ProblemText, Is.EqualTo("这份旧存档无法升级：" + reason));
            Assert.That(card.SavedAtKnown && card.SavedAtMs == null, Is.True, "Q404：保存时间未记录");
            Assert.That(card.Minute, Is.Not.Null, "能读出的字段照常显示");
        }

        Check(new MemStorage { QuotaOn = k => k == SaveSlots.Auto5Key }, "浏览器存储空间已满。");
        Check(new MemStorage { FailOn = k => k == SaveSlots.Auto5Key }, "浏览器未能写入存档，请重试。");
        Check(new MemStorage { Mangle = (k, v) => k == SaveSlots.Auto5Key ? v + " " : v }, "写入后读回的内容不一致。");

        var existing = new MemStorage();
        var c = Page(existing, "A");
        c.StartNewGame(New(), T0);
        Check(existing, "每5分钟自动存档中已有较新的进度。");
        var view = SaveCatalog.List(existing, Cfg, Import(existing));
        Assert.That(view.Auto.Select(e => e.Kind), Is.EqualTo(new[] { SaveSlotKind.Auto5, SaveSlotKind.Legacy }), "旧版卡在自动区最后");

        var unread = new MemStorage { ReadFailOn = k => k == SaveSlots.LegacyKey };
        unread.D[SaveSlots.LegacyKey] = legacy;
        var ur = Import(unread);
        Assert.That(ur.FailedCard!.ProblemText, Is.EqualTo("这份旧存档无法升级：浏览器暂时无法读取存档，请稍后重试。"));
        Assert.That(unread.D.ContainsKey(SaveSlots.LegacyKey), Is.True);
    }

    /// <summary>
    /// Q403-A01：版本过新、已识别不支持、无法识别、内容损坏、迁移冲突分别给原句；冲突明细不带内部实例编号，物品名用显示名。
    /// </summary>
    [Test]
    public void Q403_A01_UnreadableReasons()
    {
        var json = SaveSerializer.Serialize(New());
        string With(string? v)
        {
            var o = JObject.Parse(json);
            if (v == null) o.Remove("SchemaVersion"); else o["SchemaVersion"] = v;
            return o.ToString(Newtonsoft.Json.Formatting.None);
        }
        Assert.That(SaveCatalog.ReadSnapshot(With("99"), Cfg).Reason,
            Is.EqualTo($"存档版本99比当前游戏支持的版本{GameState.CurrentSchema}新。请刷新页面更新游戏后再试。"));
        Assert.That(SaveCatalog.ReadSnapshot(With("2"), Cfg).Reason, Is.EqualTo("当前游戏不支持这个存档版本。"));
        Assert.That(SaveCatalog.ReadSnapshot(With("beta"), Cfg).Reason, Is.EqualTo("存档版本无法识别。"));
        Assert.That(SaveCatalog.ReadSnapshot(With(null), Cfg).Reason, Is.EqualTo("存档版本无法识别。"));
        Assert.That(SaveCatalog.ReadSnapshot("{\"SchemaVersion\":", Cfg).Reason, Is.EqualTo("存档内容已损坏。"));

        // 迁移冲突（版本 3 夹具：火堆锅位与烧水用锅不同）
        var conflict = SaveCatalog.ReadSnapshot(Fixture("3-conflict"), Cfg);
        Assert.That(conflict.Problem, Is.EqualTo(SaveProblemKind.Conflict));
        Assert.That(conflict.Reason, Does.Contain("原火堆上的金属锅与进行中的烧水所用的锅不同，无法确定使用哪一口锅。"));
        Assert.That(conflict.Reason, Does.Not.Contain("#"));

        // 同一实例多处归属：用物品显示名
        var o3 = JObject.Parse(Fixture("3-attached-idle"));
        var items = (JArray)o3["Items"]!;
        var dup = items.First(i => (string?)i["ItemId"] == "pot");
        items.Add(dup.DeepClone());
        var dupRead = SaveCatalog.ReadSnapshot(o3.ToString(Newtonsoft.Json.Formatting.None), Cfg);
        var potName = Cfg.Items.Single(i => i.Id == "pot").Name;
        Assert.That(dupRead.Reason, Does.StartWith($"{potName}在存档中重复记录了2次，归属不一致。"));
        Assert.That(dupRead.Reason, Does.Not.Contain("#"));

        // 多存档槽：版本过新的槽能读出摘要，按所属分区显示原因，不可读
        var st = new MemStorage();
        var c = Page(st, "A");
        var s = New();
        c.StartNewGame(s, T0);
        c.SaveManual(s, null, T0 + Sec);
        var key = SaveSlots.ManualKey(0);
        var env = SaveSlotEnvelope.Parse(st.D[key]);
        env.Schema = "99";
        env.State = With("99");
        env.Checksum = SaveSlots.Checksum(env.State);
        st.D[key] = env.ToJson();
        var e = SaveCatalog.List(st, Cfg, null).Manual.Single();
        Assert.That(e.Readable, Is.False);
        Assert.That(e.ProblemText, Is.EqualTo($"这份存档无法读取：存档版本99比当前游戏支持的版本{GameState.CurrentSchema}新。请刷新页面更新游戏后再试。"));
        Assert.That(e.Minute, Is.EqualTo(s.GameMinute), "能读出的字段照常显示");
        Assert.That(SaveCatalog.Load(st, key, Cfg).ProblemText, Is.EqualTo(e.ProblemText));
    }

    /// <summary>
    /// Q384-A01 / Q404-A01：无档时列表为空且显示空态；单槽损坏只该档不可读（完全读不出摘要时只保留类型和原因），其他档仍可读取；完整性校验失败算损坏。
    /// </summary>
    [Test]
    public void Q384_A01_Q404_A01_CorruptSlotIsolated()
    {
        var st = new MemStorage();
        var empty = SaveCatalog.List(st, Cfg, null);
        Assert.That(empty.All, Is.Empty);
        Assert.That(empty.HasReadable, Is.False);

        var c = Page(st, "A");
        var s = New();
        c.StartNewGame(s, T0);
        st.D[SaveSlots.ManualKey(0)] = "{not json";
        var env = SaveSlotEnvelope.Parse(st.D[SaveSlots.Auto5Key]);
        env.Slot = SaveSlots.DailyKey;
        env.State = env.State.Replace("\"GameMinute\":", "\"GameMinute\": ");
        st.D[SaveSlots.DailyKey] = env.ToJson();   // 校验和对不上

        var view = SaveCatalog.List(st, Cfg, null);
        Assert.That(view.HasReadable, Is.True);
        var auto5 = view.Auto[0];
        Assert.That(auto5.Readable, Is.True);
        Assert.That(auto5.TypeText, Is.EqualTo("自动存档 · 每5分钟"));
        Assert.That(auto5.SavedAtMs, Is.EqualTo(T0));
        Assert.That(auto5.DayTimeText, Does.StartWith("第1日 · "));
        var daily = view.Auto[1];
        Assert.That(daily.TypeText, Is.EqualTo("自动存档 · 每日06:00"));
        Assert.That(daily.ProblemText, Is.EqualTo("这份存档无法读取：存档内容已损坏。"));
        Assert.That(daily.Minute, Is.Not.Null, "有部分可信摘要的照常显示");
        var bad = view.Manual.Single();
        Assert.That(bad.Readable, Is.False);
        Assert.That(bad.ProblemText, Is.EqualTo("这份存档无法读取：存档内容已损坏。"));
        Assert.That(bad.SavedAtKnown || bad.Minute != null || bad.Location != null, Is.False, "完全读不出时只留类型和原因");
        Assert.That(SaveCatalog.Load(st, SaveSlots.Auto5Key, Cfg).Ok, Is.True, "其他档仍可读");

        var readFail = new MemStorage { ReadFailOn = k => k == SaveSlots.DailyKey };
        readFail.D = st.D;
        Assert.That(SaveCatalog.List(readFail, Cfg, null).Auto[1].ProblemText, Is.EqualTo("这份存档无法读取：浏览器暂时无法读取存档，请稍后重试。"));
    }

    // ───────────── Q385 / Q405 / Q406 / Q411 手动存档 ─────────────

    /// <summary>
    /// Q385-A01 / Q411-A01：阻断原句与由上到下的优先级；招募待安置命中事务阻断；只有 Q243 接续待补播时可保存。
    /// </summary>
    [Test]
    public void Q385_A01_BlockReasonsAndOrder()
    {
        var st = new MemStorage();
        var c = Page(st, "A");
        var s = New();
        c.StartNewGame(s, T0);
        Assert.That(c.ManualBlockedText(s), Is.Null);

        s.City.SubstituteIntroDeferred.Add(new SubstituteIntroDeferral { CampaignId = "x", NpcId = "n", LocationId = s.CurrentLocation, IdentityKey = "k" });
        Assert.That(c.ManualBlockedText(s), Is.Null, "Q243 待补播不算在办");
        Assert.That(c.SaveManual(s, null, T0 + Sec).Ok, Is.True);

        s.Recruit.Pending = new RecruitTxn { AwaitingPlacement = true };
        Assert.That(c.ManualBlockedText(s), Is.EqualTo("先办完当前事务再存档。"));
        foreach (var it in Cfg.Items.Where(i => i.Kind == "material").Take(8)) E.CreateItems(s, it.Id, 1, ItemInstance.Carried);
        Assume.That(E.CarriedOverflow(s), Is.True);
        Assert.That(c.ManualBlockedText(s), Is.EqualTo("先完成整理再存档。"));
        s.Main = new ActionState { Type = ActionType.Rest, Status = ActionStatus.Paused };
        Assert.That(c.ManualBlockedText(s), Is.EqualTo("行动暂停中，先继续或退出行动再存档。"));
        s.Main.Status = ActionStatus.Running;
        Assert.That(c.ManualBlockedText(s), Is.EqualTo("行动进行中，不能手动存档。"));
        s.Main.Status = ActionStatus.Paused;
        s.Sub = new ActionState { Type = ActionType.Rest, IsSub = true };
        Assert.That(c.ManualBlockedText(s), Is.EqualTo("行动进行中，不能手动存档。"), "子行动在办");
        s.Expedition = new ExpeditionState { Phase = ExpeditionState.Extracting };
        Assert.That(c.ManualBlockedText(s), Is.EqualTo("撤出途中不能手动存档。"));
        s.Encounter = new PendingEncounter { LocationId = s.CurrentLocation };
        Assert.That(c.ManualBlockedText(s), Is.EqualTo("先处理敌踪再存档。"));
        s.Battle = new BattleState();
        Assert.That(c.ManualBlockedText(s), Is.EqualTo("战斗中不能手动存档。"));

        var story = NewStory();
        Assume.That(E.DialogueLocked(story), Is.True);
        story.Battle = new BattleState();
        Assert.That(c.ManualBlockedText(story), Is.EqualTo("请先读完对话"));

        // 写权限优先（Q408-3）
        Page(st, "B").StartLoaded(New(), T0 + 10 * Sec);
        Assert.That(c.ManualBlockedText(story), Is.EqualTo("另一个游戏页面正在保存。"));
    }

    /// <summary>
    /// Q405-A01：十份不可读的手动档仍能逐条明确覆盖；覆盖失败保留原槽、失败只留子页（Q406-A01），重试仍校验；离开子页清除失败。
    /// </summary>
    [Test]
    public void Q405_A01_Q406_A01_OverwriteUnreadable_FailureStaysInPage()
    {
        var st = new MemStorage();
        foreach (var k in SaveSlots.ManualKeys()) st.D[k] = "garbage";
        var c = Page(st, "A");
        var s = New();
        c.StartNewGame(s, T0);
        Assert.That(c.ManualFull, Is.True);
        var list = c.ManualSlots();
        Assert.That(list, Has.Count.EqualTo(10));
        Assert.That(list.All(e => !e.Readable && e.TypeText == "手动存档"), Is.True);

        st.QuotaOn = k => k == list[0].Key;
        var r = c.SaveManual(s, list[0].Key, T0 + Sec);
        Assert.That(r.Status, Is.EqualTo(SaveWriteStatus.Failed));
        Assert.That(c.ManualFailure!.Text, Is.EqualTo("手动存档：存档失败：浏览器存储空间已满。"));
        Assert.That(c.AutoFailures, Is.Empty, "手动失败不进底栏");
        Assert.That(st.D[list[0].Key], Is.EqualTo("garbage"), "失败保留原槽");
        st.QuotaOn = null;
        s.Main = new ActionState { Type = ActionType.Rest, Status = ActionStatus.Running };
        Assert.That(c.RetryManual(s, T0 + 2 * Sec).BlockedText, Is.EqualTo("行动进行中，不能手动存档。"), "重试仍校验安全空闲");
        s.Main = null;
        Assert.That(c.RetryManual(s, T0 + 3 * Sec).Ok, Is.True);
        Assert.That(c.ManualFailure, Is.Null);

        st.FailOn = _ => true;
        c.SaveManual(s, list[1].Key, T0 + 4 * Sec);
        Assert.That(c.ManualFailure!.Text, Is.EqualTo("手动存档：保存失败：boom"));
        c.ClearManualFailure();
        Assert.That(c.ManualFailure, Is.Null);
        st.FailOn = null;

        foreach (var e in c.ManualSlots().Where(e => !e.Readable))
            Assert.That(c.SaveManual(s, e.Key, T0 + 10 * Sec).Ok, Is.True);
        Assert.That(c.ManualSlots().All(e => e.Readable), Is.True);
        Assert.That(c.LastSavedAtMs, Is.EqualTo(T0 + 10 * Sec));
    }

    /// <summary>Q406-A01：读档后本页尚未写入任何槽时底栏保存状态为空；写成功后记该时刻。</summary>
    [Test]
    public void Q406_A01_FooterEmptyAfterLoad()
    {
        var st = new MemStorage();
        var c = Page(st, "A");
        var s = New();
        c.StartNewGame(s, T0);
        Assert.That(c.LastSavedAtMs, Is.EqualTo(T0));
        c.StartLoaded(SaveCatalog.Load(st, SaveSlots.Auto5Key, Cfg).State!, T0 + 50 * Sec);
        Assert.That(c.LastSavedAtMs, Is.Null);
        Assert.That(c.Auto5Time().SavedAtMs, Is.EqualTo(T0), "菜单仍可看读到的历史时刻");
        Assert.That(c.DailyTime().Exists, Is.False, "尚未保存");
        Assert.That(c.LatestManualTime().Exists, Is.False);
        st.D[SaveSlots.DailyKey] = "{broken";
        Assert.That(c.DailyTime().Exists && c.DailyTime().SavedAtMs == null, Is.True, "已有快照的时刻缺失：保存时间未记录");
        var rf = new MemStorage { ReadFailOn = k => k == SaveSlots.DailyKey };
        rf.D = st.D;
        var c2 = new SaveCoordinator(rf, E, "Z");
        Assert.That(c2.DailyTime().ReadFailed, Is.True, "读不出单独标出");
    }

    /// <summary>
    /// Q413-A01：读某槽时浏览器抛错，菜单对应行显示「尚未保存」，选档页仍显示该槽读取失败原因；
    /// 正常已有档仍显示保存时刻，已有快照仅缺时刻时仍写「保存时间未记录」。
    /// </summary>
    [Test]
    public void Q413_A01_ReadFailedRowSaysNotSavedYet()
    {
        static string Fmt(long ms) => "T" + ms;
        var st = new MemStorage();
        var c = Page(st, "A");
        var s = New();
        c.StartNewGame(s, T0);
        st.D[SaveSlots.DailyKey] = "{broken";
        Assert.That(SaveSlots.MenuTimeText(c.Auto5Time(), Fmt), Is.EqualTo(Fmt(T0)), "正常已有档显示保存时刻");
        Assert.That(SaveSlots.MenuTimeText(c.DailyTime(), Fmt), Is.EqualTo("保存时间未记录"), "已有快照缺时刻");
        Assert.That(SaveSlots.MenuTimeText(c.LatestManualTime(), Fmt), Is.EqualTo("尚未保存"), "未写入");

        var firstManual = SaveSlots.ManualKeys().First();
        var rf = new MemStorage { D = st.D, ReadFailOn = k => k == SaveSlots.DailyKey || k == firstManual };
        var c2 = new SaveCoordinator(rf, E, "Z");
        Assert.That(c2.DailyTime().ReadFailed, Is.True);
        Assert.That(SaveSlots.MenuTimeText(c2.DailyTime(), Fmt), Is.EqualTo("尚未保存"), "每日槽读不出：尚未保存");
        Assert.That(c2.LatestManualTime().ReadFailed, Is.True);
        Assert.That(SaveSlots.MenuTimeText(c2.LatestManualTime(), Fmt), Is.EqualTo("尚未保存"), "手动槽读不出且无其他手动档：尚未保存");
        Assert.That(SaveSlots.MenuTimeText(c2.Auto5Time(), Fmt), Is.EqualTo(Fmt(T0)), "其他槽不受影响");

        // 选档页同一个槽仍按 Q403 显示真实读取失败原因
        var view = SaveCatalog.List(rf, Cfg, null);
        var daily = view.Auto.Single(e => e.Key == SaveSlots.DailyKey);
        Assert.That(daily.Readable, Is.False);
        Assert.That(daily.Problem, Is.EqualTo(SaveProblemKind.ReadFailed));
        Assert.That(daily.ProblemText, Is.EqualTo(SaveSlots.UnreadablePrefix + SaveProblems.ReadFailed));
        Assert.That(daily.ProblemText, Does.Not.Contain("尚未保存"));

        // 有一份可读手动档、另一份读不出时，手动行仍显示可读那份的时刻
        Assert.That(c.SaveManual(s, SaveSlots.ManualKeys().ElementAt(1), T0 + 7 * Sec).Ok, Is.True);
        var c3 = new SaveCoordinator(rf, E, "Y");
        Assert.That(SaveSlots.MenuTimeText(c3.LatestManualTime(), Fmt), Is.EqualTo(Fmt(T0 + 7 * Sec)));
    }

    // ───────────── Q386 / Q387 / Q388 / Q389 / Q407 / Q409 ─────────────

    /// <summary>Q386-A01 / Q409-A01：新局只覆盖每5分钟槽；坏的每5分钟档也算已有档；局中重开即使槽为空也确认；额外写入不重算周期。</summary>
    [Test]
    public void Q386_A01_Q409_A01_NewGameConfirmAndOnlyAuto5()
    {
        var st = new MemStorage();
        var c = Page(st, "A");
        Assert.That(c.NewGameNeedsConfirm(false), Is.False, "槽为空时标题新游戏不确认");
        Assert.That(c.NewGameNeedsConfirm(true), Is.True, "局中重开即使槽为空也确认");
        st.D[SaveSlots.Auto5Key] = "broken";
        Assert.That(c.NewGameNeedsConfirm(false), Is.True, "坏档也算已有档");
        st.D.Remove(SaveSlots.Auto5Key);

        var s = New();
        s.GameMinute = At(1, 5, 55);
        c.StartNewGame(s, T0);
        s.GameMinute = At(1, 6, 0);
        c.Commit(s, T0 + Sec);
        c.SaveManual(s, null, T0 + 2 * Sec);
        var daily = st.D[SaveSlots.DailyKey];
        var manual = st.D[SaveSlots.ManualKey(0)];
        Assert.That(c.NextAutoDueMs, Is.EqualTo(T0 + 300 * Sec), "手动写入不重算周期");

        var c2 = Page(st, "A");
        Assert.That(c2.StartNewGame(New(), T0 + 100 * Sec).Ok, Is.True);
        Assert.That(st.D[SaveSlots.DailyKey], Is.EqualTo(daily), "每日档留到新局下次 06:00");
        Assert.That(st.D[SaveSlots.ManualKey(0)], Is.EqualTo(manual), "手动档保留");
        c.Tick(c.SessionId, T0 + 400 * Sec);
        Assert.That(c.NextAutoDueMs, Is.EqualTo(T0 + 600 * Sec));
        var old = c.SessionId;
        c.StartLoaded(New(), T0 + 500 * Sec);
        Assert.That(c.Tick(old, T0 + 900 * Sec), Is.False, "换局后旧计时回调失效");
    }

    /// <summary>Q387-A01：「保存并返回标题」失败留在游戏并显示失败；成功写每5分钟槽后结束会话、清除其余待重试项。</summary>
    [Test]
    public void Q387_A01_SaveAndExit()
    {
        var st = new MemStorage();
        var c = Page(st, "A");
        var s = New();
        s.GameMinute = At(1, 5, 55);
        c.StartNewGame(s, T0);
        st.QuotaOn = k => k == SaveSlots.DailyKey;
        s.GameMinute = At(1, 6, 0);
        c.Commit(s, T0 + Sec);
        Assert.That(c.AutoFailures, Has.Count.EqualTo(1));
        st.QuotaOn = k => k == SaveSlots.Auto5Key;
        var fail = c.SaveAndExit(s, T0 + 2 * Sec);
        Assert.That(fail.Status, Is.EqualTo(SaveWriteStatus.Failed));
        Assert.That(c.InSession, Is.True, "失败留在当前画面");
        Assert.That(c.AutoFailures.Select(f => f.Kind), Is.EqualTo(new[] { SaveSlotKind.Auto5, SaveSlotKind.Daily }), "依次展示");
        st.QuotaOn = null;
        Assert.That(c.SaveAndExit(s, T0 + 3 * Sec).Ok, Is.True);
        Assert.That(c.InSession, Is.False);
        Assert.That(c.AutoFailures, Is.Empty, "其余待重试项清除");
        Assert.That(c.HasUnsavedProgress, Is.False);
        Assert.That(MinuteOf(st, SaveSlots.Auto5Key), Is.EqualTo(At(1, 6, 0)));
    }

    /// <summary>
    /// Q388-A01：0 分钟提交或时间推进后有未保存标记；写失败、只存到旧快照、别的页面写入都不清；本页最新快照成功保存即清；读档以所选快照为已保存基线。
    /// </summary>
    [Test]
    public void Q388_A01_UnsavedMarker()
    {
        var st = new MemStorage();
        var c = Page(st, "A");
        var s = New();
        s.GameMinute = At(1, 5, 55);
        c.StartNewGame(s, T0);
        Assert.That(c.HasUnsavedProgress, Is.False);
        c.Commit(s, T0 + Sec);   // 0 分钟操作
        Assert.That(c.HasUnsavedProgress, Is.True);

        // 每日快照写入失败，之后再提交；重试写成功的是旧快照，不清标记
        st.QuotaOn = k => k == SaveSlots.DailyKey;
        s.GameMinute = At(1, 6, 0);
        c.Commit(s, T0 + 2 * Sec);
        Assert.That(c.HasUnsavedProgress, Is.True, "写失败不清");
        st.QuotaOn = null;
        c.Commit(s, T0 + 3 * Sec);
        c.Retry(SaveSlotKind.Daily, T0 + 4 * Sec);
        Assert.That(c.HasUnsavedProgress, Is.True, "只存到旧快照不清");

        // 别的页面写入不清
        var b = Page(st, "B");
        b.StartLoaded(New(), T0 + 5 * Sec);
        b.SaveManual(New(), null, T0 + 6 * Sec);
        Assert.That(c.HasUnsavedProgress, Is.True);
        Assert.That(b.HasUnsavedProgress, Is.False, "读档以所选快照为已保存基线");

        var c2 = Page(st, "C");
        var s2 = New();
        c2.StartNewGame(s2, T0 + 10 * Sec);
        c2.Commit(s2, T0 + 11 * Sec);
        Assert.That(c2.SaveManual(s2, null, T0 + 12 * Sec).Ok, Is.True);
        Assert.That(c2.HasUnsavedProgress, Is.False, "最新快照成功保存即清");
    }

    /// <summary>Q407-A01：地点与品牌行同取当前实际地点名：移动中为起点，安全屋内只写地点名，已结束档为终局地点。</summary>
    [Test]
    public void Q407_A01_SummaryLocation()
    {
        var s = New();
        var here = s.CurrentLocation;
        var name = E.LocName(here);
        s.Main = new ActionState { Type = ActionType.Travel, TargetId = Cfg.Locations.First(l => l.Id != here).Id };
        Assert.That(E.SaveSummaryOf(s).Location, Is.EqualTo(name), "移动中用起点");
        s.Main = null;
        s.CurrentZone = GameState.SafehouseZone;
        Assert.That(E.SaveSummaryOf(s).Location, Is.EqualTo(name), "屋内不加区域");
        s.CurrentZone = "exterior";
        s.Progress.Dead = true;
        var sum = E.SaveSummaryOf(s);
        Assert.That(sum.Location, Is.EqualTo(name));
        Assert.That(sum.Ended, Is.True);
        Assert.That(E.SaveLocationName("no_such_place"), Is.Null, "读不出的地点省略");
        Assert.That(SaveSlots.DayTimeText(At(3, 6, 5)), Is.EqualTo("第3日 · 06:05"));
    }

    /// <summary>
    /// Q389-A01（存储侧）：地牢重置的同步写盘失败返回 false（事务回滚由 ResetDungeonAtomic 负责，见 B22ResetTests.DR18），不进底栏待重试；
    /// 成功即记为已保存且不重算周期。装备、封存等普通提交后定时保存失败只报错，不撤销已提交的状态。
    /// </summary>
    [Test]
    public void Q389_A01_ResetPersistAndNormalCommit()
    {
        var st = new MemStorage();
        var c = Page(st, "A");
        var s = New();
        c.StartNewGame(s, T0);
        st.QuotaOn = _ => true;
        Assert.That(c.PersistReset(s, T0 + Sec), Is.False);
        Assert.That(c.AutoFailures, Is.Empty, "重置失败沿原重置流程提示");
        st.QuotaOn = null;
        c.Commit(s, T0 + 2 * Sec);
        Assert.That(c.PersistReset(s, T0 + 3 * Sec), Is.True);
        Assert.That(c.HasUnsavedProgress, Is.False);
        Assert.That(c.NextAutoDueMs, Is.EqualTo(T0 + 300 * Sec), "额外写入不重算周期");

        // 普通提交后的定时保存失败：已提交状态保持
        E.CreateItems(s, "stone", 2, ItemInstance.Carried);
        var stones = s.Items.Count(i => i.ItemId == "stone");
        st.QuotaOn = _ => true;
        c.Tick(c.SessionId, T0 + 300 * Sec);
        c.Commit(s, T0 + 300 * Sec);
        Assert.That(c.AutoFailures, Has.Count.EqualTo(1));
        Assert.That(s.Items.Count(i => i.ItemId == "stone"), Is.EqualTo(stones));
    }
}

/// <summary>
/// B40a Q389-A01（与内核事务联测）：地牢重置以存档协调器写「每5分钟」槽作同步写盘；写失败（配额、失权）时保持旧代、遗留物归属及请求号，
/// 槽内原快照不变；成功后槽内是新一代。
/// </summary>
public class B40aResetPersistTests : BattleTestBase
{
    [Test]
    public void Q389_A01_ResetWriteFailRollsBack()
    {
        var s = B21EntryTests.AtCrypt(E, 4001);
        UnlockCapacity(s, 3);
        E.DebugTeleport(s, "d01_08");
        E.DebugSetProgress(s, "d01_08", 100);
        E.DebugInstanceFact(s, "d01", "guardianDefeated");
        Start(s, ActionRequest.Gather("d01_08_unique"));
        Run(s);
        Assume.That(E.DungeonCleared(s, "d01"), Is.True);
        E.DebugTeleport(s, "c03_crypt");
        E.CreateItems(s, "cloth", 2, "d01_02");
        s.GameMinute = s.DungeonInstances["d01"].ResetEligibleAt!.Value;
        var leftover = E.DungeonLeftovers(s, "d01").Select(x => (x.Item.InstanceId, x.Item.Owner)).ToList();

        var st = new B40aSaveSlotsTests.MemStorage();
        var c = new SaveCoordinator(st, E, "A");
        c.StartLoaded(s, 0);
        st.QuotaOn = _ => true;
        var r = E.ResetDungeonAtomic(s, "d01", "req-1", next => c.PersistReset(next, 1000), out var after);
        Assert.That(r.Ok, Is.False);
        Assert.That(after.DungeonInstances["d01"].Generation, Is.EqualTo(1), "旧代保持");
        Assert.That(E.DungeonLeftovers(after, "d01").Select(x => (x.Item.InstanceId, x.Item.Owner)), Is.EquivalentTo(leftover), "物品归属原样");
        Assert.That(st.D.ContainsKey(SaveSlots.Auto5Key), Is.False, "槽内没有新快照");
        st.QuotaOn = null;

        // 失权同样不写、回滚
        new SaveCoordinator(st, E, "B").StartLoaded(after, 2000);
        Assert.That(E.ResetDungeonAtomic(after, "d01", "req-1", next => c.PersistReset(next, 3000), out var after2).Ok, Is.False);
        Assert.That(after2.DungeonInstances["d01"].Generation, Is.EqualTo(1));

        // 接回后同请求号重试成功，槽内是新一代
        Assert.That(c.Reclaim(4000), Is.EqualTo(SavePermission.Holding));
        Assert.That(E.ResetDungeonAtomic(after2, "d01", "req-1", next => c.PersistReset(next, 5000), out var ok).Ok, Is.True);
        Assert.That(ok.DungeonInstances["d01"].Generation, Is.EqualTo(2));
        var saved = SaveCatalog.Load(st, SaveSlots.Auto5Key, Cfg).State!;
        Assert.That(saved.DungeonInstances["d01"].Generation, Is.EqualTo(2));
        Assert.That(c.HasUnsavedProgress, Is.False);
    }
}
