using System;
using Newtonsoft.Json.Linq;
using Survival.Core.Config;
using Survival.Core.State;

namespace Survival.Core.Save
{
    /// <summary>旧单槽导入的结果。</summary>
    public sealed class LegacyImportResult
    {
        /// <summary>本次成功导入到每5分钟自动槽。</summary>
        public bool Imported { get; set; }
        /// <summary>导入失败时自动区最后的「旧版存档」卡（Q383 / Q404）；没有旧档、已导入过或成功时为 null。</summary>
        public SaveSlotEntry? FailedCard { get; set; }
        public SaveProblemKind Problem { get; set; }
        /// <summary>失败原因正文（不含前缀，开发诊断与测试用）。</summary>
        public string? Reason { get; set; }
    }

    /// <summary>
    /// Q383 / Q403 / Q410：打开游戏页面时静默把旧单槽经现有迁移链导入「自动存档 · 每5分钟」。
    /// 只有新槽已写入、读回一致、校验通过并记下已导入后，才删除旧主键与 .bak；失败保留旧键原样，给「旧版存档」卡和原因。
    /// 导入不覆盖多槽系统已存在的快照；重复打开不重导已完成的迁移；不补历史 06:00。
    /// </summary>
    public static class LegacySaveImport
    {
        /// <param name="summarize">由已迁移状态得到卡面摘要（地点名与品牌行同取，Q407）。</param>
        /// <param name="locationName">由地点 ID 取显示名（失败卡从旧原文尽量读摘要；读不出返回 null）。</param>
        /// <param name="newRev">新版本号生成器。</param>
        public static LegacyImportResult Run(ISaveStorage storage, GameConfig config,
            Func<GameState, SaveSlotSummary> summarize, Func<string, string?> locationName, Func<string> newRev)
        {
            var res = new LegacyImportResult();
            string? legacy;
            try { legacy = storage.Get(SaveSlots.LegacyKey); }
            catch (Exception) { return Fail(res, null, SaveProblemKind.ReadFailed, SaveProblems.ReadFailed, locationName); }
            if (string.IsNullOrEmpty(legacy)) return res;

            var sum = SaveSlots.Checksum(legacy!);
            if (AlreadyImported(storage, sum))
            {
                RemoveLegacy(storage);
                return res;
            }

            var read = SaveCatalog.ReadSnapshot(legacy!, config);
            if (!read.Ok) return Fail(res, legacy, read.Problem, read.Reason!, locationName);

            // 导入不覆盖多槽系统已存在的快照（含不可读的）
            string? existing;
            try { existing = storage.Get(SaveSlots.Auto5Key); }
            catch (Exception) { return Fail(res, legacy, SaveProblemKind.ReadFailed, SaveProblems.ReadFailed, locationName); }
            if (!string.IsNullOrEmpty(existing)) return Fail(res, legacy, SaveProblemKind.TargetNewer, SaveProblems.TargetNewer, locationName);

            var state = read.State!;
            var stateJson = SaveSerializer.Serialize(state);
            var env = new SaveSlotEnvelope
            {
                Slot = SaveSlots.Auto5Key,
                Rev = newRev(),
                SavedAtMs = null,   // 旧单槽没有可靠的现实保存时间
                Schema = state.SchemaVersion,
                LegacyFrom = read.Schema,
                LegacyChecksum = sum,
                Summary = summarize(state),
                Checksum = SaveSlots.Checksum(stateJson),
                State = stateJson,
            };
            var text = env.ToJson();
            try { storage.Set(SaveSlots.Auto5Key, text); }
            catch (SaveStorageWriteException ex)
            {
                var quota = SaveWriteError.IsQuotaExceeded(ex.Category, ex.Message);
                RemoveQuietly(storage, SaveSlots.Auto5Key);
                return Fail(res, legacy, quota ? SaveProblemKind.QuotaFull : SaveProblemKind.WriteFailed,
                    quota ? SaveProblems.QuotaFull : SaveProblems.WriteFailed, locationName);
            }
            catch (Exception ex)
            {
                var quota = SaveWriteError.IsQuotaExceeded(null, ex.Message);
                RemoveQuietly(storage, SaveSlots.Auto5Key);
                return Fail(res, legacy, quota ? SaveProblemKind.QuotaFull : SaveProblemKind.WriteFailed,
                    quota ? SaveProblems.QuotaFull : SaveProblems.WriteFailed, locationName);
            }

            // 读回并校验：内容一致、能完整读出
            string? back = null;
            try { back = storage.Get(SaveSlots.Auto5Key); } catch (Exception) { /* 视为不一致 */ }
            if (back != text)
            {
                RemoveQuietly(storage, SaveSlots.Auto5Key);
                return Fail(res, legacy, SaveProblemKind.ReadbackMismatch, SaveProblems.ReadbackMismatch, locationName);
            }
            var check = SaveCatalog.Load(new ReadOnlyView(storage), SaveSlots.Auto5Key, config);
            if (!check.Ok)
            {
                RemoveQuietly(storage, SaveSlots.Auto5Key);
                return Fail(res, legacy, SaveProblemKind.ReadbackMismatch, SaveProblems.ReadbackMismatch, locationName);
            }

            // 记已导入后才删旧主键与备份；记不下时新槽自带原文校验和，下次打开同样认作已导入
            try { storage.Set(SaveSlots.LegacyImportedKey, sum); } catch (Exception) { /* 见上 */ }
            // Q410：原版本 42 静默，原本就是当前版本也不提示；其余确有升级的导入档，首次成功读取提示一次
            if (SaveSerializer.AnnouncesUpgrade(legacy!))
            {
                try { storage.Set(SaveSlots.UpgradeNoticeKey, env.Rev); } catch (Exception) { /* 提示状态写不下只少一次提示 */ }
            }
            RemoveLegacy(storage);
            res.Imported = true;
            return res;
        }

