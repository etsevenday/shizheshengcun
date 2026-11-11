using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Survival.Core.Config;
using Survival.Core.State;

namespace Survival.Core.Save
{
    /// <summary>不可读与导入失败的原因类别（S01「B40 Q403–Q411 第二轮定稿」Q403 第一张表）。</summary>
    public enum SaveProblemKind
    {
        None,
        TooNew,
        Unsupported,
        Unrecognized,
        Corrupt,
        QuotaFull,
        WriteFailed,
        ReadbackMismatch,
        TargetNewer,
        ReadFailed,
        Conflict,
    }

    /// <summary>Q403 原因正文（不含「这份存档无法读取：」「这份旧存档无法升级：」前缀）。</summary>
    public static class SaveProblems
    {
        public const string Unsupported = "当前游戏不支持这个存档版本。";
        public const string Unrecognized = "存档版本无法识别。";
        public const string Corrupt = "存档内容已损坏。";
        public const string QuotaFull = "浏览器存储空间已满。";
        public const string WriteFailed = "浏览器未能写入存档，请重试。";
        public const string ReadbackMismatch = "写入后读回的内容不一致。";
        public const string TargetNewer = "每5分钟自动存档中已有较新的进度。";
        public const string ReadFailed = "浏览器暂时无法读取存档，请稍后重试。";
        public const string ConflictGeneric = "存档中的物品或设施记录存在冲突，无法安全升级。";

        public static string TooNew(string saveVersion) =>
            $"存档版本{saveVersion}比当前游戏支持的版本{GameState.CurrentSchema}新。请刷新页面更新游戏后再试。";

        /// <summary>迁移冲突：有可确认的明细时逐句排列（已去重），否则通用句。</summary>
        public static string Conflict(SaveMigrationConflictException ex) =>
            ex.PlayerReasons.Count > 0 ? string.Concat(ex.PlayerReasons.Distinct()) : ConflictGeneric;

        /// <summary>
        /// 按存档结构版本分类（不做迁移）：缺失或不是纯数字、也不是旧原型版本 → 无法识别；数字大于当前 → 过新；
        /// 数字小于可迁移起点 → 已识别但不支持；其余 → None（当前版本或可迁移）。
        /// </summary>
        public static SaveProblemKind ClassifyVersion(string? version)
        {
            if (string.IsNullOrEmpty(version)) return SaveProblemKind.Unrecognized;
            if (version == SaveMigrator.LegacySchema) return SaveProblemKind.None;
            if (!int.TryParse(version, NumberStyles.None, CultureInfo.InvariantCulture, out var v)) return SaveProblemKind.Unrecognized;
            var cur = int.Parse(GameState.CurrentSchema, CultureInfo.InvariantCulture);
            if (v > cur) return SaveProblemKind.TooNew;
            if (v == cur || SaveSerializer.IsMigratable(version)) return SaveProblemKind.None;
            return SaveProblemKind.Unsupported;
        }
    }

    /// <summary>读一份局内快照的结果：成功给状态，失败给类别与原因正文。</summary>
    public sealed class SnapshotRead
    {
        public GameState? State { get; set; }
        public SaveProblemKind Problem { get; set; }
        /// <summary>Q403 原因正文（不含前缀）；成功时为 null。</summary>
        public string? Reason { get; set; }
        public List<string> MigrationNotes { get; set; } = new List<string>();
        public string? Schema { get; set; }

        public bool Ok => State != null;
    }

    /// <summary>选档页的一张卡（Q335 / Q384 / Q404 / Q405 / Q407）。</summary>
    public sealed class SaveSlotEntry
    {
        public string Key { get; set; } = "";
        public SaveSlotKind Kind { get; set; }
        public string TypeText => SaveSlots.Label(Kind);
        public bool Readable { get; set; }
        /// <summary>不可读或旧版存档的完整原因句（含前缀）；可读时为 null。</summary>
        public string? ProblemText { get; set; }
        public SaveProblemKind Problem { get; set; }
        /// <summary>现实保存时刻（UTC 毫秒）；只在 <see cref="SavedAtKnown"/> 且有记录时有值。</summary>
        public long? SavedAtMs { get; set; }
        /// <summary>保存时刻这一栏能否可靠读出：false 时省略该字段（Q404）；true 且 SavedAtMs 为 null 时写「保存时间未记录」。</summary>
        public bool SavedAtKnown { get; set; }
        public int? Minute { get; set; }
        /// <summary>「第{世界日}日 · {时刻}」；读不出时为 null（省略）。</summary>
        public string? DayTimeText => Minute is int m ? SaveSlots.DayTimeText(m) : null;
        public string? Location { get; set; }
        public bool Ended { get; set; }
        public string? Rev { get; set; }
    }

    /// <summary>选档页数据：自动区（每5分钟、每日06:00、旧版存档在最后）与手动区（按成功保存时间从新到旧）。</summary>
    public sealed class SaveCatalogView
    {
        public List<SaveSlotEntry> Auto { get; } = new List<SaveSlotEntry>();
        public List<SaveSlotEntry> Manual { get; } = new List<SaveSlotEntry>();

