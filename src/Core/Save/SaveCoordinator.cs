using System;
using System.Collections.Generic;
using System.Linq;
using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Save
{
    /// <summary>本页对多存档的写权限（Q381 / Q408）。</summary>
    public enum SavePermission
    {
        /// <summary>本页持有写权限。</summary>
        Holding,
        /// <summary>另一个页面进入了本局：写入暂停，可点「在此页面继续保存」接回。</summary>
        Lost,
        /// <summary>接回被拒（有槽已保存较新快照）：拒绝句常驻、隐藏接回键（Q408-1）。</summary>
        Refused,
    }

    /// <summary>写入结果类别。</summary>
    public enum SaveWriteStatus
    {
        Ok,
        /// <summary>写入失败（浏览器异常）；失败句见 <see cref="SaveWriteOutcome.Failure"/>。</summary>
        Failed,
        /// <summary>没有执行：写权限、安全空闲或满额等阻断；原因句见 <see cref="SaveWriteOutcome.BlockedText"/>（可能为 null）。</summary>
        Blocked,
    }

    public sealed class SaveWriteOutcome
    {
        public SaveWriteStatus Status { get; private set; }
        public SaveFailure? Failure { get; private set; }
        public string? BlockedText { get; private set; }
        public bool Ok => Status == SaveWriteStatus.Ok;

        public static SaveWriteOutcome Success() => new SaveWriteOutcome { Status = SaveWriteStatus.Ok };
        public static SaveWriteOutcome Fail(SaveFailure f) => new SaveWriteOutcome { Status = SaveWriteStatus.Failed, Failure = f };
        public static SaveWriteOutcome Block(string? text) => new SaveWriteOutcome { Status = SaveWriteStatus.Blocked, BlockedText = text };
    }

    /// <summary>某槽一次写入失败（保留原槽与对应快照，重试写原槽与同一快照，Q335 边界 2）。</summary>
    public sealed class SaveFailure
    {
        public SaveSlotKind Kind { get; internal set; }
        public string Key { get; internal set; } = "";
        public bool Quota { get; internal set; }
        /// <summary>浏览器原始错误文本（开发诊断）。</summary>
        public string? Message { get; internal set; }
        /// <summary>Q382 / Q409-4：主角终局快照的写入失败（显示在结算卡内）。</summary>
        public bool Terminal { get; internal set; }
        /// <summary>玩家看的失败句：前标目标槽（Q387 / Q406）。</summary>
        public string Text => SaveSlots.FailureText(Kind, Quota, Message);

        internal Snapshot Snap = null!;
    }

    /// <summary>菜单三行（Q387）里一个槽的保存时刻：不存在为「尚未保存」，存在但无时刻为「保存时间未记录」。</summary>
    public sealed class SlotTime
    {
        public bool Exists { get; internal set; }
        public long? SavedAtMs { get; internal set; }
        /// <summary>浏览器读不出该槽（不知道有没有档）。菜单行按 Q413 写「尚未保存」（见 <see cref="SaveSlots.MenuTimeText"/>）。</summary>
        public bool ReadFailed { get; internal set; }
    }

    internal sealed class Snapshot
    {
        public string Json = "";
        public SaveSlotSummary Summary = new SaveSlotSummary();
        public string Schema = GameState.CurrentSchema;
        /// <summary>快照对应的提交序号（Q388：只有本页最新已提交快照写成功才清未保存标记）。</summary>
        public int Seq;
    }

    /// <summary>
    /// 多存档的写入调度与写权限（S01「2026-11-10 Q335 多存档定稿」、Q381—Q389、Q403—Q411）。内核纯逻辑，现实时间由调用方传入（UTC 毫秒）。
    /// <para>网页壳用法：进入本局调 <see cref="StartNewGame"/> / <see cref="StartLoaded"/>；每次已提交的存档相关状态变化（原 Save() 调用处、
    /// 每个世界段推进后，含跳过动画时）调 <see cref="Commit"/>；现实计时器到点调 <see cref="Tick"/>，在可序列化空闲边界调 <see cref="Pump"/>；
    /// 回标题调 <see cref="EndSession"/>。另一页面的存储变化调 <see cref="CheckPermission"/>。</para>
    /// </summary>
    public sealed class SaveCoordinator
    {
        private readonly ISaveStorage _st;
        private readonly Func<string> _newRev;
        private readonly Dictionary<string, string?> _baseline = new Dictionary<string, string?>();
        private readonly Dictionary<SaveSlotKind, SaveFailure> _failures = new Dictionary<SaveSlotKind, SaveFailure>();

        private long _entryMs;
        private long _nextDueMs;
        private bool _autoDue;
        private Snapshot? _dailyPending;
        private Snapshot? _terminalPending;
        private int _lastMinute;
        private int _commitSeq;
        private int _savedSeq;

        public SaveCoordinator(ISaveStorage storage, GameEngine engine, string pageToken, Func<string>? newRev = null)
        {
            _st = storage;
            Engine = engine;
            PageToken = pageToken;
            _newRev = newRev ?? (() => Guid.NewGuid().ToString("N"));
        }

        public GameEngine Engine { get; }
        public string PageToken { get; }

        /// <summary>本局会话号：每次新局、读档、回标题都会变，旧计时回调凭它失效。</summary>
        public int SessionId { get; private set; }
        public bool InSession { get; private set; }
        /// <summary>本会话的局已终局（死亡或尸变已提交，或读入的就是已结束档）：停止自动计时与自动写入（Q382）。</summary>
        public bool Ended { get; private set; }
        public SavePermission Permission { get; private set; } = SavePermission.Holding;

        /// <summary>底栏写权限句（Q381 / Q408-1）；持有时为 null。</summary>
        public string? PermissionNotice => Permission switch
        {
            SavePermission.Lost => SaveSlots.PermissionLostText,
            SavePermission.Refused => SaveSlots.ReclaimRefusedText,
            _ => null,
        };

        /// <summary>显示「在此页面继续保存」（拒绝接回后隐藏，Q408-1）。</summary>
        public bool CanReclaim => InSession && Permission == SavePermission.Lost;

        /// <summary>
        /// 无写权限时「重试」、手动存档、「保存并返回标题」与确认重置的禁用原因（Q381、Q408-4：失权期间重试原因为「另一个游戏页面正在保存。」）。
        /// </summary>
        public string? WriteBlockedText => Permission == SavePermission.Holding ? null : PermissionNotice;

        /// <summary>失权期间「重试」的禁用原因（Q408-4）。</summary>
        public string? RetryBlockedText => Permission == SavePermission.Holding ? null : SaveSlots.PermissionLostText;

        /// <summary>自动槽的待重试项，按「每5分钟」「每日06:00」依次（Q387）；终局失败不在这里（见 <see cref="TerminalFailure"/>）。</summary>
        public IReadOnlyList<SaveFailure> AutoFailures =>
            new[] { SaveSlotKind.Auto5, SaveSlotKind.Daily }.Where(_failures.ContainsKey).Select(k => _failures[k]).ToList();

        /// <summary>终局快照写失败（Q409-4：结算卡内显示与重试）。</summary>
        public SaveFailure? TerminalFailure { get; private set; }

        /// <summary>手动存档子页的失败（Q406：只留在子页，离开子页清除）。</summary>
        public SaveFailure? ManualFailure { get; private set; }

        /// <summary>本页最近一次实际保存成功的现实时刻（Q387 / Q406 底栏「已保存 {时:分}」）；读档后尚未写入任何槽时为 null（底栏留空）。</summary>
        public long? LastSavedAtMs { get; private set; }

        /// <summary>Q388：本页有尚未成功保存的已提交进度（浏览器原生离开确认用）。</summary>
        public bool HasUnsavedProgress => InSession && _commitSeq != _savedSeq;

        /// <summary>有已到期、尚未落盘的自动任务（网页壳在空闲边界调 <see cref="Pump"/>）。</summary>
        public bool HasPendingWrites => InSession && (_autoDue || _dailyPending != null || _terminalPending != null);

        /// <summary>下一个「每5分钟」到期点（UTC 毫秒，沿进入本局起的时间轴）。</summary>
        public long NextAutoDueMs => _nextDueMs;

        // ───────────── 会话 ─────────────

        /// <summary>
        /// 新游戏前是否先确认（Q335 / Q386 / Q409-2、3）：局中「重新开始…」一律确认；标题新游戏与终局重开在每5分钟槽有数据（含不可读）时确认。
        /// </summary>
        public bool NewGameNeedsConfirm(bool midGame)
        {
            if (midGame) return true;
            try { return !string.IsNullOrEmpty(_st.Get(SaveSlots.Auto5Key)); }
            catch (Exception) { return true; }
        }

        /// <summary>
        /// 明确开始新局（Q335）：取得写权限、旧会话的计时与待写任务作废，新局初始快照只写「每5分钟」槽（额外存档点）。
        /// 写失败留待重试（Q409-3）。
        /// </summary>
        public SaveWriteOutcome StartNewGame(GameState s, long nowMs)
        {
            Begin(s, nowMs, saved: false);
            return WriteAuto5Now(Capture(s), nowMs, terminal: false);
        }

        /// <summary>读档进入（Q335 / Q406）：取得写权限，所选快照为已保存基线；读入已结束档不启动计时、不写任何槽（Q382）。</summary>
        public void StartLoaded(GameState s, long nowMs)
        {
            Begin(s, nowMs, saved: true);
            if (s.Progress.Dead) Ended = true;
        }

        /// <summary>回标题或关闭本局：停止计时，其余未执行的写入与失败提示一并清除（Q387），终局待写与失败作废（Q409-4）。</summary>
        public void EndSession()
        {
            SessionId++;
            InSession = false;
            Ended = false;
            ClearTasks();
            LastSavedAtMs = null;
            _commitSeq = _savedSeq = 0;
        }

        private void Begin(GameState s, long nowMs, bool saved)
        {
            EndSession();
            InSession = true;
            Claim();
            CaptureBaseline();
            _entryMs = nowMs;
            _nextDueMs = nowMs + SaveSlots.AutoIntervalMs;
            _lastMinute = s.GameMinute;
            _commitSeq = 1;
            _savedSeq = saved ? 1 : 0;
        }

        private void ClearTasks()
        {
            _autoDue = false;
            _dailyPending = null;
            _terminalPending = null;
            _failures.Clear();
            TerminalFailure = null;
            ManualFailure = null;
        }

        // ───────────── 计时与边界 ─────────────

        /// <summary>
        /// 现实计时器（Q335 / Q408）：到期只记一次待写请求（错过多个间隔也只一次），下一到期点沿进入本局起的时间轴。
        /// 失权期间照常计时、任务保留。会话号不符（旧回调）或已终局时什么也不做。返回是否有待写任务。
        /// </summary>
        public bool Tick(int sessionId, long nowMs)
        {
            if (sessionId != SessionId || !InSession) return false;
            if (!Ended && nowMs >= _nextDueMs)
            {
                _autoDue = true;
                var k = (nowMs - _entryMs) / SaveSlots.AutoIntervalMs + 1;
                _nextDueMs = _entryMs + k * SaveSlots.AutoIntervalMs;
            }
            return HasPendingWrites;
        }

        /// <summary>
        /// 一次已提交的存档相关状态变化（含 0 分钟操作、每个世界段）：记未保存；主角首次终局立即写终局快照（Q382）；
        /// 世界时间实际跨过当日 06:00 时记下本段快照待写「每日06:00」（Q335）；随后在本边界执行已到期的写入。
        /// </summary>
        public void Commit(GameState s, long nowMs)
        {
            if (!InSession) return;
            _commitSeq++;
            if (Ended)
            {
                Pump(s, nowMs);
                return;
            }
            if (s.Progress.Dead)
            {
                // Q382：终局停止自动计时与其他未落盘的自动任务，终局段不另外触发每日档
                Ended = true;
                _autoDue = false;
                _dailyPending = null;
                _failures.Clear();
                _terminalPending = Capture(s);
                _lastMinute = s.GameMinute;
                Pump(s, nowMs);
                return;
            }
            if (CrossesDaily(_lastMinute, s.GameMinute)) _dailyPending = Capture(s);   // 同槽较新的任务覆盖较旧未落盘任务
            _lastMinute = s.GameMinute;
            Pump(s, nowMs);
        }

        /// <summary>段前时间小于某日 06:00 且段后时间达到或超过该时刻（Q335）。</summary>
        public static bool CrossesDaily(int beforeMinute, int afterMinute)
        {
            if (afterMinute <= beforeMinute) return false;
            var dayStart = beforeMinute - (((beforeMinute % 1440) + 1440) % 1440);
            var t = dayStart + SaveSlots.DailyMinuteOfDay;
            if (t <= beforeMinute) t += 1440;
            return t <= afterMinute;
        }

        /// <summary>在可序列化空闲边界执行已到期的写入（无写权限时保留任务）。</summary>
        public void Pump(GameState s, long nowMs)
        {
            if (!InSession || !HoldsPermission()) return;
            if (_terminalPending != null)
            {
                var t = _terminalPending;
                var r = WriteAuto5Now(t, nowMs, terminal: true);
                if (r.Status != SaveWriteStatus.Blocked) _terminalPending = null;
            }
            if (Ended) return;
            if (_autoDue)
            {
                var r = WriteAuto5Now(Capture(s), nowMs, terminal: false);
                if (r.Status != SaveWriteStatus.Blocked) _autoDue = false;
            }
            if (_dailyPending != null && Permission == SavePermission.Holding)
            {
                var d = _dailyPending;
                var r = WriteSlot(SaveSlots.DailyKey, d, nowMs);
                if (r.Status != SaveWriteStatus.Blocked)
                {
                    _dailyPending = null;
                    Record(SaveSlotKind.Daily, r);
                }
            }
        }

        private SaveWriteOutcome WriteAuto5Now(Snapshot snap, long nowMs, bool terminal)
        {
            var r = WriteSlot(SaveSlots.Auto5Key, snap, nowMs);
            if (r.Status == SaveWriteStatus.Blocked) return r;
            if (terminal)
            {
                if (r.Failure != null) r.Failure.Terminal = true;
                TerminalFailure = r.Failure;
                return r;
            }
            Record(SaveSlotKind.Auto5, r);
            return r;
        }

        private void Record(SaveSlotKind kind, SaveWriteOutcome r)
        {
            if (r.Failure != null) _failures[kind] = r.Failure;
            else if (r.Ok) _failures.Remove(kind);
        }

        // ───────────── 额外存档点 ─────────────

        /// <summary>
        /// 「保存并返回标题」（Q335 / Q387）：写入「每5分钟」槽，成功才结束本会话（其他槽未执行的写入和失败提示一并清除）；
        /// 失败留在当前画面并显示失败（按槽失败句，可重试）。无写权限时不执行。
        /// </summary>
        public SaveWriteOutcome SaveAndExit(GameState s, long nowMs)
        {
            if (!InSession) return SaveWriteOutcome.Success();
            if (Ended)
            {
                // 已结束的局停写（Q382）：不再覆盖任何槽
                EndSession();
                return SaveWriteOutcome.Success();
            }
            if (!HoldsPermission()) return SaveWriteOutcome.Block(WriteBlockedText);
            var r = WriteAuto5Now(Capture(s), nowMs, terminal: false);
            if (r.Ok) EndSession();
            return r;
        }

        /// <summary>
        /// 地牢重置事务的同步写盘（Q389，作 <c>GameEngine.ResetDungeonAtomic</c> 的 persist 回调）：写「每5分钟」槽，成功返回 true
        /// （同时记作本页最新已提交快照已保存，调用方成功后不必再 <see cref="Commit"/>）；无写权限、写失败返回 false，由事务回滚，
        /// 失败沿原重置流程提示，不进底栏待重试。不重算 300 秒周期（Q409-1）。
        /// </summary>
        public bool PersistReset(GameState next, long nowMs)
        {
            if (!InSession || Ended || !HoldsPermission()) return false;
            var snap = Capture(next);
            snap.Seq = _commitSeq + 1;
            var r = WriteSlot(SaveSlots.Auto5Key, snap, nowMs);
            if (!r.Ok) return false;
            _commitSeq = _savedSeq = snap.Seq;
            _autoDue = false;
            _failures.Remove(SaveSlotKind.Auto5);
            return true;
        }

        // ───────────── 失败重试 ─────────────

        /// <summary>重试自动槽（写原槽与对应快照，Q335 边界 2）；失权期间不执行（Q408-4）。</summary>
        public SaveWriteOutcome Retry(SaveSlotKind kind, long nowMs)
        {
            if (!InSession || !_failures.TryGetValue(kind, out var f)) return SaveWriteOutcome.Block(null);
            if (!HoldsPermission()) return SaveWriteOutcome.Block(RetryBlockedText);
            var r = WriteSlot(f.Key, f.Snap, nowMs);
            if (r.Status != SaveWriteStatus.Blocked) Record(kind, r);
            return r;
        }

        /// <summary>重试终局快照（Q382 / Q409-4）。</summary>
        public SaveWriteOutcome RetryTerminal(long nowMs)
        {
            if (!InSession || TerminalFailure == null) return SaveWriteOutcome.Block(null);
            if (!HoldsPermission()) return SaveWriteOutcome.Block(RetryBlockedText);
            var r = WriteSlot(SaveSlots.Auto5Key, TerminalFailure.Snap, nowMs);
            if (r.Status == SaveWriteStatus.Blocked) return r;
            if (r.Failure != null) r.Failure.Terminal = true;
            TerminalFailure = r.Failure;
            return r;
        }

        // ───────────── 手动存档 ─────────────

        /// <summary>
        /// 手动存档的禁用原因（Q385 / Q408-3）：先看写权限，再按阻断表由上到下第一条；可以保存时为 null。
        /// 终局（死亡结算菜单不提供入口）返回空串。
        /// </summary>
        public string? ManualBlockedText(GameState s)
        {
            if (!HoldsPermission()) return WriteBlockedText;
            var b = Engine.ManualSaveBlocked(s);
            if (b == ManualSaveBlock.None) return null;
            return GameEngine.ManualSaveBlockText(b) ?? "";
        }

        /// <summary>已有手动档（子页列表，从新到旧）。</summary>
        public List<SaveSlotEntry> ManualSlots() => SaveCatalog.ManualEntries(_st, Engine.Config);

        /// <summary>手动档已满 10 份（Q335：满时显示「手动存档已满。请选择一份覆盖。」）。</summary>
        public bool ManualFull => FreeManualKey() == null;

        /// <summary>
        /// 新建（targetKey 为 null）或明确覆盖一份手动档（Q335 / Q385 / Q405）：写入前重验写权限、安全空闲与目标槽版本；不花游戏时间。
        /// 成功返回 Ok（子页写「已保存到手动存档。」）；写失败记 <see cref="ManualFailure"/>（原槽保留，可重试）。
        /// 目标槽被别的页面写过时按 Q381 拒绝（拒绝句常驻）。
        /// </summary>
        public SaveWriteOutcome SaveManual(GameState s, string? targetKey, long nowMs)
        {
            if (!InSession) return SaveWriteOutcome.Block(null);
            ManualFailure = null;
            var why = ManualBlockedText(s);
            if (why != null) return SaveWriteOutcome.Block(why == "" ? null : why);
            var key = targetKey ?? FreeManualKey();
            if (key == null) return SaveWriteOutcome.Block(SaveSlots.ManualFullText);
            if (SaveSlots.KindOf(key) != SaveSlotKind.Manual || !SaveSlots.ManualKeys().Contains(key)) return SaveWriteOutcome.Block(null);
            return WriteManual(key, Capture(s), nowMs);
        }

        /// <summary>手动存档子页的「重试」（Q385：仍要通过安全空闲、写权限与目标槽版本校验）。</summary>
        public SaveWriteOutcome RetryManual(GameState s, long nowMs)
        {
            if (!InSession || ManualFailure == null) return SaveWriteOutcome.Block(null);
            var why = ManualBlockedText(s);
            if (why != null) return SaveWriteOutcome.Block(why == "" ? null : why);
            var f = ManualFailure;
            return WriteManual(f.Key, f.Snap, nowMs);
        }

        /// <summary>离开手动存档子页：清除该次失败提示与未执行的重试（Q406）。</summary>
        public void ClearManualFailure() => ManualFailure = null;

        private SaveWriteOutcome WriteManual(string key, Snapshot snap, long nowMs)
        {
            var r = WriteSlot(key, snap, nowMs);
            if (r.Status == SaveWriteStatus.Blocked) return r;
            ManualFailure = r.Failure;
            if (r.Failure != null) r.Failure.Kind = SaveSlotKind.Manual;
            return r;
        }

        private string? FreeManualKey()
        {
            foreach (var key in SaveSlots.ManualKeys())
            {
                string? raw;
                try { raw = _st.Get(key); }
                catch (Exception) { continue; }
                if (string.IsNullOrEmpty(raw)) return key;
            }
            return null;
        }

        // ───────────── 菜单三行 ─────────────

        /// <summary>Q387 菜单三行：每5分钟、每日06:00、最新一份手动档的保存时刻。</summary>
        public SlotTime Auto5Time() => TimeOf(SaveSlots.Auto5Key);

        public SlotTime DailyTime() => TimeOf(SaveSlots.DailyKey);

        public SlotTime LatestManualTime()
        {
            var t = new SlotTime();
            foreach (var key in SaveSlots.ManualKeys())
            {
                var one = TimeOf(key);
                if (one.ReadFailed) t.ReadFailed = true;
                if (!one.Exists) continue;
                t.Exists = true;
                if (one.SavedAtMs is long ms && (t.SavedAtMs == null || ms > t.SavedAtMs)) t.SavedAtMs = ms;
            }
            return t;
        }

        private SlotTime TimeOf(string key)
        {
            var t = new SlotTime();
            string? raw;
            try { raw = _st.Get(key); }
            catch (Exception) { t.ReadFailed = true; return t; }
            if (string.IsNullOrEmpty(raw)) return t;
            t.Exists = true;
            try { t.SavedAtMs = SaveSlotEnvelope.ParseHeader(raw!).SavedAtMs; }
            catch (Exception) { /* 时刻读不出：保存时间未记录 */ }
            return t;
        }

        // ───────────── 写权限 ─────────────

        /// <summary>重新确认写权限（另一页面写了令牌时转为失权；Q381）。返回当前状态。</summary>
        public SavePermission CheckPermission()
        {
            HoldsPermission();
            return Permission;
        }

        /// <summary>
        /// 「在此页面继续保存」（Q381 / Q408）：以本页最后确认持有写权限时所见的各槽版本为基线重验；任一槽有较新快照则拒绝（拒绝句常驻、
        /// 隐藏接回键）；通过才转移权限，原待写任务、失败与计时按原时间轴恢复（已过一个或多个周期只请求一次）。
        /// </summary>
        public SavePermission Reclaim(long nowMs)
        {
            if (!InSession || Permission != SavePermission.Lost) return Permission;
            foreach (var key in SaveSlots.AllSlotKeys())
            {
                _baseline.TryGetValue(key, out var seen);
                if (ReadRev(key, out var now) && now != seen)
                {
                    Permission = SavePermission.Refused;
                    return Permission;
                }
            }
            Claim();
            Tick(SessionId, nowMs);
            return Permission;
        }

        private void Claim()
        {
            try { _st.Set(SaveSlots.OwnerKey, PageToken); }
            catch (Exception)
            {
                // 令牌写不下时清掉旧令牌：没有其他页面登记即视为本页持有
                try { _st.Remove(SaveSlots.OwnerKey); } catch (Exception) { /* 无法确认，按持有处理 */ }
            }
            Permission = SavePermission.Holding;
        }

        private bool HoldsPermission()
        {
            if (Permission != SavePermission.Holding) return false;
            string? owner;
            try { owner = _st.Get(SaveSlots.OwnerKey); }
            catch (Exception) { return true; }
            if (!string.IsNullOrEmpty(owner) && owner != PageToken) Permission = SavePermission.Lost;
            return Permission == SavePermission.Holding;
        }

        private void CaptureBaseline()
        {
            _baseline.Clear();
            foreach (var key in SaveSlots.AllSlotKeys())
                _baseline[key] = ReadRev(key, out var rev) ? rev : null;
        }

        /// <summary>读一个槽的版本：不存在为 null；头部读不出时用原文校验和代替（内容变化同样能看出）。读失败返回 false。</summary>
        private bool ReadRev(string key, out string? rev) => ReadRev(key, out rev, out _);

        private bool ReadRev(string key, out string? rev, out string? error)
        {
            rev = null;
            error = null;
            string? raw;
            try { raw = _st.Get(key); }
            catch (Exception ex) { error = ex.Message; return false; }
            if (string.IsNullOrEmpty(raw)) return true;
            try
            {
                var r = SaveSlotEnvelope.ParseHeader(raw!).Rev;
                rev = string.IsNullOrEmpty(r) ? "raw:" + SaveSlots.Checksum(raw!) : r;
            }
            catch (Exception) { rev = "raw:" + SaveSlots.Checksum(raw!); }
            return true;
        }

        // ───────────── 写入 ─────────────

        private Snapshot Capture(GameState s) => new Snapshot
        {
            Json = SaveSerializer.Serialize(s),
            Summary = Engine.SaveSummaryOf(s),
            Schema = s.SchemaVersion,
            Seq = _commitSeq,
        };

        /// <summary>
        /// 写一个槽（权限、目标版本检查与实际写入串行，Q381）：失权或目标已被别的页面更新时不写（后者按 Q381 拒绝接回）；
        /// 成功才更新基线、底栏时刻与未保存标记；失败保留原槽。
        /// </summary>
        private SaveWriteOutcome WriteSlot(string key, Snapshot snap, long nowMs)
        {
            if (!HoldsPermission()) return SaveWriteOutcome.Block(WriteBlockedText);
            _baseline.TryGetValue(key, out var seen);
            if (!ReadRev(key, out var cur, out var readError))
                return SaveWriteOutcome.Fail(MakeFailure(key, snap, false, readError));
            if (cur != seen)
            {
                Permission = SavePermission.Refused;
                return SaveWriteOutcome.Block(WriteBlockedText);
            }
            var env = new SaveSlotEnvelope
            {
                Slot = key,
                Rev = _newRev(),
                SavedAtMs = nowMs,
                Schema = snap.Schema,
                Summary = snap.Summary,
                Checksum = SaveSlots.Checksum(snap.Json),
                State = snap.Json,
            };
            try { _st.Set(key, env.ToJson()); }
            catch (SaveStorageWriteException ex)
            {
                return SaveWriteOutcome.Fail(MakeFailure(key, snap, SaveWriteError.IsQuotaExceeded(ex.Category, ex.Message), ex.Message));
            }
            catch (Exception ex)
            {
                return SaveWriteOutcome.Fail(MakeFailure(key, snap, SaveWriteError.IsQuotaExceeded(null, ex.Message), ex.Message));
            }
            _baseline[key] = env.Rev;
            LastSavedAtMs = nowMs;
            if (snap.Seq == _commitSeq) _savedSeq = _commitSeq;
            return SaveWriteOutcome.Success();
        }

        private static SaveFailure MakeFailure(string key, Snapshot snap, bool quota, string? message) => new SaveFailure
        {
            Kind = SaveSlots.KindOf(key),
            Key = key,
            Quota = quota,
            Message = message,
            Snap = snap,
        };
    }
}