        private static bool AlreadyImported(ISaveStorage storage, string legacySum)
        {
            try
            {
                if (storage.Get(SaveSlots.LegacyImportedKey) == legacySum) return true;
                var auto = storage.Get(SaveSlots.Auto5Key);
                if (string.IsNullOrEmpty(auto)) return false;
                return SaveSlotEnvelope.ParseHeader(auto!).LegacyChecksum == legacySum;
            }
            catch (Exception) { return false; }
        }

        private static void RemoveLegacy(ISaveStorage storage)
        {
            RemoveQuietly(storage, SaveSlots.LegacyKey);
            RemoveQuietly(storage, SaveSlots.LegacyBackupKey);
        }

        private static void RemoveQuietly(ISaveStorage storage, string key)
        {
            try { storage.Remove(key); } catch (Exception) { /* 删不掉不影响 */ }
        }

        private static LegacyImportResult Fail(LegacyImportResult res, string? legacy, SaveProblemKind kind, string reason, Func<string, string?> locationName)
        {
            res.Imported = false;
            res.Problem = kind;
            res.Reason = reason;
            var card = new SaveSlotEntry
            {
                Key = SaveSlots.LegacyKey,
                Kind = SaveSlotKind.Legacy,
                Readable = false,
                Problem = kind,
                ProblemText = SaveSlots.LegacyFailPrefix + reason,
                SavedAtKnown = true,   // Q404：旧版存档卡的现实保存时刻固定写「保存时间未记录」
                SavedAtMs = null,
            };
            if (legacy != null) ReadLegacySummary(card, legacy, locationName);
            res.FailedCard = card;
            return res;
        }

        /// <summary>从旧原文尽量读出可信摘要（世界时刻、当前地点、是否已结束）；读不出的字段省略（Q404）。</summary>
        private static void ReadLegacySummary(SaveSlotEntry card, string legacy, Func<string, string?> locationName)
        {
            JObject o;
            try { o = JObject.Parse(legacy); }
            catch (Exception) { return; }
            if (o["GameMinute"] is JToken m && m.Type == JTokenType.Integer) card.Minute = m.Value<int>();
            if (o["CurrentLocation"] is JToken l && l.Type == JTokenType.String)
            {
                var id = l.Value<string>();
                if (!string.IsNullOrEmpty(id)) card.Location = locationName(id!);
            }
            if (o["Progress"] is JObject p && p["Dead"] is JToken d && d.Type == JTokenType.Boolean) card.Ended = d.Value<bool>();
        }

        /// <summary>导入校验时只读，避免校验顺带消耗 Q410 提示。</summary>
        private sealed class ReadOnlyView : ISaveStorage
        {
            private readonly ISaveStorage _inner;
            public ReadOnlyView(ISaveStorage inner) => _inner = inner;
            public string? Get(string key) => key == SaveSlots.UpgradeNoticeKey ? null : _inner.Get(key);
            public void Set(string key, string value) { }
            public void Remove(string key) { }
        }
    }
}