        public IEnumerable<SaveSlotEntry> All => Auto.Concat(Manual);
        /// <summary>没有可读档时显示 <see cref="SaveSlots.EmptyText"/>（不可读档仍列出）。</summary>
        public bool HasReadable => All.Any(e => e.Readable);
    }

    /// <summary>读取一个槽（选档页、手动存档子页、读档共用）。</summary>
    public static class SaveCatalog
    {
        /// <summary>
        /// 解析并（必要时）迁移一份局内快照 JSON。当前版本直接读；可迁移的旧版本走现有迁移链（只在内存里，不写回）。
        /// </summary>
        public static SnapshotRead ReadSnapshot(string stateJson, GameConfig config)
        {
            var r = new SnapshotRead();
            string? version;
            try { version = ProbeSchema(stateJson); }
            catch { return Fail(r, SaveProblemKind.Corrupt, SaveProblems.Corrupt); }
            r.Schema = version;
            var kind = SaveProblems.ClassifyVersion(version);
            if (kind == SaveProblemKind.TooNew) return Fail(r, kind, SaveProblems.TooNew(version!));
            if (kind == SaveProblemKind.Unsupported) return Fail(r, kind, SaveProblems.Unsupported);
            if (kind == SaveProblemKind.Unrecognized) return Fail(r, kind, SaveProblems.Unrecognized);
            try
            {
                r.State = SaveSerializer.Deserialize(stateJson, config, out var notes);
                r.MigrationNotes = notes;
                return r;
            }
            catch (SaveMigrationConflictException ex) { return Fail(r, SaveProblemKind.Conflict, SaveProblems.Conflict(ex)); }
            catch (Exception) { return Fail(r, SaveProblemKind.Corrupt, SaveProblems.Corrupt); }
        }

        private static SnapshotRead Fail(SnapshotRead r, SaveProblemKind k, string reason)
        {
            r.State = null;
            r.Problem = k;
            r.Reason = reason;
            return r;
        }

        /// <summary>局内快照的 SchemaVersion（不是 JSON 对象时抛异常）。</summary>
        public static string? ProbeSchema(string stateJson) => SaveSerializer.SchemaVersionOf(stateJson);

        /// <summary>
        /// 读一个多存档槽的卡面（不迁移当前版本的档；只有需要迁移的旧版本才整份试读，以便列出迁移冲突）。
        /// 槽不存在返回 null。
        /// </summary>
        public static SaveSlotEntry? ReadEntry(ISaveStorage storage, string key, GameConfig config)
        {
            var e = new SaveSlotEntry { Key = key, Kind = SaveSlots.KindOf(key) };
            string? raw;
            try { raw = storage.Get(key); }
            catch (Exception) { return Problem(e, SaveProblemKind.ReadFailed, SaveProblems.ReadFailed); }
            if (string.IsNullOrEmpty(raw)) return null;
            SaveSlotEnvelope head;
            try { head = SaveSlotEnvelope.ParseHeader(raw!); }
            catch (Exception) { return Problem(e, SaveProblemKind.Corrupt, SaveProblems.Corrupt); }
            e.Rev = head.Rev;
            e.SavedAtKnown = true;
            e.SavedAtMs = head.SavedAtMs;
            ApplySummary(e, head.Summary);
            var kind = SaveProblems.ClassifyVersion(head.Schema);
            if (kind == SaveProblemKind.TooNew) return Problem(e, kind, SaveProblems.TooNew(head.Schema!));
            if (head.Format != SaveSlotEnvelope.CurrentFormat) return Problem(e, SaveProblemKind.Unrecognized, SaveProblems.Unrecognized);
            if (kind == SaveProblemKind.Unsupported) return Problem(e, kind, SaveProblems.Unsupported);
            if (kind == SaveProblemKind.Unrecognized) return Problem(e, kind, SaveProblems.Unrecognized);
            // 完整性：快照正文的校验和
            SaveSlotEnvelope full;
            try { full = SaveSlotEnvelope.Parse(raw!); }
            catch (Exception) { return Problem(e, SaveProblemKind.Corrupt, SaveProblems.Corrupt); }
            if (string.IsNullOrEmpty(full.State) || SaveSlots.Checksum(full.State) != full.Checksum)
                return Problem(e, SaveProblemKind.Corrupt, SaveProblems.Corrupt);
            if (head.Schema != GameState.CurrentSchema)
            {
                var read = ReadSnapshot(full.State, config);
                if (!read.Ok) return Problem(e, read.Problem, read.Reason!);
            }
            e.Readable = true;
            return e;
        }

        private static void ApplySummary(SaveSlotEntry e, SaveSlotSummary? s)
        {
            if (s == null) return;
            e.Minute = s.Minute;
            e.Location = string.IsNullOrEmpty(s.Location) ? null : s.Location;
            e.Ended = s.Ended == true;
        }

        private static SaveSlotEntry Problem(SaveSlotEntry e, SaveProblemKind k, string reason)
        {
            e.Readable = false;
            e.Problem = k;
            e.ProblemText = SaveSlots.UnreadablePrefix + reason;
            return e;
        }

        /// <summary>
        /// 选档页列表：自动区按「每5分钟」「每日06:00」顺序、导入失败的「旧版存档」排最后（Q404）；手动区按成功保存时间从新到旧，
        /// 读不出保存时刻的排在后面。只列已有档。legacy 为本页旧单槽导入的结果（没有失败卡时为 null）。
        /// </summary>
        public static SaveCatalogView List(ISaveStorage storage, GameConfig config, LegacyImportResult? legacy)
        {
            var v = new SaveCatalogView();
            foreach (var key in new[] { SaveSlots.Auto5Key, SaveSlots.DailyKey })
                if (ReadEntry(storage, key, config) is SaveSlotEntry e) v.Auto.Add(e);
            if (legacy?.FailedCard is SaveSlotEntry card) v.Auto.Add(card);
            v.Manual.AddRange(ManualEntries(storage, config));
            return v;
        }

        /// <summary>已有手动档（Q405 子页与选档页手动区共用），按成功保存时间从新到旧。</summary>
        public static List<SaveSlotEntry> ManualEntries(ISaveStorage storage, GameConfig config)
        {
            var list = new List<SaveSlotEntry>();
            foreach (var key in SaveSlots.ManualKeys())
                if (ReadEntry(storage, key, config) is SaveSlotEntry e) list.Add(e);
            return list
                .OrderByDescending(e => e.SavedAtMs.HasValue)
                .ThenByDescending(e => e.SavedAtMs ?? 0)
                .ThenBy(e => e.Key, StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>读档：返回所选槽的完整状态（必要时内存迁移，不写回任何槽），失败给完整原因句。</summary>
        public static SlotLoad Load(ISaveStorage storage, string key, GameConfig config)
        {
            var res = new SlotLoad { Key = key };
            string? raw;
            try { raw = storage.Get(key); }
            catch (Exception) { return res.Fail(SaveProblemKind.ReadFailed, SaveProblems.ReadFailed); }
            if (string.IsNullOrEmpty(raw)) return res.Fail(SaveProblemKind.Corrupt, SaveProblems.Corrupt);
            SaveSlotEnvelope env;
            try { env = SaveSlotEnvelope.Parse(raw!); }
            catch (Exception) { return res.Fail(SaveProblemKind.Corrupt, SaveProblems.Corrupt); }
            var kind = SaveProblems.ClassifyVersion(env.Schema);
            if (kind == SaveProblemKind.TooNew) return res.Fail(kind, SaveProblems.TooNew(env.Schema!));
            if (env.Format != SaveSlotEnvelope.CurrentFormat) return res.Fail(SaveProblemKind.Unrecognized, SaveProblems.Unrecognized);
            if (string.IsNullOrEmpty(env.State) || SaveSlots.Checksum(env.State) != env.Checksum)
                return res.Fail(SaveProblemKind.Corrupt, SaveProblems.Corrupt);
            var read = ReadSnapshot(env.State, config);
            if (!read.Ok) return res.Fail(read.Problem, read.Reason!);
            res.State = read.State;
            res.Rev = env.Rev;
            res.Envelope = env;
            // Q410：旧单槽导入档首次成功读取的一次性提示（读取失败不消耗）
            if (key == SaveSlots.Auto5Key && !string.IsNullOrEmpty(env.LegacyFrom))
            {
                string? pending = null;
                try { pending = storage.Get(SaveSlots.UpgradeNoticeKey); } catch (Exception) { /* 读不出就不提示 */ }
                if (!string.IsNullOrEmpty(pending) && pending == env.Rev)
                {
                    res.UpgradeNotice = SaveSlots.UpgradeNotice(GameState.CurrentSchema);
                    try { storage.Remove(SaveSlots.UpgradeNoticeKey); } catch (Exception) { /* 删不掉下次还会提示一次 */ }
                }
            }
            return res;
        }
    }

    /// <summary>读档结果。</summary>
    public sealed class SlotLoad
    {
        public string Key { get; set; } = "";
        public GameState? State { get; set; }
        public string? Rev { get; set; }
        public SaveSlotEnvelope? Envelope { get; set; }
        public SaveProblemKind Problem { get; set; }
        /// <summary>失败时的完整原因句（「这份存档无法读取：」+ Q403 原因）。</summary>
        public string? ProblemText { get; set; }
        /// <summary>Q410：仅导入档首次成功读取且确有升级时为「旧存档已升级到版本{当前存档版本}。」。</summary>
        public string? UpgradeNotice { get; set; }

        public bool Ok => State != null;

        internal SlotLoad Fail(SaveProblemKind k, string reason)
        {
            State = null;
            Problem = k;
            ProblemText = SaveSlots.UnreadablePrefix + reason;
            return this;
        }
    }
}
